# Research: 로그인 시스템 구현 (Firebase Auth)

로그인 시스템을 새로 만드는 작업입니다. 현재 Hexiege는 로그인 화면 없이 바로 로비로 시작하며, 멀티플레이를 위한 UGS 익명 로그인만 자동으로 처리합니다. 이번 작업에서는 Firebase Authentication을 기반으로 한 별도의 로그인 씬(Login.unity)을 구축합니다. 익명, Google, 이메일 로그인을 모두 지원하며, 게임 테스트에 지장을 주지 않도록 Build Index로 분리하여 독립 운영합니다.

---

## 현재 인증 처리 현황

### 핵심 파일: UnityServicesInitializer.cs
**경로**: `Assets/_Project/Scripts/Infrastructure/Network/UnityServicesInitializer.cs`

현재 UGS를 초기화하고 익명 로그인을 수행하는 단일 파일.

```
InitializeAsync()
  → UnityServices.InitializeAsync()
  → AuthenticationService.Instance.SignInAnonymouslyAsync()
  → UGS PlayerId 발급 완료
```

**변경 대상**: `SignInAnonymouslyAsync()` → `SignInWithCustomIdAsync(firebaseUID)`로 교체  
**변경 이유**: Firebase UID를 UGS Custom ID로 전달하여 동일 사용자의 UGS PlayerId를 일관되게 유지

---

### 영향 없는 파일들

| 파일 | 이유 |
|------|------|
| `LobbyManager.cs` | `AuthenticationService.Instance.PlayerId`만 참조. UGS PlayerId가 동일하게 발급되면 변경 불필요 |
| `RelayManager.cs` | UGS 인프라 사용, PlayerId에 의존하지 않음 |
| `NetworkGameManager.cs` | `_networkGameManager.InitializeAsync()` 호출 구조 유지 |
| `LobbyUI.cs` | 씬 이름 기반 전환 (`"Lobby"`) — Build Index 변경이 코드에 영향 없음 |

---

## 신규 설치 필요 SDK

| SDK | 버전 | 역할 | 설치 상태 |
|-----|------|------|---------|
| Firebase Unity SDK (FirebaseAuth) | v13.11.0 | Firebase 인증 시스템 전체 | ✅ 설치 완료 |
| Google Play Games Plugin for Unity | v2.1.0 | Google 로그인 시 serverAuthCode 발급 | ✅ 설치 완료 (컴파일만, 런타임 미설정) |

> 주의: GPGS v2에서는 idToken 대신 `RequestServerSideAccess()` → serverAuthCode 방식을 사용.
> GPGS v1은 2026년 5월부로 deprecated.

→ 상세 설치 방법: `ThirdPartySetup.md` 참조

---

## 신규 씬

| 씬 | 경로 | 역할 |
|----|------|------|
| Login.unity | `Assets/_Project/Scenes/Login.unity` | 로그인 전용 씬 (신규) |

---

## 신규 생성 파일 목록

### Infrastructure 레이어
| 파일 | 경로 | 역할 |
|------|------|------|
| `FirebaseAuthService.cs` | `Infrastructure/Auth/` | Firebase SDK 래퍼. 로그인·로그아웃·세션확인·계정연동 API 제공 |

### Application 레이어
| 파일 | 경로 | 역할 |
|------|------|------|
| `LoginUseCase.cs` | `Application/UseCases/` | 로그인 흐름 조율 (Firebase 로그인 → UGS 브릿지 포함) |
| `AccountLinkUseCase.cs` | `Application/UseCases/` | 익명 계정 → 실계정 연동 흐름 |

### Bootstrap 레이어
| 파일 | 경로 | 역할 |
|------|------|------|
| `LoginBootstrapper.cs` | `Bootstrap/` | Login 씬 전용 Composition Root |

### Presentation 레이어 — Login 씬 신규
| 파일 | 경로 | 역할 |
|------|------|------|
| `LoginRootView.cs` | `Presentation/UI/Views/Login/` | 화면 전환 조율, 패널 스택 관리 |
| `LoginSelectView.cs` | `Presentation/UI/Views/Login/` | 로그인 방식 선택 화면 |
| `EmailLoginView.cs` | `Presentation/UI/Views/Login/` | 이메일 로그인 화면 |
| `SignUpView.cs` | `Presentation/UI/Views/Login/` | 이메일 회원가입 화면 |
| `EmailVerifyView.cs` | `Presentation/UI/Views/Login/` | 이메일 인증 대기 화면 |
| `PasswordResetView.cs` | `Presentation/UI/Views/Login/` | 비밀번호 재설정 화면 |
| `AnonymousWarningPopup.cs` | `Presentation/UI/Views/Login/` | 익명 로그인 경고 팝업 |

### Presentation 레이어 — Lobby 씬 수정
| 파일 | 경로 | 역할 |
|------|------|------|
| `ProfileView.cs` | `Presentation/UI/Views/Lobby/Profile/` | 계정 연동 + 로그아웃 UI (현재 빈 파일 → 내용 추가) |

---

## 수정 파일 목록

| 파일 | 변경 내용 | 변경 범위 | 구현 상태 |
|------|---------|---------|---------|
| `UnityServicesInitializer.cs` | `SignInAnonymouslyAsync()` 완전 제거 계획 → **항상 재로그인 방식으로 변경**. `IsSignedIn` 체크 제거, 매 초기화 시 `SignOut()` → `SignInAnonymouslyAsync()` 수행하여 서버로부터 유효한 토큰 보장 | 극소 (조건문 수정) | ✅ 완료 |
| `ProfileView.cs` | 빈 파일 → 계정 연동/로그아웃 UI 구현 | 신규 작성 수준 | ✅ 완료 |

> 변경 이유: 설치된 UGS SDK가 `SignInWithCustomIdAsync`를 지원하지 않아 Firebase UID → UGS Custom ID 브릿지 구현 불가.
>
> 추가 수정 (2026-05-24): `IsSignedIn=true`(기기 캐시)이지만 서버 토큰이 만료된 상태에서 재로그인을 건너뛰면 UGS Lobby/Relay API 호출 시 HTTP 401 Unauthorized 에러 발생. `IsSignedIn` 체크를 제거하고 항상 재로그인하도록 수정하여 커스텀 게임 + 랜덤 매칭 모두 정상 동작 확인.

### ⚠️ 추후 재검토 필요 — UnityServicesInitializer.cs

현재 `InitializeAsync()`는 항상 `SignOut()` → `SignInAnonymouslyAsync()`를 수행한다.

**문제**: Login 씬이 완성되어 Firebase UID → UGS 브릿지(`SignInWithCustomIdAsync`)가 정상 구현되면, `LoginUseCase.BridgeToUGSAsync()`가 Firebase UID 기반으로 UGS 로그인을 완료한 뒤 Lobby 씬으로 이동한다. 그런데 Lobby 씬 진입 시 `InitializeAsync()`가 그 세션을 SignOut()으로 끊고 익명 로그인으로 덮어쓰므로, Firebase UID 기반 UGS 세션이 유지되지 않는다.

**재검토 시점**: UGS SDK가 `SignInWithCustomIdAsync`를 지원하게 되어 Firebase UID → UGS 브릿지를 실제로 구현할 때

**재검토 방향**:
- Login 씬을 경유한 경우(Firebase UID 기반 로그인 완료)에는 `InitializeAsync()`에서 재로그인을 건너뛰는 조건 복원
- 단, 토큰 만료 문제(401)는 반드시 다시 고려해야 함 — `IsSignedIn=true`이지만 토큰이 만료된 케이스를 어떻게 처리할지 설계 필요

---

## 화면 전환 흐름

```
앱 시작 → Login.unity 로드
  ↓
Firebase 세션 확인 (LoginBootstrapper)
  ├─ 세션 유효 → UGS 브릿지 → Lobby 씬 이동 (자동 로그인)
  └─ 세션 없음 → 로그인 선택 화면
       ├─ 익명으로 시작하기 → 경고 팝업
       │    ├─ [계정 만들기] → 이메일 회원가입 화면
       │    └─ [계속 익명으로 진행] → UGS 브릿지 → Lobby 씬
       ├─ Google로 로그인 → GPGS idToken → Firebase → UGS 브릿지 → Lobby 씬
       └─ 이메일로 로그인 / 회원가입
            ├─ 이메일 로그인 화면
            │    ├─ 로그인 성공 → UGS 브릿지 → Lobby 씬
            │    ├─ 회원가입 → 회원가입 화면 → 인증 대기 → Lobby 씬
            │    └─ 비밀번호 찾기 → 비밀번호 재설정 화면
            └─ 비밀번호 재설정 화면 → 안내 후 이메일 로그인 화면 복귀
```

---

## 개발/테스트 분리 전략

씬 이름 기반 이동을 사용하므로 Build Index 변경이 기존 코드에 영향 없음.

| 목적 | Login | Lobby | Game |
|------|-------|-------|------|
| 로그인 기능 테스트 | 0 | 1 | 2 |
| 게임 기능 테스트 | 1 | 0 | 2 |

---

## 아키텍처 제약

- `FirebaseAuthService`는 Infrastructure 레이어 — Firebase SDK 의존
- UseCase들은 Application 레이어 — Firebase SDK에 직접 의존하지 않음 (FirebaseAuthService를 통해 접근)
- View들은 Presentation 레이어 — UseCase에 의존
- `LoginBootstrapper`는 Bootstrap 레이어 — 모든 레이어 접근 가능
- `GameBootstrapper`는 Login 씬에서 존재하지 않음 — Login 씬과 Game/Lobby 씬의 Bootstrap은 완전히 분리
