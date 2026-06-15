// ============================================================================
// FixLobbyRuleViolations.cs
// Lobby/Login 씬 규칙 위반 3건을 일괄 수정하는 1회성 에디터 스크립트.
//
// 배경:
//   씬 전수 점검에서 다음 3가지 위반이 발견되었다.
//     ① LobbyUI._lobbyPanel        : SetActive 기반 가시성 제어 (규칙 5 위반) — 현재 씬 미배치
//     ② AnonymousWarningPopup._blockingOverlay : SetActive 기반 제어 (규칙 5 위반) — Login.unity에 배치
//     ③ LoadingScreen > StatusText : LiberationSans 폰트 사용     (규칙 6 위반) — Lobby.unity에 배치
//
//   ①, ②는 코드(LobbyUI.cs / AnonymousWarningPopup.cs)에서 필드 타입이
//   GameObject → CanvasGroup 으로 변경되었다. 타입이 바뀌면 Unity 직렬화 참조가
//   끊기므로, 씬에서 해당 오브젝트를 찾아 CanvasGroup을 추가하고 Inspector 필드를
//   다시 연결해 주어야 한다. 이 스크립트가 그 작업을 자동화한다.
//
//   ③은 TMP 폰트 에셋을 허용 폰트인 Maplestory Light SDF로 교체한다.
//
// 사용법:
//   ② 수정: Unity 에디터에서 Login.unity 씬을 연 상태로 메뉴 실행
//   ③ 수정: Lobby.unity 씬을 연 상태로 메뉴 실행
//   메뉴 → Hexiege/Setup/Lobby 규칙 위반 수정
//   실행이 끝나면 안내 다이얼로그가 뜨고, Ctrl+S로 씬을 저장하면 된다.
//
// 이 스크립트는 1회성 유틸리티이며, 실행 후 삭제해도 무방하다.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Lobby.unity 씬의 규칙 5(CanvasGroup) / 규칙 6(폰트) 위반을 일괄 수정하는
    /// 1회성 에디터 유틸리티.
    /// </summary>
    public static class FixLobbyRuleViolations
    {
        // 이 스크립트가 실행 가능한 씬 이름 목록.
        // ②(AnonymousWarningPopup)는 Login.unity에, ③(StatusText 폰트)는 Lobby.unity에 배치돼 있으므로
        // 두 씬에서 모두 실행할 수 있어야 한다.
        private static readonly System.Collections.Generic.HashSet<string> AllowedScenes
            = new System.Collections.Generic.HashSet<string> { "Lobby", "Login" };

        // 허용 폰트(Maplestory Light SDF)의 GUID. AssetDatabase로 경로를 역추적하는 데 사용한다.
        private const string MaplestoryLightGuid = "58c71976882d99940aedcaa81b1248c5";

        /// <summary>
        /// 메뉴 진입점. 현재 열린 씬이 Lobby인지 검증한 뒤 3건의 수정을 순서대로 수행한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Lobby 규칙 위반 수정")]
        public static void Fix()
        {
            // ── 0) 현재 씬 검증 ──────────────────────────────────────────────
            // Lobby.unity 또는 Login.unity에서만 실행 가능. 다른 씬에서는 중단.
            // (② AnonymousWarningPopup → Login.unity, ③ StatusText 폰트 → Lobby.unity)
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!AllowedScenes.Contains(scene.name))
            {
                EditorUtility.DisplayDialog(
                    "잘못된 씬",
                    $"현재 씬은 '{scene.name}' 입니다.\n" +
                    "Lobby.unity 또는 Login.unity 씬을 연 상태에서 실행하세요.",
                    "확인");
                Debug.LogError($"[FixLobbyRuleViolations] 허용되지 않은 씬: '{scene.name}'. 작업을 중단합니다.");
                return;
            }

            // ── 1) LobbyUI._lobbyPanel CanvasGroup 추가 + 재연결 ─────────────
            FixLobbyPanel();

            // ── 2) AnonymousWarningPopup._blockingOverlay CanvasGroup 추가 + 재연결 ──
            FixBlockingOverlay();

            // ── 3) LoadingScreen > StatusText 폰트 교체 ─────────────────────
            FixStatusTextFont();

            // ── 4) 씬을 dirty로 표시 (사용자가 직접 저장) ─────────────────────
            EditorSceneManager.MarkSceneDirty(scene);

            // ── 5) 완료 안내 ────────────────────────────────────────────────
            EditorUtility.DisplayDialog(
                "Lobby 규칙 위반 수정",
                "완료! 씬을 저장하세요 (Ctrl+S).",
                "확인");
            Debug.Log("[FixLobbyRuleViolations] 수정 완료. 씬을 저장하세요 (Ctrl+S).");
        }

        // ====================================================================
        // ① LobbyUI._lobbyPanel
        // ====================================================================

        /// <summary>
        /// LobbyUI의 _lobbyPanel 필드(타입 변경으로 끊김)에 LobbyPanel 오브젝트의
        /// CanvasGroup을 추가·재연결한다. 로비 패널은 기본 표시 상태이므로 alpha=1로 초기화한다.
        /// </summary>
        private static void FixLobbyPanel()
        {
            // LobbyUI 컴포넌트를 씬에서 찾는다 (비활성 포함).
            LobbyUI lobbyUI = Object.FindFirstObjectByType<LobbyUI>(FindObjectsInactive.Include);
            if (lobbyUI == null)
            {
                Debug.LogError("[FixLobbyRuleViolations] LobbyUI 컴포넌트를 찾지 못했습니다. _lobbyPanel 재연결을 건너뜁니다.");
                return;
            }

            // 필드 타입이 GameObject → CanvasGroup으로 바뀌면 기존 연결이 끊기므로
            // 이름("LobbyPanel")으로 직접 오브젝트를 찾는다.
            GameObject panelGo = FindByName("LobbyPanel");
            if (panelGo == null)
            {
                Debug.LogError("[FixLobbyRuleViolations] 'LobbyPanel' 오브젝트를 찾지 못했습니다. _lobbyPanel 재연결을 건너뜁니다.");
                return;
            }

            // CanvasGroup 확보 후 표시 상태(기본값)로 초기화.
            CanvasGroup cg = EnsureCanvasGroup(panelGo);
            cg.alpha = 1f;             // 완전 불투명 = 표시
            cg.blocksRaycasts = true;  // 클릭/터치 수신
            cg.interactable = true;    // 버튼 등 상호작용 허용
            EditorUtility.SetDirty(panelGo);

            // SerializedObject로 _lobbyPanel 필드에 CanvasGroup 참조를 재할당한다.
            AssignReference(lobbyUI, "_lobbyPanel", cg);
            Debug.Log("[FixLobbyRuleViolations] LobbyUI._lobbyPanel 재연결 완료 (alpha=1, 표시 상태).");
        }

        // ====================================================================
        // ② AnonymousWarningPopup._blockingOverlay
        // ====================================================================

        /// <summary>
        /// AnonymousWarningPopup의 _blockingOverlay 필드에 차단 오버레이 오브젝트의
        /// CanvasGroup을 추가·재연결한다. 오버레이는 기본 숨김이므로 alpha=0으로 초기화한다.
        /// </summary>
        private static void FixBlockingOverlay()
        {
            AnonymousWarningPopup popup =
                Object.FindFirstObjectByType<AnonymousWarningPopup>(FindObjectsInactive.Include);
            if (popup == null)
            {
                Debug.LogError("[FixLobbyRuleViolations] AnonymousWarningPopup 컴포넌트를 찾지 못했습니다. _blockingOverlay 재연결을 건너뜁니다.");
                return;
            }

            // 팝업 자식 계층에서 차단 오버레이 오브젝트를 탐색한다.
            // 이름이 "BlockingOverlay" 또는 "Overlay" 인 자손을 우선 사용한다.
            GameObject overlayGo =
                FindDescendantByName(popup.transform, "BlockingOverlay") ??
                FindDescendantByName(popup.transform, "Overlay");

            if (overlayGo == null)
            {
                Debug.LogWarning("[FixLobbyRuleViolations] AnonymousWarningPopup 아래에서 'BlockingOverlay'/'Overlay' 오브젝트를 찾지 못했습니다. _blockingOverlay 재연결을 건너뜁니다.");
                return;
            }

            // CanvasGroup 확보 후 숨김 상태(기본값)로 초기화.
            CanvasGroup cg = EnsureCanvasGroup(overlayGo);
            cg.alpha = 0f;             // 완전 투명 = 숨김
            cg.blocksRaycasts = false; // 클릭 통과
            cg.interactable = false;   // 상호작용 차단
            EditorUtility.SetDirty(overlayGo);

            AssignReference(popup, "_blockingOverlay", cg);
            Debug.Log($"[FixLobbyRuleViolations] AnonymousWarningPopup._blockingOverlay 재연결 완료 ('{overlayGo.name}', alpha=0, 숨김 상태).");
        }

        // ====================================================================
        // ③ LoadingScreen > SafeAreaContainer > StatusText 폰트 교체
        // ====================================================================

        /// <summary>
        /// LoadingScreen의 StatusText TMP 폰트를 허용 폰트(Maplestory Light SDF)로 교체한다.
        /// </summary>
        private static void FixStatusTextFont()
        {
            // 계층: LoadingScreen → SafeAreaContainer → StatusText
            GameObject loadingScreen = FindByName("LoadingScreen");
            if (loadingScreen == null)
            {
                Debug.LogError("[FixLobbyRuleViolations] 'LoadingScreen' 오브젝트를 찾지 못했습니다. 폰트 교체를 건너뜁니다.");
                return;
            }

            Transform statusTextTr = loadingScreen.transform.Find("SafeAreaContainer/StatusText");
            if (statusTextTr == null)
            {
                Debug.LogError("[FixLobbyRuleViolations] 'LoadingScreen/SafeAreaContainer/StatusText' 경로를 찾지 못했습니다. 폰트 교체를 건너뜁니다.");
                return;
            }

            var tmp = statusTextTr.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogError("[FixLobbyRuleViolations] StatusText에 TextMeshProUGUI 컴포넌트가 없습니다. 폰트 교체를 건너뜁니다.");
                return;
            }

            // GUID → 에셋 경로 → 폰트 에셋 로드.
            string fontPath = AssetDatabase.GUIDToAssetPath(MaplestoryLightGuid);
            if (string.IsNullOrEmpty(fontPath))
            {
                Debug.LogError($"[FixLobbyRuleViolations] Maplestory Light SDF 폰트(GUID={MaplestoryLightGuid})를 찾지 못했습니다. 폰트 교체를 건너뜁니다.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font == null)
            {
                Debug.LogError($"[FixLobbyRuleViolations] '{fontPath}'를 TMP_FontAsset으로 로드하지 못했습니다. 폰트 교체를 건너뜁니다.");
                return;
            }

            Undo.RecordObject(tmp, "Fix StatusText Font");
            tmp.font = font;
            EditorUtility.SetDirty(tmp);
            Debug.Log($"[FixLobbyRuleViolations] StatusText 폰트를 '{font.name}'(으)로 교체했습니다.");
        }

        // ====================================================================
        // 공용 헬퍼
        // ====================================================================

        /// <summary>
        /// 씬 내 모든 Transform(비활성 포함)에서 지정한 이름의 오브젝트를 찾는다.
        /// 못 찾으면 null.
        /// </summary>
        /// <param name="name">찾을 오브젝트 이름.</param>
        private static GameObject FindByName(string name)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }

        /// <summary>
        /// 지정한 부모(root) 아래 자손 계층에서 이름이 일치하는 첫 오브젝트를 찾는다.
        /// 비활성 자손도 포함한다. 못 찾으면 null.
        /// </summary>
        /// <param name="root">탐색 시작 Transform.</param>
        /// <param name="name">찾을 자손 이름.</param>
        private static GameObject FindDescendantByName(Transform root, string name)
        {
            // GetComponentsInChildren은 자기 자신도 포함하므로, 이름 비교로 자기 자신은 자연히 제외된다.
            Transform[] children = root.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform t in children)
            {
                if (t == root) continue;       // 자기 자신 제외
                if (t.name == name) return t.gameObject;
            }
            return null;
        }

        /// <summary>
        /// 지정한 GameObject에서 CanvasGroup을 가져오거나 없으면 추가한다.
        /// </summary>
        /// <param name="go">대상 GameObject.</param>
        /// <returns>확보된 CanvasGroup.</returns>
        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>
        /// SerializedObject/SerializedProperty를 사용해 컴포넌트의 특정 필드에
        /// 오브젝트 참조를 재할당한다. (직접 필드 접근 대신 직렬화 API 사용)
        /// </summary>
        /// <param name="target">대상 컴포넌트(MonoBehaviour).</param>
        /// <param name="fieldName">재할당할 직렬화 필드 이름(언더스코어 포함).</param>
        /// <param name="reference">할당할 Unity 오브젝트 참조.</param>
        private static void AssignReference(Object target, string fieldName, Object reference)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[FixLobbyRuleViolations] '{target.GetType().Name}'에서 '{fieldName}' 프로퍼티를 찾지 못했습니다.");
                return;
            }

            prop.objectReferenceValue = reference;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
}
