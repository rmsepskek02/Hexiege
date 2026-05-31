# Plan — 유닛 생산 패널 초상화 자동 할당 에디터 스크립트

## 무엇을 왜 만드는가

유닛 생산 패널에서 유닛 버튼의 초상화 이미지가 런타임에 사라지는 문제를 해결하기 위해,
`_buildingUnitMappings` 리스트의 각 초상화 슬롯에 스프라이트를 자동으로 할당하는
1회성 에디터 유틸리티 스크립트를 만든다.

기존 `ConnectProductionPanelUI.cs` 패턴(SerializedObject + MenuItem)을 그대로 따른다.

---

## 수정/생성 파일

| 파일 | 작업 |
|------|------|
| `Assets/Editor/AssignUnitPortraits.cs` | **신규 생성** — 1회성 에디터 유틸리티 |

---

## GameSystemRules.md 근거

- 이 스크립트는 에디터 전용 유틸리티로 게임 시스템 규칙 범위 밖이나,
  기존 에디터 스크립트 패턴(SerializedObject, MenuItem, SetDirty 후 Ctrl+S 저장 안내)을 따름.

---

## 구현 상세

### 파일: `Assets/Editor/AssignUnitPortraits.cs`

**메뉴 경로**: `Hexiege/Setup/유닛 초상화 자동 할당`

**처리 흐름**:
1. `Resources.FindObjectsOfTypeAll<MonoBehaviour>()` 로 씬에서 ProductionPanelUI 탐색
2. `SerializedObject` 로 `_buildingUnitMappings` 필드 접근
3. 리스트 각 항목의 `blueUnits` / `redUnits` 배열을 순회
4. 각 `UnitPortraitEntry` 의 `type` 필드(UnitType enum) 읽기
5. `UnitType.ToString().ToLower()` → 파일명 패턴 생성
   - blueUnits: `{name}_portrait_blue`
   - redUnits: `{name}_portrait_red`
6. `AssetDatabase.FindAssets($"{pattern} t:Sprite", searchFolders)` 로 GUID 검색
7. `AssetDatabase.GUIDToAssetPath` + `AssetDatabase.LoadAssetAtPath<Sprite>` 로 스프라이트 로드
8. `portrait` 필드가 null 인 경우에만 할당 (기존 할당 유지)
9. `so.ApplyModifiedProperties()` + `EditorUtility.SetDirty(ui)` 후 저장 안내

**검색 폴더**: `Assets/_Project/Sprites/Units`

**안전장치**:
- `_buildingUnitMappings` 리스트가 비어있으면 경고 출력 후 종료
- 스프라이트를 찾지 못한 항목은 경고 로그 출력 (null로 두지 않고 로그로 알림)
- 기존 portrait != null 인 슬롯은 건드리지 않음

---

## 사용 방법 (구현 후)

1. Game.unity 씬을 열고
2. 상단 메뉴 `Hexiege > Setup > 유닛 초상화 자동 할당` 클릭
3. Console 로그로 결과 확인
4. Ctrl+S 로 씬 저장
