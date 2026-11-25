# Research — 정령 지원 스킬 「물결의 가호」 (원소 공명 자리 교체)

## 이 문서는 무엇인가

정령 스킬 건물(MagicSpirit)의 **5번 칸**에는 지금 「원소 공명」이 이름만 들어가 있고 쓸 수 없다(기획이 보류된 채 자리만 잡아 둔 스킬이다). 사용자는 원소 공명을 **다음에 스킬을 더 넣을 때** 다루기로 하고, 그 자리에 **물 원소의 아군 강화 스킬**을 넣기로 정했다. 불(화염의 축복 — 공격력)과 흙(대지의 갑주 — 받는 피해 감소)에는 아군 강화 스킬이 있는데 물만 없었기 때문이다.

새 스킬의 이름은 **물결의 가호**다. 누르면 바로(조준 없이) 발동해, 그 순간 살아 있는 **우리 편 유닛 전부**(건물은 아님)가 8초 동안 **1초마다 체력 20을 되찾고, 받는 회복이 1.25배**가 된다. 숫자는 모두 시험용 임시값이다.

이 문서는 그 일을 하기 전에 **지금 코드와 문서가 어떻게 되어 있는지**를 확인한 결과다. 핵심 결론은 세 가지다.

1. **게임 코드는 새로 만들 것이 없다.** 초월의 「재생의 뿌리」가 이미 쓰는 「재생」 · 「받는 회복 배율」 효과를 **건물 몫만 빼고** 그대로 쓰면 된다. 바꾸는 것은 스킬 자산을 만드는 **에디터 셋업 도구의 표 한 줄**, 「사용 가능 스킬 수」 숫자(14 → 15), 그 숫자를 확인하는 **회귀 검사 6곳**이다.
2. **재생의 뿌리와 물결의 가호는 같은 효과 종류를 쓰지만 서로 만날 수 없다.** 한 플레이어는 한 종족만 쓰고, 두 스킬 모두 자기 편에게만 걸린다.
3. **새로 드러난 점:** 정령 팀은 초월처럼 자연회복 · 물안개 · BloomFairy 힐이 없다. 그래서 「받는 회복 1.25배」가 실제로 키우는 회복은 **이 스킬 자신의 재생**과 **TorrentSpirit 파도 회복** 정도다. 그리고 지금 코드 주석 몇 곳은 「파도 회복은 정령 것이라 회복 증폭과 만나지 않는다」고 적고 있는데, 이 작업 뒤에는 **그 문장이 틀리게 된다**(5절).

---

**표기:** `[확인]` = 이 문서 작성자가 파일을 직접 읽어 확인 · `[확인 — grep]` = 검색 결과로 확인(의미까지는 보지 않음) · `[미확인]` = 확인하지 못함(프로그래머 확인 필요). 행 번호는 HEAD `2bb94c8` 기준이며, 파일이 바뀌면 어긋나므로 **함수 · 표 이름을 함께 적는다**(`.claude/mistakes.md` 「행 번호로 위치를 가리켜 전부 어긋남」). 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`, `Suite/` = `Assets/Editor/Tools/Combat/`, `Setup` = `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs`.

---

## 1. 사용자 결정 (메인 세션 전달, 2026-11-25)

| # | 원문 | 뜻 |
|---|---|---|
| 1 | 「원소 공명은 다음에 추가로 스킬 넣을때 하도록하고 불과 흙이 아군을 강화하는 스킬이 있으니 물도 하나넣도록하자 적절한 이름과 아군강화기능 기획해줘」 | 원소 공명 보류 · 물 원소 아군 강화 스킬을 새로 기획 |
| 2 | (game-design-lead 기획안 추천에 대해) 「추천대로하자 유닛만 적용하기로하고 수치는 임시로 주고 밸런싱할때 맞추자 비슷한 효과를 쓸수도 있지 괜찮을거같아」 | 기획안 채택 · 유닛만 · 수치 임시 · 재생의 뿌리와 비슷한 효과 허용 |

확정 내용(메인 세션 정리):
- 정령 슬롯 5(원소 공명 자리)를 **물결의 가호**(자산 `Skill_Spirit_TideBlessing`)로 교체. 원소 공명은 이후 스킬 추가 때로 보류 — 이번에 로드아웃에서 빠진다.
- 즉시 발동 · 맵 전체 · 발동 순간 살아 있는 **아군 유닛만**(건물 제외) · 유닛 재생 초당 20 · 유닛 받는 회복 배율 ×1.25 · 지속 8초 · 쿨다운 15초 — 모두 **임시값**.
- 정렬 규칙상 「강화 · 유닛만」 그룹이라 슬롯 5 유지.
- VFX는 불(화염의 축복) · 흙(대지의 갑주) · 물(물결의 가호)을 **한 세트**로 기획(같은 모양 문법, 색만 다르게). 제작은 별도 단계(Unity AI 크레딧 소진).

---

## 2. 지금 정령 로드아웃과 셋업 도구

### 2-1. 셋업 도구의 표 `[확인]`

`Setup` `BuildSpecs()`가 세 종족 × 5슬롯 = 15개 스킬의 값을 표로 들고 있고, 메뉴 `Hexiege/Setup/Skills/Create Support Skill Assets And Replace Loadout`를 누르면 그 표대로 `Assets/_Project/Resources/Config/Skills/` 아래 자산을 만들거나 값을 다시 쓰고, `SkillLoadoutConfig.asset`의 세 종족 로드아웃을 그 15개로 다시 넣는다(멱등 — 파일 이름이 같으면 GUID를 유지하고 값만 덮는다).

정령 몫(394~409행):

| 슬롯(0부터) | 화면 칸 | 표 줄 | 상태 |
|---|---|---|---|
| 0 | 1 | `meteor`(객체) | 사용 가능 |
| 1 | 2 | `PersistentZone("Skill_Spirit_FrostPrison", …)` | 사용 가능 |
| 2 | 3 | `InstantBuff("Skill_Spirit_FlameBlessing", …, AttackPowerMul, 1.5f, 8f)` | 사용 가능 |
| 3 | 4 | `InstantBuff("Skill_Spirit_EarthArmor", …, IncomingDamageMul, 0.7f, 8f)` | 사용 가능 |
| 4 | 5 | `InstantPlaceholder("Skill_Spirit_ElementalResonance", s, 4, "원소 공명", "기능 기획 보류 중인 스킬이다(이름만 등록).", allies: false)` (407~409행) | **사용 불가** — 상태 종류 None |

### 2-2. 재생의 뿌리 줄 — 그대로 본뜰 모양 `[확인]`

416~424행:

```
WithExtra(WithExtra(WithExtra(InstantBuff("Skill_Trans_RootsOfRegeneration", t, 1, "재생의 뿌리",
    "맵 전체 아군 유닛과 건물에 체력 재생을 부여하고 증폭한다.", StatusEffectKind.Regen, 20f, 8f),
    SkillEffectTarget.Building, StatusEffectKind.Regen, 40f),
    SkillEffectTarget.Unit, StatusEffectKind.HealAmpMul, 1.5f),
    SkillEffectTarget.Building, StatusEffectKind.HealAmpMul, 1.5f),
```

- `InstantBuff`(238~251행)는 사용 가능 · 즉시 · 타입 C(`GlobalStatusChange`) · **쿨다운 15 고정** · 아군 대상 · 반경 0 · 피해 0을 넣는다. 주 효과는 상태 종류 · 강도 · 지속이다.
- `WithExtra`(196~200행)는 추가 효과 한 줄(대상 종류 · 종류 · 강도)을 붙인다.
- 물결의 가호는 **주 효과 「유닛 재생 20 · 8초」 + 추가 효과 「유닛 각각 · 받는 회복 배율 · 1.25」 한 줄**이면 된다(건물 줄 두 개를 빼는 것). 쿨다운 15는 `InstantBuff` 기본값과 같아 따로 넣을 칸이 없다.

### 2-3. 사용 가능 개수와 완료 문구 `[확인]`

- `ExpectedAvailableCount = 14`(84행). 바로 위 83행에 **`// private const int ExpectedAvailableCount = 8;`** 주석 줄이 남아 있다 — 그 위 주석(82행)은 「옛 값 13 줄을 지웠다」고 적는데, 실제로 남은 옛 줄은 **8**이다(6절 발견 1).
- `ValidateSpecs`(553행~)는 표의 사용 가능 수가 이 상수와 다르면 **아무것도 쓰기 전에 멈춘다**(583~585행). 물결의 가호를 「사용 가능」으로 넣으면 표의 수가 15가 되므로 상수도 15로 바꿔야 도구가 돈다.
- 완료 문구(537~540행)는 사용 가능 스킬 이름을 괄호에 나열한다(「… · 메테오 · 연막탄)」). 개수는 상수를 읽는다.
- 슬롯 수 검사: 종족마다 슬롯 0~4가 정확히 하나씩, 전체 3 × 5 = 15(`SlotsPerRace = 5`, 154행 — 스킬 규칙 4 「건물당 최대 5」).

### 2-4. 아이콘 `[확인]`

`WriteValues`는 모든 스킬의 아이콘 칸을 **비운다**(724 · 730행 — 「아이콘 그림은 추후 UI 작업 — 상위 결정 16」), `VerifyLoadout`도 비어 있는지 확인한다(807행). 물결의 가호도 같은 처리 — 버튼에는 스킬 이름 글자가 보인다(스킬 규칙 7 「이름 · 설명」).

### 2-5. 새 칸은 도구가 쓰지 않는다 `[확인]`

「유닛 상태 VFX」 · 「건물 상태 VFX」 칸은 이 도구가 쓰지 않는다(237 · 419행 주석). 새 자산은 `ScriptableObject.CreateInstance`로 만들어지므로 이 칸들은 **빈 값**으로 시작한다(`CreateOrLoad`, 705~719행). 「발동 VFX」 칸은 도구가 매번 표 값으로 쓰는데 물결의 가호는 포격 · EMP VFX가 아니므로 비운다.

### 2-6. 자산 파일 `[확인]`

`Assets/_Project/Resources/Config/Skills/`에 `Skill_Spirit_ElementalResonance.asset`이 있고 `Skill_Spirit_TideBlessing.asset`은 없다. 도구는 **표에 없는 자산을 지우지 않는다**(헤더 「기존 임시 스킬 자산 18개는 지우지도 고치지도 않는다 — 로드아웃에서 참조만 빠진다」). 그래서 표 줄만 바꾸면:
- 새 자산 `Skill_Spirit_TideBlessing.asset`이 **새 GUID로 하나 생긴다**(완료 문구 「새로 만든 자산 1개」).
- 원소 공명 자산은 파일로 남고 로드아웃 참조만 빠진다(18개 옛 자산과 같은 처지).
- 원소 공명을 가리키는 런타임 코드는 없다(`grep` — `SupportSkillLoadoutSetup.cs` 밖에서 `ElementalResonance` · 「원소 공명」 0건, `SkillDefinition.cs`의 일반 주석 1건뿐) `[확인 — grep]`.

**공습 · 메테오 · 연막탄 자리표시 교체 선례와의 차이:** 그 셋은 파일 이름이 같아(자리표시 자산을 그대로 갱신) GUID가 유지됐다. 이번에는 **스킬 자체가 바뀌어 파일 이름이 달라진다** — 새 파일이 생기고 옛 파일이 남는다.

---

## 3. 게임 코드 — 재생 · 받는 회복 배율 경로

### 3-1. 실행기가 효과를 나누는 곳 `[확인]`

`Scripts/Application/Skill/GlobalStatusChangeExecutor.cs`(40~85행):
- 대상 팀 = `TargetsAllies`면 시전 팀.
- 주 효과 종류가 서비스 종류(`SupportSkillEffectSystem.IsServiceKind` — 받는 피해 배율 · 생산 속도 · 쉴드 · 재생 · 받는 회복 배율)면 `ApplySupportEffect(targetTeam, SkillEffectTarget.Unit, …)` — **주 효과는 항상 「유닛 각각」** 이다.
- 추가 효과는 줄마다 그 줄의 대상 종류로 `ApplySupportEffect`.

→ 물결의 가호의 주 효과(재생 20)는 유닛에, 추가 효과(받는 회복 1.25)도 유닛에 걸린다. **건물 줄이 없으니 건물에는 아무것도 걸리지 않는다.** 코드 변경 없음.

### 3-2. 효과 서비스 `[확인]`

`Scripts/Application/Services/SupportSkillEffectSystem.cs` · `.Regen.cs`:
- `IsSupported`(88~98행): 유닛 × 재생 · 받는 회복 배율 지원.
- 같은 대상에 **같은 종류**가 다시 걸리면 강도 · 남은 시간을 새 값으로 **교체**(13 · 279 · 379~418행 — 스킬 규칙 13 「다시 걸릴 때」). 교체 기준은 **대상 + 종류**이고 어느 스킬에서 왔는지는 보지 않는다.
- 재생: 1초마다 「초당 회복량」 × (그 대상의 받는 회복 배율)을 회복, 첫 회복은 1초 뒤, 체력 가득이면 그 주기는 건너뛰고 효과는 남음(`.Regen.cs`, `TickRegen`).
- 받는 회복 배율: `AmplifyHeal(target, amount, source)` 하나가 모든 회복 지점에서 회복량 × 배율 + 대상별 소수 이월을 돌려준다(208행). 배율 값을 읽기만 하므로 1.25에서도 같은 식이 된다 — 예: 재생 20 → 25, 이월 예 1 · 1 · 1 · 1 → 1 · 1 · 1 · 2 = 5 `[계산 — 코드 식 기준, 실행 확인 아님]`.
- 서버 전용. Client HP는 기존 회복 동기화(유닛 `SyncHealClientRpc`)로 맞춰진다 — **새 RPC가 없으므로 접속 호환 번호(스키마)는 13 그대로**(`UnitAttackShadowProfileResolver.PresentationContractSchemaRevision = 13`, 회귀 검사 R-T9가 확인) `[확인]`.

### 3-3. 재생의 뿌리와 만나지 않는다 `[확인 — 규칙 · 코드]`

- 스킬 로드아웃은 종족별이고 한 플레이어는 한 경기에 한 종족이다(스킬 규칙 1 · 6, 재생의 뿌리 Task Plan 6-2-7 「한 플레이어는 한 경기에 한 종족」).
- 두 스킬 모두 아군 대상이라(`InstantBuff` → `TargetsAllies = true`) 각자 **자기 팀 유닛에만** 건다.
- 따라서 한 유닛이 재생의 뿌리와 물결의 가호를 함께 받는 경우는 없다. 서로 교체할 일도 없다.
- 스킬 규칙 13 「다른 출처와 겹칠 때」는 「전역 상태변경끼리 같은 종류가 겹치는 경우(서로 다른 스킬의 같은 종류 — 지금 그런 조합은 없다)는 아직 정하지 않았다」고 적는다. 이번에 **같은 종류를 쓰는 서로 다른 스킬이 처음 생기지만, 서로 다른 종족이라 한 대상에서 겹치지 않는다** — 규칙 문장은 테스트 뒤 반영 때 다듬을 대상이다(Plan 10절).

### 3-4. 정령 팀이 받는 회복 — 증폭이 실제로 키우는 것 `[확인 — grep · 코드]`

회복이 HP에 들어가는 자리와 출처(`HealAmpSource`, `.Regen.cs` 55~70행):

| 출처 | 어디서 | 정령 팀에 있나 |
|---|---|---|
| `Natural` 자연회복 | `UnitCombatUseCase.cs` 814행 | **없음** — 연구소 자연회복은 초월 유닛 전용(재생의 뿌리 Task Plan 6-2-7, Upgrade 규칙 7) |
| `Mist` 물안개 | `MistShrineUseCase.cs` 237행 | **없음** — MistShrine은 초월 건물 |
| `Bloom` BloomFairy 힐 | `UnitCombatUseCase.cs` 3368~3369행 | **없음** — BloomFairy는 초월 유닛 |
| `HoT` 그 밖의 지속 회복 | 같은 곳(시전자가 BloomFairy가 아닐 때) | 지금 로드아웃의 정령 스킬 중 지속 회복을 거는 것은 없다 `[확인 — 로드아웃 표]` |
| `SkillRegen` 스킬 재생 | `.Regen.cs` 166행 | **있음** — 물결의 가호 자신 |
| `Wave` TorrentSpirit 파도 회복 | `UnitCombatUseCase.cs` 3854행 | **있음** — TorrentSpirit는 정령 유닛(StatsReference 「유닛 — 정령계」) |

- 파도 회복도 `ApplyHealToUnit` → `AmplifyHeal`을 지난다(2574행 — 파도 진단은 계속 요청량을 남기고, 실제 HP 변화는 같은 진단의 `delta`에 보인다). 그래서 **물결의 가호를 받은 유닛이 TorrentSpirit 파도로 회복되면 그 회복도 1.25배가 된다** — 코드를 바꾸지 않아도 그렇게 된다.
- 사용자 결정 「받는 회복 배율 ×1.25」를 글자 그대로 읽으면 파도 회복도 포함이다. 다만 사용자가 파도 회복을 따로 언급한 기록은 없다 → **Plan U-1에서 확인한다.**
- 그러면 지금 정령 팀에서 증폭이 키우는 것은 **자기 재생(20 → 25/초)과 파도 회복**이다. 「받는 회복 1.25배」가 체감상 크지 않을 수 있다는 점은 밸런싱 단계의 몫이다(사용자 「수치는 임시로 주고 밸런싱할때 맞추자」).

### 3-5. 주석이 틀리게 되는 곳 `[확인]`

「파도 회복은 정령 것이라 초월 효과와 만나지 않는다」는 전제가 적힌 곳:
- `SupportSkillEffectSystem.Regen.cs` 69행 `HealAmpSource.Wave` 요약 주석 — 「TorrentSpirit 파도 회복(정령 — 초월 팀과 만나지 않는다)」.
- `UnitCombatUseCase.cs` 2573행(`ApplyHealToUnit` 안) — 「파도는 정령 회복이라 초월 효과와 만나지 않는다」.
- `UnitCombatUseCase.cs` 3852행 — 「(진단 값 — 정령 파도는 초월 효과와 만나지 않는다)」.
- `SupportSkillEffectSystem.RegenDiagnostics.cs` 84행 — 「Other = Unspecified + Wave(초월 팀과 만나지 않는 출처 · 넘기지 않은 호출)」.

동작은 그대로이고 **주석만 틀려진다.** 주석 규칙(CLAUDE.md 규칙 8)과 이 작업이 만든 변화라는 점에서 고칠 대상이다(Plan 2절 T-5 — 사용자 확인 U-5).

---

## 4. 진단 로그 — 지금 있는 것으로 판정할 수 있는가

### 4-1. 서버 쪽 `[SKILL-REGEN]` (`SupportSkillEffectSystem.RegenDiagnostics.cs`, `Diag=SkillRegen`, `DiagRev=1`, `GameLog.Dev`) `[확인]`

| 이벤트 | 남기는 때 | 물결의 가호 판정에 쓰는 값 |
|---|---|---|
| `RegenApplied` | 효과 묶음(종류 · 대상 종류마다 한 줄)을 걸 때 | `Team` · `TargetTeam` · `Slot` · `Kind` · `Target` · `Targets` · `MagMilli` · `DurationMs`. **물결의 가호 1회 = 2줄**(`Kind=Regen Target=Unit MagMilli=20000` · `Kind=HealAmpMul Target=Unit MagMilli=1250`, 둘 다 `DurationMs=8000`). `Target=Building` 줄이 **없어야** 한다(재생의 뿌리는 4줄) |
| `RegenTickSample` | 유닛의 첫 재생 회복(표본) | `Base=20` · `Amplified=25` · `AmpMagMilli=1250` · `Check=Match` |
| `BuildingRegenSample` | 건물의 첫 재생 회복 | 물결의 가호 묶음(`Seq`)에는 **0건**이어야 한다 |
| `HealAmpSample` | 출처별 첫 증폭(표본) | `Source=SkillRegen`(20 → 25) · `Source=Wave`(파도 회복 × 1.25 + 이월) · `Check=Match` |
| `NewTargetHealSample` | 효과 중 대상이 아닌(새로 나온) 같은 팀 유닛이 증폭 없이 회복 | `Amp=False` |
| `RegenEnded` | 묶음이 끝날 때(재생 묶음 · 증폭 묶음 각각) | `Reason=Expire` · `ActualMs≈8000` · 재생 합계(`Pulses` · `BaseTotal` · `AmpTotal` · `HealedTotal` · `FullHpSkips`) · 증폭 합계(출처별 `NaturalCount` · `MistCount` · `BloomCount` · `HoTCount` · `RegenCount` · **`OtherCount`** · `TotalCheck` · `NewTargetHeals`) |

- **빈 곳:** 증폭 합계 줄은 파도를 따로 세지 않고 `OtherCount`(= Unspecified + Wave)에 넣는다(84~93행). 파도 증폭은 `HealAmpSample Source=Wave` 표본으로는 보이지만 **합계 줄에서는 다른 출처와 섞인다.** 판정은 표본 + `TotalCheck=Match`로 가능하다 — 합계에 따로 보이려면 최소 추가(파도 세 키)가 필요하다(Plan 4절 제안 D-1).
- 로그에 **스킬 이름이 없다.** 물결의 가호는 `Team`(정령 쪽 팀) · `Slot=4`(0부터 — 화면 5번 칸) · `Kind`로 가린다 `[확인 — 로그 형식]`. 슬롯 값이 0부터인지 화면 번호인지는 `RegenApplied`의 `Slot={b.Slot}` 출처를 프로그래머가 확인 `[미확인]`.

### 4-2. 발동 `[SKILL-VFX]` `[확인]`

- `ActivationConfirmed`(Host · 싱글): `Team` · `BuildingId` · `Slot` · `Mechanic` · `Sequence` · `CooldownMs`. 물결의 가호 = `Mechanic=GlobalStatusChange` · `CooldownMs=15000`.
- `ActivationReceived`(Client): `Role=Client` · `Slot` · `Sequence` · `CooldownMs`.
- `ActivationRejected`: 정상이면 0.

### 4-3. 유닛 상태 VFX `[SKILL-BUFF]` (`UnitBuffVfxPresenter.cs`) `[확인]`

- 칸이 비면 `BuffVfxSkipped Reason=NoPrefab`(`Role` · `Channel` · `Sequence` · `Team` · `Slot` · `Kind`) 한 줄 — 양쪽 화면에서 남는다. 물결의 가호 칸은 비어 있으므로 이 줄이 「Client가 발동을 받았다」는 증거가 된다(`Role=Client Channel=Received`).
- 칸을 채우면 `BuffVfxStarted` · `BuffVfxStopped`.

### 4-4. Client HP `[확인]`

- 새 줄은 없다. 기존 `NetworkHealthSync.cs` 458~459행 `GameLog.Dev.Info("Network", …, "유닛 힐 동기화", "UnitId=…, AppliedHeal=…, Hp=…")`가 Client에서 회복이 들어올 때마다 남는다(회복 틱마다 — 고빈도 `Dev`).
- Host `RegenTickSample`의 `Id` · `HpAfter`와 Client 「유닛 힐 동기화」의 `UnitId` · `Hp`를 맞대면 Client HP가 따라왔는지 볼 수 있다. 두 쪽의 유닛 Id가 같은 값인지는 `[미확인 — 프로그래머 확인]`(재생의 뿌리 Task B-L14는 VFX 줄로만 판정했고 HP 줄은 쓰지 않았다).

### 4-5. 셋업 도구 Console `[확인]`

`[SupportSkillSetup] 기존 자산 비교 — 값이 바뀐 자산 N개 · 이미 표와 같던 자산 M개 · 새로 만든 자산 K개` · `[SupportSkillSetup] 완료 — … 사용 가능 스킬 15개(…)`. Unity Console에만 나오며 RuntimeLog에는 없다(Debug.Log).

---

## 5. 회귀 스위트 — 이번 변경에 걸리는 검사

### 5-1. 사용 가능 개수 검사 6곳 `[확인 — grep]`

모두 `SupportSkillLoadoutSetup.ExpectedAvailableCount`를 반사(reflection)로 읽어 **`== 14`** 인지 본다. 상수를 15로 바꾸면 6곳 모두 **실패**한다 → 함께 15로 바꿔야 한다.

| 파일 | 검사 | 행 |
|---|---|---|
| `Suite/RunUnitActionSelfValidation.AirStrike.cs` | LS-T9 | 637~638 |
| `Suite/RunUnitActionSelfValidation.Meteor.cs` | M-T8 | 457~458 |
| `Suite/RunUnitActionSelfValidation.SkillZones.cs` | Z-T10 | 358~359 |
| `Suite/RunUnitActionSelfValidation.SupportNewStatuses.cs` | S-T9 | 598~599 |
| `Suite/RunUnitActionSelfValidation.SupportRegen.cs` | R-T9 | 523~524 |
| `Suite/RunUnitActionSelfValidation.SupportShield.cs` | (쉴드 소스 게이트) | 363~364 |

PASS 문구에도 「setup expected count 14」류 숫자가 들어 있다(`AirStrike.cs` 79행 · `Meteor.cs` 65행 · `SkillZones.cs` 70행 — 선례는 「문구만」 함께 바꿨다). 그 밖의 PASS 문구(예: `SupportRegen.cs` 69행 「setup 12」)는 이미 숫자가 낡아 있다 `[확인]` — 이번 범위 밖이다(6절 발견 2).

### 5-2. 원소 공명 · 정령 슬롯 5를 보는 검사 `[확인 — grep]`

- `ElementalResonance` · 「원소 공명」을 보는 검사 **0건**. 정령 슬롯 4(0부터)를 보는 검사 **0건**(정령은 슬롯 0 메테오 · 슬롯 3 대지의 갑주만 본다).
- 셋업 표 글자를 보는 검사(`SupportActiveLineCount`)는 공습 · 메테오 · 연막탄 줄과 재생의 뿌리 · 쉴드 관련 소스만 본다 — 정령 슬롯 5 줄을 보지 않는다.
- 재생의 뿌리 자산 계약(R-T8, `SupportRegen.cs` 453~483행)은 **재생의 뿌리 자산만** 본다 — 물결의 가호 자산은 아직 검사가 없다.

### 5-3. 회귀 스위트가 숫자를 읽는 방식의 주의 `[확인 — 공용 메모리]`

전역 스탯 표가 비어 있거나 실제 값이 남아 있을 수 있다(`.claude/MEMORY.md` 「Regression suite fixtures — never trust the global stat tables」). 새 검사는 빈 표에서 시작해 필요한 행만 고정 · 복원해야 한다(`SupportFixtureStatsScope`).

---

## 6. 작업 중 발견 (기록 — 이번 범위 결정은 Plan에서)

1. **셋업 도구의 낡은 주석 줄:** `Setup` 83행 `// private const int ExpectedAvailableCount = 8;` — 82행 주석은 「옛 값 13 줄을 지웠다」고 하는데 남은 옛 줄은 8이다. 동작에는 영향이 없다(주석). 이번에 14 줄을 주석으로 남길 때 함께 정리할지는 사용자 확인(Plan U-5).
2. **회귀 스위트 PASS 문구의 낡은 숫자:** `SupportRegen.cs` 69행 PASS 문구 「setup 12」(검사 본문은 14). 범위 밖 — 기록만.
3. **이름 겹침 주의:** 정령 유닛 **TideSpirit**가 있다(StatsReference 「유닛 — 정령계」). 자산 이름 `Skill_Spirit_TideBlessing`과 로그를 「Tide」로 검색하면 유닛 줄도 걸린다 — 검색은 `TideBlessing` 또는 스킬 이름 전체로 한다.
4. **스키마가 같아도 두 빌드를 다시 설치해야 한다:** 새 RPC가 없어 스키마는 13 그대로다. 경기 시작 호환 확인이 **스킬 자산 내용**까지 비교하는지는 `[미확인]`. 비교하지 않는다면 옛 빌드 기기는 오류 없이 경기에 들어가 5번 칸을 「원소 공명(사용 불가)」로 보여 줄 수 있다 → 멀티 테스트는 에디터 · 기기 **모두 이번 자산이 들어간 빌드**여야 한다.
5. **원소 공명이 다시 들어올 자리:** 스킬 규칙 4 「건물당 스킬 수 최대 5개」와 셋업 도구 `SlotsPerRace = 5` 때문에, 나중에 원소 공명을 다시 넣으려면 **규칙 4 개정(슬롯 6 이상)** 또는 **다른 정령 스킬 하나와 교체** 중 하나가 필요하다. 스킬 패널은 3 × 3 격자에 6번 칸 「철거」 고정 · 7~9 비움이다(규칙 9, 상위 기획 1-2).

---

## 7. 관련 문서 현황 (테스트 뒤 반영 대상 — 이번에는 고치지 않는다)

`원소 공명`이 「현재 상태」로 적힌 곳 `[확인 — grep]`: `GameSystemRules/GameSystemRules_Skills.md`(자연어 설명 끝 문단 「기능이 붙은 것은 열넷 … 나머지 하나(정령 원소 공명)」) · `PROJECT_STATUS.md` · `WORK_HISTORY.md`(이력) · `Assets/SkillVFXDesign.md`(3절 표 · 「원소 공명」 절) · `.claude/MEMORY.md` · `.claude/agent-memory/game-design-lead/MEMORY.md`. 상위 기획 Plan(`_Tasks/2026-11-02/14_25_race-support-skills-design/`) 1-2 정령 5 · 결정 15는 Task 이력이다.
