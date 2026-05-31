#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// [그룹 D] Lobby.unity 씬의 VerticalLayoutGroup(VLG) 5개 패널 내부 버튼·텍스트 16건을
    /// 고정 픽셀 높이에서 가용 공간 비율 분배 방식으로 전환하는 1회성 에디터 스크립트.
    ///
    /// 처리 방식 (규칙 2):
    ///  1) 각 패널 VLG: childControlHeight = true, childForceExpandHeight = true 로 변경
    ///     → VLG가 자식 높이를 직접 제어하고 가용 세로 공간을 모두 채우도록 함.
    ///  2) 각 자식에 LayoutElement 추가/갱신:
    ///     - 버튼류(기존 높이 100) → flexibleHeight = 2  (텍스트의 2배 공간 차지)
    ///     - 텍스트류(기존 높이 50) → flexibleHeight = 1
    ///     - preferredHeight / minWidth / minHeight / preferredWidth / flexibleWidth 는 -1로 비활성화
    ///  3) 각 자식의 sizeDelta = (0,0) 으로 변경 → 고정 픽셀 높이 제거.
    ///
    /// 이 스크립트는 코드 로직 변경 없이 Inspector 값만 변경한다.
    /// 사용법: Unity 상단 메뉴 [Hexiege/Fix/LobbyUI/GroupD_VLG] 실행.
    /// </summary>
    public static class FixLobbyUiGroupD
    {
        private const string TargetSceneName = "Lobby";

        /// <summary>
        /// 하나의 자식 처리 항목: (자식 이름, 적용할 flexibleHeight 값).
        /// </summary>
        private readonly struct ChildEntry
        {
            public readonly string Name;
            public readonly float FlexibleHeight;

            public ChildEntry(string name, float flexibleHeight)
            {
                Name = name;
                FlexibleHeight = flexibleHeight;
            }
        }

        /// <summary>
        /// 패널 이름 → 그 패널에서 처리할 자식 목록.
        /// Plan.md의 "처리 대상 패널 및 자식 목록" 표를 그대로 코드화한 것이다.
        /// 버튼류는 flexibleHeight=2, 텍스트류는 flexibleHeight=1.
        /// </summary>
        private static readonly Dictionary<string, ChildEntry[]> PanelTable = new Dictionary<string, ChildEntry[]>
        {
            {
                "BattleMainPanel", new[]
                {
                    new ChildEntry("SingleplayBtn", 2f),
                    new ChildEntry("CustomBtn", 2f),
                    new ChildEntry("RandomBtn", 2f),
                }
            },
            {
                "CustomGamePanel", new[]
                {
                    new ChildEntry("CreateRoomBtn", 2f),
                    new ChildEntry("JoinByCodeBtn", 2f),
                    new ChildEntry("BackBtn", 2f),
                }
            },
            {
                "CustomHostPanel", new[]
                {
                    new ChildEntry("CodeText", 1f),
                    new ChildEntry("PlayersText", 1f),
                    new ChildEntry("StatusText", 1f),
                    new ChildEntry("ErrorText", 1f),
                    new ChildEntry("CancelBtn", 2f),
                }
            },
            {
                "CustomJoinPanel", new[]
                {
                    new ChildEntry("CodeInput", 2f),
                    new ChildEntry("JoinBtn", 2f),
                    new ChildEntry("BackBtn", 2f),
                }
            },
            {
                "RandomMatchPanel", new[]
                {
                    new ChildEntry("StatusText", 1f),
                    new ChildEntry("CancelBtn", 2f),
                }
            },
        };

        [MenuItem("Hexiege/Fix/LobbyUI/GroupD_VLG")]
        public static void Fix()
        {
            // 활성 씬이 Lobby인지 확인.
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != TargetSceneName)
            {
                Debug.LogWarning($"[FixLobbyUiGroupD] 활성 씬이 '{TargetSceneName}'이(가) 아닙니다. " +
                                 $"현재 활성 씬: '{activeScene.name}'. 작업을 중단합니다.");
                return;
            }

            int changeCount = 0;

            // 패널 단위로 순회한다.
            foreach (var kvp in PanelTable)
            {
                string panelName = kvp.Key;
                ChildEntry[] children = kvp.Value;

                // 씬 전체에서 패널을 이름으로 찾는다.
                var panel = FindDeepChildInScene(TargetSceneName, panelName);
                if (panel == null)
                {
                    Debug.LogWarning($"[FixLobbyUiGroupD] 패널 '{panelName}'을(를) 찾지 못했습니다. 건너뜁니다.");
                    continue;
                }

                // 1) 패널의 VerticalLayoutGroup 설정 변경
                changeCount += FixVerticalLayoutGroup(panel, panelName);

                // 2) 각 자식 LayoutElement + sizeDelta 처리
                foreach (var entry in children)
                {
                    var child = FindDeepChild(panel, entry.Name);
                    if (child == null)
                    {
                        Debug.LogWarning($"[FixLobbyUiGroupD] '{panelName}/{entry.Name}'을(를) 찾지 못했습니다. 건너뜁니다.");
                        continue;
                    }

                    changeCount += FixChild(child as RectTransform, entry.FlexibleHeight, panelName, entry.Name);
                }
            }

            if (changeCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[FixLobbyUiGroupD] 완료: {changeCount}건 수정 후 '{TargetSceneName}.unity' 저장.");
            }
            else
            {
                Debug.Log("[FixLobbyUiGroupD] 변경할 항목이 없습니다.");
            }
        }

        /// <summary>
        /// 패널의 VerticalLayoutGroup이 자식 높이를 제어하고 가용 공간을 모두 채우도록 설정한다.
        /// </summary>
        private static int FixVerticalLayoutGroup(Transform panel, string panelName)
        {
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                Debug.LogWarning($"[FixLobbyUiGroupD] 패널 '{panelName}'에 VerticalLayoutGroup이 없습니다. VLG 설정을 건너뜁니다.");
                return 0;
            }

            Undo.RecordObject(vlg, "Fix VLG ChildControl/Expand");
            vlg.childControlHeight = true;      // VLG가 자식의 높이를 직접 제어
            vlg.childForceExpandHeight = true;  // 남는 세로 공간을 flexibleHeight 비율대로 분배
            EditorUtility.SetDirty(vlg);

            Debug.Log($"[FixLobbyUiGroupD] '{panelName}' VLG: childControlHeight=true, childForceExpandHeight=true 설정 완료.");
            return 1;
        }

        /// <summary>
        /// 자식 RectTransform에 LayoutElement(flexibleHeight 지정)를 부여하고 sizeDelta를 0으로 만든다.
        /// LayoutElement가 없으면 새로 추가하고, 있으면 기존 컴포넌트를 재사용한다.
        /// </summary>
        private static int FixChild(RectTransform rt, float flexibleHeight, string panelName, string childName)
        {
            if (rt == null) return 0;

            // LayoutElement 확보 (없으면 Undo.AddComponent, 있으면 Undo.RecordObject 후 재사용)
            var layoutElement = rt.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = Undo.AddComponent<LayoutElement>(rt.gameObject);
            }
            else
            {
                Undo.RecordObject(layoutElement, "Fix LayoutElement");
            }

            // 세로 공간 비율만 지정하고 나머지 항목은 -1로 비활성화한다.
            // (-1은 "이 항목으로 레이아웃에 관여하지 않음"을 의미)
            layoutElement.minWidth = -1f;
            layoutElement.minHeight = -1f;
            layoutElement.preferredWidth = -1f;
            layoutElement.preferredHeight = -1f;
            layoutElement.flexibleWidth = -1f;
            layoutElement.flexibleHeight = flexibleHeight;
            EditorUtility.SetDirty(layoutElement);

            // 고정 픽셀 높이 제거: 높이는 이제 VLG + LayoutElement가 결정한다.
            Undo.RecordObject(rt, "Fix Child SizeDelta");
            rt.sizeDelta = Vector2.zero;
            EditorUtility.SetDirty(rt);

            Debug.Log($"[FixLobbyUiGroupD] '{panelName}/{childName}': LayoutElement flexibleHeight={flexibleHeight}, sizeDelta=(0,0) 적용.");
            return 1;
        }

        // ---------------------------------------------------------------------
        // 공용 유틸리티
        // ---------------------------------------------------------------------

        /// <summary>
        /// 씬 전체(모든 루트 하위 포함)에서 이름이 일치하는 Transform을 재귀적으로 찾는다.
        /// </summary>
        private static Transform FindDeepChildInScene(string sceneName, string objectName)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != sceneName) return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == objectName) return root.transform;

                var found = FindDeepChild(root.transform, objectName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 부모 Transform 아래에서 이름이 일치하는 자식을 재귀적으로(깊이 우선) 탐색한다.
        /// </summary>
        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;

                var result = FindDeepChild(child, childName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
