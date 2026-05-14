# Deferred Bug Report: Red Client 유닛 시각 동기화 버그

**작성일:** 2026-03-29
**상태:** 미해결 — OPUS 모드에서 재분석 필요
**이전 작업 문서:** `_Tasks/2026-03-29/14_00_initialize-overwrites-turn-to-face/`

---

## BUG-A: 이동 중 방향 불일치 (Red Client)

### 증상

Red 클라이언트에서 유닛이 이동할 때 실제 이동 방향과 유닛이 바라보는 방향이 어긋남.
이동 중 간헐적으로 올바른 방향으로 회전하지만, 이동 시작 시점부터 대부분 잘못된 방향을 유지함.

- Blue host(서버): 정상
- Red client: 이동 시작 시 spawn rotation(270°)을 유지하다가 서서히 이동 방향(150°)으로 회전

### 재현 조건

- 멀티플레이 (Host=Blue, Client=Red)
- 어떤 유닛이든 이동 시작 시 발생
- 첫 번째 유닛은 주로 정상, 이후 유닛들이 더 자주 발생

### 수집된 로그

```
// TurnToFaceClientRpc: 올바른 각도(150°)로 DORotate 완료됨
[TurnToFace] unitId=0 IsFlipped=True yAngle=330 visualAngle=150
[TurnToFace OnComplete] unitId=0 finalAngle=150.0

// 그러나 LateUpdate에서 rotation이 270°로 나타남
[LateUpdate] unitId=0 delta=(0.00,-0.07) computedAngle=150.0 currentRot=270.0
[LateUpdate] unitId=0 delta=(0.00,-0.05) computedAngle=150.0 currentRot=150.0  // 수 프레임 후 수렴
```

**핵심 관찰:**
- `TurnToFace OnComplete`에서 finalAngle=150° 정상 확인
- 바로 이후 LateUpdate에서 currentRot=270° — DORotate 결과가 사라짐
- LateUpdate의 computedAngle은 150°로 정확하게 계산됨
- RotateTowards가 270°→150°로 서서히 수렴하는 과정이 보임

### 관련 아키텍처

```
[서버 (Blue Host)]
UnitView.MoveAlongPath()
  → i==1에서 ApplyDirection(dir) + OnUnitFacingChanged 발행
    → NetworkCombatController가 구독 → TurnToFaceClientRpc(unitId, yAngle, rotationDuration) 전송
  → OnUnitWalkStarted 발행 → StartWalkAnimationClientRpc 전송

[Red Client]
TurnToFaceClientRpc:
  1. ResetMovementTracking() — _isPreRotating=false, _lateUpdateLogCount=0, HasReceivedTurnToFace=false
  2. MarkTurnToFaceReceived() — HasReceivedTurnToFace=true
  3. SetPreRotating(true)
  4. DOKill() + DORotate(visualAngle=yAngle+180°, 0.15s)
  5. OnComplete → SetPreRotating(false)

StartWalkAnimationClientRpc → ApplyStartWalkWithRetry:
  - 유닛 등록될 때까지 최대 1초 대기 (yield return null)
  - ResetPositionTracking() — _hasInitialPosition=false, _lateUpdateLogCount=0
  - StartWalkAnimation()

NetworkUnit.LateUpdate():
  ① Red 클라이언트 위치 보정: ViewConverter.ToView(transform.position)
  ② _isPreRotating=false && _hasInitialPosition=true 일 때만 delta 기반 rotation 계산
     → Atan2(delta.x, delta.z) - MeshYOffset(30f) = computedAngle
     → RotateTowards(current, target, 720°/s)
```

### 시도한 수정들

#### 시도 1: TurnToFaceClientRpc에 +180° 보정 추가 ✅ 수학은 정확함
```csharp
float visualAngle = ViewConverter.IsFlipped ? (yAngle + 180f) % 360f : yAngle;
```
- 로그 확인 결과 각도 계산은 올바름 (yAngle=330 → visualAngle=150)

#### 시도 2: ApplyStartWalkWithRetry의 ResetMovementTracking → ResetPositionTracking 교체 ✅ 적용됨
- 문제: yield 중에 TurnToFaceClientRpc가 SetPreRotating(true)를 설정한 뒤
  yield 후 ResetMovementTracking()이 _isPreRotating=false로 리셋하여 DORotate와 LateUpdate가 충돌
- ResetPositionTracking()은 _hasInitialPosition만 리셋, _isPreRotating 유지

#### 시도 3: HasReceivedTurnToFace 플래그 추가 ✅ 적용되었으나 효과 없음
- 가설: SpawnUnitClientRpc(→Initialize)가 TurnToFaceClientRpc보다 늦게 도착하여
  Initialize()가 rotation을 spawn rotation(270°)으로 덮어씌운다
- 수정: NetworkUnit에 HasReceivedTurnToFace 플래그 추가, Initialize()에서 체크
- 결과: 동작은 확인되었으나 육안으로 개선 없음

### 미해결 가설들

#### 가설 A: 공격 DORotate 후 재이동 시 rotation이 270°
공격 DORotate가 적 방향(270°)으로 회전. 공격 완료 후 재이동 시 TurnToFaceClientRpc가 오지만,
어떤 이유로 DORotate가 적용되지 않고 LateUpdate가 270°에서 시작.

#### 가설 B: NetworkTransform 초기 상태 동기화가 rotation 덮어씀
NGO NetworkTransform의 SyncRotAngleY=0이지만, 초기 스폰 동기화 시
서버의 rotation(90° 또는 330°)이 클라이언트에 적용될 가능성.
클라이언트의 Initialize()가 이를 270°로 설정하고, 이후 어느 시점에 서버 값이 재적용될 가능성.

#### 가설 C: 복수의 유닛 batch에서 RPC ordering 문제
6개 유닛이 동시에 스폰될 때 TurnToFaceClientRpc와 SpawnUnitClientRpc가 뒤섞여 처리됨.
HasReceivedTurnToFace 플래그가 특정 순서에서 작동 안 할 수 있음.

### OPUS 분석 시 추천 접근법

1. **`[RotJump]` 로그 확인** (코드에 이미 추가됨):
   `NetworkUnit.LateUpdate()` 맨 앞에 프레임 간 rotation 급변 감지 로그 있음.
   rotation이 150°→270°로 변경되는 시점과 함께 `isPreRot` 상태 확인.

2. **`[Initialize]` 로그 확인** (코드에 이미 추가됨):
   `HasReceivedTurnToFace` 상태와 before/after rotation 모두 출력됨.
   Initialize()가 실제로 덮어쓰는지, 가드가 동작하는지 확인.

3. **NetworkTransform 동작 검증**:
   NGO 2.9.2에서 SyncRotAngleY=0이어도 초기 스폰 시 rotation이 동기화되는지 확인.
   Unit 프리팹의 NetworkTransform 설정 재검토 필요.

4. **공격 후 재이동 시나리오 특정**:
   공격 이력이 있는 유닛의 TurnToFaceClientRpc 로그가 존재하는지 확인.
   존재하지 않으면 → TurnToFace가 drop되고 있음.
   존재하면 → DORotate가 어느 시점에 무효화됨.

---

## BUG-B: 공격 이력 유닛의 Walk-in-place 현상

### 증상

이미 한 번 공격한 이력이 있는 유닛이 이동 중 다시 공격 범위에 진입할 때:
1. 이동이 멈춘채로 애니메이션도 멈추거나
2. 제자리에서 Walk 애니메이션이 재생되다가
3. 공격 애니메이션으로 전환됨

- 공격 이력이 없는 유닛은 발생 안 함 (첫 공격 시 정상)

### 원인 분석

```
[서버 타이밍]
1. TickCombat → TryFindTarget 성공 → TriggerAttackAnimationClientRpc 전송
2. 동시에 MoveAlongPath가 walk를 계속 진행 중
3. OnUnitWalkStopped 이벤트 발행 → StopWalkAnimationClientRpc 전송

[클라이언트 수신]
- TriggerAttackAnimationClientRpc → unitView.StopWalkAnimation() + TriggerAttackAnimation()
- StopWalkAnimationClientRpc가 별도로 도착 (순서 비보장)

[공격 이력 유닛에서 추가 발생하는 현상]
- 공격 직전 StartWalkAnimationClientRpc가 도착하여 Walk 애니메이션 재시작
- 50ms 이내에 TriggerAttackAnimationClientRpc가 도착하지만
  이미 Walk 프레임이 재생된 뒤 Attack으로 전환됨
```

**_isWalkPending 패턴**: UnitView에 이미 구현되어 있으나 완전히 해결되지 않음.
- 공격 중 Walk 요청을 `_isWalkPending=true`로 저장, 공격 완료 시 StartWalkAnimation() 자동 호출
- 그러나 이전 공격의 `_isWalkPending` 상태가 다음 공격 시작 시 영향을 미칠 수 있음

### 관련 코드 위치

| 파일 | 위치 | 내용 |
|------|------|------|
| `NetworkCombatController.cs` | TickCombat() | 서버: TryFindTarget 성공 시 TriggerAttackAnimationClientRpc + DelayedAttackDamage |
| `NetworkCombatController.cs` | TriggerAttackAnimationClientRpc | StopWalkAnimation() + TriggerAttackAnimation() 순차 호출 |
| `UnitView.cs` | StartWalkAnimation() | _isWalkPending 패턴, 공격 중 대기 |
| `UnitView.cs` | PlayAttackAnimation() | 완료 시 _isWalkPending 체크 |

### OPUS 분석 시 추천 접근법

1. **공격 이력 유닛과 미이력 유닛의 상태 차이 파악**:
   - 공격 완료 후 `_isWalkPending`, `_attackCoroutine`, `_isPreRotating` 상태가 올바르게 초기화되는지 확인

2. **StartWalkAnimationClientRpc → TriggerAttackAnimationClientRpc 연속 도착 시나리오**:
   - 50ms 이내에 두 RPC가 연속 도착하는 경우 처리 로직 추적

3. **`_moveCoroutine` 상태 확인**:
   - 클라이언트에서 `_moveCoroutine`이 실행 중이 아님에도 Walk 애니메이션이 시작되는 경위
   - `StartWalkAnimation()`이 attack 완료 후 자동 호출될 때 timing 문제 가능성

---

## 공통 컨텍스트

### 주요 파일 경로

```
Infrastructure/Network/NetworkUnit.cs
Infrastructure/Network/NetworkCombatController.cs
Presentation/Unit/UnitView.cs
Core/ViewConverter.cs
```

### 핵심 아키텍처 규칙

- NGO 2.9.2, Enable Scene Management = ON
- NetworkTransform: Position만 동기화, Rotation 동기화 비활성(SyncRotAngleY=0)
- Red client: ViewConverter.IsFlipped=true → `transform.position = 2*mapCenter - pos`
- DirectionAngles = {30, 90, 150, 210, 270, 330} (NE~NW, root rotation.y)
- MeshYOffset = 30f (모든 유닛 프리팹 메시 자식 Y=30° 회전 보정)
- Red client 방향 보정: yAngle + 180°

### 현재 코드에 남아있는 진단 로그 (제거하지 않음)

| 로그 태그 | 파일 | 목적 |
|-----------|------|------|
| `[TurnToFace]` | NetworkCombatController | visualAngle 계산 확인 |
| `[TurnToFace OnComplete]` | NetworkCombatController | DORotate 완료 시 각도 확인 |
| `[LateUpdate]` (5회 제한) | NetworkUnit | delta 기반 각도 계산 확인 |
| `[RotJump]` | NetworkUnit | 프레임 간 rotation 급변 감지 |
| `[Initialize]` | UnitView | HasReceivedTurnToFace 가드 동작 확인 |

### 현재 코드 상태 (수정 이력)

- `NetworkUnit.ResetPositionTracking()` 메서드 추가됨 (ResetMovementTracking의 subset)
- `NetworkUnit.HasReceivedTurnToFace` + `MarkTurnToFaceReceived()` 추가됨
- `NetworkUnit.ResetMovementTracking()` HasReceivedTurnToFace=false 초기화 추가됨
- `NetworkCombatController.TurnToFaceClientRpc` MarkTurnToFaceReceived() 호출 추가됨
- `UnitView.Initialize()` HasReceivedTurnToFace 가드 추가됨
- `NetworkUnit.LateUpdate()` [RotJump] 로그 추가됨 (_prevLateUpdateRot 필드 포함)
- `UnitView.Initialize()` 상세 진단 로그 추가됨

---

## OPUS 모드 전환 방법

### VSCode 익스텐션 사용 중인 경우 (현재 환경)

채팅창 하단 모델 선택 영역에서 변경:

```
채팅 입력창 왼쪽 하단 → 모델명 클릭 → "claude-opus-4-6" 선택
```

또는 새 대화 시작 시 모델 선택.

### Claude Code CLI 사용 시

```bash
# 세션 시작 시 모델 지정
claude --model claude-opus-4-6

# 또는 대화 중 /model 명령
/model claude-opus-4-6
```

### OPUS 세션 시작 시 권장 프롬프트

아래 내용을 복사하여 OPUS 세션 첫 메시지로 전달:

```
이 프로젝트에서 미해결된 두 가지 버그를 처음부터 새롭게 분석해줘.

버그 상세 내용: Assets/_Project/Docs/_Tasks/2026-03-29/15_00_deferred-red-client-bugs/BugReport.md

주요 파일:
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs
- Assets/_Project/Scripts/Presentation/Unit/UnitView.cs

코드에 진단 로그([RotJump], [Initialize], [TurnToFace], [LateUpdate])가 이미 삽입되어 있음.
테스트 시 콘솔 로그를 수집하여 분석에 활용.
```
