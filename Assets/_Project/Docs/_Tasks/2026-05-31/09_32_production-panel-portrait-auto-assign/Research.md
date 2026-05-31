# Research — 유닛 생산 패널 초상화 자동 할당 에디터 스크립트

## 무엇을 왜 하는가

유닛 생산 패널(ProductionPanelUI)에서 유닛 버튼에 초상화 이미지가 표시되지 않는 문제가 발생했다.
씬 뷰에서는 Image 컴포넌트에 직접 할당한 샘플 이미지가 보이지만, 플레이 모드에서는 코드가
별도의 데이터 목록(`_buildingUnitMappings`)에서 초상화를 읽어 덮어씌우는 구조이기 때문에,
그 목록에 초상화 Sprite가 할당되어 있지 않으면 이미지가 사라진다.

이 에디터 스크립트는 `Assets/_Project/Sprites/Units` 폴더에 있는 스프라이트 파일을
자동으로 읽어 `_buildingUnitMappings`의 각 초상화 슬롯에 할당하는 1회성 도구다.

---

## 문제의 원인

### 데이터 소스 불일치
- **씬뷰 미리보기**: `_unitButtonPortraits[i]` (Image 컴포넌트)의 Source Image 슬롯에 직접 할당
- **런타임 소스**: `_buildingUnitMappings` → `UnitPortraitEntry.portrait` Sprite 필드
- `UpdateButtonPortraits()` ([ProductionPanelUI.cs:784]) 가 건물 패널 열릴 때 실행되어 `_unitButtonPortraits[i].sprite = list[i].portrait` 로 덮어씀
- `list[i].portrait`가 null → 이미지가 사라짐

---

## 관련 파일 및 구조

### 핵심 컴포넌트
- `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`
  - `[SerializeField] private List<BuildingUnitMapping> _buildingUnitMappings` (Line 159)
  - `BuildingUnitMapping { BuildingType, List<UnitPortraitEntry> blueUnits, List<UnitPortraitEntry> redUnits }`
  - `UnitPortraitEntry { UnitType type, Sprite portrait, int requiredStage }`
- `Assets/_Project/Scenes/Game.unity` — ProductionPanelUI 컴포넌트가 씬에 직접 배치됨 (프리팹 없음)

### 스프라이트 폴더 구조
```
Assets/_Project/Sprites/Units/
├── Human/
│   ├── Assault/assault_portrait_blue.png, assault_portrait_red.png
│   ├── BattleAxe/battleaxe_portrait_blue.png, battleaxe_portrait_red.png
│   ├── CannonCart/cannoncart_portrait_blue.png, cannoncart_portrait_red.png
│   ├── LittleKnight/littleknight_portrait_blue.png, littleknight_portrait_red.png
│   ├── Pistoleer/pistoleer_portrait_blue.png, pistoleer_portrait_red.png
│   ├── Sniper/sniper_portrait_blue.png, sniper_portrait_red.png
│   ├── SpearMan/spearman_portrait_blue.png, spearman_portrait_red.png
│   └── Tank/tank_portrait_blue.png, tank_portrait_red.png
├── Spirit/
│   ├── BoulderSpirit/, DustSpirit/, EmberSpirit/, FlameSpirit/
│   ├── InfernoSpirit/, QuakeSpirit/, StreamSpirit/, TideSpirit/, TorrentSpirit/
├── Transcendence/
│   ├── BearGuard/, BloomFairy/, EagleArcher/, FoxMagician/
│   ├── LionKnight/, MushroomBomber/, RabbitTrickster/, RhinoBreaker/
```

### 파일명 변환 규칙 (검증 완료)
`UnitType.ToString().ToLower()` → 파일명 prefix 와 완벽 일치

| UnitType | 파일명 |
|---|---|
| Pistoleer | pistoleer_portrait_blue.png |
| LittleKnight | littleknight_portrait_blue.png |
| FlameSpirit | flamespirit_portrait_blue.png |
| BearGuard | bearguard_portrait_blue.png |

패턴: `{UnitType.ToString().ToLower()}_portrait_{blue|red}.png`

---

## 구현 접근법

### 에디터 전용 스크립트 (Editor 폴더)
- `#if UNITY_EDITOR` 또는 `Assets/Editor/` 에 위치시켜 빌드에 포함되지 않도록 함
- `AssetDatabase.FindAssets("t:Sprite", searchInFolders)` 로 스프라이트 검색
- `[MenuItem("Hexiege/Assign Unit Portraits")]` 메뉴 항목으로 1회 실행
- 실행 후 `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets` 호출

### 처리 흐름
1. 씬에서 `ProductionPanelUI` 컴포넌트를 `FindObjectOfType<ProductionPanelUI>()`로 조회
2. `_buildingUnitMappings` 리스트 reflection 또는 SerializedObject로 접근
3. blueUnits / redUnits 각 `UnitPortraitEntry`를 순회
4. `UnitType.ToString().ToLower()` → 파일명 패턴 생성
5. `AssetDatabase.FindAssets` 로 해당 스프라이트 GUID 검색
6. `AssetDatabase.GUIDToAssetPath` + `AssetDatabase.LoadAssetAtPath<Sprite>` 로 로드
7. `portrait` 슬롯에 할당 (null인 경우만, 기존 할당은 덮어쓰지 않음)
8. `EditorUtility.SetDirty` 후 씬 저장 요청

---

## 주의사항

- 씬 저장은 스크립트가 직접 수행하지 않고 "씬을 저장해주세요" 안내 출력
- 기존에 이미 할당된 portrait는 건드리지 않음 (`entry.portrait == null` 조건 체크)
- `_buildingUnitMappings`가 비어있으면 콘솔에 경고 출력
