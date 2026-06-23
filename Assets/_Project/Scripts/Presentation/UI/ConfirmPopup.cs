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
using Hexiege.Infrastructure;

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

        [Header("색상 설정")]
        [Tooltip("프로젝트 전체에서 공유하는 UI 색상 설정 에셋. " +
                 "Resources/Config/UIColorConfig.asset 을 연결하면 " +
                 "Awake() 시점에 확인/취소 버튼 배경색이 자동으로 적용된다.")]
        [SerializeField] private UIColorConfig _colorConfig;


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
        // Unity 생명주기
        // ====================================================================

        /// <summary>
        /// 컴포넌트 초기화 시점.
        ///
        /// 버튼 배경 색상은 매 Show() 호출마다 바뀌지 않는 고정값이다.
        /// 따라서 매번 Show()에서 색상을 재할당하는 대신, 컴포넌트가 처음 활성화될 때
        /// 단 1회만 색상을 적용하여 불필요한 GC/할당을 최소화한다.
        ///
        /// 안전 가드:
        ///   - _colorConfig가 Inspector에서 연결되지 않은 경우 즉시 return.
        ///     (기존 UI 색상이 그대로 유지되므로 시각적 깨짐은 발생하지 않음)
        ///   - _confirmButton / _cancelButton 중 하나라도 연결되지 않으면
        ///     해당 항목만 건너뛴다 — 부분 연결 상태에서도 동작.
        /// </summary>
        private void Awake()
        {
            // 색상 설정 에셋이 연결되지 않았다면 색상 적용을 건너뛴다.
            // 이 경우 Inspector에서 수동으로 지정한 기존 색상이 그대로 사용된다.
            if (_colorConfig == null) return;

            // 확인 버튼 배경 색상 (예: 포기 버튼 — 보통 초록색).
            // Button.targetGraphic은 Button이 색상 전환에 사용하는 Image를 가리킨다.
            if (_confirmButton != null)
                _confirmButton.targetGraphic.color = _colorConfig.confirmButtonColor;

            // 취소 버튼 배경 색상 (예: 취소 버튼 — 보통 빨간색).
            if (_cancelButton != null)
                _cancelButton.targetGraphic.color = _colorConfig.cancelButtonColor;
        }

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
