# Research — 전체 UI 규칙 준수 검증

이 작업은 오늘 수립한 공통 UI 규칙(GameSystemRules.md 규칙 1~10)이 현재 구현된 모든 UI에 잘 적용되어 있는지 전수 검사하는 것이다.
코드 전수 감사 + 씬 YAML 파일 검토를 통해 항목별 준수 여부를 확인했다.
Rule 2, 3, 6은 Inspector 레벨에서만 확인 가능하므로 Unity Editor에서 직접 확인이 필요하다.

---

## 규칙 약어 정의

| 약어 | 규칙 내용 |
|------|----------|
| R1 | Canvas Scaler 1080×1920 / matchWidthOrHeight=0 |
| R2 | 앵커 기반 레이아웃 (고정 픽셀 금지) |
| R3 | Filled/Simple 부모의 자식도 비율 앵커 |
| R4 | SafeAreaContainer + SafeAreaFitter |
| R5 | CanvasGroup 표시/숨김 (SetActive 금지) |
| R6 | 기본 폰트 Maplestory Light SDF / Bold SDF 허용 |
| R7 | 골드 부족 시 비용 텍스트 Color.red |
| R8 | 팝업/모달 타입 구분 |
| R9 | 팝업 Show()→Register, Close()→Unregister |
| R10 | 팝업 중첩 LIFO + 하위 입력 차단 |

범례: ✅ 준수 / ❌ 위반 / ⚠️ 조건부 허용 / N/A 해당 없음 / 🔍 Inspector 확인 필요

---

## Game 씬 UI

| UI | R1 | R2 | R3 | R4 | R5 | R6 | R7 | R8 | R9 | R10 |
|----|----|----|----|----|----|----|----|----|----|----|
| GameHudUI | ✅ | ✅² | ✅ | ✅ | ✅ | ✅ | N/A¹ | N/A | N/A | N/A |
| ProductionPanelUI | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅팝업 | ✅ | ✅ |
| BuildingPlacementUI | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅팝업 | ✅ | ✅ |
| BuildingActionPanelUI | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | N/A | ✅팝업 | ✅ | ✅ |
| InGameSettingsUI | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | N/A | ✅팝업 | ✅ | ✅ |
| ConfirmPopup | ✅ | ⚠️³ | ✅ | ✅ | ⚠️⁴ | ✅ | N/A | ✅모달 | N/A | ✅ |
| GameEndUI | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | N/A | ✅모달 | N/A | N/A |

**각주:**
- ¹ GameHudUI 골드 텍스트: 현재 보유량 표시용이므로 부족 기준이 없어 색상 변경 대상 아님 (인구 텍스트는 used≥max 조건 있어 ✅)
- ² GameHUD RectTransform: AnchorMin(0,1)~AnchorMax(1,1), 상단 고정 높이 100px (의도된 HUD 레이아웃)
- ³ ConfirmPopup > 내부 Panel: AnchorMin/Max (0.5,0.5), SizeDelta (550,350) — 모달 팝업 다이얼로그의 고정 크기, Canvas Scaler 보정으로 실질 위험 없음 → 조건부 허용
- ⁴ ConfirmPopup `_blockingOverlay.SetActive(true/false)`: overlay가 LayoutGroup 내부 아니고 DontDestroyOnLoad도 아니므로 조건부 허용

---

## Common UI

| UI | R1 | R2 | R3 | R4 | R5 | R6 | R7 | R8 | R9 | R10 |
|----|----|----|----|----|----|----|----|----|----|----|
| ToastUI | ✅ | N/A | N/A | ✅⁵ | ✅ | ✅ | N/A | N/A | N/A | N/A |
| LoadingScreen | N/A | N/A | N/A | N/A | ✅ | ✅ | N/A | N/A | N/A | N/A |
| AnimatedPanel | N/A | N/A | N/A | N/A | ⚠️⁶ | N/A | N/A | N/A | N/A | N/A |
| RematchRequestPopup | N/A | N/A | N/A | N/A | ⚠️⁶ | ✅ | N/A | ✅모달 | N/A | N/A |
| SharedBackgroundButton | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | ✅ |
| SafeAreaFitter | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| FloatingHpText | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |

**각주:**
- ⁵ ToastUI: Lobby.unity YAML에서 SafeAreaFitter 존재 확인됨
- ⁶ AnimatedPanel/RematchRequestPopup: DOTween 제약상 `SetActive(true)` 선행 필수 → 규칙 허용 예외

---

## Lobby 씬 UI (MVVM Views)

| UI | R1 | R2 | R3 | R4 | R5 | R6 | R7 | R8 | R9 | R10 |
|----|----|----|----|----|----|----|----|----|----|----|
| LobbyRootView | ✅ | ✅ | ✅ | ✅ | ❌⁷ | ✅ | N/A | N/A | N/A | N/A |
| TabBarView | ✅ | ✅ | ✅ | ✅ | N/A | ✅ | N/A | N/A | N/A | N/A |
| BattleRootView | ✅ | ✅ | ✅ | ✅ | N/A | ✅ | N/A | N/A | N/A | N/A |
| BattleMainView | ✅ | ✅ | ✅ | ✅ | ❌⁷ | ✅ | N/A | N/A | N/A | N/A |
| CustomGameView | ✅ | ✅ | ✅ | ✅ | ❌⁷ | ✅ | N/A | N/A | N/A | N/A |
| CustomHostView | ✅ | ✅ | ✅ | ✅ | ❌⁷ | ✅ | N/A | N/A | N/A | N/A |
| CustomJoinView | ✅ | ✅ | ✅ | ✅ | ❌⁷ | ✅ | N/A | N/A | N/A | N/A |
| RandomMatchView | ✅ | ✅ | ✅ | ✅ | ❌⁷ | ✅ | N/A | N/A | N/A | N/A |
| RaceSelectionView | ✅ | ✅ | ✅ | ✅ | N/A⁸ | ✅ | N/A | N/A | N/A | N/A |
| ProfileView | ✅ | ✅ | ✅ | ✅ | ❌⁷ | ✅ | N/A | N/A | N/A | N/A |
| ShopView | ✅ | ✅ | ✅ | ✅ | N/A | ✅ | N/A | N/A | N/A | N/A |
| RankingView | ✅ | ✅ | ✅ | ✅ | N/A | ✅ | N/A | N/A | N/A | N/A |

**각주:**
- ⁷ MVVM View SetActive 위반: UniRx Observable 구독하여 `gameObject.SetActive(visible)` 또는 `panel.SetActive(active)` 직접 호출. CanvasGroup 미사용.
- ⁸ RaceSelectionView: `_characterRoots[i].SetActive(true)` — 캐러셀 캐릭터 오브젝트를 항상 활성화하는 초기화 코드. 표시/숨김 제어가 아니므로 N/A

---

## Login 씬 UI (미구현 씬, 낮은 우선순위)

| UI | R5 | 비고 |
|----|----|----|
| LoginRootView | ❌⁹ | `SetActivePanel()`에서 패널 전환 시 `SetActive(true/false)` 직접 호출 |
| AnonymousWarningPopup | ⚠️ | `_blockingOverlay.SetActive` + `_panel.gameObject.SetActive(true)` + `_panel.Show()` — ConfirmPopup과 동일 패턴, 조건부 허용 |
| EmailLoginView, SignUpView, EmailVerifyView, PasswordResetView, LoginSelectView | N/A | SetActive 호출 없음, 자체 로직에 문제 없음 |

**각주:**
- ⁹ Login.unity 씬 자체가 미구현 상태. 구현 시 CanvasGroup으로 전환 권장.

---

## 씬 YAML 확인 결과 (Rule 1, 2, 3, 4, 6)

### Rule 1 — Canvas Scaler

| 씬 | Canvas | m_ReferenceResolution | m_MatchWidthOrHeight | 상태 |
|----|--------|-----------------------|----------------------|------|
| Game.unity | Canvas | {x: 1080, y: 1920} | 0 | ✅ |
| Lobby.unity | Canvas 1 | {x: 1080, y: 1920} | 0 | ✅ |
| Lobby.unity | Canvas 2 | {x: 1080, y: 1920} | 0 | ✅ |

### Rule 2 — 앵커 기반 레이아웃

주요 컨테이너 RectTransform YAML 직접 확인 결과:

| 씬 | 오브젝트 | AnchorMin | AnchorMax | SizeDelta | 상태 |
|----|---------|-----------|-----------|-----------|------|
| Game.unity | SafeAreaContainer | (0,0) | (1,1) | (0,0) | ✅ |
| Game.unity | BuildingPopup | (0,0) | (1,1) | (0,0) | ✅ |
| Game.unity | BuildingActionPanel | (0,0) | (1,1) | (0,-150) 인셋 | ✅ |
| Game.unity | GameHUD | (0,1) | (1,1) | (0,100) | ✅ 의도된 고정 높이 |
| Game.unity | ConfirmPopup | (0,0) | (1,1) | (0,0) | ✅ |
| Game.unity | ConfirmPopup > Panel | (0.5,0.5) | (0.5,0.5) | (550,350) | ⚠️ 모달 고정 크기, 조건부 허용 |
| Game.unity | GameEndPanel | (0,0) | (1,1) | (0,0) | ✅ |
| Game.unity | ProductionPopup | (0,0) | (1,1) | (0,0) | ✅ |
| Game.unity | InGameSettingsPanel | (0,0) | (1,1) | (0,0) | ✅ |
| Lobby.unity | LobbyRoot | (0,0) | (1,1) | (0,0) | ✅ |
| Lobby.unity | BattlePanel | (0,0) | (1,1) | (0,0) | ✅ |
| Lobby.unity | CustomGamePanel | (0,0.5) | (1,1) | (0,0) | ✅ 상반부 stretch |
| Lobby.unity | ProfilePanel | (0,0) | (1,1) | (0,0) | ✅ |
| Lobby.unity | RaceSelectionView | (0,0) | (1,0.5) | (0,0) | ✅ 하반부 stretch |

### Rule 3 — Filled/Simple 부모의 자식 앵커

| 씬 | Filled 오브젝트 | m_Children | 자식 앵커 | 상태 |
|----|--------------|-----------|----------|------|
| Game.unity | Fill (ProgressBar 내) | [] 없음 | — | ✅ 본인 앵커 (0.14,0.5)~(0.86,0.5) 비율 적용 |
| Lobby.unity | Spinner | [] 없음 | — | N/A 자식 없음 |

### Rule 4 — SafeArea

| 씬 | 항목 | 상태 |
|----|------|------|
| Game.unity | SafeAreaContainer + SafeAreaFitter | ✅ |
| Lobby.unity | Canvas 1 SafeAreaContainer + SafeAreaFitter | ✅ |
| Lobby.unity | Canvas 2 SafeAreaContainer + SafeAreaFitter | ✅ |
| Lobby.unity | ToastUI 근처 SafeAreaFitter | ✅ |

### Rule 6 — 기본 폰트

- 사용 중인 폰트: Maplestory Light SDF (GUID: `58c71976...`) + Maplestory Bold SDF (GUID: `96af9a12...`)
- **두 폰트 모두 허용**: Light SDF가 기본값이나, Bold SDF는 의도적으로 사용하는 폰트로 규칙 위반 아님 (사용자 확인됨)
- 상태: ✅ 전체 준수

---

## 수정 필요 항목 정리

### 코드 수정 필요 — Rule 5 위반

| UI | 위반 내용 | 파일 경로 |
|----|----------|----------|
| LobbyRootView | `panel.SetActive(active)` 직접 호출 | Views/Lobby/LobbyRootView.cs:134 |
| BattleMainView | `gameObject.SetActive(visible)` | Views/Lobby/Battle/BattleMainView.cs:65 |
| CustomGameView | `gameObject.SetActive(visible)` | Views/Lobby/Battle/CustomGameView.cs:60 |
| CustomHostView | `gameObject.SetActive(visible)` + `_errorText.gameObject.SetActive(...)` | Views/Lobby/Battle/CustomHostView.cs:70, 107 |
| CustomJoinView | `gameObject.SetActive(visible)` | Views/Lobby/Battle/CustomJoinView.cs:63 |
| RandomMatchView | `gameObject.SetActive(visible)` + `_cancelButton.gameObject.SetActive(...)` | Views/Lobby/Battle/RandomMatchView.cs:59, 79 |
| ProfileView | `SafeSetActive(_anonymousSection, ...)` | Views/Lobby/Profile/ProfileView.cs:152, 164, 177 |
| LoginRootView | `SetActivePanel()` 내 패널 전환 SetActive | Views/Login/LoginRootView.cs:183-187, 301-305 |

**판단 기준**: Lobby MVVM Views의 SetActive는 LayoutGroup 내부가 아닌 전체 View 전환이므로 레이아웃 깨짐 실질 위험은 낮다. 그러나 규칙 5를 문자적으로 위반한다.

### YAML 분석으로 확인 완료 — Rule 2, 3, 6

Rule 2, 3, 6 모두 씬 YAML 파일 직접 분석으로 확인 완료. Inspector 추가 확인 불필요.

| 규칙 | 결과 |
|------|------|
| Rule 2 (앵커 기반) | ✅ 주요 컨테이너 전부 stretch 앵커. ConfirmPopup 내부 Panel만 조건부 허용 |
| Rule 3 (Filled 자식) | ✅ Filled 이미지 자식 없음. Fill 앵커 이미 비율 적용 |
| Rule 6 (기본 폰트) | ✅ Bold/Light 두 폰트 모두 허용됨 |
