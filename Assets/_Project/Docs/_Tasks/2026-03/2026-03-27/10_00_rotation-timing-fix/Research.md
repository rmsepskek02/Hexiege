# Research: 이동 전 회전 타이밍 수정

**작성일:** 2026-03-27
**관련 작업:** `2026-03-26/15_08_network-unit-combat-sync` (보류 항목 후속)

---

## 현재 구현 상태

### 서버 (UnitView.MoveAlongPath, 첫 스텝 i==1)
1. `GameEvents.OnUnitFacingChanged` 이벤트 발행 (yAngle + rotationDuration 포함)
2. `NetworkCombatController`가 수신 → `TurnToFaceClientRpc(unitId, yAngle, rotationDuration)` 전송
3. `WaitForSeconds(_rotationDuration=0.3f)` 대기
4. 이동 재개

### 클라이언트 (TurnToFaceClientRpc 수신 시)
1. `networkUnit.ResetMovementTracking()` 호출
2. `transform.DOKill()`
3. `transform.DORotate(yAngle, rotationDuration).SetEase(Ease.OutQuad)` 시작

---

## 근본 원인

### 두 회전 시스템의 충돌

```
[매 프레임 실행 순서]
Update (DOTween 내부):  transform.rotation = 회전 보간값 설정
LateUpdate (NetworkUnit): transform.rotation = 위치 델타 기반 RotateTowards로 덮어씌움
```

**DOTween은 Update에서 실행, NetworkUnit.LateUpdate는 그 이후 실행.**
따라서 DOTween이 매 프레임 rotation을 설정해도 LateUpdate가 즉시 덮어씌운다.

- 이동 전 (delta=0): LateUpdate 임계값 미달 → DOTween이 유효하게 작동
- 이동 시작 순간 (delta > 0.0001f): LateUpdate가 RotateTowards로 매 프레임 override → DOTween 사실상 무효

### 결과
`TurnToFaceClientRpc`의 프리-회전이 이동 시작 즉시 LateUpdate에 의해 무력화됨.
서버는 `WaitForSeconds(0.3f)` 후 이동을 시작하지만, 클라이언트는 NetworkTransform 위치 변화가 도달하는 순간부터 LateUpdate 회전이 DOTween을 override하여 프리-회전 효과가 사라짐.

---

## 영향 범위

| 파일 | 변경 내용 |
|------|-----------|
| `Infrastructure/Network/NetworkUnit.cs` | `_isPreRotating` 플래그 + `SetPreRotating(bool)` + LateUpdate 조건 |
| `Infrastructure/Network/NetworkCombatController.cs` | DORotate OnComplete 콜백에서 `SetPreRotating(false)` 호출 |

싱글플레이: `NetworkContext.IsNetworkActive=false`이면 LateUpdate 전체 스킵 → 영향 없음
서버(Host): `IsServer=true`이면 LateUpdate 전체 스킵 → 영향 없음
클라이언트만 영향.
