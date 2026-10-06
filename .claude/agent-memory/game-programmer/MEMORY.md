# game-programmer 메모리 — 인덱스

프로젝트 규칙 단일 소스는 리포지토리의 `CLAUDE.md` / `AGENTS.md` / `Assets/_Project/Docs/`.
여기에는 **코드에서 반복적으로 발을 헛디딘 지점**만 적는다. 충돌하면 항상 프로젝트 문서가 옳다.

## 토픽 파일 인덱스

> 아래 19개 = 이 폴더의 `.md` 전부(`MEMORY.md` 제외).

### 먼저 읽어야 하는 것

- [logging.md](logging.md) — GameLog / sink / RuntimeLogger 구조, 판정 선례표, 로그 키 확정 매핑,
  전역 로그 훅. 🔴 **로그 관련 작업은 여기부터 읽는다** — 세부 항목과 정정 블록 목록은
  그 파일 맨 앞의 「이 파일이 담는 것」 목차에 있다.
- [network-infra.md](network-infra.md) — NGO 컨트롤러 구조와 스폰 레이스, 종료(Shutdown) 시점 뒷정리
  관례, UGS · 동기화 · 팀 할당 · 승패 Phase 1~8, 무작위 맵 전송(3단계 B~I)과 재경기 맵,
  맵 테스트 모드 삭제, 경기 종료 후 이탈 통보·자체 감시, 개발용 강제 실패 플래그들.
  🔴 **네트워크 작업은 여기부터 읽는다** — 회차별 세부 항목과 🔴 **Phase 7 머리의 정정 블록**까지
  그 파일 맨 앞의 「이 파일이 담는 것」 목차가 안내한다.

### 시스템별 (2026-06-23 재구성)

- [architecture.md](architecture.md) — 레이어 구조/제약, 정적 홀더(맵 인계 홀더 · 루트 seed 포함),
  GameBootstrapper, SO Config 패턴, DontDestroyOnLoad, 에디터 셋업 스크립트 패턴과 배치 관례,
  기존 프리팹 에셋을 여는 스크립트의 작업 순서 — 세부는 그 파일 맨 앞의 목차 참조.
- [network.md](network.md) — NGO API 제약, RPC 래퍼 패턴, GO 파괴 전파, 같은 씬 재로드, 동기화 타이밍, 회전/위치 동기화
- [ui-system.md](ui-system.md) — UIManager, BlockingOverlay, SceneLoader, LoadingIndicator,
  Canvas SortingOrder, CanvasGroup/레이아웃/팝업/ToastUI 패턴, 생산·연구·건물 패널 실측 구조,
  알림 팝업 프리팹 전제 조건, 그리고 **결과 화면**의 회차별 상세(상대 이탈 반영 · 재경기 준비/실패 ·
  진단 로그 · 싱글 맵 준비·투영 실패 안내).
  🔴 **결과 화면 · 팝업 · 상태 줄 문구 작업은 여기부터 읽는다.**
  🔴 **주석 사본 함정의 상세와 「코드 본문 SHA-256 으로 동작 무변경을 증명하는 절차」도 이 파일에 있다** —
  회차별 항목과 정정 블록 목록은 그 파일 맨 앞의 「이 파일이 담는 것」 목차가 단일 소스다.
- [unit-building.md](unit-building.md) — 유닛 이동/전투 V3, 회전, 혼잡도, 다중히트, 건물 배치/철거/업그레이드/환불,
  생산 PendingQueue, AutoTower, 랠리포인트
- [hex-grid.md](hex-grid.md) — 헥스 좌표계, HexMetrics, ViewConverter, 타일 소유권, 그리드 렌더링,
  패스파인딩, 카메라, URP RT 잔상, `HexTile` 상태 계약, 무작위 맵 1~3단계(결정적 PRNG · 격자 전환 ·
  투영 · 판정 조건 전환 · 함수 분할)와 그 정정 블록, Domain 레이어 단독 컴파일 절차.
  🔴 **타일 상태 · 건설/점령 판정 · 맵 생성 작업은 여기부터 읽는다** — 세부는 그 파일 맨 앞의 목차 참조.
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

- **git 명령 절대 금지**(규칙 5) — 검증도 git 없이 한다. 변경 전후 비교는 호출자에게 맡긴다. **[🔴 2026-10-06 correction — original kept: this line is no longer accurate. `CLAUDE.md` rule 5 was revised and now allows a short list of read-only git commands (including diff and log) without being told, so comparing before/after yourself is permitted; every other git command still needs the user to name it. Read `CLAUDE.md` rule 5 for the current list — not copied here.]**
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
  **[🔴 2026-09-15 — all lines above kept]** The count is **still 46**. The rematch-map work added
  **no key at all**; it added one **field**, `Round=` (`First`/`Rematch`/`Probe`), to the five
  `MapTransfer*` keys. 🔴 **The reason is reusable**: splitting first-match vs rematch into separate
  keys would split the transfer success/failure aggregate in two, and the remedial action is identical
  either way — so it fails both of LogRules 1.5's new-key tests and belongs in a `key=value` field.
  Details → [network-infra.md](network-infra.md) 「재경기 맵 A~D」 B.

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
- 🔴 **「증상 0건」은 「정상」이 아니다 — 결함 두 개가 서로를 가릴 수 있다(2026-10-01 실측 확인).**
  상쇄 관계인 둘을 찾았으면 **어느 쪽을 먼저 넣어야 중간 상태가 안전한가**부터 정한다(한쪽만 고치면 터진다).
  🔴 그리고 **「관측 불가」는 「지금」과 「영원히」를 갈라 적는다** — 가리던 결함을 고치면 열리는 종류가 있다.
  상세 → [ui-system.md](ui-system.md) 「네 줄이 설계대로 남았다」 (3)·(4).
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
  🔴 **2026-09-29로 같은 함정 5회째다(UI 문구 → 대입문·로그 필드 → 이번엔 판별식 **식별자**).
  「알고 있다」·「그 항목을 읽었다」로는 막히지 않았다 — 막은 것은 절차다:
  **주석을 쓴 뒤 그 회차의 검증 grep 을 전부 재실행한다.** 상세 → [ui-system.md](ui-system.md).**
  **[🔴 2026-09-29 2차 갱신 — 위 줄은 지우지 않는다(B-7). 회차와 대상 범위가 늘었다]**
  🔴 **6회째(한 회차에 두 번). 대상은 이제 **XML 태그 토큰**과 **검사가 세는 타입 이름**까지 —
  즉 「그 회차의 검증 grep 이 세는 모든 토큰」이고 목록 암기로는 막히지 않는다.
  ⚠️ 강제 실패 선례 파일 3개의 머리말은 아직 오탐을 낸다(내 위반으로 오해하지 말 것).
  상세 → [ui-system.md](ui-system.md) 「규칙 M-4 후속 H~M」 (6).**
  **[🔴 2026-09-29 3차 갱신 — 위 두 블록은 한 글자도 지우지 않는다(B-7). 아래가 이번 회차다]**
  ✅ **7회째는 나지 않았다 — 같은 절차가 또 값을 했다**(주석을 고친 직후 그 회차의 셈 전부를 재실행).
  ✅ **바로 위 ⚠️ 의 「강제 실패 선례 파일 3개의 머리말 오탐」은 이번 회차에 해소됐다** — 그 셋을
  성격 표현으로 고쳐 **에디터 전용 가드 밖 언급이 전부 0건**이 됐다(네 번째 파일은 원래 올발라 무변경).
  🔴 **이번에 새로 확정한 판정 기준: 「그 낱말이 나오는가」가 아니라 「따옴표로 감싸 값을 베꼈는가」다.**
  낱말로 세면 기능을 설명하는 산문까지 지우게 되고 그것은 **과잉 수정**이다 —
  그래서 완료 조건을 **0 이 아니라 「2 가 남아야 정상」**으로 적은 자리가 있었다.
  ⚠️ **`//` 줄 주석에 `<b>` 를 쓰지 않는다**(렌더링되지 않고 태그를 세는 검사에만 걸린다).
  상세 → [ui-system.md](ui-system.md) 「경기 종료 후 UI 마무리 회차」 (2).
  **[🔴 2026-09-30 4차 갱신 — 위 블록들은 한 글자도 지우지 않는다(B-7). 아래가 이번 회차다]**
  ✅ **8회째도 나지 않았다 — 같은 절차가 세 회차 연속으로 값을 했다.** 🔴 **이번에 위험한 자리는
  「기존 주석」이 아니라 「새로 쓰는 문자열 리터럴」이었다** — 로그 메시지에 화면 문구 낱말을 품은
  리터럴을 두 자리 썼고, **쓴 직후 셈을 돌려** 코드 리터럴 안의 그 낱말이 **1 → 3** 으로 늘어난 것을
  잡아 성격 표현으로 되돌렸다(기준선 1 복귀). 🔴 **새로 쓰는 글은 선택이 자유로우므로 그 낱말을
  아예 피하는 것이 사본 여부를 따지는 것보다 싸다** — 기존 산문을 건드리면 과잉 수정이라는
  2026-09-29 3차의 결론과 짝이 되는 판정이다.
  상세 → [ui-system.md](ui-system.md) 「두 팝업의 관측 공백을 메운 회차」 (4).
- ✅ **삭제·정리 회차의 기본 증명 수단 = 코드 본문 SHA-256**(주석·문자열 리터럴을 파싱으로 걷어내고
  공백 제거 후 해시 → 수정 전/후 비교). 🔴 **행수·중괄호 총계는 증거가 못 된다.**
  2026-09-28·2026-09-29 **2회 연속**으로 「동작 변경 0건」을 이것으로 증명했다(후자는 52줄 삭제 전후 동일,
  12,735자). 절차 상세 → [ui-system.md](ui-system.md).
- 🔴 **검사를 만들면 그 검사의 **음성 통제**를 1회 돌린다(2026-09-29 2차).** 「검사가 통과했다」와
  「검사가 실패를 잡는다」는 다른 명제다 — 일부러 깨뜨린 사본이 실제로 깨지는 것까지 확인해야 그 검사에
  값이 생긴다. 상세 → [network-infra.md](network-infra.md) 「강제 실패 플래그 ⑥」 (3).
- 🔴 **스택 트레이스의 코루틴 클래스 번호(`d__NN`)로 「이 로그가 새 빌드의 것인가」를 가르는 기법은
  **이번에 고친 클래스가 그 번호의 주인일 때만** 쓸 수 있다(2026-09-30 한계 실측 — 다른 파일만 고친
  회차는 번호가 직전 회차와 같다). 상세 → [ui-system.md](ui-system.md) 「경기 종료 후 UI」 마무리 회차 (8).
  **[🔴 2026-10-01 보완 — 위 줄은 지우지 않는다(B-7)]** 그 기법을 못 쓰는 회차에는
  **값 자체가 빌드를 가리키는지**를 묻는다(수정 전 코드에서 구조적으로 나올 수 없는 값 3개가
  동시에 들어맞았다). ⚠️ **빌드 지문이 아니라 간접 근거**이므로 지문이 있으면 그쪽이 우선.
  상세 → [logging.md](logging.md) 「로그로 빌드를 가르는 법」의 2026-10-01 보완 블록.
- 🔴 **수정 전/후를 가르는 「표식」까지 설계해야 로그가 판정이 된다(2026-10-01 실기 통과).**
  경로를 끝까지 따라가 **로그가 남을 순서를 미리 적고**, 그중 **수정 전후로 값이 달라지는 자리**를
  표식으로 고른다 — ⚠️ **「경고 줄의 유무」는 표식이 못 됐다**(수정 전에도 남는 줄이었다).
  ✅ 코드 판독만으로 세운 예측이 실기와 한 글자도 어긋나지 않았다.
  상세 → [ui-system.md](ui-system.md) 「네 줄이 설계대로 남았다」 (2).
- 🔴 **낡은 주석은 「갱신 블록 바로 옆」에 가장 많이 남는다(2026-10-05 전수 판정 실측).**
  고친 사람이 **자기가 쓴 줄만** 보기 때문에, 같은 메서드 안에서 ✅ 갱신 블록과 ⚠️ 낡은 블록이
  나란히 있는 자리가 생긴다. **갱신 블록을 쓸 때는 그 메서드의 주석을 처음부터 다시 읽는다.**
  🔴 **그리고 셈이 적힌 주석(「N곳」·「깃발 N개」·「하는 일은 N가지」)은 세는 대상이 늘 때 반드시
  낡는다 — 숫자를 다시 적지 않고 종류만 적는다.** 상세 → [ui-system.md](ui-system.md)
  「생산·연구·결과 화면 7파일 주석 전수 판정」 (1)·(2).
- 🔴 **검증 수단이 「조용히 실패」했는지를 먼저 확인한다(2026-10-05 전수 판정 실측).**
  ① 해시 역검증에서 **치환 패턴이 들여쓰기와 어긋나 파일이 전혀 바뀌지 않았는데 「같다」가 나왔다** —
  아무 변경도 없는 것을 증거로 쓸 뻔했다. **사본마다 원본과 실제로 다른지 먼저 비교한다.**
  ② **호출처를 셀 때 머리말 예시 주석이 히트해 「0건」이 「1건」으로 보였다** — 호출처를 셀 때는
  주석 줄을 제외한다. 🔴 **그리고 「모든/항상/완전히」로 시작하는 단정은 기능이 늘 때마다 거짓이
  되므로 전칭 낱말이 보이면 그 집합을 전수로 센다**(이 회차 거짓 12자리 중 4자리가 이 부류).
  상세 → [ui-system.md](ui-system.md) 「공통 UI 4파일 · UI 계약 3파일 · 맵 재조립기」 (1)·(2)·(9).
- 🔴 **주석 판정의 가장 값싼 수단은 「읽기」가 아니라 「돌려 보기」다(2026-10-05 맵 준비 조정자 회차).**
  `Domain/Map/**` 에 `Application/UseCases/MapPreparationUseCase.cs` 까지 얹어 `mcs`/`mono` 로
  자기 검증을 한 번 돌리면 고정 기대값 · 결정성 · 유형별 허용 범위 · 5개 유형 폴백 경로가
  **한꺼번에 참으로 확정**된다 — 코드를 읽어 추론하는 것보다 빠르고 추정이 섞이지 않는다.
  🔴 **반대로, 조건부 컴파일이 붙은 자기 검증 진입점은 맵 계통 13개가 전부 호출처 0건이라
  「에디터에서 저절로 돌아 사양을 지킨다」는 전제는 성립하지 않는다.**
  상세 → [hex-grid.md](hex-grid.md) 「맵 준비 조정자 주석 전수 판정 회차」 (5)·(6).
- 🔴 **"I opened the rule section" is not "that clause governs this sentence" (2026-10-06, volume binder).**
  A cite passed because the section existed and held the topic word; the sentence's actual clause was in
  another rule (reset is rule 25, the mute release is rule 27). For each cite, find the sentence in the rule
  text that states the claim; if you cannot, the cite is wrong or too broad. Detail → [ui-system.md](ui-system.md)
  "Volume control binder — rule-citation re-judgement".
- 🔴 **A header fix does not fix the class summary next to it, and "the only difference / unlike X / will be added" are universals (2026-10-06, popup/shared-overlay 8 files).** After correcting a file header re-read every `<summary>` that restates the same flow; list both bodies before writing "only"; count real callers before writing "another user is coming". Details → [ui-system.md](ui-system.md) 「2026-10-06 Popup / shared-overlay 8 files」.
- 🔴 **"The only guard" / "never held by anyone else" are universal claims — grep the callee for its own check first (2026-10-06, building/production/research panel family, 7 files).** A UI guard comment said deleting it disables the cooldown; the use case re-checks it. Also: before deleting a false sentence that sits next to a true one (clockwise sweep vs. checkbox state), read the serialized value in the scene. Details → [ui-system.md](ui-system.md) 「2026-10-06 Building / production / research panel family」.
- 🔴 **"There is no event for X" and "X still happens with zero Y" both need a measurement, not a reading (2026-10-06, HUD / lobby / floating-text 6 files).** A prior pass had just corrected a HUD header and the correction was itself false twice: the event hub *does* publish a resource-change event (other panels subscribe; only the HUD polls), and the income branch it cited is zero in the config asset. Grep the hub before writing "no event"; read the serialized asset before writing "still rises". Detail → [ui-system.md](ui-system.md) "HUD / lobby / floating-text group".
- 🔴 **A rule's first bullet is not the rule — a "correction" built on it was itself false (2026-10-06, result screen).** A mark like "wording undecided" is kept verbatim after a later dated block closes it; read every appended block before judging, and when a doc corrects a measured figure, grep the code comments for the old figure too. Detail → [ui-system.md](ui-system.md) "Result screen + five small UI files — comment audit" (1)·(2).
- 🔴 **A claim fixed at one site lives on at its siblings — search the claim, not the site list (2026-10-06, 11 files).** Handed over 6 / 5 / 1 sites; found 9 / 5 / 2: one noun spelled with a space and one editor setup script were missed by the list, and the event hub's own doc comment repeated the "published last" claim. Grep key nouns repo-wide, case-insensitive, spelling variants included; judge hedged "e.g." examples before touching them. Detail → [ui-system.md](ui-system.md) "2026-10-06 Comment audit follow-up".
- 🔴 **A sweep for one claim must cover the declaring file and every wording of it, and a "zero hits" hand-over can be a wrong search root (2026-10-06, 9 files).** Handed 16 sites (the heading said 15); fixed 28: the event hub and a registry owner repeated stale "not yet" claims, and a skill asset file the hand-over said had none had three. Also: re-judge every cite in a file by the clause, not the section (two header range cites were false). Detail → [ui-system.md](ui-system.md) "Comment audit, outside the UI / random-map scope".
