# VFX / SFX 에셋 목록

> **Human ONLY 최종 적용(Main 실제2파일 검토 완료):** Human 별도 preset의 _vfxPrefab ONLY → Assault(type1)의 실제 production VFX: EffectPreset_Pistoleer_Attack → vfx_pistoleer_attack, GUID badc7c96fdc06564fb9f9af71d1ddf68/root5594113295059828660. 기존 fixture는 Tank/Cannon=actual Tank, Assault/Human=actual Assault 및 Human 별도 preset을 검사한다. Editor static exit0/errors0/warnings0, 문서 담당 현재2해시도 인계와 일치. Assault 기존 preset·Tank/Cannon 참조 불변(Main/programmer); 다른 필드·VFX 실물·gameplay/Stats 변경 없음. 새 native/UI/build/Git 미실행·실기/pixel 미검증. Inferno는 사용자 clip/event 변경 보고만 유지하며 inspection/hash·Stats 동기화 검증 없음. burn fitting·Spirit endpoint·Tank/Cannon diagnostics deferred/미해결. 아래 대기/계획 및 이전 native는 단계별 이력이다.

> **최신 HumanTower ONLY 사용자 승인:** Human 별도 tower preset의 prefab 참조만 기존 Assault actual VFX로 교체한다. Tank/CannonCart는 Tank VFX 및 shared Cannon preset 유지. Human preset 이름/SFX/offset/scale/나머지 값·VFX 실물·피해/HP/주기/범위/Stats 불변. 실제 programmer 경로/identity 조회·Main 검토·구현 인계 대기, 새 정적/native PASS나 빌드 주장 없음. Inferno는 사용자 clip/event 변경 보고만 기록하며 확인/hash 조회하지 않는다; 새 값·Stats 동기화 미확인. burn recipient fitting·Spirit original-target endpoint·Tank/Cannon diagnostics는 보류/미해결. 이전 세대상 Tank 계약은 당시 이력이다.

> **21_43 최종 근거(Main/programmer):** Assault 양쪽56 Ready56/fail·reject·pending0, 이번 Expired0; clockInvalid1은 GameEnd transport closure. Client source52/result 교집합51·사망/despawn 종료 result-only5는 버그로 세지 않고 source-only1은 unresolved다. Inferno 실제 양 prefab/controller marker·Stats0.5/cycle3 확인, 문서1:15 모순 및 Start↔HP 정밀 측정 OPEN. source Host132/122/10·Client131/120/11/failure0/overflowTrue. burn Host Play·Drain·Return32/Client31, Pub40/Receive38는 merge 가능성으로 loss2 단정 금지. Tank/Cannon 누락 원인은 타입별 diagnostics 부재로 UNKNOWN. Tower Human22+Spirit8 exact 전달/play missing·extra0이나 dynamic target tracking/refit·pixel 수용 증거는 아니다. 제안은 미적용이며 현재 Tank 공유 계약 유지, 이전64/50 원인 OPEN. 상세는 기존00_19 및 Assault Task 최신 최종 인계.

> **2026-10-10 21_43 새 실기 분석 OPEN:** 사용자 Inferno 피해 조기 표시/burn recipient별 크기 문제, Tank·CannonCart 간헐 효과 누락 관찰. SpiritTower 원래 피격 이동 유닛 endpoint 추적 및 HumanTower Assault VFX는 미적용 후속 요청이며 현재 세대상 Tank asset 공유 계약은 유지한다. Main Editor 부분 END C3 56/56/56/fail0이나 clockInvalid1/coverageUnknownTrue, Inferno raw131/started120/suppressed11/overflowTrue로 전체 수용을 증명하지 않는다. disk marker0.5와 문서1:15 불일치의 실제 binding/runtime은 조사 중(live1.5 단정 금지). 기존 native PASS와 새 경기 timing/pixel·타입별 최종 상관관계 OPEN을 분리한다. 상세: 기존 `2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md` 및 Assault Research/Plan. 규칙·코드·에셋 변경 없음.

> **2026-10-10 최종 구현/native·빌드 시도 인계(Main):** UnitAction16:42:39 ALL terminal PASS(C2/C3 ProductionTimeline·baseUAS·VfxConnections 전 경계·Assault canonical current fixture), ROOT16:43:22 PASS, 동일 final fixture generation, Console errors0/warnings30. Tank/CannonCart/Human 실제 Tank identity 연결 및 두 fixture-only lifecycle/Stats 초기화 RED는 해소됐지만 날짜별 실패 이력은 보존한다. final fixture SHA256 `EB64059B75A20B83A5E4A16DD9A9423467B30F7AD2D3456995DBD7B3006B3E5B`, Cannon preset SHA256 `61A7A3F0F77525CE17050EF2648EEB4F361B39A012FC112B7A7F4B9374E4B8F1`. post-gates Main source-only11 Assault/protected 대조 불일치0·final2해시 exact match, 새 fixture 보완에 Production/Stats 변경 없음. **Build And Run16:43:57 KST(07:43:57 UTC) File 메뉴 클릭1회**, 즉시 Checking prerequisites/Starting Android build만 관찰하고 UI/log/process monitoring 전부 중단했다. 완료·설치·실행/runtime/visual 미확인, scene save/Git 없음. 이 native PASS는 기존 self-validation 범위다. original64/remaining50 원인·Assault 실기 timing/pixel·전체 migration은 OPEN이며 counterfactual14 Ready/50 Expired를 실기 해결로 쓰지 않는다. 아래 pending/RED/빌드 미시도는 당시 이력이다.

> **두 번째 fixture 보완 적용·Main 실제 소스 검토 완료:** actual config AutoTower/Spirit row로 temporary data 초기화·lethal preflight를 적용하고 finally 첫 단계에서 EXACT 기존 `_data` 참조를 복원한다. throwing subscriber/death/remove assertion은 유지, Production/Stats asset 변경 없음(Main/programmer 인계). 현재 Editor compile exit0/errors0/warnings0. 현행 fixture SHA256 `EB64059B75A20B83A5E4A16DD9A9423467B30F7AD2D3456995DBD7B3006B3E5B`, Cannon preset SHA256 `61A7A3F0F77525CE17050EF2648EEB4F361B39A012FC112B7A7F4B9374E4B8F1` 불변; 문서 담당 현재2해시도 일치. 이전93B11D…/1C196A…는 단계별 이력이다. fresh native 결과는 대기,16:32 RED→lifecycle 보완→16:37 tracer/impact/HP 경계 통과·후속 nullStats fixture FAIL→두 번째 보완 적용을 구분한다. ROOT16:33 PASS 별도·build 미시도·runtime/visual OPEN이며 전체 PASS로 쓰지 않는다.

> **최신 native — Main16:37:26 인계:** fresh UnitAction에서 이전 missing manager/tracer 실패는 해소됐고 실제 tracer·impact/HP assertions 통과, 해당 END failures0을 확인했다. 이후 VfxConnections line256 `Expression subscriber exception cannot prevent death/remove workflow` assertion에서 새 FAIL이다. tower fixture의 attack applied0/HP1(nullStats 컨텍스트)은 관찰값이며 원인 확정이나 production death/remove 결함 증명이 아니다. programmer read-only 진단 대기, build 미시도·runtime/visual OPEN.16:32 RED→fixture lifecycle 보완→16:37 이전 경계 통과/후속 경계 FAIL을 분리하고 전체 PASS로 쓰지 않는다. ROOT16:33:29 PASS는 독립 이전 결과다. 아래 기존 대기/실패 문단은 당시 이력이다.

> **16:32 RED 원인·최소 fixture 교정 적용(Main 실제6행 patch 검토):** EffectManager Instance는 private Awake에서만 등록되고 ExecuteInEditMode가 없다. fixture AddComponent/Initialize가 lifecycle을 누락했으며 tracer asset 참조 존재·event 전달은 확인됐다. 같은 existing VfxConnections 파일에 actual Awake 호출 + Require Instance==manager + tracer nonnull preflight를 적용했고 Main이 실제6행 patch를 검토했다. 최종 SHA256 `93B11DB06A043E3B86E8002BB1991F05016718F124BECABBA11AE22BC6AF9878`, Cannon preset SHA256 `61A7A3F0F77525CE17050EF2648EEB4F361B39A012FC112B7A7F4B9374E4B8F1` 불변. 기존 actual tracer-play/HP assertion은 약화하지 않고 production 소스/asset 변경은 없다. 현재 Editor compile exit0/errors0/warnings0; Runtime 불변·이전 static4 성공은 별도다. 교정 후 새 native 결과는 인계 대기이며 Action16:32:17 RED는 보존한다. ROOT16:33:29 PASS(Main UIstatus/Editor.log)와 source2 frozen/protection match는 별도, build 미시도·runtime/visual OPEN.

> **2026-10-10 최신 구현 인계·Main 실제2파일 검토 완료:** Tank·CannonCart·HumanTower 모두 기존 `vfx_tank_attack.prefab` actual root를 공유하도록 적용됐다. shared `EffectPreset_Cannon_Attack.asset`은 `_vfxPrefab`만 변경하고 이름·SFX·offset·나머지 값은 보존했다. 기존 VfxConnections fixture는 loaded Tank path의 세 identity 및 Human 별도 preset을 명시적으로 검사한다. 문서 담당 현재 SHA2562개도 인계값과 일치한다. programmer static4 exit0/errors0(warnings20/0/20/24), 이전 Replay/Dev/Release CLR3 exit0는 별도 근거다. Assault5·보호6·clip 불변은 Main/programmer 인계다. 새 UnitAction16:32:17은 세 Tank identity assertion을 통과한 뒤 ValidateWriterSnapshotVfx line219의 'Actual Unresolved writer event reaches actual tracer play, no committedSchedule dependency.'에서 FAIL이다. 이는 기존 Tank identity 실패가 아니며 Main/programmer read-only 원인 진단 대기, fresh ROOT16:33:29 PASS(Main UIstatus+Editor.log), build 미시도·visual OPEN이다. 이전 Action15:47:57 FAIL·ROOT15:52:38 PASS는 이력으로 보존한다. 기존 Tank 보호 해제는 이 prefab 참조에만 한정, gameplay·Stats·clip 및 VFX 실물은 불변.

현행 결정·구체 계획: [기존00_19 Plan](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md). 아래 날짜별 보호/선택 대기 서술은 이전 단계 이력이다.

> 완성된 에셋 현황과 제작 예정/보류 에셋을 관리하는 문서
> 프롬프트 작성 가이드 → [VFXSFXGuide.md](VFXSFXGuide.md)

---

## Ⅰ. VFX (시각 이펙트)

### 2026-10-10 현재 source freeze — 정적/native/수용 경계

**최신 판정 — 2026-10-10 09:53:38 정적 보고 / source freeze:** programmer 현재 snapshot44파일 freeze 인계 수신. Runtime/Editor/Development/Release 정적4경로 exit0 보고이며 native 실행 PASS가 아니다. Main SourceHash44 재확인 완료: Files44/Mismatches0/ChangedPaths=null(tool39f796), 현재 파일 SHA256 대조 불일치0. Main 보호6파일 baseline 일치 인계는 별도이며 Bloom clip baseline도 Main 확인됐다(원 JSON의 baseline unavailable 표기보다 후속 인계가 우선). **Action/ROOT 두 메뉴 미실행(UI activation2 timeout)·빌드 미시도·설치/실기/visual 수용 미확인·전체 OPEN.** 기존 메뉴 확장은 UnitAction 및 AuthoritativeAttackPresentation에 연결됐지만 fixture는 실행 전이고 Root 기존 gate 자체는 변경하지 않았다. 이전 승인·수정 중·static 대기 절은 당시 이력이다.

**구현 보고:** writer-owned immutable AttackEffectPresentation이 Mushroom remaining-flight tracer/due impact를 함께 소유하고 local marker의 Legacy HP 신호는 유지한다. tracer callback은 표현 반환 only, observer-clock/committedSchedule/Supported 의존 없음. 실제 Damage 단일 record의 provenance·attack identity/current value→recipient presenter, refresh noRestart·overwrite 종료교체와 natural/forced tail 분리를 연결했다. 타워 actual writer snapshot은 source/Impact/time/Race·appliedDamage/resultHp를 보존하고 subscriber 예외를 HP/death/remove에서 격리했다. Human 별도 preset/Tank 공유·Spirit RuneSpire·Transcendence 미지정/no fallback, 서버 즉시 피해·주기·범위·actual DoT3초 유지다.

**Asset tuning 보고:** impact PS 모두 nonloop/StopAction None. Poison_Ring duration0.2/lifetimeMax0.45, Spore_Burst0.2/0.55, Poison_Cloud0.15/0.65, root0.15/0.55(emission=false), Toxic_Bubbles0.2/0.55(초). 최대 초기 정상 tail 약0.8초 설정이며 실제 native drain/visual 종료시간 수용은 아니다. 3초 지면 damage-zone 표현은 현행 사양이 아니다.

**Live-clock/진단:** missing tracer/manager·clock deadline도 실패 counters, bounded key cap은 add 전에 적용; detail cap과 분리된 local-queue aggregate END가 failure/overflow/pending이면 unknown을 남긴다. Combat/Network recoverable Warn, match 초기화 때 detail budget reset. 이는 구현 보고이고 모든 실패조건 자동 assertion PASS가 아니다.

**검증 준비 ≠ 실행:** UnitAction/AuthoritativeAttackPresentation에 race/preset·marker/cycle/Stats·same Damage refresh/overwrite·stale Stop/duplicate·Host current/late restore/shutdown·PS start/live-particle 조기반환 거부/once-only return·actual Mushroom writer callback/immutable impact·Tower appliedDamage/resultHp 예외격리 fixtures가 준비됐다. natural simulation particles0 뒤 StopEmitting 재적용은 test-only state reassert다. **미완 assertion:** full dedupe/current/tombstone/identity capacity; future/deadline/NaN terminal counting; Legacy/Shadow/ResultPresentation mode matrix; forced Disable/Destroy classification; full wire round-trip/actual multiplayer. Root gate는 변경없음·실행안함. 기존 C3 FAIL64 fix/전체 migration/visual PASS 아님.

**Static 한계:** programmer command `Temp/expiry-clock-history/Validate.ps1`의 보고이며 네 exit0을 문서 담당이 재실행한 것은 아니다. Unity Bee/Editor dependency rsp·define 전환 Dev/Release는 actual Player/native build가 아니다. warnings0 주장 없음. 실제 compiler artefact 경로/한계·manifest44 전체 경로/hash 및 보호6은 [Mushroom Research](../_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Research.md), [Plan](../_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Plan.md), [Inferno/tower Research](../_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md), [Plan](../_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md)에 보존한다. Temp/source-freeze.json만의 임시 링크에 의존하지 않는다.

### 2026-10-10 확정 연결 계약과 진행 단계

**재개 중간 근거 — 2026-10-10 Main 인계:** 사용량 초기화 후 submission `01a1233b-895e-7530-aade-f82827243652`로 재개했다. programmer 09:36 소스 수정 및 09:42 `RunUnitActionSelfValidation.cs` 수정 진행을 Main이 확인했다. 이는 최종 구현 freeze·전체 inventory·정적 결과 인계가 아니다. native UI는 `sky.list_apps/list_windows`에서 Unity window67396 확인, 최초 `activate_window` timeout 후 returned window 재선택·`get_window` 복구 및 재시도도 timeout이었다. Main은 computer-use 복구 규정에 따라 native 입력을 중단했다. **이번 두 메뉴 미실행 / 빌드 미시도 / 완료·설치·runtime 미확인**이며, 게임 소스 결함이나 native gate FAIL로 분류하지 않는다. 최종 구현·static 결과는 programmer freeze 회신 후 별도 기록한다.

**현재 단계:** Task4 초안·Main 기술 검토 완료, programmer 구현 허가 전달·구현 진행 중(사용자/Main 인계). 실제 source/asset 변경 inventory, 구현 완료, 새 정적/native gate, 이번 빌드 시도 및 runtime/visual 수용 결과는 아직 미인계다. 아래 10-09 제작 실물/GUID 참조0·미연결은 당시 검사 이력이며 현재 구현 완료나 PASS의 증거가 아니다.

- Mushroom: 기존 3초 clip/cycle·Stats/UnitStats 불변, 1초 `OnAttackHit` 이벤트 1개 추가. tracer callback 종료 only; 같은 attack exact identity의 impact 단일 재생. impact는 짧은 포자 확산 burst/fade·StopAction None이며 3초 독구름·지면 damage-zone 표현은 현행 사양이 아니다. 서버 DoT 3초·피해·틱·범위는 유지한다.
- Recipient poison/burn: 실제 writer `(target lifetime, TimedEffectKind.Damage)` 단일 레코드에 identity/revision/provenance를 연결한다. 동일 owner refresh noRestart; poison↔burn/다른 Damage overwrite는 기존 정책대로 이전 표현 종료·교체, 독립 stack 금지. network current value·late-init/restoration/shutdown을 single presenter로 연결하고 취소·만료·사망 후 StopEmitting/drain/lease 반환과 강제 teardown을 분리한다.
- Tower: 단일 AutoTower의 Type+Race 키. Human은 기존 Tank VFX prefab을 별도 tower preset으로 재사용(shared Cannon preset의 prefab 참조만 승인 변경·구현/Main 검토 완료, 이름/SFX/offset·다른 필드 불변; 새 native/build/visual 대기), Spirit은 RuneSpire attack, Transcendence는 미지정/no other-race fallback. TowerCombatUseCase 확정 결과 identity·Impact 위치/시각·Race snapshot → GameEvents/NetworkCombatController 표현 전달 보강. current target lookup/tracer 도착으로 피해를 생성하거나 지연하지 않는다.
- 기존 HP writer·주기·범위·30fps 권위와 Unresolved/Legacy 유지, C3 Supported 승격 없음. 기존 자동 검증 확장 결과와 빌드 즉시 관찰은 Main 인계 후 기록하며 build/UI/log/process 결과 polling은 하지 않는다. 기존 Bloom 실기·native PASS 및 Inferno source focused PASS는 새 연결/visual PASS가 아니다.

**승인 Task:** [Mushroom Research](../_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Research.md) / [Plan](../_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Plan.md), [Inferno/tower Research](../_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md) / [Plan](../_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md). Task freeze 해제는 이 두 작업에만 한정한다. 새 Task 시간은 서울 2026-10-10 00:19 실측이며 다른 Task 원문은 보존한다.

### 1. 제작 및 수용 상태

> 프리팹 실물은 존재한다. **제작됨 / 게임 연결됨 / 자동 검증 / 실기·visual 수용**은 별도 상태이며, 제작 완료를 게임 전체 PASS로 취급하지 않는다. 아래 목록의 행별 상태를 따른다.
> **2026-10-09 최신:** Bloom recipient HoT VFX는 코드·설정 연결과 native self-validation 후 Match5638580898903259950의 실제 양 peer playback/종료 로그를 확보했다. Publish/Receive 차이는 같은 frame revision 전환, Client Play 차이는 수신 후 LocalDeath 취소로 구분됐다. 강제 cleanup을 자연 종료로 확대하지 않으며 Client C3 failures64 FAIL·전체/visual 수용 OPEN을 유지한다. 신규 Mushroom3·Inferno burn·RuneSpire attack은 실물 제작됨·GUID 참조 미검출·게임 미연결로 구분한다. 이전 시각별 인계는 아래 이력이며 최신은 이 요약과 「Bloom HoT 최신 관측 근거」가 우선한다.

---

### 2. VFX 목록 — 제작·연결 상태 및 제작 예정

#### 유닛 공격 이펙트

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Pistoleer | `vfx_pistoleer_attack.prefab` | 권총 발사 |
| Human | Assault | `vfx_assault_attack.prefab` | 돌격소총 연사 |
| Human | Sniper | `vfx_sniper_attack.prefab` | 저격 발사 |
| Human | LittleKnight | `vfx_littleknight_attack.prefab` | 검 베기 |
| Human | SpearMan | `vfx_spearman_attack.prefab` | 창 찌르기 |
| Human | BattleAxe | `vfx_battleaxe_attack.prefab` | 도끼 내려치기 |
| Human | Tank | `vfx_tank_attack.prefab` | shared Cannon preset 실제 prefab 참조 적용·Main 검토 완료; UnitAction16:42:39 전 경계 PASS·ROOT16:43:22 PASS·errors0/warnings30; 이전 fixture RED 이력 보존, build16:43:57 1회 시작 관찰·완료/visual OPEN |
| Human | CannonCart | `vfx_tank_attack.prefab` | Tank와 실제 prefab 공유 유지; HumanTower 최신 승인 참조는 별도 Assault 교체 예정. 기존16:42 Action·16:43 ROOT PASS는 당시 이력; 간헐 효과 누락 원인·visual OPEN/diagnostics 보류. 기존 `vfx_cannoncart_attack.prefab` 제작/존재 이력 보존·현재 미선택 |
| Spirit | EmberSpirit | `vfx_emberspirit_attack.prefab` | 불 오브 발사 |
| Spirit | FlameSpirit | `vfx_flamespirit_attack.prefab` | 화염 방출 |
| Spirit | InfernoSpirit | `vfx_infernospirit_attack.prefab` | 인페르노 폭발 |
| Spirit | DustSpirit | `vfx_dustspirit_attack.prefab` | 흙먼지 공격 |
| Spirit | BoulderSpirit | `vfx_boulderspirit_attack.prefab` | 바위 투척 |
| Spirit | QuakeSpirit | `vfx_quakespirit_attack.prefab` | 지진 광역 |
| Spirit | TideSpirit | `vfx_tidespirit_attack.prefab` | 물 튀김 |
| Spirit | StreamSpirit | `vfx_streamspirit_attack.prefab` | 물줄기 |
| Spirit | TorrentSpirit | `vfx_torrentspirit_attack.prefab` | 급류 |
| Transcendence | FoxMagician | `vfx_foxmagician_attack.prefab` | 마법 섬광 |
| Transcendence | BearGuard | `vfx_bearguard_attack.prefab` | 강타 |
| Transcendence | LionKnight | `vfx_lionknight_attack.prefab` | 발톱 베기 |
| Transcendence | RhinoBreaker | `vfx_rhinobreaker_attack.prefab` | 돌진 충격파 |
| Transcendence | EagleArcher | `vfx_eaglearcher_attack.prefab` | 화살 발사 |
| Transcendence | RabbitTrickster | `vfx_rabbittrickster_attack.prefab` | 단검 베기 |
| Transcendence | MushroomBomber | `vfx_mushroombomber_tracer.prefab` | **프리팹 제작 확인(10-09) · 연결 source/config freeze·Main hash 일치(10-10)** — 비행 동안 루프 재생하는 포자 폭탄. 실제 발사·착탄 연동 및 실기 수용 미검증 |
| Transcendence | MushroomBomber | `vfx_mushroombomber_impact.prefab` | **프리팹 제작 실물 확인(2026-10-09) · 10-10 교정/연결 source freeze·Main hash 일치** — 짧은 포자 확산 burst/fade·StopAction None 계약. 과거 독구름 표현은 superseded; 3초 지면 damage zone 없음. actual DoT 3초 유지. 최종 연결/native/실기 수용 대기 |

#### 유닛 사망 이펙트

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Pistoleer | `vfx_pistoleer_death.prefab` | |
| Human | Assault | `vfx_assault_death.prefab` | |
| Human | Sniper | `vfx_sniper_death.prefab` | |
| Human | LittleKnight | `vfx_littleknight_death.prefab` | |
| Human | SpearMan | `vfx_spearman_death.prefab` | |
| Human | BattleAxe | `vfx_battleaxe_death.prefab` | |
| Human | Tank | `vfx_tank_death.prefab` | |
| Human | CannonCart | `vfx_cannoncart_death.prefab` | |
| Spirit | EmberSpirit | `vfx_emberspirit_death.prefab` | |
| Spirit | FlameSpirit | `vfx_flamespirit_death.prefab` | |
| Spirit | InfernoSpirit | `vfx_infernospirit_death.prefab` | |
| Spirit | DustSpirit | `vfx_dustspirit_death.prefab` | |
| Spirit | BoulderSpirit | `vfx_boulderspirit_death.prefab` | |
| Spirit | QuakeSpirit | `vfx_quakespirit_death.prefab` | |
| Spirit | TideSpirit | `vfx_tidespirit_death.prefab` | |
| Spirit | StreamSpirit | `vfx_streamspirit_death.prefab` | |
| Spirit | TorrentSpirit | `vfx_torrentspirit_death.prefab` | |
| Transcendence | FoxMagician | `vfx_foxmagician_death.prefab` | |
| Transcendence | BearGuard | `vfx_bearguard_death.prefab` | |
| Transcendence | LionKnight | `vfx_lionknight_death.prefab` | |
| Transcendence | RhinoBreaker | `vfx_rhinobreaker_death.prefab` | |
| Transcendence | EagleArcher | `vfx_eaglearcher_death.prefab` | |
| Transcendence | RabbitTrickster | `vfx_rabbittrickster_death.prefab` | |
| Transcendence | MushroomBomber | `vfx_mushroombomber_death.prefab` | |
| Transcendence | BloomFairy | `vfx_bloomfairy_death.prefab` | |

#### 유닛 특수 효과 이펙트

> BloomFairy 치료는 아군 1명에게 거는 **지속 회복(HoT)**이다 — 규칙: [GameSystemRules_Units.md](../GameSystemRules/GameSystemRules_Units.md) **규칙 34**.
> 공격 순간 1회 재생되는 「유닛 공격 이펙트」와 달리 **치료받는 아군에 붙어 회복이 이어지는 동안 루프 재생**되므로 이 절에 따로 둔다.
> 꽃요정의 `attackPreset` 칸에 연결하지 않는다 — 그 칸은 치료하는 꽃요정 자신의 위치에서 1회 재생한다.

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Spirit | InfernoSpirit | `vfx_infernospirit_burn.prefab` | **프리팹 제작 확인(10-09) · 연결 source/config freeze·Main hash 일치(10-10)** — burn 루프 표현. 기존 attack VFX와 별개이며 DoT 부여·refresh·종료 연동 및 실기 수용 미검증 |
| Transcendence | MushroomBomber | `vfx_mushroombomber_poison.prefab` | **프리팹 제작 확인(10-09) · 연결 source/config freeze·Main hash 일치(10-10)** — DoT 대상 적 유닛을 따라가는 루프 표현. 부여·refresh·종료 신호 및 same-owner refresh noRestart·overwrite 종료교체·drain/lease 연결 검증은 대기; 건물 DoT를 신설하지 않음 |
| Transcendence | BloomFairy | `vfx_bloomfairy_heal.prefab` | **프리팹·연동코드/설정 구현됨 · native Action/Root self-validation PASS · 실제 양 peer 로그 확보 / 전체·visual 수용 OPEN** — recipient all5PS loop·동일 HoT refresh no restart·실제 종료 StopEmitting/natural drain/lease 연결. Client NotStarted는 수신 후 LocalDeath 취소로 확인(미수신·MissingAsset 아님). 강제 cleanup은 자연 종료로 확대하지 않으며 별도 Client C3 failures64 FAIL 원인 분석 중. 빌드 시도1의 JDK preparation 관찰은 완료·성공·설치 확인이 아님 |

#### Bloom HoT 최신 관측 근거

※ 근거 구분: **Match5638580898903259950**, Editor Host / Device Client. 다음 집계·exact-key 분류는 Main/programmer 확정 인계다. 문서 담당은 아래 두 파일에서 해당 MatchGeneration의 Host/Client 기록 존재를 직접 확인했다(집계 재분석은 하지 않음). 아래 Editor/Device 번호는 각각 이 파일의 행 anchor다.

- Editor: [RuntimeLog.txt](../_Logs/_editor/2026-10-09/RuntimeLog.txt) — `Assets/_Project/Docs/_Logs/_editor/2026-10-09/RuntimeLog.txt`.
- Device: [RuntimeLog_device.txt](../_Logs/2026-10-09/21_18_logcat/RuntimeLog_device.txt) — `Assets/_Project/Docs/_Logs/2026-10-09/21_18_logcat/RuntimeLog_device.txt`.
- **빌드 관찰과 테스트 실행 분리:** 이전 Main Build And Run1회는 JDK preparation 관찰 후 polling을 중단했으며 내부 완료·성공·설치 확인은 없었다. 이후 이번 사용자 테스트는 위 양 peer 실제 runtime 로그로 실행 증거가 확보됐다. 테스트 실행을 빌드 내부 완료 관찰로 소급 변경하지 않는다.

| 관측 | Host | Client |
|------|------|--------|
| details | 173 | 112 |
| Grant / Publish / Receive | Grant19 / Publish33 | Receive32 |
| Play / refresh | Play19 / refresh3 | Play18; refresh 수 추가 인계 대기 |
| StopEmitting / Drain / Return | 19 / 18 / 18 | 18 / 17 / 17 |
| 별도 종료 경계 | TeardownClear1 | NotStarted1 / LocalDeath instance8 target17 |
| Drop / NotStarted (이미 Play 후 terminal 경계) | 8 / 8 | 해당 Host 집계와 분리 |
| DetailsSuppressed / Overflow / DiagnosticFaults / PendingTails | 각0 | 각0 |

- **Publish33 / Receive32:** 차이1건은 life5/instance6/target3/rev1 `ActiveTrue`. Host가 같은 frame에 rev2를 발행했고 양쪽 rev2 Play를 확인했다(Editor13371/13425, Device101840). 이 current-state revision 전환을 최종 owner 재생 누락 또는 원인 불명의 패킷 손실로 합산하지 않는다.
- **Host Play19 / Client Play18:** life6/instance8/target17/rev1은 Client 수신 후 LocalDeath로 NotStarted가 됐다(Editor19381, Device140069/141392). 수신 누락·MissingAsset이 아니다. 해당 종료 취소를 visual 전수 PASS로 바꾸지도 않는다.
- **Host Drop8 / NotStarted8:** 대상 TargetId0/10/13/17/22/23/27/36은 모두 앞서 Play됐다. RecipientDespawn 뒤 TargetUnavailable terminal의 `BindingRejectedChannelAccept + StopBeforeReady`이며 grant 누락이 아니다(Editor19688/19803). 이 목록은 EffectInstanceId가 아니라 TargetId다.
- **Drain/Return Host18 / Client17:** 마지막 lease는 양쪽 `TeardownClear`, `NaturalDrainFalse / UnknownTrue`로 남는다(Editor94602, Device585565/585572). 강제 cleanup1을 natural-drain PASS로 확대하지 않는다. pending0·fault0은 모든 owner의 자연 종료·visual 수용 증거가 아니다.
- **Device C3 FAIL64:** schedules3186 / results3160 / ready3096 / pending0. Editor Host는 failures0·EVIDENCE(Editor94591)이나 peer FAIL을 상쇄하지 않는다. detail cap16의 A16은 전량 Expired이며 모두 `publishAfterLimitFalse / receiveAfterLimitEstimateTrue / decisionAfterLimitTrue`; 나머지48의 분류는 미확정이다. clock·네트워크 지연 원인은 단정하지 않으며 PhaseClockRegression도 별도 OPEN·인과 미확정이다.
- 양쪽 ROOT PASS는 **local observer** 결과로 formal cross-audit가 아니다. Client C3 required visual2887/2887은 방출된 ready 범위의 완료이며 64FAIL을 상쇄하지 않는다. Bloom 집계나 이전 자동 C3 PASS를 전체 게임·타유닛·시각/픽셀 PASS로 확대하지 않는다.
- 서버 실제 recipient HoT provenance writer → current-state replication → single presenter가 연결됐다. 동일 owner refresh는 재시작하지 않으며 실제 종료 시 방출 정지 후 tail drain과 lease 반환을 구분한다. 기존 HP/HoT 정책·수치·타유닛 규칙은 불변이다.

#### 신규 프리팹 연결 실측 (2026-10-09)

Main 병합 인계 `ad34bf93`(incoming `6a2f2403` 포함) 후 문서 담당이 checkout 실물5개와 `.meta` GUID를 확인했다. `Assets` 내 `.asset/.unity/.prefab/.cs`에서 아래 GUID 참조는 각각0건이다(`.meta` 제외). **Main도 병합 후 현재 checkout을 독립 검사해 같은5개 refs0을 확인했다**(programmer snapshot 검토와 별개). 따라서 현재 설정/직렬화 연결은 미검출이며 게임 미연결로 기록한다. 이 검색은 native 재생·동적 로딩·visual acceptance 검증이 아니다.

| 프리팹 식별 | GUID |
|------------|------|
| Mushroom tracer | `b9333de6dd687d84ea6795ef4dc7740b` |
| Mushroom impact | `585b2da7e0f5d9d43b68ee8140e28482` |
| Mushroom poison | `b26778bdfea3e174791e8fc01a003ffd` |
| Inferno burn | `32b8d45a8ebc4984485a467f6b7cbda3` |
| RuneSpire attack | `a1406d4f5b9f0544cab305479195055f` |

#### Bloom 자동 검증·빌드·초기 FAIL 이력

아래 시각별 기록은 각 시점의 이력이며 최신 판정은 위 관측 근거를 따른다. 기존 Research/Plan은 원문 보존·갱신하지 않는다.

> **최종 빌드 인계 추가:** Main Unity File → Build And Run element212 클릭1회, immediate capture에서 JDK checking progress 관찰로 preparation started까지 확인. 이후 사용자 지시에 따라 UI/log/process/build 결과 polling 없음. 완료·성공·설치 미확인/사용자 담당, 실제 멀티·기기 visual 미검증. Console Action/C3 PASS19:37:14·Root PASS19:38:13은 기존 log anchors와 같은 증거다. Scene/assets/source 수정 없음; source/protected hash unchanged는 prebuild 시점 인계다. 아래 19:39의 빌드 예정/시도0은 클릭 전 이력으로 보존하며 현재 빌드 상태는 이 문단이 우선한다.

> **최종 최신 상태(19:39 추가):** Main latest Editor DLL19:35:39·final fixture SHA8D020… 적용 후 19:36 실제 Action stress 종료 전체 UAS-DIAG PASS1678501/C3 PASS1678534/Torrent production PASS1678448, 새 Root selfValidate PASS1678545. Main source18 hash match·보호6 unchanged 실측, 정적4 직접 재실행 exit0(19:39 전후). Release 기존 warnings3(CS0168×2/CS0219×1), CS0618/CS0414 suppressed이므로 warnings0 아님. Dev/Release57 Editor refs define-switch·pure identity CLR은 player/native build·particles/게임 runtime 검증이 아니다. Simulate paused-state fixture 원인을 NoClear StopEmitting reassert 후 count0 assertion으로 교정했고 production VfxPoolItem·predicate는 불변이다. 아래 pending/FAIL은 과거 이력으로 보존하며 이 문단이 현재 상태의 단일 요약이다. 실제 멀티/기기 visual 대기, 빌드는 문서/checker0 후 Main 시도 예정으로 아직0. 전체 인벤토리·정적 artifacts·한계는 기존 Research/Plan 최종 인계 참조.

> **최신 상태(19:33 인계):** 강제 Simulate 후 5PS paused=true/stopped=false/playing=false/count0/IsAlive=true 관찰. fixture에서 count0&&!isEmitting 선검사 뒤 StopEmitting NoClear 재적용으로 native stopped state 복구 보고. 기존 predicate·production VfxPoolItem hash는 불변이며 production 원인 확정은 아니다. 정적4 exit0·source18 Main hash match·보호6 hash unchanged 보고와 별개로 Main Refresh 후 native compile 진행 중, 수정 후 Action/Root·빌드 최종 인계 대기다. 아래 “최신” 시각별 기록은 당시 이력; native 전체 PASS 없음·빌드 시도0·실기 미검증. 18파일 대조 목록·assertion 최종 SHA·static artifact 한계는 기존 Research/Plan 최신 요약 참조.

> **19:30 probe 추가 인계:** 반환0 / Ended=false / Pending=1 / SnapshotEq=true. terminal 키는 일치하나 반환·종료 집계는 pending이다. 5PS stop 후 emit=false·count1, 강제 Simulate(30) 후 count0이나 IsAlive=true. 강제 Simulate 영향과 production 동작을 구별하는 중이므로 production 원인 확정이나 drain/회수 PASS가 아니다. Main programmer 재위임, 최종 인계 대기; native 전체 PASS 없음·Root 미실행·빌드 시도0·실기 미검증을 유지한다.

> **19:24 최신 인계:** Main 확인상 Refresh 후 native Editor DLL19:23:41, 실제 Action19:24:50에서 새 source 로드. `All5PS lease starts` 통과 후 `All5PS drain/return once retains terminal key` fixture1357 FAIL. 이번은 최신 loaded source 실패이며 아래 19:16 이전 assembly FAIL과 별개다. Main programmer 재위임·구현 수정 중, native 전체 PASS 없음·Root 미실행·빌드 시도0·실기 미검증, 최종 증거 인계 대기. 아래 “19:16 최신 인계”와 compile/새 검증 pending은 당시 이력으로 보존한다.

> **19:16 최신 인계:** Main 확인상 두 번째 Action FAIL은 이전 assembly 재실행이다(stack assertion1310 / disk1333, Editor DLL18:56:38). 수정된 소스의 새 native 검증 FAIL로 집계하지 않는다. Main Assets Refresh 후 native compile 진행 중이며 완료·로드·새 검증 결과는 pending이다. 아래 “수정 중”은 19:06 기록으로 보존한다. 초기화 원인은 여전히 미확정; native PASS 없음·빌드 시도0·Root 미실행·실기 미검증, 최종 증거 추가 인계 대기.

> **이력 보존(2026-10-09 초기 단계):** 당시 표기는 “프리팹 제작됨(2026-10-09 확인) · 게임 미연결”, “연동은 기존 naming Task 확장 범위이나 아직 미구현/수용 전”이었다. 실제 구현 보고 후 최신 상태만 위와 같이 갱신했다.
> **현재 연결 및 검증 경계:** `UnitEffectConfig.asset healPreset` GUID `e248f616d9334275a75e6a08405219ac` → `EffectPreset_BloomFairy_Heal.asset _vfxPrefab` GUID `ff068a5e9b80f3043a0517ebb1cfb00f` 연결 확인. Programmer 정적 컴파일4 errors0/순수 identity CLR PASS 보고와 별개로, Main 19:06:03/04 `ValidateBloomHoTEffectBoundary`의 `All5PS lease starts` require 조건은 FAIL했다. `PSPlayRecursive PS5/lease1` 로그만으로 native PASS를 인정하지 않는다. 초기화 원인은 미확정 가설이며 programmer 수정 중이다. 빌드 시도0·Root 미실행·실기 미검증; 상세 인계는 기존 [Research](../_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Research.md) / [Plan](../_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Plan.md).

#### 건물 공격 이펙트

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | CannonTower (AutoTower + Human) | actual Assault production vfx_pistoleer_attack 재사용 | Human 별도 preset _vfxPrefab ONLY 적용·Main2파일 검토 완료. GUID badc7c96fdc06564fb9f9af71d1ddf68/root5594113295059828660. Editor static exit0/errors0/warnings0, 새 native/build/visual 미검증. Tank/Cannon=Tank 유지·SFX/offset/다른 값 불변; 이전 Tank 계약/native는 이력 |
| Spirit | RuneSpire | `vfx_runespire_attack.prefab` | **프리팹 제작 확인(10-09) · 연결 source/config freeze·Main hash 일치(10-10)** — 타워 공격 표현. BuildingEffectConfig 연결·실제 재생·SFX 대응 및 실기 수용 미검증; 즉발 서버 피해 정책은 불변 |
| Transcendence | VineTower (AutoTower + Transcendence) | 미지정 | 타 종족 fallback 금지. VFX 미지정이 기존 서버 피해를 차단·변경하지 않음 |

#### 건물 파괴 이펙트

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Castle | `vfx_castle_destroy.prefab` | 본기지 |
| Human | MiningPost | `vfx_miningpost_destroy.prefab` | |
| Human | CannonTower | `vfx_cannontower_destroy.prefab` | |
| Human | FlightFacility | `vfx_flightfacility_destroy.prefab` | |
| Human | TechnicalLaboratory | `vfx_technicallaboratory_destroy.prefab` | |
| Human | TrainingCamp | `vfx_trainingcamp_destroy.prefab` | |
| Human | WarAcademy | `vfx_waracademy_destroy.prefab` | |
| Human | Barracks | `vfx_barracks_destroy.prefab` | |
| Human | Gunsmith | `vfx_gunsmith_destroy.prefab` | |
| Human | Armory | `vfx_armory_destroy.prefab` | |
| Human | WeaponForge | `vfx_weaponforge_destroy.prefab` | |
| Human | Garage | `vfx_garage_destroy.prefab` | |
| Human | VehicleBay | `vfx_vehiclebay_destroy.prefab` | |
| Spirit | SpiritNexus | `vfx_spiritnexus_destroy.prefab` | 본기지 |
| Spirit | ManaRift | `vfx_manarift_destroy.prefab` | |
| Spirit | RuneSpire | `vfx_runespire_destroy.prefab` | |
| Spirit | MagicSpirit | `vfx_magicspirit_destroy.prefab` | |
| Spirit | AstronomicalSpirit | `vfx_astronomicalspirit_destroy.prefab` | |
| Spirit | FireSpire | `vfx_firespire_destroy.prefab` | |
| Spirit | BlazeConduit | `vfx_blazeconduit_destroy.prefab` | |
| Spirit | InfernoCore | `vfx_infernocore_destroy.prefab` | |
| Spirit | AquaSpring | `vfx_aquaspring_destroy.prefab` | |
| Spirit | TidalNexus | `vfx_tidalnexus_destroy.prefab` | |
| Spirit | OceanicHeart | `vfx_oceanicheart_destroy.prefab` | |
| Spirit | StoneMound | `vfx_stonemound_destroy.prefab` | |
| Spirit | TerraForge | `vfx_terraforge_destroy.prefab` | |
| Spirit | GaeaSanctum | `vfx_gaeasanctum_destroy.prefab` | |
| Transcendence | ElderTree | `vfx_eldertree_destroy.prefab` | 본기지 |
| Transcendence | FungalNode | `vfx_fungalnode_destroy.prefab` | |
| Transcendence | VineTower | `vfx_vinetower_destroy.prefab` | |
| Transcendence | MistShrine | `vfx_mistshrine_destroy.prefab` | |
| Transcendence | WillowShrine | `vfx_willowshrine_destroy.prefab` | |
| Transcendence | AncientGrove | `vfx_ancientgrove_destroy.prefab` | |
| Transcendence | PrimalAltar | `vfx_primalaltar_destroy.prefab` | |
| Transcendence | PrimalDen | `vfx_primalden_destroy.prefab` | |
| Transcendence | PrimalSanctuary | `vfx_primalsanctuary_destroy.prefab` | |
| Transcendence | FeralAltar | `vfx_feralaltar_destroy.prefab` | |
| Transcendence | FeralDen | `vfx_feralden_destroy.prefab` | |
| Transcendence | FeralSanctuary | `vfx_feralsanctuary_destroy.prefab` | |
| Transcendence | SporePatch | `vfx_sporepatch_destroy.prefab` | |
| Transcendence | FloralNursery | `vfx_floralnursery_destroy.prefab` | |

#### 건물 업그레이드 이펙트

> 업그레이드 가능한 건물에만 적용. 해당하지 않는 항목은 삭제.

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Castle | `vfx_castle_upgrade.prefab` | |
| Human | MiningPost | `vfx_miningpost_upgrade.prefab` | |
| Human | CannonTower | `vfx_cannontower_upgrade.prefab` | |
| Human | FlightFacility | `vfx_flightfacility_upgrade.prefab` | |
| Human | TechnicalLaboratory | `vfx_technicallaboratory_upgrade.prefab` | |
| Human | TrainingCamp | `vfx_trainingcamp_upgrade.prefab` | |
| Human | WarAcademy | `vfx_waracademy_upgrade.prefab` | |
| Human | Barracks | `vfx_barracks_upgrade.prefab` | |
| Human | Gunsmith | `vfx_gunsmith_upgrade.prefab` | |
| Human | Armory | `vfx_armory_upgrade.prefab` | |
| Human | WeaponForge | `vfx_weaponforge_upgrade.prefab` | |
| Human | Garage | `vfx_garage_upgrade.prefab` | |
| Human | VehicleBay | `vfx_vehiclebay_upgrade.prefab` | |
| Spirit | SpiritNexus | `vfx_spiritnexus_upgrade.prefab` | |
| Spirit | ManaRift | `vfx_manarift_upgrade.prefab` | |
| Spirit | RuneSpire | `vfx_runespire_upgrade.prefab` | |
| Spirit | MagicSpirit | `vfx_magicspirit_upgrade.prefab` | |
| Spirit | AstronomicalSpirit | `vfx_astronomicalspirit_upgrade.prefab` | |
| Spirit | FireSpire | `vfx_firespire_upgrade.prefab` | |
| Spirit | BlazeConduit | `vfx_blazeconduit_upgrade.prefab` | |
| Spirit | InfernoCore | `vfx_infernocore_upgrade.prefab` | |
| Spirit | AquaSpring | `vfx_aquaspring_upgrade.prefab` | |
| Spirit | TidalNexus | `vfx_tidalnexus_upgrade.prefab` | |
| Spirit | OceanicHeart | `vfx_oceanicheart_upgrade.prefab` | |
| Spirit | StoneMound | `vfx_stonemound_upgrade.prefab` | |
| Spirit | TerraForge | `vfx_terraforge_upgrade.prefab` | |
| Spirit | GaeaSanctum | `vfx_gaeasanctum_upgrade.prefab` | |
| Transcendence | ElderTree | `vfx_eldertree_upgrade.prefab` | |
| Transcendence | FungalNode | `vfx_fungalnode_upgrade.prefab` | |
| Transcendence | VineTower | `vfx_vinetower_upgrade.prefab` | |
| Transcendence | MistShrine | `vfx_mistshrine_upgrade.prefab` | |
| Transcendence | WillowShrine | `vfx_willowshrine_upgrade.prefab` | |
| Transcendence | AncientGrove | `vfx_ancientgrove_upgrade.prefab` | |
| Transcendence | PrimalAltar | `vfx_primalaltar_upgrade.prefab` | |
| Transcendence | PrimalDen | `vfx_primalden_upgrade.prefab` | |
| Transcendence | PrimalSanctuary | `vfx_primalsanctuary_upgrade.prefab` | |
| Transcendence | FeralAltar | `vfx_feralaltar_upgrade.prefab` | |
| Transcendence | FeralDen | `vfx_feralden_upgrade.prefab` | |
| Transcendence | FeralSanctuary | `vfx_feralsanctuary_upgrade.prefab` | |
| Transcendence | SporePatch | `vfx_sporepatch_upgrade.prefab` | |
| Transcendence | FloralNursery | `vfx_floralnursery_upgrade.prefab` | |

#### 건물 특수 효과 이펙트

> MistShrine 물안개 힐은 **2026-08-12 기준 로직·UI 구현 완료**(에디터 싱글플레이 실기 검증 완료 · 멀티 미검증)이지만, 아래 이펙트는 **여전히 모두 미제작**이다.
> 즉 **물안개는 동작하되 눈에 보이지 않는 상태**이며, 이 이펙트 제작이 남은 작업이다.
> 규칙: [`GameSystemRules/GameSystemRules_Buildings.md`](../GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템 규칙 26**.
> 물안개는 **뿌리는 순간**(1회)과 **지속 중**(루프)의 두 국면이 있어 이펙트를 분리한다.

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | MistShrine | `vfx_mistshrine_mist_cast.prefab` | **미제작** — 물안개 시전 순간 1회 재생 |
| Transcendence | MistShrine | `vfx_mistshrine_mist_loop.prefab` | **미제작** — 물안개 지속 중 루프 재생. 건물 중심 고정(이동하지 않음), 지속시간 종료·시전 건물 파괴 시 즉시 정지 |

> **범위 표시 반투명 원형 UI는 이 문서 대상이 아니다.** VFX가 아니라 UI/셰이더 영역이며,
> 기존 지면 데칼 셰이더 `Assets/_Project/Shaders/SkillAimOverlay.shader` 재사용을 우선 검토한다(같은 문서 규칙 27).

#### 골드 채집 이펙트

| 오브젝트 | 파일명 | 비고 |
|---------|--------|------|
| GoldMine | `vfx_goldmine_harvest.prefab` | 루프 재생 |

#### UI 이펙트

| 이펙트 | 파일명 | 비고 |
|--------|--------|------|
| 버튼 클릭 | `vfx_ui_button_click.prefab` | |
| 버튼 확인 | `vfx_ui_button_confirm.prefab` | |
| 골드 획득 | `vfx_ui_gold_gain.prefab` | |
| 타이머 완료 | `vfx_ui_timer_complete.prefab` | |

#### 타일 점령 이펙트

| 팀 | 파일명 | 비고 |
|----|--------|------|
| Blue | `vfx_tile_capture_blue.prefab` | |
| Red | `vfx_tile_capture_red.prefab` | |

#### 게임 이벤트 이펙트

| 이벤트 | 파일명 | 비고 |
|--------|--------|------|
| 게임 시작 | `vfx_game_start.prefab` | |
| 승리 | `vfx_game_victory.prefab` | |
| 패배 | `vfx_game_defeat.prefab` | |

---

## Ⅱ. SFX (사운드 이펙트)

### 1. 완성된 SFX

> 현재 없음

---

### 2. 제작 예정 SFX

#### 유닛 공격 사운드

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Pistoleer | `sfx_pistoleer_attack.wav` | 권총 발사음 |
| Human | Assault | `sfx_assault_attack.wav` | 돌격소총 연사음 |
| Human | Sniper | `sfx_sniper_attack.wav` | 저격 발사음 |
| Human | LittleKnight | `sfx_littleknight_attack.wav` | 검 베는 소리 |
| Human | SpearMan | `sfx_spearman_attack.wav` | 창 찌르는 소리 |
| Human | BattleAxe | `sfx_battleaxe_attack.wav` | 도끼 내려치는 소리 |
| Human | Tank | `sfx_tank_attack.wav` | 포탄 발사음 |
| Human | CannonCart | `sfx_cannoncart_attack.wav` | 범위 포격음 |
| Spirit | EmberSpirit | `sfx_emberspirit_attack.wav` | 불 마법음 |
| Spirit | FlameSpirit | `sfx_flamespirit_attack.wav` | 화염 방출음 |
| Spirit | InfernoSpirit | `sfx_infernospirit_attack.wav` | 인페르노 폭발음 |
| Spirit | DustSpirit | `sfx_dustspirit_attack.wav` | 흙먼지 공격음 |
| Spirit | BoulderSpirit | `sfx_boulderspirit_attack.wav` | 바위 투척음 |
| Spirit | QuakeSpirit | `sfx_quakespirit_attack.wav` | 지진 광역음 |
| Spirit | TideSpirit | `sfx_tidespirit_attack.wav` | 물 튀기는 소리 |
| Spirit | StreamSpirit | `sfx_streamspirit_attack.wav` | 물줄기 소리 |
| Spirit | TorrentSpirit | `sfx_torrentspirit_attack.wav` | 급류 소리 |
| Transcendence | FoxMagician | `sfx_foxmagician_attack.wav` | 마법 섬광음 |
| Transcendence | BearGuard | `sfx_bearguard_attack.wav` | 강타음 |
| Transcendence | LionKnight | `sfx_lionknight_attack.wav` | 발톱 베는 소리 |
| Transcendence | RhinoBreaker | `sfx_rhinobreaker_attack.wav` | 돌진 충격음 |
| Transcendence | EagleArcher | `sfx_eaglearcher_attack.wav` | 화살 발사음 |
| Transcendence | RabbitTrickster | `sfx_rabbittrickster_attack.wav` | 단검 베는 소리 |
| Transcendence | MushroomBomber | `sfx_mushroombomber_attack.wav` | 포자 폭발음 |

#### 유닛 사망 사운드

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Pistoleer | `sfx_pistoleer_death.wav` | |
| Human | Assault | `sfx_assault_death.wav` | |
| Human | Sniper | `sfx_sniper_death.wav` | |
| Human | LittleKnight | `sfx_littleknight_death.wav` | |
| Human | SpearMan | `sfx_spearman_death.wav` | |
| Human | BattleAxe | `sfx_battleaxe_death.wav` | |
| Human | Tank | `sfx_tank_death.wav` | |
| Human | CannonCart | `sfx_cannoncart_death.wav` | |
| Spirit | EmberSpirit | `sfx_emberspirit_death.wav` | |
| Spirit | FlameSpirit | `sfx_flamespirit_death.wav` | |
| Spirit | InfernoSpirit | `sfx_infernospirit_death.wav` | |
| Spirit | DustSpirit | `sfx_dustspirit_death.wav` | |
| Spirit | BoulderSpirit | `sfx_boulderspirit_death.wav` | |
| Spirit | QuakeSpirit | `sfx_quakespirit_death.wav` | |
| Spirit | TideSpirit | `sfx_tidespirit_death.wav` | |
| Spirit | StreamSpirit | `sfx_streamspirit_death.wav` | |
| Spirit | TorrentSpirit | `sfx_torrentspirit_death.wav` | |
| Transcendence | FoxMagician | `sfx_foxmagician_death.wav` | |
| Transcendence | BearGuard | `sfx_bearguard_death.wav` | |
| Transcendence | LionKnight | `sfx_lionknight_death.wav` | |
| Transcendence | RhinoBreaker | `sfx_rhinobreaker_death.wav` | |
| Transcendence | EagleArcher | `sfx_eaglearcher_death.wav` | |
| Transcendence | RabbitTrickster | `sfx_rabbittrickster_death.wav` | |
| Transcendence | MushroomBomber | `sfx_mushroombomber_death.wav` | |
| Transcendence | BloomFairy | `sfx_bloomfairy_death.wav` | |

#### 유닛 특수 효과 사운드

> 사운드 규칙 15(VFX + SFX 쌍 호출)에 따라 위 **유닛 특수 효과 이펙트**와 대응한다. 현재 SFX 없음은 같은 규칙의 주석 명시 허용으로 보류한다.

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | BloomFairy | `sfx_bloomfairy_heal.wav` | **미제작** — `vfx_bloomfairy_heal`과 쌍. 1회음/반복음 여부 미정; 현재 결정 불필요. VFX 연동 시 SFX 없음 주석 명시로 보류 |

#### 건물 파괴 사운드

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Castle | `sfx_castle_destroy.wav` | |
| Human | MiningPost | `sfx_miningpost_destroy.wav` | |
| Human | CannonTower | `sfx_cannontower_destroy.wav` | |
| Human | FlightFacility | `sfx_flightfacility_destroy.wav` | |
| Human | TechnicalLaboratory | `sfx_technicallaboratory_destroy.wav` | |
| Human | TrainingCamp | `sfx_trainingcamp_destroy.wav` | |
| Human | WarAcademy | `sfx_waracademy_destroy.wav` | |
| Human | Barracks | `sfx_barracks_destroy.wav` | |
| Human | Gunsmith | `sfx_gunsmith_destroy.wav` | |
| Human | Armory | `sfx_armory_destroy.wav` | |
| Human | WeaponForge | `sfx_weaponforge_destroy.wav` | |
| Human | Garage | `sfx_garage_destroy.wav` | |
| Human | VehicleBay | `sfx_vehiclebay_destroy.wav` | |
| Spirit | SpiritNexus | `sfx_spiritnexus_destroy.wav` | |
| Spirit | ManaRift | `sfx_manarift_destroy.wav` | |
| Spirit | RuneSpire | `sfx_runespire_destroy.wav` | |
| Spirit | MagicSpirit | `sfx_magicspirit_destroy.wav` | |
| Spirit | AstronomicalSpirit | `sfx_astronomicalspirit_destroy.wav` | |
| Spirit | FireSpire | `sfx_firespire_destroy.wav` | |
| Spirit | BlazeConduit | `sfx_blazeconduit_destroy.wav` | |
| Spirit | InfernoCore | `sfx_infernocore_destroy.wav` | |
| Spirit | AquaSpring | `sfx_aquaspring_destroy.wav` | |
| Spirit | TidalNexus | `sfx_tidalnexus_destroy.wav` | |
| Spirit | OceanicHeart | `sfx_oceanicheart_destroy.wav` | |
| Spirit | StoneMound | `sfx_stonemound_destroy.wav` | |
| Spirit | TerraForge | `sfx_terraforge_destroy.wav` | |
| Spirit | GaeaSanctum | `sfx_gaeasanctum_destroy.wav` | |
| Transcendence | ElderTree | `sfx_eldertree_destroy.wav` | |
| Transcendence | FungalNode | `sfx_fungalnode_destroy.wav` | |
| Transcendence | VineTower | `sfx_vinetower_destroy.wav` | |
| Transcendence | MistShrine | `sfx_mistshrine_destroy.wav` | |
| Transcendence | WillowShrine | `sfx_willowshrine_destroy.wav` | |
| Transcendence | AncientGrove | `sfx_ancientgrove_destroy.wav` | |
| Transcendence | PrimalAltar | `sfx_primalaltar_destroy.wav` | |
| Transcendence | PrimalDen | `sfx_primalden_destroy.wav` | |
| Transcendence | PrimalSanctuary | `sfx_primalsanctuary_destroy.wav` | |
| Transcendence | FeralAltar | `sfx_feralaltar_destroy.wav` | |
| Transcendence | FeralDen | `sfx_feralden_destroy.wav` | |
| Transcendence | FeralSanctuary | `sfx_feralsanctuary_destroy.wav` | |
| Transcendence | SporePatch | `sfx_sporepatch_destroy.wav` | |
| Transcendence | FloralNursery | `sfx_floralnursery_destroy.wav` | |

#### 건물 업그레이드 사운드

> 업그레이드 가능한 건물에만 적용. 해당하지 않는 항목은 삭제.

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Castle | `sfx_castle_upgrade.wav` | |
| Human | MiningPost | `sfx_miningpost_upgrade.wav` | |
| Human | CannonTower | `sfx_cannontower_upgrade.wav` | |
| Human | FlightFacility | `sfx_flightfacility_upgrade.wav` | |
| Human | TechnicalLaboratory | `sfx_technicallaboratory_upgrade.wav` | |
| Human | TrainingCamp | `sfx_trainingcamp_upgrade.wav` | |
| Human | WarAcademy | `sfx_waracademy_upgrade.wav` | |
| Human | Barracks | `sfx_barracks_upgrade.wav` | |
| Human | Gunsmith | `sfx_gunsmith_upgrade.wav` | |
| Human | Armory | `sfx_armory_upgrade.wav` | |
| Human | WeaponForge | `sfx_weaponforge_upgrade.wav` | |
| Human | Garage | `sfx_garage_upgrade.wav` | |
| Human | VehicleBay | `sfx_vehiclebay_upgrade.wav` | |
| Spirit | SpiritNexus | `sfx_spiritnexus_upgrade.wav` | |
| Spirit | ManaRift | `sfx_manarift_upgrade.wav` | |
| Spirit | RuneSpire | `sfx_runespire_upgrade.wav` | |
| Spirit | MagicSpirit | `sfx_magicspirit_upgrade.wav` | |
| Spirit | AstronomicalSpirit | `sfx_astronomicalspirit_upgrade.wav` | |
| Spirit | FireSpire | `sfx_firespire_upgrade.wav` | |
| Spirit | BlazeConduit | `sfx_blazeconduit_upgrade.wav` | |
| Spirit | InfernoCore | `sfx_infernocore_upgrade.wav` | |
| Spirit | AquaSpring | `sfx_aquaspring_upgrade.wav` | |
| Spirit | TidalNexus | `sfx_tidalnexus_upgrade.wav` | |
| Spirit | OceanicHeart | `sfx_oceanicheart_upgrade.wav` | |
| Spirit | StoneMound | `sfx_stonemound_upgrade.wav` | |
| Spirit | TerraForge | `sfx_terraforge_upgrade.wav` | |
| Spirit | GaeaSanctum | `sfx_gaeasanctum_upgrade.wav` | |
| Transcendence | ElderTree | `sfx_eldertree_upgrade.wav` | |
| Transcendence | FungalNode | `sfx_fungalnode_upgrade.wav` | |
| Transcendence | VineTower | `sfx_vinetower_upgrade.wav` | |
| Transcendence | MistShrine | `sfx_mistshrine_upgrade.wav` | |
| Transcendence | WillowShrine | `sfx_willowshrine_upgrade.wav` | |
| Transcendence | AncientGrove | `sfx_ancientgrove_upgrade.wav` | |
| Transcendence | PrimalAltar | `sfx_primalaltar_upgrade.wav` | |
| Transcendence | PrimalDen | `sfx_primalden_upgrade.wav` | |
| Transcendence | PrimalSanctuary | `sfx_primalsanctuary_upgrade.wav` | |
| Transcendence | FeralAltar | `sfx_feralaltar_upgrade.wav` | |
| Transcendence | FeralDen | `sfx_feralden_upgrade.wav` | |
| Transcendence | FeralSanctuary | `sfx_feralsanctuary_upgrade.wav` | |
| Transcendence | SporePatch | `sfx_sporepatch_upgrade.wav` | |
| Transcendence | FloralNursery | `sfx_floralnursery_upgrade.wav` | |

#### 건물 특수 효과 사운드

> 사운드 규칙 15(VFX + SFX 쌍 호출)에 따라 위 **건물 특수 효과 이펙트**와 1:1로 대응한다.
> MistShrine 물안개 힐은 **2026-08-12 기준 로직·UI 구현 완료**(멀티 미검증)이지만, 아래 사운드는 **여전히 모두 미제작**이다.

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | MistShrine | `sfx_mistshrine_mist_cast.wav` | **미제작** — 물안개 시전음(1회). `vfx_mistshrine_mist_cast`와 쌍 |
| Transcendence | MistShrine | `sfx_mistshrine_mist_loop.wav` | **미제작** — 물안개 지속 환경음(루프). `vfx_mistshrine_mist_loop`와 쌍, 물안개 소멸 시 함께 정지 |

#### UI 사운드

| 사운드 | 파일명 | 비고 |
|--------|--------|------|
| 버튼 클릭 | `sfx_ui_button_click.wav` | |
| 버튼 확인 | `sfx_ui_button_confirm.wav` | |
| 버튼 취소 | `sfx_ui_button_cancel.wav` | |
| 패널 열기 | `sfx_ui_panel_open.wav` | |
| 패널 닫기 | `sfx_ui_panel_close.wav` | |
| 오류/불가 | `sfx_ui_error.wav` | |

#### 타일 점령 사운드

| 사운드 | 파일명 | 비고 |
|--------|--------|------|
| 타일 점령 | `sfx_tile_capture.wav` | |

#### 배경음악 (BGM)

| 상황 | 파일명 | 비고 |
|------|--------|------|
| 전투 | `bgm_battle.wav` | 루프 |
| 로비 | `bgm_lobby.wav` | 루프 |
| 승리 | `bgm_victory.wav` | 루프 가능 |
| 패배 | `bgm_defeat.wav` | 루프 가능 |

#### 환경 효과음 (Ambient)

| 환경 | 파일명 | 비고 |
|------|--------|------|
| 전장 환경음 | `amb_battlefield.wav` | 루프 |
| 바람 | `amb_wind.wav` | 루프 |

#### 게임 이벤트 사운드

| 이벤트 | 파일명 | 비고 |
|--------|--------|------|
| 게임 시작 | `sfx_game_start.wav` | |
| 승리 | `sfx_game_victory.wav` | |
| 패배 | `sfx_game_defeat.wav` | |

---

## Ⅲ. 보류 (On Hold)

> 현재 제작하지 않으며, 추후 필요 시 제작 예정으로 이동.

### VFX 보류

| 카테고리 | 대상 | 비고 |
|---------|------|------|
| 유닛 피격 이펙트 | 전 유닛 | 추후 추가 예정 |
| 유닛 생산 완료 이펙트 | - | 추후 추가 예정 |
| 건물 건설 이펙트 | 전 건물 | 추후 추가 예정 |

### SFX 보류

| 카테고리 | 대상 | 비고 |
|---------|------|------|
| 유닛 피격 사운드 | 전 유닛 | 추후 추가 예정 |
| 유닛 생산 완료 사운드 | - | 추후 추가 예정 |
| 건물 건설 사운드 | 전 건물 | 추후 추가 예정 |
