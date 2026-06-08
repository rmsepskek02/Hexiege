# Hexiege - 작업 이력

**역할:** 완료된 작업의 시간순 기록  
**상세 문서:** 각 작업의 Research/Plan/Testcase → `_Tasks/YYYY-MM-DD/HH_MM_작업명/`

---

## 마일스톤 이력

| 날짜 | 마일스톤 |
|------|---------|
| 2026-02 이전 | 싱글플레이 코어 루프 완성 (헥스, 전투, 건물, 생산, 승패) |
| 2026-02 | 멀티플레이 Phase 1~8 완성 |
| 2026-02-27~03-01 | 2D→3D 전환 완료 (XZ 좌표계, 55도 카메라, 3D 모델) |
| 2026-03-02 | 전투 거리 정밀도 버그 수정 (IEntityPositionProvider) |
| 2026-03-02 | GameConfig 정리 (AnimationFps 제거, TileHeight 수정) |
| 2026-03-07 | 공격 방향 Transform 기반 구현 완료 (UnitView._meshYOffset=30f, Atan2 기반 방향 계산) |
| 2026-03-07 | 유닛별 AttackCooldown 시스템 완료 (UnitData.AttackCooldown/AttackCooldownRemaining) |
| 2026-03-01 | 생산 큐 클라이언트 UI 즉시 갱신 수정 (OnProductionQueueChanged → SyncQueueStateClientRpc) |
| 2026-03-07 | 자동생산 멀티플레이 지원 완료 (ToggleAutoServerRpc + AutoProductionChangedClientRpc) |
| 2026-03-07 | Siege/AI 이동 서버 권위 동기화 완료 (BroadcastServerMove + BroadcastMoveClientRpc, 클라이언트 화면 불일치 수정) |
| 2026-03-13 | 팀별 피아식별 프리팹 에셋 완료 (Castle/Barracks/Pistoleer/Assault/Sniper Blue+Red) |
| 2026-03-13 | 반응형 팝업 UI 완료 (ProductionPopup/BuildingPopup 앵커 기반 배치) |
| 2026-03-14 | 팀별 초상화 동적 업데이트 완료 (ProductionPanelUI/BuildingPlacementUI — Show() 시 팀별 스프라이트 교체) |
| 2026-03-14 | 전투 범위 임계값 수정 (UnitCombatUseCase epsilon 제거 → 타일 중심 간 정확한 거리 기준) |
| 2026-03-14 | 팀별 프리팹 코드 연동 완료 (UnitFactory 팀+타입별, BuildingFactory 팀별 분기) |
| 2026-03-14 | Assault/Sniper 코드 연동 완료 (UnitType enum, UnitStats, UnitProductionStats, ProductionPanelUI 생산 버튼) |
| 2026-03-14 | 공격 애니메이션-타격 시각 동기화 완료 (AnimationEventRelay + Animation Event + scale punch) |
| 2026-03-14 | 유닛 스탯 재조정 (ATK: Pistoleer=6, Assault=1, Sniper=10 / cooldown=클립 길이 기준 DPS 산출) |
| 2026-03-14 | 유닛 메시 방향 보정 완료 (하위 Mesh Y=30°, Root Motion OFF, _meshYOffset=공격 전용) |
| 2026-04-29 | 유닛 메시 방향 보정 전면 통일 — 전 유닛(9종) Mesh Y=0, _meshYOffset 코드 제거, 이동 anim offset=0, DirectionAngles={60,120,180,240,300,0} (FlatTop 실제 월드 각도로 재계산) |
| 2026-03-14 | 유닛 회전 DOTween 보간 완료 (이동/공격 모두 DORotate + Ease.OutQuad, _rotationDuration SerializeField) |
| 2026-03-14 | 공격 후 Walk 복귀 버그 수정 (타겟 소멸 시 Play(StateWalk) 명시 호출, 멀티/싱글 공통) |
| 2026-03-15 | 로비 씬 분리 MVVM 완료 (Lobby/Game 씬 분리, UniRx 기반 MVVM, 탭 4개 + 전투 서브화면 5개, 씬 빌드 완료) |
| 2026-03-15 | 멀티플레이 게임 시작 버그 수정 (Host OnClientConnectedCallback 누락 → HandleClientConnected 추가) |
| 2026-03-16 | 랜덤 매칭 게임 씬 전환 버그 수정 (GetHashCode 크로스-프로세스 비결정성 → GetStableHash polynomial hash 교체) |
| 2026-03-17 | 전역 로딩 스크린 완료 (LoadingScreen.cs 싱글턴, Lobby 씬 UI, 씬 전환 자동 Hide) |
| 2026-03-17 | 멀티플레이 로비 복귀 버그 수정 (Inspector _lobbySceneName 값 오류 발견, 로컬 독립 처리로 설계 변경, 30초 자동 복귀 타이머) |
| 2026-03-17 | 커스텀게임 재경기 시스템 완료 (양측 동의 재경기, RematchRequestPopup 신규, 레이스 컨디션 처리) |
| 2026-03-18 | 랜덤매칭 재경기 지원 완료 (커스텀게임과 동일 흐름) |
| 2026-03-18 | 건물 인근 타일 이동/공격 불가 버그 수정 (HexPathfinder goal blocked 체크 제거, Epsilon=0.05f 추가) |
| 2026-03-19 | 카메라 줌 DOTween 보간 완료 (CameraController _targetZoom + DOTween.To Ease.OutCubic) |
| 2026-03-19 | UI DOTween 애니메이션 프레임워크 완료 (UIAnimator + AnimatedPanel, SlideFromTop/Bottom, blocksRaycasts 버그 수정) |
| 2026-03-20 | 코드 정리 완료 (TeamAssigner.cs 삭제, 주석 정리, IsNetworkMode() 헬퍼 추출) |
| 2026-03-20 | 싱글플레이 ViewConverter 초기화 버그 수정 (Reset() → LocalPlayerTeam 기반 Setup, ApplyConfig() 직후 호출) |
| 2026-03-23 | 자동/수동 생산 하이브리드 시스템 완성 (전역 규칙 5가지, IsCharged 기반 골드 차감, BUG-01~13 수정) |
| 2026-03-24 | Game UI Lifecycle Framework 완료 (IGameUI 인터페이스 + GameUIManager, 멀티플레이 클라이언트 팝업 버그 수정) |
| 2026-03-26 | 유닛 NGO NetworkObject 전환 완료 (NetworkTransform 위치 동기화, 클라이언트 예측 제거) |
| 2026-03-27 | 이동 전 회전 선행 완료 (Rotate-then-Move, _isPreRotating 플래그) |
| 2026-03-27 | 공격 타이밍 정밀화 완료 (타격 프레임 데미지, 타겟 고정, 쿨다운 통일) |
| 2026-04-04 | 전투 애니메이션 시스템 재정비 완료 (3-신호 RPC, 6가지 규칙, _combatAnimationSent 경쟁조건 수정) |
| 2026-04-04 | 자동생산 BUG-20 수정 (CompleteProduction IsCharged 리셋 누락) |
| 2026-04-06 | 로비 종족 선택 UI 완료 (RaceId enum 3종족, 캐러셀 방식, RenderTexture 캐릭터 미리보기, DOTween 전환) |
| 2026-04-06 | 종족명 자연→초월 변경 (Nature→Transcendence enum 전체 rename) |
| 2026-04-06 | Pistoleer Idle 애니메이션 버그 수정 (Pistoleer.controller Idle m_Speed 0→1) |
| 2026-04-06 | Android URP RenderTexture 잔상 + RenderPass 에러 수정 (RT antiAliasing 2→1, allowMSAA/allowHDR false) |
| 2026-04-07 | 종족 인게임 적용 완료 (UnitFactory/BuildingFactory 종족별 6세트 분기, 에디터 자동 연결 스크립트) |
| 2026-04-11 | UnitType 개편 + 근접 사거리 시스템 완료 (9종 독립 enum, FindPathToNeighbor, ClaimedTile non-walkable 예외) |
| 2026-04-11 | 근접 공격 거리 다듬기 완료 (MeleeContactDist=0.3f, BuildingDetectionRadius=0.2f, 타겟 타입별 분리) |
| 2026-04-11~12 | 원거리 유닛 공격 중 회전 추적 완료 (Transform 참조, RotateTowards 270°/s, 타겟 고착성, 멀티 실기 MULTI-001~007 PASS) |
| 2026-04-12 | 종족+팀별 건물/유닛 초상화 완료 (BuildingRacePortraitSet 6세트, Inspector 연결) |
| 2026-04-13 | Spirit Blue ManaRift 초상화 스프라이트 제작 완료 (초상화 세트 전 종목 완성) |
| 2026-04-13 | 유닛/건물 스탯 확정 적용 완료 (Spirit/Transcendence 6종 HP/ATK/생산시간/비용, Transcendence 건물 HP 종족별 분기) |
| 2026-04-13 | 피격 시 부유 HP 텍스트 완료 (FloatingHpText DOTween, 오브젝트 풀, 줌 스케일링, 멀티 클라이언트 표시) |
| 2026-04-13 | 부유 텍스트 팀별 색상 완료 (Blue=연두, Red=노랑, Inspector 조정 가능) |
| 2026-04-18 | 타겟 고정(Target Lock) 데미지 불일치 버그 수정 — 멀티플레이에서 바라보는 타겟(B)이 아닌 가까운 유닛(C)에게 데미지가 적용되던 버그. NetworkCombatController.TickCombat() damageTargetId 지역 변수 분리로 수정. 멀티 실기 PASS |
| 2026-04-19 | 유닛 생산 패널 전면 재작성 완료 — PendingQueue 단일 큐 구조(QueueSlot), 자동/수동 통합, CancelAutoTypeIfNeeded 헬퍼 추가. 싱글 실기 TC-001~018 전체 PASS |
| 2026-04-19 | 생산 슬롯 깜빡임 버그 수정 — 큐 비어있을 때 자동 등록 시 슬롯1→슬롯0 1프레임 이동 버그. ToggleAutoProduction에서 !CurrentProducing.HasValue이면 즉시 TryStartNext 호출로 수정 |
| 2026-04-30 | 종족/팀 초상화 및 생산 연동 정비 — UI 스킨 로직 제거, 종족별 Unit/Building Entries 리스트 기반 동적 바인딩 및 프리팹 생성 일치화 완료. 인스펙터 구조 단순화. |
| 2026-05-14 | 유닛 회전 시스템 전면 개편 완료 — 방향 계산 Atan2 통일 (FacingDirection.FromCoords 제거), A*/정렬/추격 모든 단계 RotateTowards 적용, _rotationSpeed 단일 Inspector 필드로 통일. MovementLogger.cs 삭제. |
| 2026-05-15 | 랜덤 매칭 후 캐릭터 잘못 표시 버그 수정 — Lobby 씬 CharPreview 오브젝트가 실제 유닛 프리팹(NetworkTransform 자동 추가됨) 인스턴스여서 Host의 캐러셀 위치가 Red 클라이언트로 동기화되던 원인 확정. Unpack Completely 후 UnitView/AnimationEventRelay/NetworkUnit/NetworkTransform/NetworkObject 5종 제거. |
| 2026-05-15 | 혼잡도 기반 유닛 분산 시스템(v2) 완료 — 모든 유닛이 세로 줄지어 이동하는 현상 개선. 타일별 혼잡도 누적(CongestionMap) + 혼잡도 가중 A*(CongestionAwarePathfinder) 도입. CastleApproachManager(v1) 폐기 삭제. 설정값(DecayInterval/CongestionWeight)은 GameConfig에 통합. 사용자 테스트 PASS. |
| 2026-05-16 | 랠리포인트 깃발 팀별 표시 분리 완료 — 클라이언트 설정 시 호스트에도 깃발이 표시되던 버그 수정. RallyPointChangedEvent에 TeamId 추가, ProductionTicker에 로컬 팀 필터 추가. 싱글플레이 영향 없음. |
| 2026-05-16 | 유닛 생산 실패 피드백 시스템 완료 — 골드 부족/인구 초과/큐 초과 시 토스트 메시지 표시. 유닛별 생산 비용 텍스트 빨간색, HUD 인구수 텍스트 빨간색. 자동 생산 자원 부족 시 즉시 취소(IsCharged=false만). 범용 ToastUI 시스템(DontDestroyOnLoad, 큐, DOTween 페이드아웃) + ToastMessageConfig ScriptableObject 신규 구현. |
| 2026-05-16 | 건물 배치 패널 실패 피드백 + UI 개선 완료 — 골드 부족 시 건물 비용 텍스트 빨간색(OnResourceChanged 실시간 갱신) + 토스트 메시지 + 팝업 유지. 비용 텍스트 'G' 접미사 제거(숫자만 표시). ToastUI SetActive 버그 수정(ClearAll/FinishCurrent의 SetActive 제거 → blocksRaycasts+interactable로 대체). |
| 2026-05-17 | 건물 생성/파괴 시 유닛 이동 멈춤 수정 완료 — 경로 재계산 시 코루틴을 즉시 재시작하지 않고 _pendingPath 예약 방식으로 교체. 앞 타일이 막힌 경우에만 즉시 재시작. UnitView.cs 단독 수정. |
| 2026-05-17 | 자동 생산 Rule 20 슬롯0 확장 완료 — 슬롯0에서 수동 A 생산 중 A 자동등록 시 슬롯1에 중복 추가 없이 슬롯0을 자동으로 전환(CurrentIsAuto=true). 완료 후 자동 순환 자연 시작. GameSystemRules.md 규칙 20 문구 업데이트. |
| 2026-05-17~18 | 건물 업그레이드 시스템 완성 — BuildingType enum 26종 확장(단일 Barracks → 종족별 생산라인×3단계). BuildingTypeHelper.cs 신설(Domain: IsProductionBuilding/GetStage/GetNextStage/CanUpgrade). BuildingData.Stage 파생 프로퍼티. BuildingStats.GetUpgradeCost() + _totalInvestedCostCache(누적 투자비). GameEvents.OnBuildingUpgraded 이벤트. BuildingPlacementUseCase.UpgradeBuilding(). BuildingFactory.UpgradeBuildingObject(). NetworkBuildingController RequestUpgradeServerRpc/UpgradeBuildingClientRpc. ProductionPanelUI BuildingUnitMapping + requiredStage 단계별 잠금 + UpgradeButton + ToastKey.UpgradeRequired. GameBootstrapper 누적 투자비 캐싱. 신규 3D 에셋(HumanBarracks2/3, AncientGrove1/2/3, PrimalSanctuary1/2/3) Blue/Red 완료. |
| 2026-05-18 | 건물 스탯 전체 확정 + BuildingStatsConfig 완성 — StatsReference.md 기준으로 3종족 전체 건물(기지/채굴소/배럭/방어포탑/특수) HP·비용·공격력·업그레이드비용·힐량 확정. BuildingStatsConfig.asset 3개 항목 → 32개 BuildingType 전체 채움. BuildingTypeEntry에 종족별 AttackCooldown(float) 필드 추가, BuildingStats.GetAttackCooldown() API 신규. AutoTower 쿨다운 적용(Human/Trans 5.0s, Spirit 3.5s). MistShrine 힐량 1 HP/s 범위 3 확정. |
| 2026-05-18 | ProductionPopup UI 레이아웃 재구성 완료 — BuildingIconEntry 팀별 Sprite 분리(blueIcon/redIcon), 2유닛 건물 [유닛1][빈슬롯][유닛2] 레이아웃(CanvasGroup alpha=0 방식), UpdateButtonPortraits() 2유닛 슬롯 매핑 수정, HeaderText 건물 이름 동적 표시, 철거 환불 누적 계산(GameBootstrapper 초기화 시 체인 순회 캐싱 → BuildingStats.GetTotalInvestedCost()). |
| 2026-05-18 | 2/3단계 건물 랠리 마커 미표시 버그 수정 — ProductionTicker가 OnBuildingUpgraded를 구독하지 않아 업그레이드된 건물이 UnitProductionUseCase._states에 미등록되는 문제. OnBuildingUpgraded 핸들러 추가: UnregisterBarracks(OldBuildingId) + RegisterBarracks(NewBuilding). 전 종족(Human/Spirit/Transcendence) 테스트 통과. |
| 2026-05-18 | 건물 철거 시스템 완료 — 생산 건물 철거 버튼(50% 골드 환불 + 생산 큐 전액 환불). UnitProductionUseCase.CancelAllQueue() 신규(랠리포인트 제거 → 진행 중 유닛 환불 → PendingQueue IsCharged 항목 환불 → UnregisterBarracks). BuildingPlacementUseCase.DemolishBuilding() 신규(OnBuildingDied 발행 → RemoveBuilding). 멀티: RequestDemolishServerRpc/DemolishBuildingClientRpc. BuildingFactory OnBuildingDied 구독(B방식: _buildingObjects Dict O(1) GO 파괴). BuildingView.cs + MiningEffectView.cs 삭제(미사용 코드 정리). 채굴소 철거 UI는 별도 작업으로 연기. |
| 2026-05-18 | OnEntityDied 이벤트 분리 리팩토링 완료 — 단일 공용 이벤트(OnEntityDied/EntityDiedEvent) 삭제 → OnUnitDied(UnitDiedEvent) + OnBuildingDied(BuildingDiedEvent) 강타입 이벤트로 분리. 발행 4곳·구독 9곳 전면 교체(13개 파일). 구독자의 is-캐스팅 타입 필터 전면 제거. NetworkCombatController 서버 구독 단일→2개 분리. RPC 시그니처(EntityDiedClientRpc)는 호환성 유지. |
| 2026-05-18~19 | 비생산 건물 공용 액션 패널 UI 완료 — BuildingPanelBase 추상 베이스(Template Method 패턴) 신규. BuildingActionPanelUI(비생산 건물 팝업 + 철거 버튼) 신규. ProductionPanelUI BuildingPanelBase 상속 리팩토링. InputHandler CanShowActionPanel 분기 추가. GameBootstrapper 비생산 건물 환불 캐시 루프 추가. SetupBuildingActionPanelUI 에디터 스크립트 신규. |
| 2026-05-19 | 인게임 설정 메뉴 + 게임 포기 기능 완료 — InGameSettingsUI(IGameUI, 싱글 일시정지 timeScale=0, SharedBackground 등록) + ConfirmPopup(범용 확인 팝업, BlockingOverlay 공유 Background 차단) + GameEndUseCase.Forfeit()(싱글 포기) + NetworkGameEndController.ForfeitServerRpc(RequireOwnership=false, AnnounceWinnerClientRpc 재사용) + SetupInGameSettingsUI 에디터 스크립트(HUD 재배치 + 패널 생성 + 배선). AnimatedPanel._backgroundOverlay 배선 추가. |
| 2026-05-23 | AuthSystemRules.md 작성 완료 — Firebase Auth 기반 로그인 시스템 설계 규칙 확정. 로그인 방식 3종(익명/Google Play Games/이메일+비밀번호), Firebase UID → UGS 브릿지(SignInWithCustomIdAsync), 백엔드 Option A(Firebase 생태계: Firestore 실시간 리더보드 + Google Play Billing IAP) 확정. Login.unity 별도 씬으로 분리 예정. |
| 2026-05-24 | 로그인 시스템 C# 구현 완료 — Firebase SDK v13.11.0 + GPGS v2.1.0 설치. 신규 파일 11개(FirebaseAuthService, LoginUseCase, AccountLinkUseCase, LoginBootstrapper, LoginRootView, LoginSelectView, EmailLoginView, SignUpView, EmailVerifyView, PasswordResetView, AnonymousWarningPopup) + 기존 2개 수정(UnityServicesInitializer 폴백 익명 로그인 추가, ProfileView 구현). 컴파일 에러 전체 해결(CS0103/CS0029/CS1061/CS0234). 기존 UGS 익명 로그인 폴백으로 멀티플레이 기능 정상 동작 유지. Firebase/Google 기능은 컴파일만, 런타임 활성화는 추후 진행 예정. SignInWithCustomIdAsync 미지원으로 Firebase→UGS 브릿지는 임시 익명 로그인 처리. |
| 2026-05-24 | UGS 401 Unauthorized 버그 수정 — Lobby/Relay API 호출 시 HTTP 401 에러로 매칭 실패. 원인: IsSignedIn=true(기기 캐시)이지만 서버 토큰이 만료된 상태에서 재로그인을 건너뛰어 만료된 토큰으로 UGS API 호출. UnityServicesInitializer.InitializeAsync()를 항상 SignOut() → SignInAnonymouslyAsync() 순으로 수정하여 매 초기화 시 유효한 토큰 보장. 커스텀 게임 + 랜덤 매칭 모두 정상 동작 확인. |
| 2026-05-25 | 공통 UI 규칙 수립 및 CanvasGroup Rule 5 전환 완료 — 공통 UI 규칙 10개 확정(GameSystemRules.md). 로비 7개 뷰(LobbyRootView, BattleMainView, CustomGameView, CustomHostView, CustomJoinView, RandomMatchView, ProfileView) SetActive → CanvasGroup(alpha/blocksRaycasts/interactable) 전환. 랜덤 매칭 대기화면 GameObj inactive 버그 Inspector 직접 수정. |
| 2026-05-26 | 로비 배경 Safe Area 수정 완료 — LobbyRoot Image가 SafeAreaContainer 안에 위치해 Safe Area 경계에서 배경이 끊기는 버그 수정. Canvas 직속 자식 LobbyBackground 오브젝트 신규 추가(전체화면 stretch, 남색), LobbyRoot Image 비활성화. FixLobbyBackground.cs 에디터 스크립트로 적용. 실기기 테스트 PASS. |
| 2026-05-27 | 로비 CanvasGroup Rule 5 전환 실기 테스트 완료 — TC-SINGLE-001~014 전체 PASS. TC-SINGLE-001(기본 탭 표시), TC-SINGLE-002~005(탭 전환), TC-SINGLE-006~008(커스텀 게임 흐름), TC-SINGLE-009~010(에러 메시지 표시/사라짐), TC-SINGLE-011(방 참가), TC-SINGLE-012~014(랜덤 매칭). TC-SINGLE-015~016(로그인 섹션 전환)은 로그인 미구현으로 SKIP. |
| 2026-05-29 | BuildingActionPanelUI 씬 계층 재설계 + 런타임 슬롯 제어 완료 — TC-SINGLE-BAP-001 FAIL(패널 높이, X버튼 위치 불일치) 해결. BuildingPlacementUI와 동일한 3x3 VLG+HLG 그리드 구조. 래퍼 anchoredPosition/sizeDelta 오프셋 제거. CancelButton 위치 통일. BuildingActionPanelUI.cs에 _allSlotButtons/_activeSlotButtons 필드 추가, OnShow() 오버라이드에서 런타임 CanvasGroup alpha 제어(BuildingPlacementUI._buttonCanvasGroups 패턴 동일). HeaderText 앵커 순수 앵커 기반 변환. |
| 2026-05-30 | 로비 UI 에셋 제작 완료 — TabBar 아이콘 4종(Battle/Shop/Profile/Ranking), 공통 아이콘 2종(Settings/Quit), 로비 버튼 아이콘 9종(SinglePlay/RandomMatch/CustomGame/CreateRoom/JoinByCode/Email/Logout/Back/Cancel), 버튼 배경 2종(Primary/Secondary), 랜덤 매칭 스피너 1종(HexOrb). 총 18종 에셋. 저장 경로: Assets/_Project/Sprites/UI/. AssetList.md 업데이트 완료. |
| 2026-05-29 | BuildingPlacementUI 씬 계층 재설계 완료 — TC-SINGLE-BP-001/002 FAIL(패널 높이 부족, 버튼 테두리 침범, 골드 아이콘 정렬 불일치) 해결. GridLayoutGroup 제거 → VLG+HLG 중첩 구조(GameSystemRules Rule 2). BuildingPanel anchor=(0,0)~(1,0.4). GridContainer anchor=(0.08,0.123)~(0.92,0.864). CancelButton anchor=(0.883,0.852)~(0.993,0.97). 모두 순수 앵커 기반(anchoredPosition=0, sizeDelta=0). 버튼 내부: IconImage(flexibleWidth=6) + CostContainer(flexibleWidth=4) → GoldIcon(ui_icon_gold, 44px) + CostText(Maplestory Light SDF). BuildingPlacementUI.cs _buildingGoldIcons 필드 추가. GameSystemRules.md Rule 2 Layout Group 반응형 패턴 보완. GameSystemRules Rule 2/4/5/6 전체 준수 검증 완료. RebuildBuildingPlacementUI.cs 에디터 스크립트 신규. |
| 2026-05-31 | 건물 업그레이드 생산 상태 처리 오류 수정 완료 — 업그레이드 시 생산 중 골드 환불 누락 + 랠리포인트 초기화 버그 2건 수정. ProductionTicker.OnBuildingUpgraded()에서 UnregisterBarracks → CancelAllQueue 교체(환불 포함), CancelAllQueue 호출 전 RallyPoint 저장 후 RegisterBarracks 완료 시점에 SetRallyPoint로 복원. 수정 파일: ProductionTicker.cs 1개. 실기 테스트 PASS(골드 환불 확인, 랠리포인트 유지 확인). |
| 2026-06-01 | 방어 타워(AutoTower) 공격 기능 구현 완료 — TowerCombatUseCase 신규(Application 레이어). 종족별 스탯(사거리 4.0/공격력 15, 쿨다운 Human·Trans 5.0s/Spirit 3.5s). 타겟 선택: 월드 좌표 기준 가장 가까운 적 유닛. 배치 즉시 공격 가능(쿨다운 0 초기값). 멀티플레이 서버 권위 처리(NetworkCombatController.TickCombat에 타워 Tick 추가). 실기 테스트 PASS. |
| 2026-06-05 | 자동생산 재등록 슬롯 중복/누락 버그 구조 개선 — CurrentIsAuto를 수동 필드에서 파생 계산 getter로 전환(IsAutoMode와 동일 패턴). AutoTypes에서 타입 제거 시 getter가 자동으로 false 반환 → UnregisterAutoType·DisableAutoMode 수동 reset 불필요. RegisterAutoType에 PendingQueue.Count==0 조건 추가로 큐에 다른 항목 있을 때 슬롯3 추가 허용. GameSystemRules 규칙 20 보완. TC-01~06 정적분석+실기 전체 PASS. |
| 2026-06-05 | 자동생산 완료 사이클 슬롯2 깜빡임 버그 수정 — 자동생산 완료 시 재순환 항목이 슬롯2에 1프레임 표시되다가 사라지는 버그. CompleteProduction에서 ChargeVisibleSlots+OnProductionQueueChanged 직접 발행 제거 → TryStartNext 즉시 호출로 대체. 2026-04-19 AddNewAutoSlot 수정과 동일 패턴(완료 사이클 경로가 미처 처리되지 않았던 것). 실기 테스트 PASS. |
| 2026-06-02 | Human CannonTower 초기 방향 설정 완료 — BuildingFactory.GetInitialRotation() 신규. "내 진영 vs 상대 진영" 기준으로 회전 결정(Blue/Red 팀 색깔 기준 아님). ViewConverter.IsFlipped로 로컬 팀 판별 → 상대 포탑 Y180도, 내 포탑 기본값. 실기 테스트 PASS. |
| 2026-06-02 | UnitStatsConfig 미사용 필드 제거 + 스탯 정비 완료 — AttackKind enum/Kind 필드/GetAttackKind() 제거(2026-05-11 비활성화 코드 삭제). occupancySize 제거(TileOccupancyManager 미구현). 유닛 9종 attackCooldown/hitFrameTimes를 StatsReference.md 기준으로 정비. StatsReference.md 업그레이드 비용 표기 출발점 기준으로 통일. ApplyStatsReference.cs 에디터 스크립트로 일괄 적용. |
| 2026-06-05 | BuildingView Missing Script 정리 완료 — Spirit/Transcendence 건물 프리팹 8개에서 삭제된 BuildingView 컴포넌트 참조 제거. Editor 스크립트 1회 실행으로 일괄 처리. |
| 2026-06-05 | 신규 유닛 프리팹 컴포넌트 부착 에디터 스크립트 완료 — Human 5종·Spirit 6종·Transcendence 5종 × Blue/Red 총 32개 프리팹에 UnitView/AnimationEventRelay/NetworkUnit 등 컴포넌트 자동 부착. Assets/Editor/Setup/SetupNewUnitPrefabs.cs. 실기 테스트 예정. |
| 2026-06-06 | NetworkGameManager 고아 필드 + Game씬 중복 NGM 제거 완료 — GameBootstrapper._networkGameManager SerializeField(코드 사용처 없는 고아 필드) 제거. Game.unity에 중복 배치된 NGM GameObject 제거(DontDestroyOnLoad 구조상 Lobby씬 NGM이 유지되므로 불필요). 싱글플레이+멀티플레이 실기 PASS. 콘솔 DontDestroyOnLoad 경고 제거. |
| 2026-06-08 | 멀티플레이 유닛 사망 GO 미파괴 + 이펙트 미재생 버그 수정 (NGO Despawn 패턴 정립) — 근본 원인: 서버 UnitView(Presentation)의 Destroy(gameObject) 호출이 NGO Despawn 메시지를 클라이언트에 전파하지 않음. 수정: NetworkCombatController(Infrastructure)에서 EntityDiedClientRpc 발행 직후 NetworkObject.Despawn(destroy:true) 명시 호출. UnitView에서 Unity.Netcode 직접 참조 완전 제거(레이어 규칙 준수 — NetworkContext 홀더 패턴으로 교체). 런타임 로그로 검증: 13킬 전체 OnNetworkDespawn 게임플레이 중 발생 + 이펙트 재생 완료 확인. 수정 파일: NetworkCombatController.cs, UnitView.cs, NetworkUnit.cs, EffectManager.cs. |
| 2026-06-08 | 유닛 VFX 디테일 개선 3종 완료 — ① VFX 프리팹 3개(vfx_pistoleer_attack/vfx_tank_attack/vfx_unit_death) ParticleSystem ScalingMode Local→Hierarchy 일괄 변환(VfxScalingModeFixer 에디터 스크립트). 루트 Transform Scale로 이펙트 크기 조절 가능. ② 피스톨러 공격 VFX 스폰 위치 개선 — UnitView `_vfxSpawnPoint` 필드 추가, VfxSpawnPoint GO(스켈레톤 본 손 부위 하위, 총구 위치)의 position만 참조 / rotation은 `Quaternion.LookRotation(transform.forward)` 고정(본 하위 배치로 _vfxSpawnPoint.rotation에 본 회전 ~(0,-90,-90)이 섞이는 문제). EffectManager.PlayUnitAttack 시그니처에 Quaternion rot 파라미터 추가. ③ vfx_unit_death 퍼짐 효과 제거 — 3개 PS startSpeed 모두 0으로 YAML 직접 수정. |
