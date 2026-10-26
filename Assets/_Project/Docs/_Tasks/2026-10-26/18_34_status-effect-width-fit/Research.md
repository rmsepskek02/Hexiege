# Research — 상태 이펙트(화상) 몸통 폭 맞춤

## 이 문서는 무엇인가

지옥불 정령(InfernoSpirit)에게 맞은 유닛 몸에는 "불타고 있다"는 표시로 불꽃 이펙트가 붙는다. 그런데 곰전사(BearGuard)·탱크·대포수레처럼 **몸이 옆으로 넓은 유닛**에서는 이 불꽃이 잘 보이지 않는다는 문제가 있다.

원인은 불꽃 크기를 정하는 방식에 있다. 지금은 불꽃 크기를 **유닛의 키**에만 맞춰 키우고 줄이며, 불꽃은 몸 한가운데 **한 점에서 한 장씩** 피어오른다. 그래서 키는 비슷한데 몸이 두세 배 넓은 유닛은 넓은 몸통 가운데에 좁은 불꽃 하나만 보이게 된다.

사용자가 고른 방향(추천안 「안 1」)은 이렇다. **불꽃 한 장의 크기는 지금처럼 키에 맞추고, 불꽃이 피어오르는 범위를 몸통 폭만큼 넓히고, 몸이 넓을수록 불꽃 개수를 늘린다.** 이렇게 하면 작은 유닛은 지금과 똑같이 보이고 넓은 유닛만 불꽃이 여러 장으로 몸 전체에 퍼진다.

이 문서는 그 작업 전에 현재 구조와 수치, 문제의 크기, 다른 대안과의 비교, 영향 범위를 정리한다. 구현 방법과 사용자 결정 항목은 같은 폴더의 [Plan.md](Plan.md)에 있다. 이 작업은 **화면 표현만 바꾸며** 피해량·지속시간·멀티플레이 동기화 같은 게임 규칙은 바꾸지 않는다.

**후속 단계 「D7 후속: 상태 이펙트를 몸 앞에 그리기」(2026-10-26 추가).** 폭 맞춤을 넣은 뒤 실기에서 곰전사 불꽃이 계산대로 2장 나왔지만, 사용자는 "씌워져 있는 게 보이긴 하는데 잘 안 보인다 — 몸 안에 가려져서 안 보이는 게 크다"고 확인했다. 불꽃은 몸 **한가운데**에 서 있는 얇은 판처럼 그려지기 때문에, 카메라 쪽에 있는 몸 앞부분이 불꽃의 상당 부분을 덮는다. 그래서 다음 단계로 **불꽃을 카메라 쪽으로 조금 당겨 몸 앞에 그리게** 한다. 이 게임의 카메라는 원근이 없는(직교) 카메라라서, 카메라 쪽으로 당겨도 불꽃이 화면에서 위나 옆으로 밀리지 않고 "앞에 그려지는지"만 달라진다. 실기 결과는 [9절](#9-실기-확인-2026-10-26-2105-로그), 조사와 대안 비교는 [10절](#10-d7-후속-상태-이펙트를-몸-앞에-그리기가려짐-해소), 구현 계획은 [Plan.md 8절](Plan.md#8-d7-후속-상태-이펙트를-몸-앞에-그리기가려짐-해소)이다.

**다음 단계 「새 화상 VFX v2 교체·연결」(2026-10-26 22:21 실기 뒤 추가).** 몸 앞에 그리기까지 넣은 뒤 다시 실기했더니, 로그로는 곰전사 불꽃이 계산대로 2장·몸 앞으로 당겨져 나왔다. 하지만 사용자는 "불에 타오르는 느낌보다 **물건이 붙어 있는 느낌**", "불꽃이 **좌우로 빠르게 튄다**", "더 키워서 **몸을 감싸는 느낌**이면 좋겠다"고 했다. 지금 화상은 큰 불꽃 그림 한 장이 움직이지 않고 서 있는 구조라서, 크기·개수·위치를 코드로 아무리 맞춰도 "타오르는" 모습이 되기 어렵다는 것이 메인 세션의 판단이다 `[추정]`. 그래서 사용자는 **화상 이펙트 자체를 새로 만들기로** 했다 — 작은 불꽃 혀 여러 개가 몸 둘레에서 일렁이며 올라가는 모양이다. 새 이펙트(v2)는 이미 만들어져 올라왔고, 이 문서는 그것을 **화상에 연결하는 단계**를 다룬다. 새 이펙트는 스스로 몸 둘레에서 불꽃을 내므로, 앞 단계에서 넣은 "폭 맞춤"과 "몸 앞으로 당기기"는 화상에서 **끈다**(코드는 옵션으로 남기고, 기존 이펙트 파일도 되돌리기용으로 남긴다). 이번에도 **화상만** 바꾸며 독·힐은 그대로다. 실기 결과와 조사는 [11절](#11-새-화상-vfx-v2-교체연결-plan-9절), 연결 계획은 [Plan.md 9절](Plan.md#9-새-화상-vfx-v2-교체연결)이다.

**다음 단계 「뒤집힌 Client에서 상태 이펙트가 회전 중 옆으로 튐 — 따라가기 직전 위치 바로잡기」(2026-10-27 00:40 실기 뒤 추가).** 새 화상(v2)을 연결한 뒤 멀티로 실기했더니, 로그로는 v2가 Host·Client 모두에서 쓰였고 오류도 없었다. 사용자는 "화상 이펙트가 어느 정도 만들어진 것 같은데, **유닛 범위를 벗어나 잠깐씩 빠르게 옆으로 튄다**"고 했다(Host·Client 중 어느 화면인지, 어떤 유닛인지는 구분하지 못함). 코드를 읽어 보니 유력한 원인은 이렇다 `[추정 — 확정 아님]`: 화면이 뒤집혀 보이는 쪽(Red 관점 Client)에서는 유닛의 **눈에 보이는 몸**이 실제 위치와 맵 반대편에 따로 그려진다. 화상 이펙트는 그 "보이는 몸"의 위치를 매 프레임 읽어 따라가는데, 유닛이 **방향을 트는 프레임**에는 보이는 몸 위치가 아직 바로잡히기 전이라 잠깐 엉뚱한 곳(1~2 m 옆)을 가리킬 수 있다. 이펙트가 그 순간의 틀린 위치로 따라갔다가 다음 프레임에 돌아오니 "옆으로 튀는" 것처럼 보인다. 그래서 이 단계는 **이펙트가 따라가기 직전에 보이는 몸 위치를 먼저 바로잡게** 한다. 화면이 뒤집히지 않는 Host·싱글에서는 결과가 바뀌지 않는다. 화상과 같은 코드를 쓰는 독도 함께 고쳐지고, 같은 구조인 힐은 사용자가 "문제 없었던 것 같다"고 해 이번에는 빼고 작업 뒤 따로 확인한다. 조사는 [12절](#12-뒤집힌-client에서-상태-이펙트가-회전-중-옆으로-튐-plan-10절), 계획은 [Plan.md 10절](Plan.md#10-뒤집힌-client에서-상태-이펙트가-회전-중-옆으로-튐--따라가기-직전-위치-바로잡기)이다.

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
- *(2026-10-26 22:21 실기 뒤 추가)* 화상 이펙트를 새로 만들어(v2) 교체할지 — **사용자 결정: 교체.** 연결 방식(폭 맞춤·당김 끄기, 기존 프리팹 보존)도 사용자 승인. 상세는 [Plan.md 9-2절](Plan.md#9-2-사용자-결정승인-사항)
- *(2026-10-27 00:40 실기 뒤 추가)* 뒤집힌 Client에서 이펙트가 옆으로 튀는 문제를 어떻게 고칠지 — **사용자 결정: 추천안(수정안 1 — 따라가기 직전에 보이는 몸 위치를 바로잡기)대로 진행.** 힐은 이번 범위에서 빼고 작업 뒤 확인. 상세는 [Plan.md 10-2절](Plan.md#10-2-사용자-결정승인-사항)

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

---

## 11. 새 화상 VFX v2 교체·연결 (Plan 9절)

8절(Plan 8절) 「몸 앞에 그리기」는 커밋 `7f787d21`(「Draw burn VFX in front of the unit body (camera-ward view pull)」)로 구현됐다 `[사실: git log · git show --stat]`. 이 절은 그 뒤 실기, 사용자 판단, 새로 만든 화상 이펙트(v2), 그리고 v2를 연결할 때 앞 단계 옵션을 어떻게 다룰지를 정리한다. **이 절도 판정(PASS)을 매기지 않는다** — 회귀 스위트 결과가 보고되지 않았고 탱크 등이 등장하지 않았다.

### 11-1. 실기 확인 (2026-10-26 22:21 로그)

**로그 출처** `[전달 — 메인 세션]`

| 항목 | 값 |
|---|---|
| ZIP | Hexiege-Logs 릴리스 asset `678238109` — `2026-10-26_22_21_server-projectile-charge3_01.zip`, sha256 `b1dfcd25…8b0a` |
| manifest | asset `678272126`, sha256 `a53f5051…7e02` — **이번에는 업로드됨**(9-1의 21:05 판은 미업로드) |
| 무결성 | ZIP·manifest 둘 다 Release digest와 일치. manifest에 적힌 ZIP과 두 소스 파일의 크기·해시가 실제 추출본과 일치 |
| 포함 로그 | 기기 로그 sha256 `0f4ca068…` 12,503,022 B · 에디터 로그 sha256 `729c521b…` 13,533,544 B |

*(해시는 메인 세션이 앞·뒤 일부만 전달한 값이다. 문서 담당은 ZIP을 직접 내려받아 다시 확인하지 않았다. 아래 로그 수치는 메인 세션 보고값이다.)*

**관측**

| 관측 | 내용 | 근거 |
|---|---|---|
| 곰전사 화상 재생 기록 | 6건, Host·Client 모두 `WidthFit=Flame_Haze:2@0.074; ViewPull=0.385` | `[전달]` 로그 |
| 당김 거리 대조 | 0.385 = 곰 반경 0.220 × 비율 1.75 — Plan 8-2 표의 곰전사 값과 같다 | `[계산 대조]` |
| 카메라 못 찾음(`NoCamera`) | 0건 | `[전달]` |
| 탱크 등 넓은 유닛 | 등장하지 않음 | `[전달]` |
| 회귀 스위트 | **미보고** | `[전달]` |

**사용자 육안** `[전달 — 사용자 발언]`

- 「불에 타오르는 느낌보다 오브젝트가 붙어 있는 느낌」
- 「VFX가 좌우로 이동하며 빠르게 튐」
- 「크기 더 키워 몸체를 감싸는 느낌」

즉 폭 맞춤·당김은 로그상 계산대로 동작했지만, 사용자가 원하는 모습(몸을 감싸고 타오르는 불)과는 다르다는 판단이다.

### 11-2. 원인 분석 `[추정 — 메인 세션 분석, 확인 실험 없음]`

| 사용자 관찰 | 추정 원인 | 근거가 되는 사실 |
|---|---|---|
| 좌우로 빠르게 튐 | 폭 맞춤 뒤 **큰 불꽃 한 장**(곰 기준 폭 약 0.29)이 원판 안 **무작위 위치**에서 **초당 2장** 새로 생기고 1초 뒤 사라진다. 새 불꽃이 매번 다른 자리에 나타나 불꽃이 옆으로 순간이동하는 것처럼 보인다 | Circle 방출은 무작위 위치 `[사실: 4절 안 1 단점]` · 원래 초당 1개 × N 2 = 초당 2개, 수명 1초 `[계산: 2-3 표 + Plan 3-3]` · 곰 불꽃 폭 0.29 `[계산: 9-3]` |
| 물건이 붙은 느낌 | 불꽃 한 장이 **카메라를 향한 평면 그림 1장**이고 시작 속도 0이라 **움직임이 없다.** 크기·개수·위치를 코드로 바꿔도 "평면 그림이 서 있는" 성질은 그대로다 — 기존 프리팹 구조의 한계 | `Flame_Haze` Billboard·시작 속도 0 `[사실: 2-3 표 · 10-1 표]` |
| 더 키워 감쌌으면 | 기존 프리팹은 몸 가운데 한 점(폭 맞춤 뒤에도 몸 윤곽 안 원판)에서만 나와 몸 **둘레**를 두르는 모양이 아니다 | D4 (a) 윤곽 안 `[사실: Plan 1절]` |

### 11-3. 사용자 결정 `[전달 — 메인 세션]`

- 화상 이펙트를 **새로 만든다.**
- 이번에는 **화상만** 바꾼다. 독·힐은 결과가 좋으면 나중에 같은 방식으로 새로 만든다.
- 이펙트 **제작 자체는 task 문서 없이** 진행한다(에셋 제작). 만든 이펙트를 게임에 **연결하는 작업은 이 기존 task에 갱신**한다(사용자 지시 「기존 TASK 문서에 갱신」).
- 연결 방식(11-5)도 사용자가 승인했다. 상세 결정 표는 [Plan 9-2](Plan.md#9-2-사용자-결정승인-사항).

### 11-4. 새 화상 프리팹 v2

**제작 경로** `[전달 — 메인 세션]`

1. asset-prompt-crafter가 Unity AI용 프롬프트를 작성했다. 요지: 작은 불꽃 혀가 몸 둘레 **원 가장자리**에서 올라가며 일렁임, 불씨, 발밑 글로우, 옅은 연기. 제작 기준 키 1.0·반경 0.25(VFXSFXGuide 「유닛에 붙는 상태 이펙트의 모양」의 원기둥 기준). Looping·Prewarm, Simulation Space Local, Scaling Mode Hierarchy, Burst 0, Start Speed 0, Velocity over Lifetime의 x·y·z를 같은 모드로, 동시 입자 상한 33.
2. 사용자가 Unity AI로 생성해 원격 커밋 `7d1b9855`로 `Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_burn_v2.prefab`(guid `fcdce88228c7b2d41b8cc869e75a5344`)을 올렸다.

`[사실]` 이 체크아웃에는 이 커밋이 없다(`git show 7d1b9855` → unknown revision, v2 프리팹·.meta 파일 없음). 규칙 5로 fetch를 할 수 없어 **문서 담당은 v2 프리팹을 직접 읽지 않았다.** 아래 값은 메인 세션 점검값이다. *(2026-10-27 추가: 지금 체크아웃에는 `7d1b9855`와 연결 커밋 `5dd22db7`이 있다 — 상세는 12-1.)*

**레이어 구성** `[전달 — 메인 점검]`

| 레이어 | 재질 | 최대 입자 | 비고 |
|---|---|---|---|
| 루트 | — | — | 방출 꺼짐 |
| `Flame_Tongues` | `Mat_VFX_Flame` | 16 | 불꽃 혀 |
| `Embers` | GlowAdditive | 8 | 불씨 |
| `Base_Glow` | GlowAdditive | 4 | 렌더 Horizontal Billboard(바닥에 눕는 판) |
| `Smoke_Wisp` | `Mat_VFX_Smoke` | 5 | 옅은 연기 |

- 네 레이어 공통: Looping ON · Prewarm ON · Stop Action None · Simulation Space Local · Scaling Mode Hierarchy · Unscaled Time OFF · Burst 0 · Shape Circle · Velocity over Lifetime x·y·z 모두 Curve `[전달]`.
- 최대 입자 합 16 + 8 + 4 + 5 = **33** — 프롬프트 상한과 같다 `[계산]`.
- 이 공통 값은 [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 34의 상태 이펙트 loop 계약(Looping·Stop Action None·Local·burst 0)과 [VFXSFXGuide](../../../Assets/VFXSFXGuide.md) 「Unity AI 프롬프트 작성 규칙 — 코드 계약」 1번 표에 맞는다 `[계산: 전달값 대조 — 프리팹 직접 확인 아님]`.
- 재생 로그로 v2 사용을 구별할 수 있다: 재생 기록 줄의 `RecursivePS=`는 프리팹의 파티클 시스템 개수다(`VfxPoolItem.TimedDamage.cs` 75행) `[사실]`. 기존 프리팹은 2개(2-3 표), v2는 루트 포함 5개로 보인다 `[계산 — 루트에 파티클 시스템이 있다는 메인 점검 「루트 방출 OFF」 기준]`.

**같은 커밋에 함께 들어간 파일** `[전달 — 메인 점검]` — 이 task 범위 밖이며 기록만 한다.

- `Packages/manifest.json`·`packages-lock.json`: `com.unity.ai.assistant` 2.20.0-pre.2 → 2.21.0-pre.1
- 폰트 SDF 에셋
- 로그 폴더 `.meta` 파일(짝이 되는 원본 `.txt`는 없음)

### 11-5. v2 연결 시 앞 단계 옵션을 어떻게 할지

**폭 맞춤(1~3절) — 끈다** `[사실 + 계산]`

- 폭 맞춤 코드는 **방출이 켜진 모든 파티클 시스템**에 적용된다(`VfxPoolItem.TimedDamage.cs` 225행 `if (ps == null || !ps.emission.enabled) continue;`) `[사실]`. 적용 시 방출 모양 반경·두께(255·257행), 초당 개수 ×N, 최대 입자 ×N(260·261행)을 덮어쓴다 `[사실]`.
- v2는 네 레이어 모두 방출이 켜져 있고 Shape가 Circle이다(11-4). 폭 맞춤을 켜 두면 **설계된 몸 둘레 원 반경이 폭 맞춤 반경으로 덮어써진다**(곰 0.074 — 원판 안쪽 무작위 방출용 값). "둘레에서 타오르는" 구조가 깨진다 `[계산]`.
- 또 넓은 유닛은 개수 N(상한 3)만큼 입자가 늘어 v2 상한 33이 최대 99가 된다 `[계산: 33 × 3]`.
- 폭 맞춤의 목적(넓은 몸에 불꽃 퍼뜨리기)은 v2 프리팹이 스스로 둘레 방출로 맡는다 `[전달 — 메인 판단]`.

**몸 앞으로 당기기(8절) — 끈다** `[전달 — 메인 판단 · 추정]`

- v2는 불꽃이 몸 둘레를 돈다. 몸 앞쪽 불꽃은 원래 몸 앞에 보이고, 뒤쪽 불꽃은 몸에 일부 가려진다. 이 **앞은 보이고 뒤는 가려지는 차이**가 "몸을 감싸는" 입체감을 만든다.
- 당기기를 켜면 뒤쪽 불꽃까지 몸 앞으로 끌려 나와 감싸는 느낌이 사라질 것으로 본다 `[추정 — 육안 확인 대상]`.

**크기** `[계산 — 미확인 전제 포함]`

- 크기 맞춤(키 기반 균등 배율)은 그대로 적용된다(2-1). 화상 프리셋 `Scale Multiplier`는 1, 1, 1이다 `[사실: EffectPreset_InfernoSpirit_Burn.asset 18행]`.
- v2 원 반경이 프롬프트 기준대로 0.25라면 화면상 원 반경 = 0.25 × 맞춤 배율(키)이다. 아래는 그 가정으로 계산한 값이다. **v2의 실제 Shape 반경 값은 확인하지 않았다** `[미확인]`.

| 유닛 | 키(= 맞춤 배율) | 몸 반경 | v2 원 반경(가정 0.25 × 키) | 원 반경 ÷ 몸 반경 |
|---|---|---|---|---|
| BearGuard | 0.530 | 0.220 | 0.133 | 60% |
| Tank | 0.521 | 0.413 | 0.130 | 32% |
| CannonCart | 0.530 | 0.405 | 0.133 | 33% |
| StreamSpirit | 0.416 | 0.167 | 0.104 | 62% |
| LittleKnight | 0.334 | 0.135 | 0.084 | 62% |
| RabbitTrickster | 0.356 | 0.089 | 0.089 | 100% |

- 이 가정이 맞으면 넓은 유닛일수록 불꽃 원이 몸통보다 안쪽에 놓인다(탱크·대포수레 약 1/3). 사용자의 「크기 더 키워」 요청과 관련되므로 실기 육안 항목(Plan 9-7)에 넣는다. 크기 조정 수단은 (1) 프리셋 `Scale Multiplier`(전 유닛 공통 배율), (2) 코드가 Shape 반경을 몸 반경에 맞추는 방식(후속 후보 — 11-7)이다 `[제안]`.

### 11-6. 영향 범위

| 대상 | 영향 | 근거 |
|---|---|---|
| 기존 프리팹 `vfx_infernospirit_burn.prefab`(guid `32b8d45a8ebc4984485a467f6b7cbda3`) | 화상 프리셋이 더는 쓰지 않는다. **롤백용으로 보존**(삭제하지 않음) | 이 guid를 참조하는 에셋은 이 체크아웃에서 화상 프리셋 하나(15행 `_vfxPrefab`)와 문서 `Assets/_Project/Docs/History/ImplementationVerificationHistory.md`뿐이다 `[사실: guid 검색 — .asset·.prefab·.unity·.cs·.md 대상]` |
| 풀 | 풀은 프리팹 단위라(2-5) v2는 새 풀을 쓴다. 기존 프리팹 풀은 생성되지 않는다 `[계산]` | `EffectManager.GetOrCreateVfx(preset.VfxPrefab)` |
| 폭 맞춤·당김 코드 | 지우지 않고 옵션으로 남긴다. 화상 프리셋에서 값만 끈다(비활성화 우선 원칙) | 사용자 승인 `[전달]` |
| 자체 검증 | 현재 검사가 화상 프리셋의 폭 맞춤 켜짐(`RunUnitActionSelfValidation.VfxConnections.cs` 889행)과 당김 비율 1.75(1100행)를 요구한다 `[사실]`. 새 설정에서는 이 두 검사가 실패하므로 수정이 필요하다 — **game-programmer가 동시 수정 중**(구체 변경은 메인 세션이 나중에 전달) | `[사실 + 전달]` |
| 재생 로그 | 폭 맞춤·당김이 꺼지면 `WidthFit=`·`ViewPull=` 기록이 붙지 않는다(옵션이 꺼지면 null — 68·74~78행, 416행) `[사실]` | |
| 독·힐 | 프리셋·프리팹 모두 그대로 | 이번 범위 아님 |
| 멀티·싱글 | Presentation 전용. 서버 판정·피해·DoT 틱·RPC 무관. 같은 프리셋을 쓰므로 Host·Client 같은 프리팹 | [동기화 규칙](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) 「2. Simulation Root와 Visual Root」 NET-ROOT-002 |
| 성능 | 화상 1건 동시 입자 상한: 기존 2(폭 맞춤 켜짐 시 최대 6) → v2 33(폭 맞춤 꺼짐) `[계산: 2-3 표 · 6-2 · 11-4]`. 레이어 4개라 그리기 호출도 늘 수 있다 `[추정]` | Plan 9-7 PERF-2 |

*(이 체크아웃의 화상 프리셋 값은 아직 변경 전이다 — `_vfxPrefab` 기존 guid, `_widthFitEnabled: 1`, `_viewPullRadiusRatio: 1.75`(15·21·23행) `[사실: 2026-10-26 이 문서 작성 시점]`.)*

### 11-7. 후속 후보 (이번 범위 아님)

| 후보 | 상태 |
|---|---|
| 코드가 v2의 Shape 반경을 몸 반경에 맞추는 방식(넓은 유닛에서 불꽃 원이 몸통 안쪽에 머무는 문제 대응 — 11-5 크기) | v2 실기 결과를 보고 결정 |
| 독·힐 v2 제작·교체 | 화상 결과가 좋으면 진행(사용자 결정 11-3) |
| 기존 화상 프리팹 삭제 | 사용자 승인 뒤, guid 참조 0을 확인하고 진행 |

### 11-8. 미확인 항목

| 항목 | 확인 방법 |
|---|---|
| v2 프리팹 실제 값(Shape 반경·방출 개수·크기·루트 파티클 시스템 유무) | 문서 담당은 미확인(커밋 미보유). game-programmer 연결 작업 때 확인 |
| v2가 감싸는 느낌·튐 없음·크기·뒤쪽 가림을 만족하는지 | 실기 육안(Plan 9-7) |
| v2 동시 입자 33의 성능 영향 | 실기(PERF-2) |
| 같은 커밋의 패키지 버전 변경이 다른 기능에 영향을 주는지 | 이번 범위 밖 — 기록만 |

---

## 12. 뒤집힌 Client에서 상태 이펙트가 회전 중 옆으로 튐 (Plan 10절)

11절(Plan 9절) v2 연결 뒤 멀티 실기에서 "옆으로 튐"이 보고됐다. 이 절은 실기 기록, 원인 진단(가설 비교), 고치는 방안 비교를 정리한다. **이 절도 판정(PASS)을 매기지 않는다** — 회귀 스위트 결과가 보고되지 않았고, 원인은 코드 읽기로 좁힌 **추정**이며 재현 실험으로 확정하지 않았다.

### 12-1. 앞 단계 구현 상태 `[사실: git log · 에셋 직접 확인]`

- v2 프리팹 커밋 `7d1b9855`와 연결 커밋 `5dd22db7`(「Connect burn VFX v2 to InfernoSpirit burn preset」)이 지금 체크아웃에 있다. 11-4·11-6의 "이 체크아웃에는 없다/아직 변경 전" 표기는 그 문서 작성 시점의 사실이다.
- 화상 프리셋 `EffectPreset_InfernoSpirit_Burn.asset`: `_vfxPrefab` guid `fcdce88228c7b2d41b8cc869e75a5344`(v2) · `_widthFitEnabled: 0` · `_viewPullRadiusRatio: 0` · `_scaleMultiplier` 1, 1, 1(15·21·23·18행) — Plan 9-2 V3~V5와 같다.
- v2 프리팹에는 파티클 시스템이 **5개**이고, 다섯 모두 `moveWithTransform: 0`(Local 시뮬레이션 — 입자가 오브젝트를 따라 움직임)이다. 11-4의 "루트 포함 5개로 보인다" 계산과 맞는다.
- Plan 9-4 3번(자체 검증 수정)은 같은 커밋에 들어갔다. 커밋 메시지 요지: 실제 화상 프리셋은 v2·옵션 꺼짐을 확인하고, 폭 맞춤·당김 기능 검사는 메모리 안 시험용 프리셋으로 돈다 `[사실: 커밋 메시지 — 검사 코드 내용은 이 절에서 다시 읽지 않음]`.

### 12-2. 실기 확인 (2026-10-27 00:40 로그)

**로그 출처** `[전달 — 메인 세션]`

| 항목 | 값 |
|---|---|
| ZIP | Hexiege-Logs 릴리스 asset `678618903` — `2026-10-27_00_40_server-projectile-charge3_01.zip`, sha256 `fadbe518dba01cdb4dbc9007b06f83100b43a7f85c2b0d3cbaf4db5814892630` |
| manifest | asset `678618859`, sha256 `3618241d…bf6` — **업로드됨** |
| 무결성 | ZIP·manifest 둘 다 Release digest와 일치. manifest의 두 소스 파일 해시가 실제 추출본과 일치 |
| 포함 로그 | 기기 `00_40_logcat` 28,019,800 B · 에디터 `_editor/2026-10-27` 1,249,228 B |

*(manifest 해시는 메인 세션이 앞·뒤 일부만 전달한 값이다. 문서 담당은 ZIP을 직접 내려받아 다시 확인하지 않았다. 아래 로그 수치는 메인 세션 보고값이다.)*

**관측** `[전달]`

| 관측 | 내용 |
|---|---|
| 화상 재생 기록 | **31건**, Host·Client 모두 `RecursivePS=5`(v2 사용 — 12-1), `FitScale=0.53` |
| `WidthFit=` · `ViewPull=` | 없음(옵션 꺼짐 — Plan 9-7 「로그 확인」 기댓값과 같다) |
| 오류 | 0 |
| 종료 경로 | Host: `AttachmentClose` 사유 `RecipientDespawn` 31건 · Client: `LocalDeath` 31건(기기 로그 17361행) · 정지는 각 `StopEmitting` 30 + `ServerStop` 1, 그 뒤 약 1.27초 동안 남은 입자가 사라지고(Drain) 풀로 반환(Return) |
| 회귀 스위트 | **미보고** |

**사용자 육안** `[전달 — 사용자 발언]`

- 「화상 이펙트가 어느 정도 만들어진 것 같은데 유닛 범위를 벗어나 잠깐씩 빠르게 옆으로 튐」
- 호스트·클라이언트 중 어느 화면인지 구분하지 못함. 어떤 유닛(오브젝트)에서였는지도 불명.

*(`FitScale=0.53`은 곰전사 키와 같은 값이다(2-2 표). 31건이 모두 곰전사였는지, 같은 값의 다른 유닛(대포수레 0.53)이 섞였는지는 전달되지 않았다 `[미확인]`.)*

### 12-3. 원인 진단 — 가설 비교 `[전달 — game-programmer 읽기 전용 진단 · 일부 문서 담당 재확인]`

진단 결과 **H3이 유력**하다. 이것은 코드와 로그로 좁힌 **추정이며 확정이 아니다** — 이펙트 위치를 프레임 단위로 기록한 로그나 재현 실험은 없다.

| 가설 | 내용 | 판단 |
|---|---|---|
| H1 | 유닛이 죽는 순간 이펙트가 튄다 | **기각** — 아래 12-5 |
| H2 | 사망 뒤 몸이 사라졌는데 불만 남아 보인다 | **별개 현상** — "튐"이 아니다. 기록만(12-5) |
| **H3** | 뒤집힌 Client에서, 유닛이 방향을 트는 프레임에 이펙트가 바로잡히기 전의 보이는 몸 위치를 따라간다 | **유력** — 12-4 |
| H4 | v2 프리팹 자체가 옆으로 튀는 입자를 낸다 | **주원인 아님** — 아래 12-5 |

### 12-4. H3 — 구조와 크기

**코드 경로** (`[사실]`은 문서 담당이 이번에 다시 읽은 줄)

| 단계 | 내용 | 근거 |
|---|---|---|
| ① 몸 위치 얻기(1회) | 화상·독 presenter는 대상 유닛의 pose Transform을 **처음 한 번만** 얻어 저장하고 이후 그대로 쓴다 | `Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs` 106 `if (owner.Pose == null) owner.Pose = _pose?.Invoke(owner.Target);` `[사실]` |
| ② 그 Transform은 무엇인가 | 연결 함수는 `PresentationPoseProvider.GetUnitTransform` → `VisualRootProjector.PresentationTransform`. getter는 **읽는 그 순간에만** `ProjectNow()`를 부르고 VisualRoot Transform 자체를 돌려준다 | `Scripts/Bootstrap/GameBootstrapper.Map.cs` 289~295 · `Scripts/Presentation/Unit/PresentationPoseProvider.cs` 31~43 · `Scripts/Presentation/Unit/VisualRootProjector.cs` 28~35 `[사실]` |
| ③ 매 프레임 따라가기 | presenter `LateUpdate`가 저장한 Transform으로 `FollowTimedDamage`를 부르고, 그 안에서 `pose.position`·`pose.rotation`을 읽어 이펙트를 옮긴다. 이 경로에서는 `ProjectNow()`를 부르지 않는다 | `TimedDamageEffectPresenter.cs` 114 → `Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` 90 `[사실]` |
| ④ 보이는 몸 위치를 정하는 곳 | `ProjectNow()`는 멀티 Client이면서 화면이 뒤집혔을 때(`ViewConverter.IsFlipped`) VisualRoot를 맵 중심 점대칭 위치(`ViewConverter.ToView`)·180° 돌린 방향으로 둔다. Host·싱글·뒤집히지 않은 Client는 실제 Root와 같은 pose다. 같은 입력이면 같은 결과(멱등) | `VisualRootProjector.cs` 81~107 `[사실]` |
| ⑤ 정기 바로잡기 시점 | `VisualRootProjector.LateUpdate`가 매 프레임 `ProjectNow()`를 부른다 | `VisualRootProjector.cs` 75~79 `[사실]` |
| ⑥ 부모·자식 관계 | VisualRoot는 실제 위치 루트(Simulation Root)의 **직계 자식**이다. 그래서 `Update` 중에 NetworkTransform이 루트를 옮기거나 돌리면, ⑤가 다시 바로잡기 전까지 VisualRoot는 "루트와 함께 움직인" 틀린 위치에 있다 | [동기화 규칙](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) 「2. Simulation Root와 Visual Root」 NET-ROOT-004 「직접 자식 `VisualRoot`」 `[사실: 규칙 원문]` · 로그 `visualRootDirectChild=True` `[전달]` |
| ⑦ 실행 순서 | presenter와 `VisualRootProjector` 둘 다 `DefaultExecutionOrder`가 없다. 프로젝트에서 이 속성을 쓰는 곳은 `InputFormController`(−100)·`UnitRootPoseConsistencyObserver`(10000) 두 곳뿐이다. 따라서 같은 `LateUpdate` 단계에서 **presenter가 먼저 돌지, 바로잡기가 먼저 돌지 정해져 있지 않다** | 전체 `Assets/` 검색 `[사실]` |

**정리:** 뒤집힌 Client에서 presenter의 `LateUpdate`가 그 유닛의 바로잡기(⑤)보다 먼저 돌면, 이펙트는 ⑥의 틀린 위치를 따라간다. 다음 프레임 순서나 바로잡기 결과에 따라 다시 맞는 위치로 돌아오므로 "잠깐씩 옆으로 튐"과 맞는다 `[추정]`. 저장한 Transform을 쓰지 않고 매번 getter를 거쳤다면 ②처럼 읽을 때 바로잡혔을 것이다 — 화상 경로는 그 바로잡기를 **우회**하고 있다 `[사실 + 추정]`.

**로그로 본 실제·화면 위치 차이** `[전달]`

| 시각 | 쪽 | `isFlipped` | 비고 |
|---|---|---|---|
| 기기 00:22:38.973 `[UAS-ROOT-POSE]` | Client(기기) | **True** | `visualRootDirectChild=True`, 실제 위치 (6.01, −2.18) 대 화면 위치 (1.49, −15.14) — 차이 약 **13.7 m** |
| Host 00:22:39.314 | Host | False | 같은 기록 줄. 뒤집히지 않음 |

`[계산]` 두 점 차이 = √(4.52² + 12.96²) ≈ 13.7 m.

**튀는 크기** `[계산]`

루트가 한 프레임에 Δθ만큼 돌면, 루트 피벗에서 거리 L만큼 떨어진 자식(VisualRoot)은 그 프레임 안에서 원호를 따라 L × 2 × sin(Δθ ÷ 2)만큼 옆으로 밀린다. 유닛 회전 속도는 초당 270°다(`Scripts/Presentation/Unit/UnitView.cs` 742 `_rotationSpeed = 270f` `[사실]`).

| 프레임 | 프레임당 회전 Δθ | 옆으로 밀리는 거리(L = 13.7 m) |
|---|---|---|
| 60fps | 4.5° | 약 **1.08 m** |
| 30fps | 9° | 약 **2.15 m** |

- 몸 반경은 가장 큰 탱크도 0.413이다(2-2 표). 즉 회전하는 프레임 하나에서 이펙트가 **몸 폭의 몇 배**만큼 옆에 그려질 수 있다 — "유닛 범위를 벗어나 옆으로 튐"과 크기가 맞는다 `[계산 + 추정]`.
- 이동만 할 때는 이 밀림이 프레임당 이동 거리 정도(수 cm)라 눈에 띄지 않는다 `[전달 — 진단 계산]`.
- L은 유닛이 맵 중심에서 멀수록 커진다(점대칭이므로 L = 맵 중심까지 거리의 2배) `[계산]`. 같은 회전이라도 맵 가장자리 유닛에서 더 크게 튄다.
- Host·싱글에서는 ④에서 VisualRoot = 루트 pose라 L = 0, 밀림이 없다 `[계산]`. 사용자가 어느 화면인지 구분하지 못했지만, 이 가설이 맞다면 **뒤집힌 Client 화면에서만** 생긴다.
- 기존 화상 프리팹도 Local 시뮬레이션이었으므로(10-1) 이 현상은 v2 전부터 있었을 수 있다. v2에서 불꽃이 커지고 움직임이 생겨 눈에 띄게 됐을 가능성이 있다 `[추정]`.

### 12-5. 기각·별개 가설

**H1 사망 순간 튐 — 기각** `[전달 — 진단 · 일부 사실 확인]`
- 종료는 사망 RPC / despawn 처리 **안에서 동기적으로** 일어나고, 그 뒤 따라가기(follow)가 더 돌지 않는다(`TimedDamageEffectPresenter.cs` 127~129 `Stop`·`Attachment` `[전달]`; Client `LocalDeath`는 `LateUpdate` 105행에서 `CloseAttachment` 뒤 `continue` `[사실]`).
- 사망 대역(ghost)은 떼기 전에 `ProjectNow()`로 마지막 화면 pose를 쓴 뒤(`VisualRootProjector.cs` 66 `[사실]`) 월드 위치를 유지한 채 부모에서 분리된다(`Scripts/Presentation/Unit/UnitView.DeathDeferral.cs` 93 `root.SetParent(null, true)` `[사실]`). 대역 쪽에서 위치가 튈 구조가 없다.

**H2 사망 뒤 몸 없이 불만 남음 — 별개 현상, 기록만**
- 사망 대역은 최대 0.3초 뒤 파괴된다(`UnitView.DeathDeferral.cs` 126 `Destroy(ghost)` `[사실]` · 0.3초 상한 `[전달]`). 화상 이펙트는 정지 뒤 약 1.27초 동안 남은 입자가 사라진다(12-2 `[전달]`). 그래서 **몸이 사라진 자리에 불이 1초 남짓 남을 수 있다** `[계산]`.
- 이것은 "옆으로 튐"이 아니라 "제자리에 남음"이라 이번 보고의 원인으로 보지 않는다. 사용자가 거슬린다고 하면 별도 후속 후보로 다룬다(Plan 10-8).

**H4 프리팹 자체 — 주원인 아님** `[전달 — 진단]`
- v2 불꽃 텍스처는 16칸 중앙의 고리 모양 덩어리이고, 속도 x·z 성분이 0이며, noise 흔들림이 약 0.08 m다. 몸 반경(0.1~0.4) 밖으로 1 m 넘게 튀는 크기가 아니다.

### 12-6. 고치는 방안 비교

| 안 | 내용 | 장점 | 단점 | 판단 |
|---|---|---|---|---|
| **수정안 1** | presenter `LateUpdate`에서 `FollowTimedDamage` **직전에** 그 유닛의 `VisualRootProjector.ProjectNow()`를 부른다(매 프레임 `PresentationTransform` getter를 거치거나, Owner에 projector를 저장해 두고 부름 — 구현 시 결정) | 실행 순서와 무관하게 따라가는 순간 항상 바로잡힌 위치를 읽는다. `ProjectNow()`가 멱등이라 같은 프레임에 여러 번 불러도 결과가 같고(`VisualRootProjector.cs` 81~85 주석 `[사실]`), Host·싱글은 ④대로 결과 불변. 화상·독이 같은 presenter라 한 곳 수정으로 둘 다 적용 | 유닛당 매 프레임 `ProjectNow()` 1회 추가(위치·회전 계산과 대입 — 미미 `[추정]`) | **채택(사용자 승인)** |
| 수정안 2 | `DefaultExecutionOrder`로 projector가 presenter보다 먼저 돌게 순서를 고정 | 코드 변경이 속성 한 줄 | 실행 순서 전역 설정이라 다른 소비자·다른 컴포넌트와의 순서에 영향이 퍼진다. 프로젝트에서 거의 쓰지 않는 방식(⑦ — 2곳뿐). 나중에 새 소비자가 생기면 같은 문제가 반복될 수 있다 `[추정]` | 기각 |
| 수정안 3 | 화상 이펙트를 World 시뮬레이션으로 바꿔 튄 프레임의 영향을 줄인다 | 이미 나온 입자는 튄 위치로 끌려가지 않는다 | 원인(틀린 위치를 읽음)을 고치지 않는다 — 새로 나오는 입자는 여전히 튄 위치에서 나온다. 규칙 34 / VFXSFXGuide 「코드 계약」 1번 표의 상태 이펙트 계약 **Local**과 충돌하고, 이동 중 꼬리가 남는 등 모양이 바뀐다 `[사실: 계약 원문 + 추정: 모양]` | 기각 |

### 12-7. 영향 범위

| 대상 | 영향 | 근거 |
|---|---|---|
| 화상·독 | 같은 presenter(`TimedDamageEffectPresenter`)라 함께 적용된다. 독도 같은 이유로 뒤집힌 Client에서 튈 수 있었다 `[추정]` | 2-1 · 10-3 |
| 힐 | `BloomHealEffectPresenter.cs` 91~96이 **같은 패턴**(저장한 pose로 `FollowBloomPose`) `[사실]`. 사용자가 「힐은 문제 없었던 것 같다」고 해 **이번 범위에서 제외**, 작업 뒤 사용자 확인 → 후속 후보 | 사용자 결정 `[전달]` |
| Host·싱글 | 결과 불변 — `ProjectNow()`가 Root pose를 그대로 쓰고, 이미 같은 값이면 같은 값 | ④ `[사실]` |
| 다른 pose 소비자 | `ProjectNow()`는 VisualRoot만 쓰고 Simulation Root를 쓰지 않는다(`VisualRootProjector.cs` 81~84 주석). 같은 프레임 다른 소비자가 읽는 값은 "이미 바로잡힌 값"으로 같아진다 | `[사실 + 계산]` |
| 사망 대역 | 대역이 떼어진 뒤 `_visualRoot = null`이므로 `ProjectNow()`가 아무것도 하지 않는다(`VisualRootProjector.cs` 63~69 · 88~89) | `[사실]` |
| 멀티·싱글 판정 | Presentation 전용. 서버 판정·피해·DoT 틱·RPC 무관. 표현 소비자는 presentation pose를 읽는다 | 동기화 규칙 NET-ROOT-002 · NET-ROOT-004 「표현 소비자는 presentation pose를 읽으며」 |
| 성능 | 활성 화상·독 1건당 매 프레임 `ProjectNow()` 1회 | `[추정 — 미미]` |

### 12-8. 미확인 항목

| 항목 | 확인 방법 |
|---|---|
| 튐이 실제로 H3(뒤집힌 Client·회전 프레임)에서 생기는지 | 수정 뒤 Client 기기 육안. 가능하면 진단 로그(Plan 10-4 3번 제안) |
| 튄 화면이 Host였는지 Client였는지, 어떤 유닛이었는지 | 사용자 미구분 — 다음 실기에서 화면 구분해 확인 |
| 31건의 대상 유닛 구성 | 로그 재분석(이번에는 미전달) |
| 독도 같은 현상이 있었는지 | 이번 판 독 기록 미전달 — 수정 뒤 SINGLE/MULTI 확인 |
| 힐도 같은 현상이 있는지 | 이번 작업 뒤 사용자 육안(Plan 10-7 MULTI-7) |
| 사망 뒤 불 남음(H2)이 거슬리는지 | 사용자 육안 — 거슬리면 후속 |

### 12-9. 근거 파일 (이 절에서 추가로 읽은 것)

| 파일 | 확인 내용 |
|---|---|
| `Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs` | Owner 구조(13행), pose 1회 저장(106), 따라가기(114), 종료(105·126~129) |
| `Assets/_Project/Scripts/Presentation/Unit/VisualRootProjector.cs` | getter(28~53), 대역 분리(63~69), `LateUpdate`(75~79), `ProjectNow`(81~107) |
| `Assets/_Project/Scripts/Presentation/Unit/PresentationPoseProvider.cs` | `GetUnitTransform` → `PresentationTransform`(31~43) |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs` | 화상·독·힐 presenter의 pose 연결(274~295) |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` | `FollowTimedDamage`(86~92) |
| `Assets/_Project/Scripts/Presentation/Effects/BloomHealEffectPresenter.cs` | 같은 패턴(91~96) |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.DeathDeferral.cs` | 대역 분리(85·93)·파괴(126) |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 회전 속도(742) |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Burn.asset` · `Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_burn_v2.prefab` | v2 연결·옵션 꺼짐·파티클 시스템 5개 Local |
