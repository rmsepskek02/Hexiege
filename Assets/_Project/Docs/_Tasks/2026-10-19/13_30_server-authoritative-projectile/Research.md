# Research — 서버 권위 투사체 (옵션 C)

> 작성 2026-10-19 · 기준 checkout HEAD `ab9d55d7` · 원 조사: game-programmer 읽기 전용 기술 조사 보고서(HEAD `657a4f31` 기준. 그 뒤 커밋 `344dfa42`·`ab9d55d7`은 문서와 미사용 VFX 에셋 삭제뿐이라 행 번호는 유효하다고 보았다 — [추정, 메인 세션 판단])
> **상태: 조사 완료 — 사용자 결정 대부분 기록됨, 남은 미결·Plan 승인 대기.** 질문 Q1~Q17과 결정 기록(Q12만 대기)·남은 미결 U-1~U-12·단계 계획은 같은 폴더 [Plan.md](Plan.md) 1-1·1-3절에 있다. 코드·에셋·규칙 문서는 이 작업으로 바뀌지 않았다(컨트롤러의 EagleArcher Attack2 상태 삭제는 사용자 커밋 `a11b5381` — 11절). *(2026-10-20 갱신 — 종전 「조사 완료 — 사용자 결정 대기」)*
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
