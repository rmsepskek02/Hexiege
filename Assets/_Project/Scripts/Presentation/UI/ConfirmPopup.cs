// ============================================================================
// ConfirmPopup.cs
// 어떤 메시지/버튼 라벨/콜백이든 주입해 재사용 가능한 범용 확인 팝업.
//
// 사용 시나리오:
//   - 게임 포기 확인
//   - 위험한 동작 직전 사용자 의사 재확인 (예: 건물 철거, 매칭 종료 등)
//
// 사용 예:
//   _confirmPopup.Show(
//       message: "정말 포기하시겠습니까?",
//       confirmLabel: "포기",
//       cancelLabel: "취소",
//       onConfirm: () => DoForfeit(),
//       onCancel: null);
//
// 구조:
//   - _blockingOverlay: 투명 전체 화면 Image. 팝업 활성 시 뒤쪽 UI 입력 차단.
//   - _panel(AnimatedPanel, PopupFade): 실제 시각적인 팝업 박스.
//   - _messageText: 본문 메시지.
//   - _confirmButton / _cancelButton: 두 가지 선택지 버튼.
//
// 동작:
//   Show() 호출 시:
//     1. 메시지 / 라벨 갱신
//     2. 콜백 저장 (외부 onConfirm/onCancel)
//     3. 버튼 onClick 리스너 RemoveAllListeners 후 재등록
//        (Show()가 여러 번 호출돼도 콜백이 누적되지 않도록 보장)
//     4. _blockingOverlay 즉시 표시 (CanvasGroup: alpha=1, blocksRaycasts/interactable=true)
//     5. _panel.Show()
//        (AnimatedPanel은 SetActive 대신 CanvasGroup으로 가시성을 제어하므로,
//         오브젝트는 항상 active 상태를 유지하며 Show()만 호출하면 된다 — 공통 UI 규칙 5)
//
//   Hide() 호출 시:
//     1. _blockingOverlay 즉시 숨김 (CanvasGroup: alpha=0, blocksRaycasts/interactable=false)
//     2. _panel.Hide() — 페이드+스케일 아웃 애니메이션 후 CanvasGroup으로 숨김 처리
//
// Presentation 레이어 — MonoBehaviour 의존.
// ============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        // ⚠️ Inspector 배선은 사용자 작업이다. 배선되지 않아도 기존 동작이 그대로여야 하므로
        //    아래 모든 사용처에서 null 검사를 거친다(이 파일의 다른 필드와 같은 방식).
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

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 팝업을 표시한다. 메시지, 버튼 라벨, 콜백을 모두 인자로 받아 매번 새로 갱신.
        /// </summary>
        /// <param name="message">팝업 본문에 표시될 메시지.</param>
        /// <param name="confirmLabel">확정 버튼에 표시될 텍스트 (예: "포기", "확인").</param>
        /// <param name="cancelLabel">취소 버튼에 표시될 텍스트 (예: "취소").</param>
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
            UIManager.Instance?.ShowBlockingOverlay();
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
        /// <param name="title">팝업 제목 (예: "알림"). 비어 있으면 제목 자리가 숨는다.</param>
        /// <param name="message">본문 메시지 (예: "상대방이 떠났습니다.").</param>
        /// <param name="buttonLabel">유일한 버튼의 라벨 (예: "로비로").</param>
        /// <param name="onClick">버튼 클릭 시 호출될 콜백. null이면 닫히기만 한다.</param>
        public void ShowAlert(string title, string message, string buttonLabel, Action onClick)
        {
            // 0) 제목을 띄우고 취소 버튼을 숨긴다 — 이 둘이 Show() 와의 유일한 차이다.
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
            UIManager.Instance?.ShowBlockingOverlay();

            // 5) 패널 등장
            if (_panel != null)
                _panel.Show();
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

        /// <summary>
        /// 팝업을 닫는다. 외부에서 직접 호출 가능(예: 모달이 떠 있는 동안 게임 종료 시).
        /// 콜백은 호출하지 않는다 — 단순 시각적 닫기.
        /// </summary>
        public void Hide()
        {
            // 입력 차단 오버레이는 즉시 해제 (페이드 아웃 중에도 뒤쪽 조작이 즉시 가능하도록).
            // UIManager 단일 소유 BlockingOverlay를 숨김(중첩 시 참조 카운터로 처리).
            UIManager.Instance?.HideBlockingOverlay();
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
