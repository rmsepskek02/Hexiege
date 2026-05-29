# Plan — Game UI 규칙 전면 준수 작업

Game 씬에 존재하는 `GameSystemRules.md` 규칙 2(앵커 비율 기반 배치) 위반 57건을 수정합니다.
핵심 목표는 세 가지입니다.
1. 건물 배치 / 유닛 생산 / 비생산 건물 액션 패널 세 곳이 **동일한 높이**, **동일한 CancelButton 위치**를 갖도록 수정한다.
2. 세 패널 내부 버튼·큐·바의 위치와 크기를 앵커 비율 기반으로 전환한다.
3. 게임 종료 UI, 인게임 설정 메뉴, HUD, 재경기 팝업의 나머지 규칙 2 위반 항목을 수정한다.

모든 수정은 에디터 스크립트(1회성 메뉴, `FixGameUIRules.cs`)로 Game.unity에 적용한다.

구현 중 애매한 사항이 있으면 즉시 사용자에게 물어보고 진행할것
임의로 판단하여 진행하는 것을 절대로 금지함.

---

## GameSystemRules 근거

| 수정 항목 | 근거 규칙 |
|----------|----------|
| 모든 sizeDelta/offset 고정 픽셀 → 앵커 비율 전환 | 공통 UI 규칙 2 — 앵커 기반 배치 원칙 |
| 세 패널 동일 높이 | 공통 UI 규칙 2 — 일관된 비율 레이아웃 |
| CancelButton 동일 위치 | 공통 UI 규칙 2 — 일관된 비율 레이아웃 |

---

## 확정된 설계 결정

| 항목 | 결정 |
|------|------|
| 패널 내부 버튼 배치 방식 | **옵션 A** — LayoutElement(flexibleWidth=1) + HorizontalLayoutGroup 자동 분배 |
| 생산 패널 수직 영역 비율 | **1:1:1** — 유닛 버튼 영역 33% / 생산 큐 33% / 생산 바 33% |
| CancelButton 초기 위치 | anchorMin=(0.87, 0.78), anchorMax=(1.0, 1.0), sizeDelta=(0,0) → **시각 확인 후 조정** |
| HUD 바 높이 | **규칙 준수 방향** — HUD 바 자체도 SafeAreaContainer 기준 비율로 변경 |
| 인게임 설정 메뉴 패널 초기 크기 | anchorMin=(0.1, 0.3), anchorMax=(0.9, 0.7), sizeDelta=(0,0) → **시각 확인 후 조정** |

---

## 구현 방법

기존 `FixUIColorAndLayout.cs`와 분리하여 **신규 파일 `Assets/Editor/FixGameUIRules.cs`** 생성.
메뉴 경로: `Hexiege/Setup/Fix Game UI Rules`

실행 순서: Game.unity 열기 → Step 1~7 순차 실행 → 씬 저장 → 원래 씬 복구

---

## Step 1 — 세 패널 높이 통일 확인 및 재적용

**대상**: BuildingPlacementUI 패널, ProductionPanelUI 패널, BuildingActionPanelUI 패널

**목표값** (기존 `17_39_ui-color-layout-fix` Plan에서 확정된 값):

| 패널 | anchorMin | anchorMax | sizeDelta |
|------|-----------|-----------|-----------|
| BuildingPopup | (X유지, 0.0) | (X유지, 0.4) | (0, 0) |
| ProductionPopup | (X유지, 0.0) | (X유지, 0.4) | (0, 0) |
| BuildingActionPanel | (X유지, 0.0) | (X유지, 0.4) | (0, 0) |

**구현**: 각 패널 최상위 RectTransform을 SerializedObject로 찾아 값 적용.

---

## Step 2 — CancelButton 위치 통일 (세 패널 공통)

**대상**: BuildingPlacementUI._cancelButton, ProductionPanelUI._cancelButton, BuildingActionPanelUI._cancelButton

**목표값** (초기값 — 시각 확인 후 조정 예정):

| 항목 | 값 | 의미 |
|------|----|------|
| anchorMin | (0.87, 0.78) | 패널 우상단 기준 |
| anchorMax | (1.0, 1.0) | 패널 우상단 코너 |
| pivot | (1.0, 1.0) | 버튼 우상단 기준점 |
| sizeDelta | (0, 0) | 앵커 영역 전체 채움 |

**구현**: SerializedObject → `_cancelButton` 필드 → RectTransform 적용.

---

## Step 3 — 건물 배치 패널 내부 버튼 비율화 (BuildingPlacementUI)

**방식**: 옵션 A — LayoutElement + HorizontalLayoutGroup

**대상 및 목표값**:

| GO 이름 | 처리 방법 | 목표 anchorMin | 목표 anchorMax | sizeDelta |
|---------|----------|--------------|--------------|-----------|
| BuildingButtons1~3 (컨테이너) | 앵커 비율 전환 | (0.0, Y비율) | (1.0, Y비율) | (0, 0) |
| Button1~3 (각 버튼) | LayoutElement 추가, flexibleWidth=1 | 현행 유지 (LayoutGroup이 관리) | — | (0, 0) |

> ⚠️ BuildingButtons 컨테이너의 Y 비율은 씬 계층 구조 확인 후 에디터 스크립트에서 결정.
> 컨테이너가 HorizontalLayoutGroup을 이미 가지고 있는지 확인 필요.

---

## Step 4 — 유닛 생산 패널 내부 레이아웃 비율화 (ProductionPanelUI)

**방식**: 옵션 A — LayoutElement + HorizontalLayoutGroup / 수직 영역 1:1:1

### 4-1. 유닛 버튼 영역 (패널 상단 1/3)

| GO 이름 | 처리 방법 | 목표 anchorMin | 목표 anchorMax | sizeDelta |
|---------|----------|--------------|--------------|-----------|
| UnitButtons 컨테이너 | 앵커 비율 전환 | (0.0, 0.67) | (1.0, 1.0) | (0, 0) |
| Slot1~9 (유닛 버튼) | LayoutElement flexibleWidth=1, flexibleHeight=1 | LayoutGroup 관리 | — | (0, 0) |
| SlotImage (초상화) | 부모 Slot 기준 stretch | (0.0, 0.0) | (1.0, 1.0) | (0, 0) |

### 4-2. 생산 큐 영역 (패널 중간 1/3)

| GO 이름 | 처리 방법 | 목표 anchorMin | 목표 anchorMax | sizeDelta |
|---------|----------|--------------|--------------|-----------|
| Slot1~3 (큐 슬롯) | 앵커 비율 전환 | 균등 배치 | 균등 배치 | (0, 0) |

> 큐 슬롯 3개를 가로로 균등 배치: Slot1=(0.0~0.33), Slot2=(0.33~0.67), Slot3=(0.67~1.0), 세로=(0.34~0.67)

### 4-3. 생산 바 영역 (패널 하단 1/3)

| GO 이름 | 처리 방법 | 목표 anchorMin | 목표 anchorMax | sizeDelta |
|---------|----------|--------------|--------------|-----------|
| ProgressFill 컨테이너 | 앵커 비율 전환 | (0.0, 0.0) | (1.0, 0.33) | (0, 0) |
| _progressFill (fill 이미지) | 의도적 stretch — 현행 유지 | (0, 0) | (1, 1) | (0, 0) |

### 4-4. 하단 버튼 행 (철거/업그레이드/랠리 등)

| GO 이름 | 처리 방법 | 목표 anchorMin | 목표 anchorMax | sizeDelta |
|---------|----------|--------------|--------------|-----------|
| Buttons 컨테이너 | 앵커 비율 전환 | (0.0, Y) | (1.0, Y) | (0, 0) |
| DestroyButton, UpgradeButton, RallyPointButton | LayoutElement flexibleWidth=1 | LayoutGroup 관리 | — | (0, 0) |

### 4-5. 골드/인구 정보 텍스트 및 아이콘

| GO 이름 | 목표 anchorMin | 목표 anchorMax | sizeDelta |
|---------|--------------|--------------|-----------|
| GoldIcon | 패널 내 비율 위치 | 패널 내 비율 위치 | (0, 0) |
| GoldText | 패널 내 비율 위치 | 패널 내 비율 위치 | (0, 0) |
| PopIcon | 패널 내 비율 위치 | 패널 내 비율 위치 | (0, 0) |
| PopText | 패널 내 비율 위치 | 패널 내 비율 위치 | (0, 0) |

> ⚠️ 정확한 위치는 씬 계층 구조 확인 후 에디터 스크립트에서 결정.

---

## Step 5 — 비생산 건물 액션 패널 내부 비율화 (BuildingActionPanelUI)

**방식**: 옵션 A — LayoutElement + HorizontalLayoutGroup

| GO 이름 | 처리 방법 | 목표 anchorMin | 목표 anchorMax | sizeDelta |
|---------|----------|--------------|--------------|-----------|
| Buttons 컨테이너 | 앵커 비율 전환 | (0.0, 0.0) | (1.0, 0.4) | (0, 0) |
| DestroyButton | LayoutElement flexibleWidth=1 | LayoutGroup 관리 | — | (0, 0) |

---

## Step 6 — 게임 종료 UI (규칙 2 위반만 수정, 시각 문제 없음)

**대상**: ResultText, RestartButton, LobbyButton, CountdownText

| GO 이름 | 목표 anchorMin | 목표 anchorMax | sizeDelta | 추가 작업 |
|---------|--------------|--------------|-----------|---------|
| ResultText | (0.1, 0.62) | (0.9, 0.75) | (0, 0) | TMP AutoSize ON (min=18, max=60) |
| RestartButton | (0.15, 0.43) | (0.85, 0.55) | (0, 0) | 버튼 내부 텍스트 AutoSize ON |
| LobbyButton | (0.15, 0.29) | (0.85, 0.41) | (0, 0) | 버튼 내부 텍스트 AutoSize ON |
| CountdownText | 현행 유지 | 현행 유지 | 현행 유지 | — |

> ⚠️ GO 이름은 씬에서 직접 확인 필요 (Research.md 조사 미완료).
버튼에 Assets/_Project/Sprites/UI/Buttons/ui_btn_gold_normal.png 에셋 적용 바람.
---

## Step 7 — 인게임 설정 메뉴 (InGameSettingsUI)

**초기값** — 시각 확인 후 조정 예정.

| GO 이름 | 목표 anchorMin | 목표 anchorMax | sizeDelta |
|---------|--------------|--------------|-----------|
| Panel (패널 본체) | (0.1, 0.3) | (0.9, 0.7) | (0, 0) |
| CloseButton | Panel 기준 (0.88, 0.80) | Panel 기준 (1.0, 1.0) | (0, 0) |
| SoundButton | Panel 기준 (0.05, 0.50) | Panel 기준 (0.95, 0.75) | (0, 0) |
| ForfeitButton | Panel 기준 (0.05, 0.15) | Panel 기준 (0.95, 0.40) | (0, 0) |
| ButtonRow (ConfirmPopup 내) | Panel 기준 (0.0, 0.0) | Panel 기준 (1.0, 0.35) | (0, 0) |
| CancelButton (ConfirmPopup) | ButtonRow 기준 (0.0, 0.0) | ButtonRow 기준 (0.48, 1.0) | (0, 0) |
| ConfirmButton (ConfirmPopup) | ButtonRow 기준 (0.52, 0.0) | ButtonRow 기준 (1.0, 1.0) | (0, 0) |

---

## Step 8 — HUD 비율화 (GameHudUI)

**HUD 바 자체**: `sizeDelta.y` 고정 100px → SafeAreaContainer 기준 비율로 변경.

| GO 이름 | 목표 anchorMin | 목표 anchorMax | sizeDelta | 비고 |
|---------|--------------|--------------|-----------|------|
| GameHUD (바 자체) | (0.0, 0.93) | (1.0, 1.0) | (0, 0) | SafeAreaContainer 상단 7% |
| GoldImage | HUD 기준 (0.02, 0.1) | HUD 기준 (0.08, 0.9) | (0, 0) | 정사각형 → AspectRatioFitter 추가 |
| GoldText | HUD 기준 (0.09, 0.1) | HUD 기준 (0.22, 0.9) | (0, 0) | TMP AutoSize ON |
| PopulationImage | HUD 기준 (0.24, 0.1) | HUD 기준 (0.30, 0.9) | (0, 0) | AspectRatioFitter 추가 |
| PopulationText | HUD 기준 (0.31, 0.1) | HUD 기준 (0.46, 0.9) | (0, 0) | TMP AutoSize ON |
| BlueHexTile | HUD 기준 (0.50, 0.1) | HUD 기준 (0.56, 0.9) | (0, 0) | AspectRatioFitter 추가 |
| BlueHexTileText | HUD 기준 (0.57, 0.1) | HUD 기준 (0.68, 0.9) | (0, 0) | TMP AutoSize ON |
| RedHexTile | HUD 기준 (0.72, 0.1) | HUD 기준 (0.78, 0.9) | (0, 0) | AspectRatioFitter 추가 |
| RedHexTileText | HUD 기준 (0.79, 0.1) | HUD 기준 (0.90, 0.9) | (0, 0) | TMP AutoSize ON |

> ⚠️ HUD 내 요소들의 정확한 GO 경로 및 배치 순서는 씬 확인 후 조정 필요.
> SettingsButton은 TC-SINGLE-HUD-007에서 현행 유지 결정됨 — 이 작업에서 제외.

---

## Step 9 — 재경기 팝업 ButtonArea (RematchRequestPopup)

**대상**: RematchRequestPopup._declinedConfirmButton (ButtonArea GO)

| 항목 | 값 |
|------|----|
| anchorMin | (0.15, 0.05) |
| anchorMax | (0.85, 0.30) |
| pivot | (0.5, 0.0) |
| sizeDelta | (0, 0) |

> ⚠️ _acceptButton / _declineButton과 시각적으로 동일한 크기가 되도록 실기기 확인 필요.

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| HorizontalLayoutGroup이 없는 컨테이너에 LayoutElement를 추가해도 효과 없음 | 에디터 스크립트에서 LayoutGroup 존재 여부 먼저 확인, 없으면 추가 |
| TMP Auto Size 적용 시 특정 기기에서 폰트가 너무 커지거나 작아질 수 있음 | min=18, max=60으로 보수적 범위 설정 |
| HUD GO 이름이 YAML 분석값과 다를 수 있음 | 에디터 스크립트에서 컴포넌트 타입(GameHudUI)으로 탐색하여 자식 GO 확인 |
| Step 3~5 버튼들이 Layout Group의 child force expand 설정에 따라 이미 stretch 중일 수 있음 | 적용 전 현재 childForceExpandWidth/Height 값 로그 출력 |
| Undo.RecordObject 누락 시 Ctrl+Z 불가 | 모든 RT/컴포넌트 수정 전 Undo.RecordObject 적용 필수 |

---

## 🔴 구현 시 추가 확인 필요 항목

에디터 스크립트 작성 전 씬에서 직접 확인해야 하는 항목입니다.
game-programmer 에이전트가 씬을 열어 아래를 확인한 후 구현합니다.

1. **세 패널 내부 계층 구조** — BuildingButtons1~3, UnitButtons, Buttons GO의 정확한 부모-자식 관계
2. **생산 패널 수직 영역 컨테이너** — 유닛 버튼 / 큐 / 바 영역을 감싸는 GO 이름과 현재 앵커값
3. **GameEndUI GO 이름** — ResultText, RestartButton, LobbyButton에 해당하는 정확한 GO 이름
4. **HUD 컨테이너 구조** — GameHUD GO 현재 anchorMin/Max 및 자식 GO 배치 순서
5. **InGameSettingsUI Panel 기준점** — Panel GO의 부모가 Canvas인지 SafeAreaContainer인지

