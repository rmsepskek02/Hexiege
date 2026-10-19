# Plan — 차지 유닛 발사 시점 (InfernoSpirit · FoxMagician)

> 작성 2026-10-19 · 기준 checkout HEAD `4fb37139` · 근거 조사: 같은 폴더 `Research.md`(D1~D3 결정 기록 포함)
> **상태: 완료** — P1~P5 구현(커밋 `8330b5d9`) · Inferno 불티 재질 추가 수정(`f899238f`·`609ba447`) · 사용자 싱글·멀티 시각 확인 2026-10-19 · P6 문서 반영 2026-10-19. 회귀 스위트 결과는 미보고, 로그 기반 검증 없음. 결과·한계는 [9절](#9-구현-결과-2026-10-19-추가). *(2026-10-19 갱신: 종전 「사용자 승인 전. 승인 전에는 코드·에셋·규칙 문서·`StatsReference.md`를 하나도 바꾸지 않는다(WORKFLOW [4]).」)*
> 근거 표기: [확인] = 이 Plan 작성 때 파일을 직접 다시 읽은 행 · [전달] = Research가 programmer 보고서에서 옮긴 값 · [추정] · [미확인] — Research 2절과 같은 기준. 행 번호는 HEAD `4fb37139` 기준이며 메서드 이름을 함께 적는다.

## 0. 이 계획이 무엇인가 (쉬운 설명)

지옥불 정령(InfernoSpirit)과 여우 마법사(FoxMagician)는 공격할 때 먼저 힘을 모으는 「차지」 이펙트를 보여 주고 그다음 투사체를 쏜다. 지금은 지옥불 정령이 **모으는 순간에 이미 화염구를 쏘고**, 여우 마법사는 **모으기만 하고 아예 쏘지 않는다.**

이 계획은 **화면에 보이는 것과 소리만** 고친다. 애니메이션에 찍힌 두 신호를 기준으로 삼는다.

- **「차지 시작」 신호** → 차지 이펙트를 켠다. 지옥불 정령은 공격 시작 후 0.1667초, 여우 마법사는 1.0초.
- **「타격」 신호** → 투사체를 쏘고 공격 소리를 낸다. 지옥불 정령은 1.5초, 여우 마법사는 2.8333초.
- 차지 이펙트 길이는 두 신호 사이 시간에 맞춘다(지옥불 1.3333초, 여우 1.8333초). 그래서 점점 모이다가 발사 순간에 끝난다.
- 차지 도중 공격이 끊기면 차지 이펙트는 **바로 사라지고**, 투사체와 소리는 **나오지 않는다.**

**게임 결과는 바뀌지 않는다.** 서버가 피해를 주는 시점·피해량·대상·HP·화상 지속 피해는 그대로다. 피해를 투사체 도착에 맞추는 일은 나중의 「서버 투사체」 작업(옵션 C)에서 한다. 그래서 이번 작업 뒤에도 남는 어긋남이 있다. 여우 마법사의 피해 숫자는 투사체가 닿기 전에 먼저 뜰 수 있다. 차지 도중 공격이 끊겨도 피해 숫자만 뜰 수 있다. 둘 다 알려진 한계로 6절에 적었다.

가장 중요한 기술 포인트는 하나다. 멀티플레이에서는 공격 한 번마다 연출을 1회분만 허락하는 「허가표」(lease)를 쓴다. 지금은 차지 신호가 그 허가표를 먼저 써 버린다. 이대로 지옥불 정령에 차지 신호를 넣으면 멀티에서 화염구가 나가지 않는다. 그래서 **차지는 허가표를 「확인만」 하고, 실제로 쓰는 것은 타격 때**로 바꾼다. 싱글플레이에서는 이 허가표를 쓰지 않으므로 **이 문제는 멀티 테스트에서만 드러난다.**

---

## 1. 🔴 기존 로직 제거·비활성화 항목 (WORKFLOW [4] — 최상단 명시)

모두 **삭제가 아니라 주석 처리로 비활성화**하고 새 코드를 옆에 둔다. 주석 처리분의 최종 삭제는 [6] 사용자 테스트 통과 후, [7] 문서/메모리 업데이트 전에 한다.

| # | 대상 (HEAD `4fb37139`) | 처리 | 안전한 근거 |
|---|---|---|---|
| **R-1** | `UnitView.cs` `ProcessAttackMarker`의 FoxMagician 조기 종료 블록(7224~7236행 [확인]): `if (!chargeOnly && Type == FoxMagician) { … RecordFoxMagicianNativeImpactMarker(…) … return; }` | **`return;`과 그 위 설명 주석 2줄만 주석 처리**한다. 블록 안 진단 기록 `RecordFoxMagicianNativeImpactMarker`(7228행, `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`)는 **그대로 살려 둔다**. 그러면 Fox 타격 마커가 일반 경로(lease 소비 → 트레이서)로 이어진다 | ① 서버 피해는 이 return과 무관하다. 피해 시점은 `UnitFactory.cs` 586행이 `OnAttackHit`만 추출한 서버 타이머가 소유한다(규칙 18, Research 4-5 [전달]). ② 트레이서는 순수 시각 표현이다(규칙 20). ③ Unresolved 유닛의 Legacy 공격 VFX·SFX·트레이서 보존 원칙(U-ATK-TIMELINE 마지막 문단)과 맞는다. ④ 이 return이 근거로 삼던 규칙 17 「FoxMagician typed marker 계약」은 사용자가 새 기준으로 수정을 승인했다(P6) |
| **R-2** | `UnitView.cs` `OnAttackCharge`의 타입 가드 `if (_unitData == null \|\| _unitData.Type != UnitType.FoxMagician) return;`(1151행 [확인]) | 주석 처리 → 「차지 유닛 캐시」 확인으로 대체(P2-1·P2-2) | 판별 기준만 「타입 이름」에서 「공격 클립에 `OnAttackCharge` 이벤트가 있는가」로 바뀐다. 지금 이 이벤트가 있는 클립은 Fox 하나뿐이라(Research 4-1 [확인]) **InfernoSpirit 클립에 이벤트를 넣기 전까지 동작은 같다** |
| **R-3** | `UnitView.cs` `ProcessAttackMarker` 멀티 분기의 lease 소비(7346~7349행 [확인]): `_attackPresentationSourceMarkerLease.TryConsume(...)`과 이어지는 `TryAcquirePresentationShadowMarkerScope(...)` | **차지 마커(`chargeOnly`)일 때만** 「확인 전용」 경로로 분기한다(P2-3). 타격 마커의 기존 소비 코드는 그대로 둔다. 차지 마커에서 소비하던 옛 동작은 분기 옆에 주석으로 남긴다 | 규칙 U-ATK-TIMELINE은 「단일 타격은 첫 marker가 `HitIndex`를 소비」한다고 정한다. 차지 신호는 `HitIndex` marker가 아니므로(서버 추출 대상 아님 — 규칙 17) 소비하지 않는 쪽이 규칙과 맞다. 🔴 **Plan 작성 중 추가 확인:** `TryAcquirePresentationShadowMarkerScope`(7774~7780행 [확인])는 **impact lease의 `TryConsume`을 호출한다.** 그래서 지금 차지 마커는 source lease에 더해 **impact lease도 1회분 소비한다.** Research 4-2는 source lease만 적었으므로 이 항목으로 보완한다 |
| **R-4** | `UnitView.cs` 공격 SFX `AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);`(7605행 [확인]) — **차지 유닛에 대해서만** | 차지 유닛이면 이 줄에서 재생하지 않는다(조건 추가). 비차지 유닛은 이 줄 그대로다. 차지 유닛 발사음은 트레이서 발사 옆으로 옮긴다(P2-4) | 사용자 결정 「발사음은 발사 순간」. 사운드 규칙 15의 「VFX와 SFX를 같은 호출 지점에서」는 발사 지점(트레이서 VFX + SFX)에서 지킨다. 차지 VFX 지점에는 「SFX 없음 — 차지 SFX는 추후」를 주석으로 남긴다(규칙 15 「하나만 재생하면 나머지를 주석으로 명시」) |
| **V-1** | `RunUnitActionSelfValidation.cs` Fox 이벤트 fixture 중 「Fox impact must preserve the charge lease count」(8095~8096행 [확인 — Research]) | 주석 보존 → 새 기준 검사로 대체(P5) | 새 기준에서 Fox 타격은 lease를 **소비해야** 한다. 옛 검사는 반대를 요구한다 |
| **V-2** | 같은 fixture의 `localHits == 0`(8113행 [확인 — Research]) | 주석 보존 → 「타격 마커가 트레이서 경로로 진입」 검사로 대체(P5) | Fox 타격이 이제 트레이서·로컬 착탄 신호 경로로 들어간다 |
| **V-3** | Fox tracer null 요구 「FoxMagician tracer is not implemented…」(7966~7967행 [확인 — Research]) | 주석 보존 → Inferno(8255~8284행 [전달])처럼 「연결됨」 검사로 대체(P5) | P1-4에서 Fox tracer·impact preset을 연결한다 |

- 위 표 외의 기존 동작은 지우지 않는다. 비차지 유닛 23종의 공격 VFX·SFX·트레이서 경로는 바뀌지 않는다.
- R-1~R-4의 행 번호는 programmer가 착수 때 다시 확인한다. 어긋나면 메서드 이름 기준으로 찾는다.

---

## 2. 사용자 결정 반영 (2026-10-19)

결정 원문은 Research 1절 「결정 기록」에 있다.

| # | 결정 | 이 Plan에서의 반영 |
|---|---|---|
| D1 | (가) Fox 데미지 숫자 즉시 표시 예외(규칙 19) **유지** | `UnitCombatUseCase`·`HitPresentationQueue`·규칙 19는 손대지 않는다. 숫자가 도착보다 먼저 뜨는 것은 6절 L-1 |
| D2 | (가) 차지 중 취소 → 차지 VFX 즉시 정지 + 투사체·발사음 **없음** | P2-4(source-only 타격에서 VFX·SFX·트레이서 없음) · P2-6(취소 경계 정지 헬퍼). 피해 숫자만 뜰 수 있는 것은 6절 L-2 |
| D3 | (나) .anim 이벤트 추가와 두 프리팹 길이 맞추기 모두 **game-programmer가 YAML로 수정**. 불가하면 사용자에게 보고 | P1-1~P1-3 |

이전 결정(Research 3절): 시점 Inferno 0.1667/1.5, Fox 1.0/2.8333 · 차지 길이는 프리팹으로 맞춤 · 발사음은 발사 순간 · 규칙 문서 2곳 새 기준으로 수정 승인 · `StatsReference.md` 기록 · Fox `UnitEffectConfig` tracer/impact 연결.

---

## 3. 구현 단계

순서: **P1 → P2·P3 → P4 → P5 → (사용자 테스트) → P6.** P1-1(클립 이벤트) 없이 P2만 하면 Inferno는 차지 유닛으로 인식되지 않는다. 이때 Inferno는 지금처럼 타격 때 차지 VFX를 재생한다(R-2 근거). 그래서 P1과 P2는 같은 작업 묶음으로 넘긴다.

### P1. 에셋 (game-programmer, YAML 직접 수정 — D3)

**P1-1. `Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim` — `OnAttackCharge` @ 0.16666667 추가**
- `m_Events`(93389행~ [확인])에는 지금 `OnAttackHit` @ 1.5 하나뿐이다. 그 **앞**에 새 항목을 넣는다. 이벤트는 시간 오름차순으로 둔다.
- 형식은 `FoxMagician_Attack.anim` 113276~113282행 [확인]을 그대로 따른다(`time` · `functionName: OnAttackCharge` · `data:` · `objectReferenceParameter: {fileID: 0}` · `floatParameter: 0` · `intParameter: 0` · `messageOptions: 0`). 들여쓰기는 기존 `OnAttackHit` 항목과 같게 맞춘다.
- 다른 행은 바꾸지 않는다. 클립 GUID·길이 3·30fps·루프·`OrientationOffsetY`가 그대로인지는 기존 검사(8197~8204행 [전달])로 확인한다.
- 서버 피해는 바뀌지 않는다. `UnitFactory`는 `OnAttackHit`만 추출한다(Research 4-5 [전달]). P5 검사에서 「Inferno 추출 타격 시간 = [1.5] 하나」를 단언해 확인한다.
- 근거 규칙: 규칙 17(타격 시간은 `OnAttackHit`에서만 추출) · 규칙 27(생산 clip에 타입별 marker authoring) · NET-PRESENT-001(Animation Event는 로컬 VFX·SFX 전용).

**P1-2. `Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_charge.prefab` — 길이 1 → 1.3333**
- 파티클 시스템 1개. `lengthInSec` 1(44행 [전달·Research 확인]) → **1.3333**. Start Lifetime 1(119행) → **1.3333**.
- burst 1회(979~983행)와 Size over Lifetime 0.22→1은 정규화 곡선이라 그대로 둔다. 다만 burst 시각이 0이 아니면 programmer가 보고한다.
- 목표: 0.1667초에 켜져서 1.5초 발사 순간에 크기가 최대가 되고 끝난다.
- 근거 규칙: 사용자 기준(Research 3절) · 규칙 31(다중 파티클 시스템 재생 — 이 프리팹은 단일이라 영향 없음).

**P1-3. `Assets/_Project/Prefabs/VFX/Units/vfx_foxmagician_charge.prefab` — 전체가 1.8333초 안에 끝나게**
- 루트: Duration 2(45행) → **1.8333**, Start Lifetime 2(120행) → **1.8333**. burst 2(984행) 시각이 Duration 안에 있는지 확인한다.
- 자식 `Wisps`: 지금 Duration 2(4957행)에 Lifetime 0.6~1.0(5031~5033행)이라 꼬리가 약 3초 남는다 [추정]. **Duration + 최대 Lifetime ≤ 1.8333**으로 맞춘다. 사용자가 든 예시는 Duration 0.8333, Lifetime 0.6~1.0이다.
- 시각 의도 「점차 차징」을 지킨다. rate-over-time 곡선 0.25→0.6→1.0(5786행~)은 Duration에 대한 정규화 곡선이다. Duration을 줄이면 같은 상승이 더 짧은 시간에 압축된다. **최종값은 programmer가 정하고, 그 값을 고른 이유를 작업 보고에 적는다**(사용자 지시). 예: 「Duration 0.8333 + Lifetime 1.0 = 1.8333, 곡선 유지」 또는 「startDelay 사용」 등.
- 루트 `stopAction` 2(Destroy)는 기존 동작이다. 풀 반환은 파티클이 모두 끝났을 때다(Research 4-7 [전달]). 길이를 줄이면 반환도 빨라진다.
- 근거 규칙: 사용자 기준 · 규칙 31(빈 루트가 아니라 루트에 파티클이 있는 프리팹 → `Play(true)`로 하위까지 재생, 변경 없음).

**P1-4. `Assets/_Project/Resources/Config/UnitEffectConfig.asset` — FoxMagician(unitType 21) 행(160~167행 [전달·Research 확인])**
- `tracerPreset`: `{fileID: 0}` → `EffectPreset_FoxMagician_Tracer`(GUID `025332764a8d09e4d97973e8c76a487e`).
- `impactPreset`: `{fileID: 0}` → `EffectPreset_FoxMagician_Impact`(GUID `bbc25dda207ccb84a9068c4a63392e73`).
- 참조 형식(`fileID`·`type`)은 **같은 파일에서 이미 연결된 다른 행(예: 68·71행 `{fileID: 11400000, guid: …, type: 2}` [확인])을 그대로 복사**한다. 형식을 추측하지 않는다.
- 근거 규칙: 규칙 20(원거리 유닛 트레이서) · 규칙 31 끝 문단(유닛 VFX는 `EffectPreset` → `UnitEffectConfig` 연결). 착탄 VFX가 도착 지점에서 터지는 것은 R5 임시 동작(사용자 승인 2026-10-19, `UnitView.cs` 7685~7688행 주석 [확인])을 그대로 쓴다.

### P2. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` (game-programmer)

**P2-1. 차지 유닛 판별 캐시 (Research 4-1 추천 (b))**
- `Initialize`에서 한 번만 계산한다. `_animator.runtimeAnimatorController.animationClips` 중 이름이 `_Attack`으로 끝나는 클립을 고르고, 그 클립의 이벤트에 `functionName == "OnAttackCharge"`가 있으면 `true`로 캐시한다(예: `_hasAttackChargeMarker`).
- 클립 선택 규칙은 809~814행 [확인]과 같게 한다. 서로 다른 `_Attack` 클립이 둘 이상이면 판별 불가로 보고 `false`로 둔다. 🔴 단 809~814행은 `CanUseNativeAttackPhase` 조건(806~808행 [확인]) **안**에 있고, 이 조건은 Fox를 뺀다. 그래서 그 메서드를 재사용하지 말고 **같은 선택 규칙을 그 조건 밖에서** 쓴다.
- `AnimationClip.events`는 호출할 때마다 배열을 새로 만든다 [추정 — Unity API 동작]. 매 프레임이나 매 마커에서 부르지 않고 `Initialize`에서만 부른다.
- 풀 재사용이나 재초기화 때 이전 값이 남지 않도록 `Initialize` 시작에서 값을 초기화한다.
- 근거 규칙: 규칙 17(타이밍의 단일 출처는 클립 이벤트) · NET-PRESENT-001.

**P2-2. `OnAttackCharge` 일반화**
- R-2 가드를 주석 처리하고 `if (_unitData == null || !_hasAttackChargeMarker) return;`로 바꾼다. 나머지(`_nativeAttackMarker` 설정 → `ProcessAttackMarker(chargeOnly: true)` → `finally`에서 비움)는 그대로 둔다.
- 메서드 위 요약 주석(1145~1148행 「Fox의 1초 charge는 기존 source 허가로 VFX/SFX만 재생한다」)을 새 기준으로 고친다. 새 기준: 차지 유닛 공통 · 허가 확인만 · VFX만, SFX 없음.

**P2-3. lease 「확인 전용」 (R-3)**
- 멀티 분기(`networkActive && !separateHeal`)에서 `chargeOnly`이면 다음을 **모두 소비 없이 확인**한다. 하나라도 거짓이면 차지 VFX를 내지 않고 종료한다.
  1. source lease: `IsArmed` · `RemainingMarkerCount > 0` · `!IsSourceOnly`(취소가 이미 도착했으면 차지를 시작하지 않는다 — D2).
  2. 회차 일치: `PresentationRevision == _lastAttackPresentationRevision`(또는 같은 의미의 현재 회차 값 — programmer가 정확한 비교 대상을 확인한다).
  3. **회차당 1회 플래그:** 같은 회차에서 차지를 이미 냈으면 다시 내지 않는다. 예: `_lastChargePresentedRevision`. 클립이 루프하는 동안 lease가 아직 소비되지 않았으면 다음 루프의 차지 신호가 다시 통과할 수 있기 때문이다.
  4. impact lease(R-3에서 확인한 두 번째 소비 지점): `IsArmed`만으로 충분한지, 남은 `HitIndex` 확인이 필요한지 programmer가 판단한다. 필요하면 P3 API를 쓴다.
- `markerEmission`은 `ResolveNetworkAttackMarkerEmission(...)`에 확인 결과를 넣어 같은 방식으로 정한다. LegacyFallback(`authoritativePresentation == false`)에서 이 함수가 어떤 값을 내는지 programmer가 확인한다 [미확인].
- 싱글(lease 미사용 — Research 4-2 [전달]) 경로는 바꾸지 않는다. 다만 루프 중 같은 공격에서 차지가 두 번 나오지 않는지는 테스트에서 본다(7절 S-1·S-2).
- 타격 마커(`!chargeOnly`)는 지금처럼 source lease와 impact lease를 소비한다.
- 근거 규칙: U-ATK-TIMELINE(타격 허가는 `HitIndex` 개수만큼, 루프 중 다음 회차 전 marker는 VFX를 만들지 않는다, 커밋 전 marker는 소비하지 않는다) · NET-PRESENT-001 · 규칙 17.

**P2-4. 차지 유닛 타격 마커: 차지 재생 생략 + 발사 순간 SFX (R-4)**
- 차지 유닛의 타격 마커에서는 7519~7523행 `effectManager.PlayUnitAttack(...)`을 **호출하지 않는다**(차지 VFX를 다시 켜지 않음). 생략 지점에 이유를 주석으로 적는다.
  - 그 사이에 있는 진단(`RecordInfernoAttackVfxAttempt`, Pistoleer 진단 등)은 「차지 유닛 타격 = attack VFX 생략」을 구분할 수 있게 기록한다. 기록 방식은 programmer가 정하되, 실패로 기록하지 않는다.
- 7605행 SFX는 차지 유닛이면 재생하지 않는다(R-4).
  - **차지 마커**에서는 「SFX 없음 — 차지 SFX는 추후 SFX 작업」을 주석으로 남긴다.
  - **타격 마커**에서는 트레이서 발사 `em.PlayTracer(...)`(7669행 부근) **바로 아래 줄**에 `AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);`를 둔다. 주석은 「규칙 15 — 트레이서 VFX와 짝」으로 한다.
  - 두 유닛 다 공격 사거리가 1.0 이상이라(Inferno 4, Fox 3 — `StatsReference.md` 71·83행 [확인], `RangedAttackThreshold = 1.0f` 132행 [확인]) 트레이서 분기로 들어간다. `em == null`처럼 트레이서를 못 쏘는 경우 SFX를 어떻게 할지는 programmer가 정하고 보고한다.
- **source-only 타격**(일반 Stop이 타격보다 먼저 도착 — 7607~7612행 주석 [확인]): 차지 유닛이면 VFX·SFX·트레이서를 **모두 내지 않는다**(D2). 비차지 유닛은 지금처럼 발사 VFX/SFX까지만 낸다.
- 근거 규칙: 사운드 규칙 15(VFX+SFX 쌍 호출) · 사운드 규칙 14(SFX 로컬 재생) · 규칙 20(`OnAttackHit` 시점 트레이서) · 사용자 결정 D2.

**P2-5. Fox 진단 관측 기록을 `chargeOnly`일 때로 한정**
- `UnitAttackShadowObserver`의 Fox 기록 3종(1536~1539·1574~1577·1600~1603행 [전달])은 이제 Fox 타격 마커도 같은 경로를 지나므로 두 번 기록될 수 있다. 그래서 `chargeOnly`일 때만 남긴다. R-1에서 살린 `RecordFoxMagicianNativeImpactMarker`는 타격 기록이라 그대로 둔다.
- 호출 위치(`UnitView.cs` 쪽 호출부인지 관측기 내부인지)는 programmer가 확인한다. 진단 전용(`#if`)이며 동작에는 영향이 없다.

**P2-6. 취소 경계 「차지 정지」 헬퍼 (D2)**
- `UnitView`에 private 헬퍼를 하나 둔다(예: `StopOwnedChargeEffects(string reason)`). 차지 유닛일 때만 `EffectManager.Instance?.StopCasterOwnedAttackEffects(this, reason)`(283행 [확인])을 부른다.
- 호출 후보(Research 4-4 [전달]):
  - `ClearCombatTargetTracking`(8032행 [확인]). `preserveCommittedSourceMarker` 인자와 관계없이 부른다.
  - `PreserveCommittedSourceMarkerAndCloseTargetImpact`.
  - `RetireAndCloseAttackPresentationScope`.
  - 싱글 `ChangeTarget`(8536행~ [확인]). retire를 부르지 않으므로 직접 부른다.
- 🔴 **각 호출 지점이 「진짜 취소」일 때만 오는지 확인한다** [미확인]. 예를 들어 `EndServerActionRotation`(8180)이나 `Initialize`(1634)에서 오는 `ClearCombatTargetTracking`이 정상 공격 중(차지~타격 사이)에도 불린다면, 거기서 정지하면 정상 차지가 잘린다. programmer가 호출 지점별로 판단하고 보고한다.
- 정지 대상은 그 유닛이 소유한 attack preset 이펙트뿐이다. 트레이서·착탄·화상 이펙트는 정지 대상이 아니다(`GameSystemRules_Units.md` 「특수 공격 시스템 규칙」의 2026-10-13 「Stop When Caster Dies」 덧붙임 [확인]). 그래서 이미 날아가는 투사체는 끊기지 않는다.
- **표현 전용이다.** lease·피해·결과를 바꾸지 않는다.
- 근거 규칙: 사용자 결정 D2 · 「Stop When Caster Dies」 덧붙임(표현 전용, 서버 확정 결과를 취소하지 않음) · NET-AUTH-001 · NET-CANCEL-005(이미 생성된 결과는 취소가 무효화하지 않는다).

### P3. `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` — 확인 전용 API (필요할 때만)

- source lease(`AttackPresentationSourceMarkerLease`, 620행~ [확인])에는 이미 public 읽기 속성이 있다: `IsArmed` · `PresentationRevision` · `RemainingMarkerCount` · `IsSourceOnly`(629~632행 [확인]). **source 쪽은 새 API가 필요 없을 가능성이 높다.**
- impact lease(`AttackPresentationImpactLease`, 512행~ [확인])는 `IsArmed`와 `TryGetSequenceScope`만 공개하고 다음 `HitIndex`는 private이다. P2-3 4번에서 남은 `HitIndex` 확인이 필요하다고 판단되면 **상태를 바꾸지 않는** 읽기 전용 메서드를 하나 추가한다(예: `CanConsume` 또는 `TryPeek(out scope)`).
- Application 계층이며 `Unity.Netcode`를 참조하지 않는다(아키텍처 제약). 기존 `TryConsume` 동작은 바꾸지 않는다.
- 근거 규칙: U-ATK-TIMELINE.

### P4. 주석 갱신 (동작 변화 없음)

| 파일 | 위치 | 내용 |
|---|---|---|
| `Assets/_Project/Scripts/Presentation/Unit/AnimationEventRelay.cs` | 40행 `OnAttackCharge` 부근 [확인] (Research는 39행) | Fox 전용 설명 → 차지 유닛 공통 |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.CasterOwned.cs` | 5~9행 [전달] | 정지 경로에 「공격 취소」 추가 |
| `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` | 259행 [전달] | 같음 |
| `EffectPreset.cs` | 59행 [전달] | 같음 |
| `UnitView.cs` | 1145~1148 · 7232~7233 · 7607~7611행 [확인] | P2 반영 |

- 근거 규칙: CLAUDE.md 규칙 8(상세 주석).

### P5. 자동 검증 갱신 (`Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs`, 메뉴 `Hexiege/Combat/Run Full Regression Suite` 955행 [확인])

| # | 검사 | 처리 |
|---|---|---|
| T-1 | Fox 이벤트 fixture(8052~8122행) | V-1·V-2를 주석 보존한다. 새 단언: 차지는 source·impact lease를 **소비하지 않음** · 같은 회차 두 번째 차지는 VFX 없음 · 타격은 lease를 **소비**하고 트레이서 경로로 진입 · 타격에서 attack VFX(차지) 재생 없음 · source-only 타격은 VFX·SFX·트레이서 없음 |
| T-2 | Fox tracer null 요구(7966~7967행) | V-3를 주석 보존한다. Inferno(8255~8284행) 방식의 「tracer·impact preset 연결됨 + GUID 일치」로 바꾼다 |
| T-3 | Inferno 타임라인(8206~8216행) | `OnAttackCharge` @ 0.1667초 1개 검사를 추가한다(Fox 7906~7911행 패턴). 추출 타격 시간이 [1.5] 하나인지(차지 미포함)도 단언한다 |
| T-4 | 차지 클립 집합 | 「`_Attack` 클립에 `OnAttackCharge`가 있는 유닛 = {FoxMagician, InfernoSpirit}」과 두 유닛 attack preset `StopWhenCasterDies == true`를 단언한다(위험 7 대비) |
| T-5 | 차지 프리팹 길이 | 두 차지 프리팹의 「최대 Duration + 최대 Lifetime」이 각 차지 구간(1.3333 / 1.8333) 이하인지 단언한다. 허용 오차는 programmer가 정한다 |
| T-6 | 설명 주석(8125~8127 · 7811~7813행), C3 요약(1036·1038행) | 새 기준으로 갱신한다. C3 요약은 확인이 필요하다 [전달] |
| — | Fox 타임라인(7901~7921) · Fox 피해 경계(7606~7653·8040) · `ValidateCasterOwnedChargeVfx`(VfxConnections 87~202) | 변경 없음이 예상된다 [추정]. 실행으로 확인한다 |

- 근거 규칙: 규칙 27(`RunUnitActionSelfValidation` production 검증 사용).

### P6. 문서 (document-manager — **구현·사용자 테스트 후** 반영)

| 문서 | 위치 | 내용 |
|---|---|---|
| `GameSystemRules/GameSystemRules_Units.md` | 「전투 연출 동기화 규칙」 규칙 17 「FoxMagician typed marker 계약」 문단(320행 [확인]) | 차지 유닛 공통 계약으로 고친다: `OnAttackCharge` = lease 확인만·회차당 1회·차지 VFX만(SFX 없음) / `OnAttackHit` = lease 소비·트레이서 발사·발사음, 차지 VFX 재생 없음 / 취소 시 차지 즉시 정지·투사체·SFX 없음 / Inferno 0.1667·1.5, Fox 1.0·2.8333 / 서버 피해 추출은 `OnAttackHit`만(변경 없음). 규칙 번호는 유지한다. 옛 문장 처리는 그 문서의 이력 이관 방식을 따른다 |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | 「8. Animation Event와 표현」 NET-PRESENT-001 Fox 문단(309행 [확인]) | 같은 기준으로 고친다. 「damage callback에서 charge를 재생하지 않는다」는 유지한다 |
| `StatsReference.md` | 71행 InfernoSpirit [확인] · 49행 시간 설명 [확인] | 71행 「1.5」 → 「1.5 (피해·발사; charge 0.1667)」. 49행 설명에 차지 유닛 기준 추가. *(83행 Fox 「2.8333333 (피해; charge 1)」을 「피해·발사」로 맞추는 것은 일관성 제안이며 승인 범위에 없다 — 반영 전 사용자에게 확인한다)* |
| `Assets/VFXSFXList.md` | 49행 InfernoSpirit · 56행 FoxMagician · 35행 연결 요약 [확인] | Fox tracer·impact 연결 상태, 두 차지의 시작 시점·길이. 실기 수용은 행별로 따로 적는다 |
| 검토 후보 | `Assets/UnitCombatAssetMatrix.md` Fox·Inferno 행, `PROJECT_STATUS.md`, `ROADMAP.md`(옵션 C·SFX 후속), `WORK_HISTORY.md` | WORKFLOW [11]에 따라 검토하고, 바꿀 필요가 있으면 그때 보고한다 |

- 근거: Research 3절의 사용자 승인(규칙 문서 2곳·StatsReference). 규칙 번호는 바꾸지 않는다(WORKFLOW [11]).

---

## 4. 파일별 변경 요약

| 파일 | 단계 | 변경 | 주요 근거 규칙 |
|---|---|---|---|
| `Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim` | P1-1 | `OnAttackCharge` @ 0.16666667 추가 | 규칙 17 · 규칙 27 · NET-PRESENT-001 |
| `Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_charge.prefab` | P1-2 | Duration·Lifetime 1 → 1.3333 | 사용자 기준 |
| `Assets/_Project/Prefabs/VFX/Units/vfx_foxmagician_charge.prefab` | P1-3 | 루트 2 → 1.8333, Wisps ≤ 1.8333 | 사용자 기준 · 규칙 31 |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` | P1-4 | Fox tracer·impact preset 연결 | 규칙 20 · 규칙 31 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | P2·P4 | R-1~R-4, 캐시·확인 전용·생략·SFX 이동·관측 한정·정지 헬퍼 | 규칙 17 · 규칙 20 · U-ATK-TIMELINE · 사운드 규칙 14·15 · NET-AUTH-001 · NET-CANCEL-005 |
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` | P3 | (필요할 때만) 읽기 전용 메서드 | U-ATK-TIMELINE |
| `AnimationEventRelay.cs` · `VfxPoolItem.CasterOwned.cs` · `EffectManager.cs` · `EffectPreset.cs` | P4 | 주석만 | CLAUDE.md 규칙 8 |
| `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` (+ 선택: `.VfxConnections.cs`) | P5 | T-1~T-6 | 규칙 27 |
| 규칙 문서 2곳 · `StatsReference.md` · `VFXSFXList.md` | P6 | 구현·테스트 후 | 사용자 승인 |

**바꾸지 않는 것:** `UnitCombatUseCase.cs`(D1) · `HitPresentationQueue.cs` · `NetworkCombatController.cs` · `UnitFactory.cs` · 서버 피해·DoT·HP·RPC · 다른 23종 유닛 경로 · `FoxMagician_Attack.anim`(이미 기준과 같음 — Research 4-1 [확인]).

---

## 5. 아키텍처 제약 확인

- 모든 변경은 Presentation 계층(UnitView·에셋)과 Application 계층의 읽기 전용 메서드(P3)뿐이다. 새 `NetworkBehaviour`·RPC·NetworkVariable은 없다.
- Application 파일에서 `Unity.Netcode`·Infrastructure를 참조하지 않는다.
- 서버 권위(NET-AUTH-001): 로컬 Animation Event·VFX 위치는 서버 결과의 입력이 되지 않는다. 이번 변경은 이 경계를 넘지 않는다.

---

## 6. 위험 · 알려진 한계

**위험 (Research 6절 1~8 + Plan 작성 중 추가)**

| # | 위험 | 대응 |
|---|---|---|
| 1 | **lease 충돌** — 차지가 1회분을 소비하면 멀티에서 Inferno 투사체가 안 나간다. 싱글 테스트로는 안 드러난다 | P2-3 확인 전용. R-3대로 **impact lease 소비**(`TryAcquirePresentationShadowMarkerScope`)도 차지에서 뺀다. 멀티 테스트 M-1·M-2 필수 |
| 2 | **Client 지연** — Inferno 차지(0.1667초 여유)가 가끔 빠질 수 있다 [추정] | NET-PRESENT-001이 허용하는 결과로 보인다. 실기에서 빈도를 기록한다(M-3) |
| 3 | **Fox 숫자 선행** — 숫자가 투사체 도착보다 먼저 뜬다 | 알려진 한계 L-1(D1) |
| 4 | **취소 후 피해** — 차지·투사체 없이 피해 숫자만 뜰 수 있다 | 알려진 한계 L-2(D2) |
| 5 | **Fox Wisps 꼬리 약 3초** [추정] | P1-3 · T-5 |
| 6 | **진단 관측기 중복 기록** — Fox 기록 3종 | P2-5 |
| 7 | **이벤트 누락 시 조용한 회귀** — (b) 판별 방식에서 클립 이벤트를 빠뜨리면 일반 유닛처럼 동작한다 | T-4 |
| 8 | **R5 버그 B2~B4가 Fox에도** — 같은 Legacy tracer 경로라 이동 대상 고정 지점·대상 크기에 가려짐이 Fox에도 나타날 수 있다 | 이번 범위 밖(vfx-code-followups Research 15-5). 실기에서 보이면 기록만 한다 |
| 9 | (추가) **커밋이 차지 신호 뒤에 오는 경우** — 첫 진입·타겟 연계에서 클립을 되감지 않는다(U-ATK-TIMELINE). 그래서 커밋이 0.1667초(Fox 1.0초) 뒤에 확정되면 그 회차는 **차지 없이 발사**될 수 있다 [추정] | 규칙상 정상 결과다(커밋 전 marker는 소비·승인하지 않음). 실기에서 보이면 기록한다 |
| 10 | (추가) **정지 헬퍼 오발** — 정상 공격 중 불리는 경계에서 차지를 지울 수 있다 | P2-6 호출 지점별 확인 · S-1·S-2에서 「정상 공격 때 차지가 끝까지 보이는가」 |
| 11 | (추가) **.anim·prefab YAML 직접 편집**(D3) — 형식 오류 시 Unity가 에셋을 못 읽거나 값을 버릴 수 있다 [추정] | 기존 항목 형식을 복사하고, 편집 뒤 Unity 재임포트 오류 0·T-3·T-5 통과를 확인한다. 불가하면 사용자에게 보고한다(D3) |

**알려진 한계 (이번 범위 밖 — 옵션 C에서 해소 예정)**

- **L-1 (D1):** FoxMagician 피해 숫자는 서버 결과가 오면 즉시 뜬다(규칙 19 예외 유지). 그래서 투사체가 닿기 **전에** 숫자가 먼저 보일 수 있다(최대 약 0.6초 [추정] — Research 4-3). 투사체 도착 신호는 빈 큐에서 버려진다(`HitPresentationQueue.cs` 565~570행 주석 [확인 — Research]).
- **L-2 (D2):** 차지 도중 취소돼도 서버 피해는 이미 예약돼 있다. 공격자 사망 때만 취소된다(Research 4-4 [전달]). 그래서 **차지 VFX·투사체·소리 없이 피해 숫자만 뜰 수 있다.**
- InfernoSpirit 화상(DoT)은 서버 피해 시점에 따라 시작되며 투사체 도착과는 맞추지 않는다(변경 없음).

---

## 7. 테스트 절차

**자동 (programmer → 메인 세션 확인)**
- A-1. 컴파일 오류 0. `.anim`·prefab·asset 재임포트 오류·경고 0.
- A-2. `Hexiege/Combat/Run Full Regression Suite`(Edit Mode 회귀 스위트) 전체 통과. T-1~T-6 포함.

**싱글 Play (사용자)**
- S-1. InfernoSpirit: 공격 시작 약 0.17초에 차지가 켜지고 점점 커진다 → 약 1.5초에 화염구가 발사되며 발사음이 난다. 차지는 그 순간 끝난다 → 착탄 이펙트 → 대상에 화상 표시. 여러 번 공격하는 동안 한 공격에 차지가 두 번 나오지 않는다.
- S-2. FoxMagician: 1.0초에 차지 → 2.8333초에 마법탄 발사·발사음 → 도착 지점 착탄 이펙트(새로 연결한 tracer·impact). 차지가 발사 뒤에 남지 않는다. 숫자가 먼저 뜨는 것은 L-1이라 FAIL이 아니다.
- S-3. 차지 도중(발사 전) **대상이 죽거나 다른 곳으로 이동해 공격이 끊기면**: 차지 이펙트가 그 자리에서 즉시 사라지고, 투사체와 발사음이 **없다.** 피해 숫자만 뜨는 것은 L-2라 FAIL이 아니다.
- S-4. 시전자가 차지 중 죽으면 차지가 즉시 사라진다(기존 PASS 동작의 회귀 확인).
- S-5. 다른 원거리 유닛(예: Pistoleer·MushroomBomber) 공격 VFX·SFX·트레이서가 전과 같다(회귀).

**멀티 Host/Client (사용자) — 🔴 lease 문제는 여기서만 드러난다**
- M-1. Host 화면과 Client 화면 모두에서 InfernoSpirit이 매 공격 화염구를 발사한다(투사체 누락 없음).
- M-2. 양쪽 화면 모두에서 FoxMagician이 매 공격 마법탄을 발사한다.
- M-3. Client 화면에서 Inferno 차지가 빠지는 빈도를 기록한다(위험 2 — 가끔이면 허용 범위로 보이지만 사용자가 판정).
- M-4. 양쪽에서 S-3(차지 중 취소)을 1회 이상 확인한다.

판정은 사용자 실기 결과로 한다. 자동 통과만으로 시각 수용을 대신하지 않는다. Testcase.md는 사용자 지시가 있을 때만 만든다(WORKFLOW [5-1]).

---

## 8. 계획 변경 이력

| 날짜 | 변경 | 이유 |
|---|---|---|
| 2026-10-19 | 최초 작성 | Research D1~D3 사용자 결정(2026-10-19) 반영. 작성 중 확인한 사항을 함께 적었다: 차지 마커가 source lease에 더해 impact lease도 소비한다(R-3, `UnitView.cs` 7348·7779행 [확인]). 위험 9~11을 추가했다 |
| 2026-10-19 | 구현 결과·사용자 확인·P6 문서 반영 기록 (9절 추가) | 구현 `8330b5d9`, 추가 재질 수정 `f899238f`·`609ba447`, 사용자 싱글·멀티 시각 확인. 위 1~8절 계획 본문은 고치지 않았다 — 달라진 점은 9-2 |

---

<a id="9-구현-결과-2026-10-19-추가"></a>

## 9. 구현 결과 (2026-10-19 추가)

**쉬운 설명.** 계획한 P1~P5를 한 커밋(`8330b5d9`)으로 구현했다. 지옥불 정령은 이제 0.1667초에 차지를 시작해 1.5초에 화염구를 쏘고, 여우 마법사는 1.0초에 차지를 시작해 2.8333초에 마법탄을 쏜다. 발사음은 발사 순간에 나고, 차지는 발사 순간에 스스로 끝나며, 공격이 끊기면 차지가 바로 사라진다. 사용자가 싱글·멀티에서 눈으로 확인했다. 확인 중 지옥불 정령의 불티가 초록빛으로 보여 재질만 바꿨다(9-3). 서버 피해·HP·RPC는 바뀌지 않았다.

근거 표기: [직접] = document-manager가 HEAD `609ba447`에서 커밋 diff·파일을 읽어 확인 · [전달] = 메인 세션 인계 · [사용자] = 사용자 보고. 사용자 결과와 로그·회귀 스위트의 범위는 [Research 9-1](Research.md#9-implementation-result)이 단일 소스다.

### 9-1. 실행 기록

| 단계 | 상태 | 비고 |
|---|---|---|
| P1-1 Inferno 클립 이벤트 | ✅ 구현 | `OnAttackCharge` @ 0.16666667 [직접] |
| P1-2 Inferno 차지 프리팹 | ✅ 구현 | 1 → 1.3333334 (Duration·Lifetime) [직접] |
| P1-3 Fox 차지 프리팹 | ✅ 구현 | 루트 2 → 1.8333334, Wisps Duration 2 → 0.8333333(Lifetime 0.6~1.0 유지) [직접] |
| P1-4 Fox tracer·impact 연결 | ✅ 구현 | 기존 행과 같은 `{fileID: 11400000, guid: …, type: 2}` 형식 [직접] |
| P2-1~P2-6 `UnitView.cs` | ✅ 구현 | 달라진 점 9-2 |
| P3 확인 전용 API | ⬜ **불필요로 판단 — 만들지 않음** | 9-2 #3 |
| P4 주석 | ✅ 구현 | 4개 파일 주석 + `UnitView.cs` |
| P5 자동 검증 T-1~T-6 | ✅ 구현 | 실행 결과: **미보고**(Research 9-1) |
| (추가) Inferno 불티 재질 | ✅ 구현 · 사용자 확인 | 9-3 |
| P6 문서 | ✅ 반영 | 9-4 |
| 1절 「주석 처리분 최종 삭제」 | ⬜ **미실행 → C 작업 후로 이연** | 사용자 지시가 없었다. R-1~R-4·V-1~V-3 주석 블록은 코드에 남아 있다 [직접]. *(2026-10-19 사용자 결정: 「C 방식 작업 후 옛코드 정리하도록하자」 — 서버 권위 투사체 작업 [Plan 최상단 RM 항목](../13_30_server-authoritative-projectile/Plan.md)에서 사용자 테스트 PASS 후 삭제)* |

<a id="9-2-plan과-달라진-점"></a>

### 9-2. Plan과 달라진 점 [직접 — diff, 이유는 코드 주석에서 옮김]

| # | Plan | 실제 | 이유 |
|---|---|---|---|
| 1 | P2-6: 정지 헬퍼를 4곳(`ClearCombatTargetTracking` · `PreserveCommittedSourceMarkerAndCloseTargetImpact` · `RetireAndCloseAttackPresentationScope` · 싱글 `ChangeTarget`)에서 부름 | **2곳**으로 통합: `RetireAndCloseAttackPresentationScope`(사유가 `presentation-revision-replaced`이면 **끄지 않음**)와 `PreserveCommittedSourceMarkerAndCloseTargetImpact`. `ClearCombatTargetTracking`은 이 두 함수를 부르므로 따로 넣지 않았다. **싱글 `ChangeTarget`에서는 끄지 않는다** | `presentation-revision-replaced`는 매 공격 회차의 정상 커밋 경계라, 여기서 끄면 정상 차지가 잘린다(Plan 위험 10). 싱글의 타겟 변경 이벤트는 「새 공격을 시작하는 순간」에 발행되고 싱글은 lease를 쓰지 않아 다음 타격이 새 타겟으로 그대로 발사하므로 진짜 취소가 아니다 |
| 2 | T-5: 「최대 Duration + 최대 Lifetime ≤ 차지 구간」 | 파티클 시스템별 **실제 끝 시각** = (시작 지연 + 마지막 방출 시각 + 최대 수명) ÷ 재생 속도. 마지막 방출 시각은 초당/거리당 방출이 있으면 Duration 끝, burst만 있으면 마지막 burst 시각. 그 최댓값이 차지 구간 이하이면서 **구간 끝 1프레임(1/30초) 안까지 이어지는지**도 확인. 루프·무한 burst·곡선 수명이면 검사 실패로 알림 | Plan 문구는 연속 방출 시스템의 보수적 상한이다. 두 루트는 시작 순간 burst만 써서 실제 끝 = Lifetime이다 |
| 3 | P3: impact lease의 남은 `HitIndex` 확인이 필요하면 읽기 전용 API 추가 | 추가하지 않음. `IsArmed`만 확인 | `TryConsume`은 Armed일 때만 성공하고, Armed 동안 남은 `HitIndex`는 항상 1개 이상이다(마지막을 쓰는 순간 Consumed로 바뀐다). 현재 두 차지 유닛(LegacyFallback)은 impact lease를 장전하지 않으므로 이 값은 false다 |
| 4 | V-1~V-3만 주석 보존 | 같은 Fox fixture의 옛 기준 단언 몇 개(lease 남은 횟수 불변·emission scope 불변 등)도 **주석으로 보존**하고 새 기준 단언으로 대체 | 새 기준(타격은 lease 소비·투사체 발사)과 반대를 요구하는 단언이다 |
| 5 | P2-1: `Initialize`에서만 판별 계산 | `Initialize`에서 계산하되, 그때 Animator가 없으면 **첫 Animation Event 때 한 번** 계산(`EnsureAttackChargeMarkerCache`) | Animator가 없으면 판별을 못 하기 때문. 유닛 수명당 1회는 그대로 |
| 6 | T-1: Fox 이벤트 fixture 갱신 | 갱신 + `RunUnitActionSelfValidation.VfxConnections.cs`에 실제 EffectManager로 「차지→발사·같은 회차 두 번째 차지·차지 중 허가 차단·SourceOnly Stop·싱글」을 보는 fixture(`ValidateChargeUnitLaunchTiming`) 추가. 발사음은 AudioManager를 격리해 관측하지 않음 | Plan 4절의 「선택: VfxConnections」를 실행 |
| 7 | (Plan 범위 밖) | Inferno tracer `Ember_Trail`·impact `Embers` 재질 교체(9-3) | 사용자 시각 확인 중 발견, 사용자 지시 |

### 9-3. 추가 수정 — Inferno 불티 초록빛

사실·원인·FoxMagician에 남은 같은 재질은 [Research 9-3](Research.md#9-3-추가-수정--inferno-불티-초록빛-커밋-f899238f609ba447)이 단일 소스다. 요약: `Mat_VFX_SharpSpark`(청록 RuneSpire 텍스처) → `Mat_VFX_GlowAdditive`(흰색 텍스처). FoxMagician `Sparkles` 2개 레이어는 사용자 지시 범위 밖이라 그대로다.

<a id="9-4-상시-문서-반영과-개정-전-원문"></a>

### 9-4. 상시 문서 반영과 개정 전 원문

| 문서 | 반영 |
|---|---|
| `GameSystemRules/GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 규칙 17 | 「FoxMagician typed marker 계약」 문단을 **「차지 유닛 typed marker 계약」**으로 교체(규칙 번호 유지). 규칙 19는 유지 |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` 「8. Animation Event와 표현」 NET-PRESENT-001 | Fox 문단을 차지 유닛 공통 문단으로 교체 |
| `StatsReference.md` | 49행 시간 설명 · 71행 Inferno 「1.5 (피해·발사; charge 0.1667)」 · 83행 Fox 「2.8333333 (피해·발사; charge 1)」(사용자 승인) · 100행 「생산 타임라인 구현 근거」의 charge 설명(같은 옛 주장 「source VFX/SFX」가 있어 함께 고침) |
| `Assets/VFXSFXList.md` | 35행 연결 요약 · 49행 InfernoSpirit · 56행 FoxMagician |
| `Assets/VFXSFXGuide.md` 「코드 계약」 | 6번(공용 재질의 텍스처 색) · 7번(차지 프리팹 길이) 추가 |
| `Assets/UnitCombatAssetMatrix.md` | 「Fox 현행 복원」 문단 · InfernoSpirit·FoxMagician 행 |
| `PROJECT_STATUS.md` · `ROADMAP.md` · `WORK_HISTORY.md` | VFX 표현 후속 행 · C 방식 행의 18-6 덧붙임 상태 · 마일스톤 1행 |
| [vfx-code-followups Research 15-6](../../2026-10-13/13_53_vfx-code-followups/Research.md#15-6-charge-unit-fire-timing) · [Plan 18-6](../../2026-10-13/13_53_vfx-code-followups/Plan.md#18-6-charge-unit-fire-timing) | 머리 상태 표시와 CH-1~CH-3을 이 작업으로 완료 표시(제자리 수정 + 날짜 조각) |

**개정 전 원문 (2026-10-19 교체 직전, HEAD `609ba447` — 글자 그대로 보존):**

`GameSystemRules_Units.md` 320행:

````markdown
**FoxMagician typed marker 계약:** `OnAttackCharge @ 1초`는 기존 lease·생명주기 gate를 거쳐 source VFX/SFX만 방출하고 recipient observation·LocalAttackHit·tracer·HP 경로 전에 종료한다. `OnAttackHit @ 85/30초`는 피해 의도 marker로, 로컬 callback은 읽기 전용 metadata만 기록하고 charge lease 소비나 VFX 재생을 하지 않는다. SOLO/server/client 생성은 실제 OnAttackHit만 권위 피해 offset으로 추출하며 charge는 제외한다. type21 fallback은2.8333333초다. 실제 피해는 기존 권위 timer/writer가 소유한다. 이는 사용자 원래 `2:25@30fps = 85/30초`이며2.25초는 잘못된 환산이다. Unresolved/LegacyFallback·기존 writer/Immediate bool·range·다른 타입 의미는 유지한다.
````

`GameSystemRules_UnitCombatSynchronization.md` 309행:

````markdown
FoxMagician의 typed source charge·damage marker와 추출·HP 경계는 `GameSystemRules_Units.md` 규칙17을 따른다. `OnAttackCharge @ 1초`와 `OnAttackHit @ 85/30초`를 구분하며 charge를 피해 offset으로 추출하거나 damage callback에서 charge를 소비·재생하지 않는다. Unresolved/LegacyFallback·Immediate intent·기존 권위 writer를 유지한다. 원래 `2:25@30fps`의 복원이며 이전2.25초 환산은 현행 값이 아니다. source8 manifest·controlled compile·일반23종 감사 및 native/멀티/시각 미실행 경계 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-05), 현재 개별 수용은 [감사표](../Assets/UnitCombatAssetMatrix.md)를 따른다. 이 한정 결과를 전체 SOLO 완료로 확대하지 않는다.
````

`StatsReference.md` 49행 중 해당 문장: 「FoxMagician은 피해 `OnAttackHit @ 85/30초`와 charge `OnAttackCharge @ 1초`를 구분하며 로컬 이벤트가 HP를 적용하지 않는다.」 · 71행 시간 칸 「1.5」 · 83행 시간 칸 「2.8333333 (피해; charge 1)」 · 100행 해당 문장: 「별도 `OnAttackCharge @ 1초`는 source VFX/SFX만 시작하고 피해 offset에 포함하지 않는다.」

(2026-10-14 계약의 더 긴 원문은 이미 [History 「규칙 17 유닛별 타임라인 변경·Fox receipt」](../../../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-02)에 있다.)

### 9-5. 알려진 한계

[Research 9-4](Research.md#9-4-알려진-한계-유지)가 단일 소스다(Fox 숫자 선행 L-1 · 취소 후 숫자만 L-2 · Client Inferno 차지 간헐 누락 [추정] · Fox Wisps 입자 수 감소 · Fox `Sparkles` 청록 재질 · 차지 SFX 없음).

<a id="9-6-변경-파일-리스트업-workflow-12"></a>

### 9-6. 변경 파일 리스트업 (WORKFLOW [12])

[직접 — 커밋 `e2b12593`·`8330b5d9`·`7da90f92`·`f899238f`·`609ba447`의 `git show --name-status` + 이번 문서 반영(미커밋)]

```
[수정 — 코드]  (8330b5d9)
- Assets/_Project/Scripts/Presentation/Unit/UnitView.cs
- Assets/_Project/Scripts/Presentation/Unit/AnimationEventRelay.cs          (주석)
- Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs            (주석)
- Assets/_Project/Scripts/Presentation/Effects/EffectPreset.cs             (주석)
- Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.CasterOwned.cs  (주석)
- Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs
- Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs

[수정 — 에셋]
- Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim   (8330b5d9)
- Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_charge.prefab          (8330b5d9)
- Assets/_Project/Prefabs/VFX/Units/vfx_foxmagician_charge.prefab            (8330b5d9)
- Assets/_Project/Resources/Config/UnitEffectConfig.asset                    (8330b5d9)
- Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_impact.prefab          (f899238f)
- Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_tracer.prefab          (609ba447)

[수정 — 상시 문서]  (이번 반영, 미커밋)
- Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md
- Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md
- Assets/_Project/Docs/StatsReference.md
- Assets/_Project/Docs/Assets/VFXSFXList.md
- Assets/_Project/Docs/Assets/VFXSFXGuide.md
- Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md
- Assets/_Project/Docs/PROJECT_STATUS.md
- Assets/_Project/Docs/ROADMAP.md
- Assets/_Project/Docs/WORK_HISTORY.md

[수정 — 에이전트 메모리]
- .claude/agent-memory/document-manager/MEMORY.md     (e2b12593 · 이번 반영)
- .claude/agent-memory/_baseline.json                 (e2b12593 · 이번 반영 — --update-baseline)
- .claude/MEMORY.md                                   (이번 반영)
- .claude/agent-memory/game-programmer/vfx-presentation.md   (이번 반영)
- .claude/agent-memory/game-programmer/MEMORY.md      (이번 반영 — 인덱스 설명)
- .claude/agent-memory/asset-prompt-crafter/MEMORY.md (이번 반영)

[추가 — 작업 문서]
- Assets/_Project/Docs/_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Research.md   (e2b12593, 이번 9절)
- Assets/_Project/Docs/_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Plan.md       (e2b12593, 이번 9절)
- Assets/_Project/Docs/_Tasks/2026-10-19/11_09_charge-unit-launch-timing.meta          (7da90f92, 사용자)
- Assets/_Project/Docs/_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Plan.md.meta  (7da90f92, 사용자)
- Assets/_Project/Docs/_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Research.md.meta (7da90f92, 사용자)
- Assets/_Project/Docs/_Logs/2026-10-19.meta                                           (7da90f92, 사용자)
- Assets/_Project/Docs/_Logs/2026-10-19/21_43_logcat.meta                              (7da90f92, 사용자)
- Assets/_Project/Docs/_Logs/2026-10-19/21_43_logcat/RuntimeLog_device.txt.meta        (7da90f92, 사용자 — 로그 본문은 .gitignore로 저장소 밖)

[수정 — 작업 문서]
- Assets/_Project/Docs/_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md   (15-6 완료 표시)
- Assets/_Project/Docs/_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md       (18-6 완료 표시)
```
