# Research — 건물 파괴 시 열린 패널/조준 UI 원복

## 이 작업이 무엇인지 (자연어 설명)

플레이어가 어떤 건물을 클릭하면 그 건물의 **패널 UI**가 열립니다. 생산 건물이면 생산 패널, 스킬 건물이면 스킬 패널, 연구소면 연구 패널, 그 외 비생산 건물이면 간이 액션 패널이 열립니다. 스킬 건물의 경우 스킬 버튼을 누르면 패널이 잠시 닫히고 화면에 **조준 원(지점 조준 모드)**이 뜹니다.

문제는, **이렇게 패널이 열려 있는(또는 조준 중인) 상태에서 그 건물이 파괴되면** 화면에 죽은 건물의 패널·조준 UI가 그대로 남는다는 점입니다. 실제로 그 UI로 무언가를 하려고 해도(스킬 발동, 철거 등) 서버가 "그 건물은 이미 없다"라고 재검증으로 막아 게임 상태가 깨지지는 않습니다. 하지만 **UI만 원복(닫힘)되지 않아** 사용자에게는 유령 패널이 남는 시각적 버그가 됩니다.

이 작업의 목표는, **파괴된 건물이 지금 화면에 표시/조준 중인 바로 그 건물이면 패널을 닫고 조준을 취소해 평상시(맵) 화면으로 되돌리는 것**입니다. 이번 문서(Research)는 코드 현황과 근거, 영향 범위를 정리합니다. 실제 구현은 하지 않습니다.

---

## 대상 범위 — 공통 베이스 한 곳의 갭

건물 패널 4종은 모두 `BuildingPanelBase`를 상속한다. 실측 확인 결과:

| 패널 클래스 | 파일 | 선언 | 용도 |
|------------|------|------|------|
| `ProductionPanelUI` | `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | L35 `: BuildingPanelBase` | 생산 건물(배럭) |
| `BuildingActionPanelUI` | `Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs` | L34 `: BuildingPanelBase` | 비생산 건물 간이 액션 |
| `BuildingSkillPanelUI` | `Assets/_Project/Scripts/Presentation/UI/BuildingSkillPanelUI.cs` | L41 `: BuildingPanelBase` | 스킬 건물(조준 연동) |
| `ResearchPanelUI` | `Assets/_Project/Scripts/Presentation/UI/ResearchPanelUI.cs` | L43 `: BuildingPanelBase` | 연구소 |

→ 4개 패널 모두 동일한 갭을 공유한다. 개별 패널이 아니라 **공통 베이스(`BuildingPanelBase`) 한 곳**에서 "내 건물이 죽으면 닫는다"를 처리하면 4개 전부 커버된다.

---

## 근거 코드 (실측 줄번호)

### 1) `BuildingPanelBase.cs` — 공통 베이스

- L54: `public abstract class BuildingPanelBase : MonoBehaviour, IGameUI` — MonoBehaviour 기반.
- L99: `protected BuildingData _currentBuilding;` — 현재 패널이 표시 중인 건물. `Close()` 시 null로 초기화.
- L106: `public bool IsOpen => _popup != null && _popup.IsVisible;` — 팝업 가시성 기준(주의: 아래 스킬 조준 특성 참조).
- L116: `public int CurrentBuildingId => _currentBuilding?.Id ?? -1;` — 이미 존재하는 현재 건물 Id 접근자.
- L129~145: `protected void InitializeBase(BuildingPlacementUseCase, ResourceUseCase, NetworkBuildingController)` — 자식 Initialize에서 1회 호출. 버튼 이벤트 연결 지점. **여기가 구독을 추가할 후보 지점.**
- L156~180: `public virtual void Show(BuildingData building)` — `_currentBuilding = building` 저장 후 `OnShow(building)` 호출.
- L196~214: `public virtual void Close()` — 맨 처음 `OnBeforeClose()` 호출(L199) → `ClosedFrame = Time.frameCount`(L203) → `UIManager.Instance?.HideBlockingOverlay()`(L207) → `_popup?.Hide()`(L210) → `_currentBuilding = null`(L213).
- L220: `protected virtual void OnBeforeClose() { }` — 자식 정리 훅.
- **현재 `OnDestroy`가 없다.** 구독을 추가하면 해제(구독 누수 방지)를 위한 `OnDestroy` 신설이 필요하다.

### 2) `GameEvents.cs` — 사망 이벤트 채널

- L341~350: `public readonly struct BuildingDiedEvent { public readonly BuildingData Building; }` — 건물 전용 강타입 사망 DTO. `.Building`으로 바로 접근(캐스트 불필요).
- L802: `public static readonly Subject<BuildingDiedEvent> OnBuildingDied = new Subject<BuildingDiedEvent>();`

### 3) `OnBuildingDied` 발행 지점 (누가 발행하나)

| 위치 | 파일:줄 | 맥락 |
|------|---------|------|
| 전투 파괴(서버/싱글) | `UnitCombatUseCase.cs` L1335, L1612, L1804 | `RemoveBuilding` 직전 `OnBuildingDied.OnNext(...)` |
| 철거(서버/싱글) | `BuildingPlacementUseCase.cs` L309 | `DemolishBuilding` 내부 발행 |
| **멀티 클라 재발행** | `NetworkCombatController.cs` L1027 | `HandleBuildingDied`(L1001~1030): `EntityDiedClientRpc` 수신 → `RemoveBuilding` 후 `OnBuildingDied.OnNext(...)` |

### 4) 현재 `OnBuildingDied` 구독자 (건물 패널은 없음 = 갭)

`FlowFieldService`(L80), `PopulationUseCase`(L59), `GameEndUseCase`(L47), `BuildingFactory`(L108), `ProductionTicker`(L175/L208), `HexGridRenderer`(L219), `AIOpponentController`(L184), `HitPresentationQueue`(L172), `GameBootstrapper.Setup`(L417 — 연구소 파괴 시 연구 취소·환불), `GameBootstrapper.Network`(L109), `NetworkCombatController`(서버 구독 L143~144).

→ **UI 패널(4종) 중 어느 것도 `OnBuildingDied`를 구독하지 않는다.** 그래서 건물이 죽어도 패널이 스스로 닫히지 않는다. 이것이 이 작업의 핵심 갭이다.

---

## 각 패널의 기존 `OnBeforeClose` (Close 시 자동 연계될 정리)

`Close()`는 맨 처음 `OnBeforeClose()`를 호출하므로(L199), 베이스에서 `Close()`만 불러 주면 각 패널의 정리가 자동으로 따라온다.

| 패널 | `OnBeforeClose` 위치 | 정리 내용 |
|------|---------------------|-----------|
| `BuildingSkillPanelUI` | L260~263 | `_skillAimController?.CancelAim();` — **조준 취소** |
| `ProductionPanelUI` | L338~342 | `IsSettingRallyPoint=false` + `_ticker.HideAllRallyMarkers()` — 랠리 마커 숨김 |
| `ResearchPanelUI` | L197~200 | 정리 없음(주석: 로컬 진행은 의도적으로 유지) |
| `BuildingActionPanelUI` | (오버라이드 없음) | 베이스 기본 동작만 |

→ **스킬 조준 취소는 `Close()`가 `OnBeforeClose()`를 부르며 자동 연계**된다. 별도 조준 취소 코드를 베이스 핸들러에 넣을 필요가 없다.

---

## 중요 특성 — 스킬 조준 중에는 `IsOpen`이 false

`BuildingSkillPanelUI.OnSkillPointerDown`(L300~350)에서 지점 지정 스킬 버튼을 누르면 조준 모드 진입 시 **`_popup?.Hide()`를 호출한다(L322)**. 이때 `AnimatedPanel`이 비가시 상태가 되어 베이스의 `IsOpen`(L106, `_popup.IsVisible` 기준)이 **false**가 된다. 그러나 `_currentBuilding`은 여전히 조준 대상 건물을 가리킨다.

→ 따라서 "이 패널이 죽은 건물을 다루고 있는가" 판정은 **`IsOpen`이 아니라 `_currentBuilding.Id` 매칭**이어야 조준 중 케이스까지 잡을 수 있다. `IsOpen`으로 판정하면 조준 중(팝업 숨김) 상태를 놓친다.

또한 패널이 닫혀 있으면(`Close()` 후) `_currentBuilding == null`이므로(L213), Id 매칭이 자연히 실패하여 엉뚱한 닫힘이 발생하지 않는다.

---

## 멀티플레이 발행 여부 — 확인 완료

앞선 조사에서 "멀티 클라이언트에서도 `OnBuildingDied`가 로컬 발행되는지"를 위험 항목으로 남겼으나, 실측으로 확인됐다:

- 멀티 클라이언트는 `NetworkCombatController.HandleBuildingDied`(L1001~1030)가 `EntityDiedClientRpc` 흐름에서 **로컬로 `OnBuildingDied.OnNext(...)`를 재발행한다(L1027).**
- 즉 베이스에서 `OnBuildingDied`를 구독하는 방식은 **싱글/호스트/순수 클라이언트 모두**에서 동작한다. 별도의 클라 despawn 보완 연결은 불필요하다.
- 단, 이는 "내 화면에서 그 건물의 사망 패킷이 도착했을 때" 발행되는 것이므로, 파괴 인지 시점이 곧 패널 닫힘 시점이 된다(서버 권위와 정합).

---

## 영향 범위

- **주 변경 파일**: `BuildingPanelBase.cs` 1개. (구독 추가 + 핸들러 + `OnDestroy` 해제)
- **각 패널 4종**: 기존 `OnBeforeClose`를 그대로 사용. 코드 변경 없음(스킬 조준 취소는 자동 연계).
- **레이어 방향**: `BuildingPanelBase`는 Presentation, `GameEvents.OnBuildingDied`는 Application. Presentation → Application 정방향 구독이므로 아키텍처 제약 위반 없음(Application → Infra 아님).
- **회귀 위험**: 낮음. 죽은 건물이 현재 건물이 아닐 때는 아무 동작 없음. 현재 건물일 때만 `Close()` 호출(기존과 동일한 정리 경로 재사용).

---

## 조사 중 확인한 부가 사항

- `BuildingPanelBase`에 이미 `CurrentBuildingId`(L116) 접근자가 있어 매칭 판정에 활용 가능하나, 베이스 내부에서는 `_currentBuilding` 직접 참조가 더 단순하다.
- `Close()`는 `_popup?.Hide()`, `UIManager.Instance?.HideBlockingOverlay()` 등 null-safe로 구성되어 있어 중복/재진입 호출에도 비교적 안전하다. 다만 이미 닫힌 상태에서 다시 `Close()`가 불리는 중복 호출 가능성은 Plan의 위험 항목에서 다룬다.
