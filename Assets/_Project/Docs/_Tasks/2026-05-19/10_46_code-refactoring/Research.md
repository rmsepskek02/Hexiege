# Research: 코드 리팩토링

## 이 문서가 다루는 것

이 문서는 Hexiege 프로젝트 전체 스크립트(약 100여 개 C# 파일)를 한 번에 훑어보고, "지금 코드가 어떤 상태인가"를 사진처럼 기록한 보고서입니다.

쉽게 말해 다음 세 가지를 찾으려고 했습니다.

1. **버려진 코드** — 만들어 두기만 하고 지금은 아무 데서도 부르지 않는 함수/필드/클래스. 예전 시스템(슬롯/점유)을 폐기하면서 "혹시 모르니 남겨둔" 흔적들.
2. **겹치는 코드** — 같은 일을 여러 군데에서 따로따로 하고 있는 것. 예를 들어 같은 객체를 매번 새로 찾는 코드가 곳곳에 흩어져 있는 경우.
3. **고쳐야 할 코드** — 클린 아키텍처 약속(Domain은 Core를 모른다, Application은 Unity 네트워크 코드(NGO)를 모른다 등)을 어긴 곳, 매 프레임 무거운 일을 하는 곳, null 처리 누락으로 게임이 죽을 수 있는 곳.

이 작업의 목적은 "당장 바로 잡아라"가 아니라, **무엇이 얼마나 쌓여 있는지 한눈에 보고**, 다음 작업에서 어떤 것을 우선으로 정리할지 결정하기 위한 자료를 마련하는 것입니다. 코드 수정은 일절 하지 않았습니다.

심각도 기준:
- **높음**: 아키텍처 약속을 정면으로 어기거나 잠재적 크래시 위험이 있는 항목 (코드 보수 시 가장 먼저 정리해야 함)
- **중간**: 동작에는 문제 없지만 가독성/유지보수에 부담을 주는 항목
- **낮음**: 향후 기능 확장을 위해 의도적으로 남겨둔 자리표시(placeholder) 또는 주석 정리 수준의 항목

---

## 1. 미사용 코드 목록

| 파일 | 위치 | 내용 | 심각도 |
|------|------|------|--------|
| Presentation/UI/ProductionPopupDiagnostic.cs | 클래스 전체 | "1회성 진단 도구" 명시. ProductionPopup 레이아웃 분석용 임시 MonoBehaviour. 다른 어떤 파일도 참조하지 않음 (Grep 결과: 본인 외 0건). 작업 완료 후 제거 예정이었으나 남아있음. | 중간 |
| Application/Services/AttackPositionManager.cs | 클래스 전체 (약 250줄 추정) | GameBootstrapper.GetAttackPositionManager()가 "[2026-05-11 비활성화] 항상 null 반환"으로 영구 무력화됨. 새 호출자 없음. ClaimByApproach / ReleaseAttackSlot 모두 미호출. | 높음 |
| Application/Services/TileOccupancyManager.cs | 클래스 전체 | GameBootstrapper.GetTileOccupancyManager()가 "[2026-05-11 비활성화] 항상 null 반환". UnitMovementUseCase 생성자에서 `null`을 명시적으로 전달. 인스턴스가 생성되지 않음. | 높음 |
| Application/UseCases/UnitMovementUseCase.cs | `_occupancyManager` 필드 (45), `_subscriptions` (49), 생성자 인자 (53,58) | 항상 null이 주입되며 메서드 본문은 모두 주석 처리. _subscriptions에 추가되는 항목 없음. | 높음 |
| Application/UseCases/UnitMovementUseCase.cs | `_unitSpawn` 필드 (37,56) | 코드 주석에 "현 시점에서는 사용처 없음, 향후 확장 대비 유지" 명시. 실제 메서드 본문에서 한 번도 참조되지 않음. | 중간 |
| Application/UseCases/UnitMovementUseCase.cs | `FindForwardAvailable(...)` 메서드 (210~216) | 본문이 "항상 preferred 반환" 한 줄. 호출자도 0건 (Grep 결과). 시그니처만 유지. | 높음 |
| Domain/Unit/UnitStats.cs | `AttackKind` enum + `GetAttackKind()` + `StatValues.Kind` | 주석에 "[2026-05-11 비활성화 — 이동 로직 분기 미사용]" 명시. UnitView/UnitMovement/UnitCombat의 로직 분기에서 사용하지 않음. UnitStatsConfig Inspector 호환과 향후 UI 분류용으로 보존 중이지만 현재 호출자 없음. | 낮음 (의도적 보존) |
| Application/UseCases/UnitCombatUseCase.cs | `MeleeDetectDist` 상수 (45 주석) | 주석 처리 상태. 새 통합 detect 사거리 도입으로 폐기되었으나 시그니처 호환 명목으로 주석 보존. | 낮음 |
| Bootstrap/GameBootstrapper.cs | `_slotForwardRatio` / `_slotSideRatio` 필드 (149,152), `_moveSlotManager` (260), `_attackPositionManager` (263), `_occupancyManager` (266) | 모두 `[2026-05-11 비활성화 — 슬롯 시스템 폐기]` 주석으로 처리됨. 인스펙터 노출까지 제거된 상태. | 낮음 (정리 완료) |
| Bootstrap/GameBootstrapper.cs | `GetAttackPositionManager()` (306), `GetTileOccupancyManager()` (311) | "항상 null 반환" 메서드. 외부 호출자 NullReference 회피 목적으로 시그니처만 유지. 호출자가 있는지 별도 검증 필요. | 중간 |
| Domain/Hex/HexFlowField.cs | `destTileCheck` 지역 변수 (223,233) | "_ = destTileCheck;" 의미 명확화용 자리표시. 실제 사용 없음. | 낮음 |
| Domain/Building/BuildingStats.cs | `GetAttackPower(type, race)`, `GetAttackCooldown(type, race)` | 주석에 "현재 미사용, 향후 타워 기능용". 호출처는 GameBootstrapper.InitializeBuildingStatsFromConfig 의 주입만 — 실제 게임 로직에서는 호출 없음. | 낮음 (의도적 보존) |
| Infrastructure/Network/NetworkBuildingController.cs | 라인 266 `// TODO: UI 피드백 — 토스트 메시지, 버튼 흔들기 효과 등` | 방치된 TODO. ToastUI/ToastKey 시스템이 이미 구축돼 있어 연결만 하면 됨. | 중간 |
| Infrastructure/Network/NetworkProductionController.cs | 라인 850 `// TODO: UI 피드백 — 토스트 메시지 등` | 동일한 토스트 연결 TODO. 위 항목과 패턴 동일. | 중간 |
| Application/Events/GameEvents.cs | `OnUnitWalkStarted` Subject + 라인 551 "OnUnitWalkStopped 제거 — Idle 상태 없음" 주석 | 주석은 정리된 상태지만 OnUnitWalkStarted 자체는 멀티플레이 NetworkCombatController에서만 사용 — 싱글플레이에서는 발행/구독 없음. 발행/구독 양쪽 사용처 검증 필요. | 낮음 (확인 필요) |
| Application/UseCases/UnitProductionUseCase.cs | `_grid` 필드 (36) | 생성자에서 주입받지만 본문에서는 `FindSpawnTile()` 한 곳에서만 사용. 다른 ResourceUseCase/PopulationUseCase가 이미 HexGrid를 가지고 있어 중복 보관 패턴. (실제 사용은 있으므로 dead code는 아님) | 낮음 |

> 정리 우선순위 제안: 슬롯/점유 시스템(AttackPositionManager / TileOccupancyManager / FindForwardAvailable / UnitMovementUseCase._occupancyManager·_subscriptions·_unitSpawn) → 한 번에 함께 제거하면 약 600+줄 감축 가능. Bootstrap의 관련 주석 블록도 동시 정리.

---

## 2. 중복 코드 목록

| 중복 패턴 | 관련 파일 | 내용 | 심각도 |
|-----------|-----------|------|--------|
| `FindFirstObjectByType<GameBootstrapper>()` 반복 호출 | Infrastructure/Network/NetworkBuildingController.cs (54, 93, 198, 310, 411, 475, 566), NetworkCombatController.cs (106, 224, 432, 554), NetworkProductionController.cs (69, 265, 398, 502, 568, 650, 720, 771), NetworkHealthSync.cs (63, 143), NetworkResourceSync.cs (88, 217), NetworkTileSync.cs (72, 166), NetworkGameFlow.cs (78, 181), NetworkUnit.cs (178), NetworkUnitMovementController.cs (58, 126), ReconnectionHandler.cs (188) | 같은 메서드 안에서도 보호적 재탐색을 반복함. 총 30+ 호출 지점. GameBootstrapper는 씬당 1개 유일 객체이므로 한 번만 찾아 NetworkBehaviour의 OnNetworkSpawn에서 캐시해야 함. 일부 NetworkController가 이미 _bootstrapper 캐시 필드를 가지고 있음에도 "null이면 다시 찾기" 패턴이 메서드마다 중복됨. | 높음 |
| Castle 자동 배치 / Castle 파괴 판정 로직 분기 | Application/UseCases/GameEndUseCase.cs (45~80), BuildingPlacementUseCase.cs (DemolishBuilding 호출 시 Castle 검증), NetworkGameEndController (Forfeit 흐름) | "Castle인지 확인" 로직이 GameEndUseCase, ProductionPanelUI, BuildingActionPanelUI, BuildingTypeHelper.CanShowActionPanel 등에 흩어져 있음. BuildingTypeHelper.IsCastle(BuildingType) 헬퍼 1개로 통일 가능. | 중간 |
| `if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)` 클라이언트 분기 | Application/UseCases/UnitCombatUseCase.cs (208), GameEndUseCase.cs (71), Bootstrap/GameBootstrapper.cs (374, 794, 1001) 등 | "멀티플레이의 비서버 클라이언트인가" 판정 로직이 반복됨. NetworkContext에 IsClientOnly 또는 IsServerAuthority 같은 헬퍼 프로퍼티로 추출 가능. | 중간 |
| BuildingPlacementUseCase의 PlaceBuilding / PlaceMiningPost / PlaceMiningPostDirect / PlaceBuildingWithId | Application/UseCases/BuildingPlacementUseCase.cs | 네 메서드가 거의 동일한 처리: `tile.IsWalkable = false` → `_grid.SetOwner` → 인접 타일 소유권 → 이벤트 발행 2개. `PlaceBuildingInternal`로 1차 통합되어 있으나 PlaceBuildingWithId는 별도 구현으로 같은 흐름이 다시 반복됨. | 중간 |
| 사망 이벤트 발행 후 RemoveUnit/RemoveBuilding 직전 호출 패턴 | Application/UseCases/UnitCombatUseCase.cs (793~820), BuildingPlacementUseCase.cs (DemolishBuilding 290~307), Infrastructure/Network/NetworkCombatController.cs | "OnUnitDied/OnBuildingDied 발행 → Dictionary에서 제거" 순서가 여러 곳에 동일하게 반복. 향후 누군가 한 곳만 수정하면 순서 불일치 위험. | 중간 |
| 종족별 프리팹 조회 (Human/Spirit/Transcendence switch) | Infrastructure/Factories/BuildingFactory.cs (GetPrefab 183~202), UnitFactory.cs, ProductionPanelUI.cs (BuildingUnitMapping), BuildingPlacementUI.cs (6개 종족별 리스트) | "RaceId → 리스트 선택"이 매번 switch로 반복. 종족이 늘어나면 모든 곳에 case 추가 필요. RacePrefabRegistry 등 단일 조회 헬퍼로 추출 가능. | 중간 |
| Animator 파라미터 해시 캐싱 패턴 | Presentation/Unit/UnitView.cs (60~65), Animator.StringToHash 호출 | 단일 클래스 내부지만 비슷한 해시 다수. 향후 다른 애니메이션 사용 컴포넌트(예: 건물 애니메이션)가 생기면 공통 AnimHashes 정적 클래스로 분리 권장. | 낮음 |
| 마지막 결과 GameObject Dictionary 관리 | Infrastructure/Factories/UnitFactory.cs (_unitObjects), BuildingFactory.cs (_buildingObjects), Presentation/Production/ProductionTicker.cs (_rallyMarkers) | "Dictionary<int, GameObject> + DestroyAll + 이벤트 구독으로 Destroy" 패턴이 세 군데에 거의 동일하게 구현됨. 제네릭 SpawnedObjectRegistry<TKey> 등으로 추출 검토 가능. | 낮음 |
| `Vector3.Distance` + Epsilon 보정 거리 판정 | Application/UseCases/UnitCombatUseCase.cs (FindFirstEnemyTarget 539~611, FindFirstEnemyInDetectRange 462~530, IsTargetInRange 716~761) | 거의 동일한 unitMaxDist/buildingMaxDist 분기 로직이 세 메서드에 반복됨. CalculateRangeLimits(attacker, isDetect) 같은 헬퍼로 통합 가능. | 중간 |

---

## 3. 개선 필요 코드 목록

| 파일 | 위치 | 문제 유형 | 내용 | 심각도 |
|------|------|-----------|------|--------|
| Application/UseCases/UnitCombatUseCase.cs | 라인 17 | 아키텍처 위반 | `using Hexiege.Core;` — Application 레이어가 Core에 의존. CLAUDE.md 규약상 Application은 Domain만 의존해야 함. HexMetrics.TileHeight / WorldToHex 사용을 위해 들어옴. (Domain에 동등 헬퍼 추가 또는 IEntityPositionProvider 같은 패턴으로 추출 검토) | 높음 |
| Application/UseCases/UnitMovementUseCase.cs | 라인 27 | 아키텍처 위반 | `using Hexiege.Core;` — HexMetrics, ViewConverter 사용. UnitMovementUseCase는 도메인 로직만 담당해야 하나 Vector3/HexMetrics를 직접 호출함. | 높음 |
| Application/UseCases/GridInteractionUseCase.cs | 라인 25 | 아키텍처 위반 | `using Hexiege.Core;` — HexMetrics.WorldToHex 호출. 입력 좌표 변환은 Presentation(InputHandler)에서 한 뒤 HexCoord로 전달하는 편이 레이어 책임에 맞음. | 높음 |
| Application/Services/AttackPositionManager.cs | (전체) | 아키텍처 위반 | 폐기된 코드지만 잔존하는 동안 Core(HexMetrics) 직접 의존 — 제거 시 함께 해소. | 낮음 |
| Application/Services/TileOwnershipService.cs | 라인 37 | 아키텍처 위반 | `using Hexiege.Core;` — 파일 헤더 주석에 "Domain/Core 참조 가능"이라고 명시되어 있으나 CLAUDE.md 원칙과 충돌. Application 레이어 단일 규칙으로 통일 필요. (정책 결정 필요) | 중간 (확인 필요) |
| Presentation/UI/NetworkStatusUI.cs | 라인 29~30 | 아키텍처 위반 가능성 | Presentation 레이어에서 `Unity.Netcode`, `Unity.Netcode.Transports.UTP` 직접 의존. 헤더 주석에 "NetworkBehaviour 불필요: 로컬 표시 전용"이라고 명시되었으나 NGO API를 직접 호출. NetworkContext 등 추상화 검토. | 중간 |
| Presentation/UI/LobbyUI.cs | 라인 37, 95 | 아키텍처 위반 + 매 호출 Find | Presentation이 NGO 의존 + `FindFirstObjectByType<NetworkGameManager>()` 매번 호출. | 중간 |
| Presentation/UI/GameEndUI.cs | 라인 32 | 아키텍처 위반 | `using Unity.Netcode;` — Presentation이 NGO 직접 참조. | 중간 |
| Presentation/UI/GameHudUI.cs | 라인 26 | 아키텍처 위반 | `using Unity.Netcode;` — 동상. | 중간 |
| Presentation/UI/ProductionPanelUI.cs | 라인 27 | 아키텍처 위반 | `using Unity.Netcode;` — RequestProductionServerRpc 등 호출 위해 도입된 듯하나 NetworkProductionController를 통한 추상화가 이미 있음. 직접 의존 제거 가능. | 중간 |
| Presentation/UI/BuildingPanelBase.cs | 라인 41 | 아키텍처 위반 | `using Unity.Netcode;` — 동상. | 중간 |
| Presentation/UI/BuildingPlacementUI.cs | 라인 31 | 아키텍처 위반 | `using Unity.Netcode;` — 동상. | 중간 |
| Presentation/UI/InGameSettingsUI.cs | 라인 211 | Find 호출 | `FindFirstObjectByType<NetworkGameEndController>()` — 매 포기 시도 시 호출. NetworkGameEndController 인스턴스를 Initialize에서 주입받도록 변경 권장. | 중간 |
| Presentation/Production/ProductionTicker.cs | 라인 39 | 아키텍처 위반 | `using Unity.Netcode;` — Presentation 레이어가 NGO 직접 참조. | 중간 |
| Infrastructure/Config/ToastMessageConfig.cs | 라인 | Presentation 참조 | ToastMessageConfig가 Hexiege.Presentation을 import — 일반적으로 Infrastructure는 Presentation을 모르는 게 깔끔함 (역방향 의존). Config는 데이터만 담는 것이 이상적. (확인 필요) | 중간 (확인 필요) |
| Infrastructure/Factories/UnitFactory.cs | (헤더의 using Hexiege.Presentation) | Infrastructure→Presentation 의존 | UnitFactory가 Presentation.UnitView 컴포넌트를 직접 GetComponent. Infrastructure → Presentation 의존은 일반적으로 역방향. 별도 IUnitViewBinder 같은 인터페이스로 추상화 가능. | 중간 |
| Infrastructure/Network/NetworkGameEndController.cs | 헤더 using | Infrastructure→Presentation 의존 | NetworkGameEndController가 Presentation의 GameEndUI, RematchRequestPopup, GameUIManager를 직접 참조. UI 갱신은 이벤트(OnGameEnd, OnRematchRequested 등)로 추상화하면 양방향 결합 해소 가능. | 중간 |
| Infrastructure/Network/NetworkGameEndController.cs | 라인 98, 105, 110, 153, 182, 333 | 매 호출 Find | `FindFirstObjectByType<...>` 다수 호출. GameBootstrapper Inspector 주입으로 변경 권장 (이미 GameBootstrapper에 _networkGameEnd 필드 존재함). | 중간 |
| Bootstrap/GameBootstrapper.cs | Update() (352~378) | 매 프레임 호출 | Update에서 `IsNetworkMode()` 호출 → NetworkManager.Singleton.IsHost/IsClient 매번 평가. NetworkContext 정적 홀더에 캐시된 값을 쓰는 편이 일관됨. | 낮음 |
| Bootstrap/GameBootstrapper.cs | `IsNetworkMode()` (1311) | 일관성 | `NetworkManager.Singleton` 직접 호출. 다른 곳은 NetworkContext.IsNetworkActive를 사용하므로 일관성 깨짐. | 중간 |
| Application/UseCases/UnitProductionUseCase.cs | `EnqueueUnit`, `ToggleAutoProduction`, `CancelQueueAt` 등 (98~430) | 메서드 길이 | 단일 메서드가 30줄 가이드를 초과 (EnqueueUnit 60줄, ToggleAutoProduction 150+줄, CancelQueueAt 100+줄). 각 Rule별 헬퍼 분리로 가독성 확보 가능. | 중간 |
| Application/UseCases/UnitCombatUseCase.cs | `FindFirstEnemyTarget` (539), `FindFirstEnemyInDetectRange` (462), `IsTargetInRange` (716) | 메서드 길이 + 중복 | 각각 70~90줄 + 거리 판정 공식 중복. 거리 한계 계산 헬퍼로 분리 권장. | 중간 |
| Application/UseCases/UnitSpawnUseCase.cs | `GetUnitAt(HexCoord)` (109) | O(n) 탐색 | 매 호출 시 `foreach (_units)` — 유닛 수가 많아지면 비용 누적. 주석에 "유닛 수가 적어 문제 없음"이지만 ProductionTicker 등에서 매 프레임 호출 가능성 확인 필요. 위치 → 유닛 역인덱스 Dictionary 도입 검토. | 중간 |
| Application/UseCases/BuildingPlacementUseCase.cs | `GetBuildingAt(HexCoord)` (265) | O(n) 탐색 | 동일 패턴. 동일 개선안 적용 가능. | 중간 |
| Application/UseCases/PopulationUseCase.cs | `GetUsedPopulation(team)` (43) | O(n) 탐색 | 매 호출 시 전체 건물+유닛 순회. 인구 검증은 매 EnqueueUnit/ToggleAutoProduction 호출마다 일어남. 팀별 카운터 캐시 또는 OnUnitSpawned/OnUnitDied 이벤트로 증감 추적 권장. | 중간 |
| Domain/Hex/HexGrid.cs | `CountTilesOwnedBy(team)` (202) | O(n) 탐색 | 187타일 순회. PopulationUseCase.GetMaxPopulation에서 매 호출. Dictionary<TeamId, int> 카운터로 캐시 권장. | 낮음 |
| Application/UseCases/UnitProductionUseCase.cs | `TickProgressOnly(deltaTime)` (565) | 책임 모호 | "멀티플레이 클라이언트 전용" 메서드가 Application 레이어에 있음. NetworkContext 분기 없이 호출자가 모드 판단 필요 — 호출자 책임/실수 위험. 메서드 안에서 NetworkContext 가드를 두거나 Network 컨트롤러 측에서만 호출하도록 명시 필요. | 중간 |
| Infrastructure/Network/MatchmakerManager.cs | 라인 113, 116, 136, 139 | 일반 Exception 사용 | `throw new Exception(...)` — 매칭 실패에 일반 Exception 사용. MatchmakingException 등 도메인 예외 또는 Result 패턴으로 변경하면 호출자가 catch 분기 명확. | 낮음 |
| Presentation/UI/Common/AnimatedPanel.cs | 라인 150, 155 | GetComponent 비캐시 | `GetComponent<CanvasGroup>()`, `GetComponent<RectTransform>()` — Awake/OnEnable에서 1회 캐시되는지 확인 필요. 매번 호출 시 비용 누적. (Read 결과 Awake 캐시 패턴인 듯하나 검증 필요) | 낮음 (확인 필요) |
| Presentation/UI/Common/RematchRequestPopup.cs | 라인 123 | GetComponent 비캐시 | `var cg = go.GetComponent<CanvasGroup>();` — 호출 빈도 확인 필요. | 낮음 (확인 필요) |
| Presentation/UI/ProductionPanelUI.cs | 라인 262, 309 | GetComponent + AddComponent in 루프 | `button.gameObject.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();` — Show할 때마다 호출되면 비용 발생. 초기화 시 1회로 모아두기 권장. | 중간 |
| Presentation/UI/ProductionPanelUI.cs | 라인 309 | 부모 GetComponent | `_queueSlotImages[i].GetComponent<Button>() ?? _queueSlotImages[i].transform.parent?.GetComponent<Button>();` — 매 호출 시 동적 탐색. Inspector 직접 참조로 변경 권장. | 중간 |
| Presentation/Production/ProductionTicker.cs | 라인 307, 673 | 매번 GetComponent | `unitObj.GetComponent<UnitView>()` — UnitView가 프리팹에 단일 부착이므로 UnitFactory에서 한 번 캐시한 뒤 GetView(unitId)로 노출 권장. | 중간 |
| Domain/Building/BuildingTypeHelper.cs | `IsProductionBuilding`, `GetStage`, `GetNextStage` (각각 switch) | 유지보수성 | 같은 BuildingType이 세 switch에 분산 — 신규 건물 추가 시 세 곳 모두 수정 필요. struct/static 테이블로 1개소 데이터 등록 후 조회 메서드 3개로 변경하면 유지보수 부담 감소. | 중간 |
| Domain/Building/BuildingStats.cs | `GetUpgradeCost(type)` (181~188) | 비효율 조회 | "어떤 종족 키로도 같은 값" 가정으로 Human → Spirit → Transcendence 순서로 3번 TryGet 시도. UpgradeCost를 별도 Dictionary로 분리하면 1회 조회. | 낮음 |
| Domain/Unit/UnitData.cs | 생성자 2개 (109, 145) | 코드 중복 | 일반 생성자와 ID 지정 생성자가 거의 동일 코드 — 일반 생성자가 ID 지정 생성자를 호출하는 형태로 단순화 가능. | 중간 |
| Domain/Building/BuildingData.cs | 생성자 2개 (58, 78) | 코드 중복 | 동일 패턴. 위와 동일 개선 가능. | 중간 |
| Application/Events/GameEvents.cs | `OnUnitEnteredTile` (438) | 일관성 | 다른 모든 게임 이벤트는 `Subject<T>`이지만 이 항목만 `Action<int, HexCoord>` 일반 델리게이트. 일관성을 위해 `Subject<(int, HexCoord)>`로 통일 검토. | 낮음 |
| Bootstrap/GameBootstrapper.cs | SerializeField 다수 (44~140) | 큰 파일 | 1342줄 파일. SetupBuildings, SetupProduction, SetupInput, PlaceCastles, PlaceGoldMines 등을 별도 BootstrapModule로 추출하면 변경 추적이 쉬워짐. | 중간 |
| Bootstrap/GameBootstrapper.cs | 라인 1004 | 익명 람다 + ActionDisposable | OnUnitEnteredTile 구독 해제를 위해 인라인 ActionDisposable 래퍼 사용. GameEvents.OnUnitEnteredTile을 Subject로 통일하면 표준 .Subscribe(...).AddTo() 패턴으로 단순화. (위 항목과 연결) | 낮음 |
| Application/UseCases/UnitProductionUseCase.cs | `EnqueueUnit` 등 모든 큐 메서드 | null 체크 누락 가능 | `_resource`, `_population` 등 생성자 주입 의존성에 대한 null 가드 없음. GameBootstrapper에서 항상 주입되므로 실패 가능성은 낮으나, 테스트 시나리오에서 부분 주입 시 NRE 위험. | 낮음 |
| Presentation/UI/InGameSettingsUI.cs | `OnForfeitConfirmed()` 흐름 | 분기 책임 | `NetworkContext.IsNetworkActive` 분기로 RequestForfeit / GameEndUseCase.Forfeit을 직접 호출. UI 컴포넌트가 모드 분기를 알아야 함 — IForfeitService 같은 추상화로 분리하면 UI는 단일 호출로 단순화 가능. | 낮음 |
| Infrastructure/Network/NetworkBuildingController.cs | RequestUpgradeServerRpc 등 RPC 메서드 | RPC 메서드 길이 | 한 ServerRpc 메서드가 검증 + 비즈니스 호출 + ClientRpc 트리거를 모두 처리. 검증 헬퍼로 추출하여 가독성 확보 가능. | 중간 |
| Domain/Building/BuildingType.cs | enum 순서 (25~81) | 직렬화 위험 | 주석에 "열거형 멤버 순서 변경 시 직렬화 데이터 깨질 수 있음" 명시. 현재 단계별/종족별 정의 순서로 묶여 있어 사이에 새 멤버 삽입 시 인덱스 밀림 위험. 명시적 값(`Castle = 0` 등) 부여 권장. | 중간 |

---

## 4. 요약 및 우선순위

### 전체 현황

- 분석 대상 스크립트: **약 100여 개** (Assets/_Project/Scripts/ 전체)
- Editor 폴더: 비어있음 (메모리 기록상 SetupBuildingStatsConfig 등이 있어야 하나 워크트리에서 확인되지 않음 — 확인 필요)
- 코드 전반적인 품질은 **양호** — Clean Architecture 골격이 잡혀 있고, 이벤트 기반 통신이 일관되게 적용됨. 다만 빠른 기능 추가로 인해 **폐기된 시스템의 잔재**와 **레이어 위반**이 다수 누적.

### 가장 우선해야 할 정리 (높음 심각도)

1. **슬롯/점유 시스템 잔재 제거** — 2026-05-11 폐기 선언 이후 1주일 이상 코드 잔존. 함께 제거 가능:
   - `Application/Services/AttackPositionManager.cs` (전체 파일)
   - `Application/Services/TileOccupancyManager.cs` (전체 파일)
   - `Application/UseCases/UnitMovementUseCase.cs` 의 `_occupancyManager`, `_subscriptions`, `_unitSpawn`, `FindForwardAvailable`
   - `Bootstrap/GameBootstrapper.cs` 의 `GetAttackPositionManager`, `GetTileOccupancyManager`, 관련 주석 블록 다수
   - 예상 감축 분량: **500~700줄**

2. **Application → Core 의존 위반 4건 정리** (CLAUDE.md 핵심 규칙 위반):
   - `UnitCombatUseCase.cs`, `UnitMovementUseCase.cs`, `GridInteractionUseCase.cs`, `TileOwnershipService.cs`
   - 해결 방향: HexMetrics의 World↔Hex 변환을 IHexCoordinateMapper 같은 인터페이스로 추상화하여 Application은 인터페이스만 의존 / Bootstrap이 Core 구현체 주입.
   - 단, TileOwnershipService는 헤더 주석에 "Core 참조 가능"이라고 명시되어 정책 결정 필요.

3. **Presentation → NGO 직접 의존 8건 정리** — Presentation은 Infrastructure를 모르고 NetworkContext/이벤트로만 통신해야 함:
   - `LobbyUI`, `NetworkStatusUI`, `GameEndUI`, `GameHudUI`, `ProductionPanelUI`, `BuildingPanelBase`, `BuildingPlacementUI`, `ProductionTicker` (Presentation 분류)
   - 해결 방향: NGO 호출 부분을 Infrastructure 네트워크 컨트롤러로 옮기고 UI는 컨트롤러 인터페이스만 호출.

4. **`FindFirstObjectByType<GameBootstrapper>()` 30+ 회 반복** — 네트워크 컨트롤러 전반:
   - 모든 NetworkBehaviour의 `OnNetworkSpawn`에서 단 1회 캐시 후 재사용.
   - 일부 컨트롤러는 이미 캐시 필드를 가지고 있어 보호적 재탐색 줄을 제거하기만 하면 됨.

### 다음 순위 (중간 심각도)

- `ProductionPopupDiagnostic.cs` 1회성 진단 도구 제거
- `BuildingType` 열거형 명시적 값 부여로 직렬화 안전성 확보
- O(n) 탐색 캐싱: `GetUnitAt`, `GetBuildingAt`, `CountTilesOwnedBy`, `GetUsedPopulation`
- `UnitProductionUseCase` 큰 메서드 분해 (EnqueueUnit / ToggleAutoProduction / CancelQueueAt)
- TODO 2건 (NetworkBuildingController, NetworkProductionController) — 이미 ToastUI 기반 인프라가 있으므로 1~2시간 작업으로 해소
- `NetworkGameEndController`의 `FindFirstObjectByType` 다수 → GameBootstrapper 주입으로 교체

### 여유 있을 때 (낮음 심각도)

- `AttackKind` enum + 관련 잔재 — UI 분류 용도로 보존 명시되어 있어 사용처 결정 후 정리
- 생성자 중복 (UnitData, BuildingData) — `: this(...)` 위임으로 단순화
- `GameEvents.OnUnitEnteredTile`만 Action 패턴 — Subject로 통일하면 ActionDisposable 래퍼도 제거 가능
- `BuildingTypeHelper`의 3개 switch → 데이터 테이블 1개로 통합

### 결론

총 위반 사례를 정리해 보면:
- 폐기/dead code: **약 600+줄** 제거 가능 (한 번에 작업 권장)
- 레이어 위반: **15+ 파일** (Application→Core 4건, Presentation→NGO 8건, Infrastructure→Presentation 3건+)
- 중복 패턴: **8개 카테고리** (가장 효과가 큰 것: FindFirstObjectByType 캐시화 30+ 호출 해소)
- 비효율/null 위험: **약 15개 항목** (즉시 위험은 적으나 모바일 성능 관점에서 누적 영향)

권장 작업 순서:
1. (1회 작업) 슬롯/점유 시스템 잔재 일괄 삭제 + Bootstrap 주석 정리
2. (분리 작업) Application → Core 의존 4건 — 인터페이스 추출로 한 번에 해결
3. (분리 작업) Presentation → NGO 의존 8건 — UI별 NetworkXxxController 인터페이스 의존으로 전환
4. (별도 작업) FindFirstObjectByType 30+회 캐시화
5. (별도 작업) 데이터 구조 캐싱 (인구/타일 카운트, 위치 역인덱스)

이상의 작업을 차례로 진행하면 코드 양이 줄고, CLAUDE.md 규약과 코드가 일치하게 되어 향후 신규 기능 추가 시 의사결정 부담이 크게 감소합니다.
