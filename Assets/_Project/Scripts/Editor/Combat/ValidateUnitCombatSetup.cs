using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using Hexiege.Presentation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// Tracer B0의 유닛 프리팹 구조를 읽기 전용으로 검사한다.
    ///
    /// 이 파일은 B1 migration이 안전하게 실행될 수 있는지를 먼저 증명하는 도구다.
    /// 프리팹을 임시 메모리에 로드하지만 Transform을 옮기거나 에셋을 저장하지 않는다.
    /// 모든 임시 프리팹은 finally에서 반드시 해제하므로 메뉴를 반복 실행해도 씬과 에셋이 변하지 않는다.
    /// </summary>
    public static class ValidateUnitCombatSetup
    {
        [MenuItem("Hexiege/Combat/Visual Root/Validate Unit Combat Setup")]
        public static void Validate()
        {
            UnitVisualRootAuditReport report = UnitVisualRootReadOnlyAnalyzer.AnalyzeAll();
            report.Log("validate", includeMigrationPlan: false);
        }
    }

    /// <summary>
    /// validator와 dry-run 메뉴가 공유하는 전체 감사 결과다.
    /// Editor 전용 internal 타입이므로 runtime assembly나 게임 규칙 타입에 새 의존성을 만들지 않는다.
    /// </summary>
    internal sealed class UnitVisualRootAuditReport
    {
        public readonly List<UnitVisualRootPrefabAudit> Prefabs = new List<UnitVisualRootPrefabAudit>();
        public readonly List<string> Errors = new List<string>();

        public int HumanCount;
        public int SpiritCount;
        public int TranscendenceCount;
        public int ExistingVisualRootCount;
        public int PlannedVisualRootCreateCount;
        public int PlannedVisualRootReuseCount;
        public int PlannedMoveCount;
        public int AssignedVfxReferenceCount;
        public int NullVfxReferenceCount;
        public int DirectVfxSpawnPointCount;
        public int NestedVfxSpawnPointCount;
        public int ProjectorCount;
        public int ValidProjectorReferenceCount;
        public int AnimatorApplyRootMotionTrueCount;

        public bool Passed => Errors.Count == 0;

        public void Log(string menuLabel, bool includeMigrationPlan)
        {
            string runId = Guid.NewGuid().ToString("N");
            string prefix = $"[UAS-ROOT][run={runId}][menu={menuLabel}]";
            var summary = new StringBuilder();
            summary.AppendLine($"{prefix}[BEGIN]");
            summary.AppendLine(
                $"prefabs={Prefabs.Count} (Human={HumanCount}, Spirit={SpiritCount}, " +
                $"Transcendence={TranscendenceCount}), errors={Errors.Count}");
            summary.AppendLine(
                $"visualRootExisting={ExistingVisualRootCount}, " +
                $"projector={ProjectorCount}, projectorRefValid={ValidProjectorReferenceCount}, " +
                $"animatorApplyRootMotionTrue={AnimatorApplyRootMotionTrueCount}, " +
                $"vfxAssigned={AssignedVfxReferenceCount}, vfxNullFallback={NullVfxReferenceCount}, " +
                $"vfxDirect={DirectVfxSpawnPointCount}, vfxNested={NestedVfxSpawnPointCount}");

            if (includeMigrationPlan)
            {
                summary.AppendLine(
                    $"dryRunCreateVisualRoot={PlannedVisualRootCreateCount}, " +
                    $"dryRunReuseVisualRoot={PlannedVisualRootReuseCount}, " +
                    $"dryRunMoveDirectChildren={PlannedMoveCount}, assetsModified=0");
            }
            Debug.Log(summary.ToString());

            for (int index = 0; index < Prefabs.Count; index++)
            {
                UnitVisualRootPrefabAudit prefab = Prefabs[index];
                string message =
                    $"{prefix}[PREFAB {index + 1:D2}/{Prefabs.Count:D2}]" +
                    $"[{(prefab.Passed ? "OK" : "FAIL")}] {prefab.Path} | " +
                    $"identity={prefab.IdentityKey}, " +
                    $"visualRoot={prefab.VisualRootState}, create={prefab.WouldCreateVisualRoot}, " +
                    $"reuse={prefab.WouldReuseVisualRoot}, move={prefab.MoveCandidateCount}, " +
                    $"animator={prefab.AnimatorCount}, renderer={prefab.RendererCount}, " +
                    $"relay={prefab.RelayCount}, vfx={prefab.VfxReferenceState}\n" +
                    $"rollbackManifest={prefab.RollbackManifest}";
                if (prefab.Passed)
                    Debug.Log(message);
                else
                    Debug.LogError(message);
            }

            for (int index = 0; index < Errors.Count; index++)
            {
                Debug.LogError(
                    $"{prefix}[ERROR {index + 1:D2}/{Errors.Count:D2}] {Errors[index]}");
            }

            string aggregateDigest = ComputeAggregateDigest();
            string footer =
                $"{prefix}[END][{(Passed ? "PASS" : "FAIL")}] " +
                $"prefabsLogged={Prefabs.Count}/{Prefabs.Count}, errorsLogged={Errors.Count}, " +
                $"aggregateManifestSha256={aggregateDigest}, assetsModified=0";
            if (Passed)
                Debug.Log(footer);
            else
                Debug.LogError(footer);
        }

        private string ComputeAggregateDigest()
        {
            string canonical = string.Join(
                "\n",
                Prefabs
                    .OrderBy(prefab => prefab.Path, StringComparer.Ordinal)
                    .Select(prefab => $"{prefab.Path}|{prefab.RollbackManifest ?? "<null>"}"));
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }

    internal sealed class UnitVisualRootPrefabAudit
    {
        public string Path;
        public string IdentityKey;
        public string VisualRootState;
        public string VfxReferenceState;
        public string RollbackManifest;
        public int MoveCandidateCount;
        public int AnimatorCount;
        public int RendererCount;
        public int RelayCount;
        public bool WouldCreateVisualRoot;
        public bool WouldReuseVisualRoot;
        public bool Passed;

        // B1 migration이 문자열 rollback manifest를 다시 파싱하지 않고 비교할 수 있는
        // 구조화된 네트워크 identity/config baseline이다.
        public string NetworkObjectGlobalObjectIdHash;
        public string NetworkObjectSerializedSha256;
        public string NetworkTransformSerializedSha256;
    }

    /// <summary>
    /// 50개 유닛 프리팹을 같은 규칙으로 분석하는 단일 read-only 구현이다.
    /// B0에서는 SetParent, SaveAsPrefabAsset, SaveAssets, SetDirty, Undo 같은 mutation API를 호출하지 않는다.
    /// </summary>
    internal static class UnitVisualRootReadOnlyAnalyzer
    {
        private const string UnitsPrefabFolder = "Assets/_Project/Prefabs/Units";
        private const string VisualRootName = "VisualRoot";
        private const string VfxSpawnPointName = "VfxSpawnPoint";
        internal const int ExpectedPrefabCount = 50;
        private const int ExpectedHumanCount = 16;
        private const int ExpectedSpiritCount = 18;
        private const int ExpectedTranscendenceCount = 16;
        private const int ExpectedTotalVfxSpawnPointCount = 16;
        private const int ExpectedAssignedVfxReferenceCount = 16;
        private const int ExpectedNullVfxReferenceCount = 34;
        private const int ExpectedInitialMoveCount = 58;

        // NGO 2.9.2의 실제 직렬화 네트워크 상태/동작 설정만 해시한다. Unity native bookkeeping
        // (m_GameObject, prefab instance/source pointers 등)과 inspector foldout 상태,
        // 비직렬화 런타임 캐시를 제외하여 LoadPrefabContents 재로드 사이에도 동일한 값을 보장한다.
        // 패키지에서 설정 필드가 이름 변경/삭제되면 FindProperty 실패로 fail-closed 된다.
        private static readonly string[] NetworkObjectConfigPropertyPaths =
        {
            "m_Enabled",
            "InScenePlacedSourceGlobalObjectIdHash",
            "DeferredDespawnTick",
            "Ownership",
            "AlwaysReplicateAsRoot",
            "SynchronizeTransform",
            "ActiveSceneSynchronization",
            "SceneMigrationSynchronization",
            "SpawnWithObservers",
            "DontDestroyWithOwner",
            "AutoObjectParentSync",
            "SyncOwnerTransformWhenParented",
            "AllowOwnerToParent"
        };

        private static readonly string[] NetworkTransformConfigPropertyPaths =
        {
            "m_Enabled",
            "AutoOwnerAuthorityTickOffset",
            "PositionInterpolationType",
            "RotationInterpolationType",
            "ScaleInterpolationType",
            "PositionLerpSmoothing",
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

        public static UnitVisualRootAuditReport AnalyzeAll()
        {
            var report = new UnitVisualRootAuditReport();
            List<string> paths = FindUnitPrefabPaths();
            var identities = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string path in paths)
            {
                int prefabErrorsBefore = report.Errors.Count;
                CountRace(path, report);
                if (TryParseIdentity(path, out UnitType unitType, out string team))
                {
                    string key = $"{unitType}|{team}";
                    if (identities.TryGetValue(key, out string duplicatePath))
                    {
                        report.Errors.Add(
                            $"duplicate unit identity {key}: '{duplicatePath}' and '{path}'");
                    }
                    else
                    {
                        identities.Add(key, path);
                    }
                    AnalyzePrefab(path, key, prefabErrorsBefore, report);
                }
                else
                {
                    report.Errors.Add(
                        $"{path}: filename must be Unit_<UnitType>_<Blue|Red>");
                    AnalyzePrefab(path, "invalid", prefabErrorsBefore, report);
                }
            }

            UnitType[] unitTypes = Enum.GetValues(typeof(UnitType))
                .Cast<UnitType>()
                .OrderBy(value => (int)value)
                .ToArray();
            Require(
                report, unitTypes.Length == 25,
                $"UnitType enum must contain exactly 25 values, found {unitTypes.Length}");
            foreach (UnitType unitType in unitTypes)
            {
                Require(
                    report, identities.ContainsKey($"{unitType}|Blue"),
                    $"missing prefab identity {unitType}|Blue");
                Require(
                    report, identities.ContainsKey($"{unitType}|Red"),
                    $"missing prefab identity {unitType}|Red");
            }
            Require(
                report, identities.Count == 50,
                $"expected 50 unique UnitType/team identities, found {identities.Count}");

            Require(
                report, paths.Count == ExpectedPrefabCount,
                $"expected exactly {ExpectedPrefabCount} unit prefabs, found {paths.Count}");
            Require(
                report, report.HumanCount == ExpectedHumanCount,
                $"expected Human={ExpectedHumanCount}, found {report.HumanCount}");
            Require(
                report, report.SpiritCount == ExpectedSpiritCount,
                $"expected Spirit={ExpectedSpiritCount}, found {report.SpiritCount}");
            Require(
                report, report.TranscendenceCount == ExpectedTranscendenceCount,
                $"expected Transcendence={ExpectedTranscendenceCount}, found {report.TranscendenceCount}");
            Require(
                report,
                report.DirectVfxSpawnPointCount + report.NestedVfxSpawnPointCount
                == ExpectedTotalVfxSpawnPointCount,
                $"expected total {VfxSpawnPointName}={ExpectedTotalVfxSpawnPointCount}, found " +
                $"{report.DirectVfxSpawnPointCount + report.NestedVfxSpawnPointCount}");
            Require(
                report, report.AssignedVfxReferenceCount == ExpectedAssignedVfxReferenceCount,
                $"expected assigned UnitView._vfxSpawnPoint={ExpectedAssignedVfxReferenceCount}, " +
                $"found {report.AssignedVfxReferenceCount}");
            Require(
                report, report.NullVfxReferenceCount == ExpectedNullVfxReferenceCount,
                $"expected null UnitView._vfxSpawnPoint fallback={ExpectedNullVfxReferenceCount}, " +
                $"found {report.NullVfxReferenceCount}");

            // migration 전에는 0개, 완전 적용 후 재실행에는 50개여야 한다.
            // 일부 prefab만 바뀐 중간 상태는 다음 실행이 무엇을 보존해야 하는지 모호하므로 fail-closed다.
            Require(
                report,
                report.ExistingVisualRootCount == 0
                || report.ExistingVisualRootCount == ExpectedPrefabCount,
                $"partial VisualRoot migration detected: existing={report.ExistingVisualRootCount}");
            Require(
                report,
                report.ProjectorCount == 0
                || report.ProjectorCount == ExpectedPrefabCount,
                $"partial VisualRootProjector migration detected: projector={report.ProjectorCount}");
            Require(
                report, report.ProjectorCount == report.ExistingVisualRootCount,
                $"VisualRoot/projector count mismatch: roots={report.ExistingVisualRootCount}, " +
                $"projectors={report.ProjectorCount}");
            Require(
                report, report.ValidProjectorReferenceCount == report.ProjectorCount,
                $"invalid VisualRootProjector._visualRoot references: valid=" +
                $"{report.ValidProjectorReferenceCount}/{report.ProjectorCount}");
            if (report.ExistingVisualRootCount == 0)
            {
                Require(
                    report, report.PlannedVisualRootCreateCount == ExpectedPrefabCount,
                    $"initial dry-run must create {ExpectedPrefabCount} VisualRoots");
                Require(
                    report, report.ProjectorCount == 0,
                    "initial baseline must contain zero VisualRootProjectors");
                Require(
                    report, report.DirectVfxSpawnPointCount == 8
                    && report.NestedVfxSpawnPointCount == 8,
                    $"initial baseline must have direct/nested VFX sockets 8/8, found " +
                    $"{report.DirectVfxSpawnPointCount}/{report.NestedVfxSpawnPointCount}");
                Require(
                    report, report.PlannedMoveCount == ExpectedInitialMoveCount,
                    $"initial dry-run must move 50 visual models plus 8 direct VFX sockets; " +
                    $"found {report.PlannedMoveCount}");
            }
            else if (report.ExistingVisualRootCount == ExpectedPrefabCount)
            {
                Require(
                    report,
                    report.PlannedVisualRootCreateCount == 0
                    && report.PlannedVisualRootReuseCount == ExpectedPrefabCount
                    && report.PlannedMoveCount == 0,
                    $"idempotent rerun must plan create=0, reuse={ExpectedPrefabCount}, move=0");
                Require(
                    report,
                    report.ProjectorCount == ExpectedPrefabCount
                    && report.ValidProjectorReferenceCount == ExpectedPrefabCount,
                    $"migrated baseline must contain {ExpectedPrefabCount} valid projectors");
                Require(
                    report, report.AnimatorApplyRootMotionTrueCount == 0,
                    $"migrated Animators must have applyRootMotion=false; true=" +
                    $"{report.AnimatorApplyRootMotionTrueCount}");
                Require(
                    report, report.DirectVfxSpawnPointCount == 0
                    && report.NestedVfxSpawnPointCount == ExpectedTotalVfxSpawnPointCount,
                    $"migrated baseline must have direct/nested VFX sockets 0/16, found " +
                    $"{report.DirectVfxSpawnPointCount}/{report.NestedVfxSpawnPointCount}");
            }

            return report;
        }

        internal static List<string> FindUnitPrefabPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitsPrefabFolder });
            var paths = new List<string>(guids.Length);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!fileName.StartsWith("Unit_", StringComparison.Ordinal)) continue;
                if (path.Contains("/_Old/")) continue;
                paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private static void CountRace(string path, UnitVisualRootAuditReport report)
        {
            if (path.Contains("/Human/")) report.HumanCount++;
            else if (path.Contains("/Spirit/")) report.SpiritCount++;
            else if (path.Contains("/Transcendence/")) report.TranscendenceCount++;
            else report.Errors.Add($"{path}: prefab is outside the three canonical race folders");
        }

        private static bool TryParseIdentity(
            string path,
            out UnitType unitType,
            out string team)
        {
            unitType = default;
            team = null;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            const string prefix = "Unit_";
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) return false;

            string body = name.Substring(prefix.Length);
            string typeName;
            if (body.EndsWith("_Blue", StringComparison.Ordinal))
            {
                team = "Blue";
                typeName = body.Substring(0, body.Length - "_Blue".Length);
            }
            else if (body.EndsWith("_Red", StringComparison.Ordinal))
            {
                team = "Red";
                typeName = body.Substring(0, body.Length - "_Red".Length);
            }
            else
            {
                return false;
            }

            return Enum.TryParse(typeName, ignoreCase: false, out unitType)
                && Enum.IsDefined(typeof(UnitType), unitType);
        }

        private static void AnalyzePrefab(
            string path,
            string identityKey,
            int errorsBefore,
            UnitVisualRootAuditReport report)
        {
            GameObject root = null;
            var audit = new UnitVisualRootPrefabAudit
            {
                Path = path,
                IdentityKey = identityKey,
                VisualRootState = "unobserved",
                VfxReferenceState = "unobserved"
            };
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    report.Errors.Add($"{path}: PrefabUtility.LoadPrefabContents returned null");
                    return;
                }

                string expectedName = System.IO.Path.GetFileNameWithoutExtension(path);
                Require(report, root.name == expectedName, $"{path}: root name is '{root.name}'");

                UnitView[] allUnitViews = root.GetComponentsInChildren<UnitView>(true);
                NetworkObject[] allNetworkObjects = root.GetComponentsInChildren<NetworkObject>(true);
                NetworkTransform[] allNetworkTransforms =
                    root.GetComponentsInChildren<NetworkTransform>(true);
                NetworkUnit[] allNetworkUnits = root.GetComponentsInChildren<NetworkUnit>(true);

                RequireExactAuthoritativeRootComponent(path, nameof(UnitView), allUnitViews, root, report);
                RequireExactAuthoritativeRootComponent(
                    path, nameof(NetworkObject), allNetworkObjects, root, report);
                RequireExactAuthoritativeRootComponent(
                    path, nameof(NetworkTransform), allNetworkTransforms, root, report);
                RequireExactAuthoritativeRootComponent(
                    path, nameof(NetworkUnit), allNetworkUnits, root, report);

                Require(report, root.GetComponent<Animator>() == null, $"{path}: root Animator is forbidden");
                Require(report, root.GetComponent<Renderer>() == null, $"{path}: root Renderer is forbidden");

                Animator[] animators = root.GetComponentsInChildren<Animator>(true);
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                AnimationEventRelay[] relays = root.GetComponentsInChildren<AnimationEventRelay>(true);
                VisualRootProjector[] projectors =
                    root.GetComponentsInChildren<VisualRootProjector>(true);
                audit.AnimatorCount = animators.Length;
                audit.RendererCount = renderers.Length;
                audit.RelayCount = relays.Length;
                report.ProjectorCount += projectors.Length;

                Require(report, root.transform.childCount > 0, $"{path}: visual child is missing");
                Require(report, animators.Length > 0, $"{path}: descendant Animator is missing");
                Require(report, renderers.Length > 0, $"{path}: descendant Renderer is missing");
                foreach (Collider collider in colliders)
                {
                    Require(
                        report, collider.transform == root.transform,
                        $"{path}: Collider '{GetHierarchyPath(collider.transform, root.transform)}' " +
                        "must remain on Simulation Root");
                }
                Require(
                    report, relays.Length == animators.Length,
                    $"{path}: AnimationEventRelay count {relays.Length} does not match Animator count {animators.Length}");

                UnitView rootUnitView = allUnitViews.Length == 1 ? allUnitViews[0] : null;
                foreach (Animator animator in animators)
                {
                    if (animator.applyRootMotion)
                        report.AnimatorApplyRootMotionTrueCount++;

                    AnimationEventRelay relay = animator.GetComponent<AnimationEventRelay>();
                    Require(
                        report, relay != null,
                        $"{path}: Animator '{GetHierarchyPath(animator.transform, root.transform)}' " +
                        "does not have AnimationEventRelay on the same GameObject");
                }

                foreach (AnimationEventRelay relay in relays)
                {
                    UnitView resolved = relay.GetComponentInParent<UnitView>();
                    Require(
                        report, rootUnitView != null && resolved == rootUnitView,
                        $"{path}: relay '{GetHierarchyPath(relay.transform, root.transform)}' " +
                        "cannot resolve the canonical root UnitView through parent lookup");
                }

                Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform transform in allTransforms)
                {
                    int missingScriptCount =
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    Require(
                        report, missingScriptCount == 0,
                        $"{path}: '{GetHierarchyPath(transform, root.transform)}' has " +
                        $"{missingScriptCount} missing script component(s)");
                }

                Transform[] visualRoots = allTransforms
                    .Where(transform => transform != root.transform && transform.name == VisualRootName)
                    .ToArray();
                Transform visualRoot = null;
                if (visualRoots.Length == 0)
                {
                    audit.VisualRootState = "missing-create";
                    audit.WouldCreateVisualRoot = true;
                    report.PlannedVisualRootCreateCount++;
                }
                else if (visualRoots.Length == 1 && visualRoots[0].parent == root.transform)
                {
                    visualRoot = visualRoots[0];
                    audit.VisualRootState = "direct-reuse";
                    audit.WouldReuseVisualRoot = true;
                    report.ExistingVisualRootCount++;
                    report.PlannedVisualRootReuseCount++;
                    ValidateReusableVisualRoot(path, visualRoot, report);
                }
                else
                {
                    audit.VisualRootState = visualRoots.Length > 1 ? "duplicate-fail" : "nested-fail";
                    report.Errors.Add(
                        $"{path}: expected zero or one direct {VisualRootName}, found {visualRoots.Length} " +
                        "(nested or duplicate roots are fail-closed)");
                }

                if (visualRoot == null)
                {
                    Require(
                        report, projectors.Length == 0,
                        $"{path}: pre-migration prefab must contain zero VisualRootProjectors");
                }
                else
                {
                    Require(
                        report, projectors.Length == 1,
                        $"{path}: migrated prefab must contain exactly one VisualRootProjector, " +
                        $"found {projectors.Length}");
                    if (projectors.Length == 1)
                    {
                        VisualRootProjector projector = projectors[0];
                        Require(
                            report, projector.gameObject == root,
                            $"{path}: VisualRootProjector must be attached to Simulation Root");

                        var serializedProjector = new SerializedObject(projector);
                        serializedProjector.UpdateIfRequiredOrScript();
                        SerializedProperty visualRootProperty =
                            serializedProjector.FindProperty("_visualRoot");
                        bool validReference =
                            visualRootProperty != null
                            && visualRootProperty.propertyType
                            == SerializedPropertyType.ObjectReference
                            && visualRootProperty.objectReferenceValue == visualRoot;
                        Require(
                            report, validReference,
                            $"{path}: VisualRootProjector._visualRoot must reference the direct VisualRoot");
                        if (validReference)
                            report.ValidProjectorReferenceCount++;
                    }
                }

                List<Transform> directVfxSpawnPoints = GetDirectChildren(root.transform)
                    .Where(child => child.name == VfxSpawnPointName)
                    .ToList();
                report.DirectVfxSpawnPointCount += directVfxSpawnPoints.Count;

                int allNamedVfxCount = allTransforms.Count(transform => transform.name == VfxSpawnPointName);
                report.NestedVfxSpawnPointCount += allNamedVfxCount - directVfxSpawnPoints.Count;
                Require(
                    report, allNamedVfxCount <= 1,
                    $"{path}: multiple transforms named {VfxSpawnPointName} are ambiguous");

                // 무기 발사점과 VFX socket도 표현 좌표계에 속한다. 따라서 직접 VfxSpawnPoint를
                // 포함한 모든 직접 visual child가 이동 후보이며, 이미 VisualRoot 아래에 있는
                // 자식은 재실행 때 다시 계획하지 않는다.
                List<Transform> moveCandidates = GetDirectChildren(root.transform)
                    .Where(child => child != visualRoot)
                    .ToList();
                audit.MoveCandidateCount = moveCandidates.Count;
                report.PlannedMoveCount += moveCandidates.Count;

                if (visualRoot == null)
                {
                    Require(
                        report, directVfxSpawnPoints.Count <= 1,
                        $"{path}: initial structure may have at most one direct {VfxSpawnPointName}");
                    Require(
                        report,
                        moveCandidates.Count == 1 + directVfxSpawnPoints.Count,
                        $"{path}: initial structure must contain exactly one direct model child plus " +
                        $"zero or one direct {VfxSpawnPointName}; move candidates={moveCandidates.Count}");
                }
                else
                {
                    Require(
                        report, moveCandidates.Count == 0,
                        $"{path}: idempotent rerun found {moveCandidates.Count} children outside {VisualRootName}");
                }

                ValidateProspectiveVisualCoverage(
                    path, visualRoot, moveCandidates, animators, renderers, relays, report);
                ValidateVfxReference(
                    path, root, rootUnitView, visualRoot,
                    directVfxSpawnPoints, moveCandidates, audit, report);

                audit.RollbackManifest = BuildRollbackManifest(
                    path,
                    root,
                    visualRoot,
                    moveCandidates,
                    allUnitViews.Length == 1 ? allUnitViews[0] : null,
                    allNetworkObjects.Length == 1 ? allNetworkObjects[0] : null,
                    allNetworkTransforms.Length == 1 ? allNetworkTransforms[0] : null,
                    allNetworkUnits.Length == 1 ? allNetworkUnits[0] : null,
                    audit,
                    report);
                Require(
                    report, !string.IsNullOrEmpty(audit.RollbackManifest),
                    $"{path}: rollback manifest generation failed");
            }
            catch (Exception exception)
            {
                report.Errors.Add($"{path}: analyzer exception {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                // 임시 prefab contents는 정상/오류 경로 모두 반드시 해제한다.
                // B0에는 저장 호출이 없으므로 이 해제는 asset 내용을 바꾸지 않는다.
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);

                audit.Passed = report.Errors.Count == errorsBefore;
                report.Prefabs.Add(audit);
            }
        }

        private static void ValidateProspectiveVisualCoverage(
            string path,
            Transform existingVisualRoot,
            List<Transform> moveCandidates,
            Animator[] animators,
            Renderer[] renderers,
            AnimationEventRelay[] relays,
            UnitVisualRootAuditReport report)
        {
            foreach (Animator animator in animators)
            {
                Require(
                    report, WouldBeUnderVisualRoot(animator.transform, existingVisualRoot, moveCandidates),
                    $"{path}: Animator '{animator.name}' would remain outside {VisualRootName}");
            }

            foreach (Renderer renderer in renderers)
            {
                Require(
                    report, WouldBeUnderVisualRoot(renderer.transform, existingVisualRoot, moveCandidates),
                    $"{path}: Renderer '{renderer.name}' would remain outside {VisualRootName}");
            }

            foreach (AnimationEventRelay relay in relays)
            {
                Require(
                    report, WouldBeUnderVisualRoot(relay.transform, existingVisualRoot, moveCandidates),
                    $"{path}: AnimationEventRelay '{relay.name}' would remain outside {VisualRootName}");
            }
        }

        private static void ValidateVfxReference(
            string path,
            GameObject root,
            UnitView rootUnitView,
            Transform existingVisualRoot,
            List<Transform> directVfxSpawnPoints,
            List<Transform> moveCandidates,
            UnitVisualRootPrefabAudit audit,
            UnitVisualRootAuditReport report)
        {
            if (rootUnitView == null)
            {
                audit.VfxReferenceState = "unit-view-missing";
                return;
            }

            var serializedUnitView = new SerializedObject(rootUnitView);
            serializedUnitView.UpdateIfRequiredOrScript();
            SerializedProperty property = serializedUnitView.FindProperty("_vfxSpawnPoint");
            if (property == null)
            {
                audit.VfxReferenceState = "serialized-property-missing";
                report.Errors.Add($"{path}: UnitView._vfxSpawnPoint serialized property is missing");
                return;
            }

            Transform reference = property.objectReferenceValue as Transform;
            if (reference == null)
            {
                audit.VfxReferenceState = "null-fallback";
                report.NullVfxReferenceCount++;
                return;
            }

            report.AssignedVfxReferenceCount++;
            bool belongsToPrefab = reference == root.transform || reference.IsChildOf(root.transform);
            Require(report, belongsToPrefab, $"{path}: _vfxSpawnPoint points outside its prefab root");
            Require(
                report, reference.name == VfxSpawnPointName,
                $"{path}: _vfxSpawnPoint target is named '{reference.name}', expected '{VfxSpawnPointName}'");

            bool isDirect = reference.parent == root.transform;
            if (isDirect)
            {
                audit.VfxReferenceState = "assigned-direct-planned-move";
                Require(
                    report, directVfxSpawnPoints.Contains(reference),
                    $"{path}: direct _vfxSpawnPoint is not recognized by the migration plan");
                Require(
                    report, moveCandidates.Contains(reference),
                    $"{path}: direct _vfxSpawnPoint must move under {VisualRootName}");
            }
            else
            {
                audit.VfxReferenceState = "assigned-nested-preserved";
                Require(
                    report,
                    (existingVisualRoot != null && reference.IsChildOf(existingVisualRoot))
                    || moveCandidates.Any(candidate => reference.IsChildOf(candidate)),
                    $"{path}: nested _vfxSpawnPoint is not carried by a planned visual child");
            }
        }

        private static bool WouldBeUnderVisualRoot(
            Transform value,
            Transform existingVisualRoot,
            List<Transform> moveCandidates)
        {
            if (existingVisualRoot != null
                && (value == existingVisualRoot || value.IsChildOf(existingVisualRoot)))
                return true;

            return moveCandidates.Any(
                candidate => value == candidate || value.IsChildOf(candidate));
        }

        private static void ValidateReusableVisualRoot(
            string path,
            Transform visualRoot,
            UnitVisualRootAuditReport report)
        {
            const float epsilon = 0.00001f;
            Require(
                report, visualRoot.localPosition.sqrMagnitude <= epsilon * epsilon,
                $"{path}: reusable {VisualRootName} localPosition must be zero");
            Require(
                report, Quaternion.Angle(visualRoot.localRotation, Quaternion.identity) <= epsilon,
                $"{path}: reusable {VisualRootName} localRotation must be identity");
            Require(
                report, (visualRoot.localScale - Vector3.one).sqrMagnitude <= epsilon * epsilon,
                $"{path}: reusable {VisualRootName} localScale must be one");

            // 네트워크와 authoritative view 컴포넌트는 Simulation Root에만 남아야 한다.
            // 같은 타입이 VisualRoot에 있으면 이름만 맞는 임의 오브젝트일 수 있으므로 재사용하지 않는다.
            Require(
                report, visualRoot.GetComponentsInChildren<UnitView>(true).Length == 0,
                $"{path}: {VisualRootName} descendants must not contain UnitView");
            Require(
                report, visualRoot.GetComponentsInChildren<NetworkObject>(true).Length == 0,
                $"{path}: {VisualRootName} descendants must not contain NetworkObject");
            Require(
                report, visualRoot.GetComponentsInChildren<NetworkTransform>(true).Length == 0,
                $"{path}: {VisualRootName} descendants must not contain NetworkTransform");
            Require(
                report, visualRoot.GetComponentsInChildren<NetworkUnit>(true).Length == 0,
                $"{path}: {VisualRootName} descendants must not contain NetworkUnit");
            Require(
                report, visualRoot.GetComponentsInChildren<Collider>(true).Length == 0,
                $"{path}: {VisualRootName} descendants must not contain Collider");
        }

        private static string BuildRollbackManifest(
            string path,
            GameObject root,
            Transform existingVisualRoot,
            List<Transform> moveCandidates,
            UnitView unitView,
            NetworkObject networkObject,
            NetworkTransform networkTransform,
            NetworkUnit networkUnit,
            UnitVisualRootPrefabAudit audit,
            UnitVisualRootAuditReport report)
        {
            if (unitView == null || networkObject == null
                || networkTransform == null || networkUnit == null)
            {
                report.Errors.Add(
                    $"{path}: rollback manifest requires all four canonical root components");
                return null;
            }

            try
            {
                List<Transform> beforeChildren = GetDirectChildren(root.transform);
                string beforeHierarchy = string.Join(
                    ",",
                    beforeChildren.Select(child => FormatTransformState(child, root.transform)));

                var plannedVisualChildren = new List<Transform>();
                if (existingVisualRoot != null)
                    plannedVisualChildren.AddRange(GetDirectChildren(existingVisualRoot));
                plannedVisualChildren.AddRange(moveCandidates);

                string afterHierarchy =
                    $"{root.name}/{VisualRootName}[{string.Join(",", plannedVisualChildren.Select(child => child.name))}]";
                string moves = moveCandidates.Count == 0
                    ? "none"
                    : string.Join(
                        ";",
                        moveCandidates.Select(candidate =>
                            FormatTransformState(candidate, root.transform)));

                string visualRootPlan = existingVisualRoot == null
                    ? $"create:{VisualRootName}|p=(0,0,0)|r=(0,0,0,1)|s=(1,1,1)"
                    : $"reuse:{FormatTransformState(existingVisualRoot, root.transform)}";

                string references = string.Join(
                    ";",
                    new[]
                    {
                        CollectObjectReferences(unitView, root.transform),
                        CollectObjectReferences(networkObject, root.transform),
                        CollectObjectReferences(networkTransform, root.transform),
                        CollectObjectReferences(networkUnit, root.transform)
                    });

                string globalObjectIdHash = ReadGlobalObjectIdHash(networkObject);
                if (string.IsNullOrEmpty(globalObjectIdHash))
                {
                    report.Errors.Add($"{path}: NetworkObject GlobalObjectIdHash is unavailable");
                    return null;
                }

                string networkObjectHash =
                    ComputeStableSerializedConfigHash(
                        networkObject,
                        root.transform,
                        NetworkObjectConfigPropertyPaths);
                string networkTransformHash =
                    ComputeStableSerializedConfigHash(
                        networkTransform,
                        root.transform,
                        NetworkTransformConfigPropertyPaths);
                if (string.IsNullOrEmpty(networkObjectHash)
                    || string.IsNullOrEmpty(networkTransformHash))
                {
                    report.Errors.Add($"{path}: serialized network config SHA-256 failed");
                    return null;
                }

                audit.NetworkObjectGlobalObjectIdHash = globalObjectIdHash;
                audit.NetworkObjectSerializedSha256 = networkObjectHash;
                audit.NetworkTransformSerializedSha256 = networkTransformHash;

                return
                    $"beforeDirect=[{beforeHierarchy}]|" +
                    $"plannedAfter={afterHierarchy}|" +
                    $"moves=[{moves}]|" +
                    $"visualRoot={visualRootPlan}|" +
                    $"objectRefs=[{references}]|" +
                    $"networkObjectGlobalObjectIdHash={globalObjectIdHash}|" +
                    $"networkObjectSerializedSha256={networkObjectHash}|" +
                    $"networkTransformSerializedSha256={networkTransformHash}";
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    $"{path}: rollback manifest exception {exception.GetType().Name}: {exception.Message}");
                return null;
            }
        }

        private static string CollectObjectReferences(
            Component component,
            Transform root)
        {
            var references = new List<string>();
            var serializedObject = new SerializedObject(component);
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty iterator = serializedObject.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference
                    || iterator.objectReferenceValue == null)
                    continue;

                references.Add(
                    $"{component.GetType().Name}.{iterator.propertyPath}->" +
                    DescribeObjectReference(iterator.objectReferenceValue, root));
            }

            references.Sort(StringComparer.Ordinal);
            return $"{component.GetType().Name}{{{string.Join(",", references)}}}";
        }

        private static string DescribeObjectReference(UnityEngine.Object value, Transform root)
        {
            if (value is Component component
                && (component.transform == root || component.transform.IsChildOf(root)))
            {
                return
                    $"{GetHierarchyPath(component.transform, root)}#{component.GetType().Name}";
            }

            if (value is GameObject gameObject
                && (gameObject.transform == root || gameObject.transform.IsChildOf(root)))
            {
                return GetHierarchyPath(gameObject.transform, root);
            }

            string assetPath = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(assetPath))
                return assetPath;

            throw new InvalidOperationException(
                $"Object reference '{value.name}' ({value.GetType().Name}) has no prefab hierarchy or asset path");
        }

        private static string ReadGlobalObjectIdHash(NetworkObject networkObject)
        {
            var serializedObject = new SerializedObject(networkObject);
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty property = serializedObject.FindProperty("GlobalObjectIdHash");
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
                return null;
            return unchecked((uint)property.longValue).ToString(CultureInfo.InvariantCulture);
        }

        private static string ComputeStableSerializedConfigHash(
            Component component,
            Transform root,
            IReadOnlyList<string> configPropertyPaths)
        {
            var entries = new List<string>();
            var serializedObject = new SerializedObject(component);
            serializedObject.UpdateIfRequiredOrScript();
            foreach (string propertyPath in configPropertyPaths)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyPath);
                if (property == null
                    || !TryFormatSerializedProperty(property, root, out string value))
                    return null;

                entries.Add($"{property.propertyPath}|{property.propertyType}|{value}");
            }

            entries.Sort(StringComparer.Ordinal);
            if (entries.Count == 0)
                return null;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", entries));
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static bool TryFormatSerializedProperty(
            SerializedProperty property,
            Transform root,
            out string value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                    value = property.isArray
                        ? $"array:{property.arraySize.ToString(CultureInfo.InvariantCulture)}"
                        : "container";
                    return true;
                case SerializedPropertyType.Integer:
                    value = property.longValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Boolean:
                    value = property.boolValue ? "true" : "false";
                    return true;
                case SerializedPropertyType.Float:
                    value = property.doubleValue.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.String:
                    value = Convert.ToBase64String(Encoding.UTF8.GetBytes(property.stringValue ?? string.Empty));
                    return true;
                case SerializedPropertyType.Color:
                    Color color = property.colorValue;
                    value =
                        $"{FormatFloat(color.r)},{FormatFloat(color.g)}," +
                        $"{FormatFloat(color.b)},{FormatFloat(color.a)}";
                    return true;
                case SerializedPropertyType.ObjectReference:
                    value = property.objectReferenceValue == null
                        ? "null"
                        : DescribeObjectReference(property.objectReferenceValue, root);
                    return true;
                case SerializedPropertyType.LayerMask:
                    value = property.intValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Enum:
                    value =
                        $"{property.enumValueIndex.ToString(CultureInfo.InvariantCulture)}:" +
                        $"{property.enumNames.ElementAtOrDefault(property.enumValueIndex) ?? string.Empty}";
                    return true;
                case SerializedPropertyType.Vector2:
                    Vector2 vector2 = property.vector2Value;
                    value = $"{FormatFloat(vector2.x)},{FormatFloat(vector2.y)}";
                    return true;
                case SerializedPropertyType.Vector3:
                    value = FormatVector3(property.vector3Value);
                    return true;
                case SerializedPropertyType.Vector4:
                    Vector4 vector4 = property.vector4Value;
                    value =
                        $"{FormatFloat(vector4.x)},{FormatFloat(vector4.y)}," +
                        $"{FormatFloat(vector4.z)},{FormatFloat(vector4.w)}";
                    return true;
                case SerializedPropertyType.Rect:
                    Rect rect = property.rectValue;
                    value =
                        $"{FormatFloat(rect.x)},{FormatFloat(rect.y)}," +
                        $"{FormatFloat(rect.width)},{FormatFloat(rect.height)}";
                    return true;
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.FixedBufferSize:
                    value = property.intValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Character:
                    value = property.intValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Bounds:
                    Bounds bounds = property.boundsValue;
                    value = $"{FormatVector3(bounds.center)}|{FormatVector3(bounds.size)}";
                    return true;
                case SerializedPropertyType.Quaternion:
                    value = FormatQuaternion(property.quaternionValue);
                    return true;
                case SerializedPropertyType.ExposedReference:
                    value = property.exposedReferenceValue == null
                        ? "null"
                        : DescribeObjectReference(property.exposedReferenceValue, root);
                    return true;
                case SerializedPropertyType.Vector2Int:
                    Vector2Int vector2Int = property.vector2IntValue;
                    value =
                        $"{vector2Int.x.ToString(CultureInfo.InvariantCulture)}," +
                        $"{vector2Int.y.ToString(CultureInfo.InvariantCulture)}";
                    return true;
                case SerializedPropertyType.Vector3Int:
                    Vector3Int vector3Int = property.vector3IntValue;
                    value =
                        $"{vector3Int.x.ToString(CultureInfo.InvariantCulture)}," +
                        $"{vector3Int.y.ToString(CultureInfo.InvariantCulture)}," +
                        $"{vector3Int.z.ToString(CultureInfo.InvariantCulture)}";
                    return true;
                case SerializedPropertyType.RectInt:
                    RectInt rectInt = property.rectIntValue;
                    value =
                        $"{rectInt.x.ToString(CultureInfo.InvariantCulture)}," +
                        $"{rectInt.y.ToString(CultureInfo.InvariantCulture)}," +
                        $"{rectInt.width.ToString(CultureInfo.InvariantCulture)}," +
                        $"{rectInt.height.ToString(CultureInfo.InvariantCulture)}";
                    return true;
                case SerializedPropertyType.BoundsInt:
                    BoundsInt boundsInt = property.boundsIntValue;
                    value =
                        $"{boundsInt.position.x.ToString(CultureInfo.InvariantCulture)}," +
                        $"{boundsInt.position.y.ToString(CultureInfo.InvariantCulture)}," +
                        $"{boundsInt.position.z.ToString(CultureInfo.InvariantCulture)}|" +
                        $"{boundsInt.size.x.ToString(CultureInfo.InvariantCulture)}," +
                        $"{boundsInt.size.y.ToString(CultureInfo.InvariantCulture)}," +
                        $"{boundsInt.size.z.ToString(CultureInfo.InvariantCulture)}";
                    return true;
                case SerializedPropertyType.Hash128:
                    value = property.hash128Value.ToString();
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        private static string FormatTransformState(Transform value, Transform root)
        {
            Vector3 position = value.localPosition;
            Quaternion rotation = value.localRotation;
            Vector3 scale = value.localScale;
            return
                $"{GetHierarchyPath(value, root)}@{value.GetSiblingIndex()}|" +
                $"p={FormatVector3(position)}|" +
                $"r={FormatQuaternion(rotation)}|" +
                $"s={FormatVector3(scale)}";
        }

        private static string FormatVector3(Vector3 value)
        {
            return
                $"({FormatFloat(value.x)},{FormatFloat(value.y)},{FormatFloat(value.z)})";
        }

        private static string FormatQuaternion(Quaternion value)
        {
            return
                $"({FormatFloat(value.x)},{FormatFloat(value.y)}," +
                $"{FormatFloat(value.z)},{FormatFloat(value.w)})";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static List<Transform> GetDirectChildren(Transform root)
        {
            var children = new List<Transform>(root.childCount);
            for (int index = 0; index < root.childCount; index++)
                children.Add(root.GetChild(index));
            return children;
        }

        private static string GetHierarchyPath(Transform value, Transform root)
        {
            var names = new Stack<string>();
            Transform current = value;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root) break;
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static void RequireExactAuthoritativeRootComponent<T>(
            string path,
            string componentName,
            T[] components,
            GameObject root,
            UnitVisualRootAuditReport report)
            where T : Component
        {
            Require(
                report, components.Length == 1,
                $"{path}: full hierarchy must contain exactly one {componentName}, " +
                $"found {components.Length}");
            if (components.Length == 1)
            {
                Require(
                    report, components[0].gameObject == root,
                    $"{path}: {componentName} must be attached to Simulation Root");
            }
        }

        private static void Require(
            UnitVisualRootAuditReport report,
            bool condition,
            string message)
        {
            if (!condition)
                report.Errors.Add(message);
        }
    }
}
