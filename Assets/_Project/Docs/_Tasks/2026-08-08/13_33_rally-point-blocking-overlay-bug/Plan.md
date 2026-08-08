# Plan — 랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정

작성일: 2026-08-08
작업 폴더: `Assets/_Project/Docs/_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/`
선행 문서: [Research.md](Research.md)

---

## 0. 기존 로직 제거 여부 (WORKFLOW.md 기존 로직 제거 규칙)

> **이번 작업에는 기존 로직의 제거·주석 처리가 전혀 없다.**
> 두 곳에 한 줄씩 **추가**하는 순수 가산 수정이며, 기존 코드의 삭제·변경·비활성화는 하지 않는다.
> 따라서 WORKFLOW.md의 "기존 로직 제거 규칙"(제거 근거 명시 / 비활성화 우선)은 이번 작업에 **해당 사항 없음**이다.

---

## 1. 무엇을, 왜 고치는가 (자연어 설명)

### 지금 사용자에게 보이는 문제

배럭(유닛 생산 건물)을 눌러 창을 연 다음 **집결지(랠리포인트) 버튼**을 누르면,
창은 사라지는데 **화면을 덮고 있던 어두운 반투명 막이 그대로 남습니다.**

이 막은 눈에는 "그냥 어두운 배경"으로 보이지만, 실제로는 **화면 전체를 덮은 투명한 유리판**과 같습니다.
그래서 유닛을 보낼 지점을 고르려고 지도를 누르면, 그 터치는 지도에 닿지 않고 **이 유리판이 먼저 받아버립니다.**
이 유리판에는 원래 "누르면 창을 닫는다"는 성질이 붙어 있기 때문에,
집결지가 지정되는 대신 **방금 시작한 지정 작업이 그냥 취소된 것처럼** 보이게 됩니다.
깃발도 나타나지 않고, 아무 일도 없었던 것처럼 느껴집니다.

### 어떻게 고칠 것인가

고치는 방법은 아주 단순합니다. **창을 숨길 때 이 어두운 막도 같이 치우면 됩니다.**

중요한 것은, 이 방법이 **이 프로젝트가 이미 한 번 겪고 해결해 둔 방식 그대로**라는 점입니다.
건물 **스킬 패널**에서도 목표 지점을 고르는 순간 똑같은 문제가 있었고,
그때 "창을 숨길 때 어두운 막도 같이 치운다"는 처리를 넣어 해결했으며,
그 이유까지 코드에 주석으로 적어 두었습니다.

즉 이번 작업은 **새로운 방식을 만들어 내는 것이 아니라, 이미 검증된 방식을 빠져 있던 한 곳에 똑같이 적용하는 것**입니다.
새 구조를 만들지 않으므로 위험이 낮고, 다른 화면의 동작에 영향을 주지 않습니다.

### 함께 고치는 또 한 가지 (사용자 확정 사항)

조사 과정에서, 집결지 지정을 **성공적으로 마쳤을 때**에도 문제가 있음을 확인했습니다.

이 게임은 "어두운 막을 몇 개의 창이 쓰고 있는지"를 **숫자로 세어서** 관리합니다.
창이 열릴 때 1을 더하고, 닫힐 때 1을 뺍니다. 이 숫자가 0이 되어야만 막이 실제로 사라집니다.
그런데 집결지 지정을 완료하는 경로는 **"창을 닫는 절차"를 거치지 않기 때문에, 더한 1을 되돌려 빼주는 곳이 없습니다.**

지금까지 이 문제가 겉으로 드러나지 않았던 이유는, 앞서 설명한 버그(막이 남아 터치를 가로채는 현상)가
대신 "창 닫기"를 실행해 주면서 **우연히 숫자를 맞춰 주고 있었기** 때문입니다.
따라서 첫 번째 문제만 고치고 이 부분을 그대로 두면, **버그를 고친 것이 오히려 숫자를 어긋나게 만들어**
다음에 창을 열었을 때 어두운 막이 제대로 나타나지 않는 새로운 문제가 생길 수 있습니다.

그래서 이 두 가지는 **반드시 함께 고쳐야 하며**, 사용자 확정에 따라 이번 수정 범위에 모두 포함합니다.

---

## 2. 근거 규칙

### 2-1. 주 근거 — `GameSystemRules_UI.md` 공통 UI 규칙 5 (`CanvasGroup 숨김/표시 패턴`) 내 **"반투명 배경 오버레이(BlockingOverlay) 단일 소유 패턴"** (:113-119)

인용:

> - 반투명 배경 오버레이는 **UIManager가 단일 소유**한다. 개별 팝업이 자체 오버레이를 들고 있지 않으며, `UIManager.ShowBlockingOverlay(onTap)` / `UIManager.HideBlockingOverlay()`로만 제어한다.
> - **Popup 모드** — `ShowBlockingOverlay(() => Close())` (콜백 있음): 오버레이를 터치하면 등록된 콜백(팝업 닫기)이 실행된다. (InGameSettingsUI, BuildingPlacementUI, **생산/건물 패널**)
> - 팝업이 중첩될 수 있으므로 UIManager는 **참조 카운터**로 표시 횟수를 누적 관리한다. `HideBlockingOverlay()`가 호출되어 카운터가 0이 될 때에만 실제로 숨겨진다.
> - 호출은 항상 null-safe 패턴 `UIManager.Instance?.ShowBlockingOverlay(...)`를 사용한다.

이 규칙에서 도출되는 이번 수정의 핵심 요구사항 2가지:

| # | 요구사항 | 현재 랠리 경로의 위반 내용 |
|---|---------|--------------------------|
| A | 오버레이는 UIManager만 소유하며, 팝업을 숨긴다고 오버레이가 따라 숨겨지지 않는다. 팝업이 화면에서 사라져야 하는 상황이면 오버레이도 명시적으로 `HideBlockingOverlay()`로 내려야 한다. | `OnRallyPointClick()` (:534)이 `_popup?.Hide()`만 호출 → Popup 모드 오버레이(onTap = `Close`)가 `blocksRaycasts=true`인 채로 잔존 |
| B | 참조 카운터 방식이므로 **`ShowBlockingOverlay` 호출 1회에는 반드시 `HideBlockingOverlay` 호출 1회가 짝을 이뤄야** 카운터가 정확히 관리된다. | 랠리 완료 경로(`CompleteRallyPointSetting`, :536-545)가 `Close()`를 거치지 않아 `Show()` (:207)에서 올린 +1을 반납하는 지점이 전무 |

또한 같은 규칙의 null-safe 호출 요구에 따라, 이번에 추가하는 호출도 전부 `UIManager.Instance?.HideBlockingOverlay();` 형태를 사용한다.

### 2-2. 보조 근거 — `GameSystemRules_Buildings.md` 랠리포인트 시스템 규칙 2 (깃발 표시/숨김 규칙)

| 상황 | 규칙상 동작 | 현재 | 수정 후 기대 |
|------|------------|------|-------------|
| 랠리포인트 설정 직후 | 3초간 표시 → 자동 숨김 | 잔존 오버레이의 `Close()` → `OnBeforeClose()` (:338-342)의 `HideAllRallyMarkers()`가 즉시 실행되어 3초 표시가 소멸 (**규칙 위반**) | 오버레이가 이미 내려가 있어 `Close()`가 호출되지 않으므로 3초 표시가 정상 유지 (**규칙 준수 회복**) |

즉 이번 수정은 UI 규칙 준수뿐 아니라, 현재 위반 상태인 건물 규칙 2의 "설정 직후 3초 표시"도 함께 정상화한다.

---

## 3. 수정 파일 및 구체적 변경 내용

### 수정 대상 파일 — 단 1개

| 파일 | 레이어 | 변경 규모 |
|------|--------|----------|
| `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | Presentation | 2개 지점에 각 1줄 추가 (+ 주석) |

그 외 파일은 변경하지 않는다. `UIManager.cs`, `BuildingPanelBase.cs`, `InputHandler.cs`, `ProductionTicker.cs` 모두 **무변경**이다.

---

### 수정 ①  `OnRallyPointClick()` — 조준 모드 진입 시 오버레이 해제

**위치**: `ProductionPanelUI.cs:534`
**근거**: 2-1 요구사항 **A** (`GameSystemRules_UI.md` 규칙 5 — 오버레이 단일 소유 패턴 / Popup 모드)
**목적**: 사용자가 보고한 증상(반투명 배경 잔존 + 조준 터치 가로채기)의 **직접 수정**

현재 (한 줄 메서드):

```csharp
private void OnRallyPointClick() { IsSettingRallyPoint = true; RallyPointSetFrame = Time.frameCount; _popup?.Hide(); }
```

변경 후 (가독성과 주석을 위해 블록 형태로 전개):

```csharp
private void OnRallyPointClick()
{
    IsSettingRallyPoint = true;
    RallyPointSetFrame = Time.frameCount;

    // 조준 중에는 맵이 보여야 하므로 팝업을 숨긴다.
    _popup?.Hide();

    // 공유 BlockingOverlay(패널이 열릴 때 BuildingPanelBase.Show()에서 표시됨)는
    //   "탭하면 Close()" 콜백을 가진 Popup 모드다.
    //   이 오버레이를 그대로 두면 조준하려고 맵을 탭했을 때 오버레이가 터치를 먼저 먹어
    //   Close()가 실행되고, OnBeforeClose()에서 IsSettingRallyPoint가 false로 초기화되며
    //   랠리 마커까지 숨겨져 "지정이 취소된 것처럼" 보인다.
    //   따라서 조준 모드 진입 시 오버레이를 내려 터치가 맵으로 전달되게 한다.
    //   (BuildingSkillPanelUI의 지점 조준 진입부와 동일한 패턴)
    UIManager.Instance?.HideBlockingOverlay();
}
```

**한 줄 요약**: `_popup?.Hide();` 뒤에 `UIManager.Instance?.HideBlockingOverlay();` 1줄 추가.
메서드를 블록으로 전개하는 것은 주석 삽입과 가독성을 위한 형태 변경일 뿐이며, **기존 3개 동작(`IsSettingRallyPoint` / `RallyPointSetFrame` / `_popup.Hide()`)은 순서·내용 모두 그대로 유지**한다.

**동일 패턴 선례 (검증된 코드)** — `Assets/_Project/Scripts/Presentation/UI/BuildingSkillPanelUI.cs:321-328`:

```csharp
// 지점 지정 — 조준 모드 진입. 조준 중 맵이 보이도록 팝업은 숨긴다(랠리 패턴).
_popup?.Hide();

// 공유 BlockingOverlay(패널 열릴 때 표시됨)는 "탭하면 Close" 콜백을 갖는다.
//   조준 중 맵을 드래그/탭하면 이 오버레이가 먼저 먹어 패널을 닫아버리므로,
//   조준 진입 시 오버레이를 숨겨 조준 입력이 맵으로 전달되게 한다
UIManager.Instance?.HideBlockingOverlay();
```

> 이 주석에는 이미 **"랠리 패턴"**이라고 적혀 있다. 즉 스킬 패널은 랠리 경로를 참고해 만들어졌으나,
> 정작 랠리 경로에는 오버레이 해제가 빠져 있는 상태다. 이번 수정으로 두 경로의 구현이 실제로 일치하게 된다.

---

### 수정 ②  `CompleteRallyPointSetting()` — 참조 카운터 반납

**위치**: `ProductionPanelUI.cs:536-545`
**근거**: 2-1 요구사항 **B** (`GameSystemRules_UI.md` 규칙 5 — 참조 카운터로 누적 관리, 카운터가 0이 될 때만 실제 숨김)
**목적**: `ShowBlockingOverlay` +1 에 대응하는 `HideBlockingOverlay` −1 짝을 **랠리 완료 경로에 신설** (Research 3-3 항목)

현재:

```csharp
public void CompleteRallyPointSetting(HexCoord target)
{
    if (_currentBuilding == null || _production == null) return;
    // 멀티플레이 → 서버에도 위임. NetworkContext + 래퍼 메서드로 NGO 직접 의존 제거.
    if (_networkProductionController != null && NetworkContext.IsNetworkActive)
        _networkProductionController.RequestSetRallyPoint(_currentBuilding.Id, target.Q, target.R, _currentBuilding.Team);
    _production.SetRallyPoint(_currentBuilding.Id, target);
    IsSettingRallyPoint = false;
    _currentBuilding = null;
}
```

변경 후:

```csharp
public void CompleteRallyPointSetting(HexCoord target)
{
    if (_currentBuilding == null || _production == null) return;
    // 멀티플레이 → 서버에도 위임. NetworkContext + 래퍼 메서드로 NGO 직접 의존 제거.
    if (_networkProductionController != null && NetworkContext.IsNetworkActive)
        _networkProductionController.RequestSetRallyPoint(_currentBuilding.Id, target.Q, target.R, _currentBuilding.Team);
    _production.SetRallyPoint(_currentBuilding.Id, target);
    IsSettingRallyPoint = false;

    // 이 경로는 Close()를 거치지 않으므로, Close()가 대신 해 주던
    //   BlockingOverlay 참조 카운터 반납을 여기서 직접 수행한다.
    //   (패널 표시 시 BuildingPanelBase.Show()가 ShowBlockingOverlay로 +1 해 둔 몫)
    //   조준 진입 시 이미 1회 내렸으므로 보통은 카운터가 0인 상태에서 호출되지만,
    //   UIManager.HideBlockingOverlay()는 0 미만으로 내려가지 않는 가드를 갖고 있어 안전하다.
    UIManager.Instance?.HideBlockingOverlay();

    _currentBuilding = null;
}
```

**한 줄 요약**: `IsSettingRallyPoint = false;` 와 `_currentBuilding = null;` 사이에 `UIManager.Instance?.HideBlockingOverlay();` 1줄 추가.
기존 코드 라인은 하나도 삭제·변경되지 않는다.

**왜 이 수정이 필수인가 (근거 코드)**
`BuildingPanelBase.Close()` (:234-252)는 :245에서 `UIManager.Instance?.HideBlockingOverlay();`를 호출하여 카운터를 반납한다.
스킬 패널은 조준 확정/취소 시 `OnAimConfirm` / `OnAimCancel`에서 모두 `Close()`를 호출하므로(`BuildingSkillPanelUI.cs:355-367`) 이 반납이 자동으로 이루어진다.
반면 랠리 완료 경로는 `Close()`를 호출하지 않고 `_currentBuilding = null`만 수행하므로, **반납 지점이 존재하지 않는다.**
따라서 스킬 패널에서 `Close()`가 담당하던 역할을 이 메서드가 직접 수행해야 한다.

---

### 수정 ①·② 적용 후의 참조 카운터 흐름 (검증)

| 시점 | 호출 | 카운터 | 오버레이 상태 |
|------|------|--------|--------------|
| 배럭 탭 → 팝업 표시 | `BuildingPanelBase.Show()` :207 `ShowBlockingOverlay(Close)` | 0 → **1** | 표시 (Popup 모드) |
| 랠리 버튼 탭 | **[수정 ①]** `HideBlockingOverlay()` | 1 → **0** | **숨김** (터치가 맵으로 전달됨) |
| 맵 탭 → 랠리 확정 | **[수정 ②]** `HideBlockingOverlay()` | 0 → **0** (가드) | 숨김 유지 (변화 없음) |
| 이후 배럭 재탭 | `ShowBlockingOverlay(Close)` | 0 → **1** | 정상 재표시 |

수정 ②가 "0에서 한 번 더 빼는" 형태가 되는 것은 의도된 이중 안전장치이며, 아래 `UIManager.HideBlockingOverlay()` 가드에 의해 안전하다 (`UIManager.cs:307-321`):

```csharp
// 카운터 언더플로 방지 — 중복 Hide 호출에도 안전하게 동작.
if (_blockingOverlayRefCount > 0)
    _blockingOverlayRefCount--;

// 아직 오버레이를 사용 중인 팝업이 남아 있으면 숨기지 않는다.
if (_blockingOverlayRefCount > 0) return;
```

카운터가 이미 0이면 감소하지 않고(음수 진입 불가), 이어지는 리스너 제거 + `ApplyBlockingOverlayVisibility(false)`는 **이미 숨김 상태에 대한 멱등(idempotent) 재적용**이라 부작용이 없다.
이는 스킬 패널이 이미 사용 중인 구조와 동일하다 (조준 진입 시 1회 + `Close()` 경유 시 1회 = 총 2회 호출).

---

## 4. 아키텍처 제약 검토

| 항목 | 판정 |
|------|------|
| 변경 레이어 | **Presentation 단독.** Domain / Application / Core / Infrastructure / Bootstrap 무변경 |
| 레이어 의존 방향 규칙 | 신규 의존 추가 없음. `UIManager`는 동일 Presentation 레이어이며 `ProductionPanelUI`가 이미 상속 체인(`BuildingPanelBase`)을 통해 사용 중인 대상 → **제약 위반 없음** |
| 서버 권위 로직 | `RequestSetRallyPoint` RPC 및 `_production.SetRallyPoint(...)` 호출부 **무변경**. 순수 클라이언트 UI 표시 처리만 추가 |
| 싱글/멀티 분기 | 추가 코드는 `NetworkContext.IsNetworkActive` 분기 **바깥**에 위치 → 싱글·멀티 양쪽에 동일 적용 (Presentation은 클라이언트 로컬 실행) |
| 신규 파일 / 신규 필드 / 신규 인터페이스 | 없음 |
| 프리팹·씬·Inspector 작업 | **없음.** 코드 수정만으로 완결되므로 WORKFLOW [5-2] Editor 스크립트 단계 불필요 |

---

## 5. 위험 요소

### 5-1. 전체 위험도 — **낮음**

근거:
- **순수 추가 수정.** 기존 로직의 제거·변경이 전혀 없어 기존 동작을 되돌릴 위험이 없다.
- **검증된 패턴 재사용.** 동일 코드베이스의 `BuildingSkillPanelUI`가 이미 같은 방식으로 동작 중이다.
- **영향 파일 1개, 추가 라인 2줄.** 다른 패널(`BuildingPlacementUI`, `InGameSettingsUI`, 각종 Modal 팝업)은 코드가 전혀 변경되지 않으므로 회귀 영향이 없다.

### 5-2. 개별 위험 항목

| # | 위험 | 평가 및 대응 |
|---|------|-------------|
| R1 | 수정 ①과 수정 ②가 겹쳐 `HideBlockingOverlay()`가 이중 호출된다 | **안전.** `UIManager.cs:310-314`의 언더플로 가드로 카운터가 음수로 내려가지 않으며, 숨김 재적용은 멱등이다. 스킬 패널이 이미 동일한 이중 호출 구조로 동작 중이다. |
| R2 | 오버레이가 사라지면서 조준 중 맵/UI 입력이 무방비로 열린다 | **의도된 동작이자 스킬 패널과 동일.** 조준 중 오작동 방지는 `InputHandler.HandleClick` :205-216의 랠리 분기(`IsSettingRallyPoint` + `RallyPointSetFrame` 동일 프레임 가드)가 담당하며, 이 로직은 **무변경**이다. |
| R3 | 조준 도중 다른 팝업(Modal 등)이 열려 카운터가 꼬인다 | 조준 중 오버레이 카운터는 0이므로, 이후 열리는 팝업의 Show/Hide 짝이 독립적으로 0↔1을 오간다. 카운터 오염 경로 없음. |
| R4 | 랠리 조준 중 해당 배럭이 파괴되는 경우 | `BuildingPanelBase.OnBuildingDied` (:380-390)는 `_currentBuilding`이 살아 있을 때 `Close()`를 호출한다. 조준 중에는 `_currentBuilding`이 아직 null이 아니므로 `Close()`가 실행되어 `OnBeforeClose()`(`IsSettingRallyPoint=false` + 마커 숨김)까지 정상 수행되고, `Close()` :245의 `HideBlockingOverlay()`는 R1과 동일한 가드로 안전하다. 랠리 완료 후에는 `_currentBuilding`이 null이라 `Close()`가 호출되지 않으나, 이 시점에는 수정 ②로 이미 카운터가 반납된 상태다. **→ 실기 확인 대상(5-3 참조).** |
| R5 | 수정 ①만 적용하고 수정 ②를 누락하는 경우 | **금지.** 그 경우 카운터가 반납되지 않아 다음 팝업의 오버레이 표시가 어긋난다. Research 3-3에 기록된 대로 현재는 버그가 누락을 상쇄하고 있으므로 **두 수정은 반드시 함께 적용**해야 한다. |

### 5-3. 실기 확인이 필요한 항목

> 아래는 Plan 단계의 확인 필요 목록이다. 실제 TC 문서(`Testcase.md`) 작성 및 QA는
> WORKFLOW.md [5-1]/[5-3]에 따라 **사용자가 명시적으로 지시한 경우에만** 진행한다.

1. 배럭 1개 상태에서 랠리포인트 버튼 탭 → 반투명 배경이 **즉시** 사라지는가.
2. 이어서 맵을 탭했을 때 집결지가 정상 지정되고 깃발이 나타나는가 (건물 규칙 2의 **3초 표시**가 유지되는가).
3. 랠리 지정 완료 후 **같은 배럭을 다시 탭**했을 때 반투명 배경이 정상적으로 다시 나타나는가 (참조 카운터 정합성 — 수정 ②의 핵심 검증).
4. 위 3번을 여러 번 반복해도 오버레이 표시/숨김이 계속 정상인가 (카운터 누적 오염 없음 확인).
5. 랠리 조준 중 해당 배럭이 파괴되는 경우 상태가 정상 정리되는가 (R4 경로).
6. 싱글플레이 / 멀티플레이 양쪽에서 동일하게 동작하는가.

---

## 6. 이번 범위가 아닌 것

Research.md **5장 "작업 중 발견한 부가 이슈"**에 기록된 아래 항목들은 **이번 Plan 및 구현 대상이 아니다.**
모두 별개의 결함 후보이며, 필요 시 사용자 승인 후 **별도 작업 사이클**로 진행한다.

| # | 내용 | 관련 위치 | 코드 재확인 여부 |
|---|------|----------|----------------|
| 1 | 자동 숨김 코루틴이 배럭별이 아닌 단일 필드(`_autoHideCoroutine`)라, 다중 배럭 또는 AI 랠리 설정이 겹칠 때 깃발이 영구 표시될 수 있음 | `ProductionTicker.cs:86` | 해당 라인 존재 확인됨 |
| 2 | 상대팀 깃발 필터가 `NetworkContext.IsNetworkActive` 조건 안에만 있어, 싱글플레이(AI 상대)에서 팀 필터 미작동 → 랠리포인트 규칙 1 위반 가능 | `ProductionTicker.cs:341` | 해당 라인 존재 확인됨 |
| 3 | 재경기(같은 씬 LoadMap 재호출) 시 이벤트 중복 구독 및 마커 GameObject 누수 가능성 | ProductionTicker / 마커 생성 경로 | 미검증 (이전 조사 기록) |
| 4 | 랠리 좌표에 대한 유효성 검증(맵 경계 등) 부재 | `ProductionPanelUI.CompleteRallyPointSetting` / `InputHandler` 랠리 분기 | 미검증 (이전 조사 기록) |
| 5 | 멀티플레이에서 클라이언트 낙관적 적용 후 서버가 거부했을 때 정정(롤백) 경로 없음 | `ProductionPanelUI.cs:540-542` | 미검증 (이전 조사 기록) |
| 6 | 건물 업그레이드 시 클라이언트에서도 서버 가드 없이 생산 큐 취소 로직이 실행됨 | `ProductionPanelUI` 업그레이드 처리 경로 | 미검증 (이전 조사 기록) |

추가로 이번 범위에서 제외하는 사항:

- **`ProductionPanelUI` 외 다른 패널의 오버레이 처리 수정** — Research 4-4의 전수 확인 결과 나머지 호출처는 모두 Show/Hide 짝이 유지되고 있어 수정 대상이 아니다.
- **`UIManager` / `BuildingPanelBase` / `InputHandler`의 구조 개선** — 예: "오버레이 해제를 베이스 클래스가 자동 처리하도록 공통화"하는 리팩토링. 유효한 개선안이지만 요청 범위를 넘어서므로(CLAUDE.md 규칙 6) 이번 작업에 포함하지 않는다.
- **`Testcase.md` 등 다른 작업 문서 작성** — 이번 단계에서는 `Plan.md` 1개만 작성한다.

---

## 7. 구현 담당 및 진행 조건

| 항목 | 내용 |
|------|------|
| 구현 담당 | **game-programmer 에이전트** (CLAUDE.md 규칙 3 — 코드 구현은 반드시 전문 에이전트에게 위임) |
| 위임 시 전달 컨텍스트 | `.claude/MEMORY.md` 내용, 본 `Plan.md`, 선행 `Research.md` (CLAUDE.md 작업 시작 전 체크리스트 [3]) |
| 구현 범위 | 본 문서 3장의 **수정 ①·② 두 지점만.** 그 외 어떤 파일·로직도 변경하지 않는다 |

> ### 구현 시작 조건 (WORKFLOW.md [4])
> **본 Plan.md는 계획 문서이며, 이 문서 작성만으로는 어떤 코드도 수정되지 않는다.**
> 실제 구현은 **사용자가 이 Plan 내용을 확인하고 명시적으로 승인한 뒤에만** 시작한다.
> 사용자 승인 없이 game-programmer 호출 또는 코드 Edit/Write를 진행하는 것은 규칙 위반이다 (CLAUDE.md 규칙 11).

---

## 8. 요약

| 항목 | 내용 |
|------|------|
| 원인 | `ProductionPanelUI.OnRallyPointClick()` (:534)이 `_popup?.Hide()`만 호출하고, `BuildingPanelBase.Show()` (:207)가 켠 공유 BlockingOverlay를 내리지 않음 |
| 부수 결함 (범위 포함) | `CompleteRallyPointSetting()` (:536-545)에 참조 카운터 반납(`HideBlockingOverlay()`) 지점이 없음 |
| 수정 | `ProductionPanelUI.cs` 2개 지점에 `UIManager.Instance?.HideBlockingOverlay();` 각 1줄 추가 (기존 로직 제거 없음) |
| 근거 규칙 | `GameSystemRules_UI.md` 공통 UI 규칙 5 — 반투명 배경 오버레이 단일 소유 패턴 / 참조 카운터 (보조: `GameSystemRules_Buildings.md` 랠리포인트 규칙 2) |
| 선례 | `BuildingSkillPanelUI.cs:321-328`(조준 진입 시 해제) + `:355-367`(`Close()` 경유 반납) — 동일 패턴 이미 검증됨 |
| 영향 파일 | `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` 1개 |
| 영향 레이어 | Presentation 한정 / 서버 권위 로직 무변경 / 싱글·멀티 공통 |
| 위험도 | 낮음 (순수 추가 + 검증된 패턴 재사용) |
| Inspector 작업 | 없음 |
