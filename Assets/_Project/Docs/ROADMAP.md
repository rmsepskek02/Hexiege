# Hexiege - 작업 로드맵

**최종 수정일:** 2026-08-21

**구현 완료 (2026-08-20) — ⚠️ 코드 적용 완료 · 실기 미검증:** **네트워크 종료 시점 가드 전수 보강 8곳** (커밋 `bcf45ec1`, Plan 커밋 `2f255b33`). 직전 커밋 `55d24d83` 이 `NetworkCombatController` **한 파일**의 구멍 6곳을 고친 뒤, **같은 성질의 구멍을 전수 조사**해 **나머지 5파일 8곳**을 보강했다 — 전부 **`if (!IsSpawned || !IsServer) return;` 한 가지 형태**다. **[8곳]** `NetworkResourceSync.OnResourceChangedOnServer`(188, **신설**) · `NetworkTileSync.OnTileOwnerChangedOnServer`(164, **신설**) · `NetworkGameEndController.OnGameEndServer`(178) · `NetworkHealthSync.OnEntityDamaged`(131)·`OnEntityHealed`(186) · `NetworkProductionController.OnProductionStarted`(209)·`OnProductionQueueChanged`(253)·`OnUnitProduced`(319). 각 진입점은 **`ClientRpc` 를 보내거나(7곳) `NetworkVariable` 을 쓴다(1곳)**. **가드가 없던 2곳에 `IsServer` 가 새로 붙지만 동작은 안 바뀐다** — 두 구독 모두 `OnNetworkSpawn` 의 `if (IsServer)` 블록 안이라 **원래도 서버 전용**이었고, 목적은 **가드 모양 통일**이다. **[⭐ 8번째의 근거]** `OnProductionQueueChanged` 는 **사전 목록에 없었다.** Plan 작성 중 `SubscribeToProductionEvents` 를 읽다가 **같은 블록에서 나란히 구독되는 세 번째 형제**임을 확인했다 — **셋 중 둘만 막으면 같은 파일 안에서 가드 모양이 갈린다.** **[위험 구간은 정지 화면이 아니다]** `GameEndUI.cs` **343행 `Time.timeScale = 1f`** 가 **354행 `BackToLobby`** 보다 **먼저** 실행되므로, [로비로] 이후 Shutdown 까지 **생산·수입·타일 점령 틱이 정상 속도로 돈다.** **[🔴 수치 정정]** 이 구간은 **`27ms` 단일값이 아니라 실측 4회 기준 6~41ms**(25/27/41/6)다 — 최댓값 41ms 는 60fps 기준 **2~3 프레임**. 근거 `_Logs/_editor/2026-08-19/RuntimeLog.txt` 255·692·874·1398행 부근. 코드 주석의 `27ms`(`NetworkCombatController.cs` 880·920·1001·1078행)는 **범위 밖이라 미수정**(규칙 6). **[건드리지 않은 것]** **부호가 반대인 `if (IsServer) return;` 10곳**(Resource 2·Tile 1·Health 3·Production 4)은 **`ClientRpc` 수신부의 중복 처리 방지**라는 정반대 목적이라 **전부 무변경** — 특히 `NetworkTileSync.BroadcastTileChangeClientRpc` 의 것을 잘못 고치면 **클라이언트 타일 색이 통째로 죽는다.** **[로그 0건]** 가드는 **정상 종료 흐름**이고 상태 **전이** 지점이 아니라 `LogRules` **1.14 금지 8** 에 걸린다. `LogEvent` **37개 무변경**. **[검증 — 정적만]** 8곳 적용 · 반대 부호 10곳 무변경(삭제 6줄이 전부 `!IsServer`) · 중괄호 균형 전후 동일(20·23·47·52·137) · `return` 문 수 전후 동일 · `LogEvent` 37개. **⚠️ `NetworkTileSync` 의 `return` 이 2→4 로 세어지는 것은 오탐**(신설 1줄 + **혼동 방지용으로 인용한 문자열** 1줄, 162행). **⚠️ 단서:** **실기 재현 0회이자 실기 검증도 아직 안 했다**(이벤트 발행 시에만 실행되어 매 프레임 도는 `NetworkCombatController.Update` 와 위험도가 다르다. 후자는 **2회 재현**, 이번 8곳은 **0회**. **그럼에도 고친 이유는 「터진 적 없으니 놔둔다」 가 `NetworkCombatController` 를 방치했던 판단과 같고, 그것은 실제로 터졌기 때문**) · **「실기 PASS」 로 적으면 안 된다** · 고쳐졌음을 적극적으로 보일 수 없어 실기에서 볼 것은 **「멀쩡하던 것이 망가지지 않았는가」** 다. 상세: task `_Tasks/2026-08-20/12_24_network-guard-sweep/Plan.md` §13.

**문서/설정 정비 (2026-08-20):** **에이전트 메모리 손실 사고와 그 원인 제거.** `game-programmer` 가 `MEMORY.md` 를 **`Write` 로 통째로 재작성**해 **46행 → 28행**으로 줄며 `logging.md` 참조 링크 · `LogLevel` 이 **양쪽 네임스페이스에 있다는 함정** · `LogSessionOwner.cs` 의 `using` 부재 관례 · **host 는 서버이자 클라이언트**라 `ClientRpc` 안의 로그를 `if (IsServer) return;` **뒤**에 둔다는 규칙 · 선례 파일:행 목록 · **「알려진 잔존 구멍」 섹션 전체**(특히 **미해결인 `GameBootstrapper.Update` 530~590행 싱글플레이 틱 낭비**)가 소실됐다. **원인은 `.claude/agents/*.md` 6개 전부에 하드코딩된 두 줄** — ① 거짓 문장 `Your MEMORY.md is currently empty.`(파일이 실제로 비어 있던 시점에 굳어, 에이전트가 매 호출마다 *"비어 있다"* 고 믿고 시작 → `Edit` 대신 **`Write`**) ② 존재하지 않는 윈도우 절대경로(원격 리눅스 세션에 없어 **①의 오해를 확인할 방법조차 없었다**). **조치 완료:** 6개 정의 전부에서 상대경로 교체 + **`Read`→`Edit` 원칙 경고로 교체**(`.claude/agents/` 하위 `Dmain` 잔존 **0건** 확인), `game-programmer/MEMORY.md` **복원(92행)**, 원칙을 `AGENTS.md` 「에이전트 메모리」 절에 명문화. **⚠️ 남은 것:** `.claude/MEMORY.md`(공용) **33·36·44~49행**의 윈도우 절대경로 `d:/Dmain/...` — **아래 우선순위 표에 후속 항목으로 등록.** **[✅ 2026-08-20 해소 — 원문은 그대로 두고 덧붙인다: 사용자가 "이제 로컬에서 작업하는 경우가 없다"고 확정해 정리했다. 위 8행 교체 완료(파일 108행 유지). 이때 🔴 새 사실이 드러났다 — `.claude/settings.json` **8행 `SessionStart` 훅**이 `cat "d:/Dmain/.../CLAUDE.md"` 로 **존재하지 않는 윈도우 경로를 읽고 있어 원격 세션에서 계속 실패해 왔다**(= 절대 규칙 자동 로딩 장치가 죽어 있었다). 원인 ②와 같은 뿌리이며, **에이전트 정의 6개만 고쳤을 때 설정 파일에 이것이 남아 있었다는 점이 요점**이다. 함께 `.claude/agent-memory/project-orchestrator/MEMORY.md` **239행**(서브에이전트에게 윈도우 절대경로를 넘기라는 지시)도 교체. 상세는 아래 우선순위 표 해당 행 참조]** **[🔴 2026-08-21 추가 조사 — 원문은 그대로 두고 덧붙인다: 위 *"46행 → 28행"* 은 **최초 사고가 아니라 재발**이었고 손실 경로도 하나가 아니었다. ① **2026-08-17 커밋 `675203ae` 에서 같은 파일이 -450/+72 = 실질 `-378행`** 덮어써졌다 — **-18행보다 21배 크고 3일 앞선 사고이며 아직 미복구.** **[✅ 2026-08-21 복구 완료 — 원문은 그대로 두고 덧붙인다: 아래 우선순위 표의 「고아 토픽 16개 재링크」·「2026-08-17 소실분 대조 복구」 2행이 완료로 이동했다. 인덱스 복원으로 **토픽 18/18 링크**, 유일본 **7건**만 토픽 파일로 복구, **폴더 2,411 → 2,517행**. ②(200행 절삭)와 `check_docs.py` 메모리 검사는 미착수 유지]** 폴더 파일 수 19→19 그대로에 총 행수 2,503→2,125(정확히 -378)이라 **이동이 아니라 소실**이고, 커밋 제목은 `feat(log): 감사 누락 PlayerId 마스킹…` 으로 **메모리와 무관한 작업 중 곁다리로** 일어났다. 지워진 것: `## CRITICAL — GIT 명령 절대 금지` · 레이어/NGO API 제약 · DontDestroyOnLoad · **런타임 로그는 GameLog 경유** · 「최근 작업」 약 20건. ② **200행 절삭**(별개 경로, 커밋 이력에 안 남아 사고를 인지조차 못 한다) — 실측 초과 **qa-tester 502 · project-orchestrator 325 · game-design-lead 254**. ③ **고아 토픽 16개(1,839행)** — `game-programmer` 토픽 18개 중 16개가 인덱스 미링크(`work-history.md` 819 · `unit-building.md` 166 · `ui-system.md` 137 외 13개), **원인은 ①**(인덱스가 덮어써지면 토픽이 미아가 된다). ④ **✅ 프로젝트 문서(`Assets/_Project/Docs/`)는 정상** — 커밋 수 `ROADMAP` 35 · `PROJECT_STATUS` 20 · `WORK_HISTORY` 19 · `LogRules` 15(6월 이후)로 꾸준히 갱신됐고, **소실된 것은 에이전트 캐시**이며 ①에서 지워진 내용의 원본 대부분은 `CLAUDE.md` · `.claude/MEMORY.md` · `GameDesignDocument.md` 에 살아 있다. ⑤ **조치:** `Write` 금지만으로는 ②③이 막히지 않으므로 `.claude/MEMORY.md` 상단에 **「🔴 에이전트 메모리 갱신 규칙」 6개 항목**을 신설했다(**`Read`→`Edit` 순수 삽입**, 108 → 121행, 기존 108행 무손상 확인). `CLAUDE.md` 는 사용자·메인 세션 소통 전용으로 확정돼 손대지 않았다. **후속 4건은 아래 우선순위 표에 등록.** **※ 근거 구분(규칙 10):** ②③의 실측치는 이 세션에서 직접 확인했고, ①④의 커밋 해시·증감 행수·커밋 수는 **호출 세션이 git 이력을 대조해 전달한 값**이다(document-manager 는 규칙 5로 git 을 실행하지 않는다)]**

**구현 완료 (2026-08-19) — ✅ 실기 검증 통과 (재경기 2회 포함):** **게임 종료 · 서버 종료 시점의 전투 뒷정리** (커밋 `55d24d83`). **[① 가드 구멍 6곳]** 종료 시점에 네트워크가 꺼진 뒤 호출될 수 있는 서버 핸들러 전수 확인 — `OnUnitDied`·`OnBuildingDied` 는 `!IsServer` 만 있던 가드를 **`!IsSpawned || !IsServer`** 로, `OnUnitEnteredCombatHandler` 는 **가드 전무**였던 자리에 동일 형태로, Walk·HealCast·FreezeChanged 는 **공통 길목 `SetUnitAnimState` 한 곳**에 `!IsSpawned` 를 두어 **호출 지점 5곳을 한 번에** 덮었다. **⭐ `OnUnitDied` 만 고쳤으면 성 파괴 경로(`OnBuildingDied`)로 그대로 재발했을 것** — 게임을 끝내는 바로 그 경로다. **[② 게임 종료 시 전투 틱 정지]** `GameEvents.OnGameEnd` 를 **서버 전용** 구독해 `_combatStopped` 를 세우고 `Update` 진입부에서 검사 + `StopAllCoroutines()`. **구독 해제 방식은 기각** — `GameEndUseCase` 가 `OnBuildingDied` **디스패치 도중 동기적으로** `OnGameEnd` 를 발행해, 그 안에서 `Dispose()` 하면 구독 순서에 따라 **게임을 끝낸 성의 `EntityDiedClientRpc` 가 영영 안 나갈 수 있다.** **[🔴 최우선 위험 — 플래그 리셋]** `TickCombat` 에는 타워·파도·HoT·자연회복·연구·쿨다운·물안개·상태효과가 함께 들어 있어, 재경기에서 `true` 로 남으면 **게임이 영원히 끝나지 않는다.** `OnNetworkSpawn`(IsServer) · `OnNetworkDespawn` **양쪽**에서 `false` 로 리셋(같은 파일 `_attackTimer`·`_lastCarry` 선례 옆). **[✅ 실기 — 1408줄, 3경기 연속 = 재경기 2회]** ① **✅** `[ERROR]` **0건** · RPC 오류 **0건** · ② **✅** `게임 종료 — 전투 틱 정지` 경기당 정확히 **1회**(872·1090·1394행) · **재경기 ✅ 2회 연속 통과**(2경기 구간 전투 로그 **40건** / 3경기 구간 **68건** — 정상 재개). **⚠️ 단서:** **B(엔진 오류 수집) 여전히 미검증**(3경기 더 돌렸으나 오류 미발생 — **①·② 가 잘 돼서 검증 기회가 사라졌다**) · **`IsSpawned` 전이 시점 미확정**(NGO 소스 없음 — **27ms 창을 완전히 닫는다고 단정하지 않는다**) **[🔴 2026-08-20 수치 정정 — 원문 유지, 덧붙임: 이 구간은 `27ms` 단일값이 아니다. 같은 로그 4회 실측이 25/27/41/6 ms 이므로 정확한 표기는 `6~41ms`, 최댓값 41ms 는 60fps 기준 2~3 프레임이다]** · **Walk·HealCast·FreezeChanged 는 「예방」**(디스폰 후 NetworkVariable 쓰기가 오류를 내는지 미확정) · **`NetworkUnit.SetAnimState` 미수정**(다른 파일) · **다른 네트워크 컨트롤러 동일 구멍 미점검**(열지 않았으므로 있다/없다를 말할 수 없다). 상세: task `_Tasks/2026-08-19/10_16_combat-shutdown-cleanup/Plan.md` §10.

**구현 완료 (2026-08-19) — ⚠️ 부분 검증 (A ✅ / C ✅ / B 미검증):** **전역 로그 훅의 `Error` 수집 확대 + 전투 컨트롤러 RPC 가드 + 연구 흐름 로그 보강** (커밋 `cc864054`). **[B]** 전역 훅이 **`Exception` + `Error`** 를 수집해 엔진·NGO 의 `Debug.LogError` 가 파일에 남지 않던 구멍을 메웠다. 방어 1겹을 푸는 변경이라 **되먹임 방어 3겹 → 4겹**(`RuntimeLogger.IsEmittingToConsole` 신설 — `GameLog` 우회 직접 호출 경로까지 덮는다). `condition` 단위 **1초 스로틀 + `Suppressed=n`**(상한 32). `LogEvent` **36 → 37**(`UnhandledEngineError`). **[A]** `NetworkCombatController.Update` 가드 → `if (!IsSpawned || !IsServer) return;`. **[C]** `NetworkUpgradeController` 무로그 성공 경로에 로그 4줄(개발 3 / 운영 1 — 기존 키 재사용). **[+]** `NetworkProductionController` 의 `Pos=(4, 15)` **`key=value` 구분자 위반 1건**을 `Q=`/`R=` 로 수정. **[실기 704줄]** **A ✅ 해결** · **C ✅ 정상**(착수 6 ↔ 완료 6 1:1) · 되먹임·폭주 없음 · **⚠️ B 미검증 — 이번 세션에 엔진 오류가 나지 않아 확인할 기회가 없었다**(「동작 확인」이 아니다). **⚠️ 잔여 단서:** **A 의 원인인 스폰 레이스 이번에도 미발생**(정상 경로만 확인) · **`SetCameraStartPositionForTeam` 최종 삭제 여전히 잔여** · 범위 밖 2건(`OnUnitDied` → `EntityDiedClientRpc` 가드 구멍 / `NetworkCombatController` 게임 종료 구독 부재 — 종료 감지 후 약 3초간 전투 루프 지속). 상세: `LogRules.md` **1.5 / 1.9 / 1.11 / 1.13**, task `_Tasks/2026-08-18/20_07_log-coverage-and-rpc-guard/`.

**구현 완료 (2026-08-18) — 컴파일 통과 · ⚠️ 실기 미검증:** **로그 체계(`GameLog`) 전환 — 나머지 계층 이관 완료.** `Assets/_Project/Scripts/` 의 게임 런타임 코드 raw `Debug.Log` 이관이 끝나 **누적 386건**(선행 205 + 이번 **181**)이 `GameLog` 를 경유한다. 배치별 1-A **65** / 1-B **23** / 2 **50**(신규 3 포함) / 3·4 **43** + `system` 교정 **16**. `LogEvent` **36개**, `Unknown` 사용 **0건**. **최종 잔존 46건**은 전부 남을 이유가 있다 — `GameLog.cs` 9(sink 폴백) · `ILogSink.cs` 2(주석) · `Infrastructure/Debug/` 19(로그 본체) · **`Assets/Editor/` 16(미적용 — 에디터 도구는 sink 가 없어 이관 효과가 원리적으로 0. 규칙 예외가 아니라 현행 구현의 한계)**. **함께 수정한 버그 3건**(커밋 `da5eeaab`): `NetworkUpgradeController` 완료 훅 구독 누락(클라 브로드캐스트만 죽던 간헐 버그) · `SetCameraStartPositionForTeam` 비활성화 · 유닛 스폰 실패 로그 진단 필드 추가. **`system` 은 폴더가 아니라 기능 기준**이라는 해석을 `LogRules.md` **1.4 본문**에 명문화(규칙 번호 신설 없음). **⚠️ 잔여 — 과대 표기 금지:** **실기 동작 테스트 미수행**(특히 구독 수정은 간헐 버그라 확인 불가 — "안 났으니 고쳐졌다"로 결론 내리지 않는다) · **`SetCameraStartPositionForTeam` 최종 삭제 미완**([6] 통과 후) · **`LobbyUI` 가 현재 쓰이지 않을 가능성**(확인 없이 판단하지 않고 10건 전부 이관) · `_services` 재경기 인스턴스 위험(구조 공통, 이번 변경과 무관). 상세: `LogRules.md` **1.13**, task `_Tasks/2026-08-17/17_19_remaining-layers-log-migration/` · `_Tasks/2026-08-18/04_02_upgrade-subscription-fix/`.

**구현 완료 (2026-08-17) — 컴파일 통과 · ✅ 실기 검증 PASS:** **로그 체계(`GameLog`) 전환 — 네트워크·인증 계층 정리 + 씬 무관 로그 수집까지 완료.** **[씬 무관 로그 수집 — 커밋 `a253232e`]** 기록을 켜는 배선이 `GameBootstrapper`(= `Game.unity` 전용)에만 있어 **로그인·로비 구간 73건이 파일에 남지 않던** 문제를 해소했다. 신규 정적 클래스 **`Infrastructure/Debug/LogSessionOwner.cs`** 가 sink·초기화 플래그·훅 상태를 **한 곳의 `static`** 으로 소유하고, **여는 쪽 둘 / 닫는 쪽 하나** — 두 부트스트래퍼가 `Awake` 첫 줄에서 `EnsureInitialized()`(멱등)를 부르고 종료는 `Application.quitting` 1회다. **전역 미처리 예외 훅도 함께 이관**했으며, 그 결과 **로그 라인이 `[Runtime/GameBootstrapper]` → `[Runtime/LogSessionOwner]` 로 바뀐다(로그를 grep 하는 쪽에 영향)**. **[✅ 실기 PASS]** 근거 로그 `_Logs/_editor/2026-08-17/RuntimeLog.txt`(**199줄**) — `[Auth/...]` **7건** 기록(목적 달성) · 일 단위 이어쓰기 · 마스킹(`Email=` **0건**) · **정리 메뉴 실기도 PASS**(날짜 형식 아닌 폴더 제외 · 오늘 폴더 보존 · **`_Logs/` 직속 날짜 폴더 17개 무손상**). **[선행 작업]** 8파일 **205건** 이관(`개발` 120 / `운영` 85) + 미조치 4항목(커밋 `4e027e68` · `675203ae`). 8파일 **205건** 이관(`개발` 120 / `운영` 85)에 이어 남아 있던 **미조치 4항목**을 구현했다(커밋 `4e027e68` · `675203ae`) — ① 민감 데이터 마스킹 **15곳**(UID·PlayerId 해시 / 이메일 출력 제거) ② 에디터 로그 파일명 **`RuntimeLog.txt` 단일화**(역할은 `Role=` 로그 한 줄로) ③ `RuntimeLogger` 헤더 규정 준수(`BeginSession(folderPath, purpose)`) ④ **수동 정리 메뉴** `Hexiege > Logcat > 3. 오래된 에디터 로그 정리`. 감사가 놓쳤던 `LobbyManager` **`HostId` 평문 출력 1건**도 함께 마스킹했다. **⚠️ 잔여 — 과대 표기 금지:** **후속 커밋 `73574a23`(`Role` 값 표기 통일)은 컴파일 확인을 받지 않았다**(`a253232e` 까지만 통과 확인) · **`Lobby.unity` 직접 진입 미커버**(부트스트래퍼가 없어 sink 0개 — **사용자가 "항상 Login 을 거치므로 커버 불필요"로 결정**한 범위 밖 항목) · **나머지 계층 이관 미착수**(잔존 **234건** — 별도 task) · `.meta` 취급 규정 명문화 보류. 상세: `LogRules.md` **1.8**·**1.9**·**1.10**·**1.13**, task `_Tasks/2026-08-13/07_13_network-auth-log-cleanup/` · `_Tasks/2026-08-17/11_07_scene-independent-logging/`.

**구현 완료 (2026-08-12) — 에디터 싱글플레이 실기 검증 완료 · 멀티 미검증:** **MistShrine(HealShrine) 물안개 힐 시스템 구현 완료.** 2026-08-10 기획 확정 후 코드·프리팹·씬 배선을 구현하고 **에디터 싱글플레이 실기 + 런타임 로그 실측**으로 검증했다(범위 원 경계 = 실제 회복 판정 일치 / 범위 이탈 시 즉시 끊김 / 회복량 매 틱 +10 / 중첩 해소 1회 적용 + 동률 시 Id 작은 쪽). **⚠️ 과대 표기 금지 — 미완:** **멀티플레이 실기 미검증**(HP 동기화·클라 표시 등 멀티 고유 경로는 실행된 적 없음) · **물안개 지속 VFX 미제작** · **사용 버튼 아이콘 미제작** · **밸런싱 수치 미확정**(현재 값은 임시). **구현 중 발견·수정한 규칙 위반 버그:** 겹친 물안개 2개가 같은 대상을 각각 회복시켜 **초당 회복량 2배**가 되던 문제 — 물안개별 틱 위상이 달라 중첩 해소 코드가 **돌 기회 자체가 없는 죽은 코드**였다. **위상 정렬 + 소유권 판정 분리**로 수정(커밋 `be17148`), 불변식을 **규칙 8-1에 보강**. 구현 task: `_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`.
**기획 확정 이력 (2026-08-10):** MistShrine(`BuildingType.HealShrine` = 6)은 공격하지 않는 **아군 힐 전용 건물**로, 시전하면 **건물 중심 고정 원형 범위**(조준 없음)에 물안개가 깔려 지속 동안 **아군 유닛 + 아군 건물**(시전 건물 자신·Castle 포함)을 **1초 discrete 틱**으로 회복한다. 최대 체력 대상은 회복 없음, **범위 이탈 시 즉시 끊기는 아우라**, **물안개 지속시간 < 쿨다운**(다운타임 존재), 시전 비용 없음, 시전 건물 파괴 시 물안개 즉시 제거, **물안개끼리는 중첩 금지**(더 가까운 건물 우선 · 거리 동률이면 건물 Id가 작은 쪽), **연구소 자연회복과는 별개 효과로 중첩 적용**. UI는 `BuildingPanelBase` 상속 전용 패널(탭=수동 시전 / 롱프레스=자동 모드 토글, **자동 기본 OFF**, `SkillCooldownOverlay` 재사용, 범위 표시는 아군·패널 열린 동안만). 로직은 `SkillActivationUseCase` 재사용 불가로 **전용 UseCase 신설**(쿨다운 패턴만 차용 + 클라 로컬 미러). **함께 정정한 문서 오류:** GDD/TDD가 Transcendence 방어 타워를 MistShrine으로 적어 왔으나 **정확히는 VineTower**(`AutoTower` = 2)이며 MistShrine은 별개 건물이다. **수치(회복량·물안개 지속시간·쿨다운·범위 반경·회복 텍스트 표시 주기)는 전부 밸런싱 미확정** — 아래 C-1 표 참조. 상세: [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템**(규칙 1~27), `PROJECT_STATUS.md` "완료된 시스템 > MistShrine 물안개 힐 시스템", task `_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/`.
**버그 수정 (2026-08-08):** **랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정 · 실기 테스트 PASS**(커밋 `9a19cd5`). 랠리포인트 버튼 탭 시 팝업만 숨겨지고 공유 BlockingOverlay(탭=`Close`)가 잔존해 맵 탭을 가로채면서 집결지 지정이 취소된 것처럼 보이던 버그. `ProductionPanelUI.cs` **1파일 2줄 순수 추가** — ① `OnRallyPointClick()`에 `HideBlockingOverlay()`(스킬 패널 조준 진입과 동일 패턴) ② `Close()`를 거치지 않는 `CompleteRallyPointSetting()`에 참조 카운터 반납(②를 빠뜨리면 다음 팝업 오버레이가 어긋남 — 기존엔 이 버그가 대신 `Close()`를 태워 우연히 상쇄). 랠리포인트 규칙 2의 "설정 직후 3초 표시"도 정상화. 규칙 근거 `GameSystemRules_UI.md` 공통 UI 규칙 5 — **규칙 문서 변경 없음**(코드가 기존 규칙을 다시 준수하도록 맞춘 수정). task `_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/`.
**구현 완료 (2026-08-08):** **건물 파괴 시 열린 패널/조준 UI 원복** **구현 완료 · 실기 테스트 PASS**(커밋 `8c7fa01`). 건물 패널 4종(생산/건물액션/스킬/연구)이 열려 있거나 스킬 조준 중일 때 그 건물이 파괴되면 유령 패널·조준 UI가 남던 갭 해소. 공통 베이스 `BuildingPanelBase`가 `GameEvents.OnBuildingDied` 구독 → 파괴 건물이 현재 표시/조준 중인 건물(`_currentBuilding.Id` 매칭)이면 `Close()` → 각 패널 `OnBeforeClose`로 조준 취소·랠리 마커 숨김 자동 연계. **코드 변경 `BuildingPanelBase.cs` 1파일**(자식 4개 패널 무변경). 멀티=`NetworkCombatController.HandleBuildingDied` 클라 재발행으로 커버. **교훈: 자식(`ResearchPanelUI`)이 자체 `OnDestroy`를 선언하면 베이스 `OnDestroy` 은닉(hide)으로 해제 누락 → `.AddTo(this)`(UniRx)로 컴포넌트 수명에 묶음.** task `_Tasks/2026-08-08/07_40_building-death-ui-restore/`.
**구현 완료 (2026-08-05):** 스킬 시스템 **타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2** **구현 완료 · 실기+멀티(클라) 테스트 PASS**. 스킬 프레임워크의 마지막 메커니즘 타입 — 버프·디버프·제어(둔화·빙결)·회복을 하나의 상태변경 시스템으로 표현. Domain `Status/{StatusEffectKind,StatusEffect,UnitStatusState}` + Application `Services/StatusEffectSystem`(서버 권위 부여/틱) + `Skill/GlobalStatusChangeExecutor`(조준 없음 전역 즉시) 신설. 유효 스탯 접근자에 상태 배율 곱연산 합성 + 공격 게이트 `CanAttack`(무상태면 회귀 안전). 빙결=`Animator.speed=0` 애니 정지·클라 동기화, 둔화=이동 코루틴 매 프레임 배율 재조회로 라이브, 회복=HoT 재사용, 멀티=`StatusAppliedClientRpc`. **정리:** 진단 로그 코드 제거(LogRules 준수, `IRuntimeLogSink`/`RuntimeLoggerSink` 삭제)·좌표화 주석 비활성 코드 3곳 삭제. **미완:** ~~건물 파괴 시 UI 원복~~(→2026-08-08 완료·위 참조)·구체 스킬 목록/수치/아이콘(기획). 상세: `GameSystemRules/GameSystemRules_Skills.md`, `_Tasks/2026-07-28/12_14_skill-building-system-design/`.
**구현 완료 (2026-08-04):** 스킬 건물 시스템 Phase 1(타입 A 즉발 범위 피해 · 타입 B 장판 DoT + 프레임워크 골격 + 3×3 패널 UI/쿨다운 오버레이 + 모바일 탭 조준 + 조준 지점 연속 좌표화 + 조준원 지면 데칼 렌더링 + 취소 버그·쿨다운 토스트) **구현 완료 · 실기기 테스트 PASS**. 조준 중심 타일 스냅(HexCoord) → 연속 도메인 월드 Vector3 좌표화(**착탄 반경 판정은 원래 연속 원이라 무변경, 중심만 연속화** + 서버 재검증 HasTile → 맵 경계 안 점, `HexMetrics` 경계 헬퍼 신규·최외곽 타일 바깥선 엄밀 clamp). 조준원 coplanar z-fighting → 신규 셰이더 `SkillAimOverlay.shader`(ZTest LEqual + Offset -1,-1 + ZWrite Off) 지면 데칼. 실기 취소 버그(손 뗀 프레임 합성 마우스 좌표가 캐시 폴백 가로챔) → release 프레임 캐시 좌표만 판정으로 근본 수정. 쿨다운 중 탭 무시 → `ToastKey.SkillOnCooldown` 안내. **미완:** 타입 C(전역 상태변경) Phase 2·건물 파괴 시 UI 원복·구체 스킬 기획. 상세: `GameSystemRules/GameSystemRules_Skills.md`, `_Tasks/2026-07-28/12_14_skill-building-system-design/`·`_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/`.
**구현 완료 (2026-07-31):** 연구소 유닛 강화 시스템(공/방/속 + 초월 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI **구현 완료 · 멀티플레이 실기 PASS**. ×10은 config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨). 후속 보류: UI 레이아웃 다듬기·매트릭스 헤더 아이콘·AI 연구 사용 실기·MistShrine 힐·싱글 자연회복 실기. 상세: `GameSystemRules/GameSystemRules_Upgrade.md`, `StatsReference.md`, `_Tasks/2026-07-22/10_08_unit-upgrade-system/`.
**현재 단계:** MistShrine(HealShrine) 물안개 힐 **구현 완료 · 에디터 싱글플레이 실기 검증 완료 / 멀티 미검증**(2026-08-12 — 위 항목 참조. 전용 UseCase·전용 패널 UI·아군 수집 헬퍼·건물 회복 경로 전부 구현됨. **잔여: 멀티 실기 검증 · 물안개 VFX 제작 · 사용 버튼 아이콘 · 밸런싱 수치 확정**). 직전 스킬 건물 시스템 Phase 1(타입 A·B + 프레임워크 + 3×3 패널 UI/쿨다운 오버레이 + 모바일 탭 조준 + 조준 지점 연속 좌표화 + 조준원 지면 데칼 + 취소 버그·쿨다운 토스트) 구현·실기기 테스트 PASS(2026-08-04)에 이어 타입 C(전역 상태변경) Phase 2도 구현·실기+멀티(클라) PASS(2026-08-05)로 스킬 메커니즘 3종(A/B/C) 전부 완료. **직전 건물 파괴 시 패널/조준 UI 원복 구현·실기 PASS(2026-08-08, `BuildingPanelBase` OnBuildingDied 구독→현재 건물 매칭 시 Close, 4개 건물 패널 공통 커버).** 스킬 관련 다음 우선순위: 구체 스킬 목록·수치·아이콘(기획, ScriptableObject 확정 — 현재는 플레이스홀더 5슬롯 테스트용). 직전 QuakeSpirit(대지의 정령) 착탄형 즉발 범위 딜러 구현 완료(2026-07-20, QA PASS·**멀티플레이 실기 로그 검증 완료**) — 마지막 남은 특수 유닛으로, **특수 유닛 5종(BattleAxe/TorrentSpirit/BloomFairy/MushroomBomber/QuakeSpirit) 전량 + InfernoSpirit DoT까지 전부 완료**. 착탄형 **즉발** AoE(DoT 아님): 주 타깃 1마리 직접 100%(공격력 200, 건물이면 100% 공성) + 착탄 월드 원형 반경(`_quakeRadius` 기본 1.0=인접 1칸) 내 다른 적 유닛·**적 건물** 50%(올림 `CeilToInt(200×0.5)`=100, 주 타깃 제외). ⚠️ MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함. 아군 무피해, 서버 권위. 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`)+레지스트리 1줄, MushroomBomber 원형 반경 헬퍼(`internal static` 공용화)+건물 순회 신설+유닛/건물 hit-set 분리(규칙 29), `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5). 멀티 로그로 전 항목 정상 검증(로그 `_Logs/2026-07-20/11_00_quakespirit-aoe-verify/`). 규칙 43. 직전 InfernoSpirit(지옥불 정령) 단일 대상 DoT 구현 완료(2026-07-20) — 이미 완성된 원거리 유닛에 DoT 특수만 추가(주 타깃 1마리 직접 250 + 그 1마리에게만 DoT 50/초×3초, AoE 아님). 규칙 40 초 단위 틱을 단일 대상 재사용, 별도 진입점(`ApplyInfernoDot` 50/3) 분리. 규칙 41~42. **알려진 이슈(보류, 별도 task 분리)**: ① QuakeSpirit 타격 애니↔데미지 텍스트 타이밍 어긋남(Attack 클립 `OnAttackHit` 미주입+placeholder hitFrameTimes → 스플래시 텍스트 타임아웃 7.5s 지연, 피해·동기화 자체는 정상), ② 멀티 원거리 공격 facing 버그(에셋 아닌 공유 회전 로직 추정). 후속(Phase F): 기기 QA로 3D 텍스처 품질 확인, 피격 VFX 프리셋 연결, QuakeSpirit `OnAttackHit` 클립 이벤트 주입(타이밍 이슈 해소), 멀티 원거리 facing 버그 수정, Firebase/EDM 저장소 방침 정리, AI 반응 시스템·시나리오 정밀 검증 + 잔여 신규 유닛 프리팹 실기 테스트. **병행 관찰 항목 — 매치메이킹 404(호스트 결정 단계) 수정(A방식, 2026-07-17, 브랜치 `claude/matchmaker-404-error-pi9qdn` 커밋 `a3dbc73`)**: 랜덤 매칭 후 호스트 결정을 매치 결과 조회(P2P 클라 호출 시 404) → Lobby CreateOrJoin(matchId=lobbyId) 원자 선점으로 전환. 초기 매칭 실기에서 404 없이 정상 연결 확인했으나 간헐(intermittent) 버그라 지속 테스트 중(확정 PASS 아님) — 비활성화(주석)한 레거시 코드는 지속 테스트 확정 후 삭제 예정. 상세는 우선순위 표 및 Phase A-2 참조. **※ 이 문단의 전투 수치(직접 피해·DoT 틱값)는 2026-08-10에 ×10 스케일 개편(2026-07-31) 기준으로 정정했다 — 비율·반경·쿨다운은 ×10 대상이 아니라 그대로이며, 단일 진실 소스는 [StatsReference.md](StatsReference.md)다(우선순위 표 하단 ※ 참조).**
**작업 이력:** [WORK_HISTORY.md](WORK_HISTORY.md) 참조

---

## 우선순위 요약

| 우선순위 | 작업 | 카테고리 | 예상 규모 |
|---------|------|---------|---------|
| ✅ 완료 (2026-08-17, **실기 PASS**) | **로그 체계(`GameLog`) 전환 — 네트워크·인증 계층** — 8파일 **205건** 이관(`개발` 120 / `운영` 85) + 마스킹 **15곳**(UID·PlayerId 해시 / 이메일 출력 제거) + 에디터 로그 파일명 **`RuntimeLog.txt` 단일화**(역할은 `Role=` 로그 라인) + `RuntimeLogger` 헤더 규정 준수 + **수동 정리 메뉴**(`Hexiege > Logcat > 3. 오래된 에디터 로그 정리`) + 감사 누락 `HostId` 평문 출력 1건 마스킹. 커밋 `668e0aeb` 계열 · `4e027e68` · `675203ae`. **실기 검증 전 항목 PASS** — 근거 로그 `_Logs/_editor/2026-08-17/RuntimeLog.txt`(199줄) | 코드 정리/인프라 | 대 |
| ✅ 완료 (2026-08-17, **실기 PASS**) | **로그 체계 전환 — 씬 무관 로그 수집** (커밋 `a253232e`) — 신규 `Infrastructure/Debug/LogSessionOwner.cs` 가 sink·초기화 플래그·전역 예외 훅 상태를 **한 곳의 `static`** 으로 소유. **여는 쪽 둘**(두 부트스트래퍼가 `Awake` 첫 줄에서 `EnsureInitialized()`, 멱등) **/ 닫는 쪽 하나**(`Application.quitting`). 로그인·로비 구간 **73건**이 파일에 닿게 되었다(실기 확인 `[Auth/...]` **7건**). **⚠️ 부수 영향: 미처리 예외 로그가 `[Runtime/GameBootstrapper]` → `[Runtime/LogSessionOwner]` 로 바뀐다(grep 하는 쪽에 영향).** **⚠️ 후속 커밋 `73574a23`(`Role` 값 표기 통일)은 컴파일 확인 미수령** · **`Lobby.unity` 직접 진입은 사용자 결정으로 미커버**. task `_Tasks/2026-08-17/11_07_scene-independent-logging/` | 코드 정리/인프라 | 중 |
| ✅ 완료 (2026-08-18, **컴파일 통과 · 실기 미검증**) | **로그 체계 전환 — 나머지 계층 이관** — ~~잔존 234건 미착수~~ → **이관 완료.** 이번 **181건**(1-A 65 / 1-B 23 / 2 50 / 3·4 43 + `system` 교정 16)으로 **누적 386건**. `LogEvent` **36개**(배치 2 에서 4개 신설), `Unknown` **0건**. **최종 잔존 46건** = `GameLog.cs` 9(sink 폴백 — `LogRules.md` 1.8) + `ILogSink.cs` 2 + `Infrastructure/Debug/` 19 + **`Assets/Editor/` 16(미적용 — sink 가 없어 이관 효과 0. 규칙 예외 아님)**. `NetworkCombatController` 의 `IsServer` 중복 1건은 **1.14 금지 9** 에 따라 비활성화. **⚠️ 실기 동작 테스트 미수행** | 코드 정리 | 대 |
| ✅ 완료 (2026-08-19, **A/C 실기 확인 · B 미검증**) | **전역 로그 훅의 `Error` 수집 확대 + `NetworkCombatController` RPC 가드 + 연구 흐름 로그** (커밋 `cc864054`) — 훅이 `Exception`+`Error` 수집, 되먹임 방어 **3겹 → 4겹**(`RuntimeLogger.IsEmittingToConsole`), `condition` 1초 스로틀 + `Suppressed=n`(상한 32), `LogEvent` **36 → 37**(`UnhandledEngineError`), `Update` 가드에 `IsSpawned` 추가, 연구 성공 경로 로그 4줄, `Pos=` 구분자 위반 수정. **A ✅**(Shutdown 후 `StopCombatClientRpc` 0건) · **C ✅**(착수 6 ↔ 완료 6 1:1) · **⚠️ B 미검증** | 로그/QA | 중 |
| 🟡 중간 (**여전히 미검증** — 2026-08-19 (2차) 갱신) | **전역 훅의 엔진 오류 수집(B) 실기 확인** — 2026-08-19 회차에 엔진 오류가 **나지 않아** `[ERROR]`·`UnhandledEngineError`·`Suppressed=` 가 전부 0건이었고, **커밋 `55d24d83` 검증에서 3경기를 더 돌렸는데도 여전히 0건**이다(1408줄 기준). **훅이 동작한다는 증거가 아직 없다.** **⚠️ 역설이지만 기록해 둔다 — B 로 잡으려던 대상이 바로 그 RPC 에러였고, `55d24d83` 의 가드 수정이 그 에러를 없앴다. 즉 수정이 잘 돼서 검증 기회가 사라졌다.** 이제 *"우리가 만들지 않은 엔진 오류"* 가 우연히 나는 회차를 기다려야 한다. 다음에 엔진 오류가 나는 회차에서 로그 파일에 `Event=UnhandledEngineError` 줄이 남는지, `Suppressed=` 가 함께 찍히는지 확인한다 | QA | 소 |
| ✅ 완료 (2026-08-19, **실기 PASS · 재경기 2회 포함**) | **`NetworkCombatController` 의 나머지 2건** (커밋 `55d24d83`) — ~~① `OnUnitDied` 가드 구멍 ② 게임 종료 구독 0건(종료 후 약 3초간 전투 루프 지속)~~ → **둘 다 해소.** ①은 전수 확인 결과 **`OnUnitDied` 하나가 아니라 6곳**이었다(`OnBuildingDied` 는 **게임을 끝내는 바로 그 경로**이고, Walk·HealCast·FreezeChanged 는 공통 길목 `SetUnitAnimState` 한 곳으로 5개 호출 지점을 덮었다). ②는 `OnGameEnd` **서버 전용 구독 + `_combatStopped` 플래그 + `StopAllCoroutines()`**(구독 해제 방식은 디스패치 중 구독자 변경 위험으로 기각). **최우선 위험이던 플래그 리셋**은 `OnNetworkSpawn`·`OnNetworkDespawn` 양쪽에 두었고 **재경기 2회 연속 통과**로 확인됐다. **⚠️ 잔여 단서:** `IsSpawned` 전이 시점 미확정(27ms 창을 완전히 닫는다고 단정하지 않음 — **🔴 2026-08-20 정정: 이 구간의 실측은 27ms 단일값이 아니라 4회 기준 25/27/41/6 ms, 즉 6~41ms 다**) · Walk 계열 3건은 「예방」 · `NetworkUnit.SetAnimState` 미수정 · ~~**다른 네트워크 컨트롤러 미점검**~~ → **2026-08-20 전수 점검 완료 · 8곳 보강**(커밋 `bcf45ec1`, 위 완료 행 참조) | 코드 정리 | 중 |
| ✅ 완료 (2026-08-20, **⚠️ 코드 적용 완료 · 실기 미검증**) | **다른 네트워크 컨트롤러의 동일 가드 구멍 전수 점검** (커밋 `bcf45ec1`) — ~~`NetworkCombatController` 에서 6곳이 나왔으므로 다른 파일에도 같은 형태가 있을 수 있으나 열지 않아 「있다/없다」를 말할 수 없다~~ → **열었고, 8곳을 보강했다.** `Infrastructure/Network/` **`.cs` 21개 중 `.Subscribe(` 가 있는 파일은 6개**(`NetworkCombatController` 7 · `NetworkGameEndController` 4 · `NetworkProductionController` 3 · `NetworkHealthSync` 2 · `NetworkTileSync` 1 · `NetworkResourceSync` 1)이고, 직전 커밋이 처리한 `NetworkCombatController` 를 뺀 **5파일 8곳**에 `!IsSpawned \|\| !IsServer` 를 넣었다(2곳 신설 · 6곳 대체). **부호가 반대인 `if (IsServer) return;` 10곳은 무변경**(수신부의 중복 처리 방지 — 잘못 고치면 클라 타일 색이 죽는다). **⚠️ 잔여:** **실기 재현 0회이자 실기 미검증**(「실기 PASS」 아님) · 위험 구간 실측 **6~41ms**(27ms 단일값 정정) · **`NetworkBuildingController`·`NetworkUpgradeController` 는 `.Subscribe(` 0건이라 이번 대상에서 빠졌을 뿐, 「다른 형태의 구멍이 없다」는 뜻이 아니다** | 코드 정리 | 중 |
| 🟡 중간 (**미검증** — 2026-08-20 등록) | **가드 전수 보강 8곳의 실기 확인** — 커밋 `bcf45ec1`. 이 8곳은 **오류가 0회**라 *"오류가 사라졌다"* 는 확인이 성립하지 않는다. 봐야 할 것은 **「멀쩡하던 것이 망가지지 않았는가」** 다 — 호스트 1 + 클라이언트 1 로 한 경기를 끝까지 돌려 **골드 · 타일 색 · 체력 · 생산 진행 · 큐 · 승패**가 **양쪽 화면에서** 정상인지 확인하고, 종료 후 [로비로] 경로에서 콘솔 오류 0건을 함께 본다(상세 절차: Plan §10-2) | 검증 | 소 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`ProductionTicker` 에 종료 가드 추가** — `Presentation/Production/ProductionTicker.cs` **238행 `Update()`** 에 종료 가드가 없다. 246행의 유일한 분기는 *"멀티 클라이언트면 스킵"* 뿐이라, 위험 구간에서 **생산 틱(260행)과 수입 틱(265행)이 정상 속도로 돈다.** **길목으로는 이번 8곳보다 근본적**이지만 `Presentation` 레이어이고 틱 전체를 멈추는 것은 **동작 변경**이라 별도 설계 판단이 필요하다 | 코드 정리 | 중 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`NetworkGameEndController` 재경기 3종(`_localRematch*`)의 가드** — `ServerRpc` 3종(`RequestRematchServerRpc`·`AcceptRematchServerRpc`·`DeclineRematchServerRpc`)이고 **`IsServer` 블록 밖에서 구독**되어 클라이언트에서도 살아 있다. 따라서 필요한 가드는 **`!IsSpawned` 만**으로 이번 8곳과 **형태가 다르다.** 위험 구간(수십 ms) 안에 사용자가 재경기 버튼을 눌러야 발동하므로 실현 가능성도 사실상 없다 | 코드 정리 | 소 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`ServerRpc` 계열 전반의 종료 가드** — `NetworkProductionController` · `NetworkBuildingController` · `NetworkUpgradeController` · `NetworkSkillController` 등 대부분 진입부 가드가 없다. **호출 주체가 UI 입력**이라 *"이벤트 구독으로 자동 실행되는 경로"* 를 본 이번 조사와 **성격이 다르다.** 별건으로 다뤄야 한다 | 코드 정리 | 중 |
| ✅ 완료 (2026-08-20, **정적 확인 — 죽은 경로 제거**) | **에이전트 설정·공용 메모리의 윈도우 절대경로 정리** — ~~`.claude/MEMORY.md`(공용) **33 · 36 · 44~49행**이 `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/...` 를 가리킨다~~ → **해소.** 사용자가 *"이제 로컬에서 작업하는 경우가 없다"* 고 확정해 **범위를 3개 파일로 넓혀** 정리했다. **[① `.claude/MEMORY.md`(공용)]** 8행만 교체 — 33행 → `CLAUDE.md`, 36행 → `Assets/_Project/Docs/WORKFLOW.md`, 44~49행 에이전트 메모리 경로 표 6행 → `.claude/agent-memory/<에이전트명>/MEMORY.md`. **파일 전체 108행은 그대로**(`Read`→`Edit`, `Write` 금지 — 사고 재발 방지). **[② 🔴 새로 발견 — `.claude/settings.json` 8행 `SessionStart` 훅]** 훅 명령이 `cat "d:/Dmain/dev/Portfolio/Hexiege/Hexiege/CLAUDE.md"` 였다. **원격 리눅스 세션에 그 경로가 없으므로 이 훅은 매 세션 시작마다 계속 실패해 왔다** — 즉 **절대 규칙 파일을 자동으로 읽어 들이는 장치가 사실상 죽어 있었다.** 메모리 손실 사고 **원인 ②(죽은 윈도우 경로)와 같은 뿌리**이며, **2026-08-20 에 `.claude/agents/*.md` 6개만 고쳤을 때 이것이 남아 있었다는 점이 요점**이다(에이전트 정의만 훑고 설정 파일을 안 봤다). `cat CLAUDE.md`(리포지토리 루트 기준 상대경로)로 교체. 같은 파일에서 **윈도우 전용 죽은 허용 항목 4개**(`Bash("D:/dev/Node/npm.cmd" ...)` 3개 · `Read(//c/Users/rmsep/**)`) 삭제, `additionalDirectories` 를 `Assets/_Project/Docs/_Tasks` 상대경로로 교체하고 `C:\Users\rmsep` 삭제. **`hooks` 의 `PreToolUse` 항목과 나머지 permissions 는 무변경**, 수정 후 `python3 -c "import json;json.load(...)"` 로 **파싱 성공 확인**. **[③ `.claude/agent-memory/project-orchestrator/MEMORY.md` 239행]** *"위임 시 관련 파일 경로를 **절대 경로**(`d:/Dmain/...`)로 전달하라"* — **서브에이전트에게 죽은 윈도우 경로를 넘기라고 지시하는 문장**이라 원인 ②를 재생산하는 자리였다. 리포지토리 루트 기준 상대경로 + 경고로 교체(**나머지 324행 무변경**). **[검증]** `.claude/` 하위 `Dmain` 잔존은 **의도적 2건뿐**(③의 금지 예시 문장 · `document-manager/MEMORY.md` 의 사고 기록). `_Tasks/2026-07-16/12_10_.../Research.md`·`Plan.md` 의 `D:/Projects/Hexiege/...` 는 **당시 작업 상황을 적은 과거 기록이라 의도적으로 보존**. **⚠️ 과대 표기 금지:** 정적 확인(파일 내용 · JSON 파싱)까지이며 **`SessionStart` 훅이 실제로 성공하는지는 다음 세션 시작 때 확인해야 한다** | 문서/설정 | 소 |
| ✅ 완료 (2026-08-21, **정적 확인 — 규칙 신설**) | **에이전트 메모리 갱신 규칙 명문화** — `.claude/MEMORY.md` 상단에 **「🔴 에이전트 메모리 갱신 규칙」 6개 항목** 신설(① `Read`→`Edit`, **`Write` 금지** ② 비어 있다고 가정 금지 ③ 삭제는 **틀렸다고 확인했을 때만 + 이유 명기**, *"정리했다"* 는 사유가 못 됨 ④ **200행 이내 인덱스 유지**, 상세는 토픽으로 빼되 **반드시 링크** ⑤ **링크 없는 토픽 파일은 없는 것과 같음** ⑥ 토픽 이동 시 **폴더 총 행수 합이 줄지 않을 것** = 이동과 삭제를 구분하는 유일한 검증법). **손실 경로가 ①덮어쓰기 · ②200행 절삭 · ③고아 토픽으로 서로 달라 `Write` 금지 하나로는 ②③이 막히지 않기 때문**이다. 모든 에이전트가 작업 전 읽는 공용 파일이라 **그쪽을 규칙 단일 소스**로 삼았고, `AGENTS.md` 「에이전트 메모리」 절에는 **그리로 가는 참조 + ②③ 요약**을 덧붙였다(기존 인용 블록 무변경). 신설 작업 자체를 **`Read`→`Edit` 순수 삽입**으로 수행 — **108 → 121행**, 삽입 13행을 제외하면 원문 108행과 **완전 일치**함을 확인. `CLAUDE.md` 는 **사용자와 메인 세션의 소통 전용 문서로 확정**되어 손대지 않았다 | 문서/설정 | 소 |
| 🔴 높음 (**미착수** — 2026-08-21 등록) | **`Tools/check_docs.py` 에 메모리 검사 3종 추가** — 현재 검사기는 **문서 규칙 번호·링크만** 보고 `.claude/` 메모리는 전혀 보지 않는다. 그래서 2026-08-17 `-378행` 소실이 **3일간 아무에게도 안 걸렸다.** 추가할 것: ① **200행 초과 경고**(`.claude/agent-memory/*/MEMORY.md`) ② **고아 토픽 검출**(같은 폴더의 `*.md` 중 `MEMORY.md` 에서 링크되지 않은 것) ③ **행수 급감 감지**(에이전트 폴더 총 행수 합의 기준선을 파일로 두고 대비 감소 시 실패). **후속 4건 중 최우선** — 나머지 3건은 사람이 한 번 고치면 끝이지만 이건 **재발을 자동으로 막는다.** 검사기는 읽기 전용 원칙 유지 | 도구/문서 | 중 |
| ✅ 완료 (2026-08-21, **실측 확인 — 18/18 링크**) | **고아 토픽 16개 인덱스 재링크** — `game-programmer` 토픽 **18개 중 16개(1,839행)** 가 `MEMORY.md` 인덱스에서 링크가 끊겨 **에이전트가 존재를 모른다**(살아 있는 링크는 `logging.md` · `network-infra.md` 둘뿐). 대상: `work-history.md` 819 · `unit-building.md` 166 · `ui-system.md` 137 · `3d-transition.md` 104 · `network.md` 94 · `architecture.md` 91 · `hex-grid.md` 88 · `skill-aim-coordinate.md` 60 · `rendering-and-animation.md` 50 · `gameplay-systems.md` 47 · `unit-stats-and-combat.md` 46 · `camera-and-view.md` 45 · `attack-direction-refactor.md` 35 · `random-matching-bugfix.md` 34 · `combat-fixes.md` 17 · `network-todo.md` 6. **MEMORY.md 200행 제한 안에서** 한 줄 요약 + 링크 형태로 인덱싱해야 하므로 **요약 문구를 압축**하는 작업이 함께 필요하다. **`Read`→`Edit` 로만 진행**(신설 규칙 적용 대상) **[✅ 2026-08-21 해소 — 원문은 그대로 두고 덧붙인다: `MEMORY.md` 의 「토픽 파일」 절(당시 `logging.md`·`network-infra.md` 2개만 링크)을 **「토픽 파일 인덱스」로 대체**해 **18개 전부**를 한 줄 설명과 함께 링크했다. 원문 457행 260~283행의 인덱스 블록을 복원한 뒤, 그보다 나중에 생긴 `logging.md`(285행)·`skill-aim-coordinate.md`(60행) 2개를 추가해 **16 → 18개**로 맞췄다. 설명 문구는 더 나은 쪽을 살렸다(`network-infra.md` 는 현행의 `_combatStopped` 설명 + 원문의 Phase 1~8 범위를 병합). **실측 검증: 폴더의 `.md` 18개(`MEMORY.md` 제외) 전부가 `MEMORY.md` 본문에서 파일명으로 검색됨 — 누락 0건.** `MEMORY.md` 92 → **132행**(200행 제한 이내). 인덱스 상단에 *"여기서 링크가 빠진 토픽 파일은 존재하지 않는 것과 같다 / 토픽을 새로 만들면 반드시 이 목록에 한 줄을 추가한다"* 는 재발 방지 문구를 함께 넣었다]** | 문서/메모리 | 중 |
| ✅ 완료 (2026-08-21, **실측 확인 — 폴더 2,411 → 2,517행**) | **2026-08-17 소실분 대조 복구** — 커밋 `675203ae` 가 `game-programmer/MEMORY.md` 를 **실질 -378행** 덮어쓴 건이 **아직 복구되지 않았다**(08-20의 -18행은 `405538c7` 로 복원됨). 직전 원문 **457행**은 `git show 4e027e68:.claude/agent-memory/game-programmer/MEMORY.md` 로 확보 가능. **주의 — 전량 되돌리기가 아니다:** 지워진 것 중 `## CRITICAL — GIT 명령 절대 금지`(→ `CLAUDE.md` 규칙 5) · 레이어 제약(→ `.claude/MEMORY.md`) · **런타임 로그는 GameLog 경유**(→ `LogRules.md`) 처럼 **원본이 다른 문서에 살아 있는 항목은 제외**하고, **그 파일에만 있던 유일본**(NGO API 제약 · DontDestroyOnLoad 관례 · 「최근 작업」 약 20건 중 다른 곳에 기록이 없는 것)만 골라 되살린다. 그러지 않으면 같은 내용이 여러 문서에 갈라져 **다음 정리 때 또 지워진다.** **git 조회는 사용자 또는 메인 세션이 수행**(문서 에이전트는 규칙 5로 git 금지) **[✅ 2026-08-21 해소 — 원문은 그대로 두고 덧붙인다: 위 *"전량 되돌리기가 아니다"* 방침대로 **항목별 대조 판정**을 거쳤다. 457행 원문은 메인 세션이 scratchpad 파일로 전달했고(문서 에이전트는 git 미실행), 각 항목의 핵심 식별자를 `Assets/_Project/Docs/**` · `.claude/**` · `CLAUDE.md` · 토픽 18개에 **전수 grep** 했다. **복구한 유일본은 7건뿐**이며 전부 `MEMORY.md` 가 아니라 **성격에 맞는 토픽 파일**로 보냈다(200행 인덱스 원칙): ① 회전 테두리 머티리얼 `mat_ui_rotatingborder.mat` 의 `_Radius`·`_Inset` 런타임 인스턴싱 공유 함정 → `ui-system.md` ② 건물 패널 `Row0/Row1/Row2` 부모 구조 → `ui-system.md` ③ 에디터 셋업 배치 관례(`Assets/Editor/Setup/` · `Hexiege.EditorTools`) ④ 저장 반영 `SetDirty`+`MarkSceneDirty` ⑤ 멱등 헬퍼 `FindOrCreateChild` → ③~⑤ `architecture.md` ⑥ 범위 스프라이트 기준 크기 `sprite.bounds.size`(캐시 금지·긴 축 기준) → `rendering-and-animation.md` ⑦ MistShrine 셋업 메뉴 순서(`MistShrineSetup_{Config,Scene}.cs`) → `gameplay-systems.md`. **복구하지 않은 것과 그 원본 위치(grep 실측)**: `CRITICAL — GIT 명령 절대 금지` → `CLAUDE.md` 규칙 5 / 레이어·NGO·DontDestroyOnLoad 제약 → `.claude/MEMORY.md` 아키텍처 제약표 / `HexCoord.Distance` → `hex-grid.md`·`unit-stats-and-combat.md`·`GameSystemRules_Map.md` / `m_Speed` 첫 프레임 동결 → `rendering-and-animation.md`·`WORK_HISTORY.md` / `TeamAssigner` 삭제 → `architecture.md`·`network-infra.md` / `fillClockwise` · `_allSlotButtons` · `_showRequested` · `_baseDiameterOverride` · `FormerlySerializedAs` · `GetRadius()` → `WORK_HISTORY.md` / `_unitAutoIndicators` → `GameSystemRules_UI.md` / Profile·Ranking·Email verification → `.claude/MEMORY.md` 하단. **검증: 폴더 전체 행수 2,411 → 2,517행(+106) 으로 증가**(갱신 규칙 6 — 이동과 삭제를 구분하는 유일한 검증법). 판정을 확신하지 못해 일단 복구한 항목은 ②뿐이다(`_allSlotButtons` 9개·index 5=철거는 `WORK_HISTORY.md` 에 있으나 `Row0~2` 부모 구조만 유일본이라, 맥락 유지를 위해 블록째 복구하고 유일본 부분을 명시했다)]** | 문서/메모리 | 중 |
| 🟡 중간 (**미착수** — 2026-08-21 등록) | **200행 초과 3개 에이전트를 인덱스+토픽 구조로 분산** — **qa-tester 502행 · project-orchestrator 325행 · game-design-lead 254행.** 에이전트 정의가 *"lines after 200 will be truncated"* 라고 명시하므로 **초과분은 이미 에이전트에게 안 보이고 있다** — 파일에는 남아 있어 **커밋 이력에 흔적이 없고, 그래서 사고가 났다는 것조차 모른다.** `game-programmer` 가 쓰는 **인덱스 + 토픽 파일** 구조로 나눈다. **조건: 이동 전후로 각 에이전트 폴더의 총 행수 합이 줄지 않아야 한다**(이동과 삭제를 구분하는 유일한 검증법) + **옮긴 토픽은 반드시 인덱스에서 링크**(안 그러면 위 「고아 토픽」 문제를 새로 만든다). 위 `check_docs.py` 검사 3종을 **먼저 넣고 진행하면 자동 검증**된다 | 문서/메모리 | 중 |
| 🟢 낮음 (범위 밖 — 후속 후보) | **`NetworkUnit.SetAnimState` 의 `IsSpawned` 가드** — 현재 `if (!IsServer) return;` 만 있어 `IsSpawned` 를 보지 않는다. 애니메이션 상태 쓰기의 **더 근본적인 자리**지만 다른 파일이라 2026-08-19 작업에서는 `NetworkCombatController.SetUnitAnimState` 쪽에서 막았다. **디스폰 후 NetworkVariable 쓰기가 실제로 오류를 내는지 자체가 미확정**이라 착수 전 그 확인이 먼저다. **[2026-08-20 재확인]** 여전히 `NetworkUnit.cs` **173행 `if (!IsServer) return;`** 뿐이다(선언 170행). 이번 전수 보강에서도 **의도적으로 제외** — `.SetAnimState(` 호출부가 `NetworkCombatController.SetUnitAnimState` **한 곳뿐**이고 그 상위가 이미 `!IsSpawned` 로 막혀 있어 **중복**이기 때문이다 | 코드 정리 | 소 |
| 🟡 중간 (미완 · [6] 통과 후) | **`SetCameraStartPositionForTeam` 주석 처리분 최종 삭제** — 호출부 0곳으로 확인되어 `GameBootstrapper.Setup.cs` **537행**에 비활성화 블록으로 남아 있다(파일 헤더 목차 14행에 `— 제거 예정` 병기). WORKFLOW.md [4] 대로 **사용자 테스트 통과 후 · 문서 업데이트 전**에 삭제한다 | 코드 정리 | 소 |
| 🟡 중간 (**여전히 미검증** — 2026-08-19 갱신) | **`NetworkUpgradeController` 구독 누락 수정의 실기 확인** — 커밋 `da5eeaab`. **2026-08-19 회차에서 정상 경로는 로그로 확인되었으나**(착수 6 ↔ 완료 브로드캐스트 6 1:1, `Level=1`→`2`) **원래 버그의 원인인 스폰 레이스가 발생하지 않아**(`NetworkControllerSpawnedWithoutGameServices` **0건**) **지연 구독 경로 자체가 필요해진 상황이 없었다.** 즉 확인된 것은 **「버그가 재현되지 않았고 정상 경로는 확인됐다」** 까지다. **회선 상태에 좌우되는 간헐 버그**라 재현이 사실상 불가능하다. 로그에 *"스폰 시점에 IGameServices 를 얻지 못했다"* 가 **`NetworkUpgradeController` 이름으로** 찍힌 판에서 연구 완료가 정상일 때만 직접 증거가 된다. **"안 났으니 고쳐졌다"로 결론 내리지 않는다** | QA | 소 |
| 🟢 낮음 (미확인) | **`Presentation/UI/LobbyUI.cs` 가 현재 쓰이는지 확인** — 로비가 `LobbyRootView` 계열로 재구성됐는데 이 파일은 옛 구조 그대로다. 이관 작업에서는 **확인 없이 판단하지 않고 10건 전부 이관**했다(CLAUDE.md 규칙 10·12). 쓰이지 않는다면 죽은 코드 정리 대상 | 코드 정리 | 소 |
| ✅ 완료 (2026-08-17, **실기 PASS**) | **로그 정리 메뉴 첫 실행 확인** — ~~"한 번도 실행된 적이 없다"~~ → 실제로 실행해 확인했다. `_Logs/_editor/` 에 `2020-01-01`(날짜 형식)과 `2020.01.01`(점 구분)을 두고 실행: **`2020-01-01` 삭제 · `2020.01.01` 제외**(`TryParseExact("yyyy-MM-dd")` — 느슨한 `TryParse` 였다면 삭제됐을 케이스) · **오늘 폴더 보존** · 취소 경로 · **정리 범위 한정 — `_Logs/` 직속 날짜 폴더 17개 무손상**(영구 보존물 영역이라 지워지면 복구 불가) | QA | 소 |
| 🟢 낮음 (사용자 판단 대기) | **`_Logs/_editor/` 의 `.meta` 취급 규정 명문화** — `.gitignore` 는 이미 `LogRules.md` 1.10 규정에 부합한다. 남은 것은 `.meta` 를 커밋할지에 대한 **규정 공백**이다. 사용자가 커밋 `23a8da06` 에서 `.meta` 를 실제로 커밋했으나, **그 관찰만으로 규정이 확정된 것으로 적지 않는다**(CLAUDE.md 규칙 10) | 문서/규정 | 소 |
| 🟢 낮음 (범위 밖으로 미룸) | **`FileSink.EditorLogsRootRelativeToAssets` 접근 수준 조정** — 현재 `private` 이라 `LogcatCapture.cs` 가 같은 경로 문자열을 **복제**하고 「FileSink 와 동기화 필요」 경고 주석을 달아 두었다. `internal const` 로 올리면 복제를 없앨 수 있다 | 코드 정리 | 소 |
| 🔵 초기 정상·지속 관찰 중 | 매치메이킹 404(호스트 결정 단계) 수정 — 호스트 결정을 매치 결과 조회(P2P 클라 404) → Lobby CreateOrJoin 원자 선점(A방식)으로 전환 (2026-07-17, 커밋 `a3dbc73`). 초기 실기 정상, 간헐 버그라 지속 멀티 실기 검증 필요 | QA/버그 | 중 |
| ✅ 완료 | 코드 리팩토링 7개 그룹 전체 | 아키텍처 | 대 |
| ✅ 완료 | 코드 정리(클린업) Phase 1 — 히스토리성 주석/폐기 코드 제거 (약 30개 파일) | 코드 정리 | 소 |
| ✅ 완료 | 코드 구조 개선 Phase 2 — switch→Dictionary lookup table(BuildingTypeHelper) + HexMetrics 중복 setup 제거 (2026-06-25) | 코드 정리 | 중 |
| ✅ 완료 | GameBootstrapper.Setup.cs 하드코딩 배열 파생 — 환불 캐시의 stage1Buildings/nonProductionBuildings 하드코딩 배열을 BuildingTypeHelper 공개 API 파생으로 교체. 신규 건물 추가 시 `_buildingTable` 한 줄로 환불 캐시 자동 반영 (2026-06-25) | 코드 정리 | 소 |
| ✅ 완료 | IUnitFactory 인터페이스 도입 — IGameServices.GetUnitFactory() 반환 타입을 UnitFactory(Infrastructure) → IUnitFactory(Application)로 변경. Application → Infrastructure 역방향 의존 제거 (2026-06-26) | 아키텍처 | 소 |
| ✅ 완료 | 로그인 시스템 C# 구현 (Firebase Auth + GPGS) | 기능 | 중 |
| ✅ 완료 | 게임 화면 UI TC 62개 실기기 테스트 + END UI 버그 수정 | QA/버그 | 중 |
| ✅ 완료 | BuildingPlacementUI 씬 계층 재설계 (BP-001/BP-002 해결) | UI | 중 |
| ✅ 완료 | 패널 버튼 크기 불일치 수정 (PRD-001, BAP-001) — LayoutElement 균등화 | UI | 소 |
| 🔵 조건부 완료 | AI 시스템 — 코드 + Inspector 작업(AIConfig/Scenario 3종족 에셋 생성, DifficultySelectView GO 배치) 완료. 핵심 흐름(유닛 생산/건물 업그레이드) 실기 조건부 완료(2026-07-16, PASS·특별한 문제 미발견). 후속: 반응 시스템(R1~R3)·3종족 시나리오 무작위 동작 정밀 검증 | 기능 | 대 |
| 🔴 높음 | AI 시스템 — 반응 시스템(R1 유닛열세/R2 골드과잉/R3 채굴소 파괴) 트리거·동작 + 3종족(Human/Spirit/Transcendence) 시나리오 무작위 선택 동작 정밀 실기 검증 (핵심 흐름은 확인됨, 세부 정밀 검증만 잔여) | 기능 | 소 |
| 🔴 높음 | 신규 유닛 프리팹 실기 테스트 + 후속 작업 (Animation Event 부착, UnitFactory 등록, StatsReference 스탯 확정) | 기능 | 대 |
| 🔴 높음 | 게임 화면 UI 크기/레이아웃 수정 잔여 (HUD-007, SET-004, SET-007/END-001, MULTI-END-002 — 5항목) | UI | 소 |
| 🔴 높음 | FlatTop 11×21 무작위 대전 맵 구현 — 5종 생성 전략, exact 180° 대칭, 광산/초기 골드, 초기 골드 전용 테스트 모드(멀티 Host 권위), 건설 불가·차단 지형, 결정적 seed/검증/폴백, canonical chunk 전송, SameMap/NewMap, 경로 완전 차단 대응 (설계 확정·문서 동기화 2026-07-20) | 기능/맵 | 대 |
| ✅ 완료 (2026-08-04, 실기 PASS) | 스킬 건물 시스템 Phase 1 — 타입 A(즉발 범위 피해)·B(장판 DoT) 실행기 + 데이터 주도 프레임워크(SkillData/Executor/Registry/SkillActivationUseCase, 건물 글로벌 쿨다운) + 3×3 패널 UI/쿨다운 오버레이(12시 시계방향 소멸) + 모바일 탭 조준(2단계, 엣지 스크롤, 취소 X Lerp 확대) + 조준 지점 연속 좌표화(HexCoord→Vector3, **반경 판정 무변경·중심만 연속화**) + 맵 경계 point-in-bounds 재검증(`HexMetrics` 헬퍼 신규) + 조준원 지면 데칼 렌더링(`SkillAimOverlay.shader`) + 취소 버그 근본 수정 + 쿨다운 안내 토스트. 서버 권위(`NetworkSkillController`). 규칙 1~26. 미완: 타입 C·건물파괴 UI 원복·구체 스킬 기획 | 기능 | 대 |
| ✅ 완료 (2026-08-05, 실기+멀티 PASS) | 스킬 타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2 — 전역 즉시 발동(조준 없음), 상태효과 시스템(`StatusEffectSystem`, 서버 권위 부여/틱) + 실행기(`GlobalStatusChangeExecutor`) 신설, 유효 스탯 접근자에 상태 배율 곱연산 합성 + 공격 게이트 `CanAttack`(무상태 회귀 안전), 빙결 애니 정지(`Animator.speed=0`)·둔화 라이브·회복 HoT 재사용, 멀티 `StatusAppliedClientRpc` 동기화. 업그레이드 연동은 곱연산 합성으로 확정. 진단 로그 제거(LogRules 준수). 규칙 13 | 기능 | 대 |
| ✅ 완료 (2026-08-08, 실기 PASS) | 랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정 — 랠리포인트 버튼 탭 시 팝업만 숨겨지고 공유 BlockingOverlay(탭=`Close`)가 `blocksRaycasts=true`로 잔존해 맵 탭을 가로채면서 집결지 지정이 취소된 것처럼 보이던 버그. `ProductionPanelUI.cs` 1파일 2줄 순수 추가(① `OnRallyPointClick()` 오버레이 해제 ② `Close()` 미경유 `CompleteRallyPointSetting()` 참조 카운터 반납). 규칙 근거 `GameSystemRules_UI.md` 공통 UI 규칙 5(단일 소유·참조 카운터), 랠리포인트 규칙 2 "설정 직후 3초 표시" 정상화. 커밋 `9a19cd5` | QA/버그 | 소 |
| ✅ 완료 (2026-08-08, 실기 PASS) | 건물 파괴 시 패널/조준 UI 원복 — 스킬 포함 4개 건물 패널 공통 갭(파괴돼도 열린 패널·조준 락 잔존) 해소. `BuildingPanelBase`가 `OnBuildingDied` 구독→현재 건물(`_currentBuilding.Id`) 매칭 시 `Close()`(각 패널 `OnBeforeClose`로 조준 취소·랠리 마커 숨김 자동 연계). 코드 변경 `BuildingPanelBase.cs` 1파일, 구독 해제=`.AddTo(this)`(자식 `ResearchPanelUI` 자체 `OnDestroy` 은닉 회귀 회피). 멀티=`NetworkCombatController.HandleBuildingDied` 클라 재발행 커버. 커밋 `8c7fa01` | QA/버그 | 소 |
| ✅ 완료 (2026-08-12, 에디터 싱글 실기 PASS · **멀티 미검증**) | **MistShrine 물안개 힐 시스템 구현** — 전용 `MistShrineUseCase` · `BuildingPanelBase` 상속 전용 패널(탭=수동/롱프레스=자동, 자동 기본 OFF) · 범위 표시 원 · 아군 유닛/건물 수집 헬퍼 · 건물 회복 경로(`BuildingData.Heal` + 전용 동기화 RPC) · 파괴/철거 정리 경로 전부 구현. **로그 실측 검증**: 범위 원 경계 = 실제 회복 판정 일치(회복 최대 2.29 / 탈락 최소 3.12, 반경 3.00과 모순 0건) · 범위 이탈 시 즉시 끊김 · 회복량 매 틱 +10 · **중첩 해소 1회 적용 + 거리 동률 시 Id 작은 쪽**. **구현 중 규칙 13 위반 버그 발견·수정**(위상 불일치로 중첩 해소가 죽은 코드였음 → 위상 정렬 + 소유권 판정 분리, 커밋 `be17148`, 불변식을 규칙 8-1에 보강). **잔여(아래 별도 항목)**: 멀티 실기 검증 · 물안개 VFX · 버튼 아이콘 · 밸런싱 수치. 규칙 1~27: [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) | 기능 | 중 |
| 🟡 중간 (미착수) | **MistShrine 멀티플레이 실기 검증** — 이번 사이클 검증은 **에디터 싱글플레이로만** 이루어졌다. 범위 판정·중첩 해소·회복량 계산은 싱글·멀티가 같은 코드 경로를 공유하지만, **건물 HP 동기화(`SyncBuildingHealClientRpc`)·클라이언트 표시·RPC 팀 검증·쿨다운 로컬 미러·이중 틱 여부는 멀티에서만 도는 경로이며 한 번도 실행되지 않았다.** Host+Client 구성으로 확인 필요 | QA | 소 |
| 🟡 중간 (미착수) | **MistShrine 물안개 지속 VFX + 사용 버튼 아이콘 제작** — 현재 물안개는 **눈에 보이지 않고**(등록 VFX는 `vfx_mistshrine_destroy`/`vfx_mistshrine_upgrade`뿐 — 규칙 26), 사용 버튼은 **임시 텍스트 라벨**(UI 규칙 15). VFX 재생 시 사운드 규칙 15(VFX+SFX 쌍) 준수 | 에셋 | 중 |
| 🟡 중간 | 스킬 건물 구체 스킬 목록·수치 확정(기획) — 각 슬롯 1~5 스킬·쿨다운·반경·지속·피해·상태효과를 ScriptableObject 데이터로 확정. 개별 스킬 쿨다운 도입 여부·회복 스킬 지점 지정 전환 여부 포함 | 기획 | 중 |
| ✅ 완료 (2026-07-31, 멀티 실기 PASS) | 연구소 유닛 강화 시스템 + 전투 스탯 ×10 개편 + 연구 패널 UI — 공/방(신규)/속 3스탯 + 초월 자연회복, 종족별 그룹×스탯 트랙, (B) 팀 배율 실시간 소급 적용, 방어력 감쇄(K=120), 연구소 운영(복수 건설·트랙 잠금·파괴 100% 환불·서버 권위), 전투 수치 ×10(TTK 불변), 매트릭스 2-레이어 UI. 후속 보류: UI 레이아웃·헤더 아이콘·AI 연구·MistShrine 힐·싱글 자연회복 | 기능/기획 | 대 |
| 🔴 높음 | 전역 GameProtocolVersion/build 호환성 관리 — matchmaking 동일 버전 필터, custom lobby Relay 전 검사, NGO connection approval/rejection, reconnect 버전 검증, update-required UX | 네트워크/호환성 | 대 |
| ✅ 완료 | 전역 UI 시스템 (UIManager + SplashOverlay) | UI | 중 |
| ✅ 완료 | Firebase Console 설정 (Google 로그인) — SHA-1 정합 + Play Games 제공업체 활성화, 실기 로그인 성공 (2026-06-27) | 인프라 | 소 |
| 🔴 높음 | Login.unity 씬 로그인 UI 조립 | UI | 소 |
| 🔴 높음 | UGS OIDC 브릿지 활성화 — UGS Dashboard OIDC 제공자(`oidc-firebase`) 등록 (현재 `id provider not found`로 멀티플레이 제한) | 인프라 | 소 |
| 🟡 중간 | BuildFailed/EnqueueFailed UI 피드백 (멀티) | UI | 소 |
| 🟡 중간 | 게임 내 밸런싱 (골드/HP/생산시간) | 기획 | 중 |
| 🟡 중간 | 로비 UI 비주얼 폴리싱 (에셋 제작 완료 2026-05-30) | UI | 중 |
| 🟢 낮음 | 재접속 실제 구현 | 기능 | 중 |
| ✅ 완료 | 방어 타워(AutoTower) 공격 기능 | 기능 | 대 |
| ✅ 완료 | 사운드 시스템 — 코드 완료 + Inspector 작업 + 실기 버그 3종(BGM 겹침/볼륨 UI 규칙/SFX 진단) 수정 (2026-07-08) | 기능 | 중 |
| ✅ 완료 | 사운드 시스템 — 인게임/로비 볼륨·음소거·프로필 버튼 UI 로직 연결 (AudioManager 음소거 + 공용 VolumeControlBinder + 로비 설정 탭 배선 + 실기 버그 3건 수정, 2026-07-09 실기 PASS) | 기능/UI | 중 |
| ✅ 완료 | 전투 타격 타이밍 동기화 — 타격 프레임 클립 이벤트 단일 소스화 + 서버 Tick 정밀화 + 피격 표현 큐 + 타워 발사 VFX/원거리 트레이서 + 기존 버그 3건 수정 (2026-07-12 실기/로그 검증 PASS) | 기능 | 대 |
| ✅ 완료 | 이동/Walk 애니메이션 동기화 (2026-07-13) — 애니메이션 상태를 엣지 RPC → NetworkVariable 레벨 동기화 전환(스폰 레이스 유실 소멸) + Initialize 후 재적용 봉합 + 재경로 첫 스텝 역방향 보정. 규칙 U-22 등재 | QA/버그 | 중 |
| ✅ 완료 | 죽은 코드 제거 — `UnitView.StopMovement()` 미사용 메서드 삭제(호출 0건, 커밋 8840798) (2026-07-13) | 코드 정리 | 소 |
| ✅ 완료 | Animator 상태 의존 제거 — 전투 종료 후 Walk 재개 3곳의 `GetCurrentAnimatorStateInfo` 질의 → 로컬 추적 필드 기반 판별 리팩토링(겉보기 동작 불변, 커밋 97adaad) (2026-07-13) | 코드 정리 | 소 |
| ✅ 완료 | Firebase 인증 게이트 제거 — `#if HEXIEGE_ENABLE_FIREBASE_AUTH` 스텁이 로그인 무조건 실패시키던 버그 수정, 실제 Firebase 코드 무조건 컴파일 복원(커밋 4fe1cf0) (2026-07-13) | QA/버그 | 소 |
| ✅ 완료 | 이메일 인증 플로우 보정 — 실제 이메일 표시, 가입 취소 시 미인증 Firebase 계정 삭제, 앱 재실행 시 인증/닉네임 게이트 복귀, Lobby 게스트 우회 차단 (2026-07-18 실기 PASS) | QA/버그 | 중 |
| ✅ 완료 | 빌드 에셋 용량 최적화 — Android AAB 190.66 MB → 125.30 MB 절감(2026-07-15). 3D 건물/유닛 텍스처 Android max size 512 적용, 미사용 `_Old` 에셋/normal/roughness PNG 정리, FBX import 조정 | 플랫폼 | 중 |
| ✅ 완료 | 도끼병(BattleAxe) 휩쓸기형 AoE + 특수 공격 전략 핸들러 아키텍처 — 특수 유닛 5종 중 첫 구현. 월드 좌표 전방 부채꼴 판정 + `SpecialAttackConfig` 튜닝 SO + `ApplyDamageToVictim` 피해 수렴점 단일화 + AoE 연출 동시 방출 + BattleAxe 스탯/클립 이벤트 확정 (2026-07-17 실기 PASS) | 기능 | 중 |
| ✅ 완료 | TorrentSpirit 파도형 이동 AoE + 힐 서브시스템 — 특수 유닛 5종 중 2번째. special-only(`ReplacesPrimaryAttack`) 서버 권위 이동 파도(`TickWaves`, 월드 직사각형, 적 유닛·건물 피해/아군 힐 각 1회) + 힐 서브시스템(`UnitData.Heal`/`OnEntityHealed`/NetworkHealthSync 힐 동기화/치유 텍스트, BloomFairy 공용) + `VfxPoolItem` 다중 파티클 재생 수정 + 데이터 배선(스탯/EffectPreset/OnAttackHit). QA CONDITIONAL PASS(BUG-002 건물 공격 수정). 규칙 28~31. VFX 튜닝은 사용자 진행 (2026-07-17) | 기능 | 중 |
| ✅ 완료 | BloomFairy(꽃요정) 힐러 — 특수 유닛 5종 중 3번째. 힐러 전용 경로(적 공격 흐름·레지스트리 분리) + 부상 아군 탐색(팀 필터 반대·본인 포함) + HoT/DoT 공용 시간 지속 효과 시스템(서버 권위 diff 틱, 규칙 34) + 힐 유휴 감시 + 쿨다운 예외(발동 준비 1.0s 미포함 → 실제 힐 주기 4.0s, 프로젝트 유일) + HoT 힐 텍스트 완료 시 1회 표시(사망 시 생략). 규칙 32~37. 실기 완료 (2026-07-18) | 기능 | 중 |
| ✅ 완료 | MushroomBomber(버섯폭격기) 착탄형 범위 DoT — 특수 유닛 5종 중 4번째. 착탄 중심 월드 원형 반경(`BlastAttackBehavior`, arc 없는 도끼병 부채꼴, QuakeSpirit 재사용 헬퍼) 내 적 유닛 DoT + 주 타깃 직접 100(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성). DoT 초 단위 discrete 틱 모드 신설(규칙 34 확장 — 1초 간격·틱당 올림·총량 클램프·매초 데미지 텍스트). 식물 라인 생산 배선(SporePatch=MushroomBomber, FloralNursery=BloomFairy). 규칙 38~40. QA PASS·실기 완료 (2026-07-19) | 기능 | 중 |
| ✅ 완료 | InfernoSpirit(지옥불 정령) 단일 대상 DoT — 특수 유닛 5종 외 추가 DoT 유닛. 이미 완성된 원거리 유닛에 DoT 특수만 추가: 주 타깃 1마리 직접 250(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성) + 그 주 타깃 1마리에게만 DoT 50/초×3초(총 150, AoE 아님·반경 없음, 적 유닛만). 규칙 40 초 단위 틱을 단일 대상으로 재사용, 값은 별도 진입점(`ApplyInfernoDot` 50/3)으로 분리(MushroomBomber 20/3 회귀 없음). `SpecialAttackConfig` inferno DoT 필드 + GameBootstrapper 주입. 레지스트리 1줄. 규칙 41~42. 공격 방향(facing) 버그는 사용자 결정으로 보류(별도 작업). QA PASS·실기 완료 (2026-07-20) | 기능 | 소 |
| ✅ 완료 | QuakeSpirit(대지의 정령) 착탄형 즉발 AoE — **특수 유닛 5종 마지막(전량 완료)**. 착탄형 즉발(DoT 아님): 주 타깃 1마리 직접 100%(공격력 200, 건물이면 100% 공성) + 착탄 월드 원형 반경(`_quakeRadius` 1.0=인접 1칸) 내 다른 적 유닛·**적 건물** 50%(올림 `CeilToInt(200×0.5)`=100, 주 타깃 제외). ⚠️ MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함. 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`)+레지스트리 1줄, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용(유닛만 로직 무변경)+건물 순회 `CollectEnemyBuildingsInRadius` 신설+유닛/건물 hit-set 분리(규칙 29). `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5) 주입. 규칙 43. 타격 타이밍 어긋남(OnAttackHit 미주입)은 사용자 결정으로 별도 task 보류. **QA PASS·멀티플레이 실기 로그 검증 완료** (2026-07-20, 로그 `_Logs/2026-07-20/11_00_quakespirit-aoe-verify/`) | 기능 | 중 |
| 🟡 중간 | 피격 VFX 프리셋 연결 — `UnitEffectConfig.hitPreset` Inspector 배선(Human 6종 등 미연결) | 에셋 | 소 |
| 🟢 낮음 | QuakeSpirit Attack 클립 `OnAttackHit` 이벤트 주입 — 특수 공격 로직(규칙 43)은 구현·검증 완료됐으나 클립 `OnAttackHit` 미주입 + placeholder `hitFrameTimes`(1.0s)로 타격 애니↔데미지 텍스트 타이밍이 어긋남(스플래시 텍스트 타임아웃 7.5s 지연, 피해·동기화 자체는 정상). 인젝터로 실제 타격 프레임 주입 시 해소. 사용자 결정으로 별도 후속 task. (특수 공격 핸들러 확장 완료: BattleAxe·TorrentSpirit 2026-07-17, BloomFairy 2026-07-18, MushroomBomber 2026-07-19, InfernoSpirit·QuakeSpirit 2026-07-20) | 기능/에셋 | 소 |
| 🟡 중간 | 멀티플레이 원거리 공격 방향(facing) 버그 — 원거리 유닛이 공격 시 타겟을 정확히 안 바라봄. 진단상 에셋 아닌 멀티 원거리 facing 공유 회전 로직(서버 계산+NetworkTransform) 문제로 추정(근접 유닛 미노출). 수정 시 근접 유닛 회귀 주의. 상세: `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/Plan.md` (InfernoSpirit 작업에서 진단·보류) | QA/버그 | 소 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | Firebase 백엔드 (랭킹/IAP) | 기능 | 대 |

> **※ 2026-08-10 ×10 스케일 반영 정정:** 위 표의 **MushroomBomber · InfernoSpirit · QuakeSpirit** 행과 상단 **"현재 단계"** 문단에 남아 있던 ×10 개편(2026-07-31) **이전** 전투 수치(직접 피해·DoT 틱값)를 현재 값으로 정정했다. **비율·반경(100% / 50% / `×0.5` / 반경 1.0)과 쿨다운·사거리·이동속도·골드 비용은 ×10 대상이 아니므로 그대로다.** 날짜가 박힌 완료 이력(Phase D-1 방어 타워 완료, Phase F-4 클립 이벤트 주입 이력 등)은 **당시 기록이므로 원문을 보존**한다. 수치의 단일 진실 소스는 [StatsReference.md](StatsReference.md)다.

---

## Phase A — 네트워크 버그 수정

### ✅ A-1. BuildFailed/EnqueueFailed UI 피드백 (멀티플레이 분기) — 완료 (2026-05-24)
- **완료 내용**: `GameEvents.OnToastRequested` Subject 패턴 도입. NetworkBuildingController / NetworkProductionController RPC 핸들러에서 Subject 발행 → ToastUI 구독. Presentation이 Infrastructure를 직접 참조하지 않는 구조 완성.
- **파일**: `NetworkBuildingController.cs`, `NetworkProductionController.cs`, `GameEvents.cs`, `ToastKey.cs`

### 🔵 A-2. 매치메이킹 404 수정 — 호스트 결정 Lobby CreateOrJoin 전환 (A방식) — 초기 정상·지속 관찰 중 (2026-07-17)
- **증상**: 랜덤 매칭은 성사되나 **직후 호스트 결정 단계에서 HTTP 404** → 게임 연결 끊김.
- **원인**: `MatchmakerManager.DetermineIsHostAsync` 내부 `GetMatchmakingResultsAsync`가 전용 서버(Multiplay)용 서버 지향 API인데 P2P(Relay) 클라이언트가 호출 → 조회 대상 리소스 없어 404. 매칭 자체는 정상, 호스트 결정 단계만 실패.
- **해결(A방식)**: 호스트 결정을 매치 결과 조회 → **Lobby CreateOrJoin 원자 선점**으로 전환. 모든 플레이어가 같은 `matchId`를 `lobbyId`로 `CreateOrJoinLobbyAsync` 호출 → 없으면 생성=호스트 / 있으면 참가=클라. 서버 원자 처리로 정확히 한 명만 호스트.
- **변경 파일(3개, Infrastructure/Network)**: `LobbyManager.cs`(추가: `CreateOrJoinLobbyByMatchIdAsync`, `RefreshCurrentLobbyAsync`), `MatchmakerManager.cs`(비활성화 주석: `DetermineIsHostAsync`/`GetStableHash`), `NetworkGameManager.cs`(추가: `StartMatchmadeGameAsync`/`HostMatchmadeGameAsync`/`JoinMatchmadeGameAsync`, `StartMatchmakingAsync` 분기 교체, 구 클라 참가 경로 비활성화 주석). 클라 참가는 CreateOrJoin으로 일원화, RelayJoinCode 채워짐만 폴링 대기(최대 15회).
- **상태**: 초기 매칭 실기에서 404 없이 정상 연결 확인. 단 **간헐(intermittent) 버그라 지속 테스트 중** — 확정 PASS 아님. 비활성화(주석)한 레거시 코드(`DetermineIsHostAsync`/`GetStableHash`, 구 클라 참가 경로)와 미사용 `FindLobbyByMatchIdAsync`의 최종 삭제는 지속 테스트 확정 후.
- **잔여 리스크**: ① SDK 시그니처 에디터 컴파일 최종 확인 권장, ② "정확히 한 명만 호스트"·간헐 재현 지속 멀티 실기(Host+Client) 검증, ③ 클라 RelayJoinCode 대기 15초 타임아웃.
- **브랜치/커밋**: `claude/matchmaker-404-error-pi9qdn` / `a3dbc73`. task: `_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/`.

---

## Phase B — 네트워크 미완성 기능

### B-0. 전역 GameProtocolVersion/build 호환성 관리 🔴 높음 (2026-07-19 등록)

- **범위**: matchmaking same-version filter, custom lobby의 Relay 진입 전 호환성 검사, NGO connection approval/rejection, reconnect 시 버전 재검증, update-required UX.
- **경계**: FlatTop 무작위 맵 구현에 종속시키지 않고 모든 멀티플레이 진입·재접속 경로에 공통 적용한다.
- **맵 기능과의 경계**: 무작위 맵은 canonical binary 식별용 임시 `MapVersion(int, 초기값 1)`만 사용하며 unknown map format deserialize 차단과 map prep mismatch 실패만 담당한다. 이 값으로 앱/접속 호환성을 판정하지 않는다.

### B-1. 로비 UI 비주얼 폴리싱
- **현황**: MVVM 코드 완료 (2026-03-15). UI 에셋 제작 완료 (2026-05-30) — 아이콘 13종(탭/기능/로비버튼), 버튼 배경 2종(Primary/Secondary), 스피너 1종(HexOrb)
- **남은 작업**: 제작된 에셋을 로비 씬 Inspector에 연결하여 비주얼 폴리싱 진행

### B-2. 재접속 실제 구현
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs`
- **현황**: 30초 대기 후 ForceWin만 구현
- **구현 필요**: NGO Reconnect API 활용, 재접속 후 게임 상태 복원

---

## Phase C — 게임플레이 완성도

### C-0. 싱글플레이 AI 시스템
**🔵 코드 구현 완료 (2026-06-07)**: LocalPlayerDifficulty / AIConfig / AIScenarioConfig ScriptableObject / AIOpponentController(빌드오더+반응시스템+BFS) / GameBootstrapper 연동 / 로비 난이도 선택 UI(DifficultySelectView) 전체 완료. 3종족 시나리오 에셋 개편 완료 (2026-06-10): Human/Spirit/Transcendence 각 1개 에셋 × 3시나리오, Domain 레이어 아키텍처 정리.

**✅ Inspector 작업 완료 (2026-07-16)**: AIConfig 에셋 생성, AIScenarioConfig 3종족 에셋 생성, DifficultySelectView 레이아웃 배치 반영(실기 테스트가 진행된 사실로 확인).

**🔵 핵심 흐름 실기 조건부 완료 (2026-07-16)**: 유닛 생산·건물 업그레이드 등 핵심 흐름을 반복 실기로 확인 — PASS, 특별한 문제 미발견.

**남은 작업 (세부 정밀 검증)**:
1. 반응 시스템(R1 유닛열세 / R2 골드과잉 / R3 채굴소 파괴)의 트리거·동작 정밀 검증
2. 3종족(Human/Spirit/Transcendence) 시나리오 무작위 선택 동작 확인

---

### C-1. 게임 내 밸런싱
현재 수치는 임시값. 플레이테스트 후 조정 필요.

**✅ 밸런싱 인프라 완료 (2026-04-25)**: `UnitStatsConfig`, `BuildingStatsConfig` ScriptableObject 전환 완료. 코드 수정·재컴파일 없이 Unity Inspector에서 수치 직접 편집 가능.

**✅ 건물 스탯 전체 확정 (2026-05-18)**: StatsReference.md 기준으로 모든 종족 건물 HP/비용/공격력/쿨다운 확정. BuildingStatsConfig.asset 32종 항목 전부 채움.

| 항목 | 현재값 | 조정 방향 |
|------|--------|---------|
| 시작 골드 | 500 | 테스트 후 결정 |
| 채굴소 수입 | 10골드/초 | 타일 경제 밸런스 체크 |
| Pistoleer HP/공격/사거리 | 300 / 60 / 1.0 | DPS=30, cooldown≈2.0s — 2026-08-10 ×10 스케일 반영 정정 |
| Assault HP/공격/사거리 | 400 / 10 / 2.0 | cooldown≈0.2s ※2 — 2026-08-10 ×10 스케일 반영 정정 (HP 400은 단순 ×10이 아닌 별도 조정값) |
| Sniper HP/공격/사거리 | 300 / 180 / 5.0 | DPS=60, cooldown≈3.0s — 2026-08-10 ×10 스케일 반영 정정 (공격력 180은 단순 ×10이 아닌 별도 조정값) |
| Castle HP (Human `Castle` / Spirit `SpiritNexus` / Trans `ElderTree`) | 2000 / 1500 / 3000 | 확정 — 플레이테스트 후 조정. 2026-08-10 ×10 스케일 반영 정정 |
| AutoTower 공격력/쿨다운 (Human · CannonTower) | 150 / 5.0s | 확정 — 공격력은 ×10 스케일(2026-07-31) 반영값, 쿨다운은 ×10 대상 아님 |
| AutoTower 공격력/쿨다운 (Spirit · RuneSpire) | 150 / 3.5s | 확정 — 가장 강한 타워. 공격력 ×10 반영, 쿨다운 불변 |
| AutoTower 공격력/쿨다운 (Trans · **VineTower**) | 150 / 5.0s | 확정 — 공격력 ×10 반영, 쿨다운 불변. ⚠️ Transcendence 방어 타워는 **VineTower**이며 MistShrine이 아니다(MistShrine은 공격하지 않는 별도 힐 건물 `HealShrine`=6) |
| MistShrine 회복량 / 범위 반경 (Trans · HealShrine) | **미확정** | **미확정(밸런싱 예정)** — 2026-08-10 물안개 힐 재설계로 재산정 대상. 종전 표기값(이 표의 옛 값 1 HP/s는 ×10 이전, `StatsReference.md`의 10 HP/s는 ×10 적용값, 범위 3)은 **모두 재설계 이전 수치**다 |
| MistShrine 물안개 지속시간 / 쿨다운 (Trans · HealShrine) | **미확정** | **미확정(밸런싱 예정)** — 재설계로 신규 필요한 수치. **지속시간 < 쿨다운**(다운타임 필수, 상시 유지 설정 금지) |
| MistShrine 회복 텍스트 표시 주기 (Trans · HealShrine) | **미확정** (임시 3초) | **미확정(밸런싱 예정)** — 회복 틱(1초)과 표시 주기를 분리. 화면 도배 방지용 임시값 |

> **※ 수치의 단일 진실 소스(SSOT)는 [StatsReference.md](StatsReference.md)다.** 위 표는 밸런싱 논의용 **요약**이므로, 두 문서의 값이 갈리면 **항상 `StatsReference.md`를 따른다**(이 표를 근거로 `StatsReference.md`를 고치지 말 것). 2026-08-10, ×10 스케일 개편(2026-07-31) 이전 값이 이 표에 남아 있던 것을 `StatsReference.md` 기준으로 정정했다 — Pistoleer · Assault · Sniper · Castle HP 4행. ⚠️ **Assault HP(400)와 Sniper 공격력(180)은 단순 ×10이 아니라 별도 밸런스 조정이 반영된 값**이므로, 계산으로 유도하지 말고 `StatsReference.md` 원문을 그대로 옮길 것. 이 표의 이동속도·사거리·쿨다운·골드 비용은 ×10 대상이 아니다.
>
> **※2 Assault 쿨다운 표기 불일치(이번 정정 범위 밖):** 이 표의 `cooldown≈0.2s`는 `StatsReference.md`의 실제 값 `0:17(0:33)` = **0.33초**와 어긋난다. 쿨다운은 ×10 대상이 아니어서 이번(2026-08-10) 정정에서 손대지 않았으며, 어느 쪽이 맞는지 확정한 뒤 정정한다. 확정 전까지 Assault DPS는 재산정하지 않는다.

> **MistShrine 관련 수치 근거:** [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템 규칙 16**(밸런싱 미확정 표)이 단일 소스다.
> **2026-08-12 갱신 — 구현은 완료됐지만 수치는 여전히 미확정이다.** 현재 게임에서 도는 값은 **전부 임시값**이며(`_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/Plan.md` §3), **어떤 문서에서도 확정 수치로 인용해서는 안 된다.**
> 밸런싱 확정 시 바꿀 곳은 **`Resources/Config/SpecialAttackConfig.asset` 한 파일뿐**이다(코드 기본값은 폴백일 뿐이며 Inspector 값이 우선). 반경 값이 표시용/판정용으로 이중화돼 있던 문제는 2026-08-11에 제거되어, **에셋만 고치면 화면의 범위 원까지 함께 따라간다.**

---

### C-2. 연구소 유닛 강화 시스템 + 전투 스탯 ×10 개편 + 연구 패널 UI ✅ 구현 완료 · 멀티플레이 실기 PASS (2026-07-31)

연구소(Research 건물)로 팀 단위 유닛을 강화하는 신규 시스템 + 함께 진행하는 전투 수치 ×10 스케일 개편 + 연구 패널 UI. **구현 완료, 멀티플레이 실기 테스트 통과.**

- **전투 스탯 ×10 스케일**: 유닛 HP·공격력, 건물 HP, 타워 HP·공격력, DoT 틱값, 힐량 전부 ×10. HP·공격력 동일 배율로 **TTK 불변**(상성·매치업 불변). ×10은 config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)(1회 실행).
- **강화 스탯**: 공격력(유닛별 고정 정수 증가) / 방어력(신규, K=120 감쇄) / 이동속도 배율 + 초월 자연회복(3~15 HP/s). `UnitUpgradeUseCase`가 (B) 실시간 배율/증가치 조회 → 소급 강화.
- **네트워크**: 완료 레벨 양 클라 브로드캐스트(효과 양쪽 적용), 진행 중은 소유자만, 파괴 시 취소·100% 환불. `NetworkUpgradeController`. MP 완료 처리 서비스 스폰 레이스 버그 `ResolveServices()`로 수정.
- **연구 패널 UI**: `ResearchPanelUI : BuildingPanelBase`(헤더·닫기·철거+환불) + 매트릭스/진행 2-레이어(연구소 단위 전환, 진행 트랙 잠금). 규칙 13.
- **후속 보류(미완/미검증 — 별도 작업)**: ① UI 레이아웃 다듬기(자동생성 골격) ② 매트릭스 헤더 아이콘 ③ AI 연구 사용 실기 ④ **MistShrine(HealShrine) 힐 — 2026-08-12 구현 완료 · 에디터 싱글플레이 실기 검증 완료 / 멀티 미검증 · VFX·아이콘 미제작 · 밸런싱 미확정**(규칙: [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템**. 연구소 자연회복과는 **별개 효과로 중첩 적용** — 규칙 14) ⑤ 싱글 자연회복 실기.
- **문서**: `GameSystemRules/GameSystemRules_Upgrade.md`(구현 계약·구현 상태·규칙 13 UI), `StatsReference.md`(×10 값·업그레이드 표), `GameSystemRules_Units.md` 규칙 44(방어력 감쇄), GDD 연구소 섹션. old/new 대조: `_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md`·`Research.md`·`Plan.md`(하단 "완료 결과").

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 방어/마법 타워
**✅ 방어 타워 완료 (2026-06-01~02)**: TowerCombatUseCase 신규 구현. 종족별 스탯(사거리 4.0/공격력 15, 쿨다운 Human·Trans 5.0s/Spirit 3.5s). 서버 권위 처리. Human CannonTower 초기 방향(내 진영 0도/상대 진영 180도) 구현 완료.
- 스킬 건물(FlightFacility / MagicSpirit / WillowShrine): **Phase 1 + Phase 2 구현 완료 · 실기기 테스트 PASS (Phase 1: 2026-08-04, Phase 2 타입 C: 2026-08-05 실기+멀티(클라))**. **마나 미도입** — 자원 없이 건물별 글로벌 쿨다운만 사용. 건물당 최대 5개 스킬, 3×3 패널 UI(슬롯 1~5 스킬/6 철거/7~9 예약), 모바일 탭 조준(2단계·엣지 스크롤·취소 X Lerp 확대), 서버 권위. **타입 A(즉발 범위 피해)·B(장판 DoT)·C(전역 상태변경: 버프/디버프/CC/힐) 실행기 완료**, 조준 지점 연속 좌표화(HexCoord→Vector3, 반경 판정 무변경·중심만 연속화)·맵 경계 point-in-bounds 재검증·조준원 지면 데칼 렌더링(`SkillAimOverlay.shader`)·취소 버그 수정·쿨다운 안내 토스트까지 반영. 타입 C는 상태효과 시스템(`StatusEffectSystem` 서버 권위)+유효 스탯 곱연산 합성+공격 게이트(무상태 회귀 안전)·빙결 애니 정지·둔화 라이브·회복 HoT·멀티 `StatusAppliedClientRpc`. **건물 파괴 시 열린 패널/조준 UI 원복은 2026-08-08 구현 완료·실기 PASS(`BuildingPanelBase` OnBuildingDied 구독→Close, 4개 건물 패널 공통).** **미완: 구체 스킬 목록/수치/아이콘(기획, 현재 플레이스홀더 5슬롯 테스트용).** 상세: [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md)(규칙 1~26), task `_Tasks/2026-07-28/12_14_skill-building-system-design/`·`_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/`.

### D-2. 건물 업그레이드 시스템
**✅ 완료 (2026-05-17~18)**: 종족별 단계 BuildingType 26종 확장, BuildingPlacementUseCase.UpgradeBuilding(), NetworkBuildingController RPC, ProductionPopup 업그레이드 버튼/아이콘, 철거 환불 누적 계산 완료. 전 종족 테스트 통과.

### D-2-1. 건물 철거 로직 구현
**✅ 완료 (2026-05-18~19)**: BuildingActionPanelUI를 통해 비생산 건물(채굴소/타워 등) 철거 가능. BuildingPanelBase.OnDemolishButtonClick()에 싱글/멀티 분기 공통 구현. 채굴소 전용 패널(일시정지 등)은 별도 작업.

### D-3. 유닛 AI 상태머신
**✅ 기본 전투 AI 구현 완료 (2026-05-11)**: 슬롯/점유 시스템 폐기. 근접·원거리 모두 단일 상태 머신(MoveAlongPathV3) 적용. A* 이동 → 적 감지(DetectRange) → 직선 추격(EnterCombatPursuitV3) → 공격 → 재개(Lerp 정렬) 사이클 완성. 겹침 허용, 모든 유닛 동일 규칙 적용.
- 추가 목표: Idle → Patrol → Retreat 상태 확장 (현재 미구현)

### D-4. 신규 유닛 프리팹 완성 (16종)
**🔧 에디터 스크립트 완료 (2026-06-05)**: Human 5종(LittleKnight/SpearMan/BattleAxe/Tank/CannonCart)·Spirit 6종(DustSpirit/BoulderSpirit/QuakeSpirit/TideSpirit/StreamSpirit/TorrentSpirit)·Transcendence 5종(RabbitTrickster/RhinoBreaker 등) × Blue/Red 총 32개 프리팹 자동 컴포넌트 부착. `Assets/Editor/Setup/SetupNewUnitPrefabs.cs`

**진행 (2026-07-21)**: 특수 유닛 5종 **전량 구현 완료 + InfernoSpirit DoT까지 완료** — BattleAxe(휩쓸기형 부채꼴, 2026-07-17), TorrentSpirit(파도형 이동 AoE + 힐 서브시스템, 2026-07-17), BloomFairy(힐러 전용 경로 + HoT/DoT 공용 시스템, 2026-07-18), MushroomBomber(착탄형 원형 반경 DoT + DoT 초 단위 틱 모드 + 식물 라인 생산 배선, 2026-07-19), InfernoSpirit(단일 대상 DoT — 규칙 40 초 단위 틱 재사용, 값 별도 진입점 분리, 2026-07-20), QuakeSpirit(착탄형 즉발 2단계 AoE — 주 타깃 100%/스플래시 50%, 스플래시가 건물도 포함, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용 + 건물 순회 신설 + 유닛/건물 hit-set 분리, 2026-07-20 멀티 로그 검증). 특수 공격 전략 핸들러 아키텍처(핸들러 + 레지스트리 1줄) 확립. **잔여 없음**(BloomFairy는 힐러 전용 경로로 의도적 미등록). **알려진 이슈(보류, 별도 task)**: QuakeSpirit `OnAttackHit` 미주입 타이밍 어긋남, 멀티 원거리 facing 버그. 상세: `PROJECT_STATUS.md` / `WORK_HISTORY.md`, task `_Tasks/2026-07-16/18_06_battleaxe-aoe/` · `_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/` · `_Tasks/2026-07-18/03_40_bloomfairy-healer/` · `_Tasks/2026-07-19/01_42_mushroombomber-impact-dot/` · `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/` · `_Tasks/2026-07-20/10_24_quakespirit-impact-aoe/`.

**남은 작업**:
1. 실기 테스트 — 프리팹 컴포넌트 부착 정상 동작 확인
2. Animation Event 부착 (각 유닛 공격 애니메이션 타이밍)
3. UnitFactory 종족별 리스트에 신규 16종 등록
4. StatsReference.md 스탯 확정 후 UnitStatsConfig Inspector 입력

---

## Phase E — 플랫폼/폴리싱

### E-1. 사운드/BGM
- BGM (로비/인게임/승리/패배)
- 효과음 (공격, 건물 건설, 골드 획득, 유닛 사망)

### E-2. 튜토리얼
- 첫 실행 시 인터랙티브 튜토리얼
- 헥스 클릭 → 건물 건설 → 유닛 생산 → 공성 흐름 안내

### E-3. Firebase 백엔드
- 실시간 글로벌 리더보드 (Firestore onSnapshot)
- 승/패 기록 저장 (Firestore)
- Android 인앱결제 (Google Play Billing — 스킨)
- Firebase Functions (경기 결과 처리, IAP 영수증 검증)

### E-4. 로그인 시스템 구현 (Login.unity)
- Login.unity 씬 신규 생성 (Build Index 분리)
- LoginUI (익명/Google Play Games/이메일+비밀번호 선택 화면)
- ProfileView 계정 연동 탭 구현 (익명 → 실계정 전환)
- AuthSystemRules.md 기준 구현

---

## Phase F — 전투 타격 타이밍 동기화 후속 (2026-07-12 등록)

전투 타격 타이밍 동기화 작업(`_Tasks/2026-07-09/01_12_combat-hit-timing-sync/`) 완료 후 남은 후속 항목.

### F-1. 이동/Walk 애니메이션 동기화 ✅ 완료 (2026-07-13)
- **결과**: 애니메이션 상태(None/Walk/Attack)를 1회성 엣지 RPC → NetworkUnit의 **NetworkVariable 레벨 동기화**로 전환. 클라 스폰 시 현재 값 자동 적용으로 첫 Walk RPC 스폰 레이스 유실(222/223기) 구조적 소멸. `UnitView.Initialize` 후 `ReapplyAnimStateToView()`(멱등) 재적용으로 애니메이터 미준비 무음 실패 봉합. 재경로 첫 스텝 역방향(뒤로 밀림, 서버 282건)은 `AlignPathStartToTransform`로 282→41 급감.
- **검증**: 3차 로그 15,316줄 — 생성 634기 전원 상태 적용 보장, 육안 걷기 미재생/뒤로 밀림 소멸. 잔여 41건은 스폰 직후 우회 경로를 판정 지표가 오탐한 계측 한계로 코드 무수정 종결. 규칙 U-22 등재.
- **task**: `_Tasks/2026-07-12/07_55_movement-walk-anim-sync/`.

### F-2. 빌드 에셋 용량 최적화 ✅ 완료 (2026-07-15)
- **결과**: Android AAB 용량 **190.66 MB → 125.30 MB**로 65.36 MB 절감. 작업 브랜치 `codex/asset-size-optimization`이 main에 병합됨.
- **주요 변경**: `Assets/_Project/Texture/Buildings/**`, `Assets/_Project/Texture/Units/**` Android max texture size `1024 → 512`. `_Old` 미사용 에셋 디렉터리 7개 정리, normal-map PNG 93개 + roughness PNG 84개 정리, 건물/장비 FBX import를 보수적으로 조정.
- **유지한 영역**: UI 배경, 유닛 초상화, 건물 아이콘, UI 스프라이트, TextMeshPro 폰트 에셋은 품질 확인 전 유지. TMP Font Atlas 축소 테스트는 AAB 효과가 작아 되돌림.
- **후속 확인**: 기기 QA에서 설치/실행, 로그인, 로비 UI 가독성, 인게임 유닛/건물 텍스처 품질, 팀 색상 변형, emission/공격 이펙트 품질을 확인한다.
- **상세 문서**: `AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`.

### F-3. 피격 VFX 프리셋 연결 🟡 중간
- **작업**: `UnitEffectConfig.hitPreset`을 Inspector에서 각 유닛에 배선. Human 6종 등 미연결.
- **비고**: 코드 아님, 에셋 연결 작업.

### F-4. 미구현 특수 타격 클립 이벤트 주입 🟢 낮음
- **✅ BattleAxe 완료 (2026-07-17)**: 휩쓸기형 AoE 구현과 함께 Attack 클립 `OnAttackHit` 이벤트 주입 완료(`hitFrameTimes=1.1667s`, 클립 타격모션 종료 프레임 35f/30fps). `Hexiege/Combat/Inject OnAttackHit Events` 인젝터 사용.
- **✅ TorrentSpirit 완료 (2026-07-17)**: 파도형 이동 AoE + 힐 서브시스템 구현과 함께 `OnAttackHit` 주입(`0.5s`, 임시 — 실제 파도 발동 프레임 튜닝 대상). 규칙 28~31 신설.
- **✅ BloomFairy 완료 (2026-07-18)**: 힐러 전용 경로 구현과 함께 힐 발동 타이밍을 `HitFrameTimes` 타이머로 구동(`OnAttackHit`은 힐 연출 전용). HoT/DoT 공용 시스템 신설. 규칙 32~37 신설.
- **✅ MushroomBomber 완료 (2026-07-19)**: 착탄형 범위 DoT 구현과 함께 Attack 클립 `OnAttackHit` 주입(규칙 27, 1개). DoT 초 단위 discrete 틱 모드(규칙 34 확장) 신설. 규칙 38~40 신설.
- **✅ InfernoSpirit 완료 (2026-07-20)**: 이미 완성된 원거리 유닛(에셋·VFX·생산·`OnAttackHit` 완비)에 단일 대상 DoT 특수만 추가(`InfernoAttackBehavior`, 레지스트리 1줄). 주 타깃 1마리 직접 25 + 그 1마리에게만 DoT 5/초×3초(AoE 아님). 규칙 40 초 단위 틱 재사용, 값은 별도 진입점(`ApplyInfernoDot` 5/3)으로 분리. 규칙 41~42 신설.
- **🔨 QuakeSpirit 특수 공격 로직 완료·클립 이벤트 잔여 (2026-07-20)**: 착탄형 즉발 2단계 AoE 구현·멀티 로그 검증 완료(규칙 43, 핸들러 `QuakeAttackBehavior` + 레지스트리 1줄, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용). 단 Attack 클립 `OnAttackHit`은 **아직 미주입** + `hitFrameTimes` placeholder(1.0s)로 타격 애니↔데미지 텍스트 타이밍이 어긋남(스플래시 텍스트가 `HitPresentationQueue` 타임아웃 7.5s까지 지연 — 피해·판정·동기화 자체는 정상). 사용자 결정으로 **별도 후속 task 분리**.
- **남은 대상**: QuakeSpirit `OnAttackHit` 클립 이벤트 주입만 잔여(특수 공격 핸들러 확장은 5종 전량 완료). BloomFairy는 힐러 전용 경로로 의도적 미등록.
- **작업**: QuakeSpirit 후속 task에서 실제 타격 프레임으로 `hitFrameTimes` 확정 후 `Hexiege/Combat/Inject OnAttackHit Events` 인젝터로 클립에 `OnAttackHit`을 주입(규칙 17·27) → 타이밍 어긋남 해소.

### F-5. Firebase/EDM 저장소 방침 정리 🟡 중간 (2026-07-13 등록)
- **현황**: 이번 세션에서 발견 — 신규 환경에 저장소를 클론하면 Firebase/EDM(External Dependency Manager) 관련 임포트가 누락되어 재임포트가 필요한 문제.
- **작업**: Firebase/EDM 패키지·플러그인을 저장소에 어떻게 커밋/제외할지(버전 관리 방침)를 정리하여 신규 클론 시 재임포트 없이 빌드 가능하도록 정비.
- **비고**: 인프라/저장소 관리 작업(코드 아님).
- **2026-07-13 진행**: 관련하여 `#if HEXIEGE_ENABLE_FIREBASE_AUTH` 컴파일 게이트를 제거(커밋 4fe1cf0)해 SDK 로컬 임포트 시 실제 Firebase 코드가 무조건 컴파일되도록 복원(스텁 컴파일로 로그인 무조건 실패하던 버그 해소). **남은 작업은 SDK/EDM 자체의 버전 관리(커밋/제외) 방침 확정**으로, 게이트 제거와 별개.
## 2026-07-16 로비 프로필/랭킹 클라우드 연동 상태

- 완료: UGS Cloud Save 플레이어 프로필 모델/서비스/UseCase, 닉네임 설정/변경 흐름, 로비 Profile 탭 전적 표시, UGS Leaderboards Ranking 탭, RankRow 프리팹/에디터 생성 스크립트, 기본 UI 레이아웃 보정.
- 완료: 이메일 인증 완료 후 최초 로그인 시 닉네임 설정 화면으로 이동하는 흐름.
- 완료: RankingView의 숨김 상태 자동 로드 제거. 랭킹 데이터는 Ranking 탭 표시 또는 새로고침 버튼에서 로드한다.
- 완료: 이메일 인증 플로우 보완(2026-07-18).
  - 인증 대기 화면에 가입/로그인 시도 이메일을 명시적으로 전달해 표시한다.
  - 회원가입 직후 인증 대기와 기존 미인증 계정 로그인 진입을 구분한다.
  - 회원가입 직후 인증을 포기하고 되돌아가는 경우 Firebase 미인증 계정을 삭제한다.
  - 앱 재실행/자동 로그인 경로에서도 미인증 계정은 인증 화면으로, 닉네임 미설정 계정은 닉네임 설정 화면으로 복귀한다.
  - 장기적으로 생성 후 일정 기간 지난 `emailVerified=false` 계정 정리 정책을 검토한다.
---

## 2026-07-18 이메일 인증 플로우 보정 완료

- 완료: 이메일 인증 대기 화면의 이메일 표시, 뒤로가기 가입 취소 정책, 앱 재실행/자동 로그인 게이트를 보정했다.
- 사용자 실기 PASS:
  - 신규 이메일 회원가입 후 인증 화면에서 실제 이메일 표시.
  - 인증 화면 Back 시 가입 취소 확인 팝업 표시.
  - 가입 취소 확정 시 Firebase Authentication 미인증 사용자 레코드 삭제.
  - 인증 계속하기 선택 시 인증 화면 유지.
  - 인증 화면에서 앱 종료 후 재실행 시 다시 인증 화면 표시.
  - 닉네임 설정 화면에서 앱 종료 후 재실행 시 다시 닉네임 설정 화면 표시.
- 장기 과제: 일정 기간 이상 지난 `emailVerified=false` 계정의 서버/관리자 정리 정책 검토.
