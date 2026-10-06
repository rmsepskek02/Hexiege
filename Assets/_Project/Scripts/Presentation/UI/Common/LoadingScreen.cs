// ============================================================================
// LoadingScreen.cs
// 전역 로딩 스크린. 씬 전환 시 화면을 덮고 페이드 인/아웃 처리.
//
// ⚠️ 지금 이 컴포넌트는 어느 씬·어느 프리팹에도 붙어 있지 않고, 부르는 코드도 없다
//    (2026-10-02 실측 — 이 스크립트의 guid 가 프로젝트 전체에서 자기 .meta 파일에만 나온다).
//    따라서 정적 인스턴스 참조는 런타임에 항상 비어 있는 상태로 남는다.
//    씬 전환 중의 로딩 표시는 공용 UI 관리자가 들고 있는 별도의 로딩 표시 오브젝트가 맡고 있다.
//    🔴 그러므로 아래 「역할」과 「UI 구조」는 <이 컴포넌트를 실제로 쓰려면 갖춰야 하는 형태>이며,
//       지금 씬이 그렇게 생겼다는 뜻이 아니다. 쓰려면 씬에 배치하고 하위 요소를 연결해야 한다.
//
// 역할(쓰기로 결정했을 때):
//   - 전체 화면 검정 오버레이 + 스피너 + 상태 텍스트
//   - Show() 호출 시 페이드 인, 씬 로드 완료 시 자동 페이드 아웃
//   - DontDestroyOnLoad 싱글턴 — 씬 전환에도 유지
//   - 씬에 배치하고 SerializeField로 하위 요소를 연결해 두어야 동작한다
//
// UI 구조 (배치할 때 갖춰야 하는 계층):
//   LoadingScreen (Canvas, Screen Space - Overlay)
//      · Sort Order 는 임의의 큰 값을 쓰지 말고, Canvas SortingOrder 규칙 문서가 정해 둔
//        「로딩」 대역 값을 그대로 쓴다(대역 밖의 값을 쓰면 그 규칙이 깨진다).
//      · CanvasGroup 은 자식 오브젝트가 아니라 이 루트에 붙이는 컴포넌트다.
//        페이드는 이 CanvasGroup 의 alpha 로 하위 전체를 한꺼번에 처리한다.
//   └── RootPanel (Image, 전체화면 검정)
//       ├── Spinner (Image, 화면 중앙, Z축 회전)
//       └── StatusText (TextMeshProUGUI, 스피너 하단)
//
// Presentation 레이어.
// ============================================================================

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hexiege.Presentation.UI
{
    /// <summary>
    /// 전역 로딩 스크린 싱글턴. 씬 전환 구간에서 페이드 인/아웃 오버레이 표시.
    /// 배치하면 DontDestroyOnLoad로 씬 전환 간 유지된다.
    /// ⚠️ 어느 씬에도 배치돼 있지 않은 상태다 — 파일 머리말의 ⚠️ 항목 참조.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        // ====================================================================
        // 싱글턴
        // ====================================================================

        /// <summary>전역 LoadingScreen 인스턴스.</summary>
        public static LoadingScreen Instance { get; private set; }

        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private RectTransform _spinner;

        // ====================================================================
        // 설정
        // ====================================================================

        [Header("애니메이션")]
        [SerializeField] private float _fadeDuration = 0.3f;
        [SerializeField] private float _spinSpeed = 180f;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // 싱글턴 설정 — 중복 인스턴스 파괴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 초기 상태: 숨김
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // 스피너 Z축 회전 — alpha > 0일 때만 (불필요한 연산 방지)
            if (_spinner != null && _canvasGroup != null && _canvasGroup.alpha > 0f)
                _spinner.Rotate(Vector3.forward, -_spinSpeed * Time.deltaTime);
        }

        // ====================================================================
        // 공개 API
        // ====================================================================

        /// <summary>
        /// 로딩 스크린 표시. 페이드 인 + 상태 메시지 설정.
        /// </summary>
        /// <param name="message">표시할 상태 메시지.</param>
        public void Show(string message)
        {
            if (_canvasGroup == null) return;

            if (_statusText != null)
                _statusText.text = message;

            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            _canvasGroup.DOKill();
            _canvasGroup.DOFade(1f, _fadeDuration);
        }

        /// <summary>
        /// 로딩 스크린 숨기기. 페이드 아웃 후 입력 차단 해제.
        /// </summary>
        public void Hide()
        {
            if (_canvasGroup == null) return;

            _canvasGroup.DOKill();
            _canvasGroup.DOFade(0f, _fadeDuration)
                .OnComplete(() =>
                {
                    if (_canvasGroup != null)
                    {
                        _canvasGroup.interactable = false;
                        _canvasGroup.blocksRaycasts = false;
                    }
                });
        }

        // ====================================================================
        // 내부 로직
        // ====================================================================

        /// <summary>
        /// 씬 로드 완료 시 자동으로 로딩 스크린 숨기기.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Hide();
        }
    }
}
