# Hexiege — 스탯 레퍼런스

> **2026-10-11 00_46 최종 분석 / FAIL·OPEN:** same879 exact-key payload 양쪽 일치, HostReady879/fail0 ↔ ClientReady816/**Expired63 전부 Assault**; 피해 불일치·미수신63이 아닌 표현 만료다. Sniper Host14/Client13 binding1.733333349초/cycle3, 결과99(71Hit/28Miss) 양쪽Ready99/Expired0·VFX98; 설정/권위전달/Ready 기술 확인만이며 pixel·사용자 육안 수용은 미확인이다. Inferno0으로 이번1.5초 실기 평가 불가. 양쪽 local ROOT PASS는 공식 cross-audit/전체 timing PASS가 아니다. 상세·원인 미확정 경계는 WORK_HISTORY.md의 최종 근거에 기록한다.

> **[🔴 2026-10-11 correction — original kept]** 아래 중간 Host13 추정은 **Host14/Client13**으로 정정. Client63 전부 stageDecision Expired로 확정됐으며, 아래 “나머지47 원인 미확정”은 **상태 미확정이 아니라 원인 상세가16건만 관측됨**을 뜻한다. 중간 분석은 보존된 이력이고 최종 대기 표기는 종료됐다.

> **2026-10-11 00_46 경기 / Main 확정 중간 분석·전체 FAIL/OPEN:** Android Host ↔ Editor Client, session `7ae2bf913d26ef160260670c64aaf46b18f7aeab810be5aa3f7173c70582f367`. 관측 타입 Assault/Sniper; Client Sniper13기의 explicit `Sniper_Attack` binding은 marker1.733333349초(1:22@30fps), cooldown/duration3초이며 Host13기 대조는 agent 확인 대기다. Inferno 문자/타입 관측 없어 이번 Inferno 실기 검증 불가. Host results879/fail0/ready879 ↔ Client results879/ready816/fail63. **63은 표현 실패이며 실제 피해 누락63의 증거가 아니다.** bounded 상세16건은 모두 Expired; 나머지47건 원인은 미확정. 16건 `publishAfterLimitFalse / receiveAfterLimitEstimateFalse / decisionAfterLimitTrue`, Client frame32876 gap0.910803초와 serverclock189.62629→190.736736, anchoredJump 추정0.66~0.79초가 동반한다; 전달 지연·기기사양을 근본 원인으로 확정하지 않는다. Client Unit52 InitTimeout1 별도 관측. Client local ROOT PASS/errors0/evidence complete는 Client C3 FAIL을 덮지 않으며 공식 cross-audit 없음. 최종 programmer 분석 대기. 근거: `_Logs/2026-10-11/00_46_logcat/RuntimeLog_device.txt`(Host), `_Logs/_editor/2026-10-11/RuntimeLog.txt`(Client); Main 인계값이며 문서 담당 추가 raw 분석 아님.

> **2026-10-11 최종 Main UI 인계 / native gate PASS·빌드 시도만:** Assets Refresh 재컴파일 후 KST **00:17:41 Run Unit Action Self Validation 전체 native PASS**(A1/B3/C1/C2, PRODUCTION-TIMELINE의 Inferno exact1.5초 계약, C2-AUTHORITY, C3-PRESENTATION-SHADOW 포함), **00:18:47 Self Validate Unit Root Pose Cross Audit native PASS**. Console 오류0/warning30(의도된 fixture 경고 포함); warning-zero 런타임 증거가 아니다. programmer 소스/config2파일 적용·Main 해시 일치·static4 오류0 유지. **File > Build And Run 정확히1회 클릭**, 즉시 `Importing(Settings/Mobile_RPAsset.asset)`만 관찰한 뒤 UI/log/process 모니터링 전부 중단. 빌드 완료·성공·설치·실기/timing/pixel은 미확인/OPEN. 아래 대기 기록은 이전 단계이며, 과거 UnitAction FAIL의 stale0.50초 기대값과 기존 baseline은 보존한다. clip 추가 검사·해시·수정 및 모든 Task 변경 없음; 다른 VFX 보류 항목은 해소하지 않는다.

> **2026-10-11 추가 Main 인계:** 소스2파일 최종 해시 일치, static4 errors0 / warnings Runtime20·Editor0·Development20·Release24. 실제 AssetsRefresh 완료 후 domain reload 대기; fresh native 메뉴·빌드 결과는 아직 없다. Config SHA256 `0A907B25728237837C2D5298B3E47EEB8570AD093AC2BE4D1FBE0633F67BC275`, 기존 gate SHA256 `65300906C0A98D362F7940393F9219E80C9BCC5CC035A75002911A81965FFB6F` (Main 대조값; 문서 담당의 clip/source 검사가 아님).

> **2026-10-11 Inferno 현행 승인·소스 적용 인계 / 검증 대기:** 사용자 확정 `1:15@30fps = 45/30 = 1.5초`, 주기3초 유지. StatsReference 표의 기존 `1:15(3:00)` 값은 이미 올바르므로 변경하지 않는다. Main 인계상 관련 소스2파일의 1.5초 정합화가 적용됐으며 현행 source gate도1.5초다; 정적 결과는 대기 중이다. **2026-10-10 23:55:57 UnitAction FAIL(기존0.50초 기대값), 23:57:24 ROOT PASS**는 Main 실제 UI 인계의 별도 이력이며, 1.5초 적용 후 fresh UnitAction PASS나 Inferno 타격 검증을 뜻하지 않는다. 이전0.50초 binding/Stats/게이트 근거는 당시 baseline으로 보존한다. 사용자 요청에 따라 clip 조회·해시·수정 없음; 새 native/build 결과 미인계·runtime/timing/pixel OPEN. 직접 피해·DoT·서버 권위 계약과 기존 Human Assault VFX를 유지하고 burn fitting·Spirit endpoint·Tank/Cannon 진단은 보류한다. Sniper/과거 Task 수정 및 새 Task 생성 없음.

> **2026-10-10 현재 freeze / 정적 보고까지·전체 OPEN:** programmer44파일 snapshot(추가24/수정20)과 09:53:38 Runtime/Editor/Development/Release static4 exit0 보고 수신. Main SourceHash44 재확인 완료(Files44/Mismatches0/ChangedPaths=null, tool39f796), 보호6파일은 Bloom clip baseline 포함 모두 일치 인계다. UnitAction·AuthoritativeAttackPresentation 기존 fixture 확장은 연결됐으나 실행 전이고 Root 기존 gate는 불변이다. capacity·clock deadline/NaN·mode matrix·forced teardown·wire assertion 범위는 미완이다. native Action/ROOT 미실행(UI activation2 timeout)·빌드 미시도·실기/visual 미확인. writer-owned remaining-flight tracer와 immutable due impact, local Legacy HP marker·서버 즉시 타워 피해·actual DoT3초·Stats/cycle/range를 유지했다는 구현 보고와 실제 수용은 별개다. 최대 약0.8초 초기 정상 visual tail 설정은 실제 pixel 종료시간 PASS가 아니다. 기존 미인계/수정 중 단계와 다른 작업 PASS는 dated history다. [현재 구현·coverage·Task manifest](Assets/VFXSFXList.md#2026-10-10-현재-source-freeze--정적native수용-경계).

> **2026-10-10 VFX 연결 승인 / 수치 불변:** Mushroom 기존 3초 clip 길이와 cycle·Stats/UnitStats는 유지하고, 표현 marker로 1초 `OnAttackHit` 이벤트 1개만 추가하는 계약이다(30fps 기준 1:00). 실제 적용/native 확인은 대기한다. 서버 DoT 3초·틱·피해·HP writer·범위 및 Tower/Tank 수치도 변경하지 않는다. tracer 도착은 피해 시점을 결정하지 않는다. 구현 진행 중이며 새 gate/build/runtime 결과는 미인계다. [현행 계약](Assets/VFXSFXList.md#2026-10-10-확정-연결-계약과-진행-단계). 아래 날짜별 수치는 해당 이력이다.

> **§23 최신 역할교대 saved-log 분석(2026-10-04) — first-entry/due/postdispatch cooldown/등록 기준 duration focused 관측 수용·전체 Bloom OPEN:** Android Host/Editor Client 및 Editor Host/Android Client 신규 두 경기에서 Expired27/27이 등록 후3.001368~3.107432초·now>=Deadline, early0이었다. 각 경기 FirstEntry7/7 및 exact Play/Marker55/63 일치. 별도 B 시작/rebind wave clock 경고는 미해결 관찰이며 HoT 인과 증거가 아니다. visual/VFX·Client HP/text 전수·skill HoT/invalid-clock/movement-replaced coverage는 미시험이다. §21 실패 baseline/§22 구현·메뉴 이력 보존, 이번은 로그 분석·문서만. 상세 기존 Bloom Research/Plan §23.

> **§22 최종 native 인계(2026-10-04) — source1파일·static4 errors0·실제 새 기존 두 메뉴 PASS / 새 HoT runtime·전체 OPEN:** final source16:24:35 뒤 실제 Assets Refresh로 native Assembly-CSharp16:28:31/Editor16:28:32 갱신, reload busy 해소 및 Unit Action/Root SelfValidate 새 실행 PASS를 메인이 확인했다. 새 production HoT fixture·Analyze·게임 runtime 증거는 아니며 이번 빌드 없음. 아래 import/메뉴 대기는 당시 이력, §21 조기 종료 baseline·정책/수치 불변과 runtime OPEN 유지. 상세 기존 Bloom Research/Plan §22.6.

> **§22 최신 구현 인계(2026-10-04) — 공통 continuous Heal 등록 기준 duration source 완료·새 static4경로 errors0 / 두 Unity 메뉴 대기·runtime OPEN:** 메인 review로 UnitCombatUseCase.cs 한 파일 반영을 확인했다. Bloom+skill HoT의 scaled 등록/deadline 계산이며 수치·authority·에셋/UnitView·ticker·DoT 불변이다. 실제 Assets Refresh 뒤 Unity import 진행 중이고 native DLL은 아직 이전14:15본이다. 이번 빌드 없음·새 메뉴 PASS 주장 없음. §21 조기 종료 증거는 교정 전 이력, 새 duration runtime 수용은 OPEN이다. 상세 기존 Bloom Research/Plan §22.5.

> **§21 역할 한계:** 이전 §19 실패는 Android Host/Editor Client, 이번 수용은 Editor Host/Android Client 두 run만이다. 교정 후 Android Host first-entry 실기 coverage는 미확인·모든 플랫폼/역할 보편 해결 proof 아님. 새 역할교대 시험 요청/실행 없음.

> **BloomFairy §21 최종 로그 판정(2026-10-04):** first-entry/반복 전달·due·postdispatch cooldown의 **focused 관측 수용**. 공통 HoT의 누적 Elapsed3과 실제 등록→종료는 별개로, 약70ms 짧은 duration 잔여는 OPEN·후속 제안만/코드 미수정이다. **전체 Bloom OPEN**: visual/VFX·Client HP/text 전수·movement-replaced 재진입 수용 없음. 원수치·3초 정책·클립/authority 불변, VFX 후 육안 선호는 제작 승인 아님. §20 이후 이번은 saved-log 분석·문서 갱신만이며 새 빌드/자동 검증 없음. 상세 `_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/{Research,Plan}.md` §21.5/Plan §21.1. 아래 Timing 대기·이전 실패는 이력이다.

> **BloomFairy §21 최신 두 실기 로그(2026-10-04):** TeamRed 생산13/13 FirstEntry accepted·총83 exact Host/Client Play로 **첫 진입/반복 전달 회귀의 focused 로그 관측 수용**. cast별 최신 revision 전수 수신이며 중간 inactive 차이는 시작 누락이 아니다. Timing/HoT 최종 확정 인계 대기, callback pose/MOVE/local ROOT는 pixel/model visual·formal CrossAudit PASS가 아니다. **visual/VFX 및 전체 Bloom OPEN**; 사용자 VFX 후 육안 확인 선호는 제작 승인이 아니다. 정책/스탯·에셋·source 무변경, 단일 상세 근거 `_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/{Research,Plan}.md` §21. 아래 미실기/실패/PASS는 해당 단계 이력이다.

> **BloomFairy §20 최종 후속(2026-10-04):** UnitView 첫 authority 검사 전 same-object component 확보 교정 구현·새 static4경로 errors0·메인의 실제 새 Unit Action/Root SelfValidate **PASS** 확인. Build And Run1회 실제 클릭 직후 JDK checking 진행창까지만 관측하고 UI/build log 확인 모두 중단했다. **완료·설치·runtime 미확인/빌드 성공 주장 없음·runtime 재시험 OPEN.** fresh-first-entry production runtime fixture 미실행, 이전 fixture PASS는 실제 첫 진입 회귀 수용이 아니다. 클립/수치·authority·post3 정책/다른 타입 불변. 상세 `_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/{Research,Plan}.md` §20.4; 아래 구현/gate/build 대기·이전 실패는 당시 이력이다.

> **BloomFairy 현재 source 상태(2026-10-04):** §19 첫 진입 의존 순서 결함의 최소 교정/새 static 완료 인계, 새 메뉴·빌드 사실 대기/실기 OPEN. 원스탯·marker/클립·HP/HoT·서버 권위·쿨다운 정책은 불변이며 이전 §18 PASS는 새 첫 진입 수용 증거가 아니다. 상세 `_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/{Research,Plan}.md` §20.3. 아래 이전 source 상태는 이력이다.

> **BloomFairy §18 최종 후속(2026-10-04):** UnitView 한 파일 교정·최종 정적4경로 errors0 뒤 메인이 새 기존 Unit Action/Root Pose Cross Audit SelfValidate **PASS**를 확인했다. 이어 Build And Run1회 클릭 직후 Importing/Settings/Mobile_RPAsset.asset만 관측하고 모든 UI/build log polling을 중단했다. **완료·설치·runtime 미관찰/빌드 성공 추정 없음, 실기·전체 OPEN.** 기존 자동 gate는 새 coroutine runtime fixture/이전 경기 Analyze가 아니다. 정책/스탯·에셋·다른 타입 불변. 최종 상세 `_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/{Research,Plan}.md` §18, 이전 실기는 §17; 아래 gate/build 대기는 당시 이력이다.

> **BloomFairy §18 최종 정적 후속(2026-10-04):** 코드 담당 인계상 UnitView 한 파일의 최종 정적4경로 errors0이며 메인 source review/추가 owner cleanup 확인까지다. 경고 수는 정적 카운트로만 해석한다. **새 기존 두 메뉴·이번 빌드 시도는 실행 대기, runtime/전체 OPEN.** 정책/스탯·에셋·공통 ticker/다른 타입 불변. 상세 코드·정적·보조 artefact는 `_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/{Research,Plan}.md` §18, 이전 runtime은 §17; 아래 정적 대기/미승인·미구현 문구는 당시 이력이다.

> **BloomFairy §18 코드 후속(2026-10-04):** 메인 source review 인계상 UnitView의 실제 dispatch+설정 cooldown deadline/owner 보호가 반영됐다. 설정값·규칙36의 준비/발동 후 쿨다운·HoT 스탯은 불변이다. 최종 정적/gate/빌드 결과 대기·runtime 전체 OPEN, exact4초 cycle 보장 아님. `_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/{Research,Plan}.md` §18 참조; §17 및 아래 실기 전/교정 전 상태는 이전 증거다.

> **BloomFairy 최신 검증 상태(2026-10-04):** §16 교정 뒤 due 경계만 관측 수용했으며 전체 OPEN이다. 발동 후 cooldown 잔여와 시각 방향/연속성은 미완이고 규정 스탯·준비1초+발동 후3초·HoT 수치는 변경하지 않았다. 상세 증거/outcome·raw anchors는 `_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/{Research,Plan}.md` §17 참조. 아래 교정 전/실기 전 기록은 당시 이력이다.

**최종 수정일:** 2026-10-02

## TorrentSpirit 표현 교정 상태

**2026-10-02 이동 최종 분석 추가(교정 미구현):** 1차 Editor Host Pistoleer7개×최초/cleanup Fatal2=14, 2차 Android Host LittleKnight2+Pistoleer13개×2=30. 서버 공간 상태 불일치→Fatal→종료 거부→후속 이동 취소는 확정; 건물 랠리 종점 TerminalContact의 Root/domain commit 계약 충돌은 유력 근본 원인이나 직전 성공 프레임 미기록·재현 미수행이다. 사용자 멈춤 유닛은 미확인, ROOT PASS는 pose 정합이지 목표 진행 보장이 아니다. EmptySameTile0거리 거부와 wave18 소비 지연은 별개. 건물 목표/실제 walkable 종점 분리·Root/domain 일관성·안전 종료 상태는 권장 검토 방향일 뿐 구현 승인 아님. 전체 OPEN/C3 Unresolved·MOVE FAIL 유지; 아래 원인 추적 중 표기는 이전 인계 상태, 상세는 기존 Research/Plan 최신 이동 최종 분석 절.

**TorrentSpirit 2026-10-02 12:59~13:08 역할교대 최신 — 확정 전달 정합 / 전체 OPEN·C3 Unresolved:** 1차 Editor Host/Android Client(epoch `40c733ed4f786813`) wave147·publish291(피해259/회복32), 2차 Android Host/Editor Client(epoch `491a032ae783db99`) wave176·publish292(피해263/회복29). Client 수신·양쪽text exact key 누락/초과/충돌0, Client VFX exact-wave 누락/초과0; zero60/118 모두 만피 회복 정상, badZero·중복writer0, 양쪽 최종 채널 reject/failure/expiry/pending/fault/overflow0. 기존100ms를 제외한 2차 Client 큰 잔차2건349.511ms·249.849ms(나머지290건 최대21.834ms)는 수락 후 소비 지연이며 원인·적시성 완전 PASS 미확정. 육안은 어느 정도 일치하나 정확 확인 어려움. 양 경기 서버 MOVE FAIL·원인 추적 중, ROOT PASS/Client EVIDENCE는 이동 성공이 아니다. 상세 세션·시간·로그·지표는 기존 `_Tasks/2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/{Research,Plan}.md` 최신 역할교대 절. 아래 기록은 이전 경기·당시 이력으로 보존한다.

**TorrentSpirit 2026-10-02 최신 실기 — 이번 경기 확정 접촉 전달·텍스트 표시 정합 / 전체 OPEN·C3 Unresolved:** 메인 최종 분석 인계 epoch `463c2fb49e473595`에서 wave53 spawn→Client 수신/VFX 재생 성공의 exact wave ID 누락·초과0, 확정 publish144(피해111+회복33)→Client 수신·양쪽 text exact key 누락·초과·충돌0. writer176 중 HP 무변화32는 전량 만피 회복 정상이며 이전 원인 미확정11건을 소급 확정하지 않는다. channel failure/expiry/reject/pending/helper fault/overflow0. 표현은 기존100ms 지연+Host 최대37.781ms/Client31.201ms; 경계 옆 진입 적용은 Units 규칙 29 사례다. raw particle 접촉 좌표·정확 윤곽/접촉 시각·성능은 미확정, 정확 시각 수용·미완 payload/fallback final 경계는 OPEN. 코드·원본 VFX/clip 불변. 상세는 기존 Research/Plan의 최신 테스트 절. 아래 실기 전/컴파일 대기 표기는 당시 이력이다.

**TorrentSpirit 2026-10-02 최종 현행 — 로그 구현·정적 컴파일 인계 완료 / 전체 OPEN·C3 Unresolved:** invariant G17·full UTF-8 final guard 완료, >900은 keyed format-incomplete warning/faults/coverage=false. 최종 byte-format compile 4종 모두 error0·warning Runtime20/Development20/Release24/Editor0. 메인658과 별도 보수적 static fixture 최대699/fallback400은 다른 조건이며 실기값이 아니다. 944는 교정 이력으로 보존한다. Unity/device 미실행·build 미시도, Client normal/snapshot 직접 분리·single fallback server-owner final 미연결 및 정확 시각 수용은 OPEN. 서버 gameplay·원본 VFX/clip 불변. 아래 검토 대기/미해결 표현은 당시 이력이며 기존 TorrentSpirit Research/Plan 최종 절을 따른다.

**TorrentSpirit 2026-10-02 — 전 경기 사건 로그 구현 인계 / 새 실기 전 OPEN·C3 Unresolved:** 32 관측 cap 제거·causal 사건 무샘플링·제외 전이·900byte field chunk/Base64·신규 prefix Info NoStacktrace가 구현됐다. 관측 state active wave512/target1024-per-wave·8192-overall은 누적 파도 cap이 아니며 overflow explicit·causal 계속이다. Client normal/snapshot 직접 구분 및 single fallback server-owner final Bootstrap 연결은 미완이다. 컴파일 인계는 오류0이나 메인 추가 근거 검토 중, Unity 메뉴·빌드·새 실기 미수행·정확 시각 수용 OPEN. 서버 authority/판정/hit-set/HP·원본 VFX/clip·70/30초 발사/4초 cycle 불변. 기존 TorrentSpirit Research/Plan의 2026-10-02 최신 절 참조.

아래 2026-10-01의 32 cap 및 실기 수치는 이전 로그 구현의 관측 이력이다. 새 구현 이후 성공/누락0·시각 수용을 뜻하지 않는다.

**TorrentSpirit 최신(2026-10-01) — 두 경기 실기 근거 확보 / 정확 시각 수용 OPEN·전체 OPEN/C3 Unresolved:** 파도 관측/display 17→32·HP 변화 45→121·전체 Client heal 4→36. 최신 32는 cap(이후 미관측), noHpDelta 11 원인 미확정. ERROR/contract 0은 wave/text 누락 0의 증명이 아니며 실제 expiry 발생 증거도 아니다. 18:15 busy·당시 메뉴/빌드 미실행은 이력, Codex 최종 메뉴/빌드 확인 및 사용자 정확 시각 수용은 주장하지 않는다. 원본 VFX·발사 70/30초·cycle 4초 유지. 기존 `Assets/_Project/Docs/_Tasks/2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/{Research,Plan}.md` 최신 절 참조.

2026-10-01 승인된 교정은 서버 파도 키 `MatchEpoch+WaveDeliveryId`와 실제 생성 위치·방향·시각 및 접촉 스냅샷을 기준으로 파도와 확인된 피해·회복 숫자의 표현 시간을 함께 관리한다. 서버 HP 적용은 즉시 유지하며 전체 HPbar·사망·애니메이션 동시성이나 Supported/C3 migration 완료를 뜻하지 않는다. `Unresolved / LegacyFallback`을 유지한다. 아래 과거의 설정·생산 연결 구현 전 OPEN 설명은 당시 상태이며 이 현행 상태로 대체한다.

서버 폭 3 WU·길이 3 WU·이동 .5초·속도 6 WU/s, 발사 `2:10@30fps=70/30초`, cycle 4초는 그대로다. 표현 교정값은 X=.78·FrontAnchor=.928406·Hierarchy=true이며 Y 원본 배율과 원본 clip/VFX prefab을 보존한다. 17:59:49 방향 측정 CSV `Library/TorrentWaveMeasurements/directions-20261001-085949-284.csv`에서 Blue/Red × yaw 0/90/+45/-45/180의 fit/holdout 모두 lateralWidthAchieved=True·centeredExcess=0, 높이는 전후 fit1.005499·holdout1.029019로 같았다. 고정 camera·8 seed의 기하 경계 표본이며 alpha silhouette·임의 방향·네트워크 역할교대·실기 PASS 보장은 아니다.

> **이전 관찰 — 18:15 이력:** 최신 메인 전달 상태는 Unity busy 약 8분이다. 최종 값 이후 필수 두 메뉴 재실행과 빌드 시도는 승인된 남은 작업이나 busy로 **미실행**이며 Editor 정상 복귀·사용자 실기·전체 작업 **OPEN**이다. 이전 메뉴 PASS는 최종 값 검증으로 대체하지 않는다. 서버 stall의 접촉 sampling 지연 한계와 렉 원인은 별도다. 상세 근거는 기존 TorrentSpirit wave-contact-diagnosis Research/Plan을 따른다.

이 문서는 사람이 읽는 스탯 미러다. 런타임 수치의 원본은 `Assets/_Project/Resources/Config/UnitStatsConfig.asset`이며, 공격 의미는 `GameSystemRules/GameSystemRules_Units.md`, 멀티플레이 동기화 계약은 `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`, 유닛별 에셋·구현 상태는 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`를 따른다. 문서와 런타임 에셋이 다르면 어느 한쪽을 임의로 정답 처리하지 않고 불일치로 기록해 게임 의도를 확인한다.

> **2026-08-10 변경:** 초월계 특수 건물 **MistShrine(HealShrine)** 항목에 재설계 확정 내용 반영(물안개 지속 힐 — 아군 유닛+건물 대상). **2026-08-12 구현 완료 / 에디터 싱글플레이 실기 검증 완료 · 멀티 미검증**이며, 회복량·지속시간·쿨다운·범위는 **여전히 밸런싱 미확정**이다(현재 게임에서 도는 값은 전부 임시값이므로 확정 수치로 인용 금지). 아울러 MistShrine은 **방어 타워가 아님**을 명시(Transcendence 방어 타워 = VineTower). 상세: `GameSystemRules/GameSystemRules_Buildings.md` MistShrine 물안개 힐 시스템.

---

> ## ✅ 밸런스 개편(×10 스케일 + 유닛 강화 시스템) — 구현 완료 (기능·멀티플레이 실기 PASS, 2026-07-31)
>
> 이 문서의 **모든 전투 수치(유닛 HP·공격력, 건물 HP, 타워 HP·공격력, DoT 틱값, 힐량)는 ×10 스케일로 재조정된 값**이며, 신규 **유닛 강화(연구소) 시스템**의 방어력·업그레이드 표도 함께 반영되어 있다. **강화 시스템·×10 스케일·연구 패널 UI 모두 구현되어 멀티플레이 실기 테스트를 통과했다.**
>
> - **×10 config 반영:** 전투 수치 ×10은 config `.asset`(UnitStatsConfig/BuildingStatsConfig/SpecialAttackConfig)에 **값이 커밋되어 저장소 기본값이 ×10**이다(코드 폴백 기본값도 ×10, Inspector 값이 코드 기본값보다 우선). ×10 적용에 쓰였던 셋업 에디터 스크립트는 역할 종료 후 제거되어 별도 실행이 필요 없다.
> - 각 유닛 비고의 "구현 완료 / 구현 확정" 표기는 **해당 특수 공격 메커니즘(로직)이 구현·검증되었다는 뜻**이며, 함께 적힌 숫자는 ×10 값이다.
> - 변경 전(old)/후(new) 전 수치 대조, TTK 불변 근거, 미확정 쟁점 확정 내역은 **`_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md`** 를 단일 진실 소스로 참조한다.
> - 강화 시스템의 구현 계약·구현 상태·연구 패널 UI 설계(트랙 구조·데미지 공식·연구소 운영·비용·UI)는 **`GameSystemRules/GameSystemRules_Upgrade.md`** 참조.
> - **후속 보류(미완/미검증):** 연구 패널 UI 레이아웃 다듬기·매트릭스 헤더 아이콘·AI 연구 사용 실기·MistShrine 힐(**2026-08-12 구현 완료 / 멀티 미검증 · 밸런싱 미확정**)·싱글플레이 자연회복 실기는 별도 작업(과대 표기 금지).
>
> **불변(×10 하지 않음):** 이동속도(칸/초)·공격/감지 사거리(타일)·공격 쿨다운·생산 시간·연구 시간·모든 골드 비용(생산비·건설비·업그레이드비·연구비)·채굴 수입(10골드/초)·스플래시/특수 비율.

---

## 자원 시스템

| 항목 | 값 |
|------|----|
| 시작 골드 | 500 |
| 기본 수입 | 0 골드/초 |
| 채굴소 수입 | 10 골드/초 (채굴소 1개당, 전 팩션 동일) |

---

## 범위 공격 규칙

같은 타일에 유닛이 여러 마리 겹쳐 있는 경우, 공격 타입에 따라 피해 적용 방식이 다르다.
> 아래 피해 수치는 ×10 스케일 값이다(비율·반경은 불변). 방어력 감쇄(신규)는 **직격·스플래시에만** 적용되고(타워→유닛 데미지 포함) **DoT 틱값에는 미적용**이다(구현 완료, `DamageCalculator.ApplyDefense`, `GameSystemRules_Upgrade.md` 참조).

### 착탄형 (MushroomBomber, QuakeSpirit)
- **주 타깃 타일**: 유닛 1마리만 직접(착탄) 피해 적용. 나머지 겹친 유닛은 범위(스플래시) 피해만 적용
- **범위 타일**: 해당 타일의 모든 유닛에게 범위 피해 적용
- **MushroomBomber 구현 확정 (2026-07-19, 수치는 ×10 설계값)**: 판정 범위는 타일 소속이 아니라 **착탄 중심(주 타깃 위치) 기준 월드 좌표 원형 반경**이다(도끼병 부채꼴에서 arc를 뺀 형태). 착탄 중심으로부터 XZ 평면 거리 ≤ `blastRadius`(SpecialAttackConfig, 기본 1.0 = 인접 1칸 거리, 불변)인 **적 유닛 전원**(주 타깃 포함)에게 DoT를 부여한다. **직접 100**은 기존 단일 타깃 공격(`ReplacesPrimaryAttack=false`)이 주 타깃 1마리에게 적용하며(건물 공성 포함), **DoT는 적 유닛만**(건물 제외 — 단 주 타깃이 건물이어도 주변 적 유닛엔 DoT). 아군 무피해. 상세: `GameSystemRules_Units.md` 규칙 38~40. (QuakeSpirit도 이 원형 반경 수집 헬퍼를 재사용해 구현 완료 — 아래 QuakeSpirit 항목 참조.)
- **QuakeSpirit 구현 확정 (2026-07-20, 멀티플레이 로그로 메커니즘 검증 / 수치는 ×10 설계값)**: 착탄형 **즉발** AoE(DoT 아님)다. MushroomBomber와 동일하게 **착탄 중심(주 타깃 위치) 기준 월드 좌표 원형 반경**(`_quakeRadius`, SpecialAttackConfig, 기본 1.0 = 인접 1칸, 불변)으로 판정하되, 감쇠가 아니라 **2단계 고정 배율**이다: **주 타깃 1마리 = 100%(공격력 200)만**(스플래시 제외 — 건물이 주 타깃이면 100%로 공성), **착탄 반경 내 다른 적 유닛 + 적 건물 = 50%**(올림 `CeilToInt(공격력×0.5)`=100, 주 타깃 제외). ⚠️ **MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함**한다(그쪽 두 유닛의 AoE는 유닛만 — 차별점). 아군 무피해, 서버 권위. 구현: MushroomBomber 원형 반경 수집 헬퍼(`CollectEnemyUnitsInRadius`)를 `internal static`으로 공용화해 유닛 수집에 재사용(MushroomBomber 유닛만 로직 무변경) + 건물 수집 `CollectEnemyBuildingsInRadius` 신설, **유닛/건물 hit-set 분리**(규칙 29 계승). 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`) + 레지스트리 1줄. 튜닝 `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5, 불변). 상세: `GameSystemRules_Units.md` 규칙 43.

### 파도형 / 휩쓸기형 (TorrentSpirit, 도끼병)
- 범위 내 **모든 유닛**에게 동일 피해 적용 (같은 타일 겹침 여부 무관)
- **도끼병(BattleAxe) 구현 확정 (2026-07-17, 수치는 ×10 설계값)**: 판정 범위는 타일 소속이 아니라 **월드 좌표 전방 부채꼴**이다. 공격자 → 주 타깃 방향(forward) 기준으로 XZ 평면 거리 ≤ `sweepReach`(SpecialAttackConfig, 현재 실기값 0.75, 불변) **AND** 각도 ≤ `sweepArcHalfAngle`(반각, 기본 120°, 불변)인 적 유닛 전원에 동일 피해(공격력 150)를 적용한다. 아군/사망/공격자/주 타깃은 제외하며 건물은 AoE 대상이 아니다(주 타깃일 때만 단일 피해). 상세: `GameSystemRules_Units.md` 규칙 23~27. (TorrentSpirit는 미구현 — 파도형 설계 그대로.)

---

## 유닛 — 인간계 (Human)

> **공격 쿨다운 컬럼 표기**: `타격시점(쿨다운)`. 기존 표에는 Unity Animation 창의 `초:프레임`과 콜론을 소수점처럼 쓴 초 값이 혼재한다(`0:80`은 30fps 프레임 표기가 아니고, BattleAxe의 `1:17`은 소수 초 표기다). 따라서 단위가 없는 기존 콜론값을 일괄 해석하거나 환산하지 않는다. 새로 확인·교정하는 값은 `초`를 명시하고, Animation 창의 마커를 인용할 때는 `초:프레임(30fps)`이라고 구분한다.
> 타격시점 = 공격 애니메이션 시작부터 대미지가 들어가는 순간까지의 시간. 애니메이션 클립의 OnAttackHit 이벤트 시간과 일치해야 한다. `초:프레임(30fps)` 마커는 `초 + 프레임/30`으로 환산해 초 단위로 기록한다.
> **HP·공격력은 ×10 스케일 값(구현 완료 — config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)).** 이동속도·사거리·쿨다운·생산시간·골드비용·인구는 불변.

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 공격 쿨다운 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
|------|----|--------|--------------------|--------------------|------------------|------------------|-----------|-----------|------|------|
| 권총병 (Pistoleer) | 300 | 60 | 1.0 | 1.0 | 0.5 | 0.8초(주기 2초) | 5초 | 50 | 1 | 30fps 타격 0:24; 생산 연결 정합화; 단독 경기 로그상 긍정·정확 타격/주기·육안 OPEN |
| 돌격소총병 (Assault) | 400 | 10 | 2.0 | 2.0 | 1 | 0:05(주기 미확정) | 10초 | 100 | 1 | 30fps 타격 5/30초(약 0.1666667초). 현재 clip-derived 주기 0:10(10/30초), 기본 주기 0.2초는 변경하지 않음. 이전 0:17(0:33)은 교정 전 이력이며 0:33 주기 의미는 미확정·임의 환산 금지 |
| 저격총병 (Sniper) | 300 | 180 | 5.0 | 5.0 | 0.25 | 1:22(3:00) | 20초 | 200 | 1 | clip52/30초 보존·6파일 구현/Main6해시 일치·static4 errors0/actual generator37/37; 보안 modal로 fresh native/ROOT/build 차단, runtime OPEN |

**[2026-10-10 Sniper 표기 정정 — 원문 보존: 1:73(3:00)]** 30fps frame 부분73은 유효 표기가 아니다. 사용자가 승인한 현재 clip marker52/30초=1:22로 교정한다. Stats/fallback3초→52/30초 구현·native/runtime 수용은 새 Sniper Task 인계 대기다.
| 근접기사 (LittleKnight) | 350 | 40 | 0.5 | 1.0 | 1 | 0:25, 1:15(3:00) | 5초 | 50 | 1 | 2히트 공격 |
| 창병 (SpearMan) | 500 | 100 | 1.0 | 1.5 | 1 | 0:25(2:00) | 10초 | 100 | 1 | |
| 도끼병 (BattleAxe) | 800 | 150 | 0.75 | 1.0 | 1 | 1:17(3:05) | 20초 | 200 | 1 | 휩쓸기형 AoE — 월드 좌표 전방 부채꼴(반경 `sweepReach` 실기값 0.75 · 반각 `sweepArcHalfAngle` 120°) 내 모든 적 유닛 동일 피해(공격력 150). 타격 1.1667s(클립 종료 프레임 35f/30fps). 상세: GameSystemRules_Units 규칙 23~27 |
| 전차 (Tank) | 1000 | 300 | 3.0 | 4.0 | 1 | 0:17(4:00) | 30초 | 400 | 1 | 건물에 2배 대미지 ✅구현 완료(비율 불변, `ComputeFinalDamage` 건물 분기) |
| 포격수레 (CannonCart) | 500 | 200 | 3.0 | 4.0 | 1 | 0:17(4:00) | 20초 | 150 | 1 | 건물에 2배 대미지 ✅구현 완료(비율 불변, `ComputeFinalDamage` 건물 분기) |

> **Pistoleer 최신 경기(2026-10-04 23:06 KST) — focused 로그상 긍정·정확 타이밍/육안 OPEN:** Editor22:31–22:34 / device22_56 paired session192c9a08…34711d의 단독235개(Blue119/Red116) 생산·Client 생성; Ready/commit/schedule 각1302, Host 결과1225=Client accepted1225(failure/reject0), 양쪽 필수 표현1020/1020(failure0). Host 이동220148 frames 핵심/fatal0·Client 복제1655 samples invalid/conflict/entrygap/order0·최신 ERROR/FATAL 양쪽0. ROOT는 representativeTypes<2로 INCOMPLETE(errors0), cap64가 전체235 coverage는 아니다. marker/실제 Impact 상세 없어 실제0.8초 offset·2초 주기는 계산 불가, 전체 exact-key join·최신 육안 PASS·full migration/분류 승격 없음. 두 메뉴 PASS와22:09 빌드 preflight만 관찰 기록은 유지한다. Blue 이벤트 확인/의도적 설정 변경 없음 진술과 이전 원인 미확인 해시 관찰은 함께 보존한다. [분석](_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md#6-최신-paired-경기-분석--2026-10-04-2306-kst)

> **2026-10-04 정정:** 기존 Pistoleer `0:80(2:00)`를 0.8초 타격 offset·2초 주기로 구분했다. 30fps marker는 0:24다. Factory 이벤트 자동 추출 때문에 이전 config hit 2초만으로 실제 피해가 2초였다고 단정하지 않는다. 3파일 구현·새 Unit Action 22:04:36 PASS, 새 Root self-validation PASS·22:09 빌드 시도/preflight만 확인·실기 OPEN. [근거](_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md)

## 유닛 — 정령계 (Spirit)

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 공격 쿨다운 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
|------|----|--------|--------------------|--------------------|------------------|------------------|-----------|-----------|------|------|
| EmberSpirit | 350 | 60 | 0.5 | 1.0 | 0.5 | 1:00(2:20) | 5초 | 50 | 1 | |
| FlameSpirit | 350 | 30 | 0.5 | 1 | 2 | 0.6667초, 1.1667초, 1.4333초, 1.6667초, 1.9000초, 2.1000초 (쿨다운 3.0초) | 10초 | 100 | 1 | 6히트 공격. **위 타격시점 = 사용자 제공 Animation 창 화면·확인에 따른 의도값이자 현재 저장된 에셋 값**: 마커 `초:프레임(30fps)`의 `0:20`→`20/30`, `1:05`→`35/30`, `1:13`→`43/30`, `1:20`→`50/30`, `1:27`→`57/30`, `2:03`→`63/30`초. 사용자 확인 마커 `1:27`은 2026-04 기록의 `1:28`보다 우선한다. `UnitStatsConfig.asset`의 `hitFrameTimes`와 `FlameSpirit_Attack.anim`의 `OnAttackHit`가 위 6개 값으로 일치하며, Unity `[UAS-DIAG]` production timeline/self-validation 및 `[UAS-ROOT-CROSS-AUDIT]` self-validation PASS(Console Error 0)를 확인했다. **2026-09-29 실기 갱신:** 같은 세션 Editor Host/Android Client에서 FlameSpirit 실제 생산과 Host 공격 시작 Ready·shadow commit 확인, 사용자 화면 관찰은 “정상적으로 되는 것으로 보여”. Host 결과 723/실패 0 ↔ Client 723 수락/0 거부, 양쪽 필수 표현 483/483·실패 0. 이 결과·표현 수치는 LittleKnight·Pistoleer도 포함한 경기 전체 집계이며 FlameSpirit 각 회차의 6개 HitIndex offset·정확한 화면/피해 subframe 일치 증거는 아니다. `MigrationRequired`/v2 Complete 미달 유지. 근거: `_Logs/_editor/2026-09-29/RuntimeLog.txt`, `_Logs/2026-09-29/00_12_logcat/RuntimeLog_device.txt`(같은 sharedSessionKey는 FlameSpirit Task 후속 절). |
| InfernoSpirit | 600 | 250 | 4.0 | 4.0 | 1 | 1:15(3:00) | 30초 | 400 | 1 | 단일 대상 on-hit DoT(구현 완료, 수치는 ×10 설계값) — 원거리 공격이 주 타깃 1마리에 **직접 250**(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성 포함) + 그 **주 타깃 1마리에게만** DoT 50/초×3초(총 150). AoE 아님(반경 없음). DoT는 **적 유닛만**(건물 제외 — 건물은 직접 250만), 주 타깃 유닛은 직접+DoT, 아군 무피해. DoT는 규칙 40 초 단위 discrete 틱 재사용(1초 간격·틱당 올림·총량 150 클램프·매초 남은 체력 데미지 텍스트·갱신 리셋·서버 권위). DoT 값은 MushroomBomber(20/3)와 별도 진입점(`ApplyInfernoDot`)으로 분리. 상세: GameSystemRules_Units 규칙 41~42 |
| DustSpirit | 400 | 60 | 0.5 | 1.0 | 0.5 | 1:04(3:00) | 5초 | 50 | 1 | |
| BoulderSpirit | 900 | 80 | 0.5 | 1.0 | 0.5 | 1:10(4:00) | 15초 | 100 | 1 | |
| QuakeSpirit | 2500 | 200 | 0.5 | 1.0 | 0.5 | 1:20(5:00) | 30초 | 400 | 1 | 착탄형 즉발 AoE(구현 완료, 2026-07-20 멀티플레이 로그로 메커니즘 검증 / 수치는 ×10 설계값) — 주 타깃 1마리 **직접 100%(공격력 200)**(스플래시 제외, 건물이면 100% 공성) + 착탄 **월드 좌표 원형 반경**(`_quakeRadius` 기본 1.0=인접 1칸) 내 **다른 적 유닛·적 건물 50%**(올림 `CeilToInt(200×0.5)`=100, 주 타깃 제외). DoT 아님(즉발). ⚠️ **MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함**. 아군 무피해, 서버 권위. MushroomBomber 원형 반경 헬퍼(`CollectEnemyUnitsInRadius`, `internal static`) 재사용 + 건물 순회(`CollectEnemyBuildingsInRadius`) 신설, 유닛/건물 hit-set 분리. `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5). **타격 시점 후속 교정(2026-09-14):** production Attack clip `OnAttackHit` 1.667초와 `UnitStatsConfig.hitFrameTimes` 1.667초 정렬 및 Supported 승격, Unity 자동 게이트 PASS. Android 빌드는 시작만 확인했으며 완료·설치·실기 결과는 OPEN. 상세: GameSystemRules_Units 규칙 43 |
| TideSpirit | 300 | 70 | 0.5 | 1.0 | 1 | 1:15(3:00) | 5초 | 50 | 1 | |
| StreamSpirit | 300 | 60 | 3.0 | 3.0 | 0.5 | 0:17(1:15) | 10초 | 100 | 1 | |
| TorrentSpirit | 1000 | 200 | 3.0 | 3.0 | 0.5 | 2:10(4:00) | 30초 | 400 | 1 | 파도형 이동 AoE(기존 메커니즘 구현, 수치는 ×10 설계값) — 전방으로 이동하는 월드 좌표 직사각형(폭 `waveWidth` 3 × 전방 `waveLength` 3, 타겟 방향) 전선이 닿는 **적 유닛·적 건물 200 피해 / 아군 유닛 100 힐**, 각 대상 1회. special-only(단일 공격 없음). 힐량·파도 파라미터는 SpecialAttackConfig, 피해는 공격력. 아군 힐 = 공격력×0.5(비율 불변). **`2:10`은 사용자가 수정·저장한 Attack 클립의 파도 이펙트/발사 마커 `초:프레임(30fps)`으로, `2+10/30=70/30≈2.3333333초`다.** 이 값은 공격 시작부터 파도 **발사**까지의 시각이며, 적/아군별 피해·회복은 그 뒤 서버 전선이 각 대상에 닿는 시각에 적용된다. 발사 후 전선 이동 시간 `waveTravelTime=0.5초`와 공격 주기 `4:00`(4초)는 별개로 유지한다. 이 시점 교정은 설정·생산 연결 구현 및 Unity 검증 전 **OPEN**이며, 클립은 이번 문서 갱신에서 재확인하지 않았다. 상세: GameSystemRules_Units 규칙 28~31 |

## 유닛 — 초월계 (Transcendence)

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 공격 쿨다운 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
|------|----|--------|--------------------|--------------------|------------------|------------------|-----------|-----------|------|------|
| FoxMagician | 200 | 150 | 3.0 | 3.0 | 0.5 | 2:25(4:00) | 5초 | 70 | 1 | |
| BearGuard | 2000 | 60 | 0.5 | 1.0 | 1 | 0:13(1:20) | 40초 | 400 | 1 | 30fps 타격 `0:13` = `13/30초`(클립·type 20 설정 저장값 `0.43333334초`). Unity 두 self-validation PASS, 2026-09-30 사용자 육안 수용으로 타격 focused 실기 PASS/CLOSED. 괄호의 설계 주기 `1:20` = `1.666666…초`와 실제 `UnitStatsConfig.attackCooldown=1.2초` 불일치는 별도 OPEN; 이번에는 타격 시각만 교정하고 cooldown은 변경하지 않음. 정확한 BearGuard 단독 marker→피해 offset·공식 CrossAudit/전체 migration 검증은 별도. |
| LionKnight | 500 | 90 | 0.5 | 1.0 | 2 | 0:18,1:13(3:00) | 15초 | 200 | 1 | 2히트 타격 목표(30fps): `0:18` = `18/30 = 0.6초`, `1:13` = `43/30 ≈ 1.4333333초`. 괄호의 공격 주기 `3:00` = 3초 유지. 클립·type 22 설정 저장 및 Unity/실기 검증은 별도 진행 중. |
| RhinoBreaker | 600 | 100 | 0.5 | 1.0 | 2 | 1:05(2:00) | 20초 | 200 | 1 | |
| EagleArcher | 350 | 60 | 3.0 | 3.0 | 1 | 0:10(1:00) | 15초 | 150 | 1 | |
| RabbitTrickster | 200 | 60 | 0.5 | 1.0 | 2 | 0:18(2:00) | 5초 | 50 | 1 | |
| MushroomBomber | 400 | 100 | 2.0 | 2.0 | 1 | 1:00(3:00) | 15초 | 200 | 1 | 착탄형 AoE(구현 완료, 수치는 ×10 설계값) — 착탄 대상 1마리 **직접 100**(주 타깃 단일 피해, 건물 공성 포함) + 착탄 중심 **월드 원형 반경**(`blastRadius` 기본 1.0=인접 1칸) 내 **적 유닛** DoT 20/초×3초(총 60). DoT는 **1초 간격 discrete**·**매초 남은 체력 데미지 텍스트**·틱당 올림(총량 60 클램프). 건물은 직접만·DoT 제외(주 타깃 건물이어도 주변 유닛 DoT), 주 타깃 유닛은 직접+DoT, 아군 무피해. 서버 권위. special-only 아님(`ReplacesPrimaryAttack=false`). 생산: SporePatch(식물 1단계). 상세: GameSystemRules_Units 규칙 38~40 |
| BloomFairy | 500 | - | 4.0 | 4.0 | 1 | 1:00(3:00) ⚠️예외 | 20초 | 150 | 1 | 아군 단일 지정 힐 200 HP/3초 (공격 불가, 기존 회복 구현, 수치는 ×10 설계값). 생산: FloralNursery(식물 2단계). **2026-10-03 디스크 구현 확인: 30fps `1:00`=1.000초에 OnAttackHit 1개, 서버 `HitFrameTimes[0]`도 1.000초; Attack 클립은 3초 loop 유지, Game type27에 정확한 클립 명시 연결 및 production gate 추가.** ⚠️ **힐 주기 = 발동 준비 1.0초 + 발동 후 쿨다운 3.0초 = 실제 주기 4.0초**. **쿨다운 예외**: 다른 모든 유닛은 쿨다운이 발동 준비를 포함한 전체 주기(공격 시작 시 쿨다운 시작, `TryAttack` 패턴 — 위 표기 관례상 `(3:00)`=전체 3초)지만, **BloomFairy만 쿨다운(3.0s)이 힐 발동 준비(1.0s)를 미포함** → 발동 후부터 쿨다운 카운트라 실제 주기 4.0초. 3초 클립 길이는 실제 힐 주기4초와 별개다. **의도된 설계(버그 아님) — 다른 유닛과 같게 되돌리지 말 것.** 회복 발동은 애니 이벤트 `OnAttackHit`이 아니라 상태머신 타이머로 구동(`OnAttackHit`은 연출 전용). HoT diff 틱·종료 시 현재HP 텍스트1회·사망 시 생략 유지. Healer N/A/SeparateHealPipeline, attack VFX preset 미연결. **2026-10-03 후속 구현 확인:** 힐 전용 atomic current-state 복제와 Client 모션 cursor, 새 회차 재생/동일 회차 멱등·late init catch-up·one-shot marker·종료/사망 차단·Frozen/Held 및 Walk 덮어쓰기 방지. Aim은 증거용이고 새 Client Root/Visual offset 회전 없음. HP/HoT writer·원수치 불변. 새 Unit Action 04:52:02 및 Root SelfValidate 04:54:03 자동 PASS, 실제 경기 Analyze 아님. 최종 정적 컴파일 Runtime0/20·Editor0/0·PlayerDevelopment0/20·Release0/24(errors/warnings), 각 exit0; 메뉴 뒤 Console errors0/warnings25. Build And Run1회 클릭 후 Checking prerequisites/Starting Android build 관찰, 완료·설치·실행 미관찰, UI/build polling 중단. 이전 모션 FAIL 후 **실기 재검증 OPEN**, runtime PASS/Supported 승격 아님. 초기03:42 자동 게이트/JDK 및 실기 실패 이력은 기존 Bloom task Research/Plan §1~§8, 최종 파일/증거는 §9 참조. 상세: GameSystemRules_Units 규칙 22·32~37 **이전 경기 당시 분석 경계(2026-10-03 12:36~12:39, 6239bf47…9faf1; 아래 제안·미구현은 당시 상태이며17시 코드/에셋 적용 전 이력):** Bloom8기의 HostStart/HostPlay/ClientPlay50회 exact 회차 일치(missing/extra/conflict/duplicate0), Marker49/MotionEnd45 각각. 서버 Cast49/Applied43, ActualHealed 합4110은 Client HP/text exact 검증이 아니다. Client future-start32/50 offset0 즉시 재생과 실제 Walk-Held-Walk 수신이 확인됐으나 render pose/facing 증거는 부족하다. **힐 전달 성공 / 방향·모션 연결 미해결 — OPEN**. local ROOT PASS·MOVE 실패0은 heal continuity/facing PASS가 아니다. 점진 heal-facing/future-start wait/stationary handoff는 제안·미구현이며 규칙·원수치 변경 없음. 최신 근거는 Bloom task Research/Plan §10, 이전 §9는 이력이다. **최신 승인 정책/코드 인계:** Stats 표의 `1:00(3:00)`는 marker/쿨다운 예외이고 저장 clip `4:00`은 별개다. **현재 승인 힐 표현 정책:** marker1초(30fps `1:00`)는 유지하고 저장 clip4초(`4:00`)로 한다. 원본0~1초 보존, 후반1~3초→1~4초 시간1.5배/tangent 역수 보정·1초 curve 경계 검증을 native AnimationUtility로 수행한다. 일반 힐 endpoint 강제 Play/speed0 hold는 검증 전 삭제 없이 비활성화·주석 보존하고 speed1을 사용한다. 다음 유효 힐 target이 있으면 중간 inactive Walk/Held 없이 새 sequence로 연결하며 힐-only0.15초 blend는 시험값이다. 실제 Frozen/Held/target 없는 대기·사망·경기 종료는 유지한다. 서버 Action 소유권과 활성 priority1 아래 공통 RotateActionTowards를 재사용하고 Client Root/Visual offset writer는 금지한다(NET-AUTH/NET-ROOT). future-start는 IsDue 전 재생하지 않고 late-init catch-up·one-shot marker·sequence/revision/lifetime 멱등·terminal 차단을 유지한다. priority cache는 실제 적용 후 갱신한다. 양 peer 힐러는 config cooldown3을 보존하여 clip4 덮어쓰기의5초 회귀를 차단하며 일반 유닛 clip-derived cooldown은 유지한다. 준비1+발동 후3=cycle4·HoT200/3·서버 writer는 불변이다. **2026-10-03 승인 교정 코드 인계:** 기존 source4파일과 새 Setup.cs/.meta 작성 및 마지막 수정 뒤 정적 컴파일 Runtime0/20·Editor0/0·Dev0/20·Release0/24(errors/warnings) 확인. 서버 Action/priority1의 공통 RotateActionTowards, 연속 유효 target의 inactive 없는 다음 sequence, future-start IsDue 대기·실제 적용 뒤 priority cache 갱신, speed1/기존 snap·endpoint hold 주석 보존, 양 생성 경로 sharedApplyAttackClipCooldown의 힐러 config3 보존 및 future-start/priority-pending/terminal/current-value/cooldown/native-retime-helper fixture 보강이 코드에 반영됐다. native Setup은17:42:31 `[BloomHealSetup] native retime PASS`(changed=True, curves=130, front/tail samples preserved, clip=4s, marker=1s, both heal blends=0.15s)로 완료됐다. 디스크 양 prefab `_healCastBlend: 0.15` 및 OnAttackHit time1 확인. 클립/prefab3파일은 실제 변경이다. 이는 에셋/retime 검증이지 visual PASS가 아니다. **새 코드/에셋 자동 검증 완료:** Unit Action17:43:56의 BLOOM-HEAL-TIMELINE·UAS overall·production·C2·C3 PASS, Root Cross Audit SelfValidate17:44:41 PASS. Console errors0/warnings25(기존 compile20+fixture5)이며 Android warnings0·시각 실기 PASS를 뜻하지 않는다. native 디스크 m_StopTime4/m_SampleRate30/단일 OnAttackHit time1 및 양 prefab blend0.15 확인. **빌드 시도 완료:** File→Build And Run1회 실제 클릭 직후 Importing(iteration2)–Finishing import Settings/Mobile_RPAsset.asset 관찰까지만 확인했다. 이후 Unity UI/Editor/build log polling 및 완료·설치·게임 실행 확인은 하지 않았다. 빌드 성공 추정 없음. Setup·자동 메뉴·빌드 시도는 완료이며, 남은 시각 실기 검증은 OPEN이다. 신규 실기 OPEN, §9~§10은 이전 검증/경기 이력이며 §11은 승인 계획이다. 위 제안·미구현/3초 clip 표기는 당시 구현·분석 이력이며 현재 승인 정책과 구분한다. **이전 실기 분석 — 2026-10-03 18_34 세 observer run(Research/Plan §13):** run별 exact 힐 전달·future-start 실제 재생 대기 seam은 확인됐으나 **facing FAIL(사용자 보고) / 원인 OPEN**이다. B/C는 같은 shared key라도 새 runId/BEGIN/startedAt으로 분리한다. local ROOT PASS·MOVE 실패0·서버 completed HoT 결과는 실제 target-facing/model-pose 또는 Client HP/text 전수 검증이 아니다. payload Aim은 caster 현재 forward이며 target 방향이 아니고 Client 회전 writer도 아니다. target cache 시작1회/회차 중 재조회 없음은 source 제약일 뿐 이번 cache/guard 누락 발생·원인은 미확정이다. 다음 exact 각도/ownership/cache/rendered-forward trace는 별도 승인 필요한 제안·미구현이며 expected yaw는 실제 서버 caster→target 벡터로 계산해야 한다. 모델 offset/Client Root writer fix 금지, 정책·수치 불변. §12 구현/자동 gate/빌드 시도는 보존된 이력이다. **교정 전 두 테스트 분석(2026-10-03 22_48/23_04; Bloom Research/Plan §15):** 두 번째 device 파일의 첫 테스트 전체 prefix는 capture 중복이므로 두 run만 집계한다. exact 전달과 제한된 callback Root 수렴/self drift는 확인됐지만 pixel 방향 PASS는 아니다. 같은 Host clock·exact 회차에서 실제 첫 positive HP 적용이 accepted marker보다 먼저인2사례로 **TIMING EARLY HP FAIL**을 확인했다. 사용자 시각 방향 증상·최종 원인·exact Client HP/pixel timing은 OPEN이다. delta 누적/첫 frame 이전 delta 구조는 강한 원인 의심이며 absolute server dueTime=StartedAt+MarkerOffset은 다음 승인 대상 제안·미구현이다. §14 진단4파일/정적 errors0과 §12 에셋/자동 gate/빌드 시도는 이력으로 보존한다. 정책·스탯·native4초/marker1초(30fps4:00/1:00)·blend0.15초·준비1+post3·HoT200/3 불변. 상세 수치·raw/video 관측 범위는 §15 참조. **§16 승인 교정 구현 인계:** UnitView 한 파일에서 accepted StartedAt+MarkerOffset(1초/30fps1:00) absolute due를 같은 HealNow로 기다리고 yield 뒤 재조회하며 caster/authority/동일 active 회차·finite time guard 후 기존 CastHeal을 단일 실행하도록 교정했다. 이전 dt wait는 사용자 수용 전 주석 비활성화 보존, 기존 diagnostics Due/Now/Elapsed·DispatchRejected 반영. 최종 정적 Runtime0/22·Editor0/0·Development0/22·Release0/26(errors/warnings), 기존 Unit Action overall/Bloom/Production/C2/C3 및 Root Pose Cross Audit SelfValidate 새 실행 PASS는 자동 prebuild gate이지 새 heal coroutine runtime fixture/실기 수용이 아니다. **이번 빌드 시작 시도 확인:** 메인이 Unity File→Build And Run1회 클릭 직후 modal 제목 `Detect Java Development Kit (JDK)`/본문 `Checking Java Development Kit (JDK)...`를 관측했다. 완료·설치·runtime은 미관찰이며 사용자 지시로 이후 모든 UI/build log polling을 중단했다. 빌드 성공 추정 없음. §15 early HP FAIL은 교정 전 이력으로 유지하며 재시험/시각 방향·최종 원인 OPEN이다. 회전·HP/HoT·native clip/marker·정책/스탯 무변경, global cooldown ticker/post3 deadline은 미교정·별도 OPEN이다. 상세 actual source/gate/빌드 경계는 기존 Bloom Research/Plan §16 참조. |

---

## 건물 — 인간계 (Human)

> **HP·타워 공격력은 ×10 스케일 값(구현 완료 — config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)).** 건설 비용·업그레이드 비용·채굴 수입·타워 사거리·쿨다운·힐 범위는 불변.

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (Castle) | Castle | 2000 | - | - |
| 채굴소 (MiningPost) | MiningPost | 1000 | 50 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 근거리A 1단계 (TrainingCamp) | TrainingCamp | 1000 | 100 | 100 |
| 근거리A 2단계 (WarAcademy) | WarAcademy | 2000 | - | 200 |
| 근거리A 3단계 (HumanBarracks) | HumanBarracks | 3000 | - | - |
| 총기류 1단계 (Gunsmith) | Gunsmith | 1000 | 100 | 100 |
| 총기류 2단계 (Armory) | Armory | 2000 | - | 200 |
| 총기류 3단계 (WeaponForge) | WeaponForge | 3000 | - | - |
| 탈것류 1단계 (Garage) | Garage | 1000 | 200 | 300 |
| 탈것류 2단계 (VehicleBay) | VehicleBay | 2000 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 자동포탑 (CannonTower) | AutoTower | 500 | 150 | 150 | 4.0 | 5.0s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 비행시설 (FlightFacility) | FlightFacility | 1000 | 200 | - | - |
| 기술연구소 (TechnicalLaboratory) | Research | 1000 | 200 | - | 유닛 강화(연구소) — GameSystemRules_Upgrade.md |

---

## 건물 — 정령계 (Spirit)

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (SpiritNexus) | Castle | 1500 | - | - |
| 채굴소 (ManaRift) | MiningPost | 500 | 50 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 불 1단계 (FireSpire) | FireSpire | 500 | 75 | 200 |
| 불 2단계 (BlazeConduit) | BlazeConduit | 1000 | - | 400 |
| 불 3단계 (InfernoCore) | InfernoCore | 4000 | - | - |
| 물 1단계 (AquaSpring) | AquaSpring | 500 | 75 | 200 |
| 물 2단계 (TidalNexus) | TidalNexus | 1000 | - | 400 |
| 물 3단계 (OceanicHeart) | OceanicHeart | 4000 | - | - |
| 땅 1단계 (StoneMound) | StoneMound | 500 | 75 | 200 |
| 땅 2단계 (TerraForge) | TerraForge | 1000 | - | 400 |
| 땅 3단계 (GaeaSanctum) | GaeaSanctum | 4000 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 방어포탑 (RuneSpire) | AutoTower | 1500 | 200 | 150 | 4.0 | 3.5s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 스킬건물 (MagicSpirit) | MagicBuilding | 1000 | 200 | - | - |
| 연구건물 (AstronomicalSpirit) | Research | 1000 | 200 | - | 유닛 강화(연구소) — GameSystemRules_Upgrade.md |

---

## 건물 — 초월계 (Transcendence)

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (ElderTree) | Castle | 3000 | - | - |
| 채굴소 (FungalNode) | MiningPost | 1500 | 100 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 동물A 1단계 (PrimalAltar) | PrimalAltar | 1500 | 125 | 200 |
| 동물A 2단계 (PrimalDen) | PrimalDen | 3000 | - | 300 |
| 동물A 3단계 (PrimalSanctuary) | PrimalSanctuary | 4000 | - | - |
| 동물B 1단계 (FeralAltar) | FeralAltar | 1500 | 125 | 200 |
| 동물B 2단계 (FeralDen) | FeralDen | 3000 | - | 300 |
| 동물B 3단계 (FeralSanctuary) | FeralSanctuary | 4000 | - | - |
| 식물 1단계 (SporePatch) | SporePatch | 1500 | 125 | 200 |
| 식물 2단계 (FloralNursery) | FloralNursery | 3000 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 방어포탑 (VineTower) | AutoTower | 1000 | 175 | 150 | 4.0 | 5.0s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 힐 건물 (MistShrine) | HealShrine | 500 ※ | 100 ※ | **미확정** ※ | 물안개 지속 힐 — 아군 유닛+건물 회복 (**2026-08-12 구현 완료 / 멀티 미검증 · 회복량·지속·쿨다운·반경은 임시값**) |
| 스킬건물 (WillowShrine) | MagicBuilding | 1500 | 200 | - | - |
| 연구건물 (AncientGrove) | Research | 1500 | 200 | - | 유닛 강화(연구소) — GameSystemRules_Upgrade.md |

> **※ MistShrine 재설계 (2026-08-10 확정) — 위 수치는 밸런싱 재검토 대상이다.**
> MistShrine은 **방어 타워가 아니라 공격하지 않는 별도 힐 건물**이다(Transcendence 방어 타워는 위 §방어 건물의 **VineTower**).
> 2026-08-10 재설계로 동작이 확정되었다: 시전하면 **건물 중심 고정 원형 범위**에 물안개가 깔리고, 지속되는 동안
> **범위 안의 아군 유닛 + 아군 건물**(시전 건물 자신·Castle 포함)이 **1초 단위 discrete 틱**으로 회복된다.
> 범위를 벗어나면 즉시 끊기는 **아우라 방식**이고, **물안개 지속시간 < 쿨다운**(다운타임 존재)이며, 시전 비용은 없다.
> 물안개끼리는 **중첩되지 않고**(가까운 건물 우선, 거리 동률이면 건물 Id가 작은 쪽), 연구소 **자연회복과는 별개 효과로 중첩 적용**된다.
>
> **밸런싱 미확정 항목:** 회복량 · 물안개 지속시간 · 쿨다운 · 범위 반경 · 회복 텍스트 표시 주기(임시 3초).
> 표의 HP 500 / 건설비 100, 그리고 이전 표기였던 **10 HP/s(범위 3)** 는 **재설계 이전 값에 ×10만 적용한 수치**이므로
> 재설계 후에도 유효한지 밸런싱 단계에서 재확정한다. 상세 규칙:
> `GameSystemRules/GameSystemRules_Buildings.md` — **MistShrine 물안개 힐 시스템**(규칙 16 미확정 표).

---

# 유닛 강화(연구소) 시스템 — 스탯 (구현 완료 — 멀티플레이 실기 PASS)

> 아래 강화 스탯·트랙은 **연구소(Research 건물)로 골드·시간을 들여 팀 단위로 유닛을 강화**하는 신규 시스템의 값이며 **구현 완료(멀티플레이 실기 PASS, 2026-07-31)**. 트랙 구조·데미지 공식·연구소 운영·비용·연구 패널 UI·구현 상태(후속 보류 포함)의 단일 소스는 **`GameSystemRules/GameSystemRules_Upgrade.md`**, 변경 전/후 대조는 `_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md` 참조.
>
> **강화 스탯 3종 + 초월 전용 1종:** 공격력 / 방어력(신규) / 이동속도 + 자연회복(초월계 전용). 최대 HP는 강화 대상이 아니다.
> **적용 방식:** 팀별 연구 레벨에 따른 배율을 데미지·이동을 실제로 쓰는 순간에 곱한다(이미 전장에 나온 유닛도 연구 완료 즉시 소급 강화).

## 방어력 (신규 스탯)

- **전 유닛 기본 방어력 0.** 오직 연구로만 올린다(연구 전에는 방어력 0 → 감쇄 0% → 기존과 완전히 동일).
- **데미지 공식:** `데미지 = Max(1, Round(공격력 × (1 − 방어력 / (방어력 + 120))))` — 최소 1 보장(floor), 감쇄율 하드캡 60~65% 안전장치 포함.
- 방어력 감쇄는 **직격 · 스플래시에만 적용**하고(타워→유닛 데미지 포함) **DoT 틱값에는 미적용**한다(2026-07-23 확정 — 기존 "DoT에도 적용"을 뒤집음).
- 방어력 트랙 레벨 = 0/8/16/24/32/40 (아래 §방어력 트랙).

## 공격력 업그레이드 표 (유닛별, Lv1~5)

> 레벨당 증가폭 = `Round(공격력 × 8%)`의 **고정 정수 등차**. Lv0 = 위 유닛 표의 ×10 기본 공격력, Lv5 ≈ ×1.40. ×10 스케일 덕분에 죽은 레벨(증가폭 0)·불규칙 증가폭이 0건이다.
> **주의:** 공격력 연구는 **고정 DoT 틱값(MushroomBomber 20/s, InfernoSpirit 50/s)에는 반영하지 않는다.** QuakeSpirit 스플래시(공격력×0.5)·TorrentSpirit 파도(=공격력)는 공격력을 쓰므로 함께 커진다. 힐량은 아래 §힐량 트랙.

### 인간계
| 유닛 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 | 증가폭 |
|------|-----|-----|-----|-----|-----|-----|:-:|
| Pistoleer | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| Assault | 10 | 11 | 12 | 13 | 14 | 15 | +1 |
| Sniper | 180 | 194 | 208 | 222 | 236 | 250 | +14 |
| LittleKnight (2히트) | 40 | 43 | 46 | 49 | 52 | 55 | +3 |
| SpearMan | 100 | 108 | 116 | 124 | 132 | 140 | +8 |
| BattleAxe | 150 | 162 | 174 | 186 | 198 | 210 | +12 |
| Tank | 300 | 324 | 348 | 372 | 396 | 420 | +24 |
| CannonCart | 200 | 216 | 232 | 248 | 264 | 280 | +16 |

### 정령계
| 유닛 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 | 증가폭 |
|------|-----|-----|-----|-----|-----|-----|:-:|
| EmberSpirit | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| FlameSpirit (6히트) | 30 | 32 | 34 | 36 | 38 | 40 | +2 |
| InfernoSpirit | 250 | 270 | 290 | 310 | 330 | 350 | +20 |
| DustSpirit | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| BoulderSpirit | 80 | 86 | 92 | 98 | 104 | 110 | +6 |
| QuakeSpirit | 200 | 216 | 232 | 248 | 264 | 280 | +16 |
| TideSpirit | 70 | 76 | 82 | 88 | 94 | 100 | +6 |
| StreamSpirit | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| TorrentSpirit | 200 | 216 | 232 | 248 | 264 | 280 | +16 |

### 초월계
| 유닛 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 | 증가폭 |
|------|-----|-----|-----|-----|-----|-----|:-:|
| FoxMagician | 150 | 162 | 174 | 186 | 198 | 210 | +12 |
| BearGuard | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| LionKnight (2히트) | 90 | 97 | 104 | 111 | 118 | 125 | +7 |
| RhinoBreaker | 100 | 108 | 116 | 124 | 132 | 140 | +8 |
| EagleArcher | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| RabbitTrickster | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| MushroomBomber | 100 | 108 | 116 | 124 | 132 | 140 | +8 |

## 방어력 트랙 (전 유닛 공통, K=120)

| 레벨 | 방어력 | 감쇄율 = 방어/(방어+120) | 실효 HP 배율 = 1/(1−감쇄) |
|------|:-:|:-:|:-:|
| Lv0 | 0 | 0% | ×1.000 |
| Lv1 | 8 | 6.25% | ×1.067 |
| Lv2 | 16 | 11.76% | ×1.133 |
| Lv3 | 24 | 16.67% | ×1.200 |
| Lv4 | 32 | 21.05% | ×1.267 |
| Lv5 | 40 | 25.00% | ×1.333 |

> Lv0(방어 0)이면 감쇄 0% → 기존과 완전히 동일(하위호환). Lv5 감쇄 25% = 실효 HP +33%로 공격력 Lv5(+40%)와 대략 대칭.

## 이동속도 트랙 (전 유닛 공통 배율)

| 레벨 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|
| 이동속도 배율 | ×1.000 | ×1.064 | ×1.128 | ×1.192 | ×1.256 | ×1.320 |

> 레벨당 +6.4%p, Lv5 +32%. 이동속도 기본값(칸/초)은 유닛 표대로 불변이며, 이 배율은 연구로만 붙는다.

## 힐량 트랙

**BloomFairy 힐 (초월 식물 그룹의 공격력 트랙을 따름, 증가폭 +16/Lv):**

| 레벨 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|
| BloomFairy 힐(HP/3초) | 200 | 216 | 232 | 248 | 264 | 280 |

**TorrentSpirit 아군 힐 (= 물 공격력 × 0.5):**

| 레벨 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|
| 물 공격력(TorrentSpirit) | 200 | 216 | 232 | 248 | 264 | 280 |
| TorrentSpirit 아군 힐(×0.5) | 100 | 108 | 116 | 124 | 132 | 140 |

## 자연회복 (초월계 전용)

> 초월계 전 유닛 대상 상시 HoT(전투 중 포함, 최대 HP 미변경·Heal 클램프). 기존 힐/DoT 틱 인프라 재사용.

| 레벨 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|
| 자연회복(HP/초) | 0 (미연구) | 3.0 | 6.0 | 9.0 | 12.0 | 15.0 |

> 레벨당 +3.0 HP/s.

## 연구 비용·시간 (표준 트랙, 스탯 1종 Lv1~5)

| 레벨 | 골드 | 연구 시간 |
|------|:-:|:-:|
| Lv1 | 80 | 15초 |
| Lv2 | 120 | 25초 |
| Lv3 | 180 | 35초 |
| Lv4 | 260 | 50초 |
| Lv5 | 360 | 70초 |
| **합계** | **1,000** | — |

**그룹 비용 배율 (효과 동일, 비용만 배율):** 초월 동물 ×2.0(합 2,000) / 자연회복(초월 공용) ×2.5(합 2,500) / 초월 식물 포함 그 외 전부 ×1.0(합 1,000). 연구 시간은 배율과 무관하게 표준 유지. 연구소 건설비 200골드(불변). 상세: `GameSystemRules/GameSystemRules_Upgrade.md`.
