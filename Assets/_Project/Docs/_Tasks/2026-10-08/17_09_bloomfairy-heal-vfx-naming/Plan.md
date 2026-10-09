# Plan — BloomFairy 힐 이펙트 이름 정리 (attack → heal)

## 이 작업은 무엇이고 왜 하는가

꽃요정(BloomFairy)의 치료 이펙트를 Unity AI 로 만들기 전에, 이펙트 목록 문서(`VFXSFXList.md`)에 적힌 꽃요정 이펙트 이름을
「공격(attack)」에서 「치료(heal)」로 바꾼다. 꽃요정은 공격하지 않는 치료 유닛이고, 만들 이펙트도 공격 섬광이 아니라
**치료받는 아군에게 붙어 치료가 이어지는 동안 반복되는 이펙트**이기 때문이다.

바꾸는 것은 **문서 1개의 2행**(VFX 이름 1개, SFX 이름 1개)뿐이다. 실제 파일은 아직 없고, 코드·에셋 어디에서도 이 이름을
쓰지 않으므로(Research.md 1절) 이름을 바꿔도 깨지는 곳이 없다. 반면 **애니메이션과 코드의 「Attack」은 그대로 둔다** —
게임이 모든 유닛의 공격 동작을 「Attack」이라는 공통 이름으로 찾기 때문이다(Research.md 2절).

결정이 하나 남아 있다. 이름만 바꿔서 지금 자리(「유닛 공격 이펙트」 표)에 둘지, 성격이 다른 이펙트이니 따로 칸을 만들어 옮길지다.
아래 「미결 사항」에 두 방법을 나란히 적었다. **사용자가 고른 뒤에 구현한다.**

---

## ⚠️ 기존 로직 제거 — 없음

이번 작업은 문서의 이름 2개를 바꾸는 것이며 **코드·로직을 제거하거나 비활성화하는 항목이 없다**(WORKFLOW.md [4] 「기존 로직 제거 규칙」 해당 없음).
미결 사항에서 방법 B 를 고르면 표의 행 2개가 다른 표로 **이동**하지만, 이는 문서 행의 위치 이동이지 로직 제거가 아니다.

---

## 1. 근거 규칙

WORKFLOW.md [4] 에 따라 각 항목의 근거를 적는다. **에셋 파일 이름을 정하는 규칙은 `GameSystemRules` 에 없다** — 이름의 형식은
에셋 가이드 문서가 정한다. 그래서 아래 표는 「직접 근거」와 「관련 규칙」을 구분해 적는다.

| 항목 | 직접 근거 | 관련 GameSystemRules |
|---|---|---|
| VFX 이름 변경 | 사용자 결정(이펙트 에셋에서 꽃요정 attack → heal) + `VFXSFXGuide.md` 「파일 명명 규칙」 `vfx_[에셋명]_[이벤트].prefab` — `heal` 은 「이벤트」 자리에 들어가며 규칙 안이다 | 이름을 정하는 GameSystemRules 규칙은 **없음**. 이 이펙트가 반복 재생이어야 하는 이유는 `GameSystemRules_Units.md` 규칙 34(종료 처리 · 갱신 = 리셋) |
| SFX 이름 변경 | 같은 사용자 결정 + `VFXSFXGuide.md` 「파일 명명 규칙」 `sfx_[에셋명]_[이벤트].wav` | `GameSystemRules_Sound.md` 규칙 15(VFX+SFX 쌍 호출) — 이 규칙은 **호출 위치**를 정하는 규칙이라 이름을 정하지는 않는다. 다만 짝을 이루는 두 에셋의 이름을 같은 이벤트명(`heal`)으로 맞춰 두면 연결 작업에서 짝을 찾기 쉽다 |
| 애니메이션·코드 「Attack」 유지 | 사용자 결정 + 코드 실측(Research.md 2절) | `GameSystemRules_Units.md` 규칙 17(타격 프레임은 Attack 클립의 `OnAttackHit` 이벤트가 단일 출처) · 규칙 32(힐 발동은 `HitFrameTimes` 타이머) |
| (방법 B 를 고를 경우) 별도 칸으로 이동 | 문서 선례: `VFXSFXList.md` 「건물 특수 효과 이펙트」 / 「건물 특수 효과 사운드」 절(반복 재생 이펙트를 별도 칸에 두고 재생 방식을 비고에 적음) | `GameSystemRules_Units.md` 규칙 31 — 「유닛 공격 VFX 는 `attackPreset` 에 연결한다」. 공격 표에 남으면 이 문장대로 잘못 연결될 여지가 있다(Research.md 4절 2번) |

---

## 2. 변경 항목

대상 파일: `Assets/_Project/Docs/Assets/VFXSFXList.md` **한 개뿐**.

### 항목 1. VFX 이름 변경 (46행)

- 변경 전: `` | Transcendence | BloomFairy | `vfx_bloomfairy_attack.prefab` | 꽃잎 힐 | ``
- 변경 후 파일명: `vfx_bloomfairy_heal.prefab`
- 행의 위치와 비고 문구는 **미결 사항에서 고른 방법**을 따른다(아래 3절).

### 항목 2. SFX 이름 변경 (257행)

- 변경 전: `` | Transcendence | BloomFairy | `sfx_bloomfairy_attack.wav` | 힐 차임음 | ``
- 변경 후 파일명: `sfx_bloomfairy_heal.wav`
- 위치는 항목 1 과 **같은 방법**을 따른다(VFX 와 SFX 는 짝이라 한쪽만 옮기지 않는다).

---

## 3. 🔶 미결 사항 — 행을 어디에 둘 것인가 (사용자 결정 필요)

| | **방법 A — 이름만 바꾸고 제자리** | **방법 B — 「유닛 특수 효과」 칸을 새로 만들어 이동** |
|---|---|---|
| 파일/문서 변경 | 46행·257행의 파일명만 교체(2행) | ① 46행을 「유닛 공격 이펙트」 표에서 빼고, 「유닛 사망 이펙트」 표 다음에 `#### 유닛 특수 효과 이펙트` 절을 새로 만들어 넣는다 ② 257행도 같은 방식으로 「유닛 사망 사운드」 표 다음 `#### 유닛 특수 효과 사운드` 절로 옮긴다 ③ 두 절 머리에 「반복 재생, 치료받는 아군에 붙음」 안내 1~2줄 |
| 비고 문구 | 「꽃잎 힐」 / 「힐 차임음」 그대로 | 예: VFX 「**미제작** — 치료받는 아군에 붙어 지속 회복 동안 루프 재생. 만료·풀피·대상 사망 시 방출 정지, 재부여 시 유지(시간 리셋)」 / SFX 「**미제작** — `vfx_bloomfairy_heal` 과 쌍」 (문구는 확정 전 예시) |
| 즉시 따라오는 변화 | 없음 | 문서 목차(H4 절)가 2개 늘어난다 |
| 위험 | 「유닛 공격 이펙트」 표에 남아 있어, 연결 작업 때 규칙 31 대로 꽃요정 `attackPreset` 칸에 넣는 잘못된 연결로 이어질 여지가 있다(그 칸은 치료하는 꽃요정 **자신의 위치에서 한 번** 재생한다 — Research.md 4절 2번) | 새 절 이름(「유닛 특수 효과」)은 기존 「건물 특수 효과」를 본뜬 **새 이름**이다. `VFXSFXGuide.md` 의 키워드 표(302·493행)는 여전히 「유닛 공격」 분류 아래 있어 두 문서의 분류가 어긋난다(파일명이 없는 키워드 행이라 기능상 문제는 없음) |
| 얻는 것 | 변경이 가장 작다 | 재생 방식이 다른 이펙트임이 문서에서 바로 보인다. 이미 있는 「건물 특수 효과」 선례(반복 재생 이펙트를 별도 칸 + 비고에 재생·정지 조건)와 같은 형태가 된다 |
| 미확인 | — | SFX 가 한 번 울리는 소리인지 반복음인지는 **아직 정해지지 않았다** — 방법 B 의 SFX 비고에는 이를 확정해 적지 않는다 |

**추천: 방법 B.** 이유 —
1. 이 이펙트는 「공격 순간 한 번 재생」이 아니라 「대상에 붙어 길이가 매번 다른 반복 재생」이다(Research.md 3-1). 공격 표에 두면 표의 다른 24행과 성격이 다른 행이 한 줄 섞인다.
2. 공격 표에 남기면 `GameSystemRules_Units.md` 규칙 31 의 문장이 잘못된 연결(`attackPreset`)을 유도할 수 있다. 연결 작업이 아직 남아 있으므로 지금 칸을 갈라 두는 편이 안전하다.
3. 같은 문서에 「건물 특수 효과 이펙트/사운드」라는 같은 성격의 선례가 이미 있다.

> 방법 A 도 틀린 선택은 아니다 — 이름 결정이라는 이번 작업의 목적은 A 만으로 달성된다. B 의 이점은 **다음 작업(연결)의 오연결 예방**이다.
> 이 두 방법 외의 배치(예: 「보류」 절, 별도 문서)는 검토하지 않았다.

---

## 4. 변경하지 않는 것 (이유 포함)

| 대상 | 이유 |
|---|---|
| `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim` (클립 이름) | `UnitFactory.cs:461`·`:496`, `CombatHitEventInjector.cs:260`, `CombatHitEventValidator.cs:176` 이 클립 이름에 「Attack」 포함 여부로 찾는다. 바꾸면 치료 발동 타이밍(`HitFrameTimes`)을 못 찾는다 |
| `Assets/_Project/Animations/Units/BloomFairy/BloomFairy.controller` 의 `Attack` 상태 | `UnitView.cs:54` `Animator.StringToHash("Attack")` 로 재생한다. 바꾸면 치료 동작이 재생되지 않는다 |
| 코드 식별자 `AttackCooldown` · `OnAttackHit` · `UnitAnimState.Attack`(`NetworkUnit.cs:332`) · `PlayUnitAttack` · `PlayUnitAttackSfx` | 전 유닛 공용 경로다. 이번 작업은 문서 작업이며 코드는 범위 밖이다(CLAUDE.md 규칙 6) |
| 위 식별자를 인용한 규칙 문서 문장(`GameSystemRules_Units.md` 규칙 17·27·32·36, `StatsReference.md` 등) | 코드 이름과 같아야 하므로 그대로 둔다 |
| `VFXSFXGuide.md` 302·493행 | 파일 이름이 없는 키워드 행이고 키워드가 이미 「heal」이다(Research.md 1절) |
| `VFXSFXList.md` 76·287행(꽃요정 사망 이펙트/사운드) | 「attack」이 아니다 |
| `VFXSFXGuide.md` 「파일 명명 규칙」 | `heal` 이 규칙 안에 들어가므로 고칠 필요가 없다 |

---

## 5. 범위 밖 — 후속 작업으로 남기는 것

이펙트(`vfx_bloomfairy_heal.prefab`)가 만들어진 뒤 별도 작업에서 다룬다. 이번에는 **아무것도 연결하지 않는다.**

1. **`EffectManager` 에 「대상에 붙여 반복 재생 + 정지」 API 신설** — 지금은 위치에서 한 번 재생하는 메서드뿐이다(Research.md 4절 1번).
2. **HoT 시작·갱신·종료에 맞춘 시작/정지 연결** — 종료 3사유(만료·풀피·대상 사망)와 재부여 시 유지 처리(`GameSystemRules_Units.md` 규칙 34). 멀티에서 클라이언트가 HoT 시작/종료를 어떻게 알게 할지도 그때 정한다.
3. **SFX 짝 호출**(`GameSystemRules_Sound.md` 규칙 15)과 SFX 의 1회/반복 여부 결정.
4. **꽃요정 `attackPreset` 칸에 이 이펙트를 넣지 않는다**는 점을 연결 작업 Plan 에 명시(Research.md 4절 2번).
5. (참고) `BloomFairy_Attack.anim` 이벤트 0건과 규칙 27 서술의 관계 확인(Research.md 4절 3번).

---

## 6. 위험 요소

| 위험 | 대책 |
|---|---|
| 옛 이름이 다른 곳에 남는다 | 변경 후 `bloomfairy_attack` 을 `.md`·`.cs`·`.asset`·`.prefab`·`.unity` 에서 다시 찾아 **0건**을 확인한다(지금은 2건, 둘 다 변경 대상) |
| 표 행 이동(방법 B) 중 행이 사라지거나 두 번 들어간다 | `vfx_bloomfairy_heal` · `sfx_bloomfairy_heal` 이 문서에 **각 1건**인지 확인한다 |
| 코드·애니메이션을 실수로 건드린다 | 대상 파일은 `VFXSFXList.md` 하나뿐이다. 그 외 파일은 열기만 한다 |

---

## 7. 구현 후 확인 절차

1. `bloomfairy_attack` 잔존 0건, `bloomfairy_heal` VFX·SFX 각 1건 확인.
2. 리포지토리 루트에서 `python3 Tools/check_docs.py` 실행 → **0건** 확인(WORKFLOW.md [11]). 작업 전 기준선도 0건이다(2026-10-08 실측).
   - `VFXSFXList.md` 는 `Docs/` 하위라 검사 범위 안이다. 이 Plan/Research 는 `_Tasks/` 라 검사 범위 밖이다.
3. 이번 작업은 코드·씬 변경이 없으므로 실기 테스트 대상이 없다.

---

## 8. 변경 예정 파일

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 항목 1·2 (방법 A: 2행 교체 / 방법 B: 2행 이동 + 절 2개 신설) |

작업 문서(이번에 작성): `Assets/_Project/Docs/_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Research.md` · `Plan.md`
