# Research — ProductionPanelUI 자동 생산 인디케이터 중복 배선 정리

**작성일:** 2026-08-13
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-13/05_05_production-auto-indicator-dedup/`
**대상 코드:** `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`
**대상 씬(읽기 전용):** `Assets/_Project/Scenes/Game.unity`
**후속 문서:** [Plan.md](Plan.md)
**현재 상태:** **조사 완료 / 코드·씬 무변경** (이 문서 작성 시점에 어떤 파일도 수정하지 않았다)

---

## 이 조사가 무엇이고 왜 필요한가 (자연어 설명 — 기술 용어 없이)

배럭(병사를 뽑는 건물)을 누르면 뜨는 창에는 유닛 버튼이 3개 있습니다.
그 버튼을 길게 누르면 **"자동으로 계속 뽑기"** 가 켜지고, 켜졌다는 것을 알려주기 위해
**버튼 테두리가 빙글빙글 도는 효과**가 나타납니다.

그런데 프로그램 안을 들여다보니, **이 테두리 하나를 켜고 끄는 스위치가 두 개** 달려 있었습니다.

- 스위치 A: "테두리를 **투명하게 만들어** 안 보이게 한다"
- 스위치 B: "테두리 자체를 **꺼 버린다**"

두 스위치는 **같은 테두리 하나**에 연결되어 있고, 지금은 항상 같은 방향으로 함께 움직이기 때문에
**화면에는 아무 문제가 보이지 않습니다.** 즉 지금 당장 고장 난 곳은 없습니다.

문제는 **"나중에"** 입니다.
누군가 한쪽 스위치만 손대면 그 순간부터 둘이 어긋나기 시작하고,
어긋난 이유를 찾기가 매우 어렵습니다. 스위치가 두 개라는 사실 자체를 모르기 때문입니다.

게다가 스위치 B("꺼 버린다")는 이 프로젝트에서 **이미 한 번 사고를 낸 방식**입니다.
최근 MistShrine 작업에서, 꺼 놓은 채로 저장된 물건이 **다시 켜지지 않아 영영 안 보이는 버그**가 났습니다.
같은 종류의 함정이 여기에도 남아 있는 셈입니다.

그래서 이번 조사는 **스위치를 하나로 줄여도 안전한지**를 확인하기 위한 것입니다.
이 문서는 조사 결과만 담고 있으며, **실제로 고치는 것은 다음 단계**입니다.

### 이 조사에서 새로 밝혀진 것 두 가지

**첫째, 씬을 직접 열어 두 스위치가 정말 같은 물건에 붙어 있는지 확인했습니다.**
지금까지는 "그렇다고 적혀 있다"는 문서 기록만 있었는데, 이번에 **씬 파일의 실제 연결 번호를 하나하나 대조해**
같은 물건이 맞다는 것을 **직접 확인**했습니다. (§3)

**둘째, "테두리 회전이 멈춘다"는 걱정은 사실이 아니었습니다.**
사전 조사에서 "스위치 B로 꺼 버리면 테두리 도는 애니메이션이 멈춘다"는 우려가 있었는데,
확인해 보니 **회전은 프로그램이 아니라 그림 효과가 스스로 시간을 보고 도는 방식**이라
껐다 켜도 멈추거나 튀지 않습니다. **이 우려는 근거가 없으므로 정리 이유에서 제외**했습니다. (§6)

> 다만 정리해야 할 이유 자체는 그대로 남아 있습니다 — **스위치가 둘이라는 것** 그 자체입니다. (§7)

---

## 1. 조사 범위와 방법

| # | 확인 항목 | 방법 | 결과 |
|:-:|----------|------|------|
| 1 | 코드에 자동 생산 표시용 직렬화 필드가 몇 개인지 | `ProductionPanelUI.cs` 전문 통독 | **2개 확인** — §2 |
| 2 | 두 필드가 씬에서 같은 오브젝트를 가리키는지 | `Game.unity`의 fileID 대조 | **확인됨(추정 아님)** — §3 |
| 3 | 두 필드를 참조하는 코드가 다른 파일에 있는지 | 리포지토리 전체 검색 | **없음** — §4 |
| 4 | `_unitBorderOverlayCgs` 캐시 구축 경로의 동작 | 코드 실측 | **정상 · 제거와 무관** — §5 |
| 5 | 테두리 회전이 `SetActive`에 영향받는지 | 셰이더 실측 | **영향 없음(사전 조사 정정)** — §6 |
| 6 | 필드 제거 시 씬에 남는 데이터 처리 | 씬 YAML 구조 확인 + 프로젝트 선례 | §8 |
| 7 | MistShrine 패널의 단일 경로 선례 | `MistShrinePanelUI.cs` 실측 | **선례 확인** — §9 |

> **읽기만 수행했다.** 코드·프리팹·씬·에셋 어느 것도 수정하지 않았고, git 명령도 실행하지 않았다.

---

## 2. 코드 실측 — 자동 생산 표시용 직렬화 필드 2개

`Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`

### 2-1. 필드 선언 (2개)

| 행 | 선언 | 헤더 | 코드 주석 |
|:-:|------|------|-----------|
| **43** | `[SerializeField] private List<GameObject> _unitAutoIndicators;` | `[Header("Auto Indicators")]` (42행) | 없음 |
| **58** | `[SerializeField] private List<UnityEngine.UI.Image> _unitBorderOverlays;` | `[Header("Auto Production Effect")]` (45행) | 57행 Tooltip: *"각 유닛 버튼의 테두리 효과를 담당하는 오버레이 이미지 리스트. 버튼 리스트와 1:1 매칭."* |

**"기존 도트 인디케이터"라는 표현의 정확한 위치 — 사전 조사 정정 1건.**
사전 조사는 이 표현이 **43행 필드 선언의 주석**에 있다고 전달했으나, 실측 결과 43행에는 주석이 없다.
해당 문구는 **739행의 갱신 블록 주석** 한 곳에만 존재한다.

```
739:                    // ── 기존 도트 인디케이터 갱신 ──
```

> 결론 자체(= 도트 인디케이터는 과거 설계의 흔적)는 바뀌지 않으나, **문구의 소재지가 다르므로**
> 코드를 고칠 때 43행 주석을 찾다가 헛수고하지 않도록 여기에 기록해 둔다.

### 2-2. 파생 캐시 필드 (1개, 직렬화 아님)

| 행 | 선언 | 성격 |
|:-:|------|------|
| 63 | `private List<CanvasGroup> _unitBorderOverlayCgs;` | `[SerializeField]` **없음** — 런타임 캐시. `Awake()`가 `_unitBorderOverlays`로부터 구축(§5) |

### 2-3. 이중 제어 지점 — `UpdateUI()` 733~743행

```csharp
733:  if (_unitBorderOverlayCgs != null && i < _unitBorderOverlayCgs.Count && _unitBorderOverlayCgs[i] != null)
734:  {
735:      _unitBorderOverlayCgs[i].alpha = isAuto ? 1f : 0f;      // ① CanvasGroup 경로
736:      _unitBorderOverlayCgs[i].blocksRaycasts = isAuto;
737:  }
738:
739:      // ── 기존 도트 인디케이터 갱신 ──
740:  if (_unitAutoIndicators != null && i < _unitAutoIndicators.Count && _unitAutoIndicators[i] != null)
741:  {
742:      _unitAutoIndicators[i].SetActive(isAuto);               // ② GameObject 경로 (같은 오브젝트)
743:  }
```

- 두 경로 모두 **같은 `isAuto` 값**(728행에서 1회 계산)으로 구동된다 → **두 제어 방향이 항상 일치**한다.
- `_unitAutoIndicators`가 **읽히는 곳은 740·742행 두 줄뿐**이다. 다른 어떤 메서드도 이 필드를 건드리지 않는다(§4).
- **부수 사항 — 들여쓰기 붕괴 2곳:** 58행과 740행이 **열 0에서 시작**한다(주변은 8~20칸 들여쓰기).
  구문 오류는 아니고 과거 편집의 흔적으로 보인다. 처리 여부는 Plan에서 판단한다.

### 2-4. 사전 조사 대비 정정 요약

| # | 사전 조사 서술 | 실측 결과 | 영향 |
|:-:|--------------|----------|------|
| 1 | 43행 **주석**에 "기존 도트 인디케이터" | 43행에 주석 없음. 해당 문구는 **739행** | 결론 불변, 위치만 정정 |
| 2 | 이중 제어 지점이 "730~744행" | 정확히는 **733~743행** | 결론 불변, 행 번호 정밀화 |
| 3 | `SetActive(false)`면 **테두리 회전 애니메이션이 멈춘다** | **사실 아님** — 회전은 셰이더의 `_Time.y` 구동(§6) | **정리 근거에서 이 항목 제외** |

---

## 3. 씬 배선 실측 — **두 필드는 같은 3개 오브젝트를 가리킨다 (확인 완료)**

**대상:** `Assets/_Project/Scenes/Game.unity` (ASCII text YAML, 약 1.5MB — 읽기만 수행)

### 3-1. `ProductionPanelUI` 컴포넌트 블록

```
46855:  --- !u!114 &1959302710   MonoBehaviour
46860:    m_GameObject: {fileID: 1959302708}       ← GameObject 이름 "ProductionPopup"
46866:    m_EditorClassIdentifier: Assembly-CSharp::Hexiege.Presentation.ProductionPanelUI
```

배선된 두 필드의 fileID 목록:

```
46904:  _unitAutoIndicators:            46913:  _unitBorderOverlays:
46905:  - {fileID:  421601954}          46914:  - {fileID:  421601957}
46906:  - {fileID: 2109204902}          46915:  - {fileID: 2109204905}
46907:  - {fileID: 1950394263}          46916:  - {fileID: 1950394266}
```

**fileID 값 자체는 서로 다르다.** 이것만 보면 "다른 오브젝트"로 오해할 수 있으나, Unity YAML에서
`List<GameObject>`는 **GameObject의 fileID**를, `List<Image>`는 **그 GameObject에 붙은 Image 컴포넌트의 fileID**를
가리키므로 값이 다른 것이 정상이다. 실제 동일성은 Image의 `m_GameObject` 역참조로 확인해야 한다.

### 3-2. 역참조 대조 결과 — **3/3 전부 동일 오브젝트**

| 슬롯 | `_unitAutoIndicators[i]`<br>(GameObject fileID) | 그 GameObject의 이름 / 활성 | `_unitBorderOverlays[i]`<br>(Image fileID) | 그 Image의 `m_GameObject` | 동일? |
|:-:|:-:|:-:|:-:|:-:|:-:|
| 0 | `421601954` (12119행) | `BorderOverlay` / `m_IsActive: 1` | `421601957` (12178행) | `421601954` | ✅ |
| 1 | `2109204902` (51808행) | `BorderOverlay` / `m_IsActive: 1` | `2109204905` (51867행) | `2109204902` | ✅ |
| 2 | `1950394263` (46639행) | `BorderOverlay` / `m_IsActive: 1` | `1950394266` (46698행) | `1950394263` | ✅ |

> **결론: `GameSystemRules_UI.md` MistShrine 패널 UI 규칙 14가 기록한 "중복 배선"은 씬 실측으로 확인되었다.**
> 규칙 14는 근거로 "씬 확인 결과"라고만 적고 fileID를 남기지 않았는데, 이 문서가 그 대조표를 남긴다.

### 3-3. `BorderOverlay` 오브젝트의 실제 저장 상태 (슬롯 0 기준 — 3개 모두 동일 구성)

`Game.unity` 12119~12225행 실측:

| 컴포넌트 | fileID | 저장된 값 | 이번 판단에 미치는 의미 |
|---------|:-:|---------|---------------------|
| `GameObject` | 421601954 | 이름 `BorderOverlay`, **`m_IsActive: 1`** | **씬에는 활성 상태로 저장되어 있다.** `SetActive` 제거 시 "비활성으로 저장되어 다시 안 켜지는" 함정에 걸리지 않는다 — §7-2 |
| `RectTransform` | 421601955 | `m_Father: 203499423`, 앵커 (0,0)~(1,1), sizeDelta 0 | **유닛 버튼 슬롯0의 직계 자식**이며 버튼을 가득 덮는다. `203499423`의 GameObject는 `203499422`이고, `_unitButtons[0]`(203499426)·`_unitButtonGroups[0]`(203499425)이 같은 GameObject에 있다 → **버튼 CanvasGroup의 alpha가 테두리에도 곱해져 전파**된다 |
| `LayoutElement` | 421601956 | `m_IgnoreLayout: 1` | 레이아웃 계산에서 제외 → 활성으로 남겨도 형제 배치에 영향이 없다 |
| `Image` | 421601957 | `m_Material`: `mat_ui_rotatingborder`, **`m_RaycastTarget: 0`**, `m_Sprite: {fileID: 0}` | **Raycast Target이 이미 꺼져 있다.** 테두리는 애초에 입력을 가로챌 수 없다 — §7-3 |
| `CanvasRenderer` | 421601958 | **`m_CullTransparentMesh: 1`** | 실효 alpha가 0이면 Unity가 메시 렌더링을 건너뛴다 → 활성으로 남아도 드로우콜이 발생하지 않는다 — §7-3 |
| `CanvasGroup` | 421601959 | **`m_Alpha: 0` / `m_Interactable: 0` / `m_BlocksRaycasts: 0`** | CanvasGroup이 **씬에 이미 존재**한다 → `Awake()`의 `AddComponent` 폴백 경로는 현재 사용되지 않는다 — §5 |

슬롯 1·2의 CanvasGroup(2109204907 / 1950394268)도 동일하게 `m_Alpha: 0` / `m_Interactable: 0` / `m_BlocksRaycasts: 0`으로 저장되어 있다.

---

## 4. 영향 범위 — `_unitAutoIndicators` 참조는 `ProductionPanelUI.cs` 안뿐이다

리포지토리 전체(`/home/user/Hexiege`)에서 세 식별자를 검색한 결과:

| 식별자 | `.cs` 코드 | `.unity` / `.prefab` | 문서 · 메모리 |
|--------|:-:|:-:|:-:|
| `_unitAutoIndicators` | **`ProductionPanelUI.cs` 43·740·742행뿐** | `Game.unity` 46904행(배선 1건) | `GameSystemRules_UI.md` 566행(규칙 14) · `MistShrineSetup_Scene.cs` 447행(**주석 안**) · `.claude/agent-memory/game-programmer/MEMORY.md` 371행 |
| `_unitBorderOverlays` | `ProductionPanelUI.cs` 58·78·80·92·94행 | `Game.unity` 46913행 | 위와 동일 위치 |
| `_unitBorderOverlayCgs` | `ProductionPanelUI.cs` 63·91·100·105·733·735·736행 | — (직렬화 아님) | — |

**판정:**
- **다른 스크립트에서 `_unitAutoIndicators`를 읽거나 쓰는 곳은 없다** (private 필드이므로 구조적으로도 불가).
- `Assets/Editor/Setup/MistShrineSetup_Scene.cs` 446~449행의 언급은 **XML 주석 본문**이며 실행 코드가 아니다.
  즉 **에디터 셋업 스크립트가 이 필드를 배선하지 않는다** — 씬의 배선은 과거에 Inspector에서 수동으로 넣은 것으로 보인다.
- 프리팹에는 배선이 없다. `ProductionPopup`은 **씬 인스턴스**이며 `m_PrefabInstance: {fileID: 0}`로 프리팹 인스턴스가 아니다(46843~46847행).
  → **정리 대상 배선은 `Game.unity` 한 곳뿐이다.**

---

## 5. `_unitBorderOverlayCgs` 캐시 구축 경로 (69~109행) — 제거와 완전히 독립

`Awake()`가 하는 일은 **두 갈래**다.

### 5-1. 머티리얼 인스턴스화 (72~85행)

```csharp
if (_autoProductionMaterial != null)
{
    _instancedAutoMaterial = new Material(_autoProductionMaterial);   // 공유 머티리얼 오염 방지
    UpdateMaterialProperties();                                        // _Speed/_Thickness/_Radius/_Inset 주입
    if (_unitBorderOverlays != null)
        foreach (var overlay in _unitBorderOverlays)
            if (overlay != null) overlay.material = _instancedAutoMaterial;
}
```

→ **`List<Image>` 타입이 반드시 필요한 이유가 여기에 있다.** `Image.material`은 `GameObject`로는 접근할 수 없다.
따라서 **`_unitBorderOverlays` 쪽을 없애고 `SetActive` 단일 경로로 통일하는 안은 성립하지 않는다**(Plan §1 B안 기각 근거).

### 5-2. CanvasGroup 캐시 구축 (91~108행)

```csharp
_unitBorderOverlayCgs = new List<CanvasGroup>();
if (_unitBorderOverlays != null)
    foreach (var overlay in _unitBorderOverlays)
    {
        if (overlay != null)
        {
            var cg = overlay.gameObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = overlay.gameObject.AddComponent<CanvasGroup>();
            _unitBorderOverlayCgs.Add(cg);
        }
        else _unitBorderOverlayCgs.Add(null);   // 인덱스 정합성 유지
    }
```

| 특성 | 실측 내용 |
|------|----------|
| **입력 소스** | `_unitBorderOverlays` **단 하나**. `_unitAutoIndicators`를 전혀 읽지 않는다 |
| **인덱스 정합** | null 슬롯도 `null`로 채워 인덱스를 어긋나게 하지 않는다 |
| **null 안전** | `_unitBorderOverlays`가 null이면 `_unitBorderOverlayCgs`는 **빈 리스트**(null 아님) → 733행 가드가 자연히 통과 실패 |
| **현재 씬에서의 실제 동작** | CanvasGroup이 3개 모두 씬에 이미 존재하므로 `GetComponent`가 성공 → **`AddComponent` 폴백은 실행되지 않는다**(§3-3) |
| **Awake 실행 보장** | `ProductionPopup`(1959302708) `m_IsActive: 1`, 상위 `1259319714`도 `m_IsActive: 1` → **Awake는 씬 로드 시 정상 실행된다** |

> **판정: `_unitAutoIndicators`를 제거해도 이 경로는 한 줄도 영향을 받지 않는다.**
> 두 필드는 소스도 소비처도 겹치지 않는 **완전히 분리된 배관**이며, 유일한 접점은 "같은 오브젝트를 가리킨다"는 씬 배선뿐이다.

---

## 6. 사전 조사 정정 — "SetActive(false)면 테두리 회전이 멈춘다"는 **사실이 아니다**

사전 조사는 정리 근거의 하나로 *"자동 모드 OFF면 오브젝트가 비활성이 되어 테두리 회전 머티리얼 애니메이션이 멈춘다"* 를 들었다.
**셰이더를 실측한 결과 이 서술은 성립하지 않는다.**

| 단계 | 실측 내용 |
|:-:|----------|
| 1 | 씬의 Image `m_Material` → `mat_ui_rotatingborder` (guid `9db30641b36bfbe4c8cc66830cb39e94`) |
| 2 | `Assets/_Project/Materials/UI/mat_ui_rotatingborder.mat` 11행 → `m_Shader` guid `ccb0961efa7c71a4ba2ba214f6099d1b` |
| 3 | `Assets/_Project/Shaders/UI/RotatingBorderUI.shader` **142행**: `float time = _Time.y * _Speed * 0.1;` |

- 회전 위상은 **셰이더 내장 전역 시간 `_Time.y`** 에서 계산된다. C# `Update`나 코루틴이 관여하지 않는다.
- `_Time.y`는 오브젝트 활성/비활성과 무관하게 계속 흐르므로, **꺼졌다 켜져도 위상이 멈추거나 튀지 않는다.**
- 애초에 자동 모드 OFF일 때 테두리는 **보이지 않는 상태**(alpha 0)이므로, 그 구간에 렌더링이 생략되는 것은 손실이 아니다.

> **따라서 이 항목은 정리 근거에서 제외한다.** 실재하는 근거만 남긴다(§7).
> (프로젝트에서 `Update` 기반 회전을 쓰는 다른 인디케이터가 있다면 이야기가 달라지므로,
> 나중에 이 테두리를 스크립트 구동으로 바꾸는 경우 이 결론이 무효가 된다는 점을 함께 기록해 둔다.)

---

## 7. 그렇다면 왜 정리해야 하는가 — 실재하는 근거만

### 7-1. 값의 단일 소스 원칙 위반 (주 근거)

- **같은 표시 상태를 두 개의 직렬화 필드가 나눠 들고 있다.** 두 필드가 같은 오브젝트를 가리킨다는 사실은
  **코드 어디에도 명시되어 있지 않고**, 씬을 열어 fileID를 대조해야만 알 수 있다(§3-2).
- 지금 두 제어 방향이 같은 것은 **728행에서 계산된 `isAuto` 하나를 우연히 둘 다 쓰기 때문**이며,
  구조가 보장하는 것이 아니다. **한쪽 조건만 바뀌면 즉시 어긋난다.**
- 어긋났을 때의 증상은 "테두리가 안 보인다" 하나로 동일한데 원인 후보가 둘이라 **디버깅 비용이 배가된다.**
- 공용 컨텍스트(`.claude/MEMORY.md`)의 **"값의 단일 소스 원칙 — 같은 값을 두 곳에 저장하지 않는다"** 에 정면으로 어긋난다.

### 7-2. `SetActive` 계열 함정의 잔존 (부차 근거)

- `SetActive(false)`는 `CanvasGroup.alpha=0`보다 **강한 상태 변경**이다. 오브젝트가 비활성이면
  **그 위의 `Awake`가 실행되지 않는다.**
- 이 프로젝트는 **바로 직전 사이클에서 이 함정에 실제로 당했다.**
  `MistShrineRangeIndicator` / `SkillAimReticle`이 비활성 저장 → `Awake` 미실행 → 원이 영영 안 보이는 버그
  (`_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/Plan.md` §9-1, `.claude/agent-memory/game-programmer/MEMORY.md` "Awake + SetActive 함정").
- **현재 `BorderOverlay` 3개는 `m_IsActive: 1`로 저장되어 있어 지금은 함정이 발동하지 않는다**(§3-3).
  그러나 **런타임에 `SetActive(false)`가 걸린 상태로 누군가 씬을 저장하면 비활성으로 굳는다.**
  그렇게 되면 자동 모드를 켜도 `SetActive(true)` 직전의 `alpha` 설정만 남고, `BorderOverlay`에 앞으로
  `Awake`를 가진 컴포넌트가 추가될 경우 위 버그가 그대로 재현된다.
- 즉 **지금은 버그가 아니지만, 버그가 될 수 있는 상태를 씬 저장 하나로 만들 수 있다.**

### 7-3. `CanvasGroup` 단일 경로로 통일해도 잃는 것이 없다 (실행 가능성 근거)

`SetActive`를 없애 `BorderOverlay`가 항상 활성으로 남게 되어도, 씬 실측(§3-3) 기준으로 부작용이 없다.

| 우려 | 실측 결과 | 판정 |
|------|----------|------|
| 입력을 가로챈다 | Image `m_RaycastTarget: 0` **+** 코드 736행이 `blocksRaycasts`를 `isAuto`와 동기화 **+** CanvasGroup 저장값 `m_BlocksRaycasts: 0` | **3중으로 차단됨** |
| 드로우콜이 늘어난다 | CanvasRenderer `m_CullTransparentMesh: 1` → 실효 alpha 0이면 메시 렌더링 생략 | 영향 없음 |
| 레이아웃이 밀린다 | LayoutElement `m_IgnoreLayout: 1` | 영향 없음 |
| 유닛 없는 슬롯에서 테두리가 새어 나온다 | 부모 버튼 GameObject의 CanvasGroup(`_unitButtonGroups[i]`)이 `alpha=0`이고, CanvasGroup alpha는 **자식으로 곱해져 전파**된다 | 함께 숨겨짐 |

### 7-4. 규칙 문서의 방향과 일치

`GameSystemRules_UI.md` **MistShrine 패널 UI 규칙 14**는 이 중복 구조를 명시적으로 기록하고
*"MistShrine 패널은 이 중복 구조를 복제하지 않는다 — 오버레이 오브젝트를 하나만 두고 단일 경로로만 on/off 한다"* 고 정했다.
같은 규칙의 말미에 *"`ProductionPanelUI`의 중복 배선 자체를 정리할지는 이 문서의 범위가 아니다(별도 판단 필요)"* 라고
**판단을 유보**해 두었고, 이번 작업이 그 **유보된 별도 판단**에 해당한다.

---

## 8. 씬에 남는 직렬화 데이터 처리

**핵심: 코드에서 필드를 지워도 `Game.unity` 46904~46907행의 4줄은 파일에 그대로 남는다.**

| 단계 | Unity의 동작 | 확인 근거 |
|:-:|------------|----------|
| 1 | 씬 로드 시, C# 클래스에 대응 필드가 없는 YAML 키는 **무시된다** (에러·경고 없음) | Unity 직렬화 일반 동작 |
| 2 | **해당 씬을 에디터에서 한 번 저장하면** 대응 필드가 없는 키가 **자동으로 사라진다** | 동일 |
| 3 | 저장 전까지는 파일에 잔존하지만 **런타임 동작에는 아무 영향이 없다** | — |

**따라서 씬 정리를 위한 별도 에디터 스크립트는 필요하지 않다.** 필요한 것은 **"씬을 한 번 열어 저장"** 뿐이다.

**함께 확인한 사실:**
- `BorderOverlay` GameObject 3개는 `_unitBorderOverlays`가 계속 참조하므로 **씬에서 고아가 되지 않는다.**
  즉 이번 정리로 **오브젝트가 사라지거나 배선이 끊기는 일은 없다.**
- 필드 하나가 사라질 뿐 **대상 오브젝트는 그대로 유지**되므로, MistShrine 사이클의 `_baseDiameter` 사례와는 성격이 다르다.
  그 사례는 **씬에 저장된 값 자체가 버그의 원인**이어서 `[FormerlySerializedAs]`를 **일부러 붙이지 않고** 옛 값을 버렸다
  (`_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/Plan.md` §9-3, 최상단 「기존 로직 제거 여부」 ②).
  이번 건은 **이름을 바꾸는 것이 아니라 필드를 없애는 것**이므로 `[FormerlySerializedAs]`가 애초에 적용 대상이 아니다.

> **미확인으로 남기는 항목:** 이 조사 환경에는 Unity 에디터가 없어 **씬 저장 후 실제로 키가 사라지는지 실행으로 확인하지 못했다.**
> 위 1~3은 Unity 직렬화의 일반 동작에 대한 서술이며, **씬 파일에서 실측으로 검증한 것은 "현재 4줄이 존재한다"는 사실까지**다.

---

## 9. 선행 사례 — MistShrine 패널의 단일 경로 구현

`Assets/_Project/Scripts/Presentation/UI/MistShrinePanelUI.cs` 실측. **`ProductionPanelUI`가 목표로 삼을 형태 그대로다.**

| 요소 | 행 | 내용 |
|------|:-:|------|
| 직렬화 필드 | 73 | `[SerializeField] private Image _autoBorderOverlay;` — **`Image` 하나뿐. `GameObject` 필드 없음** |
| 런타임 캐시 | 102 | `private CanvasGroup _autoBorderCanvasGroup;` |
| 캐시 구축 | 171~184 | `CacheAutoBorderCanvasGroup()` — `TryGetComponent` 실패 시 `AddComponent`. 초기값 `alpha=0` / `blocksRaycasts=false` / `interactable=false`. 주석에 *"표시 제어를 이 CanvasGroup '하나'로만 하기 위한 준비다(UI 규칙 14 — 단일 경로)"* 명시 |
| 표시 제어 | 338~342 | `ApplyAutoBorder(bool isAuto)` → **`_autoBorderCanvasGroup.alpha = isAuto ? 1f : 0f;` 한 줄.** `SetActive` 없음. 주석에 *"표시 제어 경로는 이 메서드 하나뿐이다(UI 규칙 14)"* 명시 |
| 호출 지점 | 237 · 331 · 417 | 패널 표시 시 / 상태 갱신 시 / 네트워크 자동 모드 브로드캐스트 수신 시 — **셋 다 같은 메서드 하나를 경유** |

**`ProductionPanelUI`와의 차이 2가지 (Plan에서 판단이 필요한 지점):**

1. **`blocksRaycasts` 취급이 다르다.**
   - MistShrine: 캐시 구축 시 `blocksRaycasts = false`로 **한 번 고정**하고, `ApplyAutoBorder`는 `alpha`만 건드린다.
   - Production: 736행이 **매 갱신마다 `blocksRaycasts = isAuto`로 토글**한다.
   - Image `m_RaycastTarget: 0`이므로 두 방식 모두 실제 입력 차단 결과는 같다(§7-3).
2. **오버레이 개수가 다르다.** MistShrine은 사용 버튼 1개라 오버레이 1개, Production은 유닛 버튼 3개라 리스트다.
   → **`ApplyAutoBorder` 같은 단일 메서드로 추출하는 형태를 그대로 옮기기보다, 리스트 인덱스 기준으로 유지하는 편이 자연스럽다.**

> 또한 `Assets/Editor/Setup/MistShrineSetup_Scene.cs` 446~449행 주석이
> *"생산 패널(ProductionPanelUI)은 같은 BorderOverlay 오브젝트를 `_unitAutoIndicators`와 `_unitBorderOverlays` 양쪽에 …
> MistShrine 패널은 이 구조를 복제하지 않고 Image 하나만 `_autoBorderOverlay`에 배선한다"* 로 같은 판단을 남겨 두었다.
> **이번 정리가 완료되면 이 주석의 서술이 과거형이 되므로, 문서 반영 단계에서 손볼 대상이 된다.**

---

## 10. 부가 이슈 (이번 조사 중 발견 — 처리 여부는 Plan에서 판단)

| # | 내용 | 위치 | 성격 |
|:-:|------|------|------|
| 1 | **들여쓰기 붕괴 2곳** — 열 0에서 시작 | `ProductionPanelUI.cs` 58행 · 740행 | 스타일. 740행은 이번에 **삭제되는 블록 자체**라 자동 해소되고, 58행만 남는다 |
| 2 | `[Header("Auto Indicators")]`(42행)가 **필드 없는 빈 헤더가 된다** | `ProductionPanelUI.cs` 42행 | 43행 필드를 지우면 헤더만 남아 Inspector에 의미 없는 제목이 뜬다. **함께 제거 대상** |
| 3 | 규칙 14 본문이 **"현재 중복 배선이다"** 라고 현재형으로 서술 | `GameSystemRules_UI.md` 565~572행 | 정리가 끝나면 사실과 어긋난다. **구현·검증 완료 후 문서 반영 단계([11])에서 갱신 대상** |
| 4 | `.claude/agent-memory/game-programmer/MEMORY.md` 371~372행이 이를 **"알려진 기술부채 — 정리는 별도 작업 예정"** 으로 기록 | 메모리 | 정리 완료 후 갱신 대상 |
| 5 | `MistShrineSetup_Scene.cs` 446~449행 주석이 중복 배선을 현재형으로 서술 | 에디터 스크립트 **주석** | 실행 코드 아님. 갱신 여부는 Plan §7에서 판단 |

> **③④⑤는 전부 "구현·검증이 끝난 뒤" 손대는 항목이다.** 아직 구현 전이므로 이번 문서 작성 단계에서는 건드리지 않는다
> (WORKFLOW.md [11] — 문서 반영은 사용자 테스트 통과 후).

---

## 11. 결론

| # | 질문 | 답 | 근거 |
|:-:|------|---|------|
| 1 | 자동 생산 표시 필드가 정말 2개인가? | **그렇다** (43행 / 58행) | §2-1 |
| 2 | 씬에서 같은 오브젝트를 가리키는가? | **그렇다. 3/3 전부 일치 — 씬 실측으로 확인** | §3-2 |
| 3 | 지금 시각 버그가 있는가? | **없다.** 두 제어가 같은 `isAuto`로 구동된다 | §2-3 |
| 4 | `_unitAutoIndicators`를 참조하는 다른 코드가 있는가? | **없다.** `ProductionPanelUI.cs` 3줄뿐 | §4 |
| 5 | 제거하면 CanvasGroup 캐시 경로가 깨지는가? | **깨지지 않는다.** 두 배관은 완전히 독립 | §5 |
| 6 | `_unitBorderOverlays` 쪽을 없애는 안은? | **불가.** `Image.material` 할당 때문에 `List<Image>`가 필수 | §5-1 |
| 7 | "테두리 회전이 멈춘다"가 제거 근거인가? | **아니다 — 사실이 아니다.** 셰이더 `_Time.y` 구동 | §6 |
| 8 | 그래도 정리해야 하는가? | **그렇다.** 값의 단일 소스 위반 + `SetActive` 함정 잔존 + 규칙 14의 유보된 판단 | §7 |
| 9 | 씬 배선 정리에 에디터 스크립트가 필요한가? | **불필요.** 씬을 한 번 열어 저장하면 잔여 키가 사라진다 | §8 |
| 10 | `SetActive` 제거 후 항상 활성으로 남아도 안전한가? | **안전하다.** 입력 3중 차단 · 드로우콜 컬링 · 레이아웃 무시 · 부모 alpha 전파 | §7-3 |

**권고 방향:** `_unitAutoIndicators` 제거 → `CanvasGroup` 단일 경로로 통일 (사전 조사 A안).
구체적 수정 계획·근거 규칙·위험 요소·완료 판정은 [Plan.md](Plan.md)에 기술한다.
