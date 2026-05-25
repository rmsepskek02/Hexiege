# Plan — 전체 UI 규칙 준수 검증

코드 전수 감사 + 씬 YAML 파일 직접 분석을 통해 10개 공통 UI 규칙 전부를 검증 완료했다.
미준수 항목은 Rule 5(CanvasGroup 패턴) 한 가지이며, 해당 코드 수정이 유일한 남은 작업이다.

---

## 검증 완료된 규칙 (전체)

| 규칙 | 결과 | 근거 |
|------|------|------|
| Rule 1 (Canvas Scaler) | ✅ 준수 | 씬 YAML 직접 확인: 모든 Canvas 1080×1920 / 0 |
| Rule 2 (앵커 기반 레이아웃) | ✅ 준수 | 씬 YAML 직접 확인: 씬 내 모든 RectTransform 전수 추출 완료. 컨테이너 전부 stretch. 모달 팝업 내부 패널(Panel/RequestPanel/DeclinedPanel) 조건부 허용 |
| Rule 3 (Filled 자식 앵커) | ✅ 준수 | 씬 YAML 직접 확인: Filled 이미지 2개 모두 자식 없음. Fill 앵커 이미 비율 적용 |
| Rule 4 (SafeArea) | ✅ 준수 | 씬 YAML 직접 확인: SafeAreaContainer + SafeAreaFitter 모두 존재 |
| Rule 5 (CanvasGroup 패턴) | ❌ 위반 | 코드 전수 감사: Lobby MVVM Views 7곳 SetActive 직접 호출 |
| Rule 6 (기본 폰트) | ✅ 준수 | 씬 YAML 직접 확인: Light SDF + Bold SDF 모두 허용 폰트로 확인됨 (규칙 문서 수정 완료) |
| Rule 7 (골드 부족 색상) | ✅ 준수 | 코드 전수 감사 완료 |
| Rule 8 (팝업/모달 구분) | ✅ 준수 | 코드 전수 감사 완료 |
| Rule 9 (배경 탭 닫기) | ✅ 준수 | 코드 전수 감사 완료 |
| Rule 10 (LIFO 중첩) | ✅ 준수 | 코드 전수 감사 완료 |

---

## 남은 작업

### 작업 1. Rule 5 코드 수정 — Lobby MVVM Views SetActive 위반

game-programmer 에이전트에 위임한다.

| UI | 위반 코드 | 파일 경로 |
|----|----------|----------|
| LobbyRootView | `panel.SetActive(active)` | Views/Lobby/LobbyRootView.cs:134 |
| BattleMainView | `gameObject.SetActive(visible)` | Views/Lobby/Battle/BattleMainView.cs:65 |
| CustomGameView | `gameObject.SetActive(visible)` | Views/Lobby/Battle/CustomGameView.cs:60 |
| CustomHostView | `gameObject.SetActive(visible)` + `_errorText.gameObject.SetActive(...)` | Views/Lobby/Battle/CustomHostView.cs:70, 107 |
| CustomJoinView | `gameObject.SetActive(visible)` | Views/Lobby/Battle/CustomJoinView.cs:63 |
| RandomMatchView | `gameObject.SetActive(visible)` + `_cancelButton.gameObject.SetActive(...)` | Views/Lobby/Battle/RandomMatchView.cs:59, 79 |
| ProfileView | `SafeSetActive(_anonymousSection, ...)` | Views/Lobby/Profile/ProfileView.cs:152, 164, 177 |

**수정 방향**: 각 View에 CanvasGroup 컴포넌트를 추가하고, SetActive 호출을 `_canvasGroup.alpha / blocksRaycasts / interactable` 설정으로 교체한다.

**참고**: LoginRootView(Views/Login/LoginRootView.cs:183-187, 301-305)도 동일한 위반이 있으나 Login.unity 씬이 미구현 상태이므로 낮은 우선순위.

---

## 위험 요소

- CanvasGroup 전환 후 기존 SetActive 동작과 시각적 차이가 없어야 함 → 수정 후 Lobby 화면 전환 동작 확인 필요
- View에 CanvasGroup이 이미 존재하는 경우 중복 추가 주의
