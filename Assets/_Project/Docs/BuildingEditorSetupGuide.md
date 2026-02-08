# MVP: 건물 배치 시스템 - 에디터 작업 가이드

**버전:** 0.1.0
**최종 수정일:** 2026-02-08
**작성자:** HANYONGHEE

---

## 개요

건물 배치 시스템 코드 구현 완료 후, Unity 에디터에서 수행해야 할 작업을 정리한 문서.
총 5단계로 구성. 순서대로 진행.

**사전 조건:**
- 프로토타입 Phase 10 에디터 작업 완료 (`Phase10_EditorSetupGuide.md` 참고)
- 건물 배치 관련 스크립트 7개 작성 완료 (신규 7 + 수정 4)

**관련 스크립트:**

| 파일 | 레이어 | 구분 |
|------|--------|------|
| `Domain/Building/BuildingType.cs` | Domain | 신규 |
| `Domain/Building/BuildingData.cs` | Domain | 신규 |
| `Application/UseCases/BuildingPlacementUseCase.cs` | Application | 신규 |
| `Infrastructure/Factories/BuildingFactory.cs` | Infrastructure | 신규 |
| `Presentation/Building/BuildingView.cs` | Presentation | 신규 |
| `Presentation/UI/BuildingPlacementUI.cs` | Presentation | 신규 |
| `Application/Events/GameEvents.cs` | Application | 수정 |
| `Infrastructure/Config/GameConfig.cs` | Infrastructure | 수정 |
| `Presentation/Input/InputHandler.cs` | Presentation | 수정 |
| `Bootstrap/GameBootstrapper.cs` | Bootstrap | 수정 |

---

## Step 1: 건물 스프라이트 Import 설정

### 1-1. 대상 파일

```
Assets/_Project/Sprites/Buildings/
├── bld_castle.png        ← 본기지
├── bld_barracks.png      ← 배럭
└── bld_mining_post.png   ← 채굴소
```

### 1-2. Import 설정

Project 창에서 3개 파일을 동시 선택 (Ctrl+클릭) → Inspector에서 설정:

| 항목 | 값 | 설명 |
|------|------|------|
| Texture Type | Sprite (2D and UI) | 2D 게임 스프라이트 |
| Sprite Mode | Single | 단일 스프라이트 |
| Pixels Per Unit | **1024** | 타일/유닛과 동일 PPU |
| Filter Mode | Bilinear | 카툰 스타일에 적합 |
| Compression | None | 프로토타입에서는 무압축 |

설정 후 **Apply** 클릭.

> **PPU가 중요한 이유:** PPU가 다르면 건물 크기가 타일 대비 과도하게 크거나 작음.
> 기존 타일과 유닛 PPU(1024)와 동일하게 맞추고, 크기 조정은 프리팹의 Transform Scale로 처리.

---

## Step 2: 건물 프리팹 생성 (3개)

기존 `Unit_Pistoleer` 프리팹과 동일한 방식으로 생성.

### 2-1. Building_Castle 프리팹

1. **Hierarchy**에서 우클릭 → **Create Empty** → 이름: `Building_Castle`
2. **Add Component** → **Sprite Renderer**

   | 항목 | 값 |
   |------|------|
   | Sprite | `bld_castle` (Project 창에서 드래그) |
   | Order in Layer | **50** |
   | Color | White (기본값) |
   | Sorting Layer | Default |

3. **Add Component** → 스크립트 검색 → **Building View**
4. Hierarchy에서 오브젝트를 Project 창의 `Assets/_Project/Prefabs/` 폴더로 **드래그**
   - 팝업에서 **"Original Prefab"** 선택
5. Hierarchy에서 `Building_Castle` 인스턴스 **삭제** (Delete)

### 2-2. Building_Barracks 프리팹

위와 동일한 절차. 차이점만 정리:

| 항목 | 값 |
|------|------|
| GameObject 이름 | `Building_Barracks` |
| SpriteRenderer → Sprite | `bld_barracks` |
| SpriteRenderer → Order in Layer | **50** |
| 스크립트 | Building View |

Prefabs 폴더에 저장 후 Hierarchy 인스턴스 삭제.

### 2-3. Building_MiningPost 프리팹

| 항목 | 값 |
|------|------|
| GameObject 이름 | `Building_MiningPost` |
| SpriteRenderer → Sprite | `bld_mining_post` |
| SpriteRenderer → Order in Layer | **50** |
| 스크립트 | Building View |

Prefabs 폴더에 저장 후 Hierarchy 인스턴스 삭제.

### Sorting Order 체계

```
타일:    0 ~ 30   (행 기반 동적 할당)
건물:    50       (타일 위, 유닛 아래)
유닛:    100      (최상위)
```

> **건물 크기가 맞지 않을 때:** 프리팹을 더블클릭하여 Prefab Edit Mode 진입 →
> Transform의 **Scale X, Y**를 조정 (0.5~0.8 권장, 스프라이트 해상도에 따라 다름).
> 3개 프리팹 모두 비슷한 스케일로 통일.

### 결과 확인

Project 창 `Assets/_Project/Prefabs/` 폴더에 프리팹 6개:

```
Prefabs/
├── HexTile_PointyTop.prefab     (기존)
├── HexTile_FlatTop.prefab       (기존)
├── Unit_Pistoleer.prefab        (기존)
├── Building_Castle.prefab       (신규)
├── Building_Barracks.prefab     (신규)
└── Building_MiningPost.prefab   (신규)
```

---

## Step 3: 씬 오브젝트 추가

### 현재 Hierarchy (프로토타입 완료 상태)

```
SampleScene
├── Main Camera
├── [Managers]
│   ├── GameBootstrapper
│   └── UnitFactory
├── [World]
│   ├── HexGrid
│   └── Units
├── [Input]
│   └── InputHandler
└── [Debug]
    └── DebugUI
```

### 3-1. Buildings 빈 오브젝트 추가

1. **[World]** 선택 → 우클릭 → **Create Empty**
2. 이름: **Buildings**
3. Transform 확인: Position **(0, 0, 0)**

### 3-2. BuildingFactory 오브젝트 추가

1. **[Managers]** 선택 → 우클릭 → **Create Empty**
2. 이름: **BuildingFactory**
3. **Add Component** → 스크립트 검색 → **Building Factory**

### 3-3. [UI] Canvas + 건물 선택 팝업 생성

이 단계가 가장 복잡합니다. 순서대로 진행하세요.

#### Canvas 생성

1. Hierarchy **최상위**에서 우클릭 → **UI → Canvas**
2. 이름을 `[UI]`로 변경 (다른 그룹과 형식 통일)
3. **Canvas** 컴포넌트:
   - Render Mode: **Screen Space - Overlay**
4. **Canvas Scaler** 컴포넌트:
   - UI Scale Mode: **Scale With Screen Size**
   - Reference Resolution: X = **540**, Y = **960** (9:16 모바일)
   - Match: **0.5** (Width와 Height 반반 기준)

> Canvas 생성 시 **EventSystem** 오브젝트가 자동 추가됩니다.
> 이미 씬에 EventSystem이 있으면 중복 생성된 것을 삭제하세요.

#### BuildingPanel 생성

1. `[UI]` (Canvas) 선택 → 우클릭 → **Create Empty** → 이름: **BuildingPanel**
2. **Add Component** → 스크립트 검색 → **Building Placement UI**
3. **RectTransform** 설정:

   | 항목 | 값 | 설명 |
   |------|------|------|
   | Anchor Preset | **Bottom Center** | 화면 하단 중앙에 고정 |
   | Pivot | (0.5, 0) | 하단 중심 기준 |
   | Pos X | 0 | 중앙 |
   | Pos Y | **80** | 하단에서 80px 위 |
   | Width | **400** | 패널 가로 크기 |
   | Height | **120** | 패널 세로 크기 |

   > Anchor Preset 변경: RectTransform 좌상단의 사각형 아이콘 클릭 → 하단 중앙 선택

4. **BuildingPanel을 비활성 상태로 설정:**
   - Inspector 최상단의 **체크박스를 해제** (GameObject 이름 왼쪽)
   - 이렇게 하면 게임 시작 시 패널이 숨겨진 상태로 시작

#### Background 이미지

1. **BuildingPanel** 선택 → 우클릭 → **UI → Image** → 이름: **Background**
2. **RectTransform**: Anchor Preset → **Stretch All** (Alt+클릭으로 전체 확장)
   - Left, Top, Right, Bottom 모두 **0**
3. **Image** 컴포넌트:

   | 항목 | 값 |
   |------|------|
   | Source Image | `ui_panel_dark` (Sprites/UI/Panels/ 폴더에서 드래그) |
   | Image Type | **Sliced** (9-slice 대응) |
   | Color | White (기본) |

   > Image Type을 Sliced로 변경하면 "Sprite has no border" 경고가 뜰 수 있음.
   > 이 경우 스프라이트의 Inspector에서 Sprite Editor를 열어 Border를 설정하거나,
   > Image Type을 **Simple**로 유지해도 무방.

#### BarracksButton (배럭 버튼)

1. **BuildingPanel** 선택 → 우클릭 → **UI → Button - TextMeshPro** → 이름: **BarracksButton**
   - TextMeshPro 임포트 팝업이 뜨면 **Import TMP Essentials** 클릭
2. **RectTransform**:

   | 항목 | 값 |
   |------|------|
   | Anchor Preset | Middle Left |
   | Pos X | **80** |
   | Pos Y | **0** |
   | Width | **140** |
   | Height | **90** |

3. 하위 **Text (TMP)** 오브젝트 선택:

   | 항목 | 값 |
   |------|------|
   | Text | **배럭** |
   | Font Size | **18** |
   | Alignment | Center (가로/세로 모두) |

4. (선택사항) Button의 **Image** 컴포넌트에 `bld_barracks` 스프라이트를 넣으면 아이콘 표시 가능

#### MiningPostButton (채굴소 버튼)

1. **BuildingPanel** 선택 → 우클릭 → **UI → Button - TextMeshPro** → 이름: **MiningPostButton**
2. **RectTransform**:

   | 항목 | 값 |
   |------|------|
   | Anchor Preset | Middle Center |
   | Pos X | **0** |
   | Pos Y | **0** |
   | Width | **140** |
   | Height | **90** |

3. 하위 **Text (TMP)**: Text = **채굴소**, Font Size = **18**, Alignment = Center

#### CancelButton (취소 버튼)

1. **BuildingPanel** 선택 → 우클릭 → **UI → Button - TextMeshPro** → 이름: **CancelButton**
2. **RectTransform**:

   | 항목 | 값 |
   |------|------|
   | Anchor Preset | Middle Right |
   | Pos X | **-80** |
   | Pos Y | **0** |
   | Width | **80** |
   | Height | **90** |

3. 하위 **Text (TMP)**: Text = **취소**, Font Size = **18**, Alignment = Center

#### 최종 UI Hierarchy

```
[UI] (Canvas - Screen Space Overlay)
├── EventSystem (자동 생성)
└── BuildingPanel (비활성, BuildingPlacementUI 스크립트)
    ├── Background (Image: ui_panel_dark)
    ├── BarracksButton (Button + "배럭")
    ├── MiningPostButton (Button + "채굴소")
    └── CancelButton (Button + "취소")
```

### 최종 씬 Hierarchy

```
SampleScene
├── Main Camera
├── [Managers]
│   ├── GameBootstrapper
│   ├── UnitFactory
│   └── BuildingFactory        ← 신규
├── [World]
│   ├── HexGrid
│   ├── Units
│   └── Buildings              ← 신규
├── [Input]
│   └── InputHandler
├── [UI] (Canvas)              ← 신규
│   └── BuildingPanel
│       ├── Background
│       ├── BarracksButton
│       ├── MiningPostButton
│       └── CancelButton
└── [Debug]
    └── DebugUI
```

---

## Step 4: Inspector 참조 연결

### 4-1. BuildingFactory ([Managers]/BuildingFactory)

Hierarchy에서 `BuildingFactory` 선택 → Inspector에서:

| Inspector 필드 | 드래그 대상 | 소스 |
|----------------|----------|------|
| Castle Prefab | `Building_Castle` | Project 창 `Prefabs/` |
| Barracks Prefab | `Building_Barracks` | Project 창 `Prefabs/` |
| Mining Post Prefab | `Building_MiningPost` | Project 창 `Prefabs/` |
| Building Parent | `Buildings` | Hierarchy `[World]/Buildings` |

### 4-2. BuildingPlacementUI ([UI]/BuildingPanel)

Hierarchy에서 `BuildingPanel` 선택 → Inspector에서:

| Inspector 필드 | 드래그 대상 | 소스 |
|----------------|----------|------|
| Panel | `BuildingPanel` | 자기 자신 (Hierarchy) |
| Barracks Button | `BarracksButton` | Hierarchy `[UI]/BuildingPanel/BarracksButton` |
| Mining Post Button | `MiningPostButton` | Hierarchy `[UI]/BuildingPanel/MiningPostButton` |
| Cancel Button | `CancelButton` | Hierarchy `[UI]/BuildingPanel/CancelButton` |

> **Panel 필드에 자기 자신을 연결하는 방법:**
> BuildingPanel을 Hierarchy에서 Inspector의 Panel 슬롯으로 직접 드래그.

### 4-3. GameBootstrapper ([Managers]/GameBootstrapper) — 신규 슬롯 2개

기존 7개 슬롯은 이미 연결되어 있으므로, **새로 추가된 2개만** 연결:

| Inspector 필드 | 드래그 대상 | 소스 |
|----------------|----------|------|
| Building Factory | `BuildingFactory` | Hierarchy `[Managers]/BuildingFactory` |
| Building UI | `BuildingPanel` | Hierarchy `[UI]/BuildingPanel` |

### 연결 확인 체크리스트

모든 연결 완료 후, 각 컴포넌트의 Inspector에서 **None**으로 표시되는 슬롯이 없는지 확인:

- [ ] BuildingFactory: 프리팹 3개 + Parent 1개 = 4개 슬롯 모두 연결
- [ ] BuildingPlacementUI: Panel + 버튼 3개 = 4개 슬롯 모두 연결
- [ ] GameBootstrapper: 기존 7개 + 신규 2개 = 9개 슬롯 모두 연결

---

## Step 5: 실행 테스트

### 5-0. 저장

**Ctrl+S** 로 씬 저장.

### 5-1. Game 뷰 설정

Game 뷰 해상도가 **Mobile Portrait (1080x1920)** 인지 확인.
(Phase 10에서 이미 설정했으면 생략)

### 5-2. 검증 항목

Play 버튼 클릭 후 다음을 확인:

| # | 확인 항목 | 예상 결과 | 실패 시 확인 |
|---|----------|----------|-------------|
| 1 | Castle 자동 배치 | 맵 하단 중앙에 Blue Castle, 상단 중앙에 Red Castle 표시 | GameBootstrapper의 BuildingFactory 슬롯 확인 |
| 2 | Castle 타일 비이동 | Castle 있는 타일로 유닛 이동 시 A* 우회 경로 사용 | IsWalkable 설정 확인 |
| 3 | 자기 타일 탭 → 팝업 | Blue 소유 빈 타일 탭 → 화면 하단에 건물 선택 패널 표시 | BuildingPanel 비활성 상태 확인, BuildingUI 슬롯 확인 |
| 4 | 배럭 배치 | "배럭" 버튼 탭 → 해당 타일에 배럭 스프라이트 표시 | Barracks Prefab 슬롯, Building Parent 슬롯 확인 |
| 5 | 채굴소 배치 | "채굴소" 버튼 탭 → 해당 타일에 채굴소 스프라이트 표시 | MiningPost Prefab 슬롯 확인 |
| 6 | 배치 후 비이동 | 건물 있는 타일로 유닛 이동 불가 | - |
| 7 | 적 타일 배치 불가 | Red/Neutral 타일 탭 시 팝업 미표시 | - |
| 8 | 건물 중복 방지 | 이미 건물 있는 타일 탭 시 팝업 미표시 | - |
| 9 | 취소 | "취소" 버튼 또는 팝업 외부 탭 시 팝업 닫힘 | CancelButton 슬롯 확인 |
| 10 | 유닛 기능 유지 | 유닛 선택 → 이동 → 공격 → 사망 기존과 동일 동작 | - |

---

## 트러블슈팅

### 건물이 안 보임

| 증상 | 확인 |
|------|------|
| 건물 프리팹이 생성 안 됨 | BuildingFactory의 프리팹 슬롯이 None인지 확인 |
| 건물이 타일 아래에 숨김 | 프리팹의 SpriteRenderer Order in Layer가 50인지 확인 |
| Castle만 안 보임 | Castle Prefab 슬롯만 비어있을 수 있음 |

### 건물이 너무 크거나 작음

→ 건물 프리팹의 **Transform Scale** 조정 (X, Y 동일값).
→ 또는 스프라이트의 **PPU** 확인 (1024인지).

| 증상 | 해결 |
|------|------|
| 건물이 타일보다 훨씬 큼 | Scale을 0.3~0.5로 줄이거나 PPU를 높임 |
| 건물이 타일보다 작음 | Scale을 1.5~2.0으로 늘리거나 PPU를 낮춤 |

### 건물 위치가 타일 중심에서 벗어남

→ GameConfig Inspector에서 **Building Y Offset** 값을 조정.
→ 양수 = 위쪽, 음수 = 아래쪽. 기본값 0.1.

### 팝업이 안 뜸

| 증상 | 확인 |
|------|------|
| 어떤 타일 탭해도 팝업 없음 | GameBootstrapper의 Building UI 슬롯이 연결되어 있는지 확인 |
| Blue 타일 탭해도 팝업 없음 | BuildingPanel이 비활성(체크박스 해제) 상태로 시작하는지 확인. 활성 상태면 코드가 닫기 처리 |
| 유닛이 있는 타일 탭 | 유닛이 있으면 유닛 선택 우선. 빈 타일에서 테스트 |

### 팝업은 뜨지만 버튼 클릭이 안 됨

→ BuildingPlacementUI의 **Barracks Button** / **Mining Post Button** / **Cancel Button** 슬롯이 각각 올바른 Button에 연결되어 있는지 확인.
→ Button 컴포넌트의 **Interactable** 체크박스가 활성인지 확인.

### Canvas가 게임 화면을 가림

→ Canvas의 Render Mode가 **Screen Space - Overlay** 인지 확인.
→ BuildingPanel이 **비활성** 상태로 시작하는지 확인 (Inspector 체크박스 해제).
→ Background Image가 BuildingPanel 전체를 덮는 것은 정상. 패널 외부 탭 시 코드에서 닫음.

---

## 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 0.1.0 | 2026-02-08 | 초기 문서 작성: 스프라이트 Import, 프리팹 3종, 씬 오브젝트(Buildings/BuildingFactory/Canvas+UI), Inspector 연결, 실행 테스트 |

---

**문서 끝**
