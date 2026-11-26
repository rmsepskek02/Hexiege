// ============================================================================
// InGameDisconnectObserver.cs
// 인게임 연결 끊김 「실측」용 — 기록만 하는 관찰자 (개발 빌드 전용).
//
// [무엇을 하나 — 초급 개발자용 설명]
//   경기 도중 연결이 끊기거나 앱이 내려갔다 올라올 때, 그 순간을 로그 한 줄씩으로 남긴다.
//   기기 두 대의 로그 시각(UtcMs)을 비교해 「상대가 끊긴 것을 몇 초 뒤에 알았나」를 재기 위함이다
//   (task: _Tasks/2026-11-26/05_28_ingame-disconnect-measure/Plan.md 3절 ⓪①②④⑤⑨).
//
// 🔴 게임 동작은 한 줄도 바꾸지 않는다:
//   - NGO 콜백은 기존 구독자(ReconnectionHandler · NetworkGameManager)와 **따로** 구독한다.
//     C# 이벤트에 새 구독자를 더해도 기존 구독자의 호출 순서는 바뀌지 않는다(새 구독자는 맨 뒤에 붙는다).
//   - 모든 처리기 본문은 try/catch 로 감쌌다. C# 이벤트는 구독자 하나가 예외를 던지면
//     **그 뒤 구독자가 호출되지 않을 수 있으므로**, 여기서 난 예외가 밖으로 나가면 안 된다.
//   - 게임 상태(연결·대기 시간·승패·Time.timeScale)를 읽기만 하고 쓰지 않는다. RPC 를 보내지 않는다.
//   - 이 컴포넌트는 NetworkObject 가 아닌 **별도의 빈 GameObject** 에 붙는다(씬 파일 수정 없음).
//
// [언제 만들어지고 언제 사라지나]
//   - 만듦: NetworkCombatController.OnNetworkSpawn(개발 빌드 블록) → BeginMatch().
//           Host·Client 양쪽 모두 이 시점에 역할(IsServer)이 확정되므로 「경기 시작 기준점」으로 쓴다.
//   - 사라짐: Game 씬이 내려갈 때(OnDestroy) — 그때 경기 요약(MatchSummary)을 1줄 남긴다.
//     NGO 연결이 끊긴 뒤에도 씬이 남아 있는 동안은 계속 기록한다(결과 화면·앱 복귀 순간을 놓치지 않으려고).
//
// [로그 빈도 — LogRules §1.14 금지 8]
//   상태가 바뀔 때만 1줄. 1초마다 하는 RTT 읽기는 **로그를 남기지 않고** 값만 보관한다.
//
// [⑥ 「상대 신호 끊김·회복」을 넣지 않은 이유]
//   RTT 값(UnityTransport.GetCurrentRtt)은 「마지막으로 측정해 둔 값」을 돌려줄 뿐이라,
//   상대가 조용해져도 값이 그대로 남는다. 「상대가 사라져서 멈춘 것」과 「보낼 것이 없어서 멈춘 것」을
//   구별할 수 없어 끊김 신호로 쓰면 틀린 측정이 된다. 그래서 RTT 는 참고값(LastRttMs)으로만 남긴다.
//
// Infrastructure 레이어 — Unity.Netcode 사용 허용. 개발 빌드(UNITY_EDITOR / DEVELOPMENT_BUILD)에서만 컴파일된다.
// ============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Globalization;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Application; // GameLog

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 인게임 연결 끊김 실측용 기록 전용 관찰자. NGO 콜백·앱 일시정지/포커스를 로그로만 남기고
    /// 어떤 게임 상태도 바꾸지 않는다. 개발 빌드 전용.
    /// </summary>
    internal sealed class InGameDisconnectObserver : MonoBehaviour
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>로그 카테고리 System. 앱 일시정지도 「끊김 측정」 기능이라 Network 로 둔다(Plan 2절).</summary>
        private const string LogSystem = "Network";

        /// <summary>RTT 참고값을 읽는 간격(초). 읽기만 하고 로그는 남기지 않는다.</summary>
        private const double RttSampleIntervalSeconds = 1d;

        /// <summary>
        /// 실행 중인 UnityTransport 설정을 읽을 때 쓰는 직렬화 필드 이름.
        /// 근거: Game.unity 에 저장된 UnityTransport 컴포넌트의 필드 이름(m_DisconnectTimeoutMS 등).
        /// 공개 프로퍼티 이름은 이 패키지 버전(Transport 2.6.0 / NGO 2.9.2)에서 확인하지 못해,
        /// 컴파일 실패 위험이 없는 리플렉션 읽기를 쓴다. 못 찾으면 "-" 로 남는다(읽기 전용 — 값을 바꾸지 않는다).
        /// </summary>
        private const string DisconnectTimeoutField = "m_DisconnectTimeoutMS";
        private const string HeartbeatTimeoutField = "m_HeartbeatTimeoutMS";
        private const string MaxConnectAttemptsField = "m_MaxConnectAttempts";

        // ====================================================================
        // 정적 — 현재 살아 있는 관찰자 1개
        // ====================================================================

        /// <summary>현재 경기의 관찰자. 씬이 내려가 파괴되면 Unity null 비교로 null 이 된다.</summary>
        private static InGameDisconnectObserver s_current;

        // ====================================================================
        // 인스턴스 상태 — 모두 기록용
        // ====================================================================

        private NetworkManager _networkManager;   // 구독한 NetworkManager(해제할 때 같은 대상에서 빼기 위해 보관)
        private UnityTransport _transport;        // RTT 참고값을 읽을 Transport(없으면 null)
        private bool _subscribed;                 // NGO 콜백 구독 중인가
        private bool _summaryWritten;             // 이번 경기 요약을 이미 남겼는가(경기당 1회)
        private bool _rttReadFailed;              // RTT 읽기가 한 번 실패하면 더 읽지 않는다(예외 반복 방지)
        private double _nextRttSampleAt;          // 다음 RTT 읽기 시각(realtime 초)
        private string _lastRttText = DisconnectDiag.None; // 마지막으로 읽은 RTT(ms) 문자열
        private long _pausedAtUtcMs = -1;         // 마지막 일시정지 시각(UtcMs). -1 = 없음

        // ====================================================================
        // 진입점 — NetworkCombatController.OnNetworkSpawn(개발 빌드 블록)에서 1회
        // ====================================================================

        /// <summary>
        /// 경기 시작 — 관찰자를 만들거나(없으면) 재사용하고 MatchStart 를 1줄 남긴다.
        /// 이전 경기의 관찰자가 아직 살아 있으면(같은 씬에서 다음 경기) 그 경기 요약을 먼저 남긴다.
        /// 예외를 밖으로 던지지 않는다 — 부른 쪽(NetworkCombatController.OnNetworkSpawn)의 나머지 초기화를 지키기 위함.
        /// </summary>
        /// <param name="networkManager">실행 중인 NetworkManager.</param>
        /// <param name="isServer">이 기기가 서버(Host)인가.</param>
        /// <param name="scene">관찰자를 둘 씬(부른 쪽 오브젝트의 씬 — Game 씬이 내려갈 때 같이 사라지게).</param>
        public static void BeginMatch(NetworkManager networkManager, bool isServer, Scene scene)
        {
            try
            {
                if (networkManager == null) return;

                InGameDisconnectObserver observer = s_current;
                if (observer != null)
                {
                    observer.FinishMatch("NextMatchStarted");
                }
                else
                {
                    // NetworkObject 가 아닌 빈 GameObject — NGO 스폰/디스폰과 무관하게 씬 수명만 따른다.
                    var go = new GameObject(nameof(InGameDisconnectObserver));
                    if (scene.IsValid() && scene.isLoaded && go.scene != scene)
                        SceneManager.MoveGameObjectToScene(go, scene);
                    observer = go.AddComponent<InGameDisconnectObserver>();
                    s_current = observer;
                }

                observer.StartMatch(networkManager, isServer);
            }
            catch (Exception e)
            {
                SafeWarn("관찰자 시작 실패 — 이번 경기 진단 로그 일부가 빠질 수 있다",
                         "ObserverError", "Stage=Begin, Exception=" + e.GetType().Name);
            }
        }

        // ====================================================================
        // 경기 시작 / 끝
        // ====================================================================

        /// <summary>구독을 걸고 ⓪ MatchStart 를 남긴다.</summary>
        private void StartMatch(NetworkManager networkManager, bool isServer)
        {
            Unsubscribe(); // 혹시 남은 구독이 있으면 먼저 뺀다(같은 처리기가 두 번 불리지 않게)

            _networkManager = networkManager;
            _transport = networkManager.NetworkConfig != null
                ? networkManager.NetworkConfig.NetworkTransport as UnityTransport
                : null;
            _summaryWritten = false;
            _rttReadFailed = false;
            _nextRttSampleAt = 0d;
            _lastRttText = DisconnectDiag.None;
            _pausedAtUtcMs = -1;

            int matchNo = DisconnectDiag.BeginMatch(isServer);

            // ⓪ MatchStart — 측정 기준점 + 「실제로 실행 중인」 전송 설정값.
            //   DisconnectTimeoutMs / HeartbeatTimeoutMs / MaxConnectAttempts : 지금 돌고 있는 Transport 의 값
            //     (씬 값이 아니라 실행 중 인스턴스에서 읽는다 — 다른 씬의 NetworkManager 가 쓰일 수도 있어서)
            //   RunInBackground : 앱이 내려가 있을 때도 Unity 가 계속 도는 설정인가(끊김 시점에 영향)
            //   AppVersion      : 어느 빌드의 로그인지 가르기 위함
            //   LocalClientId   : 이 기기의 NGO 세션 번호(계정 식별자 아님)
            GameLog.Dev.Info(LogSystem, nameof(InGameDisconnectObserver), DisconnectDiag.MessagePrefix + " MatchStart",
                DisconnectDiag.Keys("MatchStart",
                    "MatchNo=" + matchNo.ToString(CultureInfo.InvariantCulture)
                    + ", DisconnectTimeoutMs=" + ReadTransportField(DisconnectTimeoutField)
                    + ", HeartbeatTimeoutMs=" + ReadTransportField(HeartbeatTimeoutField)
                    + ", MaxConnectAttempts=" + ReadTransportField(MaxConnectAttemptsField)
                    + ", RunInBackground=" + (UnityEngine.Application.runInBackground ? "True" : "False")
                    + ", AppVersion=" + DisconnectDiag.SanitizeReason(UnityEngine.Application.version)
                    + ", LocalClientId=" + networkManager.LocalClientId.ToString(CultureInfo.InvariantCulture)));

            // 구독 표시를 먼저 세운다 — 세 줄 중 중간에 실패해도 Unsubscribe 가 이미 붙은 것을 뺄 수 있게.
            // (C# 이벤트에서 붙지 않은 처리기를 빼는 것은 아무 일도 일어나지 않는 안전한 동작이다.)
            _subscribed = true;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnTransportFailure += HandleTransportFailure;
        }

        /// <summary>
        /// ⑨ MatchSummary 를 경기당 1번만 남기고 구독을 푼다.
        /// </summary>
        /// <param name="endReason">요약을 남긴 계기(SceneUnloaded / NextMatchStarted).</param>
        private void FinishMatch(string endReason)
        {
            try
            {
                if (!_summaryWritten && DisconnectDiag.IsMatchActive)
                {
                    _summaryWritten = true;
                    // ⑨ MatchSummary — 이번 경기에서 각 사건이 몇 번 일어났는지.
                    //   DisconnectNo / ConnectNo / PauseNo / TransportFailureNo : 각 사건 횟수
                    //   ResultShown : 결과 화면이 떴는가(Host 가 사라졌을 때 Client 에 결과 화면이 뜨는지 확인용)
                    //   EndReason   : 이 요약을 남긴 계기
                    GameLog.Dev.Info(LogSystem, nameof(InGameDisconnectObserver), DisconnectDiag.MessagePrefix + " MatchSummary",
                        DisconnectDiag.Keys("MatchSummary", DisconnectDiag.SummaryText() + ", EndReason=" + endReason));
                }
            }
            catch (Exception e)
            {
                SafeWarn("경기 요약 기록 실패", "ObserverError", "Stage=Summary, Exception=" + e.GetType().Name);
            }
            finally
            {
                DisconnectDiag.EndMatch();
                Unsubscribe();
            }
        }

        /// <summary>NGO 콜백 구독 해제. 예외를 밖으로 던지지 않는다.</summary>
        private void Unsubscribe()
        {
            try
            {
                if (_subscribed && _networkManager != null)
                {
                    _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                    _networkManager.OnClientConnectedCallback -= HandleClientConnected;
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
        // NGO 콜백 처리기 — 기록만
        // ====================================================================

        /// <summary>
        /// ① PeerDisconnected — NGO 가 「연결이 끊겼다」고 알린 순간.
        /// Host: 상대 클라이언트가 끊김 / Client: 서버와의 연결이 끊김(이때 clientId 가 무엇인지도 로그로 본다).
        /// </summary>
        private void HandleClientDisconnected(ulong clientId)
        {
            try
            {
                ulong localId = _networkManager != null ? _networkManager.LocalClientId : 0UL;
                bool isSelf = _networkManager != null && clientId == localId;
                // DisconnectReason: NGO 가 남긴 끊김 사유 문장(없으면 "-"). 끊김 콜백 이후에 읽는 값이다.
                string reason = DisconnectDiag.SanitizeReason(_networkManager != null ? _networkManager.DisconnectReason : null);
                int disconnectNo = DisconnectDiag.NotePeerDisconnected(clientId, isSelf);

                // 각 키의 뜻:
                //   ClientId       : 콜백이 준 NGO ClientId
                //   LocalClientId  : 이 기기의 NGO ClientId (ClientId 와 같으면 IsSelf=True)
                //   Reason         : NGO DisconnectReason(다듬은 값)
                //   LastRttMs      : 끊기기 전 마지막으로 읽어 둔 RTT(ms). 참고값 — 끊김 신호가 아니다
                //   SinceResumeSec : 마지막 앱 복귀 후 몇 초 만에 끊김을 받았나(복귀 직후 끊김인지 보기 위함)
                //   DisconnectNo   : 이번 경기에서 몇 번째 끊김 콜백인가
                GameLog.Dev.Warn(LogSystem, nameof(InGameDisconnectObserver), DisconnectDiag.MessagePrefix + " PeerDisconnected",
                    DisconnectDiag.Keys("PeerDisconnected",
                        "ClientId=" + clientId.ToString(CultureInfo.InvariantCulture)
                        + ", LocalClientId=" + localId.ToString(CultureInfo.InvariantCulture)
                        + ", IsSelf=" + (isSelf ? "True" : "False")
                        + ", Reason=" + reason
                        + ", LastRttMs=" + _lastRttText
                        + ", SinceResumeSec=" + DisconnectDiag.SinceResumeText()
                        + ", DisconnectNo=" + disconnectNo.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception e)
            {
                SafeWarn("끊김 콜백 기록 실패", "ObserverError", "Stage=PeerDisconnected, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>④ PeerConnected — 경기 시작 이후 들어온 연결(재접속 후보) 콜백.</summary>
        private void HandleClientConnected(ulong clientId)
        {
            try
            {
                bool isSelf = _networkManager != null && clientId == _networkManager.LocalClientId;
                int connectNo = DisconnectDiag.NotePeerConnected();

                // SameAsLastDisconnected : 마지막으로 끊긴 상대와 같은 ClientId 인가(다르면 ReconnectionHandler 는
                //                          대기를 취소하지 않는다 — 그 조건이 실제로 어떻게 나오는지 보기 위함)
                GameLog.Dev.Info(LogSystem, nameof(InGameDisconnectObserver), DisconnectDiag.MessagePrefix + " PeerConnected",
                    DisconnectDiag.Keys("PeerConnected",
                        "ClientId=" + clientId.ToString(CultureInfo.InvariantCulture)
                        + ", IsSelf=" + (isSelf ? "True" : "False")
                        + ", SameAsLastDisconnected=" + DisconnectDiag.SameAsLastDisconnectedText(clientId)
                        + ", ConnectNo=" + connectNo.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception e)
            {
                SafeWarn("연결 콜백 기록 실패", "ObserverError", "Stage=PeerConnected, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>② TransportFailure — 전송 계층이 실패를 알린 순간(그 뒤 NGO 가 스스로 종료한다).</summary>
        private void HandleTransportFailure()
        {
            try
            {
                int failureNo = DisconnectDiag.NoteTransportFailure();
                GameLog.Dev.Warn(LogSystem, nameof(InGameDisconnectObserver), DisconnectDiag.MessagePrefix + " TransportFailure",
                    DisconnectDiag.Keys("TransportFailure",
                        "LastRttMs=" + _lastRttText
                        + ", SinceResumeSec=" + DisconnectDiag.SinceResumeText()
                        + ", TransportFailureNo=" + failureNo.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception e)
            {
                SafeWarn("전송 실패 콜백 기록 실패", "ObserverError", "Stage=TransportFailure, Exception=" + e.GetType().Name);
            }
        }

        // ====================================================================
        // Unity 메시지 — 앱 일시정지·포커스(⑤), RTT 참고값 읽기, 파괴(⑨)
        // ====================================================================

        /// <summary>
        /// RTT 참고값을 1초에 한 번 읽어 보관만 한다(로그 없음).
        /// NetworkStatusUI 가 이미 0.5초마다 같은 값을 읽어 화면에 띄우고 있어(NetworkGameManager.GetCurrentRttMs),
        /// 같은 읽기를 한 번 더 하는 것은 게임에 영향이 없다.
        /// </summary>
        private void Update()
        {
            if (_rttReadFailed || _transport == null || _networkManager == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextRttSampleAt) return;
            _nextRttSampleAt = now + RttSampleIntervalSeconds;

            try
            {
                if (!_networkManager.IsListening) return;
                // NetworkGameManager.GetCurrentRttMs 와 같은 호출(Host/Client 모두 ServerClientId).
                _lastRttText = _transport.GetCurrentRtt(NetworkManager.ServerClientId).ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                _rttReadFailed = true; // 매 초 같은 예외가 쌓이지 않게 한 번만 알리고 멈춘다
                _lastRttText = DisconnectDiag.None;
                SafeWarn("RTT 참고값 읽기 실패 — 이후 LastRttMs 는 '-'", "ObserverError", "Stage=RttRead, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>
        /// ⑤ AppPause — 앱이 내려가거나(Paused=True) 다시 올라온(Paused=False) 순간.
        /// 복귀 때 PausedForSec(내려가 있던 시간, UtcMs 차)과 그 순간의 연결 상태를 함께 남긴다.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            try
            {
                if (!DisconnectDiag.IsMatchActive) return;

                long nowUtc = DisconnectDiag.UtcMsNow();
                string pausedFor = DisconnectDiag.None;
                if (paused)
                {
                    _pausedAtUtcMs = nowUtc;
                    DisconnectDiag.NotePaused();
                }
                else
                {
                    if (_pausedAtUtcMs >= 0)
                        pausedFor = ((nowUtc - _pausedAtUtcMs) / 1000d).ToString("F1", CultureInfo.InvariantCulture);
                    _pausedAtUtcMs = -1;
                    DisconnectDiag.NoteResumed();
                }

                // 각 키의 뜻:
                //   Paused            : True = 내려감 / False = 복귀
                //   PausedForSec      : (복귀 때만) 내려가 있던 시간. 기기 시계(UtcMs) 차로 잰다
                //   IsListening       : NGO 가 아직 동작 중인가
                //   IsConnectedClient : 이 기기가 아직 연결된 상태로 보이는가
                //   ConnectedCount    : (Host 만) 연결된 클라이언트 수(Host 자신 포함). Client 는 "-"
                GameLog.Dev.Info(LogSystem, nameof(InGameDisconnectObserver), DisconnectDiag.MessagePrefix + " AppPause",
                    DisconnectDiag.Keys("AppPause",
                        "Paused=" + (paused ? "True" : "False")
                        + ", PausedForSec=" + pausedFor
                        + ConnectionStateText()));
            }
            catch (Exception e)
            {
                SafeWarn("앱 일시정지 기록 실패", "ObserverError", "Stage=AppPause, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>⑤ AppFocus — 앱이 포커스를 잃거나(Focused=False) 되찾은(Focused=True) 순간.</summary>
        private void OnApplicationFocus(bool focused)
        {
            try
            {
                if (!DisconnectDiag.IsMatchActive) return;
                GameLog.Dev.Info(LogSystem, nameof(InGameDisconnectObserver), DisconnectDiag.MessagePrefix + " AppFocus",
                    DisconnectDiag.Keys("AppFocus", "Focused=" + (focused ? "True" : "False")));
            }
            catch (Exception e)
            {
                SafeWarn("앱 포커스 기록 실패", "ObserverError", "Stage=AppFocus, Exception=" + e.GetType().Name);
            }
        }

        /// <summary>Game 씬이 내려갈 때 — 경기 요약(⑨)을 남기고 구독을 푼다.</summary>
        private void OnDestroy()
        {
            FinishMatch("SceneUnloaded");
            if (s_current == this) s_current = null;
        }

        // ====================================================================
        // 보조
        // ====================================================================

        /// <summary>AppPause 줄에 붙는 연결 상태 키(", IsListening=…, IsConnectedClient=…, ConnectedCount=…").</summary>
        private string ConnectionStateText()
        {
            if (_networkManager == null)
                return ", IsListening=-, IsConnectedClient=-, ConnectedCount=-";

            string count = DisconnectDiag.None;
            if (_networkManager.IsServer && _networkManager.IsListening)
                count = _networkManager.ConnectedClientsIds.Count.ToString(CultureInfo.InvariantCulture);

            return ", IsListening=" + (_networkManager.IsListening ? "True" : "False")
                + ", IsConnectedClient=" + (_networkManager.IsConnectedClient ? "True" : "False")
                + ", ConnectedCount=" + count;
        }

        /// <summary>
        /// 실행 중인 UnityTransport 의 직렬화 필드 값을 읽어 문자열로 돌려준다(읽기 전용). 못 읽으면 "-".
        /// </summary>
        private string ReadTransportField(string fieldName)
        {
            try
            {
                if (_transport == null) return DisconnectDiag.None;
                FieldInfo field = typeof(UnityTransport).GetField(
                    fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                object value = field?.GetValue(_transport);
                return value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : DisconnectDiag.None;
            }
            catch (Exception)
            {
                return DisconnectDiag.None;
            }
        }

        /// <summary>관찰자 자신의 실패를 남긴다. 이 기록마저 실패해도 밖으로 예외를 내보내지 않는다.</summary>
        private static void SafeWarn(string message, string eventName, string extra)
        {
            try
            {
                GameLog.Dev.Warn(LogSystem, nameof(InGameDisconnectObserver), DisconnectDiag.MessagePrefix + " " + message,
                                 DisconnectDiag.Keys(eventName, extra));
            }
            catch (Exception)
            {
                // 더 할 수 있는 것이 없다 — 게임 흐름을 지키는 것이 우선이다.
            }
        }

        /// <summary>
        /// 에디터 「도메인 리로드 없이 플레이」 대비 — 플레이 시작 때 정적 값을 비운다(MatchNo 가 1부터 다시 시작).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_current = null;
            DisconnectDiag.ResetForNewRuntime();
        }
    }
}
#endif
