# Plan — 코드 정리(클린업) Phase 1

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

이 계획은 Research.md에서 확인한 "히스토리성 주석 / 폐기 코드 설명 / 빈 섹션 헤더 / 중복 배열"을 실제로 어떻게 손볼지 파일·라인 단위로 정리한 것입니다. 게임 동작은 그대로 두고, 코드를 읽기 좋게 만드는 것이 목적입니다.

작업 대상은 **Phase 1(즉시 클린업)** 만입니다. 구조 변경(switch→Dictionary 등)은 Phase 2로 별도 진행합니다.

---

## ⚠️ 기존 로직 제거 규칙 — 최상단 명시 (WORKFLOW.md [4])

- 이번 작업에서 **삭제하는 대상은 전부 주석 또는 빈 줄**이며, 실행되는 로직은 제거하지 않는다.
- 유일한 코드 변경인 "중복 RaceId 배열 정리"는 **동작이 100% 동일한 리팩토링**이며, 로직 제거가 아니다(지역 변수 1개 추가 + 두 곳 참조 교체).
- 따라서 "비활성화 후 검증" 절차가 필요한 로직 제거는 이번 작업에 **없다**.
- 단, `_enableAI` 블록(GameBootstrapper.cs 71~77행)은 이미 "사용자 테스트 통과 후 제거 예정"으로 보류된 주석이므로, **이번 클린업에서 건드리지 않고 현행 유지**하는 것을 기본안으로 한다(아래 항목 B 참조).

---

## GameSystemRules 근거 (WORKFLOW.md [4] 필수 항목)

`GameSystemRules.md` 인덱스 및 하위 규칙 파일을 확인한 결과, 이번 작업은 **UI/유닛/건물/AI/사운드 등 어떤 게임 시스템 규칙도 변경하지 않는다.** 주석·빈 줄·중복 배열 정리는 게임 동작 규칙과 무관한 순수 코드 청소이기 때문이다.

- 단, 개선 대상 4(RaceId 배열)는 `InitializeBuildingStatsFromConfig()`의 건물 환불 캐시 계산 로직 안에 있다. 이 로직은 `GameSystemRules_Buildings.md`의 "건물 철거 → 골드 환불(투자 비용 50%)" 규칙을 구현하는 부분이므로, **개선 후에도 (건물×종족) 환불 캐시가 동일하게 채워지는지** 반드시 동작 동일성을 보장해야 한다. (값/순서 변화 금지)

---

## 작업 전 확정 필요 항목 (Research.md "확인 1·2" 연동)

아래 3가지는 사용자 결정 후 Plan을 확정한다. 본 Plan은 **각 항목의 권장안**을 기본값으로 기술한다.

1. **정리 범위** → 권장: 우선 요청서 명시 5개 파일 + 빈 헤더 + 중복 배열만 (안전·검증 용이). 전체 확장은 별도 라운드.
2. **`_enableAI`/`_confirmPopup` 처리** → 권장: 둘 다 "날짜 라벨만 제거, WHY 본문 보존" (B·C 참조).
3. **RaceId 중복 개선 방식** → 권장: 지역 변수 방식(D-안 a). enum 정의 확인 전까지 `Enum.GetValues` 미사용.

---

## 파일별 변경 계획

### A. 날짜/단계 형식 변경 이력 주석 제거 (요청서 5개 파일)

처리 원칙:
- **단독 이력 주석**(예: `// [2026-05-20] ActionDisposable 내부 클래스 제거.`) → 줄 통째 제거.
- **블록/섹션 헤더 + WHY 설명**(예: 혼잡도 시스템 블록) → `[날짜]` 토큰만 제거, 설명 본문은 보존.
- **번호 매긴 단계 주석**(Map.cs 167·171행 `// 15.` `// 16.`) → 번호와 설명은 보존, `[2026-04-30]`·`[로딩 인디케이터 끄기]` 같은 라벨 토큰만 정리.

| 파일 | 라인 | 처리 |
|------|------|------|
| `GameBootstrapper.cs` | 41~43 | 블록 통째 제거(using 변경 이력) |
| `GameBootstrapper.cs` | 79 | 줄 제거(Phase 2 이력) |
| `GameBootstrapper.cs` | 216 | `[2026-05-15]` 토큰만 제거, 블록 설명 보존 |
| `GameBootstrapper.cs` | 240 | `[2026-04-30]` 토큰만 제거, 블록 설명 보존 |
| `GameBootstrapper.cs` | 468 | `[2026-05-20]` 토큰만 제거, WHY 보존 |
| `GameBootstrapper.cs` | 476~478 | 블록 통째 제거(제거 완료 이력) |
| `GameBootstrapper.Map.cs` | 130 | `[2026-05-20]` 토큰만 제거, 주입 설명 보존 |
| `GameBootstrapper.Map.cs` | 167 | `[2026-04-30]` 토큰만 제거, `15.`+설명 보존 |
| `GameBootstrapper.Map.cs` | 171 | `[로딩 인디케이터 끄기]` 라벨만 제거, `16.`+설명 보존 |
| `GameBootstrapper.Map.cs` | 195·208·216 | `[날짜]` 토큰만 제거, 설명 보존 |
| `GameBootstrapper.Setup.cs` | 308·319·514 | `[날짜]` 토큰만 제거, 설명 보존 |
| `HexGrid.cs` | 49 | 섹션 헤더에서 `[2026-05-20]`만 제거 |
| `HexGrid.cs` | 83·167·232 | XML 주석에서 `[2026-05-20]`만 제거 |
| `UnitMovementUseCase.cs` | 40·138 | `[2026-05-20]` 토큰만 제거, 설명 보존 |
| `UnitMovementUseCase.cs` | 148 | 섹션 헤더에서 `[2026-04-30]`만 제거, `새 규칙 11/15` 보존 |
| `BuildingType.cs` | 25 | `[2026-05-20]` 토큰만 제거. 26~28행 WHY 설명은 보존 |

### B. `_enableAI` 블록 (GameBootstrapper.cs 71~77행)

- **기본안(권장):** 현행 유지. 이미 "사용자 테스트 통과 후 제거 예정"으로 의도적으로 남긴 비활성화 코드이므로 이번 클린업 범위에서 제외.
- **대안(사용자 승인 시):** `[AIConfig 이전]` 설명(71~74행)은 "AI 토글이 왜 AIConfig로 이동했는가"를 알려주는 WHY이므로 1~2줄로 압축 보존하고, 주석 처리된 코드(75~77행)는 제거.

### C. `_confirmPopup` 블록 (GameBootstrapper.cs 174~178행)

- **기본안(권장):** `[2026-06-18]` 날짜 토큰만 제거하고, "이 필드가 없는 이유 / 확인 팝업을 어디서 얻는지"를 설명하는 본문(175~178행)은 보존. 이는 dead 참조 재도입을 막는 유용한 WHY 주석이다.
- **대안(사용자 승인 시):** 블록 통째 제거.

### D. 빈 섹션 헤더 제거 (NetworkGameFlow.cs 42~44행)

- `// === / Inspector 설정 / === ===` 3줄을 제거하고, 바로 아래 `내부 상태` 섹션 헤더만 남긴다. (그 사이 빈 줄 1개도 정리)
- 런타임 영향 없음.

### E. 중복 RaceId 배열 개선 (GameBootstrapper.Setup.cs)

**기본안(권장, a — 지역 변수):**
- `InitializeBuildingStatsFromConfig()` 안, 환불 캐시 계산이 시작되기 전(예: 165행 `BuildingStats.Initialize(dict);` 직후)에 1회 선언:
  ```csharp
  // 환불 캐시 계산에서 반복 사용할 종족 목록. 동일 배열을 두 곳에서 새로 만들지 않도록 한 번만 선언.
  var refundRaces = new[] { RaceId.Human, RaceId.Spirit, RaceId.Transcendence };
  ```
- 186행, 224행의 `foreach (var race in new[] { RaceId.Human, RaceId.Spirit, RaceId.Transcendence })` → `foreach (var race in refundRaces)` 로 교체.
- 원소·순서가 기존과 동일하므로 환불 캐시 결과가 완전히 같다(동작 보장).

**대안(b — `Enum.GetValues`, 사용자 승인 + enum 정의 확인 시):**
- `RaceId` enum이 정확히 `Human, Spirit, Transcendence` 3개뿐임을 확인한 경우에만 `(RaceId[])System.Enum.GetValues(typeof(RaceId))` 사용.
- ⚠️ `None`/`Neutral` 등 다른 멤버가 있으면 환불 캐시에 불필요/잘못된 종족이 추가되어 동작이 달라진다 → 확인 전 채택 금지.

---

## 변경 후 기존 기능에 미치는 영향

- A·B·C·D 항목: 전부 주석/빈 줄 변경. 컴파일 산출물 불변 → **런타임 동작 100% 동일**.
- E 항목: 동일 원소·순서의 배열을 지역 변수로 한 번만 생성하여 재사용. `BuildingStats.SetTotalInvestedCost(...)`에 들어가는 (건물, 종족, 비용) 조합이 변경 전과 완전히 동일 → **환불 캐시 결과 불변**.
- 멀티플레이/직렬화 영향: `BuildingType.cs`는 주석만 건드리므로 enum 정수값·순서 불변 → RPC 직렬화 호환성 영향 없음.

---

## 구현 및 검증 절차 (CLAUDE.md 규칙 3·11)

1. 위 "확정 필요 항목 1·2·3"에 대한 사용자 결정 수령.
2. 코드 변경은 **game-programmer 에이전트에 위임**(주석 제거 + 중복 배열 정리).
3. 변경 후 컴파일 통과 및 `InitializeBuildingStatsFromConfig()` 환불 캐시 동작 동일성 확인.
4. (사용자가 명시적으로 요청한 경우에 한해) Testcase.md / QA 진행.

## 작업 브랜치

`claude/code-refactor-cleanup-jsa24o`
