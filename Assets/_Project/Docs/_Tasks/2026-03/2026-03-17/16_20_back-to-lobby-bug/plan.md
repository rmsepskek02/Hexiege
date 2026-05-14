# Plan: 멀티플레이 로비 복귀 버그 수정

**날짜:** 2026-03-17

---

## 수정 목표

커스텀/랜덤 매칭 게임 종료 후 "로비로" 버튼 클릭 시 정상적으로 Lobby 씬으로 이동.
30초 카운트다운 후 자동 로비 복귀.

---

## 근본 원인 (디버깅으로 확인)

`NetworkGameEndController._lobbySceneName` Inspector 값이 "Lobby"가 아닌 "Game"으로 설정되어 있었음.
`SceneManager.LoadScene("Game")`이 호출되어 게임 씬이 재로드됨.

---

## 설계 결정

게임이 이미 종료된 시점이므로 네트워크 연결 상태와 무관하게 각 플레이어가 독립적으로 로비로 복귀.
- ServerRpc/ClientRpc 로비 복귀 로직 제거
- 각 클라이언트가 버튼 클릭 or 타이머 만료 시 로컬에서 직접 처리

---

## 수정 파일

| 파일 | 작업 |
|------|------|
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs` | RPC 로비 복귀 로직 제거 |
| `Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs` | 로컬 복귀 처리 + 30초 카운트다운 추가 |

---

## 구현 1: NetworkGameEndController.cs

다음 메서드 제거:
- `RequestBackToLobby()`
- `RequestBackToLobbyServerRpc()`
- `BackToLobbyClientRpc()`
- `BackToLobbyDeferred()`

`using System.Collections;` 도 필요 없으면 제거.

---

## 구현 2: GameEndUI.cs

### 추가 SerializeField

```csharp
[Tooltip("자동 복귀 카운트다운 텍스트 (예: '30초 후 로비로 돌아갑니다.')")]
[SerializeField] private TextMeshProUGUI _countdownText;

[Tooltip("자동 복귀까지 대기 시간 (초)")]
[SerializeField] private float _autoReturnSeconds = 30f;
```

### 카운트다운 로직

- `ShowResult()` / `OnGameEnd()` 호출 시 카운트다운 코루틴 시작
- 매 초마다 `"N초 후 로비로 돌아갑니다."` 텍스트 업데이트
- 0초 도달 시 자동으로 `ReturnToLobby()` 호출

### ReturnToLobby() — 싱글/멀티 통합

```csharp
private void ReturnToLobby()
{
    StopCountdown();
    Time.timeScale = 1f;
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        NetworkManager.Singleton.Shutdown();
    SceneManager.LoadScene("Lobby");
}
```

### OnBackToLobbyClicked() 변경

기존 `NetworkContext.IsNetworkActive` 분기 제거 → `ReturnToLobby()` 직접 호출

### OnRestartClicked() 변경

재시작 버튼 클릭 시 카운트다운 코루틴 중지

---

## 위험 요소

| 위험 | 평가 | 대응 |
|------|------|------|
| _countdownText Inspector 미연결 | 낮음 | null 체크로 안전 처리 |
| 카운트다운 중 씬 전환 발생 | 낮음 | OnDestroy에서 코루틴 정리 |

---

## 테스트 체크리스트

- [x] "로비로" 버튼 클릭 시 클릭한 플레이어만 Lobby로 이동
- [x] 30초 카운트다운 텍스트가 매 초 업데이트됨
- [x] 30초 후 자동으로 Lobby로 이동
- [x] "다시하기" 클릭 시 카운트다운 멈춤
- [x] 싱글플레이 로비 복귀 정상 작동
