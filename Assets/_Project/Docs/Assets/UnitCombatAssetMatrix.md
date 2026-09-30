# Unit Combat Asset Matrix

**LionKnight 현행 판정(2026-09-30): 0:18·1:13 focused 실기 PASS/CLOSED, C3 만료 별도 OPEN.** 사용자 수용 `a8134504…c3aacb5b` 경기에서 LionKnight 8기·LittleKnight 22기, 양측 결과·ready 216/216·묶음 방출 216/216·필수 표현 188/188·Client `Expired` 0을 확인했다. 이전 만료 7건의 원인은 미확정이며 진단 task OPEN이다. 정확한 접촉/피해 offset·공식 CrossAudit·전체 migration은 별도 범위다. 아래 FAIL/OPEN은 첫 경기 직후의 역사적 판정이다.

**LionKnight 첫 경기 판정(2026-09-30): 0:18·1:13 저장 정렬, 당시 focused 실기 FAIL/OPEN.** Attack clip과 type 22 fallback은 0.6/1.4333333초로 일치하고 Game 명시 연결·영구 production gate 코드가 있다. `a2d9c80e…c622225` Android Host/Editor Client의 LionKnight+LittleKnight 2/25 혼합 경기에서 Host 결과 224/실패 0·필수 표현 199/199, Client 결과 224 수락/거부 0이나 C3 `Expired` 7건으로 ready 217/224·방출 217/224, ready 범위 필수 표현 192/192였다. 첫 만료는 LionKnight UnitId 2에 대응하나 나머지 여섯 건의 유형은 미확정이다. 사용자에게 큰 시각적 문제는 없었지만 심한 렉이 있었고 FPS/frame-time은 미계측이다. 양쪽 local ROOT PASS는 공식 CrossAudit Analyze가 아니다. 정확한 접촉·피해 offset, 25종·역할교대·rollback은 미검증이다. 상세: `_Tasks/2026-09-30/01_21_lionknight-attack-timing-correction/{Research,Plan}.md`.

**BearGuard 최신 판정(2026-09-30): 0:13 focused 실기 PASS/CLOSED.** 사용자 육안 수용과 동일 재테스트 `b986fbbc…61d7d94b`의 경기 전체 Host/Client 결과 641/641·필수 표현 각 608/608·실패 0, 공간 mismatch 0, Host MOVE adapterFailures 0, 양쪽 local ROOT PASS가 근거다. BearGuard 11·LittleKnight 49의 2/25 혼합 경기이며 첫 경기 ROOT INCOMPLETE와 합산하지 않는다. BearGuard 단독 marker→피해 offset·공식 CrossAudit Analyze·25종/역할교대/rollback은 미검증, LittleKnight 이전 MOVE 4건과 설계 주기 `1:20` 대 실제 cooldown 1.2초 차이는 별도 OPEN이다. 아래 2026-09-29 “실기 전” 문단은 당시 이력이다. 상세: `_Tasks/2026-09-29/23_28_bearguard-attack-timing-correction/{Research,Plan}.md`.

유닛별 공격 설계 목표와 현재 런타임·애니메이션·VFX 준비 상태를 분리해 추적하는 감사 표다.

**BearGuard 이전 단계(2026-09-29, Unity 두 self-validation PASS / 실기 전):** 생산 Attack 단일 `OnAttackHit`와 type 20 `hitFrameTimes`를 30fps `0:13 = 0.43333334초`로 저장하고 Game 씬 UnitFactory 초월계 type 20에 GUID `6cd4a1668d01f24448599b56306f2b13`을 명시 연결했다. 영구 production gate 추가 후 Unity Console에서 23:43:36 `[UAS-DIAG] self-validation PASS`, 23:45:06 `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`를 확인했다. `File > Build And Run` 후 `Checking prerequisites / Starting Android build` 화면까지만 확인했고 **빌드 완료·설치·새 실기·육안 판정은 미확인**이다. cooldown `1.2초` 대 StatsReference 설계 주기 `1:20 = 1.666666…초`, LittleKnight 이전 MOVE 실패 4건, 전체 migration은 OPEN이다. 근거: `_Tasks/2026-09-29/23_28_bearguard-attack-timing-correction/` 최신 절.

**BearGuard 이전 상태(2026-09-29, 구현 저장 직후 / Unity 검증 전):** clip·type 20 설정과 Game 명시 연결·영구 gate 코드만 반영된 당시에는 Unity 두 메뉴와 Build And Run이 미실행이었다. 위 2026-09-29 검증 결과는 그다음 당시 단계이며, 현재 판정은 2026-09-30 최상단을 따른다.

**TideSpirit 최신 상태(2026-09-29, focused 시각 PASS/CLOSED):** 동일 `ae04f2a9…e6bd03` Editor Host/Android Client 혼합 경기에서 Red TideSpirit 32기·Blue LittleKnight 12기 생산, **경기 전체** 결과 Host 160/실패 0 ↔ Client 160 수락/거부 0, 양쪽 필수 표현 129/129·실패 0, 공간 mismatch 0, Host MOVE adapterFailures 0, 양쪽 ROOT local PASS였다. 사용자가 Tide 타격을 육안상 문제없다고 수용했다. Tide 단독 marker→권위 피해 offset·공식 CrossAudit·25종/역할교대/rollback은 미검증이고 LittleKnight 과거 MOVE 4건은 별도 OPEN이다. 다음 문단은 구현 직후의 실기 전 상태다. 근거: `_Tasks/2026-09-29/19_09_tidespirit-attack-correction/` 최신 절.

**TideSpirit 이전 상태(2026-09-29, 구현·Unity 검증 PASS / 실기 전):** 사용자가 저장한 생산 Attack 클립의 단일 `OnAttackHit @ 1.50초`(30fps `1:15`), 3초 길이·loop를 보존했다. 실제 type 16 설정 `hitFrameTimes=[1.5]`/cooldown 3초, Game 씬 Spirit type 16 명시 Attack 연결, 양 팀 생산 연결을 검사하는 영구 gate가 저장됐다. Unity 재컴파일 뒤 19:55:36 Unit Action 및 19:56:59 Root Pose Cross Audit self-validation은 각각 PASS했다. `File > Build And Run` 뒤 JDK 탐지 진행까지만 확인했으며 **당시 빌드 완료·설치·새 경기·사용자 육안 수용은 미확인**이었다. 아래 TideSpirit 행이 현행값이며 기존 `1 @ 1.15s / 동일`은 교정 전 감사 이력이다. LittleKnight 이전 MOVE-AUTH 4건은 별도 OPEN; 정확한 marker→권위 피해 offset·공식 cross-audit·25종/역할교대/rollback은 미검증이다. 근거: `_Tasks/2026-09-29/19_09_tidespirit-attack-correction/` 최신 절.

**BoulderSpirit 최신 상태(2026-09-29, 1:10 focused PASS/CLOSED):** 생산 Attack 단일 marker·type 14 설정·영구 gate 기대값을 **40/30초(약 1.3333333초)**로 맞추고 30fps·4초 주기/클립 길이를 유지했다. Unity 두 self-validation PASS 뒤 새 Android Host/Editor Client 경기에서 BoulderSpirit 7기 생산, **BoulderSpirit+LittleKnight 혼합 경기** 결과 Host/Client 283/283·필수 표현 양쪽 265/265·오류 0, Host MOVE adapterFailures 0·양쪽 local ROOT PASS를 확인했다. 사용자는 새 1:10 타격이 **“육안상 문제없었어”**라고 수용했다. 아래 1:15(1.50초) 화면 수용과 453/414는 이전 버전 이력이다. 새 값의 BoulderSpirit 단독 정밀 offset·공식 cross-audit·전체 migration은 미검증이며 LittleKnight 과거 MOVE-AUTH 4건은 별도 OPEN이다. Build And Run은 시작 화면 관찰만 기록하며 완료 관찰을 주장하지 않는다.

**BoulderSpirit 이전 1:15 버전(2026-09-29 실기 이력; 현재값 아님):** 30fps `1:15(4:00)`은 당시 1.50초/4초로 저장돼 있었다. 사용자는 타격이 약간 늦지만 수용 가능하다고 했다. 같은 Editor Host/Android Client 경기의 공격 결과 453/453·필수 표현 414/414는 **BoulderSpirit+LittleKnight 경기 전체** 수치이며 BoulderSpirit 단독 1.50초 정밀 측정이 아니다. Host LittleKnight MOVE-AUTH `adapterFailures=4`는 별도 FAIL이다. 현재 1:10 상태는 위 최신 문단·아래 Boulder 행을 따른다.

**현재 상태(2026-09-29):** FlameSpirit 현재 6시각은 `20/30, 35/30, 43/30, 50/30, 57/30, 63/30`초이며, 같은 세션 Editor Host/Android Client에서 실제 생산·Host 공격 시작 Ready/shadow commit, 사용자 화면 관찰 “정상적으로 되는 것으로 보여”가 확인됐다. 경기 전체 결과·표현은 수렴했지만 FlameSpirit 회차별 여섯 HitIndex의 정밀 타이밍 검증은 OPEN이다(아래 행·09-28 FlameSpirit Task의 09-29 후속 절). **이전 2026-09-28 상태:** Fox 표시 지연 focused 교정은 사용자 수용, 현행 타이밍 유지·재튜닝은 발사체 구현 시점으로 이관. Dust 49기 focused 로그는 CONDITIONAL PASS/OPEN(육안 최종 수용 대기). RabbitTrickster는 이전 0.18초 실기의 이른 피해 관찰 후 생산 Attack marker·type 25 설정·영구 검증 기대값을 0:20(0.6666667초)으로 맞췄다. 새 Editor Host/Android Client 경기 `3fc75ef4…b7beded`와 사용자 “알맞게 나오는 것 같다”는 육안 수용으로 Rabbit 타이밍·명시 연결은 **focused PASS/CLOSED**다. EmberSpirit도 명시 Attack 연결·영구 gate, 두 경기 Host/Client 로그와 사용자 육안 수용으로 **focused PASS/CLOSED**다. 두 focused 집계는 각 유닛 단독 타격 수가 아니며 정확한 animation→권위 피해 offset은 미계측이다. 상세 증거는 기존 Fox Task §21, Dust Task 재테스트 결과 및 Rabbit·Ember Task 후속 증거 절 참조; 전체 migration 완료 아님.

**이전 증거:** 09-27 시점 분리·17_21 지연 분석과 09-28 자동 검증 직후 상태는 Fox Task §18~20에 보존한다. 현재 판정은 위 요약을 따른다.


**감사 기준일:** 2026-08-03 (RhinoBreaker·QuakeSpirit focused 검증: 2026-09-14, BattleAxe focused 검증: 2026-09-20, InfernoSpirit focused 검증: 2026-09-22)
**브랜치:** `codex/unit-movement-attack-sync-audit`  
**범위:** 정적 공격 에셋·설정·코드 연결 감사 + main에 반영된 InfernoSpirit 사용자 실기 결과와 QuakeSpirit Host/Client 피해 로그 재검토 + B1 Simulation/Visual Root 검증 + B2 서버 이동·SimulationFacing Shadow 25종 Blue/Red 누적 멀티플레이 검증. B2 이동 진단은 완료했지만 실제 이동 writer 전환, 공격 방향·Impact/피해 시점과 25종 규칙 v2 공격 전체 멀티플레이 검증은 수행하지 않음.

이 문서는 상태 스냅샷이며 게임 규칙 권위가 아니다. 공격 의미는 `GameSystemRules_Units.md`, 동기화 계약은 `GameSystemRules_UnitCombatSynchronization.md`, 런타임 수치 원본은 `Resources/Config/UnitStatsConfig.asset`을 따른다.

---

## 상태 정의

- **Complete:** 규칙 v2 프로필, 서버 Impact, 에셋, Blue/Red, 멀티 검증 완료
- **MigrationRequired:** 기존 공격은 동작하지만 ActionSequence·서버 Impact 구조로 이전 필요
- **Incomplete:** 필수 스탯·특수 로직·Attack 클립·표현 자산 중 하나 이상 누락
- **Provisional:** 목표 전달 방식이나 에셋 타격점의 실기 확인 필요

둘 이상의 문제가 겹치면 상태를 함께 병기한다. 우선순위는 `Incomplete/Critical → Incomplete → Provisional → MigrationRequired → Complete` 순이며, 상위 상태가 해결되기 전에는 Complete로 올리지 않는다.

프리팹이나 Animation Event가 존재한다는 이유만으로 Complete로 판정하지 않는다.

---

## 공통 감사 결과

| 항목 | 결과 |
|---|---|
| UnitType | 25종 |
| Blue / Red 프리팹 | 25종 모두 양쪽 존재 |
| Animator Controller | 25종 모두 존재, Blue/Red가 같은 유닛 Controller 참조 |
| 기본 `{Unit}_Attack.anim` | 25종 모두 존재하고 Controller에 연결 |
| 기본 Attack 클립 `OnAttackHit` | 23/25 보유 — QuakeSpirit 1.667초 production marker 확인 반영 |
| 기본 Attack 이벤트 누락 | MushroomBomber, BloomFairy |
| UnitStatsConfig | 25/25 존재 — QuakeSpirit 항목 추가됨 |
| SpecialAttackRegistry | 5종 등록 — BattleAxe, TorrentSpirit, MushroomBomber, InfernoSpirit, QuakeSpirit. BloomFairy는 별도 Healer 경로 |
| SpecialAttackConfig 직렬화 | Quake 2필드는 asset에 존재. Inferno·Blast 필드는 C# 선언/폴백은 있으나 현재 asset YAML에 명시되지 않음 |
| `attackPreset` | 9/25 연결 |
| `hitPreset` | 0/25 연결 |
| `tracerPreset` | 0/25 연결 |
| B2 이동 Shadow Blue/Red 표본 | 25/25 — read-only 진단 완료, 실제 writer 전환은 B3 미완료 |
| 규칙 v2 공격 전체 멀티 검증 완료 | 0/25 — 공격 권위 런타임 구현 전 |

`attackPreset` 연결 9종: Pistoleer, Assault, Sniper, Tank, CannonCart, InfernoSpirit, StreamSpirit, TorrentSpirit, FoxMagician.

---

## 신규·교체 유닛 프리팹 등록 체크리스트

현재 `25종/50개`와 종족별 `Human 16 / Spirit 18 / Transcendence 16`, VFX 기준선은 2026-07-27 감사 스냅샷이다. 신규 유닛 타입을 추가하면 보통 Blue/Red 두 프리팹이 함께 늘어난다. 다음 항목을 모두 반영하기 전에는 신규 프리팹을 생산 목록이나 빌드에 등록하지 않는다.

- [ ] `UnitType`과 Blue/Red 쌍을 추가하고 파일명을 `Unit_<UnitType>_<Blue|Red>`로 맞춘다.
- [ ] 검증된 migrated 유닛 템플릿에서 생성한다. Simulation Root 직접 자식은 identity `VisualRoot` 하나이며 모든 모델·Animator·Renderer·VFX/socket은 그 아래에 둔다.
- [ ] Simulation Root에 `NetworkObject`, `NetworkTransform`, `NetworkUnit`, `UnitView`, `VisualRootProjector`를 유지하고, projector가 직접 자식 `VisualRoot`를 참조하게 한다.
- [ ] `NetworkTransform`의 서버 권위와 `Interpolate=true`, `PositionLerpSmoothing=false`를 유지한다. 전체 보간 비활성화로 오해하거나 클라이언트 Simulation Root writer를 추가하지 않는다.
- [ ] 모든 Animator의 Root Motion을 끄고, 네트워크·판정 컴포넌트를 `VisualRoot` 아래로 이동하지 않는다.
- [ ] Simulation Root의 Animator·Renderer가 0인지 확인한다. 각 Animator와 같은 GameObject에 `AnimationEventRelay` 하나를 두고 relay 수와 Animator 수를 일치시킨다.
- [ ] UnitFactory 등록, `UnitStatsConfig`, AttackProfile/Timeline, 실제 Animator Attack state clip, Animation Event와 VFX/SFX 연결을 함께 확인한다.
- [ ] 이 문서의 유닛 행, UnitType·프리팹·Animator·프리셋 집계, 감사 기준일을 갱신한다.
- [ ] `ValidateUnitCombatSetup.cs`의 `ExpectedPrefabCount`, 종족별 예상 수, UnitType 예상 수와 VFX/socket 기준선을 새 roster의 실제 의도에 맞춰 갱신한다. 단순히 검증을 느슨하게 하거나 오류를 무시하지 않는다.
- [ ] 전체 Validate/Dry Run에서 누락·중복 identity, 부분 migration, projector 참조, Animator Root Motion, 네트워크 설정 변화가 모두 0인지 확인한다.
- [ ] 같은 코드 리비전의 Unity Editor counterpart와 Android Development Build를 역할교대해 Host/Client·Blue/Red에서 Simulation Root 값의 일치와 Visual Root에만 적용되는 관점 변환을 확인한다.

B1 `SetupUnitVisualRoots.Apply`는 기존 50개 Legacy 프리팹을 한 번에 전환하기 위한 일회성 migration이다. 신규 프리팹마다 이 일괄 Apply를 다시 실행하지 않는다. 신규 프리팹은 처음부터 migrated 구조로 만들며, Legacy 구조를 가져온 경우 별도 단일 프리팹 설정 또는 수동 규격화 후 전체 검증을 통과시킨다. 고정 예상 수 때문에 신규 프리팹 추가 직후 검증기가 실패하는 것은 정상적인 fail-closed 동작이며, roster 문서와 검증 기준을 함께 검토하라는 신호다.

---

## 공격 프로필 및 준비 상태

`현재 Runtime`의 `TimerImpact`는 공통 `HitFrameTimes` 서버 타이머에서 즉시 결과를 적용하며 권위 발사체 비행·착탄 레코드가 없다는 뜻이다.

| 유닛 | 목표 프로필 | 현재 Runtime | 기본 Attack 이벤트 / 설정값 | 특수 처리 | 상태 및 핵심 위험 |
|---|---|---|---|---|---|
| Pistoleer | Hitscan · Single · Damage · Instant | TimerImpact | 1 @ 0.80s / 2.00s | 없음 | MigrationRequired — 이벤트/설정 불일치, hit/tracer VFX 없음 |
| Assault | Hitscan · Single · Damage · Instant | TimerImpact | 1 @ 0.1667s / 0.20s | 없음 | MigrationRequired — 30fps 기준 1 frame 경계로 marker 일치 판정 보류, Stats 0.33s와 런타임 cooldown 0.20s 충돌 |
| Sniper | Hitscan · Single · Damage · Instant | TimerImpact | 1 @ 1.7333s / 3.00s | 없음 | MigrationRequired — 이벤트/설정 불일치, hit/tracer VFX 없음 |
| LittleKnight | MeleeContact · Single · Damage · MultiImpact(2) | TimerImpact | 2 @ 0.25, 1.15s / 동일 | 없음 | MigrationRequired — marker는 일치, 규칙 v2 멀티 검증 필요 |
| SpearMan | MeleeContact · Single · Damage · Instant | TimerImpact | **현재 저장 1 @ 0.8333333s / type 4 설정 동일** (30fps `0:25`), cooldown 2s | 없음 | **0:25 focused 실기 PASS/CLOSED · MigrationRequired 별도 OPEN** — Game type 4 명시 Attack 연결·영구 production gate, Unity 두 self-validation PASS. 사용자 육안 수용. SpearMan 47+28기와 LittleKnight가 생산된 역할 교대 2경기에서 경기 전체 Host 결과 597/298·실패 0 ↔ Client 수락 597/298·거부 0, 양쪽 C3 ready 597/597·298/298, 필수 표현 474/474·271/271, 실패 0, local ROOT 양쪽 PASS, Host MOVE adapterFailures 0. 이 수치는 SpearMan 단독 집계가 아니다. 정확한 marker→권위 피해 offset·공식 CrossAudit Analyze·전체 25종·rollback/v2 migration 미검증; projectile/hit VFX 없음. 구 `0.24s`는 구현 전 이력 |
| BattleAxe | MeleeContact · **Single direct + Area/Cone** · Damage · Instant | TimerImpact + Sweep | 1 @ **1.02s** / **1.02s** | SweepAttackBehavior | **ImplementationReady · CONDITIONAL PASS** — production marker/config/controller/Blue·Red 프리팹 연결 영구 gate PASS. Focused Editor Host + Android Client에서 BattleAxe 양쪽 14기, 결과 420/420·필수 표현 440/440·AoE bundle 420/420·공간/이동/ROOT 오류 0. 렉 때문에 정확한 화면 접촉과 Sweep 아군·후방·범위 밖 제외 확인 전 Complete 아님 |
| Tank | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ 0.1667s / 4.00s | 없음 | Provisional + MigrationRequired — 서버 발사체 없음, 실기 분류 확인 필요 |
| CannonCart | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ 0.1667s / 4.00s | 없음 | Provisional + MigrationRequired — 서버 발사체 없음, 실기 분류 확인 필요 |
| EmberSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 실제 Attack 1 @ 1.00s / 설정 동일; clip 2.6666667s, 설정 폴백 cooldown 2.2s 유지 | 없음 | 명시 Attack GUID `335daaffd649791409558085159725aa`·production gate와 두 Unity 메뉴 PASS. 같은 세션 2경기 Ember 18+34기 생산, Host 경기 전체 결과 299/231·실패 0 ↔ Client 같은 수락·거부 0, 양 peer 필수 표현 264/264·196/196·실패 0; 사용자 육안 수용으로 **focused PASS/CLOSED**. 정확한 animation→피해 subframe 미계측·공식 CrossAudit 미실행. Supported 유지, MigrationRequired·25종/역할교대/rollback 미완 |
| FlameSpirit | MeleeContact · Single · Damage · MultiImpact(6) | TimerImpact | 6 @ **20/30, 35/30, 43/30, 50/30, 57/30, 63/30초** / 설정 동일 | 없음 | **Focused 실기 긍정 근거 + MigrationRequired/OPEN** — 09-29 같은 세션 실제 생산·Host 공격 시작 Ready/shadow commit, 사용자 “정상적으로 되는 것으로 보여”. Host 238 commit·723 결과/0 실패 ↔ Client 723 수락/0 거부, 양쪽 필수 표현 483/483·실패 0·ROOT local PASS. **723·483은 FlameSpirit/LittleKnight/Pistoleer 경기 전체 집계**; 여섯 HitIndex별 offset·정확한 subframe·25종/역할교대/rollback 미검증, v2 Complete 아님. 근거: `_Logs/_editor/2026-09-29/RuntimeLog.txt`, `_Logs/2026-09-29/00_12_logcat/RuntimeLog_device.txt`, 09-28 FlameSpirit Task의 09-29 후속 절 |
| InfernoSpirit | ProjectileImpact 잠정 · Single · Damage · ImpactThenPeriodic | TimerImpact + 단일 대상 DoT 구현 | 1 @ 0.50s / 0.50s | InfernoAttackBehavior | Focused visual PASS + MigrationRequired — 직접 25+유닛 전용 DoT 5/초×3초와 이동·방향·VFX는 실기 확인, Stop 선행 source VFX 11회 보존 및 Host/Client 실제 시작 64/64; 발사/착탄 분리와 LegacyFallback 이관은 미완료, Inferno 필드가 SpecialAttackConfig asset YAML에 미직렬화 |
| DustSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 실제 Attack 1 @ **1.04s**, 주기 **3초** / 설정 동일; Attack2 0 | 없음 | **ImplementationReady · CONDITIONAL PASS/OPEN** — 실제 Attack 명시 연결·순서 역전 자동 gate PASS. 09-28 실제 49기, 결과 각 250·필수 표현 각 236/236·실패/중복/공간/recovery 0. 육안 최종 수용 대기, MigrationRequired·전체 실기 미완 |
| BoulderSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | **현재 저장 1 @ 40/30s ≈ 1.3333333s / 설정 동일**, 30fps·4초 주기 | 없음 | **1:10 공격 시각 focused PASS/CLOSED · MigrationRequired/OPEN** — 단일 marker·type 14 fallback·production gate, 기존 명시 Attack 연결과 Unity 두 메뉴 PASS. 새 Android Host/Editor Client `0f0bbb32…923eb60`에서 Boulder 7기 생산, **Boulder+LittleKnight 경기 전체** Host/Client 결과 283/283·필수 표현 양쪽 265/265·오류 0; 사용자 “육안상 문제없었어” 수용. Boulder 단독 정확한 marker→피해 offset은 미계측. 이전 `06d034ab…e1a6c4`의 453/414·1.50초 화면 수용은 별도 이력이다. LittleKnight 과거 MOVE-AUTH 4건은 새 경기에서 0건이어도 원인 미확정 OPEN; 공식 cross-audit·25종/역할교대/rollback 미해결, v2 Complete 아님. 근거: 09-29 Boulder Task 최신 절 |
| QuakeSpirit | MeleeContact · **Single direct + Area/Circle** · Damage · Instant | TimerImpact + 즉발 스플래시 구현 | 기본 1 @ **1.667s** / **1.667s** | QuakeAttackBehavior | **ImplementationReady · CONDITIONAL PASS** — production marker/config/controller/Blue·Red 프리팹 연결과 `Supported / MeleeContact / Impact 1 / secondary true` 영구 gate PASS. Focused Editor Host + Android Client에서 Quake 10기, 결과 770/770·필수 표현 909/909·직접 200/스플래시 100·공간/이동/ROOT 오류 0. 화면 타격 프레임·아군/범위 밖 제외·체감 정지·비교 유닛 육안 회귀 전 Complete 아님 |
| TideSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | **현재 1 @ 1.50s / type 16 설정 동일**, 30fps `1:15`, 3초 길이·loop/cooldown | 없음 | **focused 시각 PASS/CLOSED · MigrationRequired/OPEN** — 사용자 저장 단일 marker, Game type 16 명시 Attack 연결·영구 gate, Unity 두 메뉴 PASS. 새 `ae04f2a9…e6bd03` Tide+Little 혼합 경기 전체 Host/Client 결과 160/160·필수 표현 양쪽 129/129·실패 0과 사용자 육안 수용. Tide 단독 exact offset·공식 CrossAudit·25종/역할교대/rollback은 미검증; LittleKnight 이전 MOVE 4건 별도 OPEN. 이전 `1 @ 1.15s / 동일`은 교정 전 이력, v2 Complete 아님. |
| StreamSpirit | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ **0.50s** / **0.50s** | 없음 | **Provisional + MigrationRequired · focused PASS** — production marker/config 정렬, exact asset gate와 두 Unity self-validation PASS. 2경기 Host VFX 603/603·Client 603/603, 실제 재생 실패·unmatched·duplicate·overflow 0. 서버 권위 발사체와 발사/착탄 exact-key는 미구현이므로 `Unresolved / LegacyFallback` 유지 |
| TorrentSpirit | TravelingArea · Area/Rectangle · Damage+Heal · ContactOncePerTarget | 서버 ActiveWave | 1 @ 0.50s / 동일 | TorrentAttackBehavior | MigrationRequired — 서버 전선은 유지, sequence/contact ID 이전 필요 |
| FoxMagician | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | VFX marker **1.00s** / 피해 설정 **2.25s** | 없음 | **Focused 사용자 수용 + MigrationRequired** — 09-28 양 peer 61쌍 같은 frame immediate/text-played, 최대 Host 7.097ms/Client 2.445ms. VFX 79/78·실패 0·미완료 각 1 원인 미확정. 약간 이른 현행 타이밍 수용, 재튜닝은 projectile 구현 시 이관. VFX 종료/exact 결합 미계측, Unresolved/LegacyFallback 유지 |
| BearGuard | MeleeContact · Single · Damage · Instant | TimerImpact | **현재 1 @ 0.43333334s / type 20 설정 동일**, 30fps `0:13`; cooldown 1.2s 유지 | 없음 | **0:13 focused 실기 PASS/CLOSED · MigrationRequired/OPEN** — Game type 20 명시 Attack 연결·영구 production gate·Unity 두 self-validation PASS. 09-30 2/25 혼합 재테스트 경기 전체 Host/Client 641/641 결과·양쪽 608/608 필수 표현·실패 0, local ROOT PASS, 사용자 육안 수용. 첫 경기 ROOT INCOMPLETE는 별도. 구 `1 @ 0.20s`와 빌드 시작 화면은 당시 이력. 설계 주기 `1:20` 대 실제 cooldown 1.2s, BearGuard 단독 offset·공식 CrossAudit·v2 Complete는 OPEN. |
| LionKnight | MeleeContact · Single · Damage · MultiImpact(2) | TimerImpact | **현재 저장 2 @ 0.6, 1.4333333s / type 22 설정 동일**, 30fps `0:18`, `1:13`; cooldown 3s | 없음 | **두 타격 focused 실기 PASS/CLOSED · MigrationRequired/OPEN** — Game 명시 연결·영구 production gate. 사용자 수용 `a8134504…c3aacb5b` Editor Host/Android Client의 LionKnight 8기·LittleKnight 22기 혼합 경기 전체 C2 216/216, 양측 C3 결과·ready 216/216·실패 0·묶음 216/216 방출·필수 표현 188/188, Client Expired 0, 양측 local ROOT PASS. 이전 `a2d9…c622225` Client C3 Expired 7건은 소급 해소되지 않은 **별도 결함 OPEN**이며 원인 미확정. LionKnight 단독 정확한 접촉/피해 offset·공식 CrossAudit·25종/역할교대/rollback 미완. 구 `0.22, 1.08s`는 교정 전 이력. |
| RhinoBreaker | MeleeContact · Single · Damage · Instant | TimerImpact | 기본 Attack 1 @ 1.05s / 동일; Attack2 1 @ 1.05s | 없음 | **ImplementationReady · CONDITIONAL PASS** — 실제 Animator `Attack` state marker 교정과 self-validation PASS. Blue/Red 유닛 공격, Blue 건물 공격, Host 724 ↔ Client 724 결과와 양쪽 필수 표현 692/692가 오류 0으로 수렴. 육안 접촉 시점·개별 대상 전환 및 다음 통합 빌드 확인 전 Complete 아님 |
| EagleArcher | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 기본 1 @ 0.10s / 동일; Attack2 0 | 없음 | Provisional + MigrationRequired — 서버 화살 없음, 복수 클립 위험 |
| RabbitTrickster | MeleeContact · Single · Damage · Instant | TimerImpact | 실제 Attack 1 @ **0.6666667s**(0:20/30fps), 주기 2초 / type 25 설정 동일; Attack3 0 | 없음 | 명시 연결·production gate 유지. 새 경기 `3fc75ef4…b7beded`에서 Rabbit 각 15기와 LittleKnight 각 13기 생산, 경기 전체 Host 138 schedules/129 results/0 failure ↔ Client 129 accepted/0 rejected, 양쪽 필수 표현 122/122·실패/중복/전송 실패 0; 사용자 육안 수용으로 **Rabbit focused PASS/CLOSED**. 정확한 marker→권위 피해 offset 미계측, 공식 Root CrossAudit 미실행. 이전 223/212·199/199는 0.18초 이력. Supported 유지, MigrationRequired·전체 실기 미완 |
| MushroomBomber | ProjectileImpact/LockedPoint · **Single direct + Area/Circle** · Damage · Instant+Periodic | **TimerImpact + 즉시 Blast/DoT 부여** | 기본 0 / 1.00s | BlastAttackBehavior | **Incomplete** — 문서상 착탄 의도와 현재 서버 판정 불일치, ImpactHitRadius·투사체·폭발 VFX 없음; v2 Migration도 필요 |
| BloomFairy | Hitscan cast 잠정 · Single · Heal · Periodic | Timer 기반 HoT | 기본 0 / 1.00s | 별도 Healer 경로 | Provisional + MigrationRequired — 회복은 동작, 표현 marker 없음, 4초 예외 유지 |

> 표의 설정값은 2026-07-22 `UnitStatsConfig.asset` 재감사값을 기준으로 하되 RhinoBreaker·QuakeSpirit 행은 2026-09-14, BattleAxe 행은 2026-09-20 후속 교정·focused 로그 검증으로 갱신했고, StreamSpirit 행은 2026-09-22 production timeline 교정과 Editor Host/Android Client focused 로그 검증 결과를 반영했다. FoxMagician은 2026-09-22에 설정을 marker `1.00초`로 정렬했으나, 2026-09-23 실기에서 VFX 시작과 피해가 동시에 발생해 사용자 설계와 충돌함을 확인했다. 2026-09-27 후속 교정은 피해 설정을 기존 2.25초로 복원하고 생성 시 marker가 이를 덮어쓰지 않도록 변경했다. **[2026-09-27 최신 로그 정정 — 이전 기록 보존]** 동일 `sharedSessionKey=67d84b7da7a97b206fe075c2e654380618b2be790896c23f4f164865a2c1d6a1`의 Host 종료 집계는 starts 250/markers·vfxStarted 246/timerImpacts 245/complete 242/incomplete 8, Client는 starts 250/markers·vfxStarted·complete 217/incomplete 33이다. 양쪽 VFX 실패·unmatched·duplicates·overflow·dropped는 0이고 ParticleSystem 양수·재생 활성은 각 VFX 시작 수와 같다. Host 상세 10회차 marker→timer 평균 1.255681초(실제 Applied 6회차 평균 1.250256초)로 시점 분리를 지지하지만 상세 64줄 상한과 Client 서버 timer 미관측 때문에 전 회차나 VFX 종료 장면을 증명하지 않는다. **2.25초는 새 실기에서 후반부 피해를 육안으로 확인하기 전의 기준값이며, 최종 PASS가 아니다.** 과거 BattleAxe 1.1667초, QuakeSpirit 1.00초 placeholder·marker 미주입, StreamSpirit 0.17초 기록은 당시 감사 이력으로만 보존한다. BattleAxe와 QuakeSpirit은 자동 gate와 focused Android 로그 축을 통과했지만 남은 육안 항목과 전체 roster 회귀 전이므로 규칙 v2 Complete를 의미하지 않는다. StreamSpirit도 VFX 누락 없이 focused PASS했지만 서버 권위 발사체, 발사/착탄 exact-key, full 25-type role-swap/rollback이 없어 `MigrationRequired / Unresolved`다. FoxMagician 역시 권위 발사체와 tracer가 없으므로 `MigrationRequired / Unresolved / LegacyFallback`을 유지한다.

---

## 복수 Attack 클립 위험

| 유닛 | 기본 클립 | 추가 클립 | 위험 |
|---|---|---|---|
| DustSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 실제 Attack 1 @ **1.04s**, 주기 **3초** / 설정 동일; Attack2 0 | 없음 | **ImplementationReady · CONDITIONAL PASS/OPEN** — 실제 Attack 명시 연결·순서 역전 자동 gate PASS. 09-28 실제 49기, 결과 각 250·필수 표현 각 236/236·실패/중복/공간/recovery 0. 육안 최종 수용 대기, MigrationRequired·전체 실기 미완 |
| RhinoBreaker | Attack: 이벤트 1 @ 1.05s | Attack2: 이벤트 1 @ 1.05s | 실제 Animator `Attack` state 확인·교정 완료. 두 marker는 현재 일치하지만 범용 첫 클립 선택 위험은 남음 |
| EagleArcher | Attack: 이벤트 1 | Attack2: 이벤트 0 | 컨트롤러 클립 순서 변경 시 타격점 손실 가능 |
| RabbitTrickster | Attack: 이벤트 1 @ **0.6666667s**(0:20/30fps), 2초 | Attack3: 이벤트 0, 1.1초 | 실제 UnitFactory 명시 연결 및 Attack3-first 회귀 gate 유지. 사용자 저장 marker·설정·gate 기대값 일치; 새 0:20 빌드 Host/Client 실기와 사용자 육안 수용으로 **focused PASS/CLOSED**. 정확한 시각 offset은 미계측. 이전 Unity PASS·실기는 0.18초 이력이고 과거 순서 의존 위험은 실기 오선택 관측을 뜻하지 않음 |

AttackTimeline은 클립 이름 부분 일치나 배열의 첫 항목으로 선택하지 않고, 공격 프로필이 실제 Animator state clip을 명시적으로 참조해야 한다.

---

## 완료 판정 체크리스트

유닛 한 종을 Complete로 올리려면 다음을 모두 만족해야 한다.

- [ ] 목표 Delivery / TargetScope / AreaShape / Effect / Schedule 확정
- [ ] UnitStatsConfig 및 AttackTimeline 존재
- [ ] 실제 Animator Attack state clip 명시적 연결
- [ ] 권위 `ActionMarkerOffset`과 Animation Event가 1 animation frame 이내로 일치
- [ ] 서버 Impact·취소·빗나감 규칙 구현
- [ ] 필요한 발사·비행·착탄·피격 VFX/SFX 연결
- [ ] Simulation Root / Visual Root 분리와 Blue/Red 방향 확인
- [ ] Host/Client 양쪽에서 이동·타겟·방향·Impact 일치
- [ ] 지연·지터·순서 역전·중복·늦은 스폰 검증
- [ ] 다수 유닛 및 사망·타겟 변경 회귀 검증

현재 25종 모두 최종 멀티 검증 완료 상태가 아니다. QuakeSpirit은 2026-09-14, BattleAxe는 2026-09-20 자동·focused Android 로그 gate CONDITIONAL PASS이며 육안 확인과 전체 roster 회귀가 남아 있다.
