// ============================================================================
// SkillAimController.cs
// 스킬 "지점 조준" 입력 상태 머신 — press(버튼) → 드래그 추적 → 조준점 이동 →
// 엣지 스크롤 → release 발동/취소(규칙 17~24).
//
// 무엇을 하나(초급자용 설명):
//   지점 지정 스킬(타입 A·B) 버튼을 누른 채 드래그하면, 손가락을 따라 조준점(범위 원)이 이동한다.
//   화면 가장자리로 끌면 카메라가 그 방향으로 자동 팬(엣지 스크롤)되어 맵 구석까지 조준할 수 있고,
//   손을 떼면 그 지점에 스킬이 발동한다. 화면 하단 중앙의 X(취소) 위에서 떼면 취소한다.
//
// 발동 본체와의 분리(§3-8 — AI 공용):
//   이 컨트롤러는 "좌표를 만드는 어댑터"일 뿐이다. 실제 발동은 콜백(onConfirm)으로 상위(스킬 패널)에
//   넘겨 SkillActivationUseCase.Activate(싱글) 또는 NetworkSkillController(멀티)로 흐른다.
//   AI는 이 어댑터를 거치지 않고 좌표를 직접 계산해 Activate를 호출한다.
//
// 입력 소유권(랠리 모드 우선순위 패턴 재사용):
//   조준 중에는 정적 플래그 IsAiming을 켜서 CameraController 드래그 팬·InputHandler 타일 선택을 억제한다.
//
// 좌표계:
//   화면 → XZ 평면(뷰 좌표) → ViewConverter.FromView → HexMetrics.WorldToHex 로 "도메인 좌표"를 만든다
//   (랠리 모드와 동일). 서버로는 이 도메인 좌표를 보낸다. 조준점(reticle)은 손가락 위치(뷰 좌표)에
//   맞춰 스냅 표시한다(유효 타일 중심으로 스냅 → 맵 밖 clamp 느낌, 규칙 22).
//
// Presentation 레이어 — Unity 의존.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 스킬 지점 조준 입력 상태 머신. 조준 중 좌표를 만들어 콜백으로 넘긴다.
    /// </summary>
    public sealed class SkillAimController : MonoBehaviour
    {
        // ====================================================================
        // 조준 모드 전역 플래그(입력 소유권 가드)
        // ====================================================================

        /// <summary>
        /// 현재 스킬 조준 중인지 여부(전역). CameraController/InputHandler가 이 값이 true이면
        /// 각자의 팬/타일 선택 입력을 억제한다(랠리 모드 우선순위와 동일 취지).
        /// </summary>
        public static bool IsAiming { get; private set; }

        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("카메라")]
        [Tooltip("스크린→월드 변환에 쓰는 메인 카메라(비우면 Camera.main).")]
        [SerializeField] private Camera _camera;

        [Tooltip("엣지 스크롤을 위임할 카메라 컨트롤러.")]
        [SerializeField] private CameraController _cameraController;

        [Header("조준점(범위 원)")]
        [Tooltip("조준 범위를 표시하는 조준점(플레이스홀더).")]
        [SerializeField] private SkillAimReticle _reticle;

        [Header("취소 영역(하단 X 버튼)")]
        [Tooltip("손을 떼면 발동을 취소하는 하단 X 버튼의 RectTransform(기존 UI 에셋 재사용, 규칙 20). " +
                 "비워두면 취소 영역 판정 없이 항상 발동/유효성만으로 분기한다.")]
        [SerializeField] private RectTransform _cancelZone;

        [Header("엣지 스크롤(규칙 18·21·23)")]
        [Tooltip("화면 가장자리에서 이 픽셀 여백 안에 조준점이 들어오면 엣지 스크롤을 발동한다.")]
        [SerializeField] private float _edgeMarginPx = 60f;

        [Tooltip("엣지 스크롤 속도(월드 단위/초).")]
        [SerializeField] private float _edgeScrollSpeed = 8f;

        // ====================================================================
        // 런타임 상태
        // ====================================================================

        // 조준 중 여부(인스턴스). IsAiming(정적)과 함께 갱신한다.
        private bool _isAiming;

        // 조준 대상 스킬 식별.
        private int _buildingId;
        private int _skillSlot;
        private float _radius;

        // 마지막으로 유효(맵 안)했던 도메인 좌표. 맵 밖으로 나가면 이 좌표를 유지(규칙 22 clamp 느낌).
        private HexCoord _lastValidCoord;
        private bool _hasValidCoord;

        // 발동/취소 콜백. onConfirm(buildingId, skillSlot, aimCoord).
        private Action<int, int, HexCoord> _onConfirm;
        private Action _onCancel;

        // 맵 타일 유효성 판정(grid.HasTile 주입). null이면 항상 유효로 간주.
        private Func<HexCoord, bool> _isValidTile;

        // XZ 평면(Y=0) 레이캐스트용.
        private static readonly Plane _xzPlane = new Plane(Vector3.up, Vector3.zero);

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 의존성 주입. GameBootstrapper에서 호출한다.
        /// </summary>
        /// <param name="camera">스크린→월드 변환 카메라.</param>
        /// <param name="cameraController">엣지 스크롤 위임 대상.</param>
        /// <param name="isValidTile">도메인 좌표가 유효한 맵 타일인지 판정(grid.HasTile). null 허용.</param>
        public void Initialize(Camera camera, CameraController cameraController, Func<HexCoord, bool> isValidTile)
        {
            if (camera != null) _camera = camera;
            if (cameraController != null) _cameraController = cameraController;
            _isValidTile = isValidTile;
        }

        // ====================================================================
        // 조준 시작 — 스킬 버튼 PointerDown에서 호출
        // ====================================================================

        /// <summary>
        /// 지점 조준을 시작한다(스킬 버튼을 누른 순간). 이후 매 프레임 조준점을 갱신하다가
        /// 손을 떼면 onConfirm(발동) 또는 onCancel(취소)을 호출한다.
        /// </summary>
        /// <param name="buildingId">발동할 스킬 건물 Id.</param>
        /// <param name="skillSlot">발동할 슬롯 번호(0-based).</param>
        /// <param name="radius">조준 범위 반경(월드 단위, 조준점 표시용).</param>
        /// <param name="onConfirm">발동 확정 콜백(buildingId, slot, 도메인 좌표).</param>
        /// <param name="onCancel">취소 콜백.</param>
        public void BeginAim(int buildingId, int skillSlot, float radius,
            Action<int, int, HexCoord> onConfirm, Action onCancel)
        {
            _buildingId = buildingId;
            _skillSlot = skillSlot;
            _radius = radius;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            _hasValidCoord = false;
            _isAiming = true;
            IsAiming = true;

            if (_camera == null) _camera = Camera.main;

            // 시작 즉시 현재 포인터 위치로 조준점을 1회 갱신(첫 프레임에도 보이도록).
            UpdateAimPoint(GetPointerScreenPos());
        }

        // ====================================================================
        // 매 프레임 — 조준점 추적 / 엣지 스크롤 / release 분기
        // ====================================================================

        private void Update()
        {
            if (!_isAiming) return;

            Vector2 screenPos = GetPointerScreenPos();

            // 1) 조준점(범위 원) 위치·유효 좌표 갱신.
            UpdateAimPoint(screenPos);

            // 2) 엣지 스크롤(조준점이 화면 가장자리 여백 안이면 카메라 팬 — 규칙 18·23).
            if (_cameraController != null)
                _cameraController.EdgeScroll(screenPos, Time.deltaTime, _edgeMarginPx, _edgeScrollSpeed);

            // 3) release 감지 → 발동/취소 분기(규칙 19·20).
            if (WasPointerReleasedThisFrame())
            {
                ResolveRelease(screenPos);
            }
        }

        /// <summary>
        /// 조준점 위치와 마지막 유효 도메인 좌표를 갱신한다.
        /// 맵 안이면 유효 타일 중심(뷰 좌표)에 조준점을 스냅하고, 맵 밖이면 마지막 유효 위치를 유지한다(규칙 22).
        /// </summary>
        private void UpdateAimPoint(Vector2 screenPos)
        {
            if (_camera == null) return;

            // 화면 → XZ 평면(뷰 좌표) → 도메인 좌표(랠리 모드와 동일 변환).
            Vector3 viewPos = ScreenToXZPlane(screenPos);
            Vector3 domainWorld = ViewConverter.FromView(viewPos);
            HexCoord coord = HexMetrics.WorldToHex(domainWorld);

            bool valid = _isValidTile == null || _isValidTile(coord);
            if (valid)
            {
                _lastValidCoord = coord;
                _hasValidCoord = true;
            }

            // 조준점은 "마지막 유효 타일 중심"(뷰 좌표)에 스냅해 표시한다(맵 밖 clamp 느낌).
            if (_reticle != null && _hasValidCoord)
            {
                Vector3 snappedView = ViewConverter.ToView(HexMetrics.HexToWorld(_lastValidCoord));
                _reticle.Show(snappedView, _radius);
            }
        }

        /// <summary>
        /// 손을 뗀 순간의 분기: 하단 X 위면 취소, 아니면 유효 좌표에 발동(없으면 취소).
        /// </summary>
        private void ResolveRelease(Vector2 screenPos)
        {
            bool overCancel = IsOverCancelZone(screenPos);

            // 조준 종료(플래그/조준점 정리)를 먼저 한다 — 콜백에서 패널을 닫아도 안전.
            EndAim();

            if (overCancel || !_hasValidCoord)
            {
                _onCancel?.Invoke();
                return;
            }

            _onConfirm?.Invoke(_buildingId, _skillSlot, _lastValidCoord);
        }

        /// <summary>
        /// 외부에서 조준을 강제 취소한다(패널 닫힘/게임 종료 등).
        /// </summary>
        public void CancelAim()
        {
            if (!_isAiming) return;
            EndAim();
            _onCancel?.Invoke();
        }

        /// <summary>
        /// 조준 상태를 정리한다(플래그 해제 + 조준점 숨김). 콜백은 호출하지 않는다.
        /// </summary>
        private void EndAim()
        {
            _isAiming = false;
            IsAiming = false;
            _reticle?.Hide();
        }

        // ====================================================================
        // 취소 영역 판정
        // ====================================================================

        /// <summary>
        /// 스크린 좌표가 하단 X(취소) 버튼 영역 안인지 판정. _cancelZone 미배선이면 항상 false.
        /// (Screen Space - Overlay 캔버스 가정: RectangleContainsScreenPoint에 카메라 null 전달.)
        /// </summary>
        private bool IsOverCancelZone(Vector2 screenPos)
        {
            if (_cancelZone == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_cancelZone, screenPos, null);
        }

        // ====================================================================
        // 포인터 입력 헬퍼(마우스/터치 통합)
        // ====================================================================

        /// <summary>
        /// 현재 포인터(터치 우선, 없으면 마우스)의 스크린 좌표.
        /// </summary>
        private Vector2 GetPointerScreenPos()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
                return touch.primaryTouch.position.ReadValue();

            var mouse = Mouse.current;
            if (mouse != null)
                return mouse.position.ReadValue();

            return Vector2.zero;
        }

        /// <summary>
        /// 이번 프레임에 포인터(터치/마우스 왼쪽 버튼)를 뗐는지 여부.
        /// </summary>
        private bool WasPointerReleasedThisFrame()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasReleasedThisFrame)
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
                return true;

            return false;
        }

        /// <summary>
        /// 스크린 좌표 → XZ 평면(Y=0) 위 월드 좌표(뷰 좌표계).
        /// </summary>
        private Vector3 ScreenToXZPlane(Vector2 screenPos)
        {
            if (_camera == null) return Vector3.zero;
            Ray ray = _camera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            if (_xzPlane.Raycast(ray, out float distance))
                return ray.GetPoint(distance);
            return Vector3.zero;
        }

        private void OnDisable()
        {
            // 비활성화 시 조준 상태가 남아 입력이 잠기는 것을 방지(안전장치).
            if (_isAiming)
            {
                _isAiming = false;
                IsAiming = false;
                _reticle?.Hide();
            }
        }
    }
}
