# Plan: BlockingOverlay CanvasGroup 추가로 반투명 오버레이 버그 수정

## 작업 목적

"Tap to Start" 후 로그인 화면이 검정 반투명 이미지로 덮이는 버그를 수정한다.
원인은 `NetworkErrorPopup`의 `BlockingOverlay`에 CanvasGroup이 없어서 코드로 숨길 수 없는 상태이기 때문이다.

---

## 수정 대상

- **파일**: `Assets/_Project/Scenes/Login.unity`
- **오브젝트**: `SafeAreaContainer > NetworkErrorPopup > BlockingOverlay`
- **코드 수정 없음**: `ConfirmPopup.cs`는 변경하지 않는다

---

## 수정 내용

### 변경 항목 1: `BlockingOverlay`에 CanvasGroup 컴포넌트 추가

**근거**: GameSystemRules_UI.md **규칙 5** — UI 요소를 숨길 때 SetActive 대신 CanvasGroup을 사용한다. 배경 오버레이도 항상 active 상태를 유지하되 CanvasGroup으로 표시/숨김을 제어한다.

**작업 방법** (Unity 에디터에서 사용자 직접 수행):
1. Login.unity 씬 열기
2. Hierarchy에서 `SafeAreaContainer > NetworkErrorPopup > BlockingOverlay` 선택
3. Inspector에서 **Add Component → Canvas Group** 추가
4. 추가된 CanvasGroup 설정:
   - `Alpha`: **0**
   - `Interactable`: **false**
   - `Blocks Raycasts`: **false**
5. 씬 저장 (Ctrl+S)

### 변경 항목 2: `ConfirmPopup`의 `_blockingOverlay` 필드 재연결

현재 `ConfirmPopup._blockingOverlay`는 BlockingOverlay **GameObject**를 가리키고 있어 런타임에 null이 된다. CanvasGroup 추가 후에도 연결이 잘못되어 있다면 재연결이 필요하다.

**작업 방법** (Unity 에디터에서 사용자 직접 수행):
1. Hierarchy에서 `SafeAreaContainer > NetworkErrorPopup` 선택
2. Inspector에서 `ConfirmPopup` 컴포넌트 확인
3. `Blocking Overlay` 필드에 `BlockingOverlay` 오브젝트가 이미 연결되어 있다면, Unity가 자동으로 CanvasGroup 컴포넌트를 참조한다 (컴포넌트 추가 후 자동 해결될 가능성 높음)
4. 만약 필드가 비어있으면 `BlockingOverlay` 오브젝트를 드래그하여 다시 연결

---

## 예상 동작 변경

| 상황 | 수정 전 | 수정 후 |
|------|---------|---------|
| 씬 시작 시 | BlockingOverlay Image(a=0.6) 항상 표시 | CanvasGroup alpha=0이므로 보이지 않음 |
| Tap to Start 후 | 검정 반투명 오버레이 노출 | 오버레이 보이지 않음 |
| ConfirmPopup.Show() 호출 시 | CanvasGroup null → 오버레이 제어 불가 | alpha=1로 전환되어 정상 표시 |
| ConfirmPopup.Hide() 호출 시 | CanvasGroup null → 오버레이 영구 표시 | alpha=0으로 전환되어 정상 숨김 |

---

## 위험 요소

- 없음. CanvasGroup 추가는 기존 Image 렌더링에 영향을 주지 않으며, 초기 alpha=0 설정만으로 문제가 해결된다.
- `ConfirmPopup.Show()`가 호출될 때 alpha=1로 전환하므로 팝업 정상 동작도 보장된다.
