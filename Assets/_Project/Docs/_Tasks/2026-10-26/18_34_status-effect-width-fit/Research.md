# Research — 상태 이펙트(화상) 몸통 폭 맞춤

## 이 문서는 무엇인가

지옥불 정령(InfernoSpirit)에게 맞은 유닛 몸에는 "불타고 있다"는 표시로 불꽃 이펙트가 붙는다. 그런데 곰전사(BearGuard)·탱크·대포수레처럼 **몸이 옆으로 넓은 유닛**에서는 이 불꽃이 잘 보이지 않는다는 문제가 있다.

원인은 불꽃 크기를 정하는 방식에 있다. 지금은 불꽃 크기를 **유닛의 키**에만 맞춰 키우고 줄이며, 불꽃은 몸 한가운데 **한 점에서 한 장씩** 피어오른다. 그래서 키는 비슷한데 몸이 두세 배 넓은 유닛은 넓은 몸통 가운데에 좁은 불꽃 하나만 보이게 된다.

사용자가 고른 방향(추천안 「안 1」)은 이렇다. **불꽃 한 장의 크기는 지금처럼 키에 맞추고, 불꽃이 피어오르는 범위를 몸통 폭만큼 넓히고, 몸이 넓을수록 불꽃 개수를 늘린다.** 이렇게 하면 작은 유닛은 지금과 똑같이 보이고 넓은 유닛만 불꽃이 여러 장으로 몸 전체에 퍼진다.

이 문서는 그 작업 전에 현재 구조와 수치, 문제의 크기, 다른 대안과의 비교, 영향 범위를 정리한다. 구현 방법과 사용자 결정 항목은 같은 폴더의 [Plan.md](Plan.md)에 있다. 이 작업은 **화면 표현만 바꾸며** 피해량·지속시간·멀티플레이 동기화 같은 게임 규칙은 바꾸지 않는다.

**표기:** `[사실]` = 코드·에셋·문서를 직접 읽어 확인한 것(2026-10-26 기준) · `[계산]` = 사실 값으로 계산한 것 · `[추정]` = 확인하지 않은 판단 · `[제안]` = 문서 담당이 제안하는 것(결정 아님).

---

## 1. 사용자 결정이 필요한 것 (요약)

결정 항목의 전체 내용·선택지·추천은 [Plan.md 1절](Plan.md#1-사용자-결정-사항)이 단일 소스다. 여기서는 목록만 둔다.

- 독·힐에도 같은 구조를 넣을지(이번에 켜는 것은 화상뿐) — 확정 아님
- 불꽃 개수 상한과 개수 계산 방식
- 불꽃이 퍼지는 범위를 몸통 윤곽 안으로 둘지
- 프리팹을 고칠지, 코드가 재생할 때만 바꿀지
- 자체 검증 항목을 추가할지
- 몸 앞뒤 가려짐이 남을 때 후속으로 돌릴지

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
3. `[추정 — 미확인]` 불꽃이 몸 **중심**에 있으므로, 카메라 쪽에 있는 몸 앞면이 불꽃 일부를 가릴 수 있다. 이 가려짐이 "잘 안 보임"에 얼마나 기여하는지는 실기에서 확인하지 않았다. 구현 후 육안 확인 항목으로 둔다(5절).

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
| 몸 앞뒤 가려짐이 보이지 않는 원인에 기여하는지 | `[추정 — 미확인]` | 구현 후 곰·탱크에서 육안 확인. 범위를 넓힌 뒤에도 불꽃이 몸 뒤로 숨는 것이 남으면 **후속 작업**(불꽃을 카메라 쪽으로 당기기 또는 렌더 정렬 조정)으로 기록한다. 이번 범위에서 고치지 않는다 |
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
