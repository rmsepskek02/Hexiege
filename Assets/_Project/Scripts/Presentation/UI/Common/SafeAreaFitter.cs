// ============================================================================
// SafeAreaFitter.cs
// ----------------------------------------------------------------------------
// 목적:
//   부착된 GameObject의 RectTransform을 현재 기기의 Safe Area 영역에 맞게
//   자동으로 조정한다. 노치(notch), 펀치홀, 하단 홈바 등 시스템 UI에 가려지는
//   영역을 피해 게임 UI가 표시되도록 만든다.
//
// 사용 방법:
//   1. Canvas 직속 자식으로 빈 GameObject 생성 후 이름을 SafeAreaContainer로 지정.
//   2. RectTransform은 화면 전체로 stretch (anchorMin=0,0 / anchorMax=1,1 / offset=0).
//   3. 해당 GameObject에 이 컴포넌트(SafeAreaFitter)를 추가.
//   4. 실제 UI 요소(팝업, HUD 등)는 SafeAreaContainer의 자식으로 배치.
//
// 동작 원리:
//   - Unity는 Screen.safeArea로 현재 기기의 Safe Area를 픽셀 단위 Rect로 제공한다.
//   - 이 Rect를 화면 해상도(Screen.width / Screen.height)로 나누면 0~1 비율이 된다.
//   - 그 비율을 RectTransform의 anchorMin / anchorMax에 적용하면,
//     Canvas 좌표계에서 Safe Area 영역만 차지하는 컨테이너가 만들어진다.
//   - offsetMin / offsetMax는 0으로 두어, anchor 비율 그대로 영역이 결정되게 한다.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, RectTransform).
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 부착된 RectTransform을 현재 기기의 Screen.safeArea 영역에 맞춰 조정한다.
    /// 모든 UI 요소를 이 컨테이너의 자식으로 배치하면, 노치/홈바 등에 의해
    /// 가려지지 않는 안전한 영역에만 UI가 나타나게 된다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        // ====================================================================
        // 캐시 필드
        // ====================================================================

        /// <summary>
        /// 부착된 GameObject의 RectTransform 캐시.
        /// Awake에서 한 번만 GetComponent 하고, 이후 재사용한다.
        /// </summary>
        private RectTransform _rectTransform;

        /// <summary>
        /// 마지막으로 적용한 Safe Area 값.
        /// 화면 회전이나 멀티태스킹 복귀 시 Safe Area가 바뀔 수 있는데,
        /// 매 프레임 RectTransform을 갱신하면 낭비이므로 변경된 경우에만 적용한다.
        /// </summary>
        private Rect _lastSafeArea;

        /// <summary>
        /// 마지막으로 참조한 화면 해상도.
        /// 해상도가 바뀐 경우에도 Safe Area를 다시 계산해야 한다.
        /// </summary>
        private Vector2Int _lastScreenSize;

        /// <summary>
        /// 마지막으로 참조한 화면 방향.
        /// 가로/세로 전환 시 Safe Area 영역도 함께 변하므로 감지해야 한다.
        /// </summary>
        private ScreenOrientation _lastOrientation;

        // ====================================================================
        // Unity 라이프사이클
        // ====================================================================

        /// <summary>
        /// 컴포넌트가 처음 활성화되기 직전에 호출된다.
        /// RectTransform 참조를 캐싱하고, 첫 Safe Area 적용을 수행한다.
        /// </summary>
        private void Awake()
        {
            // RequireComponent 어트리뷰트로 항상 존재가 보장되지만,
            // 명시적으로 GetComponent 결과를 캐싱해 둔다.
            _rectTransform = GetComponent<RectTransform>();

            // 최초 1회 Safe Area 적용. 이후 Update에서 변경 시에만 재적용.
            ApplySafeArea();
        }

        /// <summary>
        /// 매 프레임 호출. Safe Area / 해상도 / 화면 방향이 바뀐 경우에만 재적용한다.
        /// 변경 감지 비용이 매우 낮으므로 Update에 두어도 성능 부담 없음.
        /// </summary>
        private void Update()
        {
            // 현재 프레임의 환경 값들을 가져온다.
            Rect currentSafeArea = Screen.safeArea;
            Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);
            ScreenOrientation currentOrientation = Screen.orientation;

            // 직전 프레임과 어느 하나라도 다르면 재계산.
            // Rect 비교는 == 연산자로 가능 (Unity 내부적으로 4개 float 비교).
            if (currentSafeArea != _lastSafeArea
                || currentScreenSize != _lastScreenSize
                || currentOrientation != _lastOrientation)
            {
                ApplySafeArea();
            }
        }

        // ====================================================================
        // 내부 로직
        // ====================================================================

        /// <summary>
        /// Screen.safeArea 픽셀 좌표를 0~1 비율의 anchor 값으로 변환하여
        /// RectTransform에 적용한다.
        ///
        /// Screen.safeArea의 좌표계:
        ///   - 좌하단(0,0) 기준 픽셀 단위.
        ///   - 예: 1080×1920 화면, 상단 80px 노치만 있다면
        ///         safeArea = (x=0, y=0, width=1080, height=1840)
        ///
        /// RectTransform anchor 좌표계:
        ///   - 부모 Rect 기준 0~1 비율.
        ///   - 위 예라면 anchorMin=(0,0), anchorMax=(1, 1840/1920) ≒ (1, 0.958)
        /// </summary>
        private void ApplySafeArea()
        {
            // 환경 값 캐싱 (이후 변경 감지에 사용)
            Rect safeArea = Screen.safeArea;
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(screenWidth, screenHeight);
            _lastOrientation = Screen.orientation;

            // 화면 크기가 0이면 (앱 백그라운드 진입 등 비정상 상태) 계산 불가 → 종료.
            // 0으로 나누면 NaN/Infinity가 발생하므로 반드시 가드.
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            // 픽셀 좌표를 0~1 비율로 변환.
            //   anchorMin = Safe Area의 좌하단 꼭짓점 (safeArea.position)
            //   anchorMax = Safe Area의 우상단 꼭짓점 (safeArea.position + safeArea.size)
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= screenWidth;
            anchorMin.y /= screenHeight;
            anchorMax.x /= screenWidth;
            anchorMax.y /= screenHeight;

            // RectTransform에 anchor 비율을 적용.
            // anchor가 0~1 비율로 설정되면, RectTransform은 부모 영역의 그 비율 부분만 차지한다.
            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;

            // offsetMin / offsetMax는 anchor 기준 추가 픽셀 보정.
            // Safe Area 비율 그대로 사용할 것이므로 0으로 초기화하여 보정 제거.
            //   offsetMin = (left,  bottom)
            //   offsetMax = (right, top)   (오른쪽/위쪽은 부호가 반대 방향이므로 0이면 안 늘림)
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
