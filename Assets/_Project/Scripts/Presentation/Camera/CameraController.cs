// ============================================================================
// CameraController.cs
// 카메라 팬(드래그 이동)과 줌(스크롤/핀치)을 처리하는 컴포넌트.
//
// 부착 위치: Main Camera
//
// GDD 카메라 사양:
//   - 기본 뷰: 고정 각도 탑다운 (Orthographic)
//   - 화면 이동: 한 손가락 드래그 (Pan)
//   - 확대/축소: 두 손가락 핀치 또는 마우스 스크롤 (Zoom)
//   - 줌 범위: GameConfig에서 설정 (기본 3 ~ 12)
//   - 이동 제한: 맵 경계 내
//
// 팬 구현:
//   마우스/터치 드래그 시 이전 프레임과 현재 프레임의
//   월드 좌표 차이만큼 카메라를 반대 방향으로 이동.
//   (드래그한 만큼 맵이 따라옴 = 카메라가 반대로 이동)
//
// 줌 구현:
//   마우스 스크롤 → Camera.orthographicSize 변경
//   모바일 핀치 → 두 터치 사이 거리 변화로 줌 계산
//
// New Input System 사용:
//   Mouse.current  — 마우스 스크롤, 버튼, 위치
//   Touchscreen.current — 모바일 터치 (핀치 줌)
//
// Presentation 레이어 — Unity 의존.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("Config")]
        [Tooltip("GameConfig ScriptableObject (줌 범위, 속도 등)")]
        [SerializeField] private GameConfig _config;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 이 오브젝트의 Camera 컴포넌트. </summary>
        private Camera _cam;

        /// <summary> 드래그 시작 시 마우스/터치의 월드 좌표. </summary>
        private Vector3 _dragOrigin;

        /// <summary> 현재 드래그 중인지 여부. </summary>
        private bool _isDragging;

        /// <summary> 맵 경계 (카메라 이동 제한용). SetBounds()로 설정. </summary>
        private Bounds _mapBounds;

        /// <summary> 맵 경계가 설정되었는지 여부. </summary>
        private bool _hasBounds;

        // ====================================================================
        // 초기화
        // ====================================================================

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        /// <summary>
        /// 맵 경계를 설정. GameBootstrapper에서 그리드 생성 후 호출.
        /// 카메라가 이 경계를 벗어나지 않도록 제한.
        /// </summary>
        /// <param name="center">맵 중심 월드 좌표</param>
        /// <param name="size">맵 크기 (가로, 세로)</param>
        public void SetBounds(Vector3 center, Vector3 size)
        {
            _mapBounds = new Bounds(center, size);
            _hasBounds = true;
        }

        /// <summary>
        /// 카메라를 특정 위치로 즉시 이동. 초기 카메라 위치 설정에 사용.
        /// z는 카메라 깊이를 유지.
        /// </summary>
        public void SetPosition(Vector3 pos)
        {
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        }

        // ====================================================================
        // 매 프레임 입력 처리
        // ====================================================================

        private void Update()
        {
            HandleZoom();
            HandlePan();
        }

        // ====================================================================
        // 줌 (스크롤 / 핀치)
        // ====================================================================

        /// <summary>
        /// 마우스 스크롤 또는 모바일 핀치로 줌 처리.
        ///
        /// Orthographic Size가 작을수록 줌인(확대), 클수록 줌아웃(축소).
        /// GameConfig에서 설정한 min~max 범위로 클램프.
        ///
        /// New Input System API:
        ///   Mouse.current.scroll.ReadValue() — 스크롤 휠 델타 (Vector2)
        ///     .y 값이 양수면 위로 스크롤 (줌인), 음수면 아래로 스크롤 (줌아웃)
        ///     레거시 Input.mouseScrollDelta.y와 동일한 의미.
        ///     단, New Input System의 scroll 값은 120 단위 (Windows notch 기준)이므로
        ///     /120f로 정규화하여 레거시와 동일한 스케일로 변환.
        ///
        ///   Touchscreen.current — 모바일 터치스크린 디바이스
        ///     .touches — 현재 활성 터치 목록
        ///     개별 터치의 .position.ReadValue()로 위치 읽기
        ///     개별 터치의 .delta.ReadValue()로 이전 프레임 대비 이동량 읽기
        /// </summary>
        private void HandleZoom()
        {
            float zoomSpeed = _config != null ? _config.CameraZoomSpeed : 2f;
            float zoomMin = _config != null ? _config.CameraZoomMin : 3f;
            float zoomMax = _config != null ? _config.CameraZoomMax : 12f;

            float zoomDelta = 0f;

            // --- 마우스 스크롤 ---
            var mouse = Mouse.current;
            if (mouse != null)
            {
                // New Input System의 scroll.y 값은 플랫폼마다 스케일이 다름:
                //   Windows: ~120 per notch (raw WM_MOUSEWHEEL)
                //   일부 환경: ~1.0 per notch (이미 정규화)
                // 자동 감지: 절대값이 10 초과면 raw(120단위)로 간주하여 /120f 정규화.
                // 아니면 이미 정규화된 값으로 사용.
                float rawScroll = mouse.scroll.ReadValue().y;
                float scroll = Mathf.Abs(rawScroll) > 10f ? rawScroll / 120f : rawScroll;

                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // 스크롤 위 = 줌인(Size 감소), 스크롤 아래 = 줌아웃(Size 증가)
                    zoomDelta = -scroll * zoomSpeed;
                }
            }

            // --- 모바일 핀치 ---
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 2)
            {
                var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
                Vector2 t0Pos = touches[0].screenPosition;
                Vector2 t1Pos = touches[1].screenPosition;
                Vector2 t0Delta = touches[0].delta;
                Vector2 t1Delta = touches[1].delta;

                // 이전 프레임과 현재 프레임의 두 터치 사이 거리 비교
                float prevDist = Vector2.Distance(t0Pos - t0Delta, t1Pos - t1Delta);
                float currDist = Vector2.Distance(t0Pos, t1Pos);

                float diff = prevDist - currDist;
                zoomDelta = diff * zoomSpeed * 0.01f;
            }

            if (Mathf.Abs(zoomDelta) > 0.001f)
            {
                _cam.orthographicSize = Mathf.Clamp(
                    _cam.orthographicSize + zoomDelta,
                    zoomMin, zoomMax);
            }
        }

        // ====================================================================
        // 팬 (드래그 이동)
        // ====================================================================

        /// <summary>
        /// 마우스/터치 드래그로 카메라 팬.
        ///
        /// 원리:
        ///   1. 드래그 시작 시 마우스의 월드 좌표를 기록 (dragOrigin)
        ///   2. 매 프레임 현재 마우스의 월드 좌표와의 차이 계산
        ///   3. 차이만큼 카메라를 이동 (드래그 방향의 반대)
        ///   → 마우스 아래의 맵 위치가 고정되어 "맵을 끌고 다니는" 느낌
        ///
        /// 2터치(핀치) 중에는 팬을 비활성화하여 줌과 충돌 방지.
        ///
        /// New Input System API:
        ///   mouse.leftButton.wasPressedThisFrame  — 드래그 시작 감지
        ///   mouse.leftButton.isPressed            — 드래그 중 감지
        ///   mouse.leftButton.wasReleasedThisFrame — 드래그 종료 감지
        ///   mouse.position.ReadValue()            — 현재 마우스 스크린 좌표
        /// </summary>
        private void HandlePan()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // 2터치(핀치 줌) 중에는 팬 비활성화
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count >= 2)
            {
                _isDragging = false;
                return;
            }

            float panSpeed = _config != null ? _config.CameraPanSpeed : 1f;

            // 드래그 시작 (마우스 왼쪽 버튼)
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = mouse.position.ReadValue();
                _dragOrigin = _cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
                _isDragging = true;
            }

            // 드래그 중
            if (mouse.leftButton.isPressed && _isDragging)
            {
                Vector2 mousePos = mouse.position.ReadValue();
                Vector3 currentPos = _cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
                Vector3 diff = _dragOrigin - currentPos;
                transform.position += diff * panSpeed;

                // 맵 경계 제한
                ClampPosition();
            }

            // 드래그 종료
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                _isDragging = false;
            }
        }

        /// <summary>
        /// 카메라 위치를 맵 경계 내로 제한.
        /// 경계가 설정되지 않았으면 무시.
        /// </summary>
        private void ClampPosition()
        {
            if (!_hasBounds) return;

            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, _mapBounds.min.x, _mapBounds.max.x);
            pos.y = Mathf.Clamp(pos.y, _mapBounds.min.y, _mapBounds.max.y);
            transform.position = pos;
        }

        // ====================================================================
        // EnhancedTouch 활성화/비활성화
        // ====================================================================

        /// <summary>
        /// New Input System의 EnhancedTouch를 활성화.
        /// EnhancedTouch.Touch.activeTouches를 사용하려면 반드시 Enable 필요.
        /// OnEnable/OnDisable에서 짝을 맞춰 호출해야 함 (참조 카운트 방식).
        /// </summary>
        private void OnEnable()
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
        }
    }
}
