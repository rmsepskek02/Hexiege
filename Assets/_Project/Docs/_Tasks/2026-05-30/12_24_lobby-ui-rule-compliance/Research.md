# Research — Lobby 씬 UI 공통 규칙 준수 현황

## 작업 목적 및 배경

Lobby 씬의 모든 UI 요소를 `GameSystemRules.md`의 공통 UI 규칙(규칙 1~5)에 맞게 점검하여,
현재 어떤 요소가 규칙을 위반하고 있는지 전체 목록을 파악한다.

이 문서는 Lobby.unity 씬 파일(약 12,500줄)을 전수 분석한 결과이며,
이후 Plan.md에서 수정 방법을 결정하기 위한 근거 자료로 사용된다.

---

## 씬 구조 개요

Lobby.unity에는 Canvas가 3개 존재한다.

| Canvas 이름 | SortingOrder | 역할 |
|-------------|-------------|------|
| `[UI] Canvas` | 0 | 로비 메인 UI |
| `LoadingScreen` | 100 | 씬 전환 로딩 오버레이 |
| `Toast` | 100 | 토스트 메시지 (DontDestroyOnLoad) |

---

## 규칙별 현황

### 규칙 1. Canvas Scaler

| Canvas | 준수 여부 | 비고 |
|--------|----------|------|
| `[UI] Canvas` | ✅ | Scale With Screen Size / 1080×1920 / Match=0 |
| `LoadingScreen` | ✅ | 동일 |
| `Toast` | ❌ | **CanvasScaler 컴포넌트 없음** → Constant Pixel Size 모드로 동작. 기기마다 Toast 크기가 달라짐 |

---

### 규칙 2. 앵커 기반 배치

**위반 목록 (총 23건)**

#### [UI] Canvas 계층

| # | 오브젝트 | 위치 | 위반 내용 |
|---|----------|------|-----------|
| 1 | `TabBar` | LobbyRoot 직속 자식 | `anchorMin.y == anchorMax.y == 0` + `SizeDelta.y = 140` 고정 픽셀 높이 |
| 2 | `ContentArea` | LobbyRoot 직속 자식 | 스트레치 앵커이지만 `AnchoredPosition.y = 70`, `SizeDelta.y = -140` 고정 픽셀 오프셋 (TabBar 140px에 종속) |
| 3 | `SingleplayBtn` | BattleMainPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 4 | `CustomBtn` | BattleMainPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 5 | `RandomBtn` | BattleMainPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 6 | `CreateRoomBtn` | CustomGamePanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 7 | `JoinByCodeBtn` | CustomGamePanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 8 | `BackBtn` | CustomGamePanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 9 | `CodeText` | CustomHostPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 50` |
| 10 | `PlayersText` | CustomHostPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 50` |
| 11 | `StatusText` | CustomHostPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 50` |
| 12 | `ErrorText` | CustomHostPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 50` |
| 13 | `CancelBtn` | CustomHostPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 14 | `CodeInput` | CustomJoinPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 15 | `JoinBtn` | CustomJoinPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 16 | `BackBtn` | CustomJoinPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |
| 17 | `StatusText` | RandomMatchPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 50` |
| 18 | `CancelBtn` | RandomMatchPanel (VLG, ChildControlHeight:0) | 포인트 앵커(0,0)/(0,0) + `SizeDelta.y = 100` |

> **VLG = VerticalLayoutGroup** — `ChildControlHeight: 0`으로 설정되어 자식의 높이를 제어하지 않음.
> 결과적으로 각 자식 오브젝트의 `SizeDelta.y` 고정 픽셀값이 실제 높이로 사용됨.

#### Toast Canvas 계층

| # | 오브젝트 | 위치 | 위반 내용 |
|---|----------|------|-----------|
| 19 | `Background` | Toast Canvas 직속 자식 | 포인트 앵커(0.5,0.5)/(0.5,0.5) + `SizeDelta = (700, 80)` 고정 픽셀 |
| 20 | `Message` | Toast > Background 자식 | 스트레치 앵커(0,0)/(1,1)이지만 `SizeDelta = (-24, -24)` 고정 픽셀 패딩 |

#### LoadingScreen Canvas 계층

| # | 오브젝트 | 위치 | 위반 내용 |
|---|----------|------|-----------|
| 21 | `Spinner` | LoadingScreen > SafeAreaContainer | 포인트 앵커(0.5,0.5)/(0.5,0.5) + `SizeDelta = (120, 120)` + `AnchoredPosition.y = 80` |
| 22 | `StatusText` | LoadingScreen > SafeAreaContainer | 포인트 앵커(0.5,0.5)/(0.5,0.5) + `SizeDelta = (700, 80)` + `AnchoredPosition.y = -60` |
| 23 | `Text Area` | CodeInput > TMP_InputField 내부 | 스트레치 앵커(0,0)/(1,1)이지만 `SizeDelta = (-40, -10)` 고정 픽셀 패딩 |

---

### 규칙 3. Filled/Simple 이미지 자식 앵커

✅ 전체 준수

---

### 규칙 4. SafeAreaContainer / 배경 RaycastTarget

✅ 전체 준수

- `[UI] Canvas > LobbyBackground`: RaycastTarget = 0, SafeAreaContainer + SafeAreaFitter 존재
- `LoadingScreen Canvas`: SafeAreaContainer + SafeAreaFitter 존재
- `Toast Canvas`: SafeAreaFitter 존재
- `LoadingScreen > Background`의 RaycastTarget = 1은 LoadingScreen.cs의 CanvasGroup이 전체 표시/숨김을 제어하므로 동작 문제 없음 — 위반 아님

---

### 규칙 5. CanvasGroup 초기값

| 오브젝트 | 현재 값 | 규칙상 올바른 값 | 비고 |
|----------|---------|----------------|------|
| `Toast CanvasGroup` | alpha=0, blocksRaycasts=**1**, interactable=**1** | alpha=0, blocksRaycasts=**0**, interactable=**0** | ToastUI.Awake()의 ClearAll()이 런타임에 즉시 올바르게 초기화하므로 동작 문제는 없음. 단, Inspector 초기값이 규칙 5와 불일치하여 수정 대상 |

---

## 준수 항목 요약

- 규칙 1: [UI] Canvas, LoadingScreen Canvas ✅
- 규칙 2: LobbyRoot, BattlePanel/ShopPanel/ProfilePanel/RankingPanel, CustomGamePanel/BattleMainPanel/CustomHostPanel/CustomJoinPanel/RandomMatchPanel 컨테이너 자체, TabBar 자식 버튼(HLG 제어), RaceSelectionView ✅
- 규칙 3: 전체 ✅
- 규칙 4: 전체 ✅
- 규칙 5: 그 외 CanvasGroup 없음 ✅

---

## 위반 건수 최종 집계

| 규칙 | 위반 건수 |
|------|----------|
| 규칙 1 (Canvas Scaler) | 1건 (Toast Canvas) |
| 규칙 2 (앵커 기반 배치) | 23건 |
| 규칙 3 | 0건 |
| 규칙 4 | 0건 |
| 규칙 5 (CanvasGroup 초기값) | 1건 (Toast CanvasGroup) |
| **합계** | **25건** |
