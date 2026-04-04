# Research: NetworkTransform Rotation 동기화로 전환

**작성일:** 2026-03-29
**이전 작업:** `16_00_red-client-rotation-walk-bugs` (미해결)

---

## 1. 문제 요약

### 미해결 버그 2건
- **BUG-A**: Red 클라이언트에서 유닛 이동 시 rotation이 매 프레임 리셋 (150° → 270°)
- **BUG-B**: 공격 이력 유닛의 Walk-in-place 현상

### 버그의 근본 원인
**"rotation 동기화 OFF → 클라이언트 자체 회전 계산"이라는 최초 설계 결정**에서 파생된 복잡성.

```
rotation 동기화 OFF (1초 딜레이 회피 목적)
  → 클라이언트 delta 기반 LateUpdate 회전 로직
    → DOTween과 LateUpdate 충돌 → _isPreRotating 플래그
      → Initialize()와 TurnToFace 순서 문제 → HasReceivedTurnToFace 플래그
        → Walk 재시작과 DORotate 충돌 → ResetPositionTracking 분리
          → 공격 DORotate와 이동 DORotate 충돌 → SetAttackRotating
            → 6겹의 workaround에도 버그 미해결
```

---

## 2. 왜 rotation 동기화를 껐었는가

`AddNetworkTransformToPrefabs.cs`에 기록된 이유:
> "NGO 보간 버퍼로 인해 ~1초 회전 딜레이 발생"

### 딜레이의 실제 원인 분석: 이중 보간

Position 동기화는 문제없이 동작 중. 같은 NetworkTransform, 같은 보간 설정(0.1초).

**Position이 정상인 이유:**
```
서버: transform.position = Vector3.Lerp(from, to, t)  ← 매 프레임 직접 갱신
NetworkTransform: 서버의 최종값을 0.1초 보간으로 전달
결과: 자연스러운 이동
```

**Rotation에서 딜레이가 발생했던 이유:**
```
서버: DORotate(targetAngle, 0.3초)  ← 서버에서 0.3초에 걸쳐 서서히 변경
NetworkTransform: 서버의 중간값을 다시 0.1초 보간으로 전달
결과: 서버 보간(0.3초) + 네트워크 보간(0.1초) = 체감 ~1초 이중 딜레이
```

**핵심 발견:** 서버의 DORotate가 이미 보간을 하는데, NetworkTransform이 그 중간값을 또 보간한 것이 원인.

### 해결법: 서버에서 rotation 즉시 스냅

```
서버: transform.rotation = Quaternion.Euler(0, targetAngle, 0)  ← 즉시 설정
NetworkTransform: 0.1초 보간으로 클라이언트에 전달
결과: 클라이언트에서 자연스러운 ~0.1초 보간 회전
```

서버에서 DORotate 대신 **즉시 스냅**하면:
- NetworkTransform 보간이 "부드러운 회전" 역할을 대신 수행
- 이중 보간 제거 → 딜레이 해소
- Position과 동일한 패턴 → 일관성

---

## 3. 현재 회전 관련 코드 전체 목록

### NetworkUnit.cs — 클라이언트 전용 회전 로직 (제거 대상)

| 코드 | 위치 | 역할 |
|------|------|------|
| `_isPreRotating` 필드 | L282 | DOTween 보호 플래그 |
| `SetPreRotating(bool)` | L261 | TurnToFace DORotate 보호 |
| `SetAttackRotating(bool)` | L270 | 공격 DORotate 보호 |
| `HasReceivedTurnToFace` | L88 | Initialize() 덮어쓰기 방지 |
| `MarkTurnToFaceReceived()` | L94 | 위 플래그 설정 |
| `ResetMovementTracking()` | L223 | 전체 회전 추적 리셋 |
| `ResetPositionTracking()` | L249 | 위치만 리셋 (회전 보호) |
| `_prevPosition`, `_hasInitialPosition` | L292-298 | delta 기반 회전 계산용 |
| `_lateUpdateLogCount` | L301 | 진단 로그 |
| `_prevLateUpdateRot` | L307 | 회전 급변 감지 |
| `MeshYOffset` 상수 | L314 | Atan2 보정 |
| `LateUpdate()` 전체 | L339-391 | ①위치 보정 ②delta 기반 회전 |

### NetworkCombatController.cs — TurnToFace RPC (제거 대상)

| 코드 | 위치 | 역할 |
|------|------|------|
| `_facingChangedSubscription` | L69 | OnUnitFacingChanged 구독 |
| `TurnToFaceClientRpc()` | L464-516 | 클라이언트 회전 DORotate |
| `ApplyStartWalkWithRetry()` 내 `ResetPositionTracking` | L427 | 회전 보호 유지 |

### UnitView.cs — 서버 DORotate (즉시 스냅으로 교체)

| 코드 | 위치 | 역할 |
|------|------|------|
| `ApplyDirection()` | L262-274 | DORotate로 방향 전환 |
| `PlayAttackAnimation()` 내 DORotate | L725-729 | 공격 방향 DORotate |
| `TriggerAttackAnimation()` 내 `SetAttackRotating` | L705 | 공격 DORotate 보호 |
| `_networkUnit` 참조 | L128 | NetworkUnit 캐시 |
| `_isWalkPending` | L136 | Walk 보류 플래그 |
| `MoveAlongPath()` 내 `OnUnitFacingChanged` 발행 | L427 | TurnToFace RPC 트리거 |
| `MoveAlongPath()` 전투 후 재개 시 `OnUnitFacingChanged` | L506 | 재이동 방향 동기화 |

### GameEvents.cs — FacingChanged 이벤트 (제거 대상)

| 코드 | 역할 |
|------|------|
| `OnUnitFacingChanged` Subject | TurnToFace 이벤트 |
| `UnitFacingChangedEvent` struct | 이벤트 데이터 |

---

## 4. NetworkTransform 현재 설정

```
SyncPositionX/Y/Z: true (정상 동작 중)
SyncRotAngleX: false → true로 변경 불필요 (Y축만 사용)
SyncRotAngleY: false → true로 변경
SyncRotAngleZ: false → true로 변경 불필요 (Y축만 사용)
Interpolate: true (0.1초 보간)
AuthorityMode: Server
```

---

## 5. 전환 후 회전 흐름

### 이동 방향 회전

```
[서버]
UnitView.MoveAlongPath()
  → ApplyDirection(dir)
    → transform.rotation = Quaternion.Euler(0, DirectionAngles[index], 0)  ← 즉시 스냅
  → NetworkTransform이 rotation을 클라이언트에 자동 보간 동기화

[클라이언트]
  NetworkTransform이 rotation을 0.1초 보간으로 수신 → 끝
  자체 회전 계산 없음
```

### 공격 방향 회전

```
[서버]
TriggerAttackAnimation()
  → CalculateAttackAngle()
  → transform.rotation = Quaternion.Euler(0, attackAngle, 0)  ← 즉시 스냅
  → NetworkTransform이 자동 동기화

[클라이언트]
  NetworkTransform이 rotation 수신
  TriggerAttackAnimationClientRpc로 공격 애니메이션만 동기화
```

### Red 클라이언트 회전 보정

NetworkTransform이 전달하는 것은 서버(Blue 기준) rotation.
Red 클라이언트는 position을 ViewConverter로 반전하므로, rotation도 +180° 보정이 필요.

```
[현재 NetworkUnit.LateUpdate()]
  ① position 반전: transform.position = ViewConverter.ToView(transform.position)
  ② rotation 계산 (제거 예정)

[전환 후]
  ① position 반전: 유지
  ② rotation 반전: transform.rotation에 +180° 보정 추가
```

이 보정은 LateUpdate에서 position 반전 직후에 수행. 간단한 1줄 추가.

---

## 6. LateUpdate 잔류 코드 — Red 클라이언트 위치 보정

LateUpdate의 회전 로직은 전부 제거하지만, **Red 클라이언트 위치 보정(ViewConverter.ToView)은 유지 필수**.

NetworkTransform이 서버(Blue 기준) position을 전달하므로, Red 클라이언트는 매 프레임 반전해야 함.
여기에 rotation 반전도 추가하면 LateUpdate의 역할이 명확해짐:

```csharp
private void LateUpdate()
{
    if (IsServer || !NetworkContext.IsNetworkActive) return;

    if (ViewConverter.IsFlipped)
    {
        // Red 클라이언트: 서버(Blue) 좌표를 Red 뷰로 반전
        transform.position = ViewConverter.ToView(transform.position);

        // Red 클라이언트: 서버(Blue) rotation에 +180° 보정
        Vector3 euler = transform.eulerAngles;
        euler.y = (euler.y + 180f) % 360f;
        transform.rotation = Quaternion.Euler(euler);
    }
}
```
