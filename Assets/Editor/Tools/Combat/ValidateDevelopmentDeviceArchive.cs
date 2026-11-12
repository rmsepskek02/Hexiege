using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Hexiege.Application;
using Hexiege.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    // 원문은 출력하지 않는다. live snapshot의 완성된 prefix와 불완전 tail/정상 Flush footer를 구분한다.
    internal static class DeviceArchiveSnapshot
    {
        internal sealed class Result
        {
            public long Bytes, Records, Attempts, Committed, ConsoleFallbacks, DiagnosticFailures;
            public bool PartialTail, FlushedFooter;
            public string Identity, Sha256;
        }

        internal static Result Validate(byte[] bytes)
        {
            var result = new Result { Bytes = bytes.LongLength };
            using (var hash = SHA256.Create()) result.Sha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
            var utf8 = new UTF8Encoding(false, true);
            int position = 0;
            bool header = false;
            while (position < bytes.Length)
            {
                string line = ReadLine(bytes, ref position, utf8);
                if (line == null)
                {
                    if (result.FlushedFooter) throw new InvalidDataException("Incomplete data after archive footer.");
                    result.PartialTail = true; break;
                }
                if (line.StartsWith("=== DEVICE-ARCHIVE schema=1 ", StringComparison.Ordinal))
                {
                    if (header || result.Records != 0) throw new InvalidDataException("Duplicate archive identity.");
                    header = true; result.Identity = line; continue;
                }
                if (line.StartsWith("=== RECORD seq=", StringComparison.Ordinal))
                {
                    if (!header || result.FlushedFooter) throw new InvalidDataException("Record outside active archive.");
                    Match record = Regex.Match(line, @"^=== RECORD seq=(\d+) utf8Bytes=(\d+) ===$");
                    if (!record.Success) throw new InvalidDataException("Invalid record header.");
                    long sequence = long.Parse(record.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    long length = long.Parse(record.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (sequence != result.Records + 1 || length < 0 || length > 16 * 1024 * 1024)
                        throw new InvalidDataException("Invalid sequence or bounded record length.");
                    if (length > bytes.Length - position) { result.PartialTail = true; break; }
                    // 엄격한 UTF-8 검사. 원문은 메모리 검증에만 사용하고 receipt/Console에 복사하지 않는다.
                    string payload = utf8.GetString(bytes, position, (int)length);
                    if (!Regex.IsMatch(payload, @"^\[\d\d:\d\d:\d\d\.\d\d\d\] \[(INFO|WARN|ERROR)\] "))
                        throw new InvalidDataException("Record has no canonical GameLog first line.");
                    position += (int)length;
                    string end = ReadLine(bytes, ref position, utf8);
                    if (end == null) { result.PartialTail = true; break; }
                    if (end != "=== RECORD-END seq=" + sequence + " ===") throw new InvalidDataException("Record END differs.");
                    result.Records++; continue;
                }
                if (line.StartsWith("=== DEVICE-ARCHIVE-END ", StringComparison.Ordinal))
                {
                    Match footer = Regex.Match(line, @"^=== DEVICE-ARCHIVE-END attempts=(\d+) committed=(\d+) diagnosticFailures=(\d+) consoleFallbacks=(\d+) recordsFlushed=True ===$");
                    if (!header || result.FlushedFooter || !footer.Success) throw new InvalidDataException("Invalid archive footer.");
                    result.Attempts = long.Parse(footer.Groups[1].Value); result.Committed = long.Parse(footer.Groups[2].Value);
                    result.DiagnosticFailures = long.Parse(footer.Groups[3].Value); result.ConsoleFallbacks = long.Parse(footer.Groups[4].Value);
                    if (result.Attempts != result.Records || result.Committed != result.Records) throw new InvalidDataException("Footer count differs.");
                    result.FlushedFooter = true; continue;
                }
                if (line == "=== Android development runtime archive ===")
                {
                    if (header || result.Records != 0 || result.FlushedFooter) throw new InvalidDataException("Duplicate archive title.");
                    continue;
                }
                if (line.Length != 0)
                    throw new InvalidDataException("Unknown data outside record.");
            }
            if (!header) throw new InvalidDataException("Archive identity is missing.");
            // Footer는 파일 Flush만 증명한다. Dispose 성공은 별도 Logcat closed=True receipt와 대조한다.
            return result;
        }

        private static string ReadLine(byte[] bytes, ref int position, Encoding encoding)
        {
            int end = Array.IndexOf(bytes, (byte)'\n', position);
            if (end < 0) return null;
            string line = encoding.GetString(bytes, position, end - position).TrimEnd('\r');
            position = end + 1;
            return line;
        }
    }

    internal static class ValidateDevelopmentDeviceArchive
    {
        private static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        private static Type Logger => typeof(RuntimeLogger);
        private static object Read(string name) => Logger.GetField(name, Flags).GetValue(null);
        private static object Invoke(string name, params object[] args) => Logger.GetMethod(name, Flags).Invoke(null, args);
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        private static byte[] Snapshot(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool ArchiveFixtureCaller(string line) => (bool)Invoke("TryArchiveDeviceRecord", line, LogType.Log, false, null);

        [MenuItem("Hexiege/Combat/Diagnostics/Self Validate Development Device Archive")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEditorInternal.ProfilerDriver.enabled
                || NetworkContext.IsNetworkActive || GameServicesLocator.Current != null
                || (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening))
            { Debug.LogWarning("Archive fixture requires stopped Play/record/network/services."); return; }
            if (Read("_writer") != null) { Debug.LogWarning("Archive fixture refuses an existing runtime writer."); return; }
            var saved = new Dictionary<FieldInfo, object>();
            foreach (var field in Logger.GetFields(Flags))
                if (!field.IsInitOnly && (field.Name.StartsWith("_device", StringComparison.Ordinal)
                    || field.Name == "_writer" || field.Name == "_isEmittingToConsole")) saved.Add(field, field.GetValue(null));
            StackTraceLogType priorPolicy = UnityEngine.Application.GetStackTraceLogType(LogType.Log);
            string run = Guid.NewGuid().ToString("N");
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/ProfilerDiagnostics/device-archive-fixture-" + run));
            bool passed = false, cleanup = false;
            Debug.Log("[DEVICE-ARCHIVE-VERIFY] BEGIN Run=" + run + " Mode=EditorSharedArchiveCore");
            try
            {
                Invoke("BeginDeviceArchive", root);
                string path = (string)Read("_devicePath");
                Require(path != null && File.Exists(path), "Actual temporary file was not opened.");
                Invoke("BeginDeviceArchive", root);
                Require((string)Read("_devicePath") == path, "Repeated begin replaced the session.");
                UnityEngine.Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.ScriptOnly);
                Require(ArchiveFixtureCaller("[20:00:00.000] [INFO] [Fixture/Archive] 한글 caller"), "Caller record failed.");
                UnityEngine.Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                Require(ArchiveFixtureCaller("[20:00:00.001] [INFO] [Fixture/Archive] none-policy"), "None-policy record failed.");
                string content = new UTF8Encoding(false, true).GetString(Snapshot(path));
                Require(content.Contains(nameof(ArchiveFixtureCaller)), "Actual Unity script caller is absent.");
                int second = content.IndexOf("=== RECORD seq=2 ", StringComparison.Ordinal);
                Require(second >= 0 && !content.Substring(second).Contains(nameof(ArchiveFixtureCaller)), "None policy captured a new stack.");
                var live = DeviceArchiveSnapshot.Validate(Snapshot(path));
                Require(live.Records == 2 && !live.PartialTail && !live.FlushedFooter, "Live prefix validation failed.");
                RuntimeLogger.EndSession(); RuntimeLogger.EndSession();
                var closed = DeviceArchiveSnapshot.Validate(Snapshot(path));
                Require(closed.Records == 2 && closed.FlushedFooter && closed.ConsoleFallbacks == 0, "Closed record counts differ.");
                Require(!ArchiveFixtureCaller("[20:00:00.002] [INFO] [Fixture/Archive] postclose"), "Closed archive accepted a record.");
                File.WriteAllText(Path.Combine(root, "verification-receipt.txt"), "Run=" + run + "\nRecords=" + closed.Records
                    + "\nBytes=" + closed.Bytes + "\nSHA256=" + closed.Sha256 + "\nCaller=True\nNonePolicy=True\nFlushedFooter=True\n");
                passed = true;
            }
            catch (Exception error) { Debug.LogError("[DEVICE-ARCHIVE-VERIFY] FAILED Run=" + run + " " + error); }
            finally
            {
                cleanup = true;
                var cleanupErrors = new List<string>();
                try { RuntimeLogger.EndSession(); }
                catch (Exception error) { cleanup = false; cleanupErrors.Add(error.GetType().Name); }
                try { UnityEngine.Application.SetStackTraceLogType(LogType.Log, priorPolicy); }
                catch (Exception error) { cleanup = false; cleanupErrors.Add(error.GetType().Name); }
                foreach (var field in saved)
                    try { field.Key.SetValue(null, field.Value); }
                    catch (Exception error) { cleanup = false; cleanupErrors.Add(field.Key.Name + ":" + error.GetType().Name); }
                if (!cleanup) Debug.LogError("[DEVICE-ARCHIVE-VERIFY] CLEANUP-FAILED " + string.Join(",", cleanupErrors));
                Debug.Log("[DEVICE-ARCHIVE-VERIFY] END Run=" + run + " Passed=" + (passed && cleanup) + " Cleanup=" + cleanup
                    + " Scope=EditorSharedCoreCallerFile;AndroidFilesystemAndIL2CPPNotExercised");
            }
        }

        [MenuItem("Hexiege/Combat/Diagnostics/Validate Pulled Development Archive Snapshot")]
        private static void ValidatePulled()
        {
            string path = EditorUtility.OpenFilePanel("Validate private pulled RuntimeLog snapshot", "", "txt");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var result = DeviceArchiveSnapshot.Validate(Snapshot(path));
                string receipt = "bytes=" + result.Bytes + "\nsha256=" + result.Sha256 + "\ncompleteRecords=" + result.Records
                    + "\npartialTail=" + result.PartialTail + "\nrecordsFlushedFooter=" + result.FlushedFooter
                    + "\nconsoleFallbacks=" + (result.FlushedFooter ? result.ConsoleFallbacks.ToString() : "unknown-without-footer")
                    + "\ndiagnosticFailures=" + (result.FlushedFooter ? result.DiagnosticFailures.ToString() : "unknown-without-footer")
                    + "\ncleanDisposeRequiresSeparateLogcatReceipt=True\n";
                string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/ProfilerDiagnostics"));
                Directory.CreateDirectory(directory);
                string output = Path.Combine(directory, "device-archive-readback-" + Guid.NewGuid().ToString("N") + ".txt");
                File.WriteAllText(output, receipt, new UTF8Encoding(false));
                Debug.Log("Archive snapshot validated; records=" + result.Records + " partialTail=" + result.PartialTail + " privateReceipt=" + output);
            }
            catch (Exception error) { Debug.LogError("Archive snapshot invalid: " + error.GetType().Name); }
        }
    }
}
