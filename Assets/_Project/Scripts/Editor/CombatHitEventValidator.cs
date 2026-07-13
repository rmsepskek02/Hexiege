// ============================================================================
// CombatHitEventValidator.cs  (1회성 검증 Editor 유틸리티 — 리포트만, 수정 없음)
//
// 목적 (task: 2026-07-09 전투 타격 타이밍 동기화 / Phase 1 축 1):
//   이번 작업에서 타격 시점(HitFrameTimes)의 출처를 "UnitStatsConfig 수동 입력"에서
//   "Attack 애니메이션 클립의 OnAttackHit Animation Event"로 자동화했다(UnitFactory).
//   자동 추출이 신뢰할 수 있으려면, 전 유닛 프리팹의 Attack 클립에 OnAttackHit 이벤트가
//   실제로 찍혀 있어야 한다. 이 스크립트는 그 전수 검사를 수행하여
//   "이벤트가 없거나 개수가 이상한 유닛"을 콘솔 리포트로 특정한다.
//
// 수행 작업(읽기 전용 — 어떤 에셋도 수정하지 않는다):
//   1. Assets/_Project/Prefabs/Units 아래의 Unit_* 프리팹을 모두 찾는다(_Old 폴더 제외).
//   2. 각 프리팹의 Animator에서 "Attack"이 포함된 첫 클립을 찾는다(UnitFactory와 동일 규칙).
//   3. 그 클립의 OnAttackHit Animation Event 개수와 시간(초) 목록을 수집한다.
//   4. UnitStatsConfig.asset에 수동 입력된 hitFrameTimes와 비교한다.
//   5. 전체를 표 형태로 Console에 1회 출력한다(문제 유닛은 경고 로그로 별도 요약).
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Combat/Validate Attack Hit Events
//   실행하면 Console에 표가 출력된다. 어떤 씬/에셋도 저장·변경되지 않는다.
//
// 리포트 해석:
//   - "이벤트=0"  : 클립에 OnAttackHit이 없음 → 자동 추출 실패 → 기존 수동값 폴백으로 동작.
//                   해당 유닛은 애니메이터 창에서 Attack 클립에 OnAttackHit 이벤트를 찍어야 함.
//   - "MATCH"     : 클립 이벤트 시간과 Config 수동값이 (오차 1ms 내) 일치.
//   - "DIFF"      : 클립 이벤트가 있으나 Config 수동값과 다름 → 이제 클립 값이 우선 적용됨(정상).
//                   수동값이 더 이상 무의미하므로 참고용으로만 표시.
//   - "클립없음"   : 이름에 Attack이 포함된 클립 자체가 없음 → 애니메이터 구성 확인 필요.
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
    /// 전 유닛 프리팹의 Attack 클립 OnAttackHit 이벤트를 전수 검사하여 리포트하는 1회성 유틸리티.
    /// 읽기 전용 — 에셋을 수정하지 않는다.
    /// </summary>
    public static class CombatHitEventValidator
    {
        // 검사 대상 프리팹이 위치한 폴더.
        private const string UnitsPrefabFolder = "Assets/_Project/Prefabs/Units";

        // 비교 대상 수동 입력 값이 담긴 ScriptableObject 경로.
        private const string UnitStatsConfigPath = "Assets/_Project/Resources/Config/UnitStatsConfig.asset";

        // Animation Event가 호출하는 함수 이름(타격 프레임). UnitFactory와 반드시 동일해야 함.
        private const string HitFunctionName = "OnAttackHit";

        // float 비교 허용 오차(초). 1ms 이내면 같은 값으로 취급.
        private const float TimeEpsilon = 0.001f;

        [MenuItem("Hexiege/Combat/Validate Attack Hit Events")]
        public static void Validate()
        {
            // ── 1. UnitStatsConfig(수동 입력값) 로드 → UnitType별 hitFrameTimes 사전 구성 ──
            var manualHitTimes = LoadManualHitTimes();

            // ── 2. Unit_* 프리팹 전수 수집 ──
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitsPrefabFolder });
            var prefabPaths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

                // Unit_ 로 시작하는 프리팹만, 그리고 폐기(_Old) 폴더는 제외.
                if (!fileName.StartsWith("Unit_")) continue;
                if (path.Contains("/_Old/")) continue;

                prefabPaths.Add(path);
            }
            // 이름순 정렬 — 리포트 표를 읽기 쉽게.
            prefabPaths.Sort(StringComparer.Ordinal);

            // ── 3. 각 프리팹 검사 + 표 라인 구성 ──
            var sb = new StringBuilder();
            sb.AppendLine("========== [CombatHitEventValidator] Attack Hit Event 전수 검사 ==========");
            sb.AppendLine($"대상 폴더: {UnitsPrefabFolder} / 검사 프리팹 수: {prefabPaths.Count}");
            sb.AppendLine("형식: [상태] 프리팹  |  클립: <클립명>  |  이벤트시간(초): [...]  |  Config수동값: [...]");
            sb.AppendLine("--------------------------------------------------------------------------");

            int okCount = 0;      // 이벤트가 1개 이상 있는 프리팹 수(자동 추출 성공)
            int noEventCount = 0; // 이벤트가 0개인 프리팹 수(폴백 동작)
            int noClipCount = 0;  // Attack 클립 자체가 없는 프리팹 수
            var problems = new List<string>(); // 경고 요약용

            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                string prefabName = prefab != null ? prefab.name : System.IO.Path.GetFileNameWithoutExtension(path);

                // Animator 탐색 (모델이 자식에 있는 구조 대응 — includeInactive=true).
                Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;

                // Attack 클립 + OnAttackHit 이벤트 시간 추출 (UnitFactory.GetHitFrameTimes와 동일 규칙).
                string clipName;
                float[] eventTimes = ExtractHitEventTimes(animator, out clipName);

                // 프리팹 이름 → UnitType 파싱 후 Config 수동값 조회.
                float[] configTimes = null;
                if (TryParseUnitType(prefabName, out UnitType unitType))
                    manualHitTimes.TryGetValue(unitType, out configTimes);

                // 상태 판정.
                string status;
                if (clipName == null)
                {
                    status = "클립없음";
                    noClipCount++;
                    problems.Add($"  - {prefabName}: 이름에 'Attack'이 포함된 클립이 없음 (애니메이터 구성 확인 필요)");
                }
                else if (eventTimes.Length == 0)
                {
                    status = "이벤트=0";
                    noEventCount++;
                    problems.Add($"  - {prefabName}: Attack 클립 '{clipName}'에 {HitFunctionName} 이벤트가 없음 (수동값 폴백으로 동작)");
                }
                else
                {
                    okCount++;
                    // 클립 값과 Config 수동값 비교.
                    status = CompareTimes(eventTimes, configTimes);
                }

                sb.AppendLine(
                    $"[{status}] {prefabName}  |  클립: {(clipName ?? "-")}  " +
                    $"|  이벤트시간(초): {FormatTimes(eventTimes)}  " +
                    $"|  Config수동값: {FormatTimes(configTimes)}");
            }

            sb.AppendLine("--------------------------------------------------------------------------");
            sb.AppendLine($"요약: 이벤트있음(자동추출OK)={okCount}, 이벤트없음(폴백)={noEventCount}, 클립없음={noClipCount}");
            sb.AppendLine("==========================================================================");

            // 표 전체는 정보 로그 1건으로 출력(콘솔 스팸 방지).
            Debug.Log(sb.ToString());

            // 문제 항목이 있으면 경고 로그로 별도 요약(눈에 잘 띄도록).
            if (problems.Count > 0)
            {
                var wb = new StringBuilder();
                wb.AppendLine($"[CombatHitEventValidator] 보정이 필요할 수 있는 프리팹 {problems.Count}건:");
                foreach (string p in problems) wb.AppendLine(p);
                wb.AppendLine("→ 위 프리팹의 Attack 클립에 OnAttackHit Animation Event를 찍으면 타격 타이밍이 자동 동기화됩니다.");
                Debug.LogWarning(wb.ToString());
            }
            else
            {
                Debug.Log("[CombatHitEventValidator] 모든 프리팹이 OnAttackHit 이벤트를 보유합니다. (보정 불필요)");
            }
        }

        /// <summary>
        /// Animator에서 "Attack"이 포함된 첫 클립을 찾아 OnAttackHit 이벤트 시간(초)을 오름차순으로 반환.
        /// UnitFactory.GetHitFrameTimes()와 동일한 규칙을 사용한다(에디터 검증용 미러 구현).
        /// clipName에는 찾은 클립 이름을 담는다(클립 자체가 없으면 null).
        /// </summary>
        private static float[] ExtractHitEventTimes(Animator animator, out string clipName)
        {
            clipName = null;
            if (animator == null || animator.runtimeAnimatorController == null)
                return Array.Empty<float>();

            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || !clip.name.Contains("Attack")) continue;

                clipName = clip.name;

                var times = new List<float>();
                foreach (var evt in clip.events)
                {
                    if (evt.functionName == HitFunctionName)
                        times.Add(evt.time);
                }
                times.Sort();
                return times.ToArray();
            }

            return Array.Empty<float>();
        }

        /// <summary>
        /// UnitStatsConfig.asset을 로드하여 UnitType별 수동 입력 hitFrameTimes 사전을 만든다.
        /// 로드 실패 시 빈 사전을 반환(비교 열이 "-"로 표시됨).
        /// </summary>
        private static Dictionary<UnitType, float[]> LoadManualHitTimes()
        {
            var result = new Dictionary<UnitType, float[]>();
            var config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(UnitStatsConfigPath);
            if (config == null)
            {
                Debug.LogWarning($"[CombatHitEventValidator] UnitStatsConfig를 찾지 못했습니다: {UnitStatsConfigPath} (Config 비교 생략)");
                return result;
            }

            foreach (var entry in config.Stats)
                result[entry.unitType] = entry.hitFrameTimes;

            return result;
        }

        /// <summary>
        /// 프리팹 이름("Unit_&lt;Type&gt;_Blue" / "Unit_&lt;Type&gt;_Red")에서 UnitType을 파싱.
        /// 예: "Unit_EmberSpirit_Blue" → EmberSpirit.
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
        /// 클립 이벤트 시간과 Config 수동값을 비교하여 상태 문자열을 반환.
        /// 길이·각 원소가 오차 내에서 같으면 MATCH, 다르면 DIFF, 비교 대상이 없으면 "Config없음".
        /// </summary>
        private static string CompareTimes(float[] eventTimes, float[] configTimes)
        {
            if (configTimes == null) return "Config없음";
            if (eventTimes.Length != configTimes.Length) return "DIFF";

            for (int i = 0; i < eventTimes.Length; i++)
            {
                if (Mathf.Abs(eventTimes[i] - configTimes[i]) > TimeEpsilon)
                    return "DIFF";
            }
            return "MATCH";
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
