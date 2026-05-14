# Research: 공격 방향 실제 Transform 기반으로 복구 (2026-03-07)

## 배경
- 2026-03-03 git restore로 attack direction 작업이 삭제됨 (복구 불가)
- 삭제된 작업: 2D 레거시 제거 후 실제 타겟 transform.position 기반 공격 방향 계산
- 현재 복구 중: HexCoord → HexToWorld 변환 방식으로 잘못 복구되어 있음

## 현재 잘못된 구현

### UnitView.CalculateAttackAngle(HexCoord targetDomainPos)
```csharp
Vector3 targetView = ViewConverter.ToView(HexMetrics.HexToWorld(targetDomainPos));
Vector3 dir = targetView - transform.position;
```
- 타겟의 실제 transform이 아닌 HexCoord → 타일 중심점 계산
- Lerp 이동 중인 타겟의 실제 위치와 다를 수 있음

### TriggerAttackAnimation(HexCoord targetPosition)
- HexCoord 파라미터 사용

### NetworkCombatController.TriggerAttackAnimationClientRpc(unitId, targetQ, targetR)
- HexCoord (Q, R) int 파라미터로 전송

## 올바른 구현 방향

### 왜 실제 transform이 더 정확한가
- 타겟이 Lerp 이동 중일 때 HexCoord 기반은 타일 중심(도착지/출발지 중 하나)만 가리킴
- 실제 transform은 Lerp 진행 중의 정확한 현재 위치를 가리킴
- 시각적으로 타겟을 정확히 바라보는 효과

### 왜 방법 B (targetId → UnitFactory 조회)가 최선인가
- 서버가 RPC 전송 시점의 world position을 보내면 (방법 A),
  클라이언트 RPC 수신 시점엔 이미 타겟 위치가 달라져 있을 수 있음
- 방법 B: 클라이언트가 RPC 수신 시 직접 targetId로 GetUnitObject() 조회
  → 항상 현재 실제 위치 사용, RPC는 int 하나로 단순

## 파일 현황 파악

### UnitView.cs
- `SetDependencies(config, movementUseCase, combatUseCase)` — UnitFactory 없음
- `TriggerAttackAnimation(HexCoord)` — HexCoord 기반
- `CalculateAttackAngle(HexCoord)` — HexToWorld 변환

### NetworkCombatController.cs
- `TriggerAttackAnimationClientRpc(int unitId, int targetQ, int targetR)`
- TickCombat에서 `combat.TryAttack(unit)` → `HexCoord? targetPos`

### UnitCombatUseCase.cs
- `TryAttack()` → `HexCoord?` 반환 (target.Position)
- target의 Id 정보는 반환하지 않음

### UnitFactory.cs
- `GetUnitObject(int unitId)` — unitId로 GameObject 반환 (이미 존재)
