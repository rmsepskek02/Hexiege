# Research: 전투 범위 판정 월드 좌표 복구

**날짜:** 2026-03-07
**작업자:** game-programmer

---

## 현재 상태

`UnitCombatUseCase.FindFirstEnemyTarget()`이 `HexCoord.Distance`(도메인 좌표)로 범위 판정.

```csharp
// UnitCombatUseCase.cs L97
int distance = HexCoord.Distance(attacker.Position, unit.Position);
if (distance <= attacker.AttackRange && distance < minDistance)
```

## 문제

`attacker.Position`은 Lerp 완료 후 `ProcessStep`에서 갱신됨.
→ Lerp 중에는 이전 타일 좌표 유지 (시각 위치와 불일치)
→ 시각적으로 사거리 내에 적이 있어도 도메인 거리가 멀면 공격 안 함 (최대 MoveSeconds=0.8초 딜레이)

## 원래 구현 (2026-03-02, git restore로 소실)

- `IEntityPositionProvider` 인터페이스 (Application/Interfaces/)
- `UnitWorldPositionProvider` 구현체 (Infrastructure/)
- `UnitCombatUseCase`에 주입하여 `Vector3.Distance` 기반 범위 판정

## 영향 범위

- `UnitCombatUseCase.cs` — `FindFirstEnemyTarget`, `HasEnemyInRange`
- `GameBootstrapper.cs` — 의존성 주입
- 신규: `IEntityPositionProvider.cs`, `UnitWorldPositionProvider.cs`

## 아키텍처 제약

- Application → Infrastructure 직접 참조 금지
- `IEntityPositionProvider`는 Application 레이어에 인터페이스 정의
- `UnitWorldPositionProvider`는 Infrastructure 레이어에 구현
- `UnitFactory.GetUnitObject(id)`, `BuildingFactory.GetBuildingObject(id)` 이미 구현됨 — 재사용 가능
