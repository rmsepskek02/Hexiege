// ============================================================================
// CombatHitEventInjector.cs  (1회성 주입 Editor 유틸리티 — 클립 에셋을 실제로 수정한다)
//
// 목적 (task: 전투 타격 타이밍 동기화 / Phase 1~3 후속):
//   Phase 1에서 타격 시점(HitFrameTimes)의 출처를 "UnitStatsConfig 수동 입력"에서
//   "Attack 애니메이션 클립의 OnAttackHit Animation Event"로 자동화했다(UnitFactory).
//   그런데 CombatHitEventValidator 실기 결과, 대부분의 유닛 Attack 클립에는
//   OnAttackHit 이벤트가 아직 찍혀 있지 않아 자동 추출이 폴백(수동값)으로만 동작한다.
//
//   이 스크립트는 UnitStatsConfig에 이미 입력돼 있는 hitFrameTimes 값을 기준으로,
//   각 유닛 Attack 클립에 OnAttackHit Animation Event를 "한 번에 자동으로 찍어" 넣는다.
//   실행 후에는 클립이 진짜 타격 시점을 갖게 되므로 UnitFactory의 자동 추출이 정상 동작한다.
//
// ★ Validator와의 관계:
//   - Validator(읽기 전용)는 "무엇이 비어 있는지"를 진단한다.
//   - Injector(이 파일)는 "비어 있는 것을 Config 값으로 채운다".
//   - 두 스크립트는 프리팹 수집 / 클립 선택 / UnitType 파싱 / Config 로드 규칙을 완전히 동일하게 사용한다.
//     (규칙이 어긋나면 서로 다른 클립을 대상으로 삼는 사고가 나므로 반드시 동일해야 한다.)
//
// 수행 작업(⚠️ .anim 클립 에셋을 수정하고 저장한다 — 읽기 전용 아님):
//   1. Assets/_Project/Prefabs/Units 아래 Unit_* 프리팹을 모두 찾는다(_Old 폴더 제외).
//   2. 각 프리팹 Animator에서 "Attack"이 포함된 첫 클립을 찾는다(UnitFactory와 동일 규칙).
//      Blue/Red 프리팹이 같은 .anim 클립을 공유하므로, 이미 처리한 클립 에셋은 다시 처리하지 않는다.
//   3. 프리팹명 → UnitType 파싱 → UnitStatsConfig의 hitFrameTimes 조회.
//   4. 아래 분기로 처리한다:
//        (a) 클립에 이미 OnAttackHit 이벤트가 1개 이상 있으면    → 건너뜀("이미있음").
//        (b) Config에 항목이 없거나 hitFrameTimes가 비어 있으면   → 건너뜀("Config값없음(제외)").
//        (c) hitFrameTimes 최대값이 clip.length를 초과하면        → 건너뜀("클립길이초과(수동배치필요)").
//        (d) 그 외                                               → 모든 원소를 OnAttackHit 이벤트로 주입.
//   5. 주입 시 기존 이벤트(다른 함수명 포함)는 절대 삭제하지 않고 "병합"만 한다.
//      병합된 이벤트 배열은 time 오름차순으로 정렬 후 SetAnimationEvents로 기록한다.
//   6. 모든 클립 처리 후 AssetDatabase.SaveAssets()로 한 번에 저장한다.
//      (⚠️ 교훈: EditorUtility.SetDirty(clip) 를 호출하지 않으면 SaveAssets에 반영되지 않는다.)
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Combat/Inject OnAttackHit Events (From Config)
//   실행 후 Console 리포트를 확인하고, 이어서
//   Hexiege/Combat/Validate Attack Hit Events 를 다시 실행해 교차 검증한다.
//
// Editor 전용 — Editor 폴더에 위치하므로 빌드에 포함되지 않는다.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// UnitStatsConfig의 hitFrameTimes 값을 각 유닛 Attack 클립의 OnAttackHit Animation Event로
    /// 한 번에 주입하는 1회성 유틸리티. 기존 이벤트는 삭제하지 않고 병합만 한다.
    /// </summary>
    public static class CombatHitEventInjector
    {
        // 대상 프리팹이 위치한 폴더 (Validator와 동일).
        private const string UnitsPrefabFolder = "Assets/_Project/Prefabs/Units";

        // 주입 기준값이 담긴 ScriptableObject 경로 (Validator와 동일).
        private const string UnitStatsConfigPath = "Assets/_Project/Resources/Config/UnitStatsConfig.asset";

        // 주입할 Animation Event가 호출하는 함수 이름. UnitFactory / Validator와 반드시 동일해야 함.
        private const string HitFunctionName = "OnAttackHit";

        // float 비교 허용 오차(초). 클립 길이 초과 판정 시 이 오차만큼은 허용(경계 반올림 보호).
        private const float TimeEpsilon = 0.001f;

        [MenuItem("Hexiege/Combat/Inject OnAttackHit Events (From Config)")]
        public static void Inject()
        {
            // ── 1. UnitStatsConfig(주입 기준값) 로드 → UnitType별 hitFrameTimes 사전 구성 ──
            var configHitTimes = LoadConfigHitTimes();
            if (configHitTimes == null)
            {
                // Config 자체를 못 찾으면 주입 근거가 없으므로 중단(에셋 변경 없음).
                Debug.LogError($"[CombatHitEventInjector] UnitStatsConfig를 찾지 못해 주입을 중단합니다: {UnitStatsConfigPath}");
                return;
            }

            // ── 2. Unit_* 프리팹 전수 수집 (Validator와 동일 규칙) ──
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitsPrefabFolder });
            var prefabPaths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!fileName.StartsWith("Unit_")) continue; // Unit_ 프리팹만
                if (path.Contains("/_Old/")) continue;        // 폐기 폴더 제외

                prefabPaths.Add(path);
            }
            prefabPaths.Sort(StringComparer.Ordinal); // 리포트를 읽기 쉽게 이름순 정렬

            // ── 3. 리포트 헤더 구성 ──
            var sb = new StringBuilder();
            sb.AppendLine("========== [CombatHitEventInjector] OnAttackHit Event 주입 ==========");
            sb.AppendLine($"대상 폴더: {UnitsPrefabFolder} / 수집 프리팹 수: {prefabPaths.Count}");
            sb.AppendLine("형식: [상태] 프리팹  |  클립: <클립명>(len=길이)  |  처리내용");
            sb.AppendLine("--------------------------------------------------------------------");

            // 상태별 카운트.
            int injectedCount = 0;   // 주입완료
            int alreadyCount = 0;    // 이미있음(건너뜀)
            int noConfigCount = 0;   // Config값없음(제외)
            int overLengthCount = 0; // 클립길이초과(수동배치필요)
            int errorCount = 0;      // 예외로 건너뜀

            // 이미 처리한 클립 에셋(경로)을 기록 — Blue/Red 공유 클립의 중복 처리 방지.
            var processedClipPaths = new HashSet<string>(StringComparer.Ordinal);

            // ── 4. 각 프리팹 처리 ──
            foreach (string prefabPath in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                string prefabName = prefab != null
                    ? prefab.name
                    : System.IO.Path.GetFileNameWithoutExtension(prefabPath);

                try
                {
                    // Animator 탐색 (모델이 자식에 있는 구조 대응 — includeInactive=true).
                    Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;

                    // "Attack" 포함 첫 클립 취득 (UnitFactory / Validator와 동일 규칙).
                    AnimationClip clip = FindAttackClip(animator);
                    if (clip == null)
                    {
                        // 클립 자체가 없으면 주입 대상이 아님(진단은 Validator 몫). 리포트에만 남긴다.
                        sb.AppendLine($"[클립없음] {prefabName}  |  클립: -  |  'Attack' 포함 클립 없음(주입 불가)");
                        continue;
                    }

                    string clipPath = AssetDatabase.GetAssetPath(clip);

                    // 이미 처리한 클립이면 중복 처리 방지(공유 클립 — 예: Blue가 처리 → Red는 스킵).
                    if (!processedClipPaths.Add(clipPath))
                    {
                        sb.AppendLine($"[공유스킵] {prefabName}  |  클립: {clip.name}  |  이미 처리한 공유 클립(중복 방지)");
                        continue;
                    }

                    // (a) 이미 OnAttackHit 이벤트가 있으면 건너뜀.
                    var existingEvents = AnimationUtility.GetAnimationEvents(clip);
                    if (existingEvents.Any(e => e.functionName == HitFunctionName))
                    {
                        alreadyCount++;
                        float[] existingTimes = existingEvents
                            .Where(e => e.functionName == HitFunctionName)
                            .Select(e => e.time).OrderBy(t => t).ToArray();
                        sb.AppendLine(
                            $"[이미있음] {prefabName}  |  클립: {clip.name}(len={clip.length:0.000})  " +
                            $"|  기존 {HitFunctionName} {existingTimes.Length}개 {FormatTimes(existingTimes)} → 건너뜀");
                        continue;
                    }

                    // 프리팹명 → UnitType 파싱 → Config hitFrameTimes 조회.
                    float[] hitTimes = null;
                    if (TryParseUnitType(prefabName, out UnitType unitType))
                        configHitTimes.TryGetValue(unitType, out hitTimes);

                    // (b) Config에 항목이 없거나 배열이 비어 있으면 제외.
                    if (hitTimes == null || hitTimes.Length == 0)
                    {
                        noConfigCount++;
                        sb.AppendLine(
                            $"[Config값없음(제외)] {prefabName}  |  클립: {clip.name}(len={clip.length:0.000})  " +
                            $"|  Config hitFrameTimes 없음/빈 배열 → 주입 안 함(특수 타격 유닛)");
                        continue;
                    }

                    // (c) 배열 최대값이 클립 길이를 초과하면 제외(클램프 금지 — 밸런스 영향).
                    float maxTime = hitTimes.Max();
                    if (maxTime > clip.length + TimeEpsilon)
                    {
                        overLengthCount++;
                        sb.AppendLine(
                            $"[클립길이초과(수동배치필요)] {prefabName}  |  클립: {clip.name}(len={clip.length:0.000})  " +
                            $"|  Config값 {FormatTimes(hitTimes)} 의 최대 {maxTime:0.000}s > 클립 길이 {clip.length:0.000}s → 주입 안 함");
                        continue;
                    }

                    // (d) 정상 → 모든 원소를 OnAttackHit 이벤트로 병합 주입.
                    InjectEvents(clip, existingEvents, hitTimes);
                    injectedCount++;
                    sb.AppendLine(
                        $"[주입완료] {prefabName}  |  클립: {clip.name}(len={clip.length:0.000})  " +
                        $"|  {HitFunctionName} {hitTimes.Length}개 주입 {FormatTimes(hitTimes)}");
                }
                catch (Exception ex)
                {
                    // 어떤 클립에서 예외가 나도 나머지 처리는 계속한다(부분 실패 허용).
                    errorCount++;
                    sb.AppendLine($"[에러] {prefabName}  |  예외 발생으로 건너뜀: {ex.Message}");
                    Debug.LogError($"[CombatHitEventInjector] {prefabName} 처리 중 예외: {ex}");
                }
            }

            // ── 5. 변경 사항 저장 (주입이 1건이라도 있을 때만) ──
            if (injectedCount > 0)
                AssetDatabase.SaveAssets();

            // ── 6. 리포트 요약 출력 ──
            sb.AppendLine("--------------------------------------------------------------------");
            sb.AppendLine(
                $"요약: 주입완료={injectedCount}, 이미있음={alreadyCount}, " +
                $"Config값없음(제외)={noConfigCount}, 클립길이초과={overLengthCount}, 에러={errorCount}");
            sb.AppendLine($"처리한 고유 클립 수: {processedClipPaths.Count}");
            sb.AppendLine("====================================================================");
            Debug.Log(sb.ToString());

            // 교차 검증 안내.
            Debug.Log("[CombatHitEventInjector] 완료. 이제 'Hexiege/Combat/Validate Attack Hit Events'를 다시 실행해 " +
                      "주입 결과(MATCH/이벤트 개수)를 교차 확인하세요.");
        }

        /// <summary>
        /// 기존 이벤트 배열에 OnAttackHit 이벤트들을 병합하여 클립에 기록한다.
        /// 기존 이벤트(다른 함수명 포함)는 절대 삭제하지 않고 유지하며,
        /// 병합 후 time 오름차순으로 정렬하여 SetAnimationEvents로 저장한다.
        /// SetDirty를 반드시 호출해야 SaveAssets 시 실제 파일에 반영된다.
        /// </summary>
        private static void InjectEvents(AnimationClip clip, AnimationEvent[] existingEvents, float[] hitTimes)
        {
            var merged = new List<AnimationEvent>(existingEvents); // 기존 이벤트 보존(복사)

            // Config의 각 타격 시간마다 OnAttackHit 이벤트 하나씩 생성해 추가.
            foreach (float t in hitTimes)
            {
                merged.Add(new AnimationEvent
                {
                    time = t,
                    functionName = HitFunctionName
                });
            }

            // Unity는 이벤트가 time 오름차순으로 정렬돼 있어야 올바르게 재생한다.
            merged.Sort((a, b) => a.time.CompareTo(b.time));

            AnimationUtility.SetAnimationEvents(clip, merged.ToArray());
            EditorUtility.SetDirty(clip); // ⚠️ 없으면 SaveAssets에 반영되지 않음
        }

        /// <summary>
        /// Animator에서 "Attack"이 포함된 첫 클립 에셋을 반환한다.
        /// UnitFactory.GetAttackClipLength / GetHitFrameTimes 와 완전히 동일한 선택 규칙.
        /// 클립이 없거나 Animator가 null이면 null.
        /// </summary>
        private static AnimationClip FindAttackClip(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return null;

            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || !clip.name.Contains("Attack")) continue;
                return clip;
            }
            return null;
        }

        /// <summary>
        /// UnitStatsConfig.asset을 로드하여 UnitType별 hitFrameTimes 사전을 만든다.
        /// 로드 실패 시 null을 반환(호출 측에서 주입 중단).
        /// </summary>
        private static Dictionary<UnitType, float[]> LoadConfigHitTimes()
        {
            var config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(UnitStatsConfigPath);
            if (config == null) return null;

            var result = new Dictionary<UnitType, float[]>();
            foreach (var entry in config.Stats)
                result[entry.unitType] = entry.hitFrameTimes;
            return result;
        }

        /// <summary>
        /// 프리팹 이름("Unit_&lt;Type&gt;_Blue" / "Unit_&lt;Type&gt;_Red")에서 UnitType을 파싱.
        /// 예: "Unit_EmberSpirit_Blue" → EmberSpirit. (Validator와 동일 규칙)
        /// </summary>
        private static bool TryParseUnitType(string prefabName, out UnitType unitType)
        {
            unitType = default;
            if (string.IsNullOrEmpty(prefabName)) return false;

            string s = prefabName;
            if (s.StartsWith("Unit_")) s = s.Substring("Unit_".Length);
            if (s.EndsWith("_Blue")) s = s.Substring(0, s.Length - "_Blue".Length);
            else if (s.EndsWith("_Red")) s = s.Substring(0, s.Length - "_Red".Length);

            return Enum.TryParse(s, out unitType);
        }

        /// <summary>
        /// float 배열을 "[0.200, 0.450]" 형태 문자열로 포맷. null/빈 배열은 "-".
        /// </summary>
        private static string FormatTimes(float[] times)
        {
            if (times == null || times.Length == 0) return "-";
            return "[" + string.Join(", ", times.Select(t => t.ToString("0.000"))) + "]";
        }
    }
}
