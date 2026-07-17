// ============================================================================
// CreateSpecialAttackConfigAsset.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  상단 메뉴  Hexiege > Setup > Create SpecialAttackConfig Asset (Game)  실행. │
// │  (Game 씬이 열려 있지 않아도 됨 — 없으면 스크립트가 자동으로 열고 저장한다.)  │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가:
//   1) Assets/_Project/Resources/Config/SpecialAttackConfig.asset 을 생성한다.
//      (폴더가 없으면 단계별로 만든다. 이미 에셋이 있으면 그대로 재사용 — 멱등.)
//      값은 SO 코드 기본값을 그대로 둔다:
//        · 도끼병 휩쓸기: SweepReach 1.0 / SweepArcHalfAngle 120
//        · TorrentSpirit 파도: WaveWidth 3 / WaveLength 3 / WaveTravelTime 0.5 / WaveHeal 10
//      ※ 기존 .asset이 이미 있으면 파도 필드는 SO 필드 초기값이 그대로 반영된다(Unity 직렬화가
//        누락 필드에 C# 초기값을 적용). Inspector에서 값을 조정하려면 이 에셋을 선택해 편집한다.
//   2) Game 씬에서 GameBootstrapper 를 찾아 private [SerializeField]
//      _specialAttackConfig 슬롯에 위 에셋을 SerializedObject 로 연결한다.
//   3) Console 에 생성/재사용·연결 결과·경로를 리포트한다.
//
// 왜 필요한가:
//   SpecialAttackConfig(SO) 의 .asset 실물이 없으면 GameBootstrapper 는 코드
//   폴백(1.0/120)으로만 동작한다. Inspector 에서 휩쓸기 수치를 튜닝하려면 .asset
//   생성 + GameBootstrapper 연결이 필요하다.
//
// 멱등성:
//   여러 번 실행해도 안전하다. 에셋이 이미 있으면 재사용하고, 슬롯이 이미 같은
//   에셋을 가리키면 재연결은 무해(no-op)하다.
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Bootstrap;
using Hexiege.Infrastructure;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// SpecialAttackConfig(.asset)를 생성하고 GameBootstrapper 에 연결하는 1회성 에디터 스크립트.
    /// </summary>
    public static class CreateSpecialAttackConfigAsset
    {
        // 에셋을 만들 최종 경로. Resources 하위이므로 런타임 Resources.Load 도 가능하나,
        // 여기서는 GameBootstrapper Inspector 참조 연결이 주 목적이다.
        private const string ConfigFolder = "Assets/_Project/Resources/Config";
        private const string AssetPath = ConfigFolder + "/SpecialAttackConfig.asset";

        // GameBootstrapper 가 배치된 씬. 열려 있지 않으면 이 씬을 열어 처리한다.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // GameBootstrapper 의 private [SerializeField] 필드명(정확히 일치해야 한다).
        private const string BootstrapperFieldName = "_specialAttackConfig";

        /// <summary>
        /// 메뉴 진입점. 에셋을 (없으면) 생성하고 GameBootstrapper 에 연결한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Create SpecialAttackConfig Asset (Game)")]
        public static void Run()
        {
            // ── 1) 에셋 확보(없으면 생성, 있으면 재사용) ──────────────────────
            SpecialAttackConfig config = EnsureConfigAsset(out bool created);
            if (config == null)
            {
                Debug.LogError("[Setup] SpecialAttackConfig 에셋을 확보하지 못했습니다. 중단합니다.");
                return;
            }

            // ── 2) GameBootstrapper 확보(열린 씬 우선, 없으면 Game 씬을 연다) ──
            bool sceneWasOpenedByThis = false;
            GameBootstrapper bootstrapper = FindBootstrapperInOpenScenes();
            if (bootstrapper == null)
            {
                bootstrapper = OpenGameSceneAndFindBootstrapper(out sceneWasOpenedByThis);
            }

            if (bootstrapper == null)
            {
                // 에셋은 만들었으나 연결 대상이 없다 — 부분 성공. 경고로 명확히 알린다.
                Debug.LogWarning(
                    $"[Setup] SpecialAttackConfig 에셋은 {(created ? "생성" : "재사용")}했으나, " +
                    $"GameBootstrapper 를 씬에서 찾지 못해 연결하지 못했습니다.\n" +
                    $"  에셋 경로: {AssetPath}\n" +
                    $"  해결: Game 씬을 열고 다시 실행하거나, Inspector 에서 수동으로 연결하세요.");
                return;
            }

            // ── 3) 슬롯 연결(SerializedObject) ────────────────────────────────
            bool wired = WireBootstrapper(bootstrapper, config, out bool alreadyLinked);

            // ── 4) Dirty / 씬 저장 처리 ──────────────────────────────────────
            Scene scene = bootstrapper.gameObject.scene;
            if (wired)
            {
                EditorUtility.SetDirty(bootstrapper);
                EditorSceneManager.MarkSceneDirty(scene);

                // 이 스크립트가 직접 연 씬이면 자동 저장까지 마무리한다(사용자가 Ctrl+S 놓치지 않게).
                if (sceneWasOpenedByThis)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }

            // ── 5) 결과 리포트 ───────────────────────────────────────────────
            string assetState = created ? "생성" : "재사용";
            string linkState = alreadyLinked ? "이미 연결됨(변경 없음)"
                                             : (wired ? "연결 완료" : "연결 실패");
            string saveState = sceneWasOpenedByThis
                ? (wired ? "씬 자동 저장 완료" : "씬 저장 생략")
                : "열린 씬 dirty 표시 — Ctrl+S 로 저장하세요";

            Debug.Log(
                $"[Setup] SpecialAttackConfig 셋업 완료.\n" +
                $"  · 에셋: {assetState}  ({AssetPath})\n" +
                $"  · GameBootstrapper.{BootstrapperFieldName}: {linkState}\n" +
                $"  · 씬: {saveState}");

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        // ====================================================================
        // 에셋 확보
        // ====================================================================

        /// <summary>
        /// 지정 경로에 SpecialAttackConfig 에셋을 확보한다. 없으면 폴더까지 만들어 생성하고,
        /// 이미 있으면 로드해 재사용한다. <paramref name="created"/>는 이번에 생성했는지 여부.
        /// </summary>
        private static SpecialAttackConfig EnsureConfigAsset(out bool created)
        {
            created = false;

            // 이미 존재하면 그대로 재사용(멱등).
            var existing = AssetDatabase.LoadAssetAtPath<SpecialAttackConfig>(AssetPath);
            if (existing != null)
            {
                return existing;
            }

            // 대상 폴더가 없으면 단계별로 생성한다.
            EnsureFolderExists(ConfigFolder);

            // 새 인스턴스 생성 후 .asset 으로 저장. 값은 SO 코드 기본값 그대로.
            var instance = ScriptableObject.CreateInstance<SpecialAttackConfig>();
            AssetDatabase.CreateAsset(instance, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);
            created = true;

            // 저장 후 다시 로드해 안정적인 에셋 참조를 반환(직렬화 참조 일관성).
            return AssetDatabase.LoadAssetAtPath<SpecialAttackConfig>(AssetPath);
        }

        /// <summary>
        /// "Assets/A/B/C" 형태의 폴더 경로를 단계별로 확인·생성한다(AssetDatabase 추적).
        /// </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            // "Assets" 부터 시작해 하위 폴더를 하나씩 만든다.
            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        // ====================================================================
        // GameBootstrapper 탐색 / 씬 열기
        // ====================================================================

        /// <summary>
        /// 현재 열려 있는 씬(들)에서 GameBootstrapper 를 찾는다(비활성 포함). 없으면 null.
        /// </summary>
        private static GameBootstrapper FindBootstrapperInOpenScenes()
        {
            return Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Game 씬을 (단독 모드로) 열어 GameBootstrapper 를 찾는다.
        /// 성공 시 <paramref name="opened"/>=true 로 알려 호출부가 자동 저장하도록 한다.
        /// </summary>
        private static GameBootstrapper OpenGameSceneAndFindBootstrapper(out bool opened)
        {
            opened = false;

            // Game 씬 에셋이 실제로 있는지 먼저 확인.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                Debug.LogWarning($"[Setup] Game 씬을 찾지 못했습니다: {GameScenePath}");
                return null;
            }

            // 저장되지 않은 현재 씬 변경이 있으면 사용자에게 저장 여부를 물어본다(데이터 손실 방지).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Setup] 씬 열기가 취소되었습니다(저장 대화 취소). 연결을 건너뜁니다.");
                return null;
            }

            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            opened = true;
            return FindBootstrapperInOpenScenes();
        }

        // ====================================================================
        // 슬롯 연결
        // ====================================================================

        /// <summary>
        /// GameBootstrapper 의 private [SerializeField] _specialAttackConfig 슬롯에
        /// 에셋을 연결한다. 이미 같은 에셋을 가리키면 변경하지 않는다(멱등).
        /// 반환값: 연결 성공 여부. <paramref name="alreadyLinked"/>는 이미 동일 참조였는지.
        /// </summary>
        private static bool WireBootstrapper(
            GameBootstrapper bootstrapper, SpecialAttackConfig config, out bool alreadyLinked)
        {
            alreadyLinked = false;

            var so = new SerializedObject(bootstrapper);
            SerializedProperty prop = so.FindProperty(BootstrapperFieldName);
            if (prop == null)
            {
                Debug.LogError(
                    $"[Setup] GameBootstrapper 에 '{BootstrapperFieldName}' 필드가 없습니다. " +
                    $"필드명이 바뀌었는지 확인하세요.");
                return false;
            }

            // 이미 동일 에셋이면 아무 것도 하지 않는다(멱등).
            if (prop.objectReferenceValue == config)
            {
                alreadyLinked = true;
                return true;
            }

            prop.objectReferenceValue = config;
            so.ApplyModifiedProperties();
            return true;
        }
    }
}
