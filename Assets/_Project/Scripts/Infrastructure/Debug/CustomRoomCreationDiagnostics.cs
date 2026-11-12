using System;
using System.Diagnostics;
using System.Threading;
using Hexiege.Application;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Relay;

namespace Hexiege.Infrastructure
{
    /// <summary>커스텀 방 생성의 개발 진단만 소유한다. 서비스/인증/재시도 상태는 변경하지 않는다.</summary>
    public sealed class CustomRoomCreationDiagnostics
    {
        public enum Stage
        {
            UiRequested, UiManagerAvailable, PreparationCleanup, HostEntry, RelayAllocate,
            RelayJoinCode, RelayTransport, RelayReturned, LobbyCreate, LobbyReturned,
            NetworkCallbacks, NetworkHost, Heartbeat, HostNotified, HostUnhandled, UiUnhandled, UiReturned
        }
        public enum Outcome { Begin, Done, Missing, Failed, Returned }
        private static long _nextAttempt;
        private readonly long _attempt, _started;
        private Stage _lastStage;

        private CustomRoomCreationDiagnostics()
        {
            _attempt = Interlocked.Increment(ref _nextAttempt);
            _started = Stopwatch.GetTimestamp();
        }

        public static CustomRoomCreationDiagnostics Create()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try { return new CustomRoomCreationDiagnostics(); } catch { return null; }
#else
            return null;
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Record(Stage stage, Outcome outcome, bool? result = null, Exception error = null)
        {
            try
            {
                Stage previous = _lastStage;
                _lastStage = stage;
                int? code = (error as RequestFailedException)?.ErrorCode;
                int? reason = error is RelayServiceException relay ? (int?)relay.Reason
                    : error is LobbyServiceException lobby ? (int?)lobby.Reason : null;
                string fields = FormattableString.Invariant(
                    $"Attempt={_attempt}, Stage={stage}, Outcome={outcome}, PreviousStage={previous}, ElapsedMs={(Stopwatch.GetTimestamp() - _started) * 1000.0 / Stopwatch.Frequency:F3}, Result={(result.HasValue ? result.Value.ToString() : "Unknown")}, ExceptionType={ClassifyException(error)}, InnerExceptionType={ClassifyException(error?.InnerException)}, SdkErrorCode={(code.HasValue ? code.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown")}, SdkReasonCode={(reason.HasValue ? reason.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Unknown")}, HttpStatus=Unknown");
                GameLog.Dev.Info("Network", nameof(CustomRoomCreationDiagnostics), "[CUSTOM-ROOM-CREATE]", fields);
            }
            catch { /* 진단 실패를 기존 방 생성 흐름으로 전파하지 않는다. */ }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void RecordHostState(bool initializerReady)
        {
            try
            {
                bool initialized = UnityServices.State == ServicesInitializationState.Initialized;
                bool authReadable = false, signedIn = false;
                if (initialized)
                    try { signedIn = AuthenticationService.Instance.IsSignedIn; authReadable = true; } catch { }
                var network = NetworkManager.Singleton;
                bool present = network != null;
                GameLog.Dev.Info("Network", nameof(CustomRoomCreationDiagnostics), "[CUSTOM-ROOM-CREATE-STATE]",
                    FormattableString.Invariant($"Attempt={_attempt}, Stage=HostEntry, InitializerReady={initializerReady}, UgsInitialized={initialized}, UgsAuthReadable={authReadable}, UgsSignedIn={signedIn}, NetworkManagerPresent={present}, TransportPresent={(present && network.GetComponent<UnityTransport>() != null)}, IsListening={(present && network.IsListening)}, IsHost={(present && network.IsHost)}, IsClient={(present && network.IsClient)}"));
            }
            catch { /* 상태 조회 실패도 기존 흐름을 바꾸지 않는다. */ }
        }

        private static string ClassifyException(Exception error)
        {
            if (error == null) return "None";
            if (error is RelayServiceException) return "RelayServiceException";
            if (error is LobbyServiceException) return "LobbyServiceException";
            if (error is RequestFailedException) return "RequestFailedException";
            if (error is System.Threading.Tasks.TaskCanceledException) return "TaskCanceledException";
            if (error is OperationCanceledException) return "OperationCanceledException";
            if (error is TimeoutException) return "TimeoutException";
            if (error is NullReferenceException) return "NullReferenceException";
            if (error is InvalidOperationException) return "InvalidOperationException";
            if (error is ArgumentException) return "ArgumentException";
            return "Other";
        }
    }
}
