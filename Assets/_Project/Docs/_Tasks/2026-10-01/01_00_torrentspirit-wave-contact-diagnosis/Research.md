# Research — TorrentSpirit 서버 파도 접촉·피해·회복 진단

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

이 문서는 경기 시작부터 종료까지 모든 TorrentSpirit 파도와 대상별 피해·회복, 실제 파도·텍스트 재생 결과를 연결하는 로그 확장의 근거를 정리한다. 기존 첫 32개 관측만으로 확인할 수 없던 접촉·표현 누락과 noDelta 사유를 구분하되, 서버 판정과 게임 동작은 바꾸지 않는다. 지금은 최종 구현 및 정적 컴파일 결과가 인계됐으며 아래 실기·측정 기록은 보존한다.

> **이전 서두 — 최종 확정 전:** 이 문서는 경기 시작부터 종료까지 모든 TorrentSpirit 파도와 대상별 피해·회복, 실제 파도·텍스트 재생 결과를 연결하는 로그 확장의 근거를 정리한다. 기존 첫 32개 관측만으로 확인할 수 없던 접촉·표현 누락과 noDelta 사유를 구분하되, 서버 판정과 게임 동작은 바꾸지 않는다. 지금은 구현 결과가 인계되고 추가 근거 검토가 진행 중이며 아래 실기·측정 기록은 보존한다.

> **이전 서두 — 구현 인계 전:** 이 문서는 경기 시작부터 종료까지 모든 TorrentSpirit 파도와 대상별 피해·회복, 실제 파도·텍스트 재생 결과를 연결하는 로그 확장의 근거를 정리한다. 기존 첫 32개 관측만으로 확인할 수 없던 접촉·표현 누락과 noDelta 사유를 구분하되, 서버 판정과 게임 동작은 바꾸지 않는다. 지금은 승인된 계획 작성 단계이며 아래 실기·측정 기록은 보존한다.

> **이전 서두 — 로그 확장 승인 전 이력:** 이 문서는 TorrentSpirit의 서버 파도와 화면 표현을 맞추는 승인된 교정의 근거와 남은 검증을 정리한다. 발사 시점·서버 폭과 이동 시간·즉시 HP 적용은 유지하고, 서버 파도 키와 접촉 기록에 따라 파도 및 피해·회복 숫자의 표현을 함께 관리한다. 17:59 방향 측정과 이후 두 경기 실기 근거가 확보됐으나 정확 시각 수용 및 전체 작업은 OPEN이다. 당시 Unity busy와 최종 메뉴·빌드 미실행 관찰은 아래 이력으로 보존한다. 아래 초기 로그 진단은 과거 이력이다.

> **이전 서두 기록 — 실기 반영 전:** 이 문서는 TorrentSpirit의 서버 파도와 화면 표현을 맞추는 승인된 교정의 근거와 남은 검증을 정리한다. 발사 시점·서버 폭과 이동 시간·즉시 HP 적용은 유지하고, 서버 파도 키와 접촉 기록에 따라 파도 및 피해·회복 숫자의 표현을 함께 관리한다. 17:59 방향 측정은 한정 표본에서 성공했지만 최신 Unity busy 약 8분으로 최종 메뉴 재검증과 빌드 시도는 미실행이며 전체 OPEN이다. 아래 초기 로그 진단은 과거 이력이다.

## 현재 경계

- 이전 작업 `2026-09-30/23_19_torrentspirit-attack-timing-correction`은 Attack 단일 발사 marker와 type 18 `hitFrameTimes`를 `2:10`(30fps, 70/30초)으로 맞췄다. 발사 뒤 서버 `waveTravelTime=0.5s`는 유지한다. production 자동 게이트 통과는 접촉 실기 증명이 아니다. `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md:114`도 `MigrationRequired / Unresolved / LegacyFallback`을 유지한다.
- `TorrentAttackBehavior.Apply`는 살아 있는 시전자·주 타깃이 없거나 XZ 방향 벡터가 사실상 0이면 생성 요청 없이 반환한다. 유효하면 공격자 월드 XZ 위치를 origin으로, 주 타깃 방향을 정규화해 폭·길이·이동 시간·힐값과 함께 `SpawnWave`에 전달한다. 기본 단일 대상 피해를 대체한다. `ExecuteAttack`에서 만든 `SpecialAttackContext`에는 주 타깃이 있으나 현재 `WaveSpawnRequest`에는 그 ID가 없어 `SpawnWave`/`ActiveWave`만으로는 ‘원래 노린 대상’을 식별할 수 없다. `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs:1562-1575`; `Assets/_Project/Scripts/Application/Combat/TorrentAttackBehavior.cs:40-74`, `WaveSpawnRequest.cs:29-62`.
- `UnitCombatUseCase.SpawnWave`는 유효 시전자일 때만 `ActiveWave`를 등록하고 travelTime을 양수 하한으로 보호하며, 우측 벡터·절반 폭·속도·연구 반영 힐량을 저장한다. 현재 `ActiveWave`에는 파도별 고유 ID나 대상별 접촉 ID가 없다. `UnitCombatUseCase.cs:2591-2625,2747-2781`.
- `TickWaves`는 서버 경과 시간마다 전선을 전진시킨다. 현재 `Units.Values`/`Buildings.Values`에 **들어 있는** 후보만 순회하고, null·사망은 좌표 계산 전에 건너뛴다. 이미 딕셔너리에서 제거된 대상은 순회 자체에 나타나지 않으므로 일반 후보별 `dead/null` 로그로 그 부재를 증명할 수 없다. 살아 있는 후보에서는 시전자·기접촉 대상을 먼저 빼고 월드 XZ 전방 투영 `p`, 전선 도달 여부, 좌우 투영을 순서대로 확인한다. 적 유닛·아군 유닛·적 건물을 별도 버퍼로 모아 효과를 적용하고, 유닛/건물 hit-set도 분리한다. 수집 후 적용 직전 사망한 대상은 건너뛸 수 있다. 완료 조건은 전선의 끝 도달과 경과 시간 충족이다. `not-yet-front`는 틱 중의 정상 대기 판단이며 그 자체로 파도 종료 시 미접촉 원인이 아니다. `UnitCombatUseCase.cs:2643-2733`.
- 피해 수렴점은 서버 `TakeDamage` 후 `OnEntityDamaged`를 발행하며 파도는 `immediatePresentation=true`다. 회복 수렴점은 `Heal` 후 `OnEntityHealed`를 발행한다. `NetworkHealthSync`는 서버 이벤트의 절대 HP를 RPC로 전달하며, 클라이언트 로그의 `AppliedDamage`/`AppliedHeal`은 서버 파도 자체의 접촉 기록이 아니라 클라이언트에서 실제로 적용된 **HP 차이**다. `UnitCombatUseCase.cs:1677-1710,1771-1784,2704-2724`; `NetworkHealthSync.cs:119-169,356-375,384-423`.
- `UnitView.OnAttackHit`는 로컬 표현 허용 경계를 지나면 `EffectManager.PlayUnitAttack`으로 VFX를 한 번 시작한다. 현재 파도 VFX는 시전자 부근의 1회 표현이고 서버 전선의 이동 위치와 동일하지 않다. 따라서 화면상 겹침만으로 서버 직사각형 접촉을 입증할 수 없다. `UnitView.cs:5183-5259,5307-5369`; `GameSystemRules_Units.md` 규칙 29·31 및 규칙 31 뒤 참고.
- 틱 진입은 싱글 `GameBootstrapper.Update`와 멀티 `NetworkCombatController`의 서버 가드로 갈린다. `GameBootstrapper.cs:544`; `NetworkCombatController.cs:615-618`.

## 이번 실기에서 확인되는 것과 비어 있는 것

- 동일 세션 키 `fd8f160e…44e82ee00`이 Editor Host의 `Assets/_Project/Docs/_Logs/_editor/2026-09-30/RuntimeLog.txt:8505`와 Android Client의 `Assets/_Project/Docs/_Logs/2026-10-01/00_10_logcat/RuntimeLog_device.txt:59219`에 있다. Host의 TorrentSpirit production-start-gate `Ready` 예는 Editor 로그 `8573`행이다. 이것은 파도 생성·접촉·효과 적용의 증거가 아니다.
- Android Client에는 유닛 0의 `AppliedDamage=200, Hp=150`(`74114`행), 유닛 5의 `AppliedHeal=100, Hp=980`(`75373`행)이 있다. 일부 피해·회복 동기화가 이 세션에서 작동했다는 증거지만 어느 파도, 어느 서버 좌표·전선, 어떤 제외 판단에서 왔는지 현재 행만으로 연결할 수 없다.
- 현재 파도별 생성/종료, 후보별 좌표·투영·제외 사유, 접촉 시 HP 전후, VFX marker와 서버 파도의 일대일 대응 로그가 없다. 주 타깃 ID를 진단용으로 발사 시 보존하지 않거나 사용자가 본 대상 ID를 별도로 특정하지 못하면, 순회에서 보이지 않은 대상이 사망·제거됐는지, 애초에 후보가 아니었는지 구분할 수 없다. 기존 Client HP sync 로그도 waveId가 없는 전역 HP 변화 기록이어서 동일 대상·근접 시각만으로 파도와 확정 연결할 수 없다. 사용자가 본 ‘닿았는데 효과 없음’의 발생 횟수·대상 ID·서버 판정 이유·표현과 판정의 시간/좌표 차이는 미확정이다. 발사 실패, 기하학적 비접촉, 적용 직전 생명주기 변경, HP 변화 0, 동기화·표현 차이 중 어느 쪽인지 단정하지 않는다.

## 규칙과 범위

`GameSystemRules_Units.md` 규칙 28~31은 special-only, 서버 전선·대상별 1회, 힐 채널, VFX 독립성을 규정한다. `GameSystemRules_UnitCombatSynchronization.md` `NET-DELIVERY-TRAVELING`은 장차 같은 공격 회차 안의 대상별 첫 접촉을 독립 HitIndex 또는 명시적 접촉 인덱스로 식별하도록 요구한다. 이번 작업은 **진단 로그**이며 그 권위 계약의 구현 완료나 `Supported` 승격이 아니다. VFX 이동 구현은 별개 후속 작업이다. 코드·Testcase·QA·메모리·상시 문서는 이번 승인 범위 밖이다.

## 교정 준비 조사 (2026-10-01 추가 — 초기 진단 보존)

화면에 보이는 파도를 서버 판정에 맞추는 교정 준비다. 파티클을 보고 서버 판정을 바꾸는 작업이 아니며, 기존 VFX 프리팹은 그대로 둔다. 표현의 폭·높이·속도와 시작 기준을 어떻게 맞출지는 실제 치수와 생산 경로를 확인한 설계를 받은 뒤 확정한다.

**[🔴 2026-10-01 정정 — 원문 보존: 앞의 ‘VFX 이동 구현은 후속’ 및 이동하지 않는 표현을 전제로 한 설명은 현행 구현 근거가 아니다.]** `GameSystemRules_Units.md` 규칙 31 뒤 참고도 현재 VFX가 이동하지 않는다고 적혀 있지만, 아래 실제 에셋과 이동 코드가 그 구현 설명과 불일치한다. 규칙 29의 서버 판정 권위 자체는 유효하다. 상시 문서 수정은 메인 세션의 후속 결정 대상이다.

### 확인한 생산 데이터와 실행 경로

| 경계 | 현재 확인 사실 | 근거 |
|---|---|---|
| 서버 영역 | 폭 3 WU, 반폭 1.5 WU, 전방 길이 3 WU, 이동 시간 0.5초. 계산 속도 6 WU/s. origin은 시전자 월드 XZ(Y=0), forward는 시전자→주 타깃의 정규화 XZ 방향이다. | `Resources/Config/SpecialAttackConfig.asset`; `Application/Combat/TorrentAttackBehavior.cs` `Apply`; `Application/UseCases/UnitCombatUseCase.cs` `SpawnWave`/`TickWaves` (모두 `Assets/_Project/` 아래) |
| VFX 생산 바인딩 | `UnitEffectConfig.asset` type 18의 attackPreset GUID `06aeda7f35f745a9b948be020f6acdc0`은 `EffectPreset_TorrentSpirit_Attack.asset.meta`와 일치한다. | `Assets/_Project/Resources/Config/UnitEffectConfig.asset:96-97`; `Resources/Config/EffectPresets/EffectPreset_TorrentSpirit_Attack.asset.meta` |
| VFX 프리셋 | 로컬 위치 오프셋 `(0, 0.05, 0.4)`, Euler 오프셋 0, 스케일 배율 `(1,1,1)`, 전진 거리 3 WU/시간 0.5초다. **배율 1은 보이는 파티클 폭·높이가 판정 영역과 같다는 뜻이 아니다.** | `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_TorrentSpirit_Attack.asset` |
| 로컬 시작·방향 | 허용된 로컬 Animation Event가 `EffectManager.PlayUnitAttack`을 호출한다. 입력 위치는 VfxSpawnPoint 또는 PresentationTransform 위치, 기본 방향은 PresentationTransform.forward다. 일반 경로에는 권위 표현 소유 시 방향을 사용하는 분기도 있으나, TorrentSpirit 파도 생성 요청과 같은 origin/forward/time/ID를 전달하는 경로라고 확인되지는 않았다. | `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` `OnAttackHit`; `Presentation/Effects/EffectManager.cs` `PlayUnitAttack` |
| 로컬 이동 | `VfxPoolItem.Play`의 실제 시작은 `pos + rot * localPositionOffset`, 스케일은 프리팹 원본 스케일×배율이다. 이동 방향은 `rot * Vector3.forward`; `UpdateForwardTravel`은 로컬 `Time.deltaTime` 누적으로 루트를 선형 이동한다. 살아 있는 파티클이 없으면 풀로 반환하므로 설정 시간의 실제 시각 완주도 별도 관찰 대상이다. | `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs` `Play`/`UpdateForwardTravel`/`Update` |

**정적 결론:** 이동 거리/시간 설정은 서버와 표현 모두 3/0.5지만, 시작점 오프셋·시계·방향 출처·식별은 별개다. 이 차이는 정합 교정의 확인 대상이며 제보된 누락의 유일 원인이라는 증거는 아니다. 서버의 XZ 판정에는 시각 높이 조건이 없다. 파티클의 보이는 가로 폭, 세로 높이, 진행 방향 두께·전선 위치는 미측정이며 임의 스케일·높이를 정하지 않는다.

### 진단 추가 뒤 실기 근거 — 이전 경기와 분리

- Editor `Assets/_Project/Docs/_Logs/_editor/2026-10-01/RuntimeLog.txt:57`과 Android `Assets/_Project/Docs/_Logs/2026-10-01/05_11_logcat/RuntimeLog_device.txt:744`는 같은 키 `f2425e8e…234b9ddb`, 각각 Host/Client다. 앞의 `fd8f160e…44e82ee00` 경기와 섞지 않는다.
- Editor `141`행: wave 1, caster 7 Red, primary Unit 3, origin `(4.500,-3.380)`, forward `(0,-1)`, 폭/길이/시간 `3/3/0.5`. `161`행은 해당 파도의 대상 3 HP `350→150`, `172`행은 종료 `elapsed=0.631`, `front=3`, `detailDropped=109`, `truncated=True`다. 일부 실제 피해는 확인되지만 전체 후보를 완전히 기록한 증거가 아니며, 0.5초 설정을 정확한 프레임 완주로 일반화하지 않는다.
- Editor `143`행의 caster 7 로컬 재생은 `Started`, `systems=5`, `active=True`, `noWaveId=true`다. Android `16348`행도 caster 7의 로컬 재생 성공과 `noWaveId=true`를 기록한다. 이 값은 재생 직후의 상태이지 보이는 파티클 경계·접촉·종료 동기화 증명이 아니다. Red 반전 좌표와 서로 다른 로컬 단조 시간을 변환 없이 직접 빼거나 파도와 marker의 일대일 관계로 확정하지 않는다.
- 현행 `WaveSpawnRequest`에는 진단 전용 주 타깃 종류/ID가 있고 `ActiveWave`에는 서버 로컬 진단 ID와 후보·적용·종료 기록이 있다. 따라서 초기의 ‘ID·로그 없음’은 진단 추가 전 이력이다. 이 ID는 정식 AttackSequenceId/HitIndex나 클라이언트 파도 ID가 아니다.

### 단위·미확정·다음 경계

- `HexMetrics.HexToWorldFlatTop`의 열 간격은 `TileWidth×0.75`이고 `Resources/Config/GameConfig.asset` FlatTop TileWidth는 1이다. 이 설정에서 3 WU는 `3/0.75=4` **열 간격**이다. ‘4칸’은 임의의 4 WU나 모든 방향에서 네 타일이라는 뜻이 아니다. 세로/대각·실제 접촉은 월드 XZ 투영으로 확인한다.
- 기존 30fps `2:10` 발사 marker와 hitFrameTimes는 이번에 바꾸지 않는다. 따라서 이 교정 준비에 클립 타이밍을 이유로 StatsReference를 변경할 필요는 없다. 최종 설계가 다른 수치를 변경한다면 메인 세션에서 관련 문서 범위를 다시 확인한다.
- 사용자 승인 범위는 설명된 정합 교정 및 기존 Research/Plan 갱신이다. **최종 생산 설계·정확한 수정 파일·스케일/높이 값·구현 결과는 대기 중**이다. 서버 권위 최우선, VFX 프리팹 무변경, Testcase/QA 미작성, 전체 migration/Supported 승격 및 PASS/CLOSED 판정 없음.

## 승인된 생산 설계 인계 (2026-10-01 추가 — 구현 검토 전)

메인 세션에서 프로그래머 설계가 전달됐고 기존 사용자 승인이 구현을 포함한다고 확인했다. 앞의 설계 대기 표기는 이 인계 전 상태로 보존한다. 현재는 **서버 파도 전용 adapter 설계 수령 / 구현 진행 / 검토 OPEN**이며 구현 완료나 실기 결과로 기록하지 않는다.

- 설계 식별자는 `MatchEpoch + WaveDeliveryId`다. 기존 진단 번호를 AttackSequenceId로 위조하거나 TorrentSpirit을 Supported로 승격하지 않는다. 서버의 실제 생성 origin/aim/time, 폭 3·길이 3·시간 0.5와 접촉별 확정 snapshot을 불변 데이터로 전달한다.
- 현행 서버 `SpawnWave`/`TickWaves`는 Application 소유이고, 네트워크 서버 틱·수명 경계는 Infrastructure의 `NetworkCombatController`에 있다. `NetworkHealthSync`는 HP 이벤트를 RPC로 수렴시킨다. `FloatingHpTextSpawner`는 힐 이벤트 직접 구독 경로도 갖고 있으므로, 파도 전용 표현 소유권과 기존 텍스트 방출을 함께 검토해야 이중 표시를 막을 수 있다. 생산 설계는 spawn/contact를 Events와 NetworkCombatController로 연결하고 HP 즉시 수렴과 표현 예약을 분리한다.
- 공유 메모리의 현행 아키텍처 제약을 따른다: 조합은 Bootstrap만, Application의 Infrastructure 구체 타입/Unity.Netcode 역참조 금지, NetworkBehaviour는 Infrastructure만, Client Simulation Root writer 추가 금지. Events/계약/coordinator는 Application, 네트워크 adapter는 Infrastructure, WavePresenter/풀 재생은 Presentation으로 책임을 나눈다.
- 파도와 **확정 접촉 HP 텍스트**만 Host/Client 공통 표현 시간축에 결합한다. 전체 HPbar·사망·공격 애니메이션 타이밍이 동기화됐다고 주장하지 않는다. 실제 파티클 치수·leading-offset과 보정값은 deterministic seeded 측정 및 구현 검토 전까지 미확정이다.
- 구체적 생산 설계와 검토 경계는 Plan의 「승인된 생산 설계」를 따른다. 이 문서 작업은 코드·프리팹을 변경하지 않는다.

## Unity 측정 중간 결과 (2026-10-01 13:18:42 전달 — 최종 폭 미확정)

> **[🔴 2026-10-01 13:29:11 정정 — 원문 보존 / 측정 신뢰 보류]** 메인 전달의 추가 좌표계 probe가 **FAIL**했다. `BakeMesh` world-coordinate translation probe의 `Main_Water_Surge` error는 **1.188486**이다. 아래 CSV 값의 산술 재집계 사실은 보존하지만, 측정 좌표계가 유효하다는 뜻은 아니다. 기존 ‘root-width 변화 0’과 선두 잔차·holdout을 실제 효과 또는 유효한 최종 보정 근거로 사용하지 않는다. 좌표계/cached renderer transform 문제는 **진단 중인 가설**이지 확정 원인이 아니다. 도구 좌표계 교정 및 probe 재검증 후 재측정이 필요하다.

측정 파일 `Library/TorrentWaveMeasurements/calibrated-20261001-041842-820.csv`를 직접 읽고 phase별 유효 `ALL_SYSTEM_UNION` 행의 `maxX-minX` 최댓값과 X 최대 절댓값, 기록된 `coreFrontResidual` 범위를 재집계했다. 파일의 boundsKind는 **`baked-renderer-geometry-not-alpha-silhouette`**다. 즉 아래 폭은 baked 렌더러 기하의 표본 경계이며, 파티클의 실제 불투명 윤곽이나 서버 접촉 폭을 측정했다고 주장하지 않는다. Library 아래 파일의 장기 보존 여부도 아직 확인하지 않았다.

| phase | 최대 표본 폭(WU) | X 최대 절댓값(WU) | 선두 잔차 범위(WU) |
|---|---|---|---|
| original-fit | 3.977209 | 2.041061 | [0.671276, 1.185536] |
| corrected-fit | 3.977209 | 2.041061 | [-0.257130, 0.257130] |
| original-holdout | 3.906254 | 1.996059 | [0.682709, 1.158389] |
| corrected-holdout | 3.906254 | 1.996059 | [-0.245697, 0.229983] |

- 메인 전달에 따르면 static root X scale `0.157548` 후보를 적용해도 폭 변화가 0이어서 후보를 폐기했다. CSV의 동일 phase군 폭/X 경계가 전후 동일함은 직접 확인했다. **재현 효과가 없는 root-width 보정은 채택하지 않는다.** root transform 배율만으로 목표 폭을 맞췄다는 결론은 철회 대상으로 명시한다.
- 메인 전달 Z offset `-0.528406` 후보는 fit 선두 잔차를 ±0.257130으로, 독립 holdout을 [-0.245697, 0.229983]으로 줄였다. 잔차 범위는 CSV에서도 확인했다. 이것은 후보의 측정 근거이며 실제 runtime helper 경로에서의 최종 보정·접촉 시각 정합 PASS가 아니다.
- 초기 radius 기반 추정 폭 **약 4.03**(메인 전달 추정)과 이번 baked 기하 표본 폭 **3.977209**(CSV 직접 집계)은 측정 방법과 의미가 다르다. 어느 하나를 다른 값의 정정으로 덮어쓰거나 최종 런타임 폭으로 채택하지 않는다. 표의 ‘최대 표본 폭’과 ‘최대 |X|’도 서로 다른 지표다.
- **실제 runtime helper 측정 전까지 최종 width 미확정 / 구현 검토 OPEN**. 서버 width=3·length=3·travel=0.5는 유지한다. Unity 전체 gate, Host/Client 실기, 사용자 시각 수용 및 상시 문서의 최종 결과 반영은 이 중간 측정만으로 완료 처리하지 않는다.

**13:29:11 이후 현행 해석:** static root X scale 0.157548 후보는 여전히 생산 채택하지 않지만, 이전 측정만으로 ‘실제 폭에 효과 없음’을 최종 확정하지 않는다. Z offset -0.528406 및 그 잔차 범위도 신뢰 보류이며 채택 근거로 쓰지 않는다. 초기 radius 추정 약 4.03과 baked 집계 3.977209는 서로 다른 과거 지표로만 보존한다. 이번 probe 수치·FAIL은 메인 전달 근거로 기록했으며 이 문서 작업에서 probe를 독립 재실행하지 않았다. **도구 좌표계 신뢰 회복 → 재측정 → 실제 runtime helper 검증** 전까지 최종 geometry/width 미확정, 구현 검토 OPEN이다.

## 현행 구현 파일과 13:33:06 검증 경계 (2026-10-01 추가)

아래는 메인이 전달한 실제 코드 파일 목록이다. 신규 파도 계약·presenter·calibration·네트워크 partial 및 Editor 도구의 파일 존재를 직접 확인했다. **존재 확인은 구현 완성 또는 gate 통과가 아니다.** 모든 파일의 전체 동작 검토나 변경 전후 hash 대조는 이 문서 작업에서 수행하지 않았다.

| 계층/역할 | 실제 경로 |
|---|---|
| Application 계약·coordinator | `Assets/_Project/Scripts/Application/Combat/WavePresentationContracts.cs`, `WavePresentationCoordinator.cs` (같은 폴더) |
| 서버 파도 구현 | `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.WavePresentation.cs`, `UnitCombatUseCase.cs` (같은 폴더) |
| 이벤트 | `Assets/_Project/Scripts/Application/Events/GameEvents.cs` |
| 네트워크 전달·HP 수렴 | `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.Waves.cs`, `NetworkCombatController.cs`, `NetworkHealthSync.cs` (같은 폴더) |
| 표현·calibration·풀·기존 큐 | `Assets/_Project/Scripts/Presentation/Effects/WavePresentationPresenter.cs`, `WaveVisualCalibration.cs`, `VfxPoolItem.cs`, `EffectManager.cs`, `HitPresentationQueue.cs` (같은 폴더) |
| 텍스트·marker | `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`; `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` |
| 조합·맵 수명 | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs`, `GameBootstrapper.Map.cs` (같은 폴더) |
| 기존 gate·측정 도구 | `Assets/Editor/Tools/TorrentWaveProductionValidation.cs`, `TorrentWaveCalibrationTool.cs` (같은 폴더) |

- **메인 전달 13:33:06:** runtime 검증에서 pool seed restore order가 FAIL했고 수정됐으나 최종 재실행 대기다. 현재 `VfxPoolItem`에는 저장한 `randomSeed`를 먼저, `useAutoRandomSeed`를 그 뒤에 복원하는 순서가 보인다. 소스상 순서 확인은 runtime 재검증 PASS가 아니다.
- 메인/프로그래머 전달의 actual-server/coordinator fixture 통과는 해당 부분 근거로만 기록한다. **overall gate는 실패했으므로 전체 PASS 아님**. 좌표계 probe 실패에 따른 이전 기하 측정 신뢰 보류도 새 성공 근거가 오기 전 해소하지 않는다.
- 메인은 원본 clip/prefab hash 보존을 전달했다. 여기서 독립 before/after hash 검증을 했다고 표기하지 않는다. 클립 값·원본 VFX 프리팹 무변경 경계를 유지한다.
- **최종 runtime 재실행·측정·Unity 결과 대기 / 구현 검토 OPEN**. StatsReference 및 Units의 최종 사실 반영은 그 결과 뒤에 진행하며, Supported migration·전체 HPbar/사망/애니메이션 동기화 완료 판정은 없다.

### 13:38:51 실제 Unity 메뉴 재실행 인계 — 부분 gate 통과 / 전체 OPEN

메인 전달에 따르면 pool seed 수정 후 실제 `RunUnitActionSelfValidation` 메뉴를 재실행했고, UAS-DIAG·PRODUCTION-TIMELINE·C2·C3 전부 PASS 및 TorrentProduction PASS가 보고됐다. 메인은 관련 로그를 확인 중이며, 문서 담당자는 이 인계를 실제 메뉴 재실행 결과로 기록하되 독립 로그 감사 완료로 표기하지 않는다. 앞의 13:33:06 overall gate FAIL/재실행 대기는 그 시점의 이력으로 보존한다. **이번 재실행 gate 통과를 geometry 최종 확정이나 전체 작업 PASS로 확대하지 않는다.**

Console Error는 0, Warning은 21건(기존 20 + gate fixture Warn 1)으로 전달됐다. fixture 경고 내용은 확인 예정이므로 예상 경고·무해한 경고라고 아직 확정하지 않는다. **runtime warning zero PASS가 아니며**, 실제 Host/Client 경기 무경고 증거로도 사용하지 않는다. 최종 geometry/width는 미확정이고 이전 측정 신뢰 보류는 유효한 재측정 근거가 올 때까지 유지한다. **전체 작업 OPEN**, 상시 문서 최종 반영은 최종 측정 및 결과 확인 후다.

### 13:40:33 필수 메뉴 확인 후 인계 — 현재 코드 기준 PASS / 전체 OPEN

메인이 실제 `Self Validate Unit Root Pose Cross Audit` 메뉴의 13:40:33 PASS를 확인했다. 13:38:51 `TorrentWaveProductionValidation` PASS도 확인됐으며 원본 clip/prefab 양쪽 source hash unchanged 확인을 포함한다고 전달했다. 앞의 ‘로그 확인 중’은 이 인계 전 이력이다. 문서 담당자의 독립 hash 감사나 메뉴 실행으로 표기하지 않는다.

**현재 런타임 코드 기준 두 필수 메뉴(Unit Action selfvalidation / Unit Root Pose Cross Audit)는 PASS**다. 최종 geometry 수치가 추가되면 변경 후 코드 기준으로 필수 메뉴를 다시 확인할 예정이다. 메뉴 통과를 최종 geometry 측정 gate 해소나 경고 0건으로 해석하지 않는다. 외부 headless 검증·Host/Client 실기·사용자 시각 수용은 미완료이며 **전체 작업 OPEN**이다.

### 13:41:08 좌표 mapping v2 교정 인계 — production 재측정 전

`Library/TorrentWaveMeasurements/coordinates-20261001-043730-837.csv`를 직접 읽고 `options=0`의 `transformPointError`를 scaling별로 집계했다. **Local/Hierarchy 각각 6 pose에서 최대 오차 0**이며, 메인 전달의 공식 옵션 `flags=0` + `ParticleSystem.transform.TransformPoint` **1회** 변환 검증과 일치한다. 이는 이 좌표 probe 범위의 성공 근거이지 production geometry 최종 측정값이 아니다.

메인은 기존 `flags=3` 결과를 raw world 좌표로 가정한 측정 경로를 부모 scale 및 cached renderer transform 갱신 문제로 폐기하고, 13:41:08 tool mapping v2 교정의 컴파일 오류 0을 전달했다. CSV에는 flags3의 pose 변경 후 `none`/`simulate-zero` 사이 오차 차이가 보이지만, 모든 원인의 독립 확정 진단을 문서 담당자가 수행했다고 주장하지 않는다.

현재 production helper 메뉴 재측정 준비 중이다. **이전 calibrated CSV의 폭·선두 잔차는 여전히 신뢰 보류**이며 좌표 probe 성공만으로 재유효화하지 않는다. 새 production 수치와 독립 검증 결과 이후 최종 geometry 및 상시 상태 문서를 반영한다. 전체 작업 OPEN을 유지한다.

### 13:44:44 mapping v2 runtime helper 재측정 — 후보 미달 / 생산값 승인 대기

> **당시 상태 (17:59:49 최신 절로 대체):** 아래 승인 pending·최종 값 미확정은 13:44:44 당시 기록이다. 현행 .78 적용 및 한정 방향 측정 결과는 「17:59:49 최종 방향 측정」을 따른다.

새 파일 `Library/TorrentWaveMeasurements/runtime-helper-20261001-044444-281.csv`를 읽고 유효 `ALL_SYSTEM_UNION` 표본의 최대 폭·최대 |X|·최대 Y 높이 및 기록된 선두 잔차를 phase별 재집계했다. boundsKind는 **`bake-flags0-PS-local-to-world-once-not-alpha-silhouette`**다. 새 mapping v2 helper 측정 근거로 기록하며, 신뢰 보류된 이전 CSV를 소급 유효화하지 않는다.

| runtime helper phase | 최대 표본 폭(WU) | X 최대 절댓값(WU) | 최대 Y 높이(WU) | 선두 잔차(WU) |
|---|---|---|---|---|
| runtime-identity-fit | 3.977209 | 2.041061 | 1.005499 | [0.671276, 1.185536] |
| runtime-identity-holdout | 3.906254 | 1.996059 | 1.029019 | [0.682709, 1.158389] |
| runtime-candidate-fit | 2.922943 | 1.500076 | 1.005499 | [-0.257130, 0.257130] |
| runtime-candidate-holdout | 2.882641 | 1.470601 | 1.029019 | [-0.245697, 0.229983] |

- 메인 전달 후보는 **X=0.782384、anchor=0.928406、Hierarchy=true**다. 전달 fit 폭 2.922942는 원문 인계값으로 보존하며, CSV 행의 `maxX-minX` 직접 집계는 2.922943이었다. 0.000001 차이를 조용히 덮어쓰지 않는다. 주요 결론은 동일하다: fit 최대 |X|가 서버 반폭 1.5를 **0.000076 초과**하며 대칭 전체 경계 환산 초과는 **0.000152**, 따라서 폭 gate 미달이다. 총 표본 폭이 3보다 작다는 이유만으로 중심 기준 반폭 gate를 통과시키지 않는다.
- holdout 최대 |X|=1.470601은 반폭 안이다. 높이 집계는 원본/후보 모두 fit 1.005499·holdout 1.029019로 같다. 최종 Y 축 배율은 1 유지 경계이며, 서버 width=3 WU를 표현에 맞춰 축소하지 않는다.
- fit 선두 잔차 ±0.257130 WU를 6 WU/s로 나누면 약 **±0.042855초(±0.0429초)**다. holdout은 [-0.245697, 0.229983] WU다. 이는 기하 선두 잔차의 속도 환산이지 실제 타깃 접촉 오차 측정이나 완전 접촉 보장 아니다. transparent sprite의 baked geometry는 alpha 윤곽과 다르고 서버 접촉은 틱 표본이다.
- **생산 후보 X=0.78f·anchor=0.928406·Hierarchy=true 채택 요청**은 메인 전달 기준 **승인 pending / 재측정 미확인**이다. 0.782384 후보 측정을 0.78의 결과로 대체하지 않는다. 실제 production 값 적용·새 helper 측정·필수 메뉴 재확인 전 최종 width/geometry 미확정, 전체 작업 OPEN이다.

## 17:59:49 최종 방향 측정 (2026-10-01 KST — 한정 표본 gate 통과)

`Library/TorrentWaveMeasurements/directions-20261001-085949-284.csv`의 production/identity 표본을 직접 집계했다. 현행 `WaveVisualCalibration.Production`은 **X=.78f·FrontAnchor=.928406f·Hierarchy=true**다. `Scale`은 X만 곱하고 Y/Z 원본 배율을 유지하며 `Offset`은 preset 전방 offset에서 anchor를 뺀다. 앞의 승인 pending·최종 폭 미확정은 당시 이력이다. **현행 적용값의 한정 방향 기하 검증은 확보됐으나 전체 작업은 OPEN**이다.

| 측정 범위 | fit | holdout |
|---|---|---|
| Blue/Red × yaw 0/90/+45/-45/180 각각 production | lateralWidthAchieved=True, centeredExcess=0 | lateralWidthAchieved=True, centeredExcess=0 |
| 위 방향들의 production 최대 lateral 절댓값(WU) | 1.494449 | 1.465140 |
| identity / production 최대 Y 높이(WU) | 1.005499 / 1.005499 | 1.029019 / 1.029019 |

측정은 fit seed 1701·2903·4517·7919와 holdout seed 10007·13001·19001·23003, 고정 camera(55°·ortho 8·9:16) 및 요청한 wave-frame의 lateral/up/forward 축에 한정된다. Blue/Red는 표현 좌표 반전 표본이지 실제 network Host/Client 역할교대 경기 아니다. boundsKind는 `bake-flags0-PS-local-to-world-once-wave-frame-not-alpha-silhouette`이며 transparent sprite의 alpha 윤곽·눈으로 본 접촉을 증명하지 않는다. 임의 방향/다른 camera·seed를 보장하지 않는다.

- 기존 invalid calibrated CSV는 신뢰 보류 이력으로 유지한다. mapping v2 probe와 새 production helper/direction 측정은 별개의 신규 근거다. 0.782384 fit 미달을 지우지 않고 .78 최종 표본 결과로 구분한다. 선두 잔차 약 ±0.0429초 속도 환산은 완전 접촉 동시성 보장이 아니다.
- 서버 origin/aim/실제 spawnTime, 폭 3 WU·길이 3 WU·시간 .5초·속도 6 WU/s 및 대상별 hit-set은 유지한다. 전선은 serverNow-spawnTime 기준이지만 접촉은 서버 틱의 현재 위치 표본이므로 **서버 stall 중 접촉이 늦어질 수 있다**. 렉 원인/해결은 별도이며 이 측정으로 성능 해결을 주장하지 않는다.
- **발사 2:10@30fps=70/30초, cycle 4초 유지**. 원본 VFX 프리팹·클립 무변경, 권위 HP 즉시 적용, 새 owner 활성 시 전용 source marker/legacy text 방출 억제 경계를 유지한다. Unresolved/LegacyFallback/C3 migration OPEN이며 HPbar·사망·전체 애니메이션 timing 동기화 완료 아니다.
- **[18:06 정정 — 앞선 ‘재실행 중’ 표기는 오기] 최종 값 이후 두 필수 Unity 메뉴 재실행과 빌드 시도 모두 미실행**이다. 앞선 13:38/13:40 PASS는 당시 코드 근거로 보존한다. 외부 headless·Host/Client 실기·사용자 시각 수용 미완료. 추가 TC·new Task·QA 없음.

### 18:06 Editor 상태 — 측정 완료와 정상 복귀 분리

> **18:15 최종 UI 확인(메인 전달):** `Application.UpdateScene` busy 15:21 지속. 승인된 최종 두 메뉴 재실행·빌드 시도는 busy로 여전히 미실행이며, Editor 정상 복귀 미확인·전체 OPEN을 유지한다.

메인 UI 최종 확인은 `Application.UpdateScene` busy **06:19**, 최종 두 필수 메뉴 재실행 및 빌드 시도 모두 **미실행**이다. 프로그래머 읽기 전용 확인 인계는 loop/CSV 저장/최종 로그 이후 오류 없음, Unity Responding=true, 나머지 Unity 2개는 AssetImportWorker, 반복 update 등록 없음 및 finally 복구였다. 이것은 메인 전달 근거이며 원인 확정이나 Editor 정상 복귀 증거가 아니다. 원본 4개 hash 전부 unchanged 확인 완료도 메인이 전달했으며, 문서 담당자의 독립 hash 비교로 표기하지 않는다.

최신 메인 전달은 Unity busy 약 8분이다. 메뉴 재검증과 빌드 시도는 사용자 승인 범위의 남은 작업이나 busy로 실행 불가하여 모두 미실행이다. 추가 진단·강제 종료만 이번 범위 밖이다. 문서 담당자는 승인된 문서 반영·정합성 검사로 마무리하며, 한정 방향 측정 완료와 Editor 정상 복귀·최종 메뉴 통과를 구분하고 **전체 OPEN**을 유지한다.

## 두 경기 실기 결과와 근거 한계 (2026-10-01 최신 — 전체 OPEN)

사용자 승인된 분석 인계 및 메인이 검토한 읽기 전용 코드 조사 결과를 반영한다. 이 문서 담당자의 독립 로그 재집계·Unity 메뉴/빌드 확인을 뜻하지 않는다. **두 경기 실기 근거 확보 / 정확 시각 수용 OPEN / 전체 OPEN·C3 Unresolved**다. 18:15 busy·당시 메뉴/빌드 미실행은 과거 관찰로 보존하며, 현재 실기 미확보라는 뜻은 아니다. 넓은 분포의 사용자 실기는 진행됐지만 최종 시각 수용은 언급되지 않았다.

- **이전 22:13:** sharedSession `9442c1dde44699d3c17b64c5b237a10de60532f071333cb843e1a982f37e85cd`; 파도 spawn/end 및 Host/Client display 각각 17, HP 변화 45·Client heal 4·noHpDelta 0·detail limit/drop 977. 양쪽 local ROOT PASS·ERROR 0.
- **최신 22:29~22:33:** sharedSession `83abb2a548f55fe0d44f30a71a7fd74cb2ec4bfc376cbaf24af1a3b1f5f08199`. 근거: `Assets/_Project/Docs/_Logs/2026-10-01/22_36_logcat/RuntimeLog_device.txt` + `Assets/_Project/Docs/_Logs/_editor/2026-10-01/RuntimeLog.txt`의 동일 session. 22_35는 겹친 같은 경기 캡처이며 별도 합산하지 않는다. 누적 생산 138 = TorrentSpirit Red 10/LittleKnight Blue 37/Pistoleer Blue 91로 동시 생존 수가 아니다.
- **확대:** 첫 32개 파도 spawn/end 및 양쪽 display 각각 32, 그 범위 HP 변화 121/noHpDelta 11. 전체 Client heal 36(전회 4). 관측 표본 17→32·HP 변화 45→121·Client heal 4→36이며 모든 heal이 같은 관측 파도와 일대일 결속됐다는 뜻은 아니다.
- **cap:** 22:31:45.135 `maxWaves=32 / subsequentWavesUnobserved`, display도 32 cap이다. 32는 경기 전체 발사 수가 아니다. `detailDropped=7772`·outside-width 틱 표본 567(고유 target 수 아님)으로 이후 파도·전체 접촉/표현 누락 여부를 판정할 수 없다.
- **11건:** 원인 상세 미확정. fullHP heal일 가능성은 가설로만 유지하며 오류·누락·fullHP로 확정하지 않는다. DamageCalculator는 양수 공격력을 방어 감쇄 후 최소 1로 유지하므로 11건을 방어력 때문이라고 설명하지 않는다.
- **공통 진단:** ERROR/clock/contract failure 0, 양쪽 local ROOT PASS. Host WARN 2 = ROOT 64 cap + 종료 disconnect로 전 유닛 관측/공식 CrossAudit PASS가 아니다. 공통 C3 results/ready/released 각각 83·failures 0은 Torrent wave 전용 결과나 migration 완료 수치가 아니다.
- **오류 0의 한계:** 읽기 전용 조사에서 WavePresentationCoordinator.Tick의 일부 expiry는 Expired만 증가시키고 Failures는 증가시키지 않으며, Presenter는 Failures만 warn한다. 따라서 ERROR/contract 0으로 wave/text 누락 0을 확정할 수 없다. **이번 경기에서 실제 expiry가 발생했다는 증거는 아니다.**
- **공간·시각 한계:** 안쪽 lateral -1.236/+1.352/+1.449 수집 확인, 반폭 1.5 바깥 직후 정밀 표본·텍스트 실제 표시 시각 로그 미확보. 현재 `p<=front` 전체 영역의 틱 판정은 얇은 전선/swept 판정이 아니며 규칙29와 일치한다. 행동 변경 대상으로 확정하지 않고 틱 사이 이동·정확 접촉 시각의 잔여 한계로 기록한다.

발사 `2:10@30fps=70/30초`·cycle 4초·원본 VFX/클립 고정. Codex 최종 두 메뉴/빌드 완료 확인·최종 판정 교정 확정·전체 HPbar/사망/애니메이션 동기화 완료는 주장하지 않는다.

## 승인된 전 경기 사건 추적 준비 (2026-10-01 — 계획 완료, 구현 인계 전)

이번 로그 확장은 매 테스트 경기에서 모든 파도와 확정 접촉을 끝까지 따라가, 피해·회복 판정과 실제 파도·텍스트 재생 사이의 빈 구간을 구분하기 위한 것이다. 사용자 재승인 「그렇게 진행해」는 **기존 Research/Plan 갱신 후 로그 구현**에 대한 승인이다. 앞의 한정 표본·압축 집계 「구현 미승인」은 당시 이력으로 보존한다. 현재 구현 결과가 나온 것은 아니며 전체 OPEN/C3 Unresolved·정확 시각 수용 OPEN은 유지한다.

### 직접 확인한 관측 공백

- `UnitCombatUseCase.cs`의 spawn/end 관측은 process-lifetime 32 cap, candidate/apply는 wave별·전체 detail/contact/exclude budget으로 후속 증거가 잘린다. 제외 사유 카운터는 반복 틱 수와 고유 대상 수를 구분해야 한다.
- `UnitCombatUseCase.WavePresentation.cs`의 PublishWaveContact는 적용량 양수가 아니거나 wave key 무효면 반환한다. 진단을 위해 이를 바꾸거나 noDelta를 새 gameplay contact로 발행하지 않는다.
- `WavePresentationPresenter.cs`의 성공 display 로그도 첫 32만 남기며 EmitContact는 텍스트 TryShow 결과 bool을 반환한다. 성공 요청 기록은 픽셀의 육안 표시 시각/수용 자체가 아니다.
- `WavePresentationCoordinator.cs`에는 일부 Expired 증가가 Failures 증가 없이 끝나는 경로와 retry/channel failure·retire가 있다. 따라서 Failures 경고만으로 표현의 전 경로를 검증할 수 없다.
- `NetworkCombatController.Waves.cs`는 정상 RPC와 active spawn snapshot 경로를 모두 가진다. snapshot 복원은 새 권위 발사나 새 피해가 아니므로 분리한다.
- writer 직전·직후·이벤트 전달 뒤 HP를 구분하지 않으면 동기 콜백으로 인한 HP 변화까지 파도 적용량으로 오인할 수 있다. before/immediateAfter/eventAfter를 읽기 전용으로 기록하고 실제 writer 호출·Publish semantics·hit-set 순서를 유지한다.

### 관측 계약과 불변 경계

전 경기의 생성·확정 접촉·표현 성공/실패/만료·retire를 키로 결속해 **모두 기록**한다. 확정 접촉을 32 cap·상세 budget·조용한 샘플링으로 버리지 않는다. 반면 동일 wave/target의 동일 제외 상태는 반복 틱마다 출력하지 않고 최초 관측·사유 전이만 남긴다. 전이 상태의 저장은 bounded이며 overflow를 명시해 증거 불완전성을 드러낸다. 이 상태는 gameplay hit-set이 아니다.

공통 결속은 경기/epoch·WaveDeliveryId·target kind/id·contact ordinal·역할·기록 단계다. 권위 사건 시각, 로컬에서 읽은 동기화 server clock, delay를 뺀 presentation clock, 로컬 수명/재시도 관찰 시각의 의미를 구분한다. server contact와 실제 EmitContact 성공은 같은 server clock의 상대 시각으로 비교하되 서로 다른 장치의 로컬 realtime을 직접 빼지 않는다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 28~31(서버 파도·대상별 1회·피해/힐·VFX) 및 44(방어 감쇄); `GameSystemRules_UnitCombatSynchronization.md` NET-AUTH-001·NET-TIME-001~005·NET-DELIVERY-TRAVELING. `LogRules.md` 1.2·1.4·1.7·1.8·1.10·1.12·1.14(두 축·형식·개발 conditional·기존 sink·전이 기록). 최신 계획과 정확한 파일 범위는 Plan 「승인된 전 경기 사건 중심 로그 구현 계획」을 따른다.

서버 판정 `p<=front` 전체 영역 틱 방식·hit-set·HP 및 Publish 동작·VFX/clip 수치는 모두 불변이다. noDelta 11건은 여전히 원인 미확정이며 fullHP/dead/zero를 새 근거로 분류하기 전 오류 또는 방어력 원인으로 확정하지 않는다. 문서 담당자는 두 Task 문서만 수정하며 코드·상시 문서·메모리·TC/QA·Unity/build는 수행하지 않는다.

## 2026-10-02 로그 구현 결과 인계 — 전체 OPEN/C3 Unresolved

사용자 승인 범위의 전 경기 사건 중심 **개발 로그 구현 완료 인계**를 반영한다. 문서 담당자는 관련 소스의 helper·출력·시계·수명 경계를 읽었으며, 여기의 컴파일 수치는 compile-agent 인계다. 메인의 추가 컴파일 근거 검토는 진행 중이다. **Unity 메뉴·빌드·이 구현 이후 실기는 미수행**이며 게임 동작 PASS/누락0·정확 시각 수용 또는 C3 migration 완료가 아니다. 2026-10-01의 두 경기 수치는 이전 로그 구현의 관측 이력으로 유지한다.

### 실제 구현 파일

기존 `Assets/_Project/Scripts/` 아래 8파일: `Application/UseCases/UnitCombatUseCase.cs`·`UnitCombatUseCase.WavePresentation.cs`, `Infrastructure/Network/NetworkCombatController.Waves.cs`, `Application/Combat/WavePresentationCoordinator.cs`, `Presentation/Effects/WavePresentationPresenter.cs`·`VfxPoolItem.cs`, `Infrastructure/Debug/ConsoleSink.cs`·`RuntimeLogger.cs`. 신규 `Application/Combat/WavePresentationDiagnostics.cs` 및 `.meta`가 추가됐다. Bootstrap 2파일 연결은 이번 구현에 포함되지 않았다.

### 구현 내용과 관측 의미

- 기존 32개의 spawn/end/display cap 및 causal 사건 샘플링 제한을 제거했다. 전 경기 파도/target 사건을 기록하고 제외 사유는 primitive 상태 전이로만 출력한다. 같은 제외 사유를 매 틱 반복 출력하지 않는다. writer before/immediateAfter/eventAfter와 판정·noDelta 근거를 관찰하며 기존 damage/HP writer·Publish 조건/semantics·hit-set은 유지한다.
- helper는 `[WAVE-DIAG]` prefix, invariant 숫자와 **전체 줄 900 UTF-8 byte 제한**을 사용한다. 필드 분할·chunk 및 필요시 UTF-8→Base64로 보존하며 byte 제한을 causal 사건의 조용한 생략으로 사용하지 않는다.
- 활성 wave 관측 state 512, per-wave target 1024, 전체 target 8192는 **관측 상태만의 상한**이다. overflow를 명시하고 causal 출력은 계속한다. 512는 경기 누적 파도 cap이 아니다. transport/presenter/pool은 scope 집계만 유지한다. gameplay transport/coordinator 용량이나 판정을 바꾸지 않는다.
- scope의 role은 관측 수명에 캡처하고, 잘못된 delivery epoch는 현재 scope를 초기화하지 않는다. fallback invalid key는 localWave로 **진단만** 결속하며 유효 delivery나 정식 contact key로 위장하지 않는다.
- 권위 contact clock·position과 실제 text emit 성공 직후 읽은 synced server clock을 결속한다. `schedulerNow`와 emit 시각, `presentation=emitted-delay` 및 contact 대비 lag를 구분한다. bool/Play 요청 성공은 픽셀 표시·육안 타이밍 수용이 아니다.
- retry는 첫 channel 실패·이후 성공 또는 expiry를 기록한다. Expired/ChannelFailures/Failures/pending을 분리하고 GameEnd는 snapshot, Presenter final은 자기 coordinator까지, NetworkDespawn은 server/transport final을 기록한다. final을 먼저 찍어 pending/in-flight가 완료됐다고 오인하지 않는다.
- ConsoleSink·RuntimeLogger의 **Info + 신규 prefix/진단 class 한정** 콘솔 출력만 NoStacktrace로 바꿨다. Warn/Error/Exception·전역 stack 설정·기존 파일 기록·세션 소유권 및 IsEmittingToConsole 방어는 유지한다. 비용 개선 실측을 했다는 뜻은 아니다.
- 발사 2:10@30fps=70/30초·cycle 4초·서버 width/length/travel·원본 VFX/clip/calibration 및 서버 권위 판정/HP/히트 집합은 불변이다. `p<=front` 전체 영역 틱 판정과 틱 사이 표본 한계도 그대로다.

### 인계된 컴파일 결과와 남은 경계

compile-agent 보고: **Runtime/Development warning 20·error 0, Release warning 24·error 0, Editor warning 0·error 0**. 이 문서 작업의 직접 실행 결과가 아니며 메인 추가 근거 검토 대기다. warning 0 전체 통과나 Unity 메뉴/Android 빌드/실기 검증으로 확대하지 않는다.

1. Client 수신 payload는 normal/snapshot이 분리되지 않아 `normal-or-snapshot`으로 기록한다. sender의 정상/snapshot 사건과 키로 결속해야 하며 수신 행만으로 구분 완료라고 주장하지 않는다.
2. single fallback의 server-owner final summary는 Bootstrap 2파일 연결이 미실행되어 이번 범위 밖이다. 상세 per-wave 기록은 있지만 모든 실행 모드의 final 집계 완전성은 확보됐다고 표기하지 않는다.
3. 새 실기와 정확 시각 수용은 OPEN. 이전 noDelta 11건의 원인은 소급 확정하지 않으며 ERROR/contract 0을 wave/text 누락0 증명으로 바꾸지 않는다.

**사용자 명시 선호:** 테스트마다 상세 사건 로그를 수집하고 반복 테스트에서 증거가 확인된 경로만 교정한다. 테스트 전 임시 로그 정리로 증거 수집 경로를 제거하지 않으며 이번 Dev 진단 로그는 task closure까지 유지한다. 이번 구현은 로그 확장이지 근본 판정 교정 또는 task closure가 아니다. 추가 TC/QA/새 Task/Git 없음, 문서 정합성 검사·메모리 baseline 갱신은 메인이 수행한다.


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
