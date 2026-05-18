// ============================================================================
// SetupProductionPopupUI.cs
// ProductionPanelUI 컴포넌트의 Inspector 필드를 자동 배선하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/ProductionPopup UI 재구성
//
// 실행 전 준비:
//   - Game.unity 씬을 열어둘 것 (ProductionPanelUI 컴포넌트가 있는 씬)
//
// 처리 항목:
//   1) 아래 5개 리스트에서 인덱스 5, 4, 3을 역순으로 삭제 (6→3개)
//        - _unitButtons
//        - _unitButtonPortraits
//        - _unitCostTexts
//        - _unitAutoIndicators
//        - _unitLockIndicators
//
//   2) UnitButtons2 의 자식 슬롯(0/1/2) → 액션 버튼으로 자동 배선
//        - Slot4(0) → _rallyPointButton + _rallyGoldDisplay
//        - Slot5(1) → _upgradeButton + _upgradeButtonGroup(CanvasGroup)
//                     + _upgradeIconImage + _upgradeCostText
//        - Slot6(2) → _demolishButton + _demolishRefundText
//
//   3) 기존 별도 "RallyPointButton" GameObject 비활성화
//        (랠리 기능이 UnitButtons2 Slot4 로 이전됐으므로 더 이상 필요 없음)
//
// 역순 삭제 이유:
//   SerializedProperty의 DeleteArrayElementAtIndex는 호출 직후
//   뒤쪽 인덱스가 앞으로 당겨진다. 인덱스 3부터 지우면 4,5가 밀려서
//   원치 않는 항목이 지워질 수 있으므로 5→4→3 순서로 진행한다.
//
// 안전 장치:
//   - 이미 처리된 씬(리스트 크기가 3 이하)에서는 재실행해도 변경하지 않음.
//   - 자동 배선 단계에서 대상 GO를 찾지 못하면 LogWarning/LogError만 출력하고
//     스크립트 전체가 실패하지 않도록 진행.
//
// 1회성 — 실행 후 삭제해도 무방.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hexiege.Presentation;
using Hexiege.Domain;

public static class SetupProductionPopupUI
{
    // 축소 대상이 되는 리스트의 프로퍼티 이름 목록.
    // ProductionPanelUI.cs의 [SerializeField] 필드명과 정확히 일치해야 한다.
    private static readonly string[] TargetListPropertyNames = new[]
    {
        "_unitButtons",
        "_unitButtonPortraits",
        "_unitCostTexts",
        "_unitAutoIndicators",
        "_unitLockIndicators"
    };

    // 축소 후 유지되는 슬롯 개수 (위 3개 유닛 버튼).
    private const int TargetSize = 3;

    [MenuItem("Hexiege/Setup/ProductionPopup UI 재구성")]
    private static void Setup()
    {
        // ── 씬에서 ProductionPanelUI 컴포넌트 탐색 ─────────────────────
        var ui = Object.FindFirstObjectByType<ProductionPanelUI>();
        if (ui == null)
        {
            Debug.LogError("[Setup] ProductionPanelUI 컴포넌트를 씬에서 찾을 수 없습니다. " +
                           "Game.unity 씬을 열고 다시 실행해주세요.");
            return;
        }

        // SerializedObject를 통해 수정하면 Undo 등록 + 씬 Dirty 마킹이 자동 처리됨
        var so = new SerializedObject(ui);
        so.Update();

        // ────────────────────────────────────────────────────────────────
        // [1] 유닛 버튼 리스트 6→3 축소
        // ────────────────────────────────────────────────────────────────
        int totalDeleted = 0;
        int alreadyShrunk = 0;

        foreach (var propName in TargetListPropertyNames)
        {
            var listProp = so.FindProperty(propName);
            if (listProp == null)
            {
                Debug.LogError($"[Setup] '{propName}' 프로퍼티를 찾을 수 없습니다. " +
                               "필드명이 변경됐는지 확인해주세요.");
                return;
            }

            if (!listProp.isArray)
            {
                Debug.LogError($"[Setup] '{propName}' 프로퍼티가 배열/리스트 타입이 아닙니다.");
                return;
            }

            // 이미 3개 이하 → 이 리스트는 건너뛰기
            if (listProp.arraySize <= TargetSize)
            {
                Debug.Log($"[Setup] '{propName}' 은 이미 {listProp.arraySize}개 → 변경 없음.");
                alreadyShrunk++;
                continue;
            }

            // 인덱스 (arraySize-1) 부터 TargetSize(=3) 까지 역순 삭제.
            // 예: 6개 → 인덱스 5, 4, 3 순서로 삭제 → 0,1,2만 남음.
            int beforeSize = listProp.arraySize;
            for (int i = beforeSize - 1; i >= TargetSize; i--)
            {
                // 주의: 리스트의 요소가 Object 참조(예: Button)일 경우,
                // DeleteArrayElementAtIndex를 한 번 호출하면 참조만 null로 바뀌고
                // 두 번째 호출에서 실제 슬롯이 제거되는 경우가 있다.
                // 따라서 안전하게 한 번 더 호출해 슬롯 자체를 확실히 제거한다.
                listProp.DeleteArrayElementAtIndex(i);
                if (listProp.arraySize == beforeSize)
                {
                    // null로만 바뀌고 슬롯이 남아있는 경우 한 번 더 삭제
                    listProp.DeleteArrayElementAtIndex(i);
                }
                beforeSize = listProp.arraySize;
                totalDeleted++;
            }

            Debug.Log($"[Setup] '{propName}' 축소 완료: → {listProp.arraySize} (목표 {TargetSize})");
        }

        // ────────────────────────────────────────────────────────────────
        // [2] UnitButtons2 의 액션 버튼 자동 배선
        //   - Slot4(idx 0) → 랠리
        //   - Slot5(idx 1) → 업그레이드
        //   - Slot6(idx 2) → 철거
        // ────────────────────────────────────────────────────────────────
        var unitButtons2 = FindChildByName(ui.transform, "UnitButtons2");
        if (unitButtons2 == null)
        {
            Debug.LogError("[Setup] 'UnitButtons2' Transform 을 찾을 수 없습니다. " +
                           "씬 계층 이름을 확인해주세요. (대상: Slot4/Slot5/Slot6 부모)");
        }
        else if (unitButtons2.childCount < 3)
        {
            Debug.LogError($"[Setup] 'UnitButtons2' 의 자식 수가 부족합니다. " +
                           $"필요: 3개 이상, 현재: {unitButtons2.childCount}개.");
        }
        else
        {
            // ── Slot4 (인덱스 0) → 랠리 버튼 ─────────────────────────────
            var slot4 = unitButtons2.GetChild(0);
            WireButton(so, "_rallyPointButton", slot4.GetComponent<Button>(), "Slot4 (랠리)");

            // ── Slot5 (인덱스 1) → 업그레이드 버튼 ───────────────────────
            var slot5 = unitButtons2.GetChild(1);
            WireButton(so, "_upgradeButton", slot5.GetComponent<Button>(), "Slot5 (업그레이드)");

            // CanvasGroup: 없으면 추가하여 버튼 비활성화 시 알파/레이캐스트 차단에 사용
            var cg = slot5.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = slot5.gameObject.AddComponent<CanvasGroup>();
                Debug.Log("[Setup] Slot5 에 CanvasGroup 컴포넌트 신규 추가.");
            }
            var cgProp = so.FindProperty("_upgradeButtonGroup");
            if (cgProp == null)
                Debug.LogError("[Setup] '_upgradeButtonGroup' 프로퍼티를 찾을 수 없습니다.");
            else
            {
                cgProp.objectReferenceValue = cg;
                Debug.Log("[Setup] _upgradeButtonGroup 연결 완료.");
            }

            // 업그레이드 아이콘 Image (자식 Image 중 첫 번째)
            var upIcon = slot5.GetComponentInChildren<Image>(true);
            // 주의: Button 컴포넌트가 사용하는 Slot 자신의 Image 가 첫 결과로 잡힐 수 있다.
            // 자식 중 별도 아이콘 Image 를 우선하기 위해 Slot 자신은 제외하고 다시 탐색.
            if (upIcon != null && upIcon.gameObject == slot5.gameObject)
            {
                upIcon = null;
                foreach (var img in slot5.GetComponentsInChildren<Image>(true))
                {
                    if (img.gameObject != slot5.gameObject)
                    {
                        upIcon = img;
                        break;
                    }
                }
            }
            if (upIcon == null)
            {
                Debug.LogWarning("[Setup] _upgradeIconImage 자동 연결 실패 — Inspector에서 수동 연결 필요");
            }
            else
            {
                var prop = so.FindProperty("_upgradeIconImage");
                if (prop == null)
                    Debug.LogError("[Setup] '_upgradeIconImage' 프로퍼티를 찾을 수 없습니다.");
                else
                {
                    prop.objectReferenceValue = upIcon;
                    Debug.Log($"[Setup] _upgradeIconImage 연결 완료: '{upIcon.gameObject.name}'");
                }
            }

            // 업그레이드 비용 텍스트 (기존 필드 재배선)
            var upTmp = slot5.GetComponentInChildren<TextMeshProUGUI>(true);
            if (upTmp == null)
            {
                Debug.LogWarning("[Setup] _upgradeCostText 자동 연결 실패 — Inspector에서 수동 연결 필요");
            }
            else
            {
                var prop = so.FindProperty("_upgradeCostText");
                if (prop == null)
                    Debug.LogError("[Setup] '_upgradeCostText' 프로퍼티를 찾을 수 없습니다.");
                else
                {
                    prop.objectReferenceValue = upTmp;
                    Debug.Log($"[Setup] _upgradeCostText 연결 완료: '{upTmp.gameObject.name}'");
                }
            }

            // ── Slot6 (인덱스 2) → 철거 버튼 ─────────────────────────────
            var slot6 = unitButtons2.GetChild(2);
            WireButton(so, "_demolishButton", slot6.GetComponent<Button>(), "Slot6 (철거)");

            var demoTmp = slot6.GetComponentInChildren<TextMeshProUGUI>(true);
            if (demoTmp == null)
            {
                Debug.LogWarning("[Setup] _demolishRefundText 자동 연결 실패 — Inspector에서 수동 연결 필요");
            }
            else
            {
                var prop = so.FindProperty("_demolishRefundText");
                if (prop == null)
                    Debug.LogError("[Setup] '_demolishRefundText' 프로퍼티를 찾을 수 없습니다.");
                else
                {
                    prop.objectReferenceValue = demoTmp;
                    Debug.Log($"[Setup] _demolishRefundText 연결 완료: '{demoTmp.gameObject.name}'");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        // [3] 기존 "RallyPointButton" GameObject 비활성화
        //   랠리 기능이 UnitButtons2/Slot4 로 이전됐으므로 더 이상 사용되지 않음.
        // ────────────────────────────────────────────────────────────────
        var legacyRally = FindChildByName(ui.transform, "RallyPointButton");
        if (legacyRally == null)
        {
            Debug.LogWarning("[Setup] 'RallyPointButton' 비활성화 실패 — Inspector에서 수동 처리 필요");
        }
        else
        {
            legacyRally.gameObject.SetActive(false);
            EditorUtility.SetDirty(legacyRally.gameObject);
            Debug.Log($"[Setup] 'RallyPointButton' GameObject 비활성화 완료.");
        }

        // ────────────────────────────────────────────────────────────────
        // [4] _buildingUpgradeIcons 자동 채우기
        //   업그레이드 버튼 아이콘용으로, 16개 BuildingType의 blue/red Sprite를
        //   Assets/_Project/Sprites/Buildings 폴더에서 자동 탐색해 리스트에 채운다.
        //
        //   파일명 규칙:
        //     - blue: "bld_{buildingtype 소문자}_blue.png"
        //     - red : "bld_{buildingtype 소문자}_red.png"
        //   예: BuildingType.WarAcademy → bld_waracademy_blue / bld_waracademy_red
        //
        //   안전장치:
        //     - 이미 1개 이상 채워져 있으면 스킵 (재실행 시 덮어쓰기 방지)
        //     - Sprite를 못 찾으면 LogWarning 만 출력하고 계속 진행 (null 허용)
        //     - 리스트 프로퍼티를 못 찾으면 LogError 후 이 스텝만 건너뜀
        // ────────────────────────────────────────────────────────────────
        FillBuildingUpgradeIcons(so);

        // ── 저장 ──────────────────────────────────────────────────────
        so.ApplyModifiedProperties();

        // Inspector 갱신 + 씬 변경사항 Dirty 마킹
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        // Project/Hierarchy 창에서 대상 오브젝트 하이라이트
        EditorGUIUtility.PingObject(ui.gameObject);

        Debug.Log($"[Setup] ProductionPopup UI 재구성 완료. " +
                  $"리스트 슬롯 {totalDeleted}개 제거 + 액션 버튼 자동 배선 시도. " +
                  "변경사항을 보존하려면 씬을 저장하세요 (Ctrl+S).");
    }

    // ────────────────────────────────────────────────────────────────
    // 업그레이드 아이콘 자동 채우기 대상이 되는 16개 BuildingType.
    // (현재 BuildingType 열거형에서 업그레이드 가능한 1~2단계 건물들)
    // ────────────────────────────────────────────────────────────────
    private static readonly BuildingType[] UpgradeIconTargets = new[]
    {
        BuildingType.WarAcademy,
        BuildingType.HumanBarracks,
        BuildingType.Armory,
        BuildingType.WeaponForge,
        BuildingType.VehicleBay,
        BuildingType.BlazeConduit,
        BuildingType.InfernoCore,
        BuildingType.TidalNexus,
        BuildingType.OceanicHeart,
        BuildingType.TerraForge,
        BuildingType.GaeaSanctum,
        BuildingType.PrimalDen,
        BuildingType.PrimalSanctuary,
        BuildingType.FeralDen,
        BuildingType.FeralSanctuary,
        BuildingType.FloralNursery
    };

    // Sprite 탐색 루트 폴더 (AssetDatabase.FindAssets 의 search-in-folders 파라미터에 사용).
    private const string BuildingSpritesFolder = "Assets/_Project/Sprites/Buildings";

    // ────────────────────────────────────────────────────────────────
    // 헬퍼: _buildingUpgradeIcons 리스트에 16개 BuildingType별 blue/red Sprite를
    //       자동 탐색해 채운다. (스텝 [4])
    //
    // 동작:
    //   1) "_buildingUpgradeIcons" 리스트 프로퍼티 취득. 없으면 LogError 후 종료.
    //   2) 이미 1개 이상 채워져 있으면 "기존 데이터 보호" 차원에서 스킵.
    //   3) UpgradeIconTargets 의 각 BuildingType 에 대해:
    //        - 파일명: "bld_{type.ToString().ToLower()}_blue" / "_red"
    //        - AssetDatabase.FindAssets 로 BuildingSpritesFolder 내에서 검색
    //        - 찾으면 LoadAssetAtPath<Sprite> 로 로드, 못 찾으면 null 허용 + 경고
    //   4) 리스트에 새 요소를 추가 (InsertArrayElementAtIndex)
    //        - buildingType: intValue 로 enum 정수값 직접 기입
    //        - blueIcon / redIcon: objectReferenceValue 로 Sprite 참조 기입
    // ────────────────────────────────────────────────────────────────
    private static void FillBuildingUpgradeIcons(SerializedObject so)
    {
        var listProp = so.FindProperty("_buildingUpgradeIcons");
        if (listProp == null)
        {
            Debug.LogError("[Setup] '_buildingUpgradeIcons' 프로퍼티를 찾을 수 없습니다. " +
                           "필드명이 변경됐는지 확인해주세요. (이 스텝만 건너뜁니다)");
            return;
        }

        if (!listProp.isArray)
        {
            Debug.LogError("[Setup] '_buildingUpgradeIcons' 가 배열/리스트 타입이 아닙니다. (이 스텝만 건너뜁니다)");
            return;
        }

        // 재실행 안전장치 — 이미 1개 이상 채워져 있으면 사용자의 수동 편집이
        // 있을 수 있으므로 덮어쓰지 않고 건너뛴다.
        if (listProp.arraySize > 0)
        {
            Debug.Log($"[Setup] '_buildingUpgradeIcons' 는 이미 {listProp.arraySize}개 항목 보유 " +
                      $"→ 자동 채우기 스킵 (덮어쓰기 방지).");
            return;
        }

        int filledCount = 0;
        int blueMissingCount = 0;
        int redMissingCount = 0;

        foreach (var type in UpgradeIconTargets)
        {
            // 파일명 생성 — 소문자 변환된 enum 이름에 "bld_" prefix, "_blue"/"_red" suffix.
            string lowerName = type.ToString().ToLower();
            string blueName = $"bld_{lowerName}_blue";
            string redName = $"bld_{lowerName}_red";

            Sprite blueSprite = FindSpriteByName(blueName);
            Sprite redSprite = FindSpriteByName(redName);

            if (blueSprite == null)
            {
                Debug.LogWarning($"[Setup] {type} - Blue Sprite '{blueName}' 를 찾지 못함. (null로 채움)");
                blueMissingCount++;
            }
            if (redSprite == null)
            {
                Debug.LogWarning($"[Setup] {type} - Red Sprite '{redName}' 를 찾지 못함. (null로 채움)");
                redMissingCount++;
            }

            // 리스트 끝에 새 요소 삽입.
            // InsertArrayElementAtIndex(arraySize) → 현재 크기 위치(=끝)에 새 슬롯 생성.
            int newIndex = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(newIndex);
            var elem = listProp.GetArrayElementAtIndex(newIndex);

            // BuildingIconEntry 구조체의 각 필드 채우기.
            // enumValueIndex 대신 intValue 사용 — BuildingType 의 정수값(enum 정의 순서가
            // 아닌 명시적 int 값)을 그대로 기록하기 위함.
            var typeProp = elem.FindPropertyRelative("buildingType");
            var blueProp = elem.FindPropertyRelative("blueIcon");
            var redProp = elem.FindPropertyRelative("redIcon");

            if (typeProp != null) typeProp.intValue = (int)type;
            if (blueProp != null) blueProp.objectReferenceValue = blueSprite;
            if (redProp != null) redProp.objectReferenceValue = redSprite;

            filledCount++;
        }

        Debug.Log($"[Setup] '_buildingUpgradeIcons' 자동 채우기 완료 — " +
                  $"항목 {filledCount}개 추가 (Blue 누락 {blueMissingCount}, Red 누락 {redMissingCount}).");
    }

    // ────────────────────────────────────────────────────────────────
    // 헬퍼: 이름(확장자 제외)으로 Sprite 에셋 1개를 검색해 로드.
    //   - 검색 범위는 BuildingSpritesFolder 하위로 제한.
    //   - 동명의 Sprite 가 여러 개면 첫 번째 결과만 반환.
    //   - 못 찾으면 null 반환.
    // ────────────────────────────────────────────────────────────────
    private static Sprite FindSpriteByName(string fileName)
    {
        // AssetDatabase 의 검색 쿼리 형식: "{fileName} t:Sprite"
        // search-in-folders 로 지정 폴더 하위로 범위 제한.
        string[] guids = AssetDatabase.FindAssets($"{fileName} t:Sprite", new[] { BuildingSpritesFolder });
        if (guids == null || guids.Length == 0) return null;

        // FindAssets는 부분 일치도 잡으므로, 정확히 fileName 과 일치하는 에셋을 우선 선택.
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(assetName, fileName, System.StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        // 정확 일치가 없으면 첫 번째 부분 일치 결과를 폴백으로 사용.
        string fallbackPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
    }

    // ────────────────────────────────────────────────────────────────
    // 헬퍼: SerializedProperty 에 Button 컴포넌트 참조 연결
    //   - 누락 시 LogWarning 만 출력하고 진행 (스크립트 중단 X)
    // ────────────────────────────────────────────────────────────────
    private static void WireButton(SerializedObject so, string propName, Button button, string label)
    {
        if (button == null)
        {
            Debug.LogWarning($"[Setup] {propName} 자동 연결 실패 — {label} 에 Button 컴포넌트가 없습니다.");
            return;
        }

        var prop = so.FindProperty(propName);
        if (prop == null)
        {
            Debug.LogError($"[Setup] '{propName}' 프로퍼티를 찾을 수 없습니다.");
            return;
        }

        prop.objectReferenceValue = button;
        Debug.Log($"[Setup] {propName} 연결 완료: '{button.gameObject.name}' ({label})");
    }

    // ────────────────────────────────────────────────────────────────
    // 헬퍼: 이름으로 자식 Transform 재귀 탐색
    //   - 첫 번째로 매칭되는 결과 반환 (DFS)
    //   - 못 찾으면 null
    // ────────────────────────────────────────────────────────────────
    private static Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
