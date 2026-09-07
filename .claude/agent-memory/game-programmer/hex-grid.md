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

---

## Archetype generators, step D-2 — 협곡형 · 외곽형 · 3갈래형 (2026-09-04 phase 2)

Random-map phase 2, step **D-2**. Built on D-1 unchanged — **no D-1 file was edited.**
Rule single source: `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 6 · 7 · 8 · 15.

**Files** (all `namespace Hexiege.Domain`, pure C#, no `UnityEngine`/`Hexiege.Core`/`UnityEditor`;
each with a fresh 2-line `.cs.meta`)

| file | guid | what |
|---|---|---|
| `Domain/Map/Generators/MapBandTable.cs` | `3c1fc168b50c486a84150542ac5a6a55` | shared height-step/band table + `MapTileKindSelector` delegate + `ApplySymmetricTerrain` |
| `Domain/Map/Generators/CanyonGenerator.cs` | `1577a0cac39445739874ee3132d0cfca` | 규칙 6 |
| `Domain/Map/Generators/OuterGenerator.cs` | `43d06f7cb28d4c94b943c2071ea0181d` | 규칙 7 (+ `OuterMassShape` enum) |
| `Domain/Map/Generators/ThreeLaneGenerator.cs` | `cf51381d1f9a43a19bf782e1e272357c` | 규칙 8 |

**Height step is the only safe way to name a band.** `heightStep = row*2 + (col&1)`, range 0~41
(0 is only the 6 unpaired cells), **21 is the centre line**, rotation is `L -> 42-L`
(`MapBandTable.HeightStepSum = 42`). 🔴 **`MapBandTable.CentralBandTileCount = 39` is measured, not
assumed**: height steps 18~24 = even cols rows 9~12 (6x4=24) + odd cols rows 9~11 (5x3=15) = **39**,
asserted in `MapBandTable.TryRunSelfCheck` by building the set and comparing its size. Rows 9~11 is
**not** rotation-closed — that is the negative control in that same self-check.

**The 분리 길이 row-range table lives in ONE place** — `MapBandTable.GetBandRowRange(length, isOddColumn, …)`,
derived from `GetRowRange(minStep, maxStep, …)`, and both `OuterGenerator` and `ThreeLaneGenerator`
read it. Values reproduce 규칙 8's table exactly (5 -> even 8~13 / odd 8~12; 7 -> 7~14 / 7~13;
9 -> 6~15 / 6~14; 11 -> 5~16 / 5~15). minRow is the same for both parities; only maxRow differs by 1.

**Every generator writes rows 0..CenterRow only** (`MapBandTable.ApplySymmetricTerrain`): centre cell
via `SetCenter`, unpaired cells skipped, everything else `SetPair`. Rows 0~10 plus their rotations
cover all 231 cells exactly — no lower-half row number is ever written by hand.

**🔴 The one place D-1's interface did not fit — and how it was absorbed without editing D-1.**
`IMapArchetypeConstraints.IsNeutralMineForbidden(col,row)` is a per-tile predicate, so it cannot express
「대역 안 대응쌍은 **최대 1쌍**」(외곽형 ④) or 「레인당 광산 **최대 1개**」(3갈래형 ④). Solution: the
generator draws **one allowed pair representative** per attempt from the band's eligible pairs and marks
every *other* band tile forbidden. At most one pair can then land in the band, which is exactly the rule.
This costs one extra Terrain draw and, being per attempt, cannot leak into the next attempt.
**Do not "fix" this by adding a count field to the interface without re-reading D-1's rationale.**

**Known design consequence to raise with 기획 (not a bug):** the allowed band pair for 외곽형/3갈래형,
and 3갈래형's odd-count centre mine at (5,10), sit on `TileKind.NoBuild` tiles — a mine you cannot build
a MiningPost on. 협곡형's centre mine is likewise on a NoBuild tile. The rules as written require it.

**Canyon (규칙 6) — the 전환 행 reading that actually satisfies every stated invariant.**
Rule text says "3~8행 중 (11-W)/2 개의 전환 행". Read literally as 6 candidate *rows* it is impossible to
keep both 「3행 폭 11」 and 「8행 폭 W」 (a transition on row 8 cannot land at row 8). So the implementation
picks `k = (11-W)/2` of the **five gaps** 3->4 … 7->8 (`TransitionStepCount = 5`) and drops 2 across each
picked gap. Documented in the file header; `TryVerifyWidthProfile` counts transitions from the output.

**Outer (규칙 7) — how 울퉁불퉁형 satisfies all five constraints with no post-hoc repair.**
Only the half-profile (width by distance from the centre line) is drawn, starting at `maxWidth`, and each
next step is drawn uniformly from `{prev-2, prev, prev+2}` clipped to `[1, maxWidth]`. Centre = max (①),
odd start plus ±2 keeps every width odd (②), width never reaches 0 so consecutive steps always touch near
col 5 (③ — no cavity), the candidate set caps the adjacent difference at 2 (④), and mirroring one
half-profile gives rotational symmetry structurally (⑤). **The constraints are absorbed into the candidate
set, so no rejection/redraw loop exists here.**
마름모형 vs 타원형: with widths limited to 1/3/5 no curve distinguishes them, so the project's operational
definition is the **taper rate** — diamond widens 2 per step from the tip, ellipse 2 per **two** steps.
This is written down in the file header; it is a decision, not a derivation.
🔴 **Outer's parameters cannot be read back from the finished map** — at odd height steps width 1 and 3
look identical (only col 5 exists) and at even steps width 3 and 5 look identical (only cols 4,6). The
self-check therefore **replays the Terrain stream** with the same seed instead of reverse-engineering.
`IsNeutralMineCountAllowed` is overridden to {2,4,6}; odd counts are impossible because the mass always
blocks the rotation centre.

**Hard-coded self-check expectations** (independent Python port, mapVersion 1, attemptIndex 0, seeds 0~199):

| | |
|---|---|
| Canyon corridor width 3 / 5 / 7 counts | **73 / 57 / 70** |
| Canyon NoBuild total | **3522** (= 73x11 + 57x17 + 70x25, hand-derived from the band's open-cell counts) |
| Canyon Blocked total (unpaired 6 excluded) | **11566** |
| Outer & ThreeLane separation length 5/7/9/11 counts | **53 / 58 / 50 / 39** |
| ThreeLane Blocked / NoBuild totals | **3100** (= 2 x 1550, hand-derived) / **15150** (= 9x1550 + 6x200) |
| Outer max width 3 / 5 counts | **99 / 101** |
| Outer shape diamond / ellipse / rugged counts | **72 / 65 / 63** |
| Outer Blocked / NoBuild totals | **4832 / 5302** |

🔴 If one of these fails, the draw order or probabilities changed — decide whether `MapVersion` must be
bumped before touching the constant.

**Negative controls (one per generator, all sharing the same verifier the positive path uses):**
MapBandTable — rows 9~11 must **not** be rotation-closed; Canyon — a profile with an adjacent drop of 4,
and one that never reaches W; Outer — adjacent difference 4, centre not at max width, an even width;
ThreeLane — blocking one lane tile must fail `TryVerifyLaneStructure`.

**Verification without a compiler** (no `dotnet`/`mcs`/`csc`/`mono`): comment/string-stripped bracket
balance, `using`/namespace/banned-reference scan (0 hits for `UnityEngine` · `Hexiege.Core` · `UnityEditor`
· `System.Linq`), a scripted XML-doc check over every public/protected member, a cross-file member-existence
scan, and a full Python port that reproduces every hard-coded number above plus symmetry, connectivity and
corridor-continuity over 200 seeds x 3 types. **Nothing outside the self-checks calls these classes yet**
— the validator (E) and the coordinator are still unbuilt.

## 맵 검증기 E 단계 — 필수 통로 검사(`MapDefinitionValidator`) 구멍을 막았다 (2026-09-07)

`Domain/Map/MapDefinitionValidator.cs` 의 검사 **2**(필수 통로)에 조용한 구멍이 둘 있었다. 둘 다 수정
전후로 **실제 코드를 돌려 재현**했다(아래 mono 절 참조).

| 구멍 | 무엇이 잘못됐나 | 수정 |
|---|---|---|
| ① 빈 높이 단계를 건너뜀 | `heightSteps` 를 `tilesByHeightStep.Keys` 에서 가져왔기 때문에, 통로 타일이 **전부 막힌** 단계는 아예 방문되지 않았다 — 폭 0 으로 재는 대신 병목이 사라져 버렸다 | `MapCorridorRequirement` 가 이제 `MinHeightStep`/`MaxHeightStep` 를 갖는다. 반복은 **선언된 대역 전체**를 훑으며, 대역 밖의 통로 타일은 그 자체가 실패다 |
| ② 광산은 한 단계에서, 폭은 두 단계에서 셌다 | 폭 = `tilesAtStep + pairedTiles` 인데 광산은 `tilesAtStep` 에서만 셌다 → 한 행의 광산 2개가 "단계당 1개" 로 읽혀 통과했다 | 광산도 **같은 두 단계 창**에서 센다. 상수명은 `MaxMinePerCorridorHeightStep` → **`MaxMinePerCorridorWidthWindow`** 로 바꿨다 |
| ③ 선택 인자인 생성기 | `Validate(def, constraints, generator = null)` 이 조용히 규칙 3 의 공통 1~6 범위로 폴백했다 | 2인자 오버로드는 **없앴다.** `generator` 는 필수다(ArgumentNullException) |

- **짝 짓는 규칙은 그대로다**(`step+1` 우선, 없으면 `step-1`, 그것도 없으면 0) — 대역의 끝
  단계가 통과할 수 있는 것이 이 규칙 덕이다. 바뀐 것은 훑는 단계의 집합뿐이다.
- 대역 상수는 다시 만들어 쓰지 않는다: 협곡형/외곽형은 `MapBandTable.CentralBand{Min,Max}HeightStep`,
  3갈래형은 `MapBandTable.GetBand{Min,Max}HeightStep(separationLength)`. 생성자는 회전에 닫혀 있지 않은
  대역(`min + max != MapBandTable.HeightStepSum`, 42)을 거부한다 — 같은 부류의 구멍이기 때문이다.
- 새 음성 대조 **N13**(회전 짝인 두 행을 비움 → 폭 1) 과 **N14**(광산
  `(4,10) (5,10) (6,11)`, 회전에 닫혀 있고 한 폭 창 안에 2개). 둘 다 일부러 회전 대칭으로 만들었다:
  `ExpectFailure(..., 2)` 는 이들이 검사 1 에 걸리면 실패하므로, 대조가 자기 대칭성까지 검사한다.
- 통과율은 **움직이지 않았다**(측당 10만 시도, 시드 0~1999 x 허용 광산 수 x 측 A/B):
  FullyOpen/Canyon/Outer/ThreeLane 100.00%, ObstacleOpen 99.90%(실패 24건, 전부 검사 4 성↔성 도달).
  생성기가 이미 대역 안 광산을 금지하므로 실제로는 ②가 걸리는 일이 드물다.
  **[🔴 2026-09-07 정정 — 원문 유지: 위 줄의 「시드 0~1999 … ObstacleOpen 99.90%(실패 24건)」은
  스윕 범위와 백분율이 서로 어긋나 있었다.]** 실측값은 **시드 0~1999 = 23962/24000 = 99.84%,
  실패 38건**이다. 원문의 「실패 24건」은 **시드 0~999 구간의 값**(11976/12000 = **99.80%**)인데
  범위만 0~1999 로 적혀 어긋난 것으로 보인다(시드 1000~1999 는 11986/12000 = **99.88%**, 실패 14건).
  실패 시드는 **43 · 219 · 486 · 991 · 1221 · 1452 · 1683** 이고 **실패 사유가 전부 검사 4
  (성↔성 도달 불가)** 인 것은 원문이 맞다. **「통과율은 움직이지 않았다」(구멍 수정 전후 동일)는 결론
  자체도 유효하다** — 틀린 것은 스윕 범위와 그에 따른 백분율뿐이다. 근거: 메인 세션이 아래
  「Domain 레이어를 진짜로 컴파일하기」 절의 `mono` 절차로 세 구간(0~999 / 1000~1999 / 0~2000)을
  각각 돌려 실패 시드 목록까지 대조했다.
- **남긴 한계(보고만 하고 고치지 않음):** 폭 **5** 협곡 통로에서는 짝수 단계 하나를 통째로 비워도
  여전히 통과한다 — 짝인 홀수 단계 혼자서 열린 열 3개를 내놓아 규정 폭 3 을 충족하기 때문이며,
  그 3열은 서로 인접하지 않는다. 「비인접 3열」이 폭 3 으로 세어지는가는 규칙의 문제다.
  인접한 세 단계를 비우면 *잡힌다*(폭 0).

## Domain 레이어를 진짜로 컴파일하기 (2026-09-07) — 「컴파일러가 없다」는 종전 전제를 대체한다

`apt-get install -y mono-mcs` 가 에이전트 샌드박스에서 동작한다(`mcs` + `mono`). `Domain/Map/**` +
`Domain/Hex/*` + `Domain/Common/TeamId.cs` 는 **단독 컴파일**된다(Unity 불필요). 그래서 검증기·생성기의
자체 점검과 통과율 스윕을 추론만 하는 것이 아니라 *실행*할 수 있다. 절차:

- 소스를 임시 폴더로 복사한 뒤(프로젝트 트리를 그 자리에서 컴파일하지 말 것)
  `mcs -langversion:latest -r:System.Core.dll -out:run.exe Main.cs src/*.cs` 그리고 `mono run.exe`.
- `mcs` 는 C# 7.2 까지다: `out int _, out int _`(한 호출에 discard 두 개)는 복사본에서 이름을 붙여야 한다.
  이 레이어의 나머지는 손대지 않고 그대로 컴파일된다.
- 수정이 정말로 무언가를 고쳤음을 보이려면, 수정을 **되돌린** 사본을 하나 더 두고 둘 다 돌린다.

🔴 **이 방법으로 찾아낸 기존 컴파일 오류(범위 밖, 당시 미수정):**
`Domain/Map/InitialMapStateEvaluator.cs:858-859` 가 `IReadOnlyCollection<int>`(`GetBuildableTiles` 의
반환)에 `.Contains(occupied)` 를 부르는데 이 파일에는 **`using System.Linq` 가 없다**
— 그 인터페이스에는 `Contains` 가 없으므로 Unity(Roslyn)도 컴파일하지 못한다. 앞서의
"금지 참조 스캔(`System.Linq` 0건)" 은 정작 코드가 필요로 하는 바로 그 using 의 *부재*를 강제했던 것이다.

**[🔴 2026-09-07 correction — 위 문단은 어떻게 발견했는지의 기록으로 그대로 남긴다. 지금은
수정 완료다.]** `CheckCaseIsConsistent` 에서 두 `GetBuildableTiles(...)` 결과를 **반복문 밖에서 한 번**
지역 `HashSet<int>`(`blueBuildable` / `redBuildable`)로 복사하고, 반복문은 그것에 `Contains` 를
부른다. `System.Linq` 는 **도입하지 않았다** — Domain 의 `using System.Linq` 는 여전히 **0**건이다
(Domain 의 `.cs` 파일 49개 전부 실측). 술어는 바이트 단위로 같은 집합 소속 판정이라 검사가 약해지지
않았다. 프로브로 `OccupiedTiles.Count == 4`(반복문이 헛돌지 않음), 건설 가능 10/10, 그리고 음성 대조
(건설 가능한 것으로 알려진 타일을 넣어 봄)가 여전히 검출됨을 확인했다.
증거, 두 사본 모두 `mcs -langversion:latest -target:library` 로 빌드:
**수정 전** = `InitialMapStateEvaluator.cs(858,62): error CS1501`(오류 1건) · **수정 후** = 오류 0 경고 0,
그리고 `mono` 로 실행한 **11**개 `TryRunSelfCheck` 전부 PASS(MapRandomStreams · SymmetricMapBuilder ·
InitialMapStateEvaluator · MapBandTable · NeutralMineSampler · Open/ObstacleOpen/Canyon/Outer/ThreeLane
생성기 · MapDefinitionValidator).

⚠️ **`mcs` 는 `Domain/Map/**` + `Domain/Hex/*` + `Domain/Common/TeamId.cs` 에 대해서만 유효한 오라클이다.**
Domain **전체**를 `mcs` 로 컴파일하면 `Domain/Building/BuildingStats.cs` 에서 가짜 `CS1525` 오류가 13개 난다
— **switch 식(C# 8)** 때문이며, mcs 6.8(최대 C# 7.2)은 파싱하지 못하지만 Unity 의 Roslyn(C# 9)은 받아들인다.
이것을 진짜 오류로 보고하지 말 것. Domain 의 나머지에는 **대신 grep 오라클**을 쓴다: Domain 에는
`using System.Linq` 가 하나도 없으므로 *어떤* Linq 전용 확장 호출이든 그것이 곧 오류다. `Any|All|Where|Select|First|Last|
Single|OrderBy|Sum|Average|Aggregate|Distinct|Concat|Except|Intersect|Union|Skip|Take|Cast|OfType|
ToDictionary|ToLookup|Reverse|Zip|SequenceEqual|ElementAt` → **진짜 히트 0건**(`HexPathfinder.cs:125` 의
`path.Reverse()` 하나는 `List<T>` 의 인스턴스 메서드다), 그리고 약 40개의 `.Contains(` 수신자는 전부
`HashSet<T>`/`List<T>`/`Dictionary<T>` 의 인스턴스 메서드다. **이 부류의 다른 오류는 Domain 에 남아 있지 않다.**

## 무작위 맵 F 단계 — 폴백 템플릿 5개 + 제작 도구 (2026-09-07)

**손으로 그리지 않는다. 생성기에서 뽑는다.** Plan 의 원래 방식(윗절반 수기 지정 + 회전 복제)은
**사용자 승인으로 폐기**됐다. 손으로 그린 맵이 규칙 13 의 검사 전부를 동시에 만족한다는 보장이
없기 때문이다. 실제 방식:

```
유형마다  광산 수 = 그 유형의 MaxNeutralMineCount,  시작 광산 = CaseA 고정
          시드 0 부터 1씩 올리며 생성 → MapDefinitionValidator.Validate 를 완화 없이 통과한 첫 시드 채택
```

채택된 것은 **정의상** 검증 통과본이고, 재생성 정보가 「유형 + 시드 + 광산 수 + A/B」 네 값으로 줄어든다.

| 파일 | 역할 |
|---|---|
| `Domain/Map/MapFallbackTemplateFactory.cs` (1124행) | 순수 로직. `MapFallbackTemplate` 값 객체 + 팩터리 |
| `Assets/Editor/Tools/MapFallbackTemplateBuilder.cs` (342행) | 위를 부르기만 하는 얇은 껍데기. 메뉴 `Hexiege/무작위 맵/1·2` |
| `Assets/_Project/Resources/MapTemplates/*.bytes` | 런타임 로드용 canonical binary 5개 |
| `Assets/_Project/Docs/_Reference/MapTemplateSource/*.md` | 재생성 정보 5개(각 55행) |

🔴 **로직을 Domain 에 두는 이유가 검증 가능성이다.** 이 저장소에는 Unity 가 없어 에디터 도구를
실행할 수 없다. 로직이 순수 C# 이면 `mono` 로 그대로 돌려 **결과물을 실제로 만들고 검증까지** 할 수
있고, 에디터 도구는 같은 로직을 부르므로 두 경로가 어긋날 수 없다. 문서 본문을 만드는 문자열 조립
(`BuildSourceDocument`)까지 Domain 에 둔 것도 같은 이유다 — 에디터로 만든 문서와 mono 로 만든 문서가
글자 하나까지 같아야 하기 때문.

**실측값(mapVersion 1, attemptIndex 0, CaseA, 정상 모드):**

| 유형 | 채택 시드 | 광산 | 초기 골드 | 바이트 | SHA-256 |
|---|---|---|---|---|---|
| FullyOpen | 0 | 6 | 200 | 343 | `4fab6efbb6cb27ec30510a5e8c18dca07729a897e5d9c0f46ba86b62595fbbd9` |
| ObstacleOpen | 0 | 6 | 200 | 343 | `52ed46de30436f20c7a37260ea7bcc4b0104063f0e2a470ae96ef90b658691ee` |
| Canyon | 0 | 4 | 400 | 335 | `314db3fa4a022bb804a12a9b3050c5b38e424d13dc83333afbdf2865e7901082` |
| Outer | 0 | 6 | 200 | 343 | `67f2857cb076023332172257a94b54a3a9765eeb0f7d621138b63a91716df14f` |
| ThreeLane | 0 | 6 | 200 | 343 | `4b84e2b76fb0fc645b9d81c092f854496dc47d1413b855afc6ed839e897ae647` |

**다섯 유형 전부 시드 0 에서 바로 통과했다**(`seedsTried = 1`). 광산 수 4/6 차이 때문에 Canyon 만
8바이트 짧다(중립 광산 int 2개). 이 해시가 바뀌면 생성기의 뽑기 순서·확률이 바뀐 것이므로
`MapVersion` 을 올려야 하는지부터 판단할 것.

🔴 **`MapDefinitionCodec.Encode` 는 `TestModeFlag` 와 `InitialGold` 를 해시 입력에 포함한다**
(canonical 앞머리 40바이트 = int 8개 + ulong 1개). 규칙 12 대로 조정자(G)가 테스트 모드 초기 골드
5000 을 덮어쓰면 **해시를 반드시 다시 계산해야 한다.** 템플릿은 정상 모드 값만 담는다 —
테스트 모드 덮어쓰기는 템플릿의 몫이 아니다.

**`.bytes` 확장자는 선택이 아니다.** Unity 는 `.bytes` 여야 바이너리를 `TextAsset` 으로 임포트한다.
다른 확장자면 `Resources.Load<TextAsset>` 이 런타임에 읽지 못한다. 이름 규칙의 단일 소스는
`GetTemplateAssetName/FileName/ResourcePath` 이며 **G 단계 로더도 이것을 써야** 만드는 쪽과 읽는 쪽이
갈리지 않는다.

**`MapDefinitionCodec.Decode` 는 `Hash` 를 복원하지 않는다**(바이트열에 애초에 없다). 그래서 왕복 검사는
「해시가 복원됐는가」가 아니라 **「다시 계산하면 같은가」**를 봐야 한다.
`MapArchetypeGeneratorBase.TryCompareDefinitions` 는 **지형과 중립 광산만** 본다 — 성·시작 광산·장식·
스칼라 필드까지 보는 왕복 비교는 `MapFallbackTemplateFactory.TryCompareDefinitionsExactly` 다.

**자기 검증은 이제 12종이다**(F 로 1개 추가). `mono` 전량 실행 결과 **12/12 PASS**(합계 약 200ms).
F 의 자기 검증 9단계에는 음성 대조가 들어 있다 — 타일 배열 첫 바이트를 뒤집은 사본이 왕복 비교에서
**반드시 검출돼야** 통과한다(검출하지 못하면 왕복 검사 통과가 근거가 되지 못하므로).

⚠️ **Unity 에디터에서는 아직 한 번도 실행되지 않았다.** 에디터 도구는 `mcs` + Unity 스텁
(`MenuItem`/`EditorUtility`/`AssetDatabase`/`Debug`/`Application.dataPath` + `Hexiege.Application`
네임스페이스 재현)으로 **오류 0 · 경고 0** 컴파일까지만 확인했다.

---

## 무작위 맵 H 단계 — 격자 11×21 + `MapDefinition` → `HexGrid` 투영 (2026-09-07)

**여기서 처음으로 실제 화면이 바뀐다.** A~G 는 아무도 부르지 않는 코드였고, H 가 기존 고정 맵 경로를
무작위 맵으로 갈아끼운다. Plan §1 제거표 제거-1~3 과 §4 H 절이 근거.

| 파일 | 무엇이 바뀌었나 |
|---|---|
| `Resources/Config/GameConfig.asset` | FlatTop `GridWidth 10→11` · `GridHeight 20→21`. **PointyTop(7×17)은 무변경** |
| `Infrastructure/Config/GameConfig.cs` | FlatTop 코드 기본값 `10×29 → 11×21`. `.asset` 이 실행 권위지만 코드 기본값이 다르면 「세 번째 값」이 남는다 |
| `Application/UseCases/MapProjectionUseCase.cs` (신설, guid `12b78cbe79624d4281c93f738a46feb9`) | 순수 C#. `Project(MapDefinition, HexGrid)` |
| `Bootstrap/GameBootstrapper.Map.cs` | `LoadMap` 3-A 단계 신설 + 하드코딩 배치 주석 비활성화 |
| `Bootstrap/GameBootstrapper.cs` | `_mapPreparation` / `_mapProjection` 필드 + 게터 2개 + `NetworkInterimRootSeed` 상수 |

### `MapProjectionUseCase` — 순수 C# 이라 mono 로 검증된다

`static class`(보관 상태 0). `Project` 는 **모든 실패 판정을 대입 전에 끝낸다** — 중간에 실패하면
격자가 「절반만 새 맵」인 상태로 남아 원인을 못 찾는다. 결과 객체 `MapProjectionResult` 는
`Castles` / `StartingMines`(`MapTeamPlacement` = HexCoord + TeamId) / `NeutralMines`(HexCoord) 를 돌려주고,
**실제 건물 배치는 하지 않는다**(종족·팩터리가 필요해 Bootstrap 의 몫).

- `TileKind` ← `definition.Tiles[index]` 전 칸. `MineKind` 는 **매번 `None` 으로 되돌린 뒤** 목록으로 다시
  칠한다 → 같은 격자에 다른 맵을 재투영해도 이전 광산이 남지 않는다(재경기 경로에서 실제로 일어난다).
- `HasBuilding` · 소유권은 **건드리지 않는다.** 초기 소유권은 성·시작 채굴소를 세울 때
  `BuildingPlacementUseCase` 가 자기 타일 + 인접 타일을 칠하며 자연히 생긴다 —
  `InitialMapStateEvaluator`(C) 와 **입력이 같아 결과도 같다.** 그래서 소유권을 두 번 계산하지 않는다.
- 🔴 **방향 불일치 검출**: 인덱스→좌표 변환에 `definition.Orientation` 을 쓰고 `grid.HasTile` 로 확인하므로,
  PointyTop 격자에 투영하면 반드시 실패한다(자체 점검의 음성 대조 중 하나).
- 자체 점검 7종(전 칸 대조 · 광산 종류 · 성 좌표 · 소유권/HasBuilding 무변경 · 재투영 · 음성 대조 4개).

### 🔴 1단계에서 못 지웠던 「숨은 참조」를 이번에 끊었다

`PlaceGoldMines` 의 **시작 채굴소 자동 건설이 `startingMines[][]` 배열 좌표를 직접 읽고 있었다.**
그래서 1단계에서는 배열을 지우지 못했다. H 에서는 채굴소 좌표를 **`MapDefinition.StartingMines` →
투영 결과 `StartingMines`** 에서 받는다. 이제 `GameBootstrapper.Map.cs` 의
`startingMines` / `neutralMines` / `SetGoldMine` 히트는 **전부 `//` 주석 줄**이다.

`// [2단계 대체 대기]` 표식 = 코드 **2건**(`PlaceCastles` · `PlaceGoldMines`) + `Plan.md` 2건.
최종 삭제 후 코드 0건이 완료의 기계적 증거다.

⚠️ **주석 비활성화의 부작용**: 주석 안에 `tile.MineKind = mineKind;` 같은 **대입 모양 문자열**이 남아 있다.
「MineKind 를 쓰는 자리」를 grep 으로 세면 오탐이 된다(`.claude/mistakes.md` 2026-09-02 항목과 같은 부류).
Plan 이 주석 비활성화를 지시했으므로 그대로 두되, 그 grep 을 쓸 때는 주석을 걷어내고 셀 것.

### root seed 를 어디서 만들고 어디에 두는가

`GameBootstrapper.Map.cs` `CreateRootSeed()`. 싱글은 `Guid.NewGuid()` 앞 8바이트 ^ `DateTime.UtcNow.Ticks`
(**`UnityEngine.Random` · `GetHashCode` 미사용** — 맵 생성 계통의 금지 API 와 혼동될 여지를 남기지 않는다).
결과는 `_mapPreparation`(= `MapPreparationResult`, `RootSeed` 포함)에 보관하고 `GetLastMapPreparation()`
으로 꺼낸다. **K 단계는 이 게터에서 규칙 12 의 11개 항목을 전부 얻을 수 있다.**

🔴 **멀티는 고정 seed `NetworkInterimRootSeed = 1`.** Host 권위 seed 전송이 3단계라, 지금 양쪽이
각자 뽑으면 서로 다른 맵을 본다. 「같은 seed → 같은 맵」(규칙 12)에 기대어 전송 없이 일치시킨
**임시 조치**이며 멀티는 당분간 매 판 같은 맵이다. **사용자 확인이 필요한 판단**으로 보고했다.

### 실행 모델 — `LoadMap` 은 동기 그대로

메인 세션 실측(`Prepare` 300회, 데스크톱 mono): 중앙값 0ms · 95번째 0ms · 최대 7ms. 첫 시도 통과율이
사실상 100% 라 실제로는 생성·검증 1회로 끝난다. 코루틴화는 호출부 전체를 흔들고 H 는 이미 가장
위험한 단계라 **개조를 겹치지 않는다.** 「부르기 전 한 프레임 넘기는 것이 이상적」은 `PrepareAndProjectMap`
XML 주석에 남겨 뒀다.

### 격자 크기 변경을 따라 움직이는 것 (전부 매개변수화돼 있어 코드 수정 0건)

`HexMetrics.GridCenter` · `ComputeMapWorldBounds` · `IsWithinMapBounds` · `ClampToMapBounds` 는 전부
`gridWidth/gridHeight` 인자를 받고, 호출부는 `oc.GridWidth/GridHeight`(Setup.cs 499·516·518,
Map.cs 96, Network.cs 80) 또는 `_grid.Width/Height`(Setup.cs 395·569·571 스킬 조준 람다)를 넘긴다.
**`GridWidth`/`GridHeight` 를 참조하는 자리는 `GameConfig.cs` 정의 4곳 외에 전부 `oc.` 경유**(grep 실측).

FlatTop TileWidth 1.0 · TileHeight 0.866 기준 실제 변화값:

| | 10×20 | 11×21 |
|---|---|---|
| `GridCenter` (x, z) | (3.375, −8.4435) | (3.75, −8.66) |
| 맵 월드 경계 (minX, minZ, maxX, maxZ) | (−0.5, −17.32, 7.25, 0.433) | (−0.5, −18.186, 8.0, 0.433) |
| 카메라 clamp size (x, z) | (10.75, 20.887) | (11.5, 21.32) |

### 실측 — `mcs` + `mono` 로 실제 실행

- 자체 점검 **14종 전원 PASS**(기존 13 + `MapProjectionUseCase` 신설 1).
- **end-to-end 프로브**: 시드 0~199 를 `Prepare` → `Project` → 231칸 전수 대조. **실패 0건.**
  `TileKind` 전 칸 일치 · `MineKind` 전 칸 일치 · `HasBuilding` 전부 false · 소유권 전부 Neutral ·
  성 2 / 시작 광산 2 / 중립 광산 = `NeutralMineCount` · 성 타일은 항상 `Normal` + 광산 없음 + 이동 가능.
  최대 소요 24ms(첫 JIT 포함). 같은 seed 두 번 → 같은 해시.
- **유형별 타일 구성 실측**(시드 0~499, 정상 모드, 평균 칸수):

  | 유형 | Normal | NoBuild | Blocked |
  |---|---|---|---|
  | FullyOpen | 225.0 | 0 | 6.0 |
  | ObstacleOpen | 194.6 | 0 | 36.4 |
  | Canyon | 148.8 | 17.2 | 65.0 |
  | Outer | 172.9 | 26.8 | 31.3 |
  | ThreeLane | 129.9 | 78.9 | 22.2 |

  🔴 **`Blocked` 는 항상 최소 6칸이다** — 짝수 열 0행 6칸은 회전 상대가 격자 밖이라 `SymmetricMapBuilder`
  가 영구 `Blocked` 로 고정한다(규칙 10). 231칸 중 실제 사용 225칸.

🔴 **`UnityEngine` 을 쓰는 파일은 컴파일 검증 불가**: `GameBootstrapper.*` · `GameConfig.cs` 는 mono 로
빌드할 수 없다. 중괄호/괄호 균형(주석·문자열 스트립 후 0) · 금지 grep(`Application.` 0건) · 타입/using
소재 확인까지만 했고, **컴파일 확인은 Unity 에디터에서만 가능하다.**

### H 시점의 의도된 미완 (I·J·K 범위 — 화면에서 이렇게 보인다)

- `Blocked` 타일이 **여전히 일반 타일처럼 그려진다**(J 단계). 눈에는 평범한데 유닛이 못 지나간다
  = 「보이지 않는 벽」. `Blocked` 는 `IsWalkable` 이 false 라 건설도 이미 막혀 있다.
- `NoBuild` 타일에 **빗금이 없고 건설도 막히지 않는다**(J·I 단계). 지금 판정은 `IsWalkable` 뿐이라
  `NoBuild` 는 일반 타일과 구분되지 않는다.
- `MapPreparationResult.InitialGold`(광산 수 표 / 테스트 모드 5000)를 **아직 아무도 쓰지 않는다.**
  초기 골드는 여전히 `GameConfig.StartingGold`. H 의 파일 4개 목록에 없어 범위 밖으로 두고 보고만 했다.
