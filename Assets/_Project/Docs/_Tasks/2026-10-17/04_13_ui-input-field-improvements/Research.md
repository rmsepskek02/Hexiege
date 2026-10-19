# 인풋필드 공통 개선 조사

입력 도중 다른 곳을 눌렀다가 돌아와도 내용을 이어서 고칠 수 있게 하고, 로그인부터 방 참가까지 입력 조작을 일관되게 만드는 작업이다. 이 문서는 실제 씬에 연결된 입력란, 현재 동작의 정적 근거, 개선 범위와 아직 확인하지 못한 부분을 정리한다.

- 작성일: 2026-10-17 (KST)
- 작업 브랜치: `codex/ui-improvements` (메인 세션 생성·전환 인계)
- 현재 단계: 이번에 합의한 인풋필드 개선은 구현과 안내한 사용자 테스트까지 전체 수용 완료다. Android 삼성 키보드에서 총 9개 필드의 가림/복원을 포함한 동작을 확인했고, Unity Editor Play/Game 창에서 로그인/회원가입 Tab・Shift+Tab 이동도 정상 보고됐다. 정확한 APK/빌드 해시는 미제공이며 PC standalone 빌드 결과로 확대하지 않는다. 원인 로그는 19절, 구현・정적 검사는 21절, 최종 사용자 수용은 23절을 따른다.
- 문서 갱신 범위: 기존 `Research.md`, `Plan.md`에 Android 사용자 보고와 후속 구현・검사 인계를 기록한다. 상시 문서・메모리・Testcase는 변경하지 않는다.
- **읽는 기준:** 2~6절은 구현 전 조사, 8~21절은 각 단계의 구현・검사・재작업 당시 기록이다. 22절은 통합 수용 뒤 다른 4개 가림/복원과 Tab 확인이 남았던 당시 상태이며, 최종 전체 사용자 수용과 완료 범위는 23절을 따른다. 이전 절의 미확인・보류・미적용을 현재 잔여로 재사용하지 않는다. 8절의 씬 무변경은 최초 구현에만 해당하며 이후 씬 변경은 해당 후속 절을 따른다. 씬 저장값・런타임 교정・사용자 보고・정적 검사를 구분한다.

## 1. 사용자 합의와 경계

첫 단계는 전체 인풋필드의 커서·이어쓰기 개선이다. 재선택만으로 전체 선택되거나 입력값이 초기화되어서는 안 된다. 사용자가 터치·클릭한 위치에서 삽입·삭제할 수 있어야 하며, 직접 선택·전체 선택·복사·붙여넣기는 유지한다.

함께 계획할 항목은 다음/완료 입력, Tab·Shift+Tab 이동, 한글 조합 Enter의 오제출 방지, 필수값·오류·버튼 상태 통일, 중복 요청 차단, 화면 종료 시 포커스·키보드 정리다. 같은 화면에서 잠깐 포커스를 옮길 때는 비밀번호를 포함한 입력값을 유지하고, 해당 화면을 완전히 닫을 때 비밀번호를 비운다.

비밀번호 보기 버튼은 별도 후속이다. 모바일 키보드 가림에 따른 화면 이동·스크롤은 최초 범위에서 제외했으나, 11절의 최신 사용자 승인으로 이번 확장 범위에 포함됐다. 전면 레이아웃 변경, 네트워크 상태 UI, 인증 정책·게임플레이 변경은 이번 범위에 포함하지 않는다.

위 공통 편집·이동·오류 상태·입력 정리 동작은 **이번 대화에서 합의한 신규 요구**다. 기존 규칙 문서가 이미 이 동작을 규정한다고 해석하지 않는다. 기존 문서 변경은 이번 승인에 포함되지 않는다.

## 2. 조사 방법과 실제 적용 대상

설치된 TMP 입력 컴포넌트의 스크립트 GUID `2da0c512f12947e489f739169773d7ca`를 기준으로 `Assets/` 아래 모든 `.unity`·`.prefab`를 검색했다. 각 YAML 컴포넌트의 fileID와 View의 직렬화 참조를 대조했다. 결과는 Login 씬 7개, Lobby 씬 2개, 총 9개다. 현재 검색 범위에서 별도 프리팹의 동일 컴포넌트는 발견되지 않았다. 런타임 생성 가능한 모든 외부 콘텐츠까지 증명하는 집계는 아니다.

아래 코드 경로는 `Assets/_Project/Scripts/Presentation/UI/` 기준이다. 씬 경로는 `Assets/_Project/Scenes/` 기준이며 줄 번호는 조사 시점의 위치다.

| 화면·입력란 | 담당 코드·필드 | 씬 연결 근거 | TMP 컴포넌트 fileID |
|---|---|---|---|
| 로그인 이메일 | `Views/Login/EmailLoginView.cs:39` `_emailInput` | `Login.unity:1586` | 906678762 |
| 로그인 비밀번호 | `Views/Login/EmailLoginView.cs:42` `_passwordInput` | `Login.unity:1587` | 965364146 |
| 회원가입 이메일 | `Views/Login/SignUpView.cs:45` `_emailInput` | `Login.unity:3399` | 957664188 |
| 회원가입 비밀번호 | `Views/Login/SignUpView.cs:48` `_passwordInput` | `Login.unity:3400` | 949131676 |
| 회원가입 비밀번호 확인 | `Views/Login/SignUpView.cs:51` `_passwordConfirmInput` | `Login.unity:3401` | 2089938883 |
| 비밀번호 재설정 이메일 | `Views/Login/PasswordResetView.cs:35` `_emailInput` | `Login.unity:3331` | 2088017634 |
| 최초 닉네임 | `Views/Login/NicknameSetupView.cs:48` `_nicknameInput` | `Login.unity:11293` | 374552444 |
| 닉네임 변경 | `Views/Lobby/Profile/NicknameChangePopup.cs:49` `_nicknameInput` | `Lobby.unity:14683` | 65268447 |
| 커스텀 방 코드 | `Views/Lobby/Battle/CustomJoinView.cs:33` `_codeInput` | `Lobby.unity:18058` | 1075429685 |

### 별도 참조의 실제 연결 여부

| 조사 대상 | 정적 확인 | 이번 취급 |
|---|---|---|
| `Views/Lobby/Profile/ProfileView.cs:98,101` 이메일 연동 입력 2개 | `Lobby.unity:6492,6493`의 `_linkEmailInput`, `_linkPasswordInput`은 모두 fileID 0 | 실제 9개에 더하지 않는다. 공통 처리가 null 참조를 허용하도록 계획하며, 연동 UI 신설·배선 복구는 승인 범위 밖이다. 향후 연결한다면 공통 기준을 적용한다. |
| 구형 `LobbyUI.cs:82` `_codeInput` | `LobbyUI.cs.meta`의 GUID `e63018de7a4f8184e8f4552f0b1f950b`가 `Assets/_Project/`의 씬·프리팹에 연결된 검색 결과는 0건 | 현재 연결 대상에서 제외한다. 전역 런타임 사용 불가나 코드 삭제 근거로 확대하지 않는다. |

## 3. 커서 문제의 정적 근거와 한계

9개 입력란 모두 직렬화된 `m_OnFocusSelectAll: 1`, `m_ResetOnDeActivation: 1`을 가진다. `m_OnSubmit`·`m_OnEndEdit`의 persistent calls는 모두 빈 목록이다.

| 씬 | 전체 선택·비활성화 설정 줄 | 완료·편집 종료 이벤트 블록 시작 줄 |
|---|---|---|
| Login | 2229/2230, 5565/5566, 6122/6123, 6373/6374, 6709/6710, 14274/14275, 14473/14474 | 2196/2199, 5532/5535, 6089/6092, 6340/6343, 6676/6679, 14241/14244, 14440/14443 |
| Lobby | 1020/1021, 12494/12495 | 987/990, 12461/12464 |

패키지 근거는 `Library/PackageCache/com.unity.ugui@1890c14c96ff/Runtime/TMP/TMP_InputField.cs`다. 설치 소스를 읽었으며 수정하지 않는다.

- `OnFocus()`(1344행)는 `m_OnFocusSelectAll`이 켜져 있으면 `SelectAll()`을 호출한다. `SelectAll()`은 텍스트 양 끝을 선택 범위로 만든다.
- `OnPointerDown()`의 2014행 분기는 이미 포커스가 있거나 자동 전체 선택이 꺼져 있을 때 클릭 위치로 커서를 배치한다. 따라서 재진입 첫 클릭과 기존 편집 중 클릭은 다르게 처리될 수 있다.
- `ActivateInputFieldInternal()`(4324행 이후)은 소프트 키보드 경로와 일반 경로에서 `OnFocus()`를 호출한다. 4349행 이후에는 내부 선택 범위를 소프트 키보드 선택에도 반영한다.
- `DeactivateInputField()`(4414행)는 선택 해제와 키보드 종료 등을 처리한다. `m_ResetOnDeActivation`은 선택 해제 조건이며, 이 값이 1이라는 사실만으로 입력 텍스트가 지워진다고 볼 수 없다. 취소 후 원문 복원 분기는 별도다.

따라서 **재선택 시 자동 전체 선택이 기존 글자를 덮어쓰게 만드는 후보**라는 정적 근거는 있다. 사용자 기기의 현상을 직접 재현하지 않았으므로 전체 선택, 실제 값 초기화, 커서 위치 변경 중 어느 것이 관측된 직접 원인인지는 미확정이다. Inspector 직렬화 값이 코드 기본값보다 우선하므로 코드 기본값만 바꿔 적용됐다고 판단해서도 안 된다.

씬 이벤트의 빈 목록은 Inspector 연결의 부재를 뜻한다. 런타임 이벤트가 전혀 없다는 뜻은 아니다. 조사한 6개 실제 입력 View의 현재 코드는 버튼·일부 값 변경 구독을 사용하며 입력 완료·편집 종료 구독을 등록하는 코드는 확인되지 않았다.

## 4. 화면별 기존 처리와 보존할 동작

| 담당 파일 | 현재 확인된 동작 | 계획에서 보완할 부분 |
|---|---|---|
| `EmailLoginView.cs:118` 이후 | 실행 시 이메일 앞뒤 공백 정리, 이메일·비밀번호 빈 값 검사, 요청 중 버튼·입력 잠금, 성공·미인증·실패 분기 | 로그인용 기존 검증을 유지하며 입력 변경·완료 제출·오류 표시·화면 종료 처리를 공통 기준과 연결 |
| `SignUpView.cs:102` 이후 | 이메일 빈 값, 비밀번호 빈 값·최소 길이, 확인 일치 검사 및 요청 중 잠금 | 현재 최소 6자 기준을 유지. 비밀번호 문자열을 임의로 잘라내거나 정리하지 않음 |
| `PasswordResetView.cs:85` 이후 | 이메일 빈 값 검사·요청 중 잠금, 통신 오류 분기, 존재 여부를 드러내지 않는 안내·지연 복귀 | 완료 입력 중복 실행 방지와 화면 종료/지연 복귀의 생명주기 검토 |
| `NicknameSetupView.cs:98,118` 이후 | 값 변경 시 기존 닉네임 검증으로 확인 버튼 갱신. `PrepareForShow()`(139행)는 새 화면 준비 시 입력 초기화 | 실시간 버튼 검증은 이미 있다. 새 화면 준비와 일시 포커스 이탈을 구분하고 한글 조합 중 검증 타이밍 보완 |
| `NicknameChangePopup.cs:113,143,270` 이후 | 값 변경 구독, 열 때 현재 닉네임 채움, 기존 검증으로 확인 버튼 갱신, 요청 중 입력 잠금 | 기존 무료/유료 분기와 초기값 유지. 재선택 편집·명시적 닫기 포커스 정리 연결 |
| `CustomJoinView.cs:65` 이후 | `IsConnecting`에 따른 참가 버튼 잠금, 버튼 클릭 시 앞뒤 공백 정리·빈 코드 제외, CanvasGroup으로 표시 전환 | 비어 있을 때 버튼 상태, 완료 제출, 숨겨진 화면의 제출 차단·포커스 정리 보완. 코드 규격의 새 길이·문자 제한은 확정하지 않음 |

닉네임 원본은 `Assets/_Project/Scripts/Application/UseCases/PlayerProfileUseCase.cs:198`의 `ValidateNickname()`이다. 한글 완성형·영문·숫자를 허용하고 한글 포함 시 2~12자, 영문·숫자만이면 2~24자를 검사한다. 조합 중 자모를 곧바로 잘라내는 처리로 바꾸지 않는다.

표시/숨김은 CanvasGroup을 사용하는 경로가 있다. `Views/Login/LoginRootView.cs:382` 이후와 `CustomJoinView.SetVisible()`을 보면 시각적 숨김이 GameObject 비활성화와 같지 않으므로 OnDisable만으로 입력 정리를 맡길 수 없다. 비밀번호를 지울 경계는 실제 화면 전환·닫기와 연결해야 한다.

## 5. 관련 원본과 요구의 출처

- `Assets/_Project/Docs/GameSystemRules.md`: 도메인 원본을 찾는 인덱스.
- `GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 1~4: 기존 반응형·Safe Area 유지. 규칙 5: CanvasGroup 표시/숨김 및 오버레이 책임 유지. 커서·Tab 동작의 직접 근거는 아니다.
- `GameSystemRules/GameSystemRules_SettingsUI.md` 「로비 설정/프로필 UI」 규칙 1: ProfilePanel·SettingPanel 탭 분리 보존. 입력 개선을 이유로 탭 구조를 합치지 않는다.
- `AuthSystemRules.md` 「이메일 로그인」 규칙 1~3, 「이메일 회원가입」 규칙 1~4, 「비밀번호 재설정」 규칙 1~2, 「오류 처리」 규칙 1~3, 「인증 대기 취소와 재진입」: 기존 인증 조건·계정 존재 비노출·네트워크 오류·화면 이동 계약 보존.
- `UIGuidelines.md`: 기존 시각·레이아웃 가이드 참고. 이번 작업이 새로운 레이아웃 규칙의 승인은 아니다.
- `CLAUDE.md` 및 `WORKFLOW.md` [4]·[5]: 문서 승인과 구현 승인을 구분하고 구현은 game-programmer에게 위임. 기존 로직은 검증 전 비활성화 우선.

## 6. 미확정·위험

1. Android 키보드·TMP 편집 방식에 따른 첫 터치 커서 동작과 IME 확정 Enter의 실제 이벤트 순서는 아직 관측하지 않았다.
2. 기존 입력 잠금은 존재하지만 키보드 제출 경로가 늘면 버튼 비활성화만으로 충분한지 다시 검토해야 한다. 숨겨진 화면과 이전 비동기 요청의 완료 콜백도 함께 살펴야 한다.
3. 자동 전체 선택과 비활성화 설정은 별개의 책임이다. 두 값을 일괄 변경하는 방식은 아직 확정하지 않았다.
4. 구현 전에는 공통 `Presentation/UI/InputFormController.cs`의 런타임 연결 방향만 수용된 상태였다. 현재 소스 적용 내용은 8절을 따른다. 기기 동작의 미확인 경계와 인증 실패를 임의의 특정 필드 오류로 표시하지 않는 기준은 유지한다.
5. 현재 씬에 미배선된 계정 연동 입력은 공통 적용 누락이 아니라 별도 연결 문제다. 이번 범위에서 입력란을 새로 만들지 않는다.

## 7. 이번 실행·산출물 경계

이 문서의 최초 조사 근거는 로컬 문서·코드·씬·설치 TMP 소스의 읽기와 정적 집계다. 최종 동기화는 현재 8개 C# 소스와 helper meta를 직접 읽고 메인·구현 담당자의 결과 인계 및 아래 컴파일 산출물을 대조했다. 실행 절차와 범위는 같은 폴더의 [Plan.md](Plan.md)를 참조한다.

문서 정합성 검사 결과는 Plan 마지막 절에 기록한다. `Tools/check_docs.py`는 `_Tasks/`를 의도적으로 제외하므로 검사 결과 0건이어도 이 두 문서의 내용 정확성·구현 성공을 보증하지 않는다. 문서 작성자 검토와 메인 세션의 독립 원본 대조를 별도로 구분한다.

## 8. 최종 구현·검사 인계

### 현재 소스 동작

- `InputFormController.Attach()`를 기존 6개 View의 Initialize/Bind에서 사용하여 씬에 이미 연결된 9개 입력란을 런타임에 연결한다. 각 인스턴스에 `onFocusSelectAll=false`, `SingleLine`을 적용한다. `resetOnDeActivation`은 변경하지 않았으며, 씬·패키지 원본을 고치지 않았다. 자동 전체 선택을 해제하되 TMP의 클릭·드래그·수동 선택을 별도로 대체하지 않는다. 실제 기기의 커서·선택 결과는 미확인이다.
- New Input System의 `Keyboard.onIMECompositionChange`를 구독하고 조합 이벤트 프레임과 다음 프레임에서 제출·Tab 이동을 막는다. 구 Input API 조회는 추가하지 않았다. TMP submit은 다음 프레임에 다음 필드로 이동하거나 마지막 실행 버튼의 기존 onClick을 호출한다. Tab·Shift+Tab은 현재 입력 배열에서 이동 가능한 필드를 순환한다. 편집 종료만으로 요청하지 않는다.
- 필수값, 표시 상태, 처리 중 여부와 기존 닉네임 검증을 결합하여 실행 버튼을 갱신한다. 선택·오류는 기존 ColorBlock 기반 ColorTint로 구분하고, 수정된 필드의 오류를 해제한다. 회원가입 비밀번호 길이·확인 일치 오류는 해당 필드에 표시하며 닉네임 안내는 조합 종료 보호 후 갱신한다. 실제 9개 필드의 transition=1은 구현 담당자 인계이며 기기 시각 확인과 구분한다.
- 기존 이메일 Trim·비밀번호 원문·닉네임 검증·방 참가 명령을 유지한다. 요청 중 busy와 화면 Session 식별자로 중복 실행 및 이전 표시 회차의 결과 반영을 방어한다. 이는 화면 응답 보호이며 이미 시작한 서버 요청 자체를 취소하는 기능은 아니다.
- `LoginRootView.ShowGroup/HideGroup`, `NicknameChangePopup.Show/Hide`, `CustomJoinView.SetVisible/Unbind`에 Open/Close를 연결했다. 요청 중 로그인 화면 Android 뒤로가기도 차단한다. Close는 포커스·키보드를 해제하고 비밀번호 필드만 비운다. 비밀번호 재설정 화면을 닫으면 예약된 지연 복귀를 취소한다. 같은 화면에서 단순 포커스 이탈은 Close와 구분한다.
- 중복된 기존 버튼 직접 활성화·닉네임 값 변경 구독·방 참가 직접 람다 등 대체 지점은 비활성화 주석을 남겼으며 최종 삭제하지 않았다. 기존 미배선 Profile 입력 2개와 구형 LobbyUI의 제외 경계, 비밀번호 보기·키보드 가림의 후속 경계는 유지한다.

### 정확한 변경 파일 목록

모든 경로는 저장소 루트 기준이다. 소스·meta 9개와 기존 Task 문서 2개를 구분한다. 메인이 최종 소스·meta 9개 SHA256을 구현 담당자 인계와 대조하여 모두 일치했고, 최종 컴파일 뒤 소스 변경이 없다는 인계를 확인했다.

**추가 — 공통 처리 2개**

- `Assets/_Project/Scripts/Presentation/UI/InputFormController.cs`
- `Assets/_Project/Scripts/Presentation/UI/InputFormController.cs.meta`

**수정 — 기존 C# 7개**

- `Assets/_Project/Scripts/Presentation/UI/Views/Login/EmailLoginView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/SignUpView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/PasswordResetView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/NicknameChangePopup.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/CustomJoinView.cs`

**동기화 — 기존 Task 문서 2개**

- `Assets/_Project/Docs/_Tasks/2026-10-17/04_13_ui-input-field-improvements/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-10-17/04_13_ui-input-field-improvements/Plan.md`

씬·프리팹·TMP 패키지·인증 정책·도메인은 변경하지 않았으며 추가 Inspector 작업은 없다(메인 변경 범위 대조). 상시 문서·메모리·Testcase는 이번 변경 목록에 없다.

### 검사 근거와 남은 확인

| 축 | 결과·근거 | 한계 |
|---|---|---|
| 최종 Runtime Roslyn 컴파일 | 구현 담당자 exit 0·errors 0·warnings 23 인계. 메인이 로그를 재집계하고 신규 helper가 rsp에 포함됨과 최종 DLL 존재를 독립 확인. 문서 담당자도 rsp의 helper 포함·DLL 존재·경고 로그를 확인 | Unity Editor 실행 또는 Android 빌드 결과가 아님. 기존 Library DLL을 덮어쓰지 않는 별도 출력 |
| 경고 23개 | 변경하지 않은 Network의 obsolete RPC 22개, EffectManager 미사용 필드 1개. 신규 입력 코드 진단 없음(메인 로그 확인) | 프로젝트 경고 0을 의미하지 않음 |
| 변경 형식 | 메인 `git diff --check` exit 0 인계 | 동작·컴파일 성공을 이 검사만으로 증명하지 않음 |
| Unity·기기·사용자 수용 | 미실행·미확인. 빌드·QA·TC도 미실행 | 첫 터치 커서, IME 실제 이벤트 순서, 모바일 완료·뒤로가기·키보드, 상태 색상, 요청 실패·화면 재진입의 실제 동작은 사용자 확인 대기 |
| 문서 정합성 | Python alias 실행 실패로 결과 미확인. 상세는 Plan 5절 | `_Tasks/` 검사 제외 한계 유지 |

컴파일 근거 파일은 임시 산출물이므로 향후 보존이 보장되는 문서 원본과 구분한다. 위치는 다음과 같다.

- `C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-input-compile/runtime-final.log`
- `C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-input-compile/runtime.rsp`
- `C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-input-compile/Assembly-CSharp.dll`

최초 구현 인계 시점 판정은 **소스 구현 완료·정적 컴파일 확인 / 실기·사용자 수용 대기**였다. 이후 Android 사용자 결과는 아래 9절로 갱신한다. 위 최초 컴파일 결과를 새 재작업 소스의 컴파일 결과로 재사용하지 않는다.

## 9. Android 사용자 확인과 승인된 재작업

출처는 사용자의 Android 실기 보고를 전달한 메인 인계다. 이 절은 후속 수정 전 재작업 착수 당시의 기록이며, 이후 적용 결과는 10절을 따른다. 아래 1~6은 사용자가 보고한 항목 구분이며 새 Testcase나 QA 판정표가 아니다. 모두 Android에서 확인했으며 데스크톱 확인 결과로 확장하지 않는다.

| 항목 | 사용자 보고 | 현재 해석・남은 경계 |
|---|---|---|
| 1. 전체 입력란 이어쓰기 | 9개 모두 정상 | 해당 정상 수용 유지. 후속 결함 때문에 전부 실패로 되돌리지 않음 |
| 2. 키보드 이동 관련 확인 | Android에서 확인할 수 없어 보류 | 사용자 보류이며 Android에서 기술적으로 불가능하다는 확정은 아님 |
| 3. 한글 입력 | 정상 | 보고한 한글 입력 정상 수용. 모든 IME 내부 이벤트 순서까지 관측한 것은 아님 |
| 4. 로그인・회원가입 버튼 | 비활성화 후 다시 활성화되지 않아 추가 진행 불가 | 재활성화・유효성 갱신・요청 상태 경계의 원인 조사 및 최소 수정 진행 중. 원인 확정 전이며 후속 항목 전체의 실패를 뜻하지 않음 |
| 5. 화면 종료 후 재진입 | 비밀번호가 빈 값인 것은 정상 확인 | 이 동작만 수용. 포커스・키보드 등 나머지 세부는 미보고 |
| 6. 없는 방 코드 참가 | “게임 접속 중” 로딩에서 멈춰 추가 진행 불가 | 참가 실패→로딩 종료・실패 복구 경로 조사 및 최소 수정 진행 중. 서버・네트워크 전체 결함이나 원인 확정으로 확대하지 않음 |

추가 명시 요청은 비밀번호 재설정 입력란의 기본값을 이메일 입력 용도에 맞게 정리하고, 보이는 커서와 손가락으로 위치를 조정하는 편집을 제공하는 것이다. 구체적인 기본 설정값과 커서 구현 결과는 아직 인계되지 않았다.

game-programmer가 diagnose 절차로 버튼 및 방 참가 실패 복구 경로를 조사・최소 수정 중이다(메인 인계). 커서는 Android 네이티브 IME와 TMP 인플레이스 선택 지원을 소스로 확인 중이며 커스텀 핸들 필요성・구현 방식은 미정이다. 커서 색상 설정만으로 위치 조정 편집까지 해결됐다고 기술하지 않는다.

승인 범위는 보고된 버튼 복구, 잘못된 방 코드 참가 실패 후 로딩 종료・복구, 재설정 입력 기본값, 커서 표시・손가락 위치 편집이다. 기존 인증・root 정책 변경, 서버・네트워크 전면 리팩토링, 키보드 가림・스크롤 후속 작업은 제외한다. 정상 수용된 입력 동작은 유지한다.

현재 판정은 **Android 일부 정상 수용 / 진행을 막는 두 현상과 추가 입력 요구 재작업 중**이다. 재작업 원인・실제 수정 파일・새 컴파일・기기 재확인 결과는 메인의 후속 인계 전까지 미확인이다. 이번 문서 갱신에서는 QA테스터・TC・Unity・빌드를 실행하지 않았다.

## 10. 2026-10-18 후속 구현과 검사 인계

9절은 재작업 착수 당시의 상태다. 아래는 이후 메인 인계와 해당 소스・로그를 대조한 결과이며, 수정 후 Android 수용을 뜻하지 않는다. 사용자는 이메일과 비밀번호를 모두 입력하고 키보드를 닫아 확인했어도 버튼이 비활성 상태였다고 보충했다. 이메일은 영문・숫자였고 비밀번호의 한글 여부는 기억하지 못한다고 했다. 비밀번호는 한글・영문 등 문자 종류와 무관하게 입력하고 원문을 보존하는 요구이며, 이번 수정은 문자 종류 필터를 추가하지 않았다. 기존 회원가입 최소 6자와 확인 일치 검증은 유지한다.

### 변경 파일과 실제 적용 내용

후속 변경은 아래 기존 파일 3개다. 최초 구현 8절의 파일 목록과 구분하며 새 소스・meta・Testcase는 생성하지 않았다.

| 파일 | 후속 변경 |
|---|---|
| `Assets/_Project/Scripts/Presentation/UI/InputFormController.cs` | 전역 IME 조합 이벤트는 현재 편집 중인 폼에서만 수용한다. 모바일 키보드 종료, 필드 포커스 이탈, 화면 Close에서 남은 조합 상태를 해제하고 기존 조합 확정 Enter의 두 프레임 보호는 유지한다. 런타임 연결된 9개 필드에 `shouldHideMobileInput=true`, 커서 폭 3, 사용자 지정 파랑 `(0.1, 0.45, 1, 1)`을 적용한다. 소프트 키보드는 유지하고 TMP 인플레이스 커서・터치 위치 편집・기존 터치 드래그를 사용한다. 별도 네이티브 커서 손잡이를 추가하지 않았다. |
| `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` | 기존 참가 await를 try/catch로 감싸 전파된 예외도 기존 `OnNetworkError(e.Message)` 상태 경로를 거치게 한다. await 이후 연결 중이 아니며 오류 메시지가 있는 실패에서만 로딩을 닫고 `참가 실패` / `방에 참가하지 못했습니다. 방 코드와 방 상태를 확인해주세요.`를 표시한다. 성공 후 맵 준비 로딩은 유지하며 `OnNetworkError` 자체 본문은 변경하지 않았다. |
| `Assets/_Project/Scenes/Login.unity` | 비밀번호 재설정 placeholder 컴포넌트 fileID `1890503128`의 안내 문구 한 줄만 `이메일 주소를 입력하세요`로 교정했다. 실제 입력값의 빈 문자열은 유지한다. |

버튼 잠김은 조합 종료 이벤트가 누락될 때 공통 처리의 조합 상태가 남는 소스 경로를 재현하여 교정했다. 이는 사용자 Android에서 실제 어떤 IME 이벤트가 누락됐는지 확정한 결과는 아니다. 없는 방 코드는 하위 오류 통지 후 정상 반환할 수 있어 예외 처리만으로 로딩이 닫히지 않던 경로를 확인했다. 두 경로의 수정과 제한된 검사 통과를 Android 근본 원인 확정・실기 해결로 확대하지 않는다.

### 검사와 증거 경계

| 축 | 확인 결과 | 한계 |
|---|---|---|
| 소스 대조 | 메인이 후속 3개 파일 SHA256 일치 및 최종 컴파일 후 소스 무변경을 확인. 문서 담당자도 helper・ViewModel 분기・씬 placeholder를 직접 확인 | 해시 일치가 동작 수용을 뜻하지 않음 |
| 최종 Runtime 정적 컴파일 | 메인 최종 로그・산출물 확인: errors 0, warnings 23. helper가 응답 파일에 명시적으로 포함되고 임시 DLL로 출력됨 | Unity 실행・Android 빌드 결과가 아님. 기존 Library DLL 교체 없음 |
| 대체환경 회귀 루프 | `probe-green.log`의 개별 PASS 18개와 `TOTAL=18` 직접 확인. 실제 helper 소스 및 네트워크 관련 메서드 추출본을 대체 객체와 결합한 검사 | Android 실기 재현이나 실제 서버・네트워크 통합 검사가 아님 |
| 변경 형식・문구 | 메인 `git diff --check` exit 0, 씬 문구 JSON 해석 일치 확인 | 실제 화면 가독성・커서 조작 증거가 아님 |
| Android 재확인 | 후속 수정본에서는 미실행・미수용 | 기존 9개 이어쓰기・한글・비밀번호 빈값 수용은 유지. 2번 이동은 사용자 보류, 4・6번 진행 차단은 수정 후 실기 확인 대기 |
| 문서 정합성 | 기존 Python alias 프로세스 시작 실패 한계 유지. 이번에는 실행환경 탐색・검사 재시도 없음 | 진단 0건이나 검사기 PASS를 주장하지 않음. `_Tasks/`는 검사 제외이므로 문서 내용은 직접 대조 |

18개 검사는 ASCII 준비, 조합 중 차단・미제출, 키보드 종료・포커스 이탈 복구, 한글 비밀번호 준비, Close 비밀번호 삭제・재열기 복구, busy 차단・복구, 다른 폼 IME 무시, 커서 설정, 숨김 차단・다시 표시 복구, 없는 방・예외 실패 복구, 정상 성공 로딩 유지, ViewModel 전파 예외 복구다. 버튼 준비 검사와 문자 종류 필터 없음은 실제 인증 서버가 모든 비밀번호를 수용했다는 뜻이 아니다.

후속 임시 검사 산출물은 아래 폴더에 있으며 영구 보존을 보장하는 원본 문서와 구분한다.

`C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-input-followup/`

- `probe-green.log`, `probe.cs`, `extracted.cs`
- `runtime-final.log`, `runtime.rsp`, `Assembly-CSharp.dll`

메인 대조 인계 SHA256:

- helper: `D7FDD11BA8303C1844AD8117CD8DB0CD8C6C009424F3465DD74CF3CB7B72CF03`
- ViewModel: `E594F02F1C2611449B63702559BBEA7A66DECAF70D74BC1B46C2056ADC7D4229`
- Login 씬: `D15138B61F5D4C3DE54CFDBC4A5CDCA13CF26C33B9E0EF80F269BFEEF706ECA8`

현재 판정은 **후속 소스 수정・정적 컴파일・대체환경 회귀 확인 / Android 수정본 재확인 대기**다. 별도 Inspector 작업은 없으며 변경된 씬과 코드가 들어간 새 빌드에서 버튼 복구, 실패 로딩 종료, 재설정 문구, 커서 표시・손가락 편집을 확인해야 한다. 이번 문서 동기화에서 Unity・빌드・QA테스터・TC・Git 변경・상시 문서・메모리 변경은 수행하지 않았다.

## 11. 후속 Android 결과와 다음 작업 승인

이 절은 10절 이후 사용자 보고와 다음 작업 진행 지시를 반영한다. 앞 절의 당시 미확인 기록은 보존하며, 여기의 사용자 확인을 코드 원인 확정・실제 기기 로그 관측・전체 네트워크 정상으로 확대하지 않는다.

| 확인 항목 | 최신 사용자 보고・수용 범위 | 남은 경계 |
|---|---|---|
| 필수값과 버튼 | 빈 값・입력 완료・삭제 후 재입력의 활성화 전환 확인 | 키보드를 내려야 활성화되는 불편은 남음. 키보드를 연 채 입력 완료 시 활성화하도록 신규 요청 |
| 비밀번호 | 최소 길이・일치・불일치 안내 확인 | 현재 클라이언트 검증은 최소 6자와 확인 일치이며 조합 강제 없음. 영문만 허용해도 되는지 질문에 대해 영문 단독 가능・문자 종류 필터 추가 없음으로 안내. 모든 서버 정책 수용으로 확대하지 않음 |
| 로그인 실패 후 재입력 | 다시 입력하고 실행 가능 | 해당 실패 복구 정상 수용 유지 |
| 닉네임 오류 | 오류 확인 후 수정하면 다시 사용 가능 | 기존 닉네임 규칙 변경 없음 |
| 없는 방 코드 | 실패 처리 확인 | 기존 실패 복구 정상 수용 유지 |
| 정상 방 코드 | 실패 때와 같은 현상으로 참가하지 못함 | 신규 진단 대상. 사용자가 같은 빌드・서로 다른 계정 사용을 확인. 정상 참가 원인은 아직 미확정 |
| 전체 입력란 커서 | 9개 모두 보이고 정상 조작 | 기존 이어쓰기・커서 수용을 유지하며 전부 재테스트를 자동 요구하지 않음 |
| 비밀번호 재설정 안내 | 문구 정상 확인 | 새 변경 필요 없음 |
| 한글 조합・화면 닫기 | 최근 안내의 의미를 이해하지 못해 확인하지 못함 | 이번 후속 수정 테스트 때 다시 쉬운 조작 순서로 안내 요청. 초기 한글 입력 정상 보고와 재진입 비밀번호 빈 값 수용은 취소하지 않음 |
| Tab・Shift+Tab | 기존 Android 보류 유지 | 사용자 보류를 기술적 불가능으로 단정하지 않음 |

### 이번에 추가 승인된 범위와 조사 상태

1. **정상 방 코드 참가 실패:** 같은 빌드・다른 계정 조건을 확보했다. 참가 요청부터 Lobby/Relay/연결/준비/실패 표시까지 단계 근거를 조사한 뒤 확인된 원인에 맞춰 수정한다. 정상 방이 실제 어느 단계에서 실패했는지는 아직 인계되지 않았으며, 방 코드 오입력・서버 원인・계정 문제를 임의로 확정하지 않는다.
2. **전체 9개 키보드 가림 대응:** 선택한 입력란을 키보드 위의 가용 영역에서 편집할 수 있도록 이동・복원 처리를 추가하는 범위다. 공통 UI 반응형・Safe Area・표시/숨김 책임을 유지한다. 구현 담당자는 키보드 영역 측정, Android 가시 영역 보완, 적합한 부모의 이동・복원을 검토 중이며 새 helper・meta를 예정했다. 확정된 코드・파일 생성・기기 적용 결과는 아직 없다.
3. **키보드를 연 채 버튼 활성화:** 문서 담당자가 읽은 착수 소스에서 `InputFormController.CanSubmit`은 필수값・가시성・busy뿐 아니라 `!IsComposing`을 요구한다. 준비 상태와 IME 확정 Enter의 제출 보호를 분리하는 방향을 구현 담당자가 진행 중이다. 이 정적 결합은 조사 근거이며 사용 기기 이벤트 원인 전체를 확정하지 않는다. 조합 확정 Enter의 오제출 방지・요청 중 잠금・실패 후 재입력은 유지해야 한다.

이번에는 상시 규칙・메모리・인증 정책・서버 프로토콜 변경, 비밀번호 보기 버튼, 일반 UI 전면 개편으로 확대하지 않는다. 사용자 “다음 작업 진행” 승인을 기존 Task의 후속 범위 승인으로 기록하며, 두 Task 문서만 동기화한다. 현재 상태는 **사용자 일부 정상 수용 / 확장 구현 진행 중 / 확장 소스・검사・Android 수정본 미확인**이다. 10절의 errors 0・대체환경 18개 PASS를 이번 확장 코드 검증으로 재사용하지 않는다.

<a id="12-확장-ui-구현검사-인계와-정상-방-참가-미해결"></a>

## 12. 확장 UI 구현・검사 인계와 정상 방 참가 미해결

11절의 착수 상태 이후 메인 최종 인계를 반영했다. 문서 담당자도 두 helper, 6개 View 진입 guard, 검사 로그・응답 파일・manifest를 직접 대조했다. 현재 판정은 **키보드 관련 소스 구현・정적/대체환경 검사 완료 / Android 수정본 확인 대기 / 정상 방 참가 원인 미확정・참가 기기 로그 대기**다. 전체 작업 완료가 아니다.

### 실제 UI 변경 파일과 동작

이번 확장 UI 변경 목록은 다음 9개다. 이전 8・10절의 단계별 목록과 구분하며, 기존 `InputFormController.cs.meta`, `BattleViewModel.cs`, Login 씬은 이번 UI 확장 수정 목록에 포함하지 않는다.

**추가 2개**

- `Assets/_Project/Scripts/Presentation/UI/MobileKeyboardAvoidance.cs`
- `Assets/_Project/Scripts/Presentation/UI/MobileKeyboardAvoidance.cs.meta`

**수정 7개**

- `Assets/_Project/Scripts/Presentation/UI/InputFormController.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/EmailLoginView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/SignUpView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/PasswordResetView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/NicknameChangePopup.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/CustomJoinView.cs`

**이번 동기화 문서 2개**는 이 폴더의 `Research.md`, `Plan.md`다. 씬・Manifest・플러그인・비밀번호 정책 변경은 없으며 별도 Inspector 작업이 필요하지 않다(메인 인계).

- **열린 키보드에서 활성화:** `CanSubmit`에서 IME 조합 조건을 분리했다. 가시성・필수값・busy・화면별 기존 검증에 따라 버튼을 활성화하고, Enter/다음 입력에는 조합 확정 보호를 유지한다. 기존 조건은 비활성화 주석으로 남긴다.
- **한 번 탭 제출과 마지막 글자 보존:** 6개 View의 요청 직전 guard를 `PrepareSubmit()`으로 연결했다. Android native 입력의 마지막 글자 동기화는 문자 검증 None・사용자 검증 콜백 없음인 필드에만 적용하고 기존 길이 제한은 유지한다. 버튼 누름부터 놓음까지 이동을 고정하며 동일 버튼 제스처의 문자만 보존한다. LostFocus에서는 해당 누름 직전 snapshot만 사용하고, 취소・일반 포커스 이탈・만료・다른 표시 회차의 문자열을 복구하지 않는다. 앱 포커스 콜백은 제스처 cache를 정리하며 현재 편집값을 임의로 지우지 않는다.
- **가림 이동・원상복원:** owner RectTransform에 자기 이동량만 적용・제거한다. Android 가시 frame・화면 위치・view 높이, SDK 30 이상 IME 표시 상태로 하단 가림을 측정하고 adjustResize에 의한 중복 이동을 피한다. native 측정 실패 시 실측 `TouchScreenKeyboard.area`를 사용하며 높이를 고정 추정하지 않는다. 여유가 있으면 입력란과 실행 버튼을 함께 보이고, 부족하면 입력란을 우선한다. 새 스크롤・화면 축소는 추가하지 않는다. 실제 씬에서 팝업/레이아웃 작성자와 이동 대상의 경합이 없음을 메인이 확인했다.

### 새 검사 근거와 한계

임시 근거 폴더는 아래와 같다. 영구 보존을 보장하는 원본 문서와 구분한다.

`C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-keyboard-followup/`

| 축 | 이번 확인 | 한계 |
|---|---|---|
| 소스 manifest | `manifest.tsv` 9개 SHA256 전부 현 파일과 일치. 메인 확인 후 문서 담당자도 재대조 | 소스 동일성이지 실제 기기 동작 수용이 아님 |
| Runtime 정적 컴파일 | `runtime.log` errors 0・warnings 23 재집계, 신규 helper 2개 명시 포함과 `Assembly-CSharp.dll` 출력 확인 | Unity 실행・빌드 완료 증거 아님 |
| Android 조건 정적 컴파일 | `android.log` errors 0・warnings 27 재집계. `android.rsp`는 UNITY_ANDROID 포함・UNITY_EDITOR 제외, helper 2개 포함, `Assembly-CSharp-android.dll` 존재 | Android 조건 분기 컴파일이며 APK/AAB 빌드・JNI 실기 실행 아님 |
| 기존 경고 | Runtime 23개는 기존 obsolete RPC・EffectManager, Android 추가 4개는 기존 Unit/Combat의 미사용 변수・필드. 신규 입력 코드 진단 없음 | 프로젝트 경고 0을 뜻하지 않음 |
| 대체환경 검사 | `probe.log` 개별 PASS 34개와 TOTAL=34 직접 확인. 기하 경계 1000건은 하나의 검사항목. `probe-red.log`에서 이전 IME 결합 조건 복원 시 READY_WHILE_KEYBOARD_COMPOSING FAIL 확인 | 대체 객체 검사이며 실제 TMP 렌더・native JNI・Android 손가락・성능 확인은 아님 |
| 실기 | 이번 수정본 미확인 | 기기별 키보드・화면 크기에 따른 결과 필요. 작은 가용 화면에서 모든 버튼・오류 안내까지 항상 보임을 보장하지 않음 |
| 문서 검사 | 기존 Python alias 실행 실패 한계 유지・재탐색 없음 | 검사기 PASS・진단 0건 주장 없음. `_Tasks/` 검사 제외 한계 유지 |

### 정상 방 코드 참가 조사와 필요한 근거

별도 담당자 조사 인계에 따르면 정상 방 코드 참가 실패의 원인은 아직 확정되지 않았고, 이 이슈를 해결했다고 할 관련 코드는 수정하지 않았다. 같은 빌드・다른 계정 조건은 사용자 확인이다. 현재 CustomJoin 입력은 앞뒤 공백만 정리하고 길이 제한은 0이며, Host는 Relay 데이터 포함 Lobby 생성과 StartHost 이후 코드를 표시한다. 랜덤 매칭의 별도 지연과 정상 커스텀 방 표시를 혼동하지 않는다.

담당자가 살펴본 10월 18일 Editor 로그에서는 Host 시작 1건 외 참가 요청・Lobby 실패・Relay 실패・StartClient・맵 준비 근거가 없었다는 인계다. 이는 Android 참가 실패를 관측한 로그가 아니므로 실패 단계를 특정할 수 없다. 기존 운영 로그는 `LobbyServiceCallFailed`, `RelaySetupFailed`, `NetworkSessionStartFailed`, `GameSessionStartUnhandledException` 등의 단계 예외를 보존한다.

**필요한 다음 근거:** 참가하는 Android에서 정상 코드 실패 직후의 로그, 가능하면 같은 시도 Host 로그도 함께 확보한다. USB 연결 후 기존 Unity 메뉴 `Hexiege > Logs > Save Device Logs`로 저장한다. 출력 위치는 `Assets/_Project/Docs/_Logs/{날짜}/{시각}_logcat/RuntimeLog_device.txt`이며 기존 로그를 지울 필요는 없다. 메뉴・경로는 메인과 문서 담당자가 `Assets/Editor/Tools/LogcatCapture.cs`에서 직접 확인했다. 이번 문서 작업은 로그 수집 자체를 실행하지 않았다.

### 다음 사용자 확인에 함께 안내할 사항

1. 키보드를 열고 필수값을 모두 입력했을 때 버튼이 활성화되는지, 키보드를 직접 닫지 않고 버튼 한 번 탭으로 요청되는지 확인한다. 마지막 글자가 빠지지 않는지도 본다.
2. 기존 9개 입력란에서 선택 필드가 키보드에 가려지지 않는지, 필드 전환 때 이동이 맞는지, 키보드 종료・화면 닫기 후 원래 배치로 돌아오는지 확인한다. 기존 커서・이어쓰기 수용은 유지하되 이번 이동 처리 영향만 함께 본다.
3. 닉네임에 `가나다`를 입력하고 중간 글자를 고쳐 깨짐・중복・누락이 없는지 확인한다. 키보드가 글자 확정용 Enter를 제공한다면 그 확정만으로 저장되지 않는지도 본다. 버튼으로 제출할 때 마지막 한글이 보존되는지도 확인한다.
4. 입력 중 화면의 뒤로가기・닫기로 이전 화면에 돌아가면 키보드・커서가 사라지고 이동한 화면이 원위치인지 확인한다. Android 기기 뒤로가기가 먼저 키보드만 닫는 경우 화면까지 닫힌 것과 구분한다. 재진입 시 비밀번호 빈 값은 기존 정상 수용을 유지한다.
5. 정상 방 코드 참가 실패를 재현하면 참가 기기 실패 직후 로그를 제공한다. 원인이 확정되어 수정되기 전까지 정상 참가 해결 완료로 표시하지 않는다. Tab・Shift+Tab은 기존 보류를 유지한다.

## 13. 새 Android 확인과 첫 탭 재작업

12절 이후 사용자 실기 결과를 반영한다. 새 결과는 다음과 같으며, 12절의 대체환경 34개 PASS가 아래 첫 탭 실패를 덮지 않는다.

| 항목 | 최신 사용자 확인 | 판정 범위 |
|---|---|---|
| 키보드를 연 상태의 준비 | 필수값 입력・삭제・다시 입력에 따라 버튼 활성화 확인 | 해당 동작 정상 수용 |
| 로그인 버튼 첫 탭 | 처음에는 키보드만 내려가고 두 번째 탭에 로그인 | 한 번 탭 제출은 미충족・재작업 중 |
| 회원가입 오류 안내 | 이전 길이・일치 검증 정상 수용 유지 | 이번 재확인을 추가 요구하지 않음 |
| 한글 중간 편집 | `가나다` 중간 글자 편집 정상 | 해당 편집 수용. 모든 IME 이벤트의 기술 원인 확정은 아님 |
| 화면의 뒤로가기 버튼 | 첫 탭에는 키보드만 닫힘. 다시 누르면 이전 화면 이동 | 화면 버튼 한 번 탭 닫기는 미충족・재작업 중 |
| 화면 재진입 | 비밀번호는 비어 있고 다시 입력 가능 | 기존 비밀번호 정리・재사용 수용 유지 |
| 정상 방 코드 참가 | 이번 시도에서 정상 입장 | 현재 진행 차단 해제. 이전 실패 원인 확정이나 새 네트워크 수정의 효과를 뜻하지 않음 |
| 기존 커서・이어쓰기 | 기존 수용 유지 | 새 실패 때문에 전부 미확인으로 되돌리지 않음 |
| 9개 키보드 가림・복원 전수 | 전체 대상 결과는 미보고 | 전수 PASS로 기록하지 않음 |

사용자는 **키보드가 이미 올라와 있으면 새로 열지 않기**를 추가 요청했고, 발생 상황은 **다른 입력란으로 옮길 때**라고 확인했다. 같은 입력란 재선택만의 문제로 좁히지 않는다. 필드 전환 중 키보드 유지는 별도 최소 설계를 검토 중이며 적용 완료는 아니다.

구현 담당자는 첫 탭 소비, 제출 버튼에 한정된 제스처 처리와 일반 뒤로가기 버튼, 앱 포커스・키보드 전환, TMP 재열기 경로를 조사・수정 중이다(메인 인계). 실제 수정 파일・완료 코드・새 검사 결과는 아직 인계되지 않았다. 이전 정적 검사와 대체환경 PASS를 이 재작업 검증으로 재사용하지 않는다.

정상 방 참가에는 이번 성공 보고가 있으므로 추가 네트워크 수정・로그 수집을 현재 필수 후속으로 요구하지 않는다. 12절의 당시 로그 대기 기록은 보존하되 현재 요구는 이 절로 갱신한다. 전체 상태는 **준비 상태・한글 편집・정상 방 참가 수용 / 로그인・화면 닫기 첫 탭 실패 재작업 / 입력란 전환 시 키보드 유지 설계 검토 / 가림 전수 결과 미보고**다. 전체 완료가 아니며, 문서 변경은 기존 두 파일에 한정했다.

<a id="14-첫-탭-재작업-최종-소스와-검증-경계"></a>

## 14. 첫 탭 재작업 최종 소스와 검증 경계

13절 이후 메인 최종 인계와 현 소스・임시 검사 근거를 대조했다. **첫 탭 문제의 소스 수정・정적/대체환경 검사 확인 / Android 수정본 수용 대기 / 필드 전환 시 키보드 유지 미적용**이다. 로그인의 실제 기기 원인이 기술적으로 확정됐거나 전체 작업이 완료됐다고 판정하지 않는다.

### 이번 변경 4개와 적용 내용

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Presentation/UI/InputFormController.cs` | 폼 안 Button들에 실제 포인터 전달자 연결, 포커스 정리 지연, Android 외부 터치 소비 설정의 표시 수명 관리, 입력값 없는 개발 진단 |
| `Assets/_Project/Scripts/Presentation/UI/MobileKeyboardAvoidance.cs` | 제출 버튼만이 아니라 일반 폼 버튼의 실제 PointerDown/Up 회차로 이동・복원 보호. release raycast 판정까지 위치 유지 |
| `Assets/_Project/Scripts/Presentation/UI/InputFormPointerRelay.cs` | 신규. 기존 Button 클릭 실행은 그대로 두고 IPointerDown/Up만 폼으로 전달 |
| `Assets/_Project/Scripts/Presentation/UI/InputFormPointerRelay.cs.meta` | 신규 relay meta |

`Pointer.current` 위치 사각형 추정 대신 실제 포인터 이벤트를 사용한다. 뒤로가기 버튼에서도 TMP 선택 해제 후 배치 이동으로 손을 뗄 때의 raycast가 달라지던 경로를 보호한다. 앱 포커스 알림에서 즉시 배치를 복원하지 않고 같은 프레임 실제 누름 확인까지 정리를 미룬다. 실패한 클릭을 임의로 재호출하거나 반복 클릭 fallback으로 실행하지 않는다.

Android에서는 표시 중인 폼이 공유 참조 수를 관리하며 `TouchScreenKeyboard.Android.consumesOutsideTouches=false`를 임시 적용한다. 마지막 폼 종료・pause・Destroy 때 처음 획득 직전에 읽은 값을 복원하고 기본값을 추측하지 않는다. 이는 키보드 외부 첫 탭 소비 후보에 대한 대응이며 실제 native 초기값・사용자 기기 설정값은 미관측이다. 설치 모듈 IL에서 `closeKeyboardOnOutsideTap`의 getter/setter는 이 설정으로 완전히 위임되므로 별도 설정으로 조작하지 않으며 독립적인 선행 닫기 방지로 설명하지 않는다.

새 진단은 `GameLog.Dev` 조건부이며 입력값을 포함하지 않는다. 씬・TMP 패키지・플러그인・Manifest・비밀번호 정책 변경, Unity 실행・APK 빌드・native 실기 검증은 이번 수정에서 수행하지 않았다. Inspector 작업은 없다(메인 인계).

### 새 검사와 원인 판단 한계

임시 근거는 아래 폴더의 `manifest.tsv`, `runtime.log`, `android.log`, 두 응답 파일, `green.log`, `red.log`, `input-module-extracted.cs`, `android-keyboard-api-il.txt`다. 영구 보존 보장과 구분한다.

`C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-keyboard-first-tap/`

| 축 | 확인 | 한계 |
|---|---|---|
| 소스 동일성 | manifest 4개 SHA256과 현 파일 모두 일치. 메인 확인 후 문서 담당자도 직접 대조 | 소스 일치가 실기 수용은 아님 |
| Runtime 정적 컴파일 | errors 0・warnings 23, helper 3개 명시 포함 | Unity 실행・빌드 아님 |
| Android 조건 정적 컴파일 | UNITY_ANDROID 포함・UNITY_EDITOR 제외, helper 3개 포함, errors 0・warnings 30 | 기존 27개에 consumesOutsideTouches obsolete 진단 3개 추가. 경고 0이 아니며 native 실행 증거도 아님 |
| 실제 입력 모듈 순서 대역 | 설치 `InputSystemUIInputModule.ProcessPointerButton` 본문 발췌와 실제 helper/relay를 결합한 Green CHECKS=84・FAILURES=0 직접 확인 | TMP/native/raycast는 대역. 실제 기기 첫 탭 수용과 별개 |
| RED | 기존 제출 단독은 PASS, 뒤로가기 및 focus(false) 중 held button 위치 검사는 FAIL | 뒤로가기 배치 이동 결함 근거이며 로그인 원인 확정은 아님 |
| 변경 형식 | 메인 `git diff --check` exit 0 인계 | 동작 검증을 대신하지 않음 |
| 문서 정합성 | 기존 Python alias 실패・검사 제외 한계 유지 | 새 실행・진단 0건을 주장하지 않음 |

**남은 사용자 요구:** 발생 상황은 입력란을 옮길 때라고 확인됐지만, 이 수정은 TMP의 필드 전환 native Close/Open을 공유 키보드 구조로 바꾸지 않았다. 필드 전환 중 키보드가 닫혔다 새로 열리지 않게 하는 요구는 **미적용**이다. 같은 focused 입력란 재탭이 새 Open을 호출하지 않는 설치 TMP 소스 확인을 다른 필드 전환 해결로 확대하지 않는다.

다음 Android에서는 키보드를 연 채 로그인 버튼 한 번 탭, 화면 UI 뒤로가기 버튼 한 번 탭, 마지막 한글 보존과 정상 이동/복원을 확인한다. 입력란 전환의 키보드 유지까지 수정 완료로 안내하지 않는다. 이전 준비 상태・커서・이어쓰기・한글 편집・비밀번호 재진입 정리・정상 방 참가 수용은 유지하고, 전수 미보고인 9개 가림은 PASS로 만들지 않는다. 변경 문서는 기존 Research/Plan뿐이며 상시 문서・메모리・TC・QA・Git 변경은 수행하지 않았다.

## 15. 잔여 기능을 묶은 구현과 한 번의 통합 테스트 승인

> 이 절은 통합 구현 착수・최소 설계 검토 당시의 기록이다. 아래 진행 중・미확정 문장은 그 단계의 상태로 보존하며 현재 구현・최종 검사 결과는 16절을 따른다.

사용자는 “한번에 작업하고 한번에 테스트하자”, “비슷한 기능 테스트를 너무 여러 번 반복적으로 한다”고 요청했다. 따라서 첫 탭 수정본만 먼저 빌드・확인하도록 요청하지 않고, 입력란을 옮길 때 열린 키보드를 유지하는 잔여 요구까지 함께 진행한 뒤 사용자 통합 테스트를 한 번 요청한다. 기존 Task 두 문서의 연속 승인 범위이며 새 문서 작성 여부를 다시 묻지 않는다.

현재 구현 담당자는 설치 TMP의 실제 수명주기를 근거로 필드 전환 유지의 최소 설계・구현 가능성을 검토 중이다. 아직 새 코드・검사・manifest가 확정 인계되지 않았으므로 완료나 새 수치를 추가하지 않는다. 14절은 첫 탭 수정 시점의 증거로 보존하며 향후 통합본의 검증으로 자동 재사용하지 않는다.

기존 실기에서 수용된 준비 상태・커서・이어쓰기・한글 편집・비밀번호 정리・정상 방 참가 결과는 유지한다. 사용자 통합 테스트는 이번 변경의 영향과 미확인 가림 범위를 모아서 안내하고 이미 확인한 오류 안내 등 유사 항목을 불필요하게 반복하지 않는다. 정상 방 참가의 추가 로그 요청도 현재 해제 상태를 유지한다. **현재는 잔여 구현 검토・진행 중이며 전체 완료가 아니다.**

### 필드 전환 유지의 최소 설계 인계

메인・구현 담당자의 로컬 소스 대조 인계에 따르면 Unity GameActivity `UGASoftKeyboard.cpp` 56~64행에는 필드 전환의 Hide→Show가 false keyboard visibility를 보내 비활성화하는 문제의 주석이 있다. 메인은 PlayerSettings의 `androidApplicationEntry: 2`와 `_ShowImpl` 234~287행의 secure/type/limit/text 재설정・show 및 별도 `_HideImpl`을 독립 확인했다. 이는 구현 방향의 설치 소스 근거이며 실제 기기 수용은 아니다.

승인된 최소 방향은 TMP subclass와 meta를 추가하고 현재 9개 필드의 씬 script GUID만 교체하는 것이다. 직렬화 fileID・기존 참조・UnityEvent・입력 설정을 보존한다. 같은 현재 폼의 다른 입력란으로 이동할 때만 `OnDeselect`에서 protected `mSoftKeyboard`를 분리하고 base 나머지 생명주기를 수행하여 명시적 Hide를 피한다. 대상 필드는 기존 TMP Open 경로로 키보드 종류・가림・입력 조건을 재구성한다. 목표는 화면 키보드 유지이며 managed wrapper 객체 동일성을 보장하는 설계가 아니다.

다른 화면・빈 영역・버튼・pause・Close에서는 기존 종료를 유지한다. native 공유 키보드 직접 브릿지・설치 패키지 수정・reflection은 사용하지 않는 방향이다. 공통 폼의 전환・정리는 좁게 연결하고 기존 로직을 삭제하지 않는다. wrapper 수명・마지막 native 문자・selection・대상 활성화 실패・GC 경계는 구현 담당자가 검토 중이다. **현재는 소스 기반 설계 인계・구현 진행 중이며 최종 파일・검사・manifest・native 실기 수용은 아직 확정하지 않는다.**

<a id="16-첫-탭과-필드-전환-통합-구현-인계"></a>

## 16. 첫 탭과 필드 전환 통합 구현 인계

15절의 설계 검토 이후 필드 전환 유지 소스・씬과 최종 검사 근거가 인계됐다. 14절의 “필드 전환 미적용”은 첫 탭 수정 당시 상태이며 현재 구현 상태로 재사용하지 않는다. **통합 소스・씬 구현・최종 정적/대체환경 검사 확인 / 사용자 통합 실기 확인 대기**다. 중간 실기나 유사 항목의 중복 확인을 요청하지 않는다.

### 실제 적용과 수명 경계

- `ContinuousInputField.cs`와 meta를 추가하고, 공통 `InputFormController.cs`, `MobileKeyboardAvoidance.cs`, `Login.unity`, `Lobby.unity`를 수정했다. 이번 통합 전환 단계는 이 6개 파일이며 이전 첫 탭 relay 파일은 14절의 변경 이력과 구분한다.
- Login 7개・Lobby 2개 입력란의 script GUID와 class만 교체했다. 기존 fileID・참조・UnityEvent・직렬화 설정을 유지한다. 메인이 씬 diff・9개 fileID를 대조했으며, 문서 담당자도 `scene-serialization.log`의 SERIALIZED_PAYLOAD_IDENTICAL=True와 7+2 연결을 확인했다. 앞선 placeholder 문구 수정은 별도 이력이다.
- 같은 현재 폼의 필드 이동에만 기존 native 키보드의 명시적 Hide를 생략하고 대상 필드의 기존 TMP Open을 수행하여 type・secure 조건을 갱신한다. 이미 native Done으로 숨겨진 경로는 열린 키보드 유지 경로가 아니다.
- 이전 wrapper는 편집 세션의 retired 목록에 임시 보관한다. 종료할 때 current 키보드를 먼저 정상 닫고 old wrapper 각각 active=false로 정리한 뒤 목록을 비운다. 빈 영역・제출・화면 이탈・pause・대상 활성화 실패・timeout에서 정리한다. 다음 필드 활성화까지 한 프레임 동안 이동량을 유지하여 전환 시 배치가 먼저 튀지 않게 한다.
- 마지막 native 문자와 selection은 설치 TMP 자신의 동기화・검증 경로로 반영한다. 설치 패키지 수정・reflection・native 키보드 직접 공유 브릿지는 추가하지 않는다. managed wrapper 동일성을 요구하는 방식도 아니다.

실제 TMP 렌더・native/window・raycast 입력은 대체환경 검사와 구분하며 Android 빌드・native 실기는 실행하지 않았다. 기존 준비・커서・이어쓰기・한글 편집・비밀번호 정리・정상 방 참가 수용은 유지한다. 남은 사용자 확인은 첫 탭 제출/닫기, 필드 전환 키보드 유지, 마지막 글자・가림・원복을 한 번의 통합 흐름으로 묶는다. 전체 완료는 아니다.

### 통합 최종 근거

임시 근거 폴더는 `C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-keyboard-transfer/`다. `manifest.tsv`, `scene-serialization.log`, `runtime.log`, `android.log`, 두 응답 파일, `green.log`, `pointer-green.log`, `red.log`, `tmp-lifecycle-extracted.cs`를 대조했다. 메인 독립 확인 뒤 문서 담당자도 현 파일・로그를 직접 확인했다.

| 축 | 최종 확인 | 한계 |
|---|---|---|
| 이번 6개 소스・씬 | manifest 6개 SHA256 전부 현 파일과 일치. form 최종 SHA256은 `6E6F15D325569D1E37E7149A50FF1AF26A2F1DC7C0BFA043383C7926DC801635` | 이번 전환 단계의 6개이며 작업 전체 누적 파일 수가 아님. 이전 relay 등은 앞 단계 이력 유지 |
| 씬 payload | Login 7개・Lobby 2개 모두 직렬화 payload 동일 기록 확인 | 기존 9개 필드 연결 보존이며 실제 렌더・기기 조작 증거 아님 |
| Runtime 정적 컴파일 | errors 0・warnings 23. UNITY_ANDROID와 UNITY_EDITOR 포함, helper 4개 명시 포함 | 통합 최종 선택 결과이며 Unity 실행・APK 빌드 아님 |
| Android 조건 정적 컴파일 | errors 0・warnings 30. UNITY_ANDROID 포함・UNITY_EDITOR 제외, helper 4개 명시 포함 | 기존 소비 설정 obsolete 경고 포함. native 실행 아님 |
| 폼 포인터 회귀 | 최종 `pointer-green.log` CHECKS=84・FAILURES=0 | 실제 설치 입력 모듈 순서와 helper를 사용하되 native/raycast 입력은 대역 |
| TMP 생명주기 회귀 | 설치 TMP의 activation/deactivation/select/native poll/Validate 본문 발췌를 결합한 `green.log` CHECKS=17・FAILURES=0 | 실제 TMP renderer/native window를 실행한 것이 아님 |
| 전환 RED | detach를 제거한 사본의 `red.log` CHECKS=17・FAILURES=4. no-Hide 관련 4개 실패 | 수정 민감도 근거이며 실제 기기 재열기 해소를 증명하지 않음 |
| 문서 검사 | 기존 Python alias 실패・검사 제외 한계 유지 | 검사기 재실행・PASS를 주장하지 않음 |

helper 4개는 `InputFormController`, `MobileKeyboardAvoidance`, `InputFormPointerRelay`, `ContinuousInputField`다. 14절의 당시 4개 manifest・검사 결과와 이 절의 현재 통합 6개 manifest・검사를 구분한다. 두 문서 외 상시 문서・메모리・TC・QA・Git 변경은 수행하지 않았다.

## 17. 통합 Android 결과와 남은 두 수정

16절의 통합 소스 뒤 사용자가 Android에서 확인한 결과다. 새 수정・검사 인계 전 상태이며 이전 정적/대체환경 PASS를 아래 실기 실패 해결 근거로 재사용하지 않는다.

| 항목 | 사용자 결과・현재 상태 |
|---|---|
| 비밀번호↔비밀번호 확인 전환 | 열린 키보드 유지 정상 수용 |
| 이메일↔비밀번호 전환 | 키보드가 닫혔다 다시 열림. 미충족・재작업 대상 |
| 키보드를 연 상태의 로그인 | 버튼 한 번 탭으로 동작. 정상 수용 |
| 회원가입 뒤로가기 | 폼 상향 이동에 함께 움직여 화면 위로 벗어나 보이지 않음. 사용자는 어떤 상황에서도 같은 위치를 요구 |
| 회원가입 화면 UI 뒤로가기 한 번 탭・종료 복원 | 버튼이 가려져 확인하지 못함. 미확인 유지 |
| 기존 수용 | 커서・이어쓰기・입력 검증・닉네임 오류 복구・잘못된 코드 실패 처리・정상 방 참가 등 기존 정상 결과 유지 |
| 과거 보류・미보고 | 9개 가림 전수 결과는 미보고, Tab・Shift+Tab은 기존 보류 유지 |

메인은 이메일↔비밀번호 전환과 뒤로가기 위치 고정의 두 수정을 묶어 구현 담당자에게 위임했다. 아직 새 수정 파일・검사・manifest는 인계되지 않았으며 완료로 기록하지 않는다. 네트워크 추가 작업으로 확대하지 않는다.

이번 수정 후 사용자 재확인은 이메일↔비밀번호 키보드 유지, 회원가입에서 키보드가 열려도 뒤로가기 버튼 위치 유지, 가려져 누르지 못했던 화면 UI 뒤로가기 한 번 탭과 종료 시 키보드 닫힘・배치 복원을 한 번에 확인하는 범위다. 이미 수용된 로그인 한 번 탭과 비밀번호↔확인 전환 등을 별도 반복 테스트로 요청하지 않는다. “남은 테스트가 이것이 전부인가”는 이번 수정의 집중 확인과 과거 보류/미보고가 별개임을 구분해 설명한다. 전체 작업 완료가 아니다.

<a id="18-이메일-전환과-고정-뒤로가기-후속-구현"></a>

## 18. 이메일 전환과 고정 뒤로가기 후속 구현

17절 이후 사용자는 키보드 앱이 **삼성 키보드**라고 확인했다. 아래는 새 소스・씬・대체환경 검사 근거이며 실제 삼성 키보드에서 원인을 확정하거나 수정본 성공을 수용한 결과가 아니다. 이전 정상 수용은 유지한다.

### 이번 수정 범위와 동작

이번 단계는 `Assets/_Project/Scripts/Presentation/UI/InputFormController.cs`, `Assets/_Project/Scripts/Presentation/UI/MobileKeyboardAvoidance.cs`, `Assets/_Project/Scenes/Login.unity`, `Assets/_Project/Scenes/Lobby.unity`의 4개다. 작업 전체 누적 파일 수와 구분한다.

- **이메일↔비밀번호 전환:** mode 재구성 중 target이 focused・keyboard nonnull인데 active=false/LostFocus인 일시 상태를 세션 종료로 오인해 retired wrapper를 닫을 수 있던 경로를 교정했다. 비종료 상태는 active 변동만으로 종료하지 않는다. Done・Canceled・빈 연결 실패・대상 활성화 timeout・빈 영역・실제 화면 종료・pause 정리는 유지한다. 재현한 소스 가능 경로이며 실제 삼성 기기의 원인 확정은 아니다.
- **고정 뒤로가기:** 폼 전체 대신 본문만 이동한다. 인증 입력 폼 3개는 원래 Rect의 Back을 고정 헤더에 남기고 KeyboardViewport/RectMask2D 아래 본문에 ContentArea・StatusText를 배치한다. viewport 상단 비율 .917와 본문 역앵커 1/.917로 초기 배치를 보존한다. Back을 마지막 형제로 두고 본문 클리핑으로 위쪽 겹침을 막는다. 커스텀 방 참가도 입력・참가 버튼 영역을 묶어 Back을 고정하고 기존 배치를 보존한다.
- **초기화・피드백:** 폼 4개의 본문/피드백 참조를 씬에 연결했다. Configure 전에는 표시・Update 처리를 진행하지 않아 native 설정 수명을 미리 획득하지 않는다. 진단은 개발 조건의 mode/status/active/frame만 다루고 입력 본문・길이는 기록하지 않는다.

### 검사 근거와 남은 경계

임시 근거 폴더는 `C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-keyboard-mode-fixed-header/`다. 메인 인계와 문서 담당자 직접 대조를 구분하며 영구 보존을 보장하는 원본으로 취급하지 않는다.

| 축 | 확인 | 한계 |
|---|---|---|
| 생명주기・배치 대역 | `green.log` CHECKS=24・FAILURES=0 직접 확인 | 실제 삼성 IME・TMP renderer/native window 실행 아님 |
| RED | active 조건을 복원한 `red-final.log` CHECKS=24・FAILURES=1, MODE_RECONFIGURATION_INACTIVE_NOT_IDLE 실패 | source 경로 민감도 근거이며 실기 원인 확정 아님 |
| 포인터 회귀 | `pointer-green.log` CHECKS=84・FAILURES=0 직접 확인 | raycast/native 입력은 대역 |
| 씬 검사 | `scene-audit.log` 67개 True・False 0 직접 집계. 메인도 계층・참조・mask・반응형 수학을 대조 | native 렌더・실제 키보드 높이 수용과 구분 |
| Runtime 정적 컴파일 | errors 0・warnings 23, UNITY_ANDROID/UNITY_EDITOR 포함, helper 4개 포함 직접 확인 | Unity 실행・기기 빌드 아님 |
| Android 조건 정적 컴파일 | errors 0・warnings 30, UNITY_ANDROID 포함・UNITY_EDITOR 제외, helper 4개 포함 직접 확인 | native 실행・삼성 실기 성공 아님 |

최종 manifest 4개와 현 파일 SHA256 일치를 메인과 문서 담당자가 확인했다. 메인은 씬 빈 이름 뒤 공백 9줄만 정리한 뒤 `git diff --check` exit 0을 확인했다. 최종 Login SHA256은 `0DEC0B87599FBE5F109A7570A66A392F575BD4B519D02C6462852FFDEDEFFDBC`, Lobby는 `EAE5B154FDF5E48E050326302253605CCD6F9E793AE55F95B812AD7C44D7D6E3`이다. 소스 2개는 변하지 않아 컴파일을 반복하지 않았으며 기능 변경 없는 공백 정리를 새 기능 검증으로 세지 않는다. RED 근거는 최종 `red-final.log` 24/1이며 초기 18/1 기록과 구분한다. 새 코드・씬 실기 확인은 아직 없으며 전체 완료가 아니다. 기존 Python 실행 불가 한계도 유지한다.

**한 번의 남은 집중 확인:** 이메일↔비밀번호 전환에서 키보드가 유지되는지, 키보드가 열려도 회원가입 뒤로가기 버튼의 위치가 같은지, 그 버튼을 한 번 눌러 화면을 닫고 키보드・본문이 복원되는지 확인한다. 이전 정상 로그인 한 번 탭・비밀번호↔확인 전환・커서・입력 검증・닉네임・방 참가 수용은 유지한다. 9개 입력란 가림 전수는 미보고, Tab・Shift+Tab은 보류이므로 이번 집중 확인과 별도로 구분한다. 중간/중복 테스트・네트워크 추가 작업은 요청하지 않는다.

## 19. 2026-10-19 저장 Android 로그의 읽기 분석

사용자가 저장한 [RuntimeLog_device.txt](../../../_Logs/2026-10-19/13_58_logcat/RuntimeLog_device.txt)를 읽어 확인했다. 헤더 저장 시각은 2026-10-19 13:58:24이고 SHA256은 `DFCFD5102B90BA4580E926D55D5EBC85EAC5A309415696EDAE3530A8E47817AC`다. 메인 독립 집계와 문서 담당자의 직접 재집계가 일치한다. 원시 로그는 읽기만 했으며 수정・삭제・업로드하지 않았다. 보관・공유 경계는 `LogRules.md` §1.10의 private custody 원칙을 유지한다.

### 프로세스와 관측 범위

원시 로그에는 이전 PID 9573과 새 PID 31104가 섞여 있다. 이전 프로세스의 13:44 전환 8회와 13:45 pause 뒤 drain은 별도 실행이다. 새 프로세스는 13:47:31에 Unity 6000.3.5f2 Development/IL2CPP로 시작했다는 메인 대조 인계이며, 이번 최신 분석은 **PID 31104, 13:53:19.960~13:53:23.336**에 한정한다. 이전 pause/drain을 최신 전환 중 닫힘의 원인으로 합치지 않는다.

| 최신 구간 관측 | 직접 집계 |
|---|---|
| phase=retained | 9회 |
| phase=target-activated | 9회 |
| Keyboard Visible=False | 9회 |
| Keyboard Visible=True | 9회 |
| phase=retired-drain | 0회 |
| phase=nonterminal-gap-preserved | 0회 |

첫 사례의 진단 retained는 13:53:19.960/frame 10342, GameActivity 표시 False는 19.984, 표시 True와 target-activated는 20.046이며 target도 frame 10342다. 이는 다음 필드 활성화・설정 갱신 구간에서 실제 OS/GameActivity 키보드 표시 전환이 발생했음을 보여준다. “화면이 움직여 그렇게 보였을 뿐”으로 설명하지 않는다. 해당 9회에는 C#의 이전 retired 정리 분기가 동작하지 않았으므로 이번 관측을 그 분기의 재발로 단정할 수 없다.

정확히 어떤 native 호출이나 삼성 키보드 정책이 표시를 바꿨는지, 완전히 교정할 방법은 별도 읽기 분석 중이며 아직 확정하지 않았다. `Hiding input field is not supported when using Game Activity.` 경고가 native 별도 입력창 hideInput 설정의 미지원인지도 확인 중이다. 이 경고만으로 소프트 키보드 Hide의 원인이라고 단정하지 않는다.

현재 판정은 **새 로그에서 전환 중 표시 변화 확인 / 이전 C# drain 원인 가설은 해당 구간 미관측 / native 원인・수정 방법 미확정 / 결함 미해결**이다. 코드・씬・새 Task・QA 로그・상시 문서・메모리・Git 변경은 수행하지 않았다. 기존 정상 수용・가림 전수 미보고・Tab 보류는 유지하며 중간 실기 테스트를 요청하지 않는다.

### 최종 읽기 분석과 후속안의 선택 경계

구현 담당자도 최신 9회와 drain 0회를 독립 대조했다(메인 인계). False→True 로그 간격은 45~84ms이며, 이는 관측 로그 시각의 간격으로 실제 화면 애니메이션 시간과 구분한다. retained와 target-activated가 같은 Unity frame인 사이에 native 표시가 변했다. 따라서 해당 현상을 C# EndIdle drain 조건으로 설명할 수 없으며, 조사 범위는 대상 필드 Open/IME 설정 갱신 구간으로 좁힌다. 특정 삼성 내부 native 함수가 원인이라는 판정은 아니다.

설치 소스의 최종 읽기 분석 인계에 따르면 `Hiding input field is not supported when using Game Activity.`는 `TouchScreenKeyboard.hideInput`, 즉 키보드 위 별도 native 입력칸 숨김의 미지원 경고다. 소프트 키보드 자체 Hide의 증거로 쓰지 않는다. 위 초기 분석의 “확인 중”은 당시 상태로 보존하며 이 문장으로 범위를 확정한다.

후속 후보는 로그인/회원가입 폼 안에서 native 키보드를 Default+secure 한 연결로 유지하면서 게임 화면에서는 이메일을 평문으로 표시하고 기존 이메일 검증을 유지하는 방향이다. 이메일 전용 `@` 배치・자동완성 편의를 잃을 수 있고 기존 TMP 기본 Open과 분리된 설계가 필요하다. **아직 사용자 선택・구현・검증이 없으므로 확정 계획이나 해결책으로 기록하지 않는다.** 타입을 유지한 채 native 갱신만 하는 대안도 restartInput의 깜빡임을 회피한다고 보장할 근거가 없다. Activity 전환이나 앱 전체 변경은 권고하지 않는다.

현재는 이 편의 손실을 수용할지 사용자 선택이 필요한 단계다. 추가 로그나 동일한 테스트 반복을 요청하지 않으며 새 코드・빌드 작업도 하지 않았다. 결함은 미해결이고 기존 정상 수용은 유지한다.

## 20. 공통 보안 키보드 연결 교정 승인

19절의 선택 대기 이후 사용자는 이메일 평문 표시・비밀번호 마스킹 유지와 이메일 전용 `@` 배치/자동완성 편의 손실을 확인한 뒤 “알겠어 교정 진행해보자”라고 명시적으로 승인했다. 앞 절의 후보・사용자 선택 전 문장은 그 당시 기록으로 보존하고 현재는 **승인된 설계 검토・구현 진행**으로 갱신한다.

목표는 로그인/회원가입 폼 안에서 native Default+secure 단일 연결을 재사용하여 다른 필드를 선택할 때마다 Open을 다시 호출하는 경로를 없애는 것이다. 게임 화면의 이메일 평문・비밀번호 마스킹, 필드별 입력 종류・검증・커서・입력값 분리는 유지한다. 다른 단일 입력 폼의 키보드 종류는 보존한다. 설치 패키지의 private 수정・reflection・Activity 전환은 사용하지 않는 방향이다.

구현 담당자는 public/protected 접근만으로 동작을 구성하는 설계를 검토 중이다. 정확한 코드 변경 범위・새 파일・검사 수치・manifest는 아직 확정 인계되지 않았으므로 구현 완료나 native 해결을 주장하지 않는다. 기존 Task 두 문서의 연속 승인 범위로 진행하고 새 문서 작성 여부를 다시 묻지 않는다.

원시 로그는 읽기 전용・private custody・무업로드 원칙을 유지한다. 기존 정상 커서・검증・닉네임・잘못된 코드・정상 방 참가・로그인 한 번 탭 등 수용, 9개 가림 전수 미보고, Tab・Shift+Tab 보류도 유지한다. 중간 빌드・유사 테스트 반복을 요청하지 않으며 전체 완료가 아니다. 이번 문서 변경은 기존 Research/Plan뿐이고 상시 문서・메모리・TC・QA・Git 변경은 하지 않았다.

<a id="21-공통-보안-키보드-연결-구현과-검증-경계"></a>

## 21. 공통 보안 키보드 연결 구현과 검증 경계

> 이 절은 최종 소스・정적 검사 인계 당시 기록이다. 당시 삼성 실기 미확인 문장은 보존하며 이후 사용자 수용과 남은 확인 범위는 22절을 따른다.

20절의 승인 후 로그인/회원가입에만 공통 보안 키보드 연결을 적용했다. 이번 변경은 `Assets/_Project/Scripts/Presentation/UI/ContinuousInputField.cs`, `Assets/_Project/Scripts/Presentation/UI/InputFormController.cs`, `Assets/_Project/Scenes/Login.unity`의 3개다. 작업 전체 누적 파일 수와 구분한다. Login 씬 두 폼의 opt-in으로 범위를 한정하고 다른 단일 입력 폼의 기존 키보드 종류를 유지한다.

폼의 첫 native Open은 Default・secure=true・autocorrection=false다. 정상 이메일↔비밀번호↔확인 전환은 같은 wrapper의 text・characterLimit・selection만 갱신하고 추가 Open/Hide를 하지 않는다. 이메일 UI의 평문・기존 검증, 비밀번호 UI의 마스킹은 바꾸지 않는다. 이메일 전용 `@` 배치/자동완성 편의 손실은 사용자가 승인한 경계다. 공개 API만으로 비밀번호 관리자 정책 전체를 차단한다고 주장하지 않는다.

native 참조가 없는 활성화 경계에서만 `shouldHideSoftKeyboard`를 잠시 켜 base TMP 초기화를 실행하고 finally에서 원래 설정을 복원한다. 기존 원문/취소/포커스/커서 초기화・검증은 TMP 경로를 사용하며 UI inputType/contentType/validator를 임시 변경하지 않는다. 폼 session・현재 선택・재진입 Close를 확인한 뒤 공통 연결을 부착한다. native selection은 문자열 길이로 clamp하고 취소 시 focused 필드의 참조도 해제한다. Retain은 owned 동일 handle만 허용한다. Close・pause・Done/Canceled・선택 변경・null 실패・timeout 정리는 유지하고 설치 패키지 private 수정・reflection・Activity 전환은 하지 않는다.

### 현재 확인된 검사와 대기 근거

임시 근거 폴더는 `C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/ui-shared-secure-keyboard/`다. 문서 담당자가 현 subclass와 `green.log` CHECKS=32・FAILURES=0, 공통 활성화 guard만 끈 negative control `red.log` CHECKS=32・FAILURES=23을 직접 확인했다. 동일 32항목의 비교이며 실제 삼성 IME가 아닌 TMP/native 대역 검사다. 정상 전환 Open 1회・추가 Open/Hide 0, selection clamp, 재진입 Close/pause・선택・session 변경, null/timeout, Done/Canceled, 비종료 LostFocus, programmatic 활성화와 미요청 활성화를 포함한다.

최종 메인 독립 대조와 문서 담당자 직접 확인으로 `green.log` 32/0, `red.log` 32/23, `pointer-green.log` 84/0을 확정했다. 최종 Runtime은 errors 0/warnings 23, Android 조건 정적 컴파일은 errors 0/warnings 30이다. `manifest.tsv`의 이번 3개 해시와 현 파일은 모두 일치했고 메인은 opt-in 두 폼과 `git diff --check` exit 0을 확인했다. 이번 3개는 공통 보안 연결 단계의 변경이며 전체 누적 파일 수와 구분한다. 소스 동결 후 최종 컴파일 인계를 대조했다.

메인은 `prior-baseline4.tsv`와 `preserved4-current.tsv`를 대조해 Avoidance/Lobby가 앞 단계 기준과 같음을 확인했다. 최종 Form SHA256은 `7E74940D8A281B96F3427984E70C74D80F8D55E970EA67435F7F6BA941DD95C6`, Continuous는 `B5E03AC3CAAA105433F4975E8AD4A2FD0E3F8038B4D82168D01876AEC4B9494F`, Login은 `51D1DE4FB824F58FB8B67877B270B78982E91C5661936A7ACE67C0A256F7A69B`다.

**현재 판정은 공통 보안 연결 소스・씬・최종 정적/대체환경 검사 확인 / 실제 삼성 native・Android 빌드・사용자 수용 미확인**이다. 기존 고정 뒤로가기・가림・첫 탭 소스 동작과 정상 사용자 수용은 유지하고, 9개 가림 전수 미보고・Tab 보류도 유지한다. 전체 완료가 아니며 새 중간 빌드・유사 테스트 반복은 요청하지 않는다. 두 문서 외 원시 로그・상시 문서・메모리・TC・QA・Git 변경은 하지 않는다. Python 검사기의 기존 실행 불가 한계도 유지한다.

<a id="22-삼성-키보드-통합-사용자-수용과-남은-범위"></a>

## 22. 삼성 키보드 통합 사용자 수용과 남은 범위

> 이 절은 통합 수용 직후의 기록이다. 당시 남은 4개 가림/복원과 Tab 보류는 이후 23절의 사용자 확인으로 닫혔으며 원문은 이력으로 보존한다.

사용자는 직전 안내한 통합 절차에 대해 **“좋아 모두 정상작동 확인했어”**라고 보고하고, 이어 **“문서 갱신 진행하자”**라고 승인했다. 메인 인계에 따라 아래 Android 삼성 키보드 통합 범위를 수용으로 기록한다. 정확한 APK 해시・사용자가 수행한 빌드 실행 로그는 제공되지 않았으므로 소스 manifest와 실행 산출물의 동일성을 별도로 확정하지 않는다. 사용자 실기 보고와 21절의 정적/대체환경 검사는 서로 다른 증거다.

| 통합 확인 범위 | 최신 사용자 수용 |
|---|---|
| 로그인 이메일↔비밀번호 | 필드 전환 중 키보드 유지 |
| 회원가입 이메일↔비밀번호↔확인 | 필드 전환 중 키보드 유지 |
| 입력 표시・내용 | 값 보존, 이메일 평문, 비밀번호 마스킹 |
| 선택 입력란 가림/원복 | 로그인 2개・회원가입 3개의 안내 범위 정상 |
| 키보드 열린 상태 로그인 | 버튼 한 번 탭으로 동작 |
| 회원가입 뒤로가기 | 고정 위치 유지, 화면 뒤로가기 한 번 탭으로 닫힘 |
| 재진입・재입력 | 회원가입 비밀번호 두 칸 비움, 다시 입력 가능 |

따라서 공통 보안 키보드 연결의 집중 교정은 **사용자 수용 완료**다. 기존 커서・이어쓰기・길이/일치 검증・닉네임 복구・없는 방 실패 처리・정상 방 참가 등의 수용도 유지하고 중복 재테스트를 요청하지 않는다. 특정 삼성 내부 native 원인을 완전히 규명했다는 뜻이나 모든 입력 기능의 전체 검증 완료로 확대하지 않는다.

**남은 실기 범위**는 총 9개 중 나머지 4개, 즉 비밀번호 재설정 이메일・최초 닉네임・닉네임 변경・방 코드의 입력란 가림과 키보드를 내린 뒤 배치 복원이다. 이 항목들은 미보고로 둔다. 계정 생성이나 유료 닉네임 변경을 강제하지 않으며 이미 확인한 기능을 다시 저장/실행할 필요는 없다. PC Tab・Shift+Tab은 Android에서 확인할 수 없어 기존 보류를 유지한다. 로그인/회원가입 5개 수용을 다른 4개까지 확장하지 않는다.

이번 갱신은 기존 Research/Plan 두 파일뿐이다. 코드・주석 정리・씬・빌드 실행・원시 로그 수정/삭제/업로드・상시 문서・메모리・Testcase・QA・Git 변경은 수행하지 않았다. 이전 32/0・32/23・84/0, 컴파일 0/23・0/30, manifest 근거는 당시 검사 그대로 보존한다. Python 검사기는 기존 실행 불가 한계를 유지하며 재검색하지 않았다. `_Tasks/`가 검사 제외라는 한계와 별도로 저장 문서의 수용 범위・링크・의미를 직접 검토한다.

<a id="23-인풋필드-개선-전체-사용자-수용-완료"></a>

## 23. 인풋필드 개선 전체 사용자 수용 완료

사용자는 남은 단일 입력란에 대해 **“4가지도 모두 정상작동 확인했어”**, 이어 Editor 이동 확인 뒤 **“모두 정상작동 확인했어 문서 갱신하자”**라고 보고・승인했다. 이번에 합의한 인풋필드 개선의 구현과 안내한 사용자 테스트는 **전체 수용 완료**다. 근거는 사용자 보고이며 정확한 APK/빌드 해시・사용자 빌드 실행 로그는 별도로 제공되지 않았다.

| 최종 확인 | 수용 범위・실행 환경 |
|---|---|
| 나머지 4개 가림/복원 | Android의 비밀번호 재설정 이메일・최초 닉네임・닉네임 변경・방 코드에서 입력란 가림 없음과 키보드 종료 후 배치 복원 정상 |
| 전체 9개 가림/복원 | 앞서 수용된 로그인 2개・회원가입 3개와 위 4개를 합쳐 총 9개 수용 |
| 로그인 키보드 이동 | Unity Editor Play/Game 창에서 이메일→비밀번호→이메일 Tab 순환, Shift+Tab 역방향 정상 |
| 회원가입 키보드 이동 | 같은 Editor 환경에서 이메일→비밀번호→확인→이메일 Tab 순환, Shift+Tab 역방향 정상 |
| 이동 중 편집・제출 보호 | 값 보존, 선택 필드 즉시 입력, 이동으로 인한 오제출 없음 |

따라서 22절의 4개 미보고・Tab 보류는 현재 잔여가 아니다. Unity Editor 결과를 PC standalone 빌드 실기 증거로 확대하지 않는다. 앞선 Android 삼성 키보드 전환 유지・이메일 평문/비밀번호 마스킹・첫 탭・고정 뒤로가기・재진입 비밀번호 정리와 기존 커서・검증・닉네임・방 참가 수용도 유지한다. 정적 32/0・negative control 32/23・포인터 84/0・컴파일 0/23 및 0/30・manifest는 당시 증거로 보존하고 사용자 수용과 구분한다.

전면 레이아웃 개편・비밀번호 보기 버튼・미배선 계정 연동 등 합의 범위 밖 후속은 이번 완료를 막는 필수 테스트가 아니다. 비활성화 주석의 최종 삭제・코드 정리는 이번 문서 승인 범위 밖의 별도 정리이며 수행하지 않았다. 이를 이유로 이번 테스트 완료를 미완료로 바꾸지 않는다.

이번 갱신은 기존 Research/Plan 두 파일뿐이다. 상시 문서・메모리・Testcase・QA・코드/주석・씬・빌드 실행・원시 로그 수정/삭제/업로드・Git 변경은 하지 않았다. Python 검사기의 기존 실행 불가와 `_Tasks/` 제외 한계를 유지하고 재검색하지 않았으며, 최종 수용 범위와 문서 링크/의미는 직접 검수한다.
