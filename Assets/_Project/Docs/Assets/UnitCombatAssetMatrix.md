# Unit Combat Asset Matrix

유닛별 공격 설계 목표와 현재 런타임·애니메이션·VFX 준비 상태를 분리해 추적하는 감사 표다.

**감사 기준일:** 2026-07-20  
**브랜치:** `codex/unit-movement-attack-sync-audit`  
**범위:** 정적 에셋·설정·코드 연결 감사. 멀티플레이 실기 검증은 수행하지 않음.

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
| 기본 Attack 클립 `OnAttackHit` | 21/25 보유 |
| 기본 Attack 이벤트 누락 | QuakeSpirit, RhinoBreaker, MushroomBomber, BloomFairy |
| UnitStatsConfig | 24/25 존재 — QuakeSpirit 누락 |
| `attackPreset` | 9/25 연결 |
| `hitPreset` | 0/25 연결 |
| `tracerPreset` | 0/25 연결 |
| 규칙 v2 멀티 검증 완료 | 0/25 — 런타임 구현 전 |

`attackPreset` 연결 9종: Pistoleer, Assault, Sniper, Tank, CannonCart, InfernoSpirit, StreamSpirit, TorrentSpirit, FoxMagician.

---

## 공격 프로필 및 준비 상태

`현재 Runtime`의 `TimerImpact`는 공통 `HitFrameTimes` 서버 타이머에서 즉시 결과를 적용하며 권위 발사체 비행·착탄 레코드가 없다는 뜻이다.

| 유닛 | 목표 프로필 | 현재 Runtime | 기본 Attack 이벤트 / 설정값 | 특수 처리 | 상태 및 핵심 위험 |
|---|---|---|---|---|---|
| Pistoleer | Hitscan · Single · Damage · Instant | TimerImpact | 1 @ 0.80s / 2.00s | 없음 | MigrationRequired — 이벤트/설정 불일치, hit/tracer VFX 없음 |
| Assault | Hitscan · Single · Damage · Instant | TimerImpact | 1 @ 0.1667s / 0.20s | 없음 | MigrationRequired — 30fps 기준 1 frame 경계로 marker 일치 판정 보류, Stats 0.33s와 런타임 cooldown 0.20s 충돌 |
| Sniper | Hitscan · Single · Damage · Instant | TimerImpact | 1 @ 1.7333s / 3.00s | 없음 | MigrationRequired — 이벤트/설정 불일치, hit/tracer VFX 없음 |
| LittleKnight | MeleeContact · Single · Damage · MultiImpact(2) | TimerImpact | 2 @ 0.25, 1.15s / 동일 | 없음 | MigrationRequired — marker는 일치, 규칙 v2 멀티 검증 필요 |
| SpearMan | MeleeContact · Single · Damage · Instant | TimerImpact | 1 @ 0.24s / 동일 | 없음 | MigrationRequired — 규칙 v2 멀티 검증·hit VFX 필요 |
| BattleAxe | MeleeContact · **Single direct + Area/Cone** · Damage · Instant | TimerImpact + Sweep | 1 @ **1.02s** / **1.1667s** | SweepAttackBehavior | MigrationRequired — 과거 1.1667s 완료 기록과 실제 클립 불일치 |
| Tank | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ 0.1667s / 4.00s | 없음 | Provisional + MigrationRequired — 서버 발사체 없음, 실기 분류 확인 필요 |
| CannonCart | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ 0.1667s / 4.00s | 없음 | Provisional + MigrationRequired — 서버 발사체 없음, 실기 분류 확인 필요 |
| EmberSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 1 @ 1.00s / 동일 | 없음 | MigrationRequired — 규칙 v2 멀티 검증 필요 |
| FlameSpirit | MeleeContact · Single · Damage · MultiImpact(6) | TimerImpact | 6 @ 0.20, 1.05, 1.13, 1.20, 1.28, 2.03s / 동일 | 없음 | MigrationRequired — 다중 타격 상관관계 검증 필요 |
| InfernoSpirit | ProjectileImpact 잠정 · Single · Damage · ImpactThenPeriodic | TimerImpact, DoT 미구현 | 1 @ 0.50s / 1.15s | Registry 미등록 | **Incomplete** — delivery·DoT 핸들러·타임라인 불일치 |
| DustSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 기본 1 @ 1.04s / 동일; Attack2 0 | 없음 | Provisional + MigrationRequired — 복수 Attack 클립 선택 순서 위험 |
| BoulderSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 1 @ 1.15s / 동일 | 없음 | MigrationRequired — 규칙 v2 멀티 검증 필요 |
| QuakeSpirit | MeleeContact GroundImpact 잠정 · Area/Circle · Damage · Instant | 폴백 가능성, AoE 미구현 | 기본 0 / **UnitStatsConfig 없음** | Registry 미등록 | **Incomplete / Critical** — 런타임 폴백 스탯 위험, 타격점·범위 로직 누락 |
| TideSpirit | MeleeContact · Single · Damage · Instant | TimerImpact | 1 @ 1.15s / 동일 | 없음 | MigrationRequired — 규칙 v2 멀티 검증 필요 |
| StreamSpirit | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ 0.50s / 0.17s | 없음 | Provisional + MigrationRequired — 서버 발사체·타임라인 교정 필요 |
| TorrentSpirit | TravelingArea · Area/Rectangle · Damage+Heal · ContactOncePerTarget | 서버 ActiveWave | 1 @ 0.50s / 동일 | TorrentAttackBehavior | MigrationRequired — 서버 전선은 유지, sequence/contact ID 이전 필요 |
| FoxMagician | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 1 @ 1.00s / 2.25s | 없음 | Provisional + MigrationRequired — 서버 발사체·타임라인 교정 필요 |
| BearGuard | MeleeContact · Single · Damage · Instant | TimerImpact | 1 @ 0.20s / 동일 | 없음 | MigrationRequired — 규칙 v2 멀티 검증 필요 |
| LionKnight | MeleeContact · Single · Damage · MultiImpact(2) | TimerImpact | 2 @ 0.22, 1.08s / 동일 | 없음 | MigrationRequired — 다중 타격 상관관계 검증 필요 |
| RhinoBreaker | MeleeContact · Single · Damage · Instant | TimerImpact | 기본 Attack 0 / 1.05s; Attack2 1 @ 1.05s | 없음 | **Incomplete** — Animator Attack state와 이벤트 클립 분리 가능성 |
| EagleArcher | ProjectileImpact 잠정 · Single · Damage · Instant | TimerImpact | 기본 1 @ 0.10s / 동일; Attack2 0 | 없음 | Provisional + MigrationRequired — 서버 화살 없음, 복수 클립 위험 |
| RabbitTrickster | MeleeContact · Single · Damage · Instant | TimerImpact | 기본 1 @ 0.18s / 동일; Attack3 0 | 없음 | Provisional + MigrationRequired — 복수 Attack 클립 선택 순서 위험 |
| MushroomBomber | ProjectileImpact/LockedPoint · **Single direct + Area/Circle** · Damage · Instant+Periodic | **TimerImpact + 즉시 Blast/DoT 부여** | 기본 0 / 1.00s | BlastAttackBehavior | **Incomplete** — 문서상 착탄 의도와 현재 서버 판정 불일치, ImpactHitRadius·투사체·폭발 VFX 없음; v2 Migration도 필요 |
| BloomFairy | Hitscan cast 잠정 · Single · Heal · Periodic | Timer 기반 HoT | 기본 0 / 1.00s | 별도 Healer 경로 | Provisional + MigrationRequired — 회복은 동작, 표현 marker 없음, 4초 예외 유지 |

> 표의 설정값은 2026-07-20 `UnitStatsConfig.asset` 정적 감사값이다. 값이 이벤트 시간과 다른 항목은 의도된 폴백인지 잘못된 타임라인인지 실기·에디터 검증 전 확정하지 않는다.

---

## 복수 Attack 클립 위험

| 유닛 | 기본 클립 | 추가 클립 | 위험 |
|---|---|---|---|
| DustSpirit | Attack: 이벤트 1 | Attack2: 이벤트 0 | 이름에 `Attack`이 포함된 첫 클립 선택에 의존 |
| RhinoBreaker | Attack: 이벤트 0 | Attack2: 이벤트 1 | 실제 Animator Attack state와 추출 대상이 다를 가능성 |
| EagleArcher | Attack: 이벤트 1 | Attack2: 이벤트 0 | 컨트롤러 클립 순서 변경 시 타격점 손실 가능 |
| RabbitTrickster | Attack: 이벤트 1 | Attack3: 이벤트 0 | 컨트롤러 클립 순서 변경 시 타격점 손실 가능 |

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

현재 25종 모두 규칙 v2 구현 전이므로 최종 멀티 검증 완료 상태가 아니다.
