# Research — Human CannonTower 초기 방향 설정

## 이 작업은 무엇인가?

Human 종족의 방어 타워(CannonTower)에는 대포가 달려 있습니다.
현재는 배치 시 아무 방향이나 바라보고 있어 시각적으로 어색합니다.
배치 순간 상대방 진영을 향하도록 초기 회전값을 고정합니다.
이후 실시간 회전은 없으며, 배치 시 한 번만 설정합니다.

Human 타워에만 적용되며 Spirit(RuneSpire), Transcendence(VineTower)는 해당 없습니다.

---

## 현재 코드 상태

**파일**: `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs:170`

```csharp
GameObject obj = Instantiate(prefab, viewPos, Quaternion.identity, _buildingParent);
```

현재 모든 건물이 `Quaternion.identity`(회전 없음)로 생성됩니다.

---

## 좌표계 파악

**ViewConverter.ToView()** (`Assets/_Project/Scripts/Core/ViewConverter.cs:86`):
- Blue 팀: 변환 없이 도메인 좌표 그대로 사용
- Red 팀: 맵 중심 기준으로 X, Z 반전 (`2 * mapCenter - domainPos`)
- Y축(높이)은 반전하지 않음
- **위치만 반전, 회전값은 건드리지 않음**

**팀별 화면 배치**:
- Blue 팀 시점: 자신은 화면 하단, 상대방(Red 성)은 화면 상단(Z+ 방향)
- Red 팀 시점: ViewConverter가 X,Z를 반전하므로 자신은 화면 하단, 상대방(Blue 성)은 화면 상단

**회전 영향**:
ViewConverter는 위치만 반전하고 회전은 변환하지 않습니다.
따라서 Blue 팀 타워와 Red 팀 타워는 **서로 다른 Y 회전값**이 필요합니다.
같은 회전값을 적용하면, Red 팀 시점에서 타워가 반대 방향(적이 아닌 아군 진영)을 바라보게 됩니다.

---

## 결론

| 팀 | 상대방 방향 (뷰 좌표계 기준) | 필요 Y 회전 |
|----|--------------------------|------------|
| Blue | Z+ 방향 | 프리팹 기본 방향에 따라 결정 |
| Red | Z- 방향 (ViewConverter 반전으로 Z+가 아군 방향이 됨) | Blue + 180도 |

**정확한 Y 각도는 프리팹의 대포 기본 방향을 Unity에서 확인 후 결정해야 합니다.**
대포가 어떤 축을 기본으로 바라보는지(Z+인지 Z-인지 X+인지)에 따라 각도가 달라집니다.

---

## 영향 범위

- 수정 파일: `BuildingFactory.cs` 1개
- 수정 위치: `CreateBuildingObject()` 메서드 내 Instantiate 호출부
- Human + AutoTower 조합일 때만 분기 처리
- 업그레이드(`UpgradeBuildingObject`)는 기존 GO의 rotation을 그대로 유지하므로 별도 처리 불필요
