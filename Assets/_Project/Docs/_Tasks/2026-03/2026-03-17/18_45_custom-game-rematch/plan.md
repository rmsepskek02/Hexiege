# Plan: 커스텀게임 재경기 시스템

**날짜:** 2026-03-17

---

## 목표

| 모드 | 다시하기 동작 |
|------|-------------|
| 싱글플레이 | 현행 유지 (맵 리셋 즉시 재시작) |
| 랜덤매칭 | 다시하기 버튼 숨김, 로비 복귀만 |
| 커스텀게임 | 양측 동의 재경기 (요청 → 수락/거절 → 씬 리로드) |

---

## 설계 결정

### 1. 게임 모드 판별
`NetworkGameManager`에 `IsRandomMatchmaking` (bool) 속성 추가.
`AnnounceWinnerClientRpc` 파라미터에 `isRandomMatch` bool 추가하여 클라이언트에 전달.

### 2. 레이스 컨디션 처리
서버에서 `_rematchRequesterId` (ulong, 기본값 `ulong.MaxValue`) 상태값 유지:
- 첫 요청 수신: requesterId 기록 → 상대방에게 팝업 RPC
- 두 번째 요청 수신 (requesterId가 이미 있음): 상호 동의 → 즉시 재시작

### 3. 재경기 씬 리로드
`NetworkManager.SceneManager.LoadScene("Game")` 서버 호출
→ NGO가 모든 클라이언트 자동 동기화, Relay 연결 유지

---

## 수정 파일 목록

| 파일 | 작업 |
|------|------|
| `NetworkGameEndController.cs` | 재경기 RPC 4개 추가, 서버 상태 관리 |
| `NetworkGameManager.cs` | `IsRandomMatchmaking` 속성 추가 |
| `GameEndUI.cs` | 모드별 다시하기 버튼 처리, RematchRequestPopup 연동 |
| `RematchRequestPopup.cs` | 신규 — 재경기 요청 팝업 |
| `Game.unity` | RematchRequestPopup 오브젝트 추가, Inspector 연결 |

---

## 구현 1: NetworkGameManager.cs

```csharp
// StartMatchmakingAsync 성공 시 true, 그 외 false
public bool IsRandomMatchmaking { get; private set; }

// StartMatchmakingAsync() 내 matchId 확보 후:
IsRandomMatchmaking = true;

// DisconnectAsync() / Shutdown 시:
IsRandomMatchmaking = false;
```

---

## 구현 2: NetworkGameEndController.cs

### 추가 필드

```csharp
private ulong _rematchRequesterId = ulong.MaxValue;
```

### AnnounceWinnerClientRpc 변경

```csharp
[ClientRpc]
private void AnnounceWinnerClientRpc(int winnerTeamIndex, bool isRandomMatch)
{
    // 랜덤매칭: 다시하기 버튼 숨김
    // 커스텀게임: 다시하기 버튼 → RequestRematch 콜백 교체
    _gameEndUI.SetupRematchButton(isRandomMatch, OnRequestRematch);
    _gameEndUI.ShowResult(winnerTeam, LocalPlayerTeam.Current);
}
```

### 재경기 요청 RPC

```csharp
// 요청자 → 서버
[ServerRpc(RequireOwnership = false)]
public void RequestRematchServerRpc(ServerRpcParams rpcParams = default)
{
    ulong requesterId = rpcParams.Receive.SenderClientId;

    if (_rematchRequesterId == ulong.MaxValue)
    {
        // 첫 요청 — 상대방에게 팝업 전송
        _rematchRequesterId = requesterId;
        ulong otherClientId = GetOtherClientId(requesterId);
        NotifyRematchRequestedClientRpc(RpcTarget.Single(otherClientId, ...));
    }
    else
    {
        // 두 번째 요청 = 상호 동의 → 즉시 재시작
        StartRematch();
    }
}

// 서버 → 상대방 (팝업 표시)
[ClientRpc]
private void NotifyRematchRequestedClientRpc(/* TargetedRpc */) { ... }

// 상대방이 수락
[ServerRpc(RequireOwnership = false)]
public void AcceptRematchServerRpc() => StartRematch();

// 상대방이 거절
[ServerRpc(RequireOwnership = false)]
public void DeclineRematchServerRpc()
{
    _rematchRequesterId = ulong.MaxValue;
    NotifyRematchDeclinedClientRpc(/* 요청자에게만 */);
}

// 재경기 시작 (서버 내부)
private void StartRematch()
{
    _rematchRequesterId = ulong.MaxValue;
    NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
}
```

### GetOtherClientId 헬퍼

```csharp
private ulong GetOtherClientId(ulong myClientId)
{
    foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
        if (id != myClientId) return id;
    return ulong.MaxValue;
}
```

---

## 구현 3: GameEndUI.cs

### 추가 필드

```csharp
[SerializeField] private TextMeshProUGUI _restartButtonText;
[SerializeField] private RematchRequestPopup _rematchRequestPopup;
```

### SetupRematchButton()

```csharp
public void SetupRematchButton(bool isRandomMatch, System.Action onRequestRematch)
{
    if (isRandomMatch)
    {
        _restartButton.gameObject.SetActive(false); // 랜덤매칭: 버튼 숨김
        return;
    }
    // 커스텀게임: 다시하기 → 재경기 요청
    _restartButton.onClick.RemoveAllListeners();
    _restartButton.onClick.AddListener(() =>
    {
        _restartButtonText.text = "요청 중...";
        _restartButton.interactable = false;
        _backToLobbyButton.interactable = false;
        onRequestRematch?.Invoke();
    });
}
```

### ShowRematchDeclined()

```csharp
public void ShowRematchDeclined()
{
    // 거절 알림 팝업 표시 (RematchRequestPopup 재활용 또는 별도 메서드)
    _rematchRequestPopup.ShowDeclined();
    // 버튼 원복
    _restartButtonText.text = "다시하기";
    _restartButton.interactable = true;
    _backToLobbyButton.interactable = true;
}
```

---

## 구현 4: RematchRequestPopup.cs (신규)

```csharp
public class RematchRequestPopup : MonoBehaviour
{
    [SerializeField] private GameObject _requestPanel;   // 수락/거절 팝업
    [SerializeField] private GameObject _declinedPanel;  // 거절 알림 팝업
    [SerializeField] private Button _acceptButton;
    [SerializeField] private Button _declineButton;
    [SerializeField] private Button _declinedConfirmButton;

    public event System.Action OnAccepted;
    public event System.Action OnDeclined;

    public void ShowRequest()
    {
        _requestPanel.SetActive(true);
        _declinedPanel.SetActive(false);
    }

    public void ShowDeclined()
    {
        _requestPanel.SetActive(false);
        _declinedPanel.SetActive(true);
    }

    public void Hide()
    {
        _requestPanel.SetActive(false);
        _declinedPanel.SetActive(false);
    }
}
```

### UI 구성

```
RematchRequestPopup (GameObject)
├── RequestPanel
│   ├── Text: "상대방이 재경기를 요청하였습니다."
│   ├── Button: 수락
│   └── Button: 거절
└── DeclinedPanel
    ├── Text: "상대방이 재경기를 거절하였습니다."
    └── Button: 확인 (닫기)
```

---

## 싱글플레이 변경 없음

`AnnounceWinnerClientRpc`는 멀티플레이 전용.
싱글플레이는 `GameEndUI.OnGameEnd()` → `OnRestartClicked()` → `LoadMap()` 그대로 유지.

---

## 위험 요소

| 위험 | 평가 | 대응 |
|------|------|------|
| NGO TargetedRpc API 버전 호환 | 중간 | ClientRpc + ClientId 파라미터로 수신 측에서 필터링도 가능 |
| 재경기 중 한 명 연결 끊김 | 낮음 | ReconnectionHandler 기존 로직 그대로 동작 |
| `_networkGameStarted` 재로드 후 미리셋 | 중간 | GameBootstrapper.Start()에서 플래그 초기화 확인 |
| Inspector 연결 누락 | 낮음 | null 체크 방어 |

---

## 테스트 체크리스트

### 커스텀게임
- [x] 게임 종료 시 다시하기 버튼 표시 확인
- [x] 다시하기 클릭 → 버튼 "요청 중..." + 비활성화, 로비로 버튼도 비활성화
- [x] 상대방에게 재경기 요청 팝업 표시 확인
- [x] 상대방 수락 → 양쪽 Game 씬 재로드, 게임 정상 시작 확인
- [x] 상대방 거절 → 요청자에게 "재경기를 거절하였습니다." 팝업, 버튼 원복 확인
- [ ] 동시 클릭 (레이스 컨디션) → 팝업 없이 즉시 재경기 시작 확인

### 랜덤매칭
- [x] 게임 종료 시 다시하기 버튼 숨김 확인
- [x] 로비로 버튼만 표시 확인

### 싱글플레이
- [x] 다시하기 동작 변경 없음 확인 (맵 즉시 리셋)
