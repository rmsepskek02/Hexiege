# Testcase — OIDC Bridge & UGS 데이터 플랫폼

## 테스트 전제 조건

| 항목 | 상태 |
|------|------|
| Firebase Console 설정 (google-services.json, SHA-1, Auth 방식 활성화) | ❌ 미완료 — TC-01, TC-02, TC-04 실기 불가 |
| UGS Dashboard OIDC Provider 등록 (`oidc-firebase`) | ❌ 미완료 — TC-01, TC-04 실기 불가 |
| Login.unity 씬 Inspector 연결 | ❌ 미완료 — TC-01~04 실기 불가 |
| Lobby.unity 직접 실행 (로그인 씬 없이) | ✅ 가능 — TC-03 실기 가능 |

> Firebase 및 UGS 설정 완료 후 TC-01~04 실기 테스트 진행.
> 현재는 TC-03만 실기 가능. 나머지는 설정 완료 후 결과란에 기록.

---

## TC 목록

### TC-SINGLE-01: 실계정 로그인 후 UGS PlayerId 계정 귀속 확인

**전제:** Firebase Console 설정과 UGS Dashboard OIDC Provider 등록이 완료된 상태.
이메일로 가입된 계정이 있고, Login 씬에서 앱이 시작된다.

**동작:**
1. 앱을 실행하여 Login 씬에 진입한다.
2. 이메일과 비밀번호를 입력하고 로그인 버튼을 누른다.
3. 로그인 성공 후 Lobby 씬으로 진입한다.
4. Unity Editor Console에서 `[LoginUseCase] UGS 브릿지 완료` 로그에 표시된 PlayerId를 메모한다.
5. 앱을 종료하고 기기를 변경하거나 앱을 재설치한 뒤 동일 계정으로 다시 로그인한다.
6. Console에 표시된 PlayerId를 확인한다.

**기댓값:**
- 첫 번째 로그인과 두 번째 로그인에서 PlayerId가 동일하게 표시된다.
- `[LoginUseCase] 실계정 — Firebase ID Token 으로 UGS OIDC 브릿지 수행` 로그가 출력된다.
- `[LoginUseCase] 자동 로그인: UGS 미연결` 경고 로그가 출력되지 않는다.

**결과:** (Firebase/UGS 설정 완료 후 기록)

---

### TC-SINGLE-02: 익명 로그인 후 UGS 익명 로그인 분기 확인

**전제:** Firebase Console 설정이 완료된 상태. Login 씬에서 앱이 시작된다.

**동작:**
1. 앱을 실행하여 Login 씬에 진입한다.
2. "익명으로 시작하기" 버튼을 누른다.
3. 경고 팝업에서 "계속 익명으로 진행"을 선택한다.
4. Lobby 씬으로 진입한 후 Console 로그를 확인한다.

**기댓값:**
- `[LoginUseCase] 익명 계정 — UGS 익명 로그인 수행` 로그가 출력된다.
- `[LoginUseCase] 실계정 — Firebase ID Token 으로 UGS OIDC 브릿지 수행` 로그가 출력되지 않는다.
- PlayerId가 정상 발급되어 멀티플레이 기능이 동작한다.

**결과:** (Firebase 설정 완료 후 기록)

---

### TC-SINGLE-03: Lobby.unity 직접 실행 시 OIDC 세션 덮어쓰기 방지 확인

**전제:** Build Index를 "게임 기능 테스트" 모드로 설정하여 Lobby.unity가 Build Index 0.
Login 씬 없이 Lobby 씬으로 직접 진입한다.

**동작:**
1. Unity Editor에서 Lobby.unity 씬을 직접 실행한다.
2. Console 로그를 확인한다.
3. 랜덤 매칭 또는 커스텀 게임 참가를 시도한다.

**기댓값:**
- `[Network] UGS 세션 없음 — 익명 로그인으로 폴백 수행` 로그가 출력된다.
- `[Network] 기존 UGS 세션 로그아웃 — 토큰 갱신을 위해 재로그인 수행` 로그가 출력되지 않는다.
- PlayerId가 정상 발급되어 랜덤 매칭 / 커스텀 게임이 정상 동작한다.

**결과:**

---

### TC-SINGLE-04: UGS 브릿지 실패 시 로그인은 성공 처리 확인

**전제:** Firebase Console 설정은 완료됐으나 UGS Dashboard에 OIDC Provider가 등록되지 않은 상태.
(의도적으로 UGS 브릿지가 실패하도록 구성)

**동작:**
1. 이메일 계정으로 로그인을 시도한다.
2. 로그인 결과와 Console 로그를 확인한다.

**기댓값:**
- 로그인 자체는 성공하여 Lobby 씬으로 이동한다.
- `[LoginUseCase] 이메일 로그인: UGS 미연결 — 멀티플레이 기능이 제한될 수 있습니다` 경고 로그가 출력된다.
- 에러 팝업이 표시되거나 로그인 화면에 남아 있지 않는다.

**결과:** (Firebase 설정 완료 후 기록)

---

## QA 정적 분석 결과

### 아키텍처

| 항목 | 판정 |
|------|------|
| LoginUseCase(Application)에서 Firebase SDK 직접 참조 없음 | PASS |
| FirebaseAuthService.GetIdTokenAsync()로 토큰 캡슐화 | PASS |
| UnityServicesInitializer 레이어 경계 침범 없음 | PASS |
| BridgeToUGSAsync Task<bool> 반환 — 5개 호출부 전체 처리 | PASS |

### 기능

| 항목 | 판정 |
|------|------|
| 실계정 OIDC Bridge 흐름 완결성 | PASS |
| 익명 계정 UGS 익명 분기 | PASS |
| UnityServicesInitializer OIDC 세션 덮어쓰기 방지 | PASS |
| 기존 HTTP 401 버그 수정 (폴백 익명 로그인 유지) | PASS |
| UGS 실패 시 로그인 성공 처리 (오류 처리 규칙 3) | PASS |
| AuthSystemRules.md 구현 일치 | PASS |

### 종합 정적 분석 판정: PASS

실기 테스트는 Firebase Console + UGS Dashboard 설정 완료 후 진행.
