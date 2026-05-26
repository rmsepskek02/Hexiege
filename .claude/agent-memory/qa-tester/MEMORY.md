# QA Tester Memory — Hexiege

## ⚠️ TC 작성 형식 규칙 (CRITICAL — README.md 공식 규칙, 2026-03-24 확정)

### TC 구조 (4구조 자연어 필수)
모든 TC는 아래 4구조로 작성한다. **코드 변수명/함수명 노출 금지.**

```
### [TC-ID]: [제목]

**전제:** 테스트 시작 시점의 상태 (자연어)

**동작:**
1. 사용자가 하는 행동 (자연어)

**기댓값:**
- 예상되는 결과 (자연어)

**결과:** PASS / FAIL / CONDITIONAL PASS
```

### TC ID 접두사 규칙
- 싱글플레이 전용: `SINGLE-1`, `SINGLE-2`, ...
- 멀티플레이 전용: `MULTI-1`, `MULTI-2`, ...
- 성능/부하: `PERF-1`, `PERF-2`, ...

### QA 정적 분석 위치 규칙
- TC 목록과 **같은 문서 내 하단에 별도 섹션**으로 작성
- TC 항목 본문에 코드 분석 내용 혼재 금지
- 섹션 이름: `## 정적 분석 결과 (qa-tester)`

### 판정 기준 3단계
- **PASS**: 기댓값 전부 충족
- **FAIL**: 기댓값 미충족 또는 오동작 확인
- **CONDITIONAL PASS** (PASS*): 최종 수렴 보장, 실기 특정 조건 확인 필요

---

## ⚠️ 코드 리뷰 필수 범위 규칙 (CRITICAL)

타입 변경/신규 파일이 포함된 작업은 아래 순서로 반드시 진행:

1. **plan.md "수정 파일 전체 목록"을 기준**으로 변경된 모든 파일 읽기
2. **변경된 타입을 참조하는 상위/주변 파일도 함께 읽기**
   - 예: `GameObject _popup → AnimatedPanel _popup` 변경 시 → `_popup`을 사용하는 모든 코드 경로 추적
   - `SetActive()` 같은 구버전 API 호출 잔존 여부 확인
3. **각 파일의 namespace, using 선언 확인**
   - 신규 타입 사용 시 필요한 `using` 추가됐는지
   - namespace 충돌 여부
4. **타입 불일치 직접 추적**
   - SerializedField 타입과 Inspector 연결 타입 일치 여부
   - 반환 타입, 파라미터 타입 변경으로 인한 연쇄 영향
5. **타입 변경이 있는 경우 파일 읽기 전에 Grep 전수 검색 먼저 수행**
   - 변경 전 타입/API 패턴을 프로젝트 전체 .cs 파일에서 검색
   - 예: `GameObject _popup` → `AnimatedPanel _popup` 변경 시:
     - `_popup\.SetActive` 검색
     - `_popup\.activeSelf` 검색
     - `_popup\.gameObject` 검색
     - `GameObject.*_popup` 검색
   - 검색 결과가 있으면 해당 파일/라인 즉시 FAIL 기록
   - Grep으로 찾은 뒤 해당 파일을 읽어 맥락 파악
6. **컴파일 에러 수준 문제 발견 시** → testcase.md에 FAIL 기록 후 **나머지 TC도 계속 진행** — 모든 TC 결과를 빠짐없이 기록

범위가 좁으면 컴파일 에러도 놓칠 수 있음 — 연관 파일 누락 절대 금지.

---

## ⚠️ GIT 명령 절대 금지 (CRITICAL — 예외 없음)
- **`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등 모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)

## 아키텍처 패턴 (확인된 사항)
- Presentation이 Infrastructure(LocalPlayerTeam) 직접 참조: 정적 홀더 패턴으로 허용 범위
- Assembly Definition 없음 — 물리적 경계 없음, 네임스페이스 규약으로만 관리
- CameraController에 `using Hexiege.Infrastructure` 선언 필요 (GameConfig 사용 목적, 정상)

## 반복 확인 필요 항목
- 신규 UseCase/Manager 추가 시 GameBootstrapper 와이어링 누락 여부
- LocalPlayerTeam.Current 기본값 = Blue → 싱글플레이 동작 항상 확인
- 팀 기반 로직 변경 시 StartAutoMove의 하드코딩 Blue/Red 확인
- ViewConverter.IsFlipped 상태가 올바르게 초기화/리셋 되는지 확인
- **CanvasGroup Rule 5 검사**: UI 뷰의 Show/Hide에서 `SetActive(false/true)` 잔존 여부 확인 → 반드시 `CanvasGroup.alpha=0/1 + blocksRaycasts=false/true + interactable=false/true` 패턴으로 구현되어야 함 (DontDestroyOnLoad 오브젝트의 Awake 미호출 버그 + LayoutGroup 레이아웃 깨짐 방지)
- **Safe Area Rule 4 검사**: 전체화면 배경 요소(`Image`로 화면 전체를 채우는 오브젝트)가 `SafeAreaContainer` 밖(`Canvas` 직속)에 배치되어 있는지 확인 → 배경이 `SafeAreaContainer` 안에 있으면 노치/홈바 기기에서 Safe Area 경계에서 잘려 보임

## 알려진 취약 지점
- FindObjectsByType 사용처: InputHandler.StartAutoMove, InputHandler.HandleClick
  → 유닛 수 증가 시 성능 취약. 캐시 최적화 대상으로 마킹.
- IsPointerOverUI의 Debug.Log — 매 클릭마다 콘솔 출력, 프로덕션 전 제거 필요
- **3D 전환 후**: 신규 Factory 작성 시 Z-depth 기반 배치 확인 (sortingOrder 미사용)
  - 올바른 렌더 순서: 타일(Y=0) < 건물(Y=높이) < 유닛 (카메라에서 멀→가까운 순)

## 터치 입력 구조
- CameraController: EnhancedTouch 기반, OnEnable/OnDisable에서 Enable/Disable
- 팬 vs 탭 구분: InputHandler ClickThreshold=10px, CameraController는 시작부터 팬 추적
- 에디터 터치 팬 테스트 불가: Touchscreen.current == null 조건으로 차단됨 (의도적)
- 2터치 중 팬 비활성화: activeTouches.Count >= 2 가드
- Android: Mouse.current는 null이 아님(Unity가 터치→가상마우스 생성) → Touchscreen.current 사용
- **3D 전환 후**: 입력은 XZ 평면 레이캐스트 방식 (ScreenToXZPlane() 헬퍼)

## ViewConverter 방식 (확정, SetTeamView 방식 폐기)
- 카메라 Z축 180° 회전(SetTeamView) 방식은 폐기됨 — 3D 메시 뒤집힘 문제
- 채택 방식: ViewConverter.cs (Core 레이어, 정적 클래스)
  - `ToView(pos) = 2*mapCenter - pos` (Red팀: 맵 중심 기준 반전, 자기 역함수)
  - `FlipDirection(dir) = (dir + 3) % 6` (Red팀 이동 방향 반전)
  - `FromView(viewPos) = ToView(viewPos)` (역변환도 동일 공식)
  - **3D 전환 후**: Z축 반전 (`2*center.z - pos.z`), Y(높이)는 보존
- [수정됨] 올바른 초기화 순서: StartNetworkGame() → ViewConverter.Setup(isRed, mapCenter) → LoadMap()
- 리셋: LoadMap() 내부 isNetworkMode 분기 → 싱글플레이만 Reset(), 네트워크는 건너뜀

## ViewConverter 테스트 체크리스트
- Blue팀: 타일/유닛/건물이 도메인 좌표 그대로 렌더링됨 (반전 없음)
- Red팀: 타일/유닛/건물이 맵 중심 기준 180° 반전된 위치에 렌더링됨
- Red팀 카메라 시작 위치: ToView()로 변환된 Red Castle 위치를 향해야 함
- 입력(터치/클릭): FromView()로 역변환 후 올바른 HexCoord로 변환되는지 확인
- 유닛 이동 방향: Red팀에서 FlipDirection() 적용 여부 확인 (NE↔SW, E↔W, SE↔NW)
- **3D**: 메시 자체는 뒤집히지 않아야 함 (Y축 회전만 변경)
- 싱글플레이: ViewConverter.IsFlipped = false → 반전 없음
- [추가] 네트워크 게임 시작 시: 건물(Castle/채굴소)이 Red팀에서 반전된 위치에 나타나는지 확인
- [추가] Red팀 건물 transform.position 오프셋 버그 수정 확인 (2026-02-22):
  - 수정: GameBootstrapper.StartNetworkGame()에 HexMetrics 사전 설정 코드 추가

## 네트워크 미완성 항목 QA 체크리스트 (코드 분석, 2026-02-27)
- [ ] BuildFailedClientRpc UI 피드백 없음: 건물 배치 실패 시 사용자 알림 없음
- [ ] EnqueueFailedClientRpc UI 피드백 없음: 유닛 생산 큐 추가 실패 시 동일 문제
- [ ] InputHandler 유닛 이동 네트워크 분기 누락: 멀티플레이에서 탭 이동 시 상대방 화면에 동기화 안 됨
- [x] 자동생산 멀티플레이 — NetworkProductionController에 ToggleAutoServerRpc 구현됨 (2026-04-19 확인)
- [x] 생산 큐 클라이언트 UI: SyncQueueStateClientRpc + ProductionStartedClientRpc 두 경로로 동기화됨 (2026-04-19 확인)
- [x] NetworkGameEndController._lobbySceneName 하드코딩 수정: "SampleScene" → "Game" (수정 완료)

## 네트워크 QA 체크리스트
- 건물 배치: 서버 검증 후 양쪽에 동일하게 생성되는지
- 유닛 생산: 서버에서 생산, 양쪽 UnitFactory에 동일 ID로 스폰되는지
- 타일 소유권: BroadcastTileChangeClientRpc로 양쪽 색상 일치하는지
- 골드: NetworkVariable로 클라이언트 자동 동기화되는지
- HP: NetworkHealthSync로 양쪽 HP 일치하는지
- 승패: AnnounceWinnerClientRpc로 양쪽 동일 결과 표시되는지

## 스탯 적용 + 부유 HP 텍스트 QA (2026-04-12~13 완료)

### 최종 판정: PASS (전 항목 실기 통과)

### 핵심 발견 사항
- `SyncHealthClientRpc` 클라이언트에서 `GameEvents.OnEntityDamaged` 미발행 → FloatingHpTextSpawner 미반응 버그 발견. NetworkHealthSync에서 TakeDamage 후 이벤트 재발행으로 수정.
- `Camera.main` 매 이벤트 탐색 비용 → Initialize()에서 캐싱으로 수정.
- `_prefab` null 체크 누락 → Initialize() 진입부 null 가드 추가.
- `ReturnToPool`에서 SetActive(false) 중복 (FloatingHpText OnComplete에서도 수행) — 기능 영향 없음.
- BuildingStats.GetMaxHp 단일 파라미터 버전이 Human으로 위임 — 하위 호환 정상.
- Application 레이어에 GameRaceContext 직접 참조 없음 확인.

### 체크리스트 추가 항목
- [ ] 멀티플레이 전투 중 클라이언트 FloatingHpText 표시 여부 (NetworkHealthSync 재발행 확인)
- [ ] BuildingStats.GetMaxHp 호출부 RaceId 미전달 누락 없는지 신규 건물 추가 시 확인

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-12/06_42_stats-apply/Testcase.md`
`Assets/_Project/Docs/_Tasks/2026-04-12/18_03_floating-hp-text/Testcase.md`

---

## 종족 인게임 적용 QA (2026-04-07 완료)

### 최종 판정: PASS (SINGLE-01~06, MULTI-01 실기 통과)

### 핵심 발견 사항
- `_bluePrefabs` / `_redPrefabs` 필드명이 BuildingFactory에도 동일하게 존재 → Grep 시 클래스명 함께 확인 필수
- Inspector 필드 재연결이 필요한 작업은 에디터 설정 스크립트(`Hexiege/Setup/...`) 실행 여부 반드시 확인
- BattleViewModel + GameBootstrapper 양쪽에서 GameRaceContext.Set() 중복 호출 — 동작 오류 없음 (Minor 잔존)

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-07/09_00_faction-ingame-apply/Testcase.md`

## 생산 패널 전면 재작성 QA (2026-04-19) ✅ 실기 완료

### 최종 판정: PASS (TC-001~018 전체)

### 핵심 발견 사항
- 구 구조(ManualQueue/AutoEntries/AutoEntry/AutoIndex/CurrentProducingIsAuto/isNormalAutoState) 모두 제거됨.
- IsAutoMode 읽기 전용 프로퍼티 (`AutoTypes.Count > 0`) — 직접 대입 시도 없음.
- SyncQueueStateClientRpc 파라미터 모두 int/bool 기본 타입 — NGO 직렬화 정상.
- CancelAutoTypeIfNeeded 추가: 슬롯 클릭으로 자동 항목 취소 시 AutoTypes에서도 제거 + Rule 2 처리.
- wasAuto 캡처는 state.CurrentIsAuto=false 초기화 이전 라인에서 반드시 수행해야 함.

### 생산 큐 새 구조 핵심 (이후 QA 재검증 시 기준)
- QueueSlot { Type, IsAuto, IsCharged } — 단일 구조체
- PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2 불변식
- IsCharged=false: 자동 항목 대기 중 (골드 미차감), ChargeVisibleSlots가 슬롯 진입 시 차감
- ChargeVisibleSlots 호출 시점: TryStartNext 직후, CancelQueueAt(1/2) 직후, CompleteProduction 직후
- 슬롯 클릭 취소: 자동 항목이면 CancelAutoTypeIfNeeded도 호출

### 미해결 이슈
- ~~TC-008 관련: 큐 비어있을 때 자동 등록 시 슬롯1에 1프레임 깜빡임~~ → **수정 완료 (2026-04-19)** `ToggleAutoProduction`에서 `!CurrentProducing.HasValue`이면 즉시 `TryStartNext` 호출로 해결

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-19/production-panel-rewrite/Testcase.md`

## 이동/전투 재설계 QA (2026-04-30 정적 분석 완료, 실기 미완)

### 정적 분석 판정
- BUG-001 (Critical, FAIL): GameBootstrapper.SetupProduction()에서 TileMoveSlotManager를 UnitFactory에 미전달 → UnitFactory.CreateUnitObject()에서도 UnitView에 미전달. 슬롯 분산 전체 불작동.
- BUG-002 (Major, FAIL): ResumeFromForwardTile()에서 _unitData.Position은 forwardTile로 갱신되지만 transform.position은 공격 슬롯 위치 그대로 → RunTileTraversal 재진입 시 Lerp 출발점 불일치로 순간이동 발생.
- BUG-003 (Major, CONDITIONAL PASS): FindForwardAvailable 대기 루프 — 전방 타일 모두 가득 찬 교착 상황에서 무한 대기. 설계 의도(대기)와 일치하지만 교착 가능성은 실기 확인 필요.
- BUG-004 (Minor, CONDITIONAL PASS): ClaimByApproach fallback 무제한 — MaxUnitsPerSlot 실제 차단 미구현. 대규모 전투 실기 확인 후 정책 결정.

### 핵심 패턴 — 신규 매니저 추가 시 3곳 와이어링 필수
1. GameBootstrapper.CreateUseCases() 에서 인스턴스 생성
2. GameBootstrapper.SetupProduction() 에서 UnitFactory.SetDependencyReferences() 호출에 인자 추가
3. UnitFactory.CreateUnitObject() / InitializeUnitView() 에서 unitView.SetDependencies() 호출에 인자 추가

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-30/02_29_movement-combat-redesign/Testcase.md`
`Assets/_Project/Docs/_Logs/2026-04-30/02_29_movement-combat-redesign/Log.md`

## 로비 SetActive→CanvasGroup 전환 정적 분석 (2026-05-25) ✅ 완료

### 판정: 로비 7개 뷰 전체 규칙 준수 확인

### 핵심 발견 사항
- 로비 7개 뷰(LobbyRootView, MainLobbyView, BattleMainView, BattleRootView, ProfileView, RankingView, ShopView 등) 모두 `CanvasGroup.alpha/blocksRaycasts/interactable` 패턴으로 전환 완료 확인
- `ProductionPanelUI.UpdateUpgradeButton()`에 `SetActive` 1건 잔존 (인게임 UI, 로비 작업 범위 밖, Minor — 기능 영향 없음, 향후 일관성 정리 권장)

### task 문서
`Assets/_Project/Docs/_Tasks/2026-05-25/lobby-canvasgroup-refactor/`

---

## 게임 화면 UI TC 작성 (2026-05-26) — 실기 테스트 대기 중

### 총 62개 TC 작성 완료 (실기 미완)

### 커버 범위
- GameHudUI (7개): 골드/인구수/킬카운트 표시, 공성 게이지, 설정 버튼
- BuildingPlacementUI (9개): 패널 표시/숨김, 건설 비용 갱신, 실패 피드백
- ProductionPanelUI (22개): 수동/자동 생산, 큐 조작, 업그레이드, 랠리포인트
- BuildingActionPanelUI (5개): 채굴소/타워 액션 패널
- InGameSettingsUI (9개): 일시정지, 포기 확인, 설정 패널
- GameEndUI (7개): 승패 표시, 재경기, 로비 복귀
- 공통 (3개): 레이아웃, Safe Area

### 정적 분석 발견 사항
- `ProductionPanelUI.UpdateUpgradeButton()`에서 `_upgradeButton.gameObject.SetActive(...)` 1건 — Minor, 기능 영향 없음, Rule 5 일관성 정리 권장

### task 문서
`Assets/_Project/Docs/_Tasks/2026-05-26/game-ui-tc/Testcase.md`

---

## 참고 파일
- [patterns.md](patterns.md) — 버그 패턴 상세
- [qa_history.md](qa_history.md) — 완료된 QA 상세 내역 (생산시스템/DOTween/카메라/재경기/로비/로딩/랜덤매칭)
