# Game Programmer Agent Memory

## CRITICAL — GIT 명령 절대 금지
- **모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## CRITICAL — 구현 시 필수 확인 제약

### 레이어 제약
- Domain: `using Hexiege.Core` 절대 금지 → HexOrientationContext 등 정적 홀더 패턴
- NetworkBehaviour: Infrastructure 레이어에만 (Presentation/Application 금지)
- Application: Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더 패턴
- GameBootstrapper = 유일한 의존성 조합 루트
- Assembly Definition 없음 — 네임스페이스 규약만

### NGO API 제약
- ServerRpc/ClientRpc 메서드명: 반드시 `ServerRpc`/`ClientRpc`로 끝나야 함
- NGO 2.9.2, Enable Scene Management = ON
- NetworkBehaviour는 씬에 NetworkObject로 배치해야 RPC 작동
- RPC 파라미터: 직렬화 가능 타입만 (INetworkSerializable 또는 기본 타입/enum)
- 클라이언트 전용 분기: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`

## 최근 작업

### 건물 생성/파괴 시 유닛 이동 멈춤 수정 (2026-05-17) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/17_00_building-repath-freeze-fix/`

**수정 파일**: `Presentation/Unit/UnitView.cs`

**문제**: 건물 생성/파괴 시 `RepathAllAliveUnits → OnPathInvalidated → MoveTo` 흐름으로 코루틴이 즉시 재시작되어 1~2 프레임 유닛 멈춤 발생.

**수정 내용**:
- **필드 2개 추가**: `_pendingPath (List<HexCoord>)`, `_currentNextTileCoord (HexCoord?)`
- **`OnPathInvalidated()` 분기 추가**:
  - 현재 Lerp 중인 다음 타일(`_currentNextTileCoord`)에 건물이 생긴 경우 → 기존처럼 즉시 `MoveTo()` (건물 뚫고 지나가기 방지)
  - 그 외 → `_pendingPath = newPath` 저장만 (코루틴 유지, 멈춤 없음)
- **`MoveAlongPathV3()` 수정**: 각 타일 Lerp 시작 직전 `_currentNextTileCoord` set, 완료 직후 null. 타일 도착 직후 `_pendingPath` 소비 → 현재 위치로 새 path 슬라이스 후 외부 while 재진입. 인덱스 못 찾으면 `MoveTo()` 안전망.
- **`MoveTo()` 수정**: 진입 시 `_pendingPath = null`, `_currentNextTileCoord = null` 초기화.
- **`MoveCleanupAndCompleteV3()` 수정**: 종료 시 두 필드 모두 null 초기화.

**핵심 설계**:
- "부드러운 교체(예약) = 기본, 즉시 재시작 = 예외(앞 타일 막힌 경우만)"
- GameBootstrapper/FlowFieldService 변경 없음. UnitView.cs 단독 수정.

---

### 건물 배치 패널 실패 피드백 + UI 개선 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/20_10_building-placement-fail-feedback/`

**수정 파일**:
- `BuildingPlacementUI.cs` — 3가지 변경:
  1. **UpdateCostTextColors()** 신규 private 메서드 추가. `_buildingCostTexts[i]`를 순회하며 현재 골드와 건설 비용 비교, 부족 시 `Color.red`, 충분 시 `Color.white`.
  2. **Show()** 마지막에 `UpdateCostTextColors()` 즉시 호출 + `GameEvents.OnResourceChanged` 구독(`_resourceSubscription: IDisposable`). 팝업 열린 동안만 실시간 갱신.
  3. **Close()** 앞에 `_resourceSubscription?.Dispose()` + 비용 텍스트 전체 `Color.white` 초기화.
  4. **PlaceAndClose() 싱글플레이 분기** — 골드 부족 시 `ToastUI.Show(ToastKey.GoldInsufficient)` 호출 후 `return`(팝업 유지).

**핵심 설계**:
- 멀티플레이 분기는 수정하지 않음 (범위 밖).
- `IDisposable _resourceSubscription` 패턴으로 Show/Close 생명주기에 이벤트 구독을 한정.
- `GetBuildingList(_currentTeam, race)` 기존 메서드 재사용으로 버튼-텍스트 인덱스 일치 보장.

---

### ToastUI SetActive 버그 수정 (2026-05-16) ✅ 완료

**수정 파일**: `Presentation/UI/Common/ToastUI.cs`

**버그**: `ClearAll()` / `FinishCurrent()` 에서 `_canvasGroup.gameObject.SetActive(false)` 호출 → `OnGameStarted`로 ClearAll 실행 시 루트 비활성화 → `Update()` 정지 → 이후 토스트 큐 완전히 동작 불가.

**수정**: 3곳에서 `SetActive(false/true)` 제거:
- `TryShowNext()`: `SetActive(true)` → `blocksRaycasts=true, interactable=true`
- `FinishCurrent()`: `SetActive(false)` → `blocksRaycasts=false, interactable=false`
- `ClearAll()`: `SetActive(false)` → `blocksRaycasts=false, interactable=false`

**원칙**: Toast 루트 GameObject는 항상 활성 상태. 숨김은 `alpha=0 + blocksRaycasts=false`만으로 처리.

---

### 건물 비용 텍스트 'G' 접미사 제거 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/21_30_building-cost-g-removal/`

**수정**: `BuildingPlacementUI.cs` 2곳 — `$"{cost}G"` → `$"{cost}"`.
생산 패널(원래부터 숫자만)과 동일한 표기로 통일.

---

### 유닛 생산 실패 피드백 시스템 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/15_09_production-fail-feedback/`

**신규 파일 4개**:
- `Infrastructure/Config/ToastMessageConfig.cs` — ScriptableObject. ToastEntry struct(key/message/duration). TryGet().
- `Presentation/UI/Common/ToastKey.cs` — enum: GoldInsufficient=0, PopulationFull=1, ProductionQueueFull=2.
- `Presentation/UI/Common/ToastUI.cs` — 싱글턴 MonoBehaviour. IPointerClickHandler 구현. 정적 진입점 `ToastUI.Show(ToastKey)`. Queue<ToastKey> 방식. DontDestroyOnLoad 독립 Canvas. CanvasGroup DOTween 페이드아웃. GameEvents.OnGameStarted/OnGameEnd 구독으로 자동 정리.
- `Editor/SetupToastUI.cs` — 1회성 에디터 스크립트. Toast를 씬 루트 오브젝트(부모 없음)로 생성. 자체 Canvas(ScreenSpaceOverlay, sortingOrder=100) + GraphicRaycaster + CanvasGroup + ToastUI.

**핵심 주의사항**:
- **DontDestroyOnLoad = 루트 오브젝트 전용**: Toast를 [UI] Canvas 자식으로 배치하면 씬 전환 시 파괴됨. 반드시 씬 루트(부모 없음)에 배치.
- **SetActive(false) 사용 금지**: 비활성 상태에서 Awake() 미호출 → DontDestroyOnLoad 미등록. 숨김은 CanvasGroup.alpha=0으로 처리.
- **골드 텍스트 색상**: `_goldText`(보유 골드 표시)는 변경 안 함. `_unitCostTexts[i]`(각 유닛 생산 비용)만 개별 평가하여 빨간색 전환.

**수정 파일**:
- `ProductionPanelUI.cs` — `ProductionFailReason` enum 추가. `OnUnitTap()` 사전 검증 + HandleProductionFail(). `UpdateInfoBar()` 유닛별 비용 텍스트 색상 조건 추가.
- `GameHudUI.cs` — `_lastPopFull` nullable 캐시 필드. `UpdateDisplay()` 인구수 텍스트 `used >= max` 조건 색상 전환.
- `UnitProductionUseCase.cs` — `TryStartNext()` 자동 생산 자원 부족 시 재시도 → 즉시 취소(IsCharged=false만, IsCharged=true는 Rule 2 유지).

---

### 랠리포인트 깃발 팀별 표시 분리 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/15_30_rally-point-flag-visibility/`

**버그**: 클라이언트가 랠리포인트 설정 시 호스트 화면에도 깃발이 표시되던 현상.

**원인**: `RallyPointChangedEvent`에 팀 정보가 없어, `ProductionTicker`가 상대 팀 이벤트도 무조건 처리.
호스트가 RPC 핸들러에서 `SetRallyPoint()`를 실행하면 호스트 측에서도 `OnRallyPointChanged` 발생.

**수정 파일 (3개)**:
- `GameEvents.cs` — `RallyPointChangedEvent`에 `TeamId Team` 필드 추가, 생성자 파라미터 추가
- `UnitProductionUseCase.cs` — `SetRallyPoint()` / `ClearRallyPoint()` 이벤트 발행 시 `state.Team` 전달
- `ProductionTicker.cs` — `OnRallyPointChanged()` 진입부에 팀 필터 추가. `IsServer → Blue`, 아니면 `Red`. 싱글플레이(NetworkManager=null) 시 필터 건너뜀.

**설계 원칙**: 이벤트가 자기 완결적이 되도록(누구 팀 것인지 이벤트 자체에 포함), 필터링 책임은 Presentation 레이어(ProductionTicker)에 위치.

---

### 혼잡도 기반 유닛 분산 시스템 (2026-05-15) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-15/17_29_congestion-based-spread/`

**문제**: 모든 유닛이 성 방향으로 세로 줄 이동 현상. v1(CastleApproachManager — 성 인접 타일 배정)은 경로가 거의 동일해 시각 효과 없었음.

**신규 파일**:
- `Application/Services/CongestionMap.cs` — 타일별 혼잡도 관리 (Increment/Decay/Clear). 순수 C#.
- `Application/Services/CongestionAwarePathfinder.cs` — 혼잡도 가중 A*. 타일 비용=1+(혼잡도×CongestionWeight). 목적지 non-walkable이면 walkable 인접 자동 대체.

**삭제 파일**:
- `Application/Services/CastleApproachManager.cs` — v1 전체 삭제 (테스트 완료 후)
- `Infrastructure/Config/CongestionConfig.cs` — 필요 없어 미생성 (GameConfig에 통합)

**수정 파일**:
- `GameConfig.cs` — `CongestionDecayInterval=5f`, `CongestionWeight=3f` 필드 추가 (Header "Congestion Spread")
- `GameEvents.cs` — `OnUnitEnteredTile: Action<int, HexCoord>` 추가
- `UnitView.cs` — `_isAStarMoving` bool 필드. A* 이동 시 true, 전투 추격 시 false. 타일 전환 완료 시 `_isAStarMoving=true`이면 OnUnitEnteredTile 발행.
- `ProductionTicker.cs` — CongestionMap/Pathfinder 주입. 감쇠 타이머(`_decayTimer`). MoveTowardEnemyCastle에서 A* 우선, 실패 시 BFS 폴백.
- `GameBootstrapper.cs` — CongestionMap/Pathfinder 생성. OnUnitEnteredTile 구독(서버 가드: `if NetworkActive && !IsServer return`). ClearAll에 Clear 추가.

**핵심 설계 결정**:
- CongestionConfig ScriptableObject 미생성 → GameConfig.asset에 2필드 통합 (ScriptableObject 낭비 방지)
- reactive congestion: 유닛이 실제 타일 진입 시점에 혼잡도 증가 (사전 등록 아님) — 같은 건물에서 동시 생산 불가이므로 반응형으로 충분

---

### 로비 캐릭터 잘못 표시 버그 — 로그 추가 + 원인 확정 (2026-05-15) ✅ 완료

**작업 내용**: 랜덤 매칭 후 Red 클라이언트의 캐러셀에 선택한 종족 대신 Human이 잠깐 표시되는 버그 추적.

**로그 추가 파일**: `Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs`
- `ApplyCarouselPositions()`: Inspector 위치값, 캐릭터 배열 수, 각 캐릭터별 현재위치/목표위치 로그 추가
- `KillAllCharacterTweens()`: 호출 시각 로그 추가

**원인 확정**: CharPreview_Human/Spirit/Transcendence가 실제 유닛 프리팹(Unit_Pistoleer_Blue 등) 인스턴스 → NetworkTransform이 Host 캐러셀 위치를 Red 클라이언트로 동기화하여 DOTween 위치를 덮어씀. 코드 수정 없이 Unity Editor 작업으로 해결.

**수정 (에디터 작업)**: Lobby.unity에서 CharPreview 3종 Unpack Completely → UnitView, AnimationEventRelay, NetworkUnit, NetworkTransform, NetworkObject 컴포넌트 제거.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-15/02_57_character-display-bug/`

---

### 유닛 회전 시스템 수정 + MovementLogger 삭제 (2026-05-14) ✅ 완료

**수정 파일**:
- `Presentation/Unit/UnitView.cs`
  - `[SerializeField] private float _rotationSpeed = 270f` (기존 `const CombatRotationSpeed = 270f` 교체)
  - A* 이동 방향 계산: `FacingDirection.FromCoords(from, to)` → `CalculateAttackAngle(toPos)` (현재 월드 위치→목적지 Atan2)
  - A* Lerp 루프 내 매 프레임 `Quaternion.RotateTowards(현재, targetRot, _rotationSpeed * Time.deltaTime)` 추가
  - 정렬(Align) 단계 방향 계산: 동일하게 `CalculateAttackAngle(alignView)` 교체
  - 정렬 Lerp 루프 내 동일하게 RotateTowards 추가
  - `ApplyDirection()` 호출부(2곳) 제거 (메서드 자체는 유지)
  - `MovementLogger.Log()` 29개 호출 전체 제거
- `Application/Services/MovementLogger.cs` — **파일 삭제**
- `Bootstrap/GameBootstrapper.cs` — `MovementLogger.SessionStart()` 제거
- `Application/Services/AttackPositionManager.cs` — `MovementLogger.Log()` 3개 제거

**핵심 설계 결정**:
- `CalculateAttackAngle`이 이미 Atan2 기반 정확한 각도 계산을 하므로 A*/정렬 회전에도 동일 메서드 재사용
- `_rotationSpeed` 단일 필드로 모든 회전(이동/정렬/추격/공격) 통일 — Inspector 조정 가능
- `ApplyDirection()` 메서드는 현재 호출처 없으나 코드에 남겨둠 (삭제는 별도 작업)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-14/14_30_unit-rotation-system-fix/`

---

### 유닛 이동/전투 시스템 재설계 (2026-05-11) ✅ 완료

슬롯 기반 분산 방식 전면 폐기 → 겹침 허용 단순 구조로 전환. 근접/원거리 동일 상태 머신.

**비활성화(주석 처리) 항목** (2026-05-11 당시):
- `GameBootstrapper.cs` — TileMoveSlotManager / TileOccupancyManager / AttackPositionManager 생성 및 주입 코드
- `Presentation/Unit/UnitView.cs` — 슬롯/점유 관련 필드(`_moveSlotManager`, `_attackPositionManager`, `_v2MoveSlotTile`, `_v2AttackSlotTargetCoord`, `_pendingOccupancyTile`, `_v2InStationaryCombat`) 및 메서드(`ReleaseV2MoveSlotIfClaimed`, `ReleaseV2AttackSlotIfClaimed`)
- `Application/UseCases/UnitMovementUseCase.cs` — `_occupancyManager`, `RegisterOccupancyMove()`, `ReleaseOccupancy()`, `FindForwardAvailable()`
- `Domain/Unit/UnitData.cs` — `ClaimedTile` 필드
- `Domain/Unit/UnitStats.cs` — `OccupancySize` 필드 및 `GetOccupancySize()` 메서드

**✅ 완전 제거 완료 (2026-05-16 dead-code-cleanup)**:
- `Application/Services/TileMoveSlotManager.cs` — **파일 삭제** (+ .meta)
- `Application/Services/TileOccupancyManager.cs` — 비활성 메서드 5개 제거 (`OnUnitMoved`, `OnUnitRemoved`, `ReserveOccupancy`, `BfsFindAvailable`, `FindForwardAvailable`). 클래스 자체는 유지.
- `Application/UseCases/UnitMovementUseCase.cs` — `RegisterOccupancyMove()`, `ReleaseOccupancy()`, `GetOccupancySize()` 제거
- `Domain/Unit/UnitData.cs` — `ClaimedTile` 프로퍼티 제거
- `Domain/Unit/UnitStats.cs` — `OccupancySize` 필드, `GetOccupancySize()` 제거
- `Presentation/Unit/UnitView.cs` — `ClaimedTile` 참조 7곳 제거
- `Bootstrap/GameBootstrapper.cs` — TileMoveSlotManager getter 및 OccupancySize 할당 라인 제거
- `Domain/Hex/HexPathfinder.cs` — `FindPathToNeighbor()` 제거 (호출처 없음)
- `Application/Events/GameEvents.cs` — `OnGamePaused`, `OnGameResumed` Subject 제거 (발행 코드 없음)
- `Presentation/UI/GameUIManager.cs` — OnGamePaused/OnGameResumed 구독 코드 및 Notify 메서드 제거
- `Presentation/UI/Core/IGameUI.cs` — `OnGamePaused()`, `OnGameResumed()` default 메서드 제거

**신규 구현**:
- `UnitView.cs` — `MoveAlongPathV3()` 새 상태 머신 (근접/원거리 동일):
  - Phase 0(A* Lerp) → HasEnemyInDetectRange 감지 → Phase 1(월드 직선 추격) → HasEnemyInRange 진입 → 공격 → FindForwardClosestTile → Phase 0 재개
- `UnitCombatUseCase.cs` — `FindFirstEnemyInDetectRange()` 내 isMelee 분기 제거, 모든 유닛 `DetectRange × TileHeight` 통일
- UnitStatsConfig Inspector — 원거리 유닛 DetectRange를 AttackRange보다 크게 설정

**BUG-001 (2026-05-12)**: 전투 추격 중 건물 생성/파괴 시 유닛 멈춤
- `_isInCombatPursuit` bool 필드 추가
- `IsInCombat()` → `_combatTargetTransform != null || _isInCombatPursuit`

**BUG-002 (2026-05-13)**: 전투 종료 후 약 1타일 순간이동
- `ResumeFromForwardTileV3()` 내 즉시 스냅(`transform.position = forwardView`) 제거
- `MoveAlongPathV3()` 전투 종료 직후 정렬 Lerp 추가 (동일 이동 속도로 걸어서 이동)
- 정렬 Lerp 내 매 프레임 적 감지 체크 (중단 시 전투 이동 재진입)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-11/23_19_unit-movement-redesign/`

---

### 이동 슬롯 오프셋 Inspector 조정 기능 추가 (2026-05-11) ✅ 사용자 확인 완료

**수정 파일**:
- `Application/Services/TileMoveSlotManager.cs` — `private const float SlotForwardRatio/SlotSideRatio` → `private readonly float`. 기본값 0.30f를 유지하는 생성자 파라미터 추가. `GetSlotWorldPositionInternal`을 `static` → 인스턴스 메서드로 전환(readonly 필드 접근을 위해).
- `Bootstrap/GameBootstrapper.cs` — `[Header("이동 슬롯 오프셋")]` + `[SerializeField] private float _slotForwardRatio/SideRatio = 0.30f` 추가. `new TileMoveSlotManager()` → `new TileMoveSlotManager(_slotForwardRatio, _slotSideRatio)`.

**핵심 설계 결정**:
- TileMoveSlotManager는 순수 C# 클래스(MonoBehaviour 아님) → [SerializeField] 직접 불가. GameBootstrapper(MonoBehaviour)에 SerializeField 배치 후 생성자로 값 전달.
- 기본값 0.30f 유지 → 기존 동작과 동일, 행동 변화 없음.
- 런타임 중 Inspector 수정은 적용 안 됨(생성 시 1회 주입). 플레이 시작 전 설정 필요.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-11/10_49_slot-ratio-inspector/`

---

### UI 종족/팀 초상화 및 생산 연동 시스템 정비 (2026-04-30) ✅ 구현 완료

**수정 파일**: `Presentation/UI/ProductionPanelUI.cs`, `Presentation/UI/BuildingPlacementUI.cs`, `Bootstrap/GameBootstrapper.cs`

**변경 내용**:
- **UI Skinning 로직 제거**: 프로젝트 방향에 따라 배경 색상 변경 등 비주얼 스킨 필드 및 코드를 모두 제거하여 인스펙터를 단순화.
- **데이터 기반 바인딩**: 종족별 데이터 리스트(`UnitPortraitEntry`, `BuildingPortraitEntry`)를 사용하여 버튼에 `UnitType`/`BuildingType`과 스프라이트를 동시 바인딩.
- **생산 타입 동기화 보장**: UI에서 보이는 초상화와 실제 생성되는 프리팹이 1:1로 일치하도록 버튼 클릭 시 리스트에 매핑된 타입을 정확히 전달.
- **비용 텍스트 동적 갱신**: `UnitProductionStats` 및 `BuildingStats`를 참조하여 종족/유닛별 골드 비용을 UI에 실시간 반영.
- **Initialize 정리**: `ProductionPanelUI.Initialize`에서 더 이상 사용하지 않는 `GameConfig` 파라미터 제거.

**핵심 설계 결정**:
- **데이터 우선 원칙**: 복잡한 스킨 시스템보다 플레이어가 선택한 종족의 데이터가 정확히 UI와 게임 플레이(생산)에 반영되는 정합성을 최우선으로 함.
- **인스펙터 최적화**: 불필요한 설정 칸을 줄여 데이터 입력 실수를 방지하고, 향후 종족 추가 시 데이터 리스트만 채우면 되도록 확장성 확보.

---

### Phase 2 후방 스냅 수정 — 7차 개선 Step 4 (2026-04-29) ✅ 구현 완료

**수정 파일**: `Presentation/Unit/UnitView.cs` (Phase 2 영역, 라인 1438~1545)

**변경 1 — Phase 2 forward 타일 우선 선택 (Step 4-A)**:
- `nearestTile == _unitData.Position`(= T0)인 경우, T0의 6방향 인접 타일을 순회하여 forward neighbor(`HexCoord.Distance(neighbor, finalTarget) < currentDist`) 중 현재 위치(domainPos)에서 2D 거리(dx²+dz²)가 가장 가까운 타일을 nearestTile로 교체.
- API: `HexDirectionExtensions.Count` + `((HexDirection)i).Neighbor(origin)` 패턴 사용 (HexMetrics.GetNeighbors 부재).
- walkability 체크 생략 — Phase 0 A* 재계산이 실제 경로를 다시 잡음.
- 폴백: 앞쪽 후보가 없으면 T0 그대로 유지(`bestForward != nearestTile` 조건으로만 교체).

**변경 2 — Phase 2 Lerp 중 적 감지 (Step 4-B)**:
- Phase 2 Lerp while 루프(`Vector3.Lerp(snapStart, tileCenter, t)` 직후)에 적 감지 블록 추가.
- 조건: `HasEnemyInDetectRange && !HasEnemyInRange && snapEnemyIsForward`(Step 2 forward filter 동일 적용).
- forward 판정: `HexCoord.Distance(snapDetectCoord, finalTarget) <= HexCoord.Distance(snapCurrentTile, finalTarget)` (≤ 조건 — 동거리 적은 앞쪽 간주).
- forward 적 감지 시 break → 루프 직후 `transform.position = tileCenter` 강제 스냅 → ProcessStep 정상 실행 → 외부 while로 복귀해 A* 재계산 + Phase 0 첫 감지 체크에서 즉시 Phase 1 재진입.

**핵심 설계 결정**:
- **HexCoord 인접 탐색 패턴**: `HexMetrics.GetNeighbors`는 부재. `HexGrid.GetNeighbors`는 `List<HexTile>` 반환이라 부적합. 순수 좌표 인접 탐색에는 `HexDirectionExtensions.Count` + `((HexDirection)i).Neighbor(coord)`이 표준 패턴.
- **`<=` (동거리 forward 포함)**: Step 2/4-B 모두 동일. 동거리 적은 앞쪽으로 간주해야 잡을 수 있는 적을 놓치지 않음. `>`로 하면 잠재적 누락.
- **forward filter 일관성**: Phase 0 Lerp 중 감지(라인 811), Phase 0 스텝 완료 후 감지(라인 992), Phase 1 최초 타겟(라인 1042), Phase 1 타겟 사망 재선택, Phase 1 전투 종료 재선택, Phase 2 Lerp 중 감지(이번 변경) 모두 동일한 forward 판정 패턴 사용.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-27/01_17_phase2-backward-snap-fix/` (Step 4)

---

### Mesh Y Offset 제거 및 DirectionAngles 수정 (2026-04-29) ✅ 사용자 확인 완료

**수정 파일**: `Presentation/Unit/UnitView.cs`

**변경 내용**:
- `DirectionAngles` 수정: `{0,60,120,180,240,300}` → `{60,120,180,240,300,0}`
  - FlatTop 헥스에서 각 방향의 실제 Unity 월드 각도(atan2 기반)
  - NW(5)=0°: FlatTop NW(Q=0, R-1)의 월드 delta=(x:0, z:+1) → atan2(0,1)=0°
  - 기존 시스템: DirectionAngles + 메시자식Y(30°) = 올바른 월드 각도였음. 메시 자식 제거 후 DirectionAngles가 직접 올바른 값을 담아야 함
- `_meshYOffset` SerializeField 제거
- `CalculateAttackAngle()` 반환에서 `- _meshYOffset` 제거

**핵심 설계 결정**:
- **DirectionAngles 부호 주의**: 메시 자식 Y를 제거할 때 DirectionAngles를 -30°가 아닌 +30° 조정해야 함. 기존값{30,...,330}+30={60,...,0}이 정답. -30°로 적용하면({0,...,300}) 이동 방향과 시각 방향이 60° 어긋남.
- **CalculateAttackAngle 독립성**: DirectionAngles를 사용하지 않고 Atan2 직접 계산 → 이동 방향 변경에 무관. 메시 Y=0이므로 추가 보정값 불필요.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-28/14_30_mesh-offset-cleanup/`

---

### 근접 유닛 뭉침 개선 — 18슬롯 + 슬롯도달후 직진 (2026-04-27) ✅ 구현 완료

**수정 파일**:
- `Application/Services/AttackPositionManager.cs` — 6슬롯 → 18슬롯 재작성. 인접 타일 N개당 (중심 + 좌측경계 + 우측경계) 3위치 생성. 좌/우 경계는 N>=2일 때만. 데이터 구조 `Dictionary<HexCoord, Dictionary<int, HexCoord>>` → `Dictionary<HexCoord, Dictionary<int, Vector3>>` (도메인 좌표 보관). 점유 카운트는 Vector3.Distance < 0.01f로 동등 비교. `_candidateBuffer` 재사용 + `AddCandidateUnique`로 중복 위치 방지.
- `Presentation/Unit/UnitView.cs` (Phase 1 루프 ~Line 1245) — moveTarget 결정에 `reachedSlot` 분기 추가. `Vector2.Distance` 기준 0.15f 이내면 `enemyViewPos`로 전환.

**버그 원인**: 슬롯 위치(0.866f 또는 0.75f)가 전투 사거리(유닛 0.3f / 건물 0.5f)보다 멀어, `moveTarget = _currentAttackPos`로 유지하면 슬롯 도달 시 `dist < 0.01f`에 걸려 유닛이 그대로 멈춤 → `HasEnemyInRange` FALSE → 전투 시작 안 됨.

**핵심 설계 결정**:
- **도메인 좌표로 점유 추적**: `HexMetrics.HexToWorld` 기반 도메인 좌표를 `_assignments`에 보관. 뷰 좌표는 팀별 ViewConverter로 회전될 수 있어 카운트 기준이 흔들리기 때문. `unitViewPos`와의 거리 비교 시점에만 `ToView`로 변환.
- **AddCandidateUnique 중복 방지**: 인접 타일이 6개 미만(맵 가장자리)일 때 같은 좌/우 경계 위치가 두 번 계산될 수 있음. `Vector3.Distance < SamePositionEpsilon(0.01f)`로 중복 흡수.
- **단방향 전환 (reachedSlot)**: 한 번 슬롯 도달 후 `enemyViewPos`로 전환되면 같은 Phase 1 루프 내에서 슬롯으로 되돌아가지 않음 → 진동 방지.
- **Y축 무시 거리 판정**: `Vector2.Distance(transform.position.xz, _currentAttackPos.xz)` — UnitYOffset 차이로 인한 도달 판정 오차 제거.
- **MaxUnitsPerSlot=2 fallback 유지**: 36개 유닛 동시 공격까지 분산 가능. 초과해도 가장 적은 위치로 fallback.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/18_30_melee-spread/`

---

### 타일 소유권 실시간 감지 시스템 구현 (2026-04-26) ✅ 구현 완료

**신규 파일**: `Application/Services/TileOwnershipService.cs` — Pull 모델. 매 프레임 모든 살아있는 유닛의 viewPos를 받아 ViewConverter.FromView → HexMetrics.WorldToHex로 헥스 좌표 역산 후 `Dictionary<HexCoord, HashSet<TeamId>>`에 누적. 한 팀만 있는 타일에 한해 `_grid.GetOwner != claimingTeam`일 때만 SetOwner + OnTileOwnerChanged 발행. HashSet 풀(`Queue<HashSet<TeamId>>`)로 GC 최소화.

**수정 파일**:
- `Domain/Hex/HexGrid.cs` — `GetOwner(HexCoord)` 신규. `_tiles.TryGetValue` → `tile.Owner` 또는 Neutral.
- `Bootstrap/GameBootstrapper.cs` — `using Hexiege.Application.Services;`, `_tileOwnership` 필드, `CreateUseCases()`의 `_unitCombat` 직후 인스턴스 생성, `Update()`에 가드 `(!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer)` 후 `Tick()`.

**핵심 설계 결정**:
- **HexCoord.IsInvalid 부재** → 그리드 경계 검증은 `_grid.HasTile(tile)`로 대체 (TileOccupancyManager의 `IsInvalid`는 (0,0) 약속 기반의 사설 헬퍼이므로 점령 판정에는 부적합 — (0,0)이 일반 타일).
- **점령 규칙**: 한 팀만 있을 때만 갱신, 양 팀 동시면 유지(분쟁지), 비어있으면 유지(점령 영구화). `teams.Count != 1` 분기로 처리.
- **서버 가드**: 싱글(`!IsNetworkActive`) + Host(`IsNetworkServer`) 통과, 순수 Client 차단. 클라이언트는 `_grid.SetOwner` 직접 호출 시 도메인-뷰 불일치 위험 — 별도 동기화 경로(NetworkTileSync 등)로 결과만 수신.
- **이벤트 중복 발행 방지**: `_grid.GetOwner(tile) == claimingTeam`이면 SetOwner/OnNext 모두 생략. 같은 팀이 계속 차지 중인 타일에서 매 프레임 이벤트가 발행되어 HexTileView가 불필요하게 반응하는 것 차단.
- **Application/Services 경로**: 메모리에는 TileOccupancyManager가 Application 직속으로 적혀 있었으나 실제로는 Application/Services에 있음 → 신규 파일도 같은 폴더에 생성.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/17_00_tile-ownership-detection/`

---

### 근접 유닛 뒷무빙 수정 5차 개선 (2026-04-26) ✅ 사용자 확인 완료

**수정 파일**: `Presentation/Unit/UnitView.cs` (3곳 수정)

**Step 1 — Phase 1 타겟 사망 시 즉시 재선택**: Phase 1 이동 중 `GetUnitWorldPosition == Vector3.zero`(타겟 파괴) 감지 시 무조건 Phase 2 진입 대신, `_combatUseCase.HasEnemyInDetectRange` + `FindNearestEnemyInDetectRange`로 다음 적 재선택 → 있으면 `continue`(Phase 1 유지), 없으면 `break`(Phase 2 진입).

**Step 2 — 전투 루프 종료 후 다음 타겟 선택**: 전투 종료(`break`) 직후 `HasEnemyInDetectRange` 재확인 → 적 있으면 `FindNearestEnemyInDetectRange`로 타겟 전환 후 `continue`(Phase 1 재개), 없으면 Phase 2 진입.

**Step 3 — Phase 2 후방 스냅 방지**: Phase 2 진입 시 `HexCoord.Distance(nearestTile, finalTarget) > HexCoord.Distance(_unitData.Position, finalTarget)`이면 `nearestTile = _unitData.Position` 유지(후방 스냅 차단). `nearestTile == _unitData.Position`이면 `RegisterOccupancyMove` 생략(점유 누수 방지).

**핵심 설계 결정**:
- **뒷무빙 근본 원인**: Phase 1 타겟 사망 → 무조건 Phase 2 진입 → 현재 물리 위치에서 가장 가까운 타일(=후방일 수 있음)로 스냅.
- **거리 비교 기준**: 월드 거리(float) 대신 `HexCoord.Distance`(도메인 정수 거리) 사용 → 팀 관점(ViewConverter) 무관, 부동소수점 오차 없음.
- **4차 개선 RegisterOccupancyMove 연동**: `nearestTile == _unitData.Position`이면 실제 이동 없음 → `RegisterOccupancyMove` 생략으로 TO+1 중복 방지. FROM-1은 이후 `ProcessStep`에서만 발생하므로 점유 정합성 유지.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/15_00_phase1-target-reselect/`

---

### 패스파인딩 4차 개선 — FROM 타일 점유 해제 타이밍 수정 (2026-04-26) ✅ 실기 완료

**수정 파일**:
- `Application/Services/TileOccupancyManager.cs` — `ReserveOccupancy(HexCoord tile, float unitSize)` public 메서드 추가. `Increase(tile, unitSize)` 래퍼. IsInvalid 가드 포함.
- `Application/UseCases/UnitMovementUseCase.cs` — `RegisterOccupancyMove`: `OnUnitMoved(from, to)` → `ReserveOccupancy(to, size)` 변경(TO+1만 예약, from 파라미터 유지). `ProcessStep`: 첫 줄에 `from != to && _occupancyManager != null` 조건으로 `OnUnitRemoved(from, GetOccupancySize(unit.Type))` 추가(Lerp 완료 후 FROM 해제).

**핵심 설계 결정**:
- **FROM 해제 타이밍 분리**: Lerp 시작 전 RegisterOccupancyMove → TO+1만. Lerp 완료 후 ProcessStep → FROM-1. 유닛이 물리적으로 FROM에 있는 동안 FROM 점유가 유지되어 다른 유닛의 잘못된 진입 차단.
- **부가 수정**: death-during-Lerp 이중 해제 버그 동시 해결. FROM은 ProcessStep에서만 감소하므로 사망 시 OnEntityDied → OnUnitRemoved(FROM) 1회만 적용.
- **Phase 2 from==to**: `from != to` 조건으로 Phase 2 스냅(from==to) 시 OnUnitRemoved 미호출. 올바른 동작.
- **스폰(from=default)**: OnUnitRemoved 내부 IsInvalid 체크가 default coord를 skip. 안전.

**실기 결과**:
- 권총병(원거리) 유닛 분산 개선 확인 (PASS)
- 근접 유닛(EmberSpirit) 뭉침은 구조적 한계 — 별도 작업 필요
- 뒷무빙 현상(Phase 1 타겟 재선택 미비) 발견 → `_Tasks/2026-04-26/15_00_phase1-target-reselect/`

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/11_00_occupancy-from-fix/`

---

### 패스파인딩 3차 개선 — 뭉침/팅김 해결 (2026-04-25) ✅ 구현 완료

**수정 파일**:
- `Application/Services/TileOccupancyManager.cs` — `FindAvailableTile(preferred, size, grid, destination)` 오버로드 추가. forward 필터 BFS: `Distance(candidate, destination) <= Distance(preferred, destination) + 1` 조건 충족 타일만 반환. fallback으로 필터 없이 재BFS. 기존 단일 파라미터 오버로드는 default destination 위임으로 유지.
- `Application/UseCases/UnitMovementUseCase.cs` — `ProcessStep`에서 `_occupancyManager.OnUnitMoved` 호출 제거(도메인 로직만 담당). `RegisterOccupancyMove(from, to, type)` 신규 추가(Lerp 시작 직전 호출용). `ReleaseOccupancy(tile, type)` 신규 추가(중단 경로 누수 방지). `FindAvailableTile(preferred, size, destination)` 오버로드 추가.
- `Presentation/Unit/UnitView.cs` — `_pendingOccupancyTile` 필드 추가(default = 미등록). `ReleaseOccupancyIfPending()` 헬퍼 추가. Phase 0 루프 진입 전 `prevActualTile = _unitData.Position` 초기화. for 루프 내 `from = prevActualTile`로 변경(기존 `path[i-1]` 폐기). FindAvailableTile에 `finalTarget` 전달. Lerp 시작 직전 RegisterOccupancyMove 호출. 정상 도착 시 `prevActualTile = to; _pendingOccupancyTile = default;` 갱신. 우회 발생(`actualTo != to`) 시 `detouredNeedsRepath = true` + for break → 외부 while에서 RequestMove 재호출 후 continue. interruptedByDetect/StopMovement/사망 핸들러에 `ReleaseOccupancyIfPending()` 추가. Phase 2 스냅 후 `RegisterOccupancyMove(_unitData.Position, nearestTile, type)` 명시 호출.

**핵심 설계 결정**:
- **점유 갱신 타이밍**: ProcessStep(Lerp 후) → Lerp 시작 직전. 같은 프레임 내 다른 유닛이 즉시 "이 타일 차 있음" 인식 → Race Condition 해결.
- **prevActualTile 추적**: 우회 발생 시에도 `from`이 항상 실제 이전 도착 타일을 가리켜 OnUnitMoved의 from 감소가 올바른 타일에 적용됨.
- **우회 시 즉시 re-path**: 원래 path는 actualTo와 무관하므로 그대로 이어가면 측면/후방 지그재그(팅김) 발생. for break + RequestMove로 현재 위치 기준 새 플로우 필드 경로 받음.
- **forward 필터 +1 여유**: 헥스 그리드 특성상 측면 타일이 같은 거리이거나 +1이 될 수 있어 너무 엄격하면 모든 측면 차단됨. fallback BFS로 극단 상황도 처리.
- **_pendingOccupancyTile = default 약속**: HexCoord(0,0)이 일반 타일일 수 있지만 기존 `TileOccupancyManager.IsInvalid` 약속과 동일하게 "미등록" 의미로만 사용.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-25/10_05_pathfinding-improvement/`

---

### 유닛/건물 스탯 ScriptableObject 전환 (2026-04-25) ✅ 구현 완료

**신규 파일**:
- `Infrastructure/Config/UnitStatsConfig.cs` — `UnitStatEntry` 구조체(전투+생산 스탯 통합) + `UnitStatsConfig : ScriptableObject`
- `Infrastructure/Config/BuildingStatsConfig.cs` — `BuildingTypeEntry` 구조체(B방식: 건물타입별 3종족 값 묶음) + `BuildingStatsConfig : ScriptableObject`
- `Editor/SetupUnitStatsConfig.cs` — 메뉴: `Hexiege/Setup/UnitStatsConfig 생성`. 9종 유닛 기본값 자동 주입.
- `Editor/SetupBuildingStatsConfig.cs` — 메뉴: `Hexiege/Setup/BuildingStatsConfig 생성`. Castle/Barracks/MiningPost 기본값 자동 주입.

**수정 파일**:
- `Domain/Unit/UnitStats.cs` — switch 표현식 → `Dictionary<UnitType, StatValues>`. `Initialize(IReadOnlyDictionary<UnitType, StatValues>)` 추가. miss → 폴백 반환.
- `Domain/Unit/UnitProductionStats.cs` — 동일 패턴. `Dictionary<UnitType, ProductionValues>`, `Initialize()` 추가.
- `Domain/Building/BuildingStats.cs` — switch 표현식 → `Dictionary<(BuildingType, RaceId), StatValues>`, `Initialize()` 추가. `GetGoldCost(type, race)`, `GetAttackPower(type, race)` 신규 메서드.
- `Bootstrap/GameBootstrapper.cs` — `[SerializeField] _unitStatsConfig`, `[SerializeField] _buildingStatsConfig` 추가. `InitializeUnitStatsFromConfig()`, `InitializeBuildingStatsFromConfig()` 메서드 추가.
- `Presentation/UI/BuildingPlacementUI.cs` — `GetBuildingCost()` → `BuildingStats.GetGoldCost(type, race)` 사용으로 변경.

**에셋 경로**: `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, `Assets/_Project/Resources/Config/BuildingStatsConfig.asset`

**핵심 설계 결정**:
- Domain 순수성 유지: Domain 내부 C# 구조체(`StatValues`, `ProductionValues`)를 직접 정의. Infrastructure → Domain 의존 없음.
- GameBootstrapper가 SO → Domain 구조체 변환 담당 (단일 책임).
- Play Mode 중 SO 수정 → Dictionary는 Start() 복사본이므로 다음 Play Mode 진입까지 미반영 (의도된 동작).
- `GameConfig.BarracksCost/MiningPostCost` 필드는 유지 (참조 제거 최소화).

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-25/01_35_unit-stats-scriptableobject/`

---

### 싱글플레이 AI 종족 랜덤 결정 (2026-04-24) ✅ 실기 완료

**수정 파일**:
- `Bootstrap/GameBootstrapper.cs` — 283번째 줄 `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` → `Enum.GetValues` + `Random.Range`로 교체
- `Presentation/UI/ViewModels/BattleViewModel.cs` — `LoadSingleplayScene()`에서 중복된 `GameRaceContext.Set()` 호출 및 주석 제거

**핵심 설계 결정**:
- `(RaceId[])System.Enum.GetValues(typeof(RaceId))` 패턴 — 새 종족 추가 시 자동으로 랜덤 풀에 포함
- `GameRaceContext` 설정 책임은 `GameBootstrapper.cs` 단독 (BattleViewModel 이중 설정 제거)
- `LoadMap()` 이전 설정 순서 유지

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/23_06_random-opponent-race/`

---

### 다중 히트 데미지 구현 (2026-04-24) ✅ 실기 완료

**수정 파일**:
- `Domain/Unit/UnitStats.cs` — `GetHitFrameTime()` 제거 → `GetHitFrameTimes()` 추가 (반환형 `float[]`), LionKnight AttackCooldown 2.33f → 3.0f 수정
- `Domain/Unit/UnitData.cs` — `HitFrameTime: float` → `HitFrameTimes: float[]` 교체 (생성자 2개 모두)
- `Application/UseCases/UnitCombatUseCase.cs` — `PendingHit` struct + `_pendingHits` List + `TickPendingHits(float dt)` 추가. `TryAttack()`에서 각 히트 프레임마다 PendingHit enqueue, 쿨다운 리셋은 TryAttack에서 1회만.
- `Infrastructure/Network/NetworkCombatController.cs` — `ExecuteAttack()`에서 `HitFrameTimes` foreach로 `DelayedAttackDamage` 코루틴 N개 실행
- `Bootstrap/GameBootstrapper.cs` — `Update()`에 `_unitCombat.TickPendingHits(Time.deltaTime)` 추가

**핵심 설계 결정**:
- 쿨다운은 공격 사이클 시작 시 1회만 리셋 — 히트 횟수와 무관
- 싱글플레이: MonoBehaviour 아님 → 코루틴 불가 → `_pendingHits` 타이머 리스트 방식 (TickCooldowns와 동일 패턴)
- 멀티플레이: `DelayedAttackDamage` 코루틴을 히트 수만큼 병렬 실행
- 타겟 사망 시 잔여 히트 자동 취소 — `ApplyAttackDamage` 내 `IsAlive` 체크로 처리
- `ApplyAttackDamage()`에서 쿨다운 리셋 제거 (다중 히트 시 마지막 히트에서 재리셋 방지)

**다중 히트 유닛 타이밍 (StatsReference.md 기준, 30fps)**:
- FlameSpirit (6히트, 쿨다운 3.0s): 0.667 / 1.167 / 1.433 / 1.667 / 1.933 / 2.100s
- LionKnight (2히트, 쿨다운 3.0s): 0.733 / 1.267s

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/16_31_multi-hit-damage/`

---

### 근접유닛 추적 중 회전 개선 (2026-04-24) ✅ 실기 완료

**수정 파일**:
- `Presentation/Unit/UnitView.cs` — Phase 1 직선 이동 블록 (850~866 라인) 회전 로직 추가

**문제**: Phase 1(월드 좌표 직선 추적) 중 `transform.rotation` 업데이트 없음 → 이전 타일 이동 방향 회전 고정.

**수정**: `if (dist > 0.01f)` 블록 내 이동 전에 `CalculateAttackAngle(enemyViewPos)` + `Quaternion.RotateTowards(CombatRotationSpeed * deltaTime)` 추가.
전투 중 타겟 추적 회전(`Update()`)과 동일한 패턴 사용.

**멀티플레이**: `MoveAlongPath` 코루틴 가드(`NetworkContext.IsNetworkActive && !IsNetworkServer`)로 서버만 실행 → NetworkTransform이 클라이언트에 보간 전달.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/15_45_melee-pursuit-rotation/`

---

### 랠리포인트 Client 무시 버그 수정 (2026-04-19) ✅ 실기 완료

**수정 파일**:
- `Infrastructure/Network/NetworkProductionController.cs` — `SetRallyPointServerRpc` 신규 추가 (약 695~738행)
- `Presentation/UI/ProductionPanelUI.cs` — `CompleteRallyPointSetting()` 네트워크 분기 추가

**버그**: 멀티플레이 Client(Red팀)에서 랠리포인트를 설정해도 생산된 유닛이 랠리포인트를 무시하고 이동.

**원인**: `CompleteRallyPointSetting()`이 `_production.SetRallyPoint()`를 직접 호출 → 클라이언트 로컬 `ProductionState`만 갱신. 서버의 `state.RallyPoint`는 null → `SpawnUnitClientRpc`에 `hasRally=false` 전송.

**수정**:
- `SetRallyPointServerRpc(barracksId, q, r, teamIndex)` 추가 — 기존 ServerRpc 패턴 그대로 (팀 소유권 검증 → `production.SetRallyPoint()`)
- `CompleteRallyPointSetting()`에 네트워크 분기 추가:
  - 네트워크 모드: `SetRallyPointServerRpc` 호출(서버 반영) + 로컬 `_production.SetRallyPoint()`(마커 표시)
  - 싱글/Host: 기존대로 직접 호출
- ClientRpc 불필요: 서버 생산 완료 시 `state.RallyPoint`를 읽어 `SpawnUnitClientRpc`로 전달되므로 서버 상태만 정확하면 충분

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/18_54_rally-point-ignored/`

---

### 생산 슬롯 깜빡임 버그 수정 (2026-04-19) ✅ 싱글 실기 완료

**수정 파일**: `Application/UseCases/UnitProductionUseCase.cs` — `ToggleAutoProduction()` 284~288행

**버그**: 큐가 완전히 비어있을 때 자동 생산 타입을 등록하면 1프레임 동안 슬롯1에 표시됐다가 슬롯0으로 이동하는 깜빡임 발생.

**원인**: `canShow = CurrentProducing.HasValue && ChargedPendingCount() < 2` 조건에서 큐가 비어있으면 `HasValue=false` → `canShow=false` → 아이템이 `PendingQueue[0]`(슬롯1)에 미차감 추가. 다음 Tick의 `TryStartNext`가 슬롯0으로 올리기 때문에 1프레임 지연 발생.

**수정**: `PendingQueue.Add + AutoTypes.Add + NormalizeAutoCycleIndex` 이후, `!state.CurrentProducing.HasValue`이면 즉시 `TryStartNext(state)` 호출 후 Early Return. TryStartNext 내부에서 이벤트 발행 처리.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/17_49_production-slot-flicker/`

---

### 타겟 고정(Target Lock) 데미지 불일치 버그 수정 (2026-04-18) ✅ 멀티 실기 완료

**수정 파일**: `Infrastructure/Network/NetworkCombatController.cs` — `TickCombat()` 253~297행

**버그**: 유닛 A가 B를 공격 중 더 가까운 C가 접근 시, 애니메이션은 B를 바라보지만 데미지가 C에게 적용되는 문제.

**원인**: `IsCurrentTargetStillValid(B) = true` → `_unitCombatTargets` 미변경(애니메이션 B 유지) 했으나, `ExecuteAttack`은 항상 `TryFindTarget`이 반환한 `targetId`(C)를 사용.

**수정**: `damageTargetId` / `damageTargetIsUnit` 지역 변수 추가.
- `IsCurrentTargetStillValid = true` → `else` 분기: `damageTargetId = prev.targetId` (기존 타겟 B 유지)
- `IsCurrentTargetStillValid = false` → 기존 흐름 유지 (새 타겟 C로 교체 + RPC 전송)
- `ExecuteAttack(unit, damageTargetId, damageTargetIsUnit)` 호출

**교훈**: Target Lock에서 애니메이션 타겟(`_unitCombatTargets`)과 데미지 타겟(`targetId`)은 항상 일치해야 함. `IsCurrentTargetStillValid` 가드로 애니메이션을 유지한다면, 데미지도 같은 타겟에게 적용해야 함.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-17/22_29_target-lock-damage-bug/`

---

### 피격 시 부유 HP 텍스트 (2026-04-12~13, 2026-04-17 World Space 전환) ✅ 싱글/멀티 실기 완료

**신규 파일**:
- `Presentation/UI/Common/FloatingHpText.cs` — 단일 부유 텍스트. TextMeshPro(3D World Space). DOTween Sequence(LocalMoveY OutCubic + TMP DOFade 동시, duration초). Play(text, worldPosition, scale=1f, color). OnComplete → SetActive(false) + 풀 반환 콜백. OnDestroy → Kill.
- `Presentation/UI/FloatingHpTextSpawner.cs` — GameEvents.OnEntityDamaged 구독(AddTo). Queue<FloatingHpText> 풀 10개 사전 생성. Initialize(positionProvider, container, prefab) — null 체크 포함. 팀별 색상: `[SerializeField] Color _blueTeamColor` / `_redTeamColor`. evt.Entity.Team switch → Play()에 전달.
- `Prefabs/UI/FloatingHpText.prefab` — SetupFloatingHpText 에디터 스크립트로 자동 생성.
- `Editor/SetupFloatingHpText.cs` — 프리팹 생성 + 씬 배치 + GameBootstrapper 슬롯 자동 연결. 메뉴: `Hexiege/Setup/FloatingHpText 설정`

**변경 파일**:
- `Bootstrap/GameBootstrapper.cs` — `_floatingHpTextSpawner`, `_floatingHpTextPrefab`, `_floatingTextContainer(Transform)` SerializedField 추가. `_positionProvider` 로컬→필드 승격. `LoadMap()`에서 Initialize 호출.
- `Infrastructure/Network/NetworkHealthSync.cs` — `SyncUnitHealth`/`SyncBuildingHealth`에서 TakeDamage 후 `GameEvents.OnEntityDamaged.OnNext()` 재발행 (클라이언트에서 FloatingHpTextSpawner가 반응하도록).

**Inspector 설정값 (FloatingHpText 프리팹)**:
- `Rise Distance` (default=0.5f): 위로 이동 거리 (월드 단위, 픽셀 아님)
- `Duration` (default=1.2f): 전체 애니메이션 시간(초)
- **폰트 크기**: TMP 컴포넌트(자식 Text)에서 직접 수정
- **Material Preset**: 반드시 독립 .mat 파일(`Maplestory Light SDF FloatingHpText Material.mat`) 지정 — 폰트 에셋 내장 sub-asset 지정 시 Outline 등 편집이 .asset 파일 자체를 오염시킴

**Inspector 설정값 (FloatingHpTextSpawner)**:
- `Y Offset` (default=1.2f): 피격 오브젝트 머리 위 월드 Y 오프셋

**핵심 설계 결정**:
- **World Space TextMeshPro**: Screen Space Canvas 전환. 월드 좌표 직접 사용 → 좌표 변환 코드 없음.
- **scale = 1f 고정**: `orthoSize/referenceSize` 수식 폐기. 줌아웃 시 유닛은 작아지는데 텍스트만 커지는 비율 어긋남 방지. 텍스트가 다른 월드 오브젝트와 동일하게 줌 비례 동작.
- **빌보드 회전**: `Quaternion.LookRotation(-Camera.main.transform.forward, Camera.main.transform.up)`. 카메라 forward를 그대로 쓰면 텍스트가 카메라에 등을 보임.
- **좌우 반전 보정**: `LookRotation(-forward, up)`은 텍스트 로컬 X축을 -cameraRight로 만들어 텍스트가 좌우 반전됨. `localScale = new Vector3(-s, s, s)` (X 음수)로 한번 더 뒤집어 복원. TMP 3D 기본 머티리얼이 Cull Off(양면 렌더링)이므로 음수 스케일 정상 표시.
- **클라이언트 이벤트 재발행**: NetworkHealthSync에서 diff>0인 경우에만 → HP 이미 동기화 시 중복 표시 없음
- **팀 색상 (기본값)**: Blue=연두(120,230,80), Red=노랑(255,220,30) — Inspector 조정 가능

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-12/18_03_floating-hp-text/` (초기), `Assets/_Project/Docs/_Tasks/2026-04-13/17_50_floating-text-worldspace/` (World Space 전환)

---

### 유닛/건물 스탯 적용 + UI 골드 비용 표기 (2026-04-12~13) ✅ 싱글/멀티 실기 완료

**변경 파일**:
- `Domain/Unit/UnitStats.cs` — Pistoleer MoveSpeed 1.0→0.5, Spirit/Transcendence 6종 HP/ATK 확정값 적용
- `Domain/Unit/UnitProductionStats.cs` — Spirit/Transcendence 6종 생산시간/비용 확정값 적용
- `Domain/Building/BuildingStats.cs` — `GetMaxHp(type, RaceId race)` 오버로드 추가. Transcendence: Castle=200/Barracks=50/MiningPost=40, 나머지: 100/30/20. 단일 파라미터 버전은 `RaceId.Human`으로 위임.
- `Application/UseCases/BuildingPlacementUseCase.cs` — `PlaceBuilding`/`PlaceMiningPost`/`PlaceMiningPostDirect`/`PlaceBuildingWithId`/`PlaceBuildingInternal`에 `RaceId race = RaceId.Human` 파라미터 추가. Application 레이어 위반 없음 (GameRaceContext 직접 참조 없음).
- `Bootstrap/GameBootstrapper.cs` — Castle/mine 배치에 `GameRaceContext.BlueRace`/`RedRace` 전달.
- `Infrastructure/Network/NetworkBuildingController.cs` — ServerRpc/ClientRpc에 race 전달.
- `Presentation/UI/BuildingPlacementUI.cs` — HP 텍스트 필드 제거. `_barracksCostText`/`_miningPostCostText` 추가. 골드 숫자만 표시(G 없음).
- `Presentation/UI/ProductionPanelUI.cs` — `_slot1/2/3CostText` 추가. Spirit 슬롯 순서 확정(EmberSpirit→FlameSpirit→InfernoSpirit). 골드 숫자만 표시.
- `Editor/SetupStatCostTexts.cs` (신규) — 기존 GoldText 오브젝트를 SerializedField에 자동 연결. 메뉴: `Hexiege/Setup/스탯 비용 텍스트 연결`

**핵심 설계 결정**:
- Transcendence 건물 HP는 RaceId 파라미터로 분기 — UnitCombatUseCase/BuildingData에 Race 필드 추가하지 않음
- Application 레이어에 GameRaceContext 참조 없음 (호출자에서 race 파라미터로 전달)
- UI 골드 비용: 숫자만, "G" 없음

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-12/06_42_stats-apply/`

---

### 건물/유닛 초상화 종족+팀 기반 표시 (2026-04-12) ✅ 실기 완료

**변경 파일**:
- `Presentation/UI/BuildingPlacementUI.cs` — `BuildingPortraitSet` → `BuildingRacePortraitSet`(barracks+miningPost 필드)으로 교체, Inspector 팀×종족 6세트 필드 추가, `UpdateButtonPortraits()`에 `GameRaceContext` 조회 추가, `GetBuildingPortraitSet()` 신규 메서드
- `Presentation/UI/ProductionPanelUI.cs` — `BindButtonUnitTypes()` 슬롯 순서 변경 (Spirit: EmberSpirit→FlameSpirit→InfernoSpirit / Transcendence: FoxMagician→BearGuard→LionKnight)

**핵심 설계 결정**:
- `BuildingRacePortraitSet` 필드명은 BuildingType(barracks/miningPost) 기준 — 종족별 외형명(SummoningAltar 등) 아님. UpdateButtonPortraits에서 종족 무관하게 `set.barracks`로 통일 접근 가능
- `ProductionPanelUI.GetPortraitSet()` 패턴과 동일하게 팀×종족 switch 6분기
- `GameRaceContext`(Infrastructure 정적 홀더)는 Presentation에서 참조 허용 — 레이어 위반 없음

**Inspector 연결 확정 슬롯 순서**:
- Spirit: slot1=EmberSpirit, slot2=FlameSpirit, slot3=InfernoSpirit
- Transcendence: slot1=FoxMagician, slot2=BearGuard, slot3=LionKnight

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/building-portrait-race-support/`

### 원거리 유닛 공격 중 회전 추적 + 폴리싱 (2026-04-11~12) ✅ 실기 완료

**변경 파일**:
- `Presentation/Unit/UnitView.cs` — `_combatTargetTransform` Transform 참조 저장 + `Update()` RotateTowards(270°/s) + 방어적 백업 ID 필드(`_combatTargetId`, `_combatTargetIsUnit`) + `ChangeTarget()` 즉시 스냅 제거
- `Application/UseCases/UnitCombatUseCase.cs` — `IsCurrentTargetStillValid(attacker, targetId, targetIsUnit)` public 메서드 추가 (내부적으로 `FindTargetById` + `IsTargetInRange` 조합)
- `Infrastructure/Network/NetworkCombatController.cs` — `TickCombat` 타겟 교체 2곳에 `IsCurrentTargetStillValid` 가드 추가

**핵심 설계 결정**:
- Transform 참조 직접 저장 → 팩토리 딕셔너리 매 프레임 조회 없음
- 서버에서만 rotation 갱신 — 클라이언트 가드: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`
- RotateTowards(270°/s): DORotate 폐기 이유(이중 보간)와 달리 서버가 직접 값을 변경하므로 NetworkTransform 딜레이만 발생
- `StartCombatAnimation()` 즉시 스냅 유지, `ChangeTarget()` 즉시 스냅 제거 — Update()가 자연스럽게 전환
- 타겟 고착성: 현재 타겟 생존+사거리 내이면 더 가까운 새 유닛이 진입해도 교체 안 함

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/ranged-unit-rotation-tracking/` (TC MULTI-001~007 전체 PASS)

### 근접 공격 거리 다듬기 (2026-04-11) ✅ 실기 완료

**변경 파일**:
- `Application/UseCases/UnitCombatUseCase.cs` — `MeleeContactDist = 0.3f`, `BuildingDetectionRadius = 0.2f` 상수 추가. `FindFirstEnemyTarget`에서 `unitMaxDist`/`buildingMaxDist` 분리. `IsTargetInRange`에서 동일 분기 적용.

**핵심 설계 결정**:
- 근접(range < 1.0) vs 유닛: 0.35f / vs 건물: 0.55f
- 원거리(range ≥ 1.0): 기존 `AttackRange * TileHeight + Epsilon` 유지
- `isMelee = attacker.AttackRange < 1.0f` 분기로 완전 보호

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/melee-attack-distance/`

### UnitType 개편 + 근접 사거리 시스템 (2026-04-10~11) ✅ 싱글/멀티 실기 완료

**변경 파일**:
- `Domain/Unit/UnitType.cs` — Pistoleer=0~LionKnight=8, 9종 유닛 독립 enum
- `Domain/Unit/UnitStats.cs` — Spirit/Transcendence 6종 스탯 추가 (HP/ATK 미정, Range/Cooldown/HitFrameTime 확정)
- `Domain/Hex/HexPathfinder.cs` — `FindPathToNeighbor()` 추가: goal의 인접 walkable 타일 중 start에서 가장 가까운 타일까지 경로 반환
- `Infrastructure/Factories/UnitFactory.cs` — `UnitTeamPrefabSet` → `List<UnitPrefabEntry>(type, blue, red)` 구조로 변경
- `Application/UseCases/UnitMovementUseCase.cs` — RequestMove에 non-walkable 목표 처리 추가, `path.Count >= 1` 조건
- `Presentation/Unit/UnitView.cs` — 마지막 non-walkable 타일: ProcessStep 생략 + ClaimedTile 설정 생략
- `Presentation/UI/ProductionPanelUI.cs` — 종족별 UnitType 동적 바인딩 (`BindButtonUnitTypes`), 6세트 초상화 필드
- `Editor/SetupUnitFactoryPrefabs.cs` — List<UnitPrefabEntry> 구조에 맞게 재작성

**핵심 설계 결정**:
- 근접 유닛(range=0.5): maxDist = 0.483f 유지 + 경로에 Castle 타일 추가 → Lerp 이동 연장으로 접근
- **ClaimedTile non-walkable 타일 예외**: 마지막 타일이 non-walkable이면 ClaimedTile 설정 안 함 — 설정 시 공격 루프 내내 Castle이 blocked로 유지되어 후속 유닛 접근 차단
- `FindPathToNeighbor` start==bestCandidate → count=1 반환 → `>= 1` 조건으로 Castle 타일 추가 보장

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-10/16_09_melee-unit-attack-range/`

### 중립 광산 오브젝트 표시 제어 (2026-04-08) ✅ 싱글 실기 완료

**변경 파일**:
- `Presentation/Grid/HexGridRenderer.cs` — `_goldMineObjects` List→Dictionary, `RenderGoldMines()` 초기 숨김, `HideGoldMine()`/`ShowGoldMine()` 추가, `SubscribeGoldMineEvents()` 추가
- `Application/UseCases/BuildingPlacementUseCase.cs` — `RemoveBuilding()` 내 MiningPost 파괴 시 타일 Owner Neutral 복원 + OnTileOwnerChanged 발행

**핵심 설계 결정**:
- 초기 숨김 판별: `RenderGoldMines()` 내 `tile.Owner != TeamId.Neutral` 조건 (PlaceMiningPostDirect 이후 호출 순서 보장)
- 이벤트 구독: `OnBuildingPlaced` → HideGoldMine / `OnEntityDied(MiningPost)` → ShowGoldMine
- 타일 소유권 복원: `RemoveBuilding()`에서 처리 — 싱글(UnitCombatUseCase)/멀티(NetworkCombatController) 모두 이 메서드를 거치므로 단일 수정으로 양쪽 커버

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-07/23_45_goldmine-hide/`

### 종족 인게임 적용 (2026-04-07) ✅ 싱글/멀티 실기 완료

**변경 파일**:
- `Infrastructure/Factories/UnitFactory.cs` — 종족별 6세트 프리팹(`_humanBlue/Red`, `_spiritBlue/Red`, `_transcendenceBlue/Red`), GameRaceContext 조회 후 switch 선택, 오브젝트명=`{prefab.name}_{id}`
- `Infrastructure/Factories/BuildingFactory.cs` — 동일 종족별 6세트 패턴, BuildingTeamPrefabSet에 `miningPost` 필드 추가
- `Bootstrap/GameBootstrapper.cs` — 싱글플레이 Start()에 `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` 추가
- `Editor/SetupUnitFactoryPrefabs.cs` (신규) — 유닛 18개 + 건물 12개 자동 프리팹 연결 에디터 메뉴

**핵심 설계 결정**:
- GameRaceContext(Infrastructure 정적 홀더)를 UnitFactory/BuildingFactory에서 직접 참조 — 레이어 위반 없음
- UnitData에 Race 필드 추가하지 않음 — 스폰 시점에 GameRaceContext에서 직접 조회
- MiningPost: BuildingTeamPrefabSet.miningPost 필드로 종족별 분기

**건물 종족 매핑 (확정)**:
| BuildingType | Human | Spirit | Transcendence |
|---|---|---|---|
| Castle | Building_Castle | Building_SpiritNexus | Building_ElderTree |
| Barracks | Building_Barracks | Building_SummoningAltar | Building_HunterPlant |
| MiningPost | Building_MiningPost | Building_ManaRift | Building_FungalNode |

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-07/09_00_faction-ingame-apply/`

### 전투 애니메이션 시스템 전면 재정비 (2026-04-03~04) ✅ 완료

**핵심 변경 파일**:
- `NetworkCombatController.cs` — 3-신호 RPC, TickCombat elapsed 수정, _combatAnimationSent, ExecuteAttack 동시 호출
- `UnitView.cs` — Walk CrossFade 1회 제한, _attackToWalkBlend, StopCombatAnimation 빈 메서드
- `NetworkUnit.cs` — WaitForUnitId 폴링 → OnValueChanged 콜백 교체
- `UnitCombatUseCase.cs` — TryAttack 네트워크 완전 차단 (HOST 이중 데미지 방지)
- `UnitStats.cs` — GetAttackCooldown 실제 클립 길이로 업데이트 (Assault=0.2, Pistoleer=2.0, Sniper=3.0)

**핵심 설계 결정**:
- StartCombatClientRpc: OnUnitEnteredCombatHandler 단독 전송 (TickCombat에서 제거)
- AttackCooldown = 클립 길이 — Animator 상태 읽기 없이 순수 타이머로 사이클 판단
- StopCombatAnimation() = 빈 메서드 — Walk는 StartWalkAnimationClientRpc 타이밍에만 전환
- `_combatAnimationSent` HashSet — TickCombat/코루틴 실행 순서 경쟁 조건 방지용 RPC 전송 추적

**버그 패턴 교훈**:
- TickCombat(Update)은 코루틴(yield return null)보다 먼저 실행 → 같은 프레임에 Dictionary 먼저 등록 가능
- RPC 전송 여부 추적은 타겟 추적 Dictionary와 반드시 분리
- ExecuteAttack을 핸들러에서 즉시 호출해야 서버 공격 사이클 T=0 = 애니메이션 루프 T=0 동기화

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-03/10_00_combat-animation-overhaul/`

### 유닛 NGO NetworkObject 전환 + 이동/전투/회전 동기화 (2026-03-26~29) ✅ 완료

**핵심 설계 결정 (2026-03-29 최종)**:
- 유닛 위치 동기화: NGO NetworkTransform (서버 position → 클라이언트 자동 보간)
- **유닛 회전 동기화: NetworkTransform SyncRotAngleY=true (서버 즉시 스냅 → 클라이언트 보간)**
- Walk/공격/사망 동기화: ClientRpc (이벤트 기반)
- Red 클라이언트 좌표+회전 보정: NetworkUnit.LateUpdate() (위치 반전 + Y축 +180°)
- NGO NetworkObject 부모 제약: 씬 루트에 생성 (일반 GameObject 하위 불가)
- 클라이언트 등록 타이밍: WaitForUnitId 폴링 + ApplyStartWalkWithRetry로 등록 지연 대응

**폐기된 패턴 (2026-03-29)**:
- ~~클라이언트 LateUpdate 델타 기반 회전 (Atan2 + RotateTowards)~~ → NetworkTransform rotation 동기화로 대체
- ~~TurnToFaceClientRpc + DORotate 보간~~ → 서버 즉시 스냅 + NetworkTransform 보간으로 대체
- ~~_isPreRotating / SetPreRotating / SetAttackRotating~~ → 전면 제거
- ~~_isWalkPending~~ → 공격 중 Walk 무시 가드(`if (_attackCoroutine != null) return`)로 교체
- ~~HasReceivedTurnToFace / MarkTurnToFaceReceived~~ → 전면 제거
- ~~ResetMovementTracking / ResetPositionTracking~~ → 전면 제거
- ~~UnitView의 DOKill/DORotate~~ → Quaternion.Euler 즉시 스냅으로 교체 (using DG.Tweening 제거)
- ~~GameEvents.OnUnitFacingChanged / UnitFacingChangedEvent~~ → 전면 제거
- ~~NetworkCombatController.TurnToFaceClientRpc~~ → 전면 제거

**이중 보간 문제 교훈**:
서버 DORotate(0.3초) + NetworkTransform 보간(0.1초) = ~1초 딜레이.
서버에서 즉시 스냅하면 NetworkTransform 보간만 적용되어 자연스러운 회전.

### 공격 타이밍 정밀화 (2026-03-27) ✅ 실기 테스트 완료

**구현 내용**:
- **타격 프레임 데미지**: 서버가 애니메이션 RPC 즉시 전송 → HitFrameTime 후 데미지 적용
- **타겟 고정(Target Lock)**: ApplyAttackDamage에서 IsInRange 체크 제거 — 공격 모션 시작 시 타겟 확정
- **쿨다운 통일**: UnitView.Update() 쿨다운 제거 → GameBootstrapper.Update() → TickCooldowns()

**신규 메서드 (UnitCombatUseCase)**:
- `TryFindTarget(UnitData)`: 타겟 탐색만, 데미지/쿨다운 없음 (멀티플레이 서버용)
- `ApplyAttackDamage(UnitData, int, bool)`: 딜레이 후 호출, IsAlive만 재확인 (IsInRange 없음)
- `TickCooldowns(float dt)`: 싱글플레이 전용 일괄 쿨다운 감소
- `FindTargetById(int, bool)`: Id로 Units/Buildings Dictionary 탐색

**HitFrameTime 값 (UnitStats.GetHitFrameTime)**:
- Assault: 0.133f (0:04, 4프레임/30fps)
- Pistoleer: 0.833f (0:25, 25프레임/30fps)
- Sniper: 2.000f (2:00)

**NetworkCombatController.TickCombat() 변경**:
- TryAttack() → TryFindTarget() 교체
- 성공 시: RPC 즉시 전송 + 쿨다운 리셋 + DelayedAttackDamage 코루틴 시작

**DelayedAttackDamage 코루틴**:
- HitFrameTime > 0: WaitForSeconds(delay)
- HitFrameTime = 0: yield return null (최소 1프레임 안전망)
- 이후 ApplyAttackDamage() 호출

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-03-27/11_00_attack-timing-precision/`

### 이동 전 회전 타이밍 수정 (2026-03-27) ✅ 실기 테스트 완료

**문제**: DOTween(Update) vs NetworkUnit.LateUpdate 충돌 — LateUpdate가 매 프레임 DOTween rotation을 덮어씌워 프리-회전 무효화
**해결**: `_isPreRotating` 플래그로 DORotate 실행 중 LateUpdate 델타 회전 차단

**수정 파일**:
- `Infrastructure/Network/NetworkUnit.cs`:
  - `_isPreRotating` (bool) 필드 추가
  - `SetPreRotating(bool)` public 메서드 추가
  - `ResetMovementTracking()`에 `_isPreRotating = false` 안전망 추가 (DOKill 중단 시 플래그 고착 방지)
  - LateUpdate 델타 회전 조건: `if (!_isPreRotating && _hasInitialPosition)`
- `Infrastructure/Network/NetworkCombatController.cs`:
  - `TurnToFaceClientRpc`에 `networkUnit?.SetPreRotating(true)` 추가
  - DORotate에 `.OnComplete(() => networkUnit?.SetPreRotating(false))` 추가

**핵심 패턴**: DOTween이 활성 중일 때 LateUpdate rotation 차단이 필요하면 `_isPreRotating` 패턴 사용

### Game UI Lifecycle Framework (2026-03-24) ✅ 실기 테스트 완료

**신규 파일**:
- `Presentation/UI/Core/IGameUI.cs` — UI 생명주기 인터페이스 (OnGameStarted/OnGameEnded/OnGamePaused/OnGameResumed, 모두 default 빈 구현)
- `Presentation/UI/GameUIManager.cs` — 등록/디스패치 매니저 (MonoBehaviour, [Managers] 하위 배치)

**수정 파일**:
- `Application/Events/GameEvents.cs` — OnGameStarted, OnGamePaused, OnGameResumed Subject<Unit> 추가
- `GameHudUI.cs` / `ProductionPanelUI.cs` / `BuildingPlacementUI.cs` / `GameEndUI.cs` — IGameUI 구현
- `GameBootstrapper.cs` — `_uiManager` SerializeField + LoadMap() 맨 앞에 Register/Initialize, 맨 끝에 OnGameStarted 발행
- `NetworkGameEndController.cs` — `_uiManager` 필드 추가, AnnounceWinnerClientRpc에서 `_uiManager?.NotifyGameEnded()` 호출 추가

**핵심 패턴**:
- `GameUIManager.Register()` — 중복 등록 방지 포함, LoadMap() 재호출 시 안전
- `GameUIManager.Initialize()` — CompositeDisposable로 중복 구독 방지
- `GameEndUI`는 OnGameEnded() 호출 제외 (ReferenceEquals 비교)
- **BUG-1 (멀티플레이 클라이언트 팝업 미닫힘)**: 클라이언트는 GameEvents.OnGameEnd 미발행 설계 → AnnounceWinnerClientRpc에서 직접 NotifyGameEnded() 호출로 수정

**새 UI 추가 시 체크리스트**:
1. `IGameUI` 인터페이스 구현 (필요한 메서드만 override)
2. `GameBootstrapper.LoadMap()` 앞부분에 `_uiManager.Register(새UI)` 1줄 추가
3. Inspector 참조 연결

### 반투명 배경 오버레이 구조 개선 (2026-03-23) ✅ 실기 테스트 완료

**변경 내용**:
- `AnimatedPanel.cs`: Hide() 내 `_backgroundOverlay.SetActive(false)` 타이밍 변경 — OnComplete 콜백 → Hide() 호출 즉시
- `SharedBackgroundButton.cs` (신규, `Presentation/UI/Common/`): Canvas 직속 공유 Background에 부착
  - `Register(Action onClose)` / `Unregister()` / `OnClick()` 3개 메서드
- `BuildingPlacementUI.cs` / `ProductionPanelUI.cs`: `_backgroundButton(Button)` 제거 → `_sharedBackground(SharedBackgroundButton)` 교체
  - Show()에서 `_sharedBackground?.Register(Close)`, Close()에서 `_sharedBackground?.Unregister()`

**씬 구조 변경 (Game.unity)**:
- `[UI]/Background` 하나를 ProductionPopup/BuildingPopup/GameEndPanel이 공유
- 각 팝업 자식 Background 삭제됨

### 유닛 생산 패널 전면 재작성 (2026-04-19) ✅ 실기 완료

**수정 파일**:
- `Domain/Building/ProductionState.cs` — QueueSlot struct 추가, PendingQueue/AutoTypes/AutoCycleIndex/CurrentIsAuto 추가, IsAutoMode → 읽기 전용 프로퍼티(`AutoTypes.Count > 0`)
- `Application/UseCases/UnitProductionUseCase.cs` — EnqueueUnit/ToggleAutoProduction/CancelQueueAt/TryStartNext/CompleteProduction/ChargeVisibleSlots 전면 재작성. CancelAutoTypeIfNeeded 헬퍼 추가.
- `Presentation/UI/ProductionPanelUI.cs` — UpdateQueueSlots 단순화, OnQueueSlotClicked fallback 제거
- `Infrastructure/Network/NetworkProductionController.cs` — SyncQueueStateClientRpc 파라미터 포맷 변경

**핵심 구조 (PendingQueue 단일 큐)**:
- `QueueSlot { Type, IsAuto, IsCharged }` — 단일 구조체로 수동/자동 통합
- `PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2` 불변식 — UI는 이 순서 그대로 읽으면 됨
- `AutoTypes: List<UnitType>` — 자동 등록 타입 목록 (인디케이터 + 순환 대상)
- `IsAutoMode = AutoTypes.Count > 0` — 필드 아님, 항상 AutoTypes 상태에서 계산

**전역 규칙**:
- Rule 1: 슬롯 클릭 취소 → 항상 전액 환불 (IsCharged=true인 경우)
- Rule 2: 자동 취소 시 IsCharged=true 항목은 수동 이관 (환불 없이 생산 계속)
- Rule 2-1: 자동 등록 타입이 PendingQueue 마지막 수동 항목과 같으면 IsAuto=true로 전환 (중복 추가 금지)
- Rule 3: 수동 추가 시 자동 모드 전체 해제 (IsCharged=false 자동 항목 제거, IsCharged=true는 수동 이관)
- Rule 4: CurrentProducing + IsCharged=true PendingQueue 합산 ≤ MaxQueueSize(3)
- Rule 5: 골드 차감 = 수동은 등록 시, 자동은 슬롯1/2 진입 시 (ChargeVisibleSlots)

**슬롯 클릭 = 생산 취소 + 자동 항목이면 AutoTypes에서도 제거**:
- `CancelAutoTypeIfNeeded(state, type)` — AutoTypes 제거 + 잔여 IsAuto 항목 Rule 2 처리 + NormalizeAutoCycleIndex
- slotIndex==0: `wasAuto = state.CurrentIsAuto` 를 `CurrentIsAuto=false` 초기화 전에 캡처 필수

**미해결 이슈**: 큐 비어있을 때 자동 등록 시 슬롯1에 1프레임 깜빡임 → 별도 점검 예정

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/production-panel-rewrite/`

---

### 자동/수동 생산 하이브리드 시스템 완성 (2026-03-23) [재작성 예정으로 무효화]

**핵심 설계**: AutoEntry(UnitType + IsCharged) 기반 골드 차감 시점 추적

**수정 파일**:
- `Domain/Building/ProductionState.cs` — AutoEntry 구조체, AutoEntries(List<AutoEntry>), AutoContains/AutoIndexOf 등 편의 접근자
- `Application/UseCases/UnitProductionUseCase.cs` — ToggleAutoProduction, EnqueueUnit, TryStartNext, CancelQueueAt, CanAutoEntryShowInSlot, TryPreChargeAutoEntries
- `Presentation/UI/ProductionPanelUI.cs` — UpdateQueueSlots 혼용 표시, 버튼 탭/롱프레스 분기
- `Infrastructure/NetworkProductionController.cs` — AutoEntries 참조 갱신

**핵심 패턴**:
- `CanAutoEntryShowInSlot`: AutoIndex 위치(슬롯0) 항목을 shownCount에서 **반드시 제외** (BUG-12)
  ```csharp
  for (int i = 0; i < state.AutoEntries.Count; i++)
  {
      if (i == state.AutoIndex) continue; // 슬롯0 제외
      if (state.AutoEntries[i].IsCharged) shownCount++;
  }
  ```
- `UpdateQueueSlots` 슬롯2: manualCount==1 && isNormalAutoState일 때 autoCount >= 2 필수 (BUG-13)
  - autoCount==1이면 그 항목이 슬롯0과 동일 → 슬롯2=null
- `ToggleAutoProduction` 취소 경로: 환불 없음, IsCharged=true && 슬롯1~2면 ManualQueue.Add (Rule 2)
- `TryStartNext` 자동 경로: IsCharged=false면 이 시점에 골드 차감 후 IsCharged=true 갱신
- `CompleteProduction` 자동 순환: AutoIndex 순환 **직전**에 완료된 항목의 IsCharged를 false로 리셋 (BUG-20 수정)
  ```csharp
  // AutoIndex 순환 전 IsCharged 리셋 — 다음 순환 시 골드 재차감을 위해
  var completedEntry = state.AutoEntries[state.AutoIndex];
  state.AutoEntries[state.AutoIndex] = new AutoEntry(completedEntry.Type, false);
  state.AutoIndex = (state.AutoIndex + 1) % state.AutoEntries.Count;
  ```
  - 리셋 안 하면 IsCharged=true 유지 → TryStartNext/TryPreChargeAutoEntries 모두 건너뜀 → 첫 등록 시만 골드 소모

**전역 규칙 참조**: `GameDesignDocument.md` → "생산 패널 운영 규칙" 섹션

### 코드 정리 (2026-03-20) ✅ 테스트 완료
- **TeamAssigner.cs 삭제**: Player Prefab=None으로 스폰 안 됨, NetworkGameFlow로 완전 대체 확인 후 삭제
- **LocalPlayerTeam.cs 주석 정리**: "TeamAssigner에서 호출" → "NetworkGameFlow에서 호출" (5곳)
- **NetworkGameFlow.cs 주석 정리**: L12 "TeamAssigner 준비 대기" → "IsHost 기반으로 팀 직접 결정"
- **GameBootstrapper.cs IsNetworkMode() 헬퍼 추출**: `NetworkManager.Singleton != null && (IsHost || IsClient)` 4곳 중복 → private 메서드 통합

### 싱글플레이 ViewConverter 초기화 버그 수정 (2026-03-20) ✅ 테스트 완료
- **증상**: Red팀 싱글플레이에서 내 진영이 화면 하단이 아닌 상단에 표시
- **원인**: `ViewConverter.Reset()`이 LocalPlayerTeam.Current 무시하고 항상 Blue 관점 고정
- **수정**: `GameBootstrapper.LoadMap()` — `ViewConverter.Reset()` 제거, `ApplyConfig()` 직후 LocalPlayerTeam 기반 Setup:
  ```csharp
  if (!isNetworkMode)
  {
      Vector3 mapCenter = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
      bool isRed = (LocalPlayerTeam.Current == TeamId.Red);
      ViewConverter.Setup(isRed, mapCenter);
  }
  ```
- **주의**: `ApplyConfig()` 이후에 호출해야 HexMetrics 준비 완료 후 GridCenter 계산 가능
- **카메라 초기 위치는 변경 없음** — 맵 중앙 유지 (SetCameraStartPositionForTeam 호출 금지)

### 카메라 줌 DOTween 보간 (2026-03-19) ✅ 테스트 완료
- **CameraController.cs**: HandleZoom() 즉시 적용 → DOTween 보간으로 교체
  - `_targetZoom` (float): 입력 시 Clamp된 목표값 누적
  - `_zoomTween` (Tweener): Kill() 후 새 Tween 시작 — 연속 스크롤 시 부드럽게 목표 갱신
  - `DOTween.To(() => _cam.orthographicSize, x => _cam.orthographicSize = x, _targetZoom, _zoomDuration).SetEase(Ease.OutCubic)`
  - `_zoomDuration` (SerializeField, default=0.25f): Inspector 조정 가능
  - `Awake()`에서 `_targetZoom = _cam.orthographicSize` 초기화
  - `OnDestroy()`에서 `_zoomTween?.Kill()` 정리
  - `using DG.Tweening` 추가
- ClampPosition()은 매 프레임 orthographicSize 읽으므로 수정 불필요

### 건물 인근 이동/공격 불가 버그 수정 (2026-03-18) ✅ 테스트 완료
- **HexPathfinder.cs**: `FindPath()` goal blocked 체크 제거 — 목표 타일이 ClaimedTile에 선점되어도 경로 탐색 가능
  - 이전: `if (blockedCoords.Contains(goal)) return null;` → 인근 타일 모두 선점 시 교착 상태
  - 이후: blocked는 경로 중간 타일에만 적용, 목표 도착 충돌은 ProcessStep에서 처리
- **UnitCombatUseCase.cs**: maxDist에 `Epsilon=0.05f` 추가
  - Pistoleer maxDist(0.866) = FlatTop 인접 거리(0.866) 경계 케이스 → 부동소수점 오차로 공격 실패
  - `float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;`

### 랜덤매칭 재경기 지원 (2026-03-18) ✅
- **GameEndUI.cs**: `SetupRematchButton()`에서 `isRandomMatch==true`일 때 버튼 숨기는 분기 제거
  - 랜덤매칭도 커스텀게임과 동일 흐름: 양측 동의 재경기 팝업 + NGO SceneManager.LoadScene("Game")

### 로비 종족 선택 UI — 캐러셀 방식 (2026-04-04~06) ✅ 테스트 완료

**신규/수정 파일**:
- `Domain/Common/RaceId.cs` — enum Human=0, Spirit=1, Transcendence=2 (자연→초월 변경)
- `Infrastructure/LocalPlayerRace.cs` — 로컬 플레이어 종족 정적 홀더 (Set/Current/Reset)
- `Infrastructure/GameRaceContext.cs` — BlueRace/RedRace 정적 홀더 (멀티플레이 수신용)
- `Presentation/UI/ViewModels/RaceSelectionViewModel.cs` — UniRx ReactiveProperty, CmdPrev/CmdNext, LocalPlayerRace.Set 연동
- `Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs` — 캐러셀 DOTween, Animator CrossFade 1초, IView 패턴
- `Presentation/UI/Views/Lobby/Battle/BattleMainView.cs` — BindRace() 메서드 추가, RaceSelectionView 항상 표시(독립 토글 제거)
- `Editor/RaceSelectionPreviewSetup.cs` — 씬 자동 구성 에디터 스크립트 (CharacterPreview 레이어, RT 512×512, 카메라 Z=-2, FOV=45)
- `Animations/Units/Pistoleer/Pistoleer.controller` — Idle 상태 m_Speed 0→1 수정

**핵심 설계**:
- RaceSelectionView는 BattlePanel(BattleRootView) 직속 자식, anchorMin=(0,0) anchorMax=(1,0.5) — BattleMainPanel과 sibling
- BattleMainPanel: 상단 50% (anchorMin.y=0.5, anchorMax.y=1.0)
- RaceSelectionView 항상 표시 — BattleMainPanel(버튼 영역)만 CurrentScreen에 따라 토글
- RaceSelectionViewModel은 BattleRootView에서 생성/Dispose, BattleMainView.BindRace()로 전달
- CharacterPreview 레이어 격리 → RenderTexture → RawImage(CharacterDisplay)
- AnimBlendTime = 1.0f (_moveDuration과 동일), offset 0(중앙)=Walk, offset 1,2(좌우)=Idle

**캐러셀 위치 (씬 확정값)**:
- CenterPos: (1000, 0.35, 2), LeftPos: (999.7, 0.1, 5), RightPos: (1000.3, 0.1, 5)
- 카메라: (1000, 1.5, -2), Rotation: Euler(12, 0, 0), FOV=10

**Pistoleer Idle 버그 교훈**:
- Animator Controller 상태의 m_Speed 값 직접 확인 필수 (Editor에서 설정하지 않으면 0이 될 수 있음)
- m_Speed: 0이면 애니메이션이 첫 프레임에서 동결됨

**Android URP RenderTexture 잔상 버그 교훈 (2026-04-06)**:
- 근본 원인: RT 에셋 파일(`m_AntiAliasing: 2`)과 카메라 설정(`allowMSAA=false`, 1 sample) 간 sample count 불일치
- 에러: `Attachment 0 was created with 1 samples but 2 samples were requested`
- 현상: sample count 충돌 → URP Render Pass clear 실패 → 이전 프레임 타일 메모리 로드 → 잔상
- 수정 체크리스트 (RenderTexture 전용 카메라 설정):
  - RT 에셋: `m_AntiAliasing: 1` (YAML 직접 확인 필수 — EnsureRenderTexture 코드 수정만으로 반영 안 될 수 있음)
  - Camera: `allowMSAA = false`, `allowHDR = false` (기본값 true라 명시적으로 꺼야 함)
  - Camera: `backgroundColor.alpha = 1` (alpha=0이면 일부 Android GPU 드라이버 clear 생략)
  - URP: `urpData.antialiasing = AntialiasingMode.None`
  - URP: `urpData.renderType = CameraRenderType.Base`
  - URP: `urpData.renderShadows = false`

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-04/21_00_race-selection-ui/`

### 재경기 초기화 버그 수정 (2026-04-04) ✅ 테스트 완료

**증상**: 재경기 시 이전 게임 유닛/건물이 씬에 잔존
**원인**: NGO SceneManager.LoadScene(Single)으로 같은 씬 재로드 시 동적 스폰 NetworkObject 자동 Despawn 미보장
**수정**: `NetworkGameEndController.StartRematch()`에서 LoadScene() 직전 SpawnManager.SpawnedObjects 순회 → 동적 NetworkObject 명시적 Despawn

**핵심 패턴**:
- `SpawnedObjects.Values`를 `List<NetworkObject>` 복사본으로 순회 (Despawn 중 컬렉션 변경 방지)
- `IsSceneObject == false`만 Despawn (씬 배치 오브젝트 자동 제외)
- `IsSpawned == true` / `IsSceneObject == false` — NGO 2.9.x에서 bool? (nullable) 비교 방식 필수

**교훈**:
- `DestroyWithScene = true`는 같은 씬 재로드 시나리오에서 동작 불보장
- 같은 씬 재로드 전에는 반드시 동적 NetworkObject를 명시적으로 Despawn해야 함
- `Active Scene Synchronization`은 씬 전환용 설정 — 같은 씬 재로드와 무관

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-03/20_00_rematch-initialization-bug/`

### 커스텀게임 재경기(Rematch) 시스템 (2026-03-17) ✅ 테스트 완료
- **NetworkGameManager.cs**: `_isRandomMatchmaking` bool 필드 + `IsRandomMatchmaking` 속성 추가
  - StartMatchmakingAsync → true, CancelMatchmakingAsync/DisconnectAsync → false
- **NetworkGameEndController.cs**: 재경기 RPC 시스템 전면 교체
  - `AnnounceWinnerClientRpc(int, bool isRandomMatch)` — 파라미터 2개로 변경
  - `_rematchRequesterId` (ulong.MaxValue=없음) — 첫 요청자 추적, 양측 요청 시 즉시 재경기
  - RPC: RequestRematchServerRpc, AcceptRematchServerRpc, DeclineRematchServerRpc
  - ClientRpc: NotifyRematchRequestedClientRpc(targeted), NotifyRematchDeclinedClientRpc(targeted)
  - `StartRematch()`: NGO SceneManager.LoadScene("Game") — 네트워크 유지 상태 씬 재로드
  - `_lobbySceneName` 제거, `OnMultiplayerRestart()` 제거
  - `_rematchRequestPopup` SerializeField 추가 (Inspector 연결 필요)
- **GameEndUI.cs**: `OverrideRestartForMultiplayer()` → `SetupRematchButton(bool, Action)` + `RestoreRematchButton()` 교체
  - `_restartButtonText` SerializeField 추가 (Inspector 연결 필요)
  - ~~랜덤매칭: 다시하기 버튼 숨김~~ → 2026-03-18 제거, 랜덤매칭도 재경기 지원
  - 커스텀게임: 요청/대기/복원 UI 상태 관리
- **RematchRequestPopup.cs** (신규): `Presentation/UI/Common/` — `_overlay`+수락/거절 팝업+거절 알림 팝업
  - Inspector 연결 필요: _overlay, _requestPanel, _acceptButton, _declineButton, _declinedPanel, _declinedConfirmButton
  - **루트 오브젝트는 Active 유지 필수** — FindFirstObjectByType은 비활성 오브젝트 탐색 불가
  - Hide()/Show*()에서 _overlay도 함께 제어 (overlay 별도 필드로 관리)
- **FindFirstObjectByType 교훈**: 비활성 오브젝트 포함 탐색 시 `FindObjectsInactive.Include` 인자 필요

### 멀티플레이 로비 복귀 버그 수정 (2026-03-17)
- **근본 원인**: `NetworkGameEndController._lobbySceneName` Inspector="Game" → 게임 씬 재로드
- **GameEndUI.cs**: `ReturnToLobby()` (NGM.Shutdown + LoadScene("Lobby")), `CountdownCoroutine()` (WaitForSecondsRealtime 기반 30초)
- Inspector 연결 필요: `_countdownText` (TextMeshProUGUI), `_autoReturnSeconds` (default=30f)

### 전역 로딩 스크린 구현 (2026-03-17)
- `LoadingScreen.cs` (`Presentation/UI/Common/`): 싱글턴, DontDestroyOnLoad, CanvasGroup DOFade 페이드 인/아웃
- `BattleViewModel.cs`: 싱글플레이 `LoadSingleplayScene()` → async void + `await Task.Delay(2000)` + Show/Hide
- 커스텀 호스트/참가: `LoadGameScene()` 직전 `LoadingScreen.Instance?.Show()`
- 랜덤매칭: `NetworkGameManager.StartMatchmakingAsync`에 `onMatchFound` 콜백 추가 → matchId 확보 직후 Show()
- sceneLoaded 이벤트로 모든 케이스 자동 Hide() (NGO 씬 전환 포함)

### 랜덤 매칭 버그 수정 (2026-03-16) — [random-matching-bugfix.md](random-matching-bugfix.md)
- string.GetHashCode() 크로스-프로세스 비결정성 → GetStableHash() 대체
- NetworkGameManager: OnClientConnectedCallback 등록을 StartNetworkHost() 이전으로 이동

### Animation Event 타격 반응 (2026-03-14) — [rendering-and-animation.md](rendering-and-animation.md)
- AnimationEventRelay → UnitView.OnAttackHit() → scale punch 시각 효과

### 유닛 확정 스탯 (2026-03-14) — [unit-stats-and-combat.md](unit-stats-and-combat.md)
- Pistoleer/Assault/Sniper 3종 스탯 확정, AttackRange int→float 변경

## 토픽 파일 인덱스

### 네트워크
- [network-infra.md](network-infra.md) — Phase 1~8 상세 (UGS, NGO, 동기화, UI/UX, 팀 할당, 승패)
- [network-todo.md](network-todo.md) — 네트워크 미완성 항목
- [random-matching-bugfix.md](random-matching-bugfix.md) — 2026-03-16 랜덤 매칭 버그 수정

### 전투 & 유닛
- [unit-stats-and-combat.md](unit-stats-and-combat.md) — 스탯, IEntityPositionProvider, 쿨다운, 클라이언트 시각 동기화
- [combat-fixes.md](combat-fixes.md) — ClaimedTile 기반 공격 위치 보정, UnitView 부드러운 회전
- [attack-direction-refactor.md](attack-direction-refactor.md) — 공격 방향 리팩터링 (2D→3D)

### 렌더링 & 뷰
- [rendering-and-animation.md](rendering-and-animation.md) — UnitView 애니메이션, Shader Graph, HexTileView, 팀 프리팹
- [3d-transition.md](3d-transition.md) — XZ 좌표계 전환, 렌더링 전환, Phase별 수정 파일
- [camera-and-view.md](camera-and-view.md) — 카메라 틸트, ViewConverter, 경계 클램프, 건물 위치 버그

### 게임플레이
- [gameplay-systems.md](gameplay-systems.md) — 랠리포인트, 초상화 동적 업데이트

## 핵심 패턴 요약

### 정적 홀더 패턴 (레이어 간 의존성 우회)
- `HexOrientationContext` — Domain에서 Core의 Orientation 접근
- `NetworkContext` — Application에서 NetworkManager 상태 접근
- `LocalPlayerTeam` — 현재 플레이어 팀 (싱글=Blue, 네트워크 시 갱신)
- `ViewConverter` — Red팀 좌표/방향 반전

### GameBootstrapper Start() 분기
- NetworkManager null 또는 IsHost/IsClient=false → 싱글플레이 (LoadMap 즉시)
- 네트워크 → 맵 로드 건너뜀, NetworkGameFlow가 StartNetworkGame() 대기
- C# LangVersion 9.0 (switch expression 사용 가능)

### 팀 매핑
- TeamId: Neutral=0, Blue=1, Red=2
- Host→Blue, Client→Red
- TeamAssigner는 삭제됨 (2026-03-20) — NetworkGameFlow.WaitForTeamAndSendReady()에서 IsHost?Blue:Red로 직접 할당

### 동기화 타이밍
- NetworkSync 스폰 시 HexGrid/ResourceUseCase null 가능 → null 방어 필수
- ResourceUseCase 생성자는 OnResourceChanged 미발행 → SyncInitialGold() 필요
- ViewConverter.Setup()은 LoadMap() 이전에 호출해야 함

### 유닛 애니메이션 핵심
- Animator.Play() 직접 호출 (트랜지션 우회)
- 파라미터: IsDead(bool) 1개만
- Root Motion 반드시 OFF
- **Animator Controller 상태 m_Speed 주의**: 기본값 0이면 애니메이션 첫 프레임 동결. 새 상태 추가 시 m_Speed=1 확인 필수
