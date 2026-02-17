// ============================================================================
// GameEndUI.cs
// 게임 종료 시 승리/패배 팝업을 표시하고 다시하기 버튼을 제공.
//
// 역할:
//   1. OnGameEnd 이벤트 구독 → 승리/패배 텍스트 표시
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

        // ====================================================================
        // 초기화
        // ====================================================================

        private void Awake()
        {
            // 게임 종료 이벤트 구독
            GameEvents.OnGameEnd
                .Subscribe(OnGameEnd)
                .AddTo(this);

            // 다시하기 버튼 이벤트
            if (_restartButton != null)
                _restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void Start()
        {
            // 시작 시 패널 비활성화 (GameBootstrapper가 Hide를 호출하므로 실행되지 않을 수 있지만 안전장치로 둠)
            if (_panel != null)
                _panel.SetActive(false);
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
    }
}
