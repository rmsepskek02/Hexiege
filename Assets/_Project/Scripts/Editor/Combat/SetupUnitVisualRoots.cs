using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Presentation;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// B0 analyzer가 승인한 정확히 50개 유닛 프리팹에 B1 Visual Root 구조를 원자적으로 적용한다.
    ///
    /// 안전 계약:
    /// - Dry Run은 읽기 전용이다.
    /// - Apply만 정상 migration을 저장한다.
    /// - Apply 시작 전에 unfinished journal이 있으면 다른 검사보다 exact-byte 복구를 먼저 한다.
    /// - mutation 전에 prefab과 .meta 100개 파일의 원본 bytes/SHA-256을 Library에 기록한다.
    /// - 하나라도 실패하면 전체 100개 파일을 원본 bytes로 복원하고 강제 Import한 뒤 hash를 검증한다.
    /// - 이미 완전 migration된 상태는 no-op이며 SaveAsPrefabAsset/SaveAssets를 호출하지 않는다.
    ///
    /// 테스트용 고의 실패는 메모리 hook만 제공한다. EditorPrefs나 메뉴로 영속화하지 않으므로
    /// 도메인 reload 뒤 production Apply에 우발적으로 남을 수 없다.
    /// </summary>
    public static class SetupUnitVisualRoots
    {
        private const string MenuRoot = "Hexiege/Combat/Visual Root/";
        private const string VisualRootName = "VisualRoot";
        private const int JournalVersion = 1;
        private const string JournalRelativeDirectory =
            "Library/Hexiege/UnitVisualRootMigrationB1";
        private const string JournalFileName = "journal.json";

        /// <summary>
        /// Editor test가 특정 저장 직후 예외를 주입하는 비영속 hook.
        /// 인자는 zero-based index와 asset path다. production 메뉴/UI에서는 설정할 수 없다.
        /// </summary>
        internal static Func<int, string, bool> FailureInjectionForTests;

        [MenuItem(MenuRoot + "Dry Run Unit Visual Root Migration")]
        public static void DryRun()
        {
            UnitVisualRootAuditReport report = UnitVisualRootReadOnlyAnalyzer.AnalyzeAll();
            report.Log("dry-run", includeMigrationPlan: true);
        }

        [MenuItem(MenuRoot + "Apply Unit Visual Root Migration")]
        public static void Apply()
        {
            try
            {
                // Crash/domain reload로 이전 Apply가 pending이면 현재 prefab 상태를 분석하지 않는다.
                // 부분 상태 analyzer 결과에 따라 재개하는 것은 원자성을 깨므로 항상 원본 전체 복구가 우선이다.
                if (!RecoverPendingJournalIfAny("apply-preflight"))
                    return;

                UnitVisualRootAuditReport before =
                    UnitVisualRootReadOnlyAnalyzer.AnalyzeAll();
                before.Log("apply-preflight", includeMigrationPlan: true);
                RequirePassed(before, "B1 Apply preflight");

                if (IsCompletedState(before))
                {
                    Debug.Log(
                        "[UAS-ROOT][apply][NO-OP] 50개 프리팹이 이미 migrated state입니다. " +
                        "SaveAsPrefabAsset/SaveAssets 호출 없이 종료합니다.");
                    return;
                }

                RequireInitialState(before);
                MigrationJournal journal = CreatePendingJournal(before);

                try
                {
                    for (int index = 0; index < before.Prefabs.Count; index++)
                    {
                        UnitVisualRootPrefabAudit audit = before.Prefabs[index];
                        MigrateOnePrefab(audit.Path, index, before.Prefabs.Count);

                        if (FailureInjectionForTests != null
                            && FailureInjectionForTests(index, audit.Path))
                        {
                            throw new InvalidOperationException(
                                $"test failure injected after prefab {index + 1}: {audit.Path}");
                        }
                    }

                    UnitVisualRootAuditReport after =
                        UnitVisualRootReadOnlyAnalyzer.AnalyzeAll();
                    after.Log("apply-post", includeMigrationPlan: true);
                    RequirePassed(after, "B1 Apply post analyzer");
                    RequireCompletedState(after);
                    VerifyStructuredNetworkBaselines(journal, after);
                    VerifyMetaFilesUnchanged(journal);

                    journal.Pending = false;
                    journal.Completed = true;
                    journal.Recovered = false;
                    journal.CompletedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                    WriteJournal(journal);

                    Debug.Log(
                        "[UAS-ROOT][apply][PASS] 50/50 Visual Root migration 완료. " +
                        "post=create0/reuse50/move0/directVfx0/nestedVfx16, " +
                        "network identity/config baseline preserved.");
                }
                catch (Exception migrationException)
                {
                    Exception recoveryException = null;
                    try
                    {
                        RestoreExactBytes(journal, "apply-failure");
                    }
                    catch (Exception exception)
                    {
                        recoveryException = exception;
                    }

                    if (recoveryException != null)
                    {
                        throw new AggregateException(
                            "B1 migration failed and exact-byte recovery also failed. " +
                            "Journal remains pending; run Recover before any retry.",
                            migrationException,
                            recoveryException);
                    }

                    throw new InvalidOperationException(
                        "B1 migration failed; all 50 prefab+.meta files were exact-byte restored.",
                        migrationException);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                // Test assembly가 hook을 해제하지 못해도 다음 수동 Apply로 새지 않도록 one-run으로 소비한다.
                FailureInjectionForTests = null;
            }
        }

        [MenuItem(MenuRoot + "Recover Pending Unit Visual Root Migration")]
        public static void Recover()
        {
            try
            {
                if (!TryLoadJournal(out MigrationJournal journal))
                {
                    Debug.Log("[UAS-ROOT][recover][NO-OP] journal이 없습니다.");
                    return;
                }

                if (!journal.Pending)
                {
                    Debug.Log(
                        "[UAS-ROOT][recover][NO-OP] journal은 pending이 아닙니다. " +
                        $"completed={journal.Completed}, recovered={journal.Recovered}");
                    return;
                }

                RestoreExactBytes(journal, "manual-recover");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static bool RecoverPendingJournalIfAny(string reason)
        {
            if (!TryLoadJournal(out MigrationJournal journal) || !journal.Pending)
                return true;

            Debug.LogWarning(
                "[UAS-ROOT][recover][BEGIN] unfinished pending journal을 발견했습니다. " +
                "B0 검사보다 exact-byte 복구를 먼저 실행합니다.");
            try
            {
                RestoreExactBytes(journal, reason);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static MigrationJournal CreatePendingJournal(
            UnitVisualRootAuditReport before)
        {
            string directory = GetJournalDirectory();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
            Directory.CreateDirectory(directory);

            var journal = new MigrationJournal
            {
                Version = JournalVersion,
                Pending = true,
                Completed = false,
                Recovered = false,
                CreatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };

            List<UnitVisualRootPrefabAudit> ordered = before.Prefabs
                .OrderBy(value => value.Path, StringComparer.Ordinal)
                .ToList();
            for (int index = 0; index < ordered.Count; index++)
            {
                UnitVisualRootPrefabAudit audit = ordered[index];
                string metaPath = audit.Path + ".meta";
                byte[] prefabBytes = ReadRequiredAssetBytes(audit.Path);
                byte[] metaBytes = ReadRequiredAssetBytes(metaPath);
                string prefabBackup = $"{index:D2}.prefab.bytes";
                string metaBackup = $"{index:D2}.meta.bytes";

                WriteVerifiedLibraryBytes(
                    Path.Combine(directory, prefabBackup),
                    prefabBytes);
                WriteVerifiedLibraryBytes(
                    Path.Combine(directory, metaBackup),
                    metaBytes);

                journal.Assets.Add(new JournalAsset
                {
                    AssetPath = audit.Path,
                    MetaPath = metaPath,
                    PrefabBackupFile = prefabBackup,
                    MetaBackupFile = metaBackup,
                    PrefabSha256 = ComputeSha256(prefabBytes),
                    MetaSha256 = ComputeSha256(metaBytes),
                    NetworkObjectGlobalObjectIdHash =
                        audit.NetworkObjectGlobalObjectIdHash,
                    NetworkObjectSerializedSha256 =
                        audit.NetworkObjectSerializedSha256,
                    NetworkTransformSerializedSha256 =
                        audit.NetworkTransformSerializedSha256
                });
            }

            ValidateJournal(journal);
            // 이 write가 성공한 뒤에만 prefab mutation을 시작한다.
            WriteJournal(journal);
            Debug.Log(
                "[UAS-ROOT][journal][PENDING] exact bytes 저장 완료: " +
                "50 prefab + 50 meta, pending=true.");
            return journal;
        }

        private static void MigrateOnePrefab(string path, int index, int total)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    throw new InvalidOperationException($"LoadPrefabContents returned null: {path}");

                // B0 initial PASS 뒤에도 프리팹별로 다시 fail-closed 한다.
                if (root.transform.Find(VisualRootName) != null)
                    throw new InvalidOperationException($"{path}: VisualRoot already exists");
                if (root.GetComponentsInChildren<VisualRootProjector>(true).Length != 0)
                    throw new InvalidOperationException($"{path}: projector already exists");

                List<Transform> copiedDirectChildren = new List<Transform>();
                for (int childIndex = 0; childIndex < root.transform.childCount; childIndex++)
                    copiedDirectChildren.Add(root.transform.GetChild(childIndex));
                if (copiedDirectChildren.Count == 0)
                    throw new InvalidOperationException($"{path}: no direct visual children");

                var visualRootObject = new GameObject(VisualRootName);
                Transform visualRoot = visualRootObject.transform;
                visualRoot.SetParent(root.transform, worldPositionStays: false);
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;
                visualRoot.SetSiblingIndex(0);

                // Create 전에 복사한 원래 direct child만, 원래 sibling 순서대로 이동한다.
                // VisualRoot가 identity이므로 SetParent(false)는 각 child의 local pose를 그대로 보존한다.
                for (int childIndex = 0;
                     childIndex < copiedDirectChildren.Count;
                     childIndex++)
                {
                    Transform child = copiedDirectChildren[childIndex];
                    child.SetParent(visualRoot, worldPositionStays: false);
                    child.SetSiblingIndex(childIndex);
                }

                // NetworkTransform/UnitView가 Simulation Root의 유일한 이동 writer다.
                // 모델 Animator의 root motion은 Visual Root를 별도로 움직이는 두 번째 writer가 되므로
                // migration 결과에서는 모든 Animator에 대해 명시적으로 비활성화한다.
                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                    animator.applyRootMotion = false;

                VisualRootProjector projector =
                    root.AddComponent<VisualRootProjector>();
                var serializedProjector = new SerializedObject(projector);
                serializedProjector.UpdateIfRequiredOrScript();
                SerializedProperty property =
                    serializedProjector.FindProperty("_visualRoot");
                if (property == null
                    || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    throw new MissingFieldException(
                        typeof(VisualRootProjector).FullName,
                        "_visualRoot");
                }
                property.objectReferenceValue = visualRoot;
                if (!serializedProjector.ApplyModifiedPropertiesWithoutUndo())
                    throw new InvalidOperationException(
                        $"{path}: projector serialized reference was not applied");

                GameObject saved =
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || saved == null)
                    throw new InvalidOperationException($"SaveAsPrefabAsset failed: {path}");
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);
            ValidateReloadedPrefab(path);
            Debug.Log(
                $"[UAS-ROOT][apply][PREFAB {index + 1:D2}/{total:D2}] saved+reloaded {path}");
        }

        private static void ValidateReloadedPrefab(string path)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    throw new InvalidOperationException($"reload returned null: {path}");

                Transform visualRoot = root.transform.Find(VisualRootName);
                if (visualRoot == null || visualRoot.parent != root.transform)
                    throw new InvalidOperationException($"{path}: direct VisualRoot missing after reload");
                if (visualRoot.localPosition != Vector3.zero
                    || visualRoot.localRotation != Quaternion.identity
                    || visualRoot.localScale != Vector3.one)
                {
                    throw new InvalidOperationException(
                        $"{path}: VisualRoot identity changed after reload");
                }
                if (root.transform.childCount != 1)
                    throw new InvalidOperationException(
                        $"{path}: direct children remain outside VisualRoot");

                VisualRootProjector[] projectors =
                    root.GetComponentsInChildren<VisualRootProjector>(true);
                if (projectors.Length != 1 || projectors[0].gameObject != root)
                    throw new InvalidOperationException(
                        $"{path}: expected one root VisualRootProjector after reload");
                var serializedProjector = new SerializedObject(projectors[0]);
                serializedProjector.UpdateIfRequiredOrScript();
                SerializedProperty property =
                    serializedProjector.FindProperty("_visualRoot");
                if (property == null || property.objectReferenceValue != visualRoot)
                    throw new InvalidOperationException(
                        $"{path}: projector reference did not survive reload");

                Animator[] animators = root.GetComponentsInChildren<Animator>(true);
                if (animators.Length == 0
                    || animators.Any(animator =>
                        animator.applyRootMotion
                        || !animator.transform.IsChildOf(visualRoot)))
                {
                    throw new InvalidOperationException(
                        $"{path}: Animator coverage/applyRootMotion invalid after reload");
                }
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void VerifyStructuredNetworkBaselines(
            MigrationJournal journal,
            UnitVisualRootAuditReport after)
        {
            Dictionary<string, UnitVisualRootPrefabAudit> postByPath =
                after.Prefabs.ToDictionary(
                    value => value.Path,
                    value => value,
                    StringComparer.Ordinal);

            foreach (JournalAsset baseline in journal.Assets)
            {
                if (!postByPath.TryGetValue(
                        baseline.AssetPath,
                        out UnitVisualRootPrefabAudit post))
                {
                    throw new InvalidOperationException(
                        $"post analyzer missing {baseline.AssetPath}");
                }

                RequireEqual(
                    baseline.NetworkObjectGlobalObjectIdHash,
                    post.NetworkObjectGlobalObjectIdHash,
                    baseline.AssetPath,
                    "NetworkObject.GlobalObjectIdHash");
                RequireEqual(
                    baseline.NetworkObjectSerializedSha256,
                    post.NetworkObjectSerializedSha256,
                    baseline.AssetPath,
                    "NetworkObject serialized config SHA-256");
                RequireEqual(
                    baseline.NetworkTransformSerializedSha256,
                    post.NetworkTransformSerializedSha256,
                    baseline.AssetPath,
                    "NetworkTransform serialized config SHA-256");
            }
        }

        private static void VerifyMetaFilesUnchanged(MigrationJournal journal)
        {
            // Prefab YAML은 migration 결과로 바뀌지만 .meta identity는 한 byte도 바뀌면 안 된다.
            // pending=false를 확정하기 전에 50개 모두 원본 journal SHA-256과 비교한다.
            foreach (JournalAsset item in journal.Assets)
                VerifyAssetHash(item.MetaPath, item.MetaSha256);
        }

        private static void RestoreExactBytes(MigrationJournal journal, string reason)
        {
            ValidateJournal(journal);
            string directory = GetJournalDirectory();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (JournalAsset item in journal.Assets)
                {
                    byte[] prefabBytes = ReadVerifiedBackup(
                        directory,
                        item.PrefabBackupFile,
                        item.PrefabSha256);
                    byte[] metaBytes = ReadVerifiedBackup(
                        directory,
                        item.MetaBackupFile,
                        item.MetaSha256);

                    // exact recovery만 Assets 파일에 직접 bytes를 쓴다. 정상 Apply는
                    // PrefabUtility.SaveAsPrefabAsset만 사용한다.
                    File.WriteAllBytes(ToAbsoluteAssetPath(item.AssetPath), prefabBytes);
                    File.WriteAllBytes(ToAbsoluteAssetPath(item.MetaPath), metaBytes);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (JournalAsset item in journal.Assets)
            {
                AssetDatabase.ImportAsset(
                    item.AssetPath,
                    ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.ForceUpdate);
            }

            // Import 뒤에도 disk bytes가 journal과 정확히 같은지 prefab/meta 모두 검증한다.
            foreach (JournalAsset item in journal.Assets)
            {
                VerifyAssetHash(item.AssetPath, item.PrefabSha256);
                VerifyAssetHash(item.MetaPath, item.MetaSha256);
            }

            journal.Pending = false;
            journal.Completed = false;
            journal.Recovered = true;
            journal.CompletedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            WriteJournal(journal);
            Debug.Log(
                $"[UAS-ROOT][recover][PASS][reason={reason}] " +
                "50 prefab + 50 meta exact-byte restore/import/hash verification 완료.");
        }

        private static bool TryLoadJournal(out MigrationJournal journal)
        {
            journal = null;
            string path = GetJournalPath();
            if (!File.Exists(path))
                return false;

            string json = File.ReadAllText(path, Encoding.UTF8);
            journal = JsonUtility.FromJson<MigrationJournal>(json);
            if (journal == null)
                throw new InvalidDataException($"invalid migration journal: {path}");
            ValidateJournal(journal);
            return true;
        }

        private static void ValidateJournal(MigrationJournal journal)
        {
            if (journal.Version != JournalVersion)
                throw new InvalidDataException(
                    $"journal version {journal.Version}, expected {JournalVersion}");
            if (journal.Assets == null
                || journal.Assets.Count != UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount)
            {
                throw new InvalidDataException(
                    $"journal must contain exactly " +
                    $"{UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount} assets");
            }

            var uniquePaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (JournalAsset item in journal.Assets)
            {
                if (string.IsNullOrEmpty(item.AssetPath)
                    || !item.AssetPath.StartsWith(
                        "Assets/_Project/Prefabs/Units/",
                        StringComparison.Ordinal)
                    || !Path.GetFileName(item.AssetPath).StartsWith(
                        "Unit_",
                        StringComparison.Ordinal)
                    || !item.AssetPath.EndsWith(".prefab", StringComparison.Ordinal)
                    || item.AssetPath.Contains("/_Old/")
                    || !uniquePaths.Add(item.AssetPath))
                {
                    throw new InvalidDataException(
                        $"journal contains unsafe/duplicate non-canonical target: {item.AssetPath}");
                }
                if (item.MetaPath != item.AssetPath + ".meta")
                    throw new InvalidDataException(
                        $"invalid meta path in journal: {item.MetaPath}");
                ToAbsoluteAssetPath(item.AssetPath);
                ToAbsoluteAssetPath(item.MetaPath);
                RequireSha256(item.PrefabSha256, item.AssetPath);
                RequireSha256(item.MetaSha256, item.MetaPath);
                RequireSha256(
                    item.NetworkObjectSerializedSha256,
                    item.AssetPath + " NetworkObject baseline");
                RequireSha256(
                    item.NetworkTransformSerializedSha256,
                    item.AssetPath + " NetworkTransform baseline");
                if (string.IsNullOrEmpty(item.NetworkObjectGlobalObjectIdHash))
                    throw new InvalidDataException(
                        $"{item.AssetPath}: missing GlobalObjectIdHash baseline");
            }
        }

        private static void WriteJournal(MigrationJournal journal)
        {
            string directory = GetJournalDirectory();
            Directory.CreateDirectory(directory);
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(JsonUtility.ToJson(journal, prettyPrint: true));
            WriteVerifiedLibraryBytes(GetJournalPath(), bytes);
        }

        private static void WriteVerifiedLibraryBytes(string path, byte[] bytes)
        {
            string fullPath = Path.GetFullPath(path);
            string journalDirectory =
                Path.GetFullPath(GetJournalDirectory()) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(
                    journalDirectory,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    fullPath,
                    Path.GetFullPath(GetJournalPath()),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"journal write escaped Library directory: {fullPath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var stream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
            byte[] persisted = File.ReadAllBytes(fullPath);
            if (!persisted.SequenceEqual(bytes))
                throw new IOException($"Library exact-byte write verification failed: {fullPath}");
        }

        private static byte[] ReadVerifiedBackup(
            string directory,
            string fileName,
            string expectedSha256)
        {
            if (string.IsNullOrEmpty(fileName)
                || Path.GetFileName(fileName) != fileName)
            {
                throw new InvalidDataException($"invalid backup filename: {fileName}");
            }
            string path = Path.Combine(directory, fileName);
            byte[] bytes = File.ReadAllBytes(path);
            string actual = ComputeSha256(bytes);
            if (!string.Equals(actual, expectedSha256, StringComparison.Ordinal))
                throw new InvalidDataException($"backup SHA-256 mismatch: {path}");
            return bytes;
        }

        private static byte[] ReadRequiredAssetBytes(string assetPath)
        {
            string absolutePath = ToAbsoluteAssetPath(assetPath);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("required asset file missing", absolutePath);
            return File.ReadAllBytes(absolutePath);
        }

        private static void VerifyAssetHash(string assetPath, string expected)
        {
            byte[] bytes = ReadRequiredAssetBytes(assetPath);
            string actual = ComputeSha256(bytes);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"{assetPath}: restored SHA-256 mismatch; " +
                    $"expected={expected}, actual={actual}");
            }
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)
                || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || assetPath.Contains(".."))
            {
                throw new InvalidDataException($"unsafe asset path: {assetPath}");
            }

            string projectRoot = GetProjectRoot();
            string absolute = Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
            string assetsRoot =
                Path.GetFullPath(UnityEngine.Application.dataPath)
                + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"asset path escaped Assets: {assetPath}");
            return absolute;
        }

        private static string GetProjectRoot()
        {
            DirectoryInfo parent =
                Directory.GetParent(UnityEngine.Application.dataPath);
            if (parent == null)
                throw new DirectoryNotFoundException("Unity project root unavailable");
            return parent.FullName;
        }

        private static string GetJournalDirectory()
        {
            return Path.GetFullPath(
                Path.Combine(
                    GetProjectRoot(),
                    JournalRelativeDirectory.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static string GetJournalPath()
        {
            return Path.Combine(GetJournalDirectory(), JournalFileName);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static void RequireSha256(string value, string label)
        {
            if (string.IsNullOrEmpty(value)
                || value.Length != 64
                || value.Any(character =>
                    !((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f'))))
            {
                throw new InvalidDataException($"{label}: invalid SHA-256");
            }
        }

        private static void RequirePassed(
            UnitVisualRootAuditReport report,
            string label)
        {
            if (!report.Passed)
                throw new InvalidOperationException(
                    $"{label} failed with {report.Errors.Count} error(s)");
        }

        private static void RequireInitialState(UnitVisualRootAuditReport report)
        {
            if (report.Prefabs.Count != UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount
                || report.ExistingVisualRootCount != 0
                || report.ProjectorCount != 0
                || report.PlannedVisualRootCreateCount
                    != UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount
                || report.PlannedVisualRootReuseCount != 0
                || report.PlannedMoveCount != 58
                || report.DirectVfxSpawnPointCount != 8
                || report.NestedVfxSpawnPointCount != 8)
            {
                throw new InvalidOperationException(
                    "Apply requires exact B0 initial PASS: " +
                    "prefab50/root0/projector0/create50/reuse0/move58/directVfx8/nestedVfx8");
            }
        }

        private static bool IsCompletedState(UnitVisualRootAuditReport report)
        {
            return report.Passed
                && report.Prefabs.Count == UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount
                && report.ExistingVisualRootCount
                    == UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount
                && report.ProjectorCount
                    == UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount
                && report.ValidProjectorReferenceCount
                    == UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount
                && report.PlannedVisualRootCreateCount == 0
                && report.PlannedVisualRootReuseCount
                    == UnitVisualRootReadOnlyAnalyzer.ExpectedPrefabCount
                && report.PlannedMoveCount == 0
                && report.DirectVfxSpawnPointCount == 0
                && report.NestedVfxSpawnPointCount == 16;
        }

        private static void RequireCompletedState(UnitVisualRootAuditReport report)
        {
            if (!IsCompletedState(report))
            {
                throw new InvalidOperationException(
                    "post state must be exact: " +
                    "prefab50/root50/projector50/ref50/create0/reuse50/move0/directVfx0/nestedVfx16");
            }
        }

        private static void RequireEqual(
            string expected,
            string actual,
            string path,
            string field)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{path}: {field} changed; expected={expected}, actual={actual}");
            }
        }

        [Serializable]
        private sealed class MigrationJournal
        {
            public int Version;
            public bool Pending;
            public bool Completed;
            public bool Recovered;
            public string CreatedUtc;
            public string CompletedUtc;
            public List<JournalAsset> Assets = new List<JournalAsset>();
        }

        [Serializable]
        private sealed class JournalAsset
        {
            public string AssetPath;
            public string MetaPath;
            public string PrefabBackupFile;
            public string MetaBackupFile;
            public string PrefabSha256;
            public string MetaSha256;
            public string NetworkObjectGlobalObjectIdHash;
            public string NetworkObjectSerializedSha256;
            public string NetworkTransformSerializedSha256;
        }
    }
}
