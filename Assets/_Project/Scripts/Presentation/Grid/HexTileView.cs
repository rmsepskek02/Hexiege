// ============================================================================
// HexTileView.cs
// 타일 하나의 비주얼(색상, 선택 하이라이트)을 담당하는 Presentation 컴포넌트.
//
// 이 스크립트가 부착되는 프리팹 구조 (Phase 10에서 생성):
//   HexTile (GameObject)
//     ├─ SpriteRenderer  (tile_hex.png 스프라이트)
//     ├─ PolygonCollider2D (클릭 판정용)
//     └─ HexTileView (이 스크립트)
//
// 역할:
//   1. 타일 팀 색상 표시 — SpriteRenderer.color를 팀별 색상으로 설정
//   2. 선택 하이라이트 — 선택된 타일에 노란 틴트 적용
//   3. 이벤트 구독 — GameEvents를 구독하여 자동 갱신
//
// 색상 변경 흐름:
//   유닛이 이동 → UnitMovementUseCase가 SetOwner + OnTileOwnerChanged 발행
//   → 이 컴포넌트가 이벤트 수신 → 자신의 좌표와 일치하면 색상 변경
//
// 선택 하이라이트 흐름:
//   InputHandler가 클릭 → GridInteractionUseCase → OnTileSelected 발행
//   → 이 컴포넌트가 이벤트 수신 → 이전 선택 해제 + 새 선택 하이라이트
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, SpriteRenderer).
// ============================================================================

using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class HexTileView : MonoBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 이 타일의 헥스 좌표. Initialize()에서 설정. </summary>
        private HexCoord _coord;

        /// <summary> 이 타일의 현재 소유 팀. 색상 결정에 사용. </summary>
        private TeamId _currentOwner = TeamId.Neutral;

        /// <summary> 이 타일이 현재 선택 상태인지 여부. </summary>
        private bool _isSelected;

        /// <summary> 이 타일의 SpriteRenderer. 색상 변경에 사용. </summary>
        private SpriteRenderer _spriteRenderer;

        /// <summary> 전역 설정 참조. 팀 색상, 선택 틴트 값을 읽어옴. </summary>
        private GameConfig _config;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// HexGridRenderer에서 타일 프리팹을 Instantiate한 뒤 호출.
        /// 좌표와 설정 참조를 전달받아 초기 상태 설정.
        /// </summary>
        /// <param name="coord">이 타일의 헥스 좌표</param>
        /// <param name="config">전역 설정 (팀 색상 등)</param>
        public void Initialize(HexCoord coord, GameConfig config)
        {
            _coord = coord;
            _config = config;
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // 초기 색상 설정 (Neutral = 회색)
            UpdateColor();

            // 이벤트 구독
            SubscribeEvents();
        }

        /// <summary> 이 타일의 좌표를 외부에서 조회할 때 사용. </summary>
        public HexCoord Coord => _coord;

        // ====================================================================
        // 이벤트 구독
        // ====================================================================

        /// <summary>
        /// 게임 이벤트를 구독.
        /// .AddTo(this): 이 MonoBehaviour가 Destroy되면 자동으로 구독 해제.
        /// 메모리 누수 방지.
        /// </summary>
        private void SubscribeEvents()
        {
            // 타일 소유자 변경 이벤트 구독
            // → 자신의 좌표와 일치할 때만 색상 갱신
            GameEvents.OnTileOwnerChanged
                .Subscribe(e =>
                {
                    if (e.Coord == _coord)
                    {
                        _currentOwner = e.NewOwner;
                        UpdateColor();
                    }
                })
                .AddTo(this);

            // 타일 선택 이벤트 구독
            // → 이전 선택 타일 해제 + 새 선택 타일 하이라이트
            GameEvents.OnTileSelected
                .Subscribe(e =>
                {
                    // 이전 선택 해제 (내가 이전 선택이었으면)
                    if (e.PreviousCoord.HasValue && e.PreviousCoord.Value == _coord)
                    {
                        _isSelected = false;
                        UpdateColor();
                    }

                    // 새 선택 하이라이트 (내가 새 선택이면)
                    if (e.Coord == _coord)
                    {
                        _isSelected = !_isSelected; // 토글 지원
                        UpdateColor();
                    }
                })
                .AddTo(this);
        }

        // ====================================================================
        // 색상 갱신
        // ====================================================================

        /// <summary>
        /// 현재 소유 팀과 선택 상태에 따라 SpriteRenderer.color를 갱신.
        ///
        /// 색상 = 팀 색상 × (선택 시 노란 틴트)
        ///
        /// 예:
        ///   Blue 팀 + 선택 안 됨 → RGB(77,128,230)
        ///   Blue 팀 + 선택 됨   → RGB(77,128,230) × RGB(255,255,128) = 밝은 노란 파랑
        /// </summary>
        private void UpdateColor()
        {
            if (_config == null || _spriteRenderer == null) return;

            Color baseColor = _config.GetTeamColor(_currentOwner);

            if (_isSelected)
            {
                // 기존 색상에 노란 틴트를 곱하여 하이라이트 효과
                baseColor *= _config.SelectedTint;
            }

            _spriteRenderer.color = baseColor;
        }
    }
}
