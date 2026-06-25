# Research — GameBootstrapper.Setup.cs 하드코딩 배열 파생

## 이 작업이 무엇이고, 왜 하는가 (자연어 설명)

게임이 시작될 때, 건물을 철거하면 돌려받을 환불 골드를 미리 계산해서 저장해 두는 코드가 있습니다.
이 계산을 하려면 "어떤 건물이 1단계 생산건물인지", "어떤 건물이 비생산건물인지"를 알아야 합니다.

현재는 그 목록이 두 가지 방식으로 **두 곳에 따로** 적혀 있습니다.

1. `BuildingTypeHelper.cs` 안의 **lookup table(조회 표)** — 건물의 단계/생산 여부를 한 곳에 모아둔 표.
   (Phase 2 작업에서 세 개의 switch 문을 이 표 하나로 통합했습니다.)
2. `GameBootstrapper.Setup.cs` 안의 **하드코딩 배열 두 개** — 1단계 건물 목록, 비생산 건물 목록을 손으로 직접 나열한 배열.

문제는, 새 건물을 추가하면 이 표와 배열 **두 곳(실제로는 세 군데)을 모두** 손봐야 한다는 점입니다.
한 곳이라도 빠뜨리면 환불 골드가 잘못 계산되는 버그가 조용히 생깁니다.
실제로 lookup table에는 건물을 추가했는데 Setup.cs의 배열에 추가하는 걸 잊으면, 그 건물은 환불 캐시가 채워지지 않아 철거 시 환불액이 0으로 표시됩니다.

이번 작업은 **Setup.cs의 두 배열을 직접 나열하지 않고, lookup table에서 자동으로 뽑아내도록(파생)** 바꾸는 것입니다.
이렇게 하면 건물 목록의 "단일 소스(single source of truth)"가 lookup table 하나로 정리되어,
앞으로 건물을 추가할 때 lookup table만 고치면 환불 캐시도 자동으로 따라옵니다.

> 주의: 이 작업은 게임 동작(환불 규칙, 단계 구조 등)을 **바꾸지 않습니다**. 결과 값은 현재와 100% 동일해야 하며,
> 단지 같은 값을 "손으로 적은 배열"이 아니라 "표에서 자동 계산한 목록"으로 얻도록 코드 구조만 개선하는 작업입니다.

---

## 대상 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs` | 하드코딩 배열 두 개가 위치한 파일 (수정 대상) |
| `Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs` | lookup table 및 공개 조회 API 보유 (참조 / 안에 따라 수정 대상) |
| `Assets/_Project/Scripts/Domain/Building/BuildingType.cs` | BuildingType 열거형 정의 (참조만) |

---

## 현재 상태 분석 (실제 코드 확인 결과)

### 1. 하드코딩 배열 1 — `stage1Buildings` (Setup.cs 176~187행)

```csharp
// 1단계 생산건물 목록 (BuildingTypeHelper.GetStage() == 1)
var stage1Buildings = new BuildingType[]
{
    BuildingType.TrainingCamp,
    BuildingType.Gunsmith,
    BuildingType.Garage,
    BuildingType.FireSpire,
    BuildingType.AquaSpring,
    BuildingType.StoneMound,
    BuildingType.PrimalAltar,
    BuildingType.FeralAltar,
    BuildingType.SporePatch,
};
```

- 항목 9개. 모두 `_buildingTable`에서 `Stage == 1`인 건물.
- **사용처**: 철거 환불용 누적 투자 비용 캐시 (Setup.cs 189~208행).
  - 각 1단계 건물에서 시작해 `BuildingTypeHelper.GetNextStage()` 체인을 순방향으로 순회하며,
    단계마다 이전 단계의 업그레이드 비용을 누적해 `BuildingStats.SetTotalInvestedCost()`로 저장한다.
  - Human / Spirit / Transcendence 3종족 각각에 대해 반복.

**lookup table과의 일치 검증** (`BuildingTypeHelper.cs` `_buildingTable` 기준):
`_buildingTable`에서 `Stage == 1`인 항목은 다음 9개로, 위 배열과 **완전히 일치**한다.
TrainingCamp, Gunsmith, Garage, FireSpire, AquaSpring, StoneMound, PrimalAltar, FeralAltar, SporePatch.

### 2. 하드코딩 배열 2 — `nonProductionBuildings` (Setup.cs 218~226행)

```csharp
var nonProductionBuildings = new BuildingType[]
{
    BuildingType.MiningPost,
    BuildingType.AutoTower,
    BuildingType.FlightFacility,
    BuildingType.Research,
    BuildingType.MagicBuilding,
    BuildingType.HealShrine,
};
```

- 항목 6개. 비생산 건물 중 Castle을 제외한 전부.
- **사용처**: 비생산 건물 환불 캐시 (Setup.cs 227~234행).
  - 비생산 건물은 단계 개념이 없어 최초 건설 비용 자체가 누적 투자 비용이 된다.
  - 각 건물의 건설 비용(`BuildingStats.GetGoldCost`)을 그대로 `SetTotalInvestedCost()`로 저장.
  - Castle은 철거 불가이므로 제외 (코드 주석 214행에 명시).

**lookup table / enum과의 일치 검증** (`BuildingType.cs` 비생산 섹션 기준):
비생산 건물 enum 멤버는 Castle(0), MiningPost(1), AutoTower(2), FlightFacility(3), Research(4), MagicBuilding(5), HealShrine(6) — 총 7개.
위 배열은 여기서 Castle만 제외한 6개로 **완전히 일치**한다.
또한 `BuildingTypeHelper.IsProductionBuilding()`은 이 7개 모두에 대해 false를 반환한다 (lookup table에 없으므로).

### 3. BuildingTypeHelper 공개 API (Phase 2 완료 상태, 실제 확인)

| 메서드 | 반환 | 동작 |
|--------|------|------|
| `IsProductionBuilding(BuildingType)` | bool | lookup table에 있고 IsProduction이면 true |
| `GetStage(BuildingType)` | int | table에 있으면 Stage(1/2/3), 없으면 0 |
| `GetNextStage(BuildingType)` | BuildingType? | 다음 단계, 없으면 null |
| `CanUpgrade(BuildingType)` | bool | GetNextStage가 값이 있으면 true |
| `CanShowActionPanel(BuildingType)` | bool | 비생산 + Castle 아님이면 true |

- `_buildingTable`은 **private** Dictionary. 외부에서 직접 접근 불가 — 공개 API로만 조회 가능.
- 이 파일은 순수 C# (Unity 의존 없음, Domain 레이어).

### 4. BuildingType 열거형 (실제 확인)

- 멤버 총 32개 (값 0~31 명시 부여).
- **`Castle = 0`** — 비생산 필터에서 `t != BuildingType.Castle` 조건이 반드시 필요함을 재확인.
- 비생산 7개(0~6) + 생산 25개(7~31).

### 5. Setup.cs의 `using` 선언 (실제 확인 — 중요)

Setup.cs 22~29행 `using` 블록:
```
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Application.Services;
using Hexiege.Infrastructure;
```

- **`using System;` 은 존재하지 않는다.**
- 따라서 `Enum.GetValues` / `Array.FindAll`을 정규화된 이름 없이 쓰려면 `using System;` 추가가 필요하다.
  (또는 `System.Enum` / `System.Array` 전체 경로 사용 — Plan에서 방식 결정.)

---

## 영향 범위

| 항목 | 영향 |
|------|------|
| 환불 캐시 계산 결과 | **변화 없음** — 동일 건물 목록을 동일 순서/무관하게 처리, 값 동치 |
| `BuildingTypeHelper` 공개 API | 안 1 선택 시 메서드 2개 추가 / 안 2 선택 시 변화 없음 |
| Domain 레이어 | 안 2 선택 시 무변경 / 안 1 선택 시 헬퍼 책임 확장 |
| 직렬화 데이터 (ScriptableObject/Scene/RPC) | 영향 없음 — enum 값·순서 변경 없음 |
| 성능 | 게임 시작 시 enum 32개 1회 순회 — 무시 가능 |
| 멀티플레이 | 영향 없음 — 초기화 로직, 네트워크 동기화와 무관 |

---

## 위험 요소

1. **Castle 제외 누락 위험**: `Castle = 0`이므로 비생산 필터에서 `t != BuildingType.Castle`을 빠뜨리면
   Castle에도 환불 캐시가 채워진다. 동작상 무해(Castle은 철거 불가)하나, 기존 주석의 명시적 제외 의도와 어긋나므로 조건을 유지한다.
2. **`using System;` 누락**: Setup.cs에 `using System;`이 없으므로 `Enum`/`Array`를 정규화 이름 없이 쓰면 컴파일 에러.
   → Plan에서 `using System;` 추가 또는 전체 경로 사용을 명시.
3. **순서 의존성 없음**: `Enum.GetValues`는 enum 정의 순서대로 반환한다.
   환불 캐시 계산은 각 건물(또는 각 1단계 체인)을 독립적으로 처리하므로 순서가 결과에 영향을 주지 않는다.
   (1단계 체인 순회는 1단계 건물에서 시작하는 한 순서 무관.)
4. **lookup table 신뢰 전제**: 파생 방식은 lookup table이 정확하다는 전제에 의존한다.
   현재 table은 실제 코드 확인 결과 두 배열과 정확히 일치하므로 전환 후에도 동치가 보장된다.

---

## 발견된 부가 이슈

- 없음. 두 하드코딩 배열은 lookup table 및 enum 정의와 정확히 일치하여, 현재 상태에서 숨은 불일치(잠재 버그)는 발견되지 않았다.
  즉 이번 작업은 "현재 버그 수정"이 아니라 "미래 버그 예방(분산 관리 제거)"이 목적이다.
