# Plan: 전투 범위 판정 월드 좌표 복구

**날짜:** 2026-03-07

---

## 구현 방법

### 1. IEntityPositionProvider.cs (신규)
경로: `Assets/_Project/Scripts/Application/Interfaces/IEntityPositionProvider.cs`

```csharp
namespace Hexiege.Application
{
    public interface IEntityPositionProvider
    {
        UnityEngine.Vector3 GetUnitWorldPosition(int unitId);
        UnityEngine.Vector3 GetBuildingWorldPosition(int buildingId);
    }
}
```

### 2. UnitWorldPositionProvider.cs (신규)
경로: `Assets/_Project/Scripts/Infrastructure/UnitWorldPositionProvider.cs`

- `UnitFactory`, `BuildingFactory` 주입받아 `GetUnitObject(id).transform.position` 반환
- GameObject가 null(파괴됨)이면 `Vector3.zero` 반환

### 3. UnitCombatUseCase.cs (수정)
- 생성자에 `IEntityPositionProvider _positionProvider` 추가
- `FindFirstEnemyTarget`: HexCoord.Distance → Vector3.Distance
- 범위 임계값: `attacker.AttackRange * HexMetrics.AdjacencyDistance + 0.1f` (epsilon)
  - `HexMetrics.AdjacencyDistance` = 0.866f (FlatTop/PointyTop 공통)
- `HasEnemyInRange`는 `FindFirstEnemyTarget` 재사용하므로 자동 적용

### 4. GameBootstrapper.cs (수정)
- `UnitWorldPositionProvider` 생성
- `UnitCombatUseCase` 생성자에 전달

## HexMetrics.AdjacencyDistance

현재 HexMetrics에 없으면 추가: `public const float AdjacencyDistance = 0.866f;`

## 위험 요소

- `UnitWorldPositionProvider`가 Factory를 참조 — 팩토리가 초기화되기 전에 접근하면 null
  → GameBootstrapper에서 Factory 생성 후 Provider 생성하면 문제없음
- GameObject 파괴 후 position 조회 → null 체크 필수
