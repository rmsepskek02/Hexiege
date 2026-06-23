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

        [Tooltip("자동 복귀까지 대기 시간 (초). 기본 30초.")]
        [SerializeField] private float _autoReturnSeconds = 30f;

        [Header("다시하기 버튼 텍스트")]
        [Tooltip("다시하기 버튼의 텍스트 컴포넌트. 요청 중 상태 표시용.")]
        [SerializeField] private TextMeshProUGUI _restartButtonText;

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

        /// <summary> 멀티플레이 재경기 시작(씬 재로드 직전) 이벤트 구독 해제용. </summary>
        private System.IDisposable _rematchStartingSubscription;

        /// <summary> 네트워크 로비 복귀(씬 전환) 이벤트 구독 해제용. </summary>
        private System.IDisposable _backToLobbySubscription;

        /// <summary> 자동 로비 복귀 카운트다운 코루틴. </summary>
        private Coroutine _countdownCoroutine;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. 이벤트 구독 + 패널 숨김.
        /// LoadMap() 때마다 호출되므로 이전 구독을 정리 후 재구독.
        /// </summary>
        public void Initialize()
        {
            // 이전 구독 정리 (재시작 시 중복 방지)
            _gameEndSubscription?.Dispose();
            _rematchAvailableSubscription?.Dispose();
            _rematchDeclinedSubscription?.Dispose();
            _rematchStartingSubscription?.Dispose();
            _backToLobbySubscription?.Dispose();

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
            _rematchStartingSubscription?.Dispose();
            _backToLobbySubscription?.Dispose();
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

            // 자동 로비 복귀 카운트다운 시작
            _countdownCoroutine = StartCoroutine(CountdownCoroutine());
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

            // 자동 로비 복귀 카운트다운 시작
            _countdownCoroutine = StartCoroutine(CountdownCoroutine());
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
        /// </summary>
        private IEnumerator CountdownCoroutine()
        {
            float remaining = _autoReturnSeconds;
            while (remaining > 0f)
            {
                if (_countdownText != null)
                    _countdownText.text = $"{Mathf.CeilToInt(remaining)}초 후 로비로 돌아갑니다.";
                yield return new WaitForSecondsRealtime(1f);
                remaining -= 1f;
            }
            ReturnToLobby();
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

                // 로비 복귀 버튼도 비활성화 (재경기 응답 대기 중)
                if (_backToLobbyButton != null)
                    _backToLobbyButton.interactable = false;

                // 컨트롤러 직접 호출 대신 이벤트 발행 → NetworkGameEndController가 구독 후 ServerRpc 전송
                GameEvents.OnLocalRematchRequested.OnNext(Unit.Default);
            });
        }

        /// <summary>
        /// 재경기 거절 시 버튼 원복. 다시 요청 가능하도록 상태 복원.
        /// </summary>
        public void RestoreRematchButton()
        {
            if (_restartButton != null)
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
