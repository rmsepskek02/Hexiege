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
