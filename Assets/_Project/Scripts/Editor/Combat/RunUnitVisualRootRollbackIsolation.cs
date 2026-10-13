using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// 별도 B0-seeded Unity worktree에서 production migration의 journal/rollback을 검증하는
    /// -executeMethod 전용 진입점이다. MenuItem은 의도적으로 제공하지 않는다.
    ///
    /// 외부 실행기는 다음 환경과 marker를 모두 준비해야 한다.
    /// - HEXIEGE_B1_ISOLATION_NONCE: 실행마다 새로 만든 32자 이상의 nonce
    /// - HEXIEGE_B1_EXPECTED_PROJECT_ROOT: 현재 격리 worktree의 절대 경로
    /// - HEXIEGE_B1_PRIMARY_PROJECT_ROOT: 절대 실행하면 안 되는 primary worktree 절대 경로
    /// - HEXIEGE_B1_FAILURE_INDEX: graceful은 0/24/49 중 하나, crash는 반드시 24
    /// - Library/Hexiege/UnitVisualRootRollbackIsolation/marker.json:
    ///   위 nonce/expectedRoot/primaryRoot와 동일한 값을 가진 사전 생성 marker
    ///
    /// primary checkout에서는 .git이 디렉터리지만 linked worktree에서는 .git이 파일이라는 점,
    /// expected/primary/current root 비교, batchmode, initial B0 exact state를 모두 만족해야만
    /// 기존 SetupUnitVisualRoots.Apply/Recover를 호출한다. 따라서 migrated primary 50 prefab에서
    /// 이 코드를 직접 실행하는 경로는 fail-closed다.
    /// </summary>
    public static class RunUnitVisualRootRollbackIsolation
    {
        private const string NonceEnvironment = "HEXIEGE_B1_ISOLATION_NONCE";
        private const string ExpectedRootEnvironment = "HEXIEGE_B1_EXPECTED_PROJECT_ROOT";
        private const string PrimaryRootEnvironment = "HEXIEGE_B1_PRIMARY_PROJECT_ROOT";
        private const string FailureIndexEnvironment = "HEXIEGE_B1_FAILURE_INDEX";

        private const string IsolationRelativeDirectory =
            "Library/Hexiege/UnitVisualRootRollbackIsolation";
        private const string MarkerFileName = "marker.json";
        private const string ResultFileName = "result.json";
        private const string CrashReachedFileName = "crash-reached.json";
        private const string ProductionJournalRelativePath =
            "Library/Hexiege/UnitVisualRootMigrationB1/journal.json";

        /// <summary>
        /// 정상 예외 경로 검증. 지정 index 저장 직후 기존 failure hook이 true를 반환하게 하여
        /// production Apply의 catch → exact restore 경로를 그대로 실행한다.
        /// </summary>
        public static void RunGraceful()
        {
            ExecuteWithResult("graceful", requirePendingJournal: false, context =>
            {
                int failureIndex = ReadFailureIndex(allowGracefulSet: true);
                RequireInitialB0Exact();
                RequireProductionJournalAbsent(context.ProjectRoot);

                bool injectionReached = false;
                SetupUnitVisualRoots.FailureInjectionForTests = (index, path) =>
                {
                    if (index != failureIndex) return false;
                    injectionReached = true;
                    return true;
                };

                SetupUnitVisualRoots.Apply();
                if (!injectionReached)
                    throw new InvalidOperationException(
                        $"failure injection index {failureIndex} was not reached");

                VerificationSummary verified =
                    VerifyRecoveredInitialState(context.ProjectRoot);
                return IsolationResult.Pass(
                    "graceful",
                    failureIndex,
                    verified,
                    "production Apply failure catch restored exact B0 bytes");
            });
        }

        /// <summary>
        /// 비정상 프로세스 종료 경로 검증. 25번째(index 24) prefab 저장 직후 hook 내부에서
        /// Environment.FailFast를 호출하므로 Apply의 catch/finally는 실행되지 않는다.
        /// journal은 pending=true, assets는 부분 migration 상태로 남아야 하며 다음 별도 Unity 실행에서
        /// RecoverCrash를 -executeMethod로 호출해 복구한다.
        /// </summary>
        public static void RunCrash()
        {
            IsolationContext context = ValidateIsolationGuards(
                "crash",
                requirePendingJournal: false);
            try
            {
                int failureIndex = ReadFailureIndex(allowGracefulSet: false);
                RequireInitialB0Exact();
                RequireProductionJournalAbsent(context.ProjectRoot);

                SetupUnitVisualRoots.FailureInjectionForTests = (index, path) =>
                {
                    if (index != failureIndex) return false;

                    WriteCrashReachedDurably(context, index, path);
                    Environment.FailFast(
                        $"[UAS-ROOT-ISOLATION] intentional crash after index {index}: {path}");
                    return false;
                };

                SetupUnitVisualRoots.Apply();
                throw new InvalidOperationException(
                    "crash injection did not terminate the process");
            }
            catch (Exception exception)
            {
                WriteFailureResult(context, "crash", exception);
                throw;
            }
            finally
            {
                // FailFast terminates before finally by design. This cleanup covers only
                // pre-injection validation/Apply failures where the Unity process survives.
                SetupUnitVisualRoots.FailureInjectionForTests = null;
            }
        }

        /// <summary>
        /// RunCrash 다음 별도 batchmode 프로세스에서 호출한다. pending journal이 아니면 실패하고,
        /// 기존 Recover만 호출한 뒤 journal recovered 상태, 50 prefab+50 meta SHA, initial analyzer를
        /// 모두 검증한다.
        /// </summary>
        public static void RecoverCrash()
        {
            ExecuteWithResult("recover", requirePendingJournal: true, context =>
            {
                int failureIndex = ReadFailureIndex(allowGracefulSet: false);
                CrashReachedMarker reached =
                    RequireCrashReached(context, failureIndex);
                MigrationJournalDto before = ReadProductionJournal(context.ProjectRoot);
                if (!before.Pending || before.Completed || before.Recovered)
                    throw new InvalidDataException(
                        "RecoverCrash requires pending=true/completed=false/recovered=false");
                if (before.Assets == null
                    || before.Assets.Count <= failureIndex
                    || !string.Equals(
                        before.Assets[failureIndex].AssetPath,
                        reached.AssetPath,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "crash-reached asset path does not match journal failure index");
                }

                // Do not mutate a crash copy unless it is the exact index-24 partial state.
                // A mismatch deliberately leaves the isolation copy and pending journal intact
                // for forensic inspection.
                CrashPreRecoverySummary preRecovery =
                    VerifyCrashPreRecoveryState(context.ProjectRoot, before);
                SetupUnitVisualRoots.Recover();
                VerificationSummary verified =
                    VerifyRecoveredInitialState(context.ProjectRoot);
                verified.PreRecoveryPrefabMismatchCount =
                    preRecovery.PrefabMismatchCount;
                verified.PreRecoveryMetaMismatchCount =
                    preRecovery.MetaMismatchCount;
                verified.PreRecoveryVisualRootCount =
                    preRecovery.VisualRootCount;
                verified.PreRecoveryProjectorCount =
                    preRecovery.ProjectorCount;
                return IsolationResult.Pass(
                    "recover",
                    failureIndex,
                    verified,
                    "pending crash journal recovered by production Recover");
            });
        }

        private static void ExecuteWithResult(
            string mode,
            bool requirePendingJournal,
            Func<IsolationContext, IsolationResult> action)
        {
            IsolationContext context = null;
            try
            {
                context = ValidateIsolationGuards(mode, requirePendingJournal);
                IsolationResult result = action(context);
                WriteResult(context, result);
                Debug.Log(
                    $"[UAS-ROOT-ISOLATION][PASS] mode={mode}, " +
                    $"result={Path.Combine(context.MarkerDirectory, ResultFileName)}");
            }
            catch (Exception exception)
            {
                if (context != null)
                    WriteFailureResult(context, mode, exception);
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                SetupUnitVisualRoots.FailureInjectionForTests = null;
            }
        }

        private static IsolationContext ValidateIsolationGuards(
            string mode,
            bool requirePendingJournal)
        {
            if (!UnityEngine.Application.isBatchMode)
                throw new InvalidOperationException(
                    "rollback isolation executeMethod requires Application.isBatchMode");

            string nonce = RequireEnvironment(NonceEnvironment);
            if (nonce.Length < 32)
                throw new InvalidDataException("isolation nonce must be at least 32 characters");

            string currentRoot = NormalizeRoot(
                Directory.GetParent(UnityEngine.Application.dataPath)?.FullName);
            string expectedRoot = NormalizeRoot(RequireEnvironment(ExpectedRootEnvironment));
            string primaryRoot = NormalizeRoot(RequireEnvironment(PrimaryRootEnvironment));
            if (!PathsEqual(currentRoot, expectedRoot))
                throw new InvalidOperationException(
                    $"current root differs from expected isolation root: " +
                    $"current={currentRoot}, expected={expectedRoot}");
            if (PathsEqual(currentRoot, primaryRoot))
                throw new InvalidOperationException(
                    "primary project root is forbidden for rollback isolation");
            if (!Directory.Exists(primaryRoot))
                throw new DirectoryNotFoundException(
                    $"primary project root does not exist: {primaryRoot}");
            string primaryGitDirectory = Path.Combine(primaryRoot, ".git");
            if (!Directory.Exists(primaryGitDirectory)
                || File.Exists(primaryGitDirectory))
            {
                throw new InvalidOperationException(
                    "primary project root must contain an actual .git directory");
            }

            // Linked git worktree의 .git은 gitdir 포인터 파일이다. primary checkout의 .git
            // 디렉터리에서는 이 검증이 실패하므로 잘못된 root 환경값만으로 우회할 수 없다.
            string gitMarker = Path.Combine(currentRoot, ".git");
            if (!File.Exists(gitMarker) || Directory.Exists(gitMarker))
                throw new InvalidOperationException(
                    "isolation root must be a linked worktree with a .git pointer file");
            ValidateLinkedWorktreeRelationship(
                currentRoot,
                gitMarker,
                primaryGitDirectory);

            string markerDirectory = Path.Combine(
                currentRoot,
                IsolationRelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            string markerPath = Path.Combine(markerDirectory, MarkerFileName);
            if (!File.Exists(markerPath))
                throw new FileNotFoundException(
                    "pre-created isolation marker is required",
                    markerPath);

            IsolationMarker marker = JsonUtility.FromJson<IsolationMarker>(
                File.ReadAllText(markerPath, Encoding.UTF8));
            if (marker == null
                || !string.Equals(marker.Nonce, nonce, StringComparison.Ordinal)
                || !PathsEqual(NormalizeRoot(marker.ExpectedProjectRoot), expectedRoot)
                || !PathsEqual(NormalizeRoot(marker.PrimaryProjectRoot), primaryRoot))
            {
                throw new InvalidDataException(
                    "marker nonce/root values do not match environment");
            }

            string journalPath = Path.Combine(
                currentRoot,
                ProductionJournalRelativePath.Replace('/', Path.DirectorySeparatorChar));
            bool hasJournal = File.Exists(journalPath);
            if (requirePendingJournal && !hasJournal)
                throw new FileNotFoundException(
                    "pending production journal is required",
                    journalPath);

            return new IsolationContext
            {
                Mode = mode,
                Nonce = nonce,
                ProjectRoot = currentRoot,
                PrimaryRoot = primaryRoot,
                MarkerDirectory = markerDirectory
            };
        }

        private static void RequireInitialB0Exact()
        {
            UnitVisualRootAuditReport report =
                UnitVisualRootReadOnlyAnalyzer.AnalyzeAll();
            if (!report.Passed
                || report.Prefabs.Count != 50
                || report.ExistingVisualRootCount != 0
                || report.ProjectorCount != 0
                || report.ValidProjectorReferenceCount != 0
                || report.PlannedVisualRootCreateCount != 50
                || report.PlannedVisualRootReuseCount != 0
                || report.PlannedMoveCount != 58
                || report.DirectVfxSpawnPointCount != 8
                || report.NestedVfxSpawnPointCount != 8)
            {
                throw new InvalidOperationException(
                    "isolation worktree must be exact initial B0 state: " +
                    "prefab50/root0/projector0/ref0/create50/reuse0/move58/directVfx8/nestedVfx8");
            }
        }

        private static VerificationSummary VerifyRecoveredInitialState(string projectRoot)
        {
            MigrationJournalDto journal = ReadProductionJournal(projectRoot);
            if (journal.Pending || journal.Completed || !journal.Recovered)
                throw new InvalidDataException(
                    "recovered journal must be pending=false/completed=false/recovered=true");
            if (journal.Assets == null || journal.Assets.Count != 50)
                throw new InvalidDataException("recovered journal must contain 50 assets");

            int verifiedFiles = 0;
            var verifiedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (JournalAssetDto item in journal.Assets)
            {
                if (!string.Equals(
                        item.MetaPath,
                        item.AssetPath + ".meta",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"journal meta path does not match prefab path: {item.AssetPath}");
                }
                if (!verifiedPaths.Add(item.AssetPath)
                    || !verifiedPaths.Add(item.MetaPath))
                {
                    throw new InvalidDataException(
                        $"journal contains a duplicate asset/meta path: {item.AssetPath}");
                }
                VerifyAssetSha(projectRoot, item.AssetPath, item.PrefabSha256);
                VerifyAssetSha(projectRoot, item.MetaPath, item.MetaSha256);
                verifiedFiles += 2;
            }
            if (verifiedFiles != 100)
                throw new InvalidOperationException(
                    $"expected 100 verified files, found {verifiedFiles}");

            RequireInitialB0Exact();
            return new VerificationSummary
            {
                JournalRecovered = true,
                VerifiedFileCount = verifiedFiles,
                InitialAnalyzerPassed = true
            };
        }

        private static CrashPreRecoverySummary VerifyCrashPreRecoveryState(
            string projectRoot,
            MigrationJournalDto journal)
        {
            if (journal.Assets == null || journal.Assets.Count != 50)
                throw new InvalidDataException(
                    "crash pre-recovery journal must contain exactly 50 assets");

            var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
            int prefabMismatchCount = 0;
            int metaMismatchCount = 0;
            foreach (JournalAssetDto item in journal.Assets)
            {
                if (!string.Equals(
                        item.MetaPath,
                        item.AssetPath + ".meta",
                        StringComparison.Ordinal)
                    || !uniquePaths.Add(item.AssetPath)
                    || !uniquePaths.Add(item.MetaPath))
                {
                    throw new InvalidDataException(
                        $"invalid or duplicate crash journal path: {item.AssetPath}");
                }

                if (!string.Equals(
                        ReadAssetSha(projectRoot, item.AssetPath),
                        item.PrefabSha256,
                        StringComparison.Ordinal))
                {
                    prefabMismatchCount++;
                }
                if (!string.Equals(
                        ReadAssetSha(projectRoot, item.MetaPath),
                        item.MetaSha256,
                        StringComparison.Ordinal))
                {
                    metaMismatchCount++;
                }
            }

            UnitVisualRootAuditReport report =
                UnitVisualRootReadOnlyAnalyzer.AnalyzeAll();
            var summary = new CrashPreRecoverySummary
            {
                PrefabMismatchCount = prefabMismatchCount,
                MetaMismatchCount = metaMismatchCount,
                VisualRootCount = report.ExistingVisualRootCount,
                ProjectorCount = report.ProjectorCount
            };
            if (prefabMismatchCount != 25
                || metaMismatchCount != 0
                || report.Prefabs.Count != 50
                || report.ExistingVisualRootCount != 25
                || report.ProjectorCount != 25)
            {
                throw new CrashPreRecoveryVerificationException(
                    summary,
                    "expected exact crash state prefabMismatch=25/metaMismatch=0/" +
                    "prefabCount=50/visualRoot=25/projector=25; Recover was not called");
            }
            return summary;
        }

        private static void ValidateLinkedWorktreeRelationship(
            string currentRoot,
            string currentGitFile,
            string primaryGitDirectory)
        {
            string pointer = File.ReadAllText(currentGitFile, Encoding.UTF8).Trim();
            const string Prefix = "gitdir:";
            if (!pointer.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("linked worktree .git file has no gitdir pointer");

            string worktreeAdminDirectory = ResolveGitPath(
                currentRoot,
                pointer.Substring(Prefix.Length).Trim());
            string worktreesRoot = NormalizeRoot(
                    Path.Combine(primaryGitDirectory, "worktrees"))
                + Path.DirectorySeparatorChar;
            string worktreeAdminWithBoundary =
                NormalizeRoot(worktreeAdminDirectory) + Path.DirectorySeparatorChar;
            if (!worktreeAdminWithBoundary.StartsWith(
                    worktreesRoot,
                    StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(worktreeAdminDirectory))
            {
                throw new InvalidOperationException(
                    "linked worktree gitdir is not under primary .git/worktrees/");
            }

            string commonDirFile = Path.Combine(worktreeAdminDirectory, "commondir");
            if (!File.Exists(commonDirFile))
                throw new FileNotFoundException(
                    "linked worktree admin directory has no commondir",
                    commonDirFile);
            string resolvedCommonDirectory = ResolveGitPath(
                worktreeAdminDirectory,
                File.ReadAllText(commonDirFile, Encoding.UTF8).Trim());
            if (!PathsEqual(resolvedCommonDirectory, primaryGitDirectory))
                throw new InvalidOperationException(
                    "linked worktree commondir does not resolve to primary .git");

            string adminGitDirFile = Path.Combine(worktreeAdminDirectory, "gitdir");
            if (!File.Exists(adminGitDirFile))
                throw new FileNotFoundException(
                    "linked worktree admin directory has no reverse gitdir",
                    adminGitDirFile);
            string reverseGitFile = ResolveGitPath(
                worktreeAdminDirectory,
                File.ReadAllText(adminGitDirFile, Encoding.UTF8).Trim());
            if (!PathsEqual(reverseGitFile, currentGitFile))
                throw new InvalidOperationException(
                    "linked worktree admin gitdir does not point back to current .git file");
        }

        private static string ResolveGitPath(string relativeToDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("git administrative path is empty");
            return NormalizeRoot(
                Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(relativeToDirectory, path));
        }

        private static MigrationJournalDto ReadProductionJournal(string projectRoot)
        {
            string path = Path.Combine(
                projectRoot,
                ProductionJournalRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                throw new FileNotFoundException("production migration journal missing", path);
            MigrationJournalDto journal = JsonUtility.FromJson<MigrationJournalDto>(
                File.ReadAllText(path, Encoding.UTF8));
            if (journal == null)
                throw new InvalidDataException($"invalid migration journal: {path}");
            return journal;
        }

        private static void RequireProductionJournalAbsent(string projectRoot)
        {
            string path = Path.Combine(
                projectRoot,
                ProductionJournalRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                throw new InvalidOperationException(
                    $"clean isolation run requires no existing production journal: {path}");
        }

        private static void VerifyAssetSha(
            string projectRoot,
            string assetPath,
            string expectedSha)
        {
            string actual = ReadAssetSha(projectRoot, assetPath);
            if (!string.Equals(actual, expectedSha, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"{assetPath}: SHA mismatch expected={expectedSha}, actual={actual}");
        }

        private static string ReadAssetSha(string projectRoot, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)
                || !assetPath.StartsWith(
                    "Assets/_Project/Prefabs/Units/",
                    StringComparison.Ordinal)
                || assetPath.Contains(".."))
            {
                throw new InvalidDataException(
                    $"journal contains unsafe asset path: {assetPath}");
            }

            string absolute = Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
            string assetsRoot =
                Path.GetFullPath(Path.Combine(projectRoot, "Assets"))
                + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(absolute))
            {
                throw new InvalidDataException($"asset file unavailable: {assetPath}");
            }

            return ComputeSha256(File.ReadAllBytes(absolute));
        }

        private static int ReadFailureIndex(bool allowGracefulSet)
        {
            string raw = RequireEnvironment(FailureIndexEnvironment);
            if (!int.TryParse(
                    raw,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int index))
            {
                throw new InvalidDataException($"invalid failure index: {raw}");
            }

            if (allowGracefulSet)
            {
                if (index != 0 && index != 24 && index != 49)
                    throw new InvalidDataException(
                        "graceful failure index must be 0, 24, or 49");
            }
            else if (index != 24)
            {
                throw new InvalidDataException(
                    "crash/recover failure index must be 24");
            }
            return index;
        }

        private static void WriteFailureResult(
            IsolationContext context,
            string mode,
            Exception exception)
        {
            try
            {
                var result = new IsolationResult
                {
                    Status = "FAIL",
                    Mode = mode,
                    FailureIndex = -1,
                    Message =
                        $"{exception.GetType().Name}: {exception.Message}"
                };
                if (exception is CrashPreRecoveryVerificationException preRecovery)
                {
                    result.FailureIndex = 24;
                    result.PreRecoveryPrefabMismatchCount =
                        preRecovery.Summary.PrefabMismatchCount;
                    result.PreRecoveryMetaMismatchCount =
                        preRecovery.Summary.MetaMismatchCount;
                    result.PreRecoveryVisualRootCount =
                        preRecovery.Summary.VisualRootCount;
                    result.PreRecoveryProjectorCount =
                        preRecovery.Summary.ProjectorCount;
                }
                WriteResult(context, result);
            }
            catch (Exception writeException)
            {
                Debug.LogException(writeException);
            }
        }

        private static void WriteResult(
            IsolationContext context,
            IsolationResult result)
        {
            Directory.CreateDirectory(context.MarkerDirectory);
            result.Nonce = context.Nonce;
            result.ProjectRoot = context.ProjectRoot;
            result.PrimaryRoot = context.PrimaryRoot;
            result.Utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string json = JsonUtility.ToJson(result, prettyPrint: true);
            File.WriteAllText(
                Path.Combine(context.MarkerDirectory, ResultFileName),
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void WriteCrashReachedDurably(
            IsolationContext context,
            int index,
            string assetPath)
        {
            var reached = new CrashReachedMarker
            {
                Nonce = context.Nonce,
                ProjectRoot = context.ProjectRoot,
                PrimaryRoot = context.PrimaryRoot,
                FailureIndex = index,
                AssetPath = assetPath,
                Utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            string json = JsonUtility.ToJson(reached, prettyPrint: true);
            string path = Path.Combine(
                context.MarkerDirectory,
                CrashReachedFileName);
            byte[] bytes =
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                    .GetBytes(json);
            using (var stream = new FileStream(
                       path,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.Read,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
        }

        private static CrashReachedMarker RequireCrashReached(
            IsolationContext context,
            int expectedIndex)
        {
            string path = Path.Combine(
                context.MarkerDirectory,
                CrashReachedFileName);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "durable crash-reached marker is required before recovery",
                    path);
            CrashReachedMarker reached = JsonUtility.FromJson<CrashReachedMarker>(
                File.ReadAllText(path, Encoding.UTF8));
            if (reached == null
                || !string.Equals(
                    reached.Nonce,
                    context.Nonce,
                    StringComparison.Ordinal)
                || !PathsEqual(
                    NormalizeRoot(reached.ProjectRoot),
                    context.ProjectRoot)
                || !PathsEqual(
                    NormalizeRoot(reached.PrimaryRoot),
                    context.PrimaryRoot)
                || reached.FailureIndex != expectedIndex
                || string.IsNullOrEmpty(reached.AssetPath)
                || !reached.AssetPath.StartsWith(
                    "Assets/_Project/Prefabs/Units/",
                    StringComparison.Ordinal)
                || reached.AssetPath.Contains(".."))
            {
                throw new InvalidDataException(
                    "crash-reached marker does not match isolation marker/context");
            }
            return reached;
        }

        private static string RequireEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"required environment variable missing: {name}");
            return value.Trim();
        }

        private static string NormalizeRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("project root is empty");
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool PathsEqual(string left, string right)
            => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        [Serializable]
        private sealed class IsolationMarker
        {
            public string Nonce;
            public string ExpectedProjectRoot;
            public string PrimaryProjectRoot;
        }

        private sealed class IsolationContext
        {
            public string Mode;
            public string Nonce;
            public string ProjectRoot;
            public string PrimaryRoot;
            public string MarkerDirectory;
        }

        [Serializable]
        private sealed class CrashReachedMarker
        {
            public string Nonce;
            public string ProjectRoot;
            public string PrimaryRoot;
            public int FailureIndex;
            public string AssetPath;
            public string Utc;
        }

        [Serializable]
        private sealed class MigrationJournalDto
        {
            public bool Pending;
            public bool Completed;
            public bool Recovered;
            public List<JournalAssetDto> Assets = new List<JournalAssetDto>();
        }

        [Serializable]
        private sealed class JournalAssetDto
        {
            public string AssetPath;
            public string MetaPath;
            public string PrefabSha256;
            public string MetaSha256;
        }

        private sealed class VerificationSummary
        {
            public bool JournalRecovered;
            public int VerifiedFileCount;
            public bool InitialAnalyzerPassed;
            public int PreRecoveryPrefabMismatchCount;
            public int PreRecoveryMetaMismatchCount;
            public int PreRecoveryVisualRootCount;
            public int PreRecoveryProjectorCount;
        }

        private sealed class CrashPreRecoverySummary
        {
            public int PrefabMismatchCount;
            public int MetaMismatchCount;
            public int VisualRootCount;
            public int ProjectorCount;
        }

        private sealed class CrashPreRecoveryVerificationException : Exception
        {
            public CrashPreRecoverySummary Summary { get; }

            public CrashPreRecoveryVerificationException(
                CrashPreRecoverySummary summary,
                string message)
                : base(message)
            {
                Summary = summary;
            }
        }

        [Serializable]
        private sealed class IsolationResult
        {
            public string Status;
            public string Mode;
            public string Nonce;
            public string Utc;
            public string ProjectRoot;
            public string PrimaryRoot;
            public int FailureIndex;
            public bool JournalRecovered;
            public int VerifiedFileCount;
            public bool InitialAnalyzerPassed;
            public int PreRecoveryPrefabMismatchCount;
            public int PreRecoveryMetaMismatchCount;
            public int PreRecoveryVisualRootCount;
            public int PreRecoveryProjectorCount;
            public string Message;

            public static IsolationResult Pass(
                string mode,
                int failureIndex,
                VerificationSummary verified,
                string message)
            {
                return new IsolationResult
                {
                    Status = "PASS",
                    Mode = mode,
                    FailureIndex = failureIndex,
                    JournalRecovered = verified.JournalRecovered,
                    VerifiedFileCount = verified.VerifiedFileCount,
                    InitialAnalyzerPassed = verified.InitialAnalyzerPassed,
                    PreRecoveryPrefabMismatchCount =
                        verified.PreRecoveryPrefabMismatchCount,
                    PreRecoveryMetaMismatchCount =
                        verified.PreRecoveryMetaMismatchCount,
                    PreRecoveryVisualRootCount =
                        verified.PreRecoveryVisualRootCount,
                    PreRecoveryProjectorCount =
                        verified.PreRecoveryProjectorCount,
                    Message = message
                };
            }
        }
    }
}
