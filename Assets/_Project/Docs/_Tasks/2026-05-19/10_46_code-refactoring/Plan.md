# Plan: 코드 리팩토링

## 이 문서가 다루는 것

이 문서는 Research.md에서 식별한 문제점들을 **어떤 순서로, 어떻게 정리할 것인가**를 정한 작업 계획서입니다.

쉽게 말하면 이렇습니다.
- Research는 "지금 코드가 이런 상태다"를 사진처럼 찍어둔 보고서였습니다.
- Plan은 그 사진을 보고 "어디부터, 어떻게 손볼 건지" 순서와 방법을 정한 작업 지시서입니다.

이번 리팩토링의 핵심 목적은 다음 세 가지입니다.

1. **죽은 코드 청소** — 2026-05-11에 폐기 결정된 슬롯/점유 시스템 잔재가 약 600줄 남아 있습니다. 이걸 한 번에 깔끔하게 제거합니다.
2. **아키텍처 약속 회복** — Clean Architecture 규칙(Domain은 Core 모름, Application은 NGO 모름 등)을 어긴 자리가 12군데 이상 있습니다. 인터페이스 추출/이벤트 추출로 약속을 회복합니다.
3. **반복 호출/탐색 최적화** — `FindFirstObjectByType<GameBootstrapper>()`가 무려 30회 이상, 위치 기반 O(n) 탐색이 4개 이상 흩어져 있습니다. 캐시/역인덱스 도입으로 매 프레임 비용을 줄입니다.

작업은 6개 독립 그룹으로 나누었고, **위쪽 그룹일수록 위험이 낮고 효과가 큽니다.** 그룹 간 의존이 없으므로 위에서부터 한 그룹씩 끝낼 때마다 컴파일/실행 검증이 가능합니다.

> 본 Plan은 **계획 문서**이며 어떤 코드도 직접 수정하지 않았습니다. 각 그룹 실행 단계에서 별도 task 문서를 작성하거나 본 Plan을 기반으로 분할 작업을 진행합니다.

---

## ⚠️ 기존 로직 제거 항목 (최상단 명시)

본 리팩토링에서 **완전 삭제 예정**인 파일/필드/메서드 목록입니다. 제거 전 마지막 검토 대상입니다.

### 1. 파일 단위 삭제

| 파일 | 제거 근거 |
|------|-----------|
| `Assets/_Project/Scripts/Application/Services/AttackPositionManager.cs` | GameSystemRules.md "재설계(2026-05-11)" — 공격 슬롯 시스템 전면 폐기. GameBootstrapper.GetAttackPositionManager()가 항상 null 반환. 외부 호출자 0건(Grep 검증 필요). |
| `Assets/_Project/Scripts/Application/Services/TileOccupancyManager.cs` | 동일 — 타일 점유 한도 시스템 폐기. UnitMovementUseCase 생성자에서 명시적으로 null 전달. 인스턴스가 생성되지 않음. |
| `Assets/_Project/Scripts/Presentation/UI/ProductionPopupDiagnostic.cs` | 헤더 주석 "1회성 진단 도구" 명시. ProductionPopup 레이아웃 분석 완료 후 제거 예정이었으나 잔존. 외부 참조 0건(Grep 검증 필요). |

### 2. 필드/메서드 단위 삭제

| 위치 | 항목 | 제거 근거 |
|------|------|-----------|
| `UnitMovementUseCase.cs` 45 | `_occupancyManager` 필드 | 항상 null. GameSystemRules.md "재설계(2026-05-11)" 슬롯 폐기. |
| `UnitMovementUseCase.cs` 49 | `_subscriptions` (CompositeDisposable) | 구독 추가 0건. Dispose 시 null-safe 처리만 남음. |
| `UnitMovementUseCase.cs` 37,56 | `_unitSpawn` 필드 + 생성자 인자 | 주석 "현 시점에서는 사용처 없음" 명시. 본문 미참조. |
| `UnitMovementUseCase.cs` 210~216 | `FindForwardAvailable(...)` | 본문 "항상 preferred 반환" 한 줄. 호출자 0건. |
| `UnitMovementUseCase.cs` 53 | 생성자 4번째 인자 `TileOccupancyManager occupancyManager = null` | 클래스 삭제와 동시에 시그니처 정리. |
| `UnitCombatUseCase.cs` 45 | `MeleeDetectDist` 주석 블록 | 통합 detect 사거리 도입 완료. 보존 가치 없음. |
| `GameBootstrapper.cs` 142~152 | `_slotForwardRatio`, `_slotSideRatio` 주석 블록 | 이미 인스펙터 노출 제거. 주석만 정리. |
| `GameBootstrapper.cs` 254~266 | `_moveSlotManager`, `_attackPositionManager`, `_occupancyManager` 주석 블록 | 동일. |
| `GameBootstrapper.cs` 303~311 | `GetAttackPositionManager()`, `GetTileOccupancyManager()` 메서드 | 항상 null 반환. 외부 호출자 0건이면 시그니처 자체 삭제. |
| `HexFlowField.cs` 223,233 | `destTileCheck` 지역 변수 + `_ = destTileCheck;` | 의미 없는 자리표시. |

### 3. 보존 항목 (삭제 금지 — 의도적)

| 위치 | 항목 | 보존 근거 |
|------|------|-----------|
| `Domain/Unit/UnitStats.cs` | `AttackKind` enum + `GetAttackKind()` + `StatValues.Kind` | 향후 UI 분류용 보존 명시. UnitStatsConfig Inspector 호환. 단, Application 레이어 분기에서는 호출 안 함. |
| `Domain/Building/BuildingStats.cs` | `GetAttackPower()`, `GetAttackCooldown()` | 향후 타워 공격 구현용 보존. Bootstrap 주입 흐름은 유지. |

### 4. 안전 확인 절차 (제거 직전 필수)

각 파일 삭제 직전 반드시 아래 순서로 검증:

1. `Grep "AttackPositionManager"` — Application/Services 외 0건 확인
2. `Grep "TileOccupancyManager"` — UnitMovementUseCase 생성자 외 0건 확인
3. `Grep "GetAttackPositionManager"` / `Grep "GetTileOccupancyManager"` — GameBootstrapper 정의 외 0건 확인
4. `Grep "ProductionPopupDiagnostic"` — 본인 외 0건 확인
5. `Grep "FindForwardAvailable"` — 정의 외 0건 확인
6. 모든 검증 후 한 PR로 일괄 제거 (분할 시 컴파일 깨질 위험)

---

## 작업 그룹 1: 슬롯/점유 시스템 잔재 일괄 제거

**GameSystemRules 근거:** GameSystemRules.md "재설계(2026-05-11)" — 이동 슬롯, 공격 슬롯, 타일 점유 한도 모두 폐기. 겹침 허용하는 단순 구조로 전환.

**작업 범위:** Application/Services 2개 파일, Application/UseCases 1개 파일, Bootstrap 1개 파일, Domain/Hex 1개 파일, Presentation/UI 1개 파일.

**접근 방법:**
1. **단일 PR로 일괄 제거** — 분할 시 컴파일 깨짐. 파일 단위 삭제 + 필드 단위 삭제 + 주석 블록 정리를 한 번에 수행.
2. 위 "안전 확인 절차" 6단계를 PR 작성 직전 실행하여 외부 참조 0건 보장.
3. `UnitMovementUseCase` 생성자 시그니처가 줄어드는 변경 → GameBootstrapper의 인스턴스 생성 라인도 함께 수정.
4. 삭제된 `.cs` 파일의 `.meta` 파일도 동시 삭제 (Unity 메타파일 누수 방지).

**위험 요소:**
- `UnitMovementUseCase` 생성자 인자 4개 → 3개로 변경 시 호출처 누락 가능성. GameBootstrapper만 호출하므로 위험은 낮으나 Grep으로 한 번 더 확인.
- `.meta` 파일 삭제 누락 시 Unity가 import 오류 표시. PR 직전 Unity Editor에서 확인.
- 슬롯 시스템 관련 주석이 워낙 광범위 → 잔여 주석 검색 누락 가능. `Grep -i "슬롯\|점유\|2026-05-11 비활성화"`로 잔여 주석 식별.

### 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Application/Services/AttackPositionManager.cs` (+ .meta) | 파일 전체 | 삭제 |
| `Application/Services/TileOccupancyManager.cs` (+ .meta) | 파일 전체 | 삭제 |
| `Presentation/UI/ProductionPopupDiagnostic.cs` (+ .meta) | 파일 전체 | 삭제 |
| `Application/UseCases/UnitMovementUseCase.cs` | `_occupancyManager`, `_subscriptions`, `_unitSpawn`, `FindForwardAvailable`, 생성자 4번째 인자, using Hexiege.Core (그룹 2에서 처리) | 삭제 + 생성자 시그니처 수정 |
| `Application/UseCases/UnitCombatUseCase.cs` | `MeleeDetectDist` 주석 블록 (라인 39~46) | 삭제 |
| `Bootstrap/GameBootstrapper.cs` | `_slotForwardRatio`/`_slotSideRatio` 주석 (142~152), `_moveSlotManager`/`_attackPositionManager`/`_occupancyManager` 주석 (254~266), `GetAttackPositionManager()`/`GetTileOccupancyManager()` 메서드 (303~311), `UnitMovementUseCase` 인스턴스 생성 라인의 4번째 인자 | 삭제 + 인스턴스 생성 수정 |
| `Domain/Hex/HexFlowField.cs` | `destTileCheck` 자리표시 (라인 223, 233) | 삭제 |

**예상 감축 분량:** 약 500~700줄.

---

## 작업 그룹 2: Application → Core 의존 제거 (3건 — UseCase 한정)

> **⚠️ 결정 사항**: TileOwnershipService는 복잡도가 달라 **그룹 2-B로 별도 진행**. 이 그룹은 UseCase 3건만 처리.

**근거(아키텍처 원칙):** CLAUDE.md / 프로젝트 MEMORY.md "Architecture Rules — Application은 Domain만 의존". `using Hexiege.Core`를 통해 HexMetrics를 직접 호출하는 UseCase 3건이 위반.

**작업 범위:**
- `Application/UseCases/UnitCombatUseCase.cs` (라인 17)
- `Application/UseCases/UnitMovementUseCase.cs` (라인 27)
- `Application/UseCases/GridInteractionUseCase.cs` (라인 25)

**접근 방법:**

1. **인터페이스 추출 패턴**
   - Application 레이어에 `IHexCoordinateMapper` 신규 인터페이스 정의 (Application 권장 — Domain은 좌표를 모를수록 좋음).
   - 메서드: `HexCoord WorldToHex(Vector3 worldPos)`, `Vector3 HexToWorld(HexCoord coord)`, `float TileHeight { get; }` — 사용처에서 필요한 최소 API만.
   - Core 레이어에 `HexMetricsCoordinateMapper : IHexCoordinateMapper` 구현체 작성. 기존 HexMetrics 호출 래핑.
   - GameBootstrapper가 구현체를 생성하여 3개 UseCase 생성자에 주입.

2. **UnityEngine.Vector3 의존**
   - `using UnityEngine`은 허용 (이미 사용 중). 핵심은 `using Hexiege.Core`만 제거.
   - 인터페이스 메서드에 Vector3를 그대로 노출해도 무방.

3. **HexMetrics 호출 매핑** (실행 시 Grep으로 재검증)
   - `UnitCombatUseCase`: `HexMetrics.TileHeight`, `HexMetrics.WorldToHex`
   - `UnitMovementUseCase`: `HexMetrics`, `ViewConverter` 사용 확인
   - `GridInteractionUseCase`: `HexMetrics.WorldToHex` (라인 57)

**발생 가능한 문제 및 해결 방법:**

| 문제 | 해결 방법 |
|------|-----------|
| `IHexCoordinateMapper`에 없는 메서드를 UseCase가 호출하고 있을 경우 컴파일 에러 | 실행 전 Grep으로 3개 파일의 HexMetrics.* 호출 전수 조사 → 인터페이스에 모두 포함 |
| `IEntityPositionProvider`와 역할이 겹쳐 보일 수 있음 | `IEntityPositionProvider`는 "유닛 위치 반환", `IHexCoordinateMapper`는 "좌표 변환" — 책임 범위가 다름. 파일 헤더 주석에 명시 |
| GameBootstrapper UseCase 생성 라인이 일괄 수정되어 실수 가능성 | 인터페이스 추출 + GameBootstrapper 수정을 한 커밋으로 묶고, 컴파일 후 싱글플레이 기본 동작 검증 |

**위험 요소:**
- GameBootstrapper의 UseCase 생성 라인이 모두 영향 받음 → 인스펙터 직렬화는 영향 없으나 런타임 생성 코드 수정.
- `HexMetricsCoordinateMapper` 인스턴스는 그룹 2-B에서도 공유 사용 → 두 그룹 실행 순서상 그룹 2 먼저 완료 필요.

### 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Application/Abstractions/IHexCoordinateMapper.cs` (신규) | 인터페이스 정의 (WorldToHex / HexToWorld / TileHeight) | 추가 |
| `Core/Hex/HexMetricsCoordinateMapper.cs` (신규) | `IHexCoordinateMapper` 구현체 — HexMetrics 호출 래핑 | 추가 |
| `Application/UseCases/UnitCombatUseCase.cs` | `using Hexiege.Core` 제거, `IHexCoordinateMapper` 생성자 주입, `HexMetrics.*` 호출 → mapper 호출로 교체 | 수정 |
| `Application/UseCases/UnitMovementUseCase.cs` | 동상 | 수정 |
| `Application/UseCases/GridInteractionUseCase.cs` | 동상 | 수정 |
| `Bootstrap/GameBootstrapper.cs` | `HexMetricsCoordinateMapper` 인스턴스 생성 + 3개 UseCase 생성자에 주입 | 수정 |

---

## 작업 그룹 2-B: TileOwnershipService Core 의존 제거 (독립 진행)

> **⚠️ 결정 사항**: 그룹 2(UseCase 3건)와 별도 진행. 이유: ViewConverter.FromView()가 단순 수학 변환이 아닌 "Red 팀 좌표 반전" 게임 규칙을 포함하여 인터페이스 설계가 다름. 문제 발생 시 그룹 2에 영향 없이 독립적으로 롤백 가능.

**근거(아키텍처 원칙):** CLAUDE.md / 프로젝트 MEMORY.md "Application은 Domain만 의존". TileOwnershipService.cs 라인 37 `using Hexiege.Core`, 라인 139~142에서 직접 호출.

**정책 결정:** (A) 원칙 적용 — 예외 없이 인터페이스 의존으로 전환. 헤더 주석의 "Core 참조 가능" 예외 선언 삭제.

**TileOwnershipService가 Core에서 쓰는 것 (코드 직접 확인 결과):**

| 라인 | 호출 | 역할 |
|------|------|------|
| 139 | `ViewConverter.FromView(viewPos)` | Red 팀이면 좌표를 반전 → 도메인 기준 좌표로 정규화 |
| 142 | `HexMetrics.WorldToHex(domainPos)` | 월드 좌표 → 헥스 좌표 변환 |

**접근 방법:**

1. **그룹 2에서 생성한 `IHexCoordinateMapper`를 확장**
   - `Vector3 NormalizeToDomainPosition(Vector3 viewPos)` 메서드를 인터페이스에 추가
   - `HexMetricsCoordinateMapper` 구현체에서 내부적으로 `ViewConverter.FromView()` 호출
   - TileOwnershipService는 "도메인 좌표로 정규화해달라"고 요청만 하고, Red 팀 반전 여부는 신경 쓰지 않음

2. **생성자 인자 추가**
   - 현재 TileOwnershipService 생성자: `(HexGrid, UnitSpawnUseCase, IEntityPositionProvider)`
   - 변경 후: `(HexGrid, UnitSpawnUseCase, IEntityPositionProvider, IHexCoordinateMapper)`
   - GameBootstrapper에서 그룹 2에서 만든 동일 `HexMetricsCoordinateMapper` 인스턴스 재사용

3. **헤더 주석 수정**
   - "Application 레이어 — Domain/Core 참조 가능" 문구 삭제
   - "Application 레이어 — IHexCoordinateMapper 인터페이스를 통해 좌표 변환" 으로 교체

**발생 가능한 문제 및 해결 방법:**

| 문제 | 해결 방법 |
|------|-----------|
| `ViewConverter.FromView()`의 Red 팀 반전 로직이 인터페이스 뒤로 숨어 "왜 이렇게 동작하는가"가 불분명해질 수 있음 | `HexMetricsCoordinateMapper.NormalizeToDomainPosition()` 주석에 "Red 팀이면 X축 반전(ViewConverter.FromView 위임)"을 명시 |
| `IHexCoordinateMapper`에 `NormalizeToDomainPosition` 추가 시 그룹 2에서 이미 구현한 UseCase 3개도 재컴파일 필요 | 인터페이스에 메서드를 추가해도 기존 UseCase는 이 메서드를 호출하지 않으므로 영향 없음. 컴파일만 확인하면 됨 |
| TileOwnershipService가 매 프레임(Update) 호출되는 서비스라 생성자 인자 추가가 런타임에 문제가 될 수 있다는 우려 | 생성자 변경은 GameBootstrapper.Start() 시점 1회만 영향. Tick() 본문은 변경 없음 |
| 그룹 2 완료 전에 그룹 2-B를 진행하면 IHexCoordinateMapper가 아직 없어 컴파일 에러 | **반드시 그룹 2 완료 + 컴파일 확인 후에만 그룹 2-B 진행** |

**위험 요소:**
- `ViewConverter.FromView()`의 반전 로직이 실제 점령 판정에 직접 영향 → 잘못 구현하면 Red 팀 점령이 반대로 판정될 수 있음. 구현 후 **Red 팀 유닛이 이동할 때 타일 소유권 색상이 정상적으로 바뀌는지 반드시 확인**.
- 그룹 2 이후에만 진행 가능 (의존 관계 존재).

### 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Application/Abstractions/IHexCoordinateMapper.cs` | `NormalizeToDomainPosition(Vector3 viewPos)` 메서드 추가 | 수정 |
| `Core/Hex/HexMetricsCoordinateMapper.cs` | `NormalizeToDomainPosition()` 구현 추가 — `ViewConverter.FromView()` 래핑 | 수정 |
| `Application/Services/TileOwnershipService.cs` | `using Hexiege.Core` 제거, `IHexCoordinateMapper` 생성자 주입, 라인 139/142 교체, 헤더 주석 수정 | 수정 |
| `Bootstrap/GameBootstrapper.cs` | TileOwnershipService 생성 라인에 4번째 인자(`_hexMapper`) 추가 | 수정 |

---

## 작업 그룹 3: 레이어 간 직접 의존 제거 (11건)

**근거(아키텍처 원칙):** CLAUDE.md / 프로젝트 MEMORY.md — "레이어는 정해진 방향으로만 의존". 두 방향의 위반이 존재:
- **Presentation → NGO 직접 의존**: `using Unity.Netcode`가 Presentation 레이어 9개 파일에 산재
- **Infrastructure → Presentation 역방향 의존**: Infrastructure가 UI를 직접 참조·호출 (2개 파일, 코드 확인 완료)

**작업 범위:** (코드 직접 확인 결과 11개 파일)
- `Presentation/Production/ProductionTicker.cs` (라인 39)
- `Presentation/UI/BuildingPanelBase.cs` (라인 41)
- `Presentation/UI/BuildingPlacementUI.cs` (라인 31)
- `Presentation/UI/GameEndUI.cs` (라인 32)
- `Presentation/UI/GameHudUI.cs` (라인 26)
- `Presentation/UI/LobbyUI.cs` (라인 37)
- `Presentation/UI/NetworkStatusUI.cs` (라인 29, 30 — Transports.UTP 포함)
- `Presentation/UI/ProductionPanelUI.cs` (라인 27)
- `Presentation/UI/InGameSettingsUI.cs` (라인 211 FindFirstObjectByType 호출도 함께 정리)
- `Infrastructure/Network/NetworkCombatController.cs` (라인 34 — UnitView 직접 제어)
- `Infrastructure/Network/NetworkGameEndController.cs` (라인 44 — GameEndUI/RematchPopup/GameUIManager 직접 제어)

**접근 방법:**

각 파일별로 다른 패턴이 필요 — 5가지 카테고리로 분류:

### 카테고리 A: 단순히 NetworkContext만 참조하면 되는 경우
- **대상**: `GameHudUI`, `GameEndUI`, `ProductionTicker`
- **접근**: `using Unity.Netcode` 제거 → 멀티/싱글 분기는 이미 만들어진 `NetworkContext.IsNetworkActive` / `IsNetworkServer` 사용으로 대체.
- **⚠️ BuildingPanelBase, BuildingPlacementUI, ProductionPanelUI는 카테고리 D** (코드 직접 확인 결과 — ServerRpc 직접 호출 존재)

### 카테고리 B: NetworkBehaviour 인스턴스를 직접 잡아야 하는 경우
- **대상**: `LobbyUI`, `InGameSettingsUI`
- **접근**: NGO를 통한 호출이 필요하지만 UI가 직접 NetworkManager를 만질 필요 없음 — GameBootstrapper 또는 NetworkLifecycle 컴포넌트가 추상화한 메서드 호출로 전환.
- `LobbyUI` (라인 37,95 `FindFirstObjectByType<NetworkGameManager>()`) → Initialize 시점에 인스턴스 주입.
- `InGameSettingsUI` (라인 211 `FindFirstObjectByType<NetworkGameEndController>()`) → Initialize 시점에 인스턴스 주입 또는 IForfeitService 인터페이스 추상화.

### 카테고리 C: 디버그/표시 전용 (UTP 직접 의존)
- **대상**: `NetworkStatusUI`
- **접근**: 헤더 주석에 "로컬 표시 전용"이라고 명시되어 있으나 NGO API를 직접 호출. NetworkContext에 표시용 메타데이터(`ConnectionStateText`, `LocalAddress` 등) 추가 또는 NetworkLifecycleSnapshot 데이터 객체로 노출.

### 카테고리 D: ServerRpc 직접 호출
- **대상**: `BuildingPanelBase`, `ProductionPanelUI`, `BuildingPlacementUI`
- **접근**: 각 UI 파일에서 직접 호출하는 ServerRpc를 Infrastructure 레이어 컨트롤러의 일반 래퍼 메서드로 교체. UI는 "어떤 RPC를 써야 하는지" 알 필요 없음.

#### BuildingPanelBase (코드 직접 확인 결과)
- `OnDemolishButtonClick()` 라인 258~260: `_networkBuildingController != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening` 분기
  → `_networkBuildingController != null && NetworkContext.IsNetworkActive`로 교체
- `OnDemolishButtonClick()` 라인 265: `_networkBuildingController.RequestDemolishServerRpc(_currentBuilding.Id)` 직접 호출
  → `NetworkBuildingController`에 `RequestDemolish(int buildingId)` 래퍼 메서드 추가 후 교체
- **이 클래스가 상위 클래스이므로 ProductionPanelUI 작업 전에 먼저 처리 권장**

#### BuildingPlacementUI (코드 직접 확인 결과)
- `PlaceAndClose()` 라인 471~472: `NetworkManager.Singleton != null && (IsHost || IsClient)` 분기
  → `NetworkContext.IsNetworkActive`로 교체
- `PlaceAndClose()` 라인 490: `_networkBuildingController.RequestBuildServerRpc(...)` 직접 호출
  → `NetworkBuildingController`에 `RequestBuild(BuildingType type, TeamId team, int Q, int R)` 래퍼 메서드 추가 후 교체
- `_networkBuildingController`는 `Initialize()`에서 이미 주입받아 필드로 보관 중이므로 주입 구조 변경 불필요

#### ProductionPanelUI (코드 직접 확인 결과)
- `using Unity.Netcode` 제거 (라인 27)
- NetworkManager.Singleton 체크 5개소 (라인 386, 429, 501, 512, 595~597) → `NetworkContext.IsNetworkActive` 또는 `_networkProductionController != null` 분기로 교체
- 직접 ServerRpc 호출 5건 → NetworkProductionController(또는 NetworkBuildingController) 래퍼 메서드로 교체:

  | 현재 호출 (라인) | 교체할 래퍼 메서드 | 담당 컨트롤러 |
  |---|---|---|
  | `CancelSlotServerRpc(buildingId, slotIndex, ...)` (388) | `RequestCancelSlot(int buildingId, int slotIndex)` | NetworkProductionController |
  | `RequestEnqueueServerRpc(buildingId, (int)type, ...)` (430) | `RequestEnqueue(int buildingId, UnitType type)` | NetworkProductionController |
  | `ToggleAutoServerRpc(buildingId, (int)type, ...)` (502) | `RequestToggleAuto(int buildingId, UnitType type)` | NetworkProductionController |
  | `SetRallyPointServerRpc(buildingId, Q, R, ...)` (513) | `RequestSetRallyPoint(int buildingId, int q, int r)` | NetworkProductionController |
  | `RequestUpgradeServerRpc(buildingId)` (602) | `RequestUpgrade(int buildingId)` | NetworkBuildingController (Grep 확인 완료 — `_networkBuildingController` 상속 필드 사용) |

- **주의**: `RequestDemolishServerRpc`는 상위 클래스 BuildingPanelBase에서 호출 — 위 BuildingPanelBase 항목에서 함께 해소. ProductionPanelUI는 오버라이드 없음.

### 카테고리 E: Infrastructure → Presentation 역방향 의존 (GameEvents 이벤트 발행으로 전환)

- **대상**: `NetworkCombatController`, `NetworkGameEndController`
- **핵심 접근**: UI를 직접 호출하는 대신 **GameEvents에 이벤트를 발행**. UI 쪽에서 이벤트를 구독해 스스로 반응. 싱글플레이와 동일한 흐름으로 통일.

#### NetworkCombatController (코드 직접 확인 결과)

현재 ClientRpc 핸들러 안에서 `UnitView`를 GetComponent로 꺼내 애니메이션 메서드를 직접 호출. → **신규 GameEvents 이벤트 발행으로 교체**, UnitView가 이벤트를 구독해 스스로 처리.

| 현재 직접 호출 | 교체할 GameEvents 이벤트 (신규 추가) | UnitView 구독 처리 |
|---|---|---|
| `unitView.StartCombatAnimation(targetId, isUnit)` | `GameEvents.OnNetworkCombatStarted.OnNext(unitId, targetId, isUnit)` | `StartCombatAnimation(...)` 호출 |
| `unitView.ChangeTarget(newTargetId, isUnit)` | `GameEvents.OnNetworkCombatTargetChanged.OnNext(unitId, targetId, isUnit)` | `ChangeTarget(...)` 호출 |
| `unitView.StopCombatAnimation()` | `GameEvents.OnNetworkCombatStopped.OnNext(unitId)` | `StopCombatAnimation()` 호출 |
| `unitView.StartWalkAnimation()` | `GameEvents.OnNetworkWalkStarted.OnNext(unitId)` | `StartWalkAnimation()` 호출 |

- `using Hexiege.Presentation` 제거. `UnitView` GetComponent 코드 모두 제거.
- UnitView에 구독 추가 (OnNetworkSpawn) + 구독 해제 (OnNetworkDespawn / OnDestroy)

#### NetworkGameEndController (코드 직접 확인 결과)

현재 `AnnounceWinnerClientRpc` 안에서 `GameEndUI.ShowResult()`, `SetupRematchButton()`, `GameUIManager.NotifyGameEnded()` 직접 호출. → **기존 GameEvents.OnGameEnd 재사용 + 신규 이벤트 추가**로 UI 직접 참조 제거.

| 현재 직접 호출 | 교체 방법 |
|---|---|
| `_gameEndUI.ShowResult(winnerTeam, LocalPlayerTeam.Current)` | `GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam))` 발행 — GameEndUI 기존 구독이 자동 반응 |
| `_uiManager.NotifyGameEnded()` | 동일 `GameEvents.OnGameEnd` 구독으로 GameUIManager도 처리 |
| `_gameEndUI.SetupRematchButton(isRandom, RequestRematch)` | `GameEvents.OnNetworkRematchAvailable.OnNext(isRandom)` 신규 이벤트 발행 — GameEndUI가 구독해 버튼 설정. 콜백 대신 `GameEvents.OnRematchRequested` 이벤트를 발행하고 NetworkGameEndController가 구독 |
| `_rematchRequestPopup.ShowRequest(...)` | `GameEvents.OnNetworkRematchRequested.OnNext(...)` 발행 — 팝업이 구독해 표시 |
| `_rematchRequestPopup.ShowDeclined()` | `GameEvents.OnNetworkRematchDeclined.OnNext(...)` 발행 — 팝업이 구독해 거절 상태 표시 |
| `_gameEndUI.RestoreRematchButton()` | `GameEvents.OnNetworkRematchDeclined` 동일 이벤트 구독 |

- `[SerializeField] private GameEndUI _gameEndUI`, `RematchRequestPopup`, `GameUIManager` 필드 모두 제거
- `using Hexiege.Presentation` 제거
- FindFirstObjectByType<GameEndUI/RematchRequestPopup/GameUIManager> 호출 모두 제거

**위험 요소:**
- 11개 파일을 한 PR로 정리하면 변경 범위가 매우 큼 → **카테고리별 별도 PR 권장 (A→D→B→C→E 순)**. 카테고리 D 내부 순서: BuildingPanelBase 먼저 → BuildingPlacementUI / ProductionPanelUI.
- `BuildingPanelBase`가 추상 클래스라 변경 시 두 자식 클래스(ProductionPanelUI, BuildingActionPanelUI)에 모두 영향 — BuildingPanelBase 변경 직후 두 자식이 모두 컴파일되는지 확인.
- `RequestUpgradeServerRpc`는 NetworkBuildingController에 있음 (Grep 확인 완료) — ProductionPanelUI가 상속 필드 `_networkBuildingController`를 사용하므로 래퍼 메서드는 NetworkBuildingController에 추가.
- NetworkXxxController가 NetworkBehaviour라서 인터페이스 추출 시 Mock 부담 — 일반 C# 인터페이스로 만들고 NetworkBehaviour는 구현체로 두면 해결.
- `ProductionPanelUI`의 ServerRpc 호출이 RequiresAuthority 등 NGO 속성을 직접 참조하는 경우 — 컨트롤러 메서드 시그니처에서 흡수.
- 카테고리 B의 `FindFirstObjectByType` 제거는 그룹 4와 겹침 — 그룹 3에서 함께 정리하면 그룹 4 작업량 감소.
- **카테고리 E 이벤트 구독 해제 누락 위험** — UnitView가 신규 이벤트를 구독할 때 OnNetworkDespawn/OnDestroy에서 반드시 구독 해제. 누락 시 재경기 이후 이전 UnitView 인스턴스의 구독이 남아 중복 애니메이션 처리 발생.
- **카테고리 E 재경기 콜백 이벤트화** — `RequestRematch` 메서드가 NetworkGameEndController 내부 콜백이었으므로, GameEvents 이벤트로 교체 시 GameEndUI의 버튼 클릭 → `GameEvents.OnLocalRematchRequested` 발행 → NetworkGameEndController 구독 흐름을 명확히 설계해야 함.

### 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Presentation/UI/GameHudUI.cs` | `using Unity.Netcode` 제거 + NetworkContext 사용 | 수정 (카테고리 A) |
| `Presentation/UI/GameEndUI.cs` | 동상 | 수정 (카테고리 A) |
| `Presentation/Production/ProductionTicker.cs` | `using Unity.Netcode` 제거 + NetworkContext 분기로 전환 | 수정 (카테고리 A) |
| `Presentation/UI/BuildingPanelBase.cs` | `using Unity.Netcode` 제거 + `NetworkManager.Singleton` 체크 → `NetworkContext.IsNetworkActive`로 교체 + `RequestDemolishServerRpc` → `RequestDemolish()` 래퍼 메서드 교체 | 수정 (카테고리 D) |
| `Presentation/UI/BuildingPlacementUI.cs` | `using Unity.Netcode` 제거 + `NetworkManager.Singleton` 체크 → `NetworkContext.IsNetworkActive` 교체 + `RequestBuildServerRpc` → `NetworkBuildingController.RequestBuild(...)` 래퍼 메서드 교체 | 수정 (카테고리 D) |
| `Presentation/UI/ProductionPanelUI.cs` | `using Unity.Netcode` 제거 + NetworkManager.Singleton 체크 5개소 교체 + ServerRpc 직접 호출 5건(CancelSlot/RequestEnqueue/ToggleAuto/SetRallyPoint/RequestUpgrade) → NetworkProductionController 래퍼 메서드로 교체 | 수정 (카테고리 D) |
| `Infrastructure/Network/NetworkBuildingController.cs` | UI에서 호출할 래퍼 메서드 2개 추가: `RequestBuild(BuildingType, TeamId, int Q, int R)`, `RequestDemolish(int buildingId)` | 수정 (메서드 추가) |
| `Infrastructure/Network/NetworkProductionController.cs` | UI에서 호출할 래퍼 메서드 추가: `RequestCancelSlot`, `RequestEnqueue`, `RequestToggleAuto`, `RequestSetRallyPoint`, `RequestUpgrade`(또는 빌딩 컨트롤러 확인 후 분배) | 수정 (메서드 추가) |
| `Presentation/UI/LobbyUI.cs` | `using Unity.Netcode` 제거 + `FindFirstObjectByType<NetworkGameManager>()` 2회 제거 + Initialize 주입 | 수정 (카테고리 B) |
| `Presentation/UI/InGameSettingsUI.cs` | `using Unity.Netcode` 제거 (있다면) + `FindFirstObjectByType<NetworkGameEndController>()` 제거 + IForfeitService 주입 | 수정 (카테고리 B) |
| `Presentation/UI/NetworkStatusUI.cs` | `using Unity.Netcode`, `using Unity.Netcode.Transports.UTP` 제거 + NetworkLifecycleSnapshot 사용 | 수정 (카테고리 C) |
| `Infrastructure/Network/NetworkCombatController.cs` | `using Hexiege.Presentation` 제거 + UnitView 직접 호출 → GameEvents 이벤트 발행으로 교체 | 수정 (카테고리 E) |
| `Infrastructure/Network/NetworkGameEndController.cs` | `using Hexiege.Presentation` 제거 + GameEndUI/RematchPopup/GameUIManager 직접 호출 → GameEvents 이벤트 발행으로 교체 + IForfeitService 인터페이스 구현 + `[SerializeField]` UI 필드 전체 제거 | 수정 (카테고리 B+E) |
| `Application/Events/GameEvents.cs` | 신규 이벤트 추가 — OnNetworkCombatStarted/TargetChanged/Stopped, OnNetworkWalkStarted, OnNetworkRematchAvailable, OnNetworkRematchRequested, OnNetworkRematchDeclined, OnLocalRematchRequested | 수정 (이벤트 추가) |
| `Presentation/View/UnitView.cs` | 신규 GameEvents 이벤트 구독 추가 (OnNetworkCombatStarted 등 4개) + OnNetworkDespawn/OnDestroy에서 구독 해제 | 수정 (카테고리 E) |
| `Presentation/UI/GameEndUI.cs` | GameEvents.OnGameEnd 구독 보강(멀티플레이 흐름 통일) + OnNetworkRematchAvailable 구독 + OnNetworkRematchDeclined 구독 + RestoreRematchButton 이벤트 구독 처리 | 수정 (카테고리 E) |
| `Presentation/UI/RematchRequestPopup.cs` | OnNetworkRematchRequested 구독 → ShowRequest() + OnNetworkRematchDeclined 구독 → ShowDeclined() | 수정 (카테고리 E) |
| `Presentation/UI/GameUIManager.cs` | GameEvents.OnGameEnd 구독 추가 → NotifyGameEnded() 자동 처리 (NetworkGameEndController 직접 호출 대체) | 수정 (카테고리 E) |
| `Application/Abstractions/IForfeitService.cs` (신규) | `void RequestForfeit()` 인터페이스 | 추가 |

---

## 작업 그룹 4: FindFirstObjectByType 30+회 캐시화

**근거(아키텍처 원칙):** Unity 모바일 성능 가이드라인 — `FindFirstObjectByType`은 씬 전체를 순회하는 O(n) 호출. NetworkBehaviour의 OnNetworkSpawn에서 1회 캐시한 뒤 재사용해야 함.

**작업 범위:** Grep으로 확인된 실제 30회 호출 위치 (Infrastructure/Network 폴더 9개 파일):

| 파일 | 호출 라인 수 | 비고 |
|------|--------------|------|
| `NetworkBuildingController.cs` | 7회 (54, 93, 198, 310, 411, 475, 566) | 이미 `_bootstrapper` 캐시 필드 존재 — "null이면 재탐색" 패턴 |
| `NetworkCombatController.cs` | 4회 (106, 224, 432, 554) | 동상 |
| `NetworkProductionController.cs` | 8회 (69, 265, 398, 502, 568, 650, 720, 771) | 동상 |
| `NetworkHealthSync.cs` | 2회 (63, 143) | 동상 |
| `NetworkGameFlow.cs` | 2회 (78, 181) | 동상 |
| `NetworkResourceSync.cs` | 2회 (88, 217) | 동상 |
| `NetworkTileSync.cs` | 2회 (72, 166) | 동상 |
| `NetworkUnit.cs` | 1회 (178) | 지역 변수로 호출 |
| `NetworkUnitMovementController.cs` | 2회 (58, 126) | 동상 |
| `NetworkGameEndController.cs` | 다수 (98, 105, 110, 153, 182, 333) | 그룹 3에서 함께 처리 권장 |

**합계: 30회 (이 기준은 Grep 결과)**

**접근 방법:**

1. **`OnNetworkSpawn` 단일 캐시 패턴 통일**
   - 각 NetworkBehaviour에 `private GameBootstrapper _bootstrapper;` 필드 (이미 대부분 존재)
   - `OnNetworkSpawn()` 시작점에서 `_bootstrapper = FindFirstObjectByType<GameBootstrapper>();` 1회 호출
   - 다른 메서드의 보호적 재탐색 라인(`if (_bootstrapper == null) _bootstrapper = ...`) 모두 제거

2. **null 가드는 유지**
   - `_bootstrapper`가 null이면 메서드는 빠르게 return (NetworkObject 비활성 상황 대비)
   - 다만 재탐색은 하지 않음 — OnNetworkSpawn에서 못 잡았으면 이후에도 못 잡음

3. **`NetworkUnit.cs`의 지역 변수 호출 (178)**
   - 한 번만 쓰이는 듯하나 메서드가 자주 호출되면 캐시 가치 있음
   - 필드 캐시 또는 OnNetworkSpawn에서 1회 호출로 통일

**위험 요소:**
- OnNetworkSpawn 시점에 GameBootstrapper가 씬에 존재하는지 보장 필요 — 현재 LoadMap이 GameBootstrapper에서 일어나므로 안전.
- 일부 컨트롤러는 OnNetworkSpawn 이전에 메서드가 호출될 가능성 — 그 경우 null 가드만 작동하고 종료. 기존 동작과 동일.
- 변경 범위가 매우 작지만 파일 수가 많음 → 한 PR로 처리해도 무방.

### 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Infrastructure/Network/NetworkBuildingController.cs` | 7회 호출 → OnNetworkSpawn 1회만 유지, 나머지 6회 제거 | 수정 |
| `Infrastructure/Network/NetworkCombatController.cs` | 4회 → 1회 | 수정 |
| `Infrastructure/Network/NetworkProductionController.cs` | 8회 → 1회 | 수정 |
| `Infrastructure/Network/NetworkHealthSync.cs` | 2회 → 1회 | 수정 |
| `Infrastructure/Network/NetworkGameFlow.cs` | 2회 → 1회 | 수정 |
| `Infrastructure/Network/NetworkResourceSync.cs` | 2회 → 1회 | 수정 |
| `Infrastructure/Network/NetworkTileSync.cs` | 2회 → 1회 | 수정 |
| `Infrastructure/Network/NetworkUnit.cs` | 1회 지역 변수 → 필드 캐시 또는 단일 호출 유지 (필요 시) | 수정 |
| `Infrastructure/Network/NetworkUnitMovementController.cs` | 2회 → 1회 | 수정 |
| `Infrastructure/Network/NetworkGameEndController.cs` | **그룹 3에서 처리** (UI 의존 제거와 함께 GameBootstrapper 주입으로 전환) | 수정 (그룹 3) |

---

## 작업 그룹 5: O(n) 탐색 캐시화 (위치 역인덱스 + 팀별 카운터)

**근거(아키텍처 원칙):** 모바일 성능 가이드라인 — 매 프레임 호출 가능성이 있는 경로의 O(n) 탐색을 O(1) 또는 O(log n)으로 전환.

**작업 범위:**

1. **위치 역인덱스 Dictionary** (위치 → 엔티티)
   - `UnitSpawnUseCase.GetUnitAt(HexCoord)` (라인 109) — 매 호출 시 _units 전체 순회
   - `BuildingPlacementUseCase.GetBuildingAt(HexCoord)` (라인 265) — 매 호출 시 _buildings 전체 순회

2. **팀별 카운터 캐시**
   - `PopulationUseCase.GetUsedPopulation(team)` (라인 43) — 매 호출 시 건물+유닛 전체 순회. EnqueueUnit/ToggleAutoProduction마다 호출.
   - `HexGrid.CountTilesOwnedBy(team)` (라인 202) — 187타일 순회. PopulationUseCase.GetMaxPopulation에서 매 호출.

**접근 방법:**

### 위치 역인덱스 (UnitSpawn / BuildingPlacement)

1. 기존 `_units : Dictionary<int, UnitData>` 옆에 `_unitsByPosition : Dictionary<HexCoord, UnitData>` 추가.
2. 생성/제거/이동 시점에 두 Dictionary를 모두 갱신:
   - `SpawnUnit`: `_units[id] = unit; _unitsByPosition[unit.Position] = unit;`
   - `RemoveUnit`: 둘 다 제거
   - **이동 시점**: `UnitData.Position` 변경 직전에 기존 키 제거 → 변경 후 새 키 추가
3. `GetUnitAt(coord)` → `_unitsByPosition.TryGetValue(coord, out var unit)` 직접 조회.
4. **주의**: 같은 좌표에 여러 유닛이 겹칠 수 있는 경우(슬롯 폐기로 겹침 허용) → `Dictionary<HexCoord, List<UnitData>>` 또는 `Dictionary<HexCoord, UnitData>` 중 마지막 추가만 유지하는 정책 결정 필요.
   - **현재 `GetUnitAt`은 단일 반환** → 단순 Dictionary로 충분하되, 멀티 유닛 케이스에서 어떤 유닛이 우선인지 호출자 측 의도 확인 필요. (현재 InputHandler 등이 "임의의 하나" 정책으로 보임)
   - 또는 `Dictionary<HexCoord, List<UnitData>>`로 안전하게 가되, GetUnitAt은 List의 첫 항목 반환.

### 팀별 카운터 캐시 (Population / HexGrid)

1. `HexGrid`에 `_ownedTileCounts : Dictionary<TeamId, int>` 필드 추가.
2. `HexTile.Owner setter` 또는 `SetOwner(coord, team)` 호출 시 이전 팀 -1, 새 팀 +1 갱신.
3. `CountTilesOwnedBy(team)` → `_ownedTileCounts.TryGetValue(team, out var count) ? count : 0` 즉시 반환.
4. `PopulationUseCase.GetUsedPopulation`도 동일 패턴:
   - `_usedPopulationByTeam : Dictionary<TeamId, int>` 필드 추가
   - `GameEvents.OnUnitSpawned` / `OnUnitDied` / `OnBuildingPlaced` / `OnBuildingDied` 구독으로 증감 추적

**위험 요소:**
- **이중 자료구조 동기화 문제** — 두 Dictionary 중 하나만 갱신하면 무결성 깨짐. SpawnUnit / RemoveUnit / MoveUnit 등 모든 변경 진입점에서 양쪽 갱신 보장 필요.
- 유닛 이동은 여러 곳에서 발생 — UnitMovementUseCase, NetworkUnitMovementController 등. 모든 경로 식별 필요.
- 팀별 카운터: HexTile.Owner setter가 외부에서 직접 호출되는지 확인. SetOwner 헬퍼만 사용하도록 제한된다면 안전.
- **OnUnitDied 등 이벤트 구독 추가** — 구독 해제(Dispose) 누락 시 재경기 흐름에서 누적될 수 있음. PopulationUseCase에 CompositeDisposable 필요.
- 단위 테스트 없는 상태에서 캐시 무결성을 사후 검증하기 어려움 → 변경 시점에 디버그용 검증 메서드(`AssertCacheConsistent()`)를 Editor 빌드에서만 호출하는 방안 고려.

### 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Application/UseCases/UnitSpawnUseCase.cs` | `_unitsByPosition` 필드 추가, SpawnUnit/RemoveUnit/MoveUnit에서 동기화, GetUnitAt O(1) 변환 | 수정 |
| `Application/UseCases/BuildingPlacementUseCase.cs` | `_buildingsByPosition` 필드 추가, 같은 패턴 적용 | 수정 |
| `Application/UseCases/PopulationUseCase.cs` | `_usedPopulationByTeam` 필드 추가, OnUnit*/OnBuilding* 이벤트 구독, GetUsedPopulation O(1) 변환, Dispose 패턴 추가 | 수정 |
| `Domain/Hex/HexGrid.cs` | `_ownedTileCounts` 필드 추가, SetOwner에서 증감 갱신, CountTilesOwnedBy O(1) 변환 | 수정 |
| `Application/UseCases/UnitMovementUseCase.cs` | 유닛 이동 시 `_unitsByPosition` 갱신 호출 추가 (UnitSpawnUseCase 통해) | 수정 |
| `Infrastructure/Network/NetworkUnitMovementController.cs` | 클라이언트 측 이동 시 동일 갱신 | 수정 (필요 시) |

---

## 작업 그룹 6: 중간/낮음 항목 정리 (가독성 + 유지보수)

**근거(아키텍처 원칙):** 코드 가독성 가이드 — 메서드 30줄 미만, 생성자 위임, enum 직렬화 안전성 등.

본 그룹은 위 1~5 그룹과 독립적이며, 가장 마지막 또는 여유 있을 때 진행. **각 항목은 별도 sub-task로 분리 가능.**

### 6-1. BuildingType enum 명시값 부여

- **변경**: `Domain/Building/BuildingType.cs` — 각 멤버에 `= 정수` 명시 (Castle = 0, MiningPost = 1, ...)
- **근거**: 주석에 "열거형 멤버 순서 변경 시 직렬화 데이터 깨질 수 있음" 명시. RPC 직렬화도 영향. 현재 단계별/종족별 정의 순서로 묶여 있어 신규 추가 시 인덱스 밀림 위험.
- **위험**: ScriptableObject(`BuildingStatsConfig.asset` 등) 재import 필요. 현재 인덱스를 보존하는 값으로 부여하면 위험 없음.
- **방법**: 현재 enum 순서대로 0~31 명시 부여 → 기존 직렬화 데이터와 동일하므로 안전.

### 6-2. UnitData / BuildingData 생성자 중복 제거

- **변경**: `Domain/Unit/UnitData.cs` (생성자 라인 109, 145), `Domain/Building/BuildingData.cs` (라인 58, 78)
- **방법**: 일반 생성자가 ID 지정 생성자를 `: this(...)`로 위임.

### 6-3. UnitProductionUseCase 큰 메서드 분해

- **대상**: `EnqueueUnit` (60줄), `ToggleAutoProduction` (150+줄), `CancelQueueAt` (100+줄)
- **방법**: 검증/처리/이벤트 발행을 private 헬퍼로 분리. 시그니처는 동일 유지(외부 호출자 영향 없음).

### 6-4. UnitCombatUseCase 거리 판정 헬퍼 통합

- **대상**: `FindFirstEnemyTarget` (539), `FindFirstEnemyInDetectRange` (462), `IsTargetInRange` (716) — 세 메서드 모두 unitMaxDist/buildingMaxDist 분기 로직 중복
- **방법**: `private (float unitMax, float buildingMax) CalculateRangeLimits(UnitData attacker, bool isDetect)` 헬퍼 추출.

### 6-5. ProductionPanelUI EventTrigger / Button 초기화 1회화

- **대상**: 라인 262, 309 — Show할 때마다 GetComponent/AddComponent 호출
- **방법**: Initialize() 시점에 1회만 캐시. Inspector 직접 참조로 대체 검토.

### 6-6. ProductionTicker UnitView 캐시화

- **대상**: 라인 307, 673 — `unitObj.GetComponent<UnitView>()` 매 호출
- **방법**: UnitFactory에 `GetView(int unitId)` API 추가하여 UnitView를 미리 캐시한 것을 반환.

### 6-7. GameEvents.OnUnitEnteredTile 일관성 통일

- **대상**: `Application/Events/GameEvents.cs` 라인 438 — 유일한 `Action<int, HexCoord>` 패턴
- **방법**: `Subject<UnitEnteredTileEvent>`로 변경. 발행/구독 모두 표준화. GameBootstrapper 라인 1004의 ActionDisposable 래퍼도 제거.

### 6-8. NetworkBuildingController / NetworkProductionController TODO 해소

- **대상**: NetworkBuildingController 라인 266, NetworkProductionController 라인 850 — "TODO: 토스트" 주석
- **방법**: 이미 구축된 ToastUI/ToastKey 시스템에 연결. UpgradeRequired 등 기존 키 활용.

### 6-9. NetworkGameEndController FindFirstObjectByType 정리

- **대상**: 라인 98, 105, 110, 153, 182, 333 — 다수 호출
- **방법**: GameBootstrapper Inspector 주입으로 교체 (이미 `_networkGameEnd` 필드 존재).
- **주의**: 그룹 3 (UI 의존 제거)과 함께 진행하면 한 PR로 NetworkGameEndController의 UI 직접 참조도 함께 해소 가능.

### 6-10. BuildingTypeHelper switch 통합

- **대상**: `Domain/Building/BuildingTypeHelper.cs` — IsProductionBuilding / GetStage / GetNextStage 각각 switch 보유
- **방법**: `private static readonly Dictionary<BuildingType, (bool isProduction, int stage, BuildingType? next)>` 단일 테이블 + 조회 메서드 3개. 신규 건물 추가 시 한 곳만 수정.

### 6-11. BuildingStats.GetUpgradeCost 3중 TryGet 단순화

- **대상**: `Domain/Building/BuildingStats.cs` 라인 181~188
- **방법**: `_upgradeCosts : Dictionary<BuildingType, int>` 단일 Dictionary로 분리 (종족 무관).

### 6-12. AnimatedPanel / RematchRequestPopup GetComponent 캐시 검증

- **대상**: `AnimatedPanel.cs` 라인 150, 155, `RematchRequestPopup.cs` 라인 123
- **방법**: 호출 빈도 확인 후 필요 시 Awake/OnEnable에서 1회 캐시. 이미 캐시되어 있다면 보존.

### 6-13. GameBootstrapper IsNetworkMode 일관성

- **대상**: `Bootstrap/GameBootstrapper.cs` 라인 352~378 Update() + 라인 1311 `IsNetworkMode()`
- **방법**: `NetworkManager.Singleton` 직접 호출 → `NetworkContext.IsNetworkActive` 사용으로 통일. 매 프레임 비용도 감소.

### 6-14. MatchmakerManager 일반 Exception 교체

- **대상**: `Infrastructure/Network/MatchmakerManager.cs` 라인 113, 116, 136, 139
- **방법**: `MatchmakingException` 도메인 예외 클래스 또는 Result 패턴.

### 6-15. ToastMessageConfig / UnitFactory Infrastructure→Presentation 역방향 의존 해소

> **⚠️ 범위 변경**: NetworkCombatController, NetworkGameEndController는 **그룹 3 카테고리 E로 이동**하여 처리. 본 항목은 ToastMessageConfig, UnitFactory 2건.

**(코드 직접 확인 완료 — 해결책 확정)**

#### ToastMessageConfig (간단)
- **원인**: `ToastKey` enum이 Presentation 네임스페이스에 있어서 Config가 Presentation을 import.
- **해결**: `ToastKey` enum을 **Application 레이어로 이동** → ToastMessageConfig는 Presentation 대신 Application만 참조. 위험 거의 없음.
- **영향**: ToastKey를 참조하는 모든 파일의 using 경로 업데이트 필요 (Grep으로 확인 후 일괄 변경).

#### UnitFactory (인터페이스 추출)
- **원인**: `UnitView`를 직접 GetComponent로 꺼내어 `Initialize(unitData)` / `SetDependencies(...)` 호출.
- **해결**: Application 레이어에 `IUnitView` 인터페이스 정의 → UnitView가 구현 → UnitFactory는 `IUnitView`만 알면 됨.
  ```
  // Application/Abstractions/IUnitView.cs
  interface IUnitView {
      void Initialize(UnitData unitData);
      void SetDependencies(UnitMovementUseCase, UnitCombatUseCase, ...);
  }
  ```
- **영향**: UnitView에 `: IUnitView` 추가. UnitFactory의 `UnitView` 타입 참조를 `IUnitView`로 교체.

#### 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Presentation/UI/ToastKey.cs` (현재 위치 추정) | Application 레이어로 이동 | 이동 |
| `Infrastructure/Config/ToastMessageConfig.cs` | `using Hexiege.Presentation` → `using Hexiege.Application` | 수정 |
| `Application/Abstractions/IUnitView.cs` (신규) | `Initialize` / `SetDependencies` 인터페이스 | 추가 |
| `Presentation/View/UnitView.cs` | `: IUnitView` 추가 | 수정 |
| `Infrastructure/Factories/UnitFactory.cs` | `using Hexiege.Presentation` 제거, `UnitView` → `IUnitView` 교체 | 수정 |

---

## 작업 그룹 7: GameBootstrapper partial class 분리 (독립 진행)

> **⚠️ 결정 사항**: 그룹 6-14에서 독립 그룹으로 격상. 이유: 파일 분리는 기능 변경이 없지만 **잘라붙이기 실수 하나로 기능이 통째로 사라질 수 있어**, 다른 리팩토링 작업과 섞이면 원인 추적이 어려워짐. 단독으로 진행하고 검증까지 완료한 뒤 다음 작업으로 이동.

**근거:** 1342줄 단일 파일 → 가독성/유지보수 부담. 각 시스템(건물/생산/입력/네트워크) 초기화 코드를 담당 파일로 분리하면 변경 추적이 쉬워짐.

**작업 범위:** `Bootstrap/GameBootstrapper.cs` 전체.

**접근 방법: partial class 방식**

C#의 `partial class`는 컴파일 시 하나로 합쳐지므로, `MonoBehaviour` 상속도 정상 동작함.

권장 파일 분리 구조:

| 파일 | 담당 내용 |
|------|-----------|
| `GameBootstrapper.cs` | `[SerializeField]` 전체 + `Start()` + `Update()` + `Getter 메서드` — **생명주기와 Inspector 필드는 반드시 이 파일에만** |
| `GameBootstrapper.Setup.cs` | `SetupBuildings`, `SetupProduction`, `SetupInput`, `SetupUI` 등 초기화 헬퍼 |
| `GameBootstrapper.Map.cs` | `LoadMap`, `PlaceCastles`, `PlaceGoldMines`, `ClearAll` 등 맵 관련 |
| `GameBootstrapper.Network.cs` | `StartNetworkGame`, 네트워크 이벤트 구독 관련 |

각 파일 상단에 반드시 `public partial class GameBootstrapper : MonoBehaviour` 선언 필요.

**발생 가능한 문제 및 해결 방법:**

| 문제 | 해결 방법 |
|------|-----------|
| `Start()` / `Update()` 같은 Unity 생명주기 메서드가 두 파일에 나뉘면 "중복 정의" 컴파일 에러 | **생명주기 메서드는 무조건 `GameBootstrapper.cs` 한 파일에만** 유지. partial 파일은 private 헬퍼 메서드만 담음 |
| `[SerializeField]` 필드가 여러 파일에 흩어지면 Inspector 항목이 어느 파일에 있는지 찾기 어려움 | **`[SerializeField]` 필드는 무조건 메인 파일에만** 유지. partial 파일에 새 [SerializeField]를 추가하지 않는다는 규칙을 헤더 주석에 명시 |
| 메서드를 다른 파일로 옮기는 과정에서 잘라붙이기 실수(코드 일부 누락) | 메서드 단위로 이동 → 이동 직후 컴파일 확인 → 다음 메서드 이동. **한 번에 전체를 옮기지 않음** |
| private 필드를 참조하는 메서드가 다른 파일로 이동해 "어느 파일에 있는지" 헷갈릴 수 있음 | partial class는 같은 클래스이므로 접근은 문제없음. 단, 필드 선언은 메인 파일에, 사용은 partial 파일에 있어도 됨을 주석으로 명시 |
| 분리 후 Unity Editor에서 인식 안 됨 | partial 파일 이름이 `GameBootstrapper.*.cs` 패턴이면 Unity가 자동 인식. 단, `.meta` 파일이 생성되는지 Editor에서 확인 필수 |

**위험 요소:**
- **이 그룹은 기능 변경이 전혀 없어야 함** — 메서드 이동만. 분리 완료 후 싱글플레이/멀티플레이 전체 흐름을 플레이하여 기능 이상 없음을 반드시 검증.
- 그룹 1(슬롯 잔재 제거) 이후 진행 권장 — 분리 대상 코드가 줄어든 상태에서 작업하면 실수 가능성이 낮아짐.
- 다른 리팩토링 그룹과 **동시에 진행 금지** — 문제 발생 시 원인 파악이 어려워짐.

### 변경 파일 목록

| 파일 | 변경 내용 | 변경 방식 |
|------|-----------|-----------|
| `Bootstrap/GameBootstrapper.cs` | `[SerializeField]` / `Start()` / `Update()` / Getter만 남기고 헬퍼 메서드 이동 | 수정 |
| `Bootstrap/GameBootstrapper.Setup.cs` (신규) | 초기화 헬퍼 메서드 이동 | 추가 |
| `Bootstrap/GameBootstrapper.Map.cs` (신규) | 맵 관련 메서드 이동 | 추가 |
| `Bootstrap/GameBootstrapper.Network.cs` (신규) | 네트워크 관련 메서드 이동 | 추가 |

---

## 전체 위험 요소 요약

### 공통 위험
1. **컴파일 깨짐** — 그룹 1(슬롯 제거) / 그룹 2(생성자 시그니처 변경) / 그룹 3(UI ServerRpc 제거)이 가장 위험. 각 그룹 종료 시 Unity Editor에서 컴파일 확인 필수.
2. **인스펙터 직렬화** — SerializeField 제거 시 인스펙터의 기존 값 손실. 본 Plan은 주로 코드/주석/내부 자료구조 변경이므로 직렬화 영향 낮음. 단, BuildingType enum 명시값 부여(6-1)는 잘못하면 모든 ScriptableObject 재import 필요.
3. **이벤트 구독 해제 누락** — 그룹 5(PopulationUseCase 이벤트 구독)는 Dispose 패턴 누락 시 재경기 흐름에서 누적. CompositeDisposable + Dispose 호출 확실히.
4. **멀티/싱글 분기 회귀** — 그룹 3(UI NGO 제거)에서 NetworkContext 분기로 전환할 때 기존 분기 누락 시 싱글에서 ServerRpc 호출 시도 등 NRE 발생.

### 그룹별 핵심 위험
- **그룹 1**: 분할 시 컴파일 깨짐 — 단일 PR 권장.
- **그룹 2**: GameBootstrapper UseCase 생성 라인 일괄 수정 — 컴파일 후 기본 동작 검증 필수.
- **그룹 2-B**: Red 팀 좌표 반전 로직 오구현 시 점령 판정 반전 — Red 팀 유닛 이동 시 타일 색상 변화 반드시 확인. 그룹 2 완료 후에만 진행.
- **그룹 3**: 9개 파일 변경 범위 큼 — 카테고리별 PR 분할 권장.
- **그룹 4**: 단일 PR 가능 (변경 범위 작음). OnNetworkSpawn 시점 GameBootstrapper 존재 보장.
- **그룹 5**: 이중 자료구조 동기화 — 모든 변경 진입점 식별 필요. 디버그 빌드 검증 메서드 권장.
- **그룹 6**: 각 항목 독립 — 영향 범위 작아 위험 낮음.
- **그룹 7**: 기능 변경 없어야 함 — 메서드 이동만. 분리 후 전체 플레이 검증 필수. 다른 그룹과 동시 진행 금지.

### 결정 완료 항목
1. ✅ **TileOwnershipService 정책** → (A) 원칙 적용. 그룹 2-B로 독립 진행.
2. ✅ **IForfeitService 인터페이스 추출** → (B) 인터페이스 추상화. 그룹 3에서 처리.
3. ✅ **GetUnitAt 다중 유닛 정책** → (B) 복수 반환(`Dictionary<HexCoord, List<UnitData>>`). 그룹 5에서 처리.
4. ✅ **GameBootstrapper 분리** → (A) partial class 분리. 그룹 7로 독립 진행.
5. ⬜ **Infrastructure→Presentation 역방향 의존** (그룹 6-15) — 실제 의존 방향 재검증 후 정책 결정.

---

## 작업 순서 (권장)

위에서 아래로 순차 진행. 각 그룹 종료 시 Unity Editor 컴파일 + 기본 플레이 검증 후 다음 그룹 진행.

1. **그룹 1: 슬롯/점유 시스템 잔재 일괄 제거** (1회 단일 PR, 약 600줄 감축)
   - 가장 효과 큼, 위험 낮음 (이미 죽은 코드)
   - 후속 그룹의 영향 면적을 미리 축소
2. **그룹 7: GameBootstrapper partial class 분리** (기능 변경 없음, 단독 진행)
   - 그룹 1 이후 분리 대상 코드가 줄어든 상태에서 작업 → 실수 가능성 감소
   - 분리 완료 + 전체 플레이 검증 후 다음 단계로
3. **그룹 4: FindFirstObjectByType 30+회 캐시화** (1회 단일 PR)
   - 변경 범위 작고 위험 낮음
   - 모바일 성능 즉시 효과
4. **그룹 2: Application → Core 의존 제거 (UseCase 3건)**
   - 인터페이스 추출(IHexCoordinateMapper) + GameBootstrapper 주입
   - 완료 + 컴파일 확인 후 즉시 2-B 진행 가능
5. **그룹 2-B: TileOwnershipService Core 의존 제거** (그룹 2 완료 후에만)
   - Red 팀 좌표 반전 로직 래핑 — 점령 판정 검증 필수
6. **그룹 3: Presentation → NGO 의존 제거** (카테고리 A → D → B → C 순 분할 PR)
   - 변경 범위 큼 → 카테고리별 분할 PR 권장
   - 그룹 4 이후 진행하면 NetworkGameEndController의 FindFirstObjectByType도 함께 해소
7. **그룹 5: O(n) 탐색 캐시화** (위치 역인덱스 + 팀별 카운터)
   - 이중 자료구조 동기화 위험 → 충분한 테스트 필요
   - 6-7(GameEvents 통일) 이후 진행하면 이벤트 기반 카운터 갱신이 더 깔끔
8. **그룹 6: 중간/낮음 항목** (각 항목 독립 sub-task)
   - 6-1 (enum 명시값), 6-2 (생성자 중복) 먼저 — 가장 안전
   - 6-3~6-5 (메서드 분해) — 가독성 개선
   - 6-7 (GameEvents 통일) — 그룹 5의 사전 작업으로 진행 가능
   - 6-8 (TODO 토스트 해소) — 간단한 마무리
   - 6-13 (IsNetworkMode 일관성) — 그룹 4 이후 자연스럽게 함께 진행
   - 나머지는 여유 있을 때 진행

---

## 검증 체크리스트 (각 그룹 종료 시)

- [ ] Unity Editor 컴파일 에러 0
- [ ] 메뉴 → 로비 → 싱글플레이 게임 시작 → 유닛 생산 → 전투 → 종료 동작 확인
- [ ] 메뉴 → 로비 → 호스트/클라이언트 연결 → 게임 시작 → 동일 흐름 확인
- [ ] 게임 중 인게임 설정 → 포기 동작 확인 (싱글/멀티 양쪽)
- [ ] 건물 생산/업그레이드/철거 동작 확인
- [ ] 재경기 흐름(Rematch) 동작 확인
- [ ] 콘솔에 NullReferenceException 0건

---

## 본 Plan을 따랐을 때 예상 효과

- **코드 감축**: 약 600~800줄 (주로 그룹 1)
- **레이어 위반 해소**: 15+ → 0~3 (TileOwnershipService 정책 결정 + 일부 보류 항목 제외)
- **매 프레임 비용 감소**:
  - `FindFirstObjectByType` 30+회 → 0회 (OnNetworkSpawn 외)
  - `GetUnitAt` / `GetBuildingAt` / `GetUsedPopulation` / `CountTilesOwnedBy` O(n) → O(1)
- **신규 기능 추가 시 의사결정 부담 감소**:
  - 종족 추가 시: BuildingTypeHelper 1곳 + RacePrefabRegistry 1곳만 수정 (현재는 4곳 이상)
  - 건물 타입 추가 시: enum 명시값 덕분에 직렬화 안전 + Helper 테이블 1곳만 수정
  - 멀티/싱글 분기 추가 시: NetworkContext.IsClientOnly 헬퍼 1곳에서 결정

본 Plan은 향후 약 6주 분량의 점진적 정리 작업입니다. 각 그룹은 독립적이므로 진행 순서 조정 가능합니다.
