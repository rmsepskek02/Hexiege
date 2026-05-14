# Plan: 애니메이션 전환 부드럽게 개선 (Idle 클립 포함)

**작성일:** 2026-03-27 (재작성)
**최종 업데이트:** 2026-03-27 (구현 진행 중 — 테스트 미완료)

---

## 목표

Walk ↔ Idle ↔ Attack 간 CrossFade 블렌딩 적용.
Idle을 전환 허브로 사용하여 자연스러운 포즈 연결.

---

## 작업 순서

```
[1] Assault_Idle.anim 제작 (Inspector)        ← 미완료
[2] AnimatorController 3종 Inspector 설정      ← 부분 완료 (Sniper만)
[3] UnitView.cs 코드 수정                      ← 완료
[4] 테스트                                     ← 미완료
```

---

## [1] Assault_Idle.anim 제작 (사용자 직접) — ❌ 미완료

Sniper와 동일한 방식으로 제작:
1. 씬에 Assault 프리팹 배치
2. `Window > Animation > Animation` 열기
3. Assault의 Animator 오브젝트 선택
4. 클립 드롭다운 → "Create New Clip" → `Assault_Idle.anim` 저장 위치: `Animations/Units/Assault/`
5. Record 모드: 프레임 0, 30에 동일 포즈 키프레임 → Loop Time / Loop Pose ON

---

## [2] AnimatorController Inspector 설정 (사용자 직접)

**3종 공통 작업:**

### 상태 구성
- Idle 상태를 **Default State(기본 상태)** 로 설정
  - Controller에서 Idle 상태 우클릭 → "Set as Layer Default State"
- Motion 연결 확인:
  - Sniper: "Idle" 상태 → `Sniper_Idle.anim`
  - Assault: "Idle" 상태 → `Assault_Idle.anim`
  - Pistoleer: "Idle" 상태 → `meshy4/Pistoleer_Idle.anim`
- Loop Time / Loop Pose ON (각 Idle 클립 Inspector에서 확인)
- **Attack 클립: Loop Time = ON** (쿨다운 동안 자연스럽게 루프 재생)

### 트랜지션
`CrossFadeInFixedTime()`은 AnimatorController의 트랜지션 그래프를 완전히 우회하여 코드에서 직접 상태를 전환하므로 **트랜지션 추가 불필요.**

> ⚠️ 트랜지션을 추가하면 Has Exit Time=OFF + 조건 없음 조합 시 상태 진입 즉시 자동 전환되어 Walk↔Attack 루프 버그 발생
> 기존 `AnyState → Dead` 트랜지션만 유지

### Inspector 완료 현황

| 유닛 | Idle 클립 | Default State | Attack Loop | 트랜지션 |
|------|----------|--------------|-------------|---------|
| Sniper | ✅ Sniper_Idle.anim | ✅ 완료 | ✅ ON | ✅ 완료 |
| Assault | ❌ 미제작 | ❌ 미완료 | ❌ 미완료 | ❌ 미완료 |
| Pistoleer | ❓ 확인 필요 | ❌ 미완료 | ❌ 미완료 | ❌ 미완료 |

---

## [3] UnitView.cs 코드 수정 — ✅ 완료

### 원안 대비 실제 구현 차이점

#### 차이 1: Attack 종료 후 Idle CrossFade 제거
- **원안:** 공격 종료 시 `CrossFadeInFixedTime(StateIdle)` 호출
- **실제 구현:** 호출하지 않음
- **이유:** Attack 클립 Loop Time=ON으로 쿨다운 동안 자연스럽게 루프 재생됨. 종료 후 Idle CrossFade를 추가하면 연속 공격 시 Attack→Idle→Attack 스냅 발생

#### 차이 2: MoveAlongPath 전투 대기 진입 시 Idle CrossFade 제거
- **원안:** `speed=0` → Idle CrossFade로 교체
- **실제 구현:** 아무것도 하지 않음 (Walk 유지)
- **이유:** Walk→Idle→Attack 스냅 방지. Attack CrossFade가 Walk→Attack 직접 전환 처리

#### 차이 3: StopWalkAnimation() Idle CrossFade 추가 (멀티플레이 버그 수정)
- **원안에 없던 수정**
- **내용:** `StopWalkAnimation()`에 `CrossFadeInFixedTime(StateIdle)` 추가
- **이유:** 멀티플레이에서 StopWalkRpc 수신 후 TriggerAttackRpc 도착 전까지 Walk 애니메이션이 재생되는 "제자리 걷기" 현상 수정
  - TriggerAttackClientRpc는 같은 프레임에 StopWalk+TriggerAttack을 호출하므로 CrossFade(Idle)이 CrossFade(Attack)으로 즉시 덮어씌워짐 → 영향 없음
  - StopWalkRpc 단독 수신 시(쿨다운 대기 중)에만 실제로 Idle 재생

#### 차이 4: PlayAttackAnimation() Walk 시작 1프레임 대기 추가 (일관성 수정)
- **원안에 없던 수정**
- **내용:** `_isWalkPending` 시 Walk 시작 전 1프레임 대기 후 새 공격이 없을 때만 Walk 시작
- **이유:** 공격 이력이 있는 유닛과 없는 유닛의 Walk→Attack 전환이 다르게 동작하는 불일치 수정
  - 이전 공격 없는 유닛: Walk 안정 → 자연스러운 Attack 전환
  - 이전 공격 있는 유닛: Walk 막 시작 → 즉시 끊김 → 부자연스러운 전환 (멈춤현상)
  - 1프레임 대기로 새 TriggerAttackRpc 도착 여부 확인 → 두 경우 동일한 동작 보장

### 실제 구현된 코드 (주요 변경점)

**StopWalkAnimation():**
```csharp
_isWalkPending = false;
_animator.speed = 1f;
_animator.CrossFadeInFixedTime(StateIdle, _toIdleBlend, 0);
```

**PlayAttackAnimation() 종료부:**
```csharp
_attackCoroutine = null;
if (_isWalkPending)
{
    _isWalkPending = false;
    yield return null;  // 1프레임 대기 — 새 TriggerAttackRpc 도착 여부 확인
    if (_attackCoroutine == null)
        StartWalkAnimation();
}
```

**MoveAlongPath() 전투 대기 진입:**
```csharp
// Walk 애니메이션 유지 — Attack CrossFade가 Walk→Attack 직접 처리
GameEvents.OnUnitWalkStopped.OnNext(_unitData.Id);
```

**MoveAlongPath() 이동 완료:**
```csharp
_animator.speed = 1f;
_animator.CrossFadeInFixedTime(StateIdle, _toIdleBlend, 0);
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| Assault_Idle 클립 미제작 시 Idle 상태에 Motion 없음 | 클립 제작 후 연결 확인 후 테스트 |
| 짧은 Attack 클립(Assault 0.133초)에서 blendTime(0.08초)이 크면 Attack 포즈가 거의 안 보임 | Inspector에서 `_toAttackBlend = 0.05f`로 낮춰 조정 |
| Pistoleer Idle 클립/Controller 상태 미확인 | Sniper와 동일한 방식으로 Inspector 설정 필요 |

---

## Inspector 수치 기본값

| 필드 | 기본값 | 설명 |
|------|-------|------|
| `_toIdleBlend` | 0.15f | Walk/Attack → Idle 블렌딩 |
| `_idleToWalkBlend` | 0.10f | Idle → Walk 블렌딩 |
| `_toAttackBlend` | 0.08f | Walk/Idle → Attack 블렌딩 (빠른 반응) |

---

## 체크리스트

### Inspector (사용자 작업)
- [ ] Assault_Idle.anim 제작
- [ ] Assault AnimatorController: Idle Motion 연결, Default State, 트랜지션, Attack Loop ON
- [ ] Pistoleer AnimatorController: Idle Motion 연결 확인, Default State, 트랜지션, Attack Loop ON
- [x] Sniper AnimatorController: 전체 완료

### 코드
- [x] `StateIdle` 해시 + Inspector 필드 3개 추가
- [x] `StopWalkAnimation()` CrossFade(Idle) 추가
- [x] `StartWalkAnimation()` CrossFade(Walk) 교체
- [x] `PlayAttackAnimation()` CrossFade(Attack) + 1프레임 대기 Walk 시작
- [x] `MoveAlongPath()` Walk 시작/재개 CrossFade 교체
- [x] `MoveAlongPath()` 이동 완료 CrossFade(Idle) 교체
- [ ] 컴파일 확인 (Unity MCP 오류로 미확인)

### 테스트
- [ ] Sniper 싱글플레이: Walk→Attack→Walk 전환
- [ ] Sniper 멀티플레이: 제자리 걷기 현상 없음 확인
- [ ] Sniper 멀티플레이: 공격 이력 유무와 무관한 일관된 Walk→Attack 전환
- [ ] Assault / Pistoleer: Inspector 완료 후 동일 테스트
