# UI 구상도: 로그인 시스템 (Login.unity)

로그인 시스템에 포함되는 모든 화면의 레이아웃 구상도입니다. Hexiege는 모바일 세로 모드(9:16) 게임이므로 모든 화면은 세로 레이아웃을 기준으로 합니다. 아래 ASCII 구상도는 각 화면에 어떤 요소가 어떤 순서로 배치되는지를 나타냅니다. 실제 디자인 에셋(색상, 폰트, 이미지)은 추후 결정합니다.

---

## 공통 사항 (실제 구현 기준)

**씬 계층 구조** (GameSystemRules_UI.md Rule 4 준수):
```
Canvas
├── Background          ← bg_login.png 전체화면 (SafeAreaContainer 밖)
└── SafeAreaContainer   ← SafeAreaFitter (노치/홈바 대응)
    ├── LoginRoot       ← 일반 뷰 5개의 부모 (투명)
    ├── ConfirmPopup    ← 오버레이 팝업
    ├── LoadingIndicator← 로딩 스피너 (ui_spinner_hexorb)
    └── AnonymousWarningPopup ← 오버레이 팝업
```

**일반 뷰 (5개)**: 패널 배경 없는 투명 컨테이너. 씬 배경(bg_login.png)이 그대로 노출.
**팝업 (2개)**: BlockingOverlay(반투명 검정) + PopupBox(ui_panel_light.png).

**공통 UI 규칙**:
- 텍스트 색상: `Color.black` 전체 통일
- 버튼 스프라이트: `ui_btn_lavender` 전체 통일 (ConfirmPopup 포함)
- BackButton: `ui_icon_back.png` 직접 사용 (투명 배경 확인 완료, 별도 배경 버튼 없음)
- 팝업 닫기: PopupBox 우측 상단에 `ui_btn_cancel` 버튼
- 콘텐츠 정렬: VLG `childAlignment = MiddleCenter` (수직 중앙)
- 폰트: Maplestory Bold(타이틀·버튼) / Maplestory Light(안내문·입력)
- 레이아웃: VLG + LayoutElement.preferredHeight (sizeDelta 사용 안 함, Rule 2)
- 패널 전환: SetActive (DOTween 애니메이션은 팝업에만 AnimatedPanel 사용)
- 오류/안내: 각 화면 하단 StatusText
- 로딩: LoadingIndicator GO 활성화 (반투명 오버레이 + 중앙 스피너)

---

## 1. 로그인 선택 화면 (LoginSelectView)

자동 로그인 실패 시 표시되는 첫 화면. 세 가지 로그인 방식을 선택할 수 있습니다.
BackButton 없음 → ContentArea Padding Top 80 (ContentAreaNoBack).

```
┌─────────────────────────┐
│   [bg_login.png 배경]   │  ← 전체화면 씬 배경 (패널 배경 없음)
│                         │
│   ┌─────────────────┐   │
│   │    HEXIEGE      │   │  ← Title (Maplestory Bold, 68pt)
│   └─────────────────┘   │
│                         │  ← ↕ VLG MiddleCenter 기준 수직 중앙 정렬
│  ┌───────────────────┐  │
│  │  Google로 로그인   │  │  ← ui_btn_lavender
│  └───────────────────┘  │
│  ┌───────────────────┐  │
│  │  이메일로 로그인   │  │  ← ui_btn_lavender
│  └───────────────────┘  │
│  ┌───────────────────┐  │
│  │  익명으로 시작하기 │  │  ← ui_btn_lavender
│  └───────────────────┘  │
│                         │
│   [StatusText 오류표시]  │  ← 하단 앵커 고정 (anchorMin.y=0.042)
└─────────────────────────┘
```

**Inspector 컴포넌트**:
- `_googleLoginButton` (Button)
- `_emailLoginButton` (Button)
- `_anonymousButton` (Button)
- `_statusText` (TextMeshProUGUI)

---

## 2. 익명 로그인 경고 팝업 (AnonymousWarningPopup)

익명으로 시작하기 클릭 시 오버레이로 표시. 팝업 외부 클릭으로는 닫히지 않음.
구조: 전체화면 BlockingOverlay(검정 반투명) + 중앙 PopupBox(ui_panel_light, 720×760).

```
┌─────────────────────────┐
│  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  │  ← BlockingOverlay (검정 60% 불투명)
│  ▓  ┌─────────────[X]▓  │  ← PopupBox (ui_panel_light) + CloseButton (ui_btn_cancel, 우측상단)
│  ▓  │               │ ▓  │
│  ▓  │  [경고 텍스트]  │ ▓  │  ← WarningText (VLG Label)
│  ▓  │               │ ▓  │
│  ▓  │ ┌───────────┐ │ ▓  │
│  ▓  │ │ 계정 만들기│ │ ▓  │  ← ui_btn_lavender
│  ▓  │ └───────────┘ │ ▓  │
│  ▓  │ ┌───────────┐ │ ▓  │
│  ▓  │ │계속 익명으로│ │ ▓  │  ← ui_btn_lavender
│  ▓  │ └───────────┘ │ ▓  │
│  ▓  └───────────────┘ ▓  │
│  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  │
└─────────────────────────┘
```

**Inspector 컴포넌트**:
- `_panel` (AnimatedPanel)
- `_blockingOverlay` (GameObject)
- `_warningText` (TextMeshProUGUI)
- `_createAccountButton` (Button)
- `_continueAnonymousButton` (Button)

---

## 3. 이메일 로그인 화면 (EmailLoginView)

BackButton 있음 → ContentArea Padding Top 200.

```
┌─────────────────────────┐
│ [←]                     │  ← BackButton (ui_icon_back 직접, 좌상단 앵커 고정)
│                         │
│      이메일 로그인        │  ← Title (Maplestory Bold, 68pt)
│                         │  ← ↕ VLG MiddleCenter
│  ┌─────────────────────┐│
│  │ 이메일 주소          ││  ← TMP_InputField (EmailAddress)
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │ 비밀번호             ││  ← TMP_InputField (Password)
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │    비밀번호 찾기      ││  ← ui_btn_lavender
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │       로그인          ││  ← ui_btn_lavender
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │       회원가입        ││  ← ui_btn_lavender
│  └─────────────────────┘│
│   [StatusText 오류표시]  │  ← 하단 앵커 고정
└─────────────────────────┘
```

**Inspector 컴포넌트**:
- `_backButton` (Button)
- `_emailInput` (TMP_InputField, EmailAddress)
- `_passwordInput` (TMP_InputField, Password)
- `_forgotPasswordButton` (Button)
- `_loginButton` (Button)
- `_signUpButton` (Button)
- `_statusText` (TextMeshProUGUI)

---

## 4. 이메일 회원가입 화면 (SignUpView)

BackButton 있음 → ContentArea Padding Top 200.

```
┌─────────────────────────┐
│ [←]                     │  ← BackButton (ui_icon_back 직접)
│                         │
│          회원가입         │  ← Title (Maplestory Bold, 68pt)
│                         │  ← ↕ VLG MiddleCenter
│  ┌─────────────────────┐│
│  │ 이메일 주소          ││  ← TMP_InputField (EmailAddress)
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │ 비밀번호 (최소 6자)  ││  ← TMP_InputField (Password)
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │ 비밀번호 확인        ││  ← TMP_InputField (Password)
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │        회원가입       ││  ← ui_btn_lavender
│  └─────────────────────┘│
│   [StatusText 오류표시]  │  ← 하단 앵커 고정
└─────────────────────────┘
```

**Inspector 컴포넌트**:
- `_backButton` (Button)
- `_emailInput` (TMP_InputField, EmailAddress)
- `_passwordInput` (TMP_InputField, Password)
- `_passwordConfirmInput` (TMP_InputField, Password)
- `_signUpButton` (Button)
- `_statusText` (TextMeshProUGUI)

---

## 5. 이메일 인증 대기 화면 (EmailVerifyView)

회원가입 완료 직후 또는 미인증 계정 로그인 시도 시 표시.
BackButton 없음 → ContentAreaNoBack (Padding Top 80).

```
┌─────────────────────────┐
│                         │
│       이메일 인증         │  ← Title (Maplestory Bold, 68pt)
│                         │  ← ↕ VLG MiddleCenter
│   [이메일 주소 표시]      │  ← EmailText (Label)
│                         │
│   인증 메일이 발송되었    │  ← GuideText (Label)
│   습니다. 메일함을 확인   │
│   하고 인증 링크를        │
│   클릭해 주세요.          │
│                         │
│  ┌─────────────────────┐│
│  │    인증 완료 확인    ││  ← ui_btn_lavender
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │   인증 메일 재발송   ││  ← ui_btn_lavender
│  └─────────────────────┘│
│   [StatusText 안내/오류] │  ← 하단 앵커 고정
└─────────────────────────┘
```

**Inspector 컴포넌트**:
- `_emailText` (TextMeshProUGUI)
- `_checkVerifyButton` (Button)
- `_resendButton` (Button)
- `_statusText` (TextMeshProUGUI)

---

## 6. 비밀번호 재설정 화면 (PasswordResetView)

BackButton 있음 → ContentArea Padding Top 200.

```
┌─────────────────────────┐
│ [←]                     │  ← BackButton (ui_icon_back 직접)
│                         │
│      비밀번호 재설정      │  ← Title (Maplestory Bold, 68pt)
│                         │  ← ↕ VLG MiddleCenter
│  가입 시 사용한 이메일    │  ← GuideText (Label)
│  주소를 입력하시면        │
│  재설정 링크를 보내드립   │
│  니다.                   │
│                         │
│  ┌─────────────────────┐│
│  │ 이메일 주소          ││  ← TMP_InputField (EmailAddress)
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │  재설정 메일 보내기  ││  ← ui_btn_lavender
│  └─────────────────────┘│
│   [StatusText 안내/오류] │  ← 하단 앵커 고정
└─────────────────────────┘
```

**Inspector 컴포넌트**:
- `_backButton` (Button)
- `_emailInput` (TMP_InputField, EmailAddress)
- `_sendButton` (Button)
- `_statusText` (TextMeshProUGUI)

---

## 7. 앱 종료 확인 / 네트워크 오류 팝업 (ConfirmPopup 공용)

로그인 선택 화면에서 Android 뒤로가기 2회 연속 입력 시 앱 종료 확인으로 사용.
네트워크 오류 발생 시에도 동일 인스턴스 재사용.
구조: BlockingOverlay + PopupBox(ui_panel_light, 720×460) + HLG 버튼 2개.

```
┌─────────────────────────┐
│  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  │  ← BlockingOverlay
│  ▓  ┌─────────────[X]▓  │  ← PopupBox + CloseButton (ui_btn_cancel, 우측상단)
│  ▓  │               │ ▓  │
│  ▓  │  [MessageText] │ ▓  │  ← 런타임에 Show() 호출 시 메시지 주입
│  ▓  │               │ ▓  │
│  ▓  │ ┌─────┐┌─────┐│ ▓  │
│  ▓  │ │ 확인 ││ 취소 ││ ▓  │  ← HLG 버튼 컨테이너 (ui_btn_lavender × 2)
│  ▓  │ └─────┘└─────┘│ ▓  │
│  ▓  └───────────────┘ ▓  │
│  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  │
└─────────────────────────┘
```

**Inspector 컴포넌트**:
- `_panel` (AnimatedPanel)
- `_blockingOverlay` (GameObject)
- `_messageText` (TextMeshProUGUI)
- `_confirmButton` (Button)
- `_cancelButton` (Button)
- `_confirmButtonText` (TextMeshProUGUI) — 런타임 버튼 라벨 변경용
- `_cancelButtonText` (TextMeshProUGUI) — 런타임 버튼 라벨 변경용
- `_colorConfig` (UIColorConfig) — 버튼 색상 설정 ScriptableObject

---

## 8. 네트워크 오류 팝업

7번 ConfirmPopup과 동일한 인스턴스를 재사용. `_networkErrorPopup` 필드에 동일 GO를 연결.
런타임에 `Show("네트워크 설정을 확인하고 다시 시도하세요.", "확인", "", ...)` 형태로 호출.

---

## 9. ProfileView — Lobby 씬 Profile 탭

기존 Lobby 씬 Profile 탭에 추가. 현재 `ProfileView.cs`는 빈 파일.

### 익명 계정 로그인 상태
```
┌─────────────────────────┐
│         프로필            │
│                         │
│  현재 익명으로 로그인    │
│  중입니다.               │
│                         │
│  계정을 연동하면 기기    │
│  변경 시에도 데이터를    │
│  유지할 수 있습니다.     │
│                         │
│  ┌─────────────────────┐│
│  │   G  Google로 연동   ││
│  └─────────────────────┘│
│                         │
│  ┌─────────────────────┐│
│  │   ✉  이메일로 연동   ││
│  └─────────────────────┘│
│                         │
│  ───────────────────────│
│                         │
│  ┌─────────────────────┐│
│  │       로그아웃        ││
│  └─────────────────────┘│
└─────────────────────────┘
```

### 실계정 로그인 상태 (Google 또는 이메일)
```
┌─────────────────────────┐
│         프로필            │
│                         │
│  [Google 계정명]         │  ← 또는 이메일 주소
│  또는 [이메일 주소]      │
│                         │
│  (연동 버튼 미표시)       │
│                         │
│                         │
│                         │
│  ───────────────────────│
│                         │
│  ┌─────────────────────┐│
│  │       로그아웃        ││
│  └─────────────────────┘│
└─────────────────────────┘
```

**Inspector 컴포넌트** (ProfileView.cs):
- `_accountInfoText` (TextMeshProUGUI) — 계정 정보 또는 "익명" 안내
- `_linkGoogleButton` (Button) — Google 연동 (익명 계정만 표시)
- `_linkEmailButton` (Button) — 이메일 연동 (익명 계정만 표시)
- `_logoutButton` (Button) — 로그아웃 후 Login 씬 이동
- `_anonymousSection` (GameObject) — 익명 계정 전용 영역 (연동 버튼 포함)
