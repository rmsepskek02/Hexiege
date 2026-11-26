#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>기기 원문을 PC에 먼저 보존한다. 삭제는 사용자가 고른 검증된 종료 파일 하나씩만 수행한다.</summary>
    internal sealed class AndroidDevelopmentLogArchive : EditorWindow
    {
        internal const string Package = "com.gorocompany.hexiege";
        internal const string RemoteRoot = "/storage/emulated/0/Android/data/" + Package + "/files/DevelopmentLogs";
        internal const string PrivateRelative = ".utmp/raw-logs/_Logs/Android";
        internal const string PrivateLogcatRelative = ".utmp/raw-logs/_Logs/Logcat", LogcatRawName = "RuntimeLog_device.txt";
        internal const string RawName = "RuntimeLog.txt", ReceiptName = "CollectionReceipt.json";
        internal const long MaxRawBytes = 256L * 1024 * 1024, MaxReceiptBytes = 64L * 1024;
        private string _adb, _serial, _deviceLabel;
        private Vector2 _scroll;
        private string _lastDeletionResult;
        private string _collectionStatus, _taskName = "android-logs";
        private bool _lastLogcatEmpty;
        private readonly Dictionary<string, DateTime> _localUsage = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private GUIStyle _deleteButtonStyle;
        private readonly List<Entry> _entries = new List<Entry>();
        private sealed class Entry { internal string session, local, status; internal long bytes; internal bool selected; }

        [Serializable] internal sealed class Receipt
        {
            public int schema = 1;
            public string package, serial, session, collectedUtc, rawSha256, remoteBeforeSha256, remoteAfterSha256;
            public long rawBytes, remoteBeforeBytes, remoteAfterBytes;
            public bool remoteStable;
            public string validation;
        }
        internal sealed class Parsed
        {
            internal string session, build, error;
            internal int pid, records;
            internal bool complete;
            internal long tailBytes;
        }
        internal sealed class RemoteInfo { internal long bytes; internal string sha; }
        private static readonly Regex SessionPattern = new Regex(@"^\d{8}-\d{6}-\d{3}-[a-f0-9]{32}$", RegexOptions.CultureInvariant);

        // 통합 창의 중복 진입 메뉴는 비활성화하고 기능은 유지한다.
        // [MenuItem("Hexiege/Logs/Android Development Logs", false, 25)]
        private static void Open() => GetWindow<AndroidDevelopmentLogArchive>("Device Logs and ZIP");
        [MenuItem("Hexiege/Logs/Collect Device Logs and Create ZIP", false, 23)]
        private static void OpenUnified() => Open();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("선택한 앱 로그와 현재 Logcat 버퍼를 함께 PC로 수집·검증하고 ZIP을 만듭니다. 세션은 앱 실행 1회이며 여러 경기를 포함할 수 있습니다. ZIP에는 오늘의 아직 보관하지 않은 기존 로그도 포함됩니다. 이 버튼은 삭제·업로드를 실행하지 않습니다. 개발 Android 앱은 시작 시 30일 미사용 원본만 자동 정리합니다.", MessageType.Info);
            if (GUILayout.Button("기기 선택 · 세션 목록 읽기")) Safe(RefreshSessions);
            EditorGUILayout.LabelField("기기", _deviceLabel ?? "선택되지 않음");
            _taskName = EditorGUILayout.TextField("ZIP 작업명 (영문)", _taskName);
            if (!string.IsNullOrEmpty(_collectionStatus)) EditorGUILayout.HelpBox(_collectionStatus, MessageType.Info);
            if (!string.IsNullOrEmpty(_lastDeletionResult)) EditorGUILayout.HelpBox(_lastDeletionResult, MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var row in _entries)
            {
                row.selected = EditorGUILayout.ToggleLeft(row.session + " (" + row.bytes + " bytes)", row.selected);
                EditorGUILayout.LabelField(row.status ?? "미회수", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_serial)))
            {
                if (GUILayout.Button("선택 앱 로그 + Logcat 수집 · ZIP 만들기")) Safe(CollectAndZip);
                if (GUILayout.Button("선택 세션 PC 복사 · 검증")) Safe(CollectSelected);
                // 삭제 버튼의 글자만 바꾼다. GUI 전역 색이나 다른 버튼 스타일은 건드리지 않는다.
                if (_deleteButtonStyle == null)
                {
                    _deleteButtonStyle = new GUIStyle(GUI.skin.button);
                    _deleteButtonStyle.normal.textColor = Color.red;
                    _deleteButtonStyle.hover.textColor = Color.red;
                    _deleteButtonStyle.active.textColor = Color.red;
                    _deleteButtonStyle.focused.textColor = Color.red;
                    _deleteButtonStyle.onNormal.textColor = Color.red;
                    _deleteButtonStyle.onHover.textColor = Color.red;
                    _deleteButtonStyle.onActive.textColor = Color.red;
                    _deleteButtonStyle.onFocused.textColor = Color.red;
                }
                if (GUILayout.Button("PC 동일 보관본 기기 로그 삭제…", _deleteButtonStyle)) Safe(DeleteSelected);
            }
            EditorGUILayout.HelpBox("수집·ZIP은 즉시 삭제하지 않습니다. 수동 삭제는 앱 종료 후 PC 동일 바이트 보관본이 있는 항목만 한 번 확인하며, 부적격은 사유를 표시하고 건너뜁니다. 정상 종료 기록은 필수가 아닙니다. 개발 앱은 30일 미사용 로그를 별도 정리합니다. 사용 중 잠금이 남으면 보호됩니다. 업로드는 직접 수행합니다.", MessageType.None);
        }

        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception error) { EditorUtility.DisplayDialog("Android Development Logs", error.Message, "확인"); }
            finally { EditorUtility.ClearProgressBar(); }
        }
        private void RefreshSessions()
        {
            if (!LogcatCapture.TryResolveAdb(out _adb) || !LogcatCapture.TryResolveDevice(_adb, out _serial, out _deviceLabel)) return;
            if (!Regex.IsMatch(_serial, @"^[A-Za-z0-9._:\-]+$")) throw new IOException("지원하지 않는 기기 식별자입니다.");
            EnsureDevice();
            // One bounded-output listing command; listing never hashes or reads raw logs.
            string usageName = Hexiege.Infrastructure.DevelopmentLogRetention.UsageName;
            string output = Shell("if [ -d '" + RemoteRoot + "' ] && [ ! -L '" + RemoteRoot + "' ]; then for d in '" + RemoteRoot + "'/*; do if [ -d \"$d\" ] && [ ! -L \"$d\" ] && [ -f \"$d/RuntimeLog.txt\" ] && [ ! -L \"$d/RuntimeLog.txt\" ]; then printf '%s\\t%s\\t' \"${d##*/}\" \"$(stat -c %s \"$d/RuntimeLog.txt\")\"; m=\"$d/" + usageName + "\"; if [ -e \"$m\" ]; then if [ -f \"$m\" ] && [ ! -L \"$m\" ] && [ \"$(stat -c %s \"$m\")\" -le 128 ]; then cat \"$m\"; else printf INVALID; fi; fi; printf '\\n'; fi; done; fi");
            var local = BuildLocalIndex();
            _entries.Clear();
            var sync = new StringBuilder();
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Split('\t');
                if (fields.Length != 3 || !ValidSession(fields[0]) || !long.TryParse(fields[1], out long bytes) || bytes < 0) continue;
                local.TryGetValue(fields[0], out string raw);
                var row = new Entry { session = fields[0], bytes = bytes, local = raw,
                    status = raw == null ? "미회수" : "PC 보관 후보 있음(목록에서는 미검증). 삭제 직전에 동일 원문을 검증합니다." };
                _entries.Add(row);
                if (_localUsage.TryGetValue(row.session, out DateTime used))
                {
                    if (TryUsageUtc(fields[2], out DateTime? remote) && (!remote.HasValue || used > remote.Value))
                        sync.Append(BuildUsageScript(row.session, used, fields[2])).Append(';');
                    else if (!TryUsageUtc(fields[2], out _)) row.status += " 기기 사용 시각 불명: 자동 정리 보호.";
                }
            }
            // One metadata synchronization command, never N raw reads or N ADB launches.
            if (sync.Length != 0) Shell(sync.ToString());
        }
        private Dictionary<string, string> BuildLocalIndex()
        {
            var index = new Dictionary<string, string>(StringComparer.Ordinal);
            _localUsage.Clear();
            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath), root = Path.Combine(project, PrivateRelative);
            if (!Directory.Exists(root)) return index;
            CheckLocalPath(project, root);
            foreach (string date in Directory.EnumerateDirectories(root).OrderByDescending(x => x, StringComparer.Ordinal))
            {
                if (!ValidDate(Path.GetFileName(date))) continue;
                CheckLocalPath(project, date);
                foreach (string sessionDir in Directory.EnumerateDirectories(date))
                {
                    string session = Path.GetFileName(sessionDir);
                    if (!ValidSession(session)) continue;
                    CheckLocalPath(project, sessionDir);
                    foreach (string collection in Directory.EnumerateDirectories(sessionDir).OrderByDescending(x => x, StringComparer.Ordinal))
                    {
                        if (!ValidSession(Path.GetFileName(collection))) continue;
                        string raw = Path.Combine(collection, RawName), receiptPath = Path.Combine(collection, ReceiptName);
                        try
                        {
                            CheckLocalPath(project, collection); CheckLocalPath(project, raw); CheckLocalPath(project, receiptPath);
                            if (!File.Exists(raw) || !File.Exists(receiptPath)) continue;
                            Receipt receipt = JsonUtility.FromJson<Receipt>(Encoding.UTF8.GetString(BoundedRead(receiptPath, MaxReceiptBytes)));
                            if (receipt != null && receipt.package == Package && receipt.session == session && receipt.serial == _serial)
                            {
                                if (!index.ContainsKey(session)) index[session] = raw;
                                string usage = Path.Combine(collection, Hexiege.Infrastructure.DevelopmentLogRetention.UsageName);
                                CheckLocalPath(project, usage);
                                if (File.Exists(usage) && new FileInfo(usage).Length <= 128 && TryUsageUtc(File.ReadAllText(usage).Trim(), out DateTime? used)
                                    && used.HasValue && (!_localUsage.TryGetValue(session, out DateTime old) || used.Value > old)) _localUsage[session] = used.Value;
                            }
                        }
                        catch (IOException) { }
                    }
                }
            }
            return index;
        }
        internal static void MarkPrivateLogUsed(string raw)
        {
            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            CheckPrivateRawPath(project, raw);
            string marker = Path.Combine(Path.GetDirectoryName(raw), Hexiege.Infrastructure.DevelopmentLogRetention.UsageName);
            CheckLocalPath(project, marker);
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), new UTF8Encoding(false));
        }
        internal static bool TryUsageUtc(string text, out DateTime? used)
        {
            used = null;
            if (text == "") return true;
            if (text.Length > 128 || !DateTime.TryParseExact(text, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime value) || value.Kind != DateTimeKind.Utc || value > DateTime.UtcNow) return false;
            used = value; return true;
        }
        internal static string BuildUsageScript(string session, DateTime requested, string expected)
        {
            if (!TryUsageUtc(expected, out DateTime? old) || requested.Kind != DateTimeKind.Utc || requested > DateTime.UtcNow)
                throw new IOException("사용 시각이 불명확합니다. 기존 원문을 보호합니다.");
            if (old.HasValue && old.Value >= requested) return "true";
            string directory = RemoteRoot + "/" + session;
            string marker = directory + "/" + Hexiege.Infrastructure.DevelopmentLogRetention.UsageName;
            string claim = directory + "/" + Hexiege.Infrastructure.DevelopmentLogRetention.LockName;
            return "(set -e; " + RemoteGuard(session) + " || exit 70; test ! -L '" + claim + "'; (set -C; : > '" + claim + "') || exit 73; "
                + "trap \"rm -- '" + claim + "'\" EXIT; test ! -L '" + marker + "'; "
                + "if [ -e '" + marker + "' ]; then test -f '" + marker + "'; test \"$(stat -c %s '" + marker + "')\" -le 128; test \"$(cat '" + marker + "')\" = '" + expected + "'; else test '" + expected + "' = ''; fi; "
                + "printf '%s' '" + requested.ToString("O", CultureInfo.InvariantCulture) + "' > '" + marker + "')";
        }
        private void TouchRemoteUsage(string session, DateTime used)
        {
            string marker = RemoteRoot + "/" + session + "/" + Hexiege.Infrastructure.DevelopmentLogRetention.UsageName;
            string old = Shell(RemoteGuard(session) + " && test ! -L '" + marker + "' && if [ -e '" + marker + "' ]; then test -f '" + marker + "' && test \"$(stat -c %s '" + marker + "')\" -le 128 && cat '" + marker + "'; fi");
            Shell(BuildUsageScript(session, used, old));
        }
        private void EnsureDevice()
        {
            var r = LogcatCapture.RunAdb(_adb, _serial, "get-state", 10000, null);
            if (!r.Success || r.StandardOutput.Trim() != "device") throw new IOException("기기 연결/USB 승인을 확인하세요. 동작을 중단했습니다.");
        }
        private string Shell(string script)
        {
            if (script.Contains('"')) script = script.Replace("\"", "\\\"");
            var r = LogcatCapture.RunAdb(_adb, _serial, "shell \"" + script + "\"", 15000, null);
            if (!r.Success) throw new IOException("기기 읽기/가드 명령 실패. 정리 작업을 중단했습니다. Exit=" + r.ExitCode);
            return r.StandardOutput.TrimEnd('\r', '\n');
        }
        internal static bool ValidSession(string session) => session != null && SessionPattern.IsMatch(session)
            && DateTime.TryParseExact(session.Substring(0, 19), "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        internal static string RemoteFile(string session)
        {
            if (!ValidSession(session)) throw new IOException("잘못된 세션 경로입니다.");
            return RemoteRoot + "/" + session + "/" + RawName;
        }
        private static string RemoteGuard(string session)
        {
            string file = RemoteFile(session), dir = RemoteRoot + "/" + session;
            return "test ! -L '" + RemoteRoot + "' && test ! -L '" + dir + "' && test ! -L '" + file + "' && test -f '" + file + "'"
                + " && test \"$(readlink -f '" + file + "')\" = \"$(readlink -f '" + RemoteRoot + "')/" + session + "/" + RawName + "\"";
        }
        private RemoteInfo Stat(string session)
        {
            string file = RemoteFile(session);
            string output = Shell(RemoteGuard(session) + " && stat -c %s '" + file + "' && sha256sum '" + file + "'");
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length != 2 || !long.TryParse(lines[0], out long size) || size < 0
                || !Regex.IsMatch(lines[1], @"^[a-fA-F0-9]{64}\s+")) throw new IOException("원격 크기/hash를 확인할 수 없습니다.");
            return new RemoteInfo { bytes = size, sha = lines[1].Substring(0, 64).ToLowerInvariant() };
        }
        private void CollectSelected()
        {
            EnsureDevice();
            foreach (var row in _entries.Where(x => x.selected))
            {
                TouchRemoteUsage(row.session, DateTime.UtcNow);
                row.status = "사용 시각을 기기에 갱신했습니다. 복사 실패 시에도 원문은 유지되며 사용 시각은 갱신된 상태입니다.";
                var before = Stat(row.session);
                if (before.bytes > MaxRawBytes) throw new IOException("256 MiB 도구 읽기 한도를 넘습니다. 기기 원본은 유지됩니다.");
                string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                string date = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string collection = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
                string directory = Path.Combine(project, PrivateRelative, date, row.session, collection);
                CheckLocalPath(project, directory);
                Directory.CreateDirectory(directory);
                string temp = Path.Combine(directory, RawName + ".partial"), target = Path.Combine(directory, RawName);
                var pull = LogcatCapture.RunAdb(_adb, _serial, "pull \"" + RemoteFile(row.session) + "\" \"" + temp + "\"", 120000, "Android 로그를 PC에 복사하는 중…");
                if (!pull.Success || !File.Exists(temp)) throw new IOException("로그 복사가 완료되지 않았습니다. 기존 PC 원본은 유지됩니다.");
                var after = Stat(row.session);
                byte[] bytes = BoundedRead(temp, MaxRawBytes);
                string sha = Sha(bytes);
                var parsed = Parse(bytes);
                bool stable = before.bytes == after.bytes && before.sha == after.sha && bytes.LongLength == after.bytes && sha == after.sha;
                if (parsed.session != null && parsed.session != row.session) throw new IOException("원문 header와 선택 세션이 다릅니다. partial 원본을 보존했습니다.");
                var receipt = new Receipt { package = Package, serial = _serial, session = row.session, collectedUtc = DateTime.UtcNow.ToString("O"),
                    rawSha256 = sha, rawBytes = bytes.LongLength, remoteBeforeSha256 = before.sha, remoteAfterSha256 = after.sha,
                    remoteBeforeBytes = before.bytes, remoteAfterBytes = after.bytes, remoteStable = stable,
                    validation = parsed.complete ? "CompleteRecordsAndFooter" : "SnapshotIncomplete: " + parsed.error };
                File.Move(temp, target);
                string receiptTemp = Path.Combine(directory, ReceiptName + ".partial");
                File.WriteAllText(receiptTemp, JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
                File.Move(receiptTemp, Path.Combine(directory, ReceiptName));
                row.local = target;
                MarkPrivateLogUsed(target);
                row.status = stable && parsed.complete ? "PC 원문/hash/정상 종료 기록 검증 완료. 수동 삭제는 앱 종료 후 가능합니다."
                    : "진단 snapshot 보존(분석 완결성 별도). 원문이 PC와 동일하고 앱이 종료되면 수동 삭제 가능합니다. " + parsed.error;
            }
        }

        private void CollectAndZip()
        {
            if (!_entries.Any(x => x.selected)) throw new IOException("앱 로그 세션을 먼저 선택하세요. 수집/ZIP을 실행하지 않았습니다.");
            if (!Regex.IsMatch(_taskName ?? "", @"^[a-z0-9][a-z0-9\-]{0,39}$"))
                throw new IOException("ZIP 작업명은 영문 소문자·숫자·하이픈 1~40자로 입력하세요.");
            string phase = "앱 로그 복사·검증";
            DateTime archiveStartDate = DateTime.UtcNow.AddHours(9).Date;
            try
            {
                _collectionStatus = phase + " 중";
                CollectSelected();
                phase = "같은 기기의 Logcat 수집";
                _collectionStatus = "앱 로그 PC 보관 완료. " + phase + " 중";
                EnsureDevice();
                SavePrivateLogcat();
                phase = "ZIP 생성";
                _collectionStatus = "앱 로그와 Logcat PC 보관 완료. ZIP 생성 중";
                DateTime endDate = DateTime.UtcNow.AddHours(9).Date;
                if (endDate < archiveStartDate) archiveStartDate = endDate;
                bool created = LogArchiveZip.TryCreateArchive(_taskName, archiveStartDate);
                _collectionStatus = created ? "앱 로그·Logcat 수집 및 ZIP 검증 완료. 기기 원본 유지/자동 업로드 없음."
                    : "PC 수집본은 보존됐습니다. ZIP은 취소·실패·신규 대상 없음으로 생성되지 않았습니다.";
                if (_lastLogcatEmpty) _collectionStatus += " 현재 필터에 일치하는 Logcat 0건: 조회는 성공했습니다.";
            }
            catch
            {
                _collectionStatus = phase + " 단계에서 중단했습니다. 이미 복사한 PC 원문과 기기 원본은 보존합니다. 이후 단계는 실행하지 않았습니다.";
                throw;
            }
        }
        private string SavePrivateLogcat()
        {
            _lastLogcatEmpty = false;
            var result = LogcatCapture.ReadDeviceLogs(_adb, _serial);
            if (!result.Success) throw new IOException("Logcat 수집 명령이 실패했습니다. ZIP 자동 진행을 중단합니다.");
            _lastLogcatEmpty = string.IsNullOrWhiteSpace(result.StandardOutput);
            string text = "=== Android Logcat 캡처 ===\n=== 저장 UTC: " + DateTime.UtcNow.ToString("O")
                + " ===\n=== 대상 기기: " + _deviceLabel + " ===\n\n"
                + (_lastLogcatEmpty ? "matching Logcat records=0; no device buffer entries matched the configured filters. Read succeeded.\n" : result.StandardOutput);
            if (Encoding.UTF8.GetByteCount(text) > MaxRawBytes) throw new IOException("Logcat 도구 읽기 한도 256 MiB를 넘습니다.");
            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string directory = Path.Combine(project, PrivateLogcatRelative, DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd"),
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
            CheckLocalPath(project, directory); Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, LogcatRawName), temp = target + ".partial";
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.Write(bytes, 0, bytes.Length);
            File.Move(temp, target);
            if (Sha(BoundedRead(target, MaxRawBytes)) != Sha(bytes)) throw new IOException("PC Logcat 저장 hash가 다릅니다. 원문을 보존하고 중단합니다.");
            return target;
        }

        private string FindLocal(string session)
        {
            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath), root = Path.Combine(project, PrivateRelative);
            if (!Directory.Exists(root)) return null;
            CheckLocalPath(project, root);
            foreach (var date in Directory.GetDirectories(root).OrderByDescending(x => x, StringComparer.Ordinal))
            {
                if (!ValidDate(Path.GetFileName(date))) continue;
                CheckLocalPath(project, date);
                string dir = Path.Combine(date, session);
                if (!Directory.Exists(dir)) continue;
                CheckLocalPath(project, dir);
                foreach (var collection in Directory.GetDirectories(dir).OrderByDescending(x => x, StringComparer.Ordinal))
                {
                    if (!ValidSession(Path.GetFileName(collection))) continue;
                    string raw = Path.Combine(collection, RawName), receipt = Path.Combine(collection, ReceiptName);
                    CheckLocalPath(project, collection);
                    if (!File.Exists(raw) || !File.Exists(receipt)) continue;
                    CheckLocalPath(project, raw); CheckLocalPath(project, receipt);
                    try
                    {
                        var metadata = ValidatePair(BoundedRead(raw, MaxRawBytes), BoundedRead(receipt, MaxReceiptBytes), session);
                        if (metadata.serial == _serial) return raw;
                    }
                    catch (IOException) { /* 손상 보관본은 삭제 근거로 사용하지 않는다. 원본은 그대로 둔다. */ }
                }
            }
            return null;
        }
        [Serializable] private sealed class DeletionAudit { public string serial, utc; public int selected; public List<string> deleted, skipped; }
        private sealed class DeletePlan { internal Entry row; internal string localSha, receiptSha; internal long bytes; internal int pid; }
        private DeletePlan CheckDelete(Entry row)
        {
            if (row.local == null) throw new IOException("PC 보관본이 없습니다. 먼저 복사·검증하세요.");
            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            CheckPrivateRawPath(project, row.local);
            string receiptFile = Path.Combine(Path.GetDirectoryName(row.local), ReceiptName);
            CheckLocalPath(project, receiptFile);
            byte[] raw = BoundedRead(row.local, MaxRawBytes), receiptBytes = BoundedRead(receiptFile, MaxReceiptBytes);
            Receipt receipt = ValidatePair(raw, receiptBytes, row.session);
            Parsed parsed = Parse(raw); // PID는 receipt가 아닌 원문에서 얻는다.
            if (receipt.serial != _serial)
                throw new IOException("같은 기기의 PC 동일 바이트 보관본이 없어 삭제하지 않습니다.");
            var remote = Stat(row.session);
            if (remote.bytes != raw.LongLength || remote.sha != Sha(raw)) throw new IOException("회수 뒤 원격 파일이 변경되었습니다. 삭제하지 않습니다.");
            string pids = Shell("ps -A -o PID,NAME && printf '\\nPROCESS-LIST-END\\n'");
            ValidateDeleteState(receipt, parsed, remote, Sha(raw), raw.LongLength, pids);
            return new DeletePlan { row = row, localSha = remote.sha, receiptSha = Sha(receiptBytes), bytes = remote.bytes, pid = parsed.pid };
        }
        internal static void ValidateDeleteState(Receipt receipt, Parsed parsed, RemoteInfo remote, string localSha, long localBytes, string pids)
        {
            if (remote.sha != localSha || remote.bytes != localBytes)
                throw new IOException("PC 동일 바이트 보관본/동일 원격 파일이 아닙니다.");
            if (!AppStopped(pids ?? "")) throw new IOException("앱이 실행 중이거나 프로세스 조회가 불명확합니다. 앱을 종료한 뒤 다시 시도하세요.");
        }
        private void DeleteSelected()
        {
            EnsureDevice();
            if (!AppStopped(Shell("ps -A -o PID,NAME && printf '\\nPROCESS-LIST-END\\n'")))
                throw new IOException("앱이 실행 중이거나 조회가 불명확합니다. 앱을 종료한 뒤 삭제하세요. 원문 검증/삭제는 시작하지 않았습니다.");
            var chosen = _entries.Where(x => x.selected).ToList();
            var plans = new List<DeletePlan>();
            foreach (var row in chosen)
            {
                try { plans.Add(CheckDelete(row)); }
                catch (IOException e) { row.status = "보호/건너뜀: " + e.Message; }
            }
            if (plans.Count == 0) { _lastDeletionResult = "적격 0개. 선택 항목의 보호 사유를 확인하세요."; return; }
            string list = string.Join("\n", plans.Select(x => x.row.session + "  " + x.bytes + " bytes"));
            if (!EditorUtility.DisplayDialog("기기 로그 삭제 확인", list + "\n\n적격 " + plans.Count + "개 / " + plans.Sum(x => x.bytes)
                + " bytes. 기기 원문만 삭제하며 PC 원문·ZIP은 유지합니다. 부적격 항목은 건너뜁니다.", "적격 파일 삭제", "취소")) return;
            var deleted = new List<string>();
            try
            {
            foreach (var plan in plans)
            {
                try
                {
                EnsureDevice();
                var current = CheckDelete(plan.row);
                if (current.localSha != plan.localSha || current.receiptSha != plan.receiptSha || current.pid != plan.pid || current.bytes != plan.bytes)
                    throw new IOException("확인 중 보관본이 변경되었습니다. 이 항목을 건너뜁니다.");
                string script = BuildDeleteScript(plan.row.session, plan.localSha, plan.bytes, plan.pid);
                if (Shell(script).Trim() != "DELETED") throw new IOException("삭제 결과를 확인하지 못했습니다. 이 항목은 결과 미확인으로 표시합니다.");
                plan.row.status = "기기 로그 파일 삭제 완료. PC 원문·CollectionReceipt 보존됨.";
                plan.row.selected = false;
                deleted.Add(plan.row.session + " sha256=" + plan.localSha + " bytes=" + plan.bytes);
                }
                catch (IOException e) { plan.row.status = "변경/보호로 건너뜀: " + e.Message; }

            }
            }
            finally
            {
                _lastDeletionResult = "기기 삭제 확인 " + deleted.Count + "/" + plans.Count + "개. PC 원문은 유지됩니다. 중간 실패 시 확인된 항목도 이 결과에 포함됩니다. 연결 오류로 미확인인 항목은 삭제됐을 수 있습니다.";
                string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                string directory = Path.Combine(project, ".utmp/raw-logs/DeletionReceipts");
                CheckLocalPath(project, directory); Directory.CreateDirectory(directory);
                string audit = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".json");
                using (var file = new FileStream(audit, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                    writer.Write(JsonUtility.ToJson(new DeletionAudit { serial = _serial, utc = DateTime.UtcNow.ToString("O"), selected = chosen.Count, deleted = deleted, skipped = chosen.Where(x => x.selected).Select(x => x.session + " " + x.status).ToList() }, true));
            }
        }
        internal static bool PidAbsent(string output, int pid)
        {
            if (pid <= 0) return false;
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2 || lines[0].Trim() != "PID") return false;
            bool init = false; var seen = new HashSet<int>();
            foreach (string line in lines.Skip(1))
            {
                if (!Regex.IsMatch(line.Trim(), @"^[1-9]\d*$") || !int.TryParse(line.Trim(), out int value) || value == pid || !seen.Add(value)) return false;
                if (value == 1) init = true;
            }
            return init;
        }
        internal static bool AppStopped(string output)
        {
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3 || !Regex.IsMatch(lines[0].Trim(), @"^PID\s+NAME$") || lines[lines.Length - 1] != "PROCESS-LIST-END") return false;
            bool init = false; var seen = new HashSet<int>();
            foreach (string value in lines.Skip(1).Take(lines.Length - 2))
            {
                var match = Regex.Match(value.Trim(), @"^([1-9]\d*)\s+(.+)$");
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int pid) || !seen.Add(pid)) return false;
                string name = match.Groups[2].Value.Trim();
                if (pid == 1 && (name == "init" || name == "/system/bin/init")) init = true;
                if (name.Length == 0 || name == Package || name.StartsWith(Package + ":", StringComparison.Ordinal)) return false;
            }
            return init;
        }
        internal static string BuildDeleteScript(string session, string sha, long bytes, int pid)
        {
            if (!Regex.IsMatch(sha ?? "", @"^[a-f0-9]{64}$") || bytes < 0) throw new IOException("잘못된 삭제 검증값입니다.");
            string file = RemoteFile(session);
            string directory = RemoteRoot + "/" + session;
            string claim = directory + "/" + Hexiege.Infrastructure.DevelopmentLogRetention.LockName;
            string marker = directory + "/" + Hexiege.Infrastructure.DevelopmentLogRetention.UsageName;
            return "set -e; " + RemoteGuard(session) + " || exit 70; test ! -L '" + claim + "'; (set -C; : > '" + claim + "') || exit 73; trap \"rm -- '" + claim + "'\" EXIT; " + RemoteGuard(session) + " || exit 70; names=$(ps -A -o PID,NAME && printf '\\nPROCESS-LIST-END\\n'); "
                + "printf '%s\\n' \"$names\" | awk -v pkg='" + Package + "' 'NR==1 {if($1!=\"PID\" || $2!=\"NAME\" || NF!=2) exit 72; next} "
                + "{if(NF==0) next; if($0==\"PROCESS-LIST-END\") {done=1; next} if(done || $1 !~ /^[1-9][0-9]*$/ || $1>2147483647 || seen[$1]++) exit 72; pid=$1; sub(/^[[:space:]]*[0-9]+[[:space:]]+/,\"\"); gsub(/[[:space:]]+$/,\"\"); if($0==\"\" || $0==pkg || index($0,pkg\":\")==1) exit 72; if(pid==1 && ($0==\"init\" || $0==\"/system/bin/init\")) init=1; n++} END {if(n<1 || !init || !done) exit 72}'; "
                + "test \"$(stat -c %s '" + file + "')\" = '" + bytes.ToString(CultureInfo.InvariantCulture) + "'; "
                + "test \"$(sha256sum '" + file + "' | cut -c 1-64)\" = '" + sha + "'; "
                + RemoteGuard(session) + " || exit 71; rm -- '" + file + "'; test ! -e '" + file + "'; "
                + "if [ -f '" + marker + "' ] && [ ! -L '" + marker + "' ]; then rm -- '" + marker + "'; fi; "
                + "rm -- '" + claim + "'; trap - EXIT; rmdir -- '" + directory + "' 2>/dev/null || true; printf DELETED";
        }

        internal static bool ValidDate(string name) => DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        internal static IEnumerable<string> FindPrivateRawFiles(string project, DateTime startDate)
        {
            string root = Path.Combine(project, PrivateRelative);
            if (!Directory.Exists(root)) yield break;
            CheckLocalPath(project, root);
            foreach (string date in Directory.GetDirectories(root))
            {
                if (!DateTime.TryParseExact(Path.GetFileName(date), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    || day.Date < startDate.Date) continue;
                CheckLocalPath(project, date);
                foreach (string session in Directory.GetDirectories(date))
                {
                    if (!ValidSession(Path.GetFileName(session))) continue;
                    CheckLocalPath(project, session);
                    foreach (string collection in Directory.GetDirectories(session))
                    {
                        if (!ValidSession(Path.GetFileName(collection))) continue;
                        CheckLocalPath(project, collection);
                        string raw = Path.Combine(collection, RawName), receipt = Path.Combine(collection, ReceiptName);
                        if (!File.Exists(raw) || !File.Exists(receipt)) continue;
                        CheckPrivateRawPath(project, raw); CheckLocalPath(project, receipt);
                        yield return raw;
                    }
                }
            }
        }
        internal static IEnumerable<string> FindPrivateLogcatFiles(string project, DateTime startDate)
        {
            string root = Path.Combine(project, PrivateLogcatRelative);
            if (!Directory.Exists(root)) yield break;
            CheckLocalPath(project, root);
            foreach (string date in Directory.GetDirectories(root))
            {
                if (!DateTime.TryParseExact(Path.GetFileName(date), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    || day.Date < startDate.Date) continue;
                CheckLocalPath(project, date);
                foreach (string collection in Directory.GetDirectories(date))
                {
                    if (!ValidSession(Path.GetFileName(collection))) continue;
                    CheckLocalPath(project, collection);
                    string raw = Path.Combine(collection, LogcatRawName);
                    if (!File.Exists(raw)) continue;
                    CheckLocalPath(project, raw);
                    yield return raw;
                }
            }
        }
        internal static void CheckLocalPath(string project, string path)
        {
            string boundary = Path.GetFullPath(project).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new IOException("프로젝트 밖 경로입니다.");
            for (string p = full; p != null && p.Length >= boundary.Length - 1; p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("링크/정션 경로는 사용하지 않습니다.");
        }
        internal static string CheckPrivateRawPath(string project, string raw)
        {
            CheckLocalPath(project, raw);
            string root = Path.GetFullPath(Path.Combine(project, PrivateRelative)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(raw);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("private Android 보관 경로 밖입니다.");
            string[] parts = full.Substring(root.Length).Split(Path.DirectorySeparatorChar);
            if (parts.Length != 4 || !ValidDate(parts[0]) || !ValidSession(parts[1]) || !ValidSession(parts[2]) || parts[3] != RawName)
                throw new IOException("보관 경로 형식이 다릅니다.");
            return parts[1];
        }
        internal static Receipt ValidatePair(byte[] raw, byte[] receiptBytes, string expectedSession)
        {
            if (raw == null || raw.LongLength > MaxRawBytes || receiptBytes == null || receiptBytes.LongLength > MaxReceiptBytes)
                throw new IOException("원문/영수증 도구 읽기 한도를 넘습니다.");
            Receipt r;
            try { r = JsonUtility.FromJson<Receipt>(new UTF8Encoding(false, true).GetString(receiptBytes)); }
            catch (Exception) { throw new IOException("회수 영수증을 읽을 수 없습니다."); }
            ValidateReceipt(raw, r, expectedSession);
            return r;
        }
        internal static void ValidateReceipt(byte[] raw, Receipt r, string expectedSession)
        {
            Parsed parsed = Parse(raw);
            if (r == null || r.schema != 1 || r.package != Package || r.session != expectedSession || !ValidSession(r.session)
                || r.rawBytes != raw.LongLength || r.rawSha256 != Sha(raw) || string.IsNullOrEmpty(r.serial)
                || (parsed.session != null && parsed.session != r.session)) throw new IOException("원문/영수증 출처·hash가 일치하지 않습니다.");
            if (r.remoteStable && (r.remoteBeforeBytes != r.rawBytes || r.remoteAfterBytes != r.rawBytes
                || r.remoteBeforeSha256 != r.rawSha256 || r.remoteAfterSha256 != r.rawSha256)) throw new IOException("안정 snapshot 영수증이 모순됩니다.");
        }
        internal static byte[] BoundedRead(string path, long maximum)
        {
            if (new FileInfo(path).Length > maximum) throw new IOException("도구 읽기 한도를 넘는 파일입니다. 원본을 유지합니다.");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = file.Length;
                if (length > maximum || length > int.MaxValue) throw new IOException("도구 읽기 한도를 넘습니다.");
                var bytes = new byte[(int)length]; int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = file.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) throw new IOException("읽는 중 파일이 짧아졌습니다.");
                    offset += read;
                }
                if (file.ReadByte() != -1 || file.Length != length) throw new IOException("읽는 중 파일이 변경되었습니다.");
                return bytes;
            }
        }
        internal static string Sha(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        internal static Parsed Parse(byte[] bytes)
        {
            var p = new Parsed(); int cursor = 0;
            try
            {
                if (bytes == null || bytes.LongLength > MaxRawBytes) throw new IOException("InputLimit");
                new UTF8Encoding(false, true).GetString(bytes); // 손상 UTF8은 삭제 근거가 될 수 없다.
                if (Line(bytes, ref cursor) != "=== Android development runtime archive ===") throw new IOException("ArchiveHeader");
                var header = Regex.Match(Line(bytes, ref cursor), @"^=== DEVICE-ARCHIVE schema=1 session=(\d{8}-\d{6}-\d{3}-[a-f0-9]{32}) buildGuid=([a-fA-F0-9]{32}) backend=(IL2CPP|MonoOrEditor) process=([1-9]\d*) utc=([^ ]+) ===$");
                if (!header.Success || !ValidSession(header.Groups[1].Value) || !int.TryParse(header.Groups[4].Value, out p.pid)) throw new IOException("SessionHeader");
                p.session = header.Groups[1].Value; p.build = header.Groups[2].Value;
                if (Line(bytes, ref cursor) != "") throw new IOException("HeaderSeparator");
                while (cursor < bytes.Length)
                {
                    string line = Line(bytes, ref cursor);
                    var footer = Regex.Match(line, @"^=== DEVICE-ARCHIVE-END attempts=(\d+) committed=(\d+) diagnosticFailures=(\d+) consoleFallbacks=(\d+) recordsFlushed=True ===$");
                    if (footer.Success)
                    {
                        if (!long.TryParse(footer.Groups[1].Value, out long attempts) || !long.TryParse(footer.Groups[2].Value, out long committed)
                            || committed != p.records || attempts < committed || cursor != bytes.Length) throw new IOException("FooterCountsOrTail");
                        p.complete = true; return p;
                    }
                    var begin = Regex.Match(line, @"^=== RECORD seq=([1-9]\d*) utf8Bytes=([1-9]\d*) ===$");
                    if (!begin.Success || !int.TryParse(begin.Groups[1].Value, out int seq) || seq != p.records + 1
                        || !int.TryParse(begin.Groups[2].Value, out int length) || length > bytes.Length - cursor) throw new IOException("RecordHeaderOrPartialPayload");
                    if (bytes[cursor + length - 1] != 10) throw new IOException("PayloadNewline");
                    cursor += length; // payload 안의 RECORD/END 문자열은 절대 검색하지 않는다.
                    if (Line(bytes, ref cursor) != "=== RECORD-END seq=" + seq + " ===") throw new IOException("RecordEnd");
                    p.records++;
                }
                throw new IOException("NoFooterActiveOrKilled");
            }
            catch (Exception error) { p.error = error is IOException ? error.Message : error.GetType().Name; p.tailBytes = Math.Max(0, (bytes?.LongLength ?? 0) - cursor); return p; }
        }
        private static string Line(byte[] bytes, ref int cursor)
        {
            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != 10) cursor++;
            if (cursor == bytes.Length) throw new IOException("PartialLine");
            int end = cursor++; if (end > start && bytes[end - 1] == 13) end--;
            return Encoding.UTF8.GetString(bytes, start, end - start);
        }
    }
}
#endif
