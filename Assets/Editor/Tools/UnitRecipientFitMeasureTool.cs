// ============================================================================
// UnitRecipientFitMeasureTool.cs  (영구 보존 도구 — Assets/Editor/Tools/, 삭제 금지)
//
// 무엇을 하는가 (2026-10-13 vfx-code-followups C, 사용자 결정 D-1 「혼합」):
//   화상·독·힐 같은 "유닛 몸에 붙는 상태 이펙트"를 맞은 유닛 크기에 맞추려면 유닛마다 몸 크기
//   (키·반경·발밑 위치)가 필요하다. 게임 중에 매 프레임 렌더러 크기를 재면 애니메이션 때문에
//   크기가 출렁이고 모바일 비용도 든다. 그래서 이 도구가 "에디터에서 한 번" 실제 생산 유닛 프리팹을
//   재서 UnitEffectConfig.asset 의 크기 표(_recipientFits)에 채워 둔다. 게임은 그 표만 읽는다.
//
// 언제 다시 실행하나:
//   유닛 모델을 바꾸거나 새 유닛을 추가했을 때. 다시 실행해도 사람이 Inspector에서 넣은
//   manualScale(수동 배율)은 지우지 않고 그대로 둔다(측정값 radius/height/footOffset만 갱신).
//
// 어떤 유닛 프리팹을 재는가 (이름 추측이 아니라 실제 생산 연결을 따라간다):
//   Game 씬의 UnitFactory 에 등록된 종족별 목록(_humanPrefabs/_spiritPrefabs/_transcendencePrefabs)의
//   Blue 프리팹을 잰다. Red 프리팹도 재서 Blue와 5% 넘게 다르면 경고만 남긴다(표에는 Blue 값).
//   Game 씬은 "미리보기(preview)"로만 열고 닫는다 → 현재 열린 씬·저장 상태를 바꾸지 않는다.
//
// 어떤 렌더러를 "몸통"으로 보는가 (실제 25종 프리팹 구조를 보고 정한 기준):
//   - SkinnedMeshRenderer(애니메이션 되는 캐릭터 몸, 이름 char1)가 있으면 그것만 잰다.
//     같은 유닛의 MeshRenderer는 무기·방패·화살(Rifle, HumanAxe, BearShield, EagleBow 등)이라 제외한다.
//   - SkinnedMeshRenderer가 하나도 없으면(Tank, CannonCart 같은 차량) MeshRenderer 전체를 잰다.
//   - 파티클·스프라이트·트레일·라인 렌더러는 이펙트/표시용이라 항상 제외한다.
//   - 자세는 프리팹에 저장된 자세(애니메이션을 돌리지 않은 기본 자세)를 쓴다.
//
// 측정 값의 의미 (모두 VisualRoot 기준, 단위 월드 미터 — VisualRoot 스케일은 전 유닛 1):
//   height     = 몸통 최고점 Y − 최저점 Y
//   radius     = 몸통 XZ 가운데에서 가장 먼 점까지 거리
//   footOffset = (몸통 XZ 가운데 X, 최저점 Y, 몸통 XZ 가운데 Z) — 이펙트 pivot을 "발밑 가운데"로 옮기는 보정
//
// 좌표계 하나로 통일 (2026-10-13 스케일 버그 수정):
//   모든 유닛(스킨드든 차량이든)을 "프리팹 루트를 원점·무회전에 둔 상태의 실제 월드 크기"로 잰 뒤
//   VisualRoot 기준으로 옮긴다. 런타임도 이펙트 위치를 `VisualRoot 위치 + VisualRoot 회전 × footOffset`
//   (VfxPoolItem.TimedDamage.cs FollowTimedDamage / VfxPoolItem.cs Bloom)으로 계산하고 스케일은 곱하지 않으므로,
//   여기서도 "회전만 되돌리고 월드 미터 그대로" 재야 같은 공간이 된다.
//   🔴 예전 버그: 스킨드 메시는 굽기(BakeMesh) 결과를 "렌더러 자기 로컬 공간"으로 받는데, 그 위에 위치·회전만
//   더하고 스케일을 빼먹었다. 생산 유닛은 `Unit_xxx_Mesh` 오브젝트가 0.2~0.3 배로 줄여져 있어서
//   (뼈대 Armature 도 그 아래에 있으므로 실제 화면 크기는 0.2~0.3 배) 키·반경이 3~5배 크게 기록됐다.
//   → 지금은 렌더러의 localToWorldMatrix(위치·회전·스케일 전부)로 옮긴다. 차량(MeshRenderer) 경로는 원래 이렇게 했다.
//
// 이상값 경고:
//   측정 키가 1.2m 를 넘거나, 반경이 0.43m(헥스 타일 폭의 절반)를 넘거나, 키가 0.1m 보다 작으면 경고를 남긴다.
//   유닛이 타일보다 크거나 손톱만 하면 거의 확실히 좌표계/스케일 회귀이므로, 표에는 쓰되 눈에 띄게 알린다.
//
// 안전:
//   - 프리팹은 LoadPrefabContents 로 격리해 읽고 저장하지 않고 닫는다(프리팹 수정 0).
//   - 쓰는 에셋은 UnitEffectConfig.asset 하나뿐이며, 실행 전에 확인 창을 띄운다.
//   - Play 중에는 실행하지 않는다.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using Hexiege.Application;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using Hexiege.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 생산 유닛 프리팹의 몸 크기를 재서 UnitEffectConfig 의 상태 이펙트 크기 표를 채우는 메뉴 도구.
    /// </summary>
    public static class UnitRecipientFitMeasureTool
    {
        private const string ConfigPath = "Assets/_Project/Resources/Config/UnitEffectConfig.asset";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string LogClass = nameof(UnitRecipientFitMeasureTool);
        /// <summary>Blue/Red 측정값 차이를 경고로 남기는 비율(5%).</summary>
        private const float TeamMismatchRatio = 0.05f;

        // ── 이상값 경고 기준 (2026-10-13 스케일 버그 재발 감지용) ──
        //   헥스 타일 한 칸의 폭은 약 0.87m 다. 유닛은 타일 위에 서므로 "반경 > 타일 폭의 절반" 이면 옆 칸까지
        //   삐져나오는 크기라 측정이 잘못됐을 가능성이 높다. 키도 같은 이유로 1.2m 를 넘으면 의심한다.
        //   반대로 0.1m 보다 작으면 스케일을 두 번 곱한 반대 방향의 회귀일 수 있다.
        /// <summary>이 키(미터)를 넘으면 경고.</summary>
        internal const float SuspiciousMaxHeight = 1.2f;
        /// <summary>이 반경(미터, 헥스 타일 폭의 절반)을 넘으면 경고.</summary>
        internal const float SuspiciousMaxRadius = 0.43f;
        /// <summary>이 키(미터)보다 작으면 경고.</summary>
        internal const float SuspiciousMinHeight = 0.1f;

        /// <summary>한 유닛 프리팹의 측정 결과.</summary>
        internal struct Measurement
        {
            public bool Ok;
            public float Height, Radius;
            public Vector3 FootOffset;
            public int RendererCount;
            public string Method;   // SkinnedBake / MeshVertices / BoundsFallback (로그용)
            public string Failure;  // 실패 사유(로그용 짧은 토큰)
        }

        /// <summary>
        /// 메뉴: 실제 생산 유닛 프리팹 25종을 재서 상태 이펙트 크기 표를 채운다(수동 배율은 보존).
        /// </summary>
        [MenuItem("Hexiege/Effects/Measure Unit Recipient Fit")]
        public static void MeasureAndWrite()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Recipient Fit", "Play 모드에서는 실행할 수 없습니다. Play를 멈춘 뒤 다시 실행하세요.", "확인");
                return;
            }
            var config = AssetDatabase.LoadAssetAtPath<UnitEffectConfig>(ConfigPath);
            if (config == null)
            {
                GameLog.Dev.Warn("Combat", LogClass, "UnitEffectConfig 에셋을 찾지 못해 측정을 중단했다", "Path=" + ConfigPath);
                EditorUtility.DisplayDialog("Recipient Fit", "UnitEffectConfig.asset 을 찾지 못했습니다:\n" + ConfigPath, "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog("Recipient Fit",
                    "Game 씬 UnitFactory 에 등록된 유닛 프리팹을 재서\n" + ConfigPath + "\n의 상태 이펙트 크기 표를 갱신합니다.\n\n"
                    + "- 프리팹·씬은 수정하지 않습니다.\n- Inspector에서 넣은 manualScale 은 그대로 둡니다.\n\n진행할까요?",
                    "측정 후 저장", "취소"))
                return;

            List<(UnitType Type, GameObject Blue, GameObject Red)> rows = ReadProductionPrefabs();
            if (rows == null) return; // 사유는 ReadProductionPrefabs 가 이미 기록했다.

            var results = new List<(UnitType Type, Measurement M)>();
            int failures = 0;
            foreach (var row in rows)
            {
                Measurement blue = Measure(row.Blue);
                if (!blue.Ok)
                {
                    failures++;
                    GameLog.Dev.Warn("Combat", LogClass, "유닛 몸 크기 측정 실패 — 이 유닛은 표를 갱신하지 않는다",
                        $"Type={row.Type}, Reason={blue.Failure}");
                    continue;
                }
                WarnIfTeamsDiffer(row.Type, blue, row.Red);
                // 이상값이어도 표에는 쓴다(측정 자체는 성공) — 대신 경고로 눈에 띄게 남겨 회귀를 바로 알 수 있게 한다.
                WarnIfImplausible(row.Type, blue);
                results.Add((row.Type, blue));
            }

            int written = WriteTable(config, results);
            string summary = string.Format(CultureInfo.InvariantCulture,
                "Registered={0}, Written={1}, Failed={2}", rows.Count, written, failures);
            GameLog.Dev.Info("Combat", LogClass, "상태 이펙트 크기 표 측정·저장 완료", summary);
            EditorUtility.DisplayDialog("Recipient Fit",
                "완료: 등록 " + rows.Count + "종 / 저장 " + written + "종 / 실패 " + failures + "종\n\n"
                + "자세한 값은 Console(및 런타임 로그 파일)의 [Combat/" + LogClass + "] 줄을 확인하세요.\n"
                + "Inspector: " + ConfigPath + " → 상태 이펙트 크기 맞춤", "확인");
        }

        /// <summary>
        /// Game 씬을 미리보기로 열어 UnitFactory 의 종족별 등록 목록에서 (타입, Blue, Red) 를 읽는다.
        /// 씬은 바로 닫으며 현재 편집 중인 씬에는 영향이 없다.
        /// </summary>
        private static List<(UnitType, GameObject, GameObject)> ReadProductionPrefabs()
        {
            var rows = new List<(UnitType, GameObject, GameObject)>();
            var seen = new HashSet<UnitType>();
            var scene = EditorSceneManager.OpenPreviewScene(GameScenePath);
            try
            {
                int factories = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    foreach (string listName in new[] { "_humanPrefabs", "_spiritPrefabs", "_transcendencePrefabs" })
                    {
                        SerializedProperty list = serialized.FindProperty(listName);
                        if (list == null || !list.isArray) continue;
                        for (int i = 0; i < list.arraySize; i++)
                        {
                            SerializedProperty entry = list.GetArrayElementAtIndex(i);
                            // enum 프로퍼티의 intValue 는 enum 의 실제 숫자 값이다(UnitType 은 0~7/10~18/20~27 로 띄엄띄엄).
                            var type = (UnitType)entry.FindPropertyRelative("type").intValue;
                            var blue = entry.FindPropertyRelative("blue").objectReferenceValue as GameObject;
                            var red = entry.FindPropertyRelative("red").objectReferenceValue as GameObject;
                            if (blue == null || !seen.Add(type)) continue;
                            rows.Add((type, blue, red));
                        }
                    }
                }
                if (factories != 1)
                {
                    GameLog.Dev.Warn("Combat", LogClass, "Game 씬의 UnitFactory 가 정확히 1개가 아니라 측정을 중단했다",
                        "Factories=" + factories);
                    EditorUtility.DisplayDialog("Recipient Fit", "Game 씬의 UnitFactory 가 1개가 아닙니다(" + factories + "개). 측정을 중단합니다.", "확인");
                    return null;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return rows;
        }

        /// <summary>
        /// 프리팹 하나를 격리 로드해 VisualRoot 기준 몸통 크기를 잰다. 프리팹은 저장하지 않고 닫는다.
        /// (internal: 자체 검증 RunUnitActionSelfValidation 이 같은 측정 코드를 그대로 재사용한다.)
        /// </summary>
        /// <param name="prefab">잴 유닛 프리팹 에셋.</param>
        /// <returns>측정 결과(실패 시 Ok=false, Failure 에 사유).</returns>
        internal static Measurement Measure(GameObject prefab)
        {
            var result = new Measurement();
            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
            if (string.IsNullOrEmpty(path)) { result.Failure = "PrefabPathMissing"; return result; }
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // 런타임에 유닛을 생성하는 것과 같은 조건을 만든다: 루트를 원점·무회전에 둔다.
                //   루트 스케일은 손대지 않는다 — 런타임 Instantiate 도 프리팹 루트 스케일을 그대로 쓰기 때문이다
                //   (현재 25종 루트 스케일은 모두 1). 이 변경은 격리 로드본에만 적용되고 저장하지 않으므로 프리팹은 그대로다.
                //   (VisualRoot 기준으로 다시 옮기므로 결과에는 영향이 없지만, "한 기준에서 잰다"를 코드로 보장해 둔다.)
                contents.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform visualRoot = ResolveVisualRoot(contents);
                var points = new List<Vector3>(4096);
                result.Method = CollectBodyPoints(visualRoot, points, out result.RendererCount);
                if (points.Count == 0) { result.Failure = "NoBodyRenderer"; return result; }

                // 월드 점 → VisualRoot 기준(회전만 되돌리고 스케일은 쓰지 않음 = 월드 미터 유지).
                Quaternion inverse = Quaternion.Inverse(visualRoot.rotation);
                Vector3 origin = visualRoot.position;
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 p = inverse * (points[i] - origin);
                    points[i] = p;
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
                float cx = (min.x + max.x) * 0.5f, cz = (min.z + max.z) * 0.5f;
                float radiusSqr = 0f;
                foreach (Vector3 p in points)
                {
                    float dx = p.x - cx, dz = p.z - cz;
                    radiusSqr = Mathf.Max(radiusSqr, dx * dx + dz * dz);
                }
                result.Height = max.y - min.y;
                result.Radius = Mathf.Sqrt(radiusSqr);
                result.FootOffset = new Vector3(cx, min.y, cz);
                result.Ok = result.Height > 0.0001f && IsFinite(result.FootOffset) && !float.IsNaN(result.Radius);
                if (!result.Ok) result.Failure = "DegenerateBounds";
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents); // 저장 없이 닫는다
            }
        }

        /// <summary>
        /// VisualRootProjector 가 가리키는 VisualRoot 를 찾는다. 없으면 "VisualRoot" 이름 자식, 그것도 없으면 루트.
        /// (런타임 상태 이펙트 pose 도 같은 VisualRoot 를 쓰므로 같은 기준에서 재야 한다.)
        /// </summary>
        private static Transform ResolveVisualRoot(GameObject root)
        {
            var projector = root.GetComponent<VisualRootProjector>();
            if (projector != null)
            {
                var visual = new SerializedObject(projector).FindProperty("_visualRoot")?.objectReferenceValue as Transform;
                if (visual != null) return visual;
            }
            Transform named = root.transform.Find("VisualRoot");
            return named != null ? named : root.transform;
        }

        /// <summary>
        /// 몸통 렌더러의 정점을 월드 좌표로 모은다. 기준은 파일 머리말 참고(SMR 우선, 없으면 MeshRenderer).
        /// </summary>
        /// <returns>사용한 측정 방법 이름(로그용).</returns>
        private static string CollectBodyPoints(Transform visualRoot, List<Vector3> points, out int rendererCount)
        {
            rendererCount = 0;
            string method = "None";
            var skinned = visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length > 0)
            {
                foreach (var smr in skinned)
                {
                    if (smr.sharedMesh == null) continue;
                    rendererCount++;
                    method = AddSkinnedPoints(smr, points) ? Combine(method, "SkinnedBake") : Combine(method, "BoundsFallback");
                }
                return method;
            }
            foreach (var renderer in visualRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                rendererCount++;
                Mesh mesh = filter.sharedMesh;
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                if (mesh.isReadable)
                {
                    foreach (Vector3 v in mesh.vertices) points.Add(toWorld.MultiplyPoint3x4(v));
                    method = Combine(method, "MeshVertices");
                }
                else
                {
                    // Read/Write 가 꺼진 메시는 정점 대신 메시 로컬 경계 상자의 8 꼭짓점으로 잰다(약간 크게 나올 수 있음).
                    AddBoxCorners(mesh.bounds, toWorld, points);
                    method = Combine(method, "BoundsFallback");
                }
            }
            return method;
        }

        /// <summary>
        /// 스킨드 메시를 현재(프리팹 저장) 자세로 굽고 정점을 월드로 옮긴다. 실패하면 렌더러 경계 상자로 대신한다.
        /// </summary>
        private static bool AddSkinnedPoints(SkinnedMeshRenderer smr, List<Vector3> points)
        {
            var baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                // [버그 수정 2026-10-13] 굽기 결과를 "렌더러 로컬 공간"으로 받고, 렌더러의 전체 변환으로 월드에 옮긴다.
                //
                //   스킨드 메시는 뼈를 따라 실제 화면(월드)에 그려진 뒤, BakeMesh 가 그 결과를 렌더러 Transform 의
                //   로컬 공간으로 되돌려 준다. useScale=true 이면 렌더러(와 부모들)의 스케일까지 "되돌린" 값,
                //   즉 이 Transform 아래 MeshFilter 로 다시 그리면 화면과 똑같이 보이는 순수 로컬 좌표다.
                //   그래서 월드로 보내려면 위치·회전뿐 아니라 스케일까지 포함한 localToWorldMatrix 를 곱해야 한다.
                //
                //   예: Pistoleer 는 Unit_Pistoleer_Blue_Mesh(스케일 0.2) 아래에 몸통 char1 과 뼈대 Armature 가 있다.
                //       화면 키 ≈ 0.34m 인데, 예전 코드는 스케일을 빼고 옮겨서 로컬 키 1.71 을 그대로 기록했다(5배).
                //       차량(Tank/CannonCart) 경로는 처음부터 localToWorldMatrix 를 써서 값이 정상이었다.
                smr.BakeMesh(baked, true);
                Vector3[] vertices = baked.vertices;
                if (vertices.Length == 0) throw new InvalidOperationException("EmptyBake");
                // [버그 — 2026-10-13 주석 처리] 스케일(1)을 강제로 넣어 중간 부모 스케일 0.2~0.3 이 빠졌다.
                // Matrix4x4 placement = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                Matrix4x4 placement = smr.transform.localToWorldMatrix;
                foreach (Vector3 v in vertices) points.Add(placement.MultiplyPoint3x4(v));
                return true;
            }
            catch (Exception)
            {
                // 굽기 실패는 측정 정확도만 낮출 뿐 도구를 멈출 이유가 아니다 → 경계 상자로 대체하고 방법 이름에 남긴다.
                AddBoxCorners(smr.bounds, Matrix4x4.identity, points); // smr.bounds 는 이미 월드 좌표 상자다
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        private static void AddBoxCorners(Bounds b, Matrix4x4 toWorld, List<Vector3> points)
        {
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                points.Add(toWorld.MultiplyPoint3x4(corner));
            }
        }

        private static string Combine(string current, string next) =>
            current == "None" || current == next ? next : current.Contains(next) ? current : current + "+" + next;

        /// <summary>
        /// 측정값이 유닛으로서 말이 안 되는 크기인지 판정한다(키 &gt; 1.2m, 반경 &gt; 0.43m, 키 &lt; 0.1m).
        /// </summary>
        /// <param name="m">측정 결과.</param>
        /// <param name="reason">이상하면 사유 토큰(여러 개면 '|' 로 잇는다 — LogRules: 값 안에 ", " 금지).</param>
        /// <returns>이상값이면 true.</returns>
        internal static bool IsImplausible(Measurement m, out string reason)
        {
            reason = null;
            if (m.Height > SuspiciousMaxHeight) reason = "HeightAboveMax";
            if (m.Height < SuspiciousMinHeight) reason = "HeightBelowMin";
            if (m.Radius > SuspiciousMaxRadius) reason = reason == null ? "RadiusAboveHalfTile" : reason + "|RadiusAboveHalfTile";
            return reason != null;
        }

        /// <summary>측정값이 이상하면(IsImplausible) 경고를 남긴다. 표 기록은 막지 않는다.</summary>
        private static void WarnIfImplausible(UnitType type, Measurement m)
        {
            if (!IsImplausible(m, out string reason)) return;
            GameLog.Dev.Warn("Combat", LogClass,
                "측정한 유닛 몸 크기가 비정상 범위다 — 스케일/좌표계 회귀 의심(표에는 기록함)",
                string.Format(CultureInfo.InvariantCulture,
                    "Type={0}, Reason={1}, Height={2:0.###}, Radius={3:0.###}, MaxHeight={4:0.###}, MaxRadius={5:0.###}, MinHeight={6:0.###}, Method={7}",
                    type, reason, m.Height, m.Radius, SuspiciousMaxHeight, SuspiciousMaxRadius, SuspiciousMinHeight, m.Method));
        }

        /// <summary>Red 프리팹 측정값이 Blue 와 5% 넘게 다르면 경고를 남긴다(표에는 Blue 값을 쓴다).</summary>
        private static void WarnIfTeamsDiffer(UnitType type, Measurement blue, GameObject redPrefab)
        {
            if (redPrefab == null) return;
            Measurement red = Measure(redPrefab);
            if (!red.Ok)
            {
                GameLog.Dev.Warn("Combat", LogClass, "Red 프리팹 측정 실패 — 표에는 Blue 값만 사용", $"Type={type}, Reason={red.Failure}");
                return;
            }
            if (Mathf.Abs(red.Height - blue.Height) > blue.Height * TeamMismatchRatio)
                GameLog.Dev.Warn("Combat", LogClass, "Blue/Red 프리팹 키가 5% 넘게 다르다 — 표에는 Blue 값 사용",
                    string.Format(CultureInfo.InvariantCulture, "Type={0}, BlueHeight={1:0.###}, RedHeight={2:0.###}", type, blue.Height, red.Height));
        }

        /// <summary>
        /// 측정 결과를 UnitEffectConfig 의 _recipientFits 에 쓴다. 같은 타입 줄은 측정값만 갱신(manualScale 보존),
        /// 없으면 새 줄(manualScale 0 = 자동)을 추가한다. 다른 줄은 건드리지 않는다.
        /// </summary>
        /// <returns>갱신·추가한 줄 수.</returns>
        private static int WriteTable(UnitEffectConfig config, List<(UnitType Type, Measurement M)> results)
        {
            var serialized = new SerializedObject(config);
            SerializedProperty table = serialized.FindProperty("_recipientFits");
            if (table == null || !table.isArray)
            {
                GameLog.Dev.Warn("Combat", LogClass, "UnitEffectConfig 에 _recipientFits 필드가 없어 저장하지 못했다", "Path=" + ConfigPath);
                return 0;
            }
            int written = 0;
            foreach (var (type, m) in results)
            {
                SerializedProperty row = null;
                for (int i = 0; i < table.arraySize; i++)
                {
                    SerializedProperty candidate = table.GetArrayElementAtIndex(i);
                    if (candidate.FindPropertyRelative("unitType").intValue == (int)type) { row = candidate; break; }
                }
                bool added = row == null;
                if (added)
                {
                    table.arraySize++;
                    row = table.GetArrayElementAtIndex(table.arraySize - 1);
                    row.FindPropertyRelative("unitType").intValue = (int)type;
                    row.FindPropertyRelative("manualScale").floatValue = 0f; // 새 줄은 자동(키 비율)
                }
                row.FindPropertyRelative("radius").floatValue = m.Radius;
                row.FindPropertyRelative("height").floatValue = m.Height;
                row.FindPropertyRelative("footOffset").vector3Value = m.FootOffset;
                float manual = row.FindPropertyRelative("manualScale").floatValue;
                written++;
                GameLog.Dev.Info("Combat", LogClass, added ? "크기 표 줄 추가" : "크기 표 줄 측정값 갱신",
                    string.Format(CultureInfo.InvariantCulture,
                        "Type={0}, Height={1:0.###}, Radius={2:0.###}, FootX={3:0.###}, FootY={4:0.###}, FootZ={5:0.###}, AutoScale={6:0.###}, ManualScale={7:0.###}, Renderers={8}, Method={9}",
                        type, m.Height, m.Radius, m.FootOffset.x, m.FootOffset.y, m.FootOffset.z,
                        m.Height / UnitEffectConfig.RecipientReferenceHeight, manual, m.RendererCount, m.Method));
            }
            serialized.ApplyModifiedProperties(); // Undo 기록 포함
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config); // 이 에셋 하나만 저장한다(씬·다른 에셋 저장 없음)
            config.Initialize(); // 같은 에디터 세션의 조회 캐시도 새 표로 맞춘다
            return written;
        }

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
            && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
