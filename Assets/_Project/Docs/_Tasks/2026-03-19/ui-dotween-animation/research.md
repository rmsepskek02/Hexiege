# Research: UI DOTween 애니메이션 프레임워크

**날짜:** 2026-03-19

---

## 1. 현재 UI 애니메이션 현황

모든 기존 UI가 SetActive(true/false) 즉시 전환 방식 사용 — 애니메이션 없음.

### 팝업/패널 현황

| 파일 | 패널 참조 | 현재 Show/Hide 방식 |
|------|-----------|-------------------|
| `GameEndUI.cs` | `_panel` (GameObject) | `_panel.SetActive(true/false)` |
| `ProductionPanelUI.cs` | `_popup` (GameObject) | `_popup.SetActive(true/false)` |
| `BuildingPlacementUI.cs` | `_popup` (GameObject) | `_popup.SetActive(true/false)` |
| `RematchRequestPopup.cs` | `_overlay`, `_requestPanel`, `_declinedPanel` | 각각 `SetActive()` |

### 인게임 HUD 현황

| 파일 | 요소 | 현재 방식 |
|------|------|---------|
| `GameHudUI.cs` | `_goldText` (TMP) | `text = gold.ToString()` 즉시 대입 |
| `GameHudUI.cs` | `_populationText` (TMP) | `text = $"{used}/{max}"` 즉시 대입 |
| `ProductionPanelUI.cs` | `_progressFill` (Image) | `fillAmount = state.Progress` 즉시 대입 |

### 로비 탭 현황

| 파일 | 요소 | 현재 방식 |
|------|------|---------|
| `TabBarView.cs` | 탭 버튼 선택 강조 | `button.colors = colors` 즉시 대입 |
| `BattleRootView.cs` | 서브뷰 전환 | 서브뷰 내부 SetActive (추정) |

---

## 2. DOTween 기존 사용 현황

이미 Presentation 레이어에서 DOTween 사용 중:
- `UnitView.cs`: `DOKill()`, `DORotate()`, `Ease.OutQuad`
- `LoadingScreen.cs`: `CanvasGroup.DOFade()`
- `CameraController.cs`: `DOTween.To()`, `Ease.OutCubic`

→ **DOTween 의존성 이미 확립, 추가 설치 불필요**

---

## 3. AnimatedPanel 설계 분석

### 기존 구조 문제
각 UI 스크립트(GameEndUI 등)가 `_popup`(GameObject)를 직접 SetActive로 제어.
→ AnimatedPanel을 **base class**로 사용하면 기존 상속/구조를 대거 수정 필요.

### 채택 방식: 컴포넌트 방식
`AnimatedPanel`을 `_popup`/`_panel` GameObject에 직접 부착하는 별도 컴포넌트로 설계.

```
기존: _popup.SetActive(true)
개선: _animatedPanel.Show()  ← AnimatedPanel 컴포넌트 참조
```

**장점**:
- 기존 스크립트(GameEndUI 등) 구조 변경 최소화
- `_popup`에 CanvasGroup + AnimatedPanel 추가만 하면 적용
- 각 UI는 `GameObject _popup` 대신 `AnimatedPanel _popup`만 참조 변경

---

## 4. CanvasGroup 요구사항

DOFade 사용을 위해 각 팝업 패널 GameObject에 `CanvasGroup` 컴포넌트 추가 필요:
- `GameEndPanel` → CanvasGroup + AnimatedPanel 추가
- `ProductionPopup` → CanvasGroup + AnimatedPanel 추가
- `BuildingPopup` → CanvasGroup + AnimatedPanel 추가
- `RematchRequestPopup._overlay` → CanvasGroup 기존 여부 확인 필요

---

## 5. 토스트 알림 신규 구현 필요 항목

현재 미구현 항목 (RPC 구조만 존재):
- `NetworkBuildingController.BuildFailedClientRpc` — UI 피드백 없음
- `NetworkProductionController.EnqueueFailedClientRpc` — UI 피드백 없음

→ 토스트 알림 시스템으로 처리 예정:
- `ToastNotification.cs` — 개별 알림 프리팹
- `ToastManager.cs` — 싱글턴, 큐 관리, 화면 상단/하단 위치

---

## 6. 영향 범위 전체

### 신규 파일
| 파일 | 역할 |
|------|------|
| `Presentation/UI/Common/UIAnimator.cs` | 공통 애니메이션 패턴 static 헬퍼 |
| `Presentation/UI/Common/AnimatedPanel.cs` | 팝업/패널용 MonoBehaviour 컴포넌트 |
| `Presentation/UI/Common/ToastNotification.cs` | 개별 토스트 알림 |
| `Presentation/UI/Common/ToastManager.cs` | 토스트 큐 싱글턴 |

### 수정 파일
| 파일 | 수정 내용 |
|------|---------|
| `GameEndUI.cs` | `GameObject _panel` → `AnimatedPanel _panel` |
| `ProductionPanelUI.cs` | `GameObject _popup` → `AnimatedPanel _popup` |
| `BuildingPlacementUI.cs` | `GameObject _popup` → `AnimatedPanel _popup` |
| `RematchRequestPopup.cs` | ShowRequest/ShowDeclined/Hide에 DOTween 적용 |
| `GameHudUI.cs` | 골드/인구 DOCounter + flash 효과 |
| `TabBarView.cs` | 탭 전환 DOColor 보간 |
