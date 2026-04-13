// ============================================================================
// FloatingHpText.cs
// 피격 시 오브젝트 머리 위에 표시되는 부유 텍스트 오브젝트.
//
// 역할:
//   - 단일 부유 텍스트 인스턴스를 관리.
//   - Play() 호출 시 지정된 Canvas 로컬 좌표에서 위로 이동하면서 페이드아웃.
//   - 애니메이션 완료 후 오브젝트 풀에 반환(콜백 호출).
//
// 사용 흐름:
//   1. FloatingHpTextSpawner가 오브젝트 풀에서 꺼냄.
//   2. Play("150", localPoint) 호출 → 텍스트 설정 + 애니메이션 시작.
//   3. 1.2초 후 애니메이션 완료 → SetActive(false) + 풀 반환 콜백.
//
// 필수 컴포넌트:
//   - TextMeshProUGUI: 텍스트 표시용 (Inspector에서 연결).
//   - CanvasGroup: 알파 페이드 제어용 (Inspector에서 연결).
//
// Presentation 레이어 — DOTween, TMPro 의존.
// ============================================================================

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 피격 시 남은 HP를 표시하는 단일 부유 텍스트 오브젝트.
    /// 오브젝트 풀에서 관리되며, Play() 호출로 애니메이션 재생 후 자동 반환.
    /// </summary>
    public class FloatingHpText : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Tooltip("HP 수치를 표시할 TextMeshProUGUI 컴포넌트")]
        [SerializeField] private TextMeshProUGUI _text;

        [Tooltip("페이드 애니메이션용 CanvasGroup 컴포넌트")]
        [SerializeField] private CanvasGroup _canvasGroup;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 애니메이션 완료 시 호출되는 풀 반환 콜백.
        /// FloatingHpTextSpawner가 SetReturnCallback()으로 주입.
        /// </summary>
        private Action<FloatingHpText> _onReturn;

        /// <summary>
        /// 현재 실행 중인 DOTween 시퀀스 참조.
        /// Play() 재호출 시 이전 트윈을 정리하기 위해 캐싱.
        /// </summary>
        private Sequence _currentSequence;

        // ====================================================================
        // Inspector 설정값 (런타임에 자유롭게 조정 가능)
        // ====================================================================

        [Header("애니메이션 설정")]

        [Tooltip("텍스트가 위로 이동하는 거리 (픽셀 단위). 클수록 더 높이 올라감.")]
        [SerializeField] private float _riseDistance = 80f;

        [Tooltip("이동 + 페이드아웃 애니메이션 총 시간 (초). 클수록 오래 표시됨.")]
        [SerializeField] private float _duration = 1.2f;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// CanvasGroup 초기 설정.
        /// blocksRaycasts = false로 설정하여, 부유 텍스트가 터치/클릭 입력을 가로채지 않도록 방지.
        /// (설정하지 않으면 텍스트 영역 위의 터치가 무시되는 문제 발생)
        /// </summary>
        private void Awake()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
            }
        }

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 풀 반환 콜백 설정. FloatingHpTextSpawner가 풀에서 생성 직후 호출.
        /// 이 콜백이 애니메이션 완료 시 호출되어 오브젝트가 풀로 돌아감.
        /// </summary>
        /// <param name="onReturn">풀 반환 처리 콜백. null이면 반환 동작 없음.</param>
        public void SetReturnCallback(Action<FloatingHpText> onReturn)
        {
            _onReturn = onReturn;
        }

        /// <summary>
        /// 부유 텍스트 애니메이션 재생.
        /// 지정된 Canvas 로컬 좌표에 텍스트를 배치하고, 위로 이동 + 페이드아웃 애니메이션 실행.
        ///
        /// 애니메이션 상세:
        ///   - 시작: anchoredPosition에 텍스트 배치, alpha = 1 (완전 불투명).
        ///   - 진행: Y축으로 riseDistance * scale 위로 이동 (OutCubic: 처음에 빠르고 끝에서 감속).
        ///          동시에 alpha가 0으로 페이드아웃.
        ///   - 완료: SetActive(false) 후 풀 반환 콜백 호출.
        /// </summary>
        /// <param name="text">표시할 텍스트 (예: 남은 HP 수치).</param>
        /// <param name="anchoredPosition">Canvas 로컬 좌표 기준 시작 위치.</param>
        /// <param name="scale">
        ///   카메라 줌 기반 UI 스케일 비율.
        ///   1f = 기준 줌, 0.5f = 절반 크기, 2f = 두 배 크기.
        ///   transform.localScale과 이동 거리에 동시 적용됨.
        /// </param>
        public void Play(string text, Vector2 anchoredPosition, float scale = 1f)
        {
            // 이전 애니메이션이 진행 중이면 즉시 정리하여 상태 충돌 방지
            if (_currentSequence != null && _currentSequence.IsActive())
            {
                _currentSequence.Kill();
                _currentSequence = null;
            }

            // 텍스트 내용 설정
            if (_text != null)
            {
                _text.text = text;
            }

            // 줌 스케일 적용: 카메라 줌 비율에 따라 텍스트 전체 크기 조정
            // localScale을 변경하면 텍스트 크기와 RectTransform 시각 크기가 함께 조정됨
            transform.localScale = Vector3.one * Mathf.Max(scale, 0.1f);

            // 초기 상태 설정: 완전 불투명 + 지정 위치
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
            gameObject.SetActive(true);

            // RectTransform의 anchoredPosition을 시작 위치로 설정
            RectTransform rt = transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = anchoredPosition;
            }

            // DOTween 시퀀스 생성: 위로 이동 + 페이드아웃 동시 실행
            _currentSequence = DOTween.Sequence();

            // Y축으로 RiseDistance(80px) 만큼 위로 이동
            // OutCubic: 시작이 빠르고 끝이 느려져 자연스러운 감속 효과
            if (rt != null)
            {
                _currentSequence.Join(
                    // 이동 거리도 scale 적용: 줌 아웃 시 더 짧게 이동하여 시각적으로 자연스럽게 보임
                    rt.DOAnchorPosY(anchoredPosition.y + _riseDistance * scale, _duration)
                      .SetEase(Ease.OutCubic));
            }

            // 알파를 0으로 페이드아웃 (이동과 동시 진행)
            if (_canvasGroup != null)
            {
                _currentSequence.Join(
                    _canvasGroup.DOFade(0f, _duration));
            }

            // 애니메이션 완료 시: 오브젝트 비활성화 + 풀 반환
            _currentSequence.OnComplete(() =>
            {
                gameObject.SetActive(false);
                _onReturn?.Invoke(this);
            });
        }

        // ====================================================================
        // 정리
        // ====================================================================

        /// <summary>
        /// 오브젝트 파괴 시 실행 중인 DOTween 시퀀스 정리.
        /// 정리하지 않으면 파괴된 오브젝트에 대한 트윈이 남아 에러 발생 가능.
        /// </summary>
        private void OnDestroy()
        {
            if (_currentSequence != null && _currentSequence.IsActive())
            {
                _currentSequence.Kill();
                _currentSequence = null;
            }
        }
    }
}
