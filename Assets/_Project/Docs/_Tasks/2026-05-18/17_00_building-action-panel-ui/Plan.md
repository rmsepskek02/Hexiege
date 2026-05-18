# Plan — 비생산 건물 공용 액션 팝업 UI (베이스 클래스 방식, 안 A)

## 무엇을 만드는가 (자연어 설명)

지금 이 게임은 "내 생산건물"을 누르면 유닛 생산 팝업이 뜨지만, **채굴소·방어 타워·마법 건물 같은 비생산 건물을 누르면 아무 반응이 없다**. 이번 작업으로 그런 건물들도 누르면 **공용 팝업**이 뜨고, 그 안의 **철거 버튼**을 통해 건물을 없애고 골드를 일부 돌려받을 수 있도록 한다.

본기지(Castle)는 이번에도 클릭 무반응을 유지한다(철거 불가 + 추가 기능 없음).

새 팝업은 기존 생산 팝업과 똑같이 생긴 단순 버전이다: 상단에는 건물 이름, 본문에는 철거 버튼과 그 옆의 환불 예상 금액. 그 외의 모든 요소(생산 큐, 진행 바, 유닛 버튼, 업그레이드 버튼 등)는 들어가지 않는다.

### 왜 베이스 클래스 방식인가

생산 패널(`ProductionPanelUI`)과 새 액션 패널은 다음과 같은 공통 로직을 가진다:

- 팝업 열고/닫기 흐름 (`AnimatedPanel.Show/Hide` + `SharedBackgroundButton.Register/Unregister`)
- 헤더 텍스트에 건물 이름 표시
- `IGameUI` 인터페이스 (게임 시작/종료 시 자동 닫힘)
- `IsOpen` / `ClosedFrame` 프로퍼티 (InputHandler가 같은 프레임 재오픈 방지에 사용)
- 철거 버튼 클릭 처리 — 골드 환불 + 도메인 제거 (싱글/멀티 분기)
- 환불 금액 텍스트 갱신 (`UpdateDemolishRefund`)

이 공통 로직을 그대로 복사 붙여넣기 하면 **유지보수 시 두 곳을 동시에 수정해야 하는 위험**이 생긴다. 따라서 두 패널의 공통 부모인 **`BuildingPanelBase`** 를 신규로 추가해 공통 로직을 한 곳에 모으고, 두 서브클래스가 각자의 차이점만 책임지도록 한다. 이렇게 하면 새 액션 패널의 자체 구현 코드는 **거의 비어 있는 수준**이 된다.

### 이번 작업의 범위 요약

1. `BuildingPanelBase` 추상 베이스 클래스 신규 추가 — 공통 로직 보유
2. `ProductionPanelUI`를 `BuildingPanelBase` 상속으로 리팩토링 — 공통 코드 제거, 생산 전용 코드만 남김
3. `BuildingActionPanelUI` 신규 추가 — `BuildingPanelBase` 상속, 추가 코드 거의 없음
4. `BuildingTypeHelper.CanShowActionPanel()` 헬퍼 추가
5. `InputHandler`에 새 패널 분기 추가
6. `GameBootstrapper`에 새 패널 주입 + 비생산 건물 환불 캐시 추가
7. 새 패널 자동 연결 에디터 스크립트 작성 (`SetupBuildingActionPanelUI.cs`)
8. 프리팹/Inspector 작업 (사용자)

---

## §0. 기존 로직 제거 안전 근거 (필수 명시)

본 작업은 **신규 추가 + ProductionPanelUI 리팩토링**으로 구성된다. 외부 동작 변화는 다음 한 가지에 한정한다:

- 비생산 건물 클릭 시 새 팝업이 뜸 (기존: 무반응)

ProductionPanelUI 리팩토링은 **내부 구조 변경**이며 외부 동작은 동일하게 유지한다 — 즉, 다른 클래스(InputHandler, GameBootstrapper, ProductionTicker 등)가 호출하는 `ProductionPanelUI`의 공개 API(`Initialize`, `Show`, `Close`, `IsOpen`, `ClosedFrame`, `CompleteRallyPointSetting`, `IsSettingRallyPoint`, `RallyPointSetFrame`, `CurrentBarracksId`)는 **시그니처와 동작 모두 동일**하다.

### 변경 분류

| 항목 | 변경 종류 | 외부 영향 |
|---|---|---|
| `BuildingPanelBase.cs` | 신규 | 없음 (서브클래스가 상속하기 전까지 미사용) |
| `ProductionPanelUI.cs` | 리팩토링 (베이스 상속) | 공개 API 동일 → 외부 호출자에 영향 없음 |
| `BuildingActionPanelUI.cs` | 신규 | 없음 (Bootstrap에서 주입하기 전까지 미사용) |
| `BuildingTypeHelper.cs` | 메서드 추가 | 없음 (기존 메서드 변경 없음) |
| `InputHandler.cs` | 분기 추가 + Initialize 시그니처 확장 | Bootstrap의 호출부 함께 수정 |
| `GameBootstrapper.cs` | 필드/호출 추가 + 캐시 채움 루프 추가 | 없음 (기존 호출 흐름 유지) |

### ProductionPanelUI 리팩토링 회귀 위험

- 베이스로 이동시킨 공통 코드는 **메서드 본문 그대로 보존** — 시그니처/동작 변경 없음.
- 베이스의 `Show(BuildingData)` 가상 메서드는 생산 패널 전용 흐름을 `OnShow()` 훅으로 위임 → 기존 `Show(BuildingData barracks)` 본문의 모든 동작 보존.
- 베이스의 `Close()` 가상 메서드는 생산 패널 전용 정리 흐름(`_ticker.HideAllRallyMarkers()`, `IsSettingRallyPoint = false`)을 `OnBeforeClose()` 훅으로 위임 → 기존 `Close()` 본문의 모든 동작 보존.
- 베이스의 `OnDemolishButtonClick` 공통 흐름은 `BeforeDemolish()` 가상 훅으로 생산 패널의 `CancelAllQueue` 호출을 위임 → 기존 멀티/싱글 분기 동작 보존.

회귀 테스트는 **§8 구현 순서**의 [2] 단계 직후, 생산 패널의 기존 기능(유닛 생산, 큐 취소, 자동 생산, 랠리 포인트, 업그레이드, 철거) 전체를 회귀 시나리오로 검증.

---

## §1. `BuildingPanelBase` 설계 (신규 베이스 클래스)

### 1-1. 파일 위치

`Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs`

### 1-2. 클래스 선언

```csharp
namespace Hexiege.Presentation
{
    /// <summary>
    /// 건물 클릭 팝업 UI의 공통 부모.
    /// 생산 건물용 ProductionPanelUI와 비생산 건물용 BuildingActionPanelUI가 이를 상속한다.
    ///
    /// 공통으로 제공하는 것:
    ///   - 팝업 열고/닫기 (AnimatedPanel + SharedBackgroundButton)
    ///   - 헤더 텍스트 갱신
    ///   - 철거 버튼 클릭 → 골드 환불 + 도메인 제거 (싱글/멀티 분기)
    ///   - 환불 금액 텍스트 갱신
    ///   - IsOpen / ClosedFrame 프로퍼티
    ///   - IGameUI 인터페이스 (OnGameStarted/OnGameEnded에서 Close 호출)
    ///
    /// 서브클래스에서 확장 지점:
    ///   - protected virtual void OnShow(BuildingData building)
    ///       → 추가 초기화 (유닛 버튼 바인딩, 진행바, 업그레이드 버튼 등)
    ///   - protected virtual void OnBeforeClose()
    ///       → 닫기 직전 추가 정리 (랠리 마커 숨김 등)
    ///   - protected virtual void BeforeDemolish()
    ///       → 싱글플레이 철거 직전 추가 처리 (CancelAllQueue 등)
    /// </summary>
    public abstract class BuildingPanelBase : MonoBehaviour, IGameUI
    {
        // ... 아래 1-3 ~ 1-7 참조
    }
}
```

### 1-3. 직렬화 필드 (공통)

```csharp
[Header("Popup")]
[Tooltip("팝업 등장/사라짐 애니메이션을 담당하는 컴포넌트.")]
[SerializeField] protected AnimatedPanel _popup;

[Tooltip("팝업 바깥 영역 탭 감지용 컴포넌트. Show 시 Register, Close 시 Unregister.")]
[SerializeField] protected SharedBackgroundButton _sharedBackground;

[Header("Header")]
[Tooltip("팝업 상단에 건물 이름을 표시. Show() 시 BuildingType.ToString()으로 갱신.")]
[SerializeField] protected TextMeshProUGUI _headerText;

[Header("Buttons")]
[Tooltip("팝업 닫기 버튼 (X 버튼).")]
[SerializeField] protected Button _cancelButton;

[Header("Demolish")]
[Tooltip("철거 버튼. 클릭 시 환불 후 건물 제거.")]
[SerializeField] protected Button _demolishButton;

[Tooltip("철거 시 받게 될 골드 환불액 텍스트. 초록색으로 표시.")]
[SerializeField] protected TextMeshProUGUI _demolishRefundText;
```

### 1-4. 의존성 필드 (공통)

```csharp
protected BuildingPlacementUseCase _buildingPlacement;
protected ResourceUseCase _resource;
protected NetworkBuildingController _networkBuildingController;
protected BuildingData _currentBuilding;
```

> **참고**: 생산 패널은 추가로 `_production`, `_population`, `_ticker`, `_networkProductionController`를 가지지만, 이는 **서브클래스 전용 필드**로 `ProductionPanelUI`에 그대로 남는다.

### 1-5. 공개 상태 프로퍼티

```csharp
public bool IsOpen => _popup != null && _popup.IsVisible;
public int ClosedFrame { get; protected set; } = -1;
public int CurrentBuildingId => _currentBuilding?.Id ?? -1;
```

> **마이그레이션 주의**: 기존 `ProductionPanelUI.CurrentBarracksId`는 의미적으로 동일하지만 이름이 다르다. 외부 호출자가 있다면 같이 갱신해야 한다 — 호출처는 §2-3에서 확인.

### 1-6. 베이스의 초기화 메서드 (`InitializeBase`)

서브클래스마다 의존성 목록이 다르므로 `Initialize` 자체는 서브클래스가 정의한다. 베이스는 **공통 의존성만 받는 `InitializeBase`** 를 protected로 제공한다.

```csharp
/// <summary>
/// 베이스 공통 의존성 주입 + 버튼 이벤트 연결.
/// 서브클래스의 Initialize에서 가장 먼저 호출해야 한다.
/// </summary>
protected void InitializeBase(
    BuildingPlacementUseCase buildingPlacement,
    ResourceUseCase resource,
    NetworkBuildingController networkBuildingController)
{
    _buildingPlacement = buildingPlacement;
    _resource = resource;
    _networkBuildingController = networkBuildingController;

    if (_cancelButton != null) _cancelButton.onClick.AddListener(Close);
    if (_demolishButton != null) _demolishButton.onClick.AddListener(OnDemolishButtonClick);
}
```

### 1-7. 공통 메서드 — Show / Close / 철거 / 환불

```csharp
/// <summary>
/// 패널 표시. 공통 흐름(상태 저장, 헤더 갱신, 팝업 표시, 환불액 갱신)을 처리한 뒤
/// OnShow(building) 훅을 호출해 서브클래스 전용 초기화를 위임한다.
/// </summary>
public virtual void Show(BuildingData building)
{
    _currentBuilding = building;

    if (_headerText != null)
        _headerText.text = building.Type.ToString();

    _popup?.Show();
    _sharedBackground?.Register(Close);

    RaceId race = (building.Team == TeamId.Blue)
        ? GameRaceContext.BlueRace
        : GameRaceContext.RedRace;
    UpdateDemolishRefund(race);

    OnShow(building);
}

/// <summary>
/// 서브클래스 전용 초기화 훅. Show()의 마지막에 호출된다.
/// 생산 패널은 유닛 버튼 바인딩 / 업그레이드 버튼 표시 등을 여기서 처리.
/// 액션 패널은 추가 작업이 없으므로 빈 구현으로 충분.
/// </summary>
protected virtual void OnShow(BuildingData building) { }

/// <summary>
/// 패널 닫기. OnBeforeClose() 훅으로 서브클래스 전용 정리를 먼저 수행한 뒤
/// 공통 정리(ClosedFrame 기록, 배경 해제, 팝업 숨김, 상태 초기화)를 실행한다.
/// </summary>
public virtual void Close()
{
    OnBeforeClose();

    ClosedFrame = Time.frameCount;
    _sharedBackground?.Unregister();
    _popup?.Hide();
    _currentBuilding = null;
}

/// <summary>
/// 닫기 직전 추가 정리 훅. 생산 패널은 랠리 마커 숨김 / IsSettingRallyPoint 리셋을 여기서 처리.
/// </summary>
protected virtual void OnBeforeClose() { }

/// <summary>
/// 환불 금액 텍스트 갱신.
/// 누적 투자 비용(GameBootstrapper에서 캐싱)의 50%를 초록색으로 표시.
/// </summary>
protected void UpdateDemolishRefund(RaceId race)
{
    if (_demolishRefundText == null || _currentBuilding == null) return;

    int totalInvested = BuildingStats.GetTotalInvestedCost(_currentBuilding.Type, race);
    int refund = totalInvested / 2;

    _demolishRefundText.text = $"{refund}";
    _demolishRefundText.color = Color.green;
}

/// <summary>
/// 철거 버튼 클릭 핸들러 — 공통 흐름.
/// 멀티: RequestDemolishServerRpc 호출 → 서버에서 모두 처리.
/// 싱글: BeforeDemolish() 훅 → 골드 환불 → DemolishBuilding → Close.
/// </summary>
protected virtual void OnDemolishButtonClick()
{
    if (_currentBuilding == null) return;

    bool isNetworkMode = _networkBuildingController != null
        && NetworkManager.Singleton != null
        && NetworkManager.Singleton.IsListening;

    if (isNetworkMode)
    {
        _networkBuildingController.RequestDemolishServerRpc(_currentBuilding.Id);
    }
    else
    {
        // 1) 서브클래스 전용 사전 처리 (예: 생산 패널의 CancelAllQueue)
        BeforeDemolish();

        // 2) 골드 환불 — 누적 투자비의 50%
        if (_resource != null)
        {
            RaceId race = (_currentBuilding.Team == TeamId.Blue)
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            int totalInvested = BuildingStats.GetTotalInvestedCost(_currentBuilding.Type, race);
            int refund = totalInvested / 2;
            _resource.AddGold(_currentBuilding.Team, refund);
        }

        // 3) 도메인 제거 (BuildingFactory가 GO도 함께 제거)
        _buildingPlacement?.DemolishBuilding(_currentBuilding.Id);
    }

    Close();
}

/// <summary>
/// 싱글플레이 철거 직전 추가 처리 훅.
/// 생산 패널만 오버라이드해서 CancelAllQueue(생산 큐 취소 + 차감 골드 환불)를 호출한다.
/// </summary>
protected virtual void BeforeDemolish() { }

// IGameUI
public virtual void OnGameStarted() => Close();
public virtual void OnGameEnded() => Close();
```

### 1-8. 베이스로 올리지 않는 것 (서브클래스 전용)

생산 패널 전용 — `ProductionPanelUI`에 그대로 남는다:

- 유닛 버튼 / 초상화 / 비용 텍스트 / 자동·잠금 인디케이터
- 생산 큐 슬롯 이미지들
- 진행 바 (`_progressFill`)
- 골드 / 인구 텍스트
- 랠리 포인트 버튼 + `IsSettingRallyPoint` / `RallyPointSetFrame` / `CompleteRallyPointSetting`
- 업그레이드 버튼 / 비용 텍스트 / 아이콘 / CanvasGroup
- `BuildingUnitMapping` / `UnitPortraitEntry` / `BuildingIconEntry` 직렬화 구조체
- 의존성: `UnitProductionUseCase`, `PopulationUseCase`, `ProductionTicker`, `NetworkProductionController`
- `Update()` (롱프레스 감지 + 진행바 갱신)
- 모든 유닛 탭/롱프레스/큐 슬롯/업그레이드 관련 메서드

---

## §2. `ProductionPanelUI` 리팩토링

### 2-1. 클래스 선언 변경

기존:
```csharp
public class ProductionPanelUI : MonoBehaviour, IGameUI
```

변경:
```csharp
public class ProductionPanelUI : BuildingPanelBase
```

> `IGameUI`는 베이스가 이미 구현하므로 제거.
> `MonoBehaviour`는 베이스 상속으로 자동 포함.

### 2-2. 베이스로 이동 (서브클래스에서 제거할 것)

다음 필드/메서드를 **`ProductionPanelUI`에서 제거**하고 베이스에서 상속받아 사용:

**제거할 필드**
- `_popup`, `_sharedBackground` (Header: Popup)
- `_headerText` (Header: Header)
- `_cancelButton` (Header: Buttons — `_rallyPointButton`은 남김)
- `_demolishButton` (Header: Action Buttons — `_upgradeIconImage`, `_upgradeButtonGroup`은 남김)
- `_demolishRefundText` (Header: Action Buttons)
- `_buildingPlacement`, `_resource`, `_networkBuildingController`, `_currentBarracks` 의존성/상태

**제거할 메서드**
- `IsOpen`, `ClosedFrame` 프로퍼티 (베이스로 이동)
- `UpdateDemolishRefund(RaceId)` (베이스로 이동)
- `OnGameEnded()`, `OnGameStarted()` (베이스의 virtual 메서드 사용)

**시그니처 변경**
- `_currentBarracks` 필드 제거 → 베이스의 `_currentBuilding` 사용. 내부 모든 참조를 `_currentBuilding`으로 교체.
- `CurrentBarracksId` 프로퍼티 → 베이스의 `CurrentBuildingId`로 대체. **외부 호출자가 있다면 함께 갱신** (호출처 확인은 §2-3).

### 2-3. `CurrentBarracksId` 외부 호출자 확인

리팩토링 전에 다음을 grep으로 확인:

```
grep -r "CurrentBarracksId" Assets/
```

발견된 모든 호출처를 `CurrentBuildingId`로 교체. 발견되지 않으면 안전하게 이름만 변경.

### 2-4. `Initialize` 시그니처 유지 + `InitializeBase` 호출 추가

```csharp
public void Initialize(UnitProductionUseCase production,
    ResourceUseCase resource, PopulationUseCase population,
    ProductionTicker ticker,
    NetworkProductionController networkProductionController = null,
    BuildingPlacementUseCase buildingPlacement = null,
    NetworkBuildingController networkBuildingController = null)
{
    // 1) 공통 의존성은 베이스에 위임
    InitializeBase(buildingPlacement, resource, networkBuildingController);

    // 2) 생산 패널 전용 의존성
    _production = production;
    _population = population;
    _ticker = ticker;
    _networkProductionController = networkProductionController;

    // 3) 생산 전용 버튼 이벤트 (취소/철거 버튼은 베이스에서 등록 완료)
    if (_rallyPointButton != null) _rallyPointButton.onClick.AddListener(OnRallyPointClick);
    if (_upgradeButton != null) _upgradeButton.onClick.AddListener(OnUpgradeButtonClick);

    if (_unitButtons != null)
    {
        for (int i = 0; i < _unitButtons.Count; i++) SetupUnitButtonBySlot(_unitButtons[i], i);
    }

    SetupQueueSlotButtons();

    GameEvents.OnProductionQueueChanged.Subscribe(_ => UpdateUI()).AddTo(this);
    GameEvents.OnResourceChanged.Subscribe(_ => UpdateInfoBar()).AddTo(this);
}
```

> **외부 영향 없음**: `Initialize`의 시그니처는 그대로 유지 → GameBootstrapper의 호출부 변경 불필요.

### 2-5. `Show` → `OnShow` 오버라이드로 분해

기존 `Show(BuildingData barracks)` 본문을 베이스의 `Show`와 서브클래스의 `OnShow`로 분리:

```csharp
/// <summary>
/// 생산 패널 전용 초기화. 베이스의 Show()가 호출한다.
/// 베이스가 이미 _currentBuilding 저장, 헤더 갱신, _popup.Show(), _sharedBackground.Register(Close),
/// UpdateDemolishRefund(race)를 끝낸 상태에서 호출되므로 여기서는 생산 전용 작업만 처리한다.
/// </summary>
protected override void OnShow(BuildingData building)
{
    IsSettingRallyPoint = false;

    if (_ticker != null) _ticker.ShowRallyMarker(building.Id);

    RaceId race = (building.Team == TeamId.Blue) ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
    Debug.Log($"[ProductionUI] Show - Team: {building.Team}, Race: {race}, BuildingType: {building.Type}, Stage: {building.Stage}");

    BindButtonUnitTypes(race);
    UpdateButtonPortraits(building.Team, race);
    UpdateLockIndicators();
    UpdateUpgradeButton(race);
    UpdateUI();
}
```

> 기존 `Show` 메서드 자체는 **삭제**. 베이스의 `Show(BuildingData)`가 외부에 그대로 노출되므로 호출자(InputHandler, ProductionTicker 등)에 영향 없음.

### 2-6. `Close` → `OnBeforeClose` 오버라이드로 분해

기존 `Close()` 본문에서 베이스로 이미 이동한 부분(`ClosedFrame` 기록, `_sharedBackground.Unregister`, `_popup.Hide`, `_currentBuilding = null`)을 빼고 남은 생산 전용 정리를 `OnBeforeClose`로 옮긴다:

```csharp
/// <summary>
/// 베이스의 Close()가 호출하는 사전 정리 훅.
/// 생산 전용 상태(IsSettingRallyPoint)와 랠리 마커를 정리한다.
/// </summary>
protected override void OnBeforeClose()
{
    IsSettingRallyPoint = false;
    if (_ticker != null) _ticker.HideAllRallyMarkers();
}
```

> 기존 `Close()` 메서드 자체는 **삭제**. 베이스의 `Close()`가 외부에 그대로 노출.

### 2-7. `OnDemolishButtonClick` → `BeforeDemolish` 오버라이드로 축소

기존 `OnDemolishButtonClick()` 본문은 베이스가 모두 가져간다. 생산 패널의 차이점인 `CancelAllQueue` 호출만 `BeforeDemolish` 훅으로 남긴다:

```csharp
/// <summary>
/// 싱글플레이 철거 직전 처리.
/// 생산 건물인 경우 생산 큐 전체 취소 + 이미 차감된 골드 환불을 먼저 수행해야
/// 사용자가 잃는 골드 없이 깔끔하게 제거된다.
/// </summary>
protected override void BeforeDemolish()
{
    if (BuildingTypeHelper.IsProductionBuilding(_currentBuilding.Type) && _production != null)
        _production.CancelAllQueue(_currentBuilding.Id);
}
```

> 기존 `OnDemolishButtonClick()` 메서드 자체는 **삭제**.

### 2-8. 남는 것 (생산 전용 코드는 그대로)

다음은 ProductionPanelUI에 그대로 유지:

- 모든 `[Header("Unit ...")]` ~ `[Header("Building Icons ...")]` 필드 (베이스로 이동한 것 제외)
- `_rallyPointButton`, `_upgradeButton`, `_upgradeCostText`, `_upgradeIconImage`, `_upgradeButtonGroup`
- `_production`, `_population`, `_ticker`, `_networkProductionController` 의존성
- `IsSettingRallyPoint`, `RallyPointSetFrame`
- `_activeUnitTypes`, `_activeUnitLocks`, 롱프레스 관련 상태
- `enum ProductionFailReason`
- `SetupUnitButtonBySlot`, `OnUnitPointerDown`, `OnUnitPointerUp`, `Update`, `SetupQueueSlotButtons`, `OnQueueSlotClicked`
- `OnUnitTap`, `IsUnitLocked`, `ValidateProduction`, `HandleProductionFail`, `OnUnitLongPress`, `HandleToggleAuto`
- `OnRallyPointClick`, `CompleteRallyPointSetting`
- `UpdateUpgradeButton`, `OnUpgradeButtonClick`
- `GetBuildingIcon`
- `UpdateUI`, `UpdateLockIndicators`, `UpdateQueueSlots`, `ApplySlotImage`, `UpdateProgressBar`, `UpdateInfoBar`
- `UpdateButtonPortraits`, `GetPortrait`, `GetUnitEntriesForCurrentBuilding`, `BindButtonUnitTypes`

> **참조 일괄 치환**: 메서드 내부의 모든 `_currentBarracks` → `_currentBuilding`으로 변경.

---

## §3. `BuildingActionPanelUI` 신규

### 3-1. 파일 위치

`Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs`

### 3-2. 클래스 선언

```csharp
namespace Hexiege.Presentation
{
    /// <summary>
    /// 비생산 건물(채굴소·타워·마법 건물 등)을 클릭했을 때 표시되는 공용 액션 팝업.
    /// 현재 범위는 "헤더 + 철거 버튼 + 환불 금액 표시"이며, 향후 채굴소 일시정지 /
    /// 타워 우선 타겟 등의 추가 액션이 들어와도 이 클래스가 책임진다.
    ///
    /// 거의 모든 동작은 BuildingPanelBase에서 제공되므로 자체 구현은 Initialize 한 개로 충분하다.
    /// </summary>
    public class BuildingActionPanelUI : BuildingPanelBase
    {
        /// <summary>
        /// 의존성 주입. GameBootstrapper에서 호출.
        /// 비생산 건물은 생산 큐가 없으므로 UnitProductionUseCase / PopulationUseCase /
        /// NetworkProductionController는 필요하지 않다.
        /// </summary>
        public void Initialize(
            BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource,
            NetworkBuildingController networkBuildingController = null)
        {
            InitializeBase(buildingPlacement, resource, networkBuildingController);
        }
    }
}
```

### 3-3. 오버라이드 없음

베이스의 기본 동작(공통 Show → 헤더 + 환불 표시 + 팝업 열림, 공통 Close, 공통 OnDemolishButtonClick)이 비생산 건물에는 그대로 충분하므로 **`OnShow`/`OnBeforeClose`/`BeforeDemolish` 모두 오버라이드하지 않는다**.

### 3-4. 추가 필드 없음

베이스가 모든 직렬화 필드를 protected로 제공하므로 서브클래스가 추가로 선언할 필드가 없다. Inspector에는 베이스의 protected 필드들이 자동으로 노출된다.

> Unity의 protected `[SerializeField]` 필드는 서브클래스 Inspector에 노출되는 표준 패턴이다.

---

## §4. `BuildingTypeHelper.CanShowActionPanel()` 추가

### 4-1. 파일 위치

`Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs`

### 4-2. 추가 메서드

```csharp
/// <summary>
/// 비생산 건물 중 액션 패널을 표시할 대상인지 판정.
/// 생산 건물은 ProductionPanelUI가 담당하므로 제외, Castle은 철거 불가하므로 제외.
/// </summary>
public static bool CanShowActionPanel(BuildingType type)
{
    return !IsProductionBuilding(type) && type != BuildingType.Castle;
}
```

> 도메인 레이어 — Unity 의존 없음. 순수 함수.

---

## §5. `InputHandler` 수정

### 5-1. 신규 필드

```csharp
private BuildingActionPanelUI _actionPanelUI;
```

### 5-2. `Initialize` 시그니처 확장

```csharp
public void Initialize(
    GridInteractionUseCase gridInteraction,
    Camera mainCamera,
    BuildingPlacementUseCase buildingPlacement,
    BuildingPlacementUI buildingUI,
    ProductionPanelUI productionUI,
    BuildingActionPanelUI actionPanelUI)    // 신규
{
    _gridInteraction = gridInteraction;
    _mainCamera = mainCamera;
    _buildingPlacement = buildingPlacement;
    _buildingUI = buildingUI;
    _productionUI = productionUI;
    _actionPanelUI = actionPanelUI;
}
```

### 5-3. ClosedFrame 체크에 새 UI 포함

기존:
```csharp
int frame = Time.frameCount;
if ((_buildingUI != null && _buildingUI.ClosedFrame == frame)
    || (_productionUI != null && _productionUI.ClosedFrame == frame))
    return;
```

변경:
```csharp
int frame = Time.frameCount;
if ((_buildingUI != null && _buildingUI.ClosedFrame == frame)
    || (_productionUI != null && _productionUI.ClosedFrame == frame)
    || (_actionPanelUI != null && _actionPanelUI.ClosedFrame == frame))
    return;
```

### 5-4. 건물 클릭 분기 확장

기존 분기를 다음과 같이 변경 — `BuildingTypeHelper.CanShowActionPanel()` 사용:

```csharp
if (_buildingPlacement != null)
{
    BuildingData buildingAtPos = _buildingPlacement.GetBuildingAt(clickedCoord);
    if (buildingAtPos != null)
    {
        bool isMine = buildingAtPos.Team == LocalPlayerTeam.Current;
        bool isAlive = buildingAtPos.IsAlive;

        if (isMine && isAlive)
        {
            if (BuildingTypeHelper.IsProductionBuilding(buildingAtPos.Type)
                && _productionUI != null)
            {
                // 생산 건물 → 생산 패널
                _productionUI.Show(buildingAtPos);
            }
            else if (BuildingTypeHelper.CanShowActionPanel(buildingAtPos.Type)
                && _actionPanelUI != null)
            {
                // 비생산 건물 (Castle 제외) → 공용 액션 패널
                _actionPanelUI.Show(buildingAtPos);
            }
            // Castle은 어느 분기에도 들어가지 않음 → 클릭 무반응 유지
        }
        // 적 건물 클릭: 어떤 팝업도 띄우지 않음 (기존 동작 유지)

        _gridInteraction?.SelectTileAt(worldPos);
        return;
    }
}
```

> 적 건물 클릭 처리는 **기존 동작 유지** — 의뢰서 확정 사항.

---

## §6. `GameBootstrapper` 수정

### 6-1. Inspector 필드 추가

```csharp
[Tooltip("비생산 건물 공용 액션 패널 UI (MiningPost / Tower / 특수건물 클릭 시 표시).")]
[SerializeField] private BuildingActionPanelUI _buildingActionPanelUI;
```

위치: `_productionUI` 필드 바로 다음 줄.

### 6-2. UIManager 등록 추가

`LoadMap()` 안의 UIManager 등록 블록:

```csharp
if (_uiManager != null)
{
    _uiManager.Register(_gameHudUI);
    _uiManager.Register(_productionUI);
    _uiManager.Register(_buildingUI);
    _uiManager.Register(_buildingActionPanelUI);   // 추가
    _uiManager.Register(_gameEndUI);
    _uiManager.Initialize();
}
```

### 6-3. Initialize 호출 추가

`SetupBuildings()` 끝부분에 추가:

```csharp
if (_buildingActionPanelUI != null)
{
    bool isNetworkMode = IsNetworkMode();
    NetworkBuildingController controller = isNetworkMode ? _networkBuildingController : null;
    _buildingActionPanelUI.Initialize(_buildingPlacement, _resource, controller);
}
```

### 6-4. SetupInput 수정

```csharp
private void SetupInput()
{
    if (_inputHandler != null)
    {
        _inputHandler.Initialize(
            _gridInteraction, _mainCamera,
            _buildingPlacement, _buildingUI, _productionUI,
            _buildingActionPanelUI);   // 추가
    }
}
```

### 6-5. 비생산 건물 환불 캐시 추가 (필수)

`InitializeBuildingStatsFromConfig()` 마지막에 비생산 건물용 캐시 채움 루프 추가.

**이유**: 기존 캐시 채움 로직은 stage1 생산 건물만 순회하므로, 비생산 건물에는 `SetTotalInvestedCost`가 호출되지 않는다. 새 액션 패널이 `GetTotalInvestedCost`로 환불액을 계산할 때 0이 반환되어 환불액이 0으로 표시되는 버그가 발생한다.

**추가 코드**:
```csharp
// ── 비생산 건물 환불 캐시 ───────────────────────────────────────────
// 비생산 건물은 단계 개념이 없으므로 GoldCost 자체가 누적 투자 비용이 된다.
// 액션 패널에서 GetTotalInvestedCost를 조회해 환불액(50%)을 계산하므로,
// 여기서 미리 캐시를 채워두지 않으면 환불액 0으로 표시되는 버그가 생긴다.
// (Castle은 철거 불가이므로 캐시 채울 필요 없음 — 안전상 동일 처리해도 무방)
var nonProductionBuildings = new[]
{
    BuildingType.MiningPost,
    BuildingType.AutoTower,
    BuildingType.FlightFacility,
    BuildingType.Research,
    BuildingType.MagicBuilding,
    BuildingType.HealShrine,
};
foreach (var race in new[] { RaceId.Human, RaceId.Spirit, RaceId.Transcendence })
{
    foreach (var type in nonProductionBuildings)
    {
        int cost = BuildingStats.GetGoldCost(type, race);
        BuildingStats.SetTotalInvestedCost(type, race, cost);
    }
}
```

> **목록 확인 필수**: 위 비생산 건물 enum 목록은 현재 `BuildingType` 정의와 일치해야 한다. 구현 시 `BuildingTypeHelper.IsProductionBuilding(type) == false` && `type != Castle` 인 모든 BuildingType을 확인해 누락 없도록 한다.

---

## §7. `SetupBuildingActionPanelUI.cs` 에디터 자동 연결 스크립트 (완전 자동화)

### 7-1. 파일 위치

`Assets/_Project/Scripts/Editor/SetupBuildingActionPanelUI.cs`

### 7-2. 목적

메뉴 한 번 클릭으로 다음을 **전부 자동 처리**한다:

1. `ProductionPanelUI` GO를 복제해 `BuildingActionPanel` GO를 생성
2. 생산 전용 자식 GO 제거 (6개 공유 필드가 참조하는 GO만 남김)
3. `ProductionPanelUI` 컴포넌트 제거 → `BuildingActionPanelUI` 컴포넌트 추가
4. 공유 필드 6개 값을 `BuildingActionPanelUI`에 복사 배선
5. `GameBootstrapper._buildingActionPanelUI` 슬롯에 인스턴스 연결
6. 씬 Dirty 마킹 + Undo 등록

→ **사용자는 메뉴를 실행하고 씬을 저장(Ctrl+S)하기만 하면 된다.**

> ⚠️ 이미 `BuildingActionPanelUI` 컴포넌트가 씬에 존재하면 생성 단계를 건너뛰고 필드 재배선 + GameBootstrapper 연결만 수행한다.

### 7-3. 실행 방법

Unity 메뉴 `Hexiege/UI/Setup Building Action Panel UI` 클릭 → 콘솔 확인 → Ctrl+S 저장.

### 7-4. 구현 방식 (핵심 알고리즘)

```
[A] 씬에서 BuildingActionPanelUI 탐색
    → 이미 있으면 → [D] 필드 재배선 + [E] GameBootstrapper 연결 → 종료

[B] 씬에서 ProductionPanelUI 탐색
    → 없으면 에러 후 종료

[C] BuildingActionPanel GO 생성
    1. ProductionPanelUI GO 복제 (Object.Instantiate)
    2. ProductionPanelUI 컴포넌트로부터 공유 필드 6개의 참조 오브젝트 읽기:
         _popup, _sharedBackground, _headerText,
         _cancelButton, _demolishButton, _demolishRefundText
    3. "보존 GO 집합" 구성:
         위 6개 Component가 부착된 GO + 루트까지의 조상 GO 전부
    4. ProductionPanelUI 컴포넌트 제거 (Undo.DestroyObjectImmediate)
    5. BuildingActionPanelUI 컴포넌트 추가 (Undo.AddComponent)
    6. 복제 GO의 모든 자식 순회:
         보존 집합에 포함되지 않은 자식은 DestroyImmediate
    7. GO 이름 → "BuildingActionPanel"
    8. ProductionPanelUI 옆에 위치 (같은 부모, sibling index +1)

[D] 공유 필드 6개 배선
    새 BuildingActionPanelUI의 SerializedObject를 통해
    C-2에서 읽은 참조를 그대로 기입
    (_popup, _sharedBackground, _headerText, _cancelButton,
     _demolishButton, _demolishRefundText)

[E] GameBootstrapper 연결
    FindFirstObjectByType<GameBootstrapper>() →
    SerializedObject.FindProperty("_buildingActionPanelUI") →
    objectReferenceValue = BuildingActionPanelUI 인스턴스

[F] 마무리
    ApplyModifiedProperties, EditorUtility.SetDirty, MarkSceneDirty,
    PingObject(BuildingActionPanel GO), 콘솔 로그 출력
```

### 7-5. 주의 사항

- `Object.DestroyImmediate` 사용 시 Undo 스택에 올리려면 `Undo.DestroyObjectImmediate`를 사용해야 한다.
- 공유 필드의 참조가 `ProductionPanelUI`에 연결되어 있지 않으면(null) 해당 필드는 건너뛰고 LogWarning 출력.
- `GameBootstrapper`를 씬에서 찾지 못하면 `[E]` 단계만 건너뛰고 LogWarning 출력 (나머지 작업은 완료).
- 스크립트는 **멱등(idempotent)** — 여러 번 실행해도 동일한 결과. `BuildingActionPanelUI`가 이미 존재하면 생성 없이 재배선만 수행.

---

## §7-6. BuildingActionPanel UI 레이아웃 명세

### 목적

ProductionPanelUI를 복제해 만든 BuildingActionPanel의 **최종 시각 구조**를 정의한다. 에디터 스크립트는 이 명세에 맞는 계층 구조를 자동으로 생성한다.

### 전체 계층 구조

```
BuildingActionPanel (RectTransform — full-screen stretch anchor)
├── SharedBackground (SharedBackgroundButton — 패널 외부 탭 닫기)
├── Popup (AnimatedPanel — 등장/사라짐 애니메이션)
│   ├── Header (이미지/배경)
│   │   ├── HeaderText (TextMeshProUGUI — 건물 이름)
│   │   └── CancelButton (Button — X 닫기)
│   └── ButtonGrid (GridLayoutGroup 컨테이너)
│       ├── Slot_1_1 (Button + CanvasGroup)  ← 1행 1열
│       ├── Slot_1_2 (Button + CanvasGroup)  ← 1행 2열
│       ├── Slot_1_3 (Button + CanvasGroup)  ← 1행 3열
│       ├── Slot_2_1 (Button + CanvasGroup)  ← 2행 1열
│       ├── Slot_2_2 (Button + CanvasGroup)  ← 2행 2열
│       └── Slot_2_3_Demolish (Button + CanvasGroup)  ← 2행 3열 = 철거 버튼
│           └── DemolishRefundText (TextMeshProUGUI — 환불 금액)
```

### 버튼 그리드 규칙

| 항목 | 값 |
|------|-----|
| 레이아웃 컴포넌트 | `GridLayoutGroup` |
| 열(Column) 수 | 3 |
| 행(Row) 수 | 2 |
| 철거 버튼 위치 | 2행 3열 (`Slot_2_3_Demolish`) |
| 셀 크기 결정 방식 | `cellSize` 고정 + 패널 너비에 맞게 계산 |
| 균일 크기 | 6개 슬롯 모두 동일한 `cellSize` |

### 미구현 버튼 처리

기능이 없는 슬롯은 **공간은 차지하되 보이지 않도록** 처리:

- 슬롯 GO에 `CanvasGroup` 컴포넌트 부착
- `CanvasGroup.alpha = 0` → 투명하게 숨김
- `CanvasGroup.blocksRaycasts = false` → 클릭 이벤트 무시
- 철거 버튼(`Slot_2_3_Demolish`)은 `alpha = 1`, `blocksRaycasts = true`로 활성 상태

> 향후 버튼 기능 추가 시 해당 슬롯의 `CanvasGroup.alpha = 1`, `blocksRaycasts = true`로만 변경하면 된다.

### 해상도 대응 (반응형 UI)

모든 RectTransform은 화면 크기에 관계없이 동일한 비율로 표시되도록 앵커를 설정한다.

| 요소 | 앵커 설정 |
|------|-----------|
| `BuildingActionPanel` (루트) | `min(0.5, 0)` `max(0.5, 0)` + pivot(0.5, 0) — 화면 하단 중앙 기준 고정 크기 |
| `Popup` 내부 컨테이너 | 부모에 full stretch (`0,0` ~ `1,1`) |
| `ButtonGrid` | 부모 너비에 맞춰 stretch + 높이는 셀 크기 × 행 수로 고정 |
| 각 슬롯(`Slot_*`) | `GridLayoutGroup`이 자동 배치 — 개별 앵커 불필요 |

### 패널 크기

ProductionPanelUI 대비 높이 대폭 축소:
- 헤더 영역: 고정 높이 (기존과 동일)
- 버튼 그리드: `셀 높이 × 2행 + spacing`
- 전체 패널 높이 ≈ 헤더 + 버튼 2행 분량만 확보

> 실제 픽셀 값은 에디터 스크립트가 ProductionPanelUI의 기존 셀 크기를 참고해 자동 계산한다.

---

## §8. 구현 순서

의존성을 따라 안전한 빌드 유지가 가능한 순서:

```
[1] BuildingPanelBase.cs 신규 작성
    └─ 공통 로직 모두 포함, 추상 클래스 선언, 컴파일은 통과 (다른 코드에서 참조 없음).

[2] ProductionPanelUI 리팩토링
    ├─ : BuildingPanelBase 상속으로 변경
    ├─ 공통 필드/메서드 제거 (베이스에서 상속)
    ├─ _currentBarracks → _currentBuilding 전체 치환
    ├─ CurrentBarracksId → CurrentBuildingId 치환 (외부 호출자도 함께)
    ├─ Show → OnShow 오버라이드로 분해
    ├─ Close → OnBeforeClose 오버라이드로 분해
    └─ OnDemolishButtonClick → BeforeDemolish 오버라이드로 축소
    ⇒ 컴파일 통과 + 생산 패널 기능 회귀 테스트 (유닛 생산, 큐 취소, 자동 생산, 랠리 포인트, 업그레이드, 철거 전부).

[3] BuildingTypeHelper.CanShowActionPanel() 추가
    └─ 순수 도메인 메서드, 사이드 이펙트 없음.

[4] BuildingActionPanelUI.cs 신규 작성
    └─ BuildingPanelBase 상속 + Initialize 한 개만 구현. 컴파일 통과.

[5] InputHandler 수정
    ├─ _actionPanelUI 필드 + Initialize 시그니처 확장
    ├─ ClosedFrame 체크에 새 UI 포함
    └─ 건물 클릭 분기에 BuildingTypeHelper.CanShowActionPanel 분기 추가
    ⇒ GameBootstrapper.SetupInput도 함께 수정해 시그니처 불일치 방지.

[6] GameBootstrapper 수정
    ├─ Inspector 필드 _buildingActionPanelUI 추가
    ├─ UIManager.Register 추가
    ├─ SetupBuildings()에서 Initialize 호출 추가
    ├─ SetupInput()에서 새 인자 전달
    └─ InitializeBuildingStatsFromConfig() 마지막에 비생산 건물 환불 캐시 루프 추가

[7] SetupBuildingActionPanelUI.cs (Editor) 완전 자동화로 재작성
    ├─ ProductionPanelUI GO 복제 → 생산 전용 자식 GO 제거 → BuildingActionPanelUI 컴포넌트 교체
    ├─ 공유 필드 6개 자동 배선 (ProductionPanelUI의 기존 참조값 그대로 복사)
    └─ GameBootstrapper._buildingActionPanelUI 슬롯 자동 연결

[8] 씬 작업 (사용자)
    └─ 메뉴 Hexiege/UI/Setup Building Action Panel UI 실행 → Ctrl+S 저장
       (BuildingActionPanel GO 생성 + 필드 배선 + GameBootstrapper 연결 전부 자동)
```

각 단계 사이에 컴파일이 통과되도록 순서를 잡았다 — [1] 이후 항상 빌드 가능.

### 테스트 시나리오 (구현 [6] 이후 + 프리팹 작업 [8] 이후)

**ProductionPanelUI 회귀 테스트 (베이스 상속 후 기존 동작 보존 확인)**:
- 싱글/멀티 각각에서:
  - 유닛 생산 (탭) — 정상 큐 등록
  - 자동 생산 (롱프레스) — 토글 동작
  - 큐 슬롯 클릭 — 취소 동작
  - 랠리 포인트 설정 — 마커 표시, 위치 저장
  - 업그레이드 — 다음 단계 건물로 교체
  - 철거 — 큐 환불 + 골드 환불 + 건물 제거
  - 패널 외부 탭 — 닫힘 + 같은 프레임 재오픈 방지
  - 게임 종료 시 패널이 열려 있으면 자동 닫힘

**BuildingActionPanelUI 신규 테스트**:
- 싱글/멀티 각각에서:
  - 자기 팀 채굴소 클릭 → 새 팝업 표시, 환불액 정확
  - 자기 팀 AutoTower 클릭 → 새 팝업 표시, 환불액 정확
  - 자기 팀 Castle 클릭 → **아무 팝업도 안 뜸**
  - 자기 팀 생산건물 클릭 → 기존 생산 팝업 표시 (회귀 없음)
  - 적 건물 클릭 → 어떤 팝업도 안 뜸
  - 새 팝업의 철거 버튼 → 골드 환불 + 건물 GO 제거 + 도메인 제거
  - 새 팝업 외부 탭 → 팝업 닫힘 + 같은 프레임 다른 팝업 재오픈 안 됨
  - 게임 종료 시 새 팝업이 열려 있으면 자동 닫힘

---

## §9. 위험 요소 및 완화책

| 위험 | 영향 | 완화책 |
|---|---|---|
| **ProductionPanelUI 리팩토링 회귀** — 베이스로 옮긴 코드의 동작이 미세하게 달라져 기존 기능 일부가 깨짐 | 유닛 생산, 큐, 자동 생산, 업그레이드 등 기존 기능 회귀 | 베이스의 메서드 본문은 기존 ProductionPanelUI 본문을 **그대로 보존**. `Show → OnShow`, `Close → OnBeforeClose`, `OnDemolishButtonClick → BeforeDemolish` 분해 시 처리 순서까지 동일하게 유지. §8 [2] 단계 회귀 테스트 필수. |
| `_currentBarracks → _currentBuilding` 치환 누락 | 컴파일 오류 또는 NullRef | 전체 파일 검색 후 일괄 치환. 컴파일 통과로 1차 확인. |
| `CurrentBarracksId → CurrentBuildingId` 외부 호출자 누락 | 컴파일 오류 | 리팩토링 전 grep으로 모든 호출처 확인 후 동시 갱신. |
| 비생산 건물 `GetTotalInvestedCost` 캐시 미스 → 환불액 0 표시 | UX 깨짐, 골드 환불 0 | §6-5의 캐시 채움 루프 필수 적용. 누락된 BuildingType 없는지 확인. |
| InputHandler `_actionPanelUI` 미주입 시 NullRef | 클릭 시 예외 | 분기마다 null 체크 (`_actionPanelUI != null`) — §5-4. |
| `_uiManager.Register` 누락 시 게임 종료 후에도 팝업 잔존 | 다음 게임 진입 시 잘못된 상태 | §6-2 등록 추가 필수. |
| `ClosedFrame` 체크 누락 → 같은 프레임 재오픈 | 외부 탭으로 닫아도 다시 열림 | §5-3 새 UI 포함 필수. |
| Castle이 새 팝업에 노출됨 | 사용자가 철거 시도 → 서버에서 차단되어도 UX 혼란 | InputHandler 분기에서 `CanShowActionPanel` 사용 — Castle은 자동 제외 (§4-2, §5-4). |
| 멀티플레이 `NetworkManager.Singleton.IsListening` 검사 누락 시 싱글에서 RPC 호출 | NullRef | 베이스의 `OnDemolishButtonClick`에 동일 패턴 적용 (§1-7). |
| `RequestDemolishServerRpc`가 Castle을 차단하지만 InputHandler에서도 차단 안 되면 적이 호스트일 때 의도 외 동작 | 실제로는 InputHandler에서 차단 + 서버에서 재차단 → 이중 안전망 | 그대로 유지 |
| protected `[SerializeField]` 필드가 서브클래스 Inspector에 노출되지 않음 | UI 연결 불가 | Unity 표준 동작 — protected SerializeField는 서브클래스 Inspector에 정상 노출. 우려 시 [field: SerializeField] 패턴이나 베이스에 자체 GetX/SetX 헬퍼 추가 가능. |

---

## 완료 현황

**완료일:** 2026-05-19  
**테스트 결과:** PASS — 채굴소(MiningPost)/AutoTower 클릭 시 BuildingActionPanel 팝업 표시, 철거 버튼 동작 확인.

---

## §10. 작업 범위 외 (이번 작업에서 하지 않는 것)

- 채굴소 전용 UI (생산 효율 표시, 일시정지) — 별도 작업.
- 방어 타워 전용 UI (사정거리 표시, 우선 타겟) — 별도 작업.
- 마법 건물 전용 스킬 버튼 — 별도 작업.
- 적 건물 클릭 시 정보 패널 — 별도 작업.
- `BuildingPanelBase`에 추가 공통 기능(Population/Gold 표시 등)을 더 끌어올리는 확장 — 이번 범위는 §1-3 ~ §1-7 한정.
- `MiningPostPanelUI` 등 비생산 건물 타입별 전용 서브클래스 — 향후 채굴소 일시정지 등 액션이 늘어나면 `BuildingActionPanelUI`를 베이스로 더 분화 가능 (현재는 단일 컴포넌트로 충분).
