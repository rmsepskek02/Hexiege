using UnityEditor;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// Tracer B1 migration이 만들 변경을 계산만 하는 B0 dry-run 진입점이다.
    ///
    /// 이름에 Setup이 들어가지만 현재 단계에는 실제 적용 메뉴가 없다. 분석 결과의
    /// create/reuse/move 수를 확인한 뒤 별도 승인된 B1에서만 mutation API를 추가한다.
    /// 따라서 이 메뉴는 여러 번 실행해도 prefab, scene, importer를 변경하지 않는다.
    /// </summary>
    public static class SetupUnitVisualRoots
    {
        [MenuItem("Hexiege/Combat/Visual Root/Dry Run Unit Visual Root Migration")]
        public static void DryRun()
        {
            UnitVisualRootAuditReport report = UnitVisualRootReadOnlyAnalyzer.AnalyzeAll();
            report.Log("dry-run", includeMigrationPlan: true);
        }
    }
}
