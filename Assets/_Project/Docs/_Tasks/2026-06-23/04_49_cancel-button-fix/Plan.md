# Plan — 로그인 씬 팝업 닫기 버튼 무반응 수정

## 작업 목적

로그인 화면 두 팝업의 CloseButton GO가 활성화되어 있지만 코드에 필드가 없어 무반응인 문제를 수정한다.
두 파일에 `_closeButton` SerializeField를 추가하고, Inspector에서 CloseButton GO를 연결한다.

---

## 수정 항목

### 1. `AnonymousWarningPopup.cs` — _closeButton 필드 추가

**근거**: GameSystemRules_UI.md — 팝업 닫기는 `AnimatedPanel.Hide()` + `UIManager.HideBlockingOverlay()` 패턴 (이미 `Hide()` 내부에서 처리됨)

**변경 내용**:
1. `[Header("버튼")]` 아래에 `_closeButton` SerializeField 추가
2. `Initialize()`에 `_closeButton` 리스너 등록
3. `OnDestroy()`에 `_closeButton` 리스너 해제
4. `OnCloseButtonClicked()` 메서드 추가 → `Hide()` 호출
5. `SetInteractable()`에 `_closeButton` 포함 (진행 중 취소 방지)

---

### 2. `NetworkErrorPopup.cs` — _closeButton 필드 추가

**근거**: 동일. CloseButton GO가 씬에 존재하나 코드 필드 없음.

**변경 내용**:
1. `[Header("버튼")]` 아래에 `_closeButton` SerializeField 추가
2. `Initialize()`에 `_closeButton` 리스너 등록
3. `OnDestroy()`에 `_closeButton` 리스너 해제
4. `OnCloseButtonClicked()` 메서드 추가 → `Hide()` 호출

> 기존 `_confirmButton` → `ConfirmButton` 연결은 유지 (이미 정상 동작 중)

---

### 3. Inspector 연결 (코드 수정 후 수동 진행)

| 팝업 | 추가할 슬롯 | 연결할 GO |
|------|-----------|---------|
| AnonymousWarningPopup | `_closeButton` | CloseButton |
| NetworkErrorPopup | `_closeButton` | CloseButton |

---

## 위험 요소

- AnonymousWarningPopup의 `SetInteractable()`에 `_closeButton` 누락 시 로그인 진행 도중 닫기가 가능해짐
- 코드 수정 후 반드시 Inspector에서 CloseButton GO를 `_closeButton` 슬롯에 연결해야 동작함

---

## 변경 파일 목록

```
[수정]
- Assets/_Project/Scripts/Presentation/UI/Views/Login/AnonymousWarningPopup.cs
- Assets/_Project/Scripts/Presentation/UI/Views/Login/NetworkErrorPopup.cs
```
