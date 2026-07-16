# Plan - main 기준 프로필/Cloud Save/리더보드 이식

## 목표

Unity가 실제로 여는 `D:/Projects/Hexiege/Hexiege`에서 모든 프로필/닉네임/Cloud Save/리더보드 작업을 진행한다.

최종 목표:

1. 이메일/Google 최초 로그인 후 닉네임 설정을 반드시 거친다.
2. 닉네임은 Cloud Save `nickname` + `nicknameCode`를 정식 소스로 사용한다.
3. 프로필 탭은 계정 정보, 닉네임, 전적, 내 랭킹, 닉네임 변경, 로그아웃만 정리된 UI로 표시한다.
4. 랭킹 탭은 UGS Leaderboards 데이터를 표시한다.
5. 런타임 임시 UI 생성은 제거하고, 씬 자체에 필요한 UI 구조를 고정한다.

## 작업 원칙

- 기준 경로는 항상 `D:/Projects/Hexiege/Hexiege`.
- `Hexiege-main-firebase`는 읽기 전용 참고 원본으로만 사용한다.
- `main`에는 직접 누적하지 않고 `codex/profile-cloudsave-leaderboard-port` 브랜치에서 진행한다.
- 현재 임시 디버그 로그는 검증 후 제거한다.
- `ProjectSettings/AndroidResolverDependencies.xml`는 기존 변경으로 취급하고 건드리지 않는다.

## 구현 단계

### 1. 기반 패키지 이식

변경 대상:

- `Packages/manifest.json`
- `Packages/packages-lock.json`

추가/확인:

- `com.unity.services.cloudsave`
- `com.unity.services.leaderboards`

검증:

- Unity 패키지 resolve 후 컴파일 에러 없음.

### 2. Application/Infrastructure 계층 이식

원본에서 가져올 파일:

- `Application/Models/PlayerProfileData.cs`
- `Application/Models/LeaderboardEntry.cs`
- `Application/Interfaces/IPlayerProfileService.cs`
- `Application/Interfaces/ILeaderboardService.cs`
- `Application/UseCases/PlayerProfileUseCase.cs`
- `Application/UseCases/RankingUseCase.cs`
- `Infrastructure/Cloud/PlayerProfileService.cs`
- `Infrastructure/Cloud/LeaderboardService.cs`

검증:

- Presentation 파일을 아직 연결하지 않은 상태에서도 컴파일 통과.
- UGS 로그인 상태에서 Cloud Save load/save 호출이 가능한 구조인지 확인.

### 3. 로그인 닉네임 플로우 이식

변경 대상:

- `LoginBootstrapper.cs`
- `LoginRootView.cs`
- `EmailLoginView.cs`
- `EmailVerifyView.cs`
- 신규/이식 `NicknameSetupView.cs`
- `Login.unity`

정식 흐름:

1. 이메일 가입 성공 후에는 인증 대기 화면으로 이동한다.
2. 이메일 인증 완료 확인 시 `CheckEmailVerifiedAsync()`가 UGS bridge까지 완료한다.
3. 이후 Cloud Save에서 `nickname`이 비었는지 확인한다.
4. 최초 로그인이라면 `NicknameSetupView`로 이동한다.
5. 닉네임 저장 성공 후 로비로 이동한다.

주의:

- 이메일 가입 직후 Cloud Save 저장을 하지 않는다.
- 현재 `SignUpView`의 Firebase DisplayName 임시 닉네임 입력은 최종 플로우 확정 후 제거하거나 보조 표시명 용도로만 축소한다.

### 4. 프로필/랭킹 Presentation 이식

변경 대상:

- `ProfileView.cs`
- `NicknameChangePopup.cs`
- `RankingView.cs`
- `RankRowView.cs`
- `LobbyRootView.cs`
- `Lobby.unity`

프로필 탭 표시:

- 계정 표시명 또는 이메일
- `nickname#code`
- 총 전투/승/패/승률
- 마지막 접속 종료
- 내 랭킹
- 닉네임 변경
- 로그아웃

랭킹 탭 표시:

- 상위 랭킹 목록
- 닉네임/승률/게임수/승/패
- 데이터 없음/조회 실패 상태

### 5. 씬 UI 고정

목표:

- 런타임에서 임시로 입력칸/패널을 생성하지 않는다.
- Login 씬에 `NicknameSetupPanel`을 실제 오브젝트로 둔다.
- Lobby 씬에 `ProfileStatsContainer`, `NicknameChangePopup`, `RankingTable`을 실제 오브젝트로 둔다.
- 각 탭/패널은 CanvasGroup `alpha`, `interactable`, `blocksRaycasts`로 제어한다.

작업 방법:

- 기존 editor setup script는 구조 참고용으로만 먼저 검토한다.
- 필요하면 1회성 editor script를 현재 씬에 맞게 수정 후 실행한다.
- 최종적으로 런타임 보정 코드는 최소화한다.

### 6. 임시 변경 정리

제거/정리 대상:

- `[DEBUG-PROFILE-TAB]`
- `[DEBUG-PROFILE-UI]`
- `[DEBUG-NICKNAME-FLOW]`
- `SignUpView`의 런타임 닉네임 입력칸 생성 임시 코드

단, 실제 이식 검증이 끝난 뒤 제거한다.

## 검증 체크리스트

1. Unity 컴파일 에러 없음.
2. Login 씬 진입 가능.
3. 이메일 신규 가입:
   - 이메일/비밀번호 입력
   - 인증 메일 발송
   - 인증 완료 후 닉네임 설정 화면 표시
   - 닉네임 저장 후 로비 이동
4. 기존 이메일 계정:
   - Cloud Save 닉네임이 있으면 닉네임 화면 생략
   - Cloud Save 닉네임이 없으면 닉네임 화면 표시
5. Google 최초 로그인:
   - UGS bridge 후 닉네임 화면 표시
6. 프로필 탭:
   - Cloud Save 프로필 표시
   - 닉네임 변경 팝업 동작
   - 로그아웃 동작
7. 랭킹 탭:
   - Leaderboard 조회
   - 실패/빈 상태 UI 표시

## 다음 작업 단위

1. 패키지 manifest 이식
2. Application/Infrastructure 파일 이식
3. 컴파일 확인
4. Login 닉네임 플로우 이식
5. Lobby 프로필/랭킹 UI 이식
6. 씬 고정 및 임시 로그 제거

