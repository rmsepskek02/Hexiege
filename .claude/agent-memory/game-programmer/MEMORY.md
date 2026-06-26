# Game Programmer Agent Memory

> 이 파일은 200줄 이내 핵심 요약만 유지한다. 상세 내용은 토픽 파일 참조.

---

## CRITICAL — GIT 명령 절대 금지
- **모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## CRITICAL — 레이어 제약 (상세: architecture.md)
- Domain: `using Hexiege.Core` 금지, UnityEngine 참조 금지 → 정적 홀더 패턴(HexOrientationContext 등)
- Application: Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더
- NetworkBehaviour / Unity.Netcode: **Infrastructure 레이어 전용** (Presentation/Application 금지)
- Infrastructure→Presentation 직접 호출 금지 → GameEvents(Subject) 이벤트 경유
- GameBootstrapper = 유일한 의존성 조합 루트. Assembly Definition 없음 — 네임스페이스 규약만
- `Hexiege.Application`이 `UnityEngine.Application`을 가림 → `UnityEngine.Application.xxx` 명시 필요

## CRITICAL — NGO API 제약 (상세: network.md)
- ServerRpc/ClientRpc 메서드명은 반드시 `ServerRpc`/`ClientRpc`로 끝나야 함
- NGO 2.9.2, Enable Scene Management = ON. NetworkObject는 씬 루트에 생성
- RPC 파라미터: 직렬화 가능 타입만. NGO 2.9.x bool? nullable 비교 필수
- 클라이언트 전용 분기: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`
- **GO 파괴 전파**: 서버에서 `NetworkObject.Despawn(destroy:true)` 명시 호출. `Destroy(gameObject)`는 NGO 클라 전파 불보장

## CRITICAL — DontDestroyOnLoad (상세: architecture.md / ui-system.md)
- 루트 GameObject에만 작동. 자식 배치 시 씬 전환마다 재생성+즉시파괴 반복
- DontDestroyOnLoad 오브젝트는 생성 씬 하나에만 배치. SetActive(false)면 Awake 미호출→미등록(숨김은 CanvasGroup.alpha=0)

---

## 최근 작업 (상세 전체는 work-history.md)

### 게임포기 로딩 인디케이터 미해제 버그 수정 (2026-06-26) ✅
멀티플레이 포기 시 `OnForfeitConfirmed()`에서 `ShowLoading(true)` 호출 후 씬 전환이 없어 꺼지지 않던 문제. 포기는 씬 전환 없이 GameEndUI만 표시하므로 ShowLoading 호출 자체를 제거. GameSystemRules_UI.md 규칙 L-2에서 "게임 포기(멀티)" 항목도 함께 제거.

### 랜덤 매칭 2회차 실패 — GameEndUI NGM null 참조 (2026-06-25) ✅
GameEndUI `_networkGameManager` Inspector 미연결(null) → ReturnToLobby에서 BackToLobby 미호출 → NetworkManager.Shutdown 없이 씬 전환 → 2번째 매칭 시 IsListening=True로 StartHost 재호출("Cannot start Host while an instance is already running"). 수정: GameEndUI.Initialize()에 `FindFirstObjectByType<NetworkGameManager>()` 자동 탐색 추가(LobbyUI 동일 패턴). DontDestroyOnLoad 오브젝트는 Inspector 연결 불안정 → 자동 탐색 우선. (상세: network.md)

### RuntimeLogger 유틸리티 생성 (2026-06-25) ✅
`Infrastructure/Debug/RuntimeLogger.cs` 신규. BeginSession(folderPath, role)/Log(level, system, className, message, data)/EndSession() API. `#if UNITY_EDITOR` 파일 기록, 항상 Debug.Log 출력(Logcat 대응). task: `_Tasks/2026-06-25/07_25_runtime-logger/`

### Setup.cs 하드코딩 배열 파생 (2026-06-25) ✅
- `GameBootstrapper.Setup.cs` 환불 캐시 초기화의 `stage1Buildings`(9개)/`nonProductionBuildings`(6개) 하드코딩 배열 → `Array.FindAll`+`BuildingTypeHelper.GetStage`/`IsProductionBuilding` 파생. `using System;` 추가. 환불 루프·동작·값 불변. 신규 생산건물은 `_buildingTable` 한 줄로 환불 캐시까지 자동 반영. 안 2(도메인 무변경) 선택. 사용자 PASS. 커밋 `8d74e06`(main). (상세: unit-building.md)

### 코드 구조 개선 Phase 2 (2026-06-25) ✅
- `BuildingTypeHelper`: IsProductionBuilding/GetStage/GetNextStage 3개 switch → 단일 `Dictionary<BuildingType, BuildingMeta>` lookup table. 신규 생산건물은 table 한 줄 추가로 끝. (상세: unit-building.md)
- `GameBootstrapper.Network.cs`: StartNetworkGame HexMetrics 수동 4줄 → `ApplyConfig(FlatTop, oc)` 1줄. ApplyConfig 멱등(멀티서 2회 실행 무해), UnitYOffset 누락 해소. (상세: hex-grid.md)
- 동작 보존 리팩토링 — SINGLE 7 + MULTI 2 전 항목 PASS. 기존 switch/수동4줄은 주석 보존(별도 지시 시 삭제). 브랜치 `claude/code-refactor-phase2-structural`(3838c4d)

### 코드 정리(클린업) Phase 1 (2026-06-23)
약 30개 파일 히스토리성 주석/폐기코드 제거. GameBootstrapper.Setup.cs 환불 캐시 `refundRaces` 지역변수 통합. 런타임 동작 불변. 구조 변경(switch→Dictionary)은 Phase 2 별도.

### 스플래시 로그인 흐름 — skipFade 모드 (2026-06-23) ✅
SplashOverlayView `_skipFadeOnTap` + `SetTapCallback(callback, skipFade=false)`. 자동 로그인 성공 시 FadeOut 없이 즉시 GoToNextScene → 로딩 인디케이터(SO=300)가 커버. 로그인 X는 기존 FadeOut 유지.

### 로그인 팝업 CloseButton 무반응 (2026-06-23) ✅
AnonymousWarningPopup/NetworkErrorPopup에 `_closeButton` 필드+OnCloseButtonClicked()→Hide() 추가. CloseButton GO가 있어도 SerializeField 필드 없으면 Inspector 연결 불가 → 무반응 패턴.

### LoadingIndicator 전수 적용 (2026-06-22~23) ✅
SceneLoader 정적 유틸(씬 전환 단일 진입점) 신규. ShowLoading은 코루틴 외부 동기 실행. Infrastructure→Presentation은 GameEvents(OnNetworkBackToLobby/OnNetworkRematchStarting) 경유. (상세: ui-system.md)

### Canvas SortingOrder + BlockingOverlay 확정 (2026-06-22) ✅
SO 0(HUD)/100(UIManager)/200(패널 Override)/250(ConfirmPopup)/300(LoadingIndicator). UIManager는 루트 GO 배치 필수. ConfirmPopup 독립 Canvas SO=250. (상세: ui-system.md)

---

## 토픽 파일 인덱스

### 신규 분류 (2026-06-23 재구성)
- [architecture.md](architecture.md) — 레이어 구조/제약, 정적 홀더, GameBootstrapper, SO Config 패턴, 에디터 스크립트 패턴, DontDestroyOnLoad
- [network.md](network.md) — NGO API 제약, RPC 래퍼 패턴, GO 파괴 전파, 같은 씬 재로드, 동기화 타이밍, 회전/위치 동기화
- [ui-system.md](ui-system.md) — UIManager, BlockingOverlay, SceneLoader, LoadingIndicator, Canvas SortingOrder, CanvasGroup/레이아웃/팝업/ToastUI 패턴
- [unit-building.md](unit-building.md) — 유닛 이동/전투 V3, 회전, 혼잡도, 다중히트, 건물 배치/철거/업그레이드/환불, 생산 PendingQueue, AutoTower, 랠리포인트
- [hex-grid.md](hex-grid.md) — 헥스 좌표계, HexMetrics, ViewConverter, 타일 소유권, 그리드 렌더링, 패스파인딩, 카메라, URP RT 잔상
- [work-history.md](work-history.md) — 완료 작업 상세 전체 (날짜 역순, 2026-03~06)

### 기존 토픽 (세부 보조 자료)
- [network-infra.md](network-infra.md) — Phase 1~8 상세 (UGS, NGO, 동기화, 팀 할당, 승패)
- [network-todo.md](network-todo.md) — 네트워크 미완성 항목
- [random-matching-bugfix.md](random-matching-bugfix.md) — 2026-03-16 랜덤 매칭 버그
- [unit-stats-and-combat.md](unit-stats-and-combat.md) — 스탯, IEntityPositionProvider, 쿨다운, 클라 시각 동기화
- [combat-fixes.md](combat-fixes.md) — ClaimedTile 공격 위치 보정, UnitView 회전
- [attack-direction-refactor.md](attack-direction-refactor.md) — 공격 방향 리팩터링(2D→3D)
- [rendering-and-animation.md](rendering-and-animation.md) — UnitView 애니메이션, Shader Graph, HexTileView, 팀 프리팹
- [3d-transition.md](3d-transition.md) — XZ 좌표계 전환, Phase별 수정 파일
- [camera-and-view.md](camera-and-view.md) — 카메라 틸트, ViewConverter, 경계 클램프
- [gameplay-systems.md](gameplay-systems.md) — 랠리포인트, 초상화 동적 업데이트

---

## 핵심 패턴 요약

### 팀 매핑
- TeamId: Neutral=0, Blue=1, Red=2. Host→Blue, Client→Red
- TeamAssigner 삭제됨(2026-03-20) — NetworkGameFlow에서 `IsHost ? Blue : Red` 직접 할당

### 유닛 애니메이션
- Animator.Play() 직접 호출(트랜지션 우회). 파라미터 IsDead(bool) 1개만. Root Motion OFF
- **Animator Controller 상태 m_Speed 주의**: 기본값 0이면 첫 프레임 동결. 새 상태 추가 시 m_Speed=1 확인

### 거리 비교
- 월드 거리(float) 대신 `HexCoord.Distance`(도메인 정수) 우선 — ViewConverter 무관, 부동소수점 오차 없음

### 미사용 코드 정리
- 미사용 필드 확인 시 주석 언급만 믿지 말고 코드베이스 전체 Grep 필수
- 비활성화(주석) 우선, 테스트 통과 후 삭제 (WORKFLOW 규칙)
