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

using UnityEngine;
using UnityEngine.UI;
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
        [Tooltip("게임 종료 패널 (전체 래퍼, SetActive로 토글)")]
        [SerializeField] private GameObject _panel;

        [Tooltip("승리/패배 결과 텍스트")]
        [SerializeField] private TextMeshProUGUI _resultText;

        [Tooltip("다시하기 버튼")]
        [SerializeField] private Button _restartButton;

        [Header("Dependencies")]
        [Tooltip("GameBootstrapper (재시작용)")]
        [SerializeField] private GameBootstrapper _bootstrapper;

        // ====================================================================
        // 색상 설정
        // ====================================================================

        private static readonly Color WinColor = new Color(0.3f, 0.5f, 0.9f);   // 파랑
        private static readonly Color LoseColor = new Color(0.9f, 0.3f, 0.3f);  // 빨강

        /// <summary> 현재 이벤트 구독. 재초기화 시 이전 구독 정리용. </summary>
        private System.IDisposable _gameEndSubscription;

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

            // 패널 숨김
            Hide();
        }

        private void OnDestroy()
        {
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

            _panel.SetActive(true);
            // 게임 일시정지
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 다시하기 버튼 클릭 시 게임 재시작.
        /// </summary>
        private void OnRestartClicked()
        {
            // 시간 복원
            Time.timeScale = 1f;

            // 패널 닫기
            Hide();

            // 맵 재로드 (전체 재초기화)
            if (_bootstrapper != null)
                _bootstrapper.LoadMap(HexOrientation.FlatTop);
        }

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 패널 숨김. 재시작 시 GameBootstrapper에서도 호출.
        /// </summary>
        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
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

            _panel.SetActive(true);
            // 게임 일시정지
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 멀티플레이 재시작 동작으로 버튼 클릭 콜백을 교체.
        /// 기존 싱글플레이 OnRestartClicked 대신 멀티플레이 종료 흐름(callback)을 연결.
        /// Initialize() 이후에 호출해야 AddListener 중복 등록을 방지할 수 있음.
        /// </summary>
        /// <param name="multiplayerRestartCallback">멀티플레이 재시작 시 호출할 콜백.</param>
        public void OverrideRestartForMultiplayer(System.Action multiplayerRestartCallback)
        {
            if (_restartButton == null) return;

            // 싱글플레이 콜백 제거 후 멀티플레이 콜백 등록
            _restartButton.onClick.RemoveListener(OnRestartClicked);
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() => multiplayerRestartCallback?.Invoke());
        }
    }
}
