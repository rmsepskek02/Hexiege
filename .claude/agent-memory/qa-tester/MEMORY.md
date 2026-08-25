# QA Tester Memory — Hexiege

## 토픽 파일 인덱스

| 토픽 파일 | 내용 |
|---|---|
| [patterns.md](patterns.md) | 세션별 버그 패턴 상세 — 반복해서 걸린 함정과 그 검사 방법(grep 루틴 포함). 신규 유닛 데이터 배선 검사 루틴도 여기 |
| [qa_history.md](qa_history.md) | 완료된 QA 이력 전문. 1차 이관분(생산시스템 / DOTween / 카메라 / 재경기 / 로비 복귀 / 로딩 / 랜덤매칭 / 전투거리 / 공격방향) + **2026-08-24 2차 이관분 18건**(2026-04-07 ~ 2026-07-20) + **네트워크 종료 시점 가드 8곳 실기 검증(2026-08-24, 맨 위)** |

> 이 파일에는 **매 QA 작업마다 필요한 규칙·체크리스트만** 남긴다. 지나간 QA 기록은 위 [qa_history.md](qa_history.md) 로 옮겼다(2026-08-24, 삭제 아님 — 원문 그대로 보존).

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
- **NetworkBehaviour `OnNetworkSpawn` 시점 `IGameServices`(GameServicesLocator) 미등록 스폰 레이스**: 씬 NetworkObject의 `OnNetworkSpawn`이 GameBootstrapper의 서비스 등록보다 먼저 돌 수 있어, 스폰 시점 캐시가 null이면 이후 RPC 처리에서 조용히 무동작할 위험. **해결 패턴 = 사용 시점 지연 재조회**(2026-07-31 `NetworkUpgradeController.ResolveServices()` — 멀티 연구 완료 레벨이 클라에 반영 안 되던 실기 버그를 이걸로 수정). 신규 Infrastructure NetworkBehaviour QA 시 "서비스 참조를 `OnNetworkSpawn`에서만 캐시하고 null 복구 경로가 없는가"를 점검할 것.

## 연구소 유닛 강화 시스템 — 멀티플레이 실기 PASS (2026-07-31)
- **결과**: 강화 시스템(공/방/속+초월 자연회복)·전투 스탯 ×10·연구 패널 UI 멀티플레이 실기 PASS. 방어 감쇄 순수 함수 `DamageCalculator.ApplyDefense`(K=120, floor 1, 하드캡 65%, `raw<=0`·`defense<=0`이면 원본 반환)로 하위호환(방어 0=회귀 없음).
- **QA 점검 포인트(향후 재검증)**: ① ×10은 config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨) — **미실행 환경은 구 수치로 동작**하므로 실기 전 스크립트 실행 여부 확인(Unit/Building은 곱셈이라 2회 실행=×100 주의). ② 완료 레벨=양 클라 브로드캐스트(양쪽 효과)·진행 중=소유자만. ③ 자연회복↔BloomFairy 힐 별개 채널(상호 덮어쓰기 없음). ④ 미검증/미완: AI 연구 사용 실기·싱글 자연회복 실기·MistShrine 힐(미구현)·UI 레이아웃. task: `_Tasks/2026-07-22/10_08_unit-upgrade-system/`.

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

## 🔴 런타임 로그로 판정할 때의 3대 함정 (2026-08-24 실증 — 매 로그 QA마다 확인)

상세·근거는 [qa_history.md](qa_history.md) 「네트워크 종료 시점 가드 8곳 실기 검증 (2026-08-24)」.

1. **회차 간 건수 비교 전에 역할 구성부터 맞춘다.** `[WARN]` 1,099건이 2026-08-24 로그에만 있고 2026-08-19 로그에 0건이라 *"이번에 생겼다"* 로 읽히지만, **08-19 는 3경기 내내 호스트**(`IsServer=False` 스폰 0건)였고 그 경고는 **클라이언트 전용 경로**의 것이다. **차이는 "코드가 바뀐 것"이 아니라 "기록한 쪽이 바뀐 것"이었다.** → 두 로그를 비교하기 전에 **`네트워크 스폰 | IsServer=` 로 역할 구성을 먼저 센다.**
2. **서버 전용 가드는 에디터가 서버인 구간에서만 검증된다.** 클라이언트 구간의 수신 로그(`… 수신` · `… 보정`)는 **상대 호스트가 보낸 것**이라, 우리 쪽 서버 가드의 근거가 **되지 못한다**(상대 빌드의 커밋을 확인할 수 없다 — 규칙 10). 근거표를 만들 때 **서버 구간 / 클라 구간을 반드시 갈라 적는다.**
3. **`if (!IsServer …)` 로 시작하는 가드는 클라이언트에서 뒷 조건이 평가되지 않는다.** `_combatStopped` 리셋이 이 때문에 *"재경기 정상"* 으로 오독될 뻔했다 — 2·3경기 사망 로그는 전부 `EntityDiedClientRpc 수신` 경로였다. **단락 평가 순서를 읽고 "이 조건이 실제로 평가되는 구간이 로그에 있는가"를 확인한다.**
> **경고 건수가 크다 ≠ 문제가 심하다.** 재시도 성공/실패 짝을 먼저 센다(이번엔 대기 319 ↔ 성공 319 · 실패 0건). **한 대상당 여러 번 찍히는 경고**라 1,099 대 319 로 부풀어 보였다.

## 네트워크 QA 체크리스트
- 건물 배치: 서버 검증 후 양쪽에 동일하게 생성되는지
- 유닛 생산: 서버에서 생산, 양쪽 UnitFactory에 동일 ID로 스폰되는지
- 타일 소유권: BroadcastTileChangeClientRpc로 양쪽 색상 일치하는지
- 골드: NetworkVariable로 클라이언트 자동 동기화되는지
- HP: NetworkHealthSync로 양쪽 HP 일치하는지
- 승패: AnnounceWinnerClientRpc로 양쪽 동일 결과 표시되는지

