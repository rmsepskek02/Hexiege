# Unit Combat Asset Matrix

> **목적·답하는 질문:** 유닛별 공격 에셋·생산 연결·구조 이행·개별 테스트 수용을 나눠 보고 무엇이 실제 잔여인지 판단하는 감사표다.
> **포함:** 공격 프로필·클립·이벤트·설정·특수 처리·VFX 연결, 유닛별 현재 수용 범위와 미확정·보류·전역 이행 상태, 신규 프리팹 확인 조건과 짧은 근거 링크를 담는다. 개별 수용과 전체 구조 이행은 독립 축이다.
> **제외·담당 문서:** 공격 동작은 [유닛 규칙](../GameSystemRules/GameSystemRules_Units.md), 권위·시간·표현 계약은 [전투 동기화 규칙](../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md), 수치는 [StatsReference](../StatsReference.md), 효과 제작·연결 현황은 [VFXSFXList](VFXSFXList.md)가 담당한다. 세션별 컴파일·해시·빌드 클릭·긴 경기 집계는 Task/History에 둔다.
> **권위원본·주요 참조:** 이 문서는 유닛별 연결·수용 상태의 감사표이며 게임 규범 원본이 아니다. 실제 Config·UnitFactory의 생산 선택·Animator 클립·Inspector 연결과 작업별 검증 근거를 구분해 대조한다.
> **갱신 조건·방법:** 실제 연결·구현 이행·검증 범위·사용자 수용이 바뀐 행을 제자리 갱신한다. 날짜별 PASS를 쌓거나 과거 MigrationRequired를 새 미테스트 판정으로 읽지 않으며, 원문은 이력으로 보존하고 근거 링크를 남긴다.

## 현행 개별 테스트·후속 목록 (2026-10-11 사용자 정정)

이 절과 아래 유닛별 표가 현재 수용·잔여의 기준이다. 개별 수용은 전역 구조 이행과 독립이며, MigrationRequired/conditional은 미테스트 판정이 아니다.

- **사용자 개별 수용 유지:** RhinoBreaker, QuakeSpirit, BattleAxe, DustSpirit, LittleKnight, EagleArcher. LittleKnight 다음 추천은 철회됐으며 확인된 현행 공격 결함은 없다.
- **기존 focused 수용 유지:** Assault, Sniper, InfernoSpirit, SpearMan, EmberSpirit, FlameSpirit, BoulderSpirit, TideSpirit, StreamSpirit, BearGuard, LionKnight, RabbitTrickster. FoxMagician의 이전 2.25초 구현 수용은 현행 복원 세대의 수용으로 확대하지 않는다.
- **별도 후속:** Pistoleer의 기존 개별 테스트, Tank/CannonCart의 테스트 완료·세부 VFX 보류, TorrentSpirit의 이전 개별 수용을 유지한다. Torrent 연결 복구·회귀 확인은 별도다.
- **시각 확인 후보:** 연결된 Mushroom tracer VFX(포물선 궤적 미구현 후속 포함). *(2026-10-15 갱신, 2026-10-19 병합 반영: 종전 후보 중 Mushroom impact·poison은 사용자 승인, BloomFairy 힐(PS 4개)은 재생 사용자 PASS, 상태 이펙트 크기는 프리셋 값 조정 진행 중 — [vfx-code-followups Research](../_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md) 10절. 종전 문구: 「연결된 Mushroom tracer/impact/poison 및 Bloom recipient VFX」)* HumanTower의 actual Assault 교체 후 시각 확인은 건물 후속이다. [VFX 현황](VFXSFXList.md).
- **공통 미확정·보류:** overall Invalid 진단 scope/origin, 과거 Editor Client63 표현 만료 원인·해결, dead-abort 실기 미재현, 전체 migration은 OPEN이다. 랠리 목적지에 이동 도중 건설할 때의 정지 문제는 [기존 랠리 Task](../_Tasks/2026-10-13/10_37_rally-point-fix/Research.md)에서 사용자 싱글·멀티 확인 범위로 수용했다. 전체 경로 차단 대응은 별도이며 개별 공격 수용을 다시 여는 gate가 아니다.

**싱글 공통 이동 복귀:** 사용자 새 두 경기 육안 수용과 runtime 분석 범위에서 SOLO focused PASS/CLOSED다. Lion/Rhino 직접 resume와 Rabbit pursuit 관측은 구분하며, Rabbit target-loss resume 미관측만으로 재시험을 요구하지 않는다. 광산·랠리, native 메뉴·25종·멀티·전체 동등성은 이 종결에 포함하지 않는다. [상세](../_Tasks/2026-10-14/10_04_post-merge-unit-regression-audit/Research.md#solo-return-two-match-acceptance).

**Fox 현행 복원:** charge `OnAttackCharge @ 1초`와 damage `OnAttackHit @ 85/30초`를 분리했다. Factory 양 모드 피해 추출/fallback 정합과 controlled 검증을 확인했으며, 새 싱글 초월8종 관측에서는 Fox 조기 피해·근접 불발의 재발 근거가 없다. charge VFX/SFX·정확한 native 예약 owner 연결·픽셀/전체 시각 수용은 미확정이고 cap·종료 진단 경고 한계도 유지한다. General23 gate·native lease 이행·멀티 gate 복사·새 overlap은 철회된 범위 확장이다. 기존 멀티 zeroAim/InvalidPose와 SOLO Legacy 차이는 복원의 새 정책 blocker가 아니다. [현행 근거](../_Tasks/2026-10-14/10_04_post-merge-unit-regression-audit/Research.md#solo-match-2026-10-15).

### 6종 실제 구현 감사 최종 근거 (2026-10-11)

감사 대기는 해제됐다. RhinoBreaker·QuakeSpirit·BattleAxe·DustSpirit·LittleKnight는 기존 교정 작업이 확인됐고, EagleArcher는 기존 연결·테스트만 확인됐다. Eagle의 최신 전용 공격 교정·Supported 승격은 확인되지 않았으나 사용자 수용을 취소하거나 자동 재테스트를 요구하지 않는다.

| 유닛 | 확인된 연결·검사 범위 | 상세 근거 |
|---|---|---|
| RhinoBreaker | Blue/Red Attack·1.05초 marker/fallback, Supported 1. 전용 production asset gate는 발견되지 않음 | [교정 Plan](../_Tasks/2026-09-13/20_34_rhino-breaker-impact-marker-correction/Plan.md) |
| QuakeSpirit | Blue/Red Attack·1.667초, 실제 asset/config gate | [교정 Plan](../_Tasks/2026-09-14/00_56_quake-spirit-impact-marker-correction/Plan.md) |
| BattleAxe | Blue/Red Attack·1.02초, production asset/config gate | [교정 Plan](../_Tasks/2026-09-19/23_06_battleaxe-impact-marker-correction/Plan.md) |
| DustSpirit | Blue/Red 명시 Attack·1.04초, Attack2-first/명시 선택 gate | [교정 Plan](../_Tasks/2026-09-27/10_54_dustspirit-explicit-attack-clip-binding/Plan.md) |
| LittleKnight | fallback .25/1.15초·Supported 2, 연속 timeline wait/no restart·atomic cooldown·first-boundary gate | [공통 교정 Research](../_Tasks/2026-09-13/11_41_attack-entry-target-payload-seam/Research.md) |
| EagleArcher | fallback .1초·Unresolved Projectile/Legacy. 전용 production timeline gate는 발견되지 않음 | [기존 pipeline Plan](../_Tasks/2026-07-22/13_26_unit-action-sequence-implementation/Plan.md) |

위 연결은 programmer 감사, Main 독립 확인은 Factory 선택·resolver·LittleKnight 공통 근거와 config3종으로 범위가 다르다. 새 전체 source/clip·native·raw-log 감사나 global migration PASS를 뜻하지 않는다. 이전 감사 대기·날짜별 수용·경기·컴파일·빌드 인계 원문과 소스 범례는 [상세 이력](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-01)에 보존한다.

## 상태 정의

### 상태를 읽는 축

| 축 | 이 문서의 근거 위치 | 해석 |
|---|---|---|
| 에셋 준비·연결 | 공격 이벤트/설정값·특수 처리, VFXSFXList | 파일 존재·연결은 시각 수용과 별개 |
| 구현·구조 이행 | 목표 프로필·현재 Runtime·MigrationRequired | TimerImpact와 목표 프로필의 차이를 보존; 미테스트 판정이 아님 |
| 자동 검증 | 유닛별 근거 링크·상세 이력 | 실행된 fixture 범위이며 실기·픽셀·전체 이행을 확정하지 않음 |
| 사용자 실기 수용 | 현행 개별 테스트·후속 목록 | 기존 개별 수용을 유지; 새 VFX·공통 진단·랠리 후속은 별도 |

여러 축이 같은 칸에 적힌 항목은 이 구분으로 읽으며, 한 축의 PASS로 다른 축의 OPEN을 지우지 않는다. 현재 표는 현행 상태를 읽는 요약이고 과거 행·경기 집계 원문은 연결된 이력에 보존한다.

- **Complete:** 규칙 v2 프로필, 서버 Impact, 에셋, Blue/Red, 멀티 검증 완료
- **MigrationRequired:** 기존 공격은 동작하지만 ActionSequence·서버 Impact 구조로 이전 필요
- **Incomplete:** 필수 스탯·특수 로직·Attack 클립·표현 자산 중 하나 이상 누락
- **Provisional:** 목표 전달 방식이나 에셋 타격점의 실기 확인 필요

둘 이상의 문제가 겹치면 상태를 함께 병기한다. 우선순위는 `Incomplete/Critical → Incomplete → Provisional → MigrationRequired → Complete` 순이며, 상위 상태가 해결되기 전에는 Complete로 올리지 않는다.

프리팹이나 Animation Event가 존재한다는 이유만으로 Complete로 판정하지 않는다.

---

## 공통 감사 결과

현재 연결·수용·구조 이행의 범위는 [현행 개별 테스트·후속 목록](#현행-개별-테스트후속-목록-2026-10-11-사용자-정정)과 [25종 공격 프로필 표](#공격-프로필-및-준비-상태)를 따른다. 이전 공통 집계는 [과거 감사 기준선](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-04)에 보존하며 현재 전체 집계로 사용하지 않는다. 이번 정리는 최신 25종 집계를 새로 확정하지 않는다.

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
- [ ] 새 roster의 실제 의도에 맞춰 필요한 구조·연결 검사와 VFX/socket 기준선을 검토한다. 별도 `ValidateUnitCombatSetup` 도구는 삭제됐으며, 남긴 전투 검사와 현재 에셋 authoring에서 확인할 조건을 빼거나 오류를 무시하지 않는다.
- [ ] 누락·중복 identity, 부분 migration, projector 참조, Animator Root Motion과 네트워크 설정을 확인한다. 삭제된 과거 Validate/Dry Run 메뉴의 재실행을 요구하지 않는다.
- [ ] 같은 코드 리비전의 Unity Editor counterpart와 Android Development Build를 역할교대해 Host/Client·Blue/Red에서 Simulation Root 값의 일치와 Visual Root에만 적용되는 관점 변환을 확인한다.

B1의 과거 일괄 VisualRoot migration/rollback 도구는 삭제됐다. 신규 프리팹은 처음부터 migrated 구조로 만들고 Legacy 구조를 가져왔으면 현재 규격에 맞춰 수동 authoring한 뒤 필요한 구조·연결 조건을 확인한다. 남긴 검사기의 회귀 조건은 완화하지 않으며, 삭제된 도구 전체가 남긴 검사기로 완전히 대체됐다고 주장하지 않는다. 도구 정리 경위는 [기존 작업 기록](../_Tasks/2026-10-15/23_18_editor-script-organization/Research.md)을 따른다.

---

## 공격 프로필 및 준비 상태

`현재 Runtime`의 `TimerImpact`는 공통 `HitFrameTimes` 서버 타이머에서 즉시 결과를 적용하며 권위 발사체 비행·착탄 레코드가 없다는 뜻이다.

| 유닛 | 목표 프로필 | 현재 Runtime | 기본 Attack 이벤트 / 설정값 | 특수 처리 | 상태 및 핵심 위험 |
|---|---|---|---|---|---|
| Pistoleer | Hitscan · Single · Damage · Instant | TimerImpact | 1 @ 0.80s / 설정 0.80s; 주기 2.00s | 없음 | 기존 개별 테스트 기록 유지. MigrationRequired; 정확 타이밍·픽셀·전체 이행 OPEN. 공통 진단을 새 유닛 미테스트로 분류하지 않는다. [작업 근거](../_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md) |
| Assault | Hitscan · Single · Damage · Instant | TimerImpact | 1 @ 5/30s / clip-derived 10/30s (fallback 0.20s) | 없음 | focused 로그·사용자 수용 유지 / MigrationRequired. 전체 Invalid·과거63 원인 및 정밀 pixel/global migration OPEN. 이전 0:33 주기 의미 미확정. [작업 근거](../_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md) |
| Sniper | Hitscan · Single · Damage · Instant | TimerImpact | 승인 clip 1 @ 52/30s(1:22) / 3.00s; Stats 정합화 구현 | 현재 clip marker30fps1:22 보존 | focused 로그·사용자 수용 유지 / MigrationRequired. 52/30초·주기3초 정합화 구현. 정밀 pixel/global migration·전체 Invalid·과거63 OPEN. [작업 근거](../_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md) |
| LittleKnight | MeleeContact · Single · Damage · MultiImpact(2) | TimerImpact | 2 @ 0.25, 1.15s / 동일 | 없음 | 사용자 개별 수용 유지 / MigrationRequired. 두 marker 정합 및 공통 continuous gate 확인. 전용 Task 부재는 교정 부재가 아니며 전역·정밀 측정과 별도. [작업 근거](../_Tasks/2026-09-13/11_41_attack-entry-target-payload-seam/Research.md) |
| SpearMan | MeleeContact · Single · Damage · Instant | TimerImpact | **현재 저장 1 @ 0.8333333s / type 4 설정 동일** (30fps `0:25`), cooldown 2s | 없음 | 0:25 focused 실기 PASS/CLOSED / MigrationRequired. 명시 Attack·production gate 유지. 정확 offset·공식 CrossAudit·25종·rollback/v2 이행 미검증; projectile/hit VFX 없음. [작업 근거](../_Tasks/2026-09-30/18_02_spearman-attack-timing-correction/Research.md) |
| BattleAxe | MeleeContact · **Single direct + Area/Cone** · Damage · Instant | TimerImpact + Sweep | 1 @ **1.02s** / **1.02s** | SweepAttackBehavior | 사용자 개별 수용 유지 / ImplementationReady·MigrationRequired. production 연결·영구 gate와 focused 로그 확인. 개별 수용을 전체 v2 Complete로 확대하지 않는다. [작업 근거](../_Tasks/2026-09-19/23_06_battleaxe-impact-marker-correction/Research.md) |
| Tank | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ 0.1667s / 4.00s | 없음 | 사용자 개별 테스트 완료 / Provisional·MigrationRequired. 서버 발사체 없음. shared Cannon preset의 actual Tank 연결 유지. 간헐 VFX·원인 진단/세부 후속 보류. [작업 근거](../_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md) |
| CannonCart | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ 0.1667s / 4.00s | 없음 | 사용자 개별 테스트 완료 / Provisional·MigrationRequired. 서버 발사체 없음. Tank와 actual prefab 공유. 간헐 VFX·원인 진단/세부 후속 보류. [작업 근거](../_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md) |
| EmberSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 실제 Attack 1 @ 1.00s / 설정 동일; clip 2.6666667s, 설정 폴백 cooldown 2.2s 유지 | 없음 | focused PASS/CLOSED·사용자 육안 수용 / Supported·MigrationRequired. 명시 Attack·production gate 유지. 정확 subframe·공식 CrossAudit·전체25종/rollback 미완. [작업 근거](../_Tasks/2026-09-28/21_05_ember-spirit-attack-correction/Research.md) |
| FlameSpirit | MeleeContact · Single · Damage · MultiImpact(6) | TimerImpact | 6 @ **20/30, 35/30, 43/30, 50/30, 57/30, 63/30초** / 설정 동일 | 없음 | focused 실기 긍정·사용자 정상 관측 / MigrationRequired·OPEN. 여섯 HitIndex별 exact offset·subframe·전체25종/역할교대/rollback 미검증. v2 Complete 아님. [작업 근거](../_Tasks/2026-09-28/21_55_flamespirit-multihit-attack-correction/Research.md) |
| InfernoSpirit | ProjectileImpact 잠정 · Single · Damage · ImpactThenPeriodic | TimerImpact + 단일 대상 DoT 구현 | **현재 승인 1 @ 1.50s / 설정1.50s, 1:15@30fps·주기3초** | InfernoAttackBehavior | focused 로그·사용자 수용 / MigrationRequired. 현재 승인1.5초·주기3초, 직접 피해25+유닛 전용 DoT5/초×3초. 발사/착탄 분리·LegacyFallback 이관 미완, Config YAML 미직렬화 경고 유지. burn fitting 보류; Invalid 원인 유닛 단정 금지. [작업 근거](../_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md) |
| DustSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 실제 Attack 1 @ **1.04s**, 주기 **3초** / 설정 동일; Attack2 0 | 없음 | 사용자 개별 수용 유지 / ImplementationReady·MigrationRequired. 명시 Attack 선택·순서 역전 gate 유지. 전체 이행/정밀 측정과 별도. [작업 근거](../_Tasks/2026-09-27/10_54_dustspirit-explicit-attack-clip-binding/Research.md) |
| BoulderSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | **현재 저장 1 @ 40/30s ≈ 1.3333333s / 설정 동일**, 30fps·4초 주기 | 없음 | 1:10 focused PASS/CLOSED / MigrationRequired·OPEN. 명시 Attack·production gate 유지. 단독 exact offset·공식 CrossAudit·전체25종/rollback 미검증. 과거 LittleKnight MOVE 원인은 별도 OPEN. [작업 근거](../_Tasks/2026-09-29/03_13_boulderspirit-attack-correction/Research.md) |
| QuakeSpirit | MeleeContact · **Single direct + Area/Circle** · Damage · Instant | TimerImpact + 즉발 스플래시 구현 | 기본 1 @ **1.667s** / **1.667s** | QuakeAttackBehavior | 사용자 개별 수용 유지 / ImplementationReady·MigrationRequired. Supported/MeleeContact/Impact1/secondary true 및 production gate 확인. 전체 v2 Complete와 별도. [작업 근거](../_Tasks/2026-09-14/00_56_quake-spirit-impact-marker-correction/Research.md) |
| TideSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | **현재 1 @ 1.50s / type 16 설정 동일**, 30fps `1:15`, 3초 길이·loop/cooldown | 없음 | focused 시각 PASS/CLOSED / MigrationRequired·OPEN. 명시 Attack·production gate 유지. 단독 exact offset·공식 CrossAudit·전체25종/역할교대/rollback 미검증. [작업 근거](../_Tasks/2026-09-29/19_09_tidespirit-attack-correction/Research.md) |
| StreamSpirit | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ **0.50s** / **0.50s** | 없음 | focused PASS / Provisional·MigrationRequired. production marker/config·asset gate 확인. 권위 발사체·발사/착탄 exact-key 미구현: Unresolved/LegacyFallback 유지. [작업 근거](../_Tasks/2026-09-22/12_13_streamspirit-production-timeline-correction/Research.md) |
| TorrentSpirit | TravelingArea · Area/Rectangle · Damage+Heal · ContactOncePerTarget | 서버 ActiveWave | 사용자 저장 발사 marker **1 @ 2:10(30fps)=70/30s** / type 18 `hitFrameTimes: [2.3333333]` | TorrentAttackBehavior | 이전 개별 작업 사용자 수용 유지 / MigrationRequired·Unresolved/LegacyFallback. 명시 Attack·발사 gate 유지. 발사 뒤 waveTravelTime0.5초 동안 서버 전선 접촉별 피해·힐. 연결 복구/회귀와 contact identity 이행·정밀 시각은 별도 후속. [작업 근거](../_Tasks/2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Research.md) |
| FoxMagician | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | charge `OnAttackCharge` **1s** / damage `OnAttackHit` 및 fallback **85/30s ≈2.8333333s** | 없음 | 현재 charge1초/damage85/30초 복원 구현·controlled GREEN / MigrationRequired·Unresolved/LegacyFallback. charge는 source VFX/SFX만, damage는 metadata만이며 charge lease/VFX 중복 없음. 기존2.25초 수용은 이전 세대. 새 싱글 focused 관측을 native owner·charge 표현·멀티/전체 시각 수용으로 확대하지 않는다. [작업 근거](../_Tasks/2026-10-14/10_04_post-merge-unit-regression-audit/Research.md) |
| BearGuard | MeleeContact · Single · Damage · Instant | TimerImpact | **현재 1 @ 0.43333334s / type 20 설정 동일**, 30fps `0:13`; cooldown 1.2s 유지 | 없음 | 0:13 focused 실기 PASS/CLOSED / MigrationRequired·OPEN. 명시 Attack·production gate 유지. 정확 offset·공식 CrossAudit·전체25종/rollback과 과거 LittleKnight MOVE 원인 별도 OPEN. 설계1:20 대비 실제 cooldown1.2초 차이 유지. [작업 근거](../_Tasks/2026-09-29/23_28_bearguard-attack-timing-correction/Research.md) |
| LionKnight | MeleeContact · Single · Damage · MultiImpact(2) | TimerImpact | **현재 저장 2 @ 0.6, 1.4333333s / type 22 설정 동일**, 30fps `0:18`, `1:13`; cooldown 3s | 없음 | 두 타격 focused 실기 PASS/CLOSED / MigrationRequired·OPEN. 명시 Attack·production gate 유지. 과거 C3 만료 원인·정확 offset·공식 CrossAudit·전체25종/rollback 별도 OPEN. [작업 근거](../_Tasks/2026-09-30/01_21_lionknight-attack-timing-correction/Research.md) |
| RhinoBreaker | MeleeContact · Single · Damage · Instant | TimerImpact | 기본 Attack 1 @ 1.05s / 동일; Attack2 1 @ 1.05s | 없음 | 사용자 개별 수용 유지 / ImplementationReady·MigrationRequired. 실제 Attack marker 교정·Supported1 확인. 전체 이행·정밀 측정과 별도. [작업 근거](../_Tasks/2026-09-13/20_34_rhino-breaker-impact-marker-correction/Research.md) |
| EagleArcher | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 기본 1 @ 0.10s / 동일; Attack2 0 | 없음 | 사용자 개별 수용 유지 / Provisional·MigrationRequired. 서버 화살 없음·복수 클립 위험. 기존 연결·테스트 확인이며 최신 전용 교정·Supported 승격 증거는 없음. [작업 근거](../_Tasks/2026-07-22/13_26_unit-action-sequence-implementation/Research.md) |
| RabbitTrickster | MeleeContact · Single · Damage · Instant | TimerImpact | 실제 Attack 1 @ **0.6666667s**(0:20/30fps), 주기 2초 / type 25 설정 동일; Attack3 0 | 없음 | 0:20 focused PASS/CLOSED·사용자 육안 수용 / MigrationRequired. 명시 Attack·Attack3-first gate 유지. 정확 offset·공식 CrossAudit·전체25종/rollback 미검증. [작업 근거](../_Tasks/2026-09-28/10_03_rabbittrickster-explicit-attack-clip-binding/Research.md) |
| MushroomBomber | ProjectileImpact/LockedPoint · **Single direct + Area/Circle** · Damage · Instant+Periodic | **TimerImpact + 즉시 Blast/DoT 부여** | 현재 저장 OnAttackHit 1 @ 1.00s / 설정 동일; 3초 클립·주기 유지 | BlastAttackBehavior | 새 tracer/impact/poison 연결 구현·typed Impact 전달/양 peer 재생 관측 / Incomplete·MigrationRequired. 기존 [VFX 후속 작업](../_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md)의 착탄 impact·새 poison 프리팹 사용자 승인을 반영한다. tracer·공통 시간 경계·전체 lifecycle 수용 OPEN. LateResultNoFlight+ImpactPlay는 impact 소실 아님. poison 조기 Stop·forced Teardown은 공통 시간 경계 OPEN이며 고유 결함/자동 rollback 근거 아님. 권위 착탄/ImpactHitRadius·전체 lifecycle 수용 미확정. [작업 근거](../_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md) |
| BloomFairy | Hitscan cast 잠정 · Single · Heal · Periodic | Timer 기반 HoT | **기본 1 @ 1.000s / timer 1.000s**, 30fps `1:00`; 4초 클립(30fps `4:00`, native retime 적용), 발동 후 cooldown3초/실제 주기4초 | 별도 Healer 경로 · NotApplicableAttack/SeparateHealPipeline | Provisional·MigrationRequired / NotApplicableAttack·SeparateHealPipeline 유지. marker1초·clip4초·양팀 heal blend0.15, 준비1+post3=cycle4·HoT200/3초. atomic snapshot·late-init·회차 멱등·terminal 차단, future-start IsDue·등록 기준 duration 교정 확인. speed1·서버 Action/priority1 회전, Client Root/Visual writer 금지. 다음 유효 target은 inactive 없이 연결하며 Frozen/Held·대기·사망·경기 종료는 유지. healPreset 연결·exact cast/recipient playback 관측; forced cleanup·사망 Unknown은 자연 종료/힐 손실 증거 아님. visual·정밀 facing·Client HP/text 전수·skill/movement-replaced 범위와 전체 수용 OPEN. [작업 근거](../_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Research.md) |

현행 수치·생산 선택의 세부 경계는 [StatsReference](../StatsReference.md)를 따른다. 표의 TimerImpact/Legacy 표기는 목표 구조 이행 축이며 개별 수용을 취소하지 않는다. 이전 행·집계·빌드 시도 원문은 [공격 프로필 상세 이력](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-02)에 보존한다.

<a id="2026-10-11-현행3행-정정-전-보존-이력"></a>

이전 Assault·Sniper·Inferno 세 행은 [당시 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-02), 현재 판정은 위 표를 따른다.

## 복수 Attack 클립 위험


| 유닛 | 기본 클립 | 추가 클립 | 위험 |
|---|---|---|---|
| DustSpirit | Attack: 이벤트 1 @ 1.04s, 명시 연결 | Attack2: 이벤트 0 | 명시 Attack·Attack2-first gate 유지. 사용자 수용은 위 현행 표를 따르며 이전 중복 행은 [이력](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-matrix-03) 참조 |
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

현재 25종 모두 최종 멀티 검증 완료 상태가 아니다. QuakeSpirit·BattleAxe의 기존 자동·focused Android 로그 근거와 이후 사용자 개별 수용은 유지하며, 이를 전체 roster 이행·정밀 측정·모든 체크 항목의 완료로 확대하지 않는다.
