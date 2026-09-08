// ============================================================================
// GridInteractionUseCase.cs
// 타일 선택/해제 처리를 담당하는 UseCase.
//
// UseCase란?
//   "사용자가 ~을 하면, 시스템이 ~을 한다"를 정의하는 Application 레이어 클래스.
//   Presentation(입력) → UseCase(처리) → Event(결과 알림) → View(화면 반영)
//
// 이 UseCase의 흐름:
//   1. InputHandler가 클릭된 월드 좌표를 전달
//   2. 월드 좌표 → HexCoord 변환 (HexMetrics.WorldToHex)
//   3. 해당 좌표에 타일이 존재하는지 확인 (HexGrid.HasTile)
//   4. 선택 상태 갱신 (이전 선택 해제, 새 선택 설정)
//   5. GameEvents.OnTileSelected 이벤트 발행
//   6. HexTileView가 이벤트를 받아 하이라이트 표시
//
// 상태:
//   _selectedCoord: 현재 선택된 타일 좌표 (없으면 null)
//   같은 타일을 다시 클릭하면 선택 해제 (토글)
//
// ─────────────────────────────────────────────────────────────────────────────
// 무작위 맵 2단계 J — 클릭 판정 순서
//   (단일 소스: GameSystemRules_UI.md 「무작위 맵 타일 선택과 건설 패널」 규칙 5~8)
//
//   1. 기존 building action 분기        ← InputHandler.HandleClick 이 담당
//   2. MineKind 기반 MiningPost 자격 분기 ← InputHandler.HandleClick 이 담당
//   3. TileKind.Blocked  → 선택 불가. 선택 해제 + 배치 패널 닫기. 토스트·선택 이벤트 없음
//   4. TileKind.NoBuild  → 자기 팀 : 선택 + 하이라이트, 건설 패널은 열지 않음,
//                                     ToastKey.BuildingNotAllowed 표시
//                          중립·적 : 선택 + 하이라이트만, 토스트 없음
//   5. TileKind.Normal   → 지금까지와 동일 (자기 팀 빈 타일이면 건설 패널)
//
//   🔴 1·2번이 3·4번보다 먼저인 것이 핵심이다(규칙 5의 마지막 문장).
//      광산 타일 위 채굴소(MiningPost)는 NoBuild 타일에도 지을 수 있는 예외인데,
//      NoBuild 처리가 먼저 오면 그 예외가 통째로 막힌다.
//
//   이 클래스는 3~5번의 "판정"만 담당하고, 그 결과를 TileClickOutcome으로 돌려준다.
//   패널을 닫거나 토스트를 띄우는 것은 화면(Presentation)의 일이라 InputHandler가 한다.
//   (Application 레이어가 UI를 직접 호출하지 않도록 하는 레이어 규약)
//
// Application 레이어 — Domain만 의존. 좌표 변환은 IHexCoordinateMapper 인터페이스로
// 추상화하여 Core 레이어(HexMetrics)에 직접 의존하지 않는다.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 타일 클릭 판정 결과. 호출자(InputHandler)가 이 값을 보고 화면 반응을 결정한다.
    /// </summary>
    public enum TileClickOutcome
    {
        /// <summary>
        /// 선택할 수 없는 자리 — 막힌 타일(Blocked)이거나 격자 밖(빈 공간).
        /// 호출자는 열린 건물 배치 패널을 닫고, 토스트는 띄우지 않는다(규칙 6).
        /// </summary>
        NotSelectable,

        /// <summary>
        /// 자기 팀 소유의 건설 불가 타일(NoBuild). 선택·하이라이트는 이미 처리됐다.
        /// 호출자는 건물 배치 패널을 닫고 ToastKey.BuildingNotAllowed를 띄운다(규칙 7).
        /// </summary>
        NoBuildOwnTeam,

        /// <summary>
        /// 중립 또는 적 소유의 건설 불가 타일(NoBuild). 선택·하이라이트는 이미 처리됐다.
        /// 호출자는 건물 배치 패널만 닫고 토스트는 띄우지 않는다(규칙 8).
        /// </summary>
        NoBuildOther,

        /// <summary>
        /// 일반 타일(Normal). 선택·하이라이트는 이미 처리됐다.
        /// 호출자는 지금까지와 똑같이 건설 가능 여부를 보고 배치 패널을 연다(규칙 5의 5번).
        /// </summary>
        Normal
    }

    public class GridInteractionUseCase
    {
        // 타일 데이터 조회용 그리드 참조
        private readonly HexGrid _grid;

        // 월드↔헥스 좌표 변환기. Core(HexMetrics) 의존을 피하기 위한 인터페이스 주입.
        private readonly IHexCoordinateMapper _mapper;

        // 현재 선택된 타일 좌표. null이면 아무것도 선택 안 된 상태.
        private HexCoord? _selectedCoord;

        /// <summary> 현재 선택된 타일 좌표. 외부에서 읽기 전용. </summary>
        public HexCoord? SelectedCoord => _selectedCoord;

        public GridInteractionUseCase(HexGrid grid, IHexCoordinateMapper mapper)
        {
            _grid = grid;
            _mapper = mapper;
        }

        /// <summary>
        /// 월드 좌표를 받아 타일 선택/해제 처리.
        /// InputHandler에서 호출됨.
        ///
        /// 동작:
        ///   - 그리드 밖 클릭: 기존 선택 해제
        ///   - 같은 타일 재클릭: 선택 해제 (토글)
        ///   - 다른 타일 클릭: 이전 해제 + 새 타일 선택
        /// </summary>
        /// <param name="worldPos">클릭된 Unity 월드 좌표</param>
        public void SelectTileAt(UnityEngine.Vector3 worldPos)
        {
            HexCoord coord = _mapper.WorldToHex(worldPos);

            // 그리드 밖 클릭 → 선택 해제
            if (!_grid.HasTile(coord))
            {
                Deselect();
                return;
            }

            ApplySelection(coord);
        }

        /// <summary>
        /// TileKind(지형 종류)까지 따져서 타일을 선택하고, 그 결과를 돌려준다.
        /// 무작위 맵 2단계 J의 클릭 판정 3~5번에 해당한다
        /// (1·2번 = 건물 분기 / 광산 분기는 호출자인 InputHandler가 이미 처리하고 돌아온다).
        ///
        /// 판정 순서(위에서부터, GameSystemRules_UI.md 규칙 5~8):
        ///   3. 격자 밖이거나 TileKind.Blocked → 선택 해제하고 NotSelectable
        ///   4. TileKind.NoBuild              → 선택은 하고, 소유자에 따라 결과를 구분
        ///   5. TileKind.Normal               → 선택하고 Normal
        ///
        /// 선택 자체(토글·이벤트 발행)는 SelectTileAt과 완전히 같은 규칙을 쓴다
        /// (같은 타일을 다시 누르면 선택이 풀리는 동작 포함).
        /// </summary>
        /// <param name="worldPos">클릭된 Unity 월드 좌표</param>
        /// <param name="localTeam">지금 화면을 보고 있는 플레이어의 팀. 규칙 7/8 구분에 쓴다</param>
        /// <returns>호출자가 화면 반응(패널 닫기·토스트)을 결정하는 데 쓰는 판정 결과</returns>
        public TileClickOutcome SelectTileByKind(UnityEngine.Vector3 worldPos, TeamId localTeam)
        {
            HexCoord coord = _mapper.WorldToHex(worldPos);

            // --- 3. 격자 밖 클릭 -------------------------------------------
            //   맵 바깥을 눌렀을 때. 논리 격자에 좌표 자체가 없는 경우다.
            //
            //   ⚠️ "화면에 안 그려진 Blocked 자리"는 여기로 오지 않는다.
            //      InputHandler는 collider가 아니라 XZ 평면 수학 레이캐스트로 좌표를 구하므로,
            //      타일 오브젝트가 없어도 그 자리의 헥스 좌표는 정상적으로 계산된다.
            //      → Blocked 자리 클릭은 바로 아래 Blocked 분기가 받는다.
            if (!_grid.HasTile(coord))
            {
                Deselect();
                return TileClickOutcome.NotSelectable;
            }

            HexTile tile = _grid.GetTile(coord);
            if (tile == null)
            {
                Deselect();
                return TileClickOutcome.NotSelectable;
            }

            // --- 3. 막힌 타일(Blocked) -------------------------------------
            //   화면에는 타일이 없지만 논리 격자에는 좌표가 남아 있다. 실제로 "빈 공간을
            //   눌렀다"는 사건의 대부분이 여기로 들어온다(위 격자 밖 주석 참조).
            //   선택 이벤트를 "발행하지 않는" 것이 핵심이다.
            //   Deselect()는 이전에 선택돼 있던 타일이 있을 때만 해제 이벤트를 낸다.
            if (tile.TileKind == TileKind.Blocked)
            {
                Deselect();
                return TileClickOutcome.NotSelectable;
            }

            // 여기서부터는 선택이 성립한다(NoBuild·Normal 공통).
            ApplySelection(coord);

            // --- 4. 건설 불가 타일(NoBuild) --------------------------------
            if (tile.TileKind == TileKind.NoBuild)
            {
                return tile.Owner == localTeam
                    ? TileClickOutcome.NoBuildOwnTeam   // 규칙 7 — 토스트까지
                    : TileClickOutcome.NoBuildOther;    // 규칙 8 — 선택만
            }

            // --- 5. 일반 타일(Normal) --------------------------------------
            return TileClickOutcome.Normal;
        }

        /// <summary>
        /// 선택 상태를 갱신하고 선택 이벤트를 발행하는 공통 처리.
        /// 같은 타일을 다시 클릭하면 선택이 풀리는 토글 동작이 여기에 들어 있다.
        /// (SelectTileAt / SelectTileByKind가 같은 규칙을 쓰도록 한 곳으로 모았다)
        /// </summary>
        /// <param name="coord">선택 대상 좌표. 격자 안에 있는 좌표여야 한다</param>
        private void ApplySelection(HexCoord coord)
        {
            HexCoord? previous = _selectedCoord;

            // 같은 타일 재클릭 → 토글 (선택 해제)
            if (_selectedCoord.HasValue && _selectedCoord.Value == coord)
            {
                _selectedCoord = null;
            }
            else
            {
                // 새 타일 선택
                _selectedCoord = coord;
            }

            // 이벤트 발행 → HexTileView가 하이라이트 갱신
            GameEvents.OnTileSelected.OnNext(new TileSelectedEvent(coord, previous));
        }

        /// <summary>
        /// 선택 해제. 아무 타일도 선택되지 않은 상태로 초기화.
        /// </summary>
        public void Deselect()
        {
            if (!_selectedCoord.HasValue) return;

            HexCoord previous = _selectedCoord.Value;
            _selectedCoord = null;

            // 해제 이벤트 발행 (Coord와 PreviousCoord가 같으면 View에서 해제로 처리)
            GameEvents.OnTileSelected.OnNext(new TileSelectedEvent(previous, previous));
        }
    }
}
