# 무작위 맵 2단계 — 구현 계획

> 조사 기록은 같은 폴더 `Research.md`. 범위표의 단일 소스는 그 문서 §0 이다.
> 규칙의 단일 소스는 `Assets/_Project/Docs/GameSystemRules/GameSystemRules_RandomMap.md`(이하 **규칙 문서**)이며,
> 기술 계약은 `Assets/_Project/Docs/TechnicalDesignDocument.md`(이하 **TDD**) 「무작위 맵 시작 동기화」 절이다.
> **두 곳이 어긋나면 규칙 문서가 옳다**(TDD 자신이 그렇게 적고 있다).

---

## 0. 이 작업이 무엇이고 왜 하는지 (자연어 설명 — CLAUDE.md 규칙 13)

지금 이 게임의 전장은 **매 판 똑같습니다.** 성이 놓이는 자리도, 광산이 있는 자리도 코드에 좌표가 박혀 있어서 몇 번을 새로 해도 같은 지형이 나옵니다.

이번 작업은 **매 판 다른 전장이 나오게** 만듭니다. 다만 아무렇게나 다른 것이 아니라, **두 플레이어에게 정확히 공평한** 전장이어야 합니다. 그래서 세 가지를 함께 만듭니다.

1. **맵을 만들어내는 기계** — 다섯 가지 성격의 맵(탁 트인 벌판 / 바위가 흩어진 벌판 / 가운데가 좁아지는 협곡 / 가운데가 막혀 좌우로 도는 형태 / 세 갈래로 갈라지는 형태)을 무작위로 만듭니다. 만들 때 **한 칸을 놓으면 반대편 대칭 자리에도 자동으로 같은 것이 놓이도록** 강제하는 장치를 거칩니다.
2. **만들어진 맵을 검사하는 기계** — 만든 쪽을 믿지 않고 결과만 보고 다시 확인합니다. 정말 대칭인지, 양쪽이 성 근처에 지을 자리를 똑같이 10칸씩 갖는지, 광산까지 가는 거리가 양쪽이 같은지, 좁은 통로가 막히지 않았는지를 봅니다. 하나라도 어긋나면 그 맵은 버리고 다시 만듭니다. **최대 100번까지** 시도하고, 그래도 안 되면 미리 만들어 둔 **예비 맵**을 씁니다.
3. **만든 맵으로 실제 경기가 돌아가게 하는 일** — 만들어진 설계도를 실제 전장으로 옮기고, 막힌 곳은 화면에서 빈 공간으로 그리고, 지을 수 없는 곳에는 빗금을 얹고, AI가 지을 수 없는 자리에 건물을 지으려 들지 않게 고칩니다.

**이번 단계가 끝났다고 말할 수 있는 조건은 하나입니다 — 싱글 경기에 들어갔을 때 매번 다른 맵이 나오고, 그 맵에서 게임이 정상적으로 돌아가는 것.** 화면을 보고 확인할 수 있습니다.

**멀티플레이는 이번에 하지 않습니다.** 만든 맵을 상대에게 보내고 양쪽이 같은지 대조하는 일은 3단계입니다.

---

## 🔴 1. 기존 로직 제거·대체 — 먼저 읽을 것 (`WORKFLOW.md` [4] 「기존 로직 제거 규칙」)

이번 작업은 **현재 고정 맵을 만드는 코드를 무작위 맵으로 대체**한다. 지우는 코드가 있으므로 규정대로 여기 최상단에 적는다.

| # | 대상 | 처리 | 안전한 근거 |
|---|---|---|---|
| 제거-1 | `GameBootstrapper.Map.cs` 의 **하드코딩 좌표로 성·광산을 놓는 구간**(`PlaceCastles` · `PlaceGoldMines` 와 그 안의 `startingMines`·`neutralMines` 배열) | 🔴 **삭제하지 않고 주석 비활성화.** 최종 삭제는 [6] 사용자 실기 통과 후 · [7] 문서 갱신 전 | 대체 경로(`MapDefinition` 투영)가 **실기로 검증되기 전까지는 되돌릴 수단을 남겨야** 한다. 1단계에서 「Plan 이 지우라고 한 배열을 실제로는 못 지웠다」(시작 채굴소 자동 건설이 계속 참조)는 일이 있었고, 같은 부류의 숨은 참조가 또 있을 수 있다 |
| 제거-2 | `GameConfig.asset` FlatTop `GridWidth: 10` / `GridHeight: 20` | **11 / 21 로 변경**(삭제 아님) | 규칙 1 이 11×21 로 확정. 근거는 `Research.md` §2 |
| 제거-3 | `GameConfig.cs:77~78` 코드 기본값 `10 × 29` | **11 / 21 로 변경** | 실행에는 `.asset` 이 쓰이지만 남겨 두면 **세 번째 값**이 계속 존재한다(`Research.md` §2-1) |
| 제거-4 | `BuildingPlacementUseCase` 의 **일반 건물 배치 판정이 `tile.IsWalkable` 을 쓰는 자리**(`:75` · `:217` · `:237`) | **조건 교체**(제거 아님) | TDD 「기존 코드 전환 요구」 5번의 뒤 절반. 규칙 문서 5장 판정표가 단일 소스 |
| 제거-5 | `AIOpponentController.cs:807~809` 배치 후보 판정 + `:770~773` XML 주석 | **조건 교체**(제거 아님) | TDD 「기존 코드 전환 요구」 6번 · `GameSystemRules_AI.md` 규칙 26. **코드와 주석을 반드시 함께** 옮긴다 |

> **제거-1 의 주석 처리 구간에는 `// [2단계 대체 대기]` 표식을 단다.** 최종 삭제 시 `grep` 으로 한 번에 찾기 위함이며, 삭제 후 0건이 되는 것이 완료의 기계적 증거다(1단계 진단 로그의 `Diag=` 표식과 같은 방식).

---

## 2. 완료 판정

| | 내용 |
|---|---|
| **주 판정** | 싱글 경기 진입 시 **매번 다른 맵**이 나오고, 그 맵에서 유닛 생산·이동·전투·건설·철거가 정상 동작한다 |
| 부 판정 1 | 같은 seed 를 주면 **같은 맵**이 재현된다(규칙 12) |
| 부 판정 2 | 폴백 템플릿 5개가 **전부 검증기를 통과**한다(규칙 12 — 빌드·에디터에서 상시 전수 검증) |
| 부 판정 3 | 생성 로그에 규칙 12 의 필수 항목 중 **2단계에서 값이 생기는 11가지**가 남는다(전송/재전송 횟수 · Host/Client 해시 비교 결과는 3단계에서 값이 생긴다) |
| **범위 밖** | 멀티 전송·해시 대조·실패 UI·재경기(`SameMap`/`NewMap`) — 3단계 |

---

## 3. 작업 순서 — 의존 관계

앞 단계 산출물이 뒤 단계의 입력이므로 **순서를 바꿀 수 없다.** A~D 는 순수 C# 이라 Unity 실행 없이도 검증 가능하고, E 부터 화면에 영향이 간다.

```
A. 결정적 PRNG + 설정 필드          ← 다른 모든 것의 입력
        ↓
B. SymmetricMapBuilder              ← 생성기가 통과해야 하는 유일한 경계
        ↓
C. InitialMapStateEvaluator         ← 생성기(보호 10타일 제외)와 검증기(3번) 양쪽이 쓴다
        ↓
D. 생성기 5종 + 중립 광산 샘플링  ─┬→ E. MapDefinitionValidator
                                    │      (D 없이는 검사할 대상이 없다)
                                    ↓
                                 F. 폴백 템플릿 5개 + 제작 도구
                                    (E 를 통과해야 만들어졌다고 할 수 있다)
        ↓
G. 맵 준비 조정자 (싱글 로컬 권위) ← A~F 를 묶어 「최대 100회 시도 후 폴백」을 실행
        ↓
H. GameConfig 11×21 + MapDefinition → HexGrid 투영   🔴 여기서 처음 화면이 바뀐다
        ↓
I. 판정 조건 전환 (건설·점령·AI)   ← H 로 NoBuild/Blocked 타일이 실제로 생긴 뒤라야 의미가 있다
        ↓
J. 렌더러 + 클릭 판정 순서
        ↓
K. 로그 키
```

> **H 를 D~F 앞으로 당기지 않는다.** 격자만 11×21 로 바꾸고 무작위 맵이 없으면, 고정 맵이 낯선 크기에서 도는 **아무도 원하지 않는 중간 상태**가 오래 유지된다. H 는 투영 코드와 **같은 묶음**으로 처리한다.

---

## 4. 단계별 계획

### A. 결정적 PRNG + 설정 필드

**규칙 근거**: 규칙 12(같은 seed → 같은 맵) · 규칙 3(경기 선택 단계) · TDD 「결정적 PRNG 및 독립 스트림 계약」

| 파일 | 내용 |
|---|---|
| `Domain/Map/MapRandom.cs` (신설) | 프로젝트 전용 **고정 정수 PRNG**. `System.Random`·`UnityEngine.Random`·문자열 해시·`GetHashCode()` **사용 금지**(TDD 명시). 정수 폭·오버플로 규칙을 코드에 고정한다 |
| `Domain/Map/MapRandomStreams.cs` (신설) | 4스트림 상수 ID(`MapSelection`/`Terrain`/`MinePlacement`/`Decoration`)와 파생 함수. **스트림 이름을 런타임 문자열 해시로 바꾸지 않는다** — 스키마 고정 정수 ID |
| `Infrastructure/Config/GameConfig.cs` | `MapTestModeEnabled`(bool) · `TestStartingGold`(int, 기본 5000) 필드 추가. 이름은 규칙 3·규칙 12·TDD 가 이미 그 이름으로 참조하므로 **그대로 쓴다** |
| `Resources/Config/GameConfig.asset` | 위 두 필드 직렬화 값 추가(기본: OFF / 5000) |

```text
domainSeed  = Derive(mapVersion, rootSeed, fixedDomainId)
attemptSeed = Derive(domainSeed, attemptIndex)
```

**보장해야 하는 성질**(TDD): Decoration 의 draw 수 변경이 Terrain/MinePlacement 결과를 바꾸지 않는다 · Terrain 과 MinePlacement 가 서로의 draw 수에 결합되지 않는다 · Attempt-N 의 draw 수가 Attempt-(N+1) 시작 상태를 바꾸지 않는다.

> ⚠️ **기존 `_startingGold`(값 5000)와의 관계는 미확정이다**(`Research.md` §9-3). **새 필드를 별도로 만들고 기존 필드는 건드리지 않는다** — 두 값이 같다는 이유로 같은 것이라 단정하지 않는다. 실제로 같은 것이라면 통합은 별도 작업으로 제안한다.

**아키텍처 제약**: `Domain` 은 `Hexiege.Core` 를 참조할 수 없다(`.claude/MEMORY.md`). `MapRandom` 은 `UnityEngine` 도 참조하지 않는 순수 C# 이다(TDD 「생성·검증 실행 모델」).

---

### B. `SymmetricMapBuilder` — 유일한 변경 경계

**규칙 근거**: 규칙 1(180도 회전 정의) · TDD 「`SymmetricMapBuilder` 생성 경계」

`Domain/Map/SymmetricMapBuilder.cs` (신설)

```text
RotateCoord(col, row)              ← 좌표 변환. 계산식의 단일 소스는 규칙 1
    col → 10 - col
    row → 21 - row   (col 이 짝수)
    row → 20 - row   (col 이 홀수)

RotateState(state)                 ← 상태 변환. 팀 전용 상태 Blue↔Red, 장식 회전 단계 ID

SetPair(col, row, state)           ← 두 자리에 원자적으로 기록
SetCenter(state)                   ← (5,10) 전용
```

**계약** (전부 TDD 명시):
- `SetPair` 는 중심 `(5,10)` 입력을 **거부**한다. 중심은 `SetCenter` 로만.
- `SetCenter` 는 중심 외 좌표를 받지 않는다.
- 🔴 **`SetPair` 는 짝 없는 6칸 `(0,0)(2,0)(4,0)(6,0)(8,0)(10,0)` 을 입력으로 받지 않는다.** 회전 상대가 21행이라 맵 밖이다. 이 6칸은 생성이 아니라 **고정값(항상 `Blocked`)** 으로 builder 초기화 시 기록한다.
- generator 에 raw mutable tile buffer 와 회전 상대 직접 수정 API 를 **노출하지 않는다.**
- 🔴 **이름을 나눠 쓴다** — 좌표는 `RotateCoord`, 상태는 `RotateState`. 2026-08-31 이전에 둘 다 같은 이름이어서 읽는 사람이 구분할 수 없었다. **같은 이름으로 되돌리지 않는다.**

> **폐기된 것**: 행열 반전 `(10-col, 20-row)`. 헥스 인접성을 보존하지 못해 공정성 논증이 무너진다(`GameSystemRules_Map.md` 규칙 5 — 400개 방향쌍 붕괴, 231칸 중 126칸 거리 어긋남, 문서 자신의 검증 59.8% 불합격). **이 식을 코드·주석 어디에도 쓰지 않는다.**

---

### C. `InitialMapStateEvaluator`

**규칙 근거**: 규칙 2(즉시 건설 10타일) · TDD 「초기 소유권 단일 소스」

`Domain/Map/InitialMapStateEvaluator.cs` (신설). `MapDefinition` 은 타일별 초기 소유자를 저장·전송·해시하지 **않는다.** 성·시작 광산 위치에서 파생한다.

```text
각 팀의 초기 소유 타일 =
    Castle 타일 + Castle 인접 6타일
  ∪ Starting MiningPost 타일 + 그 인접 6타일
```

**즉시 건설 가능 타일 판정 순서**(TDD 명시):
1. 성·초기 채굴소가 점유한 타일 제외
2. `TileKind == Normal` · `MineKind == None` · `HasBuilding == false` + 기존 소유권 조건
3. 중복 좌표를 한 번만 세어 Blue/Red 각각 **정확히 10개**인지 확인

**이 evaluator 를 세 곳이 공유한다** — 생성기(보호 10타일을 광산 후보에서 제외), 검증기(규칙 13 검증 3번), 런타임 초기 소유권 적용. 같은 계산을 세 벌 만들지 않는다.

---

### D. 생성기 5종 + 중립 광산 샘플링

**규칙 근거**: 규칙 4~8(3장 — 유형별 값의 유일한 자리) · TDD 「archetype generator 알고리즘」 · 「중립 광산 canonical orbit sampling」

`Domain/Map/Generators/` 아래 신설: `IMapArchetypeGenerator.cs`, `OpenGenerator.cs`, `ObstacleOpenGenerator.cs`, `CanyonGenerator.cs`, `OuterGenerator.cs`, `ThreeLaneGenerator.cs`, `NeutralMineSampler.cs`

**대역은 「높이 단계」로 다룬다** — `높이 단계 = 행 × 2 + (홀수 열이면 1)`, 범위 1~41, 회전은 `L ↔ 42-L`, **21이 중앙선**. 행으로 다루면 홀짝 열의 반 칸 시프트 때문에 회전 대칭이 표현되지 않는다.

| 유형 | 뽑는 값 | ③광산 수 | ④광산 금지 | ⑤건설 불가 | ⑥필수 통로 |
|---|---|---|---|---|---|
| 완전개방 | 없음 | 1~6 | 없음 | 없음 | 없음 |
| 장애물 개방 | 행별 0~4(각 20%) · 중앙선 0/2/4(각 1/3) | 1~6 | 없음 | 없음 | 없음 |
| 협곡 | `W ∈ {3,5,7}` · 전환 행 `(11-W)/2` 개 | 1~4 | 단계 18~24 | 단계 18~24 열린 타일 | 1개, 폭 ≥3 |
| 외곽 | `L ∈ {5,7,9,11}` · 최대폭 `{3,5}` · 형태 3종 | 2·4·6 | 단계 18~24 (최대 1쌍) | 단계 18~24 덩어리 바깥 | 2개, 각 ≥3 |
| 3갈래 | `L ∈ {5,7,9,11}` | 1~6 | 레인당 최대 1 | 분리 대역 세 레인 | 3개, **정확히 3** |

> **위 표는 구현자가 한눈에 보라고 옮겨 적은 요약이다. 값이 어긋나면 규칙 문서 3장이 옳다.** TDD 도 같은 방식으로 요약을 두고 같은 단서를 달았다.

**중립 광산 샘플링(공용, 규칙 3)**:
1. 성 · 시작 광산 · 양 팀 보호 10타일(C) · `Blocked` · 유형별 금지 구역(3장 ④)을 제외
2. 남은 좌표 `p` 에서 `(p, RotateCoord(p))` 대응쌍을 만들고 **두 row-major index 중 작은 쪽을 canonical representative** 로 삼아 역순 중복 제거
3. `MinePlacement` 스트림으로 필요한 수만큼 **중심/가장자리 가중치 없이 균등** 선택
4. 홀수 개면 고정 중심 `(5,10)` 단독 광산 추가

- 광산 간 최소 간격·인접 금지 필터 **없음.** 광산끼리 인접한 군집을 허용한다.
- 유형의 **국소** 제약 위반 → 같은 확률 분포에서 다시 뽑는다(rejection sampling).
- **전역** 검증 실패 → 광산을 이동·수선하지 않고 **시도 전체를 거부**한다.

**장식**: 최초 구현은 **빈 목록**을 반환하고 `Decoration` 스트림 draw 를 소비하지 않는다(규칙 15). 다만 `MapDefinition`·codec·builder·validator 는 처음부터 장식 스키마를 처리한다.

---

### E. `MapDefinitionValidator`

**규칙 근거**: 규칙 13(4장) · TDD 「`MapDefinitionValidator` access metric」 · 「필수 통로 validator」

`Domain/Map/MapDefinitionValidator.cs` (신설). **생성기를 신뢰하지 않고 최종 정의만 읽는다.** builder 내부 상태나 "대칭으로 만들었다"는 플래그를 믿지 않는다.

**검증 순서는 고정이며 실패 처리가 항목마다 다르다.** BFS 를 쓰는 4·5번이 가장 비싸므로 뒤에 둔다.

| 순서 | 검증 | 실패 시 |
|---|---|---|
| 1 | 모든 맵 요소·장식의 180도 회전 대칭 + **짝 없는 6칸이 전부 `Blocked` 인지 고정값 검사** | 판 전체 버림 |
| 2 | 유형별 형상 제약(필수 통로 폭 · 광산 수 · 광산 금지 구역) | **다시 뽑기** |
| 3 | 양 팀 즉시 건설 가능 고유 타일 **정확히 10개** | 판 전체 버림 |
| 4 | 성↔성 도달 가능 · 모든 성에서 모든 **광산 덩어리** 도달 가능 | 판 전체 버림 |
| 5 | 교차 접근 거리 · 기하 교차 거리 | 판 전체 버림 |
| 6 | 실제 초기 골드가 규칙 3이 정한 결정 방식과 일치 | 판 전체 버림 |

> 🔴 **1번의 짝 없는 6칸은 「제외」가 아니라 「고정값 검사로 대체」다.** 대응쌍이 없다고 그냥 빠뜨리면 그 6칸이 무엇이든 될 수 있어 **검증에 구멍이 생긴다.**
> 🔴 **6번은 「광산 수 표와 대응하는가」가 아니다.** 그 시점의 모드(정상/테스트)에 규칙 3이 지정한 값과 실제 값이 같은지를 본다. 그래서 A 의 설정 필드가 필요하다.

**접근 거리 계산**:

```text
StaticTraversable = TileKind != Blocked && MineKind == None && !Castle && !StartingPost
```

- 성 접근 칸 = 성에 인접한 `StaticTraversable` 전체 → 거리 0 으로 두고 **multi-source BFS**
- 🔴 **재는 단위는 광산 하나가 아니라 「광산 덩어리」** — 서로 인접한 광산을 한 덩어리로 묶고, 그 덩어리의 **어느 광산에라도 인접한** `StaticTraversable` 전체를 접근 칸으로 쓴다. **접근 칸이 하나도 없는 덩어리가 하나라도 있으면 판 전체를 버린다.**
- 중심 `C`: `Access(B,C) == Access(R,C)`
- 대응쌍 `A`: `Access(B,A) == Access(R,RotateCoord(A))` **와** `Access(R,A) == Access(B,RotateCoord(A))` 를 **모두**
- 같은 교차 등식을 **장애물을 무시한 기하 `HexCoord` 거리**로도 별도 검사

**필수 통로 검증**:
- 광산 배치 **전**: 같은 높이 단계 단면이 규정 폭 이상. 그 열린 타일은 모두 `NoBuild`
- 광산 배치 **후**: 같은 통로·같은 단계에 광산 0개면 이동 가능 폭 ≥3, 1개면 ≥2, **2개 이상은 무효**
- 통로별 start set → end set BFS 로 연속성·의도치 않은 단절 부재 확인

> **모든 우회 경로를 열거하거나 경로 수를 세지 않는다.** 타일 상태 대칭이 이동 그래프 대칭을 함의하므로(참 180도 회전이 인접성을 보존하기 때문), BFS 는 접근 거리와 통로 연속성에만 쓴다.

**⚠️ 크기 검사 추가** — `Research.md` §8-6 이 찾은 설계 빈틈이다. `MapDefinition` 이 `Width`/`Height` 를 생성자 인자로 받고 `Decode` 가 임의 크기를 수용하는데, 규칙 13 목록에 「크기 확인」이 없다. **검증 1번에 `Width == 11 && Height == 21 && Tiles.Length == 231` 을 넣는다.** 규칙 문서 개정 없이 가능한가 여부는 §6-마 참조.

---

### F. 폴백 템플릿 5개 + 제작 도구

**규칙 근거**: 규칙 12 「폴백 — 유형별 고정 템플릿 5개」 · TDD 「deterministic fallback 정의」

| | 내용 |
|---|---|
| 개수 | **맵 유형마다 1개씩, 총 5개.** 「유형 × 광산 수 × A/B」 50케이스 구조는 **폐기됨** |
| 광산 수 | 그 유형이 허용하는 **최대값** — 완전개방 6 / 장애물 6 / 협곡 4 / 외곽 6 / 3갈래 6 |
| 초기 골드 | 그 광산 수에서 파생 — 6→200, 4→400 |
| 시작 광산 A/B | **템플릿에 그대로 고정해 담는다.** 좌우 대응 변환은 **폐기됨**(보호 10타일 집합을 바꿔 규칙 2·13 검증을 깨뜨린다) |
| 데이터 범위 | **윗절반(높이 단계 1~20) + 중앙선(21)만 지정**하고 아랫절반은 builder 의 180도 회전으로 복제 |
| 저장 포맷 | canonical binary 한 종류 |
| 입력 | **`MapType` 하나뿐.** 경기 선택 단계의 광산 수·A/B·초기 골드는 이 경로에서 **템플릿 값으로 대체**된다. 유지되는 것은 맵 유형과 테스트 모드 표식 |
| 검증 | 🔴 **전용 완화 없이 전체 validator 를 통과해야 한다.** 폴백은 「검증을 건너뛰는 비상구」가 아니라 「생성을 건너뛰는 비상구」다. 실패하면 조용히 쓰지 않고 **맵 준비 실패**로 처리 |

**부수 조건 2가지 — 반드시 함께 지킨다**(규칙 12):

1. **제작 도구를 삭제하지 않는다.** → `Assets/Editor/Tools/MapFallbackTemplateBuilder.cs` (영구 보존 폴더 — `WORKFLOW.md` [5-2])
2. **재생성용 원본을 별도 보관한다.** 바이너리만 남으면 포맷 변경 시 손으로 다시 만들어야 한다.

**📌 데이터 파일 위치 — 제 제안** (규칙 문서·TDD 어디에도 없다. `Research.md` §9-5 가 미확인으로 남긴 자리):

```
Assets/_Project/Resources/MapTemplates/            ← canonical binary 5개 (런타임 로드)
Assets/_Project/Docs/_Reference/MapTemplateSource/ ← 재생성용 원본 (윗절반 지정값, 사람이 읽는 형식)
```

이유: 런타임이 읽어야 하므로 `Resources/` 아래여야 하고(기존 `Resources/Config/` 와 같은 관례), 재생성용 원본은 런타임이 읽지 않으므로 빌드에 포함될 이유가 없다. **승인해 주시면 이 위치로 진행하고, 다른 곳을 원하시면 그대로 따르겠습니다.**

---

### G. 맵 준비 조정자 (싱글 로컬 권위)

**규칙 근거**: 규칙 12 · 규칙 3 「경기 선택 단계」 · TDD 「생성·검증 실행 모델」

`Application/UseCases/MapPreparationUseCase.cs` (신설)

```
1. 64-bit root seed 확정
2. MapSelection 스트림으로 경기당 한 번만 선택
     MapType · NeutralMineCount(유형 허용 범위) · StartingMineSide(A/B 50:50)
     정상 모드 InitialGold = 광산 수 표 / 테스트 모드 = 5000
3. attempt 0~99 반복
     attemptSeed 파생 → 생성기 실행 → validator 실행
     통과하면 종료. 2번의 선택값은 이 구간에서 불변
4. 100회 모두 실패 → 유형별 폴백 템플릿 조립 → validator 재실행
     통과하면 사용, 실패하면 맵 준비 실패
5. 로그 기록
```

🔴 **실패한 시도를 이유로 2번을 다시 뽑지 않는다.** 특정 유형·광산 수·A/B 가 더 자주 실패하더라도 재추첨하면 **최초 선택 확률이 왜곡**된다. 재시도에서 바꿀 수 있는 것은 지형 세부 형태와 중립 광산 위치뿐이다(장식은 최초 구현에서 항상 비어 있음).

**실행 모델**(TDD): 초기 구현은 **main thread synchronous**. 로딩 UI 표시 → 1 frame yield 해서 UI 가 실제 렌더되게 → generator/validator 실행. **profiling 근거 없이 비동기화하지 않는다.**

**싱글 권위**: 로컬 `GameConfig` 가 권위다(규칙 3). 멀티 Host 권위는 3단계.

---

### H. `GameConfig` 11×21 + `MapDefinition` → `HexGrid` 투영 🔴

**여기서 처음 화면이 바뀐다.** 위 제거-1~3 이 여기서 실행된다.

| 파일 | 변경 |
|---|---|
| `Resources/Config/GameConfig.asset` | FlatTop `GridWidth: 10 → 11` · `GridHeight: 20 → 21` |
| `Infrastructure/Config/GameConfig.cs:77~78` | 코드 기본값 `10 × 29` → `11 × 21` |
| `Bootstrap/GameBootstrapper.Map.cs` | `LoadMap` 이 `MapPreparationUseCase` 결과를 받아 `HexGrid` 로 투영. 하드코딩 성·광산 배치 구간은 **주석 비활성화 + `// [2단계 대체 대기]` 표식** |
| 신설 | `Application/UseCases/MapProjectionUseCase.cs` — `MapDefinition` → `HexGrid`(`TileKind` 투영) + 성·시작 채굴소·중립 광산 배치 목록 적용 |

**투영 규칙**(TDD 「`HexTile` 런타임 상태 계약」):
- `TileKind` ← `MapDefinition.Tiles[index]`, **경기 중 불변**
- `MineKind` ← 광산 배치 **목록에서 투영**. 별도 직렬화 원본을 병행 유지하지 않는다
- `HasBuilding` ← 건물 배치·철거·파괴에 따른 동적 상태 (1단계에서 이미 이 형태)
- 초기 소유권 ← `InitialMapStateEvaluator`(C) 결과. `MapDefinition` 에 저장하지 않는다

**따라 움직이는 것**(`Research.md` §2-4·2-6): 맵 중심(`HexMetrics.GridCenter`) · 카메라 중심·경계 · 스킬 조준 경계 · `GameBootstrapper.Setup.cs:499·518`·`:395·569·571`.

---

### I. 판정 조건 전환

**규칙 근거**: 규칙 문서 5장 판정표 · TDD 「`HexTile` 런타임 상태 계약」 판정 규칙 · `GameSystemRules_AI.md` 규칙 26

```text
일반 건설 = TileKind == Normal && MineKind == None && !HasBuilding && 기존 소유권 조건
MiningPost = TileKind != Blocked && MineKind != None && !HasBuilding && 기존 인접 팀 타일 조건
점령 가능 = TileKind != Blocked
```

| 파일 | 자리 | 변경 |
|---|---|---|
| `BuildingPlacementUseCase.cs` | `:75` · `:217` · `:237` | 일반 건설 판정을 위 조건으로 |
| 점령 경로 | 실측으로 확인 후 | `TileKind != Blocked` 확인 추가 |
| `AIOpponentController.cs` | `:807~809` | 후보 판정을 이동 가능 여부 → **일반 건설 조건**으로 |
| 같은 파일 | `:770~773` | **XML 주석도 함께 옮긴다.** 한쪽만 고치면 주석이 코드와 어긋난 채 남는다 |

> **`MiningPost` 예외와 기존 건물 상호작용 분기를 건설 불가 처리보다 먼저 판정한다**(규칙 9). 그래야 광산 위 채굴소 예외가 `NoBuild` 에 가로막히지 않는다.

---

### J. 렌더러 + 클릭 판정 순서

**규칙 근거**: 규칙 9 · 규칙 10 · TDD 「`HexGridRenderer`와 입력 표현」 · 「`GridInteractionUseCase` 클릭 판정 순서」

| `TileKind` | 렌더 |
|---|---|
| `Normal` | 현행 그대로 |
| `NoBuild` | **일반 타일과 같은 mesh·높이·소유권 색상** + 표면에 **반투명 짙은 회색 diagonal hatch 3개 overlay**. selection highlight 와 **동시에** 보여야 하며 어느 한쪽이 다른 쪽을 대체하지 않는다 |
| `Blocked` | **standard hex mesh 와 collider 를 생성하지 않는다.** 높낮이 지형도 만들지 않아 결과는 **빈 공간**. 배경·하부 collider 가 raycast 에 잡혀도 선택 대상으로 변환하지 않는다 |

**클릭 판정 순서**(TDD·`GameSystemRules_UI.md` 규칙 5~8 과 **같은 내용** — 한쪽만 고치면 조용히 갈라진다):

```
1. 기존 building action 분기
2. MineKind 기반 MiningPost 자격 분기
3. Blocked   → 선택 불가. Deselect + 배치 패널 닫기. 토스트·TileSelected 없음
4. NoBuild   → 자기 팀: TileSelected+highlight, 건설 패널 안 열고 ToastKey.BuildingNotAllowed
              중립·적: 선택·highlight 만, 토스트 없음
5. Normal    → 현행
```

---

### K. 로그 키

**규칙 근거**: 규칙 12 「로그 필수 항목」 · `Assets/_Project/Docs/LogRules.md`

`Research.md` 가 실측한 대로 **`LogEvent` 37개 중 맵 관련은 0개**다. 새 키가 필요하다.

- 축 B 판정: 맵 준비는 **플레이어 기기에서만 벌어지는 일**이므로 `운영` 자격이 있다 → `GameLog.Ops` + `LogEvent` 키 신설(LogRules 1.5 — `LogEvent` 적용 범위는 운영 로그만)
- 남길 항목 **11가지**(규칙 12 의 14가지 중 전송/재전송 횟수 · Host/Client 해시 비교 결과는 3단계에서 값이 생긴다):
  `MapVersion` · seed · 맵 유형 · 중립 광산 수 · 시작 광산 방향 · 테스트 모드 표식 · 실제 초기 골드 · **생성 소요 시간** · 시도 횟수 · 폴백 사용 여부 · 최종 맵 해시 · 내부 error code
- 🔴 **「생성 소요 시간」의 측정 구간**: **seed 확정 직후부터 최종 맵 확정(검증 통과 또는 폴백 확정)까지의 누적 실경과 시간(ms), 하나의 값만.** 시도별로 쪼개지 않는다. 맵 전송·해시 비교·씬 로드는 **제외**(3단계 범위)

---

## 5. 규칙 근거 매핑 (`WORKFLOW.md` [4] — 각 항목이 어느 규칙에 근거하는지)

| 작업 | 규칙 문서 | TDD |
|---|---|---|
| A PRNG·설정 | 규칙 3 · 12 | 결정적 PRNG 및 독립 스트림 계약 |
| B builder | 규칙 1 | `SymmetricMapBuilder` 생성 경계 |
| C evaluator | 규칙 2 | 초기 소유권 단일 소스 |
| D 생성기 | 규칙 3 · 4~8 · 15 | archetype generator 알고리즘 · 중립 광산 canonical orbit sampling |
| E 검증기 | 규칙 13 · `GameSystemRules_Map.md` 규칙 1~5 | `MapDefinitionValidator` access metric · 필수 통로 validator |
| F 폴백 | 규칙 12 | deterministic fallback 정의 |
| G 조정자 | 규칙 12 · 3 | 생성·검증 실행 모델 |
| H 투영 | 규칙 1 · 10 | `MapDefinition` 정규 데이터 계약 · `HexTile` 런타임 상태 계약 |
| I 판정 | 규칙 9 · 10 · `GameSystemRules_AI.md` 규칙 26 | `HexTile` 런타임 상태 계약 · 기존 코드 전환 요구 |
| J 렌더러 | 규칙 9 · 10 · `GameSystemRules_UI.md` 규칙 5~8 | `HexGridRenderer`와 입력 표현 · `GridInteractionUseCase` 클릭 판정 순서 |
| K 로그 | 규칙 12 | — (`LogRules.md`) |

---

## 6. 위험 요소

**가. 🔴 H 가 되돌리기 가장 어렵다**
격자 크기와 맵 구성 경로를 동시에 바꾸므로, 문제가 생기면 「무작위 맵이 잘못됐나 / 격자 크기가 잘못됐나 / 투영이 잘못됐나」가 섞인다.
→ **완화**: 제거-1 을 삭제가 아니라 주석 비활성화로 두어, 투영 경로를 끄고 옛 경로를 되살리는 것을 **한 줄 토글**로 만든다.

**나. 「지운다고 했다가 못 지우는」 자리**
1단계에서 `startingMines` 배열이 시작 채굴소 자동 건설에 계속 쓰여 삭제하지 못했다(`Research.md` §7). H 에서 같은 일이 생길 수 있다.
→ **완화**: 주석 처리 전에 `PlaceCastles`·`PlaceGoldMines`·`startingMines`·`neutralMines` **각각의 참조를 전수 검색**하고 결과를 Plan 실행 보고에 적는다.

**다. 카메라·줌이 21행 맵을 담는지 미확인**
`Research.md` §9-1. `.asset` 의 `CameraZoomMin 2 / Max 7 / Default 7` 이 커진 격자에 맞는지 실기 확인이 필요하다.
→ **완화**: H 직후 **첫 실기 확인 항목**으로 둔다. 조정이 필요하면 그 자리에서 값만 바꾼다.

**라. 공정성 시뮬레이션은 파이썬이고 Unity 실기가 아니다**
2026-08-26 회차가 다섯 유형 각 600개로 4항목 100%를 확인했지만 **규칙 문서 스스로 「구현 후 C# 검증기로 재측정하기 전까지 "검증 완료"로 표기하지 않는다」**고 적었다.
→ **완화**: E 완성 후 **에디터에서 유형별 대량 생성 → 검증 통과율 측정**을 하고, 그 수치를 `Testcase.md` 가 아니라 이 Plan 의 실행 결과에 적는다. 파이썬 수치와 다르면 **규칙 문서가 아니라 구현을 의심**한다(규칙이 단일 소스).

**마. 검증 「크기 확인」이 규칙 문서에 없다**
E 에서 추가하려는 항목이 규칙 13 목록에 없다. **규칙에 없는 검증을 코드에 넣는 것**이 되므로, 두 가지 중 하나를 골라야 한다.
→ **제 제안**: 코드에 넣고 **규칙 13 에 항목을 추가**한다. 근거는 TDD 「Client 검증 순서」 4번이 이미 「231개 타일」 확인을 요구하고 있어 **의도는 이미 있고 규칙 문서에만 빠진 것**으로 보이기 때문이다. 승인해 주시면 규칙 문서 개정을 이 작업에 포함한다.

**바. 1단계 미완이 여기서 처음 드러난다**
`TileKind` 를 설정하는 코드가 지금 0곳이라 이동 기준과 건설 기준이 갈리지 않았다. D 가 `NoBuild`/`Blocked` 를 만들기 시작하면 I 를 하기 전까지 **AI 가 못 짓는 자리에 지으려 들고, 플레이어도 건설 불가 타일에 지을 수 있다.**
→ **완화**: I 를 H 직후로 배치했다. **D~G 만 끝내고 실기 확인을 시도하지 않는다** — 그 중간 상태는 정상 동작하지 않는 것이 정상이다.

---

## 7. 변경·신설 파일 목록 (예상)

**신설**
```
Assets/_Project/Scripts/Domain/Map/MapRandom.cs
Assets/_Project/Scripts/Domain/Map/MapRandomStreams.cs
Assets/_Project/Scripts/Domain/Map/SymmetricMapBuilder.cs
Assets/_Project/Scripts/Domain/Map/InitialMapStateEvaluator.cs
Assets/_Project/Scripts/Domain/Map/MapDefinitionValidator.cs
Assets/_Project/Scripts/Domain/Map/NeutralMineSampler.cs
Assets/_Project/Scripts/Domain/Map/Generators/IMapArchetypeGenerator.cs
Assets/_Project/Scripts/Domain/Map/Generators/OpenGenerator.cs
Assets/_Project/Scripts/Domain/Map/Generators/ObstacleOpenGenerator.cs
Assets/_Project/Scripts/Domain/Map/Generators/CanyonGenerator.cs
Assets/_Project/Scripts/Domain/Map/Generators/OuterGenerator.cs
Assets/_Project/Scripts/Domain/Map/Generators/ThreeLaneGenerator.cs
Assets/_Project/Scripts/Domain/Map/MapFallbackTemplates.cs
Assets/_Project/Scripts/Application/UseCases/MapPreparationUseCase.cs
Assets/_Project/Scripts/Application/UseCases/MapProjectionUseCase.cs
Assets/Editor/Tools/MapFallbackTemplateBuilder.cs
Assets/_Project/Resources/MapTemplates/*.bytes              (5개 — §F 승인 대기)
Assets/_Project/Docs/_Reference/MapTemplateSource/*          (재생성 원본 — §F 승인 대기)
```

**수정**
```
Assets/_Project/Scripts/Infrastructure/Config/GameConfig.cs
Assets/_Project/Resources/Config/GameConfig.asset
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs
Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs
Assets/_Project/Scripts/Application/Services/AIOpponentController.cs
Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs
Assets/_Project/Scripts/Application/UseCases/GridInteractionUseCase.cs
Assets/_Project/Scripts/Application/Interfaces/ILogSink.cs               (LogEvent 키 추가 — enum 은 :73)
```

---

## 8. 미결 2건의 처리 (2026-09-03 확정)

| # | 항목 | 처리 |
|---|---|---|
| 1 | **폴백 템플릿 데이터 파일 위치** (§F — 어느 문서에도 없음) | ✅ **제안대로 진행.** 바이너리는 `Assets/_Project/Resources/MapTemplates/`, 재생성용 원본은 `Assets/_Project/Docs/_Reference/MapTemplateSource/`. 근거: 런타임이 읽어야 하므로 `Resources/` 아래여야 하고(기존 `Resources/Config/` 관례), 재생성용 원본은 런타임이 읽지 않으므로 빌드에 포함될 이유가 없다 |
| 2 | **검증기 「크기 확인」과 규칙 13 개정** (§6-마) | ⏸ **지금 정하지 않는다.** 규칙 문서를 고치는 일이므로 **E(검증기) 단계에서 실제 필요가 확인된 뒤** 별도 승인을 받는다. 그때까지 규칙 13 은 손대지 않는다. E 착수 시 이 항목을 다시 꺼낸다 |

그 밖의 사항은 규칙 문서와 TDD 에 이미 정해져 있어 이 Plan 은 그것을 구현 순서로 옮긴 것이다.

---

## 9. 실행 기록

| 단계 | 상태 | 비고 |
|---|---|---|
| A 결정적 PRNG + 설정 필드 | ✅ 구현 완료 (컴파일 미검증) | SplitMix64 채택. 검증 벡터 13개를 메인 세션이 **파이썬 독립 구현으로 재대조해 전부 일치** 확인. 자기 검증은 테스트 어셈블리가 프로젝트에 없어 `MapRandomStreams.TryRunSelfCheck` / `AssertSelfCheck`(`[Conditional("UNITY_EDITOR")]`) 내장 방식. 기존 `_startingGold` 는 건드리지 않고 새 필드 2개를 별도 추가 |
| B `SymmetricMapBuilder` | ⏳ | |
| C `InitialMapStateEvaluator` | ⏳ | |
| D 생성기 5종 + 광산 샘플링 | ⏳ | |
| E `MapDefinitionValidator` | ⏳ | 착수 시 §8-2 재확인 |
| F 폴백 템플릿 + 제작 도구 | ⏳ | |
| G 맵 준비 조정자 | ⏳ | |
| H `GameConfig` 11×21 + 투영 | ⏳ | 🔴 여기서 처음 화면이 바뀐다 |
| I 판정 조건 전환 | ⏳ | |
| J 렌더러 + 클릭 판정 | ⏳ | |
| K 로그 키 | ⏳ | |
