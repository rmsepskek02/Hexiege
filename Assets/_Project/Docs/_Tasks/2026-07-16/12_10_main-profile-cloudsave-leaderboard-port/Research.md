# Research - main 기준 프로필/Cloud Save/리더보드 이식

## 기준 경로

- Unity Editor 실제 프로젝트 경로: `D:/Projects/Hexiege/Hexiege`
- 작업 브랜치: `codex/profile-cloudsave-leaderboard-port`
- 참고 원본 경로: `D:/Projects/Hexiege/Hexiege-main-firebase`
- 참고 원본 브랜치: `claude/profile-stats-display-lhghz8`

## 확인된 혼란 원인

- Unity는 `D:/Projects/Hexiege/Hexiege`를 열고 있었다.
- 이전 작업 일부는 `D:/Projects/Hexiege/Hexiege-main-firebase`에 적용되어 Unity 런타임에 반영되지 않았다.
- 이후 모든 구현/검증은 `D:/Projects/Hexiege/Hexiege`에서만 진행한다.

## 현재 `Hexiege`에 임시 반영된 내용

- `TabBarView.cs`
  - 프로필 탭 클릭 로그 추가.
  - `TabBarView.Bind()` 호출 로그 추가.
- `LobbyRootView.cs`
  - `ProfileView` 자동 탐색 필드 추가.
  - Profile 탭 선택 시 `ProfileView.OnProfileTabShown()` 호출.
- `ProfileView.cs`
  - `OnProfileTabShown()` 추가.
  - 진단용 로그 추가.
- `EmailVerifyView.cs`
  - 이메일 인증 완료 분기 진단용 로그 추가.
- `SignUpView.cs`
  - 런타임 닉네임 입력칸 추가.
  - 이메일 가입 시 Firebase `displayName`으로 닉네임 전달.

이 변경은 최종 구조가 아니라, 실제 경로 반영 여부와 최소 동작을 확인하기 위한 임시/브릿지 작업이다.

## `Hexiege-main-firebase`에 있고 `Hexiege`에 부족한 주요 요소

### 패키지

`Hexiege-main-firebase`에는 있고 현재 `Hexiege`에는 확인되지 않은 패키지:

- `com.unity.services.cloudsave`
- `com.unity.services.leaderboards`

Cloud Save/Leaderboards 코드를 이식하려면 `Packages/manifest.json`과 `Packages/packages-lock.json` 반영이 필요하다.

### Application 계층

- `Assets/_Project/Scripts/Application/Models/PlayerProfileData.cs`
- `Assets/_Project/Scripts/Application/Models/LeaderboardEntry.cs`
- `Assets/_Project/Scripts/Application/Interfaces/IPlayerProfileService.cs`
- `Assets/_Project/Scripts/Application/Interfaces/ILeaderboardService.cs`
- `Assets/_Project/Scripts/Application/UseCases/PlayerProfileUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/RankingUseCase.cs`

### Infrastructure 계층

- `Assets/_Project/Scripts/Infrastructure/Cloud/PlayerProfileService.cs`
- `Assets/_Project/Scripts/Infrastructure/Cloud/LeaderboardService.cs`

### Presentation 계층

- `Assets/_Project/Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/NicknameChangePopup.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankRowView.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs`

현재 `Hexiege`에도 `RankingView.cs`는 존재하지만, 관련 Application/Infrastructure 타입과 패키지가 부족하므로 버전 차이 확인 후 교체/병합해야 한다.

### Editor/Setup 계층

`Hexiege-main-firebase`에는 다음 1회성 UI 생성/보정 스크립트가 있다.

- `Assets/Editor/Setup/CreateNicknameSetupPanel.cs`
- `Assets/Editor/Setup/CreateNicknameChangePopup.cs`
- `Assets/Editor/Setup/CreateProfileStatsFields.cs`
- `Assets/Editor/Setup/CreateRankingTable.cs`

이번 작업 방향은 가능한 한 런타임 임시 생성이 아니라 씬 자체에 UI를 고정하는 것이다. 따라서 이 스크립트들은 그대로 실행하기보다 필요한 구조를 이해하고, 최종 씬 구성에 맞게 선별 사용/폐기한다.

## 위험 지점

1. 패키지 누락
   - Cloud Save/Leaderboards 코드만 복사하면 컴파일 실패 가능성이 높다.

2. 씬 슬롯 누락
   - `NicknameSetupView`, `NicknameChangePopup`, `RankingView`, `RankRowView`는 Inspector 참조가 필요하다.
   - 단순 파일 이식만으로는 런타임 UI가 비거나 null guard로 동작하지 않을 수 있다.

3. 이메일 가입 닉네임 시점
   - Cloud Save 닉네임 저장은 UGS 세션 이후 가능하다.
   - 과거 문서에 따르면 이메일 가입 직후 Cloud Save 저장 시 "Access token is missing" 문제가 있었다.
   - 안전한 방향은 이메일 인증 완료 후 UGS bridge가 끝난 뒤 최초 로그인 닉네임 설정을 진행하는 것이다.

4. 현재 임시 Firebase DisplayName 닉네임
   - 새 가입자 게스트 표기를 줄이는 임시 완화책이다.
   - 최종 프로필 닉네임 소스는 Cloud Save `nickname`/`nicknameCode`로 통일해야 한다.

5. `ProjectSettings/AndroidResolverDependencies.xml`
   - 현재 `Hexiege`에 이미 변경되어 있으나 이번 작업에서 만든 변경으로 보지 않는다.
   - 보존하고 별도 판단 없이는 되돌리지 않는다.

