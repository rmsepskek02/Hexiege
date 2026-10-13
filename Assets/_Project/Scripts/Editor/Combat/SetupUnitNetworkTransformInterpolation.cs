using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// 50개 유닛 프리팹의 NGO 위치 보간 두 번째 smoothing pass를 원자적으로 비활성화한다.
    /// LegacyLerp와 Interpolate는 유지하며, 허용된 변경은 PositionLerpSmoothing 하나뿐이다.
    /// </summary>
    public static class SetupUnitNetworkTransformInterpolation
    {
        private const string MenuRoot = "Hexiege/Combat/Network Transform/";
        private const string UnitsPrefabFolder = "Assets/_Project/Prefabs/Units";
        private const string JournalDirectory =
            "Library/Hexiege/UnitNetworkTransformInterpolationB1";
        private const string JournalPath =
            JournalDirectory + "/journal.json";
        private const int JournalVersion = 1;
        private const int ExpectedPrefabCount = 50;

        // rollback isolation 메뉴만 사용하는 비영속 one-run hook이다.
        internal static Func<int, string, bool> FailureInjectionForTests;

        private static readonly string[] InvariantPropertyPaths =
        {
            "m_Enabled",
            "AutoOwnerAuthorityTickOffset",
            "PositionInterpolationType",
            "RotationInterpolationType",
            "ScaleInterpolationType",
            "PositionMaxInterpolationTime",
            "RotationLerpSmoothing",
            "RotationMaxInterpolationTime",
            "ScaleLerpSmoothing",
            "ScaleMaxInterpolationTime",
            "AuthorityMode",
            "TickSyncChildren",
            "UseUnreliableDeltas",
            "SyncPositionX",
            "SyncPositionY",
            "SyncPositionZ",
            "SyncRotAngleX",
            "SyncRotAngleY",
            "SyncRotAngleZ",
            "SyncScaleX",
            "SyncScaleY",
            "SyncScaleZ",
            "PositionThreshold",
            "RotAngleThreshold",
            "ScaleThreshold",
            "UseQuaternionSynchronization",
            "UseQuaternionCompression",
            "UseHalfFloatPrecision",
            "InLocalSpace",
            "SwitchTransformSpaceWhenParented",
            "Interpolate",
            "SlerpPosition"
        };

        [MenuItem(MenuRoot + "Dry Run Disable Position Lerp Smoothing")]
        public static void DryRun()
        {
            try
            {
                LogPlan("dry-run", InspectAll());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem(MenuRoot + "Apply Disable Position Lerp Smoothing")]
        public static void Apply()
        {
            try
            {
                if (!RecoverPendingJournalIfAny("apply-preflight"))
                    return;

                List<PrefabState> before = InspectAll();
                LogPlan("apply-preflight", before);
                List<PrefabState> changes = before
                    .Where(state => state.PositionLerpSmoothing)
                    .ToList();
                if (changes.Count == 0)
                {
                    Debug.Log(
                        "[UAS-NETWORK-TRANSFORM][apply][NO-OP] " +
                        "50개 프리팹이 이미 규격 상태입니다. 저장 호출 없이 종료합니다.");
                    return;
                }

                MigrationJournal journal = CreatePendingJournal(before);
                try
                {
                    for (int index = 0; index < changes.Count; index++)
                    {
                        ApplyOne(changes[index].Path);
                        if (FailureInjectionForTests != null
                            && FailureInjectionForTests(index, changes[index].Path))
                        {
                            throw new InvalidOperationException(
                                $"test failure injected after changed prefab {index + 1}");
                        }
                    }

                    List<PrefabState> after = InspectAll();
                    VerifyPostState(journal, after);
                    VerifyMetaFiles(journal);

                    journal.Pending = false;
                    journal.Completed = true;
                    journal.Recovered = false;
                    WriteJournal(journal);
                    Debug.Log(
                        "[UAS-NETWORK-TRANSFORM][apply][PASS] " +
                        $"prefabs=50, modified={changes.Count}, unchanged={50 - changes.Count}, " +
                        "prefab+meta rollback baseline and non-target config invariants verified.");
                }
                catch (Exception applyException)
                {
                    try
                    {
                        RestoreExactBytes(journal, "apply-failure");
                    }
                    catch (Exception recoveryException)
                    {
                        throw new AggregateException(
                            "Apply와 exact-byte recovery가 모두 실패했습니다. " +
                            "pending journal을 보존합니다.",
                            applyException,
                            recoveryException);
                    }

                    throw new InvalidOperationException(
                        "Apply가 실패하여 50개 prefab+.meta를 실행 전 bytes로 복구했습니다.",
                        applyException);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                FailureInjectionForTests = null;
            }
        }

        [MenuItem(MenuRoot + "Recover Pending Position Lerp Smoothing Apply")]
        public static void Recover()
        {
            try
            {
                if (!TryLoadJournal(out MigrationJournal journal) || !journal.Pending)
                {
                    Debug.Log("[UAS-NETWORK-TRANSFORM][recover][NO-OP] pending journal이 없습니다.");
                    return;
                }

                RestoreExactBytes(journal, "manual-recover");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static IReadOnlyList<string> GetUnitPrefabPathsForTests()
        {
            return FindPrefabPaths();
        }

        internal static int GetSmoothingEnabledCountForTests()
        {
            return InspectAll().Count(state => state.PositionLerpSmoothing);
        }

        private static bool RecoverPendingJournalIfAny(string reason)
        {
            if (!TryLoadJournal(out MigrationJournal journal) || !journal.Pending)
                return true;

            Debug.LogWarning(
                "[UAS-NETWORK-TRANSFORM][recover][BEGIN] " +
                "unfinished journal을 발견해 Apply보다 먼저 복구합니다.");
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

        private static List<PrefabState> InspectAll()
        {
            List<string> paths = FindPrefabPaths();
            if (paths.Count != ExpectedPrefabCount)
            {
                throw new InvalidOperationException(
                    $"expected {ExpectedPrefabCount} unit prefabs, found {paths.Count}");
            }

            return paths.Select(InspectOne).ToList();
        }

        private static List<string> FindPrefabPaths()
        {
            return AssetDatabase
                .FindAssets("t:Prefab", new[] { UnitsPrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static PrefabState InspectOne(string path)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                NetworkTransform networkTransform = GetCanonicalNetworkTransform(path, root);
                return new PrefabState
                {
                    Path = path,
                    PositionLerpSmoothing = networkTransform.PositionLerpSmoothing,
                    InvariantSha256 = ComputeInvariantHash(networkTransform)
                };
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static NetworkTransform GetCanonicalNetworkTransform(
            string path,
            GameObject root)
        {
            if (root == null)
                throw new InvalidOperationException($"{path}: failed to load prefab contents");

            NetworkTransform[] values = root.GetComponentsInChildren<NetworkTransform>(true);
            if (values.Length != 1 || values[0].gameObject != root)
            {
                throw new InvalidOperationException(
                    $"{path}: expected exactly one NetworkTransform on Simulation Root, " +
                    $"found {values.Length}");
            }

            return values[0];
        }

        private static void ApplyOne(string path)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                NetworkTransform networkTransform = GetCanonicalNetworkTransform(path, root);
                if (!networkTransform.PositionLerpSmoothing)
                    return;

                networkTransform.PositionLerpSmoothing = false;
                EditorUtility.SetDirty(networkTransform);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || saved == null)
                    throw new InvalidOperationException($"{path}: SaveAsPrefabAsset failed");
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static MigrationJournal CreatePendingJournal(
            IReadOnlyList<PrefabState> before)
        {
            if (Directory.Exists(JournalDirectory))
                Directory.Delete(JournalDirectory, recursive: true);
            Directory.CreateDirectory(JournalDirectory);

            var journal = new MigrationJournal
            {
                Version = JournalVersion,
                Pending = true,
                Completed = false,
                Recovered = false
            };

            for (int index = 0; index < before.Count; index++)
            {
                PrefabState state = before[index];
                string metaPath = state.Path + ".meta";
                byte[] prefabBytes = ReadRequiredBytes(state.Path);
                byte[] metaBytes = ReadRequiredBytes(metaPath);
                string prefabBackup = $"{index:D2}.prefab.bytes";
                string metaBackup = $"{index:D2}.meta.bytes";
                File.WriteAllBytes(Path.Combine(JournalDirectory, prefabBackup), prefabBytes);
                File.WriteAllBytes(Path.Combine(JournalDirectory, metaBackup), metaBytes);
                journal.Items.Add(new JournalItem
                {
                    AssetPath = state.Path,
                    PrefabBackup = prefabBackup,
                    MetaBackup = metaBackup,
                    PrefabSha256 = ComputeSha256(prefabBytes),
                    MetaSha256 = ComputeSha256(metaBytes),
                    InvariantSha256 = state.InvariantSha256
                });
            }

            WriteJournal(journal);
            return journal;
        }

        private static void VerifyPostState(
            MigrationJournal journal,
            IReadOnlyList<PrefabState> after)
        {
            var byPath = after.ToDictionary(value => value.Path, StringComparer.Ordinal);
            foreach (JournalItem item in journal.Items)
            {
                if (!byPath.TryGetValue(item.AssetPath, out PrefabState state))
                    throw new InvalidOperationException($"{item.AssetPath}: missing after Apply");
                if (state.PositionLerpSmoothing)
                    throw new InvalidOperationException($"{item.AssetPath}: smoothing remains enabled");
                if (!string.Equals(
                    state.InvariantSha256,
                    item.InvariantSha256,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{item.AssetPath}: non-target NetworkTransform config changed");
                }
            }
        }

        private static void VerifyMetaFiles(MigrationJournal journal)
        {
            foreach (JournalItem item in journal.Items)
            {
                string current = ComputeSha256(ReadRequiredBytes(item.AssetPath + ".meta"));
                if (!string.Equals(current, item.MetaSha256, StringComparison.Ordinal))
                    throw new InvalidOperationException($"{item.AssetPath}.meta changed during Apply");
            }
        }

        private static void RestoreExactBytes(MigrationJournal journal, string reason)
        {
            foreach (JournalItem item in journal.Items)
            {
                byte[] prefab = ReadVerifiedBackup(item.PrefabBackup, item.PrefabSha256);
                byte[] meta = ReadVerifiedBackup(item.MetaBackup, item.MetaSha256);
                File.WriteAllBytes(Path.GetFullPath(item.AssetPath), prefab);
                File.WriteAllBytes(Path.GetFullPath(item.AssetPath + ".meta"), meta);
            }

            foreach (JournalItem item in journal.Items)
            {
                AssetDatabase.ImportAsset(
                    item.AssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                RequireFileHash(item.AssetPath, item.PrefabSha256);
                RequireFileHash(item.AssetPath + ".meta", item.MetaSha256);
            }

            journal.Pending = false;
            journal.Completed = false;
            journal.Recovered = true;
            WriteJournal(journal);
            Debug.Log(
                $"[UAS-NETWORK-TRANSFORM][recover][PASS] reason={reason}, " +
                "50 prefab + 50 meta exact-byte restored.");
        }

        private static byte[] ReadVerifiedBackup(string fileName, string expectedHash)
        {
            string path = Path.Combine(JournalDirectory, fileName);
            byte[] bytes = File.ReadAllBytes(path);
            if (!string.Equals(
                ComputeSha256(bytes),
                expectedHash,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{path}: backup SHA-256 mismatch");
            }
            return bytes;
        }

        private static void RequireFileHash(string path, string expected)
        {
            string actual = ComputeSha256(ReadRequiredBytes(path));
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"{path}: restored SHA-256 mismatch");
        }

        private static string ComputeInvariantHash(NetworkTransform networkTransform)
        {
            var serializedObject = new SerializedObject(networkTransform);
            serializedObject.UpdateIfRequiredOrScript();
            var canonical = new StringBuilder();
            foreach (string path in InvariantPropertyPaths)
            {
                SerializedProperty property = serializedObject.FindProperty(path);
                if (property == null)
                    throw new InvalidOperationException(
                        $"{networkTransform.name}: missing serialized property '{path}'");
                canonical.Append(path)
                    .Append('=')
                    .Append(ReadCanonicalValue(property))
                    .Append('\n');
            }
            return ComputeSha256(Encoding.UTF8.GetBytes(canonical.ToString()));
        }

        private static string ReadCanonicalValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "true" : "false";
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Float:
                    return property.doubleValue.ToString("R", CultureInfo.InvariantCulture);
                default:
                    throw new InvalidOperationException(
                        $"{property.propertyPath}: unsupported invariant property type " +
                        property.propertyType);
            }
        }

        private static byte[] ReadRequiredBytes(string path)
        {
            string absolutePath = Path.GetFullPath(path);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("Required migration file is missing.", path);
            return File.ReadAllBytes(absolutePath);
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

        private static bool TryLoadJournal(out MigrationJournal journal)
        {
            journal = null;
            if (!File.Exists(JournalPath))
                return false;
            journal = JsonUtility.FromJson<MigrationJournal>(File.ReadAllText(JournalPath));
            if (journal == null || journal.Version != JournalVersion)
                throw new InvalidOperationException("invalid interpolation migration journal");
            return true;
        }

        private static void WriteJournal(MigrationJournal journal)
        {
            Directory.CreateDirectory(JournalDirectory);
            string temporaryPath = JournalPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(journal, prettyPrint: true));
            File.Copy(temporaryPath, JournalPath, overwrite: true);
            File.Delete(temporaryPath);
        }

        private static void LogPlan(string phase, IReadOnlyList<PrefabState> states)
        {
            int changes = states.Count(state => state.PositionLerpSmoothing);
            Debug.Log(
                $"[UAS-NETWORK-TRANSFORM][{phase}] prefabs={states.Count}, " +
                $"wouldModify={changes}, alreadyCompliant={states.Count - changes}, assetsModified=0");
            foreach (PrefabState state in states)
            {
                Debug.Log(
                    $"[UAS-NETWORK-TRANSFORM][{phase}][PREFAB]" +
                    $"[{(state.PositionLerpSmoothing ? "CHANGE" : "NO-OP")}] {state.Path}");
            }
        }

        private sealed class PrefabState
        {
            public string Path;
            public bool PositionLerpSmoothing;
            public string InvariantSha256;
        }

        [Serializable]
        private sealed class MigrationJournal
        {
            public int Version;
            public bool Pending;
            public bool Completed;
            public bool Recovered;
            public List<JournalItem> Items = new List<JournalItem>();
        }

        [Serializable]
        private sealed class JournalItem
        {
            public string AssetPath;
            public string PrefabBackup;
            public string MetaBackup;
            public string PrefabSha256;
            public string MetaSha256;
            public string InvariantSha256;
        }
    }
}
