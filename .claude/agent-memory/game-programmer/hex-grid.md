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
