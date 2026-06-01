# Plan — Human CannonTower 초기 방향 설정

## 이 계획은 무엇인가?

Human CannonTower를 배치할 때 대포가 상대방 진영을 향해 고정되도록 초기 회전값을 설정합니다.
`BuildingFactory.cs` 한 곳만 수정하며, 실시간 회전 로직은 추가하지 않습니다.

---

## GameSystemRules 규칙 근거

방어 타워 초기 방향에 대한 규칙이 `GameSystemRules_Buildings.md`에 아직 없습니다.
이 구현은 시각적 완성도를 위한 것이며 게임 동작(데미지/쿨다운)에는 영향 없습니다.
구현 완료 후 `GameSystemRules_Buildings.md`의 방어 타워 시스템 섹션에 규칙을 추가합니다.

---

## 구현 내용

### 수정 파일: `BuildingFactory.cs`

**수정 위치**: `CreateBuildingObject()` 메서드

**현재 코드**:
```csharp
GameObject obj = Instantiate(prefab, viewPos, Quaternion.identity, _buildingParent);
```

**수정 방향**:
```csharp
Quaternion rotation = GetInitialRotation(race, data.Type, data.Team);
GameObject obj = Instantiate(prefab, viewPos, rotation, _buildingParent);
```

**추가할 메서드 `GetInitialRotation()`**:
- Human + AutoTower 조합일 때만 팀에 따라 Y 회전값 반환
- Blue 팀: Inspector에서 설정한 각도
- Red 팀: Blue 각도 + 180도
- 그 외 모든 경우: `Quaternion.identity`

**Inspector 필드 추가**:
```csharp
[Header("Human CannonTower 초기 방향")]
[Tooltip("Blue팀 CannonTower가 배치될 때 상대방 진영을 향하는 Y축 회전 각도(도). Red팀은 자동으로 +180도 적용.")]
[SerializeField] private float _cannonTowerFacingAngle = 0f;
```

Y 각도를 Inspector로 노출하는 이유: 프리팹의 대포 기본 방향에 따라 달라지므로 하드코딩하지 않고 Inspector에서 조정 가능하게 합니다.

---

## 수정 파일 목록

| 파일 | 변경 종류 | 내용 |
|------|----------|------|
| `Infrastructure/Factories/BuildingFactory.cs` | 수정 | `_cannonTowerFacingAngle` 필드 추가, `GetInitialRotation()` 메서드 추가, `CreateBuildingObject()`에서 호출 |

---

## Inspector 작업

구현 후 Unity에서 `BuildingFactory` Inspector에서 `_cannonTowerFacingAngle` 값을 직접 조정해 대포가 적 진영을 바라보도록 맞춥니다.
(프리팹 대포 기본 방향 확인 후 값 결정)

---

## 완료 조건

1. Blue 팀 CannonTower 배치 시 대포가 상대방(Red 성) 방향을 바라봄
2. Red 팀 CannonTower 배치 시 대포가 상대방(Blue 성) 방향을 바라봄
3. Spirit/Transcendence 타워 회전값 변동 없음
4. 생산 건물 등 다른 Human 건물 회전값 변동 없음
