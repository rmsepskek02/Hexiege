# Plan — ProductionPanelUI 자동 생산 인디케이터 중복 배선 정리

**작성일:** 2026-08-13
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-13/05_05_production-auto-indicator-dedup/`
**선행 문서:** [Research.md](Research.md)
**수정 대상:** `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` (코드 1) · `Assets/_Project/Scenes/Game.unity` (씬 저장 1)
**구현 담당:** **game-programmer 에이전트** — 이 문서 작성 시점에는 코드를 작성하지 않는다
**현재 상태:** **계획 수립 완료 / 사용자 승인 대기 · 코드·씬 무변경**

---

## 이 계획이 무엇이고 왜 필요한가 (자연어 설명 — 기술 용어 없이)

배럭(병사를 뽑는 건물) 창의 유닛 버튼을 길게 누르면 **"자동으로 계속 뽑기"** 가 켜지고,
켜졌다는 표시로 **버튼 테두리가 빙글빙글 도는 효과**가 나타납니다.

이 테두리 하나를 켜고 끄는 **스위치가 두 개** 달려 있습니다.
하나는 "**투명하게 만들어** 안 보이게 하는" 스위치, 다른 하나는 "**아예 꺼 버리는**" 스위치입니다.
둘 다 같은 테두리에 연결되어 있고 항상 함께 움직이기 때문에 **지금 화면에는 아무 문제가 없습니다.**

**이번 작업은 이 두 스위치 중 "아예 꺼 버리는" 쪽을 없애고, "투명하게 만드는" 쪽 하나만 남기는 것입니다.**

### 왜 하나로 줄여야 하나요

**첫째, 스위치가 둘이라는 사실이 어디에도 적혀 있지 않습니다.**
프로그램만 읽어서는 둘이 같은 물건을 가리킨다는 것을 알 수 없고, 화면 구성 파일을 열어
연결 번호를 하나하나 맞춰 봐야만 알 수 있습니다. 그래서 나중에 누군가 한쪽만 고치면
**둘이 어긋나는데, 어긋난 이유를 찾기가 아주 어렵습니다.**

**둘째, "아예 꺼 버리는" 방식은 이 프로젝트에서 이미 사고를 낸 적이 있습니다.**
바로 직전 작업에서, 꺼 놓은 채로 저장된 물건이 **다시 켜지지 않아 영영 안 보이는 버그**가 났습니다.
지금 이 테두리는 켜진 상태로 저장되어 있어 아직 괜찮지만, 누군가 자동 모드가 꺼진 상태에서
화면 구성을 저장하면 **꺼진 채로 굳어 버릴 수 있습니다.** 미리 그 가능성을 없애자는 것입니다.

### 조사에서 바로잡은 것 하나

작업을 계획하기 전에는 *"꺼 버리면 테두리 도는 애니메이션이 멈춘다"* 는 걱정이 있었습니다.
그런데 확인해 보니 **회전은 프로그램이 아니라 그림 효과가 스스로 시간을 보고 도는 방식**이라
껐다 켜도 멈추거나 튀지 않습니다. **이 걱정은 사실이 아니어서 이유 목록에서 뺐습니다.**
빼고 나서도 위의 두 가지 이유는 그대로 남아 있으므로, 작업은 그대로 진행할 가치가 있습니다.

### 이번 작업이 바꾸지 않는 것

- **화면에 보이는 모습은 전혀 달라지지 않습니다.** 자동 모드를 켜면 지금처럼 테두리가 돌고, 끄면 사라집니다.
- **게임 규칙(자동 생산 동작, 골드 차감, 큐 처리)은 하나도 건드리지 않습니다.**
- **다른 창(MistShrine 창, 스킬 창, 연구 창)은 손대지 않습니다.**

---

## ⚠️ 기존 로직 제거 여부 (WORKFLOW.md [4] 최상단 기술 규칙)

> **요약: 직렬화 필드 1개 + 그에 딸린 갱신 블록 1개 + 빈 헤더 1개를 "주석 처리가 아니라 제거"한다.**
> WORKFLOW.md [4]는 *"검증 전까지는 제거 대신 비활성화(주석 처리)"* 를 기본으로 요구하므로,
> **그 기본을 따르지 않는 근거를 아래에 남긴다.**

### 제거 대상 3건

| # | 제거 대상 | 위치 | 성격 |
|:-:|----------|------|------|
| 1 | `[SerializeField] private List<GameObject> _unitAutoIndicators;` | `ProductionPanelUI.cs` **43행** | 직렬화 필드 |
| 2 | `UpdateUI()` 내 도트 인디케이터 갱신 블록 (주석 1줄 + `if` 4줄) | `ProductionPanelUI.cs` **739~743행** | ①의 **유일한 소비처** |
| 3 | `[Header("Auto Indicators")]` | `ProductionPanelUI.cs` **42행** | ①을 지우면 필드 없는 **빈 헤더**가 된다 |

### 주석 처리가 아니라 제거를 택하는 근거

| 근거 | 상세 |
|------|------|
| **① 직렬화 필드는 주석 처리해도 "비활성화"가 성립하지 않는다** | `[SerializeField]` 필드를 주석 처리하면 Unity 직렬화 대상에서 즉시 빠진다. 씬에 저장된 값은 **주석 처리든 삭제든 똑같이 버려진다.** 즉 "언제든 주석만 풀면 되돌아온다"가 성립하지 않고 — 되돌리려면 **Inspector에서 3개 오브젝트를 다시 배선해야 한다.** 주석 처리는 되돌리기 비용을 낮춰 주지 못하면서 **죽은 코드만 남긴다.** (MistShrine 사이클이 같은 판단을 내린 선례: `_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/Plan.md` 최상단 「기존 로직 제거 여부」 ②) |
| **② 되돌릴 대체 경로가 이미 존재하며, 같은 커밋 안에서 공백이 없다** | 제거 대상이 담당하던 표시 기능은 **`CanvasGroup` 경로(735~736행)가 이미 100% 동일하게 수행 중**이다. 새로 만드는 것이 아니라 **이미 나란히 돌고 있는 두 경로 중 하나를 걷어내는 것**이므로, 제거 순간에 기능이 비는 구간이 **존재하지 않는다.** (Research.md §2-3 — 두 경로 모두 728행의 같은 `isAuto`로 구동) |
| **③ 주석으로 남기면 "스위치가 둘"이라는 문제가 문서상으로만 사라진다** | 이번 작업의 목적 자체가 **"제어 경로를 하나로 만든다"** 이다. 주석 처리된 `SetActive` 블록이 파일에 남아 있으면 다음 사람이 *"이건 왜 꺼져 있지? 다시 켜야 하나?"* 를 판단해야 하고, **문제의 본질(둘 중 어느 쪽이 진짜인가)이 그대로 남는다.** |
| **④ 제거 범위가 한 파일 · 세 지점으로 완결되고 회귀 표면이 없다** | `_unitAutoIndicators`는 **`private` 필드**이며, 리포지토리 전수 검색 결과 참조가 `ProductionPanelUI.cs` 3줄뿐이다(Research.md §4). 프리팹 배선 없음, 에디터 스크립트 배선 없음. **다른 시스템으로 번질 경로가 구조적으로 없다.** |

### 제거하지 않는 것 (명시)

- **`BorderOverlay` GameObject 3개는 씬에서 삭제하지 않는다.** `_unitBorderOverlays`가 계속 참조하므로 그대로 유지된다.
- **`_unitBorderOverlays` · `_unitBorderOverlayCgs` · `Awake()`의 캐시 구축 경로는 한 줄도 건드리지 않는다.**
- **`[FormerlySerializedAs]`는 붙이지 않는다** — 이번 건은 필드 **이름 변경**이 아니라 **필드 삭제**라 적용 대상이 아니다.

---

## 1. 채택안과 기각안

**표기:** `UI 공통 n` = `GameSystemRules_UI.md` **공통 UI 규칙 n** / `UI MistShrine n` = 같은 문서 **MistShrine 패널 UI 규칙 n**
> `GameSystemRules_UI.md`는 **섹션마다 규칙 번호가 1부터 다시 시작**하므로, 이 문서의 모든 규칙 참조는 **반드시 섹션명을 함께 적는다**
> (같은 문서 5~8행 경고 · WORKFLOW.md [11] ③).

| 안 | 내용 | 판정 | 근거 |
|:-:|------|:-:|------|
| **A** | `_unitAutoIndicators` 제거 → **`CanvasGroup` 단일 경로**로 통일 | **채택** | `UI 공통 5`(숨김은 `SetActive` 대신 `CanvasGroup`)의 정면 요구. `UI MistShrine 14`가 정한 단일 경로 방식과 일치. 제거해도 잃는 것이 없음이 씬 실측으로 확인됨(Research.md §7-3) |
| B | `_unitBorderOverlays` 제거 → `SetActive` 단일 경로 | **기각** | `Awake()` 78~84행이 `overlay.material = _instancedAutoMaterial`을 수행한다. `Image.material`은 `GameObject`로 접근 불가 → **`List<Image>` 타입이 구조적으로 필수**(Research.md §5-1). 게다가 `UI 공통 5`가 `SetActive` 숨김을 금지한다 |
| C | 현행 유지 | **기각** | 시각 버그는 없으나 값의 단일 소스 위반과 `SetActive` 함정이 남는다(Research.md §7-1·§7-2). `UI MistShrine 14`가 *"정리할지는 별도 판단 필요"* 로 남겨 둔 판단을 계속 미루게 된다 |

---

## 2. 근거 규칙

| # | 규칙 | 이번 작업에 적용되는 부분 |
|:-:|------|------------------------|
| R-1 | **`GameSystemRules_UI.md` 공통 UI 규칙 5** (CanvasGroup 숨김/표시 패턴) | *"UI 요소를 숨길 때 `SetActive(false)` 대신 `CanvasGroup`을 사용한다"* — 표에 명시된 숨김 상태는 `alpha=0 / blocksRaycasts=false / interactable=false`다. 현재 코드의 `SetActive(isAuto)`는 이 규칙에 **정면으로 어긋나는 잔재**이며, 제거가 곧 규칙 준수다 |
| R-2 | **`GameSystemRules_UI.md` MistShrine 패널 UI 규칙 14** (자동 모드 표시는 테두리 회전 효과만 · 오버레이 오브젝트 하나) | *"`ProductionPanelUI`의 `_unitAutoIndicators`와 `_unitBorderOverlays`는 같은 `BorderOverlay` 오브젝트를 가리키는 중복 배선"* · *"도트 인디케이터 UI는 프로젝트에 실재하지 않는다"* · *"MistShrine 패널은 이 중복 구조를 복제하지 않는다"*. 말미의 *"정리할지는 이 문서의 범위가 아니다(별도 판단 필요)"* 가 **이번 작업이 수행하는 그 별도 판단**이다 |
| R-3 | **`GameSystemRules_UI.md` 생산 패널 UI 규칙 4·5** (롱프레스 자동 생산 토글 / 자동 생산 중 유닛 탭) | 이번 작업은 **표시 배선만** 바꾸고 이 두 규칙이 정한 **조작·동작은 전혀 바꾸지 않는다.** 회귀가 없어야 할 기준선이 이 두 규칙이다 |
| R-4 | **`GameSystemRules_UI.md` 생산 패널 UI 규칙 18~22** (자동 생산 규칙) | 동일 — 자동 생산 **로직**은 이번 범위 밖이며 무변경이어야 한다 |
| R-5 | **`.claude/MEMORY.md` — 값의 단일 소스 원칙** ("같은 값을 두 곳에 저장하지 않는다") | 같은 표시 상태를 두 직렬화 필드가 나눠 들고 있는 현 구조가 이 원칙 위반이다 |
| R-6 | **CLAUDE.md 규칙 6** (작업 범위 초과 금지) | 자동 생산 로직·다른 패널·레이아웃 개선은 손대지 않는다 → §7 |
| R-7 | **CLAUDE.md 규칙 8** (주석은 상세하게 — 초급 개발자도 이해 가능하게) | 남는 `CanvasGroup` 경로에 *"제어 경로는 이곳 하나뿐"* 임을 주석으로 명시한다 → §3-2 |
| R-8 | **WORKFLOW.md [4] 기존 로직 제거 규칙** | 제거 근거를 문서 최상단에 기술 → 이 문서 「⚠️ 기존 로직 제거 여부」 |

---

## 3. 단계별 구현 계획

### 3-1. [1단계] 코드 수정 — `ProductionPanelUI.cs` (수정 1파일)

> **행 번호는 현재 파일 기준이며, 위에서부터 지우면 아래 번호가 밀린다.**
> **아래쪽(739~743행)부터 먼저 처리한 뒤 위쪽(42~43행)을 처리할 것.**

| # | 위치 | 작업 | 근거 |
|:-:|------|------|------|
| ① | **739~743행** | 도트 인디케이터 갱신 블록 **삭제** (주석 `// ── 기존 도트 인디케이터 갱신 ──` 포함 5줄) | R-1 · R-2 |
| ② | **42~43행** | `[Header("Auto Indicators")]` + `_unitAutoIndicators` 필드 선언 **삭제** (2줄) | R-2 · Research.md §10-② |
| ③ | **733~737행** | 남는 `CanvasGroup` 블록의 주석을 **"이제 이 경로가 유일한 제어 지점"** 임이 드러나게 보강 | R-7 |
| ④ | **58행** | 들여쓰기 복구 (열 0 → 주변과 동일한 8칸) | §7-③ 판단에 따름 |

**① 삭제 대상 (현재 코드 그대로):**

```csharp
                    // ── 기존 도트 인디케이터 갱신 ──
if (_unitAutoIndicators != null && i < _unitAutoIndicators.Count && _unitAutoIndicators[i] != null)
                    {
                        _unitAutoIndicators[i].SetActive(isAuto);
                    }
```

**② 삭제 대상:**

```csharp
        [Header("Auto Indicators")]
        [SerializeField] private List<GameObject> _unitAutoIndicators;
```

**③ 주석 보강 방침 (문구는 game-programmer가 확정):** 아래 3가지가 드러나야 한다.
- 이 `CanvasGroup` 경로가 **테두리 표시의 유일한 제어 지점**이라는 것
- `SetActive`를 **쓰지 않는 이유** — `UI 공통 5`(레이아웃 붕괴 + 내부 로직 정지) 및 `Awake` 미실행 함정
- `blocksRaycasts`를 가시성과 맞추는 이유 (기존 732행 주석 유지)

**수정 후 `UpdateUI()` 루프의 형태 (예상):**

```csharp
for (int i = 0; i < _unitButtons.Count; i++)
{
    if (_unitButtons[i] == null) continue;
    bool isAuto = i < _activeUnitTypes.Count && state.AutoTypes.Contains(_activeUnitTypes[i]);

    // ── 자동 생산 시각 효과 갱신 (유일한 제어 경로) ──
    if (_unitBorderOverlayCgs != null && i < _unitBorderOverlayCgs.Count && _unitBorderOverlayCgs[i] != null)
    {
        _unitBorderOverlayCgs[i].alpha = isAuto ? 1f : 0f;
        _unitBorderOverlayCgs[i].blocksRaycasts = isAuto;
    }
}
```

**손대지 않는 것 (명시):**

| 대상 | 행 | 사유 |
|------|:-:|------|
| `Awake()` 머티리얼 인스턴스화 | 72~85 | `_unitBorderOverlays`만 읽는다. 무관 |
| `Awake()` CanvasGroup 캐시 구축 | 91~108 | `_unitBorderOverlays`만 읽는다. 무관 (Research.md §5) |
| `UpdateMaterialProperties()` / `OnValidate()` | 114~133 | 무관 |
| `_unitLockIndicators` / `_unitButtonGroups` 처리 | 757~795 · 992~1002 | **별개 시스템.** 이번 범위 밖 |
| 자동 생산 로직 전반 (`OnUnitTap` / `OnUnitLongPress` / `HandleToggleAuto`) | 424~532 | R-3 · R-4 — **표시만 바꾸고 동작은 무변경** |

---

### 3-2. [2단계] 씬 배선 정리 — `Game.unity` (저장 1회)

**핵심: 별도 에디터 스크립트를 만들지 않는다.** (Research.md §8)

| 단계 | 내용 |
|:-:|------|
| 1 | 1단계 코드 수정 후 Unity 에디터에서 `Assets/_Project/Scenes/Game.unity`를 연다 |
| 2 | `ProductionPopup` 오브젝트의 Inspector에서 **`Auto Indicators` 헤더와 그 아래 리스트가 사라졌는지** 육안 확인 |
| 3 | **씬을 저장한다.** 대응 필드가 없어진 YAML 키(`Game.unity` 46904~46907행 4줄)가 자동으로 사라진다 |
| 4 | 저장 후 `Game.unity`에서 `_unitAutoIndicators` 문자열이 **0건**임을 확인한다 |

> **에디터 스크립트가 불필요한 이유:** 이번 건은 **필드가 사라질 뿐 대상 오브젝트는 그대로 남는다.**
> `BorderOverlay` 3개는 `_unitBorderOverlays`가 계속 참조하므로 고아가 되지 않고, 새로 배선할 것도 없다.
> WORKFLOW.md [5-2]가 말하는 "Inspector에서 수동으로 해야 하는 작업"이 존재하지 않으므로 **[5-2]는 생략된다.**

> **⚠️ 씬 저장 순서 주의:** **반드시 1단계(코드 제거)가 컴파일된 뒤에 저장**해야 한다.
> 코드가 아직 옛 상태인 채로 저장하면 키가 그대로 유지되어 4단계 확인이 실패한다.

---

### 3-3. [3단계] 검증

**Unity가 이 환경에 없으므로 컴파일·실기 검증은 사용자 단계에서 수행된다**(§5 R-4).

| # | 확인 항목 | 방법 | 기대 결과 |
|:-:|----------|------|----------|
| V-1 | 컴파일 통과 | Unity 에디터 콘솔 | 에러 0건 |
| V-2 | `_unitAutoIndicators` 잔재 0건 | `ProductionPanelUI.cs` 검색 | 0건 |
| V-3 | 씬 잔여 키 제거 | `Game.unity`에서 `_unitAutoIndicators` 검색 | **0건** (2단계 저장 후) |
| V-4 | 자동 모드 ON → 테두리 표시 | 유닛 버튼 롱프레스(0.5초) | 테두리 회전 효과가 나타난다 (`UI 생산 패널 4`) |
| V-5 | 자동 모드 OFF → 테두리 사라짐 | 자동 중인 유닛 버튼 짧은 탭 | 테두리가 사라진다 (`UI 생산 패널 5`) |
| V-6 | 유닛 버튼 입력 정상 | 자동 모드 ON 상태에서 그 버튼 탭 | 자동 해제로 처리된다 — 테두리가 입력을 가로채지 않는다 |
| V-7 | 유닛 3개 슬롯 전부 독립 동작 | 슬롯 0·1·2 각각 토글 | 해당 슬롯 테두리만 반응 |
| V-8 | 유닛 없는 슬롯에서 테두리 미표출 | 유닛 2종 건물(2유닛 배치) 패널 열기 | 숨김 슬롯(인덱스 1)에 테두리가 보이지 않는다 |
| V-9 | 패널 재열기 후에도 정상 | 자동 ON → 패널 닫기 → 다시 열기 | 테두리가 다시 표시된다 |
| V-10 | 다른 패널 무영향 | MistShrine·스킬·연구 패널 열기 | 변화 없음 |

> **V-8·V-9는 이번 변경에서 특별히 확인해야 하는 항목이다.** `SetActive`가 없어져 `BorderOverlay`가
> 항상 활성으로 남게 되므로, "숨겨야 할 상황에서 정말 안 보이는가"를 실기로 확인해야 한다.
> (정적 분석상으로는 부모 CanvasGroup alpha 전파로 숨겨지는 것이 확인되었다 — Research.md §7-3)

> **Testcase.md는 작성하지 않는다** — WORKFLOW.md [5-1]에 따라 사용자의 명시적 지시가 있을 때만 작성한다.
> 위 표는 Plan 내부의 검증 항목이며 TC 문서가 아니다.

---

## 4. 수정 파일 목록 (예정)

```
[수정]
- Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs
- Assets/_Project/Scenes/Game.unity        ← 코드 수정 후 에디터에서 저장 (잔여 직렬화 키 제거)

[추가]
- 없음

[삭제]
- 없음   ← BorderOverlay 오브젝트는 유지된다
```

**작업 문서(별도 항목):**
```
[추가]
- Assets/_Project/Docs/_Tasks/2026-08-13/05_05_production-auto-indicator-dedup/Research.md
- Assets/_Project/Docs/_Tasks/2026-08-13/05_05_production-auto-indicator-dedup/Plan.md
```

---

## 5. 위험 요소

| ID | 위험 | 발생 조건 | 심각도 | 대응 |
|:-:|------|----------|:-:|------|
| **R-1** | **씬을 저장하지 않아 필드만 사라지고 오브젝트 참조가 씬에 남는다** | 코드만 고치고 `Game.unity`를 열어 저장하지 않음 | **낮음(기능) / 중간(위생)** | 런타임 동작에는 **영향이 없다** — Unity가 대응 필드 없는 키를 무시한다. 다만 파일에 죽은 4줄이 남아 다음 사람이 *"이 필드는 어디 갔지?"* 로 혼동한다. **§3-2 4단계(잔재 0건 확인)를 완료 판정에 포함**해 강제한다 |
| **R-2** | **`SetActive` 제거 후 자동 모드 OFF 상태에서 테두리 오브젝트가 계속 활성으로 남는다** | 항상 | **낮음** | 씬 실측으로 부작용이 없음을 확인했다(Research.md §7-3): ⓐ **입력** — Image `m_RaycastTarget: 0` + 코드 736행 `blocksRaycasts = isAuto` + CanvasGroup 저장값 `m_BlocksRaycasts: 0`의 **3중 차단** ⓑ **성능** — CanvasRenderer `m_CullTransparentMesh: 1`로 실효 alpha 0이면 메시 렌더링 생략 ⓒ **레이아웃** — LayoutElement `m_IgnoreLayout: 1`. **실기 V-6·V-8로 재확인한다** |
| **R-3** | **유닛 없는 슬롯의 테두리가 새어 보인다** | 유닛 2종 건물(2유닛 배치)에서 숨김 슬롯이 활성으로 남음 | **낮음** | 정적 분석상 부모 버튼 CanvasGroup(`_unitButtonGroups[i]`, `BindButtonUnitTypes` 994행에서 `alpha=0`)이 자식에 곱해져 함께 숨겨진다. **다만 이번 변경으로 처음 노출되는 경로이므로 V-8로 실기 확인 필수** |
| **R-4** | **컴파일 미검증** | 이 환경에 Unity가 없다 | **중간** | 이 문서는 코드를 수정하지 않는다. 구현은 game-programmer가 수행하고, **컴파일 확인은 사용자 에디터에서 수행**한다(V-1). 변경이 "삭제 3지점 + 주석"뿐이라 컴파일 위험 표면은 매우 작다 |
| **R-5** | **행 번호 밀림으로 잘못된 줄을 삭제한다** | 42~43행을 먼저 지운 뒤 739~743행을 찾을 때 | **중간** | **§3-1의 "아래쪽부터" 순서를 지킨다.** 또는 행 번호가 아니라 **문자열 일치로 편집**한다 |
| **R-6** | **런타임에 `SetActive(false)`가 걸린 상태로 씬이 저장되어 오브젝트가 비활성으로 굳는다** | **이번 작업을 하지 않고 방치할 경우**의 위험 | **중간** | **이번 작업이 이 위험을 제거하는 것 자체가 목적이다.** 다만 **작업 직전에 씬 상태를 확인**해야 한다 — 현재 3개 모두 `m_IsActive: 1`임은 확인했으나(Research.md §3-3), 구현 시점에 누군가 그 사이 저장했다면 비활성일 수 있다. **구현 전 `m_IsActive` 재확인, 비활성이면 활성으로 되돌린 뒤 진행** |
| **R-7** | **규칙 14 본문이 "현재 중복 배선이다"라고 남아 사실과 어긋난다** | 구현 완료 후 | **낮음** | `GameSystemRules_UI.md` 565~572행은 **구현·사용자 테스트 통과 후 [11] 문서 반영 단계**에서 갱신한다. **아직 구현 전이므로 지금 고치지 않는다** — §7-⑤ |
| **R-8** | **`SetActive` 제거로 실제 시각 동작이 달라진다** | 이론상 | **매우 낮음** | 두 경로가 **같은 `isAuto`로 항상 동시에 구동**되었고(Research.md §2-3), OFF 시 `alpha=0`이 이미 시각적 은폐를 완결하고 있었다. **`SetActive`는 그 위에 얹힌 중복 조치**였으므로 제거해도 보이는 결과가 같다. V-4·V-5로 확인 |
| **R-9** | 테두리 회전 위상이 어긋나거나 튄다 | — | **없음(해당 없음)** | 회전은 셰이더 `_Time.y` 구동이라 활성/비활성과 무관하다(Research.md §6). **사전 조사가 우려했던 항목이나 실측으로 기각되었다.** 단, 향후 이 테두리를 `Update` 구동으로 바꾸면 이 판단이 무효가 된다 |

---

## 6. 완료 판정 체크리스트

### 코드
- [ ] `ProductionPanelUI.cs` 42~43행(`[Header("Auto Indicators")]` + `_unitAutoIndicators`) 제거
- [ ] `ProductionPanelUI.cs` 739~743행(도트 인디케이터 갱신 블록 5줄) 제거
- [ ] 파일 전체에서 `_unitAutoIndicators` 문자열 **0건**
- [ ] 남은 `CanvasGroup` 블록에 **"유일한 제어 경로"** 임이 주석으로 명시됨 (R-7)
- [ ] `Awake()`의 머티리얼 인스턴스화·CanvasGroup 캐시 경로 **무변경**
- [ ] 자동 생산 **로직**(`OnUnitTap` / `OnUnitLongPress` / `HandleToggleAuto`) **무변경** (R-3·R-4)
- [ ] 58행 들여쓰기 처리 여부가 §7-③ 판단대로 반영됨

### 씬
- [ ] 구현 전 `BorderOverlay` 3개의 `m_IsActive`가 `1`임을 재확인 (R-6)
- [ ] Unity 에디터에서 `Game.unity`를 열고 **저장** 완료
- [ ] `Game.unity`에서 `_unitAutoIndicators` 문자열 **0건** (R-1)
- [ ] `_unitBorderOverlays` 배선 3건은 **그대로 유지**됨
- [ ] `BorderOverlay` GameObject 3개가 씬에 **그대로 존재**

### 실기 (사용자 테스트 — WORKFLOW.md [6])
- [ ] V-1 컴파일 에러 0건
- [ ] V-4 롱프레스 → 테두리 표시
- [ ] V-5 자동 중 탭 → 테두리 사라짐
- [ ] V-6 테두리가 버튼 입력을 가로채지 않음
- [ ] V-7 슬롯 3개 독립 동작
- [ ] V-8 **유닛 없는 슬롯에 테두리 미표출** (R-3 — 이번 변경의 핵심 확인 항목)
- [ ] V-9 패널 재열기 후에도 정상
- [ ] V-10 다른 패널 무영향

### 문서 (테스트 통과 후 — WORKFLOW.md [7]~[11], **이번 문서 작성 범위 아님**)
- [ ] `GameSystemRules_UI.md` **MistShrine 패널 UI 규칙 14** 본문을 정리 완료 사실에 맞게 갱신 (R-7)
- [ ] `.claude/agent-memory/game-programmer/MEMORY.md` 371~372행 "알려진 기술부채" 항목 갱신
- [ ] `Assets/Editor/Setup/MistShrineSetup_Scene.cs` 446~449행 **주석** 갱신 여부 판단
- [ ] `PROJECT_STATUS.md` / `WORK_HISTORY.md` 반영
- [ ] `python3 Tools/check_docs.py` **0건** 확인

---

## 7. 이번 범위 밖 (CLAUDE.md 규칙 6 — 범위 초과 금지)

| # | 항목 | 판단 | 사유 |
|:-:|------|:-:|------|
| ① | 자동 생산 **로직** 개선 (`UI 생산 패널 18~22`) | **범위 밖** | 이번은 **표시 배선 정리**다. 로직에 손대면 회귀 표면이 급격히 넓어진다 |
| ② | `_unitLockIndicators` · `_unitButtonGroups` 등 다른 표시 배선 점검 | **범위 밖** | 별개 시스템이며 중복 배선 정황이 확인되지 않았다. 필요하면 별도 작업으로 제안 |
| ③ | **들여쓰기 붕괴 정리 (58행)** | **범위 안 — 단, 58행만** | **판단 근거:** 붕괴 2곳 중 **740행은 이번에 삭제되는 블록 자체**라 자동으로 사라진다. 남는 것은 58행 하나이며, 이는 **이번 작업이 다루는 바로 그 필드 쌍(`_unitBorderOverlays`)의 선언 줄**이라 같은 화면 안에서 처리된다. 별도 커밋으로 미루면 "왜 하나만 고쳤나"라는 의문만 남는다. **공백 문자만 바꾸는 무위험 변경**이므로 함께 처리한다 |
| ④ | `blocksRaycasts` 취급을 MistShrine 방식(초기 1회 고정)으로 통일 | **범위 밖** | 두 방식 모두 결과가 같고(Image `m_RaycastTarget: 0`), 현행 매 갱신 토글도 규칙 위반이 아니다. **불필요한 변경**이므로 하지 않는다 |
| ⑤ | `GameSystemRules_UI.md` 규칙 14 본문 갱신 | **범위 밖 (시점 문제)** | **아직 구현 전이다.** WORKFLOW.md [11]에 따라 사용자 테스트 통과 후 문서 반영 단계에서 처리한다. 지금 고치면 **구현되지 않은 상태를 완료로 기록**하게 된다 |
| ⑥ | `PROJECT_STATUS.md` / `ROADMAP.md` / `WORK_HISTORY.md` 갱신 | **범위 밖 (시점 문제)** | ⑤와 동일 |
| ⑦ | `Testcase.md` 작성 | **범위 밖** | WORKFLOW.md [5-1] — 사용자 명시 지시가 있을 때만 작성한다 |
| ⑧ | 다른 패널(MistShrine·스킬·연구·건물 배치)의 유사 구조 점검 | **범위 밖** | MistShrine은 이미 단일 경로임을 확인했다(Research.md §9). 나머지는 조사하지 않았으므로 **문제가 있다고 단정하지 않는다**(CLAUDE.md 규칙 10). 필요하면 별도 조사 작업으로 제안 |

---

## 8. 위임 대상

| 단계 | 담당 | 비고 |
|------|------|------|
| 코드 수정 (§3-1) | **game-programmer** | 이 Plan과 Research를 함께 전달. `.claude/MEMORY.md` 동봉(CLAUDE.md 체크리스트 [3]) |
| 씬 저장 (§3-2) | **사용자** | Unity 에디터 필요. 에디터 스크립트 불필요 → WORKFLOW.md [5-2] 생략 |
| 실기 검증 (§3-3) | **사용자** | WORKFLOW.md [6] |
| 문서·메모리 반영 | **document-manager** | WORKFLOW.md [7]~[11] — 테스트 통과 후 |

---

## 9. 승인 요청

**이 Plan은 아직 승인되지 않았다. WORKFLOW.md [4]에 따라 사용자의 명시적 승인 전까지 구현을 시작하지 않는다.**

사용자가 특히 확인해 주셔야 할 판단 2가지:

1. **A안(`CanvasGroup` 단일 경로) 채택에 동의하시는지** — §1
2. **58행 들여쓰기 복구를 이번 범위에 포함하는 것에 동의하시는지** — §7-③
   (포함하지 않기를 원하시면 §3-1 ④와 완료 판정의 해당 항목을 제거한다)
