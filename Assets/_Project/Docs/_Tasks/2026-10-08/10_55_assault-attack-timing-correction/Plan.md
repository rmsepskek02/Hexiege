# Assault 0:05 구현 계획

## 2026-10-11 사용자 focused 수용 — 다음 후보 추천만

사용자 최신 확인: **“로그상으로 문제없으면 됐어 확인했어 다음 작업 어떤거할거야?”** Main은 최신19_07 두 경기의 Assault·Sniper·Inferno 정상 관측을 현재 focused 범위에서 충분한 근거로 판단하고 **로그 기반 사용자 수용·다음 진행 가능**을 인계했다. 이는 정밀 pixel/subframe·exact payload/HP 동일·global migration 완료가 아니다. 아래 overall END FAIL 분석은 유효하며 Invalid 진단 scope·과거 Editor Client Expired63 원인/해결은 별도 OPEN이다. 역할 반대·suppressedNormal63≠Expired63·정상 retry109/dead-abort 실기 미재현 경계를 유지한다.

**Main 다음 추천은 LittleKnight(추천만, 새 Task·구현 승인 없음).** 매트릭스의 단독 focused 교정 미완·현재2hits0.25/1.15초를 기준으로 숫자 재설계 없이 추후 승인 작업에서 source/사용자 clip30fps를 확인한다. projectile 미준비 영향 없음은 Main 인계다. 이번 source/clip 조회·수정·Git/build·새 Task/TC 없음; 아래 수용 미확인 문구는 당시 이력으로 보존한다.

## 2026-10-11 19_07 최종 실기 분석 인계 — 전체 FAIL / 후속 제안만

두 새 경기(Editor Host / Android Client)에서 Ready452/398·Coordinator fail/pending0, required presentations435/387, C2 resultfail/Client reject0과 정상 retry109건을 관측했다. 사망 대기 dead-abort/timeout 실제 사례는 재현되지 않았다. overall END 양쪽 FAIL은 attacker=-1/default key의 LegacyObservation 입력 자격→Invalid→진단 집계 경로로 확정됐으나 개별 틱의 원인 유닛·조회 실패 사유는 미확정이며 Inferno로 단정하지 않는다. gameplay HP/text/VFX 오작동 증거나 전체 완료 PASS가 아니다.

**후속: 진단 적용 범위·결과 집계 정합성 검토 제안만/OPEN; 이번 소스·빌드 없음.** 이전00_46은 Android Host / Editor Client로 역할 반대이며 기존 Editor Client63만료의 재현/해결 증명이 아니다. 신규 Expired/D0, 2차 Client `suppressedNormal=63`은 Expired63이 아님(Ready398/fail0). 양쪽 local ROOT 총4 PASS와 formal CrossAudit 미실행을 구분한다. exact payload/HP 동일 주장은 하지 않는다. 상세 session·로그·원인 경로·중복 집계·retry 대기/과거 timeout 제외 근거는 Research 「2026-10-11 19_07 저장 로그 최종 분석」에 단일 기록한다. 아래 구현·검사 이력 보존; 새 Task/TC/구현 없음.

## 2026-10-11 현재 승인4파일 계획 — expiry 근거 정밀화·죽은 유닛 retry 종료

**현재: 구현 완료 / 실기 수용 미완료(Main 최종 검토 승인).** 승인4파일 저장·programmer/Main 최종 해시 일치, static4 errors0, CLR retry18시나리오/ReceiptPhaseClock/CanonicalTickClock 모두 PASS·최종 exit0. D worst-case792 UTF-8 bytes <1000. native Unity callback/NGO PlayerLoop·fresh UnitAction/ROOT·멀티 HP 화면/실게임·빌드는 미실행, 원63건 원인/해결 OPEN. 아래16시나리오·검사 pending·준비단계는 당시 이력이다. 전체 결과/산출물 시간/4소스 및 보호 해시는 Research 「최종 구현·검사 근거」에 단일 상세 기록한다.

화면에 육안 문제가 없었다는 사용자 관찰과 별개로, 서버 피해는 그대로 두고 죽은 유닛의 초기화 재시도를 정상 종료한다. Assault 표현 만료는 기존 정책을 유지한 채 실제 수신 표본과 갱신된 판단 시계를 분리해 원인을 좁힌다. Main이 아래 기술안을 승인했으며 **문서 동기화 후 programmer 구현 GO** 순서다. 아직 적용·해결·새 검증 완료를 뜻하지 않는다.

### 정확한 기존4파일 manifest (`Assets/_Project/Scripts/` 기준)

| 수정 파일 | 승인된 변경·회귀 | 규칙 근거 |
|---|---|---|
| `Infrastructure/Network/NetworkProductionController.cs` | 실제 retry owner/session/controller generation·captured UnitSpawnUseCase/factory exact refs·spawn/domain exact reference 수명 검사, 정상 Abort bounded 로그. | UnitCombatSynchronization NET-ACTION-SEQ/NET-ACTION-IDEMPOTENT의 객체 수명 식별 및 NET-TIME-005 baseline; Units 규칙19의 사라진/재사용 View 반응 금지. |
| `Editor/Combat/RunUnitActionSelfValidation.cs` | 실제 IEnumerator 경로의 성공/진짜 timeout/죽음/retire/동일ID 객체 교체/세션 종료 회귀를 기존 gate에 연결. | 같은 객체 수명 계약·Units 규칙19; 정상 완료와 timeout 안전망 구분. |
| `Infrastructure/Network/UnitAttackShadowObserver.cs` | RPC 시점의 이전 갱신 표본과 freshCanonicalTick을 같은 peer의 경과시간으로 구별하는 bounded expiry 진단. | NET-TIME-001~005, NET-PRESENT-003/004. |
| `Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | 실제 CanonicalTickClock→Coordinator 경로 Ready와 genuine Expired 회귀를 기존 gate에 연결. | NET-TIME-001~005, NET-PRESENT-003/004; genuine late/catch-up/baseline 보존. |

### Retry 소유권·세션 검사 계약

- 서비스 reference만 같다는 것으로 다음 경기의 작업 소유권을 인정하지 않는다. 같은 서비스가 재사용될 수 있으므로 **기존 controller lifetime generation**을 캡처하고 spawn/despawn/GameEnd에서 무효화하는 기존 수명 경계와 일치시킨다. **OnGameStarted마다 generation을 증가시켜 정상 진행 작업을 취소하는 방식 금지.**
- captured UnitSpawnUseCase/factory exact references, `GetUnit(id)`가 캡처한 **동일 UnitData**인지와 IsAlive, network listening/shutdown, controller owner active/spawn 상태를 함께 검사한다. null/retire/death/교체/세션 종료는 old retry의 정상 Abort이며 새 객체 초기화로 이전하지 않는다.
- yield 전·후 및 timeout 로그 직전에 검사한다. 유효한 living same-owner 작업의 성공·진짜 timeout은 유지한다. 정상 Abort와 진짜 InitTimeout을 bounded 로그에서 구분하고 server HP/생산 결과를 만들거나 바꾸지 않는다.
- OnDisable→OnEnable은 유효한 현재 수명의 재개만 허용하며 GameEnd 뒤에는 `_retryMatchEnded`로 재개를 금지한다. owner/closed reason/Coroutine shared seam과16시나리오의 기존 gate 연결은 Main 부분 검토 근거이며, static/CLR/native 통과 판정은 아직 없다.

### Expiry 진단·원인 경계

- NGO 소스 인계: NetworkManager.cs336 EarlyUpdate/349 incoming→399 PreUpdate tick,864 cached ServerTime. 같은 frame RPC receive189.62629와 fresh canonical Tick190.736736의 차이는 phase별 표본 freshness를 분리해야 하는 근거다. 표면상 receiveBeforeLimit만으로 on-time을 확정하거나 서로 다른 peer 시계를 직접 빼지 않는다.
- bundle-anchor는 진단용이며 실제 canonical-wave-server 판단을 오염시키지 않았다는 확인을 보존한다. observer off/on은 판단을 바꾸지 않고 per-frame 무제한 문자열 로그를 추가하지 않는다. raw phase/sample/local elapsed·gap의 명시로 frame/clock 기여도를 분리한다.
- 실제 clock→Coordinator 경로에서 Ready와 genuine Expired를 모두 보존한다. 모든63 Ready를 만들거나 `.50`을 늘려 통과시키지 않는다. 상세16/전건63 status 구분, 긴 프레임·시계·네트워크 근본 원인 미확정 유지.
- **구현된 진단/Main 부분 검토:** D expiry formatter의 추가 필드는 기존16상세 bound를 유지한다. 실제 `canonicalTickInProgress` bool을 전달해 canonical Tick과 lifecycle/validation 호출을 구별하고 후자는 `sampleDeltaKnown=false`/unknown으로 기록한다. previous RPC/fresh canonical/local elapsed 구분은 원인 분리 준비이지 원63건의 false expiry 확정이 아니다. Ready/genuine Expired fixture의0.10/0.50은 보존됐다; 실행 결과 pending.

### 보존·인계

- server HP/단일 writer/권위 결과·exact key·cancellation/baseline·`.10` 표현지연/`.50` catch-up·Stats/주기/범위/scene/clip/VFX 참조/다른 정책 유지. 기존 로직의 무검증 삭제 없음. production clock/presentation expiry 정책 변경 없음.
- 구현4파일 인계→Main 검토→실제 static/native 근거를 순서대로 기록. Unit52 retry 교정과 Assault expiry 원인 해결은 별도 판정. **이번 build 시도 지시 없음: 자동 실행하지 않음.** 문서 담당 코드/Git/UI/build 없음.
- 기존 Research/Plan만 먼저 갱신, 새 Task/Sniper Task/TC 없음. 이후 상시문서·memory는 짧은 상태+이 Task 링크로 실제 증거만 갱신하고 `check_docs`는 이번 작업 최종에 한 번 실행한다.
- Main 보호 해시와 Game 씬 재개 읽기 기준점은 Research의 「Main 보호 확인」에 기록한다. 최종 검사 결과 수신 후 실제 static/CLR와 native/실게임 미실행을 분리해 갱신하고 마지막 `check_docs`를 실행한다. 이번 build 승인 없음.
- **최종 실행 인계:** retry16→18은 timeout-dead/timeout-replaced가 마지막 factory 시도 중 Abort해 timeout을 차단하는2건 추가다. Main은 OnDestroy 해제/base call·D invalid-frame/lifecycle unknown·log budget gate와 최종6해시(4소스+Stats/Game) 일치를 승인했다. CLR3 PASS는 production-derived seam의 독립 실행 근거로만 기록하며 새 native/실기 PASS로 대체하지 않는다. 이전 단계의 pending 표기는 보존한다.

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


> **과거 계획 기록 (2026-10-10, 21_18 phase1/2 당시; 현재5소스 적용 상태는 위 요약):** Client Expired64/runtime OPEN. actual512 제한 Bridge replay는 pending/schedule synthetic을 포함하며 원64 재현이 아니다. canonical-clock-only counterfactual14 Ready/50 Expired를 근거로 아래 최소5파일 교정안을 Main 검토에 인계한다. 문서 승인/소스 구현/static/native/build/runtime 수용은 각각 별도 단계다. 이전 자동 PASS는 최신 경기 해결 PASS가 아니다.

돌격소총병이 사용하는 타격 시간을 30fps의 `0:05`(5/30초)로 명확하게 맞춘다. 이미 올바른 클립 이벤트는 건드리지 않고 기본 설정·생산 연결·문서를 일치시킨다. 테스트를 다시 해야 하는 로그 누락이 없도록 기존 공격 회차 진단을 Assault에도 연결한다. 공격 주기의 밸런스나 다른 유닛의 정책은 바꾸지 않는다.

## 구현 순서와 근거

1. `UnitStatsConfig.asset` type 1의 단일 타격 기본값만 `0.16666667`로 수정한다. `attackCooldown=0.2`는 보존한다. 근거: Units 규칙17·18, U-ATK-TIMELINE.
2. `Game.unity` UnitFactory Human type 1에 기존 실제 Attack 클립 참조 한 줄만 추가한다. 다른 씬 객체·MistShrine·production 프리팹·Animation clip은 수정하지 않는다. 근거: Units 규칙17, NET-AUTH-001/NET-PRESENT-002.
3. 기존 `PistoleerTimingDiagnostics`를 읽기 전용 공용 관측으로 확장한다. 기존 함수명/태그·Pistoleer 호환성은 유지하고 타입 식별을 추가한다. UnitView의 진단 전용 타입 필터만 두 타입으로 확장한다. Factory 양 생성 경로에서 선택 타임라인을 한 번씩 기록한다. 피해/회차/시각/lease/애니메이션 결정은 바꾸지 않는다. 근거: Units 규칙18·19·20, NET-AUTH-001/002·NET-TIME-001/002·NET-ACTION-IDEMPOTENT, LogRules 1.2/1.4/1.7/1.14.
4. 기존 Unit Action production gate에 실제 Assault Attack state/controller·양 프리팹·명시 씬 연결·30fps/0:05·기본 설정과 Factory 결과를 검사한다. 기존 진단 fixture에 Assault 실제 formatter 경로를 추가하고 parser가 타입 혼합을 구별할 수 있도록 한다. tolerance 완화·fake clock·scope 조작은 금지한다. 근거: Units 규칙17, NET-PRESENT-002/003.
5. StatsReference, UnitCombatAssetMatrix, 상태·로드맵·작업 이력과 관련 메모리를 실제 진행 상태로 갱신한다. `0:33` 주기는 미확정 이력으로 남기고 현재 runtime은 clip-derived 0:10임을 분리한다. 근거: 각 규칙 문서 서두의 원본 역할 분리, WORKFLOW [11].

## 확인과 빌드 경계

- 변경 전후 씬의 정규화 내용 비교로 Assault 참조 한 줄 외 변경이 없는지 확인한다. 사용자의 별도 수정은 되돌리지 않는다. Git은 실행하지 않는다.
- 문서 정합성 검사 0건 확인, 실제 Unity reload/컴파일 오류 여부 확인.
- 새 `Run Unit Action Self Validation` 및 `Run Unit Root Pose Cross Audit → Self Validate` PASS를 확인한다. 실패하면 원인을 교정하고 새 PASS 전 빌드하지 않는다.
- File → Build And Run을 한 번 시도하고 즉시 결과 화면을 확인한 뒤 완료 감시를 중단한다. 저장·보안·예상 밖 확인창은 임의로 넘기지 않는다.
- 자동 검증은 설정/구현 준비 증거다. 실제 서버 피해·화면 타격·반복 공격의 focused 수용은 새 사용자 로그/육안 테스트 후 별도로 판정한다.

## 사용자 테스트에서 확인할 범위

Assault의 0:05 타격, 반복 공격과 타겟 교체/사망 뒤 처리, Host/Client 표현을 본다. TorrentSpirit·BloomFairy도 함께 생산해 기존 연결 복구의 회귀를 확인한다. 기존 clock·경기 종료 후 callback·건물 랠리·성능 문제는 별도 미해결 항목이며 이번 타격값 정합화로 고쳤다고 선언하지 않는다.

## 승인 및 진행

사용자는 위 대화에서 계획 설명 후 task·문서·구현·빌드 시도를 명시 승인했다. 사용량 제한에 따른 직접 수행 예외도 승인했다. 기존 로직 삭제는 없다. 구현과 문서 정합화는 완료했다. 사용자 실기는 OPEN이다.

## 실제 확인 결과 (2026-10-08 KST)

- Unity 자동 재컴파일 후 실제 메뉴 `Hexiege/Combat/Run Unit Action Self Validation` 실행: 11:09:06 최종 PASS. 새 Assault production 검사도 실행 목록에 포함된다. 생산 formatter가 생성한 실제 분석기 fixture 29종 모두 `ok=true` (`Temp/pistoleer-timing-fixtures/parser-report.json`). 고의 오류 fixture의 errors는 기대대로 검출된 것이며 경기 실패가 아니다.
- 실제 메뉴 `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit` 실행: 11:10:24 PASS. 이전 계획의 축약 메뉴명은 실제 경로로 정정한다.
- 검증 후 Console errors 0, warnings 27. 경고 0을 주장하지 않는다. 문서 검사 7종 0건 확인; 규칙 원본 10문서/참조 77문서 범위 확인. 메모리 기준값은 증가 9행만 도구로 갱신했다.
- Game 씬 정규화 후 Assault 참조 한 줄을 제거한 SHA256은 변경 전과 동일: `F3C758A563E29C46E306D85FFAD485C1DF51FDE51BA455D024A164212B162F1E`. 무관 MistShrine 설정/씬 저장 작업 없음.
- File → Build And Run을 한 번 실행했고 즉시 JDK 검사 진행 화면을 관찰했다. 저장/추가 승인창은 나타나지 않았다. 요청대로 여기서 모든 빌드 UI/로그 감시를 중단했다. 완료·설치·실기 수용은 사용자 담당이며 자동 PASS로 대체하지 않는다.

## 변경 파일 전체 목록

추가 작업 문서: 이 폴더의 `Research.md`, `Plan.md` 및 Unity 생성 `.meta`, 폴더 `.meta`.

구현 수정:

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scenes/Game.unity` (Assault 명시 클립 참조 한 줄)
- `Assets/_Project/Scripts/Application/Combat/PistoleerTimingDiagnostics.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` (진단 필터만)
- `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs` (생성 타임라인 로그)
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- `Tools/analyze_pistoleer_timing.py`

상시 문서/메모리 수정:

- `Assets/_Project/Docs/StatsReference.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`
- `Assets/_Project/Docs/PROJECT_STATUS.md`, `ROADMAP.md`, `WORK_HISTORY.md`
- `.claude/MEMORY.md`
- `.claude/agent-memory/game-programmer/MEMORY.md`, `logging.md`
- `.claude/agent-memory/_baseline.json` (검사 도구의 증가 방향 갱신만)

검증 생성물은 `Temp/pistoleer-timing-fixtures/`이며 실기 로그가 아니다. TC/QA 문서는 생성하지 않았다. 클립·controller·Blue/Red 프리팹은 수정하지 않았다.

## 최신 역할교대 실기 후속 (2026-10-08)

Research의 최신 실기 분석 절이 이번 두 경기의 단일 상세 근거다. Editor의 01시 옛 Pistoleer 경기는 분리했다. 신규 session `50c16c6b…3c68a2`(Android Host / Editor Client)과 `d9f49c5c…1e9e5d`(Editor Host / Android Client)를 섞지 않는다.

- 두 경기 합계 Factory Host 88 / Client 88, 총 176관측에서 hit `0.1666666716`과 선택 cycle `0.3333333433`, fallback cycle `0.2` 확인: 승인 `0:05=5/30초` 설정 정합만 수용한다. 기존 `0:33` 의미와 새 밸런스 결정은 미확정/범위 밖이다.
- type 1 paired scope는 경기별 양 peer 2262 / 280이며 canonical Result/Receive pair는 2253 / 262다. Host/Client VfxStarted는 2244/1590 및 251/173, Client source incomplete는 644/74 관측 레코드다. 결과 키 pairing의 revision/role alias 제외와 source completeness를 구분한다. incomplete를 정확한 게임플레이 실패 수나 PASS로 치환하지 않는다.
- Client bound wrong-absolute-occurrence 578/62와 1차 unbound29/duplicate45, VFX 이전 return 및 loop/marker exact-match 코드 경계가 확인됐다. 피해 Receive/Ready와 source gate 차단이 함께 있는 2차 device 사례를 Research에 기록했다. multi-loop marker elapsed 2~7초를 타격 offset으로 해석하지 않는다.
- 실제 bound clock 역행 38/11events와 parser errors39/12(각 unscoped1)을 구분한다. raw<high-water의 animator 보류는 source 계약과 부합하지만 전체 누락의 clock 단독 인과는 미확정이다.
- **전체 OPEN / 사용자 visual 수용 없음.** 종합 근거 반영 완료. 다음 검토는 exact occurrence/clock/source gate의 같은 scope 연결이며, 분석 문서 갱신은 코드 교정·재빌드 승인이나 교정 구현을 뜻하지 않는다.

종합 결과: 1차 Assault26·BearGuard10·Bloom6·Torrent0, 전체 C22325/2325 reject0이지만 Client C3 Expired7·ready2318/2325다. mandatory2276/2276은 ready 부분집합이며 **Client 표현 FAIL/OPEN**이다. Expired7은 publish/receive limit 이내·decision limit 이후, 같은 local receive→decision448.512~1168.508ms 및 frame gap 최대751.367ms로 수신 후 지연/만료다. clock/frame lag의 모든 인과는 미확정이다.

2차 Assault62·Torrent13·Bloom0, 전체 C2262/262 reject0·C3fail0의 해당 범위 PASS와 별도 Torrent 파이프라인을 분리한다. wave224 spawn/receive→VFX199(거부24=clock4+payload-or-time20, 정상 경기종료 expire1); contact216 publish/receive→text198(거부18=clock1+payload-or-time17). 정확한 payload-or-time 분기는 미확정이다. Bloom HostStart/Play45·CastDispatch/Marker43 ↔ ClientPlay45·Marker43·positive HoT 첫 기록26건은 경로 실행 근거이며 exact timing/visual 완전 수용은 아니다.

MOVE 네 terminal EVIDENCE·adapter/handoff/spatial fatal/drop0, Hostframes14261/69482 및 ROOT 네 local PASS·errors0·complete=True는 로컬 gate 범위다. formal CrossAudit Analyze 미실행이며 공격 전체 PASS로 확대하지 않는다. 후속은 source/clock·Client 만료·Torrent 거부 분기 증거 검토와 사용자 visual 확인이다. 새 코드·빌드는 이번 분석 범위에 없다.

이번 분석 반영에서 코드·씬·에셋·Unity·Git 변경 및 새 Task/TC/QA 생성은 없다. 이전 구현·자동 gate·빌드 시도 이력은 보존한다.

## 사용자 승인 후속 — 진단 로그만 보완 (2026-10-08, 계획 단계)

사용자가 “그렇게 진행해”로 **기존 문서 갱신 → 로그만 구현 → 새 실제 Unity 두 검증 PASS → Build And Run 시작 시도만**을 승인했다. 재승인 질문은 하지 않는다. programmer의 구현 착수와 구체 중간 범위를 수신해 아래에 반영했다. **구현 진행 중·최종 완료/새 PASS/빌드 미확인**이며 과거 코드/빌드0 기록과 이전 구현·메뉴 이력을 소급 변경하지 않는다.

1. **programmer 구체 계획 대응:** Research의 가설별 예측/수집값과 구체 7파일 표를 기준으로 진행한다. UnitAttackPresentationPolicy 읽기 전용 개발 accessor → UnitView NativeLease/Hit bind/consume/retire/preserve·expected loop/marker/time/consumed·actual callback → 기존 PIST-TIME 단계/phase-observation domain·parser 필수값을 연결한다. WavePresentationCoordinator의 firstFailed/invalidMask/limits/raw clock, UnitAttackShadowObserver의 기존 A/B 유지 및 C receiveAnchorAdvanceEstimate/anchoredJumpEstimate/bridgeToDecisionLocal, 기존 RunUnitActionSelfValidation fixture 보강이 인계된 범위다. 최종 필드/schema·포맷 크기·cap 수치·diff는 대기하며 완료로 선언하지 않는다. 근거: 규칙 인덱스 및 Units17~22·29, U-ATK-TIMELINE, NET-PRESENT-001/002/003.
2. **공격 lease 관측:** 예상 loop/marker와 실제 callback, bind/replace/skip/consume/retire의 전후 identity·이유를 같은 scope/native epoch로 연결하고 clock 보류/재개 전후를 구분한다. wrong-absolute/unbound/duplicate/late/skipped를 합치지 않는다. lease 결정·Animation 재생·서버 타격은 불변이다. 근거: Units21·22, NET-TIME-001, NET-PRESENT-001/002 및 NET-CANCEL 계약. 전체644/74를 clock 하나의 실패 수로 바꾸지 않는다.
3. **Torrent 거부 관측:** 기존 최종 거부 지점의 exact predicate·입력값/기준값·실제 경로를 기록해 payload-or-time을 분리한다. spawn VFX와 contact text, clock 거부와 정상 경기종료 expire를 별도 집계한다. 서버 wave/contact/HP·예정 발사/취소는 변경하지 않는다. 근거: Units29, NET-AUTH-001/002·NET-DELIVERY-TRAVELING·NET-CANCEL-003/004/005.
4. **C3 만료 관측:** 같은 결과 키의 receive/decision local 단조 시각·frame gap과 server estimate/presentation/deadline 판정 입력 전후를 연결해 local stall·clock 변경·혼합/미확정을 구분한다. TTL/지연/timeout·HP·표현 방출 조건은 바꾸지 않는다. 근거: NET-TIME-001~005·NET-PRESENT-003/004. ready 부분집합 성공과 Expired는 독립 판정이다.
5. **기존 진단/검증 흐름 보강:** GameLog와 기존 Development 진단·formatter/parser 경로를 재사용한다. 전이/최종 사건만 기록하고 중복 출력·매 프레임 스팸·새 게임 상태 writer를 추가하지 않는다. 실제 production formatter fixture로 사유·clock domain·필수 identity·UTF-8/incomplete/overflow·기존 타입 호환성을 확인한다. 근거: LogRules1.2·1.4·1.7·1.14. 신규 Task/TC/QA/검증기는 금지한다.
   - 메인의 명시적 bounded 요구에 따라 최초 사유/전이 중심으로 cap을 두며 cap/overflow·억제·unknown 표시와 수치를 최종 인계에서 확인한다. phase-observation domain과 새 C estimate는 관측 전용이다. Bind/TryConsume 원본 조건과 Torrent exact predicates를 그대로 유지한 diff를 확인해야 하며, 수집 때문에 조건을 재판정하거나 결정 순서를 바꾸지 않는다.
6. **새 자동 gate와 빌드 시도 — 메인 수행:** 구현 인계와 변경 범위를 확인하고 정적 컴파일 후 실제 `Hexiege/Combat/Run Unit Action Self Validation`, `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit`를 새로 실행한다. 실제 PASS·Console 오류/경고 근거를 각각 기록한 뒤에만 File→Build And Run1회 시도한다. 실패/예상 밖 modal은 기록하며 임의로 씬 저장이나 안전 조건 완화로 진행하지 않는다. 시작/선행 확인 화면만 관측한 뒤 모든 완료 감시를 중단한다. 완료·설치·runtime 성공 추정 금지.

**보존 범위:** 게임 로직·Stats/밸런스·씬/설정/클립/controller/prefab/VFX·공통 정책은 무변경이다. 이번 문서 담당 수정은 Research/Plan 두 파일뿐이며 메모리/상태 문서는 아직 추가 갱신하지 않는다. 기존 분석의 표현 FAIL·clock/정확 timing·visual/전체 OPEN과 미확정 인과는 유지한다. 구체 필드 인계와 실제 실행 결과는 이 후속 절에 통합한다.

- [x] 승인 후속 계획 기록.
- [x] programmer의 7파일·핵심 필드·bounded 최초 사유/전이 중심 중간 범위 수신/계획 반영(최종 수치/필드 검증은 대기).
- [ ] 로그만 구현 및 최종 diff/기존 formatter 검증 근거 수신.
- [ ] 새 실제 Unity 두 메뉴 PASS·Console 결과 확인.
- [ ] Build And Run 시작 시도 사실 기록(완료 감시 없음).
- [ ] 새 사용자 경기 로그로 가설 구분 및 visual 수용 확인.

### 최종 source·자동 검증 인계 (2026-10-08)

앞선 중간 단계 표기는 이력이다. Research의 후속 구현·실제 자동 gate 결과 절에 실패→refresh→새 실행의 단일 상세를 보존했다. 진단7파일 source freeze 인계와 실제 Unit Action12:52:08(A1/C2/C3/production) PASS, RootSelfValidate12:53:24 PASS를 확인했다. Unity 생성 fixture32 모두ok·독립 CLI cases32/failed0/exit0이다. Console errors0/warnings27=재compile20+validation7이며 warning0이 아니다.

최종 정적 compile verified: Runtime0errors/20warnings·Editor0/0·Development0/20·Release0/24, 모두exit0. baseline8 원문 압축length/CRC 검증 후 변경7파일 self-diff에서 lease 본문 exact 동일·UnitView 진단 제거 시 기존 코드 동일·Wave 로그 제외 시 판정/state 순서 동일·observer 새C/formatter 제외 시 기존A/B 포함 동일을 확인했다는 programmer 최종 인계를 받았다. 메인 코드 지점 review 및 씬 hash 동일 근거와 함께 보존한다. source동결 후 추가편집0이며 정적/source 보존 검증을 runtime 원인/수정효과 PASS로 치환하지 않는다.

12:48:10 ActualCLI lease-Host.txt 누락 FAIL은 삭제하지 않는다. 저장 generator와 Unity 생성물이 불일치했던 상태에서 Ctrl+R compilation 요청→실제 CompilingScripts→reload 종료 errors0/warnings20→새 menu32 생성/PASS 순으로 해소됐다. 로드본이 오래됐다는 의심은 최종 원인 확정과 구분한다.

**Diagnostic ready / runtime OPEN / 게임 수정 효과 미검증.** 이번 검증은 로그 준비와 자동 fixture 근거이지 source 누락·C3 만료·Torrent 거부 원인의 확정이나 gameplay fix PASS가 아니다. Game SHA `A5310D1545A0080AFB8D5149EAAC95E0B1D2649318D0F9F973CC206D5BB42966`은 시작과 동일하다. Stats/Matrix/규칙·밸런스·에셋은 불변이다. 기존 체크리스트의 구현/새 gate/빌드 대기는 이 최종 인계 근거로 대체한다.

메인이 File→Build And Run1회 실제 클릭 직후 Building Player / Backup & unload 화면을 관측했다. 저장/추가 확인창은 없었다. 이후 모든 Unity/build UI·로그 polling을 중단했으며 완료·성공·설치·실행 판정은 불가하다. 감사01은 로그 준비·새 gate·이번 시작 시도 근거, 감사02는 새 runtime의 logging hypotheses 구분이다. 01 준비 완료를 02 원인/수정/visual PASS로 확대하지 않는다. 이전 중간 대기/실패는 당시 이력으로 보존한다.

이번 후속 전체 manifest는 Research에 있는 source7파일 + Task2문서 + 상태3문서 + 공용/logging메모리이며 baseline은 도구 증가 갱신만 허용한다. `Temp/pistoleer-timing-fixtures/` 생성물/분석기 결과는 검증물로 분리한다. 최종 필드별 인과 검증·사용자 새 경기/visual 수용은 OPEN이고 빌드 완료 감시는 금지한다.

**다음 분석의 필수 분류 제한:** 정상 provisional/Suppressed scope0의 NativeLeaseUnavailable invalid context도 omitted++→global completeness unknown이 될 수 있다. Research에 read-only source 경로를 보존했다. normal provisional/NormalNone·global unknown·actual NativeOccurrenceRejected를 분리하고 unknown을 실제 gameplay/source gate failure 수로 집계하지 않는다. valid exact-scope snapshot/predicate/clock 진단은 가능하지만 전수PASS는 금지한다. 현재 자동PASS는 기존assertions만 검증했으며 이 분류 한계의 해소는 미검증이다. 필요 시 추후 fixture/코드 검토 대상으로 남기고 지금 source 편집·추가 rebuild는 하지 않는다. 진단 준비/실행 범위 완료와 runtime 전체OPEN을 구분한다.

## 새 사용자 경기 b41f 최종 분석 반영 (2026-10-08, source/timing/pixel OPEN)

**[최종 Sagan 인계 반영]** 전체 source 분류 기록 완료, runtime/source/timing/pixel OPEN. 양 peer191scopes/183results에서 result-present source-absent는 Client32(미소비 이전 lease revision 교체23+공격자 사망9), Host6(first-close despawn)이다. Client23은 이전 remaining1/consumed0 lease close→새 owner bind→닫힌 이전 expected loop와 일치하는 callback23/23→새 owner WrongAbsolute 거부의 연쇄다. **새 owner19는 이후 정상 Marker/VFX를 기록했으므로 23새 owner 영구 손실로 판정하지 않는다.** representative old seq13 누락/새 seq14 회복 및 raw anchor는 Research에 보존했다. clock/rate/RPC 상대 기여와 deeper root cause는 미확정이다.

omitted Host474/Client476=각 NativeLeaseUnscoped count, emission16each bounded 및 scope0 unbound11each는 정상 provisional/global unknown 가능성 때문에 실제 source 실패에 더하지 않는다. 초기 parser missing23/rejected34와 Client result-present 누락32는 다른 집계다. 다음 제안은 continuous native epoch의 정확한 이전 occurrence ownership 보존/라우팅 검토만이다. exact loop/key 완화·free VFX·replay·gameplay clock/HP/writer 변경이나 구현/빌드는 없다. 이번 분석 기록 완료를 gameplay fix/전체 PASS로 확대하지 않는다.

추가 raw 분류: Client rejected34=valid-scope WrongAbsolute23+scope0 unbound11; Host11은 scope0 unbound만/WrongAbsolute0. 양 peer 각각 bounded invalid-lease unknown16은 errors가 아니고 PhaseHold/Resume29 reason=phase-ahead-or-rate는 raw clock regression이 아니다. C3failures0으로 새 만료 원인정보는 없다. NCC 예정 loop 계산과 policy exact nativeLoop 연결을 메인이 확인했다. lease 연쇄 분류는 완료했지만 clock/rate/RPC deeper 기여는 미확정이다. 실제 이전23scope 손실과 scope0/unknown을 분리하고 source-expression defect/timing/pixel OPEN을 유지한다.

Research의 새 경기 절이 session/key·원본 경로·raw anchor의 단일 상세다. Android Host / Editor Client b41f, wave epoch4685cdc632424817만 분리하고 embedded old 혼합 aggregate/global errors를 인용하지 않는다. Assault41/Torrent9/Bloom0 생산이며 Bloom은 미검증이다. canonical183 독립 keyed/payload join missing/extra/conflict0을 추가 확인했다. device355295는 새 epoch의 full-HP zero-heal 유효 anchor로 정정했다. 이번 새 경기 절은 최종 분류로 정합화했으며 이전 실제 경기·실패/실행 이력은 그대로 보존했다.

확정 범위: C2183/183 reject0, C3 양쪽 ready/released·expectedVisual/emits183 failure/timeout/spatial0, ROOT 양쪽 local PASS(Host12/Client11 endpoints), MOVE Host adapter/Fatal0. Torrent72 spawn/receive와 양쪽71 VFX는 wave72 정상 game-end expiry만 제외한 같은 집합이다. contact104 publish/receive/text 양쪽 exact-key missing/extra0, spawn/contact/clock reject·fault/overflow/failure0. writer233 중 damagechanged73+healchanged31만 publish104; full-HP zero-heal129는 모두 before==after==maxHP·delta0·contradictions0인 정상 nonpositive-delta 미발행이다. 이전 실패의 무재발을 fix로 선언하지 않는다.

source records382/191each, Marker/VFX177Host/151Client, Client parser missing23 및 terminal rejected34는 채널 결과·result-present source 누락32와 분리한다. 대표 Client instance1/seq14/revision15의 expectedLoop13↔callbackLoop12 거부는 이전 seq13 callback의 새 owner 거부이며 seq14는 이후 loop13 정상 표현을 기록했다. 승인0:05 설정 오류나 새 owner의 영구 손실이 아니다. normal provisional/NormalNone·global unknown·실제 거부는 독립 집계하고 missing/unknown을 정확한 실패 수로 바꾸지 않는다.

이번 분석 문서 반영은 완료했다. 후속은 continuous native epoch의 정확한 이전 occurrence ownership 보존/라우팅 검토 제안만이며 exact-loop/key 완화·free VFX·replay·gameplay clock/HP/writer 변경·구현·재빌드 승인이 아니다. 감사02의 채널 관측 성공, native source 결함 연쇄 및 timing/visual 수용은 각각 별도 축이다. 전체 runtime/source/timing/pixel OPEN, lease 분류 대기 없음. 이번 변경은 기존 Task/상태/관련 메모리 기록뿐이며 코드·에셋·씬·규칙·Stats·Matrix·Git·UI·빌드·새 Task/TC/QA 변경0이다.

## 사용자 승인 교정 접근과 최종 인계 (2026-10-08)

이전 분석의 proposal-only 기록 이후 사용자가 **기존 Research/Plan 갱신→코드 교정→새 실제 Unity 두 메뉴 PASS→Build And Run 시작 시도만**을 승인했고 이번 최종 인계를 받았다. 구현5파일 freeze·새 자동 gate/35fixture·이번 빌드 시작 시도는 확보됐으며 새 사용자 runtime/source/timing/pixel 수용은 OPEN이다. 이전 실제 경기/실패·자동 gate/빌드 시도 이력은 보존한다. 아래 접근과 최종 결과는 구분해서 읽는다.

1. **구체 5파일 구현 freeze:** Research의 최종 manifest가 단일 상세다. pending8owner/unit·64hits/owner 목록은 물리적으로 UnitView에 있으며 policy는 CanRetain/Matches/RetireBefore 순수 helper다. overflow9 old permission close/fail-closed·silent eviction0. UnitView 실제 handoff→emitter의 immutable old context·동일 Transform/muzzle, consumed-old cleanup의 semantic Retire 미발행과 latest count 독립 소비를 반영했다. NativeRoute 진단·parser·기존 production fixture35를 함께 보강했다. Torrent 예약·Bloom SeparateHeal 제외, server clock/NCC timer/HP·Stats·0:05 marker·cycle·에셋·씬 불변이다.
2. **정확한 pending 이전 소유권:** 정상 continuous same attacker instance/epoch/lifetime/clip/target의 새 revision 교체 시 old unconsumed exact lease만 보존한다. latest cursor는 새 scope로 단조 갱신하고 Animator/현재 상태를 되돌리지 않는다. actual callback epoch/lifetime/clip/loop/marker로 old owner를 정확히1회 소비한 뒤 불변 old target/facing/VFX·SFX·tracer context를 사용한다. 새 target/context 대입·free VFX·key/loop 완화·replay·FIFO fallback·gameplay clock/HP/writer 변경은 금지한다.
3. **취소·bounded 수명:** 실제 target switch/death/Stop/despawn/game-end·epoch/lifetime/clip 전환과 normal consume/occurrence-passed/baseline 처리는 기존 계약을 유지한다. exact epoch/lifetime/clip/state/loop/marker matching을 유지하고 clock/TTL/FIFO 완화는 없다. pending8owner/unit·64hits/owner의 8vs9 경계·명시 overflow/retire를 검증하며 silent eviction·임의 TTL/게임 규칙 변경을 금지한다. 메인 review는 취소되지 않은 정상 handoff/pending와 latest cursor 분리에 충돌을 식별하지 않았다. 실제 취소로 closed된 scope revive는 금지하며 이 계약을 침범하는 변경은 메인에 먼저 알리고 규칙 무단 편집 없이 멈춘다.
4. **실제 연쇄 RED→GREEN + 진단/parser 필수:** 기존 production fixture에서 old unconsumed→new bind→old callback 손실을 RED로 먼저 확인한다. 교정 뒤 old exact1회/new 자체 occurrence1회·불변 context·중복0을 GREEN으로 확인하고 원래 연쇄를 재실행한다. target switch/death/Stop/despawn·epoch/clip/lifetime 불일치·provisional·baseline·occurrence passed·consume 중복·capacity/overflow negative case를 기존 fixture에 보강한다. 기존 diagnostics와 parser 모두 pending preserve/route/consume/retire 및 old loss/new recovery·normal unknown 분리에 필요한 최소 내용을 보강한다. 새 Task/TC/QA/검증기 없음, 기존 진단 closure 전 유지. 실제 callsite 연쇄에 도달하지 못하면 seam 한계를 보고하고 얕은 fixture PASS로 대체하지 않는다.
5. **최종 source 인계 후 메인 검증:** 최종 diff·정적 컴파일·fixture/진단/parser 근거를 받은 다음 메인이 실제 `Hexiege/Combat/Run Unit Action Self Validation` 및 `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit`를 새 실행한다. 각각 실제 PASS와 Console errors/warnings를 기록하며 이전 PASS 재사용 금지. 두 gate PASS 뒤에만 File→Build And Run1회 시도하고 시작 화면 근거만 남긴다. 예상 밖 modal/실패는 그대로 보고하며 임의 씬 저장·gate 완화로 우회하지 않는다. 이후 Unity/build UI·로그 감시를 중단하고 완료·성공·설치·실행을 추정하지 않는다.

### 이번 실제 결과와 한계

Research 최종 인계 절에 이번 실행의 단일 상세·manifest를 보존한다. 첫17:58:24 ActualCLI route-Host.txt 누락 FAIL(disk generator35/loaded fixture32 불일치)→Ctrl+R 요청/fresh Compiling→reload errors0/warnings20→18:01:47 실제 Unit Action UAS-DIAG/ProductionTimeline/C2/C3 PASS→18:02:42 RootSelfValidate PASS 순서다. stale loader는 의심/원인 미확정이다. Unity 생성formatter/parser-report35 모두ok·독립CLI35/failed0/exit0, 최종 Console errors0/warnings27(20+7)이다.

계획의 RED→GREEN은 실제 old commit prepatch failing test를 먼저 실행한 엄밀 TDD로 달성한 것이 아니다. 수정 후 production-boundary full-close negative control로 old loss를 재현하고 GREEN 경로를 비교했다. native Animator/relay old/new 각각1회·cache999에도 oldtarget902·rotation snapshot·cap8/9·death/target cancel/baseline/exceptions를 통과한 emitter 호출 지점 근거다. manager 부재·particle/pixel 미검증을 명시하며 shallow/manual/private-value-only fixture PASS나 새 multiplayer 수용과 구분한다.

정적 Runtime17:55:10 errors0/warnings20·Development17:55:29 0/20·Release17:55:28 0/24·Editor17:56:04 0/0. original bytes/hash verify5true·freeze hash5true·메인 source review·asset6 baseline hashtrue·Game SHA 동일을 인계했다. stdout 한국어 mojibake는 원문 bytes 손상 증거가 아니다.

18:03:22 KST 메인 Build And Run1회 클릭 직후 Importing(Settings/UniversalRenderPipelineGlobalSettings.asset) 관측, save/confirmation0. 이후 Unity/build UI·로그 polling 중단. 완료·성공·설치·실행 판정 불가이며 이전 시도와 합치지 않는다. 구현/자동 경로 준비 및 빌드 시작 시도 범위 인계 완료, **새 실기 source/timing/pixel OPEN**이다. premature lease close seam의 수정 경로 준비가 전체 fix PASS나 clock/RPC 기여 확정은 아니다. normal scope0/global unknown 한계도 보존한다.

이번 문서 manifest는 Research/Plan·상태3·공용 MEMORY/programmer logging/QA patterns 및 도구 증가 baseline이다. 관련 상시 규칙/Stats/Matrix/GDD/TDD·에셋은 의미 변경 없어 미수정, 새 Task/TC/QA 없음. source5 및 Temp12/fixture35×2txt/report 검증물은 Research에서 별도 분류했다. 다음 수용 단계는 사용자 새 시험이다.

## 18_52 최종 실기 분석 후 현재 후속 — expiry/source/performance OPEN (2026-10-08)

사용자 새 session `2cdc5b05b0ecf5bdee466c8344831e6f8a07c8f22e7306cbb7879203ec4be3b5`의 Android Client run `7f96524bb09345feaba7378a768e064b` / Editor Host run `ff50f6e3d864469bb4f4d170310f3628`을 기존 교정 전 경기와 분리한다. 원본과 exact raw anchors/수치는 Research의 같은 제목 새 절이 단일 상세다.

- **확정:** C2 accepted3180/rejected0·transportfail0, C3 results3180/ready3176/failures4·ready 범위 expectedVisual/emits3037 정합. 유일 ERROR header raw568368은 coordinator-END 진단 FAIL이며 크래시 예외가 아니다. Expired4는 type1 Assault unit8/instance7 seq8·11·45와 unit5/instance6 seq26이다. Client ROOT raw568445 local PASS/errors0/events54는 이 gate와 독립이다.
- **관측과 추정 분리:** 3건의 receiveAfterLimitEstimate=true는 server estimate 기준이다. seq11은 Client 수신 후 local19frame/0.643435s와 anchoredJumpEstimate+0.560599s를 동반한다. 같은 peer local clock과 server estimate domain을 구분하고 서로 다른 peer monotonic 시간을 빼지 않는다. 네트워크 손실/단일 stall/clock/RPC 원인 확정이나 source fix 전체 성공으로 해석하지 않는다.
- **최종 인계 완료:** pending 정상 consume Host705/Client767(메인 device767/overflow0 독립 확인), 양측 최대2/overflow0. result-present marker-absent Host358/Client12는 occurrence-passed 포함 별도 source 누락이며 전부 이번 fix 회귀 원인으로 확정하지 않는다. C2 Host3180/failure0=Client3180/reject0, C3 Hostready3180/failure0와 Client3176/Expired4를 분리한다. Hostadapter/Fatal0·양peer local ROOT PASS는 formal 멀티/timing/pixel PASS가 아니다.
- **생산·성능 범위:** Assault47/BearGuard8/Bloom8, Torrent0. Bloom exact Start/HostPlay/ClientPlay63 missing/extra/target conflict0는 delivery 근거이며 heal timing/pixel 전체 수용은 아니다. PIST Host약486/s·Client약385/s, FPS/CPU/GC 미측정으로 렉 원인 단정 금지. 별도 crash/throw 증거 없음; expiry 전송/추정시계/대기 기여는 미확정이다.
- **제안-only 다음 단계:** 기존 production fixture의 ObserveResult/CompletedBundle→Tick anchor 연쇄 재현 검토와 source occurrence-passed 별도 분류. expiry 확대/clock clamp/lease 완화 근거 없음. Sagan 최종 분석 인계는 완료됐으며 구현 전 task 선택/구체 plan 승인 필요, 이 턴 승인·코드 fix·빌드 없음. expiry·performance·source/timing/pixel·전체 runtime OPEN, 정상 unscoped/global unknown은 실패 수와 분리한다.
- **현재 실행 범위:** 기존 Task2·상태3·공용/관련 logging·QA patterns 최소 기록과 check_docs/증가 baseline만. 상시 GDD/TDD·규칙·Stats/Matrix 관련성 검토 결과 값/정책 불변이라 미수정. 새 Task/TC/QA·source/assets/씬/빌드/Git/UI 변경0. 이전 실제 실패·gate·빌드 시작 이력은 보존하며 분석 문서 갱신은 사용자 standing 승인으로 재질문 없이 진행한다.

### Expired4 production 재현 승인 계획 (2026-10-08, 준비 단계)

네 만료 결과를 실제 생산 경로에서 다시 처리하고, 수신이 늦었는지·시간 앵커가 바뀌었는지·수신 뒤 대기가 길었는지를 한 요소씩 비교한다. 사용자 “ㄷ진행해그럼” 승인에 따라 기존 Task 안에서 진행한다. 앞 절의 제안-only/승인 없음/상태·메모리 갱신 범위는 이전 분석 단계 이력이다. 이번 우선 단계는 두 Task 문서에 계획 반영이며 재현 실행·원인 확정·source fix·전체 PASS가 아니다.

1. **입력/manifest 고정 — NET-TIME-001, NET-PRESENT-003:** 기존 2cdc 원본 로그의 Expired4 exact key(unit8/instance7/seq8·11·45, unit5/instance6/seq26, type1/hit0), Result/CompletedBundle payload·snapshot revision·ordinal·초기 baseline·객체 수명·순서와 clock domain을 trace에 보존한다. 누락 입력은 추정/합성으로 표시하고 실제 로그 재현과 synthetic 대조를 구분한다. raw 로그는 변경하지 않는다.
2. **실제 fixture 연쇄 — NET-TIME-002, NET-PRESENT-003/004:** 기존 `RunAuthoritativeAttackPresentationValidation.ValidateProductionAdapter`의 생산 초기화를 기준으로 Result/CompletedBundle→Bridge→TickCoordinator→Coordinator.Decided까지 통과시킨다. 개별 Result와 CompletedBundle 경로를 구별하고, Development/Release defines별 조건부 Result Bridge 입력·bundle 입력의 순서/횟수를 기록한다. 직접 coordinator 시각 대입만으로 production 연쇄 재현을 대체하지 않는다. production source는 이 단계에서 수정하지 않는다.
3. **한 변수 대조 — NET-TIME-001~003:** (a) 수신/result/local Tick을 고정하고 앵커 교체만 변경, (b) 앵커 궤적/수신 후 대기를 고정하고 수신 시점만 변경, (c) 수신/앵커 궤적을 고정하고 로컬 대기만 변경한다. 기준과 대조의 exact payload는 동일하게 유지한다. seq11의 19frame/0.643435s 및 +0.560599s anchor jump 추정은 시작 근거이지 인과 결론이 아니다. authoritative impact/due/limit, 수신 시 server estimate, 같은 peer local frame/시간, 교체 전후 anchor, Decided 사유·Ready/Expired·emitter 횟수를 비교한다. cross-peer local 차이를 RTT로 해석하지 않는다.
4. **안전 대조 — NET-TIME-003~005, NET-PRESENT-003/004:** 중복·순서 역전·retire/취소·late-join baseline을 기존 계약대로 처리하는지, 확정 결과의 single emitter 및 HP 변경0을 확인한다. 기존 지연/catch-up/버퍼/수명 정책 유지; expiry 확대·clock clamp·lease 완화·다음 marker 기다림·FIFO fallback 없음. Dev/Release 입력 차이는 재현 대조로 확인하기 전까지 원인 미확정이다.
5. **결과→Main review 경계:** 관측 재현과 synthetic 대조의 결과·반증·미확정 입력을 구분해 results/manifest로 반환한다. 원인 확정된 부분의 후보 source 파일 목록·최소 교정 계획을 Main이 검토한 뒤 다음 source 단계로 분리한다. 현 단계 source 변경/build 권한으로 확대하지 않는다. source occurrence-passed 별도 누락, FPS/CPU/GC 미측정 성능, formal 멀티/timing/pixel 수용은 이번 expiry 재현으로 닫지 않는다. 최종 문서/메모리 완료 인계도 별도 단계다.

**보존 계약 근거:** `GameSystemRules_UnitCombatSynchronization.md`의 NET-TIME-001 단일 서버 시간축, 002 지연 흡수 표현 시간축, 003 늦은 도착, 004 순서 역전, 005 늦은 참가/재접속, NET-PRESENT-003 피격 표현 상관관계, 004 타임아웃을 사용한다. 재현은 이 규칙의 검증이며 규칙/정책 변경이 아니다.

**단계별 파일 manifest (생성 예정과 실제 변경 분리):**

| 단계 | 파일 | 현재 상태 |
|---|---|---|
| 이번 계획 반영 | 기존 Task `Research.md`, `Plan.md` | 승인 계획 append; 기존 기록 보존 |
| 후속 기계 재현 | `Temp/expired-replay/Replay.cs`, `trace.json`, `compile.rsp` | 예정, 이번 단계 미생성/미실행 |
| 후속 실행 산출물 | 같은 폴더의 실행 dll/runtimeconfig, `results.json`, `manifest.json` | 예정; 정확한 이름/defines/source·reference hash/실행 명령/결과를 manifest에 기록 |
| source 교정 후보 | 재현 결과에 따른 파일 manifest | 아직 미확정, Main review 전 수정 없음 |

이번 변경 허용 범위는 Task2뿐이며 새 Task/TC/QA·코드·Git·UI·build·assets·Stats·씬·규칙 변경0이다. Temp 산출물은 기계 검증 범위이며 Unity/Android 실기·pixel 수용 증거가 아니다. 준비 결과는 계획 반영과 문서 검사로 반환하고, 재현 실행/교정 결과를 선행 선언하지 않는다. expiry·source/timing/pixel·performance·전체 runtime OPEN을 유지한다.

### 수정 전 replay 인계와 최소 source 교정 계획 (2026-10-08, 미구현)

진단을 켰다는 이유로 실제 표현 시간이 달라지는 경로만 분리하는 최소 교정 계획이다. 앞 절의 미생성/미실행/후보 미확정은 준비 당시 상태다. Main의 수정 전 Development/Release replay 독립 실행 exit0 인계를 받았으며 이번 단계에서는 결과와 후보를 문서에만 반영한다. source fix는 아직 없고 Main review 후의 미래 단계다.

**수정 전 근거/반증:** `Temp/expired-replay/{manifest.json,results.json}`의 frozen unmodified production CLR Bridge→Tick 실행은 edition당19case/동일2반복이다. 정상 exact payload의 controlled unrelated-result-jump는 Dev Expired/Release Ready로 C2 diagnostic 입력의 실제 표현 시계 개입을 분리했다. stable-normal은 양쪽 Ready, genuine late/wait는 양쪽 Expired다. recorded endpoint4 reduction은 양쪽4Expired이며 no-other-anchor/immediateTick 대조도3Expired 유지·seq11 Pending(Ready 아님)이다. 네 실기 만료 원인 전체 확정/해결이나 전체 capture 재현으로 확대하지 않는다.

**미래 source 후보 manifest — 기존2파일만, 현재 수정0:**

| 후보 파일 (`Assets/_Project/Scripts/` 기준) | 최소 교정/검증 계획 | 유지할 근거 |
|---|---|---|
| `Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs` | Bridge `ObserveResult`에서 ResultPresentation의 diagnostic 결과가 실제 `_clockAnchor*`/`_hasClockAnchor`/`_lastCoordinatorTime`을 변경하지 않도록 분리; C2 결과 수락·Scheduler 분류 보존. 일반 PresentationShadow의 기존 anchor 및 coordinator 행동 보존. 완결 Bundle의 정규 표현 시간 입력 유지 | NET-TIME-001/002, NET-PRESENT-003/004; observer가 실제 표현 결정의 시간을 변경하지 않음 |
| `Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | 기존 `ValidateProductionAdapter`에 C2 diagnostic on/off 동일 anchor/clock+동일 Ready 결정 대조, 정상 Shadow anchor 행동 보존, genuine late Expired 기존0.5초 경계 회귀 추가 | NET-TIME-003~005, NET-PRESENT-003/004; 한도 완화·중복/과거 replay 없이 생산 연쇄 검증 |

1. **후보 review/교정 재현:** 위2파일의 최소 diff와 source manifest를 Main review 대상으로 제시한다. 후속 구현 시 controlled unrelated-result-jump의 on/off가 same clock/same Ready로 수렴하고 genuine late는 Expired인지를 다시 확인한다. C2 diagnostic 수락 자체를 끄거나 일반 PresentationShadow의 시계 행동을 바꾸는 방식으로 해결하지 않는다. completed bundle 입력·경기 초기 anchor는 유지하고 새 시계/HP writer를 만들지 않는다.
2. **한계/보류:** CLR replay는 Unity PlayerLoop/NGO/RPC accepted classifier/실제 emitter 미실행, synthetic 정상 non-time payload·companion, 중간 anchor/Tick 누락·반올림 anchor·역산 tickLocal을 포함한 partial endpoint reduction이다. 초기 harness mandatory Impact position 누락은 Temp 입력 보완 이력이며 gameplay RED가 아니다. duplicate/invalid-bundle Expired probe는 classifier 선행 때문에 실제 accepted RPC reach 미확정이라 추가 source 교정 보류. bounded clock history/Perf는 candidate 재현 뒤 필요성만 판단하며 신규 source GO 없음.
3. **실기 후속 경계:** 교정 이후 별도 새 capture의 기존 expiry A/B/C에서 `clockAnchorSource`가 diagnostic `result`로 갱신되지 않고 `completed-bundle`만 정규 입력으로 갱신되는지 확인한다(초기 begin-match와 구분). candidate fixture green은 이 실기 확인을 대체하지 않는다. late3·seq11 실제 인과/전송·시계·대기 상대 기여, 렉/FPS·CPU·GC, marker 누락/occurrence-passed, formal 멀티/timing/pixel은 OPEN이다.

**산출물 manifest와 단계 분리:** 실제 기존 `Temp/expired-replay/manifest.json`의 목록은 `Replay.cs`, `trace.json`, `compile.Development.rsp`, `compile.Release.rsp`, `Replay.Development.dll`, `Replay.Release.dll`, 각 edition `.runtimeconfig.json`, `results.json`, `manifest.json`이다. source/원본 로그/frozen assembly hash 및 CLR 한계는 그 manifest를 참조한다. 새 compile0/0은 harness만이며 production 새 컴파일/Unity build 완료가 아니다. Main 실행 exit0는 이번 문서 담당의 재실행 결과로 표시하지 않는다. 이번 실제 수정 manifest는 기존 Research.md/Plan.md append2개뿐, source 후보2개는 미구현이다. 이전 계획·실패·로그·검증 이력 보존; 신규 Task/TC/QA·Git/UI/build·규칙·Stats·씬·assets 변경 없음. NoClockClamp/timeout 확대 금지, 기존0.5초/NET-TIME-001~005·NET-PRESENT-003/004 및 HP 서버 권위 유지. 최종 상태/메모리 인계는 후속 단계로 분리한다.

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

#### 후보 source manifest와 근거별 구현 계획 — Main review 후 별도 GO

경로는 Assets/_Project/Scripts/ 기준이다. 아래는 미래 수정 후보이며 이번 문서 담당의 source 변경 목록이 아니다.

| 기존 파일 | 계획 책임 | 근거 |
|---|---|---|
| Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs | 기존 Bridge 처리의 숫자 event 및 Tick ticket/entry/exit, 원 callsite 값·anchor before/after·Decision status 관측만 발행. live clock/결정 변경 금지 | GameSystemRules_UnitCombatSynchronization.md NET-AUTH-001/002, NET-TIME-001/002, NET-PRESENT-003/004 |
| Infrastructure/Network/UnitAttackShadowObserver.cs | subscriber에서 fresh NGO와 local/frame validity를 결합, ring/frozen windows·arm/freeze·unknown counters 및 EndSession history formatter/preflight 소유. 기존 terminal 예산 보존 | 같은 문서 NET-TIME-001~005·NET-PRESENT-003/004; LogRules.md 1.7·1.11·1.14 |
| Infrastructure/Network/NetworkCombatController.cs | 기존 production 관측 경계·session lifecycle에서 subscriber 연결/해제와 reset을 배선. 기존 수신/HP 적용/clock 입력 순서 유지, 추가 조회는 진단 활성 경로에 한정 | 같은 문서 NET-AUTH-001/002·NET-TIME-001~005; LogRules.md 1.7·1.14 |
| Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs | 기존 production adapter fixture만 보강하여 비간섭·chronology·bounds/reset·Release·실제 formatter full-byte preflight 검증. 별도 validator 신설 없음 | 같은 문서 NET-AUTH-001/002·NET-TIME-001~005·NET-PRESENT-003/004; LogRules.md 1.7·1.11·1.14 |

1. **권위/비간섭 — NET-AUTH-001/002:** 진단 on/off와 subscriber absent/disabled를 같은 production 입력으로 비교해 HP 즉시 수렴, Scheduler 수락/분류, anchor/current time, Ready/Expired 결정 및 emitter 횟수가 동일해야 한다. 숫자 history로 수신/HP writer나 gameplay state를 지연·재판정하지 않는다.
2. **시간 영역 — NET-TIME-001/002:** local/frame·fresh NGO·원 callsite clock을 따로 저장하고 anchor 교체 전후를 event sequence와 ticket으로 연결한다. 값이 달라도 덮어쓰거나 clamp하지 않는다. 표현 지연/서버 시간 입력은 기존대로 유지한다.
3. **만료/순서/수명 — NET-TIME-003~005, NET-PRESENT-003/004:** genuine late/wait의 Expired와 기존0.5초 한도를 보존한다. 중복·역순·retire/cancel·과거 baseline 회귀에서 exact key/단일 emitter를 유지하고 다음 marker/FIFO로 넘기지 않는다. history bounds는 gameplay buffer/timeout 변경 근거가 아니다.
4. **출력 경로 — LogRules.md 1.7/1.11/1.14:** Development numeric history이며 매 Tick/frame 문자열 로깅 금지, EndSession 단일 소유 출력으로 제한한다. GameLog 개발 경로와 기존 sink/파일 기록을 사용하고 raw Debug.Log나 별도 파일 writer를 만들지 않는다. RuntimeLogger의 기존 파일 구현/되먹임 방어를 보존한다. 개발 호출은 UNITY_EDITOR/DEVELOPMENT_BUILD 조건부 제거를 지키며 식 본문 람다로 stripping을 우회하지 않는다. Release에서 arrays/subscriber/query/format 경로까지 제거하고 disabled early gate는 할당·조회보다 앞에 둔다.

#### 기존 fixture 보강 검증 계획 — 이번 실행 없음

- **비간섭:** production 초기화→Result/CompletedBundle→Bridge→Tick→Decision 연쇄에서 on/off equality, 기존 ResultPresentation diagnostic isolation과 일반 PresentationShadow 동작, genuine late/wait Expired, HP 불변·VFX hook one/zero·중복 표현 없음. hook은 particle/pixel 수용이 아니다.
- **chronology:** 같은 frame의 여러 결과와 anchor 변경, 네 Expired 결정에서 arm→같은 ticket TickExit→freeze 순서, event sequence/ticket의 구별과 before/after 값 보존. 이전/다음 TickExit로 동결하지 않으며 incomplete Tick/invalid sample은 unknown. fresh NGO와 원 callsite 값이 다를 때도 각각 보존.
- **bounds:** ring1024/다음 record wrap, frozen128/초과 truncation, frozen4/다섯 번째 saturation·suppression, record layout<=256bytes와 384KiB+overhead 구분, event/ticket/counter 포화·invalid/nonfinite 값 및 누락 경계 검증. silent eviction과 false completeness 금지.
- **reset/lifecycle:** EndSession 중복 출력 방지, session 시작/reset/종료·subscriber 연결/해제 후 이전 key/ticket/window/counter가 새 session에 섞이지 않음. 종료 전 crash/shutdown으로 dump 미확보는 별도 한계로 유지.
- **Release/disabled:** 기존 Dev/Release 검증 경로에서 추가 arrays 할당·NGO query·formatting이 Release 및 disabled에서 없어야 함을 확인할 계획. 조건부 호출 인자 평가/람다·subscription 잔존도 점검하며 이번 단계에서는 컴파일/실행하지 않는다.
- **formatter preflight:** 실제 production formatter와 logger 전체 prefix를 포함한 UTF-8 길이를 최악 숫자폭·최대 key·invalid/unknown/포화·다국어 prefix에서 측정한다. full line<1000bytes, window<=258lines, session<=1033lines/<=1033000bytes를 검증한다. 초과 시 bounded format-incomplete/suppression을 명시하고 fallback 자체도 동일 예산 안에서 검증한다. 기존 terminal budget은 별도 회귀로 유지한다.

**다음 gate:** Main 문서 review→별도 source GO→후보4 source 구현/기존 fixture 검증 인계→source 완료 뒤 별도 상태·메모리 단계. 이번 actual 변경 manifest는 기존 Research.md/Plan.md append2개뿐이다. 문서 검사0건은 상시 참조 정합성 결과이며 _Tasks/는 검사 범위 밖이므로 이 계획의 실제 동작/PASS를 보증하지 않는다.

## 2026-10-09 bounded clock history 최종 인계 — diagnostic readiness / runtime OPEN

승인 후보4 source 구현 완료. 과거 source 미구현/메뉴 대기는 당시 이력으로 보존한다. [Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md)의 “Clock-history 최종 파일 인벤토리 (2026-10-09)”에 source4 SHA256·문서8·기준값1·Temp38 전체 링크와 검증 로그를 모았다.

- **정정(원문 보존):** NCC=RPC entry ticket→accepted exact-key binding, observer=lifecycle. 출력=`GameLog.Dev.Warn` 기존 경로이며 별도 temporary RuntimeLogger writer 아님. budget=formatted GameLog envelope만; platform logcat prefix/stack/separator 제외.
- numeric ring1024/frozen128×4 및 동일 ticket TickExit freeze, generation/reset/unknown guards 구현. record200B, numeric307200B, Reset managed307560B, disabledReset0B. fixture1033lines/540898formatted bytes/max531/worst666. 실제 native query count/성능은 이 수치로 증명하지 않는다.
- static4 errors0(warnings Runtime20/Editor0/Dev20/Release24), Main CLR Replay/PolicyDev/PolicyRelease exit0. stale refs/초기 compile 오류와 synthetic550不足→650 보강은 Research/manifest에 보존. 현재 Temp/expired-replay 없음.
- Main 실제 Unity Action13:41:11→13:41:36 overall/C2/C3 PASS, Root13:44:44 PASS(2026-10-09); Console errors0/warnings27. Main 확인 frozen4 manifest hash 일치 및 protected Game/Login/Stats/Assault anim baseline 동일. 씬 저장/빌드 없음.
- **다음 gate:** 사용자 새 Development 빌드→경기 종료→로비 복귀→저장(EndSession dump). 강제 종료/EndSession 전 reset은 history 누락 가능. 실제 Expired/source-marker/PhaseClockRegression/실기기 오류/timing/pixel/performance/NGO native query count OPEN. source diagnostic gate를 실기 해결 PASS로 확대하지 않는다.

## Canonical Tick 적용 후 현재 실행 단계 (2026-10-10)

**15:52 최신 상태 — 아래 earlier pending을 supersede:** Main freeze check22항목 mismatch0/VFX unexpected0, 보호6＋Assault clip 불변/추가source변경0. fresh ROOT15:52:38 실제 PASS(UI/Editor.log 확인), Unit Action15:47:57 FAIL은 actual Tank=Cannon prefab/새 Human tower=Tank prefab 사용자 선택 대기다. 빌드 미시도. 다음은 사용자 선택→Main/programmer 승인 blocker 교정→fresh Unit Action→승인 build attempt 사실→사용자 완료/새 경기 수용이다. 대기 중 추가 구현·규칙/Tank/Stats 임의 수정 없음. 아래 pending 항목은 추가 인계 전 이력이며 Assault 전체 CLOSED/timing/runtime PASS로 승격하지 않는다.

- 아래5파일 proposal은 programmer 실제 적용·최종freeze 인계 단계로 진행됐다. 정확한 수정5개·SHA256·보호/Temp 증거 인벤토리는 Research의 “Canonical Tick 실제5소스 적용·최종freeze 인계”에 직접 기록했다. 문서 담당 current5/manifest5 hash 일치; Main 독립 재확인 결과 대기. 추가 소스·규칙·Stats·클립·씬 수정 없음.
- static4 errors0/exit0 (warnings20/0/20/24), Replay/Dev/Release CLR exit0 및 Main12:51 canonical fixture/bounds PASS. endpoint14 Ready/50 Expired는 missing earlier pending/lifecycle 가정 경계가 있다. all-retained33 Ready/31 Expired 추가19는 synthetic preseed로 **실제 추가 교정 증명 아님**. before-fix RED는 Research에 원문 경로/hash로 보존했다.
- actual provider/runtime acquisition/MonoBehaviour lifecycle는 CLR로 증명하지 않는다. invalid provider skip/pending 유지, inactive0/active1 query, generation rebind, diagnostic noninterference 및 true-late/취소/exact-key/0.10/0.50는 유지한다. 규칙 NET-TIME-001/002/003·NET-PRESENT-003/004 의도 불변으로 규칙 내용 수정 없음.
- **native 최신:** Unit Action15:47:57 FAIL은 Human tower 별preset의 actual Tank prefab 공유를 거부한 별도 VFX fixture blocker다. 실제 실패 경로=`Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs:33`(UnitAction 및 AuthoritativeAttackPresentation 호출). 원인 검토/교정은 Main/programmer 후속이며 이 Plan에서 추가 소스 범위를 임의 확대하지 않는다. 새 ROOT 결과 미수신, 빌드 미시도.
- **다음:** Main freeze/protected 재확인→별도 native blocker 원인/승인 교정 인계→fresh Unit Action 및 ROOT 결과→승인된 build attempt 사실→사용자 완료/설치/새 경기 수용. 완료 polling 없음. remaining50 원인/원64 재현/PhaseClockRegression 인과/timing/pixel/performance OPEN 유지.
- VFX44 연관 변경은 **Map＋existing fixture host2개**, 다른42불변이다(43불변 예상 정정). 보호6＋Assault Attack hash 일치 programmer 보고; Main 독립 proof pending을 완료로 추정하지 않는다. 기존 VFX 문서의44/static/native OPEN 및 unrelated Task 원문은 수정하지 않는다.

## 21_18 phase1/2 — 최소 canonical Tick 교정 계획 (2026-10-10)

### 근거와 조사 우선순위 (확정 원인 순위 아님)

1. **Live decision anchor freshness/update 경로:** window1의14건은 retained anchor와 fresh NGO 약0.537s 차이가 있고 fresh arithmetic상 in-bound다. 실제512 numeric 입력을 이용한 real BridgeCoordinator 제한 비교에서 마지막 Bundle canonical clock만 바꿨을 때14 Ready/50 Expired였다. stale Bundle epoch extrapolation과 현재 synchronized server time을 구분하는 최소 교정 근거이며 전체64 원인 확정은 아니다.
2. **수신 전 lateness / canonical clock 관계:** 나머지50은 fresh 기준 late, 첫3건은 recvAlreadyLate다. 원 failure tickets12581/20655/21016/25898 이전 RPC 미보존으로 authoritative publish/due/limit/RPC-entry의 완전한 원인 연쇄는 미확보다. network/lag/PhaseClockRegression 원인은 OPEN.
3. **수신 후 wait/Tick 순서:** 마지막 Bundle 이후 wait 제거만으로64 Expired가 유지됐다. post-receive 약166ms를 전체 원인으로 단정하지 않는다. 필요한 후속 capture/replay는 같은 payload·key·lifetime·순서를 유지하고 clock/wait 변수를 각각 분리한다.

4 windows3+14+28+19/19 UnitIds,128-row 제한 capture의 truncatedTrue/coverageUnknownTrue를 유지한다. actual512 replay의 pending/schedule은 missing earlier inputs 때문에 synthetic이다. retained contrast64 Expired, canonical-only14 Ready/50 Expired, wait-only64 Expired, 각2회/observer on-off 동일 및 baseline Replay/Dev/Release exit0는 보고 근거다. **원64 production 재현/새 native PASS/runtime 해결을 뜻하지 않는다.** 원문 로그 경로/run/session/raw anchors는 Research 최신절에 명시했다.

### 구현 검토용 정확한5파일 범위

| 기존 파일 (`Assets/_Project/Scripts/` 아래) | 최소 교정 계약 |
|---|---|
| `Presentation/Effects/UnitAttackResultPresentationShadow.cs` | `Func<double>` provider 주입. active Update에서 canonical clock1회 query 후 실제 `TickCoordinator` callsite에 explicit server/local 시간을 전달. inactive query0. |
| `Bootstrap/GameBootstrapper.Map.cs` | Initialize 전에 provider 연결, 기존 `_unitCombat.WaveServerTime` 재사용. network/listening/shutdown guard에서 solo fallback 금지. generation 재초기화 시 old provider 재사용 금지. |
| `Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs` | explicit synchronizedServerTime/local Tick 입력으로 live decision 현재 시간을 갱신. stale last-Bundle epoch extrapolation을 live decision fallback으로 사용하지 않음. 기존 result/completed-bundle 역할과 exact-key/lifetime 경계 유지. |
| `Infrastructure/Network/UnitAttackShadowObserver.cs` | bounded clock inputs/anchorDelta/invalid-skip 및 END 진단. 매 frame formatted log 금지; disabled 관찰이 gameplay decision을 바꾸지 않음. |
| `Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | 기존 fixture에14 counterexample/true-late50/invalid-provider/lifecycle/noninterference 회귀 연결. 실제 입력 subset와 synthetic setup/controls를 명확히 분리. |

NCC/Setup/HitPresentationQueue/UnitView 변경은 이 후보 범위에 없다. 새 Task/TC/validator 없음. NET-TIME-001/002/003 및 NET-PRESENT-003/004의 기존 의도를 바꾸지 않는다.0.10/0.50, true-late, exact keys, cancellation, single writer, HP·주기·범위·Assault0:05 및 VFX44 freeze 보존.

### 실행·회귀·인계 gate

1. Main이 이5파일 비설계 교정안을 검토한 후 기존 교정 승인 범위로 구현한다. 추가 사용자 질문을 prerequisite로 두지 않되 이 문서가 소스 완료를 뜻하지는 않는다. 미지원 원인에 대한 TTL/clamp 증가 금지.
2. provider fault/null/NaN/Infinity/negative는 invalid skip: anchor fallback 금지, pending 보존. inactive query0/active1, network/listening/shutdown guard, generation reinit 및 old provider 차단을 fixture/targeted bounded 로그로 확인한다.
3. 기존 fixture에서14 stale-clock counterexample, genuine late50/실제 wait expiry, result/bundle 순서, duplicate/inversion/retire/cancel/key/lifetime 및 observer on/off noninterference를 확인한다. 모든64 Ready를 목표로 만들지 않는다. 누락 입력은 unknown로 남기며 원64 재현으로 승격하지 않는다.
4. 추가 actual capture가 필요하면 bounded ticket/RPC/result/completed-bundle/Tick/decision 입력과 anchor before/after·fresh canonical·local/frame을 분리 보존한다. budget/truncation/coverageUnknown을 숨기지 않고 actual replay와 synthetic controls를 별도 집계한다.
5. 실제 구현 뒤 source freeze/inventory/hash 및 static 결과를 인계하고 fresh Unity Unit Action/Root native gate를 실행한다. 이전 PASS 재사용 금지. static/fixture/native PASS는 실기 clock/source/timing/pixel/performance 수용과 별개다.
6. 승인된 build attempt는 클릭/즉시 관찰 사실만 기록; 내부 완료/설치 및 새 경기 runtime 수용은 사용자 담당. UI/log/process build 결과 polling 없음. 새 경기 종료→로비→저장 EndSession 근거로 실제 실패/unknown을 재평가한다.

**현재 단계:** 문서 교정안 준비, Main review 인계. 이번 document-manager 작업은 기존2Task만 수정하며 production source/asset/build는 수행하지 않는다. 실제 구현/static/native/build/outcome은 후속 근거 수신 시 구분하여 기록한다.
