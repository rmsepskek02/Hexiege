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
// Application 레이어 — Domain과 Core에 의존, Unity에 직접 의존하지 않음.
// ============================================================================

using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Application
{
    public class GridInteractionUseCase
    {
        // 타일 데이터 조회용 그리드 참조
        private readonly HexGrid _grid;

        // 현재 선택된 타일 좌표. null이면 아무것도 선택 안 된 상태.
        private HexCoord? _selectedCoord;

        /// <summary> 현재 선택된 타일 좌표. 외부에서 읽기 전용. </summary>
        public HexCoord? SelectedCoord => _selectedCoord;

        public GridInteractionUseCase(HexGrid grid)
        {
            _grid = grid;
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
            HexCoord coord = HexMetrics.WorldToHex(worldPos);

            // 그리드 밖 클릭 → 선택 해제
            if (!_grid.HasTile(coord))
            {
                Deselect();
                return;
            }

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
