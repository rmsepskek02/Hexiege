# Research: Walk→Attack 전환 멈춤 & 회전 불일치 (멀티플레이 클라이언트)

**작성일:** 2026-03-28

---

## 증상 요약

1. **멈춤현상**: 이동 중 적 감지 시 Walk → 잠깐 멈춤(Idle) → Attack 순서로 전환됨. 클라이언트만 발생.
2. **회전 불일치**: 유닛이 이동하는 방향과 바라보는 방향이 다른 현상. 클라이언트만 발생.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Presentation/Unit/UnitView.cs` | 이동(MoveAlongPath), Walk/Attack 애니메이션, 방향(ApplyDirection) |
| `Infrastructure/Network/NetworkCombatController.cs` | 서버→클라이언트 RPC 전송 (Walk/Stop/Attack/Facing) |
| `Infrastructure/Network/NetworkUnit.cs` | 클라이언트 LateUpdate 방향 계산, 회전 추적 |

---

## 시스템 구조 파악

### 서버와 클라이언트의 역할 분리

```
서버 (Host)
├─ MoveAlongPath 코루틴 실행 (이동 로직)
├─ ApplyDirection()으로 직접 회전
├─ TickCombat()으로 전투 판정 (50ms 주기)
└─ RPC 전송: StartWalk, StopWalk, TriggerAttack, TurnToFace

클라이언트
├─ NetworkTransform: 서버 위치 자동 동기화
├─ NetworkUnit.LateUpdate(): 위치 델타로 이동 방향 계산 → 회전 적용
├─ RPC 수신으로 Walk/Attack 애니메이션 제어
└─ TurnToFaceClientRpc: 이동 첫 타일 방향 설정 (DORotate)
```

### 클라이언트 회전 처리 방식

```
이동 첫 타일 (i == 1):
  서버 → TurnToFaceClientRpc → DORotate + _isPreRotating = true
  DORotate 완료 → _isPreRotating = false → LateUpdate 델타 회전 재개

이동 i > 1 타일:
  TurnToFaceClientRpc 없음 → LateUpdate 위치 델타로만 회전

공격:
  TriggerAttackAnimationClientRpc → PlayAttackAnimation → DOKill + DORotate(공격 각도)
  _isPreRotating 설정 없음 → LateUpdate가 간섭 가능
```

### 멀티플레이 Walk→Attack 전환 RPC 흐름

```
서버 MoveAlongPath: 적 감지
  → [A] GameEvents.OnUnitWalkStopped → StopWalkAnimationClientRpc 전송

서버 TickCombat (0~50ms 후): 쿨다운 = 0, 타겟 발견
  → [B] TriggerAttackAnimationClientRpc 전송
       (내부: StopWalkAnimation() + TriggerAttackAnimation() 동시 호출)

클라이언트:
  [A] 수신 → StopWalkAnimation() → CrossFade(Idle)
  [B] 수신 (0~50ms 후) → StopWalkAnimation() + TriggerAttackAnimation()
                         → CrossFade(Idle) + CrossFade(Attack) 동시 호출
```

---

## 버그 1: 멈춤현상 분석

### 근본 원인

`StopWalkAnimationClientRpc` [A]와 `TriggerAttackAnimationClientRpc` [B] 사이에 **0~50ms 간격** 존재.

- [A] 수신: `StopWalkAnimation()` → `CrossFade(Idle)` 시작
- 50ms 대기: Idle 애니메이션 재생 → 시각적으로 "멈춤" 현상
- [B] 수신: `CrossFade(Attack)` → [A]의 Idle CrossFade를 덮어씌움

`TriggerAttackAnimationClientRpc`[B] 내부에서 이미 `StopWalkAnimation()` + `TriggerAttackAnimation()`을 **같은 프레임**에 호출하고 있음. Attack CrossFade가 Idle을 즉시 덮어쓰므로 Walk→Attack 직접 전환 가능.

→ **[A] `StopWalkAnimationClientRpc`는 전투 감지 시 중복이자 멈춤현상의 원인.**

### 불필요한 이유

```csharp
// TriggerAttackAnimationClientRpc 내부 (이미 같은 프레임에 처리)
unitView.StopWalkAnimation();       // Walk 정지 + _isWalkPending = false
unitView.TriggerAttackAnimation();  // Attack CrossFade 시작
```

[A]가 없어도 [B]가 동일하게 처리함.

### [A]가 여전히 필요한 경우

```
이동 완료(경로 끝) → StopWalkAnimationClientRpc  ← 유지 필요
StopMovement() 강제 정지 → StopWalkAnimationClientRpc  ← 유지 필요
```

전투 감지 시의 StopWalkRpc **만** 제거 대상.

---

## 버그 2: 회전 불일치 분석

### 근본 원인 후보 (2가지)

#### 후보 A: 공격 DORotate 중 LateUpdate 간섭

```
상황: 적 감지 직후 NetworkTransform이 아직 움직임을 동기화 중 (1~2프레임)
      TriggerAttackAnimationClientRpc → PlayAttackAnimation → DORotate(공격 방향)
      LateUpdate: delta > 0.0001f (NetworkTransform 보간 잔류) → 이동 방향으로 회전 덮어씌움
결과: 공격 중 이동 방향을 바라봄
```

`TurnToFaceClientRpc`는 `_isPreRotating = true`로 LateUpdate를 차단하지만,
`PlayAttackAnimation`의 DORotate는 `_isPreRotating`을 설정하지 않음.

#### 후보 B: 전투 후 이동 재개 시 회전 지연

```
상황: 전투 후 이동 재개 시 서버는 ApplyDirection으로 즉시 회전
      클라이언트: TurnToFaceClientRpc 없음 (i > 1 구간) → LateUpdate 델타로만 회전
      LateUpdate RotateTowards(720 deg/s) → 180° 전환에 약 0.25초
결과: 이동 방향과 회전 방향이 최대 0.25초 동안 불일치
```

### 현재 LateUpdate 차단 조건

```csharp
// NetworkUnit.LateUpdate()
if (!_isPreRotating && _hasInitialPosition)
{
    // delta 기반 회전
}
```

`_isPreRotating = true`: `TurnToFaceClientRpc` DORotate 실행 중만 설정됨.
공격 중 DORotate → `_isPreRotating` 설정 없음 → 간섭 가능.

---

## 현재 코드 상태 (수정 이력)

```
최근 수정된 항목 (이번 작업 사이클 중):
- StopWalkAnimation(): speed=0 → CrossFade(Idle) 변경
- PlayAttackAnimation(): _isWalkPending 시 1프레임 대기 후 Walk 시작
- Attack 클립 Loop Time = ON 설정
- Idle 클립 제작 (Sniper, Assault, Pistoleer)
- AnimatorController Default State = Idle
```

---

## 수정 방향 요약

| 버그 | 원인 | 수정 위치 | 수정 내용 |
|------|------|----------|----------|
| 멈춤현상 | `StopWalkRpc` 중복 + 50ms 간격 | `UnitView.MoveAlongPath()` | 전투 감지 시 `OnUnitWalkStopped` 이벤트 발행 제거 |
| 회전 불일치 (후보A) | `PlayAttackAnimation` DORotate 중 LateUpdate 간섭 | `NetworkUnit` + `UnitView` | 공격 DORotate 시작 시 `_isPreRotating = true` 설정 추가 |
| 회전 불일치 (후보B) | 전투 후 이동 재개 시 방향 RPC 없음 | `UnitView.MoveAlongPath()` | 전투 후 Walk 재개 시 `OnUnitFacingChanged` 이벤트 추가 발행 |

> ⚠️ 후보 A와 B는 독립적으로 발생할 수 있으며 둘 다 수정 필요할 수 있음.
> Plan.md에서 우선순위와 구현 순서 결정.
