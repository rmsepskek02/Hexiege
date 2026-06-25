# Game Programmer — 유닛 & 건물

유닛 이동/전투/혼잡도, 건물 배치/철거/업그레이드/환불, 생산 시스템.
(스탯/쿨다운/공격 상세는 [unit-stats-and-combat.md], [combat-fixes.md], [attack-direction-refactor.md] 참조)

---

## 유닛 이동/전투 상태 머신 (2026-05-11 재설계, V3)

슬롯 기반 분산 전면 폐기 → 겹침 허용 단순 구조. 근접/원거리 동일 상태 머신.

`UnitView.MoveAlongPathV3()`:
- Phase 0(A* Lerp) → HasEnemyInDetectRange 감지 → Phase 1(월드 직선 추격) → HasEnemyInRange 진입 → 공격 → FindForwardClosestTile → Phase 0 재개
- `UnitCombatUseCase.FindFirstEnemyInDetectRange()`: 모든 유닛 `DetectRange × TileHeight` 통일 (isMelee 분기 제거)
- 원거리 유닛 DetectRange는 AttackRange보다 크게 설정

### 핵심 버그 패턴
- **전투 추격 중 건물 생성/파괴 시 멈춤**: `_isInCombatPursuit` bool, `IsInCombat() → _combatTargetTransform!=null || _isInCombatPursuit`
- **전투 종료 후 ~1타일 순간이동**: 즉시 스냅 제거, 정렬 Lerp 추가(동일 속도 걸어서, 매 프레임 적 감지)
- **건물 생성/파괴 시 멈춤(repath)**: `_pendingPath`/`_currentNextTileCoord`. OnPathInvalidated에서 앞 타일에 건물 생기면 즉시 MoveTo, 그 외 _pendingPath 저장만(코루틴 유지). "부드러운 교체=기본, 즉시 재시작=예외(앞 타일 막힘만)"
- **Phase1 타겟 사망/전투 종료 후**: 무조건 Phase2 진입 금지 → HasEnemyInDetectRange 재확인 후 다음 타겟 재선택(continue) 또는 Phase2(break)
- **후방 스냅 방지**: 거리 비교는 월드 거리 대신 `HexCoord.Distance`(도메인 정수, ViewConverter 무관/부동소수점 없음)

---

## 유닛 회전

- `[SerializeField] _rotationSpeed=270f` 단일 필드로 모든 회전(이동/정렬/추격/공격) 통일
- 방향 계산: `CalculateAttackAngle(toPos)` (Atan2 직접, DirectionAngles 무관)
- Lerp 루프 매 프레임 `Quaternion.RotateTowards(현재, targetRot, _rotationSpeed * Time.deltaTime)`
- DirectionAngles: `{60,120,180,240,300,0}` (FlatTop atan2 실제 월드 각도. NW(5)=0°). 메시 자식 Y(30°) 제거 시 +30° 조정
- 멀티: 코루틴 가드(서버만 실행) → NetworkTransform 보간으로 클라 전달

---

## 혼잡도 기반 분산 (2026-05-15)

- `Application/Services/CongestionMap.cs` — 타일별 혼잡도 Increment/Decay/Clear (순수 C#)
- `Application/Services/CongestionAwarePathfinder.cs` — 혼잡도 가중 A*. 타일 비용=1+(혼잡도×CongestionWeight). non-walkable 목적지 → walkable 인접 자동 대체
- GameConfig: CongestionDecayInterval=5f, CongestionWeight=3f (ScriptableObject 낭비 방지로 GameConfig 통합)
- reactive congestion: 유닛 실제 타일 진입 시점 증가 (`GameEvents.OnUnitEnteredTile`). UnitView `_isAStarMoving` true일 때만 발행. 서버 가드

---

## 다중 히트 데미지

- UnitStats `GetHitFrameTimes()` (float[]). UnitData `HitFrameTimes: float[]`
- 쿨다운은 공격 사이클 시작 시 1회만 리셋 (히트 횟수 무관)
- 싱글: `_pendingHits` 타이머 리스트 + TickPendingHits(dt). 멀티: DelayedAttackDamage 코루틴 히트 수만큼 병렬
- 타겟 사망 시 잔여 히트 자동 취소 (ApplyAttackDamage 내 IsAlive 체크)
- 타이밍 예(30fps): FlameSpirit 6히트(쿨3.0s) 0.667/1.167/1.433/1.667/1.933/2.100, LionKnight 2히트 0.733/1.267

---

## 종족별 프리팹 (Factory)

- UnitFactory/BuildingFactory: 종족별 6세트(humanBlue/Red, spiritBlue/Red, transcendenceBlue/Red), GameRaceContext switch
- UnitFactory: `List<UnitPrefabEntry>(type, blue, red)` 구조
- 건물 매핑: Castle(Castle/SpiritNexus/ElderTree), Barracks(Barracks/SummoningAltar/HunterPlant), MiningPost(MiningPost/ManaRift/FungalNode)
- UnitData에 Race 필드 추가 안 함 — 스폰 시점에 GameRaceContext 직접 조회

---

## 건물 배치/철거/업그레이드

### BuildingType 구조 (2026-05-17~)
- 단일 Barracks 제거 → 종족별 라인 × 단계(1/2/3) 26종. enum 0~31 명시값
- `BuildingTypeHelper`(Domain): IsProductionBuilding / GetStage / GetNextStage / CanUpgrade / CanShowActionPanel(`!IsProductionBuilding && type != Castle`)
- `BuildingData.Stage` 파생 프로퍼티 (별도 저장 없음)
- **주의 — 직렬화**: enum 순서 변경 시 기존 인덱스 직렬화가 다른 값으로 덮어쓰임. Inspector 전체 재검토 필요

### BuildingTypeHelper lookup table 전환 (Phase 2, 2026-06-25)
- IsProductionBuilding / GetStage / GetNextStage 3개 switch → 단일 `Dictionary<BuildingType, BuildingMeta>` (`_buildingTable`)
- `BuildingMeta` private readonly struct: `IsProduction`(bool) / `Stage`(int 1·2·3) / `NextStage`(BuildingType?)
- 세 메서드는 `_buildingTable.TryGetValue` 조회만: 미등록 건물 = 비생산(false/0/null). 비생산 7종(Castle/MiningPost/AutoTower/FlightFacility/Research/MagicBuilding/HealShrine)은 table 미등록
- **신규 생산건물 추가 시 `_buildingTable`에 한 줄만 추가** → 세 메서드 자동 정합. CanUpgrade(=GetNextStage().HasValue)/CanShowActionPanel은 호출 기반이라 무수정 자동 반영
- 시그니처/반환타입/네임스페이스 동일 → 호출부 무영향. 동작 보존 리팩토링(SINGLE 7 + MULTI 2 전 항목 PASS)
- PrimalSanctuary(동물A 3단계): 기존 switch에도 포함돼 있던 항목. table에 `(true, 3)` 명시(동작 보존). "누락 의심"은 오판이었음
- **기존 switch 3개 본문은 주석으로 보존 중** — 사용자 지침상 별도 지시 있을 때만 삭제(현재 보존). 브랜치 `claude/code-refactor-phase2-structural`(3838c4d)

### 건물 프리팹 구조
- Root GO(Transform ONLY) + Child GO(MeshFilter/MeshRenderer). BuildingView 미부착
- BuildingFactory `_buildingObjects` Dict로 Id→GO 관리 → 이 Dict로 GO 직접 파괴
- 업그레이드: **새 GO 먼저 생성 → 기존 GO Destroy** 순서 (빈 타일 방지)

### 철거 (CancelAllQueue)
- `UnitProductionUseCase.CancelAllQueue(barracksId)`: ① ClearRallyPoint(UnregisterBarracks 이전 필수) ② CurrentProducing 환불 ③ PendingQueue IsCharged=true 환불, false 환불없이 제거 ④ 상태 초기화 ⑤ OnProductionQueueChanged ⑥ UnregisterBarracks
- 골드 환불: `BuildingStats.GetTotalInvestedCost(type, race) / 2`
- 환불 누적: 1단계 건설비 + 모든 업그레이드비 합산. `BuildingStats._totalInvestedCostCache`(Set/Get), GameBootstrapper 단계 체인 순회로 채움
- 비생산 건물 환불 캐시 루프 누락 시 GetTotalInvestedCost → 0 버그

### 업그레이드 시 생산 상태 (ProductionTicker.OnBuildingUpgraded)
- ProductionTicker는 OnBuildingPlaced + **OnBuildingUpgraded** 둘 다 구독 (업그레이드 새 건물 미등록 시 랠리 마커 미표시)
- 핸들러 순서(바꾸면 버그): ① savedRallyPoint=GetState(oldId)?.RallyPoint ② CancelAllQueue(oldId) ③ RegisterBarracks(newBuilding) ④ SetRallyPoint 복원

---

## 생산 시스템 (PendingQueue 단일 큐, 2026-04-19 재작성)

- `QueueSlot { Type, IsAuto, IsCharged }` — 수동/자동 통합 단일 구조체
- `PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2` 불변식 (UI는 이 순서 그대로)
- `AutoTypes: List<UnitType>` — 자동 등록 타입. `IsAutoMode = AutoTypes.Count > 0` (필드 아님, 계산값)
- `CurrentIsAuto` 파생 getter: `_currentIsAutoFlag && CurrentProducing.HasValue && AutoTypes.Contains(CurrentProducing.Value)` (수동 관리 금지)

### 전역 규칙
- R1: 슬롯 클릭 취소 → 항상 전액 환불(IsCharged=true)
- R2: 자동 취소 시 IsCharged=true 항목은 수동 이관(환불 없이 생산)
- R2-1: 자동 등록 타입이 마지막 수동 항목과 같으면 IsAuto=true 전환(중복 금지)
- R3: 수동 추가 시 자동 모드 전체 해제(IsCharged=false 자동 제거, true는 수동 이관)
- R4: CurrentProducing + IsCharged=true 합산 ≤ MaxQueueSize(3)
- R5: 골드 차감 = 수동은 등록 시, 자동은 슬롯1/2 진입 시
- R20(슬롯0 확장): 슬롯0 수동 생산 중 동일 타입 자동등록 → 중복없이 슬롯0 자체 전환
- GameSystemRules.md 규칙 20 참조

### 깜빡임 방지
- 큐 빌 때 자동 등록: Add 후 `!CurrentProducing.HasValue`이면 즉시 TryStartNext + Early Return
- 완료 사이클: CompleteProduction에서 OnUnitProduced 후 즉시 TryStartNext (ChargeVisibleSlots/직접 발행 제거)
- slotIndex==0 취소: `wasAuto = CurrentIsAuto`를 초기화 전 캡처 필수

### ProductionPopup UI 슬롯
- 유닛 버튼 3개 = 1/2/3단계. 슬롯0 항상 해금(LockIndicator 불필요), 슬롯1/2만
- `_unitLockIndicators[0]→슬롯1, [1]→슬롯2`. UpdateLockIndicators `int slotIndex=i+1` 매핑
- 2유닛 건물 [유닛1][빈][유닛2]: 슬롯1 CanvasGroup alpha=0(공간 유지). UpdateButtonPortraits slot0=list[0]/slot1=스킵/slot2=list[1]
- 초상화: `_buildingUnitMappings[x].blueUnits[i].portrait`가 런타임 소스(씬뷰 미리보기 Image 슬롯 아님). 파일명 `{UnitType소문자}_portrait_{blue|red}.png`

---

## 방어 타워 (AutoTower)

- `Application/UseCases/TowerCombatUseCase.cs` — Tick(dt): AutoTower 순회 → 쿨다운 감소 → 0 이하 시 가장 가까운 적 유닛(건물 제외) 데미지
- 멀티 가드: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`이면 조기 반환
- 팀→종족: `Func<TeamId, RaceId>` 델리게이트 주입
- 쿨다운: `BuildingStats.GetAttackCooldown(type, race)` (비타워 0f). Human 5.0s, Spirit 3.5s, Trans 5.0s
- Human CannonTower 초기 방향: BuildingFactory.GetInitialRotation. 내 진영 identity / 상대 진영 Euler(0,180,0). ViewConverter.IsFlipped로 판별

---

## 랠리포인트 팀별 표시

- RallyPointChangedEvent에 TeamId Team 필드 (이벤트 자기완결)
- ProductionTicker.OnRallyPointChanged 진입부 팀 필터: IsServer→Blue, 아니면 Red. 싱글 시 스킵
- 필터링 책임은 Presentation 레이어

---

## 사망 이벤트 (강타입 분리)

- EntityDiedEvent/OnEntityDied 삭제 → UnitDiedEvent/BuildingDiedEvent + OnUnitDied/OnBuildingDied
- RPC 시그니처는 `EntityDiedClientRpc(int, bool)` 유지 (와이어 호환)
- 발행 순서: RemoveUnit/RemoveBuilding 직전 (구독자 도메인 Dict 접근)

---

## 부유 HP 텍스트

- World Space TMP(3D), scale=1f 고정(줌 비례). 빌보드 `LookRotation(-Camera.forward, up)`, 좌우반전 `localScale(-s,s,s)`
- FloatingHpTextSpawner: OnEntityDamaged 구독, Queue 풀 10개. 팀 색상 Blue 연두/Red 노랑
- Material Preset은 독립 .mat 필수 (폰트 sub-asset 지정 시 .asset 오염)
