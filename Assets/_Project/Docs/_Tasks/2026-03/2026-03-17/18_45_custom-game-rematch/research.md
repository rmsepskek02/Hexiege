# Research: 커스텀게임 재경기 시스템

**날짜:** 2026-03-17

---

## 현재 구현 상태

### GameEndUI.cs
- `OnRestartClicked()`: 싱글플레이 전용 — `GameBootstrapper.LoadMap()` 직접 호출
- `OverrideRestartForMultiplayer(System.Action callback)`: 멀티플레이 시 다시하기 버튼의 콜백을 교체
- `OnBackToLobbyClicked()` / `ReturnToLobby()`: 로컬 독립 처리 (Shutdown → LoadScene("Lobby"))
- `CountdownCoroutine()`: 30초 자동 로비 복귀

### NetworkGameEndController.cs
- `AnnounceWinnerClientRpc(int winnerTeamIndex)`: 게임 종료 시 모든 클라이언트에 승자 전파
  - 내부에서 `OverrideRestartForMultiplayer(OnMultiplayerRestart)` 호출 → 다시하기 버튼 콜백 교체
- `OnMultiplayerRestart()`: 현재 다시하기 콜백 — `Shutdown()` → `LoadScene("Lobby")`
  - 문제: 커스텀/랜덤 구분 없이 로비로 이동 (재경기 기능 없음)

### 현재 다시하기 흐름 (멀티 공통)
```
다시하기 클릭 → OnMultiplayerRestart() → Shutdown() → LoadScene("Lobby")
```
→ 재경기 없이 모두 로비 복귀

---

## 변경이 필요한 이유

| 모드 | 원하는 동작 | 현재 동작 |
|------|------------|----------|
| 싱글플레이 | 맵 리셋 즉시 재시작 | ✅ 정상 |
| 랜덤매칭 | 다시하기 버튼 없음 (로비 복귀만) | ❌ 버튼 존재 |
| 커스텀게임 | 양측 동의 후 게임 씬 재로드 | ❌ 로비 복귀 |

---

## 영향 범위

### 수정 파일
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs`
  - 재경기 요청/수락/거절 RPC 추가
  - 서버 상태 관리 (_rematchRequesterId)
- `Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs`
  - 다시하기 버튼 상태 관리 ("요청 중..." 텍스트, 비활성화)
  - 게임 모드 분기 (싱글/랜덤/커스텀)
  - 재경기 요청 팝업 UI 제어

### 신규 파일
- `Assets/_Project/Scripts/Presentation/UI/RematchRequestPopup.cs`
  - "OO님이 재경기를 요청하였습니다." 수락/거절 팝업

### 씬 수정
- `Assets/_Project/Scenes/Game.unity`
  - GameEndUI에 RematchRequestPopup 연결 필요

---

## 레이스 컨디션 분석

두 플레이어가 동시에 다시하기를 클릭하는 경우:
- 서버에서 `_rematchRequesterId` 상태값으로 처리
- 첫 번째 요청 수신 시 → requesterId 기록, 상대방에게 팝업 RPC
- 두 번째 요청 수신 시 → requesterId가 이미 있으므로 상호 동의로 판단 → 즉시 재시작
- 팝업 없이 자연스럽게 재경기 진행됨

---

## 네트워크 씬 재로드 방식

커스텀게임 재경기는 `NetworkManager.SceneManager.LoadScene("Game")` 사용:
- NGO Enable Scene Management = ON → 서버가 LoadScene 호출 시 모든 클라이언트 자동 동기화
- Relay/Lobby 연결 유지 — 새 방 코드 불필요
- NetworkObject 재스폰, GameBootstrapper.Start() 재실행 → 게임 상태 자동 리셋
- `_networkGameStarted` 플래그 리셋 필요 (중복 방지)

---

## 게임 모드 판별 방법

`AnnounceWinnerClientRpc` 호출 시점에 `NetworkGameManager`의 게임 모드를 참조:
- `NetworkGameManager`에 `GameMode` enum (Singleplay / CustomGame / RandomMatch) 또는
- NetworkGameEndController에서 `NetworkGameManager.IsMatchmaking` 속성으로 랜덤/커스텀 구분
