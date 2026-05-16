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
