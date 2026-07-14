# Research: 닉네임 설정 시점 통일 (로그인 흐름 수정)

## 이 작업이 무엇인가

이메일 회원가입 시 닉네임을 저장하려 하면 **"Access token is missing"** 에러가 나면서 저장이 실패한다.
원인은 **닉네임을 저장하는 시점에 UGS 로그인 세션이 아직 없기 때문**이다.

이 작업은 닉네임 설정 시점을 **"UGS 세션이 있는 첫 로그인 성공 직후"로 통일**하여,
이메일·Google 두 경로가 같은 방식으로 동작하고 토큰 에러가 원천적으로 사라지도록 흐름을 고친다.

---

## 문제 재현 및 로그

실기기 테스트 중 이메일 회원가입 → 닉네임 확인 버튼 클릭 시:

```
[PlayerProfileService] 닉네임 저장 실패: Access token is missing -
ensure you are signed in through the Authentication SDK and try again.
  → Hexiege.Infrastructure.PlayerProfileService:SaveNicknameAsync
  → Hexiege.Application.PlayerProfileUseCase:SaveNicknameAsync
  → Hexiege.Presentation.NicknameSetupView:OnConfirmClicked
```

익명 로그인은 정상("게스트" 표시 확인됨). Google은 세션이 있으므로 정상 예상.

---

## 근본 원인 (코드 근거)

### LoginUseCase.SignUpWithEmailAsync (234~243번 줄)
```csharp
public async Task<LoginResult> SignUpWithEmailAsync(string email, string password, string displayName)
{
    await _authService.SignUpWithEmailAsync(email, password, displayName);
    // ... 인증 메일 발송
    return LoginResult.NeedsEmailVerification;   // ← BridgeToUGSAsync 호출 없음!
}
```

- **이메일 가입은 UGS 브릿지(BridgeToUGSAsync)를 하지 않는다.** Firebase 계정만 만들고 즉시 `NeedsEmailVerification` 반환.
- 반면 실제 로그인 메서드들은 브릿지함:
  - `SignInWithGoogleAsync` → BridgeToUGSAsync (118번 줄)
  - `SignInWithEmailAsync` → BridgeToUGSAsync (214번 줄)
  - `SignInAnonymouslyAsync` → BridgeToUGSAsync (147번 줄)

### 현재 흐름 (이메일)
```
SignUpView.OnSignUpClicked
  → SignUpWithEmailAsync → NeedsEmailVerification (UGS 세션 없음)
  → _rootView.ShowNicknameSetup(isGooglePath: false)
  → NicknameSetupView.OnConfirmClicked
  → PlayerProfileUseCase.SaveNicknameAsync
  → PlayerProfileService.SaveNicknameAsync
  → CloudSaveService...SaveAsync  ❌ 토큰 없음 → 실패
```

Cloud Save는 UGS access token이 있어야 쓸 수 있는데, 이 시점엔 세션이 없다.

---

## 각 로그인 경로의 현재 상태

| 경로 | 진입 View | UGS 세션 시점 | 닉네임 시점 | 상태 |
|------|-----------|--------------|------------|------|
| Google | LoginSelectView.OnGoogleLoginClicked | 로그인 성공 시(브릿지) | 로그인 후 IsFirstLogin 분기 | ✅ 정상 |
| 익명 | AnonymousWarningPopup | 로그인 성공 시(브릿지) | 없음(게스트) | ✅ 정상 |
| 이메일 가입 | SignUpView | **없음** | **가입 직후** | ❌ 토큰 에러 |
| 이메일 로그인 | EmailLoginView.OnLoginClicked | 로그인 성공 시(브릿지) | 없음(바로 로비) | 닉네임 분기 없음 |

**관찰**: Google은 이미 "로그인 → IsFirstLogin() → 닉네임" 패턴(LoginSelectView.OnGoogleLoginClicked 112~123번 줄).
이메일만 가입 시점에 닉네임을 넣으려다 깨진다.

---

## 관련 파일

### 수정 대상
- `Scripts/Application/UseCases/LoginUseCase.cs` — SignUpWithEmailAsync (브릿지 안 함), SignInWithEmailAsync (브릿지 함)
- `Scripts/Presentation/UI/Views/Login/SignUpView.cs` — 가입 성공 시 ShowNicknameSetup(false) 호출 중 (140~151번 줄)
- `Scripts/Presentation/UI/Views/Login/EmailLoginView.cs` — 로그인 성공 시 GoToNextScene만(125~127번 줄), 닉네임 분기 없음, PlayerProfileUseCase 미주입
- `Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs` — GoToNextStep()이 isGooglePath로 분기(Google→로비, 이메일→EmailVerify)
- `Scripts/Bootstrap/LoginBootstrapper.cs` — InjectDependencies에서 EmailLoginView에 PlayerProfileUseCase 미주입

### 참고 (변경 없음, 패턴 참조용)
- `LoginSelectView.OnGoogleLoginClicked` — 목표로 삼을 "로그인→IsFirstLogin→닉네임" 패턴
- `PlayerProfileUseCase.IsFirstLogin()` — Cloud Save에 nickname 없으면 true

### 문서
- `Docs/AuthSystemRules.md` — 이메일 회원가입 흐름 서술(가입 직후 닉네임)
- `Docs/GameSystemRules/GameSystemRules_UI.md` — 닉네임 설정 화면 규칙 4(완료 후 흐름)

---

## 채택 방향 (사용자 확정: C안)

**닉네임 설정 = "UGS 세션이 있는 첫 로그인 성공 직후"로 통일.**

- 이메일 가입 시점의 닉네임 제거 → 가입은 인증 화면으로 직행
- 이메일 로그인 성공 시 IsFirstLogin 분기 추가 (Google과 동일 패턴)
- 이메일 로그인은 이미 UGS 브릿지되므로 닉네임 저장 시 토큰 존재 → 성공

### 대안 비교 (기록용)
- **A. 가입 직후 UGS 브릿지**: 최소 변경. 단 미인증 계정에도 UGS 세션+데이터 생성 → 설계 원칙 위반, 고아 데이터. 기각.
- **B. 저장 지연(로컬→세션 후 flush)**: 흐름 유지, 데이터 깨끗. 단 로컬 pending 관리 edge case. 차선.
- **C. 닉네임 시점을 첫 로그인 후로 통일**: 완성도 최고(경로 통일, 토큰 에러 원천 제거, 이메일 특수 분기 삭제). 채택.

---

## 확인 필요/주의

1. **설계 변경 문서화**: C는 "가입 직후 닉네임" → "인증 후 첫 로그인 시 닉네임"으로 흐름이 바뀐다. AuthSystemRules.md / GameSystemRules_UI.md 갱신 필요.
2. **자동닉네임 접두사**: 스킵 시 `구글_xxx` vs `사용자_xxx` 구분이 있었음. 라우팅용 isGooglePath는 제거하되, 접두사 구분은 유지해야 함(접두사만 전달하거나 provider에서 판단).
3. **UI 개선(별도)**: 닉네임 패널 스프라이트 적용은 본 작업과 분리(기능 우선, 사용자 지시).
