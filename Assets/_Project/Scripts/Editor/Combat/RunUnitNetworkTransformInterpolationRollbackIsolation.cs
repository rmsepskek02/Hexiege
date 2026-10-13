using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// Apply 도중 25번째 변경 프리팹 직후 실패를 주입하고 50 prefab + 50 meta의
    /// 실행 전 SHA-256이 완전히 복구되는지 확인한다. 실제 규격 적용은 남기지 않는다.
    /// </summary>
    public static class RunUnitNetworkTransformInterpolationRollbackIsolation
    {
        [MenuItem(
            "Hexiege/Combat/Network Transform/" +
            "Test Rollback Isolation at Changed Prefab 25")]
        public static void Run()
        {
            try
            {
                IReadOnlyList<string> paths =
                    SetupUnitNetworkTransformInterpolation.GetUnitPrefabPathsForTests();
                if (paths.Count != 50)
                    throw new InvalidOperationException($"expected 50 prefabs, found {paths.Count}");
                int enabledCount =
                    SetupUnitNetworkTransformInterpolation.GetSmoothingEnabledCountForTests();
                if (enabledCount < 25)
                {
                    throw new InvalidOperationException(
                        "rollback isolation requires at least 25 pending prefab changes; " +
                        $"found {enabledCount}. No assets were modified.");
                }

                Dictionary<string, string> before = CaptureHashes(paths);
                SetupUnitNetworkTransformInterpolation.FailureInjectionForTests =
                    (index, path) => index == 24;
                SetupUnitNetworkTransformInterpolation.Apply();
                Dictionary<string, string> after = CaptureHashes(paths);

                foreach (KeyValuePair<string, string> item in before)
                {
                    if (!after.TryGetValue(item.Key, out string hash)
                        || !string.Equals(item.Value, hash, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{item.Key}: rollback isolation SHA-256 mismatch");
                    }
                }

                Debug.Log(
                    "[UAS-NETWORK-TRANSFORM][rollback-isolation][PASS] " +
                    "50 prefab + 50 meta exact-byte unchanged after injected failure.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                SetupUnitNetworkTransformInterpolation.FailureInjectionForTests = null;
            }
        }

        private static Dictionary<string, string> CaptureHashes(
            IReadOnlyList<string> prefabPaths)
        {
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string prefabPath in prefabPaths)
            {
                hashes.Add(prefabPath, ComputeFileHash(prefabPath));
                hashes.Add(prefabPath + ".meta", ComputeFileHash(prefabPath + ".meta"));
            }
            return hashes;
        }

        private static string ComputeFileHash(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(File.ReadAllBytes(Path.GetFullPath(path)));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}
