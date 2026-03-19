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
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Bootstrap;

namespace Hexiege.Presentation
{
    public class GameEndUI : MonoBehaviour
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

        private static readonly Color WinColor = new Color(0.3f, 0.5f, 0.9f);   // 파랑
        private static readonly Color LoseColor = new Color(0.9f, 0.3f, 0.3f);  // 빨강

        /// <summary> 현재 이벤트 구독. 재초기화 시 이전 구독 정리용. </summary>
        private System.IDisposable _gameEndSubscription;

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

            // 게임 종료 이벤트 구독
            _gameEndSubscription = GameEvents.OnGameEnd
                .Subscribe(OnGameEnd);

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
        }

        // ====================================================================
        // 이벤트 핸들러
        // ====================================================================

        /// <summary>
        /// 게임 종료 시 호출. 승리/패배 텍스트 표시 + 게임 일시정지.
        /// </summary>
        private void OnGameEnd(GameEndEvent e)
        {
            if (_panel == null) return;
            // 플레이어 = Blue 고정
            bool isWin = (e.Winner == TeamId.Blue);

            if (_resultText != null)
            {
                _resultText.text = isWin ? "승리!" : "패배!";
                _resultText.color = isWin ? WinColor : LoseColor;
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
                _resultText.color = isWin ? WinColor : LoseColor;
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
        /// </summary>
        private void ReturnToLobby()
        {
            StopCountdown();
            Time.timeScale = 1f;
            Hide();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            SceneManager.LoadScene("Lobby");
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
        /// 랜덤매칭: 다시하기 버튼 숨김 (로비 복귀만 가능).
        /// 커스텀게임: 재경기 요청 콜백 연결 + 요청 중 상태 표시.
        /// </summary>
        /// <param name="isRandomMatch">랜덤 매칭 여부. 현재 미사용 — 랜덤/커스텀 모두 동일 동작.</param>
        /// <param name="onRequestRematch">재경기 요청 콜백.</param>
        public void SetupRematchButton(bool isRandomMatch, System.Action onRequestRematch)
        {
            if (_restartButton == null) return;

            // 멀티플레이: 재경기 요청 콜백 연결 (랜덤/커스텀 동일)
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

                onRequestRematch?.Invoke();
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
