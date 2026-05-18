# Research — 비생산 건물 공용 액션 팝업 UI

## 무엇을, 왜 조사하는가 (자연어 설명)

지금까지 게임에서 "내 건물"을 클릭했을 때 팝업이 뜨는 건 **유닛을 생산하는 건물**(예: 훈련소, 화염 사원 등)뿐이었다. 그 외의 건물 — 즉 **채굴소, 방어 타워, 마법 건물 같은 특수 건물** — 은 클릭해도 아무 반응이 없었다. 이번 작업의 목표는 그런 비생산 건물들도 클릭하면 공용 팝업이 뜨고, 그 안에서 **철거 버튼**을 통해 건물을 없앨 수 있게 만드는 것이다.

생산건물에는 이미 "철거 + 환불 50%" 시스템이 들어가 있다(2026-05-18 작업으로 완료). 이번에는 **같은 철거 로직을 재사용**하되, 생산큐·생산바·유닛 버튼 같은 "생산 전용 요소"는 모두 없는, **단순화된 공용 팝업**을 새로 만들 예정이다.

본기지(Castle)는 이번 범위에서 제외한다. Castle은 철거 자체가 금지되어 있고(이미 서버 RPC에서 막혀 있음), 추가로 붙일 기능도 없기 때문에 클릭 시 아무 반응 없는 현재 동작을 그대로 둔다.

이 문서는 **구현 계획(Plan.md)을 세우기 전에 반드시 확인해야 할 6개 파일을 모두 읽고, 어디를 어떻게 손대야 하는지를 정리한 사전 조사 결과**다.

---

## 1. 조사 대상 파일 (필수 Read 결과 요약)

### 1-1. `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`

생산건물 클릭 시 표시되는 기존 팝업. 이번에 만들 새 팝업의 **레이아웃 / API / 흐름 참조 원본**.

핵심 패턴 정리:

| 구성 요소 | 역할 | 새 팝업 재사용 여부 |
|---|---|---|
| `_popup (AnimatedPanel)` | DOTween 기반 팝업 등장/사라짐 애니메이션 | 재사용 (동일 패턴) |
| `_sharedBackground (SharedBackgroundButton)` | 팝업 외부 클릭 시 닫기 | 재사용 |
| `_headerText (TextMeshProUGUI)` | 상단에 `BuildingType.ToString()` 표시 | 재사용 |
| `_unitButtons / _unitButtonPortraits / _unitCostTexts` | 유닛 생산 버튼들 | **제거** |
| `_unitAutoIndicators / _unitLockIndicators / _unitButtonGroups` | 자동/잠금/숨김 인디케이터 | **제거** |
| `_queueSlotImages` | 생산 큐 슬롯 표시 | **제거** |
| `_progressFill` | 생산 진행 바 | **제거** |
| `_goldText / _populationText` | 보유 골드 / 인구 표시 | **제거** (또는 정책 결정 필요) |
| `_cancelButton` | 팝업 닫기 | 재사용 |
| `_rallyPointButton` | 랠리 포인트 설정 | **제거** (비생산 건물은 랠리 개념 없음) |
| `_upgradeButton / _upgradeCostText / _upgradeIconImage / _upgradeButtonGroup` | 업그레이드 | **제거** (현재 비생산 건물은 업그레이드 라인이 없음) |
| `_demolishButton / _demolishRefundText` | 철거 + 환불액 표시 | **재사용 (핵심)** |
| `_buildingUpgradeIcons` | 업그레이드 아이콘 매핑 | **제거** |

핵심 메서드 시그니처 (그대로 따라갈 부분):

- `Show(BuildingData barracks)` — 건물 데이터 받아서 팝업 열기.
- `Close()` — 팝업 닫고 `ClosedFrame = Time.frameCount` 기록.
- `IsOpen { get; }` — `_popup.IsVisible` 기준.
- `ClosedFrame { get; private set; } = -1` — InputHandler가 "같은 프레임에 닫힌 팝업 클릭이 그대로 다음 분기까지 흘러가는 것"을 막기 위해 사용.
- `IGameUI` 인터페이스 구현 → `OnGameStarted()` / `OnGameEnded()`에서 `Close()` 호출.

기존 `OnDemolishButtonClick()` 로직 (위치: 라인 660~705)은 다음과 같이 작동:

1. 멀티플레이 분기: `_networkBuildingController.RequestDemolishServerRpc(buildingId)` 호출 후 `Close()`.
2. 싱글플레이 분기:
   - 생산건물이면 `_production.CancelAllQueue(buildingId)` (큐 환불).
   - `BuildingStats.GetTotalInvestedCost(type, race) / 2`를 `_resource.AddGold()`로 환불.
   - `_buildingPlacement.DemolishBuilding(buildingId)` 호출.
   - `Close()`.

→ **비생산 건물은 `CancelAllQueue` 호출이 필요 없으므로 그 분기만 빼면 그대로 재사용 가능.**

기존 `UpdateDemolishRefund(RaceId race)` 로직 (라인 611~625):
- `BuildingStats.GetTotalInvestedCost(_currentBarracks.Type, race) / 2` → 텍스트에 표시, 초록색.
- 비생산 건물도 `BuildingStats`에 등록되어 있고(GameBootstrapper 초기화 시 모든 BuildingType 항목이 들어감), Stage 개념이 없는 건물은 `TotalInvestedCost` 캐시가 1단계 건설비 그대로일 가능성 → **GameBootstrapper에서 캐시를 어떻게 채우는지 확인 필요**.

---

### 1-2. `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs`

타일 클릭 → 어떤 팝업을 띄울지 결정하는 라우터. 새 팝업을 띄우려면 여기에 새 분기를 추가해야 함.

현재 클릭 분기 (라인 169~259, `HandleClick(screenPos)`):

```
0.5. 랠리포인트 설정 중이면 → 그것부터 처리
0.   UI 위 클릭이면 무시
     같은 프레임에 buildingUI/productionUI가 닫혔으면 무시 (ClosedFrame 체크)
2.   클릭한 타일에 건물이 있으면:
       - IsProductionBuilding 이고 자기 팀 이고 IsAlive 면 → _productionUI.Show(buildingAtPos)
       - 그 외에는 그냥 타일 선택만 (return)        ← 여기서 비생산 건물도 그냥 끝나버림
3.   금광이 있는 자기 팀 빈 타일 → MiningPost 건설 팝업
4.   자기 팀 빈 타일 → 건물 배치 팝업
5.   기타 → 타일 선택
```

**수정 지점**: 분기 2 안의 "그 외에는 그냥 타일 선택만"이 비생산 건물 클릭이 들어왔을 때 아무 팝업 없이 끝나는 원인. 여기에 새 분기를 추가해 "자기 팀 비생산 건물 + Castle 제외 + IsAlive" 조건일 때 새 팝업 `Show()`를 호출해야 함.

`ClosedFrame` 체크 (라인 198~201) — 새 UI도 똑같이 추가해야 함:
```csharp
if ((_buildingUI != null && _buildingUI.ClosedFrame == frame)
    || (_productionUI != null && _productionUI.ClosedFrame == frame))
    return;
```
→ 새 UI를 `_actionPanelUI`라 할 때 `|| (_actionPanelUI != null && _actionPanelUI.ClosedFrame == frame)` 추가 필요.

`Initialize()` 시그니처 (라인 82~94):
```csharp
public void Initialize(
    GridInteractionUseCase gridInteraction,
    Camera mainCamera,
    BuildingPlacementUseCase buildingPlacement,
    BuildingPlacementUI buildingUI,
    ProductionPanelUI productionUI)
```
→ 새 UI 참조를 받기 위해 인자 1개 추가 필요.

---

### 1-3. `Assets/_Project/Scripts/Domain/Building/BuildingType.cs`

BuildingType enum 전체. **비생산 + Castle 제외 건물 목록** 확정에 사용.

비생산 건물 (라인 27~33):
- `Castle` — **이번 작업 제외 (클릭 무반응 유지)**
- `MiningPost` — 채굴소
- `AutoTower` — 자동 방어 포탑
- `FlightFacility` — 지원 건물
- `Research` — 업그레이드 연구 건물
- `MagicBuilding` — 마법 특수 건물
- `HealShrine` — 회복 건물

→ **새 팝업이 떠야 하는 대상: `MiningPost`, `AutoTower`, `FlightFacility`, `Research`, `MagicBuilding`, `HealShrine` (6종).**

---

### 1-4. `Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs`

도메인 헬퍼. 다음 두 메서드가 이번 작업에서 필요:

- `IsProductionBuilding(BuildingType type)` — true/false. (현재는 `Castle` 포함 모든 비생산 건물이 false.)
- `GetStage(BuildingType type)` — 비생산 건물은 0 반환.

**현재 헬퍼에 없지만 추가하면 깔끔한 메서드**:
- `IsCastle(BuildingType type)` — 단순히 `type == BuildingType.Castle`이지만 의도가 명확.
- `CanShowActionPanel(BuildingType type)` — `!IsProductionBuilding(type) && !IsCastle(type)` 와 같이 한 줄로 묶을 수 있음. (Plan에서 추가 여부 제안)

InputHandler에서 직접 `type != BuildingType.Castle && !IsProductionBuilding(type)` 으로 인라인 처리해도 무방하지만, **Plan.md에서 헬퍼 추가 옵션을 사용자에게 제안**한다.

---

### 1-5. `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

새 UI를 어떻게 주입할지 정하는 기준 파일.

기존 `_productionUI` 주입 흐름:
1. `[SerializeField] private ProductionPanelUI _productionUI;` — Inspector 연결 (라인 76).
2. `LoadMap()` 안의 `_uiManager.Register(_productionUI);` (라인 617) — UI 매니저에 등록 (게임 시작/종료 콜백 자동 호출용).
3. `SetupProduction()` 내부 (라인 1143~1151)에서 `_productionUI.Initialize(...)` — 의존성 전달.
4. `SetupInput()` 내부 (라인 1031~1039)에서 InputHandler에 함께 주입.

→ **새 UI도 동일한 4단계로 등록해야 한다**:
1. `[SerializeField] private BuildingActionPanelUI _buildingActionPanelUI;` 필드 추가.
2. `_uiManager.Register(_buildingActionPanelUI);` 추가.
3. `_buildingActionPanelUI.Initialize(...)`에서 필요한 UseCase/컨트롤러 주입.
4. `_inputHandler.Initialize(...)`에 `_buildingActionPanelUI` 인자 추가.

**철거 환불 캐시 관련 확인 사항**:
`InitializeBuildingStatsFromConfig()` 라인 555~594에서 `BuildingStats.SetTotalInvestedCost` 캐시를 채우는 로직은 **stage1 시작 → GetNextStage 체인을 따라가는 방식**이다. 이 코드는 `stage1Buildings` 배열에 9개 1단계 생산건물만 들어 있고, **비생산 건물(MiningPost / AutoTower / FlightFacility / Research / MagicBuilding / HealShrine)에는 `SetTotalInvestedCost`가 전혀 호출되지 않는다**.

→ **결과: `BuildingStats.GetTotalInvestedCost(MiningPost, race)` 등의 호출은 캐시 미스 → 폴백 값(0 또는 GoldCost) 반환 가능성**. Plan.md에서 이 부분을 확정해야 함.

→ **확인 필요**: `BuildingStats.GetTotalInvestedCost()` 메서드가 캐시 미스 시 무엇을 반환하는가? 폴백이 `GoldCost(type, race)`라면 새 코드에서 그대로 사용해도 환불액이 "건설비의 50%"가 되므로 의도와 일치. 폴백이 0이면 GameBootstrapper에서 비생산 건물도 캐시를 채워줘야 함.
→ **이 확인은 Plan 단계에서 BuildingStats.cs 추가 확인 후 결정.** (Research 범위 외이므로 일단 보류, Plan에서 처리)

---

### 1-6. `Assets/_Project/Scripts/Infrastructure/Network/NetworkBuildingController.cs`

멀티플레이 철거 RPC. `RequestDemolishServerRpc(int buildingId)` 메서드가 이미 존재 (라인 467~546).

핵심 동작:
1. 건물 존재 확인.
2. **Castle이면 차단** (`if (building.Type == BuildingType.Castle)` 라인 504) — Castle 보호 로직이 이미 RPC에 박혀 있음.
3. 소유권 검증 (발신자 ClientId → 기대 팀 매핑).
4. **생산건물이면** `CancelAllQueue` 호출 (`if (BuildingTypeHelper.IsProductionBuilding(...))` 라인 522) — **비생산 건물은 이 분기를 건너뜀, 새 UI가 호출해도 정상 동작**.
5. `GetTotalInvestedCost / 2` 환불.
6. `DemolishBuilding(buildingId)` 호출.
7. `DemolishBuildingClientRpc(buildingId)` 전파.

→ **결론: 새 UI는 멀티플레이 분기에서 `_networkBuildingController.RequestDemolishServerRpc(buildingId)`를 호출하기만 하면 끝. 추가 RPC나 서버 분기는 필요 없음.**

---

## 2. 철거 로직 재사용 가능 여부 확인

### 2-1. `BuildingPlacementUseCase.DemolishBuilding(buildingId)`

(코드 위치: `Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs`)

ProductionPanelUI의 `OnDemolishButtonClick`이 이미 호출하는 메서드. **동작 내용은 BuildingType 종류와 무관 — Id만 받아서 처리**한다.

내부 동작 (메모리 기준):
1. `OnBuildingDied` 이벤트 발행 → `BuildingFactory`가 GO 파괴.
2. `RemoveBuilding(buildingId)` → 도메인 Dict에서 제거, 타일 IsWalkable 복구.

→ **MiningPost / AutoTower 등 비생산 건물에도 그대로 사용 가능.**

### 2-2. `NetworkBuildingController.RequestDemolishServerRpc(buildingId)`

위의 1-6에서 분석한 대로 **이미 모든 분기가 BuildingType에 따라 동작**한다. 새 UI에서 호출만 하면 됨.

### 2-3. `BuildingStats.GetTotalInvestedCost(type, race)`

**미확인 — Plan.md 작성 전 BuildingStats.cs를 한 번 더 읽어 폴백 값을 확정해야 함**. Plan에서 처리.

---

## 3. ClosedFrame 패턴이 왜 필요한가

InputHandler가 매 프레임 Update에서 `wasReleasedThisFrame`을 검사하는데, **팝업이 닫히는 순간(외부 영역 탭)에도 같은 프레임에서 클릭 이벤트가 InputHandler까지 도달**한다. 그러면:

1. SharedBackgroundButton이 팝업의 Close()를 호출.
2. 같은 프레임에 InputHandler가 동일 클릭 위치를 받음.
3. 팝업 뒤에 있던 타일/건물이 클릭된 것처럼 인식 → 새 팝업이 다시 열림 (의도하지 않은 동작).

방지법:
- 팝업의 `Close()`가 `ClosedFrame = Time.frameCount`를 기록.
- InputHandler가 `if (ClosedFrame == Time.frameCount) return;`로 무시.

→ 새 `BuildingActionPanelUI`도 **반드시 같은 패턴을 따라야 한다**. (Plan에 명시)

---

## 4. ProductionPanelUI 재사용 vs 새 컴포넌트 — 결정 근거

옵션 A — **ProductionPanelUI에 "비생산 모드" 플래그 추가**:
- 장점: 코드 한 곳에서 모든 건물 팝업 관리.
- 단점: 생산 전용 필드(_queueSlotImages, _progressFill, _goldText 등 약 15개)가 비생산 모드에서 모두 비활성화 처리 필요. 분기문 폭발. 책임 두 가지 혼재 (SRP 위반).

옵션 B — **새 `BuildingActionPanelUI` 컴포넌트 작성 (권장)**:
- 장점: 책임 명확. ProductionPanelUI는 손대지 않으니 회귀 위험 없음. 추후 비생산 건물 전용 기능(채굴 효율 표시, 타워 사정거리 표시 등)을 깨끗하게 확장 가능.
- 단점: 공통 로직(`Show`/`Close`/`IsOpen`/`ClosedFrame`/`OnDemolishButtonClick`/`UpdateDemolishRefund`) 일부 중복.
- 중복 완화 방법: 공용 base class 또는 인터페이스 추출 — **이번 작업 범위 외**. 일단 단순 중복 허용.

**결정**: 옵션 B(새 컴포넌트). 사용자가 작업 의뢰에서 명시한 "공용 팝업 UI" = 비생산 건물들 사이의 공용, 생산 팝업과는 별도라는 의미로 해석.

---

## 5. SetupProductionPopupUI.cs (Editor) — 새 UI에도 필요한가?

파일 존재 확인: `Assets/_Project/Scripts/Editor/SetupProductionPopupUI.cs` — 존재함.

역할 (메모리 기준): ProductionPanelUI 팝업의 자식 UI를 메뉴에서 자동 생성하고, BuildingType별 Sprite를 자동 매핑하는 에디터 스크립트.

새 UI에 필요한가? — **선택적**. ProductionPanelUI보다 훨씬 단순(헤더 + 철거 버튼만)하므로 수동 프리팹 작성도 무리 없음. 다만 정책 일관성 측면에서 다음 둘 중 하나는 결정 필요:

1. 새 에디터 스크립트 `SetupBuildingActionPanelUI.cs` 작성.
2. 수동으로 프리팹 작성 후 Inspector에서 직접 연결.

→ Plan.md에서 옵션을 명시하고 사용자가 선택하도록 한다.

---

## 6. 향후 확장 가능성 (참고용, 이번 범위 외)

- 채굴소: 채굴 효율, 일시 정지 토글, 업그레이드 등.
- 방어 타워: 사정거리 표시, 우선 타겟 정책, 업그레이드.
- 마법 건물: 스킬 활성화 버튼.

→ 새 컴포넌트로 분리해두면 위 기능을 BuildingType별 패널 변형으로 자연스럽게 확장 가능.

---

## 7. 위험 요소 (조사 단계에서 발견된 것)

| 위험 | 영향 | Plan에서 다룰 항목 |
|---|---|---|
| `BuildingStats.GetTotalInvestedCost`가 비생산 건물에 대해 폴백 값을 반환할 가능성 | 환불액이 0으로 표시될 수 있음 | Plan 작성 시 BuildingStats.cs 폴백 확인 필요 |
| `_uiManager.Register` 누락 시 게임 종료/재시작에서 팝업이 안 닫힘 | 다음 게임으로 팝업 끌고 들어감 | Plan에 등록 단계 명시 |
| InputHandler의 `ClosedFrame` 체크 누락 시 같은 프레임 재오픈 버그 | UX 깨짐 | Plan에 명시 |
| Castle 클릭 시 새 팝업이 뜨지 않도록 분기 조건 정확히 작성 | 본기지 철거 시도 가능성 | Plan의 InputHandler 분기에 명시 |
| 멀티플레이에서 `NetworkManager.Singleton.IsListening` 가드 누락 시 싱글플레이에서 RPC 호출 | NullRef | ProductionPanelUI 패턴 그대로 복사 |

---

## 8. 결론 — 무엇을 만들고 무엇을 손대야 하는가 (요약)

**새로 만들 것**:
- `Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs` (가칭) — 비생산 건물 공용 팝업.
- (선택) `Assets/_Project/Scripts/Editor/SetupBuildingActionPanelUI.cs` — 에디터 자동 연결 스크립트.

**수정할 것**:
- `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` — 비생산 건물(Castle 제외) 클릭 분기 추가, ClosedFrame 체크에 새 UI 포함, Initialize() 시그니처에 새 UI 인자 추가.
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` — SerializeField 추가, UIManager 등록, Initialize 호출, InputHandler 주입에 포함.
- (선택) `Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs` — `IsCastle` 또는 `CanShowActionPanel` 헬퍼 추가.

**손대지 않을 것**:
- `BuildingPlacementUseCase.DemolishBuilding` — 이미 BuildingType 무관 동작.
- `NetworkBuildingController.RequestDemolishServerRpc` — Castle 차단 / 생산건물 분기 모두 이미 처리됨.
- `BuildingStats.GetTotalInvestedCost` — Plan에서 폴백 확인 후 필요 시 GameBootstrapper 초기화에 추가만 함.
- `ProductionPanelUI` — 회귀 위험 차단.
