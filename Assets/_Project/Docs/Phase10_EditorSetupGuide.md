# Phase 10: Unity 에디터 작업 가이드

**버전:** 0.7.0
**최종 수정일:** 2026-02-08
**작성자:** HANYONGHEE

---

## 📋 목차

1. [사전 준비: 폴더 생성](#사전-준비-폴더-생성)
2. [Step 0: Input System 설정](#step-0-input-system-설정)
3. [Step 1: 스프라이트 Import 설정](#step-1-스프라이트-import-설정)
3. [Step 2: ScriptableObject 에셋 생성](#step-2-scriptableobject-에셋-생성)
4. [Step 3: 프리팹 생성](#step-3-프리팹-생성)
5. [Step 4: 씬 오브젝트 구성](#step-4-씬-오브젝트-구성)
6. [Step 5: 컴포넌트 부착](#step-5-컴포넌트-부착)
7. [Step 6: Main Camera 설정](#step-6-main-camera-설정)
8. [Step 7: Inspector 참조 연결](#step-7-inspector-참조-연결)
9. [Step 8: 실행 테스트](#step-8-실행-테스트)
10. [트러블슈팅](#트러블슈팅)

---

## 사전 준비: 폴더 생성

Project 창에서:

1. `Assets/_Project/` 우클릭 → Create → Folder → **`Resources`**
2. `Resources/` 우클릭 → Create → Folder → **`Config`**

최종 경로: `Assets/_Project/Resources/Config/`

---

## Step 0: Input System 설정

프로젝트가 **New Input System** 패키지를 사용하도록 설정되어 있어야 합니다.
코드가 `UnityEngine.InputSystem` (New Input System)을 사용하므로 아래 설정을 확인합니다.

### 0-1. Player Settings 확인

1. **Edit → Project Settings → Player → Other Settings**
2. **Active Input Handling** 항목 확인:
   - **Input System Package (New)** 또는 **Both** 로 설정되어 있어야 함
   - "Input Manager (Old)"로만 되어 있으면 에러 발생

> 변경 시 Unity가 에디터 재시작을 요청합니다. **Yes**를 눌러 재시작.

### 0-2. Input System 패키지 확인

1. **Window → Package Manager** 열기
2. 좌측 목록에서 **Input System** 검색
3. 설치되어 있는지 확인 (보통 Unity 6 프로젝트에는 기본 포함)
4. 미설치 시: **Install** 클릭

### 레거시 Input vs New Input System 대응표

| 레거시 (UnityEngine.Input) | New Input System (UnityEngine.InputSystem) |
|---|---|
| `Input.mousePosition` | `Mouse.current.position.ReadValue()` |
| `Input.GetMouseButtonDown(0)` | `Mouse.current.leftButton.wasPressedThisFrame` |
| `Input.GetMouseButton(0)` | `Mouse.current.leftButton.isPressed` |
| `Input.GetMouseButtonUp(0)` | `Mouse.current.leftButton.wasReleasedThisFrame` |
| `Input.mouseScrollDelta.y` | `Mouse.current.scroll.ReadValue().y / 120f` |
| `Input.touchCount` | `EnhancedTouch.Touch.activeTouches.Count` |
| `Input.GetTouch(i)` | `EnhancedTouch.Touch.activeTouches[i]` |

---

## Step 1: 스프라이트 Import 설정

스프라이트의 PPU(Pixels Per Unit)를 설정.
PPU는 "1024픽셀 = Unity 월드 몇 유닛"을 결정.

### 1-1. 타일 스프라이트

Project 창에서 `Sprites/Tiles/tile_hex.png` 선택 → Inspector:

| 항목 | 값 |
|------|-----|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| **Pixels Per Unit** | **1024** |
| Filter Mode | Bilinear |
| Compression | None |

→ **Apply** 클릭

> PPU=1024이면 1024px 스프라이트 = 1.0 월드 유닛.
> TileWidth=0.866으로 배치 시 인접 타일이 자연스럽게 겹침.

### 1-2. 유닛 스프라이트 (14장 일괄 설정)

Project 창에서 `Sprites/Units/Pistoleer/` 하위의 **모든 .png 파일을 전부 선택**
(Idle 3장 + Walk 4장 + Attack 6장 + portrait 1장 = 14장):

**다중 선택 방법**: 첫 파일 클릭 → Shift+클릭으로 마지막 파일까지 선택
(하위 폴더별로 나눠서 해도 됨)

| 항목 | 값 |
|------|-----|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| **Pixels Per Unit** | **1536** |
| Pivot | Center |
| Filter Mode | Bilinear |
| Compression | None |

→ **Apply** 클릭

> PPU=1536이면 유닛이 타일의 약 2/3 크기로 표시.
> Pivot=Center 기본값 사용. UnitYOffset으로 타일 위 위치 조정.

### PPU 조정 기준

| PPU | 유닛 크기 | 비고 |
|-----|----------|------|
| 1024 | 타일과 동일 크기 | 너무 큼 |
| **1536** | **타일의 ~67%** | **권장** |
| 2048 | 타일의 50% | 너무 작을 수 있음 |

실행 후 크기가 안 맞으면 PPU 값을 조정.

---

## Step 2: ScriptableObject 에셋 생성

### 2-1. GameConfig.asset

1. Project 창에서 `Resources/Config/` 폴더 진입
2. 우클릭 → **Create → Hexiege → GameConfig**
3. 이름 그대로 **"GameConfig"**
4. 선택 후 Inspector에서 값 확인:

#### PointyTop Grid (접이식 그룹)

| 항목 | 값 | 비고 |
|------|-----|------|
| Grid Width | **7** | 모바일 9:16 포트레이트 기준 |
| Grid Height | **17** | 세로 타일 수 |
| Tile Width | **0.866** | pointy-top hex 이론값 (√3/2) |
| Tile Height | **0.82** | 약간 기울어진 시점 (탑다운 입체감) |

#### FlatTop Grid (접이식 그룹)

| 항목 | 값 | 비고 |
|------|-----|------|
| Grid Width | **10** | flat-top 가로 타일 수 |
| Grid Height | **29** | flat-top 세로 타일 수 |
| Tile Width | **1.0** | flat-top hex 폭 |
| Tile Height | **0.36** | 프리팹 Y Scale=0.4 기준 아이소메트릭 높이 |

#### 공통 설정

| 항목 | 값 | 비고 |
|------|-----|------|
| Neutral Color | RGB(178,178,178) | 기본값 그대로 |
| Blue Team Color | RGB(77,128,230) | 기본값 그대로 |
| Red Team Color | RGB(230,77,77) | 기본값 그대로 |
| Selected Tint | RGB(255,255,128) | 기본값 그대로 |
| Unit Move Seconds | 0.3 | 기본값 그대로 |
| Unit Y Offset | **0.15** | 유닛이 타일 위에 서있는 느낌 |
| Animation Fps | 6 | 기본값 그대로 |
| Camera Zoom Min | **2** | 모바일 세로 기준 |
| Camera Zoom Max | **7** | 맵에 맞게 축소 |
| Camera Zoom Default | **5** | 전체 맵 너비가 딱 보이는 줌 레벨 |
| Camera Zoom Speed | 2 | 기본값 그대로 |
| Camera Pan Speed | 1 | 기본값 그대로 |

> **중요:** ScriptableObject 필드 구조가 변경되면 기존 Inspector 값이 초기화될 수 있습니다.
> 위 표의 값을 Inspector에서 직접 확인/입력해야 합니다.

### 2-2. PistoleerAnimData.asset

1. `Resources/Config/` 에서 우클릭 → **Create → Hexiege → UnitAnimationData**
2. 이름을 **"PistoleerAnimData"** 로 변경
3. 선택 후 Inspector에서 각 배열에 스프라이트 연결

#### Idle (대기) — 각 배열 Size = 1

| 배열 | Size | Element 0 |
|------|------|-----------|
| Idle NE | 1 | `Sprites/Units/Pistoleer/Idle/pistoleer_idle_ne_01` |
| Idle E | 1 | `Sprites/Units/Pistoleer/Idle/pistoleer_idle_e_01` |
| Idle SE | 1 | `Sprites/Units/Pistoleer/Idle/pistoleer_idle_se_01` |

#### Walk (이동) — NE/SE는 Size=1, E는 Size=2

| 배열 | Size | Element 0 | Element 1 |
|------|------|-----------|-----------|
| Walk NE | 1 | `pistoleer_walk_ne_01` | — |
| Walk E | **2** | `pistoleer_walk_e_01` | `pistoleer_walk_e_02` |
| Walk SE | 1 | `pistoleer_walk_se_01` | — |

#### Attack (공격) — 각 배열 Size = 2

| 배열 | Size | Element 0 | Element 1 |
|------|------|-----------|-----------|
| Attack NE | 2 | `pistoleer_attack_ne_01` | `pistoleer_attack_ne_02` |
| Attack E | 2 | `pistoleer_attack_e_01` | `pistoleer_attack_e_02` |
| Attack SE | 2 | `pistoleer_attack_se_01` | `pistoleer_attack_se_02` |

**연결 방법:**
1. 배열 이름 왼쪽의 ▶ 클릭하여 펼침
2. **Size** 숫자를 입력 (1 또는 2)
3. Element 칸이 생기면 Project 창에서 해당 스프라이트를 **Element 칸으로 드래그 앤 드롭**

---

## Step 3: 프리팹 생성

### 3-1. HexTile_PointyTop.prefab

1. **Hierarchy** 빈 곳 우클릭 → Create Empty → 이름 **"HexTile_PointyTop"**
2. **Add Component** → **Sprite Renderer**
   - Sprite: Project 창에서 `tile_hex.png` 드래그
   - Color: **흰색** (런타임에 HexTileView가 변경)
   - Sorting Layer: Default
   - Order in Layer: **0**
3. **Transform → Scale Y**: **0.4** (아이소메트릭 효과)
4. **Add Component** → **Polygon Collider 2D**
   - 자동으로 헥스 모양에 맞는 콜라이더 생성됨
5. **Add Component** → 스크립트 검색 → **Hex Tile View**
6. Hierarchy에서 Project 창의 **`Prefabs/`** 폴더로 **드래그**
   - "Original Prefab" 선택
7. Hierarchy에서 인스턴스 **삭제** (Delete)

### 3-1b. HexTile_FlatTop.prefab

1. **Hierarchy** 빈 곳 우클릭 → Create Empty → 이름 **"HexTile_FlatTop"**
2. **Add Component** → **Sprite Renderer**
   - Sprite: Project 창에서 `tile_hex_flat.png` 드래그
   - Color: **흰색**
   - Sorting Layer: Default
   - Order in Layer: **0**
3. **Transform → Scale Y**: **0.4** (아이소메트릭 효과, PointyTop과 동일)
4. **Add Component** → **Polygon Collider 2D**
5. **Add Component** → 스크립트 검색 → **Hex Tile View**
6. Hierarchy에서 Project 창의 **`Prefabs/`** 폴더로 **드래그**
7. Hierarchy에서 인스턴스 **삭제**

### 3-2. Unit_Pistoleer.prefab

1. Hierarchy 빈 곳 우클릭 → Create Empty → 이름 **"Unit_Pistoleer"**
2. **Add Component** → **Sprite Renderer**
   - Sprite: `pistoleer_idle_e_01` 드래그
   - Sorting Layer: Default
   - **Order in Layer: 100** (타일보다 항상 위에 표시)
3. **Add Component** → 스크립트 검색 → **Frame Animator**
4. **Add Component** → 스크립트 검색 → **Unit View**
5. Hierarchy에서 Project 창의 **`Prefabs/`** 폴더로 **드래그**
   - "Original Prefab" 선택
6. Hierarchy에서 "Unit_Pistoleer" 인스턴스 **삭제**

> Order in Layer를 100으로 설정하는 이유:
> 타일은 sortingOrder = row (0~29). 유닛은 100 이상으로 하면 항상 타일 위에 표시.

---

## Step 4: 씬 오브젝트 구성

SampleScene에서 다음 계층 구조를 만든다.

### 4-1. 최상위 빈 오브젝트 생성

Hierarchy에서:

1. 우클릭 → Create Empty → 이름 **`[Managers]`** (Position 0,0,0)
2. 우클릭 → Create Empty → 이름 **`[World]`** (Position 0,0,0)
3. 우클릭 → Create Empty → 이름 **`[Input]`** (Position 0,0,0)
4. 우클릭 → Create Empty → 이름 **`[Debug]`** (Position 0,0,0)

### 4-2. [World] 하위 오브젝트

1. **[World]** 우클릭 → Create Empty → 이름 **"HexGrid"**
2. **[World]** 우클릭 → Create Empty → 이름 **"Units"**

### 4-3. [Managers] 하위 오브젝트

1. **[Managers]** 우클릭 → Create Empty → 이름 **"GameBootstrapper"**
2. **[Managers]** 우클릭 → Create Empty → 이름 **"UnitFactory"**

### 4-4. [Input] 하위 오브젝트

1. **[Input]** 우클릭 → Create Empty → 이름 **"InputHandler"**

### 4-5. [Debug] 하위 오브젝트

1. **[Debug]** 우클릭 → Create Empty → 이름 **"DebugUI"**

### 최종 Hierarchy 구조

```
SampleScene
├── Main Camera          ← 기존 카메라 (수정만)
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

---

## Step 5: 컴포넌트 부착

각 오브젝트를 선택하고 Add Component로 스크립트를 부착.

| 오브젝트 | Add Component (스크립트 검색) |
|----------|-------------------------------|
| **Main Camera** | Camera Controller |
| **HexGrid** | Hex Grid Renderer |
| **GameBootstrapper** | Game Bootstrapper |
| **UnitFactory** | Unit Factory |
| **InputHandler** | Input Handler |
| **DebugUI** | Debug UI |

> Main Camera에는 이미 Camera와 Audio Listener가 있으므로 CameraController만 추가.
> InputHandler와 DebugUI에는 Inspector 필드가 없음 (런타임 초기화).

---

## Step 6: Main Camera 설정

Main Camera 선택 → Inspector:

| 항목 | 값 |
|------|-----|
| Position | **(0, 0, -10)** |
| Projection | **Orthographic** |
| Size | **5** (모바일 세로 기준) |
| Background | **#1A1A2E** (어두운 남색) |
| Clear Flags | Solid Color |

Background 색상 설정:
1. Background 색상 클릭 → Color Picker 열림
2. Hexadecimal 입력란에 **1A1A2E** 입력

---

## Step 7: Inspector 참조 연결

**가장 중요한 단계.** 각 컴포넌트의 빈 슬롯에 올바른 참조를 드래그.

### 7-1. Main Camera → CameraController

| 필드 | 드래그 대상 |
|------|------------|
| Config | `Resources/Config/GameConfig` (Project 창에서) |

### 7-2. HexGrid → HexGridRenderer

| 필드 | 드래그 대상 |
|------|------------|
| Pointy Top Tile Prefab | `Prefabs/HexTile_PointyTop` (Project 창에서) |
| Flat Top Tile Prefab | `Prefabs/HexTile_FlatTop` (Project 창에서) |
| Config | `Resources/Config/GameConfig` (Project 창에서) |

### 7-3. UnitFactory → UnitFactory

| 필드 | 드래그 대상 |
|------|------------|
| Unit Prefab | `Prefabs/Unit_Pistoleer` (Project 창에서) |
| Unit Parent | **Units** (Hierarchy의 [World]/Units) |

### 7-4. GameBootstrapper → GameBootstrapper (슬롯 7개)

| 필드 | 드래그 대상 |
|------|------------|
| Config | `Resources/Config/GameConfig` (Project 창) |
| Pistoleer Anim Data | `Resources/Config/PistoleerAnimData` (Project 창) |
| Grid Renderer | **HexGrid** (Hierarchy의 [World]/HexGrid) |
| Camera Controller | **Main Camera** (Hierarchy) |
| Input Handler | **InputHandler** (Hierarchy의 [Input]/InputHandler) |
| Unit Factory | **UnitFactory** (Hierarchy의 [Managers]/UnitFactory) |
| Main Camera | **Main Camera** (Hierarchy) |

> Hierarchy에서 드래그할 때는 오브젝트를 Inspector의 해당 슬롯으로 직접 드래그.
> Project 창에서 드래그할 때는 .asset 또는 .prefab 파일을 드래그.

---

## Step 8: 실행 테스트

1. **Ctrl+S** 로 씬 저장
2. **Play 버튼** 클릭

### Game 뷰 모바일 세로 설정

1. Game 뷰 상단의 해상도 드롭다운 클릭
2. **+** 버튼 클릭하여 새 해상도 추가
3. Label: **Mobile Portrait**, Width: **1080**, Height: **1920** 입력
4. 새로 만든 해상도를 선택하여 세로 비율(9:16)로 테스트

### 확인 체크리스트

| # | 항목 | 기대 결과 |
|---|------|----------|
| 1 | 타일 그리드 | FlatTop 10×29 육각형이 빈틈/겹침 없이 회색으로 표시 (기본 맵) |
| 2 | 유닛 표시 | 맵 하단(Blue)과 상단(Red)에 권총병 표시 |
| 3 | 타일 클릭 | 클릭 시 노란 하이라이트, 재클릭 시 해제 |
| 4 | 유닛 클릭→타일 클릭 | 유닛이 경로를 따라 이동 + 지나간 타일 팀 색상 변경 |
| 5 | 이동 방향 전환 | 이동 방향에 따라 스프라이트가 전환 (flipX 포함) |
| 6 | Walk 애니메이션 | E방향 이동 시 2프레임 걷기 애니메이션 재생 |
| 6-1 | 전투 (자동 공격) | 이동 완료 후 인접 적 유닛을 자동 공격, Attack 애니메이션 재생 |
| 6-2 | 사망 처리 | 적 HP가 0 이하가 되면 화면에서 제거됨 |
| 7 | 카메라 드래그 | 마우스 드래그로 맵 이동 |
| 8 | 마우스 스크롤 | 줌 인/아웃 (Size 2~7 범위) |
| 9 | 디버그 UI | 좌상단에 FPS, 마우스 좌표, 타일 정보 표시 |

---

## 트러블슈팅

### 타일이 너무 크거나 작음

→ 타일 스프라이트의 **PPU** 조정 또는 GameConfig의 **OrientationConfig.TileWidth/TileHeight** 변경.

| 증상 | 해결 |
|------|------|
| 타일이 너무 큼 | PPU를 높이거나 TileWidth/Height를 낮춤 |
| 타일이 너무 작음 | PPU를 낮추거나 TileWidth/Height를 높임 |
| 타일 사이에 빈틈 | 스프라이트가 캔버스를 꽉 채우는지 확인. PointyTop: 0.866/0.82, FlatTop: 1.0/0.36 |

### 유닛이 타일보다 크거나 작음

→ 유닛 스프라이트의 **PPU** 조정.

| 증상 | 해결 |
|------|------|
| 유닛이 너무 큼 | PPU 높이기 (예: 2048) |
| 유닛이 너무 작음 | PPU 낮추기 (예: 1024) |

### 클릭이 안 먹힘

→ HexTile 프리팹에 **Polygon Collider 2D**가 있는지 확인.
→ Main Camera가 **Orthographic**인지 확인.

### 유닛이 안 보임

→ Unit_Pistoleer 프리팹의 **Order in Layer**가 타일보다 높은지 확인 (100 권장).
→ SpriteRenderer에 스프라이트가 연결되어 있는지 확인.
→ FlatTop 타일의 sortingOrder 범위는 약 0~30이므로 유닛 100과 충돌 없음.

### Input 관련 에러 (InvalidOperationException)

에러 메시지: `You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package`

→ 이 에러는 **발생하지 않아야 함** (코드가 New Input System 사용).
→ 만약 발생하면 코드가 최신 버전인지 확인. InputHandler.cs, CameraController.cs, DebugUI.cs가
   `using UnityEngine.InputSystem;`을 사용하고 `Mouse.current` 등을 호출해야 함.

### 컴파일 에러

→ Console 창(Ctrl+Shift+C)에서 에러 메시지 확인.
→ `UnityEngine.InputSystem` 네임스페이스 못 찾음: Input System 패키지 미설치. Window → Package Manager에서 설치.
→ UniRx 네임스페이스 누락 시: UniRx 패키지가 설치되어 있는지 Packages/manifest.json 확인.

### 아무것도 안 보임

→ GameBootstrapper의 Inspector 슬롯이 모두 연결되어 있는지 확인.
→ 특히 **Grid Renderer**, **Config** 슬롯이 비어있으면 그리드가 생성되지 않음.

---

## 📝 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 0.7.0 | 2026-02-08 | 듀얼 Orientation: GameConfig OrientationConfig 그룹화, HexTile_PointyTop/FlatTop 프리팹 분리, HexGridRenderer 듀얼 프리팹 슬롯, FlatTop 기본 맵 |
| 0.6.0 | 2026-02-07 | 전투 시스템 반영: 실행 테스트 체크리스트에 자동 공격/사망 처리 항목 추가 |
| 0.5.0 | 2026-02-03 | 모바일 9:16 대응: GridWidth 11→7, GridHeight 17→30, TileWidth=0.866, TileHeight=0.82, UnitYOffset=0.15, CameraZoomMax=7, 타일 Scale Y=0.82 |
| 0.4.0 | 2026-02-03 | 타일 빈틈 수정: Python으로 hex 스프라이트 재생성 (패딩 제거) |
| 0.3.0 | 2026-02-03 | 타일 간격 수정(TileWidth=0.866), 유닛 Y오프셋, 카메라 기본값 조정, 모바일 세로 가이드 추가 |
| 0.2.0 | 2026-02-03 | New Input System 대응: Step 0 추가, 트러블슈팅 항목 추가 |
| 0.1.0 | 2026-02-03 | 초기 문서 작성 |

---

**문서 끝**
