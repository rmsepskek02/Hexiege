# Research: 게임포기 시 로딩 인디케이터가 사라지지 않는 버그

## 작업 개요

멀티플레이에서 게임포기(Forfeit)를 하면 게임 종료 UI(GameEndUI)가 표시되어야 하는데,
그 위에 로딩 인디케이터(스피너)가 덮인 채로 사라지지 않는 버그입니다.
로딩 인디케이터가 GameEndUI를 가려서 사용자가 결과창을 제대로 볼 수 없습니다.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs` | 포기 버튼 처리. 포기 확정 시 로딩 인디케이터를 켬 |
| `Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs` | 게임 종료 결과창 표시 |
| `Assets/_Project/Scripts/Presentation/UI/UIManager.cs` | 전역 로딩 인디케이터(ShowLoading) 관리 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs` | 멀티플레이 포기 RPC 처리 |
| `Assets/_Project/Scripts/Presentation/UI/Common/LoadingScreen.cs` | DontDestroyOnLoad 로딩 스크린 (씬 로드 시 자동 숨김) |

---

## 버그 재현 경로

1. 멀티플레이 게임 진행 중
2. 인게임 설정 메뉴 → 포기 버튼 클릭
3. 확인 팝업에서 "포기" 선택
4. → GameEndUI(게임 종료 결과창)가 나타나야 하지만, 로딩 인디케이터가 그 위를 가리고 사라지지 않음

---

## 근본 원인

### 1. 로딩 인디케이터를 켜는 지점

`InGameSettingsUI.cs` - `OnForfeitConfirmed()` (333~334번 줄):

```csharp
if (NetworkContext.IsNetworkActive)
    UIManager.Instance?.ShowLoading(true, "게임을 포기하는 중...");
```

이 코드의 의도(주석에 명시):
> "멀티플레이 포기는 서버 RPC 왕복 + 결과 동기화/씬 전환까지 시간이 걸리므로
>  로딩을 끄는 책임은 목적지 씬 Bootstrapper가 담당한다(UI 규칙 L-3)."

**즉, 포기 후 씬 전환이 일어날 것이라고 가정하고 로딩을 켰습니다.**

### 2. 실제 포기 흐름 (씬 전환 없음)

```
OnForfeitConfirmed()
  → UIManager.ShowLoading(true, "게임을 포기하는 중...")   ← 로딩 켬
  → NetworkGameEndController.RequestForfeit()
      → ForfeitServerRpc (서버 RPC 전송)
          [서버 처리]
          → GameEvents.OnGameEnd 발행
          → AnnounceWinnerClientRpc
              [모든 클라이언트]
              → GameEndUI.OnGameEnd() 호출
                  → GameEndUI 패널 표시 ← 같은 씬 안에서 표시됨! (씬 전환 없음)
```

**포기 흐름은 씬 전환 없이 같은 Game 씬 안에서 GameEndUI를 표시합니다.**

### 3. 로딩이 꺼지지 않는 이유

`UIManager.ShowLoading`으로 켠 로딩 인디케이터는 다음 두 경우에만 꺼집니다:
- `UIManager.ShowLoading(false)` 직접 호출
- *(씬 전환 관련 자동 해제 로직 없음 — UIManager는 LoadingScreen과 별개의 컴포넌트)*

포기 흐름에서 `UIManager.ShowLoading(false)`를 호출하는 코드가 어디에도 없습니다:
- `GameEndUI.OnGameEnd()` → 호출 없음
- `GameEndUI.ShowResult()` → 호출 없음
- `GameUIManager.NotifyGameEnded()` → 호출 없음
- `NetworkGameEndController.AnnounceWinnerClientRpc()` → 호출 없음

### 4. LoadingScreen과 UIManager.ShowLoading의 차이

혼동하기 쉬운 두 컴포넌트:

| 컴포넌트 | 위치 | 씬 로드 시 자동 숨김 |
|---------|------|---------------------|
| `LoadingScreen` | `Presentation/UI/Common/` | ✅ `OnSceneLoaded`로 자동 숨김 |
| `UIManager._loadingIndicator` | `Presentation/UI/UIManager.cs` | ❌ 자동 숨김 없음, `ShowLoading(false)` 직접 호출 필요 |

포기 흐름에서 켜는 것은 `UIManager.ShowLoading`이며, 이건 씬 전환 자동 해제가 없습니다.
설령 씬 전환이 있었더라도 `UIManager.ShowLoading`은 자동으로 꺼지지 않습니다.

---

## 영향 범위

- **싱글플레이 포기**: `NetworkContext.IsNetworkActive == false`이므로 `ShowLoading`을 호출하지 않음 → **정상**
- **멀티플레이 포기**: `ShowLoading(true)` 호출 후 끄는 코드 없음 → **버그 발생**
- **멀티플레이 로비 복귀(ReturnToLobby)**: `ShowLoading(true)` 후 씬 전환 → 씬 전환 뒤 `LobbyRootView`가 끔 → **정상** (이건 의도된 패턴)
- **재경기(Rematch)**: `OnNetworkRematchStarting` 이벤트 → `ShowLoading(true)` → 씬 재로드 → `OnSceneLoaded`... 아니 이것도 `UIManager.ShowLoading`이므로 확인 필요

---

## 수정 방향 (안)

### 안 A: GameEndUI에서 로딩 인디케이터 끄기
`GameEndUI.OnGameEnd()` 내부에 `UIManager.Instance?.ShowLoading(false)` 추가.

- 장점: 게임 종료 시 항상 확실하게 꺼짐
- 단점: GameEndUI가 "로딩을 끄는 책임"을 갖게 되어 의도를 모르면 의아하게 보일 수 있음

### 안 B: InGameSettingsUI에서 ShowLoading 자체를 제거
포기 시 로딩 인디케이터를 아예 안 켜는 것.

- 장점: 코드 단순화
- 단점: 서버 RPC 응답 대기 중 사용자가 빈 화면을 보게 될 수 있음 (실제 딜레이는 미미하지만)

### 안 C: GameUIManager.NotifyGameEnded()에서 로딩 끄기
게임 종료 알림 시 `UIManager.Instance?.ShowLoading(false)` 추가.

- 장점: 게임 종료 시 공통 처리
- 단점: 씬 전환을 동반한 종료(ReturnToLobby)에서는 중복 호출이 될 수 있으나 부작용은 없음

**추천: 안 A 또는 안 C** — 수정 범위가 명확하고 안전함.
