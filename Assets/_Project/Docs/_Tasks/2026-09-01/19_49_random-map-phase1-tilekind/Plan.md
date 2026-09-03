# Plan — 무작위 맵 1단계: `TileKind` 도입과 `HexTile` 상태 계약 전환

작성일: 2026-09-01
작업 폴더: `Assets/_Project/Docs/_Tasks/2026-09-01/19_49_random-map-phase1-tilekind/`
선행 문서: 같은 폴더 `Research.md` (반드시 함께 읽을 것 — 특히 §2-3의 "대입 9건→5건 불일치")

> ~~🔴 **이 문서는 아직 실행되지 않았다.** 사용자의 명시적 승인 후에만 game-programmer에게 위임한다(CLAUDE.md 규칙 2, `WORKFLOW.md` [4]).~~
>
> **[✅ 2026-09-03 — 이 계획은 실행됐다.]** 구현 커밋 `cee857d` → 임시 진단 로그 `5649a7e` → 진단 로그 제거 `51516a1`(브랜치 `claude/random-map-implementation`). 에디터 실기 검증 통과.
> **계획 본문(§0~§6)은 한 글자도 고치지 않았다.** 실행 결과·계획과 달라진 점·미완 단서는 문서 끝의 **§7 구현 결과**에 모아 두었으며, §5 회귀 확인 항목별 결과는 그 절 머리의 인용 블록에 있다.

---

## 0. 이 작업이 무엇이고 왜 하는지 (자연어 설명 — CLAUDE.md 규칙 13)

`Research.md`에서 확인한 대로, 지금 타일의 "걸을 수 있는가"라는 값은 여러 코드 자리에서 직접 켜고 끄는 방식으로 되어 있습니다. 이번 계획은 그 값을 **직접 켜고 끄지 못하게 막고**, 대신 "지형이 막혀 있는가" · "광산이 있는가" · "건물이 서 있는가"라는 **세 가지 진짜 이유**를 각각 따로 기록한 뒤, "걸을 수 있는가"는 그 세 가지로부터 **자동으로 계산**되도록 바꾸는 작업입니다.

지금 코드에서 "걸을 수 없게" 만드는 자리는 딱 5곳입니다 — 건물을 지을 때 2곳, 건물을 없앨 때 1곳, 광산을 배치할 때 1곳, 타일을 처음 만들 때 1곳. 이 5곳은 재검증 결과 각각 "건물이 생김" · "건물이 없어짐" · "광산이라서 원래 막힘" · "새 타일의 기본값" 중 하나를 뜻하고 있었습니다(`Research.md` §2-4). 이번 계획은 이 5곳을 그 뜻 그대로 새 이름(`HasBuilding`·`MineKind`)으로 옮겨 적는 것이고, 나머지 30여 곳(값을 읽기만 하는 자리)은 손대지 않아도 그대로 동작해야 합니다.

동시에, 앞으로 무작위 맵을 만들 때 쓸 자료구조(맵 전체를 하나의 데이터로 표현하는 `MapDefinition`, 그것을 저장하는 방식, 두 대의 기기가 같은 맵을 받았는지 확인하는 해시값)도 이번에 만들어 둡니다. 다만 **이번에는 그 자료구조를 실제로 쓰지 않습니다** — 만들어만 두고 아무도 호출하지 않으므로, 이 부분은 잘못되어도 지금 게임에 아무 영향이 없습니다.

이번 작업의 성공 기준은 하나뿐입니다 — **작업 전후로 지금 있는 고정 맵이 완전히 똑같이 동작하는 것.** 새로운 기능은 하나도 켜지지 않습니다.

---

## 1. 🔴 기존 로직 제거 규칙 (WORKFLOW [4] — 문서 최상단 기술 필수)

이번 작업은 `HexTile.IsWalkable`의 **mutable setter**와 `HexTile.HasGoldMine` **필드 전체**를 제거한다.

### 제거해도 안전한 근거

- `Research.md` §2-2·§2-4가 40건 전체를 읽기/쓰기로 분류했고, 쓰기(대입) 5곳 전부가 "건물 생성/제거" 또는 "광산 배치" 또는 "타일 초기화"라는 세 가지 뜻 중 하나로 환원됨을 확인했다. 세 가지 뜻은 목표 계약의 `HasBuilding`/`MineKind`/기본값과 정확히 1:1로 대응하므로, **제거가 아니라 이름 이전**에 가깝다.
- `HasGoldMine` 대입은 1곳뿐이고 그 1곳이 `IsWalkable` 대입 5곳 중 1곳(`GameBootstrapper.Map.cs:317`)과 같은 함수·같은 실행 시점에 나란히 있어, 두 필드를 동시에 `MineKind` 하나로 합쳐도 실행 순서가 바뀌지 않는다.
- 읽기 전용 자리(30여 곳)는 `tile.IsWalkable`이라는 **문법이 그대로**이므로 코드를 고칠 필요가 없다 — 컴파일러가 이를 강제로 검증한다(아래 「예외 처리」 참조).

### ⚠️ 예외 — "비활성화(주석 처리)" 원칙을 문자 그대로 적용할 수 없는 이유

WORKFLOW [4]의 기본 원칙은 "제거 대신 주석 처리"다. 그러나 이번 대상은 **필드**이고, C#은 같은 이름의 필드(`IsWalkable { get; set; }`)와 계산 프로퍼티(`IsWalkable => …`)를 동시에 존재시킬 수 없다. 즉 "새 것을 켜고 옛 것을 주석으로 남겨 둔 채 스위치로 전환"하는 것이 **물리적으로 불가능**하다(하나의 파일 안에서 이름이 충돌한다).

대신 다음 두 가지로 같은 목적(문제 발생 시 즉시 되돌릴 수 있는 참조)을 satisfy한다.

1. **원본 스냅샷을 이 문서에 보존한다** — `Research.md` §2-1(전문)과 §2-4(대입 5곳 각각의 원문·의미)가 이미 그 스냅샷이다. 회귀가 발견되면 이 두 절을 참조해 정확히 무엇이 어떻게 바뀌었는지 대조할 수 있다.
2. **git 히스토리가 실질적인 롤백 경로다.** 이 프로젝트는 git 저장소이므로(`CLAUDE.md` 규칙 5에 따라 이 세션은 git 명령을 실행하지 않지만) 사용자가 필요 시 직접 되돌릴 수 있다.
3. **최종 삭제 시점 원칙은 그대로 지킨다** — 스냅샷(`Research.md`의 원문 인용)은 [6] 사용자 테스트 통과 전까지 이 문서에 남기고, 통과 후 [7] 이전 단계에서 "더 이상 필요 없다"고 판단되면 정리한다. `Research.md`·`Plan.md` 자체는 WORKFLOW 규칙상 삭제하지 않는다(작업 폴더 보존 원칙).

---

## 2. 파일별 변경 내용

### 2-1. 신설 파일 (아무도 호출하지 않음 — 회귀 위험 0)

| 파일 | 내용 | 근거 |
|---|---|---|
| `Assets/_Project/Scripts/Domain/Hex/TileKind.cs` | `public enum TileKind { Normal = 0, NoBuild = 1, Blocked = 2 }` | `GameSystemRules_RandomMap.md` 5장 표(395~403행), TDD 236행 |
| `Assets/_Project/Scripts/Domain/Hex/MineKind.cs` | `public enum MineKind { None = 0, Neutral = 1, BlueStart = 2, RedStart = 3 }` | TDD 519행, `GameSystemRules_RandomMap.md` 규칙 13 |
| `Assets/_Project/Scripts/Domain/Map/MapType.cs` | `public enum MapType { FullyOpen, ObstacleOpen, Canyon, Outer, ThreeLane }` (5종, `GameSystemRules_RandomMap.md` 3장 순서) | `GameSystemRules_RandomMap.md` 3장 서두(190~192행: 완전개방형·장애물개방형·협곡형·외곽형·3갈래형) |
| `Assets/_Project/Scripts/Domain/Map/DecorationDefinition.cs` | 위치 + `typeId`/`materialVariantId`/`scaleStepId`/`rotationStepId` (전부 정수 ID) | TDD 247~249행, `GameSystemRules_RandomMap.md` 규칙 15 |
| `Assets/_Project/Scripts/Domain/Map/MapDefinition.cs` | `MapVersion`(int, 기본 1) · root seed(ulong) · `MapType` · 너비 11 · 높이 21 · 중립 광산 수 · 테스트 모드 표식(0/1) · 실제 초기 골드 · 최종 해시 · `TileKind[231]`(row-major) · 성 2개(위치+팀) · 시작 광산 2개(위치+팀) · 중립 광산 목록(위치) · `DecorationDefinition` 목록 | TDD 214~251행 |
| `Assets/_Project/Scripts/Domain/Map/MapDefinitionCodec.cs` | canonical binary 인코드/디코드(고정폭 정수, little-endian, row-major, 정규 정렬) + SHA-256 계산(해시 필드 자신은 입력에서 제외) | TDD 271~294행 |

이 6개 파일은 **Domain 레이어의 순수 C# 클래스/구조체이며 어디에서도 `new MapDefinition(...)`이나 `MapDefinitionCodec.Encode(...)`를 호출하지 않는다.** `GameBootstrapper`·`HexGrid`·기존 40건의 어느 자리도 이 파일들을 참조하지 않는다. 컴파일은 통과하지만 **실행 경로에 전혀 연결되지 않으므로, 이 6개 파일에 버그가 있어도 1단계 완료 판정("기존 고정 맵이 그대로 동작")에는 영향이 없다.**

> `Domain → Core 참조 금지` 제약 준수: 6개 파일 모두 `System`/`System.Collections.Generic`/`System.Security.Cryptography`(SHA-256, BCL)만 참조하고 `Hexiege.Core`·`UnityEngine`을 참조하지 않는다.

### 2-2. `HexTile.cs` 전환 (핵심 변경)

**변경 전** (`Research.md` §2-1 전문 인용):

```csharp
public bool IsWalkable { get; set; }
public bool HasGoldMine { get; set; }

public HexTile(HexCoord coord, TeamId owner = TeamId.Neutral, bool isWalkable = true)
{
    Coord = coord;
    Owner = owner;
    IsWalkable = isWalkable;
}
```

**변경 후**:

```csharp
/// <summary> 이 타일의 정적 지형 종류. 맵 정의로 결정되며 경기 중 불변. </summary>
public TileKind TileKind { get; set; } = TileKind.Normal;

/// <summary> 이 타일의 광산 종류. 광산 배치 목록에서 로드 시 투영되며 경기 중 불변. </summary>
public MineKind MineKind { get; set; } = MineKind.None;

/// <summary> 이 타일 위에 건물이 서 있는지. 건물 배치/철거/파괴에 따라 동적으로 바뀐다. </summary>
public bool HasBuilding { get; set; }

/// <summary>
/// 유닛이 이 타일 위를 지나갈 수 있는지 여부. 계산 프로퍼티 — 직접 대입 불가.
/// TDD 「HexTile 런타임 상태 계약」 판정식의 단일 소스를 그대로 구현한다.
/// </summary>
public bool IsWalkable => TileKind != TileKind.Blocked
                        && MineKind == MineKind.None
                        && !HasBuilding;

public HexTile(HexCoord coord, TeamId owner = TeamId.Neutral)
{
    Coord = coord;
    Owner = owner;
}
```

- 생성자에서 `isWalkable` 매개변수를 제거한다. 유일한 호출처(`HexGrid.cs:93`)가 기본값만 쓰므로 영향 없음(`Research.md` §2-1).
- `TileKind`·`MineKind`에 `set`을 남겨 두는 이유: TDD는 "경기 중 불변"이라 하지만 이는 **맵 로드 이후** 불변이라는 뜻이고, 로드 시점(현재는 `GameBootstrapper.Map.cs`)에는 설정할 수 있어야 한다. 타입 시스템으로 강제하지 않고 "로드 시점 이후 재설정하지 않는다"는 규약으로 관리한다 — `Owner`·기존 `HasGoldMine`도 같은 방식(자유 setter + 관례)이었으므로 기존 패턴과 일치한다.
- 근거: TDD 512~532행(세 축 표), 583~589행(기존 코드 전환 요구 1~4번째 항목: "mutable `HexTile.IsWalkable` 필드를 제거하거나 setter 없는 계산 프로퍼티로 전환", "이중 대입은 `MineKind` 설정으로 교체", "`IsWalkable=false` 대신 `HasBuilding=true`", "`IsWalkable=true` 복구 대신 `HasBuilding=false`만 설정").

### 2-3. `IsWalkable` 대입 5곳 전환 (자리별 상세)

| # | 파일:행 | 변경 전 | 변경 후 | 근거 |
|---|---|---|---|---|
| 1 | `BuildingPlacementUseCase.cs:147`(`PlaceBuildingWithId`) | `tile.IsWalkable = false;` | `tile.HasBuilding = true;` | TDD 587행 "건물 배치 시 `IsWalkable=false` 대신 `HasBuilding=true`를 설정한다" |
| 2 | `BuildingPlacementUseCase.cs:186`(`PlaceBuildingInternal`) | `tile.IsWalkable = false;` | `tile.HasBuilding = true;` | 위와 동일. `PlaceBuilding`/`PlaceMiningPost`/`PlaceMiningPostDirect` 세 진입점이 공유하는 공통 경로(`Research.md` §2-4 #2)이므로 한 번의 수정으로 셋 다 적용됨 |
| 3 | `BuildingPlacementUseCase.cs:328~329`(`RemoveBuilding`) | `if (!tile.HasGoldMine)`<br>`    tile.IsWalkable = true;` | `tile.HasBuilding = false;`<br>(가드 없이 무조건) | TDD 588행 "건물 철거/파괴 시 `IsWalkable=true` 복구 대신 `HasBuilding=false`만 설정한다". **가드가 사라지는 이유**: 새 `IsWalkable` 계산식이 `MineKind == None`을 이미 요구하므로, 광산 타일(`MineKind != None`)은 `HasBuilding`을 `false`로 되돌려도 계산 결과가 자동으로 계속 `false`(못 걸음)로 나온다 — `Research.md` 위험 #2, 아래 §5 회귀 확인에서 반드시 실기로 재확인 |
| 4 | `GameBootstrapper.Map.cs:316~317`(`SetGoldMine` 로컬 함수) | `tile.HasGoldMine = true;`<br>`tile.IsWalkable = false;` | `tile.MineKind = mineKind;`(매개변수로 받음, 아래 §2-4 참조) | TDD 586행 "광산 배치의 `HasGoldMine`/`IsWalkable=false` 이중 대입은 `MineKind` 설정으로 교체한다" |
| 5 | `HexTile.cs:47`(생성자) | `IsWalkable = isWalkable;` | (삭제 — §2-2에서 매개변수 자체를 제거) | 계산 프로퍼티는 대입 불가. 기본값 `TileKind.Normal`+`MineKind.None`+`HasBuilding=false`가 계산되어 기존 기본값 `true`와 동일한 결과를 낸다 |

### 2-4. `GameBootstrapper.Map.cs` — `SetGoldMine` 시그니처 변경 (구조 변경, §4-2 위험 대응)

현재 `PlaceGoldMines()`는 `startingMines[]`(Blue/Red 시작 광산, 팀 구분 없이 배열 순서로만 구분됨)와 `neutralMines[]`(중립 광산)를 각각 `foreach`로 순회하며 팀 구분 없는 `SetGoldMine(col, row)`를 호출한다(`Research.md` §2-4 #4). `MineKind`는 `Neutral`/`BlueStart`/`RedStart`를 구분해야 하므로, 배열 순회 대신 **호출부에서 직접 `MineKind`를 지정**하도록 바꾼다.

**변경 전**:
```csharp
int[][] startingMines = new int[][] {
    new int[] { centerCol - 2, blueRow },
    new int[] { centerCol - 2, redRow },
};
int[][] neutralMines = new int[][] {
    new int[] { 2, midRow },
    new int[] { 8, midRow },
};
void SetGoldMine(int col, int row) { ... tile.HasGoldMine = true; tile.IsWalkable = false; ... }
foreach (var m in startingMines) SetGoldMine(m[0], m[1]);
foreach (var m in neutralMines) SetGoldMine(m[0], m[1]);
```

**변경 후**:
```csharp
void SetGoldMine(int col, int row, MineKind mineKind)
{
    HexCoord coord = HexGrid.OffsetToCube(col, row, orientation);
    HexTile tile = _grid.GetTile(coord);
    if (tile != null)
    {
        tile.MineKind = mineKind;
    }
}
SetGoldMine(centerCol - 2, blueRow, MineKind.BlueStart);
SetGoldMine(centerCol - 2, redRow, MineKind.RedStart);
SetGoldMine(2, midRow, MineKind.Neutral);
SetGoldMine(8, midRow, MineKind.Neutral);
```

- **왜 `Neutral` 하나로 뭉치지 않고 `BlueStart`/`RedStart`를 정확히 구분하는가**: 지금 이 값을 읽는 코드가 없으므로 우선 `Neutral`로 통일해도 1단계 판정("고정 맵 회귀 0")은 통과한다. 그러나 CLAUDE.md 규칙 7(완성도 우선)에 따라, 원본 배열이 이미 Blue/Red를 구분해 갖고 있고(주석 "Blue 시작 금광"/"Red 시작 금광") 정확한 값을 넣는 데 추가 비용이 거의 없으므로 **뭉뚱그리는 지름길 대신 정확한 값을 넣는다.** 2단계(생성기)나 그 이후 코드가 `MineKind.BlueStart`/`RedStart`를 구분해 읽기 시작할 때 재작업이 필요 없다.
- 근거: TDD 586행, `GameSystemRules_RandomMap.md` 규칙 2("팀별 시작 광산은 1개이며 … 상대 팀 위치는 180도 회전으로 대응")의 팀 구분 원칙과 일치.

### 2-5. `HasGoldMine` 읽기 5곳 전환

| 파일:행 | 변경 전 | 변경 후 |
|---|---|---|
| `BuildingPlacementUseCase.cs:96`(`PlaceMiningPost`) | `if (!tile.HasGoldMine) return null;` | `if (tile.MineKind == MineKind.None) return null;` |
| `BuildingPlacementUseCase.cs:117`(`PlaceMiningPostDirect`) | `if (!tile.HasGoldMine) return null;` | `if (tile.MineKind == MineKind.None) return null;` |
| `BuildingPlacementUseCase.cs:230`(`CanPlaceBuildingType`) | `return tile.HasGoldMine && …` | `return tile.MineKind != MineKind.None && …` |
| `AIOpponentController.cs:224` | `if (kvp.Value.HasGoldMine)` | `if (kvp.Value.MineKind != MineKind.None)` |
| `HexGridRenderer.cs:162`(`RenderGoldMines`) | `if (!kvp.Value.HasGoldMine) continue;` | `if (kvp.Value.MineKind == MineKind.None) continue;` |

⚠️ **부정 방향 주의**(`Research.md` 위험 #6): `!tile.HasGoldMine` ↔ `tile.MineKind == MineKind.None`, `tile.HasGoldMine` ↔ `tile.MineKind != MineKind.None`. 5곳 모두 위 표대로 정확히 대응시킨다.

근거: TDD 251행 "타일 레코드에 `HasGoldMine` 같은 중복 정체성 필드를 두지 않는다", 583~586행.

### 2-6. 순수 읽기 30곳 — 코드 변경 없음

`Research.md` §2-2 표의 "읽기" 열 18곳 + 주석 15곳 + `UnitMovementUseCase.IsWalkable()` 래퍼 메서드 1곳은 **한 글자도 고치지 않는다.** `tile.IsWalkable`이라는 접근 문법이 계산 프로퍼티 전환 후에도 동일(타입 `bool`, 읽기 전용 프로퍼티 접근)하므로 소스 호환이다. 대상 파일: `DebugUI.cs`, `UnitMovementUseCase.cs`, `UnitSpawnUseCase.cs`, `NetworkBuildingController.cs`, `AIOpponentController.cs`(807~809행 판정문은 **이번에 손대지 않는다** — 3단계 몫, `Research.md` §1), `CongestionAwarePathfinder.cs`, `SpiritAttackVfxTestSpawner.cs`, `UnitView.cs`, `HexFlowField.cs`, `HexPathfinder.cs`, `HexGrid.cs`.

- 이 30곳은 **컴파일 성공 여부로 검증**한다(§5). "이론상 안 바뀐다"를 결론으로 적지 않고 실제 빌드로 확인한다(`Research.md` 위험 #5).

---

## 3. 근거 요약표 (GameSystemRules ↔ 각 변경)

| 변경 항목 | 근거 |
|---|---|
| `IsWalkable` 계산 프로퍼티 전환 | TDD 526~532행, 583행 / `GameSystemRules_RandomMap.md` 5장 표(397~401행) |
| `TileKind` 3상태 단일 필드 | TDD 236~239행("2필드 구조는 폐기"), `GameSystemRules_RandomMap.md` 규칙 9·10 |
| `MineKind` 4값 | TDD 519행, `GameSystemRules_RandomMap.md` 규칙 3 |
| `HasBuilding` 동적 필드 | TDD 520·587·588행 |
| 광산 배치 `MineKind` 교체 | TDD 586행 |
| `AIOpponentController.cs` 미변경(3단계 이연) | TDD 590행이 이미 별도 항목으로 등재(오늘 이전 회차 승인 반영, `Research.md` §6-가) — 1단계 범위 아님 |
| `MapDefinition`/canonical/SHA-256 신설 | TDD 214~294행 |
| `Domain → Core 참조 금지` 준수 | `.claude/MEMORY.md` 「아키텍처 핵심 제약」, TDD 239행 |

---

## 4. 작업 순서 (컴파일 유지 전략)

`Research.md` 위험 #1이 지적한 대로, **`HexTile.cs`와 대입 5곳은 이름 충돌 때문에 나눠서 커밋할 수 없다.** 따라서 두 그룹으로만 나눈다.

1. **그룹 A (독립, 먼저 진행 가능)** — §2-1의 신설 6개 파일(`TileKind.cs`, `MineKind.cs`, `MapType.cs`, `DecorationDefinition.cs`, `MapDefinition.cs`, `MapDefinitionCodec.cs`)을 추가한다. 기존 코드는 전혀 건드리지 않으므로 이 시점에 한 번 컴파일해 신설 파일 자체의 문법 오류만 검증할 수 있다.
2. **그룹 B (원자적 단일 작업)** — 아래 5개를 **한 번의 연속 작업으로** 함께 바꾼다. 중간에 컴파일이 깨진 상태로 멈추지 않는다.
   - `HexTile.cs` 전환(§2-2)
   - `BuildingPlacementUseCase.cs` 3곳(§2-3 #1~#3) + `HasGoldMine` 읽기 3곳(§2-5)
   - `GameBootstrapper.Map.cs`의 `SetGoldMine` 재구성(§2-4)
   - `AIOpponentController.cs`의 `HasGoldMine` 읽기 1곳(§2-5) — **`IsWalkable` 807~809행은 그대로 둔다**
   - `HexGridRenderer.cs`의 `HasGoldMine` 읽기 1곳(§2-5)
3. **전체 컴파일 확인** — Unity 에디터가 그룹 B 완료 직후 전체 프로젝트를 재컴파일하고 콘솔에 에러가 0건인지 확인한다. 나머지 30여 곳(읽기 전용)이 실제로 컴파일되는지는 이 시점에 판정된다.
4. **§5 회귀 확인** 수행.

이 순서를 따르면 "새 타입만 먼저 넣고 아무도 안 쓰게" 분리는 **그룹 A(순수 신설)에 한해서만 가능**하고, `HexTile` 자체의 전환(그룹 B)은 목적상 분리할 수 없다는 것이 이번 검토의 결론이다 — `HexTile.IsWalkable`은 이미 게임 전체(빌드 배치·유닛 이동·AI·경로탐색)에서 활성으로 쓰이는 필드이므로, "만들되 쓰지 않는" 방식이 애초에 불가능하다(Research.md §1 요청에 대한 답).

---

## 5. 회귀 확인 방법 (실제로 확인할 것 — `check_docs.py`는 여기서 의미 없음)

> **[✅ 2026-09-03 수행 — 1~8 통과 · 9(멀티)는 미수행]** 에디터 싱글플레이 세션(2026-09-03 03:21:55, 임시 진단 로그 `Diag=RandomMapPhase1` 77줄 분석)으로 확인했다. 항목별 결과는 아래 표, 원문 목록은 그대로 둔다.
>
> | 항목 | 결과 |
> |---|---|
> | 1 콘솔 에러 0건 | ✅ |
> | 2 초기 배치 그대로 | ✅ **성 2 · 시작 채굴소 2 · 광산 타일 4** |
> | 3 중립 광산 이동 불가 | ✅ 광산 타일 4칸 전부 이동 불가. **그중 중립 광산 2칸은 건물이 없는데도 이동 불가** — 계산 프로퍼티가 상태에서 값을 만든다는 직접 증거다 |
> | 4 일반 건물 배치 시 이동 불가 전환 | ✅ |
> | 5 🔴 철거 시 복구 (최고 위험 지점) | ✅ **채굴소 철거 후 이동 불가 유지** — §2-3 #3에서 없앤 가드의 대체 논리가 실제로 작동함을 확인. **일반 건물 철거 후에는 이동 가능으로 복구** |
> | 6 경로탐색이 건물·광산을 피함 | ✅ **발급된 경로 43건 전부 중간에 이동 불가 칸 0개** |
> | 7 AI 건물 배치 정상 | ✅ **AI 건물 배치 2건 전부 성사** |
> | 8 `DebugUI` Walkable 표시 | ✅ |
> | 9 멀티 건물 배치 동기화 | ⚠️ **미수행** — 원문이 예상한 대로 에디터(Host)+빌드(Client) 구성이 필요해 이번 세션에서는 확인하지 못했다. **「통과」가 아니라 「미검증」으로 남긴다.** 후속은 `ROADMAP.md` |
>
> **판정: §5 판정 기준의 「1~8이 전부 이전과 동일」을 충족 → 1단계 완료.** 9는 원문이 이미 조건부로 적어 둔 항목이라 판정 기준에서 빠져 있었고, 실제로 미수행이므로 **완료 판정에 포함시키지 않는다.**

에디터 Play Mode(싱글플레이)로 기존 고정 맵을 로드해 아래를 **직접 확인**한다.

1. **콘솔 에러 0건** — Play Mode 진입 시 NullReferenceException 등 런타임 에러가 없어야 한다.
2. **초기 배치 그대로**: Blue/Red 성이 기존과 같은 위치에 자동 배치되고, 시작 채굴소(MiningPost) 2개가 금광 타일 위에 자동 건설되어 있다(외형·위치 변화 없음).
3. **중립 광산 2개가 여전히 이동 불가·건설 불가**: 그 타일을 클릭했을 때 일반 건물 배치 패널이 뜨지 않고(광산 자격 분기), 유닛이 그 타일 위로 직접 이동하지 못하며 우회한다.
4. **일반 건물 배치 시 즉시 이동 불가로 전환**: 임의의 빈 타일에 Barracks 등을 배치한 직후 그 타일 위로 유닛이 지나가지 못하는지 확인한다(§2-3 #1·#2 검증).
5. **🔴 건물 철거 시 원래대로 복구** — 이번 변경의 최고 위험 지점(§2-3 #3, `Research.md` 위험 #2):
   - 일반 건물(광산 아닌 타일) 철거 → 그 타일이 다시 이동 가능해지는지.
   - 채굴소(MiningPost, 광산 타일 위) 철거 → 그 타일이 **여전히 이동 불가로 유지**되는지(가드가 사라졌으므로 반드시 확인).
6. **유닛 경로탐색이 여전히 건물/광산을 피해 간다** — `HexPathfinder`/`CongestionAwarePathfinder`/`HexFlowField` 전부가 관여하므로, 건물로 막힌 경로에서 유닛이 우회하는지 확인한다.
7. **AI(Red)가 정상적으로 건물을 계속 배치한다** — `AIOpponentController.FindPlacementTile()`은 이번에 코드가 바뀌지 않지만, `HexTile.IsWalkable`이 계산 프로퍼티로 바뀐 뒤에도 같은 값을 반환하는지 간접 확인된다.
8. **`DebugUI` 오버레이의 Walkable 표시가 여전히 올바르다**(`DebugUI.cs:165`).
9. **멀티플레이 건물 배치 동기화** — `NetworkBuildingController.cs`/`PlaceBuildingWithId`(§2-3 #1) 경로. 에디터(Host)+빌드(Client) 구성이 필요해 이 작업만으로는 실기 검증이 어려울 수 있다 — 가능하면 실기, 불가능하면 코드 리뷰로 로직 대응만 확인하고 그 사실을 사용자에게 명시적으로 보고한다.

**판정 기준**: 위 1~8(그리고 가능하면 9)이 전부 이전과 동일하게 동작하면 1단계 완료. 하나라도 다르면 재작업.

---

## 6. 위험 요소와 대응 (요약 — 상세는 `Research.md` §5)

| 위험 | 대응 |
|---|---|
| 그룹 B 작업 중 컴파일이 장시간 깨진 채로 남음 | §4 순서대로 한 번의 연속 작업으로 그룹 B 전체를 마친 뒤에만 컴파일 확인 |
| `RemoveBuilding`의 가드 제거가 틀린 재해석일 가능성 | §5-5에서 채굴소 철거 케이스를 반드시 실기로 별도 확인 |
| `SetGoldMine` 구조 변경 중 좌표·팀 매핑 실수 | 원본 배열의 주석("Blue 시작 금광"/"Red 시작 금광")과 좌표값(`centerCol-2, blueRow`/`redRow`)을 그대로 유지한 채 호출부만 명시적으로 나눔 — 좌표 계산 로직 자체는 변경하지 않음 |
| `HasGoldMine` 부정 방향 반전 실수 | §2-5 표로 5곳 전부 명시, 리뷰 시 표와 코드 diff를 나란히 대조 |
| 신설 6개 파일이 실제로는 다른 곳에서 참조되기 시작해 "아무도 안 씀" 전제가 깨짐 | 그룹 B 작업 중 신설 타입을 `GameBootstrapper`나 기존 로직에 연결하는 코드를 추가하지 않는다 — 1단계 범위(`Research.md` §1)를 벗어나면 별도 승인 필요 |
| `AIOpponentController.cs:807~809`을 실수로 함께 고칠 위험 | §2-6에서 명시적으로 "이번엔 손대지 않는다"고 못박음. game-programmer에게 위임 시 이 문장을 그대로 전달 |

---

## 7. 구현 결과 (2026-09-03 추가)

### 7-0. 무엇이 끝났는지 (자연어 — CLAUDE.md 규칙 13)

계획대로 **타일의 「지나갈 수 있는가」를 저장하지 않고 계산하도록** 바꿨습니다. 이제 타일에는 *막힌 지형인가 · 광산이 있는가 · 건물이 서 있는가* 세 가지만 기록되고, 「지나갈 수 있는가」는 그 셋으로부터 매번 계산됩니다. 계산 결과라서 **손으로 덮어쓸 수 없습니다.**

계획이 세운 성공 기준은 하나였습니다 — **작업 전후로 지금 있는 고정 맵이 완전히 똑같이 동작할 것.** 에디터 실기로 확인한 결과 그대로였고, 특히 가장 걱정했던 **「채굴소를 부순 뒤에도 그 칸을 계속 못 지나가는가」가 통과**했습니다(§5 인용 블록).

새로 만든 자료구조 6개 중 **4개는 아직 아무도 부르지 않습니다.** 계획 §2-1이 의도한 그대로이며, 실제로 쓰이는 것은 2단계(맵 생성기)부터입니다.

### 7-1. 실적표 (계획 항목 ↔ 결과)

| 계획 항목 | 결과 | 비고 |
|---|---|---|
| §2-1 신설 6개 파일 | ✅ 그대로 | `TileKind.cs` · `MineKind.cs` · `MapType.cs` · `DecorationDefinition.cs` · `MapDefinition.cs` · `MapDefinitionCodec.cs` |
| §2-2 `HexTile` 전환 | ✅ 그대로 | 계산 프로퍼티 + 생성자 매개변수 제거 |
| §2-3 `IsWalkable` 대입 5곳 | ✅ 그대로 | #3(가드 제거) 포함 |
| §2-4 `SetGoldMine` 시그니처 변경 | ⚠️ **일부 다름** | 아래 §7-2 |
| §2-5 광산 읽기 5곳 | ✅ 그대로 | 부정 방향 반전 없음 |
| §2-6 순수 읽기 30곳 무변경 | ✅ 그대로 | 컴파일로 검증 |
| §2-6 `AIOpponentController.cs:807~809` 미변경 | ✅ 그대로 | 3단계 몫으로 남김 |
| §5 회귀 확인 1~8 | ✅ 통과 | 9(멀티)는 미수행 |

### 7-2. 🔴 계획과 달라진 점 — `startingMines` 배열은 지우지 못했다

**계획 §2-4의 「변경 후」 코드는 `startingMines`·`neutralMines` 배열을 없애고 좌표를 호출부에 직접 적는 형태**였다. 실제 구현은 **배열을 그대로 두고 호출부만 나눴다.**

- **이유:** `startingMines` 는 광산 타일 설정 말고도 **시작 채굴소 자동 건설에서 더 쓰인다** — 같은 파일의 `PlaceMiningPostDirect` 호출 2곳이 `startingMines[0]`·`startingMines[1]` 로 좌표를 다시 읽는다. 배열을 지우면 그 좌표가 갈 곳이 없어지고, 좌표를 두 벌로 복제하게 되어 **「좌표 계산이 두 벌로 갈라지지 않게 한다」는 §2-4의 원래 취지에 정면으로 어긋난다.**
- **실제 사용처는 4곳**(`startingMines[0][0]`/`[0][1]`/`[1][0]`/`[1][1]` 을 읽는 자리 기준)이며, 배열 선언까지 세면 파일 안에서 5개 자리가 이 배열을 참조한다.
- **좌표 값 자체는 계획과 동일하다**(`centerCol - 2, blueRow` / `centerCol - 2, redRow`). 팀 구분(`BlueStart`/`RedStart`)도 계획대로 정확히 들어갔다.
- `neutralMines` 도 같은 이유가 아니라 **단순히 전부 같은 종류라서** `foreach` 를 유지했다(계획은 명시적 2회 호출을 그렸다). 동작은 동일하다.

> **교훈:** 계획의 「변경 후」 코드 블록이 **그 변수를 쓰는 다른 자리를 다 보고 쓴 것인지** 확인해야 한다. 이번 §2-4는 `PlaceGoldMines()` 안의 광산 설정 부분만 보고 쓰였고, **같은 함수 아래쪽의 시작 채굴소 자동 건설 구간을 시야에 넣지 않았다.**

### 7-3. ⚠️ 미완 단서 (과대 표기 금지)

1. **무작위 맵 자체는 미구현이다.** 이번에 끝난 것은 타일 상태 계약 전환뿐이고, 지금 경기는 여전히 고정 맵을 쓴다. 맵 생성기 5종·검증기·폴백은 **2단계**다.
2. **신설 6종 중 `MapType`·`DecorationDefinition`·`MapDefinition`·`MapDefinitionCodec` 4종은 호출부가 0곳이다.** 계획 §2-1이 의도한 상태 그대로이며, 이는 곧 **이 4개의 정확성은 아직 아무 방법으로도 검증되지 않았다**는 뜻이다(컴파일만 통과했다).
3. **AI 건물 배치 후보 판정(807~809행)과 그 XML 주석(770~773행)은 전환하지 않았다 — 3단계 몫이다.** 다만 **같은 파일의 광산 타일 조회(`CacheMineTiles`, 224행)는 §2-5대로 전환됐으므로**, `AIOpponentController.cs` 안에는 전환된 자리와 안 된 자리가 함께 있다. 한쪽을 보고 다른 쪽을 단정하지 말 것.
4. **건설·점령의 전용 조건 전환도 미완**이다(TDD 「기존 코드 전환 요구」의 다섯 번째 항목 뒤 절반). 일반 건물 배치가 아직 이동 가능 여부를 그대로 쓰고, 점령에 막힌 지형 확인이 없다. 지금 고정 맵은 `TileKind` 를 설정하는 코드가 한 곳도 없어 모든 타일이 `Normal` 이라 결과가 갈리지 않을 뿐이다.
5. **멀티플레이 실기 미검증**(§5-9). 계획이 조건부로 예상한 그대로다.
6. **임시 진단 로그는 전량 제거됐다** — `5649a7e` 투입 → `51516a1` 에서 224행 순수 삭제, `Diag=RandomMapPhase1` grep **0건**.

### 7-4. 변경 파일 리스트업 (WORKFLOW [12])

**코드 (구현 커밋 `cee857d` 기준)**

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Domain/Hex/HexTile.cs` | 상태 3축 + 계산 프로퍼티로 전환, 생성자 매개변수 제거, 광산 표시 필드 삭제 |
| `Assets/_Project/Scripts/Domain/Hex/TileKind.cs` | **신설** |
| `Assets/_Project/Scripts/Domain/Hex/MineKind.cs` | **신설** |
| `Assets/_Project/Scripts/Domain/Map/MapType.cs` | **신설** (호출부 0곳) |
| `Assets/_Project/Scripts/Domain/Map/DecorationDefinition.cs` | **신설** (호출부 0곳) |
| `Assets/_Project/Scripts/Domain/Map/MapDefinition.cs` | **신설** (호출부 0곳) |
| `Assets/_Project/Scripts/Domain/Map/MapDefinitionCodec.cs` | **신설** (호출부 0곳) |
| `Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs` | 대입 3곳 + 광산 읽기 3곳 전환, `RemoveBuilding` 가드 제거 |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs` | `SetGoldMine(col, row, MineKind)` 로 시그니처 변경 (§7-2 참조) |
| `Assets/_Project/Scripts/Application/Services/AIOpponentController.cs` | 광산 타일 조회 1곳만 전환 (**807~809행·770~773행은 무변경**) |
| `Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs` | 광산 읽기 1곳 전환 |

**문서 (2026-09-03 이 사이클)**

`GameSystemRules/GameSystemRules_AI.md` · `GameDesignDocument.md` · `TechnicalDesignDocument.md` · `PROJECT_STATUS.md` · `ROADMAP.md` · `WORK_HISTORY.md` · 이 폴더의 `Research.md`·`Plan.md` · `.claude/agent-memory/game-programmer/{hex-grid.md,logging.md}` · `.claude/agent-memory/document-manager/{MEMORY.md,doc-conventions.md}`

> **※ 근거 구분(규칙 10):** 커밋 해시·삭제 행수(224행)·실기 로그 수치는 **호출 세션이 전달한 값**이다. `.cs` 전수 검색 결과(광산 표시 필드 0건 · 신설 4종 호출부 0곳 · `TileKind` 설정 코드 0곳 · `startingMines` 참조 5곳 · `Diag=RandomMapPhase1` 0건)는 **이 문서 작업 중 직접 실측한 값**이다.
