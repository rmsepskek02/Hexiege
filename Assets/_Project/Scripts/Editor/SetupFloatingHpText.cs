// ============================================================================
// SetupFloatingHpText.cs
// FloatingHpText 시스템을 씬에 자동으로 설정하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/FloatingHpText 설정
//
// 수행 작업:
//   [1] FloatingHpText 프리팹 생성 및 저장
//         - Assets/_Project/Prefabs/UI/FloatingHpText.prefab
//         - RectTransform (120×40) + CanvasGroup
//         - 자식: Text (TextMeshProUGUI) — Maplestory Light SDF, 크기 28, 흰색, 중앙 정렬
//         - FloatingHpText 스크립트 추가 및 _text, _canvasGroup Inspector 연결
//
//   [2] 씬에 FloatingHpTextSpawner GameObject 배치
//         - 이름: "FloatingHpTextSpawner"
//         - FloatingHpTextSpawner 스크립트 추가
//         - 이미 존재하면 재사용 (중복 생성 방지)
//
//   [3] GameBootstrapper Inspector 슬롯 자동 연결
//         - _floatingHpTextSpawner → 씬의 FloatingHpTextSpawner
//         - _floatingHpTextPrefab  → 생성된 FloatingHpText 프리팹
//         - _uiCanvas              → 씬에서 찾은 Canvas (이미 연결된 경우 스킵)
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

        // 씬에 생성할 FloatingHpTextSpawner 오브젝트 이름
        private const string SpawnerObjName = "FloatingHpTextSpawner";

        // TextMeshProUGUI 설정값
        private const float  TextFontSize   = 28f;
        private const string FontAssetPath  = "Assets/_Project/Fonts/Maplestory Light SDF.asset";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            // [1] FloatingHpText 프리팹 생성
            FloatingHpText prefab = CreateOrLoadPrefab();
            if (prefab == null)
            {
                Debug.LogError("[SetupFloatingHpText] 프리팹 생성/로드 실패. 작업을 중단합니다.");
                return;
            }

            // [2] 씬에 FloatingHpTextSpawner GameObject 배치
            FloatingHpTextSpawner spawner = GetOrCreateSpawner();
            if (spawner == null)
            {
                Debug.LogError("[SetupFloatingHpText] FloatingHpTextSpawner 생성 실패. 작업을 중단합니다.");
                return;
            }

            // [3] GameBootstrapper에 세 슬롯 연결
            int connected = WireGameBootstrapper(spawner, prefab);

            // 씬 변경 표시
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[SetupFloatingHpText] 완료. GameBootstrapper 슬롯 {connected}개 연결됨. 씬을 저장하세요 (Ctrl+S).");
        }

        // ====================================================================
        // [1] FloatingHpText 프리팹 생성 또는 기존 프리팹 로드
        // ====================================================================

        /// <summary>
        /// FloatingHpText 프리팹을 생성하거나, 이미 존재하면 기존 프리팹을 반환합니다.
        ///
        /// 프리팹 구조:
        ///   FloatingHpText (RectTransform 120×40 + CanvasGroup + FloatingHpText 스크립트)
        ///     └─ Text (TextMeshProUGUI — Maplestory Light SDF, 크기28, 흰색, Center)
        /// </summary>
        private static FloatingHpText CreateOrLoadPrefab()
        {
            // 이미 프리팹이 존재하면 재사용
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                Debug.Log($"[SetupFloatingHpText] 기존 프리팹 사용: {PrefabPath}");
                var existingComp = existing.GetComponent<FloatingHpText>();
                if (existingComp == null)
                    Debug.LogWarning("[SetupFloatingHpText] 기존 프리팹에 FloatingHpText 컴포넌트가 없습니다. 수동으로 확인하세요.");
                return existingComp;
            }

            // 폴더가 없으면 생성
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                // "Assets/_Project/Prefabs" 하위에 "UI" 폴더 생성
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
                Debug.Log($"[SetupFloatingHpText] 폴더 생성: {PrefabFolder}");
            }

            // ── 루트 오브젝트 구성 ──────────────────────────────────────────
            var root = new GameObject("FloatingHpText");

            // RectTransform 기본값: sizeDelta (120, 40), 앵커 중앙
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 40f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            // CanvasGroup: blocksRaycasts = false (FloatingHpText.Awake()에서도 설정하지만 프리팹 기본값으로도 설정)
            var canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts  = false;
            canvasGroup.interactable    = false;

            // FloatingHpText 스크립트
            var floatingHpText = root.AddComponent<FloatingHpText>();

            // ── 자식 Text 오브젝트 구성 ──────────────────────────────────────
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(root.transform, false);

            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin  = Vector2.zero;
            textRt.anchorMax  = Vector2.one;
            textRt.offsetMin  = Vector2.zero;
            textRt.offsetMax  = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize        = TextFontSize;
            tmp.color           = Color.white;
            tmp.alignment       = TextAlignmentOptions.Center;
            tmp.raycastTarget   = false;   // 텍스트 영역이 터치 이벤트를 가로채지 않도록
            tmp.text            = "150";   // 에디터 미리보기용 기본값

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
            // 프리팹 저장 전, SerializedObject를 통해 _text / _canvasGroup 연결
            var so = new SerializedObject(floatingHpText);
            so.FindProperty("_text").objectReferenceValue         = tmp;
            so.FindProperty("_canvasGroup").objectReferenceValue  = canvasGroup;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── 프리팹 저장 ──────────────────────────────────────────────────
            bool success;
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out success);
            Object.DestroyImmediate(root); // 임시 씬 오브젝트 정리

            if (!success || savedPrefab == null)
            {
                Debug.LogError($"[SetupFloatingHpText] 프리팹 저장 실패: {PrefabPath}");
                return null;
            }

            Debug.Log($"[SetupFloatingHpText] 프리팹 생성 완료: {PrefabPath}");
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
            // 씬에 이미 있으면 재사용
            var existing = Object.FindAnyObjectByType<FloatingHpTextSpawner>();
            if (existing != null)
            {
                Debug.Log($"[SetupFloatingHpText] 기존 FloatingHpTextSpawner 재사용: {existing.gameObject.name}");
                return existing;
            }

            // 새 GameObject 생성
            var go = new GameObject(SpawnerObjName);
            var spawner = go.AddComponent<FloatingHpTextSpawner>();
            Undo.RegisterCreatedObjectUndo(go, "Create FloatingHpTextSpawner");

            Debug.Log($"[SetupFloatingHpText] FloatingHpTextSpawner 생성: '{SpawnerObjName}'");
            return spawner;
        }

        // ====================================================================
        // [3] GameBootstrapper Inspector 슬롯 연결
        // ====================================================================

        /// <summary>
        /// 씬의 GameBootstrapper에서 Floating HP Text 관련 세 SerializedField를 연결합니다.
        ///   - _floatingHpTextSpawner  → spawner
        ///   - _floatingHpTextPrefab   → prefab
        ///   - _uiCanvas               → 씬에서 찾은 Canvas (이미 연결 시 스킵)
        /// </summary>
        /// <returns>실제로 연결된 슬롯 수</returns>
        private static int WireGameBootstrapper(FloatingHpTextSpawner spawner, FloatingHpText prefab)
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
            if (spawnerProp != null && spawnerProp.objectReferenceValue == null)
            {
                spawnerProp.objectReferenceValue = spawner;
                count++;
                Debug.Log("[SetupFloatingHpText] GameBootstrapper._floatingHpTextSpawner 연결 완료.");
            }
            else if (spawnerProp?.objectReferenceValue != null)
            {
                Debug.Log("[SetupFloatingHpText] GameBootstrapper._floatingHpTextSpawner 이미 연결됨 — 스킵.");
            }

            // _floatingHpTextPrefab
            var prefabProp = so.FindProperty("_floatingHpTextPrefab");
            if (prefabProp != null && prefabProp.objectReferenceValue == null)
            {
                prefabProp.objectReferenceValue = prefab;
                count++;
                Debug.Log("[SetupFloatingHpText] GameBootstrapper._floatingHpTextPrefab 연결 완료.");
            }
            else if (prefabProp?.objectReferenceValue != null)
            {
                Debug.Log("[SetupFloatingHpText] GameBootstrapper._floatingHpTextPrefab 이미 연결됨 — 스킵.");
            }

            // _uiCanvas — 씬에서 Canvas를 찾아 연결 (이미 연결된 경우 스킵)
            var canvasProp = so.FindProperty("_uiCanvas");
            if (canvasProp != null && canvasProp.objectReferenceValue == null)
            {
                var canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    canvasProp.objectReferenceValue = canvas;
                    count++;
                    Debug.Log($"[SetupFloatingHpText] GameBootstrapper._uiCanvas 연결 완료: '{canvas.gameObject.name}'");
                }
                else
                {
                    Debug.LogWarning("[SetupFloatingHpText] 씬에서 Canvas를 찾을 수 없습니다. _uiCanvas를 수동으로 연결하세요.");
                }
            }
            else if (canvasProp?.objectReferenceValue != null)
            {
                Debug.Log("[SetupFloatingHpText] GameBootstrapper._uiCanvas 이미 연결됨 — 스킵.");
            }

            so.ApplyModifiedProperties();
            return count;
        }
    }
}
#endif
