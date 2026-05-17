# Plan — 건물 업그레이드 시스템 및 단계별 생산건물 에셋 적용

## 이 작업에서 무엇을 하는가

현재 모든 생산건물이 "Barracks" 하나로 처리되는 구조를 바꿔,
각 건물(TrainingCamp, Gunsmith, FireSpire 등)을 고유하게 식별하고
단계(1→2→3)를 올릴 수 있도록 시스템을 확장한다.

결과적으로:
- 게임 내에서 3D 건물 모델이 건물 종류별로 올바르게 표시된다
- 건물 배치 패널에서는 각 생산 라인의 1단계 건물만 표시된다
- 유닛 생산 패널에서 업그레이드 버튼을 눌러 건물 단계를 올릴 수 있다
- 건물 단계에 따라 생산 가능한 유닛이 달라진다

---

## ✅ 확정된 기획 사항

**[A] 단계별 유닛 생산 — 누적 방식**
업그레이드할수록 생산 가능 유닛이 추가된다.
예시:
- TrainingCamp(1단계) → LittleKnight
- WarAcademy(2단계) → LittleKnight + SpearMan
- HumanBarracks(3단계) → LittleKnight + SpearMan + BattleAxe

**[B] 업그레이드 방식**
- 골드 소모: 있음 (금액은 Inspector ScriptableObject에서 설정)
- 완료 방식: 즉시 완료 (건설 시간 없음)
- 건물 교체: 기존 건물 파괴 후 새 프리팹 생성. 단, **빈 타일이 보이지 않도록** 새 프리팹을 먼저 배치한 뒤 기존 건물을 제거하는 순서로 처리

---

## GameSystemRules.md 근거

현재 `GameSystemRules.md`에 건물 업그레이드 시스템 규칙이 없다.
이 작업은 새 규칙을 정의하는 작업이므로, 구현 완료 후 GameSystemRules.md에 추가가 필요하다.

기존 규칙 중 영향받는 항목:
- **생산 패널 UI** 섹션 규칙 1-28: 업그레이드 버튼 추가로 팝업 인터랙션 범위가 확장됨
- **건물 배치 패널 UI** 섹션 규칙 1-4: 표시할 건물 목록 기준 변경 (1단계만 표시)

---

## 구현 방법 — 단계별

### Phase 1: 기반 구조 변경 (Domain + Infrastructure)

#### 1-A. `BuildingType.cs` — 열거형 확장

`Barracks`를 모든 구체적인 건물 이름으로 대체한다.
열거형 정수값 순서를 바꾸면 기존 저장 데이터가 깨지지만, 개발 단계이므로 허용.

```csharp
// 기존 Barracks 제거, 아래 값들로 대체
// Human 생산건물
TrainingCamp,    // 근거리A 1단계
WarAcademy,      // 근거리A 2단계
HumanBarracks,   // 근거리A 3단계 (기존 Barracks 이름 충돌 방지)
Gunsmith,        // 총기류 1단계
Armory,          // 총기류 2단계
WeaponForge,     // 총기류 3단계
Garage,          // 탈것류 1단계
VehicleBay,      // 탈것류 2단계
// Spirit 생산건물
FireSpire,       // 불 1단계
BlazeConduit,    // 불 2단계
InfernoCore,     // 불 3단계
AquaSpring,      // 물 1단계
TidalNexus,      // 물 2단계
OceanicHeart,    // 물 3단계
StoneMound,      // 땅 1단계
TerraForge,      // 땅 2단계
GaeaSanctum,     // 땅 3단계
// Transcendence 생산건물
PrimalAltar,     // 동물A 1단계
PrimalDen,       // 동물A 2단계
PrimalSanctuary, // 동물A 3단계 (제작예정)
FeralAltar,      // 동물B 1단계
FeralDen,        // 동물B 2단계
FeralSanctuary,  // 동물B 3단계
SporePatch,      // 식물 1단계
FloralNursery,   // 식물 2단계
```

**하위 호환 헬퍼 추가**: `BuildingType`이 생산건물인지 판별하는 정적 헬퍼 클래스를 별도 파일로 작성한다.

```csharp
// BuildingTypeHelper.cs (새 파일)
public static class BuildingTypeHelper
{
    // 생산건물 여부 판별 (기존 Barracks 역할)
    public static bool IsProductionBuilding(BuildingType type) { ... }
    // 업그레이드 가능 여부
    public static bool CanUpgrade(BuildingType type) { ... }
    // 다음 단계 반환 (3단계면 null)
    public static BuildingType? GetNextStage(BuildingType type) { ... }
    // 단계 번호 반환 (1/2/3)
    public static int GetStage(BuildingType type) { ... }
}
```

영향: `BuildingPlacementUseCase.cs` 내 `Barracks` 카테고리 체크 → `IsProductionBuilding()` 호출로 대체.

---

#### 1-B. `BuildingData.cs` — Stage 프로퍼티 추가

별도 필드를 저장하지 않고 `BuildingType`에서 파생한다.

```csharp
// BuildingTypeHelper.GetStage(Type)를 래핑하는 편의 프로퍼티
public int Stage => BuildingTypeHelper.GetStage(Type);
```

---

#### 1-C. `BuildingStats.cs` + `BuildingStatsConfig.cs` — 새 타입 스탯 대응

`BuildingStats` 폴백 딕셔너리에 새 BuildingType들 추가.
`BuildingStatsConfig`는 기존 `BuildingTypeEntry` 구조를 그대로 유지하면서
Inspector에서 새 BuildingType 항목들을 추가 등록하면 된다.

---

### Phase 2: 프리팹 등록 (BuildingFactory)

#### 2-A. `BuildingFactory.cs` — 프리팹 리스트 재구성

현재 종족별 1개의 `List<BuildingPrefabEntry>` → 동일 구조를 유지하되
Inspector에서 각 건물 타입별로 항목을 추가하는 방식으로 확장한다.

```
_humanPrefabs:
  (TrainingCamp, blue프리팹, red프리팹)
  (WarAcademy, blue프리팹, red프리팹)
  (HumanBarracks, blue프리팹, red프리팹)
  (Gunsmith, blue프리팹, red프리팹)
  ...
```

코드 변경 없이 Inspector 항목 추가만으로 대응 가능하다.

---

### Phase 3: 건물 배치 패널 변경 (BuildingPlacementUI)

#### 3-A. `BuildingPlacementUI.cs` — 1단계 건물만 표시

현재 Inspector 리스트에서 각 종족별 건물 목록을 교체한다.
현재: [Barracks 아이콘, MiningPost 아이콘]
변경: [TrainingCamp 아이콘, Gunsmith 아이콘, Garage 아이콘, MiningPost 아이콘]
(각 라인의 1단계만 포함)

코드 변경 사항:
- `BuildingPortraitEntry.type` 필드가 이제 구체적인 BuildingType 값을 사용
- 배치 로직은 그대로 작동 (`PlaceAndClose(entry.type)`)
- `CanPlaceBuildingType()` 메서드: 생산건물 판별을 `IsProductionBuilding()` 헬퍼로 대체

Inspector 재설정 필요:
- 6개 리스트(Blue/Red × 3종족) 내용을 새 1단계 건물 목록으로 교체
- → Editor 1회성 스크립트로 처리할지, 수동 Inspector 작업으로 할지 확인 필요

---

### Phase 4: 유닛 생산 패널 변경 (ProductionPanelUI)

#### 4-A. 건물 타입별 유닛 목록 — 전체 표시 + 단계별 잠금

**표시 방식**: 라인의 전체 유닛을 모두 표시한다. 현재 건물 단계에서 아직 해금되지 않은 유닛은 잠금 상태로 표시하고, 탭 시 토스트 메시지를 보여준다.

예시 (Human 근거리 라인, WarAcademy = 2단계):
- LittleKnight → 정상 생산 가능 (1단계 해금)
- SpearMan → 정상 생산 가능 (2단계 해금)
- BattleAxe → **잠금** 표시, 탭 시 "업그레이드가 필요합니다" 토스트 (3단계 해금)

새 Inspector 구조:
```csharp
// UnitPortraitEntry에 해금 단계 필드 추가
[System.Serializable]
public struct UnitPortraitEntry
{
    public UnitType type;
    public Sprite portrait;
    public int requiredStage; // 이 유닛을 생산하려면 필요한 건물 단계 (1/2/3)
}

// 건물 타입별 유닛 전체 목록
[System.Serializable]
public struct BuildingUnitMapping
{
    public BuildingType buildingType;
    public List<UnitPortraitEntry> blueUnits; // 해금 단계 포함한 전체 목록
    public List<UnitPortraitEntry> redUnits;
}

[SerializeField] private List<BuildingUnitMapping> _buildingUnitMappings;
```

`Show(BuildingData barracks)` 호출 시:
- `barracks.Type`으로 매핑 조회 → 해당 라인의 전체 유닛 목록 바인딩
- `barracks.Stage`와 각 유닛의 `requiredStage` 비교 → 잠금 여부 결정
- 잠금 유닛 버튼: 어둡게 처리 (alpha 또는 색상으로 구분)

탭 처리 변경:
- 잠금 유닛 탭 시 → `ToastUI.Show(ToastKey.UpgradeRequired)` 출력, 생산 등록 안 함
- 잠금 해제 유닛 탭 시 → 기존 생산 로직 그대로

새 토스트 키 추가 (`ToastKey.UpgradeRequired`): "건물 업그레이드가 필요합니다"

기존 6개 종족 고정 리스트는 **비활성화(주석 처리)** 후 테스트 통과 시 삭제.

#### 4-B. 업그레이드 버튼 추가

Inspector에 업그레이드 버튼 추가:
```csharp
[SerializeField] private Button _upgradeButton;
[SerializeField] private TextMeshProUGUI _upgradeCostText;
```

`Show()` 시 로직:
- `BuildingTypeHelper.CanUpgrade(barracks.Type)` → true면 버튼 활성화, 업그레이드 비용 텍스트 표시
- 3단계 건물(업그레이드 불가)이면 버튼 숨김
- 버튼 클릭 시 골드 검증 → 부족하면 토스트 → 충분하면 `RequestUpgrade(barracks)` 호출

업그레이드 실행 흐름:
1. 클라이언트: 사전 골드 검증 → `NetworkBuildingController.RequestUpgradeServerRpc(buildingId)` 전송
2. 서버: 소유권·골드 재검증 → `BuildingPlacementUseCase.UpgradeBuilding(buildingId)` 실행
3. 서버: 성공 시 골드 차감 → `UpgradeBuildingClientRpc(buildingId, newTypeInt)` 전파
4. 모든 클라이언트: `BuildingFactory`가 **새 프리팹 먼저 생성 → 기존 GO 제거** 순서로 처리 (빈 타일 방지)

---

### Phase 5: 업그레이드 도메인 로직 + 네트워크 (BuildingPlacementUseCase + NetworkBuildingController)

#### 5-A. `BuildingPlacementUseCase.cs` — UpgradeBuilding 추가

```csharp
// 건물 업그레이드: 기존 BuildingData 제거 → 다음 단계 BuildingData 생성
// 타일의 IsWalkable 상태는 변경 없음 (건물이 계속 존재)
// 반환: 생성된 새 BuildingData (실패 시 null)
public BuildingData UpgradeBuilding(int buildingId, RaceId race)
```

내부 처리 순서:
1. `_buildings[buildingId]` 조회
2. `BuildingTypeHelper.GetNextStage(type)` → 다음 타입 확인
3. 기존 BuildingData를 `_buildings`에서 제거
4. 새 타입으로 BuildingData 생성 → `_buildings`에 등록
5. `GameEvents.OnBuildingUpgraded` 이벤트 발행 (buildingId, oldType, newBuilding)
6. 타일 IsWalkable은 false 유지, 소유권은 변경 없음

새 이벤트 추가 (`GameEvents.cs`):
```csharp
public static Subject<BuildingUpgradedEvent> OnBuildingUpgraded = new Subject<BuildingUpgradedEvent>();
// BuildingUpgradedEvent: int OldBuildingId, BuildingData NewBuilding
```

#### 5-B. `BuildingFactory.cs` — OnBuildingUpgraded 구독

```
OnBuildingUpgraded 수신 시:
1. GetPrefab(race, newBuilding.Type, newBuilding.Team)으로 새 프리팹 조회
2. Instantiate(새 프리팹, viewPos, ...) → 새 GO 생성
3. Destroy(_buildingObjects[oldBuildingId]) → 기존 GO 제거
4. _buildingObjects[newBuilding.Id] = 새 GO 등록
```
새 프리팹을 먼저 생성 후 기존 GO를 제거하므로 타일이 빈 것처럼 보이지 않는다.

#### 5-C. `NetworkBuildingController.cs` — 업그레이드 ServerRpc/ClientRpc 추가

```csharp
[ServerRpc(RequireOwnership = false)]
public void RequestUpgradeServerRpc(int buildingId, ServerRpcParams rpcParams = default)
// 검증: 소유권 + 골드 → UpgradeBuilding() → 성공 시 UpgradeBuildingClientRpc 전파

[ClientRpc]
private void UpgradeBuildingClientRpc(int oldBuildingId, int newBuildingId, int newTypeInt, int teamIndex, int q, int r)
// 클라이언트: BuildingPlacementUseCase.UpgradeBuildingWithId() → OnBuildingUpgraded 발행
```

---

## 파일별 변경 내용 요약

| 파일 | 변경 유형 | 내용 |
|------|-----------|------|
| `BuildingType.cs` | 수정 | `Barracks` 제거 → 구체적 타입 26개+ 추가 |
| `BuildingTypeHelper.cs` | **신규** | 카테고리 판별, Stage 반환, 업그레이드 경로, 비용 헬퍼 |
| `BuildingData.cs` | 수정 | `Stage` 파생 프로퍼티 추가 |
| `BuildingStats.cs` | 수정 | 폴백 딕셔너리에 신규 타입 추가, `GetUpgradeCost(BuildingType)` 추가 |
| `BuildingStatsConfig.cs` | 수정 | `BuildingTypeEntry`에 `UpgradeCost` 필드 추가, 신규 타입 항목 추가 |
| `BuildingPlacementUseCase.cs` | 수정 | `UpgradeBuilding()` 추가, Barracks → IsProductionBuilding() |
| `GameEvents.cs` | 수정 | `OnBuildingUpgraded` 이벤트 추가 |
| `BuildingFactory.cs` | 수정 + Inspector | `OnBuildingUpgraded` 구독 추가, 프리팹 리스트 재설정 |
| `BuildingPlacementUI.cs` | 수정 + Inspector | Barracks→IsProductionBuilding 교체, 1단계 리스트 재구성 |
| `ProductionPanelUI.cs` | 수정 + Inspector | 건물별 유닛 매핑 구조로 교체, requiredStage 잠금 처리, 업그레이드 버튼 추가 |
| `ToastKey.cs` (또는 해당 열거형 파일) | 수정 | `UpgradeRequired` 토스트 키 추가 |
| `NetworkBuildingController.cs` | 수정 | RequestUpgradeServerRpc / UpgradeBuildingClientRpc 추가 |
| `GameSystemRules.md` | 추가 | 업그레이드 시스템 규칙 작성 |

---

## 작업 순서

```
[1] BuildingType.cs 확장 + BuildingTypeHelper.cs 작성
      ↓ (컴파일 오류 일괄 수정)
[2] BuildingData.cs Stage 프로퍼티 추가
      ↓
[3] BuildingStats.cs 폴백 업데이트
      ↓
[4] BuildingPlacementUseCase.cs Barracks → IsProductionBuilding 교체
      ↓
[5] BuildingFactory.cs Inspector 재설정 (프리팹 연결)
      ↓
[6] BuildingPlacementUI.cs 코드 수정 + Inspector 재설정 (1단계만 표시)
      ↓
[7] ProductionPanelUI.cs 유닛 매핑 구조 변경 + 업그레이드 버튼 (미확정 항목 확정 후)
      ↓
[8] NetworkBuildingController.cs 업그레이드 ServerRpc 추가
```

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| `BuildingType` 열거형 정수값 변경 | 기존 씬/에셋의 직렬화 데이터 무효화 가능 | 개발 단계이므로 허용. Inspector 재설정으로 복구 |
| `ProductionPanelUI` 기존 6개 리스트 → 새 구조 전환 | 기존 Inspector 연결 전부 재설정 필요 | 기존 리스트 주석 처리 후 단계적 전환 |
| 미확정 항목 [A][B] | 유닛 매핑과 업그레이드 로직 미완성 시 ProductionPanelUI 구현 불완전 | Phase 4/5는 [A][B] 확정 후 진행 |

---

## ✅ 추가 확정 사항

**업그레이드 비용 관리 방식: 건물별 ScriptableObject 설정**
각 건물 타입마다 개별 업그레이드 비용을 Inspector에서 직접 설정한다.
`BuildingStatsConfig`의 `BuildingTypeEntry` 구조체에 `UpgradeCost` 필드를 추가하여 처리한다.
(예: TrainingCamp 업그레이드 비용 = 80골드, Gunsmith 업그레이드 비용 = 120골드 등 각각 다르게 설정 가능)

`BuildingStats`에 `GetUpgradeCost(BuildingType)` 조회 메서드 추가.
업그레이드 비용은 종족과 무관하게 BuildingType 하나당 단일 값으로 관리한다.

**각 건물별 생산 유닛 목록 전체 확정**
Inspector에서 직접 설정하는 방식이므로 코드 구현 후 Inspector 작업 시 결정해도 된다.
단, ProductionPanelUI 구현 시 매핑 구조 테스트용으로 Human 근거리 라인의 예시 매핑이 필요하다.
