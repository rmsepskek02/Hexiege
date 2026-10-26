# Research — 상태 이펙트(화상) 몸통 폭 맞춤

## 이 문서는 무엇인가

지옥불 정령(InfernoSpirit)에게 맞은 유닛 몸에는 "불타고 있다"는 표시로 불꽃 이펙트가 붙는다. 그런데 곰전사(BearGuard)·탱크·대포수레처럼 **몸이 옆으로 넓은 유닛**에서는 이 불꽃이 잘 보이지 않는다는 문제가 있다.

원인은 불꽃 크기를 정하는 방식에 있다. 지금은 불꽃 크기를 **유닛의 키**에만 맞춰 키우고 줄이며, 불꽃은 몸 한가운데 **한 점에서 한 장씩** 피어오른다. 그래서 키는 비슷한데 몸이 두세 배 넓은 유닛은 넓은 몸통 가운데에 좁은 불꽃 하나만 보이게 된다.

사용자가 고른 방향(추천안 「안 1」)은 이렇다. **불꽃 한 장의 크기는 지금처럼 키에 맞추고, 불꽃이 피어오르는 범위를 몸통 폭만큼 넓히고, 몸이 넓을수록 불꽃 개수를 늘린다.** 이렇게 하면 작은 유닛은 지금과 똑같이 보이고 넓은 유닛만 불꽃이 여러 장으로 몸 전체에 퍼진다.

이 문서는 그 작업 전에 현재 구조와 수치, 문제의 크기, 다른 대안과의 비교, 영향 범위를 정리한다. 구현 방법과 사용자 결정 항목은 같은 폴더의 [Plan.md](Plan.md)에 있다. 이 작업은 **화면 표현만 바꾸며** 피해량·지속시간·멀티플레이 동기화 같은 게임 규칙은 바꾸지 않는다.

**후속 단계 「D7 후속: 상태 이펙트를 몸 앞에 그리기」(2026-10-26 추가).** 폭 맞춤을 넣은 뒤 실기에서 곰전사 불꽃이 계산대로 2장 나왔지만, 사용자는 "씌워져 있는 게 보이긴 하는데 잘 안 보인다 — 몸 안에 가려져서 안 보이는 게 크다"고 확인했다. 불꽃은 몸 **한가운데**에 서 있는 얇은 판처럼 그려지기 때문에, 카메라 쪽에 있는 몸 앞부분이 불꽃의 상당 부분을 덮는다. 그래서 다음 단계로 **불꽃을 카메라 쪽으로 조금 당겨 몸 앞에 그리게** 한다. 이 게임의 카메라는 원근이 없는(직교) 카메라라서, 카메라 쪽으로 당겨도 불꽃이 화면에서 위나 옆으로 밀리지 않고 "앞에 그려지는지"만 달라진다. 실기 결과는 [9절](#9-실기-확인-2026-10-26-2105-로그), 조사와 대안 비교는 [10절](#10-d7-후속-상태-이펙트를-몸-앞에-그리기가려짐-해소), 구현 계획은 [Plan.md 8절](Plan.md#8-d7-후속-상태-이펙트를-몸-앞에-그리기가려짐-해소)이다.

**표기:** `[사실]` = 코드·에셋·문서를 직접 읽어 확인한 것(2026-10-26 기준) · `[계산]` = 사실 값으로 계산한 것 · `[추정]` = 확인하지 않은 판단 · `[제안]` = 문서 담당이 제안하는 것(결정 아님).

---

## 1. 사용자 결정이 필요한 것 (요약)

결정 항목의 전체 내용·선택지·추천은 [Plan.md 1절](Plan.md#1-사용자-결정-사항)이 단일 소스다. 여기서는 목록만 둔다.

*(2026-10-26 갱신 — 아래 D1~D6은 Plan의 추천안대로 커밋 `eb660f70`에 구현됐다 `[사실: git show --stat · 코드 주석 「사용자 결정 D1」「D2·D3·D4」]`. 사용자의 결정 문구 원문은 이 Task 문서에 기록되지 않았다 `[미확인]`.)*

- 독·힐에도 같은 구조를 넣을지(이번에 켜는 것은 화상뿐) — 추천 (a)대로 구현(화상·독·힐 재생 경로 모두 옵션 구조, 화상 프리셋만 켬)
- 불꽃 개수 상한과 개수 계산 방식 — 상한 3 · 반올림으로 구현
- 불꽃이 퍼지는 범위를 몸통 윤곽 안으로 둘지 — 윤곽 안으로 구현
- 프리팹을 고칠지, 코드가 재생할 때만 바꿀지 — 프리팹 무변경, 코드가 재생 때 설정
- 자체 검증 항목을 추가할지 — 추가 구현(메뉴 실행 결과 미보고)
- 몸 앞뒤 가려짐이 남을 때 후속으로 돌릴지 — **실기에서 남음 확인(9절) → 후속 단계 착수 승인(2026-10-26).** 후속 단계의 결정 항목은 [Plan.md 8-2절](Plan.md#8-2-사용자-결정-사항-d7-후속)

---

## 2. 현재 구조

### 2-1. 상태 이펙트 크기가 정해지는 경로 `[사실]`

화상·독은 `EffectManager.StartTimedDamageEffect` → `VfxPoolItem.PlayTimedDamage`(`Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs`)로, 힐은 `EffectManager.StartBloomEffect` → `VfxPoolItem.PlayBloomLoop`(`VfxPoolItem.cs`)로 재생된다. 두 경로 모두 크기는 같은 공식이다.

> **최종 크기 = 프리팹 원본 크기 × 프리셋 `Scale Multiplier` × 대상 유닛 맞춤 배율**

- **대상 유닛 맞춤 배율**은 `UnitEffectConfig.TryGetRecipientFit`(`UnitEffectConfig.cs`)이 정한다. 표의 `manualScale`이 양수면 그 값, 아니면 **키 ÷ 1.0**(`RecipientReferenceHeight = 1.0`)이다. 결과는 `RecipientFit.UniformScale`로 0.1~10 사이로 제한된다.
- `RecipientReferenceRadius = 0.25`도 있으나 주석대로 **참고용**이며 배율 계산에 쓰이지 않는다. 표의 `radius` 칸 Tooltip도 "현재 배율 계산에는 쓰지 않고 참고·튜닝용"이라고 적고 있다.
- `RecipientFit` 구조체가 들고 다니는 값은 **배율(`Scale`)과 발밑 보정(`FootOffset`) 두 가지뿐**이다. 몸통 반경은 재생 코드까지 전달되지 않는다.
- 이 "한 배율(균등)" 방식은 2026-10-13 사용자 결정 **D-3**이다([당시 Plan](../../2026-10-13/13_53_vfx-code-followups/Plan.md) 2절). 근거: 프리팹이 Hierarchy 스케일링이라 가로·세로를 다르게 키우면 파티클이 납작/길쭉해질 수 있고, 사용자가 승인한 화상 모양을 보존하기 위해서였다.
- 맞춤 배율은 **재생 시작 때 1번만** 적용된다. 같은 대상의 갱신(refresh)은 재생을 다시 시작하지 않으므로 크기도 유지된다(`PlayTimedDamage` 요약 주석, `RefreshTimedDamage`).
- 재생 로그에는 `FitScale=` 값이 남는다(`channel.Record("Play", …)`).

현재 프리셋 배율 `[사실]`:

| 프리셋 | 연결된 프리팹 | `Scale Multiplier` | 위치 보정 |
|---|---|---|---|
| `EffectPreset_InfernoSpirit_Burn` | `vfx_infernospirit_burn.prefab` | 1, 1, 1 | 0 |
| `EffectPreset_MushroomBomber_Poison` | `vfx_mushroombomber_poison.prefab` | 2, 2, 2 | 0 |
| `EffectPreset_BloomFairy_Heal` | `vfx_bloomfairy_heal.prefab` | 2, 2, 2 | 0 |

(경로: `Assets/_Project/Resources/Config/EffectPresets/`)

### 2-2. 유닛 크기 표 `[사실]`

`Assets/_Project/Resources/Config/UnitEffectConfig.asset`의 `_recipientFits`에 25종 모두 줄이 있다(`manualScale`은 전부 0 = 자동). 값은 에디터 메뉴 `Hexiege/Effects/Measure Unit Recipient Fit`(`Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs`)이 채운다.

이번 작업에서 자주 언급되는 유닛:

| 유닛 (번호) | 반경 radius | 키 height | 맞춤 배율 (= 키) |
|---|---|---|---|
| BearGuard (20) | 0.220 | 0.530 | 0.53 |
| Tank (6) | 0.413 | 0.521 | 0.52 |
| CannonCart (7) | 0.405 | 0.530 | 0.53 |
| StreamSpirit (17) | 0.167 | 0.416 | 0.42 |
| LittleKnight (3) | 0.135 | 0.334 | 0.33 |
| RabbitTrickster (25) | 0.089 | 0.356 | 0.36 |

### 2-3. 화상 프리팹 `vfx_infernospirit_burn.prefab` `[사실]`

경로: `Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_burn.prefab`. 파티클 시스템 2개.

| 항목 | 루트 `vfx_infernospirit_burn` | 자식 `Flame_Haze` |
|---|---|---|
| 위치(로컬) | 0, 0, 0 | 0, **0.45**, 0 |
| 방출(Emission) | **꺼짐** | 켜짐, 초당 **1개** |
| 머티리얼 | 없음 | 있음(불꽃) |
| 최대 입자 수 | 1 | **2** |
| 수명 | — | 1초 |
| 시작 크기(3D) | 1, 1, 1 | **0.55 × 0.95 × 0.55** |
| 시작 속도 | — | 0 |
| 방출 모양(Shape) | 꺼짐 | **꺼짐** (설정은 Circle·반경 0.12·X축 −90° 회전·radiusThickness 0.5로 남아 있음) |
| Scaling Mode | Hierarchy | Hierarchy |
| 렌더 | — | Billboard(카메라를 향함), 정렬 보정 0 |
| Looping · Stop Action | 켜짐 · None | 켜짐 · None |

정리하면: **실제로 보이는 것은 `Flame_Haze` 하나이고, 몸 중심의 한 점에서 1초에 불꽃 1장이 나와 1초 동안 산다.** 화면에는 대체로 불꽃 1장(겹치는 순간 2장)이 보인다. 루트는 방출이 꺼지고 머티리얼이 없어 그려지는 것이 없다.

이 프리팹 모양은 사용자가 승인했고, 크기·위치는 프리셋으로 사용자가 미세 조정 중이다([VFXSFXList](../../../Assets/VFXSFXList.md) InfernoSpirit burn 행).

### 2-4. 독·힐 프리팹과의 차이 `[사실]`

독과 힐은 **이미 원 모양(Circle) 범위에서 방출**하는 레이어를 갖고 있다. 다만 그 범위도 키 기반 배율로 함께 커질 뿐 몸통 폭과는 무관하다.

| 프리팹 | 레이어 | 방출 | 모양 | 모양 반경 | 최대 입자 |
|---|---|---|---|---|---|
| 독 | `Poison_Bubbles` | 켜짐 | Circle 켜짐 | 0.09 | 5 |
| 독 | `Toxic_Drips` | 켜짐 | Circle 켜짐 | 0.09 | 5 |
| 독 | `Poison_Aura` | 켜짐 | 꺼짐 | — | 2 |
| 독 | 루트 | 꺼짐 | 꺼짐 | — | 1 |
| 힐 | `Rising_Petals` | 켜짐 | Circle 켜짐 | 0.15 | 6 |
| 힐 | `Sparkle_Motes` | 켜짐 | Circle 켜짐 | 0.15 | 8 |
| 힐 | `Soft_Aura` | 켜짐 | 꺼짐 | — | 2 |
| 힐 | 루트 | 꺼짐 | 꺼짐 | — | 1 |

전 레이어가 Scaling Mode Hierarchy다. 즉 **Hierarchy 스케일링에서는 모양 반경도 오브젝트 배율에 곱해진다** — 코드로 반경을 정할 때는 "원하는 실제 반경 ÷ 그 오브젝트에 걸린 배율"을 넣어야 한다 `[사실: Unity Hierarchy 스케일링 정의]`.

### 2-5. 풀링·반환 `[사실]`

- 풀은 **프리팹 단위**다(`EffectManager.GetOrCreateVfx(preset.VfxPrefab)`). 같은 화상 아이템이 다음에는 다른 유닛에 붙는다. 따라서 한 유닛에 맞춰 바꾼 값이 다음 재사용에 남으면 안 된다.
- 지금 바뀌는 값은 크기(`localScale`) 하나이며, `PlayTimedDamage`가 **매 재생마다 원본에서 새로 계산해 덮어쓰고**, `Return()`이 원본(`_originalLocalScale`, 최초 1회 저장)으로 되돌린다. 힐은 `_bloomFitScale`도 `Return()`에서 1로 되돌린다.
- 화상·독의 종료 경로는 세 갈래다: 자연 반환(`Return()` — 방출 정지 후 남은 입자가 다 사라진 뒤), 재생 실패 거부(`RejectTimedDamageAcquire`), 강제 정리(`OnDisable` — "Teardown" 기록, 반환 아님).
- 파티클 모듈 값(방출 모양·초당 개수·최대 입자)은 **현재 어떤 코드도 바꾸지 않는다.** 힐 재생 코드에는 "emission 설정을 변형하지 않는다"는 주석이 있다(`PlayBloomLoop`).

### 2-6. 자체 검증 `[사실]`

`Hexiege/Combat/Run Full Regression Suite`(`Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs`)가 부르는 `RunUnitActionSelfValidation.VfxConnections.cs`에 관련 검사가 있다.

- `ValidateDamageParticles` — 독·화상 프리셋 각각에 대해, 맞춤 없이 재생하면 원본 × 프리셋 크기, 맞춤(배율 2 + 발밑 보정) 재생하면 그 크기·위치, 같은 대상 갱신에도 크기 유지, 자연 반환 뒤 원본 크기 복원을 확인한다. 중간에 각 시스템에 입자 1개를 강제로 내보내고(`Emit(1)`) 시뮬레이션으로 비우는 단계가 있다.
- `ValidateRecipientFitTable` — 표 조회 규칙(키 기준 배율, 수동 배율 우선, 잘못된 값은 배율 1), 실제 표 25종의 사용 가능 여부, 비정상 크기 경고, 측정 도구 회귀.
- 파티클 모듈(모양·개수) 값을 검사하는 항목은 없다.

---

## 3. 문제 분석

### 3-1. 불꽃 폭 대 몸통 폭 `[계산]`

불꽃 한 장의 실제 폭 = 0.55 × 프리셋 1 × 맞춤 배율(키). 몸통 폭 = 반경 × 2. 불꽃 위 끝 = (0.45 + 0.95 ÷ 2) × 맞춤 배율 = 키의 92.5%(발밑 기준).

| 유닛 | 불꽃 폭 | 몸통 폭 | 불꽃/몸 |
|---|---|---|---|
| Tank | 0.287 | 0.827 | **35%** |
| CannonCart | 0.292 | 0.811 | **36%** |
| DustSpirit | 0.161 | 0.377 | 43% |
| InfernoSpirit | 0.235 | 0.548 | 43% |
| BoulderSpirit | 0.208 | 0.442 | 47% |
| Assault | 0.244 | 0.511 | 48% |
| FoxMagician | 0.169 | 0.350 | 48% |
| QuakeSpirit | 0.252 | 0.498 | 51% |
| LionKnight | 0.231 | 0.453 | 51% |
| RhinoBreaker | 0.280 | 0.498 | 56% |
| FlameSpirit | 0.208 | 0.359 | 58% |
| Sniper | 0.281 | 0.480 | 58% |
| TorrentSpirit | 0.234 | 0.384 | 61% |
| EmberSpirit | 0.191 | 0.310 | 61% |
| EagleArcher | 0.257 | 0.392 | 65% |
| BattleAxe | 0.242 | 0.368 | 66% |
| **BearGuard** | 0.292 | 0.440 | **66%** |
| LittleKnight | 0.184 | 0.271 | 68% |
| MushroomBomber | 0.213 | 0.313 | 68% |
| StreamSpirit | 0.229 | 0.333 | 69% |
| TideSpirit | 0.189 | 0.271 | 70% |
| SpearMan | 0.232 | 0.296 | 78% |
| BloomFairy | 0.239 | 0.261 | 91% |
| Pistoleer | 0.188 | 0.191 | 99% |
| RabbitTrickster | 0.196 | 0.179 | **109%** |

*(메인 세션 보고값 「래빗 110%」는 이 계산에서 109.4%로, 반올림 차이다.)*

곰전사 기준으로는 불꽃 폭 0.29·위 끝 0.49로 **몸(폭 약 0.44·키 0.53) 윤곽 안**에 들어간다. 즉 불꽃이 "작아서 몸에 묻히는" 모양이다.

### 3-2. 원인 `[사실 + 추정]`

1. `[사실]` 배율이 키만 보고 정해진다. 키가 비슷한 유닛(곰 0.53, 탱크 0.52, 리틀나이트 0.33)은 몸통 폭이 1.6~3배 달라도 불꽃 폭이 키에 비례할 뿐이다.
2. `[사실]` 불꽃이 한 점(몸 중심)에서만 나오고 화면에 대체로 1장이다. 넓은 몸에서 가장자리 쪽에는 불꽃이 전혀 없다.
3. `[사실 — 2026-10-26 실기 육안]` 불꽃이 몸 **중심**에 있으므로, 카메라 쪽에 있는 몸 앞면이 불꽃 일부를 가린다. 폭 맞춤 구현 뒤 사용자가 곰전사에서 "몸 안에 가려져서 안 보이는 게 크다"고 확인했다(9절). 원인 계산과 대응은 10절. *(2026-10-26 갱신: 종전 표기 「추정 — 미확인, 구현 후 육안 확인」)*

---

## 4. 대안 비교

사용자에게 설명된 세 안이다. 사용자는 **안 1**을 골랐다(작업 지시 「추천안으로 task 문서 작성해서 작업 진행」).

### 안 1 (선택) — 불꽃 크기는 키 기준 유지, 방출 범위를 몸통 폭만큼, 넓을수록 개수 증가

- 불꽃 한 장의 크기·모양은 지금과 같다(D-3 균등 배율 결정을 그대로 지킨다).
- 몸통 반경(표의 `radius`)으로 불꽃이 나오는 원판 범위를 정하고, 몸통 폭 ÷ 불꽃 폭으로 개수를 정한다.
- 좁은 유닛(래빗·권총병 등)은 계산상 범위 0·개수 1이 되어 **지금과 같다**.
- 장점: 승인된 불꽃 모양 보존, 위로 과하게 솟지 않음, 작은 유닛 무변화, 몸 앞쪽에 놓이는 불꽃이 생겨 가려짐도 줄어들 가능성 `[추정]`.
- 단점: 파티클 개수 증가(상한으로 묶음 — 6-2절), 재생 시 파티클 모듈 값을 바꾸므로 풀 재사용 때 원복이 필요, 방출 위치가 무작위라 두 장이 겹치는 순간이 있음 `[사실: Circle 방출은 무작위 위치]`.

### 안 2 — 가로·세로 비균등 배율 (가로는 반경 비율, 세로는 키 비율)

- 불꽃 한 장을 옆으로 늘린다.
- 단점: 2026-10-13 결정 D-3(균등)을 뒤집는다. Hierarchy 스케일링이라 Billboard 불꽃이 **납작하게 찌그러진** 모양이 된다(당시 Plan의 균등 선택 근거). 탱크는 가로가 약 3배 늘어난다 `[계산: 몸통 폭 ÷ 불꽃 폭 2.88]`.

### 안 3 — 배율 = max(키 비율, 반경 비율)

- 반경 기준 배율 = 반경 ÷ 0.25가 키 비율보다 크면 그것을 쓴다. 모양은 유지된다.
- 단점: 반경이 큰 유닛은 불꽃이 **위로도 함께 커진다**. 반경 기준이 이기는 유닛은 불꽃/몸 폭이 모두 110%가 되지만, 불꽃 위 끝이 키를 크게 넘는다 `[계산]`:

| 유닛 | 안 3 배율 | 불꽃 위 끝 ÷ 키 |
|---|---|---|
| Tank | 1.65 | 약 293% |
| CannonCart | 1.62 | 약 283% |
| BearGuard | 0.88 | 약 154% |
| StreamSpirit | 0.67 | 약 148% |

  VFXSFXGuide 「유닛에 붙는 상태 이펙트의 모양」의 "오래 솟구치는 상승을 피한다 — 높이도 유닛 크기 배율로 함께 커진다"와 같은 부류의 문제가 생긴다.

---

## 5. 미확인 항목과 확인 방법

| 항목 | 상태 | 확인 방법 |
|---|---|---|
| 몸 앞뒤 가려짐이 보이지 않는 원인에 기여하는지 | **곰전사에서 확인됨 — 남음**(2026-10-26 21:05 실기, 사용자 육안 · 9절). 탱크·대포수레는 미등장으로 미확인 | 후속 단계로 착수 — 조사 10절, 계획 [Plan.md 8절](Plan.md#8-d7-후속-상태-이펙트를-몸-앞에-그리기가려짐-해소). *(2026-10-26 갱신: 종전 상태 「추정 — 미확인」, 종전 방법 「구현 후 육안 확인, 남으면 후속 작업으로 기록」)* |
| Circle 방출 반경 값을 0으로 두었을 때 지금(Shape 꺼짐)과 같은 모양인지 | 미확인 | Plan의 구현 방식 결정(D5)에 따라 필요할 때만 확인 |
| 무작위 방출에서 두 장이 겹쳐 보이는 빈도 | 미확인 | 육안 확인. 거슬리면 Shape의 원호 분산(arc spread) 같은 균등 배치 방식을 game-programmer가 검토 `[제안]` |

---

## 6. 영향 범위

### 6-1. 독·힐 적용 여부

- **이번에 켜는 것은 화상뿐**이다(작업 지시). 독·힐은 **지금 동작을 그대로** 둔다.
- 화상과 독은 같은 재생 함수(`PlayTimedDamage`)를 쓴다. 따라서 이 함수에 넣는 폭 맞춤을 **프리셋 단위의 켜기 옵션**으로 만들면, 독은 옵션이 꺼진 채 지금과 같고 나중에 프리셋에서 켤 수 있다 `[제안]`.
- 힐은 별도 함수(`PlayBloomLoop`)다. 여기에 같은 옵션을 연결하려면 그 함수의 "emission 설정을 변형하지 않는다" 주석과 [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 34의 재생 전 검사 목록(burst 0 등)을 함께 검토해야 한다. 초당 개수(rate)만 바꾸고 burst는 건드리지 않으면 규칙 34의 검사와 충돌하지 않는다 `[계산: 규칙 34는 burst 0을 검사하고 rate는 검사하지 않음]`.
- 독·힐은 레이어가 여러 개(오라 레이어 포함)라, 켤 때 **어느 레이어에 적용할지** 정해야 한다(오라까지 여러 장 복제하면 어색할 수 있다 `[추정]`). 화상은 보이는 레이어가 하나라 이 문제가 없다.
- 어디까지 넣을지는 확정되지 않았다 → [Plan.md](Plan.md) 결정 D1.

### 6-2. 성능 `[계산]`

- 현재 화상 1건 = 보이는 입자 대체로 1개(최대 2개).
- 개수를 N배로 하면(초당 개수 ×N, 최대 입자 ×N) 화상 1건 = 대체로 N개(최대 2N개). 상한 3이면 최대 6개, 상한 4면 최대 8개.
- 현재 표로 계산한 필요 개수(Plan의 추천 공식)는 최대 3(탱크·대포수레)이다. 화상은 InfernoSpirit의 단일 대상 DoT라 한 번에 붙는 수는 InfernoSpirit 공격 수에 묶인다([유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 41).
- 상한은 프리셋 값으로 두어 무한히 늘지 않게 한다 `[제안]`.

### 6-3. 멀티플레이·싱글플레이 `[사실 + 계산]`

- 변경 대상(`VfxPoolItem`·`EffectManager`·`EffectPreset`·`UnitEffectConfig`)은 전부 Presentation 레이어이며 서버 판정·피해·DoT 틱에 관여하지 않는다. [동기화 규칙](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)의 Visual Root 계약대로 표현 소비자는 표현 pose만 읽는다.
- 같은 크기 표·같은 프리셋을 쓰므로 Host와 Client는 같은 개수·같은 범위를 계산한다 `[계산]`. 불꽃이 나오는 정확한 위치는 파티클 무작위 시드(`autoRandomSeed` 켜짐)라 화면마다 다를 수 있으나 표현 전용이라 문제가 아니다.
- 싱글·멀티 경로 차이 없음(같은 재생 함수).

### 6-4. 풀링 재사용 시 원복

- 바뀌는 값이 크기 하나에서 **모양 반경·모양 켜짐·초당 개수·최대 입자 수**까지 늘어난다. 2-5절의 세 종료 경로 모두에서 원본으로 돌아가야 하며, 가장 안전한 방식은 지금 크기 처리처럼 **매 재생 시작 때 원본에서 다시 설정**하는 것이다 `[제안]`. 상세는 Plan 3절.

### 6-5. 관련 규칙·가이드 문서

| 문서 | 관련 부분 | 영향 |
|---|---|---|
| [VFXSFXGuide](../../../Assets/VFXSFXGuide.md) 「Unity AI 프롬프트 작성 규칙 — 코드 계약」 1번 표 | 상태 이펙트 파티클 계약(Looping·Stop Action None·Local·Burst 없음·Hierarchy) | 이 계약은 그대로 지킨다. 코드가 재생 중 모양 반경·초당 개수를 바꾼다는 사실을 표의 "코드 근거" 칸에 추가해야 한다(구현 후) |
| 같은 문서 2번 「유닛에 붙는 상태 이펙트의 모양」 | 반경 0.25 원기둥·높이 1.0 제작 기준 · Circle에서 바깥 Start Speed 금지 · 크기는 EffectPreset과 크기 표로 맞춤 | 화상 `Flame_Haze` 시작 속도는 0이라 범위를 넓혀도 바깥으로 날아가지 않는다. "크기 맞춤" 설명에 "넓은 유닛은 방출 범위·개수도 맞춤(프리셋 옵션)"을 추가해야 한다(구현 후) |
| 같은 문서 「상태 이펙트 크기 측정 도구」 | 표의 `radius`를 쓰는 곳 | `radius`가 처음으로 런타임에 쓰이게 된다. 재측정 시점 설명은 그대로 유효 |
| [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 34 | 상태 이펙트 loop 소유·재생 전 검사·같은 owner refresh 미재시작 | 맞춤을 재생 시작 때 1번만 적용하면 refresh 미재시작 계약과 맞는다 |
| 같은 문서 규칙 40 | 대상 poison은 refresh로 끊거나 restart하지 않음 | 독 옵션을 켜더라도 같은 원칙(재생 시작 때 1번) |
| 같은 문서 규칙 41 | InfernoSpirit 단일 대상 on-hit DoT | 화상 표현의 대상. 규칙 변경 없음 |
| [VFXSFXList](../../../Assets/VFXSFXList.md) InfernoSpirit burn 행·「2026-10-13 대상 크기 맞춤」 블록 | 연결·수용 상태 | 구현·실기 후 상태 갱신 대상 |
| `UnitEffectConfig.cs` `radius` Tooltip · `RecipientReferenceRadius` 주석 | "배율 계산에 쓰지 않고 참고용" | 코드 주석이 사실과 달라지므로 구현 때 함께 고쳐야 한다(game-programmer 몫) |

---

## 7. 작업 중 발견한 부가 사항

- `[사실]` `RejectTimedDamageAcquire`는 크기(`localScale`)를 원본으로 되돌리지 않는다. 다만 `PlayTimedDamage`가 매 재생마다 크기를 원본에서 새로 계산해 덮어쓰므로 지금은 다음 재사용에 영향이 없다. 새로 바꾸는 파티클 값도 같은 "재생 시작 때 덮어쓰기" 방식이면 같은 이유로 안전하다. 별도 수정은 제안하지 않는다.
- `[사실]` 2026-10-13 측정 이후 `radius` 값은 표에만 있고 어떤 런타임 코드도 읽지 않았다. 이번 작업이 첫 사용처가 되므로, 측정값의 신뢰도(예: 탱크 0.413은 헥스 타일 폭의 절반 0.43에 가까움)가 처음으로 화면 결과에 영향을 준다. 측정 도구는 반경 0.43 초과를 경고한다(`SuspiciousMaxRadius`).

---

## 8. 근거 파일

| 파일 | 확인 내용 |
|---|---|
| `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs` | `UnitRecipientFitEntry`·`RecipientFit`·`RecipientReferenceHeight/Radius`·`TryGetRecipientFit` |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` | `PlayTimedDamage`·`RefreshTimedDamage`·`StopTimedDamage`·`RejectTimedDamageAcquire`·`OnDisable` |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs` | `PlayBloomLoop`·`FollowBloomPose`·`Return`·`InitializeSystems` |
| `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` | `StartTimedDamageEffect`·`StartBloomEffect`·`ResolveRecipientFit` |
| `Assets/_Project/Scripts/Presentation/Effects/EffectPreset.cs` | 프리셋 필드 목록 |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` | `_recipientFits` 25줄 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_{InfernoSpirit_Burn,MushroomBomber_Poison,BloomFairy_Heal}.asset` | 프리셋 배율·보정 |
| `Assets/_Project/Prefabs/VFX/Units/vfx_{infernospirit_burn,mushroombomber_poison,bloomfairy_heal}.prefab` | 파티클 모듈 값 |
| `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | `ValidateDamageParticles`·`ValidateRecipientFitTable` |
| `Assets/_Project/Scripts/Domain/Unit/UnitType.cs` | 유닛 번호 |

---

## 9. 실기 확인 (2026-10-26 21:05 로그)

폭 맞춤 구현(커밋 `eb660f70`) 뒤 사용자가 한 판을 실기했다. **이 절은 판정(PASS)을 매기지 않는다** — 회귀 스위트 결과가 보고되지 않았고, 탱크·대포수레·래빗이 등장하지 않아 Plan 5절의 여러 항목이 실행되지 않았다.

### 9-1. 로그 출처 `[전달 — 메인 세션]`

| 항목 | 값 |
|---|---|
| 보관 위치 | Hexiege-Logs 릴리스 asset `678048130` — `2026-10-26_21_05_server-projectile-charge2_01.zip` |
| sha256 | `d20769c82f2bfeb133dd46bb0762604c6b9fa8fe107a12e636bd26d674a88b7c` (Release digest와 일치, 메인 세션 확인) |
| manifest | 미업로드 |
| 포함 로그 | 기기 logcat 2개(`20_47`, `21_02`) + 에디터 RuntimeLog |

문서 담당은 이 ZIP을 직접 내려받아 다시 확인하지 않았다. 아래 로그 수치는 메인 세션 보고값이다.

### 9-2. 관측

| 관측 | 내용 | 근거 |
|---|---|---|
| 곰전사 화상 재생 기록 | 8건, Host·Client 모두 `FitScale=0.53; WidthFit=Flame_Haze:2@0.074` | `[전달]` 로그. Plan 1절 표의 곰전사 계산값(N 2, R 0.074)과 같다 `[계산 대조]` |
| 탱크·대포수레·래빗 | 이번 판에 등장하지 않음 | `[전달]` |
| 회귀 스위트(`Hexiege/Combat/Run Full Regression Suite`) | **미보고** | `[전달]` |
| 사용자 육안 | 「곰전사에게 화상 이펙트가 씌워져 있는 게 보이긴 하는데 잘 안 보인다」 → 「몸 안에 가려져서 안 보이는 게 크다」 | `[전달 — 사용자 발언]` |

### 9-3. 크기 대조 `[계산]`

곰전사(맞춤 배율 0.53, 반경 0.220, 키 0.530) 기준:

- 불꽃 한 장 폭 = 0.55 × 0.53 ≈ 0.29, 높이 = 0.95 × 0.53 ≈ 0.50
- 불꽃 2장이 퍼지는 최대 폭 = 원판 지름(0.074 × 2) + 불꽃 폭 0.29 ≈ **0.44** — 몸 폭(0.220 × 2 = **0.44**)과 같다
- 불꽃 높이 0.50 대 키 0.53

즉 폭 맞춤은 설계대로 동작해 불꽃이 **몸 윤곽을 꽉 채우는 크기**가 되었지만, D4 (a) 「몸통 윤곽 안」 결정 때문에 **윤곽 밖으로 나오는 부분이 없다.** 윤곽 안의 불꽃이 몸 뒤에 그려지면 보이는 부분이 거의 남지 않는다. 이것이 사용자 관찰(가려짐)과 맞는다 `[추정 — 원인 계산은 10-5]`.

### 9-4. Plan 5절 테스트 항목과의 대응

| 항목 | 이번 실기 | 비고 |
|---|---|---|
| SINGLE-1 곰전사 2장 | 로그로 2장 재생 확인. 육안은 「보이긴 하나 잘 안 보임」 | 판정 없음 — 기댓값 「몸 폭에 걸쳐 2장 보인다」를 육안으로 확인하지 못함 |
| SINGLE-2 탱크·대포수레 | 미실행 | 미등장 |
| SINGLE-3 래빗·스트림 | 미실행 | 래빗 미등장. 스트림 등장 여부 미보고 |
| SINGLE-4 가려짐 | **남음** | Plan 기댓값이 「남으면 남음으로 적는다」라고 정한 항목. → 후속 단계(10절) |
| SINGLE-5 재사용 | 미보고 | |
| SINGLE-6 독·힐 무변화 | 미보고 | |
| MULTI-1 Host·Client 같은 개수 | 곰전사만 로그로 Host·Client 모두 2장 | 탱크 미등장. 판정 없음 |
| PERF-1 | 미보고 | |
| 자동 검사 | 미보고 | 도구 존재·정적 검사는 메뉴 실행 PASS가 아니다(WORKFLOW [5-2]) |

---

## 10. D7 후속: 상태 이펙트를 몸 앞에 그리기(가려짐 해소)

Plan 1절 D7은 "가려짐이 남으면 후속 작업으로 기록"이었다. 9절의 실기로 가려짐이 확인되었고, 사용자가 후속 진행을 승인했다 — 「2번으로 진행, 몸 안에 가려져서 안 보이는 게 큰 것 같다」, 「TASK 문서 작성하고 진행」 `[전달 — 메인 세션]`. *(「2번」이 메인 세션이 제시한 선택지 중 어느 문구였는지 원문은 이 문서에 없다. 메인 세션 전달로는 「가려짐 해소 후속 착수」다.)*

### 10-1. 렌더 설정 `[사실]`

**화상 불꽃 `Flame_Haze`의 렌더러** (`Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_burn.prefab` — 렌더러 블록 4777행, `m_GameObject` 219084064984518426 = 15행 `m_Name: Flame_Haze`)

| 항목 | 값 | 행 |
|---|---|---|
| 렌더 모드 | `m_RenderMode: 0` = Billboard(카메라를 향한 판) | 4826 |
| 정렬 | `m_SortMode: 0`, `m_SortingFudge: 0`, `m_SortingOrder: 0` | 4828 · 4834 · 4824 |
| 그림자 | `m_CastShadows: 0`, `m_ReceiveShadows: 0` | 4786 · 4787 |
| 재질 | guid `0ddb3fa737ae41648a095998858c6918` = `Assets/_Project/Materials/VFX/Mat_VFX_Flame.mat` | 4803 |
| 시뮬레이션 공간 | `moveWithTransform: 0` = Local(입자가 오브젝트를 따라 움직임) | 109 |
| Scaling Mode | `scalingMode: 0` = Hierarchy | 111 |

**재질 `Mat_VFX_Flame.mat`**

| 항목 | 값 | 행 |
|---|---|---|
| 셰이더 | guid `0406db5a14f94604a8c57ccfbc9f3b46` | 11 |
| 표면 | `_Surface: 1`(투명), 키워드 `_SURFACE_TYPE_TRANSPARENT`, `RenderType: Transparent` | 70 · 15 · 23 |
| 렌더 큐 | `m_CustomRenderQueue: 3000`, `_QueueOffset: 0` | 21 · 64 |
| 깊이 쓰기 | `_ZWrite: 0`(깊이를 쓰지 않음) | 71 |
| 깊이 테스트 | 재질에 `_ZTest` 속성 **없음** | — |
| 블렌드 | `_Blend: 1`, `_SrcBlend: 1`, `_DstBlend: 10` | 47 · 68 · 59 |
| Soft Particles · Camera Fading | 꺼짐(`_SoftParticlesEnabled: 0`, `_CameraFadingEnabled: 0`) | 65 · 49 |
| 꺼진 패스 | `DepthOnly`, `SHADOWCASTER` | 25 · 26 |

- 셰이더 이름: 속성 구성(`_Surface`·`_ColorMode`·`_FlipbookBlending`·`_SoftParticlesEnabled`·`_CameraFadingEnabled`·`_DistortionEnabled`)은 URP 파티클 셰이더의 것이다 `[사실: 속성 이름]`. guid로 보면 URP `Particles/Unlit`으로 보이나, 이 체크아웃에 URP 패키지 소스(`Library/PackageCache`)가 없어 직접 확인하지 못했다 `[추정]`. 같은 이유로 셰이더의 깊이 테스트 값을 읽지 못했다 — 재질에 바꿀 수 있는 `_ZTest` 속성이 없으므로 셰이더 고정값(일반적으로 LEqual = "앞에 있는 것보다 뒤면 안 그림")일 것 `[추정 — 구현 시 확인]`.
- 같은 재질을 쓰는 프리팹은 5개다: `vfx_infernospirit_burn` · `vfx_infernospirit_tracer` · `vfx_infernospirit_impact` · `vfx_foxmagician_tracer` · `vfx_foxmagician_impact` `[사실: guid 검색]`. 재질을 바꾸면 이 5개가 모두 바뀐다.

**유닛 모델 재질** (예: 곰전사 `Assets/_Project/Prefabs/Units/Transcendence/Unit_BearGuard_Blue.prefab` — 몸 `SkinnedMeshRenderer` 169행 재질 `mat_bearguard_blue.mat`, 방패 `MeshRenderer` 862행 재질 `mat_bearshield_blue.mat`)

| 항목 | 값 |
|---|---|
| 셰이더 | guid `933532a4fcc9baf4fa0491de14d08ed7` (URP Lit으로 보임 `[추정 — 위와 같은 이유]`) |
| 표면 | `_Surface: 0`(불투명), `RenderType: Opaque` |
| 렌더 큐 | `m_CustomRenderQueue: -1`(셰이더 기본 = 불투명 큐) |
| 깊이 쓰기 | `_ZWrite: 1` |

`Assets/_Project/Materials/Units/` 아래 재질 73개 모두 같은 셰이더 guid·`Opaque`다 `[사실: 일괄 집계]`.

**정리** `[사실 + 일반 지식]`: 유닛 몸은 불투명 큐(2000번대)에서 먼저 그려지며 깊이를 남기고, 불꽃은 투명 큐(3000)에서 나중에 그려지며 깊이를 쓰지 않고 그 깊이와 비교만 한다. 그래서 **불꽃 픽셀 중 몸 표면보다 카메라에서 먼 부분은 그려지지 않는다.** 이것이 가려짐의 직접적인 구조다.

### 10-2. 카메라 `[사실]`

| 항목 | 값 | 근거 |
|---|---|---|
| 투영 | **직교**(`orthographic: 1`), orthographic size 8 | `Assets/_Project/Scenes/Game.unity` 9688 · 9689 |
| 기울기 | X축 **55°**, Y·Z 회전 0 | `Game.unity` 9777 `_tiltAngle: 55` · `Scripts/Presentation/Camera/CameraController.cs` 50(기본값) · 130 `ApplyTilt` |
| 회전을 쓰는 코드 | `ApplyTilt` 한 곳뿐(`CameraController.cs` 130, 호출: 같은 파일 121 · `Scripts/Bootstrap/GameBootstrapper.Setup.cs` 524) | `Camera`·`Bootstrap` 폴더 검색 |
| Red 관점 | 카메라를 돌리지 않고 위치를 뒤집는다(`Scripts/Core/ViewConverter.cs` `IsFlipped`) — 카메라 방향은 양 팀이 같다 | 위 검색 + `ViewConverter.cs` 40~47 |

- 카메라가 바라보는 방향(forward) = (0, −0.819, 0.574), **카메라 쪽 방향 = (0, 0.819, −0.574)** `[계산: Euler(55, 0, 0)]`.
- 🔴 **직교 카메라에서는 물체를 카메라 쪽 방향으로 옮겨도 화면 위치·크기가 전혀 바뀌지 않는다** — 화면 좌표는 카메라의 오른쪽·위쪽 축 성분으로만 정해지기 때문이다 `[계산: 직교 투영 정의]`. 바뀌는 것은 깊이(무엇의 앞에 그려지는가)뿐이다. 이 점이 아래 대안 (가)의 핵심 근거다.

### 10-3. 상태 이펙트 위치가 정해지는 경로 `[사실]`

| 단계 | 코드 |
|---|---|
| 매 프레임 따라붙기(화상·독) | `Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs` 114 — `LateUpdate`에서 `FollowTimedDamage` 호출 |
| 위치 보정값 저장(재생 시작 1회) | `VfxPoolItem.TimedDamage.cs` 47 — `_damageOffset = 프리셋 위치 보정 + 크기 표 발밑 보정` |
| 실제 위치 계산 | `VfxPoolItem.TimedDamage.cs` 70 — `위치 = 대상 pose 위치 + 대상 pose 회전 × _damageOffset` |
| 힐(같은 구조) | `VfxPoolItem.cs` 184(보정 저장) · 221~222(`FollowBloomPose` 위치 계산), 호출 `BloomHealEffectPresenter.cs` 93 |
| 재생 기록 줄 | `VfxPoolItem.TimedDamage.cs` 65~67(`FitScale=`, 옵션이 켜졌을 때만 `WidthFit=`) · 힐 `VfxPoolItem.cs` 206~207 |

- 보정값은 **대상 유닛 회전을 곱한 로컬 값**이다. 유닛은 돌아서지만 카메라는 돌지 않으므로, 카메라 쪽 당김은 이 로컬 보정에 넣으면 안 되고 **회전을 곱하지 않은 월드 값으로 따로 더해야 한다** `[계산]`.
- 입자는 Local 시뮬레이션(10-1)이라 오브젝트를 옮기면 이미 나온 불꽃도 함께 옮겨진다 `[사실 + Unity Local 시뮬레이션 정의]`.
- 화상 프리셋 `EffectPreset_InfernoSpirit_Burn.asset`의 위치 보정은 0, 0, 0이다(16행) `[사실]`.
- 기존 자체 검증 `RunUnitActionSelfValidation.VfxConnections.cs` 805~808은 "재생 위치 = pose 위치 + pose 회전 × (프리셋 보정 + 발밑 보정)"을 검사하며, 이때 쓰는 맞춤 값은 **반경 0**(2인자 생성자, 801행)이다 `[사실]`.

### 10-4. 기존 「카메라 쪽」 유사 구현 `[사실]`

`Assets/_Project/Scripts/`에서 카메라 쪽 오프셋을 찾았다(검색어: 「카메라 쪽」「카메라 방향」「toward…camera」「cam….forward」「Camera.main.transform」「_cam.transform」).

| 대상 | 가려짐을 피하는 방식 | 카메라 쪽 당김인가 |
|---|---|---|
| 피해·힐 숫자 `FloatingHpTextSpawner.cs` 87 · 251 / `FloatingHpText.cs` 143~145 | 대상 위로 1.2 올려 띄우고, 카메라를 정면으로 보게 회전 | 아니다(위로 올림 + 회전) |
| 스킬 조준원 `Assets/_Project/Shaders/SkillAimOverlay.shader` 46~48 | `ZTest LEqual` + `Offset -1, -1` — 같은 높이 타일과의 z-fighting만 이기는 아주 작은 깊이 보정 | 셰이더 깊이 보정(대안 (라)의 선례). 크기가 작아 몸 두께만큼은 못 넘는다 `[추정]` |
| 파도 이펙트 `VfxPoolItem.cs` 422~426 | 화면 관점 좌표(`ViewConverter`)로 배치 | 아니다 |
| `VisualRootProjector.cs` · `HitPresentationQueue.cs` | 카메라 참조 없음 | 아니다 |

→ **카메라 쪽으로 위치를 당기는 기존 구현은 없다.** 새로 만드는 방식이 된다.

또한 규칙 문서에 **ZTest Always 금지** 선례가 있다: [건물 규칙](../../../GameSystemRules/GameSystemRules_Buildings.md) 「MistShrine 물안개 힐 시스템」 규칙 27, [스킬 규칙](../../../GameSystemRules/GameSystemRules_Skills.md) 「모바일 지점 조준 UX」 규칙 22-1(「유닛·건물은 조준 원 위에」), [건물 패널 UI 규칙](../../../GameSystemRules/GameSystemRules_BuildingPanelsUI.md) 268·311행. 모두 지면 범위 표시에 대한 규칙이라 상태 이펙트에 직접 적용되지는 않지만, "모든 것 위에 뚫고 그리지 않는다"는 프로젝트 방침이다 `[사실: 원문]`.

### 10-5. 왜 가려지는가 — 깊이 계산 `[계산: 몸을 원기둥으로 본 근사]`

- 곰전사 불꽃 입자 중심 높이 = 0.45 × 0.53 ≈ 0.24(발밑 보정 제외). 직교 카메라의 Billboard 판은 화면과 나란해 **판 전체가 입자 중심과 같은 깊이**다.
- 불꽃의 한 픽셀에서 카메라 쪽으로 선을 그으면, 선은 수평으로 0.574, 위로 0.819 비율로 나아간다. 몸 원기둥(반경 r, 키 H)을 빠져나올 때까지의 거리 = 최소(수평으로 반경을 넘는 거리 r ÷ 0.574, 위로 키를 넘는 거리 (H − 픽셀 높이) ÷ 0.819).
- 곰전사 중심 불꽃의 **아래쪽 픽셀**(높이 ≈ 0): 최소(0.384, 0.647) = **0.384** — 몸 앞면이 이만큼 앞에 있어 가린다. **위쪽 픽셀**(높이 ≈ 0.45): (0.53 − 0.45) ÷ 0.819 ≈ 0.10 — 거의 안 가린다. → **불꽃의 아래·가운데 대부분이 몸 앞면 뒤에 있다.** 9-3에서 불꽃이 윤곽 밖으로 나오지 않는 것과 합쳐져 "몸 안에 가려져 안 보임"이 된다.
- 폭 맞춤 원판의 **뒤쪽 끝** 불꽃은 원판 반경 R만큼 더 뒤에 있어 최소((r + R) ÷ 0.574, H ÷ 0.819)까지 가려진다.

| 유닛 | 반경 r | 키 H | 원판 R | 중심 불꽃이 몸 앞으로 나오는 거리 | 원판 뒤끝 불꽃이 나오는 거리 |
|---|---|---|---|---|---|
| BearGuard | 0.220 | 0.530 | 0.074 | 0.384 | 0.513 |
| Tank | 0.413 | 0.521 | 0.270 | 0.636(키 쪽이 먼저) | 0.636 |
| CannonCart | 0.405 | 0.530 | 0.260 | 0.647(키 쪽이 먼저) | 0.647 |
| StreamSpirit | 0.167 | 0.416 | 0(1장) | 0.291 | — |
| LittleKnight | 0.135 | 0.334 | 0(1장) | 0.235 | — |
| RabbitTrickster | 0.089 | 0.356 | 0(1장) | 0.155 | — |

실제 모델은 원기둥이 아니므로(곰은 방패, 탱크는 상자형 차체) 이 값은 크기 가늠용이다 `[추정 — 근사 한계]`.

### 10-6. 대안 비교

| 안 | 내용 | 가려짐 해소 | 부작용 | 성능 | 작업 범위 |
|---|---|---|---|---|---|
| **(가) 카메라 쪽 당김** | 재생 중 이펙트 위치를 카메라 쪽 방향으로 몸통 반경 × 비율만큼 옮긴다 | **해소** — 당긴 거리가 10-5 값 이상이면 그 불꽃은 자기 몸보다 앞 `[계산]` | 직교 카메라라 화면 위치·크기 변화 없음 `[계산]`. 당긴 거리 사이에 있는 **다른 물체**(바로 앞을 지나가는 유닛, 큰 건물)의 앞에 그려질 수 있다 — 10-7 | 활성 이펙트 1개당 매 프레임 벡터 덧셈 1회 `[계산]`. 무시할 수준 | 코드(Presentation) + 프리셋 값 1칸. 재질·프리팹 무변경 |
| (나) 깊이 테스트 끔(ZTest Always) | 불꽃 재질이 깊이를 무시하고 항상 그려지게 | 해소 | **유닛·건물·지형 뒤에 있어도 뚫고 보인다**(건물 뒤에 숨은 유닛의 불꽃이 건물 위에 그려짐). 프로젝트 ZTest Always 금지 선례(10-4)와 충돌. `Mat_VFX_Flame`은 5개 프리팹 공용이라 **별도 재질**이 필요하고, 재질에 `_ZTest` 속성이 없어 셰이더 쪽 수정 또는 별도 셰이더가 필요할 수 있다 `[추정]` | 없음 | 재질 신규·셰이더 검토 |
| (다) 정렬 보정(sortingFudge / renderQueue) | 그리는 순서를 바꾼다 | **해소 못 함** — 정렬은 투명 물체끼리의 순서만 바꾼다. 불투명 몸은 이미 깊이를 써 두었고 불꽃은 그 깊이와 비교하므로 순서를 바꿔도 가려진다. 큐를 불투명보다 앞(2000 미만)으로 내리면 나중에 그려지는 몸이 불꽃을 덮어쓴다 `[사실: 10-1 값 + 일반 지식]` | — | 없음 | — |
| (라) 셰이더 깊이 보정 | 셰이더에서 깊이만 카메라 쪽으로 당긴다(`Offset` 또는 정점 셰이더에서 시점 방향 이동) | `Offset`: 깊이 버퍼 정밀도 단위라 몸 두께(약 0.4)만큼 넘기려면 큰 값이 필요하고 기기마다 효과가 다를 수 있다 `[추정]`. 정점 이동: (가)와 같은 효과 | (가)와 같은 다른 물체 앞 문제 + URP 파티클 셰이더 기능(블렌드·플립북 등)을 복제한 **자작 셰이더** 유지 부담, Android 셰이더 변형 검증 | GPU 정점 연산(미미) | 셰이더 신규·재질 신규 |

**추천 `[제안]`: (가).** 이 게임 카메라가 직교라서 화면 모습(위치·크기·모양)을 전혀 바꾸지 않고 깊이만 바꿀 수 있고, 재질·셰이더·프리팹을 건드리지 않으며, 폭 맞춤과 같은 "프리셋 옵션 + 재생 시작 때 계산 + 반환 때 원복" 구조를 그대로 쓸 수 있다. (나)는 프로젝트 방침과 부딪치고, (다)는 원리상 해결이 안 되며, (라)는 (가)와 결과가 같으면서 작업·유지 비용이 크다.

### 10-7. 영향 범위

**다른 물체 앞에 그려질 가능성** `[계산: 정지 배치 + 원기둥 근사]`
- 헥스 한 줄 앞(카메라 쪽) 칸의 유닛은 깊이로 약 0.43 앞(0.75 × 0.574)이지만, 화면에서는 0.61 아래(0.75 × 0.819)에 있다. 그 유닛 머리 꼭대기는 화면상 최대 −0.61 + 0.53 × 0.574 ≈ −0.31, 불꽃 아래 끝은 화면상 −0.22 × 맞춤 배율 ≥ −0.12다. **칸에 서 있는 상태에서는 화면에서 겹치지 않으므로** 당긴 불꽃이 앞 유닛 위에 그려질 일이 없다. 같은 줄 옆 칸(가로 1.0 거리)도 화면에서 겹치지 않는다. (`Scripts/Core/HexMetrics.cs` 49 `TileWidth = 1.0`, 112 줄 간격 0.75)
- 칸 사이를 **이동 중**인 유닛이 바로 앞을 지나거나, 키 큰 **건물**이 바로 앞에 있으면 겹칠 수 있다 `[추정 — 육안 확인]`. 당긴 거리가 클수록(탱크) 가능성이 커진다.

**독·힐** — 화상과 독은 같은 따라붙기 함수, 힐은 별도 함수다(10-3). D1과 같은 방식으로 **프리셋 옵션(기본 꺼짐)** 으로 넣으면 독·힐은 값이 0이라 지금과 같다 `[제안]`.

**규칙 34 loop 계약** — 오브젝트 위치만 바꾸고 Looping·Stop Action·Local·Burst·rate를 건드리지 않는다. 같은 대상 갱신(refresh)은 재계산하지 않는다(재생 시작 때 1번). 충돌 없음 `[계산]`.

**멀티·싱글** — Presentation 전용. 서버 판정·피해·DoT 틱·RPC와 무관하다. 카메라 방향이 양 팀 같고(10-2) 당김 거리는 같은 크기 표·프리셋에서 나오므로 Host·Client가 같은 값을 쓴다 `[계산]`. [동기화 규칙](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) 「2. Simulation Root와 Visual Root」 NET-ROOT-002 — VFX는 Visual Root(표현 계층) 담당이며 Simulation Root를 바꾸지 않는다.

**폭 맞춤과의 관계** — 당김은 원판 전체를 같이 옮기므로 폭 맞춤의 개수·범위·원복 경로에 영향이 없다. 직교라 화면상 범위도 그대로여서 D4 (a) 「윤곽 안」이 화면에서도 유지된다 `[계산]`.

**자체 검증** — 기존 위치 검사(10-3, 805~808)는 반경 0인 맞춤을 쓰므로, 당김 거리를 "반경 × 비율"로 정하면 기존 검사는 값이 바뀌지 않는다 `[계산]`. 에디터 검사 환경에서 카메라(`Camera.main`)가 있는지는 확인하지 않았다 `[미확인 — 구현 시 확인]`.

### 10-8. 미확인 항목

| 항목 | 확인 방법 |
|---|---|
| 불꽃 셰이더의 실제 이름과 고정 깊이 테스트 값 | 구현 시 Unity 에디터에서 재질 Inspector 확인(game-programmer) |
| 탱크·대포수레에서도 가려짐이 같은 정도인지 | 실기 육안(이번 판 미등장) |
| 당김 비율의 적정값(10-5 표는 원기둥 근사) | 구현 후 육안 조정 — 프리셋 값 |
| 이동 중 유닛·건물 앞으로 불꽃이 튀어나와 보이는 경우 | 실기 육안 |
| 에디터 자체 검증에서 카메라가 없을 때의 동작 | 구현 시 확인(카메라가 없으면 당김 0으로 두는 안을 Plan 8-4에 적음) |

### 10-9. 근거 파일 (이 절에서 추가로 읽은 것)

| 파일 | 확인 내용 |
|---|---|
| `Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_burn.prefab` | `Flame_Haze` 렌더러·시뮬레이션 공간·위치 |
| `Assets/_Project/Materials/VFX/Mat_VFX_Flame.mat` | 셰이더 guid·투명·큐·깊이 쓰기·블렌드 |
| `Assets/_Project/Prefabs/Units/Transcendence/Unit_BearGuard_Blue.prefab` · `Assets/_Project/Materials/Units/BearGuard/mat_bearguard_blue.mat` · `mat_bearshield_blue.mat` | 유닛 렌더러·불투명 재질 |
| `Assets/_Project/Scenes/Game.unity` | 카메라 직교·크기·기울기 |
| `Assets/_Project/Scripts/Presentation/Camera/CameraController.cs` · `Scripts/Bootstrap/GameBootstrapper.Setup.cs` | 기울기 적용 |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` · `VfxPoolItem.cs` · `TimedDamageEffectPresenter.cs` · `EffectPreset.cs` | 위치 계산·폭 맞춤 구현·프리셋 필드 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Burn.asset` | 위치 보정 0, 폭 맞춤 켜짐·상한 3 |
| `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs` · `UI/Common/FloatingHpText.cs` · `Assets/_Project/Shaders/SkillAimOverlay.shader` | 기존 가려짐 회피 방식 |
| `Assets/_Project/Scripts/Core/HexMetrics.cs` | 칸 간격 |
| `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | 기존 위치 검사(801~808) |
