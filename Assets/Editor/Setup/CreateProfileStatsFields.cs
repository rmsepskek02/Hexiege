// ============================================================================
// CreateProfileStatsFields.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) Unity 에디터에서 반드시 "Lobby.unity" 씬을 연다.                        │
// │  2) 상단 메뉴  Hexiege > Setup > Create Profile Stats Fields (Lobby)  실행. │
// │  3) 실행 후 씬을 저장(Ctrl+S)한다.                                          │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가:
//   - 열린 Lobby 씬에서 ProfileView 를 찾는다.
//   - ProfileView 하위에 세로 배치(VerticalLayoutGroup) 컨테이너를 만들고,
//     "라벨: 값" 형태의 전적 항목 텍스트와 닉네임 변경 버튼을 생성한다.
//   - ProfileView 의 [SerializeField] 슬롯을 SerializedObject 로 연결한다:
//       _nicknameText, _changeNicknameButton, _totalGamesText, _winsText,
//       _lossesText, _winRateText, _lastSessionText, _myRankText
//
// 주의:
//   본 스크립트는 위 8개 슬롯만 다룬다. 계정 정보/연동/로그아웃 등 나머지
//   ProfileView 슬롯은 기존 씬 구성을 그대로 두고 건드리지 않는다(작업 범위 한정).
// ============================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Lobby 씬 ProfileView 에 전적/닉네임 표시 필드를 생성·연결하는 1회성 에디터 스크립트.
    /// </summary>
    public static class CreateProfileStatsFields
    {
        private const string LightFontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // 스프라이트 경로(확정 결정 5).
        private const string BtnGoldSpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_gold.png";
        private const string BtnSkySpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_sky.png";

        [MenuItem("Hexiege/Setup/Create Profile Stats Fields (Lobby)")]
        public static void Run()
        {
            // ── 1) ProfileView 탐색 ──────────────────────────────────────
            ProfileView profileView =
                Object.FindFirstObjectByType<ProfileView>(FindObjectsInactive.Include);
            if (profileView == null)
            {
                Debug.LogError("[Setup] ProfileView 를 씬에서 찾지 못했습니다. " +
                               "Lobby.unity 씬을 연 상태에서 실행하세요.");
                return;
            }

            // ── 2) 폰트/스프라이트 로드 ──────────────────────────────────
            TMP_FontAsset lightFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LightFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (lightFont == null)
                Debug.LogWarning($"[Setup] 기본 폰트를 찾지 못했습니다: {LightFontPath}");
            Sprite goldSprite = LoadSprite(BtnGoldSpritePath);
            Sprite skySprite = LoadSprite(BtnSkySpritePath);

            // ── 3) 생성 부모 결정: LobbyProfileView/MainView 하위 ─────────
            // 기존에는 겹침(Research 2-1)이 있던 ProfileView 직속에 생성했으나,
            // 이번에는 기존 수작업 UI 트리인 MainView 하위로 재배치해 두 트리를 통합한다.
            Transform generateParent = FindDeep(profileView.transform, "MainView");
            if (generateParent == null)
            {
                Debug.LogWarning("[Setup] 'MainView' 를 찾지 못해 ProfileView 하위에 생성합니다. " +
                                 "(기존 트리와 겹칠 수 있으니 수동 확인 권장.)");
                generateParent = profileView.transform;
            }

            // ── 3-1) 세로 배치 컨테이너 생성(멱등) ───────────────────────
            GameObject container = FindOrCreateChild(generateParent, "ProfileStatsContainer");
            RectTransform containerRt = container.GetComponent<RectTransform>();
            // MainView 하위의 중상단 밴드를 비율 앵커로 차지(고정 픽셀 대신 비율, UI 규칙 2).
            //   주의: MainView 에는 기존 수작업 UI(계정 정보/로그아웃 등)가 이미 배치돼 있다.
            //   전체 스트레치로 두면 그 위를 덮을 수 있어 밴드로 제한한다. 정확한 위치는
            //   실기에서 사용자가 미세 조정한다(확정 결정 5). 완전 통합(기존 요소까지 한 VLG로
            //   재배치)은 기존 직렬화 참조를 옮기는 별도 작업이 필요하다.
            SetAnchors(containerRt, new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.93f));

            VerticalLayoutGroup vlg = EnsureVLG(container);
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            // childForceExpandHeight=false + 자식 LayoutElement 로 높이 배분(규칙 2, 메모리 교훈).
            vlg.childForceExpandHeight = false;

            // ── 4) 항목 생성 ─────────────────────────────────────────────
            // 닉네임 행: 값 텍스트 + 변경 버튼을 한 줄에 둔다.
            GameObject nickRow = FindOrCreateChild(container.transform, "NicknameRow");
            EnsureHLG(nickRow, 12f);
            SetLayoutElement(nickRow, 90f, 0f);
            TextMeshProUGUI nicknameText = CreateValueOnly(
                nickRow.transform, "NicknameValue", lightFont, lightFont, "닉네임", "-");
            GameObject changeBtnGo = FindOrCreateChild(nickRow.transform, "ChangeNicknameButton");
            Button changeNicknameButton =
                EnsureButton(changeBtnGo, boldFont, "닉네임 변경", new Color(0.30f, 0.50f, 0.75f, 1f), goldSprite);

            // 전적 헤더 행: "전적" 라벨 + 새로고침 버튼(확정 결정 2).
            GameObject statsHeaderRow = FindOrCreateChild(container.transform, "StatsHeaderRow");
            EnsureHLG(statsHeaderRow, 12f);
            SetLayoutElement(statsHeaderRow, 70f, 0f);
            GameObject statsLabelGo = FindOrCreateChild(statsHeaderRow.transform, "StatsLabel");
            EnsureText(statsLabelGo, boldFont, "전적", 30, TextAlignmentOptions.Left);
            GameObject refreshBtnGo = FindOrCreateChild(statsHeaderRow.transform, "RefreshButton");
            // 아이콘 에셋이 없어(Research 4-2) 우선 텍스트/기호로 대체한다.
            Button refreshButton =
                EnsureButton(refreshBtnGo, boldFont, "새로고침 ⟳", new Color(0.30f, 0.60f, 0.80f, 1f), skySprite);

            // 전적 항목들("라벨: 값" 행).
            TextMeshProUGUI totalGamesText =
                CreateStatRow(container.transform, "TotalGamesRow", lightFont, boldFont, "총 게임", "0");
            TextMeshProUGUI winsText =
                CreateStatRow(container.transform, "WinsRow", lightFont, boldFont, "승", "0");
            TextMeshProUGUI lossesText =
                CreateStatRow(container.transform, "LossesRow", lightFont, boldFont, "패", "0");
            TextMeshProUGUI winRateText =
                CreateStatRow(container.transform, "WinRateRow", lightFont, boldFont, "승률", "-");
            TextMeshProUGUI lastSessionText =
                CreateStatRow(container.transform, "LastSessionRow", lightFont, boldFont, "마지막 접속", "-");
            TextMeshProUGUI myRankText =
                CreateStatRow(container.transform, "MyRankRow", lightFont, boldFont, "랭킹", "-");

            // ── 5) 슬롯 연결 ─────────────────────────────────────────────
            Connect(profileView, "_nicknameText", nicknameText);
            Connect(profileView, "_changeNicknameButton", changeNicknameButton);
            Connect(profileView, "_refreshButton", refreshButton);
            Connect(profileView, "_totalGamesText", totalGamesText);
            Connect(profileView, "_winsText", winsText);
            Connect(profileView, "_lossesText", lossesText);
            Connect(profileView, "_winRateText", winRateText);
            Connect(profileView, "_lastSessionText", lastSessionText);
            Connect(profileView, "_myRankText", myRankText);

            // ── 6) Dirty 처리 ────────────────────────────────────────────
            EditorUtility.SetDirty(profileView);
            EditorSceneManager.MarkSceneDirty(profileView.gameObject.scene);

            Debug.Log("[Setup] ProfileView 전적 필드 생성 및 연결 완료. 씬을 저장하세요(Ctrl+S).");
            Selection.activeGameObject = container;
        }

        // ====================================================================
        // 항목 생성 헬퍼
        // ====================================================================

        /// <summary>"라벨: 값" 한 줄(HorizontalLayoutGroup)을 만들고 값 텍스트를 반환한다.</summary>
        private static TextMeshProUGUI CreateStatRow(
            Transform parent, string rowName, TMP_FontAsset valueFont, TMP_FontAsset labelFont,
            string label, string defaultValue)
        {
            GameObject row = FindOrCreateChild(parent, rowName);
            EnsureHLG(row, 12f);
            SetLayoutElement(row, 60f, 0f); // 전적 행 선호높이(균등 큰 높이 방지, 규칙 2).
            return CreateValueOnly(row.transform, "Value", valueFont, labelFont, label, defaultValue);
        }

        /// <summary>
        /// 한 행 안에 라벨(고정 문구) + 값(런타임 갱신 대상) 두 텍스트를 만든다.
        /// 값 텍스트만 반환해 슬롯에 연결한다.
        /// </summary>
        private static TextMeshProUGUI CreateValueOnly(
            Transform rowParent, string valueName, TMP_FontAsset valueFont, TMP_FontAsset labelFont,
            string label, string defaultValue)
        {
            // 라벨(왼쪽)
            GameObject labelGo = FindOrCreateChild(rowParent, "Label");
            TextMeshProUGUI labelText =
                EnsureText(labelGo, labelFont, label + " :", 28, TextAlignmentOptions.Left);
            labelText.color = new Color(0.75f, 0.80f, 0.90f, 1f);

            // 값(오른쪽) — 이 텍스트가 슬롯에 연결된다.
            GameObject valueGo = FindOrCreateChild(rowParent, valueName);
            return EnsureText(valueGo, valueFont, defaultValue, 30, TextAlignmentOptions.Left);
        }

        // ====================================================================
        // 공통 UI 헬퍼
        // ====================================================================

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static VerticalLayoutGroup EnsureVLG(GameObject go)
        {
            if (!go.TryGetComponent(out VerticalLayoutGroup vlg))
                vlg = go.AddComponent<VerticalLayoutGroup>();
            return vlg;
        }

        private static HorizontalLayoutGroup EnsureHLG(GameObject go, float spacing)
        {
            if (!go.TryGetComponent(out HorizontalLayoutGroup hlg))
                hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            return hlg;
        }

        private static TextMeshProUGUI EnsureText(
            GameObject go, TMP_FontAsset font, string text, int fontSize, TextAlignmentOptions align)
        {
            if (!go.TryGetComponent(out TextMeshProUGUI t))
                t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        private static Button EnsureButton(GameObject go, TMP_FontAsset font, string label, Color bg, Sprite sprite)
        {
            if (!go.TryGetComponent(out Image img))
                img = go.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = bg;
            }

            if (!go.TryGetComponent(out Button btn))
                btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject labelGo = FindOrCreateChild(go.transform, "Label");
            SetStretch(labelGo.GetComponent<RectTransform>());
            EnsureText(labelGo, font, label, 28, TextAlignmentOptions.Center);
            return btn;
        }

        /// <summary>
        /// LayoutGroup 자식으로서의 선호높이/유연높이 지정(고정 픽셀 대신 비율/선호값, UI 규칙 2).
        /// </summary>
        private static void SetLayoutElement(GameObject go, float preferredHeight, float flexibleHeight)
        {
            if (!go.TryGetComponent(out LayoutElement le))
                le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.flexibleHeight = flexibleHeight;
        }

        /// <summary>이름으로 자손(재귀)을 찾는다. 없으면 null.</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (child != null && child.name == name)
                    return child;
            }
            return null;
        }

        /// <summary>스프라이트를 로드한다. 없으면 경고 후 null(단색 폴백).</summary>
        private static Sprite LoadSprite(string path)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null)
                Debug.LogWarning($"[Setup] 스프라이트를 찾지 못해 단색으로 대체합니다: {path}");
            return s;
        }

        /// <summary>
        /// private [SerializeField] 슬롯을 SerializedObject 로 연결한다(런타임 private 필드 대응).
        /// </summary>
        private static void Connect(Component target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Setup] {target.GetType().Name} 에 '{fieldName}' 필드가 없습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
