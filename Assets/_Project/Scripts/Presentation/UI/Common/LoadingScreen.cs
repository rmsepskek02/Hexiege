// ============================================================================
// LoadingScreen.cs
// 전역 로딩 스크린. 씬 전환 시 화면을 덮고 페이드 인/아웃 처리.
//
// 역할:
//   - 전체 화면 검정 오버레이 + 스피너 + 상태 텍스트
//   - Show() 호출 시 페이드 인, 씬 로드 완료 시 자동 페이드 아웃
//   - DontDestroyOnLoad 싱글턴 — 씬 전환에도 유지
//   - Lobby 씬에 배치, SerializeField로 하위 요소 참조
//
// UI 구조 (Lobby 씬 배치):
//   LoadingScreen (Canvas, Screen Space - Overlay, Sort Order: 999)
//   ├── CanvasGroup (페이드 제어)
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
    /// Lobby 씬에 배치되며, DontDestroyOnLoad로 씬 전환 간 유지.
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
