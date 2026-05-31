// ====================================================================================
// AssignUnitPortraits.cs
// ------------------------------------------------------------------------------------
// [목적]
//   ProductionPanelUI 의 _buildingUnitMappings 리스트 안에 있는
//   각 UnitPortraitEntry.portrait 슬롯에 스프라이트를 자동으로 할당한다.
//
//   파일명 규칙: {UnitType.ToString().ToLower()}_portrait_{blue|red}.png
//   예) UnitType.Pistoleer → pistoleer_portrait_blue.png / pistoleer_portrait_red.png
//
// [사용 방법]
//   1) Game.unity 씬을 열고
//   2) 상단 메뉴 Hexiege > Setup > 유닛 초상화 자동 할당 클릭
//   3) Console 로그로 할당 결과 확인
//   4) Ctrl+S 로 씬 저장
//
// [주의]
//   - 기존에 이미 portrait 가 할당된 슬롯은 건드리지 않는다.
//   - 스프라이트를 찾지 못한 슬롯은 경고 로그를 출력하고 null 을 유지한다.
// ====================================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    public static class AssignUnitPortraits
    {
        // 스프라이트 파일이 위치한 폴더 (AssetDatabase.FindAssets 검색 범위)
        private static readonly string[] SearchFolders = { "Assets/_Project/Sprites/Units" };

        [MenuItem("Hexiege/Setup/유닛 초상화 자동 할당")]
        public static void Assign()
        {
            // ── 1) 씬에서 ProductionPanelUI 컴포넌트 탐색 ────────────────────────
            // FindObjectOfType 은 비활성 오브젝트를 찾지 못하므로 AllObjectsOfType 사용
            MonoBehaviour ui = null;
            foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                // 타입 이름으로 비교 (직접 참조 없이 안전하게 탐색)
                if (mb.GetType().Name == "ProductionPanelUI" && mb.gameObject.scene.isLoaded)
                {
                    ui = mb;
                    break;
                }
            }

            if (ui == null)
            {
                Debug.LogError("[AssignPortraits] 씬에서 ProductionPanelUI 를 찾을 수 없습니다. Game.unity 를 열었는지 확인하세요.");
                return;
            }

            Debug.Log($"[AssignPortraits] ProductionPanelUI 발견: {ui.gameObject.name}");

            // ── 2) SerializedObject 로 직렬화 필드 접근 ─────────────────────────
            var so = new SerializedObject(ui);
            so.Update();

            // _buildingUnitMappings : List<BuildingUnitMapping>
            var mappingsProp = so.FindProperty("_buildingUnitMappings");
            if (mappingsProp == null)
            {
                Debug.LogError("[AssignPortraits] _buildingUnitMappings 프로퍼티를 찾을 수 없습니다.");
                return;
            }

            if (mappingsProp.arraySize == 0)
            {
                Debug.LogWarning("[AssignPortraits] _buildingUnitMappings 가 비어있습니다. Inspector 에서 먼저 건물-유닛 매핑을 설정하세요.");
                return;
            }

            int totalAssigned = 0;
            int totalSkipped  = 0;
            int totalMissing  = 0;

            // ── 3) 각 BuildingUnitMapping 순회 ───────────────────────────────────
            for (int i = 0; i < mappingsProp.arraySize; i++)
            {
                // BuildingUnitMapping 구조체 프로퍼티
                var mappingProp      = mappingsProp.GetArrayElementAtIndex(i);
                var buildingTypeProp = mappingProp.FindPropertyRelative("buildingType");
                string buildingName  = buildingTypeProp != null ? buildingTypeProp.enumNames[buildingTypeProp.enumValueIndex] : $"Index{i}";

                // blueUnits / redUnits 각각 처리
                ProcessUnitList(mappingProp.FindPropertyRelative("blueUnits"), "blue", buildingName,
                    ref totalAssigned, ref totalSkipped, ref totalMissing);

                ProcessUnitList(mappingProp.FindPropertyRelative("redUnits"), "red", buildingName,
                    ref totalAssigned, ref totalSkipped, ref totalMissing);
            }

            // ── 4) 변경 적용 및 Dirty 마킹 ────────────────────────────────────────
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ui);

            // ── 5) 결과 요약 로그 ──────────────────────────────────────────────────
            Debug.Log($"[AssignPortraits] 완료. " +
                      $"할당: {totalAssigned}개  /  이미 있어 건너뜀: {totalSkipped}개  /  파일 없음(경고): {totalMissing}개");
            Debug.Log("[AssignPortraits] Ctrl+S 로 씬을 저장하세요.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // blueUnits 또는 redUnits 리스트를 순회하며 portrait 슬롯을 채운다.
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// UnitPortraitEntry 배열을 순회하여 portrait 가 null 인 슬롯에만 스프라이트를 할당한다.
        /// teamSuffix: "blue" 또는 "red" — 파일명 패턴에 사용.
        /// </summary>
        private static void ProcessUnitList(
            SerializedProperty listProp,
            string teamSuffix,
            string buildingName,
            ref int assigned,
            ref int skipped,
            ref int missing)
        {
            if (listProp == null || listProp.arraySize == 0) return;

            for (int j = 0; j < listProp.arraySize; j++)
            {
                var entryProp    = listProp.GetArrayElementAtIndex(j);
                var typeProp     = entryProp.FindPropertyRelative("type");
                var portraitProp = entryProp.FindPropertyRelative("portrait");

                if (typeProp == null || portraitProp == null) continue;

                // 이미 초상화가 할당되어 있으면 건너뜀
                if (portraitProp.objectReferenceValue != null)
                {
                    skipped++;
                    continue;
                }

                // UnitType enum 이름을 소문자로 변환해 파일명 패턴 생성
                // 예: Pistoleer → "pistoleer_portrait_blue"
                string unitName     = typeProp.enumNames[typeProp.enumValueIndex];
                string searchFilter = $"{unitName.ToLower()}_portrait_{teamSuffix} t:Sprite";

                string[] guids = AssetDatabase.FindAssets(searchFilter, SearchFolders);

                if (guids.Length == 0)
                {
                    // 파일명이 없거나 아직 임포트되지 않은 경우
                    Debug.LogWarning($"[AssignPortraits] [{buildingName}] {teamSuffix}/{unitName}: 스프라이트를 찾지 못했습니다. (검색어: {searchFilter})");
                    missing++;
                    continue;
                }

                // 검색 결과가 여러 개면 첫 번째를 사용하고 경고
                if (guids.Length > 1)
                    Debug.LogWarning($"[AssignPortraits] [{buildingName}] {teamSuffix}/{unitName}: 스프라이트 {guids.Length}개 발견, 첫 번째를 사용합니다.");

                string   path    = AssetDatabase.GUIDToAssetPath(guids[0]);
                Sprite   sprite  = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (sprite == null)
                {
                    // 파일은 있지만 Sprite 타입이 아닌 경우 (TextureType 설정 문제 등)
                    Debug.LogWarning($"[AssignPortraits] [{buildingName}] {teamSuffix}/{unitName}: 경로 '{path}' 에서 Sprite 로드 실패. Texture Import 설정의 Sprite Mode 를 확인하세요.");
                    missing++;
                    continue;
                }

                // portrait 슬롯에 할당
                portraitProp.objectReferenceValue = sprite;
                Debug.Log($"[AssignPortraits] [{buildingName}] {teamSuffix}/{unitName} → {sprite.name} ✓");
                assigned++;
            }
        }
    }
}
#endif
