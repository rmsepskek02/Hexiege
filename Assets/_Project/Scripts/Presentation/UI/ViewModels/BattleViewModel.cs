// ============================================================================
// BattleViewModel.cs
// 전투 탭의 전체 상태 + 커맨드를 관리하는 ViewModel.
//
// 역할:
//   - 화면 상태: Main / CustomGame / CustomHost / CustomJoin / RandomMatch
//     + 싱글플레이 난이도 선택 화면
//   - 네트워크·매칭 상태: LobbyCode, IsConnecting, ConnectedPlayers, ErrorMessage,
//     그리고 랜덤 매칭 진행 여부·대기 초를 들고 있는 상태 둘
//   - 커맨드: 싱글플레이 시작, 난이도 선택, 호스팅, 참가, 호스팅 취소,
//     랜덤 매칭 시작, 랜덤 매칭 취소, 뒤로가기
//   - NetworkGameManager 이벤트 구독 → ReactiveProperty 갱신
//
// 🔴 [2026-10-05 정정] 위 세 목록은 셋 다 「항목이 빠진 목록」이었다 — 화면 상태에서 난이도
//    선택 화면이, 상태 목록에서 랜덤 매칭 관련 둘이, 커맨드 목록에서 난이도 선택과 랜덤 매칭
//    둘이 빠져 있었다. 기능이 늘 때 선언부만 늘고 이 머리말은 그대로 남기 때문이다.
//    🔴 그래서 이 목록을 「전부다」로 믿지 말 것. 단일 소스는 아래 선언부다 —
//    화면 상태는 BattleScreen 열거형, 상태는 「상태」 절의 ReactiveProperty 들,
//    커맨드는 「커맨드」 절의 Subject 들이다.
//
// 순수 C# 클래스 — MonoBehaviour 아님.
// 🔴 [2026-10-05 정정] 종전에는 이 자리에 「씬 전환용으로 유니티 엔진의 씬 관리 네임스페이스를
//    예외적으로 참조한다」고 적혀 있었다. 그 문장은 파일이 생긴 2026-03-15 부터 2026-06-22(커밋
//    39246575, 씬 전환을 정적 래퍼로 옮긴 날)까지는 사실이었고, 그 뒤로 낡았다. 지금은 이 파일의
//    using 목록에는 엔진 네임스페이스가 하나도 없고, 엔진 타입을 직접 쓰는 자리도 없다. 씬 전환은 같은 Presentation
//    레이어의 정적 래퍼(SceneLoader)가, 로딩 표시는 UIManager 가 대신 맡는다.
//    다만 [2026-10-12] 맵 전송 실패 모달이 생기면서 참조가 한 줄 늘었다 — 모달의 두 버튼 라벨을
//    결과 화면 클래스(GameEndUI)의 공개 상수에서 가져온다. 그 클래스는 MonoBehaviour 이지만 여기서
//    쓰는 것은 상수 값뿐이고, 상수는 컴파일할 때 값이 이 파일에 그대로 박히므로 실행 중에 그 화면
//    객체를 건드리지 않는다. 그래서 엔진 의존이 위 세 곳(SceneLoader · UIManager · GameEndUI 상수)에
//    모여 있고, 이 ViewModel 자신은 MonoBehaviour 가 아닌 평범한 C# 클래스로 남는다.
//    ⚠️ 이 파일만 따로 떼어 유니티 없이 컴파일하려면 그 세 클래스가 함께 필요하다.
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

        /// <summary>
        /// 에러 메시지. 비어있으면 에러 없음.
        ///
        /// 🔴 <b>[2026-10-05 실측] 이 값을 화면에 띄우는 곳은 커스텀 방 만들기 대기 화면 하나뿐이다.</b>
        /// 코드로 참가하는 화면과 랜덤 매칭 화면에는 <b>오류 텍스트를 받을 직렬화 필드 자체가 없다</b>
        /// (그 두 뷰의 Inspector 참조 목록을 직접 세어 확인했다. 매칭 화면이 가진 텍스트 한 칸은
        /// 대기 초·매칭 진행 여부만 구독한다).
        ///
        /// ⚠️ <b>그래서 이 값에 무엇을 넣어도 그 두 화면에서는 아무 일도 일어나지 않는다.</b>
        /// 「여기에 넣으면 플레이어가 본다」를 전제로 하는 주석·설계를 쓰지 말 것 —
        /// 실제로 그 전제로 쓰인 주석이 아래 맵 전송 실패 핸들러에 있었고 이번에 고쳤다.
        /// ⚠️ 이것은 <b>실측 기록이고 결함을 고친 것이 아니다</b>(코드 변경은 사용자 승인 사항).
        ///
        /// ✅ <b>맵 전송 실패 안내는 이 값 하나에 기대지 않는다.</b> 실패 핸들러가 이 값을 채우는 것과
        /// 별개로 <b>방장(호스트) 화면에 모달 팝업</b>(재시도 · 로비 두 버튼)을 띄운다. 즉 방을 만든 쪽은
        /// 어느 화면에 있든 팝업으로 실패를 안내받는다. 이 값은 팝업이 뜨지 못하는 경우(공용 UI 관리자가
        /// 없거나 팝업이 배선되지 않은 경우)의 보조 안내로 남아 있다.
        /// ⚠️ 상대방(접속한 쪽) 화면에는 여전히 아무 안내도 가지 않는다 — 호스트의 실패를 상대에게
        /// 알리는 네트워크 경로가 없기 때문이다.
        /// </summary>
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
            // 무작위 맵 3단계 I: 맵 준비 실패 시 로딩 UI 를 내리기 위해 구독한다.
            //   구독하지 않으면 실패했을 때 로딩 화면이 영영 내려가지 않는다.
            _networkManager.OnMapTransferFailed += OnMapTransferFailed;

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
        /// 호스트 게임 시작. 먼저 대기 화면으로 전환하고 진행 플래그를 세운 뒤, 세션 관리자에게
        /// Relay 할당 → Lobby 생성 → 호스트 시작을 맡긴다(그 순서는 세션 관리자 쪽 주석이 정한다).
        ///
        /// 🔴 <b>[2026-10-05 정정]</b> 종전에는 「로딩 스크린 표시 후 Relay + Lobby 생성, 대기 화면
        /// 전환」이라고 적혀 있었으나 두 군데가 사실과 달랐다.
        /// <list type="number">
        ///   <item>🔴 <b>이 경로는 로딩 인디케이터를 띄우지 않는다.</b> 이 메서드에도, 세션 관리자의
        ///         호스트 시작 경로에도 로딩 표시 호출이 한 줄도 없다(둘 다 직접 확인). 대신 대기
        ///         화면이 진행 플래그를 구독해 자기 상태 텍스트를 바꾼다. ⚠️ <b>참가 경로는 반대로
        ///         로딩 인디케이터를 띄운다</b> — 이 비대칭은 실측한 사실이며, 어느 쪽에 맞출지는
        ///         동작 변경이라 여기서 정하지 않는다.</item>
        ///   <item>순서가 거꾸로였다 — <b>화면 전환이 가장 먼저</b> 일어난다(본문 첫 줄).</item>
        /// </list>
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
        /// 호스팅 취소. 화면과 상태를 먼저 메인으로 되돌리고, 연결 해제는 결과를 기다리지 않고 맡긴다.
        ///
        /// 🔴 <b>[2026-10-05 정정]</b> 종전에는 「연결 해제 후 메인 화면으로 복귀」라고 적혀 순서가
        /// 거꾸로였다. 본문을 보면 <b>화면 복귀가 먼저이고 연결 해제가 마지막 줄</b>이며, 그 호출은
        /// 바로 아래 주석이 적은 대로 결과를 기다리지 않는다. 즉 「해제가 끝난 뒤에 복귀한다」가
        /// 아니라 「복귀는 즉시, 해제는 뒤에서」다 — 이 차이를 모르면 「복귀했으니 세션도 정리됐다」로
        /// 읽게 된다.
        /// </summary>
        private void CancelHosting()
        {
            IsConnecting.Value = false;
            LobbyCode.Value = "";
            ConnectedPlayers.Value = 0;
            ErrorMessage.Value = "";
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
        /// 클라이언트 접속 완료. 2명 이상이면 <b>맵 준비를 요청</b>한다.
        ///
        /// 🔴 종전에는 여기서 곧바로 Game 씬을 로드했다(<c>_networkManager.LoadGameScene()</c>).
        ///    이제는 그러지 않는다 — 무작위 맵 멀티플레이에서는 Host 가 로비에서 맵을 확정해
        ///    Client 에게 보내고 <b>양쪽 해시가 같을 때만</b> 전투 씬으로 넘어가야 하기 때문이다
        ///    (GameSystemRules_RandomMap.md 규칙 16).
        ///    그래서 씬 로드는 이 ViewModel 이 아니라 전송이 성공했을 때
        ///    <c>NetworkGameManager</c> 가 스스로 시작한다.
        ///
        /// ⚠️ 「로딩 켜기 + 맵 전송 시작」은 <see cref="StartMapTransferWithLoading"/> 에 있다.
        ///    실패 모달의 재시도 버튼이 같은 두 동작을 다시 해야 해서 이 자리에서 뽑아냈다.
        ///
        /// ⚠️ 실패하면 씬은 넘어가지 않고 로비에 그대로 남는다. 그때 로딩 UI 를 내리고 안내 모달을
        ///    띄우는 것은 아래 <see cref="OnMapTransferFailed"/> 의 몫이다.
        /// </summary>
        private void OnClientConnected()
        {
            ConnectedPlayers.Value++;
            if (ConnectedPlayers.Value >= 2)
            {
                StartMapTransferWithLoading();
            }
        }

        /// <summary>
        /// 로딩 화면을 켜고 Host 의 맵 준비·전송을 시작한다. 성공하면 전투 씬으로 넘어가고,
        /// 실패하면 <see cref="OnMapTransferFailed"/> 가 불린다.
        ///
        /// [초급자용 설명] 이 두 줄은 원래 <see cref="OnClientConnected"/> 안에 있었다.
        /// 맵 준비에 실패했을 때 모달의 「재시도」 버튼이 <b>똑같은 두 동작</b>을 다시 해야 해서
        /// 한 메서드로 뽑았다. 두 줄을 복사해 두 곳에 두면 언젠가 한쪽만 고쳐지기 때문이다.
        ///
        /// 🔴 순서가 중요하다 — 로딩을 <b>먼저</b> 켜야 한다. 아래 맵 전송 시작이 같은 호출 안에서
        ///    곧바로 실패할 수도 있고(그때 실패 핸들러가 로딩을 끈다), 켜기 전에 실패하면
        ///    「끈 뒤에 켜지는」 순서가 되어 로딩이 영영 안 내려간다.
        /// </summary>
        private void StartMapTransferWithLoading()
        {
            UIManager.Instance?.ShowLoading(true, "게임에 접속하는 중...");
            _networkManager.BeginMapTransferAndLoadGameScene();
        }

        /// <summary>
        /// 맵 준비·전송이 실패해 전투 씬으로 넘어가지 못했다(무작위 맵 3단계 I).
        ///
        /// 🔴 여기서 반드시 해야 하는 일은 <b>로딩 UI 를 내리는 것</b>이다.
        ///    안 내리면 화면이 "게임에 접속하는 중..." 에 걸린 채 영영 멈춘다.
        ///
        /// ✅ 그 다음으로 하는 일은 <b>방장(호스트) 화면에 모달 팝업을 띄우는 것</b>이다
        ///    (공통 UI 규칙 M-2 · GameSystemRules_RandomMap.md 규칙 16). 버튼은 둘이다.
        ///    · 재시도 — 맵 준비·전송을 <b>새 시드부터 처음부터</b> 다시 한다
        ///      (<see cref="OnMapTransferRetrySelected"/>).
        ///    · 로비 — 연결을 끊고 방에서 나간다(<see cref="OnMapTransferLobbySelected"/>).
        ///    싱글플레이의 맵 준비 실패 모달과 <b>같은 공용 확인/취소 팝업</b>을 그대로 쓰고
        ///    (새 UI 컴포넌트 없음), 모달이라 배경을 눌러도 닫히지 않는다(규칙 8 · 9).
        ///    두 버튼 라벨은 결과 화면과 같은 값을 써야 하므로 그 클래스의 상수를 가리킨다.
        ///
        /// ⚠️ <b>이 팝업은 방장 화면에만 뜬다.</b> 이 메서드를 부르는 이벤트는 호스트 안의 C# 이벤트
        ///    하나뿐이고, 호스트의 실패를 상대방(접속한 쪽)에게 알리는 네트워크 길은 아직 없다.
        ///    그래서 상대방 화면은 접속 때 켜진 로딩이 그대로 남는 것으로 읽힌다(코드 판독, 실기 미확인).
        ///    규칙 M-2 의 「어느 플레이어든」은 이 단계에서 호스트에게만 충족된다.
        ///
        /// ⚠️ 모달과 함께 오류 메시지 상태(<see cref="ErrorMessage"/>)에도 같은 글자를 채워 둔다.
        ///    공용 UI 관리자가 없거나 팝업이 배선되지 않으면 모달은 경고만 남기고 조용히 무시되는데,
        ///    그때는 방 만들기 대기 화면의 오류 텍스트가 유일한 안내이기 때문이다.
        ///    그 텍스트를 받는 화면이 하나뿐이라는 실측은 <see cref="ErrorMessage"/> 주석이 단일 소스다.
        ///
        /// ⚠️ 사유 문자열(<paramref name="reason"/>)은 <b>화면에 쓰지 않는다</b> —
        ///    규칙 16 이 *"seed·유형·내부 error code는 UI에 노출하지 않고 로그에만 남긴다"* 고
        ///    못 박았기 때문이다. 진단은 로그로 한다.
        /// </summary>
        /// <param name="reason">진단용 사유 문자열(로그 쪽에서만 쓴다).</param>
        private void OnMapTransferFailed(string reason)
        {
            UIManager.Instance?.ShowLoading(false);
            IsConnecting.Value = false;

            // 🔴 [2026-09-29 판정 — 합치지 않는다. 다음 사람이 「같은 문구 세 벌」로 보고
            //    한 상수로 모으려 들 자리다.]
            //
            // [초급자용 설명] 프로젝트 안에 「맵 준비가 안 됐다」는 뜻의 안내가 세 자리에 있다.
            //   ⓐ 이 자리 — 아래 지역 값 하나 (오류 텍스트와 모달 본문이 함께 쓴다)
            //   ⓑ 결과 화면의 상태 줄 (Presentation/UI/GameEndUI.cs 의 상태 줄 문구 상수)
            //   ⓒ 싱글플레이 실패 모달의 본문 (Bootstrap/GameBootstrapper.Map.cs 의 모달 본문 상수)
            //   글자가 거의 같아 보여서 「중복이니 상수 하나로 합치자」는 생각이 들기 쉽다.
            //   🔴 합치면 안 된다. 근거는 둘이다.
            //
            // 근거 ⓐ — 「끝의 마침표 유무가 서로 다르고, 그 차이가 사양이다.」
            //   ⓑ 자리는 규칙 D-7 이 「상태 줄은 문장으로 적는다」고 정해 마침표가 있고,
            //   ⓒ 자리는 규칙 M-4 가 마침표 없이 표기를 확정했다.
            //   🔴 [2026-10-05 정정] 종전에는 여기에 「즉 세 값의 글자가 애초에 같지 않다」가
            //   붙어 있었으나 사실이 아니다. 세 자리의 값을 바이트 단위로 대조하면 끝이 다른 것은
            //   ⓑ 하나뿐이고 ⓐ(이 자리)와 ⓒ 는 글자가 완전히 같다. 🔴 같은 것은 사고가 아니라
            //   사양이다 — 규칙 M-4 의 「표시 문구」 칸이 「멀티(규칙 M-2) 쪽 문구를 그대로 쓴다」고
            //   확정해 두었기 때문이다. 🔴 그래서 근거 ⓐ 는 ⓑ 를 나머지 둘과 가르는 근거일 뿐이고,
            //   이 자리를 ⓒ 와 가르는 것은 아래 근거 ⓑ 하나다. 규칙 문서의 판정 표도 같은 모양이라
            //   마침표 근거를 ⓑ·ⓒ 사이에만 걸고 이 자리에는 사건 근거만 건다.
            //   (그래서 ⓑ·ⓒ 쪽 주석에도 같은 판정이 이미 적혀 있다 — 여기가 세 번째 자리다.
            //    단, 그 두 자리의 주석은 마침표 근거를 자기들 둘 사이에만 걸고 있어 올바르다.)
            //
            // 근거 ⓑ — 🔴 「사건 자체가 다르다.」
            //   이 자리는 「전투 씬에 들어가기 전」(접속·맵 전송 단계)에 알리는 안내이고,
            //   따르는 규칙은 M-2 다. ⓑ·ⓒ 는 「전투 씬 안」의 자리이고 규칙 D-7 · M-4 를 따른다.
            //   🔴 M-2 와 M-4 는 서로 다른 규칙이라 「따로 개정될 수 있다.」 한 상수로 합치면
            //   한쪽 규칙만 개정돼도 다른 쪽 화면의 표기가 함께 바뀌어 버리는 「거짓 단일 소스」가 된다.
            //
            // 🔴 그래서 이 자리는 상수로 올리지 않고 맨 리터럴로 둔다 — 상수로 올리는 것이
            //    「합치기」의 첫 걸음이 되기 때문이다(그 판단 자체는 이번 범위 밖으로 남겼다).
            // ⚠️ 위 설명에 실제 문구의 글자를 옮겨 적지 않았다 — 「그 문구가 코드에 몇 곳 있는가」를
            //    세는 검사가 주석까지 함께 세어 거짓을 말하게 되기 때문이다.
            //
            // [초급자용 설명] 글자를 지역 변수에 한 번만 적고 아래 두 곳이 함께 쓴다 —
            //   ① 오류 메시지 상태(모달이 못 뜰 때의 보조 안내) ② 모달 본문.
            //   변수에 담는 이유는 같은 글자를 두 번 적으면 글자 사본이 하나 더 생기기 때문이다.
            //   🔴 이 변수는 메서드 안의 지역 값이다 — 상수(const)나 클래스 필드로 올리지 말 것.
            string failureNotice = "맵 준비에 실패했습니다";
            ErrorMessage.Value = failureNotice;

            // 모달 팝업 (공통 UI 규칙 M-2) — 싱글플레이 맵 준비 실패 모달과 같은 5인자 호출이다.
            //   · 본문: 위 지역 값. 사유(시드·유형·내부 코드)는 쓰지 않는다 — 사유로 문구를 가르지 않는다.
            //   · 확정 버튼 = 재시도 / 취소 버튼 = 로비. 라벨은 결과 화면과 같아야 하므로
            //     그 클래스의 상수를 직접 가리킨다(새 글자를 이 파일에 적지 않는다).
            //   · 확인/취소 팝업은 버튼을 누르면 스스로 먼저 닫고 나서 콜백을 부른다.
            //     그래서 재시도 콜백 안에서 곧바로 로딩을 켜고 다시 시도해도 순서가 꼬이지 않는다.
            //   · UIManager 는 Login 씬에서 만들어진 전역 객체라 Lobby 씬을 직접 열어 실행하면 없을 수 있다.
            //     그래서 프로젝트 관례대로 null 안전 호출(?.)을 쓴다.
            //   · 로딩은 이 메서드 맨 앞에서 이미 껐다. 모달은 그 뒤에 띄우므로 로딩에 가려지지 않는다.
            UIManager.Instance?.ShowConfirm(
                failureNotice,
                onConfirm: OnMapTransferRetrySelected,
                onCancel: OnMapTransferLobbySelected,
                confirmLabel: GameEndUI.RestartButtonLabel,
                cancelLabel: GameEndUI.BackToLobbyButtonLabel);
        }

        /// <summary>
        /// 맵 전송 실패 모달에서 <b>재시도</b>를 골랐을 때의 처리.
        ///
        /// <para>
        /// 하는 일은 둘이다 — ① 지난 실패가 남긴 오류 텍스트를 비우고 ② 접속 완료 때와 같은
        /// 「로딩 켜기 + 맵 준비·전송 시작」을 다시 한다(<see cref="StartMapTransferWithLoading"/>).
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>지난 맵 데이터를 다시 보내는 것이 아니다.</b> 진입점이 호출될 때마다 시드를 새로 뽑고
        /// 전송 객체도 새로 스폰하므로, 재시도는 <b>새 시드부터 맵 준비·전송을 처음부터</b> 한다
        /// (규칙 M-2 · RandomMap 규칙 16).
        /// </para>
        ///
        /// <para>
        /// ⚠️ 재시도 횟수에 제한을 두지 않는다(싱글플레이 모달과 같다). 다시 실패하면 같은 모달이 또 뜨고,
        /// 그때마다 사용자가 버튼을 눌러야 하므로 저절로 반복되지는 않는다.
        /// </para>
        /// </summary>
        private void OnMapTransferRetrySelected()
        {
            ErrorMessage.Value = "";
            StartMapTransferWithLoading();
        }

        /// <summary>
        /// 맵 전송 실패 모달에서 <b>로비</b>를 골랐을 때의 처리.
        ///
        /// <para>
        /// 🔴 <b>씬 전환이 아니다.</b> 싱글플레이 모달의 같은 버튼은 로비 씬을 불러오지만, 이 화면은
        /// 이미 로비 씬 안이다. 그래서 <b>연결을 끊고 방에서 나가는 일</b>을 한다 —
        /// 기존 방 만들기 취소 처리(<see cref="CancelHosting"/>)가 정확히 그 일이라 그대로 재사용한다
        /// (화면·상태를 메인으로 되돌리고, 연결 종료는 결과를 기다리지 않고 뒤에서 돌린다).
        /// 연결을 끊으면 맵 준비 상태와 확정된 맵도 함께 비워진다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ 이 처리는 로딩을 켜지 않는다 — 재사용하는 방 만들기 취소가 켜지 않는 선례를 따른다.
        /// </para>
        /// </summary>
        private void OnMapTransferLobbySelected()
        {
            CancelHosting();
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
                _networkManager.OnMapTransferFailed -= OnMapTransferFailed;
            }
            _disposables.Dispose();
            CmdSelectDifficulty?.Dispose();
        }
    }
}
