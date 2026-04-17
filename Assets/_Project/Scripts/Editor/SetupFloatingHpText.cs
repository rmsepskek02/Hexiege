// ============================================================================
// SetupFloatingHpText.cs
// FloatingHpText 시스템을 씬에 자동으로 설정하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/FloatingHpText 설정
//
// 수행 작업:
//   [1] FloatingHpText 프리팹 재생성
//         - Assets/_Project/Prefabs/UI/FloatingHpText.prefab
//         - 일반 GameObject (Transform, World Space TMP)
//         - 자식: Text (TextMeshPro 3D) — Maplestory Light SDF, 크기 28, 흰색, 중앙 정렬
//         - FloatingHpText 스크립트 추가 및 _text Inspector 연결
//
//   [2] 씬에 FloatingHpTextSpawner GameObject 배치
//         - 이름: "FloatingHpTextSpawner"
//         - FloatingHpTextSpawner 스크립트 추가
//         - 이미 존재하면 재사용 (중복 생성 방지)
//
//   [3] 씬에 FloatingTexts 컨테이너 오브젝트 배치
//         - 이름: "FloatingTexts"
//         - 부유 텍스트 오브젝트들의 부모 역할 (씬 정리 목적)
//         - 이미 존재하면 재사용
//
//   [4] GameBootstrapper Inspector 슬롯 자동 연결
//         - _floatingHpTextSpawner  → 씬의 FloatingHpTextSpawner
//         - _floatingHpTextPrefab   → 생성된 FloatingHpText 프리팹
//         - _floatingTextContainer  → 씬의 FloatingTexts 오브젝트
//
// Editor 전용 — 빌드에 포함되지 않음.
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Hexiege.Presentation;
using Hexiege.Bootstrap;

namespace Hexiege.Editor
{
    public static class SetupFloatingHpText
    {
        private const string MenuPath = "Hexiege/Setup/FloatingHpText 설정";

        // 프리팹 저장 경로
        private const string PrefabFolder   = "Assets/_Project/Prefabs/UI";
        private const string PrefabPath     = "Assets/_Project/Prefabs/UI/FloatingHpText.prefab";

        // 씬 오브젝트 이름
        private const string SpawnerObjName   = "FloatingHpTextSpawner";
        private const string ContainerObjName = "FloatingTexts";

        // TextMeshPro 설정값
        private const float  TextFontSize  = 28f;
        private const string FontAssetPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            // [1] FloatingHpText 프리팹 재생성 (World Space TMP 구조)
            FloatingHpText prefab = CreatePrefab();
            if (prefab == null)
            {
                Debug.LogError("[SetupFloatingHpText] 프리팹 생성 실패. 작업을 중단합니다.");
                return;
            }

            // [2] 씬에 FloatingHpTextSpawner GameObject 배치
            FloatingHpTextSpawner spawner = GetOrCreateSpawner();
            if (spawner == null)
            {
                Debug.LogError("[SetupFloatingHpText] FloatingHpTextSpawner 생성 실패. 작업을 중단합니다.");
                return;
            }

            // [3] 씬에 FloatingTexts 컨테이너 오브젝트 배치
            Transform container = GetOrCreateContainer();
            if (container == null)
            {
                Debug.LogError("[SetupFloatingHpText] FloatingTexts 컨테이너 생성 실패. 작업을 중단합니다.");
                return;
            }

            // [4] GameBootstrapper 슬롯 자동 연결
            int connected = WireGameBootstrapper(spawner, prefab, container);

            // 씬 변경 표시
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[SetupFloatingHpText] 완료. GameBootstrapper 슬롯 {connected}개 연결됨. 씬을 저장하세요 (Ctrl+S).");
        }

        // ====================================================================
        // [1] FloatingHpText 프리팹 재생성 (World Space TMP)
        // ====================================================================

        /// <summary>
        /// FloatingHpText 프리팹을 World Space TMP 구조로 새로 생성합니다.
        /// 기존 프리팹이 있으면 덮어씁니다 (구버전 UGUI 구조 대체).
        ///
        /// 프리팹 구조:
        ///   FloatingHpText (Transform + FloatingHpText 스크립트)
        ///     └─ Text (TextMeshPro 3D — Maplestory Light SDF, 크기 28, 흰색, Center)
        /// </summary>
        private static FloatingHpText CreatePrefab()
        {
            // 폴더가 없으면 생성
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
                Debug.Log($"[SetupFloatingHpText] 폴더 생성: {PrefabFolder}");
            }

            // ── 루트 오브젝트 구성 ──────────────────────────────────────────
            // World Space TMP는 일반 GameObject — RectTransform, Canvas 불필요
            var root = new GameObject("FloatingHpText");

            // FloatingHpText 스크립트
            var floatingHpText = root.AddComponent<FloatingHpText>();

            // ── 자식 Text 오브젝트 구성 ──────────────────────────────────────
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(root.transform, false);

            // TextMeshPro (3D) — World Space 텍스트 렌더링
            var tmp = textObj.AddComponent<TextMeshPro>();
            tmp.fontSize    = TextFontSize;
            tmp.color       = Color.white;
            tmp.alignment   = TextAlignmentOptions.Center;
            tmp.text        = "150";   // 에디터 미리보기용 기본값

            // 폰트 어셋 연결
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset != null)
            {
                tmp.font = fontAsset;
                Debug.Log($"[SetupFloatingHpText] 폰트 연결: {FontAssetPath}");
            }
            else
            {
                Debug.LogWarning($"[SetupFloatingHpText] 폰트를 찾을 수 없습니다: {FontAssetPath}. 기본 폰트로 진행합니다.");
            }

            // ── FloatingHpText SerializedField 연결 ──────────────────────────
            // _text: TextMeshPro (3D) 연결 (_canvasGroup은 제거됨)
            var so = new SerializedObject(floatingHpText);
            so.FindProperty("_text").objectReferenceValue = tmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── 프리팹 저장 (기존 파일 덮어쓰기) ───────────────────────────
            bool success;
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out success);
            Object.DestroyImmediate(root); // 임시 씬 오브젝트 정리

            if (!success || savedPrefab == null)
            {
                Debug.LogError($"[SetupFloatingHpText] 프리팹 저장 실패: {PrefabPath}");
                return null;
            }

            Debug.Log($"[SetupFloatingHpText] 프리팹 생성 완료 (World Space TMP): {PrefabPath}");
            return savedPrefab.GetComponent<FloatingHpText>();
        }

        // ====================================================================
        // [2] 씬에 FloatingHpTextSpawner GameObject 배치
        // ====================================================================

        /// <summary>
        /// 씬에서 FloatingHpTextSpawner를 찾거나, 없으면 새 GameObject를 생성합니다.
        /// </summary>
        private static FloatingHpTextSpawner GetOrCreateSpawner()
        {
            var existing = Object.FindAnyObjectByType<FloatingHpTextSpawner>();
            if (existing != null)
            {
                Debug.Log($"[SetupFloatingHpText] 기존 FloatingHpTextSpawner 재사용: {existing.gameObject.name}");
                return existing;
            }

            var go = new GameObject(SpawnerObjName);
            var spawner = go.AddComponent<FloatingHpTextSpawner>();
            Undo.RegisterCreatedObjectUndo(go, "Create FloatingHpTextSpawner");

            Debug.Log($"[SetupFloatingHpText] FloatingHpTextSpawner 생성: '{SpawnerObjName}'");
            return spawner;
        }

        // ====================================================================
        // [3] 씬에 FloatingTexts 컨테이너 오브젝트 배치
        // ====================================================================

        /// <summary>
        /// 부유 텍스트 오브젝트들의 부모 역할을 할 빈 GameObject를 씬에 생성합니다.
        /// 씬 계층 구조 정리 목적으로 사용 — Position (0, 0, 0), Scale (1, 1, 1) 유지 필수.
        /// </summary>
        private static Transform GetOrCreateContainer()
        {
            // 이름으로 씬에서 검색
            var existingObj = GameObject.Find(ContainerObjName);
            if (existingObj != null)
            {
                Debug.Log($"[SetupFloatingHpText] 기존 FloatingTexts 컨테이너 재사용: {existingObj.name}");
                return existingObj.transform;
            }

            var go = new GameObject(ContainerObjName);
            // Position과 Scale을 (0,0,0) / (1,1,1)로 명시 설정
            // Scale이 1이 아니면 자식의 localPosition이 월드 거리와 달라져 riseDistance가 의도와 다르게 동작함
            go.transform.position   = Vector3.zero;
            go.transform.rotation   = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(go, "Create FloatingTexts Container");

            Debug.Log($"[SetupFloatingHpText] FloatingTexts 컨테이너 생성: '{ContainerObjName}'");
            return go.transform;
        }

        // ====================================================================
        // [4] GameBootstrapper Inspector 슬롯 연결
        // ====================================================================

        /// <summary>
        /// 씬의 GameBootstrapper에서 Floating HP Text 관련 SerializedField를 연결합니다.
        ///   - _floatingHpTextSpawner  → spawner
        ///   - _floatingHpTextPrefab   → prefab
        ///   - _floatingTextContainer  → container (FloatingTexts 오브젝트의 Transform)
        /// </summary>
        /// <returns>실제로 연결된 슬롯 수</returns>
        private static int WireGameBootstrapper(
            FloatingHpTextSpawner spawner,
            FloatingHpText prefab,
            Transform container)
        {
            var bootstrapper = Object.FindAnyObjectByType<GameBootstrapper>();
            if (bootstrapper == null)
            {
                Debug.LogWarning("[SetupFloatingHpText] GameBootstrapper를 씬에서 찾을 수 없습니다.");
                return 0;
            }

            var so = new SerializedObject(bootstrapper);
            int count = 0;

            // _floatingHpTextSpawner
            var spawnerProp = so.FindProperty("_floatingHpTextSpawner");
            if (spawnerProp != null)
            {
                spawnerProp.objectReferenceValue = spawner;
                count++;
                Debug.Log("[SetupFloatingHpText] GameBootstrapper._floatingHpTextSpawner 연결 완료.");
            }

            // _floatingHpTextPrefab
            var prefabProp = so.FindProperty("_floatingHpTextPrefab");
            if (prefabProp != null)
            {
                prefabProp.objectReferenceValue = prefab;
                count++;
                Debug.Log("[SetupFloatingHpText] GameBootstrapper._floatingHpTextPrefab 연결 완료.");
            }

            // _floatingTextContainer (Transform) — 새로 추가된 슬롯
            var containerProp = so.FindProperty("_floatingTextContainer");
            if (containerProp != null)
            {
                containerProp.objectReferenceValue = container;
                count++;
                Debug.Log("[SetupFloatingHpText] GameBootstrapper._floatingTextContainer 연결 완료.");
            }
            else
            {
                Debug.LogWarning("[SetupFloatingHpText] GameBootstrapper에 '_floatingTextContainer' 필드를 찾을 수 없습니다. 수동으로 연결하세요.");
            }

            so.ApplyModifiedProperties();
            return count;
        }
    }
}
#endif
