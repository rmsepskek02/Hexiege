# Plan — TorrentSpirit 서버 파도 접촉·피해·회복 진단 로그

> **최상단 현행 판정 — 2026-10-02 최종 인계로 supersede:** 아래 “원인 추적 중/작성 승인 대기/최종 원인 인계 후 추가”는 인계 전 이력이다. 확정 전달 누락0·만피 정상이나 텍스트 적시성/정확 육안 수용·전체 OPEN/C3 Unresolved 유지. 서버 공간 상태 불일치→Fatal→종료 거부→후속 이동 취소 확정, 건물 랠리 TerminalContact 계약 충돌은 직전 성공 프레임 미기록·재현 미수행의 유력 근본 원인이다. 서버 MOVE FAIL14/30(메인 fatal regex 집계 unique7/15), ROOT PASS는 목표 진행 보장이 아니다. 원문을 삭제하지 않으며 현재 상세는 아래 「2026-10-02 이동 최종 분석」을 따른다. 메인이 TerminalContactAccepted 분류 원문(1479 부근)도 직접 확인했다. 권장 후속은 구현 승인 아님; 추가 조사·교정 없이 이번 문서 반영을 마친다.

## 2026-10-02 12:59~13:08 역할교대 두 경기 최신 요약 — 전달 정합 / 적시성·전체 OPEN

이번 두 경기는 파도와 확정 피해·회복 숫자가 양쪽에 빠짐없이 전달됐는지를 확인한 결과다. 사용자는 VFX가 어느 정도 일치해 보였지만 정확한 확인은 어려웠고 두 번째 경기에서 멈춤을 발견했다. **메인의 최종 exact-key 분석 인계**를 반영하며, 문서 담당자가 전체 로그를 재집계하거나 새 구현·QA를 수행한 결과가 아니다. 아래 53파도 경기와 실기 전 기록은 별도 경기·당시 이력으로 보존한다.

| 구분 | 역할·시간 | sharedSessionKey | wave epoch | 로그 |
|---|---|---|---|---|
| 1차 | Editor Host / Android Client, 12:59~13:03 | `b47df4545ff053c426b13d6a84033f9e378fcba5c37258b688711f862e5b70aa` | `40c733ed4f786813` | Editor `Assets/_Project/Docs/_Logs/_editor/2026-10-02/RuntimeLog.txt` 해당 시간 / device `Assets/_Project/Docs/_Logs/2026-10-02/13_03_logcat/RuntimeLog_device.txt` |
| 2차 | Android Host / Editor Client, 13:04~13:08 | `aab0822e2bf8d0539833764f28f1b9705663e55d984f5f0b79767288cbde53c5` | `491a032ae783db99` | device `Assets/_Project/Docs/_Logs/2026-10-02/13_08_logcat/RuntimeLog_device.txt` / 同 Editor 일일 파일 13:04 이후 |

- **확정 전달 정합:** 1차 wave147 / publish291(피해259·회복32), 2차 wave176 / publish292(피해263·회복29). `epoch/wave/ordinal/target/kind` exact-key join에서 Client receive와 양쪽 text 각각291/292 unique이며 missing/extra/conflict0. Client VFX 성공 exact-wave join도147/176, missing/extra0. 이는 확정 전달·emitter 성공 범위이지 픽셀 접촉 시각 수용이 아니다.
- **만피 정상:** zero-delta 1차60·2차118 모두 fullHP 회복, badZero0·duplicate writer0. 과거 원인 미확정11건을 소급 확정하지 않는다. 양 경기 양쪽 최종 reject/Failures/channelFailures/Expired/pending/fault/overflow는 각각0.
- **표현 시간(모두 ms):** 기존100ms 지연을 이미 제외한 추가 잔차의 1차 Server max46.370/p95 25.667, Client max33.272/p95 31.033; 2차 Server max83.913/p95 33.390, Client max349.511/p95 13.828. 2차의 큰 지연2건은349.511ms·249.849ms이며 나머지290건 최대21.834ms다. “20ms 초과2건” 또는 “모두 몇십ms로 정상”이라고 쓰지 않는다. 권위 접촉→emitter 총시간은 큰2건 각각449.511ms·349.849ms로, 추가 잔차와 혼용하지 않는다.
- **2차 큰 지연의 결속:** wave18 ordinal1/target134와 ordinal2/target140은 모두 hp0/applied100. Editor Client `Assets/_Project/Docs/_Logs/_editor/2026-10-02/RuntimeLog.txt:23177`·`:23179`에서13:05:32.694 text-emitter-success를 직접 확인했다. 메인 추적 인계상 ordinal1은13:05:32.152 receive/dispatch/accepted(client clock92.341518, contact92.401731), ordinal2는13:05:32.659 receive(client clock92.385268, contact92.501393);13:05:32.694 scheduler clock92.851242에 함께 소비됐다. **미수신·표현 누락이 아니라 이미 수락된 결과의 소비 지연**이다. Client main-loop stall 대 동기 clock 갱신 원인은 미확정이고 FPS/profile 근거가 없어 텍스트 적시성 완전 PASS·렉 원인 확정은 금지한다.
- **서버 spawn 이후 적용:** 1차0.050808428~0.599394715초, 2차0.033624084~0.583872416초. 이를 정확 raw particle 접촉 시각 증명으로 확대하지 않는다.
- **종료 경계:** 1차 server147파도 중 travel-end145, active2는 최종 clear. Coordinator cleanup5에도 contact pending0이며 cleanup5를 텍스트 누락5로 분류하지 않는다.
- **이동 별도 미완 단락:** 양 경기 서버 권위 MOVE terminal은 FAIL, 원인 추적 중이다. local ROOT observer PASS는 MOVE 성공이 아니다. 2차 첫 Fatal13:04:59.679와 위 소비 지연13:05:32.694는 별개 사건이며 인과 관계를 주장하지 않는다. 최종 이동 원인은 별도 인계 후 추가한다.
- **메인 추가 확정 — 이동 terminal/ROOT 경계:** 1차 Editor Host spatialFatalPreflights14, 2차 Android Host30이며 두 서버 END verdict FAIL. Client MOVE terminal EVIDENCE는 서버 PASS가 아니다. ROOT는 양 경기 PASS(1차 server stable22/client19, 2차 양쪽39)지만, 메인 소스 검토 인계상 `UnitRootPoseConsistencyObserver.cs:249~289`는 simulationRoot→visualRoot 위치·회전 정합, `:820~824`는 coverage/error 판정이지 정상 목표 진행·서버 이동 수락 보장 검사가 아니다. ROOT PASS로 사용자의 멈춤을 부정하지 않는다. 멈춘 사용자 유닛 종류는 확인 질문 응답 대기이며, 최종 원인 기록은 메인 검토·작성 승인 뒤 별도 추가한다.
- **현행 판정:** 확정 전달 누락0·만피 회복 정상만 확인. 사용자 육안 “어느 정도 일치하지만 정확 확인 어려움”을 정확 시각 PASS로 확대하지 않는다. 전체 OPEN/C3 Unresolved, raw particle 윤곽·정확 VFX 접촉 시각·성능·텍스트 적시성 및 기존 payload/fallback-final 미완 경계 유지. Dev 진단은 task closure까지 보존한다. 코드·원본 VFX/clip·규칙·게임 수치 변경, 신규 Task/TC/QA/Git/build 없음. 정합성 검사와 baseline은 메인이 수행한다.

### 2026-10-02 이동 최종 분석 — 확정 실패 경로와 유력 근본 원인 구분

앞의 “원인 추적 중/최종 승인 대기” 문장은 인계 전 이력으로 보존하고 **현재 진단은 이 절**을 따른다. 메인의 source+raw-log 최종 검토 인계와 문서 담당자의 일부 원문 대조에 근거한다. **서버 공간 상태 불일치 → Fatal → 종료 거부 → 후속 이동 취소는 확정**이며, **건물 랠리 종점의 TerminalContact 계약 충돌은 유력 근본 원인**이다. 직전 TerminalContact 성공 프레임 raw data가 없고 재현도 수행하지 않아 완전한 runtime causal 증명으로 확대하지 않는다.

- **로그에서 확인된 범위(사용자가 본 유닛과 동일하다는 뜻 아님):** 1차 Editor Host의 Pistoleer7개 unitId24/28/34/39/46/51/57, 각 최초 프레임 Fatal+cleanup Fatal2건으로14. 2차 Android Host의15개는 LittleKnight129/131과 Pistoleer147/150/153/156/162/165/169/172/176/179/185/188/192이며 각2건으로30. 사용자 멈춤 유닛 종류는 미확인. 서버 MOVE END는 둘 다 FAIL이고 Client EVIDENCE/양쪽 ROOT PASS는 이를 뒤집지 않는다.
- **확정 불일치 증거:** 2차 `Assets/_Project/Docs/_Logs/2026-10-02/13_08_logcat/RuntimeLog_device.txt:1379`의13:04:59.679 unit129는 committedPosition(8,-2), rootBeforeHex/candidateHex(9,-2), InvalidInput/Fatal이다. 같은 파일1594행 stable3.083초의 simulation/visual pose 정합 뒤1597행은 movementPhase=Move 잔존이다. 문서 담당자가 이 세 행을 직접 확인했다. 멈춰도 두 Root가 같은 위치이면 ROOT PASS가 가능하다.
- **선행 랠리·건물 결속(메인 raw-log 인계):** 같은 device 파일969행 배럭39 rally(0,3),1025행 배럭38 rally(9,-2),1274행 RedTrainingCamp46(9,-2),3243행 RedGunsmith50(0,3),1335행 LittleKnight129 spawn(7,-2). 1차 Editor `Assets/_Project/Docs/_Logs/_editor/2026-10-02/RuntimeLog.txt:5734` Gunsmith24(7,15),6186행 rally(7,15),6814행 Pistoleer24 Fatal13:01:08.985. 이 결속은 건물 좌표 종점 가설을 지지하지만 직전 성공 프레임 자체를 대체하지 않는다.
- **소스 계약 충돌:** `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs` planner는 sampled start tile≠committed tile이면 InvalidInput(1551 부근), non-walkable TerminalContact에서는 terminalReached 뒤 continue하여 공간 전이를 추가하지 않는다(1616 부근). `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`는 Root 위치를 적용(1503 부근)하되 spatialTransitionCount>0일 때만 domain spatial commit(1525 부근)한다. 따라서 Root만 건물 종점으로 진입하고 domain committed tile은 인접 walkable tile에 남는 충돌 경로가 존재한다. 다음 공간 planner가 시작 불일치를 Fatal로 거부하고 cleanup의 현재 Root 재검사도 Fatal이다. ProductionTicker는 path가 null일 때만 빈 타일 fallback을 사용하고, HexFlowField는 non-walkable 최종 목적지를 경로에 덧붙인다(메인 확인). UnitView의 Rejected fail-closed cleanup은 navigation objective를 Cancel하고 invokeCompletion=false로 랠리 뒤 적 성 이동 콜백을 막는다. planner/Root/commit guard/cleanup은 문서 담당자도 해당 원문을 대조했다.
- **합치지 않는 별도 사건:** 1차 EmptySameTile112 등은 Root=goal인0거리 경로 거부이며 이번 committed/Root 불일치와 같은 원인으로 묶지 않는다. 2차 첫 이동 Fatal13:04:59.679와 wave18 text 소비 지연13:05:32.694도 별개이며 causal 관계/FPS 성능 원인은 미확정이다.
- **권장 후속(미구현·새 구현 승인 아님):** 건물 목표와 실제 walkable 이동 종점 분리, Root/domain 공간 일관성 유지, 거부 시 안전 종료 상태 및 후속 이동 계약 교정의 검토가 필요하다. 구체 설계·교정·재현·회귀 검증은 별도 승인 대상이며 이번 Plan 추가를 새 구현 승인으로 읽지 않는다. 코드·프리팹·Unity/build·신규 Task/TC/QA/Git 수행 없음. 전체 OPEN/C3 Unresolved·MOVE FAIL 유지.

## 2026-10-02 최신 테스트 — 확정 접촉 전달·표시 정합 / 전체 OPEN

이번 경기는 실제 파도별 접촉과 피해·회복 숫자 전달이 빠짐없이 이어졌는지 확인한 결과다. **근거는 메인의 최종 읽기 전용 분석 인계**이며 문서 담당자의 별도 실측·새 구현 결과가 아니다. Editor `Assets/_Project/Docs/_Logs/_editor/2026-10-02/RuntimeLog.txt`와 device `Assets/_Project/Docs/_Logs/2026-10-02/02_09_logcat/RuntimeLog_device.txt` 중 **epoch `463c2fb49e473595`만** 결속한다(기기 파일에 이전 경기 포함).

- **파도·확정 접촉 전달:** wave53 spawn과 Client 수신/VFX 재생 성공 각각 exact wave ID 누락·초과0. writer176=피해111 HP변화+회복33 HP변화+회복32 만피. publish144=피해111+회복33이며 `epoch/wave/ordinal/target/kind` exact key 기준 Client 수신·Host text·Client text 각각144unique, 누락·초과·applied/HP/heal 충돌0.
- **무변화의 원인:** noDelta32 전량 `effect=heal/zeroReason=fullHP/before=after=maxHp`(예:1000→1000), badZero0. wave/target/kind 중복 writer0, writer immediate-after↔event-after mismatch0. 이번32건을 신규 미적용/누락 버그로 판정하지 않는다. 이전 capped 경기11건은 원인 미확정 이력 그대로다.
- **표현 채널:** 양쪽 coordinator Failures/channelFailures/Expired0, reject0·최종pending0·helper fault/overflow0. text는 기존100ms 표현 지연에 더해 Host 최대37.781ms/Client31.201ms다. delay를 이미 뺀 presentation lag는 각각 .037781/.031201초이며 HP→text 총시간을 그 잔차만으로 쓰지 않는다. 로그의 재생/텍스트 성공은 정확 픽셀 접촉 시각·육안 수용의 증명이 아니다.
- **접촉 분포·경계 진입:** spawn→적용 .056048724~.580537604초(힐 포함), 생성 순간 일괄 적용 근거 없음. wave7 target13은 spawn97.3595595, t97.7508409 lateral1.630>1.5로 밖, t97.9400971 lateral1.461/p1.767/front3에서 collected·HP500→300·publish ordinal3. wave40 target49도 outside1.588→collected1.493. 전선이 지난 직사각 영역에 옆에서 진입하면 적용되는 현행 Units 규칙 29 사례이지 얇은 전선/swept 판정 교정 근거가 아니다. 늦은 적용 잔차를 네트워크 지연/버그로 단정하지 않는다.
- **종료·관측 한계:** 서버 완주 elapsed min.501795/max.592160/평균.555213887초는 틱 표본이며 렉 원인/성능 검증이 아니다. 양쪽 ROOT summary-END PASS/errors0/drop0이나 공격 observer는 EVIDENCE·Host5/25 범위라 전체 공격 PASS가 아니다. Client detach clock813.643 대 network despawn172.150은 종료 후 clock 전환 관측이며 정상 경기중 전달 사건과 혼동하지 않는다.
- **출력 근거:** 신규 WAVE 행 server5089/client1903, 관측 본문 최대530 UTF-8 bytes(`[INFO]/[WARN]`부터, 기기 Logcat 접두·시각 제외), 파일 내 parts>1 없음. 이는 전체 행900byte 상한 실측 보장/분할 경계 실행 검증이 아니다. 이전658 메인 fixture와699/400 programmer fixture는 여전히 별개 정적 근거다.
- **현행 판정·다음 범위:** 이번 경기의 확정 접촉 전달/텍스트 표시 정합과 만피32건 정상은 확인됐다. raw particle 접촉 좌표/정확 윤곽은 미기록이므로 geometry 일치·정확 VFX 접촉 시각·성능 및 사용자 정확 시각 수용은 미확정, **전체 OPEN/C3 Unresolved** 유지. Client normal/snapshot payload 직접 구분·single fallback server-owner final 경계도 유지한다. 상세 Dev 로그를 task closure까지 보존하고 후속 테스트의 근거 있는 경로만 교정한다. 이번 문서 갱신에 code/Git/build/Unity 메뉴/QA/TC/새 Task 수행 없음.

**아래 구현·컴파일·실기 전 표현은 당시 이력으로 보존하며 최신 실기 상태는 이 절을 따른다.**

> **2026-10-02 최종 현행 — 로그 구현·byte-format 정적 재컴파일 인계 완료 / 전체 OPEN·C3 Unresolved:** G17·UTF-8 초과 guard·clock domain 교정 완료. 944 반례/658 메인 계산/699·400 별도 fixture는 끝의 최종 확정 절에서 구분한다. Unity/device 미실행·build 미시도·정확 시각 수용 OPEN, gameplay/원본 VFX·clip 불변.

> **이전 단계 요약 — 이력 보존:** **2026-10-02 현재 단계 — 전 경기 사건 로그 구현 인계 / 추가 컴파일 근거 검토 대기 / 전체 OPEN·C3 Unresolved:** 32 관측 cap 제거·causal 무샘플링 구현이 인계됐다. 최신 결과와 미완 범위는 끝의 2026-10-02 절을 따른다. Unity 메뉴·빌드·새 실기 및 정확 시각 수용은 미수행/OPEN이며 gameplay·원본 VFX/clip 불변이다.

> **이전 단계 요약 — 이력 보존:** **2026-10-01 현재 단계 — 전 경기 사건 중심 로그 계획 작성 / 구현은 메인→game-programmer 인계:** 기존 Research/Plan 갱신 후 로그 구현을 사용자가 재승인했다. 32 관측 cap 제거·확정 접촉 무샘플링·제외 전이 기반 추적이며 gameplay/수치 불변이다. 두 경기 실기 이력·정확 시각 수용 OPEN·전체 OPEN/C3 Unresolved 유지. 최신 계획은 끝의 승인된 로그 절을 따른다.

이번 계획은 매 테스트 경기의 모든 TorrentSpirit 파도와 확정 접촉을 빠짐없이 기록하고, 제외 사유는 같은 상태를 매 틱 반복하지 않고 변화할 때만 남기는 로그 확장이다. 피해·회복이 언제 결정됐고 실제 파도·텍스트 재생은 성공·실패·만료 중 무엇이었는지 연결한다. 서버 권위·판정·HP·VFX·클립 수치는 바꾸지 않는다. 계획 후 구현이 인계됐으며 새 실기/정확 시각 수용 전까지 전체 OPEN이다. 실제 결과와 남은 범위는 2026-10-02 최신 절을 따른다.

> **이전 서두 — 구현 인계 전:** 이번 계획은 매 테스트 경기의 모든 TorrentSpirit 파도와 확정 접촉을 빠짐없이 기록하고, 제외 사유는 같은 상태를 매 틱 반복하지 않고 변화할 때만 남기는 로그 확장이다. 피해·회복이 언제 결정됐고 실제 파도·텍스트 재생은 성공·실패·만료 중 무엇이었는지 연결한다. 서버 권위·판정·HP·VFX·클립 수치는 바꾸지 않는다. 계획 후 구현은 메인이 프로그래머에게 위임하며 이 단계는 두 Task 문서 갱신만 수행한다.

> **이전 서두 — 로그 확장 승인 전 이력:** 이번 작업은 화면에 보이는 TorrentSpirit 파도와 피해·회복 텍스트를 서버가 확정한 파도 및 대상별 접촉에 맞추는, 사용자 승인된 교정이다. 서버의 실제 생성 위치·방향·시각을 기준으로 양쪽 화면에서 파도와 확정 접촉 텍스트를 같은 표현 시계로 재생하고, 생성 이전 시간을 전선 진행량에 포함하지 않도록 고친다. 권위 HP는 즉시 적용하며 폭 3 WU·길이 3 WU·이동 시간 0.5초와 대상별 1회 판정, 기존 발사 클립 값 및 원본 VFX 프리팹은 유지한다. 표현의 폭·높이·선두 보정은 실제 파티클 측정에 근거한다. 구현은 진행 중이며 **최종 측정·Unity 결과와 구현 검토 전까지 OPEN**이다. 전체 HP바·사망·공격 애니메이션 동기화나 Supported migration 완료를 뜻하지 않는다.

**17:59:49 현재 요약:** production X=.78·FrontAnchor=.928406·Hierarchy=true 적용과 요청된 Blue/Red 방향의 기하 측정은 확인됐다. 최종 값 이후 필수 메뉴 결과 및 사용자 실기는 아직 대기다. 과거 측정/후보 상태를 최종 결과로 오독하지 않도록 단계별 이력을 유지한다.

## 과거 이력 — 최초 로그 전용 계획의 자연어 설명 (교정 승인 전)

아래 단락과 이어지는 최초 진단 계획은 당시 범위를 원문 그대로 보존한 것이다. ‘로그만 추가’·‘구현 승인 아님’은 **현재 승인 상태가 아니다**. 현행 구현 범위는 이 문서의 「승인된 생산 설계」와 이후 추가 승인 절을 따른다.

다음 실기에서는 화면에 보인 파도와 서버가 판정한 파도를 구별하고, 각 대상이 왜 효과를 받았거나 받지 않았는지 한 경기의 로그만으로 추적할 수 있게 한다. 이번 계획은 관측용 로그만 추가한다. 공격 시점, 전선 이동, 피해·회복량, HP 동기화, VFX 동작은 바꾸지 않는다. **이 문서는 구현 승인이 아니며, 사용자에게 공유해 별도 승인을 받은 뒤에만 코드 작업을 시작한다.**

## 변경 항목과 규칙 근거

| 향후 수정 후보 | 진단 내용과 불변 경계 | 규칙 근거 |
|---|---|---|
| `Application/Combat/TorrentAttackBehavior.cs` 및 진단용 `WaveSpawnRequest` 전달값 | `Apply`가 조기 반환하면 이유(`ctx/attacker/target/zero-forward`)와 식별 가능한 시전자·주 타깃 ID를 상한 안에서 기록한다. 현재 `WaveSpawnRequest`에는 주 타깃 ID가 없으므로, 성공한 발사의 주 타깃을 파도 종료까지 관찰하려면 **진단 전용 primaryTargetKind/Id를 요청과 `ActiveWave`에 보존하는 방안**을 구현 전에 검토한다. 이 값은 판정·공격 대상 교체·RPC·결과 키에 쓰지 않는다. `SpawnWave` 델리게이트는 현재 `void`이므로 성공 여부를 반환값으로 가정하지 않고 실제 등록 지점의 `spawn` 행으로 판별한다. | `GameSystemRules_Units.md` 규칙 28·29; `NET-DELIVERY-TRAVELING` |
| `Application/UseCases/UnitCombatUseCase.cs`의 `SpawnWave`/`ActiveWave` | 서버 로컬 관측용 파도 ID를 등록 시 부여한다(동일 세션 내 단조 증가, 게임 결과 키나 네트워크 ID로 사용하지 않음). `spawn` 1회에 `role/session`, `waveId`, 진단 전용 주 타깃 종류/ID(전달 가능할 때만), `casterId/team`, 서버 시각, origin·forward XZ, width·halfWidth·length·travelTime, heal 설정/실효값을 기록한다. 기존 요청·속도·힐 계산과 목록 삽입 순서는 불변. | 규칙 28·29·30; `NET-DELIVERY-TRAVELING`(진단 ID는 정식 contact ID가 아님) |
| `UnitCombatUseCase.cs`의 `TickWaves` | 매 프레임 전체 로그는 금지한다. 파도별 전선 상태는 접촉/제외 판단이 일어난 표본과 종료 요약에서만 남긴다. **실제로 딕셔너리 순회에 나타난** 후보에 한해 서버 시각·waveId·대상 종류/ID/팀·월드 XZ·`p`/`lateral`·frontDistance·판정 폭/길이·결과를 기록한다. 좌표 계산 전 null/사망은 ID/좌표가 없을 수 있어 개수만 셀 수 있고, 이미 제거된 대상은 이 순회에서 아예 관측 불가다. 살아 있는 후보의 `caster`, `already-hit`, `ally-building`, `behind`, `beyond-length`, `outside-width`, `collected`, 수집 후 `dead-before-apply`, `applied-damage/heal`, `no-hp-delta`를 실제 분기에 맞춰 구분한다. `not-yet-front`는 틱 중 일시 대기 카운터로만 집계하고 종료 원인으로 쓰지 않는다. 진단용 주 타깃 ID가 있으면 종료 시 딕셔너리 재조회로 `present-alive`/`absent`/`present-dead`를 분리하되, `absent`의 사망·제거 시각이나 이유는 이 로그만으로 단정하지 않는다. 사용자가 본 다른 대상 ID는 별도 식별이 없으면 관측 불가로 표시한다. 순회/수집/적용 순서를 바꾸지 않고 피해·힐 직전과 직후 HP를 기록하며 유닛/건물 hit-set을 유지한다. | 규칙 29·30; `NET-DELIVERY-TRAVELING` |
| `Presentation/Unit/UnitView.cs`의 `OnAttackHit` | TorrentSpirit에 한해 허용/억제된 로컬 marker, 로컬 시각·피어, 시전자 ID, 가능한 presentation scope/revision, VFX spawn 좌표·방향, playback 결과를 기록한다. 서버 waveId가 로컬에 없으므로 `VFX marker`는 **별도 표지**로 두고 대략적 시간/시전자 대조와 정확한 인과 상관관계를 구분한다. VFX 스폰·이동·허용 게이트는 무변경. | 규칙 29·31; `NET-DELIVERY-TRAVELING` |

`NetworkHealthSync.cs`는 **이번 코드 수정 후보에서 제외**한다. 기존 Android Client `AppliedDamage`/`AppliedHeal` 로그만 읽기 전용 보조 자료로 대조한다. 이 로그는 전역 HP 변화이고 waveId가 없어 파도별 확정 상관관계가 아니며, 이 계층에 새 로그를 넣으면 다른 공격·힐까지 폭주할 수 있다. 기존 로직 **제거·비활성화 없음**. 위 파일은 구현 승인 뒤의 수정 후보이며 실제 시그니처/관측 위치는 현재 코드 의존성과 로그 중복 방지에 맞춰 좁힌다. 진단 카운터·ID는 관측 전용이고 권위 AttackSequenceId/HitIndex를 만들어 냈다고 표기하지 않는다.

## 로그 상한과 안전성

- `LogRules.md` **1.2(두 축)**·**1.11(`RuntimeLogger`의 위치)**·**1.14(금지 사항)**는 실제 절 제목을 확인했다. 단기 진단은 `[INFO]`/`임시`로 분류하고 기존 로그 경로로 에디터 파일·Android Logcat에서 읽히게 한다. raw `Debug.Log`를 새로 쓰지 않으며, 운영 로그 키가 필요한 경로로 승격하지 않는다. 작업 종료 후 임시 코드 제거 및 로그 파일 보존은 별도 승인·후속 단계다.
- 발사별 `spawn`·`end`는 각각 최대 1행, 대상 상세는 **파도별 최대 32행**, 세션 전체는 **최대 256행**을 기본 상한으로 제안한다. cap 초과 시 추가 상세 대신 종류/사유별 누락 카운터를 파도 종료 1행에 넣고 `truncated=true`를 표기한다. cap에 걸렸는데 `0건 제외`처럼 해석하지 않는다. 실제 경기 밀도에 따라 구현 전 수치 조정 가능하나, 상한 없는 후보·매 틱 출력은 금지한다.
- 최대 라인 **UTF-8 4,096 byte 미만**을 필수로 하고 여유를 위해 3,000 byte 이하를 목표로 한다. 긴 배열/대상 목록을 한 줄에 넣지 않고 고정 필드의 짧은 행으로 나눈다. 구현 검증에서 최대 UTF-8 바이트 수를 측정하며 초과 시 명시적 축약/overflow 행을 남긴다. Android Logcat 절단 가능성을 배제하지 않는다.
- 시간은 서버의 단조 시간 기준을 우선 사용하고 Android/Editor 벽시계와 혼동하지 않는다. 좌표는 `ResolveWorldPosition`의 월드 XZ 판정값과 로컬 VFX 좌표를 각각 출처와 함께 기록한다. 민감한 계정·토큰·세션 인증값은 출력하지 않는다(`LogRules.md` 1.6). 공유 경기 식별자는 기존 관측 로그의 안전한 키 사용 방식을 확인하고 적용한다.

## 다음 실기에서의 판정 분기

1. 화면 marker는 있지만 서버 `spawn`이 없으면 공격 실행/핸들러 조기 반환 경계를 본다. `spawn`이 있으면 발사 자체는 이 단계에서 통과다.
2. `spawn` 뒤 `not-yet-front`는 정상적인 진행 중 상태다. 파도 종료까지 추적한 **동일 ID의 살아 있는 관측 후보**가 직사각형 밖이었거나, 최종 전선 도달 뒤에도 접촉 기록이 없었을 때만 서버 좌표·투영과 로컬 VFX 좌표/시각을 대조한다. 주 타깃이 종료 시 `absent`라면 기하학적 제외로 분류하지 않고 생명주기 미확정으로 남긴다. 이는 **시각과 판정 불일치 후보**이지 원인 확정이 아니다.
3. `collected` 뒤 `dead-before-apply` 또는 HP 변화 0이면 생명주기·최대 HP·동시 효과·방어 감쇄를 분리해 본다. hit-set 등록과 실제 HP 변화는 별도 칸으로 해석한다.
4. 서버 HP는 변했으나 기존 Client HP sync 로그에 같은 대상의 변화가 없거나 표시가 다르면 RPC 수신/대상 존재/동기화·표현 경계를 **후속 조사 후보**로 둔다. Client 로그는 waveId가 없고 타 공격·힐도 섞이므로 부재·근접 시각만으로 전송 실패나 파도와의 일대일 대응을 확정하지 않는다. 양쪽 HP가 변했어도 해당 제보 장면의 대상·시각 재식별이 필요하다.
5. 상세 cap이나 Logcat 절단으로 필요한 행이 빠졌으면 **미판정**으로 남기고 표본 범위만 좁혀 재실기한다. 한 정상 사례를 전체 파도 PASS로 일반화하지 않는다.

## 검증·위험·승인 경계

구현을 승인받은 뒤에는 정적 변경 검토로 gameplay writer/틱 횟수/공격 설정/RPC payload/VFX 위치가 불변인지 확인하고, Unity 컴파일 및 관련 자동 검사, 같은 세션의 Editor Host·Android Client 실제 로그에서 파도 ID·대상별 제외/접촉·HP 전후·marker와 종료 요약을 확인한다. cap 초과·4KB 미만·중복/누락 요약도 검사한다. 위험은 상세 문자열 생성에 따른 모바일 GC/프레임 비용, 서버 로그량, 발사 ID와 로컬 marker의 거짓 상관관계, 기존 동시 공격/회복으로 인한 HP 차이 오독이다. 진단 결과가 나올 때까지 피해 누락의 원인·수정안·실기 PASS를 확정하지 않는다. 현재 단계에서는 코드, Testcase/QA, 메모리, 상시 문서를 수정하지 않는다.

## 정합 교정 범위 초안 (2026-10-01 추가 — OPEN / 설계 대기)

사용자는 “그럼 교정진행해보자”로 앞서 설명된 폭·높이·속도 정합 교정을 승인했고, 기존 작업 문서를 갱신하기로 했다. 이제 화면 파도를 서버가 판정한 기준에 맞추는 접근을 준비한다. 서버 결과를 파티클 위치에 맞춰 재판정하지 않으며, VFX 프리팹은 그대로 둔다. 앞의 ‘진단 로그만/표현 무변경’은 당시 범위로 보존하고 현재 교정 범위와 구분한다.

### 불변 조건과 설계 검토 항목

| 항목 | 현재 합의 및 최종 설계에서 확인할 것 | 규칙 근거 |
|---|---|---|
| 서버 권위 | 서버 origin/forward/발사 및 접촉 시각/영역/피해·힐이 최우선이다. 클라 파티클 위치·로컬 marker를 권위 결과 입력으로 사용하지 않고 HP를 표현 대기 때문에 지연하지 않는다. | `GameSystemRules_Units.md` 규칙 18·29; `GameSystemRules_UnitCombatSynchronization.md` `NET-AUTH-001`·`NET-AUTH-002` |
| 이동·시작 정합 | 현행 서버와 표현의 거리/시간은 모두 3 WU/0.5초이므로 ‘VFX 이동 신설’로 계획하지 않는다. 로컬 시작 위치+오프셋, 방향 출처와 서버 시간축 연결 방식을 생산 설계에서 명시한다. 전용 payload/회차 연결/늦은 도착/중복/취소·사망 정책 및 실제 수정 파일은 아직 미확정이다. | 규칙 29; `NET-TIME-001`·`NET-TIME-002`·`NET-TIME-003`; `NET-DELIVERY-TRAVELING` |
| 폭·높이·시각 경계 | 서버 폭 3 WU와 보이는 파티클 폭은 별개로 측정한다. 프리팹 자체를 수정하지 않는 보정 방법은 프로그래머 설계 검토 뒤 확정한다. 배율 `(1,1,1)`에서 목표 스케일을 추정하지 않으며, 높이 오프셋 0.05도 시각 높이 측정값이 아니다. 높이를 서버 적중 조건으로 추가하지 않는다. | 규칙 29·31; `NET-AUTH-001` |
| 피해·힐·수명 | special-only, 대상별 1회, 적 유닛/건물 피해·아군 유닛 힐, 시전자·사망 제외 및 독립 HP/힐 채널을 유지한다. 로컬 VFX 반환·소멸이 서버 파도/HP 결과를 취소하지 않는다. | 규칙 28·29·30·31; `NET-DELIVERY-TRAVELING` |
| 기록·판정 | 기존 서버 진단 ID와 `noWaveId=true` 로컬 재생을 정식 회차 상관관계로 오인하지 않는다. cap/누락, 좌표 반전, 로컬 시계 차이를 표시한다. 임시 로그 정리·증거 보존은 후속 승인 단계이며 TC/QA는 진행하지 않는다. | `LogRules.md` 1.2·1.6·1.11·1.14; `NET-DELIVERY-TRAVELING` |

### 생산 설계를 받은 뒤 채울 부분

1. 권위 파도 생성 데이터의 소유 계층, 표현으로 전달하는 계약과 식별·시간축을 명시한다. 전송·중복·늦은 도착 정책 및 기존 표현 게이트와의 호환성을 메인 세션이 검토한다.
2. 원본 VFX 프리팹 무변경을 전제로 실제 파티클 치수와 전선 기준을 확인하고, 폭·높이·속도/위치 보정의 정확한 값과 적용 지점을 기록한다. 공용 풀의 다른 유닛 회귀와 재사용 상태 초기화도 검토한다. 지금 임의 값을 넣지 않는다.
3. 확정된 수정 파일 목록 및 단계별 근거를 추가한다. Research에 확인한 `TorrentAttackBehavior`/`WaveSpawnRequest`/`UnitCombatUseCase`, `UnitView`/`EffectManager`/`VfxPoolItem`/`EffectPreset` 경로는 **검토 경계이지 전부 수정한다는 목록이 아니다**.
4. 기존 발사 marker `2:10`/hitFrameTimes는 유지한다. 클립 타이밍 변경이 없으므로 그 사유의 StatsReference 변경은 불필요하다. 규칙 31 뒤 낡은 ‘정지 VFX’ 참고 및 최종 구현과 불일치하는 상시 문서 갱신 여부는 메인 세션의 결정 뒤 반영하며 이번에 수정하지 않는다.
5. 구현 뒤 컴파일·관련 자동 게이트와 실제 Host/Client 시각·접촉 근거를 단계별로 분리해 기록한다. 설계 정합, 재생 성공, 실제 접촉, 사용자 시각 수용을 같은 PASS로 묶지 않는다. 자세한 검토 기준과 결과는 구현 검토 후 추가한다.

**현재 결과:** 교정 준비 문서만 갱신했다. 최종 설계/구현 검토 대기이며 **OPEN**이다. 코드·VFX 프리팹·상시 규칙·StatsReference·메모리는 수정하지 않았고 Testcase/QA도 만들거나 수행하지 않았다. 이 초안은 구현 완료, 접촉 문제 해결, 정식 C3 migration 또는 PASS/CLOSED 증거가 아니다.

## 승인된 생산 설계 (2026-10-01 추가 — 구현 진행 / 검토 OPEN)

메인 세션이 전달한 프로그래머 설계를 아래에 기록한다. 사용자 기존 승인이 구현을 포함하며, 앞의 ‘설계 대기’는 이 인계 전 이력이다. 서버가 확정한 파도와 접촉을 화면에서 같은 시계로 재생하는 **파도 전용 adapter**다. 구현이 시작됐지만 완료/검증 결과는 아직 없고 Plan의 구현 검토는 OPEN이다. 기존 로직 삭제 승인은 없으며, marker 및 기존 텍스트의 중복 방출은 새 소유권이 실제 활성일 때만 전용 경로에서 비활성화한다.

### 계약과 파일별 책임

| 구현 경계 (`Assets/_Project/Scripts/` 기준) | 승인된 설계 및 검토 기준 | 근거 |
|---|---|---|
| `Application/Combat/` 계약·coordinator | 키는 `MatchEpoch + WaveDeliveryId`. 서버 실제 생성의 origin/aim/time/width=3/length=3/travel=0.5를 immutable spawn snapshot으로 보존하고 접촉별 확정 snapshot을 결합한다. AttackSequenceId/HitIndex를 만들어 낸 것처럼 표기하지 않는다. | `NET-AUTH-001`; `NET-DELIVERY-TRAVELING`(정식 회차 migration과 adapter를 구분) |
| `Application/UseCases/UnitCombatUseCase.cs`, `Application/Events/GameEvents.cs` 및 관련 이벤트 계약 | 실제 생성·접촉 수렴점에서 snapshot을 발행한다. 서버의 직사각형·전선·대상별 1회·유닛/건물 hit-set·피해/힐 계산은 유지한다. 클라 marker/particle이 서버 피해·힐을 생성하지 않는다. | `GameSystemRules_Units.md` 규칙 28·29·30; `NET-AUTH-001` |
| `Infrastructure/Network/NetworkCombatController.cs`, `NetworkHealthSync.cs` | Events의 spawn/contact를 서버에서 전달하고 Host/Client를 같은 표현 소비 계약으로 연결한다. 권위 HP RPC 즉시 적용은 유지하며, 파도 소유 경로의 기존 피해/힐 텍스트만 중복 방출하지 않도록 분리한다. HPbar·사망 타이밍 전체 전환은 아니다. | `NET-AUTH-002`; `NET-TIME-001`·`NET-TIME-002` |
| `Bootstrap/GameBootstrapper*.cs` 조합 | coordinator/presenter/event·network adapter 연결은 유일 조합 루트에서 한다. Application→Infrastructure 구체 참조나 Unity.Netcode 참조를 추가하지 않고 NetworkBehaviour는 Infrastructure에만 둔다. | `.claude/MEMORY.md` 「아키텍처 핵심 제약」 |
| Presentation `WavePresenter` (정확한 신규 경로는 구현 보고에서 확인), `Effects/EffectManager.cs`, `Effects/VfxPoolItem.cs` | 서버 spawn origin/aim과 공통 presentation clock으로 파도 진행을 재생한다. 확정 접촉 snapshot의 HP 텍스트도 같은 시계에 예약한다. 원본 VFX 프리팹은 무변경이며 seeded 측정으로 얻은 axis bounds/leading-offset 기반 runtime calibrated geometry를 사용한다. 측정 전 임의 스케일/높이/선두 보정값을 넣지 않는다. | 규칙 29·31; `NET-TIME-001`·`NET-TIME-002` |
| `Presentation/Unit/UnitView.cs`, 기존 텍스트 emitter (`FloatingHpTextSpawner` 및 실제 연결 경로) | **새 wave owner가 활성인 TorrentSpirit 경로만** 로컬 marker source VFX/SFX와 legacy contact text 방출을 막는다. 비활성/fallback·다른 유닛은 유지한다. 새 소유 경로에서 VFX/SFX 재생을 담당해 중복 또는 무음·무표현 공백을 만들지 않는다. 권위 HP/힐 이벤트 자체를 없애지 않는다. | 규칙 28·30·31; `NET-AUTH-002`; `NET-DELIVERY-TRAVELING` |
| Editor 측정 도구와 기존 selfvalidation | 동일 seed·시간 표본·축 정의로 실제 파티클 axis bounds와 leading-offset을 **교정 전/후** 산출한다. 기존 gate에 adapter·geometry·ownership 회귀를 연결한다. 도구의 최종 파일/메뉴·측정 결과·수치/허용치는 구현 보고 후 기재한다. 프리팹 원본을 수정하지 않는다. | 규칙 29·31; WORKFLOW [5-2] 도구 역할 구분 |

### 수명·순서·회복·재설정 검토

- MatchEpoch는 경기 경계를 구분하고 WaveDeliveryId와 함께 dedupe한다. 접촉별 중복 판별·멱등 기준도 명시해 같은 피해/힐 텍스트를 두 번 방출하지 않는다. Client는 확정 contact만 표시하며 예측 적중을 만들지 않는다.
- contact-before-spawn 등 reorder는 bounded buffer로 결합한다. 최대 개수/age 및 초과 처리는 구현에서 명시·검토하며 여기서 임의 숫자를 확정하지 않는다. 생존 파도 늦은 도착은 공통 표현 시각의 진행도로 catch-up하고, 만료·이미 소비된 과거 표현을 새 파도/재사용 대상에 적용하지 않는다. `NET-TIME-003`·`NET-TIME-004`·`NET-TIME-005`를 검토 기준으로 삼되, 현재 adapter만으로 일반 C3 migration 완료를 주장하지 않는다.
- 경기 종료·despawn·재경기의 수명 경계에서 coordinator/buffer/dedupe/presenter/pool 상태를 reset/retire한다. 로컬 파티클 종료나 attacker View 상실이 이미 확정된 서버 contact·HP를 소급 취소하지 않는다. 사망·취소 뒤 아직 미확정인 표현의 처리와 이미 확정된 contact의 표현을 구분해 검토한다.
- Host도 같은 clock/coordinator를 소비하며 자체 spawn 경로와 RPC loopback이 중복하지 않는지 확인한다. Red 좌표·방향 반전은 표현 경계에서만 적용한다. Client Simulation Root writer 추가나 서버 HP 지연은 금지다.

### 경계와 검토 산출물

`Unresolved / LegacyFallback / MigrationRequired`를 유지한다. 파도 전용 delivery key는 정식 AttackSequenceId 전환이나 Supported 승격이 아니다. 발사 marker `2:10`/hitFrameTimes 무변경, 원본 VFX 프리팹 무변경, TC/QA 새 작업 없음. 범위는 **파도 표현과 확정 접촉 HP 텍스트 정합**이며 전체 HPbar/사망/공격 애니메이션 시간 동기화·25종 역할교대·rollback 완료로 확대하지 않는다.

구현 보고 후 실제 수정 파일·계약 필드·buffer 상한·owner 활성 조건·측정 전후 결과·selfvalidation/컴파일 결과·미검증 실기 경계를 추가한다. 상시 규칙의 낡은 VFX 이동 참고 수정은 메인 세션의 후속 문서 결정에 따른다. **현재: 승인된 설계 기록 완료, 프로그래머 구현 진행, 구현 검토 OPEN, PASS/CLOSED 아님.**

### 추가 승인 — 생성 이전 구간을 제외하는 서버 전선 시계 교정

메인 소스 검토에서 기존 `Elapsed += dt`가 이전 틱부터의 전체 구간을 새 파도에도 더할 수 있는 경계가 확인됐다. 파도가 그 구간 중간에 생성됐거나 싱글플레이에서 같은 프레임의 `TickPendingHits`가 `TickWaves`보다 먼저 파도를 생성하면, 생성 이전 시간까지 전선 진행량에 포함할 수 있다. 이 교정은 **실제 생성 이후 경과 시간만 반영하는 승인된 정확성 수정**이며 임의 게임플레이 파라미터 튜닝이 아니다. 과거 진단의 ‘전선 무변경’은 당시 범위로 남긴다.

- `UnitCombatUseCase`에 주입된 동일 서버 시간원의 **실제 spawnTime**을 keyed wave 생성 시 저장하고, 진행은 `elapsed = max(0, serverNow - spawnTime)`, `front = min(length, speed × elapsed)`로 계산한다. spawn snapshot과 서버 전선이 같은 시각 기준을 사용해야 한다. 로컬 marker 시각이나 Client 시계로 서버 진행량을 계산하지 않는다.
- 길이 3 WU·이동 시간 **0.5초**·속도 **6 WU/s**는 유지한다. **접촉 후보 판정은 여전히 서버 틱의 현재 좌표 표본**에서 수행하며, 틱 사이 이동을 연속 충돌로 재구성하거나 접촉 시각을 임의 보간하지 않는다. 대상별 1회 및 유닛/건물 별도 hit-set, 피해·힐 계산/권위 HP 즉시 적용도 유지한다. 틱 지연으로 종료 관측이 0.5초보다 늦을 수 있다는 사실과 설정 시간 자체를 구분한다.
- 현재 소스 읽기에서 `SpawnTime = WaveServerTime`와 `TickWaves`의 `WaveServerTime - wave.SpawnTime` 계산은 확인했다. 이것은 구현 중인 소스 관찰일 뿐 최종 동작 검증이 아니다. 최종 검토에서는 실제 시간원 주입/싱글·멀티 시간축 일치, 구간 중간 생성과 같은 프레임 생성의 pre-spawn elapsed 배제, 전선 clamp·종료 조건·hit-set 유지 및 경기 reset 경계를 확인한 뒤 결과를 추가한다.
- 규칙 근거: `GameSystemRules_Units.md` 규칙 29의 서버 전선·대상별 접촉 계약, `GameSystemRules_UnitCombatSynchronization.md` `NET-AUTH-001`·`NET-TIME-001`·`NET-DELIVERY-TRAVELING`.

### 추가 승인 — 최종 구현 확인 후 상시 문서의 사실 반영

메인 세션은 구현 확인 뒤 `GameSystemRules_Units.md` **규칙 31 뒤의 낡은 정지 VFX 참고**와 `StatsReference.md`의 관련 **구현 상태/튜닝 설명**을 이 교정의 실제 결과에 맞춰 갱신하도록 승인했다. 앞의 ‘StatsReference 변경 불필요’는 **클립 타이밍을 이유로 한 값 변경이 불필요**하다는 뜻이며, 이번 상태·튜닝 설명의 사실 반영을 배제하지 않는다.

발사 marker `2:10`/hitFrameTimes·클립 값은 변경하지 않는다. 구현 결과와 측정값이 오기 전에는 상시 문서에 숫자·완료 상태를 미리 쓰지 않는다. 현재 단계에서는 Plan만 추가 갱신하며 규칙·StatsReference 수정은 다음 최종 정보 뒤에 수행한다. 전체 HPbar·사망·애니메이션 시간 동기화나 Supported migration 완료로 확대하지 않고 **구현 검토 OPEN**을 유지한다.

### 측정에 따른 후보 폐기와 남은 gate (2026-10-01 13:18:42 전달)

> **[🔴 2026-10-01 13:29:11 정정 — 원문 보존 / 중간 측정 신뢰 보류]** 추가 `BakeMesh` world-coordinate translation probe에서 `Main_Water_Surge` error=1.188486, **FAIL**이 전달됐다. 아래 ‘실제 폭 변화 0’·fit/holdout 선두 잔차는 측정 좌표계 신뢰가 회복되기 전까지 실제 효과·최종 보정의 유효한 근거가 아니다. root-width 후보는 생산 미채택으로 유지하되 효과 없음의 확정 판정은 보류하고, Z 보정 후보도 채택하지 않는다. 좌표계/cached renderer transform은 원인 가설이며 진단 중이다.

`calibrated-20261001-041842-820.csv`의 상세 집계와 근거 구분은 Research 「Unity 측정 중간 결과」에 기록했다. 기존의 측정 기반 runtime calibrated geometry 계획은 유지하되, **static root X scale 0.157548 후보는 실제 폭 변화 0으로 폐기**한다. 같은 원본/교정 fit의 baked 표본 폭은 모두 3.977209 WU, 최대 |X|는 모두 2.041061 WU다. root scale의 수식만으로 폭 3 WU 정합을 완료했다고 기록하거나 효과 없는 후보를 생산 보정에 채택하지 않는다.

Z offset `-0.528406` 후보의 fit 선두 잔차 ±0.257130 및 독립 holdout [-0.245697, 0.229983]은 중간 측정 근거다. 이 값의 최종 runtime 채택은 실제 helper 재생 경로의 측정·구현 검토 뒤에 확인한다. 초기 radius 추정 약 4.03과 baked 기하 폭 3.977209를 서로 같은 측정값으로 취급하지 않는다. baked 경계는 alpha silhouette가 아니다.

**남은 필수 확인:** 실제 runtime helper의 파티클 재생·이동·기하 보정 경로로 before/after 폭 및 선두 잔차를 재측정하고 재현 효과를 확인한다. 측정과 독립 holdout 결과 없이 다른 폭 보정값을 임의 확정하지 않는다. 원본 VFX 프리팹·서버 폭 3 WU/길이 3 WU/시간 0.5초·속도 6 WU/s는 그대로 유지한다. **최종 width 미확정, 구현 검토 OPEN**이며 상시 문서 최종 반영은 최종 측정·Unity 결과 이후다.

**13:29:11 추가 선행 gate:** 먼저 도구의 좌표계와 renderer transform 적용을 진단·교정하고 translation probe의 재검증 결과를 확보한다. 그 뒤 같은 측정 정의로 before/after·독립 holdout을 새로 측정하고 실제 runtime helper 경로를 검증한다. 이전 CSV의 숫자를 재사용해 교정 효과나 최종 폭/선두 보정을 확정하지 않는다. 최종 결과를 받기 전 상시 문서의 완료·튜닝 수치 반영은 계속 보류한다. **측정 신뢰 보류 / 최종 width 미확정 / 구현 검토 OPEN**이다.

### 실제 파일 확정 및 runtime 재실행 gate (2026-10-01 13:33:06 전달)

앞의 `WavePresenter`는 구현 파일에서 **`Presentation/Effects/WavePresentationPresenter.cs`**로 확인됐다. 계약/coordinator는 `Application/Combat/WavePresentationContracts.cs`·`WavePresentationCoordinator.cs`, 서버 구현 partial은 `Application/UseCases/UnitCombatUseCase.WavePresentation.cs`, 네트워크 partial은 `Infrastructure/Network/NetworkCombatController.Waves.cs`다(모두 `Assets/_Project/Scripts/` 기준). 실제 `WaveVisualCalibration`·`HitPresentationQueue`·Bootstrap Setup/Map을 포함한 전체 검토 파일 목록은 Research 「현행 구현 파일과 13:33:06 검증 경계」 표를 따른다. Editor 파일은 `Assets/Editor/Tools/TorrentWaveProductionValidation.cs`·`TorrentWaveCalibrationTool.cs`다. 이전 추정 이름·파일 미확정 표기는 인계 전 이력으로 보존한다.

메인 전달에 따르면 actual-server/coordinator fixture는 통과했지만 **overall gate는 실패**했다. runtime pool seed restore order FAIL은 수정됐으나 **최종 재실행 대기**다. 실제 풀 반환·재사용에서 seed/auto-seed 원복, 이전 wave 설정 잔존 여부 및 runtime geometry 측정을 재확인하고 전체 gate 결과를 받은 뒤 판정을 추가한다. 코드상 복원 순서만으로 FAIL을 해소하거나 전체 PASS로 바꾸지 않는다.

원본 clip/prefab hash 보존은 메인 전달 사실이며 문서 담당자의 독립 hash 검증으로 쓰지 않는다. 최종 결과 이후 StatsReference·Units에 상태와 실제 튜닝을 반영한다. 현재 상시 문서는 수정하지 않으며 **측정 신뢰 보류·최종 폭 미확정·구현 검토 OPEN·전체 PASS 아님**을 유지한다.

### 13:38:51 후속 메뉴 gate 결과 — geometry 최종 검토는 OPEN

pool seed 수정 후 실제 `RunUnitActionSelfValidation` 재실행에서 UAS-DIAG·PRODUCTION-TIMELINE·C2·C3 및 TorrentProduction PASS가 메인으로부터 전달됐다. 관련 로그는 메인이 확인 중이다. 이는 앞의 실패 이후 **메뉴 gate 재실행 통과 인계**이며, 문서 담당자의 독립 감사 완료나 전체 작업 PASS가 아니다. 13:33:06 실패 이력은 그대로 남긴다.

Console Error 0과 Warning 21(기존 20 + gate fixture Warn 1)을 분리해 기록한다. fixture 경고의 실제 내용·분류를 확인한 뒤 결과를 추가하며, runtime warning zero PASS로 표기하지 않는다. 최종 geometry/width·재측정 신뢰와 실제 runtime 정합 검토가 남아 있으므로 **전체 작업 OPEN**, 상시 StatsReference·Units 최종 반영은 아직 보류다.

### 13:40:33 필수 메뉴 확인 — 단계별 gate 유지

메인 확인 결과, 현재 런타임 코드 기준 `RunUnitActionSelfValidation`(13:38:51)과 `Self Validate Unit Root Pose Cross Audit`(13:40:33) **두 필수 메뉴 PASS**다. `TorrentWaveProductionValidation` PASS 및 원본 clip/prefab 양쪽 source hash unchanged 확인도 전달됐다. 앞의 확인 대기 표기는 당시 이력이며, 독립 문서 담당자 실행/감사로 표기하지 않는다.

남은 gate는 분리한다: **최종 geometry 수치·신뢰 가능한 재측정 → 수치 반영 후 필수 메뉴 재확인 → 외부 headless 검증 → Host/Client 실기 → 사용자 시각 수용**. 현재 메뉴 PASS는 후속 단계의 통과 근거가 아니다. 경고 21건의 내용·분류 확인도 별개이며 무경고 PASS로 바꾸지 않는다. 상시 문서 최종 반영은 최종 결과 인계 이후이고 **전체 작업 OPEN**을 유지한다.

### 13:41:08 측정 도구 mapping v2 — 좌표 probe와 production 측정 분리

새 좌표 probe `coordinates-20261001-043730-837.csv`에서 `flags=0` + PS `TransformPoint` 1회 변환은 Local/Hierarchy 각각 6 pose 오차 0으로 확인됐다. 메인 전달에 따르면 기존 `flags=3/rawWorld` 가정 경로는 부모 scale·cached renderer transform 갱신 문제로 폐기했고 tool mapping v2 교정은 컴파일 오류 0이다. 이 단계는 **좌표 변환 probe 및 컴파일 gate**이며 production helper의 최종 geometry gate와 다르다.

교정된 도구로 실제 production helper 메뉴를 재측정해 새 before/after·fit/holdout 수치를 확보한다. 이전 폭/선두 잔차는 재사용하거나 probe 성공만으로 신뢰 보류를 해제하지 않는다. 최종 geometry 수치 반영 후 필수 메뉴 재확인과 후속 검증은 그대로 남는다. **새 production 수치 대기 / 최종 폭 미확정 / 전체 OPEN**, 상시 상태·StatsReference·Units 최종 반영 보류다.

### 13:44:44 새 runtime helper 측정과 생산 후보 승인 경계

> **당시 상태 — 17:59:49 최신 절로 대체:** 아래 후보 미달·승인 pending·미확정은 13:44:44 인계 시점의 기록이다. 현행 production .78 적용 및 방향 측정 결과는 「17:59:49 production 방향 측정 반영」을 따른다.

mapping v2의 `runtime-helper-20261001-044444-281.csv` 측정은 확보됐다. 직접 집계 및 인계값 차이는 Research 후속 표에 기록했다. **X=0.782384·anchor=0.928406·Hierarchy=true 후보는 fit 최대 |X|=1.500076으로 반폭 1.5를 초과해 미달**이다. 대칭 전체 경계 환산 초과 0.000152를 임의 허용치 상향으로 통과시키지 않는다. holdout 1.470601은 안쪽이지만 fit 미달을 상쇄하지 않는다.

메인이 전달한 **생산 X=0.78f·anchor=0.928406·Hierarchy=true 채택 요청은 승인 pending**, 해당 값의 production 재측정은 미확인이다. 현행 계획에서 채택 완료·최종 calibration 값으로 기록하지 않는다. 승인 후 실제 production helper로 fit/holdout을 다시 측정하고, 최종 수치 반영 뒤 두 필수 메뉴를 재확인한다. 기존 `after-production` phase를 새 0.78 결과라고 해석하지 않는다.

서버 폭 3 WU·길이 3 WU·시간 0.5초·속도 6 WU/s는 불변이다. 표현 X 축만 보정하고 **Y 배율 1 유지**: 새 helper 원본/후보의 높이는 fit 1.005499·holdout 1.029019로 같다. 선두 잔차 fit ±0.257130 WU(속도 환산 약 ±0.0429초), holdout [-0.245697, 0.229983] WU가 남는다. baked transparent sprite geometry와 서버 틱 접촉 경계를 구분해 완전 접촉 동시성은 보장하지 않는다.

**당시 상태 (17:59:49 최신 절로 대체): 새 v2 helper 측정 확보, 0.782384 후보 fit 미달, 0.78 생산 후보 승인·재측정 대기, 최종 geometry 미확정 / 전체 OPEN.** 상시 문서 최종 수치·상태 반영은 당시 후속 최종 정보 대기였다.

## 17:59:49 production 방향 측정 반영 (2026-10-01 KST — 전체 OPEN)

적용값은 **X=.78f·FrontAnchor=.928406f·Hierarchy=true**이며 서버 폭 3 WU/길이 3 WU/시간 .5초/속도 6 WU/s는 바꾸지 않았다. 표현 X만 축소하고 Y 원본 배율은 유지한다. `directions-20261001-085949-284.csv`는 Blue/Red × yaw0/90/+45/-45/180의 fit·holdout **모두 lateralWidthAchieved=True·centeredExcess=0**이다. 최대 lateral 절댓값은 fit1.494449·holdout1.465140 WU, identity/production 높이는 fit1.005499/1.005499·holdout1.029019/1.029019 WU다. Research에 새 CSV 직접 집계와 코드 적용값을 기록했다. 앞의 승인 pending·최종 값 미확정은 당시 이력으로 보존한다.

### gate와 남은 검증

- **확보:** mapping v2 좌표 probe, 현행 runtime helper/생산 적용값, 고정 camera·8 seed·10 표현 방향 조합의 기하 경계 표본. 이는 alpha silhouette나 임의 방향/네트워크 역할교대·사용자 실기 PASS가 아니다.
- **대기:** 최종 값 이후 Unit Action selfvalidation 및 Unit Root Pose Cross Audit 실제 메뉴 재실행 결과. 13:38/13:40 PASS는 변경 전 이력이며 새 결과로 대신하지 않는다. 외부 headless·Host/Client 실기·눈으로 본 수용은 각각 별도 OPEN이다.
- **불변/한계:** marker2:10@30fps=70/30·cycle4초, 원본 clip/prefab, 서버 권위 HP 즉시 적용·hit-set 유지. serverNow-spawnTime은 pre-spawn elapsed를 막지만 서버 stall 동안 contact sampling이 늦는 한계가 있다. 렉 원인·성능 해결은 별도다. Unresolved/LegacyFallback 및 C3 migration 미완료, 전체 HPbar·사망·애니메이션 타이밍 보장 없음.
- **빌드:** 메인은 시도 예정만 전달했다. 빌드 시작/완료·APK 설치·실기 실행 확인 없이 완료라고 표기하지 않는다. TC·new Task·QA는 추가하지 않는다.

사용자가 승인한 StatsReference 상태·튜닝 설명과 Units 규칙31 참고를 현행 구현에 맞춰 갱신하며, 결과 범위와 남은 OPEN을 함께 적는다. 관련 메모리에는 반복 확인된 측정 방법·근거 구분 교훈을 남긴다. **전체 작업 OPEN / 최종 필수 메뉴 결과·사용자 실기 대기**다.

### 18:06 확정 상태 — Editor busy / 최종 두 메뉴·빌드 미실행

> **18:15 최종 UI 확인(메인 전달):** `Application.UpdateScene` busy 15:21 지속. 승인된 최종 두 메뉴 재실행·빌드 시도는 busy로 여전히 미실행이며, Editor 정상 복귀 미확인·전체 OPEN을 유지한다.

메인 UI 확인은 `Application.UpdateScene` busy 06:19다. 측정 loop 완료·CSV 저장·최종 로그는 확인됐지만 Editor 정상 복귀는 별도 미확인이다. 프로그래머의 읽기 전용 확인(Responding=true, AssetImportWorker 2개, 후속 오류 없음, 반복 update 등록 없음, finally 복구)은 원인 확정이 아니다. **최종 값 이후 두 필수 메뉴 재실행 및 빌드 시도 모두 미실행**으로 기록한다. 앞의 메뉴 결과 대기는 실행 중이라는 뜻이 아니다.

원본 4 hash unchanged 확인 완료는 메인 전달 근거다. 최신 전달은 Unity busy 약 8분이며 정상 복귀는 미확인이다. **두 필수 메뉴 재실행과 빌드 시도는 사용자 승인 범위의 남은 작업이나 Unity busy로 실행 불가하여 모두 미실행**이다. 추가 진단·강제 종료만 이번 범위 밖으로 수행하지 않는다. 문서 담당자는 승인된 문서·메모리 최소 갱신과 최종 정합성 검사로 마무리한다. 사용자 실기와 전체 작업 OPEN을 유지한다.

## 두 경기 실기 반영 및 다음 제안 (2026-10-01 — 구현 미승인)

두 경기 비교와 정확한 session·cap·오류 0의 한계는 Research 「두 경기 실기 결과와 근거 한계」를 따른다. 파도 관측/display 17→32·HP 변화 45→121·전체 Client heal 4→36으로 확대됐으나 최신 32는 관측 상한, noHpDelta 11은 원인 미확정이다. **두 경기 실기 근거 확보 / 정확 시각 수용 OPEN / 전체 OPEN·C3 Unresolved** 유지. 18:15 busy·당시 메뉴/빌드 미실행 관찰은 이력이며 Codex 최종 메뉴/빌드 확인 완료로 바꾸지 않는다.

후속은 메인이 검토한 읽기 전용 코드 분석에 따른 **제안**으로, 판정/설계 교정 확정이나 구현 승인이 아니다.

- gameplay 무변경 경기 전체 압축 terminal 집계: spawn/display/contact/retire/failure와 **Expired/channelFailure/pending을 분리**. 매 틱 상세 기록·무제한 로그 상한 증대 없음.
- noHpDelta의 fullHP/dead/zero 등 근거 기반 분류. 11건을 오류·fullHP·방어력 원인으로 단정하지 않는다.
- wave/target key 접촉·텍스트 실제 표시 시각의 한정 표본 및 반폭 1.5 안/바깥 직후 정밀 표본. **server contact와 EmitContact 실제 성공 시각을 동일 server clock의 상대 시각으로 비교할 표본**도 필요하다.
- 규칙29와 일치하는 현재 `p<=front` 전체 영역 틱 판정은 유지한다. 얇은 전선/swept 행동 변경을 제안 확정하지 않는다. ERROR/contract 0은 누락 0의 증명이 아니며 실제 expiry 발생도 미확정이다.
- 원본 VFX/클립·발사 70/30초·cycle 4초·서버 권위 및 gameplay 수치 유지. 새 Task/TC/QA 없이 기존 문서 갱신만 수행한다.

## 승인된 전 경기 사건 중심 로그 구현 계획 (2026-10-01 — 메인 인계용)

매 테스트의 첫 일부 파도만 보는 대신, 경기 전체에서 파도와 대상별 판정·실제 화면 재생 결과를 빠짐없이 연결한다. 상세한 **사건별 기록**과 반복 틱의 **제외 상태 전이 기록**을 함께 사용한다. 사용자는 기존 Research/Plan 갱신 후 로그 구현을 재승인했다. 앞선 「구현 미승인/한정 표본 제안」은 과거 이력이며 이 승인된 범위로 대체된다. 본 절은 계획이지 구현 완료·새 실기 판정이 아니다.

**기존 gameplay 로직 제거 없음.** 관측용 32 cap/상세 contact 출력 제한만 제거·대체한다. coordinator/transport의 gameplay 안전 용량·age/baseline·retry/retire 정책은 그대로다. `p<=front` 판정·서버 권위·hit-set·HP·damage 호출·Publish semantics·원본 VFX/clip 및 모든 수치 불변. 발사 70/30초·cycle 4초·production calibration 유지. 전체 OPEN/C3 Unresolved·정확 시각 수용 OPEN.

### 구현 파일 범위 — 프로그래머 읽기 전용 인계 기준

아래 기존 6개와 추가 승인된 ConsoleSink·RuntimeLogger 2개는 `Assets/_Project/Scripts/` 기준이다. 신규 파일은 후속 **코드 관측 helper**이지 신규 Task/TC/QA 문서가 아니다. 이 문서 작업에서는 생성하지 않는다.

| 파일 | 로그 구현 범위 | 규칙 근거 |
|---|---|---|
| `Application/UseCases/UnitCombatUseCase.cs` | 32 cap 제거, 전 파도 spawn/end, target 판정 전이·수집·writer before/immediateAfter/eventAfter·noDelta 근거, 서버 위치/전선·시각 | Units 29·30·44; NET-AUTH-001·NET-TIME-001 |
| `Application/UseCases/UnitCombatUseCase.WavePresentation.cs` | 확정 contact 발행과 반환 사유를 observational 기록; 기존 적용량 계산·ordinal 증가·Publish 조건/호출 순서 유지 | Units 29·30; NET-DELIVERY-TRAVELING |
| `Infrastructure/Network/NetworkCombatController.Waves.cs` | 정상 RPC/snapshot 구분, 송신·수신·수락/거부/대기·전달/정리 사건을 결속 | NET-TIME-001·004·005; LogRules 1.4 |
| `Application/Combat/WavePresentationCoordinator.cs` | accept/dedupe/reject·첫 channel 실패·성공·expiry·retire 및 pending/Expired/ChannelFailures/Failures 분리 | NET-TIME-003~005; Units 26·29 |
| `Presentation/Effects/WavePresentationPresenter.cs` | display cap 제거, 실제 spawn/text emitter 결과·공통 clock, GameEnd snapshot과 Detach final/epoch cleanup | Units 29~31; NET-TIME-001·002 |
| `Presentation/Effects/VfxPoolItem.cs` | wave로 결속된 실제 재생·풀 반환/retire의 성공/실패·수명 사건만 관측; 이동/재생/seed 동작 무변경 | Units 31; NET-AUTH-001 |
| `Infrastructure/Debug/ConsoleSink.cs` (메인 추가 승인) | 신규 wave diagnosis prefix의 예외 없는 Info에만 `LogOption.NoStacktrace` 콘솔 출력 분기; 다른 로그·예외·전역 설정·GameLog/FileSink 유지 | LogRules 1.4·1.8·1.9; 아래 안전 조건 |
| `Infrastructure/Debug/RuntimeLogger.cs` (메인 추가 승인) | FileSink→RuntimeLogger.Log의 콘솔 출력에도 동일 신규 wave diagnosis Info 한정 `NoStacktrace`; 파일 기록·세션 소유권·IsEmittingToConsole·기존 오류/예외 로그 유지 | LogRules 1.4·1.8·1.9·1.10; 아래 안전 조건 |
| 신규 `Application/Combat/WavePresentationDiagnostics.cs` + `.meta` | 개발 전용 관측 helper, 키/전이 state/terminal 집계·짧은 구조화 출력. Application→Infrastructure 역참조 금지 | LogRules 1.4·1.7·1.8·1.14; 공용 메모리 레이어 제약 |

### 사건 기록 계약

1. **전 경기 전 파도/확정 접촉:** 생성·collected·판정/적용·Publish·전달·표현 소비를 causal key로 연결한다. `epoch+WaveDeliveryId`, target kind/id, contact ordinal(발행된 경우), stage·role·event identity를 유지한다. 진단만의 번호와 정식 delivery key를 구분한다. cap 제거는 출력 관측 제한이며 coordinator의 active slots 제한 제거가 아니다. 확정 접촉은 무샘플링·무음 drop 금지; 집계만으로 상세 evidence를 대신하지 않는다.
2. **판정 before/after와 noDelta:** 읽은 target 생존/팀/종류·HP/MaxHP·요청량·관측 가능한 실제 적용량·damage/heal·좌표/forward 투영 p/lateral/front/폭/길이를 기록한다. writer 직전 HP, 즉시 적용 직후 HP, 동기 event 처리 뒤 HP를 구분한다. 기존 writer를 재호출하거나 계산 결과를 새로 적용하지 않는다. noDelta는 fullHP/dead/zero 등 실제 조건으로 입증된 사유만 부여하고 미확정은 Unknown으로 유지한다. fullHP는 heal 가설이며 방어 감쇄를 11건의 원인으로 단정하지 않는다.
3. **제외는 전이 기반:** per-wave + target kind/id의 최초 사유와 사유 변화만 출력한다. 같은 틱/같은 사유 반복 및 alreadyHit 매 틱 출력 금지. target identity/수명과 epoch를 분리하며 retire 시 상태를 정리한다. bounded 전이 state overflow는 첫 overflow 사건·영향 개수/범위·terminal completeness로 명시한다. overflow를 contact evidence sampling이나 gameplay 누락으로 전파하지 않는다.
4. **시계·위치:** 권위 spawn/contact clock과 위치, 현재 local synced-server clock, presentation-minus-delay clock을 필드로 구분한다. 적용 순간·Publish 순간·EmitSpawn/EmitContact 실제 성공 clock을 같은 사건 key로 결속하고 contact 대비 상대 지연을 남긴다. wall clock/realtime은 보조·로컬임을 명시한다. Red 변환 전 domain 위치와 view 위치의 좌표계를 구분한다. 새 판정 시간 보간·delay 튜닝 금지.
5. **성공·실패·expiry:** emitter 시도와 실제 bool 성공을 구분한다. retry는 첫 실패→성공 또는 expiry만 출력하고 같은 실패의 매 프레임 재출력 금지(누적 시도 수는 집계 가능). VFX item 반환/텍스트 TryShow 성공을 관측한다. bool 성공을 실제 픽셀/육안 수용으로 부르지 않는다. Expired는 Failures와 별도이며 ChannelFailures·reject·duplicate·pending도 독립 기록한다. snapshot 복원과 normal RPC를 구분해 새 발사 수/중복 피해로 합산하지 않는다.
6. **종료:** wave retire의 사유·미소비 접촉·최종 표현 상태를 남긴다. GameEnd는 아직 살아 있는/pending 상태의 **중간 snapshot**이며 조기 terminal 완료 선언 금지. Detach/최종 epoch cleanup에서는 reset·clear 전에 final 집계와 남은 상태를 기록하고 종료 사유로 구분한다. 여러 cleanup 호출은 중복 final을 만들지 않는다. 재경기 epoch 초기화는 앞 경기 이력과 통계를 섞지 않는다.

### 집계·형식·출력 보존

- 경기/역할/epoch 및 wave retire별 spawn·접촉 수집·적용/HP 변화/noDelta 사유·Publish·normalRPC/snapshot·spawn/text 성공·첫 실패/recovery·Expired·ChannelFailures·Failures·pending·retired·전이 overflow/관측 유실을 구분한다. 서버 contact 분모와 peer별 소비 결과 분모가 다름을 유지한다.
- 기존 GameLog/FileSink/LogSessionOwner 경로를 사용한다. 에디터 daily RuntimeLog.txt와 기기 Logcat RuntimeLog_device.txt의 현재 기록 경로 유지, 별도 raw Debug.Log/직접 파일 writer/상시 sink 교체 없음.
- 세부 사건은 Info + 개발 관측, 계약/관측 불완전성은 근거에 맞는 Warn + 개발 진단으로 구분하며 기존 운영 오류 처리는 변경하지 않는다. helper는 UNITY_EDITOR·DEVELOPMENT_BUILD conditional/전처리 관습을 유지하고 릴리스에서 문자열/전이 state 비용이 남지 않도록 한다. 식 본문 logging 람다 금지. 근거: LogRules 1.2·1.7·1.14.
- 새 구조화 payload는 ASCII stage/reason과 invariant 숫자, 고정 key 표기 및 분해 가능한 x/y/z 필드를 사용한다. 값 안의 구분자 `, ` 금지. 실제 GameLog prefix 포함 UTF-8 byte cap을 확인해 Android에서 잘리지 않도록 긴 terminal을 짧은 번호 매긴 chunk로 나눈다. chunk count/identity/completeness를 남겨 유실을 숨기지 않는다. byte cap은 contact 생략 수단이 아니다. 인증 토큰·사용자 식별 원본·비밀 세션 키를 출력하지 않는다. 근거: LogRules 1.4·1.6.
- 전 경기 사건 보존은 무제한 in-memory 이력 축적을 뜻하지 않는다. 사건은 기존 sink로 즉시 기록하고 retire/reset으로 상태를 회수한다. 쓰기/포맷/관측 용량 문제가 있으면 명시적인 incomplete/overflow 근거를 남기며 조용히 성공 처리하지 않는다. gameplay 경로를 막거나 기존 안전 제한을 높여 해결하지 않는다.

### 추가 승인 — 신규 wave diagnosis Info의 콘솔 stack 비용 제한

메인이 Plan을 검토하고 프로그래머 구현을 위임한 뒤 추가 승인한 범위다. 직접 읽은 `ConsoleSink.Write`의 일반 Info는 `Debug.Log(line)`으로 출력된다. 전 경기 상세 사건이 늘면 현재 stack 출력 설정에 따라 Logcat 줄 수와 비용이 커질 위험이 있으나, 실제 성능 악화나 렉 원인이 확정됐다는 뜻은 아니다.

- **ConsoleSink 내부의 좁은 분기만 허용:** 신규 WavePresentationDiagnostics가 쓰는 정확한 diagnosis 메시지 prefix의 시작 일치 + Info + exception 없음 조건에만 `LogOption.NoStacktrace`를 적용한다. 기존 TORRENT 로그 전체나 문자열 중간 포함 검색으로 범위를 넓히지 않는다. 최종 prefix/조건은 구현 보고에서 명시한다.
- `GameLog.Compose` 형식·로그 본문/구조화 데이터·sink 전달을 보존한다. FileSink 기록은 그대로이며 호출부는 계속 GameLog를 경유한다. 이 분기는 sink의 출력 구현이지 진단 호출부 raw Debug.Log 도입이나 새 sink 교체가 아니다.
- 전역 stack trace 설정 변경 금지. 다른 Info·Warn·Error의 기존 출력과 `exception != null`의 `LogException`/예외 객체·스택·기존 전역 예외 훅/재진입 방어는 모두 그대로 유지한다. 조건 불일치는 기존 경로로 처리하며 로그 자체를 생략하지 않는다.
- 신규 개발 로그의 conditional/릴리스 stripping 관습은 유지한다. stack 생략으로 줄이는 것은 이 prefix의 콘솔 부가 stack뿐이며, 확정 접촉 evidence·terminal chunk·관측 불완전성 기록을 줄이거나 성공 판정을 바꾸지 않는다. 규칙 근거: LogRules 1.4·1.7·1.8·1.9·1.14.
- 현재 document-manager는 Plan만 갱신하고 구현 실제 결과 인계를 기다린다. 코드·다른 문서·메모리·TC/QA·Unity/build 변경/실행 없음.

**에디터 경로 추가 승인:** 직접 확인한 FileSink.Write는 에디터에서 RuntimeLogger.Log로 전달하며 RuntimeLogger는 파일과 콘솔을 함께 출력한다. ConsoleSink만 바꾸면 이 경로의 stack 비용이 남으므로 RuntimeLogger의 **콘솔 emission만** 같은 신규 prefix/Info 한정 분기를 적용한다. FileSink 자체 수정은 범위에 추가하지 않는다. FileSink의 예외 경로는 message에 예외 내용을 합쳐 전달하므로, newline/예외 결합 메시지는 신규 prefix가 시작에 있더라도 기존 출력으로 유지한다. 정확한 단일 행 진단 메시지에만 적용하며 예외·오류 stack을 숨기는 경로를 만들지 않는다.

RuntimeLogger의 file writing·BeginSession/EndSession·session ownership·파일 오류 처리와 기존 `IsEmittingToConsole` 설정/복구(try/finally 포함)는 그대로 유지한다. 콘솔 branch를 위해 조기 return하여 파일 기록 또는 방어 플래그 복구를 건너뛰지 않는다. 전역 설정·다른 로그·예외 출력 불변 조건은 두 콘솔 경로 모두에 적용한다. 본 추가는 최소 구현 파일 1개 확장 승인 기록이며 구현 성공이나 비용 개선 실측을 주장하지 않는다.

### 메인 인계와 완료 경계

메인이 위 범위를 game-programmer에 위임한다. 이번 document-manager 작업은 Research/Plan 두 파일만 append/update하고 종료한다. **코드 구현·상시 문서·메모리·신규 Task/TC/QA·Unity/build는 이번 단계에서 수행하지 않는다.** 구현 실제 결과 인계 전 완료 수치·새 실기 성공/누락0·PASS/CLOSED·Supported 승격을 기록하지 않는다. 구현 보고에서 실제 변경 파일·출력 stage/사유·byte cap/chunk·epoch 종료 정책·관측 비용/overflow와 불변 gameplay 보존 근거를 메인에게 전달한다.

## 2026-10-02 구현 완료 인계와 계획 대비 잔여 경계

전 경기 사건 로그 구현은 인계됐으며 실제 변경 파일·사건/byte/state/clock 계약·컴파일 수치와 한계는 Research 「2026-10-02 로그 구현 결과 인계」를 따른다. 앞의 계획 작성·위임 대기는 이력이다. 기존 6파일 + ConsoleSink/RuntimeLogger, 신규 WavePresentationDiagnostics.cs/.meta가 구현 범위다. **전체 OPEN/C3 Unresolved·정확 시각 수용 OPEN**, Unity 메뉴·빌드·새 실기는 미수행이다.

- **구현:** 32 관측 cap 제거·causal 사건 무샘플링, primitive 제외 전이, writer before/immediateAfter/eventAfter/noDelta 근거, 실제 text emit 직후 clock과 scheduler clock 분리, 첫 retry 실패/성공/expiry, GameEnd snapshot·Presenter 자기 coordinator final·NetDespawn server/transport final.
- **출력:** `[WAVE-DIAG]`, 900byte full-line field chunk/Base64, Info만 신규 prefix/class NoStacktrace. Warn/Error/Exception/file/session/IsEmittingToConsole 기존 동작 유지.
- **observer state:** active wave 512·per-wave target 1024·overall target 8192, overflow explicit·causal 계속. transport/presenter/pool은 scope-only 집계다. 누적 512 파도 cap이나 gameplay 상한 변경이 아니다. role lifetime 캡처, 잘못된 delivery epoch에 scope reset 없음, fallback invalid key는 localWave 진단만.
- **계획 대비 미완:** Client payload normal/snapshot 직접 분리는 미구현(sender 결속 필요). single fallback server-owner final의 Bootstrap 2파일 연결도 미실행·이번 범위 밖이며 per-wave 상세는 존재한다. 이 두 항목을 완료로 체크하거나 승인 없이 추가 구현하지 않는다.
- **compile-agent 인계:** Runtime/Development 20 warnings/0 errors, Release 24/0, Editor 0/0. 메인 compile evidence 검토 진행 중; 문서 담당자가 컴파일·Unity·빌드·실기를 실행했다고 쓰지 않는다.
- **후속:** 사용자 테스트마다 상세 사건 로그를 수집·반복 대조하고 증거 있는 경로만 교정한다. Dev 로그는 task closure까지 유지, 테스트 전 cleanup으로 제거하지 않는다. 서버 authority/판정/hit-set/HP/Publish/VFX/clip 수치는 모두 불변이다. 새 결과 전 누락0/게임 PASS/Supported 승격/closure 없음.

규칙 근거는 기존 계획의 Units 28~31·44, NET-AUTH/TIME/TRAVELING 및 LogRules의 개발·sink·전이 기록 계약을 유지한다. 이번 문서 반영은 승인된 이행 사실과 잔여 범위 기록이며 신규 Task/TC/QA/Git·Unity/build 실행은 없다. 정합성 검사/baseline은 메인이 수행한다.


### 2026-10-02 추가 인계 — 최종 재컴파일과 실행 검증 구분

프로그래머 마지막 재컴파일 인계도 Runtime/Development warning20·error0, Release warning24·error0, Editor warning0·error0이다. 산출 로그는 `C:/Users/rmsep/AppData/Local/Temp/hexiege-wave-compile/runtime.compile.log`, `development.compile.log`, `release.compile.log`, `editor.compile.log`(뒤 세 파일도 같은 폴더)다. 이는 **구현 + 정적 컴파일 근거**이며 문서 담당자의 컴파일 실행 또는 새로운 Unity 메뉴/selftest·빌드·실기 결과가 아니다. 앞의 추가 근거 대기는 당시 인계 상태로 보존한다.

메인 검토 인계는 HP/Publish/RPC retry/VFX 이동의 변경 전후 동작 보존 확인이다. **900byte 필드 분할 코드는 있지만 최대 head 및 Base64 128+ 경계의 최종 출력 actual-execution 근거는 미확보**이므로 byte 상한 실측 PASS라고 쓰지 않는다. 실제 로그 출력/잘림·표현 성공과 새로운 게임 동작 검증은 OPEN이다. Client normal/snapshot 직접 분리 및 single fallback server-owner final의 미연결 경계도 유지한다. 기존 Task 폴더와 모든 과거 이력은 보존한다.


### 2026-10-02 메인 정적 byte 반례 — 수정 요청 / 최종 결과 대기

메인 정적 계산에서 극대 유한 시계의 `Double.MinValue:F6`를 포함한 head + Base64 128 metadata의 전체 행이 **944bytes > 900bytes**인 반례를 발견했다. 앞의 900byte 분할 구현 설명은 코드 의도/구현 인계 이력이지 상한 보장 완료가 아니다. **현재 byte 상한 보장은 미해결**이며, 프로그래머에 header G17 또는 dynamic chunk 예산/최종 guard 수정이 요청됐다. 어떤 방법을 채택·완료했는지는 최종 구현 결과 인계 전 확정하지 않는다. 이 정적 반례를 실제 기기 로그 잘림 발생 증거로 확대하지 않는다.

시계 도메인도 single은 `simulation-local`, network는 `synced-server`로 구분하도록 표기 교정 요청됐다. 기존 전체 clockDomain 표기를 모든 실행 모드의 동기화 서버 시계 보장으로 읽지 않는다. 수정 완료/재검증 인계 전 byte 상한 PASS·clock 표기 교정 완료를 기록하지 않는다. 새 Unity/selftest·빌드·실기 및 정확 시각 수용 미검증, 전체 OPEN/C3 Unresolved·single fallback final 미연결 경계는 그대로다.


### 2026-10-02 byte/clock 교정 적용 후 메인 검토 — 정적 재계산 확보

메인의 현재 소스 검토 및 검토 명령 실행 결과 인계: single `clockDomain=simulation-local` / network `clockDomain=synced-server`, header `now:G17` 적용과 최종 `bytes>900`의 명시적 `format-incomplete` guard 적용을 확인했다. 이전 944byte 반례 및 수정 요청/미해결 표기는 **그 당시의 이력**으로 보존하며 현재 적용 상태는 이 절을 따른다.

같은 극대 유한 시계 head + Base64 128 metadata 조건에서 impact도 `int.MaxValue`로 둔 재계산은 **659bytes < 900bytes**였다. 이는 메인 검토 명령 실행에 근거한 **해당 정적 구성의 계산 결과**이지 실제 GameLog/sink 출력·Android 실기·모든 입력의 byte 상한 실측 PASS가 아니다. 초과 시 format-incomplete를 드러내는 guard가 적용됐다는 것과 실제 출력 전체 검증을 구분한다.

**[🔴 2026-10-02 인계 수치 정정 — 원문 보존: 위 659는 메인의 메시지 타이핑 오류]** 실제 PowerShell 산출 출력은 `StaticExtremeBase64LineBytes=658`이다. 동일 극대 head/Base64 128 metadata/impact int.MaxValue 구성의 신규 직접 계산 근거는 **658bytes < 900bytes**만 채택한다. 문서 담당자의 재계산 또는 실제 GameLog 실기가 아닌 메인 산출 출력 인계이며, byte 상한 실측 PASS·최종 재컴파일 완료를 뜻하지 않는다.

**프로그래머 최종 수정 후 재컴파일 결과는 아직 대기**다. 앞의 Runtime/Development20·Release24·Editor0 warning/error0 인계는 이 최신 수정 뒤 재컴파일 증거로 대체하지 않는다. Unity 메뉴/selftest·빌드·새 실기 미수행, 정확 시각 수용 OPEN·전체 OPEN/C3 Unresolved, Client normal/snapshot 직접 구분·single fallback server-owner final 미연결 경계 유지.

### 2026-10-02 최종 구현 확정 — byte-format 재컴파일 인계 / 실제 실행 OPEN

**최종 메인 직접 확인:** G17/900byte final guard 적용, 4 `*.byte-format.compile.log` 모두 Exit0·error0(warning runtime20/development20/release24/editor0). `byte-format-bounds.log`의 보수적 fixture699/fallback400bytes는 정적 계산이며 실기가 아니다. 메인658 fixture와 프로그래머699 fixture는 조건이 달라 혼용하지 않는다. 이전 재컴파일 대기는 이력이다. 전체 OPEN/C3 Unresolved, build·Unity·device 미실행. 메인 checker 문제0 및 memory baseline document-manager1119/game-programmer3796 갱신 완료 인계. 추가 조사·메모리 수정 없이 문서 반영 종료.

최종 프로그래머 인계: **invariant G17 header와 full UTF-8 final guard 구현 완료**. 900byte를 초과하면 keyed `format-incomplete` warning과 faults 증가·coverage=false를 명시한다. single simulation-local/network synced-server 표기 교정도 적용됐다. 이전 944byte 반례·수정 요청·미해결·최종 재컴파일 대기는 당시 이력이며 **현재 구현/정적 컴파일 상태는 이 절로 대체**한다. 실제 출력 전 범위의 실측 PASS로 확대하지 않는다.

최종 산출물은 `C:/Users/rmsep/AppData/Local/Temp/hexiege-wave-compile/`의 `runtime.byte-format.compile.log`, `development.byte-format.compile.log`, `release.byte-format.compile.log`, `editor.byte-format.compile.log`다. 마지막 4개 모두 **error 0**, 기존 warning은 Runtime20/Development20/Release24/Editor0으로 인계됐다. 앞선 일반 `*.compile.log`와 구분하며 문서 담당자의 재컴파일 실행 결과가 아니다.

별도 `byte-format-bounds.log`의 보수적 **static fixture 최대 699bytes, fallback 400bytes**와 메인의 `StaticExtremeBase64LineBytes=658`은 **구성 조건이 다른 정적 근거**다. 같은 fixture의 전후 값으로 혼용하거나 한 값을 다른 값의 정정으로 덮어쓰지 않는다. 659는 메시지 오타 이력이고 944는 발견/교정 이력이다. 699/400/658은 Unity/GameLog/device 실측값이 아니다.

**최종 범위:** 로그 구현과 정적 컴파일 인계 완료. Unity 메뉴/selftest·실제 device는 미실행, build는 미시도이며 전체 OPEN/C3 Unresolved·정확 시각 수용 OPEN. Client normal/snapshot 수신 payload 직접 구분과 single fallback server-owner final Bootstrap 연결은 미완 경계로 유지한다. 서버 authority/판정/hit-set/HP/Publish/RPC retry/VFX 이동·원본 VFX/clip 수치는 보존한다. 테스트별 상세 Dev 로그 유지·증거 있는 경로만 반복 교정하는 사용자 선호도 유지한다. 추가 구현/TC/QA/Task/Git 없이 문서 정리를 마치며 정합성 검사/baseline은 메인 담당이다.
