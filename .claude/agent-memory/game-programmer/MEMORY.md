# game-programmer 메모리 — 인덱스

프로젝트 규칙 단일 소스는 리포지토리의 `CLAUDE.md` / `AGENTS.md` / `Assets/_Project/Docs/`.
여기에는 **코드에서 반복적으로 발을 헛디딘 지점**만 적는다. 충돌하면 항상 프로젝트 문서가 옳다.

> ⚠️ **이 파일을 갱신할 때는 `Read` 로 먼저 읽고 `Edit` 로 해당 부분만 고친다.**
> `Write` 로 전체를 다시 쓰지 마라 — 2026-08-20 에 실제로 그렇게 해서
> 「알려진 잔존 구멍」 등 앞선 세션의 지식이 통째로 사라졌다.

## 토픽 파일 인덱스

> ⚠️ **여기서 링크가 빠진 토픽 파일은 존재하지 않는 것과 같다**(`.claude/MEMORY.md` 갱신 규칙 5).
> 이 인덱스는 2026-08-17 `675203ae` 에서 통째로 지워져 토픽 16개가 고아가 됐고,
> 2026-08-21 에 복원했다. **토픽 파일을 새로 만들면 반드시 이 목록에 한 줄을 추가한다.**
> 아래 18개 = 이 폴더의 `.md` 전부(`MEMORY.md` 제외).

### 먼저 읽어야 하는 것

- [logging.md](logging.md) — GameLog / sink / RuntimeLogger 구조, 판정 선례표, `key=value` 확정 매핑,
  전역 로그 훅(4겹 방어 + 스로틀). **로그 관련 작업은 여기부터 읽는다.**
- [network-infra.md](network-infra.md) — NGO 컨트롤러 구조, 스폰 레이스, **종료(Shutdown) 시점 뒷정리 관례 +
  `_combatStopped`(게임 종료 후 서버 틱 정지) 패턴**, UGS/동기화/팀 할당/승패 Phase 1~8 상세.
  **네트워크 작업은 여기부터 읽는다.**

### 시스템별 (2026-06-23 재구성)

- [architecture.md](architecture.md) — 레이어 구조/제약, 정적 홀더, GameBootstrapper, SO Config 패턴,
  DontDestroyOnLoad, **에디터 셋업 스크립트 패턴 + 배치 관례(`Assets/Editor/Setup/`·`Hexiege.EditorTools`)
  와 저장 반영(`SetDirty`+`MarkSceneDirty`)**
- [network.md](network.md) — NGO API 제약, RPC 래퍼 패턴, GO 파괴 전파, 같은 씬 재로드, 동기화 타이밍, 회전/위치 동기화
- [ui-system.md](ui-system.md) — UIManager, BlockingOverlay, SceneLoader, LoadingIndicator, Canvas SortingOrder,
  CanvasGroup/레이아웃/팝업/ToastUI 패턴, 생산·연구 패널 실측 구조,
  **건물 패널 골격(`Row0~2`) + 회전 테두리 머티리얼 `_Radius`·`_Inset` 공유 함정**
- [unit-building.md](unit-building.md) — 유닛 이동/전투 V3, 회전, 혼잡도, 다중히트, 건물 배치/철거/업그레이드/환불,
  생산 PendingQueue, AutoTower, 랠리포인트
- [hex-grid.md](hex-grid.md) — 헥스 좌표계, HexMetrics, ViewConverter, 타일 소유권, 그리드 렌더링, 패스파인딩,
  카메라, URP RT 잔상, **거리 비교는 `HexCoord.Distance`(도메인 정수) 우선**
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
- 검사 **7종**: `[1]~[5]` 문서 참조 정합성(기존) · **`[6]` 고아 토픽 · `[7]` 폴더 총합 행수 감소**(2026-08-21 추가).
- `[6]`·`[7]` 은 `.claude/agent-memory/` 를 본다. 경로 인자는 **`--memory-root`**(기본 `.claude/agent-memory`).
  🔴 **`--root` 를 메모리 폴더로 돌리지 마라** — `parse_rule_docs()` 가 하위 `GameSystemRules/` 를 전제해서
  그 폴더가 없으면 `docs` 가 빈 딕셔너리가 되고 **`[1][3][4][5]` 가 조용히 "이상 없음"** 을 낸다.
- `[7]` 기준값은 **`.claude/agent-memory/_baseline.json`** (폴더별 `files`/`lines`). **임계값 0 — 감소는 전부 보고.**
  도구는 이 파일에 **절대 쓰지 않는다**. 갱신은 `--update-baseline` 플래그로 사람이 명시적으로만 한다
  (자동이면 사고가 그대로 새 기준이 되어 도구를 한 번 돌리는 것만으로 사고가 지워진다).
- **`--reason` 사용법 (2026-08-24 커밋 `2b3f2c6a` 신설)** — 감소를 기준값에 반영하려면
  `python3 Tools/check_docs.py --update-baseline --reason "왜 줄었는지"` 가 필요하다.
  사유 없이는 **거부(EXIT=2)** 되고 **기준값 파일은 열리지도 않는다**(불변).
  사유를 주면 `change_log` 에 날짜·폴더별 증감·사유가 **자동 기록**된다.
  - **증가만이면 `--reason` 없이 통과**한다(`change_log` 미기록. `--reason` 을 주면 opt-in 기록).
  - **`--reason` 만 주고 `--update-baseline` 을 빠뜨리면 오류로 차단**된다 — 조용히 무시하면
    "사유를 남겼다"고 착각한 채 지나가기 때문이다.
  - 같은 커밋에서 `실제 > 기준값` 인 폴더를 **안내 블록으로 출력**하게 됐다(드리프트 알림).
    **문제로 집계하지 않고 종료 코드에도 넣지 않는다** — 집계하면 `WORKFLOW.md` [11]③ 의 "0건 확인"이 막힌다.
- 🔴 **`.claude/MEMORY.md` 를 참조할 때 행 번호를 쓰지 말고 「갱신 규칙 N」으로 쓴다.**
  2026-08-24 에 `32~44행` 표기가 실제로는 `51~61행` 으로 밀려 docstring·`[6]`·`[7]` 출력·`_baseline.json`
  참조가 전부 어긋났다(가리키던 자리는 아키텍처 제약 표였다). 행 번호는 문서가 자라면 반드시 거짓이 된다.
- 같은 파일의 **`known_orphans`** 목록에 있는 `[6]` 항목은 출력에만 보이고 종료 코드엔 안 들어간다.
  **줄이는 방향은 자유, 추가는 사람 승인.** 현재 등록: `project-orchestrator/roadmap-3d.md`(224행).
- ⚠️ 「`MEMORY.md` 200행 초과」 검사는 **계획에 있었으나 폐기됐다.** 호출 세션의 프로브 4건에서
  `MEMORY.md` 가 에이전트 시스템 프롬프트에 **아예 자동 주입되지 않음**이 확인돼(200행에서 잘리는 게 아니라
  0행이 실린다) 전제가 무너졌다. *(내가 직접 측정한 값이 아니라 전달받은 값이다.)*
  → 그래서 신규 검사 번호가 `[6]`·`[7]` 이다. 옛 Plan 문서의 `[6][7][8]` 3종 번호와 어긋나니 주의.

## 자기 검증 스크립트

- 중괄호 개폐 균형은 **주석·문자열 리터럴을 걷어낸 뒤** 세야 한다. 단독행 카운트나 `{` 총계는 오탐이 잦다
  (문자열 보간 `$"{x}"` 때문). 파이썬으로 스트립 후 세는 것이 유일하게 신뢰할 수 있다.
- ⚠️ **주석에 `Debug.Log` / `GameLog.Dev.` / `Pos=` / `if (IsServer) return;` 같은 검증 grep 대상 낱말을
  쓰지 마라.** 그 자체가 오탐이 된다(2026-08-20 `NetworkTileSync` 에서 `return` 수가 2→4 로 세어짐).
