# Research — 랠리포인트 조준 시 반투명 오버레이 잔존 버그

작성일: 2026-08-08
작업 폴더: `Assets/_Project/Docs/_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/`

---

## 1. 이 문서가 무엇을 왜 조사하는가 (자연어 설명)

### 사용자가 겪는 현상

배럭(생산 건물)을 눌러 팝업을 연 뒤 **랠리포인트 버튼**을 누르면, 팝업 창은 사라지지만
**화면 전체를 덮고 있던 반투명한 어두운 배경이 그대로 남아 있습니다.**

이 상태에서 유닛을 보낼 지점을 지정하려고 지도를 손가락으로 누르면,
**집결지가 지정되는 대신 방금 시작한 지정 작업이 그냥 취소된 것처럼 보입니다.**
깃발도 나타나지 않고, 아무 일도 일어나지 않은 것처럼 느껴집니다.

### 왜 이런 일이 생기는가 (쉬운 말로)

이 게임에는 팝업 창이 열릴 때 화면 전체에 깔리는 "반투명 가림막"이 있습니다.
이 가림막은 두 가지 역할을 합니다.

1. 팝업 뒤쪽을 어둡게 만들어 팝업에 시선을 집중시킨다.
2. **가림막을 누르면 팝업이 닫히도록 한다** (팝업 바깥을 눌러 닫는 흔한 방식).

문제는, 랠리포인트 버튼을 눌렀을 때 **팝업 창만 사라지게 만들고 이 가림막은 치우지 않았다**는 점입니다.
가림막은 눈에 보이지 않는 유리창처럼 화면 전체에 남아 있고,
"누르면 팝업을 닫는다"는 성질도 그대로 갖고 있습니다.

그래서 사용자가 지도를 누르려고 화면을 터치하면,
그 터치는 지도가 아니라 **남아 있던 가림막이 먼저 받아버립니다.**
가림막은 자기 역할대로 "팝업 닫기"를 실행하고,
팝업을 닫는 과정에서 랠리포인트 지정 상태와 깃발 표시가 모두 초기화됩니다.
사용자 입장에서는 "집결지를 찍으려 했더니 그냥 취소됐다"로 보이는 것입니다.

### 이미 같은 문제를 겪고 해결한 선례가 있다

같은 팝업 계열인 **건물 스킬 패널**은 스킬 목표 지점을 조준할 때 정확히 이 문제를 만났고,
"팝업을 숨길 때 가림막도 반드시 같이 치워야 한다"는 처리를 코드에 넣어두었습니다.
심지어 그 이유를 코드 주석으로 남겨두었습니다.

즉 **이 프로젝트는 이미 이 문제를 알고 있었고, 스킬 쪽만 고쳐져 있고 랠리포인트 쪽은 빠져 있는 상태**입니다.

### 이 조사의 범위

이 문서는 **위 하나의 버그**(랠리포인트 조준 진입 시 반투명 오버레이가 화면에 남는 문제)에 대한
코드 현황·재현 경로·영향 범위 파악만 다룹니다.
어떻게 고칠지에 대한 내용은 이 문서에 담지 않으며, 이후 별도의 Plan.md에서 다룹니다.

---

## 2. 관련 코드 구조

모두 **Presentation 레이어**에 속하며, 도메인/서버 권위 로직과는 무관합니다.

### 2-1. 오버레이 소유·관리 주체

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/Core/IUIManager.cs` | `ShowBlockingOverlay(Action onTap = null)` / `HideBlockingOverlay()` 계약 정의 (:73, :79) |
| `Assets/_Project/Scripts/Presentation/UI/UIManager.cs` | 반투명 오버레이 단일 소유 및 실제 표시/숨김 구현 |

`UIManager`의 오버레이 관리 방식 (실제 코드 확인 결과):

- `_blockingOverlay` (CanvasGroup, :73) + `_blockingOverlayButton` (Button, :77) 2개 필드로 구성.
- `_blockingOverlayRefCount` (:90) 참조 카운터 기반. `ShowBlockingOverlay` 1회 = +1, `HideBlockingOverlay` 1회 = -1.
- `ShowBlockingOverlay(onTap)` (:280-297)
  - 카운터 +1
  - `_blockingOverlayButton.onClick.RemoveAllListeners()` 후, `onTap != null`이면 해당 콜백을 리스너로 등록
  - `onTap != null` → **Popup 모드**(탭하면 콜백 실행) / `onTap == null` → **Modal 모드**(입력 차단만)
  - 항상 `ApplyBlockingOverlayVisibility(true)` 호출
- `HideBlockingOverlay()` (:307-321)
  - 카운터 -1 (0 미만 방지 가드 있음)
  - 카운터가 여전히 0보다 크면 **숨기지 않고 즉시 return** (:314)
  - 0이 되었을 때만 리스너 제거 + 숨김
- `ApplyBlockingOverlayVisibility(bool)` (:349-356)
  - `alpha`, `blocksRaycasts`, `interactable`을 동시에 설정. `SetActive` 미사용.
  - → **숨김 상태(alpha=0)에서는 raycast도 통과**하므로, 오버레이가 "보이는데 입력만 안 먹는" 상태는 없다. 보이면 곧 입력을 가로챈다.

### 2-2. 건물 팝업 공통 베이스

`Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs`

| 위치 | 코드 | 의미 |
|------|------|------|
| `Show(BuildingData)` :194-218 | :203 `_popup?.Show();` / :207 `UIManager.Instance?.ShowBlockingOverlay(Close);` | 팝업 표시와 동시에 오버레이를 **Popup 모드**로 켠다. 탭 콜백은 자기 자신의 `Close`. |
| `Close()` :234-252 | :237 `OnBeforeClose();` / :241 `ClosedFrame = Time.frameCount;` / :245 `UIManager.Instance?.HideBlockingOverlay();` / :248 `_popup?.Hide();` / :251 `_currentBuilding = null;` | **오버레이가 해제되는 유일한 경로.** |

핵심: **오버레이를 켜는 곳은 `Show()`, 끄는 곳은 `Close()` 단 한 곳**이다.
`_popup.Hide()`를 직접 호출하는 경로는 오버레이를 건드리지 않는다.

### 2-3. 생산 패널 (랠리포인트 진입 지점)

`Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` — `BuildingPanelBase` 상속

| 위치 | 내용 |
|------|------|
| :196 | `[SerializeField] private Button _rallyPointButton;` |
| :239-240 | `public bool IsSettingRallyPoint { get; private set; }` / `public int RallyPointSetFrame { get; private set; }` — 조준 상태를 InputHandler에 노출 |
| :296 | `_rallyPointButton.onClick.AddListener(OnRallyPointClick);` |
| `OnShow(building)` :318-332 | `IsSettingRallyPoint = false;` (:320), `_ticker.ShowRallyMarker(building.Id)` (:322) 등 |
| `OnBeforeClose()` :338-342 | `IsSettingRallyPoint = false;` (:340), `_ticker.HideAllRallyMarkers();` (:341) |
| `OnRallyPointClick()` :534 | `{ IsSettingRallyPoint = true; RallyPointSetFrame = Time.frameCount; _popup?.Hide(); }` — **한 줄. `HideBlockingOverlay()` 호출 없음.** |
| `CompleteRallyPointSetting(HexCoord)` :536-545 | 멀티면 `RequestSetRallyPoint` 송신 → `_production.SetRallyPoint(...)` → `IsSettingRallyPoint = false;` → `_currentBuilding = null;` — **`Close()`도 `HideBlockingOverlay()`도 호출하지 않음.** |

### 2-4. 입력 처리

`Assets/_Project/Scripts/Presentation/Input/InputHandler.cs`

`HandleClick(Vector2 screenPos)` :193-233 의 판정 순서:

1. :202-203 — `SkillAimController.IsAiming`이면 즉시 return (스킬 조준 중 입력 억제)
2. :205-216 — **랠리포인트 분기.** `_productionUI.IsSettingRallyPoint == true` 이고 `Time.frameCount != RallyPointSetFrame`이면
   화면 좌표를 헥스 좌표로 변환해 `_productionUI.CompleteRallyPointSetting(rallyCoord)` 호출 후 return
3. :224-225 — `IsPointerOverUI(screenPos)`면 return (UI 위 클릭 무시)
4. :230-233 — `ClosedFrame == 현재 프레임`인 패널이 있으면 return (같은 프레임 닫힘 클릭 차단)
5. 이후 타일/건물 클릭 처리

주목할 점: **랠리 분기(2)가 UI 히트 판정(3)보다 앞에 있다.** 주석(:196-198)에도
"팝업이 닫힌 상태에서 타일을 선택해야 하므로 IsPointerOverUI보다 먼저 처리해야 함"이라 명시되어 있다.
즉 InputHandler는 "랠리 조준 중에는 UI 위를 눌러도 맵 좌표로 처리한다"는 전제로 작성되어 있다.
그러나 이 전제는 **Unity EventSystem이 같은 터치를 오버레이 버튼 클릭으로도 처리하는 것까지는 막지 못한다.**

### 2-5. 마커(깃발) 표시 주체

`Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs`

| 위치 | 내용 |
|------|------|
| :83 | `Dictionary<int, GameObject> _rallyMarkers` — 배럭 Id별 마커 |
| :86 | `private Coroutine _autoHideCoroutine;` — **배럭별이 아닌 단일 필드** |
| `OnRallyPointChanged` :334-359 | 좌표가 있으면 `CreateOrMoveMarker` + `ShowMarkerTemporary`(3초 표시) |
| `ShowMarkerTemporary` :487-497 | 자동 숨김 코루틴 재시작 후 마커 활성화 |
| `ShowRallyMarker(int)` :515-526 | 자동 숨김 코루틴 취소 + 해당 마커 활성화 (팝업 열림 시) |
| `HideAllRallyMarkers()` :531-545 | 자동 숨김 코루틴 취소 + **모든 마커 비활성화** (팝업 닫힘 시) |

### 2-6. 동일 패턴을 올바르게 처리하는 선례 — 스킬 패널

`Assets/_Project/Scripts/Presentation/UI/BuildingSkillPanelUI.cs` — 역시 `BuildingPanelBase` 상속

`OnSkillPointerDown(int slotIndex)` :300-350 중 지점 지정 스킬 분기 (:319-328):

```
_popup?.Hide();
// 공유 BlockingOverlay(패널 열릴 때 표시됨)는 "탭하면 Close" 콜백을 갖는다.
//   조준 중 맵을 드래그/탭하면 이 오버레이가 먼저 먹어 패널을 닫아버리므로,
//   조준 진입 시 오버레이를 숨겨 조준 입력이 맵으로 전달되게 한다
UIManager.Instance?.HideBlockingOverlay();
```

- 스킬 패널은 `_popup.Hide()`와 `HideBlockingOverlay()`를 **반드시 짝으로** 호출한다.
- 주석에 "오버레이가 먼저 먹어 패널을 닫아버린다"는 **본 버그와 동일한 증상**이 명시되어 있다.
- 반면 `ProductionPanelUI.OnRallyPointClick()` (:534)은 `_popup?.Hide()`만 호출한다.

→ 프로젝트 내부에 이미 정답 패턴이 존재하며, 랠리포인트 경로에만 적용이 누락된 상태.

---

## 3. 현재 동작(버그) 분석

### 3-1. 단계별 재현 경로

| 단계 | 사용자 조작 | 코드 동작 | 결과 상태 |
|------|-----------|----------|----------|
| 1 | 자기 팀 배럭 탭 | `InputHandler.HandleClick` → `ProductionPanelUI.Show(building)` → `BuildingPanelBase.Show` :203 `_popup.Show()`, :207 `ShowBlockingOverlay(Close)` | 팝업 표시. 오버레이 **표시(refCount=1)**, onClick = `Close` |
| 2 | 랠리포인트 버튼 탭 | `OnRallyPointClick()` :534 → `IsSettingRallyPoint = true`, `RallyPointSetFrame = 현재 프레임`, `_popup.Hide()` | 팝업만 사라짐. **오버레이는 refCount=1 그대로 → 화면에 반투명 배경 잔존.** onClick도 `Close` 그대로 |
| 3 | 지도 위 목표 지점 탭 | 같은 터치가 두 경로로 동시에 흘러감:<br>(a) Unity EventSystem → 오버레이 Button onClick → `BuildingPanelBase.Close()`<br>(b) `InputHandler.HandleClick` :205 랠리 분기 → `CompleteRallyPointSetting(...)` | 두 경로의 실행 순서에 따라 결과가 갈림 (아래 3-2) |

**단계 2가 사용자가 목격한 "남아 있는 반투명 백그라운드"의 정체**이며,
`ApplyBlockingOverlayVisibility(true)`에 의해 `blocksRaycasts = true` 상태이므로 시각적 잔존이 곧 입력 가로채기로 이어진다.

### 3-2. 단계 3에서 갈리는 두 경우

`InputHandler`(Update에서 Input System 폴링)와 Unity EventSystem(자체 Update에서 포인터 이벤트 처리) 중
어느 쪽이 먼저 실행되는지는 코드로 강제되어 있지 않다.
확인 결과 `InputHandler.cs`에는 `DefaultExecutionOrder` 특성이 없고, `UIManager.cs.meta`의 `executionOrder`는 `0`이다.
따라서 순서가 보장되지 않으며, 두 경우 모두 사용자에게는 **"취소된 것처럼"** 보인다.

**경우 A — InputHandler가 먼저 처리되는 경우**

1. `CompleteRallyPointSetting(target)` (:536-545) 실행 → `SetRallyPoint` 호출됨 → **랠리포인트 자체는 실제로 설정됨**
2. `IsSettingRallyPoint = false`, `_currentBuilding = null`
3. `RallyPointChangedEvent` → `ProductionTicker.OnRallyPointChanged` (:334) → `CreateOrMoveMarker` + `ShowMarkerTemporary`(3초 표시 시작)
4. 이어서 EventSystem이 오버레이 onClick 실행 → `BuildingPanelBase.Close()` → `OnBeforeClose()` → `HideAllRallyMarkers()` (:341)
5. → **깃발이 즉시 숨겨져 화면에 나타나지 않음.** 사용자에게는 "설정이 안 됐다/취소됐다"로 보임

**경우 B — EventSystem(오버레이 onClick)이 먼저 처리되는 경우**

1. `BuildingPanelBase.Close()` → `OnBeforeClose()`에서 `IsSettingRallyPoint = false` (:340) + `HideAllRallyMarkers()` (:341)
2. `ClosedFrame = 현재 프레임`, `HideBlockingOverlay()`(refCount 1→0, 오버레이 숨김), `_currentBuilding = null`
3. 이후 `InputHandler.HandleClick` 실행 → :205 랠리 분기 조건 `IsSettingRallyPoint`가 이미 false → 분기 미진입
4. :230-233 `ClosedFrame == 현재 프레임` 가드에 걸려 return
5. → **랠리포인트가 아예 설정되지 않음.** 사용자에게는 동일하게 "취소됐다"로 보임

두 경우 모두 근본 원인은 동일하다: **단계 2에서 오버레이를 내리지 않았다.**

### 3-3. 부수적으로 확인된 상태 불일치 (동일 버그의 일부)

`CompleteRallyPointSetting()` (:536-545)은 `Close()`를 거치지 않고
`_currentBuilding = null` (:544)만 수행한다. 즉 이 경로 자체에도 `HideBlockingOverlay()` 호출이 없다.

- 결과적으로 **오버레이 참조 카운터를 정상 반납하는 경로가 랠리 완료 흐름에는 존재하지 않는다.**
- 경우 A에서 오버레이가 결국 사라지는 이유는, 오버레이 onClick의 `Close()`가 뒤이어 실행되며 카운터를 반납해 주기 때문이다.
  즉 현재는 **버그(오버레이 잔존)가 또 다른 누락(카운터 미반납)을 우연히 상쇄하고 있는 구조**다.
- `Close()` 시점에 `_currentBuilding`이 이미 null이므로 `UpdateDemolishRefund` 등은 자체 null 가드(:271)로 무해하게 넘어가지만,
  패널이 "팝업은 숨겨졌고 `_currentBuilding`은 null인" 반쯤 닫힌 상태를 거치는 것은 사실이다.

이 항목은 별개의 버그가 아니라 **본 버그의 원인 구조를 이루는 같은 결함의 다른 면**이므로 여기에 기록한다.

### 3-4. 기획 규칙과의 대조

`Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` — 랠리포인트 시스템

| 규칙 | 내용 | 현재 상태 |
|------|------|----------|
| 규칙 1 | 각 플레이어는 자신이 설정한 랠리포인트 깃발만 볼 수 있다 | 본 버그와 직접 관련 없음 (아래 부가 이슈 참조) |
| 규칙 2 — "랠리포인트 설정 직후 3초간 표시 → 자동 숨김" | 설정 직후 3초 노출 | **위반.** 경우 A에서 `HideAllRallyMarkers()`가 즉시 실행되어 3초 표시가 소멸 |
| 규칙 2 — "팝업 닫힘 → 모든 마커 숨김" | 팝업 닫힘 시 숨김 | 규칙 자체는 정상 구현되어 있으나, **랠리 조준 중인 터치가 "팝업 닫힘"으로 오인**되어 잘못된 타이밍에 발동 |

---

## 4. 영향 범위

### 4-1. 기능 영향

- **랠리포인트 기능이 사실상 동작하지 않는다.** 경우 A/B 어느 쪽이든 사용자는 집결지를 지정할 수 없거나, 지정 사실을 확인할 수 없다.
- 랠리포인트를 지정하지 못하면 생산 유닛의 집결 지점 제어가 불가능하므로, 생산 건물 운용 전반의 조작성에 영향을 준다.

### 4-2. 코드 영향 범위 (레이어)

- **Presentation 레이어 한정.** Domain / Application / Infrastructure 로직은 관여하지 않는다.
- `CompleteRallyPointSetting`이 실제로 도달하는 경우(경우 A)에는 `_production.SetRallyPoint(...)` 및
  멀티 시 `RequestSetRallyPoint` 서버 요청까지 정상 수행된다. 즉 **서버 권위 로직에는 결함이 없다.**

### 4-3. 플레이 모드 영향

- **싱글플레이 / 멀티플레이 공통.** 원인이 UI 오버레이 처리이므로 `NetworkContext.IsNetworkActive` 값과 무관하게 동일하게 발생한다.
- 멀티플레이에서는 경우 A일 때 서버로 `RequestSetRallyPoint`가 전송되므로, **실제 게임 상태와 화면 표시가 어긋난 채로 진행될 수 있다**(설정은 됐는데 깃발이 보이지 않음).

### 4-4. 다른 패널로의 파급 여부

`ShowBlockingOverlay` 호출처를 전수 확인한 결과는 다음과 같다.

| 파일 | 모드 | 조준 모드 진입 여부 | 본 버그 해당 |
|------|------|------------------|-------------|
| `BuildingPanelBase.cs` :207 | Popup(`Close`) | 자식에 따라 다름 | **해당 (ProductionPanelUI 랠리 경로)** |
| `BuildingSkillPanelUI.cs` :328 | — | 지점 조준 시 `HideBlockingOverlay()` 호출 | 비해당 (이미 처리됨) |
| `BuildingPlacementUI.cs` :292 / :446 | Popup(`Close`) | Show/Close 짝 유지 | 비해당 |
| `InGameSettingsUI.cs` :268 / :291 | Popup(`Hide`) | Show/Hide 짝 유지 | 비해당 |
| `ConfirmPopup.cs` :148 / :172 | Modal | — | 비해당 |
| `NicknameChangePopup.cs` :149 / :181 | Modal | — | 비해당 |
| `AnonymousWarningPopup.cs` :113 / :121 | Modal | — | 비해당 |
| `NetworkErrorPopup.cs` :93 / :101 | Modal | — | 비해당 |
| `RematchRequestPopup.cs` :270 / :280 | Modal (자체 점유 플래그로 중복 방지) | — | 비해당 |

→ **`BuildingPanelBase`를 상속하면서 팝업만 숨기고 조준 모드로 들어가는 경로는 스킬 패널과 생산 패널 랠리 경로 두 곳뿐이며, 그중 랠리 경로만 미처리 상태**이다.

### 4-5. 관련 문서

| 문서 | 관련성 |
|------|-------|
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` | 랠리포인트 시스템 규칙 1·2 — 현재 규칙 2가 위반되고 있음 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` | 팝업/오버레이 동작 규칙 (Plan 단계에서 대조 필요) |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_CanvasSortingOrder.md` | 오버레이 레이어 순서 (직접 원인은 아니나 참조 가치 있음) |

---

## 5. 작업 중 발견한 부가 이슈

> **이번 작업 범위 아님.** 아래 항목들은 이전 조사(game-programmer, 정적 분석·코드 미수정) 과정에서
> 함께 발견된 **별개의** 랠리포인트 관련 결함 후보이며, 이번 Research/Plan 대상은
> **BlockingOverlay 잔존 건 하나로 한정**한다. 필요 시 사용자 승인 후 별도 작업으로 진행한다.

| # | 내용 | 관련 위치 |
|---|------|----------|
| 1 | 자동 숨김 코루틴이 배럭별이 아닌 단일 필드(`_autoHideCoroutine`)라, 다중 배럭 또는 AI 랠리 설정이 겹칠 때 깃발이 영구 표시될 수 있음 | `ProductionTicker.cs:86` |
| 2 | 상대팀 깃발 필터가 `NetworkContext.IsNetworkActive` 조건 안에만 있어, 싱글플레이(AI 상대)에서는 팀 필터가 작동하지 않아 상대 깃발이 노출됨 (랠리포인트 규칙 1 위반 가능) | `ProductionTicker.cs:341` |
| 3 | 재경기(같은 씬 LoadMap 재호출) 시 이벤트 중복 구독 및 마커 GameObject 누수 가능성 | ProductionTicker / 마커 생성 경로 |
| 4 | 랠리 좌표에 대한 유효성 검증(맵 경계 등) 부재 | `ProductionPanelUI.CompleteRallyPointSetting` / `InputHandler` 랠리 분기 |
| 5 | 멀티플레이에서 클라이언트 낙관적 적용 후 서버가 거부했을 때 정정(롤백) 경로 없음 | `ProductionPanelUI.cs:540-542` |
| 6 | 건물 업그레이드 시 클라이언트에서도 서버 가드 없이 생산 큐 취소 로직이 실행됨 | `ProductionPanelUI` 업그레이드 처리 경로 |

위 6개 항목은 **본 문서 작성 과정에서 코드로 재확인한 범위는 #1, #2에 한정**되며(각각 해당 라인 존재 확인),
#3~#6은 이전 조사 보고 내용을 기록만 한 것으로 별도 검증이 필요하다.

---

## 6. 요약

- **원인**: `ProductionPanelUI.OnRallyPointClick()` (:534)이 `_popup?.Hide()`만 호출하고, `BuildingPanelBase.Show()` (:207)에서 켠 공유 BlockingOverlay를 내리지 않는다.
- **결과**: 화면에 반투명 오버레이가 `blocksRaycasts = true` 상태로 남아, 조준 터치를 가로채 오버레이의 onClick 콜백인 `Close()`를 실행시킨다. `Close()` → `OnBeforeClose()` (:338-342)에서 `IsSettingRallyPoint = false` + `HideAllRallyMarkers()`가 실행되어 랠리 조준이 취소되거나 깃발이 즉시 사라진다.
- **선례**: 동일 문제를 `BuildingSkillPanelUI.cs:319-328`이 이미 `_popup.Hide()` + `HideBlockingOverlay()` 짝 호출로 해결해 두었으며, 그 주석에 동일 증상이 명시되어 있다.
- **부수 결함**: `CompleteRallyPointSetting()` (:536-545)에도 오버레이 참조 카운터 반납 경로가 없다.
- **범위**: Presentation 레이어 한정, 싱글/멀티 공통, 서버 권위 로직 무결.

---

## 7. 후속 진행 상태 (2026-08-08)

본 조사에서 확인된 원인은 [Plan.md](Plan.md)의 수정 ①·②로 **구현 완료 · 사용자 실기 테스트 PASS**(커밋 `9a19cd5`).
`ProductionPanelUI.cs` 1개 파일에 `HideBlockingOverlay()` 호출 2줄을 추가한 것이 전부이며, 계획과 실제 구현이 완전히 일치한다.
상세 결과는 Plan.md 9장 참조.

**5장 "작업 중 발견한 부가 이슈" 6건은 이번 수정 범위 밖으로 여전히 미해결 상태**이며, 별도 작업 사이클의 후보로 남는다.
