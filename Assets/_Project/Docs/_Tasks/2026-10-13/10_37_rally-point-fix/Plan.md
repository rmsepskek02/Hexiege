# 랠리포인트 버그 2건 + 지정 불가 위치 — Plan

> **현행 상태 (2026-10-19):** 사용자 수용과 승인된 코드 정리·별도 검증 후 PR 없이 main에 통합했고 사용자 push 승인으로 `9bb2c20e`까지 origin/main 반영을 완료했다(§12-7). 전체 경로 차단 대응·정밀 로그 계약은 별도다.

## ⚠️ 최상단 — 기존 로직 비활성화 목록 (WORKFLOW.md [4] 「기존 로직 제거 규칙」)

이번 계획은 기존 코드 몇 곳을 **새 로직으로 대체**한다. 대체되는 옛 코드는 지우지 않고 **주석 처리로 비활성화**한다. 최종 삭제는 사용자 실기 테스트([6]) 통과 후, 문서/메모리 업데이트([7]) 전에 한다.

**현행 정리 단계 (2026-10-18):** 아래는 최초 비활성화 계획과 안전 근거다. 사용자 싱글·멀티 및 AI 5번 수용 후 승인된 D1~D3·옛 랠리 경로 선택 부분의 최종 정리·별도 검증이 완료됐다. 실제 제거 범위와 검증 경계는 §12-6에서 구분한다.

| # | 비활성화 대상 (2026-10-13 HEAD `d48aa27` 기준 행) | 대체하는 것 | 비활성화해도 안전한 근거 |
|---|---|---|---|
| D1 | `Presentation/Production/ProductionTicker.cs` 86행 필드 `_autoHideCoroutine`(배럭 전체가 공유하는 단 하나의 자동 숨김 코루틴 참조)와 그 사용처 — `ShowMarkerTemporary` 492~496행, `AutoHideMarker` 509행, `ShowRallyMarker` 518~522행, `HideAllRallyMarkers` 534~538행 | 배럭 Id별 자동 숨김 코루틴 사전(P6) | 이 필드를 읽고 쓰는 곳은 위 네 메서드뿐이다(같은 파일 안 전수 확인. 필드가 `private`이라 다른 파일에서 접근할 수 없다). 네 메서드 모두 P6에서 새 사전을 쓰도록 바뀌므로, 옛 필드가 남아 있으면 오히려 두 타이머 체계가 섞인다. 버그 2의 원인이 바로 이 공유 필드다(Research §4-3). |
| D2 | `ProductionTicker.OnUnitProduced`의 랠리 분기 309~322행(`RequestMove` → `null`일 때만 `FindPathToNearestEmptyTile` → 경로가 있을 때만 이동) | 새 메서드 `ResolveRallyPath`(이름은 가칭) 호출(P5) | 이 분기가 버그 1의 원인이다 — 건물·금광 칸 랠리에서 `RequestMove`가 「건물 앞 칸 → 건물 칸」 경로를 돌려줘 BFS 대체가 실행되지 않는다(Research §3-2). 또 경로가 없으면 유닛이 스폰 칸에 그대로 멈춘다(Research §3-4). 대체 로직은 같은 입력(유닛·랠리 좌표)으로 같은 출력 형태(이동 경로 + 완료 콜백)를 만든다. |
| D3 | `ProductionTicker.OnRallyPointChanged`의 팀 필터 341~345행(멀티일 때만 동작) | 싱글·멀티 공통 팀 필터(P8) | 멀티 동작은 같은 식(`IsNetworkServer ? Blue : Red`)을 그대로 유지한다. 바뀌는 것은 싱글에서 AI(Red) 이벤트를 걸러내는 것뿐이다. |

**바꾸지 않는 것 (명시):**
- `ProductionTicker.FindPathToNearestEmptyTile`(753~787행)은 **변경하지 않는다.** 공성 이동인 `MoveTowardEnemyCastle`(616행)과 `TickSiege`(706행)가 성 방향 폴백으로 이 메서드를 쓴다. 랠리용 탐색은 별도 메서드로 새로 만든다. D2 이후 `OnUnitProduced`는 이 메서드를 부르지 않지만, 메서드 자체는 공성 경로에서 계속 쓰인다.
- `UnitMovementUseCase.RequestMove`(86~92행)의 「목적지가 이동 불가 칸이면 경로 끝에 목적지를 덧붙이는」 계약은 **변경하지 않는다.** 전투 접근 경로 `RequestCombatApproachPath`(102~105행)가 `RequestMove`를 그대로 호출하며 이 계약에 의존한다(그 메서드 주석: 「타겟 타일이 건물처럼 이동 불가여도 … 마지막 접촉 타일까지 경로를 만든다」).

---

## 0. 이 계획이 무엇을 하는가 (자연어 설명)

배럭에 꽂는 집결 깃발(랠리포인트)에 관한 문제 세 가지와, 새로 정한 사양 하나를 함께 고친다.

1. **깃발을 건물이나 금광 위에 꽂으면 유닛이 멈추는 문제.** 지금은 유닛이 건물 칸 자체를 목적지로 삼아 걸어가다 이동이 고장 난다. 앞으로는 깃발 칸에 갈 수 없으면 **깃발에서 가까운 갈 수 있는 칸**(최대 3칸 거리)으로 가고, 그런 칸이 없으면 **곧장 적 성으로** 간다.
2. **다른 배럭을 열었을 때 앞 배럭의 깃발이 남는 문제.** 지금은 깃발 「3초 뒤 숨기기」 타이머가 모든 배럭에 하나뿐이라 서로 끊어 먹는다. 앞으로는 **배럭마다 타이머를 따로** 두고, 배럭을 열면 **그 배럭의 깃발만** 보이게 한다. 또 깃발을 꽂은 뒤 맵의 다른 곳을 누르면 깃발이 바로 사라진다.
3. **싱글플레이에서 AI의 깃발이 내 화면에 끼어드는 문제.** 싱글에서도 **내 팀 깃발만** 처리한다.
4. **새 사양 — 깃발을 꽂을 수 없는 곳.** 맵 밖이나 막힌 타일(화면에 타일이 없는 빈 곳)을 누르면 「랠리포인트를 지정할 수 없는 위치입니다」 토스트를 띄우고, 창을 다시 열 필요 없이 바로 다른 칸을 누를 수 있게 한다. 멀티플레이에서는 서버도 같은 기준으로 한 번 더 검사한다.

이 문서는 최초 구현 계획과 후속 작업 상태를 구분한다. 최초 코드는 커밋 `9aa921f9`에 반영됐으며(2026-10-18 확인), 이동 도중 건설 문제의 후속 계획은 §12의 범위에서 원인 조사 후 구체화한다.

### 표기 약속
- 행 번호는 **2026-10-13 HEAD `d48aa27`(작업 트리에서 이 Task 폴더 외 변경 없음)에서 직접 열어 확인한 값**이다. 구현 중 파일이 바뀌면 낡으므로, 위치는 **파일 경로 + 메서드명**을 우선으로 읽는다.
- 이 Plan의 코드 동작 서술은 별도 표기가 없으면 **정적 분석(코드 읽기) — 실행 미확인**이다.
- 사양 번호 S1~S12는 같은 폴더 `Research.md` §1·§1-1을 가리킨다.

---

## 1. 근거로 읽은 규칙 문서 (WORKFLOW.md [4])

`Assets/_Project/Docs/GameSystemRules.md`(인덱스)를 읽고, 다음 하위 파일의 해당 절을 직접 읽었다. 섹션마다 번호가 1부터 다시 시작하는 `GameSystemRules_Buildings.md`·`GameSystemRules_UI.md`는 섹션명을 함께 적는다.

| 문서 · 절 | 이 계획에서 쓰는 내용 |
|---|---|
| `GameSystemRules_Buildings.md` 「랠리포인트 시스템」 규칙 1 | 각 플레이어는 자신의 깃발만 본다. 상대 팀 깃발은 표시하지 않는다 → P8 |
| 같은 절 규칙 2 (표) | 설정 직후 3초 표시 후 자동 숨김 / 배럭 팝업 열림 → 해당 배럭 마커 표시(3초 타이머 중지) / 팝업 닫힘 → 모든 마커 숨김 / 배럭 파괴 → 마커 제거 / 자기 칸 설정 → 해제 → P6 |
| `GameSystemRules_UI.md` 「생산 패널 UI」 규칙 1·2 | 팝업 열림 → 해당 배럭 랠리마커 표시 / 팝업 닫힘 → 모든 랠리마커 숨김 → P6·P7 |
| 같은 문서 「생산 패널 UI」 규칙 25·28 | 토스트 표시 방식, 토스트 종류 표 → P4 (규칙 28 표는 구현 후 갱신 대상) |
| 같은 문서 「공통 UI 규칙」 규칙 8~11 | 생산 패널은 Popup 타입(배경 탭으로 닫힘) / 대상 건물 파괴 시 `OnBeforeClose`를 거쳐 자동 닫힘(랠리 마커 숨김 포함) → P2의 「조준 유지」가 이 규칙과 충돌하지 않는지 확인(§6 R5) |
| 같은 문서 「공통 UI 규칙」 「UI 숨김/표시」 규칙 5의 BlockingOverlay 단락 | 조준 진입 시 팝업만 숨기는 경로의 오버레이 처리 → P2는 이 경로를 건드리지 않는다 |
| 같은 문서 「건물 배치 패널 UI」 규칙 5~8 (특히 규칙 6 「막힌 타일 또는 빈 공간 클릭」) | 「막힌 타일 또는 격자 밖 빈 공간 = 선택 불가」 기준 → P1의 판정 기준 |
| `GameSystemRules_Units.md` 「유닛 이동 시스템」 작업 배경 「재설계 (2026-05-11)」 목록 | 「정적 이동 불가 지형과 건물은 경로를 차단한다」, 「최종 목적지도 목적 타일 중심에 정확히 수렴한다」 → P5 |
| 같은 문서 「유닛 이동 시스템」 규칙 1·3 | 유닛은 스폰 순간부터 상대 성으로 이동 / 이동 결정은 서버에서만 → P5 |
| 같은 문서 규칙 45 | 건물로 경로가 완전히 막혔을 때의 동작 — 「확정된 후속 구현 범위 / 미구현」. **이번 범위 밖**(§7) |
| `GameSystemRules_Network.md` 「🔴 최상위 원칙 — 플레이어는 자신이 Host 인지 Client 인지 알 수 없다」 | Host·Client 화면과 결과가 역할과 무관하게 같아야 한다 → P1·P3·P8 |
| `GameSystemRules_AI.md` 규칙 27 「MiningPost 병행 트랙」 | 트랙 흐름 3번 「모든 생산 건물에 `SetRallyPoint` → 대상 금광 인접 타일 지정」 → P3·P5의 AI 영향(§6 R6) |
| `GameSystemRules_RandomMap.md` 「공통 중립 광산 샘플링」 1번 | 중립 광산 후보에서 막힌 타일(`TileKind.Blocked`)을 제외 → P1 판정이 AI 금광 랠리를 막지 않는 근거(§2-4) |

---

## 2. 코드 확인 결과 — 메인 세션 설계와 같은 점 · 달라진 점

메인 세션이 넘겨준 설계(P0~P8)의 파일·메서드·의존성 접근 경로를 코드를 열어 확인했다.

### 2-1. grid(격자) 접근 경로 — **새 주입 불필요**

| 필요한 곳 | 확인 결과 |
|---|---|
| `ProductionTicker` | **이미 `HexGrid _grid` 필드가 있다**(72행). `Initialize`의 `grid` 인자로 받으며(130·142행), `GameBootstrapper.Setup.cs` 714~717행이 `_grid`를 넘긴다. 다만 그 주입 주석(708~712행)과 `Initialize` 주석(141행)은 「`_grid`가 null이어도 폴백 경로로 동작」을 전제로 한다. 그래서 P5의 이동 가능 판정은 `_grid`를 직접 읽지 않고 **`_unitMovement.IsWalkable(coord)`**(`UnitMovementUseCase.cs` 288~292행, 격자 밖이면 false)를 쓴다. → **메인 설계의 「없으면 GameBootstrapper 주입으로 추가」는 필요 없다.** |
| `UnitProductionUseCase` | **이미 `HexGrid _grid` 필드가 있다**(36행, 생성자 45~52행). `GameBootstrapper.Setup.cs` 458~459행이 `_grid`를 넘긴다. → P1 판정을 여기에 둘 수 있다. |
| `InputHandler` | **격자 참조가 없다.** 주입받는 것은 `GridInteractionUseCase`·`BuildingPlacementUseCase`·각 패널·`SkillAimController`·카메라뿐이다(`Initialize` 97~124행). `GridInteractionUseCase`는 `_grid`를 `private readonly`로 갖고 있다(82행). → InputHandler는 **이미 갖고 있는 `_productionUI`(ProductionPanelUI)를 통해** 판정을 묻는다(2-3). |
| `NetworkProductionController.SetRallyPointServerRpc` | 같은 RPC가 이미 `UnitProductionUseCase production = _services?.GetUnitProduction();`(1019행)으로 UseCase를 얻는다. `GameBootstrapper.GetUnitProduction()`(401행)은 위 `_unitProduction`을 돌려준다. → 같은 UseCase의 판정 메서드를 부르면 된다. |

### 2-2. 싱글플레이 「로컬 팀」 판정의 기존 근거

| 근거 (코드) | 내용 |
|---|---|
| `Presentation/UI/GameEndUI.cs` 1092~1100행 | 주석 「로컬 팀 비교는 LocalPlayerTeam.Current(멀티)로 처리하되, **싱글은 LocalPlayerTeam이 설정되지 않으므로 Blue 고정 폴백**을 사용한다」. 코드 `TeamId localTeam = NetworkContext.IsNetworkActive ? LocalPlayerTeam.Current : TeamId.Blue;` |
| `Presentation/UI/GameHudUI.cs` 196~197행 | 주석 「싱글플레이는 Blue 고정」, 코드 `_isNetworkMode ? LocalPlayerTeam.Current : TeamId.Blue` |
| `Application/Services/AIOpponentController.cs` 63행 | `private const TeamId AiTeam = TeamId.Red;` — 싱글 AI는 항상 Red |

→ P8의 싱글 로컬 팀은 **`TeamId.Blue` 고정**으로 한다. 기존 두 UI가 쓰는 판정과 같은 식이다.

⚠️ **함께 발견한 사실(이번 범위 밖, 수정하지 않음):** `InputHandler`는 싱글에서도 `LocalPlayerTeam.Current`를 쓴다(285·350·352·368·402·404행). `LocalPlayerTeam.Current`의 기본값은 Blue이지만(`Infrastructure/Network/LocalPlayerTeam.cs`), **`LocalPlayerTeam.Reset()`을 부르는 곳이 프로젝트 코드에 없다**(전체 스크립트 검색, 호출처는 `NetworkGameFlow.cs` 190행의 `Set` 하나). 따라서 같은 실행 중에 멀티 Client(Red)로 한 판을 한 뒤 싱글을 시작하면 `Current`가 Red로 남아 있을 가능성이 코드상 있다 — **실행 미확인**. 그 경우 P8(Blue 고정)과 InputHandler(Red)가 서로 다른 팀을 「내 팀」으로 볼 수 있다. P8은 기존 UI 두 곳과 같은 Blue 고정을 따르고, 이 불일치는 §10 미확인 항목으로 넘긴다.

### 2-3. InputHandler에서 판정·마커 숨김에 닿는 경로

- InputHandler에는 `ProductionTicker` 참조가 없다. 가진 것은 `_productionUI`(54행)다.
- `ProductionPanelUI`는 `_production`(UnitProductionUseCase, 242행)과 `_ticker`(ProductionTicker, 244행)를 `private`으로 가진다(`Initialize` 291~303행, `GameBootstrapper.Setup.cs` 730~737행에서 주입).
- 지금 마커 API(`ShowRallyMarker`·`HideAllRallyMarkers`)를 외부에서 부르는 곳은 **`ProductionPanelUI` 두 곳뿐**이다(333·351행). `CompleteRallyPointSetting`을 부르는 곳은 **`InputHandler` 227행 하나뿐**이다(전체 스크립트 검색).
- → **결정: `ProductionPanelUI`에 공개 메서드 두 개를 추가해 InputHandler가 그것을 부른다.** `CanSetRallyPointAt(HexCoord)`(P2), `HideAllRallyMarkers()`(P7, 내부에서 `_ticker`로 위임). 이렇게 하면 `InputHandler.Initialize` 서명과 `GameBootstrapper`를 바꾸지 않는다. 마커 API를 부르는 곳도 계속 `ProductionPanelUI` 한 곳으로 모인다.
- 대안(채택하지 않음): `InputHandler.Initialize`에 `ProductionTicker`를 인자로 추가하고 `GameBootstrapper.Setup.cs` 563~572행에서 넘기는 방법. 주입 지점과 파일이 하나씩 늘고, 마커 API 호출처가 두 곳으로 흩어진다.

### 2-4. P1 판정을 어디에 두는가 — `UnitProductionUseCase`

- **결정:** Application의 `UnitProductionUseCase`에 `public bool CanSetRallyPointAt(HexCoord coord)`를 둔다. 식은 「격자에 타일이 있고, 그 타일이 `TileKind.Blocked`가 아니다」. 입력(ProductionPanelUI 경유)과 서버 RPC가 **같은 인스턴스·같은 메서드**를 부른다.
- 이유: ① 이미 `_grid`가 있다(2-1). ② 입력 쪽 `ProductionPanelUI._production`과 서버 쪽 `_services.GetUnitProduction()`이 같은 클래스를 이미 갖고 있어 새 의존성이 생기지 않는다. ③ 순수 C#(Application)이라 레이어 규칙에 맞다.
- Domain(`HexTile`)에 새 판정 프로퍼티를 두는 안은 채택하지 않았다. `HexTile`의 `Accepts~` 프로퍼티들은 `GameSystemRules_RandomMap.md` 5장 판정표(이동/점령/건설)와 TDD 「HexTile 런타임 상태 계약」의 판정식을 옮긴 것이라고 스스로 적고 있다(`HexTile.cs` 79~128행 주석). 랠리 지정은 그 표에 없는 판정이다. 여기에 넣으면 그 계약 문서도 함께 고쳐야 한다.
- ⚠️ 기존 건물 배치 판정(`GridInteractionUseCase.SelectTileByKind` 150~172행: 격자 밖 또는 `tile == null` 또는 `TileKind.Blocked` → `NotSelectable`)은 **이번에 고치지 않는다.** 그래서 「같은 기준」이 **같은 식이 두 곳에 있는** 형태가 된다. 한쪽 기준이 바뀌면 다른 쪽도 맞춰야 한다. 두 메서드 주석에 서로를 가리키는 문장을 넣는다(§6 R7).
- **싱글 경로(`UnitProductionUseCase.SetRallyPoint`) 안에서도 판정할 것인가 → 한다(방어선).** `SetRallyPoint`(465~479행) 첫 부분, 배럭 상태 조회 직후에 `if (!CanSetRallyPointAt(target)) return;`을 둔다. 근거:
  - `SetRallyPoint`를 부르는 곳은 네 곳이다 — `ProductionPanelUI` 571행(로컬), `NetworkProductionController` 1035행(서버), `AIOpponentController` 665행(AI 금광), `ProductionTicker` 404행(업그레이드 복원). 한 곳에서 막으면 네 경로가 모두 같은 규칙을 따른다.
  - **AI 금광 랠리는 통과한다:** 판정은 지형(`TileKind`)만 보고 금광·건물은 보지 않는다(S7·S9). AI가 노리는 금광은 `_grid.Tiles` 중 `MineKind != None`인 칸이다(`AIOpponentController.CacheMineTiles` 217~231행). 중립 광산은 생성 단계에서 막힌 타일을 후보에서 뺀다(`GameSystemRules_RandomMap.md` 「공통 중립 광산 샘플링」 1번). ⚠️ **팀 시작 광산이 막힌 타일 위에 놓이는 맵 정의가 있는지는 확인하지 못했다**(§10). 그런 칸이 있으면 그 칸을 노리는 AI 랠리만 조용히 무시된다(유닛은 곧장 성으로 간다).
  - 업그레이드 복원 값은 이전에 이미 통과한 좌표이고, 지형은 경기 중 바뀌지 않으므로(`HexTile.cs` 8행) 다시 통과한다.
  - 자기 칸 해제(S4, 470~474행)는 배럭이 서 있는 칸이 막힌 타일일 수 없으므로 판정 순서와 무관하다. 그래도 읽기 쉽도록 판정 뒤에 둔다.

### 2-5. 메인 설계와 같은 점 (그대로 확인됨)

- `FindPathToNearestEmptyTile`가 공성 경로에서 쓰인다(616·706행) → 변경 금지 확인.
- `RequestCombatApproachPath`가 `RequestMove`를 그대로 호출한다(102~105행) → 계약 변경 금지 확인.
- 멀티 클라이언트에서는 `UnitView.MoveToInternal`이 바로 반환한다(3152행 `if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;`) → P5는 서버·싱글에서만 실제 이동을 일으킨다.
- `ToastKey`의 마지막 값은 `BuildingNotAllowed`이고, `ToastMessageConfig.asset`의 마지막 항목은 `key: 5`다(31~33행) → 새 키는 6.
- 서버 RPC의 기존 경고 패턴: `GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkProductionController), "…", "Request=SetRally, Reason=Team, …")`(1008~1011행).

---

## 3. 수정 항목

### P1. 랠리 지정 가능 판정 (공용)

- **파일·메서드:** `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` — 새 공개 메서드 `CanSetRallyPointAt(HexCoord coord)`.
- **판정식:** `_grid`가 null이면 false. `_grid.GetTile(coord)`가 null(격자 밖)이면 false. `tile.TileKind == TileKind.Blocked`이면 false. 그 밖에는 true. **건물·금광·건설 불가 타일(NoBuild)은 true**(S7·S9).
- **같은 메서드 사용처:** P2(입력), P3(서버 RPC), `SetRallyPoint` 내부 방어선(2-4).
- **주석:** 「`GridInteractionUseCase.SelectTileByKind`의 `NotSelectable` 판정과 같은 기준이며, 한쪽을 바꾸면 다른 쪽도 맞춘다」를 양쪽에 적는다.
- **근거 규칙:** `GameSystemRules_UI.md` 「건물 배치 패널 UI」 규칙 6(막힌 타일·빈 공간은 선택 불가 — 같은 기준). `GameSystemRules_Network.md` 최상위 원칙 — 판정이 **경기 중 바뀌지 않는 지형(`TileKind`, `HexTile.cs` 8행)과 격자 구성만** 보므로 Host와 Client가 같은 좌표에 대해 같은 답을 낸다(유닛·건물처럼 동기화 시차가 있는 상태를 읽지 않는다).

### P2. 입력 — 지정 불가면 토스트 + 조준 유지

- **파일·메서드:**
  - `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` — 새 공개 메서드 `CanSetRallyPointAt(HexCoord target)`. `_production`이 null이면 false, 아니면 `_production.CanSetRallyPointAt(target)`.
  - `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` `HandleClick`의 랠리 조준 분기(219~230행).
- **바뀌는 흐름:** `rallyCoord`를 계산한 직후(225행 뒤)
  - `_productionUI.CanSetRallyPointAt(rallyCoord)`가 false → `ToastUI.Show(ToastKey.RallyPointNotAllowed)` 후 **return**. `CompleteRallyPointSetting`도 `SelectTileAt`도 부르지 않는다. 따라서 `IsSettingRallyPoint`가 true로 남고, 다음 탭이 다시 이 분기로 들어온다(**조준 유지**, S10).
  - true → 기존대로 `CompleteRallyPointSetting(rallyCoord)` + `SelectTileAt(rallyWorldPos)` + return.
- 토스트는 이 기기에서만 뜬다(로컬 `ToastUI.Show`). 멀티에서 상대 화면에는 뜨지 않는다(S11).
- **근거 규칙:** `GameSystemRules_UI.md` 「건물 배치 패널 UI」 규칙 6(같은 기준). 같은 문서 「생산 패널 UI」 규칙 25(토스트 표시 방식)·규칙 28(토스트 종류 — 구현 후 행 추가 대상). S10.

### P3. 서버 — 같은 판정으로 재검증

- **파일·메서드:** `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` `SetRallyPointServerRpc`(992행~).
- **바뀌는 흐름:** `production` null 검사(1019~1027행) 뒤, `HexCoord target = new HexCoord(q, r);`(1034행) 직후에 `if (!production.CanSetRallyPointAt(target))` → 기존 경고 패턴으로 기록하고 return. 「설정 완료」 Info 로그(1037행)는 실행되지 않는다.
  - 기록 형식(제안): `GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkProductionController), "랠리포인트 설정 거부 — 지정할 수 없는 좌표다", $"Request=SetRally, Reason=Coord, ClientId={senderClientId}, BarracksId={barracksId}, TargetQ={q}, TargetR={r}")`.
  - `LogEvent`는 새로 만들지 않는다. `ServerRejectedUnauthorizedRequest`의 정의가 「팀 불일치·소유권 불일치·**규칙 위반** … 요청을 서버가 거부했다. 정상 클라이언트는 보낼 수 없는 요청이라 변조 탐지 신호로 읽는다」(`Application/Interfaces/ILogSink.cs` 219~224행)이다. P2 이후 정상 클라이언트는 이 좌표를 보내지 않으므로 이 정의에 들어맞는다.
- 클라이언트에는 아무것도 돌려보내지 않는다(기존 RPC도 ClientRpc가 없다, 975~986행 주석). 입력한 쪽은 P2에서 이미 같은 판정으로 걸러졌다.
- **싱글 경로 결정:** `UnitProductionUseCase.SetRallyPoint` 안에도 같은 판정을 둔다(2-4의 결정과 근거). 서버 RPC의 판정은 **로그를 남기기 위해** 따로 둔다. `SetRallyPoint` 안의 판정은 조용히 무시한다.
- **근거 규칙:** `GameSystemRules_Network.md` 최상위 원칙(Host·Client가 같은 기준으로 같은 결과). S11.

### P4. 토스트 키·문구

- **파일:**
  - `Assets/_Project/Scripts/Application/Events/ToastKey.cs` — enum **맨 뒤**(`BuildingNotAllowed` 다음)에 `RallyPointNotAllowed` 추가. XML 주석에 표시 상황(랠리 조준 중 격자 밖·막힌 타일을 탭했을 때)과 근거(S10)를 적는다. 기존 주석의 「enum 순서 = asset key 숫자이므로 항상 맨 뒤에 추가」 경고를 따른다.
  - `Assets/_Project/Resources/Config/ToastMessageConfig.asset` — `_entries` 맨 뒤(33행 뒤)에 아래 세 줄을 YAML로 직접 추가한다.
    ```
      - key: 6
        message: "\uB7A0\uB9AC\uD3EC\uC778\uD2B8\uB97C \uC9C0\uC815\uD560 \uC218 \uC5C6\uB294 \uC704\uCE58\uC785\uB2C8\uB2E4"
        duration: 1
    ```
    - 위 이스케이프는 문구 「랠리포인트를 지정할 수 없는 위치입니다」를 Python으로 변환한 뒤 **다시 디코드해 원문과 같은지 확인한 값**이다(2026-10-13, 비교 결과 True).
    - 🔴 구현자는 이 값을 손으로 다시 조립하지 말고, 넣은 뒤 **파일에서 그 줄을 읽어 디코드해 원문과 비교**한다. 한글 이스케이프는 한 자리만 틀려도 「다른 유효한 음절」이 되어 아무 오류 없이 다른 문구가 들어간다(`.claude/mistakes.md` 2026-09-29 「한글을 유니코드 이스케이프로 써서 …」).
- **[5-2] Inspector 작업 생략 근거:** 에셋 YAML을 직접 고치므로 Inspector에서 손으로 할 일이 없다. 기존 여섯 항목도 같은 형식(`key` 정수 · 이스케이프 문자열 · `duration` 1)이다(16~33행). ⚠️ Unity 에디터가 열려 있으면 에셋을 다시 불러온 뒤(에디터 포커스) 확인한다.
- **근거 규칙:** `GameSystemRules_UI.md` 「생산 패널 UI」 규칙 25·28. S10.

### P5. 버그 1 — 생산 유닛의 랠리 이동 경로 결정

- **파일·메서드:** `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs` — `OnUnitProduced`의 랠리 분기(D2로 비활성화)를 새 private 메서드 호출로 교체. 새 메서드 이름은 가칭 `ResolveRallyPath`.
- **경로 결정 순서** (`unit` = 생산된 유닛, `rally` = `e.RallyPoint.Value`, 이동 가능 판정 = `_unitMovement.IsWalkable(coord)`):
  1. **랠리 칸 자체:** `rally`가 이동 가능하면
     - 유닛이 이미 그 칸에 있으면(`unit.Position == rally`) → **도착한 것으로 보고** 곧바로 `MoveTowardEnemyCastle`.
     - 아니면 `_unitMovement.RequestMove(unit, rally)`. 결과가 있으면 그 경로로 이동하고, 완료 콜백으로 `MoveTowardEnemyCastle`을 건다(기존 320~321행과 같은 형태).
     - 결과가 null(이동 가능한 칸인데 갈 길이 없음)이면 2로 간다.
  2. **근처 칸 후보 (링 1 → 2 → 3):** 거리 `k` = 1, 2, 3 순서로
     - `rally`에서 헥스 거리가 정확히 `k`인 칸 중 이동 가능한 칸만 후보로 삼는다(격자 밖은 `IsWalkable`이 false).
     - 각 후보에 대해: 유닛이 그 칸에 이미 있으면 경로 길이 1(가장 짧음)로 친다. 아니면 `RequestMove(unit, 후보)`. null이면 버린다.
     - 이 링에서 경로가 있는 후보가 하나라도 있으면 **경로 길이(`path.Count`)가 가장 짧은 후보로 확정**하고 탐색을 멈춘다(S8). 같은 길이면 **열거 순서가 먼저인 칸**(결정적 순서 — 방향 인덱스 순 링 순회)으로 한다. 확정한 후보에 유닛이 이미 있으면 바로 `MoveTowardEnemyCastle`, 아니면 그 경로로 이동 + 완료 콜백 `MoveTowardEnemyCastle`.
     - 링 순회 도우미는 `HexCoord`에 없다(`Domain/Hex/HexCoord.cs`의 공개 정적 멤버는 `Distance`와 연산자뿐). `HexDirection.Neighbor`(기존 `FindPathToNearestEmptyTile` 777행이 쓰는 것)로 링을 돈다. 새 도우미를 Domain에 둘지는 구현자 판단에 맡기되, **`FindPathToNearestEmptyTile`은 건드리지 않는다.**
  3. **모두 실패:** 곧바로 `MoveTowardEnemyCastle(unit, unitView)`. 이 메서드는 경로가 없어도 공성 목록에 등록한다(624~628행). 그래서 지금처럼 스폰 칸에 영원히 서 있는 상태(Research §3-4)가 없어진다(S5).
- **경로 끝이 항상 이동 가능한 칸이 된다는 점이 핵심이다.** 1·2에서 고르는 목적지는 모두 `IsWalkable`이 true인 칸이다. 그래서 `HexFlowField.GetPath`의 「이동 불가 목적지 덧붙임」(Domain/Hex/HexFlowField.cs 218~229행)이 일어나지 않는다. 따라서 Research §3-3의 TerminalContact 경로(`UnitView` 2792~2804행 판정)에 들어가지 않는다(정적 분석 — 실행 미확인).
- 랠리 칸이 자기 배럭 칸인 경우는 설정 시점에 이미 해제되므로(S4, `UnitProductionUseCase.SetRallyPoint` 470~474행) 따로 처리하지 않는다.
- `OnUnitProduced` 앞부분(299~305행: 의존성·유닛 오브젝트 확인, 이미 이동 중이면 return)과 랠리가 없을 때의 분기(324~328행)는 그대로 둔다.
- **멀티:** 이동은 서버(호스트)·싱글에서만 실제로 일어난다(`UnitView.MoveToInternal` 3152행). 클라이언트에서도 `OnUnitProduced`가 실행되어 같은 경로 계산을 하지만 이동은 시작되지 않는다. 이 구조는 지금과 같다(Research §3-6).
- **근거 규칙:** `GameSystemRules_Units.md` 「유닛 이동 시스템」 작업 배경 「재설계 (2026-05-11)」 목록의 「정적 이동 불가 지형과 건물은 경로를 차단한다」·「최종 목적지도 목적 타일 중심에 정확히 수렴한다」(건물 칸을 최종 목적지로 삼지 않는다). 같은 문서 규칙 1(유닛은 결국 상대 성으로 이동). 같은 문서 규칙 3(이동 결정은 서버에서만). S5·S6·S7·S8.

### P6. 버그 2 — 배럭별 마커 자동 숨김 타이머

- **파일:** `ProductionTicker.cs` 랠리 마커 관리부(440~545행).
- **새 상태:** `Dictionary<int, Coroutine>`(배럭 Id → 그 배럭의 3초 자동 숨김 코루틴). 옛 `_autoHideCoroutine`은 D1로 비활성화한다.
- **메서드별 동작:**

| 메서드 | 새 동작 |
|---|---|
| `ShowMarkerTemporary(id)` | **그 배럭의 타이머만** 멈추고 다시 시작한다. 그 마커를 켠다. 다른 배럭 마커와 타이머는 건드리지 않는다. |
| `AutoHideMarker(id)` | 3초 뒤 그 마커를 끄고, **사전에서 자기 항목을 지운다.** |
| `ShowRallyMarker(id)` (패널 열림) | **모든 배럭의 타이머를 멈추고**(자기 것 포함 — 「3초 타이머 중지」), **다른 배럭 마커를 모두 끈 뒤** 그 배럭 마커를 켠다(S3). |
| `HideAllRallyMarkers()` (패널 닫힘·P7) | 모든 타이머를 멈추고 사전을 비운 뒤 모든 마커를 끈다. |
| `DestroyMarker(id)` (배럭 파괴·랠리 해제) | 그 배럭의 타이머도 멈추고 사전에서 지운다. 그다음 기존대로 마커를 파괴한다. |

- 「타이머를 멈춘다」 = 사전에 항목이 있으면 `StopCoroutine` 후 항목 제거. 멈춘 코루틴은 끝부분(자기 항목 지우기)을 실행하지 않으므로, 멈추는 쪽에서 반드시 지운다.
- **호스트의 이벤트 2회 발행(Research §4-5)이 P6 이후에도 무해한지:** 두 번째 `OnRallyPointChanged`는 `CreateOrMoveMarker`(같은 좌표로 다시 이동)와 `ShowMarkerTemporary(같은 id)`를 부른다. 새 `ShowMarkerTemporary`는 **같은 배럭의 타이머만** 다시 시작하므로, 결과는 「그 마커가 두 번째 호출 시점부터 3초」다. 다른 배럭에는 영향이 없다 → **무해하다**(정적 분석 — 두 호출이 같은 프레임인지는 Research §4-5대로 미확인이지만, 결론은 순서와 무관하다).
- **근거 규칙:** `GameSystemRules_Buildings.md` 「랠리포인트 시스템」 규칙 2 표(설정 직후 3초 / 팝업 열림 → 해당 배럭 마커 표시, 타이머 중지 / 팝업 닫힘 → 모든 마커 숨김 / 파괴 → 제거). `GameSystemRules_UI.md` 「생산 패널 UI」 규칙 1·2. S2·S3.

### P7. 다른 오브젝트를 누르면 마커 숨김

- **파일·메서드:**
  - `ProductionPanelUI.cs` — 새 공개 메서드 `HideAllRallyMarkers()`. 내부에서 `if (_ticker != null) _ticker.HideAllRallyMarkers();`(`OnBeforeClose` 351행과 같은 형태).
  - `InputHandler.HandleClick` — 「팝업 닫힘 프레임」 가드(252~258행) **뒤**, 월드 좌표 계산(261행 `ScreenToXZPlane`) **앞**에 `if (_productionUI != null) _productionUI.HideAllRallyMarkers();`. (`ProductionPanelUI`는 MonoBehaviour라 이 파일의 기존 관례대로 `!= null`로 검사한다.)
- **이 위치에서 생기는 결과:**
  - 스킬 조준 중(216행)과 랠리 조준 중(219~230행)에는 그 앞에서 return하므로 숨기지 않는다.
  - UI 위 클릭(238행)과 팝업이 닫힌 그 프레임(252~258행)도 숨기지 않는다. 생산 패널이 열려 있을 때 배경을 누르면 오버레이(UI)가 받아 `Close()` → `OnBeforeClose` → 숨김이 되므로 기존 경로가 처리한다.
  - 그 밖의 맵 클릭(건물·금광·일반 타일·건설 불가 타일·막힌 타일·격자 밖)은 **모두 마커를 먼저 숨긴다.** 자기 생산 건물을 누른 경우에는 이어서 `_productionUI.Show` → `OnShow` → `ShowRallyMarker(그 배럭)`(P6)로 **그 배럭 마커가 다시 켜진다.**
  - 유닛을 누른 경우: `HandleClick`에는 유닛 전용 분기가 없고 그 아래 타일 클릭으로 처리된다(Research §4-4, 실행 미확인). 그래서 같은 위치에서 숨김이 적용된다.
- ⚠️ **확인 요청:** 이 위치는 **빈 공간·막힌 타일 클릭(아무것도 선택되지 않는 클릭)에도 마커를 숨긴다.** S2의 「다른 오브젝트를 **선택**하면」을 좁게 읽으면 빈 공간 클릭은 포함되지 않을 수 있다. 메인 설계(월드 클릭 처리 직전 한 지점)를 그대로 따랐고, 빈 공간 클릭을 제외하려면 숨김 호출을 각 분기 안으로 나눠 넣어야 한다. 어느 쪽인지 사용자 확인이 필요하다(§11).
- **근거:** S2. GDD 「랠리 포인트」 절 747행 「다른 오브젝트 클릭 시 마커 숨김」과 TDD 1812행 「팝업 닫힘/다른 오브젝트 클릭: 마커 숨김」(Research §5 C4). 규칙 문서(`GameSystemRules_Buildings.md` 「랠리포인트 시스템」 규칙 2 표)에는 아직 이 행이 없으며 구현 후 C4로 편입한다.

### P8. 싱글플레이에서도 내 팀 랠리 이벤트만 처리

- **파일·메서드:** `ProductionTicker.OnRallyPointChanged`(334~360행). 기존 필터(341~345행)는 D3으로 비활성화한다.
- **새 필터:**
  ```
  TeamId localTeam = NetworkContext.IsNetworkActive
      ? (NetworkContext.IsNetworkServer ? TeamId.Blue : TeamId.Red)
      : TeamId.Blue;   // 싱글: GameEndUI·GameHudUI와 같은 Blue 고정 (§2-2)
  if (e.Team != localTeam) return;
  ```
- **결과:** 싱글에서 AI(Red)의 `SetRallyToMine`·`ClearAllRally` 이벤트가 마커를 만들거나 타이머를 건드리지 않는다(S12, Research §4-6). AI 배럭 마커는 처음부터 만들어지지 않으므로, AI 배럭이 파괴될 때 `OnBuildingDied` → `DestroyMarker`는 아무 일도 하지 않는다(사전에 없음). AI의 **랠리 좌표 자체**(`ProductionState.RallyPoint`)는 이벤트 필터와 무관하게 저장되므로 P5의 AI 유닛 이동에는 영향이 없다.
- **근거 규칙:** `GameSystemRules_Buildings.md` 「랠리포인트 시스템」 규칙 1(상대 팀 깃발은 표시하지 않는다). S12.

---

## 4. 변경 파일 목록 (예정)

| 파일 | 항목 |
|---|---|
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | P1(`CanSetRallyPointAt` 신설), P3(`SetRallyPoint` 방어선) |
| `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | P2(`CanSetRallyPointAt` 위임), P7(`HideAllRallyMarkers` 위임) |
| `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` | P2(랠리 분기 판정·토스트·조준 유지), P7(월드 클릭 전 숨김) |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` | P3(RPC 재검증·경고 로그) |
| `Assets/_Project/Scripts/Application/Events/ToastKey.cs` | P4 |
| `Assets/_Project/Resources/Config/ToastMessageConfig.asset` | P4 |
| `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs` | P5, P6, P8 (D1·D2·D3 비활성화 포함) |
| `Assets/_Project/Scripts/Application/UseCases/GridInteractionUseCase.cs` | 코드 변경 없음. **주석 한 줄만**(P1과 같은 기준임을 서로 가리키는 표기, §6 R7). 원하지 않으면 생략할 수 있다 |

- **변경 없음 (확인):** `GameBootstrapper*.cs`(새 주입 없음 — §2-1·§2-3), `UnitMovementUseCase.cs`, `HexFlowField.cs`, `UnitView.cs`, `AIOpponentController.cs`, `HexTile.cs`.
- 새 파일 없음. 씬·프리팹 변경 없음.

## 5. 아키텍처 제약 점검 (`.claude/MEMORY.md` 「아키텍처 핵심 제약」)

| 제약 | 이 계획에서 |
|---|---|
| GameBootstrapper가 유일한 조합 루트 | 새 주입이 없다. 기존에 주입된 참조만 쓴다. |
| NetworkBehaviour는 Infrastructure에만 | 서버 검증은 기존 `NetworkProductionController`(Infrastructure) 안에서 한다. |
| Application → Unity.Netcode 직접 참조 금지 | P1은 `HexGrid`·`HexTile`(Domain)만 읽는다. 네트워크 판정은 Presentation(`ProductionTicker`)에서 기존처럼 `NetworkContext`로 한다. |
| Application → Infrastructure 역참조 금지 | `UnitProductionUseCase`에 추가하는 것은 Domain만 읽는 bool 메서드다. |
| Domain → Core 참조 금지 | Domain 변경 없음. |
| UIManager null-safe | 새 UIManager 호출 없음. 토스트는 기존 `ToastUI.Show`(인스턴스 없으면 무시, `ToastUI.cs` 149~154행). |
| NGO RPC 메서드명 | 새 RPC 없음. |

---

## 6. 위험 요소

| # | 위험 | 내용과 판단 |
|---|---|---|
| R1 | **링 탐색 비용** | 후보는 최대 6+12+18 = 36칸이다. 후보마다 `RequestMove` → `FlowFieldService.GetOrCompute(목적지)`가 **처음 보는 목적지면 격자 전체 BFS를 1회 계산하고 캐시**한다(`Application/Services/FlowFieldService.cs` 100~114행). 격자는 7×17 또는 11×21(`Resources/Config/GameConfig.asset` 16~22행) — 최대 231칸. 캐시는 건물 배치·파괴 시에만 비워지고(140~145행), 크기는 목적지 칸 수(≤231)로 묶인다. 판단: ① 이 비용은 **랠리 칸에 갈 수 없을 때만**, **유닛 생산 1회당 1번** 든다. ② 첫 성공 링에서 멈추므로 보통 링 1(≤6칸)에서 끝난다. ③ 같은 배럭의 다음 유닛은 같은 목적지라 캐시를 쓴다(그 사이 건물 변화가 없으면). 최악(36칸 모두 처음 계산)은 건물 변화 직후 링 3까지 실패할 때다. 「187칸 BFS 1ms 미만」은 **코드 주석의 주장**(FlowFieldService.cs 21행)이며 이번에 측정하지 않았다. 허용 범위라고 판단하지만, 실기에서 생산 순간 프레임 끊김이 보이면 다시 검토한다. |
| R2 | **멀티 클라이언트 이벤트 흐름** | 클라이언트도 `SpawnUnitClientRpc` 재발행 이벤트로 `OnUnitProduced`를 실행해 같은 경로 계산(R1 비용 포함)을 한다. 이동은 시작되지 않는다(3152행). 랠리 실패 시 3단계(`MoveTowardEnemyCastle`)도 클라이언트에서 불리며, 경로가 없으면 클라이언트 `_siegeUnits`에 등록된다. 이것은 지금 랠리가 없는 유닛이 클라이언트에서 겪는 흐름과 같다(324~328행). 새 위험은 계산 비용뿐이다. |
| R3 | **주석 처리 코드 잔존** | D1~D3은 테스트 통과 후 [7] 전에 삭제해야 한다(WORKFLOW [4]). 주석 블록 첫 줄에 「2026-10-13 rally-point-fix — 테스트 통과 후 삭제」를 적어 찾기 쉽게 한다. 🔴 주석에 옛 필드명을 그대로 남기면 「잔존 0건」 grep 검증이 막힌다(`.claude/mistakes.md` 2026-08-26·08-31·09-02 「폐기한 이름 인용」 계열). 삭제 단계에서 grep 검증은 **주석 블록 표지 문구**로 한다. |
| R4 | **걷는 도중 랠리 칸에 건물이 지어질 때** | P5는 **생산 시점**에만 목적지를 고른다(S6이 요구하는 범위). 랠리 칸이 이동 가능해 그 칸으로 걷고 있던 유닛은, 도중에 그 칸에 건물이 지어지면 `UnitView.OnPathInvalidated`(3351행~)가 **같은 목적지**(`_currentDestination`)로 재경로를 요청한다. 그 목적지는 이제 이동 불가이므로 「목적지 덧붙임」 경로가 다시 생겨 버그 1과 같은 TerminalContact 경로를 탈 수 있다(정적 분석 — 실행 미확인, Research §7-2 마지막에서 두 번째 행). **이번 계획은 이것을 고치지 않는다.** 실기 시나리오 M-6·S-6에서 관찰만 하고, 재현되면 별도 작업으로 다룰지 사용자에게 묻는다. |
| R5 | **조준 유지의 탈출 경로** | 조준 중에 끝내는 방법은 「지정 가능한 칸을 누르기」와 「그 배럭이 파괴되어 패널이 자동으로 닫히기」(`GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 11)뿐이다. 별도 취소 버튼은 지금도 없다. 지정 불가 칸만 계속 누르면 토스트만 반복된다. 같은 토스트는 쌓이지 않는다(「생산 패널 UI」 규칙 25). 기존에도 취소 수단이 없었으므로 새로 생긴 위험은 아니다. |
| R6 | **AI 금광 행동 변화** | 지금은 AI 유닛이 금광 칸 자체로 가려다 Research §3-3 경로를 탈 수 있다. 수정 후에는 금광에서 거리 1인 이동 가능 칸으로 간 뒤 적 성으로 진군한다. 규칙 문서(`GameSystemRules_AI.md` 규칙 27)는 처음부터 「금광 **인접** 타일 지정」이라고 적고 있어 수정 후 동작이 문서에 더 가깝다. 다만 ① 금광 점령(`TryCaptureMine` → `PlaceMiningPost`) 조건이 유닛이 잠깐 들른 인접 칸 점령으로 충족되는지, ② Blue MiningPost가 서 있을 때 행군 유닛이 자동 전투로 그것을 부수는지(규칙 27의 4번 둘째 줄)는 **실행 미확인**이다 → 시나리오 S-5. |
| R7 | **같은 판정식이 두 곳** | P1(`UnitProductionUseCase`)과 `GridInteractionUseCase.SelectTileByKind`가 같은 식을 각자 갖는다(§2-4). 상호 주석으로 연결하지만, 한쪽만 고치는 일이 생기면 「건물 배치는 되는데 랠리는 안 되는 칸」 같은 불일치가 생길 수 있다. |
| R8 | **P7 숨김 범위** | §3 P7의 확인 요청 참조. |

---

## 7. 범위 밖 (이번에 다루지 않음)

- `GameSystemRules_Units.md` 규칙 45(건물로 경로가 완전히 막혔을 때의 유닛 동작) — 규칙 스스로 「확정된 후속 구현 범위 / 미구현」.
- `ProductionPanelUI.CompleteRallyPointSetting`의 `HideBlockingOverlay()` 호출 결함 — 그 주석(574~589행)이 「아직 고치지 않았다(별도 작업)」라고 적고 있다.
- 공성(성 방향) 이동 경로 — `MoveTowardEnemyCastle`·`TickSiege`·`FindPathToNearestEmptyTile`은 변경하지 않는다.
- 호스트의 `OnRallyPointChanged` 2회 발행 — 멱등이라 무해하며 P6 이후에도 무해하다(§3 P6).
- R4(걷는 도중 랠리 칸 건설 시 재경로), §2-2의 `LocalPlayerTeam` 미초기화 가능성 — 관찰·보고만 한다.
- 랠리 조준 중 UI 위를 눌러도 그 아래 맵 좌표로 지정되는 순서(Research §7-2 마지막 행).

## 8. 구현 후 문서 반영 대상 (지금 고치지 않음 — [11] 단계에서)

| ID | 대상 | 내용 |
|---|---|---|
| C1~C4 | GDD 「랠리 포인트」 절, TDD 「랠리포인트 마커 표시 규칙」·「랠리포인트 BFS 빈 타일 탐색」, `GameSystemRules_Buildings.md` 「랠리포인트 시스템」 규칙 2 표 | Research §5 표 그대로. C4 = 「다른 오브젝트 클릭 → 마커 숨김」 행 추가(P7 확인 결과 반영) |
| G1~G5 | `GameSystemRules_Buildings.md` 「랠리포인트 시스템」 | Research §5 표 그대로(S3~S6). G3·G4에 S7(금광 포함)·S8(가장 가까운 칸 기준)을 함께 반영 |
| N1 (신규) | `GameSystemRules_Buildings.md` 「랠리포인트 시스템」 새 규칙 | 지정 불가 위치(격자 밖·막힌 타일), 토스트 + 조준 유지, 서버 재검증(S9~S11). 🔴 `**규칙 N. 제목**` 굵은 글씨 형식으로 쓴다(WORKFLOW.md [11]) |
| N2 (신규) | `GameSystemRules_UI.md` 「생산 패널 UI」 규칙 28 토스트 종류 표 | `RallyPointNotAllowed` 행 추가 |
| N3 | TDD 「랠리포인트 BFS 빈 타일 탐색」 1828행 「범위 내 빈 타일 없으면 이동 안 함」 | S5(곧장 성 진군)로 교체 |
| A1 | `GameSystemRules_AI.md` 규칙 27 「금광 인접 타일 지정」 vs 코드(`SetRallyToMine`은 **금광 칸** 좌표를 지정) | 문구와 코드가 다르다. 이번 수정 후 **동작**은 「인접 칸으로 이동」이 된다. 문서를 「금광 칸을 지정하고, 유닛은 인접 이동 가능 칸으로 간다」로 맞출지 결정 필요 |
| T1 | TDD 「HexTile 런타임 상태 계약」 | P1을 HexTile에 두지 않았으므로 변경 없음(확인만) |

---

## 9. 검증 방법 — 사용자 실기 시나리오

> TC/QA 문서는 만들지 않는다(사용자 요청 없음 — WORKFLOW.md [5-1]). 아래는 사용자가 직접 해 볼 순서다. 「막힌 타일」은 맵에서 **타일이 그려지지 않은 빈 곳**이다.

**싱글플레이**

| # | 해 볼 것 | 기대 결과 |
|---|---|---|
| S-1 | 배럭 A에서 랠리 버튼 → **아군 건물** 위를 탭 → 유닛 생산 | 깃발이 그 건물 위에 꽂힌다. 유닛은 그 건물 **옆의 빈 칸**까지 간 뒤 적 성으로 진군한다(멈추지 않는다). |
| S-2 | 랠리를 **적 건물**·**금광** 위에 꽂고 생산 | S-1과 같다. |
| S-3 | 랠리 버튼 → 맵의 **빈 곳(막힌 타일)** 또는 **맵 바깥** 탭 | 토스트 「랠리포인트를 지정할 수 없는 위치입니다」. 창은 다시 열리지 않는다. 이어서 정상 칸을 탭하면 그 칸에 꽂힌다. |
| S-4 | 배럭 A 랠리 지정 → 3초 안에 배럭 B 열기 → B에서 랠리 지정 → 기다리기 | B를 연 순간 A 깃발이 사라지고 B 깃발만 보인다. B 지정 3초 뒤 B 깃발도 사라진다. A 깃발이 다시 나타나지 않는다. |
| S-5 | AI가 금광을 노리는 시점까지 진행(AI 켠 상태) | 내 화면에 AI 깃발이 나타나지 않는다. 내 깃발 3초 타이머가 AI 때문에 끊기지 않는다. AI 유닛이 금광 근처에서 멈춰 서지 않고, 결국 금광을 점령한다(R6 관찰). |
| S-6 | 빈 칸에 랠리 → 유닛이 걸어가는 도중 그 칸에 건물 짓기 | (관찰만) 걷던 유닛이 멈추는지 기록한다(R4). 그 뒤 생산된 유닛은 건물 옆 칸으로 간다(S6). |
| S-7 | 랠리 지정 직후 3초 안에 맵의 일반 타일 / 빈 곳 / 내 다른 건물 탭 | 깃발이 바로 사라진다. 내 생산 건물을 탭했으면 **그 건물의 깃발만** 다시 보인다. (빈 곳 탭도 숨기는지 — P7 확인 결과에 따라 기대값 확정) |
| S-8 | 랠리 칸 주변 3칸이 모두 건물·금광·빈 곳인 위치에 랠리 → 생산 | 유닛이 스폰 칸에 서 있지 않고 곧장 적 성으로 진군한다. |

**멀티플레이 (Host·Client 양쪽에서 같은 결과여야 한다 — Network 최상위 원칙)**

| # | 해 볼 것 | 기대 결과 |
|---|---|---|
| M-1 | Host와 Client 각각 S-1·S-2 | 양쪽 모두 유닛이 건물 옆 칸 → 적 성. |
| M-2 | Host와 Client 각각 S-3 | 탭한 쪽 화면에만 토스트. 상대 화면에는 아무것도 없다. |
| M-3 | Host와 Client 각각 S-4 | 양쪽 모두 앞 배럭 깃발이 남지 않는다. |
| M-4 | Host·Client 각각 랠리 지정 | 상대 화면에 내 깃발이 보이지 않는다(규칙 1). |
| M-5 | 정상 지정 후 Host 로그 확인 | `서버: 랠리포인트 설정 완료`가 남고, 지정 불가 거부 로그(`Reason=Coord`)는 정상 플레이에서 나오지 않는다. |
| M-6 | Host·Client 각각 S-6 | (관찰만) R4. |

---

## 10. 미확인 항목

| 위치 | 주장 | 미확인 사유 |
|---|---|---|
| 맵 정의(템플릿·고정 맵) | 팀 시작 광산이 막힌 타일 위에 놓이지 않는다 | 중립 광산의 막힌 타일 제외만 규칙 문서에서 확인했다. 시작 광산 쪽 정의·투영 코드(`MapProjectionUseCase.cs` 270~300행 부근)에서 막힌 타일 배제를 확인하지 않았다 |
| `LocalPlayerTeam.cs` / `NetworkGameFlow.cs` 190행 | 멀티 Client 경기 뒤 같은 실행에서 싱글을 하면 `LocalPlayerTeam.Current`가 Red로 남는다 | `Reset()` 호출처가 없다는 검색 결과에서 추론. 씬 전환·도메인 리로드 시점 동작은 실행 확인하지 않았다 |
| `FlowFieldService.cs` 21행 | 187칸 BFS가 1ms 미만 | 코드 주석의 주장. 측정하지 않았다 |
| `UnitView.cs` 3351행~ `OnPathInvalidated` | 걷는 도중 랠리 칸에 건물이 생기면 이동 불가 목적지로 재경로 → TerminalContact 경로 가능 | 코드 읽기만 함. 재현하지 않음 |
| `AIOpponentController.TryCaptureMine` 616~634행 | 인접 칸을 잠깐 들른 뒤 성으로 가도 금광 점령 조건이 충족된다 | `PlaceMiningPost`의 인접 점령 조건을 이번에 추적하지 않았다 |
| `InputHandler.HandleClick` | 유닛을 누르면 그 아래 타일 클릭으로 처리된다 | Research §4-4와 같음 — 실행 미확인 |
| P5 전체 | 이동 가능 칸을 최종 목적지로 삼으면 Research §3-3 실패 경로를 타지 않는다 | `HexFlowField.GetPath`·`UnitView` 판정식에서 추론. 실행 미확인 |

## 11. 승인 전에 사용자에게 확인할 것

1. **P7 숨김 범위** — 빈 공간·막힌 타일 클릭(아무것도 선택되지 않는 클릭)에도 깃발을 숨길지. 이 계획은 메인 설계대로 「숨긴다」로 적었다.
2. **P1 판정 위치** — `UnitProductionUseCase`에 두고 `GridInteractionUseCase`와 같은 식을 두 곳에 두는 방식(§2-4)으로 진행해도 되는지. 대안은 Domain에 공용 판정을 두고 두 UseCase가 함께 쓰게 하는 것이다(이 경우 `GridInteractionUseCase` 코드도 바뀌고 TDD 「HexTile 런타임 상태 계약」 검토가 필요하다).
3. **`SetRallyPoint` 내부 방어선** — 싱글 경로·AI·업그레이드 복원까지 같은 판정으로 막는 것(§2-4)에 동의하는지.
4. **R4** — 걷는 도중 랠리 칸 건설 시 재경로 문제는 이번에 관찰만 하고, 재현되면 별도 작업으로 다루는 것으로 해도 되는지.

---

## 12. 2026-10-18 실기 결과 반영·이동 중 건설 후속 작업

### 12-1. 최초 구현과 사용자 확인 범위

최초 수정은 커밋 `9aa921f9`에 반영됐다. 사용자는 대화의 싱글 테스트 **1·2·3·4·6은 정상적으로 작동하는 것으로 보인다**고 보고했고, **5번 AI 항목은 눈으로 확인하지 못했다**고 밝혔다. 멀티도 확인했다고 보고했다. 자세한 항목별 원문 대응과 검증 한계는 같은 폴더 `Research.md` §9-1을 따른다. §9의 M 항목 전체나 Host·Client 각각의 실행·서버 로그까지 확인됐다는 의미는 아니다.

**생산 시점과 이동 도중을 구별한다.** 건설 후 생산된 유닛은 근접 타일을 거쳐 정상 이동한 반면, 이미 랠리 칸으로 걷던 유닛은 그 칸의 건설로 멈춘다는 사용자 관찰이 있다. 후자는 해결되지 않았다. AI 항목과 기존 미확인 목록을 이번 보고만으로 완료 처리하지 않는다.

### 12-2. 후속 범위와 계획 구체화 조건

사용자는 이 멈춤 문제의 작업 진행과 기존 task 갱신을 승인했다. 따라서 **R4·§7·§11의 관찰 전용 경계는 최초 계획의 이력이며, 현행 후속 조사·계획 대상은 이동 도중 랠리 목적지에 건물이 생긴 경우**다. 현재 변경할 메서드와 수정 방법은 원인 조사 후 확정한다. 이 갱신은 후속 구현 완료나 새로운 규칙 확정을 뜻하지 않는다.

1. 현재 코드에서 랠리 이동의 목적지·경로 무효화·재경로·완료 콜백의 생명주기를 추적해, 생산 후 대체 이동이 정상인 경로와 차이를 확인한다. 근거는 Research의 사용자 확정 사양 S5·S6과 §9-2의 관찰이다.
2. 랠리 전용 대체 목적지 선택을 다시 적용할 지점과 기존 이동·전투 접근 계약에 미치는 영향을 조사한다. `GameSystemRules_Units.md` 「유닛 이동 시스템」의 건물 경로 차단·타일 중심 수렴 계약, 기존 Plan §1에 인용한 서버 권위 이동 규칙을 보존하는 수정 범위를 먼저 제시한다.
3. 원인·수정 후보·영향 범위·위험을 이 Research/Plan에 추가하고 메인 세션 검토로 넘긴다. 가설을 확정 원인으로 쓰거나, 이 단계에서 후속 수정·실기 통과를 선언하지 않는다. 규칙 45의 전체 경로 차단 후속 구현과 동일 문제라고 단정하지 않는다.

기존 주석 비활성화 목록 D1~D3의 정리 완료 여부도 이번 사용자 보고만으로 선언하지 않는다. 특히 D3 관련 AI 실기는 미확인이며, 이 문서 작업에서는 코드 정리를 수행하지 않았다. 새 Testcase 파일·메모리 파일은 만들거나 수정하지 않았다.

<a id="12-3-병합-후-후속-구현-후보--명시적-계획-승인-대기"></a>
### 12-3. 병합 후 후속 구현 계획 — 사용자 승인·구현 반영

다음은 main 병합 후 programmer 정적 조사에 기반해 메인 세션이 전달한 후속 계획이다. **2026-10-18 사용자가 진행을 승인했고 game-programmer가 구현 반영·별도 컴파일 결과를 전달했다(§12-4).** 문서 담당의 실행 검증 결과나 상세 원인의 실기 확정 판정은 아니다. 최초 §11의 질문을 다시 미결로 열지 않는다.

| 항목 | 후속 후보 | 보존할 계약·근거 |
|---|---|---|
| 랠리 경로 질의 공용화 | `ProductionTicker.ResolveRallyPath`의 링 선택을 `UnitMovementUseCase`의 랠리 전용 질의로 공용화한다. 원래 랠리 칸과 실제 도착 칸을 분리하고, 현재 칸 도착·후보 없음을 구별하는 결과 형식을 둔다. | 사용자 확정 S5·S6·S8: 이동 가능한 후보만, 링 1→2→3 우선, 같은 링에서는 출발점 기준 최단 경로. |
| 랠리 이동 문맥 | `UnitView`에 랠리 시작 진입점과 최소 문맥을 둔다. 일반 `MoveTo` 전환 시 해당 문맥을 해제한다. | 일반 성 진군·전투 이동을 랠리로 오인하지 않도록 구별한다. |
| 재경로에서 대체 칸 재선택 | 공통 경계 `RequestLatestAuthoritativeRepath`에서 활성 랠리라면 대체 도착 칸을 다시 고른다. 전투 복귀·막힘·refresh 경로도 같은 경계를 확인한다. | 기존 최신 권위 Root·revision 검증을 유지한다. 최초 Plan §1의 서버 권위 이동·건물 경로 차단 계약. |
| 목적지 원자적 전환 | 안전한 수용 경계에서 `UnitNavigationObjective`의 읽기 전용 목적지와 `_currentDestination`, `finalTarget`, 대기 경로를 함께 전환한다. | 타일 중심 수렴·공간 상태 계약과 기존 무진전 차단을 보존한다. guard를 매 프레임 초기화하지 않는다. |
| 종료 후 성 진군 | 이미 대체 칸에 도착했거나 후보가 없으면 기존 `OnMoveComplete` → 성 진군 연결을 한 번만 실행한다. | S5·S6. 코루틴 내부 콜백 재진입이나 자기 `StopCoroutine`으로 전환을 처리하지 않는다. |

계획 시 예상 후보 파일은 `ProductionTicker.cs`, `UnitView.cs`, `UnitMovementUseCase.cs`였으며, 결과 형식·목적지 전환에 필요하면 `UnitMovementContracts.cs`도 포함하도록 했다. 새 DI·RPC·씬 변경은 계획하지 않았다. 일반 성 이동·전투 접근의 이동 불가 종점 덧붙임 계약, 서버 이동 작성 권한, 공간 commit·무진전 차단은 유지하는 범위다. 이 표는 승인 계획이며, 실제 반영·한정 확인 결과는 §12-4를 따른다.

<a id="12-4-구현-반영별도-컴파일-완료--사용자-실기-대기"></a>
### 12-4. 구현 반영·별도 컴파일 결과

programmer/메인 인계에 따르면 최종 코드 변경은 `UnitMovementContracts.cs`, `UnitMovementUseCase.cs`, `ProductionTicker.cs`, `UnitView.cs`다. 승인 계획의 랠리 전용 목적지 질의를 생산 시·권위 재경로에서 공용으로 사용하고, 원래 랠리와 실제 도착 칸, 현재 칸 선택·후보 없음·질의 실패를 구분하는 후속 수정이다. 기존 일반 이동·전투 접근 경계는 보존하는 범위로 인계됐다.

Runtime Roslyn exit 0/errors 0/warnings 23(기준선 warnings 23), Editor Roslyn exit 0/errors 0/warnings 0이며 임시 CLR production 호출도 exit 0으로 보고됐다. CLR의 확인 항목과 증거 한계는 같은 폴더 `Research.md` §9-5를 따른다. 이 결과는 **별도 컴파일·한정 CLR 확인**이며 에이전트의 실제 Unity 코루틴·NetworkTransform·멀티 실기 실행 결과가 아니다. 사용자 후속 확인은 §12-5에서 구분한다.

최종 코드 변경 없이 재컴파일 결과가 동일하게 확인됐고 최종 DLL CLR 실제 7항목·diff 공백 검사도 exit 0이라는 programmer/메인 최종 인계를 받았다. 임시 산출물 위치와 보존 한계는 Research §9-5에 기록했다. 문서 담당의 재실행 결과는 아니다.

이 단계의 별도 검사를 사용자 실기나 상세 원인의 실행 입증으로 확대하지 않는다. 이후 사용자 싱글·멀티 수용과 사양 동기화는 §12-5를 따른다. AI 5번과 기존 최초 구현의 주석 정리 완료 여부는 이 결과로 확장하지 않는다.

### 12-5. 사용자 싱글·멀티 수용과 사양 동기화

2026-10-18 사용자 싱글 정상 보고에 이어 멀티 정상 보고·문서 갱신 요청을 받아 **이동 도중 랠리 목적지 건설 시 정지 문제를 사용자 확인 범위에서 수용·종결**했다. 자세한 원문과 역할별·장치별·정밀 계약 수용의 한계는 같은 폴더 `Research.md` §9-6을 따른다. 당시 별도 미확인이던 AI 5번은 이후 §12-6에서 사용자 싱글 수용됐으며 유닛 이동 규칙 45의 전체 경로 차단 대응은 별도다.

최초 §8의 동기화 대상은 현행 원본 역할에 맞춰 반영했다. `GameSystemRules_Buildings.md` 「랠리포인트 시스템」에 선택 배럭만 표시·맵 탭 숨김·지정 불가 위치·대체 목적지 우선순위·이동 중 건설 재선택을 기술했다. `GameSystemRules_BuildingPanelsUI.md`의 생산 패널과 토스트 표, `GameSystemRules_AI.md`의 금광 칸 지정 문구를 맞추고, TDD는 랠리 전용 `RequestRallyPath`와 공성 공용 폴백을 분리했다. GDD는 이미 원본 링크 중심 요약이므로 수정하지 않았다. 문서 담당은 코드 삭제·새 Testcase·메모리 편집을 수행하지 않았다. 메모리 갱신은 game-programmer가 별도로 담당했다.

### 12-6. AI 5번 수용 후 불필요 코드 정리·main 병합 준비

사용자가 AI 5번의 싱글 정상 동작을 보고하고 문서 반영·불필요 코드 삭제·main 병합 준비를 승인했다. 관련 상세 범위는 Research §9-7을 따른다. game-programmer가 D1·D2·D3과 옛 랠리 목적지 선택 코드의 불필요 부분 정리·메모리 갱신을 완료했다. 최초 삭제 안전 근거는 최상단 표로 보존하며, 공성 공용 폴백·현행 랠리 질의·전투 접근 계약은 유지한다.

programmer는 D1 주석 5곳·D2 1곳·D3 1곳, 비활성화된 옛 랠리 루프와 미참조 상수·버퍼·`CollectRing`의 삭제 완료를 전달했다. 제거 선언을 제외한 활성 Roslyn 토큰 동일·문법 오류 0, Runtime/Editor 컴파일 errors 0·warnings 23/0(이전 동일), 최종 DLL CLR 기존 7항목 exit 0이다. 상세 수치·최종 해시·임시 근거와 재실행 한계는 Research §9-9를 따른다. 공성 공용 폴백은 유지했다.

정리·별도 검증은 완료됐으며 신규 사용자 실기는 없었다. 메인의 main 대비 비교·검토·검사로 병합 전 준비 상태를 확인하되, 커밋·푸시·병합은 실행하지 않았다. 원격 fetch 미실행과 외부 변경 보존 경계는 Research §9-8을 따른다. 역할별·장치별·정밀 계약 측정이나 전체 경로 차단 대응까지 수용을 확대하지 않는다. 문서 담당은 코드·메모리를 편집하지 않았다.

### 12-7. 사용자 승인에 따른 로컬 main 통합 완료

사용자가 PR 없이 병합하도록 승인했고 메인은 origin fetch 후 작업 파일 20개를 `af5ac268`에 커밋한 다음 main에서 `claude/rally-point-fix`를 fast-forward 병합했다. 로컬 main 통합은 완료됐으며 §12-6의 병합 전 준비 상태는 당시 단계다. 이 로컬 통합 시점에는 PR 생성·원격 push를 하지 않았다. 이후 2026-10-19 사용자 「푸시하자」 승인에 따른 메인의 push가 exit 0으로 성공해 `9bb2c20e`까지 origin/main 반영을 완료했다. 정확한 refs와 외부 변경 보존 경계는 같은 폴더 `Research.md` §9-10을 따른다.

최종 통합·push 기록은 main에서 별도 문서 커밋으로 관리한다. 이 문서 담당은 코드·메모리·git을 변경하지 않는다. 원격 반영 완료를 통합 후 신규 실기 완료로 확대하지 않는다.
