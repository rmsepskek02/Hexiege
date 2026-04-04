# Research: Walk→Attack 전환 일관성 문제 및 스폰 회전 오류

**작성일:** 2026-03-28

---

## 증상 요약

1. **Walk→Attack 일관성 문제**: 공격 이력이 없는 유닛은 이동 중 적 감지 시 Walk→Attack 전환이 자연스럽지만,
   공격 이력이 있는 유닛은 동일한 상황에서 걷기 애니메이션이 제자리에서 잠깐 재생된 후 공격으로 전환됨.
   클라이언트 전용 현상. 멀티플레이.

2. **스폰 회전 오류**: 유닛 생성 직후 회전값이 잘못 설정되어 있음.
   클라이언트 전용 현상.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Presentation/Unit/UnitView.cs` | Walk/Attack 애니메이션, `_isWalkPending` 메커니즘 |
| `Infrastructure/Network/NetworkUnit.cs` | LateUpdate 델타 회전, `_isPreRotating` 플래그 |

---

## 버그 1: Walk→Attack 일관성 문제

### 원인

클라이언트에서 Walk 시작 경로가 두 가지로 분리되어 있음:

```
[경로 A] 미공격 유닛
  StartWalkRpc 도착 → _attackCoroutine == null → StartWalkAnimation() → Walk 즉시 시작
  Walk가 충분히 안정된 상태에서 적 감지 → TriggerAttackRpc → Attack 전환 (자연스러움)

[경로 B] 공격 경험 유닛
  StartWalkRpc 도착 → _attackCoroutine != null → _isWalkPending = true → 대기
                    → 공격 코루틴 종료 → _isWalkPending = true → 1프레임 대기
                    → StartWalkAnimation() → Walk 시작
  Walk가 방금 시작된 시점에 TriggerAttackRpc 도착 → 제자리 걷기 짧게 재생 → Attack
```

### `_isWalkPending` 메커니즘이 도입된 이유 (12_00 작업)

```csharp
// StartWalkAnimation()
if (_attackCoroutine != null)
{
    _isWalkPending = true;  // 공격 코루틴 종료 후 Walk 시작을 예약
    return;
}
```

`StartWalkRpc`가 공격 코루틴 실행 중에 도착하면 Walk가 즉시 시작되어
Attack + Walk 애니메이션이 동시에 재생되는 문제를 방지하기 위해 도입됨.

```csharp
// PlayAttackAnimation() 종료부
_attackCoroutine = null;
if (_isWalkPending)
{
    _isWalkPending = false;
    yield return null;  // 1프레임 대기 — 새 TriggerAttackRpc 도착 여부 확인
    if (_attackCoroutine == null)
        StartWalkAnimation();
}
```

### 왜 `_isWalkPending`이 문제인가

- 서버는 Walk/Attack을 RPC로 명령. 클라이언트는 이 RPC를 따르는 것이 원칙.
- 하지만 `_isWalkPending`은 서버 RPC를 무시하고 클라이언트 자체 타이밍으로 Walk를 지연시킴.
- 결과: 공격 이력에 따라 Walk 시작 시점이 달라져 Walk→Attack 전환이 비일관적.

### 올바른 구조

클라이언트 애니메이션은 서버 RPC를 단일 기준으로 따라야 한다:

```
StartWalkRpc 도착 → 즉시 Walk CrossFade (현재 어떤 상태든 관계없이)
TriggerAttackRpc 도착 → 즉시 Attack CrossFade (Walk를 덮어씌움)
```

CrossFade가 상태 전환의 부드러움을 담당하므로 클라이언트 버퍼링 불필요.

### Attack + Walk 동시 재생 우려 (반박)

`_isWalkPending` 도입 이유였던 "Attack + Walk 동시 재생" 문제:
- CrossFadeInFixedTime은 현재 상태에서 목표 상태로 블렌딩됨.
- Walk CrossFade가 시작되어도 Attack→Walk 블렌딩이 진행될 뿐 동시 재생이 아님.
- TriggerAttackRpc가 도착하면 Walk CrossFade를 즉시 Attack으로 덮어씌움.
- 시각적으로 자연스러운 전환.

---

## 버그 2: 이동 중 회전 방향 불일치

### 증상
- 클라이언트(Red팀) 유닛이 이동 시작부터 이동 경로 전체에서 잘못된 방향을 바라봄
- 서버(Host/Blue팀)는 정상

### 구조 파악

```
서버 (Blue팀, IsFlipped=false):
  MoveAlongPath → dir = ViewConverter.FlipDirection(dir)
                  → IsFlipped=false → 플립 없음 → 도메인 방향 그대로
  → TurnToFaceClientRpc(yAngle = DirectionAngles[도메인방향])
  예) Blue가 East(90°) 이동 → yAngle=90° 전송

Red 클라이언트 TurnToFaceClientRpc 수신:
  → DORotate(90°) → 유닛이 East(90°)를 바라봄

Red 클라이언트 LateUpdate:
  → ViewConverter.ToView(pos) → X,Z 반전된 뷰 좌표
  → delta = 반전된_현재 - 반전된_이전 → 반전된 이동 방향
  예) 서버 East 이동 → Red 뷰에서 West 방향 delta
  → Atan2(West_delta) - MeshYOffset ≈ 270° (West)
```

### 핵심 불일치

`TurnToFaceClientRpc`는 **도메인 기준 각도(90°)** 로 DORotate.
`LateUpdate`는 **반전된 위치 델타 기준(270°)** 으로 회전.

이 둘은 서로 다른 방향을 가리킴. 둘 중 하나가 Red 클라이언트에서 시각적으로 맞는 방향.

### 어느 쪽이 맞는가

ViewConverter는 **위치만** 반전 (카메라 회전 없음).
Red 클라이언트 카메라의 world orientation은 Blue와 동일.
유닛이 world East(90°)를 바라보면 두 팀 모두 화면에서 같은 방향으로 보임.
따라서:
- 서버가 East로 이동하는 Red 유닛 → Red 클라이언트에서도 East(90°) 바라봐야 함
- **TurnToFaceClientRpc의 90°가 정상**
- LateUpdate가 270°를 계산하는 것이 오류

### 수정 방향

LateUpdate 델타 회전이 반전된 뷰 좌표로 인해 항상 반대 방향을 계산함.
해결책: **이동 방향을 LateUpdate 델타 추정에 의존하지 않고, TurnToFaceRpc로만 제어.**
모든 타일 이동 시(i==1뿐 아니라 방향이 바뀌는 모든 스텝) `OnUnitFacingChanged` 이벤트 발행.

---

## 수정 방향 요약

| 버그 | 원인 | 수정 |
|------|------|------|
| Walk→Attack 일관성 | `_isWalkPending` 클라이언트 자체 타이밍 지연 | `_isWalkPending` 제거, StartWalkRpc 즉시 반영 |
| 스폰 회전 오류 | `ApplyDirection` DORotate가 LateUpdate 간섭으로 덮어씌워짐 | Initialize에서 즉시 회전 (DORotate 없이) |
