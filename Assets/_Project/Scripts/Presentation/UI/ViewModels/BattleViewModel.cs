// ============================================================================
// BattleViewModel.cs
// 전투 탭의 전체 상태 + 커맨드를 관리하는 ViewModel.
//
// 역할:
//   - 화면 상태: Main / CustomGame / CustomHost / CustomJoin / RandomMatch
//   - 네트워크 상태: LobbyCode, IsConnecting, ConnectedPlayers, ErrorMessage
//   - 커맨드: 싱글플레이 시작, 호스팅, 참가, 취소, 뒤로가기
//   - NetworkGameManager 이벤트 구독 → ReactiveProperty 갱신
//
// 순수 C# 클래스 — MonoBehaviour 아님.
// UnityEngine.SceneManagement만 예외적 참조 (씬 전환용).
// Presentation 레이어.
// ============================================================================

using System;
using System.Threading.Tasks;
using UniRx;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 전투 탭 ViewModel. 싱글/멀티플레이 진입 흐름을 상태 기반으로 관리.
    /// </summary>
    public class BattleViewModel : IDisposable
    {
        // ====================================================================
        // 화면 상태 열거형
        // ====================================================================

        public enum BattleScreen
        {
            Main,
            CustomGame,
            CustomHost,
            CustomJoin,
            RandomMatch,
            /// <summary>
            /// 싱글플레이 난이도 선택 화면.
            /// [싱글플레이] 버튼 클릭 시 BattleMain → 이 화면으로 전환.
            /// 난이도를 선택하면 LocalPlayerDifficulty에 저장 후 게임 씬으로 이동.
            /// </summary>
            SingleplayDifficulty
        }

        // ====================================================================
        // 상태 (View가 구독)
        // ====================================================================

        /// <summary>현재 표시 중인 화면.</summary>
        public ReactiveProperty<BattleScreen> CurrentScreen = new(BattleScreen.Main);

        /// <summary>호스트 생성 후 표시할 로비 코드.</summary>
        public ReactiveProperty<string> LobbyCode = new("");

        /// <summary>비동기 작업 진행 중 여부.</summary>
        public ReactiveProperty<bool> IsConnecting = new(false);

        /// <summary>연결된 플레이어 수.</summary>
        public ReactiveProperty<int> ConnectedPlayers = new(0);

        /// <summary>에러 메시지. 비어있으면 에러 없음.</summary>
        public ReactiveProperty<string> ErrorMessage = new("");

        /// <summary>랜덤 매칭 진행 중 여부.</summary>
        public ReactiveProperty<bool> IsMatchmaking = new(false);

        /// <summary>매칭 대기 시간 (초).</summary>
        public ReactiveProperty<int> MatchWaitSeconds = new(0);

        // ====================================================================
        // 커맨드 (View → ViewModel)
        // ====================================================================

        /// <summary>
        /// 싱글플레이 시작. 클릭 시 난이도 선택 화면(SingleplayDifficulty)으로 전환.
        /// (GameSystemRules_AI.md 규칙 35)
        /// </summary>
        public Subject<Unit> CmdStartSingleplay = new();

        /// <summary>
        /// 난이도 선택 완료. DifficultySelectView에서 쉬움/보통/어려움 버튼 클릭 시 발행.
        /// LocalPlayerDifficulty에 선택 난이도를 저장하고 Game 씬을 로드한다.
        /// (GameSystemRules_AI.md 규칙 35)
        /// </summary>
        public Subject<DifficultyLevel> CmdSelectDifficulty = new();

        /// <summary>호스트로 방 만들기.</summary>
        public Subject<Unit> CmdStartHosting = new();

        /// <summary>코드로 게임 참가.</summary>
        public Subject<string> CmdJoinGame = new();

        /// <summary>호스팅 취소.</summary>
        public Subject<Unit> CmdCancelHosting = new();

        /// <summary>랜덤 매칭 시작.</summary>
        public Subject<Unit> CmdStartMatchmaking = new();

        /// <summary>랜덤 매칭 취소.</summary>
        public Subject<Unit> CmdCancelMatchmaking = new();

        /// <summary>뒤로가기.</summary>
        public Subject<Unit> CmdBack = new();

        // ====================================================================
        // 의존성
        // ====================================================================

        private readonly NetworkGameManager _networkManager;
        private readonly CompositeDisposable _disposables = new();

        // ====================================================================
        // 생성자
        // ====================================================================

        /// <summary>
        /// BattleViewModel 생성. NetworkGameManager 이벤트를 구독하고 커맨드 처리 설정.
        /// </summary>
        /// <param name="networkManager">네트워크 세션 관리자.</param>
        public BattleViewModel(NetworkGameManager networkManager)
        {
            _networkManager = networkManager;

            // NetworkGameManager 이벤트 → ReactiveProperty 갱신
            _networkManager.OnHostStarted += OnHostStarted;
            _networkManager.OnClientConnected += OnClientConnected;
            _networkManager.OnError += OnNetworkError;

            // 커맨드 처리 설정
            // [싱글플레이] 버튼 → 난이도 선택 화면으로 전환 (씬 로드 직접 아님)
            // (GameSystemRules_AI.md 규칙 35)
            CmdStartSingleplay
                .Subscribe(_ => CurrentScreen.Value = BattleScreen.SingleplayDifficulty)
                .AddTo(_disposables);

            // 난이도 선택 완료 → LocalPlayerDifficulty에 저장 후 Game 씬 로드
            CmdSelectDifficulty
                .Subscribe(level =>
                {
                    LocalPlayerDifficulty.Set(level);
                    LoadSingleplayScene();
                })
                .AddTo(_disposables);

            CmdStartHosting
                .Subscribe(async _ =>
                {
                    try { await StartHosting(); }
                    catch (Exception e) { ErrorMessage.Value = e.Message; IsConnecting.Value = false; UIManager.Instance?.ShowLoading(false); }
                })
                .AddTo(_disposables);

            CmdJoinGame
                .Subscribe(async code =>
                {
                    try { await JoinGame(code); }
                    catch (Exception e) { ErrorMessage.Value = e.Message; IsConnecting.Value = false; UIManager.Instance?.ShowLoading(false); }
                })
                .AddTo(_disposables);

            CmdCancelHosting
                .Subscribe(_ => CancelHosting())
                .AddTo(_disposables);

            CmdStartMatchmaking
                .Subscribe(async _ =>
                {
                    try
                    {
                        CurrentScreen.Value = BattleScreen.RandomMatch;
                        IsMatchmaking.Value = true;
                        MatchWaitSeconds.Value = 0;
                        ErrorMessage.Value = "";

                        await _networkManager.StartMatchmakingAsync(
                            onWaitSecond: sec => MatchWaitSeconds.Value = sec,
                            onMatchFound: () => UIManager.Instance?.ShowLoading(true, "게임에 접속하는 중..."));
                    }
                    catch (OperationCanceledException)
                    {
                        // 사용자가 취소한 경우
                        IsMatchmaking.Value = false;
                        CurrentScreen.Value = BattleScreen.Main;
                        UIManager.Instance?.ShowLoading(false);
                    }
                    catch (Exception e)
                    {
                        ErrorMessage.Value = e.Message;
                        IsMatchmaking.Value = false;
                        UIManager.Instance?.ShowLoading(false);
                    }
                })
                .AddTo(_disposables);

            CmdCancelMatchmaking
                .Subscribe(async _ =>
                {
                    try { await _networkManager.CancelMatchmakingAsync(); }
                    catch { /* 취소 실패는 무시 — 이미 매칭 완료됐을 수 있음 */ }
                    IsMatchmaking.Value = false;
                    CurrentScreen.Value = BattleScreen.Main;
                })
                .AddTo(_disposables);

            CmdBack
                .Subscribe(_ => NavigateBack())
                .AddTo(_disposables);
        }

        // ====================================================================
        // 내부 로직
        // ====================================================================

        /// <summary>
        /// 싱글플레이 씬 로드. 로딩 스크린 표시 후 Game 씬으로 전환.
        /// </summary>
        private async void LoadSingleplayScene()
        {
            // SceneLoader.Load 가 내부에서 ShowLoading(true)를 호출하므로 별도 호출은 제거한다.
            // 단, 여기서는 로딩 표시 후 2초 대기가 필요하므로 먼저 로딩만 띄우고 대기한 뒤 씬을 로드한다.
            UIManager.Instance?.ShowLoading(true, "게임 로딩 중...");
            await Task.Delay(2000);
            SceneLoader.Load(SceneLoader.Game, "게임 로딩 중...");
        }

        /// <summary>
        /// 호스트 게임 시작. 로딩 스크린 표시 후 Relay + Lobby 생성, 대기 화면 전환.
        /// </summary>
        private async Task StartHosting()
        {
            CurrentScreen.Value = BattleScreen.CustomHost;
            IsConnecting.Value = true;
            ErrorMessage.Value = "";
            await _networkManager.HostGameAsync();
        }

        /// <summary>
        /// 코드로 게임 참가. 로딩 스크린 표시 후 Lobby 참가 → Relay 연결.
        /// </summary>
        private async Task JoinGame(string code)
        {
            IsConnecting.Value = true;
            ErrorMessage.Value = "";
            UIManager.Instance?.ShowLoading(true, "게임에 참가하는 중...");
            await _networkManager.JoinGameAsync(code);
        }

        /// <summary>
        /// 호스팅 취소. 연결 해제 후 메인 화면으로 복귀.
        /// </summary>
        private void CancelHosting()
        {
            IsConnecting.Value = false;
            CurrentScreen.Value = BattleScreen.Main;
            // DisconnectAsync는 fire-and-forget (UI에서 결과를 기다릴 필요 없음)
            _ = _networkManager.DisconnectAsync();
        }

        /// <summary>
        /// 현재 화면에 따라 이전 화면으로 돌아가기.
        /// </summary>
        private void NavigateBack()
        {
            CurrentScreen.Value = CurrentScreen.Value switch
            {
                BattleScreen.CustomHost          => BattleScreen.CustomGame,
                BattleScreen.CustomJoin          => BattleScreen.CustomGame,
                BattleScreen.CustomGame          => BattleScreen.Main,
                BattleScreen.RandomMatch         => BattleScreen.Main,
                // 난이도 선택 화면에서 뒤로가기 → 전투 메인으로 복귀
                BattleScreen.SingleplayDifficulty => BattleScreen.Main,
                _                                => BattleScreen.Main
            };
        }

        // ====================================================================
        // NetworkGameManager 이벤트 핸들러
        // ====================================================================

        /// <summary>
        /// Host 시작 완료. 로비 코드 표시 + 연결 상태 갱신.
        /// </summary>
        private void OnHostStarted(string code)
        {
            LobbyCode.Value = code;
            IsConnecting.Value = false;
            ConnectedPlayers.Value = 1;
        }

        /// <summary>
        /// 클라이언트 접속 완료. 2명 이상이면 Game 씬 로드.
        /// </summary>
        private void OnClientConnected()
        {
            ConnectedPlayers.Value++;
            if (ConnectedPlayers.Value >= 2)
            {
                UIManager.Instance?.ShowLoading(true, "게임에 접속하는 중...");
                _networkManager.LoadGameScene();
            }
        }

        /// <summary>
        /// 네트워크 오류 수신. 에러 메시지 표시 + 연결 상태 해제.
        /// </summary>
        private void OnNetworkError(string msg)
        {
            ErrorMessage.Value = msg;
            IsConnecting.Value = false;
        }

        // ====================================================================
        // 정리
        // ====================================================================

        public void Dispose()
        {
            if (_networkManager != null)
            {
                _networkManager.OnHostStarted -= OnHostStarted;
                _networkManager.OnClientConnected -= OnClientConnected;
                _networkManager.OnError -= OnNetworkError;
            }
            _disposables.Dispose();
            CmdSelectDifficulty?.Dispose();
        }
    }
}
