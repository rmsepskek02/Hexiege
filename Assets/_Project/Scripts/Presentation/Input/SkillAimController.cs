// ============================================================================
// SkillAimController.cs
// 스킬 "지점 조준" 입력 상태 머신 — 탭 기반 2단계(규칙 17~20·20-1).
//
// 무엇을 하나(초급자용 설명 — 2단계):
//   [1단계] 스킬 버튼을 "탭"하면 조준 모드로 진입한다. 진입 즉시 범위(조준 원)가 지도 위(기본:
//           화면 중앙의 유효 타일, 없으면 시전 건물 타일)에 나타나고, 하단 취소(X) 버튼이 켜진다.
//           **버튼에서 손을 떼도 발동/종료되지 않는다**(그 버튼 gesture는 무시). 조준 모드는 유지된다.
//   [2단계] 이후 화면을 새로 눌러 드래그하면 범위가 손가락을 따라 이동한다(엣지 스크롤·맵 clamp 포함).
//           손을 떼면 그 위치에 발동한다. 드래그 중 손가락을 하단 X 위로 가져가면 취소 버튼이 확대되어
//           예고(규칙 20-1)되고, X 위에서 떼면 취소된다.
//
// 발동 본체와의 분리(§3-8 — AI 공용):
//   이 컨트롤러는 "좌표를 만드는 어댑터"일 뿐이다. 실제 발동은 콜백(onConfirm)으로 상위(스킬 패널)에
//   넘겨 SkillActivationUseCase.Activate(싱글) 또는 NetworkSkillController(멀티)로 흐른다.
//   AI는 이 어댑터를 거치지 않고 좌표를 직접 계산해 Activate를 호출한다.
//
// 입력 소유권(랠리 모드 우선순위 패턴 재사용):
//   조준 중에는 정적 플래그 IsAiming을 켜서 CameraController 드래그 팬·InputHandler 타일 선택을 억제한다.
//
// 좌표계(좌표화 이후 — 타일 스냅 폐지):
//   화면 → XZ 평면(뷰 좌표) → ViewConverter.FromView 로 "연속 도메인 좌표"를 만든다(타일 스냅 없음).
//   서버로는 이 연속 도메인 좌표(Vector3, XZ·Y=0)를 보낸다. 조준점(reticle)은 손가락 위치를 그대로
//   따라가며, 맵 밖으로 나갈 때만 맵 경계(최외곽 타일 바깥선) 안으로 clamp된다(규칙 22).
//
// ── 2026-11-17 공습 Task L-3 추가(_Tasks/2026-11-17/09_12_support-skill-air-strike — Plan U-2~U-10 · U-15~U-18 · M-1) ──
//   (1) 손가락을 "터치 Id"로 따라간다(SkillAimPointerTracker). 예전에는 첫 손가락(primaryTouch)만 읽었는데,
//       두 손가락 중 첫 손가락을 먼저 떼면 primaryTouch 가 남은 손가락으로 바뀌어 판정 · 캐시 좌표가 오염될 수 있었다(R-16).
//   (2) 두 손가락(조준 중 화면 이동 · 줌 — CameraController)이 되어도 조준은 유지된다. 조준점은 "첫 손가락 아래 월드 지점"을
//       계속 따라가고(U-15), 두 손가락 동안 엣지 스크롤은 끈다(U-18).
//   (3) 떼는 규칙 「한 손가락만 남아야 발동」(U-16): 두 번째 손가락을 먼저 떼면 한 손가락 조준으로 돌아가고,
//       첫 손가락을 먼저 떼면 발동하지 않고 조준이 남는다(남은 손가락을 떼도 발동 안 함 → 다시 터치해 끌고 떼면 발동).
//   (4) 공습(직선 연속 폭격 — SkillMechanicType.LineStrike)은 BeginLineAim 으로 들어온다(직선 모드):
//       누른 자리 = 직사각형 시작점(U-2) · 끈 쪽 = 방향(U-3, 도메인 좌표끼리 빼서 — P-10) · 최소 드래그(약 0.5칸) 미만이면
//       발사하지 않고 조준 유지(U-5) · 다시 누르면 시작점 이동(U-6) · X 위에서 떼면 취소(U-7) · 엣지 스크롤 없음(U-8).
//       첫 터치 전에는 직사각형 없이 기존 조준과 같은 범위 원 · X 버튼만 보인다(M-1).
//   진단 로그 [SKILL-AIM](Diag=SkillAim — AimBegin · AimAnchorSet · AimFingerCountChanged · AimReleaseResolved)은
//   손가락이 닿고 떨어지는 순간에만 1줄씩 남긴다(드래그 중 매 프레임 남기지 않는다 — LogRules 금지 8).
//
// Presentation 레이어 — Unity 의존.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;

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

        /// <summary>
        /// 지금 조준이 공습(직선) 모드인지(전역, 2026-11-17 L-3 추가). CameraController 진단 로그 [CAM-GESTURE] 의 AimMode 키가 읽는다.
        /// 조준 중이 아니면 false.
        /// </summary>
        public static bool IsLineAim { get; private set; }

        private System.Func<bool> _gameplayInputBlocked;
        /// <summary>게임 입력 판정은 조합 루트가 주입한다. 없는 경우 기존 동작을 유지한다.</summary>
        public void SetGameplayInputGate(System.Func<bool> isBlocked)
        {
            _gameplayInputBlocked = isBlocked;
        }


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

        [Header("취소 버튼 hover 피드백(규칙 20-1)")]
        [Tooltip("손가락이 취소(X) 버튼 위에 있을 때 버튼을 몇 배로 확대할지(예고 피드백).")]
        [SerializeField] private float _cancelHoverScale = 1.25f;

        [Tooltip("취소 버튼 확대/복귀 보간 속도(클수록 빠름). 프레임 독립 보간(1-exp(-speed*dt))에 쓰인다.")]
        [SerializeField] private float _cancelHoverLerpSpeed = 14f;

        [Header("공습(직선) 조준 — 2026-11-17 L-3")]
        [Tooltip("공습 최소 드래그 거리(칸 단위). 시작점에서 손을 뗀 곳까지의 거리가 (이 값 × 타일 폭) 월드 단위보다 짧으면 " +
                 "발사하지 않고 조준을 유지한다(사용자 결정 U-5 — 약 0.5칸). 화면 픽셀이 아니라 지도(월드) 거리라 줌과 무관하다.")]
        [SerializeField] private float _lineMinDragTiles = 0.5f;

        // ====================================================================
        // 런타임 상태
        // ====================================================================

        // 조준 모드 활성 여부(인스턴스). IsAiming(정적)과 함께 갱신한다.
        //   탭 기반 2단계: BeginAim(버튼 탭)으로 켜지고, 화면 드래그-release(발동/취소)로 꺼진다.
        private bool _isAiming;

        // 2단계 상태:
        //   _pendingButtonRelease = true : 버튼 탭 gesture(스킬 버튼 press)가 아직 안 끝남 → 이 gesture는 무시.
        //   _dragging            = true : 버튼을 뗀 뒤 새로 시작한 "화면 드래그" 진행 중(이때 조준 이동/발동).
        private bool _pendingButtonRelease;
        private bool _dragging;

        // 조준 대상 스킬 식별.
        private int _buildingId;
        private int _skillSlot;
        private float _radius;

        // 마지막 조준 도메인 좌표(연속). 좌표화 이후 정수 타일이 아니라 손가락을 그대로 따라가는
        //   연속 월드 좌표다. 맵 밖으로 나가면 맵 경계 안으로 clamp된 값이 담긴다(규칙 22).
        private Vector3 _lastValidDomain;
        private bool _hasValidCoord;

        // 발동/취소 콜백. onConfirm(buildingId, skillSlot, 도메인 월드 좌표(연속)).
        private Action<int, int, Vector3> _onConfirm;
        private Action _onCancel;

        // 연속 도메인 좌표를 맵 경계 안으로 clamp하는 함수(주입). null이면 clamp 없이 그대로 사용.
        private Func<Vector3, Vector3> _clampToBounds;
        // 연속 도메인 좌표가 맵 경계 안인지 판정하는 함수(주입). 기본 조준 위치 결정에만 사용. null이면 항상 유효.
        private Func<Vector3, bool> _isWithinBounds;

        // 취소(X) 버튼의 기준(원래) 스케일. hover 시 이 값의 배수로 확대한다.
        private Vector3 _cancelBaseScale = Vector3.one;

        // 취소 버튼 스케일 보간 목표. ApplyCancelHover가 목표만 세우고, TickCancelHover가 매 프레임 보간한다.
        private Vector3 _cancelTargetScale = Vector3.one;

        // 드래그 중 "마지막으로 확인된 유효 포인터 스크린 좌표".
        //   실기기(터치)에서 손을 뗀 프레임에는 primaryTouch.press.isPressed가 그 프레임에 false가 되어
        //   포인터 좌표를 신뢰할 수 없다(마우스 폴백도 없으므로 (0,0)이 나옴). 그 (0,0)으로 취소/발동을
        //   판정하면 "취소 버튼 위에서 뗐는데도 발동"되는 버그가 생긴다. 이를 막기 위해 드래그 중 매 프레임
        //   유효 좌표를 여기에 캐시해 두고, release 프레임처럼 유효 좌표가 없을 때 이 값으로 판정한다.
        private Vector2 _lastDragScreenPos;

        // XZ 평면(Y=0) 레이캐스트용.
        private static readonly Plane _xzPlane = new Plane(Vector3.up, Vector3.zero);

        // ── 2026-11-17 L-3 추가 상태 ──────────────────────────────────────

        // 손가락 추적 상태 머신(터치 Id 기반 — 떼는 순서 판정 U-16 · 캐시 오염 방지 R-16).
        private readonly SkillAimPointerTracker _pointerTracker = new SkillAimPointerTracker();

        // 이번 프레임에 눌려 있는 포인터 목록(매 프레임 Clear 후 재사용 — 할당 없음).
        private readonly List<AimPointerSample> _pressedSamples = new List<AimPointerSample>(4);

        // 이 조준에서 터치를 한 번이라도 보았는가. true면 이 조준이 끝날 때까지 마우스를 읽지 않는다.
        //   이유: 실기기(Android)에서 손을 뗀 프레임 등에 합성 마우스 좌표((0,0) 등)가 보고되는 일이 있었다(규칙 20-1 버그 원인).
        //   터치 기기에서 마우스 입력을 아예 섞지 않으면 그 좌표가 새 접촉으로 들어오는 일이 없다.
        private bool _touchSeenThisAim;

        // 조준 모드 — 원(포격 · EMP · 서리 감옥 · 수액 늪) / 직선(공습).
        private enum AimMode { Circle = 0, Line = 1 }
        private AimMode _mode = AimMode.Circle;

        // 공습(직선) 조준 값 — BeginLineAim 이 스킬 데이터에서 받아 둔다.
        private float _lineLength;   // 직선 길이(월드)
        private float _lineWidth;    // 직선 폭(월드)

        // 공습 시작점(도메인 월드, 맵 경계 clamp 됨)과 방향(도메인, 정규화). 시작점은 지도에 고정 — 카메라가 움직여도 같은 지점(R-7).
        private Vector3 _lineAnchorDomain;
        private Vector3 _lineDirectionDomain = Vector3.forward;
        private bool _lineAnchorSet;      // 이번 조준에서 시작점을 한 번이라도 정했는가(첫 터치 전 = false — M-1)
        private float _lineDragDistance;  // 시작점 → 추적 손가락 아래 지점 거리(도메인 월드 — 최소 드래그 판정 U-5)

        // 방향을 새로 정하는 최소 거리(월드). 시작점 바로 위에서 손가락이 조금 떨리는 정도로는 방향이 휙휙 돌지 않게 한다.
        //   발사 판정(최소 드래그 약 0.5칸)과는 별개 — 그보다 훨씬 작다.
        private const float LineDirectionUpdateMinWorld = 0.05f;

        // 공습 직선 조준 표시(런타임 생성 — 씬 배선 없음, TowerRangeIndicator 선례). 처음 필요할 때 만든다.
        private SkillLineAimReticle _lineReticle;

        // 공습 확정 콜백(buildingId, slot, 시작점(도메인), 방향(도메인, 정규화)).
        private Action<int, int, Vector3, Vector3> _onLineConfirm;

        // 맵 경계 사각형 함수(공습 「맵 안 유효 길이」 계산용 — 서버와 같은 HexMetrics.ComputeMapWorldBounds 값). null이면 잘라내지 않는다.
        private MapWorldBoundsProvider _mapBoundsProvider;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 의존성 주입. GameBootstrapper에서 호출한다.
        /// </summary>
        /// <param name="camera">스크린→월드 변환 카메라.</param>
        /// <param name="cameraController">엣지 스크롤 위임 대상.</param>
        /// <param name="clampToBounds">연속 도메인 좌표를 맵 경계 안으로 clamp하는 함수(규칙 22). null 허용.</param>
        /// <param name="isWithinBounds">연속 도메인 좌표가 맵 경계 안인지 판정하는 함수(기본 위치 결정용). null 허용.</param>
        public void Initialize(Camera camera, CameraController cameraController,
            Func<Vector3, Vector3> clampToBounds, Func<Vector3, bool> isWithinBounds)
        {
            if (camera != null) _camera = camera;
            if (cameraController != null) _cameraController = cameraController;
            _clampToBounds = clampToBounds;
            _isWithinBounds = isWithinBounds;

            // 취소(X) 버튼 기준 스케일 기억(hover 확대의 기준값) + 평소 숨김(조준 중에만 표시, 규칙 20).
            if (_cancelZone != null)
            {
                _cancelBaseScale = _cancelZone.localScale;
                _cancelTargetScale = _cancelBaseScale;
            }
            SetCancelButtonVisible(false);
        }

        /// <summary>
        /// 하단 취소(X) 버튼의 표시/숨김을 토글한다. 조준 중에만 보이도록 BeginAim/EndAim에서 호출한다.
        /// </summary>
        private void SetCancelButtonVisible(bool visible)
        {
            if (_cancelZone != null && _cancelZone.gameObject.activeSelf != visible)
                _cancelZone.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 맵 경계 사각형 함수를 주입한다(2026-11-17 L-4 — 공습 조준 표시의 「맵 안 유효 길이」 계산용, U-14).
        /// 서버 판정(SkillActivationUseCase)에 주입되는 것과 같은 HexMetrics.ComputeMapWorldBounds 값을 넣어야
        /// 화면에 그린 길이와 서버가 폭격하는 길이가 같아진다(Plan R-14). null이면 잘라내지 않고 원래 길이를 그린다.
        /// </summary>
        /// <param name="provider">맵 경계 사각형 함수(GameBootstrapper가 _grid 를 읽는 람다로 넣는다).</param>
        public void SetMapWorldBoundsProvider(MapWorldBoundsProvider provider)
        {
            _mapBoundsProvider = provider;
        }

        // ====================================================================
        // 1단계 — 스킬 버튼 탭으로 조준 모드 진입(유지). 스킬 버튼 PointerDown에서 호출
        // ====================================================================

        /// <summary>
        /// 스킬 버튼을 "탭"한 순간 조준 모드로 진입한다(규칙 17). 진입 즉시 범위(조준 원)를 화면에 표시하고
        /// 취소(X) 버튼을 켠다. **버튼의 release로는 종료/발동하지 않는다**(그 gesture는 무시).
        /// 이후 사용자가 화면을 새로 눌러 드래그하면 범위가 손가락을 따라 이동하고, 손을 떼면 발동한다(2단계).
        /// </summary>
        /// <param name="buildingId">발동할 스킬 건물 Id.</param>
        /// <param name="skillSlot">발동할 슬롯 번호(0-based).</param>
        /// <param name="radius">조준 범위 반경(월드 단위, 조준점 표시용).</param>
        /// <param name="fallbackDomain">화면 중앙이 맵 밖일 때 쓸 기본 도메인 월드 좌표(보통 시전 건물 위치).</param>
        /// <param name="onConfirm">발동 확정 콜백(buildingId, slot, 도메인 월드 좌표(연속)).</param>
        /// <param name="onCancel">취소 콜백.</param>
        public void BeginAim(int buildingId, int skillSlot, float radius, Vector3 fallbackDomain,
            Action<int, int, Vector3> onConfirm, Action onCancel)
        {
            // [2026-11-17 L-3] 본문을 BeginAimCore 로 옮겼다(원형 · 직선 공용). 원형 조준의 동작은 같다.
            //   옛 본문(기록용 — 지금은 BeginAimCore 가 같은 순서로 실행한다):
            // if (_gameplayInputBlocked?.Invoke() == true) return;
            // _buildingId = buildingId;
            // _skillSlot = skillSlot;
            // _radius = radius;
            // _onConfirm = onConfirm;
            // _onCancel = onCancel;
            //
            // _isAiming = true;
            // IsAiming = true;
            // // 버튼 탭 gesture(현재 눌린 스킬 버튼 press)는 무시한다 — 이 gesture가 끝날 때까지 드래그로 넘어가지 않는다.
            // _pendingButtonRelease = true;
            // _dragging = false;
            //
            // if (_camera == null) _camera = Camera.main;
            //
            // // 조준 원 기본 위치 = 화면 중앙(맵 밖이면 시전 건물 위치). 진입 즉시 지도 위에 표시.
            // Vector3 start = ResolveDefaultDomain(fallbackDomain);
            // ShowReticleAtDomain(start);
            //
            // // 취소(X) 버튼을 조준하는 동안만 표시(규칙 20). 기준 스케일로 리셋.
            // SetCancelButtonVisible(true);
            // ApplyCancelHover(false);
            if (_gameplayInputBlocked?.Invoke() == true) return;
            _mode = AimMode.Circle;
            _onConfirm = onConfirm;
            _onLineConfirm = null;
            BeginAimCore(buildingId, skillSlot, radius, fallbackDomain, onCancel);
        }

        /// <summary>
        /// 공습(직선 연속 폭격)의 조준 모드로 진입한다(2026-11-17 L-3 — U-2~U-8 · M-1).
        /// 첫 터치 전에는 기존 조준과 같은 범위 원 · X 버튼만 보이고(M-1), 화면을 누르면 그 자리가 직사각형 시작점이 된다(U-2).
        /// 누른 채 끌면 끈 쪽으로 방향이 정해지고(U-3), 최소 드래그 이상 끈 뒤 손을 떼면 <paramref name="onConfirm"/>으로 발사한다(U-10).
        /// </summary>
        /// <param name="buildingId">발동할 스킬 건물 Id.</param>
        /// <param name="skillSlot">발동할 슬롯 번호(0-based).</param>
        /// <param name="radius">첫 터치 전 범위 원 반경(스킬 데이터 Radius — 기존 조준과 같은 표시, M-1).</param>
        /// <param name="lineLength">직선 길이(월드 — 스킬 데이터 LineLength).</param>
        /// <param name="lineWidth">직선 폭(월드 — 스킬 데이터 LineWidth).</param>
        /// <param name="fallbackDomain">화면 중앙이 맵 밖일 때 범위 원을 둘 기본 도메인 좌표(보통 시전 건물 위치).</param>
        /// <param name="onConfirm">발사 확정 콜백(buildingId, slot, 시작점(도메인), 방향(도메인, 정규화)).</param>
        /// <param name="onCancel">취소 콜백.</param>
        public void BeginLineAim(int buildingId, int skillSlot, float radius, float lineLength, float lineWidth,
            Vector3 fallbackDomain, Action<int, int, Vector3, Vector3> onConfirm, Action onCancel)
        {
            if (_gameplayInputBlocked?.Invoke() == true) return;
            _mode = AimMode.Line;
            _onConfirm = null;
            _onLineConfirm = onConfirm;
            _lineLength = lineLength;
            _lineWidth = lineWidth;
            BeginAimCore(buildingId, skillSlot, radius, fallbackDomain, onCancel);
        }

        /// <summary>
        /// 원형 · 직선 조준 공통 진입 처리(옛 BeginAim 본문 + 2026-11-17 손가락 추적 초기화).
        /// </summary>
        private void BeginAimCore(int buildingId, int skillSlot, float radius, Vector3 fallbackDomain, Action onCancel)
        {
            _buildingId = buildingId;
            _skillSlot = skillSlot;
            _radius = radius;
            _onCancel = onCancel;

            _isAiming = true;
            IsAiming = true;
            IsLineAim = _mode == AimMode.Line;
            // 버튼 탭 gesture(현재 눌린 스킬 버튼 press)는 무시한다 — 이 gesture가 끝날 때까지 드래그로 넘어가지 않는다.
            _pendingButtonRelease = true;
            _dragging = false;
            //   [2026-11-17] 위 두 플래그 대신 손가락 추적기가 같은 일을 한다(버튼을 누른 손가락이 모두 떨어질 때까지 새 접촉을 받지 않음).
            //   두 플래그는 기록용으로 값만 맞춰 둔다(이제 판정에 쓰지 않는다).
            _pointerTracker.Reset(waitForButtonRelease: true);
            _touchSeenThisAim = false;

            // 공습 상태 초기화 — 첫 터치 전에는 시작점이 없다(M-1).
            _lineAnchorSet = false;
            _lineDragDistance = 0f;
            _lineDirectionDomain = Vector3.forward;
            // 런타임 생성 오브젝트는 씬 전환으로 파괴될 수 있어 `?.` 대신 Unity null 검사(!= null)를 쓴다.
            if (_lineReticle != null) _lineReticle.Hide();

            if (_camera == null) _camera = Camera.main;

            // 조준 원 기본 위치 = 화면 중앙(맵 밖이면 시전 건물 위치). 진입 즉시 지도 위에 표시.
            //   공습도 첫 터치 전에는 이 원을 그대로 보여 준다(M-1 — 「기존 조준과 같은 범위 표시」).
            Vector3 start = ResolveDefaultDomain(fallbackDomain);
            ShowReticleAtDomain(start);

            // 취소(X) 버튼을 조준하는 동안만 표시(규칙 20). 기준 스케일로 리셋.
            SetCancelButtonVisible(true);
            ApplyCancelHover(false);

            LogAimBegin();
        }

        // ====================================================================
        // 매 프레임 — 2단계(화면 드래그로 범위 이동 → release 발동/취소)
        // ====================================================================

        private void Update()
        {
            if (_gameplayInputBlocked?.Invoke() == true)
            {
                CancelAim();
                return;
            }
            if (!_isAiming) return;

            // 취소(X) 버튼 스케일을 목표값으로 매 프레임 부드럽게 보간(규칙 20-1). 1·2단계 모두에서 실행.
            TickCancelHover();

            // ────────────────────────────────────────────────────────────────
            // [2026-11-17 공습 Task L-3] 아래 옛 1·2단계 판정(primaryTouch · 마우스 왼쪽 버튼 하나만 읽음)을 비활성화했다.
            //   대신 손가락 추적기(SkillAimPointerTracker)가 "터치 Id"로 첫 손가락을 따라가며 같은 판정을 한다:
            //     · 옛 _pendingButtonRelease(버튼 gesture 무시)  → 추적기 Reset(waitForButtonRelease: true)
            //     · 옛 WasPointerPressedThisFrame(새 press)       → AimPointerEvent.Began
            //     · 옛 드래그 지속 + _lastDragScreenPos 캐시     → AimPointerEvent.Moved(추적 Id 좌표만 캐시)
            //     · 옛 WasPointerReleasedThisFrame → ResolveRelease(캐시 좌표) → AimPointerEvent.Released(같은 캐시 좌표로 판정)
            //   🔴 「release 프레임에는 라이브 포인터를 읽지 않는다」(실기 취소 버그 수정 — 아래 옛 주석)는 그대로 지킨다:
            //      추적기는 "이번 프레임에 눌려 있는" 손가락만 받고, 떨어진 손가락은 직전 프레임 캐시 좌표로만 판정한다.
            //   옛 코드(기록용):
            // // ── 1단계 유지 구간: 아직 화면 드래그가 시작되지 않음 ──────────────
            // if (!_dragging)
            // {
            //     // 스킬 버튼 탭 gesture가 끝날 때까지(포인터가 떨어질 때까지) 기다렸다가,
            //     // 그 다음에 오는 "새 press"부터 드래그(조준 이동)로 인정한다.
            //     if (_pendingButtonRelease)
            //     {
            //         if (!IsPointerPressed()) _pendingButtonRelease = false; // 버튼에서 손을 뗌 → 대기 해제.
            //         return; // 버튼 gesture 동안은 아무 것도 하지 않음(range/취소버튼은 이미 표시됨).
            //     }
            //
            //     // 버튼을 뗀 뒤, 화면을 새로 누르면 그 순간부터 드래그 시작(규칙 17·19).
            //     if (WasPointerPressedThisFrame())
            //     {
            //         _dragging = true;
            //         // 방금 눌린 프레임이라 포인터가 유효하다 → 좌표를 얻어 캐시 초기값으로 삼는다.
            //         Vector2 p = TryGetPointerScreenPos(out Vector2 pressPos) ? pressPos : _lastDragScreenPos;
            //         _lastDragScreenPos = p;
            //         UpdateAimPoint(p);
            //         ApplyCancelHover(IsOverCancelZone(p));
            //     }
            //     return;
            // }
            //
            // // ── 2단계: 드래그 중 — 범위 이동 + 엣지 스크롤 + X hover + release 발동/취소 ──
            //
            // // [핵심 — 실기기 취소 버그 근본 수정]
            // //   ★ 손을 뗀 프레임(release)에는 "라이브 포인터를 절대 읽지 않는다".
            // //
            // //   왜냐하면(실기기 Android 터치에서만 재현되던 원인):
            // //     - 손을 뗀 프레임엔 터치의 press.isPressed가 그 프레임에 false로 떨어진다.
            // //     - 이때 InputSystem이 터치를 마우스로 합성하거나 시스템 포인터를 보고해서
            // //       Mouse.current가 non-null인 경우가 많다. 그 합성 마우스 좌표는 (0,0)이거나
            // //       지연/고정된 잘못된 값이다.
            // //     - 그런데 TryGetPointerScreenPos는 "터치가 안 눌렸으면 마우스 좌표를 무조건
            // //       유효(true)"로 반환하므로, 이 잘못된 (0,0)이 유효 좌표인 척 흘러들어온다.
            // //     - 결과: (0,0)은 하단 취소(X) 영역 밖이라 IsOverCancelZone=false → 취소가 아니라
            // //       발동(_onConfirm)이 실행되어 건물 글로벌 쿨다운이 걸린다.
            // //
            // //   그래서 release 프레임에는 라이브 좌표/ TryGetPointerScreenPos 결과를 완전히 무시하고,
            // //   "드래그 중 매 프레임 캐시해 둔 마지막 유효 좌표(_lastDragScreenPos)"로만 취소/발동을
            // //   판정한다. 그 캐시값 = 마지막으로 손가락이 눌려 있던 위치 = 실제로 손을 뗀 위치이므로
            // //   취소 버튼 위에서 뗐다면 정확히 취소로 판정된다.
            // //   (에디터 마우스도 동일하게 동작: 드래그 중 매 프레임 커서 위치가 캐시되고, 마우스는
            // //    손을 떼도 좌표가 튀지 않으므로 캐시값 = 실제 뗀 커서 위치 → 회귀 없음.)
            // if (WasPointerReleasedThisFrame())
            // {
            //     ResolveRelease(_lastDragScreenPos);
            //     return;
            // }
            //
            // // ── 여기부터는 "손을 아직 안 뗀" 드래그 지속 프레임 ──
            // // 이번 프레임의 포인터 좌표를 얻는다.
            // //   • 유효하면(터치가 눌린 상태 또는 에디터 마우스) 그 값을 쓰고, 캐시(_lastDragScreenPos)에 저장.
            // //   • 유효하지 않으면 마지막으로 캐시한 드래그 좌표를 쓴다(표시/스크롤이 튀지 않도록).
            // Vector2 screenPos;
            // if (TryGetPointerScreenPos(out Vector2 livePos))
            // {
            //     screenPos = livePos;
            //     _lastDragScreenPos = livePos; // 유효 좌표를 매 프레임 캐시(다음 release 프레임 대비 — 핵심).
            // }
            // else
            // {
            //     screenPos = _lastDragScreenPos; // 유효 좌표 없음 → 마지막 드래그 좌표 유지.
            // }
            //
            // // 1) 조준점(범위 원) 위치·유효 좌표 갱신(맵 밖은 마지막 유효 위치로 clamp — 규칙 22).
            // UpdateAimPoint(screenPos);
            //
            // // 2) 엣지 스크롤(조준점이 화면 가장자리 여백 안이면 카메라 팬 — 규칙 18·23).
            // if (_cameraController != null)
            //     _cameraController.EdgeScroll(screenPos, Time.deltaTime, _edgeMarginPx, _edgeScrollSpeed);
            //
            // // 3) 취소 버튼 위 hover면 확대 예고(규칙 20-1), 벗어나면 원래 크기.
            // ApplyCancelHover(IsOverCancelZone(screenPos));
            // ────────────────────────────────────────────────────────────────

            // 이번 프레임에 눌려 있는 손가락(또는 마우스 왼쪽 버튼)을 모아 추적기에 넣는다.
            GatherPressedPointers(_pressedSamples);
            AimPointerFrame frame = _pointerTracker.Step(_pressedSamples);
            _dragging = _pointerTracker.HasTracked; // 기록용 플래그(판정에는 쓰지 않음)

            // 손가락 수가 바뀐 순간에만 진단 1줄(L-L27). 버튼을 누른 손가락이 떨어지는 것은 조준 손가락이 아니라 남기지 않는다.
            if (frame.CountChanged && frame.CountFrom > 0 && frame.WaitReason != AimPointerWaitReason.ButtonGesture)
                LogFingerCountChanged(frame);

            switch (frame.Event)
            {
                case AimPointerEvent.Began:
                    OnContactBegan(frame);
                    break;
                case AimPointerEvent.Moved:
                    OnContactMoved(frame);
                    break;
                case AimPointerEvent.Released:
                    // 첫 손가락이 혼자 남은 상태에서 떨어졌다 → 발동 후보(U-16 ①). 좌표 = 떨어지기 직전 캐시(규칙 20-1).
                    _lastDragScreenPos = frame.TrackedPos;
                    if (_mode == AimMode.Line) ResolveLineRelease(frame.TrackedPos);
                    else ResolveRelease(frame.TrackedPos);
                    break;
                case AimPointerEvent.ReleasedNoFire:
                    // 첫 손가락이 다른 손가락보다 먼저(또는 함께) 떨어졌다 → 발동하지 않고 조준 유지(U-16 ②).
                    //   조준 표시(원 · 직사각형)는 마지막 자리에 그대로 남는다. 다시 터치해 끌고 떼면 발동한다.
                    ApplyCancelHover(false);
                    LogReleaseResolved("NoFire", frame.NoFireReason, IsOverCancelZone(frame.TrackedPos));
                    break;
            }
        }

        /// <summary>
        /// 조준점 위치와 마지막 조준 도메인 좌표(연속)를 갱신한다.
        /// 손가락을 그대로 따라가되(타일 스냅 없음), 맵 밖으로 나가면 맵 경계 안으로 clamp한다(규칙 22).
        /// </summary>
        private void UpdateAimPoint(Vector2 screenPos)
        {
            if (_camera == null) return;

            // 화면 → XZ 평면(뷰 좌표) → 도메인 좌표(랠리 모드와 동일 변환).
            //   타일 스냅 없이 연속 도메인 좌표(domainWorld)를 그대로 채택한다(착탄 판정이 이미 연속 원).
            Vector3 viewPos = ScreenToXZPlane(screenPos);
            Vector3 domainWorld = ViewConverter.FromView(viewPos);

            // 맵 경계 안으로 연속 좌표를 clamp한다(규칙 22 — 최외곽 타일 바깥선 기준). clamp 함수가 없으면 원본 사용.
            Vector3 clampedDomain = _clampToBounds != null ? _clampToBounds(domainWorld) : domainWorld;
            _lastValidDomain = clampedDomain;
            _hasValidCoord = true;

            // 조준점 표시: 타일 중심 되돌림 없이 "연속 좌표를 그대로" 뷰로 변환해 표시한다.
            //   ToView(FromView(v)) == v(자기 역함수)이므로, 맵 안에서는 손가락 위치(뷰)와 정확히 일치하고,
            //   맵 밖일 때만 clamp된 경계 지점(뷰)이 표시된다 → 스냅이 아니라 연속 추종 + 경계 clamp.
            if (_reticle != null)
            {
                Vector3 view = ViewConverter.ToView(clampedDomain);
                _reticle.Show(view, _radius);
            }
        }

        /// <summary>
        /// 손을 뗀 순간의 분기: 하단 X 위면 취소, 아니면 유효 좌표에 발동(없으면 취소).
        /// </summary>
        private void ResolveRelease(Vector2 screenPos)
        {
            if (_gameplayInputBlocked?.Invoke() == true)
            {
                CancelAim();
                return;
            }
            bool overCancel = IsOverCancelZone(screenPos);

            // [2026-11-17 L-3] 진단 1줄(L-L18 · L-L28) — 조준 종료 전에 남긴다(EndAim 이 모드 플래그를 내리기 때문).
            if (overCancel || !_hasValidCoord)
                LogReleaseResolved("Cancel", overCancel ? "OverCancel" : "NoValidCoord", overCancel);
            else
                LogReleaseResolved("Fire", "Released", false);

            // 조준 종료(플래그/조준점 정리)를 먼저 한다 — 콜백에서 패널을 닫아도 안전.
            EndAim();

            if (overCancel || !_hasValidCoord)
            {
                _onCancel?.Invoke();
                return;
            }

            _onConfirm?.Invoke(_buildingId, _skillSlot, _lastValidDomain);
        }

        // ====================================================================
        // [2026-11-17 L-3] 손가락 접촉 처리(원형 · 직선 공용) — 추적기 사건별
        // ====================================================================

        /// <summary>
        /// 새 접촉 시작(누른 순간). 원형 = 누른 자리로 조준점 이동(옛 동작 그대로), 직선 = 누른 자리가 시작점(U-2 · U-6).
        /// </summary>
        private void OnContactBegan(AimPointerFrame frame)
        {
            Vector2 p = frame.TrackedPos;
            _lastDragScreenPos = p;
            if (_mode == AimMode.Line)
                SetLineAnchor(p);
            else
                UpdateAimPoint(p);
            ApplyCancelHover(IsOverCancelZone(p));
        }

        /// <summary>
        /// 추적 손가락이 눌린 채 이어진다(한 손가락 또는 두 손가락). 조준점 / 방향은 "추적 손가락 아래 월드 지점"을 매 프레임 다시 구한다 —
        /// 두 손가락으로 카메라가 움직여도 손가락 아래 지점을 따라간다(U-15 · U-17).
        /// </summary>
        private void OnContactMoved(AimPointerFrame frame)
        {
            Vector2 screenPos = frame.TrackedPos;
            _lastDragScreenPos = screenPos; // 추적 Id 의 좌표만 캐시한다(R-16).

            if (_mode == AimMode.Line)
            {
                // 공습: 시작점은 지도에 고정, 방향만 손가락을 따라간다(U-3 · U-17). 엣지 스크롤 없음(U-8).
                UpdateLineDirection(screenPos);
            }
            else
            {
                // 원형: 옛 2단계와 같다 — 조준점 이동 + 엣지 스크롤(한 손가락일 때만 — U-18).
                UpdateAimPoint(screenPos);
                if (!frame.MultiTouch && _cameraController != null)
                    _cameraController.EdgeScroll(screenPos, Time.deltaTime, _edgeMarginPx, _edgeScrollSpeed);
            }

            // 취소 버튼 위 hover면 확대 예고(규칙 20-1), 벗어나면 원래 크기.
            ApplyCancelHover(IsOverCancelZone(screenPos));
        }

        // ====================================================================
        // [2026-11-17 L-3 · L-4] 공습(직선) 조준 — 시작점 · 방향 · 최소 드래그 · 표시
        // ====================================================================

        /// <summary> 공습 최소 드래그 임계값(월드) = 인스펙터 칸 수 × 타일 폭(U-5). 음수 · NaN 은 0으로 본다. </summary>
        private float LineMinDragWorld
        {
            get
            {
                float tiles = float.IsNaN(_lineMinDragTiles) || float.IsInfinity(_lineMinDragTiles) ? 0f : Mathf.Max(0f, _lineMinDragTiles);
                return tiles * HexMetrics.TileWidth;
            }
        }

        /// <summary>
        /// 누른 자리를 직사각형 시작점으로 정한다(U-2 첫 터치 · U-6 다시 터치). 시작점은 맵 경계 안으로 clamp 한다(규칙 22).
        /// 첫 터치면 범위 원을 숨기고 직사각형을 처음 보여 준다(M-1). 방향은 첫 터치면 화면 위쪽, 다시 터치면 이전 방향을 유지한다
        /// (끌기 시작하면 바로 손가락 쪽으로 바뀐다).
        /// </summary>
        private void SetLineAnchor(Vector2 screenPos)
        {
            if (_camera == null) return;
            bool retouch = _lineAnchorSet;

            Vector3 domainWorld = ViewConverter.FromView(ScreenToXZPlane(screenPos));
            _lineAnchorDomain = _clampToBounds != null ? _clampToBounds(domainWorld) : domainWorld;
            _lineAnchorDomain.y = 0f;
            if (!retouch) _lineDirectionDomain = SkillLineAimMath.DefaultDomainDirection(_lineAnchorDomain);
            _lineAnchorSet = true;
            _lineDragDistance = 0f;

            // 공습 조준에서는 원형 조준원을 쓰지 않는다 — 첫 터치 전 범위 표시(M-1)였던 원을 숨긴다.
            //   _hasValidCoord 는 원형 경로 전용이라 직선 판정에 쓰지 않는다.
            _reticle?.Hide();
            ShowLineReticle(out float effLength);

            LogAnchorSet(retouch, effLength);
        }

        /// <summary>
        /// 추적 손가락 아래 지점으로 방향과 드래그 거리를 갱신한다(P-10 — 시작점 · 손가락 둘 다 도메인으로 바꾼 뒤 차).
        /// </summary>
        private void UpdateLineDirection(Vector2 screenPos)
        {
            if (!_lineAnchorSet || _camera == null) return;
            Vector3 fingerView = ScreenToXZPlane(screenPos);
            bool ok = SkillLineAimMath.TryComputeDomainDirection(_lineAnchorDomain, fingerView,
                out Vector3 direction, out float distance);
            _lineDragDistance = distance;
            // 시작점 바로 근처(손가락 떨림 수준)에서는 방향을 바꾸지 않는다 — 직사각형이 휙휙 돌지 않게.
            if (ok && distance >= LineDirectionUpdateMinWorld) _lineDirectionDomain = direction;
            ShowLineReticle(out _);
        }

        /// <summary>
        /// 맵 안 유효 길이(U-14 — 서버와 같은 순수 함수 LineStrikeWorld.ComputeEffectiveLength)를 구한다.
        /// </summary>
        private float ComputeLineEffectiveLength()
        {
            return LineStrikeWorld.ComputeEffectiveLength(_lineAnchorDomain, _lineDirectionDomain, _lineLength,
                _mapBoundsProvider, out _);
        }

        /// <summary>
        /// 직사각형 + 화살표를 지금 시작점 · 방향 · 유효 길이 · 폭으로 그린다(L-4). 화면이 뒤집혀 있으면 화면 좌표로 바꿔 그린다.
        /// </summary>
        /// <param name="effLength">그린 유효 길이(진단 로그용).</param>
        private void ShowLineReticle(out float effLength)
        {
            effLength = ComputeLineEffectiveLength();
            // 표시물은 처음 필요할 때 만든다(씬 배선 없음). 씬이 바뀌어 사라졌으면(Unity null) 다시 만든다.
            if (_lineReticle == null) _lineReticle = SkillLineAimReticle.CreateRuntime();
            Vector3 viewStart = ViewConverter.ToView(_lineAnchorDomain);
            Vector3 viewDirection = SkillLineAimMath.DomainDirectionToView(_lineAnchorDomain, _lineDirectionDomain);
            _lineReticle.Show(viewStart, viewDirection, effLength, _lineWidth);
        }

        /// <summary>
        /// 공습 조준에서 첫 손가락이 혼자 남은 상태로 떨어졌을 때의 분기(U-5 · U-7 · U-10):
        ///   X 위 → 취소, 최소 드래그 미만 → 발사하지 않고 조준 유지, 그 밖 → 시작점 + 방향으로 발사.
        /// 좌표는 떨어지기 직전 캐시 값이다(규칙 20-1 — 라이브 포인터를 읽지 않는다).
        /// </summary>
        private void ResolveLineRelease(Vector2 screenPos)
        {
            if (_gameplayInputBlocked?.Invoke() == true)
            {
                CancelAim();
                return;
            }
            bool overCancel = IsOverCancelZone(screenPos);

            // 캐시 좌표로 방향 · 드래그 거리를 한 번 더 맞춘다(마지막 Moved 프레임과 같은 좌표라 값도 같다 — 안전장치).
            UpdateLineDirection(screenPos);

            if (overCancel)
            {
                LogReleaseResolved("Cancel", "OverCancel", true);
                EndAim();
                _onCancel?.Invoke();
                return;
            }

            if (!_lineAnchorSet || !SkillLineAimMath.IsDragEnough(_lineDragDistance, LineMinDragWorld))
            {
                // 거의 끌지 않았다 → 발사하지 않고 조준 유지(U-5). 직사각형은 그 자리에 남고, 다시 누르면 시작점을 옮긴다(U-6).
                ApplyCancelHover(false);
                LogReleaseResolved("NoFire", "BelowThreshold", false);
                return;
            }

            Vector3 anchor = _lineAnchorDomain;
            Vector3 direction = _lineDirectionDomain;
            LogReleaseResolved("Fire", "Released", false);

            // 조준 종료(플래그 · 표시 정리)를 먼저 한다 — 콜백에서 패널을 닫아도 안전(원형과 같은 순서).
            EndAim();
            _onLineConfirm?.Invoke(_buildingId, _skillSlot, anchor, direction);
        }

        // ====================================================================
        // [2026-11-17 L-3] 포인터 모으기 — 터치는 터치 Id 별로, 마우스는 왼쪽 버튼 하나를 손가락처럼
        // ====================================================================

        /// <summary>
        /// 이번 프레임에 "눌려 있는" 포인터를 목록에 모은다(떨어진 손가락은 넣지 않는다 — 그 좌표는 믿을 수 없다, 규칙 20-1).
        /// 터치: Touchscreen.touches 의 각 손가락(터치 Id 로 구분 — primaryTouch 는 읽지 않는다, R-16).
        /// 마우스: 왼쪽 버튼만(오른쪽 버튼 끌기는 카메라 팬 — U-11, 조준에 쓰지 않는다). 터치를 본 조준에서는 마우스를 읽지 않는다.
        /// </summary>
        private void GatherPressedPointers(List<AimPointerSample> list)
        {
            list.Clear();

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (!t.press.isPressed) continue;
                    list.Add(new AimPointerSample(t.touchId.ReadValue(), t.position.ReadValue()));
                }
                if (list.Count > 0) _touchSeenThisAim = true;
            }

            // 터치 기기에서는 마우스를 섞지 않는다(합성 마우스 좌표 오염 방지 — 위 _touchSeenThisAim 설명).
            if (_touchSeenThisAim) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                list.Add(new AimPointerSample(SkillAimPointerTracker.MousePointerId, mouse.position.ReadValue()));
        }

        // ====================================================================
        // [2026-11-17 L-3] 진단 로그 [SKILL-AIM] — 개발 축(GameLog.Dev = [Conditional], 릴리스에서 사라짐)
        //   손가락이 닿고 떨어지는 순간에만 1줄. 드래그 중 매 프레임 남기지 않는다(LogRules 금지 8).
        //   길이 · 거리 · 좌표는 밀리 정수(값 × 1000), 좌표는 도메인 기준(빨강 화면 Client 도 서버와 같은 기준).
        // ====================================================================

        private static int Milli(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0;
            return Mathf.RoundToInt(v * 1000f);
        }

        private string ModeLabel => _mode == AimMode.Line ? "Line" : "Circle";

        /// <summary> 조준 시작 1줄(L-L1 · L-L8 · L-L18). </summary>
        private void LogAimBegin()
        {
            GameLog.Dev.Info("Skill", nameof(SkillAimController), "[SKILL-AIM] AimBegin",
                FormattableString.Invariant(
                    $"Diag=SkillAim, DiagRev=1, Event=AimBegin, Mode={ModeLabel}, BuildingId={_buildingId}, Slot={_skillSlot}, EdgeScroll={(_mode == AimMode.Line ? "Off" : "On")}, LineShown=False, RadiusMilli={Milli(_radius)}, LengthMilli={Milli(_mode == AimMode.Line ? _lineLength : 0f)}, WidthMilli={Milli(_mode == AimMode.Line ? _lineWidth : 0f)}, ThresholdMilli={Milli(_mode == AimMode.Line ? LineMinDragWorld : 0f)}"));
        }

        /// <summary> 공습 시작점 지정 1줄(L-L2). </summary>
        private void LogAnchorSet(bool retouch, float effLength)
        {
            GameLog.Dev.Info("Skill", nameof(SkillAimController), "[SKILL-AIM] AimAnchorSet",
                FormattableString.Invariant(
                    $"Diag=SkillAim, DiagRev=1, Event=AimAnchorSet, Mode=Line, BuildingId={_buildingId}, Slot={_skillSlot}, Touch={(retouch ? "Retouch" : "First")}, AnchorXmm={Milli(_lineAnchorDomain.x)}, AnchorZmm={Milli(_lineAnchorDomain.z)}, DirXMilli={Milli(_lineDirectionDomain.x)}, DirZMilli={Milli(_lineDirectionDomain.z)}, EffLengthMilli={Milli(effLength)}, LineShown=True, Flipped={ViewConverter.IsFlipped}"));
        }

        /// <summary> 조준 중 손가락 수 변화 1줄(L-L27 · L-L29). </summary>
        private void LogFingerCountChanged(AimPointerFrame frame)
        {
            string lifted = frame.CountTo < frame.CountFrom ? (frame.TrackedLifted ? "Tracked" : "Other") : "None";
            bool edgeScrollOn = _mode == AimMode.Circle && _pointerTracker.HasTracked && frame.CountTo == 1;
            int trackedId = frame.TrackedId;
            GameLog.Dev.Info("Skill", nameof(SkillAimController), "[SKILL-AIM] AimFingerCountChanged",
                FormattableString.Invariant(
                    $"Diag=SkillAim, DiagRev=1, Event=AimFingerCountChanged, Mode={ModeLabel}, BuildingId={_buildingId}, Slot={_skillSlot}, From={frame.CountFrom}, To={frame.CountTo}, TrackedId={trackedId}, Lifted={lifted}, Tracking={_pointerTracker.HasTracked}, Wait={_pointerTracker.WaitReason}, EdgeScroll={(edgeScrollOn ? "On" : "Off")}, AimXmm={Milli(_lastValidDomain.x)}, AimZmm={Milli(_lastValidDomain.z)}, AnchorXmm={Milli(_lineAnchorDomain.x)}, AnchorZmm={Milli(_lineAnchorDomain.z)}, DirXMilli={Milli(_lineDirectionDomain.x)}, DirZMilli={Milli(_lineDirectionDomain.z)}, DragMilli={Milli(_lineDragDistance)}"));
        }

        /// <summary> 접촉이 끝날 때의 판정 1줄(L-L3 · L-L4 · L-L5 · L-L18 · L-L25 · L-L28). </summary>
        /// <param name="result">Fire · NoFire · Cancel.</param>
        /// <param name="reason">Released · BelowThreshold · TrackedLiftedFirst · LiftedTogether · OverCancel · NoValidCoord.</param>
        /// <param name="overCancel">손을 뗀 자리가 X 위였는가.</param>
        private void LogReleaseResolved(string result, string reason, bool overCancel)
        {
            if (_mode == AimMode.Line)
            {
                float eff = _lineAnchorSet ? ComputeLineEffectiveLength() : 0f;
                GameLog.Dev.Info("Skill", nameof(SkillAimController), "[SKILL-AIM] AimReleaseResolved",
                    FormattableString.Invariant(
                        $"Diag=SkillAim, DiagRev=1, Event=AimReleaseResolved, Mode=Line, BuildingId={_buildingId}, Slot={_skillSlot}, Result={result}, Reason={reason}, OverCancel={overCancel}, AnchorSet={_lineAnchorSet}, DragMilli={Milli(_lineDragDistance)}, ThresholdMilli={Milli(LineMinDragWorld)}, AnchorXmm={Milli(_lineAnchorDomain.x)}, AnchorZmm={Milli(_lineAnchorDomain.z)}, DirXMilli={Milli(_lineDirectionDomain.x)}, DirZMilli={Milli(_lineDirectionDomain.z)}, LengthMilli={Milli(_lineLength)}, EffLengthMilli={Milli(eff)}, WidthMilli={Milli(_lineWidth)}, Flipped={ViewConverter.IsFlipped}"));
                return;
            }
            GameLog.Dev.Info("Skill", nameof(SkillAimController), "[SKILL-AIM] AimReleaseResolved",
                FormattableString.Invariant(
                    $"Diag=SkillAim, DiagRev=1, Event=AimReleaseResolved, Mode=Circle, BuildingId={_buildingId}, Slot={_skillSlot}, Result={result}, Reason={reason}, OverCancel={overCancel}, HasCoord={_hasValidCoord}, AimXmm={Milli(_lastValidDomain.x)}, AimZmm={Milli(_lastValidDomain.z)}, Flipped={ViewConverter.IsFlipped}"));
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
            _pendingButtonRelease = false;
            _dragging = false;
            _reticle?.Hide();
            // [2026-11-17 L-3 · L-4] 공습 표시 · 모드 · 손가락 추적 정리.
            IsLineAim = false;
            if (_lineReticle != null) _lineReticle.Hide();
            _pointerTracker.Reset(waitForButtonRelease: false);
            // 취소 버튼 스케일 즉시 기준값으로 원복(잔여 없게) 후 숨김(규칙 20·20-1).
            _cancelTargetScale = _cancelBaseScale;
            if (_cancelZone != null) _cancelZone.localScale = _cancelBaseScale;
            SetCancelButtonVisible(false);
        }

        // ====================================================================
        // 기본 좌표 / 조준점 표시 / 취소 버튼 hover
        // ====================================================================

        /// <summary>
        /// 조준 진입 시 조준 원의 기본 위치(도메인 월드, 연속)를 정한다 — 화면 중앙이 맵 안이면 그 연속 좌표,
        /// 맵 밖이면 <paramref name="fallbackDomain"/>(보통 시전 건물 위치)를 쓴다.
        /// </summary>
        private Vector3 ResolveDefaultDomain(Vector3 fallbackDomain)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return fallbackDomain;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector3 viewPos = ScreenToXZPlane(screenCenter);
            Vector3 domainWorld = ViewConverter.FromView(viewPos);

            // 화면 중앙이 맵 안이면 그 연속 좌표를, 밖이면 시전 건물 위치를 기본값으로 쓴다.
            return (_isWithinBounds == null || _isWithinBounds(domainWorld)) ? domainWorld : fallbackDomain;
        }

        /// <summary>
        /// 지정 도메인 월드 좌표(연속)를 마지막 조준 좌표로 삼고 조준 원을 그 위치(뷰 좌표)에 표시한다.
        /// </summary>
        private void ShowReticleAtDomain(Vector3 domain)
        {
            _lastValidDomain = domain;
            _hasValidCoord = true;
            if (_reticle != null)
            {
                Vector3 view = ViewConverter.ToView(domain);
                _reticle.Show(view, _radius);
            }
        }

        /// <summary>
        /// 취소(X) 버튼 확대의 "목표 스케일"만 설정한다(규칙 20-1). 실제 스케일은 TickCancelHover가 매 프레임
        /// 목표로 부드럽게 보간한다(즉시 적용 아님). over=true면 기준×hover배, false면 기준으로 복귀.
        /// </summary>
        private void ApplyCancelHover(bool over)
        {
            _cancelTargetScale = over ? _cancelBaseScale * _cancelHoverScale : _cancelBaseScale;
        }

        /// <summary>
        /// 취소(X) 버튼 스케일을 목표(_cancelTargetScale)로 프레임 독립 보간한다.
        /// t = 1 - exp(-speed·dt) 는 프레임레이트에 무관하게 같은 감쇠(부드러운 확대/복귀)를 준다.
        /// </summary>
        private void TickCancelHover()
        {
            if (_cancelZone == null) return;
            float t = 1f - Mathf.Exp(-_cancelHoverLerpSpeed * Time.deltaTime);
            _cancelZone.localScale = Vector3.Lerp(_cancelZone.localScale, _cancelTargetScale, t);
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
        //   [2026-11-17 L-3] 아래 4개(TryGetPointerScreenPos · WasPointerReleasedThisFrame · WasPointerPressedThisFrame ·
        //   IsPointerPressed)는 primaryTouch 하나만 읽는 옛 판정용이라 지금은 호출하는 곳이 없다(Update 의 주석 처리된 옛 코드만 썼다).
        //   손가락 판정은 GatherPressedPointers + SkillAimPointerTracker 가 한다. 기존 로직 제거 규칙(WORKFLOW [4])에 따라
        //   사용자 테스트 통과 뒤 최종 삭제 여부를 묻기 위해 지우지 않고 남겨 둔다.
        // ====================================================================

        /// <summary>
        /// 현재 포인터(터치 우선, 없으면 마우스)의 스크린 좌표를 "신뢰할 수 있을 때만" 반환한다.
        /// 반환값 true면 <paramref name="screenPos"/>가 유효한 좌표, false면 신뢰할 수 없어 호출부가
        /// 캐시(마지막 유효 좌표)로 대체해야 한다.
        ///
        /// 왜 Try 형태인가:
        ///   실기기(마우스 없음)에서 손을 뗀 프레임에는 primaryTouch.press.isPressed가 false가 되어
        ///   터치 좌표를 신뢰할 수 없다. 예전처럼 무조건 (0,0)을 돌려주면 그 프레임의 취소/발동 판정이
        ///   화면 좌하단(0,0) 기준으로 잘못 계산된다. 그래서 "유효할 때만 true"로 명확히 구분한다.
        ///   - 터치가 눌린 상태  → 터치 좌표(유효).
        ///   - 마우스 존재(에디터) → 마우스 좌표는 버튼과 무관하게 항상 유효하므로 그대로 사용.
        ///   - 그 외(실기기 release 등) → false(캐시로 대체).
        /// </summary>
        private bool TryGetPointerScreenPos(out Vector2 screenPos)
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                screenPos = touch.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                // 마우스 좌표는 버튼을 떼도 항상 현재 커서 위치가 유효하다(에디터 정상 동작 유지).
                screenPos = mouse.position.ReadValue();
                return true;
            }

            screenPos = Vector2.zero;
            return false;
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
        /// 이번 프레임에 포인터(터치/마우스 왼쪽 버튼)를 새로 눌렀는지 여부.
        /// </summary>
        private bool WasPointerPressedThisFrame()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            return false;
        }

        /// <summary>
        /// 지금 포인터(터치/마우스 왼쪽 버튼)가 눌린 상태인지 여부.
        /// 버튼 탭 gesture가 끝났는지(손을 뗐는지) 판정하는 데 쓴다.
        /// </summary>
        private bool IsPointerPressed()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
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
            if (_isAiming) EndAim();
        }
    }
}
