# Research: 로비 프로필 탭 로그아웃 버튼 추가

## 작업 목적 및 내용

로비 씬의 프로필 탭에 로그아웃 버튼을 추가하는 작업입니다.
Firebase 익명 로그인 성공 이후, 사용자가 로비에서 직접 로그아웃을 할 수 있는 UI가 필요합니다.
현재 로그아웃 로직은 코드에 이미 모두 구현되어 있고, Unity Inspector에서 버튼 UI를 연결하는 작업만 남아 있는 상태입니다.

---

## 현재 상태 파악

### 로그아웃 관련 코드 — 이미 완전히 구현되어 있음

| 파일 | 구현 내용 |
|------|-----------|
| `FirebaseAuthService.cs` (Infrastructure) | `SignOutAsync()` — Firebase `_auth.SignOut()` 호출 후 Task 반환 |
| `LoginUseCase.cs` (Application) | `SignOutAsync()` — UGS SignOut + Firebase SignOut 순서로 세션 정리 |
| `ProfileView.cs` (Presentation) | `_logoutButton` 필드 + `OnLogoutClicked()` 핸들러 완전 구현 |

### ProfileView 로그아웃 흐름 (이미 구현된 코드)

```
OnLogoutClicked()
  → SetInteractable(false)  // 버튼 비활성화 (중복 클릭 방지)
  → await _loginUseCase.SignOutAsync()  // Firebase + UGS 세션 정리
  → SceneManager.LoadScene("Login")    // Login 씬으로 이동
  (예외 발생 시 SetStatus("로그아웃 중 오류") + SetInteractable(true))
```

### ProfileView Inspector 필드 현황

- `_logoutButton` (Button 타입) — Inspector에서 연결 필요 (현재 비어 있음)
- `_loginSceneName` = "Login" (기본값 설정됨)
- `_accountInfoText`, `_statusText`, `_anonymousSection` 등 다른 필드도 Inspector 연결 필요

### ProfileView.Start()에서 버튼 리스너 등록 방식

```csharp
if (_logoutButton != null) _logoutButton.onClick.AddListener(OnLogoutClicked);
```

→ `_logoutButton`이 null이면 리스너가 등록되지 않음 → 버튼 연결이 핵심

---

## 영향 범위

- **수정 대상 파일**: 없음 (코드 변경 불필요)
- **Inspector 작업 대상**: `Lobby.unity` 씬의 ProfilePanel 내 UI 구성

### 작업이 필요한 이유

`ProfileView.cs`에 이미 `_logoutButton` SerializeField가 선언되어 있고,
`OnLogoutClicked()` 핸들러도 완전히 구현되어 있습니다.
단지 Unity 씬의 ProfilePanel 안에 **로그아웃 버튼 UI GameObject를 생성하고**
**ProfileView 컴포넌트의 `_logoutButton` 필드에 연결**하면 작동합니다.

---

## 관련 파일 목록

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs` | 로그아웃 버튼 Inspector 필드 및 핸들러 보유 |
| `Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs` | SignOutAsync() 구현체 |
| `Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs` | Firebase SignOut 구현체 |
| `Assets/Scenes/Lobby.unity` | ProfilePanel 씬 파일 (Inspector 작업 대상) |
