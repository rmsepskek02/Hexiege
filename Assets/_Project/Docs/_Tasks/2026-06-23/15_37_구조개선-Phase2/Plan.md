# Plan — 코드 구조 개선 Phase 2

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

이 문서는 Research.md에서 정리한 두 가지 구조 문제를 "실제로 어떻게 고칠지" 정리한 계획서입니다.
목표는 **게임 동작은 전혀 바꾸지 않으면서** 코드 구조만 정리하는 것입니다(동작 보존 리팩토링).

1. **건물 정보를 표 하나로 모으기**
   건물별 정보(생산 건물 여부 / 단계 / 다음 단계)가 세 곳의 분기문에 흩어져 있던 것을,
   "건물 하나 = 정보 한 줄"인 표(Dictionary) 하나로 합칩니다.
   앞으로 건물을 추가할 때 이 표에 한 줄만 추가하면 세 가지 질문에 대한 답이 자동으로 맞춰집니다.

2. **헥스 타일 설정 중복 제거**
   멀티플레이 시작 시 같은 설정을 두 번 적용하던 것을, 한 번만 적용하도록 정리합니다.

> 두 작업 모두 외부에서 호출하는 함수의 이름·입력·출력은 그대로 유지하므로,
> 이 함수들을 사용하는 다른 코드는 수정할 필요가 없습니다.

---

## ⚠️ 기존 로직 제거 관련 사항 (WORKFLOW.md 규칙 — 최상단 명시)

이번 작업에서 "기존 로직 제거"에 해당하는 항목과 처리 방침:

| 제거 대상 | 위치 | 제거 안전 근거 | 처리 방식 |
|-----------|------|----------------|-----------|
| `IsProductionBuilding` / `GetStage` / `GetNextStage`의 switch 본문 | BuildingTypeHelper.cs:32~170 | 새 lookup table이 동일한 입출력을 보장하면 기능 동치. 단, 검증 전까지는 안전 보장 불가 | **1차: 비활성화(주석 처리) 후 신규 table 기반 구현 병기** → 사용자 테스트 통과 후([6]) 최종 삭제 |
| `StartNetworkGame`의 HexMetrics 수동 설정 4줄 | Network.cs:64~67 | `ApplyConfig`가 동일 값 + UnitYOffset까지 설정하므로 중복. 단, 호출 시점(ViewConverter 사전 설정) 제약 유지 필요 | **1차: 대체 메서드로 치환**, 기존 4줄은 주석 처리 → 테스트 통과 후 삭제 |

> 주석 처리된 기존 로직의 **최종 삭제는 [6] 사용자 테스트 통과 후 → [7] 문서/메모리 업데이트 전**에 수행한다.
>
> **[2026-06-25 현황]** 사용자 테스트(SINGLE 7 + MULTI 2, 전 항목 PASS) 통과 완료. 단,
> - `BuildingTypeHelper.cs`의 주석 처리된 switch 3개 본문은 **별도 지시가 있을 경우에만** 삭제(사용자 지침). 현재 보존 중.
> - `Network.cs`의 주석 처리된 HexMetrics 수동 4줄은 위와 동일하게 보존 중(별도 삭제 지시 시 정리).

---

## ⚠️ 구현 착수 전 사용자 확인 필요 사항 (CLAUDE.md 규칙 10·12)

아래 3개는 추정으로 진행하지 않고, 구현 시작 전에 사용자에게 확인한다.

1. **`PrimalSanctuary` 생산 건물 포함 여부** — **[2026-06-25 해소]**
   초기 Research에서 `IsProductionBuilding`에 `PrimalSanctuary`가 빠진 것으로 의심했으나, qa-tester 정적 분석 결과
   기존 `IsProductionBuilding` switch에도 포함되어 있었음이 확인되었다(`GetStage`/`GetNextStage`와 정합, 세 곳 모두 존재).
   따라서 "의도된 제외"/"누락 버그" 판단은 전제 자체가 잘못된 것이었으며, 데이터 불일치는 없었다.
   → 결론: table에 `IsProduction=true, Stage=3, NextStage=null`로 **기존 동작을 그대로 보존**하여 명시 포함.
   BuildingType.cs:68 주석 `// 동물A 3단계 (제작 예정)`은 3D 에셋 제작 상태 메모로, 생산 판정과 무관하다.

2. **`GameBootstrapper.Setup.cs` 하드코딩 배열 통합 범위**
   stage1Buildings(:176~187), nonProductionBuildings(:218~226)을 신규 table에서 파생시킬지 여부.
   - 권장: 이번 Phase는 BuildingTypeHelper 내부 정리에 집중하고, 배열 파생은 별도 작업으로 분리(범위 초과 방지 — CLAUDE.md 규칙 6).
   → 사용자 판단 필요.

3. **HexMetrics 중복 제거 방식** (아래 2번 항목의 2가지 안 중 택1) → 사용자 선택 필요.

---

## 작업 1 — BuildingTypeHelper switch → lookup table 전환

### 근거 규칙
- `GameSystemRules_Buildings.md` 건물 철거 시스템 **규칙 5(생산 큐 처리 — 생산 건물 한정)**:
  "생산 건물"의 정의가 코드에서 `IsProductionBuilding`으로 판정되므로, 이 판정의 정확성이 규칙 준수의 전제다.
  lookup table로 통합하면 단계/생산여부/다음단계가 항상 정합하게 유지되어 이 규칙의 신뢰성이 높아진다.
- `GameSystemRules_Buildings.md` 건물 철거 시스템 **규칙 4(골드 환불)**:
  환불 누적 비용은 `GetNextStage` 체인 순회로 계산된다(Setup.cs:201). 체인 정의가 단일 소스가 되면 환불 계산의 일관성이 보장된다.
- 본 작업은 위 규칙들의 **동작을 바꾸지 않고**, 그 동작을 떠받치는 데이터 정의를 한 곳으로 모으는 구조 개선이다.

### 변경 방식
BuildingTypeHelper 내부에 건물 메타데이터 단일 정의를 추가하고, 세 메서드가 이 정의를 조회하도록 변경한다.

**(1) 메타데이터 레코드 정의 (신규, BuildingTypeHelper.cs 내부 private)**
건물 1종의 정보를 한 줄로 담는 구조:
- `IsProduction` (bool) — 생산 건물 여부
- `Stage` (int) — 1/2/3, 비생산은 0
- `NextStage` (BuildingType?) — 다음 단계, 없으면 null

**(2) 정적 Dictionary 정의 (신규)**
`Dictionary<BuildingType, BuildingMeta>` 형태로 생산 건물 라인 전체를 한 곳에 선언.
Research.md의 "라인별 단계 체인" 표를 그대로 코드 테이블로 옮긴다.
- 비생산 건물(7종)은 table에 미등록 → 조회 실패 시 기본값(IsProduction=false, Stage=0, NextStage=null) 반환.
  (즉 default 동작을 "table 미등록 = 비생산"으로 일원화.)

**(3) 세 메서드를 table 조회로 재작성**
| 메서드 | 변경 후 동작 |
|--------|--------------|
| `IsProductionBuilding(t)` | table에 있고 `meta.IsProduction == true`면 true, 아니면 false |
| `GetStage(t)` | table에 있으면 `meta.Stage`, 없으면 0 |
| `GetNextStage(t)` | table에 있으면 `meta.NextStage`, 없으면 null |

- `CanUpgrade`, `CanShowActionPanel`은 위 세 메서드를 호출하므로 **수정 불필요** (자동으로 새 table 기반 동작).
- 메서드 시그니처/반환 타입/네임스페이스 전부 동일 유지 → 호출부 무영향.

**(4) 기존 switch 처리**
1차 구현 시 기존 switch 본문은 주석으로 비활성화하여 table 결과와 대조 가능하게 둔다.
사용자 테스트 통과 후 최종 삭제.

### 수정 파일
- `Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs` (단일 파일)

### 위험 요소
- 표 작성 시 한 줄이라도 라인/단계를 잘못 적으면 동작이 바뀐다 → table을 Research.md의 검증된 표와 1:1 대조.
- `PrimalSanctuary` 정의는 위 "확인 필요 사항 1"의 사용자 결정에 따라 작성.
- Domain 레이어 규칙(순수 C#, Unity 의존 없음) 준수 — Dictionary/record는 순수 C#이므로 문제없음.

---

## 작업 2 — HexMetrics 중복 setup 제거

### 근거 규칙
- 이 변경은 게임 시스템 동작 규칙이 아니라 **부트스트랩 초기화 순서**에 관한 것으로, `GameSystemRules`에 직접 대응하는 규칙은 없다.
  (GameSystemRules는 게임플레이 동작 규칙을 다루며, 엔진 초기화 절차는 다루지 않음 — GameSystemRules.md 인덱스 확인 결과.)
- 따라서 본 작업의 기준은 **동작 보존**(멀티/싱글 양쪽에서 ViewConverter·카메라·렌더링 결과가 변경 전과 동일)이다.

### 변경 방식 (2가지 안 — 사용자 선택)

**안 1: ApplyConfig를 사전 설정 시점에 재사용**
`StartNetworkGame`의 수동 4줄(Network.cs:64~67)을 `ApplyConfig(HexOrientation.FlatTop, oc)` 호출로 대체.
- 장점: 설정 코드가 `ApplyConfig` 한 곳으로 단일화. `UnitYOffset` 누락도 자동 해소.
- 고려: `LoadMap` 내부에서 `ApplyConfig`가 한 번 더 호출되므로, 멀티플레이에서 `ApplyConfig`가 2회 실행된다.
  단 `ApplyConfig`는 멱등(idempotent — 같은 값을 다시 대입할 뿐)이라 부작용 없음.
  (현재도 동일 값이 2회 설정되는 상태이므로 동작 동치.)

**안 2: ViewConverter 사전 설정 전용 경량 메서드 분리**
HexMetrics를 FlatTop으로 준비 → GridCenter 계산 → ViewConverter.Setup까지를 묶은 private 헬퍼를 만들어
`StartNetworkGame`이 호출. 내부적으로 `ApplyConfig`를 재사용.
- 장점: "ViewConverter 사전 설정"이라는 의도가 메서드 이름으로 드러남.
- 단점: 메서드가 하나 늘어남.

> **권장: 안 1** (변경 최소, 단일화 효과 동일, 멱등성으로 안전). 단 최종 선택은 사용자 승인.

### 수정 파일
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Network.cs` (수동 4줄 → ApplyConfig 호출)
- (안 2 선택 시) `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs`에 헬퍼 추가

### 위험 요소 (핵심 제약)
- **순서 제약 유지 필수**: ViewConverter 사전 설정(`GridCenter` 계산)은 반드시 HexMetrics가 FlatTop으로 준비된 *이후*, `LoadMap` *이전*에 일어나야 한다 (Network.cs:54~56 주석).
  → 대체 후에도 "HexMetrics 적용 → GridCenter → ViewConverter.Setup → LoadMap" 순서가 깨지지 않는지 확인.
- 싱글플레이 경로(Map.cs:82~87의 ViewConverter 설정)는 이번 변경 대상이 아니다 — 건드리지 않는다 (범위 초과 방지).
- `ApplyConfig`는 현재 `private`. `StartNetworkGame`은 같은 partial class 멤버이므로 접근 가능 — 가시성 변경 불필요.

---

## 구현 담당 에이전트
- 코드 수정: **game-programmer** (CLAUDE.md 규칙 3 — 코드는 직접 수정하지 않고 위임)
- 본 Plan은 동작 보존이 핵심이므로, 변경 후 동치성 확인을 위해 사용자 테스트 또는 qa-tester 검증이 권장됨(사용자 지시 시).

## 수정 파일 요약
[수정 예정]
- Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs (작업 1)
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Network.cs (작업 2)
- (안 2 선택 시) Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs (작업 2)

[영향 없음 — 시그니처 유지로 미수정]
- BuildingTypeHelper 호출부 전체 (UI/입력/Bootstrap)
- GameBootstrapper.Map.cs의 LoadMap/ApplyConfig 흐름 (작업 2 안 1 선택 시 미수정)
