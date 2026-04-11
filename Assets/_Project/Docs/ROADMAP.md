# Hexiege - 작업 로드맵

**최종 수정일:** 2026-04-11
**현재 단계:** UnitType 개편 + 근접 사거리 시스템 완료. 근접 유닛이 Castle 타일 방향으로 접근하며 공격, 다중 유닛 연속 공격, 종족별 생산 패널 동적 바인딩 완료. 생산 패널 Spirit/Transcendence 초상화 스프라이트 미제작.

---

## 우선순위 요약

| 우선순위 | 작업 | 카테고리 | 예상 규모 |
|---------|------|---------|---------|
| ~~🔴 높음~~ | ~~팀별 프리팹 코드 연동 (UnitFactory/BuildingFactory 팀별 분기)~~ | ✅ 완료 (2026-03-14) | - |
| ~~🔴 높음~~ | ~~UnitType Assault/Sniper 추가 + 생산 UI 연동~~ | ✅ 완료 (2026-03-14) | - |
| 🟡 중간 | BuildFailed/EnqueueFailed UI 피드백 | UI 기획 후 진행 | 소 |
| ~~🟡 중간~~ | ~~TechnicalDesignDocument.md 3D 업데이트~~ | ✅ 완료 (2026-03-09) | - |
| 🟡 중간 | 게임 내 밸런싱 (골드/HP/생산시간) | 기획 | 중 |
| ~~🟡 중간~~ | ~~추가 유닛 타입 에셋 제작~~ | ✅ 에셋 완료 (2026-03-13) | - |
| ~~🟡 중간~~ | ~~로비 씬 빌드 (LobbySceneBuilder 실행)~~ | ✅ 완료 (2026-03-15) | - |
| 🟡 중간 | 로비 UI 에셋 제작 + 비주얼 폴리싱 | 에셋+UI | 중 |
| ~~🟢 낮음~~ | ~~멀티플레이 로비 UI 완성~~ | ✅ MVVM 코드 완료 (2026-03-15) | - |
| 🟢 낮음 | 재접속 실제 구현 | 기능 | 중 |
| ~~🟡 중간~~ | ~~3종족 시스템 — 종족 선택 UI + 인게임 종족별 유닛/건물 반영~~ | ✅ 완료 (2026-04-07) | - |
| ~~🟡 중간~~ | ~~UnitType 개편 + 근접 사거리 시스템~~ | ✅ 완료 (2026-04-11) | - |
| 🟡 중간 | 생산 패널 Spirit/Transcendence 초상화 스프라이트 제작 + 연결 | 에셋+UI | 소 |
| 🟡 중간 | Spirit/Transcendence HP/ATK/생산비용 확정 (StatsReference.md) | 기획 | 소 |
| ⬜ 백로그 | 방어/마법 타워 | 기능 | 대 |
| ⬜ 백로그 | 사운드/BGM | 기능 | 중 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | PlayFab 백엔드 | 기능 | 대 |

---

## Phase A — 네트워크 버그 수정 (긴급)

현재 멀티플레이에서 발생하는 알려진 버그/미완성 항목들.

### A-1. BuildFailed/EnqueueFailed UI 피드백 누락
- **파일**: `NetworkBuildingController.cs`, `NetworkProductionController.cs`
- **증상**: 건물 배치/생산 큐 실패 시 사용자에게 아무 피드백 없음 (서버 로그만 출력)
- **현황**: `BuildFailedClientRpc` / `EnqueueFailedClientRpc` RPC 구조는 완성. 함수 내부에 UI 호출만 추가하면 됨
- **대기 이유**: 전반적인 UI 기획(토스트/팝업 디자인 등)을 먼저 진행한 후 구현 예정

---

## Phase B — 네트워크 미완성 기능

### B-2. 로비 씬 분리 + MVVM UI ✅ 완료 (2026-03-15)
- **구조**: Lobby.unity(신규) + Game.unity(기존) 씬 분리 완료
- **MVVM 아키텍처**: LobbyViewModel(탭), BattleViewModel(게임모드) + UniRx ReactiveProperty
- **구현된 View**: LobbyRootView, TabBarView, BattleRootView, BattleMainView, CustomGameView, CustomHostView, CustomJoinView, RandomMatchView, ShopView(플레이스홀더), ProfileView(플레이스홀더), RankingView(플레이스홀더)
- **게임 모드**: 싱글플레이 / 커스텀게임(코드 방 만들기/참가) / 랜덤매칭(추후 구현)
- **남은 작업**: UI 에셋(버튼/패널 스프라이트) 제작 후 비주얼 폴리싱

### B-3. 재접속 실제 구현
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs`
- **현황**: 30초 대기 후 ForceWin만 구현
- **구현 필요**: NGO Reconnect API 활용, 재접속 후 게임 상태 복원

---

## Phase C — 게임플레이 완성도

### C-1. 게임 내 밸런싱
현재 수치는 임시값. 플레이테스트 후 조정 필요.

| 항목 | 현재값 | 조정 방향 |
|------|--------|---------|
| 시작 골드 | 500 | 테스트 후 결정 |
| 채굴소 수입 | 10골드/초 | 타일 경제 밸런스 체크 |
| Pistoleer HP/공격/사거리 | 30 / 6 / 1.0 | 2026-03-14 재확정 (DPS=3, cooldown≈2.0s) |
| Assault HP/공격/사거리 | 50 / 1 / 2.0 | 2026-03-14 재확정 (DPS=5, cooldown≈0.2s) |
| Sniper HP/공격/사거리 | 30 / 10 / 5.0 | 2026-03-14 재확정 (DPS≈3.3, cooldown≈3.0s) |
| Castle HP | 50 | 게임 시간 조정 |

### C-2. 추가 유닛 타입 코드 연동
- **에셋 완료 (2026-03-13)**: Assault(돌격소총병), Sniper(저격총병) Blue/Red 프리팹 제작 완료
- **남은 코드 작업**:
  1. `UnitType.cs`: `Assault = 1`, `Sniper = 2` enum 추가
  2. `UnitFactory.cs`: `_unitPrefab` 단일 필드 → `Dictionary<(UnitType, TeamId), GameObject>` 또는 팀+타입별 Inspector 필드로 확장
  3. `BuildingFactory.cs`: `_castlePrefab` / `_barracksPrefab` → Blue/Red 팀별 분기 추가
  4. `ProductionPanelUI.cs`: Assault/Sniper 생산 버튼 추가 (팀별 초상화 동적 업데이트는 2026-03-14 완료)
  5. Assault/Sniper UnitStats 정의 (HP/공격력/사거리/생산시간/비용)

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 3종족 시스템
- 각 종족마다 고유 유닛/건물/패시브 차별화
- 매칭 화면에서 종족 선택

### D-2. 방어/마법 타워
- 건설 후 자동 공격
- 방어 타워: 단일 타겟, 직선 사거리
- 마법 타워: 범위 공격, 마나 자원 추가 필요 가능성

### D-3. 건물 업그레이드 시스템
- Castle/Barracks/MiningPost 레벨업
- 골드 소모 + 생산 시간/수입/HP 증가

### D-4. 유닛 AI 상태머신
- 현재: 이동 중 인접 적 자동 공격 (하드코딩)
- 목표: Idle → Patrol → Chase → Attack → Retreat 상태 전환

---

## Phase E — 플랫폼/폴리싱

### E-1. 사운드/BGM
- BGM (로비/인게임/승리/패배)
- 효과음 (공격, 건물 건설, 골드 획득, 유닛 사망)

### E-2. 튜토리얼
- 첫 실행 시 인터랙티브 튜토리얼
- 헥스 클릭 → 건물 건설 → 유닛 생산 → 공성 흐름 안내

### E-3. PlayFab 백엔드
- 계정 시스템 (로그인/회원가입)
- 랭킹 (승/패 기록)
- 인앱결제 (스킨/종족 언락)

---

## 문서 관리 워크플로우

새 작업 시작 전 반드시:

1. `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/research.md` 작성
   - 관련 코드 파악, 영향 범위, 현재 상태 정리
2. `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/plan.md` 작성
   - 구현 접근법, 파일별 변경 내용, 위험 요소
3. 사용자 승인 후 구현 시작

---

## 완료된 마일스톤

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
| 2026-03-14 | 유닛 회전 DOTween 보간 완료 (이동/공격 모두 DORotate + Ease.OutQuad, _rotationDuration SerializeField) |
| 2026-03-14 | 공격 후 Walk 복귀 버그 수정 (타겟 소멸 시 Play(StateWalk) 명시 호출, 멀티/싱글 공통) |
| 2026-03-15 | 로비 씬 분리 MVVM 완료 (Lobby/Game 씬 분리, UniRx 기반 MVVM, 탭 4개 + 전투 서브화면 5개, 씬 빌드 완료) |
| 2026-03-15 | 멀티플레이 게임 시작 버그 수정 (Host OnClientConnectedCallback 누락 → HandleClientConnected 추가) |
| 2026-03-16 | 랜덤 매칭 게임 씬 전환 버그 수정 (GetHashCode 크로스-프로세스 비결정성 → GetStableHash polynomial hash 교체, OnClientConnectedCallback 등록 순서 수정) |
| 2026-03-17 | 전역 로딩 스크린 완료 (LoadingScreen.cs 싱글턴, Lobby 씬 UI, 싱글플레이 2초 딜레이, 커스텀/랜덤매칭 sceneLoaded 자동 Hide) |
| 2026-03-17 | 멀티플레이 로비 복귀 버그 수정 (Inspector _lobbySceneName 값 오류 발견, 로컬 독립 처리로 설계 변경, 30초 자동 복귀 타이머 추가) |
| 2026-03-17 | 커스텀게임 재경기 시스템 완료 (양측 동의 재경기, RematchRequestPopup 신규, NGO SceneManager.LoadScene 재로드, 레이스 컨디션 처리) |
| 2026-03-18 | 랜덤매칭 재경기 지원 완료 (GameEndUI.SetupRematchButton 랜덤매칭 버튼 숨김 제거 → 커스텀게임과 동일 흐름) |
| 2026-03-18 | 건물 인근 타일 이동/공격 불가 버그 수정 (HexPathfinder goal blocked 체크 제거, UnitCombatUseCase maxDist Epsilon=0.05f 추가) |
| 2026-03-19 | 카메라 줌 DOTween 보간 완료 (CameraController _targetZoom + DOTween.To Ease.OutCubic, _zoomDuration SerializeField) |
| 2026-03-19 | UI DOTween 애니메이션 프레임워크 완료 (UIAnimator static 헬퍼 + AnimatedPanel 컴포넌트 / GameEndPanel=SlideFromTop, ProductionPopup/BuildingPopup=SlideFromBottom / RematchRequestPopup blocksRaycasts 버그 수정) |
| 2026-03-20 | 코드 정리 완료 (TeamAssigner.cs 삭제, LocalPlayerTeam/NetworkGameFlow 주석 정리, GameBootstrapper IsNetworkMode() 헬퍼 추출) |
| 2026-03-20 | 싱글플레이 ViewConverter 초기화 버그 수정 (ViewConverter.Reset() → LocalPlayerTeam 기반 Setup(isRed, mapCenter), ApplyConfig() 직후 호출) |
| 2026-03-23 | 자동/수동 생산 하이브리드 시스템 완성 (전역 규칙 5가지 구현, IsCharged 기반 골드 차감 추적, BUG-01~13 수정, 실기 테스트 전 케이스 PASS) |
| 2026-03-24 | Game UI Lifecycle Framework 완료 (IGameUI 인터페이스 + GameUIManager, 게임 종료/재시작 UI 일괄 제어, 멀티플레이 클라이언트 팝업 미닫힘 BUG-1 수정) |
| 2026-04-06 | 로비 종족 선택 UI 완료 (RaceId enum 3종족, RaceSelectionViewModel/View 캐러셀, RenderTexture 캐릭터 미리보기, DOTween 전환, Walk/Idle CrossFade 1초) |
| 2026-04-06 | 종족명 자연→초월 변경 (Nature→Transcendence enum 전체 rename) |
| 2026-04-06 | Pistoleer Idle 애니메이션 버그 수정 (Pistoleer.controller Idle m_Speed 0→1) |
| 2026-04-07 | 종족 인게임 적용 완료 (UnitFactory/BuildingFactory 종족별 6세트 분기, MiningPost 종족별 분기, 오브젝트 이름 실제 프리팹명 반영, 에디터 자동 연결 스크립트, 싱글/멀티 실기 PASS) |
