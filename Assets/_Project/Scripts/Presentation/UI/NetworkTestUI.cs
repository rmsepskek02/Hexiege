// ============================================================================
// NetworkTestUI.cs
// 멀티플레이 테스트용 간단한 Host / Join UI.
//
// 사용 흐름:
//   [Host] "시작" 버튼 클릭 → 방 생성 → 방 코드 화면에 표시 → 상대 접속 대기
//   [Client] 코드 입력 후 "참가" 버튼 클릭 → 접속 → 게임 시작
//
// 2명이 모두 연결되면 패널이 자동으로 사라지고 게임이 시작됩니다.
// ============================================================================

using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class NetworkTestUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector 연결 필드
        // ====================================================================

        [Header("패널")]
        [Tooltip("이 UI 전체 패널 — 게임 시작 시 숨겨짐")]
        [SerializeField] private GameObject _panel;

        [Header("버튼")]
        [Tooltip("Host로 방 만들기 버튼")]
        [SerializeField] private Button _hostButton;

        [Tooltip("Client로 방 참가 버튼")]
        [SerializeField] private Button _joinButton;

        [Header("코드 입력")]
        [Tooltip("참가 시 방 코드를 입력하는 필드")]
        [SerializeField] private TMP_InputField _codeInput;

        [Header("상태 텍스트")]
        [Tooltip("현재 상태 메시지 표시 (예: 방 코드, 오류 등)")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("참조")]
        [Tooltip("씬의 NetworkGameManager 오브젝트 — 비워두면 자동 탐색")]
        [SerializeField] private NetworkGameManager _networkGameManager;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private bool _isBusy; // 비동기 작업 중 중복 입력 방지

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Start()
        {
            // NetworkGameManager 자동 탐색
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            if (_networkGameManager == null)
            {
                Debug.LogError("[NetworkTestUI] NetworkGameManager를 찾을 수 없습니다.");
                SetStatus("오류: NetworkGameManager 없음");
                return;
            }

            // 버튼 이벤트 연결
            _hostButton.onClick.AddListener(OnHostClicked);
            _joinButton.onClick.AddListener(OnJoinClicked);

            // NetworkManager 콜백 — 2명 연결 시 UI 숨기기
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            // UGS 초기화
            SetStatus("초기화 중...");
            SetButtonsInteractable(false);
            InitializeAsync();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        // ====================================================================
        // 초기화
        // ====================================================================

        private async void InitializeAsync()
        {
            _networkGameManager.OnError += OnError;

            await _networkGameManager.InitializeAsync();

            // 초기화 완료 시 버튼 활성화
            SetStatus("시작 버튼을 누르거나 코드를 입력하세요");
            SetButtonsInteractable(true);
        }

        // ====================================================================
        // 버튼 핸들러
        // ====================================================================

        private async void OnHostClicked()
        {
            if (_isBusy) return;
            _isBusy = true;
            SetButtonsInteractable(false);
            SetStatus("방 생성 중...");

            // 방 생성 완료 시 코드 표시
            _networkGameManager.OnHostStarted += OnHostStarted;

            await _networkGameManager.HostGameAsync("Hexiege Room");

            _isBusy = false;
        }

        private async void OnJoinClicked()
        {
            if (_isBusy) return;

            string code = _codeInput.text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("방 코드를 입력하세요");
                return;
            }

            _isBusy = true;
            SetButtonsInteractable(false);
            SetStatus("접속 중...");

            await _networkGameManager.JoinGameAsync(code);

            _isBusy = false;
        }

        // ====================================================================
        // 콜백
        // ====================================================================

        private void OnHostStarted(string lobbyCode)
        {
            _networkGameManager.OnHostStarted -= OnHostStarted;
            // 방 코드를 화면에 크게 표시 — 상대방에게 알려줄 코드
            SetStatus($"방 코드: {lobbyCode}\n상대방이 접속하길 기다리는 중...");
        }

        private void OnClientConnected(ulong clientId)
        {
            // Host 기준: 2명(Host + Client) 연결됐을 때 UI 숨기기
            int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
            if (connectedCount >= 2)
            {
                Debug.Log("[NetworkTestUI] 2명 연결 완료 — UI 숨김");
                _panel.SetActive(false);
            }
        }

        private void OnError(string errorMessage)
        {
            SetStatus($"오류: {errorMessage}");
            SetButtonsInteractable(true);
            _isBusy = false;
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_hostButton != null) _hostButton.interactable = interactable;
            if (_joinButton != null) _joinButton.interactable = interactable;
            if (_codeInput != null) _codeInput.interactable = interactable;
        }
    }
}
