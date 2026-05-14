# Research: 전역 로딩 스크린

**날짜:** 2026-03-16

---

## 배경

현재 로딩이 필요한 구간에서 유저에게 아무런 시각 피드백이 없음.
대표적으로 랜덤 매칭 완료 후 게임 씬 진입까지 수 초~최대 10초 지연이 발생하는데,
화면은 "매칭 중... 00:XX" 타이머가 멈춘 채로 유지됨.

---

## 로딩이 필요한 구간

| 구간 | 예상 대기 시간 | 현재 피드백 |
|------|--------------|------------|
| 랜덤 매칭 완료 → 게임 씬 진입 | 3~10초 | 없음 |
| 커스텀 게임 호스팅 → 대기 화면 | 2~4초 | `IsConnecting` 스피너 (있음) |
| 커스텀 게임 참가 → 게임 씬 진입 | 2~5초 | `IsConnecting` 스피너 (있음) |
| 싱글플레이 씬 로드 | 1~2초 | 없음 |

→ 매칭 완료 이후 구간과 싱글플레이 씬 로드가 우선 대상.

---

## 현재 관련 코드 분석

### 1. RandomMatchView.cs — 현재 텍스트 표시 방식

```csharp
// vm.MatchWaitSeconds 구독 → 매칭 대기 타이머
_statusText.text = $"매칭 중... {min:00}:{s:00}";

// vm.IsMatchmaking == false → 초기 텍스트 복귀
_statusText.text = "랜덤 매칭";
```

`MatchWaitSeconds`는 `PollUntilMatchedAsync` 폴링 중에만 증가.
매칭 완료(matchId 확보) 이후에는 타이머가 멈추고 상태 변화 없음.

### 2. NetworkGameManager.StartMatchmakingAsync — 매칭 완료 후 흐름

```
PollUntilMatchedAsync() → matchId 반환
  ↓  ← 이 시점부터 씬 로드 전까지 최대 10초 지연
DetermineIsHostAsync(matchId)          (~1초, API 호출)
  ↓
[Host] HostGameAsync()                 (~2~3초, Relay + Lobby 생성)
[Client] JoinByMatchIdAsync()          (~1~10초, 1초 간격 최대 10회 재시도)
  ↓
LoadGameScene()
```

### 3. BattleViewModel.CmdStartMatchmaking

```csharp
await _networkManager.StartMatchmakingAsync(
    onWaitSecond: sec => MatchWaitSeconds.Value = sec);
// StartMatchmakingAsync가 완료(= 씬 로드 후)되면 아래 실행되지 않음
// OperationCanceledException → IsMatchmaking = false
// Exception → ErrorMessage 표시
```

`StartMatchmakingAsync` 내에서 matchId 확보 이후 알림 수단이 없음.

---

## 요구사항 정리

1. **전역 접근** — 씬에 종속되지 않고 어디서든 호출 가능
2. **DontDestroyOnLoad** — 씬 전환 중에도 화면 유지
3. **커스터마이징 가능한 메시지** — 상황별 다른 텍스트 표시
4. **자동 숨김** — 씬 로드 완료 시 자동으로 숨김 처리 (호출부에서 Hide() 명시 불필요)
5. **페이드 인/아웃** — 부드러운 등장/퇴장 (DoTween 활용)
6. **재활용** — 매칭, 씬 전환, 기타 모든 로딩 구간에서 사용 가능

---

## 기술 스택 참조

- **DoTween** — 프로젝트에 포함됨 (`DOTweenSettings.asset` 확인), 페이드 애니메이션 활용
- **UniRx** — 프로젝트 전반 사용 중, 필요 시 Observable 연동 가능
- **TMP (TextMeshPro)** — 현재 UI 텍스트 전반 사용 중
- **Unity SceneManager** — `sceneLoaded` 이벤트로 자동 숨김 구현

---

## 영향 범위

| 파일 | 변경 여부 |
|------|---------|
| `Scripts/Presentation/UI/Common/LoadingScreen.cs` | ✅ 신규 생성 |
| `BattleViewModel.cs` | ✅ 수정 (matchId 확보 시점에 Show 호출) |
| Unity Editor (프리팹/Canvas) | ✅ 에디터 작업 필요 |
| `NetworkGameManager.cs` | 수정 불필요 (ViewModel에서 처리) |
| 그 외 | 수정 불필요 |
