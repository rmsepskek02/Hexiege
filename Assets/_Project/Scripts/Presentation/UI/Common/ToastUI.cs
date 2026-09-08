// ============================================================================
// ToastUI.cs
// 화면 하단 중앙에 짧은 알림(토스트) 메시지를 표시하는 UI.
//
// ─────────────────────────────────────────────────────────────────────────────
// 역할:
//   - 어느 씬에서든 사용자에게 즉시 알려야 하는 상황(골드 부족, 인구 가득 등)을
//     화면 하단에 잠깐 보여주고 자동으로 사라지도록 한다.
//   - 동시에 여러 요청이 와도 큐에 쌓아 차례대로 한 번에 하나씩 표시한다.
//   - 단, "같은 종류"의 요청이 연달아 들어오면 쌓지 않고 하나로 합친다(아래 규칙 2 참조).
//
// ─────────────────────────────────────────────────────────────────────────────
// 동작 규칙:
//   1) Show(ToastKey)로 호출 → 큐에 추가.
//   2) 【중복 방지】 같은 키(ToastKey)가 이미 표시 중이거나 큐에서 대기 중이면 새로 쌓지 않는다.
//      - 표시 중인 키와 같으면 → 큐에 넣지 않고 "잔여 표시 시간만" 처음 값으로 되돌린다.
//        (버튼을 다시 눌렀다는 반응은 유지되면서, 같은 메시지가 여러 번 반복되지는 않는다)
//      - 큐에서 대기 중인 키와 같으면 → 이미 곧 표시될 예정이므로 그냥 무시한다.
//      - 서로 다른 키는 예전과 동일하게 전부 순서대로 표시된다.
//   3) 표시 중인 토스트가 없으면 즉시 큐의 첫 항목을 띄움(진입 애니메이션 없음 = 즉시 노출).
//   4) ToastMessageConfig에서 설정된 duration이 지나면 DOTween 페이드아웃으로 사라짐.
//   5) 페이드아웃 완료 즉시 다음 큐 항목을 띄움.
//   6) 사용자가 토스트를 터치하면 현재 메시지만 즉시 제거되고 다음 메시지가 곧바로 표시됨.
//
// ─────────────────────────────────────────────────────────────────────────────
// 정적 진입점:
//   ToastUI.Show(ToastKey.GoldInsufficient) 처럼 어디서든 호출 가능.
//   인스턴스가 없으면 호출이 안전하게 무시된다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 씬 배치 (Lobby.unity 한 번만):
//   첫 씬(Lobby.unity)의 [UI] Canvas 하위에 배치.
//   Awake()에서 DontDestroyOnLoad 처리 → 이후 모든 씬에서 살아있음.
//   ToastMessageConfig는 Resources에서 자동 로드 — Inspector 연결 불필요.
//   씬 전환 시 잔여 토스트는 GameEvents.OnGameStarted / OnGameEnd 구독으로 자동 정리.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, UI, DOTween, UniRx).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using UniRx;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 화면 하단 중앙에 짧은 알림 메시지를 표시하는 토스트 UI.
    /// Lobby.unity에 한 번만 배치하면 DontDestroyOnLoad로 모든 씬에서 동작.
    /// 정적 Show(ToastKey)로 어느 씬에서든 호출 가능.
    /// </summary>
    public class ToastUI : MonoBehaviour, IPointerClickHandler
    {
        // ====================================================================
        // 싱글턴 (DontDestroyOnLoad — 씬 전환 후에도 유지)
        // ====================================================================

        /// <summary>
        /// 현재 씬의 ToastUI 인스턴스.
        /// Initialize() 시 자기 자신을 등록하고, OnDestroy() 시 해제.
        /// 인스턴스가 없을 때 Show()가 호출되어도 안전하게 무시됨.
        /// </summary>
        private static ToastUI _instance;

        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("표시 컴포넌트")]
        [Tooltip("페이드아웃을 위한 CanvasGroup. 토스트 루트 GameObject에 부착.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("메시지 텍스트(TMP).")]
        [SerializeField] private TextMeshProUGUI _messageText;

        [Header("애니메이션")]
        [Tooltip("페이드아웃 시간(초). 표시 시간이 끝난 직후 이 시간 동안 알파 1→0.")]
        [SerializeField, Min(0f)] private float _fadeOutDuration = 0.25f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 대기 중인 토스트 큐. 앞에서부터 차례대로 표시. </summary>
        private readonly Queue<ToastKey> _queue = new Queue<ToastKey>();

        /// <summary> 메시지/표시시간 정보 소스. Initialize에서 주입. </summary>
        private ToastMessageConfig _config;

        /// <summary>
        /// 현재 표시 중인 메시지의 잔여 표시 시간(초).
        /// 0보다 크면 표시 중, 0 이하가 되면 페이드아웃 시작.
        /// </summary>
        private float _remainingDuration;

        /// <summary>
        /// 현재 표시 중인 토스트의 키.
        /// 같은 키가 다시 요청됐는지 판별하는 데 쓴다.
        /// ⚠️ 이 값은 <see cref="_isShowing"/>이 true일 때만 의미가 있다
        ///    (enum이라 null을 담을 수 없으므로, 표시 중이 아닐 때의 값은 의미 없는 잔재다).
        /// </summary>
        private ToastKey _currentKey;

        /// <summary>
        /// 현재 표시 중인 토스트의 "처음" 표시 시간(초).
        /// 같은 키가 다시 요청됐을 때 <see cref="_remainingDuration"/>을 이 값으로 되돌린다.
        /// TryShowNext()에서 Config를 조회할 때 한 번만 저장해 두므로,
        /// 되돌릴 때 Config를 다시 조회할 필요가 없다(조회 코드가 한 곳에만 존재).
        /// </summary>
        private float _currentDuration;

        /// <summary> 현재 진행 중인 페이드아웃 트윈. 새 표시 시 항상 Kill 후 재시작. </summary>
        private Tween _fadeTween;

        /// <summary> 현재 토스트가 화면에 보이고 있는지 여부(논리적 상태). </summary>
        private bool _isShowing;

        /// <summary> 초기화 완료 여부. false면 모든 진입점이 동작하지 않음. </summary>
        private bool _initialized;

        // ====================================================================
        // 정적 진입점
        // ====================================================================

        /// <summary>
        /// 토스트 표시 요청.
        /// 인스턴스가 없거나 아직 초기화되지 않은 상태에서는 안전하게 무시.
        /// </summary>
        /// <param name="key">표시할 토스트 종류.</param>
        public static void Show(ToastKey key)
        {
            // Unity 특수 null 비교 — 파괴된 오브젝트도 안전하게 체크.
            if (_instance == null) return;
            _instance.Enqueue(key);
        }

        // ====================================================================
        // 초기화 (Awake — DontDestroyOnLoad + 자동 설정 로드)
        // ====================================================================

        /// <summary>
        /// Lobby.unity 로드 시 1회 실행.
        /// - 중복 인스턴스가 있으면 자기 자신을 파괴(씬 재진입 방지).
        /// - DontDestroyOnLoad로 이후 모든 씬에서 유지.
        /// - Resources에서 ToastMessageConfig를 자동으로 로드.
        /// - GameEvents 구독으로 게임 시작/종료 시 잔여 토스트를 자동 정리.
        /// </summary>
        private void Awake()
        {
            // 이미 다른 인스턴스가 살아있으면(씬 재로드 등) 자기 자신을 파괴하고 종료.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // DontDestroyOnLoad는 씬 루트 오브젝트(부모 없음)에서만 동작한다.
            // SetupToastUI 스크립트가 Toast를 씬 루트로 생성하지만, 혹시 Canvas 하위에
            // 잘못 배치된 경우에도 안전하게 루트로 분리한 뒤 DontDestroyOnLoad를 적용.
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject); // 씬 전환 후에도 이 오브젝트를 파괴하지 않음

            // Resources 폴더에서 ToastMessageConfig를 자동 로드.
            // "Config/ToastMessageConfig" → Assets/_Project/Resources/Config/ToastMessageConfig.asset
            _config = Resources.Load<ToastMessageConfig>("Config/ToastMessageConfig");
            if (_config == null)
            {
                // [개발] Warn + 개발 — Resources 에셋 누락은 Inspector 배선 누락과 같은 "설정 오류"다.
                //   에셋은 빌드에 함께 들어가므로 있으면 모든 기기에 있고 없으면 모든 기기에 없다
                //   → 축 B ①("플레이어 기기에서만 벌어지는가")이 항상 "아니오" → 개발.
                GameLog.Dev.Warn("UI", nameof(ToastUI),
                                 "Resources/Config/ToastMessageConfig.asset 을 찾을 수 없다 — 에셋 경로를 확인해야 한다",
                                 "Path=Config/ToastMessageConfig");
            }

            _initialized = true;
            ClearAll(); // 초기 상태는 항상 숨김

            // [개발] 둘 다 Warn + 개발.
            //   원본은 LogError 였지만 LogRules 1.3 원칙 3 단서가 Inspector 배선 누락 같은 설정 오류를
            //   Warn + 개발로 낮추라고 규정한다 — 개발자가 빌드 전에 잡는 문제이기 때문이다.
            if (_canvasGroup == null)
            {
                GameLog.Dev.Warn("UI", nameof(ToastUI),
                                 "CanvasGroup 미배선 — Inspector 에서 연결해야 한다",
                                 "Field=_canvasGroup");
            }
            if (_messageText == null)
            {
                GameLog.Dev.Warn("UI", nameof(ToastUI),
                                 "메시지 텍스트 미배선 — Inspector 에서 연결해야 한다",
                                 "Field=_messageText");
            }

            // 게임 시작/종료 이벤트 구독 — 씬 전환 시 이전 게임의 잔여 토스트 자동 정리.
            // AddTo(this): 이 오브젝트가 파괴될 때 구독 자동 해제.
            GameEvents.OnGameStarted.Subscribe(_ => ClearAll()).AddTo(this);
            GameEvents.OnGameEnd.Subscribe(_ => ClearAll()).AddTo(this);

            // 토스트 요청 이벤트 구독.
            // Infrastructure 레이어(NetworkBuildingController 등)가 ToastUI 정적 호출 대신
            // GameEvents.OnToastRequested를 발행해 Presentation 의존을 끊는다.
            GameEvents.OnToastRequested.Subscribe(Enqueue).AddTo(this);
        }

        // ====================================================================
        // 큐 관리
        // ====================================================================

        /// <summary>
        /// 토스트 키를 큐에 추가. 표시 중이 아니면 즉시 다음 항목 처리 시도.
        ///
        /// 【중복 방지】같은 키가 이미 화면에 떠 있거나 큐에서 대기 중이면 새로 쌓지 않는다.
        /// 예전에는 무조건 큐에 넣었기 때문에, 골드가 부족한 상태에서 생산 버튼을 5번 연타하면
        /// 같은 문구가 5번 연달아(1초짜리면 5초 동안) 표시되고, 손을 뗀 뒤에도 계속 밀려 나왔다.
        /// </summary>
        /// <param name="key">표시할 토스트 종류.</param>
        private void Enqueue(ToastKey key)
        {
            if (!_initialized) return;

            // (1) 지금 화면에 떠 있는 토스트와 같은 키라면 → 큐에 넣지 않고 표시 시간만 되돌린다.
            //     "방금 또 눌렀다"는 반응(토스트가 다시 처음부터 유지됨)은 살리면서,
            //     같은 메시지가 여러 번 반복되는 누적은 막는다.
            if (_isShowing && _currentKey == key)
            {
                RestartCurrentDisplay();
                return;
            }

            // (2) 이미 큐에서 순서를 기다리는 키라면 → 곧 표시될 예정이므로 그냥 무시한다.
            //     Queue<T>.Contains는 앞에서부터 훑는 O(n) 연산이지만,
            //     이 중복 방지 덕분에 큐에는 "서로 다른 키"만 들어가므로
            //     길이가 ToastKey 종류 수(현재 6종)를 넘지 못한다 → 성능 문제가 없다.
            if (_queue.Contains(key)) return;

            _queue.Enqueue(key);

            // 현재 보이는 토스트가 없으면 즉시 다음 항목 표시.
            if (!_isShowing) TryShowNext();
        }

        /// <summary>
        /// 이미 표시 중인 토스트를 "처음 뜬 상태"로 되돌린다(같은 키가 다시 요청됐을 때).
        ///
        /// ⚠️ 시간만 되돌리면 안 되는 이유:
        ///    표시 시간이 이미 끝나 페이드아웃(알파 1→0)이 진행 중일 수 있다.
        ///    이때 잔여 시간만 되돌리면 알파가 줄어든 흐릿한 상태로 남거나,
        ///    트윈이 끝나면서 FinishCurrent()가 불려 토스트가 사라져 버린다.
        ///    그래서 트윈을 먼저 Kill로 중단하고 알파를 1로 되살린다.
        ///    (DOTween의 Kill()은 기본값이 complete:false라 OnComplete 콜백을 부르지 않는다.
        ///     즉 FinishCurrent()가 실행되지 않으므로 현재 토스트가 그대로 유지된다.)
        /// </summary>
        private void RestartCurrentDisplay()
        {
            // 진행 중이던 페이드아웃 중단(없으면 아무 일도 일어나지 않음).
            _fadeTween?.Kill();
            _fadeTween = null;

            // 흐릿해진 알파를 다시 완전 불투명으로 되살린다.
            ApplyVisibleState();

            // 잔여 시간을 처음 값으로 리셋 → 마지막 탭 기준으로 duration만큼 더 보인다.
            _remainingDuration = _currentDuration;
        }

        /// <summary>
        /// 토스트를 "보이는 상태"로 만든다(알파 1 + 입력 수신 허용).
        /// 새로 표시할 때와, 같은 키 재요청으로 되살릴 때 양쪽에서 사용한다.
        /// </summary>
        private void ApplyVisibleState()
        {
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true; // 클릭 입력 받기 시작
            _canvasGroup.interactable = true;   // 상호작용 허용
        }

        /// <summary>
        /// 큐에서 다음 토스트를 꺼내 표시 시작.
        /// 큐가 비어있거나 표시 중이면 아무것도 하지 않음.
        /// </summary>
        private void TryShowNext()
        {
            if (_isShowing) return;
            if (_queue.Count == 0) return;

            ToastKey key = _queue.Dequeue();

            // 메시지/시간 조회. Config가 없거나 키가 등록되지 않았어도
            // TryGet 폴백으로 안전한 기본값이 들어오므로 표시는 진행한다.
            string message = key.ToString();
            float duration = 1.5f;
            if (_config != null && _config.TryGet(key, out var entry))
            {
                message = entry.message;
                duration = entry.duration;
            }

            // 텍스트 갱신 + 즉시 표시(진입 애니메이션 없음).
            // SetActive를 사용하지 않는 이유: 루트가 비활성화되면 Update()가 멈춰
            // 큐가 영영 동작하지 않는다. 대신 알파 + 레이캐스트 차단/허용으로 표시 제어.
            if (_messageText != null) _messageText.text = message;
            ApplyVisibleState();

            // 같은 키 재요청 시 시간을 되돌릴 수 있도록 "처음 표시 시간"을 함께 보관한다.
            // Config 조회는 바로 위 한 곳에서만 하고, 그 결과를 여기에 저장해 재사용한다.
            _currentKey = key;
            _currentDuration = duration;

            _remainingDuration = duration;
            _isShowing = true;

            // 진행 중인 페이드아웃 트윈은 명시적으로 제거(빠른 연속 표시 안전 처리).
            _fadeTween?.Kill();
            _fadeTween = null;
        }

        // ====================================================================
        // 매 프레임 갱신
        // ====================================================================

        /// <summary>
        /// 표시 중인 토스트의 잔여 시간을 매 프레임 감소.
        /// 0 이하가 되면 페이드아웃 트윈 시작 → 완료 시 다음 항목 표시.
        /// 일시정지 영향을 받지 않도록 unscaledDeltaTime 사용.
        /// </summary>
        private void Update()
        {
            if (!_isShowing) return;
            if (_fadeTween != null && _fadeTween.IsActive()) return; // 페이드아웃 진행 중

            _remainingDuration -= Time.unscaledDeltaTime;
            if (_remainingDuration <= 0f)
            {
                StartFadeOut();
            }
        }

        /// <summary>
        /// 페이드아웃 시작. 완료 시점에 큐에서 다음 항목으로 자동 전환.
        /// </summary>
        private void StartFadeOut()
        {
            if (_canvasGroup == null)
            {
                // CanvasGroup이 없어도 흐름이 멈추지 않도록 즉시 다음으로.
                FinishCurrent();
                return;
            }

            _fadeTween?.Kill();
            _fadeTween = _canvasGroup
                .DOFade(0f, _fadeOutDuration)
                .SetUpdate(true) // 일시정지 영향 받지 않게
                .OnComplete(FinishCurrent);
        }

        /// <summary>
        /// 현재 토스트 마무리 처리.
        /// 알파 0 + 레이캐스트 차단 후, 큐에 다음 항목이 있으면 즉시 표시.
        /// 루트 GameObject는 항상 활성 상태로 유지(Update()가 멈추면 큐가 정지함).
        /// </summary>
        private void FinishCurrent()
        {
            // _isShowing = false 가 되는 순간 _currentKey / _currentDuration 은 의미를 잃는다.
            // (Enqueue의 중복 판정은 _isShowing이 true일 때만 _currentKey를 보므로,
            //  아래 TryShowNext()가 다음 항목의 값으로 덮어쓰기 전까지 남아 있어도 영향이 없다.)
            _isShowing = false;
            _remainingDuration = 0f;
            _currentDuration = 0f;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false; // 클릭 입력 차단(투명하지만 클릭 막힘 방지)
                _canvasGroup.interactable = false;   // 상호작용 차단
            }
            _fadeTween = null;

            TryShowNext();
        }

        // ====================================================================
        // 터치로 즉시 제거
        // ====================================================================

        /// <summary>
        /// 토스트를 터치하면 현재 메시지만 즉시 제거 + 다음 항목 표시.
        /// IPointerClickHandler 구현 — Image 등 Raycast Target이 있는 UI에 부착.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isShowing) return;
            // 페이드 중이든 표시 중이든 무조건 종료.
            _fadeTween?.Kill();
            _fadeTween = null;
            FinishCurrent();
        }

        // ====================================================================
        // 상태 정리
        // ====================================================================

        /// <summary>
        /// 큐와 현재 표시를 모두 비워 초기 상태로 되돌림.
        /// 게임 시작/종료/재초기화 시 호출.
        /// 루트 GameObject는 항상 활성 상태로 유지해야 한다.
        /// (루트가 비활성화되면 Update()가 멈춰 이후 토스트 큐가 영영 동작하지 않음)
        /// </summary>
        private void ClearAll()
        {
            _queue.Clear();
            _fadeTween?.Kill();
            _fadeTween = null;
            _isShowing = false;
            _remainingDuration = 0f;
            // 중복 판정용 상태도 함께 초기화한다.
            // (_isShowing=false이면 _currentKey는 어차피 참조되지 않지만,
            //  "표시 중인 것이 없다"는 사실이 상태에도 그대로 드러나도록 시간값을 0으로 되돌린다.)
            _currentDuration = 0f;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false; // 클릭 입력 차단
                _canvasGroup.interactable = false;   // 상호작용 차단
            }
        }

        // ====================================================================
        // 라이프사이클
        // ====================================================================

        /// <summary>
        /// 파괴 시 싱글턴 해제 및 트윈 정리.
        /// </summary>
        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this)) _instance = null;
            _fadeTween?.Kill();
            _fadeTween = null;
        }
    }
}
