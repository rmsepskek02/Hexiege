# Auth System Rules

로그인 및 계정 관리 시스템의 구현 규칙 모음.
아이디어나 기획 의도가 아닌, 실제 코드로 구현할 때 기준이 되는 구체적인 규칙을 기록한다.

---

## 목차

- [기술 구성](#기술-구성)
- [씬 구성 및 Build Index](#씬-구성-및-build-index)
- [화면 흐름](#화면-흐름)
- [자동 로그인](#자동-로그인)
- [익명 로그인](#익명-로그인)
- [Google 로그인](#google-로그인)
- [이메일 로그인](#이메일-로그인)
- [이메일 회원가입](#이메일-회원가입)
- [이메일 인증](#이메일-인증)
- [비밀번호 재설정](#비밀번호-재설정)
- [계정 연동](#계정-연동)
- [로그아웃](#로그아웃)
- [세션 및 다기기 동작](#세션-및-다기기-동작)
- [Android 뒤로가기](#android-뒤로가기)
- [오류 처리](#오류-처리)
- [UGS 연결](#ugs-연결)
- [개발 및 테스트 분리](#개발-및-테스트-분리)

---

## 기술 구성

### 사용 SDK

| SDK | 역할 |
|-----|------|
| Firebase Authentication SDK | 로그인 시스템 전체 (계정 생성, 인증, 세션 관리) |
| Google Play Games Plugin for Unity | Google 로그인 시 Google idToken 발급 |

### 레이어별 구성

| 레이어 | 구성 요소 |
|--------|-----------|
| Infrastructure | FirebaseAuthService (신규), UnityServicesInitializer (수정) |
| Application | LoginUseCase, AccountLinkUseCase (신규) |
| Presentation | 로그인 선택 View, 이메일 로그인 View, 회원가입 View, 비밀번호 재설정 View, 계정 연동 View |
| Bootstrap | LoginBootstrapper (신규, Login 씬 전용) |

### Firebase ↔ UGS 연결 구조

Firebase 로그인 후, Firebase가 발급한 **ID Token(JWT)** 을 UGS OIDC Provider(`oidc-firebase`)에 전달하여 UGS PlayerId를 발급받는다(OIDC Bridge).
동일 Firebase 계정이면 기기를 바꿔도 항상 같은 UGS PlayerId가 반환되므로, 랭킹·재화 등 계정 귀속 데이터를 안전하게 저장할 수 있다.
UGS Lobby / Relay 는 UGS PlayerId만 필요로 하므로 기존 멀티플레이 코드(LobbyManager, RelayManager 등)는 변경하지 않는다.

**실계정(Google / 이메일)** — OIDC Bridge:

```
Firebase 로그인 → Firebase ID Token(JWT) 발급 (TokenAsync)
                       ↓
UGS SignInWithOpenIdConnectAsync("oidc-firebase", idToken) → UGS PlayerId 발급 (계정 1:1 연결)
                                                                  ↓
                                                   UGS Lobby / Relay 사용 가능
```

**익명 계정(Anonymous)** — 기존 익명 로그인 유지:

익명 Firebase 계정은 기기 종속이라 다기기 일관성이 애초에 불필요하고, OIDC Provider가 익명 토큰을 식별 주체로 인정하지 않으므로 UGS도 익명 로그인을 사용한다.

```
익명 Firebase 로그인 → (ID Token 미사용)
                       ↓
UGS SignInAnonymouslyAsync() → UGS PlayerId 발급 (기기 종속)
                                    ↓
                     UGS Lobby / Relay 사용 가능
```

> OIDC Provider 이름은 UGS 규약상 `oidc-` 접두사로 시작해야 하며, UGS Dashboard에 `oidc-firebase` Provider를 등록해 두어야 한다.
> 구현 위치: `LoginUseCase.BridgeToUGSAsync()`. Firebase ID Token은 Infrastructure의 `FirebaseAuthService.GetIdTokenAsync(false)`를 통해서만 발급한다(Application 레이어가 Firebase SDK에 직접 의존하지 않도록 캡슐화).

---

## 씬 구성 및 Build Index

**규칙 1. 씬 목록**

| 씬 | 역할 |
|----|------|
| Login.unity | 로그인 및 계정 관련 화면 전체 |
| Lobby.unity | 기존 로비 화면 |
| Game.unity | 기존 게임 화면 |

**규칙 2. Build Index 운영 방식**

로그인 시스템 테스트 시와 게임 기능 테스트 시 Build Index 순서를 수동으로 교체하여 사용한다.
씬 간 이동은 모두 씬 이름 기준으로 동작하므로 Build Index 변경이 멀티플레이에 영향을 주지 않는다.

| 테스트 목적 | Login | Lobby | Game |
|------------|-------|-------|------|
| 로그인 기능 테스트 | 0 | 1 | 2 |
| 게임 기능 테스트 | 1 | 0 | 2 |

---

## 화면 흐름

**규칙 1. 화면 구성**

Login 씬 내부에서 아래 화면들을 패널 전환 방식으로 구성한다. 별도 씬으로 분리하지 않는다.

| 화면 | 표시 조건 |
|------|-----------|
| 로그인 선택 화면 | 앱 시작 시 Firebase 세션이 없는 경우 |
| 이메일 로그인 화면 | 로그인 선택 화면에서 "이메일로 로그인" 선택 시 |
| 이메일 회원가입 화면 | 이메일 로그인 화면에서 "회원가입" 선택 시 |
| 비밀번호 재설정 화면 | 이메일 로그인 화면에서 "비밀번호 찾기" 선택 시 |

**규칙 2. 로그인 완료 후 씬 전환**

로그인 방식에 관계없이, 로그인 성공 시 Lobby 씬으로 전환한다.

**규칙 3. 로그인 선택 화면 구성**

로그인 선택 화면에서 사용자에게 제공하는 선택지는 다음과 같다.

- 익명으로 시작하기
- Google로 로그인
- 이메일로 로그인 / 회원가입

---

## 자동 로그인

**규칙 1. 자동 로그인 조건**

앱 시작 시 Firebase 세션(Refresh Token)이 유효하게 남아있으면 로그인 화면을 표시하지 않고 Lobby 씬으로 바로 이동한다.

**규칙 2. 자동 로그인 실패 처리**

| 실패 원인 | 처리 방식 |
|-----------|-----------|
| Firebase 세션 만료 (장기 미사용 등) | 별도 안내 없이 로그인 선택 화면으로 이동 |
| 네트워크 오류 | 네트워크 오류 팝업 표시 후 재시도 유도 |

**규칙 3. 자동 로그인 진행 중 표시**

자동 로그인 시도 중에는 로딩 인디케이터를 표시한다.

---

## 익명 로그인

**규칙 1. 경고 팝업 표시**

익명으로 시작하기 버튼을 누르면 즉시 로그인하지 않고 경고 팝업을 먼저 표시한다.

**규칙 2. 경고 팝업 내용**

기기를 변경하거나 앱을 재설치하면 게임 데이터를 잃을 수 있음을 안내한다.
팝업에서 제공하는 선택지는 다음과 같다.

- [계정 만들기]: 이메일 회원가입 화면으로 이동
- [계속 익명으로 진행]: 익명 로그인 후 Lobby 씬으로 이동

**규칙 3. 익명 계정 특성**

익명 계정은 기기에 종속된다. 앱 재설치 또는 기기 변경 시 동일 계정으로 복구할 수 없다.

---

## Google 로그인

**규칙 1. 로그인 흐름**

Google Play Games Plugin → Google idToken 발급 → Firebase 인증 → Firebase UID 발급 → UGS PlayerId 발급 → Lobby 씬 이동

**규칙 2. Google Play Games 미설치 처리**

기기에 Google Play Games 앱이 없거나 계정이 설정되지 않은 경우 오류 메시지를 표시한다.

---

## 이메일 로그인

**규칙 1. 이메일 인증 완료 조건**

이메일 인증이 완료된 계정만 로그인을 허용한다.
이메일 인증이 완료되지 않은 계정으로 로그인 시도 시 인증 안내 메시지를 표시하고 로그인을 차단한다.

**규칙 2. 비밀번호 오류 처리**

비밀번호가 틀린 경우 안내 메시지를 표시한다. 계정 존재 여부는 노출하지 않는다.
(보안: "이메일 또는 비밀번호가 올바르지 않습니다" 형태로 통합 표시)

**규칙 3. 존재하지 않는 이메일 처리**

가입되지 않은 이메일로 로그인 시도 시 규칙 2와 동일한 통합 메시지를 표시한다.

---

## 이메일 회원가입

**규칙 1. 입력 항목**

이메일 주소와 비밀번호를 입력받는다. 비밀번호 확인 입력란을 별도로 제공한다.

**규칙 2. 비밀번호 규칙**

비밀번호 최소 조건은 추후 정식 서비스 시점에 결정한다. 현재는 Firebase 기본 조건(최소 6자)을 따른다.

**규칙 3. 이미 사용 중인 이메일 처리**

이미 가입된 이메일로 회원가입 시도 시 "이미 사용 중인 이메일입니다" 안내 메시지를 표시한다.

**규칙 4. 회원가입 완료 후 처리**

회원가입 완료 즉시 이메일 인증 메일을 발송한다.
이메일 인증 화면으로 이동하여 인증 완료를 유도한다. (이메일 인증 규칙 참조)

---

## 이메일 인증

**규칙 1. 인증 필수 여부**

이메일/비밀번호 계정은 이메일 인증이 필수다.
인증 전까지 로그인이 차단된다.

**규칙 2. 인증 메일 발송 시점**

회원가입 완료 직후 인증 메일을 자동 발송한다.

**규칙 3. 인증 대기 화면**

인증 메일 발송 후 인증 대기 화면을 표시한다.
대기 화면에서 제공하는 기능은 다음과 같다.

- 인증 완료 확인 버튼: Firebase에서 인증 상태를 재확인하고 완료 시 Lobby 씬으로 이동
- 인증 메일 재발송 버튼: 메일을 받지 못한 경우 재발송

**규칙 4. 인증 완료 확인 방식**

"인증 완료 확인" 버튼을 누르면 Firebase에서 현재 계정의 이메일 인증 상태를 갱신하여 확인한다.
인증이 완료된 경우 Lobby 씬으로 이동한다. 미완료 시 안내 메시지를 표시한다.

---

## 비밀번호 재설정

**규칙 1. 재설정 흐름**

비밀번호 재설정 화면에서 이메일을 입력하면 Firebase가 해당 이메일로 재설정 링크를 발송한다.
발송 완료 후 "이메일을 확인하세요" 안내 메시지를 표시하고 이메일 로그인 화면으로 이동한다.

**규칙 2. 가입되지 않은 이메일 처리**

가입되지 않은 이메일로 재설정 요청 시 보안상 동일한 "이메일을 확인하세요" 메시지를 표시한다.
실제 계정 존재 여부를 노출하지 않는다.

---

## 계정 연동

**규칙 1. 연동 UI 위치**

계정 연동 UI는 로비 씬의 Profile 탭에 위치한다.

**규칙 2. 연동 버튼 노출 조건**

익명 계정으로 로그인한 사용자에게만 연동 버튼을 표시한다.
이미 실계정(Google 또는 이메일)으로 로그인한 경우 연동 버튼을 표시하지 않는다.

**규칙 3. 제공 연동 방식**

- Google로 연동
- 이메일로 연동 (이메일 회원가입 화면으로 이동)

**규칙 4. 연동 충돌 처리**

선택한 Google 계정 또는 이메일이 이미 다른 Firebase 계정에 연결되어 있는 경우
"이미 다른 계정에 연동된 정보입니다" 알림 팝업을 표시하고 연동을 중단한다.
기존 익명 계정 상태를 유지한다.

**규칙 5. 연동 완료 처리**

연동 완료 시 Profile 탭에 연동된 계정 정보를 표시한다.
연동 버튼은 숨긴다.

---

## 로그아웃

**규칙 1. 로그아웃 버튼 위치**

로그아웃 버튼은 로비 씬 Profile 탭에 위치한다.

**규칙 2. 로그아웃 처리**

로그아웃 버튼을 누르면 Firebase 세션을 종료하고 Login 씬으로 이동한다.

**규칙 3. 익명 계정 로그아웃 주의**

익명 계정으로 로그아웃하면 해당 익명 계정으로 다시 로그인할 수 없다.
로그아웃 전 연동 권유 안내를 표시하는 것을 권장하나, 강제하지는 않는다.

---

## 세션 및 다기기 동작

**규칙 1. 세션 유효 기간**

Firebase Refresh Token은 약 30일 비활성 시 만료된다.
Access Token(약 1시간 만료)은 SDK가 자동 갱신하므로 사용자가 인지하지 못한다.

**규칙 2. 다기기 동시 로그인**

동시 다기기 로그인을 명시적으로 지원하지 않는다.
단, 기기B에서 로그인하더라도 기기A의 세션을 강제 종료하지 않는다.
각 기기의 세션은 독립적으로 자연 만료될 때까지 유효하다.

**규칙 3. 비밀번호 변경 시 세션 처리**

비밀번호 변경 시 다른 기기의 세션이 강제 종료될 수 있다.
강제 종료된 기기는 자동 로그인 실패로 처리되어 로그인 선택 화면으로 이동한다.

---

## Android 뒤로가기

**규칙 1. 로그인 선택 화면에서 뒤로가기**

로그인 선택 화면은 앱 최초 진입점이므로 뒤로가기 시 앱 종료 확인 팝업을 표시한다.

**규칙 2. 앱 종료 확인 팝업 조건**

뒤로가기를 연달아 2회 입력한 경우 "앱을 종료하시겠습니까?" 팝업을 표시한다.
1회 입력 후 일정 시간이 지나면 카운트를 초기화한다.

**규칙 3. 하위 화면에서 뒤로가기**

이메일 로그인, 회원가입, 비밀번호 재설정 화면에서 뒤로가기를 누르면 이전 화면으로 돌아간다.

---

## 오류 처리

**규칙 1. 네트워크 오류**

Firebase 인증 중 네트워크 오류가 발생한 경우 "네트워크 설정을 확인하고 다시 시도하세요" 팝업을 표시한다.
팝업 확인 후 현재 화면을 유지한다.

**규칙 2. Firebase 서비스 오류**

Firebase 서버 오류 발생 시 "일시적인 오류가 발생했습니다. 잠시 후 다시 시도하세요" 메시지를 표시한다.

**규칙 3. UGS 연결 오류**

Firebase 로그인 성공 후 UGS PlayerId 발급(OIDC Bridge 또는 익명 로그인)에 실패한 경우 멀티플레이 기능이 동작하지 않을 수 있음을 안내한다.
로그인 자체는 성공으로 처리하고 Lobby 씬으로 이동한다.
(`LoginUseCase.BridgeToUGSAsync()` 는 UGS 브릿지 예외를 잡아 경고 로그만 남기고, 로그인 결과를 실패로 바꾸지 않는다.)

---

## UGS 연결

**규칙 1. UGS 초기화 시점**

Firebase 로그인 완료 후 UGS 초기화 및 UGS 로그인(OIDC Bridge 또는 익명)을 수행한다.
브릿지 진입점은 `LoginUseCase.BridgeToUGSAsync()` 한 곳으로 통일한다.

**규칙 2. UGS 로그인 방식 (실계정 — OIDC Token)**

실계정(Google / 이메일)은 Firebase ID Token(JWT)을 UGS OIDC Provider에 전달하여 PlayerId를 1:1로 연결한다.

```csharp
string firebaseToken = await firebaseAuthService.GetIdTokenAsync(false); // 내부적으로 FirebaseUser.TokenAsync(false)
await AuthenticationService.Instance.SignInWithOpenIdConnectAsync("oidc-firebase", firebaseToken);
```

- Provider 이름은 `oidc-firebase` (UGS 규약상 `oidc-` 접두사 필수, UGS Dashboard에 사전 등록 필요).
- 토큰은 캐시 우선(`forceRefresh: false`)으로 발급하며, 만료 임박 시 Firebase SDK가 자동 갱신한다.
- Firebase ID Token 발급은 Infrastructure의 `FirebaseAuthService.GetIdTokenAsync()`로만 수행한다(Application 레이어가 Firebase SDK에 직접 의존하지 않도록 캡슐화).

**규칙 3. UGS 로그인 방식 (익명 계정 — 예외 처리)**

익명 Firebase 계정(`FirebaseUser.IsAnonymous == true`)은 OIDC 식별 주체로 인정되지 않으므로 기존대로 UGS 익명 로그인을 사용한다.

```csharp
await AuthenticationService.Instance.SignInAnonymouslyAsync();
```

- 익명 계정은 기기 종속이라 다기기 PlayerId 일관성이 애초에 불필요하다.
- 분기 판정은 `FirebaseAuthService.IsAnonymous` 프로퍼티로 수행한다.

**규칙 4. 세션 보존 (UnityServicesInitializer)**

`UnityServicesInitializer.InitializeAsync()`는 UGS 초기화만 담당하며, 인증은 `LoginUseCase.BridgeToUGSAsync()`에 위임한다.

- Login 씬이 만든 OIDC(또는 익명) 세션이 살아 있으면(`IsSignedIn == true`) 재로그인 없이 그대로 보존한다. (UGS SDK가 Access Token을 자동 갱신하므로 세션을 덮어쓰지 않는다.)
- 세션이 전혀 없는 경우(Login 씬을 거치지 않은 단독 테스트 진입 등)에만 익명 로그인으로 폴백한다.
- LobbyManager, RelayManager, MatchmakerManager, NetworkGameManager 등 기존 멀티플레이 코드는 변경하지 않는다.

> **참고 (기존 로직 비활성화)**
>
> 과거 `InitializeAsync()`는 매 호출 시 `SignOut()` → `SignInAnonymouslyAsync()`로 항상 재로그인했다(HTTP 401 토큰 만료 회피 목적).
> OIDC 전환으로 이 동작은 OIDC 세션을 덮어써 PlayerId가 매번 바뀌는 문제를 일으키므로 비활성화했다.
> 해당 블록은 `UnityServicesInitializer.cs`에 주석으로 남아 있으며, 사용자 테스트 통과 후 최종 삭제 예정.

---

## 개발 및 테스트 분리

**규칙 1. 분리 원칙**

로그인 시스템 구현 및 검증이 완료될 때까지 로그인 씬을 게임 진입 흐름에 포함하지 않는다.
Build Index를 수동으로 조정하여 테스트 목적에 따라 진입점을 선택한다.

**규칙 2. 정식 통합 조건**

로그인 시스템 검증이 완료되고 게임 기능이 충분히 완성된 시점에 Build Index를 Login = 0 으로 고정하여 정식 통합한다.
