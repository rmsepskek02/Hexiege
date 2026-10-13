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
//    [2026-10-12] 맵 전송 실패 모달이 두 버튼이던 동안에는 모달의 두 버튼 라벨을 결과 화면 클래스
//    (GameEndUI)의 공개 상수에서 가져와 참조가 한 줄 늘어 있었다. 같은 날 모달이 버튼 하나짜리
//    알림으로 줄면서 그 참조는 이 파일의 살아 있는 코드에서 사라졌다(비활성화해 둔 구 로직 주석에만
//    이름이 남아 있었으나 2026-10-13 에 그 주석을 최종 삭제했다). 그래서 엔진 의존은 다시
//    SceneLoader · UIManager 이고, 이 ViewModel 자신은 MonoBehaviour 가 아닌 평범한 C# 클래스로 남는다.
//    ⚠️ 이 파일만 따로 떼어 유니티 없이 컴파일하려면 그 두 클래스가 함께 필요하다.
// Presentation 레이어.
// ============================================================================

using System;
using System.Threading.Tasks;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application; // GameLog — 맵 준비 실패 진입 경로를 가르는 개발 로그용(LogRules.md 1.4)
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
        /// ✅ <b>맵 준비 실패 안내는 이 값 하나에 기대지 않는다.</b> 실패 처리(맵 준비 실패 공통 진입점)가
        /// 이 값을 채우는 것과 별개로 <b>양쪽(호스트·접속한 쪽) 화면에 알림 모달</b>(버튼 하나)을 띄운다.
        /// 이 값은 모달이 뜨지 못하는 경우(공용 UI 관리자가 없거나 팝업이 배선되지 않은 경우)의 보조
        /// 안내로 쓰려고 남겨 둔 것이다.
        ///
        /// 🔴 <b>[2026-10-12 한계 — 이 보조 안내는 지금 사양에서 눈에 보이지 않는다.]</b> 같은 진입점이 이 값을
        /// 채운 <b>직후에 곧바로 방을 나가는데</b>, 방 나가기 처리가 이 값을 비우고 화면을 로비 메인으로
        /// 돌린다. 이 값을 보여 주는 화면은 위에서 말한 방 만들기 대기 화면 하나뿐이라, 쓰는 순간과
        /// 지워지는 순간 사이에 그 화면이 그려질 틈이 없다. 즉 <b>모달이 못 뜨는 경우에는 안내가 아예 없다.</b>
        /// 기존 동작을 없애지 않고 한계만 적어 두었다 — 제거 · 이동 · 유지의 판정은 모달이 실기에서 확인된
        /// 뒤 사용자 몫이다.
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
        // 맵 준비 대기 상태 (2026-10-12)
        // ====================================================================

        /// <summary>
        /// 지금 <b>맵 준비를 기다리는 중</b>인가 — 맵 준비 실패 안내를 <b>한 번만</b>, 그리고
        /// <b>기다리는 동안에만</b> 처리하게 하는 깃발 하나다.
        ///
        /// <para>
        /// [초급자용 설명] 이 깃발이 필요한 이유는 다음과 같다.
        /// <list type="number">
        ///   <item><b>한 번만 처리</b> — 접속한 쪽은 실패를 아는 길이 여럿(호스트의 통보 · 연결 끊김)이고,
        ///         그것들이 겹쳐 들어올 수 있다. 호스트가 통보를 보낸 뒤 방을 나가면 통보와 끊김이 연달아
        ///         올 수 있기 때문이다(실제로 어느 쪽이 먼저, 또는 둘 다 오는지는 확인하지 못했다).
        ///         깃발이 서 있을 때만 처리하고 처리하면서 내리므로, 두 번째 도착은
        ///         아무 일도 하지 않는다(모달이 두 번 뜨거나 방 나가기가 두 번 돌지 않는다).</item>
        ///   <item><b>기다리는 동안에만 처리</b> — 정상적으로 경기가 시작되는 길에서는 이 깃발이 서 있어도
        ///         안내가 뜨면 안 된다. 그 구분은 깃발이 아니라 <b>이 객체의 수명</b>이 맡는다:
        ///         이 ViewModel 은 로비 씬이 만들고 로비 씬이 사라질 때 함께 정리되며(<c>LobbyRootView</c> 의
        ///         생성·정리), 정리되면 이벤트 구독이 전부 풀린다. 맵 전송이 성공하면 호스트가 전투 씬을
        ///         불러 로비 씬이 사라지므로, 전투 중에는 이 객체도 구독도 존재하지 않는다.</item>
        /// </list>
        /// </para>
        ///
        /// <para>
        /// 🔴 서는 자리: 호스트 = 맵 준비를 시작하는 자리(<see cref="StartMapTransferWithLoading"/>),
        /// 접속한 쪽 = 접속 완료 콜백의 「호스트 경로가 아닌 쪽」. 내려가는 자리: 실패 처리 진입점 첫 줄,
        /// 방 나가기 처리(<see cref="CancelHosting"/>) 첫 줄 — 후자가 <b>스스로 방을 나갈 때 생기는 연결
        /// 끊김이 안내를 다시 부르지 않게</b> 하는 장치다(연결을 끊으면 끊김 핸들러가 불린다).
        /// </para>
        /// </summary>
        private bool _awaitingMapPreparation;

        /// <summary>
        /// 맵 준비 실패가 <b>어느 길로</b> 들어왔는가. 처리는 길과 무관하게 완전히 같고(규칙 M-2 —
        /// 사유로 안내를 가르지 않는다), 이 값은 <b>로그에서 길을 가르는 데만</b> 쓴다.
        /// 호스트의 통보가 실제로 도착하는지는 확인하지 못했으므로, 어느 길로 안내가 떴는지가
        /// 실기에서 그 질문에 답하는 유일한 단서다.
        /// </summary>
        private enum MapFailureOrigin
        {
            /// <summary>호스트: 자기 쪽 맵 준비·전송이 실패했다(실패 게이트가 알렸다).</summary>
            HostOwnFailure,

            /// <summary>접속한 쪽: 호스트가 보낸 실패 통보(ClientRpc)가 도착했다.</summary>
            HostNotice,

            /// <summary>접속한 쪽: 맵 준비를 기다리는 중에 연결이 끊겼다.</summary>
            ConnectionLost
        }

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
            // 2026-10-12: 접속한 쪽이 맵 준비 실패를 아는 길들. 호스트는 위 OnMapTransferFailed 로 안다.
            //   ① 호스트의 실패 통보가 도착했다  ② 맵 준비를 기다리는 중 연결이 끊겼다.
            //   둘 다 같은 진입점(EnterMapPreparationFailed)으로 모인다. 구독하지 않으면 접속한 쪽은
            //   "게임에 접속하는 중..." 로딩 화면에 갇힌다. 해제는 Dispose 에서 짝을 맞춘다.
            _networkManager.OnMapFailureNoticeReceived += OnMapFailureNoticeReceived;
            _networkManager.OnServerDisconnected += OnServerDisconnected;

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
        ///
        /// 🔴 <b>[2026-10-12] 이 메서드의 본문은 역할(호스트 · 접속한 쪽)과 무관하다.</b> 이름은 호스트
        /// 전용처럼 읽히지만 하는 일은 「화면 상태를 처음으로 되돌리고 연결을 끊는다」뿐이라(본문을 직접
        /// 열어 확인했다), 맵 준비 실패 처리가 <b>양쪽 모두</b>의 「방 나가기」로 그대로 재사용한다.
        /// 이름 변경은 이번 범위가 아니다.
        ///
        /// 🔴 <b>첫 줄이 「맵 준비 대기 깃발 내리기」인 이유</b> — 이 메서드가 <b>이 ViewModel 에서 방을 스스로
        /// 나가는 유일한 길</b>이기 때문이다(2026-10-12 기준 연결 종료를 부르는 곳은 여기뿐이다). 연결을 끊으면 끊김
        /// 핸들러가 불리는데(<c>NetworkGameManager</c> 주석: 의도적으로 나갈 때도 불린다), 깃발을 먼저
        /// 내려 두면 그 핸들러가 「기다리다 끊겼다」로 오해해 안내를 다시 띄우지 않는다.
        /// </summary>
        private void CancelHosting()
        {
            _awaitingMapPreparation = false;

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
        ///
        /// 🔴 <b>[2026-10-12] 이 이벤트는 호스트와 접속한 쪽 양쪽에서 발행된다.</b> 호스트에서는 상대가
        ///    접속했을 때, 접속한 쪽에서는 자기 접속 시작이 끝났을 때다(<c>NetworkGameManager</c> 가 호스트는 접속 감지
        ///    콜백에서, 접속한 쪽은 접속 시작이 끝난 자리에서 발행한다). 둘을 가르는 것은 접속 인원 수다 — 호스트는 방을 만들 때 1 이 되어 있어 여기서 2 가
        ///    되고, 접속한 쪽은 0 에서 시작해 1 이 된다. 그래서 <b>아래 else 가 접속한 쪽</b>이며, 그쪽은
        ///    맵을 직접 준비하지 않고 호스트의 결말(전투 씬 이동 · 실패 통보 · 연결 끊김)을 기다리기만 한다.
        ///    ⚠️ 이 판별은 <c>ConnectedPlayers</c> 가 호스트에서 먼저 1 이 되어 있다는 순서에 기댄다.
        ///
        /// ⚠️ 실패하면 씬은 넘어가지 않는다. 그때 로딩 UI 를 내리고, 방을 나가고, 안내 모달을 띄우는 것은
        ///    아래 <see cref="EnterMapPreparationFailed"/> 의 몫이다(호스트 · 접속한 쪽 공통).
        /// </summary>
        private void OnClientConnected()
        {
            ConnectedPlayers.Value++;
            if (ConnectedPlayers.Value >= 2)
            {
                StartMapTransferWithLoading();
            }
            else
            {
                // 접속한 쪽: 지금부터 호스트가 맵을 준비해 보내 주기를 기다린다.
                BeginWaitingForMapPreparation("Client");
            }
        }

        /// <summary>
        /// 로딩 화면을 켜고 Host 의 맵 준비·전송을 시작한다. 성공하면 전투 씬으로 넘어가고,
        /// 실패하면 <see cref="OnMapTransferFailed"/> 가 불린다.
        ///
        /// [초급자용 설명] 이 두 줄은 원래 <see cref="OnClientConnected"/> 안에 있었다.
        /// 모달의 「재시도」 버튼이 <b>똑같은 두 동작</b>을 다시 해야 해서 한 메서드로 뽑았다.
        /// ⚠️ <b>[2026-10-12] 재시도 버튼이 없어져 호출처는 다시 접속 완료 콜백이다.</b> 그래도 이 메서드를
        /// 도로 인라인하지는 않았다 — 이번 범위가 아니다(기존 구조를 건드리지 않는다는 방침).
        ///
        /// 🔴 순서가 중요하다 — 로딩을 <b>먼저</b> 켜야 한다. 아래 맵 전송 시작이 같은 호출 안에서
        ///    곧바로 실패할 수도 있고(그때 실패 처리가 로딩을 끈다), 켜기 전에 실패하면
        ///    「끈 뒤에 켜지는」 순서가 되어 로딩이 영영 안 내려간다.
        /// 🔴 <b>[2026-10-12] 「맵 준비 대기 깃발」은 맵 전송을 시작하기 전에 세운다.</b> 시작 호출 안에서
        ///    곧바로 실패해도 실패 처리가 깃발이 서 있을 때만 동작하기 때문이다.
        /// </summary>
        private void StartMapTransferWithLoading()
        {
            BeginWaitingForMapPreparation("Host");
            UIManager.Instance?.ShowLoading(true, "게임에 접속하는 중...");
            _networkManager.BeginMapTransferAndLoadGameScene();
        }

        /// <summary>
        /// 「맵 준비를 기다리는 중」 깃발을 세운다(호스트 · 접속한 쪽 공통). 이 깃발의 뜻은
        /// <see cref="_awaitingMapPreparation"/> 주석이 단일 소스다.
        /// </summary>
        /// <param name="role">로그에 남길 역할 표기(Host 또는 Client).</param>
        private void BeginWaitingForMapPreparation(string role)
        {
            _awaitingMapPreparation = true;

            // 축 A = Info(의도된 흐름) · 축 B = 개발(에디터 2인 구성에서 그대로 재현된다).
            //   아래 「실패 진입」 줄이 나오기 전에 이 줄이 있었는지가 「기다리는 중이었다」의 증거다.
            GameLog.Dev.Info("UI", nameof(BattleViewModel), "맵 준비 대기 시작", $"Role={role}");
        }

        // ====================================================================
        // 맵 준비 실패 — 양쪽이 같은 알림 모달을 보고 곧바로 방을 나간다
        // (공통 UI 규칙 M-2 · 2026-10-12 사양 개정)
        //
        // [초급자용 설명] 한 줄 요약
        //   맵 준비가 실패하면 호스트와 접속한 쪽이 각자 자기 화면에서 같은 일을 한다 —
        //   로딩을 끄고, 방을 나가고(연결 종료 · 화면은 로비 메인), 알림 모달 하나를 띄운다.
        //   모달의 「확인」은 모달만 닫는다. 다시 하려면 방을 새로 만든다.
        //
        // 실패를 아는 길은 아래와 같고, 모두 같은 진입점 하나(EnterMapPreparationFailed)로 모인다.
        //   호스트          : OnMapTransferFailed        — 자기 쪽 맵 준비·전송이 실패했다
        //   접속한 쪽 ①     : OnMapFailureNoticeReceived — 호스트의 실패 통보가 도착했다
        //   접속한 쪽 ②     : OnServerDisconnected       — 맵 준비를 기다리는 중 연결이 끊겼다
        // ====================================================================

        /// <summary>
        /// [호스트] 맵 준비·전송이 실패해 전투 씬으로 넘어가지 못했다(무작위 맵 3단계 I).
        /// 인자는 진단용 사유이며 <b>화면에 쓰지 않는다</b>(아래 진입점 설명).
        ///
        /// ✅ [2026-10-12] 하는 일은 진입점에 맡긴다 — 이 이벤트는 호스트 안에서만 발행되고, 접속한 쪽의
        ///    같은 처리는 아래 두 핸들러가 같은 진입점으로 보낸다.
        /// ⚠️ 이 이벤트는 호스트가 접속한 쪽에 실패 통보를 <b>보낸 다음 프레임에</b> 발행된다
        ///    (<c>NetworkGameManager</c> 의 실패 게이트). 통보를 먼저 보내야 방을 나가는 순서가 성립한다.
        /// </summary>
        /// <param name="reason">진단용 사유 문자열(로그 쪽에서만 쓴다).</param>
        private void OnMapTransferFailed(string reason)
        {
            EnterMapPreparationFailed(MapFailureOrigin.HostOwnFailure, reason);
        }

        /// <summary>
        /// [접속한 쪽 ①] 호스트가 「맵 준비가 실패했다」고 알려 왔다.
        /// 처리는 호스트의 것과 완전히 같다 — 진입점으로 보낸다.
        /// </summary>
        private void OnMapFailureNoticeReceived()
        {
            EnterMapPreparationFailed(MapFailureOrigin.HostNotice, null);
        }

        /// <summary>
        /// [접속한 쪽 ②] 서버(호스트)와의 연결이 끊겼다. <b>맵 준비를 기다리는 중이었을 때만</b>
        /// 안내가 된다 — 그 판정은 진입점이 <see cref="_awaitingMapPreparation"/> 으로 한다.
        ///
        /// [초급자용 설명] 호스트의 통보(①)와 이 끊김(②) 중 <b>먼저 온 쪽이 처리</b>하고 나머지는 깃발에 막혀
        /// 아무 일도 하지 않는다. 이 경로가 혼자 일을 하는 경우는 통보가 도착하지 못했을 때다 — 호스트가 보낸
        /// 직후 연결을 끊어 통보가 버려졌거나, 호스트가 방을 없애며 끊었거나, 호스트 앱이 죽어 전송 계층
        /// 타임아웃(씬 설정 60초)이 끊김을 알려 온 경우다(어느 경우가 실제로 일어나는지는 확인하지 못했다).
        /// 🔴 <b>이 경로를 위한 별도 타이머는 없다</b> — 전송 계층이 이미 같은 시간에 같은 일을 하므로
        /// 두 번째 시계는 같은 사건을 두 번 결론 내리게 한다.
        ///
        /// 🔴 <b>정상 경기 중에는 이 핸들러가 불릴 일이 없다.</b> 이 ViewModel 은 로비 씬과 함께 사라진다
        /// (<see cref="_awaitingMapPreparation"/> 주석). 로비 씬 안에서도 스스로 방을 나갈 때는 깃발이 이미
        /// 내려가 있어 안내가 뜨지 않는다(<see cref="CancelHosting"/>).
        /// </summary>
        private void OnServerDisconnected()
        {
            EnterMapPreparationFailed(MapFailureOrigin.ConnectionLost, null);
        }

        /// <summary>
        /// 🔴 <b>맵 준비 실패 처리의 공통 진입점</b> — 호스트의 실패 · 호스트의 통보 · 기다리다 끊김이
        /// 모두 여기로 온다. 길마다 따로 쓰지 않는 이유는 하는 일이 완전히 같기 때문이다
        /// (사유로 안내를 가르지 않는다 — 규칙 M-1 · RandomMap 규칙 16).
        ///
        /// <para>하는 일은 순서대로 다음과 같다.</para>
        /// <list type="number">
        ///   <item><b>한 번만 처리한다</b> — 맵 준비를 기다리는 중이 아니면 아무것도 하지 않고, 처리하면서 깃발을 내린다.</item>
        ///   <item>어느 길로 들어왔는지 로그로 남긴다.</item>
        ///   <item>로딩을 끈다(안 끄면 "게임에 접속하는 중..." 에 영영 걸려 있다).</item>
        ///   <item><b>방을 나간다</b> — 방 만들기 취소 처리를 그대로 재사용한다(<see cref="CancelHosting"/>).</item>
        ///   <item><b>알림 모달을 띄운다</b> — 제목과 본문, 버튼 하나. 버튼의 콜백은 비어 있어 모달만 닫는다.</item>
        /// </list>
        ///
        /// 🔴 <b>순서는 「나가기 → 모달」이다</b>(사양). 모달이 뜰 때 이미 방을 나와 있어 모달을 누르지
        /// 않아도 갇히지 않는다 — 그래서 모달에 타이머가 필요 없다.
        ///
        /// ⚠️ 사유 문자열은 <b>화면에 쓰지 않는다</b> — 규칙 16 이 *"seed·유형·내부 error code는 UI에 노출하지
        /// 않고 로그에만 남긴다"* 고 못 박았다. 진단은 로그(Reason= 필드)로 한다.
        /// </summary>
        /// <param name="origin">어느 길로 들어왔는가(로그에서 길을 가르는 용도뿐이다).</param>
        /// <param name="reason">진단용 사유 문자열. 호스트 자신의 실패일 때만 있고 나머지는 null.</param>
        private void EnterMapPreparationFailed(MapFailureOrigin origin, string reason)
        {
            // ① 한 번만. 기다리는 중이 아니면 — 이미 처리했거나, 스스로 나가는 중이거나, 애초에
            //    기다린 적이 없으면 — 조용히 돌아간다. 🔴 이 가드에는 로그를 넣지 않는다:
            //    통보와 끊김이 연달아 오는 것은 정상 흐름이고 상태 전이 지점이 아니다(LogRules 1.14 금지 8).
            if (!_awaitingMapPreparation) return;
            _awaitingMapPreparation = false;

            // ② 어느 길로 들어왔는가. 축 A = Warn(경기는 못 열렸지만 사용자는 로비로 복구된다) ·
            //    축 B = 개발(에디터 2인 구성에서 그대로 재현되고, 원인은 이미 다른 로그가 갖고 있다).
            //    🔴 새 로그 키를 만들지 않았다 — 같은 사건(맵 준비 실패)의 결말은 전송 계층이 운영 로그로 이미
            //    한 줄 남기므로 여기서 또 운영으로 남기면 한 판이 두 번 세어진다(LogRules 1.14 금지 9).
            //    길을 가르는 값은 Origin= 필드 하나다(결과 화면의 재경기 실패 로그가 같은 이름의 필드를 쓴다).
            GameLog.Dev.Warn("UI", nameof(BattleViewModel),
                             "맵 준비 실패 안내 진입 — 로딩을 끄고 방을 나간 뒤 알림 모달을 띄운다",
                             $"Origin={origin}, Role={(origin == MapFailureOrigin.HostOwnFailure ? "Host" : "Client")}, " +
                             $"Reason={(string.IsNullOrEmpty(reason) ? "-" : reason)}");

            // ③ 로딩을 끈다 + 접속 중 표시를 내린다.
            UIManager.Instance?.ShowLoading(false);
            IsConnecting.Value = false;

            // 🔴 [2026-09-29 판정 — 합치지 않는다. 다음 사람이 「같은 문구 세 벌」로 보고
            //    한 상수로 모으려 들 자리다.]
            //    [2026-10-12 갱신] 판정은 그대로다. 다만 근거 중 「끝의 마침표 유무」는 거짓이 되어
            //    아래에서 현재 사실로 고쳤고, 남는 근거를 다시 적었다.
            //
            // [초급자용 설명] 프로젝트 안에 「경기를 열지 못했다」는 뜻의 안내가 세 자리에 있다.
            //   ⓐ 이 자리 — 아래 지역 값 하나 (오류 텍스트와 알림 모달 본문이 함께 쓴다)
            //   ⓑ 결과 화면의 상태 줄 (Presentation/UI/GameEndUI.cs 의 상태 줄 문구 상수 — 싱글 재경기 실패)
            //   ⓒ 싱글플레이 최초 경기 실패 모달의 본문 (Bootstrap/GameBootstrapper.Map.cs 의 모달 본문 상수)
            //   글자가 거의 같아 보여서 「중복이니 상수 하나로 합치자」는 생각이 들기 쉽다.
            //   🔴 합치면 안 된다.
            //
            // 🔴 [2026-10-12 현재 사실 — 종전의 「끝의 마침표 유무가 서로 다르다」는 더 이상 참이 아니다]
            //   세 자리 모두 끝에 마침표가 있다. 종전에는 ⓑ 만 마침표가 있어 그 차이를 첫째 근거로 들 수
            //   있었으나, 이번 사양 개정으로 ⓐ·ⓒ 에도 마침표가 붙어 그 근거는 사라졌다.
            //   ⓐ 와 ⓒ 는 글자가 완전히 같다(종전부터 같았고 지금도 같다). ⓑ 는 재경기 상황의 문구라
            //   앞에 「재」 한 글자가 더 붙어 낱말이 다르다.
            //   ⚠️ 눈에 띄던 차이(마침표)가 없어져 세 값이 한결 더 같아 보인다 — 「중복이니 합치자」는
            //   압력은 오히려 커졌고, 그래서 이 제약은 지금이 더 중요하다. 남은 근거는 아래 하나다.
            //
            // 근거 — 🔴 「사건이 다르고, 규칙이 따로 개정될 수 있다.」
            //   이 자리는 「전투 씬에 들어가기 전」(접속·맵 전송 단계)에 알리는 안내이고, 따르는 규칙은 M-2 다.
            //   ⓑ·ⓒ 는 「전투 씬 안」의 자리이고 규칙 M-4 · D-7 을 따른다. 규칙들은 서로 따로 개정된다 —
            //   실제로 2026-10-12 에 규칙 M-2 와 M-4 의 표시 문구가 개정되는 동안 규칙 M-3(멀티 재경기 실패)은
            //   한 글자도 바뀌지 않았다(커밋 4208e468 전후의 규칙 본문을 대조해 확인). 한 상수로 합치면 한쪽
            //   규칙만 개정돼도 다른 쪽 화면의 표기가 함께 바뀌어 버리는 「거짓 단일 소스」가 된다.
            //   (ⓑ·ⓒ 쪽 주석에도 같은 판정이 적혀 있다 — 여기가 세 번째 자리다.)
            //
            // 🔴 그래서 이 자리는 상수로 올리지 않고 지역 값으로 둔다 — 상수로 올리는 것이
            //    「합치기」의 첫 걸음이 되기 때문이다.
            // ⚠️ 위 설명에 실제 문구의 글자를 옮겨 적지 않았다 — 「그 문구가 코드에 몇 곳 있는가」를
            //    세는 검사가 주석까지 함께 세어 거짓을 말하게 되기 때문이다.
            //
            // [초급자용 설명] 글자를 지역 변수에 한 번만 적고 아래 두 곳이 함께 쓴다 —
            //   ① 오류 메시지 상태(모달이 못 뜰 때의 보조 안내) ② 알림 모달 본문.
            //   변수에 담는 이유는 같은 글자를 두 번 적으면 글자 사본이 하나 더 생기기 때문이다.
            //   🔴 이 변수는 메서드 안의 지역 값이다 — 상수(const)나 클래스 필드로 올리지 말 것.
            string failureNotice = "경기를 시작할 수 없습니다.";

            // ④ 오류 텍스트(보조 안내). 🔴 기존 동작이라 남겼다. 다만 바로 아래 방 나가기가 이 값을 비운다
            //    (ErrorMessage 주석의 「한계」 참조) — 지금 사양에서는 눈에 보이지 않는다.
            ErrorMessage.Value = failureNotice;

            // ⑤ 방을 나간다. 🔴 모달보다 먼저다 — 모달이 떴을 때 이미 방 밖이어야 한다.
            //    CancelHosting 의 본문은 호스트 · 접속한 쪽 모두에게 맞다(그 메서드 주석 참조).
            CancelHosting();

            // ⑥ 알림 모달. 공통 UI 규칙 D-5(알림 팝업) · 8 · 9(모달 — 배경을 눌러도 닫히지 않는다).
            //   · 제목은 한 낱말로 무엇에 대한 알림인지 가리킨다(결과 화면의 상대 이탈 알림과 같은 제목).
            //   · 본문은 위 지역 값. 사유(시드·유형·내부 코드)는 쓰지 않는다 — 사유로 문구를 가르지 않는다.
            //   · 클릭 콜백은 비워 둔다(null) — 「확인」은 모달만 닫는다. 이 모달에는 타이머도 없다.
            //     버튼 라벨은 UIManager.ShowAlert 의 기본값을 쓴다.
            //   · UIManager 는 Login 씬에서 만들어진 전역 객체라 Lobby 씬을 직접 열어 실행하면 없을 수 있다.
            //     그래서 프로젝트 관례대로 null 안전 호출(?.)을 쓴다.
            //   · 로딩은 위에서 이미 껐다. 모달은 그 뒤에 띄우므로 로딩에 가려지지 않는다.
            //     (⚠️ UIManager 는 로딩 최소 표시 시간을 지키려고 끄기를 늦출 수 있다 — 겹침은 실기 미확인이다.)
            UIManager.Instance?.ShowAlert(title: "알림", message: failureNotice, onClick: null);
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
                _networkManager.OnMapFailureNoticeReceived -= OnMapFailureNoticeReceived;
                _networkManager.OnServerDisconnected -= OnServerDisconnected;
            }
            _disposables.Dispose();
            CmdSelectDifficulty?.Dispose();
        }
    }
}
