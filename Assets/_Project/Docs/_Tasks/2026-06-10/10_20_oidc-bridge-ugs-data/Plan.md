# Plan — OIDC Bridge & UGS 데이터 플랫폼 설계

## 이 작업이 구현하는 것

Firebase 로그인과 UGS를 실제로 연결하는 OIDC Bridge를 구현한다.
이 작업이 완료되면 Firebase 계정(Google/이메일)으로 로그인한 플레이어는
기기를 바꿔도 동일한 UGS PlayerId를 받게 되며, 이후 랭킹·재화·인벤토리 등
계정 귀속 데이터를 UGS 서비스에 안전하게 저장할 수 있는 기반이 마련된다.

**이 Plan의 범위**: OIDC Bridge 연결 코드 수정 + AuthSystemRules.md 업데이트.
Cloud Save / Leaderboard / Economy 실제 기능 구현은 별도 작업으로 진행한다.

---

## 규칙 근거

| 수정 항목 | 근거 문서 |
|-----------|----------|
| OIDC Bridge 구현 방식 | `AuthSystemRules.md` — UGS 연결 규칙 1~3 (OIDC 방식으로 재설계) |
| UnityServicesInitializer 세션 보존 | `AuthSystemRules.md` — UGS 연결 규칙 3 주석 "재검토 시점" |
| PlayerId 계정 귀속 | `AuthSystemRules.md` — 세션 및 다기기 동작 규칙 1~2 |
| 아키텍처 레이어 준수 | `MEMORY.md` — Infrastructure 레이어 원칙 |

---

## 구현 항목

### Step 1. `LoginUseCase.cs` — OIDC Bridge 구현

**파일**: `Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs`
**변경 위치**: `BridgeToUGSAsync()` 메서드 내부 (lines 362–368)

**현재 코드**
```csharp
// TODO: 이 UGS Authentication SDK 버전은 SignInWithCustomIdAsync 를 지원하지 않는다.
if (!AuthenticationService.Instance.IsSignedIn)
{
    await AuthenticationService.Instance.SignInAnonymouslyAsync();
}
```

**변경 후**
```csharp
// Firebase ID Token을 UGS OIDC Provider에 전달 → Firebase 계정과 PlayerId 1:1 연결
string firebaseToken = await firebaseUser.TokenAsync(false);
await AuthenticationService.Instance.SignInWithOpenIdConnectAsync(
    "oidc-firebase",
    firebaseToken
);
```

**처리 방식**
- 익명 계정(Anonymous)의 경우: Firebase UID가 없으므로 기존대로 `SignInAnonymouslyAsync` 사용
- 실계정(Google / 이메일) 로그인의 경우: `SignInWithOpenIdConnectAsync` 사용
- Firebase Token 발급 실패 시: `AuthSystemRules.md` 오류 처리 규칙 3에 따라 로그인은 성공 처리 후 멀티플레이 제한 안내

**전달받아야 하는 값**
- `BridgeToUGSAsync(string firebaseUID)` → 시그니처 변경 필요
- `BridgeToUGSAsync(Firebase.Auth.FirebaseUser firebaseUser)` 또는 JWT 문자열 추가 인자
- 정확한 시그니처는 game-programmer 에이전트가 `LoginUseCase.cs` 전체 흐름을 보고 결정

---

### Step 2. `UnityServicesInitializer.cs` — 세션 보존 로직 교체

**파일**: `Assets/_Project/Scripts/Infrastructure/Network/UnityServicesInitializer.cs`
**변경 위치**: `InitializeAsync()` 내부 lines 87–93

**현재 코드** (문제 구간)
```csharp
// 항상 재로그인 — 토큰 만료 대비
if (AuthenticationService.Instance.IsSignedIn)
{
    AuthenticationService.Instance.SignOut();
}
await AuthenticationService.Instance.SignInAnonymouslyAsync();
```

**변경 방향**
- OIDC 세션이 유효한 경우 → 재로그인 없이 그대로 유지
- 세션이 없는 경우에만 → 필요시 재로그인 또는 오류 반환

**주의**: 기존 HTTP 401 버그를 고치기 위해 "항상 재로그인"을 도입했다.
OIDC로 전환 후에는 토큰 갱신 책임이 Firebase SDK → UGS SDK로 넘어가므로
"항상 재로그인" 로직은 더 이상 필요하지 않다.
단, 세션 만료 엣지케이스 처리 방법은 game-programmer 에이전트가 결정.

> ⚠️ 기존 로직 제거 주의 (WORKFLOW.md 규칙)
> 현재의 `SignOut()` + `SignInAnonymouslyAsync()` 블록은 즉시 삭제하지 않고
> **주석 처리 후 비활성화**한다. 사용자 테스트 통과 후 최종 삭제.

---

### Step 3. `AuthSystemRules.md` — 규칙 문서 업데이트

**파일**: `Assets/_Project/Docs/AuthSystemRules.md`
**변경 위치**: `Firebase ↔ UGS 연결 구조` 섹션 (lines 48–59), `UGS 연결` 섹션 (lines 343–366)

**변경 내용**
- `SignInWithCustomIdAsync` → `SignInWithOpenIdConnectAsync("oidc-firebase", firebaseJWT)` 로 교체
- 다이어그램 업데이트:
  ```
  Firebase 로그인 → Firebase ID Token 발급
                            ↓
  UGS SignInWithOpenIdConnectAsync("oidc-firebase", token) → UGS PlayerId 발급
                                                                    ↓
                                             UGS Lobby / Relay / Cloud Save / Leaderboard 사용 가능
  ```
- UGS 연결 규칙 2: Custom ID → OIDC Token 으로 변경
- UGS 연결 규칙 3: 익명 계정 예외 처리 추가
- 현재 상태 경고 주석 제거

---

## 외부 설정 (코드 작업 전 필요)

이 설정이 완료되어야 OIDC Bridge 코드가 실제로 작동한다.

| 단계 | 내용 | 담당 |
|------|------|------|
| 1 | UGS Dashboard → Authentication → ID Providers → Add OIDC Provider | 사용자 |
| 2 | Provider 이름: `oidc-firebase` / Issuer: `https://securetoken.google.com/<project-id>` / Client ID: `<firebase-project-id>` | 사용자 |
| 3 | `google-services.json` 내 `project_id` 확인 | 사용자 |

---

## 구현 제외 항목 (이번 작업 범위 아님)

| 항목 | 이유 |
|------|------|
| Cloud Save 기능 구현 | 별도 작업으로 진행 |
| Leaderboard 기능 구현 | 별도 작업으로 진행 |
| Economy 기능 구현 | 별도 작업으로 진행 |
| RankingView UI 구현 | 별도 작업으로 진행 |

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| UGS Dashboard OIDC 설정 전 코드 실행 시 `InvalidProviderException` | 설정 완료 전 테스트 불가, 순서 필수 |
| 익명 계정에서 OIDC 호출 시 토큰 없음 오류 | `firebaseUser.IsAnonymous` 분기로 처리 |
| 기존 매칭 중 세션 덮어쓰기 버그 재발 | Step 2 적용 후 멀티플레이 테스트 필수 |
| Firebase Token 만료(1시간) 후 재로그인 시 PlayerId 유지 확인 | `TokenAsync(false)` → 자동 갱신, 정상 작동 예상 |

---

## 구현 순서

```
[1] 외부 설정 — UGS Dashboard OIDC Provider 등록 (사용자)
      ↓
[2] LoginUseCase.cs — BridgeToUGSAsync() OIDC 구현 (game-programmer)
      ↓
[3] UnityServicesInitializer.cs — 세션 보존 로직 교체 (game-programmer)
      ↓
[4] AuthSystemRules.md 업데이트 (규칙 문서 반영)
      ↓
[5] 사용자 테스트: Google/이메일 로그인 → 기기 변경 후 PlayerId 동일 확인
```
