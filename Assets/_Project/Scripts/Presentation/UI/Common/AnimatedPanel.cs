// ============================================================================
// AnimatedPanel.cs
// 팝업/패널 GameObject에 직접 부착하여 등장/퇴장 애니메이션을 자동 처리하는 컴포넌트.
//
// 역할:
//   - Show()/Hide() 호출만으로 UIAnimator의 애니메이션이 적용됨.
//   - Inspector에서 애니메이션 타입(PopupFade / SlideFromBottom / SlideFromTop)과 파라미터를 설정.
//   - CanvasGroup이 없으면 Awake()에서 자동 추가 — 수동 추가 불필요.
//
// 사용 방법:
//   1. 팝업/패널 GameObject에 이 컴포넌트를 부착.
//   2. Inspector에서 AnimationType 선택:
//      - PopupFade: 스케일 0.85→1 + 페이드인 (일반 팝업에 적합)
//      - SlideFromBottom: 하단에서 위로 슬라이드 + 페이드인 (하단 패널에 적합)
//      - SlideFromTop: 상단에서 아래로 슬라이드 + 페이드인 (게임 종료 패널 등)
//   3. 코드에서 Show()/Hide()만 호출하면 됨.
//
// 주의:
//   - 오브젝트는 항상 active 상태를 유지한다(SetActive 사용 안 함, 공통 UI 규칙 5).
//     초기 숨김은 EnsureInitialized()에서 CanvasGroup(alpha=0/blocksRaycasts=false/interactable=false)으로 처리.
//   - _currentSeq?.Kill()로 진행 중인 애니메이션을 항상 정리 후 새 애니메이션 시작.
//     (Show→Hide 빠른 전환 시 이전 애니메이션이 남아있으면 상태 꼬임 방지)
//   - IsVisible 프로퍼티로 현재 표시 상태를 확인 가능.
//     (activeSelf 대신 사용 — 애니메이션 중에도 정확한 논리적 상태 반환)
//
// 씬 계층 예시:
//   [UI] Canvas
//     └─ ProductionPopup ← AnimatedPanel 부착
//         ├─ Background
//         └─ ContentPanel
//
// Presentation 레이어 — MonoBehaviour + DOTween 의존.
// ============================================================================

using DG.Tweening;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 팝업/패널 오브젝트에 부착하여 Show()/Hide()로 DOTween 등장/퇴장 애니메이션을 제공.
    /// Inspector에서 애니메이션 타입과 파라미터를 설정.
    /// </summary>
    public class AnimatedPanel : MonoBehaviour
    {
        // ====================================================================
        // 애니메이션 타입 열거형
        // ====================================================================

        /// <summary>
        /// 패널 등장/퇴장 애니메이션 타입.
        /// PopupFade: 스케일+페이드 (일반 팝업용).
        /// SlideFromBottom: 하단 슬라이드+페이드 (하단 패널용).
        /// SlideFromTop: 상단 슬라이드+페이드 (상단 패널/게임 종료 결과 등).
        /// </summary>
        public enum AnimationType
        {
            /// <summary>스케일 0.85→1 + 알파 0→1, Ease.OutBack. 일반 팝업에 적합.</summary>
            PopupFade,
            /// <summary>하단에서 위로 슬라이드 + 알파 0→1. 하단 패널/토스트에 적합.</summary>
            SlideFromBottom,
            /// <summary>상단에서 아래로 슬라이드 + 알파 0→1. 게임 종료 패널/상단 알림에 적합.</summary>
            SlideFromTop
        }

        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("애니메이션 설정")]
        [Tooltip("등장/퇴장 애니메이션 타입. PopupFade=스케일+페이드, SlideFromBottom=하단슬라이드+페이드, SlideFromTop=상단슬라이드+페이드.")]
        [SerializeField] private AnimationType _animationType = AnimationType.PopupFade;

        [Tooltip("등장 애니메이션 지속 시간 (초). 짧을수록 빠르게 나타남.")]
        [SerializeField] private float _showDuration = 0.2f;

        [Tooltip("퇴장 애니메이션 지속 시간 (초). 등장보다 약간 짧은 것이 자연스러움.")]
        [SerializeField] private float _hideDuration = 0.15f;

        [Tooltip("SlideFromBottom/SlideFromTop 전용: 시작/끝 위치의 Y 오프셋(px). 기본 300px.")]
        [SerializeField] private float _slideOffset = 300f;

        [Header("배경 오버레이 (선택적)")]
        [Tooltip("슬라이드 패널 뒤에 깔리는 반투명 배경 CanvasGroup. " +
                 "연결 시 Show()에서 즉시 alpha=1/입력차단 ON, Hide() 호출 즉시 alpha=0/입력차단 OFF. " +
                 "(SetActive 대신 CanvasGroup으로 제어 — 공통 UI 규칙 5) " +
                 "연결하지 않으면 무시됨 — 기존 동작에 영향 없음.")]
        [SerializeField] private CanvasGroup _backgroundOverlay;

        // ====================================================================
        // 내부 캐시
        // ====================================================================

        /// <summary>
        /// 알파/레이캐스트 제어용 CanvasGroup.
        /// EnsureInitialized()에서 자동 취득 또는 추가됨 — Inspector에서 별도 연결 불필요.
        /// </summary>
        private CanvasGroup _cg;

        /// <summary>
        /// SlideFromBottom 타입에서 anchoredPosition 조작에 사용하는 RectTransform.
        /// EnsureInitialized()에서 자동 캐시.
        /// </summary>
        private RectTransform _rt;

        /// <summary>
        /// 현재 재생 중인 DOTween Sequence.
        /// Show/Hide 전환 시 Kill()하여 이전 애니메이션을 즉시 정리.
        /// null이면 애니메이션 미재생 상태.
        /// </summary>
        private Sequence _currentSeq;

        /// <summary>
        /// 초기화 완료 여부. Awake()와 Show()/Hide() 중 먼저 호출되는 쪽에서 초기화.
        /// 오브젝트가 비활성 상태에서 시작하면 Awake()가 호출되지 않으므로
        /// Show()/Hide()에서 지연 초기화(lazy init)로 보장.
        /// </summary>
        private bool _initialized;

        // ====================================================================
        // 공개 프로퍼티
        // ====================================================================

        /// <summary>
        /// 패널이 현재 보이는 상태인지 여부.
        /// gameObject.activeSelf 대신 이 프로퍼티를 사용해야 함:
        /// Hide 애니메이션 중에는 activeSelf=true이지만 논리적으로는 숨김 상태.
        /// </summary>
        public bool IsVisible { get; private set; }

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// CanvasGroup/RectTransform 캐시 + 초기 상태 설정.
        /// Awake()에서 호출되지만, 오브젝트가 비활성 상태에서 시작하면
        /// Awake()가 실행되지 않으므로 Show()/Hide()에서도 호출하여 지연 초기화 보장.
        /// 중복 호출에 안전 (_initialized 플래그로 1회만 실행).
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            // CanvasGroup이 없으면 자동 추가 — 알파/레이캐스트 제어에 필수
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null)
                _cg = gameObject.AddComponent<CanvasGroup>();

            // RectTransform 캐시 (UI 오브젝트이므로 항상 존재)
            _rt = GetComponent<RectTransform>();

            // 초기 상태: 숨김.
            // 오브젝트는 항상 active 상태이므로(SetActive 사용 안 함, 공통 UI 규칙 5),
            // 초기 숨김을 SetActive(false)에 의존하지 않고 CanvasGroup 값으로 직접 설정한다.
            //   - alpha = 0           : 완전 투명(화면에 안 보임)
            //   - blocksRaycasts=false: 투명한 영역이 뒤쪽 UI의 클릭을 가로채지 않도록 차단 해제
            //   - interactable=false  : 패널 내부 버튼/입력이 비활성 상태에서 반응하지 않도록 차단
            // 이후 Show()가 호출되면 UIAnimator가 위 값들을 다시 켜준다.
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            IsVisible = false;
        }

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 패널 등장. 애니메이션 타입에 따라 UIAnimator 메서드를 호출.
        /// 이미 Show 상태여도 다시 호출 가능 — 진행 중인 애니메이션을 Kill 후 새로 시작.
        /// </summary>
        public void Show()
        {
            // 비활성 상태에서 호출될 수 있으므로 지연 초기화 보장
            EnsureInitialized();

            // 진행 중인 애니메이션이 있으면 즉시 정리 (Show→Show, Hide→Show 빠른 전환 대응)
            _currentSeq?.Kill();
            IsVisible = true;

            // 배경 오버레이가 Inspector에 연결된 경우 애니메이션 없이 즉시 표시.
            // 패널 슬라이드/페이드 애니메이션과 별개로 배경만 바로 표시하여
            // 사용자가 패널 등장 시작과 동시에 뒤쪽 UI 조작을 차단하는 역할.
            // SetActive 대신 CanvasGroup 값으로 제어한다(공통 UI 규칙 5):
            //   - alpha = 1            : 반투명 배경을 즉시 보이게 함
            //   - blocksRaycasts=true  : 뒤쪽 UI 클릭을 차단
            //   - interactable=true    : (오버레이 자체에 상호작용 요소가 있을 경우 대비) 입력 허용
            // _backgroundOverlay == null이면 이 블록은 건너뛰어 기존 동작과 동일.
            if (_backgroundOverlay != null)
            {
                _backgroundOverlay.alpha = 1f;
                _backgroundOverlay.blocksRaycasts = true;
                _backgroundOverlay.interactable = true;
            }

            // 애니메이션 타입에 따라 분기.
            // 각 UIAnimator 메서드는 SetActive를 호출하지 않고 CanvasGroup(alpha/raycast/interactable)만 제어한다.
            switch (_animationType)
            {
                case AnimationType.PopupFade:
                    _currentSeq = UIAnimator.PopupShow(_cg, transform, _showDuration);
                    break;

                case AnimationType.SlideFromBottom:
                    _currentSeq = UIAnimator.SlideInFromBottom(_rt, _cg, _slideOffset, _showDuration);
                    break;

                case AnimationType.SlideFromTop:
                    _currentSeq = UIAnimator.SlideInFromTop(_rt, _cg, _slideOffset, _showDuration);
                    break;
            }
        }

        /// <summary>
        /// 패널 퇴장. 애니메이션 완료 후 CanvasGroup으로 숨김 처리 + onComplete 콜백 실행.
        /// (SetActive(false)를 호출하지 않고 alpha/raycast/interactable로만 숨김 — 공통 UI 규칙 5)
        /// 이미 숨김 상태면 아무 동작도 하지 않음 (중복 호출 방어).
        /// </summary>
        /// <param name="onComplete">퇴장 애니메이션 완료 후 실행할 콜백. null 허용.</param>
        public void Hide(System.Action onComplete = null)
        {
            // 비활성 상태에서 호출될 수 있으므로 지연 초기화 보장
            EnsureInitialized();

            // 이미 숨김 상태 → 아무것도 하지 않음 (중복 Hide 방어)
            if (!IsVisible) return;

            IsVisible = false;

            // 진행 중인 애니메이션 정리
            _currentSeq?.Kill();

            // 배경 오버레이가 Inspector에 연결된 경우:
            // Hide() 호출 즉시 배경을 숨겨, 패널 퇴장 슬라이드 애니메이션이
            // 시작되는 순간부터 뒤쪽 UI가 보이도록 함.
            // (이전에는 애니메이션 완료 후에 숨겨서 배경이 너무 오래 남아있었음)
            // SetActive 대신 CanvasGroup 값으로 제어한다(공통 UI 규칙 5):
            //   - alpha = 0            : 배경을 즉시 투명하게
            //   - blocksRaycasts=false : 뒤쪽 UI 클릭 차단 해제
            //   - interactable=false   : 오버레이 입력 차단
            // _backgroundOverlay == null이면 이 블록은 건너뛰어 기존 동작과 동일.
            if (_backgroundOverlay != null)
            {
                _backgroundOverlay.alpha = 0f;
                _backgroundOverlay.blocksRaycasts = false;
                _backgroundOverlay.interactable = false;
            }

            // wrappedComplete: switch 문에 전달할 완료 콜백.
            // 배경 숨김은 이미 위에서 즉시 처리했으므로 onComplete만 그대로 전달.
            System.Action wrappedComplete = onComplete;

            // 각 UIAnimator 메서드는 OnComplete에서 SetActive 대신
            // blocksRaycasts/interactable=false로 입력을 차단한다(오브젝트는 active 유지).
            switch (_animationType)
            {
                case AnimationType.PopupFade:
                    _currentSeq = UIAnimator.PopupHide(_cg, transform, wrappedComplete, _hideDuration);
                    break;

                case AnimationType.SlideFromBottom:
                    _currentSeq = UIAnimator.SlideOutToBottom(_rt, _cg, wrappedComplete, _slideOffset, _hideDuration);
                    break;

                case AnimationType.SlideFromTop:
                    _currentSeq = UIAnimator.SlideOutToTop(_rt, _cg, wrappedComplete, _slideOffset, _hideDuration);
                    break;
            }
        }

        /// <summary>
        /// 오브젝트 파괴 시 진행 중인 DOTween Sequence 정리.
        /// 씬 전환 시 파괴되는 오브젝트의 Tween이 남아있으면 에러 발생 방지.
        /// </summary>
        private void OnDestroy() => _currentSeq?.Kill();
    }
}
