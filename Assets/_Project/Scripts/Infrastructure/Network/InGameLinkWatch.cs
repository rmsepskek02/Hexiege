// ============================================================================
// InGameLinkWatch.cs
// 인게임 연결 감시기 — 경기 중 「상대가 아직 있는가」를 Host·Client 가 각자 같은 방식으로 지켜본다
// (2026-11-26 신설 — task: _Tasks/2026-11-26/10_31_ingame-leave-judgment/Plan.md 3절 3단계).
//
// [무엇을 하나 — 초급 개발자용 설명]
//   1) 1초마다 상대에게 「나 여기 있다」 신호를 보낸다(실제 RPC 는 NetworkGameEndController 가 보낸다 —
//      RPC 는 NetworkBehaviour 에서만 보낼 수 있기 때문이다).
//   2) 상대 신호를 받으면 그 시각과 왕복 시간(RTT)을 상태기계(InGameLinkMonitor)에 넣는다.
//   3) 상태기계가 「지연」이라고 하면 덮개를 띄우게 하고(NetworkLinkState → 덮개 UI) 조작을 막는다.
//   4) 상태기계가 「판정 중」이라고 하면(침묵 15초 · 끊김 통보) 내 인터넷이 되는지 확인한다.
//        되면 → 상대가 나간 것 → 내 승리 / 안 되면(확인 못 함 포함) → 내가 끊긴 것 → 내 이탈.
//   5) 내 앱이 13초 이상 멈췄다가 돌아오면 → 스스로 패배 처리(상대 쪽은 이미 판정했을 것이므로).
//
// [왜 NetworkBehaviour 가 아니라 일반 MonoBehaviour 인가 — 이 구조의 핵심]
//   연결이 끊기면 NGO 가 NetworkBehaviour 들을 **디스폰**한다. 감시기가 NetworkBehaviour 안에 있으면
//   바로 그 순간(판정이 가장 필요한 순간) 같이 멈춰 버린다(결과 화면 감시가 그렇게 돼 있다 — Research 2-4).
//   그래서 측정 task 의 관찰자(InGameDisconnectObserver)처럼 **NetworkObject 가 아닌 빈 GameObject** 에 붙여
//   Game 씬이 내려갈 때까지 살게 한다.
//
// [🔴 릴리스 빌드에서도 동작한다]
//   관찰자와 달리 이 파일 전체를 #if 로 감싸지 않았다. 판정 기능은 실제 플레이어 기기에서 돌아야 하기 때문이다.
//   진단 로그(GameLog.Dev)만 [Conditional] 로 릴리스에서 사라진다(LogRules §1.7).
//
// [🔴 역할(Host/Client)로 갈리는 것은 「배달 수단」뿐]
//   판정 코드는 양쪽이 똑같다. RPC 방향(Host=ClientRpc, Client=ServerRpc)과 「Host 는 Client 의 첫 신호를 받은 뒤부터
//   보낸다」(아래 TrySendHeartbeat 주석)만 다르다 — 최상위 원칙 · 규칙 2 확정 D.
//
// [로그 빈도 — LogRules §1.14 금지 8] 신호 송수신마다 남기지 않는다. 상태가 바뀔 때만 1줄.
//
// [2026-11-26 2차 수정 — 사용자 확정 사양 ①~⑥ (Plan 「2차 수정」 절)]
//   ① 인터넷 확인 반복: 끊김 구간 동안 「진입 직후 · 일정 간격 · 판정 시점」에 확인한다(InGameLinkProbeSchedule).
//      한 번이라도 실패하면 판정은 「내 이탈(SelfLeft)」 — 판정 시점에 성공해도 마찬가지. 모두 성공이면 OpponentLeft.
//      (1차: 판정 시점 1회만 — 와이파이를 껐다 켠 기기가 판정 시점에 인터넷이 돌아와 「양쪽 승리」가 났다.)
//   ② 조작 차단 기준: 「문제가 생긴 쪽만 막고 피해 쪽은 막지 않는다」 — InGameLinkBlockPolicy 가 덮개(Overlay)/알림 띠(Banner)를 고른다.
//   ③ 표시 시점: 신호 0.5초 · 무응답 1.0초 · 신호가 오면 즉시 해제(InGameLinkMonitor).
//   ④ 조작 처리 확인 진단: CommandSent(보낸 쪽) ↔ CommandApplied(Host 처리) — 「조작 도장」(CommandStamp)으로 Seq · 보낸 시각을 잇는다.
//   ⑥ 진단 로그 DiagRev=3 — LinkProbeStart/Result(Seq · Kind · AnyFailed), LinkBlockMode, LinkGapEpisode, LinkDelaySummary,
//      LinkDelayEnter/Exit 의 깜빡임 관찰 키(DelayNo · SinceLastExitMs · SignalGapMs · ShownMs · ExitBy), CommandSent/CommandApplied.
//
// Infrastructure 레이어 — Unity.Netcode 사용 허용.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application; // GameLog · GameEvents · NetworkLinkState · InGameLinkMonitor. ⚠️ 수식 없는 Application 은 Hexiege.Application 이다.

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 인게임 연결 감시기. 경기마다 Game 씬에 하나 만들어지고 씬과 함께 사라진다.
    /// Host·Client 양쪽에서 같은 코드로 신호 송수신 · 지연 판단 · 이탈 판정을 한다.
    /// </summary>
    public sealed class InGameLinkWatch : MonoBehaviour
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>로그 카테고리 System.</summary>
        private const string LogSystem = "Network";

        /// <summary>LinkWatchStart 줄에 실제 전송 계층 끊김 시간을 남기기 위해 읽는 직렬화 필드 이름(읽기 전용).</summary>
        private const string DisconnectTimeoutField = "m_DisconnectTimeoutMS";

        // ====================================================================
        // 정적 — 현재 살아 있는 감시기 1개
        // ====================================================================

        /// <summary>현재 경기의 감시기. 씬이 내려가 파괴되면 Unity null 비교로 null 이 된다.</summary>
        private static InGameLinkWatch s_current;

        // ====================================================================
        // 인스턴스 상태
        // ====================================================================

        private NetworkManager _networkManager;          // 구독한 NetworkManager(해제할 때 같은 대상에서 빼기 위해 보관)
        private bool _isServer;                          // 이 기기가 Host 인가(배달 수단 선택에만 쓴다)
        private NetworkGameEndController _endController; // 신호 RPC · 로컬 결론을 맡기는 컨트롤러(같은 Game 씬)
        private InGameLinkMonitor _monitor;              // 상태기계(순수 C#)
        private bool _subscribed;                        // NGO 콜백 구독 중인가
        private IDisposable _gameEndSubscription;        // 「결과가 정해졌다」 구독

        private double _nextHeartbeatAt;                 // 다음 신호 송신 시각(realtime 초)
        private double _lastPeerSentAt = -1d;            // 상대가 보낸 마지막 신호의 「상대 시계」 송신 시각(-1 = 없음)
        private double _lastPeerReceivedAt;              // 그 신호를 내가 받은 시각(내 realtime 초)

        private long _lastAliveUtcMs;                    // 마지막으로 살아 있던(Update 를 돈) 기기 시각(UtcMs). 앱 정지 측정용

        private InternetReachabilityProbe _probe;        // 인터넷 도달 확인기(판정 때만 쓰므로 그때 만든다)
        private Task<InternetReachabilityResult> _probeTask; // 진행 중인 도달 확인(null = 없음)
        private double _probeStartedAt;                  // 도달 확인을 시작한 시각(realtime 초)
        private bool _concluded;                         // 이 감시기가 이미 결론을 냈는가(경기당 1회)
        private bool _matchEndLogged;                    // LinkWatchStop Reason=MatchEnd 를 남겼는가

        private bool _overlayMissingWarned;              // 「덮개가 씬에 없다」 경고를 남겼는가(경기당 1회)
        private bool _updateErrorLogged;                 // 매 프레임 틱 예외를 이미 남겼는가(경기당 1회)

        // ── 조작 차단(2차 방어) 집계 ─────────────────────────────────────
        private string _lastBlockedCommand;              // 이번 지연 동안 마지막으로 막은 명령 이름
        private int _blockedCommandCount;                // 이번 지연 동안 막은 명령 수

        // ── [2026-11-26 2차] 인터넷 확인 반복(사양 ①) · 차단 방식(사양 ②) ─────────
        private InGameLinkProbeSchedule _probeSchedule;  // 끊김 구간 동안의 확인 일정 · 결과 집계(순수 C#)
        private string _lastJudgeProbeText = DisconnectDiag.None; // 판정 시점 확인 결과 글자(LinkVerdict Probe= 값 — 1차 키 유지)
        private InGameLinkBlockMode _blockMode;          // 지금 감시기가 정한 차단 방식(바뀔 때만 LinkBlockMode 로그)
        private InGameLinkBlockReason _blockReason;      // 그 이유
        private bool _bannerMissingWarned;               // 「알림 띠가 씬에 없다」 경고를 남겼는가(경기당 1회)

        // ── [2026-11-26 2차] 깜빡임 관찰 집계(LinkDelaySummary — 감시 끝에 1줄) ─────
        private int _shortDelayCount;                    // ShortDelaySec(1.0초)보다 짧게 떴다 사라진 지연 수
        private double _totalDelaySec;                   // 지연 표시 시간 합(초)
        private double _maxDelaySec;                     // 가장 길었던 지연 표시(초)
        private int _bannerShownCount;                   // 차단 방식이 None/Overlay → Banner 로 바뀐 횟수
        private int _overlayShownCount;                  // 차단 방식이 None/Banner → Overlay 로 바뀐 횟수

        // ── [2026-11-26 2차] 조작 처리 확인 진단(사양 ④) ────────────────────────
        private int _commandSentSeq;                     // 이 기기가 보낸 조작 번호(경기마다 1부터)
        private readonly Dictionary<ulong, Queue<CommandStamp>> _commandStamps
            = new Dictionary<ulong, Queue<CommandStamp>>(); // [Host] 보낸 쪽별 「아직 처리 안 된 조작 도장」
        private readonly Dictionary<ulong, int> _commandRecvSeq
            = new Dictionary<ulong, int>();              // [Host] 보낸 쪽별 처리한 조작 수(순서 대조용 RecvSeq)

        /// <summary>도장을 보관하는 최대 수(보낸 쪽마다). 짝 없는 도장이 쌓여 메모리가 늘지 않게 하는 상한.</summary>
        private const int MaxPendingStamps = 32;

        /// <summary>
        /// [2026-11-26 2차] 「조작 도장」 — 보낸 쪽이 조작 RPC 바로 앞에 따로 보내는 (번호, 보낸 시각).
        /// 조작 RPC 12개의 인자를 바꾸지 않고(에디터 자체 검증 도구가 그 RPC 들을 지금 인자 그대로 직접 부른다)
        /// Host 가 「몇 번째 조작이 몇 ms 뒤에 처리됐나」를 알 수 있게 한다.
        /// </summary>
        private readonly struct CommandStamp
        {
            public readonly int Seq;          // 보낸 쪽 조작 번호
            public readonly long SentUtcMs;   // 보낸 쪽 기기 시계(UtcMs)
            public readonly bool Local;       // Host 자신의 조작인가(같은 시계 — 지연은 사실상 0)

            public CommandStamp(int seq, long sentUtcMs, bool local)
            {
                Seq = seq;
                SentUtcMs = sentUtcMs;
                Local = local;
            }
        }

        // ====================================================================
        // 진입점 — NetworkCombatController.OnNetworkSpawn 에서 1회 (🔴 #if 밖 — 릴리스에서도)
        // ====================================================================

        /// <summary>
        /// 경기 시작 — 감시기를 만들거나(없으면) 재사용하고 감시를 시작한다.
        /// 예외를 밖으로 던지지 않는다 — 부른 쪽(NetworkCombatController.OnNetworkSpawn)의 나머지 초기화를 지키기 위함.
        /// </summary>
        /// <param name="networkManager">실행 중인 NetworkManager.</param>
        /// <param name="isServer">이 기기가 서버(Host)인가.</param>
        /// <param name="scene">감시기를 둘 씬(부른 쪽 오브젝트의 씬 — Game 씬이 내려갈 때 같이 사라지게).</param>
        public static void BeginMatch(NetworkManager networkManager, bool isServer, Scene scene)
        {
            try
            {
                if (networkManager == null) return;

                InGameLinkWatch watch = s_current;
                if (watch != null && watch.gameObject.scene != scene)
                {
                    // 🔴 지난 경기의 감시기가 **다른(곧 내려갈) 씬**에 아직 남아 있다(재경기 씬 재로드 순서 — 미확인).
                    //    그것을 재사용하면 그 씬이 내려갈 때 새 경기 감시기까지 함께 사라진다. 그래서 옛 것은 감시만
                    //    끝내 두고(파괴는 씬이 한다) 새 씬에 새로 만든다. 옛 것의 OnDestroy 는 s_current 가 자기가 아니므로
                    //    전역 상태를 건드리지 않는다.
                    watch.StopWatching("NextMatchStarted");
                    watch = null;
                    s_current = null;
                }

                if (watch == null)
                {
                    // NetworkObject 가 아닌 빈 GameObject — NGO 스폰/디스폰과 무관하게 씬 수명만 따른다.
                    var go = new GameObject(nameof(InGameLinkWatch));
                    if (scene.IsValid() && scene.isLoaded && go.scene != scene)
                        SceneManager.MoveGameObjectToScene(go, scene);
                    watch = go.AddComponent<InGameLinkWatch>();
                    s_current = watch;
                }
                else
                {
                    // 같은 씬에서 다음 경기가 시작됐다(현재 구조에서는 재경기가 씬을 다시 불러오므로 드물다).
                    watch.StopWatching("NextMatchStarted");
                }

                watch.StartWatching(networkManager, isServer);
            }
            catch (Exception e)
            {
                SafeWarn("감시기 시작 실패 — 이번 경기는 연결 판정 없이 진행된다",
                         "LinkWatchError", "Stage=Begin, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>
        /// [NetworkGameEndController 의 신호 RPC 수신부가 부른다] 상대 「나 여기 있다」 신호가 도착했다.
        /// </summary>
        /// <param name="peerSentAt">상대가 보낸 시각(상대 기기 시계 — 비교하지 않고 되돌려 주기만 한다).</param>
        /// <param name="echoOfMySentAt">상대가 마지막으로 받은 「내」 신호의 송신 시각(내 시계). 없으면 음수.</param>
        /// <param name="echoHoldSec">상대가 그 내 신호를 받은 뒤 이 신호를 보내기까지 쥐고 있던 시간(초).</param>
        public static void ReceivePresenceHeartbeat(double peerSentAt, double echoOfMySentAt, double echoHoldSec)
        {
            InGameLinkWatch watch = s_current;
            if (watch == null) return;
            watch.OnHeartbeatReceived(peerSentAt, echoOfMySentAt, echoHoldSec);
        }

        /// <summary>
        /// [조작 입구 12곳이 맨 앞에서 부른다 — 2차 방어, Plan 3절 7단계]
        /// 지금 지연·판정 중이면 true 를 돌려준다 → 부른 쪽은 요청을 보내지 않고 끝낸다(U-5 「반영하지 않고 재전송하지 않음」).
        /// 같은 지연 동안 같은 명령이 연속으로 막히면 첫 1줄만 남기고 나머지는 세기만 한다(해제 때 합계 1줄).
        /// </summary>
        /// <param name="command">막힌 명령 이름(예: RequestBuild).</param>
        /// <returns>막았으면 true.</returns>
        public static bool TryBlockPlayerCommand(string command)
        {
            if (!NetworkLinkState.BlocksPlayerCommands) return false;
            InGameLinkWatch watch = s_current;
            if (watch != null) watch.NoteBlockedCommand(command);
            return true;
        }

        /// <summary>
        /// [2026-11-26 2차 — 사양 ④] [조작 입구 12곳이 「차단 검사를 통과한 뒤 · RPC 를 보내기 직전」에 부른다]
        /// 조작 하나를 보낸다고 기록한다(CommandSent). Client 면 Host 에 「조작 도장」(번호 · 보낸 시각)을 먼저 보내고,
        /// Host 면 자기 조작 도장을 바로 보관한다 — Host 가 처리할 때(CommandApplied) 같은 번호로 짝을 맞춘다.
        /// 감시기가 없으면(싱글 · 에디터 검증 도구) 아무것도 하지 않는다. 예외를 밖으로 던지지 않는다.
        /// </summary>
        /// <param name="owner">조작을 보내는 컨트롤러 이름(같은 조작 이름이 두 컨트롤러에 있어 구분용 — 예: RequestToggleAuto).</param>
        /// <param name="command">조작 이름(예: RequestBuild — CommandBlocked 와 같은 이름).</param>
        public static void NoteCommandSent(string owner, string command)
        {
            InGameLinkWatch watch = s_current;
            if (watch == null) return;
            watch.OnCommandSent(owner, command);
        }

        /// <summary>
        /// [2026-11-26 2차 — 사양 ④] [Host 의 조작 ServerRpc 12곳이 맨 앞에서 부른다] 조작 요청을 처리하기 시작한다고 기록한다
        /// (CommandApplied — 「요청이 Host 에 도착해 처리 코드에 들어왔다」. 검증 통과 · 거부 결과는 각 RPC 의 기존 로그가 남긴다).
        /// 감시기가 없거나 Host 가 아니면 아무것도 하지 않는다. 예외를 밖으로 던지지 않는다.
        /// </summary>
        /// <param name="owner">컨트롤러 이름.</param>
        /// <param name="command">조작 이름(보낸 쪽 CommandSent 와 같은 이름).</param>
        /// <param name="senderClientId">보낸 기기의 NGO ClientId(Host 자신이면 0).</param>
        public static void NoteCommandApplied(string owner, string command, ulong senderClientId)
        {
            InGameLinkWatch watch = s_current;
            if (watch == null) return;
            watch.OnCommandApplied(owner, command, senderClientId);
        }

        /// <summary>
        /// [2026-11-26 2차 — 사양 ④] [NetworkGameEndController.CommandStampServerRpc 가 부른다] Client 의 조작 도장이 Host 에 도착했다.
        /// </summary>
        /// <param name="senderClientId">보낸 Client 의 NGO ClientId.</param>
        /// <param name="seq">보낸 쪽 조작 번호.</param>
        /// <param name="sentUtcMs">보낸 쪽 기기 시계(UtcMs).</param>
        public static void ReceiveCommandStamp(ulong senderClientId, int seq, long sentUtcMs)
        {
            InGameLinkWatch watch = s_current;
            if (watch == null) return;
            watch.StoreCommandStamp(senderClientId, new CommandStamp(seq, sentUtcMs, false));
        }

        // ====================================================================
        // 감시 시작 / 끝
        // ====================================================================

        /// <summary>구독을 걸고 상태기계를 새로 만든 뒤 LinkWatchStart 를 남긴다.</summary>
        private void StartWatching(NetworkManager networkManager, bool isServer)
        {
            Unsubscribe();

            _networkManager = networkManager;
            _isServer = isServer;
            _endController = FindFirstObjectByType<NetworkGameEndController>();
            double now = Now();
            _monitor = new InGameLinkMonitor(now);
            _nextHeartbeatAt = now;      // 첫 틱에서 곧바로 한 번 보낸다
            _lastPeerSentAt = -1d;
            _lastPeerReceivedAt = 0d;
            _lastAliveUtcMs = DisconnectDiag.UtcMsNow();
            _probeTask = null;
            _concluded = false;
            _matchEndLogged = false;
            _overlayMissingWarned = false;
            _updateErrorLogged = false;
            _lastBlockedCommand = null;
            _blockedCommandCount = 0;

            // [2026-11-26 2차] 확인 일정 · 차단 방식 · 깜빡임 집계 · 조작 진단을 새 경기 기준으로 비운다.
            _probeSchedule = new InGameLinkProbeSchedule();
            _lastJudgeProbeText = DisconnectDiag.None;
            _blockMode = InGameLinkBlockMode.None;
            _blockReason = InGameLinkBlockReason.None;
            _bannerMissingWarned = false;
            _shortDelayCount = 0;
            _totalDelaySec = 0d;
            _maxDelaySec = 0d;
            _bannerShownCount = 0;
            _overlayShownCount = 0;
            _commandSentSeq = 0;
            _commandStamps.Clear();
            _commandRecvSeq.Clear();

            NetworkLinkState.BeginMatch();

            // 「결과가 정해졌다」 — 어떤 경로든(성 파괴 · 포기 · 이 감시기의 결론) 한 번 오면 감시를 끝낸다.
            //   🔴 블록 본문 람다: 안에 Dev 로그가 이어질 수 있으므로 식 본문으로 쓰지 않는다(LogRules §1.7).
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = GameEvents.OnGameEnd.Subscribe(_ => { OnMatchResultDecided(); });

            _subscribed = true;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnTransportFailure += HandleTransportFailure;

            if (_endController == null)
            {
                // [개발] Warn — 씬 배치 누락(설정 오류, LogRules 1.3 원칙 3 단서). 신호를 보낼 수단이 없으므로
                //   상대 쪽에서는 이 기기가 「신호 없음」으로 보이고, 이 기기는 끊김 통보로만 판정한다.
                SafeWarn("NetworkGameEndController 를 찾지 못했다 — 신호를 보낼 수 없다", "LinkWatchError",
                         "Stage=FindController");
            }

            // LinkWatchStart — 감시 시작(Plan 5절). 값은 상태기계 상수를 그대로 싣는다(숫자를 베껴 쓰지 않는다).
            //   DisconnectTimeoutMs : 실제 전송 계층 끊김 시간(안전망 60초 — U-12). 리플렉션 읽기, 못 읽으면 "-".
            GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkWatchStart",
                DisconnectDiag.Keys("LinkWatchStart",
                    "HeartbeatSec=" + Sec(InGameLinkMonitor.HeartbeatIntervalSec)
                    + ", DelaySilenceSec=" + Sec(InGameLinkMonitor.DelayEnterSilenceSec)
                    + ", JudgeSec=" + Sec(InGameLinkMonitor.JudgeSilenceSec)
                    + ", SelfPauseSec=" + Sec(InGameLinkMonitor.SelfPauseDefeatSec)
                    // [2026-11-26 2차 — DiagRev=3] 새 값: 확인 간격 · 최소 쉼 · 앱 정지 감지 기준 · RTT 진입 기준
                    + ", ProbeIntervalSec=" + Sec(InGameLinkProbeSchedule.ProbeIntervalSec)
                    + ", ProbeMinGapSec=" + Sec(InGameLinkProbeSchedule.ProbeMinGapSec)
                    + ", FreezeDetectSec=" + Sec(InGameLinkMonitor.FreezeDetectSec)
                    + ", DelayRttMs=" + Ms(InGameLinkMonitor.DelayEnterRttMs)
                    + ", DelayRttHoldSec=" + Sec(InGameLinkMonitor.DelayEnterRttHoldSec)
                    + ", IsServer=" + (isServer ? "True" : "False")
                    + ", DisconnectTimeoutMs=" + ReadDisconnectTimeoutText(networkManager)));
        }

        /// <summary>구독을 풀고 상태를 정리한다. LinkWatchStop 을 남긴다.</summary>
        /// <param name="reason">정리 계기(SceneUnload / NextMatchStarted).</param>
        private void StopWatching(string reason)
        {
            try
            {
                if (_monitor != null)
                {
                    GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkWatchStop",
                        DisconnectDiag.Keys("LinkWatchStop", "Reason=" + reason
                            + ", Phase=" + _monitor.Phase + ", Concluded=" + (_concluded ? "True" : "False")));
                    LogDelaySummary(reason);
                }
            }
            catch (Exception)
            {
                // 로그 실패는 정리를 막지 않는다.
            }
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = null;
            Unsubscribe();
            _monitor = null;
            _probeTask = null;
        }

        /// <summary>NGO 콜백 구독 해제. 예외를 밖으로 던지지 않는다.</summary>
        private void Unsubscribe()
        {
            try
            {
                if (_subscribed && _networkManager != null)
                {
                    _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                    _networkManager.OnTransportFailure -= HandleTransportFailure;
                }
            }
            catch (Exception)
            {
                // NetworkManager 가 이미 파괴되는 중이면 해제가 실패할 수 있다. 구독자도 함께 사라지므로 무시한다.
            }
            _subscribed = false;
        }

        // ====================================================================
        // Unity 메시지
        // ====================================================================

        /// <summary>
        /// 매 프레임: ① 앱 정지 검사(🔴 침묵 검사보다 먼저 — Plan 2-5) ② NGO 정지 검사
        /// ③ 상태기계 틱 ④ 판정 중이면 도달 확인 결과 확인 ⑤ 1초마다 신호 송신.
        /// [2026-11-26 2차] ④는 「상태와 무관하게 인터넷 확인 처리(PumpProbes)」로 바뀌었고, ⑤는 0.5초마다다.
        /// 예외를 밖으로 던지지 않는다.
        /// </summary>
        private void Update()
        {
            try
            {
                long nowUtc = DisconnectDiag.UtcMsNow();
                // ① 앱 정지 검사 — 「내가 얼마나 멈췄나」를 「상대가 얼마나 조용했나」보다 먼저 본다.
                //    멈춘 쪽이 돌아온 프레임에 상대를 침묵으로 오판하지 않게 하기 위함이다(Research 5-9).
                //    OnApplicationPause(false) 가 Update 보다 먼저 오든 나중에 오든(순서 미확인 — Research 8절)
                //    「마지막으로 살아 있던 기기 시각」 하나로 재므로 한 번만 처리된다.
                CheckFreeze(nowUtc, "FrameGap");
                _lastAliveUtcMs = nowUtc;

                if (_monitor == null || _monitor.Phase == InGameLinkPhase.Ended) return;
                double now = Now();

                // [2026-11-26 2차] 1차 구조(기록용 — 판정 중에만 도달 확인을 보던 갈래):
                // if (_monitor.Phase == InGameLinkPhase.Judging)
                // {
                //     PollProbe(now);
                // }
                // else
                // {
                //     ... (아래 ② · ③ 과 같음)
                // }
                //   2차는 인터넷 확인이 「지연 진입 직후 · 일정 간격 · 판정 시점」에 모두 일어나므로(사양 ①),
                //   확인 처리(④ PumpProbes)를 상태와 무관하게 매 프레임 돌린다.
                if (_monitor.Phase != InGameLinkPhase.Judging)
                {
                    // ② NGO 가 이미 내려가 있으면(내 전송 실패 · 상대가 연결을 내림 등) 곧바로 판정 중.
                    //    끊김 콜백이 먼저 오면 그쪽에서 이미 판정 중이 된다 — 어느 쪽이 먼저든 한 번만 들어간다.
                    if (_networkManager == null || !_networkManager.IsListening)
                    {
                        Apply(_monitor.OnDisconnectNotice(now, InGameLinkJudgeTrigger.NetworkStopped), now);
                    }
                    else
                    {
                        // ③ 시간 경과 검사(침묵 · 느림 · 해제).
                        Apply(_monitor.Tick(now), now);
                    }
                }

                // ④ [2026-11-26 2차] 인터넷 확인 — 끝난 결과 받기 · 다음 확인 시작 · 판정 시점 결과가 나왔으면 결론.
                if (_monitor != null && _monitor.Phase != InGameLinkPhase.Ended)
                {
                    PumpProbes(now);
                }

                // ⑤ 신호 송신(판정 중에도 보낸다 — 판정은 뒤집지 않지만, 상대가 아직 듣고 있다면 상대 쪽 오판을 줄인다).
                if (_monitor != null && _monitor.Phase != InGameLinkPhase.Ended && now >= _nextHeartbeatAt)
                {
                    _nextHeartbeatAt = now + InGameLinkMonitor.HeartbeatIntervalSec;
                    TrySendHeartbeat(now);
                }
            }
            catch (Exception e)
            {
                // 같은 예외가 매 프레임 반복되면 로그가 넘치므로 경기당 첫 1번만 남긴다(LogRules 1.14 금지 8).
                if (_updateErrorLogged) return;
                _updateErrorLogged = true;
                SafeWarn("감시 틱 처리 실패(경기당 1줄)", "LinkWatchError", "Stage=Update, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>
        /// 앱이 내려가거나(true) 돌아왔다(false). 돌아왔을 때 정지 시간을 재서 스스로 패배 / 「연결 복구 중」을 정한다.
        /// 🔴 정지 시간은 기기 시계(UtcNow) 차로 잰다 — realtimeSinceStartup 이 백그라운드 동안 흐르는지 미확인이다(Research 8절).
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            try
            {
                long nowUtc = DisconnectDiag.UtcMsNow();
                if (paused)
                {
                    // 내려가는 순간을 「마지막으로 살아 있던 시각」으로 적는다 — 돌아왔을 때 이 값과의 차가 정지 시간이다.
                    _lastAliveUtcMs = nowUtc;
                    return;
                }
                CheckFreeze(nowUtc, "AppPause");
                _lastAliveUtcMs = nowUtc;
            }
            catch (Exception e)
            {
                SafeWarn("앱 일시정지 처리 실패", "LinkWatchError", "Stage=AppPause, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>Game 씬이 내려갈 때 — 감시를 끝내고 전역 상태를 싱글플레이 기본값으로 되돌린다.</summary>
        private void OnDestroy()
        {
            StopWatching("SceneUnload");
            if (s_current == this)
            {
                s_current = null;
                NetworkLinkState.EndMatch();
            }
        }

        // ====================================================================
        // 신호
        // ====================================================================

        /// <summary>
        /// 내 신호를 보낸다. 실제 RPC 는 NetworkGameEndController 가 보낸다(스폰돼 있지 않으면 아무것도 안 한다).
        ///
        /// 🔴 [Host 만의 예외 — 배달 시작 시점] Host 는 Client 의 첫 신호를 받은 **뒤부터** 보낸다.
        ///    Host 의 씬 오브젝트는 Client 가 아직 씬을 불러오는 중일 때 먼저 스폰된다. 그때 보낸 ClientRpc 는
        ///    Client 쪽에 받을 오브젝트가 없어 보류·폐기 경고가 쌓일 수 있다(NGO 의 보류 메시지 동작 — 패키지 소스가 없어 미확인).
        ///    Client 의 첫 신호(ServerRpc)는 Client 가 스폰을 마친 뒤에만 나가므로, 그것을 받은 뒤부터 보내면 안전하다.
        ///    판정 코드는 양쪽이 같고, 이것은 「배달을 언제부터 하나」의 차이일 뿐이다.
        /// </summary>
        private void TrySendHeartbeat(double now)
        {
            if (_isServer && !_monitor.HasFirstSignal) return;

            NetworkGameEndController controller = ResolveController();
            if (controller == null) return;

            // 되돌림 값: 상대가 보낸 마지막 신호의 송신 시각(상대 시계)과, 내가 그것을 받은 뒤 쥐고 있던 시간.
            //   상대는 이 값으로 「자기가 보낸 시각 → 지금」에서 쥐고 있던 시간을 빼 RTT 를 잰다.
            //   두 기기의 시계를 서로 비교하지 않으므로 시계가 달라도 된다.
            double echo = _lastPeerSentAt;
            double hold = _lastPeerSentAt >= 0d ? Math.Max(0d, now - _lastPeerReceivedAt) : 0d;
            controller.SendPresenceHeartbeat(now, echo, hold);
        }

        /// <summary>상대 신호 도착 — RTT 를 계산해 상태기계에 넣는다.</summary>
        private void OnHeartbeatReceived(double peerSentAt, double echoOfMySentAt, double echoHoldSec)
        {
            try
            {
                if (_monitor == null) return;
                double now = Now();
                _lastPeerSentAt = peerSentAt;
                _lastPeerReceivedAt = now;

                double rttMs = double.NaN;
                if (echoOfMySentAt >= 0d && echoOfMySentAt <= now)
                {
                    // RTT = (지금 - 내가 보낸 시각) - 상대가 쥐고 있던 시간. 음수가 나오면(프레임 경계 오차) 0 으로.
                    rttMs = Math.Max(0d, (now - echoOfMySentAt - Math.Max(0d, echoHoldSec)) * 1000d);
                }

                Apply(_monitor.OnSignal(now, rttMs), now);
            }
            catch (Exception e)
            {
                SafeWarn("신호 수신 처리 실패", "LinkWatchError", "Stage=Receive, Exception=" + e.GetType().Name);
            }
        }

        // ====================================================================
        // NGO 콜백 — 경기 중 끊김 통보 → 곧바로 판정 중 (Plan 2-2)
        // ====================================================================

        /// <summary>
        /// NGO 가 「연결이 끊겼다」고 알렸다. Host: 상대 Client 끊김 / Client: 서버와의 연결 끊김.
        /// 이 감시기가 스스로 결론을 내고 연결을 내린 뒤에 오는 콜백은 무시된다(이미 종료 상태).
        /// </summary>
        private void HandleClientDisconnected(ulong clientId)
        {
            try
            {
                if (_monitor == null) return;
                Apply(_monitor.OnDisconnectNotice(Now(), InGameLinkJudgeTrigger.DisconnectCallback), Now());
            }
            catch (Exception e)
            {
                SafeWarn("끊김 콜백 처리 실패", "LinkWatchError", "Stage=DisconnectCallback, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>전송 계층이 실패를 알렸다 → 곧바로 판정 중.</summary>
        private void HandleTransportFailure()
        {
            try
            {
                if (_monitor == null) return;
                Apply(_monitor.OnDisconnectNotice(Now(), InGameLinkJudgeTrigger.TransportFailure), Now());
            }
            catch (Exception e)
            {
                SafeWarn("전송 실패 콜백 처리 실패", "LinkWatchError", "Stage=TransportFailure, Exception=" + e.GetType().Name);
            }
        }

        // ====================================================================
        // 상태 변화 반영 — 로그 · 전역 상태 · 판정 시작
        // ====================================================================

        /// <summary>상태기계가 알려 준 변화마다 로그를 남기고 전역 상태(NetworkLinkState)를 맞춘다.</summary>
        private void Apply(InGameLinkChange change, double now)
        {
            if (_monitor == null) return;

            if ((change & InGameLinkChange.FirstSignal) != 0)
            {
                // LinkFirstSignal — 상대 첫 신호(T-1).
                GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkFirstSignal",
                    DisconnectDiag.Keys("LinkFirstSignal", "SinceStartSec=" + Sec(_monitor.SinceStartSeconds(now))));
            }

            if ((change & InGameLinkChange.WaitingLong) != 0)
            {
                // LinkWaitingLong — 첫 신호 없이 15초. 판정하지 않는다(경기 시작 직후 오판 방지 — Plan 11절 3번 보고).
                GameLog.Dev.Warn(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkWaitingLong",
                    DisconnectDiag.Keys("LinkWaitingLong", "SinceStartSec=" + Sec(_monitor.SinceStartSeconds(now))));
            }

            if ((change & InGameLinkChange.DelayEntered) != 0)
            {
                ResetBlockedCommands();
                if (_monitor.DelayTrigger == InGameLinkDelayTrigger.Resume)
                {
                    // 「연결 복구 중」 진입 로그(LinkRecovering)는 CheckFreeze 가 정지 시간과 함께 남긴다.
                }
                else
                {
                    // LinkDelayEnter — 지연 진입(T-3). SilenceSec 은 마지막 상대 신호 후 경과, RttMs 는 최근 RTT.
                    //   [2026-11-26 2차 — 깜빡임 관찰] DelayNo(이번 경기 몇 번째 지연) · SinceLastExitMs(직전 해제 후 몇 ms 만에
                    //   다시 떴나 — 짧으면 깜빡임, 첫 지연이면 "-")를 덧붙였다. 1차 키(Trigger · SilenceSec · RttMs)는 그대로다.
                    GameLog.Dev.Warn(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkDelayEnter",
                        DisconnectDiag.Keys("LinkDelayEnter",
                            "Trigger=" + _monitor.DelayTrigger
                            + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(now))
                            + ", RttMs=" + Ms(_monitor.LatestRttMs)
                            + ", DelayNo=" + _monitor.DelayCount.ToString(CultureInfo.InvariantCulture)
                            + ", SinceLastExitMs=" + MsFromSec(_monitor.SinceLastExitAtEnterSeconds)));
                }
                WarnIfOverlayMissing();
            }

            if ((change & InGameLinkChange.DelayTriggerChanged) != 0)
            {
                // [2026-11-26 2차] LinkDelayEnter(계기 변경) — RTT 지연 중에 신호마저 1.0초 끊겼다 → 끊김 구간 시작(인터넷 확인 시작).
                //   같은 지연 표시가 이어지는 중이므로 DelayNo 는 늘지 않는다. From=Rtt 로 구분한다.
                GameLog.Dev.Warn(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkDelayEnter",
                    DisconnectDiag.Keys("LinkDelayEnter",
                        "Trigger=" + _monitor.DelayTrigger + ", From=Rtt"
                        + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(now))
                        + ", RttMs=" + Ms(_monitor.LatestRttMs)
                        + ", DelayNo=" + _monitor.DelayCount.ToString(CultureInfo.InvariantCulture)));
            }

            if ((change & InGameLinkChange.DelayExited) != 0)
            {
                // LinkDelayExit — 지연 해제(T-4). ShownSec 은 덮개가 떠 있던 시간.
                //   [2026-11-26 2차 — 즉시 해제 확인] ShownMs(표시 시간 ms) · SignalGapMs(해제를 부른 신호와 그 직전 신호의 간격 —
                //   「끊겼던 시간」과 거의 같으면 신호가 오자마자 풀렸다는 뜻) · ExitBy(Signal | RttRecovered | ResumeHold) · DelayNo ·
                //   Mode(해제 직전 차단 방식)를 덧붙였다. 1차 키(ShownSec · RttMs · BlockedCommands)는 그대로다.
                double shown = _monitor.LastShownSeconds;
                GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkDelayExit",
                    DisconnectDiag.Keys("LinkDelayExit",
                        "ShownSec=" + Sec(shown)
                        + ", RttMs=" + Ms(_monitor.LatestRttMs)
                        + ", BlockedCommands=" + _blockedCommandCount.ToString(CultureInfo.InvariantCulture)
                        + ", ShownMs=" + MsFromSec(shown)
                        + ", SignalGapMs=" + (_monitor.LastExitBy == "Signal" ? MsFromSec(_monitor.LastSignalGapSeconds) : DisconnectDiag.None)
                        + ", ExitBy=" + _monitor.LastExitBy
                        + ", DelayNo=" + _monitor.DelayCount.ToString(CultureInfo.InvariantCulture)
                        + ", Mode=" + _blockMode));
                NoteDelayShown(shown);
                ResetBlockedCommands();
            }

            if ((change & InGameLinkChange.JudgeStarted) != 0)
            {
                // LinkJudgeStart — 판정 중 진입(T-5). SilenceSec 은 첫 신호 전이면 "-".
                GameLog.Dev.Warn(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkJudgeStart",
                    DisconnectDiag.Keys("LinkJudgeStart",
                        "Trigger=" + _monitor.JudgeTrigger
                        + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(now))
                        + ", HasFirstSignal=" + (_monitor.HasFirstSignal ? "True" : "False")));
                WarnIfOverlayMissing();
                // [2026-11-26 2차] 확인 시작은 PumpProbes 가 맡는다(판정 시점 확인 Kind=Judge — 다른 확인이 돌고 있으면 끝난 뒤 순차로).
                // 옛 줄(1차): StartProbe(now);
            }

            // [2026-11-26 2차] 상태 반영을 SyncAndPublish 로 옮겼다 — 끊김 구간 시작/끝 · 차단 방식(덮개/띠)까지 함께 정한다.
            //   신호 수신 · 끊김 콜백처럼 Update 밖에서 불려도 곧바로 반영돼, 신호가 돌아오면 그 순간 화면이 풀린다(사양 ③).
            // 옛 줄(1차): NetworkLinkState.SetPhase(_monitor.Phase, _monitor.IsRecovering);
            SyncAndPublish(now);
        }

        // ====================================================================
        // 앱 정지 — 스스로 패배 / 「연결 복구 중」 (Plan 2-5, U-6)
        // ====================================================================

        /// <summary>
        /// 마지막으로 살아 있던 기기 시각과 지금의 차로 「앱이 멈춰 있었나」를 본다.
        /// 13초 이상 → 스스로 패배. 2초 이상 13초 미만 → 「연결 복구 중」.
        /// </summary>
        /// <param name="nowUtc">지금 기기 시각(UtcMs).</param>
        /// <param name="source">어디서 알아챘나(AppPause / FrameGap) — 로그용.</param>
        private void CheckFreeze(long nowUtc, string source)
        {
            if (_monitor == null || _concluded || _lastAliveUtcMs <= 0) return;
            if (_monitor.Phase == InGameLinkPhase.Ended) return;

            double pausedSec = (nowUtc - _lastAliveUtcMs) / 1000d;
            if (pausedSec < InGameLinkMonitor.FreezeDetectSec) return;

            // 🔴 상대 첫 신호를 받기 전(대기 상태)에는 정지를 판정하지 않는다.
            //   대기 상태에서는 상대도 침묵으로 판정하지 않으므로(InGameLinkMonitor.Evaluate 의 Waiting 분기)
            //   여기서 스스로 패배하면 오히려 두 기기의 결론이 어긋난다. 게다가 이 구간은 씬 로딩 직후라
            //   긴 프레임 멈춤이 정지로 잘못 잡힐 수 있다. 진단용으로 정지 사실만 한 줄 남긴다.
            if (!_monitor.HasFirstSignal)
            {
                GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkFreezeIgnored",
                    DisconnectDiag.Keys("LinkFreezeIgnored", "PausedForSec=" + Sec(pausedSec) + ", Source=" + source
                        + ", Reason=NoFirstSignal"));
                return;
            }

            if (pausedSec >= InGameLinkMonitor.SelfPauseDefeatSec)
            {
                // SelfPauseDefeat — 돌아온 기기 스스로 패배(T-6). 상대 쪽은 15초 판정으로 이미 승리했을 것이다.
                GameLog.Dev.Warn(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " SelfPauseDefeat",
                    DisconnectDiag.Keys("SelfPauseDefeat",
                        "PausedForSec=" + Sec(pausedSec) + ", Source=" + source + ", Phase=" + _monitor.Phase));
                Conclude(selfLeft: true, MatchEndCause.SelfPauseDefeat, "-", Now());
                return;
            }

            InGameLinkChange change = _monitor.OnResumedShort(Now());
            if (_monitor.IsRecovering)
            {
                // LinkRecovering — 「연결 복구 중」 표시(T-4 · T-14).
                GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkRecovering",
                    DisconnectDiag.Keys("LinkRecovering", "PausedForSec=" + Sec(pausedSec) + ", Source=" + source));
            }
            Apply(change, Now());
        }

        // ====================================================================
        // 판정 — 인터넷 도달 확인 → 결론 (Plan 2-3)
        // ====================================================================

        // --------------------------------------------------------------------
        // [2026-11-26 2차 — 사양 ①] 1차의 「판정 시점 1회 확인」 두 메서드를 주석으로 비활성화한다(삭제 금지 규칙).
        //   이유: 1차 실기 (가) — 와이파이를 껐다 켠 기기가 판정 시점에만 확인해 「내 인터넷 됨 → 내 승리」로 판정,
        //   상대도 승리로 판정해 양쪽 모두 승리가 났다. 2차는 끊김 동안 여러 번 확인하고 한 번이라도 실패하면 내 이탈로 본다.
        //   대신하는 코드: 아래 PumpProbes / StartProbeTask / HandleProbeCompleted (+ 순수 C# InGameLinkProbeSchedule).
        // --------------------------------------------------------------------
        /*
        /// <summary>인터넷 도달 확인을 시작한다(최대 10초 — 확인기 내부 한도).</summary>
        private void StartProbe(double now)
        {
            if (_probeTask != null) return;
            try
            {
                if (_probe == null) _probe = new InternetReachabilityProbe();
                _probeStartedAt = now;
                _probeTask = _probe.CheckAsync();
            }
            catch (Exception e)
            {
                // 확인기를 시작조차 못 했다 → 「확인 실패」와 같게 다룬다(U-3 · Plan 2-3 4번).
                SafeWarn("도달 확인 시작 실패 — 확인 실패로 본다", "LinkWatchError", "Stage=ProbeStart, Exception=" + e.GetType().Name);
                _probeTask = Task.FromResult(InternetReachabilityResult.Unreachable);
            }
        }

        /// <summary>도달 확인이 끝났는지 본다. 끝났으면 결론을 낸다.</summary>
        private void PollProbe(double now)
        {
            if (_probeTask == null) { StartProbe(now); return; }
            if (!_probeTask.IsCompleted) return;

            string probeText;
            bool reachable;
            if (_probeTask.IsFaulted || _probeTask.IsCanceled)
            {
                probeText = "Faulted";   // Plan 2-3 4번 — 예외는 도달 불가와 같게
                reachable = false;
            }
            else
            {
                InternetReachabilityResult result = _probeTask.Result;
                probeText = result.ToString(); // Reachable / Unreachable / Skipped
                reachable = result == InternetReachabilityResult.Reachable; // Skipped 는 도달 불가(U-13)
            }
            _probeTask = null;

            // LinkProbeResult — 도달 확인 끝(T-5). ProbeMs 는 확인에 걸린 시간.
            GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkProbeResult",
                DisconnectDiag.Keys("LinkProbeResult",
                    "Probe=" + probeText + ", ProbeMs=" + Ms((now - _probeStartedAt) * 1000d)));

            Conclude(selfLeft: !reachable,
                     reachable ? MatchEndCause.OpponentLeft : MatchEndCause.SelfDisconnected,
                     probeText, now);
        }
        */

        // ====================================================================
        // [2026-11-26 2차 — 사양 ①] 인터넷 확인 반복 — 끊김 구간 동안 「진입 직후 · 간격 · 판정 시점」
        // ====================================================================

        /// <summary>
        /// 매 프레임: ① 돌고 있는 확인이 끝났으면 결과를 센다 ② 다음 확인을 시작할 때면 시작한다(동시에 하나만)
        /// ③ 판정 중이고 판정 시점 확인 결과가 나왔으면 결론을 낸다.
        /// 🔴 결론: 이번 끊김 구간에서 확인이 한 번이라도 실패했으면 「내 이탈」, 모두 성공이면 「상대 이탈(내 승리)」.
        /// </summary>
        private void PumpProbes(double now)
        {
            if (_monitor == null || _probeSchedule == null) return;

            // ① 끝난 확인 받기
            if (_probeTask != null && _probeTask.IsCompleted)
            {
                HandleProbeCompleted(now);
                SyncAndPublish(now); // 결과로 차단 방식이 바뀔 수 있다(예: Host 알림 띠 → 실패 → 덮개)
            }

            if (_monitor == null || _monitor.Phase == InGameLinkPhase.Ended) return;
            bool judging = _monitor.Phase == InGameLinkPhase.Judging;

            // ② 다음 확인 시작
            if (_probeTask == null && _probeSchedule.ShouldStart(now, judging))
            {
                StartProbeTask(now, judging);
            }

            // ③ 결론 — 판정 시점 확인 결과가 나온 뒤에만(판정 시점 결과를 기다리는 구조 유지)
            if (judging && _probeSchedule.VerdictReady && !_concluded)
            {
                bool selfLeft = _probeSchedule.AnyFailed;
                Conclude(selfLeft: selfLeft,
                         selfLeft ? MatchEndCause.SelfDisconnected : MatchEndCause.OpponentLeft,
                         _lastJudgeProbeText, now);
            }
        }

        /// <summary>확인 하나를 시작한다(최대 10초 — 확인기 내부 한도). 시작조차 못 하면 「실패」로 끝난 확인으로 다룬다.</summary>
        private void StartProbeTask(double now, bool judging)
        {
            int seq = _probeSchedule.Start(now, judging);
            _probeStartedAt = now;
            try
            {
                if (_probe == null) _probe = new InternetReachabilityProbe();
                _probeTask = _probe.CheckAsync();
            }
            catch (Exception e)
            {
                // 확인기를 시작조차 못 했다 → 「확인 실패」와 같게 다룬다(U-3 · Plan 2-3 4번 — 1차와 같은 처리).
                SafeWarn("도달 확인 시작 실패 — 확인 실패로 본다", "LinkWatchError", "Stage=ProbeStart, Exception=" + e.GetType().Name);
                _probeTask = Task.FromResult(InternetReachabilityResult.Unreachable);
            }

            // LinkProbeStart — 확인 시작(DiagRev=3). Seq 는 이번 끊김 구간 안 번호, Kind 는 Entry | Periodic | Judge.
            GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkProbeStart",
                DisconnectDiag.Keys("LinkProbeStart",
                    "Seq=" + seq.ToString(CultureInfo.InvariantCulture)
                    + ", Kind=" + _probeSchedule.InFlightKind
                    + ", EpisodeNo=" + _probeSchedule.EpisodeNo.ToString(CultureInfo.InvariantCulture)
                    + ", Phase=" + _monitor.Phase
                    + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(now))));
        }

        /// <summary>끝난 확인의 결과를 일정표에 넣고 LinkProbeResult 를 남긴다.</summary>
        private void HandleProbeCompleted(double now)
        {
            string probeText;
            bool reachable;
            if (_probeTask.IsFaulted || _probeTask.IsCanceled)
            {
                probeText = "Faulted";   // Plan 2-3 4번 — 예외는 도달 불가와 같게
                reachable = false;
            }
            else
            {
                InternetReachabilityResult result = _probeTask.Result;
                probeText = result.ToString(); // Reachable / Unreachable / Skipped
                reachable = result == InternetReachabilityResult.Reachable; // Skipped 는 도달 불가(U-13)
            }
            _probeTask = null;

            InGameLinkProbeOutcome outcome = _probeSchedule.Complete(now, reachable);
            if (!outcome.Stale && outcome.Kind == InGameLinkProbeKind.Judge) _lastJudgeProbeText = probeText;

            // LinkProbeResult — 확인 끝(T-5). ProbeMs 는 확인에 걸린 시간(1차 키 유지).
            //   [2026-11-26 2차 — DiagRev=3] Seq · Kind · AnyFailed(이번 구간 한 번이라도 실패) · Completed · Failed ·
            //   Stale(이미 끝난 지난 구간의 확인 — 판정에 세지 않음) · EpisodeNo · Phase · SilenceSec 을 덧붙였다.
            GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkProbeResult",
                DisconnectDiag.Keys("LinkProbeResult",
                    "Probe=" + probeText + ", ProbeMs=" + Ms(outcome.DurationSec * 1000d)
                    + ", Seq=" + outcome.Seq.ToString(CultureInfo.InvariantCulture)
                    + ", Kind=" + outcome.Kind
                    + ", AnyFailed=" + (_probeSchedule.AnyFailed ? "True" : "False")
                    + ", Completed=" + _probeSchedule.CompletedCount.ToString(CultureInfo.InvariantCulture)
                    + ", Failed=" + _probeSchedule.FailedCount.ToString(CultureInfo.InvariantCulture)
                    + ", Stale=" + (outcome.Stale ? "True" : "False")
                    + ", EpisodeNo=" + outcome.EpisodeNo.ToString(CultureInfo.InvariantCulture)
                    + ", Phase=" + _monitor.Phase
                    + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(now))));
        }

        // ====================================================================
        // [2026-11-26 2차 — 사양 ②] 끊김 구간 · 차단 방식 반영
        // ====================================================================

        /// <summary>
        /// 상태기계 상태로 ① 끊김 구간을 시작/끝내고 ② 차단 방식(덮개/알림 띠/없음)을 정해 ③ NetworkLinkState 에 적는다.
        /// 끊김 구간 = 무응답 지연(Silence) · 「연결 복구 중」(Resume) · 판정 중. RTT 만 느린 지연은 끊김이 아니다(신호는 온다).
        /// </summary>
        private void SyncAndPublish(double now)
        {
            if (_monitor == null) return;
            InGameLinkPhase phase = _monitor.Phase;
            if (phase == InGameLinkPhase.Ended) return; // 결과가 정해진 뒤는 OnMatchResultDecided 가 맡는다

            bool delayed = phase == InGameLinkPhase.Delayed;
            bool gap = phase == InGameLinkPhase.Judging
                || (delayed && (_monitor.DelayTrigger == InGameLinkDelayTrigger.Silence
                                || _monitor.DelayTrigger == InGameLinkDelayTrigger.Resume));

            if (_probeSchedule != null)
            {
                if (gap && !_probeSchedule.EpisodeActive)
                {
                    _probeSchedule.BeginEpisode(now);
                    LogGapEpisode("Begin", now);
                }
                else if (!gap && _probeSchedule.EpisodeActive && phase != InGameLinkPhase.Judging)
                {
                    // 판정 중에는 끝내지 않는다(판정은 뒤집지 않는다). 여기 오는 것은 지연이 풀렸을 때뿐이다.
                    LogGapEpisode("End", now);
                    _probeSchedule.EndEpisode();
                }
            }

            bool anyFailed = _probeSchedule != null && _probeSchedule.EpisodeActive && _probeSchedule.AnyFailed;
            int completed = _probeSchedule != null && _probeSchedule.EpisodeActive ? _probeSchedule.CompletedCount : 0;
            InGameLinkBlockMode mode = InGameLinkBlockPolicy.Decide(_isServer, phase, _monitor.DelayTrigger,
                _monitor.IsRecovering, anyFailed, completed, out InGameLinkBlockReason reason);

            if (mode != _blockMode || reason != _blockReason)
            {
                LogBlockMode(mode, reason, now);
                if (mode == InGameLinkBlockMode.Banner && _blockMode != InGameLinkBlockMode.Banner)
                {
                    _bannerShownCount++;
                    WarnIfBannerMissing();
                }
                if (mode == InGameLinkBlockMode.Overlay && _blockMode != InGameLinkBlockMode.Overlay) _overlayShownCount++;
                _blockMode = mode;
                _blockReason = reason;
            }

            NetworkLinkState.SetLinkState(phase, _monitor.IsRecovering, mode, reason);
        }

        /// <summary>LinkBlockMode — 차단 방식이 바뀔 때 1줄(DiagRev=3). Mode=Overlay|Banner|None, Reason=… .</summary>
        private void LogBlockMode(InGameLinkBlockMode mode, InGameLinkBlockReason reason, double now)
        {
            GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkBlockMode",
                DisconnectDiag.Keys("LinkBlockMode",
                    "Mode=" + mode + ", Reason=" + reason
                    + ", PrevMode=" + _blockMode + ", PrevReason=" + _blockReason
                    + ", Phase=" + _monitor.Phase + ", Trigger=" + _monitor.DelayTrigger
                    + ", BlocksInput=" + (mode == InGameLinkBlockMode.Overlay ? "True" : "False")
                    + ", ProbeCompleted=" + (_probeSchedule != null ? _probeSchedule.CompletedCount : 0).ToString(CultureInfo.InvariantCulture)
                    + ", AnyFailed=" + (_probeSchedule != null && _probeSchedule.AnyFailed ? "True" : "False")
                    + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(now))));
        }

        /// <summary>LinkGapEpisode — 끊김 구간 시작/끝 1줄(DiagRev=3). 끝날 때 그 구간의 확인 집계를 함께 남긴다.</summary>
        private void LogGapEpisode(string op, double now)
        {
            GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkGapEpisode",
                DisconnectDiag.Keys("LinkGapEpisode",
                    "Op=" + op
                    + ", EpisodeNo=" + _probeSchedule.EpisodeNo.ToString(CultureInfo.InvariantCulture)
                    + ", Phase=" + _monitor.Phase + ", Trigger=" + _monitor.DelayTrigger
                    + ", ProbeStarted=" + _probeSchedule.StartedCount.ToString(CultureInfo.InvariantCulture)
                    + ", ProbeCompleted=" + _probeSchedule.CompletedCount.ToString(CultureInfo.InvariantCulture)
                    + ", AnyFailed=" + (_probeSchedule.AnyFailed ? "True" : "False")
                    + ", ProbeInFlight=" + (_probeSchedule.InFlight ? "True" : "False")
                    + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(now))));
        }

        /// <summary>
        /// 결론을 낸다 — 내 승리(상대 이탈) 또는 내 이탈. 이미 결과가 정해졌으면 아무것도 하지 않는다(U-10).
        /// 🔴 판정은 뒤집지 않는다 — 경기당 한 번만 여기를 통과한다.
        /// </summary>
        /// <param name="selfLeft">true = 내 이탈(상대 승리), false = 상대 이탈(내 승리).</param>
        /// <param name="cause">결과 화면 사유.</param>
        /// <param name="probeText">도달 확인 결과 글자(로그용). 확인하지 않았으면 "-".</param>
        /// <param name="now">현재 시각(초).</param>
        private void Conclude(bool selfLeft, MatchEndCause cause, string probeText, double now)
        {
            if (_concluded) return;
            _concluded = true;

            if (NetworkLinkState.IsMatchResultDecided)
            {
                // LinkVerdictSkipped — 이미 다른 경로(성 파괴 · 포기)로 결과가 났다(T-9).
                GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkVerdictSkipped",
                    DisconnectDiag.Keys("LinkVerdictSkipped",
                        "Existing=" + NetworkLinkState.MatchEndCause + ", WouldBe=" + cause));
                return;
            }

            // LinkVerdict — 결론(T-5 · T-6). SinceLastSignalSec 은 판정 순간의 마지막 상대 신호 후 경과.
            GameLog.Dev.Warn(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkVerdict",
                DisconnectDiag.Keys("LinkVerdict",
                    "Verdict=" + (selfLeft ? "SelfLeft" : "OpponentLeft")
                    + ", Cause=" + cause
                    + ", Probe=" + probeText
                    + ", SinceLastSignalSec=" + Sec(_monitor != null ? _monitor.SilenceSeconds(now) : double.NaN)
                    // [2026-11-26 2차 — DiagRev=3] 결론의 근거: 이번 끊김 구간 확인 집계(사양 ① — 한 번이라도 실패면 SelfLeft).
                    //   Probe= 는 판정 시점(Judge) 확인 결과다(1차와 같은 뜻 · 같은 키). 스스로 패배 경로는 "-".
                    + ", AnyFailed=" + (_probeSchedule != null && _probeSchedule.AnyFailed ? "True" : "False")
                    + ", ProbeCompleted=" + (_probeSchedule != null ? _probeSchedule.CompletedCount : 0).ToString(CultureInfo.InvariantCulture)
                    + ", ProbeFailed=" + (_probeSchedule != null ? _probeSchedule.FailedCount : 0).ToString(CultureInfo.InvariantCulture)
                    + ", EpisodeNo=" + (_probeSchedule != null ? _probeSchedule.EpisodeNo : 0).ToString(CultureInfo.InvariantCulture)
                    + ", Mode=" + _blockMode));

            TeamId myTeam = LocalPlayerTeam.Current;
            TeamId winner = selfLeft ? Opponent(myTeam) : myTeam;

            NetworkGameEndController controller = ResolveController();
            if (controller != null)
            {
                controller.ConcludeMatchLocally(winner, cause);
            }
            else
            {
                // 컨트롤러가 이미 파괴됐다(연결 종료 때 씬 오브젝트가 지워지는 경우 — NGO 동작 미확인).
                //   같은 순서(사유 → 결과 발행 → 연결 종료)를 정적 경로로 그대로 밟는다.
                SafeWarn("NetworkGameEndController 없음 — 정적 경로로 결론을 낸다", "LinkWatchError", "Stage=Conclude");
                NetworkGameEndController.PublishLocalConclusion(winner, cause,
                    FindFirstObjectByType<NetworkGameManager>());
            }
        }

        /// <summary>
        /// 「결과가 정해졌다」(어떤 경로든 OnGameEnd) — 감시를 끝내고 덮개를 내린다.
        /// </summary>
        private void OnMatchResultDecided()
        {
            try
            {
                NetworkLinkState.MarkMatchResultDecided();
                if (_monitor == null) return;
                _monitor.MarkEnded();
                if (!_matchEndLogged)
                {
                    _matchEndLogged = true;
                    // LinkWatchStop Reason=MatchEnd — 신호 송신·판정을 멈췄다(씬이 내려갈 때 Reason=SceneUnload 가 한 줄 더 남는다).
                    GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkWatchStop",
                        DisconnectDiag.Keys("LinkWatchStop", "Reason=MatchEnd, Concluded=" + (_concluded ? "True" : "False")
                            + ", Cause=" + NetworkLinkState.MatchEndCause));
                }
                // [2026-11-26 2차] 차단 방식까지 함께 내린다. 옛 줄: NetworkLinkState.SetPhase(InGameLinkPhase.Ended, false);
                _blockMode = InGameLinkBlockMode.None;
                _blockReason = InGameLinkBlockReason.None;
                NetworkLinkState.SetLinkState(InGameLinkPhase.Ended, false, InGameLinkBlockMode.None, InGameLinkBlockReason.None);
            }
            catch (Exception e)
            {
                SafeWarn("경기 종료 반영 실패", "LinkWatchError", "Stage=MatchEnd, Exception=" + e.GetType().Name);
            }
        }

        // ====================================================================
        // 조작 차단 집계
        // ====================================================================

        /// <summary>막힌 명령을 센다. 같은 지연 안에서 명령이 바뀔 때만 1줄 남긴다.</summary>
        private void NoteBlockedCommand(string command)
        {
            try
            {
                _blockedCommandCount++;
                if (command == _lastBlockedCommand) return;
                _lastBlockedCommand = command;
                // CommandBlocked — 지연·판정 중 조작 요청을 보내지 않았다(T-10, 2차 방어).
                GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " CommandBlocked",
                    DisconnectDiag.Keys("CommandBlocked",
                        "Command=" + command + ", State=" + NetworkLinkState.Phase
                        + ", BlockedSoFar=" + _blockedCommandCount.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception)
            {
                // 집계 실패는 차단 자체에 영향이 없다.
            }
        }

        /// <summary>지연 한 번이 시작·끝날 때 집계를 비운다.</summary>
        private void ResetBlockedCommands()
        {
            _lastBlockedCommand = null;
            _blockedCommandCount = 0;
        }

        /// <summary>덮개 UI 가 씬에 없으면 경기당 한 번 경고한다(런타임은 덮개 없이도 조작 차단·판정을 그대로 한다).</summary>
        private void WarnIfOverlayMissing()
        {
            if (_overlayMissingWarned || NetworkLinkState.HasLagOverlay) return;
            _overlayMissingWarned = true;
            // [개발] Warn — 씬 배치 누락(에디터 1회성 메뉴 미실행). 설정 오류라 개발 로그(LogRules 1.3 원칙 3 단서).
            SafeWarn("지연 덮개(NetworkLagOverlayUI)가 씬에 없다 — 화면 표시 없이 조작 차단·판정만 한다",
                     "LagOverlayMissing", "Menu=Hexiege/Setup/Network Lag Overlay - Place In Game Scene");
        }

        /// <summary>
        /// [2026-11-26 2차] 알림 띠 UI 가 씬에 없으면 경기당 한 번 경고한다. 띠가 없어도 게임은 예외 없이 돈다 —
        /// 띠는 조작을 막지 않으므로 「화면에 안내가 안 보인다」 외의 차이는 없다(사양 ⑤).
        /// </summary>
        private void WarnIfBannerMissing()
        {
            if (_bannerMissingWarned || NetworkLinkState.HasLagBanner) return;
            _bannerMissingWarned = true;
            SafeWarn("알림 띠(NetworkLagBannerUI)가 씬에 없다 — 안내 표시 없이 진행한다(조작은 막지 않는다)",
                     "LagBannerMissing", "Menu=Hexiege/Setup/Network Lag Banner - Place In Game Scene");
        }

        // ====================================================================
        // [2026-11-26 2차] 깜빡임 관찰 집계 — 감시가 끝날 때 LinkDelaySummary 1줄
        // ====================================================================

        /// <summary>지연 한 번이 끝났다 — 표시 시간을 집계한다.</summary>
        private void NoteDelayShown(double shownSec)
        {
            if (double.IsNaN(shownSec) || shownSec < 0d) return;
            _totalDelaySec += shownSec;
            if (shownSec > _maxDelaySec) _maxDelaySec = shownSec;
            if (shownSec < InGameLinkMonitor.ShortDelaySec) _shortDelayCount++;
        }

        /// <summary>
        /// LinkDelaySummary — 이번 경기 지연 표시 요약(깜빡임 관찰용). ShortDelayCount 가 많으면 짧게 떴다 사라지기를 반복했다는 뜻.
        /// </summary>
        private void LogDelaySummary(string reason)
        {
            if (_monitor == null) return;
            GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " LinkDelaySummary",
                DisconnectDiag.Keys("LinkDelaySummary",
                    "Reason=" + reason
                    + ", DelayCount=" + _monitor.DelayCount.ToString(CultureInfo.InvariantCulture)
                    + ", ShortDelayCount=" + _shortDelayCount.ToString(CultureInfo.InvariantCulture)
                    + ", ShortDelaySec=" + Sec(InGameLinkMonitor.ShortDelaySec)
                    + ", TotalDelaySec=" + Sec(_totalDelaySec)
                    + ", MaxDelaySec=" + Sec(_maxDelaySec)
                    + ", BannerCount=" + _bannerShownCount.ToString(CultureInfo.InvariantCulture)
                    + ", OverlayCount=" + _overlayShownCount.ToString(CultureInfo.InvariantCulture)
                    + ", ProbeStartedTotal=" + (_probeSchedule != null ? _probeSchedule.TotalStarted : 0).ToString(CultureInfo.InvariantCulture)
                    + ", ProbeFailedTotal=" + (_probeSchedule != null ? _probeSchedule.TotalFailed : 0).ToString(CultureInfo.InvariantCulture)
                    + ", EpisodeCount=" + (_probeSchedule != null ? _probeSchedule.EpisodeNo : 0).ToString(CultureInfo.InvariantCulture)
                    + ", CommandSentCount=" + _commandSentSeq.ToString(CultureInfo.InvariantCulture)));
        }

        // ====================================================================
        // [2026-11-26 2차 — 사양 ④] 조작 처리 확인 진단 — CommandSent ↔ CommandApplied
        //
        // [초급자용 설명]
        //   「끊기기 직전에 보낸 조작이 늦게라도 Host 에서 처리되는가」를 보려면, 보낸 쪽 기록과 Host 의 처리 기록을
        //   같은 번호(Seq)로 이어야 한다. 조작 RPC 12개에 번호 인자를 붙이면 RPC 모양이 바뀌고, 에디터 자체 검증 도구
        //   (RunUnitActionSelfValidation.VerificationRuntime.cs)가 그 RPC 들을 지금 인자 그대로 직접 불러 컴파일이 깨진다.
        //   그래서 조작 RPC 바로 앞에 「조작 도장」 RPC 하나(번호 · 보낸 시각)를 따로 보낸다.
        //   Host 는 도장을 보낸 쪽별 줄(큐)에 넣어 두고, 조작이 처리될 때 가장 오래된 도장을 꺼내 짝을 맞춘다.
        //   ⚠️ 도장 RPC(NetworkGameEndController)와 조작 RPC(각 컨트롤러)는 서로 다른 오브젝트의 RPC 라서
        //      「보낸 순서대로 도착한다」는 것은 NGO 내부 동작에 기대는 가정이다(미확인). 그래서 Host 쪽 처리 순번 RecvSeq 를
        //      함께 남긴다 — 정상이면 Seq 와 RecvSeq 가 같다. 다르면 순서가 바뀌었거나 도장이 빠진 것이다.
        //   ⚠️ DelayMs 는 「Host 시계 − 보낸 쪽 시계」라 두 기기 시계 차가 섞인다(Clock=CrossDevice). 자동 시간 설정 기기면
        //      보통 수십~수백 ms 안이다. Host 자신의 조작은 같은 시계(Clock=Local)다.
        // ====================================================================

        /// <summary>조작 하나를 보낸다 — CommandSent 를 남기고 도장을 보낸다(또는 Host 면 보관한다).</summary>
        private void OnCommandSent(string owner, string command)
        {
            try
            {
                if (_monitor == null) return;
                int seq = ++_commandSentSeq;
                long sentUtc = DisconnectDiag.UtcMsNow();

                // CommandSent — 보낸 쪽 기록(사양 ④). 현재 Phase · 차단 방식도 함께(지연 중 보낸 조작인지 보기 위함).
                GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " CommandSent",
                    DisconnectDiag.Keys("CommandSent",
                        "Owner=" + owner + ", Command=" + command
                        + ", Seq=" + seq.ToString(CultureInfo.InvariantCulture)
                        + ", SentUtcMs=" + sentUtc.ToString(CultureInfo.InvariantCulture)
                        + ", Phase=" + _monitor.Phase + ", Mode=" + _blockMode
                        + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(Now()))));

                if (_isServer)
                {
                    ulong localId = _networkManager != null ? _networkManager.LocalClientId : 0UL;
                    StoreCommandStamp(localId, new CommandStamp(seq, sentUtc, true));
                }
                else
                {
                    NetworkGameEndController controller = ResolveController();
                    if (controller != null) controller.SendCommandStamp(seq, sentUtc);
                }
            }
            catch (Exception)
            {
                // 진단 실패는 조작 자체에 영향이 없다(조작 RPC 는 부른 쪽이 이어서 보낸다).
            }
        }

        /// <summary>[Host] 도장을 보낸 쪽별 줄에 넣는다(상한을 넘으면 가장 오래된 것을 버린다).</summary>
        private void StoreCommandStamp(ulong senderClientId, CommandStamp stamp)
        {
            try
            {
                if (!_commandStamps.TryGetValue(senderClientId, out Queue<CommandStamp> queue))
                {
                    queue = new Queue<CommandStamp>();
                    _commandStamps[senderClientId] = queue;
                }
                if (queue.Count >= MaxPendingStamps) queue.Dequeue();
                queue.Enqueue(stamp);
            }
            catch (Exception)
            {
                // 진단 실패는 무시한다.
            }
        }

        /// <summary>[Host] 조작 요청이 처리 코드에 들어왔다 — 도장과 짝을 맞춰 CommandApplied 를 남긴다.</summary>
        private void OnCommandApplied(string owner, string command, ulong senderClientId)
        {
            try
            {
                if (_monitor == null || !_isServer) return;

                _commandRecvSeq.TryGetValue(senderClientId, out int recvSeq);
                recvSeq++;
                _commandRecvSeq[senderClientId] = recvSeq;

                string seqText = DisconnectDiag.None;
                string delayText = DisconnectDiag.None;
                string clockText = DisconnectDiag.None;
                if (_commandStamps.TryGetValue(senderClientId, out Queue<CommandStamp> queue) && queue.Count > 0)
                {
                    CommandStamp stamp = queue.Dequeue();
                    seqText = stamp.Seq.ToString(CultureInfo.InvariantCulture);
                    delayText = (DisconnectDiag.UtcMsNow() - stamp.SentUtcMs).ToString(CultureInfo.InvariantCulture);
                    clockText = stamp.Local ? "Local" : "CrossDevice";
                }

                // CommandApplied — Host 처리 기록(사양 ④). Seq=- 면 짝 도장이 없었다(도장 유실 · 순서 바뀜 — 미확인 가정 위반).
                GameLog.Dev.Info(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " CommandApplied",
                    DisconnectDiag.Keys("CommandApplied",
                        "Owner=" + owner + ", Command=" + command
                        + ", FromClientId=" + senderClientId.ToString(CultureInfo.InvariantCulture)
                        + ", Seq=" + seqText
                        + ", RecvSeq=" + recvSeq.ToString(CultureInfo.InvariantCulture)
                        + ", DelayMs=" + delayText + ", Clock=" + clockText
                        + ", Phase=" + _monitor.Phase + ", Mode=" + _blockMode
                        + ", SilenceSec=" + Sec(_monitor.SilenceSeconds(Now()))));
            }
            catch (Exception)
            {
                // 진단 실패는 조작 처리에 영향이 없다.
            }
        }

        // ====================================================================
        // 보조
        // ====================================================================

        /// <summary>신호 RPC · 결론을 맡길 컨트롤러. 파괴됐으면(Unity null) 한 번 다시 찾는다.</summary>
        private NetworkGameEndController ResolveController()
        {
            if (_endController == null)
                _endController = FindFirstObjectByType<NetworkGameEndController>();
            return _endController;
        }

        /// <summary>앱 시작 후 실제 경과(초). Time.timeScale 의 영향을 받지 않는다(결과 화면에서 0 이 될 수 있다).</summary>
        private static double Now() => Time.realtimeSinceStartupAsDouble;

        /// <summary>상대 팀(Blue ↔ Red).</summary>
        private static TeamId Opponent(TeamId team) => team == TeamId.Blue ? TeamId.Red : TeamId.Blue;

        /// <summary>초 값을 소수 1자리로(NaN 이면 "-").</summary>
        private static string Sec(double seconds)
            => double.IsNaN(seconds) ? DisconnectDiag.None : seconds.ToString("F1", CultureInfo.InvariantCulture);

        /// <summary>ms 값을 정수로(NaN 이면 "-").</summary>
        private static string Ms(double ms)
            => double.IsNaN(ms) ? DisconnectDiag.None : ((long)Math.Round(ms)).ToString(CultureInfo.InvariantCulture);

        /// <summary>[2026-11-26 2차] 초 값을 정수 ms 로(NaN 이면 "-").</summary>
        private static string MsFromSec(double seconds)
            => double.IsNaN(seconds) ? DisconnectDiag.None : Ms(seconds * 1000d);

        /// <summary>실행 중 UnityTransport 의 끊김 시간 직렬화 값을 읽는다(읽기 전용). 못 읽으면 "-".</summary>
        private static string ReadDisconnectTimeoutText(NetworkManager networkManager)
        {
            try
            {
                var transport = networkManager.NetworkConfig != null
                    ? networkManager.NetworkConfig.NetworkTransport as UnityTransport : null;
                if (transport == null) return DisconnectDiag.None;
                FieldInfo field = typeof(UnityTransport).GetField(
                    DisconnectTimeoutField, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                object value = field?.GetValue(transport);
                return value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : DisconnectDiag.None;
            }
            catch (Exception)
            {
                return DisconnectDiag.None;
            }
        }

        /// <summary>감시기 자신의 실패를 남긴다. 이 기록마저 실패해도 밖으로 예외를 내보내지 않는다.</summary>
        private static void SafeWarn(string message, string eventName, string extra)
        {
            try
            {
                GameLog.Dev.Warn(LogSystem, nameof(InGameLinkWatch), DisconnectDiag.MessagePrefix + " " + message,
                                 DisconnectDiag.Keys(eventName, extra));
            }
            catch (Exception)
            {
                // 더 할 수 있는 것이 없다 — 게임 흐름을 지키는 것이 우선이다.
            }
        }

        /// <summary>
        /// 에디터 「도메인 리로드 없이 플레이」 대비 — 플레이 시작 때 정적 값을 비운다.
        /// 🔴 #if 밖이다(릴리스에서도 정적 값이 깨끗하게 시작해야 판정이 맞다).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_current = null;
            NetworkLinkState.ResetForNewRuntime();
            NetworkContextResetDeferral.ResetForNewRuntime();
        }
    }

    // ========================================================================
    // NetworkContext.Reset 미루기 — Plan X-2 (「혼자 진행 모드」 입구 막기)
    // ========================================================================

    /// <summary>
    /// [Plan X-2] 경기 중 예상 밖 디스폰(연결이 갑자기 내려감)일 때 NetworkContext.Reset 을
    /// Game 씬이 내려갈 때까지 미룬다.
    ///
    /// <para>
    /// [초급자용 설명] 왜 미루는가 — NetworkContext 의 「멀티 경기 중」 표시가 꺼지면 게임 곳곳(200곳 넘는 분기)이
    /// 싱글플레이처럼 동작하기 시작한다(Research 2-1 「혼자 진행 모드」). 연결이 끊긴 순간 그 표시가 꺼지면
    /// Client 기기가 혼자 게임을 진행해 버린다. 그래서 끊긴 뒤에도 씬이 남아 있는 동안은 표시를 켜 둔 채로
    /// 판정 → 결과 화면 → 로비로 가게 하고, 씬이 내려갈 때 반드시 끈다(다음 경기·싱글에 남지 않게).
    /// </para>
    /// </summary>
    internal static class NetworkContextResetDeferral
    {
        private static bool s_pending;        // 미뤄 둔 Reset 이 있는가
        private static int s_sceneHandle;     // 그 Reset 을 실행할 씬(이 씬이 내려가면 실행)

        /// <summary>미뤄 둔 Reset 이 있는가.</summary>
        public static bool IsPending => s_pending;

        /// <summary>
        /// Reset 을 미룬다. 씬이 유효하지 않으면 미룰 기준이 없으므로 곧바로 실행한다.
        /// </summary>
        /// <param name="scene">기다릴 씬(NetworkCombatController 가 있는 Game 씬).</param>
        /// <param name="className">로그 Class 이름(부른 쪽).</param>
        public static void Defer(Scene scene, string className)
        {
            if (s_pending) return;
            if (!scene.IsValid())
            {
                Execute("InvalidScene", className);
                return;
            }
            s_pending = true;
            s_sceneHandle = scene.handle;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            // SoloModeBlocked — 경기 중 예상 밖 디스폰에서 Reset 을 미뤘다(T-8).
            GameLog.Dev.Warn("Network", className, DisconnectDiag.MessagePrefix + " SoloModeBlocked",
                DisconnectDiag.Keys("SoloModeBlocked", "Deferred=True"));
        }

        /// <summary>
        /// 같은 경기 컨트롤러가 다시 스폰됐다(NetworkContext.Set 이 다시 불린다) — 미뤄 둔 Reset 을 취소한다.
        /// </summary>
        /// <param name="className">로그 Class 이름(부른 쪽).</param>
        public static void CancelForRespawn(string className)
        {
            if (!s_pending) return;
            s_pending = false;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            GameLog.Dev.Info("Network", className, DisconnectDiag.MessagePrefix + " NetworkContextReset",
                DisconnectDiag.Keys("NetworkContextReset", "At=CancelledByRespawn"));
        }

        /// <summary>씬이 내려갔다 — 기다리던 씬이면 미뤄 둔 Reset 을 실행한다.</summary>
        private static void OnSceneUnloaded(Scene scene)
        {
            if (!s_pending)
            {
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                return;
            }
            if (scene.handle != s_sceneHandle) return;
            Execute("SceneUnload", nameof(NetworkContextResetDeferral));
        }

        /// <summary>미뤄 둔 Reset 을 실행한다(원래 NetworkCombatController.OnNetworkDespawn 이 하던 그 한 줄).</summary>
        private static void Execute(string at, string className)
        {
            s_pending = false;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            NetworkContext.Reset();
            // NetworkContextReset — 미뤘던 Reset 실행(T-8).
            GameLog.Dev.Info("Network", className, DisconnectDiag.MessagePrefix + " NetworkContextReset",
                DisconnectDiag.Keys("NetworkContextReset", "At=" + at));
        }

        /// <summary>플레이 시작 때 정적 값을 비운다(InGameLinkWatch.ResetStatics 가 부른다).</summary>
        public static void ResetForNewRuntime()
        {
            s_pending = false;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }
    }
}
