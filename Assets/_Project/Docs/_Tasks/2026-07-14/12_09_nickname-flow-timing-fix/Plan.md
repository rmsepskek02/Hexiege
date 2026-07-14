# Plan: 닉네임 설정 시점 통일 (로그인 흐름 수정 — C안)

## 이 작업이 무엇인가

이메일 회원가입 직후 닉네임을 저장할 때 UGS 세션이 없어서 "Access token is missing" 에러가 난다.
이를 고치기 위해 **닉네임 설정을 "UGS 세션이 있는 첫 로그인 성공 직후"로 통일**한다.
이메일도 Google과 똑같이 "로그인 성공 → 최초 여부 확인 → 닉네임 설정" 순서로 맞춘다.
결과적으로 닉네임 저장은 항상 유효한 세션에서만 일어나 토큰 에러가 사라지고, 코드도 단순해진다.

---

## 변경 요약 (경로별)

| 경로 | 변경 |
|------|------|
| Google | 변경 없음 (이미 로그인→IsFirstLogin→닉네임) |
| 익명 | 변경 없음 |
| 이메일 가입 | 닉네임 제거 → 인증 화면 직행 |
| 이메일 로그인 | IsFirstLogin 분기 추가 → 최초면 닉네임, 아니면 로비 |

---

## 파일별 구현 상세

### ① SignUpView.cs — 가입 시 닉네임 제거
**근거**: Research 근본원인 — 가입 시점엔 UGS 세션이 없음.

```csharp
// 현재 (140~151번 줄 부근)
case LoginResult.NeedsEmailVerification:
    // _rootView.ShowEmailVerify();              // 구 로직(주석)
    _rootView.ShowNicknameSetup(isGooglePath: false);   // ← 제거
    return;

// 변경
case LoginResult.NeedsEmailVerification:
    _rootView.ShowEmailVerify();   // 가입 성공 → 인증 화면 직행
    return;
```
- 이전 작업에서 주석 처리했던 `ShowEmailVerify()`를 되살리고, `ShowNicknameSetup(false)` 제거.

### ② EmailLoginView.cs — 로그인 성공 시 닉네임 분기 추가
**근거**: Google(LoginSelectView.OnGoogleLoginClicked)과 동일 패턴 적용. 로그인 성공 시 UGS 브릿지 완료 상태이므로 IsFirstLogin/저장 모두 토큰 있음.

```csharp
// 의존성 필드 추가
private PlayerProfileUseCase _profileUseCase;

// Initialize 시그니처에 파라미터 추가
public void Initialize(
    LoginRootView rootView, LoginUseCase loginUseCase, LoginBootstrapper bootstrapper,
    PlayerProfileUseCase profileUseCase)
{
    ...
    _profileUseCase = profileUseCase;
    ...
}

// OnLoginClicked 성공 분기 (125~127번 줄)
case LoginResult.Success:
    bool isFirst = _profileUseCase != null && await _profileUseCase.IsFirstLogin();
    if (isFirst)
        _rootView.ShowNicknameSetup(isEmailFirstLogin: true);  // ③에서 시그니처 정리
    else
        _bootstrapper.GoToNextScene();
    return;
```

### ③ NicknameSetupView.cs — 완료 후 항상 로비 + 접두사 정리
**근거**: 이제 Google/이메일 모두 "세션 있는 로그인 후"이므로 완료 시 항상 로비.

- `GoToNextStep()` 단순화:
```csharp
private void GoToNextStep()
{
    _bootstrapper.GoToNextScene();   // 항상 로비 (EmailVerify 분기 제거)
}
```
- `_isGooglePath` (라우팅 목적) 제거. 대신 **자동닉네임 접두사 구분만** 유지:
  - `PrepareForShow(bool isGoogle)` → 스킵 시 접두사 `"구글"`/`"사용자"` 선택에만 사용.
  - 또는 접두사 결정을 `PlayerProfileUseCase`로 이관해 provider에서 판단(더 깔끔하나 범위 확대 → 이번엔 파라미터 유지, 라우팅만 제거).
- `LoginRootView.ShowNicknameSetup(bool)` 파라미터 의미를 "라우팅" → "자동닉네임 접두사 구분(Google/기타)"으로 축소. `PrepareForShow`에 그대로 전달.

### ④ LoginBootstrapper.cs — EmailLoginView에 UseCase 주입
```csharp
// InjectDependencies (현재 271번 줄 부근)
if (_emailLoginView != null)
    _emailLoginView.Initialize(_rootView, _loginUseCase, this, _playerProfileUseCase);
```
- `_playerProfileUseCase`는 이미 InitializeAndDispatchAsync에서 생성됨(재사용).

### ⑤ 문서 반영 (설계 변경)
- **AuthSystemRules.md**: 이메일 회원가입 흐름을 "가입 → 인증 → 로그인 → (최초) 닉네임 → 로비"로 수정. 닉네임 규칙에서 "가입 직후 닉네임" 서술 제거.
- **GameSystemRules_UI.md**: 닉네임 설정 화면 규칙 4(완료 후 흐름)를 "모든 경로 → 로비"로 수정. "이메일 경로 → EmailVerify" 서술 제거.

---

## 기존 로직 제거 규칙 (WORKFLOW 준수)

- **SignUpView의 `ShowNicknameSetup(false)` 제거**: 대체 코드(`ShowEmailVerify()`)가 명확하고, 이 경로가 토큰 에러의 직접 원인이므로 제거 안전. 단 검증 전까지 삭제 대신 주석 처리 유지 → [6] 사용자 실기 통과 후 최종 삭제.
- **NicknameSetupView의 EmailVerify 분기 제거**: 마찬가지로 주석 처리 후 실기 통과 시 삭제.

---

## 아키텍처 제약 확인

| 제약 | 적용 |
|------|------|
| Application→Infrastructure 역참조 금지 | 변경 없음(기존 IPlayerProfileService 사용) |
| UIManager null-safe | 기존 패턴 유지 |
| Presentation 레이어 규약 | View만 수정, 로직은 UseCase 통해 |

---

## 변경 파일 목록 (예상)

**[수정]**
- `Scripts/Presentation/UI/Views/Login/SignUpView.cs`
- `Scripts/Presentation/UI/Views/Login/EmailLoginView.cs`
- `Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs`
- `Scripts/Presentation/UI/Views/Login/LoginRootView.cs` (ShowNicknameSetup 파라미터 의미 축소, 필요 시)
- `Scripts/Bootstrap/LoginBootstrapper.cs`
- `Docs/AuthSystemRules.md`
- `Docs/GameSystemRules/GameSystemRules_UI.md`

**[변경 없음]**
- LoginSelectView.cs (Google 경로 — 이미 목표 패턴)
- AnonymousWarningPopup.cs (익명 — 닉네임 없음)
- PlayerProfileService/UseCase (저장 로직 자체는 그대로, 호출 시점만 변경)

---

## 테스트 시나리오 (실기)

| 경로 | 기대 |
|------|------|
| 이메일 가입 | 가입 → **바로 인증 화면**(닉네임 화면 안 뜸) |
| 이메일 인증 후 첫 로그인 | 로그인 성공 → **닉네임 화면** → 저장 성공(토큰 에러 없음) → 로비 |
| 이메일 재로그인 | 로그인 → 닉네임 화면 없이 바로 로비 |
| Google 최초 | 변경 없음 — 닉네임 화면 → 로비 |
| Google 재로그인 | 변경 없음 — 바로 로비 |
| 익명 | 변경 없음 — 게스트, 바로 로비 |

---

## 범위 밖 (별도 작업)

- **닉네임 패널 UI 개선(스프라이트 적용)**: 사용자 지시로 기능 완료 후 진행.
- **initPlayer/recordMatchResult Cloud Code 연결(서버 권위)**: 별도 후속 작업.
