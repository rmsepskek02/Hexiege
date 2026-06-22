# Plan: 로비 프로필 탭 로그아웃 버튼 추가

## 작업 목적 및 내용

로비 씬의 프로필 탭에 로그아웃 버튼 UI를 추가합니다.
Firebase 익명 로그인 성공 후, 사용자가 직접 로그아웃할 수 있는 버튼을 프로필 탭에 배치합니다.
C# 코드는 이미 완전히 구현되어 있으며, Unity Inspector에서 버튼 UI를 생성하고 연결하는 작업만 진행합니다.

---

## 현재 상태 요약

- `ProfileView.cs`에 `_logoutButton` 필드와 `OnLogoutClicked()` 핸들러가 이미 구현되어 있음
- `LoginUseCase.SignOutAsync()` — Firebase + UGS 세션 정리 로직 완성
- 단, Lobby.unity 씬의 ProfilePanel에 로그아웃 버튼 UI GameObject가 없고, Inspector 연결도 비어 있음

---

## 구현 계획

### 작업 방식: Editor 1회성 스크립트

Lobby.unity 씬에서 수동으로 하기 어렵거나 빠른 UI 생성이 필요하므로,
Editor 메뉴에서 실행 가능한 1회성 에디터 스크립트를 작성합니다.

> **대안**: 직접 Inspector에서 수동 생성도 가능합니다. 에디터 스크립트가 불필요하면 생략합니다.
> 단, 이 경우 아래 UI 규칙을 직접 적용해야 합니다.

---

## 적용 UI 규칙 (GameSystemRules_UI.md 근거)

| 규칙 | 내용 | 적용 방법 |
|------|------|-----------|
| **규칙 5** (CanvasGroup 숨김/표시 패턴) | SetActive(false) 대신 CanvasGroup 사용 | ProfileView는 이미 CanvasGroup 기반으로 구현되어 있음 — 버튼은 별도 CanvasGroup 불필요 |
| **규칙 2** (앵커 기반 배치 원칙) | 고정 픽셀 크기 대신 앵커 비율 기반 배치 | 버튼 RectTransform 앵커를 비율로 설정 |
| **규칙 6** (기본 폰트) | Maplestory Light SDF / Bold SDF 사용 | 버튼 텍스트에 Maplestory Bold SDF 적용 (버튼 강조 표현) |

---

## 구현 상세

### 1. ProfilePanel에 로그아웃 버튼 UI 생성 (Inspector 작업)

**배치 위치**: Lobby.unity 씬 > ProfilePanel 내부

**버튼 구성**:
- GameObject 이름: `LogoutButton`
- 컴포넌트: `Button` + `Image` (버튼 배경) + `TextMeshProUGUI` (버튼 라벨)
- 버튼 라벨 텍스트: `"로그아웃"`
- 폰트: Maplestory Bold SDF (규칙 6 — 버튼 강조)
- 앵커: 비율 기반 (규칙 2)

### 2. ProfileView Inspector 필드 연결

`ProfileView` 컴포넌트의 아래 필드에 생성한 버튼을 연결:
- `_logoutButton` → `LogoutButton` 오브젝트의 Button 컴포넌트

### 3. 동작 흐름 (코드 변경 없음)

```
사용자가 로그아웃 버튼 탭
  → ProfileView.OnLogoutClicked()
  → SetInteractable(false)  // 중복 클릭 방지
  → await _loginUseCase.SignOutAsync()
      → UGS AuthenticationService.SignOut()
      → FirebaseAuthService.SignOut()
  → SceneManager.LoadScene("Login")  // Login 씬으로 이동
```

---

## 수정/생성 파일 목록

| 구분 | 파일 | 내용 |
|------|------|------|
| Inspector 작업 | `Assets/Scenes/Lobby.unity` | ProfilePanel에 LogoutButton UI 추가 + ProfileView._logoutButton 연결 |
| 코드 변경 없음 | — | — |

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| ProfileView 초기화 타이밍 | `_logoutButton`은 `Start()`에서 리스너 등록 — 연결이 없으면 동작 안 함 | Inspector에서 반드시 연결 확인 |
| 익명 로그아웃 복구 불가 | 익명 계정 로그아웃 시 동일 계정 복구 불가 | `ProfileView` 주석에 이미 명시. 이 작업에서는 별도 경고 팝업 미추가 (임시 버튼이므로) |
| `_loginSceneName` 기본값 | "Login" 으로 설정되어 있어 씬 이름과 일치해야 함 | 씬 이름 "Login" 확인 필요 |
