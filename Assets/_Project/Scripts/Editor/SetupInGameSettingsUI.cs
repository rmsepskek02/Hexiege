// ============================================================================
// SetupInGameSettingsUI.cs
// 인게임 설정 메뉴(InGameSettingsPanel)와 범용 확인 팝업(ConfirmPopup)을
// Game.unity 씬에 자동 생성하고 모든 Inspector 참조를 배선하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/InGame Settings UI 설정
//
// 실행 전 준비:
//   1. Game.unity 씬을 열어둘 것.
//   2. 씬에 [UI] 이름의 Canvas와 그 하위 "Background", "GameHUD"가 존재해야 함.
//
// 자동 수행 단계:
//   [1] [UI] Canvas / Background / GameHUD 탐색.
//   [2] GameHUD 재배치:
//       - StatsPanel(VerticalLayoutGroup) 생성 후 골드/인구/타일카운트 텍스트 이동.
//       - SettingsButton(우측 상단) 생성, ui_icon_lock.png 아이콘 사용.
//   [3] InGameSettingsPanel 생성: 풀스크린 루트 + center Panel(600×700).
//       - CloseButton(우측 상단 X), SoundButton, ForfeitButton 배치.
//       - AnimatedPanel(PopupFade), CanvasGroup, InGameSettingsUI 컴포넌트 부착.
//   [4] ConfirmPopup 생성: 풀스크린 루트 + BlockingOverlay + center Panel(550×350).
//       - MessageText, ConfirmButton, CancelButton 배치.
//       - AnimatedPanel(PopupFade), CanvasGroup, ConfirmPopup 컴포넌트 부착.
//   [5] GameBootstrapper의 _inGameSettingsUI, _confirmPopup 자동 연결.
//   [6] GameHudUI의 _settingsButton, _settingsUI 자동 연결.
//   [7] 씬 Dirty 마킹 + 에셋 저장.
//
// 반응형 UI 제약:
//   Canvas Scaler가 Reference Resolution 1080×1920 기준이므로
//   모든 RectTransform은 앵커/피벗/오프셋 기반으로 설정한다.
//   절대 픽셀 좌표를 직접 기입하지 않는다.
//
// 재실행 안전성:
//   InGameSettingsPanel / ConfirmPopup이 이미 존재하면 새로 만들지 않고
//   필드 재배선만 수행한다.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hexiege.Presentation;
using Hexiege.Bootstrap;

public static class SetupInGameSettingsUI
{
    // ====================================================================
    // 에셋 경로 (변경 시 한 곳만 수정)
    // ====================================================================

    private const string PathPanelLight   = "Assets/_Project/Sprites/UI/Panels/ui_panel_light.png";
    private const string PathBtnGold      = "Assets/_Project/Sprites/UI/Buttons/ui_btn_gold_normal.png";
    private const string PathBtnCancel    = "Assets/_Project/Sprites/UI/Buttons/ui_btn_cancel.png";
    private const string PathIconLock     = "Assets/_Project/Sprites/UI/Icons/ui_icon_lock.png";
    private const string PathFontMaple    = "Assets/_Project/Fonts/Maplestory Light SDF.asset";

    // ====================================================================
    // 메뉴 진입점
    // ====================================================================

    [MenuItem("Hexiege/Setup/InGame Settings UI 설정")]
    private static void Setup()
    {
        // ── [1] 씬 컴포넌트 탐색 ─────────────────────────────────────────
        Canvas uiCanvas = FindUICanvas();
        if (uiCanvas == null)
        {
            Debug.LogError("[Setup] [UI] Canvas를 씬에서 찾을 수 없습니다. " +
                           "Game.unity 씬을 열고 다시 실행해주세요.");
            return;
        }

        Transform canvasTf = uiCanvas.transform;

        Transform backgroundTf = FindChildByName(canvasTf, "Background");
        if (backgroundTf == null)
        {
            Debug.LogError("[Setup] Canvas 하위에 'Background' GameObject가 없습니다.");
            return;
        }

        Transform gameHudTf = FindChildByName(canvasTf, "GameHUD");
        if (gameHudTf == null)
        {
            Debug.LogError("[Setup] Canvas 하위에 'GameHUD' GameObject가 없습니다.");
            return;
        }

        // 에셋 로드
        Sprite panelLightSprite = LoadAssetAt<Sprite>(PathPanelLight);
        Sprite btnGoldSprite    = LoadAssetAt<Sprite>(PathBtnGold);
        Sprite btnCancelSprite  = LoadAssetAt<Sprite>(PathBtnCancel);
        Sprite iconLockSprite   = LoadAssetAt<Sprite>(PathIconLock);
        TMP_FontAsset mapleFont = LoadAssetAt<TMP_FontAsset>(PathFontMaple);

        if (panelLightSprite == null || btnGoldSprite == null ||
            btnCancelSprite == null || iconLockSprite == null || mapleFont == null)
        {
            Debug.LogError("[Setup] 필수 에셋 로드 실패. 경로를 확인해주세요.");
            return;
        }

        // ── [2] GameHUD 재배치 ────────────────────────────────────────────
        Button settingsButton = SetupGameHud(gameHudTf, iconLockSprite);

        // ── [3] InGameSettingsPanel 생성 ─────────────────────────────────
        InGameSettingsUI settingsUI = SetupInGameSettingsPanel(
            canvasTf, backgroundTf, panelLightSprite, btnGoldSprite, btnCancelSprite, mapleFont);

        // ── [4] ConfirmPopup 생성 ────────────────────────────────────────
        ConfirmPopup confirmPopup = SetupConfirmPopup(
            canvasTf, panelLightSprite, btnGoldSprite, btnCancelSprite, mapleFont);

        // InGameSettingsUI에 ConfirmPopup 참조 연결
        if (settingsUI != null && confirmPopup != null)
        {
            SerializedObject suSo = new SerializedObject(settingsUI);
            suSo.Update();
            SetRef(suSo, "_confirmPopup", confirmPopup);
            suSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(settingsUI);
        }

        // ── [5] GameBootstrapper 연결 ────────────────────────────────────
        WireGameBootstrapper(settingsUI, confirmPopup);

        // ── [6] GameHudUI 연결 ───────────────────────────────────────────
        WireGameHudUI(settingsButton, settingsUI);

        // ── [7] 씬 저장 ───────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(uiCanvas.gameObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Setup] InGame Settings UI 설정 완료. " +
                  "씬을 저장하세요 (Ctrl+S).");
    }

    // ========================================================================
    // [2] GameHUD 재배치
    // ========================================================================

    /// <summary>
    /// GameHUD를 올바른 구조로 재배치한다.
    ///
    /// 목표 구조:
    ///   GameHUD
    ///     StatsPanel (VerticalLayoutGroup, 좌상단)
    ///       GoldRow       (HLG) → GoldImage + GoldText
    ///       PopulationRow (HLG) → PopulationImage + PopulationText
    ///       BlueRow       (HLG) → BlueHexTile + BlueHexTileText
    ///       RedRow        (HLG) → RedHexTile + RedHexTileText
    ///     SettingsButton (우상단)
    ///     Horizontal → SetActive(false) [비워진 구 컨테이너]
    ///
    /// 기존 'Horizontal' 컨테이너의 아이콘/텍스트를 각 Row로 이동하고,
    /// Horizontal은 비활성화한다.
    /// </summary>
    /// <returns>생성/재사용된 SettingsButton (GameHudUI 배선용).</returns>
    private static Button SetupGameHud(Transform gameHudTf, Sprite lockSprite)
    {
        // [2-A] 이동 대상 오브젝트 탐색 (GameHUD 서브트리 전체 DFS)
        // GoldText/PopulationText는 이전 스크립트 실행으로 StatsPanel 안에 있을 수 있으므로
        // gameHudTf 전체에서 재귀 탐색한다.
        Transform goldImageTf  = FindChildByName(gameHudTf, "GoldImage");
        Transform goldTextTf   = FindChildByName(gameHudTf, "GoldText");
        Transform popImageTf   = FindChildByName(gameHudTf, "PopulationImage");
        Transform popTextTf    = FindChildByName(gameHudTf, "PopulationText");
        Transform blueHexTf    = FindChildByName(gameHudTf, "BlueHexTile");
        Transform blueTextTf   = FindChildByName(gameHudTf, "BlueHexTileText");
        Transform redHexTf     = FindChildByName(gameHudTf, "RedHexTile");
        Transform redTextTf    = FindChildByName(gameHudTf, "RedHexTileText");
        Transform horizontalTf = FindChildByName(gameHudTf, "Horizontal");

        // [2-B] StatsPanel 생성 또는 재사용
        Transform statsPanelTf = FindChildByName(gameHudTf, "StatsPanel");
        if (statsPanelTf == null)
        {
            GameObject statsPanelGO = new GameObject("StatsPanel", typeof(RectTransform));
            statsPanelGO.transform.SetParent(gameHudTf, false);
            statsPanelTf = statsPanelGO.transform;
            Debug.Log("[Setup] StatsPanel 생성.");
        }

        // StatsPanel RectTransform: 앵커 top-left, pivot(0,1), 좌상단 (20,-20) 오프셋
        // sizeDelta = (0,0): ContentSizeFitter가 크기 자동 결정
        RectTransform statsPanelRt = statsPanelTf.GetComponent<RectTransform>();
        statsPanelRt.anchorMin        = new Vector2(0f, 1f);
        statsPanelRt.anchorMax        = new Vector2(0f, 1f);
        statsPanelRt.pivot            = new Vector2(0f, 1f);
        statsPanelRt.anchoredPosition = new Vector2(20f, -20f);
        statsPanelRt.sizeDelta        = Vector2.zero;

        // VerticalLayoutGroup: 4개 Row를 세로로 쌓음
        VerticalLayoutGroup vlg = statsPanelTf.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = statsPanelTf.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = 8f;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = false;
        vlg.childControlHeight     = false;
        vlg.childAlignment         = TextAnchor.UpperLeft;

        // ContentSizeFitter: 콘텐츠 크기에 맞춰 StatsPanel 자동 리사이즈
        ContentSizeFitter statsCsf = statsPanelTf.GetComponent<ContentSizeFitter>();
        if (statsCsf == null) statsCsf = statsPanelTf.gameObject.AddComponent<ContentSizeFitter>();
        statsCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        statsCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // [2-C] 4개 Row 생성 + 아이콘/텍스트 이동
        // 각 Row = HorizontalLayoutGroup(아이콘 + 텍스트 가로 배치)
        MoveToRow(statsPanelTf, "GoldRow",       goldImageTf, goldTextTf);
        MoveToRow(statsPanelTf, "PopulationRow", popImageTf,  popTextTf);
        MoveToRow(statsPanelTf, "BlueRow",       blueHexTf,   blueTextTf);
        MoveToRow(statsPanelTf, "RedRow",        redHexTf,    redTextTf);

        EditorUtility.SetDirty(statsPanelTf.gameObject);

        // [2-D] 기존 Horizontal 컨테이너 비활성화 (자식이 모두 이동됨)
        if (horizontalTf != null && horizontalTf.gameObject.activeSelf)
        {
            horizontalTf.gameObject.SetActive(false);
            EditorUtility.SetDirty(horizontalTf.gameObject);
            Debug.Log("[Setup] Horizontal 컨테이너 비활성화.");
        }

        // [2-E] SettingsButton 생성 또는 재사용 (우상단, 40×40)
        Transform settingsBtnTf = FindChildByName(gameHudTf, "SettingsButton");
        Button settingsButton;

        if (settingsBtnTf == null)
        {
            GameObject btnGO = new GameObject("SettingsButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(gameHudTf, false);

            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta        = new Vector2(40f, 40f);

            Image img = btnGO.GetComponent<Image>();
            img.sprite = lockSprite;
            img.preserveAspect = true;

            settingsButton = btnGO.GetComponent<Button>();
            Debug.Log("[Setup] SettingsButton 생성 완료.");
        }
        else
        {
            settingsButton = settingsBtnTf.GetComponent<Button>();
            // 이슈4: 이미 존재하는 버튼도 크기 업데이트
            RectTransform rt = settingsBtnTf.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(40f, 40f);
            EditorUtility.SetDirty(settingsBtnTf.gameObject);
            Debug.Log("[Setup] SettingsButton 재사용 - 크기 40×40 업데이트.");
        }

        return settingsButton;
    }

    /// <summary>
    /// StatsPanel 안에 rowName Row를 생성(또는 재사용)하고
    /// iconTf와 textTf를 그 안으로 이동시킨다.
    /// 각 Row는 HorizontalLayoutGroup으로 아이콘+텍스트를 한 행에 배치한다.
    /// </summary>
    private static void MoveToRow(Transform statsPanelTf, string rowName,
        Transform iconTf, Transform textTf)
    {
        // Row 생성 또는 재사용
        Transform rowTf = FindChildByName(statsPanelTf, rowName);
        if (rowTf == null)
        {
            GameObject rowGO = new GameObject(rowName, typeof(RectTransform));
            rowGO.transform.SetParent(statsPanelTf, false);
            rowTf = rowGO.transform;
        }

        // HorizontalLayoutGroup: 아이콘과 텍스트를 가로로 나란히 배치
        HorizontalLayoutGroup hlg = rowTf.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = rowTf.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 8f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;

        // ContentSizeFitter: Row 크기를 자식 크기에 자동 맞춤
        ContentSizeFitter csf = rowTf.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = rowTf.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // 아이콘 이동 (이미 Row 안에 있으면 건너뜀)
        if (iconTf != null && iconTf.parent != rowTf)
        {
            iconTf.SetParent(rowTf, false);
            RectTransform iconRt = iconTf.GetComponent<RectTransform>();
            if (iconRt != null) iconRt.anchoredPosition = Vector2.zero;
            Debug.Log($"[Setup] '{iconTf.name}' → {rowName}");
        }

        // 텍스트 이동 (이미 Row 안에 있으면 건너뜀)
        if (textTf != null && textTf.parent != rowTf)
        {
            textTf.SetParent(rowTf, false);
            RectTransform textRt = textTf.GetComponent<RectTransform>();
            if (textRt != null) textRt.anchoredPosition = Vector2.zero;
            Debug.Log($"[Setup] '{textTf.name}' → {rowName}");
        }

        EditorUtility.SetDirty(rowTf.gameObject);
    }

    // ========================================================================
    // [3] InGameSettingsPanel 생성
    // ========================================================================

    /// <summary>
    /// InGameSettingsPanel(풀스크린 루트 + 600×700 center Panel)을 Canvas 하위에 생성.
    /// Background GO의 SharedBackgroundButton, AnimatedPanel을 자동 배선한다.
    /// </summary>
    private static InGameSettingsUI SetupInGameSettingsPanel(
        Transform canvasTf, Transform backgroundTf,
        Sprite panelSprite, Sprite btnSprite, Sprite btnCancelSprite, TMP_FontAsset font)
    {
        // 이미 존재하면 재사용 + 시각 속성도 업데이트
        Transform existing = FindChildByName(canvasTf, "InGameSettingsPanel");
        if (existing != null)
        {
            InGameSettingsUI existingUI = existing.GetComponent<InGameSettingsUI>();
            if (existingUI != null)
            {
                Debug.Log("[Setup] InGameSettingsPanel이 이미 존재합니다. 속성 업데이트 후 재배선.");

                // 이슈2: 이미 존재하는 Panel의 앵커/크기 업데이트 (가로 stretch → 좌우 화면 여백 20px)
                Transform panelTf = FindChildByName(existing, "Panel");
                if (panelTf != null)
                {
                    RectTransform existingPanelRt = panelTf.GetComponent<RectTransform>();
                    if (existingPanelRt != null)
                    {
                        existingPanelRt.anchorMin        = new Vector2(0f, 0.5f);
                        existingPanelRt.anchorMax        = new Vector2(1f, 0.5f);
                        existingPanelRt.pivot            = new Vector2(0.5f, 0.5f);
                        existingPanelRt.anchoredPosition = Vector2.zero;
                        existingPanelRt.sizeDelta        = new Vector2(-40f, 650f);
                    }

                    // 이슈3: 이미 존재하는 CloseButton 스프라이트를 ui_btn_cancel로 교체
                    Transform closeTf = FindChildByName(panelTf, "CloseButton");
                    if (closeTf != null)
                    {
                        Image closeImg = closeTf.GetComponent<Image>();
                        if (closeImg != null) closeImg.sprite = btnCancelSprite;
                        EditorUtility.SetDirty(closeTf.gameObject);
                    }

                    EditorUtility.SetDirty(panelTf.gameObject);
                }

                WireInGameSettingsFields(existingUI, backgroundTf);
                return existingUI;
            }
        }

        // 루트 GO (풀스크린, CanvasGroup)
        GameObject rootGO = new GameObject("InGameSettingsPanel",
            typeof(RectTransform), typeof(CanvasGroup));
        rootGO.transform.SetParent(canvasTf, false);

        RectTransform rootRt = rootGO.GetComponent<RectTransform>();
        SetFullStretch(rootRt);

        InGameSettingsUI ui = rootGO.AddComponent<InGameSettingsUI>();

        // Panel(자식): AnimatedPanel + 배경 이미지 + 600×700 center
        GameObject panelGO = new GameObject("Panel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(rootGO.transform, false);

        RectTransform panelRt = panelGO.GetComponent<RectTransform>();
        // 이슈2: center 고정→ 가로 stretch 앵커로 변경. 좌우 화면 20px 여백, 높이 650px 고정.
        panelRt.anchorMin        = new Vector2(0f, 0.5f);
        panelRt.anchorMax        = new Vector2(1f, 0.5f);
        panelRt.pivot            = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta        = new Vector2(-40f, 650f);

        Image panelImg = panelGO.GetComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.type = Image.Type.Sliced;

        AnimatedPanel animatedPanel = panelGO.AddComponent<AnimatedPanel>();

        // Panel 자식: CloseButton (우측 상단 X) — 이슈3: ui_btn_cancel 스프라이트 사용
        GameObject closeBtnGO = CreateButton(panelGO.transform, "CloseButton",
            btnCancelSprite, "X", font, Color.black, new Vector2(80f, 80f));
        RectTransform closeRt = closeBtnGO.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot     = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-15f, -15f);

        // Panel 자식: SoundButton (중앙, sizeDelta(400×80))
        GameObject soundBtnGO = CreateButton(panelGO.transform, "SoundButton",
            btnSprite, "♪ 사운드", font, Color.black, new Vector2(400f, 80f));
        RectTransform soundRt = soundBtnGO.GetComponent<RectTransform>();
        soundRt.anchorMin = new Vector2(0.5f, 0.5f);
        soundRt.anchorMax = new Vector2(0.5f, 0.5f);
        soundRt.pivot     = new Vector2(0.5f, 0.5f);
        soundRt.anchoredPosition = new Vector2(0f, 60f);

        // Panel 자식: ForfeitButton (SoundButton 아래)
        GameObject forfeitBtnGO = CreateButton(panelGO.transform, "ForfeitButton",
            btnSprite, "게임 포기", font, Color.black, new Vector2(400f, 80f));
        RectTransform forfeitRt = forfeitBtnGO.GetComponent<RectTransform>();
        forfeitRt.anchorMin = new Vector2(0.5f, 0.5f);
        forfeitRt.anchorMax = new Vector2(0.5f, 0.5f);
        forfeitRt.pivot     = new Vector2(0.5f, 0.5f);
        forfeitRt.anchoredPosition = new Vector2(0f, -60f);

        // 패널 자체는 비활성으로 시작 (AnimatedPanel.Show()/Hide() 호출 시 활성화)
        panelGO.SetActive(false);

        // 필드 배선
        WireInGameSettingsFields(ui, backgroundTf);

        Debug.Log("[Setup] InGameSettingsPanel 생성 완료.");
        return ui;
    }

    /// <summary>
    /// InGameSettingsUI의 SerializeField들을 배선한다.
    /// _confirmPopup은 ConfirmPopup 생성 후 별도로 배선되므로 여기서는 건드리지 않는다.
    /// </summary>
    private static void WireInGameSettingsFields(InGameSettingsUI ui, Transform backgroundTf)
    {
        SerializedObject so = new SerializedObject(ui);
        so.Update();

        GameObject root = ui.gameObject;

        Transform panelTf = FindChildByName(root.transform, "Panel");
        AnimatedPanel panel = panelTf != null ? panelTf.GetComponent<AnimatedPanel>() : null;
        SetRef(so, "_panel", panel);

        SharedBackgroundButton sharedBg = backgroundTf.GetComponent<SharedBackgroundButton>();
        if (sharedBg == null)
        {
            Debug.LogWarning("[Setup] Background GO에 SharedBackgroundButton 컴포넌트가 없습니다. " +
                             "수동 연결이 필요합니다.");
        }
        SetRef(so, "_sharedBackground", sharedBg);

        // AnimatedPanel._backgroundOverlay = Background의 CanvasGroup
        // 이 연결이 없으면 Show() 시 반투명 배경이 활성화되지 않는다.
        if (panel != null)
        {
            CanvasGroup bgCanvasGroup = backgroundTf.GetComponent<CanvasGroup>();
            if (bgCanvasGroup != null)
            {
                SerializedObject animPanelSo = new SerializedObject(panel);
                animPanelSo.Update();
                SetRef(animPanelSo, "_backgroundOverlay", bgCanvasGroup);
                animPanelSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(panel);
            }
            else
            {
                Debug.LogWarning("[Setup] Background GO에 CanvasGroup이 없습니다. " +
                                 "AnimatedPanel._backgroundOverlay를 수동으로 연결해주세요.");
            }
        }

        Transform closeTf   = FindChildByName(root.transform, "CloseButton");
        Transform soundTf   = FindChildByName(root.transform, "SoundButton");
        Transform forfeitTf = FindChildByName(root.transform, "ForfeitButton");

        SetRef(so, "_closeButton",   closeTf   != null ? closeTf.GetComponent<Button>()   : null);
        SetRef(so, "_soundButton",   soundTf   != null ? soundTf.GetComponent<Button>()   : null);
        SetRef(so, "_forfeitButton", forfeitTf != null ? forfeitTf.GetComponent<Button>() : null);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
    }

    // ========================================================================
    // [4] ConfirmPopup 생성
    // ========================================================================

    /// <summary>
    /// ConfirmPopup(풀스크린 루트 + BlockingOverlay + 550×350 center Panel)을 생성.
    /// Canvas의 마지막 자식으로 추가하여 다른 팝업 위에 표시되도록 한다.
    /// </summary>
    private static ConfirmPopup SetupConfirmPopup(
        Transform canvasTf,
        Sprite panelSprite, Sprite btnSprite, Sprite cancelSprite, TMP_FontAsset font)
    {
        // 이미 존재하면 재사용
        Transform existing = FindChildByName(canvasTf, "ConfirmPopup");
        if (existing != null)
        {
            ConfirmPopup existingPopup = existing.GetComponent<ConfirmPopup>();
            if (existingPopup != null)
            {
                Debug.Log("[Setup] ConfirmPopup이 이미 존재합니다. 재배선만 수행.");
                WireConfirmPopupFields(existingPopup);
                return existingPopup;
            }
        }

        // 루트 GO
        GameObject rootGO = new GameObject("ConfirmPopup",
            typeof(RectTransform), typeof(CanvasGroup));
        rootGO.transform.SetParent(canvasTf, false);
        rootGO.transform.SetAsLastSibling(); // 다른 팝업 위에 그려지도록 최상위

        RectTransform rootRt = rootGO.GetComponent<RectTransform>();
        SetFullStretch(rootRt);

        ConfirmPopup popup = rootGO.AddComponent<ConfirmPopup>();

        // BlockingOverlay: 풀스크린 투명 Image, Raycast Target=true
        GameObject overlayGO = new GameObject("BlockingOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGO.transform.SetParent(rootGO.transform, false);

        RectTransform overlayRt = overlayGO.GetComponent<RectTransform>();
        SetFullStretch(overlayRt);

        Image overlayImg = overlayGO.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0f); // 알파 0 (시각적으로 보이지 않음)
        overlayImg.raycastTarget = true;

        overlayGO.SetActive(false); // 초기 상태는 비활성

        // Panel(자식): AnimatedPanel + 550×350 center
        GameObject panelGO = new GameObject("Panel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(rootGO.transform, false);

        RectTransform panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot     = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(550f, 350f);

        Image panelImg = panelGO.GetComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.type = Image.Type.Sliced;

        AnimatedPanel animatedPanel = panelGO.AddComponent<AnimatedPanel>();

        // MessageText: 상단 stretch, sizeDelta(0, 120), center align
        GameObject msgGO = new GameObject("MessageText",
            typeof(RectTransform), typeof(CanvasRenderer));
        msgGO.transform.SetParent(panelGO.transform, false);

        TextMeshProUGUI msgText = msgGO.AddComponent<TextMeshProUGUI>();
        msgText.font = font;
        msgText.color = Color.black;
        msgText.alignment = TextAlignmentOptions.Center;
        msgText.fontSize = 36f;
        msgText.text = "정말 진행하시겠습니까?";
        msgText.enableWordWrapping = true;

        RectTransform msgRt = msgGO.GetComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0f, 1f);
        msgRt.anchorMax = new Vector2(1f, 1f);
        msgRt.pivot     = new Vector2(0.5f, 1f);
        msgRt.anchoredPosition = new Vector2(0f, -40f);
        msgRt.sizeDelta = new Vector2(-40f, 120f); // 좌우 20px 여백

        // ButtonRow: 하단 center, HorizontalLayoutGroup
        GameObject btnRowGO = new GameObject("ButtonRow",
            typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRowGO.transform.SetParent(panelGO.transform, false);

        RectTransform btnRowRt = btnRowGO.GetComponent<RectTransform>();
        btnRowRt.anchorMin = new Vector2(0.5f, 0f);
        btnRowRt.anchorMax = new Vector2(0.5f, 0f);
        btnRowRt.pivot     = new Vector2(0.5f, 0f);
        btnRowRt.anchoredPosition = new Vector2(0f, 30f);
        btnRowRt.sizeDelta = new Vector2(400f, 80f);

        HorizontalLayoutGroup hlg = btnRowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // ConfirmButton: 확인
        GameObject confirmBtnGO = CreateButton(btnRowGO.transform, "ConfirmButton",
            btnSprite, "확인", font, Color.black, new Vector2(180f, 80f));

        // CancelButton: 취소
        GameObject cancelBtnGO = CreateButton(btnRowGO.transform, "CancelButton",
            cancelSprite, "취소", font, Color.black, new Vector2(180f, 80f));

        // Panel 자체는 비활성으로 시작
        panelGO.SetActive(false);

        // 필드 배선
        WireConfirmPopupFields(popup);

        Debug.Log("[Setup] ConfirmPopup 생성 완료.");
        return popup;
    }

    /// <summary>
    /// ConfirmPopup의 SerializeField들을 배선한다.
    /// </summary>
    private static void WireConfirmPopupFields(ConfirmPopup popup)
    {
        SerializedObject so = new SerializedObject(popup);
        so.Update();

        GameObject root = popup.gameObject;

        Transform overlayTf = FindChildByName(root.transform, "BlockingOverlay");
        Transform panelTf   = FindChildByName(root.transform, "Panel");
        Transform msgTf     = FindChildByName(root.transform, "MessageText");
        Transform confirmTf = FindChildByName(root.transform, "ConfirmButton");
        Transform cancelTf  = FindChildByName(root.transform, "CancelButton");

        // _blockingOverlay는 GameObject 타입 — objectReferenceValue에 GameObject를 그대로 할당
        SerializedProperty overlayProp = so.FindProperty("_blockingOverlay");
        if (overlayProp != null && overlayTf != null)
            overlayProp.objectReferenceValue = overlayTf.gameObject;

        SetRef(so, "_panel",
            panelTf != null ? panelTf.GetComponent<AnimatedPanel>() : null);
        SetRef(so, "_messageText",
            msgTf != null ? msgTf.GetComponent<TextMeshProUGUI>() : null);
        SetRef(so, "_confirmButton",
            confirmTf != null ? confirmTf.GetComponent<Button>() : null);
        SetRef(so, "_cancelButton",
            cancelTf != null ? cancelTf.GetComponent<Button>() : null);

        // 버튼 내부 TMP 텍스트(자식의 첫 번째 TextMeshProUGUI)
        TextMeshProUGUI confirmText = confirmTf != null
            ? confirmTf.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        TextMeshProUGUI cancelText  = cancelTf != null
            ? cancelTf.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        SetRef(so, "_confirmButtonText", confirmText);
        SetRef(so, "_cancelButtonText",  cancelText);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(popup);
    }

    // ========================================================================
    // [5] GameBootstrapper 연결
    // ========================================================================

    /// <summary>
    /// GameBootstrapper의 _inGameSettingsUI / _confirmPopup 필드 배선.
    /// </summary>
    private static void WireGameBootstrapper(InGameSettingsUI settingsUI, ConfirmPopup confirmPopup)
    {
        GameBootstrapper bs = Object.FindFirstObjectByType<GameBootstrapper>();
        if (bs == null)
        {
            Debug.LogWarning("[Setup] 씬에 GameBootstrapper가 없습니다. 수동 연결이 필요합니다.");
            return;
        }

        SerializedObject so = new SerializedObject(bs);
        so.Update();

        SetRef(so, "_inGameSettingsUI", settingsUI);
        SetRef(so, "_confirmPopup",     confirmPopup);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(bs);
        Debug.Log("[Setup] GameBootstrapper 연결 완료.");
    }

    // ========================================================================
    // [6] GameHudUI 연결
    // ========================================================================

    /// <summary>
    /// GameHudUI의 _settingsButton / _settingsUI 필드 배선.
    /// </summary>
    private static void WireGameHudUI(Button settingsButton, InGameSettingsUI settingsUI)
    {
        GameHudUI hud = Object.FindFirstObjectByType<GameHudUI>();
        if (hud == null)
        {
            Debug.LogWarning("[Setup] 씬에 GameHudUI가 없습니다. 수동 연결이 필요합니다.");
            return;
        }

        SerializedObject so = new SerializedObject(hud);
        so.Update();

        SetRef(so, "_settingsButton", settingsButton);
        SetRef(so, "_settingsUI",     settingsUI);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(hud);
        Debug.Log("[Setup] GameHudUI 연결 완료.");
    }

    // ========================================================================
    // 헬퍼: UI 생성
    // ========================================================================

    /// <summary>
    /// Button GO + Image(sprite, Sliced) + 자식 TextMeshProUGUI 라벨을 한 번에 생성.
    /// 반환값은 Button GameObject (부모 LayoutGroup에 즉시 배치 가능).
    /// </summary>
    private static GameObject CreateButton(Transform parent, string name,
        Sprite sprite, string label, TMP_FontAsset font, Color textColor, Vector2 size)
    {
        GameObject btnGO = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        Image img = btnGO.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;

        // 자식 TMP 라벨
        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        txtGO.transform.SetParent(btnGO.transform, false);

        TextMeshProUGUI tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 32f;
        tmp.text = label;
        tmp.raycastTarget = false; // 버튼 클릭이 텍스트에 막히지 않도록

        RectTransform txtRt = txtGO.GetComponent<RectTransform>();
        SetFullStretch(txtRt);

        return btnGO;
    }

    /// <summary>
    /// RectTransform을 부모 전체 영역(0,0)~(1,1)에 채우도록 설정.
    /// </summary>
    private static void SetFullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ========================================================================
    // 헬퍼: 탐색 / 에셋 로드
    // ========================================================================

    /// <summary>
    /// 씬에서 [UI] 이름의 Canvas를 탐색. 못 찾으면 첫 번째 Canvas를 폴백으로 반환.
    /// </summary>
    private static Canvas FindUICanvas()
    {
        // [UI] 이름의 Canvas 우선 탐색
        Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c.name == "[UI]") return c;
        }
        // 폴백: 첫 번째 Canvas
        return all.Length > 0 ? all[0] : null;
    }

    /// <summary>
    /// 이름으로 자식 Transform을 재귀 탐색 (DFS).
    /// </summary>
    private static Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// AssetDatabase 경로로부터 에셋을 로드. 실패 시 LogError + null 반환.
    /// </summary>
    private static T LoadAssetAt<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            Debug.LogError($"[Setup] 에셋 로드 실패: {path}");
        return asset;
    }

    /// <summary>
    /// SerializedObject의 필드에 Object 참조를 안전하게 할당.
    /// </summary>
    private static int SetRef(SerializedObject so, string fieldName, Object value)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[Setup] 프로퍼티 '{fieldName}'를 찾을 수 없습니다. " +
                             $"({so.targetObject.GetType().Name})");
            return 0;
        }
        if (value == null)
        {
            Debug.LogWarning($"[Setup] '{fieldName}'에 할당할 값이 null입니다.");
            return 0;
        }
        prop.objectReferenceValue = value;
        return 1;
    }
}
#endif
