# Assault 타격 시점 교정

## 2026-10-11 사용자 focused 수용 — 다음 후보 추천만

사용자 최신 확인: **“로그상으로 문제없으면 됐어 확인했어 다음 작업 어떤거할거야?”** Main은 최신19_07 두 경기의 Assault·Sniper·Inferno 정상 관측을 현재 focused 범위에서 충분한 근거로 판단하고 **로그 기반 사용자 수용·다음 진행 가능**을 인계했다. 이는 정밀 pixel/subframe·exact payload/HP 동일·global migration 완료가 아니다. 아래 overall END FAIL 분석은 유효하며 Invalid 진단 scope·과거 Editor Client Expired63 원인/해결은 별도 OPEN이다. 역할 반대·suppressedNormal63≠Expired63·정상 retry109/dead-abort 실기 미재현 경계를 유지한다.

**Main 다음 추천은 LittleKnight(추천만, 새 Task·구현 승인 없음).** 매트릭스의 단독 focused 교정 미완·현재2hits0.25/1.15초를 기준으로 숫자 재설계 없이 추후 승인 작업에서 source/사용자 clip30fps를 확인한다. projectile 미준비 영향 없음은 Main 인계다. 이번 source/clip 조회·수정·Git/build·새 Task/TC 없음; 아래 수용 미확인 문구는 당시 이력으로 보존한다.

## 2026-10-11 19_07 저장 로그 최종 분석 — 전체 FAIL / 진단 scope OPEN

이번 두 경기는 권위 결과·Coordinator Ready와 정상 초기화 관측을 확보했지만, 기존 Shadow 입력 자격의 진단 집계 때문에 전체 END는 양쪽 FAIL이다. **기존63건 해결 PASS나 전체 작업 완료가 아니다.** 아래는 Main/programmer의 최종 로그·소스 검토 인계이며 문서 담당의 추가 raw/source 실행 검증은 아니다. 이전 기록은 당시 이력으로 보존한다.

- **원본/역할:** Client `Assets/_Project/Docs/_Logs/2026-10-11/19_07_logcat/RuntimeLog_device.txt`, Host `Assets/_Project/Docs/_Logs/_editor/2026-10-11/RuntimeLog.txt`. session `02bca677971568eeaee28d578c9d492e789bc68a65566f667f8a5f9560bb1103`(18:53~18:57), `8b7eaec2ccc44c68b457aaa022ddba9817ab03724076daf53c155f135f069958`(19:01~19:05). 이번은 **Editor Host / Android Client**, 이전00_46 Android Host / Editor Client와 반대다. 이전 Editor Client63만료의 재현/해결을 증명하지 못한다.
- **경기별 집계:** 1차 Host/Client Ready452/452·released452/452·required presentations435/435, 2차 Ready398/398·released398/398·required presentations387/387. Coordinator fail0/pending0, C2 resultfail0/Client rejected0. 합계850 Ready는 단순 집계이며 **exact-key payload 일치·HP 동일의 증거로 확대하지 않는다**. 신규 device timeout0/Expired0/D로그0; 2차 Client END `suppressedNormal=63`은 이번 Expired63이 아니다(Ready398/fail0).
- **Retry:** Main의 경기별 unique unit52/57, 최대 대기0.067/0.501초. 1차 Assault30/Sniper13/Inferno9, 2차 Assault33/Sniper13/Inferno11; terminal109건 모두 정상 initialized. dead-abort/timeout 실제 사례는 없어 사망 대기 교정의 표적 재현은 미확인. Editor 과거 Unit52 timeout20579행은 신규 시작26187행 이전이므로 재계수하지 않는다.
- **전체 FAIL 경로 확정(Main+programmer):** `HitPresentationQueue.ObserveLegacy`1065행은 `evt.AttackerIsUnit`이면 AttackerId=-1/default key도 Bridge에 전달한다. `LegacyObservation.IsValid`의 attacker>=0 요구→Invalid→Observer `_presentationInvalid`→`DetermineLocalVerdict` overallFAIL 경로다. 별도 ResultPresentation axis의 `AuthoritativePresentationFailureCount=0`이므로 presentation EVIDENCE와 overallFAIL이 공존한다.
- **생성/전파 경로와 한계:** `ApplyTimedDamageToUnit`2718행은 attacker null이면 -1/AttackerIsUnit=true/ImmediatePresentation=true/default result key를 발행하고 `NetworkHealthSync`373행이 Client로 전파한다(Main 소스 검토 인계). 기록 패턴과 일치하지만 각 틱의 정확한 원인 유닛·조회 실패 사유는 미확정이며 **Inferno로 단정하지 않는다**. attacker=-1/instance0/seq0 Invalid는 gameplay HP·텍스트·VFX 오작동 증거가 아니다.
- **중복 집계 경계/Main device 분류:** 1차 Invalid4줄=피해2사건×Enqueued/EmittedImmediate2단계(victims13,9); 2차14줄=피해7사건×2단계(victims71,73,74,90,93). Host 동일 count는 미확인. 대표 Host28071/28072·41578/41579, Device51285/51291·187109/187115행. 양 peer 예시8줄은8errors가 아니라2사건×2단계×2peers이며 전수 Host count로 해석하지 않는다.
- **독립 검증/OPEN:** 양 경기 양쪽 local ROOT PASS4건; formal CrossAudit 미실행. 신규 expiry/D0은 기존63건 원인/해결 확정이 아니다. 진단 적용 범위·결과 집계 정합성 검토는 **제안만/OPEN**, 구현 승인·수정 없음. precise timing/pixel·멀티 HP 화면 동일·전체 수용은 미확정. 이번은 기존 문서만 갱신하며 새 Task/TC·Git/UI/build/code/asset/new implementation 없음.

## 2026-10-11 00_46 현재 후속 — 사용자 육안 관찰과 두 교정 승인

**현재: 구현 완료 / 실기 수용 미완료(Main 최종 검토·programmer 결과 인계).** 기존4소스 저장·최종 해시 일치, static4 errors0 및 CLR3 gates PASS. retry는 실제18시나리오 PASS로 증가했다. native Unity callback/NGO PlayerLoop·fresh UnitAction/ROOT·멀티 HP 화면/실게임·빌드는 미실행이며 원63건 원인/해결 OPEN이다. 아래 준비·부분 검토의16시나리오 및 검사 pending 표기는 당시 이력이다.

사용자는 이번 화면에서 육안 문제가 없다고 보고했지만, 로그의 Assault 표현 만료63건과 이미 죽은 유닛의 초기화 재시도는 별도로 교정하기로 승인했다. 실제 피해 권위는 유지하며, 이 절은 앞선 날짜별 기록보다 최신인 조사·구현 준비 단계다. 육안 관찰은 C3 실패 해소나 정밀 timing/pixel PASS가 아니다.

- **최종 경기 근거(Main/programmer):** Android Host / Editor Client, session `7ae2bf913d26ef160260670c64aaf46b18f7aeab810be5aa3f7173c70582f367`. 저장 로그 `_Logs/2026-10-11/00_46_logcat/RuntimeLog_device.txt`, `_Logs/_editor/2026-10-11/RuntimeLog.txt`(Docs 기준).879 exact-key payload 일치; HostReady879/fail0, ClientReady816/Expired63. 전부 Assault type1/HitApplied/hit0/revision5, frame32876/32892/32894에22/14/27건. 피해 누락·미수신63이 아닌 표현 만료다.36.322~37.868초 만료는 GameEnd retire45.347보다 앞/Cancel0,10건 source VFXStarted/sourceOnly0.
- **시간 근거의 해석:** 상세16건의 publishAfterLimitFalse/receiveAfterLimitEstimateFalse/decisionAfterLimitTrue는 보존한다. frame gap0.910803초 및 same-frame receive189.62629→fresh canonical Tick190.736736 차이 확인. Main/programmer의 로컬 NGO 소스 인계는 NetworkManager.cs EarlyUpdate336/incoming349→PreUpdate tick399/ServerTime cached864 순서다. RPC 수신은 이전 갱신 표본일 수 있어 receiveBeforeLimit 추정만으로 실제 정시 수신을 확정하지 않는다. 실제 canonical-wave-server 판단에 bundle-anchor 진단 오염은 없다. 장프레임 대 시계 기여도·네트워크 근본 원인은 아직 미확정; 정책 변경/Expired63 해결 선언 없음.
- **Retry 원인 확정 인계:** Unit52 Blue Sniper spawn37.319→등록37.322→HP15037.324→HP0/사망37.326~327→timeout40.339. 기존 retry loop의 death/domain replacement/service/controller-owner 수명 guard 누락을 programmer가 원인으로 확정했다. 이전 가능성 단계는 이력이다. prefab 미등록이나 Assault63과의 직접 인과로 바꾸지 않는다.
- **독립 범위:** Sniper binding Host14/Client13, marker1.733333349초/cycle3, 결과99(71Hit/28Miss) Ready99/Expired0·VFX98 양쪽은 기술 근거다. Inferno0으로1.5 실기 평가 없음. MOVE 주요Fatal/공간 mismatch0·양쪽 local ROOT PASS는 공식 cross-audit(미실행)/전체 timing PASS가 아니다.
- **현재 승인:** Plan의 기존4파일안을 코드 적용 전에 동기화. retry 생명주기 교정과 expiry 원인 분리 진단/실제 clock→Coordinator 회귀이며, Assault 만료 정책 교정 완료는 아니다. 소스 구현/static/native는 후속 인계 전, 이번 build 지시 없음. 새 Task/Sniper Task/TC 없음.

- **Main 부분 소스 검토:** retry owner/lifetime16시나리오는 기존 gate에 호출 연결됐으며 실행 통과16건이라는 뜻은 아니다. D expiry formatter는 기존16상세 bound 안에서 previous RPC 표본/fresh canonical/local elapsed를 추가한다. 실제 `canonicalTickInProgress` bool을 전달하며 lifecycle/validation 호출은 `sampleDeltaKnown=false`/unknown으로 별도 표기한다. CanonicalTick→Coordinator Ready/genuine Expired fixture는0.10초 지연/0.50초 catch-up을 보존한다. 오래된 receive estimate만으로 잘못된 expiry 또는 정시 수신을 증명하지 않는다.
- **Main 보호 확인:** UnitStatsConfig SHA256 `0A907B25728237837C2D5298B3E47EEB8570AD093AC2BE4D1FBE0633F67BC275`는 이전 Inferno1.5 설정과 일치한다. Game.unity SHA256 `6BBFE73F8904A1A55E47D575AE0FD214B21AB18D21E5F6AED494475EE9600C5E`는 재개 읽기 기준점이지 이전 original freeze 증거가 아니다. 이번 차례 에셋/씬 수정 없음(Main 인계). Sniper52/30·Inferno1.5·HP/기존 정책 보존; 문서 담당의 소스/에셋 검사나 최종4파일 해시 대조는 수행하지 않았다.

### 최종 구현·검사 근거 (2026-10-11, Main/programmer 인계)

- **최종 소스 검토/Main 승인:** OnDestroy 구독 해제 및 base call, owner/lifetime shared seam, 마지막 factory 시도 중 사망/동일ID 객체 교체로 Abort되어 timeout을 차단하는 추가2시나리오(timeout-dead/timeout-replaced), D invalid-frame/lifecycle unknown·log budget gate를 확인했다. OnDisable→OnEnable 재개는 GameEnd 이후 `_retryMatchEnded`로 차단한다. 이는 소스 검토/CLR 근거이지 native callback 실행 증거가 아니다.
- **정적 검사/programmer 실제 결과:** Runtime Unity dotnet csc rsp exit0/errors0/warnings20. Roslyn Emit Editor/Development/Release errors0, warnings0/20/24. Main은 static DLL5개(Runtime/ref 포함)의 존재/시간을 확인했다: Runtime/ref17:36:56, Editor17:39:24, Development17:39:31, Release17:39:35; 모두 최종 소스17:35:19 이후다. 이 시간 확인은 산출물 존재/세대 근거이며 native 실행을 뜻하지 않는다.
- **CLR 실행/programmer stdout:** `ValidateUnitViewInitializationRetry()` 실제18시나리오 PASS(부분 검토16에서2추가), `ValidateReceiptPhaseClock()` PASS, `ValidateCanonicalTickClock()` PASS, 최종 command exit0. 실제 `FormatFullLineD`의 보수적 worst-case792 UTF-8 bytes <1000이며 플랫폼 prefix/stack 등을 포함한 실기 전체 로그 길이 판정은 아니다. previous RPC/fresh canonical/local elapsed와 실제 `canonicalTickInProgress`, lifecycle/validation `sampleDeltaKnown=false`/unknown을 구분하고0.10/0.50·HP/권위 결과 정책을 보존한다.
- **미실행/OPEN:** native Unity callback·NGO PlayerLoop·fresh UnitAction/ROOT·멀티플레이 HP 화면/실게임 및 빌드는 미실행. 모든 검사 process 종료(programmer 인계). 원Assault Expired63은 피해 누락63이 아니며 장프레임/clock/네트워크 원인과 실기 해결은 OPEN이다. 새 Task/TC/Sniper Task/Git/asset·scene 변경 없음. 문서 담당은 결과 인계만 동기화했고 소스·에셋 검사/컴파일/CLR을 직접 실행하지 않았다.

**최종4소스 manifest — programmer 최종 해시와 Main 대조 exact match** (`Assets/_Project/Scripts/` 기준). 보호 Stats/Game은 위 「Main 보호 확인」의 전체 SHA256과 일치하며 Game은 재개 읽기 기준점으로만 해석한다.

| 파일 | SHA256 |
|---|---|
| `Infrastructure/Network/NetworkProductionController.cs` | `01441A7AEC4D44C0FB47DB72916CF7381AC4794BCD71DA343E556840A6FD4562` |
| `Editor/Combat/RunUnitActionSelfValidation.cs` | `3AF9850A1B9C20614936494C1308856B145F9D20E04A1AC4B066A95BB56DC7D2` |
| `Infrastructure/Network/UnitAttackShadowObserver.cs` | `7F08D5254D0E898A15490864EBD187E1431797BFFA3E0F340F6E6410DB136C41` |
| `Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | `5ABC4FF41D968013FC393BF4BCE8040763B3DC5503D04BD39DDB01DBFB4D19D1` |

## 21_43 최종 로그·소스 인계 — Main/programmer 확인 / visual OPEN

- **동일 경기:** 위 match/sharedSession의 Android Host / Editor Client이며 아래 수치는 Main/programmer 최종 read-only 인계다. 앞선 부분 집계는 이력으로 보존한다.
- **Assault:** 양쪽 C2/C3 results56 / Ready56, fail/reject/pending0; Client Receive56 전부 Assault. Client VFXStarted52와 exact 결과 교집합51, result-only5(unit0/7/15/30/35)는 attacker death/despawn 종료에 연결되므로 버그5건으로 세지 않는다. source-only1(unit20, instance7, seq3)은 unresolved다. 실제 marker5/30초·cycle10/30초 확인. clockInvalid1은 GameEnd transport closure(pending0), 이번 경기 Expired 없음. 이는 이번 경기 범위이며 이전 original64/remaining50 원인이나 timing/pixel을 닫지 않는다.
- **Inferno 실제 binding:** 양쪽 prefab/controller가 실제0.5초 marker clip에 연결, Stats0.5초/cycle3초 확인. StatsReference `1:15=1.5s`와의 모순은 강한 조기 타격 후보이나 native Start↔HP 정밀 측정은 없다. 현행0.5 확인과 향후 의도1.5 제안은 구분한다. source attack Host raw132/started122/suppressed10, Client131/120/11, playbackFailure0, overflowTrue라 완전 source coverage/visual PASS가 아니다.
- **Burn:** Host Play/Drain/Return 각각32, Client 각각31; Host StopBeforeReady4/staleClosedReject27. Publish40/Receive38 차이는 current-value merge 가능성이 있어 loss2로 세지 않는다. fixed scale1/no recipient bounds fitting 소스 확인은 사용자 크기 관찰과 양립하며 pixel·정확한 원인 수용은 미확정이다.
- **Tank/CannonCart:** current actual Tank 참조/해시 일치. 해당 Legacy 타입별 per-attack source emitter diagnostics가 없어 간헐 누락 원인은 UNKNOWN; Assault C3 성공을 이 두 타입 VFX 성공으로 확대하지 않는다.
- **Tower:** Human22+Spirit8=30, Host publish / Client receive / play exact identities missing/extra0. 현행 source는 방향만 사용하며 actual target transform 추적/거리 refit 없음. 사용자에게 길이가 잘 보였던 관찰을 부정하지 않되 dynamic code fitting의 증거로 쓰지 않는다. 모든 particle/pixel 수용은 미확정이다.
- **Main 후속 제안만(미적용):** 사용자 의도에 따라 Inferno1.5초 여부 확인; burn recipient fitting; Tank/Cannon production boundary diagnostics; Spirit endpoint original identity의 visual-only 추적; Human은 별도 tower preset 참조만 Assault로 변경. 현재 세대상 Tank 참조 계약 유지, 규칙/spec 변경 및 gameplay·소스·에셋·빌드 작업 없음. 기존 Task 안에서 근거/승인 검토를 이어가며 새 Task/TC는 만들지 않는다.

## 2026-10-10 21_43 실기 후속 — 부분 근거 / 분석 OPEN

- **경기 식별(Main 인계):** match `f226c344-2531-47db-8ce1-bc3818fa2eb3`, device Host / Editor Client, sharedSession `febbd6c5502bd937d1b8dd788b73ff8f2cf1959155bbf672262c5d7583e47680`. 저장 로그: `Assets/_Project/Docs/_Logs/2026-10-10/21_43_logcat/RuntimeLog_device.txt` 및 `Assets/_Project/Docs/_Logs/_editor/2026-10-10/RuntimeLog.txt`.
- **사용자 관찰:** Inferno 피해가 공격보다 일찍 보임; burn 크기/모양이 recipient에 따라 너무 크거나 어색함; Tank/CannonCart 효과가 간헐적으로 빠져 보임. 이는 사용자 시각 관찰이며 측정된 타격 오차·누락 건수·원인 확정이 아니다. Assault도 테스트 대상이나 이 단계의 Assault 단독 판정은 없다.
- **후속 요청(미적용):** SpiritTower 표현 수명 동안 원래 맞은 이동 유닛을 endpoint가 추적; HumanTower는 향후 Assault VFX 사용. 현재 Tank/CannonCart/HumanTower actual Tank asset 공유 계약은 그대로다. 새 요청을 현행 사양/구현으로 바꾸지 않으며 규칙·코드·에셋 수정, 새 Task/TC는 없다.
- **Main read-only 소스 확인:** `VfxPoolItem.TimedDamage` line30은 original scale × preset multiplier 고정, Follow line39–40은 position/rotation만 갱신; Burn preset offset0/scale1로 recipient bounds fitting 없음. `EffectManager.PlayAttackEffect` lines405–425는 source/impact vectors만 받으며 target reference 없음. 이는 현재 소스 제한이며 모든 관찰 증상의 runtime 원인을 증명하지 않는다.
- **Inferno timing 미확정:** disk clip marker0.5(line93390)와 StatsReference의 `1:15=1.5s`가 다르다는 Main 확인까지다. 실제 binding/runtime 조사 중이므로 live1.5s나 잘못된 실제 타격값이라고 단정하지 않는다.
- **Editor 부분 로그(Main 확인):** END C3 results56 / ready56 / released56 / fail0. coordinator clockInvalid1, coverageUnknownTrue; Inferno raw131 / started120 / suppressed11, overflowTrue. 타입별 최종 상관관계 전의 subset이며 전체 VFX 완전성·timing/pixel PASS 또는 Tank/Cannon 누락 반증이 아니다.
- **현재 경계:** 앞선16:42/16:43 native PASS는 self-validation 이력이지 새 경기 수용 판정이 아니다. original64 입력 부족·remaining50 원인 및 새로운 타입별 상관관계·clock coverage·시각 원인은 OPEN. 사용자 실기 사실만으로 이전 빌드 시도의 완료/설치 또는 실행 artifact identity를 새로 확정하지 않는다. Main/programmer 최종 로그 근거 후 추가한다.

> **2026-10-10 최종 구현/native·빌드 시도 인계(Main):** UnitAction16:42:39 ALL terminal PASS(C2/C3 ProductionTimeline·baseUAS·VfxConnections 전 경계·Assault canonical current fixture), ROOT16:43:22 PASS, 동일 final fixture generation, Console errors0/warnings30. Tank/CannonCart/Human 실제 Tank identity 연결 및 두 fixture-only lifecycle/Stats 초기화 RED는 해소됐지만 날짜별 실패 이력은 보존한다. final fixture SHA256 `EB64059B75A20B83A5E4A16DD9A9423467B30F7AD2D3456995DBD7B3006B3E5B`, Cannon preset SHA256 `61A7A3F0F77525CE17050EF2648EEB4F361B39A012FC112B7A7F4B9374E4B8F1`. post-gates Main source-only11 Assault/protected 대조 불일치0·final2해시 exact match, 새 fixture 보완에 Production/Stats 변경 없음. **Build And Run16:43:57 KST(07:43:57 UTC) File 메뉴 클릭1회**, 즉시 Checking prerequisites/Starting Android build만 관찰하고 UI/log/process monitoring 전부 중단했다. 완료·설치·실행/runtime/visual 미확인, scene save/Git 없음. 이 native PASS는 기존 self-validation 범위다. original64/remaining50 원인·Assault 실기 timing/pixel·전체 migration은 OPEN이며 counterfactual14 Ready/50 Expired를 실기 해결로 쓰지 않는다. 아래 pending/RED/빌드 미시도는 당시 이력이다.

기존 교차 blocker의 사용자 선택은 세대상 actual Tank 공유로 확정·적용됐고, 후속16:32/16:37 fixture 준비 누락은 production 변경 없이 기존 fixture에서 보완했다. 이번16:42:39 검증은 Assault canonical current fixture까지 실제 실행해 통과한 새 native 근거지만 actual512 제한 replay의 synthetic pending/lifecycle 및 원64 입력 부족을 해소하지 않는다. VFX 참조·fixture 최종2파일 inventory/세부 RED→보완→PASS 이력은 기존 `2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md` 및 `Plan.md`에 보존한다. 보호0:05/Stats/clip·.10/.50·exact keys/cancel/single writer 불변.

> **15:47 native blocker 원인 확정·사용자 선택 대기:** 현재 Tank production `UnitEffectConfig` type6는 `EffectPreset_Cannon_Attack`→`vfx_cannon_attack`이며 UnitView/EffectManager override 없음(Main/programmer 인계). 새 Human tower preset은 `vfx_tank_attack`이어서 actual Tank 공유 fixture와 다르다. 현재 Tank 시각 공유 versus Tank 이름의 새 asset 사용은 Main이 사용자에게 질문한 선택 대기다. 선택 전 Human 계약/Tank/Stats/소스 변경·빌드 없음. ROOT15:52:38 독립 PASS이며 Assault5 freeze/CLR 성공은 유지한다. Action15:47:57 FAIL은 선택 대기로 유지한다.

> **2026-10-10 Assault canonical Tick 최신:** 승인5소스 적용·최종freeze 보고, static4 errors0/exit0 및 Replay/Dev/Release CLR exit0 수신.12:51 canonical Tick/bounds fixture PASS, actual endpoint14 Ready/50 Expired. all-retained Tick33 Ready/31 Expired는 synthetic preseed이며 추가19건의 실제 교정 증명이 아니다. 원64 pending/lifecycle 입력 부족·나머지50 원인/runtime OPEN. Main 최종 freeze check22항목 mismatch0/VFX unexpected0 확인 인계, freeze 이후 source 변경 없음. fresh Unit Action15:47:57은 Human tower 별preset의 Tank prefab 공유를 거부한 VFX connection fixture로 FAIL; Assault CLR 성공과 별도다. fresh ROOT15:52:38 실제 PASS(Main UI/Editor.log 확인), Unit Action FAIL·사용자 선택 대기·빌드 미시도. VFX44는 Map＋기존fixture host2변경/다른42불변의 programmer 보고이며 보호6＋Assault clip 불변.


> **과거 단계 기록 (2026-10-10, 21_18 phase1/2 당시; 현재 상태는 위 freeze/native 요약):** Client Expired64 FAIL은 미해결이다. 실제 numeric512를 production BridgeCoordinator에 입력한 제한 비교에서 canonical clock만 바꾼 counterfactual은14 Ready/50 Expired였다. 원 ticket 이전 입력이 없어 pending/schedule은 synthetic이며 원64 재현 또는 실기 해결 PASS가 아니다. 최소5파일 교정안은 Plan 검토 대상이며 아직 이 문서 단계의 소스 구현/새 native/build 결과는 없다. 아래 기존 PASS는 당시 자동 gate 이력으로 보존한다.

돌격소총병의 타격 기준을 사용자가 지정한 애니메이션 창의 `0:05`로 통일한다. 30fps에서 5프레임은 약 0.1666667초다. 정상 생성 경로는 이미 클립 이벤트를 읽지만, 기본 설정과 스탯문서가 달라 유지보수와 테스트 해석에 혼선을 준다. 이번에는 그 불일치를 고치고 실제 서버 피해와 화면 표식을 같은 공격 회차로 확인할 로그를 함께 준비한다.

## 승인과 범위

- 사용자: 새 task 작성, 관련 문서 수정, 구현, 두 필수 검증 후 빌드 시도 승인.
- 전문 에이전트 두 명이 사용량 제한으로 중단. 사용자가 이번 작업의 직접 수행 예외를 승인했다.
- TC/QA 별도 문서·에이전트는 요청되지 않았으므로 진행하지 않는다.
- 빌드는 시도만 한다. 완료·설치·실기 검증은 사용자 담당이다.

## 확인한 원본

- `Assault_Attack.anim`: 30fps, 단일 `OnAttackHit=0.16666667`, 길이 `0.33333334`초(0:10).
- `UnitStatsConfig.asset` type 1: 타격 기본값 0.2초, 주기 기본값 0.2초.
- `UnitFactory`: 서버/싱글·Client 생성 모두 동일 선택 클립의 길이로 일반 유닛 주기를 덮어쓰고 이벤트로 타격 시간을 덮어쓴다. Fox만 별도 정책이다. 따라서 설정 0.2초를 실제 Assault 피해 시각으로 단정할 수 없다.
- `Game.unity` UnitFactory의 Human type 1은 Blue/Red 생산 프리팹이 연결돼 있지만 명시 `attackTimelineClip`은 없다. controller 목록의 첫 Attack 이름에 의존하지 않도록 실제 클립을 명시한다.
- `StatsReference.md`: 타격 `0:17`, 주기 `0:33` 이력. 타격은 승인값으로 교정한다. `0:33`은 표준 30fps 초:프레임의 프레임 범위를 벗어나며 의미 미확정이다. 0.33초 또는 1.1초로 임의 해석하지 않는다.
- Assault는 기존 Supported/Hitscan/단일 타격이며 이번에 분류를 승격·변경하지 않는다.

## 로그 준비의 공백과 해결 범위

기존 `[PIST-TIME]` 생산 진단은 Pistoleer만 수집한다. 이미 있는 서버 Schedule/Dispatch/Writer/Result, Host·Client Command/Marker/VFX/Receive/Decision hook을 재사용해 Assault도 수집하되 `type=1`을 명시한다. 기존 결과 키와 서버 시간축을 유지하며 새 피해·회전·애니메이션 writer를 만들지 않는다. Factory 생성 때는 별도 `[ASSAULT-TIMELINE]` 기록으로 선택 클립, 프레임률, 기본/최종 타격·주기와 생성 경로를 확인한다.

정상 취소·Miss·source 종료와 실제 누락, clock 이상, formatIncomplete·overflow·증거 부족을 구분한다. 기존 세부 표본은 팀별 32회차의 공유 quota이며 중요 사건은 quota 밖에서도 제한된 active/tombstone 저장 범위 안에서 기록된다. 혼합 타입 집계는 Assault 단독 집계가 아니다. peer의 서로 다른 로컬 시각을 직접 빼거나 commit+offset이 항상 실제 다음 occurrence라고 가정하지 않는다.

## 상태

구현·문서 정합화 완료. 새 Unit Action 메뉴 11:09:06 및 Root Pose Cross Audit Self Validate 11:10:24 PASS, Console errors0/warnings27. 분석기 production formatter fixture29종 모두 통과, 문서 검사7종0건. 상세 변경 목록·씬 한 줄 범위 증거·빌드 시도는 Plan의 실제 확인 결과에 기록한다. 과거 Pistoleer PASS나 다른 경기 결과를 이번 Assault 실기 완료 근거로 사용하지 않는다. Assault 사용자 실기는 OPEN이며 TorrentSpirit·BloomFairy도 이번 빌드에서 함께 테스트할 대상이다.

Build And Run 한 번 시도 후 JDK 검사 화면을 관찰하고 감시 중단했다. 빌드 완료/설치/실기 결과 미확인. 진단 스킬의 증거 분리 원칙에 따라 기본 설정 교정과 실제 경기 효과를 구분했고, 컴퓨터 사용 스킬에 따라 필요한 검증·빌드 메뉴만 실행했다.

## 최신 역할교대 실기 분석 (2026-10-08, 종합 근거 반영 완료)

근거는 `_Logs/_editor/2026-10-08/RuntimeLog.txt`와 `_Logs/2026-10-08/11_54_logcat/RuntimeLog_device.txt`에 대한 메인 확정 분석 인계다. Editor 파일에 포함된 01시 이전 Pistoleer 경기는 제외하고 아래 두 shared session을 독립 집계했다. 이번 기록은 문서 반영이며 코드·씬·에셋·Unity·Git·새 빌드 작업은 없다.

| 항목 | 1차 Android Host / Editor Client | 2차 Editor Host / Android Client |
|---|---|---|
| shared session | `50c16c6b03d36d652a046c7f82731cb70df9f8fcb73e3cf8756689d1773c68a2` | `d9f49c5c8f85df82d3dd052b70224be068c81d5a68692655027003845d1e9e5d` |
| Assault type 1 scope / peer | 2262、양 peer 동일 paired scope | 280、양 peer 동일 paired scope |
| Result / Receive 및 canonical pair | 2253 / 2253、pair 2253 | 262 / 262、pair 262 |
| peer-local VfxStarted | Host 2244 / Client 1590 | Host 251 / Client 173 |
| Client source incomplete | 644 관측 레코드 | 74 관측 레코드 |
| bound PhaseClockRegression | 38 events、역행 0.165227~0.286781초 | 11 events、역행 0.172152~0.356969초 |
| parser clock errors | 39 = bound 38 + unscoped 1 | 12 = bound 11 + unscoped 1 |

**설정 정합은 확인됐다.** 두 경기 전체 Factory timeline 관측은 Host 88 + Client 88 = 176건이며 hit `0.1666666716`, 선택 clip-derived cycle `0.3333333433`, fallback cycle `0.2`가 확인됐다. 88/88은 경기마다 반복되는 수가 아니라 두 경기 합계다. 승인 타격 `0:05@30fps=5/30초`와 일치한다. 기존 `0:33` 주기의 의미는 여전히 미확정이며 이번 관측으로 밸런스 주기를 새로 결정하지 않는다. multi-loop marker elapsed 2~7초는 회차/루프 진행 지표로, 타격 offset의 실측 증거가 아니다.

**피해 전달과 source 표현은 별도 판정이다.** 결과 pairing은 revision/role alias를 상관 키에서 제외해 동일 정규 결과를 연결한 집계다. canonical pair 일치만으로 native marker/VFX 전수 또는 정확한 픽셀 타이밍을 PASS 처리하지 않는다. source incomplete 644/74는 분석기의 관측 누락 레코드이며 정확한 게임플레이 실패 건수와 동치가 아니다.

Client bound `NativeOccurrenceRejected` 사유는 1차 `wrong-absolute-occurrence=578`, `unbound=29`, `duplicate=45`; 2차 `wrong-absolute-occurrence=62`가 확인됐다. source 누락을 해석할 때 이 사유들과 late/skipped를 구분한다. 코드 근거상 UnitView `OnAttackHit`의 occurrence gate 거부는 VFX 호출 전에 return하며, `AttackSourceOccurrenceLease.TryConsume`은 loop와 marker float의 정확 일치를 요구하고 불일치 시 wrong-absolute로 거부한다. 따라서 피해 결과 수신과 source VFX 차단이 동시에 관측될 수 있다.

직접 사례는 2차 device 행 346424의 PhaseBind(instance 2 / sequence 12 / skipped 0) → 346499 wrong-absolute(loop 11) → 346615 HitApplied Receive → 346806 Ready Decision이다. 이는 해당 source gate의 차단과 피해 결과 전달을 구분하는 근거이지 전체 누락 원인을 확정하는 증거가 아니다.

`FollowAttackPhase`는 raw clock이 high-water보다 낮으면 `animator.speed=0`으로 보류한다. 두 경기에서 bound clock 역행은 관측됐지만, **clock 하나가 모든 source 누락을 일으켰다는 인과는 미확정**이다. occurrence identity/loop·marker mismatch와 보류/재개·late/skipped의 exact scope 연결을 별도로 확인해야 한다. parser의 unscoped 1건을 추가 bound 사건처럼 집계하지 않는다.

### 경기 전체 결과와 다른 타입의 독립 경계

1차 생산은 Assault26·BearGuard10·BloomFairy6·Torrent0이다. 경기 전체 C2 Host 결과 2325 = Client 수신 2325, reject0으로 서버 피해 결과 전달은 정합이다. Assault 전용 canonical2253과 전체2325를 혼동하지 않는다. C3 Host failure0이나 Client는 Expired7, ready2318/2325이며 필수 표현2276/2276은 **ready 부분집합만**의 성공이다. 따라서 1차 서버 피해 전달 정합과 **Client 표현 FAIL/OPEN**을 분리한다.

1차 Editor 행38530의 Expired7은 모두 `publishAfterLimit=False`, `receiveAfterLimitEstimate=False`, `decisionAfterLimit=True`다. receive→decision의 같은 local clock 지연은 448.512~1168.508ms, 최근 frame gap 최대751.367ms다. 이는 전송 소실이 아니라 수신 후 결정 지연·만료 근거다. clock/frame lag의 각 사건 기여와 모든 만료의 최종 인과는 확정하지 않는다.

2차 생산은 Assault62·TorrentSpirit13·Bloom0이다. 경기 전체 C2 262/262·reject0, C3 failure0으로 **그 C3 범위는 PASS**이나 전체 실기 PASS는 아니다. Torrent는 별도 파이프라인이므로 그 누락을 C3 failure0으로 덮지 않는다.

Torrent epoch `41751d14a7a1413d`: 서버 spawn224 = Client receive224, VFX199다. spawn 거부24(clock4 / payload-or-time20)와 정상 game-end expire1을 구분한다. contact publish216 = receive216, text198이며 거부18(clock1 / payload-or-time17)이다. device355005 clock regression과355164 wave5 clock reject가 확인됐다. `payload-or-time`의 정확한 조건 분기는 미확정이며 payload 손상이나 특정 time 조건으로 단정하지 않는다. source/VFX와 contact/text 누락은 별도 OPEN이다.

1차 Bloom은 Host Start/Play45·CastDispatch/Marker43, Client Play45·Marker43이며 positive HoT 첫 기록26건으로 runtime 경로가 실행됐음을 확인했다. 이것은 힐러26기라는 뜻이 아니며 exact-key 타격/회복 시각·Client 표현 전수·사용자 visual 수용은 미확정이다.

네 peer-run MOVE terminal은 모두 EVIDENCE, adapter/handoff/spatial fatal/drop0이며 Host frames는 1차14261 / 2차69482다. 네 ROOT도 local PASS(errors0, complete=True)다. 이는 로컬 이동·pose gate 범위의 성공이지 모든 공격/힐/파도 타이밍 PASS가 아니다. formal Root CrossAudit Analyze는 실행되지 않았다.

**최종 판정: Assault 설정 일치 수용 / source 표현·clock·정확 타이밍·전체 OPEN.** 사용자 visual 수용은 아직 없다. 1차 피해 전달 정합과 Client C3 표현 실패, 2차 C3 성공 부분과 별도 Torrent 누락을 분리한다. source incomplete644/74는 관측 레코드이지 정확한 실패 횟수가 아니다. 기존 자동 메뉴 PASS와 빌드 시도는 과거 준비 근거로 보존하며 이번 요청에서는 코드·씬·에셋·Unity·Git·빌드 작업을 하지 않았다.

## 승인된 다음 단계 — 원인 분리용 로그 보완 계획 (2026-10-08)

사용자의 최종 “그렇게 진행해”에 따라 **기존 Task 갱신 → 로그만 구현 → 실제 새 Unity 두 메뉴 PASS 확인 → Build And Run 시작 시도만**의 순서가 승인됐다. programmer 구현 착수와 아래 구체 범위의 중간 인계를 받았다. **구현 진행 중이며 최종 완료·새 메뉴 PASS·빌드 시도는 미확인**이다. 이전 분석과 미확정 원인은 보존한다. 아래는 실제 구현 중 관측 인계에 대응시킨 계획이고, 최종 diff/필드 검증 완료 목록이 아니다.

### 구체 파일·필드 범위 — 구현 중 인계

| 기존 파일 | 진단 전용 보완 범위 |
|---|---|
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` | 개발빌드 조건부 읽기 전용 lease owner/hit accessor. 게임 Bind/TryConsume 원본 조건은 유지 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | bind/consume/retire/preserve 경계의 NativeLease/Hit 관측. lease/hit의 expected loop·marker·time·consumed와 actual callback, Bind 전후 및 replace/skip 이유 연결 |
| `Assets/_Project/Scripts/Application/Combat/PistoleerTimingDiagnostics.cs` | 기존 PIST-TIME 단계 확장, 관측 전용 `phase-observation` domain과 명시적 bounded 수집 |
| `Tools/analyze_pistoleer_timing.py` | 새 단계 필수값을 기존 parser에 반영. 관측 domain·필수 identity 누락/unknown과 기존 결과 pairing을 구분 |
| `Assets/_Project/Scripts/Application/Combat/WavePresentationCoordinator.cs` | 기존 exact predicate는 유지하고 `firstFailed`·`invalidMask`·limits·raw clock detail로 실제 거부 조건 분리 |
| `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs` | 기존 A/B 관측 유지, C의 `receiveAnchorAdvanceEstimate`·`anchoredJumpEstimate`·`bridgeToDecisionLocal` 추가로 clock 추정과 local 경과 분리 |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | 기존 fixture 보강. 새 검증기/TC/QA 없이 실제 formatter/parser의 새 단계·필수값·경계 검증 |

기존 7파일 범위이며 UnitFactory·Stats·씬·에셋·게임 로직은 이번 후속 대상이 아니다. 메인의 요구는 **최초 사유/상태 전이 중심의 명시적 cap**이다. cap 숫자·overflow/억제 표시·최종 필드와 source diff는 최종 인계에서 확인하며 임의 수치를 만들지 않는다. `phase-observation`은 관측 domain이고 게임 phase/clock writer 변경을 뜻하지 않는다. C의 estimate 필드는 원인 확정이나 네트워크 지연 실측으로 해석하지 않고 같은 peer의 local bridge와 분리한다.

### 가설별 예측과 필요한 관측

| 진단 축 / 미확정 가설 | 가설이 맞을 때의 예측 | 같은 사건에서 수집할 값과 반증 경계 |
|---|---|---|
| 공격 occurrence 불일치: lease가 예상한 loop/marker와 실제 native callback이 다르거나 replace/skip 때 구 lease가 밀림 | 거부 직전의 유효 lease 예상값과 callback 실제값이 다름. replace/skip 전이가 해당 scope 또는 native epoch의 차이를 설명함 | instance·type·sequence·HitIndex·revision/native epoch, lease 생성/교체/종료/소비 전후 상태·예상 loop/marker, 실제 clip/hash/loop/marker, replace 대상과 이유·skip 수/이유·gate 결과. 예상/실제가 같으면 다른 거부 이유를 분리하며 로그 부족을 원인 확정으로 바꾸지 않음 |
| 공격 보류/재개 시 clock 변경이 occurrence 연결에 기여 | 같은 scope의 raw 역행·high-water 보류/재개 전후와 lease/실제 occurrence 차이가 연결됨 | 같은 peer의 단조 local 시각·frame, raw server estimate·high-water·presentation 시각/anchor, 보류/재개 이유·native 진행 및 lease 상태. 역행 없는 mismatch와 역행 있어도 정상 소비된 사례를 함께 비교; clock을 전체644/74 누락의 단독 원인으로 단정하지 않음 |
| Torrent payload-or-time 거부: 여러 조건 중 특정 조건이 실제로 실패 | 실제 최종 거부 지점에서 실패 predicate가 분리되고 해당 입력값이 실패 조건을 재현함 | wave epoch/ID·contact 정규 키, spawn/contact 경로, normal/snapshot 등 실제 입력 경로, 각 검증 조건의 통과/실패·선택된 최종 이유와 비교 입력/기준값, local/raw/high-water/기준 time·age/due/expiry. payload/time/clock·정상 game-end expire를 분리. 요약 문자열만으로 payload 손상이나 정확한 분기를 단정하지 않음 |
| C3 만료: local main-loop stall이 수신 후 결정 지연을 만듦 | 같은 local 단조 clock의 receive→decision 지연 및 frame gap이 커지는 동안 처리 기회가 지연됨 | 같은 정규 결과 키의 receive/decision local 시각·frame, 최근 frame gap·진입/처리 시점, 당시 ready/pending 상태와 deadline/age 판정 입력. stall은 관측 근거이지 FPS 전체/외부 원인의 확정이 아님 |
| C3 만료: clock estimate 변화가 deadline 판정에 기여 | local 경과와 별도로 receive→decision 사이 estimate/표현 시각 또는 deadline 비교값이 변함 | receive/decision 각 local·raw server estimate·presentation clock·high-water와 deadline/age, clock 변경 사건 identity/전후값. local stall만·clock만·둘 다·둘 다 불명으로 분류; 서로 다른 peer의 local clock을 직접 빼지 않음 |

### 유지할 계약과 증거 경계

규칙 인덱스 `GameSystemRules.md`가 가리키는 Units 원본 규칙17~22·29 및 `U-ATK-TIMELINE`, 동기화 원본의 `NET-AUTH-001/002`, `NET-TIME-001~005`, `NET-PRESENT-001~004`, `NET-CANCEL-003~005`를 따른다. 서버 피해·HP 단일 writer, continuous native 진행·lease 소비/거부·예정 발사와 취소·결과 표현의 현재 결정을 **관측만** 한다. 로그 때문에 판정 순서·조건·허용 오차·clock 보정·재시도·애니메이션 속도·lease 교체를 바꾸지 않는다. Torrent 예외를 다른 타입에 확대하지 않는다.

LogRules의 심각도/존속 두 축, 기존 GameLog·파일 sink·구조화 값·Development 경계 및 상태 전이 중심 수집을 유지한다. 매 frame 출력 대신 전이/최종 거부/수신·결정 사건에 필요한 기존 값을 캡처한다. 정상 취소/중복과 실제 실패를 구분하고, sampling quota·active/tombstone·overflow·UTF-8 formatIncomplete 경계를 명시한다. 기존 formatter→parser fixture로 실제 생성 형식과 사유 분리·clock domain·누락/unknown 판정을 검증하되 새 검증기/TC/QA를 만들지 않는다. Stats·설정·씬·클립·VFX·게임 로직은 변경하지 않는다.

이번 로그 보완은 원인 분리를 위한 준비이며 기존 표현 FAIL/OPEN이나 전체 OPEN을 닫지 않는다. 기존 메뉴 PASS는 이 후속 source의 PASS가 아니다. 빌드 시작 관측 이후 완료·설치·runtime 감시는 금지하며 성공을 추정하지 않는다.

### 후속 구현·실제 자동 gate 결과 (2026-10-08, 메인 최종 인계)

위 중간 단계의 대기/구현 중 기록은 당시 상태다. 진단 전용 7파일 source freeze와 새 자동 gate 근거를 수신했다. **현재 diagnostic ready / runtime OPEN / gameplay fix 증거 아님**이다. Bind/TryConsume·Torrent exact predicate 및 기존 게임 결정을 바꾸는 교정은 범위가 아니다. 실제 cap 숫자 등 미전달 세부값은 임의 확정하지 않는다.

- **최초 실행 실패 보존:** 12:48:10 실제 Unit Action에서 ActualCLI `lease-Host.txt` fixture 누락으로 FAIL. 저장 generator에 lease 생성 경로가 존재하는 반면 Unity 생성물이 불일치한 상태였다. Unity 로드 generator가 오래됐다는 원인은 의심이며 독립 확정하지 않는다.
- **refresh와 새 검증을 분리:** 메인이 Ctrl+R로 compilation을 요청했고 실제 CompilingScripts를 관찰했다. reload 종료 Console errors0/warnings20 확인 후 메뉴를 새로 실행했다. 요청 자체를 컴파일 성공이나 gate PASS로 처리하지 않는다.
- **새 실제 Unit Action:** 12:52:08 A1/C2/C3/production PASS. 실제 Unity가 생성한 fixture32 모두 ok이며, 메인의 독립 CLI 재실행도 cases32/failed0/exit0이다. 앞선 missing fixture 실패는 새 생성·검증으로 해소됐으며 원래 실패 기록은 남긴다.
- **새 실제 Root SelfValidate:** 12:53:24 PASS. formal runtime CrossAudit Analyze나 새 경기 수용이 아니다.
- **Console:** errors0/warnings27(재컴파일20 + validation7). warning0이나 Android runtime 무경고를 주장하지 않는다.
- **최종 정적 컴파일 verified:** programmer 인계 Runtime errors0/warnings20, Editor0/0, Development0/20, Release0/24이며 모두 exit0. source 동결 이후 추가 편집0이다. 정적 warning 수와 위 실제 Unity Console27은 서로 다른 검증 집계다.
- **진단-only 보존 검증:** baseline8 원문 압축 length/CRC 검증 후 변경7파일 self-diff를 확인했다는 programmer 인계다. Lease Bind/TryConsume/Close/PreserveSourceOnly 본문은 원문 exact 동일, UnitView는 진단 제거 시 기존 코드 동일, Wave는 거부 로그 제외 시 판정·state 순서 동일, observer는 새 C/formatter 제외 시 기존 A/B 포함 동일이다. 메인이 결과와 해당 코드 지점을 review했고 씬 hash도 동일하다. 이는 기존 의미 보존의 source 검증이며 새 경기 인과/visual 또는 gameplay fix PASS는 아니다.
- **보존 근거:** Game scene SHA256 `A5310D1545A0080AFB8D5149EAAC95E0B1D2649318D0F9F973CC206D5BB42966`이 시작과 같다. Stats/Matrix/규칙 내용은 이번 후속에서 수정하지 않았다. 이전 분석/게임플레이·시각 미확정 상태도 유지한다.
- **이번 빌드 시작 시도:** 메인이 File→Build And Run을 1회 실제 클릭했고 직후 Building Player / Backup & unload 화면을 확인했다. 저장/추가 확인창은 없었다. 이 시점부터 모든 Unity/build UI·로그 polling을 중단했다. 완료·성공·설치·실행은 판정할 수 없으며 이전 빌드 시도를 이번 근거로 재사용하지 않는다.

**이번 후속 manifest:** source는 위 구체 표의 기존7파일, 문서/메모리는 이 Task의 Research/Plan, PROJECT_STATUS·ROADMAP·WORK_HISTORY, `.claude/MEMORY.md`, `.claude/agent-memory/game-programmer/logging.md`다. logging 행수 증가 baseline은 필요할 때 검사 도구로만 반영한다. 실제 Unity 생성 `Temp/pistoleer-timing-fixtures/`와 그 분석기 결과는 **검증 생성물**로 별도 분류하며 새 실기 로그/TC/QA/게임 에셋으로 집계하지 않는다.

**Task 감사 포인트:** `01`은 로그 준비 증거(source7·최초실패/refresh·새Unity2gate·실제생성32fixture/독립CLI·이번 빌드 시작 관측)를 각각 확인한다. `02`는 logging hypotheses의 예상 occurrence/replace/skip, Torrent exact predicate, C3 local stall/clock 변경이 새 runtime exact scope에서 구분되는지를 확인한다. 현재01의 준비 근거와 빌드 시도는 확보됐지만 빌드 완료는 아니며, 02의 인과·gameplay fix·visual 수용은 **runtime OPEN**이다.

**최종 read-only 검토에서 확인된 진단 분류 한계:** `NativeLeaseUnavailable`의 invalid context는 정상 provisional/Suppressed scope0에도 `omitted++`를 발생시켜 parser의 global completeness unknown으로 분류될 수 있다. programmer 근거 경로는 NCC3058 Suppress default→policy34 valid 검사→UnitView7706 bind before/after→PIST114 omitted→parser193 unknown이다. 이는 게임플레이/source gate 실패 건수와 동치가 아니다. 다음 분석에서는 정상 provisional/NormalNone, global unknown, 실제 `NativeOccurrenceRejected`를 분리하며 unknown으로 failure 수를 늘리지 않는다. valid exact-scope의 lease snapshot·predicate·clock 원인 수집은 활용할 수 있지만 전수 completeness/전체 PASS를 선언하지 않는다.

이번 자동 PASS는 기존 assertions의 범위이며 이 분류 한계를 해소한 fixture/코드 검증이 아니다. 필요하면 추후 별도 진행할 항목으로 남긴다. 이번 빌드 시도 이후 source 추가 편집·재빌드는 없고 기존7source/문서·실행 준비 범위는 완료하되 전체 runtime OPEN이다. 기존 실패/분석 이력은 삭제하지 않는다.

## 새 경기 분석 — b41f / 최종 채널·source lease 분류 (2026-10-08)

### 최종 source lease 인계 — 이전 owner 손실과 새 owner 회복 분리

**[2026-10-08 최종 인계]** Sagan의 read-only 결과로 전체 분류를 반영했다. 양 peer191scopes/183results, Marker/VFX Host177/Client151이다. **result present이면서 source absent인 Client32 = unconsumed lease의 revision 교체23 + 공격자 사망9**다. Host6은 모두 first-close despawn이다. Client parser missing23, rejected34, result-present/source-absent32는 서로 다른 축/분모이며 동치가 아니다. 사망/despawn 분류를 lease 교체와 합쳐 동일 결함으로 처리하지 않는다.

Client revision 교체23 전부에서 이전 lease가 remaining1/consumed0 상태로 닫힌 뒤 새 owner bind가 있었고, incoming callback loop가 **닫힌 이전 lease의 expected loop와 23/23 일치**했다. 새 owner의 exact-loop gate에서는 WrongAbsolute23으로 거부됐다. 확인된 연쇄는 **이전 미소비 lease close → 새 bind → 이전 callback의 새 owner gate 거부**다. 정상 exact-loop 조건 자체를 완화할 근거가 아니다. WrongAbsolute23 중 **새 owner19는 나중에 정상 Marker/VFX를 기록**했다. 따라서 23개 새 owner의 영구 source 손실로 표현하지 않는다. 확인된 누락 scope는 그보다 앞선 닫힌 이전 owner다.

대표 연쇄(editor 원본): old seq13/expectedLoop12 close66818(remaining1/consumed0) → new seq14/expectedLoop13 bind66827(frame5229, baseline-1, skipped0, presentationTime67.240308, due67.579464) → actual callbackLoop12/동일 marker offset66833 → new seq14 loop13 admitted66851/Marker66852/VFXStarted66854. old seq13은 HitApplied/Ready 수신이 있지만 Marker/VFX가 없다. seq14 대표 rejection은 이 연쇄의 중간이며 seq14의 영구 손실 증거가 아니다. 메인 독립 raw review도 이 close/회복을 확인했다. 동일 native epoch 안에서 owner 교체/이전 callback routing seam을 확인했지만 clock/rate/RPC 상대 기여와 deeper root cause는 미확정이다.

omitted Host474/Client476은 각각 NativeLeaseUnscoped count와 정확히 같다. 정상 provisional에서도 global completeness unknown이 될 수 있으며 gameplay/source gate 실패 수가 아니다. invalid-lease unknown emission16은 양 peer 각각 bounded 출력이다. scope0 unbound(양쪽11), valid-scope WrongAbsolute(Client23/Host0), 위 result-present source 누락32/6 및 normal provisional/NormalNone·global unknown을 별도로 유지한다. unknown으로 실패 수를 늘리지 않고 전수 completeness PASS도 선언하지 않는다. PhaseHold/Resume29 each의 phase-ahead-or-rate를 raw clock regression으로 재분류하지 않는다.

**후속 제안만:** continuous native epoch 안에서 정확한 이전 occurrence 소유권을 보존/라우팅하는 방법을 검토한다. exact loop/key 완화·free VFX·replay 허용·gameplay clock/HP/writer 변경은 금지다. 이번은 분석/문서 반영만이며 Sagan source/doc edits0, 이 문서 담당 코드/빌드0이다. 최종 분류 기록은 완료했으나 **전체 source-expression defect OPEN, exact timing/pixel 및 사용자 visual 수용 OPEN**이다. 새 C3/Torrent 채널 성공과 이전 만료/거부의 무재발은 수정 증거가 아니다.

**추가 메인 raw 분류 확인:** 새 b41f Client rejected34 = valid-scope WrongAbsolute23 + unbound scope0 11, Host rejected11은 unbound scope0만이며 WrongAbsolute0이다. scope0 unbound를 실제23건의 source gate 차단에 더하지 않는다. invalid-lease unknown16 emitted는 양 peer 각각 bounded 관측이며 errors로 집계하지 않는다. PhaseHold/Resume29는 양 peer 각각 reason=phase-ahead-or-rate이며 raw clock regression으로 재명명하지 않는다. 이번 C3failures0이라 새 Expiry 원인정보는 확보되지 않았다. 전체 source-expression defect·timing/pixel OPEN이다.

메인 source 경계 확인: NCC1404의 예정 loop는 `floor((authoritativeDue-phaseEpoch)/cooldown)`으로 만들어지고 policy358은 exact nativeLoop 일치를 요구한다. marker 설정은 맞지만 논리 시축과 native event 반복번호 연결의 정합 문제가 관측됐다. 최종 lease 연쇄 분류는 완료했으며 clock/rate/RPC의 deeper 기여는 미확정이다. lease 조건을 완화하지 않는다.

메인 확정 분석 인계를 기록한다. session `b41f696fd6471f3a26880e64b67ad84ce259b49f494cb8bd9c203acc9ede6a11`은 Android Host / Editor Client, Torrent wave epoch은 `4685cdc632424817`이다. 원본은 `Assets/_Project/Docs/_Logs/2026-10-08/13_25_logcat/RuntimeLog_device.txt` 및 `Assets/_Project/Docs/_Logs/_editor/2026-10-08/RuntimeLog.txt`다. device에 embedded old 캡처가 있으므로 새 session/key만 분리한다. 혼합 파일 parser aggregate/global errors 숫자는 새 경기 수치로 인용하지 않는다. 아래는 새 로그 근거이며 앞선 두 경기나 빌드 시작 관측을 소급 변경하지 않는다.

### 생산·권위 결과와 공통 표현 채널

- 신규 Client SpawnUnitClientRpc: type1 Assault41, type18 Torrent9, Bloom0. Bloom은 이번 경기에서 미검증이며 앞선 Bloom 실행 근거와 합치지 않는다.
- C2 Host results183/resultFailures0, Client accepted183/reject0. 메인의 독립 canonical Result/Receive exact join은 keyed instance/seq/hit 및 payload revision/victim kind/effect/ordinal/outcome/amount/HP/impact에서 missing0/extra0/conflict0이다. C3 양쪽 ready/released183, expectedVisual/emits183, failure0/timeout0/spatial0이다. 이 채널의 관측 범위 수용이며 native source 표현의 성공을 대신하지 않는다.
- ROOT 양쪽 local PASS, stable endpoints Host12/Client11. MOVE Host adapter0/Fatal0. local observer 결과를 formal CrossAudit나 공격·visual 전체 PASS로 확대하지 않는다.
- C2/C3/ROOT raw anchor: device399502~399579; MOVE core399171/spatial399192. 이번 경기의 C3 만료 무재발은 이전 수신 후 Expired7의 수정 효과/원인 확정이 아니다.

### Torrent exact-set 대조 및 정상 zero-heal/종료 취소

- serverSpawn72 unique = ClientReceive72. 양쪽 VFX unique71은 동일 wave ID 집합이며 gap은 양쪽 모두 wave72 하나다. wave72는 game-end expiry(age Host0.000430s/Client0.030396s, channelFailed=False)로 정상 종료 취소다. 실제 미수신이나 거부로 집계하지 않는다.
- publish104 unique contacts = ClientReceive104 = 양쪽 text104. `(wave, impact, target, kind)` exact-key 집합 대조 missing/extra0이다. spawn/contact/clock reject0, faults/overflow0, channel failure0. 이는 이번 관측 채널 근거이며 이전 clock/payload-or-time 거부의 교정 증거는 아니다.
- 서버 writer233 = damagechanged73 + healchanged31 + zero-heal129. HP 변경104만 publish됐고 nonpositive-delta129는 정상 publish-skipped다. zero129 전부 `effect=heal`, `delta=0`, `before==after==maxHP`, contradictions0으로 메인이 재계산 확인했다. 129errors로 해석하지 않는다. full-HP raw 예시 device355295는 **신규 epoch4685cdc632424817**의 13:24:03.258 `[WAVE-DIAG] scope=server role=server wave=2 stage=writer-applied now=76.611002129875118 target=6 effect=heal before=1000 after=1000 maxHp=1000 delta=0 zeroReason=fullHP`이며 이번129건의 유효 anchor다. 초도 draft의 embedded old 분류는 잘못됐으며 메인 원문 확인으로 정정했다.
- raw anchor: editor75753~75762 summaries,75696 wave72 expiry,75715~75781 wave END; device398873 expiry. 집합 일치가 particle pixel·정확 contact-to-VFX timing·전체 migration 수용을 뜻하지 않는다.

### Assault native source 관측과 최종 수용 경계

새 key 필터 parser records382(Host191/Client191), source-stage Marker/VFX Host177/Client151. Client missing23은 parser 관측 레코드이며 result-present source-absent32와 다른 집계다. terminal rejected34=이전 callback의 valid-scope WrongAbsolute23+scope0 unbound11로 최종 분류했다. normal provisional/NormalNone·global unknown·실제 NativeOccurrenceRejected를 섞거나 unknown을 실패에 더하지 않는다.

메인의 representative raw lease review: editor66823~66836, Client instance1/seq14/revision15, valid bound owner1/14, remaining1/generation1, 동일 epoch4634074462552745574. expectedLoop13인데 callbackLoop12이며 marker는 양쪽0.1666666716으로 같다. bind와 callback은 같은 frame5229, WrongAbsolute/noVfx1이다. 이는 해당 scope의 **실제 loop ID 불일치** 근거이며 승인0:05 설정 오류가 아니다. C2 server result/Client Normal183 및 C3 채널 성공이 이 source gate 차단을 상쇄하지 않는다. 이 사례 하나로 rejected34 또는 missing23 전체 원인을 확정하지 않는다.

후속 제안은 continuous native epoch의 정확한 이전 occurrence ownership 보존/라우팅 검토이며 **proposal only**다. exact-key/loop 완화·free VFX·replay 허용·gameplay clock/HP/writer 변경은 제안/승인 범위가 아니다. 이번 분석에는 구현·Unity 실행·빌드가 없다. lease 분류 기록은 완료했으며 source defect·deeper 기여·exact timing/visual 사용자 수용 및 전체 runtime은 OPEN이다. 감사01 준비 증거는 보존하고 감사02의 확정 채널과 source 원인/unknown은 별도 축으로 판정한다.

## 승인된 source occurrence 소유권 교정 — 접근 계획 (2026-10-08)

사용자가 기존 Task 갱신→코드 교정→새 실제 Unity 두 메뉴 PASS 확인→Build And Run 시작 시도만의 순서를 명시 승인했다. 위 proposal-only는 이전 분석 단계의 상태다. **최종 구현5파일 freeze·새 자동 gate·35fixture·이번 빌드 시작 시도 인계 완료 / runtime source·timing·pixel OPEN**이다. 아래 접근 원칙은 유지하며 실제 결과·증거 한계는 이 절의 최종 인계에 기록한다. 계획 당시 pending 배치 표기는 실제 구현에 맞춰 정합화했다.

### 최종 source manifest — 기존 5파일

| 파일 | 최종 구현 범위 |
|---|---|
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` | 순수 자격/matching/retire helper CanRetain/Matches/RetireBefore additive. semantic cancel과 exact epoch/lifetime/clip/state/loop/marker 조건 유지. pending 목록의 물리적 소유자는 UnitView다. |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | pending old owner 목록 최대8/unit·owner별64hits 및 native context. Apply/Start/Retire/Bind/nativecallback→emitter 연결, 불변 old target 위치/rotation/snapshot·같은 Transform identity·동일 attacker marker muzzle 유지. 9번째 overflow는 old permission close/fail-closed·silent eviction0. |
| `Assets/_Project/Scripts/Application/Combat/PistoleerTimingDiagnostics.cs` | handoff/routed-consume/cancel/occurrence-passed/overflow 전이를 bounded 관측. |
| `Tools/analyze_pistoleer_timing.py` | exact old key result→marker/VFX 연결 및 new owner recovery 별도 판정, 정상 scope0/unknown 분리. |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | 기존 production 실제 handoff RED→GREEN·실제 emitter context, duplicate/lifetime/clip/target/cancel/SourceOnly/multihit/8vs9 capacity 경계 보강. |

메인 review는 **취소되지 않은 정상 handoff의 pending 보존과 latest cursor 분리에는 충돌을 식별하지 않았다.** 실제 semantic cancel로 closed된 scope의 revive는 금지다. clock/TTL/FIFO 완화는 없으며 새 별도 source/검증기/TC/QA 추가는 계획하지 않는다. pending 수명은 기존 의미의 consume/occurrence-passed/cancel 종료로 제한하고 임의 시간 예산을 만들지 않는다.

### 계약 해석과 적용 경계

NET-PRESENT-002의 새 커서 발행/오래된 시작 무시는 latest 상태를 단조 갱신하고 Animator를 되돌리지 않는 계약이다. 취소되지 않은 동일 연속 epoch의 **이미 승인된 정확한 이전 occurrence**를 pending 소유권으로 보존하는 것은 오래된 시작 스냅샷 재수락이나 새 회차로 소급 결합하는 것과 구분한다. NET-ACTION-IDEMPOTENT·NET-TIME-004/005·NET-PRESENT-001/003 및 NET-CANCEL 계약의 최대1회·baseline·수명·미래허가 종료를 모두 유지하는 범위에서는 소유권 구현 교정으로 접근하며, 규칙 변경/Supported 승격으로 해석하지 않는다.

보존 자격은 정상 연속 재생의 동일 attacker instance/native epoch/lifetime/clip/target에서 새 revision/owner가 교체될 때의 미소비 exact lease뿐이다. 실제 target switch/death/Stop/despawn·게임 종료 등 기존 취소와 정상 consume/occurrence-passed는 별개다. 취소된 lease 복원, 새 target에 구회차 이전, 지난 marker 재생, 다른 epoch/lifetime의 callback 소비는 금지한다. 기존 SourceOnly의 타입별 허용·금지 역시 확대하지 않는다. 세부 구현이 이 구분 없이 취소를 revision 교체와 합치거나 pending 자체를 새 공격 허가로 사용한다면 NET-PRESENT-003/NET-CANCEL과 충돌하므로 **메인에 알린 뒤 해당 변경을 멈추고 규칙을 무단 편집하지 않는다.**

실제 callback의 epoch/lifetime/clip/loop/marker를 pending owner와 exact match하고, 그 owner를 먼저 단일 소비한 뒤 **불변 old target/facing/VFX·SFX·tracer context**로 최대 한 번 방출하도록 계획한다. 최신 target이나 최신 회차의 context로 바꾸지 않는다. target/facing 필드의 의미는 승인 당시 source 표현 context이며 권위 Impact 결과를 재계산하는 입력이 아니다. pending와 latest 둘 다 같은 occurrence를 소비하는 중복, free VFX, loop/key tolerance 완화·FIFO/시간 근접 fallback은 금지다. 서버 타격/HP·clock·게임 상태 writer·0:05 설정/Stats/에셋은 불변이다. 공통 source 경로를 교정하더라도 Torrent 예약·Bloom SeparateHeal 예외를 자동 적용하거나 다른 타입의 기존 규칙을 바꾸지 않는다.

### 반증 가능한 검증·수집 계획

1. **소유권 교체가 seam이라는 예측:** 기존 production handoff 연쇄에서 old remaining1/consumed0→정상 same-context new bind→old exact callback을 재현하면 수정 전 old source가 누락되는 RED여야 한다. 교정 뒤에는 old owner만1회 소비/방출하고 new owner가 자기 loop에서 별도1회 표현하는 GREEN이어야 한다. latest 상태/Animator의 되감기·재시작0, 실제 gameplay 결과 동일도 확인한다. 직접 만든 lease 단독 fixture만으로 production chain PASS를 대신하지 않는다.
2. **불변 context 유지 예측:** callback이 새 revision을 보더라도 방출 context는 old owner의 저장 target/facing/VFX·SFX·tracer 값이어야 한다. owner/epoch/instance/lifetime/clip/loop/marker·old/new generation·hit index와 consume 전후를 기존 진단에서 연결하고 replay/중복 callback 두 번째 소비는0이어야 한다.
3. **취소와 정상 교체 분리 예측:** actual target switch/death/Stop/despawn, epoch/clip/lifetime 전환·occurrence passed·baseline 이전·이미 consumed는 pending 방출을 허용하지 않아야 한다. 정상 provisional/NormalNone·unscoped omitted/global unknown을 실제 source 실패와 분리한다. death/despawn 사례를 revision 교체23과 합친 fix/PASS로 주장하지 않는다.
4. **bounded 종료 예측:** pending8owner/unit·64hits/owner의 수용 경계와 9번째 명시 overflow fail-closed를 검증한다. silent eviction 금지, consume/occurrence-passed/cancel 기반 retire로 제한하고 임의 TTL로 게임 규칙을 만들지 않는다. overflow는 identity/사유와 누락·unknown을 명시하고 다른 owner 강제소비/새 gameplay 상태 쓰기로 숨기지 않는다. 최초 사유/전이 중심 bounded 로그와 suppressed/drop/overflow를 함께 판독한다.

**기존 production fixture와 진단/분석기 둘 다 필수:** 기존 RunUnitActionSelfValidation/production formatter fixture를 실제 handoff 연쇄 RED→GREEN 및 위 negative case로 보강하고, 기존 PIST-TIME·parser에 pending preserve/route/consume/retire·owner outcome 분리에 필요한 최소 정보를 연결한다. 새 TC/QA/검증기는 만들지 않는다. 진단은 LogRules의 Development/구조화/전이 중심 경계를 유지하고 closure 전 제거하지 않는다. parser-only green이나 자동 fixture green은 timing/pixel·새 실기 source 전수 수용이 아니다. clock/rate/RPC의 deeper 기여는 계속 미확정이며 이 교정 계획이 clock 문제의 해결을 뜻하지 않는다.

### 최종 교정·실제 gate·빌드 시작 인계 (2026-10-08, 18:03 KST)

**구현/자동 검증 경로 ready, 새 실기 수용 OPEN.** 진단-only7파일/12시32fixture 및 b41f 교정 전 경기와 이번 source교정5파일/18시35fixture는 별도 단계다. 이전 실제 실패·실기 이력은 보존한다. 승인 대상의 premature lease close seam을 교정한 경로가 준비됐지만 새 multiplayer source·timing·particle pixel·사용자 visual acceptance나 full migration PASS는 아니다.

- **최종 동작:** 정상 same-target 연속 handoff에서 old 미소비 exact permission을 UnitView pending 목록에 보존한다. immutable old target position/rotation/snapshot·same Transform identity로 exact scope를 consume-before-emit하고 pending 소비는 latest count를 소비하지 않는다. consumed old cleanup은 semantic Retire를 emit하지 않아 pending Result/Tracer를 취소하지 않는다. occurrence-passed old와 실제 cancel은 retire한다. overflow9는 old permission을 닫고 fail-closed하며 eviction/임의TTL/clock writer/damage 변경은 없다. TargetSwitch/Stop/death/lifetime/epoch/clip/baseline 취소 계약 유지, Torrent 예약/Bloom SeparateHeal 제외다. NativeRoute 진단·parser schema·기존 fixture35를 함께 보강했다.
- **최종 정적 컴파일:** 17:55:10 Runtime errors0/warnings20,17:55:29 Development0/20,17:55:28 Release0/24,17:56:04 Editor0/0. programmer 결과와 메인 review 인계이며 새 multiplayer 실행 증거가 아니다.
- **원문·동결 확인:** self-diff original bytes/hash verify5true, 메인 freeze hash5 모두true. 메인이 FollowPhase/Bind/TryConsume 보존·diff를 review했다. programmer의 에셋 baseline6(Game/Stats/Assault clip/controller/Blue·Red prefabs) hash 모두true, 메인의 재확인 Game SHA256 `A5310D1545A0080AFB8D5149EAAC95E0B1D2649318D0F9F973CC206D5BB42966` 동일. server clock/NCC timer/HP 불변이다. selfdiff stdout의 한국어 mojibake는 출력 표시 문제이며 원본 source bytes/hash 손상으로 오인하지 않는다. Temp selfdiff.py는 agent-generated 기계 검증물로 메인 추가 편집이 없었다.
- **첫 실제 메뉴 실패 보존:** 17:58:24 Unit Action ActualCLI FAIL, route-Host.txt 미생성. disk generator는 route 포함35를 생성하도록 저장됐지만 당시 loaded fixture32와 불일치했다. stale loaded generator/reload 불일치는 의심이며 최종 원인 확정이 아니다.
- **refresh→새 실제 PASS:** 메인 Ctrl+R 요청→실제 fresh Compiling→reload 종료 Console errors0/warnings20→18:01:47 실제 UAS-DIAG/ProductionTimeline/C2/C3 모두PASS. 18:02:42 실제 RootSelfValidate PASS. 최종 Console errors0/warnings27=compile20+validator7이며 warning0이 아니다.
- **RED control의 정확한 의미:** 수정 뒤 동일 production boundary에 기존 full-close를 명시 주입하는 negative control로 old loss를 재현하고 GREEN 경로와 비교했다. **실제 old commit baseline에서 prepatch failing test를 먼저 실행한 엄밀 TDD RED가 아니다.** native Animator/relay handoff로 old/new scope 각각1회, cache999 오염에도 old target902·rotation snapshot 유지, cap8/9·death/target cancel/baseline/exception 경계를 통과했다. manager 부재 fixture의 emitter 호출 지점 근거이며 실제 manager/VFX particle playback·pixel 증거가 아니다.
- **실제 생성 formatter/CLI:** Unity formatter/parser-report35 모두ok, 메인 독립 CLI cases35/failed0/exit0. 최초 missing-route 실패는 새 생성/검증으로 해소됐으며 원래 실패는 남긴다. normal unscoped/unknown의 기존 global completeness 분류 한계를 해소한 전수 runtime 증거로 치환하지 않는다.
- **이번 빌드 시작 시도:** 메인 18:03:22 KST File→Build And Run1회 클릭 직후 Importing(`Settings/UniversalRenderPipelineGlobalSettings.asset`) 전처리 화면 관측. save/confirmation0. 이후 모든 Unity/build UI·로그 polling을 중단했다. completion/success/install/실행 미확인, 이전 빌드 시도 재사용·추가 완료 감시 없음.

**검증 생성물 manifest(실기 로그와 구분):** Temp의 `assault-owner-{runtime,development,release,editor}.{dll,pdb}`8개, `assault-owner-editor.rsp`, `assault-owner-baseline.json`, `assault-owner-selfdiff.py`, `assault-owner-selfdiff.json`의12개 기계 검증물과 `Temp/pistoleer-timing-fixtures/`의35×2txt 및 report를 인계했다. 생성물 명명 stem은 인계 manifest 기준이며 TC/QA/게임 에셋/새 runtime 로그가 아니다. 문서 담당은 이 생성물을 만들거나 변경하지 않았다.

**문서 동기화 manifest:** Task Research/Plan2 + PROJECT_STATUS/ROADMAP/WORK_HISTORY3 + 공용 MEMORY/programmer logging/QA patterns3. 기존 인덱스·실제 경기/실패 이력은 유지하며 baseline은 검사 도구의 증가 갱신만이다. WORKFLOW [9]~[12]에 따라 상시 문서 관련성을 검토했고 GDD/TDD·Stats/Matrix·규칙·에셋의 값/정책은 변경하지 않는다. 새 Task/TC/QA 실행/문서 없음. 다음은 사용자 새 시험이며 source/timing/pixel·clock/rate/RPC 상대 기여·normal scope0/global unknown 한계는 OPEN이다.

## 18_52 새 실기 최종 분석 — C3 Expired4 / source·성능 OPEN (2026-10-08)

이번 사용자 시험에서는 결과 수신과 ready 범위 표현 정합이 확인됐지만 Client C3 만료4가 발생했다. 이는 coordinator의 진단 오류이며 별도 crash/throw 증거는 없다. 이전 18:03 source 교정/자동 gate 준비와 이번 실기 판정은 별개다. 현재 기록은 Sagan 최종 읽기 전용 분석·메인 독립 확인 인계와 제한된 원본 확인을 통합한 최종 분석이며 source fix 완료·전체 runtime PASS·expiry 원인 확정으로 승격하지 않는다.

- session `2cdc5b05b0ecf5bdee466c8344831e6f8a07c8f22e7306cbb7879203ec4be3b5`, Android Client attack run `7f96524bb09345feaba7378a768e064b`, Editor Host attack run `ff50f6e3d864469bb4f4d170310f3628`.
- 원본: `Assets/_Project/Docs/_Logs/2026-10-08/18_52_logcat/RuntimeLog_device.txt` 및 `Assets/_Project/Docs/_Logs/_editor/2026-10-08/RuntimeLog.txt`. 다른 경기·run/counter를 섞지 않는다. 메인 확인의 유일 ERROR header는 device raw568368 coordinator-END다. stack 출력 줄을 별도 오류로 세지 않는다.
- C2 accepted3180/rejected0, transportfail0(메인 확인). Client C3 results3180, ready/released3176, failures4, pending0, duplicates0; expectedVisual/presentationEmits3037 정합은 ready 범위의 성공이지 만료4를 없애는 전체 성공이 아니다. raw568368은 presentationWrites3037/gameplayWrites0/verdictFAIL을 기록한다. 다른 observer aggregate `clientAccepted`와 C2 canonical 분모를 합치지 않는다.
- Client local ROOT는 **raw568445** summary-END에서 PASS/errors0, stable endpoint/rotation events54·logs54·drop0이다. 인계의 raw568435 근처를 bounded 확인해 정확 anchor를 찾았으며 raw568435 자체는 Fox timeline이다. ROOT run `710d969cbc724fd7a0d3a4bb2baeb298`은 attack run과 별도다. local ROOT 성공은 C3·source·formal CrossAudit·timing/pixel 전체 PASS가 아니다.

| Client exact attacker scope (모두 type1 Assault: 메인 유형 확인) | Expired raw | timing A/B/C raw | receiveAfterLimitEstimate | receive→decision local / frameDelta | anchoredJumpEstimate |
|---|---|---|---|---|---|
| unit8 / instance7 / seq8 / hit0 | 43635 | 43619 / 43624 / 43629 | true | 0.047224s / 0 | -0.028627s |
| unit5 / instance6 / seq26 / hit0 | 43664 | 43648 / 43653 / 43658 | true | 0.038449s / 0 | -0.019890s |
| unit8 / instance7 / seq11 / hit0 | 46334 | 46318 / 46323 / 46328 | false | 0.643435s / 19 | +0.560599s |
| unit8 / instance7 / seq45 / hit0 | 76212 | 76196 / 76201 / 76206 | true | 0.139336s / 0 | -0.137903s |

네 건의 A는 모두 arrivalFound=true/overflow0/publishAfterLimit=false/decisionAfterLimit=true다. 3건은 **수신 시 server estimate 기준** limit 초과 관측이며 실제 네트워크 지연/손실/RTT로 확정하지 않는다. seq11은 수신 후 같은 Client local clock의 19frame/0.643435s와 anchor jump estimate +0.560599s를 함께 기록한다. 이것만으로 단일 main-loop stall·clock 보정·RPC 원인을 확정하거나 새 교정 효과를 판정하지 않는다. 서로 다른 peer의 publishLocal/receiveLocal을 빼지 않으며 server estimate를 실제 동일 clock 측정으로 취급하지 않는다.

**최종 Sagan/메인 인계의 추가 확정:**

- 이전 owner pending 정상 consume Host705/Client767, 양측 pending 최대2/overflow0. 메인은 device pendingconsumed767/overflow0을 독립 검증했다. 이번 경기의 bounded pending 라우팅 실행 근거이며 전수 source fix PASS는 아니다.
- result-present marker-absent scope Host358/Client12가 남았다. occurrence-passed를 포함하는 source 누락이며 전부를 이번 교정의 동일 회귀 원인으로 확정하지 않는다. old pending 정상 소모와 남은 누락을 합치거나 normal scope0/global unknown을 실제 실패에 더하지 않는다.
- C2 Host3180/resultFailure0=Client3180/rejected0. C3 Hostready3180/failure0, Clientready3176/Expired4. Host movement adapter/Fatal0, 양peer local ROOT PASS는 해당 로컬 범위 근거이며 formal 멀티·timing/pixel 수용이 아니다.
- 실제 생산 Assault47/BearGuard8/Bloom8, Torrent0. Bloom exact Start/HostPlay/ClientPlay63 정합, missing/extra/target conflict0. 이는 keyed delivery 범위이며 정확 heal timing·particle/pixel·전체 Bloom 수용이 아니다. 이번 경기에는 Torrent runtime coverage가 없다.
- PIST 로그만 Host약486records/s·Client약385records/s로 관측됐다. FPS/CPU/GC 미측정이므로 이 로그 밀도를 렉 원인으로 단정하지 않는다. 별도 crash/throw 증거 없음; expiry의 전송/추정시계/대기 상대 기여는 미확정이다.

**최종 경계/제안-only 후속:** Client C3 표현 gate FAIL/expiry OPEN, 성능·source/timing/pixel 및 전체 runtime OPEN. 최종 분석 인계는 수신 완료이며 더 이상 Sagan 분석 대기 상태가 아니다. 기존 production fixture의 ObserveResult/CompletedBundle→Tick anchor 연쇄 재현 검토와 source occurrence-passed의 별도 분류를 제안한다. expiry 확대·clock clamp·lease 완화의 근거는 없으며 구현 전 task 선택/구체 plan 승인이 필요하다. 이 턴 승인·코드 fix·빌드 없음. 지금은 문서 기록만이며 source/시간·HP 서버 권위/Stats/씬/에셋/규칙 변경0, 새 Task/TC/QA 없음. 사용자 standing 승인에 따라 분석 문서 갱신은 재질문 없이 반영한다.

### Expired4 production 재현 승인 후 준비 (2026-10-08, 실행 전)

이번 단계는 이미 수신한 네 결과가 왜 표현되기 전에 만료됐는지, 실제 생산 경로에 같은 입력을 넣어 구분하기 위한 준비다. 사용자 “ㄷ진행해그럼” 승인으로 기존 Task의 재현 계획을 먼저 기록한다. 위의 승인 없음/제안-only는 최종 분석 당시 이력이며, 현재는 아래 재현 단계가 승인됐다. 원인 확정이나 source 교정 완료를 뜻하지 않는다.

- 대상은 기존 2cdc Client Expired4의 exact key: Assault type1, unit8/instance7/seq8·11·45와 unit5/instance6/seq26, 모두 hit0. C2 3180 전달과 Client C3 3176/Expired4를 유지하고 source occurrence-passed 누락은 별도로 분리한다.
- seq11의 수신 뒤 19frame/0.643435s와 anchoredJumpEstimate +0.560599s는 관측·추정 구분을 유지한다. 나머지 세 건의 receive-after-limit 추정과 달리 seq11은 그 추정이 false다. 이 차이만으로 전송·앵커 교체·로컬 대기 어느 하나를 원인으로 확정하지 않는다.
- 기존 `RunAuthoritativeAttackPresentationValidation.ValidateProductionAdapter`의 실제 초기화와 `Result/CompletedBundle → UnitAttackResultPresentationShadowBridge → TickCoordinator → Coordinator.Decided` 연쇄를 사용한다. 기존 CompletedBundle fixture가 있다는 사실은 이번 로그의 재현 성공 증거가 아니다. coordinator 직접 입력이나 추정 시각만 대입한 대체 모델로 production 원인 확정을 주장하지 않는다.

| 대조 가설 | 고정할 입력 | 한 번에 바꿀 변수 / 관찰 |
|---|---|---|
| 앵커 교체 | exact result·수신·로컬 Tick 간격 | 앵커 교체만 변경; 교체 전후 시간 추정·Decided 상태 변화 |
| 늦은 수신 | 앵커 궤적·수신 후 대기 | 수신 시점만 변경; impact/due/limit 대비 Ready/Expired 경계 |
| 로컬 대기 | result·수신·앵커 궤적 | 수신 후 Tick까지 대기만 변경; 동일 peer frame/시간과 상태 변화 |

각 기준/대조 쌍에서 payload·수명·초기 baseline·입력 순서를 보존하고 변경 변수와 결과를 기계 기록한다. 앵커 종류/revision 및 server/local anchor, result key·snapshot revision·bundle ordinal, authoritative impact/due/limit, receive/Bridge/Tick/Decided 시각·frame·clock domain, 결정 사유와 emitter 횟수를 수집한다. peer 간 local 시각 차이를 RTT나 전송 지연으로 쓰지 않는다. Dev/Release 각각 개별 Result의 조건부 Bridge 입력 유무·CompletedBundle 입력 순서·횟수도 확인하되 입력 차이 자체를 인과 증명으로 취급하지 않는다.

재현은 기존 NET-TIME-001~005 및 NET-PRESENT-003/004를 유지한다. HP 서버 writer·표현 지연·catch-up 한도·순서 역전 버퍼·late-join baseline·exact-key atomic bundle·single emitter와 수명/취소 경계를 완화하지 않는다. expiry 확대, clock clamp, lease 완화는 계획에 없다. 중복/순서 역전/retire/취소/과거 baseline 대조에서 중복 표현과 HP 변경이 없음을 확인할 계획이며, timing/pixel·성능 수용과 별개다.

**준비 manifest:** 이번 실제 변경은 기존 Research.md/Plan.md 두 문서의 append뿐이다. 후속 기계 검증 산출물의 허용 위치는 `Temp/expired-replay/`이며 `Replay.cs`, `trace.json`, `compile.rsp`, 실행 dll/runtimeconfig, `results.json`, `manifest.json`으로 한정한다. dll/runtimeconfig의 정확한 이름·source/reference hash·compile defines·명령·입력 출처·결과/미확정 항목은 생성 시 manifest에 기록한다. 이 준비 단계에서는 생성/컴파일/실행하지 않았다. 재현 결과와 후보 source 파일 manifest를 Main이 검토한 뒤 원인 확정 부분만 교정 대상으로 삼는다. 신규 Task/TC/QA 및 코드/Git/UI/build/assets/Stats/씬/규칙 변경 없음; 원본 로그·이전 실패/검증/빌드 이력 보존. 문서/메모리 최종 인계는 재현 단계와 분리하며 expiry/source/timing/pixel/performance OPEN을 유지한다.

### 수정 전 replay 결과와 최소 교정 후보 (2026-10-08)

실제 표현 시계에 진단용 결과가 개입할 수 있다는 결함을 제한된 재현에서 확인했다. 실기 네 만료의 원인을 모두 찾았거나 해결한 것은 아니다. 앞 절의 Temp 미생성/미실행은 준비 당시 이력으로 보존한다. 후속 Main 인계는 수정 전 Development/Release replay 독립 실행 exit0이며, 이번 문서 단계에서는 재실행이나 source 수정을 하지 않았다.

**증거:** `Temp/expired-replay/manifest.json`과 `results.json`을 확인했다. frozen unmodified production assembly를 CLR에서 로드하여 실제 Bridge→Coordinator/Tick을 실행했다. edition당 19개 case, 두 번 반복 결과 동일; 새 컴파일 errors0/warnings0은 replay harness에 한정되며 production 전체 새 컴파일이나 Unity build 증거가 아니다. 초기 harness는 필수 Impact position 누락으로 invalid bundle/Pending이었고 Temp 입력만 보완했다. 이 초기 입력 실패를 gameplay RED로 취급하지 않는다.

| 수정 전 대조 | Development | Release | 확정 범위 |
|---|---|---|---|
| stable-normal | Ready | Ready | 정상 synthetic 기준 |
| unrelated-result-jump | Expired | Ready | 같은 exact 정상 payload에 별도 diagnostic Result 입력 차이로 결정/시계 변화 |
| stable-late / stable-wait | Expired | Expired | 실제 늦음/대기 대조에서 만료 유지 |
| recorded endpoint4 | 4 Expired | 4 Expired | 기록 endpoint의 단순화 reduction |
| no-other-anchor / immediate-bundle-tick | 3 Expired + seq11 Pending | 3 Expired + seq11 Pending | seq11은 Ready가 아님; 원인 전체 확정/해결 증거 아님 |

source `UnitAttackResultPresentationShadow.cs`의 Bridge `ObserveResult`는 `_clockAnchorServerTime/_clockAnchorLocalTime/_hasClockAnchor/_lastCoordinatorTime`을 갱신한 뒤에야 `!UsesAuthoritativeEmitter`로 coordinator 입력을 제한한다. 따라서 ResultPresentation의 C2 diagnostic 결과가 coordinator에 직접 들어가지 않아도 실제 표현 시계를 바꿀 수 있다. `_clockAnchorSource="result"` 진단도 이 경로에서 갱신된다. 이것은 controlled unrelated-result-jump로 분리한 source 결함이며 실제 네 만료의 전부 원인이라는 결론은 아니다. 별도 PresentationShadow 모드의 기존 앵커 행동은 교정에서 보존해야 한다.

**한계:** whole-capture full replay가 아니다. Unity PlayerLoop/NGO transport/RPC classifier/실제 emitter를 실행하지 않았다. exact key·시간 endpoint 외 position/direction/delivery/schedule 및 마지막 companion bundle은 synthetic 정상 입력이며, 중간 anchor/Tick 누락·반올림된 진단 anchor·역산 tickLocal 한계가 있다. 개별 Result의 Dev/Release 조건부 callsite 차이는 모델링했지만 실제 NetworkUnit RPC를 재실행한 것은 아니다. duplicate/invalid-bundle-jump도 Expired지만 accepted RPC classifier가 선행하므로 실제 accepted network 경로 도달 가능성은 미확정; 이 두 probe의 source 교정은 보류한다. unrelated-bundle-jump는 양 edition Expired이며 bundle 자체 앵커 기여를 없애는 근거로 사용하지 않는다.

**최소 source 후보(아직 수정0):** 기존 Application bridge 파일에서 ResultPresentation `ObserveResult`의 C2 diagnostic 수락/분류는 유지하되 실제 표현 anchor/lastCoordinatorTime을 바꾸지 않도록 분리한다. 완결 Bundle은 정규 표현 시간의 유일한 입력으로 유지한다(경기 초기 anchor 초기화는 유지). 기존 Editor production adapter fixture에 diagnostic on/off의 same clock+same Ready 결정, genuine late Expired의 기존0.5초 한도, 일반 PresentationShadow 앵커 행동 보존을 추가하는 계획이다. 상세 후보2파일/근거/후속 gate는 Plan의 다음 절 참조. 실기 expiry A/B/C의 `clockAnchorSource` 갱신이 result가 아닌 completed-bundle인지 새 capture로 확인해야 한다. late3/seq11의 실제 원인·렉·marker 누락·timing/pixel은 OPEN; clock clamp/timeout 확대/HP writer·Stats·씬·assets 변경 없음. bounded clock history/Perf는 candidate 교정 재현 뒤 필요성을 판단할 후속 후보이며 신규 source GO가 아니다.

### Diagnostic live clock 격리 구현·현재 replay 인계 (2026-10-08, Unity 새 gate 대기)

앞 절의 source 미구현/후보-only는 당시 계획 이력이다. 후속 source2 final patch와 Main 직접 review가 완료됐다. ResultPresentation ObserveResult의 finite-clock assignment만 !UsesAuthoritativeEmitter로 감싸며 기존 Scheduler 필터/Publish·invalid clock 분류, CompletedBundle/TickCoordinator/Coordinator/HP 서버 writer는 보존한다. 일반 PresentationShadow의 기존 anchor 행동도 유지한다. 교정 범위는 Dev diagnostic의 실제 표현 시계 개입이며 실기 Expired4 전체 해결이 아니다.

- source 수정2: Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs (SHA256 C8004D97DB1ECC51A787BA04F9E5677465C0445F4C04AFE0A3A25A24A5792820), Assets/_Project/Scripts/Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs (SHA256 073A863E8D6D6D7FCD32EEC830755F0D06A253B536D663357DA6E840A640E2E8). 이번 문서 담당은 source 수정 없음.
- 기존 ValidateProductionAdapter Required fixture 보강: C2 diagnostic on/off clock/anchor/current time equality·classification Scheduled·exact Ready/Expired 결정·queue VFX hook one/zero·HP 불변·재표현 없음, 정상 completed-bundle anchor/source 및 별도 PresentationShadow result 행동 보존. 이는 Main 코드 review이며 새 Unity fixture 실행 결과는 아직 없다. VFX hook 성공을 particle/pixel PASS로 표시하지 않는다.
- Sagan 인계/fix-results의 Runtime/Editor/Development/Release 정적4경로 errors0, 기존 경고 유지(Editor warnings0). Main 현재 CLR replay Dev/Release 독립 실행 exit0: frozen old Dev unrelated-result-jump Expired → current 양쪽 Ready, time100.101 동일; genuine late/wait Expired 유지·Shadow preserved/classified1. actual Unity prepatch fixture RED는 없으며 frozen 실제 Bridge controlled RED와 현재 CLR GREEN을 구분한다.
- endpoint4는 현재도 Expired이며 seq11 no-other-anchor/immediateTick 대조는 Pending, Ready 아님. whole-capture full replay가 아닌 CLR partial/synthetic 입력·companion/반올림 anchor/역산 tickLocal/중간 Tick 누락이며 Unity PlayerLoop/NGO transport/RPC accepted classifier/실제 emitter 미실행이다. duplicate/invalid direct-Bridge probe의 actual accepted RPC reach와 교정은 보류한다.
- Main CtrlR 후 새 reload 진행 관찰까지 인계됐고 actual 새 Unit Action/Root 메뉴는 아직 미실행/결과 인계 대기다. 이번 build 요청/시도 없음. 이전18:03 Build And Run 시작과 미확인 완료 이력은 별도로 보존한다.
- 기존 expiry A/B/C clockAnchorSource가 result로 갱신되지 않고 completed-bundle만 정규 표현 입력으로 갱신되는지(begin-match 초기화 구분) 새 실기 capture가 필요하다. 실제 late3/seq11 인과·expiry 전송/clock/wait 기여·렉/FPS/CPU/GC·marker 누락/source/timing/pixel/formal 멀티·전체 runtime OPEN. bounded history/Perf 신규 source GO 없음.

**전체 산출물 listing:** 수정 전 Temp/expired-replay/manifest.json의 Replay.cs, trace.json, compile.Development.rsp, compile.Release.rsp, Replay.Development.dll, Replay.Release.dll, Replay.Development.runtimeconfig.json, Replay.Release.runtimeconfig.json, results.json, manifest.json은 보존됐다. 교정 후 fix-manifest.json의 Temp 목록은 current-runtime.dll, current-editor.dll, current-development.dll, current-release.dll, current-editor.rsp, CurrentReplay.cs, current.Development.rsp, current.Release.rsp, CurrentReplay.Development.dll, CurrentReplay.Release.dll, CurrentReplay.Development.runtimeconfig.json, CurrentReplay.Release.runtimeconfig.json, fix-results.json, fix-manifest.json이다. 실행 명령·source/assembly hash·차이/한계는 해당 manifests를 참조한다. 실행/컴파일은 담당 인계이며 문서 담당의 재실행이 아니다.

**문서 관련성/보존:** Task2·상태3·공용 memory·programmer logging·QA patterns만 현재 경계로 최소 동기화하고 사용량 오류 판단 실수는 mistakes4칸 누적한다. GDD/TDD/규칙/Stats/asset matrix는 권위·구조·수치·에셋 정책 불변이라 수정하지 않는다. NET-TIME-001~005·NET-PRESENT-003/004/기존0.5초 유지, clamp/timeout 확대·HP 권위 변경 없음. 원본 로그·기존 이력 보존; 새 Task/TC/QA·Git/UI/build/assets/Stats/씬/규칙 변경 없음. 새 Unity 결과 수신 후 별도 추가 반영한다.

### 23:09–23:10 새 Unity gate 최종 인계 (2026-10-08)

앞 절의 새 메뉴 대기는 당시 이력이며 이번 Main actual UI 인계로 완료됐다. CtrlR 재컴파일의 Library assemblies23:01 기록 후 Unit Action 클릭 시 domain reload 시작/완료를 관찰했다. 새 Console23:09:32 C2/C3 self-validation PASS 및 C3 detail PASS 확인. 현재 RunUnitActionSelfValidation.Run은 RunAuthoritativeAttackPresentationValidation.Run(line911)을 final C3 PASS(line917) 전에 호출하므로 final source hash 불변의 새 Required production fixture도 완료된 것으로 확인한다. Root Self Validate 클릭 후23:10:44 UAS-ROOT-CROSS-AUDIT self-validation PASS를 하단 status에서 확인했다. 이것은 Main UI snapshot 인계이며 문서 담당의 UI 재실행이 아니다.

Console errors0, warnings29→31이며 zero-warning이 아니다.23:09:33·23:10:06 token refresh30초 timeout, 짧은 HoldOn AndroidLogcatRuntimeBase.OnUpdate modal 및 activate_window timeout1회 뒤 fresh-window 복구 성공은 Editor/도구 관측이다. 실제 경기 crash로 동일시하지 않는다. daily runtime tail은18:52뿐이므로 새 저장 runtime log가 있다고 주장하지 않으며 이번 fresh gate 증거는 UI snapshot이다. Main 인계상 Game/Login/Stats hash는 제공된 baseline과 동일, scene save/build/Git/asset 변경 없음; 이번 build 요청/시도 없음.

diagnostic live-clock 개입 결함은 제한된 frozen/current CLR 대조와 이번 actual production gates로 교정 검증됐으나 실제4Expired 인과/해결·perf·source marker 누락·timing/pixel/formal 멀티·전체 runtime은 OPEN이다. source2/Temp manifests는 앞 절 그대로이며 추가 source 변경/새 Task/TC/QA/규칙 변경 없음. 이전 pending/실패/빌드 시도 이력 보존; 더 이상 UI 인계 대기 없음.

### 00_10 새 사용자 실기 확인 핵심 (2026-10-09, 심층 최종 인계 전)

새 경기에서도 Client C3 Expired4가 남았다. diagnostic live-clock 개입 교정과 자동 gate 성공은 유지하지만, 실기 만료까지 해결됐다는 뜻은 아니다. 사용자 standing 분석 기록 승인으로 Main 독립 원본 확인 인계를 기존 Task에 추가하며 문서 담당의 전수 재분석으로 표시하지 않는다. Sagan의 추가 심층 최종 분석은 Main 인계 대기이고 그 결과/미확인 수치는 선행 작성하지 않는다.

**원본/identity:** device `Assets/_Project/Docs/_Logs/2026-10-09/00_10_logcat/RuntimeLog_device.txt` + editor daily `Assets/_Project/Docs/_Logs/_editor/2026-10-08/RuntimeLog.txt`. shared session `027acf2d591342e2179a6ee5dc6472ea6e279310e059cfcd5dadccf7947e7349`, Host attack run `4ba4664c61d94518ad784f8221390453`, Client attack run `0ebde80068f34b1aa38f109fee2308e4`. 이전2cdc/23:10 자동 gate와 별개 경기이며 원본 로그는 보존한다.

- Host C2/C3 1831/failure0, required1779/1779. Client accepted1831/reject0, C3 ready1827/Expired4·required1775/1775는 ready subset만의 성공이다. Client C3 표현 FAIL/전체 runtime OPEN이며 수신 정합으로 만료를 지우지 않는다.
- device unique ERROR raw331365는 coordinator END FAIL. EndSession/OnDespawn stack은 별도 crash 사건이 아니며 Fatal signal/FATAL EXCEPTION은 관측되지 않았다는 Main 확인 범위로 기록한다. crash 절대 부재나 전체 예외0으로 확대하지 않는다.
- 네 scope는 모두 type1 Assault/building0/damage10이며 아래 순서대로 HP860/850/840/830 결과다.

| unit / instance / seq | receive→decision local ms | frameDelta | anchor jump estimate ms |
|---|---:|---:|---:|
| 8 / 11 / 18 | 73.658 | 0 | -99.167 |
| 6 / 8 / 50 | 76.323 | 0 | -74.301 |
| 2 / 3 / 111 | 77.466 | 0 | -75.372 |
| 4 / 6 / 81 | 77.747 | 0 | -75.645 |

네 건 모두 impact115.43199262933805, receive server estimate116.042557984, decision116.045846522·frame23021이다. due115.531993/expiry limit116.031993 대비 receive는 추정상 약10.565ms, decision은 약13.854ms 초과했다. 이 server estimate 차이와 위 same-peer local73~78ms는 서로 다른 clock domain이다. cross-clock 차이를 실제 네트워크 전송 지연이나 RTT로 확정하지 않는다. recent frame gap137.588ms 관측 역시 FPS/CPU/GC·렉 원인을 증명하지 않는다.

**교정 적용 경계:** failure A/B/C raw91486–91583의 anchor450..453→465는 completed-bundle이며 해당 실패 snapshot에는 result anchor가 없다. diagnostic isolation 적용을 지지하는 scoped 증거이지 limited snapshot으로 경기 전체 모든 anchor가 bundle이라고 증명할 수는 없다. jump 추정은 모두 음수라 이전2cdc seq11의 양수 jump와 다르며 동일 원인으로 단정하지 않는다. diagnostic 격리 결함이 교정됐어도 이 네 만료는 미해결이다.

**MOVE/ROOT 범위:** Host MOVE18134frames adapter/Fatal0. 이번 양peer ROOT는 INCOMPLETE/coverageFalse이며 errors/drop0, stable Host33/Client35다. 이전 경기 local ROOT PASS 또는23:10 SelfValidate PASS를 이번 runtime ROOT 판정으로 재사용하지 않는다. formal 멀티/timing/pixel 수용은 없다.

**현재 실행/후속 경계:** 이번은 확인된 핵심 기록만이며 새 정책 제안/구현 없음. expiry 전송·추정시계·수신 처리/대기 상대 기여, 렉/FPS/CPU/GC, source marker 누락·전체 timing/pixel OPEN을 유지한다. 심층 인계 전 source 전수/생산 수/추가 loss 수치를 만들어 넣지 않는다. 기존 Task2·상태3·공용/관련 topic 메모리 최소 반영과 check_docs/증가 baseline만; source/Temp/Git/build/UI/assets/규칙/Stats/Matrix/TC 변경0, 새 Task/QA 없음. GDD/TDD/권위·수치·정책/에셋 계약 불변이라 관련 원본 문서는 미수정이며 기존 교정/실패/자동 gate/빌드 시도 이력 보존.

### 00_10 Sagan 최종 read-only 분석 인계 추가 (2026-10-09)

앞 절의 심층 인계 대기는 당시 이력이며, 동일027acf session에 대한 Sagan 최종 분석을 Main 사용자 인계에 따라 추가한다. 문서 담당의 독립 전수 재분석으로 표시하지 않으며 앞 절의 원본·identity·확인 핵심은 덮어쓰지 않는다.

- Full canonical key는 양peer1831, missing/extra0. 네 exact Client scope(8/11/18, 6/8/50, 2/3/111, 4/6/81)는 모두 LateSourceOccurrenceSkipped 및 Marker/VFX 미관측이며 Host의 같은 scope에는 Marker/VFX가 있다. 이는 canonical delivery, C3 expiry, native source 표현의 별도 증거축이다. expiry 자체가 native gate를 취소했다고 인과를 단정하지 않는다.
- Pending-old 정상 소비는 Host261/Client125, max1/overflow0. result-scope Marker 미관측은 Host70/Client82이며 Client82의 분류는 death14 / late-skip17 / PhaseBind 미관측51이다. missing bind51을 확정 실패 총수로 세지 않는다. source sampling/tombstone eviction으로 전체 source completeness는 확보되지 않았다.
- PhaseClockRegression19는 별도 관측이며 네 expiry 또는 source gap의 원인으로 확정하지 않는다. Host MOVE18134frames adapter/Fatal/drop0, Client replication494/invalid0. 양peer ROOT INCOMPLETE는 Assault-only 경기의 type coverage 미충족이며 이전 PASS를 재사용하지 않는다.
- 실패 관련 anchor snapshot8개는 completed-bundle을 가리키는 scoped 증거일 뿐 APK hash/deploy identity 증명이 아니다. diagnostic clock isolation 교정 검증과 실제 네 expiry·source/timing/pixel·성능 인과 OPEN을 분리한다.

후속은 **제안만**: bounded same-local/frame NGO time → completed-bundle anchor → Tick history로 transport delay와 time estimate 기여를 분리할 근거를 수집하는 후보다. 계측/source 구현 승인이나 timeout 확대/clamp 변경 승인이 아니다. 이번 변경은 기존 Research/Plan 두 파일 최소 append만이며 상시 상태/메모리/baseline·원본 로그·source/Temp/build/Git/UI/에셋/규칙/Stats/Matrix/TC는 미변경이다.

### 00_10 bounded numeric clock history 승인 계획 (2026-10-09, source 미구현)

결과가 늦게 도착한 것인지, 네트워크가 추정한 서버 시간이 달라진 것인지, 표현 시간 앵커가 교체된 것인지, 수신 뒤 처리까지 기다린 것인지를 구분할 수 있도록 제한된 숫자 이력을 모으는 계획이다. 매 프레임 문자열 로그를 늘리지 않고 실패 직전의 시간 흐름을 보존한 뒤 경기 종료 때만 출력한다. 계측으로 원인을 확정하거나 네 실기 만료를 해결했다고 주장하지 않는다.

**승인/실행 경계:** 앞 절의 bounded history “제안만/승인 아님”은 그 당시 이력으로 보존한다. 사용자는 기존 Task 갱신과 진단 구현 방향을 승인했고, 이번 GO는 기존 Research/Plan 두 파일의 계획 append만 허용한다. 아래 source4는 game-programmer 인계의 후보이며 **현재 bounded history source 미구현**이다. Main이 이 계획을 검토한 뒤 별도 source GO를 받아야 source를 시작한다. 상시 상태/메모리/baseline 갱신은 source 완료 뒤 별도 단계이며 이번에는 하지 않는다.

**출발 증거:** 같은027acf…7349 경기의 canonical1831 전달과 Client ready1827/Expired4는 별개다. 네 exact scope(8/11/18,6/8/50,2/3/111,4/6/81)의 수신 server estimate 약10.565ms 초과, same-peer local 대기73–78ms 및 음수 anchor jump 추정은 서로 다른 시간 영역이다. latest 최종 분석은 앞 절에 보존하며 이를 네트워크 전송 지연/RTT·렉·expiry가 marker 누락을 유발했다는 인과로 바꾸지 않는다. 이 새 계측은 이전 capture의 빠진 history를 소급 복원하지 못한다.

**수집/결합 계약:** 숫자 event sequence, Tick ticket, frame, 같은 peer local time, subscriber에서 새로 읽은 fresh NGO server/local time, 원 callsite가 전달한 server/local 값과 각각의 validity를 별도 필드로 보존한다. exact result key(인스턴스·회차·hit 및 해당 결과의 피해자/효과/ordinal), anchor before/after와 종류/revision, Decision status를 연결한다. 기록에 없는 key/time은 invalid/unknown이며 0을 유효 시각처럼 사용하지 않는다. fresh NGO 조회는 원 callsite 값의 대체나 gameplay clock writer가 아니다. 같은 local/frame의 chronology와 추정 서버 시간을 나눠 판독하고 cross-peer local 차이를 RTT로 쓰지 않는다.

**저장/동결/출력 상한:** 숫자 ring1024 records와 frozen128 records×4 windows; record<=256bytes이므로 numeric storage<=1536×256=393216bytes(384KiB), 배열/객체/메타데이터 overhead는 별도다. ClientExpired에서 arm하고 **같은 Tick ticket의 TickExit까지 포함한 뒤 동결**하여 같은 Tick의 네 결정과 끝 경계를 보존한다. 네 frozen slot을 넘는 사건은 saturation/suppression을 명시하며 기존 slot을 조용히 덮어쓰지 않는다. 숫자 기록 중에는 문자열/로그 출력이 없고 문자열화·출력은 EndSession에서만 한다.

한 window 출력 예산은 최대258lines, 네 window와 session summary1을 합쳐 최대1033lines/session이다. 각 line은 메시지뿐 아니라 실제 logger prefix/header를 포함한 **full UTF-8 <1000bytes**, session history 출력은 **<=1033000bytes**의 별도 상한으로 preflight한다. 이 숫자는 승인된 상한이며 구현 실측/PASS가 아니다. 기존 terminal budget·형식은 그대로 유지하고 history budget과 합쳐 기존 gate를 우회하지 않는다.

ring wrap, window truncation, suppression, invalid time/key 및 counter/ticket saturation은 수치/flag로 명시하고 누락 구간은 unknown으로 판정한다. ring1024는 시간 길이 보장이 아닌 event 개수 한도다. 종료 전 crash/강제 shutdown으로 EndSession이나 동일 TickExit가 확보되지 않으면 history 확보를 보장하지 않으며 없는 dump를 정상/완전 증거로 간주하지 않는다.

**의존성과 비용:** Application의 numeric event를 Infrastructure subscriber가 받는 단방향 구조이며 Application→Infrastructure/Unity.Netcode 역참조를 추가하지 않는다. Editor/Development 전용으로 Release에서 history 경로를 제거한다. disabled 상태에서도 history arrays 할당·추가 NGO query·formatting이 없어야 한다. 진단 subscriber가 live anchor/current-time/Scheduler/Coordinator 결정·emitter/HP를 쓰지 않으며 기존 diagnostic live-clock isolation을 보존한다.

**불변/미확정:** 서버 HP와 권위, threshold/기존0.5초 age, lease/marker/클립/씬/Stats/규칙·single writer/emitter 및 baseline/취소 계약은 유지한다. Presentation/NetworkUnit/Coordinator는 수정 대상이 아니다. expiry 원인·source/timing/pixel·성능 및 실기 수용은 OPEN이다. 신규 Task/TC/QA/new validator, Git/build/UI 실행 또는 새 runtime log 생성 없음.

## 2026-10-09 bounded numeric clock history 최종 구현·gate 인계

00_10 실제 Client Expired4의 원인을 다음 경기에서 구분하기 위한 관측 장치가 구현·자동 검증되었다. **진단 준비 완료이지 실제 Expired4 해결/실기 수용은 아니다.** 위 source 미구현/메뉴 대기 기록은 당시 단계의 이력이며 이 절이 현재 준비 상태를 supersede한다.

- live numeric ring1024, frozen window128×4, event sequence/Tick ticket/exact-key 및 generation을 보존한다. Client Expired 결정이 arm하고 동일 ticket TickExit에서 동결한다. wrap/truncation/suppression/invalid/saturation 및 불완전 종료는 unknown이지 정상 completeness가 아니다. BeginMatch/reset에서 이전 generation 창은 보존되지 않는다.
- **[2026-10-09 정정 — 원문 보존]** NCC는 lifecycle 소유자가 아니라 **RPC entry ticket→accepted exact-key binding** 연결이다. observer가 BeginMatch/reset/EndSession/subscriber lifecycle을 담당한다. Application numeric event→Infrastructure subscriber 단방향으로 live anchor/current-time/Scheduler 결정·HP/emitter를 바꾸지 않는다. Main은 Bridge/NCC의 Dev 블록 제외 원문이 baseline과 동일함을 확인했다.
- 출력은 `GameLog.Dev.Warn`의 기존 GameLog→RuntimeLogger 경로이며 별도의 temporary direct RuntimeLogger writer가 아니다. **[2026-10-09 정정 — 원문 보존]** UTF-8 budget은 formatted GameLog envelope까지다. 플랫폼 logcat prefix·stack·줄 separator는 제외하며 실제 Android 물리 출력 byte-limit/성능 수용 증거가 아니다. full line<1000B, window≤258lines, session≤1033lines/1,033,000formatted bytes 계약을 유지한다.

### 현재 검증 증거와 한계

- programmer static4 Runtime/Editor/Development/Release errors0, warnings 각각20/0/20/24. 현재 project source와 설치 Unity6000.3.5f2 managed refs를 사용했다. 기존 package artifacts는 재빌드하지 않았다. Android/IL2CPP 빌드 결과가 아니다.
- Main 독립 CLR3: Replay + Policy Development + Policy Release 모두 exit0. [Temp/expiry-clock-history/Replay.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Replay.log)와 두 Policy 로그/manifest 참조. history/C2 on/off 동일 normal Ready/time100.101, genuine late Expired/time100.601 유지; throwing subscriber 비간섭, Release diagnostic type 제거, EndMatch callback unknown 및 반복 reset/subscriber/storage cleanup을 분리 검증했다.
- record200B; numeric ring+frozen307200B; managed header/padding 포함 Reset 배열 할당307560B; disabled Reset0B. formatter1033lines/540898formatted bytes, max531B, worst record part0 666B/part1 618B, fallback0. **할당 layout/fixture 결과이지 NGO query count나 gameplay 성능 측정은 아니다.**
- native RPC/frame 호출은 CLR JIT Unity ECall 제약으로 runtimeCount 검증 불가; disabled early gate는 source로 확인했다. 실제 NGO native query count/performance는 未검증/OPEN.
- Main Windows computer-use 실제 Unity Action 메뉴 2026-10-09 13:41:11 시작→13:41:36 overall/C2/C3 PASS, Root cross audit 메뉴13:44:44 PASS. 최신 Console errors0/warnings27 유지. 두 메뉴 모두 완료했고 씬 저장/빌드는 하지 않았다. 이 fresh 메뉴 증거가 이전 ReloadingDomain busy/메뉴 대기를 supersede한다. HP/VFX production fixture gate와 particle/pixel 실기 수용은 별개다.
- Main 최종 확인: frozen source4 모두 manifest hash와 동일, protected Game/Login/Stats/Assault anim hashes 모두 Main baseline과 동일. 이는 Main 인계 증거이며 문서 담당의 새 UI 실행/asset diff 주장으로 바꾸지 않는다.
- 예비 실패도 보존: stale response의 삭제 source CS2001, Firebase/CloudSave/Leaderboards refs 누락, Editor stale runtime/Assets/Editor 누락, native DLL refs, observer CLR NGO resolution 보완. synthetic550은 overflow가 아니어서650으로 보강한 뒤 기존 fixture PASS. 새 validator/Task/TC/별도 QA는 만들지 않았다.

### OPEN 및 다음 사용자 경기

실제 런타임 Expired4 원인, source-marker, PhaseClockRegression, 실기기 오류, timing/pixel, 성능과 NGO native query count는 **OPEN**이다. canonical1831/ClientReady1827/Expired4 및 required1775/1775의 ready-subset 경계를 그대로 유지한다. 기록 장치의 PASS로 실제 원인/수정 PASS를 선언하지 않는다.

다음 사용자 검증은 **새 Development 빌드→경기 종료→로비 복귀→저장(EndSession dump)** 순서다. 이번 문서 단계에서 빌드를 시작하지 않는다. 강제 종료/crash/shutdown 또는 EndSession 이전 reset으로 동결 history가 누락될 수 있으므로, dump 미확보를 실패0/완전 관측으로 해석하지 않는다.

### Clock-history 최종 파일 인벤토리 (2026-10-09)

아래는 이번 구현/기록 인계 범위다. 다른 기존 dirty 파일의 변경을 이 작업에 귀속하지 않는다. source4는 programmer 구현본이며 이번 문서 단계에서 source/Temp를 수정하지 않았다. 파일별 bytes/SHA256 및 검증 참조는 [Temp/expiry-clock-history/manifest.json](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/manifest.json) (self-hash 제외).

**구현 source 4개**

- [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs](D:/Projects/Hexiege/Hexiege/Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs) — SHA256 `FB0E5F9FDB64FE29446ADFBAD7C827AA068B2FB00D8E2BB93DF7D62C652279CC`
- [Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs](D:/Projects/Hexiege/Hexiege/Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs) — SHA256 `62E639848EE0C877900CECF2CDBDECD0381AE4FBA0D62AFE830D89D63FA777C8`
- [Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs](D:/Projects/Hexiege/Hexiege/Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs) — SHA256 `AB8DAC6C0AE0C53BA62DBCFD34199D0C67F95CCD1759F9228FDA935E06A95CCA`
- [Assets/_Project/Scripts/Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs](D:/Projects/Hexiege/Hexiege/Assets/_Project/Scripts/Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs) — SHA256 `4376D3E2B0D8F1B13BD133DBC6D4796A2BA3154589C30F414783D17AEFF6819F`

**기록 문서 8개 + 검사기 관리 기준값 1개**

- [Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md)
- [Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Plan.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Plan.md)
- [Assets/_Project/Docs/PROJECT_STATUS.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/PROJECT_STATUS.md)
- [Assets/_Project/Docs/ROADMAP.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/ROADMAP.md)
- [Assets/_Project/Docs/WORK_HISTORY.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/WORK_HISTORY.md)
- [.claude/MEMORY.md](D:/Projects/Hexiege/Hexiege/.claude/MEMORY.md)
- [.claude/agent-memory/game-programmer/logging.md](D:/Projects/Hexiege/Hexiege/.claude/agent-memory/game-programmer/logging.md)
- [.claude/agent-memory/qa-tester/patterns.md](D:/Projects/Hexiege/Hexiege/.claude/agent-memory/qa-tester/patterns.md)
- [.claude/agent-memory/_baseline.json](D:/Projects/Hexiege/Hexiege/.claude/agent-memory/_baseline.json) — 기존 기준값, 메모리 증가분을 검사기의 `--update-baseline`으로 반영하는 대상(직접 편집/신규 생성 아님).

**Temp 38개** — manifest에 temporary-validation 37개 + manifest.json 자체 1개. 전체 경로/해시는 manifest에 있다. 아래 목록은 현재 이 검증 디렉터리의 정확한 인벤토리이며 Temp 전체나 새 경기 로그 목록이 아니다.

- [Temp/expiry-clock-history/Development.dll](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Development.dll)
- [Temp/expiry-clock-history/Development.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Development.log)
- [Temp/expiry-clock-history/Development.pdb](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Development.pdb)
- [Temp/expiry-clock-history/Development.rsp](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Development.rsp)
- [Temp/expiry-clock-history/Editor.dll](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Editor.dll)
- [Temp/expiry-clock-history/Editor.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Editor.log)
- [Temp/expiry-clock-history/Editor.pdb](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Editor.pdb)
- [Temp/expiry-clock-history/Editor.rsp](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Editor.rsp)
- [Temp/expiry-clock-history/formatter-output.txt](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/formatter-output.txt)
- [Temp/expiry-clock-history/Manifest.ps1](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Manifest.ps1)
- [Temp/expiry-clock-history/Policy.cs](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy.cs)
- [Temp/expiry-clock-history/Policy-Development.compile.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy-Development.compile.log)
- [Temp/expiry-clock-history/Policy-Development.dll](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy-Development.dll)
- [Temp/expiry-clock-history/Policy-Development.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy-Development.log)
- [Temp/expiry-clock-history/Policy-Development.rsp](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy-Development.rsp)
- [Temp/expiry-clock-history/Policy-Release.compile.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy-Release.compile.log)
- [Temp/expiry-clock-history/Policy-Release.dll](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy-Release.dll)
- [Temp/expiry-clock-history/Policy-Release.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy-Release.log)
- [Temp/expiry-clock-history/Policy-Release.rsp](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Policy-Release.rsp)
- [Temp/expiry-clock-history/references.sha256](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/references.sha256)
- [Temp/expiry-clock-history/Release.dll](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Release.dll)
- [Temp/expiry-clock-history/Release.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Release.log)
- [Temp/expiry-clock-history/Release.pdb](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Release.pdb)
- [Temp/expiry-clock-history/Release.rsp](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Release.rsp)
- [Temp/expiry-clock-history/Replay.compile.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Replay.compile.log)
- [Temp/expiry-clock-history/Replay.cs](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Replay.cs)
- [Temp/expiry-clock-history/Replay.dll](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Replay.dll)
- [Temp/expiry-clock-history/Replay.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Replay.log)
- [Temp/expiry-clock-history/Replay.ps1](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Replay.ps1)
- [Temp/expiry-clock-history/Replay.rsp](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Replay.rsp)
- [Temp/expiry-clock-history/Replay.runtimeconfig.json](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Replay.runtimeconfig.json)
- [Temp/expiry-clock-history/Runtime.dll](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Runtime.dll)
- [Temp/expiry-clock-history/Runtime.log](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Runtime.log)
- [Temp/expiry-clock-history/Runtime.pdb](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Runtime.pdb)
- [Temp/expiry-clock-history/Runtime.rsp](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Runtime.rsp)
- [Temp/expiry-clock-history/unity-version.txt](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/unity-version.txt)
- [Temp/expiry-clock-history/Validate.ps1](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/Validate.ps1)
- [Temp/expiry-clock-history/manifest.json](D:/Projects/Hexiege/Hexiege/Temp/expiry-clock-history/manifest.json)

`Temp/expired-replay`는 현재 없으며 보존/재실행했다는 현재 claim을 하지 않는다. 오래된 기록은 당시 이력으로만 유지한다.

### 최종 문서 검사 및 이력 보존 확인 (2026-10-09)

- 문서8개 + 기존 메모리 기준값1개 갱신. 신규 baseline/Task/TC/QA/source/Temp 생성 없음. source4와 Temp38은 기존 programmer 구현·검증 인벤토리다.
- 검사기 실제 범위: 규칙 정의 원본10문서, 참조 검색77개 .md(그중 .claude40개), _Tasks/_Logs 제외. [1] 번호 결번, [2] 파일 링크, [3] 규칙 번호 참조, [4] 모호한 섹션 참조, [5] 제목 병기 불일치, [6] 고아 토픽, [7] 메모리 총행수 감소 **모두0건/exit0**. Task의 구현/PASS 자체를 이 검사로 증명하지 않는다.
- 기존 baseline을 검사기 --update-baseline으로 증가분만 반영: programmer3886→3892(+6), QA1378→1384(+6), 감소0. 첫 실행은 저장 후 Windows cp949 출력 UnicodeEncodeError가 발생했고 UTF-8 설정 후 재실행은 성공했다. 신규 생성/직접 baseline 편집이 아니다.
- 문서8개 각각 추가 블록을 제외한 원문을 재구성하여 수정 전 SHA256과 일치 확인(기존 줄구분 포함). 원문 삭제/덮어쓰기 없이 이력을 보존했다. source4 current hash mismatch0, Temp directory 실제38files=manifest temporary37+self1, old expired-replay 없음 재확인.
- 남은 사항은 사용자 새 Development 경기와 EndSession dump 확보 및 실제 결함 원인/실기기/pixel/performance/NGO native query count OPEN이다. 문서 단계에서 Git/빌드/씬 저장/규칙·Stats·에셋 변경은 하지 않았다.

## Canonical Tick 실제5소스 적용·최종freeze 인계 (2026-10-10)

**15:52 최종 추가 확인 — 아래 earlier pending을 supersede:** Main freeze check22항목 mismatch0/VFX unexpected0 및 보호6＋Assault Attack hash 불변, freeze 이후 source 변경 없음 확인 인계. fresh ROOT15:52:38 PASS는 Main UI/Editor.log 실확인이다(saved daily log 경로/anchor 미수신, 새 runtime 파일 추정 금지). Unit Action15:47:57 FAIL은 위 Tank 실제Cannon/새 tower Tank prefab 불일치의 사용자 선택 대기, 빌드 미시도다. below pending 문구는 이 추가 인계 이전 스냅샷이며 현재는 사용자 선택→승인 blocker 교정→Unit Action 재검증→build attempt/사용자 완료 gate가 남는다. 원64 재현·remaining50 원인·runtime/timing/pixel 수용 OPEN.

이 절이 phase1/2 proposal-only 단계보다 최신이다. programmer 최종freeze 보고와 Main12:51 CLR 결과를 기록하며 원64 입력 전체 재현 또는 실제 경기 해결로 확대하지 않는다. 문서 담당은 manifest와 현재5소스 hash를 독립 대조해5/5 일치를 확인했다. Main 별도 재확인 결과는 아직 수신 전이다.

### 실제 수정5개 — 추가 소스 없음

아래 경로는 `Assets/_Project/Scripts/` 기준이며 현재 SHA256이다. manifest 원문은 `Temp/expiry-clock-history/canonical-tick-source-freeze.json`, SHA256=`BA2D5FDCA19BBF6D8144AFA7E5D3EA542B91F0B43A56522064DF4CA4347ADAAA` (문서 담당 실측 일치).

| 수정 경로 | 현재 SHA256 | 적용 계약 |
|---|---|---|
| `Presentation/Effects/UnitAttackResultPresentationShadow.cs` | `40B222A8DD91C722E66FA2C793972CEB5AFFDC86BBCB78C77841501EE684D810` | active Update explicit canonical clock 공급, provider1 query/inactive0 |
| `Bootstrap/GameBootstrapper.Map.cs` | `2C1734FCC7667995680BF80D69828D95D0FCE1FA42587195EDF3A7F1F2128400` | Initialize 전 WaveServerTime provider, network/listening/shutdown guard·generation rebind |
| `Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs` | `50F751D89AB78B580CC03384C74F753391F3B0AB0976F08D1EC0E2A65BE9070F` | explicit server/local Tick, live decision의 stale Bundle extrapolation fallback 없음 |
| `Infrastructure/Network/UnitAttackShadowObserver.cs` | `65D35E9CABA467C7E9709229DCCCBFC62039D7799F36E8BB5BCB31DD30FC15C0` | bounded supplied clock/anchorDelta/invalid 집계 및 END; per-frame formatted log 없음 |
| `Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | `74CE7042FA6CE1794D6A025E05F1F818970DCF938984E2293E15F9D3E404DD81` | 기존 canonical fixture 확장, invalid/lifecycle/mode/noninterference 회귀 |

VFX44 비교는 **2변경/42불변**이다. 변경2개는 위 Bootstrap Map과 기존 VFX fixture host인 RunAuthoritativeAttackPresentationValidation이다. 앞선43불변 예상은 최종인계로 정정하며 원문 이력은 보존한다. 보호6(Game/Login/UnitStats/SpecialAttack/Tank prefab/Bloom Attack)와 Assault Attack clip은 programmer baseline hash 일치 보고다. HP/Q/NCC/Setup/UnitView 및 Stats·클립·씬은 이번5소스 교정 대상이 아니다.

### 정적/CLR 증거와 가정

- Runtime/Editor/Development/Release static4 errors0/exit0; warnings 각각20/0/20/24. warnings0 전체 주장 금지. `Temp/expiry-clock-history/{Runtime,Editor,Development,Release}.dll`의 정적 컴파일이며 native Player build 증거가 아니다.
- Main12:51 Replay canonical Tick fixture/bounds PASS 및 최종 Replay/Dev/Release CLR all exit0 보고. 실제512 numeric 입력을 사용하지만 원 ticket earlier pending/lifecycle가 없어 synthetic schedule/pending을 포함한다. endpoint14 Ready/50 Expired; all-retained canonical Tick33 Ready/31 Expired의 추가19건은 window3 preseed availability 가정에 민감하며 **실제 추가 교정 증거 아님**.
- Dev/Release policy: C2 off/on 모두 Ready/time100.101, true-late Expired/time100.601 동일. observer on/off/repeated replay 동일, single query와 supplied frame 재사용 검증은 CLR 경계다. actual native NGO acquisition/inactive MonoBehaviour/lifecycle는 미검증.
- before-fix RED exit1 보존: `Temp/expiry-clock-history/canonical-before-fix-RED.log` SHA256=`B3DB10B1F27DF2F7D4771A5AC348F42845101511097BEB05695E5148B9D17D84`. after evidence: `Temp/expiry-clock-history/Replay.log`, `Policy-Development.log`, `Policy-Release.log`; 실제 입력/가정은 `Replay.cs`/`Policy.cs`. synthetic 결과로 original64 전량 해결을 선언하지 않는다.
- fixture 보고: stale epoch14, invalid NaN/infinity/negative/throw 및 pending retention, closed/rebound/inactive, 양 pipeline mode no-anchor, diagnostic/frame throw noninterference, END byte preflight/canonical flag4096. END=`coordinator-clock-END`, source=`canonical-wave-server`; delta는 old-anchor extrapolation minus supplied canonical server clock의 진단값이지 실제 network lag 측정이 아니다.

### 최신 native/빌드 및 미변경 정책

- Main fresh Unity Unit Action15:47:57 **FAIL**: Human tower 별preset이 실제 Tank prefab을 공유하는 것을 fixture가 거부했다는 인계. 실패 fixture 실제 경로는 `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs:33`이며 UnitAction과 AuthoritativeAttackPresentation이 호출한다. 정밀 원인/교정은 Main/programmer 후속이다. 이 실패를 Assault CLR 실패나 전체 native PASS로 바꾸지 않는다.
- 새 ROOT native 결과 미수신·빌드 미시도. native gate blocker 해소 및 재실행 후 실제 결과를 추가한다. 과거 Unity PASS를 재사용하지 않는다. 원64 재현, remaining50 지연 원인, PhaseClockRegression 인과, timing/pixel/performance/runtime 수용은 OPEN.
- 기존 계약 원본 `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`의 NET-TIME-001/002/003 및 NET-PRESENT-003/004와 의도 일치: synchronized server time·0.10 표현지연·0.50 catch-up·exact-key·취소·단일writer 보존. 원본 규칙 변경 불필요. Stats/Assault0:05 및 clip/cycle 값도 불변이므로 관련 수치 문서/에셋 변경 불필요. 기존 VFX Task/목록 변경 없음.

## 21_18 phase1 분석과 phase2 제한 replay 인계 (2026-10-10)

### 실제 경기 근거와 누락 경계

- Device: `Assets/_Project/Docs/_Logs/2026-10-09/21_18_logcat/RuntimeLog_device.txt`, raw585481: results3160/ready3096/fail64 **FAIL**. run=`acaffe2adfc44305b219beb164b32873`, session=`c8324098ac17654eef8301eae863d9b7dd3167db7419cc34df02fe5690d7b429`.
- Host: `Assets/_Project/Docs/_Logs/_editor/2026-10-09/RuntimeLog.txt`, raw94591: ready3160/fail0 **EVIDENCE**. Host 성공이나 local ROOT는 Client64 실패를 상쇄하지 않는다.
- Numeric part-join Expired64는 windows3+14+28+19,19 UnitIds다. 4 windows의128-row 제한 capture는 truncatedTrue/coverageUnknownTrue이며 원 failure tickets12581/20655/21016/25898 이전 RPC 입력은 보존되지 않았다. 전체 원인 입력이 확보됐다고 주장하지 않는다.
- zero-based window1의14건: retained anchor152.520274와 fresh NGO151.983345의 차이0.536929s(약0.537s).14건 모두 fresh arithmetic상 in-bound이며 나머지50은 fresh 기준으로도 late다. 첫3건은 recvAlreadyLate이므로 post-receive 약166ms 대기만으로 전체 실패를 설명할 수 없다. 이 산술은 Ready 재현이나 네트워크/lag 원인 확정이 아니다.

### Main/programmer phase2 보고 — actual subset와 counterfactual 구분

- 실제512 numeric records를 real BridgeCoordinator에 입력했다. 원 ticket의 이전 입력이 없으므로 pending/schedule 구성은 synthetic이다. **원래64 전체 production 재현은 아직 아니다.**
- retained-anchor contrast:64 Expired. 마지막 Bundle의 canonical clock만 변경한 counterfactual:14 Ready/50 Expired. 마지막 Bundle 이후 wait만 제거한 비교:64 Expired.
- 각 비교2회 반복 및 observer on/off 결과는 동일했다. baseline Replay/Development/Release exit0 보고는 별도 baseline 검증이며 경기 전체/새 native/실기 PASS가 아니다.
- 이 제한 비교는 live decision clock의 stale Bundle epoch extrapolation 제거를 최소 교정 후보로 지지한다. 원 입력 미보존, 나머지50 true-late 분리, 실제 source 적용 뒤 native/runtime 검증은 OPEN이다. canonical clock 자체의 정확도 또는 네트워크 지연 원인까지 증명하지 않는다.
- 기존0.10/0.50, HP 단일 writer, exact keys/cancellation, Assault0:05, VFX44 freeze는 보존한다. 정책/TTL 확대나 clamp-only 처방이 아니다. 이번 갱신은 Research/Plan2개뿐이며 소스·에셋·상시문서·메모리·빌드 변경은 없다.
