// ============================================================================
// RaceSelectionPreviewSetup.cs
// 종족 선택 UI 전체 환경을 자동으로 구성하는 1회성 에디터 스크립트.
//
// 메뉴 경로: Hexiege/Setup Race Selection Preview
//
// 이 스크립트가 하는 일:
//   [3D 환경]
//   1. "CharacterPreview" 레이어 추가
//   2. RenderTexture 에셋 생성 (Assets/_Project/Textures/UI/CharacterPreviewRT.renderTexture)
//   3. CharacterPreviewCamera 씬 배치 (CharacterPreview 레이어만 촬영 → RenderTexture 출력)
//   4. 종족별 캐릭터 프리팹 인스턴스 생성 — 캐러셀 위치에 배치 (3개 모두 활성)
//
//   [UI 환경]
//   5. BattleMainView 하위에 RaceSelectionView 오브젝트 생성 (반응형 앵커 기반)
//      ├─ CharacterDisplay (RawImage — RenderTexture 출력)
//      ├─ RaceNameText (TMP_Text — 종족명)
//      ├─ PrevButton (Button — 이전 종족)
//      └─ NextButton (Button — 다음 종족)
//   6. RaceSelectionView 컴포넌트 추가 + 모든 Inspector 슬롯 자동 연결
//      (캐러셀 Vector3 위치 포함)
//   7. BattleMainView._raceSelectionView 슬롯 자동 연결
//
// 실행 전 조건:
//   - Lobby.unity가 열려 있어야 함
//   - 프리팹들이 Assets/_Project/Prefabs/Units/ 경로에 있어야 함
//
// [중요] Unity Object null 체크:
//   Unity Object는 C#의 ?? 연산자가 올바르게 동작하지 않음.
//   모든 null 체크는 if (x == null) 명시적 패턴을 사용한다.
//
// 1회성 스크립트 — 실행 완료 후 삭제해도 무방.
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.IO;
using Hexiege.Presentation;

namespace Hexiege.Editor
{
    public static class RaceSelectionPreviewSetup
    {
        // ====================================================================
        // 상수 — 경로 및 설정값
        // ====================================================================

        private const string LayerName     = "CharacterPreview";
        private const string RenderTexPath = "Assets/_Project/Textures/UI/CharacterPreviewRT.renderTexture";
        private const string FontPath      = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        private const int    RT_Width      = 512;
        private const int    RT_Height     = 512;

        // 종족별 대표 캐릭터 프리팹 경로 ([0]인간 [1]정령 [2]자연)
        private static readonly string[] PrefabPaths = {
            "Assets/_Project/Prefabs/Units/Unit_Pistoleer_Blue.prefab",   // [0] 인간
            "Assets/_Project/Prefabs/Units/Unit_EmberSpirit_Blue.prefab", // [1] 정령
            "Assets/_Project/Prefabs/Units/Unit_FoxMagician_Blue.prefab", // [2] 자연
        };

        // 씬 오브젝트 이름
        private static readonly string[] CharNames = {
            "CharPreview_Human",
            "CharPreview_Spirit",
            "CharPreview_Nature",
        };

        // 캐러셀 슬롯 위치 (World Space)
        // 중앙: 카메라에 가까움 → 크게 보임, 좌/우: 카메라에서 멀어 → 작게 보임
        private static readonly Vector3 CenterPos = new Vector3(1000f,   0f, 4f);
        private static readonly Vector3 LeftPos   = new Vector3(997.5f,  0f, 7f);
        private static readonly Vector3 RightPos  = new Vector3(1002.5f, 0f, 7f);

        // 3D 카메라 위치 — 캐릭터보다 앞(Z가 작은 쪽)에서 +Z 방향으로 촬영
        private static readonly Vector3 CamPos = new Vector3(1000f, 1.2f, -1f);

        // ====================================================================
        // 메뉴 항목
        // ====================================================================

        [MenuItem("Hexiege/Setup Race Selection Preview")]
        public static void Run()
        {
            // ----------------------------------------------------------------
            // [1] CharacterPreview 레이어
            // ----------------------------------------------------------------
            int layer = EnsureLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogError("[Setup] Project Settings > Tags and Layers에 빈 슬롯이 없습니다. " +
                               "수동으로 'CharacterPreview' 레이어를 추가하고 다시 실행해주세요.");
                return;
            }
            Debug.Log($"[Setup] 레이어 준비 완료: '{LayerName}' (index={layer})");

            // ----------------------------------------------------------------
            // [2] RenderTexture 에셋
            // ----------------------------------------------------------------
            RenderTexture rt = EnsureRenderTexture(RenderTexPath, RT_Width, RT_Height);
            if (rt == null) { Debug.LogError("[Setup] RenderTexture 생성 실패."); return; }
            Debug.Log($"[Setup] RenderTexture 준비 완료: {RenderTexPath}");

            // ----------------------------------------------------------------
            // [3] CharacterPreviewCamera
            // ----------------------------------------------------------------
            EnsureCamera(layer, rt);
            Debug.Log("[Setup] CharacterPreviewCamera 설정 완료.");

            // ----------------------------------------------------------------
            // [4] 캐릭터 프리팹 인스턴스 (3개) — 캐러셀 위치에 배치, 모두 활성
            // ----------------------------------------------------------------
            GameObject previewRoot = EnsurePreviewRoot();
            GameObject[] charObjects = new GameObject[PrefabPaths.Length];

            for (int i = 0; i < PrefabPaths.Length; i++)
            {
                charObjects[i] = EnsureCharacterInstance(i, previewRoot, layer);
                if (charObjects[i] != null)
                    Debug.Log($"[Setup] 캐릭터 배치 완료: {CharNames[i]}");
                else
                    Debug.LogWarning($"[Setup] 프리팹 없음: {PrefabPaths[i]} — 슬롯 {i}는 null로 남습니다.");
            }

            // ----------------------------------------------------------------
            // [5] BattleMainView 탐색
            // ----------------------------------------------------------------
            BattleMainView battleMainView = Object.FindFirstObjectByType<BattleMainView>(
                FindObjectsInactive.Include);
            if (battleMainView == null)
            {
                Debug.LogError("[Setup] BattleMainView를 씬에서 찾을 수 없습니다. " +
                               "Lobby.unity가 열려있는지 확인하세요.");
                return;
            }

            // ----------------------------------------------------------------
            // [6] RaceSelectionView UI 오브젝트 생성 (반응형 앵커 기반)
            // ----------------------------------------------------------------
            RaceSelectionView raceView = EnsureRaceSelectionUI(battleMainView, rt, charObjects);
            Debug.Log("[Setup] RaceSelectionView UI 생성 및 슬롯 연결 완료.");

            // ----------------------------------------------------------------
            // [7] BattleMainView._raceSelectionView 슬롯 연결
            // ----------------------------------------------------------------
            SerializedObject bmvSo = new SerializedObject(battleMainView);
            SerializedProperty raceViewProp = bmvSo.FindProperty("_raceSelectionView");
            if (raceViewProp != null)
            {
                raceViewProp.objectReferenceValue = raceView;
                bmvSo.ApplyModifiedProperties();
                Debug.Log("[Setup] BattleMainView._raceSelectionView 연결 완료.");
            }
            else
            {
                Debug.LogWarning("[Setup] BattleMainView에서 '_raceSelectionView' 필드를 찾지 못했습니다. " +
                                 "수동으로 연결해주세요.");
            }

            // ----------------------------------------------------------------
            // 완료
            // ----------------------------------------------------------------
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Selection.activeGameObject = raceView.gameObject;
            Debug.Log("[Setup] 전체 설정 완료! 씬을 저장하세요 (Ctrl+S).");
        }

        // ====================================================================
        // [3] 카메라 생성/갱신
        // ====================================================================

        /// <summary>
        /// CharacterPreviewCamera를 생성하거나 기존 카메라를 갱신한다.
        /// Perspective 카메라로 설정하여 캐러셀 원근감 표현.
        /// </summary>
        private static void EnsureCamera(int layer, RenderTexture rt)
        {
            // Unity Object는 ?? 연산자가 올바르게 동작하지 않으므로 명시적 null 체크 사용
            GameObject go = GameObject.Find("CharacterPreviewCamera");
            if (go == null) go = new GameObject("CharacterPreviewCamera");

            Camera cam = go.GetComponent<Camera>();
            if (cam == null) cam = go.AddComponent<Camera>();

            // 카메라 위치: 캐릭터보다 앞(Z=-1)에서 +Z 방향으로 촬영
            go.transform.position = CamPos;
            go.transform.rotation = Quaternion.identity; // +Z 방향 촬영

            cam.cullingMask    = 1 << layer;                          // CharacterPreview 레이어만 촬영
            cam.targetTexture  = rt;
            cam.clearFlags     = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0f);   // 반투명 어두운 배경
            cam.orthographic   = false;                                // Perspective — 원근감 필수
            cam.fieldOfView    = 50f;                                  // 캐러셀 3개 캐릭터가 모두 보이는 화각
            cam.nearClipPlane  = 0.1f;
            cam.farClipPlane   = 50f;
            cam.depth          = -10;
        }

        // ====================================================================
        // [4] 캐릭터 인스턴스 생성 — 캐러셀 위치에 배치
        // ====================================================================

        /// <summary>
        /// CharacterPreviewRoot 오브젝트를 찾거나 생성한다.
        /// 모든 프리뷰 캐릭터의 부모 역할.
        /// </summary>
        private static GameObject EnsurePreviewRoot()
        {
            // Unity Object는 ?? 연산자가 올바르게 동작하지 않으므로 명시적 null 체크 사용
            GameObject root = GameObject.Find("CharacterPreviewRoot");
            if (root == null) root = new GameObject("CharacterPreviewRoot");
            root.transform.position = Vector3.zero;
            return root;
        }

        /// <summary>
        /// 종족별 캐릭터 프리팹 인스턴스를 생성하고 캐러셀 초기 위치에 배치한다.
        /// 초기 상태: Human(0) 선택 기준.
        ///   offset = (i - 0 + 3) % 3
        ///   Human(0) → offset 0 → Center (선택됨, 카메라에 가까움)
        ///   Spirit(1) → offset 1 → Right (비선택, 카메라에서 멈)
        ///   Nature(2) → offset 2 → Left (비선택, 카메라에서 멈)
        /// 모든 캐릭터는 활성 상태(SetActive(true)) — 캐러셀에서 3개 동시 표시.
        /// </summary>
        private static GameObject EnsureCharacterInstance(int index, GameObject parent, int layer)
        {
            // 이미 존재하면 재사용 (재실행 안전)
            Transform existing = parent.transform.Find(CharNames[index]);
            if (existing != null)
            {
                SetLayerRecursive(existing.gameObject, layer);
                // 캐러셀 위치로 재배치 + 항상 활성
                existing.position = GetCarouselPosition(index);
                existing.gameObject.SetActive(true);
                return existing.gameObject;
            }

            // 프리팹 로드
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[index]);
            if (prefab == null) return null;

            // 프리팹 인스턴스 생성
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            inst.name = CharNames[index];

            // 캐러셀 초기 위치 배치 (Human 선택 기준)
            inst.transform.position = GetCarouselPosition(index);
            // 카메라(+Z 방향에서 촬영) 쪽을 바라보도록 180도 회전
            inst.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            SetLayerRecursive(inst, layer);
            // 캐러셀: 모든 캐릭터 항상 활성
            inst.SetActive(true);
            return inst;
        }

        /// <summary>
        /// Human(인덱스 0) 선택 기준으로 캐러셀 초기 위치를 반환한다.
        /// offset = (index - 0 + 3) % 3 → 0=Center, 1=Right, 2=Left
        /// </summary>
        private static Vector3 GetCarouselPosition(int index)
        {
            int offset = (index + 3) % 3; // selectedIndex=0이므로 (index - 0 + 3) % 3 = index % 3
            return offset switch
            {
                0 => CenterPos,  // Human → 중앙
                1 => RightPos,   // Spirit → 오른쪽
                _ => LeftPos,    // Nature → 왼쪽
            };
        }

        // ====================================================================
        // [5~6] RaceSelectionView UI 생성 및 슬롯 연결 (반응형 앵커 기반)
        // ====================================================================

        /// <summary>
        /// BattleMainView 하위에 RaceSelectionView UI 계층을 생성하고
        /// RaceSelectionView 컴포넌트의 Inspector 슬롯을 모두 연결한다.
        /// 반응형 UI: 모든 요소가 앵커 기반으로 배치되어 해상도에 자동 적응.
        /// </summary>
        private static RaceSelectionView EnsureRaceSelectionUI(
            BattleMainView battleMainView,
            RenderTexture rt,
            GameObject[] charObjects)
        {
            // Maplestory Light SDF 폰트 로드 — 모든 TMP_Text에 적용
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogWarning($"[Setup] 폰트를 찾을 수 없습니다: {FontPath}. 기본 폰트가 사용됩니다.");

            Transform bmvTransform = battleMainView.transform;

            // ── BattleMainView가 부모를 꽉 채우도록 RectTransform 강제 설정 ────
            // 기본적으로 BattleMainView는 버튼 3개 높이만큼만 크기를 가짐.
            // RaceSelectionView가 anchorMax.y=0.5로 하단 50%를 차지하려면
            // BattleMainView가 부모(ContentArea)를 가득 채워야 한다.
            RectTransform bmvRect = battleMainView.GetComponent<RectTransform>();
            if (bmvRect != null)
            {
                bmvRect.anchorMin        = Vector2.zero;
                bmvRect.anchorMax        = Vector2.one;
                bmvRect.sizeDelta        = Vector2.zero;
                bmvRect.anchoredPosition = Vector2.zero;

                // ContentSizeFitter가 있으면 Unconstrained으로 — 앵커 기반 크기와 충돌 방지
                ContentSizeFitter sizeFitter = battleMainView.GetComponent<ContentSizeFitter>();
                if (sizeFitter != null)
                {
                    sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    sizeFitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;
                }
                Debug.Log("[Setup] BattleMainView RectTransform → 부모 전체 채우기 설정 완료.");
            }

            // 이미 RaceSelectionView가 존재하면 재사용 (재실행 안전)
            RaceSelectionView existing = bmvTransform.GetComponentInChildren<RaceSelectionView>(true);

            GameObject panelGO;
            if (existing != null)
            {
                panelGO = existing.gameObject;
                Debug.Log("[Setup] 기존 RaceSelectionView 재사용.");
            }
            else
            {
                // ── 패널 루트 ──────────────────────────────────────────────
                panelGO = new GameObject("RaceSelectionView");
                panelGO.transform.SetParent(bmvTransform, false);

                RectTransform panelRect = panelGO.AddComponent<RectTransform>();
                // 반응형 앵커: 가로는 부모 전체, 세로는 LayoutElement가 제어
                panelRect.anchorMin        = new Vector2(0f, 0f);
                panelRect.anchorMax        = new Vector2(1f, 1f);
                panelRect.pivot            = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta        = Vector2.zero;
            }

            // ── LayoutElement: BattleMainView의 VerticalLayoutGroup이 있을 때
            // 버튼 3개 아래 남은 공간을 RaceSelectionView가 모두 차지하도록 설정
            // flexibleHeight=1 → 레이아웃 그룹이 남은 공간을 이 요소에 배분
            LayoutElement le = panelGO.GetComponent<LayoutElement>();
            if (le == null) le = panelGO.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            Debug.Log("[Setup] RaceSelectionView LayoutElement(flexibleHeight=1) 설정 완료.");

            // ── RaceSelectionView 컴포넌트 ──────────────────────────────
            // Unity Object는 ?? 연산자가 올바르게 동작하지 않으므로 명시적 null 체크 사용
            RaceSelectionView raceView = panelGO.GetComponent<RaceSelectionView>();
            if (raceView == null) raceView = panelGO.AddComponent<RaceSelectionView>();

            // ── UI 자식 요소 생성 (반응형 앵커 기반) ──────────────────────

            // [A] 캐릭터 미리보기 RawImage — 패널 상단 85%의 중앙 80% 영역
            RawImage rawImage = EnsureUIElement<RawImage>(panelGO, "CharacterDisplay", ri =>
            {
                RectTransform r = ri.GetComponent<RectTransform>();
                // 반응형 앵커: 가로 10%~90%, 세로 15%~100% (패널 내 상단 대부분 차지)
                r.anchorMin = new Vector2(0.1f, 0.15f);
                r.anchorMax = new Vector2(0.9f, 1.0f);
                r.pivot     = new Vector2(0.5f, 0.5f);
                r.sizeDelta = Vector2.zero;  // 앵커에 완전히 의존
                r.anchoredPosition = Vector2.zero;
                ri.texture = rt;              // RenderTexture 연결
                ri.color   = Color.white;
            });

            // [B] 종족명 텍스트 — 패널 하단 18% 영역 중앙
            TextMeshProUGUI raceText = EnsureUIElement<TextMeshProUGUI>(panelGO, "RaceNameText", t =>
            {
                RectTransform r = t.GetComponent<RectTransform>();
                // 반응형 앵커: 가로 10%~90%, 세로 0%~18%
                r.anchorMin = new Vector2(0.1f, 0f);
                r.anchorMax = new Vector2(0.9f, 0.18f);
                r.pivot     = new Vector2(0.5f, 0.5f);
                r.sizeDelta = Vector2.zero;
                r.anchoredPosition = Vector2.zero;
                t.text      = "인간";
                t.alignment = TextAlignmentOptions.Center;
                t.fontSize  = 28f;
                t.fontStyle = FontStyles.Bold;
                t.color     = Color.white;
                if (font != null) t.font = font; // Maplestory Light SDF 폰트 적용
            });

            // [C] 왼쪽(이전) 버튼 — 패널 왼쪽 12%, 세로 30%~70% 영역
            Button prevButton = EnsureUIElement<Button>(panelGO, "PrevButton", b =>
            {
                RectTransform r = b.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(0f, 0.3f);
                r.anchorMax = new Vector2(0.12f, 0.7f);
                r.pivot     = new Vector2(0.5f, 0.5f);
                r.sizeDelta = Vector2.zero;
                r.anchoredPosition = Vector2.zero;

                // Button이 렌더링되려면 Image(targetGraphic)가 반드시 필요
                Image img = b.GetComponent<Image>();
                if (img == null) img = b.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f); // 완전 투명 — 라벨 텍스트만 보임
                b.targetGraphic = img;

                SetOrCreateButtonLabel(b.gameObject, "\u25C0", 36f, font); // ◀
            });

            // [D] 오른쪽(다음) 버튼 — 패널 오른쪽 12%, 세로 30%~70% 영역
            Button nextButton = EnsureUIElement<Button>(panelGO, "NextButton", b =>
            {
                RectTransform r = b.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(0.88f, 0.3f);
                r.anchorMax = new Vector2(1f, 0.7f);
                r.pivot     = new Vector2(0.5f, 0.5f);
                r.sizeDelta = Vector2.zero;
                r.anchoredPosition = Vector2.zero;

                // Button이 렌더링되려면 Image(targetGraphic)가 반드시 필요
                Image img = b.GetComponent<Image>();
                if (img == null) img = b.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f); // 완전 투명 — 라벨 텍스트만 보임
                b.targetGraphic = img;

                SetOrCreateButtonLabel(b.gameObject, "\u25B6", 36f, font); // ▶
            });

            // ── Inspector 슬롯 연결 (SerializedObject 사용) ─────────────
            SerializedObject so = new SerializedObject(raceView);

            // UI 컴포넌트 참조 연결
            SetObjectRef(so, "_rawImage",     rawImage);
            SetObjectRef(so, "_raceNameText", raceText);
            SetObjectRef(so, "_prevButton",   prevButton);
            SetObjectRef(so, "_nextButton",   nextButton);

            // _characterRoots 배열 연결
            SerializedProperty rootsProp = so.FindProperty("_characterRoots");
            if (rootsProp != null)
            {
                rootsProp.arraySize = charObjects.Length;
                for (int i = 0; i < charObjects.Length; i++)
                    rootsProp.GetArrayElementAtIndex(i).objectReferenceValue = charObjects[i];
            }

            // 캐러셀 슬롯 위치 Vector3 연결 — RaceSelectionView의 기본값과 동기화
            SetVector3(so, "_centerPos", CenterPos);
            SetVector3(so, "_leftPos",   LeftPos);
            SetVector3(so, "_rightPos",  RightPos);

            so.ApplyModifiedProperties();

            return raceView;
        }

        // ====================================================================
        // UI 헬퍼
        // ====================================================================

        /// <summary>
        /// parent 하위에서 name으로 찾거나 없으면 생성. 생성 시 setup 액션 실행.
        /// T가 Component일 때 이미 있는 오브젝트는 setup을 건너뜀(재실행 안전).
        /// </summary>
        private static T EnsureUIElement<T>(
            GameObject parent,
            string childName,
            System.Action<T> setup) where T : Component
        {
            Transform existing = parent.transform.Find(childName);
            if (existing != null)
            {
                // Unity Object는 ?? 연산자가 올바르게 동작하지 않으므로 명시적 null 체크 사용
                T comp = existing.GetComponent<T>();
                if (comp == null) comp = existing.gameObject.AddComponent<T>();
                return comp;
            }

            // 새 오브젝트 생성
            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent.transform, false);

            // RectTransform이 없으면 추가 (UI 요소는 RectTransform 필요)
            if (go.GetComponent<RectTransform>() == null)
                go.AddComponent<RectTransform>();

            T newComp = go.AddComponent<T>();
            setup?.Invoke(newComp);
            return newComp;
        }

        /// <summary>
        /// 버튼 오브젝트 하위에 라벨 텍스트 자식을 생성하거나 갱신.
        /// </summary>
        private static void SetOrCreateButtonLabel(
            GameObject buttonGO, string label, float fontSize, TMP_FontAsset font = null)
        {
            Transform labelTr = buttonGO.transform.Find("Label");
            TextMeshProUGUI text;

            if (labelTr == null)
            {
                GameObject labelGO = new GameObject("Label");
                labelGO.transform.SetParent(buttonGO.transform, false);

                // 라벨은 버튼 전체를 채우도록 스트레치 앵커
                RectTransform r = labelGO.AddComponent<RectTransform>();
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;

                text = labelGO.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                text = labelTr.GetComponent<TextMeshProUGUI>();
                if (text == null) text = labelTr.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.text      = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize  = fontSize;
            text.color     = Color.white;
            // Maplestory Light SDF 폰트 적용 (null이면 TMP 기본 폰트 유지)
            if (font != null) text.font = font;
        }

        /// <summary>
        /// SerializedObject에서 필드명으로 Object Reference를 설정하는 헬퍼.
        /// </summary>
        private static void SetObjectRef(SerializedObject so, string fieldName, Object value)
        {
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning($"[Setup] 필드를 찾지 못했습니다: '{fieldName}' — 수동으로 연결해주세요.");
        }

        /// <summary>
        /// SerializedObject에서 필드명으로 Vector3 값을 설정하는 헬퍼.
        /// RaceSelectionView의 캐러셀 위치 필드를 에디터에서 설정할 때 사용.
        /// </summary>
        private static void SetVector3(SerializedObject so, string fieldName, Vector3 value)
        {
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.vector3Value = value;
            else
                Debug.LogWarning($"[Setup] 필드 없음: '{fieldName}'");
        }

        // ====================================================================
        // 레이어 / RenderTexture / 폴더 헬퍼
        // ====================================================================

        /// <summary>
        /// layerName 레이어가 없으면 빈 슬롯에 추가하고 인덱스를 반환한다.
        /// 빈 슬롯이 없으면 -1 반환.
        /// </summary>
        private static int EnsureLayer(string layerName)
        {
            // 이미 존재하는 레이어 검색
            for (int i = 0; i < 32; i++)
                if (LayerMask.LayerToName(i) == layerName) return i;

            // TagManager에서 빈 슬롯 찾아 레이어 추가
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));

            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null) return -1;

            // 사용자 레이어는 인덱스 8부터 시작
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// RenderTexture 에셋을 로드하거나 없으면 생성한다.
        /// </summary>
        private static RenderTexture EnsureRenderTexture(string path, int w, int h)
        {
            RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (existing != null) return existing;

            // 폴더가 없으면 재귀적으로 생성
            string dir = Path.GetDirectoryName(path).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(dir)) CreateFoldersRecursive(dir);

            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;
            rt.Create();

            AssetDatabase.CreateAsset(rt, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        }

        /// <summary>
        /// GameObject와 모든 자식의 레이어를 재귀적으로 설정한다.
        /// </summary>
        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        /// <summary>
        /// Assets/ 하위 폴더를 재귀적으로 생성한다.
        /// 예: "Assets/_Project/Textures/UI" → Assets → _Project → Textures → UI 순서로 생성.
        /// </summary>
        private static void CreateFoldersRecursive(string path)
        {
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
