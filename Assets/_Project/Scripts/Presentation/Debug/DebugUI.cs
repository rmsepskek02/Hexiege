// ============================================================================
// DebugUI.cs
// 화면에 디버그 정보를 표시하는 컴포넌트.
//
// 부착 위치: [Debug]/DebugUI
//
// 표시 정보:
//   - FPS (초당 프레임 수)
//   - 마우스 아래 타일 좌표 (HexCoord)
//   - 해당 타일의 소유 팀
//   - 현재 타일 총 수
//
// Unity의 OnGUI()를 사용하여 화면 좌상단에 텍스트 표시.
// OnGUI는 IMGUI(Immediate Mode GUI)로, 별도 Canvas 없이 동작.
// 프로토타입/디버그 전용. 출시 시 제거 또는 비활성화.
//
// New Input System 사용:
//   Mouse.current.position.ReadValue() — 마우스 스크린 좌표 읽기
//   레거시 Input.mousePosition 대체.
//
// Presentation 레이어 — Unity 의존.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    public class DebugUI : MonoBehaviour
    {
        // ====================================================================
        // 외부 참조 (GameBootstrapper 또는 Inspector에서 설정)
        // ====================================================================

        /// <summary> 타일 조회용 그리드 참조. </summary>
        private HexGrid _grid;

        /// <summary> 메인 카메라 (ScreenToWorldPoint 변환). </summary>
        private Camera _mainCamera;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> FPS 계산용 프레임 카운터. </summary>
        private float _fpsTimer;
        private int _fpsFrameCount;
        private float _currentFps;

        /// <summary> 마우스 아래 타일 좌표. </summary>
        private HexCoord _hoverCoord;

        /// <summary> 마우스 아래 타일 데이터. </summary>
        private HexTile _hoverTile;

        /// <summary> 마지막 선택된 좌표. 이벤트로 갱신. </summary>
        private HexCoord? _lastSelectedCoord;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 외부에서 의존성 주입. GameBootstrapper에서 호출하거나,
        /// Start()에서 자동으로 찾기.
        /// </summary>
        public void Initialize(HexGrid grid, Camera mainCamera)
        {
            _grid = grid;
            _mainCamera = mainCamera;
        }

        private void Start()
        {
            // 자동 초기화 (GameBootstrapper에서 주입하지 않은 경우 대비)
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            // 타일 선택 이벤트 구독 → 디버그 표시 갱신
            GameEvents.OnTileSelected
                .Subscribe(e => _lastSelectedCoord = e.Coord)
                .AddTo(this);
        }

        // ====================================================================
        // 매 프레임 갱신
        // ====================================================================

        /// <summary>
        /// FPS 계산 및 마우스 아래 타일 정보 갱신.
        ///
        /// New Input System API:
        ///   Mouse.current — 현재 마우스 디바이스 (null이면 마우스 미연결)
        ///   Mouse.current.position.ReadValue() — 마우스 스크린 좌표 (Vector2)
        ///   ScreenToWorldPoint에 Vector3 필요하므로 z=0으로 변환.
        /// </summary>
        private void Update()
        {
            // FPS 계산
            _fpsFrameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 1f)
            {
                _currentFps = _fpsFrameCount / _fpsTimer;
                _fpsFrameCount = 0;
                _fpsTimer = 0f;
            }

            // 마우스 아래 타일 갱신
            if (_mainCamera != null && _grid != null)
            {
                // New Input System으로 마우스 위치 읽기
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 mousePos = mouse.position.ReadValue();
                    Vector3 worldPos = _mainCamera.ScreenToWorldPoint(
                        new Vector3(mousePos.x, mousePos.y, 0f));
                    _hoverCoord = HexMetrics.WorldToHex(worldPos);
                    _hoverTile = _grid.GetTile(_hoverCoord);
                }
            }
        }

        // ====================================================================
        // 화면 표시 (IMGUI)
        // ====================================================================

        /// <summary>
        /// 화면 좌상단에 디버그 정보 표시.
        /// OnGUI는 매 렌더 프레임마다 호출됨.
        /// </summary>
        private void OnGUI()
        {
            // 배경 스타일 설정 (검정 반투명 배경으로 가독성 확보)
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            float x = 10f;
            float y = 10f;
            float lineHeight = 20f;

            // FPS
            GUI.Label(new Rect(x, y, 300, lineHeight),
                $"FPS: {_currentFps:F1}", style);
            y += lineHeight;

            // 마우스 아래 타일 좌표
            GUI.Label(new Rect(x, y, 300, lineHeight),
                $"Hover: {_hoverCoord}", style);
            y += lineHeight;

            // 타일 소유 팀
            if (_hoverTile != null)
            {
                GUI.Label(new Rect(x, y, 300, lineHeight),
                    $"Owner: {_hoverTile.Owner}  Walkable: {_hoverTile.IsWalkable}", style);
            }
            else
            {
                GUI.Label(new Rect(x, y, 300, lineHeight),
                    "Owner: (outside grid)", style);
            }
            y += lineHeight;

            // 선택된 타일
            if (_lastSelectedCoord.HasValue)
            {
                GUI.Label(new Rect(x, y, 300, lineHeight),
                    $"Selected: {_lastSelectedCoord.Value}", style);
            }
            y += lineHeight;

            // 총 타일 수
            if (_grid != null)
            {
                GUI.Label(new Rect(x, y, 300, lineHeight),
                    $"Tiles: {_grid.Tiles.Count}", style);
            }
        }
    }
}
