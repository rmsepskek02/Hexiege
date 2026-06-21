# Plan: BlockingOverlay CanvasGroup 추가로 반투명 오버레이 버그 수정

## 작업 목적

"Tap to Start" 후 로그인 화면이 검정 반투명 이미지로 덮이는 버그를 수정한다.
원인은 `NetworkErrorPopup`과 `AnonymousWarningPopup` 두 팝업의 `BlockingOverlay`에 CanvasGroup이 없어서 코드로 숨길 수 없는 상태이기 때문이다.

---

## 수정 대상

- **파일**: `Assets/_Project/Scenes/Login.unity`
- **오브젝트 1**: `SafeAreaContainer > NetworkErrorPopup > BlockingOverlay`
- **오브젝트 2**: `SafeAreaContainer > AnonymousWarningPopup > BlockingOverlay`
- **코드 수정 없음**: `ConfirmPopup.cs`, `AnonymousWarningPopup.cs`는 변경하지 않는다

---

## 수정 내용

### 변경 항목: 두 팝업의 `BlockingOverlay`에 CanvasGroup 컴포넌트 추가

**근거**: GameSystemRules_UI.md **규칙 5** — UI 요소를 숨길 때 SetActive 대신 CanvasGroup을 사용한다. 배경 오버레이도 항상 active 상태를 유지하되 CanvasGroup으로 표시/숨김을 제어한다.

**에디터 스크립트**: `Assets/Editor/Setup/AddBlockingOverlayCanvasGroup.cs`
**메뉴**: `Hexiege/Setup/BlockingOverlay CanvasGroup 추가`

스크립트가 자동으로 수행하는 작업 (두 팝업 모두):
1. `BlockingOverlay`에 CanvasGroup 추가 (없는 경우)
2. CanvasGroup 초기값 설정: `alpha=0`, `interactable=false`, `blocksRaycasts=false`
3. `_blockingOverlay` 필드에 CanvasGroup 재연결 (null인 경우)
4. 씬 저장

---

## 예상 동작 변경

| 상황 | 수정 전 | 수정 후 |
|------|---------|---------|
| 씬 시작 시 | BlockingOverlay Image(a=0.6) 두 곳 모두 항상 표시 | CanvasGroup alpha=0이므로 보이지 않음 |
| Tap to Start 후 | 검정 반투명 오버레이 노출 | 오버레이 보이지 않음 |
| 팝업.Show() 호출 시 | CanvasGroup null → 오버레이 제어 불가 | alpha=1로 전환되어 정상 표시 |
| 팝업.Hide() 호출 시 | CanvasGroup null → 오버레이 영구 표시 | alpha=0으로 전환되어 정상 숨김 |

---

## 위험 요소

- 없음. CanvasGroup 추가는 기존 Image 렌더링에 영향을 주지 않으며, 초기 alpha=0 설정만으로 문제가 해결된다.
- 각 팝업의 `Show()`가 호출될 때 alpha=1로 전환하므로 팝업 정상 동작도 보장된다.
