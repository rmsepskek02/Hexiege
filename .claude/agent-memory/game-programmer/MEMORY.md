# game-programmer 메모리 — 인덱스

프로젝트 규칙 단일 소스는 리포지토리의 `CLAUDE.md` / `AGENTS.md` / `Assets/_Project/Docs/`.
여기에는 **코드에서 반복적으로 발을 헛디딘 지점**만 적는다. 충돌하면 항상 프로젝트 문서가 옳다.

## 토픽 파일 인덱스

> 아래 19개 = 이 폴더의 `.md` 전부(`MEMORY.md` 제외).

### 먼저 읽어야 하는 것

- [logging.md](logging.md) — GameLog / sink / RuntimeLogger 구조, 판정 선례표, `key=value` 확정 매핑,
  전역 로그 훅(4겹 방어 + 스로틀). **로그 관련 작업은 여기부터 읽는다.**
- [network-infra.md](network-infra.md) — NGO 컨트롤러 구조, 스폰 레이스, **종료(Shutdown) 시점 뒷정리 관례 +
  `_combatStopped`(게임 종료 후 서버 틱 정지) 패턴**, UGS/동기화/팀 할당/승패 Phase 1~8 상세.
  **무작위 맵 3단계 B: `NetworkMapTransfer` 골격 · 순수 C# `MapChunkAssembler`(실행 검증 완료) ·
  조각 크기 「잠정값」 표기 관례 · 프리팹 미생성(사용자 Unity 작업 대기) · `DefaultNetworkPrefabs.asset`
  이 두 벌이고 씬이 쓰는 것은 `Resources/Config` 쪽이다.**
  **3단계 F·G·H: Host 준비·전송·timeout/재전송 · Client 해시 대조 순서 계약 · 결말 1줄(`_outcomeLogged`) ·
  프로브가 운영 지표를 오염시키지 않게 하는 법.**
  🔴 **3단계 I(2026-09-14, 여기서 처음 멀티 동작이 바뀐다): 씬 전환 게이트 ·
  게이트 결말 「한 번만 + 반드시 한 번은」 2겹 깃발 · 로그를 누가 어디서 남기는가 표 ·
  `MapHandoff.Clear()` 폐기 배선 · ⚠️ 프리팹을 어느 필드에 물리는가.**
  🔴 **재경기 맵 A~D(2026-09-15, 여기서 재경기 동작이 바뀐다): 「수락 즉시 씬 재로드」 사이에
  맵 준비·전송·검증을 끼워 넣기 · 진입점 분리 시 회귀 면적을 0으로 두는 법(이름·시그니처 유지) ·
  회차 결말 콜백 2개를 들고 있는 자리와 비우는 자리 4곳 · 실패 통보 채널을 왜 새로 파는가 ·
  회차 표식 `Round=` 를 `extraFields` 가 아니라 `BuildTransferLogData` 본문에 넣은 이유 ·
  `mcs`/`mono` 하네스로 확인한 불변식 9가지.**
  **네트워크 작업은 여기부터 읽는다.**

### 시스템별 (2026-06-23 재구성)

- [architecture.md](architecture.md) — 레이어 구조/제약, 정적 홀더(**`MapHandoff` 「읽고 비운다」 +
  `Clear()` 배선 정정 · `MapRootSeed`(Domain) 를 왜 Domain 에 뒀는가 포함**),
  GameBootstrapper, SO Config 패턴,
  DontDestroyOnLoad, **에디터 셋업 스크립트 패턴 + 배치 관례(`Assets/Editor/Setup/`·`Hexiege.EditorTools`)
  와 저장 반영(`SetDirty`+`MarkSceneDirty`)**
- [network.md](network.md) — NGO API 제약, RPC 래퍼 패턴, GO 파괴 전파, 같은 씬 재로드, 동기화 타이밍, 회전/위치 동기화
- [ui-system.md](ui-system.md) — UIManager, BlockingOverlay, SceneLoader, LoadingIndicator, Canvas SortingOrder,
  CanvasGroup/레이아웃/팝업/ToastUI 패턴, 생산·연구 패널 실측 구조,
  **건물 패널 골격(`Row0~2`) + 회전 테두리 머티리얼 `_Radius`·`_Inset` 공유 함정**
- [unit-building.md](unit-building.md) — 유닛 이동/전투 V3, 회전, 혼잡도, 다중히트, 건물 배치/철거/업그레이드/환불,
  생산 PendingQueue, AutoTower, 랠리포인트
- [hex-grid.md](hex-grid.md) — 헥스 좌표계, HexMetrics, ViewConverter, 타일 소유권, 그리드 렌더링, 패스파인딩,
  카메라, URP RT 잔상, **거리 비교는 `HexCoord.Distance`(도메인 정수) 우선**,
  **`HexTile` 상태 계약(`TileKind`/`MineKind`/`HasBuilding` + 계산 프로퍼티 `IsWalkable`) 과
  무작위 맵 1단계 신설 타입** — 타일 상태·건물 배치/철거 작업은 여기부터 읽는다.
  **무작위 맵 2단계 A: 결정적 PRNG `MapRandom`(SplitMix64) · 4스트림 `MapRandomStreams` ·
  seed 파생 순서 · 코드 내장 검증 벡터 · `GameConfig` 테스트 모드 필드 2개** — 맵 생성 작업도 여기부터 읽는다.
  **2단계 H: 격자 11×21 전환 · `MapProjectionUseCase`(설계도 → `HexGrid` 투영) · root seed 생성/보관 ·
  하드코딩 배치 주석 비활성화(`[2단계 대체 대기]`) · I/J/K 미완이 화면에 어떻게 보이는지**
  🔴 **2단계 I: 판정 조건 전환 — `IsWalkable` 은 건설 판정이 아니다.**
  신설 계산 프로퍼티 `AcceptsGeneralBuilding` / `AcceptsMiningPost` / `AcceptsCapture`(setter 없음,
  소유권은 호출부에 남음) · `NoBuild` 만 결과가 달라지는 근거 · 손대면 안 되는 이동 판정 목록 ·
  AI BFS 확장 조건을 좁히면 안 되는 이유 · `GetBuildingAt` vs `HasBuilding` 중복 전수 조사.
  **건설/점령 판정을 만지기 전에 반드시 읽는다.**
  **3단계 C·D(2026-09-09, 동작 무변경): `MapHandoff` 가 맵 계통 어디에 끼는지 ·
  `PrepareAndProjectMap()` → 래퍼 + `PrepareMap()` + `ProjectMap()` 3분할 ·
  🔴 가르면서 반드시 지켜야 하는 3가지(리셋 두 줄·가드는 래퍼에 남긴다 / 인자는
  `MapPreparationResult`) · 임시 고정 seed 는 아직 살아 있다.**
  🔴 **3단계 I(2026-09-14) 정정: 임시 고정 seed 는 주석 비활성화됐고(`CS1587` 주의),
  `CreateRootSeed` 싱글 경로는 `Domain/Map/MapRootSeed.Create()` 로 옮겨졌으며,
  `PrepareAndProjectMap()` 에 멀티 분기(`ProjectHandedOverMap`)가 생겼다 —
  인계가 비면 대체하지 않고 실패 처리한다.**
- [work-history.md](work-history.md) — 완료 작업 상세 전체 (날짜 역순, 2026-03~06)

### 세부 보조 자료

- [network-todo.md](network-todo.md) — 네트워크 미완성 항목
- [random-matching-bugfix.md](random-matching-bugfix.md) — 2026-03-16 랜덤 매칭 버그
- [unit-stats-and-combat.md](unit-stats-and-combat.md) — 스탯, IEntityPositionProvider, 쿨다운, 클라 시각 동기화
- [combat-fixes.md](combat-fixes.md) — ClaimedTile 공격 위치 보정, UnitView 회전
- [attack-direction-refactor.md](attack-direction-refactor.md) — 공격 방향 리팩터링(2D→3D)
- [rendering-and-animation.md](rendering-and-animation.md) — UnitView 애니메이션(`Animator.Play` 직접 호출,
  **상태 `m_Speed`=0 이면 첫 프레임 동결**), Shader Graph, HexTileView, 팀 프리팹,
  **범위 표시 스프라이트 기준 크기는 `sprite.bounds.size`(캐시 금지)**
- [3d-transition.md](3d-transition.md) — XZ 좌표계 전환, Phase별 수정 파일
- [camera-and-view.md](camera-and-view.md) — 카메라 틸트, ViewConverter, 경계 클램프
- [gameplay-systems.md](gameplay-systems.md) — 랠리포인트 구조 맵, 초상화 동적 업데이트,
  **MistShrine 에디터 셋업 메뉴 순서**
- [skill-aim-coordinate.md](skill-aim-coordinate.md) — 스킬 지점 조준 좌표화(HexCoord→Vector3),
  지면 데칼 셰이더 `Hexiege/SkillAimOverlay`, 취소 판정 버그 (2026-08-04)
- [check-docs-tool.md](check-docs-tool.md) — `Tools/check_docs.py` 상세: 옵션·`--reason` 규칙·
  `known_orphans`, **2026-08-25 검사 범위 확장(`.claude/` 포함, 35→73)**,
  **알려진 한계(Map·RandomMap·Upgrade 규칙 33개가 검사기에 등록되지 않음, 미해결)**

## 프로젝트 기본

- Hexiege — 모바일 1v1 헥사 RTS / Unity 6000.0.x (URP), C# 9.0, NGO 2.9.2
- 레이어: Domain → Application → Core → Infrastructure → Presentation → Bootstrap
- asmdef 없음(전부 Assembly-CSharp). 주석은 **한국어**, 초급자도 이해할 수준으로 상세히.

## 지켜야 할 규칙 (CLAUDE.md 요약 — 원문이 항상 우선)

- **git 명령 절대 금지**(규칙 5) — 검증도 git 없이 한다. 변경 전후 비교는 호출자에게 맡긴다.
- 계획서/요청 **범위만** 구현(규칙 6). 추가 리팩터링·개선은 제안만 한다.
- **추정 금지**(규칙 10) — 근거(파일:행)를 직접 확인하고 답한다. 확정 못 한 것은 "미확정" 으로 남긴다.
- 판단이 모호하면 스스로 결정하지 말고 보고한다(규칙 12).

## 컴파일에서 반복해서 물린 함정

- **`Hexiege.Application` 네임스페이스가 존재한다.** 수식 없는 `Application` 은 `UnityEngine.Application` 이 아니다
  (CS0234 3건 이력). `UnityEngine.Application.logMessageReceived` 처럼 **완전 수식** 필수.
  검증: `grep -nE '(^|[^.a-zA-Z_])Application\.' <file>` 이 0건이어야 한다.
- **`LogLevel` 이 `Hexiege.Application` · `Hexiege.Infrastructure` 양쪽에 있다.**
  인터페이스 구현 시그니처는 `Hexiege.Application.LogLevel` 로 완전 수식해야 구현으로 인정된다.
- `Infrastructure/Debug/LogSessionOwner.cs` 는 **의도적으로 `using` 이 하나도 없다.** 새 타입도 완전 수식으로 쓴다
  (`System.Collections.Generic.Dictionary`, `System.Diagnostics.Stopwatch`).
- `LogEvent` enum 은 `Application/Interfaces/ILogSink.cs` 에 있다(2026-08-20 기준 멤버 37개).
  **[🔴 2026-09-08 correction — original line kept above]** The count is now **41**: the random-map
  phase-2 step K added four map keys (`MapPreparationSucceeded` · `MapPreparationUsedFallbackTemplate` ·
  `MapPreparationFailed` · `MapProjectionFailed`). Details → [logging.md](logging.md).
  **[🔴 2026-09-14 correction — both lines above kept]** The count is now **46**: random-map phase-3
  step E added five transfer keys (`MapTransferSucceeded` · `MapTransferRetried` · `MapTransferFailed` ·
  `MapHashMismatch` · `MapClientVerificationFailed`). 🔴 Four of them are mutually exclusive outcomes,
  but `MapTransferRetried` is **not an outcome** — it is an intermediate state transition, so one match
  can legitimately emit two lines. Details → [logging.md](logging.md).

## NGO(Netcode) 관용구

- **`IsServer` 는 "이 오브젝트가 살아 있는가" 가 아니다.** `NetworkManager.Shutdown()` 뒤에도 참일 수 있어
  늦은 `Update` 가 통과하고 RPC 발신이 *"Rpc methods can only be invoked after starting the NetworkManager!"* 로 터진다.
  → 서버 틱/RPC 발신 자리는 **`if (!IsSpawned || !IsServer) return;`**.
  `IsSpawned` 를 **앞에** 두는 이유는 단락 평가로 싱글플레이(미스폰)에서 `IsServer` 를 건드리지 않기 위해서다.
  선례: `NetworkUnit:291` · `NetworkCombatController:310`(Update) · `NetworkGameEndController:457` · `UnitFactory:533`.
- 🔴 **부호가 반대인 `if (IsServer) return;` 과 혼동하지 마라.** 그것은 **ClientRpc 수신부**에서
  서버의 중복 처리를 막는 정반대 목적이다. 고치기 전에 그 가드가 무엇을 막는지 확인한다.
  (`NetworkTileSync.BroadcastTileChangeClientRpc` 의 것을 잘못 고치면 클라 타일 색이 통째로 죽는다.)
- **host 는 서버이자 클라이언트다.** `ClientRpc` 안의 로그는 `if (IsServer) return;` **뒤**에 둬야
  같은 사건이 host 파일에 두 줄로 남지 않는다(LogRules 1.14 금지 9).
- **가드 자체에는 로그를 넣지 않는다** — 가드에 걸리는 것은 정상 종료 흐름이고 상태 *전이* 지점이 아니라
  LogRules 1.14 금지 8(매 틱 로깅 금지)에 걸린다.

## 알려진 잔존 구멍 (2026-08-20 기준)

- ~~`NetworkCombatController` 의 게임 종료 구독 0건 / `OnUnitDied` 가드 부족~~ → **2026-08-19 해소.**
  `_combatStopped` 플래그 + 6개 핸들러 가드. 상세는 `network-infra.md` 「네트워크 종료 시점 뒷정리」 참조.
- ~~`NetworkProductionController` / `NetworkResourceSync` / `NetworkTileSync` / `NetworkHealthSync` /
  `NetworkGameEndController` 전수 점검 미실시~~ → **2026-08-20 해소(8곳).** 상세는 `network-infra.md`.
- **아직 안 봄 / 범위 밖으로 남긴 것**
  - `NetworkUnit.SetAnimState`(`NetworkUnit.cs:170`) — `IsServer` 만 본다. 다만 유일한 호출부인
    `NetworkCombatController.SetUnitAnimState` 가 이미 막혀 있어 중복이다.
  - `NetworkGameEndController` 의 `_localRematch*` 3종 — `ServerRpc` 이고 `IsServer` 블록 **밖** 구독이라
    `!IsSpawned` 만 필요하다.
  - `ProductionTicker.Update`(`Presentation`) — 종료 가드 없음. 길목으로는 더 근본적이나 동작 변경이라
    별도 설계 판단이 필요하다.
  - `NetworkBuildingController` / `NetworkUpgradeController` — `GameEvents` 구독이 없어 이번 전수 대상에서
    제외됐다. 다른 형태의 구멍 유무는 확인하지 않았다.
- **싱글플레이의 같은 낭비**: `GameBootstrapper.Update`(530~590행)도 게임 종료 후 쿨다운/파도/HoT/자연회복/
  연구/물안개 틱을 계속 돌린다. 네트워크가 없어 오류는 안 나고 낭비만 있다.

## 조사 습관 (실제로 틀려 본 것들)

- **진입점의 이름만 보고 판단하지 않는다.** "`Update()` 가 없다" / "코루틴이다" / "`grep` 에 안 잡힌다" —
  셋 다 근거가 되지 못한다. 본문과 호출 경로를 끝까지 따라간다.
  - 실패 1: "다른 컨트롤러엔 `Update` 가 없으니 안전" → 코루틴을 보지 않았다.
  - 실패 2: "코루틴이라 위험" → 본문에 `yield return` 이 하나도 없어 한 프레임에 끝났다.
  - 실패 3: `grep` 으로 "구독 해제를 안 한다" → 헬퍼 메서드로 하고 있었다.
  - (`ReconnectionHandler.WaitAndForceWin` 은 30초 코루틴이지만 `OnNetworkDespawn` 이
    `StopCoroutine` 으로 정리하므로 구멍이 아니다.)
- **한 파일에서 한 핸들러만 고치면 같은 버그가 다른 경로로 재발한다.** 구독 목록을 전수로 훑는다.
- **실측값은 표본 하나로 단정하지 않는다.** Shutdown~디스폰 창을 "27ms" 로 적었으나
  4회 표본은 6·25·27·41ms 였다. 41ms 는 60fps 에서 2~3 프레임이다.

## 문서/메모리 검사 도구 `Tools/check_docs.py`

- 읽기 전용 검사기. 기본 실행 `python3 Tools/check_docs.py` → **0건 / 종료 코드 0** 이 기준선이다.
- 검사 **7종**: `[1]~[5]` 문서 참조 정합성 · `[6]` 고아 토픽 · `[7]` 폴더 총합 행수 감소.
- 🔴 **`--root` 를 메모리 폴더로 돌리지 마라** — 규칙 정의를 못 찾아 `[1][3][4][5]` 가
  조용히 "이상 없음" 을 낸다. `[6]`·`[7]` 의 경로 인자는 **`--memory-root`** 다.
- 🔴 `_baseline.json` 에 도구가 직접 쓰는 일은 없다. 갱신은 `--update-baseline`(감소면 `--reason` 필수)뿐.
- 🔴 **검사 사각지대 있음** — Map · RandomMap · Upgrade 의 규칙 **33개는 검사기에 등록되지 않아**
  그 문서를 가리키는 규칙 번호 참조는 [3]에서 그냥 통과한다(2026-08-25 확인, 미해결).
- 상세(옵션 · `--reason` 규칙 · `known_orphans` · 2026-08-25 범위 확장 · 알려진 한계)
  → [check-docs-tool.md](check-docs-tool.md)

## 자기 검증 스크립트

- 🔴 **Domain 레이어는 진짜로 컴파일해서 돌릴 수 있다(2026-09-07).** `apt-get install -y mono-mcs` →
  `mcs`/`mono`. `Domain/Map/**` + `Domain/Hex/*` + `Common/TeamId.cs` 는 Unity 없이 단독 빌드된다.
  「컴파일러가 없어 추론만 했다」는 종전 전제는 **더 이상 사실이 아니다.** 절차·제약(C# 7.2 한계,
  수정 전/후 사본 2벌 비교)과 이 방법으로 찾아낸 기존 컴파일 오류 1건 → [hex-grid.md](hex-grid.md) 맨 끝.
- 중괄호 개폐 균형은 **주석·문자열 리터럴을 걷어낸 뒤** 세야 한다. 단독행 카운트나 `{` 총계는 오탐이 잦다
  (문자열 보간 `$"{x}"` 때문). 파이썬으로 스트립 후 세는 것이 유일하게 신뢰할 수 있다.
- ⚠️ **주석에 `Debug.Log` / `GameLog.Dev.` / `Pos=` / `if (IsServer) return;` 같은 검증 grep 대상 낱말을
  쓰지 마라.** 그 자체가 오탐이 된다(2026-08-20 `NetworkTileSync` 에서 `return` 수가 2→4 로 세어짐).
