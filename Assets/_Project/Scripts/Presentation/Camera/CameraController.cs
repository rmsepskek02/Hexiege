// ============================================================================
// CameraController.cs
// 카메라 팬(드래그 이동)과 줌(스크롤/핀치)을 처리하는 컴포넌트.
//
// 부착 위치: Main Camera
//
// GDD 카메라 사양:
//   - 기본 뷰: 고정 각도 탑다운 (Perspective 또는 Orthographic)
//   - 화면 이동: 한 손가락 드래그 (Pan) — XZ 평면
//   - 확대/축소: 두 손가락 핀치 또는 마우스 스크롤 (Zoom)
//   - 줌 범위: GameConfig에서 설정 (기본 3 ~ 12)
//   - 이동 제한: 맵 경계 내 (XZ 평면)
//
// XZ 평면 기반:
//   카메라는 XZ 평면(Y=고정 높이) 위를 이동.
//   팬 시 스크린 좌표 → XZ 평면 레이캐스트로 월드 좌표 계산.
//   Y축은 카메라 높이로 고정.
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
using DG.Tweening;
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

        [Header("3D 카메라 설정")]
        [Tooltip("카메라 X축 틸트 각도 (45~60도 권장). 0이면 수직 탑다운.")]
        [SerializeField] private float _tiltAngle = 55f;

        [Header("줌 보간")]
        [Tooltip("DOTween 줌 보간 지속시간 (초). 낮을수록 빠름.")]
        [SerializeField] private float _zoomDuration = 0.25f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 이 오브젝트의 Camera 컴포넌트. </summary>
        private Camera _cam;

        /// <summary> 드래그 시작 시 마우스/터치의 XZ 평면상 월드 좌표. </summary>
        private Vector3 _dragOrigin;

        /// <summary> 현재 드래그 중인지 여부. </summary>
        private bool _isDragging;

        /// <summary> 맵 경계 (카메라 이동 제한용, XZ 평면). SetBounds()로 설정. </summary>
        private Bounds _mapBounds;

        /// <summary> 맵 경계가 설정되었는지 여부. </summary>
        private bool _hasBounds;

        /// <summary> DOTween 줌 목표값. 입력 시 갱신, Tween이 이 값으로 보간. </summary>
        private float _targetZoom;

        /// <summary> 현재 실행 중인 줌 Tween. 새 입력 시 Kill 후 교체. </summary>
        private Tweener _zoomTween;

        /// <summary> XZ 평면 (Y=0). 스크린 좌표 → 월드 좌표 변환에 사용. </summary>
        private static readonly Plane _xzPlane = new Plane(Vector3.up, Vector3.zero);

        // ====================================================================
        // 초기화
        // ====================================================================

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _targetZoom = _cam.orthographicSize;
        }

        /// <summary>
        /// 카메라 X축 틸트 적용.
        /// Awake 이후 실행되어 Camera 캐시 완료 보장.
        /// </summary>
        private void Start()
        {
            ApplyTilt();
        }

        /// <summary>
        /// Inspector에서 설정한 틸트 각도를 카메라 X축 회전으로 적용.
        /// Y, Z 회전은 0으로 유지 (팬 시 방향 왜곡 방지).
        /// </summary>
        public void ApplyTilt()
        {
            transform.rotation = Quaternion.Euler(_tiltAngle, 0f, 0f);
        }

        /// <summary>
        /// 현재 설정된 틸트 각도 반환. 외부에서 Z 오프셋 계산 시 사용.
        /// </summary>
        public float TiltAngle => _tiltAngle;

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
        /// 카메라를 특정 XZ 위치로 즉시 이동. 초기 카메라 위치 설정에 사용.
        /// Y(높이)는 카메라의 현재 높이를 유지.
        /// </summary>
        public void SetPosition(Vector3 pos)
        {
            transform.position = new Vector3(pos.x, transform.position.y, pos.z);
        }

        // ====================================================================
        // 매 프레임 입력 처리
        // ====================================================================

        private void Update()
        {
            HandleZoom();
            HandlePan();
            ClampPosition(); // 줌/팬 후 항상 즉시 보정 — 순간이동 방지
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
                _targetZoom = Mathf.Clamp(_targetZoom + zoomDelta, zoomMin, zoomMax);
                _zoomTween?.Kill();
                _zoomTween = DOTween.To(
                    () => _cam.orthographicSize,
                    x => _cam.orthographicSize = x,
                    _targetZoom,
                    _zoomDuration
                ).SetEase(Ease.OutCubic);
            }
        }

        // ====================================================================
        // 팬 (드래그 이동)
        // ====================================================================

        /// <summary>
        /// 마우스/터치 드래그로 카메라 팬.
        ///
        /// 원리:
        ///   1. 드래그 시작 시 마우스/터치의 월드 좌표를 기록 (dragOrigin)
        ///   2. 매 프레임 현재 마우스/터치의 월드 좌표와의 차이 계산
        ///   3. 차이만큼 카메라를 이동 (드래그 방향의 반대)
        ///   → 마우스/손가락 아래의 맵 위치가 고정되어 "맵을 끌고 다니는" 느낌
        ///
        /// 2터치(핀치) 중에는 팬을 비활성화하여 줌과 충돌 방지.
        ///
        /// InputHandler와의 충돌 방지:
        ///   - InputHandler는 터치 release 시 drag 거리 &lt; 10px이면 클릭 처리
        ///   - CameraController는 터치 시작부터 드래그를 추적하여 카메라 이동
        ///   - 드래그하면 → CameraController가 팬, InputHandler는 거리 >= 10px이므로 클릭 무시
        ///   - 탭하면 → CameraController 미세 이동(무시 수준), InputHandler가 클릭 처리
        ///
        /// New Input System API:
        ///   mouse.leftButton.wasPressedThisFrame  — 드래그 시작 감지
        ///   mouse.leftButton.isPressed            — 드래그 중 감지
        ///   mouse.leftButton.wasReleasedThisFrame — 드래그 종료 감지
        ///   mouse.position.ReadValue()            — 현재 마우스 스크린 좌표
        ///   EnhancedTouch.Touch.activeTouches     — 모바일 터치 (1터치 팬, 2터치 핀치)
        /// </summary>
        private void HandlePan()
        {
            // 2터치(핀치 줌) 중에는 팬 비활성화 (마우스/터치 공통 가드)
            var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            if (activeTouches.Count >= 2)
            {
                _isDragging = false;
                return;
            }

            float panSpeed = _config != null ? _config.CameraPanSpeed : 1f;

            // --- 마우스 팬 (에디터 및 데스크톱) ---
            // activeTouches.Count == 0: 모바일에서 Mouse.current가 null이 아닐 수 있으므로
            // 터치가 없을 때만 마우스 팬 처리 (터치 팬과 충돌 방지)
            var mouse = Mouse.current;
            if (mouse != null && activeTouches.Count == 0)
            {
                // 드래그 시작 (마우스 왼쪽 버튼)
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 mousePos = mouse.position.ReadValue();
                    _dragOrigin = ScreenToXZPlane(mousePos);
                    _isDragging = true;
                }

                // 드래그 중
                if (mouse.leftButton.isPressed && _isDragging)
                {
                    Vector2 mousePos = mouse.position.ReadValue();
                    Vector3 currentPos = ScreenToXZPlane(mousePos);
                    Vector3 diff = _dragOrigin - currentPos;
                    // XZ 평면에서의 차이만 적용 (Y 높이는 유지)
                    transform.position += new Vector3(diff.x, 0f, diff.z) * panSpeed;
                }

                // 드래그 종료
                if (mouse.leftButton.wasReleasedThisFrame)
                    _isDragging = false;
            }

            // --- 터치 팬 (실제 모바일 디바이스) ---
            // Touchscreen.current != null: 실제 터치스크린 기기에서만 처리
            // (mouse == null 조건은 Android에서 가상 마우스 때문에 항상 false가 될 수 있음)
            if (Touchscreen.current != null && activeTouches.Count == 1)
            {
                var touch = activeTouches[0];

                // 터치 시작 — 드래그 원점 기록
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    _dragOrigin = ScreenToXZPlane(touch.screenPosition);
                    _isDragging = true;
                }

                // 터치 이동 — XZ 평면상 월드 좌표 차이만큼 카메라 이동
                if (_isDragging && (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved
                    || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary))
                {
                    Vector3 currentPos = ScreenToXZPlane(touch.screenPosition);
                    Vector3 diff = _dragOrigin - currentPos;
                    transform.position += new Vector3(diff.x, 0f, diff.z) * panSpeed;
                }

                // 터치 종료
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended
                    || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    _isDragging = false;
                }
            }
        }

        /// <summary>
        /// 카메라 위치를 맵 경계 내로 제한 (XZ 평면).
        ///
        /// 핵심 원리:
        ///   55도 틸트 카메라는 transform.position.z ≠ 화면 중심이 바라보는 지면 Z.
        ///   카메라 높이 H에서 tiltAngle로 기울어지면,
        ///   화면 중심 지면 좌표 = camera.z + H / tan(tiltAngle).
        ///   → 카메라 위치를 직접 클램프하면 지면 기준으로는 오프셋이 생겨 엣지 타일을 볼 수 없음.
        ///
        /// 해결:
        ///   1. 카메라 위치 → 지면 look-at 좌표로 변환
        ///   2. look-at 좌표를 타일 경계 내로 클램프 (줌 레벨 반영)
        ///   3. 다시 카메라 위치로 역변환
        ///
        /// 줌 레벨(orthographicSize)에 따라 화면이 표시하는 영역이 달라지므로
        /// halfW/halfH만큼 여유를 두어 타일 끝까지 볼 수 있도록 허용 범위를 동적으로 계산.
        /// → 줌인 시 더 넓은 범위로 이동 가능, 줌아웃 시 맵 중앙 고정.
        /// </summary>
        private void ClampPosition()
        {
            if (!_hasBounds) return;

            // 화면이 표시하는 절반 너비/높이 (월드 단위, 지면 기준)
            float halfW = _cam.orthographicSize * _cam.aspect;
            float sinTilt = Mathf.Sin(_tiltAngle * Mathf.Deg2Rad);
            float halfH = sinTilt > 0.001f ? _cam.orthographicSize / sinTilt : _cam.orthographicSize;

            // 카메라 위치 → 화면 중심이 바라보는 지면 좌표(look-at)로 변환
            float cameraHeight = transform.position.y;
            float tanTilt = Mathf.Tan(_tiltAngle * Mathf.Deg2Rad);
            float zOffset = tanTilt > 0.001f ? cameraHeight / tanTilt : 0f;

            Vector3 pos = transform.position;
            float lookAtX = pos.x;
            float lookAtZ = pos.z + zOffset;

            // 지면 좌표를 타일 경계 내로 클램프
            lookAtX = Mathf.Clamp(lookAtX, _mapBounds.min.x + halfW, _mapBounds.max.x - halfW);
            lookAtZ = Mathf.Clamp(lookAtZ, _mapBounds.min.z + halfH, _mapBounds.max.z - halfH);

            // 다시 카메라 위치로 역변환
            pos.x = lookAtX;
            pos.z = lookAtZ - zOffset;
            transform.position = pos;
        }

        // ====================================================================
        // XZ 평면 레이캐스트
        // ====================================================================

        /// <summary>
        /// 스크린 좌표 → XZ 평면(Y=0) 위의 월드 좌표로 변환.
        /// 카메라 레이를 XZ 평면에 교차시켜 정확한 월드 위치를 얻음.
        /// </summary>
        private Vector3 ScreenToXZPlane(Vector2 screenPos)
        {
            Ray ray = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            if (_xzPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            // 레이캐스트 실패 시 (카메라가 평면과 평행한 극단적 경우) 폴백
            return transform.position;
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

        private void OnDestroy()
        {
            _zoomTween?.Kill();
        }
    }
}
