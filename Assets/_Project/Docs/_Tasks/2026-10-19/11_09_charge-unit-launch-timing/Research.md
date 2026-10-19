# Research — 차지 유닛 발사 시점 (InfernoSpirit · FoxMagician)

> 작성 2026-10-19 · 기준 checkout HEAD `4fb37139` · D1~D3 사용자 결정 완료(2026-10-19, 1절 결정 기록) → 같은 폴더 `Plan.md` 승인·구현됨 · **상태: 완료** — 사용자 싱글·멀티 시각 확인(2026-10-19). 결과·한계는 [9절](#9-implementation-result) *(2026-10-19 갱신: 종전 「Plan 작성됨(승인 전)」)*

## 0. 쉬운 설명

지옥불 정령(InfernoSpirit)과 여우 마법사(FoxMagician)는 공격할 때 먼저 힘을 모으는 「차지」 이펙트를 보여 주고, 그다음 투사체(화염구·마법탄)를 쏜다. 지금 지옥불 정령은 차지와 화염구가 **같은 순간**에 시작돼 「모으는 중에 이미 쏜」 것처럼 보이고, 여우 마법사는 차지는 제대로 나오지만 **투사체를 아예 쏘지 않는다.**

사용자는 2026-10-19에 기준을 정했다. **애니메이션에 찍힌 두 신호로 정한다 — 「차지 시작」 신호에서 차지 이펙트를 켜고, 「타격」 신호에서 투사체를 쏘고 공격 소리를 낸다.** 차지 이펙트는 그 두 신호 사이 시간에 딱 맞게 점점 커지도록 길이를 맞추고, 차지 도중 공격이 끊기면 차지 이펙트는 그 자리에서 사라진다. **서버가 피해를 주는 시점은 이번에 바꾸지 않는다**(그 일은 나중의 「서버 투사체」 작업 — 옵션 C에서 한다).

이 문서는 그 작업을 하기 전에 **지금 코드·애니메이션·이펙트가 어떻게 되어 있는지, 무엇을 바꿔야 하는지, 어디가 위험한지**를 정리한 조사 문서다. 코드·에셋·규칙 문서는 아직 아무것도 바꾸지 않았다. 가장 중요한 발견은 하나다 — 멀티플레이에서 공격 연출을 「1회분 허가표」로 관리하는데, 차지 신호가 그 허가표를 먼저 써 버리면 타격 순간에 투사체가 **나가지 않는다**(4-2). 그래서 「차지는 허가표를 확인만 하고, 쓰는 것은 타격 때」로 바꾸는 것이 핵심이다.

---

## 1. 🔴 사용자 결정 필요 (Plan 작성 전에 정할 것)

| # | 질문 | 선택지 | 메인 세션 추천 |
|---|---|---|---|
| **D1** | 여우 마법사의 **데미지 숫자 즉시 표시 예외**(규칙 19)를 그대로 둘까? 지금 여우 마법사는 서버 피해 결과가 오면 숫자를 **즉시** 띄운다. 이번에 투사체가 생기면 숫자가 투사체 도착보다 **먼저**(최대 약 0.6초 [추정]) 뜰 수 있다(4-3). | (가) 유지 — 숫자는 지금처럼 즉시 / (나) 투사체 도착에 맞춰 숫자 표시 — Application 계층과 규칙 19 변경이 필요해 이번 범위(화면 전용)를 넘는다 | **(가) 유지.** 숫자·피해를 도착에 맞추는 일은 옵션 C(서버 투사체)에서 함께 한다 |
| **D2** | 차지가 끝난 뒤(타격 신호 전) 공격이 **취소**되면 투사체·공격 소리를 낼까? 취소돼도 서버 피해는 이미 예약돼 있어 **피해 숫자만** 뜰 수 있다(4-4). | (가) 투사체·SFX 없음(차지 이펙트만 즉시 사라짐) / (나) 취소돼도 투사체 발사 | **(가) 투사체·SFX 없음.** 피해 숫자만 뜨는 어긋남은 옵션 C에서 해소 |
| **D3** | InfernoSpirit 공격 클립에 차지 신호를 넣는 일과 두 차지 프리팹 길이를 고치는 일은 **누가** 할까? | 클립 이벤트: (가) 사용자가 Unity Animation 창에서 / (나) 코드 에이전트가 .anim 파일 직접 편집. 프리팹 길이: (가) 사용자 / (나) 코드 에이전트 | **클립 이벤트는 사용자가 Animation 창에서**(4-6 — 파일 직접 편집은 위험 [추정]). **프리팹 길이는 사용자 결정** |

> **결정 기록 (2026-10-19, 사용자 결정 — 위 표는 질문 당시 원문 그대로 보존):**
> - **D1 → (가) 유지.** FoxMagician 데미지 숫자 즉시 표시 예외(`GameSystemRules_Units.md` 규칙 19)는 이번에 바꾸지 않는다. 피해·숫자 시점은 옵션 C(서버 투사체) 작업에서 다룬다. 이번 작업 뒤 **Fox 숫자가 투사체 도착보다 먼저 뜨는 것은 알려진 한계**로 Plan에 기록한다.
> - **D2 → (가).** 차지 도중(타격 신호 전) 공격이 취소되면 **차지 VFX는 즉시 사라지고, 투사체·발사음은 모두 없다.**
> - **D3 → (나) 코드 에이전트.** 메인 세션 추천(클립은 사용자가 Animation 창에서)과 **다르게**, `InfernoSpirit_Attack.anim`에 `OnAttackCharge` @ 0.16666667을 넣는 일과 두 차지 프리팹 길이 맞추기를 **모두 game-programmer가 YAML로 직접 수정**한다. 불가하면 사용자에게 보고한다. 4-6의 「YAML 직접 편집은 위험 [추정]」은 질문 당시 판단으로 남기며, 위험 관리는 Plan(위험·검증)에서 다룬다.
>   - 프리팹 목표값(사용자 지정): Inferno `lengthInSec`·Start Lifetime 1 → **1.3333**. Fox 루트 Duration·Lifetime 2 → **1.8333**, Wisps는 **Duration + 최대 Lifetime ≤ 1.8333**(예: Duration 0.8333, Lifetime 0.6~1.0). 프로그래머가 「점차 차징되는」 시각 의도를 유지하며 최종값을 정하고 그 근거를 기록한다.

---

## 2. 근거 수준과 기준

- [확인] = document-manager가 HEAD `4fb37139`에서 해당 파일·행을 직접 읽어 확인. 이 문서 작성 때 **표본으로 다시 확인한 행**에만 붙였다.
- [전달] = game-programmer 읽기 전용 조사 보고서(2026-10-19, 메인 세션 경유) 값. 다시 재지 않았다.
- [추정] = 조사자 또는 이 문서의 해석이며 실행·실기로 확인하지 않음. [미확인] = 코드만으로 정하지 못함.
- 행 번호는 HEAD `4fb37139` 기준이다. 코드가 바뀌면 어긋나므로 **메서드 이름을 함께 적었다**(행 번호만으로 위치를 가리켜 전부 어긋난 과거 실수 — `.claude/mistakes.md` 2026-08-24).
- 이 문서는 [vfx-code-followups Research 15-6](../../2026-10-13/13_53_vfx-code-followups/Research.md#15-6-charge-unit-fire-timing)과 [Plan 18-6](../../2026-10-13/13_53_vfx-code-followups/Plan.md#18-6-charge-unit-fire-timing)의 후속 항목 **CH-1~CH-3을 실제로 진행하는 별건**이다. 15-6은 HEAD `3306cf1b` 기준 기록이며 고치지 않는다.

---

## 3. 사용자 확정 기준 (2026-10-19) [사용자]

| 항목 | InfernoSpirit | FoxMagician |
|---|---|---|
| 차지 VFX 시작 = `OnAttackCharge` | 0:05 @30fps = **0.1667초** | 1:00 @30fps = **1.0초** |
| 투사체 발사 + 공격 SFX = `OnAttackHit` | 1:15 @30fps = **1.5초** | 2:25 @30fps = **2.8333초** |
| 차지 VFX 길이(프리팹을 차지 구간에 맞춤) | **1.333초** | **1.833초** |

- 차지 VFX는 점차 차징되어 **시작과 끝이 분명하게** 보이도록 한다.
- 차지 도중 공격이 끊기면 차지 VFX는 **즉시** 사라진다.
- 공격 SFX는 **발사 순간**. 차지 SFX는 추후 SFX 작업에서 다룬다.
- 서버 피해 시점은 **이번 범위 밖**(투사체 유닛 서버 투사체 작업 = 옵션 C와 함께).
- 규칙 문서(Units 규칙 17 「FoxMagician typed marker 계약」, UnitCombatSynchronization 「8. Animation Event와 표현」)를 새 기준으로 고치는 것은 **승인됨** — 단 Plan 승인 후 수정한다. `StatsReference.md`에도 시점을 기록한다.
- FoxMagician tracer·impact 에셋(사용자 커밋 `683e1566`)을 이번에 `UnitEffectConfig`에 연결한다.
- 15-6과 달라진 점: 15-6-1은 「`OnAttackCharge` = 차지 VFX만」으로 적었고 Plan 18-6 CH-1은 Inferno 차지 위치를 「0.5초 부근 — 사용자 결정」으로 남겼다. 이번에 **0.1667초로 확정**됐고, 공격 SFX는 발사 순간으로 정해졌다.

---

## 4. 현재 상태 조사

### 4-1. 차지 신호 처리와 「차지 유닛」 판별

- [확인] `UnitView.cs` `OnAttackCharge`(1149~1155행)는 1151행에서 **FoxMagician이 아니면 바로 return**한다.
- [확인] 저장소의 `.anim` 중 `OnAttackCharge`가 있는 클립은 `FoxMagician_Attack.anim` **하나뿐**이다(113277행).
- [확인] `FoxMagician_Attack.anim`: `OnAttackCharge` @ 1(113276~113277행), `OnAttackHit` @ 2.8333333(113283~113284행) — 이미 사용자 기준과 같다.
- **판별 방법 후보** [전달 — 추천은 조사자 제안]:

| 안 | 내용 | 장점 | 단점 |
|---|---|---|---|
| **(b) 추천** | `Initialize` 때 공격 클립(`_Attack`으로 끝나는 클립 — `UnitView.cs` 809~814행의 선택 방식과 같게 [확인])에 `OnAttackCharge`가 있는지 캐시 | 애니메이션 이벤트가 곧 기준이라 사용자 기준과 일치. 새 유닛도 클립에 이벤트만 넣으면 됨 | 이벤트를 빠뜨리면 조용히 일반 유닛처럼 동작 → 자동 검증으로 보완(「`OnAttackCharge` 클립 집합 = {Fox, Inferno}」, 두 유닛 attack preset `StopWhenCasterDies` = true) |
| (a) | 유닛 타입 목록 | 단순·명확 | 유닛 추가 때마다 코드 수정 |
| (c) | preset의 `StopWhenCasterDies`로 판별 | 기존 표시 재사용 | 「시전자 사망 시 정지」와 「차지 유닛」은 다른 의미 |
| (d) | 새 설정 필드 | 명시적 | 설정·검증 추가 비용 |

### 4-2. 타격 마커 경로와 🔴 lease(1회분 허가표) 충돌

**현재 타격 마커 경로** (`UnitView.cs` `ProcessAttackMarker`):

| 위치 | 내용 | 근거 |
|---|---|---|
| 7224~7236행 | FoxMagician은 타격 마커에서 진단(`RecordFoxMagicianNativeImpactMarker`, 7228행)만 기록하고 **일찍 return** — 투사체를 쏘지 않는다 | [확인] |
| 7344~7349행 | 멀티(네트워크 활성)에서 `_attackPresentationSourceMarkerLease.TryConsume(...)` — **차지 마커도 여기서 lease를 소비**한다 | [확인] |
| 7519~7523행 | 공격 VFX 재생 `effectManager.PlayUnitAttack(...)` — 이 한 곳뿐 | [확인] (15-6-2에서도 「한 곳뿐」 확인) |
| 7605행 | 공격 SFX `PlayUnitAttackSfx` (주석 「규칙 15 — VFX와 짝」) | [확인] |
| 7612행 | `if (chargeOnly \|\| sourceOnlyPresentation) return;` — 차지 마커는 여기서 종료 | [확인] |
| 7627행 · 7669~7688행 | Legacy tracer(`em.PlayTracer(...)`) | [전달] (7669행은 15-5·15-6에서 확인) |

**lease 충돌** [전달 — 핵심 위험]:

- FoxMagician·InfernoSpirit은 둘 다 투사체 체계 미결정(Unresolved) 유닛이라 LegacyFallback으로 연출한다. [확인] `UnitAttackShadowProfileResolver.cs` 64행(Inferno)·66행(Fox)이 `UnresolvedProjectile(...)`을 돌려준다. 같은 파일 22~27행 `CanUseNativeAttackPhase`는 Fox를 명시적으로 빼고, `CanRunSequencer`(`UnitAttackShadowProfile.cs` 71~76행)는 Supported만 허용한다. *(보고서는 22~27행을 Unresolved→LegacyFallback 근거로 묶어 적었으나, 이 행은 native 공격 phase 제외 조건이다 — 결론은 같다.)*
- 커밋(공격 회차 확정)마다 `StartCombatAnimation`이 lease를 `HitFrameTimes.Length` = **1회분** 장전한다 [전달 — `UnitView.cs` 8272~8283행(8272행 LegacyFallback 분기 [확인]), `NetworkCombatController.cs` 994~1042행].
- `TryConsume`(`UnitAttackPresentationPolicy.cs` 657~672행, [확인] 657행 시그니처·660행 「`!IsArmed || _remainingMarkerCount <= 0`」이면 실패)는 1회분을 소비한다.
- 그래서 InfernoSpirit에 차지 신호를 넣으면, **멀티에서 차지가 1회분을 써 버리고 → 타격 마커가 Suppressed(7356~7385행)되어 → 투사체가 안 나간다** [전달 — 코드 경로 해석, 실행 확인 아님].
- 싱글은 lease를 쓰지 않는다(7387행, 정책 345~348행) [전달] → 싱글에서는 이 문제가 드러나지 않아 **싱글 테스트만으로는 놓친다.**
- FoxMagician은 지금 차지에서 1회분을 쓰고 타격에서는 일찍 return(7224행)이라 충돌이 드러나지 않았다. [확인] 자동 검증 `RunUnitActionSelfValidation.cs` 8095~8096행이 「Fox impact must preserve the charge lease count」를 요구한다.

**제안** [전달 — 조사자 제안, 미승인]:

- 차지는 lease를 **확인만**(IsArmed · source-only 아님 · revision 일치 · 회차당 1회 플래그) 하고, **소비는 타격**에서.
- lease를 2회분 장전하는 방식은 비추천 [추정].
- 차지 유닛의 타격 마커에서는 7519행 공격 VFX 재생을 **생략**(차지 VFX를 다시 켜지 않음)하고, 공격 SFX는 `PlayTracer` 옆으로 옮긴다(사운드 규칙 15 짝 주석 유지).
- Fox 진단 관측기(`UnitAttackShadowObserver` 1536~1539·1574~1577·1600~1603행 [전달])의 중복 기록을 막기 위해 Fox 기록 3종은 `chargeOnly`일 때만 남긴다.

### 4-3. FoxMagician 데미지 숫자 즉시 표시 (D1)

- [확인] `UnitCombatUseCase.cs` 1768~1772행: 공격자가 FoxMagician이고 결과 key가 유효하지 않으면 `immediatePresentation`으로 피해를 적용한다.
- [확인] `GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 규칙 19 「피격 표현 큐」(330행)가 이 예외를 규칙으로 정한다.
- [전달] `HitPresentationQueue.cs` 420~425행이 즉시 표시를 처리하므로 큐가 비어 있고, 투사체 도착 신호(7679행)는 빈 큐에서 버려진다 — [확인] 565~570행 주석 「빈 신호는 그냥 버린다」.
- 결과: 투사체를 쏘게 되면 숫자가 도착보다 **최대 약 0.6초 먼저** 뜰 수 있다 [추정]. 이것을 바꾸려면 Application 계층 + 규칙 19 변경이 필요하다 → 이번 범위 밖(D1).

### 4-4. 공격 취소 시 차지 VFX 정지 (D2)

- [확인] 정지 함수 `EffectManager.StopCasterOwnedAttackEffects`(283행~, 보고서 283~300행). 현재 호출: `UnitView.cs` 1529행(`"CasterRemoved"`)·1818행(`"CasterDeath"`) — **시전자 제거·사망 때만** 멈춘다.
- 「공격이 끊긴」 경계 후보 [전달]:
  - `ClearCombatTargetTracking`(8032~8047행, [확인] 8032행) — 호출: `StopCombatAnimation` 8620, `EndServerActionRotation` 8180, `MoveCleanupAndCompleteV3` 6711, `Initialize` 1634.
  - `PreserveCommittedSourceMarkerAndCloseTargetImpact`(8009~8030행).
  - `RetireAndCloseAttackPresentationScope`(7972~8002행) — 호출 이유: combat-target-change 8559, provisional-suppression 8569, presentation-revision-replaced / scope-arm-rejected 8257·8270.
  - 싱글 `ChangeTarget`(8536~8561행)은 retire를 부르지 않으므로 **별도 처리가 필요**하다.
- 제안 [전달]: `UnitView`에 「차지 정지」 헬퍼를 두고 위 경계에서 부른다.
- 정지해도 **피해는 들어간다** [전달]: 싱글 `UnitCombatUseCase` 1093행, 멀티 `DelayedAttackDamage`(1100~1145행)는 공격자 사망으로만 취소된다. Unresolved 유닛의 사거리 재판정 여부는 [미확인]. → D2의 「숫자만 뜰 수 있다」의 근거.

### 4-5. 서버 피해와 자동 검증에 대한 영향

- 서버 피해 시점: 영향 없음. `UnitFactory.cs` 586행은 `OnAttackHit`만 추출하고, `UnitView.cs` 820·849행은 native 전용이다 [전달 — 15-6-2에서 [확인]된 사실과 같다].
- 갱신이 필요한 자동 검증(`Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs`) [전달, 표본 [확인] 표시]:

| 검사 | 위치 | 이번 변경 후 |
|---|---|---|
| Fox 이벤트 fixture | 8052~8122행 (8095~8096 lease 보존 [확인], 8103~8108, 8113 `localHits == 0` [확인]) | **깨짐** → 새 기준으로 갱신 |
| Fox tracer null 요구 | 7966~7967행 [확인] — 「FoxMagician tracer is not implemented…」 | **깨짐** → Inferno(8255~8284행)처럼 「연결됨」 검사로 바꾸고 원 검사는 주석으로 보존 |
| Fox 타임라인 | 7901~7921행 | 통과 [추정] |
| Inferno 타임라인 | 8206~8216행 | 통과 [추정] + 차지 0.1667초 1개 검사 추가(Fox 7906~7911행 패턴) |
| 설명 주석 | 8125~8127 · 7811~7813행 | 갱신 |
| Fox 피해 경계 | 7606~7653 · 8040행 | 변경 없음(D1 유지 시) |
| `ValidateCasterOwnedChargeVfx`(VfxConnections) | 87~202행 | 충돌 없음 |
| C3 요약 | 1036 · 1038행 | 확인 필요 |

### 4-6. InfernoSpirit 클립에 차지 신호 넣기 (D3)

- [확인] `Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim`은 93396줄이고, `m_Events`(93389행~)는 `OnAttackHit` @ 1.5 **하나뿐**이다.
- 클립 해시를 고정하는 검사는 `TorrentWaveProductionValidation` 37·41행(TorrentSpirit)뿐이고, Inferno/Fox는 GUID(8163·7865행)·길이 3·30fps·루프·`OrientationOffsetY` −58(8197~8204행)만 검사한다 [전달] → 이벤트를 넣어도 이 검사는 깨지지 않는다 [추정].
- 방법 [전달]: **사용자가 Unity Animation 창에서**(함수명 정확히 `OnAttackCharge`, 시각 0.16666667) 넣는 것을 권장. YAML 직접 편집(Fox 형식 113276~113282행)은 위험 [추정].

### 4-7. 차지 프리팹 길이

- 풀 반환은 파티클이 모두 끝났을 때(`IsAlive` false — `VfxPoolItem` 13~15·550행) [전달].

| 프리팹 | 현재 | 제안(미승인) |
|---|---|---|
| `vfx_infernospirit_charge.prefab` | 시스템 1개. Duration(`lengthInSec`) 1 [확인 44행], burst 1(979~983행), Lifetime 1 [확인 119행], Size over Lifetime 0.22→1(1036행~) | `lengthInSec`·Lifetime 1 → **1.3333** |
| `vfx_foxmagician_charge.prefab` | 루트 Duration 2 [확인 45행]·burst 2(984행)·Lifetime 2 [확인 120행]. Wisps Duration 2 [확인 4957행]·Lifetime 0.6~1.0(5031~5033행)·rate 22/s 곡선 0.25→0.6→1.0(5786행~) → 실제 꼬리 약 3초 [추정] | 루트 2 → **1.8333**. Wisps는 Duration + 최대 Lifetime ≤ 1.833(예: 0.833 + 1.0) |

- 검사 충돌 없음(Fox 7975행은 파티클 시스템 1개 이상만 요구) [전달]. 두 루트 `stopAction` 2(Destroy)는 기존 동작 [전달].
- 지금 Fox 차지는 차지 구간(1.833초)보다 길게 남는다 → 투사체가 나간 뒤에도 차지가 보일 수 있다 [추정].

### 4-8. 멀티플레이

- 두 유닛 모두 KnownUnresolved라 `authoritativePresentation` = false(Shadow 825~828행) [전달].
- Host는 커밋과 같은 프레임(1017~1024행), Client는 RPC(1031·3220~3253행)로 시작한다. SFX는 각자 로컬 재생(사운드 규칙 14) [전달].
- Client 지연이 있으면 Inferno 차지(0.1667초 여유)가 **가끔 빠질 수 있다** [추정]. 이는 `GameSystemRules_UnitCombatSynchronization.md` 「8. Animation Event와 표현」 NET-PRESENT-001(305행~)이 허용하는 정상 결과로 보이며, 실기 확인이 필요하다.

---

## 5. 변경 예상 파일 (초안 — Plan에서 확정) [전달]

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 4-1~4-4 행들. 🔴 Fox 타격 마커 일찍 return(7224행) 제거는 **Plan 최상단 「제거·비활성화」**에 근거와 함께 적는다(WORKFLOW [4] — 비활성화 우선) |
| `UnitAttackPresentationPolicy.cs` | lease 「확인 전용」 API가 필요할 때 |
| `AnimationEventRelay.cs` 39행 | 주석 |
| `VfxPoolItem.CasterOwned.cs` 5~9행 · `EffectManager.cs` 259행 · `EffectPreset.cs` 59행 | 주석 |
| `Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim` | `OnAttackCharge` @ 0.16666667 (D3) |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` 160~167행 FoxMagician(unitType 21) 행 | [확인] 현재 `impactPreset`·`tracerPreset` 모두 `{fileID: 0}`. 연결 대상: tracer `EffectPreset_FoxMagician_Tracer`(GUID `025332764a8d09e4d97973e8c76a487e`), impact `EffectPreset_FoxMagician_Impact`(GUID `bbc25dda207ccb84a9068c4a63392e73`) — [확인] 두 GUID가 각 preset `.meta`의 것이며, 둘 다 사용자 커밋 `683e1566`(2026-10-19 19:59, 메시지 「.」)이 프리팹 2개와 함께 추가했다 |
| 두 차지 프리팹 | 4-7 (D3) |
| `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` (+ 선택: VfxConnections 검사) | 4-5 |
| 문서 | `GameSystemRules_Units.md` 규칙 17(320행) · `GameSystemRules_UnitCombatSynchronization.md` 「8. Animation Event와 표현」(309행) · `StatsReference.md` 71행(Inferno — 예: 「1.5 (피해·발사; charge 0.1667)」)·49행(시간 설명 문단). **이번 Research 단계에서는 수정하지 않는다** |

---

## 6. 위험

1. **lease 충돌** — 차지가 1회분을 소비해 멀티에서 Inferno 투사체가 안 나감(4-2). 싱글 테스트로는 드러나지 않는다.
2. **Client 지연** — Inferno 차지가 가끔 빠질 수 있음 [추정](4-8).
3. **Fox 숫자 선행** — 숫자가 투사체 도착보다 먼저 뜸 [추정](4-3, D1).
4. **취소 후 피해** — 차지 VFX·투사체 없이 피해 숫자만 뜰 수 있음(4-4, D2).
5. **Fox Wisps 꼬리 약 3초** — 차지 구간을 넘어 남음 [추정](4-7).
6. **진단 관측기 중복 기록** — Fox 기록 3종(4-2 제안).
7. **이벤트 누락 시 조용한 회귀** — (b) 판별 방식에서 클립 이벤트를 빠뜨리면 일반 유닛처럼 동작 → 자동 검증으로 막는다(4-1).
8. **R5 버그 B2~B4가 Fox에도** — Fox가 같은 Legacy tracer 경로를 쓰게 되므로 [vfx-code-followups Research 15-5](../../2026-10-13/13_53_vfx-code-followups/Research.md#15-5-r5-field-bugs)의 B2(이동 대상 고정 지점)·B3·B4(대상 크기에 가려짐)가 Fox에도 나타날 수 있다.

---

## 7. 근거 규칙과 🔴 규칙 문서 변경 대상

| 규칙 | 위치 | 이번 작업과의 관계 |
|---|---|---|
| `GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 규칙 17 「타격 프레임 타이밍의 단일 출처」(311행~) — 「FoxMagician typed marker 계약」(320행) | [확인] 320행: 「`OnAttackHit @ 85/30초`는 … 로컬 callback은 … charge lease 소비나 VFX 재생을 하지 않는다」 | 🔴 새 기준(타격에서 투사체 발사 + lease 소비)과 **충돌**. 수정 승인됨 — Plan 승인 후 수정, 규칙 번호 유지 |
| 같은 문서 규칙 18 「서버 데미지 타이밍 정밀화」(324행) | — | 서버 피해 시점 무변경 |
| 같은 문서 규칙 19 「피격 표현 큐」(329행~, Fox 예외 330행) | [확인] | D1 |
| 같은 문서 「특수 공격 시스템 규칙」 454행 — 2026-10-13 「Stop When Caster Dies」 덧붙임 | [확인] | 차지 VFX 정지 경로 |
| `GameSystemRules_UnitCombatSynchronization.md` 「8. Animation Event와 표현」 NET-PRESENT-001 「Animation Event 역할」(305행~), Fox 문단 309행 | [확인] 309행: 「damage callback에서 charge를 소비·재생하지 않는다」 | 「차지를 다시 재생하지 않음」과는 맞지만 Fox 투사체 발사·lease 소비 위치를 새 기준으로 고친다. 수정 승인됨 — Plan 승인 후 |
| `GameSystemRules_Sound.md` 「SFX 규칙」 규칙 14 「멀티플레이 SFX 동기화 금지」(105~107행) · 규칙 15 「VFX+SFX 쌍 호출 규칙」(109~122행) | [확인] 제목 행 | SFX 로컬 재생 · SFX를 발사 순간으로 옮길 때 짝 주석 |
| `StatsReference.md` 49·71행 | [확인] 71행 Inferno 「1.5」 | 시점 기록(Plan 승인 후) |

---

## 8. 다른 기록과의 관계

- [vfx-code-followups Plan 18-6](../../2026-10-13/13_53_vfx-code-followups/Plan.md#18-6-charge-unit-fire-timing) CH-1(Inferno 클립 이벤트) · CH-2(코드) · CH-3(규칙 문서)을 이 작업이 진행한다. 그 Plan의 표는 당시 기록으로 두고 고치지 않는다(`_Tasks/`는 이력).
- 옵션 C(서버 권위 투사체 — [vfx-code-followups Research 14절](../../2026-10-13/13_53_vfx-code-followups/Research.md#14-projectile-visibility-decision))는 별건이다. 이 작업은 「언제 쏘는 모습을 보일지」만 다루고 「언제 피해를 줄지」는 옵션 C가 다룬다.
- Testcase.md: 사용자 요청이 없어 만들지 않았다(WORKFLOW [5-1]).

---

<a id="9-implementation-result"></a>

## 9. 구현 결과와 사용자 확인 (2026-10-19 추가)

**쉬운 설명.** 계획대로 고쳤고, 사용자가 싱글과 멀티에서 지옥불 정령·여우 마법사의 「차지 → 발사」가 제대로 보이는 것을 눈으로 확인했다. 확인 뒤 지옥불 정령의 화염구·착탄 불티가 초록빛으로 보이는 문제가 하나 더 나와, 불티 재질만 흰색 재질로 바꿨고 사용자가 확인했다. 게임 결과(피해 시점·피해량·HP)는 바뀌지 않았다. 이 절 이전 내용(1~8절)은 조사 당시 기록이라 고치지 않았다.

근거 표기: [직접] = document-manager가 이 checkout(HEAD `609ba447`)에서 커밋 diff·파일을 읽어 확인 · [전달] = 메인 세션 인계 · [사용자] = 사용자 보고.

### 9-1. 사용자 결과 (2026-10-19) [사용자 — 메인 세션 경유]

| 항목 | 결과 |
|---|---|
| 싱글·멀티 테스트에서 두 차지 유닛 VFX 재생 | **정상 — 사용자 시각 확인** |
| Inferno 투사체·착탄 불티 초록빛 | 재질 교체(9-3) 뒤 **사용자 확인** |
| 로그 기반 검증 | **없음.** 사용자가 로그를 저장했으나 본문(`_Logs/2026-10-19/21_43_logcat/RuntimeLog_device.txt`)은 `.gitignore` 814행 규칙으로 저장소에 올라오지 않아 읽지 못했다 [직접 — `git check-ignore -v`]. 저장소에는 `.meta`만 있다(사용자 커밋 `7da90f92`) |
| 회귀 스위트(`Hexiege/Combat/Run Full Regression Suite`) | **미보고** — 사용자가 결과를 따로 알리지 않았다. PASS로 적지 않는다 |
| Plan 7절 항목별(S-1~S-5, M-1~M-4) | 항목별 결과는 따로 보고되지 않았다. 위 「VFX 정상 재생」이 어느 항목까지 덮는지(예: 차지 도중 취소 S-3·M-4, Client 차지 누락 빈도 M-3)는 **확인되지 않았다** |

### 9-2. 실제 구현 [직접 — 커밋 `8330b5d9` diff]

- `InfernoSpirit_Attack.anim`: `m_Events`에 `OnAttackCharge` @ 0.16666667 추가, `OnAttackHit` @ 1.5 유지.
- `vfx_infernospirit_charge.prefab`: `lengthInSec`·Start Lifetime 1 → **1.3333334**.
- `vfx_foxmagician_charge.prefab`: 루트 `lengthInSec`·Start Lifetime 2 → **1.8333334**. 자식 Wisps `lengthInSec` 2 → **0.8333333**(Lifetime 0.6~1.0은 그대로 → 끝 약 1.8333초).
- `UnitEffectConfig.asset` FoxMagician(unitType 21): `impactPreset` = `EffectPreset_FoxMagician_Impact`(`bbc25dda…`), `tracerPreset` = `EffectPreset_FoxMagician_Tracer`(`02533276…`).
- `UnitView.cs`: 차지 유닛 = 공격 클립(`_Attack`)에 `OnAttackCharge`가 있는 유닛(캐시) · 차지 신호는 source lease를 **확인만**(Armed·남은 횟수·SourceOnly 아님·현재 회차·회차당 1회), impact lease는 `IsArmed` 확인만 · 타격 신호는 종전대로 두 lease 소비 → 차지 VFX 재재생 생략 → 투사체(트레이서) 발사 + 발사음 · 취소 시 차지 정지(`RetireAndCloseAttackPresentationScope` — 정상 회차 교체 사유 `presentation-revision-replaced` 제외 — 와 `PreserveCommittedSourceMarkerAndCloseTargetImpact` 2곳) · Fox 타격 조기 return 주석 처리.
- 주석만: `AnimationEventRelay.cs`·`EffectManager.cs`·`EffectPreset.cs`·`VfxPoolItem.CasterOwned.cs`.
- 자동 검증: `RunUnitActionSelfValidation.cs`(Fox fixture 새 기준·옛 단언 주석 보존, Fox tracer/impact 연결 검사, Inferno 차지 0.1667 검사, 차지 클립 집합·차지 프리팹 길이 검사) · `RunUnitActionSelfValidation.VfxConnections.cs`(실제 EffectManager로 차지→발사·취소·SourceOnly·싱글 경로 fixture 추가).
- 바뀌지 않음: `UnitAttackPresentationPolicy.cs`(P3 불필요) · `UnitCombatUseCase.cs` · `HitPresentationQueue.cs` · `NetworkCombatController.cs` · `UnitFactory.cs` · `FoxMagician_Attack.anim` [직접 — 커밋 파일 목록].

계획과 달라진 점은 [Plan 9-2](Plan.md#9-2-plan과-달라진-점)가 단일 소스다.

<a id="9-3-추가-수정--inferno-불티-초록빛-커밋-f899238f609ba447"></a>

### 9-3. 추가 수정 — Inferno 불티 초록빛 (커밋 `f899238f`·`609ba447`)

| 프리팹 | 레이어 | 재질 변경 |
|---|---|---|
| `vfx_infernospirit_impact.prefab` | `Embers` | `Mat_VFX_SharpSpark`(GUID `ba98d9c3…`) → `Mat_VFX_GlowAdditive`(GUID `b1f44011…`) [직접] |
| `vfx_infernospirit_tracer.prefab` | `Ember_Trail` | 같음 [직접] |

- 원인: `Mat_VFX_SharpSpark`의 텍스처는 `Art/VFX/RuneSpire/vfx_sharp_spark.png`(RuneSpire 번개용) [직접 — 재질의 텍스처 GUID → `.meta`]이며 청록색이다(평균 RGB 약 89/237/238 [전달 — 이 checkout에는 이미지 측정 도구가 없어 다시 재지 못함]). 파티클 색은 레이어의 색(Start Color·Color over Lifetime)에 텍스처 색이 **곱해져** 나오므로, 주황 불티가 초록빛으로 보였다. `Mat_VFX_GlowAdditive`의 텍스처는 `Art/VFX/Pistoleer/vfx_soft_glow.png`(흰색 [전달])라 레이어 색이 그대로 나온다.
- 🔴 같은 `Mat_VFX_SharpSpark`를 FoxMagician `vfx_foxmagician_impact.prefab`·`vfx_foxmagician_tracer.prefab`의 `Sparkles` 레이어와 `vfx_runespire_attack.prefab`도 쓴다 [직접 — GUID 검색, 렌더러 소유 GameObject 이름 확인]. 사용자는 Inferno만 바꾸라고 지시했으므로 Fox 쪽은 **그대로**다(사실 기록 — 후속 작업으로 정하지 않았다).

<a id="9-4-알려진-한계-유지"></a>

### 9-4. 알려진 한계 (유지)

- Fox 피해 숫자가 투사체 도착보다 먼저 뜬다(D1, 규칙 19 유지 — 옵션 C에서 다룬다).
- 차지 도중 취소돼도 피해 숫자만 뜰 수 있다(D2 — 옵션 C에서 다룬다).
- Client 화면에서 Inferno 차지가 가끔 빠질 수 있다 **[추정 — 실기 빈도 미보고]**.
- Fox 차지의 Wisps 입자 수는 방출 구간이 2 → 0.8333초로 줄어 약 2.4배 적어졌다(rate-over-time 곡선은 그대로) [전달]. 사용자 이의 없음.
- Fox `Sparkles`는 청록 텍스처 재질 그대로다(9-3).
- 차지 SFX는 없다 — 추후 SFX 작업(사용자 결정, 3절).

### 9-5. 상시 문서 반영

규칙 개정(Units 규칙 17 「차지 유닛 typed marker 계약」, UnitCombatSynchronization 「8. Animation Event와 표현」 NET-PRESENT-001)과 `StatsReference.md`·`VFXSFXList.md`·`VFXSFXGuide.md`·감사표·현황 문서 반영 내역, 개정 전 원문은 [Plan 9-4](Plan.md#9-4-상시-문서-반영과-개정-전-원문)에 있다. [vfx-code-followups Research 15-6](../../2026-10-13/13_53_vfx-code-followups/Research.md#15-6-charge-unit-fire-timing)의 머리 상태 표시는 이 작업으로 완료 표시했다.
