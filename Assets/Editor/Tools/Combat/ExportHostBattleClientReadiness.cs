using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Application;
using Hexiege.Infrastructure;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

namespace Hexiege.EditorTools
{
    /// <summary>실제 Editor Client의 준비와 기존 로그 anchor만 private 파일로 확인한다.</summary>
    internal static class ExportHostBattleClientReadiness
    {
        private sealed class ReadinessFailure : Exception { internal readonly string Code; internal ReadinessFailure(string code) { Code = code; } }
        [Serializable] private sealed class Request
        { public string captureToken, readyNonce, mapHash; public ulong networkObjectId; public int mapVersion; }
        [Serializable] private sealed class Receipt
        {
            public string protocol = "host-battle-client-v2", utc, captureToken, readyNonce, mapHash, reason, runtimeLogPath, anchorSha256, assemblySha256;
            public bool ready, requiresRestart, logReadback, isClient, isServer, isHost, listening, networkGameStarted;
            public ulong networkObjectId, clientNetworkId;
            public int editorProcessId;
            public int mapVersion, frame;
            public long offsetBeforeAnchor, offsetAfterAnchor, sourceLengthObserved;
            public double serverTime, realtimeMs;
            public string semantics = "Editor log prefix/anchor only. Original file can grow later; collector must snapshot exact offset range and record new length/hash. Server clock is a shared projection, not exact cross-machine clock calibration.";
        }
        [MenuItem("Hexiege/Combat/Diagnostics/Export Host Battle Client Readiness")]
        private static void Export()
        {
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/ProfilerDiagnostics"));
            Directory.CreateDirectory(directory);
            var result = new Receipt { utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), reason = "Unknown" };
            try
            {
                if (!EditorApplication.isPlaying) throw new ReadinessFailure("EditorNotPlaying");
                if (EditorApplication.isPaused) throw new ReadinessFailure("EditorPaused");
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(Path.Combine(directory, "host-battle-client-request.json")));
                if (!Guid.TryParseExact(request.captureToken, "N", out _) || !Guid.TryParseExact(request.readyNonce, "N", out _))
                    throw new ReadinessFailure("InvalidRequestToken");
                result.captureToken = request.captureToken; result.readyNonce = request.readyNonce;
                var network = NetworkManager.Singleton;
                if (network == null) throw new ReadinessFailure("NoNetworkManager");
                result.isClient = network.IsClient; result.isServer = network.IsServer; result.isHost = network.IsHost; result.listening = network.IsListening;
                if (!result.isClient || result.isServer || result.isHost || !result.listening || !network.IsConnectedClient)
                    throw new ReadinessFailure("ExpectedConnectedPureClient");
                var services = GameServicesLocator.Current;
                result.networkGameStarted = services != null && services.IsNetworkGameStarted;
                var map = (services as IReadOnlyMapPreparationEvidence)?.GetLastMapPreparation();
                var ncc = UnityEngine.Object.FindFirstObjectByType<NetworkCombatController>();
                if (!result.networkGameStarted || map == null || ncc == null || !ncc.IsSpawned)
                    throw new ReadinessFailure("GameOrIdentityNotReady");
                result.mapHash = map.HashHex; result.mapVersion = map.MapVersion; result.networkObjectId = ncc.NetworkObjectId;
                if (!string.Equals(result.mapHash, request.mapHash, StringComparison.OrdinalIgnoreCase) || result.mapVersion != request.mapVersion || result.networkObjectId != request.networkObjectId)
                    throw new ReadinessFailure("HostIdentityMismatch");
                result.frame = Time.frameCount; result.realtimeMs = Time.realtimeSinceStartupAsDouble * 1000; result.serverTime = network.ServerTime.Time;
                result.clientNetworkId = network.LocalClientId;
                using (var process = System.Diagnostics.Process.GetCurrentProcess()) result.editorProcessId = process.Id;
                if (!RuntimeLogger.TryGetActiveEditorLogEvidence(out string path, out long before))
                    throw new ReadinessFailure("EditorLogInactive");
                result.runtimeLogPath = path; result.offsetBeforeAnchor = before;
                GameLog.Dev.Info("Performance", "HostBattleReadiness", "Client readiness anchor",
                    FormattableString.Invariant($"Capture={request.captureToken}, ReadyNonce={request.readyNonce}, MapHash={map.HashHex}, Ncc={ncc.NetworkObjectId}, Frame={result.frame}, ServerTime={result.serverTime:G17}"));
                if (!RuntimeLogger.TryGetActiveEditorLogEvidence(out string afterPath, out long after) || path != afterPath || after <= before || after - before > 65536)
                    throw new ReadinessFailure("EditorAnchorNotFlushed");
                byte[] bytes = new byte[after - before];
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    result.sourceLengthObserved = file.Length;
                    file.Position = before;
                    int read = 0;
                    while (read < bytes.Length) { int n = file.Read(bytes, read, bytes.Length - read); if (n == 0) throw new EndOfStreamException(); read += n; }
                }
                result.offsetAfterAnchor = after;
                string anchor = Encoding.UTF8.GetString(bytes);
                result.logReadback = anchor.Contains(request.captureToken) && anchor.Contains(request.readyNonce) && anchor.Contains("Client readiness anchor");
                if (!result.logReadback) throw new ReadinessFailure("AnchorReadbackMissing");
                using (var hash = SHA256.Create()) result.anchorSha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
                string assembly = typeof(GameServicesLocator).Assembly.Location;
                using (var hash = SHA256.Create()) using (var file = File.OpenRead(assembly)) result.assemblySha256 = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "");
                result.ready = true; result.reason = "Ready";
            }
            catch (Exception error)
            {
                result.reason = error is ReadinessFailure known ? known.Code : error.GetType().Name;
                result.requiresRestart = false;
            }
            string output = Path.Combine(directory, "host-battle-client-ready-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(output, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
            Debug.Log("Private Host battle Client readiness receipt: " + output + "; ready=" + result.ready);
        }
    }
}
