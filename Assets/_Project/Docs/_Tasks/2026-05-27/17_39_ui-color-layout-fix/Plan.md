# Plan — UI 색상 일관성 + 레이아웃 수정

게임 화면 UI TC FAIL 항목 9개(레이아웃/색상)를 수정합니다.
색상 관련 수정은 `UIColorConfig` ScriptableObject를 먼저 도입한 뒤 기존 하드코딩 색상을 교체하는 방식으로 진행합니다.
레이아웃 수정은 UI별로 하나씩 Inspector에서 수정하고 실기기 테스트로 확인한 뒤 다음 UI로 넘어갑니다.

---

## GameSystemRules 근거

| 수정 항목 | 근거 규칙 |
|----------|----------|
| 골드 부족 텍스트 색상 (BuildingPlacementUI, ProductionPanelUI) | 공통 UI 규칙 7 — 비용 텍스트 색상 |
| 인구 만원 텍스트 색상 (GameHudUI) | 생산 패널 UI 규칙 16 — 인구 가득 상태 표시 |
| ConfirmPopup 버튼 색상 | 공통 UI 규칙 8 — 팝업 타입 구분 (모달 타입에 해당) |
| 레이아웃 수정 전반 | 공통 UI 규칙 1 (Canvas Scaler), 규칙 2 (앵커 기반 배치) |

---

## 수정 항목 요약

| 단계 | 작업 | 파일 | 방식 |
|------|------|------|------|
| **Step 1** | UIColorConfig.cs 생성 | `Infrastructure/Config/UIColorConfig.cs` | 신규 코드 |
| **Step 2** | UIColorConfig.asset 생성 | `Resources/Config/UIColorConfig.asset` | 에디터 스크립트 또는 수동 |
| **Step 3** | ConfirmPopup 버튼 색상 적용 코드 추가 | `Presentation/UI/ConfirmPopup.cs` | 코드 수정 |
| **Step 4** | 기존 하드코딩 색상 → UIColorConfig 교체 | GameEndUI, GameHudUI, BuildingPlacementUI, ProductionPanelUI, BuildingPanelBase | 코드 수정 (5개 파일) |
| **Step 5** | GameEndPanel 크기 조정 | Inspector (Game.unity) | 레이아웃 |
| **Step 6** | GameHudUI 설정 버튼 크기/패딩 조정 | Inspector (Game.unity) | 레이아웃 |
| **Step 7** | BuildingPopup 패널/버튼 크기, 골드 아이콘 정렬 | Inspector (Game.unity) | 레이아웃 |
| **Step 8** | ProductionPopup 패널/버튼 크기 조정 | Inspector (Game.unity) | 레이아웃 |
| **Step 9** | BuildingActionPanel 패널 높이, X 버튼 위치 | Inspector (Game.unity) | 레이아웃 |
| **Step 10** | ConfirmPopup Inspector 연결 및 색상 확인 | Inspector (Game.unity) | Inspector 연결 |
| **Step 11** | RematchRequestPopup 크기 조정 | Inspector (Game.unity) | 레이아웃 |

---

## Step 1 — UIColorConfig.cs 생성

**파일:** `Assets/_Project/Scripts/Infrastructure/Config/UIColorConfig.cs`

```csharp
// UIColorConfig.cs
// 프로젝트 전체 UI에서 공통으로 사용하는 색상 설정.
// Resources/Config/UIColorConfig.asset 으로 생성하고 각 UI 컴포넌트에서 SerializeField로 참조한다.
// 색상을 변경하려면 이 에셋만 수정하면 된다 — 코드 수정 및 재컴파일 불필요.

[CreateAssetMenu(fileName = "UIColorConfig", menuName = "Hexiege/Config/UIColorConfig")]
public class UIColorConfig : ScriptableObject
{
    [Header("텍스트 — 기본/경고")]
    public Color normalTextColor  = Color.white;          // 기본 텍스트 색상 (리셋용)
    public Color goldInsufficientColor = Color.red;       // 골드 부족 시 비용 텍스트
    public Color populationFullColor   = Color.red;       // 인구 만원 시 HUD 텍스트

    [Header("게임 종료")]
    public Color winColor  = new Color(0.3f, 0.5f, 0.9f); // 승리 텍스트 (파랑)
    public Color loseColor = new Color(0.9f, 0.3f, 0.3f); // 패배 텍스트 (빨강)

    [Header("건물 철거")]
    public Color demolishRefundColor = Color.green;       // 철거 환불 텍스트

    [Header("확인 팝업 버튼")]
    public Color confirmButtonColor = Color.green;        // 확인(포기) 버튼 배경
    public Color cancelButtonColor  = Color.red;          // 취소 버튼 배경
}
```

---

## Step 2 — UIColorConfig.asset 생성

`Resources/Config/UIColorConfig.asset` 경로에 에셋을 생성합니다.

에디터 스크립트 메뉴 `Hexiege/Setup/UIColorConfig 생성`으로 자동 생성하거나,
Project 창에서 `Create > Hexiege/Config/UIColorConfig`로 수동 생성합니다.

---

## Step 3 — ConfirmPopup.cs 수정

**파일:** `Assets/_Project/Scripts/Presentation/UI/ConfirmPopup.cs`

추가할 필드:
```csharp
[Header("색상 설정")]
[Tooltip("UI 공통 색상 설정. Resources/Config/UIColorConfig.asset 연결.")]
[SerializeField] private UIColorConfig _colorConfig;

[Tooltip("확인 버튼 배경 Image. Awake에서 confirmButtonColor 적용.")]
[SerializeField] private Image _confirmButtonImage;

[Tooltip("취소 버튼 배경 Image. Awake에서 cancelButtonColor 적용.")]
[SerializeField] private Image _cancelButtonImage;
```

추가할 메서드:
```csharp
private void Awake()
{
    // 버튼 색상은 Show() 시마다 바뀌지 않으므로 초기화 시 1회 적용한다.
    if (_colorConfig == null) return;
    if (_confirmButtonImage != null) _confirmButtonImage.color = _colorConfig.confirmButtonColor;
    if (_cancelButtonImage  != null) _cancelButtonImage.color  = _colorConfig.cancelButtonColor;
}
```

---

## Step 4 — 기존 하드코딩 색상 교체 (5개 파일)

### GameEndUI.cs

수정 전:
```csharp
private static readonly Color WinColor  = new Color(0.3f, 0.5f, 0.9f);
private static readonly Color LoseColor = new Color(0.9f, 0.3f, 0.3f);
```

수정 후:
- `static readonly` 상수 제거
- `[SerializeField] private UIColorConfig _colorConfig` 필드 추가
- `_resultText.color = isWin ? _colorConfig.winColor : _colorConfig.loseColor`로 교체

### GameHudUI.cs

수정 전: `_populationText.color = isFull ? Color.red : Color.white`

수정 후:
- `[SerializeField] private UIColorConfig _colorConfig` 필드 추가
- `_populationText.color = isFull ? _colorConfig.populationFullColor : _colorConfig.normalTextColor`

### BuildingPlacementUI.cs

수정 전: `_buildingCostTexts[i].color = Color.red / Color.white`

수정 후:
- `[SerializeField] private UIColorConfig _colorConfig` 필드 추가
- `Color.red` → `_colorConfig.goldInsufficientColor`
- `Color.white` → `_colorConfig.normalTextColor`

### ProductionPanelUI.cs

수정 전: `_unitCostTexts[i].color = Color.red / Color.white`, `_upgradeCostText.color = Color.red / Color.white`

수정 후:
- `[SerializeField] private UIColorConfig _colorConfig` 필드 추가
- `Color.red` → `_colorConfig.goldInsufficientColor`
- `Color.white` → `_colorConfig.normalTextColor`

### BuildingPanelBase.cs

수정 전: `_demolishRefundText.color = Color.green`

수정 후:
- `[SerializeField] private UIColorConfig _colorConfig` 필드 추가
- `Color.green` → `_colorConfig.demolishRefundColor`

---

## Step 5~11 — UI 레이아웃 수정 (Inspector, 순차 진행)

각 UI를 수정한 뒤 사용자가 실기기에서 해당 TC를 테스트하고 결과를 확인한 후 다음 UI로 넘어갑니다.

### Step 5 — GameEndPanel (TC-SINGLE-SET-007, TC-SINGLE-END-001)

**문제:** 게임 종료 화면 내부 요소들이 고정 픽셀(sizeDelta)로 설정되어 기기에 따라 크기가 너무 작게 보임

**원인 분석 (Rule 2 위반 항목):**
| 오브젝트 | 현재 anchor | 현재 sizeDelta | 문제 |
|---------|-----------|--------------|------|
| ResultText | (0.5,0.5) point | (200, 50) | 고정 픽셀 |
| RestartButton | (0.5,0.5) point | (160, 30) | 고정 픽셀 |
| LobbyButton | (0.5,0.5) point | (160, 30) | 고정 픽셀 |
| CountdownText | (0,0)~(1,0.5) | (0, 0) | 이미 앵커 기반 ✓ |

> GameEndPanel 자체 (anchorMin=0,0 / anchorMax=1,1) 는 이미 전체 화면 비율 — 수정 불필요.

**수정 방향:** ResultText, RestartButton, LobbyButton을 anchor 비율 기반으로 전환 (sizeDelta → 0).
GameEndPanel 기준 비율이므로 모든 기기에서 동일 비율 유지.

**적용할 앵커 값 (시작점 — 실기기 테스트 후 세부 조정):**

| 오브젝트 | anchorMin (X, Y) | anchorMax (X, Y) | sizeDelta |
|---------|-----------------|-----------------|-----------|
| ResultText | (0.1, 0.62) | (0.9, 0.75) | (0, 0) |
| RestartButton | (0.15, 0.43) | (0.85, 0.55) | (0, 0) |
| LobbyButton | (0.15, 0.29) | (0.85, 0.41) | (0, 0) |
| CountdownText | (0, 0) | (1, 0.5) | 현행 유지 |

**검증:** 게임 종료 시 결과 화면이 적절한 크기로 표시되는지 확인 (TC-SINGLE-SET-007, TC-SINGLE-END-001)

---

### Step 6 — GameHudUI 설정 버튼 (TC-SINGLE-HUD-007)

**문제:** 설정(톱니바퀴) 버튼이 고정 40×40px로 설정되어 터치하기 어렵고 패딩 부족

**원인 분석 (Rule 2 위반):**
- 부모: GameHUD (anchorMin=0,1 / anchorMax=1,1 / sizeDelta.y=100px — 상단 HUD 바)
- SettingsButton 현재: anchorMin(1,1) point / anchoredPosition(0,-10) / sizeDelta(40,40) — 고정 픽셀

**수정 방향:** GameHUD 높이(100px) 대비 앵커 비율로 전환. 우측 상단에 여백 포함.

**적용할 앵커 값 (시작점 — 실기기 테스트 후 세부 조정):**

| 항목 | 값 |
|------|---|
| anchorMin | (0.87, 0.05) |
| anchorMax | (0.99, 0.95) |
| sizeDelta | (0, 0) |
| anchoredPosition | (0, 0) |

> GameHUD 자체가 고정 100px 높이(sizeDelta.y=100)이므로 SettingsButton 높이 = 100 × 0.9 = 90px.
> 정사각형이 필요한 경우 AspectRatioFitter(Mode: Height Controls Width, Ratio: 1.0)를 추가.

**검증:** 설정 버튼이 탭하기 적절한 크기로 표시되고 화면 경계와 적당한 여백이 있는지 확인 (TC-SINGLE-HUD-007)

---

### 패널 높이 공통 기준 (Step 7, 8, 9 공통 적용)

BuildingPopup, ProductionPopup, BuildingActionPanel 세 패널 모두 동일한 높이 기준을 적용합니다.

**원칙:**
- 세 패널은 모두 SafeAreaContainer의 자식. SafeAreaContainer는 SafeAreaFitter로 기기의 Safe Area에 자동 대응.
- 따라서 SafeAreaContainer 기준 앵커 비율 = Safe Area 높이 기준 비율 → 모든 기기에서 동일 비율 유지.
- GameSystemRules Rule 2 준수: sizeDelta(고정 픽셀) 사용 금지, 앵커 비율만 사용.

**적용할 앵커 값:**

| 패널 | anchorMin (X, Y) | anchorMax (X, Y) | 의미 |
|------|-----------------|-----------------|------|
| BuildingPopup | (X값 유지, **0.0**) | (X값 유지, **0.4**) | Safe Area 하단 기준 40% 높이 |
| ProductionPopup | (X값 유지, **0.0**) | (X값 유지, **0.4**) | 동일 |
| BuildingActionPanel | (X값 유지, **0.0**) | (X값 유지, **0.4**) | 동일 |

가로(X) 앵커는 현재 설정 유지. 세로(Y)만 교체.

---

### Step 7 — BuildingPopup (TC-SINGLE-BP-001, TC-SINGLE-BP-002)

**문제:**
- BP-001: 패널 높이 부족으로 버튼이 테두리 밖으로 나감, 버튼 크기 작음
- BP-002: 금화 아이콘(위)과 비용 숫자(아래)가 세로로 쌓인 구조에서 숫자가 좌우로 치우침

**수정 방향:**
- 패널 RectTransform: anchorMin.y = 0.0, anchorMax.y = 0.4 적용 (Safe Area 하단 40%)
- 내부 버튼 그룹 padding/spacing 조정하여 버튼이 패널 안에 수용되도록 수정
- 비용 숫자 텍스트 오브젝트의 Alignment를 Center로 설정하거나 anchoredPosition.x = 0으로 조정

**검증:** 건물 배치 팝업이 잘린 부분 없이 표시되고, 비용 숫자가 금화 아이콘 아래 중앙에 정렬되는지 확인 (TC-SINGLE-BP-001, TC-SINGLE-BP-002)

---

### Step 8 — ProductionPopup (TC-SINGLE-PRD-001)

**문제:** BP-001과 동일 — 패널 높이 부족, 버튼 크기 작음

**수정 방향:**
- 패널 RectTransform: anchorMin.y = 0.0, anchorMax.y = 0.4 적용 (BuildingPopup과 동일 기준)
- 내부 버튼 크기/비율 BuildingPopup과 일관성 있게 조정

**검증:** 유닛 생산 팝업이 잘린 부분 없이 표시되는지 확인 (TC-SINGLE-PRD-001)

---

### Step 9 — BuildingActionPanel (TC-SINGLE-BAP-001)

**문제:** 패널 높이 조정 필요, 닫기(X) 버튼 위치가 다른 패널의 닫기 버튼 위치와 다름

**수정 방향:**
- 패널 RectTransform: anchorMin.y = 0.0, anchorMax.y = 0.4 적용 (세 패널 공통 기준)
- X 버튼 anchoredPosition을 BuildingPopup/ProductionPopup 수정 후 X 버튼 위치와 동일하게 맞춤

**검증:** 비생산 건물(채굴소/타워) 클릭 시 팝업이 올바른 크기로 표시되고, X 버튼 위치가 다른 팝업과 동일한지 확인 (TC-SINGLE-BAP-001)

---

### Step 10 — ConfirmPopup Inspector 연결 (TC-SINGLE-SET-004)

**문제:** 포기 확인 팝업의 확인/취소 버튼에 색상이 없음

**수정 방향:**
- Step 3 코드 수정 완료 후, Inspector에서 ConfirmPopup 컴포넌트에 연결:
  - `_colorConfig` → UIColorConfig.asset
  - `_confirmButtonImage` → 확인 버튼의 Image 컴포넌트
  - `_cancelButtonImage` → 취소 버튼의 Image 컴포넌트

**검증:** 포기 버튼 탭 → 확인 팝업 등장 → 확인 버튼=초록, 취소 버튼=빨강으로 표시되는지 확인 (TC-SINGLE-SET-004)

---

### Step 11 — RematchRequestPopup (TC-MULTI-END-002)

**문제:** 재경기 요청/거절 팝업 패널이 고정 픽셀로 설정되어 기기에 따라 크기 부적절

**원인 분석 (Rule 2 위반):**
- RematchRequestPopup 자체: anchorMin(0,0) / anchorMax(1,1) — 전체 Canvas 기준 ✓
- Overlay: anchorMin(0,0) / anchorMax(1,1) — 이미 OK ✓

| 오브젝트 | 현재 anchor | 현재 sizeDelta | 문제 |
|---------|-----------|--------------|------|
| RequestPanel | (0.5,0.5) point | (480, 280) | 고정 픽셀 |
| DeclinedPanel | (0.5,0.5) point | (400, 220) | 고정 픽셀 |

> RequestPanel/DeclinedPanel 내부 자식들(TitleText, MessageText, ButtonArea)은 이미 anchor 비율 기반 ✓ — 수정 불필요.
> RematchRequestPopup은 Canvas 직속 자식(SafeAreaContainer 밖)이므로 앵커는 전체 Canvas 기준.

**수정 방향:** RequestPanel, DeclinedPanel을 Canvas 기준 anchor 비율로 전환. 화면 중앙에 위치.

**적용할 앵커 값 (시작점 — 실기기 테스트 후 세부 조정):**

| 오브젝트 | anchorMin (X, Y) | anchorMax (X, Y) | sizeDelta |
|---------|-----------------|-----------------|-----------|
| RequestPanel | (0.1, 0.3) | (0.9, 0.7) | (0, 0) |
| DeclinedPanel | (0.1, 0.3) | (0.9, 0.7) | (0, 0) |

의미: 캔버스 가로 80% × 세로 40% 크기로 화면 중앙에 배치. 모든 기기에서 동일 비율 유지.

**검증:** 멀티플레이 게임 종료 후 재경기 요청 버튼 탭 → 클라이언트 화면에 팝업이 적절한 크기로 표시되는지 확인 (TC-MULTI-END-002)

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| UIColorConfig 연결 누락 시 NullReferenceException | 각 UI의 Awake/Initialize에서 `if (_colorConfig == null) return` 가드 추가. 색상 미적용으로 기능에는 영향 없음 |
| ConfirmPopup Awake 추가 시 기존 동작 영향 | Awake에서 Image.color만 설정. 기존 Show()/Hide() 로직과 충돌 없음 |
| BuildingPopup/ProductionPopup 높이 변경 시 내부 레이아웃 틀어짐 | VerticalLayoutGroup이 있으므로 패널 높이만 늘리면 내부 요소 자동 재배치. 수정 후 각 요소 클리핑 여부 확인 필요 |
| Color.white를 normalTextColor로 교체 시 투명도 차이 | UIColorConfig.normalTextColor = Color.white(alpha=1)로 설정하면 동일. alpha 값 주의 |
| ProductionPanelUI의 `_queueSlotImages[slotIndex].color = new Color(1, 1, 1, 0)` | 이 코드는 슬롯 빈 상태 투명화용으로 UIColorConfig 범위 밖. 수정하지 않음 |

---

## Round 2 — 실기기 테스트 결과 및 추가 수정 계획

### Round 1 결과 요약 (2026-05-27)

| TC | 결과 | 비고 |
|----|------|------|
| TC-SINGLE-HUD-007 | 현행 유지 | 인게임 화면은 Safe Area 미적용 구조 — 설정 버튼 위치 현행 유지가 맞다고 판단 |
| TC-SINGLE-BP-001 | **FAIL** | 외부 패널 높이는 커졌으나 내부 자식 레이아웃 미적용 |
| TC-SINGLE-BP-002 | **FAIL** | 비용 숫자 텍스트 좌측 치우침 |
| TC-SINGLE-PRD-001 | **FAIL** | BP-001과 동일 |
| TC-SINGLE-BAP-001 | **FAIL** | BP-001과 동일 |
| TC-SINGLE-SET-004 | **PASS** ✓ | ConfirmPopup 버튼 색상 정상 적용 |
| TC-SINGLE-SET-007 | **FAIL** | 버튼 컨테이너만 커짐 — 내부 텍스트 크기 미적용 |
| TC-SINGLE-END-001 | **FAIL** | SET-007과 동일 |
| TC-MULTI-END-002 | **FAIL** | 패널만 커짐 — 내부 텍스트/레이아웃 비율 미적용 |

### 실패 공통 원인

외부 컨테이너의 RectTransform 앵커를 비율 기반으로 변환했으나, **내부 자식 요소들은 여전히 고정 픽셀 크기를 유지**하고 있어 부모가 커져도 내용물이 따라오지 않음.

- **패널 내 레이아웃 요소** (빌딩 버튼, 유닛 카드 등): GridLayoutGroup/VerticalLayoutGroup 셀 크기가 고정 픽셀 → 부모 크기 변화를 반영하지 못함
- **텍스트 요소** (승리/패배 결과 텍스트, 버튼 내부 텍스트 등): TMP 폰트 크기 고정 → 컨테이너가 커져도 텍스트 크기 그대로

---

### Step 12 — BuildingPopup / ProductionPopup / BuildingActionPanel 내부 자식 레이아웃 수정

**대상 TC:** TC-SINGLE-BP-001, TC-SINGLE-PRD-001, TC-SINGLE-BAP-001

**근거 규칙:** GameSystemRules Rule 2 (앵커 비율 기반 배치)

**문제:**
외부 컨테이너(BuildingPanel, ProductionPanel, ActionPanel)가 SafeArea 하단 40% 비율로 커졌지만,
내부 자식 오브젝트(빌딩 버튼 그룹, 유닛 카드 그리드, 비용 텍스트 등)는 고정 픽셀 크기를 유지하여
레이아웃이 깨짐.

**수정 방향 (game-programmer 에이전트 조사 후 결정):**
1. 씬에서 내부 패널의 Layout Group 구조 파악 (GridLayoutGroup / VerticalLayoutGroup / HorizontalLayoutGroup)
2. 자식 요소를 앵커 비율로 전환하거나, Layout Group의 셀 크기/spacing/padding을 비율 기반으로 전환
3. 에디터 스크립트에 내부 자식 RectTransform 수정 로직 추가

---

### Step 13 — GameEndPanel 텍스트/버튼 내부 텍스트 크기 수정

**대상 TC:** TC-SINGLE-SET-007, TC-SINGLE-END-001

**근거 규칙:** GameSystemRules Rule 2 (앵커 비율 기반 배치)

**문제:**
ResultText(승리!/패배!), RestartButton 내부 레이블 텍스트, LobbyButton 내부 레이블 텍스트,
CountdownText가 고정 폰트 크기로 설정되어 있어, 버튼 컨테이너 RectTransform이 커져도
텍스트는 원래 크기 그대로임.

**수정 방향:**
1. 위 텍스트 요소들에 **TextMeshPro Auto Size** 활성화
   - Min Font Size: 적절한 최소값 (예: 12)
   - Max Font Size: 적절한 최대값 (예: 72)
   - 컨테이너 크기에 맞게 자동으로 폰트 크기가 결정됨
2. 에디터 스크립트에 TMP Auto Size 활성화 로직 추가
   - `TextMeshProUGUI.enableAutoSizing = true`
   - `fontSizeMin`, `fontSizeMax` 설정

---

### Step 14 — RematchRequestPopup 내부 자식 레이아웃/텍스트 수정

**대상 TC:** TC-MULTI-END-002

**근거 규칙:** GameSystemRules Rule 2 (앵커 비율 기반 배치)

**문제:**
RequestPanel/DeclinedPanel이 Canvas 비율(80%×40%) 기반으로 커졌지만,
내부 TitleText, MessageText, 버튼 영역이 고정 픽셀 크기를 유지하여 비율이 맞지 않음.

**수정 방향 (game-programmer 에이전트 조사 후 결정):**
1. RequestPanel/DeclinedPanel 내부 자식 GO 계층 파악
2. 텍스트 요소: TMP Auto Size 활성화
3. 버튼 영역: 앵커 비율로 전환하거나 VerticalLayoutGroup flexible 설정
4. 에디터 스크립트에 해당 오브젝트 수정 로직 추가

---

### Step 15 — BuildingPopup 비용 숫자 좌측 치우침 수정 (TC-SINGLE-BP-002)

**대상 TC:** TC-SINGLE-BP-002

**문제:**
골드 아이콘과 비용 숫자가 세로로 쌓인 구조에서 숫자 텍스트가 좌측으로 치우쳐 있음.

**수정 방향 (game-programmer 에이전트 조사 후 결정):**
1. 비용 숫자 TextMeshProUGUI의 Horizontal Alignment를 Center로 설정
2. 또는 RectTransform anchoredPosition.x = 0 으로 정렬
3. 에디터 스크립트에 해당 GO 탐색 및 수정 로직 추가

---

### 에디터 스크립트 확장 방향

`FixUIColorAndLayout.cs`를 Round 2 수정 사항을 포함하도록 확장 또는
별도 `FixUILayoutRound2.cs` 메뉴 스크립트 생성.

Round 2 수정에는 씬 계층 탐색이 필요하므로 game-programmer 에이전트가 씬을 직접 확인한 뒤 구체적인 GO 경로/이름을 파악하여 구현.
