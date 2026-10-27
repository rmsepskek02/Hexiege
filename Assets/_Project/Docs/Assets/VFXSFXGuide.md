# VFX / SFX 제작 가이드 (Unity AI Assistant)

> **목적·답하는 질문:** VFX/SFX를 어떤 스타일·파일 규약·프롬프트·연결 조건으로 제작할지 안내한다.
> **포함:** 종족별 표현 방향, 폴더·명명 규약, 복사해 전달할 게임 컨텍스트·프롬프트와 카테고리별 제작 조건, 상태효과·종족별 타워의 현행 배선 계약을 담는다.
> **제외·담당 문서:** 제작·연결·실기 수용·보류 현황은 [VFXSFXList](VFXSFXList.md), 유닛별 구조 이행·개별 수용은 [전투 감사표](UnitCombatAssetMatrix.md)가 담당한다. source 해시·컴파일·빌드 클릭·경기별 통계는 이 가이드에 쌓지 않는다.
> **권위원본·주요 참조:** 효과 제작·명명·배선 조건의 원본이며 피해·힐·동기화 동작은 [유닛 규칙](../GameSystemRules/GameSystemRules_Units.md)과 [전투 동기화 규칙](../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md), 사운드 동작은 [사운드 규칙](../GameSystemRules/GameSystemRules_Sound.md)을 따른다. 제작 프롬프트는 구현·수용 증거가 아니다.
> **갱신 조건·방법:** 승인된 표현·제작·배선 계약이 바뀌면 해당 절과 관련 템플릿을 제자리 갱신한다. 실제 연결·수용 변화는 목록에 반영하고 이전 실행 근거는 Task/History에 보존한다.

날짜별 인계·fixture·native·빌드 시도 원문은 [구현·검증 상세 이력](../History/ImplementationVerificationHistory.md#vfx-guide-handoff)에 보존한다. native PASS와 빌드 클릭은 실기 시각 수용이나 빌드 완료가 아니다.

## 게임 컨텍스트

> Unity AI에게 프롬프트를 작성할 때 아래 컨텍스트를 함께 전달한다.

**Hexiege** — 헥스 타일 기반 1v1 PvP 실시간 전략 게임 (모바일, 세로 모드 9:16)

- 플레이어는 건물 건설 + 유닛 생산으로 상대 본기지(Castle)를 파괴하는 것이 목표
- 유닛은 AI 자동 제어 — 플레이어는 전략적 배치와 생산 관리에 집중
- 타일 점령으로 영토 확장 → 인구수 확보 → 더 많은 유닛 운용
- **비주얼 스타일**: 3D 이소메트릭 카툰 (Clash of Clans / Clash Royale 레퍼런스), 밝고 선명한 색상

---

## 종족별 스타일 가이드

이펙트의 시각/청각 방향을 종족에 맞게 통일한다.

| 종족 | 시각 스타일 | 사운드 스타일 | 키워드 |
|------|-----------|------------|-------|
| **Human** | 화약, 금속, 연기, 불꽃, 기계 | 폭발음, 금속 충돌음, 기계 작동음 | gunpowder, steel, smoke, explosion |
| **Spirit** | 마법 파티클, 빛 에너지, 크리스탈, 오러 | 신비로운 마법음, 크리스탈 울림, 에너지 방출음 | magic, glowing, crystal, ethereal |
| **Transcendence** | 자연, 식물, 흙, 유기물, 동물 | 자연음, 동물 소리, 나뭇잎/흙 소리 | nature, organic, vines, earth, wild |

---

## 폴더 구조

```
Assets/_Project/
├── Prefabs/
│   └── VFX/                    ← 파티클 시스템 프리팹
│       ├── Units/              ← 유닛 이펙트
│       ├── Buildings/          ← 건물 이펙트
│       ├── Tiles/              ← 타일 점령 이펙트
│       ├── GoldMine/           ← 골드 채집 이펙트
│       ├── UI/                 ← UI 이펙트
│       └── Game/               ← 게임 시작/승리/패배 이펙트
│
├── Sprites/
│   └── Effects/                ← VFX용 스프라이트 시트 (텍스처)
│       ├── Units/
│       ├── Buildings/
│       ├── Tiles/
│       ├── GoldMine/
│       ├── UI/
│       └── Game/
│
└── Audio/
    ├── BGM/                    ← 배경음악
    ├── SFX/
    │   ├── Units/              ← 유닛 공격/사망
    │   ├── Buildings/          ← 건물 파괴/업그레이드
    │   ├── UI/                 ← UI 효과음
    │   ├── Tiles/              ← 타일 점령
    │   └── Game/               ← 게임 시작/승리/패배
    └── Ambient/                ← 환경 효과음
```

---

## 파일 명명 규칙

> 모두 **소문자**, 단어 구분은 **언더바(_)**

### VFX 파티클 프리팹

```
vfx_[에셋명]_[이벤트].prefab
```

| 예시 | 설명 |
|------|------|
| `vfx_pistoleer_attack.prefab` | Pistoleer 공격 이펙트 |
| `vfx_emberspirit_death.prefab` | EmberSpirit 사망 이펙트 |
| `vfx_castle_destroy.prefab` | Castle(Human 본기지) 파괴 이펙트 |
| `vfx_miningpost_upgrade.prefab` | MiningPost 업그레이드 이펙트 |
| `vfx_goldmine_harvest.prefab` | 골드 채집 이펙트 |
| `vfx_tile_capture.prefab` | 타일 점령 이펙트 |
| `vfx_ui_button_click.prefab` | 버튼 클릭 이펙트 |
| `vfx_game_victory.prefab` | 승리 연출 이펙트 |

### VFX 스프라이트 시트

```
vfx_[에셋명]_[이벤트]_sheet.png
```

### SFX 파일

```
sfx_[에셋명]_[이벤트].wav
```

| 예시 | 설명 |
|------|------|
| `sfx_pistoleer_attack.wav` | Pistoleer 공격 사운드 |
| `sfx_bearguard_death.wav` | BearGuard 사망 사운드 |
| `sfx_castle_destroy.wav` | Castle 파괴 사운드 |
| `sfx_trainingcamp_upgrade.wav` | TrainingCamp 업그레이드 사운드 |
| `sfx_ui_button_click.wav` | 버튼 클릭 사운드 |
| `sfx_tile_capture.wav` | 타일 점령 사운드 |
| `sfx_game_victory.wav` | 승리 사운드 |

### BGM 파일

```
bgm_[상황].wav
```

| 예시 | 설명 |
|------|------|
| `bgm_battle.wav` | 전투 중 배경음악 |
| `bgm_lobby.wav` | 로비 배경음악 |
| `bgm_victory.wav` | 승리 배경음악 |
| `bgm_defeat.wav` | 패배 배경음악 |

### Ambient 파일

```
amb_[환경].wav
```

| 예시 | 설명 |
|------|------|
| `amb_battlefield.wav` | 전장 환경음 |
| `amb_wind.wav` | 바람 환경음 |

---

## 프롬프트 작성 원칙

### 공통 원칙

**원칙 0 — 프롬프트는 영어로 작성, 내용 요약은 한글로 제공**
Unity AI에게 전달하는 프롬프트는 반드시 영어로 작성한다.
프롬프트 작성 후 해당 내용을 사용자가 이해할 수 있도록 한글로 요약하여 함께 제공한다.

```
[English Prompt]
Create a VFX particle system prefab for ...

[한글 요약]
- 어떤 이펙트인지
- 색상/느낌
- 재생 시간 등
```

**원칙 1 — 저장 경로와 파일명 항상 명시**
Unity AI가 올바른 위치에 파일을 생성하도록 경로와 이름을 명확히 지정한다.

```
저장 경로: Assets/_Project/Audio/SFX/Units/
파일명: sfx_pistoleer_attack.wav
```

**원칙 2 — 게임 컨텍스트 제공**
Unity AI가 게임 스타일에 맞는 에셋을 생성하도록 간략한 컨텍스트를 함께 전달한다.

```
이 게임은 카툰 스타일 이소메트릭 모바일 RTS 게임입니다. (Clash of Clans 레퍼런스)
```

**원칙 3 — 종족 스타일 명시**
유닛/건물이 어느 종족 소속인지, 해당 종족의 시각/청각 스타일을 함께 명시한다.

```
Human 종족 유닛입니다. 화약과 금속 느낌의 사운드로 만들어 주세요.
```

**원칙 4 — 에셋 타입 명확히 지정**
- VFX: "파티클 시스템 프리팹으로 생성해주세요" 또는 "스프라이트 시트로 생성해주세요"
- SFX: "wav 파일로 생성해주세요"

---

### VFX 프롬프트 추가 원칙

**파티클 시스템 vs 스프라이트 시트 선택 기준**

| 상황 | 권장 에셋 타입 |
|------|-------------|
| 지속적인 루프 이펙트 (골드 채집, 상시 빛남) | 파티클 시스템 프리팹 |
| 일회성 짧은 폭발 이펙트 (사망, 파괴) | 스프라이트 시트 또는 파티클 |
| 성능이 중요한 다수 동시 재생 | 스프라이트 시트 |

**모바일 최적화 명시 (항상 포함)**
```
모바일(안드로이드) 게임이므로 파티클 수를 최소화하고 성능을 최적화해 주세요.
```

**재생 방식 명시**
- 일회성 이펙트: "한 번 재생 후 자동 소멸되도록 해주세요."
- 루프 이펙트: "루프 재생되도록 해주세요."

**이펙트 지속 시간 가이드**

| 카테고리 | 권장 지속 시간 |
|---------|-------------|
| 유닛 공격 이펙트 | 0.3초 ~ 0.8초 |
| 유닛 사망 이펙트 | 0.8초 ~ 1.5초 |
| 건물 파괴 이펙트 | 1.0초 ~ 2.0초 |
| 건물 업그레이드 이펙트 | 1.0초 ~ 2.0초 |
| UI 이펙트 | 0.2초 ~ 0.5초 |
| 골드 채집 이펙트 | 루프 |
| 게임 시작/승리/패배 | 2.0초 ~ 4.0초 |

### Unity AI 프롬프트 작성 규칙 — 코드 계약 (2026-10-15 추가)

게임 코드는 프리팹의 파티클 설정을 **재생 전에 검사**하고, 어기면 그 이펙트를 아예 재생하지 않는다(예: 꽃요정 힐은 「파티클 묶음 정확히 5개」 검사 때문에 4개짜리 새 프리팹이 한 번도 재생되지 않았다). 그래서 Unity AI 프롬프트에는 아래 계약을 **매번 글로 적어** 넣는다. 출처: `_Tasks/2026-10-13/13_53_vfx-code-followups/`(Research 2·4-2·8-2·9-3·10절), `_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/`, `_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/`. 연결·수용 상태는 [VFX 목록](VFXSFXList.md)이 관리한다.

**1. 파티클 계약을 명시한다**

| 종류 | 프롬프트에 적을 계약 | 코드 근거 |
|---|---|---|
| 상태 이펙트(화상·독·힐처럼 유닛에 붙어 반복 재생) | **모든** Particle System(루트 포함) Looping ON · Stop Action None · Simulation Space Local · Burst 없음(Rate over Time만) · Unscaled Time 아님 · Scaling Mode Hierarchy · 커스텀 스크립트 없음. 파티클 묶음 **개수는 정하지 않는다**(1개 이상이면 된다) | 힐: `VfxPoolItem.PlayBloomLoop`가 0개만 거부하고 묶음마다 Looping·Stop Action·Local·Burst·Unscaled를 검사. 화상·독: `VfxPoolItem.PlayTimedDamage`가 1개 이상 + Looping·Stop Action을 검사. Hierarchy는 게임이 유닛 크기에 맞춰 프리팹 전체를 한 배율로 키우고 줄이기 위해 필요하다. 「커스텀 스크립트 없음」은 사용자·메인 세션이 정한 제작 규칙이다(재생·정지·반환은 풀 코드가 맡는다) |
| 1회성 착탄(공격이 맞는 순간 한 번) | Looping OFF · Stop Action None · 길이 약 1초 이하 · 풀에서 재사용됨을 전제(스스로 파괴하지 않는다) | 공격 VFX 재생 경로(`EffectManager.PlayAttackEffect`)가 재생 직전 모든 하위 시스템의 Stop Action을 None으로 덮어쓰고, 풀은 파티클이 모두 사라지면 반환한다. 버섯 착탄 승인본은 비루프·Stop Action None·꼬리 약 0.8초다 |

**2. 유닛에 붙는 상태 이펙트의 모양**

- 루트 pivot = **유닛 발밑**. 모든 파티클이 **반경 0.25 원기둥 안, 높이 약 1.0까지**에 머물게 한다(제작 기준).
- 🔴 **Circle·Sphere 형태에서 바깥으로 나가는 Start Speed를 주지 않는다** — 위로 움직임이 필요하면 **Velocity over Lifetime의 Y만** 쓴다. 첫 독 프리팹은 Start Speed 0.55~0.75 · 수명 약 1.2초로 약 0.9 월드 단위까지 나가 옆 칸(약 0.87)을 넘었고, 「주변 6칸으로 퍼지는 독」으로 보였다. 의도는 「이 유닛이 중독됐다」는 표시였다.
- **오래 솟구치는 상승을 피한다** — 첫 힐은 1.2~1.5초 동안 위로 올라가 너무 높았다(그 높이도 유닛 크기 배율로 함께 커진다).
- 크기·위치는 가져온 뒤 **EffectPreset**(`Local Position Offset` · `Scale Multiplier`)과 유닛별 **`UnitEffectConfig` 「상태 이펙트 크기 맞춤」 표**(에디터 메뉴 `Hexiege/Effects/Measure Unit Recipient Fit`으로 채움 — 아래 [상태 이펙트 크기 측정 도구](#상태-이펙트-크기-측정-도구-2026-10-19-추가))로 맞춘다. 크기가 조금 안 맞는 것은 프리팹을 다시 만들 이유가 아니다.
- **몸이 넓은 유닛·몸에 가려짐을 위한 프리셋 옵션 두 가지 (2026-10-26 추가, 둘 다 기본 꺼짐)** — 출처: `_Tasks/2026-10-26/18_34_status-effect-width-fit/`. 화상·독·힐 재생 경로가 모두 읽지만 **지금 켠 프리셋은 없다**(화상 v2 연결 때 둘 다 껐다).
  - **폭 맞춤**(`EffectPreset` 「Recipient Width Fit」 — 켜기 · 개수 상한 기본 3): 재생할 때 크기 표의 몸통 `radius`로 **방출이 켜진 모든 파티클 시스템**의 Shape를 몸통 폭 원판으로 바꾸고, 초당 방출 수·최대 입자 수를 필요한 장수만큼 곱한다. 바꾼 값은 풀 반환 때 원래 값으로 되돌린다(`VfxPoolItem.TimedDamage.cs` 「폭 맞춤」 절). 계산상 1장이면 아무것도 바꾸지 않는다.
  - 🔴 **몸 둘레에서 방출하도록 만든 프리팹에는 폭 맞춤을 켜지 않는다** — 설계한 Shape 반경을 폭 맞춤 원판 반경으로 덮어써 「둘레에서 타오르는」 모양이 깨지고, 넓은 유닛은 입자가 최대 상한 배수만큼 늘어난다. 폭 맞춤은 「작은 그림 한 장이 몸 가운데에서 나오는」 옛 구조용이다.
  - **카메라 쪽 당김**(「Recipient View Pull」 — 비율, 0 = 꺼짐, 권장 1.75 = 1 ÷ cos 55°): 재생 시작 때 이펙트를 카메라 쪽으로 (몸통 반경 × 비율)만큼 옮겨 몸에 가려지던 부분을 몸 앞에 그린다. 이 게임 카메라는 **직교(Orthographic)** 라서 카메라 쪽으로 옮겨도 화면 위치·크기·모양은 그대로이고 앞뒤(깊이)만 바뀐다 — 카메라를 원근으로 바꾸면 이 전제가 깨진다. 몸 둘레를 도는 프리팹에서는 뒤쪽 불꽃이 몸에 가려지는 차이가 「감싸는」 입체감을 만들므로 당김을 켜지 않았다.
- **큰 평면 그림 한 장보다 작은 조각 여러 개를 몸 둘레에서 (2026-10-27 추가)** — 옛 화상(카메라를 향한 큰 불꽃 그림 1장, Start Speed 0)은 크기·개수·위치를 코드로 맞춰도 「타오르는 불」보다 「물건이 붙어 있는」 느낌이었다. 사용자가 다시 만든 화상 v2(`vfx_infernospirit_burn_v2.prefab` — 작은 불꽃 혀가 몸 둘레 원 가장자리에서 일렁이며 올라가고 불씨·발밑 빛·옅은 연기, 파티클 시스템 5개)는 「특별히 문제 없음 · 지금 상태로도 사용 가능」으로 받아들여졌다. 다만 사용자는 화상·독·힐 모두 「전체적으로 눈에 띄는 느낌은 아니다」라고 했고, 가시성 강화는 [ROADMAP](../ROADMAP.md) 후속이다.
- **코드 계약 — 표현 pose를 저장해 매 프레임 읽는 소비자 (2026-10-27 추가):** 화상·독은 붙을 유닛의 보이는 몸(Visual Root) Transform을 한 번 얻어 두고 매 프레임 따라간다. 화면이 뒤집힌 Client에서는 그 Transform이 실제 위치 루트의 자식이라, 루트가 도는 프레임에는 `VisualRootProjector`가 바로잡기 전 틀린 자리에 있을 수 있다. 그래서 `TimedDamageEffectPresenter`는 재생 시작·따라가기 **직전에** 그 유닛의 `VisualRootProjector.ProjectNow()`를 부른다(같은 입력이면 같은 결과, Host·싱글은 결과 불변 — [동기화 규칙](../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) 「2. Simulation Root와 Visual Root」 NET-ROOT-004). 힐(`BloomHealEffectPresenter`)은 같은 구조지만 튐 보고가 없어 이 바로잡기를 넣지 않았다. 새 상태 이펙트 소비자를 만들면 같은 방식을 따른다. 이 계약은 프리팹 제작 조건이 아니라 코드 쪽 계약이다.

**3. 착탄 배치**

- 착탄 이펙트는 **대상 표면 지점**(서버 경로가 「중심 거리 − 대상 반경」으로 정한 착탄 지점 — 아래 「8. 투사체(트레이서)·착탄 프리팹과 연결 칸」의 착탄 항목 · 유닛 규칙 20)에서 재생되는 것을 전제로 만든다. 그 지점의 높이가 발밑인지는 아직 확정하지 않았다 — 끝점 높이 후속 R-A2는 사용자 결정으로 진행 안 함 · 현재 상태 유지로 종결됐다([서버 권위 투사체 Plan 10-11](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#10-11-arrow-reach-followup) · [12-8](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#12-8-closing-decisions)). *(2026-10-28 갱신 — 직전 「(… Plan 10-11 후보 R-A2)」)* *(2026-10-27 갱신 — 사용자 승인 P5-I. 종전 「착탄 이펙트는 **대상 위치의 지면**에 놓이는 것을 전제로 만든다.」)* 단일 대상 착탄은 **반경 약 0.3** 안에 들게 한다(사용자·메인 세션이 정한 제작 기준 — 코드 검사 항목은 아니다).

**4. 길이가 바뀌는 빔(번개 등)**

- 빔 본체(예: RuneSpire의 `Bolt`)는 **로컬 Z 0→1 길이**로 만든다. 코드는 그 오브젝트의 **Z 스케일만** 실제 거리로 바꾸고 X·Y는 그대로 둔다(`VfxPoolItem.TowerBolt.cs`). 끝점 불꽃(`Impact_Sparks`)은 빔 오브젝트의 **자식으로 로컬 Z = 1**에 두면 자동으로 끝점을 따라간다 — 따로 위치를 움직이게 만들지 않는다.

**5. 범위 착탄 — 보이는 범위·바닥 텍스처·먼지 (2026-10-19 추가)**

출처: QuakeSpirit 공격 VFX 재제작(사용자 승인 2026-10-19, `_Tasks/2026-10-13/13_53_vfx-code-followups/` Research 13절). 위 3번의 「단일 대상 반경 약 0.3」은 단일 대상 기준이며, 범위 공격은 아래를 따른다.

- **「한 타일 범위」가 무엇인지 먼저 정한다.** 헥스 수치: 타일 폭 약 0.866 · 이웃 칸 중심까지 약 0.78~0.87 · 이웃 칸 바깥 가장자리 약 1.3. 「맞은 칸 + 이웃 6칸 전체」·「이웃 칸 중심까지」·「실제 피해 반경」(예: QuakeSpirit 1.0 — [StatsReference](../StatsReference.md)) 중 무엇인지에 따라 크기가 크게 달라진다. 보이는 범위가 피해 반경보다 넓어도 되는지는 사용자에게 확인하고 결정을 기록한다(QuakeSpirit은 약 1.6 — 사용자 선택).
- **바닥에 까는 텍스처는 투명 배경과 구도를 글로 명시한다.** 「투명 배경에 6갈래 균열 한 장」만 요청하면 처음에는 **갈라진 사각형 석판**이 나왔다. 「배경 완전 투명(알파 0) · 중심에서 바깥으로 뻗는 N갈래 · 바깥 테두리는 비어 있음」처럼 적고, 결과 PNG가 실제로 투명한지 확인한다.
- **크기를 정하기 전에 텍스처를 잰다.** 균열 끝이 텍스처 반폭의 몇 %에 있는지(알파 기준)를 재서 Start Size를 계산한다 — QuakeSpirit은 끝이 약 95% 지점이라 Start Size 3.4 → 끝 약 1.6이었다.
- **먼지는 「부드러운 연기 텍스처의 중간 크기 퍼프가 천천히 부풀며 퍼지는」 방식으로 만든다.** 큰 파티클 몇 개에 먼지 덩어리 그림을 한 장씩 실으면 「도장 찍힌 그림이 미끄러지는」 것처럼 보였고, 아주 작은 알갱이는 점으로만 보였다. 승인된 예: QuakeSpirit `Dust_Puffs`(공용 `Mat_VFX_Smoke` + Start Color로 갈색 착색, 크기 0.6~0.8에서 약 1.3배로 부풀음, Limit Velocity로 감속) · `vfx_cannon_impact.prefab`의 `Smoke_Puff`(같은 머티리얼).
- **공용 머티리얼은 고치지 말고 Start Color로 색을 입힌다** — 다른 이펙트가 같은 머티리얼을 쓴다(`Mat_VFX_Smoke`는 여러 프리팹이 참조).
- **Scaling Mode가 Local인 레이어는 루트·preset의 `Scale Multiplier`를 따르지 않는다** — 그런 레이어는 레이어마다 따로 조정해야 한다. 전체를 한 번에 키우고 줄이려면 모든 레이어를 Hierarchy로 둔다(QuakeSpirit 최종본은 전부 Hierarchy).
- 공격자 방향을 따라 회전하는 공격 슬롯(`EffectPreset_*_Attack`)에서 재생되므로, 균열 갈래 방향은 월드 헥스 격자가 아니라 **공격 방향**을 따른다. 격자에 맞춘 모양이 필요하면 제작 전에 코드 쪽 재생 방식을 따로 확인한다.

**6. 공용 재질의 텍스처 색은 파티클 색과 곱해진다 (2026-10-19 추가)**

출처: InfernoSpirit 화염구·착탄 불티 초록빛 수정(사용자 확인 2026-10-19, `_Tasks/2026-10-19/11_09_charge-unit-launch-timing/` Research 9-3).

- 파티클에 보이는 색 = 레이어 색(Start Color · Color over Lifetime) × **재질 텍스처의 색**이다. 텍스처가 흰색·회색이 아니면 레이어 색을 아무리 주황으로 줘도 그 텍스처 색이 섞인다.
- `Mat_VFX_SharpSpark`의 텍스처는 RuneSpire 번개용 **청록색** `vfx_sharp_spark.png`이다. 따뜻한 색(불·불티·황금) 이펙트에 쓰면 **초록빛**으로 보인다(Inferno `Ember_Trail`·`Embers`가 그랬다). 따뜻한 색 불티에는 흰색 텍스처 재질 **`Mat_VFX_GlowAdditive`**(`vfx_soft_glow.png`)를 쓰고 색은 레이어 색으로 입힌다.
- 프롬프트·수작업 모두 공용 재질을 고를 때 **그 재질의 텍스처가 무슨 색인지 먼저 확인**한다. 재질 이름(「Spark」)만 보고 고르지 않는다. 공용 재질 자체는 고치지 않는다(위 5번 — 다른 이펙트가 같이 쓴다).

**7. 차지(기 모으기) 프리팹의 길이 = 차지 구간 (2026-10-19 추가)**

출처: InfernoSpirit·FoxMagician 차지→발사 시점 작업(사용자 확인 2026-10-19, 같은 Task Plan 9-2). 규칙: [유닛 규칙 17](../GameSystemRules/GameSystemRules_Units.md) 「차지 유닛 typed marker 계약」.

- 차지 이펙트는 공격 클립의 `OnAttackCharge`(차지 시작)에서 켜지고, `OnAttackHit`(발사)에서는 **다시 켜지지 않는다.** 코드가 따로 끄지도 않으므로 프리팹이 **발사 순간에 스스로 끝나야** 한다. 길이 = 클립의 (`OnAttackHit` − `OnAttackCharge`) — 현재 InfernoSpirit 1.3333초, FoxMagician 1.8333초.
- 🔴 **자식 파티클 시스템까지 끝 시각을 계산한다.** 한 시스템이 실제로 끝나는 시각 = 시작 지연 + 마지막 방출 시각 + 최대 수명(재생 속도로 나눔)이다. 초당 방출(rate over time)이 있으면 마지막 방출은 Duration 끝, 시작 순간 burst만 있으면 burst 시각이다. 그래서 「Duration = 차지 구간」만 맞추면 꼬리가 넘친다 — Fox `Wisps`는 Duration 2 + 수명 최대 1.0이라 약 3초까지 남았고, Duration 0.8333 + 수명 1.0 = 1.8333으로 맞췄다. 자동 검증이 이 끝 시각을 차지 구간과 비교한다(구간 이하 · 구간 끝 1프레임 안까지 이어짐).
- 「점점 모이는」 느낌은 Size over Lifetime·rate 곡선처럼 **정규화 곡선**으로 만든다. Duration을 줄이면 같은 곡선이 짧은 시간에 압축되며, 초당 방출량을 그대로 두면 **입자 총수는 Duration에 비례해 줄어든다**(Fox `Wisps`는 약 2.4배 감소 — 사용자 이의 없음).
- 차지 이펙트 preset은 「Stop When Caster Dies」를 켠다 — 시전자 사망·제거, 그리고 발사 전 공격 취소 때 코드가 즉시 끈다.

**8. 투사체(트레이서)·착탄 프리팹과 연결 칸 (2026-10-21 추가)**

출처: StreamSpirit·Tank·CannonCart tracer/impact 연결(`_Tasks/2026-10-13/13_53_vfx-code-followups/` Research 14-4·15-2·15-3), 서버 권위 투사체 단계 A~C(`_Tasks/2026-10-19/13_30_server-authoritative-projectile/`). 규칙: [유닛 규칙 20](../GameSystemRules/GameSystemRules_Units.md) 「원거리 유닛 트레이서」.

- **트레이서 프리팹 계약:** Looping ON · Stop Action None · 몸체 Local / 꼬리(trail) World · **+Z가 앞 방향** · 커스텀 스크립트 없음(Research 14-4).
- **착탄 프리팹은 `impactPreset` 칸에 연결한다. `hitPreset`에 넣지 않는다** — `hitPreset`은 **맞은 유닛의 종류**로 고르는 칸이라, StreamSpirit 물 튀김을 거기 넣었더니 StreamSpirit이 때릴 때는 안 나오고 **맞을 때** 나왔다(Research 15-3). preset 이름(`EffectPreset_StreamSpirit_Hit`)과 칸 이름은 따로 확인한다 — 지금 그 preset은 impactPreset 칸에 있다.
- **비행 속도는 프리팹이 아니라 서버 설정이 정한다.** 서버 권위 투사체로 옮긴 유닛·타워는 `Assets/_Project/Resources/Config/ProjectileConfig.asset`의 속도로 서버가 착탄 시각을 정하고, 화면 트레이서는 그 시각에 맞춰 대상을 따라간다. 이 경로에서는 tracer 프리팹의 `_speed`를 쓰지 않는다(`TracerProjectile.cs` 서버 경로 주석). 「너무 빠르다/느리다」는 프리팹이 아니라 `ProjectileConfig`에서 고친다. 값은 [StatsReference](../StatsReference.md) 「투사체 속도」 열.
- **착탄은 대상 표면 지점에서 재생된다** — 서버 경로에서는 비행 거리를 「중심 거리 − 대상 반경」으로 계산해 착탄 이펙트도 그 표면 지점에서 낸다(규칙 20 「서버 착탄 시각·표면 지점」, 반경은 `ProjectileConfig`의 유닛·건물 반경 표). 첫 StreamSpirit 착탄은 「작고 짧다」는 지적이 있었고 키우는 값은 제안만 했다(Research 15-3 2번 — 적용 여부 사용자).
- 투사체 유닛 7종(StreamSpirit · Tank · CannonCart · InfernoSpirit · FoxMagician · EagleArcher · MushroomBomber)은 모두 서버 경로다 — InfernoSpirit·FoxMagician은 단계 D `a0a23423`부터, EagleArcher·MushroomBomber는 단계 E `5b8fb82e`부터(분류 `ProjectileDeliveryContracts.cs` `ProjectileUnitTypes`). 이행 기간에 쓰던 로컬 트레이서 경로의 R5 임시 연결(트레이서 도착 지점에서 착탄)은 P5-1 `9cefe80d`에서 삭제됐다. *(2026-10-27 P5 갱신 — 종전 「서버 경로로 옮기기 전 유닛은 로컬 트레이서 경로의 R5 임시 연결(`UnitView.cs` `playImpactOnArrive: true`)대로 트레이서 도착 지점에서 착탄이 나온다(`impactPreset`이 없는 종류는 변화 없음). 지금 서버 경로가 아닌 투사체 유닛은 없다 — … (스위치 `ProjectileDeliveryContracts.cs` `EnabledUnitTypes`).」)* EagleArcher는 팀별 화살 tracer(Red 팀은 `UnitEffectConfig` `redTeamTracerPreset`)를 쓰고 착탄 이펙트가 없다. *(2026-10-27 갱신 — 종전 「지금 서버 경로가 아닌 투사체 유닛은 EagleArcher · MushroomBomber이며, MushroomBomber는 로컬 트레이서 호출이 꺼져 있어 서버 writer 공격 표현 스냅샷이 트레이서·착탄을 맡는다([유닛 규칙 27](../GameSystemRules/GameSystemRules_Units.md) 아래 블록). InfernoSpirit·FoxMagician은 단계 D `a0a23423`부터 서버 경로다.」)* 연결 상태는 [VFX 목록](VFXSFXList.md)이 관리한다. *(2026-10-26 교정 — 사용자 승인 「지금 동작에 맞춰서 수정」. 종전: 「서버 경로로 옮기기 전 유닛(InfernoSpirit·FoxMagician — 단계 D 진행 중)은 R5 임시 연결대로 트레이서 도착 지점에서 착탄이 나온다.」)*

#### 상태 이펙트 크기 측정 도구 (2026-10-19 추가)

화상·독·힐처럼 유닛에 붙는 이펙트의 크기를 유닛 몸집에 맞추려면 먼저 유닛별 몸 크기 표가 있어야 한다. 이 표를 채우는 에디터 도구다.

- **파일·메뉴:** `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs` · 메뉴 `Hexiege/Effects/Measure Unit Recipient Fit`.
- **무엇을 쓰나:** 생산 유닛 프리팹의 몸을 재서 `Assets/_Project/Resources/Config/UnitEffectConfig.asset`의 「상태 이펙트 크기 맞춤」 표(`_recipientFits`)에 유닛별 `radius`(반경)·`height`(키)·`footOffset`(발밑 가운데 보정)을 쓴다. Inspector에서 넣은 `manualScale`(수동 배율)은 지우지 않고, 표에 없던 유닛은 `manualScale` 0(자동)으로 새 줄을 만든다. 실행 전에 확인 창을 띄우며 프리팹·씬은 수정하지 않는다. 게임은 이 표만 읽는다.
- **언제 다시 실행하나:** 유닛 모델·프리팹(중간 메시 포함)의 스케일이 바뀌었을 때, 새 유닛 종류가 추가됐을 때. 이펙트 크기를 조금 바꾸고 싶을 때는 다시 재지 않고 EffectPreset의 `Scale Multiplier` · `Local Position Offset`이나 `manualScale`로 맞춘다.
- **정상 값 확인:** Pistoleer 키 약 0.34m(현재 표 0.342). 키가 1.2m를 넘거나, 반경이 0.43m(헥스 타일 폭의 절반)를 넘거나, 키가 0.1m보다 작으면 도구가 측정 오류 의심 경고를 남긴다. 사람형 유닛이 3~5배로 나오면 2026-10-13 스케일 버그(중간 메시 스케일 누락, `84b6ff2a`에서 수정)와 같은 모양이므로 표를 쓰기 전에 의심한다.
- **유지 근거:** 이 도구는 WORKFLOW [5-2] 「에디터 스크립트 최소 유지 기준」의 유지 대상인 `Tools/Combat/RunUnitActionSelfValidation.cs`(`Hexiege/Combat/Run Full Regression Suite`)의 **실제 내부 의존**이다 — 그 메뉴가 부르는 `RunUnitActionSelfValidation.VfxConnections.cs`가 이 도구의 측정 함수와 의심 기준값을 직접 써서 Pistoleer 키 0.2~0.6m·Tank가 Pistoleer보다 큼을 검사한다. 그래서 WORKFLOW 진입점 표에 따로 올리지 않는다(2026-10-19 사용자 결정).

---

### SFX 프롬프트 추가 원칙

**오디오 사양 명시 (항상 포함)**
```
wav 파일, 모노(Mono), 44100Hz, 16bit
```
> 배경음악(BGM)만 스테레오(Stereo)로 지정.

**오디오 길이 가이드**

| 카테고리 | 권장 길이 |
|---------|---------|
| UI 효과음 | 0.5초 이내 |
| 유닛 공격음 | 0.3초 ~ 1.0초 |
| 유닛 사망음 | 0.5초 ~ 1.5초 |
| 건물 파괴음 | 1.0초 ~ 2.0초 |
| 건물 업그레이드음 | 0.8초 ~ 1.5초 |
| 타일 점령음 | 0.5초 ~ 1.0초 |
| 게임 이벤트음 | 1.0초 ~ 3.0초 |
| 배경음악 | 1분 ~ 3분 (루프 가능하게) |
| 환경 효과음 | 10초 ~ 30초 (루프) |

---

## VFX 카테고리별 가이드

### 1. 유닛 공격 이펙트

> 각 유닛의 공격 방식(근접/원거리/마법)과 종족 스타일에 맞게 제작.

**프롬프트 예시 — Human / Pistoleer (권총 공격)**
```
카툰 스타일 이소메트릭 RTS 게임용 VFX를 만들어주세요.

Pistoleer(권총병) 유닛의 공격 이펙트 파티클 시스템 프리팹을 생성해주세요.
- Human 종족: 화약 연기와 불꽃 느낌
- 총구에서 작은 불꽃(muzzle flash)과 연기가 터지는 이펙트
- 재생 시간: 약 0.3초, 한 번 재생 후 자동 소멸
- 모바일 최적화 (파티클 수 최소화)
- 저장 경로: Assets/_Project/Prefabs/VFX/Units/
- 파일명: vfx_pistoleer_attack.prefab
```

**프롬프트 예시 — Spirit / EmberSpirit (마법 공격)**
```
카툰 스타일 이소메트릭 RTS 게임용 VFX를 만들어주세요.

EmberSpirit(불정령) 유닛의 공격 이펙트 파티클 시스템 프리팹을 생성해주세요.
- Spirit 종족: 마법 파티클과 빛나는 불꽃 에너지 느낌
- 작은 불꽃 에너지 구가 발사되는 이펙트
- 색상: 주황~붉은 계열, 빛나는 느낌
- 재생 시간: 약 0.5초, 한 번 재생 후 자동 소멸
- 모바일 최적화 (파티클 수 최소화)
- 저장 경로: Assets/_Project/Prefabs/VFX/Units/
- 파일명: vfx_emberspirit_attack.prefab
```

**유닛별 공격 이펙트 참고 키워드**

| 유닛 | 종족 | 공격 방식 | VFX 키워드 |
|------|------|---------|----------|
| Pistoleer | Human | 권총 | muzzle flash, gunpowder smoke |
| Assault | Human | 돌격소총 | rapid muzzle flash, bullet shell |
| Sniper | Human | 저격 | bright muzzle flash, dust |
| LittleKnight | Human | 근접 검 | metal slash, spark |
| SpearMan | Human | 창 찌르기 | pierce impact, dust |
| BattleAxe | Human | 도끼 내려치기 | heavy slash, ground crack |
| Tank | Human | 포탄 | cannon blast, explosion, smoke |
| CannonCart | Human | 범위 포격 | area explosion, fire, smoke |
| EmberSpirit | Spirit | 불 마법 | fire orb, flame particle |
| FlameSpirit | Spirit | 불 마법 | fire burst, ember |
| InfernoSpirit | Spirit | 불 마법 | inferno blast, lava |
| DustSpirit | Spirit | 흙 마법 | earth particle, dust cloud |
| BoulderSpirit | Spirit | 흙 마법 | rock throw, boulder |
| QuakeSpirit | Spirit | 광역 지진 | earthquake crack, stone shatter |
| TideSpirit | Spirit | 물 마법 | water splash, droplet |
| StreamSpirit | Spirit | 물 마법 | water stream, bubble |
| TorrentSpirit | Spirit | 물 마법 | water torrent, wave |
| FoxMagician | Transcendence | 마법 | sparkle, illusion, fox wisp |
| BearGuard | Transcendence | 근접 | heavy slam, ground shake, paw |
| LionKnight | Transcendence | 근접 검 | claw slash, wind cut |
| RhinoBreaker | Transcendence | 돌진 | charge dust, impact shockwave |
| EagleArcher | Transcendence | 화살 | arrow trail, feather |
| RabbitTrickster | Transcendence | 단검 | quick slash, motion blur |
| MushroomBomber | Transcendence | 포자 투척·짧은 착탄 확산 | short spore diffusion burst; recipient poison loop (no lingering ground damage zone) |
| BloomFairy | Transcendence | 힐 | petal heal, bloom glow |

---

### 1-a. 상태효과 및 종족별 타워 연결 계약

> Mushroom impact는 nonloop·StopAction None이며 초기 particle tail budget 약0.8초는 실제 렌더 종료·natural drain 수용값이 아니다. 서버 DoT3초를 표현 길이로 바꾸지 않는다. 당시 준비·검증 단계는 [asset freeze 보고](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-guide-01)에 보존하며 현재 연결·수용은 [VFX 목록](VFXSFXList.md)을 따른다.

> **현행 제작/연결 계약:** Mushroom impact는 짧은 포자 확산 burst다. burst 길이·입자 수는 임의 확정하지 않으며 서버 DoT3초와 recipient poison/burn 기간·갱신·취소·사망·StopEmitting 후 drain 계약을 유지한다. Human AutoTower는 별도 preset의 actual Assault VFX를 참조하고 Tank/CannonCart는 actual Tank를 유지한다. Spirit은 RuneSpire, Transcendence는 미지정이며 타 종족 fallback 금지다. 선택 키는 단일 AutoTower 타입 + Race다. 구현·native·실기 수용은 [VFX 목록](VFXSFXList.md)에서 별도 관리하고, 이전 Tank 재사용 계약은 [당시 원문](../History/ImplementationVerificationHistory.md#vfx-guide-tower-contract-history)에 보존한다.

### 2. 유닛 사망 이펙트

> 유닛이 사망했을 때 재생되는 이펙트. 종족 스타일에 맞는 "사라지는 방식"으로 표현.

**종족별 사망 표현 방향**

| 종족 | 사망 표현 방향 |
|------|-------------|
| Human | 쓰러지며 연기/불꽃 발생 |
| Spirit | 빛 파티클이 흩어지며 사라짐 |
| Transcendence | 자연으로 돌아가는 느낌 (낙엽, 흙, 빛) |

**프롬프트 예시 — Spirit / EmberSpirit 사망**
```
카툰 스타일 이소메트릭 RTS 게임용 VFX를 만들어주세요.

EmberSpirit(불정령) 유닛의 사망 이펙트 파티클 시스템 프리팹을 생성해주세요.
- Spirit 종족: 빛나는 불꽃 파티클이 사방으로 흩어지며 사라지는 이펙트
- 마치 불꽃이 소멸되는 듯한 표현
- 색상: 주황~붉은 계열
- 재생 시간: 약 1.0초, 한 번 재생 후 자동 소멸
- 모바일 최적화
- 저장 경로: Assets/_Project/Prefabs/VFX/Units/
- 파일명: vfx_emberspirit_death.prefab
```

---

### 3. 건물 파괴 이펙트

> 건물 HP가 0이 되어 파괴될 때 재생. 큰 폭발감 있게 표현.

**종족별 파괴 표현 방향**

| 종족 | 파괴 표현 방향 |
|------|-------------|
| Human | 폭발, 연기, 파편, 불꽃 |
| Spirit | 크리스탈 파편이 흩어지며 에너지 폭발 |
| Transcendence | 나무/식물 파편, 흙먼지, 낙엽 |

**프롬프트 예시 — Human / Castle 파괴**
```
카툰 스타일 이소메트릭 RTS 게임용 VFX를 만들어주세요.

Human 종족 Castle(본기지 건물) 파괴 이펙트 파티클 시스템 프리팹을 생성해주세요.
- Human 종족: 큰 폭발과 연기, 돌 파편이 튀는 이펙트
- 건물이 무너지며 폭발하는 느낌 (카툰 과장 표현)
- 색상: 주황 불꽃, 회색 연기, 갈색 파편
- 재생 시간: 약 2.0초, 한 번 재생 후 자동 소멸
- 모바일 최적화
- 저장 경로: Assets/_Project/Prefabs/VFX/Buildings/
- 파일명: vfx_castle_destroy.prefab
```

---

### 4. 건물 업그레이드 이펙트

> 건물이 업그레이드될 때 재생. 긍정적이고 밝은 느낌.

**종족별 업그레이드 표현 방향**

| 종족 | 업그레이드 표현 방향 |
|------|-----------------|
| Human | 기계 작동음과 함께 불꽃/광선 |
| Spirit | 빛나는 에너지가 건물을 감싸는 표현 |
| Transcendence | 식물이 자라나거나 빛이 퍼지는 표현 |

---

### 5. 골드 채집 이펙트

> Gold Mine에서 골드가 채집될 때 지속적으로 재생되는 루프 이펙트.

**프롬프트 예시**
```
카툰 스타일 이소메트릭 RTS 게임용 VFX를 만들어주세요.

Gold Mine에서 금이 채집되는 루프 이펙트 파티클 시스템 프리팹을 생성해주세요.
- 반짝이는 금색 파티클이 위로 떠오르는 이펙트
- 밝고 긍정적인 느낌, 카툰 스타일
- 색상: 골드/노란색 계열
- 루프 재생 (소멸 없이 지속)
- 모바일 최적화
- 저장 경로: Assets/_Project/Prefabs/VFX/GoldMine/
- 파일명: vfx_goldmine_harvest.prefab
```

---

### 6. UI 이펙트

> 버튼 클릭, 생산 완료 알림, 골드 획득 등 UI 상호작용 이펙트.

**UI 이펙트 목록**

| 이펙트명 | 파일명 | 설명 |
|---------|-------|------|
| 버튼 클릭 | `vfx_ui_button_click.prefab` | 버튼 클릭 시 반짝이는 효과 |
| 버튼 확인 | `vfx_ui_button_confirm.prefab` | 확인/구매 버튼 클릭 시 |
| 골드 획득 | `vfx_ui_gold_gain.prefab` | 골드 증가 시 반짝임 |
| 타이머 완료 | `vfx_ui_timer_complete.prefab` | 카운트다운 완료 시 |

**프롬프트 예시 — 버튼 클릭**
```
카툰 스타일 모바일 RTS 게임용 UI VFX를 만들어주세요.

버튼 클릭 시 재생되는 UI 이펙트 파티클 시스템 프리팹을 생성해주세요.
- 클릭 위치에서 작은 반짝임(spark)이 터지는 이펙트
- 밝고 경쾌한 카툰 느낌
- 색상: 밝은 노란/흰색 계열
- 재생 시간: 약 0.3초, 한 번 재생 후 자동 소멸
- 모바일 최적화 (매우 간단하게)
- 저장 경로: Assets/_Project/Prefabs/VFX/UI/
- 파일명: vfx_ui_button_click.prefab
```

---

### 7. 타일 점령 이펙트

> 플레이어가 헥스 타일을 점령할 때 재생. 팀 색상(Blue/Red)에 맞게 제작.

**프롬프트 예시 — Blue 팀 점령**
```
카툰 스타일 이소메트릭 RTS 게임용 VFX를 만들어주세요.

헥스 타일 점령 이펙트 파티클 시스템 프리팹을 생성해주세요. (Blue 팀)
- 타일 위에서 파란색 에너지/빛이 퍼지는 이펙트
- 점령 완료의 느낌, 밝고 경쾌하게
- 색상: 파란색 계열
- 재생 시간: 약 0.8초, 한 번 재생 후 자동 소멸
- 모바일 최적화
- 저장 경로: Assets/_Project/Prefabs/VFX/Tiles/
- 파일명: vfx_tile_capture_blue.prefab
```

> Red 팀: 색상만 빨간색으로 변경, 파일명 `vfx_tile_capture_red.prefab`

---

### 8. 게임 이벤트 이펙트 (시작 / 승리 / 패배)

> 게임 시작, 승리, 패배 화면에 사용되는 연출 이펙트.

| 이펙트 | 파일명 | 표현 방향 |
|--------|-------|---------|
| 게임 시작 | `vfx_game_start.prefab` | 에너지가 폭발하며 시작되는 느낌 |
| 승리 | `vfx_game_victory.prefab` | 화려한 축하 폭죽, 별, 빛 |
| 패배 | `vfx_game_defeat.prefab` | 어둡고 연기가 피어오르는 느낌 |

---

## SFX 카테고리별 가이드

### 1. 유닛 공격 사운드

> 각 유닛의 공격 방식에 맞는 사운드. 같은 근접 공격이라도 종족별로 다르게.

**유닛별 공격 사운드 참고 키워드**

| 유닛 | 종족 | SFX 키워드 |
|------|------|----------|
| Pistoleer | Human | gun shot, pistol fire |
| Assault | Human | rapid gunfire, automatic rifle |
| Sniper | Human | single powerful shot, sniper |
| LittleKnight | Human | metal sword swing, slash |
| SpearMan | Human | spear thrust, pierce |
| BattleAxe | Human | heavy axe swing, impact |
| Tank | Human | cannon fire, heavy explosion |
| CannonCart | Human | cannon blast, rolling |
| EmberSpirit | Spirit | fire whoosh, magical flame |
| FlameSpirit | Spirit | fire burst, roaring flame |
| InfernoSpirit | Spirit | massive fire explosion, inferno |
| DustSpirit | Spirit | earth rumble, sand rush |
| BoulderSpirit | Spirit | heavy rock throw, thud |
| QuakeSpirit | Spirit | earthquake boom, shatter |
| TideSpirit | Spirit | water splash, wave |
| StreamSpirit | Spirit | water rush, bubble pop |
| TorrentSpirit | Spirit | powerful water surge |
| FoxMagician | Transcendence | magical sparkle, mystical |
| BearGuard | Transcendence | heavy slam, bear growl |
| LionKnight | Transcendence | claw slash, lion roar |
| RhinoBreaker | Transcendence | charge stomp, heavy impact |
| EagleArcher | Transcendence | arrow release, bowstring |
| RabbitTrickster | Transcendence | quick blade swipe |
| MushroomBomber | Transcendence | spore throw, toxic pop |
| BloomFairy | Transcendence | gentle heal chime, flower bloom |

**프롬프트 예시 — Human / Pistoleer 공격**
```
카툰 스타일 이소메트릭 모바일 RTS 게임용 SFX를 만들어주세요.

Pistoleer(권총병) 유닛의 공격 사운드를 생성해주세요.
- 권총 한 발 발사음, 카툰 스타일 (과장되고 경쾌하게)
- Human 종족: 금속/화약 느낌
- 재생 시간: 약 0.5초
- wav 파일, 모노(Mono), 44100Hz, 16bit
- 저장 경로: Assets/_Project/Audio/SFX/Units/
- 파일명: sfx_pistoleer_attack.wav
```

---

### 2. 유닛 사망 사운드

> 유닛이 사망할 때 재생. 종족별 "사라지는 느낌"에 맞게.

| 종족 | 사망 사운드 방향 |
|------|--------------|
| Human | 쓰러지는 소리, 마지막 고함 |
| Spirit | 에너지가 소산되는 마법음 |
| Transcendence | 동물 울음소리가 잦아드는 느낌 |

---

### 3. 건물 파괴 사운드

> 건물이 파괴될 때 재생. 건물 크기와 종족에 맞게 무게감 조절.

| 종족 | 파괴 사운드 방향 |
|------|--------------|
| Human | 폭발음, 돌/철 무너지는 소리 |
| Spirit | 크리스탈 깨지는 소리 + 에너지 방출음 |
| Transcendence | 나무 부서지는 소리, 흙 무너지는 소리 |

---

### 4. 건물 업그레이드 사운드

> 건물 업그레이드 완료 시 재생. 긍정적이고 성취감 있는 사운드.

| 종족 | 업그레이드 사운드 방향 |
|------|------------------|
| Human | 기계 작동음 + 완성 벨/차임 |
| Spirit | 마법 에너지 충전 + 크리스탈 울림 |
| Transcendence | 자연 성장 소리 + 밝은 자연음 |

---

### 5. UI 사운드

> 버튼 클릭, 메뉴 열기/닫기 등 UI 상호작용 사운드. 매우 짧고 경쾌하게.

| 사운드 | 파일명 | 방향 |
|--------|-------|------|
| 일반 버튼 클릭 | `sfx_ui_button_click.wav` | 경쾌한 클릭음 |
| 확인/구매 버튼 | `sfx_ui_button_confirm.wav` | 밝은 확인음 |
| 취소/닫기 버튼 | `sfx_ui_button_cancel.wav` | 짧은 닫힘음 |
| 패널 열기 | `sfx_ui_panel_open.wav` | 슬라이드 열림음 |
| 패널 닫기 | `sfx_ui_panel_close.wav` | 슬라이드 닫힘음 |
| 오류/불가 | `sfx_ui_error.wav` | 거절/오류음 |

---

### 6. 타일 점령 사운드

> 플레이어가 헥스 타일을 점령할 때 재생.

**프롬프트 예시**
```
카툰 스타일 모바일 RTS 게임용 SFX를 만들어주세요.

헥스 타일 점령 완료 사운드를 생성해주세요.
- 영토를 확보하는 느낌의 짧고 경쾌한 사운드
- 카툰 스타일 (밝고 긍정적)
- 재생 시간: 약 0.8초
- wav 파일, 모노(Mono), 44100Hz, 16bit
- 저장 경로: Assets/_Project/Audio/SFX/Tiles/
- 파일명: sfx_tile_capture.wav
```

---

### 7. 배경음악 (BGM)

> 게임 씬과 로비에 사용되는 루프 배경음악.

| BGM | 파일명 | 분위기 |
|-----|-------|-------|
| 전투 | `bgm_battle.wav` | 긴장감, 박진감, 카툰 RTS 전투 |
| 로비 | `bgm_lobby.wav` | 가볍고 밝음, 대기 분위기 |
| 승리 | `bgm_victory.wav` | 밝고 화려한 팡파레, 루프 가능 |
| 패배 | `bgm_defeat.wav` | 조금 어둡고 무거운 느낌 |

**프롬프트 예시 — 전투 BGM**
```
카툰 스타일 이소메트릭 모바일 RTS 게임용 배경음악을 만들어주세요.

전투 씬에서 루프 재생될 배경음악을 생성해주세요.
- Clash of Clans / Clash Royale 느낌의 카툰 RTS 전투 음악
- 긴장감 있고 박진감 넘치되 너무 어둡지 않게
- 루프 포인트가 자연스럽게 이어지도록 (seamless loop)
- 재생 시간: 약 1~2분
- wav 파일, 스테레오(Stereo), 44100Hz, 16bit
- 저장 경로: Assets/_Project/Audio/BGM/
- 파일명: bgm_battle.wav
```

---

### 8. 환경 효과음 (Ambient)

> 게임 씬 전반에 깔리는 환경 사운드. 전장의 생동감을 더해줌.

| Ambient | 파일명 | 방향 |
|---------|-------|------|
| 전장 환경음 | `amb_battlefield.wav` | 멀리서 들리는 전투음, 바람 |
| 바람 | `amb_wind.wav` | 바람 소리, 루프 |

---

### 9. 게임 이벤트 사운드 (시작 / 승리 / 패배)

| 사운드 | 파일명 | 방향 |
|--------|-------|------|
| 게임 시작 | `sfx_game_start.wav` | 긴장감 있는 시작 신호음 |
| 승리 | `sfx_game_victory.wav` | 화려하고 밝은 승리 팡파레 |
| 패배 | `sfx_game_defeat.wav` | 묵직하고 아쉬운 패배음 |

---

## 보류 카테고리

> 현재는 제작하지 않으며, 추후 필요 시 이 문서에 추가한다.

| 카테고리 | 보류 이유 |
|---------|---------|
| 유닛 피격 이펙트 + 사운드 | 추후 추가 예정 |
| 유닛 생산 완료 이펙트 + 사운드 | 추후 추가 예정 |
| 건물 건설 이펙트 + 사운드 | 추후 추가 예정 |
