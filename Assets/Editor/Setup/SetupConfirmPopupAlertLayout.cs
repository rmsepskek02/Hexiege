// ============================================================================
// SetupConfirmPopupAlertLayout.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  0) ConfirmPopup.prefab 을 Prefab 모드로 열어 두었다면 먼저 닫는다.        │
// │     (열린 채로 실행하면 에디터가 들고 있는 사본과 충돌할 수 있다)          │
// │  1) 상단 메뉴  Hexiege > Setup > Setup ConfirmPopup Alert Layout  실행.   │
// │  2) Console 에 "[Setup] ConfirmPopup 알림 레이아웃 구성 완료" 가 뜨는지    │
// │     확인한다. 무엇을 새로 만들고 무엇을 갱신했는지 같이 출력된다.          │
// │  3) 프리팹 에셋이 직접 저장되므로 씬 저장(Ctrl+S)은 필요 없다.            │
// │     다만 Login.unity 등 이 프리팹의 '인스턴스'가 놓인 씬은 아래 주의 참조.│
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가:
//   공통 UI 규칙 D-5 「알림 팝업 — 타이틀 + 본문 + 버튼 1개」를 프리팹 쪽에서 성립시킨다.
//   코드(ConfirmPopup.cs)는 이미 완성돼 있고, 그 코드가 전제로 삼는 프리팹 구조를
//   여기서 만든다. 구체적으로 4가지다.
//
//     1) Panel 에 VerticalLayoutGroup 추가
//        → ConfirmPopup.ApplyTitle() 이 제목을 SetActive(false) 로 숨길 때
//          "그 자리가 레이아웃에서 사라지는" 동작은 부모에 세로 레이아웃 그룹이
//          있어야만 성립한다. 그게 없으면 제목을 숨겨도 빈 공간이 남아,
//          제목이 없는 기존 확인/취소 팝업의 생김새가 달라진다.
//          (ConfirmPopup.cs 의 ApplyTitle() XML 주석 「프리팹 전제 조건」 참조)
//
//     2) Panel 아래, MessageText 바로 위에 TitleText(TextMeshProUGUI) 생성
//
//     3) TitleText / MessageText / ButtonRow 에 LayoutElement 로 높이 배분 지정
//
//     4) 루트 ConfirmPopup 컴포넌트의 _titleText 슬롯에 TitleText 연결
//
// 건드리지 않는 것:
//   - BlockingOverlay : 코드에서 주석 처리된 미사용 오브젝트. 손대지 않는다.
//   - ButtonRow 의 HorizontalLayoutGroup : 기존 값 그대로 둔다(버튼 배치는 이미 정상).
//   - ConfirmButton / CancelButton / MessageText 의 텍스트·폰트 등 시각 속성.
//
// 주의(에디터 전용):
//   - Assets/Editor/ 하위이므로 자동으로 에디터 전용 컴파일 — 빌드에 포함되지 않는다.
//   - 🔴 프리팹 '에셋' 을 직접 고치는 스크립트라 Ctrl+Z 로 되돌아가지 않는다.
//     그래서 이 스크립트는 **멱등(idempotent)** 하게 작성돼 있다. 즉 몇 번을 실행해도
//     결과가 같다. 이미 있는 것은 새로 만들지 않고 값만 덮어쓴다.
//   - 실패(프리팹 없음 / Panel 없음 / MessageText 없음 등) 시에는 아무것도 저장하지 않고
//     Debug.LogError 로 무엇이 없는지 알린 뒤 중단한다. 즉 "반쯤 고쳐진 프리팹" 이 남지 않는다.
//
// 실행 후 사용자가 해야 할 일:
//   - Login.unity 등 이 프리팹을 인스턴스로 갖고 있는 씬을 열었을 때,
//     해당 인스턴스에 프리팹 오버라이드가 남아 있지 않은지 확인한다.
//     (Hierarchy 에서 인스턴스 선택 → Overrides 드롭다운 → 의도치 않은 항목이 있으면 Revert)
// ============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// ConfirmPopup 프리팹을 「타이틀 + 본문 + 버튼 1개」 알림 팝업까지 지원하도록
    /// 재구성하는 1회성 에디터 스크립트. (공통 UI 규칙 D-5)
    /// </summary>
    public static class SetupConfirmPopupAlertLayout
    {
        // ====================================================================
        // 상수 — 이 값들이 어디서 나왔는지
        // ====================================================================
        //
        // Canvas 기준 해상도는 1080×1920, ScreenMatchMode=MatchWidthOrHeight, Match=0(width).
        // 즉 UI 좌표계의 가로 폭은 항상 1080 이다.
        // Panel 의 앵커는 X 0.1~0.9 / Y 0.3~0.7 이므로 실제 크기는
        //   가로 = 1080 × (0.9 - 0.1) = 864px
        //   세로 = 1920 × (0.7 - 0.3) = 768px
        // 아래 여백/간격 수치는 전부 이 864 × 768 에서 비율로 계산한 값이다.
        // (숫자를 직접 적은 이유: 레이아웃 그룹은 픽셀 값만 받기 때문)

        private const string PrefabPath = "Assets/_Project/Prefabs/UI/ConfirmPopup.prefab";

        private const string PanelName = "Panel";
        private const string MessageTextName = "MessageText";
        private const string ButtonRowName = "ButtonRow";
        private const string TitleTextName = "TitleText";

        /// <summary>Panel 실측 세로 크기(px). 아래 검산 주석의 기준값이다.</summary>
        private const float PanelHeight = 768f;

        // ── VerticalLayoutGroup 수치 ────────────────────────────────────────
        private const int PaddingLeft = 69;     // 864 × 0.08 = 69.12 → 69
        private const int PaddingRight = 69;    // 좌우 대칭
        private const int PaddingTop = 92;      // 768 × 0.12 = 92.16 → 92
        private const int PaddingBottom = 61;   // 768 × 0.08 = 61.44 → 61
        private const float Spacing = 38f;      // 768 × 0.05 = 38.4  → 38

        // ── LayoutElement 수치 ──────────────────────────────────────────────
        private const float TitlePreferredHeight = 70f;       // 제목 1줄(54px 폰트) 이 들어갈 높이
        private const float ButtonRowPreferredHeight = 207f;  // 기존 버튼행 실측 높이 유지
        private const float UnusedLayoutValue = -1f;          // LayoutElement 에서 -1 = "이 값 사용 안 함"

        //
        // ★ 높이 검산 (Panel 세로 768px 기준) ★
        //
        //   본문(MessageText)은 flexibleHeight = 1 이라 "남는 높이를 전부 가져가는" 칸이다.
        //   따라서 본문 높이 = 768 − (위아래 여백) − (간격들) − (고정 높이 칸들) 이 된다.
        //
        //   ● 제목 없음 (기존 확인/취소 2버튼 팝업 — TitleText 가 SetActive(false))
        //       세로로 놓이는 것: [본문] [간격] [버튼행]
        //       768 − 92(top) − 61(bottom) − 38(간격 1개) − 207(버튼행) = 370px
        //       → 변경 전 본문 높이와 비교하면:
        //         MessageText 의 앵커 Y 0.40~0.88 은 화면이 아니라 **부모(Panel) 기준**이므로
        //         768 × (0.88 − 0.40) = 368.6px ≈ 369px 였다.
        //         370px 과 1px 차이이므로 **기존 팝업의 생김새는 바뀌지 않는다.**
        //
        //   ● 제목 있음 (알림 팝업 — TitleText 가 SetActive(true))
        //       세로로 놓이는 것: [제목] [간격] [본문] [간격] [버튼행]
        //       768 − 92 − 61 − 38×2 − 70(제목) − 207(버튼행) = 262px
        //
        //   이 계산이 성립하려면 ChildForceExpandHeight 가 반드시 false 여야 한다(아래 참조).
        //

        // ── TitleText 텍스트 속성 ───────────────────────────────────────────
        private const float TitleFontSize = 54f;              // 본문 48보다 크게 — 제목임을 드러낸다
        private const string TitlePlaceholderText = "알림";   // 런타임에 ShowAlert(title) 이 덮어쓴다

        // ── ConfirmPopup 의 직렬화 필드 이름 ────────────────────────────────
        private const string TitleTextFieldName = "_titleText";


        // ====================================================================
        // 진입점
        // ====================================================================

        /// <summary>
        /// 메뉴에서 호출되는 진입점. 프리팹을 열어 수정하고 저장한다.
        /// 중간에 하나라도 실패하면 저장하지 않고 중단한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Setup ConfirmPopup Alert Layout")]
        public static void Run()
        {
            // 1) 프리팹이 실제로 존재하는지 먼저 확인한다.
            //    PrefabUtility.LoadPrefabContents 는 경로가 틀리면 예외를 던지므로,
            //    그 전에 조용히 확인해서 친절한 오류 메시지를 내보낸다.
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[Setup] 프리팹을 찾지 못했습니다: {PrefabPath}\n" +
                               "경로가 바뀌었는지 확인하세요. 아무것도 저장하지 않고 중단합니다.");
                return;
            }

            // 2) 프리팹 '내용물' 을 임시 씬에 펼쳐서 연다.
            //    씬 대상 셋업 스크립트와 달리, 여기서는 씬이 아니라 프리팹 에셋 자체를 고친다.
            //    반드시 UnloadPrefabContents 로 닫아야 하므로 try/finally 로 감싼다.
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError($"[Setup] 프리팹 내용을 여는 데 실패했습니다: {PrefabPath}");
                return;
            }

            try
            {
                // 3) 실제 구성 작업. 실패하면 false 를 돌려주고, 이유는 자기가 LogError 로 알린다.
                var created = new List<string>();   // 이번 실행에서 '새로 만든' 것
                var updated = new List<string>();   // 이미 있어서 '값만 갱신한' 것

                if (!Configure(root, created, updated))
                {
                    Debug.LogError("[Setup] 구성에 실패했습니다. " +
                                   "🔴 프리팹은 저장하지 않았습니다(변경 전 상태 그대로입니다).");
                    return;
                }

                // 4) 여기까지 왔으면 전부 성공 — 그때서야 저장한다.
                //    (프리팹 내용물은 SaveAsPrefabAsset 이 객체 그래프를 통째로 직렬화하므로
                //     씬 셋업 스크립트에서 쓰는 EditorUtility.SetDirty 는 필요 없다.)
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError($"[Setup] 프리팹 저장에 실패했습니다: {PrefabPath}");
                    return;
                }

                AssetDatabase.SaveAssets();

                // 5) 무엇을 새로 만들고 무엇을 갱신했는지 구분해서 보고한다.
                Debug.Log(
                    "[Setup] ConfirmPopup 알림 레이아웃 구성 완료 — " + PrefabPath + "\n" +
                    "  · 새로 만든 것 : " + Describe(created) + "\n" +
                    "  · 갱신한 것   : " + Describe(updated) + "\n" +
                    "  (이 스크립트는 멱등입니다 — 다시 실행하면 '새로 만든 것' 이 비고 " +
                    "'갱신한 것' 만 남습니다)");
            }
            finally
            {
                // 6) 성공하든 실패하든 임시로 열어 둔 프리팹 내용물은 반드시 닫는다.
                PrefabUtility.UnloadPrefabContents(root);
            }
        }


        // ====================================================================
        // 구성 본체
        // ====================================================================

        /// <summary>
        /// 프리팹 루트를 받아 알림 팝업 레이아웃을 구성한다.
        /// 필요한 오브젝트를 하나라도 찾지 못하면 아무것도 바꾸지 않고 false 를 반환한다.
        /// </summary>
        /// <param name="root">LoadPrefabContents 로 연 프리팹 루트.</param>
        /// <param name="created">이번 실행에서 새로 만든 항목을 적어 넣을 목록.</param>
        /// <param name="updated">이번 실행에서 값만 갱신한 항목을 적어 넣을 목록.</param>
        /// <returns>전부 성공하면 true, 하나라도 실패하면 false.</returns>
        private static bool Configure(GameObject root, List<string> created, List<string> updated)
        {
            // ── 0) 필요한 오브젝트를 전부 먼저 찾는다 ────────────────────────
            //    하나라도 없으면 '고치기 시작하기 전에' 중단하기 위해서다.
            //    중간까지 고쳐 놓고 실패하면 반쯤 바뀐 프리팹이 남을 수 있는데,
            //    프리팹 에셋 수정은 Ctrl+Z 로 되돌릴 수 없으므로 그것을 피한다.
            ConfirmPopup popup = root.GetComponent<ConfirmPopup>();
            if (popup == null)
            {
                Debug.LogError("[Setup] 프리팹 루트에 ConfirmPopup 컴포넌트가 없습니다. " +
                               "대상 프리팹이 맞는지 확인하세요.");
                return false;
            }

            Transform panel = root.transform.Find(PanelName);
            if (panel == null)
            {
                Debug.LogError($"[Setup] '{PanelName}' 오브젝트를 찾지 못했습니다 " +
                               "(ConfirmPopup 바로 아래에 있어야 합니다).");
                return false;
            }

            Transform messageTr = panel.Find(MessageTextName);
            if (messageTr == null)
            {
                Debug.LogError($"[Setup] '{MessageTextName}' 오브젝트를 찾지 못했습니다 " +
                               $"({PanelName} 바로 아래에 있어야 합니다).");
                return false;
            }

            TextMeshProUGUI messageText = messageTr.GetComponent<TextMeshProUGUI>();
            if (messageText == null)
            {
                Debug.LogError($"[Setup] '{MessageTextName}' 에 TextMeshProUGUI 가 없습니다. " +
                               "제목의 폰트/머티리얼/색을 본문에서 복사해야 하므로 필수입니다.");
                return false;
            }

            Transform buttonRow = panel.Find(ButtonRowName);
            if (buttonRow == null)
            {
                Debug.LogError($"[Setup] '{ButtonRowName}' 오브젝트를 찾지 못했습니다 " +
                               $"({PanelName} 바로 아래에 있어야 합니다).");
                return false;
            }

            // ── 1) Panel 에 VerticalLayoutGroup ─────────────────────────────
            ConfigureVerticalLayout(panel, created, updated);

            // ── 2) TitleText 생성 또는 재사용 ───────────────────────────────
            TextMeshProUGUI titleText = EnsureTitleText(panel, messageText, created, updated);

            // ── 3) 높이 배분(LayoutElement 3개) ─────────────────────────────
            //    제목: 고정 70px, 늘어나지 않음
            ConfigureLayoutElement(titleText.gameObject, TitlePreferredHeight, 0f,
                                   TitleTextName, created, updated);
            //    본문: 고정 높이 없음(-1) + 남는 높이를 전부 가져감(flexible 1)
            ConfigureLayoutElement(messageText.gameObject, UnusedLayoutValue, 1f,
                                   MessageTextName, created, updated);
            //    버튼행: 고정 207px, 늘어나지 않음
            ConfigureLayoutElement(buttonRow.gameObject, ButtonRowPreferredHeight, 0f,
                                   ButtonRowName, created, updated);

            // ── 4) ConfirmPopup._titleText 슬롯 배선 ────────────────────────
            return ConnectTitleSlot(popup, titleText, created, updated);
        }


        // ====================================================================
        // 1) VerticalLayoutGroup
        // ====================================================================

        /// <summary>
        /// Panel 에 VerticalLayoutGroup 을 붙이고(없으면) 값을 지정한다.
        /// 이미 있으면 새로 만들지 않고 값만 덮어쓴다 — 멱등성의 핵심이다.
        /// </summary>
        private static void ConfigureVerticalLayout(Transform panel,
                                                    List<string> created, List<string> updated)
        {
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            bool isNew = layout == null;
            if (isNew)
                layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();

            // 여백/간격 — 위쪽 상수 주석에 각 수치의 출처(864 × 0.08 등)를 적어 두었다.
            layout.padding = new RectOffset(PaddingLeft, PaddingRight, PaddingTop, PaddingBottom);
            layout.spacing = Spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;

            // ChildControlWidth/Height = true
            //   → 자식의 가로/세로 크기를 레이아웃 그룹이 직접 정한다.
            //     이게 false 면 자식이 자기 RectTransform 크기를 그대로 쓰므로
            //     아래 LayoutElement 의 preferredHeight 가 무시된다.
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // ChildForceExpandWidth = true
            //   → 자식들이 가로 폭을 꽉 채운다(제목/본문/버튼행 모두 같은 폭).
            layout.childForceExpandWidth = true;

            // 🔴 ChildForceExpandHeight = false  ← 반드시 false
            //   이 값이 true 면 레이아웃 그룹이 "남는 세로 공간을 자식들에게 똑같이 나눠 준다".
            //   그러면 제목 70px · 버튼행 207px 같은 preferredHeight 가 전부 무시되고
            //   세 칸이 비슷한 높이로 균등 분할돼 버려서, 위 검산(370px / 262px)이 성립하지 않는다.
            //   false 로 두어야 "고정 높이는 고정으로 주고, 남는 높이는 flexibleHeight=1 인
            //   본문에게만 몰아 준다" 는 의도대로 동작한다.
            layout.childForceExpandHeight = false;

            (isNew ? created : updated).Add($"{PanelName}.VerticalLayoutGroup");
        }


        // ====================================================================
        // 2) TitleText
        // ====================================================================

        /// <summary>
        /// Panel 아래 MessageText 바로 위에 TitleText 를 만든다(이미 있으면 재사용).
        /// 폰트 에셋 · 머티리얼 · 글자색은 <b>MessageText 의 것을 그대로 복사</b>한다 —
        /// 경로를 하드코딩하면 나중에 본문 폰트를 바꿨을 때 제목만 따로 놀게 된다.
        /// </summary>
        private static TextMeshProUGUI EnsureTitleText(Transform panel, TextMeshProUGUI messageText,
                                                       List<string> created, List<string> updated)
        {
            Transform titleTr = panel.Find(TitleTextName);
            bool isNew = titleTr == null;

            if (isNew)
            {
                var go = new GameObject(TitleTextName, typeof(RectTransform));
                // worldPositionStays: false — UI 는 부모 기준 로컬 좌표로 붙여야 한다.
                go.transform.SetParent(panel, false);
                titleTr = go.transform;
            }

            TextMeshProUGUI title = titleTr.GetComponent<TextMeshProUGUI>();
            if (title == null)
                title = titleTr.gameObject.AddComponent<TextMeshProUGUI>();

            // ── 본문에서 복사해 오는 값 ─────────────────────────────────────
            // ⚠️ font 를 대입하면 TMP 가 머티리얼을 그 폰트의 기본값으로 되돌리므로,
            //    머티리얼은 반드시 font 다음에 대입해야 한다.
            title.font = messageText.font;
            title.fontSharedMaterial = messageText.fontSharedMaterial;
            title.color = messageText.color;

            // ── 제목 고유의 값 ──────────────────────────────────────────────
            title.text = TitlePlaceholderText;          // 런타임에 ShowAlert(title) 이 덮어쓴다
            title.fontSize = TitleFontSize;             // 본문 48 → 제목 54
            title.fontStyle = FontStyles.Bold;          // 제목은 굵게
            title.alignment = TextAlignmentOptions.Center;  // 가로 Center + 세로 Middle
            title.raycastTarget = false;                // 글자가 버튼 터치를 가로채지 않도록

            (isNew ? created : updated).Add($"{TitleTextName} (TextMeshProUGUI)");

            // ── 형제 순서: MessageText 바로 위 ──────────────────────────────
            //   새로 만든 경우 맨 끝(ButtonRow 아래)에 붙으므로 위로 올려야 한다.
            //   이미 위에 있는 경우에는 움직이지 않는다(두 번 실행해도 순서가 흔들리지 않게).
            //   (GetSiblingIndex / SetSiblingIndex 는 Transform 의 기능이라 RectTransform 캐스팅이 필요 없다)
            int messageIndex = messageText.transform.GetSiblingIndex();
            int titleIndex = titleTr.GetSiblingIndex();

            // 제목이 본문보다 뒤에 있으면 본문 자리로 옮기면 된다(본문이 한 칸 뒤로 밀린다).
            // 제목이 이미 앞에 있으면 '본문 바로 앞' 인 messageIndex - 1 이 목표 위치다.
            int desiredIndex = titleIndex < messageIndex ? messageIndex - 1 : messageIndex;
            if (titleIndex != desiredIndex)
                titleTr.SetSiblingIndex(desiredIndex);

            return title;
        }


        // ====================================================================
        // 3) LayoutElement
        // ====================================================================

        /// <summary>
        /// 대상 오브젝트에 LayoutElement 를 붙이고(없으면) 높이 관련 값을 지정한다.
        /// 표에 없는 값(minHeight, preferredWidth 등)은 건드리지 않는다.
        /// </summary>
        /// <param name="target">LayoutElement 를 붙일 오브젝트.</param>
        /// <param name="preferredHeight">
        /// 선호 높이(px). <see cref="UnusedLayoutValue"/>(-1)를 넘기면
        /// "이 값 사용 안 함" 이 되어 Inspector 의 체크박스가 꺼진 상태가 된다.
        /// </param>
        /// <param name="flexibleHeight">
        /// 남는 높이를 가져가는 가중치. 0 이면 늘어나지 않고, 1 이면 남는 높이를 가져간다.
        /// </param>
        /// <param name="label">로그에 찍을 이름.</param>
        private static void ConfigureLayoutElement(GameObject target,
                                                   float preferredHeight, float flexibleHeight,
                                                   string label,
                                                   List<string> created, List<string> updated)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            bool isNew = element == null;
            if (isNew)
                element = target.AddComponent<LayoutElement>();

            element.preferredHeight = preferredHeight;
            element.flexibleHeight = flexibleHeight;

            (isNew ? created : updated).Add($"{label}.LayoutElement");
        }


        // ====================================================================
        // 4) _titleText 슬롯 배선
        // ====================================================================

        /// <summary>
        /// 루트의 ConfirmPopup 컴포넌트에 있는 private [SerializeField] 슬롯 _titleText 를 연결한다.
        /// private 필드라 코드에서 직접 대입할 수 없으므로 SerializedObject 로 접근한다
        /// (CreateNicknameChangePopup.Connect 와 같은 방식 — 다만 그쪽은 씬 대상이라
        /// ApplyModifiedProperties 를 쓰고, 여기는 프리팹 대상이라 아래처럼 Undo 없이 적용한다).
        /// </summary>
        private static bool ConnectTitleSlot(ConfirmPopup popup, TextMeshProUGUI titleText,
                                             List<string> created, List<string> updated)
        {
            var so = new SerializedObject(popup);
            SerializedProperty prop = so.FindProperty(TitleTextFieldName);
            if (prop == null)
            {
                Debug.LogError($"[Setup] ConfirmPopup 에 '{TitleTextFieldName}' 필드가 없습니다. " +
                               "ConfirmPopup.cs 가 최신인지 확인하세요.");
                return false;
            }

            // 이미 같은 오브젝트가 물려 있으면 '갱신' 으로만 보고한다(멱등).
            bool alreadyWired = prop.objectReferenceValue == titleText;
            prop.objectReferenceValue = titleText;

            // ⚠️ LoadPrefabContents 로 연 임시 씬에서는 Undo 기록이 의미가 없고,
            //    ApplyModifiedProperties() 가 변경을 직렬화에 반영하지 못하는 사례가 있었다.
            //    그래서 Undo 를 태우지 않는 쪽을 쓴다(프리팹 대상 에디터 스크립트의 확립된 관례).
            so.ApplyModifiedPropertiesWithoutUndo();

            (alreadyWired ? updated : created).Add($"ConfirmPopup.{TitleTextFieldName} 배선");
            return true;
        }


        // ====================================================================
        // 로그 보조
        // ====================================================================

        /// <summary>로그용 — 목록이 비어 있으면 "(없음)" 으로 표시한다.</summary>
        private static string Describe(List<string> items)
        {
            return items.Count == 0 ? "(없음)" : string.Join(", ", items);
        }
    }
}
