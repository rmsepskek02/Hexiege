# Research — 로그인 씬 팝업 닫기 버튼 무반응 수정

## 작업 목적

로그인 화면의 두 팝업(익명 경고 팝업, 네트워크 오류 팝업)의 닫기 버튼(CloseButton GO)이
클릭해도 아무 반응이 없는 문제를 수정한다.

---

## 씬 계층 구조 (Login.unity 직접 확인)

### AnonymousWarningPopup
```
AnonymousWarningPopup (Canvas, SO=200)
  └── Panel (AnimatedPanel)
       ├── CloseButton (Active ✅, 코드 연결 없음 ❌)   ← 문제
       ├── _warningText
       └── ButtonContainer
            ├── CreateAccountButton → _createAccountButton 연결 ✅
            └── ContinueAnonymousButton → _continueAnonymousButton 연결 ✅
```

### NetworkErrorPopup
```
NetworkErrorPopup (Canvas, SO=200)
  └── Panel (AnimatedPanel)
       ├── CloseButton (Active ✅, 코드 연결 없음 ❌)   ← 문제
       └── ButtonContainer
            ├── ConfirmButton → _confirmButton 연결 ✅ (정상 동작)
            └── CancelButton (Inactive ⚠️, 코드 연결 없음)
```

---

## 원인 분석

### 공통 원인
두 팝업 모두 `CloseButton` GameObject가 활성화(Active)되어 있지만,
각 C# 스크립트에 `_closeButton` SerializeField 필드가 없다.
필드가 없으면 Inspector에서 연결할 수 없고, 따라서 `onClick` 리스너도 등록되지 않아 완전 무반응이다.

### AnonymousWarningPopup.cs 현재 버튼 필드
- `_createAccountButton` → CreateAccountButton 연결 ✅
- `_continueAnonymousButton` → ContinueAnonymousButton 연결 ✅
- `_closeButton` 없음 ❌

### NetworkErrorPopup.cs 현재 버튼 필드
- `_confirmButton` → ConfirmButton 연결 ✅ (정상 동작 중)
- `_closeButton` 없음 ❌
- (CancelButton GO는 Inactive 상태이며 이 작업에서는 다루지 않음)

---

## CloseButton 역할

두 팝업 모두 CloseButton은 **팝업 닫기만** 수행한다.
- `Hide()` 호출 → AnimatedPanel 숨김 + BlockingOverlay 해제 (이미 `Hide()` 내부에서 처리)
- 추가 로직 없음

### AnonymousWarningPopup 특이사항
익명 로그인 진행 중(`OnContinueAnonymousClicked` 실행 중)에는 모든 버튼을 비활성화(`SetInteractable(false)`)한다.
CloseButton도 이 처리에 포함시켜야 진행 중 취소를 방지할 수 있다.
