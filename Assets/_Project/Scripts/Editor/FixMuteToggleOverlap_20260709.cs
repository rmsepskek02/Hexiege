// ============================================================================
// FixMuteToggleOverlap_20260709.cs  (1회성 Editor 유틸리티 — 실행 후 삭제 가능)
//
// 목적 (버그2 수정: 소리켜기/음소거 버튼 위치가 전환 시 달라지는 문제):
//   VolumeButtonContainer는 VerticalLayoutGroup으로 자식을 세로 배치한다.
//   그런데 OnButton(전체 소리켜기)과 OffButton(전체 음소거)이 각각 "별도의 형제
//   오브젝트"로 컨테이너 바로 아래 들어가 있어서, VerticalLayoutGroup이 이 둘을
//   서로 다른 두 개의 독립 슬롯(행)으로 계산한다. 두 버튼은 뮤트 상태에 따라
//   CanvasGroup alpha로 상호 배타 표시되지만(GameObject는 계속 active), 레이아웃상
//   서로 다른 위치를 차지하므로 켜기↔음소거 전환 시 버튼 위치가 달라 보인다.
//
// 해결 방법 (사용자와 합의됨):
//   OnButton/OffButton을 감싸는 빈 래퍼 오브젝트 "MuteToggleSlot"을 새로 만들어
//   VolumeButtonContainer의 직접 자식으로 삽입하고, 기존 OnButton/OffButton은
//   "파괴/재생성 없이" Transform.SetParent()로 그 안에 재부모화(reparent)한다.
//   MuteToggleSlot 안에서 두 버튼을 anchorMin=(0,0)/anchorMax=(1,1)/sizeDelta=(0,0)/
//   anchoredPosition=(0,0)로 완전히 겹치게 만든다.
//   이렇게 하면 VerticalLayoutGroup의 자식 목록에서 두 버튼이 MuteToggleSlot
//   "한 슬롯"만 차지하게 되어, 어느 버튼이 보이든 항상 동일한 위치에 표시된다.
//
//   ※ 중요 — 왜 파괴/재생성이 아니라 재부모화인가:
//     기존 씬의 InGameSettingsUI/LobbySettingsView가 _soundOnButton/_muteButton
//     필드로 이 버튼들의 Button 컴포넌트를 fileID로 직접 참조한다. GameObject를
//     파괴하고 새로 만들면 fileID가 바뀌어 이 참조 배선이 전부 깨진다.
//     Transform.SetParent()로 재부모화만 하면 fileID/컴포넌트 참조가 그대로
//     유지되므로 기존 배선이 안 깨진다.
//
// 대상:
//   [Game.unity]  InGameSettingsPanel > Panel > VolumePanel > VolumeButtonContainer
//                 아래의 OnButton, OffButton (형제 ResetButton/BackButton은 그대로 둔다)
//   [Lobby.unity] SettingPanel > LobbySettingsView > SubViewContainer > SoundSubView >
//                 VolumeButtonContainer 아래의 OnButton, OffButton (형제 ResetButton은 그대로 둔다)
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Setup/On-Off 버튼 겹치기 수정 (2026-07-09)
//   실행하면 Game.unity → Lobby.unity 순서로 자동으로 열고, 구조 변경 후 저장한다.
//   실행 전 열려 있던 씬에 저장하지 않은 변경이 있으면 저장 여부를 물으므로 주의.
//
// 재실행 안전성(idempotent):
//   이미 MuteToggleSlot이 VolumeButtonContainer 아래 있으면 그것을 재사용하므로
//   여러 번 실행해도 중복 생성되지 않는다.
//
// Editor 전용 — Editor 폴더에 위치하므로 빌드에 포함되지 않는다.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// VolumeButtonContainer의 OnButton/OffButton을 MuteToggleSlot 래퍼로 감싸
    /// 항상 같은 위치에 겹쳐 표시되도록 구조를 바로잡는 1회성 유틸리티.
    /// </summary>
    public static class FixMuteToggleOverlap_20260709
    {
        // 씬 경로 상수.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        // 씬 내 오브젝트 이름 상수 — 하이러키 이름이 바뀌면 이 한 곳만 수정한다.
        private const string NameVolumeButtonContainer = "VolumeButtonContainer";
        private const string NameMuteToggleSlot = "MuteToggleSlot"; // 새로 만드는 래퍼
        private const string NameOnButton = "OnButton";             // 전체 소리켜기
        private const string NameOffButton = "OffButton";           // 전체 음소거

        [MenuItem("Hexiege/Setup/On-Off 버튼 겹치기 수정 (2026-07-09)")]
        public static void Run()
        {
            Debug.Log("[FixMuteToggleOverlap] 시작 — Game.unity → Lobby.unity 순서로 OnButton/OffButton을 " +
                      "MuteToggleSlot 래퍼로 감쌉니다.");

            ProcessScene(GameScenePath);
            ProcessScene(LobbyScenePath);

            Debug.Log("[FixMuteToggleOverlap] 완료 — 위 로그에서 각 씬의 처리 결과(신규 생성/재사용/건너뜀)를 확인하세요.");
        }

        // ================================================================
        // 씬 단위 처리
        // ================================================================

        /// <summary>
        /// 지정한 씬을 열어 VolumeButtonContainer 아래 OnButton/OffButton을
        /// MuteToggleSlot 래퍼로 감싸고 저장한다. 대상을 못 찾으면 경고만 남기고 건너뛴다.
        /// </summary>
        private static void ProcessScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string tag = $"[FixMuteToggleOverlap][{System.IO.Path.GetFileNameWithoutExtension(scenePath)}]";

            // 1) VolumeButtonContainer 찾기.
            GameObject container = FindDeepInScene(scene, NameVolumeButtonContainer);
            if (container == null)
            {
                Debug.LogWarning($"{tag} {NameVolumeButtonContainer}를 찾지 못했습니다. 이 씬은 건너뜁니다.");
                return;
            }

            Transform containerTf = container.transform;

            // 2) 컨테이너 하위에서 OnButton/OffButton 찾기.
            GameObject onButton = FindDeep(containerTf, NameOnButton);
            GameObject offButton = FindDeep(containerTf, NameOffButton);
            if (onButton == null || offButton == null)
            {
                Debug.LogWarning($"{tag} {NameOnButton}({(onButton != null ? "찾음" : "없음")}) / " +
                                 $"{NameOffButton}({(offButton != null ? "찾음" : "없음")})을(를) 확보하지 못했습니다. " +
                                 "구조가 이미 다르거나 이름이 변경된 것으로 보여 이 씬은 건너뜁니다.");
                return;
            }

            // 3) MuteToggleSlot 확보(있으면 재사용, 없으면 OnButton의 형제 순서 위치에 새로 생성).
            RectTransform slot = EnsureMuteToggleSlot(containerTf, onButton.transform, tag);

            // 4) OnButton/OffButton을 slot 안으로 재부모화(파괴/재생성 없음 → fileID/참조 보존).
            //    worldPositionStays=false로 로컬 좌표를 리셋한다.
            ReparentAndStretch(onButton.transform, slot, tag, NameOnButton);
            ReparentAndStretch(offButton.transform, slot, tag, NameOffButton);

            // 5) 변경 표시 + 저장.
            EditorUtility.SetDirty(container);
            EditorUtility.SetDirty(slot.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"{tag} 구조 변경 및 저장 완료 — OnButton/OffButton이 MuteToggleSlot 안에서 완전히 겹칩니다.");
        }

        /// <summary>
        /// VolumeButtonContainer 아래의 MuteToggleSlot 래퍼를 확보한다.
        /// 이미 있으면 그대로 반환하고, 없으면 새로 만들어 OnButton이 원래 있던
        /// 형제 순서(sibling index)에 삽입한다(레이아웃 순서 유지). idempotent.
        /// MuteToggleSlot은 LayoutGroup 등 특별한 컴포넌트 없이 순수 RectTransform 컨테이너다.
        /// </summary>
        /// <param name="containerTf">VolumeButtonContainer의 Transform.</param>
        /// <param name="onButtonTf">OnButton의 현재 Transform(형제 순서 기준으로 사용).</param>
        /// <param name="tag">로그 접두사.</param>
        /// <returns>확보한 MuteToggleSlot의 RectTransform.</returns>
        private static RectTransform EnsureMuteToggleSlot(Transform containerTf, Transform onButtonTf, string tag)
        {
            // 1) 이미 컨테이너 직접 자식으로 MuteToggleSlot이 있으면 재사용(재실행 대비).
            Transform existing = containerTf.Find(NameMuteToggleSlot);
            if (existing != null)
            {
                Debug.Log($"{tag} 기존 MuteToggleSlot 재사용함.");
                return existing as RectTransform;
            }

            // 2) OnButton이 컨테이너 직접 자식이라면 그 형제 순서를, 아니라면 맨 앞을 삽입 위치로 삼는다.
            int insertIndex = (onButtonTf.parent == containerTf) ? onButtonTf.GetSiblingIndex() : 0;

            // 3) 순수 RectTransform 컨테이너로 새 오브젝트 생성.
            var slotGo = new GameObject(NameMuteToggleSlot, typeof(RectTransform));
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.SetParent(containerTf, worldPositionStays: false);
            slotRt.SetSiblingIndex(insertIndex); // OnButton이 있던 자리에 넣어 레이아웃 순서 보존.

            // 슬롯 자체는 로컬 기준 초기화(스케일 1, 위치 0). 실제 크기는 VerticalLayoutGroup이 계산한다.
            slotRt.localScale = Vector3.one;
            slotRt.anchoredPosition = Vector2.zero;

            Debug.Log($"{tag} MuteToggleSlot 신규 생성함 — 형제 순서 {insertIndex}에 삽입(OnButton 원위치).");
            return slotRt;
        }

        /// <summary>
        /// 대상 Transform을 slot 안으로 재부모화하고, RectTransform을 slot 전체를
        /// 꽉 채우도록(anchor 0~1, sizeDelta 0, anchoredPosition 0) 설정한다.
        /// GameObject를 파괴하지 않으므로 fileID/컴포넌트 참조는 그대로 유지된다.
        /// </summary>
        private static void ReparentAndStretch(Transform target, RectTransform slot, string tag, string label)
        {
            // worldPositionStays=false: 로컬 좌표를 리셋하며 재부모화(월드 위치 유지 X).
            target.SetParent(slot, worldPositionStays: false);

            var rt = target as RectTransform;
            if (rt == null)
            {
                Debug.LogWarning($"{tag} {label}에 RectTransform이 없어 스트레치 설정을 건너뜁니다.");
                return;
            }

            rt.anchorMin = Vector2.zero;        // (0,0)
            rt.anchorMax = Vector2.one;         // (1,1)
            rt.pivot = new Vector2(0.5f, 0.5f); // 중앙 피벗(겹침 기준 통일).
            rt.sizeDelta = Vector2.zero;        // (0,0) — 앵커 범위와 동일 크기.
            rt.anchoredPosition = Vector2.zero; // (0,0)
            rt.localScale = Vector3.one;

            EditorUtility.SetDirty(rt);
            Debug.Log($"{tag} {label} 재부모화 + 슬롯 꽉 채우기 완료.");
        }

        // ================================================================
        // 탐색 헬퍼
        // ================================================================

        /// <summary>씬 전체에서 이름이 일치하는 첫 GameObject를 찾는다(비활성 포함).</summary>
        private static GameObject FindDeepInScene(Scene scene, string name)
        {
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                GameObject found = FindDeep(rootGo.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>root 하위(자기 자신 포함)에서 이름이 일치하는 첫 GameObject를 DFS로 찾는다(비활성 포함).</summary>
        private static GameObject FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root.gameObject;

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
