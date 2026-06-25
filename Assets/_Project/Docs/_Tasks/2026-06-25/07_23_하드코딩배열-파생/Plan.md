# Plan — GameBootstrapper.Setup.cs 하드코딩 배열 파생

## 이 작업이 무엇이고, 왜 하는가 (자연어 설명)

`GameBootstrapper.Setup.cs`에는 환불 골드 계산에 쓰이는 건물 목록 배열 두 개가 손으로 직접 적혀 있습니다.
같은 정보가 이미 `BuildingTypeHelper`의 lookup table에 들어 있으므로, 이 배열들을 **표에서 자동으로 뽑아내도록** 바꿔
건물 목록을 한 곳에서만 관리하게 만드는 작업입니다.

목표는 단 하나입니다 — **앞으로 새 건물을 추가할 때 lookup table 한 곳만 고치면 환불 캐시도 자동으로 따라오게** 하는 것.
게임의 동작이나 환불 규칙은 전혀 바뀌지 않으며, 계산 결과 값은 현재와 완전히 동일합니다.

---

## ⚠️ 기존 로직 제거 관련 (WORKFLOW.md 규칙 — 최상단 명시)

이 작업은 **하드코딩 배열 두 개(`stage1Buildings`, `nonProductionBuildings`)를 파생 코드로 교체**한다.

- **교체 즉시 삭제 가능 여부: 가능.**
  Research.md에서 실제 코드를 대조한 결과, 두 배열의 항목이 lookup table / enum 정의와 **정확히 일치**함을 확인했다.
  파생 코드가 생성하는 목록은 기존 배열과 **값이 완전히 동치**이며, 환불 캐시 계산은 순서에 무관하다.
  따라서 "비활성화(주석 처리) 후 검증" 단계 없이 **선언부를 곧바로 파생 코드로 교체**해도 안전하다.
- 단, 사용자가 보수적으로 진행하길 원하면 1단계로 주석 처리 후 테스트 통과 시 삭제하는 방식도 가능하다 (선택 항목).

---

## GameSystemRules 근거

WORKFLOW.md [4] 규칙에 따라 `GameSystemRules.md` 인덱스 및 관련 파일을 확인했다.

- 이 작업은 **게임플레이 동작 규칙의 변경이 아니라 초기화 코드의 구조 개선**이다.
  (Phase 2의 `HexMetrics` / `BuildingTypeHelper` switch→table 통합과 동일한 성격의 리팩토링.)
- `GameSystemRules_Buildings.md`의 "건물 철거 시스템 / 골드 환불" 규칙이 이 코드가 채우는 환불 캐시의 **결과 동작**을 정의한다.
  이번 작업은 그 규칙이 정한 동작을 **그대로 유지**하는 것이 제약이다 — 환불 값이 바뀌면 규칙 위반이다.
- 즉, **신설/변경되는 GameSystemRules 규칙은 없으며**, 기존 철거/환불 규칙의 동작 보존이 합격 기준이다.

---

## 변경 방식 — 2가지 안 (사용자 선택 필요)

### 안 1 — BuildingTypeHelper에 공개 유틸리티 메서드 추가

`BuildingTypeHelper.cs`에 목록 조회 메서드 2개를 추가하고, Setup.cs에서 호출한다.

추가 메서드(개념):
- `GetAllStage1Buildings()` — 모든 BuildingType을 순회해 `GetStage() == 1`인 것만 반환.
- `GetNonProductionBuildingsExceptCastle()` — `IsProductionBuilding() == false`이고 Castle이 아닌 것만 반환.

- **장점**: 건물 관련 질의가 `BuildingTypeHelper` 한 창구로 모인다. Setup.cs는 의미가 명확한 메서드만 호출.
- **단점**: 도메인 헬퍼의 공개 API가 늘고, "단계 판별기"가 "목록 제공자" 역할까지 겸하게 된다.
  현재 호출처가 Setup.cs 단 한 곳이라 헬퍼에 공개 API를 추가할 정당성이 약하다.

### 안 2 — Setup.cs에서 `Enum.GetValues` + 기존 공개 API로 직접 파생 ✅ 권장

`BuildingTypeHelper`는 건드리지 않고, Setup.cs 내부에서 기존 공개 메서드만으로 목록을 만든다.

```csharp
// 모든 BuildingType 멤버를 한 번 가져온 뒤, 기존 공개 조회 API로 필터링한다.
var allTypes = (BuildingType[])Enum.GetValues(typeof(BuildingType));

// 1단계 생산건물 = lookup table에서 Stage == 1인 것 (기존 9개와 동치)
var stage1Buildings = Array.FindAll(allTypes, t => BuildingTypeHelper.GetStage(t) == 1);

// 비생산 건물(Castle 제외) = 생산건물이 아니면서 Castle이 아닌 것 (기존 6개와 동치)
var nonProductionBuildings = Array.FindAll(allTypes,
    t => !BuildingTypeHelper.IsProductionBuilding(t) && t != BuildingType.Castle);
```

- **장점**: 도메인 레이어 무변경, BuildingTypeHelper 공개 API 불변, 역할 명확 유지.
  호출처(Setup.cs)가 기존 공개 메서드만 사용하므로 변경 표면이 가장 작다.
- **단점**: enum 전체(32개) 순회가 Setup.cs에 노출된다 (게임 시작 시 1회, 성능 영향 없음).

> **권장: 안 2** — 변경 최소, 도메인 레이어 불변, BuildingTypeHelper 역할 명확 유지.
> **최종 선택은 사용자 승인 필요.**

---

## 구현 상세 (안 2 기준)

### 수정 위치: `GameBootstrapper.Setup.cs` `InitializeBuildingStatsFromConfig()` 내부

1. **`using System;` 추가** (22~29행 using 블록).
   - Research에서 확인: Setup.cs에 `using System;`이 없다. `Enum`/`Array`를 짧은 이름으로 쓰려면 필요.
   - 대안: `using` 추가 없이 `System.Enum.GetValues` / `System.Array.FindAll` 전체 경로 사용도 가능
     (파일 상단 using 변경을 피하고 싶은 경우). → 사용자 취향에 따라 선택.

2. **`stage1Buildings` 선언(176~187행) → 파생 코드로 교체.**
   - 기존 주석("1단계 생산건물 목록 …")은 파생 의도를 설명하도록 갱신.

3. **`nonProductionBuildings` 선언(218~226행) → 파생 코드로 교체.**
   - 기존 주석(214~217행)의 "대상 enum … 1:1 일치(Castle 제외)" 설명은 유지하되,
     이제 목록을 손으로 적지 않고 lookup table에서 파생함을 명시하도록 보강.

4. **두 배열을 사용하는 `foreach` 루프(189~208행, 227~234행)는 변경 없음** — 변수명과 타입(`BuildingType[]`)이 동일하게 유지되므로 그대로 동작.

### 주석은 상세하게 (CLAUDE.md 규칙 8)
- 파생 코드 위에 "왜 손으로 나열하지 않고 lookup table에서 뽑는가(단일 소스 통합)"를 초급 개발자도 이해하도록 설명하는 주석을 단다.

---

## 수정 파일 목록

### 안 2 선택 시 (권장)
```
[수정]
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs
    · using System; 추가 (또는 전체 경로 사용)
    · stage1Buildings 배열 → Array.FindAll 파생 코드로 교체
    · nonProductionBuildings 배열 → Array.FindAll 파생 코드로 교체
    · 관련 주석 갱신
```
→ `BuildingTypeHelper.cs`는 **무수정**.

### 안 1 선택 시
```
[수정]
- Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs
    · GetAllStage1Buildings() 추가
    · GetNonProductionBuildingsExceptCastle() 추가
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs
    · 두 하드코딩 배열 → 위 메서드 호출로 교체
```

---

## 위험 요소 및 대응

| 위험 | 대응 |
|------|------|
| `Castle = 0` 누락 시 Castle에 환불 캐시 채워짐 | 비생산 필터에 `t != BuildingType.Castle` 명시 유지 (동작 무해하나 의도 보존) |
| `using System;` 부재로 컴파일 에러 | `using System;` 추가 또는 `System.Enum`/`System.Array` 전체 경로 사용 |
| enum 순서 의존 우려 | 환불 캐시는 건물별/체인별 독립 계산 → 순서 무관, 결과 동치 |
| lookup table 부정확 시 파생 결과 오류 | 현재 table이 두 배열과 정확히 일치함을 Research에서 검증 완료 |

---

## 검증 기준 (구현 후)

- 두 배열 → 파생 코드 교체 후, 생성되는 1단계 목록(9개) / 비생산 목록(6개)이 기존과 동일해야 한다.
- 환불 캐시 값(`BuildingStats.GetTotalInvestedCost`)이 종족·건물별로 변경 전과 동일해야 한다.
- 컴파일 에러 없음 (`Enum`/`Array` 네임스페이스 해결).

> 본 검증은 사용자가 명시적으로 TC/QA를 요청한 경우에만 Testcase.md로 진행한다 (WORKFLOW.md [5-1]).

---

## 사용자 결정 필요 항목 (요약)

1. **안 1 vs 안 2** 중 선택 (권장: 안 2).
2. 안 2 선택 시: `using System;` 추가 방식 vs `System.Enum`/`System.Array` 전체 경로 방식.
3. 배열을 곧바로 교체할지(권장), 보수적으로 주석 처리 후 테스트 통과 시 삭제할지.
