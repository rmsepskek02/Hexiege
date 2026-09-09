# Research — 무작위 맵 3단계: 멀티플레이 맵 전송 · 해시 대조 · 재경기 맵 선택

작성일: 2026-09-08
작업 폴더: `Assets/_Project/Docs/_Tasks/2026-09-08/17_29_random-map-phase3-multiplayer/`
선행 작업:
- `Assets/_Project/Docs/_Tasks/2026-09-01/19_49_random-map-phase1-tilekind/` (1단계 — 완료)
- `Assets/_Project/Docs/_Tasks/2026-09-03/03_14_random-map-phase2-generator/` (2단계 — A~K 완료 · **싱글플레이만** 실기 검증)

---

## 0. 이 작업이 무엇이고 왜 하는지 (자연어 설명 — CLAUDE.md 규칙 13)

지금 이 게임은 **혼자 하는 경기에서만** 매번 다른 전장이 나옵니다. 2단계에서 맵을 만들어내는 기계와 그 맵을 실제 전장으로 옮기는 일까지 끝냈고, 사용자가 직접 플레이해서 매 판 다른 맵이 나오는 것을 눈으로 확인했습니다.

**둘이서 하는 경기는 아직 그렇지 않습니다.** 두 사람이 각자 자기 기기에서 맵을 만들면 서로 다른 전장에서 싸우게 되므로, 지금은 임시로 **둘 다 똑같은 고정된 값(1번)으로 맵을 만들게** 해 두었습니다. 같은 값을 넣으면 같은 맵이 나온다는 성질에 기대어 "일단 어긋나지는 않게" 막아 둔 것입니다. 그 대가로 **멀티플레이는 지금 매 판 완전히 같은 맵**이 나옵니다.

3단계는 이 임시 조치를 걷어내고 **제대로 된 방식**으로 바꾸는 일입니다. 제대로 된 방식이란 이렇습니다.

- **방을 만든 사람(Host)이 맵을 만드는 유일한 주인**이 됩니다. 상대는 맵을 만들지 않습니다.
- Host가 만든 맵을 **상대에게 통째로 보내 줍니다.** 상대는 받은 맵을 그대로 씁니다 — 같은 값으로 다시 만들어 보는 것이 아닙니다.
- 보낸 맵과 받은 맵이 **정말 한 글자도 다르지 않은지**, 맵 전체를 요약한 지문(해시)을 서로 맞춰 봅니다. **지문이 같을 때만 전투 화면으로 넘어갑니다.**
- 보내다 실패하거나 지문이 다르면 **전투 화면으로 넘어가지 않고 대기실에 그대로 머무릅니다.** 그리고 "맵 준비에 실패했습니다"라고 알리고 다시 시도할지 나갈지를 묻습니다.
- 경기가 끝난 뒤 한 판 더 할 때는 **"같은 맵으로" / "새 맵으로"** 중에 고를 수 있게 합니다. 상대에게는 어느 쪽으로 요청했는지가 보이고, 상대가 그 조건을 확인한 뒤 수락합니다.

**이 문서는 그 일을 시작하기 전에, 지금 코드와 규칙 문서가 실제로 어떤 상태인지 직접 열어 본 기록입니다.** 계획서(`Plan.md`)가 아니며, **무엇을 어떻게 만들지는 정하지 않습니다.** "지금은 이렇고, 규칙은 이걸 요구하고, 그래서 이런 선택지와 대가가 있다"까지만 적습니다. 결정은 계획 단계와 사용자 승인의 몫입니다.

이번 조사에서 가장 크게 드러난 것 세 가지를 먼저 적어 둡니다.

1. **맵을 보낼 자리가 지금 아예 없습니다.** 대기실(Lobby) 화면에는 **네트워크로 말을 주고받을 수 있는 물체가 하나도 없습니다**(실측 0개). 맵 전송은 대기실에서 시작해 전투 화면으로 넘어가도 살아남아야 하는데, 그런 물체를 새로 만드는 것부터가 3단계의 첫 일입니다.
2. **맵 데이터는 생각보다 훨씬 작습니다 — 실측 335~343바이트입니다.** 한 번에 실어 보낼 수 있는 한도(현재 설정 6,144바이트)의 **6% 미만**입니다. 즉 "조각으로 쪼개 보낸다"는 규칙은 지키되, 실제로는 **조각이 한 개뿐**이 됩니다. 이 사실을 알고 시작하는 것과 모르고 시작하는 것은 설계가 달라집니다.
3. **사용자에게 물어야 할 기획 결정이 4건 남아 있습니다.** 규칙 문서가 일부러 "구현 시 확정한다"고 비워 둔 자리입니다. 계획서를 쓰기 전에 이 4건이 정해져야 합니다 — 자세한 것은 **§8**을 보십시오.

---

## 1. 이 문서가 답하는 질문과, 답을 얻은 방법

| 절 | 답하는 질문 | 근거 |
|---|---|---|
| §2 | 3단계에 남은 범위가 정확히 무엇인가 | `ROADMAP.md` 3단계 행 · `GameSystemRules_RandomMap.md` 규칙 14·16 · 2단계 `Plan.md` §13 |
| §3 | 현행 재경기 흐름은 실제로 어떻게 도는가 | 코드 5개 파일 통독 (`NetworkGameEndController` · `GameEndUI` · `RematchRequestPopup` 외) |
| §4 | 맵 전송이 붙을 자리는 어디인가 | `Lobby.unity` · `Game.unity` 실측 + 네트워크 클래스 21개 전수 분류 |
| §5 | 조각 크기·전체 한도를 어떻게 실측할 것인가 | `.bytes` 5개 파이썬 디코드 + `MapDefinitionCodec` 통독 + `.unity` 전송 설정 실측 |
| §6 | 해시 대조의 재료는 어디서 꺼내는가 | `MapPreparationResult` · `GameBootstrapper` 필드/접근자 실측 |
| §7 | seed 전송은 순서 문제가 있는가 | `LoadMap` 호출 지점 2곳 역추적 |
| §8 | 실패 UI 중 미정 항목이 정확히 무엇인가 | `GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 M-1~M-4 전문 + 기존 UI 자산 실측 |
| §9 | `MapVersion` 값은 어디서 오는가 | `MapDefinition.cs` · `MapDefinitionCodec.cs` |
| §10 | 2단계 보류 8건 중 3단계에 들어오는 것은 무엇인가 | 2단계 `Plan.md` §13 8행 전수 대조 |
| §11 | 아키텍처 제약과 충돌하는 지점은 어디인가 | 레이어별 `using` 전수 검색 |
| §12 | 문서끼리 어긋난 곳이 있는가 | 규칙 문서 · `TechnicalDesignDocument.md` 대조 |
| §13 | 확인하지 못한 것은 무엇인가 | — |

> **근거 구분(CLAUDE.md 규칙 10)**: 이 문서에서 **「실측」**은 내가 이번에 파일을 열어 직접 센 값이다. **「문서 인용」**은 규칙·설계 문서에 적힌 값이다. **「미확인」**은 확인하지 못한 것이며 추정으로 채우지 않았다. 세 가지를 각 항목에 표시했다.

---

## 2. 3단계에 남은 범위 — 문서가 정한 것

### 2-1. `ROADMAP.md` 3단계 행이 정한 범위 (문서 인용)

`Assets/_Project/Docs/ROADMAP.md` 우선순위 표 3단계 행 기준.

| 기호 | 항목 | 상태 |
|---|---|---|
| ① | AI 건물 배치 후보 판정 전환 | ✅ **2단계 I 단계(`c47a067`)에서 처리 완료 — 3단계 범위가 아니다** |
| ② | 건설·점령 전용 조건 전환 | ✅ **2단계 I 단계에서 처리 완료 — 3단계 범위가 아니다** |
| ③ | canonical chunk 전송 · `SameMap`/`NewMap` · 경로 완전 차단 대응 | 🔴 남음 |
| ⓐ | 멀티 seed 전송 (H 단계 임시 고정값 제거) | 🔴 남음 |
| ⓑ | 2단계 결과의 **멀티플레이 실기 검증** | 🔴 남음 |

### 2-2. 3단계 관련 코드가 하나도 없다 (실측 — 전수 검색 0건)

`Assets/_Project/Scripts` 하위 `*.cs` 전체 검색 결과.

| 식별자 | 건수 |
|---|---:|
| `NetworkMapTransfer` | **0** |
| `RematchMapMode` | **0** |
| `SameMap` / `NewMap` | **0** / **0** |
| `MapPrepareBegin` / `MapChunk` / `MapReady` | **0** / **0** / **0** |
| `matchNonce` / `chunkCount` | **0** / **0** |

**즉 전송 경로는 개조가 아니라 전면 신설이다.** 반면 **재경기 흐름 자체는 이미 있다**(§3) — 3단계는 기존 재경기 위에 맵 선택을 얹는 형태가 된다. 규칙 14도 *"기존 재경기의 요청·수락·거절과 Game 씬 재로드 흐름은 유지하고, 요청 조건에 `RematchMapMode`를 추가한다"* 고 그렇게 정한다.

### 2-3. 「경로 완전 차단 대응」은 이 문서의 범위가 아니다 (문서 인용)

`GameSystemRules_RandomMap.md` **규칙 11**은 건물로 경로가 완전히 막혔을 때의 **유닛 동작**을 다루며, 그 단일 소스를 **`GameSystemRules_Units.md` 규칙 45**로 넘긴다 — *"그것은 맵의 성질이 아니라 유닛의 행동이므로 유닛 문서가 단일 소스다."* 따라서 ROADMAP 3단계 행의 ③에 묶여 있지만 **맵 전송과는 별개의 작업**이다. 이 문서는 전송·해시·재경기만 조사했고 규칙 45는 조사 대상에 넣지 않았다(→ §13).

---

## 3. 현행 재경기 흐름의 실제 동작 (규칙 14가 「유지한다」고 말한 그 흐름)

### 3-1. 멀티플레이 재경기 — 소유 관계와 신호 경로 (실측)

```
[요청자] GameEndUI 「다시하기」 버튼
   → GameEvents.OnLocalRematchRequested 발행            (Presentation → Application 이벤트)
   → NetworkGameEndController 가 구독해 RequestRematchServerRpc() 전송
                                                        (Infrastructure/NetworkGameEndController.cs:344)
[서버] 첫 요청이면 _rematchRequesterId 에 기록
   → NotifyRematchRequestedClientRpc(상대에게만)
   → [상대] GameEvents.OnNetworkRematchRequested 발행
   → RematchRequestPopup 이 구독해 ShowRequest()        (Presentation/UI/Common/RematchRequestPopup.cs:199)
[상대] 수락 → GameEvents.OnLocalRematchAccepted → AcceptRematchServerRpc()  (:395) → StartRematch()
[상대] 거절 → GameEvents.OnLocalRematchDeclined → DeclineRematchServerRpc() (:406)
             → _rematchRequesterId 초기화 + NotifyRematchDeclinedClientRpc(요청자에게만)
[양측 동시 요청] 두 번째 ServerRpc 가 곧바로 StartRematch()                  (:375)
```

`StartRematch()` (`NetworkGameEndController.cs:447`) 가 하는 일은 정확히 세 가지다.

1. `_rematchRequesterId` 초기화.
2. **동적 스폰 `NetworkObject` 전수 Despawn** — `IsSceneObject == false` 인 것만 지운다(씬 배치 오브젝트는 NGO가 알아서 처리하므로 건드리지 않는다).
3. `NotifyRematchStartingClientRpc()` 로 로딩 표시 신호를 뿌린 뒤 **`SceneManager.LoadScene("Game", LoadSceneMode.Single)`** (`:495`).

**규칙 14의 `RematchMapMode` 가 끼어들 자리는 이 흐름의 두 지점이다** (조사 결과이며 「이렇게 하자」는 결정이 아니다).

| 지점 | 현재 | 규칙 14가 요구하는 것 |
|---|---|---|
| `RequestRematchServerRpc()` 인자 | **없음** (`ServerRpcParams` 뿐) | 요청 조건에 `RematchMapMode` 추가 |
| `NotifyRematchRequestedClientRpc()` 인자 | **없음** (`ClientRpcParams` 뿐) | 상대 팝업에 같은 맵/새 맵 조건 **표시** |
| `_rematchRequesterId` 상태 | `ulong` 한 개 | *"서버가 먼저 접수한 요청을 현재 제안으로 확정"* + *"뒤에 도착한 다른 조건의 요청은 기존 제안을 덮어쓰지 않는다"* → **조건까지 함께 보관해야 한다** |
| `StartRematch()` 직전 | 곧바로 씬 재로드 | `NewMap` 이면 **생성·전송·검증을 끝낸 뒤에만** 재로드 |

⚠️ **현재 「양측 동시 요청 → 즉시 시작」 분기는 규칙 14와 정면으로 어긋난다.** 규칙 14: *"양측이 서로 다른 mode로 동시에 요청해도 자동 시작하지 않는다. … 다른 플레이어가 그 조건을 확인해 명시적으로 수락해야 한다."* 현재 코드는 조건 개념이 없으므로 두 번째 요청을 받는 즉시 `StartRematch()` 한다(`NetworkGameEndController.cs:375`). **조건이 같을 때도 자동 시작이 금지되는지, 다를 때만 금지되는지는 규칙 문언만으로는 확정할 수 없다**(→ §13).

### 3-2. UI 자산이 규칙 14를 아직 담지 못한다 (실측)

| 컴포넌트 | 현재 가진 것 | 규칙 14·M-3 이 요구하는 것 |
|---|---|---|
| `GameEndUI` | `_restartButton`(「다시하기」) · `_backToLobbyButton` — **버튼 2개** (`GameEndUI.cs:54`, `:67`) | `SameMap` · `NewMap` · `Lobby` — **선택지 3개** |
| `RematchRequestPopup` | `_requestPanel` · `_acceptButton` · `_declineButton` · `_declinedPanel` · `_declinedConfirmButton` — **텍스트 필드가 없다** (`:32`~`:45`) | 같은 맵/새 맵 **조건을 표시**해야 하므로 텍스트 슬롯이 필요하다 |
| 자동 로비 복귀 countdown | `_autoReturnSeconds = 30f` (`GameEndUI.cs:73`) | *"전체 길이의 자동 로비 복귀 countdown"* 복원 → **전체 길이 = 30초**로 확정 가능 |

### 3-3. 싱글플레이 재경기는 **씬을 재로드하지 않는다** (실측 — 멀티와 메커니즘이 다르다)

`GameEndUI.OnRestartClicked()` (`:254`) 는 씬을 건드리지 않고 **`_bootstrapper.LoadMap(HexOrientation.FlatTop)` 를 직접 호출**한다(`:266`). 그리고 `LoadMap` 안의 `CreateRootSeed()` 는 싱글에서 매번 새 값을 뽑으므로 —

> 🔴 **싱글플레이 재경기는 이미 사실상 `NewMap` 고정이며, `SameMap` 에 해당하는 경로가 코드에 없다.**

규칙 14 마지막 문단은 *"싱글플레이 결과 화면도 같은 맵/새 맵 선택을 제공한다"* 고 정하므로, **싱글 쪽도 3단계에서 손을 대야 한다.** 이것은 「멀티 작업의 곁가지」가 아니라 규칙이 명시적으로 요구하는 항목이다.

또한 재경기 메커니즘이 **싱글=`LoadMap` 재호출 / 멀티=씬 재로드**로 갈려 있다는 사실은 규칙 14의 *"현재 맵 정의와 재경기 제안을 보관하는 재경기 컨텍스트는 Game 씬 재로드 사이에는 유지"* 라는 요구가 **두 경로에서 서로 다른 의미**가 됨을 뜻한다(싱글은 애초에 씬이 유지되므로 보관 문제가 없다).

---

## 4. 맵 전송이 붙을 자리 — 「씬에 종속되지 않는」 것이 지금 무엇인가

### 4-1. 규칙이 금지하는 것 (문서 인용 — `GameSystemRules_RandomMap.md` 규칙 16)

- *"최초 경기와 `NewMap` 재경기는 모두 **씬에 종속되지 않는 공용 전송 경로**를 사용한다. **씬에 묶인 단일 RPC 하나로 전체 맵을 보내지 않는다.**"*
- *"맵 준비가 실패하거나 해시가 다르면 **전투 씬으로 이동하지 않고 기존 로비를 유지**한다."*

두 번째 문장이 첫 번째보다 강한 제약이다 — **해시 대조가 전투 씬 전환보다 먼저 끝나야 하므로, 전송은 로비에서 완결돼야 한다.**

### 4-2. 🔴 로비 씬에는 `NetworkObject` 가 **0개**다 (실측)

`Assets/_Project/Scenes/*.unity` 문자열 실측:

| 씬 | `NetworkObject` 등장 수 | `NetworkManager` | `NetworkGameManager` |
|---|---:|:---:|:---:|
| `Lobby.unity` | **0** | 있음 | 있음 |
| `Game.unity` | **13** | 있음 | (Lobby에서 `DontDestroyOnLoad` 로 넘어옴) |

**즉 지금 로비에서는 RPC를 보낼 수단이 하나도 없다.** `NetworkBehaviour` 를 상속한 클래스 15개는 **전부 Game 씬 배치 오브젝트**다(`ReconnectionHandler` 도 `Game.unity` 1건 · `Lobby.unity` 0건으로 실측 확인).

### 4-3. 네트워크 계열 21개 파일의 생명주기 분류 (실측 — 전수)

| 분류 | 클래스 | 생명주기 |
|---|---|---|
| **`MonoBehaviour` + `DontDestroyOnLoad`** | `NetworkGameManager` (`:37` 클래스 선언 · `:104` `DontDestroyOnLoad(gameObject)`) | **로비 → 전투 → 재경기 전 구간 생존.** 단 `NetworkBehaviour` 가 아니라 **RPC를 스스로 보낼 수 없다** |
| **순수 C# 클래스** (씬 무관) | `LobbyManager`(`:35`) · `MatchmakerManager`(`:36`) · `RelayManager`(`:33`) · `UnityServicesInitializer`(`:41`) | `NetworkGameManager` 가 소유. RPC 불가 |
| **`static` 홀더** | `LocalPlayerTeam` · (Application의) `NetworkContext` | 상태 보관만 |
| **`NetworkBehaviour` — Game 씬 배치** | `NetworkGameFlow` · `NetworkGameEndController` · `NetworkCombatController` · `NetworkBuildingController` · `NetworkProductionController` · `NetworkResourceSync` · `NetworkTileSync` · `NetworkHealthSync` · `NetworkSkillController` · `NetworkUpgradeController` · `NetworkMistShrineController` · `NetworkUnitMovementController` · `ReconnectionHandler` · `NetworkUnit` | **씬 재로드 때 사라지고 다시 스폰된다** |

### 4-4. 후보와 각각의 대가 (조사 결과 — 결정하지 않는다)

**어느 것도 지금 그대로는 규칙 16을 만족하지 못한다.** 세 방향이 보이며 각각 대가가 다르다.

| 후보 | 생명주기 | 얻는 것 | 대가 / 미확인 |
|---|---|---|---|
| **A. 신규 `NetworkObject` 프리팹을 Host가 동적 스폰** (`NetworkMapTransfer`) | Host가 `StartHost()` 직후 스폰 → 씬 재로드를 건너 살아남게 하려면 NGO의 씬 파괴 대상에서 빼야 한다 | 규칙 16의 「씬 비종속」을 문자 그대로 만족. `NewMap` 재경기에서도 같은 객체 재사용 | **NGO 2.9.2에서 동적 스폰 오브젝트가 `LoadSceneMode.Single` 재로드를 넘어 생존하는 정확한 조건은 이 조사에서 확인하지 못했다**(패키지 소스가 이 환경에 없다 — `Library/PackageCache` 부재). `DefaultNetworkPrefabs.asset` 에 **매니저류 프리팹 등록 사례가 0건**이라 프로젝트 내 선례도 없다 |
| **B. 로비 씬에 `NetworkObject` 를 새로 배치** | 로비 씬 오브젝트 → **전투 씬으로 넘어가면 사라진다** | 스폰 절차가 필요 없다 | **`NewMap` 재경기(전투 씬에서 시작)를 담지 못한다** — 규칙 16의 「최초 경기와 `NewMap` 이 같은 경로」를 정면으로 위반 |
| **C. `NetworkGameManager` 를 `NetworkBehaviour` 로 승격** | 이미 `DontDestroyOnLoad` 라 전 구간 생존 | 새 객체가 필요 없다 | `NetworkGameManager` 는 **로비 UI가 직접 참조하는 959행짜리 매니저**다. `NetworkBehaviour` 가 되면 스폰 전에는 RPC가 불가능해 **초기화 순서 전체가 영향권**에 들어간다. 회귀 위험이 가장 크다 |

> 🔴 **후보 A가 규칙 문언에 가장 가깝지만, 그것이 실제로 성립하는지는 NGO 동작 확인이 선행돼야 한다.** 이 확인 없이 계획을 세우면 §5의 「근거 없는 숫자」와 같은 부류의 실수가 된다 → §13-(1).

### 4-5. 전송이 끼어들 시점 — 현재 씬 전환 트리거 (실측)

```
BattleViewModel.OnClientConnected()                (Presentation/UI/ViewModels/BattleViewModel.cs:299)
  └ ConnectedPlayers >= 2
      ├ UIManager.Instance?.ShowLoading(true, "게임에 접속하는 중...")
      └ _networkManager.LoadGameScene()            (:305)
            └ NetworkGameManager.LoadGameScene()   (Infrastructure/Network/NetworkGameManager.cs:770)
                  └ SceneManager.LoadScene("Game", LoadSceneMode.Single)
```

**규칙 16의 「해시가 같을 때만 전투 씬 전환을 시작한다」는 이 `LoadGameScene()` 호출 앞에 게이트를 세우라는 뜻이 된다.** 게이트를 어디에 둘지(ViewModel / `NetworkGameManager` / 새 전송 객체)는 계획의 몫이다.

---

## 5. 🔴 조각 크기와 전체 한도 — 실측한 것과 못 잰 것

규칙 16은 **일부러 숫자를 적지 않았다**: *"조각 크기와 전체 한도의 숫자는 이 문서에 적지 않는다. … 값은 구현 시 NGO 실측으로 확정하고 근거와 함께 `TechnicalDesignDocument.md` 에 기록한다."* 그래서 이 절은 **값을 정하지 않고, 무엇을 어떻게 재야 하는지**만 확정한다.

### 5-1. ✅ 실측 — 맵 canonical 바이트의 **실제 크기** (재현 가능)

폴백 템플릿 5개(`Assets/_Project/Resources/MapTemplates/*.bytes`)는 **`MapDefinitionCodec.Encode` 가 만든 canonical 바이트 그 자체**다. 파일 크기가 곧 payload 크기다.

| 파일 | 크기(바이트) | 디코드 결과 |
|---|---:|---|
| `MapTemplate_FullyOpen.bytes` | **343** | ver 1 · 11×21 · 중립 광산 **6** · 장식 0 |
| `MapTemplate_ObstacleOpen.bytes` | **343** | ver 1 · 11×21 · 중립 광산 **6** · 장식 0 |
| `MapTemplate_Outer.bytes` | **343** | ver 1 · 11×21 · 중립 광산 **6** · 장식 0 |
| `MapTemplate_ThreeLane.bytes` | **343** | ver 1 · 11×21 · 중립 광산 **6** · 장식 0 |
| `MapTemplate_Canyon.bytes` | **335** | ver 1 · 11×21 · 중립 광산 **4** · 장식 0 |

**측정 방법(재현용)**: 파이썬으로 `MapDefinitionCodec.Encode` 의 필드 순서(`MapDefinitionCodec.cs:53`~`:119`)를 그대로 따라 little-endian 파싱하고, 마지막에 소비한 오프셋이 파일 길이와 정확히 일치하는지 확인했다 — 5개 모두 `consumed == len(file)` 로 **잔여 바이트 0**이다. 즉 이 크기는 추정이 아니라 **형식이 끝까지 맞아떨어진 값**이다.

**크기 공식(코드에서 유도 — `Encode` 본문 그대로)**

```
319 + 4 × (중립 광산 수) + 20 × (장식 수)
  = 40 (상위 필드 9개: int32 × 8 = 32, uint64 × 1 = 8)
  + 231 (타일 11×21, 한 칸 1바이트)
  + 20 (성  : 개수 int32 + 2개 × 8바이트)
  + 20 (시작 광산 : 개수 int32 + 2개 × 8바이트)
  + 4 + 4×N (중립 광산 : 개수 int32 + N × 4바이트)
  + 4 + 20×D (장식 : 개수 int32 + D × 20바이트)
```

검산: N=6·D=0 → 343 ✓ / N=4·D=0 → 335 ✓ (위 표와 일치)

**따라서 현 구현에서 payload 는 중립 광산 수 1~6에 대해 `323 ~ 343바이트` 범위에 갇혀 있다**(장식 0 — 규칙 3·12가 *"최초 구현의 장식 목록은 항상 비어 있다"* 고 확정).

### 5-2. ✅ 실측 — 전송 한도 설정값

`UnityTransport` 설정을 `.unity` 에서 직접 읽었다.

| 항목 | `Lobby.unity` | `Game.unity` |
|---|---:|---:|
| `m_MaxPayloadSize` | **6144** (`Lobby.unity:7475`) | **6144** (`Game.unity:45469`) |
| `m_MaxPacketQueueSize` | 128 (`:7474`) | 128 (`:45468`) |
| `EnableSceneManagement` | 1 | 1 |
| `TickRate` | 30 | 30 |

**맵 canonical 343바이트 ÷ 설정 한도 6,144바이트 = 5.6%.** 즉 **한 조각으로 충분히 들어간다.**

### 5-3. ❌ 못 잰 것 — Plan 단계에서 실측해야 하는 항목

| 무엇 | 왜 지금 못 쟀나 | 어떻게 재야 하나 |
|---|---|---|
| **NGO 2.9.2 RPC 한 번의 실제 페이로드 상한** | 패키지 소스가 이 환경에 없다(`Library/PackageCache` 부재). 코드로 확인할 수 없는 값을 문서에 적지 않는다 | 실기에서 **payload 크기를 키워 가며 보내 보고 실패하는 지점**을 찾는다. `m_MaxPayloadSize`(6144)는 **설정값이지 실효 상한이 아니다** — NGO 헤더·`FastBufferWriter` 오버헤드가 그 안에서 소비된다 |
| **Relay 경유 시의 실효 MTU** | 로컬 실측 불가 | Relay 세션(실기 2대)에서 같은 방식으로 측정. 로컬(127.0.0.1) 측정값을 Relay 값으로 삼지 않는다 |
| **reliable 전송에서 조각 여러 개를 연속 발신했을 때의 실패 지점** | 현 payload가 한 조각뿐이라 **실전에서 절대 발생하지 않는다** | 인위적으로 조각 크기를 줄여 여러 조각을 만드는 시험이 필요하다 — 다만 이는 **코드를 실행시키려고 값을 왜곡하는 것**이므로 `TechnicalDesignDocument.md` 가 이미 그 방안을 폐기했다. **시험 전용 경로로 둘지 여부 자체가 기획 결정이다** |

### 5-4. 🔴 그래서 「조각이 정말 필요한가」 — 규칙과 실측의 긴장

| 사실 | 출처 |
|---|---|
| 규칙 16: *"맵 데이터는 **조각으로 나눠 신뢰성 있게 보내고**, 모든 조각이 모여 선언된 크기와 일치한 뒤에야 해시 검증 → 역직렬화 → 공정성 검증"* | 문서 인용 |
| 규칙 16: *"**부분 데이터는 어떤 검증이나 맵 구성에도 쓰지 않는다.**"* | 문서 인용 |
| 실제 payload 343바이트 → **조각 수 1** | 실측 (§5-1) |
| `TechnicalDesignDocument.md` 도 이미 인정: *"조각이 1개뿐이어서 쪼개기·재조립·중복 무시 로직이 실전에서 한 번도 실행되지 않는다"* | 문서 인용 |

**두 사실은 모순이 아니다.** 규칙 16이 금지한 것은 정확히 *"**씬에 묶인** 단일 RPC 하나로 전체 맵을 보내는 것"* 이고, 요구한 것은 *"조각으로 나눠 **신뢰성 있게**"* 다. 조각 수가 1이어도 **`MapPrepareBegin` → `MapChunk` → `MapReady` 라는 3메시지 상태 기계는 그대로 성립**하며, 장식이 도입되면(장식 1개당 20바이트) 조각 수가 자연히 늘어난다.

**남는 질문(계획에서 답해야 함)**: 실행되지 않을 재조립 로직을 처음부터 넣을 것인가, 조각 수 1을 전제로 단순화하고 확장점만 남길 것인가. **두 선택지의 대가가 다르므로 여기서 정하지 않는다.**

---

## 6. 해시 대조의 실제 재료 — 어디서 어떻게 꺼내는가

### 6-1. 재료는 이미 다 있다 (실측)

`MapPreparationResult` (`Application/UseCases/MapPreparationUseCase.cs:128`~`:194`) 가 담고 있는 것:

| 프로퍼티 | 행 | 3단계에서의 쓰임 |
|---|---:|---|
| `Definition` (`MapDefinition`) | `:147` | 최종 맵 그 자체 |
| **`CanonicalBytes` (`byte[]`)** | `:150` | **전송할 payload 그 자체** |
| **`Hash` (`byte[]` 32바이트)** | `:153` | **대조에 쓸 원본** |
| `HashHex` (`string`) | `:156` | 로그용 |
| `MapVersion` / `RootSeed` / `MapType` | `:161` / `:164` / `:167` | 규칙 16 전달 항목 |
| `NeutralMineCount` / `StartingMineSide` | `:170` / `:173` | 규칙 16 전달 항목 |
| `MapTestModeEnabled` / `InitialGold` | `:176` / `:179` | 규칙 16 전달 항목 |
| `ElapsedMilliseconds` / `AttemptCount` / `UsedFallback` | `:188` / `:191` / `:194` | 규칙 12 로그 항목 |

`MapDefinitionCodec` 의 대조 도구도 이미 있다 — `ComputeHash(byte[])` (`Domain/Map/MapDefinitionCodec.cs:233`·`:241`) 과 **`HashEquals(byte[] a, byte[] b)`** (`:259`, 길이·전 바이트 비교).

### 6-2. 🔴 꺼내는 통로가 아직 **막혀 있다** (실측)

- `GameBootstrapper` 가 결과를 보관한다: `private MapPreparationResult _mapPreparation` (`GameBootstrapper.cs:209`), 접근자 `public MapPreparationResult GetLastMapPreparation()` (`:459`).
- **그런데 `GetLastMapPreparation()` 의 호출자는 프로젝트 전체에 0건이다**(`Assets/_Project` 전수 검색 — 정의 1건 + 주석 언급 1건뿐). 코드 주석은 *"다른 코드가 `GetLastMapPreparation()` 으로 … 다시 읽는다"* 라고 적었지만 **아직 아무도 읽지 않는다.**
- **`IGameServices` 에 이 멤버가 없다**(`Application/Interfaces/IGameServices.cs` 멤버 15개 전수 확인 — `GetGrid` · `GetResource` … `GetUnitFactory` · `StartNetworkGame`). 따라서 Infrastructure는 **인터페이스를 통해서는 준비 결과에 닿을 수 없다.**

**해시 대조는 원본 32바이트(`Hash`)로 해야 한다**(K 단계 로그가 앞 16자만 싣는 것은 로그 표기일 뿐 — `ToMapHashField()` 가 `MapHashLogLength` 로 자른다 — `GameBootstrapper.Map.cs:608`~`:614`). **원본은 그대로 살아 있다.**

### 6-3. Client 쪽 검증에 필요한 것 중 **canonical 바이트에 없는 값** (실측 — 이번 조사의 핵심 발견)

`TechnicalDesignDocument.md` 「Client 검증 순서」 4번은 Client가 **semantic fairness validator를 실행**하라고 요구한다. 그런데 —

`MapDefinitionValidator.Validate(definition, constraints, generator)` (`Domain/Map/MapDefinitionValidator.cs:299`) 는 **`constraints` 를 필수 인자로 받는다.** 그리고 그 `constraints` 는 생성 시도마다 `TryApplyTerrain(builder, terrainStream, out constraints)` 가 **지형 PRNG 스트림에서 만들어 내는 값**이다(`Domain/Map/Generators/MapArchetypeGeneratorBase.cs:136`). 그 스트림은 `(MapVersion, RootSeed, Terrain, AttemptIndex)` 로 파생된다(`:120`~`:121`).

**즉 `constraints` 를 복원하려면 `AttemptIndex` 가 필요하다.** 그런데 `Encode` (`MapDefinitionCodec.cs:53`~`:119`) 가 쓰는 필드는 `MapVersion` · `RootSeed` · `MapType` · `Width` · `Height` · `Orientation` · `NeutralMineCount` · `TestModeFlag` · `InitialGold` + 타일 + 4개 목록이 전부다 — **`AttemptIndex` 도 `StartingMineSide` 도 canonical 바이트에 없다.**

> 🔴 **결과: Client는 전달받은 canonical 바이트만으로는 `constraints` 를 복원할 수 없고, 따라서 현재 API 그대로는 `Validate()` 를 호출할 수 없다.**

**선례는 있다.** 폴백 경로가 똑같은 문제를 이미 푼다 — `MapPreparationUseCase` 가 `.bytes` 파일을 `Decode` 한 뒤 `MapFallbackTemplateFactory.CreateGenerator(MapType)` 로 생성기를 만들고, **템플릿 상수(`TemplateAttemptIndex` · `TemplateStartingMineSide` · `TemplateTestModeFlag`)로 `MapGenerationRequest` 를 재조립해 생성기를 다시 돌려 `Constraints` 를 복원**한 다음 `Validate()` 를 부른다(`MapPreparationUseCase.TryPrepareFromFallback` — `MapPreparationUseCase.cs:542`~`:678`, 생성기 재실행 `:612` · `Validate` 호출 `:664`). **폴백은 그 상수들을 알기 때문에 가능했다.** 무작위 생성 맵에는 그 상수가 없다.

**따라서 선택지는 (결정하지 않는다):**

| 선택지 | 대가 |
|---|---|
| package 헤더에 `AttemptIndex` · `StartingMineSide` 를 함께 실어 보낸다 | canonical 바이트를 건드리지 않으므로 **해시 계약은 안전**. 대신 **해시가 보호하지 않는 값**이 생긴다(위변조·손상 시 검증 불가) |
| canonical 바이트에 두 필드를 넣는다 | 해시가 보호한다. 대신 **`MapVersion` 을 올려야 하고 폴백 `.bytes` 5개를 전부 다시 만들어야 한다**(현재 파일은 새 형식이 아니다) |
| Client의 semantic 검증 범위를 `constraints` 없이 가능한 것만으로 좁힌다 | 코드 변경 최소. 대신 **`TechnicalDesignDocument.md` 「Client 검증 순서」 4번을 만족하지 못한다** — 사양 변경이므로 사용자 승인이 필요하다 |

---

## 7. seed 전송 — `LoadMap` 호출 시점과 순서 문제

### 7-1. 현재 임시 조치 (실측)

```csharp
// GameBootstrapper.Map.cs:625~648  CreateRootSeed()
if (IsNetworkMode())
    return NetworkInterimRootSeed;     // :633~634
```
`private const ulong NetworkInterimRootSeed = 1UL;` (`GameBootstrapper.cs:221`)

주석(`GameBootstrapper.cs:212`~`:220`)이 스스로 밝힌다 — *"3단계에서 Host 권위 seed 가 들어오면 이 상수는 사라진다. 즉 멀티는 당분간 매 판 같은 맵이다."*

싱글 분기(`:637`~`:647`)는 `Guid.NewGuid()` 앞 8바이트 ⊕ `DateTime.UtcNow.Ticks` 로 매 판 새 값을 만든다(`UnityEngine.Random`·`GetHashCode` 를 **의도적으로** 쓰지 않는다).

### 7-2. `LoadMap` 이 언제 누구에 의해 불리는가 (실측 — 호출 지점 3곳 전부)

| # | 호출 지점 | 모드 | 시점 |
|---|---|---|---|
| 1 | `GameBootstrapper.cs:558` | **싱글** | `Start` 계열 초기화에서 `IsNetworkMode()==false` 분기 (`:535`~`:559`) |
| 2 | `GameBootstrapper.Network.cs:92` ← `StartNetworkGame(TeamId)` (`:38`) | **멀티** | `NetworkGameFlow.StartGameClientRpc()` 가 호출 (`NetworkGameFlow.cs:191`, `:212`) |
| 3 | `GameEndUI.cs:266` | **싱글 재경기** | 「다시하기」 클릭 |

멀티 경로 전체:

```
[Game 씬 로드 완료]
  → NetworkGameFlow.OnNetworkSpawn()          (NetworkGameFlow.cs:69)
  → 각 클라: RequestReadyServerRpc(myRace)     (:148)
  → [서버] ReadyCount >= 2 → StartGameClientRpc(blueRace, redRace)   (:191)
  → 모든 클라: _services.StartNetworkGame(LocalPlayerTeam.Current)   (:212)
      → GameBootstrapper.Network.cs:92  LoadMap(FlatTop)
          → GameBootstrapper.Map.cs:110  PrepareAndProjectMap()
              → :445 CreateRootSeed()  ← 여기서 seed 가 필요하다
```

### 7-3. 🔴 순서 문제의 성격 — 「늦게 도착」이 아니라 **「너무 늦은 자리」**

`LoadMap` 이 도는 시점은 **이미 Game 씬 안**이다. 그런데 규칙 16은 **해시 대조가 씬 전환 전에 끝나야 한다**고 정한다. 두 사실을 겹치면:

> **seed가 `LoadMap` 시점에 「도착해 있는가」는 문제의 본질이 아니다.** 규칙 16을 지키면 **맵 자체가 씬 전환 전에 이미 확정·전송·대조까지 끝나 있어야 하므로**, `LoadMap` 은 seed를 받아 새로 만드는 것이 아니라 **이미 확정된 `MapDefinition` 을 받아 투영만 하는 자리**로 성격이 바뀐다.

이것이 규칙 16의 다음 두 문장과 정확히 같은 말이다.

- *"Client는 전달받은 최종 맵을 **그대로 로드**하며 별도로 추첨·재생성·폴백하지 않는다."*
- *"전투 씬에서는 **로비에서 확정한 맵 데이터만 사용해** 맵을 구성한다."*

**따라서 seed 전송의 목적은 「Client가 seed로 맵을 다시 만들기 위해서」가 아니다.** seed는 canonical 바이트 안에 이미 들어 있고(`Encode` 2번째 필드), 그 용도는 **로그와 재현**이다 — 규칙 12의 로그 필수 항목이고, `_mapPreparation` 을 보관하는 이유 자체가 "문제가 생긴 맵을 다시 만들어 보기 위해서"라고 코드 주석이 밝힌다(`GameBootstrapper.cs:200`~`:204`). **이 구분이 흐려지면 「Client도 생성기를 돌린다」는 설계로 잘못 미끄러진다.**

### 7-4. 그래서 실제로 바뀌어야 하는 것 (조사 결과)

| 지금 | 규칙 16을 만족하려면 |
|---|---|
| `PrepareAndProjectMap()` 이 **준비(생성)와 투영을 한 함수에서** 연달아 한다 (`GameBootstrapper.Map.cs:433`) | Host: 준비는 **로비에서**, 투영은 전투 씬에서 / Client: **준비 없이 투영만** |
| `CreateRootSeed()` 가 두 모드를 분기로 처리 | 멀티에서는 **애초에 호출되지 않아야** 한다(Host는 로비에서 이미 뽑았고, Client는 뽑지 않는다) |
| `LoadMap` 이 `MapPreparationUseCase` 를 직접 생성 (`:441`~`:448`) | 확정된 정의를 **밖에서 받아** 쓰는 통로가 필요하다 |

⚠️ **이는 「이렇게 하자」가 아니라 「규칙을 만족하려면 이 구조가 움직인다」는 관찰이다.** 실제 분할 방식은 계획의 몫이다.

---

## 8. 🔴 실패 복구와 UI — 미정 항목 목록 (Plan 착수 전 사용자 결정 필요)

### 8-1. 미정 항목은 **4건**이다 (`GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 M-3·M-4 전문 확인)

규칙 16이 *"위 실패 UI 규정에는 아직 정해지지 않은 항목이 남아 있다 — 구현 시 확정한다"* 며 단일 소스로 지목한 자리를 열어 전부 뽑았다.

| # | 어느 규칙 | 무엇이 미정인가 | 규칙 문언 |
|---|---|---|---|
| **1** | 「공통 UI 규칙」 **규칙 M-3** (멀티 `NewMap` 재경기 실패) | **팝업이 뜨는가, 결과 화면만 복원되는가** | *"위 세 줄은 결과 화면을 되돌리는 것만 규정할 뿐, 실패를 알리는 별도 팝업이 뜨는지 결과 화면만 복원되는지는 정해진 바 없다."* |
| **2** | 「공통 UI 규칙」 **규칙 M-4** (싱글 재경기 새 맵 실패) | **팝업이 뜨는가, 결과 화면만 복원되는가** — 1번과 같은 문제이나 **적용 대상이 다르다** | *"상세와 근거는 위 규칙 M-3 의 같은 미정 표시를 따른다."* |
| **3** | 「공통 UI 규칙」 **규칙 M-4** (싱글 최초 경기 실패) | **표시 문구** — 멀티 최초 실패(규칙 M-2)의 `맵 준비에 실패했습니다` 를 그대로 쓸지 다른 문구를 쓸지 | *"멀티 최초 실패(규칙 M-2)의 `맵 준비에 실패했습니다`를 그대로 쓸지 다른 문구를 쓸지는 정해진 바 없다."* |
| **4** | 「공통 UI 규칙」 **규칙 M-4** (싱글 최초 경기 실패) | **loading UI 처리** — 규칙 M-2는 첫 불릿에서 loading UI를 닫는다고 규정하는데 규칙 M-4에는 그 서술이 없다 | *"`TechnicalDesignDocument.md` 「복구 상태」 절의 싱글 항목도 같은 자리가 비어 있다. **채워 넣지 않고 미정으로 둔다 — 새 사양 추가이기 때문이다.**"* |

**추가로 따라오는 조건부 결정(1·2가 「팝업」으로 정해질 때만 발생):**

> 규칙 M-3: *"**팝업으로 확정되면 공통 UI 규칙 8(팝업 타입 구분)에 따라 팝업/모달 타입을 함께 정해야 한다.**"*

즉 1·2가 「팝업」이면 **팝업(Popup)인지 모달(Modal)인지**를 각각 정해야 하므로 실질 결정 수가 **최대 6건**이 된다. 「결과 화면만 복원」으로 정하면 **4건에서 끝난다.**

### 8-2. 결정에 필요한 재료 — 기존 UI 자산으로 만들 수 있는가 (실측)

| 규칙이 요구하는 것 | 기존 자산 | 판정 |
|---|---|---|
| **멀티 최초 실패**: 모달 · `맵 준비에 실패했습니다` · `Retry` / `Leave Match` | `ConfirmPopup.Show(message, confirmLabel, cancelLabel, onConfirm, onCancel)` (`Presentation/UI/ConfirmPopup.cs:116`) — **버튼 2개 + 라벨 자유 지정 + Modal 오버레이 단일 소유**(`:144`~`:148`). `UIManager.ShowConfirm(...)` 로 노출(`UIManager.cs:143`). 실물은 `Login.unity` 의 `DontDestroyOnLoad` UIManager 캔버스 아래에 있고(`Login.unity:8005`) 프리팹도 있다(`Assets/_Project/Prefabs/UI/ConfirmPopup.prefab`) | ✅ **기존 자산으로 만들 수 있다.** 로비 씬에서도 `UIManager.Instance` 로 도달한다 |
| **싱글 최초 실패**: 모달 · `Retry` / `Lobby` | 동일 | ✅ 가능 |
| **재경기 실패**: `SameMap` / `NewMap` / `Lobby` **3개 복원** | `ConfirmPopup` 은 **버튼 2개뿐** · `GameEndUI` 도 버튼 2개뿐(§3-2) | ⚠️ **3선택지를 담을 자산이 없다.** 「결과 화면 복원」으로 정하더라도 결과 화면 자체에 버튼이 하나 더 필요하다 |
| **내부 정보 비공개**(규칙 M-1): seed·`MapType`·`MapVersion`·error code 를 UI에 노출 금지 | `ConfirmPopup` 은 메시지 문자열만 받는다 | ✅ 구조적으로 안전 |

### 8-3. timeout·재전송 규정은 결정이 아니라 구현 사항 (문서 인용 — 미정 아님)

규칙 16이 이미 확정한 값들이라 사용자에게 물을 것이 없다.

- Host 대기 **10초** · **1회만 재전송**(timeout 또는 조각 불완전 수신일 때만) · 두 번째 10초에도 실패하면 종료.
- **즉시 실패(재전송 없음)**: 미지원 `MapVersion` · 용량 한도 초과 · 해시 불일치 · 역직렬화 실패 · 공정성 검증 실패 · 연결 끊김.
- 어떤 실패에서도 전투 씬으로 전환하지 않는다. seed·유형·error code는 **로그에만** 남긴다.
- `Retry` 는 **같은 데이터 재전송이 아니라 새 root seed부터 다시 시작**하며, 중복 요청은 idempotent 하게 한 번만 처리한다.

### 8-4. 로그 키가 부족하다 (실측 — 결정이 아니라 관찰)

현재 `LogEvent` 의 맵 관련 키는 **4개뿐**이며 전부 `PrepareAndProjectMap` 발생 지점이다 — `MapPreparationSucceeded`(`Application/Interfaces/ILogSink.cs:410`) · `MapPreparationUsedFallbackTemplate`(`:429`) · `MapPreparationFailed`(`:447`) · `MapProjectionFailed`(`:478`). **전송 실패·해시 불일치·timeout·재전송에 해당하는 키는 0건이다.** 규칙 16이 *"불일치 내용을 기록한다"* 고 요구하므로 3단계에서 키 신설 판단이 필요하다(신설 기준의 단일 소스는 `LogRules.md` 1.5).

---

## 9. `MapVersion` 취급 — 값이 어디서 오는가

| 항목 | 실측 |
|---|---|
| 상수 정의 | `public const int CurrentMapVersion = 1;` — `Domain/Map/MapDefinition.cs:96` |
| 인스턴스 기본값 | `public int MapVersion { get; set; } = CurrentMapVersion;` — `:112` |
| canonical 바이트의 **첫 필드** | `WriteInt32(buffer, def.MapVersion);` — `MapDefinitionCodec.cs:60` |
| 미지원 형식 차단 | `Decode` 가 **가장 먼저** 검사하고 다르면 즉시 `null` 반환 — `MapDefinitionCodec.cs:132` 이하 (*"지원하지 않는 형식 버전은 해석을 시도하지 않는다"*) |
| 실제 파일의 값 | 폴백 템플릿 5개 전부 **`1`** (§5-1 디코드 결과) |

**규칙 16의 규정과 코드가 일치한다** — *"`MapVersion`은 canonical map binary 형식 식별과 미지원 형식 차단에만 사용한다. … matchmaking·앱 업데이트·전역 접속 호환성 판정에는 사용하지 않는다."* 코드에도 그 외 용도는 없다(전수 검색 결과 `MapVersion` 은 `MapDefinition` · `MapDefinitionCodec` · `MapPreparationResult` · 생성기 요청 안에서만 쓰인다).

⚠️ **3단계에서 새로 생기는 자리**: `TechnicalDesignDocument.md` 는 package 헤더에도 `mapVersion` 을 두고 *"canonical `MapDefinition.MapVersion`과 일치해야 한다"* 고 규정한다. **헤더 값과 본문 값이 두 군데가 되므로 「불일치 시 실패」 판정이 새로 필요하다.** 지금은 값이 한 곳뿐이라 그 판정이 존재하지 않는다.

---

## 10. 2단계 보류 8건 중 3단계 범위에 자연히 들어오는 것

`_Tasks/2026-09-03/03_14_random-map-phase2-generator/Plan.md` §13 표 8행을 전수 대조했다.
⚠️ **가려내기만 한다 — 「하겠다」고 정하지 않는다. 범위 확정은 Plan 과 사용자 승인의 몫이다.**

| # | 보류 항목 | 3단계 범위인가 | 판정 근거 |
|---|---|:---:|---|
| 1 | **멀티 seed 고정(임시값 1)** | 🔴 **명백히 그렇다** | ROADMAP 3단계 행이 ⓐ로 명시. 코드 주석도 *"3단계에서 … 이 분기는 사라진다"*(`GameBootstrapper.Map.cs:628`~`:634`) |
| 2 | `MapPreparationResult.InitialGold` **계산만 하고 미사용** | 🟡 **그렇다고 볼 근거가 있다** | 실측: `InitialGold` 를 읽는 곳은 **로그 문자열 한 군데뿐**(`GameBootstrapper.Map.cs:573`). 실제 시작 골드는 여전히 `new ResourceUseCase(_config.StartingGold)` (`GameBootstrapper.Setup.cs:444`). **규칙 16이 *"Host가 확정한 … 실제 초기 골드를 canonical `MapDefinition`에 넣어 전송하고, 양쪽은 그 값을 그대로 적용한다"* 고 정하므로, 3단계가 이 값을 실제로 적용하지 않으면 규칙 16이 미충족 상태로 남는다.** 즉 3단계에서 자연히 걸린다 |
| 3 | **맵 준비·투영 실패 처리 미명세** | 🟡 **절반만 그렇다 — 인계 서술이 부정확하다** | ⚠️ **보류표의 *"어느 문서에도 없다"* 는 현재 사실이 아니다.** **준비 실패**는 규칙 16 실패 복구 절 + 「공통 UI 규칙」 규칙 M-2·M-3·M-4 가 이미 규정한다(미정 4건 포함 — §8). **투영 실패**만 규정이 없다: `Docs/` 전수 검색 결과 `MapProjectionFailed` 는 **로그 키 정의(`LogRules.md`)와 ROADMAP 한 줄에만** 등장하고, 실패했을 때 무엇을 보여줄지는 어디에도 없다. → **「준비 실패 UI」는 §8의 미정 4건과 겹치고, 「투영 실패」는 별개로 열려 있다** |
| 4 | 규칙 13 「광산 배치 후」 **임계값 3·2 기획 확인** | ⚪ **아니다** | 생성기·검증기 쪽 기획값. 전송·재경기와 접점 없음 |
| 5 | `MapPreparationUseCase` **생성기 주입 이음매 없음** | ⚪ **아니다(다만 §6-3과 인접)** | 3단계가 이 이음매를 요구하지는 않는다. 단 Client 검증에서 생성기를 만들어야 한다면 현재 `MapFallbackTemplateFactory.CreateGenerator(MapType)` 경로가 그대로 쓰인다(`MapPreparationUseCase.cs:328`·`:441`·`:578`) — **필요는 하지만 새 이음매 없이도 닿는다** |
| 6 | `AGENTS.md` 에 `_Reference` 누락 | ⚪ **아니다** | 문서 인덱스 정비. 언제든 독립 처리 가능 |
| 7 | `PlaceMiningPostDirect` **막힌 타일 가드 없음** | ⚪ **아니다** | 2단계 §12-(1) 실측상 **현재 도달 불가**. 전송과 무관 |
| 8 | `NoBuildHatch.mat` **`sharedMaterial` 공유** | ⚪ **아니다** | 렌더러 쪽. 단 2단계 보류표가 *"1·3·8 은 그대로 두면 실제 문제가 되는 성격"* 이라 적었으므로 **3단계 착수 시 별도로 꺼낼지 사용자에게 확인할 가치는 있다** |

**요약: 3단계 범위에 자연히 들어오는 후보는 1 · 2 · 3(투영 실패를 제외한 준비 실패 부분은 §8과 통합) — 3건이다.**

---

## 11. 🔴 아키텍처 충돌 지점 — Host 권위 전송을 어느 레이어가 소유하는가

### 11-1. 제약과 현재 상태 (실측)

| 제약 (`.claude/MEMORY.md`) | 현재 상태 |
|---|---|
| `NetworkBehaviour` 는 **Infrastructure 레이어에만** | ✅ 지켜짐 — `NetworkBehaviour` 상속 15개 전부 `Scripts/Infrastructure/Network/` |
| Application → `Unity.Netcode` **직접 참조 금지** | ✅ 지켜짐 — `Scripts/Application` 하위에 **`using Unity.Netcode` 0건**(주석 언급 3건은 *"참조 없음"* 이라고 밝히는 문장) |
| Application → Infrastructure **역참조 금지** | ✅ 지켜짐 — 필요한 것은 `Application/Interfaces/` 에 인터페이스로 선언 |
| `GameBootstrapper` 가 **유일한 의존성 조합 루트** | ✅ 지켜짐 |

### 11-2. 충돌의 모양

**맵을 만드는 주체는 Application에 있다.** `MapPreparationUseCase` 는 `namespace Hexiege.Application` (`Application/UseCases/MapPreparationUseCase.cs:89`) 이고 **일부러** `UnityEngine` 을 참조하지 않는다 — 파일 헤더가 그 이유를 밝힌다(*"Unity 없이 그 파일만 따로 컴파일해서 「100회 실패 → 폴백」 같은 좀처럼 안 걸리는 경로까지 실제로 돌려 볼 수 있다"* — `GameBootstrapper.Map.cs:459`~`:464` 의 대응 주석).

**맵을 보내는 주체는 Infrastructure 여야 한다.** RPC는 `NetworkBehaviour` 에서만 나갈 수 있고 그것은 Infrastructure 전용이다.

**그런데 규칙 16은 「Host가 유일한 권위자」라는 하나의 책임으로 둘을 묶는다** — 생성·검증·재시도·폴백·전송·재전송·timeout·해시 대조가 한 상태 기계다. 이 상태 기계를 통째로 어느 한 레이어에 두면 반드시 제약을 깬다.

| 두는 곳 | 깨지는 것 |
|---|---|
| Application에 상태 기계 전부 | **`Unity.Netcode` 직접 참조 금지** 위반 |
| Infrastructure에 상태 기계 전부 | 제약 위반은 아니나, **Application의 순수 C# 경계가 제공하던 「Unity 없이 폴백 경로를 돌려 본다」는 검증 수단**이 전송 부분에 대해서는 성립하지 않는다 |
| 갈라서 둔다 | **경계면 설계가 필요하다** — 아래 |

### 11-3. 이미 있는 두 가지 선례 (실측 — 결정이 아니라 재료)

1. **정적 홀더**: `NetworkContext` (`Application/NetworkContext.cs:23`) — `IsNetworkServer` · `IsNetworkActive` 두 bool 을 Infrastructure가 `Set()` 하고 Application이 읽는다. **상태 조회에는 맞지만 「조각을 보내고 ACK를 기다린다」 같은 비동기 대화에는 맞지 않는다.**
2. **의존성 역전 인터페이스**: `Application/Interfaces/` 에 선언 → Infrastructure가 구현. 실사례 `IUnitFactory` · `IGameServices` · `IForfeitService` · **`IMapFallbackTemplateSource`**. 특히 마지막은 **바로 이 맵 계통에서 이미 쓰이고 있다** — Application의 조정자는 인터페이스만 알고, `Resources` 를 읽는 구현은 Infrastructure에 있다(`Infrastructure/Config/ResourcesMapFallbackTemplateSource.cs:47`).

> 🔴 **즉 이 프로젝트는 같은 문제를 맵 계통에서 이미 한 번 풀었다.** 「전송」도 같은 모양(Application에 인터페이스 · Infrastructure에 NGO 구현)으로 갈 수 있는지가 계획의 첫 판단이 된다. **다만 `IMapFallbackTemplateSource` 는 동기 조회인 반면 전송은 비동기 왕복이라 같은 형태가 그대로 성립하는지는 확인하지 못했다**(→ §13).

### 11-4. 부수 충돌 — `IGameServices` 확장 필요 (실측)

§6-2에서 확인한 대로 Infrastructure는 인터페이스를 통해 `MapPreparationResult` 에 닿을 수 없다. `MapPreparationResult` 자체는 **Application 타입**이므로 `IGameServices` 에 `MapPreparationResult GetLastMapPreparation()` 를 추가해도 **역참조 위반이 아니다**(Application 인터페이스가 Application 타입을 반환하는 것이므로). 즉 **제약 위반 없이 뚫을 수 있는 통로가 있다** — 다만 그것이 옳은 자리인지는 계획의 몫이다.

---

## 12. 조사 중 발견한 문서 간 어긋남 (고치지 않고 기록만 한다)

### 12-1. 🔴 `TechnicalDesignDocument.md` 의 canonical payload 크기 「약 274바이트」가 실측과 다르다

| 출처 | 값 | 내역 |
|---|---:|---|
| `TechnicalDesignDocument.md` 전송 프로토콜 절 (2026-08-26 기록) | **약 274바이트** | *"타일 231 + 헤더 19 + 성·광산 24 + 장식 0"* |
| **이번 실측** | **335~343바이트** | 헤더 **40** + 타일 231 + 성 20 + 시작 광산 20 + 중립 광산 `4+4N` + 장식 4 |

**어긋난 이유 두 가지(코드 대조로 특정):**
1. **헤더가 19가 아니라 40바이트다** — `Encode` 의 상위 필드는 `int32` 8개(`MapVersion`·`MapType`·`Width`·`Height`·`Orientation`·`NeutralMineCount`·`TestModeFlag`·`InitialGold` = 32바이트)와 `uint64` 1개(`RootSeed` = 8바이트)로 **합계 40**이다(`MapDefinitionCodec.cs:60`~`:68`).
2. **4개 목록의 「개수」 필드 4×4 = 16바이트가 누락됐다** — `Encode` 는 성·시작 광산·중립 광산·장식마다 `WriteInt32(buffer, …Count)` 를 쓴다.

그 문서 자신이 *"파이썬 계산이며 Unity·NGO 실기 측정이 아니다"* 라고 밝히고 있으므로 **거짓 주장이 아니라 계산 착오**다. ⚠️ **다만 그 수치가 「조각이 1개뿐」이라는 결론의 근거로 쓰이고 있는데, 실측값 343으로도 그 결론은 그대로 성립한다**(6,144바이트 한도의 5.6%). **결론은 유지되고 근거 수치만 정정 대상이다.**

> **이 문서는 `TechnicalDesignDocument.md` 를 고치지 않았다** — 이번 작업 범위가 `Research.md` 한 건이기 때문이다. **정정 반영 여부는 사용자 판단.**

### 12-2. 규칙 16의 전달 항목과 package 정의가 어긋난다

- **규칙 16**: *"Host는 … 최종 맵 전체 데이터와 seed, 맵 유형, 중립 광산 수, **시작 광산 방향**, 실제 테스트 모드 표식, 실제 초기 골드를 Client에 전달한다."*
- **`TechnicalDesignDocument.md` package 정의**: `mapVersion` / `canonicalLength` / `canonicalBytes` / `sha256Digest[32]` — 그것뿐이다.
- **canonical 바이트에 `StartingMineSide` 가 없다**(실측 — §6-3).

**즉 규칙이 「전달한다」고 정한 항목 중 하나가 현재 package 정의로는 전달되지 않는다.** (실질적으로는 시작 광산의 타일 좌표가 목록에 있으므로 **맵 자체는 완전히 결정되지만**, `StartingMineSide` 라는 **값**은 전달되지 않는다.) §6-3의 선택지와 같은 문제다.

### 12-3. 규칙 14의 「자동 시작 금지」와 현재 코드가 어긋난다

§3-1 표 마지막 행 참조. **현재 코드가 규칙을 어기는 것이 아니라 `RematchMapMode` 자체가 아직 없어서 생기는 미구현**이다. 다만 **3단계에서 조건을 도입하는 순간 이 분기를 반드시 함께 손봐야 한다** — 안 그러면 조건이 다른데도 자동 시작되는 상태가 된다.

### 12-4. `GameBootstrapper` 주석이 아직 사실이 아니다

`GameBootstrapper.Map.cs:453`~`:454` 주석: *"다른 코드가 `GetLastMapPreparation()` 으로 같은 판의 결과(최종 맵 바이트·해시 등)를 다시 읽는다."* — **읽는 코드가 아직 0건이다**(§6-2). 3단계가 그 첫 독자가 될 예정이므로 **틀린 주석이 아니라 앞선 주석**이다. 기록만 한다.

---

## 13. 확인하지 못하고 남긴 것 (Plan 전에 확인할 것 — 추정으로 채우지 않았다)

| # | 무엇 | 왜 확인 못 했나 | 어떻게 확인해야 하나 |
|---|---|---|---|
| **1** | **NGO 2.9.2에서 동적 스폰 `NetworkObject` 가 `LoadSceneMode.Single` 씬 재로드를 넘어 생존하는 조건** | 패키지 소스가 이 환경에 없다(`Library/PackageCache` 부재). 프로젝트 안에 선례도 0건 | NGO 문서 확인 + 에디터 2인 구성으로 실측. **§4-4 후보 A의 성립 여부가 여기에 걸려 있다** |
| **2** | **NGO RPC 한 번의 실효 페이로드 상한**(설정값 6,144와 얼마나 다른가) · **Relay 경유 시의 실효 MTU** | 코드로 확인할 수 없는 런타임 값 | 실기에서 payload를 키워 가며 실패 지점 탐색(§5-3). **로컬 측정값을 Relay 값으로 삼지 않는다** |
| **3** | **`constraints` 가 정말 `AttemptIndex` 에 의존하는가** — 생성기 5종 중 실제로 지형 스트림을 소비해 constraints를 만드는 것이 몇 개인가 | 기반 클래스(`MapArchetypeGeneratorBase.cs:136`)까지만 확인했고 **생성기 5개의 `TryApplyTerrain` 구현 본문은 열지 않았다** | `OpenGenerator` · `ObstacleOpenGenerator` · `CanyonGenerator` · `OuterGenerator` · `ThreeLaneGenerator` 5개 본문 확인. **`MapType` 만으로 결정되는 생성기가 있다면 §6-3의 문제 범위가 줄어든다** |
| **4** | **`IMapFallbackTemplateSource` 형태의 인터페이스 역전이 「비동기 왕복」에도 성립하는가** | 기존 선례는 전부 **동기 조회**다 | 계획 단계에서 경계면 설계 시 판단 |
| **5** | **규칙 14의 「양측이 서로 다른 mode로 동시 요청」 규정이 「같은 mode 동시 요청」에도 적용되는가** | 규칙 문언이 *"서로 다른 mode로 동시에 요청해도"* 라 **같은 mode 경우를 명시하지 않는다.** 문맥만으로 확정할 수 없다 | **사용자 확인 필요**(CLAUDE.md 규칙 12) |
| **6** | **투영 실패(`MapProjectionFailed`) 시의 UI·진행 규정** | `Docs/` 전수 검색 결과 어느 규칙 문서에도 없다(§10-3) | **사용자 결정 필요** — 새 사양이다 |
| **7** | **경로 완전 차단 대응(`GameSystemRules_Units.md` 규칙 45)** | ROADMAP 3단계 행 ③에 묶여 있으나 **맵 전송과 별개 작업**이라 이번 조사 범위에서 제외했다 | 3단계 범위에 넣을지 사용자 확인 |
| **8** | **`NetworkGameManager` 959행 전문** | 씬 전환·생명주기 관련 절만 읽었다(`:37` · `:104` · `:770`~`:784` · `:904`~`:916`) | 후보 C(§4-4)를 검토할 경우 전문 통독 필요 |
| **9** | **2단계 결과의 멀티플레이 실기 동작**(ⓑ) | 실기 세션이 필요하다 | 3단계 구현 전에 **현 임시 고정 seed 상태로 한 번 돌려 보는 것만으로도** 맵 생성·투영·렌더러·건설 판정의 멀티 경로가 확인된다 — 전송을 만들기 전에 할 수 있는 검증이다 |

---

## 14. 요약 — 이번 조사에서 확정된 사실

1. **3단계 코드는 0에서 시작한다** — `NetworkMapTransfer` · `RematchMapMode` · `SameMap` · `NewMap` · 전송 3메시지 전부 **코드 0건**(실측).
2. **로비 씬에 `NetworkObject` 가 0개다**(실측). 규칙 16의 「씬 비종속 전송 경로」는 **새 객체 신설**이 사실상 유일한 길이며, 그 생존 조건(NGO 동작)은 아직 확인되지 않았다.
3. **맵 canonical 바이트는 335~343바이트**(실측 · 공식 `319 + 4N + 20D`). 전송 설정 한도 6,144바이트의 **5.6%**. **조각 수는 1이 된다.**
4. **해시 대조의 재료(`CanonicalBytes` · 32바이트 `Hash` · `HashEquals`)는 이미 전부 있다.** 다만 **꺼내는 통로가 없다** — `GetLastMapPreparation()` 호출자 0건, `IGameServices` 미노출(실측).
5. **Client는 canonical 바이트만으로 `constraints` 를 복원할 수 없다** — `AttemptIndex` · `StartingMineSide` 가 canonical에 없다(실측). `TechnicalDesignDocument.md` 「Client 검증 순서」 4번이 이 지점에서 막힌다.
6. **seed 전송은 「Client가 맵을 다시 만들기 위한 것」이 아니다.** 규칙 16이 Client의 재생성을 금지하므로 seed는 **로그·재현용**이다. 실제로 바뀌는 것은 **`PrepareAndProjectMap()` 의 준비/투영 분리**다.
7. **사용자에게 물어야 할 미정 항목은 4건**(팝업 여부 2 · 싱글 최초 실패 문구 1 · 싱글 최초 실패 loading UI 1). 팝업으로 정해지면 타입 결정 2건이 추가된다.
8. **재경기 UI는 규칙 14를 아직 담지 못한다** — `GameEndUI` 버튼 2개(3개 필요) · `RematchRequestPopup` 텍스트 필드 없음(조건 표시 필요) · **싱글 재경기는 이미 `NewMap` 고정이고 `SameMap` 경로가 없다**(실측).
9. **아키텍처 충돌은 실재하지만 선례가 있다** — 같은 맵 계통의 `IMapFallbackTemplateSource` 가 「Application 인터페이스 · Infrastructure 구현」으로 이미 풀었다. 비동기 왕복에도 같은 형태가 성립하는지는 미확인.
10. **2단계 보류 8건 중 3단계에 자연히 들어오는 것은 3건**(멀티 seed 고정 · `InitialGold` 미사용 · 준비 실패 처리). ⚠️ **가려냈을 뿐 범위로 확정하지 않았다.**
11. **문서 간 어긋남 4건을 발견해 기록만 했다**(canonical 크기 274 vs 실측 343 · 규칙 16 전달 항목 vs package 정의 · 규칙 14 자동 시작 금지 vs 현재 코드 · 앞선 주석 1건). **어느 것도 고치지 않았다.**

---

> **이 문서는 조사 기록이며 구현 방법을 정하지 않았다.** 각 절의 선택지는 대가와 함께 나열했을 뿐이고, 무엇을 고를지는 `Plan.md` 와 사용자 승인의 몫이다(CLAUDE.md 규칙 1·6·11·12).
