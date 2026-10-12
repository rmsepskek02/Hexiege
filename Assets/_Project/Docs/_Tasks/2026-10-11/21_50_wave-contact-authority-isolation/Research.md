# 파도 접촉 표현 예외와 서버 권위 처리의 분리 — 조사

파도 접촉의 화면 표현이 실패하더라도 이미 적용한 피해·회복과 뒤따르는 사망·제거, 나머지 대상 처리는 계속되어야 한다. 이번 작업은 소스 감사에서 확인한 예외 전파 위험을 예방적으로 격리하는 좁은 작업이다. 정상 Torrent 개별 테스트의 사용자 수용은 유지하며, 이 위험이 실기에서 재현됐거나 기존 피해가 누락됐다고 판단하지 않는다.

## 2026-10-12 최신 clock 재현·승인 계획 인계 — 구현 GO 전

## 2026-10-12 10_10 저장 경기 — 전달·표시 scoped PASS

Main 독립 분석/full-key 교차대조와 **Programmer(Bacon) 독립 최종 결과**의 일치 인계에 따라 이번 경기의 **전달·표시만 scoped PASS**로 기록한다. 최종 대조 및 raw 행 근거까지 수신 완료했으며, 문서 담당이 raw 로그를 재분석하거나 코드를 실행한 결과가 아니다. **ClockPending 수신보류·회복 실전 분기 미실행 OPEN**, 상위 clock/NGO 원인 미해결 및 정밀 시각/global closure 미확정을 유지한다. 이전 native PASS·빌드 클릭 이력은 별도 축이다. 최초 Main 인계 당시의 추가 결과 미수신 단계는 이 최종 인계로 supersede한다.

### 원본 및 경기 identity

- Device(server): `Assets/_Project/Docs/_Logs/2026-10-12/10_10_logcat/RuntimeLog_device.txt`.
- Editor(client): `Assets/_Project/Docs/_Logs/_editor/2026-10-12/RuntimeLog.txt`.
- Delivery epoch: `443cdc5dde2f8a57`. 역할은 실제 device=server/editor=client이며 이전 반대 역할 경기와 섞지 않는다.
- 아래 수치는 Main 및 Bacon 최종 인계 모집단이다. 최초 Main 인계에서 raw 행이 없었던 단계는 supersede하며, 이번 Bacon 최종 결과가 제공한 contact2336→1377→1398, 마지막contact85423→texts85559/18969, GameEnd85556/18966을 아래 원본 파일별 행 표에 기록했다. 행 번호를 추정하지 않았고 민감 header/command line을 출력하거나 전재하지 않았다.

### Main full-key 대조 및 채널 결과

| 경계 | 이번 경기 인계 |
|---|---|
| Spawn 전달 | server spawn465 ↔ client receive-spawn465, delta0 |
| Client spawn 처리 | receive/dispatch/accepted 각465; vfx-consumed464 |
| 종료 마지막 wave | wave465 game-end expired1, 정상 종료 취소; VFX 누락 결함으로 세지 않음 |
| Contact 전달·표시 | receive/dispatch/accepted/consumed/text-emitter-success 각313 |
| Full-key contact join | deliveryEpoch/wave/impact/target/kind로 server publish-contact313 ↔ client text-emitter-success313, 양쪽 unique313, delta0 |
| Coordinator final | failures/channelFailures/duplicates0 |
| Clock 수신보류 branch | ClockPending/Recovered/Expired 각0; 자연 경기의 pending 회복을 실행한 증거 없음 |
| Clock anomaly | 총2건, 아래 ingame/종료 시점 구분 |
| 오류 검색 | Main의 양쪽 대상 `[ERROR]/Exception/FATAL/ANR` 검색0. 전체 과거 로그·Unity Editor.log 오류0 또는 gameplay/시각 완전성으로 확대하지 않음 |

Full-key 집합 delta0는 단순 총수 일치보다 강한 전달·해당 text 성공 증거다. 이번 인계는 binary wire/full snapshot/replica HP 동등성이나 화면 픽셀·정확 접촉 프레임을 비교한 결과가 아니다. Spawn delta0에 contact와 동일한 unique 수를 임의로 추가하지 않는다.

### Programmer(Bacon) 독립 최종 대조 — Main 인계와 일치

동일 경기 **C3v12/combatSchema3**, device=server/editor=client, epoch443cdc5dde2f8a57 확인. 아래는 Bacon 읽기전용 최종 대조 인계이며 문서 담당의 raw 재검사 결과가 아니다.

- Spawn/receive465 full-key 누락·추가·중복0, spawn 시각 충돌0.
- Publish/receive/양쪽 text313, 각 unique313이며 key 차이0. Contact time/HP/applied/heal/domain 좌표가 **기록 정밀도에서 일치**한다.
- 양쪽 VFX464 **동일 key 집합**, 시각/ParticleSystem 수/playbackActive 충돌0. Wave465는 양쪽 GameEnd 취소이며 정상 종료 취소다.
- Server writer2491 = damage273 + effective heal40 + full-HP zero-heal2178. Writer key 중복0, 기록 HP 차분 산술 오류0, 실제 변화313. Full-HP zero heal은 가짜 표시를 만들 대상이 아니다.
- 실제 ERROR/FATAL/예외0. Device의 `Exception` 문자열11014는 로그 메서드 인자 시그니처이며 **실제 예외11014건이 아니다**. 앞선 Main 검색0은 실제 오류/예외 판정 범위로 읽고, 단순 `Exception` 문자열 hit0로 확대하지 않는다.

**[🔴 2026-10-12 correction — original kept: 앞선 “Exception 검색0” 요약은 실제 예외 판정0으로 한정한다. Device 문자열 hit11014는 로그 메서드 인자 시그니처이며 실제 예외가 아니다.]**

Raw anchors: Device는 `Assets/_Project/Docs/_Logs/2026-10-12/10_10_logcat/RuntimeLog_device.txt`, Editor는 `Assets/_Project/Docs/_Logs/_editor/2026-10-12/RuntimeLog.txt`다.

| 경계 | 제공된 원문 행 |
|---|---|
| Contact 경로 | device2336 → editor1377 → editor1398 |
| 마지막 contact/text | device85423 → texts device85559 / editor18969 |
| GameEnd | device85556 / editor18966 |

이 독립 결과로 이번 경기의 **정상 전달·표시 scoped PASS**를 보강한다. Binary 전체 snapshot/Client replica HP/pixel·정밀 시각/정확 build hash는 미확인이다. 상위 clock 원인 미해결과 **ClockPending 수신보류·회복 실전 분기 미실행 OPEN**은 그대로다. Ingame clock anomaly 회복을 수신 pending 회복 PASS로 바꾸지 않는다. 아래 기존 Main anomaly 인계·과거 evidence는 삭제하지 않는다.

### Clock anomaly와 미실행 경계

- Ingame **10:07:04.698 backward0.376005s → 10:07:05.103 recovered**, 약405ms. 이는 관측 clock anomaly 회복이며 **수신 ClockPending 회복 branch PASS가 아니다**.
- Disconnect **10:09:17 backward43.996686s**는 종료 시점의 별도 anomaly다. 이것만으로 경기 중 전송 손실·damage loss·NGO/device/lag 원인을 단정하지 않는다.
- ClockPending/Recovered/Expired가0인 이번 경기로 승인된 2초 clock 대기·정상2.6 slot·tombstone·bounded retry 회귀를 자연 경기에서 검증했다고 하지 않는다. CLR/native 자동 검증은 이전 별도 증거로 유지한다.
- 정상 전달·표시 scoped PASS는 현재 경기의 해당 모집단만 수용한다. 상위 clock 원인, 수신보류 branch 실전 재현/회복, 정밀 timing/visual/global closure는 OPEN이며 코드 변경·새 Task/TC·빌드 승인이 아니다. 이전 정상 Torrent/개별 수용과 공통 랠리 보류는 바꾸지 않는다.

### 문서 영수증

최종 실제 check_docs89562 **exit0·문제0**. 범위 규칙10/Markdown78/Claude40, _Tasks/_Logs 제외; 별도 현재 wave 링크0 및 scoped PASS/pending 미실행/Exception 시그니처 구분/raw 행 anchor/빌드 hash 미확인 표기 확인. Baseline tool 갱신 exit0: document-manager1213→1217행(+4), 파일2→2, 감소 없음.

이번 수정은 총9파일: 현재 wave Research.md/Plan.md, PROJECT_STATUS.md/ROADMAP.md/WORK_HISTORY.md, .claude/MEMORY.md, .claude/agent-memory/document-manager/MEMORY.md 및 doc-conventions.md, tool-generated .claude/agent-memory/_baseline.json. 신규 Task/TC·status 재설계·메모리 정리·소스/LogRules/LogHistory 변경 없음. 테스트 영수증 최소 추가로 종료한다.

기존 wave Research/Plan, PROJECT_STATUS/ROADMAP/WORK_HISTORY, 공용 MEMORY 및 기존 document-manager 인덱스/토픽만 최소 후속한다. LogRules/LogHistory·old Task·코드/Git/Unity/빌드/ZIP/업로드/삭제는 변경·실행하지 않는다. 검사기 baseline은 tool로 증가만 갱신하고 실제 check_docs 종료 결과와 수정 목록을 별도 추가한다. 이전 전체 manifest는 그 시점 snapshot이며, 이번 문서 변경을 source manifest 재검증으로 읽지 않는다.

위 최신 저장 경기 다음의 구현/native 인계는 이전 단계 이력이다.

### 2026-10-12 fresh native 최종·빌드 시도 인계

### 최종 전체 manifest와 문서 검증 인계

현재 인계 전체 inventory는 **actual code3 + Markdown doc15 + mistakes1 + Unity auto-generated meta5 + tool-generated baseline1 = 25파일**이다. 아래24행과 manifest를 담는 이 Research 자체1개를 포함한다. Self-hash는 순환하므로 표에 넣지 않고 최종 인계 시 별도 측정한다. Code는 programmer 변경이며 문서 담당이 소스를 수정하거나 compile/native/build를 실행하지 않았다. Source3 hash 일치 판정은 Main 독립 대조 인계이고 아래 byte SHA256은 현재 파일 inventory 측정이다.

이번 최종 receipt에서 직접 갱신한 것은 기존 wave Research/Plan·PROJECT_STATUS/ROADMAP/WORK_HISTORY·공용 MEMORY·기존 programmer combat-fixes 및 tool baseline뿐이다. Cleanup Task·다른 과거 Task·README·LogRules/WORKFLOW·.gitignore에는 추가 편집하지 않았다. 그 파일들은 이전 문서 작업의 현재 hash를 inventory에 포함한다. 실수 기록은 이전 요청에 따른 사건·교훈 기록이며 인증값/명령줄은 전재하지 않았다.

Main이 read-only 확인한 meta5는 Unity Asset Refresh 자동 생성물이다. 현재 경로 존재/hash를 별도 inventory로 확인했으며 직접 생성·수정하지 않았다. **LogHistory/Task Markdown 및 대응 meta의 file-tracking 정책을 유지**한다. 실제 Git add/commit·.gitignore·upload·delete 실행은 없다. 이후 빌드 UI/log/process 관찰도 없다.

Baseline 실제 runtime 갱신 exit0: game-programmer3944→3947행(+3), 파일20→20. 감소 없음; baseline 직접 편집 없음. 이 정보와 gate PASS는 real-match retest OPEN/underlying clock root cause 미해결을 바꾸지 않는다.

| 구분 | 전체 파일 | 현재 byte SHA256 |
|---|---|---|
| code | `Assets/_Project/Scripts/Application/Combat/WavePresentationCoordinator.cs` | `493f36933904efc88aa9efb7f5238f7deeb15be119853ee83a6eee8f09367be2` |
| code | `Assets/_Project/Scripts/Presentation/Effects/WavePresentationPresenter.cs` | `4fdf10cfbf40400b5deb4851f7a94e8029d298fe023898bd9aec4774b99dd977` |
| code | `Assets/Editor/Tools/TorrentWaveProductionValidation.cs` | `31a13986f9f8f423072cc560993fa9b71b03156ed725502e3ea5851a9363fd4c` |
| doc | `Assets/_Project/Docs/LogRules.md` | `bef2d4eb705ce45a5168b516acbddb126610177ceed5415469e964f4c9ac5ca1` |
| doc | `Assets/_Project/Docs/LogHistory.md` | `e6ca47458f0d7d0269512a7b11847ab134ae64b84eb545f9a08d35ab1984c09c` |
| doc | `Assets/_Project/Docs/WORKFLOW.md` | `82c4152f92146ee68db97dafcee9880ee37566e6898faef1777ab0b681bfd2ad` |
| doc | `AGENTS.md` | `ff34bc9305b78ca52b9e72bd364758ca5dab55d11c74d77c6a13331fcc9acc14` |
| doc | `Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Plan.md` | `b295d5a1938d0bccd757ea5fa05da4616a1036bcfcb5e435b19fc46bf71cb02e` |
| doc | `Assets/_Project/Docs/_Tasks/2026-10-11/21_50_wave-contact-authority-isolation/Plan.md` | `7b5e5d7f72c1d233bbee21ec8fd263fd7e466f21f26c4d72ddfdd26bda609573` |
| doc | `Assets/_Project/Docs/PROJECT_STATUS.md` | `bffe7dee8aeb8b0a30e068d421d27cd904f32f8d65cbd647ee15864108acd0a2` |
| doc | `Assets/_Project/Docs/ROADMAP.md` | `25cb0b08266ee5dcc1287d852e7ffa4647c6c238fa77f12e73eb8fa5f286ed46` |
| doc | `Assets/_Project/Docs/WORK_HISTORY.md` | `8957bc2edb998ec2d6bf083b165cf82ba62b69fdf2cef98331981966f8deae5f` |
| doc | `.claude/MEMORY.md` | `f7691c2b190665aa0cd1df068454ab0194cfc5e08d595cd470799022eb2e729b` |
| doc | `.claude/agent-memory/document-manager/MEMORY.md` | `eab2cd66d74bbbc2fcc02d5db6de847603e24667451a6df0230f236916dead2a` |
| doc | `.claude/agent-memory/document-manager/doc-conventions.md` | `67beeb9c8367655cb139cb01caa14ae7063cc0c80f17a39061708ac90a4b4933` |
| doc | `Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Research.md` | `11f869642ef4c8fa6e727f9060918bcdaceeaa29c0efc299f07c49b7ec75f3fd` |
| doc | `.claude/agent-memory/game-programmer/combat-fixes.md` | `2e56bf9342f40273bde7d398519bb16da8e65775a5826d1de88ac0a0593b285e` |
| incident | `.claude/mistakes.md` | `0bacd8fe5b0d2d99ac8884047c830ef42bda39967ddff74e9682cd06303126f2` |
| Unity-auto-meta | `Assets/_Project/Docs/LogHistory.md.meta` | `4a9cee2270412ecc4a23a13663bdf852642cb9ea8a08765f79a1e2d8f393f504` |
| Unity-auto-meta | `Assets/_Project/Docs/_Tasks/2026-10-12.meta` | `19cf815014333fadb1d2c28b6ae4a12316dab2cff35a8da75329a677d34281aa` |
| Unity-auto-meta | `Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive.meta` | `4b49834b61f212e18b618dbc9c7e9c246b75cc7a085375200b780eced0b71a73` |
| Unity-auto-meta | `Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Research.md.meta` | `2dbf1bd73b2da12bce9608a5ced00cbdca8b85bd7e7080540230db2c9ecfe7c4` |
| Unity-auto-meta | `Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Plan.md.meta` | `a0f4d52734e0588fd9e296538225b22554b236a7677d20cf5fd4ec6074cd1a03` |
| tool-baseline | `.claude/agent-memory/_baseline.json` | `2ea6ce778e4370d51f506bdb0b2c4891be2785cc7987c9c2e87d6d24f013897c` |

최종 checker는 이 문서 기록 뒤 실행하고 실제 종료 결과를 추가한다. 기존 _Tasks/_Logs 제외 범위를 인정하며 현재 wave Task 링크는 별도로 확인한다.

**최종 실행 확인:** fresh checker52591 실제 종료 **exit0·문제0**. 범위 규칙10/Markdown78/Claude40, _Tasks/_Logs 제외 확인. 별도 현재 wave Task 링크0; fresh UnitAction/Root 시각·빌드 클릭만 관측·runtime OPEN·warnings scope·contact/Spawn 관계 검사 정상 Tick·2초 clock 전용·2.6 slot 보존·25파일 inventory 확인. 이전 “실행 후 추가” 문장은 종료 전 단계이며 이 결과로 supersede한다. 문서 인계 완료가 빌드 완료나 실제 경기 재테스트 PASS는 아니다.


**Actual code 흐름 정합성 인계:** Clock P2의 저장 전 검사는 payload 자체 유효성/epoch/key/identity/capacity 및 dup/conflict다. Cross-snapshot contact-before-Spawn envelope는 양 snapshot available인 정상 Tick에서 검사하는 기존 source 흐름이며, 저장 전 모든 관계 검사가 끝난다는 이전 요약을 의미하지 않는다. Plan을 이 구분으로 정밀화했다. Authoritative result를 새로 만들거나 HP를 재적용하지 않는다.

Main이 programmer 최종 source3 hash와 현재 소스의 독립 일치를 확인했다. Actual CLR RED→GREEN100/100(2.309s), 현재 정적 errors0/warnings Runtime20·Editor0·Development20, Release NOT RUN을 유지한다. 이 인계는 문서 담당의 소스 실행/독립 native 관찰이 아니다.

- Unity Asset Refresh 클릭/import 후 **Runtime DLL 2026-10-12 02:16:56.962, Editor DLL 02:16:57.020** 갱신을 Main이 확인했다.
- 새 Unit Action 실제 실행02:19:29, **UI02:20:22 전체 UAS/Production/C2/C3 PASS**. Main bounded Editor.log 신규 byte 범위 **485268251~762193795**에 TORRENT-WAVE production validation PASS 포함을 확인했다. 해당 로그를 문서 담당이 재독하지 않았다.
- Root SelfValidate 실제 클릭 후 **02:22:11 PASS**, Main UI와 마지막12KB 로그 확인 근거다.
- Console **errors0/warnings999+**, injected stress diagnostics 포함이다. runtime warning0 또는 새 실제 경기 경고0가 아니다.
- 첫02:15 이전 generation PASS는 별도 이력이다. 앞선 약02:14 인계는 그 실행의 근사 시각이며 이번 정확 인계는02:15다. 이전22:13:40 DLL PASS를 새 source PASS로 인정하지 않은 경계는 보존한다. Fresh DLL 이후 위 실행만 새 clock source native 검증이다.
- **2026-10-12 약02:23 KST Build And Run 실제 클릭1회 성공**. File 메뉴 fresh screenshot에 표시된 항목을 Main이 클릭했다. 클릭 뒤 getstate/screenshot/Editor build log/process 관찰은 전혀 하지 않았다. 완료·성공·설치·runtime은 미확정/사용자 소유다.

최종 source/static/CLR 및 fresh native만 scoped 검증 수용한다. **현재 실제 경기 재테스트 OPEN**, underlying clock/NGO root cause·정밀 timing/visual/global closure는 미해결이다. Build 시도는 완료 증거가 아니다. 아래 native/빌드 대기 및 최종 결과 미수신은 당시 단계이며 이 인계로 supersede한다. 기존 정상 수용·공통 랠리 보류는 그대로다. TC/Git/.gitignore/업로드/삭제 실행 없음.


### 2026-10-12 약02:14 후속 — 소스/CLR 확인과 이전 assembly native 분리

Main 인계: 최종 exact3-file source hash 확인 완료. Actual CLR regression **RED→GREEN 100/100, 2.309s**; 현행 정적 errors0, warnings Runtime20/Editor0/Development20. Release NOT RUN. 이전 187.315ms 재현과 구별하는 교정 검사이며 문서 담당의 독립 소스/실행 감사가 아니다. 최종3 hash 값은 이 인계에 제공되지 않았으므로 추정해 기록하지 않는다.

Main 첫 Unit Action은 약02:14 PASS였지만 **Library/ScriptAssemblies DLL timestamp가 이전22:13:40**이므로 새 clock source native PASS로 인정하지 않는다. 이전 assembly PASS 관측만 보존하며 확대하지 않는다. Unity Refresh/import 후 fresh gate 재실행 예정; fresh generation/native 및 후속 빌드 인계는 대기다. 새 native PASS·빌드 완료·runtime/NGO root cause 해결은 주장하지 않는다.

아래 GO 전/최종 결과 미수신은 당시 단계다. 이번 인계는 source/CLR·정적 경계를 supersede하고 fresh native/빌드 대기는 유지한다.

후속 인계: Main 디스크 Plan 확인 후 programmer 구현 GO 전달 완료. 위 제목/GO 대기는 이전 단계이며 새 코드 최종 결과는 아직 수신하지 않았다. P별 규칙 근거는 [최신 Plan](Plan.md)에 명시했다. Receipt 2초는 clock 이상 ClockPending만 제한하며 정상 .25초 이내 future due 대기와 기존 2.6초 slot은 보존한다. Duplicate deadline 미연장·expired ordinal tombstone·정상 raw Tick 재검사를 유지한다. 실제 코드 최종 결과 전 새 PASS를 기록하지 않는다.

Main/programmer가 전달한 실제 source Coordinator + contracts/Team CLR 실행 결과를 기록한다. 문서 담당의 독립 실행·소스 감사가 아니다. **100회 실행, exit0, 187.315ms**이며 Dev/native Time 경로는 사용하지 않았다. 아래의 기존 Time/UniRx 차단은 이전 예외 격리 검사 이력이고, 이번 좁은 CLR 실행 성공과 구별한다.

| 재현/대조 | 전달된 관측 |
|---|---|
| wave79 future | 319.758ms future로 failures1. Tick 시계 회복 뒤에도 기존 입력 VFX0; 동일 payload를 정상 시계에서 재투입하면 VFX1, duplicate0 |
| contact3 backstep | failures3 뒤 기존 입력 text0. 정상 시계 재투입 시 text3, duplicate0 |
| receipt Math.Max만 적용한 대조 | text2만 회복. 마지막 future 시각256.102508ms는 reject1로 남음; 수신 시 high-water만 올리는 수정으로 충분하지 않음 |
| negative controls | 5/5: GameEnd spawn 차단/contact 유지, catch-up .50 만료, baseline, epoch, nonfinite 경계 |

사용한 contact 시각은 로그 소수6자리이며 position/team은 representative 입력이다. 실제 Coordinator 계약의 재현·대조이지 binary wire/full snapshot, 실제 Presenter/native/NGO 실행 또는 underlying NGO root cause 증명이 아니다. 저장 경기에서 서버 HP가 이미 완료된 사실과 Client 표현 누락을 분리하며, 새 runtime 회복 PASS는 없다.

**Main 기술 Plan 승인:** 기존 gate regression RED → coordinator/presenter/기존 production fixture의 정확히 3파일 변경 → 단일 raw Tick 재검사 → bounded 진단의 순서다. Programmer 구현 GO는 이 문서 갱신 확인 후 Main이 별도로 전달하며 아직 적용·hash·정적/native·새 빌드 결과가 없다. 구체 변경 경로/hash manifest는 programmer 구현 인계에서 확정한다. 새 wave Task를 만들지 않는다.

### 승인된 ClockPending·2초 계약

- snapshot valid/epoch/identity/envelope를 검사한 뒤 시간 문제만 ClockPending으로 bounded store에 보류한다. 비정상 payload를 정상으로 꾸미거나 정책을 우회하지 않는다.
- **수신 시 high-water를 전진시키지 않는다.** 단일 raw Tick sample에서만 시계를 판단한다. raw 역행/nonfinite는 보류하고 정상 Tick에서 재검사한다.
- 기존 **.25초 future / .10초 표현 delay / .50초 catch-up**, duration/baseline/envelope를 유지한다. **receipt 단조 시계의 2초는 pending expiry만** 결정하며 gameplay clock·payload 시각·기존 표현 허용치를 대체하지 않는다.
- 승인된 capacity **512/256/8192/4096** 경계를 포함한다. duplicate는 첫 deadline을 연장하지 않고 conflict/expired key를 재활성화하지 않는다. 구체 저장소별 배정·terminal 처리 구현은 최종 manifest/fixture 근거로 확인한다.
- GameEnd에서 spawn pending은 cancel하되 contact는 기존 age 허용 계약을 유지한다. epoch/owner terminal과 pending cleanup을 함께 처리한다.
- pending/recovered/expired는 exact key 및 receipt/raw/previous/payload/future delta/wait age를 bounded 진단으로 구별한다. 무제한·매 Tick spam을 추가하지 않는다.
- 서버/transport/bootstrap/pool/HP 및 기존 contact 예외 격리는 변경하지 않는다. 이전 native PASS·빌드 시작 클릭은 이번 clock 교정 후 실행 증거가 아니다.

아래 23_16 분석의 ‘후속 분석 후보만’은 당시 단계다. 이번 인계로 **기술 계획 승인/구현 GO 대기**까지 진행했으며 원인·정밀 시각·전체 runtime/global closure는 계속 OPEN이다. 기존 native/빌드 이력은 삭제하지 않는다.

> **2026-10-11 최신 실행 인계:** Main Unit Action22:16:40 KST 전체 PASS(새 TORRENT-WAVE 포함), Root self-validation22:21:12 PASS(Editor.log line2546935); Console errors0/warnings50. 약22:22 Build And Run 한 번 클릭, 이후 완료 감시 없음·완료 사용자 소유. Release NOT RUN·독립 CLR 차단(no PASS) 유지. 아래 구현·정적 단계의 native/빌드 미실행 기록은 당시 이력이며 새 native 실행으로 supersede한다. 실기/global closure는 아니다.

## 2026-10-11 23_16 저장 경기 분석 — 서버 적용과 Client 표현 분리

Main 직접 분석·streamed exact-key 대조 인계이며 문서 담당은 파일 경로/크기만 확인했다. 경기 약22:58–23:02, epoch `450ae1b5a0c0e7b7`, shared session `2d4e7d00...`, Host run `7ca52a...` / Client `f44ac...` (공급된 접두부만 기록; 전체 ID 추정 없음). [Android Host 원본](../../../_Logs/2026-10-11/23_16_logcat/RuntimeLog_device.txt) 35,580,884B/header23:16:59, [Editor Client daily](../../../_Logs/_editor/2026-10-11/RuntimeLog.txt) 41,820,521B.

- **서버 처리:** device final96547–96559 faults0/coverage complete, spawn272/end269/network despawn 시 active3. Writer-applied/event-after/apply-return1398씩. HP damage238+heal43=281 양수 contact 발행; FullHP1117은 skip, 효과를 꾸며내지 않았다.
- **Contact exact-key 대조 최종 보강:** server send ↔ Client receive281 unique씩, duplicate0/missing0/extra0. 로그 필드 contact(소수6자리), hp, applied, heal281/281 일치. binary float/full snapshot/gameplay replica HP 전체 동등성 증거는 아니다. Spawn sent↔received/dispatched272, contact received/dispatched281은 별도 수치다.
- **Client 실패4:** payload valid인 wave79 spawn은 firstFailedFuture now179.433590/spawn179.753349, 약319.758ms ahead로 기존250ms 한계 초과. 23:02:38 wave242/ord1/building11, wave242/ord2/building13, wave243/ord1/building13 contact3은 previous264.217106 대비 now264.050035/264.150397의 약167.071/66.709ms backstep으로 거부됐다.
- **서버 HP와 Client 표현 분리:** 실패 contact3의 서버 HP2000→1800,1200→1000,1000→800(각200damage), apply-return/send 확인. Client clock recovery2와 별개로 `WavePresentationCoordinator.ValidateClock/Accept` 실패 후 caller return이 contact를 drop한다. 서버 피해 손실이 아니며 underlying NGO/기기/lag 인과는 입증되지 않았다. Server text281 / Client text278.
- **VFX 총수와 key 집합:** 양쪽 success271이지만 same-key equality가 아니다. Server wave79 success/Client79 spawn reject, Server272 game-end expired/Client272 success(Editor217605–217606). Server272 expiry1은 game-end/age0.121293/channelFailedFalse의 별도 정상 teardown이다.
- **오류·회복 모집단:** Main의 현재22/23h Editor ERROR 및 Client23h ERROR/actual NullRef/InvalidOp/Unhandled/Fatal/GameplayContinues/PresentationDegraded 검색0. 이전20:27 ERROR는 역사이며 전체 파일 error0로 확대하지 않는다. 새 WaveContactFault/GameplayContinues/PresentationDegraded 기록이 없어 예외 격리 경로의 자연 실행/runtime fault-recovery PASS는 미입증. 이전 native fixture PASS는 별도 증거로 유지한다.
- **Generic 비교축:** 양쪽 C2/C3 results/ready/emits92, failure/reject0/overall EVIDENCE는 전체 wave PASS가 아니다.

**판정:** 서버 적용·지정 contact 필드 전달은 관측 범위 일치, Client 표현 clock FAIL/OPEN 및 VFX key 비대칭은 분리 유지. 전체 wave runtime/정밀 시각/사용자 수용/자연 예외 복구 closure 없음. Source2/native 이력·정상 Torrent/6종 수용·공통 랠리 후순위 유지. 후속 clock/scope는 분석 후보만이며 코드/허용치/추가 빌드/old Task/로그 보관·정책 변경 없음. 빌드 완료는 감시하거나 역추정하지 않는다.

## 최신 native 검증·빌드 시도 인계 — 2026-10-11 (저장 경기 전 이력)

Main의 sky UI 실제 실행 인계: **2026-10-11 22:16:40 KST Unit Action 전체 gates PASS**, 새 실제 코드의 **TORRENT-WAVE production validation PASS 포함**. **22:21:12 KST Root cross-audit self-validation PASS**, 근거 Editor.log line2546935. Console errors0/warnings50이며 intentional fixture 경고를 포함한다(경고0 증거가 아님). 이는 native 자동 검증이며 새 멀티플레이 실기/정밀 시각/공식 경기 CrossAudit 완료가 아니다.

Main이 약 **22:22 KST File 메뉴 Build And Run을 한 번 성공적으로 클릭**했다. 빌드 시도만 확인했으며 이후 screenshot/log/process/완료 확인은 사용자 지시대로 하지 않았다. 빌드 성공·완료·설치·실기 결과는 미확정이고 완료 확인은 사용자 소유다. Release NOT RUN·독립 CLR Time/UniRx 차단(no PASS)은 그대로이며 로그 보관·정책 변경 없음.

**위 결과가 아래 구현·정적 인계의 native/빌드 대기와 fixture 미실행 문장을 supersede한다.** 기존 USER STOP→재시도/timeout→새 native PASS→빌드 클릭 이력은 보존한다. 문서 담당은 새 UI/로그 검사를 하지 않았으며 Main의 실제 실행 인계를 기록했다.

## 최종 구현·검사 인계 — 2026-10-11 (native 실행 전 이력)

문서 담당의 새 소스/UI 감사가 아니라 programmer 결과와 Main 독립 확인의 인계다. 예방적 예외 격리이며 정상 Torrent 개별 테스트 수용을 철회하거나 실기 재현 결함의 해결을 선언하지 않는다.

| 적용 파일 (리포지토리 루트 기준) | 최종 SHA256 — Main 독립 일치 |
|---|---|
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.WavePresentation.cs` | `4A97A17C960CEFFD621689536A4EDAC73D7F488CE767F3848F5E0060DAF9A9D7` |
| `Assets/Editor/Tools/TorrentWaveProductionValidation.cs` | `FBF77092302C81B1F2F67963FFDC8C0FCA22CFA50EA0AF1A3038035C58616D24` |

- Contact clock 조회/진단/각 subscriber 예외를 좁게 격리하고 실제 gameplay clock은 그대로 둔다. HP 적용 → 사망 전 불변 snapshot 발행 → 사망·제거 순서 및 남은 대상 처리를 유지한다. 표현 실패를 이유로 피해·회복을 재시도하거나 다른 clock으로 대체하지 않는다.
- Operations 예외 기록은 최종 catch에서 bounded context와 함께 한 번, 개발 fault와 구분한다. 기록 자체의 guard는 비재귀적이다. 로그 보관·정리 정책/규칙 변경은 없다.
- 기존 fixture는 callback 내부 Require를 밖으로 옮기고 clock throw, throwing/healthy handler, lethal removal, multi-victim 및 damage/heal once 경계를 준비했다. 이는 소스 구현 근거이며 해당 fixture의 native/CLR 실행 PASS가 아니다. Main은 source isolation과 fixture2의 부분 소스 검토를 수행했다.

| Programmer 보고 — Unity 6000.3.5f2 현재 정적 검사 | exit | errors | warnings |
|---|---|---|---|
| Runtime | 0 | 0 | 20 |
| Editor | 0 | 0 | 0 |
| Development | 0 | 0 | 20 |

Release는 NOT RUN이며 stale rsp를 현재 성공으로 사용하지 않는다. 독립 CLR 실행은 Time/UniRx 제약으로 차단되어 PASS가 없다. Main은 보호 Game (`6BBFE..`), Stats (`0A907..`) 불변을 독립 확인했다(인계의 축약 hash이며 완전한 hash를 추정하지 않는다). 피해·회복·사거리·속도·타이밍·VFX·Stats·씬은 변경 범위 밖이다.

Native/빌드는 아직 실행되지 않았다. 앞선 physical Escape USER STOP을 보존하며, 이후 사용자가 modal을 닫고 Main CU 재시도를 승인했다. 가벼운 목록 조회는 성공했지만 activate timeout으로 Main 복구 중이다. 이는 validation/build 성공이나 영구 실패 판정이 아니다. 이번 문서 동기화는 UI를 사용하지 않았다. 최종 native/빌드 관측은 후속 실제 인계 전까지 대기이며 runtime·정밀 시각·global migration closure는 주장하지 않는다.

## 1. 승인과 현재 단계 — 문서 생성 당시 이력

- 사용자: 새 Research/Plan 작성 후 즉시 구현·검사·빌드 시도 승인. 현재는 두 문서 작성 단계이며 Main의 기술 문서 검토와 코드 GO 전이다.
- 생성 기준: 2026-10-11 21:50 KST. 요청의 21:44 이후 실제 시각을 확인하여 폴더를 `21_50_wave-contact-authority-isolation`로 생성했다.
- 현재 구현·정적 검사·native 검증·빌드 시도는 수행하지 않았다. [계획](Plan.md)은 예정 작업이며 실행 결과가 아니다.
- 이전 Task, TC, QA/새 에이전트, Git, 로그 보관·정리 정책 변경은 범위 밖이다. 상태·메모리 동기화는 최종 구현 인계 후 별도 지정 범위에서 한다.

## 2. 직접 읽은 실제 경로 — 구현 전 소스 감사 이력

아래 구조와 위험 설명은 구현 전 읽기 기록이다. 현재 변경·검사 판정은 상단 최종 인계로 대체하며, 이 표의 미격리 설명을 현재 코드 상태로 읽지 않는다.

경로 기준은 리포지토리 루트다. 아래는 문서 담당의 직접 소스 확인이며 programmer의 후속 실행 결과가 아니다.

| 소스 | 확인한 경계 |
|---|---|
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`, `ApplyDamageToVictim` | HP 변경 → 기존 공격/HP 이벤트 → `PublishWaveContact` → 사망 이벤트 → 유닛/건물 제거. contact 발행은 사망 전에 있다. |
| 같은 파일, `ApplyHealToUnit` | 생존·양수 회복 검사 → `Heal` → 기존 HP 동기화 이벤트 → contact 발행. |
| 같은 파일, `TickWaves` | 권위 `WaveServerTime`으로 전선을 진행하고 대상을 선수집한다. 유닛/건물 hit-set을 먼저 기록한 뒤 적 유닛 피해·아군 유닛 회복·적 건물 피해를 순회한다. |
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.WavePresentation.cs`, `PublishWaveContact` | 적용량·delivery key 검사, contact ordinal 증가, `WaveServerTime` 읽기, 진단 기록, `WaveContactConfirmed` multicast 직접 호출. 발행 전체/개별 구독자 격리가 없다. |
| `Assets/_Project/Scripts/Application/Combat/WavePresentationContracts.cs`, `WavePresentationContact` | readonly snapshot: key/ordinal, 피해자 ID·종류·팀·위치, 결과 HP·적용량·heal·접촉 시각. finite 시각·양수 적용량 등 유효성 검사를 유지해야 한다. |
| `Assets/_Project/Scripts/Application/Combat/WavePresentationDiagnostics.cs`, `Record`/`ObserveFault` | 이미 내부 catch가 있다. 진단 본체가 무방비라는 뜻은 아니다. 호출 인자 준비 및 contact 발행 경계까지 보호되는지는 별도 축이다. |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.Waves.cs`, `SendWaveContact` | `WaveContactConfirmed` 구독자가 snapshot을 RPC로 전달한다. 확정 contact는 `_combatStopped`만으로 버리지 않고 기존 HP 동기화와 분리한다. 이 파일 변경은 계획하지 않는다. |
| `Assets/Editor/Tools/TorrentWaveProductionValidation.cs`, `ValidateServerClockAndWriter` | 실제 `SpawnWave`/`TickWaves`의 피해·회복·한 번 적용을 검사한다. 현재 callback 내부 `Require`가 있어, 예외 격리 후에는 assertion을 밖에서 평가해야 검증 실패가 숨지 않는다. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`, `TorrentWaveProductionValidation.Run()` 호출 | 기존 Unit Action self-validation에 위 production fixture가 연결돼 있다. 새 TC/별도 검증 실행 체계를 만들지 않는다. |

## 3. 위험과 보존 계약

contact용 시계 읽기가 throw하면 HP는 이미 변경됐지만 사망·제거까지 반환하지 못할 수 있다. multicast 구독자 하나가 throw하면 이후 구독자와 현재 피해자의 후속 처리, 같은 파도의 다음 대상이 중단될 수 있다. hit-set을 이미 기록한 경로이므로 gameplay를 다시 실행하는 재시도는 해결책이 아니다. 이것은 호출 구조에서 확인한 위험이며 실제 경기 발생 건수나 원인 증명은 없다.

정상 시계에서는 기존 서버 접촉 시각과 snapshot을 그대로 사용한다. contact 시계가 실패하면 임의 로컬 시각·0·NaN을 정상 시각으로 위조하지 않고 해당 표현 발행을 fail-closed한다. **gameplay의 `TickWaves` 시계·전선 계산은 바꾸지 않는다.** 이미 적용한 HP를 취소·재적용하거나 표현 성공까지 사망을 기다리지 않는다.

진단 실패는 유효 contact 전달을 막지 않는다. 각 구독자의 실패는 다른 구독자 호출을 막지 않는다. snapshot 발행 시도는 계속 사망·제거 전이며, 사라진 피해자 View/Factory를 재조회하는 발행으로 바꾸지 않는다. 전역 GameEvents 예외 정책이나 authority 이벤트 자체의 예외 처리는 이번 범위가 아니다.

## 4. 규칙 근거와 후속 인계

- [전투 동기화 규칙](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md): NET-AUTH-001/002, NET-TIME-001/002, NET-DELIVERY-TRAVELING, NET-PRESENT-003, NET-ACTION-IDEMPOTENT. 서버 결과와 표현은 분리하고 확정 결과의 불변 snapshot을 보존한다.
- [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md): 규칙 18·19·26·29·30. 서버 판정, 대상별 contact, 한 번 적용 및 피해·회복 채널을 보존한다.
- [로그 규칙](../../../LogRules.md): §1.2·1.3·1.8·1.9·1.14. 최종 catch에서 예외 원인을 한 번 기록하며 로그 자체가 gameplay로 예외를 재전파하지 않게 한다. 보관 정책 수정은 하지 않는다.
- Programmer 최종 기술 인계: 변경 예정 소스는 `UnitCombatUseCase.WavePresentation.cs`와 `TorrentWaveProductionValidation.cs` 두 파일뿐. contact clock/diagnostic/구독자별 catch, bounded context의 Ops 예외 기록과 Dev fault, 비재귀 logging guard. production fixture는 clock throw, throwing+healthy subscriber, lethal removal, 다중 대상·피해/회복 한 번 적용을 검사한다.
- Programmer 도구 인계상 현재 Unity `6000.3.5f2`의 Runtime/Editor/PDev rsp가 사용 가능하다. stale Release 결과는 현재 결과로 주장하지 않는다. Main native 접근은 가능하나 로그 저장 modal은 사용자 직접 닫기 대기다. 이 문서 담당이 실행·UI 확인한 사실은 아니다.

피해·회복 수치, 범위·속도·접촉/발사 timing, VFX·clip·Stats·scene, Unresolved/Legacy 분류는 보호한다. 기존 Invalid/original63, poison 시간축, 공통 랠리 등 다른 잔여 문제를 이 작업의 해결 범위로 확대하지 않는다.
