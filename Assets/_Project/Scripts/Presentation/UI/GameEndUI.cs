// ============================================================================
// GameEndUI.cs
// 게임 종료 시 승리/패배 팝업을 표시하고 다시하기 버튼을 제공.
//
// 역할:
//   1. Initialize()에서 OnGameEnd 이벤트 구독 → 승리/패배 텍스트 표시
//   2. Time.timeScale = 0 으로 게임 일시정지
//   3. 다시하기 버튼 → GameBootstrapper.LoadMap() 호출로 재시작
//
// 씬 구조 (Inspector에서 수동 배치):
//   [UI] Canvas
//     └─ GameEndPanel (비활성 상태)
//         ├─ Background (전체 화면, 반투명 검정)
//         ├─ ResultText (TMP - "승리!" / "패배!")
//         └─ RestartButton (버튼 - "다시하기")
//
// 초기화 방식:
//   GameBootstrapper.LoadMap()에서 Initialize() 호출.
//   다른 UI 컴포넌트(GameHudUI, ProductionPanelUI)와 동일한 패턴.
//   Awake()를 사용하지 않음 — 패널이 비활성 상태로 시작할 수 있으므로.
//
// 플레이어 = Blue 팀 고정.
//   Blue Castle 파괴 = 패배, Red Castle 파괴 = 승리.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Bootstrap;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class GameEndUI : MonoBehaviour, IGameUI
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("UI References")]
        [Tooltip("게임 종료 패널 (AnimatedPanel 부착, Show()/Hide()로 토글)")]
        [SerializeField] private AnimatedPanel _panel;

        [Tooltip("승리/패배 결과 텍스트")]
        [SerializeField] private TextMeshProUGUI _resultText;

        [Tooltip("다시하기 버튼")]
        [SerializeField] private Button _restartButton;

        [Header("Dependencies")]
        [Tooltip("GameBootstrapper (재시작용)")]
        [SerializeField] private GameBootstrapper _bootstrapper;

        // NGO 종료는 NetworkGameManager.BackToLobby()에 위임한다(GameEndUI는 Unity.Netcode를 직접 참조하지 않음).
        // (NetworkGameManager는 Infrastructure 레이어 컴포넌트로 NGO를 안전하게 종료하는 책임을 가짐)
        [Tooltip("네트워크 게임 매니저. 멀티플레이 시 로비 복귀 처리 위임용. 싱글플레이면 null이어도 됨.")]
        [SerializeField] private NetworkGameManager _networkGameManager;

        [Header("로비 복귀")]
        [Tooltip("로비로 돌아가기 버튼")]
        [SerializeField] private Button _backToLobbyButton;

        [Tooltip("자동 복귀 카운트다운 텍스트 (예: '30초 후 로비로 돌아갑니다.')")]
        [SerializeField] private TextMeshProUGUI _countdownText;

        // 공통 UI 규칙 D-4 「자동 로비 복귀 카운트다운 — 기본 60초」.
        // ⚠️ 이 값은 씬(Game.unity)에 직렬화된 Inspector 값이 우선한다.
        //    여기만 고치면 실제 동작은 바뀌지 않으므로 씬 값도 함께 맞춰야 한다.
        [Tooltip("자동 복귀까지 대기 시간 (초). 기본 60초.")]
        [SerializeField] private float _autoReturnSeconds = 60f;

        [Header("다시하기 버튼 텍스트")]
        [Tooltip("다시하기 버튼의 텍스트 컴포넌트. 요청 중 상태 표시용.")]
        [SerializeField] private TextMeshProUGUI _restartButtonText;

        // ====================================================================
        // 상대 이탈 관련 고정값 (공통 UI 규칙 D-1 · D-4)
        //
        // [초급자용 설명] 왜 [SerializeField] 가 아니라 const 인가
        //   [SerializeField] 로 두면 그 값이 씬 파일(Game.unity)에 저장되고,
        //   런타임에는 **씬에 저장된 값이 코드 기본값을 덮어쓴다.**
        //   그래서 코드만 고치면 실제 동작이 바뀌지 않는 함정이 생긴다
        //   (바로 위 _autoReturnSeconds 가 실제로 그 함정에 걸려 코드와 씬을 모두 고쳐야 했다).
        //   아래 두 값은 규칙이 고정한 값이라 Inspector 에서 조절할 이유가 없으므로
        //   const 로 둬서 「코드 한 자리만 고치면 끝」이 되게 한다(씬 작업 불필요).
        // ====================================================================

        /// <summary>
        /// 상대 이탈 시 타이머 텍스트 문구 형식 (공통 UI 규칙 D-1).
        /// <c>{0}</c> 자리에 남은 초가 들어간다.
        ///
        /// 🔴 <b>이 문자열은 코드 전체에 이 한 곳에만 둔다.</b> 평시 문구와는
        /// <c>CountdownCoroutine</c> 안의 분기 하나로만 갈린다 — 같은 문구를 두 곳에 적으면
        /// 한쪽만 고쳐졌을 때 화면에 두 가지 표현이 섞여 나온다.
        /// </summary>
        private const string OpponentLeftCountdownFormat = "상대방이 떠났습니다. {0}초 뒤 로비로 이동합니다.";

        /// <summary>
        /// 상대 이탈 판정 시 자동 로비 복귀 카운트다운을 다시 시작할 길이(초) — 공통 UI 규칙 D-4.
        ///
        /// [초급자용 설명] 왜 남은 시간을 그대로 쓰지 않는가
        ///   이탈은 카운트다운이 거의 끝나갈 때 판정될 수도 있다. 그때 남은 시간을 그대로 두면
        ///   바뀐 타이머 문구를 읽기도 전에 화면이 사라져 버린다. 그래서 판정 시점의
        ///   남은 시간이 얼마였든 **무조건 30초로 다시 시작**해 읽을 시간을 보장한다.
        ///
        /// ⚠️ <b>규칙 M-3(재경기 맵 준비 실패)의 카운트다운 재시작과 섞지 않는다.</b>
        ///    그쪽은 <b>전체 길이</b>(_autoReturnSeconds)로 재시작하고 이쪽은 <b>30초</b>다.
        ///    재시작 시점도 길이도 다르므로 진입 메서드를 공유하지 않는다
        ///    (이탈 전용 진입점은 <c>RestartCountdownForOpponentLeft()</c> 하나뿐이다).
        /// </summary>
        private const float OpponentLeftCountdownSeconds = 30f;

        /// <summary>
        /// 상대 이탈 알림 팝업의 타이틀 (공통 UI 규칙 D-6 2항).
        /// </summary>
        private const string OpponentLeftAlertTitle = "알림";

        /// <summary>
        /// 상대 이탈 알림 팝업의 본문 (공통 UI 규칙 D-6 2항).
        ///
        /// ⚠️ 위 <see cref="OpponentLeftCountdownFormat"/> 의 앞부분과 겹쳐 보이지만
        ///    <b>같은 문자열이 아니다.</b> 타이머 쪽은 남은 초가 함께 들어가는 형식 문자열이고,
        ///    이쪽은 남은 초가 없는 한 문장이다. 규칙 D-1 과 규칙 D-6 이 두 자리의 문구를
        ///    각각 따로 정하고 있으므로 하나로 합치지 않는다.
        /// </summary>
        private const string OpponentLeftAlertMessage = "상대방이 떠났습니다.";

        /// <summary>
        /// 상대 이탈 알림 팝업의 버튼 라벨 (공통 UI 규칙 D-6 2항). 누르면 로비로 이동한다.
        /// </summary>
        private const string OpponentLeftAlertButtonLabel = "로비로";

        // ====================================================================
        // 색상 설정
        // ====================================================================

        [Header("색상 설정")]
        [Tooltip("프로젝트 공용 UI 색상 설정 에셋. Resources/Config/UIColorConfig.asset 을 연결. " +
                 "승리/패배 결과 텍스트 색상이 이 에셋에서 결정된다.")]
        [SerializeField] private UIColorConfig _colorConfig;

        /// <summary> 현재 이벤트 구독. 재초기화 시 이전 구독 정리용. </summary>
        private System.IDisposable _gameEndSubscription;

        /// <summary> 멀티플레이 재경기 활성화 이벤트 구독 해제용. </summary>
        private System.IDisposable _rematchAvailableSubscription;

        /// <summary> 멀티플레이 재경기 거절 이벤트 구독 해제용. </summary>
        private System.IDisposable _rematchDeclinedSubscription;

        /// <summary> 멀티플레이 재경기 맵 준비 실패 이벤트 구독 해제용(재경기 맵 C 단계). </summary>
        private System.IDisposable _rematchMapFailedSubscription;

        /// <summary> 멀티플레이 재경기 시작(씬 재로드 직전) 이벤트 구독 해제용. </summary>
        private System.IDisposable _rematchStartingSubscription;

        /// <summary> 네트워크 로비 복귀(씬 전환) 이벤트 구독 해제용. </summary>
        private System.IDisposable _backToLobbySubscription;

        /// <summary> 결과 화면에서의 상대 이탈 알림 구독 해제용 (공통 UI 규칙 D-1 · D-2 · D-4 · D-6). </summary>
        private System.IDisposable _opponentLeftSubscription;

        /// <summary>
        /// 상대가 이탈한 것으로 판정됐는지 여부.
        /// 카운트다운 문구를 평시/이탈 중 어느 쪽으로 쓸지 가르고(규칙 D-1),
        /// 이탈 뒤에 재경기 버튼이 다시 켜지지 않게 막는 데도 쓴다(규칙 D-2).
        /// </summary>
        private bool _opponentLeft;

        /// <summary> 자동 로비 복귀 카운트다운 코루틴. </summary>
        private Coroutine _countdownCoroutine;

        /// <summary>
        /// 재경기 요청 수락/거절 팝업. 공통 UI 규칙 D-6 에서 「응답 전 요청자 이탈」일 때 닫을 대상이다.
        ///
        /// [초급자용 설명] 왜 <c>[SerializeField]</c> 로 Inspector 배선을 하지 않는가
        ///   <c>[SerializeField]</c> 로 두면 그 참조가 씬 파일(Game.unity)에 저장되고,
        ///   <b>씬을 손으로 배선하지 않으면 런타임에 null</b> 이 되어 규칙이 조용히 성립하지 않는다.
        ///   이 팝업은 Game 씬의 <c>[UI]</c> 아래에 이미 활성 상태로 놓여 있으므로
        ///   런타임 탐색(<c>FindFirstObjectByType</c>)만으로 확실히 찾을 수 있다.
        ///   바로 위 <c>_networkGameManager</c> 가 쓰는 것과 같은 탐색 방식이며,
        ///   덕분에 이 단계는 <b>씬 작업이 필요 없다.</b>
        ///
        ///   탐색은 필요한 순간(이탈 통보 수신)에 1회만 하고 이 필드에 캐시한다 —
        ///   <c>FindFirstObjectByType</c> 은 씬 전체를 훑는 무거운 호출이라 매번 부르지 않는다.
        /// </summary>
        private RematchRequestPopup _rematchRequestPopup;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. 이벤트 구독 + 패널 숨김.
        /// LoadMap() 때마다 호출되므로 이전 구독을 정리 후 재구독.
        /// </summary>
        public void Initialize()
        {
            // NetworkGameManager 자동 탐색 (Inspector에 연결 안 된 경우)
            // NetworkGameManager는 DontDestroyOnLoad 오브젝트라 Game 씬 인스펙터에서 연결이 불가능하다.
            // 따라서 미연결 시 런타임에 직접 탐색해야 한다. 미탐색이면 로비 복귀 시 NGO Shutdown이 누락되어
            // 두 번째 매칭에서 "Cannot start Host while an instance is already running" 에러가 발생한다.
            // (LobbyUI.cs와 동일한 패턴)
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            // 멀티플레이에서 NGM을 못 찾으면 로비 복귀 시 네트워크 종료가 누락되므로 경고만 남긴다.
            // (싱글플레이는 NGM이 없는 것이 정상이므로 NetworkContext.IsNetworkActive로 분기)
            if (_networkGameManager == null && NetworkContext.IsNetworkActive)
            {
                // [개발] Warn + 개발 — LobbyUI · LobbyRootView 와 같은 사건, 같은 판정이다.
                //   FindFirstObjectByType 이 null 이라는 것은 "씬(또는 DontDestroyOnLoad)에 없다" 는 뜻이고,
                //   그건 배치 누락이라는 설정 오류다(1.3 원칙 3 단서) → Warn + 개발.
                GameLog.Dev.Warn("Network", nameof(GameEndUI),
                                 "NetworkGameManager 를 찾을 수 없다 — 로비 복귀 시 NGO Shutdown 이 누락될 수 있다");
            }

            // 이전 구독 정리 (재시작 시 중복 방지)
            _gameEndSubscription?.Dispose();
            _rematchAvailableSubscription?.Dispose();
            _rematchDeclinedSubscription?.Dispose();
            _rematchMapFailedSubscription?.Dispose();
            _rematchStartingSubscription?.Dispose();
            _backToLobbySubscription?.Dispose();
            _opponentLeftSubscription?.Dispose();

            // 새 판이 시작되므로 지난 판의 이탈 상태를 지운다.
            // (이 플래그가 남아 있으면 새 결과 화면이 처음부터 이탈 문구로 뜬다)
            _opponentLeft = false;

            // 게임 종료 이벤트 구독
            // NetworkGameEndController가 GameEvents.OnGameEnd를 발행하므로 싱글/멀티 모두 본 구독으로 ShowResult 진입한다.
            _gameEndSubscription = GameEvents.OnGameEnd
                .Subscribe(OnGameEnd);

            // 멀티 재경기 버튼 활성화 신호 구독.
            _rematchAvailableSubscription = GameEvents.OnNetworkRematchAvailable
                .Subscribe(e => SetupRematchButton(e.IsRandomMatch));

            // 멀티 재경기 거절 신호 구독 — 버튼/카운트다운 상태 복원.
            _rematchDeclinedSubscription = GameEvents.OnNetworkRematchDeclined
                .Subscribe(_ => RestoreRematchButton());

            // [재경기 맵 준비 실패] 새 맵을 못 만들었거나 전송·검증이 실패했다.
            //   씬은 재로드되지 않고 결과 화면이 그대로 있으므로, 여기서는 **눌리기 전 상태로
            //   되돌리는 것**만 한다(GameSystemRules_UI.md 「공통 UI 규칙」 규칙 M-3 —
            //   "결과 화면의 기존 선택지를 모두 복원한다").
            //   🔴 거절과 같은 메서드(RestoreRematchButton)를 부르지만 **이벤트 채널은 다르다** —
            //      거절과 실패는 원인도 다르고 나중에 붙을 안내 문구도 다르다.
            //   ⚠️ 실패를 알리는 팝업·문구는 이번 범위가 아니다(규칙 M-3 의 "팝업 여부 미정").
            //   ⚠️ 자동 로비 복귀 카운트다운 재시작도 이번 범위가 아니다 — 그동안 계속 돌던
            //      카운트다운은 재시작되지 않는다(결함이 아니라 확정된 범위 결정).
            _rematchMapFailedSubscription = GameEvents.OnNetworkRematchMapFailed
                .Subscribe(_ => RestoreRematchButton());

            // [재경기 로딩] 서버가 재경기를 시작(씬 재로드 직전)하면 모든 클라이언트가
            // 전역 로딩 인디케이터를 표시한다. 씬이 재로드되어 새 GameBootstrapper.LoadMap()이
            // 완료되면 자동으로 꺼진다(UI 규칙 L-3).
            _rematchStartingSubscription = GameEvents.OnNetworkRematchStarting
                .Subscribe(_ => UIManager.Instance?.ShowLoading(true, "재경기 준비 중..."));

            // [로비 복귀] NetworkGameManager.BackToLobby(Infrastructure)가 NGO Shutdown 완료 후
            //   본 이벤트를 발행한다. 씬 전환(SceneLoader)은 Presentation 책임이므로
            //   Infrastructure가 직접 호출하지 않고 이 구독을 통해 처리한다(UI 규칙 L-4).
            _backToLobbySubscription = GameEvents.OnNetworkBackToLobby
                .Subscribe(sceneName => SceneLoader.Load(sceneName));

            // [상대 이탈] 결과 화면이 떠 있는 동안 상대가 사라졌다는 통보.
            //   발행자는 Infrastructure 의 NetworkGameEndController 이며 두 갈래가 이 한 채널을 쓴다 —
            //     ① 상대가 로비 복귀 버튼으로 스스로 나간 정상 퇴장(상대가 나가기 직전에 보낸 통보)
            //     ② 30초 동안 상대의 신호가 끊긴 무반응 이탈(내 쪽에서 직접 판정)
            //   🔴 화면이 해야 할 일은 두 갈래가 완전히 같으므로(규칙 17) 구독도 하나만 둔다.
            //   🔴 규칙 D-6(응답 전 요청자 이탈)도 이 구독 하나에 이어 붙였다 — OnOpponentLeft() 안에서
            //      처리하며, 같은 신호에 구독을 새로 만들지 않는다(처리 순서를 보장할 수 없게 된다).
            _opponentLeftSubscription = GameEvents.OnNetworkOpponentLeft
                .Subscribe(_ => OnOpponentLeft());

            // 다시하기 버튼 이벤트 (중복 등록 방지)
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(OnRestartClicked);
                _restartButton.onClick.AddListener(OnRestartClicked);
            }

            // 로비 복귀 버튼 이벤트 (중복 등록 방지)
            if (_backToLobbyButton != null)
            {
                _backToLobbyButton.onClick.RemoveListener(OnBackToLobbyClicked);
                _backToLobbyButton.onClick.AddListener(OnBackToLobbyClicked);
            }

            // 패널 숨김
            Hide();
        }

        private void OnDestroy()
        {
            StopCountdown();
            _gameEndSubscription?.Dispose();
            _rematchAvailableSubscription?.Dispose();
            _rematchDeclinedSubscription?.Dispose();
            _rematchMapFailedSubscription?.Dispose();
            _rematchStartingSubscription?.Dispose();
            _backToLobbySubscription?.Dispose();
            _opponentLeftSubscription?.Dispose();
        }

        // ====================================================================
        // IGameUI 구현
        // ====================================================================

        /// <summary>
        /// 게임 시작/재시작 시 호출.
        /// 재경기(Rematch) 시 이전 게임의 결과 패널이 남아있는 것을 숨김.
        /// </summary>
        public void OnGameStarted()
        {
            Hide();
        }

        // OnGameEnded(): GameUIManager에서 호출 제외 대상.
        // GameEndUI는 게임 종료 시 "표시"되어야 하는 UI이므로 닫기 동작이 아닌
        // 자체 OnGameEnd 이벤트 구독으로 결과 패널을 표시함.
        // IGameUI의 default 빈 구현을 그대로 사용.

        // ====================================================================
        // 이벤트 핸들러
        // ====================================================================

        /// <summary>
        /// 게임 종료 시 호출. 승리/패배 텍스트 표시 + 게임 일시정지.
        ///
        /// 싱글/멀티 모두 GameEvents.OnGameEnd 발행으로 본 핸들러에서 처리된다.
        /// 로컬 팀 비교는 LocalPlayerTeam.Current(멀티)로 처리하되, 싱글은 LocalPlayerTeam이
        /// 설정되지 않으므로 Blue 고정 폴백을 사용한다.
        /// </summary>
        private void OnGameEnd(GameEndEvent e)
        {
            if (_panel == null) return;

            // 멀티플레이면 LocalPlayerTeam.Current(자신의 팀)과 비교, 싱글이면 Blue 기본.
            TeamId localTeam = NetworkContext.IsNetworkActive ? LocalPlayerTeam.Current : TeamId.Blue;
            bool isWin = (e.Winner == localTeam);

            if (_resultText != null)
            {
                _resultText.text = isWin ? "승리!" : "패배!";
                // 색상 설정 에셋이 연결되어 있으면 그 값을, 아니면 합리적인 폴백 색을 사용한다.
                // (Inspector 미연결 시에도 시각적으로 승/패 구분이 가능하도록 안전 가드.)
                if (_colorConfig != null)
                    _resultText.color = isWin ? _colorConfig.winColor : _colorConfig.loseColor;
                else
                    _resultText.color = isWin ? new Color(0.3f, 0.5f, 0.9f) : new Color(0.9f, 0.3f, 0.3f);
            }

            _panel?.Show();
            // 게임 일시정지
            Time.timeScale = 0f;

            // 자동 로비 복귀 카운트다운 시작 (평시 = 전체 길이)
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(_autoReturnSeconds));
        }

        /// <summary>
        /// 다시하기 버튼 클릭 시 게임 재시작.
        /// </summary>
        private void OnRestartClicked()
        {
            StopCountdown();

            // 시간 복원
            Time.timeScale = 1f;

            // 패널 닫기
            Hide();

            // 맵 재로드 (전체 재초기화)
            if (_bootstrapper != null)
                _bootstrapper.LoadMap(HexOrientation.FlatTop);
        }

        /// <summary>
        /// "로비로 돌아가기" 버튼 클릭 처리.
        /// 네트워크 활성 여부와 무관하게 로컬에서 독립 처리.
        /// </summary>
        private void OnBackToLobbyClicked()
        {
            ReturnToLobby();
        }

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 패널 숨김. 재시작 시 GameBootstrapper에서도 호출.
        /// </summary>
        public void Hide()
        {
            StopCountdown();
            _panel?.Hide();
        }

        /// <summary>
        /// 네트워크 모드에서 서버 권위의 승자 팀과 로컬 팀을 비교하여 결과 표시.
        /// 싱글플레이 OnGameEnd는 Blue 팀 고정이지만,
        /// 멀티플레이에서는 Red 팀 플레이어도 자신의 승/패를 올바르게 확인해야 함.
        /// </summary>
        /// <param name="winnerTeam">서버에서 확정된 승리 팀.</param>
        /// <param name="localTeam">이 클라이언트의 로컬 팀.</param>
        public void ShowResult(TeamId winnerTeam, TeamId localTeam)
        {
            if (_panel == null) return;

            bool isWin = (winnerTeam == localTeam);

            if (_resultText != null)
            {
                _resultText.text = isWin ? "승리!" : "패배!";
                // 색상 설정 에셋이 연결되어 있으면 그 값을, 아니면 합리적인 폴백 색을 사용한다.
                // (Inspector 미연결 시에도 시각적으로 승/패 구분이 가능하도록 안전 가드.)
                if (_colorConfig != null)
                    _resultText.color = isWin ? _colorConfig.winColor : _colorConfig.loseColor;
                else
                    _resultText.color = isWin ? new Color(0.3f, 0.5f, 0.9f) : new Color(0.9f, 0.3f, 0.3f);
            }

            _panel?.Show();
            // 게임 일시정지
            Time.timeScale = 0f;

            // 자동 로비 복귀 카운트다운 시작 (평시 = 전체 길이)
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(_autoReturnSeconds));
        }

        // ====================================================================
        // 로비 복귀 + 카운트다운
        // ====================================================================

        /// <summary>
        /// 로비로 즉시 복귀. 네트워크 활성 여부와 무관하게 로컬 독립 처리.
        ///
        /// 네트워크 활성 시:
        ///   NetworkGameManager.BackToLobby() 호출 — 내부에서 OnClientConnectedCallback 해제,
        ///   Heartbeat 정지, Lobby 퇴장, Shutdown, 씬 전환을 순서대로 안전하게 처리.
        /// 싱글플레이 시:
        ///   SceneLoader.Load(SceneLoader.Lobby) 호출 (로딩 인디케이터 자동 표시).
        ///
        /// 이전에는 NetworkManager.Singleton.Shutdown()을 직접 호출했으나,
        /// Unity.Netcode 직접 의존을 제거하고자 Application 레이어 NetworkContext로 분기하고,
        /// 실제 Shutdown 책임은 Infrastructure 레이어 NetworkGameManager로 위임.
        /// </summary>
        private void ReturnToLobby()
        {
            StopCountdown();
            Time.timeScale = 1f;
            Hide();

            // 🔴 결과 화면 위에 떠 있었을지 모르는 공통 팝업(확인 팝업 · 알림 팝업)을 닫는다.
            //
            //   왜 필요한가:
            //     공통 팝업의 실체는 UIManager 가 들고 있는 ConfirmPopup 하나이고,
            //     UIManager 는 DontDestroyOnLoad 라 <b>씬이 바뀌어도 파괴되지 않는다.</b>
            //     그래서 닫지 않은 채 로비로 넘어가면 결과 화면에서 띄운 팝업이
            //     로비 화면 위에 그대로 남는다(반투명 배경까지 함께).
            //     버튼으로 닫는 경로는 ConfirmPopup.OnConfirmClicked() 가 Hide() 를 먼저 부르므로
            //     이미 닫히지만, <b>카운트다운 만료로 자동 복귀하는 경로에는 닫는 사람이 없었다.</b>
            //
            //   🔴 여기(ReturnToLobby) 한 곳에만 넣는 이유 — 이 메서드는
            //     자동 복귀(CountdownCoroutine 만료)와 버튼 클릭(OnBackToLobbyClicked)이
            //     <b>둘 다 반드시 지나가는 길목</b>이다. 그래서 여기 한 줄이면 두 경로가 모두 덮인다.
            //     CountdownCoroutine 쪽에 같은 호출을 또 넣지 말 것 —
            //     두 곳에 흩어지면 나중에 한쪽만 고쳐져 경로별로 동작이 갈린다.
            //
            //   🔴 아래 ShowLoading(true) 보다 <b>반드시 앞</b>이어야 한다 —
            //     ConfirmPopup.Hide() 안에서 UIManager.HideBlockingOverlay() 가 불리는데,
            //     이 프로젝트의 BlockingOverlay 는 <b>참조 카운터</b>로 중첩을 관리한다.
            //     화면 점유를 정리하는 일(팝업 닫기)은 새 점유를 만드는 일(로딩 표시)보다
            //     먼저 끝나 있어야 두 점유가 섞이지 않는다. 순서를 바꾸지 말 것.
            //
            //   조건 없이 무조건 호출한다. ReturnToLobby 는 「이 화면을 떠난다」는 뜻이고,
            //   떠 있지 않았다면 아무 일도 일어나지 않으므로 검사할 이유가 없다.
            UIManager.Instance?.HideConfirmOrAlert();

            // 로비 복귀는 씬 전환(멀티는 네트워크 종료 포함)이 일어나므로
            // 그 사이 사용자가 멈춘 화면을 보지 않도록 전역 로딩 인디케이터를 띄운다.
            // 로딩을 끄는 책임은 목적지 씬(Lobby)의 LobbyRootView 초기화 완료 시점이 담당한다(UI 규칙 L-3).
            UIManager.Instance?.ShowLoading(true, "로비로 이동 중...");

            // 멀티플레이 활성 + NGM 주입되어 있으면 NGM에 위임 (BackToLobby가 내부에서 씬 전환까지 처리)
            if (NetworkContext.IsNetworkActive && _networkGameManager != null)
            {
                _networkGameManager.BackToLobby("Lobby");
                return;
            }

            // 싱글플레이 또는 NGM 미연결: 씬 전환만 수행.
            // 위에서 이미 ShowLoading(true)를 호출했지만, SceneLoader.Load 가 다시 호출해도
            // 같은 메시지로 갱신될 뿐이므로 부작용은 없다.
            SceneLoader.Load(SceneLoader.Lobby, "로비로 이동 중...");
        }

        /// <summary>
        /// 진행 중인 카운트다운 코루틴 정지 및 텍스트 초기화.
        /// </summary>
        private void StopCountdown()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }
            if (_countdownText != null)
                _countdownText.text = "";
        }

        /// <summary>
        /// 자동 로비 복귀 카운트다운. WaitForSecondsRealtime 사용 (timeScale=0 대응).
        ///
        /// 문구는 <b>상대 이탈 여부에 따라 이 메서드 안의 분기 하나로만</b> 갈린다(공통 UI 규칙 D-1).
        /// 이탈 사실과 남은 시간은 사용자에게 한 덩어리의 정보라, 별도 팝업을 새로 띄우지 않고
        /// <b>이미 떠 있는 타이머 텍스트 자리</b>를 그대로 쓴다.
        /// </summary>
        /// <param name="totalSeconds">
        /// 카운트다운 전체 길이(초). 평시에는 <c>_autoReturnSeconds</c>(규칙 D-4 의 60초),
        /// 상대 이탈 판정 시에는 <c>OpponentLeftCountdownSeconds</c>(30초)가 들어온다.
        /// </param>
        private IEnumerator CountdownCoroutine(float totalSeconds)
        {
            float remaining = totalSeconds;
            while (remaining > 0f)
            {
                if (_countdownText != null)
                {
                    int seconds = Mathf.CeilToInt(remaining);
                    // 🔴 규칙 D-1 의 이탈 문구는 상수 한 곳(OpponentLeftCountdownFormat)에만 있고
                    //    평시 문구와는 이 삼항 분기 하나로 갈린다.
                    _countdownText.text = _opponentLeft
                        ? string.Format(OpponentLeftCountdownFormat, seconds)
                        : $"{seconds}초 후 로비로 돌아갑니다.";
                }
                yield return new WaitForSecondsRealtime(1f);
                remaining -= 1f;
            }
            ReturnToLobby();
        }

        // ====================================================================
        // 상대 이탈 반영 (공통 UI 규칙 D-1 · D-2 · D-3 · D-4 · D-6)
        // ====================================================================

        /// <summary>
        /// 결과 화면이 떠 있는 동안 상대가 사라졌다는 통보를 받았을 때의 화면 처리.
        ///
        /// 하는 일은 넷이다.
        ///   1. <b>재경기 버튼 비활성화</b>(규칙 D-2) — 상대가 없으니 요청해도 받을 사람이 없다.
        ///      누를 수 있게 두면 응답이 영영 오지 않는 요청으로 사용자를 또 기다리게 만든다.
        ///   2. <b>카운트다운을 30초로 다시 시작</b>(규칙 D-4).
        ///   3. <b>타이머 문구 교체</b>(규칙 D-1) — 문구 자체는 아래 플래그를 보고
        ///      <c>CountdownCoroutine</c> 이 매 초 갱신하므로 여기서 직접 쓰지 않는다.
        ///   4. <b>응답 전이던 재경기 요청 팝업 정리 + 알림 팝업</b>(규칙 D-6) —
        ///      <see cref="HandleUnansweredRematchRequestOnOpponentLeft"/> 가 담당한다.
        ///      🔴 이 이벤트 구독은 <b>단계 7 에서 만든 한 건뿐</b>이며, 규칙 D-6 을 위해
        ///      구독을 새로 늘리지 않고 <b>이 핸들러에 이어 붙였다.</b> 같은 이탈 신호에
        ///      구독이 둘이면 처리 순서를 아무도 보장할 수 없어, 팝업을 닫는 쪽과
        ///      알림을 띄우는 쪽이 뒤바뀔 수 있다.
        ///
        /// 🔴 <b>로비 복귀 버튼은 여기서 끄지 않는다</b>(규칙 D-3). 이탈 판정 뒤에도 켜 둔다 —
        ///    두 버튼이 동시에 꺼지면 사용자가 스스로 화면을 빠져나갈 방법이 없어진다.
        ///    이탈로 꺼지는 것은 재경기 버튼뿐이다.
        /// </summary>
        private void OnOpponentLeft()
        {
            // 같은 사건이 두 번 도달할 수 있다 — 상대의 정상 퇴장 통보가 먼저 오고,
            // 그 직후 내 쪽 무반응 감시가 같은 침묵을 이탈로 판정하는 경우가 그렇다.
            // 두 번째 신호로 카운트다운이 30초부터 다시 시작되면 화면이 영영 안 닫힐 수 있으니 막는다.
            if (_opponentLeft) return;
            _opponentLeft = true;

            // 규칙 D-2 — 재경기 버튼 비활성화.
            //   버튼 텍스트("요청 중..." 등)는 건드리지 않는다. 이탈 사실을 알리는 자리는
            //   규칙 D-1 이 정한 타이머 텍스트 한 곳뿐이다.
            if (_restartButton != null)
                _restartButton.interactable = false;

            // 규칙 D-4 — 카운트다운 30초 재시작. 이때부터 문구가 이탈 문구로 바뀐다(규칙 D-1).
            RestartCountdownForOpponentLeft();

            // 규칙 D-6 — 재경기 요청에 응답하기 전에 요청자가 이탈한 경우의 추가 처리.
            //   🔴 여기서 별도의 타이머를 만들지 않는다. 규칙 D-6 3항은 이 시점의 카운트다운이
            //      「규칙 D-4 의 이탈 재시작과 같은 시점·같은 값(30초)」이라고 정하고 있으며,
            //      그 재시작은 바로 위 한 줄이 이미 끝냈다. 알림 팝업에 자기 전용 타이머를 붙이면
            //      같은 30초를 세는 시계가 두 개가 되어 한쪽을 고칠 때 다른 쪽이 조용히 어긋난다.
            HandleUnansweredRematchRequestOnOpponentLeft();
        }

        /// <summary>
        /// 상대 이탈 판정 시 자동 로비 복귀 카운트다운을 <b>30초로</b> 다시 시작한다(규칙 D-4).
        ///
        /// ⚠️ <b>이탈 전용 진입점이다.</b> 규칙 M-3(재경기 맵 준비 실패)의 카운트다운 재시작은
        ///    <b>전체 길이</b>로 다시 시작하는 별개의 규정이므로 이 메서드를 쓰지 않는다
        ///    (그쪽은 아직 미구현이며, 구현할 때도 이 메서드를 재사용하지 말 것 —
        ///     길이가 달라 한쪽을 고치면 다른 쪽이 조용히 망가진다).
        /// </summary>
        private void RestartCountdownForOpponentLeft()
        {
            // 돌고 있던 카운트다운(평시 60초)을 멈추고 30초로 새로 시작한다.
            // StopCountdown() 이 텍스트를 비우지만, 아래 코루틴이 첫 yield 전에 다시 채우므로
            // 사용자에게는 빈 텍스트가 보이지 않는다.
            StopCountdown();
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(OpponentLeftCountdownSeconds));
        }

        /// <summary>
        /// 공통 UI 규칙 D-6 — <b>재경기 요청에 응답하기 전에 요청자가 이탈한 경우</b>의 처리.
        ///
        /// <para>
        /// 규칙이 정한 순서 그대로 두 가지를 한다.
        ///   1. 떠 있던 <b>재경기 요청 팝업을 닫는다.</b>
        ///   2. <b>알림 팝업</b>(규칙 D-5)을 띄운다 — 타이틀 · 본문 · 버튼 1개.
        /// </para>
        ///
        /// <para>
        /// [초급자용 설명] 왜 팝업을 닫는 것이 먼저인가
        ///   요청을 보낸 상대가 이미 떠났으니 수락을 눌러도 그 응답이 닿을 곳이 없다.
        ///   팝업을 그대로 두면 사용자는 아직 고를 수 있다고 믿고 수락을 누르고,
        ///   아무 반응도 없는 화면 앞에서 또 기다린다. 그래서 <b>닿을 곳이 없어진 선택지를
        ///   먼저 치우고</b>, 그 다음에 무슨 일이 있었는지 알린다.
        /// </para>
        ///
        /// <para>
        /// [초급자용 설명] 왜 별도의 타이머를 만들지 않는가
        ///   규칙 D-6 3항이 「이 시점의 자동 로비 복귀 카운트다운은 30초이며 여기에 별도의
        ///   타이머를 두지 않는다」고 못 박고 있다. 그 30초는 호출부(<see cref="OnOpponentLeft"/>)의
        ///   <see cref="RestartCountdownForOpponentLeft"/> 가 이미 다시 시작했다.
        ///   그러므로 이 알림 팝업은 <b>스스로 닫히지 않는다</b> — 사용자가 버튼을 누르면 로비로 가고,
        ///   누르지 않아도 그 카운트다운이 만료되면 로비로 간다(규칙 D-4). 어느 쪽이든 갇히지 않는다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>요청 팝업이 떠 있지 않았다면 알림 팝업도 띄우지 않는다.</b> 규칙 D-6 은 제목 그대로
        ///    「요청 팝업이 떠 있는 상태에서 응답하기 전에」로 적용 범위가 한정돼 있고, 그 밖의
        ///    이탈은 규칙 D-1 의 타이머 텍스트가 알리기로 정해져 있다(규칙 D-1 은 「별도 팝업을 새로
        ///    띄우지 않고 이미 떠 있는 타이머 텍스트 자리를 쓴다」고 명시한다). 요청이 없던 이탈에도
        ///    팝업을 띄우면 그 규정과 어긋난다.
        /// </para>
        /// </summary>
        private void HandleUnansweredRematchRequestOnOpponentLeft()
        {
            // 씬에 놓인 팝업을 필요한 순간에 1회만 찾아 캐시한다(Inspector 배선 불필요).
            if (_rematchRequestPopup == null)
                _rematchRequestPopup = FindFirstObjectByType<RematchRequestPopup>();

            if (_rematchRequestPopup == null)
            {
                // [개발] Warn + 개발 — 씬에서 팝업을 찾지 못한 것은 배치 누락이라는 설정 오류다
                //   (위 NetworkGameManager 탐색 실패와 같은 사건·같은 판정).
                //   이 로그는 이탈 통보를 받은 순간에만 찍히고, 중복 통보는 호출부의
                //   _opponentLeft 가드가 막으므로 한 경기에 최대 1줄이다.
                GameLog.Dev.Warn("UI", nameof(GameEndUI),
                                 "RematchRequestPopup 을 찾을 수 없다 — 규칙 D-6 의 요청 팝업 닫기와 알림 팝업을 건너뛴다");
                return;
            }

            // 1항 — 응답 대기 중이던 요청 팝업을 닫는다.
            //   떠 있지 않았다면 이번 이탈은 규칙 D-6 의 상황이 아니므로 여기서 끝낸다
            //   (이탈 사실은 규칙 D-1 의 타이머 텍스트가 이미 알리고 있다).
            if (!_rematchRequestPopup.TryCloseUnansweredRequest())
                return;

            // 2항 — 알림 팝업(규칙 D-5). 타이틀 + 본문 + 버튼 1개, 배경 탭으로 닫히지 않는 모달이다.
            //   🔴 호출은 규칙 D-5 가 정한 null-safe 패턴을 쓴다 — Game 씬에 직접 진입하면
            //      Login 씬에서 만들어지는 UIManager 가 없어 Instance 가 null 일 수 있다.
            //   🔴 버튼 콜백은 <b>로비 복귀 버튼과 똑같은 핸들러</b>(OnBackToLobbyClicked)를 그대로 넘긴다.
            //      새 이동 경로를 만들면 카운트다운 정지 · timeScale 복원 · 로딩 인디케이터 ·
            //      멀티에서의 NGO 종료 위임 중 하나라도 빠질 수 있고, 두 경로가 갈리면
            //      한쪽만 고쳐졌을 때 버튼에 따라 동작이 달라진다.
            UIManager.Instance?.ShowAlert(OpponentLeftAlertTitle,
                                          OpponentLeftAlertMessage,
                                          OnBackToLobbyClicked,
                                          OpponentLeftAlertButtonLabel);
        }

        /// <summary>
        /// 멀티플레이 재경기 버튼 설정. 게임 모드에 따라 버튼 동작 분기.
        /// 랜덤매칭: 다시하기 버튼 숨김 (로비 복귀만 가능) — 현재는 동일 동작으로 유지.
        /// 커스텀게임: 재경기 요청 발행 + 요청 중 상태 표시.
        ///
        /// GameEvents.OnNetworkRematchAvailable 구독 시 자동 호출되며, 재경기 요청은
        /// OnLocalRematchRequested 이벤트로 발행해 컨트롤러가 ServerRpc로 변환한다.
        /// </summary>
        /// <param name="isRandomMatch">랜덤 매칭 여부. 현재 미사용 — 랜덤/커스텀 모두 동일 동작.</param>
        public void SetupRematchButton(bool isRandomMatch)
        {
            if (_restartButton == null) return;

            // 멀티플레이: 재경기 요청 이벤트 발행 (랜덤/커스텀 동일)
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() =>
            {
                // 버튼 텍스트 → "요청 중..." + 비활성화
                if (_restartButtonText != null)
                    _restartButtonText.text = "요청 중...";
                _restartButton.interactable = false;

                // 🔴 로비 복귀 버튼은 여기서 끄지 않는다 (공통 UI 규칙 D-3 「로비 복귀 버튼은 항상 활성」).
                //    종전에는 "재경기 응답 대기 중"이라는 이유로 이 버튼도 함께 껐는데,
                //    그러면 다시하기 버튼(바로 위에서 꺼진다)과 로비 버튼이 동시에 잠겨
                //    사용자가 스스로 결과 화면을 빠져나갈 방법이 사라진다.
                //    상대가 응답하지 않으면 자동 복귀 카운트다운이 끝날 때까지 갇히게 되고,
                //    상대가 이미 나가 버린 경우에는 응답 자체가 영영 오지 않는다.
                //    기다리는 것은 사용자의 선택이어야 하므로 나가는 길은 항상 열어 둔다.

                // 컨트롤러 직접 호출 대신 이벤트 발행 → NetworkGameEndController가 구독 후 ServerRpc 전송
                GameEvents.OnLocalRematchRequested.OnNext(Unit.Default);
            });
        }

        /// <summary>
        /// 재경기 거절 시 버튼 원복. 다시 요청 가능하도록 상태 복원.
        /// </summary>
        public void RestoreRematchButton()
        {
            // 🔴 상대가 이미 이탈한 뒤라면 재경기 버튼을 되살리지 않는다(규칙 D-2).
            //    이 메서드는 「재경기 거절」·「재경기 맵 준비 실패」 두 채널이 부르는데,
            //    그 신호가 이탈 통보보다 늦게 도착하면 꺼 둔 버튼이 다시 켜져
            //    받을 사람이 없는 요청을 다시 보낼 수 있게 된다.
            //    (로비 복귀 버튼을 켜는 아래 줄은 그대로 실행한다 — 규칙 D-3 은 항상 켜 두라고 정한다.)
            if (_restartButton != null && !_opponentLeft)
            {
                if (_restartButtonText != null)
                    _restartButtonText.text = "다시하기";
                _restartButton.interactable = true;
            }

            if (_backToLobbyButton != null)
                _backToLobbyButton.interactable = true;
        }
    }
}
