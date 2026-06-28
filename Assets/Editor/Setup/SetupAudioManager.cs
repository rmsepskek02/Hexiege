// ============================================================================
// SetupAudioManager.cs
// Login.unity 씬에 AudioManager 게임오브젝트를 구성하고,
// SoundConfig ScriptableObject 에셋을 생성하는 1회성 에디터 셋업 스크립트.
//
// 실행 방법: Unity 상단 메뉴 → Hexiege/Setup/사운드 - AudioManager 설정
//
// 사전 조건: Assets/_Project/Audio/GameAudioMixer.mixer 가 먼저 존재해야 한다.
//   (AudioMixer는 Unity Editor에서 수동 생성 필요 — API 미지원)
//
// 동작 개요:
//   1) GameAudioMixer.mixer 존재 확인 → 없으면 경고 후 중단
//   2) SoundConfig.asset 생성 (Assets/Resources/Config/) — 이미 있으면 재사용
//   3) Login.unity 열기
//   4) [Audio] GO 생성/재사용 + AudioManager 컴포넌트 부착
//   5) BGM_A, BGM_B AudioSource 자식 생성 (loop=true, playOnAwake=false)
//   6) SFX_Container 빈 Transform 자식 생성
//   7) AudioManager 필드를 SerializedObject로 연결
//   8) LoginBootstrapper._soundConfig 연결
//   9) 씬 저장
//
// Editor 전용 — 빌드에 포함되지 않는다 (Assets/Editor 폴더).
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using Hexiege.Bootstrap;
using Hexiege.Infrastructure;
using Hexiege.Presentation;

namespace HexiegeEditor
{
    /// <summary>
    /// Login.unity 씬에 AudioManager 오브젝트와 SoundConfig 에셋을 자동 구성하는 에디터 도구.
    /// </summary>
    public static class SetupAudioManager
    {
        private const string MixerPath    = "Assets/_Project/Audio/GameAudioMixer.mixer";
        private const string ScenePath    = "Assets/_Project/Scenes/Login.unity";
        private const string ConfigFolder = "Assets/Resources/Config";
        private const string ConfigPath   = "Assets/Resources/Config/SoundConfig.asset";

        [MenuItem("Hexiege/Setup/사운드 - AudioManager 설정")]
        public static void Run()
        {
            // ----------------------------------------------------------------
            // 1) AudioMixer 존재 확인 — 없으면 수동 생성 안내 후 중단.
            // ----------------------------------------------------------------
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                EditorUtility.DisplayDialog(
                    "AudioMixer 없음",
                    $"{MixerPath} 를 먼저 생성해주세요.\n\n" +
                    "Project 창 우클릭 → Create > Audio Mixer\n" +
                    "이름: GameAudioMixer\n" +
                    "BGM / SFX 그룹 추가 후 각 볼륨을 Expose:\n" +
                    "  Master Volume → MasterVolume\n" +
                    "  BGM Volume    → BGMVolume\n" +
                    "  SFX Volume    → SFXVolume",
                    "확인");
                return;
            }

            // ----------------------------------------------------------------
            // 2) SoundConfig.asset 생성 — 이미 있으면 재사용.
            // ----------------------------------------------------------------
            EnsureFolder(ConfigFolder);
            var soundConfig = AssetDatabase.LoadAssetAtPath<SoundConfig>(ConfigPath);
            if (soundConfig == null)
            {
                soundConfig = ScriptableObject.CreateInstance<SoundConfig>();
                AssetDatabase.CreateAsset(soundConfig, ConfigPath);
                Debug.Log($"[SetupAudioManager] SoundConfig.asset 생성: {ConfigPath}");
            }

            // ----------------------------------------------------------------
            // 3) Login.unity 열기.
            // ----------------------------------------------------------------
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("씬 없음",
                    $"{ScenePath} 를 찾을 수 없습니다.\n먼저 Login 씬을 생성해주세요.",
                    "확인");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);

            // ----------------------------------------------------------------
            // 4) [Audio] GO 생성 / 재사용.
            // ----------------------------------------------------------------
            var audioGo = GameObject.Find("[Audio]");
            if (audioGo == null)
                audioGo = new GameObject("[Audio]");

            // AudioManager 컴포넌트 부착 — 이미 있으면 재사용.
            var audioManager = audioGo.GetComponent<AudioManager>();
            if (audioManager == null)
                audioManager = audioGo.AddComponent<AudioManager>();

            // ----------------------------------------------------------------
            // 5) BGM_A / BGM_B AudioSource 자식 생성.
            // ----------------------------------------------------------------
            var bgmA       = GetOrCreateChild(audioGo.transform, "BGM_A");
            var bgmSourceA = bgmA.GetComponent<AudioSource>();
            if (bgmSourceA == null) bgmSourceA = bgmA.gameObject.AddComponent<AudioSource>();
            bgmSourceA.loop         = true;
            bgmSourceA.playOnAwake  = false;

            var bgmB       = GetOrCreateChild(audioGo.transform, "BGM_B");
            var bgmSourceB = bgmB.GetComponent<AudioSource>();
            if (bgmSourceB == null) bgmSourceB = bgmB.gameObject.AddComponent<AudioSource>();
            bgmSourceB.loop         = true;
            bgmSourceB.playOnAwake  = false;

            // ----------------------------------------------------------------
            // 6) SFX_Container 빈 Transform 자식 생성.
            // ----------------------------------------------------------------
            var sfxContainer = GetOrCreateChild(audioGo.transform, "SFX_Container");

            // ----------------------------------------------------------------
            // 7) AudioMixerGroup 로드.
            // ----------------------------------------------------------------
            var bgmGroups = mixer.FindMatchingGroups("BGM");
            var sfxGroups = mixer.FindMatchingGroups("SFX");

            if (bgmGroups.Length == 0 || sfxGroups.Length == 0)
            {
                EditorUtility.DisplayDialog("AudioMixerGroup 없음",
                    "GameAudioMixer에 BGM 또는 SFX 그룹이 없습니다.\n" +
                    "AudioMixer Inspector에서 그룹을 추가해주세요.",
                    "확인");
                return;
            }

            var bgmGroup = bgmGroups[0];
            var sfxGroup = sfxGroups[0];

            // BGM AudioSource에 믹서 그룹 연결.
            bgmSourceA.outputAudioMixerGroup = bgmGroup;
            bgmSourceB.outputAudioMixerGroup = bgmGroup;

            // ----------------------------------------------------------------
            // 8) AudioManager SerializedObject로 필드 연결.
            // ----------------------------------------------------------------
            var amSo = new SerializedObject(audioManager);
            amSo.FindProperty("_audioMixer").objectReferenceValue   = mixer;
            amSo.FindProperty("_bgmGroup").objectReferenceValue     = bgmGroup;
            amSo.FindProperty("_sfxGroup").objectReferenceValue     = sfxGroup;
            amSo.FindProperty("_bgmSourceA").objectReferenceValue   = bgmSourceA;
            amSo.FindProperty("_bgmSourceB").objectReferenceValue   = bgmSourceB;
            amSo.FindProperty("_sfxContainer").objectReferenceValue = sfxContainer.transform;
            amSo.ApplyModifiedPropertiesWithoutUndo();

            // ----------------------------------------------------------------
            // 9) LoginBootstrapper._soundConfig 연결.
            // ----------------------------------------------------------------
            var bootstrapper = Object.FindFirstObjectByType<LoginBootstrapper>();
            if (bootstrapper != null)
            {
                var bsSo = new SerializedObject(bootstrapper);
                bsSo.FindProperty("_soundConfig").objectReferenceValue = soundConfig;
                bsSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[SetupAudioManager] LoginBootstrapper를 찾지 못했습니다. " +
                                 "_soundConfig 연결을 수동으로 해주세요.");
            }

            // ----------------------------------------------------------------
            // 10) 씬 저장.
            // ----------------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupAudioManager] 완료.\n" +
                      $"  [Audio] GO: {audioGo.name}\n" +
                      $"  SoundConfig: {ConfigPath}\n" +
                      "  다음: BGM 클립을 SoundConfig.asset에 수동으로 연결하세요.");

            EditorUtility.DisplayDialog("완료",
                "AudioManager 설정이 완료되었습니다.\n\n" +
                "남은 수동 작업:\n" +
                "SoundConfig.asset에 BGM 클립을 연결해주세요.",
                "확인");
        }

        // --------------------------------------------------------------------
        // 헬퍼
        // --------------------------------------------------------------------

        /// <summary>부모 Transform에서 이름으로 자식을 찾거나 없으면 새로 생성한다.</summary>
        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            var found = parent.Find(childName);
            if (found != null) return found;

            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>폴더가 없으면 생성한다 (중첩 경로 대응).</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts   = path.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
