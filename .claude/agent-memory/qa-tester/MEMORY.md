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

## Google 로그인(GPGS) 진단 체크리스트 (2026-06-27 확정)
- **즉시 Canceled / DEVELOPER_ERROR 증상**(계정 선택 UI 미표시 + 수십 ms 내 `signInStatus=Canceled`): 가장 흔한 원인은 **SHA-1 불일치**.
- **진단 1순위 — logcat 실제 서명 SHA-1 확인**: Unity 태그 필터 제거 후 `PlayGamesServices[SignInAuthenticator]` 태그의 `Cert SHA1 fingerprint`(또는 `Cert SHA1 fingerprint`) 캡처 → 이것이 APK 실제 서명 SHA-1. Firebase Console / Play Console GPGS 사용자 인증 정보에 등록된 SHA-1과 비교, 불일치 시 즉시 FAIL.
- **SHA-1 등록 위치 3곳 모두 일치 검증**: ① Firebase Console OAuth 클라이언트 ② Play Console GPGS 사용자 인증 정보 ③ 실제 빌드 키스토어. 한 곳이라도 빠지면 `signIn()` 실패. 키스토어 파일이 등록 시점과 다른 파일일 수 있으니 실제 서명 SHA-1(logcat) 기준으로 역검증.
- **`serverAuthCode length=0`은 인증 실패 신호**: SHA-1 불일치로 signIn 실패 시 빈 값 반환. 정합되면 `length=73`(정상 발급).
- **코드 레벨 점검**: 최초 로그인은 `Authenticate()`(=`isAuthenticated()`만, 세션 없으면 Canceled)가 아닌 `ManuallyAuthenticate()`(=`signIn()`) 호출인지 확인(GPGS Plugin 2.1.0).
- task: `_Tasks/2026-06-27/12_26_google-login-debug/`

## 아키텍처 패턴 (확인된 사항)
- Presentation이 Infrastructure(LocalPlayerTeam) 직접 참조: 정적 홀더 패턴으로 허용 범위
- Assembly Definition 없음 — 물리적 경계 없음, 네임스페이스 규약으로만 관리
- CameraController에 `using Hexiege.Infrastructure` 선언 필요 (GameConfig 사용 목적, 정상)

## 반복 확인 필요 항목
- 신규 UseCase/Manager 추가 시 GameBootstrapper 와이어링 누락 여부
- LocalPlayerTeam.Current 기본값 = Blue → 싱글플레이 동작 항상 확인
- 팀 기반 로직 변경 시 StartAutoMove의 하드코딩 Blue/Red 확인
- ViewConverter.IsFlipped 상태가 올바르게 초기화/리셋 되는지 확인
- **AAB 최적화 후 기기 QA (2026-07-15)**: 3D 건물/유닛 텍스처 Android max size가 512로 낮아짐. 설치/실행, 로그인, 로비 UI 가독성, 인게임 유닛/건물 텍스처 뭉개짐, Blue/Red 팀 색상 변형, emission/공격 이펙트 품질을 우선 확인. 최종 수치/롤백 기준은 `AABSizeOptimization.md`.
- **CanvasGroup Rule 5 검사**: UI 뷰의 Show/Hide에서 `SetActive(false/true)` 잔존 여부 확인 → 반드시 `CanvasGroup.alpha=0/1 + blocksRaycasts=false/true + interactable=false/true` 패턴으로 구현되어야 함 (DontDestroyOnLoad 오브젝트의 Awake 미호출 버그 + LayoutGroup 레이아웃 깨짐 방지)
- **Safe Area Rule 4 검사**: 전체화면 배경 요소(`Image`로 화면 전체를 채우는 오브젝트)가 `SafeAreaContainer` 밖(`Canvas` 직속)에 배치되어 있는지 확인 → 배경이 `SafeAreaContainer` 안에 있으면 노치/홈바 기기에서 Safe Area 경계에서 잘려 보임

## 알려진 취약 지점
- FindObjectsByType 사용처: InputHandler.StartAutoMove, InputHandler.HandleClick
  → 유닛 수 증가 시 성능 취약. 캐시 최적화 대상으로 마킹.
- IsPointerOverUI의 Debug.Log — 매 클릭마다 콘솔 출력, 프로덕션 전 제거 필요
- **3D 전환 후**: 신규 Factory 작성 시 Z-depth 기반 배치 확인 (sortingOrder 미사용)
  - 올바른 렌더 순서: 타일(Y=0) < 건물(Y=높이) < 유닛 (카메라에서 멀→가까운 순)
- **Animator 런타임 상태 질의(`GetCurrentAnimatorStateInfo`)로 "이미 X 상태?" 판별**: CrossFade 블렌딩 도중 출발 상태를 반환해 판별이 어긋날 수 있음. 프로젝트 원칙상 로컬 논리상태 추적으로 대체해야 함(2026-07-13 UnitView 3곳 제거 완료 — `_currentAnimStateHash`/`ResumeWalkAnimation`). **신규 애니메이션 전환 코드 리뷰 시 `GetCurrentAnimatorStateInfo` 잔존 여부를 Grep으로 점검**할 것.
- **로컬 임포트 대형 SDK를 `#if SYMBOL` 컴파일 게이트로 감싸는 패턴 위험**: 심볼 미정의 시 스텁이 조용히 대체돼 기능이 무조건 실패(2026-07-13 Firebase `#if HEXIEGE_ENABLE_FIREBASE_AUTH` → 로그인 항상 실패, 게이트 제거로 해소). Firebase/GPGS 등 `.gitignore`로 git 미포함·로컬 임포트하는 SDK 관련 작업 QA 시, 컴파일 게이트/스텁 존재 여부를 확인.
- **죽은 코드 삭제 검증 패턴**: 미사용 메서드 삭제 시 Grep 전수로 호출 0건 확인(2026-07-13 `UnitView.StopMovement()`).
- **특수 유닛 쿨다운 컨벤션(중요, 2026-07-18 BloomFairy QA에서 확정)**: `StatsReference.md`의 "X:XX(Y:YY)" 표기는 "타격시간(전체쿨다운)" — Y가 **사이클 총 길이**이고 X는 그 안에서 타격이 발생하는 시점이다(예: BattleAxe 1:17(3:05), TorrentSpirit 0:50(4:00)). `UnitCombatUseCase.TryAttack`은 `attacker.AttackCooldownRemaining = attacker.AttackCooldown`을 히트 타이머 등록과 **동시에**(캐스트/스윙 시작 시점) 설정한다 — 쿨다운이 타격 딜레이를 포함한다. 신규 특수 유닛(힐/캐스트형)의 상태머신 코루틴을 볼 때는 **쿨다운 설정 위치**를 반드시 확인할 것: "타격 대기 → 효과 적용 → 그 다음에 쿨다운 설정"으로 짜면 실제 사이클이 (타격시간 + 전체쿨다운)만큼 되어 설계보다 길어지는 회귀가 생긴다(BloomFairy `EnterHealLoopV3`에서 발견 — 설계 3.0s인데 실제 4.0s). 쿨다운은 캐스트/스윙 **시작 시점**에 설정해야 한다.
- **비전투(지원형) 상태머신의 "영구 유휴" 위험 패턴**: `MoveAlongPathV3`는 A* 경로 순회 중 `ShouldEngage()`가 감지될 때만 전투/힐 루프로 분기하고, 감지 없이 경로 끝(적 성채 인접 타일)에 도달하면 `MoveCleanupAndCompleteV3()`로 코루틴이 완전히 끝난다. 일반 공격 유닛은 성채도 감지 대상이라 `EnterCombatLoopV3`가 성이 죽을 때까지 무한 루프를 돌아 사실상 "코루틴 종료"가 없다(항상 재감지). 반면 **건물을 감지하지 않는 지원형 유닛(BloomFairy 등)**은 경로 끝에서 대상이 없으면 코루틴이 완전 종료되고, 이후 재감지를 트리거하는 주기적 재스캔이 전혀 없다(건물 변경 시 `RepathAllAliveUnits`가 가끔 우연히 재기동시킬 수 있으나 신뢰 불가). 향후 힐/지원/논컴뱃 유닛(예: 버프 유닛) QA 시 "목표 없이 최전선 도달 → 이후 부상 아군이 나타나도 영구 방치"를 항상 재현 시나리오로 체크할 것.

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

## 로비 SetActive→CanvasGroup 전환 실기 테스트 (2026-05-25~27) ✅ 완료

### 판정: TC-SINGLE-001~014 전체 PASS / TC-SINGLE-015~016 SKIP (로그인 미구현)

### 핵심 발견 사항
- 로비 7개 뷰 모두 `CanvasGroup.alpha/blocksRaycasts/interactable` 패턴으로 전환 완료 + 실기 통과
- `ProductionPanelUI.UpdateUpgradeButton()`에 `SetActive` 1건 잔존 (인게임 UI, 로비 작업 범위 밖, Minor — 향후 일관성 정리 권장)
- TC-SINGLE-009/010 (방 만들기 에러 메시지): 초기 TC가 잘못된 전제(코드 입력란/확인 버튼 존재)로 작성됨 → 실제 구현(네트워크 오류 시 ErrorMessage 표시, StartHosting 재시도 시 초기화) 기준으로 수정 후 통과
- TC-SINGLE-015/016 (프로필 로그인): Firebase Auth 구현은 존재하나 프로젝트에서 미사용 상태 → 추후 로그인 기능 활성화 시 재테스트

### task 문서
`Assets/_Project/Docs/_Tasks/2026-05-25/ui-rules-inspection/Testcase.md`

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

## 사운드 시스템 QA 정적 분석 (2026-06-10) — PASS

task 문서: `Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/`

정적 분석 결과: 전체 PASS (FAIL 0건)
- 규칙 1~22 전체 준수 확인
- 아키텍처 의존성 방향 준수 (SoundConfig=Infrastructure, AudioManager=Presentation)
- VFX+SFX 쌍 호출 3곳 모두 확인 (UnitView×2, NetworkUnit×1)
- BGM 크로스페이드 로직, SFX 풀, 볼륨 dB 변환 정상

QA 수정 사항 (3건):
- ReturnSfxAfterPlay: WaitForSeconds → WaitForSecondsRealtime (timeScale=0 대응)
- Initialize() 재호출 시 SFX 풀 중복 생성 방지 코드 추가
- 무음 전환 후 _activeBgmSource = fadeIn으로 상태 명확화

실기 테스트: 완료 (2026-07-08) — 실기에서 버그 3종 발견 및 수정 (아래 참조)
TC 문서: `Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/Testcase.md`

### 실기 버그 3종 (2026-07-08 수정 완료)
- **BUG-1 BGM 씬 전환 시 겹침**: `StartCrossfade()`가 `StopCoroutine`만 하고 페이드아웃 중이던 AudioSource를 `Stop()`하지 않아 이전 BGM이 계속 재생. → stale 채널 즉시 Stop으로 수정. **사운드/오디오 QA 시 크로스페이드 중단 경로에서 이전 채널의 AudioSource가 확실히 정지되는지 확인할 것.**
- **BUG-2 볼륨 UI 규칙 위반**: 에디터 스크립트 생성 슬라이더의 고정 픽셀값(규칙 2 위반) + TMP 폰트 미지정(규칙 6 위반). → 앵커 비율 + Maplestory Bold SDF + `EditorUtility.SetDirty()`. **에디터 스크립트로 UI 생성하는 작업 QA 시 규칙 2(앵커)/규칙 6(폰트) + SetDirty 호출 여부 점검(전역 UI 시스템 QA 2026-06-18과 동일 패턴).**
- **BUG-3 SFX 볼륨 미작동**: Exposed Parameter 이름은 정상이었음(추정 오류). `SetFloat` 실패 감지 로깅 추가. **AudioMixer.SetFloat은 실패 시 조용히 false 반환 → 반환값 확인 로직 유무 점검.**
- task: `_Tasks/2026-07-07/12_28_sound-system-bugfix/`

---

## AI 시나리오 ScriptableObject 개편 QA (2026-06-10 → 2차 완료 2026-06-11)

### 종합 판정: CONDITIONAL PASS (정적 분석 전항목 PASS, 실기 대기)

### 1차 QA (2026-06-10) 핵심 발견 사항
- BuildOrderStep/AIActionType이 Infrastructure에 있어 Application→Infrastructure 직접 의존 발생
  → 2차 작업에서 Domain/AI 레이어로 이동하여 해소
- DifficultyLevel도 Infrastructure에 있어 BuildOrderStep.GetDelaySeconds()로 인한 연쇄 위반
  → DifficultyLevel도 Domain으로 함께 이동하여 해소

### 2차 QA (2026-06-11) 핵심 확인 사항
- BuildOrderStep, AIActionType, DifficultyLevel 모두 Hexiege.Domain 네임스페이스로 이동 완료
- 세 타입 모두 UnityEngine 의존 없음 ([Serializable]은 System.Serializable — 허용)
- LoadScenarioBundleForRace(): 3종족 switch 완비, null/빈배열 방어 코드 존재
- 레거시 Human_A/B/C.asset 실물 삭제 완료 (Glob 결과 0건)
- 에셋 YAML: 3종족 9개 시나리오 모두 actionType(0~2), phaseIndex(0~3) 범위 내
- NOTE-001(Minor): AIScenarioConfig.cs 주석/XML doc에 구버전 경로 예시 잔존 — 기능 무영향

### AI 시나리오 현행 구조
- 종족별 단일 에셋: Human/Spirit/Transcendence 각 1개
- 각 에셋에 3개 ScenarioBundle 내장
- Human: Rush/Tech/Balance, Spirit: Inferno/Torrent/Quake, Transcendence: Rush/Flora/Beast
- 종족 결정: GameRaceContext.RedRace (AI는 항상 Red팀)

### task 문서
`Assets/_Project/Docs/_Tasks/2026-06-10/01_06_ai-scenario-scriptableobject-restructure/`

---

## Login UI 완성도 QA 정적 분석 (2026-06-11)

### 종합 판정: FAIL (CanvasGroup Rule 5 위반)

### 핵심 발견 사항
- **[Rule 5 위반]** LoginRootView.SetActivePanel() / HideAll()에서 5개 패널 전환에 `SetActive()` 직접 사용 → CanvasGroup 패턴으로 교체 필요
- **[Inspector 공유 이슈]** `_confirmPopup`(종료 팝업) 과 `_networkErrorPopup`이 동일한 ConfirmPopup 오브젝트(fileID: 422375806) 참조 → 두 팝업이 동시에 호출되면 Show() 재호출로 메시지/콜백 덮어쓰기 발생 가능 (Major)
- **[Safe Area Rule 4]** Background가 Canvas 직속 자식으로 SafeAreaContainer 밖에 배치됨 → PASS
- **[Inspector 연결]** LoginBootstrapper 7개 View 슬롯 전부 연결됨, `_loadingIndicator` 연결됨
- `_headerText: {fileID: 0}` — null 허용(코드에서 Optional 처리됨), 문제 없음
- AnonymousWarningPopup의 `_blockingOverlay.SetActive()` — Lobby 씬 별도 점검 예정 항목. `_panel.gameObject.SetActive(true)` 는 이미 제거됨(2026-06-15).

### Login 씬 계층 구조 (확인 완료)
- Canvas → [Background (Canvas 직속), SafeAreaContainer (Canvas 직속)]
- SafeAreaContainer → [LoginRootView, ConfirmPopup, LoadingIndicator, AnonymousWarningPopup]
- LoginRootView → [LoginSelectPanel, EmailLoginPanel, SignUpPanel, EmailVerifyPanel, PasswordResetPanel]

### task 문서
정적 분석만 수행 (플레이모드 실기 불가 — Firebase 미설정)

---

## Login UI CanvasGroup 전환 + NetworkErrorPopup 분리 QA (2026-06-11)

### 종합 판정: CONDITIONAL PASS

### 핵심 발견 사항
- **[Rule 5 준수 확인]** `LoginRootView.cs` 전체에서 `GameObject.SetActive()` 패널 전환 호출 없음. `ShowGroup()`/`HideGroup()`으로 alpha/blocksRaycasts/interactable 3속성 모두 처리. 완전 준수.
- **[Inspector 공유 이슈 해소]** `_confirmPopup`(종료 팝업)과 `_networkErrorPopup`이 별도 오브젝트로 분리. LoginUiSetup 에디터 스크립트로 자동 생성/연결.
- **[Minor Bug-1]** `LoginUiSetup.cs`: `SafeAreaContainer` 미발견 시 NetworkErrorPopup이 씬 루트에 생성된 채 저장됨 — `LogWarning`만 출력, `DisplayDialog` 미사용 (186-190행).
- **[Minor Bug-2]** `ShowNetworkErrorPopup()`이 `cancelLabel: string.Empty` 전달하지만 `ConfirmPopup.Show()`는 취소 버튼을 숨기지 않아 빈 버튼 노출 가능성 (`ConfirmPopup.cs` 175-179행).
- **[LoginBootstrapper 호환]** CanvasGroup 타입 변경(GameObject → CanvasGroup)에 대해 LoginBootstrapper가 패널 슬롯 직접 참조 없음 — 영향 없음.
- **[ConfirmPopup Show() 시그니처]** 5개 인자 모두 일치. LoginRootView 양쪽 호출부 모두 정상.

### ConfirmPopup 재사용 패턴 주의
- `cancelLabel: string.Empty` 전달 시 취소 버튼이 빈 텍스트로 노출됨 (버튼 숨김 로직 없음)
- 단독 확인 버튼만 필요한 팝업은 취소 버튼을 Inspector에서 비활성화하거나, ConfirmPopup에 `cancelLabel` 빈 문자열 시 버튼 숨김 로직 추가 필요

### task 문서
`Assets/_Project/Docs/_Tasks/2026-06-11/00_31_login-ui-canvasgroup-popup-fix/Testcase.md`

---

## 전역 UI 시스템 QA (2026-06-18) ✅ 완료

### 종합 판정: PASS (수정 후)

### 발견 및 수정된 버그
- **[Major, 수정완료] 규칙 1 위반**: `SetupUIManagerInScene.cs` `CanvasScaler.matchWidthOrHeight = 1f` → `0f` (가로 기준). UIManager Canvas / SplashOverlay Canvas 모두 해당.
- **[Major, 수정완료] 규칙 4 위반**: SplashOverlay에 SafeAreaContainer 없어 StatusText/TapToStartText가 노치 영역에 가려질 수 있음 → SafeAreaContainer + SafeAreaFitter 추가, 텍스트를 그 안으로 이동. Background는 전체화면 요소이므로 SafeAreaContainer 밖(SplashOverlay 직속) 유지.
- **[Major, 수정완료] 규칙 6 위반**: `SetupUIManagerInScene.cs`에서 에디터 스크립트로 생성하는 TextMeshProUGUI(StatusText/TapToStartText)에 Maplestory Light SDF 폰트 미적용 → `AssetDatabase.LoadAssetAtPath<TMP_FontAsset>` 로드 후 명시 적용.

### Minor (미수정)
- `LoginBootstrapper.cs:116` 주석에서 `(규칙 5)` 참조 부정확 — null-safe 패턴 설명인데 CanvasGroup 규칙 번호 기재됨. 기능 무영향.
- `BattleViewModel.cs:222` `Task.Delay(2000)` 고정 2초 지연 — 씬 로딩 완료 타이밍과 무관한 하드코딩. 이번 작업 범위 외.

### 핵심 교훈
- 에디터 스크립트로 UI를 생성할 때 **폰트(규칙 6)** 와 **SafeAreaContainer(규칙 4)** 는 반드시 명시 설정
- `matchWidthOrHeight`는 프로젝트 규칙 상 **0f(가로 기준)** 고정

### task 문서
`Assets/_Project/Docs/_Tasks/2026-06-16/12_25_global-ui-system/Testcase.md`

---

## BlockingOverlay UIManager 통합 QA (2026-06-21) — CONDITIONAL PASS

### 종합 판정: CONDITIONAL PASS (Inspector 연결 미확인)

### 핵심 구현 패턴 (이후 유사 작업 참조)
- UIManager 단일 소유: `_blockingOverlay(CanvasGroup)` + `_blockingOverlayButton(Button)`
- 참조 카운터(`_blockingOverlayRefCount`): 중첩 Show 지원, HideOverlay가 0이 될 때만 실제 숨김
- 두 모드: Modal(onTap=null) vs Popup(onTap=콜백), Button.onClick으로 분기
- RematchRequestPopup 전용 패턴: `_overlayShown` bool로 ShowOverlayOnce/HideOverlayOnce — 팝업 전환 시 중복 +1 방지

### 발견된 잠재 문제 (Minor)
- BUG-001: `BuildingPlacementUI.Show()`에서 `_popup?.Show()` 이후 `UIManager.Instance?.ShowBlockingOverlay(Close)` 호출 순서 — 팝업이 먼저 나타난 직후 오버레이 표시. `InGameSettingsUI.Show()`는 반대 순서(오버레이 먼저). 기능 차이는 없으나 순서 불일치.
- BUG-002: `BuildingPanelBase.Show()`도 `_popup?.Show()` 이후 `ShowBlockingOverlay` 호출. `ConfirmPopup.Show()`와 `InGameSettingsUI.Show()`와 순서 불일치.
- BUG-003: `SharedBackgroundButton.cs` 파일 자체는 테스트 통과 전이므로 아직 존재(정상). 그러나 Lobby씬/Game씬 프리팹에서 SharedBackgroundButton 컴포넌트가 완전히 제거됐는지는 Inspector 레벨에서만 확인 가능.
- `BuildingPlacementUI.cs` line 468 주석이 과거 SharedBackgroundButton 언급 — 코드 주석 일관성 Minor 이슈.

### ShowBlockingOverlay 호출 순서 패턴 (확인 필요)
- 권장 순서: ShowBlockingOverlay 먼저 → 팝업 패널 Show. (오버레이가 먼저 표시돼야 팝업 아래에 정확히 배치됨)
- ConfirmPopup, InGameSettingsUI: 오버레이 먼저 호출 (올바름)
- BuildingPlacementUI, BuildingPanelBase: 팝업 Show 먼저 → 오버레이 나중 (Minor 불일치)

### 에디터 스크립트 주의점
- `MigrateBlockingOverlayToUIManager.cs`: Login.unity만 BlockingOverlay 생성, Game.unity는 RematchOverlay RaycastTarget만 비활성화
- `SetSiblingIndex(0)`: BlockingOverlay를 SafeAreaContainer보다 Hierarchy 위에 배치 → 팝업보다 뒤에 렌더링 (올바름)

### task 문서
분석 대상: `Assets/_Project/Docs/_Tasks/` 내 해당 날짜 폴더

## 참고 파일
- [patterns.md](patterns.md) — 버그 패턴 상세
- [qa_history.md](qa_history.md) — 완료된 QA 상세 내역 (생산시스템/DOTween/카메라/재경기/로비/로딩/랜덤매칭)
### 2026-07-16 - Profile/Ranking Cloud UI QA notes

- User confirmed email sign-up reaches nickname setup after verification path changes.
- User confirmed Profile tab click path and ProfileView enable path with temporary debug logs; debug logs were removed afterward.
- Ranking/Profile/NicknameChangePopup UI is functionally visible; final fine layout tuning remains manual Inspector work.
- Regression focus for next pass: Unity console compile errors, Profile tab refresh, Ranking tab refresh/empty state, nickname change validation, and email verification abandonment.

### 2026-07-17 - TorrentSpirit 파도 AoE + 힐 서브시스템 QA (정적 분석, CONDITIONAL PASS)
- 로직/이벤트/서버권위/레이어 규칙: 전부 PASS (TickWaves 이중 틱 없음, 힐 멀티 동기화 정상, Domain 순수성 유지).
- **BUG-001급 발견 — "코드는 맞는데 데이터가 안 채워짐" 패턴**: UnitStatsConfig.asset에 unitType 18 항목 없음(범용 폴백로 동작) +
  TorrentSpirit_Attack.anim에 OnAttackHit 이벤트 미주입(규칙 27) + UnitEffectConfig.asset의 attackPreset이 fileID 0(고아 VFX 프리팹).
  신규 유닛 QA 시 항상 이 3종 데이터 배선을 grep으로 직접 확인할 것 — 상세 검사 루틴은 patterns.md 참조.
- **구조적 버그 발견**: ReplacesPrimaryAttack=true(special-only) 유닛은 주 타깃이 건물이면 그 공격 사이클에 피해가 전혀 발생하지 않음
  (주 타깃 단일피해 스킵 + AoE가 유닛만 순회). Castle 파괴가 승리조건인데 해당 유닛은 건물을 절대 못 부숨 — Major.
- 상세: patterns.md "세션: 2026-07-17" 참조. task: `_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/`

### 2026-07-16 - Email verification flow cleanup QA

- Required checks:
  - signup verification screen shows the typed email instead of the placeholder.
  - signup verification back -> confirm -> Firebase unverified user deleted.
  - signup verification back -> continue verification -> stays on screen.
  - existing unverified email login back -> Firebase sign-out and previous login panel, account remains.
  - verified first login still routes through nickname setup.

### 2026-07-18 - Email verification flow QA result

- User confirmed PASS for signup email display, signup cancel popup, Firebase unverified user deletion, continue verification staying on screen, relaunch from verification returning to verification, and relaunch from nickname setup returning to nickname setup.
- Regression focus if revisited: verified complete button path, existing unverified-login back sign-out path, and long-term stale unverified account cleanup remain policy/test follow-ups rather than current blockers.

### 2026-07-19 - MushroomBomber(버섯폭격기) 착탄형 DoT QA (정적 분석, PASS)
- 검증 범위: BlastAttackBehavior(신규), SpecialAttackRegistry/Context, UnitCombatUseCase의 DoT 초단위 틱 모드
  (ActiveTimedEffect.TickInterval/TickAccumulator, TickDiscreteDamageEffect, ApplyOneDamageTick), SpecialAttackConfig
  blast 필드, GameBootstrapper.Setup.cs 주입, UnitStatsConfig(26), 에디터 스크립트 2종. 이슈 0건 — 전 항목 PASS.
- **DoT 초단위 틱 시스템의 정확성 보장 메커니즘(향후 InfernoSpirit/QuakeSpirit도 재사용 — 반드시 재확인)**:
  `TickInterval>0`(discrete DoT) vs `TickInterval<=0`(연속 HoT diff)로 완전히 분기(같은 `TickTimedEffects` 루프
  안에서 `if (effect.TickInterval > 0.0001f)`로 조기 분기 후 `continue`) — 힐 경로 회귀 없음.
  discrete 틱은 "따라잡기 while 루프"(저프레임 대응) + "만료 시 잔여 1틱 강제 정산"(고프레임/누락 대응) 이중 안전망으로
  프레임레이트 무관 총량 정확 보장. `ApplyOneDamageTick`이 `Min(TickAmount, remaining)`로 클램프하므로
  총량 초과 불가능 — 이 두 안전망 존재 여부를 신규 DoT 유닛 QA 시 항상 확인.
- **DoT 텍스트가 OnAttackHit 애니 이벤트 미주입과 무관하게 정상 동작하는 이유**: `ApplyTimedDamageToUnit`이
  DoT 매틱마다 `EntityDamagedEvent(immediatePresentation: true)`를 발행 → `HitPresentationQueue.OnEntityDamaged`가
  `evt.ImmediatePresentation` 체크로 큐잉을 완전히 우회하고 즉시 `Emit()`(규칙 26 확장, 파도와 동일 경로).
  따라서 "매초 텍스트" 요구사항은 Attack 클립의 `OnAttackHit` 이벤트 주입 여부와 **무관**하게 항상 충족된다
  (직접 단일 피해 쪽만 OnAttackHit 타이밍에 의존 — 미주입 시 타임아웃 안전망(쿨다운×1.5)까지 지연되나 이는
  "연출 타이밍" 문제이지 DoT 로직 문제 아님. 향후 이런 착탄형 유닛 QA 시 "OnAttackHit 미주입 = 직접타격 텍스트만 지연,
  DoT/파도류 즉시연출 텍스트는 영향 없음"을 구분해서 판단할 것).
- **에디터 스크립트 필드명 검증 패턴 확인(기존 패턴 재확인)**: `RegisterMushroomBomberPrefabs.cs`/`WireFloraProductionLine.cs`
  모두 `SerializedProperty.FindPropertyRelative` 대상 필드명(`type`/`blue`/`red`, `buildingType`/`blueUnits`/`redUnits`/
  `type`/`portrait`/`requiredStage`)이 실제 struct 정의(`UnitFactory.UnitPrefabEntry`, `ProductionPanelUI.
  BuildingUnitMapping`/`UnitPortraitEntry`)와 정확히 일치함을 grep으로 대조 완료. 멱등성(이미 있으면 no-op, 값
  다르면 보정)도 코드상 확인됨 — 다만 Unity 에디터 부재로 실제 메뉴 실행/씬 반영 여부는 미검증(에디터 작업 영역,
  task 지시상 범위 외).
- **직접 10 + DoT 이중 적용/사망 시 배제 로직**: `ExecuteAttack`이 `ApplyDamageToVictim`(직접, 죽으면 `_unitSpawn`에서
  즉시 제거) 실행 후 `special.Apply(ctx)` 호출 — `BlastAttackBehavior`는 `ctx.Units.Values`(생존자만)를 순회하므로
  직접타격으로 죽은 주 타깃은 자연스럽게 DoT 수집에서 제외됨(별도 방어 코드 불필요, 데이터 흐름상 자동 보장).
- task: `_Tasks/2026-07-19/01_42_mushroombomber-impact-dot/`

### 2026-07-20 - 유닛 전투 동기화 규칙 감사

- 규칙 v2 완료 기준은 서버 ActionSequence/ImpactResult, Simulation Root/Visual Root, ActionMarkerOffset, Host/Client·Blue/Red·지연/지터/순서 역전/중복/늦은 스폰 검증을 모두 포함한다. Animation Event 존재만으로 PASS 처리하지 않는다.
- 현재 25종 모두 v2 최종 멀티 검증 전이다. UnitStatsConfig는 QuakeSpirit 추가로 25/25가 됐지만 QuakeSpirit/RhinoBreaker/MushroomBomber/BloomFairy는 기본 Attack marker가 없다.
- BattleAxe 1.1667/1.02, Pistoleer 2.0/0.8, Sniper 3.0/1.7333, Tank·Cannon 4.0/0.1667, Inferno 1.15/0.5, Stream 0.17/0.5, Fox 2.25/1.0 등 설정·marker 불일치를 구현 전 기준선으로 사용한다.
- `hitPreset`과 `tracerPreset`은 0/25, `attackPreset`은 9/25다. 상세 단일 감사표는 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`다.

### 2026-07-22 - InfernoSpirit/QuakeSpirit main 반영 재감사

- InfernoSpirit 직접 25 + 유닛 전용 DoT 5/초×3초는 사용자 실기 확인 범위에서 Legacy PASS다. 공격 방향 수정은 해당 작업에서 보류됐으며, 0.50초 marker/1.15초 설정 불일치와 권위 착탄·sequence 부재 때문에 v2 PASS가 아니다.
- QuakeSpirit 직접 20 + 주변 적 유닛·적 건물 10은 Host/Client HP 로그 범위에서 PASS다. 기본 `OnAttackHit`이 없어 직접 피해 표현이 쿨다운×1.5 안전망(최대 7.5초)을 기다릴 수 있으므로 시각 동기화는 FAIL/Incomplete다.
- Quake의 `ApplyFixedDamageToVictim`은 기존 공용 피해 경로와 별도 진입점이다. v2 QA 전 피해·이벤트·사망·네트워크 결과가 단일 writer/emitter에서 한 번만 발생하는지 반드시 검증한다.
- `SpecialAttackConfig.asset` YAML에는 Quake 필드만 명시되고 Inferno/Blast 필드는 C# 폴백에 의존한다. Inspector 직렬화·씬 주입을 에디터에서 확인하기 전 데이터 배선을 PASS로 판정하지 않는다.
