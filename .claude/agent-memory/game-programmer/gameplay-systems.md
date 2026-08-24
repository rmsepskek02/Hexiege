---
name: gameplay-systems
description: 랠리포인트, 생산 시스템, 건물 배치 등 게임플레이 시스템 세부사항
type: project
---

# 게임플레이 시스템

## 랠리포인트 마커 (2026-03-07)
- GameConfig.RallyMarkerOffset (Vector3, default: 0.05/0.15/0) — **에셋 실제값 0.1/0.28/0**
- GameConfig.RallyMarkerEuler (Vector3, default: 0/0/0) — **에셋 실제값 -90/180/0**
- ProductionTicker.CreateOrMoveMarker(): _config 참조

## 랠리포인트 시스템 구조 맵 (2026-08-08 코드 조사)
데이터/이벤트:
- 상태: `Domain/Building/ProductionState.cs:150` `RallyPoint (HexCoord?)`, `BarracksPosition`(get-only, RegisterBarracks 시 확정)
- 이벤트: `Application/Events/GameEvents.cs` `RallyPointChangedEvent(BarracksId, Coord?, Team)` / `UnitProducedEvent(Unit, RallyPoint?, BarracksId)`
- 설정/해제: `UnitProductionUseCase.SetRallyPoint/ClearRallyPoint` (465~488). target==BarracksPosition이면 해제(기획 규칙). state 없으면 **조용히 return**(이벤트 미발행)
- 좌표 유효성 검증 없음 — 맵 밖/비walkable 좌표도 그대로 저장 (서버 RPC에도 검증 없음)

UI/마커(전부 Presentation 로컬):
- 마커 관리 전부 `Presentation/Production/ProductionTicker.cs` (`_rallyMarkers` Dict + **단일** `_autoHideCoroutine`)
- 3초 표시: `ShowMarkerTemporary`/`AutoHideMarker`(WaitForSeconds=timeScale 영향), 팝업 열림 `ShowRallyMarker`, 닫힘 `HideAllRallyMarkers`
- 팝업 연동: `ProductionPanelUI.OnShow → ShowRallyMarker`, `OnBeforeClose → HideAllRallyMarkers`, 조준 진입 `OnRallyPointClick`은 `_popup.Hide()` + **`HideBlockingOverlay()`**(2026-08-08 수정), 완료 `CompleteRallyPointSetting`은 `Close()` 미호출이라 **`HideBlockingOverlay()`로 참조 카운터를 직접 반납**(2026-08-08 추가) 후 `_currentBuilding=null`
- 입력: `InputHandler.HandleClick` 최상단(IsPointerOverUI보다 앞)에서 rally 분기 + `RallyPointSetFrame` 같은프레임 가드
- 파괴/철거 정리: `ProductionTicker.OnBuildingDied → UnregisterBarracks + DestroyMarker`; 철거는 `CancelAllQueue`가 ClearRallyPoint를 먼저 호출
- 업그레이드 승계: `ProductionTicker.OnBuildingUpgraded`가 RallyPoint 저장 → CancelAllQueue → RegisterBarracks → SetRallyPoint 복원 (**서버 가드 없음 → 클라에서도 CancelAllQueue 실행**)

멀티:
- 서버 권위 저장: `NetworkProductionController.SetRallyPointServerRpc`(748~). 결과 ClientRpc 없음 → 클라는 `_production.SetRallyPoint` 로컬 낙관 적용(마커 표시 목적)
- 생산 유닛의 랠리는 `SpawnUnitClientRpc(hasRally/rallyQ/rallyR)`로 서버 값 전달
- 팀 필터: `ProductionTicker.OnRallyPointChanged` 진입부에서 `IsNetworkActive`일 때만 `IsNetworkServer?Blue:Red` 비교 → **싱글플레이(=AI Red)에서는 필터 자체가 비활성**

구조적 취약점(코드 근거 확인, 실기 미검증):
1. `_autoHideCoroutine`이 배럭 단위가 아닌 **단일 필드** → 3초 내 두 번째 ShowMarkerTemporary/ShowRallyMarker가 앞 코루틴을 StopCoroutine → 앞 마커가 영구 표시 잔존. AI가 `SetRallyToMine`으로 모든 배럭을 한 프레임에 설정하므로 N-1개 잔존
2. 싱글플레이 팀 필터 부재 → AI(Red) 깃발이 플레이어 화면에 표시(기획 규칙 1 위반)
3. ~~랠리 조준 중 UIManager 공유 BlockingOverlay(Popup 모드, onTap=Close)를 숨기지 않음~~ → **✅ 2026-08-08 수정 완료·실기 PASS(커밋 `9a19cd5`)**. 스킬 패널(`BuildingSkillPanelUI.cs:328`)과 동일 패턴으로 `OnRallyPointClick`에 `HideBlockingOverlay()` 추가 + `Close()`를 안 거치는 `CompleteRallyPointSetting`에 참조 카운터 반납 1줄 추가. task `_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/`
4. `LoadMap()` 재호출(재경기)마다 `ProductionTicker.Initialize→SubscribeEvents`가 **중복 구독**(`AddTo(this)`는 컴포넌트 파괴 시에만 해제). `_rallyMarkers`도 초기화되지 않고 `BuildingPlacementUseCase.Clear()`는 OnBuildingDied를 발행하지 않아 마커 GameObject 누수

## 공격 방향 — 상세: [attack-direction-refactor.md](attack-direction-refactor.md)
- TryAttack 반환: `(int id, bool isUnit)?` 튜플
- UnitView.CalculateAttackAngle: 실제 transform.position → Atan2 → _meshYOffset 보정
- BuildingFactory.GetBuildingObject(int buildingId) 추가

## MistShrine 물안개 힐 — 에디터 셋업 진입점 (2026-08-21 복구)

> 2026-08-17 `675203ae` 로 `MEMORY.md` 「주요 파일 위치」 표에서 소실.
> **셋업 스크립트 경로와 메뉴 순서는 다른 문서에 남아 있지 않은 유일본**이라 여기로 복구한다.
> (`MistShrineUseCase` / `MistShrinePanelUI` / `MistShrineRangeIndicator` 의 런타임 코드 경로는
>  `TechnicalDesignDocument.md` · `WORK_HISTORY.md` 에 있어 중복 복구하지 않는다.)

- 에디터 셋업: `Assets/Editor/Setup/MistShrineSetup_Config.cs` · `MistShrineSetup_Scene.cs`
- **메뉴는 순서대로 실행해야 한다**:
  `Hexiege/MistShrine/1. Apply Config Values` → `2. Setup Scene (Panel, Range, Network)`
- 알려진 기술부채: `ProductionPanelUI` 의 `_unitAutoIndicators`(GameObject) ↔ `_unitBorderOverlays`(Image) 가
  **같은 BorderOverlay 를 이중 배선**한다. 정리는 별도 작업이며, **다른 패널로 복제 금지**(UI 규칙 14).

## 팀별 초상화 동적 업데이트 (2026-03-14)
- ProductionPanelUI: UpdateButtonPortraits(TeamId) — Show(barracks) 시 교체
- BuildingPlacementUI: UpdateButtonPortraits(TeamId) — Show(coord, team) 시 교체
