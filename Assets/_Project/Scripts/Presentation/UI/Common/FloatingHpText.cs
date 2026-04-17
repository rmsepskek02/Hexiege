// ============================================================================
// FloatingHpText.cs
// 피격 시 오브젝트 머리 위에 표시되는 부유 텍스트 오브젝트.
//
// 역할:
//   - 단일 부유 텍스트 인스턴스를 관리.
//   - Play() 호출 시 지정된 월드 좌표에서 위로 이동하면서 페이드아웃.
//   - 애니메이션 완료 후 오브젝트 풀에 반환(콜백 호출).
//
// 사용 흐름:
//   1. FloatingHpTextSpawner가 오브젝트 풀에서 꺼냄.
//   2. Play("150", worldPosition) 호출 → 텍스트 설정 + 애니메이션 시작.
//   3. 1.2초 후 애니메이션 완료 → SetActive(false) + 풀 반환 콜백.
//
// 필수 컴포넌트:
//   - TextMeshPro (3D World Space): 텍스트 표시용 (Inspector에서 연결).
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

        [Tooltip("HP 수치를 표시할 TextMeshPro(3D World Space) 컴포넌트")]
        [SerializeField] private TextMeshPro _text;

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

        [Tooltip("텍스트가 위로 이동하는 거리 (월드 단위). 클수록 더 높이 올라감.")]
        [SerializeField] private float _riseDistance = 0.5f;

        [Tooltip("이동 + 페이드아웃 애니메이션 총 시간 (초). 클수록 오래 표시됨.")]
        [SerializeField] private float _duration = 1.2f;

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
        /// 월드 좌표 기준으로 텍스트를 배치하고, 위로 이동 + 페이드아웃 애니메이션 실행.
        ///
        /// 애니메이션 상세:
        ///   - 시작: worldPosition에 텍스트 배치, alpha = 1 (완전 불투명), rotation = 카메라 정렬.
        ///   - 진행: 로컬 Y축으로 riseDistance 위로 이동 (OutCubic: 처음에 빠르고 끝에서 감속).
        ///          동시에 TMP alpha가 0으로 페이드아웃.
        ///   - 완료: SetActive(false) 후 풀 반환 콜백 호출.
        /// </summary>
        /// <param name="text">표시할 텍스트 (예: 남은 HP 수치).</param>
        /// <param name="worldPosition">월드 공간 시작 위치 (피격 오브젝트 머리 위).</param>
        /// <param name="scale">
        ///   텍스트 스케일 비율. 기본값 1f.
        ///   transform.localScale과 이동 거리에 동시 적용됨.
        /// </param>
        /// <param name="color">
        ///   텍스트 색상. null이면 흰색(Color.white) 적용.
        ///   Spawner가 피격 대상 팀에 따라 색상을 지정할 때 사용.
        /// </param>
        public void Play(string text, Vector3 worldPosition, float scale = 1f, Color? color = null)
        {
            // 이전 애니메이션이 진행 중이면 즉시 정리하여 상태 충돌 방지
            if (_currentSequence != null && _currentSequence.IsActive())
            {
                _currentSequence.Kill();
                _currentSequence = null;
            }

            // 텍스트 내용 + 색상 설정
            if (_text != null)
            {
                _text.text = text;

                // null 병합 연산자(??): 좌측이 null이면 우측(Color.white) 사용
                _text.color = color ?? Color.white;

                // TMP 자체 알파를 1로 초기화 (이전 애니메이션으로 0이 되어 있을 수 있음)
                _text.alpha = 1f;
            }

            // 월드 위치로 직접 배치
            transform.position = worldPosition;

            // 카메라를 정면으로 바라보도록 회전 정렬 (빌보드 효과).
            // LookRotation(-forward): 텍스트의 +Z 방향이 카메라를 향하도록 설정.
            // Camera.transform.rotation을 그대로 복사하면 텍스트가 카메라와 같은 방향을 바라봐
            // 카메라에 등을 보여 화면에 렌더되지 않는 문제가 발생함.
            if (Camera.main != null)
            {
                transform.rotation = Quaternion.LookRotation(
                    -Camera.main.transform.forward,
                    Camera.main.transform.up);
            }

            // 스케일 적용.
            // X축을 음수로 설정하는 이유:
            //   LookRotation(-forward, up)은 수학적으로 텍스트 로컬 X축을 -cameraRight 방향으로 만든다.
            //   이 때문에 텍스트가 좌우 반전되어 표시됨.
            //   X scale을 음수로 한 번 더 뒤집으면 원래 방향으로 복원됨.
            //   TMP 3D 기본 머티리얼은 Cull Off(양면 렌더링)이므로 음수 스케일이어도 정상 표시됨.
            float s = Mathf.Max(scale, 0.1f);
            transform.localScale = new Vector3(-s, s, s);

            gameObject.SetActive(true);

            // DOTween 시퀀스 생성: 로컬 Y축 이동 + TMP 페이드아웃 동시 실행
            _currentSequence = DOTween.Sequence();

            // 로컬 Y축으로 riseDistance 만큼 위로 이동.
            // DOLocalMoveY를 쓰는 이유: 카메라 회전이 적용된 transform의 "위쪽"(로컬 Y)으로 올라가야 화면상 수직 상승처럼 보임.
            // OutCubic: 시작이 빠르고 끝이 느려져 자연스러운 감속 효과.
            float targetLocalY = transform.localPosition.y + _riseDistance * scale;
            _currentSequence.Join(
                transform.DOLocalMoveY(targetLocalY, _duration)
                         .SetEase(Ease.OutCubic));

            // TMP 알파를 0으로 페이드아웃 (이동과 동시 진행).
            // DOFade: DG.Tweening.TMPro 확장 메서드 — DG.Tweening using 필요.
            if (_text != null)
            {
                _currentSequence.Join(
                    _text.DOFade(0f, _duration));
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
