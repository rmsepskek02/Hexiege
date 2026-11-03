// ============================================================================
// SfxTieringOneTimeSetup.cs
// [1회성 Editor 스크립트 — 2026-11-02 효과음 task 1단계 1-C]
//   계획 원본: Assets/_Project/Docs/_Tasks/2026-11-02/13_39_unit-attack-sfx-tiering/Plan.md
//   (최상단 RM-1 · RM-3, 6-2 「한도 16」, 사용자 승인 2026-11-02 「추천대로 승인」)
//
// 무엇을 하나 (유니티 초급자용):
//   Inspector에서 손으로 하면 실수하기 쉬운 에셋·씬 값 정리 4가지를 메뉴 한 번으로 한다.
//   실행 전 확인 창을 띄우고, 끝나면 단계별 결과를 Console(개발 로그)과 결과 창으로 보여준다.
//
//   ① VFX 프리팹 4개의 AudioSource(소리 재생 부품)를 「끄기」(enabled = false) — Plan RM-1
//        vfx_pistoleer_attack · vfx_streamspirit_attack · vfx_tank_attack · vfx_unit_death
//      왜: 이 부품들은 이펙트가 나올 때 볼륨 설정(믹서)을 거치지 않고 옛 소리를 직접 내던 것이다
//          (사운드 규칙 1 · 17 위반 상태). 옛 소리 파일은 이미 지워져 지금은 무음이지만, 소리는
//          AudioManager로만 나게 하려고 부품을 끈다. 삭제가 아니라 「끄기」인 이유는 WORKFLOW [4] —
//          사용자 테스트 통과 뒤에 컴포넌트를 삭제한다(Inspector 「Remove Component」 또는 별도 정리).
//      주의: vfx_pistoleer_attack 은 Human 방어 타워 발사 이펙트(EffectPreset_HumanAutoTower_Attack)도 같이 쓴다.
//          끄는 것은 AudioSource 뿐이라 파티클(눈에 보이는 이펙트)은 그대로다.
//
//   ② SoundConfig.asset 의 「찾을 수 없음(Missing)」 참조 칸을 비우기 — Plan RM-3
//      대상(작성 시점 확인): BGM 4칸(로그인 · 로비 · 전투 · 게임종료) + Pistoleer 공격음 칸.
//      삭제된 오디오 파일(커밋 323dd454)을 가리키고 있어 이미 무음이다. 칸을 비워도 소리는 바뀌지 않는다.
//      규칙: 「파일이 사라진 참조」만 비운다. 정상 연결된 칸(EagleArcher 등 5개 공격음)은 건드리지 않는다.
//      Pistoleer 항목 자체는 지우지 않는다(나중에 총기 「중」 파일을 넣을 자리).
//
//   ③ Login.unity 의 AudioManager 「_maxConcurrentSfx」(효과음 전체 동시 재생 한도) 8 → 16 — Plan 6-2
//      왜: 코드 기본값을 16으로 바꿔도, 씬에 저장된 값(8)이 코드 기본값보다 우선한다.
//      Login 씬이 열려 있지 않으면 잠깐 추가로 열어 바꾸고 저장한 뒤 다시 닫는다.
//      Login 씬이 이미 열려 있고 저장 안 된 변경이 있으면 이 단계는 건너뛴다(사용자 변경까지 같이 저장되지 않게).
//
//   ④ 옛 EffectPreset 2개의 남은 「_sfxClip · _sfxVolume」 값 지우기
//        EffectPreset_Pistoleer_Attack.asset · EffectPreset_Unit_Death_Common.asset
//      왜: EffectPreset.cs 에서 이 두 칸은 이미 주석 처리됐지만(SOUND_SYSTEM_REFACTOR), 에셋 파일에는 옛 값이
//          글자로 남아 있다(삭제된 소리 파일을 가리킴). 코드에 칸이 없으므로 Inspector · SerializedObject 로는
//          보이지도 지울 수도 없다. 대신 Unity 공식 API AssetDatabase.ForceReserializeAssets 로 에셋을
//          「지금 코드 모양대로 다시 저장」하면 코드에 없는 칸은 저장되지 않는다.
//          실행 뒤 파일을 다시 읽어 「_sfxClip:」 글자가 정말 사라졌는지 확인하고, 남아 있으면 경고 로그를 남긴다.
//      부수 효과: 다시 저장하면서 EffectPreset 에 나중에 추가된 칸(예: 폭 맞춤 · 당김 비율 등)이 에셋에 없었다면
//          지금의 기본값으로 글자가 새로 적힌다. 실행 중 쓰던 값도 원래 그 기본값이었으므로 동작은 바뀌지 않는다.
//
// 다시 실행해도 된다: 각 단계는 "이미 된 상태"면 건너뛰고(변경 0) 그렇다고 로그에 남긴다.
// 이 스크립트가 하지 않는 것: AudioSource 컴포넌트 삭제, SoundConfig 정상 칸 수정, 우선 재생 체크, 다른 씬 수정.
//
// ⚠️ 1회성: 실행 완료 확인 뒤 WORKFLOW [5-2] · [4] 의 비활성화 · 최종 삭제 절차를 따른다(다른 코드의 직접 의존 없음).
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hexiege.Application;   // GameLog
using Hexiege.Presentation;  // AudioManager
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 효과음 task 1-C 의 에셋 · 씬 정리(VFX AudioSource 끄기 · SoundConfig 끊긴 참조 비우기 ·
    /// Login 씬 효과음 한도 16 · 옛 EffectPreset 잔존 SFX 값 지우기)를 한 번에 실행하는 1회성 메뉴 도구.
    /// </summary>
    public static class SfxTieringOneTimeSetup
    {
        private const string MenuPath = "Hexiege/Setup/SFX Tiering - Apply One-time Asset Changes";
        private const string DialogTitle = "SFX Tiering One-time Setup";
        private const string LogSystem = "Audio";
        private const string LogClass = nameof(SfxTieringOneTimeSetup);

        /// <summary>① AudioSource 를 끌 VFX 프리팹 4개(Plan RM-1).</summary>
        private static readonly string[] VfxPrefabPaths =
        {
            "Assets/_Project/Prefabs/VFX/Units/vfx_pistoleer_attack.prefab",
            "Assets/_Project/Prefabs/VFX/Units/vfx_streamspirit_attack.prefab",
            "Assets/_Project/Prefabs/VFX/Units/vfx_tank_attack.prefab",
            "Assets/_Project/Prefabs/VFX/Units/vfx_unit_death.prefab",
        };

        /// <summary>② 끊긴 참조를 비울 소리 설정 에셋(Plan RM-3).</summary>
        private const string SoundConfigPath = "Assets/_Project/Resources/Config/SoundConfig.asset";

        /// <summary>③ AudioManager 가 붙어 있는 씬(사운드 규칙 2 — Login 씬 [Audio] 오브젝트).</summary>
        private const string LoginScenePath = "Assets/_Project/Scenes/Login.unity";

        /// <summary>③ AudioManager 의 전체 한도 필드 이름(private 이라 nameof 를 쓸 수 없어 글자로 적는다).</summary>
        private const string MaxConcurrentSfxField = "_maxConcurrentSfx";

        /// <summary>③ 새 전체 한도(Plan 6-1 D).</summary>
        private const int NewMaxConcurrentSfx = 16;

        /// <summary>④ 잔존 _sfxClip 값을 지울 옛 EffectPreset 2개.</summary>
        private static readonly string[] LegacyPresetPaths =
        {
            "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Pistoleer_Attack.asset",
            "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Unit_Death_Common.asset",
        };

        /// <summary>④ 에셋 파일 안에서 「남은 옛 칸」을 찾는 글자(YAML 키).</summary>
        private const string LegacySfxClipKey = "_sfxClip:";

        /// <summary>
        /// 메뉴: 확인 창 → 4단계 실행 → 결과 창 · 로그.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Play 모드에서는 실행할 수 없습니다. Play를 멈춘 뒤 다시 실행하세요.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog(DialogTitle, BuildConfirmMessage(), "실행", "취소"))
                return;

            var report = new StringBuilder();
            bool anyProblem = false;
            anyProblem |= !DisableVfxAudioSources(report);
            anyProblem |= !ClearMissingSoundConfigReferences(report);
            anyProblem |= !RaiseLoginSceneSfxLimit(report);
            anyProblem |= !ReserializeLegacyPresets(report);
            AssetDatabase.SaveAssets();

            GameLog.Dev.Info(LogSystem, LogClass, "효과음 1회성 정리 메뉴 실행 완료", $"AnyProblem={anyProblem}");
            EditorUtility.DisplayDialog(DialogTitle,
                report + (anyProblem
                    ? "\n⚠️ 일부 단계가 끝나지 않았습니다. Console 경고 로그를 확인하세요."
                    : "\n모든 단계 완료. 자세한 내용은 Console 로그를 확인하세요."),
                "확인");
        }

        /// <summary>확인 창 문구 — 무엇을 바꾸는지 미리 보여준다.</summary>
        private static string BuildConfirmMessage()
        {
            var sb = new StringBuilder();
            sb.AppendLine("아래 4가지를 실행합니다(이미 된 단계는 건너뜀).");
            sb.AppendLine();
            sb.AppendLine("① VFX 프리팹 4개의 AudioSource 끄기(삭제 아님)");
            foreach (string p in VfxPrefabPaths) sb.AppendLine("   · " + Path.GetFileName(p));
            sb.AppendLine("② SoundConfig 의 「Missing」 참조 칸 비우기(정상 칸은 그대로)");
            sb.AppendLine($"③ Login 씬 AudioManager 효과음 전체 한도 → {NewMaxConcurrentSfx}");
            sb.AppendLine("   (Login 씬에 저장 안 된 변경이 있으면 이 단계는 건너뜀)");
            sb.AppendLine("④ 옛 EffectPreset 2개의 남은 _sfxClip 값 지우기(에셋 다시 저장)");
            foreach (string p in LegacyPresetPaths) sb.AppendLine("   · " + Path.GetFileName(p));
            sb.AppendLine();
            sb.Append("진행할까요?");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        //  ① VFX 프리팹 AudioSource 끄기
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 프리팹마다 내용을 열어(LoadPrefabContents) 모든 AudioSource 를 끄고, 바뀐 것이 있으면 저장한다.
        /// </summary>
        /// <returns>모든 프리팹을 처리했으면 true.</returns>
        private static bool DisableVfxAudioSources(StringBuilder report)
        {
            bool ok = true;
            report.AppendLine("① VFX AudioSource 끄기");
            foreach (string path in VfxPrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    GameLog.Dev.Warn(LogSystem, LogClass, "VFX 프리팹을 찾지 못해 건너뛴다", $"Path={path}");
                    report.AppendLine($"   · {Path.GetFileName(path)}: 프리팹 없음 — 건너뜀");
                    ok = false;
                    continue;
                }

                // 프리팹 편집 전용 임시 씬에 내용을 불러온다(Unity 권장 방식 — 열린 씬에 영향 없음).
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
                    int turnedOff = 0;
                    foreach (AudioSource src in sources)
                    {
                        if (!src.enabled) continue;
                        src.enabled = false; // 꺼진 AudioSource 는 Play On Awake 여도 소리를 내지 않는다.
                        turnedOff++;
                    }
                    if (turnedOff > 0)
                        PrefabUtility.SaveAsPrefabAsset(root, path);

                    GameLog.Dev.Info(LogSystem, LogClass, "VFX 프리팹 AudioSource 끄기",
                                     $"Path={path}, AudioSourceCount={sources.Length}, TurnedOff={turnedOff}");
                    report.AppendLine($"   · {Path.GetFileName(path)}: AudioSource {sources.Length}개 중 {turnedOff}개 끔"
                                      + (turnedOff == 0 ? "(이미 꺼져 있음)" : ""));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────
        //  ② SoundConfig 끊긴 참조 비우기
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// SoundConfig 의 모든 오브젝트 참조 칸을 돌며 「Missing」(파일이 사라진 참조)만 None 으로 비운다.
        /// Missing 판정: 참조 번호(instanceID)는 남아 있는데 실제 에셋을 불러오면 null 인 칸.
        /// </summary>
        /// <returns>에셋을 찾았으면 true.</returns>
        private static bool ClearMissingSoundConfigReferences(StringBuilder report)
        {
            report.AppendLine("② SoundConfig Missing 참조 비우기");
            UnityEngine.Object config = AssetDatabase.LoadMainAssetAtPath(SoundConfigPath);
            if (config == null)
            {
                GameLog.Dev.Warn(LogSystem, LogClass, "SoundConfig 에셋을 찾지 못해 건너뛴다", $"Path={SoundConfigPath}");
                report.AppendLine("   · SoundConfig 없음 — 건너뜀");
                return false;
            }

            var so = new SerializedObject(config);
            var cleared = new List<string>();
            SerializedProperty it = so.GetIterator();
            // enterChildren=true 로 목록(_unitSoundEntries 등) 안의 칸까지 모두 돈다.
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (it.objectReferenceValue != null || it.objectReferenceInstanceIDValue == 0) continue;

                cleared.Add(it.propertyPath);
                it.objectReferenceValue = null;
                it.objectReferenceInstanceIDValue = 0; // 남은 참조 번호까지 0 으로 — 파일에 {fileID: 0} 으로 저장된다.
            }

            if (cleared.Count > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
            }

            // 값에 ", " 를 쓰지 않도록 칸 목록은 "|" 로 잇는다(LogRules 1.4).
            GameLog.Dev.Info(LogSystem, LogClass, "SoundConfig Missing 참조 비우기",
                             $"Path={SoundConfigPath}, ClearedCount={cleared.Count}, Cleared={string.Join("|", cleared)}");
            report.AppendLine(cleared.Count == 0
                ? "   · Missing 칸 없음(이미 비어 있음)"
                : $"   · {cleared.Count}칸 비움: {string.Join(", ", cleared)}");
            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        //  ③ Login 씬 AudioManager 전체 한도 16
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Login 씬의 AudioManager 「_maxConcurrentSfx」 를 16 으로 바꾸고 씬을 저장한다.
        /// 씬이 열려 있지 않으면 추가(Additive)로 열었다가 저장 뒤 닫는다.
        /// </summary>
        /// <returns>값을 16 으로 맞췄으면(이미 16 포함) true.</returns>
        private static bool RaiseLoginSceneSfxLimit(StringBuilder report)
        {
            report.AppendLine($"③ Login 씬 효과음 전체 한도 → {NewMaxConcurrentSfx}");
            Scene scene = SceneManager.GetSceneByPath(LoginScenePath);
            bool openedHere = false;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty)
                {
                    GameLog.Dev.Warn(LogSystem, LogClass, "Login 씬에 저장 안 된 변경이 있어 한도 변경을 건너뛴다",
                                     $"Scene={LoginScenePath}");
                    report.AppendLine("   · Login 씬에 저장 안 된 변경이 있어 건너뜀 — 씬을 저장(또는 되돌림)한 뒤 메뉴를 다시 실행하세요");
                    return false;
                }
            }
            else
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LoginScenePath) == null)
                {
                    GameLog.Dev.Warn(LogSystem, LogClass, "Login 씬 파일을 찾지 못해 건너뛴다", $"Scene={LoginScenePath}");
                    report.AppendLine("   · Login 씬 파일 없음 — 건너뜀");
                    return false;
                }
                scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                return ApplySfxLimit(scene, report);
            }
            finally
            {
                // 이 메뉴가 연 씬이면 다시 닫아, 사용자가 보던 씬 구성을 그대로 둔다.
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>씬 안의 AudioManager 를 찾아 한도 값을 바꾸고 저장한다.</summary>
        private static bool ApplySfxLimit(Scene scene, StringBuilder report)
        {
            var managers = new List<AudioManager>();
            foreach (GameObject root in scene.GetRootGameObjects())
                managers.AddRange(root.GetComponentsInChildren<AudioManager>(true));

            if (managers.Count != 1)
            {
                GameLog.Dev.Warn(LogSystem, LogClass, "Login 씬의 AudioManager 가 정확히 1개가 아니라 건너뛴다",
                                 $"Scene={LoginScenePath}, Count={managers.Count}");
                report.AppendLine($"   · AudioManager {managers.Count}개 발견(1개여야 함) — 건너뜀");
                return false;
            }

            var so = new SerializedObject(managers[0]);
            SerializedProperty prop = so.FindProperty(MaxConcurrentSfxField);
            if (prop == null || prop.propertyType != SerializedPropertyType.Integer)
            {
                GameLog.Dev.Warn(LogSystem, LogClass, "AudioManager 에서 한도 칸을 찾지 못해 건너뛴다",
                                 $"Field={MaxConcurrentSfxField}");
                report.AppendLine("   · 한도 칸을 찾지 못함 — 건너뜀");
                return false;
            }

            int oldValue = prop.intValue;
            if (oldValue != NewMaxConcurrentSfx)
            {
                prop.intValue = NewMaxConcurrentSfx;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    GameLog.Dev.Warn(LogSystem, LogClass, "Login 씬 저장에 실패했다", $"Scene={LoginScenePath}");
                    report.AppendLine("   · 씬 저장 실패");
                    return false;
                }
            }

            GameLog.Dev.Info(LogSystem, LogClass, "Login 씬 AudioManager 효과음 전체 한도 설정",
                             $"Scene={LoginScenePath}, Old={oldValue}, New={NewMaxConcurrentSfx}");
            report.AppendLine(oldValue == NewMaxConcurrentSfx
                ? $"   · 이미 {NewMaxConcurrentSfx}"
                : $"   · {oldValue} → {NewMaxConcurrentSfx} 저장");
            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        //  ④ 옛 EffectPreset 잔존 _sfxClip 지우기
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 잔존 「_sfxClip:」 글자가 있는 프리셋만 지금 코드 모양으로 다시 저장하고, 저장 뒤 글자가 사라졌는지 확인한다.
        /// </summary>
        /// <returns>모든 대상에서 「_sfxClip:」 이 없으면 true.</returns>
        private static bool ReserializeLegacyPresets(StringBuilder report)
        {
            report.AppendLine("④ 옛 EffectPreset 잔존 _sfxClip 지우기");
            var targets = new List<string>();
            bool ok = true;
            foreach (string path in LegacyPresetPaths)
            {
                if (!TryReadAssetText(path, out string text))
                {
                    report.AppendLine($"   · {Path.GetFileName(path)}: 파일 없음 — 건너뜀");
                    ok = false;
                    continue;
                }
                if (text.Contains(LegacySfxClipKey))
                    targets.Add(path);
                else
                    report.AppendLine($"   · {Path.GetFileName(path)}: 이미 없음");
            }

            if (targets.Count > 0)
                AssetDatabase.ForceReserializeAssets(targets, ForceReserializeAssetsOptions.ReserializeAssets);

            foreach (string path in targets)
            {
                bool removed = TryReadAssetText(path, out string after) && !after.Contains(LegacySfxClipKey);
                if (removed)
                {
                    GameLog.Dev.Info(LogSystem, LogClass, "옛 EffectPreset 잔존 SFX 값 제거", $"Path={path}");
                    report.AppendLine($"   · {Path.GetFileName(path)}: 제거됨");
                }
                else
                {
                    // 다시 저장해도 남아 있으면 Unity 가 옛 칸을 보존한 것 — 사람이 확인해야 한다.
                    GameLog.Dev.Warn(LogSystem, LogClass,
                                     "다시 저장했는데도 옛 _sfxClip 값이 남아 있다 — 수동 확인 필요", $"Path={path}");
                    report.AppendLine($"   · {Path.GetFileName(path)}: ⚠️ 아직 남아 있음(수동 확인 필요)");
                    ok = false;
                }
            }
            return ok;
        }

        /// <summary>
        /// "Assets/..." 에셋 파일을 글자로 읽는다(결과 확인용). 실패하면 경고 로그를 남기고 false.
        /// </summary>
        private static bool TryReadAssetText(string assetPath, out string text)
        {
            text = null;
            try
            {
                // 이 네임스페이스 안에서 그냥 Application 이라고 쓰면 Hexiege.Application 으로 해석되므로 완전히 적는다.
                string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                text = File.ReadAllText(Path.Combine(projectRoot, assetPath));
                return true;
            }
            catch (Exception e)
            {
                GameLog.Dev.Warn(LogSystem, LogClass, "에셋 파일을 읽지 못했다",
                                 $"Path={assetPath}, Error={e.GetType().Name}");
                return false;
            }
        }
    }
}
