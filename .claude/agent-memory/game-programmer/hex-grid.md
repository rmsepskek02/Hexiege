# Game Programmer — 헥스 그리드 & 좌표/뷰

헥스 좌표계, HexMetrics, ViewConverter, 그리드 렌더링, 카메라.
(좌표계 3D 전환 / 카메라 상세는 [3d-transition.md], [camera-and-view.md] 참조)

---

## 헥스 좌표계 (FlatTop)

- FlatTop 헥스, XZ 평면 (Y는 높이)
- 인접 거리 = 0.866 (= HexMetrics.TileHeight 기반)
- `HexCoord.Distance(a, b)` — 도메인 정수 거리 (월드 거리보다 우선 — ViewConverter 무관, 부동소수점 오차 없음)

### HexCoord 인접 탐색 (표준 패턴)
- `HexMetrics.GetNeighbors`는 부재. `HexGrid.GetNeighbors`는 `List<HexTile>` 반환(순수 좌표 탐색에 부적합)
- 순수 좌표 인접: `HexDirectionExtensions.Count` + `((HexDirection)i).Neighbor(coord)`
- DirectionAngles `{60,120,180,240,300,0}` — 각 방향 실제 Unity 월드 각도. NW(5)=0° (Q=0,R-1 → delta(x:0,z:+1) → atan2(0,1)=0°)

### HexCoord(0,0) 주의
- (0,0)은 일반 타일일 수 있음. `IsInvalid`((0,0) 약속 기반 사설 헬퍼)는 점령 판정에 부적합
- 그리드 경계 검증은 `_grid.HasTile(tile)` 사용
- `default`(미등록) 약속으로 (0,0) 사용 시 기존 관례와 일관성 유지

---

## HexMetrics

- `HexToWorld` / `WorldToHex` — 도메인 좌표 ↔ 월드
- `GridCenter(width, height)` — 맵 중앙. ApplyConfig 이후 호출(준비 완료 후)
- TileHeight — FlatTop 세로 간격, 사거리 계산 기준 (`AttackRange * TileHeight + Epsilon`)

### HexMetrics 초기화 = ApplyConfig 단일 경로 (Phase 2, 2026-06-25)
- `GameBootstrapper.Setup.cs`의 `ApplyConfig(orientation, oc)`가 HexMetrics 설정의 단일 소스: Orientation/HexOrientationContext.Current/TileWidth/TileHeight/**UnitYOffset**
- `StartNetworkGame`(Network.cs): 기존 HexMetrics 수동 설정 4줄 → `ApplyConfig(HexOrientation.FlatTop, oc)` 1줄로 대체. 수동 4줄엔 UnitYOffset이 빠져 있던 부분 중복(partial dup) 해소
- **ApplyConfig는 멱등** — 멀티 경로에서 StartNetworkGame 1회 + LoadMap 내부 1회 = 총 2회 실행되나 같은 값 재대입이라 부작용 없음
- 순서 제약 유지 필수: `ApplyConfig(FlatTop) → GridCenter → ViewConverter.Setup → LoadMap`. ViewConverter 사전 설정은 HexMetrics가 FlatTop 준비된 뒤·LoadMap 전에 와야 함
- 싱글 경로(Map.cs ViewConverter 설정)는 미변경. 수동 4줄은 주석 보존 중(별도 지시 시 삭제)

---

## ViewConverter (Red팀 반전)

- Red팀 좌표/방향 반전 (위치만 반전, 회전은 변환 안 함)
- `ViewConverter.Setup(isRed, mapCenter)` — LoadMap() 이전 호출 필수. ApplyConfig 직후 LocalPlayerTeam 기반
- `ViewConverter.Reset()`은 항상 Blue 고정이므로 싱글플레이 Red팀에서 버그 → Setup 사용
- `ViewConverter.IsFlipped` — 로컬 플레이어 팀 판별 (`IsFlipped ? Red : Blue`)
- `FromView` / `ToView` — 뷰↔도메인 변환. 도메인 좌표로 점유/거리 추적, 비교 시점에만 ToView
- 상대 진영 오브젝트 회전: ViewConverter가 회전 변환 안 하므로 Y축 180도 수동 적용

---

## 타일 소유권

- `HexGrid.GetOwner(HexCoord)` — TryGetValue → tile.Owner 또는 Neutral
- `_ownedTileCounts: Dictionary<TeamId, int>` 캐시 — CountTilesOwnedBy O(187)→O(1). SetOwner 시 ±1 갱신
- `TileOwnershipService`(Application/Services): Pull 모델. 매 프레임 유닛 viewPos → FromView → WorldToHex 역산 → `Dictionary<HexCoord, HashSet<TeamId>>`. 한 팀만 있고 GetOwner!=claimingTeam일 때만 SetOwner+OnTileOwnerChanged. HashSet 풀
- 점령 규칙: 한 팀만 있을 때만 갱신, 양 팀 동시면 유지(분쟁지), 비어있으면 유지(영구화)
- 서버 가드: 싱글(`!IsNetworkActive`) + Host(`IsNetworkServer`) 통과, 순수 Client 차단

---

## 그리드 렌더링

- `HexGridRenderer` — 타일/광산 렌더
- 중립 광산: `_goldMineObjects` Dictionary. 초기 숨김(`tile.Owner != Neutral`). HideGoldMine/ShowGoldMine. OnBuildingPlaced→Hide / OnEntityDied(MiningPost)→Show

---

## 패스파인딩

- `HexPathfinder.FindPath()` — goal blocked 체크 제거(목표 타일이 선점돼도 탐색). blocked는 경로 중간 타일만, 도착 충돌은 ProcessStep
- CongestionAwarePathfinder — 혼잡도 가중 A* (unit-building.md 참조)
- 근접 유닛 non-walkable 목표(Castle): 경로에 Castle 타일 추가 → Lerp 이동 연장으로 접근

---

## 카메라

- CameraController: 줌 DOTween 보간. `_targetZoom`/`_zoomTween`(Kill 후 새 Tween)/`_zoomDuration`(0.25f). Awake 초기화, OnDestroy Kill
- 카메라 초기 위치는 맵 중앙 유지 (SetCameraStartPositionForTeam 호출 금지)
- ClampPosition은 매 프레임 orthographicSize 읽음

---

## Android URP RenderTexture 잔상 (캐릭터 프리뷰)

- 근본 원인: RT 에셋(m_AntiAliasing:2)과 카메라(allowMSAA=false) sample 불일치 → clear 실패 → 잔상
- 체크리스트: RT m_AntiAliasing:1(YAML 직접 확인), Camera allowMSAA/allowHDR=false, backgroundColor.alpha=1, URP antialiasing=None / renderType=Base / renderShadows=false

---

## HexTile state contract — `TileKind` / `MineKind` / `HasBuilding` (2026-09-02 phase 1)

Random-map work, phase 1 of 3. **Structure change only — no behavior change intended.**
Task docs: `Assets/_Project/Docs/_Tasks/2026-09-01/19_49_random-map-phase1-tilekind/`.
Contract single source: `TechnicalDesignDocument.md` 「`HexTile` 런타임 상태 계약」.

**What `HexTile` looks like now** (`Domain/Hex/HexTile.cs`)

- `TileKind TileKind` (Normal/NoBuild/Blocked) — map definition, static during a match, setter kept for load time
- `MineKind MineKind` (None/Neutral/BlueStart/RedStart) — projected from the mine placement list at load
- `bool HasBuilding` — dynamic, set on place / cleared on remove
- `bool IsWalkable => TileKind != Blocked && MineKind == None && !HasBuilding` — **computed, no setter**
- Constructor is `HexTile(HexCoord, TeamId = Neutral)`; the old `isWalkable` parameter is gone
  (only caller is `HexGrid.Generate()` at `HexGrid.cs:93`, which used the default).

**Where the writes live now** (these are the ONLY writes in the codebase)

- `HasBuilding = true` — `BuildingPlacementUseCase.PlaceBuildingWithId` and `.PlaceBuildingInternal`
  (`PlaceBuilding` / `PlaceMiningPost` / `PlaceMiningPostDirect` all funnel through `PlaceBuildingInternal`)
- `HasBuilding = false` — `BuildingPlacementUseCase.RemoveBuilding` only, **unconditionally**.
  The old `if (!tile.HasGoldMine)` guard is gone on purpose: the computed `IsWalkable` already requires
  `MineKind == None`, so a mine tile stays unwalkable by itself. `UpgradeBuilding*` does NOT go through
  `RemoveBuilding` (it removes from `_buildings` directly), so `HasBuilding` never gets cleared while
  a building still stands.
- `MineKind = ...` — `GameBootstrapper.Map.cs` `PlaceGoldMines()` local `SetGoldMine(col, row, MineKind)`.
  Starting mines are called out explicitly (`BlueStart` / `RedStart`), neutral mines stay a `foreach`.
  The `startingMines[][]` array is still needed below for the auto-built MiningPosts — **do not delete it.**

**Reads are source-compatible.** ~30 `tile.IsWalkable` read sites needed no edit at all.

**Deliberately NOT changed (phase 3):** `AIOpponentController.cs` 807~809 placement predicate and its
XML comment at 770~773. TDD 「기존 코드 전환 요구」 lists it separately. Only the `HasGoldMine` read at
line 224 was converted. So `grep -rnE "IsWalkable\s*=[^=]" Assets/ --include=*.cs` legitimately returns
**1 comment hit** at `AIOpponentController.cs:771` — that is expected, not a leftover.

**New Domain types, deliberately unreferenced** (`Domain/Hex/TileKind.cs`, `Domain/Hex/MineKind.cs`,
`Domain/Map/{MapType,DecorationDefinition,MapDefinition,MapDefinitionCodec}.cs`)

- All `namespace Hexiege.Domain` (the Domain tree is flat — every file uses that one namespace).
- `MapDefinition` = 상위 필드 + `TileKind[]` row-major (`index = row * Width + col`) + castle/starting-mine
  (`MapObjectPlacement`: tile index + team) + neutral mine (tile index) + `DecorationDefinition` lists.
- `MapDefinitionCodec` = canonical little-endian binary (hand-rolled writes — **`BitConverter` is
  platform-endian and must not be used here**) + SHA-256 over those bytes, hash field itself excluded.
- Nothing calls them. The map generator is phase 2, NGO transfer is phase 3.

**Trap for the next session:** `public TileKind TileKind { get; set; }` plus `TileKind != TileKind.Blocked`
relies on C#'s color-color rule (a property may share its type's name). It is legal; do not "fix" it by
renaming the property.

**Editor playtest result (2026-09-03, temp `Diag=RandomMapPhase1` log, 77 lines):** initial layout 2 castles /
2 starting MiningPosts / 4 mine tiles; all 4 mine tiles unwalkable and **the 2 neutral ones are unwalkable
with no building on them** (direct evidence the computed property derives from state); 🔴 **after demolishing
a MiningPost the tile stays unwalkable** — this is the replacement logic for the removed mine-flag guard and was
the highest-risk point of the whole transition; a normal building's tile goes back to walkable; 43 issued paths
contained 0 unwalkable intermediate tiles; both AI building placements succeeded. **Multiplayer is still
unverified** — this was an editor single-player session only. (Figures relayed by the calling session.)

---

## Deterministic map PRNG — `MapRandom` / `MapRandomStreams` (2026-09-03 phase 2, step A)

Random-map phase 2, step **A** (the input every later step depends on). Plan §4-A of
`Assets/_Project/Docs/_Tasks/2026-09-03/03_14_random-map-phase2-generator/Plan.md`.
Contract single source: `TechnicalDesignDocument.md` 「결정적 PRNG 및 독립 스트림 계약」;
rule single source: `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 3 · 규칙 12.
**When the two disagree, the rules document wins** (the TDD says so itself).

**Files** — both `namespace Hexiege.Domain`, pure C#, no `UnityEngine` and no Core reference.

- `Domain/Map/MapRandom.cs` — SplitMix64. 64-bit state, `unchecked` everywhere.
  `Gamma = 0x9E3779B97F4A7C15`, finalizer multipliers `0xBF58476D1CE4E5B9` / `0x94D049BB133111EB`,
  shifts 30/27/31. Public surface: `Mix64` · `Combine` (static, pure), `NextUInt64` ·
  `NextInt(max)` · `NextInt(min,max)` · `Choose(IReadOnlyList<int>)` · `NextBool` · `DrawCount`.
- `Domain/Map/MapRandomStreams.cs` — fixed integer stream IDs
  `MapSelection=1 · Terrain=2 · MinePlacement=3 · Decoration=4` (0 reserved for "unset"),
  `MaxAttemptCount=100`, `DeriveDomainSeed` / `DeriveAttemptSeed`,
  `CreateMatchStream` (match-level, MapSelection) / `CreateAttemptStream` (per attempt),
  plus the self-check vectors.

**Derivation order (fixed — changing it invalidates every past seed)**

```
domainSeed  = Combine(Combine((uint)mapVersion, rootSeed), (uint)streamId)
attemptSeed = Combine(domainSeed, (uint)attemptIndex)
Combine(seed, salt) = Mix64(seed + Gamma * (salt + 1))
```

`salt + 1` exists because **`Mix64(0) == 0`** (known SplitMix64 finalizer property) — without it a
zero salt would be a no-op. Same reason `NextUInt64` advances the state *before* mixing.

**Why not `% n`**: `NextInt` rejects `r < (2^64 mod bound)` and only then takes the remainder.
Plain modulo is biased and 규칙 3/5 demand equal probability. Rejection chance is `bound / 2^64`.

**How the four TDD guarantees are met** (the four sentences are quoted verbatim in the file header):
one `MapRandom` instance per stream, state lives only inside the instance, and an attempt seed is
recomputed from `(domainSeed, attemptIndex)` — never continued from the previous attempt's generator.
🔴 **Reusing a previous attempt's `MapRandom` instance breaks guarantee 3 instantly.**

**Test vectors live in code, not in a test assembly.** There is no unit-test assembly in this project
(2026-09-03: the only `.asmdef` under `Assets/` is the external `ai.meshy` package). So
`MapRandomStreams.TryRunSelfCheck(out string)` holds hard-coded expectations (computed with an
independent Python implementation) and `AssertSelfCheck()` wraps it with
`[System.Diagnostics.Conditional("UNITY_EDITOR")]`. `TryRunSelfCheck` itself is **not** Conditional so a
future test assembly can call it directly. Anchor values, `mapVersion=1`, `rootSeed=0x0123456789ABCDEF`:

| | |
|---|---|
| `Mix64(1)` | `0x5692161D100B05E5` |
| `Combine(0,0)` = first draw of seed 0 | `0xE220A8397B1DCDAF` |
| domainSeed MapSelection / Terrain / MinePlacement / Decoration | `0x1B2F2F00FA7AD69C` / `0x1626569ABECE1769` / `0xF68B89A15F89931E` / `0xBF73AACBB7A78706` |
| attemptSeed Terrain-0 / -1 / -99 | `0x4E5F400C26BB210B` / `0x75BC33FA43E1A9A4` / `0xCC6B972591720A76` |

🔴 A self-check failure means **the PRNG spec changed**, not that the test is wrong. Decide whether
`MapVersion` must be bumped before touching the constants.

**Config fields (Infrastructure)** — `GameConfig.cs` gained a `[Header("Random Map Test Mode")]` block:
`_mapTestModeEnabled` → `MapTestModeEnabled` (bool, default off) and `_testStartingGold` →
`TestStartingGold` (int, 5000). The public names are fixed by 규칙 3 · 규칙 12 · TDD — **do not rename.**
Serialized in `Assets/_Project/Resources/Config/GameConfig.asset` as `_mapTestModeEnabled: 0` /
`_testStartingGold: 5000`. ⚠️ The pre-existing `_startingGold: 5000` is a **different field**; whether the
two are really the same thing is still unconfirmed (`Research.md` §9-3) — it was left untouched.

**Comment hygiene applied here** (`.claude/mistakes.md` 2026-09-02, the three-times trap): the header
that explains *why* the banned RNG APIs must not be used spells their names in prose, never in dotted
code form, so `grep` for banned APIs over `Assets/` returns 0 hits inside these files. A note in the file
says the phrasing is deliberate — don't "tidy" it back into code form.

**Still not built (steps B~K):** `SymmetricMapBuilder`, `InitialMapStateEvaluator`, the 5 archetype
generators, `NeutralMineSampler`, `MapDefinitionValidator`, fallback templates, the map-prep coordinator,
`MapDefinition` → `HexGrid` projection, predicate switchover, renderer, log keys. Nothing calls
`MapRandom` yet.

**[🔴 2026-09-03 correction — original sentence above kept]** Steps **B and C are now built.** `SymmetricMapBuilder.cs`
(step B, `Domain/Map/`) and `InitialMapStateEvaluator.cs` (step C, below) exist. Still not built: the 5
archetype generators, `NeutralMineSampler`, `MapDefinitionValidator`, fallback templates, the map-prep
coordinator, `MapDefinition` → `HexGrid` projection, predicate switchover, renderer, log keys.
Nothing outside the two files' own self-checks calls them yet.

---

## Initial map state — `InitialMapStateEvaluator` (2026-09-03 phase 2, step C)

Random-map phase 2, step **C**. Plan §4-C of
`Assets/_Project/Docs/_Tasks/2026-09-03/03_14_random-map-phase2-generator/Plan.md`.
Contract single source: `TechnicalDesignDocument.md` 「초기 소유권 단일 소스」 (inside 「`MapDefinition` 정규
데이터 계약」); rule single source: `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 2 · 규칙 13 검증 3번.

**File** — `Assets/_Project/Scripts/Domain/Map/InitialMapStateEvaluator.cs` (+ `.cs.meta`,
guid `33e102f310014010bcfe0b0851d318fd`). `namespace Hexiege.Domain`, pure C#, no `UnityEngine`, no Core.

**Why it exists**: three consumers need the same derivation — the generator (must keep neutral mines off the
protected tiles), the validator (규칙 13 검증 3번), and runtime initial castle/mining-post placement +
ownership. `MapDefinition` stores no per-tile initial owner; castle + starting-mine positions are the only input.

**Shape** — constructor takes a `MapDefinition` and **snapshots** everything (owned / occupied / buildable /
unique / shared / protected sets). ⚠️ Mutating the definition afterwards does not refresh the instance;
make a new evaluator. This is deliberate (the generator asks the same question many times per attempt).

Public surface: `RequiredBuildableTileCount = 10` · `MaxNeighborCount = 6` · `OffsetToCube` / `CubeToOffset` ·
`GetNeighborIndices` (static width/height form + instance form, buffer-filling) · `CollectNeighborIndices` ·
`GetInitialOwnedTiles(team)` · `GetInitialOwner(index)` · `GetBuildableTiles(team)` ·
`GetUniqueBuildableTiles(team)` · `GetUniqueBuildableTileCount(team)` ·
`TryValidateBuildableTileCount(out reason, out blue, out red)` · `GetMineKind` · `HasInitialBuilding` ·
`OccupiedTiles` / `ContestedOwnedTiles` / `SharedBuildableTiles` / `ProtectedTiles` ·
`TryRunSelfCheck(out string)` / `AssertSelfCheck()`.

🔴 **Neighbours must go through cube coordinates.** `MapDefinition` indexes by offset (col,row) row-major, but
hex adjacency is only defined in cube space. The order is always: `HexGrid.OffsetToCube(col,row,FlatTop)` →
`((HexDirection)d).Neighbor(cube)` (`HexDirectionExtensions.Count` = 6) → **even-q inverse back to offset**
(`col = q; row = r + (col - (col & 1)) / 2`) → drop anything outside the grid. Picking "up/down/left/right"
in the offset table is wrong for half the columns and the error is invisible. The project has **no
`CubeToOffset` API** — `InitialMapStateEvaluator.CubeToOffset` is the first one; the same two lines were
previously inlined in `SymmetricMapBuilder.cs` self-check (~line 705).

**Two meanings of 「고유(unique)」 — both implemented**
1. within a team: duplicate coordinates counted once (TDD 판정 3번) — every result is a `HashSet<int>`, so
   this is structural. The castle ring and the starting-mine ring really do overlap (2 tiles per team).
2. across teams: a coordinate in **both** teams' buildable sets is unique to neither, so
   `GetUniqueBuildableTiles` subtracts `SharedBuildableTiles` from both sides. The 10-count check uses this.
   ⚠️ `ProtectedTiles` deliberately does **not** apply (2) — a tile both sides touch still must not get a mine.
   On the canonical layout the overlap is 0, so (1) and (2) give the same answer.

**Measured on the canonical layout** (Blue castle (5,19) via `SetCastlePair` → Red (5,1) by rotation;
case A Blue starting mine (3,19) → Red (7,1); case B (7,19) → (3,1)); terrain otherwise empty:

| | case A | case B |
|---|---|---|
| initial owned tiles per team | 12 (7+7 minus 2 overlap) | 12 |
| contested owned / shared buildable | 0 / 0 | 0 / 0 |
| **unique buildable per team** | **10 / 10** | **10 / 10** |
| protected tiles (occupied 4 + 20) | 24 | 24 |

So 규칙 2's 10 does come out on an empty-terrain map — no fudging was needed.

**Self-check items** (`TryRunSelfCheck`, expectations derived independently in Python before writing the C#):
offset↔cube round trip over all 231 cells · every returned neighbour at cube distance 1, no self, no dupes ·
**exactly 60 cells have fewer than 6 neighbours** (= perimeter 11·2 + 21·2 − 4; interior cells all have 6) ·
`(0,0)` has 2 neighbours and `(5,10)` has 6 · neighbour relation is reciprocal · the two 규칙 2 layouts above ·
and a **negative control**: blocking the pair `(2,19)↔(8,2)` drops both teams to 9 and the count check must
fail. Without the negative control a check that accepts anything would look identical to a correct one.

**Verification without a compiler** (no `dotnet`/`mcs`/`csc`/`mono` in this environment): comment- and
string-stripped bracket/paren stack balance, `using`/namespace inspection, a scripted check that every public
member carries an XML doc, and a Python port of the whole self-check to produce the hard-coded expectations.

**Comment hygiene** (`.claude/mistakes.md` 2026-09-02): no assignment-shaped identifier text in comments
(the row-major index formula is written as prose), no literal mention of retired identifiers.

---

## Archetype generators, step D-1 — `Domain/Map/Generators/` (2026-09-03 phase 2)

Random-map phase 2, step **D-1**: the shared skeleton, the neutral-mine sampler, and the two *open*
archetypes. 협곡형 · 외곽형 · 3갈래형 (D-2) and the validator (E) are **not** built.
Rule single source: `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 1 · 3 · 4 · 5 · 6 · 15.

**Files** (all `namespace Hexiege.Domain`, pure C#, no `UnityEngine`, no Core; each has a fresh 2-line `.cs.meta`,
plus a new `Generators.meta` folder asset)

| file | what |
|---|---|
| `IMapArchetypeGenerator.cs` | contract + `MapStartingMineSide` · `MapGenerationRequest` · `MapCorridorRequirement` · `IMapArchetypeConstraints` · `MapArchetypeConstraints` · `MapGenerationResult` |
| `MapArchetypeGeneratorBase.cs` | template-method `Generate`, **the only place castles/starting mines are placed**, the probe helper, and the shared self-check helpers |
| `NeutralMineSampler.cs` | 규칙 3, type-agnostic |
| `OpenGenerator.cs` | 규칙 4 — adds **no** terrain, draws **zero** from Terrain |
| `ObstacleOpenGenerator.cs` | 규칙 5 |

**Where per-type values live — and why they are split in two**

- Fixed before generation → on the generator: `MapType`, `MinNeutralMineCount` / `MaxNeutralMineCount`,
  `IsNeutralMineCountAllowed` (virtual, so a "even counts only" type needs one override).
- **Decided by the draw** → on `IMapArchetypeConstraints`, produced *per attempt* and carried in
  `MapGenerationResult.Constraints`: `IsNeutralMineForbidden(col,row)` (④), `IsBuildForbidden` (⑤),
  `RequiredCorridors` (⑥). 🔴 Putting ④⑥ on the generator instance would be a bug for D-2 — the canyon's
  corridor width/position is drawn per attempt, so the previous attempt's zone would leak into the next one.
  D-2 fits: canyon/outer fill the forbidden set + corridors, three-lane emits three corridors.

**Common layout is `MapArchetypeGeneratorBase.ApplyCommonLayout(builder, side)`** — public static so the
sampler's self-check reuses it. Only Blue coordinates appear: `SetCastlePair(5,19,Blue)` and
`SetStartingMinePair(3 or 7, 19, Blue)`; the Red side comes from the builder's rotation. Case A uses col 3,
case B col 7.

**The probe trick (`CreateInitialStateProbe`)** — `InitialMapStateEvaluator` needs a finished `MapDefinition`,
but `SymmetricMapBuilder` deliberately never exposes the in-progress one. So the base replays the current
terrain plus the common layout into a *second* builder through `SetPair`/`SetCenter` and evaluates
`probe.Build()`. Never reimplement the protected-tile derivation locally — three consumers must agree.

**Rejection sampling lives in `NeutralMineSampler.TryPickDistinctSlots`** (draw, redraw on a repeat, cap
`MaxRedrawCount = 1000`). `ObstacleOpenGenerator` calls the same helper for its per-row distinct columns, so
규칙 6's "같은 확률 분포에서 다시 뽑는다" has exactly one implementation. Type-level *zone* constraints are
applied by pre-filtering the candidate list, not by redrawing.

**Rejection of a whole attempt is a return value, never an exception**: `MapGenerationResult.IsAccepted`
false + `RejectionReason`, and `Reject` refuses to carry a `Definition` so there is no path to "repair" a
half-built map (규칙 6). A bad `NeutralMineCount` in the request *does* throw — that is a caller bug, not a
bad draw, and must not hide inside the retry loop.

**ObstacleOpen algorithm (규칙 5), the part that is easy to get wrong**

- Draw unit is the **row** (always 11 cells wide → uniform density); the *band* is expressed in **height
  steps** (row·2, +1 on odd columns, 1~41, 21 is the centre line, rotation sends L to 42−L). The two are
  not the same word and the comments say so.
- Rows 3~9, each independently 0/1/2/3/4 obstacles at 20% each, distinct columns. Rows 0~2 stay empty, and
  so does their rotated band (**even cols 19~20, odd cols 18~20** — they differ). The lower projection lands
  on **rows 11~18**, which does not line up with 3~9.
- Centre line = **odd columns of row 10 only** (5 cells). 0/2/4 obstacles at 1/3 each, placed as the pairs
  `(1,10)↔(9,10)` and `(3,10)↔(7,10)`. `(5,10)` is the rotation fixed point and never gets a solo obstacle.
- Zero obstacle pairs → reject the attempt.

**Measured figures (independent Python port, mapVersion 1, attemptIndex 0, rootSeed 0~199)**

| | |
|---|---|
| obstacle total over 200 seeds | **5904**, mean **29.52** (theory 30; sd of the mean ≈ 0.54) |
| attempts rejected in those 200 | 0 |
| rows ever holding an obstacle | 3~18 |
| protected tiles, both mine cases | 24 |
| **neutral-mine candidate pairs, open map** | **100** (= 112 paired non-centre pairs − 12 protected pairs) |
| unique buildable per team | 10 / 10 |

`ObstacleOpenGenerator.SelfCheckExpectedObstacleTotal = 5904` is asserted exactly, plus a soft mean band of
27~33. 🔴 If it fails, the draw order or the probabilities changed — decide whether `MapVersion` must be
bumped before touching the constant.

**Self-check coverage** (`TryRunSelfCheck` / `AssertSelfCheck`, same shape as steps A·B·C; the shared
verifiers `TryVerifyRotationalSymmetry` and `TryCompareDefinitions` are static on the base class):
full-grid rotational symmetry (terrain + castles + starting mines + neutral mines) · forbidden band empty ·
centre line paired-only with the fixed point empty · same seed same map · `DecorationDrawCount == 0`
(규칙 15) · `TerrainDrawCount == 0` for the fully-open type · fully-open blocks nothing beyond the 6
unpaired cells · candidate-pair count and the odd-count centre mine · **a negative control** (leave 2
candidate pairs, ask for 6 mines, it must be refused while 4 still succeeds).

**Hash is deliberately left null** by the generator — it must be computed over the canonical bytes by
whoever exports the map (coordinator), not here.

**Verification without a compiler** (no `dotnet`/`mcs`/`csc`/`mono`): comment/string-stripped bracket
balance, `using`/namespace/banned-reference scan (`UnityEngine` · `Hexiege.Core` · `UnityEditor` all 0 hits),
a scripted XML-doc check over every public/protected member, and a full Python port of the five files that
reproduces every hard-coded expectation above. **Nothing outside the self-checks calls these classes yet.**
