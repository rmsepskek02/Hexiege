// ============================================================================
// FixDifficultySelectViewLayout.cs
//
// [용도]
//  Lobby.unity 씬에 이미 존재하는 DifficultySelectView의 계층 구조를 바로잡는
//  1회성(한 번만 쓰고 버려도 되는) 에디터 스크립트입니다.
//
//  기존 구조에서는 버튼 4개(Easy/Normal/Hard/Back)가 "ButtonArea"라는
//  중간 컨테이너 GameObject 아래에 들어 있었습니다.
//  이 스크립트는 그 중간 컨테이너를 없애고, 버튼 4개를 DifficultySelectView
//  바로 아래(직속 자식)로 옮긴 뒤, VerticalLayoutGroup으로 세로 정렬되도록
//  구조만 정리합니다.
//
// [중요 포인트]
//  - 버튼을 "리패런팅(부모만 바꾸기)"만 하므로, 사용자가 이미 손으로 설정해 둔
//    Image.sprite(버튼 그림), Image.color(색상), TMP 글자 색·폰트 등 모든 값이
//    그대로 보존됩니다. (컴포넌트를 새로 만들지 않기 때문)
//
// [실행 방법]
//  1) Unity 에디터에서 Lobby.unity 씬을 먼저 열어 둡니다. (필수)
//  2) 상단 메뉴 → Hexiege/Fix/DifficultySelectView 레이아웃 수정 클릭
//
// [실행 후]
//  이 파일은 1회 실행 후 삭제해도 무방합니다.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// DifficultySelectView의 ButtonArea 컨테이너를 제거하고
    /// 버튼들을 직속 자식으로 끌어올려 VerticalLayoutGroup으로 정렬하는 에디터 도구.
    /// </summary>
    public static class FixDifficultySelectViewLayout
    {
        // 메뉴 경로 상수 (오타 방지 및 한 곳에서 관리)
        private const string MenuPath = "Hexiege/Fix/DifficultySelectView 레이아웃 수정";

        /// <summary>
        /// 메뉴 클릭 시 실행되는 진입점.
        /// 씬에서 DifficultySelectView를 찾아 계층 구조를 수정한다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Fix()
        {
            // ---------------------------------------------------------------
            // 1단계: 씬에서 DifficultySelectView 컴포넌트를 찾는다.
            //  - 비활성(꺼져 있는) 오브젝트에 붙어 있어도 찾을 수 있도록
            //    FindObjectsInactive.Include 옵션을 사용한다.
            // ---------------------------------------------------------------
            var view = Object.FindFirstObjectByType<DifficultySelectView>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogError("[FixDifficultySelectViewLayout] DifficultySelectView를 씬에서 찾을 수 없습니다. Lobby.unity 씬을 열고 다시 실행하세요.");
                return;
            }

            // ---------------------------------------------------------------
            // 2단계: DifficultySelectView 아래에서 "ButtonArea" 자식을 찾는다.
            //  - 이미 한 번 수정해서 ButtonArea가 사라진 상태라면 더 할 일이 없다.
            // ---------------------------------------------------------------
            Transform buttonAreaTr = view.transform.Find("ButtonArea");
            if (buttonAreaTr == null)
            {
                Debug.LogWarning("[FixDifficultySelectViewLayout] 이미 수정되었거나 ButtonArea가 없습니다.");
                return;
            }
            GameObject buttonArea = buttonAreaTr.gameObject;

            // ---------------------------------------------------------------
            // 3단계: ButtonArea의 자식(버튼들)을 별도 배열에 복사해 둔다.
            //  - 주의: 리패런팅을 하면 ButtonArea의 childCount가 실시간으로 줄어든다.
            //    따라서 반복 중에 GetChild를 직접 쓰면 인덱스가 어긋난다.
            //    먼저 배열로 스냅샷을 떠 두고(순서 유지), 그 배열을 기준으로 옮긴다.
            // ---------------------------------------------------------------
            var buttons = new Transform[buttonAreaTr.childCount];
            for (int i = 0; i < buttonAreaTr.childCount; i++)
            {
                buttons[i] = buttonAreaTr.GetChild(i);
            }

            // ---------------------------------------------------------------
            // 4단계: 복사한 버튼들을 순서대로 DifficultySelectView의 직속 자식으로 옮긴다.
            //  - Undo.SetTransformParent를 쓰면 Ctrl+Z(되돌리기)가 가능하다.
            //  - 부모만 바뀌므로 Image.sprite, Image.color, TMP color/font 등
            //    버튼에 붙어 있던 모든 컴포넌트 값은 그대로 유지된다.
            // ---------------------------------------------------------------
            foreach (var btn in buttons)
            {
                Undo.SetTransformParent(btn, view.transform, "Reparent Button");
            }

            // ---------------------------------------------------------------
            // 5단계: 비워진 ButtonArea GameObject를 삭제한다.
            //  - 자식을 모두 옮겼으므로 이제 빈 컨테이너만 남아 있다.
            // ---------------------------------------------------------------
            Undo.DestroyObjectImmediate(buttonArea);

            // ---------------------------------------------------------------
            // 6단계: DifficultySelectView에 VerticalLayoutGroup(세로 정렬)을 추가한다.
            //  - BattleMainPanel과 동일한 설정값을 사용한다.
            //  - childForceExpandHeight = false 가 중요하다. true로 두면
            //    버튼이 패널 전체 높이를 균등 분할해 너무 커진다.
            // ---------------------------------------------------------------
            var vlg = Undo.AddComponent<VerticalLayoutGroup>(view.gameObject);
            vlg.padding = new RectOffset(0, 0, 60, 60);   // Left, Right, Top, Bottom
            vlg.spacing = 20f;                            // 버튼 사이 간격
            vlg.childAlignment = TextAnchor.MiddleCenter; // 자식을 가운데 정렬
            vlg.childForceExpandWidth = true;             // 가로는 패널 폭에 맞춰 확장
            vlg.childForceExpandHeight = false;           // 세로는 강제 확장 금지(버튼이 커지는 것 방지)
            vlg.childControlWidth = true;                 // 자식 가로 크기를 레이아웃이 제어
            vlg.childControlHeight = true;                // 자식 세로 크기를 레이아웃이 제어

            // ---------------------------------------------------------------
            // 7단계: 버튼 4개 각각에 LayoutElement를 추가한다.
            //  - preferredHeight로 버튼 높이를 100px로 지정한다.
            //  - flexibleHeight = 0으로 두어 남는 공간을 따라 늘어나지 않게 한다.
            //  - 그 외 값(minWidth/minHeight/preferredWidth/flexibleWidth)은
            //    기본값(-1, "미설정")을 그대로 둔다.
            // ---------------------------------------------------------------
            foreach (var btn in buttons)
            {
                var le = Undo.AddComponent<LayoutElement>(btn.gameObject);
                le.preferredHeight = 100f;
                le.flexibleHeight = 0f;
            }

            // ---------------------------------------------------------------
            // 8단계: 씬을 "변경됨" 상태로 표시하고 저장한다.
            //  - MarkSceneDirty: 저장이 필요한 변경이 있음을 에디터에 알림
            //  - SaveScene: 실제로 디스크에 씬을 저장
            // ---------------------------------------------------------------
            var scene = view.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // ---------------------------------------------------------------
            // 9단계: 결과를 알리고, 수정된 오브젝트를 Hierarchy에서 강조 표시한다.
            //  - Selection.activeObject: 해당 오브젝트를 선택 상태로 만듦
            //  - PingObject: Hierarchy에서 깜빡여 위치를 알려줌
            // ---------------------------------------------------------------
            Selection.activeObject = view.gameObject;
            EditorGUIUtility.PingObject(view.gameObject);
            Debug.Log("[FixDifficultySelectViewLayout] 완료. ButtonArea 제거, VLG 부착, LayoutElement 추가.");
        }
    }
}
