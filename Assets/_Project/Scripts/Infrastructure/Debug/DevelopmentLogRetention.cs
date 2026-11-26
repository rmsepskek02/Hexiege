#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace Hexiege.Infrastructure
{
    // Device-only policy. The Editor shares the marker name/UTC format, not deletion authority.
    public static class DevelopmentLogRetention
    {
        public const string UsageName = "LastUsedUtc.txt";
        public const string LockName = ".Retention.lock";
        internal const string RawName = "RuntimeLog.txt";
        internal const int RetentionDays = 30;
        private static readonly Regex Session = new Regex(@"^\d{8}-\d{6}-\d{3}-[a-f0-9]{32}$", RegexOptions.CultureInvariant);
        private static int _running;

        internal static bool TrySessionUtc(string name, out DateTime utc)
        {
            utc = default;
            if (name == null || !Session.IsMatch(name) || !DateTime.TryParseExact(name.Substring(0, 19),
                "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value)) return false;
            utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return true;
        }

        internal static bool IsExpired(DateTime now, DateTime created, DateTime written, DateTime? used)
        {
            if (now.Kind != DateTimeKind.Utc || created.Kind != DateTimeKind.Utc || written.Kind != DateTimeKind.Utc
                || (used.HasValue && used.Value.Kind != DateTimeKind.Utc)) return false;
            if (created > now || written > now || (used.HasValue && used.Value > now)) return false;
            DateTime latest = created > written ? created : written;
            if (used.HasValue && used.Value > latest) latest = used.Value;
            return now - latest >= TimeSpan.FromDays(RetentionDays);
        }

        internal static bool Plain(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;

        internal static bool TryReadUsage(string path, out string text)
        {
            text = null;
            if (!Plain(path)) return false;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > 128) return false;
                byte[] bytes = new byte[129];
                int length = 0, read;
                while (length < bytes.Length && (read = stream.Read(bytes, length, bytes.Length - length)) > 0) length += read;
                if (length > 128) return false;
                text = new System.Text.UTF8Encoding(false, true).GetString(bytes, 0, length).Trim();
                return true;
            }
        }

        // Unity APIs and log callbacks are deliberately absent from this worker.
        // The cap counts eligible deletion attempts, so protected early folders cannot starve later ones.
        internal static void Schedule(string root, string activeFile)
        {
            if (string.IsNullOrEmpty(activeFile) || Interlocked.Exchange(ref _running, 1) != 0) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { Sweep(root, Path.GetDirectoryName(activeFile), DateTime.UtcNow, 32); }
                catch (Exception error)
                {
                    // Startup remains nonblocking, but an enumeration failure is not reported as zero deletions.
                    try
                    {
                        string audit = Path.Combine(root, "RetentionAudit.txt");
                        if (Directory.Exists(root) && Plain(root) && (!File.Exists(audit) || Plain(audit)))
                            File.WriteAllText(audit, "utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                                + ";sweepFailed=" + error.GetType().Name);
                    }
                    catch { }
                }
                finally { Volatile.Write(ref _running, 0); }
            });
        }

        internal static int Sweep(string root, string activeDirectory, DateTime now, int maxAttempts)
        {
            int attempted = 0, deleted = 0, skipped = 0, failed = 0, scanned = 0;
            long deletedBytes = 0;
            var deletedNames = new System.Collections.Generic.List<string>();
            root = Path.GetFullPath(root);
            if (!Directory.Exists(root) || !Plain(root)) return 0;
            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                if (attempted >= maxAttempts) break;
                scanned++;
                FileStream claim = null;
                string claimPath = Path.Combine(directory, LockName);
                try
                {
                    if (Path.GetFullPath(directory) == Path.GetFullPath(activeDirectory) || !Plain(directory)
                        || !TrySessionUtc(Path.GetFileName(directory), out DateTime created)) continue;
                    string raw = Path.Combine(directory, RawName), marker = Path.Combine(directory, UsageName);
                    if (!File.Exists(raw) || !Plain(raw)) continue;
                    bool unknown = false;
                    foreach (string child in Directory.EnumerateFileSystemEntries(directory))
                        if ((child != raw && child != marker) || !Plain(child) || Directory.Exists(child)) { unknown = true; break; }
                    if (unknown) continue;
                    DateTime? used = null;
                    if (File.Exists(marker))
                    {
                        if (!TryReadUsage(marker, out string text)) continue;
                        if (!DateTime.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time)
                            || time.Kind != DateTimeKind.Utc) continue;
                        used = time;
                    }
                    DateTime write = File.GetLastWriteTimeUtc(raw);
                    if (!IsExpired(now, created, write, used)) continue;
                    // CreateNew matches the Editor shell noclobber claim; only one mutator can own a session.
                    claim = new FileStream(claimPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    long bytes = new FileInfo(raw).Length;
                    attempted++;
                    // Repeat all mutable boundaries; a vanished marker is not equivalent to "never used".
                    if (!Plain(root) || !Plain(directory) || !Plain(raw) || File.GetLastWriteTimeUtc(raw) != write
                        || File.Exists(marker) != used.HasValue) { skipped++; continue; }
                    bool changed = false;
                    foreach (string child in Directory.EnumerateFileSystemEntries(directory))
                        if ((child != raw && child != marker && child != claimPath) || !Plain(child) || Directory.Exists(child)) { changed = true; break; }
                    if (changed || (used.HasValue && (!TryReadUsage(marker, out string latestUsage)
                        || latestUsage != used.Value.ToString("O", CultureInfo.InvariantCulture)))) { skipped++; continue; }
                    File.Delete(raw);
                    if (File.Exists(marker)) File.Delete(marker);

                    deleted++; deletedBytes += bytes; deletedNames.Add(Path.GetFileName(directory));
                }
                catch { failed++; /* Unknown or changed entries are protected; continue. */ }
                finally
                {
                    if (claim != null)
                    {
                        claim.Dispose();
                        try
                        {
                            File.Delete(claimPath);
                            using (var remaining = Directory.EnumerateFileSystemEntries(directory).GetEnumerator())
                                if (!remaining.MoveNext()) Directory.Delete(directory, false);
                        }
                        catch { }
                    }
                }
            }
            // A fixed, bounded audit under the known root contains no raw log payload.
            try
            {
                string audit = Path.Combine(root, "RetentionAudit.txt");
                if (!File.Exists(audit) || Plain(audit))
                    File.WriteAllText(audit, "utc=" + now.ToString("O", CultureInfo.InvariantCulture)
                        + ";scanned=" + scanned + ";notAttempted=" + (scanned - attempted) + ";attempted=" + attempted + ";deleted=" + deleted + ";bytes=" + deletedBytes
                        + ";changedSkipped=" + skipped + ";failed=" + failed + ";deletedSessions=" + string.Join(",", deletedNames));
            }
            catch { }
            return deleted;
        }
    }
}
#endif
