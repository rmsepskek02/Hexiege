# Research: Red Client 유닛 시각 동기화 버그 (BUG-A, BUG-B)

**작성일:** 2026-03-29
**이전 작업:** `14_00_initialize-overwrites-turn-to-face`, `15_00_deferred-red-client-bugs`

---

## 1. 전체 아키텍처 요약

### 1.1 유닛 이동·회전 흐름 (멀티플레이)

```
[서버 (Blue Host)]
UnitView.MoveAlongPath()
  ├─ i==1: ApplyDirection(dir) + GameEvents.OnUnitFacingChanged 발행
  │         + yield WaitForSeconds(_rotationDuration)
  ├─ Animator.CrossFade(Walk)
  ├─ GameEvents.OnUnitWalkStarted 발행
  └─ Lerp 이동 (매 프레임 transform.position 갱신 → NetworkTransform 자동 동기화)

      ↓ (RPC 전파)

[Red 클라이언트]
NetworkCombatController가 이벤트 수신 → ClientRpc 발행:
  ├─ TurnToFaceClientRpc(unitId, yAngle, duration)
  │     1. ResetMovementTracking()
  │     2. MarkTurnToFaceReceived()
  │     3. SetPreRotating(true)
  │     4. DOKill + DORotate(visualAngle, duration)
  │     5. OnComplete → SetPreRotating(false)
  │
  ├─ StartWalkAnimationClientRpc(unitId)
  │     코루틴: 유닛 등록 대기 → ResetPositionTracking() → StartWalkAnimation()
  │
  └─ StopWalkAnimationClientRpc(unitId)
        Idle CrossFade

NetworkUnit.LateUpdate() (Red 클라이언트 전용):
  ① ViewConverter.ToView()로 위치 반전
  ② _isPreRotating==false && _hasInitialPosition==true 일 때:
     delta 기반 Atan2 각도 계산 → RotateTowards(720°/s)
```

### 1.2 공격 흐름 (멀티플레이)

```
[서버]
NetworkCombatController.TickCombat() (0.05초 주기)
  └─ TryFindTarget(unit) 성공 시:
      1. TriggerAttackAnimationClientRpc(unitId, targetId, isUnit) 즉시 발행
      2. unit.AttackCooldownRemaining = unit.AttackCooldown
      3. StartCoroutine(DelayedAttackDamage(unit, targetId, isUnit, hitFrameTime))

[클라이언트]
TriggerAttackAnimationClientRpc:
  1. unitView.StopWalkAnimation()  ← Walk 즉시 정지
  2. unitView.TriggerAttackAnimation(targetId, isUnit)
       → SetAttackRotating(true)
       → CalculateAttackAngle() → PlayAttackAnimation(angle) 코루틴
       → DOKill + DORotate(attackAngle, duration)
       → OnComplete: SetAttackRotating(false)
       → Animator.CrossFade(Attack)
       → 클립 길이만큼 대기
       → _attackCoroutine = null
       → _isWalkPending이면 → yield null → StartWalkAnimation()
```

### 1.3 NetworkTransform 설정

| 항목 | 값 |
|------|-----|
| SyncPosition | XYZ 모두 ON |
| SyncRotAngle | **XYZ 모두 OFF** |
| Interpolate | ON (0.1s) |
| AuthorityMode | Server |
| InLocalSpace | OFF (World) |

**회전 동기화 비활성 이유:** NGO 보간 버퍼로 인해 ~1초 회전 지연이 발생하여, 클라이언트에서 로컬로 회전을 처리.

### 1.4 Apply Root Motion

모든 유닛 프리팹에서 `m_ApplyRootMotion: 0` (비활성). Root motion 커브(RootQ)는 Walk 클립에 존재하지만, Apply Root Motion OFF이므로 적용되지 않음.

---

## 2. BUG-A: 이동 중 방향 불일치 (Red Client)

### 2.1 증상 재확인

```
[TurnToFace OnComplete] unitId=0 finalAngle=150.0     ← DORotate 완료, rotation=150°
[LateUpdate] unitId=0 ... computedAngle=150.0 currentRot=270.0  ← 직후 rotation=270°
[LateUpdate] unitId=0 ... computedAngle=150.0 currentRot=150.0  ← 수 프레임 후 수렴
```

**핵심 모순:** DORotate가 Update에서 rotation을 150°로 설정 → OnComplete 확인. 그런데 같은 프레임의 LateUpdate에서 rotation이 270°로 읽힘.

### 2.2 Unity 프레임 실행 순서 분석

```
┌─────────────────────────────────────────────┐
│ Update                                       │
│   └─ DOTween 평가 → rotation = 150°         │
│      └─ OnComplete → SetPreRotating(false)   │
├─────────────────────────────────────────────┤
│ Internal Animation Update                    │  ← ★ 여기서 rotation 변경 가능
│   └─ Animator 상태 평가                      │
│      └─ Write Defaults ON → rotation 리셋?   │
├─────────────────────────────────────────────┤
│ LateUpdate                                   │
│   └─ NetworkUnit: currentRot = 270° 읽힘     │
└─────────────────────────────────────────────┘
```

Unity의 실행 순서에서 **Internal Animation Update**는 Update 이후, LateUpdate 이전에 실행된다. 이 단계에서 Animator가 rotation을 수정할 수 있다.

### 2.3 ★ 근본 원인 (유력 가설): Animator Write Defaults

**발견:** 모든 Animator Controller의 모든 상태(Idle, Walk, Attack, Dead)에 `m_WriteDefaultValues: 1`이 설정되어 있다.

| Controller | 상태 | WriteDefaults |
|------------|------|---------------|
| Assault | Idle, Walk, Attack, Dead 외 | 전부 1 |
| Sniper | Idle, Walk, Attack, Dead | 전부 1 |
| Pistoleer | Idle, Walk, Attack, Dead 외 | 전부 1 |

**Write Defaults 동작 원리:**
- Animator 상태가 활성일 때, 해당 클립이 **애니메이트하지 않는** 프로퍼티는 매 프레임 "기본값"으로 리셋
- "기본값" = Animator가 처음 평가될 시점의 프로퍼티 값
- Idle/Walk/Attack 클립 모두 루트 transform의 rotation을 직접 애니메이트하지 않음
  (Walk의 RootQ는 Root Motion 커브로, Apply Root Motion=OFF이면 적용 안 됨)

**BUG-A 발생 메커니즘:**

```
1. 유닛 스폰 → Initialize()가 rotation = 270° 설정 (Red 클라이언트 보정)
2. Animator 첫 평가 → rotation 270°를 "기본값"으로 캡처
3. TurnToFaceClientRpc → DORotate(150°) 시작 + SetPreRotating(true)
4. DORotate 진행 중:
   - Update: DOTween이 rotation을 보간 (270° → ... → 150°)
   - Internal Animation Update: Animator가 Write Defaults로 rotation을 270°로 리셋
   - LateUpdate: _isPreRotating=true → 건너뜀
   ※ DOTween은 매 Update마다 목표값으로 재보간하므로 Animator의 리셋을 매 프레임 덮어씀
   ※ 이 과정에서 DOTween과 Animator가 매 프레임 "150° vs 270°"를 번갈아 쓰는 떨림 발생 가능

5. DORotate 완료:
   - Update: DOTween이 최종 150° 설정 + OnComplete → SetPreRotating(false)
   - Internal Animation Update: ★ Animator가 rotation을 270°로 리셋
   - LateUpdate: _isPreRotating=false → currentRot=270° 읽힘 → RotateTowards 시작

6. 이후 매 프레임:
   - Update: 아무 동작 없음 (DORotate 완료됨)
   - Internal Animation Update: Animator가 270°로 리셋
   - LateUpdate: RotateTowards(270° → 150°) 적용
   - ★ 다음 프레임 Update: 아무것도 안 함
   - ★ Internal Animation Update: 다시 270°로 리셋!
   → 무한 반복: Animator(270°)와 LateUpdate(→150°)가 매 프레임 싸움
   → 육안으로는 270°에 가까운 방향으로 고정되다가 서서히 수렴하는 것처럼 보임
```

**이 가설이 로그와 일치하는 이유:**
- `finalAngle=150.0`: DOTween은 Update에서 150° 설정 후 OnComplete 실행 → 로그 출력 시점에 150° 맞음
- `currentRot=270.0`: LateUpdate 시점에는 이미 Internal Animation Update가 270°로 리셋한 후
- "수 프레임 후 수렴": RotateTowards가 720°/s로 매 프레임 ~12° 이동하나, Animator가 매 프레임 리셋하므로 완전한 수렴은 느려짐. 그러나 이동 중 NetworkTransform 위치 변경으로 delta가 커지면 RotateTowards의 영향력이 커져 점차 수렴

### 2.4 기존 수정 시도가 실패한 이유

| 시도 | 왜 실패 |
|------|---------|
| +180° 보정 | 수학은 정확. DORotate 목표값이 맞아도 Animator가 리셋하는 문제와 무관 |
| ResetPositionTracking | _isPreRotating 보호는 개선되었으나, Animator 리셋 문제와 무관 |
| HasReceivedTurnToFace | Initialize() 덮어쓰기 방지이나, Initialize()가 아닌 Animator가 범인 |

### 2.5 추가 확인 필요 사항

1. **Animator의 "기본값" 캡처 시점 정확한 확인:**
   - Animator가 enabled될 때인지, 첫 평가 시점인지
   - Initialize()가 rotation을 설정한 후 vs 전에 첫 평가가 일어나는지

2. **CrossFade 중 Write Defaults 동작:**
   - CrossFade로 상태 전환 중에도 Write Defaults가 적용되는지
   - 블렌딩 중에는 두 상태의 값이 혼합되는지

3. **Blue Host에서는 정상인 이유 재확인:**
   - 서버에서는 MoveAlongPath가 직접 transform.position을 Lerp로 갱신
   - LateUpdate 회전 로직이 서버에서 실행 안 됨 (IsServer return)
   - 서버 ApplyDirection의 DORotate도 Animator Write Defaults에 영향받을 수 있으나, 서버는 매 프레임 Lerp로 position을 갱신하므로 시각적으로 덜 두드러질 수 있음

---

## 3. BUG-B: 공격 이력 유닛의 Walk-in-place 현상

### 3.1 증상 재확인

이미 한 번 공격한 유닛이 이동 중 다시 공격 범위 진입 시:
1. 이동 멈춤 + 애니메이션 멈춤 (또는)
2. 제자리 Walk 재생 → 공격 전환

첫 공격 시에는 정상.

### 3.2 서버-클라이언트 RPC 타이밍 분석

```
[서버 MoveAlongPath - 적 범위 진입 시]
  └─ HasEnemyInRange() == true
     └─ while 루프 진입 (이동 일시 정지, Lerp 중단)
        ※ OnUnitWalkStopped 발행 안 함 (의도적)
        └─ TickCombat()이 TryFindTarget 성공 시:
           → TriggerAttackAnimationClientRpc 발행

[서버 MoveAlongPath - 적 범위 이탈 시]
  └─ while (_attackCoroutine != null) yield return null  ← 현재 공격 완료 대기
  └─ 이동 재개:
     1. Animator.CrossFade(Walk)
     2. GameEvents.OnUnitWalkStarted 발행  → StartWalkAnimationClientRpc
     3. GameEvents.OnUnitFacingChanged 발행 → TurnToFaceClientRpc
```

### 3.3 클라이언트 RPC 수신 시퀀스 문제

**정상 시나리오 (첫 공격):**
```
1. StartWalkAnimationClientRpc → Walk 시작
2. TurnToFaceClientRpc → DORotate 시작
3. (이동 중...)
4. TriggerAttackAnimationClientRpc → StopWalk + Attack 시작
5. (공격 완료)
6. StartWalkAnimationClientRpc → Walk 재개
7. TurnToFaceClientRpc → DORotate
```

**문제 시나리오 (재공격):**
```
1~5: 위와 동일
6. StartWalkAnimationClientRpc 도착
   → _attackCoroutine이 아직 살아있음 (Attack 클립 재생 중)
   → _isWalkPending = true
7. Attack 코루틴 종료: _attackCoroutine = null
   → _isWalkPending = true → yield return null (1프레임 대기)
   → _attackCoroutine == null → StartWalkAnimation() 호출
8. ★ 이 1프레임 사이에 TriggerAttackAnimationClientRpc가 도착하면:
   - StartWalkAnimation()이 호출되지 않아야 하지만... 타이밍에 따라 다름
```

### 3.4 _isWalkPending 상태 오염 가능성

[PlayAttackAnimation 코루틴](Assets/_Project/Scripts/Presentation/Unit/UnitView.cs#L701):
```
코루틴 시작 → DOKill + DORotate + Attack CrossFade → 클립 대기 → _attackCoroutine = null
→ if (_isWalkPending):
    _isWalkPending = false
    yield return null  ← ★ 1프레임 대기
    if (_attackCoroutine == null):
        StartWalkAnimation()  ← Walk 시작
```

**문제 지점:** `yield return null` 이후 `_attackCoroutine == null` 체크 사이에:
- 새로운 TriggerAttackAnimationClientRpc가 도착하면 → 새 _attackCoroutine 생성
- `_attackCoroutine == null` → false → Walk 안 시작 (이것은 정상)

하지만 **다른 경로의 문제:**
```
[시나리오: 서버의 전투 재개 후 이동]
서버: 전투 루프 탈출 → OnUnitWalkStarted + OnUnitFacingChanged 발행

클라이언트에서 RPC 도착 순서:
  A) TurnToFaceClientRpc (먼저)
     → ResetMovementTracking() → _isPreRotating=false
     → DORotate 시작 + SetPreRotating(true)

  B) StartWalkAnimationClientRpc (직후)
     → ApplyStartWalkWithRetry 코루틴 시작
     → 유닛 이미 등록됨 → yield 없이 즉시 진행
     → ResetPositionTracking() (isPreRotating 유지)
     → StartWalkAnimation()
        → ★ _attackCoroutine이 아직 살아있으면(이전 공격의 잔여 코루틴)
           _isWalkPending = true

  C) 이전 공격 코루틴이 종료됨 → _isWalkPending = true
     → yield null → StartWalkAnimation()
     → Walk CrossFade

  D) 50ms 이내 TriggerAttackAnimationClientRpc 도착 (서버 TickCombat)
     → StopWalkAnimation() → Walk 정지
     → TriggerAttackAnimation() → Attack 시작
```

**문제 핵심:** 시나리오 C와 D 사이의 타이밍 갭 동안:
- Walk 애니메이션이 시작된 상태에서 유닛이 이동하지 않음 (클라이언트는 NetworkTransform 위치 동기화 대기)
- 결과: **제자리 Walk 재생** (Walk-in-place)

### 3.5 추가 원인: Animator Write Defaults와의 복합 효과

BUG-A의 Write Defaults 문제가 BUG-B를 악화시킬 수 있음:
- 공격 완료 후 Walk로 CrossFade 시, Write Defaults가 rotation을 리셋
- rotation 리셋으로 유닛이 잘못된 방향을 향한 채 Walk → 시각적 혼란 가중
- 사용자에게 "유닛이 멈춰있다"고 인지될 수 있음

---

## 4. 관련 파일 목록

### 핵심 코드 파일
| 파일 | 역할 |
|------|------|
| `Scripts/Infrastructure/Network/NetworkUnit.cs` | LateUpdate 회전 보정, 프리-회전 플래그 관리 |
| `Scripts/Infrastructure/Network/NetworkCombatController.cs` | RPC 발행/수신, TurnToFace/Attack/Walk 동기화 |
| `Scripts/Presentation/Unit/UnitView.cs` | Initialize, 이동(MoveAlongPath), 공격 애니메이션 |
| `Scripts/Core/ViewConverter.cs` | Red팀 좌표 반전 |

### Animator 관련 파일
| 파일 | Write Defaults |
|------|---------------|
| `Animations/Units/Assault/Assault.controller` | 모든 상태 ON |
| `Animations/Units/Sniper/Sniper.controller` | 모든 상태 ON (Idle, Walk, Attack, Dead) |
| `Animations/Units/Pistoleer/Pistoleer.controller` | 모든 상태 ON |

### 프리팹 파일
| 파일 | Root Motion | NetworkTransform Rot |
|------|-------------|---------------------|
| `Prefabs/Units/Unit_*_Blue.prefab` | OFF | OFF |
| `Prefabs/Units/Unit_*_Red.prefab` | OFF | OFF |

---

## 5. 해결 방향 요약

### BUG-A 해결 핵심
1. **Animator Write Defaults를 OFF로 변경** — 모든 Controller의 모든 상태
2. 또는 **Animator 평가 후 LateUpdate에서 rotation을 강제 적용** (workaround)

### BUG-B 해결 핵심
1. **공격 코루틴 완료와 Walk 재개 사이의 상태 관리 개선**
2. **_isWalkPending 리셋 타이밍 정리** — 새 공격 시작 시 확실한 초기화
3. BUG-A 해결 시 Write Defaults에 의한 rotation 리셋이 제거되어 BUG-B의 시각적 심각도도 감소

---

## 6. 검증 방법

### BUG-A Write Defaults 가설 검증
1. **빠른 검증:** 에디터에서 Sniper Controller의 모든 상태 Write Defaults를 끄고 Red Client 테스트
2. **로그 검증:** Internal Animation Update 직후 rotation 값을 확인하는 로그 추가 (OnAnimatorMove 오버라이드)
3. **격리 검증:** 빈 씬에서 Animator + DOTween만으로 재현

### BUG-B RPC 타이밍 검증
1. 공격 이력 유닛에서 [Attack Complete] → [Walk Start] → [Attack Start] 로그 시퀀스 확인
2. _isWalkPending 상태 변화 로그 추가
