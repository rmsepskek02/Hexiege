# 랠리포인트 버그 2건 — Research

> **현행 상태 (2026-10-18):** 이동 도중 건설 후속 수정의 싱글·멀티 및 AI 5번의 싱글 사용자 수용 후, 승인된 불필요 코드 정리·별도 검증이 완료됐다(§9-9). main 병합 전 준비 상태이며 커밋·원격 main 최신 확인·병합은 실행하지 않았다. 역할별·장치별·정밀 로그 계약까지 수용을 확대하지 않는다.

## 0. 이 작업이 무엇이고 왜 하는가 (자연어 설명)

생산 건물(배럭)에는 「랠리포인트」라는 집결 깃발을 꽂을 수 있다. 깃발을 꽂아 두면 그 건물에서 나온 유닛이 먼저 깃발 칸으로 걸어가고, 도착한 뒤 적 성으로 진군한다. 이번에 사용자가 이 기능에서 버그 두 개를 보고했다.

1. **깃발을 건물 위에 꽂으면 유닛 이동이 고장 난다.** 깃발 칸에 건물(아군이든 적이든)이 서 있으면, 그 배럭에서 나온 유닛이 건물 앞까지는 가지만 그다음 동작(적 성으로 진군)을 하지 못하고 멈춘다.
2. **멀티플레이에서 다른 배럭을 열면 앞 배럭의 깃발이 남아 있다.** A 배럭에 깃발을 꽂고 창이 닫힌 뒤 B 배럭의 생산 창을 열면, B의 깃발뿐 아니라 A의 깃발까지 화면에 계속 보인다.

이 문서는 최초 수정 전 코드 조사와 이후 사용자 실기 결과를 구분해 정리한다. §1~§8의 수정 전 분석을 현재 구현 상태로 읽지 않으며, 최신 실기 결과와 후속 작업 범위는 §9를 따른다.

이번에 사용자와 확정한 「원래 이렇게 동작해야 한다」는 사양은 §1에 그대로 옮겼다. 요약하면 다음과 같다. 깃발 칸에 유닛이 갈 수 없으면 근처(최대 3칸)의 갈 수 있는 칸으로 대신 간다. 그런 칸도 없으면 곧장 적 성으로 간다. 깃발은 그 건물을 골랐을 때 **그 건물 것만** 보인다.

### 표기 약속

- 위치는 **파일 경로 + 메서드명**을 기본으로 가리키고, 행 번호는 보조로 붙인다. 행 번호는 **2026-10-13 작성 시점(HEAD `d48aa27`, 작업 트리 변경 없음)에 직접 열어 확인한 값**이며, 파일이 바뀌면 낡는다(`.claude/mistakes.md` 2026-08-24 「행 번호로 위치를 가리켜 전부 어긋남」).
- 코드만 읽고 확인한 것은 **「정적 분석 — 실행 미확인」**이라고 적는다. 이번 Research에서는 Unity 실행·재현·로그 수집을 하지 않았다. 따라서 이 문서의 코드 동작 서술은 별도 표기가 없으면 전부 정적 분석이다.
- 이전 작업 문서에서 옮긴 사실은 「전달값」 또는 출처를 함께 적는다.
- 규칙을 인용할 때, 섹션마다 번호가 1부터 다시 시작하는 문서(`GameSystemRules_Buildings.md`, `GameSystemRules_UI.md`)는 섹션명을 함께 적는다.

---

## 1. 사용자 확정 사양 (2026-10-13 대화, 그대로 기록)

| # | 사양 |
|---|------|
| S1 | 랠리 버튼을 누르면 맵을 지정할 수 있도록 생산 창이 내려가고, 지정이 끝나면 창은 닫힌 상태로 남는다(현재 구현 방식 유지 — 사용자 선택 A). |
| S2 | 지정 직후 마커가 3초간 보이며, 그 전에 다른 오브젝트를 선택하면 바로 사라진다. |
| S3 | 랠리포인트가 설정된 건물을 선택하면 **그 건물의 마커만** 보인다(다른 배럭 마커는 숨김). |
| S4 | 랠리 칸이 그 배럭 자신의 칸이면 랠리포인트가 해제된다(이 규칙이 아래 대체 이동보다 우선). |
| S5 | 랠리 칸에 유닛이 갈 수 없으면(건물 — 아군/적 무관, 이동 불가 지형) 가장 가까운 이동 가능한 칸(최대 3칸)으로 이동한다. 그런 칸이 없으면 바로 적 성으로 진군한다. |
| S6 | 랠리 설정 후 그 칸에 건물이 지어져도 랠리는 유지하고, 유닛 생산 시마다 위 대체 이동으로 처리한다(마커는 그대로). |

### 1-1. 2026-10-13 추가 확정 사항 (같은 날 후속 대화)

> 아래는 §7-1의 사용자 확인 항목 네 개에 대한 답과, 그 대화에서 함께 정해진 「지정 불가 위치」 사양이다. 위 S1~S6 표는 그대로 두고 덧붙인다.

| # | 사양 |
|---|------|
| S7 | **금광 칸도 「갈 수 없는 칸」에 포함한다.** 금광 칸과 건물(아군/적 무관) 칸에도 랠리포인트를 **지정할 수는 있다.** 그 배럭에서 생산된 유닛은 S5대로 근처의 이동 가능한 칸으로 간다. |
| S8 | **「가장 가까운 칸」의 기준**: 먼저 **랠리 칸에서의 헥스 거리**가 가까운 칸(링 1 → 2 → 3 순)을 고른다. 같은 거리의 후보가 여럿이면 **유닛 출발점에서 경로가 짧은 칸**을 고른다. |
| S9 | **랠리포인트를 지정할 수 없는 위치 = 격자 밖 좌표 + 막힌 타일(`TileKind.Blocked`).** 막힌 타일은 `HexGridRenderer`가 타일 오브젝트를 만들지 않아(`Presentation/Grid/HexGridRenderer.cs` 198~208행에서 건너뜀) 화면에서는 「타일이 없는 곳」, 즉 맵에 듬성듬성 비어 보이는 곳이다. 이 판정은 기존 건물 배치 클릭 판정(`GameSystemRules_UI.md` 「건물 배치 패널 UI」 규칙 6 「막힌 타일 또는 빈 공간 클릭」 — 코드는 `GridInteractionUseCase.SelectTileByKind`의 `NotSelectable` 분기)과 **같은 기준**이다. |
| S10 | 지정할 수 없는 위치를 탭하면, 잠금 유닛 생산을 시도했을 때처럼 **토스트** 「랠리포인트를 지정할 수 없는 위치입니다」를 띄우고 **조준 상태를 유지한다**(창을 다시 열지 않고 곧바로 다른 칸을 다시 찍을 수 있다). |
| S11 | 멀티플레이에서는 **서버(`SetRallyPointServerRpc`)도 같은 판정으로 다시 검사**해, 지정할 수 없는 좌표면 무시한다(서버 권위). 토스트는 **입력한 플레이어 화면에만** 뜬다. |
| S12 | 싱글플레이에서 **AI의 랠리 마커가 플레이어 화면에 나타나거나 플레이어 마커의 3초 타이머를 끊는 문제(§4-6)도 이번 작업에서 함께 고친다.** |

**S5·S7·S9를 함께 읽을 때 (코드에서 확인한 사실과 그에 따른 해석):**
- `HexTile.TileKind`는 「맵 정의로 결정, 경기 중 불변」이다(`Domain/Hex/HexTile.cs` 8행 주석). 따라서 S9로 막힌 타일이 지정 단계에서 걸러지면, 이미 저장된 랠리 칸이 나중에 막힌 타일로 바뀌는 경우는 없다. 결과적으로 생산 시점에 S5의 「갈 수 없는 칸」으로 처리되는 칸은 **건물 칸과 금광 칸**이다(`HexTile.IsWalkable` 판정식 84~86행 기준의 해석 — 실행 미확인).
- S9의 「지정 불가」와 S5·S7의 「지정은 되지만 유닛이 갈 수 없음」은 서로 다른 판정이다. 앞의 것은 지형만 보고(건물·금광은 통과), 뒤의 것은 `HexTile.IsWalkable`(지형·금광·건물 모두)을 본다.

---

## 2. 관련 코드 지도

| 역할 | 파일 | 핵심 메서드 |
|------|------|-------------|
| 생산 완료 → 랠리 이동, 마커 관리, 공성 체인 | `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs` | `OnUnitProduced`, `OnRallyPointChanged`, `ShowMarkerTemporary`, `AutoHideMarker`, `ShowRallyMarker`, `HideAllRallyMarkers`, `MoveTowardEnemyCastle`, `FindPathToNearestEmptyTile` |
| 랠리 상태 저장·이벤트 발행 | `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | `SetRallyPoint`, `ClearRallyPoint`, `CompleteProduction` |
| 경로 계산 | `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs` | `RequestMove`, `RequestMoveCore`, `IsWalkable` |
| 플로우 필드(목적지 기준 BFS) | `Assets/_Project/Scripts/Domain/Hex/HexFlowField.cs` | `Compute`, `GetPath` |
| 이동 가능 판정식 | `Assets/_Project/Scripts/Domain/Hex/HexTile.cs` | `IsWalkable` |
| 생산 창 UI·랠리 지정 | `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | `OnShow`, `OnBeforeClose`, `OnRallyPointClick`, `CompleteRallyPointSetting` |
| 패널 공통 열기/닫기 | `Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs` | `Show`, `Close`, `OnBuildingDied` |
| 입력 분기 | `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` | `HandleClick` |
| 멀티 중계 | `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` | `RequestSetRallyPoint`, `SetRallyPointServerRpc`, `OnUnitProduced`, `SpawnUnitClientRpc` |
| 이동 공간 전이 계약 | `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs` | `UnitSpatialTransitionPolicy.TryBuild` |
| 유닛 이동 실행 | `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | `MoveTo`/`MoveToInternal`, TerminalContact 판정부, `MoveCleanupAndCompleteV3` |
| 랠리를 쓰는 다른 호출자(AI) | `Assets/_Project/Scripts/Application/Services/AIOpponentController.cs` | `SetRallyToMine`, `ClearAllRally` |

---

## 3. 버그 1 — 랠리 칸에 건물이 있으면 유닛 이동이 고장

### 3-1. 생산 완료 시 랠리 이동 흐름 (`ProductionTicker.OnUnitProduced`, 297~329행)

1. 유닛 오브젝트·`UnitView`를 찾고, 이미 이동 중이면 아무것도 하지 않는다(305행 `unitView.IsMoving`이면 return).
2. 랠리가 있으면 `_unitMovement.RequestMove(e.Unit, rallyTarget)`를 먼저 호출한다(311행).
3. **결과가 `null`일 때만** `FindPathToNearestEmptyTile(e.Unit, rallyTarget)`(BFS, 기본 maxRange 3)로 대체한다(314~315행). 주석은 「랠리포인트 타일이 점유 중이면」이라고 적혀 있다(313행).
4. 경로가 생기면 `OnMoveComplete = () => MoveTowardEnemyCastle(...)`를 걸고 `MoveTo(path)`(317~322행).
5. 랠리가 없으면 곧장 `MoveTowardEnemyCastle`(324~328행).

### 3-2. 건물 칸 랠리에서 `RequestMove`가 `null`이 아닌 이유

- `UnitMovementUseCase.RequestMove`(86~92행)는 `RequestMoveCore`(204~229행)를 부르며, `RequestMoveCore`는 `HexFlowField.GetPath(start)` 결과가 `null`이거나 길이 2 미만일 때만 `null`을 돌려준다(226행). `RequestMove`의 요약 주석도 「목적지가 non-walkable(성/건물)이라도 GetPath()가 마지막에 목적지를 덧붙여 주므로…」라고 적고 있다(74~75행).
- `HexFlowField.Compute`(77행~)는 목적지가 이동 불가이면 **목적지의 이동 가능한 이웃 칸들을 BFS 출발점(seed)**으로 삼는다(101~108행).
- `HexFlowField.GetPath`(173행~)는 seed까지 경로를 따라간 뒤, 목적지가 flow에 키로 없으면(=이동 불가 목적지) **경로 끝에 목적지 좌표를 덧붙인다**(218~229행).
- 「이동 불가」의 판정식은 `HexTile.IsWalkable`(84~86행)이다. **막힌 지형(`TileKind.Blocked`)이 아니고, 광산이 없고(`MineKind == None`), 건물이 없어야** 이동 가능하다. 따라서 위 「목적지 덧붙임」은 건물 칸뿐 아니라 **막힌 지형 칸·광산 칸**에도 똑같이 적용된다(정적 분석 — 실행 미확인).

**결론(정적 분석 — 실행 미확인):** 랠리 칸이 건물 칸이고 그 건물에 이동 가능한 이웃이 있어 유닛이 닿을 수 있으면 `RequestMove`는 「… → 건물 앞 칸 → 건물 칸」 경로를 돌려준다. 그래서 314행의 BFS 대체는 **실행되지 않는다.**

**추가 사실 — BFS 자체도 건물 칸을 걸러내지 못한다:** `FindPathToNearestEmptyTile`(753~787행)은 Ring 0(랠리 칸 자신)부터 각 칸에 `_unitMovement.RequestMove(unit, current)`를 시도하고, `null`이 아니면 바로 반환한다(758·766~768행). Ring 0에 같은 `RequestMove`를 쓰므로, 이 BFS를 건물 칸에서 호출하더라도 위와 같은 이유로 **Ring 0에서 건물 칸 종점 경로를 그대로 돌려줄 것**이다(정적 분석 — 실행 미확인). 즉 「null일 때만 BFS」 조건만 바꿔서는 S5가 성립하지 않는다. 각 칸이 실제로 이동 가능한지(`IsWalkable`)도 함께 봐야 한다는 사실을 Plan에 넘긴다.

### 3-3. 건물 칸 종점 경로가 이동을 고장 내는 경로 — 이전 분석 인용

출처: `Assets/_Project/Docs/_Tasks/2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/Research.md` 「2026-10-02 이동 최종 분석 — 확정 실패 경로와 유력 근본 원인 구분」 절. 아래는 그 절의 내용을 옮긴 **전달값**이다.

- **확정으로 적힌 것:** 서버 공간 상태 불일치 → Fatal → 종료 거부 → 후속 이동 취소.
- **「유력 근본 원인」으로 적힌 것:** 건물 랠리 종점의 TerminalContact 계약 충돌. Root(시뮬레이션 위치)는 건물 칸으로 들어가지만 도메인의 커밋 타일은 인접 칸에 남는다. 다음 이동 planner가 「출발 샘플 ≠ 커밋 타일」을 InvalidInput으로 거부하고 Fatal이 되며, 정리 과정이 후속 이동(랠리 뒤 적 성 이동 콜백)을 취소한다.
- **로그 결속(전달값, 메인 raw-log 인계):** 2차 Android Host 로그 `Assets/_Project/Docs/_Logs/2026-10-02/13_08_logcat/RuntimeLog_device.txt`의 배럭38 rally(9,-2)(1025행) → RedTrainingCamp46 (9,-2)(1274행) → LittleKnight129 spawn(7,-2)(1335행) → 13:04:59.679 unit129 committedPosition(8,-2) / rootBeforeHex·candidateHex(9,-2) InvalidInput/Fatal(1379행).
- **그 문서가 스스로 적은 한계(그대로 옮김):** 「직전 TerminalContact 성공 프레임 raw data가 없고 재현도 수행하지 않아 완전한 runtime causal 증명으로 확대하지 않는다.」 즉 **유력 근본 원인일 뿐 실행으로 증명된 원인이 아니다.** 이번 Research에서도 재현하지 않았다.

**이번에 소스를 다시 열어 대조한 결과(정적 분석 — 실행 미확인):**

| 위치 | 확인 내용 |
|------|-----------|
| `UnitMovementContracts.cs` `UnitSpatialTransitionPolicy.TryBuild`(1540행~) | 첫 샘플 타일 ≠ 커밋 타일이면 `InvalidInput`(1555~1557행). 이동 불가 종점(`hasNonWalkableTerminalContact`)의 샘플에 닿으면 `terminalReached = true; continue;`로 **공간 전이 버퍼에 넣지 않는다**(1616~1620행). 마지막에 `TerminalContact`를 반환한다(1632~1633행). 이전 문서의 「1551 부근 / 1616 부근」과 일치한다. |
| `UnitView.cs` TerminalContact 판정부(2792~2804행) | 마지막 샘플이 논리 경로의 마지막 칸과 같고 `_movementUseCase.IsWalkable(lastSample)`가 거짓이면 TerminalContact로 본다. 판정 근거가 `IsWalkable`이므로 **건물뿐 아니라 막힌 지형·광산 칸 종점도 같은 분기를 탄다**(정적 분석). |
| `UnitView.cs` 위치 적용·도메인 커밋(2512~2550행) | 이동이 허용되면 `transform.position`을 갱신하고(2512~2531행), **`spatialTransitionCount > 0`일 때만** `CommitPreflightedSpatialTransitionsPreservingFacing`으로 도메인 커밋을 한다(2534~2550행). ⚠️ 이전 문서는 이 두 자리를 「1503 부근 / 1525 부근」으로 적었다. 현재 파일에서는 위 행으로 **밀려 있다**(내용은 같은 구조로 확인). |
| `UnitView.cs` 이동 종료 정리(5103~5123행, `MoveCleanupAndCompleteV3` 6238행~) | 실패로 닫힌 이동(`movementFailedClosed`)이면 navigation objective를 `Cancel`하고 `invokeCompletion: false`로 정리한다(5118~5123행). `MoveCleanupAndCompleteV3`는 `OnMoveComplete`를 비운 뒤 `invokeCompletion`일 때만 호출한다(6267~6270행). → 랠리 뒤 `MoveTowardEnemyCastle` 콜백이 실행되지 않는 경로가 코드상 존재한다. |

### 3-4. 대체 경로가 없을 때의 현재 동작

- `OnUnitProduced`의 랠리 분기에서 `RequestMove`와 BFS가 모두 `null`이면 `MoveTo`도 `OnMoveComplete` 등록도 하지 않는다(317~322행에 else 없음). 이 경우 **유닛은 스폰 칸에 그대로 서 있고 공성 목록에도 등록되지 않는다**(정적 분석 — 실행 미확인). S5의 「그런 칸이 없으면 바로 적 성으로 진군」과 다르다.
- 비교: `MoveTowardEnemyCastle`(580~629행)은 경로가 `null`이어도 `RegisterSiege`를 호출해 공성 목록에 넣는다(624~628행). 그러면 `TickSiege`가 1초마다 다시 시도한다.
- 참고로 랠리 도착 후 체인은 `OnMoveComplete` → `MoveTowardEnemyCastle`이다(320행). 이 체인은 3-3의 실패 정리(`invokeCompletion: false`) 경로에서 끊긴다.

### 3-5. 랠리 설정 시점의 처리 (S4·S6 관련)

- **자기 칸 해제(S4)는 이미 구현되어 있다:** `UnitProductionUseCase.SetRallyPoint`(465~479행)는 `target == state.BarracksPosition`이면 `ClearRallyPoint`를 호출하고 끝낸다(470~474행). 이 판정은 **설정 시점**에 이루어지므로, 생산 시점의 대체 이동보다 항상 먼저 적용된다.
- **설정 시점에 칸 검증은 없다:** `InputHandler.HandleClick`의 랠리 분기(219~230행)는 클릭 좌표를 그대로 `CompleteRallyPointSetting(rallyCoord)`에 넘긴다. `CompleteRallyPointSetting`(565~593행)과 `SetRallyPoint`도 건물·지형·격자 밖 여부를 검사하지 않는다. 따라서 **건물 칸·막힌 지형·광산 칸(그리고 격자 밖 좌표로 보이는 칸)도 랠리로 저장된다**(정적 분석 — 격자 밖 좌표가 실제로 저장되는지는 실행 미확인).
- **설정 후 건물이 지어지는 경우(S6):** 랠리 좌표는 `ProductionState.RallyPoint`에 좌표로만 저장된다(`ProductionState.cs` 150행). 건물 배치 이벤트가 랠리를 지우는 코드는 찾지 못했다(`ProductionTicker`의 건물 배치 구독 `OnBuildingPlaced`(366~374행)·`OnWalkableChanged`(218~222행)는 랠리를 건드리지 않음). 따라서 랠리는 유지되고, 다음 생산부터는 3-2와 같은 「건물 칸 종점」 경로가 만들어진다(정적 분석 — 실행 미확인). 이미 랠리 칸으로 걷고 있던 유닛이 건설 순간 어떻게 되는지(재경로)는 **미확인**이다.
- 업그레이드 시 랠리는 보존된다: `ProductionTicker.OnBuildingUpgraded`(383~405행)가 이전 좌표를 저장했다가 새 건물에 `SetRallyPoint`로 복원한다.

### 3-6. 멀티플레이에서 이동이 결정되는 위치

- **서버(호스트):** `ProductionTicker.Update`는 서버·싱글에서만 `_productionUseCase.Tick`을 돌린다(246~260행). 생산 완료 시 `UnitProductionUseCase.CompleteProduction`이 `GameEvents.OnUnitProduced`를 발행하며(721~722행) 랠리 좌표를 함께 싣는다. 서버의 `ProductionTicker.OnUnitProduced`가 이를 받아 경로를 계산하고 `MoveTo`를 호출한다.
- **클라이언트:** `NetworkProductionController.OnUnitProduced`(397~439행, 서버 전용 가드 410행)가 `SpawnUnitClientRpc`로 랠리 좌표(`rallyQ/rallyR/hasRally`)를 보낸다. 클라이언트는 `SpawnUnitClientRpc`(607행~, 호스트는 618행 `if (IsServer) return;`으로 제외)에서 유닛 데이터를 재생성한 뒤 `GameEvents.OnUnitProduced`를 다시 발행한다(685행). 그러면 클라이언트의 `ProductionTicker.OnUnitProduced`도 실행된다.
- 다만 `UnitView.MoveTo` → `MoveToInternal`은 `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`이면 즉시 반환한다(3152행). 메서드 주석은 「클라이언트는 NetworkTransform이 서버 위치를 보간·동기화」라고 적고 있다. 따라서 **실제 이동은 서버(호스트)에서만 결정된다.** 클라이언트에서는 `RequestMove` 계산과 `OnMoveComplete` 대입까지만 일어나고 이동은 시작되지 않는다(정적 분석 — 실행 미확인).
- **결론:** 수정 지점이 `ProductionTicker.OnUnitProduced` / `FindPathToNearestEmptyTile` 쪽이면 **싱글과 서버(호스트) 양쪽에 같은 코드로 적용된다.** 클라이언트의 같은 코드는 이동을 일으키지 않는다. `GameSystemRules_Units.md` 「유닛 이동 시스템」 규칙 3도 「유닛 이동 결정은 서버에서만」이라고 적고 있다.

### 3-7. 같은 경로를 쓰는 다른 호출자 — 싱글 AI의 금광 랠리 (새로 확인)

- `AIOpponentController.SetRallyToMine`(662~666행)는 AI의 모든 생산 건물 랠리를 **금광 칸 좌표**로 설정한다. 금광 칸은 `HexTile.IsWalkable` 판정식상 이동 불가(`MineKind != None`)다.
- 따라서 싱글플레이 AI 유닛도 3-2·3-3과 같은 「이동 불가 종점」 경로를 탈 수 있다(정적 분석 — 실행 미확인, AI 유닛에서 Fatal이 실제로 나는지는 로그로 확인하지 않음).
- S5를 「랠리 칸이 이동 불가이면 근처 이동 가능 칸으로」로 구현하면 **AI의 금광 행군 동작도 바뀐다**(금광 칸으로 들어가려는 대신 근처 칸에서 멈춘 뒤 성으로 진군). AI의 금광 점령은 `TryCaptureMine`(인접 타일 점령 조건)이 판정하므로 영향 여부를 Plan에서 따로 확인해야 한다(§7).

---

## 4. 버그 2 — 다른 배럭을 열면 앞 배럭 마커가 남음

### 4-1. 마커 관리 구조 (`ProductionTicker`)

| 요소 | 위치 | 동작 |
|------|------|------|
| 마커 사전 | 83행 `_rallyMarkers` | 배럭 Id → 마커 오브젝트. 배럭마다 하나 |
| 자동 숨김 코루틴 | 86행 `_autoHideCoroutine` | **배럭 전체가 공유하는 단 하나의 참조** |
| 표시 시간 | 91행 `RallyMarkerShowDuration = 3f` | |
| `CreateOrMoveMarker` | 447~470행 | 새 마커는 숨김 상태로 생성(467행) |
| `ShowMarkerTemporary(id)` | 487~497행 | 공유 코루틴을 멈추고(492~493행), 그 마커를 켜고, `AutoHideMarker(id)`를 새로 시작 |
| `AutoHideMarker(id)` | 502~510행 | 3초 뒤 **그 id의 마커만** 끔 |
| `ShowRallyMarker(id)` | 515~526행 | 공유 코루틴을 멈추고(518~522행) 그 마커를 켬. **다른 마커는 끄지 않는다** |
| `HideAllRallyMarkers()` | 531~545행 | 공유 코루틴을 멈추고 모든 마커를 끔 |
| `OnRallyPointChanged` | 334~360행 | 좌표가 있으면 `CreateOrMoveMarker` + `ShowMarkerTemporary`, 없으면 `DestroyMarker` |

외부에서 `ShowRallyMarker`·`HideAllRallyMarkers`를 부르는 곳은 **`ProductionPanelUI.cs` 두 곳뿐**이다(333행 `OnShow`, 351행 `OnBeforeClose`). 전수 검색으로 확인했다.

### 4-2. 생산 창의 랠리 흐름 (`ProductionPanelUI`)

- `OnShow`(329~342행): `_ticker.ShowRallyMarker(building.Id)`(333행). `BuildingPanelBase.Show`(200~224행) 끝에서 호출된다.
- `OnBeforeClose`(348~352행): `_ticker.HideAllRallyMarkers()`(351행). `BuildingPanelBase.Close`(244~262행)의 첫 줄에서 호출된다.
- `OnRallyPointClick`(547~563행): `IsSettingRallyPoint = true`, `_popup?.Hide()`(553행) — **`Close()`가 아니다.** 그다음 `HideBlockingOverlay()`(562행)를 부른다. 따라서 `OnBeforeClose`가 실행되지 않고, 이 시점에 열려 있던 마커는 그대로 켜져 있다.
- `CompleteRallyPointSetting`(565~593행): 멀티이면 `RequestSetRallyPoint`(569~570행), 이어서 로컬 `_production.SetRallyPoint`(571행)를 부른다. `IsSettingRallyPoint = false` 뒤 `HideBlockingOverlay()`(590행), 끝에 `_currentBuilding = null`(592행). **`Close()`를 거치지 않으므로 `HideAllRallyMarkers`가 불리지 않는다.** 창은 숨겨진 채로 남는다(S1과 일치).

### 4-3. 앞 배럭 마커가 남는 시나리오 (정적 분석 — 실행 미확인)

**시나리오 A — B 창이 열려 있는 동안 A 마커가 보임**
1. A 랠리 지정 완료 → `OnRallyPointChanged(A)` → `ShowMarkerTemporary(A)`: A 마커 켬 + 공유 타이머(A용) 시작.
2. 3초 안에 B를 누름 → `Show(B)` → `OnShow` → `ShowRallyMarker(B)`: **공유 타이머(A용)를 취소**하고 B 마커를 켬. A 마커는 끄지 않음.
3. → A 마커가 타이머 없이 켜진 채 남는다. B 창을 배경 탭 등으로 `Close()`하면 `HideAllRallyMarkers`로 함께 꺼진다.

**시나리오 B — A 마커가 무기한 남음**
1~2. 위와 같음(A 마커가 타이머 없이 켜짐).
3. B 창에서 랠리 버튼 → `_popup?.Hide()`(Close 아님) → B 랠리 지정 완료 → `ShowMarkerTemporary(B)`: B용 타이머만 새로 시작.
4. 3초 뒤 B 마커만 꺼진다. **A 마커는 어떤 타이머도 없이 계속 켜져 있다.** 다음에 어느 생산 창이 `Close()`될 때까지 꺼지지 않는다.

**참고 — 3초가 지난 뒤 B를 여는 경우:** A 마커는 이미 `AutoHideMarker`로 꺼졌으므로 증상이 없다. 따라서 이 버그는 **타이밍에 따라 나타났다 안 나타났다 한다**(정적 분석).

**S3과의 차이:** 시나리오 A의 2단계에서 다른 마커를 끄는 처리가 없으므로, 「그 건물의 마커만 보인다」(S3)가 현재 코드로는 성립하지 않는다.

### 4-4. 지정 완료 후 다른 오브젝트를 선택할 때 마커를 숨기는 경로 (S2 관련)

`InputHandler.HandleClick`(207행~)을 순서대로 확인했다.

- 랠리 지정 분기(219~230행)는 `IsSettingRallyPoint`일 때만 탄다. 지정이 끝나면 꺼진다.
- **건물 칸 클릭**(279~338행): 자기 팀 생산 건물이면 `_productionUI.Show`(294행). 이때 `ShowRallyMarker(그 건물)`만 불리고, 다른 마커를 끄지 않는다. 연구소·스킬 건물·MistShrine·기타 건물이면 각각 다른 패널을 연다. 이 경로들에는 랠리 마커를 숨기는 호출이 없다. 적 건물이면 선택만 하고 아무 패널도 열지 않는다.
- **금광/빈 타일/막힌 지형/건설 불가 타일**(344행~): 배치 패널을 열거나 닫는 처리만 있고 랠리 마커를 숨기는 호출은 없다.
- **유닛 선택:** `HandleClick`에는 유닛 전용 분기가 없다. `Presentation/Input/`에는 `InputHandler.cs`·`SkillAimController.cs`만 있고, 전체 스크립트에서 `SelectUnit`/`UnitSelected` 식별자도 0건이다. 유닛을 누르면 그 아래 타일로 처리되는 것으로 보인다(정적 분석 — 실행 미확인).
- **결론:** 지정 완료 후(창이 숨겨진 상태) 다른 오브젝트를 선택해도 **마커를 즉시 숨기는 경로는 현재 없다.** 마커는 3초 타이머로만 꺼지거나(그것도 4-3처럼 다른 생산 창이 타이머를 취소하면 남는다), 다른 생산 창이 `Close()`될 때 꺼진다. 다만 다른 생산 건물을 선택했을 때는 위 4-3 경로를 탄다.
- 참고: 지정 완료 직후 `_productionUI`는 `IsOpen == false`이고 `_currentBuilding == null`이며, `ClosedFrame`도 갱신되지 않는다(`Close()`를 거치지 않으므로).

### 4-5. 멀티플레이 특이점 (사실만)

- **랠리 변경 이벤트의 팀 필터:** `ProductionTicker.OnRallyPointChanged`는 멀티일 때 `localTeam = IsNetworkServer ? Blue : Red`로 정하고, 이벤트 팀이 다르면 무시한다(341~345행). 싱글에서는 이 필터가 없다.
- **클라이언트(Red):** `CompleteRallyPointSetting`이 `RequestSetRallyPoint` → `SetRallyPointServerRpc`(992행~)를 서버로 보내고, 로컬 `_production.SetRallyPoint`도 호출한다(571행). 서버 쪽 `SetRallyPoint`가 발행한 이벤트(Team=Red)는 서버의 `ProductionTicker`에서 필터로 무시된다(서버 localTeam=Blue). 클라이언트에서는 로컬 호출 1회분의 이벤트로 마커가 생성·표시된다. `SetRallyPointServerRpc` 주석(975~986행)도 「ClientRpc가 필요 없고, 마커는 클라이언트 로컬 `SetRallyPoint`로 표시만 갱신」이라고 적고 있다.
- **호스트(Blue):** `RequestSetRallyPoint`(211~214행)가 `SetRallyPointServerRpc`를 호출하고, 서버 측에서 `production.SetRallyPoint`(1035행)가 실행된다. 이어서 같은 프레임에 로컬 `_production.SetRallyPoint`(571행)도 실행된다. 두 호출이 쓰는 `UnitProductionUseCase`는 **같은 인스턴스**다(`GameBootstrapper.GetUnitProduction()` 401행이 `_unitProduction`을 반환하고, `GameBootstrapper.Setup.cs` 730~731행에서 같은 필드를 `_productionUI.Initialize`에 주입). 따라서 서버 RPC 본문이 호스트에서 실행되면 **`OnRallyPointChanged`가 2번 발행된다.** 두 번째 `ShowMarkerTemporary`는 같은 마커의 타이머를 다시 시작할 뿐이라, 정적으로는 마커 잔존의 직접 원인이 아니다.
  - ⚠️ **미확인:** 호스트가 부른 ServerRpc가 같은 프레임 안에서 동기 실행되는지는 NGO 내부 동작이라 프로젝트 코드로 확인할 수 없었다. 로그 확인도 하지 않았다. 「2번 발행」이 같은 프레임에서 일어나는지도 미확인이다.
- **멀티에서만 나는 이유는 찾지 못했다:** 4-3의 공유 타이머 시나리오는 정적으로 **싱글에서도 똑같이 성립한다**. 사용자가 멀티에서 관측했다는 사실과 별개로, 멀티 전용 원인은 이번 분석에서 확인되지 않았다(§7).

### 4-6. 싱글플레이 AI 랠리가 마커에 끼어드는 경로 (새로 확인 — 정적 분석, 실행 미확인)

- `OnRallyPointChanged`의 팀 필터는 멀티에서만 동작한다(341행 `if (NetworkContext.IsNetworkActive)`). 싱글에서는 AI(Red)의 `SetRallyToMine`/`ClearAllRally`가 발행한 이벤트도 그대로 처리된다.
- 결과 ①: AI 배럭의 마커도 생성되고 `ShowMarkerTemporary`로 3초간 켜진다. `GameSystemRules_Buildings.md` 랠리포인트 시스템 규칙 1(「상대 팀의 랠리포인트 깃발은 표시하지 않는다」)과 어긋날 수 있다. 다만 `GameConfig.RallyPointPrefab`이 실제로 화면에 보이는 위치에 생성되는지는 실행으로 확인하지 않았다.
- 결과 ②: AI 이벤트의 `ShowMarkerTemporary`가 **공유 타이머를 취소**한다. 그 순간 플레이어 마커가 3초 타이머 안에 있었다면, 플레이어 마커는 타이머를 잃고 켜진 채 남을 수 있다(4-3과 같은 구조).
- 이 항목은 이번 보고 범위(버그 2는 멀티 보고) 밖일 수 있으므로, 이번 작업에 포함할지 사용자 확인이 필요하다(§7).

---

## 5. 규칙 문서 검토 결과 — 구현 후 문서 반영 단계에서 고칠 대상 목록

> **이 절은 목록만 남긴다. 지금 문서를 고치지 않는다.** 사용자와 합의한 정합 항목이다. 아래 행 번호는 2026-10-13에 직접 연 값이다.

| ID | 문서·위치 | 현재 내용 | 처리 방향 |
|----|-----------|-----------|-----------|
| C1 | `Assets/_Project/Docs/GameDesignDocument.md` 「랠리 포인트」 절 741행 / `Assets/_Project/Docs/TechnicalDesignDocument.md` 「랠리포인트 BFS 빈 타일 탐색」 1819행 | 「랠리포인트 타일이 점유 중이면」 | `GameSystemRules_Units.md` 「유닛 이동 시스템」 재설계(2026-05-11)로 유닛 겹침이 허용된 뒤로는 유닛 점유가 이동을 막지 않는다. 「건물/이동 불가 칸이면」으로 문구 정리 |
| C2 | `GameSystemRules_Units.md` 「유닛 이동 시스템」(「정적 이동 불가 지형과 건물은 경로를 차단한다」, 「최종 목적지도 목적 타일 중심에 정확히 수렴한다」) vs 현재 랠리 코드 | 랠리 경로가 건물 칸을 최종 목적지로 덧붙임(§3-2) | **코드 교정 대상**(문서는 유지) |
| C3 | `GameDesignDocument.md` 739행 「타일 지정 모드 → 팝업 닫힘」 vs `GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 5(133행 「조준 모드 진입 등을 위해 팝업만 숨기는 경로」) | 문서끼리 「닫힘」과 「숨김」이 다름. 실제 코드는 숨김(§4-2) | 현재 구현(숨김, 창 닫힌 상태로 남음 + 3초 마커, S1)에 맞춰 GDD 문구 수정 |
| C4 | GDD 747행 「팝업 닫힘 / 다른 오브젝트 클릭 시 마커 숨김」, TDD 1812행 「팝업 닫힘/다른 오브젝트 클릭: 마커 숨김」 vs `GameSystemRules_Buildings.md` 랠리포인트 시스템 규칙 2 표(31~35행) | 규칙 표에 「다른 오브젝트 클릭」 행이 없음 | 랠리포인트 시스템 규칙 2 표에 행 추가(S2) |
| G1 | `GameSystemRules_Buildings.md` 랠리포인트 시스템 규칙 2 | 「배럭 팝업 열림 → 해당 배럭 마커 표시」만 있고 다른 마커 처리는 없음 | 「그 건물 마커만 표시, 다른 배럭 마커 숨김」(S3) |
| G2 | 같은 절 | 「배럭 자신의 타일에 랠리포인트 설정 → 해제」는 있으나 대체 이동과의 우선순위는 없음 | 자기 칸 해제가 대체 이동보다 우선(S4) |
| G3 | 같은 절(규칙 2 또는 신규 규칙) | 이동 불가 랠리 칸 처리 규칙 없음 | 아군/적 건물·이동 불가 지형 동일 처리, 최대 3칸 대체 이동(S5) |
| G4 | 같은 절 | 대체 칸이 없을 때의 규칙 없음 | 대체 칸 없으면 바로 적 성 진군(S5) |
| G5 | 같은 절 | 설정 후 그 칸에 건설될 때의 규칙 없음 | 랠리 유지 + 생산 시마다 대체 이동, 마커 유지(S6) |

- 신규 규칙을 추가할 때는 `**규칙 N. 제목**` 굵은 글씨 형식을 써야 검사기(`Tools/check_docs.py`)가 규칙으로 인식한다(`Assets/_Project/Docs/WORKFLOW.md` [11]).
- GDD 「랠리 포인트」 절(739~747행)과 TDD 「랠리포인트 마커 표시 규칙」(1809~1816행)·「랠리포인트 BFS 빈 타일 탐색」(1818~1828행), 그리고 TDD 진군 흐름(1778~1779행 「BFS 빈 타일 탐색 → 랠리포인트 근처로 이동」)도 구현이 확정되면 함께 맞춘다. TDD 1828행에는 「범위 내 빈 타일 없으면 이동 안 함」이 적혀 있다. 이는 현재 코드(§3-4)와는 일치하지만 S5와는 다르다.

### 5-1. 범위 밖으로 명시하는 항목

- `GameSystemRules_Units.md` 「유닛 이동 시스템」 규칙 45(건물로 경로가 완전히 막혔을 때의 유닛 동작) — 그 규칙 스스로 「확정된 후속 구현 범위 / 미구현」으로 표시되어 있다. 이번 작업에서 다루지 않는다.
- `ProductionPanelUI.CompleteRallyPointSetting`의 `HideBlockingOverlay()` 호출에 대해 그 주석(574~589행)이 적은 별도 결함(「점유한 적 없는 몫을 놓는 호출」) — 주석 스스로 「아직 고치지 않았다(별도 작업)」라고 적고 있다. 이번 작업에서 다루지 않는다.

---

## 6. 영향 범위 — 수정 후보 파일 (확정 아님)

| 후보 파일 | 이유 | 관련 사양 |
|-----------|------|-----------|
| `Presentation/Production/ProductionTicker.cs` | `OnUnitProduced`의 대체 이동 조건, `FindPathToNearestEmptyTile`의 Ring 0 이동 가능 판정, 대체 칸이 없을 때 성 진군. 마커의 공유 타이머(`_autoHideCoroutine`)와 `ShowRallyMarker`의 다른 마커 처리 | S3·S5·S6, 버그 1·2 |
| `Presentation/UI/ProductionPanelUI.cs` | 지정 완료 후 마커 상태, 다른 오브젝트 선택 시 숨김 연동 지점 | S1·S2·S3 |
| `Presentation/Input/InputHandler.cs` | 지정 완료 후 다른 오브젝트 선택을 감지할 위치(현재 숨김 경로 없음) | S2 |
| `Application/UseCases/UnitMovementUseCase.cs` | 이동 가능 판정(`IsWalkable`)을 재사용할 수 있음. `RequestMove` 자체의 「이동 불가 목적지 덧붙임」 계약은 전투 접근(`RequestCombatApproachPath`가 `RequestMove`를 그대로 씀, 102~105행)도 쓰므로 **바꾸면 전투 이동에 영향**. 변경 대상인지는 Plan에서 판단 | S5 |
| `Application/UseCases/UnitProductionUseCase.cs` | 자기 칸 해제는 이미 구현(§3-5). 변경 필요 여부는 Plan에서 판단 | S4 |
| `Application/Services/AIOpponentController.cs` | 직접 수정 대상은 아니나 금광 랠리 동작이 S5의 영향을 받음(§3-7) | — |
| 문서: `GameDesignDocument.md`, `TechnicalDesignDocument.md`, `GameSystemRules/GameSystemRules_Buildings.md` | §5의 C1·C3·C4·G1~G5 | — |

- 멀티 관련: 이동 결정은 서버(호스트)에서만 일어나므로(§3-6) `NetworkProductionController.cs`는 이동 수정에서는 후보가 아닐 것으로 본다(정적 분석). 마커 이벤트 2번 발행(§4-5)을 다룰지에 따라 후보가 될 수 있다.
- 레이어 제약(`.claude/MEMORY.md` 「아키텍처 핵심 제약」): `ProductionTicker`·`ProductionPanelUI`·`InputHandler`는 Presentation이다. 그대로 두면 Netcode 직접 참조 없이 `NetworkContext`를 쓰는 현재 방식을 유지할 수 있다.

---

## 7. 남은 확인 사항 / 미확인 목록

### 7-1. 사용자 확인이 필요한 항목

> **2026-10-13 확정 — 아래 네 항목은 모두 사용자 답으로 해소됐다.** 질문 원문은 기록으로 남긴다. 답은 §1-1에 있다: 1 → S7, 2 → S8, 3 → S12, 4 → S9·S10·S11.

1. **(2026-10-13 확정 → S7: 포함한다)** **S5의 「이동 불가 지형」에 금광 칸이 포함되는가?** 코드상 금광 칸은 이동 불가다(`HexTile.IsWalkable`). 포함하면 싱글 AI의 금광 랠리(§3-7)도 「근처 칸 → 성 진군」으로 바뀐다. AI 동작 변화를 받아들이는지 확인이 필요하다.
2. **(2026-10-13 확정 → S8: 랠리 칸 기준 링 거리 우선, 같은 거리면 출발점 경로 길이)** **「가장 가까운」의 기준**: 랠리 칸에서의 칸 거리인지, 유닛 출발점에서의 경로 길이인지. 같은 거리 후보가 여럿일 때 무엇을 고르는지. (현재 BFS는 랠리 칸 기준 링 순서 + 이웃 방향 순서다.)
3. **(2026-10-13 확정 → S12: 이번 작업에 포함)** **싱글 AI 랠리의 마커 표시·타이머 간섭(§4-6)**을 이번 작업에 포함할지.
4. **(2026-10-13 확정 → S9·S10·S11: 격자 밖·막힌 타일만 지정 불가, 건물·금광 칸은 지정 허용)** **설정 시점 검증**: 건물 칸·이동 불가 칸·격자 밖 좌표를 랠리로 저장하는 현재 동작(§3-5)을 그대로 둘지. S6이 「설정 후 건설돼도 유지」이므로 저장은 허용하는 방향으로 읽힌다. 다만 격자 밖 좌표는 사양에 언급이 없다.

### 7-2. 미확인 항목 (파일 · 행 · 주장 · 미확인 사유)

| 파일 · 행 | 주장 | 미확인 사유 |
|-----------|------|-------------|
| `_Tasks/2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/Research.md` 「2026-10-02 이동 최종 분석」 | 건물 랠리 종점 TerminalContact가 Fatal의 근본 원인 | 원문 스스로 「유력 근본 원인, 직전 성공 프레임 미기록·재현 미수행」. 이번에도 재현하지 않음 |
| `ProductionTicker.cs` 753~787행 `FindPathToNearestEmptyTile` | Ring 0에서 건물 칸 종점 경로를 그대로 반환함 | 코드 읽기만 함. 실행·단위 테스트 없음 |
| `UnitView.cs` 2792~2804행 | 막힌 지형·금광 칸 종점도 TerminalContact 분기를 탐 | 판정식(`IsWalkable`)에서 추론. 해당 조건의 실행 로그 없음 |
| `AIOpponentController.cs` 662~666행 | 싱글 AI 유닛도 금광 칸 종점에서 같은 실패를 겪음 | AI 경기 로그를 확인하지 않음 |
| `NetworkProductionController.cs` 211~214·992행~ / `ProductionPanelUI.cs` 569~571행 | 호스트에서 `OnRallyPointChanged`가 같은 프레임에 2번 발행됨 | 호스트 ServerRpc의 동기 실행 여부는 NGO 내부 동작이라 프로젝트 코드로 확인 불가. 로그 미확인 |
| `ProductionTicker.cs` 334~360행 (싱글) | AI 마커가 플레이어 화면에 3초간 보이고 공유 타이머를 취소함 | 싱글 실행 미확인. 프리팹 표시 위치 미확인 |
| 버그 2 전체 | 멀티에서만 발생하는 원인 | 정적으로는 싱글에서도 같은 시나리오가 성립. 멀티 전용 원인을 찾지 못함. 재현 미수행 |
| `InputHandler.cs` 207행~ | 유닛을 누르면 그 아래 타일 클릭으로 처리됨 | 유닛 전용 선택 분기·식별자가 없다는 검색 결과에서 추론. 실행 미확인 |
| `ProductionTicker.cs` 366~374행·218~222행 | 랠리 칸 위에 건설돼도 랠리가 유지됨 | 건물 배치 시 랠리를 지우는 코드가 없다는 검색 결과. 실행 미확인 |
| — | 랠리 칸으로 걷는 중에 그 칸에 건물이 지어질 때의 재경로 결과 | 이번에 추적하지 않음(`GameBootstrapper.SetupEagerRepathOnBuildingChanges`·`UnitView.OnPathInvalidated` 경로) |
| `InputHandler.cs` 219~230행 | 랠리 지정 중에는 UI 위를 눌러도 그 아래 맵 좌표로 지정됨(랠리 분기가 `IsPointerOverUI` 검사보다 앞) | 코드 순서에서 추론. 실행 미확인. 이번 범위 밖의 부가 관찰 |

---

## 8. 참고 문서

- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` 랠리포인트 시스템 규칙 1·2
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` 공통 UI 규칙 규칙 5, 생산 패널 UI 규칙 1·2
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` 「유닛 이동 시스템」(재설계 2026-05-11, 규칙 3, 규칙 45)
- `Assets/_Project/Docs/GameDesignDocument.md` 「랠리 포인트」 절
- `Assets/_Project/Docs/TechnicalDesignDocument.md` 「랠리포인트 마커 표시 규칙」·「랠리포인트 BFS 빈 타일 탐색」
- `Assets/_Project/Docs/_Tasks/2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/Research.md` 「2026-10-02 이동 최종 분석」
- `Assets/_Project/Docs/_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/` (랠리 조준 진입 시 오버레이 결함 이력)

---

## 9. 2026-10-18 사용자 실기 보고와 후속 조사 범위

### 9-1. 실기 결과 (사용자 보고값)

사용자는 대화에 제시한 싱글 테스트 1~6을 진행했고 멀티도 확인했으며, **5번만 눈으로 확인하지 못했고 나머지는 정상적으로 잘 작동하는 것으로 보인다**고 보고했다. 아래 번호는 그 대화의 여섯 항목이며, Plan §9의 S/M 번호와 다르다.

| 대화 번호 | 확인 대상 | 사용자 보고 |
|---|---|---|
| 1 | 아군 건물·적 건물·금광 위 랠리 → 옆 빈 칸 → 적 성 진군 | 정상적으로 작동하는 것으로 보임 |
| 2 | 막힌 타일·맵 밖 탭 시 토스트 → 이어서 정상 칸 지정 | 정상적으로 작동하는 것으로 보임 |
| 3 | A 배럭 랠리 후 3초 안에 B 배럭 열기 → A 숨김·B만 표시 | 정상적으로 작동하는 것으로 보임 |
| 4 | 랠리 직후 맵의 다른 곳 탭 → 깃발 즉시 숨김 | 정상적으로 작동하는 것으로 보임 |
| 5 | AI 금광 공략 시 AI 깃발 비표시·AI 유닛 정지 없음 | 눈으로 확인하지 못함 — 미확인 유지 |
| 6 | 랠리 주변 3칸에 대체 이동 칸 없음 → 적 성 직행 | 정상적으로 작동하는 것으로 보임 |

멀티 확인도 사용자 보고로 기록한다. **Host·Client 각각의 항목별 실행 여부, 장치 구성, 서버 거부 로그, 정밀 이동 계약·로그 검증은 보고되지 않았다.** 따라서 Plan §9의 M-1~M-6 전체 PASS 또는 서버 재검증 실측 PASS로 확장하지 않는다. 이번 기록 과정에서는 에이전트가 Unity 실행·재현·로그 수집을 하지 않았다.

### 9-2. 이동 도중 건설 문제 — 현재 후속 작업 대상

- **사용자가 관찰한 현상:** 유닛이 랠리 칸으로 걸어가는 도중 그 칸에 건물을 지으면, 걷던 유닛이 멈춘다.
- **구별되는 정상 관찰:** 건설이 끝난 뒤 새로 생산된 유닛은 근접 타일까지 이동한 뒤 정상적으로 이동한다.
- **요청과 범위 변경:** 사용자는 문서 기록과 이 문제의 후속 작업을 요청하고, 새 task 대신 기존 Research/Plan 갱신을 승인했다. 최초 Plan R4·§7·§11의 「관찰만 / 범위 밖」은 당시 경계로 보존하되, 지금은 후속 원인 조사·수정 계획 대상으로 다룬다.
- **확정하지 않은 것:** 사용자 관찰은 멈춤 증상에 대한 근거다. §7-2의 `OnPathInvalidated` 재경로 → 이동 불가 종점 → TerminalContact라는 설명은 여전히 정적 분석 가설이며, 그 인과를 실기로 입증한 것은 아니다. 발생 역할·유닛 종류·정확한 정지 위치·로그는 보고되지 않았다.
- **후속 조사 목표:** 이동 중 목적지의 건물 점유 변화가 경로 무효화·목적지 재선택·이동 완료 콜백·적 성 진군 전환에 어떻게 전달되는지 현재 코드로 확인한다. 생산 직후 대체 이동과 전투 접근 계약을 구분해 영향 범위를 정하고, 확인된 근거를 바탕으로 Plan을 구체화한다. 이 문서 갱신 시점에는 후속 수정이 적용되지 않았다.

### 9-3. 최초 구현 기록의 근거와 한계

`git show 9aa921f9`에서 생산 시 `ResolveRallyPath`, 배럭별 깃발 타이머, 싱글 팀 필터가 추가된 변경을 확인했다. 커밋 설명에는 지정 불가 위치 토스트·조준 유지·서버 재검증과 기존 로직 주석 비활성화도 기록돼 있다. 이것은 최초 수정이 반영된 근거이며, 후속 이동 중 건설 문제까지 수정됐거나 AI 항목이 실기 확인됐다는 근거는 아니다.

### 9-4. main 병합 후 조사 인계 (메인 세션 전달값)

- 메인 세션은 main 병합 완료 커밋 `5f54cfa1`(부모 `7bb3486b`, `42111f54`)을 전달했다. 로그 메타 파일의 rename/delete 충돌은 실제 남아 있는 로그의 메타를 보존하는 방향으로 해결했다고 보고했다. 이 문서 담당은 git 변경을 수행하지 않았다.
- programmer 조사 인계에 따르면 현행 `OnPathInvalidated`는 `RequestLatestAuthoritativeRepath`를 사용한다. 따라서 최초 R4·§7-2의 직접 `RequestMove` 재호출 설명은 **2026-10-13 조사 시점의 설명**이며, 병합 후 코드의 현행 설명으로 사용하지 않는다. 이것은 원인 확정이 아닌 후속 정적 조사 인계다.
- programmer가 전달한 현행 코드 위치는 `ProductionTicker.cs`의 생산 시 `ResolveRallyPath`(355·954행), `UnitView.cs`의 경로 끝 목적지 저장(3464행) 및 같은 목적지 권위 재경로(3654·3722행), `Bootstrap/GameBootstrapper.Network.cs`의 생존 유닛 `OnPathInvalidated` 통보(107~155행), `UnitMovementUseCase.cs`의 권위 재경로(130행, `HexFlowField.GetPath`의 이동 불가 종점 덧붙임), `UnitMovementContracts.cs`의 읽기 전용 objective 목적지(1984행)다. 행 번호는 병합 직후 programmer 조사 전달값이며 문서 담당의 코드 재검증 값이 아니다.
- 실기 멈춤이 TerminalContact·guard 중 어느 경계에서 발생하는지는 미확정이다. 자동 재현 루프는 확보되지 않았으며, 이 후속 조사에서 QA·Unity 실행은 없었다는 programmer 인계를 유지한다.
- 후속 후보는 기존 랠리 경로 선택을 이동 질의로 공용화하고, 활성 랠리의 재경로 때 대체 목적지를 다시 고르되 권위 루트·revision·목적지 전환 계약을 유지하는 것이다. 구체 후보와 승인 경계는 Plan §12-3에 둔다.

### 9-5. 승인 후속 구현·별도 컴파일 결과 (2026-10-18, programmer/메인 인계)

사용자 승인 후 game-programmer는 `UnitMovementContracts.cs`, `UnitMovementUseCase.cs`, `ProductionTicker.cs`, `UnitView.cs`에 후속 수정을 반영했다고 보고했다. 생산 시와 이동 도중 재경로에 랠리 목적지 재선택을 공용으로 적용하고, 현재 칸 선택·대체 후보 없음·질의 실패를 구별하는 방향이다. 구체 구현 정리는 Plan §12-4를 따른다. 이 문서 담당은 코드를 수정하지 않았다.

| 검사 | 전달된 결과 | 의미·한계 |
|---|---|---|
| Runtime Roslyn 별도 컴파일 | exit 0, errors 0, warnings 23. 기준선도 warnings 23 | 별도 컴파일 통과이며 실제 Unity 에디터 컴파일·실기 통과는 아님 |
| Editor Roslyn 별도 컴파일 | exit 0, errors 0, warnings 0 | 같은 별도 컴파일 범위 |
| 임시 CLR에서 production 호출 | exit 0 | 직접 랠리, 이동 도중 건설 후 링 1의 이동 가능 칸 선택, `CurrentTileSelected`와 `NoReachableCandidate` 구별, invalid Root·provider failure를 정상 완료로 오인하지 않음, endpoint 변경 전후 guard 동일성을 확인했다고 보고 |

최종 인계에서 programmer는 이후 코드 수정 없이 Runtime/Editor 재컴파일 exit 0(errors 0, warnings 23/0, Runtime 기준선 23 동일), 최종 DLL의 CLR 실제 7항목 exit 0, diff 공백 검사 exit 0을 보고했다. 임시 근거는 `.utmp/rally_compile/`의 `baseline.log`, `rally.log`, `rally-editor.log`, `clr.log`, `source-hashes.json`이며 ignored 임시 산출물이다. 이 경로를 영구 보존 근거 링크로 간주하지 않는다. 수치·실행 결과는 programmer/메인 전달값이며 문서 담당이 재실행한 결과가 아니다.

**에이전트 실행 경계:** 에이전트는 실제 Unity 코루틴·NetworkTransform·멀티 실기를 수행하지 않았다. 최초 1·2·3·4·6 정상 관찰과 후속 수정의 사용자 수용은 구분하며, 후속 싱글·멀티 보고는 §9-6, AI 5번의 이후 사용자 싱글 수용은 §9-7을 따른다. 상세 원인이 TerminalContact·guard 중 어느 경계였는지를 실기로 확정한 것은 아니다. 메인은 최종 코드 4파일의 SHA 확인·CLR 7줄 독립 열람·diff 검토를 완료했고, `python -X utf8 Tools/check_docs.py` exit 0(문제 없음), `git diff --check` exit 0을 전달했다.

### 9-6. 후속 수정의 사용자 싱글·멀티 확인과 수용 (2026-10-18)

사용자는 먼저 「싱글에서 확인했고 모두 정상이였어」라고 보고했고, 이어 「멀티도 정상작동하는거 확인했어 문서 갱신하도록하자」라고 요청했다. 이 보고로 **이동 도중 랠리 칸에 건물을 지었을 때 걷던 유닛이 멈추는 후속 문제는 싱글·멀티 사용자 확인 범위에서 수용·종결**한다. 앞선 구현·별도 컴파일 단계의 실기 대기 상태를 대체한다.

**당시 수용 한계:** 이 보고 시점에는 AI 5번을 별도로 확인했다는 보고가 없어 미확인이었다(이후 사용자 싱글 수용은 §9-7). Host·Client 각각의 세부 실행, 장치·유닛별 목록, 정밀 이동·NetworkTransform 계약이나 로그 측정까지 확인됐다고 쓰지 않는다. 전체 경로 차단 대응인 유닛 이동 규칙 45와 전역 이동 교정도 이번 종결에 포함하지 않는다.

사용자 승인에 따라 기존 사양 불일치를 현행 코드와 대조해 건물 랠리 규칙·BuildingPanelsUI 토스트 표·AI 금광 지정 문구·TDD 랠리 전용 경로 설명을 동기화했다. GDD는 이미 상세 조건을 원본에 위임한 요약과 링크로 정리돼 있어 추가 중복 사양을 넣지 않았다. 주석 비활성화된 코드의 최종 정리 여부는 문서 수용과 별도이며, 이 문서 작업에서는 코드 변경·삭제를 하지 않았다.

### 9-7. AI 5번 사용자 싱글 수용과 코드 정리·main 병합 준비

사용자가 AI 5번도 싱글에서 정상 동작한다고 보고한 뒤 「반영하고 필요없는 코드 삭제하고 메인에 병합 준비하도록하자」라고 승인했다. 이로써 AI 깃발 비표시·금광 랠리 유닛 이동에 관한 5번은 사용자 싱글 확인 범위에서 수용한다. 이전 표·단계의 미확인은 당시 기록이며 현재 대기 항목이 아니다. AI 세부 시나리오 전체나 별도 로그·역할·장치별 측정으로 확대하지 않는다.

game-programmer가 기존 Plan의 D1·D2·D3 및 옛 랠리 경로 선택 코드의 불필요 부분 정리와 관련 메모리 갱신을 담당했다. 당시 정리 진행 단계에서 받은 후속 완료 근거는 §9-9에 기록한다. 공성 공용 폴백·현행 랠리 질의·일반 전투 접근 계약은 삭제 대상이 아니다. main 병합 준비는 메인의 비교·검토·검사 단계이며 커밋·푸시·병합은 실행하지 않았다.

### 9-8. main 병합 준비 검토 (메인 전달값)

메인이 전달한 현재 HEAD는 `5f54cfa1`, 로컬 main과 저장된 origin/main은 `42111f54`다. `HEAD..main`·`HEAD..origin/main`의 로그 출력이 없어 **현재 저장소가 알고 있는 main은 포함**한다. 이번 준비에서 원격 fetch는 하지 않았으므로 원격의 최신 상태까지 확인했다는 뜻은 아니다. 미해결 충돌과 staged 변경은 없다고 보고했다.

main 대비 HEAD의 커밋은 `9aa921f9`·`7bb3486b`·`5f54cfa1`로 전달됐다. 기존 `7bb3486b`에는 `Maplestory Light SDF.asset`의 Unicode 47008(랠)·47532(리) glyph 및 atlas 변경, 로그·Task meta가 포함돼 있어 전체 브랜치 병합 시 함께 들어간다. 미커밋 `AndroidResolverDependencies.xml`과 사용자 로그·meta는 별도 외부 변경으로 보존하며 준비 작업에 자동 포함하지 않는다. 실제 커밋·푸시·main 병합은 아직 실행하지 않았다. 문서 담당은 이 비교를 재실행하거나 git을 변경하지 않았다.

### 9-9. 불필요 코드 정리 완료·별도 검증 (programmer/메인 전달값)

`ProductionTicker.cs`에서 D1 주석 5곳·D2 1곳·D3 1곳과 10월 18일 비활성화한 옛 랠리 루프, 미참조 private 상수·버퍼·`CollectRing`을 삭제했다는 최종 인계를 받았다. 파일은 1052→884행(-168행)이며 공성 BFS는 유지했다. 제거 선언 3개를 제외한 나머지 활성 Roslyn 토큰이 전후 동일하고 문법 오류는 0이라는 보고로, 주석·미참조 선언 정리와 활성 동작 변경을 구별했다.

Runtime/Editor 별도 컴파일 exit 0·errors 0·warnings 23/0(이전 동일), 기존 CLR 실제 7항목의 최종 DLL 재실행 exit 0으로 전달됐다. 최종 Ticker SHA256은 `6E9557AAA18B6870DCCDD05B33530A5CC43B98DC8ABF901BF950F7CD91C86BF5`이며 나머지 코드 3파일은 이전과 동일하다고 보고했다. 임시 근거는 `.utmp/rally_compile/cleanup-{runtime,editor,clr,syntax}.log`, `cleanup-source-hashes.json`이다. ignored 임시 산출물이므로 영구 보존 근거 링크가 아니며 문서 담당이 재실행·재측정한 값도 아니다.

programmer가 관련 메모리 4파일을 AI 싱글 수용에 맞춰 갱신했고 각 행수는 동일하다고 전달했다. 이번 정리 후 신규 사용자 실기는 없으며, 앞선 사용자 수용과 활성 동작 코드 불변 검증을 구별한다. 현재는 정리·별도 검증 완료 후 main 병합 전 준비 단계다. §9-8의 저장된 refs·원격 fetch 미실행·외부 변경 보존 경계는 그대로 유지한다.

메인은 cleanup 로그를 독립 열람해 활성 토큰 동일·문법 오류 0·CLR 7항목 PASS를 확인하고, cleanup manifest의 코드 4파일 해시가 실제 파일과 모두 일치한다고 전달했다. 이는 정리 근거 검토이며 신규 실기나 실제 병합 완료가 아니다.
