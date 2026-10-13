# 공통 전투 후 이동 복구 및 TorrentSpirit 발사 교정 조사

전투가 끝난 뒤 안전하게 돌아갈 길이 있는데도 유닛이 오래 멈추는 문제를 공통 이동 경로에서 교정하려는 작업이다. 함께 TorrentSpirit은 서버가 이미 승인한 공격이라면 원래 목표가 발사 전에 사라져도 예정된 시각에 실제 파도를 발사하도록 바꾼다. 다른 유닛의 공격 안전 규칙을 완화하거나 화면 효과만 추가하는 작업은 아니다.

## 1. 현재 상태와 승인 경계

**최신 실기 현행 — 2026-10-03 01_03 경기:** 이번 경기 이동·승인 발사 subset 관측 통과,415파도 전달/VFX 일치. Client 텍스트1건 누락은 HP정상과 분리한 clock/text defect OPEN이다. 전체OPEN·Unresolved/LegacyFallback 유지. 아래 실기 전 상태는 과거 이력이며 현재는 §9를 따른다.

- **최종 통합 인계 — 2026-10-03:** 구현·정적 컴파일 인계 및 변경 코드 Unity 메뉴 두 개 PASS까지 확인됐다. File > Build And Run 실제 클릭 뒤 DetectJavaDevelopmentKit(JDK) / Checking JDK 진행을 관찰했다. 빌드 ATTEMPTED·preflight 시작이며 완료/성공은 미확인, 사용자 완료 확인과 실기 교정 판정은 OPEN이다. 아래 준비·GUI 대기 문장은 당시 이력이며 현재는 §7을 따른다.
- **현재 상태 — 2026-10-03 구현 인계:** 코드 변경과 정적 컴파일 인계가 도착했다. 실제 예약 소유자는 `UnitCombatUseCase.TorrentLaunch.cs`의 중앙 서버/싱글 tick이며 §6에 기록한다. 변경 코드 Unity GUI 검증·빌드 시도는 대기, 실기 원인/교정 판정은 OPEN이다. 아래 구현 전·GO 문장은 최초 준비 이력으로 보존한다.
- 작업 기준: 2026-10-02 23:45 KST(14:45 UTC). Research/Plan 및 좁은 영구 규칙 예외 작성, 구현과 빌드 시도는 사용자 승인 범위다.
- 현재는 구현 전 문서 초안이다. 프로그램 에이전트의 파일별 제안·구현·컴파일·변경 코드 Unity 메뉴·빌드 결과는 아직 인계되지 않았다. 교정 PASS/해결 완료를 주장하지 않는다.
- **최신 진행:** 메인이 초안을 승인하고 프로그램 에이전트의 제한된 파일별 제안을 검토한 뒤 구현 GO를 전달했다. 위 구현 전 상태는 최초 작성 이력이다. 현재 제안 경계는 Plan §3에 반영됐으며 구현 완료·검증 결과 인계는 아직 없다.
- 기존 조사 `_Tasks/2026-10-02/15_25_common-post-combat-recovery-diagnosis/{Research,Plan}.md`와 이전 Torrent 파도 보정 근거는 별도 이력으로 보존한다. Torrent resolver는 `Unresolved / LegacyFallback`이며 이 승인만으로 승격하지 않는다.

## 2. 확정 근거와 남은 증거 경계

근거는 `Assets/_Project/Docs/_Logs/2026-10-02/17_05_logcat/RuntimeLog_device.txt`의 Android Host 및 메인의 최종 source/raw-log 검토 인계다. 기존 조사 최신 절의 수치와 원인 경계를 계승한다.

- Torrent24는 16:55:16.886 Blocked에 들어갔다가 17:01:23.421 Align, 17:01:23.460 Move로 복귀했다(약366.5초). Sagan의499개 정지 위치 표본은 별도의 spatial-record 집계가 아니다.
- provider는 Success 경로 `6;16|7;16|8;15|9;14|9;13|9;12`를 반환했다. 첫 hop의 목표 hex 거리는4→4였고 `centerAvailable=False`, spatial stage 없음, accepted counters0이었다.
- 강한 source 추론: 엄격히 가까워지는 직접 중심 후보 조건이 안전한 옆방향 경로 hop을 제외하고, no-center fallback이 같은 경로를 `SpatialRecoverable`로 요청해 `RejectedRepeatedPath` 뒤 Blocked로 들어간다. Blocked loop는 외부 pending path/view environment 변화만 기다린다. 정확한 fallback guard 결정은 로그에 없어 전체 runtime 인과 증명은 아니다.
- provider env12와 View env0는 다른 revision 영역이다. 값 차이만으로 배선 결함을 확정하지 않는다.
- 첫 corridor 실패61건은11개 인스턴스에서 planner accepted/blocked route installed/pursuit target selected 각각61건으로 복구됐다. 영구 정지61건이 아니다.
- Torrent45의 acquireTrue 상황에서 Root `6;14`, 후보 `5;15`, `OutsideLogicalPath`가 관측됐다. source상 acquire 분기에서 갱신되지 않은 alignElapsed의 이전 Lerp 후보를 검증하는 문제가 있다. 공통 코드 문제지만 다른 유닛 종류의 동일 runtime 실패까지 입증한 것은 아니다.
- Torrent18/39의 initial+cleanup Fatal4건 및 Torrent24 복귀 뒤17:01:33.590 EmptySameTile 종료 거부는 원래 장기 Blocked와 분리한다. 건물 랠리 Fatal 교정은 이번 범위 밖이다.
- 4137 chunks→3453 logical records, incomplete0,62 complete dumps/loss counters0은 출력 완결성이다. blocked-only episodeTriggeredFalse로 이후 capture가 숨겨지는 문제, fallback guard 결정 미기록, private planner ordinal 부재는 남아 있다.

## 3. 승인된 Torrent 발사 정책

일반 TargetLocked는 계속 원래 타깃을 추적한다. 이번 예외는 서버가 승인한 Torrent 공격의 발사 예약만 보존하며, 공격 시작 방향을 무조건 고정하는 정책이 아니다.

- 원래 primary가 살아 있고 조회 가능하면 발사 시 그 primary의 현재 서버 방향을 사용한다.
- 원래 primary가 사망·소멸·조회 불가이면 같은 primary에 대해 마지막으로 유효했던 서버 aim을 사용한다. 새 타깃으로 대체하지 않는다.
- primary의 사망 또는 사거리 이탈만으로 승인 예약을 취소하지 않는다. 기존 launch writer에서 살아 있는 primary의 range exit만으로 취소하지 않았다는 관찰과, dead/missing primary를 prelaunch에서 생략하던 구현을 구분한다.
- provisional 애니메이션은 서버 승인과 다르므로 발사하지 않는다. 발사 전 공격자 사망·게임 종료는 기존처럼 취소한다.
- 예약당 서버 파도 생성은 정확히 한 번이다. 파도가 접촉한 현재 살아 있는 적 유닛·적 건물의 피해와 아군 유닛 회복은 기존 서버 판정으로 적용한다. Client VFX만 만들거나 죽은 primary에 가짜 피해 결과를 만들지 않는다.
- 이미 생성된 파도의 기존 수명·취소 정책은 유지한다. 다른 TravelingArea/Homing, MeleeContact/Hitscan까지 무조건 발사시키는 전역 예외가 아니다.

## 4. 영향 경로와 구현 인계 경계

현재 확인된 관련 파일은 아래와 같다. 실제 변경 파일은 프로그램 에이전트 제안과 메인 검토 뒤 확정하며 이 목록은 수정 완료 목록이 아니다.

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`: 공통 post-combat 복구, acquire 후보, Blocked 상태 및 bounded recorder.
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`: 경로 요청과 복구 결과.
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs`: 이동 후보·corridor 승인 계약.
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`: 승인 예약, 예정 시각 dispatch, 서버 파도 생성·contact writer.
- `Assets/_Project/Scripts/Application/Combat/TorrentAttackBehavior.cs`: 파도 방향과 요청 경계.
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.WavePresentation.cs`: 기존 서버 파도 표현 전달 경계 확인 대상.

안전한 경로와 직접 중심 접근을 분리하되 corridor/walkability 검증을 생략하지 않는다. 실제 unsafe 반복 경로 guard를 유지하고, tolerance 확대·Root snap·retry budget 증가는 하지 않는다. stale Lerp 대신 실제 authoritative acquire 후보를 사용한다. recorder는 기존 bounded/read-only 구조를 유지하며 추가 경로 탐색·상태 변이를 만들지 않는다.

## 5. 범위 밖 및 완료 판정

건물 랠리 Root/domain Fatal, 성능 최적화, 전체25종 migration, 에셋·스탯·클립 보정, 새 Testcase/QA/validator는 포함하지 않는다. 다른 유닛의 규칙 충돌이 발견되면 사용자에게 변경 전에 알리고 승인을 기다린다. 컴파일·기존 Unity 메뉴는 통합 확인이며 실제 복구와 발사/contact 재시험 PASS를 대신하지 않는다. 구현 완료 결과는 검증 인계 뒤 별도로 추가한다.

## 6. 실제 구현 소유 구조 및 정적 인계(2026-10-03、최종 완료 아님)

> 현재 GUI/빌드 상태는 §7로 갱신됐다. 이 절의 GUI 대기는 해당 인계 당시 이력이고 실제 코드 구조·파일 목록은 그대로 유효하다.

아래는 프로그램 구현과 정적 컴파일의 인계다. §4의 파일 후보 및 §1의 구현 전 상태를 대체하는 현재 구조이며, GUI·빌드·실기 결과를 대신하지 않는다. 중앙 helper 원문에서 capacity/identity/aim 갱신/소비 순서를 직접 확인했다. 나머지 세부 변경과 컴파일 수치는 메인의 구현 인계 근거로 기록한다.

- `UnitCombatUseCase.TorrentLaunch.cs`가 서버/싱글 예약을 소유하고 tick에서 발사한다. cap1024, service epoch+단조 reservation key+공격자/원래 primary 객체 identity를 사용한다. 원래 primary의 유효 aim을 매 서버 frame 갱신하며 상실 시 마지막 유효 방향을 보존한다. 발사 원점은 현재 caster 위치다.
- cooldown/commit 전에 예약 용량을 확보한다. 거부 시 commit하지 않으며 활성 예약을 eviction하지 않는다. 실제 SpawnWave 호출 전에 예약을 제거하여 재진입에 의한 이중 생성 경계를 닫는다. 이는 코드 설계 확인이며 runtime exactly-once PASS는 아직 아니다.
- 공격자 사망·gameend·despawn/shutdown·서비스 교체는 예약을 닫고 진단을 남긴다. Stop/target switch는 승인 예약 소유자가 아니다. 타깃 ID 재사용은 원래 객체 identity로 차단한다. 공통 targetalive damage gate와 C3 migration 상태는 유지한다.
- `UnitView`는 no-center fallback을 General 경로로 처리하고 즉시 acquire는 현재 Root를 사용한다. typed EmptySameTile center intent는 실제 안전한 중심 도착 completion에 한정한다.
- `UnitMovementUseCase`는 검증된 A*의 첫 인접 route checkpoint에서만 동일 goal distance hop을 허용한다. 직접 중심 탐색의 전역 strict-closer 완화나 corridor safety 우회는 아니다. 실제 SpatialRecoverable guard는 유지한다.
- diagnostics는 guard decision, post-Blocked 후속 상태 및 launch reservation→wave key를 연결한다. private planner ordinal과 Legacy world aim/SimulationFacingAim 경계는 여전히 남는다.

정적 컴파일 인계: Runtime/Release/Development/Editor 각각 exit0·errors0, warnings는 순서대로20/24/20/0. Runtime276 sources 누락0, helper 포함 정확히1회. 이것은 Unity 변경 assembly 검증·Android 빌드 성공·실기 복구/발사 증거가 아니다.

실제 코드 변경 인계 목록(10 C# + helper meta):

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.TorrentLaunch.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.TorrentLaunch.cs.meta`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Application/Combat/TorrentAttackBehavior.cs`
- `Assets/_Project/Scripts/Application/Combat/WaveSpawnRequest.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs`

§4의 `UnitMovementContracts.cs`와 `UnitCombatUseCase.WavePresentation.cs`는 확인 후보였으며 위 실제 변경 목록에 포함되지 않는다. 싱글 pending hits/네트워크 delayed coroutine에서 직접 예약을 소비하는 초기 제안 대신 서비스 중앙 tick이 수명을 소유한다. Bootstrap 변경은 기존 tick·shutdown/서비스 교체 연결 범위이며 새 취소 정책이 아니다. 최종 GUI/build 인계를 기다리고, 다른 유닛 규칙의 문제·충돌은 변경 전에 사용자에게 알리는 영구 지시를 유지한다.

## 7. 최종 통합 검증 및 빌드 시도 인계(2026-10-03)

새 코드 Unity refresh/domain reload가 완료된 뒤 실행한 실제 메뉴 결과다. 메인의 GUI 관찰 인계이며 이전 코드 메뉴 PASS를 재사용한 것이 아니다.

- Editor Console errors0, compile warnings20. self-validation 뒤 warning 집계는25이며 warning0으로 기록하지 않는다. 별도 정적 Editor compile warnings0과 Unity Console 집계는 다른 검사다.
- 00:19:46 `RunUnitActionSelfValidation` 실제 메뉴 PASS: UAS/production timeline/C2/C3 네 부분 모두 통과.
- 00:20:28 Root Pose Cross Audit self-validation 실제 메뉴 PASS. 이는 runtime CrossAudit Analyze나 목표 이동 성공 판정이 아니다.
- File > Build And Run 실제 클릭, 최신 관찰은 DetectJavaDevelopmentKit(JDK) / Checking JDK progress다. **ATTEMPTED / preflight started, completion 미확인**이다. 이번 시도를 이전15_25의 signing-password 미시작 사례와 합치지 않는다.
- 완료 모니터링·signing 비밀번호 탐색·설정 변경은 하지 않았다. 빌드 완료/설치/기기 실행은 사용자 확인 영역이다.

구현·통합 단계 인계는 완료됐으나 신규 실기에서 shared recovery, 원래 primary 상실 시 current/last-valid aim, 중복 없는 서버 wave/contact HP·힐을 확인한 증거는 아직 없다. 따라서 runtime OPEN, Torrent Unresolved/LegacyFallback, 랠리 Fatal·성능·전체 migration 별도 OPEN을 유지한다. 실제 구현 전체11파일(10 C# + helper meta)은 §6의 목록을 따른다. asset/stats/clip·TC·새 validator 변경은 없다.

## 8. 전체 작업 파일 목록 및 메인 검사 인계

> 이 절은 이전 구현 작업 목록이다. 이번 최신 경기 문서 반영은 §9의 범위만 수행한다.

**코드 추가:** §6의 `UnitCombatUseCase.TorrentLaunch.cs`와 `.cs.meta`.
**코드 수정:** §6의 나머지9개 C# 파일. 전체10 C# + meta11개이며 문서 담당자가 코드를 추가 수정한 것은 아니다.

**작업 문서 추가 및 후속 갱신:**

- `Assets/_Project/Docs/_Tasks/2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Plan.md`

**승인 규칙·인덱스 수정:**

- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`
- `Assets/_Project/Docs/GameSystemRules.md`

**상태·이력·메모리·실수 기록 수정:**

- `Assets/_Project/Docs/PROJECT_STATUS.md`
- `Assets/_Project/Docs/ROADMAP.md`
- `Assets/_Project/Docs/WORK_HISTORY.md`
- `.claude/MEMORY.md`
- `.claude/agent-memory/document-manager/MEMORY.md`
- `.claude/agent-memory/game-programmer/MEMORY.md`
- `.claude/agent-memory/game-programmer/combat-fixes.md`
- `.claude/agent-memory/qa-tester/MEMORY.md`
- `.claude/agent-memory/game-design-lead/MEMORY.md`
- `.claude/mistakes.md`(기존 사건 보강, 별도 신규 사건 아님)

**메인 담당:** `.claude/agent-memory/_baseline.json`은 checker의 증가 갱신으로 메인이 반영한다. 문서 담당자가 직접 편집하거나 checker를 동시 실행하지 않았다. 이번 최종 갱신의 메모리 폴더 순증은 document-manager+2행, game-programmer+5행(index2+topic3), qa-tester+2행, game-design-lead+4행이며 공용 MEMORY는 별도+2행이다. 이전 초안 단계 증가분과 합산 여부는 메인의 직전 baseline 기준으로 확인한다. 이전 checker0은 이번 수정 후 검사 결과를 대신하지 않는다.

## 9. 최신 사용자 경기 — 이동·발사 관측 통과 / 텍스트 결함 OPEN(2026-10-03)

쉽게 말하면 이번 경기에서는 멈춤이 보이지 않았고, 원래 목표가 사라져도 파도가 실제로 발사되어 피해·회복을 적용했다. 하지만 Client 피해 숫자 하나가 빠졌다. 피해 자체와 HP는 정상이며, 파도나 네트워크 수신 누락이 아니라 표현 시계가 잠깐 뒤로 가면서 수신 결과를 거부한 별도 문제다. 모든 공격이 실패한 것으로 바꾸거나 전체 PASS로 닫지 않는다.

근거: Editor Host `Assets/_Project/Docs/_Logs/_editor/2026-10-03/RuntimeLog.txt`, Android Client `Assets/_Project/Docs/_Logs/2026-10-03/01_03_logcat/RuntimeLog_device.txt`(01:03:30 저장). 동일 session `81ef6adac7a937d385c36562f07c887278b6e6be10935ff39d1480423762c25a`, epoch `4c5fc1a2f46db585`. 집계/source 판단은 메인의 검증된 read-only 분석 인계이며 핵심 raw anchors를 재확인했다.

- 예약421=발사415+gameend취소6(keys416..421), current aim215/last-valid200. launch→wave415 unique·중복0. Host serverSpawn415=Client poolplay415 exact wave ID 집합, 양쪽VFX emit415.
- 서버 HP 변화320=유닛 피해265+건물 피해29+아군 유닛 힐26. 만피 heal919는 정상 zero·텍스트 억제, 누락 아님. lost-primary200파도에서도 유닛 피해37/건물 피해1/힐5가 실제 적용됐다.
- Server publish320=Client receive320, Host text320/Client text319. 누락 exact key는 위 epoch/wave1/impact2/unit4 하나다. coordinator failure1이나415VFX는 모두 존재한다.
- Client clock92.216268→92.183309(32.959ms 회귀),00:58:46.271 ValidateClock 거부/return;00:58:46.280 clock92.216294 회복. Presenter Update의 Math.Max와 ReceiveSpawn/Contact의 raw `_combat.WaveServerTime` 경계 차이를 메인이 source로 확인했다.
- 해당 contact damage200·HP500→300: Host lines187~189 publish/accepted/send, line202 text success. Client line17477의00:58:46.269 UnitId4 AppliedDamage200 Hp300은 HP정상 근거다. Client lines17437~17440 이전 clock,17487~17490 수신/dispatch/clock-invalid,17541 clock-recovered. 피해 권위는 유지되고 텍스트만 누락됐다.
- contact92.478973와 receivenow92.183309 차이295.664ms는 기존250ms future guard도 넘는다. Math.Max만으로 모든 경우가 해결된다고 단정하지 않는다.
- **후속 제안만:** 공유 monotonic presentation clock과 권위 contact bounded retention/retry 검토. epoch/exact-key dedupe·finite/time validation·expiry·HP authority 유지가 전제다. tolerance 확대·catch-all·무조건 accept·규칙 변경은 하지 않는다. 이번 요청은 문서 반영이지 교정 구현 승인이 아니다.
- Host MOVE99761frames rejected/invalid/gate/adapter0, spatialFatal/preflight/commit/repeatedrecoverable0, planned1141=committed1141·checkpoints941. Blockedphase0; PC retire10/captures1428/dumps0/faults0/openepisodes0. Host lines33206~33209 MOVE terminal과 line33086 gameend취소6 확인.
- 이전366.5초 stall은 미재현, dumps0은 trigger 없는 관측이다. 직접 old-trigger 회귀·보편적 수정 증명은 아니다. 사용자 육안 문제 없음은 이번 경기 이동·발사 subset 관측 통과를 뒷받침한다.
- Local ROOT 양쪽PASS는 formal CrossAudit가 아니다. GameLog errors양쪽0; ClientWARN283=UnitFactory148+production130+WaveDIAG2+Presenter1+rootcapacity1+disconnect1, HostWARN2=rootcapacity1+disconnect1. ERROR0을 텍스트 완전성으로 확대하지 않는다.
- 사용자 경기 artifact는 사용자가 빌드/실행한 사실을 뒷받침하나 이전 Codex 빌드 완료 직접 관찰로 소급하지 않는다. 빌드 모니터링 없음.

현행: **이번 경기 이동·발사 subset 관측 통과 / Client clock·text defect OPEN / 전체OPEN·C3 Unresolved/LegacyFallback**. 랠리 Fatal·성능·다른 종류 보편적 이동 증명은 범위 밖이며 이전 파도 보정·구현·검증 이력을 보존한다. 이번에는 Research/Plan·Status/Roadmap/History와 관련 기존 메모리11파일만 갱신하며 코드·규칙·빌드·새Task/TC/validator·에셋/스탯/클립 변경은 없다. Checker/baseline은 메인이 담당한다.
