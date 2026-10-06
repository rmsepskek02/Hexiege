// ============================================================================
// ConfirmPopup.cs
// 어떤 메시지/버튼 라벨/콜백이든 주입해 재사용 가능한 범용 확인 팝업.
//
// 사용 시나리오:
//   - 게임 포기 확인
//   - 위험한 동작 직전 사용자 의사 재확인 (실제로 쓰는 예: 앱 종료 확인 · 가입 취소 확인.
//     건물 철거나 매칭 종료 확인은 지금 이 팝업을 쓰지 않는다 — 쓸 수 있다는 뜻일 뿐이다)
//
// 사용 예 — ⚠️ 실제 화면 문구는 적지 않는다(아래 인자 설명의 ⚠️ 와 같은 이유:
//            그 문구가 코드에 한 곳만 있는지 세는 검사가 설명문까지 함께 세게 된다):
//   _confirmPopup.Show(
//       message: 본문으로 보일 한 문장,
//       confirmLabel: 확정 버튼에 보일 짧은 말,
//       cancelLabel: 취소 버튼에 보일 짧은 말,
//       onConfirm: () => 확정 시 실행할 동작,
//       onCancel: null);
//
// 구조:
//   - 뒤쪽 입력 차단용 반투명 막: 🔴 이 팝업이 소유하지 않는다. UIManager 가 단일 소유하는
//     공용 막을 Modal 모드로 잡아서 쓴다(공통 UI 규칙 4·5 — 규칙 5 의 Modal 모드 항목이 이 팝업을
//     명시한다). 이 파일에 남아 있는 자체 소유
//     필드는 비활성화된 옛 로직이며, 최종 삭제 시점은 WORKFLOW.md [4] 가 정한다.
//   - _panel(AnimatedPanel, PopupFade): 실제 시각적인 팝업 박스.
//   - _titleText: 알림 팝업에서만 쓰는 한 줄 제목. 확인/취소 경로에서는 자리를 숨긴다.
//   - _messageText: 본문 메시지.
//   - _confirmButton / _cancelButton: 두 가지 선택지 버튼.
//
// 동작:
//   Show() 호출 시:
//     1. 제목 자리를 숨기고 취소 버튼을 켠다 (확인/취소 2버튼 구조로 맞춘다)
//     2. 메시지 / 라벨 갱신
//     3. 콜백 저장 (외부 onConfirm/onCancel)
//     4. 버튼 onClick 리스너 RemoveAllListeners 후 재등록
//        (Show()가 여러 번 호출돼도 콜백이 누적되지 않도록 보장)
//     5. 공용 막을 잡는다 — 이미 잡고 있으면 다시 잡지 않는다(아래 잡기 헬퍼)
//     6. _panel.Show()
//        (AnimatedPanel은 SetActive 대신 CanvasGroup으로 가시성을 제어하므로,
//         오브젝트는 항상 active 상태를 유지하며 Show()만 호출하면 된다 — 공통 UI 규칙 5)
//     7. 떴다는 사실을 기록에 남긴다 (개발 빌드 전용 — 아래 기록 절)
//
//   ShowAlert() 호출 시:
//     위와 같은 순서이며 1번이 반대다 — 제목을 띄우고 취소 버튼을 숨긴다(공통 UI 규칙 D-5).
//     취소 버튼이 숨겨져 있으므로 2·3·4번에서 취소 쪽(라벨·콜백·리스너)은 다루지 않는다.
//
//   Hide() 호출 시:
//     1. 닫기가 불렸다는 사실을 기록에 남긴다 (개발 빌드 전용)
//     2. 공용 막을 놓는다 — 🔴 내가 잡은 적이 있을 때만 놓는다(아래 놓기 헬퍼)
//     3. _panel.Hide() — 페이드+스케일 아웃 애니메이션 후 CanvasGroup으로 숨김 처리
//
// Presentation 레이어 — MonoBehaviour 의존.
// ============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Application;      // GameLog — 런타임 로그 파사드(로그 줄 형식·카테고리 규정은 LogRules.md 1.4)

namespace Hexiege.Presentation
{
    /// <summary>
    /// 범용 확인 팝업. Show() 호출 시 메시지/라벨/콜백을 주입하여 재사용.
    /// 어떤 상황에서도 동일한 팝업 UI를 재활용하기 위해 일체의 비즈니스 로직을 포함하지 않는다.
    /// </summary>
    public class ConfirmPopup : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        // 자체 _blockingOverlay 대신 UIManager.Instance?.ShowBlockingOverlay() (Modal 모드)를 사용한다.
        //   아래 필드는 테스트 통과 후 삭제 예정 — 현재는 비활성화(주석 처리)로 보존.
        // [Header("입력 차단 오버레이")]
        // [Tooltip("팝업이 떠 있을 때 뒤쪽 UI 입력을 차단하는 전체 화면 투명 Image의 CanvasGroup. " +
        //          "규칙 5에 따라 SetActive 대신 alpha/blocksRaycasts로 표시·숨김을 제어한다. " +
        //          "차단이 동작하려면 하위 Image의 Raycast Target=true 이어야 한다.")]
        // [SerializeField] private CanvasGroup _blockingOverlay;

        [Header("팝업 본체")]
        [Tooltip("팝업 박스 자체에 부착된 AnimatedPanel (PopupFade 타입 권장).")]
        [SerializeField] private AnimatedPanel _panel;

        [Header("텍스트")]
        // 공통 UI 규칙 D-5 「알림 팝업 — 타이틀 + 본문 + 버튼 1개」를 위해 추가한 자리.
        // ✅ 프리팹 배선은 이미 끝나 있다(이 팝업 프리팹의 일곱 직렬화 슬롯이 모두 채워져 있다).
        // ⚠️ 그래도 아래 모든 사용처에서 null 검사를 거친다 — 배선이 빠진 상태에서도
        //    기존 확인/취소 동작이 그대로여야 하기 때문이다(이 파일의 다른 필드와 같은 방식).
        [Tooltip("팝업 제목 (예: '알림'). 제목이 없는 팝업에서는 숨겨진다. 배선하지 않아도 동작한다.")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Tooltip("팝업 본문 메시지 (예: '정말 포기하시겠습니까?').")]
        [SerializeField] private TextMeshProUGUI _messageText;

        [Header("버튼")]
        [Tooltip("확정 버튼 (왼쪽 또는 오른쪽).")]
        [SerializeField] private Button _confirmButton;

        [Tooltip("취소 버튼.")]
        [SerializeField] private Button _cancelButton;

        [Tooltip("확정 버튼 라벨 텍스트.")]
        [SerializeField] private TextMeshProUGUI _confirmButtonText;

        [Tooltip("취소 버튼 라벨 텍스트.")]
        [SerializeField] private TextMeshProUGUI _cancelButtonText;


        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 현재 등록된 확정 콜백. Show()에서 갱신, OnConfirmClicked()에서 Invoke.
        /// </summary>
        private Action _onConfirm;

        /// <summary>
        /// 현재 등록된 취소 콜백. Show()에서 갱신, OnCancelClicked()에서 Invoke.
        /// </summary>
        private Action _onCancel;

        /// <summary>
        /// 이 팝업이 지금 공용 반투명 막의 점유를 <b>들고 있는지</b>를 기억하는 깃발.
        ///
        /// <para>
        /// [초급자용 설명] 이 깃발이 왜 필요한가
        ///   그 막은 이 팝업이 소유한 물건이 아니다. 공용 UI 관리자가 하나만 들고 있고,
        ///   「잡아 달라고 한 곳이 지금 몇 곳인가」를 숫자로 세어 두었다가 그 숫자가 0이 될
        ///   때에만 실제로 막을 끈다. 즉 <b>잡은 쪽과 놓는 쪽의 횟수가 정확히 맞아야</b> 한다.
        ///   그런데 이 팝업의 닫기는 「혹시 떠 있으면 닫아라」는 뜻으로 <b>조건 없이</b> 불리는
        ///   자리가 있어서, 잡은 적이 없는데도 놓으려 드는 일이 실제로 일어난다.
        ///   이 깃발을 보고 나서 움직이면 <b>내가 잡은 만큼만</b> 놓는다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>이 값을 직접 대입하는 자리는 아래 잡기/놓기 헬퍼 두 곳뿐이다.</b>
        /// 표시·닫기 경로가 모두 그 두 곳을 지나므로, 거기서만 판별하면 어느 경로도 빠뜨리지 않는다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>이 깃발과 헬퍼 본문에는 컴파일 조건을 붙이지 않는다.</b> 기록용이 아니라
        /// <b>실제 동작</b>이기 때문이다 — 조건을 붙이면 릴리스 빌드에서 막이 꺼지지 않는다.
        /// (컴파일 조건이 붙는 것은 이 파일 아래쪽의 기록 전용 메서드들뿐이다.)
        /// </para>
        /// </summary>
        private bool _overlayShown;

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 팝업을 표시한다. 메시지, 버튼 라벨, 콜백을 모두 인자로 받아 매번 새로 갱신.
        /// </summary>
        /// <param name="message">팝업 본문에 표시될 메시지.</param>
        /// <param name="confirmLabel">확정 버튼에 표시될 텍스트. 눌렀을 때 무엇이 실행되는지를
        /// 알려 주는 짧은 말을 넣는다. ⚠️ 이 설명문에 실제 화면 문구를 예시로 베껴 적지 않는다 —
        /// 그 문구가 코드에 한 곳만 있는가를 세는 검사가 설명문까지 함께 세어 거짓을 말하게
        /// 되기 때문이다.</param>
        /// <param name="cancelLabel">취소 버튼에 표시될 텍스트. 눌렀을 때 아무것도 하지 않고
        /// 닫힌다는 뜻이 드러나는 짧은 말을 넣는다.
        /// ⚠️ 이곳에도 실제 화면 문구를 예시로 적지 않는다(위와 같은 이유).</param>
        /// <param name="onConfirm">확정 버튼 클릭 시 호출될 콜백. null 허용.</param>
        /// <param name="onCancel">
        /// 취소 버튼 클릭 시 호출될 콜백. null이면 단순히 팝업이 닫히기만 한다.
        /// </param>
        public void Show(string message, string confirmLabel, string cancelLabel,
                         Action onConfirm, Action onCancel)
        {
            // 0) 이 경로(확인/취소 2버튼)에는 제목이 없다. 제목 자리를 숨겨
            //    제목 필드가 생기기 전과 화면이 똑같이 보이게 한다.
            ApplyTitle(null);
            SetCancelButtonVisible(true);

            // 1) 텍스트/라벨 갱신
            if (_messageText != null)
                _messageText.text = message;
            if (_confirmButtonText != null)
                _confirmButtonText.text = confirmLabel;
            if (_cancelButtonText != null)
                _cancelButtonText.text = cancelLabel;

            // 2) 콜백 저장 — 클릭 시점에 Invoke
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // 3) 버튼 리스너 RemoveAllListeners 후 재등록.
            //    Show()가 반복 호출돼도 onClick에 콜백이 누적되지 않도록 보장.
            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            }
            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveAllListeners();
                _cancelButton.onClick.AddListener(OnCancelClicked);
            }

            // 4) 뒤쪽 입력 차단을 즉시 활성화.
            //    UIManager가 단일 소유하는 BlockingOverlay를 Modal 모드로 표시.
            //    Modal 모드(콜백 없음)이므로 오버레이를 터치해도 닫히지 않고 입력만 차단된다.
            //    (확인/취소 버튼으로만 닫힘 — 규칙 9: 모달은 배경 탭 닫기 불가)
            //
            //    ⚠️ 잡는 일을 아래 헬퍼에 맡긴다 — 이미 잡고 있으면 다시 잡지 않아야 하기 때문이다.
            //      그 이유는 헬퍼 쪽 설명에 적었다.
            ShowOverlayOnce();
            // [구로직 — 테스트 통과 후 삭제]
            // if (_blockingOverlay != null)
            // {
            //     _blockingOverlay.alpha = 1f;
            //     _blockingOverlay.blocksRaycasts = true;
            //     _blockingOverlay.interactable = true;
            // }

            // 5) 패널 등장 애니메이션 시작.
            //    AnimatedPanel은 CanvasGroup으로 가시성을 제어하므로
            //    오브젝트는 항상 active 상태이며, Show() 호출만으로 다시 표시된다.
            if (_panel != null)
                _panel.Show();

            // 6) 떴다는 사실을 남긴다 — 위 처리가 모두 끝난 뒤라 실제로 적용된 구조가 실린다.
            LogPopupShown(isAlert: false);
        }

        /// <summary>
        /// 알림 팝업을 표시한다. 제목 + 본문 + 버튼 1개 구조다(공통 UI 규칙 D-5).
        ///
        /// <para>
        /// 위 <see cref="Show"/> 와 무엇이 다른가 — <see cref="Show"/> 는 사용자가
        /// 두 갈래 중 하나를 고르는 자리(확인/취소)이고, 이것은 <b>고를 것이 없고
        /// 알리기만 하는 자리</b>다. 그래서 취소 버튼을 숨기고 제목을 띄운다.
        /// </para>
        ///
        /// <para>
        /// 🔴 타입은 <b>모달</b>이다(공통 UI 규칙 8 · 9). 고를 것이 하나여도
        /// 사용자가 반드시 응답해야 하므로 <b>배경을 탭해도 닫히지 않는다</b> —
        /// 아래에서 <see cref="Show"/> 와 같은 Modal 모드 오버레이를 쓰기 때문에
        /// 이를 위해 따로 해 줄 일은 없다.
        /// </para>
        /// </summary>
        /// <param name="title">팝업 제목. 무엇에 대한 알림인지를 한 낱말로 가리키는 짧은 말을 넣는다.
        /// 비어 있으면 제목 자리가 숨는다.
        /// ⚠️ 이곳에도 실제 화면 문구를 예시로 적지 않는다(아래 두 줄과 같은 이유).</param>
        /// <param name="message">본문 메시지. 사용자에게 지금 무슨 일이 생겼는지 알리는 한 문장을 넣는다.
        /// ⚠️ 이 설명문에 실제 화면 문구를 예시로 베껴 적지 않는다 — 그 문구가 코드에 한 곳만
        /// 있는가를 세는 검사가 설명문까지 함께 세어 거짓을 말하게 되기 때문이다.</param>
        /// <param name="buttonLabel">유일한 버튼의 라벨. 눌렀을 때 어디로 가는지를 알려 주는
        /// 짧은 말을 넣는다. ⚠️ 이곳에도 실제 화면 문구를 예시로 적지 않는다(위와 같은 이유).</param>
        /// <param name="onClick">버튼 클릭 시 호출될 콜백. null이면 닫히기만 한다.</param>
        public void ShowAlert(string title, string message, string buttonLabel, Action onClick)
        {
            // 0) 제목을 띄우고 취소 버튼을 숨긴다 — 이 둘이 Show() 와의 본질적인 차이다.
            //    아래에서 취소 쪽 라벨·콜백·리스너를 다루지 않는 것은 그 버튼이 숨겨져 있어서일 뿐이다.
            ApplyTitle(title);
            SetCancelButtonVisible(false);

            // 1) 텍스트/라벨 갱신
            if (_messageText != null)
                _messageText.text = message;
            if (_confirmButtonText != null)
                _confirmButtonText.text = buttonLabel;

            // 2) 콜백 저장.
            //    취소 쪽은 비워 둔다 — 버튼이 숨겨져 있어 눌릴 일이 없지만,
            //    지난 호출의 콜백이 남아 있으면 엉뚱한 곳으로 새어 나갈 수 있다.
            _onConfirm = onClick;
            _onCancel = null;

            // 3) 버튼 리스너 재등록 (Show() 와 같은 이유 — 반복 호출 시 누적 방지)
            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            // 4) 뒤쪽 입력 차단 — Show() 와 똑같이 Modal 모드다(콜백 없음).
            //    🔴 두 표시 메서드가 연달아 불릴 수 있어(두 갈래 팝업 → 한 버튼 팝업으로 갈아타기)
            //      이 자리도 반드시 헬퍼를 지나야 한다. 직접 부르면 점유가 2가 된다.
            ShowOverlayOnce();

            // 5) 패널 등장
            if (_panel != null)
                _panel.Show();

            // 6) 떴다는 사실을 남긴다 (위 Show() 의 같은 자리와 같은 이유).
            LogPopupShown(isAlert: true);
        }

        // ====================================================================
        // 내부 보조
        // ====================================================================

        /// <summary>
        /// 제목을 적용한다. 제목이 비어 있으면 그 자리를 <b>레이아웃에서 빼 버린다.</b>
        ///
        /// <para>
        /// ⚠️ <b>공통 UI 규칙 5(CanvasGroup 숨김/표시 패턴)에서 일부러 벗어난 자리다.</b>
        /// 규칙 5가 SetActive 를 막는 이유는 두 가지인데, 여기서는 둘 다 해당하지 않는다.
        /// </para>
        /// <para>
        /// 하나는 "Layout Group 안에서 공간이 사라져 나머지가 이동한다"인데,
        /// 제목은 <b>그렇게 되는 것이 맞다.</b> 제목 없는 기존 팝업(확인/취소)에서
        /// 제목 자리가 빈 공간으로 남으면, 제목 필드를 추가했다는 이유만으로
        /// 이미 쓰이고 있는 팝업들의 생김새가 달라진다. 그것을 막는 것이 이 처리의 목적이다.
        /// </para>
        /// <para>
        /// 다른 하나는 "오브젝트 내부 로직(Update 등)이 멈춘다"인데,
        /// 제목은 글자만 표시하는 텍스트라 멈출 로직이 없다.
        /// </para>
        /// <para>
        /// ⚠️ <b>프리팹 전제 조건</b> — 위의 "공간이 사라진다"는 동작은
        /// 부모(ConfirmPopup 프리팹의 Panel)에 <b>VerticalLayoutGroup 이 있어야</b> 성립한다.
        /// 레이아웃 그룹이 없는 앵커 배치라면 숨긴 제목의 자리가 빈 공간으로 남아,
        /// 제목 없는 기존 팝업의 본문이 아래로 밀려 내려간다.
        /// (같은 이유로 취소 버튼은 ButtonRow 의 HorizontalLayoutGroup 에 의존한다.)
        /// </para>
        /// </summary>
        private void ApplyTitle(string title)
        {
            if (_titleText == null) return;   // Inspector 미배선 — 조용히 넘어간다

            bool hasTitle = !string.IsNullOrEmpty(title);
            if (hasTitle)
                _titleText.text = title;

            if (_titleText.gameObject.activeSelf != hasTitle)
                _titleText.gameObject.SetActive(hasTitle);
        }

        /// <summary>
        /// 취소 버튼의 표시 여부를 바꾼다. 알림 팝업(버튼 1개)에서는 숨긴다.
        /// 숨김 방식을 SetActive 로 고른 이유는 <see cref="ApplyTitle"/> 과 같다 —
        /// 버튼이 나란히 놓인 Layout Group 에서 숨긴 버튼의 자리가 남으면
        /// 남은 버튼 하나가 한쪽으로 치우쳐 보인다.
        /// </summary>
        private void SetCancelButtonVisible(bool visible)
        {
            if (_cancelButton == null) return;   // Inspector 미배선 — 조용히 넘어간다

            if (_cancelButton.gameObject.activeSelf != visible)
                _cancelButton.gameObject.SetActive(visible);
        }

        // ====================================================================
        // 공용 반투명 막의 점유 헬퍼 (막은 UIManager 단일 소유 — 공통 UI 규칙 4·5)
        //
        // [초급자용 설명] 이 두 메서드는 무엇이고 왜 생겼는가 (2026-09-30 추가)
        //   종전에는 표시 두 자리가 막을 직접 잡고, 닫기 한 자리가 막을 「조건 없이」 놓았다.
        //   그래서 두 가지가 어긋났다.
        //     ① 표시 두 자리가 연달아 불리면 점유가 2가 되고, 닫기 한 번으로는 0이 되지 않아
        //        막이 다음 화면까지 켜진 채 따라간다. 막은 화면 전체를 받는 버튼이라 남으면
        //        그 화면의 어떤 버튼도 눌리지 않는다.
        //     ② 잡은 적이 없는데 놓으면 「다른 팝업이 정당하게 들고 있던 몫」이 줄어든다.
        //        공용 관리자 쪽 검사는 숫자가 음수로 내려가는 것만 막아 주므로 이 자리를
        //        대신해 주지 못한다 — 남의 몫을 가져가는 것은 음수가 아니기 때문이다.
        //   아래 한 쌍이 깃발을 먼저 보고 「실제로 상태가 바뀌는 경우에만」 움직여 둘 다 막는다.
        //
        // 🔴 이 형태는 새로 만든 것이 아니다 — 같은 문제를 이미 올바르게 푼 재경기 요청 팝업의
        //    「깃발 + 잡기/놓기 한 쌍」을 그대로 따랐다. 두 팝업이 같은 모양이어야 다음에 읽는
        //    사람이 한쪽만 보고도 나머지를 안다.
        //
        // ⚠️ 여기에는 컴파일 조건을 붙이지 않는다 — 기록이 아니라 실제 동작이다.
        //    (컴파일 조건이 붙는 것은 이 파일 아래쪽의 기록 전용 메서드들뿐이다.)
        // ====================================================================

        /// <summary>
        /// 공용 반투명 막을 Modal 모드로 잡는다. <b>이미 잡고 있으면 아무 일도 하지 않는다.</b>
        ///
        /// <para>
        /// 🔴 <b>두 표시 메서드가 연달아 불릴 수 있다.</b> 두 갈래 중 고르는 팝업이 떠 있는
        /// 상태에서 고를 것이 없는 한 버튼 팝업으로 갈아타면 표시가 두 번 불리는데, 그때
        /// 점유가 2가 되면 닫기 한 번으로 0이 되지 않는다. 이 검사가 그것을 막는다.
        /// </para>
        /// </summary>
        private void ShowOverlayOnce()
        {
            if (_overlayShown) return;   // 이미 잡고 있으면 그대로 둔다.
            _overlayShown = true;
            UIManager.Instance?.ShowBlockingOverlay();   // Modal 모드(콜백 없음)
        }

        /// <summary>
        /// 이 팝업이 잡고 있는 막의 점유를 1회 놓는다. <b>잡은 적이 없으면 아무 일도 하지 않는다.</b>
        ///
        /// <para>
        /// 🔴 <b>이 검사가 없으면 남의 몫을 놓는다.</b> 이 팝업의 닫기는 「혹시 떠 있으면 닫아라」는
        /// 뜻으로 <b>조건 없이</b> 불리는 자리가 있다(결과 화면이 다음 화면으로 나가는 길목).
        /// 떠 있지 않았는데 놓으면 공용 관리자가 세어 둔 숫자가 한 칸 줄고, 그 숫자를 다른 팝업이
        /// 정당하게 들고 있었다면 <b>아직 막이 필요한데 꺼진다.</b>
        /// </para>
        /// </summary>
        private void HideOverlayOnce()
        {
            if (!_overlayShown) return;   // 잡은 적이 없으면 놓을 것도 없다.
            _overlayShown = false;
            UIManager.Instance?.HideBlockingOverlay();
        }

        /// <summary>
        /// 팝업을 닫는다. 외부에서 직접 호출 가능(예: 모달이 떠 있는 동안 게임 종료 시).
        /// 콜백은 호출하지 않는다 — 단순 시각적 닫기.
        /// </summary>
        public void Hide()
        {
            // 닫기가 불렸다는 사실을 먼저 남긴다 — 아래 두 줄보다 앞이어야 「닫히기 직전에
            // 팝업이 실제로 떠 있었는가」가 그 줄에 그대로 실린다.
            LogPopupHideRequested();

            // 입력 차단 오버레이는 즉시 해제 (페이드 아웃 중에도 뒤쪽 조작이 즉시 가능하도록).
            // UIManager 단일 소유 BlockingOverlay를 숨김(중첩 시 참조 카운터로 처리).
            //
            // 🔴 놓는 일을 아래 헬퍼에 맡긴다 — 이 닫기는 떠 있지 않은 상태에서도 불리므로,
            //   조건 없이 놓으면 「다른 팝업이 정당하게 들고 있던 몫」까지 놓아 버린다.
            //   헬퍼가 위 깃발을 먼저 보고 「내가 잡은 경우에만」 놓는다.
            HideOverlayOnce();
            // [구로직 — 테스트 통과 후 삭제]
            // if (_blockingOverlay != null)
            // {
            //     _blockingOverlay.alpha = 0f;
            //     _blockingOverlay.blocksRaycasts = false;
            //     _blockingOverlay.interactable = false;
            // }

            // 팝업 본체는 애니메이션 후 CanvasGroup으로 숨김 처리 — AnimatedPanel이 담당
            if (_panel != null)
                _panel.Hide();
        }

        // ====================================================================
        // 기록 (2026-09-30 추가 · 화면 동작은 바뀌지 않는다)
        //
        // [초급자용 설명] 이 절은 무엇이고 왜 생겼는가
        //   이 팝업이 떴는지 · 닫혔는지는 지금까지 **사람이 화면을 보는 것** 말고는 확인할
        //   방법이 없었다. 이 파일에는 기록을 남기는 자리가 한 곳도 없었기 때문이다.
        //   그래서 공통 UI 규칙 D-5 가 정한 구조(제목 + 본문 + 버튼 1개)로 실제로 떴는지도
        //   기록으로는 확인되지 않았다.
        //
        // 🔴 부르는 쪽이 아니라 이 팝업 본문에 넣은 이유 — 이 팝업을 쓰는 자리는 하나가 아니고
        //    앞으로도 늘 수 있다(예: 싱글 최초 경기의 맵 준비 실패 안내는 이 팝업의 확인/취소
        //    경로를 쓴다 — 규칙 M-4). 부르는 쪽마다 넣으면 **새 호출부가
        //    생길 때마다 빠뜨린다.** 본문에 넣으면 호출부가 몇 개가 되어도 자동으로 덮인다.
        //
        // 🔴 **화면에 뜨는 글자를 기록에 싣지 않는다.** 이 팝업의 표시 메서드가 받는 인자는
        //    전부 화면에 그대로 뜨는 글자이고, 그 값을 기록이나 설명문에 옮겨 적으면
        //    「그 글자가 코드에 한 곳(상수)에만 있는가」를 세는 검사가 오염된다.
        //    대신 **구조만** 싣는다 — 제목 자리가 보이는가 · 두 번째 버튼이 숨겨졌는가.
        //    🔴 그 둘이 곧 규칙 D-5 가 정한 구조이므로, 글자를 한 자도 적지 않고
        //    「그 구조로 떴다」를 확인할 수 있다.
        //
        // ⚠️ 존속 축은 **개발**이다(로그 규칙 1.2). 에디터·개발 빌드에서만 의미가 있는 화면 상태
        //    기록이라 운영 축의 이벤트 키를 **새로 만들지 않았다.** 아래 두 컴파일 조건 덕분에
        //    릴리스 빌드에서는 호출도 **문자열 조립도** 통째로 사라진다(로그 규칙 1.7).
        //
        // 🔴 판별을 부르는 쪽이 아니라 이 메서드들 안에서 하는 이유 — 그 컴파일 조건은
        //    **호출문 전체(인자 계산 포함)** 를 지운다. 그래서 판별을 안에 넣으면 릴리스
        //    빌드에는 **조건식조차 남지 않는다.**
        // ====================================================================

        /// <summary>
        /// 팝업이 떴다는 사실을 남긴다(심각도 <b>정보</b> — 의도된 흐름이다).
        ///
        /// <para>
        /// 싣는 두 참/거짓은 <b>실제로 적용된 뒤의 상태를 되읽은 값</b>이다. 「그렇게 하라고
        /// 시켰다」가 아니라 「그렇게 됐다」를 남기는 것이므로, Inspector 배선이 빠져 아무 일도
        /// 일어나지 않은 경우가 그대로 드러난다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>이 줄만으로는 어느 호출 자리에서 온 팝업인지를 가를 수 없다.</b>
        /// 가르려면 화면 글자를 싣거나 부르는 쪽이 표식을 넘겨야 하는데,
        /// 앞쪽은 위 절의 금지 사항이고 뒤쪽은 공개 시그니처 변경이다.
        /// 🔴 <b>어느 자리에서 온 팝업인지는 바로 앞뒤 줄로 읽는다</b> — 예를 들어 규칙 D-6 의
        /// 이탈 알림은 바로 앞에서 요청 팝업이 이탈 때문에 닫히는 기록이 먼저 남는다.
        /// (맵 준비 실패 안내 중 싱글 최초 경기 쪽은 알림 형태가 아니라 확인/취소 형태로 뜬다 —
        /// 그래서 이 줄의 알림 형태 여부 값만으로도 위 이탈 알림과 갈린다.)
        /// </para>
        /// </summary>
        /// <param name="isAlert">true면 알릴 목적의 한 버튼 구조, false면 두 갈래 중 고르는 구조.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogPopupShown(bool isAlert)
        {
            string fields = $"IsAlert={isAlert}, "
                          + $"IsTitleShown={IsTitleShown()}, "
                          + $"IsCancelHidden={IsCancelHidden()}";

            if (isAlert)
            {
                GameLog.Dev.Info("UI", nameof(ConfirmPopup),
                                 "고를 것이 없는 한 버튼 팝업을 띄웠다 — 공통 UI 규칙 D-5 구조",
                                 fields);
                return;
            }

            GameLog.Dev.Info("UI", nameof(ConfirmPopup),
                             "두 갈래 중 고르는 팝업을 띄웠다",
                             fields);
        }

        /// <summary>
        /// 닫기가 불렸다는 사실을 남긴다(심각도 <b>정보</b> — 의도된 흐름이다).
        ///
        /// <para>
        /// ⚠️ <b>이 팝업이 떠 있지 않은 상태에서도 이 메서드는 불린다.</b> 결과 화면이 로비로
        /// 나가는 자리가 「혹시 떠 있으면 닫아라」라는 뜻으로 조건 없이 부르기 때문이다.
        /// 🔴 그래서 <b>닫히기 직전에 팝업이 실제로 떠 있었는지</b>를 함께 싣는다 — 그 값이
        /// 거짓인 줄은 「아무것도 닫지 않은 호출」이라는 뜻이다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>함께 싣는 두 번째 참/거짓</b> — 그 순간 이 팝업이 <b>공용 반투명 막의 점유를
        /// 들고 있었는가</b>다. 앞 문단의 「떠 있었는가」만으로는 판정이 반쪽이다. 닫기 본체는
        /// 「들고 있을 때만」 놓으므로, 이 값이 참인 줄 뒤에는 막을 놓은 결과가 따라오고
        /// <b>거짓인 줄은 아무 몫도 건드리지 않았다</b>는 뜻이 된다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>2026-09-30 이전에는 이 닫기가 점유를 조건 없이 놓아</b> 떠 있지 않았는데도
        /// 불리면 다른 팝업이 정당하게 들고 있던 몫을 가져갈 수 있었다. 그 결함이 고쳐졌으므로
        /// 🔴 <b>이 값은 이제 「고쳐진 검사가 실제로 일하고 있다」를 읽는 자리</b>다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>요청 팝업의 같은 줄과 낱말을 맞췄다.</b> 두 팝업이 같은 이름으로 같은 것을
        /// 실어야 기록을 읽는 사람이 둘을 같은 눈으로 읽는다.
        /// </para>
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogPopupHideRequested()
        {
            GameLog.Dev.Info("UI", nameof(ConfirmPopup),
                             "공통 팝업 닫기가 불렸다",
                             $"IsPanelVisible={(_panel != null && _panel.IsVisible)}, "
                             + $"IsOverlayHeld={_overlayShown}");
        }

        /// <summary>
        /// 제목 자리가 지금 화면에 보이는지. Inspector 미배선이면 항상 거짓이다.
        /// </summary>
        /// <returns>제목 자리가 켜져 있으면 <c>true</c>.</returns>
        private bool IsTitleShown()
        {
            return _titleText != null && _titleText.gameObject.activeSelf;
        }

        /// <summary>
        /// 두 번째 버튼이 지금 숨겨져 있는지. Inspector 미배선이면 「없으니 숨겨진 것과 같다」로
        /// 보아 참을 돌려준다 — 화면에 그 버튼이 없다는 사실이 같기 때문이다.
        /// </summary>
        /// <returns>두 번째 버튼이 화면에 없으면 <c>true</c>.</returns>
        private bool IsCancelHidden()
        {
            return _cancelButton == null || !_cancelButton.gameObject.activeSelf;
        }

        // ====================================================================
        // 내부 콜백
        // ====================================================================

        /// <summary>
        /// 확정 버튼 클릭 핸들러.
        /// 1) 먼저 등록된 콜백을 로컬 변수에 백업 후 필드를 null로 초기화한다.
        ///    (콜백 안에서 다시 Show()를 호출하더라도 상태 꼬임 방지)
        /// 2) Hide()로 팝업을 닫는다.
        /// 3) 백업한 콜백을 마지막에 Invoke — 콜백에서 발생할 수 있는 예외로
        ///    팝업이 안 닫히는 상황을 피한다.
        /// </summary>
        private void OnConfirmClicked()
        {
            Action cb = _onConfirm;
            _onConfirm = null;
            _onCancel = null;
            Hide();
            cb?.Invoke();
        }

        /// <summary>
        /// 취소 버튼 클릭 핸들러. OnConfirmClicked()와 동일한 흐름 — 콜백 분기만 다름.
        /// </summary>
        private void OnCancelClicked()
        {
            Action cb = _onCancel;
            _onConfirm = null;
            _onCancel = null;
            Hide();
            cb?.Invoke();
        }
    }
}
