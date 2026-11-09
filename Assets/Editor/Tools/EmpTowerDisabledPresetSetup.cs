// ============================================================================
// EmpTowerDisabledPresetSetup.cs
// [1회성 에디터 스크립트 — 2026-11-04 EMP Task 커밋 4(자산 활성화)]
//   계획 원본: Assets/_Project/Docs/_Tasks/2026-11-04/03_54_emp-tower-disable/Plan.md
//   (3-5 「종족별 프리셋 칸 P1」 · 4절 커밋 4 · 7-1 사용자 결정 Q9)
//
// 무엇을 하나 (유니티 초급자용):
//   EMP를 맞은 적 방어탑 위에 띄우는 「무력화 상태 VFX(V6)」가 실제로 보이려면 두 가지 데이터가 필요하다.
//     ① EffectPreset 자산 3개 — 종족(인간 · 정령 · 초월)마다 하나씩. 셋 다 같은 프리팹 vfx_emp_disabled를 담고,
//        나중에 종족별 탑 높이에 맞춰 「높이(Local Position Offset의 Y)」와 「크기(Scale Multiplier)」만 다르게 한다.
//          Assets/_Project/Resources/Config/EffectPresets/EffectPreset_EmpDisabled_Human.asset
//          Assets/_Project/Resources/Config/EffectPresets/EffectPreset_EmpDisabled_Spirit.asset
//          Assets/_Project/Resources/Config/EffectPresets/EffectPreset_EmpDisabled_Transcendence.asset
//     ② BuildingEffectConfig.asset 의 「방어탑 무력화 상태 VFX (EMP)」 목록(_towerDisabledPresets)에
//        race 0 / 1 / 2 → 위 프리셋 3줄.
//   Inspector에서 손으로 하면 종족 · 프리셋을 엇갈려 넣기 쉬워서 WORKFLOW [5-2] 규칙대로 메뉴 한 번으로 한다.
//   .asset 파일을 글자로 직접 쓰지 않고 Unity API(AssetDatabase · SerializedObject)로 만들어
//   GUID · 직렬화 형식이 Unity가 만든 것과 똑같다.
//
// 실행 방법:
//   Unity 상단 메뉴 → Hexiege → Setup → Skills → Create EMP Tower Disabled Presets
//   Console에 "[EmpPresetSetup] 완료" 줄이 나오면 성공.
//   문제가 있으면 "[EmpPresetSetup] 중단" 줄과 이유가 나오며, 그 경우 아무 파일도 바꾸지 않는다
//   (모든 검사를 쓰기 전에 끝낸다).
//
// 넣는 값 (새로 만들 때 — Plan 3-5 「측정 결과 전에는 3종 모두 배율 1 · 보정 0」):
//   프리팹 = vfx_emp_disabled.prefab(GUID까지 확인), 위치 보정 (0,0,0), 회전 보정 (0,0,0), 배율 (1,1,1),
//   전진 거리 · 시간 0, 시전자 사망 정지 끔, 폭 맞춤 끔(상한은 기본값 3), 카메라 당김 0.
//   ※ 실제 재생(EffectManager.StartTowerStatusVfx)은 위치 보정의 Y와 배율만 읽는다. 나머지 칸은
//     "다른 기능이 우연히 켜지지 않게" 꺼 둔 값이다.
//
// 여러 번 실행해도 결과가 같다(멱등) — 그리고 "나중에 맞춘 높이 · 크기"를 되돌리지 않는다:
//   - 프리셋이 없으면 위 값으로 새로 만든다.
//   - 프리셋이 이미 있으면 높이 · 크기(위치 보정 · 배율)는 그대로 두고, 나머지 고정 칸만 위 값으로 다시 쓴다.
//     왜: 탑 높이 측정 결과가 오면 사용자가 Inspector에서 높이 · 크기를 바꾼다(Plan R-11). 이 메뉴를 다시 눌렀다고
//         그 값이 0 · 1로 되돌아가면 안 된다(SupportSkillLoadoutSetup 의 R-9와 같은 종류의 함정).
//     지금 들어 있는 높이 · 크기 값은 실행 로그에 그대로 찍힌다.
//   - 설정 목록에 그 종족 줄이 없으면 추가하고, 이미 이 프리셋을 가리키면 그대로 둔다.
//
// 이런 경우에는 멈춘다(아무것도 쓰지 않음):
//   - Play 모드 중 / 스크립트 컴파일이 덜 돼 칸 이름을 못 찾을 때
//   - vfx_emp_disabled 프리팹이 없거나 GUID가 기대값과 다를 때(같은 이름의 다른 파일일 수 있음)
//   - 만들 자리에 EffectPreset 이 아닌 파일이 있거나, 같은 이름의 프리셋이 다른 프리팹을 담고 있을 때
//   - 설정 목록에 같은 종족 줄이 2개 이상이거나, 그 종족 줄이 이미 "다른" 프리셋을 가리킬 때
//     (사람이 일부러 넣은 값을 덮어쓰지 않는다 — 그 경우 Inspector에서 정리한 뒤 다시 실행)
//
// 무엇을 바꾸지 않나:
//   - 다른 EffectPreset · BuildingEffectConfig 의 다른 목록(건물 이펙트 · 탑 공격 프로필) · 코드.
//   - 스킬 자산(Skill_Human_Emp 등)은 이 메뉴가 아니라 SupportSkillLoadoutSetup 메뉴가 바꾼다.
//
// 왜 SupportSkillLoadoutSetup 에 합치지 않았나(Plan 4절 커밋 4 지시 「더 단순하고 안전한 쪽」):
//   그 도구는 지원 스킬 작업이 모두 끝날 때까지 남아 있고(사용자 결정 — 포격 Task 10-5), 실행할 때마다
//   스킬 15개 값을 표 값으로 "다시 덮어쓴다". 프리셋을 거기 넣으면 나중에 맞춘 탑 높이 · 크기도
//   재실행 때마다 같이 되돌아갈 위험이 생기고, 스킬 데이터 도구가 화면 연출 설정까지 건드리게 된다.
//   그래서 프리셋은 이 별도 메뉴로 한 번만 만들고, 이 파일은 확인 뒤 바로 지울 수 있게 했다.
//
// 사용 후 정리:
//   실행 · 확인(V6가 탑 위에 보임)이 끝나면 이 파일(과 Unity가 만든 .meta)을 지운다
//   (WORKFLOW [5-2] · [4] 절차 — 삭제는 사용자 승인 후). 런타임 코드 · 회귀 검사는 이 파일을 참조하지 않는다.
//
// 위치: Assets/Editor/Tools (Assembly-CSharp-Editor — 새 1회성 메뉴 배치 관례). 네임스페이스 Hexiege.EditorTools.
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// EMP 무력화 상태 VFX 프리셋 3개 생성 + BuildingEffectConfig 종족 줄 연결을 수행하는 1회성 에디터 메뉴.
    /// </summary>
    public static class EmpTowerDisabledPresetSetup
    {
        // ── 경로 상수 ────────────────────────────────────────────────────────
        private const string MenuPath = "Hexiege/Setup/Skills/Create EMP Tower Disabled Presets";
        private const string LogPrefix = "[EmpPresetSetup]";

        // 프리셋을 두는 폴더(기존 EffectPreset 자산들과 같은 곳).
        private const string PresetFolder = "Assets/_Project/Resources/Config/EffectPresets";
        private const string ConfigPath = "Assets/_Project/Resources/Config/BuildingEffectConfig.asset";

        // V6 프리팹과 그 GUID(vfx_emp_disabled.prefab.meta 에서 확인한 값).
        //   경로만 맞고 GUID가 다르면 "같은 이름의 다른 파일"이라 연결하지 않는다.
        private const string DisabledVfxPath = "Assets/_Project/Prefabs/VFX/Skills/vfx_emp_disabled.prefab";
        private const string DisabledVfxGuid = "32e68fa52406d994fbdbfd8f8d4fcfbf";

        // ── EffectPreset 의 직렬화 칸 이름(private 필드 이름과 같아야 한다) ──
        //   SerializedObject로 private 칸에 값을 넣기 때문에 이름이 틀리면 값이 안 들어간다.
        //   그래서 쓰기 전에 모든 이름이 실제로 있는지 먼저 확인한다(ValidateFieldNames).
        private const string P_VfxPrefab = "_vfxPrefab";
        private const string P_LocalPositionOffset = "_localPositionOffset";
        private const string P_EulerRotationOffset = "_eulerRotationOffset";
        private const string P_ScaleMultiplier = "_scaleMultiplier";
        private const string P_ForwardTravelDistance = "_forwardTravelDistance";
        private const string P_ForwardTravelDuration = "_forwardTravelDuration";
        private const string P_StopWhenCasterDies = "_stopWhenCasterDies";
        private const string P_WidthFitEnabled = "_widthFitEnabled";
        private const string P_WidthFitMaxCount = "_widthFitMaxCount";
        private const string P_ViewPullRadiusRatio = "_viewPullRadiusRatio";

        private static readonly string[] PresetFieldNames =
        {
            P_VfxPrefab, P_LocalPositionOffset, P_EulerRotationOffset, P_ScaleMultiplier,
            P_ForwardTravelDistance, P_ForwardTravelDuration, P_StopWhenCasterDies,
            P_WidthFitEnabled, P_WidthFitMaxCount, P_ViewPullRadiusRatio
        };

        // ── BuildingEffectConfig 의 직렬화 칸 이름 ──
        private const string C_TowerDisabledPresets = "_towerDisabledPresets";
        private const string C_EntryRace = "race";     // TowerDisabledPresetEntry.race
        private const string C_EntryPreset = "preset"; // TowerDisabledPresetEntry.preset

        // 만들 종족 순서(race 0 · 1 · 2). 파일 이름 뒤쪽 = RaceId 이름 그대로(Human · Spirit · Transcendence).
        private static readonly RaceId[] Races = { RaceId.Human, RaceId.Spirit, RaceId.Transcendence };

        // 종족 하나의 처리 결과(로그 · 확인용).
        private sealed class RaceWork
        {
            public RaceId Race;
            public string PresetPath;
            public EffectPreset Preset;  // 생성 또는 불러온 프리셋.
            public bool PresetCreated;   // 이번에 새로 만들었나.
            public string RowAction;     // 설정 줄 처리: "추가" · "빈 칸 채움" · "그대로".
        }

        // ====================================================================
        // 메뉴 진입점
        // ====================================================================

        /// <summary>
        /// 메뉴 실행: ① 사전 검사(쓰기 전 전부) → ② 프리셋 3개 생성/고정 칸 갱신 → ③ 설정 줄 연결 → ④ 다시 읽어 확인.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LogPrefix} 중단 — Play 모드에서는 실행할 수 없다. Play를 끄고 다시 실행하세요.");
                return;
            }

            // ① 사전 검사 — 하나라도 실패하면 아무것도 쓰지 않는다.
            if (!ValidateFieldNames()) return;

            GameObject disabledVfx = LoadDisabledVfx();
            if (disabledVfx == null) return;

            if (!AssetDatabase.IsValidFolder(PresetFolder))
            {
                Debug.LogError($"{LogPrefix} 중단 — 프리셋 폴더가 없다: {PresetFolder}");
                return;
            }

            BuildingEffectConfig config = AssetDatabase.LoadAssetAtPath<BuildingEffectConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"{LogPrefix} 중단 — BuildingEffectConfig 자산을 찾지 못했다: {ConfigPath}");
                return;
            }

            if (!ValidatePresetPaths(disabledVfx)) return;
            if (!ValidateConfigRows(config)) return;

            // ② 프리셋 생성 또는 고정 칸 갱신.
            var works = new List<RaceWork>();
            foreach (RaceId race in Races)
            {
                var work = new RaceWork { Race = race, PresetPath = PresetPathOf(race) };
                work.Preset = CreateOrLoadPreset(work.PresetPath, out work.PresetCreated);
                if (work.Preset == null)
                {
                    Debug.LogError($"{LogPrefix} 중단 — 프리셋을 만들지 못했다: {work.PresetPath} " +
                                   "(설정 목록은 아직 바꾸지 않았다 — 원인을 고친 뒤 다시 실행하면 된다)");
                    return;
                }
                WritePresetValues(work.Preset, disabledVfx, work.PresetCreated);
                works.Add(work);
            }

            // ③ 설정 목록에 종족 줄 연결.
            if (!LinkConfigRows(config, works)) return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ④ 다시 읽어 확인(런타임과 같은 GetTowerDisabled 경로로 읽는다).
            if (VerifyResult(config, works, disabledVfx))
            {
                Debug.Log($"{LogPrefix} 완료 — 무력화 상태 VFX 프리셋 3종족 연결 " +
                          "(다음: SupportSkillLoadoutSetup 메뉴로 EMP 스킬 자산을 사용 가능하게 바꾼다).");
            }
            else
            {
                Debug.LogError($"{LogPrefix} 확인 실패 — 자산은 저장됐지만 다시 읽은 값이 기대와 다르다. 위 오류 줄을 확인하세요.");
            }
        }

        // ====================================================================
        // ① 사전 검사
        // ====================================================================

        // EffectPreset · BuildingEffectConfig 에 이 스크립트가 쓰려는 칸이 모두 있는지 확인한다(코드가 최신으로 컴파일됐는지).
        //   임시 인스턴스를 만들어 칸 이름을 찾아 보고 바로 지운다(자산으로 저장되지 않는다).
        private static bool ValidateFieldNames()
        {
            EffectPreset presetProbe = ScriptableObject.CreateInstance<EffectPreset>();
            BuildingEffectConfig configProbe = ScriptableObject.CreateInstance<BuildingEffectConfig>();
            try
            {
                var pso = new SerializedObject(presetProbe);
                foreach (string field in PresetFieldNames)
                {
                    if (pso.FindProperty(field) == null)
                    {
                        Debug.LogError($"{LogPrefix} 중단 — EffectPreset 에 '{field}' 칸이 없다. " +
                                       "스크립트 컴파일이 끝났는지(Console 오류 0) 확인한 뒤 다시 실행하세요.");
                        return false;
                    }
                }

                var cso = new SerializedObject(configProbe);
                SerializedProperty list = cso.FindProperty(C_TowerDisabledPresets);
                if (list == null || !list.isArray)
                {
                    Debug.LogError($"{LogPrefix} 중단 — BuildingEffectConfig 에 '{C_TowerDisabledPresets}' 목록이 없다 " +
                                   "(EMP 커밋 3 코드가 컴파일됐는지 확인하세요).");
                    return false;
                }

                // 줄 안의 칸 이름(race · preset)은 줄이 있어야 확인할 수 있어, 임시 인스턴스에 한 줄을 넣어 본다.
                list.arraySize = 1;
                SerializedProperty row = list.GetArrayElementAtIndex(0);
                if (row.FindPropertyRelative(C_EntryRace) == null || row.FindPropertyRelative(C_EntryPreset) == null)
                {
                    Debug.LogError($"{LogPrefix} 중단 — '{C_TowerDisabledPresets}' 줄에 '{C_EntryRace}' · '{C_EntryPreset}' 칸이 없다.");
                    return false;
                }
                return true;
            }
            finally
            {
                Object.DestroyImmediate(presetProbe);
                Object.DestroyImmediate(configProbe);
            }
        }

        // V6 프리팹을 경로로 읽고, GUID까지 기대값과 같은지 확인한다.
        private static GameObject LoadDisabledVfx()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DisabledVfxPath);
            if (prefab == null)
            {
                Debug.LogError($"{LogPrefix} 중단 — 무력화 VFX 프리팹을 찾지 못했다: {DisabledVfxPath}");
                return null;
            }

            string guid = AssetDatabase.AssetPathToGUID(DisabledVfxPath);
            if (guid != DisabledVfxGuid)
            {
                Debug.LogError($"{LogPrefix} 중단 — 무력화 VFX 프리팹의 GUID가 기대값과 다르다 " +
                               $"(기대 {DisabledVfxGuid}, 실제 {guid}). 다른 파일일 수 있어 연결하지 않는다.");
                return null;
            }
            return prefab;
        }

        // 만들 자리에 "EffectPreset 이 아닌 파일" 또는 "다른 프리팹을 담은 같은 이름 프리셋"이 있으면 멈춘다.
        private static bool ValidatePresetPaths(GameObject disabledVfx)
        {
            foreach (RaceId race in Races)
            {
                string path = PresetPathOf(race);
                Object existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing == null) continue; // 없음 → 새로 만든다.

                if (!(existing is EffectPreset preset))
                {
                    Debug.LogError($"{LogPrefix} 중단 — {path} 에 EffectPreset 이 아닌 자산({existing.GetType().Name})이 있다. 덮어쓰지 않는다.");
                    return false;
                }

                // 프리팹 칸이 비어 있으면(만들다 멈춘 경우 등) 채우면 되므로 통과. 다른 프리팹이면 남이 쓰는 자산일 수 있어 멈춘다.
                if (preset.VfxPrefab != null && preset.VfxPrefab != disabledVfx)
                {
                    Debug.LogError($"{LogPrefix} 중단 — {path} 가 이미 다른 프리팹({AssetDatabase.GetAssetPath(preset.VfxPrefab)})을 담고 있다. " +
                                   "덮어쓰지 않는다.");
                    return false;
                }
            }
            return true;
        }

        // 설정 목록의 종족 줄이 "비었거나 · 이 프리셋을 가리키거나" 둘 중 하나인지 확인한다.
        //   같은 종족 줄이 2개 이상이면 런타임(GetTowerDisabled)은 첫 줄만 쓰므로 어느 줄을 고칠지 모호하다 → 멈춘다.
        private static bool ValidateConfigRows(BuildingEffectConfig config)
        {
            var so = new SerializedObject(config);
            SerializedProperty list = so.FindProperty(C_TowerDisabledPresets);
            if (list == null || !list.isArray)
            {
                Debug.LogError($"{LogPrefix} 중단 — BuildingEffectConfig.asset 에서 '{C_TowerDisabledPresets}' 목록을 읽지 못했다.");
                return false;
            }

            foreach (RaceId race in Races)
            {
                string expectedPath = PresetPathOf(race);
                int count = 0;
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty row = list.GetArrayElementAtIndex(i);
                    if (row.FindPropertyRelative(C_EntryRace).intValue != (int)race) continue;
                    count++;

                    Object current = row.FindPropertyRelative(C_EntryPreset).objectReferenceValue;
                    if (current != null && AssetDatabase.GetAssetPath(current) != expectedPath)
                    {
                        Debug.LogError($"{LogPrefix} 중단 — race {(int)race} ({race}) 줄이 이미 다른 프리셋을 가리킨다: " +
                                       $"{AssetDatabase.GetAssetPath(current)}. 덮어쓰지 않는다(Inspector에서 정리 후 다시 실행).");
                        return false;
                    }
                }

                if (count > 1)
                {
                    Debug.LogError($"{LogPrefix} 중단 — race {(int)race} ({race}) 줄이 {count}개다. 런타임은 첫 줄만 쓰므로 " +
                                   "Inspector에서 한 줄만 남긴 뒤 다시 실행하세요.");
                    return false;
                }
            }
            return true;
        }

        // ====================================================================
        // ② 프리셋 생성 · 값 쓰기
        // ====================================================================

        private static string PresetPathOf(RaceId race)
        {
            return $"{PresetFolder}/EffectPreset_EmpDisabled_{race}.asset";
        }

        // 프리셋이 있으면 그대로 불러오고(GUID 유지 — 멱등), 없으면 새로 만든다.
        private static EffectPreset CreateOrLoadPreset(string path, out bool wasCreated)
        {
            EffectPreset preset = AssetDatabase.LoadAssetAtPath<EffectPreset>(path);
            if (preset != null)
            {
                wasCreated = false;
                return preset;
            }

            preset = ScriptableObject.CreateInstance<EffectPreset>();
            AssetDatabase.CreateAsset(preset, path);
            wasCreated = true;
            return AssetDatabase.LoadAssetAtPath<EffectPreset>(path);
        }

        // 프리셋 칸에 값을 쓴다(SerializedObject — Inspector에서 입력한 것과 같은 효과).
        //   새로 만든 프리셋: 높이 보정 0 · 배율 1 까지 모두 쓴다.
        //   이미 있던 프리셋: 높이 · 크기(위치 보정 · 배율)는 건드리지 않는다 — 측정 뒤 맞춘 값을 지키기 위해서다.
        private static void WritePresetValues(EffectPreset preset, GameObject disabledVfx, bool isNew)
        {
            var so = new SerializedObject(preset);

            if (isNew)
            {
                so.FindProperty(P_LocalPositionOffset).vector3Value = Vector3.zero;
                so.FindProperty(P_ScaleMultiplier).vector3Value = Vector3.one;
            }

            // 고정 칸 — 매번 같은 값으로 다시 쓴다(멱등).
            so.FindProperty(P_VfxPrefab).objectReferenceValue = disabledVfx;
            so.FindProperty(P_EulerRotationOffset).vector3Value = Vector3.zero; // 재생 코드가 읽지 않지만 0으로 둔다.
            so.FindProperty(P_ForwardTravelDistance).floatValue = 0f;           // 전진 이동 없음.
            so.FindProperty(P_ForwardTravelDuration).floatValue = 0f;
            so.FindProperty(P_StopWhenCasterDies).boolValue = false;            // 시전자 귀속 이펙트가 아니다(탑 상태).
            so.FindProperty(P_WidthFitEnabled).boolValue = false;               // 유닛 몸통 폭 맞춤 기능 끔.
            so.FindProperty(P_WidthFitMaxCount).intValue = EffectPreset.DefaultWidthFitMaxCount; // 꺼져 있어 쓰이지 않는 기본값.
            so.FindProperty(P_ViewPullRadiusRatio).floatValue = 0f;             // 카메라 쪽 당김 끔.

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset); // 저장 대상으로 표시(빠지면 SaveAssets가 파일에 쓰지 않는다).
        }

        // ====================================================================
        // ③ 설정 목록 연결
        // ====================================================================

        // 종족마다: 줄이 없으면 추가, 줄의 프리셋 칸이 비었으면 채움, 이미 이 프리셋이면 그대로.
        //   (다른 프리셋을 가리키는 줄 · 중복 줄은 ValidateConfigRows 가 이미 걸러냈다.)
        private static bool LinkConfigRows(BuildingEffectConfig config, List<RaceWork> works)
        {
            var so = new SerializedObject(config);
            SerializedProperty list = so.FindProperty(C_TowerDisabledPresets);
            if (list == null || !list.isArray)
            {
                Debug.LogError($"{LogPrefix} 중단 — '{C_TowerDisabledPresets}' 목록을 읽지 못했다. " +
                               "(프리셋은 이미 만들어졌다 — 다시 실행하면 된다)");
                return false;
            }

            foreach (RaceWork work in works)
            {
                SerializedProperty row = FindRaceRow(list, work.Race);
                if (row == null)
                {
                    int index = list.arraySize;
                    list.arraySize = index + 1;
                    row = list.GetArrayElementAtIndex(index);
                    row.FindPropertyRelative(C_EntryRace).intValue = (int)work.Race;
                    row.FindPropertyRelative(C_EntryPreset).objectReferenceValue = work.Preset;
                    work.RowAction = "줄 추가";
                    continue;
                }

                SerializedProperty presetProp = row.FindPropertyRelative(C_EntryPreset);
                if (presetProp.objectReferenceValue == null)
                {
                    presetProp.objectReferenceValue = work.Preset;
                    work.RowAction = "빈 칸 채움";
                }
                else
                {
                    work.RowAction = "이미 연결됨(그대로)";
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return true;
        }

        // 목록에서 race 값이 같은 첫 줄(없으면 null).
        private static SerializedProperty FindRaceRow(SerializedProperty list, RaceId race)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty row = list.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative(C_EntryRace).intValue == (int)race) return row;
            }
            return null;
        }

        // ====================================================================
        // ④ 다시 읽어 확인
        // ====================================================================

        // 런타임이 쓰는 GetTowerDisabled(race) 로 다시 읽어, 종족마다 이번 프리셋이 나오고 고정 칸이 기대값인지 본다.
        private static bool VerifyResult(BuildingEffectConfig config, List<RaceWork> works, GameObject disabledVfx)
        {
            bool ok = true;
            foreach (RaceWork work in works)
            {
                EffectPreset got = config.GetTowerDisabled(work.Race);
                bool linked = got != null && got == work.Preset;
                bool fixedOk = got != null
                               && got.VfxPrefab == disabledVfx
                               && got.EulerRotationOffset == Vector3.zero
                               && Mathf.Approximately(got.ForwardTravelDistance, 0f)
                               && Mathf.Approximately(got.ForwardTravelDuration, 0f)
                               && !got.StopWhenCasterDies
                               && !got.WidthFitEnabled
                               && Mathf.Approximately(got.ViewPullRadiusRatio, 0f);

                if (!linked || !fixedOk)
                {
                    Debug.LogError($"{LogPrefix} race {(int)work.Race} ({work.Race}) 확인 실패 — " +
                                   $"연결 {(linked ? "맞음" : "틀림")}, 고정 칸 {(fixedOk ? "맞음" : "틀림")} ({work.PresetPath})");
                    ok = false;
                    continue;
                }

                // 높이 · 크기는 재생이 실제로 읽는 값이라 그대로 보여 준다(측정 뒤 값 조정의 출발점).
                Vector3 offset = got.LocalPositionOffset;
                Vector3 scale = got.ScaleMultiplier;
                string guid = AssetDatabase.AssetPathToGUID(work.PresetPath);
                Debug.Log($"{LogPrefix} race {(int)work.Race} ({work.Race}) → {got.name} " +
                          $"[{(work.PresetCreated ? "새로 만듦" : "이미 있음 — 높이 · 크기 유지")}, 설정 {work.RowAction}] " +
                          $"프리팹 {got.VfxPrefab.name}, 높이 보정 Y={offset.y:0.###}, 배율 ({scale.x:0.###}, {scale.y:0.###}, {scale.z:0.###}), GUID {guid}");
            }
            return ok;
        }
    }
}
