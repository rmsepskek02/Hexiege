// ============================================================================
// CreateRankingTable.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) Unity 에디터에서 반드시 "Lobby.unity" 씬을 연다.                        │
// │  2) 상단 메뉴  Hexiege > Setup > Create Ranking Table (Lobby)  실행.        │
// │  3) 실행 후 씬을 저장(Ctrl+S)한다.                                          │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가:
//   - 열린 Lobby 씬에서 RankingView 를 찾는다.
//   - RankingView 하위에 랭킹 테이블 UI 를 구성한다:
//       · 헤더 행(HorizontalLayoutGroup) + 정렬 버튼 6개
//         (좌→우: 순위 / 닉네임 / 승률 / 게임수 / 승 / 패)  → _headerRow
//       · ScrollRect + Viewport + Content(VerticalLayoutGroup) → _scrollRect / _content
//       · 페이지네이션 행(이전 / 페이지 텍스트 / 다음)        → _prevPageButton / _pageText / _nextPageButton
//   - RankRowView 프리팹(6열 텍스트 + CanvasGroup)을
//     Assets/_Project/Prefabs/UI/RankRow.prefab 로 저장 → _rowPrefab 에 연결.
//
// 주의:
//   헤더 버튼의 좌→우 순서는 RankingView.BindHeaderButtons() 가 인덱스로 정렬 기준을
//   매핑하므로 반드시 순위·닉네임·승률·게임수·승·패 순서여야 한다.
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
    /// Lobby 씬 RankingView 에 랭킹 테이블과 RankRow 프리팹을 구성·연결하는 1회성 에디터 스크립트.
    /// </summary>
    public static class CreateRankingTable
    {
        private const string LightFontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";
        private const string RowPrefabPath = "Assets/_Project/Prefabs/UI/RankRow.prefab";

        // 스프라이트 경로(확정 결정 4·5).
        private const string PanelDarkSpritePath = "Assets/_Project/Sprites/UI/Panels/ui_panel_dark.png";
        private const string BtnSkySpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_sky.png";

        // 헤더/행 열 순서(좌→우). RankingView 정렬 매핑과 일치해야 한다.
        private static readonly string[] ColumnLabels = { "순위", "닉네임", "승률", "게임수", "승", "패" };

        // 열별 폭 비율(고정 픽셀 대신 flexibleWidth 비율, UI 규칙 2). ColumnLabels 와 같은 순서.
        //   순위 0.6 / 닉네임 2.0 / 승률 1.0 / 게임수 1.0 / 승 0.8 / 패 0.8
        private static readonly float[] ColumnWidths = { 0.6f, 2.0f, 1.0f, 1.0f, 0.8f, 0.8f };

        [MenuItem("Hexiege/Setup/Create Ranking Table (Lobby)")]
        public static void Run()
        {
            // ── 1) RankingView 탐색 ──────────────────────────────────────
            RankingView rankingView =
                Object.FindFirstObjectByType<RankingView>(FindObjectsInactive.Include);

            // RankingView 컴포넌트가 씬에 아직 없을 수 있다.
            //   RankingPanel 은 존재하지만 RankingView 스크립트가 부착되지 않은 placeholder 상태일 때.
            //   이 경우 LobbyRootView 가 참조하는 RankingPanel(또는 이름 탐색)에 컴포넌트를 새로 붙인다.
            if (rankingView == null)
            {
                GameObject rankingPanel = FindRankingPanel();
                if (rankingPanel == null)
                {
                    Debug.LogError("[Setup] RankingView 컴포넌트도, RankingPanel 오브젝트도 찾지 못했습니다. " +
                                   "Lobby.unity 씬을 연 상태에서 실행하세요.");
                    return;
                }
                rankingView = rankingPanel.AddComponent<RankingView>();
                EditorUtility.SetDirty(rankingView);
                Debug.Log($"[Setup] RankingPanel('{rankingPanel.name}')에 RankingView 컴포넌트를 새로 추가했습니다.");
            }

            // ── 2) 폰트/스프라이트 로드 ──────────────────────────────────
            TMP_FontAsset lightFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LightFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (lightFont == null)
                Debug.LogWarning($"[Setup] 기본 폰트를 찾지 못했습니다: {LightFontPath}");
            Sprite panelSprite = LoadSprite(PanelDarkSpritePath);
            Sprite skySprite = LoadSprite(BtnSkySpritePath);

            // ── 3) 전체 테이블 컨테이너(세로 3분할 + 패널 배경) ──────────
            GameObject table = FindOrCreateChild(rankingView.transform, "RankingTable");
            SetStretch(table.GetComponent<RectTransform>());

            // 테이블 전체 패널 배경(ui_panel_dark). 로비 배경 위 시인성 확보(확정 결정 4).
            if (!table.TryGetComponent(out Image tableBg))
                tableBg = table.AddComponent<Image>();
            if (panelSprite != null)
            {
                tableBg.sprite = panelSprite;
                tableBg.type = Image.Type.Sliced;
                tableBg.color = Color.white;
            }
            else
            {
                tableBg.color = new Color(0.10f, 0.12f, 0.16f, 0.92f);
            }
            tableBg.raycastTarget = true;

            VerticalLayoutGroup tableVlg = EnsureVLG(table);
            tableVlg.spacing = 8f;
            tableVlg.padding = new RectOffset(8, 8, 8, 8);
            tableVlg.childControlWidth = true;
            tableVlg.childControlHeight = true;
            tableVlg.childForceExpandWidth = true;
            tableVlg.childForceExpandHeight = false; // 자식 flexibleHeight 비율로 분배.

            // ── 4) 헤더 행 + 버튼 6개 ────────────────────────────────────
            GameObject headerRow = FindOrCreateChild(table.transform, "HeaderRow");
            EnsureHLG(headerRow, 4f);
            SetLayoutFlexibleHeight(headerRow, 1f);   // 비율 1
            for (int i = 0; i < ColumnLabels.Length; i++)
            {
                // 자식 순서를 좌→우로 고정하기 위해 이름에 인덱스를 포함.
                GameObject colGo = FindOrCreateChild(headerRow.transform, $"Col{i}_{ColumnLabels[i]}");
                colGo.transform.SetSiblingIndex(i);
                EnsureButton(colGo, boldFont, ColumnLabels[i], new Color(0.18f, 0.22f, 0.30f, 1f), 28);
                // 열 폭 차등(flexibleWidth 비율). 헤더와 행이 같은 비율을 써야 열이 어긋나지 않는다.
                SetLayoutFlexibleWidth(colGo, ColumnWidths[i]);
            }

            // ── 5) 스크롤 영역(Viewport + Content) ───────────────────────
            GameObject scrollGo = FindOrCreateChild(table.transform, "ScrollView");
            SetLayoutFlexibleHeight(scrollGo, 8f);    // 비율 8 (가장 넓게)
            if (!scrollGo.TryGetComponent(out Image scrollBg))
                scrollBg = scrollGo.AddComponent<Image>();
            // 패널 배경(table)이 이미 배경을 제공하므로 스크롤 영역 배경은 거의 투명하게 낮춘다(Plan 3-1).
            scrollBg.color = new Color(0f, 0f, 0f, 0.10f);
            if (!scrollGo.TryGetComponent(out ScrollRect scrollRect))
                scrollRect = scrollGo.AddComponent<ScrollRect>();

            // Viewport(클리핑)
            GameObject viewportGo = FindOrCreateChild(scrollGo.transform, "Viewport");
            RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
            SetStretch(viewportRt);
            viewportRt.pivot = new Vector2(0f, 1f);
            if (!viewportGo.TryGetComponent(out RectMask2D _))
                viewportGo.AddComponent<RectMask2D>();

            // Content(세로 리스트)
            GameObject contentGo = FindOrCreateChild(viewportGo.transform, "Content");
            RectTransform contentRt = contentGo.GetComponent<RectTransform>();
            // 상단 스트레치 + pivot 상단(스크롤 컨텐츠 표준 앵커).
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = new Vector2(0f, contentRt.offsetMin.y);
            contentRt.offsetMax = new Vector2(0f, contentRt.offsetMax.y);
            VerticalLayoutGroup contentVlg = EnsureVLG(contentGo);
            contentVlg.spacing = 2f;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;
            if (!contentGo.TryGetComponent(out ContentSizeFitter fitter))
                fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // ── 6) 페이지네이션 행 ───────────────────────────────────────
            GameObject pageRow = FindOrCreateChild(table.transform, "PaginationRow");
            EnsureHLG(pageRow, 12f);
            SetLayoutFlexibleHeight(pageRow, 1f);     // 비율 1

            GameObject prevGo = FindOrCreateChild(pageRow.transform, "PrevButton");
            prevGo.transform.SetSiblingIndex(0);
            Button prevButton = EnsureButton(prevGo, boldFont, "이전", new Color(0.30f, 0.34f, 0.42f, 1f), 28);

            GameObject pageTextGo = FindOrCreateChild(pageRow.transform, "PageText");
            pageTextGo.transform.SetSiblingIndex(1);
            TextMeshProUGUI pageText =
                EnsureText(pageTextGo, lightFont, "1 / 1", 30, TextAlignmentOptions.Center);

            GameObject nextGo = FindOrCreateChild(pageRow.transform, "NextButton");
            nextGo.transform.SetSiblingIndex(2);
            Button nextButton = EnsureButton(nextGo, boldFont, "다음", new Color(0.30f, 0.34f, 0.42f, 1f), 28);

            // 새로고침 버튼(확정 결정 4). 아이콘 에셋 부재(Research 4-2) → 텍스트/기호로 대체.
            GameObject refreshGo = FindOrCreateChild(pageRow.transform, "RefreshButton");
            refreshGo.transform.SetSiblingIndex(3);
            Button refreshButton =
                EnsureButton(refreshGo, boldFont, "새로고침 ⟳", new Color(0.30f, 0.60f, 0.80f, 1f), 26, skySprite);

            // ── 6-1) 빈 상태 안내 텍스트(오버레이) ───────────────────────
            // 등재 인원 0명일 때만 표시(RankingView 가 CanvasGroup 으로 토글).
            //   LayoutElement.ignoreLayout=true 로 테이블 VLG 흐름에서 제외하고 전체를 덮는 오버레이로 둔다
            //   → 숨김(alpha=0) 상태에서도 목록/페이지 레이아웃을 밀지 않는다.
            GameObject emptyGo = FindOrCreateChild(table.transform, "EmptyStateText");
            SetStretch(emptyGo.GetComponent<RectTransform>());
            TextMeshProUGUI emptyStateText = EnsureText(
                emptyGo, lightFont, "아직 랭킹이 없습니다 (20판 이상 필요)", 30, TextAlignmentOptions.Center);
            if (!emptyGo.TryGetComponent(out LayoutElement emptyLe))
                emptyLe = emptyGo.AddComponent<LayoutElement>();
            emptyLe.ignoreLayout = true;
            // 초기엔 숨김(RefreshAsync 가 데이터 로드 후 표시 여부를 결정).
            if (!emptyGo.TryGetComponent(out CanvasGroup emptyCg))
                emptyCg = emptyGo.AddComponent<CanvasGroup>();
            emptyCg.alpha = 0f;
            emptyCg.blocksRaycasts = false;
            emptyCg.interactable = false;

            // ── 7) RankRow 프리팹 확보(멱등, 매 실행 재빌드) ─────────────
            RankRowView rowPrefab = EnsureRankRowPrefab(lightFont);

            // ── 8) 슬롯 연결 ─────────────────────────────────────────────
            Connect(rankingView, "_headerRow", headerRow.transform);
            Connect(rankingView, "_scrollRect", scrollRect);
            Connect(rankingView, "_content", contentGo.transform);
            Connect(rankingView, "_prevPageButton", prevButton);
            Connect(rankingView, "_nextPageButton", nextButton);
            Connect(rankingView, "_pageText", pageText);
            Connect(rankingView, "_rowPrefab", rowPrefab);
            Connect(rankingView, "_refreshButton", refreshButton);
            Connect(rankingView, "_emptyStateText", emptyStateText);

            // ── 9) Dirty 처리 ────────────────────────────────────────────
            EditorUtility.SetDirty(rankingView);
            EditorSceneManager.MarkSceneDirty(rankingView.gameObject.scene);

            Debug.Log("[Setup] RankingView 테이블 구성 및 RankRow 프리팹 연결 완료. 씬을 저장하세요(Ctrl+S).");
            Selection.activeGameObject = table;
        }

        // ====================================================================
        // RankingPanel 탐색 (RankingView 미부착 대비)
        // ====================================================================

        /// <summary>
        /// RankingView 를 붙일 RankingPanel GameObject 를 찾는다.
        ///   1순위: LobbyRootView 의 [SerializeField] _rankingPanel 참조.
        ///   2순위: 씬에서 이름 "RankingPanel" 로 탐색(비활성 포함).
        /// 못 찾으면 null.
        /// </summary>
        private static GameObject FindRankingPanel()
        {
            // 1순위 — LobbyRootView._rankingPanel 직렬화 참조.
            LobbyRootView lobbyRoot =
                Object.FindFirstObjectByType<LobbyRootView>(FindObjectsInactive.Include);
            if (lobbyRoot != null)
            {
                var so = new SerializedObject(lobbyRoot);
                SerializedProperty prop = so.FindProperty("_rankingPanel");
                if (prop != null && prop.objectReferenceValue is GameObject panel && panel != null)
                    return panel;
            }

            // 2순위 — 이름으로 탐색(비활성 오브젝트 포함, 씬에 속한 것만).
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t != null && t.name == "RankingPanel" && t.gameObject.scene.IsValid())
                    return t.gameObject;
            }

            return null;
        }

        // ====================================================================
        // RankRow 프리팹 생성
        // ====================================================================

        /// <summary>
        /// RankRow 프리팹을 구성해 저장한다(멱등 — 매 실행 재빌드하여 같은 경로에 덮어쓴다).
        ///   경로 기반이라 GUID 가 유지되므로 씬의 _rowPrefab 참조가 깨지지 않는다.
        ///   기존 프리팹이 있어도 배경 Image(_rowBackground)/열 폭 비율 등 최신 변경을 반영하기 위해
        ///   항상 새로 만들어 덮어쓴다.
        /// </summary>
        private static RankRowView EnsureRankRowPrefab(TMP_FontAsset font)
        {
            // 임시 씬 오브젝트로 행을 구성한다(프리팹 저장 후 파괴).
            var rowGo = new GameObject("RankRow", typeof(RectTransform));

            // 행 크기: 스크롤 리스트에서 최소 높이를 갖도록 LayoutElement 지정.
            if (!rowGo.TryGetComponent(out LayoutElement le))
                le = rowGo.AddComponent<LayoutElement>();
            le.minHeight = 60f;
            le.preferredHeight = 60f;
            le.flexibleWidth = 1f;

            // 행 배경 Image(_rowBackground — 짝수행 얼룩용). 기본은 투명.
            if (!rowGo.TryGetComponent(out Image rowBg))
                rowBg = rowGo.AddComponent<Image>();
            rowBg.color = new Color(0f, 0f, 0f, 0f);
            rowBg.raycastTarget = false;

            // 6열 가로 배치.
            EnsureHLG(rowGo, 4f);

            // 행 표시/숨김용 CanvasGroup(공통 UI 규칙 5 — RankRowView.SetVisible 가 사용).
            if (!rowGo.TryGetComponent(out CanvasGroup _))
                rowGo.AddComponent<CanvasGroup>();

            if (!rowGo.TryGetComponent(out RankRowView rowView))
                rowView = rowGo.AddComponent<RankRowView>();

            // 6개 열 텍스트 생성(좌→우 순서 고정, 열 폭 비율은 헤더와 동일하게 적용).
            TextMeshProUGUI rankT = CreateCell(rowGo.transform, "RankText", font, 0, ColumnWidths[0]);
            TextMeshProUGUI nickT = CreateCell(rowGo.transform, "NicknameText", font, 1, ColumnWidths[1]);
            TextMeshProUGUI winRateT = CreateCell(rowGo.transform, "WinRateText", font, 2, ColumnWidths[2]);
            TextMeshProUGUI gamesT = CreateCell(rowGo.transform, "GamesText", font, 3, ColumnWidths[3]);
            TextMeshProUGUI winsT = CreateCell(rowGo.transform, "WinsText", font, 4, ColumnWidths[4]);
            TextMeshProUGUI lossesT = CreateCell(rowGo.transform, "LossesText", font, 5, ColumnWidths[5]);

            // RankRowView 슬롯 연결(프리팹 저장 전, 자식 내부 참조로 직렬화됨).
            Connect(rowView, "_rowBackground", rowBg);
            Connect(rowView, "_rankText", rankT);
            Connect(rowView, "_nicknameText", nickT);
            Connect(rowView, "_winRateText", winRateT);
            Connect(rowView, "_gamesText", gamesT);
            Connect(rowView, "_winsText", winsT);
            Connect(rowView, "_lossesText", lossesT);

            // 프리팹 저장(덮어쓰기) 후 임시 오브젝트 파괴.
            EnsureFolder(RowPrefabPath);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rowGo, RowPrefabPath);
            Object.DestroyImmediate(rowGo);

            if (savedPrefab == null)
            {
                Debug.LogError($"[Setup] RankRow 프리팹 저장에 실패했습니다: {RowPrefabPath}");
                return null;
            }

            return savedPrefab.GetComponent<RankRowView>();
        }

        /// <summary>한 열(셀) 텍스트를 만들고 열 폭 비율(flexibleWidth)을 지정한다.</summary>
        private static TextMeshProUGUI CreateCell(
            Transform parent, string name, TMP_FontAsset font, int siblingIndex, float widthWeight)
        {
            GameObject go = FindOrCreateChild(parent, name);
            go.transform.SetSiblingIndex(siblingIndex);
            SetLayoutFlexibleWidth(go, widthWeight);
            return EnsureText(go, font, string.Empty, 26, TextAlignmentOptions.Center);
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

        /// <summary>LayoutGroup 자식으로서 세로 비율 가중치를 지정한다(고정 픽셀 대신 비율, UI 규칙 2).</summary>
        private static void SetLayoutFlexibleHeight(GameObject go, float weight)
        {
            if (!go.TryGetComponent(out LayoutElement le))
                le = go.AddComponent<LayoutElement>();
            le.flexibleHeight = weight;
            le.flexibleWidth = 1f;
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

        private static Button EnsureButton(
            GameObject go, TMP_FontAsset font, string label, Color bg, int fontSize, Sprite sprite = null)
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
            EnsureText(labelGo, font, label, fontSize, TextAlignmentOptions.Center);
            return btn;
        }

        /// <summary>LayoutGroup 자식으로서의 가로 폭 비율을 지정한다(flexibleWidth, UI 규칙 2).</summary>
        private static void SetLayoutFlexibleWidth(GameObject go, float weight)
        {
            if (!go.TryGetComponent(out LayoutElement le))
                le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = weight;
        }

        /// <summary>스프라이트를 로드한다. 없으면 경고 후 null(단색 폴백).</summary>
        private static Sprite LoadSprite(string path)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null)
                Debug.LogWarning($"[Setup] 스프라이트를 찾지 못해 단색으로 대체합니다: {path}");
            return s;
        }

        /// <summary>프리팹 저장 경로의 폴더가 없으면 생성한다.</summary>
        private static void EnsureFolder(string assetPath)
        {
            string dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir))
                return;

            string[] parts = dir.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
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
