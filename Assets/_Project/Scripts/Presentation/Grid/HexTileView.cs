// ============================================================================
// HexTileView.cs
// 타일 하나의 비주얼(색상, 선택 하이라이트)을 담당하는 Presentation 컴포넌트.
//
// [Phase 2] 3D 전환 완료:
//   - SpriteRenderer → Renderer (MeshRenderer/SpriteRenderer 모두 호환)
//   - PolygonCollider2D → Collider (BoxCollider/MeshCollider 등 3D 콜라이더)
//   - sortingOrder 관련 코드 제거
//   - 색상 변경: renderer.material.color 사용
//
// 프리팹 구조 (3D 전환 후):
//   HexTile (GameObject)
//     ├─ MeshFilter + MeshRenderer (3D 타일 메시)
//     ├─ BoxCollider (3D 클릭 판정)
//     └─ HexTileView (이 스크립트)
//
// 역할:
//   1. 타일 팀 색상 표시 — Renderer.material.color를 팀별 색상으로 설정
//   2. 선택 하이라이트 — 선택된 타일에 노란 틴트 적용
//   3. 이벤트 구독 — GameEvents를 구독하여 자동 갱신
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Renderer).
// ============================================================================

using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
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

        /// <summary>
        /// 이 타일의 Renderer. 색상 변경에 사용.
        /// [Phase 2] Renderer 타입으로 선언하여 MeshRenderer/SpriteRenderer 모두 호환.
        /// </summary>
        private Renderer _renderer;

        /// <summary> 머티리얼 인스턴스 캐시. material 접근 시 복제되므로 캐시하여 GC 방지. </summary>
        private Material _materialInstance;

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

            // [Phase 2] Renderer 타입으로 가져와서 MeshRenderer/SpriteRenderer 모두 대응
            _renderer = GetComponent<Renderer>();

            // 머티리얼 인스턴스 캐시 — SG_HexTile 셰이더(윗면)를 우선 탐색
            if (_renderer != null)
            {
                foreach (var mat in _renderer.materials)
                {
                    if (mat.shader.name.Contains("SG_HexTile"))
                    {
                        _materialInstance = mat;
                        break;
                    }
                }
                // SG_HexTile 셰이더를 찾지 못한 경우 fallback (index 0)
                if (_materialInstance == null)
                    _materialInstance = _renderer.material;
            }

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
        /// </summary>
        private void SubscribeEvents()
        {
            // 타일 소유자 변경 이벤트 구독
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
                        _isSelected = !(e.PreviousCoord.HasValue && e.PreviousCoord.Value == e.Coord);
                        UpdateColor();
                    }
                })
                .AddTo(this);
        }

        // ====================================================================
        // 색상 갱신
        // ====================================================================

        /// <summary>
        /// 현재 소유 팀과 선택 상태에 따라 머티리얼 색상을 갱신.
        /// [Phase 2] SpriteRenderer.color → material.color 로 변경.
        /// </summary>
        private void UpdateColor()
        {
            if (_config == null || _materialInstance == null) return;

            Color baseColor = _config.GetTeamColor(_currentOwner);

            if (_isSelected)
            {
                // 기존 색상에 노란 틴트를 곱하여 하이라이트 효과
                baseColor *= _config.SelectedTint;
            }

            _materialInstance.SetColor("_BaseColor", baseColor);
        }
    }
}
