using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Hexiege.Application;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Hexiege.EditorTools
{
    // 고정 타일 하나만 변환한다. 원본 편집 데이터는 private backup에 남기고 씬은 저장하지 않는다.
    internal static class BakeHexTileRuntimeMesh
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Tiles/HexTile.prefab";
        private const string MeshPath = "Assets/_Project/Prefabs/Tiles/HexTile_RuntimeMesh.asset";
        private const string ExpectedGuid = "ebdae33e1133e744697ea79add92517e";
        private static Type ShapeType => typeof(ProBuilderMesh).Assembly.GetType("UnityEngine.ProBuilder.Shapes.ProBuilderShape", true);
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }

        [MenuItem("Hexiege/Combat/Diagnostics/Bake Fixed HexTile Runtime Mesh")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
                || UnityEditorInternal.ProfilerDriver.enabled || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null
                || NetworkContext.IsNetworkActive || GameServicesLocator.Current != null
                || (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening))
            { Debug.LogWarning("HexTile bake requires stopped Play/record/network/services."); return; }
            string run = Guid.NewGuid().ToString("N");
            string backup = Path.Combine(".utmp", "hex-tile-bake-20261029", run);
            GameObject contents = null;
            Mesh reference = null;
            Mesh generated = null;
            Mesh preparedAsset = null;
            bool meshCreated = false, prefabWriteAttempted = false, passed = false, restored = false;
            byte[] original = null, originalMeta = null;
            Debug.Log("[HEX-TILE-BAKE] BEGIN Run=" + run);
            try
            {
                Require(AssetDatabase.AssetPathToGUID(PrefabPath) == ExpectedGuid, "Unexpected target GUID.");
                Require(!File.Exists(MeshPath) && !File.Exists(MeshPath + ".meta"), "Mesh destination already exists; refusing overwrite.");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Require(source != null && source.GetComponent<ProBuilderMesh>() != null, "Expected original ProBuilder tile.");
                Require(source.transform.childCount == 0, "Unexpected tile hierarchy.");
                var localIds = new Dictionary<Type, long>();
                foreach (var component in source.GetComponents<Component>())
                    if (!(component is ProBuilderMesh) && component.GetType() != ShapeType)
                    {
                        Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out string componentGuid, out long localId)
                            && componentGuid == ExpectedGuid, "Cannot capture original component identity.");
                        localIds.Add(component.GetType(), localId);
                    }
                original = File.ReadAllBytes(PrefabPath); originalMeta = File.ReadAllBytes(PrefabPath + ".meta");
                Directory.CreateDirectory(backup);
                File.WriteAllBytes(Path.Combine(backup, "HexTile.prefab"), original);
                File.WriteAllBytes(Path.Combine(backup, "HexTile.prefab.meta"), originalMeta);
                File.WriteAllText(Path.Combine(backup, "receipt.txt"), "Run=" + run + "\nPrefabSHA256=" + Hash(original)
                    + "\nMetaSHA256=" + Hash(originalMeta) + "\nTarget=" + PrefabPath + "\nMesh=" + MeshPath);
                Require(Hash(File.ReadAllBytes(Path.Combine(backup, "HexTile.prefab"))) == Hash(original), "Backup verification failed.");

                contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                var pb = contents.GetComponent<ProBuilderMesh>();
                var shape = contents.GetComponent(ShapeType);
                var filter = contents.GetComponent<MeshFilter>();
                var collider = contents.GetComponent<MeshCollider>();
                var renderer = contents.GetComponent<MeshRenderer>();
                Require(pb != null && shape != null && filter != null && collider != null && renderer != null, "Unexpected components.");
                Require(contents.GetComponents<Component>().Length == 7, "Unexpected custom component count.");
                string transformJson = EditorJsonUtility.ToJson(contents.transform);
                string rendererJson = EditorJsonUtility.ToJson(renderer);
                string colliderSettings = ColliderSettings(collider);
                var materials = renderer.sharedMaterials;
                Require(materials.Length == 2 && materials[0] != null && materials[1] != null, "Expected two materials.");

                // 런타임 Awake가 meshSyncState.Null에서 호출하는 동일 메서드(인자 없음)를 사용한다.
                var rebuild = typeof(ProBuilderMesh).GetMethod("Rebuild", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(rebuild != null, "ProBuilder Rebuild API changed.");
                rebuild.Invoke(pb, null);
                Require(filter.sharedMesh != null && collider.sharedMesh == filter.sharedMesh, "Rebuild mesh/collider mismatch.");
                generated = filter.sharedMesh;
                Require(!AssetDatabase.Contains(generated), "Expected an isolated temporary generated mesh.");
                reference = UnityEngine.Object.Instantiate(filter.sharedMesh);
                reference.hideFlags = HideFlags.HideAndDontSave;
                Require(reference.subMeshCount == 2, "Expected two submeshes.");
                var baked = UnityEngine.Object.Instantiate(reference);
                preparedAsset = baked;
                baked.name = "HexTile_RuntimeMesh"; baked.hideFlags = HideFlags.None;
                meshCreated = true;
                AssetDatabase.CreateAsset(baked, MeshPath);
                AssetDatabase.SaveAssetIfDirty(baked);
                AssetDatabase.ImportAsset(MeshPath, ImportAssetOptions.ForceSynchronousImport);
                baked = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                SameMesh(reference, baked);
                filter.sharedMesh = baked; collider.sharedMesh = baked;
                pb.preserveMeshAssetOnDestroy = true;
                Require(pb.preserveMeshAssetOnDestroy, "Mesh preservation flag did not stick.");
                // shape를 먼저 제거해 이후 shape.mesh getter가 PB를 다시 찾거나 추가하지 않게 한다.
                // preserve 플래그로 PB OnDestroy가 baked mesh를 지우지 않도록 한다.
                UnityEngine.Object.DestroyImmediate(shape);
                UnityEngine.Object.DestroyImmediate(pb);
                filter.hideFlags = source.GetComponent<MeshFilter>().hideFlags;
                Require(filter.sharedMesh == baked && collider.sharedMesh == baked, "Strip destroyed/replaced baked mesh.");
                Require(EditorJsonUtility.ToJson(contents.transform) == transformJson, "Transform changed.");
                Require(EditorJsonUtility.ToJson(renderer) == rendererJson, "Renderer settings/material references changed.");
                Require(ColliderSettings(collider) == colliderSettings, "Collider settings changed.");
                Require(Hash(File.ReadAllBytes(PrefabPath)) == Hash(original)
                    && Hash(File.ReadAllBytes(PrefabPath + ".meta")) == Hash(originalMeta), "Target changed after backup; refusing overwrite.");
                prefabWriteAttempted = true;
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out bool saved);
                Require(saved, "Prefab save failed.");
                PrefabUtility.UnloadPrefabContents(contents); contents = null;
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
                Require(Hash(File.ReadAllBytes(PrefabPath + ".meta")) == Hash(originalMeta), "Prefab GUID/meta changed.");
                ValidateSaved(reference, materials, colliderSettings, localIds);
                string receipt = Path.Combine(backup, "receipt.txt");
                string completeReceipt = File.ReadAllText(receipt) + "\nNewPrefabSHA256=" + Hash(File.ReadAllBytes(PrefabPath))
                    + "\nMeshSHA256=" + Hash(File.ReadAllBytes(MeshPath)) + "\nVertices=" + reference.vertexCount
                    + "\nSubMeshes=" + reference.subMeshCount + "\nColliderRays=31\nEND Passed=True\n";
                RestoreAtomically(receipt, System.Text.Encoding.UTF8.GetBytes(completeReceipt), Path.Combine(backup, "complete-receipt.tmp"));
                passed = true;
            }
            catch (Exception ex)
            {
                passed = false;
                // 새로 만든 지정 mesh만 정리한다. 기존 다른 파일은 절대 삭제하거나 복원하지 않는다.
                try
                {
                    if (contents != null) { PrefabUtility.UnloadPrefabContents(contents); contents = null; }
                    if (prefabWriteAttempted && original != null)
                    {
                        RestoreAtomically(PrefabPath, original, Path.Combine(backup, "restore-prefab.tmp"));
                        RestoreAtomically(PrefabPath + ".meta", originalMeta, Path.Combine(backup, "restore-meta.tmp"));
                        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
                        Require(Hash(File.ReadAllBytes(PrefabPath)) == Hash(original)
                            && Hash(File.ReadAllBytes(PrefabPath + ".meta")) == Hash(originalMeta), "Rollback hash mismatch.");
                    }
                    if (meshCreated && (File.Exists(MeshPath) || File.Exists(MeshPath + ".meta")))
                        Require(AssetDatabase.DeleteAsset(MeshPath), "Could not remove this run's new mesh.");
                    restored = true;
                }
                catch (Exception rollback) { Debug.LogError("[HEX-TILE-BAKE] ROLLBACK-FAILED " + rollback); }
                if (Directory.Exists(backup))
                    try { File.AppendAllText(Path.Combine(backup, "receipt.txt"), "\nEND Passed=False Restored=" + restored + "\n"); }
                    catch (Exception receiptError) { Debug.LogError("[HEX-TILE-BAKE] Failure receipt could not be saved: " + receiptError.GetType().Name); }
                Debug.LogError("[HEX-TILE-BAKE] FAILED Run=" + run + " Restored=" + restored + " Backup=" + backup + " " + ex);
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
                if (generated != null && !AssetDatabase.Contains(generated)) UnityEngine.Object.DestroyImmediate(generated);
                if (preparedAsset != null && !AssetDatabase.Contains(preparedAsset)) UnityEngine.Object.DestroyImmediate(preparedAsset);
                Debug.Log("[HEX-TILE-BAKE] END Run=" + run + " Passed=" + passed + " Restored=" + restored + " Backup=" + backup);
            }
        }

        private static void RestoreAtomically(string target, byte[] bytes, string staging)
        {
            File.WriteAllBytes(staging, bytes);
            Require(Hash(File.ReadAllBytes(staging)) == Hash(bytes), "Rollback staging hash mismatch.");
            if (File.Exists(target)) File.Replace(staging, target, null);
            else File.Move(staging, target);
        }

        private static string ColliderSettings(MeshCollider c) => c.enabled + "|" + c.isTrigger + "|" + c.convex + "|"
            + c.cookingOptions + "|" + (c.sharedMaterial == null ? 0 : c.sharedMaterial.GetInstanceID()) + "|"
            + c.includeLayers.value + "|" + c.excludeLayers.value + "|" + c.layerOverridePriority + "|" + c.providesContacts;

        private static void Same<T>(T[] a, T[] b, string field)
        {
            Require(a.Length == b.Length, field + " length differs.");
            for (int i = 0; i < a.Length; i++) Require(EqualityComparer<T>.Default.Equals(a[i], b[i]), field + " differs at " + i);
        }
        private static void SameMesh(Mesh a, Mesh b)
        {
            Require(a != null && b != null && a.indexFormat == b.indexFormat && a.bounds.Equals(b.bounds)
                && a.subMeshCount == b.subMeshCount, "Mesh header differs.");
            Same(a.vertices, b.vertices, "vertices"); Same(a.normals, b.normals, "normals");
            Same(a.tangents, b.tangents, "tangents"); Same(a.colors, b.colors, "colors");
            Same(a.GetVertexAttributes(), b.GetVertexAttributes(), "vertex attribute formats/dimensions");
            for (int channel = 0; channel < 8; channel++)
            {
                var x = new List<Vector4>(); var y = new List<Vector4>();
                a.GetUVs(channel, x); b.GetUVs(channel, y); Same(x.ToArray(), y.ToArray(), "UV" + channel);
            }
            for (int s = 0; s < a.subMeshCount; s++)
            {
                Require(a.GetTopology(s) == b.GetTopology(s) && a.GetSubMesh(s).Equals(b.GetSubMesh(s)), "Submesh descriptor differs.");
                Same(a.GetIndices(s), b.GetIndices(s), "indices" + s);
            }
        }
        private static void ValidateSaved(Mesh reference, Material[] materials, string colliderSettings, Dictionary<Type, long> localIds)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab.GetComponent<ProBuilderMesh>() == null
                && prefab.GetComponent(ShapeType) == null, "ProBuilder remains.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            Require(prefab.GetComponent<MeshFilter>().sharedMesh == mesh && prefab.GetComponent<MeshCollider>().sharedMesh == mesh, "Saved mesh references differ.");
            Same(materials, prefab.GetComponent<MeshRenderer>().sharedMaterials, "materials");
            Require(ColliderSettings(prefab.GetComponent<MeshCollider>()) == colliderSettings, "Saved collider differs.");
            Require(prefab.GetComponents<Component>().Length == localIds.Count, "Unexpected saved component count.");
            foreach (var identity in localIds)
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.GetComponent(identity.Key), out string guid, out long id)
                    && guid == ExpectedGuid && id == identity.Value, "Saved component identity changed: " + identity.Key.Name);
            SameMesh(reference, mesh);
            GameObject first = null, second = null;
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                first = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                second = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                Require(first.GetComponent<MeshFilter>().sharedMesh == mesh && second.GetComponent<MeshFilter>().sharedMesh == mesh
                    && first.GetComponent<MeshCollider>().sharedMesh == mesh && second.GetComponent<MeshCollider>().sharedMesh == mesh, "Clones did not retain shared baked mesh.");
                SameMesh(reference, first.GetComponent<MeshFilter>().sharedMesh);
                // 게임의 Instantiate는 root 회전을 identity로 덮는다. 정점에 prefab의 30도를 굽지 않는다.
                first.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                second.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                first.GetComponent<MeshFilter>().sharedMesh = reference;
                first.GetComponent<MeshCollider>().sharedMesh = reference;
                Physics.SyncTransforms();
                var oldCollider = first.GetComponent<MeshCollider>();
                var newCollider = second.GetComponent<MeshCollider>();
                Bounds bounds = oldCollider.bounds;
                int hits = 0;
                for (int x = -2; x <= 2; x++)
                    for (int z = -2; z <= 2; z++)
                    {
                        var origin = new Vector3(bounds.center.x + bounds.extents.x * x * .4f,
                            bounds.max.y + 2f, bounds.center.z + bounds.extents.z * z * .4f);
                        if (SameRay(oldCollider, newCollider, new Ray(origin, Vector3.down))) hits++;
                    }
                foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                    if (SameRay(oldCollider, newCollider, new Ray(bounds.center - direction * (bounds.extents.magnitude + 2f), direction))) hits++;
                Require(hits > 0, "Collider probes produced no hits.");
            }
            finally
            {
                if (first != null) UnityEngine.Object.DestroyImmediate(first);
                if (second != null) UnityEngine.Object.DestroyImmediate(second);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static bool SameRay(MeshCollider a, MeshCollider b, Ray ray)
        {
            bool oldHit = a.Raycast(ray, out RaycastHit oldInfo, 100f);
            bool newHit = b.Raycast(ray, out RaycastHit newInfo, 100f);
            Require(oldHit == newHit, "Collider probe hit/miss differs.");
            if (oldHit)
                Require(Mathf.Abs(oldInfo.distance - newInfo.distance) <= .00001f
                    && (oldInfo.point - newInfo.point).sqrMagnitude <= 1e-10f
                    && (oldInfo.normal - newInfo.normal).sqrMagnitude <= 1e-10f
                    && oldInfo.triangleIndex == newInfo.triangleIndex, "Collider probe surface differs.");
            return oldHit;
        }
    }
}
