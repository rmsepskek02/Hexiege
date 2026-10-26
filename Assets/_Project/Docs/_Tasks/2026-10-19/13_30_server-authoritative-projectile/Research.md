# Research — 서버 권위 투사체 (옵션 C)

> 작성 2026-10-19 · 기준 checkout HEAD `ab9d55d7` · 원 조사: game-programmer 읽기 전용 기술 조사 보고서(HEAD `657a4f31` 기준. 그 뒤 커밋 `344dfa42`·`ab9d55d7`은 문서와 미사용 VFX 에셋 삭제뿐이라 행 번호는 유효하다고 보았다 — [추정, 메인 세션 판단])
> **상태: 조사 완료 — 사용자 결정 대부분 기록됨, 남은 미결·Plan 승인 대기.** 질문 Q1~Q17과 결정 기록(Q12만 대기)·남은 미결 U-1~U-12·단계 계획은 같은 폴더 [Plan.md](Plan.md) 1-1·1-3절에 있다. 코드·에셋·규칙 문서는 이 작업으로 바뀌지 않았다(컨트롤러의 EagleArcher Attack2 상태 삭제는 사용자 커밋 `a11b5381` — 11절). *(2026-10-20 갱신 — 종전 「조사 완료 — 사용자 결정 대기」)* *(2026-10-20 5차: 앞 문장은 조사 회차 당시 사실이다. 그 뒤 규칙 문서는 `7e527c12`(승인 개정안 반영), 코드·에셋은 `d09cc4a9`(단계 A — 공통 기반 + StreamSpirit만 ON)에서 바뀌었다. 단계 A 실기 로그 분석은 [12절](#12-stage-a-field-log-analysis-20261020))* *(2026-10-21: 코드는 `639ac1aa`(단계 B — Tank·CannonCart ON, 의심 1 수정)·`ac05e189`(회귀 검증 수정)에서 다시 바뀌었다. 단계 B 실기 로그 분석 — 🔴 사망타 피해 숫자 누락(회귀), 수정 사용자 승인 대기 — 은 [13절](#13-stage-b-field-log-analysis-20261021))* *(2026-10-21 B-2: 그 수정은 13-7 A안으로 `64b743c8`에서 들어갔고(화면 표현·진단·회귀 검증만), 실기 로그상 사망타 숫자 누락 0 — [14절](#14-stage-b2-kill-shot-number-check-20261021). 회귀 스위트 결과는 미보고)* *(2026-10-21 단계 C: 코드는 `b819dc51`(인간 대포 타워 투사체 전환)에서 다시 바뀌었다. 실기 로그상 인간 타워 투사체 정상(scoped), 사용자 「테스트 완료 · 육안상 특별한 문제 없음」, **회귀 스위트 PASS(사용자 Console 확인 — RuntimeLog에는 남지 않음)** — [15절](#15-stage-c-human-cannon-tower-check-20261021). 앞 조각의 「회귀 스위트 결과는 미보고」는 B-2 회차 당시 상태다)* *(2026-10-26 단계 D: 코드는 `a0a23423`(차지 유닛 InfernoSpirit · FoxMagician ON, main 병합 `e340f783`)에서 다시 바뀌었다. 실기 로그상 서버 판정 · 차지 취소 · Fox 즉시 표시 0 · 숫자 hold 정상(scoped), 🔴 **Client 사망 표현 · 화상 VFX가 화면 착탄보다 0.04~0.17초 앞섬**(사용자 보고와 일치) — 수정 방식 (a)/(b)/(c) 사용자 결정 대기, 회귀 스위트 결과 미보고 — [16절](#16-stage-d-charge-unit-check-20261026))*
> 근거 표기: **[확인]** = document-manager가 이 checkout에서 파일을 다시 읽어 확인한 행 · **[전달]** = game-programmer 보고서 값을 옮긴 것(다시 읽지 않음) · **[추정]** = 코드로 확정하지 못한 해석 · **[미확인]** = 실행·실기 증거 없음. 행 번호는 바뀔 수 있으므로 메서드 이름을 함께 적는다.

## 0. 쉬운 설명

물폭탄·화염구·마법탄·포탄처럼 「날아가서 맞는」 공격은 지금 **공격 동작의 타격 순간에 서버가 바로 피해를 준다.** 화면의 투사체는 장식일 뿐이라 날아가는 시간이 게임에 아무 의미가 없다. 그래서 투사체가 거의 안 보이거나, 투사체가 닿기 전에 피해 숫자·사망이 먼저 나온다.

사용자는 이것을 **C 방식 — 서버 권위 투사체**로 고치기로 이미 정했다(2026-10-19, [옛 결정 기록](../../2026-10-13/13_53_vfx-code-followups/Research.md#14-projectile-visibility-decision)). 서버가 「언제 쏘는가(발사)」와 「언제 닿는가(착탄 = 발사 + 거리 ÷ 속도)」를 정하고, **닿는 순간에 피해를 확정한다.** 그러면 비행·피해·피해 숫자·사망·착탄 이펙트가 싱글·Host·Client 모든 화면에서 같은 순간에 맞는다.

이 문서는 그 작업을 시작하기 전의 조사다. 지금 피해가 어디서 생기는지, 이미 비슷하게 만들어 둔 것(버섯 폭격기·급류 정령의 파도)을 얼마나 재사용할 수 있는지, 바꾸면 함께 깨지는 것이 무엇인지를 정리했다. 「어떻게 할지」를 정하는 질문 17개는 Plan에 있으며 **사용자가 정하기 전에는 구현하지 않는다.**

대상 유닛: StreamSpirit(물의 정령) · InfernoSpirit(지옥불 정령) · FoxMagician(여우 마법사) · Tank · CannonCart · MushroomBomber(버섯 폭격기). EagleArcher와 방어 타워는 제외를 제안한다(Plan Q10). *(2026-10-20: 사용자 결정으로 **EagleArcher와 인간 방어타워(CannonTower)가 범위에 들어갔고**, Spirit RuneSpire는 즉시 피해를 유지한다 — Plan 1-1절 Q10. EagleArcher 사실은 11절.)*

---

## 1. 배경과 다른 기록과의 관계

| 기록 | 내용 | 이 작업과의 관계 |
|---|---|---|
| [vfx-code-followups Research 14절](../../2026-10-13/13_53_vfx-code-followups/Research.md#14-projectile-visibility-decision) / [Plan 17절](../../2026-10-13/13_53_vfx-code-followups/Plan.md#17-projectile-visibility-decision) | 옵션 C 채택(A·B·D 기각), 미결 기획 질문 4개(속도 · 비행 중 공격자 사망 · LockedPoint/Homing · 밸런스) | 이 작업의 출발점. 질문 4개는 Plan Q1~Q3·Q15로 이어진다 |
| [같은 Task Research 15절](../../2026-10-13/13_53_vfx-code-followups/Research.md#15-r5-interim-impact-hookup) / [Plan 18절](../../2026-10-13/13_53_vfx-code-followups/Plan.md#18-r5-interim-impact-hookup) | 재작업 R5 — VFX 확인용 임시 착탄 연결(「C 방식 구현 시 교체」) | C가 R5 경로를 교체한다(4-3) |
| [Research 15-5](../../2026-10-13/13_53_vfx-code-followups/Research.md#15-5-r5-field-bugs) / [Plan 18-5](../../2026-10-13/13_53_vfx-code-followups/Plan.md#18-5-r5-followup-fix-queue) | R5 실기 버그 B2(움직이는 대상) · B3(대상 사망 시 중간 이펙트) · B4(큰 대상에 가려짐) — 「C 방식과 함께 해결」 | 3-4·3-5·4-5에서 다룬다 |
| [차지 유닛 발사 시점 Task](../11_09_charge-unit-launch-timing/Research.md#9-implementation-result) | 화면 표현만 완료. 알려진 한계 **L-1** Fox 피해 숫자 선행 · **L-2** 차지 중 취소 후 숫자만 표시 → 「옵션 C에서 다룬다」 | 4-4에서 다룬다. 그 Task가 남긴 주석 처리 옛 코드 R-1~R-4 · V-1~V-3의 삭제 시점은 5-4 |

> 🔴 **번호 충돌 주의.** 「R-1」「R-3」「R-4」는 **두 Task에서 서로 다른 것**을 가리킨다. 차지 작업의 R-1(`UnitView.cs` 7340행 Fox 조기 종료)과 vfx-code-followups의 R-1(`VfxPoolItem.cs` 164행 [전달])은 다른 코드다. vfx-code-followups의 R-3은 검증 1389·1457행, R-4는 `VfxPoolItem.TimedDamage.cs` 42행이다 [전달]. 또 「R5」는 vfx-code-followups의 **재작업 회차 이름**이고 「R-5」 항목이 아니다. 이 문서와 Plan은 항상 **출처 Task 이름을 붙여** 부르며(「차지 R-1」「VFX R-1」), Plan의 새 제거 항목은 겹치지 않도록 **RM-** 접두사를 쓴다.

---

## 2. 현재 피해 적용 경로

### 2-1. 멀티플레이(서버)

1. **공격 커밋**: `NetworkCombatController.cs` `ExecuteAttack` 1306~1334행 — 쿨다운 리셋, 오버슈트 차감(규칙 18). TorrentSpirit만 1338행에서 분리된다 [전달].
2. **C2 토큰**: 1363~1366행, `CanRunSequencer`일 때만 발급. 투사체 7종은 `Unresolved`라 토큰이 없다 [전달]. Unresolved 7종은 Tank · CannonCart · InfernoSpirit · StreamSpirit · FoxMagician · EagleArcher · MushroomBomber다 — `UnitAttackShadowProfileResolver.cs` 62~68행 `UnresolvedProjectile(...)` [확인].
3. **피해 예약**: 1430~1518행, `HitFrameTimes` 원소마다 `DelayedAttackDamage` 코루틴, delay = hitTime − overshoot [확인: 1430행 루프와 오버슈트 주석].
4. **타격 순간**: `DelayedAttackDamage` 1100~1291행. 1116행 `WaitForSeconds`. 토큰이 없으면 1124~1145행 due 대기·취소 확인을 건너뛴다. 1195~1200행 writerAuthorization=Applied → 1213행 `ApplyAttackDamageObserved` [전달].
5. **판정**: `TryDispatchAttackShadow` 1714행은 `!token.IsValid`면 false → **투사체 유닛은 타격 순간 사거리·방향 검사가 없다** [전달].
6. **단일 HP writer**: `UnitCombatUseCase.cs` `ApplyAttackDamageCore` 979~1028행(987행 공격자 생존, 993행 타겟 생존 확인) → 1014행 `ExecuteAttack`(1723~1796행) → `ApplyDamageToVictim`(1897~1990행)이 `TakeDamage`·피해 이벤트·사망을 처리 [전달].

### 2-2. 싱글플레이

- `TryAttack` 636~756행: 709행 `Clock.TryCommit` → 731~748행 `PendingHit`(DueAt, CapturedTarget) 생성 [전달].
- `TickPendingHits` 1084~1127행: 1092~1100행 공격자 사망·공격자/타겟 객체 교체 시 취소 → 1103행 `ApplyAttackDamageCore` [전달].
- 실행 순서: `GameBootstrapper.cs` 621~628행 `TickCooldowns` → `TickTorrentLaunches` → `TickPendingHits` → `TickWaves` → `TickTimedEffects` [확인].

### 2-3. 피해 시각의 출처

`UnitFactory.cs` `GetHitFrameTimes` 577~592행 — 586행에서 `OnAttackHit` 이벤트만 수집한다 [확인]. 규칙 17과 일치한다.

### 2-4. 싱글과 멀티의 차이 [전달]

| 항목 | 싱글 | 멀티 |
|---|---|---|
| 시계 | `Time.timeAsDouble` 절대 due | 코루틴 상대 대기, 토큰 없음 |
| 취소 | 공격자 사망, 공격자/타겟 객체 교체 | 공격자 사망, 타겟 사망(core) |
| 사거리 재판정 | 없음 | 없음(Unresolved) |
| 피격 표시 | 공격자별 FIFO | 같은 FIFO, FoxMagician만 즉시 |

### 2-5. 지속 피해(DoT)와 범위 효과

화상·독 DoT, Blast(Mushroom), Inferno 처리는 모두 `ExecuteAttack` 1778~1789행 안에서 시작된다 [전달]. 그래서 **피해 시각을 착탄으로 옮기면 DoT 시작도 자동으로 착탄으로 옮겨진다.**

---

## 3. 이미 있는 서버 확정 경로 — 재사용 후보

### 3-1. MushroomBomber 스냅샷 경로 (현재)

1. `UnitCombatUseCase.cs` 1732~1742행 피해 시점에 `AttackEffectPresentation` 생성(Source/Impact = 피해 시점 위치, ImpactTime = 서버 시각) [전달].
2. 1790~1795행 `OnAttackEffectConfirmed` → `NetworkCombatController.cs` 60~86행 ClientRpc [전달].
3. `HitPresentationQueue.cs` 197~221행 수신 즉시 트레이서, 남은 시간 = ImpactTime + 0.10 − now ≈ 0.1초 [전달].
4. 222~256행 `EmitAttackEffects`가 ImpactTime + 0.10에 착탄 VFX(`EffectManager.cs` 502~512, 526~555행) [전달].
5. HP 숫자는 따로: `UnitView.cs` 7832~7840행 화면 타격 순간의 `OnLocalAttackHit` [전달].

**재사용 가능** [전달]: 불변 식별자(Match/Lifetime/Sequence)와 중복 거부(`HitPresentationQueue` 200~204행), RPC 틀, Client 반전 좌표 변환(208~209, 235~236행), `TracerProjectile`의 authoritativeDuration(116~129행), 착탄 시각 VFX 구조, 검증 fixture 뼈대(`RunUnitActionSelfValidation.VfxConnections.cs` 913행~).

**부족한 점** [전달]:
1. 착탄 **뒤** 1회 메시지뿐이다 — 「발사 기록」과 「결과 기록(명중 / 빗나감 / 대상 소실 / 취소)」을 나눠야 한다.
2. 표현 종류(`AttackEffectVisual`)가 MushroomImpact·Tower뿐이고 프리셋 조회가 MushroomBomber로 고정돼 있다(`EffectManager` 505, 533행).
3. 늦게 접속한 Client에게 비행 중 투사체를 복원하는 경로가 없다.
4. Impact 좌표가 유닛 루트 위치다 — 타워 번개 OPEN 결함의 가설 H1과 같은 출처다([vfx-code-followups Research 10-4](../../2026-10-13/13_53_vfx-code-followups/Research.md)).

### 3-2. TorrentSpirit 파도 경로 — 더 적합한 선례 [전달]

- 서버 예약: `UnitCombatUseCase.TorrentLaunch.cs` 29~68행(객체 참조 보관 → ID 재사용에 안전).
- 매 프레임 진행: 81~124행 `TickTorrentLaunches` (싱글 `GameBootstrapper` 622행 [확인], 멀티 `NetworkCombatController` 634행).
- 닫기: `CloseTorrentLaunches` — 게임 종료·맵 정리·디스폰·서비스 교체, 호출 5곳.
- 진행 + 접촉 확정 + 전송: `TickWaves`, `PublishWaveContact`(`UnitCombatUseCase.WavePresentation.cs` 117행~), `NetworkCombatController.Waves.cs`(epoch · baseline · 늦은 접속 스냅샷).
- 화면 단일 소비자: `WavePresentationPresenter`.

**조사 보고서의 제안** [전달]: Torrent의 예약·진행 구조 + Wave의 전송·화면 패턴으로 투사체 경로를 새로 만들고, Mushroom은 그 위로 옮긴 뒤 기존 스냅샷 경로를 교체한다.

---

## 4. 서버 투사체 모델 설계안 (제안 — 결정은 Plan Q1~Q6)

### 4-1. 시각

- **발사** = 지금의 피해 예약 시점(`OnAttackHit` offset). 규칙 17의 「`OnAttackHit` = 발사」와 맞는다.
- **착탄** = 발사 + 거리 ÷ 속도.
- 싱글은 `PendingHit` 만기, 멀티는 `DelayedAttackDamage`에서 `ApplyAttackDamage` 대신 **발사 기록**을 만든다 [전달].

### 4-2. 속도 데이터

`TracerProjectile._speed`(49행, 기본 14 [확인])를 서버 값으로 쓰는 것은 부적합하다 [전달]: ① Presentation 프리팹 값이라 레이어 위반 ② 피어마다 에셋이 다를 수 있음 ③ 컴포넌트가 없으면 기본 14(`EffectManager` 848~851행). 제안은 `UnitStatsConfig`(40~90행)에 서버 전용 필드를 두고 Client는 서버가 보낸 착탄 시각만 쓰는 것이다. Stats는 handshake 해시에 포함되지 않는다(검색 0건) [전달].

현재 프리팹 값과 최대 비행 시간 [추정 — TileHeight 0.866(`GameConfig.asset` 19행), 사거리 공식 `UnitCombatUseCase` 593~623행, 건물 대상 +0.2 기준 계산, 전달값]:

| 유닛 | 프리팹 속도 | 최대 비행 시간 |
|---|---|---|
| StreamSpirit | 4 | 약 0.66초 |
| Tank / CannonCart | 4 | 약 0.66초 |
| InfernoSpirit | 5 | 약 0.70초 |
| FoxMagician | 5 | 약 0.53초 |
| MushroomBomber | 7 | 약 0.25초 |

모두 공격 주기보다 짧다(예: Stream 0.5 + 0.66 < 1.5) [추정].

### 4-3. 착탄 처리

착탄 시각에 기존 `ApplyAttackDamageCore` → `ExecuteAttack`을 부르면 DoT·Blast·Inferno가 그대로 동작한다 [전달]. 서버 진행은 50ms 전투 틱이 아니라 **매 프레임**이어야 한다(`TickTorrentLaunches` 선례, `NetworkCombatController` 51행 `_attackInterval` 0.05). 그렇지 않으면 착탄이 50ms 격자만큼 늦는다 [전달].

### 4-4. 대상 처리 방식 (Plan Q1)

| 방식 | 내용 | 근거 규칙 | 영향 |
|---|---|---|---|
| LockedPoint | 발사 지점 고정. Single은 `ImpactHitRadius` 안일 때만 명중 | `U-IMPACT-LOCKEDPOINT` · `NET-FACING-002` | 회피가 생긴다(게임플레이 변화). B2는 규칙상 정상 동작이 된다 |
| Homing — 비행 시간 발사 때 고정 | 대상을 따라가고, 착탄 시각에 대상이 살아 있으면 명중 | `U-IMPACT-INDEPENDENT` · `NET-FACING-002` | 지금 게임플레이(빗나감 없음)와 가장 가깝다. B2 해소, 전송 단순 |
| Homing — 가변 착탄 | 매 틱 남은 거리로 다시 계산 | 같음 | 복잡 — 비권장 |

[전달] Mushroom은 resolver상 LockedPoint(167~169행)지만, 실제 범위 중심은 착탄 시점 대상 위치다(`BlastAttackBehavior`). 이것은 규칙 38 「판정 중심 = 주 타깃(착탄 지점) 월드 위치」, 규칙 40 「착탄 때 한 번 수집한 대상」과 맞물린다(Plan Q13).

### 4-5. 비행 중 대상 사망 — 버그 B3

- **원인** [추정, 강함 — 전달]: 지금은 피해가 발사 순간에 들어가므로, 마지막 일격이면 **발사 순간에 대상이 죽는다** → 사망 VFX(`PlayUnitDeath`) + 대상 사망 즉시 방출(`HitPresentationQueue` 584~589행 → `FlushTarget` 1010~1039행)로 숫자·피격 표현이 바로 나온다 → 투사체는 아직 비행 중이라 「중간 이펙트」처럼 보인다. C에서는 사망이 착탄 시각으로 옮겨지므로 **구조적으로 해소된다.** (Research 15-5는 이 원인을 「미확인」으로 남겼다. 이 판정도 실기로 확인된 것은 아니다.)
- **다른 공격자가 비행 중에 대상을 죽이는 경우**: 서버 피해 없음(TargetUnavailable). 화면은 「소멸」과 「마지막 위치까지 날아가 착탄 VFX」 중 결정이 필요하다(Plan Q4). 타워 번개의 「1회 해석 → 추적 → 사망 시 고정」(`VfxPoolItem.TowerBolt.cs`)을 재사용할 수 있다 [전달].

### 4-6. 비행 중 공격자 사망 (Plan Q3)

공격 프로필의 `PersistsAfterSourceDeath`가 정한다(`U-IMPACT-INDEPENDENT`, `NET-CANCEL-003` [확인]). 발사 **전** 사망은 취소다. Persist=true를 고르면 `ApplyAttackDamageCore` 987행, `BlastAttackBehavior.Apply`, `InfernoAttackBehavior.Apply`가 공격자 사망 시 return하므로 **착탄용 진입점이 따로 필요하고, 공격력은 발사 때 값을 저장해야 한다** [전달]. 그대로 두면 피해가 조용히 사라진다(위험 3).

### 4-7. 사거리 이탈 (Plan Q5)

지금 Unresolved 유닛은 타격 순간 사거리 검사가 없다 [전달]. 발사·착탄 때 재판정하지 않는 것이 「동작 무변경」이다.

---

## 5. 클라이언트 표현

### 5-1. 🔴 빈 큐 신호가 버려진다 — 함께 바꾸지 않으면 깨진다

원거리 유닛의 HP 숫자는 공격자별 FIFO에 보류됐다가 로컬 트레이서 도착 신호(`UnitView` 7843~7858행 [전달])로 나온다. C에서는 피해 이벤트가 화면 타격 신호보다 **늦게** 오므로, 먼저 온 신호는 빈 큐에서 버려진다 — `HitPresentationQueue.cs` 565~570행 주석이 「빈 신호는 그냥 버린다 … 다음 타격 신호 또는 타임아웃 안전망(ⓐ)이 방출을 보장」이라고 적고 있다 [확인]. 결과적으로 숫자가 다음 공격 주기나 시간 초과(쿨다운 × 1.5 — 1223행, Tank 6초 [전달])까지 밀린다. Mushroom의 화면 타격 신호(7838행)도 같다 [전달].

선택지 [전달]: (a) 기존 ImmediatePresentation 경로(Client 쪽 `NetworkHealthSync.cs` 159~161행) / (b) 파도 선례처럼 투사체 키 + 단일 소비자. 보고서는 (b)를 제안한다(Plan Q7).

### 5-2. 트레이서

Supported 분기에는 이미 「서버 착탄 + 0.10 − now」 방식이 있다(`UnitView` 7812~7829행) [전달]. 투사체는 **서버 발사 기록을 받았을 때** 발사하고, 로컬 marker는 총구 VFX·SFX만 내는 것이 제안이다(marker와 기록의 경쟁을 피함, Plan Q9). Homing의 화면 추적은 `TracerProjectile`(고정 지점 직선 보간, 154~165행)을 확장해야 한다 [전달].

<a id="5-3-r5-and-charge-leftovers"></a>

### 5-3. R5 임시 표시 경로 — 교체 대상

- 운영 코드: `EffectManager.cs` `PlayTracer`의 `playImpactOnArrive` 인자(600행)와 분기(621~632행), `ResolveLegacyArrivalImpact`(653행~), `PlayLegacyArrivalImpact`(669행~) [확인 — 행 번호는 보고서의 596·607·617·643·661과 조금 다르다]. 호출 쪽 `UnitView.cs` 7859~7862행 [전달].
- 검증: `RunUnitActionSelfValidation.VfxConnections.cs` 76행, 372~480행 `ValidateLegacyTracerArrivalImpact` · `RunUnitActionSelfValidation.cs` 7508~7524행(Stream), 7973행(Fox 설명), 8276행, 8391~8451행(Inferno) [전달].

### 5-4. 차지 작업의 주석 처리 옛 코드 — C 뒤 정리

차지 작업이 주석으로 남긴 블록의 현재 위치 [확인 — `[R-n]`/`[V-n]` 표식 grep]:

| 표식 (차지 작업) | 위치 |
|---|---|
| R-1 | `UnitView.cs` 7340행 |
| R-2 | `UnitView.cs` 1157행 |
| R-3 | `UnitView.cs` 7460행 |
| R-4 | `UnitView.cs` 7766 · 7825 · 7863 · 7870행 |
| V-1 | `RunUnitActionSelfValidation.cs` 8155행 |
| V-2 | 같은 파일 8200 · 8254행 |
| V-3 | 같은 파일 7972행 |

- 차지 작업 [Plan 9-1](../11_09_charge-unit-launch-timing/Plan.md#9-구현-결과-2026-10-19-추가)은 「주석 처리분 최종 삭제 — 미실행, 사용자 지시가 없었다」로 남아 있다 [확인].
- 이 블록과 R5 경로를 **C 사용자 테스트 PASS 뒤에 삭제**하는 것은 사용자 결정이다 [전달 — 메인 세션]. Plan Q16에서 재확인한다.

### 5-5. FoxMagician 규칙 19 예외와 차지 작업 한계 L-1·L-2

- Fox 즉시 표시: `UnitCombatUseCase.cs` 1768~1769행 `immediatePrimaryPresentation = attacker.Type == UnitType.FoxMagician && !presentationResultKey.IsValid` [확인]. 검증 `ValidateFoxMagicianPrimaryPresentation`(7641행), `ValidateFoxMagicianTimingAuthorityCore`(8074행)가 즉시 표시를 요구한다 [전달].
- 피해가 착탄으로 옮겨지면 「즉시 표시」≈ 착탄 시각이 된다(Host 약 0.10초, Client는 전송 지연만큼 [추정]). Plan Q7에서 (b)를 고르면 예외를 없애고 통합할 수 있으며, 그때는 규칙 19 본문 개정이 필요하다(Plan Q8). → **L-1 해소.**
- **L-2**(차지 중 취소 후 숫자만): 서버 발사 기록을 기준으로 트레이서를 쏘면 해소된다 [추정](Plan Q6).

### 5-6. 착탄 이펙트가 큰 대상에 가려진다 — 버그 B4

유닛은 `UnitEffectConfig._recipientFits`의 `radius`가 있지만 툴팁 75행이 「참고값」이라고 적는다 [전달]. 건물은 `BuildingData`(23행~)에 hex 위치만 있고, 크기 관련 값은 `BuildingDetectionRadius` 0.2(`UnitCombatUseCase` 37행)뿐이며 `BuildingEffectConfig`에 크기 표가 없다 [전달]. 보고서 제안: 서버 착탄점은 그대로 두고 **화면에서만** 대상 중심에서 공격자 쪽으로 반경만큼 당겨 재생, 건물 반경 데이터 신설(Plan Q12).

---

## 6. 네트워크 규칙 · C2/C3 분류

- **규칙 변경이 필요 없는 근거** [확인 — 규칙 원문]: `NET-DELIVERY-PROJECTILE`(「서버가 발사 시각, 착탄 시각, 목표 처리 방식과 착탄 위치를 관리한다. 피해와 범위 효과는 권위 착탄 레코드에서만 확정된다」)이 이미 이 목표 설계다. `NET-CANCEL-003`(이미 발사한 독립 ProjectileImpact만 `PersistsAfterSourceDeath`에 따라 진행) · `NET-CANCEL-005` · `NET-TIME-002`·`NET-TIME-005` · `NET-AUTH-001`·`002`도 그대로 맞다.
- **C3 승격 불필요** [전달]: `U-ATK-TIMELINE` 마지막 문단(Unresolved는 Legacy 표현 보존), `NET-PRESENT-002`, `NET-DELIVERY-TRAVELING`의 Torrent 선례(「Unresolved/LegacyFallback의 로컬 단조 예약 identity는 Supported 승격을 뜻하지 않는다」 [확인])를 따라 **Unresolved를 유지하고 서버 투사체를 별도 경로로** 두는 것이 제안이다. Supported 승격은 `CanRunSequencer`(TargetLocked + MeleeContact/Hitscan) 등을 넓혀야 하므로 별건이다.
- **Manifest 해시**: `TryComputePresentationProfileHash`(264~302행)가 25종 행을 해시하고, 불일치면 경기 시작을 거부한다(`NetworkGameFlow.cs` 149, 351~364행) [전달]. 행이 안 바뀌면 해시도 안 바뀐다. 새 전송 계약이므로 `PresentationContractSchemaRevision`을 3 → 4로 올리는 것이 제안이다. 상수는 `UnitAttackShadowProfileResolver.cs` 19행 `= 3`, 읽는 곳은 `NetworkGameFlow.cs` 83행이다 [확인 — 보고서는 82행으로 적었다].
- Game 씬 `_serverCombatPipelineMode`: 2(ResultPresentation, `Game.unity` 2891행). Unresolved는 `OwnsAttacker` false(`UnitAttackResultPresentationShadow.cs` 825~828행) → Legacy [전달].

---

## 7. DoT · 밸런스 · AI

- DoT 시작은 착탄 기준으로 자동 이동한다(`InfernoAttackBehavior`, `BlastAttackBehavior` + `ApplyDamageOverTime` 2165행~) [전달].
- DPS: 정상 상태(연속 공격)에서는 같다. [추정] 첫 피해·처치 시간이 비행 시간만큼(0.25~0.7초) 늦어지고, 이미 죽을 대상에게 비행 중 중복 사격하는 낭비가 생기며, Persist=false면 공격자 사망 시 피해가 사라지고, LockedPoint면 회피가 생긴다.
- `StatsReference.md` 49행 「일반 공격은 권위 피해 예약 시점」 [확인]과 표의 「타격·발동 시점」 열이 「발사 시점」 의미로 바뀐다 → 수정 대상.
- AI: `AIOpponentController`는 HP·피해·쿨다운을 보지 않는다(생산 위주, 검색 0건). 타겟 선택 `FindFirstEnemyTarget`은 생존만 본다 [전달] → 확정 처치 비행 중 중복 사격 판단은 Plan Q15.

---

## 8. 검증

### 8-1. 바뀌는 기존 검증 [전달]

| 검증 | 위치 | 이유 |
|---|---|---|
| `ValidateFoxMagicianTimingAuthorityCore` | 8030~8086행 | 85/30초 피해 1회 + 즉시 → 발사/착탄 분리 |
| `ValidateFoxMagicianPrimaryPresentation` | 7607~7655행 | 규칙 19 결정(Q8) |
| Fox marker fixture | 8087행~, 차지 V-1/V-2 대체 | 발사 위치 변경 |
| Stream/Fox/Inferno resolver 단언 | 7535~7545, 8015~8025, 8463~8469행 | 행이 바뀔 때만 |
| Unresolved 수 고정(16/8/1) | 9037행 | 승격할 때만 |
| `ValidateLegacyTracerArrivalImpact` | VfxConnections 382~480행 | 교체 |
| `ValidateWriterSnapshotVfx` | 913행~ | Mushroom 의미 변경 |
| `ValidateChargeUnitLaunchTiming` | 91행~ | marker 발사 → 서버 기록 발사 |
| Stream/Inferno 타임라인 R5 블록 | 7404행~, 8280행~ | 교체 |

### 8-2. 새 fixture 후보 [전달]

싱글: 발사 시 피해 0 / 착탄 시 1회. 멀티: 서버 발사 → 착탄 1회(프레임 기준). 비행 중 대상 사망 · 공격자 사망(Persist 양쪽) · 게임 종료·맵 정리 닫기 · ID 재사용 다른 객체 미적중. Homing 착탄 또는 LockedPoint 반경. DoT 시작 = 착탄. 발사·결과 중복 거부 · 늦은 접속 · 반전 좌표. 화면: 비행 중 숫자·VFX 없음 → 착탄 1회, 빈 큐 회귀 방지, 착탄 VFX 이중 재생 금지. B4 표면점 · 건물 반경 누락 대체. 해시/스키마 불일치 시 시작 거부.

---

## 9. 규칙 문서 영향 — 🔴 변경 전 사용자 보고 대상

결정 내용에 따라 아래 문장이 현재 사실과 어긋나게 된다. **규칙 문서는 이번에 고치지 않았다.** 개정 여부와 문구는 Plan 4절에서 사용자 승인을 받는다.

| 문서 · 위치 | 현재 문장 [확인] | 어긋나는 이유 |
|---|---|---|
| `GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 규칙 17, 324행 | 「서버 피해 시점·피해량은 이 계약으로 바뀌지 않는다」, 「실제 `OnAttackHit`만 권위 피해 offset으로 추출」 | 투사체 유닛은 `OnAttackHit` = 발사, 피해 = 착탄이 된다 |
| 같은 규칙 17 차지 계약 셋째 항목 | 「발사 전에 공격이 끊기면 … 투사체·공격 SFX는 내지 않는다」 | Q6을 추천대로 정하면 서버가 발사하고 화면도 투사체를 보여 준다 |
| 같은 절 규칙 19, 335행~ | FoxMagician 즉시 표시 예외 | Q7·Q8 |
| 같은 절 규칙 20, 347행 | 「원거리 유닛은 `OnAttackHit` 시점에 연출 전용 발사체를 재생한다」 | Q9 — 서버 발사 기록 기준 |
| 같은 문서 「특수 공격 시스템 규칙」, 419행 인용 블록 | MushroomBomber는 규칙 20을 「스냅샷 소유 방식으로 대체」 | Mushroom을 새 경로로 옮기면 바뀐다 *(document-manager가 grep으로 추가 확인 — 보고서 목록에 없음)* |
| 「착탄형 DoT 확장」 규칙 38 | 「판정 중심 = 주 타깃(착탄 지점) 월드 위치」 | Q13 — 고정 지점 중심 |
| 같은 확장 규칙 40 | 「착탄 때 한 번 수집한 대상」 | Q13 |
| `StatsReference.md` 49행 + 표 「타격·발동 시점」 열 | 「일반 공격은 권위 피해 예약 시점」 | 투사체 유닛은 발사 시점 |
| `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12, 197행 | 「방어 타워는 즉발 데미지를 유지한다」 | 타워를 범위에 넣을 때만(Q10 — 제외 제안) |

---

## 10. 위험 [전달 — Plan 5절에서 대응과 함께 다룬다]

1. 빈 큐 신호 버림 — 화면 경로를 함께 안 바꾸면 숫자가 최대 6초 밀림
2. 착탄 VFX 이중 재생(R5 · Mushroom 스냅샷 · 새 경로)
3. Persist=true인데 4-6의 세 곳을 그대로 두면 피해가 조용히 사라짐
4. 50ms 격자 지연
5. 스키마 미상향 시 구·신버전 혼합 경기
6. ID만 보관하면 ID 재사용으로 다른 대상 적중(객체 동일성 확인 필요)
7. 게임 종료 후 착탄 피해 → 닫기 필수
8. 기존 검증 다수 깨짐(8-1)
9. 밸런스 체감 변화
10. 서버 착탄 좌표(유닛 루트)와 화면 위치 차이 — 타워 번개 H1과 같은 종류 [미확인]

---

<a id="11-eaglearcher-facts-20261020"></a>

## 11. EagleArcher 사실 (2026-10-20 추가 — Q10 결정으로 범위에 들어옴)

> **쉬운 설명.** 독수리 궁수는 지금까지 「공격 애니메이션이 둘(Attack · Attack2)이라 어느 쪽이 쓰이는지 확정하지 못한다」는 이유로 투사체 작업에서 빠져 있었다. 사용자가 애니메이션 컨트롤러에서 Attack2를 지워 공격 동작이 하나만 남았다. 그래서 이번 작업에 넣을 수 있게 됐다. 남은 확인은 「0.1초의 타격 표식이 정말 화살을 놓는 순간인가」와 「날아갈 화살을 무엇으로 만들 것인가」다. *(2026-10-20 2차: 둘 다 정해졌다 — 사용자가 표식을 0.2초로 옮겼고, 화살은 유닛 프리팹의 화살 메시를 복사한 투사체로 만들며 착탄 이펙트는 없다. Plan 1-1-2)*
> 기준 HEAD `a11b5381` → 2차 갱신 `968e56bc`. [확인] = document-manager가 이 회차에 파일을 읽거나 `git show`로 본 것.

### 11-1. 컨트롤러와 공격 클립 [확인]

| 항목 | 값 | 근거 |
|---|---|---|
| 커밋 `a11b5381` (사용자, 2026-10-20) | `EagleArcher.controller`에서 상태 `EagleArcher_Attack2`(motion GUID `689bae402518d2c4c885a5b20038740d`)와 그 상태 목록 항목을 삭제(29행 감소). 같은 커밋의 나머지는 `.meta` 파일뿐 | `git show --stat a11b5381`, 컨트롤러 diff |
| 현재 컨트롤러 상태 | **Attack · Walk 두 개뿐**. Attack → GUID `573c970a…`(= `EagleArcher_Attack.anim`), Walk → GUID `e3c040dc…` | `Assets/_Project/Animations/Units/EagleArcher/EagleArcher.controller` 35·48·83·96행 |
| `EagleArcher_Attack.anim` | 샘플레이트 30, 길이(`m_StopTime`) **1초**, Animation Event는 **`OnAttackHit` 하나, 0.2초**(30fps `0:06`). `OnAttackCharge` 없음(차지 유닛 아님). *(2026-10-20 2차 갱신 — 1차 때 0.1초, 사용자 커밋 `968e56bc`가 0.1 → 0.2로 옮김. `git show --stat 968e56bc` = 이 `.anim` 한 파일 1행)* | 같은 폴더 `.anim` 9631·10859·20749~20750행 |
| `EagleArcher_Attack2.anim` | 파일은 **남아 있다**. 길이 0.70000005초, Animation Event 없음 | 같은 폴더 `.anim` 13092·14275행, `functionName` 0건 |
| Attack2를 참조하는 곳 | **찾지 못했다** — `Assets/` 아래 `.controller` · `.overrideController` · `.prefab` · `.asset` · `.unity` · `.cs`에서 GUID `689bae40…`와 이름 `EagleArcher_Attack2` 모두 0건 | `grep -rl` (탐색 범위는 이 6개 확장자) |

- 정리 후보: Plan RM-11(사용자 확인 필요, U-11).
- 사용자 개별 테스트·수용(2026-10-11, 공용 메모리 기록)은 이 삭제로 철회되지 않는다. 이번 삭제 뒤의 실기 확인은 없다 [미확인].

### 11-2. resolver 사유와 Factory 클립 선택

- `UnitAttackShadowProfileResolver.cs` **67행**: `case UnitType.EagleArcher: return UnresolvedProjectile(type, false, "projectile-animation-selection-unresolved", out profile);` [확인]. 코드는 바뀌지 않았다.
- `UnitFactory.cs` 494~497행은 명시 클립(`attackTimelineClip`)이 없으면 컨트롤러의 `animationClips`를 넘기고, 510행은 **이름에 `Attack`이 들어간 첫 클립**을 고른다 [확인]. 지금 컨트롤러가 참조하는 클립은 Attack·Walk 둘이므로 고를 수 있는 Attack 클립은 `EagleArcher_Attack` 하나다 → 사유 「animation-selection-unresolved」의 근거(두 클립 중 어느 쪽인지 모름)는 **해소된 것으로 본다** [추정, 강함 — Unity `animationClips`가 컨트롤러 참조 클립만 돌려준다는 일반 동작에 기댄 판단이며 실행 관측은 없다]. EagleArcher 항목에 명시 클립이 연결돼 있는지는 이번에 확인하지 않았다 [미확인].
- 이 사실로 낡는 문서 서술(1차: 수정하지 않음, Plan 4-2 A-12 → **2026-10-20 2차: 사용자 승인으로 0.2초와 함께 반영 완료**): `StatsReference.md` 87행 「Attack 1 / Attack2 0.70000005 (선택 순서 미확인)」, 118행 「남은 구현 선택 경계」, `Assets/UnitCombatAssetMatrix.md` 133행 「Attack2: 이벤트 0 · 컨트롤러 클립 순서 변경 시 타격점 손실 가능」 [확인].

### 11-3. 화살 오브젝트와 이펙트 프리셋 [확인]

- 화살 모델: `Assets/_Project/Models/Units/Transcendence/EagleArcher/EagleArrow/EagleArrow.fbx`, 머티리얼 `mat_eaglearrow_blue.mat` · `mat_eaglearrow_red.mat`, 텍스처 4장.
- 이 FBX를 쓰는 곳은 유닛 프리팹 `Unit_EagleArcher_Blue.prefab` · `Unit_EagleArcher_Red.prefab` 두 개뿐이며, 그 안의 **자식 오브젝트 `EagleArrow`**(같은 프리팹에 `EagleBow`도 있다)의 메쉬다. **따로 떨어진 화살 투사체 프리팹은 찾지 못했다**(FBX GUID `581e593c…` 검색, `.prefab`·`.asset`·`.unity`).
- `Assets/_Project/Resources/Config/EffectPresets/`에 이름에 `Eagle`이 들어간 프리셋이 없다. `Prefabs/VFX/Units/`의 트레이서 프리팹은 cannon · foxmagician · infernospirit · mushroombomber · streamspirit 다섯 개다.
- 결론: 사용자 결정 「화살 오브젝트를 투사체로 사용」을 구현하려면 **날아갈 화살을 무엇으로 만들지**(자식 메쉬를 복제한 투사체 프리팹 · 새 에셋)와 **발사 때 손의 화살을 어떻게 할지**를 정해야 한다(Plan U-10). 발사 시점 0.1초가 화살을 놓는 순간인지는 사용자 확인 대기(Plan U-9). *(2026-10-20 2차: U-9·U-10 닫힘 — `EagleArrow` 메시 복사 투사체 프리팹, impactPreset 비움, marker 0.2초. 손의 화살 처리는 Plan U-13으로 열려 있다)*

### 11-4. 다른 절과의 관계

| 절 | 상태 |
|---|---|
| 0절 「EagleArcher와 방어 타워는 제외를 제안」 | **낡음** — 결정으로 포함(같은 줄에 2026-10-20 조각을 달았다) |
| 2-1 ② Unresolved 7종 목록(EagleArcher 포함) | **그대로 유효** — 분류 행은 바뀌지 않았다 |
| 4-2 속도 표 | **그대로 유효**(현재 프리팹 값). EagleArcher 행이 없는 것은 화살 트레이서 프리팹이 없기 때문이다(11-3). MushroomBomber 7은 사용자 결정으로 낮춰 시작한다(Plan 1-1 Q2). *(2차: 서버 속도 초기값 확정 — Stream 4 · Tank 4 · CannonCart 4 · 인간 타워 4 · Inferno 5 · Fox 5 · EagleArcher 6 · Mushroom 5, Plan 1-1-2)* |
| 9절 규칙 문서 영향 | **보완됨** — 결정 뒤 개정안은 Plan 4-2 A-1~A-12(규칙 39 · `StatsReference.md` 87·118행 · `NET-PRESENT-001` · Buildings 규칙 12 Human 한정 추가) |

### 11-5. 인간 방어타워 피해 경로 (Q10 결정 관련 사실) [확인]

- 타워 피해는 유닛 전투 경로가 아니라 `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` `ExecuteTowerAttack`(216행~)에서 난다. 241행 `target.TakeDamage(damage)`가 **즉시** 피해를 주고, 245~248행이 `AttackEffectVisual.Tower` 표현 스냅샷을 발행한다. 종족 구분은 230행 `ResolveRace(tower.Team)`.
- 발사 이펙트 프리셋: `EffectPreset_HumanAutoTower_Attack.asset` 15행 `_vfxPrefab` → `vfx_pistoleer_attack.prefab`, 16행 `_localPositionOffset` (0,0,0). 사용자가 쓰라고 한 CannonCart 투사체·착탄: `EffectPreset_Cannon_Tracer` → `vfx_cannon_tracer.prefab`(`_speed` 4), `EffectPreset_Cannon_Impact` → `vfx_cannon_impact.prefab`.
- 건물 스탯은 `BuildingStatsConfig`(ScriptableObject)에 종족별 칸이 있다(`humanAttackRange` 94행 등) — 인간 타워 속도를 둘 후보(Plan U-4, 제안).

<a id="11-6-target-change-facts-20261020"></a>

### 11-6. 공격 중 타겟이 바뀌는 경로 (2026-10-20 2차 — Plan 1-2 근거) [확인, HEAD `968e56bc`]

- **싱글** `UnitCombatUseCase.cs` `TryAttack`: 657행 `if (attacker.AttackCooldownRemaining > 0f) return null;` 뒤 660행 `FindFirstEnemyTarget(attacker)`로 타겟을 고르고, 680~684행에서 이전 타겟과 다르면 `OnCombatTargetChanged`를 발행한다. 즉 타겟은 **쿨다운이 끝난 새 공격 시작 때만** 바뀐다.
- **멀티 서버** `NetworkCombatController.cs` 819~826행: 이전 타겟과 새 후보가 다르면 826행 `combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit)`가 false일 때만 교체한다. 825행 주석 「현재 타겟이 사망했거나 사거리를 벗어난 경우에만 교체를 허용한다」.
- 따라서 차지(발사 전) 중의 「타겟 교체」는 독립 사건이 아니라 **대상 사망** 또는 **사거리 이탈**의 결과다(메인 확인과 같음). 사거리 이탈 쪽은 사용자 결정 「원래 대상에게 발사」와 맞추려면 커밋된 투사체 회차 동안 교체·표현 닫기를 막아야 한다(Plan 위험 16).
- `GameSystemRules_Units.md` 187행 「커밋된 회차의 타겟은 … 다른 대상에 이전하지 않는다 … 새 타겟은 반드시 새 `AttackSequenceId`를 가진 새 회차에서만 사용한다」 — 이 방향과 같다.

### 11-7. EagleArcher 타격 시각의 두 출처 (2026-10-20 2차) [확인]

- 클립 marker: `EagleArcher_Attack.anim` `OnAttackHit` **0.2초**.
- 설정 fallback: `Resources/Config/UnitStatsConfig.asset` type 24(EagleArcher = 24, `UnitType.cs` 46행) `hitFrameTimes` **0.1**.
- 선택 규칙: `UnitFactory.cs` 529~530행 `ShouldUseAttackClipHitFrameTimes` = `markerCount > 0` → marker가 있으면 clip 값(0.2)을 쓴다. 지금 동작은 0.2초이고 fallback 0.1은 쓰이지 않지만 **두 값이 다르다**(Plan U-15).

### 11-8. Q12(B4) 결정과 관련한 데이터 위치 (2026-10-20 2차) [확인]

- 사용자 결정: 서버가 비행 거리를 「중심 거리 − 대상 반경」으로 계산하고, 유닛 반경은 `UnitEffectConfig._recipientFits` `radius`를 서버 권위 데이터로 옮겨 쓰며, 건물은 종류별 반경을 새로 만든다 [전달 — 메인 세션].
- 지금 그 radius가 있는 `UnitEffectConfig.cs`와 `BuildingEffectConfig.cs`는 `Assets/_Project/Scripts/Presentation/Effects/`에 있다. 서버 판정은 Presentation 값을 읽지 않으므로(아키텍처 방향 Domain → … → Presentation) 값을 Application/Infrastructure 쪽 데이터로 옮겨야 한다 — 그래서 「옮겨 사용」이 필요하다. 건물 쪽은 5-6절대로 크기 값이 `BuildingDetectionRadius` 0.2뿐이다 [전달].

### 11-9. MushroomBomber 대상 처리 — 예외 철회 관련 사실 (2026-10-20 3차) [확인]

- 사용자 결정(3차): MushroomBomber도 다른 투사체처럼 Homing으로 대상에게 날아간다. 지점 공격 없음 [전달 — 메인 세션, Plan 1-1-3].
- resolver: `UnitAttackShadowProfileResolver.cs` `UnresolvedProjectile` 167~169행이 `type == UnitType.MushroomBomber ? AttackTargetMode.LockedPoint : AttackTargetMode.TargetLocked`로 Mushroom만 LockedPoint로 둔다. 4-4절의 「Mushroom은 resolver상 LockedPoint(167~169행)지만 실제 범위 중심은 착탄 시점 대상 위치」 [전달]와 같은 행이며, 결정 뒤에는 이 값이 실제 게임플레이와 다르다 → Plan 1-1 Q11 설계 주의 · U-16.
- 규칙 원문: 규칙 38 535행 「판정 중심 = 주 타깃(착탄 지점) 월드 위치」, 규칙 39 544행 「주 타깃이 적 유닛이면 직접 피해+DoT」, 규칙 40 551행 「착탄 때 한 번 수집한 대상」 — Homing 착탄이면 그대로 참이라 개정이 필요 없다(Plan 4-2-1 F-6·F-7).
- 이 절로 **낡은 것:** 4-4절 표의 LockedPoint 행을 Mushroom에 적용하는 해석, Plan 2차의 Mushroom 지점 고정·기본안. **그대로 유효한 것:** 3-1절 스냅샷 경로 교체 필요성(RM-8), 2-5절 DoT 시작 = 착탄.

---

<a id="12-stage-a-field-log-analysis-20261020"></a>

## 12. 단계 A 실기 로그 분석 (2026-10-20)

> 기준 checkout HEAD `51271083` · 대상 구현 커밋 `d09cc4a9`(단계 A — P1~P3 공통 기반 + 유닛별 스위치 **StreamSpirit만 ON**, `ProjectileDeliveryContracts.cs` 229~232행 `EnabledUnitTypes = { StreamSpirit }` [확인]).
> 출처: game-programmer **읽기 전용** 로그 분석을 메인 세션이 전달 [전달]. document-manager는 로그 본문을 다시 읽지 않았다(로컬에 없음 — 12-2). 이 절의 수치는 모두 [전달]이며, 코드 행은 이번 회차에 다시 읽은 것만 [확인]으로 적었다.
> 사용자 상시 승인(로그 분석 결과는 확인 없이 해당 Task·관련 상태 문서에 기록)에 따라 작성했다. 이 기록은 코드·에셋·빌드 변경을 승인하지 않는다.

### 12-1. 쉬운 설명

단계 A는 물의 정령(StreamSpirit) 하나만 새 방식으로 바꾼 것이다. 이제 물폭탄은 서버가 「쐈다」를 기록하고, 날아가 닿는 순간에 피해가 들어간다. 사용자는 멀티플레이 한 경기와 싱글플레이 두 경기를 했고, 멀티 화면에서 **「유닛을 잘 따라감」**을 눈으로 확인했다.

로그를 보면 **서버 쪽 판정은 멀티·싱글 모두 정상**이었다 — 쏠 때는 HP가 그대로이고, 닿는 순간에 60씩 줄었으며, 날아가는 중에 대상이 죽으면 피해가 없었고, 쏘기 전에 대상이 죽으면 쏘지 않았다. 다만 이 확인은 **로그가 남은 앞부분(약 30발 안팎)** 에 한정되고, **화면에 투사체·착탄 이펙트·피해 숫자가 정확히 한 번 나왔는지**는 그것을 적는 로그가 없어 확인하지 못했다. 그래서 이 결과는 「단계 A 전체 PASS」가 아니다.

한 가지 의심도 나왔다. 멀티의 Client 화면에서, 서버가 **쏘기 전에 취소한 공격**에도 총구 이펙트(어쩌면 발사음도)가 나왔을 가능성이 있다. 사용자 결정 「쏘기 전에 끊기면 투사체도 소리도 없다」와 어긋날 수 있어, 고칠지 여부는 사용자 판단을 기다린다(12-7 의심 1).

### 12-2. 로그 보관 영수증 (LogRules §1.10)

| 항목 | 값 | 근거 |
|---|---|---|
| 보관 위치 | private 저장소 `rmsepskek02/Hexiege-Logs`, Release `logs-2026-10`(분석 원문 표기) | [전달 — 메인 세션] |
| 업로드 주체 | 사용자 | [전달] |
| 다운로드 검증 | 메인 세션이 API로 내려받아 **Release digest와 SHA256 일치** 확인 | [전달 — 메인 세션] |
| manifest | **사용자 업로드에 별도 manifest 파일 없음** — 메인이 아래 값으로 대조 | [전달] |

| ZIP (remote 이름 그대로) | asset id | ZIP 크기(B) | ZIP SHA256 | 안의 로그 |
|---|---|---|---|---|
| `2026-10-20_17_11_streamspritdevice.zip` | 658693264 | 519410 | `6f7a721f6913e29e705998b5512b66cb4d4b5bd1da714221b22b0009807ddbbd` | 기기 `RuntimeLog_device.txt` 10049066 B (SM_S901N, 저장 16:59:15) |
| `2026-10-20_17_11_streamspriteditor.zip` | 658693322 | 1009451 | `79ff89034b9e9b4a435e9c07fc4876d2c9ec2257779fdffe4007608d3d5d3e46` | 에디터 `RuntimeLog.txt` 12549999 B |
| `2026-10-20_17_21_streamspriteditorsingle.zip` | 658713901 | 1069118 | `7853589252681834ff614e47cf5cef6d186b3e23b9e3264c53800e45caca9dbc` | 에디터 `RuntimeLog.txt` 13271698 B (싱글 포함) |

- **§1.10과의 차이 (기록만, 보정 요청 아님):** ① manifest가 없어 §1.10의 「ZIP 전체 entry의 상대경로·개수·크기·개별 SHA256을 manifest와 대조」는 수행되지 않았다 — 안의 로그는 **크기만** 전달됐고 개별 SHA256은 없다 [미확인]. ② 파일명은 `작업명_회차` 형식이 아니고 `streamsprit`(철자 그대로)·회차 없음이다. 이름은 remote 원문 그대로 적었다.
- **소스 저장소 쪽 [확인]:** 사용자 커밋 `51271083`이 `Assets/_Project/Docs/_Logs/2026-10-20/16_59_logcat/RuntimeLog_device.txt.meta`(+ 폴더 meta 2개)만 추가했다. 본문 `RuntimeLog_device.txt`는 `.gitignore` 814행 패턴으로 제외되며(`git check-ignore -v`), 이 checkout에는 없다. 이 폴더(16_59)와 기기 ZIP 안의 로그(저장 16:59:15)가 같은 파일인지는 시각이 맞을 뿐 확인하지 않았다 [추정].

### 12-3. 결론

| 축 | 판정 | 범위 |
|---|---|---|
| 서버 판정(발사·착탄·피해·취소·닫기) | **정상 — scoped** | 멀티 1경기 · 싱글 2경기, 각 경기 `[PROJECTILE]` 로그가 남은 구간만 |
| 화면 표현(트레이서·착탄 VFX·피해 숫자 1회·Client 0.10초 지연) | **미확인** | 표현 쪽 로그 없음. 근거는 사용자 육안 「유닛을 잘 따라감」(멀티)뿐 |
| 사용자 육안 | 멀티 「유닛을 잘 따라감」 · 싱글 테스트함 | 싱글의 육안 판정 문구는 전달받지 않았다 [미확인]. 어느 화면(Host/Client)인지도 전달 없음 |
| 회귀 스위트(`Run Full Regression Suite`) | **미보고** | 사용자 결과 보고 없음 — PASS로 적지 않는다 |
| 의심 | 1건(의심 1) + 진단 결함 2건 + 관찰 2건 | 12-7 |

- **로그 상한 [확인]:** `UnitCombatUseCase.Projectiles.cs` 47행 `ProjectileDiagnosticLimit = 96`, 582행에서 넘으면 기록하지 않는다. 46행 주석은 상한을 「한 UseCase 수명당」이라고 적는다(분석 원문은 「경기당」 — 싱글 세션의 둘째 맵에서 다시 기록된 것으로 보아 맵마다 UseCase가 새로 만들어진다고 읽을 수 있으나 확인하지 않았다 [추정]). 따라서 결과가 없는 발사 id는 모두 이 상한 때문이며, **경기 전체에 대한 PASS가 아니다.**

### 12-4. 경기와 수치 [전달]

| 경기 | 구성 | 시간 | 기타 |
|---|---|---|---|
| 멀티 | MatchId `6414765f-5788-45df-a797-87735b03e02e`, 에디터 = Host(Blue), 기기 SM_S901N = Client(Red), epoch `44bf70726221b597` | 16:55:43.97 ~ 16:58:58.347, Blue 승 | StreamSpirit 65 · FlameSpirit 37, combatSchema = 4 양쪽, 호환 실패 0 |
| 싱글 S1 | 34320행 세션의 둘째 맵, AI Human-Balance(Easy), Outer, epoch `4a7dfbbaaa7c59c1` | 17:15:03.8 ~ 17:16:16.33 | 세션 첫 맵은 시작 직후 종료(전투 없음) |
| 싱글 S2 | 35168행 세션의 둘째 맵, AI Spirit-Inferno(Easy), FullyOpen, epoch `4b7daec00e154e25` | 17:19:32.4 ~ 17:20:58.22 | 같음 |

**수명주기 (기록 구간)**

| 단계 | 멀티 | S1 | S2 |
|---|---|---|---|
| reserved(발사 예약) | 33 | 34 | 36 |
| launched(발사) | 32 | 31 | 31 |
| hit(명중) | 30 | 23 | 23 |
| target-lost(비행 중 대상 소실) | 1 | 6 | 4 |
| cancelled-target-unavailable(발사 전 대상 없음 → 취소) | 0 | 2 | 2 |
| 게임 종료 닫기 | 8발 | 4발 | 9발 |

- 결과가 없는 id는 모두 96줄 상한 때문이다. 중복·순서 위반 0.
- 비행 시간: 멀티 0.041~0.687초, S1 0.584~0.628초, S2 0.048~0.622초.
- 대상 반경(설정값과 일치): StreamSpirit 0.167 · FlameSpirit 0.179 · LittleKnight 0.135 · 건물 0.350(S1 26발) · EmberSpirit 0.155. 공격력 60.
- 처리 지연은 수십 ms 이내 — 매 프레임 진행이 동작한다(Plan 위험 4).

**착탄 시각에 피해가 들어갔는가 [전달 — 분석 판정 「확인」]**

- 멀티: Client HP 동기화 도착 − 서버 착탄 = −0.023 ~ +0.122초, − 서버 발사 = +0.33 ~ +0.74초(두 기기 시계 정렬 오차 ±0.05초는 분석 쪽 추정). 명중 30건이 HP 변화와 짝이 맞는다. 유닛 1은 5번째 명중에 사망(HP 300 ÷ 60).
- 싱글: 같은 대상의 기존 경로 hp-dispatch `HpBefore`/`HpAfter`로 **발사 때 변화 없음 · 착탄 직후 −60**을 확인(S1 유닛 13·15, 건물 6은 1700 → 1460 = 착탄 4발 × 60). S2 유닛 2·4·8은 6번째 착탄에 사망.
- StreamSpirit의 기존 「타격 순간 즉시 피해」 hp-dispatch는 0건 → 옛 경로(Plan RM-1)를 타지 않았다는 근거.
- **피해 숫자 1회 방출: 로그 없음 [미확인].**

### 12-5. 경계 사례 [전달]

| 상황 (Plan 1-2절 상황표) | 관측 | 판정 |
|---|---|---|
| 발사 후 — 대상 사망(비행 중) | 멀티 1건 · 싱글 10건, 피해 없음 | 서버 정상 (화면 「마지막 지점 착탄 이펙트만」은 미확인) |
| 발사 전 — 대상 사망 | 싱글 4건 발사 취소 | 서버 정상 |
| 발사 후 — 공격자 사망 | 멀티 id 10 — **같은 프레임** 사례에서 피해 적용 | 서버 정상, 단 시간 간격을 둔 사망은 미관측 |
| 게임 종료 | 닫기 정상(위 표) | 서버 정상 |
| 정지 명령 · 이동 명령 취소 · 늦은 접속 · 시간 간격을 둔 공격자 사망 · 멀티 건물 대상 | 관측 없음 | **미확인** |

- **다른 유닛 [전달]:** `[PROJECTILE]` 로그는 type 17(StreamSpirit)만 나왔다. FlameSpirit·LittleKnight·EmberSpirit는 기존 경로, 멀티 C3 EVIDENCE. Tank 등 다른 투사체 유닛은 경기에 나오지 않았다 [미확인] — 스위치 OFF 유닛의 「기존 동작 유지」는 이 로그로 확인되지 않았다.

### 12-6. 오류 [전달]

- Exception 0.
- 멀티 ERROR 양쪽 각 1건 = `UAS-ATTACK-SHADOW` END FAIL. 원인은 진단 줄 상한으로 버려진 기록(dropped — Host 1779 / Client 1322)이며 게임플레이 오류가 아니다(분석 판정). 상한 처리 코드는 `UnitAttackShadowObserver.cs` 3949~3953행의 `Log` — `_lines >= MaximumNormalLines`이면 `_dropped`만 늘리고 반환 [확인].
- 싱글 ERROR 0, WARN 각 3(진단 상한).
- 기기 로그의 `SyncUnitHealth(...ProjectileDeliveryKey)` 547줄은 `Debug.Log` 스택트레이스이며 예외가 아니다.
- **범위 밖 관찰:** S1 게임 종료(17:16:16.327) 뒤 17:16:18.008에 LittleKnight 기존 경로 피해(유닛 11, 70 → 30). 기존 경로의 「종료 뒤 처리」 문제일 가능성 [추정 — 분석]. 이 작업의 대상 유닛이 아니며 원인은 조사하지 않았다.

### 12-7. 문제·의심

> 2·3은 **진단 표기 문제**이며, 분석은 서버 피해 판정 자체의 오류로 보지 않았다 [전달].

1. **[의심 · 추정, 멀티 Client] 서버가 발사 취소한 공격에도 Client 총구 이펙트(발사음 가능).**
   - 관측 [전달]: Host 총구 VFX 577 = 서버 발사 577. Client는 marker 595개가 **전부** VFX를 냈다(차이 약 18).
   - 코드 [확인]: `UnitCombatUseCase.Projectiles.cs` 105~108행 — `_projectileCueStates` 주석이 「싱글·Host처럼 이 UseCase가 서버 권위인 곳에서만 값이 생긴다(순수 Client는 서버 lease가 대신 막는다)」라고 적는다. 242~248행 `WasLatestProjectileLaunchCancelled`는 그 상태가 없으면 false다. `UnitView.cs` 7687~7696행은 `sourceOnlyPresentation || WasLatestProjectileLaunchCancelled(...)`일 때만 marker를 막는다 → 순수 Client에서는 사실상 `sourceOnlyPresentation`만 남는다. 주석의 「서버 lease가 대신 막는다」가 발사 취소까지 막는지는 확인되지 않았고, 관측 차이 약 18은 막지 못했을 가능성을 가리킨다 [추정].
   - 영향: 사용자 결정 1-2절 「발사 전 끊김 = 투사체·소리·피해 없음」(Plan 1-1 Q6, 규칙 17 개정 F-1)과 어긋날 가능성. 서버 피해에는 영향 없음.
   - ✅ **수정됨 — `639ac1aa`(단계 B), 실기 로그로 효과 확인** *(2026-10-21 갱신 — 종전 「🔴 수정 여부는 사용자 판단 대기(Plan 8절 5차). 지금은 기록만이다」. 근거 [13-6절](#13-6-suspect1-latejoin-errors))*
2. **[진단 오표기]** 발사 확정 시점의 `timer-impact` 기록이 `writerResult=Applied`로 남는다 — `NetworkCombatController.Projectiles.cs` 121~126행이 발사 순간에 `RecordStreamSpiritTimerImpact(..., AttackDamageApplyStatus.Applied)`를 부른다 [확인]. 주석(121~122행)은 의도적으로 「예전 타격 피해 시각 = 이제 발사 시각」 자리에 같은 기록을 남긴다고 적는다. 실제 피해는 착탄 때이므로 `Applied` 표기는 진단상 오해를 낳는다. *(2026-10-21: `639ac1aa`에서 `writerResultOverride: "NotInvoked(...)"`로 정정 — 코드 [확인], 로그 표기는 [미확인]. [13-6절](#13-6-suspect1-latejoin-errors). 위 행 번호 121~126은 단계 A 당시 값이며 지금은 172~182행)*
3. **[진단 FAIL]** StreamSpirit 시간축 END가 overflow/incomplete = 64로 FAIL. 새 경로가 발사 취소를 이 진단에 기록하지 않는 것이 Host 쪽 원인에 기여할 수 있다 [추정 — 분석]. Client도 overflow.
4. **[관찰]** 가까운 대상은 비행 시간이 0.04~0.05초다(사용자 원칙 「눈으로 보이는 속도」 — Plan 1-0 원칙 2). 육안 확인 필요.
5. **[알려진 차이]** Client의 HP 막대가 화면 착탄보다 최대 약 0.1초 먼저 줄어든다 [전달]. HP 동기화는 서버 착탄 직후 도착하고(12-4) 화면 착탄은 Client 표현 지연 0.10초를 두기 때문으로 읽힌다 [추정 — document-manager].

### 12-8. 진단 보강 제안 (제안 — 미실행, 코드 변경은 별도 승인)

> *(2026-10-21 갱신: 앞 네 항목은 `639ac1aa`(단계 B)에서 진단으로 들어갔다 — 경기 전체 `[PROJECTILE-END]` server·view·transport 합계, 보관함 계수, HP 동기화 `Projectile=epoch:id`, Client `markers=…withoutLaunch` 계수(커밋 본문·[13-4절](#13-4-match-and-figures) 로그 줄 [전달]). 「미실행」은 단계 A 회차 당시 상태다. 마지막 항목(볼 장면)은 13-8절 미확인 목록으로 이어진다)*

- 경기 종료 때 투사체 단계·취소 사유 합계를 **96줄 상한과 무관하게 항상 1줄** 남긴다.
- 화면 쪽 요약 1줄: 발사·결과 수신 수, Rejected, ResultTimeouts, 숫자 보관함의 착탄/즉시/타임아웃 방출 수.
- HP 로그에 투사체 번호표(delivery key)를 붙인다.
- Client의 「서버 발사 없이 나온 marker」 수(의심 1의 직접 계측).
- 다음 실기에서 볼 장면: 정지 명령·이동 명령 취소, 시간 간격을 둔 공격자 사망, 멀티 건물 대상, 늦은 접속, Red(반전 좌표) 쪽 육안.

### 12-9. 미확인 목록 (Plan 7절 테스트 항목 기준, StreamSpirit만)

| 항목 | 이번 근거 | 남은 것 |
|---|---|---|
| S-1 착탄 때 이펙트·숫자·피격 함께 | 서버 착탄 피해 시각 [전달] | 화면에서 숫자·VFX가 착탄과 함께 1회 나오는지 — 로그 없음 |
| S-2 움직이는 적 추적 | 사용자 육안 「유닛을 잘 따라감」(멀티) | 싱글 육안 판정 문구 미전달 |
| S-3 비행 중 대상 소실 | 서버 피해 0 (멀티 1 · 싱글 10) | 화면 「마지막 지점 착탄 이펙트만 · 숫자 없음」 |
| S-4 비행 중 공격자 사망 | 같은 프레임 1건 피해 적용 | 시간 간격을 둔 사망 |
| S-5 큰 대상 표면 착탄 | 건물 반경 0.350 사용 (S1 26발, 피해 적용) | 표면 지점의 화면 위치 육안 |
| S-8 발사 전 끊김 | 발사 전 대상 사망 취소 (싱글 4) | 정지·이동 명령 취소, 발사음 없음, 의심 1(Client) |
| M-1 두 화면 일치 | HP 도착 시각 비교(−0.023 ~ +0.122초) | 투사체 위치·착탄 이펙트의 두 화면 일치 |
| M-2 Red 반전 좌표 | — | 미관측 |
| M-3 종료 뒤 피해 없음 | 게임 종료 닫기 정상 | — (scoped) |
| J-1 늦은 접속 복원 | — | 미관측 |
| 스위치 OFF 유닛 기존 동작 | 같은 경기의 FlameSpirit·LittleKnight·EmberSpirit 기존 경로 | Tank·CannonCart·Inferno·Fox·EagleArcher·Mushroom 미등장 |
| 회귀 스위트 | — | **미보고** |

---

<a id="13-stage-b-field-log-analysis-20261021"></a>

## 13. 단계 B 실기 로그 분석 (2026-10-21)

> 기준 checkout HEAD `fd4c04cb` · 대상 구현 커밋 `639ac1aa`(단계 B — 진단 보강 + 의심 1 수정 + 스위치 **Tank · CannonCart · StreamSpirit ON**, `ProjectileDeliveryContracts.cs` 234~237행 `EnabledUnitTypes = { Tank, CannonCart, StreamSpirit }` [확인]) · 검증 수정 커밋 `ac05e189`(회귀 검증만, 운영 코드 무변경 — 커밋 본문 [확인]).
> 출처: 메인 세션의 읽기 전용 로그 분석 원문(scratchpad `stageB_log_analysis.md`)을 전달받음 [전달]. 로그 수치는 모두 [전달]이며, document-manager는 로그 본문을 다시 분석하지 않았다. **영수증의 해시와 이번 회차에 다시 읽은 코드 행만 [확인]이다.**
> 사용자 상시 승인(로그 분석 결과는 확인 없이 해당 Task에 기록)에 따라 작성했다. 이 기록은 코드·에셋·빌드 변경을 승인하지 않는다. 🔴 **13-7절의 수정은 사용자 승인 대기다.** *(2026-10-21 B-2: A안으로 수정 `64b743c8` — 실기 확인은 [14절](#14-stage-b2-kill-shot-number-check-20261021). 이 절의 「승인 대기」·「고치지 않는다」는 13절 회차 당시 상태다)*

<a id="13-1-plain-explanation"></a>

### 13-1. 쉬운 설명

단계 B는 Tank와 CannonCart(대포 수레)를 새 방식 — 서버가 「쐈다」를 기록하고 포탄이 닿는 순간에 피해를 주는 방식 — 으로 바꾼 것이다. 함께 단계 A의 의심 1(서버가 취소한 공격에도 Client 화면에서 총구 이펙트가 나옴)도 고쳤다.

사용자는 멀티 한 경기를 하고 **「Tank·CannonCart는 포탄이 발사되고 맞았는데 피해 숫자가 안 나오는 경우가 있었다」**고 알렸다.

로그와 코드를 보면, 그 경우는 **포탄이 대상을 죽이는 한 발**일 때다. 서버는 대상이 죽으면 「죽었다」를 알리고 **곧바로 그 유닛 오브젝트를 없앤다.** Client 화면은 서버보다 0.1초 늦게 포탄을 그리므로, 화면에서 포탄이 닿아 숫자를 띄우려는 순간에는 숫자를 띄울 자리(대상 오브젝트)가 이미 없다. 숫자 표시는 자리를 못 찾으면 **아무 기록 없이 그냥 건너뛴다.** Tank는 한 발에 300을 줘서 이번 경기의 Tank 명중이 모두 처치 한 발이었기 때문에 특히 눈에 띄었다. 바꾸기 전의 옛 방식에서는 대상이 죽는 순간 남은 숫자를 바로 띄웠으므로 이번 문제는 **단계 B로 생긴 회귀**다.

서버의 피해 판정 자체는 정상이었고, Host와 Client의 수치도 맞았다. 의심 1 수정도 로그로 효과가 확인됐다. 고치는 방법은 세 가지를 제안했으며(13-7) **사용자가 고르기 전에는 고치지 않는다.**

<a id="13-2-log-archive-receipt"></a>

### 13-2. 로그 보관 영수증 (LogRules §1.10)

| 항목 | 값 | 근거 |
|---|---|---|
| 보관 위치 | private 저장소 `rmsepskek02/Hexiege-Logs`, Release `logs-2026-10` | [전달 — 메인 세션] |
| 업로드 주체 | 사용자 | [전달] |
| 만든 도구 | `Assets/Editor/Tools/LogArchiveZip.cs`(manifest `tool` 값), schema `hexiege-log-archive-v1` | [확인 — manifest 사본] |
| 다운로드 검증 | 메인 세션이 manifest의 SHA256과 대조 OK | [전달 — 메인 세션] |
| document-manager 재계산 | 메인이 내려받은 scratchpad 사본에서 ZIP 1의 SHA256, 두 로그 본문의 SHA256을 다시 계산 → 아래 표 값과 일치 | [확인] |

| 회차 | ZIP (remote 이름) | ZIP asset id / 크기(B) | ZIP SHA256 | manifest asset id | 안의 로그 (manifest `sources[]`) |
|---|---|---|---|---|---|
| 01 | `2026-10-21_00_02_server-projectile-stageB_01.zip` | 659639844 / 1158902 | `d6e8cb64e97052fd9097c63d7700e407d57d09f0dd7312998f7a0f9db062d78f` [전달 + 확인] | 659639798 | 에디터 `_Logs/_editor/2026-10-20/RuntimeLog.txt` 14314720 B, sha256 `b1443cc042e2a0d3c8bf440d49cffdeb06f1bf9ce2decc0bbb0ff84dd5e3ed34`, kind `editor_daily` (스냅샷 — 이후 덧붙은 바이트는 없다는 manifest 주석) |
| 02 | `2026-10-21_00_11_server-projectile-stageB_02.zip` | 659661908 / 127378 | `f9708f90e53e65dbcb3c05bca1d61b4685e12cd6826a0fa93f41f2d3e869ff11` [확인 — document-manager 계산, 메인 전달값 없음] | 659661975 | 기기 `_Logs/2026-10-21/00_11_logcat/RuntimeLog_device.txt` 1794974 B, sha256 `7a5b4652874a3c8b7a89ea4f7d6a81ae11e453cfb3d7f657fdb1811e6bea7c4d`, kind `device_capture` |

- 두 manifest 모두 `file_count` 1, `prior_archive_dedupe: not checked by tool`(옛 보관본과의 중복은 도구가 보지 않음 — 도구 쪽 사실이며 이번 분석에는 영향 없음) [확인 — manifest 사본].
- **소스 저장소 쪽 [확인]:** 이 checkout에는 `_Logs/2026-10-21/` 이 없고 git 추적 파일도 없다. 로그 본문은 위 Release에만 있다.
- 에디터 로그 폴더 날짜(`_editor/2026-10-20`)와 ZIP 이름 날짜(`2026-10-21`)가 다른 것은 원시 로그 폴더가 PC 로컬 날짜, ZIP 이름이 KST 날짜를 쓰기 때문으로 읽힌다 [추정]. 같은 로그다(manifest 경로·해시가 일치).

<a id="13-3-conclusion"></a>

### 13-3. 결론

| 축 | 판정 | 범위·근거 |
|---|---|---|
| 서버 판정(발사·착탄·피해·취소·닫기) | **정상 — scoped** | 멀티 1경기(13-4). `[PROJECTILE-END]` server·transport·Client view verdict PASS [전달] |
| 숫자 보관함(Client) | 정상 — 잃어버린 항목 없음 | 보관 221 = 착탄 방출 218 + 경기 종료 때 남은 3. 타임아웃·빈 방출·키 불일치·준비 안 됨 0 [전달] |
| **사망타 피해 숫자(Client 화면)** | 🔴 **누락 — 단계 B 회귀** | 숫자가 「꺼내졌지만 그려지지 못했다」. 원인 경로는 코드로 [확인](13-5), 「실제로 실패했다」는 직접 로그는 없다(숫자 방출 성공·실패 계수가 없음) — 근거 수준은 13-5 끝. *(2026-10-21 B-2: `64b743c8`(13-7 A안)로 수정, 다음 경기 로그 missing 0 · missingDead 0 — [14-4](#14-4-conclusion))* |
| 의심 1 수정(Client 총구 VFX) | **효과 확인** | 13-6 |
| 늦은 접속 | **미발생** | 13-6 |
| 오류 | 예외 0, ERROR는 진단 관측기 FAIL만 | 13-6 |
| 사용자 육안 | 「Tank·CannonCart 투사체 발사·피격, 피해 숫자가 안 나오는 경우가 있었다」 | **어느 화면(Host/Client)에서 봤는지 미전달** [미확인] |
| 회귀 스위트(`Run Full Regression Suite`) | 1차 **FAIL** → `ac05e189` 검증 수정 → 재실행 **미보고** | 1차 실패 「Switch OFF keeps the legacy hit-time damage…」: EagleArcher 0.2f · Tank 0.4f를 double 리터럴로 tick해 float 타격 시각보다 일찍 판정 — 검증 코드 오류, 운영 코드 무관(커밋 본문 [확인]). 재실행은 했다는 전달만 있고 결과는 받지 않았다 — PASS로 적지 않는다 *(2026-10-21 단계 C: 사용자가 `b819dc51` 빌드에서 회귀 스위트 **모두 PASS**를 Console로 확인 — RuntimeLog에는 남지 않는 확인, [15-4](#15-4-conclusion). 「미보고」는 13절 회차 당시 상태다)* |

<a id="13-4-match-and-figures"></a>

### 13-4. 경기와 수치 [전달]

| 항목 | 값 |
|---|---|
| 대상 경기 | MatchId `f33024a1-072b-4d25-9519-3de3cf3ee953`, epoch `4c0d166ccbcc73dd`, 00:00:20 ~ 00:02:17.886, Blue(Host) 승, IsRandomMatch=True |
| 역할 | **에디터 = Client(Red)** · 기기 SM_S901N = Host(Blue) — 단계 A와 반대 |
| 같은 세션의 다른 매치 | MatchId `8352a565…`, 00:00:05 Red 포기로 종료, 투사체 0발 (분석 대상 아님) |
| 싱글 | 없음 |
| 스위치(handshake) | `6,7,17` (Tank, CannonCart, StreamSpirit) |

**`[PROJECTILE-END]` 요약 (epoch 4c0d166c)**

| 항목 | Host server | Client view | Host view |
|---|---|---|---|
| 발사 | reserved 289 / launched 263 (건물 대상 172) | recv·accepted 263 | 263 |
| 결과 | hit 221 (건물 138) / targetLost 37 | 257 | 257 |
| 취소 | 22 (공격자 사망 2 · 대상 사망 20), 닫기 예약 4 + 비행 5 | — | — |
| 화면 finish | — | hit 218 / lost 37, aborts 7 | hit 220 / lost 37, aborts 6 |
| 착탄 VFX | — | played 255 / missing 0 | played 257 / missing 0 |
| rejected · timeouts · open | — | 모두 0 | 모두 0 |
| 순서 | — | resultBeforeArrival 246, **arrivalBeforeResult 11**, maxEndpointWait 0.294 | arrivalBeforeResult 0 |
| displayLag | — | 0.100 ~ 0.234초 (평균 0.105) | 0.000 ~ 0.034초 |
| 숫자 보관함 | — | held 221, atImpact 218, heldNow 3, ownerNotReady 0 | held 221, atImpact 220, 나머지 [미확인] |
| verdict | PASS | PASS | [미확인] |

- transport(Host sent ↔ Client recv/delivered)는 발사 263 · 결과 257 일치, dropped 0, verdict PASS [전달].
- **Host view END 줄은 logcat 줄 길이 한도(약 1024자)에서 잘렸다**(기기 로그 10431행). Host 보관함 뒷부분·markers·verdict는 [미확인]. *(2026-10-21 B-2: `64b743c8`에서 view END를 `v=2` 2줄(`part=1/2`·`part=2/2`)로 나눴다 — [14-2](#14-2-fix-contents) 5행, 잘림 여부 [14-7](#14-7-view-end-split))*
- 정합: Host 명중 221 = Client 투사체 번호표가 붙은 HP 동기화 221(유닛 83 + 건물 138, id 중복 0). 번호표 없는 HP 동기화 5줄(150 피해)은 비투사체 공격. Host 서버 `path=6:s7|7:s6|17:s276` 합 289 = reserved → Tank 7회 · CannonCart 6회 예약.
- 경기 종료 때 남은 3건(heldNow 3)은 id 277·279·280의 HQ 마지막 타격으로, 게임 종료로 화면이 거둬져 숫자가 없다 — 설계대로 [전달].

<a id="13-5-death-blow-number-missing"></a>

### 13-5. 사망타 피해 숫자 누락 — 원인

**사망타 대조 (Client 로그) [전달]**

| id | 종류 | 대상 | 피해 → HP | HP 동기화 | 사망 RPC | 화면 finish |
|---|---|---|---|---|---|---|
| 4 | CannonCart | 유닛 0 | 200 → 100 | 01:03.090 | — | 01:03.095 → 숫자 정상 경로 |
| 8 | CannonCart | 유닛 0 | 100 → **0** | 01:06.867 | 01:06.868 | 01:07.028 (+0.16초) |
| 12 | Tank | 유닛 1 | 300 → **0** | 01:11.225 | 01:11.225 | 01:11.391 |
| 18 | Tank | 유닛 5 | 300 → **0** | 01:15.195 | 01:15.196 | 01:15.375 |
| 23 | Tank | 유닛 2 | 300 → **0** | 01:19.214 | 01:19.216 | 01:19.375 |
| 29 | CannonCart | 유닛 4 | 200 → 100 | 01:22.365 | — | 표본 밖 |
| 31 | Tank | 유닛 7 | 300 → **0** | 01:23.377 | 01:23.377 | 표본 밖 |
| 42 · 61 | CannonCart [추정] | 유닛 4 · 11 | 100 → **0** | — | 같은 ms | 표본 밖 |
| 43 · 73 · 114 | Tank [추정] | 유닛 6 · 8 · 12 | 300 → **0** | — | 같은 ms | 표본 밖 |

- 종류는 Host `[PROJECTILE] stage=hit` 줄로 확인했으나 Host 상세 로그가 96줄 상한이라 id 31까지만 있다. 그 뒤는 피해량(300 = Tank, 200·100 = CannonCart)으로 구분한 [추정].
- **사망타 비율: Tank 7/7, CannonCart 3/6.** StreamSpirit(60 피해)도 사망타가 있다(유닛 대상 6건 id 11·41·47·62·120·133, 건물 대상 8건) — 같은 문제를 겪되 마지막 한 발뿐이라 덜 보였을 것이다 [추정].
- 모든 사망타가 「HP 동기화 → 사망 RPC(0~2ms 뒤) → 화면 finish(0.10초 이상 뒤)」 순서였다. Client displayLag 최솟값이 0.100이므로 finish가 사망보다 먼저 오는 경우는 없다.
- 점검했지만 해당 없는 경로 [전달]: 보관 타임아웃, 빈 방출, ownerNotReady, 결과 먼저 도착(rejected 0), 키 불일치(held = atImpact + heldNow), 다중 대상, 건물 2배 피해(Tank·CannonCart는 이번 경기에서 건물을 때리지 않음).

**코드 경로 (HEAD `fd4c04cb`)**

| # | 위치 | 내용 | 근거 |
|---|---|---|---|
| 1 | `NetworkCombatController.cs` `OnUnitDied`(2793행~) 2858 · 2901 | 서버가 `EntityDiedClientRpc(unitId, true)`를 보내고, **같은 함수 안에서** `netObj.Despawn(destroy: true)`를 부른다. 사망 연출을 기다리는 지연이 없다 | [확인] |
| 2 | `UnitView.cs` 2002~2006 | 멀티 순수 Client는 사망 시 아무것도 하지 않고 반환(파괴는 NGO 디스폰) | [확인] |
| 3 | `ProjectilePresentationPresenter.cs` 195~196 · 50 | 순수 Client는 서버 시각 − `ClientPresentationDelaySeconds`(= `AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds` 0.10, 67행)로 그린다 | [확인] |
| 4 | 같은 파일 281~283 | 화면 착탄(명중) 순간에야 `_hits?.ReleaseProjectileImpact(slot.Key)` | [확인] |
| 5 | `HitPresentationQueue.cs` 468~473 | 번호표가 있고 소비자가 준비되면 투사체 피해는 보관함(`_projectileHold`)에만 들어간다 | [확인] |
| 6 | 같은 파일 654 `OnUnitDied` → 657 · 1084 `FlushTarget` | `_exactRendezvous`와 공격자 대기열(`_pendingByAttacker`)만 비운다. `_projectileHold`를 쓰는 곳은 127 · 145 · 148 · 159 · 391 · 471 · 679행뿐이고 `FlushTarget`에는 없다 — Plan 1-1 Q7 결정 「피해 숫자·착탄은 서버 착탄 기준」과 코드 주석(`ProjectilePresentationPresenter.cs` 281행 「숫자는 착탄 시각 1회」)에 맞는 동작 | [확인] |
| 7 | 같은 파일 1027 | `Emit`이 `_hpTextSpawner.TryShowDamage(evt)`를 호출, 결과는 `ObserveFoxLegacyPresentation`에 넘길 뿐 투사체 쪽 계수는 없다 | [확인] |
| 8 | `FloatingHpTextSpawner.cs` 217~222 | 대상 위치가 `Vector3.zero`면 `return false` — 실패 로그 없음 | [확인] |
| 9 | `PresentationPoseProvider.cs` 31~37 · 53~57 | 오브젝트가 없으면(Unity null) Transform null → 위치 `Vector3.zero` | [확인] |
| 10 | `HitPresentationQueue.cs` 1031~1049 | 같은 이유로 `Emit`의 피격 VFX·펀치도 생략된다. 착탄 VFX는 서버 착탄 지점에 따로 나오므로 impactVfx는 정상(13-4) | [확인 — 코드] |
| 11 | `BuildingFactory.cs` 113~116 | 건물은 사망 즉시 사전에서 지우고 `Destroy` — 건물 사망타도 같은 결과. 이 경로는 Host에서도 같은 프레임에 사전에서 빠지므로 **Host 건물 사망타도 누락 가능** | 코드 [확인] · Host 누락 [추정] |

- **옛 경로와의 차이 — 회귀 [전달 — 분석]:** 단계 B 전 Tank·CannonCart(옛 경로)는 타격 순간 피해 → 공격자 대기열에 보류 → 대상 사망 순간 `FlushTarget`이 **오브젝트가 아직 살아 있을 때** 숫자 「0」을 냈다. 6행의 `FlushTarget`이 `_pendingByAttacker` 매칭 항목을 `Emit`하는 것은 [확인]이고, 옛 Tank 피해가 그 대기열을 탔다는 것은 [전달]이다.
- **근거 수준:** 원인 경로는 코드로 [확인]. 「숫자가 실제로 실패했다」는 직접 로그는 없다 — 투사체 숫자 방출의 성공·실패 계수가 없다. Client에서 디스폰이 처리된 정확한 시각도 로그가 없어 [추정]이다. 다만 디스폰은 사망 RPC와 같은 서버 호출에서 나오고 finish는 0.16초 뒤라 근거는 강하다(분석 판정).

**Host 화면 · 싱글 [추정 / 미확인]**

- Host는 arrivalBeforeResult 0이고 displayLag 분포가 resultLag와 같다 — 결과를 받은 그 프레임에 finish한다 [전달]. Host의 `Despawn(destroy: true)`는 프레임 끝에 파괴되고 UnitFactory 사전에서도 그 자리에서 지우지 않으므로 **Host 화면의 유닛 사망타 숫자는 나왔을 가능성이 높다** [추정 — 분석]. 표시 성공 로그는 없다.
- 싱글은 `GameBootstrapper` 틱과 Presenter의 실행 순서가 정해져 있지 않아(두 스크립트 모두 executionOrder 0 [전달]) [미확인].

<a id="13-6-suspect1-latejoin-errors"></a>

### 13-6. 의심 1 수정 효과 · 늦은 접속 · 오류 [전달]

**의심 1 수정 효과 (Client) — 확인**

- 수정 내용(커밋 `639ac1aa` 본문 [확인]): 새 경로 유닛의 총구 VFX·발사음을 **서버 발사 기록을 화면에 표시하는 시점**에 재생한다. 애니메이션 marker는 총구 VFX를 내지 않는다(기존 억제 블록은 주석으로 보존).
- `markers=server:267|sourceOnly:0|muzzleAtMarker:0|withoutLaunch:5|launchWithoutMarker:1|attackers:34`. 단계 A에서는 Client marker 595개 전부가 총구 VFX를 냈는데(12-7 의심 1) 이번에는 **marker 자리 총구 VFX 0**. 서버 발사 없이 나온 marker 5개도 총구 VFX를 내지 않았다 → 효과 [확인 — 분석 판정].
- `cue=played:260|vfxStarted:260|skippedMidFlight:1|noAttackerView:1` — 발사 신호는 서버 발사 기록 기준으로만 나왔다. Host 263/263.
- `path=6:s6|7:s6|17:s255`, `legacyTracer=none` — Tank·CannonCart가 새 경로만 탔다(이중 트레이서 없음). Client Tank marker 6 대 명중 7은 launchWithoutMarker 1과 맞는다 [추정].
- 발사음이 실제로 들리지 않았는지는 소리 로그가 없어 [미확인].

**진단 오표기 수정(12-7 항목 2)** — `NetworkCombatController.Projectiles.cs` 172~182행이 발사 순간 `writerResultOverride: "NotInvoked(server-projectile-launch; damage-at-impact)"`를 넘긴다(옛 호출은 176~177행 주석 보존) [확인 — 코드]. 이번 로그에서 그 표기를 본 기록은 전달받지 않았다 [미확인].

**늦은 접속 — 미발생.** 스냅샷 requests 1 / sends 1 / launches 0, Client requested 1 / expected 0 / received 0, epochResets 1(정상). midFlight 1건은 beginLag 최대 0.256초로 프레임 끊김으로 보인다 [추정]. → Plan 7절 J-1은 이번에도 **미관측**.

**오류·예외**

- 예외 0. 기기 로그의 「Exception」 1081건은 `Debug.Log` 스택트레이스 문구.
- ERROR 양쪽 각 2건: `UAS-MOVE-AUTH` END(첫 매치), `UAS-ATTACK-SHADOW` END(본 매치) — 진단 관측기 FAIL이며 게임플레이 오류가 아니다(단계 A와 같은 종류, 12-6).
- WARN: UnitFactory 296건은 `InitializeUnitView` 재시도(정상 경로), WAVE-DIAG clock-invalid 3건. 투사체 관련 WARN·fault 0.

<a id="13-7-fix-proposals"></a>

### 13-7. 수정 방향 제안 (제안 — 미실행, 🔴 사용자 승인 대기)

> *(2026-10-21 B-2 갱신: **A안이 `64b743c8`에서 구현됐다**(Plan U-17 닫힘). 아래 표·fixture·진단 제안은 제안 당시 문구 그대로 두며, 실제 구현과 다른 점은 [14-2](#14-2-fix-contents)에 적었다 — 특히 `text=` 계수 순서는 실제로 `played|fallback|missing|missingDead`다. 제목의 「미실행 · 승인 대기」는 13절 회차 당시 상태다)*

> 아래는 분석의 제안을 옮긴 것이다 [전달]. 어느 것도 실행하지 않았다. 코드 변경은 사용자 승인 뒤 game-programmer가 한다.

| 안 | 내용 | 장점 | 단점 |
|---|---|---|---|
| **A (추천)** | 숫자를 내보낼 때 대상 위치가 없으면 **대체 위치**를 쓴다. `ReleaseProjectileImpact(key, Vector3 screenImpactPoint)`로 Presenter가 화면 착탄 지점을 넘기고, 보관함이 내보낸 항목은 `TryShowDamage`가 실패하면 기존 `TryShowDamageAt(evt, point)`(`FloatingHpTextSpawner.cs` 232행 — C3가 쓰는 경로 [확인 — 메서드 존재])로 표시. 피격 VFX도 같은 지점(`evt.Entity`의 UnitData는 남아 있음). 타임아웃 방출용으로는 보관할 때(HP 이벤트 시점) 대상 위치를 함께 기억해 대체값으로 쓴다 | Q7(숫자는 착탄 기준, 1회)을 지킨다. 서버·HP·RPC 무변경. 건물 사망타와 Host·싱글 순서 문제도 함께 해결 | 사망타에서는 대상이 사라진 자리에 숫자가 뜬다. 펀치는 생략 |
| B (비추천) | 대상 사망 시 보관함을 즉시 비운다 | 변경이 작다 | 숫자가 착탄보다 최대 0.1초 이상 먼저 나와 **Q7을 어긴다** |
| C (비추천) | Client 디스폰을 지연시킨다 | 오브젝트가 남아 기존 경로로 표시 | 네트워크·사망 흐름 전체에 영향 |

- 규칙 문서 영향: A는 Plan 1-1 Q7 「피해 숫자·착탄은 서버 착탄 기준」을 그대로 지키므로 규칙 문장 변경은 분석 범위에서 제기되지 않았다. B는 Q7과 그 반영 문장을 바꿔야 한다 [추정 — document-manager]. 어느 안이든 규칙 문장이 바뀌면 변경 전 사용자 보고 대상이다.

**검증 fixture 후보 [전달]**

1. 유닛 사망타: HP 0 보관 → `OnUnitDied` → 위치 null → Release(key, point) → 텍스트 1회, 「0」, 위치 = point.
2. 건물 사망타: 사전 제거 후 Release → 1회 표시.
3. 살아 있는 대상: 기존과 같이 대상 위치에 1회(회귀 없음).
4. 타임아웃 방출: 보관할 때 기억한 위치로 1회.
5. 순서 역전: Release가 먼저, HP가 나중 → 바로 1회.
6. 중복 0: 같은 키를 두 번 Release해도 1회.

**추가 진단 제안 [전달]**

- 투사체 숫자 방출 결과 계수를 보관함 END에 붙인다: `text=played:N|missing:N|fallback:N|missingDead:N` — 지금은 실패가 로그에 남지 않는다.
- Host view END 줄이 logcat에서 잘리므로 2줄로 나눈다.
- 다음 실기에서 Tank 사망타를 **Client·Host 화면 양쪽에서** 육안 확인한다.

<a id="13-8-unconfirmed"></a>

### 13-8. 미확인 목록

| 항목 | 이번 근거 | 남은 것 |
|---|---|---|
| 사용자가 누락을 본 화면 | — | Host / Client 어느 쪽인지 |
| Host 화면 유닛 사망타 숫자 | 구조상 나왔을 가능성 [추정] | 표시 성공 로그·육안 |
| Host 건물 사망타 숫자 | 같은 프레임 사전 제거 [추정] | 로그·육안 |
| Host view 보관함 뒷부분 · markers · verdict | 줄 잘림 | 줄 분할 뒤 재관측 |
| 싱글 모드 동작 | — | 이번 회차 싱글 없음 |
| Client 디스폰 처리 정확한 시각 | 같은 서버 호출에서 나옴 [추정] | 로그 |
| 발사음 미재생(의심 1) | 총구 VFX 계수만 | 소리 계수·청취 |
| Plan 7절 S-1 ~ S-10 · M-1 · M-2 (Tank·CannonCart) | 서버 판정 [전달] · 사망타 숫자 누락 | 화면 항목별 판정은 전달되지 않았다 |
| J-1 늦은 접속 | 미발생 | 미관측 |
| 회귀 스위트 재실행 | 사용자가 실행함 [전달] | **결과 미보고** |

*(2026-10-21 단계 B-2: 위 표 중 「Host view 보관함 뒷부분 · markers · verdict」·「Host 건물 사망타 숫자」는 [14절](#14-stage-b2-kill-shot-number-check-20261021)에서 다시 관측됐다. 표 원문은 13절 회차 당시 상태로 둔다)* *(2026-10-21 단계 C: 「회귀 스위트 재실행」 행은 사용자 Console 확인 **PASS**로 닫혔다(RuntimeLog에는 남지 않음) — [15-4](#15-4-conclusion))*

---

<a id="14-stage-b2-kill-shot-number-check-20261021"></a>

## 14. 단계 B-2 사망타 숫자 수정 실기 확인 (2026-10-21)

> 기준 checkout HEAD `64b743c8` · 대상 수정 커밋 `64b743c8` 「fix(vfx): 투사체 사망타 피해 숫자 누락 수정 (제안 A)」 — 13-7절 **A안**(Plan U-17). 바뀐 파일 3개: `HitPresentationQueue.cs` · `ProjectilePresentationPresenter.cs` · `RunUnitActionSelfValidation.ServerProjectiles.cs`, 커밋 본문 「서버·네트워크·HP 경로 무변경」 [확인 — `git show --stat`].
> 출처: 메인 세션의 읽기 전용 로그 분석 원문(scratchpad `stageB2_log_analysis.md`)을 전달받음 [전달]. 로그 수치는 모두 [전달]이며 document-manager는 로그 본문을 다시 분석하지 않았다. **영수증의 해시와 이번 회차에 다시 읽은 코드 행만 [확인]이다.**
> 사용자 상시 승인(로그 분석 결과는 확인 없이 해당 Task에 기록)에 따라 작성했다. 이 기록은 코드·에셋·빌드 변경을 승인하지 않는다.

<a id="14-1-plain-explanation"></a>

### 14-1. 쉬운 설명

13절에서 찾은 문제는 「포탄이 대상을 죽이는 마지막 한 발이면, 화면에서 포탄이 닿을 때 대상이 이미 사라져 있어 피해 숫자가 안 뜬다」였다. 이번 수정은 그럴 때 **포탄이 화면에서 닿은 그 자리**에 숫자 「0」을 띄우도록 했다. 대상이 살아 있으면 지금까지처럼 대상 위치에 뜬다. 서버의 피해 판정이나 통신은 바꾸지 않았다.

사용자는 멀티 한 경기를 하고 **「테스트했고 육안상으론 괜찮아 보여」**라고 알렸다. 사용자가 정한 기준(2026-10-21)은 **「대상을 죽이는 마지막 한 발은 "0"을 보여 주고, 그 뒤에 도착하는 투사체는 숫자를 보여 주지 않는다」**이다.

로그로 보면 이번 경기에서 **꺼낸 숫자는 하나도 빠짐없이 화면에 그려졌다**(양쪽 화면 모두 「못 그림」 0, 「대상이 없어서 못 그림」 0). 대상이 죽은 뒤에 도착한 투사체 11발은 피해 이벤트 자체가 없어 숫자가 나오지 않았다 — 사용자 기준과 맞는다. 다만 사망타 한 발 한 발이 「대체 위치」로 그려졌는지는 발마다 남는 로그가 없어 **개수로 맞춘 결과**이고, 회귀 스위트 결과는 로그에 남지 않아 **아직 모른다.**

<a id="14-2-fix-contents"></a>

### 14-2. 수정 내용 (`64b743c8`) [확인 — 코드]

| # | 위치 | 내용 |
|---|---|---|
| 1 | `ProjectilePresentationPresenter.cs` 287 · 371 `NumberFallbackPoint` | 화면 착탄(명중) 순간 `ReleaseProjectileImpact(slot.Key, NumberFallbackPoint(...))`로 **화면 착탄 지점**을 함께 넘긴다. 높이는 대상 화면 중심의 마지막 높이. 화면 오브젝트를 못 만든 투사체는 NaN(= 지점 없음). 옛 호출 `ReleaseProjectileImpact(slot.Key)`는 285행 주석으로 보존 |
| 2 | `HitPresentationQueue.cs` 195 `ReleaseProjectileImpact(key, screenImpactPoint)` | 순서 역전(착탄 신호가 HP 이벤트보다 먼저)이면 그 지점을 기억했다가 HP 이벤트 도착 때 쓴다 |
| 3 | 같은 파일 1153 `EmitProjectile` | 숫자 위치 규칙: ① 대상 화면 오브젝트가 있으면 그 위치 ② 없으면 화면 착탄 지점 ③ 그것도 없으면(타임아웃 등) 보관할 때 기억한 대상 위치(`RememberedPosition`) ④ 모두 없으면 `missing`으로 센다. 숫자 값은 언제나 그 이벤트의 남은 HP(`evt.CurrentHp`, 1150행 주석) — 사망타면 「0」. 그리기는 `TryShowDamageAt`(1199행). 펀치는 대상이 없으면 생략 |
| 4 | 같은 파일 144~150 · 175 | 진단 계수 `played`(대상 위치) · `fallback`(대체 위치) · `missing`(못 그림) · `missingDead`(missing 중 대상 오브젝트가 없던 수 — 이번 버그 증상, 0이어야 정상). 불변식: played + fallback + missing = atImpact + immediateAfterRelease + immediateCapacity + timeout |
| 5 | `ProjectilePresentationPresenter.cs` 418~420 · 495~516 | `[PROJECTILE-END] side=view`를 **`v=2` 2줄(`part=1/2` · `part=2/2`)** 로 나눴다(logcat 약 1024자 잘림 대응 — 13-4). 두 줄 모두 같은 머리(role·epoch·reason). 1줄째 = 개수 … `hold=` 뒤에 **`text=played:N\|fallback:N\|missing:N\|missingDead:N`**, 2줄째 = displayLag·resultLag·beginLag·markers·verdict. `missing` > 0이면 verdict에 `text-missing` 주의 표시. 옛 1줄 형식(`v=1`)은 주석으로 보존 |
| 6 | `RunUnitActionSelfValidation.ServerProjectiles.cs` 129 · 643 `ValidateServerProjectileKillShotNumbers` | 회귀 검증: 유닛·건물 사망타(디스폰 뒤 착탄 지점에 「0」 1회), 살아 있는 대상 회귀 없음, 타임아웃 기억 위치, 순서 역전 1회, 중복 Release 1회, **이미 죽은 대상에 닿는 후속 투사체 숫자 없음**, `text=` 계수 — 13-7 fixture 후보 ①~⑥ + 후속 투사체 |

- 13-7의 제안 문구 `text=played:N|missing:N|fallback:N|missingDead:N`과 **실제 순서가 다르다** — 실제는 `played|fallback|missing|missingDead`(위 4·5행). 13-7은 제안 당시 문구로 둔다.

<a id="14-3-log-archive-receipt"></a>

### 14-3. 로그 보관 영수증 (LogRules §1.10)

| 항목 | 값 | 근거 |
|---|---|---|
| 보관 위치 | private 저장소 `rmsepskek02/Hexiege-Logs`, Release `logs-2026-10` | [전달 — 메인 세션] · manifest `upload_target` [확인 — manifest 사본] |
| 업로드 주체 | 사용자 | manifest `upload: upload performed by user` [확인 — manifest 사본] |
| 만든 도구 | `Assets/Editor/Tools/LogArchiveZip.cs`, schema `hexiege-log-archive-v1`, `task_name` `server-projectile-stageB2`, `sequence` 1 | [확인 — manifest 사본] |
| 다운로드 검증 | 메인 세션이 manifest와 대조 OK | [전달 — 메인 세션] |
| document-manager 재계산 | 메인이 내려받은 scratchpad 사본에서 ZIP과 두 로그 본문의 SHA256을 다시 계산 → 아래 값·manifest 값과 일치 | [확인] |

| ZIP (remote 이름) | ZIP asset id / 크기(B) | ZIP SHA256 | manifest asset id | 시작 날짜 |
|---|---|---|---|---|
| `2026-10-21_01_59_server-projectile-stageB2_01.zip` | 659929691 / 512439 | `0870f00a9c9c736af6ba94dad6e1acfd2cc261bfd48c5193806ff05165483dfe` [전달 + 확인] | 659929763 | `start_date` 2026-10-21 |

| 안의 로그 (manifest `sources[]`) | kind | 크기(B) | SHA256 [확인] |
|---|---|---|---|
| 기기 `_Logs/2026-10-21/01_59_logcat/RuntimeLog_device.txt` | `device_capture` | 7775023 | `b8b34764cc07a08735a6302760851d05ebf5545f91c94d444cc6f5c82d9e1928` |
| 에디터 `_Logs/_editor/2026-10-21/RuntimeLog.txt` | `editor_daily`(스냅샷 — 이후 덧붙은 바이트는 없다는 manifest 주석) | 470003 | `eaaca83230533bf70e4b0976b5898b3938ebb3f1e45d092f7dfcb254b8333f64` |

- manifest `file_count` 2, `skipped_already_archived` 1, `manifests_scanned` 3, `prior_archive_dedupe: not checked by tool` [확인 — manifest 사본]. 건너뛴 1개가 어느 파일인지는 manifest에 적히지 않는다 [미확인].
- 이번에는 에디터 로그 폴더 날짜(`_editor/2026-10-21`)와 ZIP 이름 날짜가 같다(13-2 끝 문단의 날짜 차이는 이번엔 없음) [확인 — manifest 경로].
- 기기 로그 앞부분(pid 19271, 23:59~00:02, view END `v=1`)은 지난 단계 B 세션이라 분석에서 뺐다 [전달].
- **소스 저장소 쪽 [확인]:** 이 checkout에는 `_Logs/2026-10-21/`이 없고 git 추적 파일도 없다. 로그 본문은 위 Release에만 있다.

<a id="14-4-conclusion"></a>

### 14-4. 결론

| 축 | 판정 | 범위·근거 |
|---|---|---|
| **사망타 피해 숫자(양쪽 화면)** | ✅ **로그상 정상 — scoped** | 꺼낸 숫자 전부 표시: Host 182건 = played 179 + fallback 3, Client 181건 = played 158 + fallback 23, **missing 0 · missingDead 0**(양쪽) [전달]. 사망타별 fallback 대응은 개수로 맞춘 [추정](14-6) |
| 사용자 기준(마지막 한 발 「0」, 그 뒤 투사체 숫자 없음) | **로그와 맞음** | missing 0 · missingDead 0, 서버 targetLost 11 = 화면 finish lost 11(양쪽), 명중 183 = HP 이벤트 183 → lost 11발은 HP 이벤트 0 → 숫자 없음 [전달]. lost id별 직접 대조는 못 함(서버 상세 로그 id 33까지) |
| 서버 판정 · transport · 화면(view) | **PASS — scoped** | 모든 `[PROJECTILE-END]` verdict PASS, rejected · faults · timeouts · open 0 [전달] |
| view END 2줄 잘림 | **잘림 없음** | 14-7 |
| 의심 1 수정(총구 VFX) | **효과 유지** | `muzzleAtMarker:0`(양쪽) [전달] |
| 오류 | 예외 0, ERROR는 진단 관측기 FAIL만 | 14-9 |
| 사용자 육안 | 「테스트했고 육안상으론 괜찮아 보여」 | 어느 화면(Host/Client)을 봤는지·싱글 여부는 전달 없음 [미확인] |
| 회귀 스위트(`Run Full Regression Suite`) | **미확인 — 결과 미보고** | PASS 문구(`[UAS-DIAG][SERVER-PROJECTILE] self-validation PASS`)는 `Debug.Log`(검증 파일 170행 [확인])로 Console에만 나온다. RuntimeLog의 Unity 로그 훅은 Error·Exception만 받는다(`LogSessionOwner.cs` 389행 [확인]). 에디터 로그에도 그 줄 0건 [전달]. 사용자 Console 결과 미보고 — PASS로 적지 않는다 *(2026-10-21 단계 C: 사용자 Console 확인 **PASS**(`b819dc51` 빌드의 스위트) — 위 코드 근거대로 RuntimeLog에는 남지 않는 확인이다, [15-4](#15-4-conclusion). 「미확인」은 14절 회차 당시 상태다)* |

<a id="14-5-match-and-figures"></a>

### 14-5. 경기와 수치 [전달]

| 항목 | 값 |
|---|---|
| 대상 경기 | MatchId `9bccd143-5a61-4805-af4b-97e22e32d7a2`, epoch `4314f9a5110251fd`, 01:56:59 ~ 01:59:01.901(Host 기준), Blue 승, IsRandomMatch=True |
| 역할 | **에디터 = Host(Blue, 종족 1)** · 기기 SM_S901N = Client(Red, 종족 0, pid 32484, view END `v=2` → 새 빌드) — 13절(단계 B)과 반대, 12절(단계 A)과 같음 |
| 스위치 | `6,7,17` (Tank, CannonCart, StreamSpirit) |
| 싱글 | 없음 |

| 항목 | Host server | Host transport | Client transport | Host view | Client view |
|---|---|---|---|---|---|
| 발사 | reserved 219 / launched 201 (건물 대상 113) | sent 201 | recv / delivered 201 | accepted 201, begin 201 | accepted 201, begin 200 |
| 결과 | hit 183 (건물 102) / lost 11 | sent 193 | recv / delivered 193 | recv 193, finish 182 + 11 | recv 193, finish 181 + 11 |
| 취소·닫기 | cancelled 15 (공격자 사망 2 · 대상 사망 13), closedReserved 3, closedInFlight 7 | — | dropped 0 | aborts 8 | aborts 8 |
| rejected / faults | — | — | — | 0 / 0 | 0 / 0 |
| timeouts / open | — | — | — | 0 / 0 | 0 / 0 |
| 순서 | — | — | — | resultBeforeArrival 193, arrivalBeforeResult 0 | 같음 |
| 보관함(hold) | — | — | — | held 183 = atImpact 182 + heldNow 1 | held 183 = atImpact 181 + heldNow 2 |
| **text** (played / fallback / missing / missingDead) | — | — | — | **179 / 3 / 0 / 0** | **158 / 23 / 0 / 0** |
| displayLag(초) | — | — | — | 0.000 ~ 0.012 | 0.100 ~ 0.134 (평균 0.117) |
| verdict | PASS (balance=ok) | PASS | PASS | PASS | PASS |

- **검산(14-2 4행 불변식):** Host 179 + 3 + 0 = 182 = atImpact 182 + 0 + 0 + 0 · Client 158 + 23 + 0 = 181 = 181 + 0 + 0 + 0.
- **서버 수지:** 219 = 201 + 15 + 3 · 201 = 183 + 11 + 7. 결과 전송 193이 hit + lost 194보다 1 적은 것은 HQ 마지막 타격(id 209 — 결과를 보내기 전에 경기 종료, heldNow로 남아 숫자 없음 = 설계대로).
- **Client가 1씩 적은 부분:** begin 200(발사 201), finish 192(결과 193), heldNow 2는 경기 종료 직전 0.1초 표시 지연 안에 있던 건이다. id 208은 HP 동기화 02.475, 경기 종료 02.613 [추정 — 분석].
- 정합: Client 투사체 번호표 HP 동기화 183(유닛 81 + 건물 102) = 서버 hit 183 · hitBuilding 102, id 중복 0, 번호표 없는 HP 동기화 0.

<a id="14-6-kill-shot-reconciliation"></a>

### 14-6. 사망타 대조 (Client HP 동기화 ↔ 숫자 결과) [전달]

- 이번 경기 사망 17건은 **모두 투사체 사망타**(유닛 13 · 건물 4). 사망 RPC 17건은 모두 HP 0 동기화 1~3ms 뒤.

| id | 대상 | 피해 | 종류 | 종류 근거 |
|---|---|---|---|---|
| 5 · 14 | 유닛 0 · 1 | 100 | CannonCart | 서버 상세 로그 |
| 13 | 유닛 2 | 20 | StreamSpirit | 서버 상세 로그 |
| 16 · 18 · 25 | 유닛 3 · 4 · 6 | 300 | Tank | 서버 상세 로그 |
| 45 · 70 | 유닛 7 · 5 | 300 | Tank | 피해량 [추정] |
| 38 · 46 · 56 · 79 · 110 | 유닛 12 · 10 · 21 · 25 · 31 | 20/40 | StreamSpirit | 피해량 [추정] |
| 99 · 159 · 178 | 건물 7 · 9 · 2 | 40/20/40 | StreamSpirit | 피해량 [추정] |
| 209 | 건물 0 (HQ) | 20 | 경기 종료 타격 | 결과 전 경기 종료 — 숫자 없음이 설계대로 |

- Tank는 예약 5회(`path=6:s5`)였고 5발 모두 사망타였다.
- Client 화면 표본 로그가 남은 사망타 6건(5 · 13 · 14 · 16 · 18 · 25)은 모두 finish, impactVfx=1. HP 동기화 → 화면 finish 0.106 ~ 0.174초, 사망 RPC는 HP 동기화 뒤 0.003초 이내 → 이 6건은 대상이 이미 사라진 뒤 finish했다 = fallback 경로.
- **Client fallback 23건의 구성 [추정 — 개수 대응]:** 경기 종료 HQ 한 발을 뺀 사망타 16건 + 사망타가 아닌 명중 7건(이 발의 화면 착탄 전에 다른 발로 대상이 먼저 디스폰된 경우). 7건 후보는 사망 0.03 ~ 0.07초 전 HP 동기화 4건(id 44 · 77 · 78 · 98)과 0.10 ~ 0.17초 전 6건(id 96 · 160 · 208 · 109 · 97 · 175). 이 7건은 그 발을 맞은 뒤의 **남은 HP**(예: 「40」)가 착탄 지점에 뜬다(14-2 3행 — 값은 `evt.CurrentHp`).
- **Host fallback 3건 [추정]:** 건물 사망타 3건(99 · 159 · 178)과 개수가 같다. `BuildingFactory.cs` 113~116행이 사망 즉시 사전에서 지우고 `Destroy`한다(13-5 11행 [확인]). Host의 유닛 사망타는 대상 오브젝트가 그 프레임 끝까지 남아 played로 셌을 것이다 — 13-5 「Host 화면」 추정과 같은 방향.
- **사망 뒤 도착한 투사체:** 서버 targetLost 11 = 화면 finish lost 11(양쪽), 사망한 대상에 사망 뒤 HP 동기화 0건, 명중 183 = HP 이벤트 183 → lost 11발은 HP 이벤트가 없어 숫자 없음(착탄 VFX만). lost id별 직접 대조는 서버 상세 로그가 id 33까지라 못 했다.

<a id="14-7-view-end-split"></a>

### 14-7. view END 2줄 잘림 여부 [전달]

- 기기(이번엔 Client) 줄 길이 1줄째 873자 · 2줄째 670자 — 잘리던 한도(약 1024자)보다 짧다. 1줄째는 마지막 토큰 `text=…`로, 2줄째는 `verdict=PASS`로 끝난다 → **잘림 없음.** 에디터(Host) 쪽 줄도 온전하다.
- 13-4의 「Host view END 줄 잘림」은 그때 Host가 **기기**였기 때문이고, 이번 Host는 에디터라 logcat 한도를 받지 않는다. 기기가 Host일 때의 2줄 형식 관측은 아직 없다 [미확인].

<a id="14-8-stage-ab-open-items"></a>

### 14-8. 단계 A·B 미확인 항목 — 이번 결과 [전달]

| 항목 (12-9 · 13-8) | 이번 결과 |
|---|---|
| 비행 중 공격자 사망(시간 간격) | **관측됨** — 서버 `attackerDead=hit:5`, 간격 0.017 ~ 0.260초. id 14(0.226초) · 15(0.260초)는 Client에서도 finish, impactVfx=1. id 14는 사망타라 fallback 경로 |
| Client 화면 사망타 숫자 | **관측됨** — missingDead 0. 13절에서 누락이 났던 Client 화면이 이번엔 기기였다 |
| 건물 사망타 숫자 | **관측됨** — 4건(Host 화면 3건 fallback [추정]) |
| Host view 보관함 · markers · verdict | **관측됨** — Host view verdict PASS(이번 Host = 에디터) |
| 의심 1(서버가 취소한 공격의 총구 VFX) | 수정 효과 유지 — `muzzleAtMarker:0`(양쪽), `withoutLaunch` Host 3 / Client 2, `launchWithoutMarker:0` |
| 늦은 접속(J-1) | 미발생 — 스냅샷 expected 0 / received 0 |
| 가까운 대상 비행 시간(12-7 항목 4) | 해당 없음 — 거리 2.644 ~ 2.848, 비행 0.558 ~ 0.624초 |
| 싱글 | 세션 없음 |
| 순서 역전 · 타임아웃 · 상한 초과 방출 | 실전 발생 0건. 대체 위치 중 「보관 때 기억한 위치」(14-2 3행 ③)는 **실기 미검증** |

<a id="14-9-errors"></a>

### 14-9. 오류 [전달]

- 예외 0. 「Exception」 문자열은 스택트레이스 서명 문구뿐.
- ERROR 양쪽 각 1건: `[UAS-ATTACK-SHADOW] END verdict=FAIL`(dropped Host 319 / Client 112) — 단계 A·B와 같은 진단 관측기 FAIL이며 게임플레이 오류가 아니다(12-6 · 13-6).
- WARN: Host — UAS-ATTACK-SHADOW INCONCLUSIVE 5, 경기 뒤 정상 퇴장 때 연결 끊김 1. Client — UnitFactory `InitializeUnitView` 재시도 47(정상 재시도 경로), UAS 6, 연결 끊김 · RematchPopup 각 1. **투사체 관련 WARN · fault 0.**

<a id="14-10-remaining"></a>

### 14-10. 남은 것 · 다음 유닛 전에 정할 것 (제안 — 미실행)

> 아래 2~5는 분석의 제안을 옮긴 것이다 [전달]. 코드·진단 추가는 별도 승인 대상이다.

1. **회귀 스위트 결과 [미확인].** 사용자 Console의 `[UAS-DIAG][SERVER-PROJECTILE] self-validation PASS` 여부를 받아야 한다(13-8 「회귀 스위트 재실행」도 여전히 미보고). 로그로 남기려면 별도 승인. *(2026-10-21 단계 C: 사용자 Console 확인 **PASS** — 닫힘, [15-4](#15-4-conclusion). 로그에 남기는 것은 여전히 별도 승인 대상)*
2. **사망타별 표시 방식은 개수로만 확인.** 숫자 결과에 id별 표본 줄이 없어 Client fallback 23 중 7건이 「디스폰된 대상의 남은 HP 숫자」라는 해석은 [추정]이다. 필요하면 사망타 표본 진단 추가(승인 필요).
3. **육안 확인 권장 장면 [추정]:** 사망한 대상 자리에 사망타가 아닌 「40」 같은 숫자가 「0」과 함께 연달아 뜨는 장면이 Client에서 약 7건 있었을 것이다. 각 발의 남은 HP라 설계상 정상(사용자 기준 「그 **뒤에** 도착한 투사체는 숫자 없음」과도 충돌하지 않는다 — 이 7발은 서버에서 사망 **전에** 맞았다)이지만, 보기 좋은지는 사용자 판단 대상이다.
4. **실기에서 아직 안 나온 경로:** 타임아웃 방출(기억한 위치), 순서 역전 즉시 방출, 늦은 접속, 근접 비행, 싱글, Host 화면 played/fallback의 id별 구분, 기기가 Host일 때의 view END 2줄(14-7).
5. **다음 유닛 전에 정할 것:** 남은 대상(인간 타워 · InfernoSpirit · FoxMagician · EagleArcher · MushroomBomber) 중 범위형(Mushroom — 착탄 때 주 타깃 위치 기준 Blast/독, Plan 1-1-3)은 사망타 숫자의 대체 위치가 대상마다 달라야 할 수 있다. 이번 수정은 단일 대상 Tank · CannonCart · StreamSpirit에서만 확인됐다. 진행 전에 범위형의 숫자 위치 규칙을 기획에서 확인하고, 차지 유닛(Inferno · Fox)은 규칙 19 개정(Fox 즉시 표시 예외 삭제, Plan 4-2-3 F-4)과의 관계도 같이 본다.
   - *document-manager 주:* 분석 원문은 「다중 대상 투사체(MushroomBomber는 TargetLocked)」라고 적었다. resolver의 Mushroom 값을 TargetLocked로 바꾸는 것은 Plan U-16 결정(스키마 상향과 같은 커밋)이며, Mushroom의 범위 피해는 여전히 여러 대상에게 들어간다(Plan 1-1-3) — 「다중 대상」은 범위 피해 쪽을 가리키는 것으로 읽었다 [추정].

---

<a id="15-stage-c-human-cannon-tower-check-20261021"></a>

## 15. 단계 C 인간 대포 타워 실기 확인 (2026-10-21)

> 기준 checkout HEAD `b819dc51` · 대상 구현 커밋 `b819dc51` 「feat(combat): 서버 권위 투사체 단계 C — 인간 대포 타워 투사체 전환」, 12개 파일(+978 / −26) [확인 — `git show --stat`].
> 출처: 메인 세션의 읽기 전용 로그 분석 원문(scratchpad `stageC_log_analysis.md`)을 전달받음 [전달]. 로그 수치는 모두 [전달]이며 document-manager는 로그 본문을 다시 분석하지 않았다. **영수증의 해시와 이번 회차에 다시 읽은 코드 행만 [확인]이다.**
> 사용자 상시 승인(로그 분석 결과는 확인 없이 해당 Task에 기록)에 따라 작성했다. 이 기록은 코드·에셋·빌드·규칙 문서 변경을 승인하지 않는다.

<a id="15-1-plain-explanation"></a>

### 15-1. 쉬운 설명

단계 C는 **인간 방어타워(CannonTower)** 를 새 방식으로 바꾼 것이다. 전에는 타워가 쏘는 순간 바로 피해가 들어갔다. 이제는 서버가 대포 입구에서 포탄을 「쐈다」고 기록하고, 포탄이 날아가 닿는 순간 피해를 준다. 정령 타워(RuneSpire)의 번개는 투사체가 아니므로 예전처럼 즉시 피해를 준다.

사용자는 테스트를 마치고 **「육안상 특별한 문제 없음」**, 그리고 **회귀 스위트가 모두 PASS**라고 알렸다(Unity Console에서 확인). 로그로 봐도 인간 타워가 쏜 두 경기 모두 서버·통신·화면 집계가 맞았고, Red 쪽 인간 타워(화면이 뒤집힌 쪽)도 실제로 쏘았다.

새로 보인 것이 하나 있다. 타워 여러 개가 같은 적을 노리면, **이미 다른 포탄에 죽을 적에게 쏜 포탄이 헛방이 된다.** 이것은 사용자가 정한 원칙(쏘는 순간 적이 살아 있으면 쏜다 — Q15)대로 동작한 결과라 고칠 대상으로 올리지 않고 관찰로만 적는다.

또 사용자는 **「인간 방어타워는 앞쪽 120도 안의 적만 공격해야 어색하지 않다」**고 요청했다. 이번에는 문서에 기록만 한다(15-9, Plan 9절).

<a id="15-2-implementation"></a>

### 15-2. 구현 내용 (`b819dc51`) [확인 — 커밋 본문 · 코드 표본]

| # | 위치 | 내용 |
|---|---|---|
| 1 | `ProjectileDeliveryContracts.cs` 257~258 · 276~277 `IsServerProjectileTower` | 「투사체 타워」 분류 `ProjectileTowerRaces = { Human }`과 이행 스위치 `EnabledTowerRaces = { Human }`을 나눴다(유닛 쪽 분류·스위치와 같은 구조). AutoTower + Human만 true. 유닛 스위치 `EnabledUnitTypes`는 242~245행 그대로 Tank·CannonCart·StreamSpirit |
| 2 | 같은 파일 401~406 `ResolveTowerMuzzle` | 포구 = 타워 루트 + `HumanTowerMuzzleOffset`, Red는 x·z 부호를 뒤집는다. 값은 `ProjectileConfig.asset` `_humanTowerMuzzleOffset: {x: 0, y: 0.405, z: 0.17}`(이 커밋에서 추가) · `_humanTowerSpeed: 4` |
| 3 | `TowerCombatUseCase.cs` 162~167 · 250~260 | 인간 타워는 즉발 피해 대신 `TryLaunchTowerProjectile`로 서버 발사 기록을 만든다(공격력은 발사 때 저장). **발사를 못 하면 쿨다운을 시작하지 않는다.** 옛 줄 `ExecuteTowerAttack(building, target);`는 주석으로 보존(RM-10) |
| 4 | 커밋 본문 | 비행 중 타워가 파괴돼도 착탄 피해 · CannonCart Tracer/Impact 프리셋 연결 · 발사 이펙트도 포구 지점 · RuneSpire·VineTower 즉발 유지 · 스위치 토큰 `6,7,17;tower:human` · 진단 `side=server-tower` 줄 · `path tower-Human/Spirit` · 회귀 검증 stage-C fixture |

<a id="15-3-log-archive-receipt"></a>

### 15-3. 로그 보관 영수증 (LogRules §1.10)

| 항목 | 값 | 근거 |
|---|---|---|
| 보관 위치 | private 저장소 `rmsepskek02/Hexiege-Logs`, Release `logs-2026-10` | [전달 — 메인 세션] · manifest `upload_target` [확인 — manifest 사본] |
| 업로드 주체 | 사용자 | manifest `upload: upload performed by user` [확인 — manifest 사본] |
| 만든 도구 | `Assets/Editor/Tools/LogArchiveZip.cs`, schema `hexiege-log-archive-v1`, `task_name` **`server-projectile-stageB3`**, `sequence` 1 | [확인 — manifest 사본]. 이름은 「stageB3」이지만 내용은 단계 C(`b819dc51`) 경기다 — 사용자가 도구 창에 넣은 이름 그대로이며, 분석 대상 경기는 15-5에서 스위치 토큰 `tower:human`으로 확인됐다 [전달] |
| 다운로드 검증 | 메인 세션이 manifest와 대조 OK | [전달 — 메인 세션] |
| document-manager 재계산 | 메인이 내려받은 scratchpad 사본에서 ZIP, ZIP 안 두 항목, 풀어 둔 두 로그 본문의 SHA256을 다시 계산 → 아래 값·manifest 값과 일치 | [확인] |

| ZIP (remote 이름) | ZIP asset id / 크기(B) | ZIP SHA256 | manifest asset id | 시작 날짜 |
|---|---|---|---|---|
| `2026-10-21_03_20_server-projectile-stageB3_01.zip` | 660109339 / 619101 | `edee919b4d28491662bb00afd7121143a863d44acd216ff8938be0cdcd1f55d2` [전달 + 확인] | 660109426 | `start_date` 2026-10-21 |

| 안의 로그 (manifest `sources[]`) | kind | 크기(B) | SHA256 [확인] |
|---|---|---|---|
| 기기 `_Logs/2026-10-21/03_20_logcat/RuntimeLog_device.txt` | `device_capture` | 7966501 | `ff008b561289e2afaa4c68b6e9e32e3f52609990d6187c0df81af020f3eb2877` |
| 에디터 `_Logs/_editor/2026-10-21/RuntimeLog.txt` | `editor_daily`(스냅샷 — 이후 덧붙은 바이트는 없다는 manifest 주석) | 1522659 | `4654129105d622e2a6885852bf3fbac46cd8cda574da80aa7236b74d3708da52` |

- manifest `file_count` 2, `skipped_already_archived` 2, `manifests_scanned` 4, `prior_archive_dedupe: not checked by tool` [확인 — manifest 사본]. 건너뛴 2개가 어느 파일인지는 manifest에 적히지 않는다 [미확인].
- **소스 저장소 쪽 [확인]:** 이 checkout에는 `_Logs/2026-10-21/`이 없다. 로그 본문은 위 Release에만 있다.

<a id="15-4-conclusion"></a>

### 15-4. 결론

| 축 | 판정 | 범위·근거 |
|---|---|---|
| **인간 타워 투사체(서버·전송·화면)** | ✅ **로그상 정상 — scoped** | 인간 타워가 쏜 두 경기(15-5 경기 2 · 4) 모두 `[PROJECTILE-END]` verdict PASS(Host·Client 각 view·transport). 화면 `missing` 0 · `missingDead` 0 · faults 0 · rejected 0 · timeouts 0, 발사 이펙트(`towerCue`) 59/59 · 77/77 [전달] |
| Red 인간 타워(180° 반전) | **실기 관측됨** | 경기 4 — Red 인간 타워 77발, 화면 이상 0. 포구 위치는 간접 근거만(15-6) [전달] |
| RuneSpire 즉발 유지 | **유지됨** | 경기 3 `tower-Spirit:s0/l3` — 즉발 3발, 투사체 0 [전달] |
| 사용자 육안 | 「테스트 완료, 육안상 특별한 문제 없음」(2026-10-21) | 어느 화면(Host/Client)을 봤는지는 전달 없음 [미확인] |
| **회귀 스위트(`Run Full Regression Suite`)** | ✅ **PASS — 사용자 Console 확인**(2026-10-21) [전달 — 메인 세션] | 🔴 **RuntimeLog에는 남지 않는 확인이다.** PASS 문구가 `Debug.Log`라 Console에만 나오고 RuntimeLog의 Unity 로그 훅은 Error·Exception만 받는다(14-4 회귀 스위트 행의 코드 근거). 이번 실행은 `b819dc51` 빌드의 스위트라 단계 B·B-2·C 검증을 함께 포함한다 [추정 — 스위트가 누적 실행된다는 전제]. 이 결과로 13-3 · 13-8 · 14-4 · 14-10의 「미보고」를 갱신했다(각 자리의 날짜 조각) |
| 유닛 투사체(6·7·17) 회귀 | **로그로는 확인 불가** [미확인] | 이번 경기에서 Tank·CannonCart·StreamSpirit이 생산되지 않아 한 발도 쏘지 않았다(유닛 `path=none`). 근거는 회귀 스위트 PASS뿐 |
| 오류 | 예외 0, ERROR는 이동 관측기 FAIL 2건뿐 | 15-8 |

<a id="15-5-match-and-figures"></a>

### 15-5. 경기와 수치 [전달]

| # | 시각 (epoch) | Host | Client | 타워 | 비고 |
|---|---|---|---|---|---|
| 1 | 03:15:41~43 (`4b1103db…`) | 에디터 Blue | Red (기기 로그에 없음) | — | 2초 만에 포기, 발사 0 |
| 2 | 03:16:05~03:17:52 (`4181b24d…`) | 에디터 Blue 인간 | 기기 Red 정령 | 인간 타워 8기(Blue), RuneSpire 2기(Red) | 인간 타워 59발. RuneSpire 2기는 한 번도 쏘지 않음 |
| 3 | 03:17:55~03:18:20 재경기 (`4c0df7a5…`) | 같음 | 같음 | RuneSpire 3기 | RuneSpire 즉발 3발 |
| 4 | 03:18:33~03:19:47 (`4d5f7cc4…`) | 기기 Blue 정령 | 에디터 Red 인간 | 인간 타워 10기(Red) | 인간 타워 77발 |

- 네 경기 모두 스위치 토큰 `switch=6,7,17;tower:human`. 역할이 서로 바뀐 두 쌍(에디터 Host · 기기 Host)이 들어 있다. **싱글 경기는 없다**(기기의 `Role=Solo` 줄 6개는 경기 3 Shutdown 직후 줄 — 15-8).

| 항목 | 경기 2 (Blue 타워) | 경기 4 (Red 타워) |
|---|---|---|
| launched / hit / targetLost | 59 / 58 / 0 | 77 / 57 / **19** |
| closedInFlight (경기 종료) | 1 | 1 |
| 수지 | 59 = 58 + 0 + 1 | 77 = 57 + 19 + 1 |
| 발사 거절(`reserveRefused`) | 0 | 0 |
| 비행 시간 최소 / 최대 / 평균(초) | 0.061 / 0.839 / 0.389 | 0.076 / 0.844 / 0.550 |
| 거리 최소 / 최대 / 평균 | 0.379 / 3.492 / 1.691 | 0.439 / 3.512 / 2.336 |
| 서버 착탄 지연 최대(`impactLagMax`) | 0.022 | 0.016 |
| 피해량 / 사망타 | 150 / 26 | 150 / 28 |

| 화면 대조 | 경기 2 Host(에디터) | 경기 2 Client(기기) | 경기 4 Host(기기) | 경기 4 Client(에디터) |
|---|---|---|---|---|
| launch recv / accepted | 59 / 59 | 59 / 59 | 77 / 77 | 77 / 77 |
| result recv / accepted | 58 / 58 | 58 / 58 | 76 / 76 | 76 / 76 |
| finish hit / lost | 58 / 0 | 58 / 0 | 57 / 19 | 57 / 19 |
| impactVfx played / missing | 58 / 0 | 58 / 0 | 76 / 0 | 76 / 0 |
| towerCue played / skippedMidFlight / noPreset | 59 / 0 / 0 | 59 / 0 / 0 | 77 / 0 / 0 | 77 / 0 / 0 |
| text played / fallback / missing / missingDead | 58 / 0 / 0 / 0 | 31 / 27 / 0 / 0 | 57 / 0 / 0 / 0 | 24 / 33 / 0 / 0 |
| displayLag 최소 ~ 최대(초) | 0 ~ 0.022 | 0.100 ~ 0.117 | 0 ~ 0.016 | 0.100 ~ **0.333** |
| verdict | PASS | PASS | PASS | PASS |

- **비행 시간 공식:** 상세 로그가 남은 97발 모두 `비행 시간 = (거리 − 0.135) ÷ 4`와 0.002초 이내로 맞는다. 속도 4는 `ProjectileConfig.asset` `_humanTowerSpeed` [확인].
- **근접 비행:** 0.1초 미만 비행 13발(경기 2: 9, 경기 4: 4), 최소 0.061초 — 12-7 항목 4의 「가까운 대상」 경로가 이번에 실기로 나왔다.
- **Client fallback 구성 [추정 — 개수만 맞춤]:** 경기 2 Client 27 = 사망타 26 + 사망 0.085초 전 비사망 명중 1. 경기 4 Client 33 ≈ 사망타 28 + 화면 표시 지연 안에 대상이 죽은 비사망 명중 약 5(사망 0.2초 이내 후보 7건). 비사망 명중은 죽은 대상 자리에 남은 HP 「150」이 뜬다(14-10 항목 3과 같은 현상).
- 대상 소실(targetLost) 발도 착탄 VFX를 재생했다(화면 표본 각 24줄 모두 `impactVfx=1`, `waitedAtEndpoint=0`).

<a id="15-6-muzzle-origin"></a>

### 15-6. 포구 원점 — 직접 확인 불가, 간접 근거만 [전달 · 추정]

- 서버 `[PROJECTILE]` 줄과 `[PROJECTILE-VIEW]` 줄 모두 **발사 위치(Source) 좌표를 찍지 않는다** → 직접 확인 불가 [미확인].
- 간접 근거 [추정]: 타워 사거리는 루트 기준 4 × 0.866 + 0.05 = 3.514. 첫 발 거리 3.302(경기 2) · 3.306(경기 4) — 앞에서 다가온 적이 사거리에 막 들어온 순간이라면 포구가 적 쪽 0.17이면 약 3.3, 반대쪽이면 3.6 이상이어야 한다. 전체 최대 거리 3.512도 3.514를 넘지 않았다 → Blue·Red 모두 포구가 적 쪽에 있다는 해석과 맞는다.
- 직접 확인하려면 진단 줄에 source 칸을 추가해야 한다(제안 — 승인 필요, 15-10).

<a id="15-7-overlap-fire"></a>

### 15-7. 타워 겹사격 헛방 — 사용자 결정 Q15와 일치하는 결과 (관찰만)

- **사실 [전달]:** 경기 4의 targetLost 19발(77발 중 24.7%) 가운데 상세 로그가 있는 15발은 모두 같은 패턴이다 — 다른 타워가 이미 날린 사망타가 닿기 **전에** 이 발이 발사됐다(`cause=target-gone-in-flight`). 예: id 2·3이 같은 시각(25.678)에 대상 101(HP 150)을 향해 발사, id 4는 25.879에 발사. id 2가 26.474에 101을 죽였고 id 3·4는 lost.
- **왜 전에는 없었나 [전달 — 코드 근거는 분석 원문]:** 즉발 시절에는 앞 타워가 같은 Tick 안에서 피해를 즉시 줬으므로 다음 타워는 죽은 대상을 건너뛰고(`!unit.IsAlive` — `TowerCombatUseCase.cs` 215행 [확인]) 다른 대상을 골랐다. 지금은 헛방 발도 쿨다운(5초)을 소모한다.
- **사용자 결정과의 관계 [확인 — Plan 1-1 Q15 행]:** 사용자 결정 Q15는 「발사 시점에 타겟이 **살아 있으면 발사**. 죽을 것을 예상해 공격하지 않는 것은 잘못」이다. 이번 헛방은 그 원칙대로 동작한 결과다. 그래서 **결함·후속 항목으로 올리지 않는다.**
- **관찰 [전달]:** 타워가 모여 있으면 인간 타워의 실효 화력이 즉발 시절보다 줄었다. 밸런스 판단은 하지 않았다. 분석 원문의 「예약 피해 계산 도입 여부」 제안은 Q15 결정(죽을 것을 예상해 안 쏘는 것은 잘못)과 반대 방향이라 이 문서에서는 제안으로 옮기지 않는다 — 다시 논의하려면 사용자가 Q15를 다시 열어야 한다.

<a id="15-8-errors"></a>

### 15-8. 오류 [전달]

- **실제 예외 0.** 「Exception」 문자열은 스택트레이스 서명 문구뿐.
- **ERROR 에디터 2건:** 모두 `[UAS-MOVE-AUTH] END verdict=FAIL`(경기 1, 경기 3 Host) — Host 표본 조건(`UnitMovementAuthorityObserver.cs` 1134~1135행)에 짧은 경기가 걸린 진단 범위 실패이며 투사체·게임플레이 오류가 아니다. `UAS-ATTACK-SHADOW`는 새 세션 FAIL 0 · dropped 0(EVIDENCE 4 · INCONCLUSIVE 2).
- **[관찰 — 기존 경로, 단계 C 범위 밖]** 경기 3이 끝나고 기기(Client)가 Shutdown한 직후(03:18:22.196) 같은 기기에서 RuneSpire 3기가 로컬로 공격을 1회 실행했다(`Role=Solo`, `ClockDomain=SoloScaled`, HP 200→0). `GameBootstrapper.cs` 722~725행이 `!IsNetworkMode()`일 때 타워 Tick을 부르므로 씬이 내려가기 전 몇 프레임 동안 싱글처럼 동작한 것으로 읽힌다 [전달]. 인간 타워가 이 구간에서 어떻게 되는지는 나오지 않았다 [미확인].
- **WARN:** `InitializeUnitView` 재시도(정상 재시도 경로) 기기 155 · 에디터 202, 연결 끊김, `RematchRequestPopup`, WAVE-DIAG, ROOT-POSE capacity. **투사체 관련 WARN · fault 0.**

<a id="15-9-front-120-request"></a>

### 15-9. 사용자 요청 — 인간 방어타워 전방 120° 공격 (기록만, 미착수)

> **사용자 요청(2026-10-21) [전달 — 메인 세션]:** 「인간 방어타워는 전방 120도 영역만 공격 가능하도록 바꿔야 어색하지 않을 것 — 문서에 기록만」. 구현·규칙 개정은 하지 않는다. 후속 항목은 [Plan 9절](Plan.md#9-follow-up-items-not-started) · ROADMAP 「전투·유닛 후속」.

현재 코드 사실(분석 원문 §8 — 표본 행은 이번 회차에 다시 읽음):

1. **대상 선택은 360°다** [확인]. `TowerCombatUseCase.FindNearestEnemyUnit`(190~235행)은 사거리(199행 `BuildingStats.GetAttackRange × TileHeight + Epsilon`)와 최단 거리(224행 `Vector3.Distance`)만 본다. 기준 위치는 `GetBuildingWorldPosition`, 없으면 `HexToWorld`(203~207행). 방향·각도 검사는 없다.
2. **세 종족이 같은 탐색을 쓴다** [확인]. `Tick`(142~174행)은 종족 구분 없이 모든 AutoTower를 같은 탐색으로 처리하고, 종족 분기는 탐색 뒤 `ExecuteTowerAttack`(254행 `IsServerProjectileTower`)에서만 일어난다.
3. **대상이 없으면 쿨다운을 시작하지 않고 다음 Tick에 다시 찾는다** [확인 — 159~160행. 분석 원문은 156~160행]. 그래서 각도 조건을 넣으면 「범위 밖이면 기다림」이 자연스럽게 된다 [추정].
4. **포신 방향은 배치 때 고정, 실시간 회전 없음** [확인]. `BuildingFactory.GetInitialRotation`(231~247행): Human + AutoTower만, 내 타워는 회전 없음, 상대 타워는 Y 180°. `ViewConverter.ToView`의 맵 중심 점대칭과 합쳐 도메인 좌표에서 **Blue 포신은 +Z, Red 포신은 −Z**(`ProjectileDeliveryContracts.cs` 391~397행 주석). 타워를 돌리는 LookAt 코드는 없다(검색에 걸린 것은 RuneSpire 볼트 VFX뿐 [전달]).
5. **어색함의 코드상 원인** [전달]: 투사체는 포구에서 대상 중심으로 곧바로 날아가고(`TryLaunchTowerProjectile`), 발사 이펙트 방향도 포구 → 대상이다. 그래서 대상이 타워 뒤에 있으면 포신 끝에서 **뒤로** 날아가는 모양이 된다.
6. **현재 120° 밖 발사가 몇 발인지는 측정할 수 없다** [미확인]. 로그에 대상 위치와 Source가 없다. 최소 거리 0.379는 타워 바로 옆 유닛에게도 쏜다는 것만 보여 준다.
7. **참고 수치(사실):** 120°는 정면에서 좌우 ±60°이며, XZ 정규화 방향과 정면 벡터(Blue (0,0,1) · Red (0,0,−1))의 내적 ≥ 0.5와 같다. 각도 기준점을 타워 루트로 할지 포구로 할지는 **정해지지 않았다.**

**바꿀 때 관련되는 규칙 위치** — `GameSystemRules_Buildings.md` 「방어 타워 시스템」 [확인 — 위치만, 본문 수정 안 함]:

| 규칙 | 행 | 지금 문장의 요지 | 120°와의 관계 |
|---|---|---|---|
| 규칙 1 「타겟 대상」 | 108~110 | 적 유닛만 타겟 | 「적 유닛」에 각도 조건이 붙는 자리 |
| 규칙 2 「타겟 선택 기준」 | 112~113 | 사거리 안 여러 명이면 월드 거리 최단 | 「사거리 안」 → 「사거리 안 + 전방 120° 안」으로 범위가 바뀐다 |
| 규칙 3 「사거리 판정 기준」 | 119~122 | 월드 좌표 `Vector3.Distance` | 각도 판정 기준점(루트·포구)과 높이(Y 포함 여부)를 함께 정해야 한다 |
| 규칙 11 「Human CannonTower 배치 시 초기 회전」 | 180~187 | 배치 때 한 번 고정, 실시간 회전 없음 | 「정면」의 정의가 이 규칙의 초기 회전에서 나온다. 회전을 넣을지(규칙 11 변경)와 사격 각도 제한(규칙 1·2·3 변경)은 다른 선택이다 |

- 🔴 위 규칙은 세 종족 공통 문장이다(규칙 11만 Human 전용). 인간 타워에만 각도 제한을 두려면 종족 예외 문장이 필요하다 — **규칙 변경 전 사용자 보고·승인 대상**(공용 메모리 「Permanent user workflow preference」).

<a id="15-10-remaining"></a>

### 15-10. 남은 것 · 다음 유닛 전에 정할 것 (제안 — 미실행)

> 2~5는 분석의 제안을 옮긴 것이다 [전달]. 코드·진단 추가는 별도 승인 대상이다.

1. **유닛 투사체 6·7·17 실기 회귀** — 이번 로그로 확인 불가. 다음 실기에서 유닛 투사체와 인간 타워를 섞어 확인 [제안].
2. **포구 Source 직접 확인** — 진단 줄에 source 칸 추가(승인 필요). 지금은 간접 근거(15-6)와 육안뿐.
3. **실기에서 아직 안 나온 경로:** 비행 중 타워 파괴(Plan U-6 결정 「피해 들어간다」의 실기), 늦은 접속(스냅샷 expected 0 / received 0), 싱글, 타임아웃 · 순서 역전 방출(이번에도 모두 resultBeforeArrival), 경기 4 Client displayLag 0.333 1건 · 0.2~0.3 1건의 원인(표본 24줄 밖이라 [미확인]).
4. **InfernoSpirit · FoxMagician(차지 유닛) 시작 전에 정할 것:** 차지 marker부터 발사까지 끊기는 구간과 그 취소 규칙(Plan 1-2 확정표와의 대조), 차지 중 숫자 표시 규칙(Units 규칙 19 — Plan 4-2-3 F-4로 Fox 예외 삭제)과의 관계. *(2026-10-26: 단계 D `a0a23423`으로 구현됐다 — 차지 중 끊김은 서버 발사 취소 + 취소 통보, 사거리 이탈은 원래 대상 발사, Fox 예외는 RM-5로 비활성화. 실기 결과는 [16절](#16-stage-d-charge-unit-check-20261026))*
   - *document-manager 주:* 분석 원문은 여기에 「겹사격 정책을 유닛에도 적용할지」도 넣었다. 겹사격 헛방은 Q15 결정의 결과이므로(15-7) 정책 질문으로 옮기지 않았다. 유닛에서도 같은 헛방이 생기는 것은 Q15상 정상 동작이다 [추정 — Q15 문장이 유닛·타워를 나누지 않으므로].
5. **인간 타워 전방 120°** — 15-9, Plan 9절(기록만, 미착수).

---

<a id="16-stage-d-charge-unit-check-20261026"></a>

## 16. 단계 D 차지 유닛 실기 확인 (2026-10-26)

> 기준 checkout HEAD `e340f783`(main 병합 — 부모 `a0a23423` · `7c66e4c5`) · 대상 구현 커밋 `a0a23423` 「feat(combat): 서버 권위 투사체 단계 D — 차지 유닛 InfernoSpirit·FoxMagician 활성화」, 14개 파일(+1099 / −63) [확인 — `git show --stat`]. 병합은 이 Task 문서를 바꾸지 않았다 [확인 — `git diff --stat a0a23423 e340f783 --` 이 폴더 0건].
> 출처: 메인 세션의 읽기 전용 로그 분석 원문(scratchpad `stageD_log_analysis.md`)을 전달받음 [전달]. 로그 수치는 모두 [전달]이며 document-manager는 로그 본문을 다시 분석하지 않았다. **영수증의 해시와 이번 회차에 다시 읽은 코드 행만 [확인]이다.**
> 사용자 상시 승인(로그 분석 결과는 확인 없이 해당 Task에 기록)에 따라 작성했다. 이 기록은 코드·에셋·빌드·규칙 문서 변경을 승인하지 않는다.

<a id="16-1-plain-explanation"></a>

### 16-1. 쉬운 설명

단계 D는 **기를 모은 뒤 쏘는 두 유닛 — 지옥불 정령(InfernoSpirit)과 여우 마법사(FoxMagician)** 를 새 방식으로 바꾼 것이다. 이제 서버가 공격 동작의 발사 순간에 투사체를 「쐈다」고 기록하고, 닿는 순간에 피해를 준다. 기를 모으는 도중 대상이나 공격자가 죽거나 이동 명령을 받으면 쏘지 않고 취소하며, 화면의 기 모으기 이펙트도 바로 꺼진다.

사용자는 테스트 뒤 이렇게 알렸다. **「에디터가 Host일 때는 육안상 문제없어 보이는데, Client(기기)에서는 투사체가 맞기 전에 대미지가 적용되는 느낌이 강하다 — Inferno 투사체가 Fox에게 닿기 전에 Fox에게 대미지가 적용됐다.」**

로그로 보면 서버 판정은 정상이다. 피해는 서버 착탄 순간에 들어갔고, 피해 숫자도 화면 착탄까지 기다렸다가 나왔다. 사용자가 본 「대미지」는 숫자가 아니라 **사망 표현**이다. Inferno 한 발은 Fox에게 언제나 치명타라서, Client 화면에서는 Fox가 쓰러지고 사망 이펙트가 터지고 사라지는 장면이 투사체가 닿기 0.04~0.17초(평균 0.13초) **전에** 나왔다. Client는 투사체 그림만 일부러 0.10초 늦게 그리는데, 사망 소식은 늦추지 않고 바로 보여 주기 때문이다. 게다가 Client의 서버 시계가 이미 조금 뒤처져 있어, 그 0.10초가 고스란히 「앞섬」으로 더해진다.

고치는 방법 세 가지(16-9)를 정리했다. **어느 방법으로 고칠지는 사용자가 정한다.** 정하기 전에는 코드를 바꾸지 않는다. 별개로 기기에서 파티클 오류 문구가 12,007번 찍혔다(16-8).

<a id="16-2-implementation"></a>

### 16-2. 구현 내용 (`a0a23423`) [확인 — 커밋 본문 · 코드 표본]

| # | 위치 | 내용 |
|---|---|---|
| 1 | `ProjectileDeliveryContracts.cs` 329~333 `EnabledUnitTypes` | 유닛 스위치에 **InfernoSpirit · FoxMagician 추가** → Tank · CannonCart · StreamSpirit · InfernoSpirit · FoxMagician [확인]. 커밋 본문: 스위치 토큰 `6,7,12,17,21;tower:human`, 스키마 5 → 6(발사 전 취소 통보 RPC) |
| 2 | 커밋 본문 | `OnAttackHit`에서 서버 발사(Inferno 1.5초, Fox 85/30초) → 착탄 때 피해, Inferno 화상은 착탄 때 시작 |
| 3 | 커밋 본문 | 차지 중 대상·공격자 사망, 이동 명령 → 서버 발사 취소 + 취소 통보로 차지 VFX 즉시 정지(Client는 공격 회차가 맞을 때만 정지). 사거리 이탈은 원래 대상에게 발사(Plan 1-2 확정표) |
| 4 | `UnitCombatUseCase.cs` 1808행 주석 | **RM-5 실행** — Fox 규칙 19 즉시 표시 분기 비활성화(주석 보존) → 숫자는 착탄 때 [확인 — 주석 「[RM-5 비활성화 — … 단계 D, 사용자 승인 F-4(규칙 19 Fox 예외 삭제)]」]. 같은 파일 1817행에 RM-5 확인 진단 계수 |
| 5 | 커밋 본문 · `RunUnitActionSelfValidation.ServerProjectiles.cs` 156 · 193 | 진단 `side=server-charge` · transport cancel 계수 · view `chargeCancel`, 회귀 검증 D-1~D-7(`ValidateServerProjectileStageDSourceGates` · `ValidateServerProjectileChargeUnits`), 옛 Fox 검사 V-4~V-7 주석 보존 |

<a id="16-3-log-archive-receipt"></a>

### 16-3. 로그 보관 영수증 (LogRules §1.10)

| 항목 | 값 | 근거 |
|---|---|---|
| 보관 위치 | private 저장소 `rmsepskek02/Hexiege-Logs`, Release `logs-2026-10` | [전달 — 메인 세션] |
| 업로드 주체 | 사용자 | [전달 — 메인 세션] |
| 🔴 **manifest** | **두 ZIP 모두 manifest가 업로드되지 않았다** | [전달 — 메인 세션]. 그래서 **ZIP SHA256만 Release digest와 대조했고(OK), ZIP 안 파일별 대조는 할 수 없었다.** 아래 「안의 로그」 해시는 document-manager가 ZIP을 풀어 계산한 값이며 대조할 manifest 값이 없다 |
| document-manager 재계산 | 메인이 내려받은 scratchpad 사본에서 두 ZIP과 풀어 둔 세 로그 본문의 SHA256을 다시 계산 → ZIP 값은 아래(메인 전달 값)와 일치 | [확인] |
| LogRules 대비 | manifest 미업로드는 이번이 **2회째**다. 메인 세션이 사용자에게 「다음부터 ZIP과 manifest를 함께 업로드」를 안내했다 | [전달 — 메인 세션] |

| ZIP (remote 이름) | ZIP asset id / 크기(B) | ZIP SHA256 |
|---|---|---|
| `2026-10-26_13_00_server-projectile-charge_01.zip` | 676948296 / 2855737 | `31e95bec1aa352e495d36835f994c80a40fc59d034888fc65bfcf665b66f6548` [전달 + 확인] |
| `2026-10-26_13_03_server-projectile-charge-DEVICE_01.zip` | 676955456 / 930964 | `a9767efe25607bb72832cfb345a78b3238103eb2d6cfee90139bfdbf28764a2f` [전달 + 확인] |

| 안의 로그 | 들어 있는 ZIP | 크기(B) | SHA256 [확인 — 풀어서 계산, manifest 대조 없음] | 분석 대상 |
|---|---|---|---|---|
| 에디터 `_Logs/_editor/2026-10-26/RuntimeLog.txt` | charge_01 | 11890772 | `2f33289baef7329ab4c33bc5f0519c9518861c6fc56b403e537aeb2eecc35120` | ✅ 대상 세션 12:52:11~ [전달] |
| 기기 `_Logs/2026-10-26/03_09_logcat/RuntimeLog_device.txt` | charge_01 | 30899436 | `08ff22e46c9e35b4ab26346e625d89117d7d669d352bb918d03443ef49d3475a` | ❌ **이전 빌드 로그 — 대상 아님.** 사용자가 이번 logcat을 저장하기 전에 ZIP을 만들었다 [전달] |
| 기기 `_Logs/2026-10-26/13_03_logcat/RuntimeLog_device.txt` | charge-DEVICE_01 | 15117754 | `6dc348f27fb2b8ff9b11262399a894eb5fbc6b6c77f3439150099486ab40828e` | ✅ 대상 [전달] |

- **소스 저장소 쪽 [확인]:** 이 checkout에는 `_Logs/2026-10-26/`이 없다. 로그 본문은 위 Release에만 있다.

<a id="16-4-conclusion"></a>

### 16-4. 결론

| 축 | 판정 | 범위·근거 |
|---|---|---|
| **서버 판정(발사·착탄 피해·차지 취소)** | ✅ **로그상 정상 — scoped** | 모든 END 줄 verdict PASS. 예약 = 발사 + 취소 + 닫힘 수지 일치(16-7) [전달] |
| 차지 취소 통보 전송 | ✅ 정상 | sent = recv = delivered, dropped 0(16-7) [전달] |
| **Fox 즉시 표시 0(RM-5)** | ✅ 확인 | `foxPrimary` projectileKeyed 126 · immediate 0 [전달]. 차지 Task 한계 **L-1(Fox 숫자 선행)** 은 숫자에 대해서는 이번 로그로 재현되지 않았다 |
| 피해 숫자 hold | ✅ 정상 | hold held = atImpact, immediate 0 · timeout 0, text missing 0 · missingDead 0(16-5). 치명타가 아닌 명중에서 숫자·피격 VFX·펀치는 앞서지 않는다 [전달] |
| 🔴 **Client 사망 표현** | **화면 착탄보다 앞섬 — 사용자 보고와 일치** | 사망 RPC가 화면 착탄보다 0.043~0.168초(평균 0.129) 먼저, 표본 27건 전부 앞섬(16-6) [전달]. Host(에디터)는 displayLag 0~0.009초라 같은 프레임 — 「Host에서는 문제없어 보인다」와 일치 |
| 🔴 Client 화상(Inferno DoT) VFX | **화면 착탄보다 앞섬** | 표본 0.103 · 0.068초 앞섬. 다만 이번 경기의 화상은 BearGuard에만 걸렸다 — 사용자가 본 Fox 장면의 원인은 아니다(16-6) [전달] |
| HP 막대 | **해당 없음 — 게임에 없음** | 분석이 Presentation·프리팹·씬에서 HpBar류를 찾지 못했다 [전달]. HP는 숫자로만 보인다 |
| 사용자 육안 | Host 「문제없어 보임」 / Client 「맞기 전에 대미지가 적용되는 느낌이 강함」(2026-10-26) | [전달 — 메인 세션] |
| **회귀 스위트** | **미보고** | 이번 회차에 결과가 전달되지 않았다. PASS로 적지 않는다(RuntimeLog에 남지 않는 확인이라 로그로도 알 수 없다 — 14-4 근거) |
| 수정 방식 | 🔴 **사용자 결정 대기** — (a) · (b) · (c) | 16-9, Plan 1-3 U-18 |
| 별건 오류 | 기기 파티클 오류 12,007건 | 16-8, Plan 1-3 U-19 |

<a id="16-5-match-and-figures"></a>

### 16-5. 경기와 수치 [전달]

| # | 시각 (epoch) | Host | Client | 종족 | 종료 |
|---|---|---|---|---|---|
| 1 | 12:54:16~12:56:59 (`4241eb81…`) | 에디터 Blue | 기기 Red | Blue 초월, Red 정령 | Host 포기 |
| 2 | 12:57:04~12:59:25 재경기 (`498cdd42…`) | 같음 | 같음 | 같음 | Host 포기, 12:59:29 Client 이탈 |

- 경기 1 유닛: Red InfernoSpirit 10 · Blue FoxMagician 79. 경기 2: Red Inferno 8 · Blue Fox 21 · BearGuard 3.
- Inferno는 기기 소유, Fox는 에디터 소유. 사용자가 본 장면은 기기(Client) 화면에서 「내 Inferno가 상대 Fox를 쏜」 경우다.
- 두 경기 모두 에디터 = Host다. **기기가 Host인 경기와 싱글은 없다.** Tank · CannonCart · StreamSpirit · 인간 타워는 나오지 않았다 → 이들의 로그 회귀 확인은 없다.

**Client `[PROJECTILE-END]` view 집계**

| 항목 | 경기 1 | 경기 2 |
|---|---|---|
| displayLag (화면 착탄 − 서버 착탄, 초) | 0.100~0.133, 평균 0.117 | 0.100~0.133, 평균 0.116 |
| resultLag (결과 수신 − 착탄, Client 시계, 초) | −0.089~+0.050, 평균 −0.048 (음수 199/205) | −0.079~−0.010, 평균 −0.049 (음수 81/81) |
| hold held / atImpact / immediate / timeout | 173 / 173 / 0 / 0 | 66 / 66 / 0 / 0 |
| text played / fallback / missing / missingDead | 105 / 68 / 0 / 0 | 39 / 27 / 0 / 0 |
| order resultBeforeArrival | 205 / 205 | 81 / 81 |

- Client의 투사체 번호표가 붙은 Fox 피격 64건이 전부 `AppliedDamage=200, Hp=0`(서버 공격력 250) — Inferno 한 발은 Fox에게 언제나 치명타다. 치명타 뒤 빈 자리에 「0」이 대체 위치로 뜬다(text fallback 68 / 27).

<a id="16-6-death-presentation-ahead"></a>

### 16-6. Client 사망 표현이 앞서는 원인 [전달 + 코드 표본 확인]

**투사체별 대조** — 같은 기기 시계로 잰 값이라 시계 정렬이 필요 없다 [전달]:

| 측정 | n | 범위(초) | 평균 | 앞선 비율 |
|---|---|---|---|---|
| 화면 finish − HP 동기화 수신 | 41 | 0.046~0.176 | 0.137 | 100% |
| 화면 finish − 사망 RPC (Fox 치명타) | 27 | 0.043~0.168 | 0.129(중앙값 0.135) | 100%(0.1초 이상 23건) |
| 사망 RPC − HP 동기화 | 77(번호표 붙은 치명타 전부) | 0~0.003 | — | 같은 시점 |

- 비행 시간이 0.47~0.67초이므로, 투사체가 경로의 약 75~80% 지점에 있을 때 Fox가 사라지는 셈이다 [전달].
- 🔴 **표본 한계:** 화면 finish 줄은 맵당 처음 24발만 남는다. 투사체별 대조는 표본 기준이고, 전체 발수는 16-5 집계로만 본다.
- 극단 예(`498cdd42…:8`, 비행 0.026초): HP 동기화 07.863 · 화상 Receive 07.898이 화면 begin 08.000보다도 먼저 왔고 finish는 08.002 [전달].

**왜 앞서는가:**

1. **Client는 투사체 그림만 늦춘다** [확인]. `ProjectilePresentationPresenter.cs` 246행 `presentationTime = serverNow - (pureClient ? ClientPresentationDelaySeconds : 0d)` — 순수 Client만 지연, Host는 0. 상수는 59~60행에서 `AuthoritativeAttackPresentationCoordinator.cs` 67행 `PresentationDelaySeconds = 0.10d`를 참조한다(C3 · 버섯 · 파도와 공유).
2. **Client의 서버 시계가 이미 뒤처져 있다** [시계 출처 확인 · 수치 전달]. 시계는 `GameBootstrapper.Setup.cs` 367~372행 `network.ServerTime.Time`(NGO). 결과를 받는 순간 Client 시계는 착탄 시각보다 평균 0.048초 앞을 가리킨다(resultLag 음수 280/286). 그래서 0.10초 지연이 도착 지연을 흡수하지 못하고 **고스란히 앞섬으로 더해진다.** 「회선 지연이 0.1초 넘으면 오히려 HP가 늦다」는 처음 가설은 실측으로 성립하지 않았다. NGO가 ServerTime을 내부에서 어떻게 계산하는지는 소스로 확인하지 않았다 [미확인 — 실측만 근거].
3. **사망 표현에는 지연 장치가 없다** [확인]. 서버가 `NetworkCombatController.cs` 2905행에서 `netObj.Despawn(destroy: true)`. Client는 `HandleUnitDied`(3362행~)가 도메인에서 유닛을 지우고 `GameEvents.OnUnitDied`를 바로 재발행 → `UnitView.cs` 2027행 차지 VFX 정지 · 2033행 `AnimIsDead` 켬(분석 원문 2028 · 2034행) → `NetworkUnit.cs` `OnNetworkDespawn`(1099행~)에서 `PlayUnitDeath`(1154행 부근) VFX·SFX와 오브젝트 파괴.
4. **피해 숫자는 기다린다** [확인]. `HitPresentationQueue.cs` 563~576행이 번호표 붙은 피격을 보관하고, 화면 finish → `ReleaseProjectileImpact`(195행)에서 한 번 방출한다. 사망 처리(757행 `OnUnitDied`)는 투사체 보관함을 비우지 않는다는 것이 분석 판단이며 timeout 0 · immediate 0이 근거다 [전달].
5. **화상 VFX도 기다리지 않는다** [경로 확인 · 수치 전달]. NetworkVariable 수신 → `NetworkUnit.cs` 561~570행 → `TimedDamageEffectPresentation.cs` 73~88행 `Receive` → `EffectManager.cs` 329행 `PlayTimedDamage`. 지연 장치가 없다. Fox는 직접 피해로 죽어 `InfernoAttackBehavior.cs` 65행(`!victim.IsAlive` → DoT 건너뜀)으로 화상이 걸리지 않는다 [확인] — 그래서 사용자가 본 Fox 장면의 원인은 화상이 아니다. DoT 틱 숫자는 hold를 거치지 않지만 첫 틱이 착탄 약 0.9~1초 뒤라 착탄 전에 보이는 경로가 아니다(`UnitCombatUseCase.cs` 2911 · 2971행 — 분석 원문 인용, 다시 읽지 않음) [전달].

- **규칙과의 관계 [확인 — 원문 읽음, 판정 아님]:** `GameSystemRules_UnitCombatSynchronization.md` 「5. 서버 시간 재생」 `NET-TIME-002`는 「표시용 HP바·텍스트·피격 반응만 권위 Result와 같은 표현 Impact에 맞춘다」고 적고, **사망 표현(쓰러짐 · 사망 VFX·SFX · 소멸)과 화상 VFX는 그 목록에 없다.** 또 「HP바」가 적혀 있지만 이 게임에는 HP 막대가 없다(위 결론). 같은 규칙의 「호스트도 같은 표현 지연 정책을 사용」과 지금 코드의 Host 지연 0(1번 항목)은 단계 A부터의 기존 차이이며 이번 판정 대상이 아니다.

<a id="16-7-charge-cancel-balance"></a>

### 16-7. 차지 취소와 수지 [전달]

| 항목 | 경기 1 | 경기 2 |
|---|---|---|
| cancelled | 205 (Inferno 5+11, Fox 23+166 — 합 맞음) | 59 (Inferno 54, Fox 5 — 합 맞음) |
| reserved = launched + cancelled + closedReserved | 421 = 205 + 205 + 11 | 146 = 83 + 59 + 4 |
| 취소 통보 전송 | sent 205 = recv · delivered 205, dropped 0 | sent 59 = recv · delivered 59, dropped 0 |
| Client chargeCancel stopped + nothingActive + noView | 128 + 49 + 28 = 205 | 32 + 22 + 5 = 59 |
| Host chargeCancel | 129 + 48 + 28 | 29 + 25 + 5 |
| suppressedAfterCancel | Host 49 · Client 0 | Host 51 · Client 0 |
| verdict | 모든 END 줄 PASS | 모든 END 줄 PASS |

- `suppressedAfterCancel` Client 0은 Host·싱글 전용 게이트라 설계상 정상이라는 것이 분석 판단이다 [전달].
- 차지 Task 한계 **L-2(차지 중 취소 뒤 숫자만 표시)**: 취소된 회차는 서버 발사·피해가 없으므로(위 수지) 숫자가 생길 원인이 없다는 것까지가 로그 근거다. 「취소 뒤 숫자만 뜨는 장면」을 직접 세는 항목은 로그에 없다 [미확인].

<a id="16-8-errors"></a>

### 16-8. 오류 [전달]

- 🔴 **기기 Unity 엔진 오류 「Particle Velocity curves must all be in the same mode」 12,007건 — 이번 타이밍 문제와 별건.**
  - 첫 발생 12:55:08.744 — 첫 Inferno 화면 착탄 14ms 뒤. 이전 기기 로그 4개(b2 · c · d · e/03_09)에는 0건.
  - 정적 검사 후보 둘: `Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_impact.prefab` · `vfx_foxmagician_impact.prefab`의 활성 VelocityModule이 x=0 · y=3 · z=0으로 축마다 모드가 섞여 있다(분석 스크립트 `velmode.py` 결과 — document-manager는 다시 검사하지 않았다). 두 프리팹 파일이 이 checkout에 있는 것만 확인했다 [확인].
  - 어느 프리팹이 원인인지는 로그로 확정하지 못했다 [미확인]. 기기에서 착탄 VFX 모양이 달라질 수 있다 [추정]. 처리 여부는 Plan 1-3 U-19.
- **에디터 `[UAS-MOVE-AUTH] END verdict=FAIL`** (12:59:30, 경기 2 Client 이탈 직후 network-despawn) — 진단 관찰자의 판정이며 범위 밖이라 원인을 확인하지 않았다(15-8의 같은 이름 FAIL과 같은 원인인지도 [미확인]).
- 그 밖에는 기존에 알려진 경고뿐(UnitFactory 스폰 순서 128, WAVE clock-invalid 2, root-pose 용량, inferno-vfx-overflow 등). **예외(Exception) 0건.**

<a id="16-9-fix-options"></a>

### 16-9. 수정안 (제안 — 미실행, 🔴 사용자 결정 대기)

> 분석의 제안을 옮긴 것이다 [전달]. 효과는 실측 기반 **예상**이며 실행으로 확인하지 않았다. 코드 변경은 사용자 결정과 별도 승인 뒤에만 한다.

| 안 | 내용 | 예상 효과 | 장점 | 단점 · 위험 | 영향 코드 [전달] |
|---|---|---|---|---|---|
| **(a) 번호표 단위 사망 · 화상 표현 보류** (분석 권고) | Client에서 치명타 번호표가 보관 중인 대상의 사망 표현(`OnUnitDied` 재발행 · IsDead · 사망 VFX · 소멸)과 화상 VFX 시작을 그 키의 화면 착탄까지(상한 약 0.3초) 미룬다. 도메인 제거는 즉시 유지 | 앞섬 0(화면 착탄과 같은 프레임) | 정확함. 서버 판정 · Host 무변경 | **규모가 크다.** NGO Despawn이 오브젝트를 파괴하므로 시각 대리체(ghost)를 떼어 두거나 서버 Despawn 유예가 필요하다. 재경기 · ID 재사용 · 관찰자 Retire · 호밍 IsAlive 판정과 얽힌다. 화상만은 `Start + 지연`까지 미루면 비교적 간단 | NCC 3362~3394 · 2905 / NetworkUnit 1099~1160 / UnitView 2009~2035 / HitPresentationQueue 195 · 563 / TimedDamage Receive 73 |
| **(b) 투사체 Client 지연 0.10 → 0** | 공유 상수에서 떼어 투사체 전용 값으로 둔다 | 앞섬 약 0.13 → 약 0.05초(\|resultLag\|만큼 남음). 늦은 결과(6/286, 최대 +0.05초)는 끝점에서 기다림(`ProjectilePresentationCoordinator.cs` 231~246행 — [확인]) | 한 곳만 바꾸면 됨, 위험 낮음. (a)와 함께 쓸 수 있음 | 0이 되지는 않는다. 발사 기록이 늦게 오면 begin이 늦어져 진행도가 튈 수 있다(발사 수신 지연 미측정) | Presenter 59~60 · 246 |
| **(c) 적응형 오프셋** | 화면 시각 = 서버 시각 − EMA(resultLag)로 두어 화면 착탄을 결과 도착 순간에 맞춘다 | 대부분 약 0, 흔들림만큼 오차 | (a)보다 훨씬 작음 | HP와 결과 RPC가 같은 프레임에 오지 않는 표본이 있다(finish − HP 0.046~0.176초 편차, 원인 미확인) | Presenter 197 · 246 |

- **분석 권고 [전달]:** 정확도를 우선하는 원칙(CLAUDE.md 규칙 7)에 따르면 (a)가 근본 해결이다. 화상만 먼저 `Start + 지연` 보류로 처리하는 것은 비교적 작다. (b)는 빠른 완화책으로 (a)와 함께 쓸 수 있다.
- 🔴 **규칙과의 관계 — 변경 전 사용자 보고 대상 [확인 — 원문 대조, 판정은 사용자]:**
  - (b) 투사체 Client 지연 0은 `NET-TIME-002`의 「실제 적용값은 … 0.075~0.25초 범위에서 조정한다」 범위 밖이다.
  - (c)는 같은 문장의 「조정」과 방향은 맞지만, 기준(「NetworkTransform 보간 지연과 추정 단방향 지연 + jitter margin 중 큰 값」)과 다른 기준(resultLag EMA)이다.
  - (a)는 `NET-TIME-002`의 맞춤 대상 목록(「표시용 HP바·텍스트·피격 반응」)에 사망 표현 · 화상 VFX를 더하는 쪽이다. 지금 문장이 사망 표현을 금지하지는 않지만 명시하지도 않는다.
- **fixture 후보** (`Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.ServerProjectiles.cs` — 메서드 위치 [확인]): 857 `ValidateServerProjectilePresentationCoordinator`(지연값과 늦은 결과의 끝점 대기) · 914 `ValidateServerProjectileImpactHold` · 959 `ValidateServerProjectileKillShotNumbers`(사망 표현 보류) · 1561 `ValidateServerProjectileStageDSourceGates`(상수 분리 게이트) · 새로 만들 것: 화상 Play를 `Start + 지연`까지 미루는 검증 [전달].

<a id="16-10-unconfirmed"></a>

### 16-10. 미확인 · 남은 것

1. **회귀 스위트 결과 [미보고].** 사용자 Console 결과를 받아야 한다.
2. NGO ServerTime 내부 버퍼 구성(실측만 있음) · HP RPC와 결과 메시지의 도착 프레임 차이 원인 · Despawn 메시지의 Client 도착 시각(로그 없음, 코드상 사망 RPC 직후 같은 프레임 송신) · 표본(맵당 24발) 밖 투사체별 앞섬 값 · 파티클 오류를 낸 프리팹 [미확인 — 분석 원문 「미확인」].
3. **실기에서 아직 안 나온 구성:** 기기 = Host, 싱글, Tank · CannonCart · StreamSpirit · 인간 타워와 섞인 경기(15-10 항목 1과 같은 이유로 유닛 투사체 6·7·17 · 인간 타워 로그 회귀 확인 없음).
4. **에디터 `[UAS-MOVE-AUTH]` FAIL** — 범위 밖, 원인 미확인(16-8).
5. **남은 대상:** EagleArcher · MushroomBomber. 수정안(16-9)을 고르면 단계 D 재실기가 필요하다 [추정 — 차지 유닛 화면 경로가 바뀌므로].
