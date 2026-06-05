# Research — NetworkGameManager 정리 (DontDestroyOnLoad 경고 + 기술 부채)

## 작업 목적 (자연어 설명)

플레이모드 실행 시 출력되는 "DontDestroyOnLoad only works for root GameObjects" 경고를 없애려다 보니,
더 근본적인 구조 문제가 발견됐습니다.

조사 결과 두 가지 기술 부채가 확인됐습니다:
1. **Game.unity에 불필요한 NetworkGameManager가 배치되어 있음** — Lobby → Game 씬 전환 시 중복 인스턴스 가능성
2. **GameBootstrapper에 선언만 있고 코드에서 전혀 쓰이지 않는 `_networkGameManager` 필드가 있음**

이 두 가지를 정리하면 DontDestroyOnLoad 경고도 자연히 해결됩니다.

---

## 현재 씬 구성 확인

### 씬 파일 저장 상태 (m_Father 기준)

| 씬 | NetworkGameManager 위치 | 비고 |
|----|------------------------|------|
| Lobby.unity | `m_Father: {fileID: 0}` → **루트** | 정상 |
| Game.unity | `m_Father: {fileID: 0}` → **루트** | 존재 자체가 문제 |

두 씬 모두 저장된 파일 기준으로는 루트에 있음.
에디터에서 `[Managers]` 아래로 보이는 건 **미저장 변경사항** 때문.

---

## 발견된 문제

### 문제 1 — Game.unity에 불필요한 NetworkGameManager 존재

**설계 의도**: NetworkGameManager는 Lobby.unity에서 생성되고 `DontDestroyOnLoad`로 씬 전환 후에도 유지되는 오브젝트.

**실제 상황**:
- Game.unity에도 NetworkGameManager GameObject가 배치되어 있음
- 씬 로드 시 `m_IsActive: 1` 오브젝트는 코드 참조 여부와 무관하게 `Awake()` 자동 호출됨
- Lobby → Game 전환 시 흐름:
  1. Lobby NGM → DontDestroyOnLoad → 살아남음
  2. Game.unity 로드 → Game NGM `Awake()` 실행 → `DontDestroyOnLoad` 호출
  3. NGM 인스턴스 2개 공존
  4. `FindFirstObjectByType<NetworkGameManager>()` → 어느 쪽이 반환될지 불확정

**현재 증상이 없는 이유 (추정)**:
Game.unity의 NGM은 내부 상태(로비 연결, Relay 등)가 없는 빈 상태로 `Awake()`만 실행되어 조용히 공존하고 있을 가능성이 높음. 하지만 구조적으로 취약한 상태.

---

### 문제 2 — GameBootstrapper._networkGameManager 고아 필드

```csharp
// GameBootstrapper.cs line 110-112
[Header("Network")]
[Tooltip("네트워크 게임 세션 관리 컴포넌트 (씬에 NetworkGameManager GameObject 배치 후 연결)")]
[SerializeField] private Hexiege.Infrastructure.NetworkGameManager _networkGameManager;
```

**4개 partial 파일 전체 grep 결과**: 선언 이외 사용처 없음.
- `GameBootstrapper.cs` — 선언만
- `GameBootstrapper.Setup.cs` — 없음
- `GameBootstrapper.Map.cs` — 없음
- `GameBootstrapper.Network.cs` — 없음

네트워크 활성 여부 판단은 `NetworkContext.IsNetworkActive`로, 네트워크 흐름 제어는 `_networkGameFlow`로 대체됨.
`_networkGameManager`는 과거 코드에서 쓰였다가 리팩토링으로 대체된 뒤 필드 선언만 남은 것으로 판단됨.

---

## NetworkGameManager 실제 사용 구조

NetworkGameManager를 실제로 사용하는 파일들:

| 파일 | 사용 방식 | 목적 |
|------|----------|------|
| `LobbyUI.cs` | SerializeField + FindFirstObjectByType 폴백 | Host/Join 호출, 이벤트 구독 |
| `LobbyRootView.cs` | SerializeField + FindFirstObjectByType 폴백 | 이벤트 구독 |
| `NetworkStatusUI.cs` | SerializeField + FindFirstObjectByType 폴백 | RTT 표시, 연결 끊김 감지 |
| `GameEndUI.cs` | SerializeField | BackToLobby() 호출 |
| `BattleViewModel.cs` | 생성자 주입 | 이벤트 구독 |

→ 모두 Lobby.unity에서 생성된 단일 NGM 인스턴스를 참조하는 구조.
→ Game.unity의 NGM은 아무도 의미있게 사용하지 않음.

---

## DontDestroyOnLoad 경고 발생 원인 결론

에디터에서 `[Managers]` 아래 자식으로 배치된 상태(미저장)에서 플레이모드 실행 시 발생.
저장된 씬 파일 기준으로는 이미 루트이지만, **에디터 현재 상태가 달라서 발생**.

근본 원인: Game.unity에 불필요한 NGM이 존재하고, 현재 에디터 작업 중에 [Managers] 아래로 이동된 상태.

---

## 현재 상태 요약

| 항목 | 상태 | 심각도 |
|------|------|--------|
| DontDestroyOnLoad 경고 | 에디터 미저장 상태에서 발생 | 낮음 |
| Game.unity NGM 중복 | 잠재적 인스턴스 중복 | 중간 |
| `_networkGameManager` 고아 필드 | 완전 미사용 | 낮음 |
| 현재 게임 기능 | 정상 동작 중 | 안전 |
