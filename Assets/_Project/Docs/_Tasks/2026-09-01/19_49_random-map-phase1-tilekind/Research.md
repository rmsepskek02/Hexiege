# Research — 무작위 맵 1단계: `TileKind` 도입과 `HexTile` 상태 계약 전환

작성일: 2026-09-01
작업 폴더: `Assets/_Project/Docs/_Tasks/2026-09-01/19_49_random-map-phase1-tilekind/`

---

## 0. 이 작업이 무엇이고 왜 하는지 (자연어 설명 — CLAUDE.md 규칙 13)

지금 게임은 맵이 하나로 고정되어 있습니다. 앞으로는 경기마다 다른 모양의 맵(장애물이 다르게 흩뿌려지거나, 중앙이 좁아지거나, 외곽으로 우회하는 등 5가지 유형)이 무작위로 만들어지도록 바꿀 예정입니다.

그런데 이 변화는 한 번에 다 만들 수 있는 크기가 아닙니다. **"맵을 무작위로 만드는 기능"과 "타일 하나하나가 어떤 상태를 가질 수 있는지의 기본 틀"은 서로 다른 일**이기 때문입니다. 지금 타일은 "이동할 수 있는가"라는 값 하나(`IsWalkable`)를 여기저기서 직접 켜고 끄는 방식으로 되어 있는데, 앞으로는 "지형이 원래 막혀 있는가" · "광산이 있는가" · "지금 건물이 서 있는가"라는 **세 가지 서로 다른 이유**를 따로 기록하고, "이동 가능한가"는 그 세 가지로부터 **자동으로 계산**되도록 바꿔야 합니다. 이렇게 해야 나중에 "이동은 되는데 건물은 못 짓는 타일" 같은 새로운 조합이 생겨도 코드가 자연스럽게 처리할 수 있습니다.

이번 1단계에서 하는 일은 **딱 이 기본 틀을 바꾸는 것뿐입니다.** 무작위로 맵을 만드는 기능은 이번에 켜지 않습니다 — 만들지도 않습니다. 목표는 오직 하나, **"지금 있는 고정 맵이 이 틀 전환 후에도 예전과 완전히 똑같이 동작하는 것"**입니다. 겉보기에는 아무것도 달라지지 않아야 성공입니다. 이번 문서는 그 전환을 하기 위해 코드의 현재 상태를 실제로 읽어서 확인한 기록이며, `Plan.md`는 그 확인을 바탕으로 한 실행 계획입니다.

---

## 1. 전체 로드맵 — 3단계와 그 경계

무작위 맵 구현 전체는 3단계로 나뉘며, **각 단계의 승인과 실행은 별도**입니다. 이번에 쓰는 것은 1단계의 `Research.md`/`Plan.md`뿐이고, 2·3단계는 아래 경계만 기록합니다(1단계 결과에 따라 내용이 달라지므로 지금 계획을 쓰지 않습니다).

| 단계 | 범위 | 완료 판정 |
|---|---|---|
| **1 (이번 작업)** | `TileKind` 도입 · `HexTile` 상태 계약 전환(`IsWalkable`을 계산 프로퍼티로) · `MapDefinition` 자료구조 · canonical 직렬화 · SHA-256 | **기존 고정 맵이 그대로 동작**(회귀 0) |
| **2** | `SymmetricMapBuilder` · 생성기 5종(완전개방형/장애물개방형/협곡형/외곽형/3갈래형) · `MapDefinitionValidator` · 폴백 템플릿 5개 | 에디터에서 맵이 생성되고 검증 통과 |
| **3** | NGO 전송(조각·재조립·해시 비교) · 렌더러(막힌 타일 빈 공간·건설 불가 해치) · `AIOpponentController` 전환(`FindPlacementTile()` 807~809행을 일반 건설 조건으로 교체) · 실패 UI | 멀티에서 양쪽 맵이 일치 |

### 1단계에서 하지 않는 것 (명확화)

- 무작위 맵 생성기(`SymmetricMapBuilder`, 유형별 generator 5종, `MapDefinitionValidator`)를 만들지 않는다.
- 폴백 템플릿 5개를 만들지 않는다.
- `NetworkMapTransfer`(조각 전송)·해시 비교·씬 전환 게이팅을 만들지 않는다.
- `HexGridRenderer`의 막힌 타일 빈 공간 표현, 건설 불가 해치 오버레이를 만들지 않는다.
- `AIOpponentController.FindPlacementTile()`의 배치 후보 판정을 바꾸지 않는다(807~809행은 그대로 둔다 — 이유는 §5 참조).
- 맵 준비 실패 UI를 만들지 않는다.
- **`MapDefinition`·canonical 직렬화·SHA-256은 만들지만, 어디에서도 호출하지 않는다.** 즉 `GameBootstrapper`가 이 새 자료구조로 맵을 로드하도록 바꾸지 않는다. 지금처럼 코드에 하드코딩된 좌표로 고정 맵을 계속 만든다.

---

## 2. 현재 코드 상태 실측

### 2-1. `HexTile.cs` 현재 필드 (전문, `Assets/_Project/Scripts/Domain/Hex/HexTile.cs`)

```csharp
namespace Hexiege.Domain
{
    public class HexTile
    {
        public HexCoord Coord { get; }                          // 24행, 불변
        public TeamId Owner { get; set; }                        // 27행
        public bool IsWalkable { get; set; }                     // 34행 — mutable
        public bool HasGoldMine { get; set; }                    // 41행 — mutable

        public HexTile(HexCoord coord, TeamId owner = TeamId.Neutral, bool isWalkable = true)
        {
            Coord = coord;
            Owner = owner;
            IsWalkable = isWalkable;                              // 47행
        }
    }
}
```

프로퍼티는 **4개**(불변 좌표 1개 + 상태 3개)이며, 이 중 이번 작업이 다루는 것은 `IsWalkable`·`HasGoldMine` 2개다. `TileKind`·`MineKind`·`HasBuilding`·`MapDefinition`·`SymmetricMapBuilder`·`MapDefinitionValidator` — 이번 계약이 요구하는 새 이름은 **코드 전체에 0건**이다(확인 방법: `grep -rn "MineKind|TileKind|DecorationDefinition|MapType\b" Assets/_Project/Scripts` → 0건 매치, `grep -rn "MapDefinition|SymmetricMapBuilder|MapDefinitionValidator" Assets/_Project/Scripts` → 0건 매치). 클린 슬레이트다.

`HexTile` 생성자 호출처는 `Assets/_Project/Scripts/Domain/Hex/HexGrid.cs:93` `new HexTile(coord)` **1건뿐**이다(전체 코드베이스 `grep -rn "new HexTile\("` 확인). `isWalkable` 매개변수를 `true` 외의 값으로 넘기는 호출은 없다.

### 2-2. `IsWalkable` 실측 — 14개 파일 / 40건

지시서가 준 값(14개 파일 · 40건 · 대입 9건)을 **직접 재실측**했다. 셈법: `Assets/_Project/Scripts` 하위 `.cs` 전체에서 `Grep "IsWalkable"`(대소문자 구분, 리터럴 일치)로 나온 모든 줄을 한 줄씩 열어 주석/읽기/쓰기로 분류했다. 전체 repo `*.cs`(Scripts 밖 포함) 재확인 결과도 동일한 15개 파일(`IsWalkable` 14 + `HasGoldMine` 5, 중복 4 제외)로 일치해 **범위를 좁혀 놓고 "전수"라 부르는 실수(`.claude/mistakes.md` 2026-08-20 항목)는 없다.**

| 파일 | 건수 | 주석 | 읽기 | 쓰기(대입) |
|---|---|---|---|---|
| `Application/UseCases/BuildingPlacementUseCase.cs` | 13 | 7 | 3 | **3** |
| `Domain/Hex/HexTile.cs` | 3 | 1 | 0(선언 1 별도) | **1** |
| `Bootstrap/GameBootstrapper.Map.cs` | 3 | 2 | 0 | **1** |
| `Application/UseCases/UnitMovementUseCase.cs` | 3 | 0 | 2 | 0 (+ 래퍼 메서드 선언 1) |
| `Application/UseCases/UnitSpawnUseCase.cs` | 3 | 2 | 1 | 0 |
| `Application/Services/CongestionAwarePathfinder.cs` | 3 | 0 | 3 | 0 |
| `Presentation/Unit/UnitView.cs` | 2 | 0 | 2 | 0 |
| `Domain/Hex/HexGrid.cs` | 2 | 1 | 1 | 0 |
| `Application/Services/AIOpponentController.cs` | 2 | 1 | 1 | 0 |
| `Editor/SpiritAttackVfxTestSpawner.cs` | 2 | 0 | 2 | 0 |
| `Presentation/Debug/DebugUI.cs` | 1 | 0 | 1 | 0 |
| `Infrastructure/Network/NetworkBuildingController.cs` | 1 | 1 | 0 | 0 |
| `Domain/Hex/HexFlowField.cs` | 1 | 0 | 1 | 0 |
| `Domain/Hex/HexPathfinder.cs` | 1 | 0 | 1 | 0 |
| **합계** | **40** | **15** | **18**(+래퍼선언 1) | **5** |

**파일 수(14)와 총 건수(40)는 지시서와 정확히 일치한다.**

### 🔴 2-3. 대입(쓰기) 건수 재검증 — 지시서 "9건"과 실측이 다르다

지시서가 준 값은 **9건**이었다. 재검증 방법: `Grep pattern:'\.IsWalkable\s*=[^=]|IsWalkable\s*=\s*is'`(대입 연산자 `=`만 매치, 동등비교 `==`는 제외)로 40건 중 대입만 다시 걸렀다. 결과는 **5건**이다.

```
Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs:147   tile.IsWalkable = false;
Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs:186   tile.IsWalkable = false;
Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs:329   tile.IsWalkable = true;
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs:317                  tile.IsWalkable = false;
Assets/_Project/Scripts/Domain/Hex/HexTile.cs:47                              IsWalkable = isWalkable;
```

40건 전체를 한 줄씩 다시 읽어 분류한 결과(위 §2-2 표의 "쓰기" 열)도 같은 5건으로 수렴했고, `new HexTile(` 호출처가 1건뿐이며 그 호출도 기본값(`true`)만 쓰므로 생성자 경유의 숨은 대입도 없다. **두 가지 독립적인 셈법(정규식 필터 / 줄 단위 재분류)이 같은 값에 수렴하므로 5건을 확정값으로 본다.**

**차이가 어디서 났는지는 알아낼 수 없었다** — 지시서에 "어떻게 셌는지"가 함께 오지 않아 재현할 방법이 없다(`.claude/mistakes.md` 2026-08-19 항목이 정확히 지적하는 상황: 숫자만 넘어가면 받는 쪽은 틀렸다는 사실조차 확인하기 전엔 모른다). 짐작 가는 후보 하나만 적어 둔다 — `HexTile.cs`의 프로퍼티 **선언**(`public bool IsWalkable { get; set; }`, 34행)이나 `HasGoldMine` 계열 대입(아래 §2-4, 1건)을 합쳐 세었다면 6~7건까지는 설명되지만 9건까지는 채워지지 않는다. **추정이므로 이 문단 자체를 근거로 쓰지 않는다** — 확정값은 위 5건이다.

이 문서와 `Plan.md`의 이후 모든 서술은 **재검증된 5건**을 기준으로 한다.

### 2-4. 대입 5곳 각각이 무엇을 뜻하는가

| # | 위치 | 소속 메서드 | 이 대입이 실제로 뜻하는 것 |
|---|---|---|---|
| 1 | `BuildingPlacementUseCase.cs:147` | `PlaceBuildingWithId()` | **네트워크 클라이언트 측 건물 재생성.** 서버가 이미 검증·배치한 건물을 클라이언트 도메인 상태에 반영할 때, 그 타일 위에 건물이 존재하게 됐다는 사실을 기록한다. |
| 2 | `BuildingPlacementUseCase.cs:186` | `PlaceBuildingInternal()` | **건물 배치 공통 로직.** `PlaceBuilding`(일반 건물)·`PlaceMiningPost`·`PlaceMiningPostDirect`(채굴소) 세 진입점이 모두 이 메서드를 거쳐 호출되므로, **어떤 종류의 건물이든 배치되는 순간** "이 타일 위에 건물이 있다"를 기록하는 자리다. |
| 3 | `BuildingPlacementUseCase.cs:329` | `RemoveBuilding()` | **건물 철거/파괴 복구.** `if (!tile.HasGoldMine) tile.IsWalkable = true;` — 건물이 사라지면 타일을 다시 이동 가능하게 되돌리되, **금광 타일이면 되돌리지 않는다**(광산 오브젝트 자체가 계속 이동을 막아야 하므로). 즉 "건물이 없어졌다"와 "광산이 있어서 여전히 막혀 있어야 한다"는 두 조건이 한 줄에 섞여 있다. |
| 4 | `GameBootstrapper.Map.cs:317` | `PlaceGoldMines()` 내부 `SetGoldMine(col, row)` | **고정 맵의 금광 타일 초기화.** `tile.HasGoldMine = true;`와 한 쌍으로, "여기 광산이 있으니 못 지나간다"를 기록한다. 시작 금광 2개(Blue/Red)와 중립 금광 2개, 총 4번 호출된다(코드 자체는 이 한 줄이지만 실행은 4회). |
| 5 | `HexTile.cs:47` | 생성자 | **타일 생성 시 초기값 대입.** `HexGrid.Generate()`가 231(현재 맵은 그보다 작은 실제 크기, §2-6 참조)개 타일을 만들 때마다 기본값 `true`(이동 가능)로 초기화한다. 유일한 호출처(`HexGrid.cs:93`)가 기본값만 쓰므로, 사실상 "새로 만든 타일은 항상 걸을 수 있다"는 뜻 하나로만 쓰인다. |

**패턴 요약**: 5곳 중 3곳(#1·#2·#3)은 "건물이 생기거나 사라짐"을 뜻하고, 1곳(#4)은 "광산이라서 원천적으로 막혀 있음"을 뜻하며, 1곳(#5)은 "새로 만든 타일의 기본값"이다. 이 세 가지 뜻이 정확히 목표 계약의 세 축(`HasBuilding` · `MineKind` · 기본값)과 하나씩 대응한다 — TDD가 요구하는 전환이 **임의 재해석이 아니라 이미 코드에 있던 세 가지 의도를 이름으로 분리하는 작업**임을 뒷받침한다.

### 2-5. `HasGoldMine` 사용처 — 5개 파일

지시서는 "5개 파일"만 주고 세부 건수는 주지 않았다. 실측 결과 **5개 파일 / 10건**이며, 대입은 **1건뿐**이다.

| 파일 | 건수 | 성격 |
|---|---|---|
| `Application/UseCases/BuildingPlacementUseCase.cs` | 5 | 주석 1 + 읽기 4(`PlaceMiningPost`·`PlaceMiningPostDirect`의 자격 검증, `CanPlaceBuildingType`의 판정, `RemoveBuilding`의 복구 가드) |
| `Bootstrap/GameBootstrapper.Map.cs` | 2 | 주석 1 + **쓰기 1**(316행 `tile.HasGoldMine = true;`, 위 §2-4 #4와 한 쌍) |
| `Domain/Hex/HexTile.cs` | 1 | 프로퍼티 선언(41행) |
| `Application/Services/AIOpponentController.cs` | 1 | 읽기(224행, 금광 소유 여부 판정 — MiningPost 대상 탐색으로 추정, 본 조사 범위 밖) |
| `Presentation/Grid/HexGridRenderer.cs` | 1 | 읽기(162행, `RenderGoldMines()` — 금광 프리팹을 렌더링할 타일 필터링) |

대입이 1건뿐이므로 `HasGoldMine → MineKind` 전환은 `IsWalkable`보다 훨씬 단순하다. 유일한 쓰기(#4)가 이미 위 표에 포함되어 있다.

### 2-6. 현재 그리드 상태 — 목표 무작위 맵(11×21)과는 다른 맵이다

`Infrastructure/Config/GameConfig.cs`의 코드 기본값은 `FlatTop: GridWidth=10, GridHeight=29`, `PointyTop: GridWidth=7, GridHeight=17`이다(67~80행). `GameSystemRules_RandomMap.md` 규칙 1이 요구하는 미래의 무작위 맵은 **FlatTop 11×21**이므로 **현재 고정 맵과 크기가 다르다.**

⚠️ **이 값은 ScriptableObject `.asset`의 Inspector 값이 코드 기본값을 덮어쓸 수 있다**(`.claude/MEMORY.md` 공통 교훈 "Inspector 값이 코드 기본값보다 우선"). 실제 `.asset` 파일의 Inspector 값은 확인하지 않았다 — 이번 작업은 그리드 크기를 다루지 않으므로 확인이 불필요했다. Plan.md도 그리드 크기·`OffsetToCube` 변환식을 손대지 않는다.

`GameBootstrapper.Map.cs`의 `PlaceGoldMines()`를 보면 현재 고정 맵에는 **완전 차단 지형(`Blocked`에 대응하는 것)이 코드 어디에도 없다** — 성 2개, 시작 금광 2개, 중립 금광 2개 외에는 전부 기본값(`IsWalkable=true`)인 일반 타일이다. 즉 **1단계 전환 후에도 기존 맵의 모든 타일은 `TileKind.Normal`이어야 회귀가 없다** — `TileKind.NoBuild`·`TileKind.Blocked`를 실제로 쓰는 자리는 현재 코드에 하나도 없다.

---

## 3. 계약 문서가 요구하는 목표 상태

🔴 **아래는 요약이며 단일 소스가 아니다.** 실제 판정식·필드 정의의 단일 소스는 `TechnicalDesignDocument.md` 「`HexTile` 런타임 상태 계약」절(512~591행)이다. 값이 어긋나면 그쪽이 옳다.

### 3-1. 세 축 (512~532행)

| 상태 | 값 | 변경 주체 |
|---|---|---|
| `TileKind` | `Normal`(일반) / `NoBuild`(건설 불가) / `Blocked`(막힘) | 맵 정의. 경기 중 불변 |
| `MineKind` | `None` / `Neutral` / `BlueStart` / `RedStart` | 광산 배치 목록에서 로드 시 투영. 경기 중 불변 |
| `HasBuilding` | `bool` | 건물 배치·철거·파괴에 따른 동적 상태 |

### 3-2. 판정식 (526~548행)

```text
IsWalkable = TileKind != Blocked
          && MineKind == None
          && !HasBuilding

일반 건설 = TileKind == Normal
         && MineKind == None
         && !HasBuilding
         && 기존 소유권 조건

MiningPost = TileKind != Blocked
          && MineKind != None
          && !HasBuilding
          && 기존 인접 팀 타일 조건

점령 가능 = TileKind != Blocked
```

`IsWalkable`은 **setter 없는 계산 프로퍼티**로 전환한다(583~589행 「기존 코드 전환 요구」). 이번 1단계는 「일반 건설」·`MiningPost` 판정식을 실제로 어디에도 연결하지 않는다(§1 「하지 않는 것」) — 다만 `IsWalkable` 계산식만은 판정식과 같은 세 필드를 쓰므로 **동시에** 성립한다.

### 3-3. `MapDefinition` 정규 데이터 계약 (214~336행) — 1단계에서 만드는 것

- 상위 필드: `MapVersion`(int, 초기값 1) · 64-bit root seed · 맵 유형 · 너비 11 · 높이 21 · FlatTop · 중립 광산 수 · 테스트 모드 표식(고정폭 0/1) · 실제 초기 골드 · 최종 해시.
- 타일 배열 231개, row-major, `index = row * 11 + col`, 타일당 `TileKind` 단일 필드(`InitialOwner`·장식 상태 미포함).
- 오브젝트 배치: 성(위치+팀), 시작 광산(위치+팀), 중립 광산(위치), 장식(`DecorationDefinition`: 위치+`typeId`+`materialVariantId`+`scaleStepId`+`rotationStepId`).
- canonical binary: 필드 순서 고정, 고정폭 정수, **little-endian**, 타일 231개 row-major, 성·광산·장식 정규 정렬, `string`/`float`/해시 필드 자체는 제외, 나머지 전체에 SHA-256.
- 이번 1단계는 이 스키마와 인코더/SHA-256 계산기를 **만들지만 어디에서도 호출하지 않는다.**

---

## 4. 영향 범위

### 4-1. 레이어 (`.claude/MEMORY.md` 「아키텍처 핵심 제약」 대조)

| 파일/범주 | 레이어 |
|---|---|
| `HexTile.cs`, 신설 `TileKind`/`MineKind`/`MapDefinition`/직렬화/해시 | **Domain** — 순수 C#. `TileKind`는 Domain 계층 타입이므로 `Domain → Core 참조 금지` 제약을 받는다(TDD 239행이 명시). SHA-256은 `System.Security.Cryptography`(BCL)이며 `Hexiege.Core`나 Unity API가 아니므로 이 제약과 무관하다. |
| `BuildingPlacementUseCase.cs`, `UnitMovementUseCase.cs`, `UnitSpawnUseCase.cs` | **Application** |
| `CongestionAwarePathfinder.cs`, `AIOpponentController.cs` | **Application/Services** |
| `NetworkBuildingController.cs` | **Infrastructure** |
| `GameBootstrapper.Map.cs` | **Bootstrap**(유일한 조합 루트) |
| `UnitView.cs`, `DebugUI.cs`, `HexGridRenderer.cs`(§2-5 읽기 1건, 이번엔 손대지 않음 — §1 참조) | **Presentation** |
| `SpiritAttackVfxTestSpawner.cs` | **Editor**(1회성 테스트 스포너, `WORKFLOW.md` [5-2] 분류 대상은 아님 — 게임 코드가 아니라 VFX 테스트 도구) |

이번 작업은 **모든 변경이 같은 레이어 내부 또는 상위→하위 읽기**이며, `Domain→Core`·`Application→Netcode 직접 참조`·`Application→Infrastructure 역참조` 어느 것도 새로 만들지 않는다. `NetworkBuildingController.cs`의 유일한 매치(204행)는 주석이라 코드 변경이 없다.

### 4-2. `GameBootstrapper` 단일 조합 루트 원칙과의 관계

`GameBootstrapper.Map.cs`의 `SetGoldMine()` 로컬 함수가 `HasGoldMine`/`IsWalkable` 대입 지점이다. 이 함수는 이미 `GameBootstrapper` 내부에 있으므로 원칙 위반이 아니다. 다만 §2-4 #4에서 보듯 현재 `SetGoldMine(col, row)`는 시작 광산과 중립 광산을 구분하지 않고 호출된다 — `MineKind`는 `Neutral`/`BlueStart`/`RedStart` 세 값을 구분해야 하므로 이 함수의 시그니처를 바꿔야 한다(구체안은 `Plan.md`).

---

## 5. 위험 요소

| # | 위험 | 왜 생기는가 | 비고 |
|---|---|---|---|
| 1 | **`IsWalkable`을 계산 프로퍼티로 바꾸는 순간, 필드 이름 충돌로 기존 mutable 필드와 새 계산 프로퍼티가 동시에 존재할 수 없다.** | C#은 같은 이름의 필드와 프로퍼티를 공존시킬 수 없다. 따라서 `HexTile.cs`와 5곳의 대입 지점은 **한 번에(원자적으로) 함께 바뀌어야** 컴파일이 유지된다 — 단계적으로 나눠 커밋하면 중간 상태가 컴파일되지 않는다. | `Plan.md` 「작업 순서」에서 다룬다 |
| 2 | **`BuildingPlacementUseCase.cs:329`의 `if (!tile.HasGoldMine)` 가드를 없애도 되는가** | 새 모델에서는 `IsWalkable` 계산식이 이미 `MineKind == None`을 요구하므로, `HasBuilding=false`로만 되돌려도 광산 타일은 자동으로 계속 막힌 것으로 계산된다. 즉 가드가 **불필요해진다.** 이 재해석이 틀리면 광산 위 건물 파괴 후 그 타일이 잘못 걸을 수 있게 되는 회귀가 생긴다 — Plan.md와 실기 검증에서 반드시 확인해야 하는 지점이다. | 최고 위험 지점 |
| 3 | **`GameBootstrapper.Map.cs`의 `SetGoldMine` 시그니처 변경** | 현재 배열 순회 구조(`foreach (var m in startingMines) SetGoldMine(...)`)를 바꿔야 `MineKind`를 팀별로 정확히 넣을 수 있다(§4-2). 구조를 바꾸는 만큼 실수 여지가 있다. | |
| 4 | **`HexTile` 생성자 시그니처 변경** | `isWalkable` 매개변수가 새 모델에서 의미를 잃는다(계산값이므로 강제할 수 없음). 매개변수를 없애면 시그니처가 바뀌지만 호출처가 1곳(`HexGrid.cs:93`, 기본값만 사용)뿐이라 위험은 낮다. | 낮음 |
| 5 | **읽기 전용 30여 곳은 이론상 무변경**이지만, 컴파일이 실제로 통과하는지는 전수 확인이 필요하다 | `IsWalkable`의 타입(`bool`)과 접근 문법(`tile.IsWalkable`)이 그대로이므로 계산 프로퍼티 전환은 읽기 쪽에서는 소스 호환이지만, "이론상 그렇다"와 "실제로 컴파일된다"는 다르다(`.claude/mistakes.md`가 반복 지적하는 실수 패턴 — 검증 없이 이론을 결론으로 적지 않는다). | Plan.md 회귀 확인에 명시 |
| 6 | **`HasGoldMine` 대입이 1건뿐이라 상대적으로 안전**하지만, 읽기 5곳(§2-5)이 전부 `!= None` 비교로 정확히 바뀌어야 한다 | 예: `if (!tile.HasGoldMine)`은 `if (tile.MineKind == MineKind.None)`으로, `if (tile.HasGoldMine)`은 `if (tile.MineKind != MineKind.None)`으로 — 부정 방향을 뒤집어 실수하기 쉬운 자리다. | |

---

## 6. 부가 이슈 (고치지 않고 기록만 함)

**(가) 이번 조사가 검증한 것과 다른 회차가 검증한 것의 교차 확인**

같은 폴더 상위의 `Assets/_Project/Docs/_Tasks/2026-09-01/07_07_map-docs-out-of-scope-findings/Research.md`(오늘 이전 회차)가 `AIOpponentController.cs`의 770~773행 주석과 807~809행 판정문을 이미 실측했고, 그 결과(**807~809행 세 줄에 걸친 한 문장**, `HexTile`은 **불변 좌표 1 + 상태 3 = 프로퍼티 4개**)가 이번 조사와 **정확히 일치한다.** 이 문서는 그 확인을 재사용하지 않고 독립적으로 다시 읽어 대조했으며, 두 회차의 실측이 같은 값에 수렴했다는 사실만 기록해 둔다(다른 사례로 일반화하지 않는다 — `.claude/mistakes.md` 2026-08-24 「한 사례를 확인하고 전체로 일반화」 교훈).

같은 문서는 이미 사용자 승인을 받아 `TechnicalDesignDocument.md` 「기존 코드 전환 요구」에 `AIOpponentController.FindPlacementTile()` 전환 항목을 추가했다 — 이번에 읽은 TDD 583~591행에 그 항목이 실제로 들어 있음을 확인했다(§1 「하지 않는 것」에 반영, 3단계 몫).

**(나) `GameBootstrapper.Map.cs`의 그리드 크기 실측 미완**

§2-6에서 언급했듯 실제 `.asset` Inspector 값(코드 기본값 10×29/7×17과 다를 수 있음)은 확인하지 않았다. 1단계 작업 자체에는 영향이 없지만(그리드 크기를 다루지 않음), 2단계(생성기 구현)에서는 실제 씬에 배정된 `GameConfig.asset`의 Inspector 값을 먼저 확인해야 한다 — 코드 기본값을 그대로 믿지 않는다.

**(다) 계약 문서 간 어긋남은 발견하지 못했다**

`GameSystemRules_RandomMap.md`·`GameSystemRules_Map.md`·`TechnicalDesignDocument.md`의 이번 조사 관련 절(1~7장, 512~628행)을 통독한 범위에서는 서로 어긋나는 서술을 찾지 못했다. 두 문서 모두 "규칙 문서가 옳다"는 명시적 우선순위를 여러 곳에 남겨 두고 있고(예: TDD 563행), 2026-08-26 교정 이후로는 대역 수치·용어가 통일되어 있다. 다만 이 확인은 **1단계 작업이 직접 참조하는 절에 한정**되며, 2·3단계 관련 절(생성기 알고리즘 세부·전송 프로토콜) 전체를 재검증한 것은 아니다.

**(라) 1KB/64KB 전송 한도는 이미 문서 자신이 "근거 미확인"으로 표시해 둔 것이다**

TDD 306~313행이 스스로 "근거 미확인 — 구현 시 NGO 실측으로 확정"이라 적어 두었다. 새로 발견한 문제가 아니라 이미 알려진 3단계 몫이므로 여기서는 확인만 하고 넘어간다.

---

## 7. 요약 — 이번 조사에서 확정된 사실

1. `IsWalkable` 14개 파일 / 40건 — **지시서와 일치.**
2. `IsWalkable` 대입(쓰기) — 🔴 **지시서 9건, 실측 5건. 불일치.** 두 가지 독립 셈법이 5건에 수렴했으므로 5건을 확정값으로 채택한다.
3. `HasGoldMine` 5개 파일 — **지시서와 일치**(지시서는 세부 건수를 주지 않음). 실측 10건 중 대입은 1건.
4. `MapDefinition`·`TileKind`·`MineKind`·`SymmetricMapBuilder`·`MapDefinitionValidator` — 코드 전체 0건, 클린 슬레이트.
5. 대입 5곳 전부가 목표 계약의 세 축(`HasBuilding`/`MineKind`/기본값)에 정확히 하나씩 대응한다 — 전환은 재해석이 아니라 이름 분리다.
6. 현재 고정 맵에는 `Blocked`/`NoBuild`에 해당하는 지형이 전혀 없다 — 전환 후 모든 기존 타일은 `TileKind.Normal`이어야 회귀가 없다.

---

## 8. 구현 후 확인 (2026-09-03 추가)

> **이 문서의 §1~§7은 2026-09-01 시점의 코드 실측 기록이므로 한 글자도 고치지 않았다.** 아래는 1단계를 실제로 구현·검증한 뒤(구현 커밋 `cee857d`), 그 조사에서 **빠졌던 것**만 덧붙인 것이다. 실행 결과 전체는 `Plan.md` §7 에 있다.

### 8-1. 🔴 조사가 놓친 것 — `startingMines` 배열의 다른 사용처

§4-2 와 §5 위험 #3 은 `GameBootstrapper.Map.cs` 의 배열 순회 구조를 바꿔야 한다고만 적었고, **그 배열이 광산 타일 설정 말고 어디에 더 쓰이는지는 조사하지 않았다.** 그 결과 `Plan.md` §2-4 의 「변경 후」 코드가 배열을 없애는 형태로 쓰였고, **실제 구현에서는 없앨 수 없었다.**

- 실측(2026-09-03): 같은 파일에서 `startingMines` 를 참조하는 자리는 **선언 1 + 광산 설정 2 + 시작 채굴소 자동 건설 2 = 5곳**이다. 뒤의 2곳은 `PlaceMiningPostDirect(TeamId.Blue/Red, …)` 에 넘길 좌표를 이 배열에서 다시 읽는다.
- 배열을 지웠다면 그 좌표가 두 벌로 복제되어, 「좌표 계산이 두 벌로 갈라지지 않게 한다」는 §2-4 의 취지 자체가 무너진다. **그래서 배열을 유지한 실제 구현이 옳다.**
- **좌표 값과 팀 구분(`BlueStart`/`RedStart`)은 계획과 동일하다.** 달라진 것은 「배열을 없앤다」는 형태뿐이다.

> **교훈:** 어떤 변수의 **선언 근처만 읽고** 「이 변수는 여기서만 쓰인다」고 계획에 적지 않는다. 변경 대상 변수는 **파일 전체(가능하면 리포지토리 전체)에서 이름으로 전수 검색**한 결과를 Research 에 남긴다. 이번 배열은 **같은 함수 안 30여 줄 아래**에 다른 사용처가 있었다.

### 8-2. §7 요약 항목의 사후 상태

| §7 항목 | 2026-09-03 상태 |
|---|---|
| 4. `MapDefinition`·`TileKind`·`MineKind` 등 코드 0건(클린 슬레이트) | 타입 6종이 생겼다. 단 `MapType`·`DecorationDefinition`·`MapDefinition`·`MapDefinitionCodec` **4종은 여전히 호출부 0곳**이다(계획된 상태) |
| 6. 현재 고정 맵에 `Blocked`/`NoBuild` 지형 없음 | **여전히 그렇다** — `TileKind` 를 설정하는 코드가 `.cs` 전체에 0곳이라 모든 타일이 `Normal` 이다. 그래서 이동 판정과 건설 판정이 아직 같은 답을 낸다 |
