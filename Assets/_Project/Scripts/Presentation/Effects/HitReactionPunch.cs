// ============================================================================
// HitReactionPunch.cs
// 피격 시 대상 GameObject에 짧은 "스케일 펀치"(움찔) 반응을 주는 컴포넌트.
//
// 왜 별도 컴포넌트인가:
//   스케일 펀치는 원래 크기를 잠깐 키웠다가 되돌리는 연출이다.
//   이때 반드시 "원래 스케일"을 정확히 캐시해 두었다가 복원해야 한다.
//   만약 펀치가 진행 중(스케일이 이미 부풀려진 상태)에 또 원래 스케일을
//   현재 값으로 다시 캐시하면, 부풀려진 값이 "원래 값"으로 굳어져
//   타일/유닛의 스케일이 영구적으로 커지는 버그가 생긴다.
//   → 이 컴포넌트는 Awake()에서 (아직 펀치 전인) 원래 스케일을 딱 한 번만 캐시하고,
//     연속 피격 시에는 진행 중 코루틴을 멈추고 원래 스케일로 되돌린 뒤 다시 시작한다.
//     (중첩 방지 + 원 스케일 보존)
//
// 사용법:
//   HitReactionPunch.Play(targetGameObject);  // get-or-add 후 즉시 재생 (정적 헬퍼)
//
// 부착:
//   런타임에 필요할 때 대상 GameObject에 AddComponent로 자동 부착된다.
//   AddComponent 시점에 Awake()가 즉시 호출되어, 아직 펀치가 없는 "원래 스케일"이
//   안전하게 캐시된다.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Coroutine).
// ============================================================================

using System.Collections;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 피격 대상에 짧은 스케일 펀치(움찔) 반응을 재생하는 컴포넌트.
    /// 원래 스케일을 캐시 후 항상 복원하여 영구 스케일 변형을 방지한다.
    /// </summary>
    public class HitReactionPunch : MonoBehaviour
    {
        // ====================================================================
        // 연출 파라미터 (기본값 — 필요 시 Play 오버로드로 조정 가능)
        // ====================================================================

        /// <summary> 펀치로 커지는 비율. 0.08 = 원 스케일의 +8%. </summary>
        private const float DefaultMagnitude = 0.08f;

        /// <summary> 펀치 총 지속 시간(초). 커졌다가 되돌아오는 데 걸리는 시간. </summary>
        private const float DefaultDuration = 0.12f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 대상의 원래 로컬 스케일. Awake()에서 (펀치 전에) 딱 한 번 캐시.
        /// 타일 Y Scale 0.4 등 비균등 스케일도 그대로 보존된다.
        /// </summary>
        private Vector3 _originalScale;

        /// <summary> 원래 스케일 캐시 완료 여부. </summary>
        private bool _cached;

        /// <summary> 현재 진행 중인 펀치 코루틴. 연속 피격 시 중첩 방지에 사용. </summary>
        private Coroutine _routine;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        /// <summary>
        /// AddComponent 시점(=아직 펀치가 일어나기 전)에 원래 스케일을 캐시한다.
        /// 이 시점의 localScale은 프리팹 원본 크기이므로 안전하다.
        /// </summary>
        private void Awake()
        {
            CacheOriginalScale();
        }

        /// <summary> 원래 스케일을 1회만 캐시. 이미 캐시했으면 무시. </summary>
        private void CacheOriginalScale()
        {
            if (_cached) return;
            _originalScale = transform.localScale;
            _cached = true;
        }

        // ====================================================================
        // 정적 헬퍼 — 외부 호출 진입점
        // ====================================================================

        /// <summary>
        /// 대상 GameObject에 스케일 펀치를 재생한다.
        /// 컴포넌트가 없으면 자동으로 부착(AddComponent)한 뒤 재생한다.
        /// </summary>
        /// <param name="target">피격 반응을 줄 대상 GameObject. null이면 무시.</param>
        public static void Play(GameObject target)
        {
            if (target == null) return;

            // 이미 붙어 있으면 재사용, 없으면 새로 부착(그 순간 Awake가 원 스케일을 캐시).
            HitReactionPunch punch = target.GetComponent<HitReactionPunch>();
            if (punch == null)
                punch = target.AddComponent<HitReactionPunch>();

            punch.PlayPunch(DefaultMagnitude, DefaultDuration);
        }

        // ====================================================================
        // 재생 로직
        // ====================================================================

        /// <summary>
        /// 스케일 펀치를 시작한다. 진행 중이면 멈추고 원 스케일로 되돌린 뒤 다시 시작(중첩 방지).
        /// </summary>
        /// <param name="magnitude">커지는 비율(예: 0.08 = +8%).</param>
        /// <param name="duration">총 지속 시간(초).</param>
        public void PlayPunch(float magnitude, float duration)
        {
            // 방어적으로 캐시 보장 (Awake 이전 호출 가능성 대비).
            CacheOriginalScale();

            // 이미 펀치 중이면 즉시 멈추고 원 스케일로 리셋 후 재시작.
            // → 부풀려진 상태가 다음 펀치의 기준이 되지 않도록 하여 스케일 누적을 막는다.
            if (_routine != null)
            {
                StopCoroutine(_routine);
                transform.localScale = _originalScale;
            }

            _routine = StartCoroutine(PunchRoutine(magnitude, duration));
        }

        /// <summary>
        /// 원 스케일 → 최대(원*(1+magnitude)) → 원 스케일로 되돌아오는 코루틴.
        /// 어떤 경로로 끝나더라도 마지막에 반드시 원 스케일로 정확히 복원한다.
        /// </summary>
        private IEnumerator PunchRoutine(float magnitude, float duration)
        {
            Vector3 peakScale = _originalScale * (1f + magnitude);
            float half = duration * 0.5f;

            // 1단계: 원 → 피크 (커지기)
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float k = half > 0f ? Mathf.Clamp01(t / half) : 1f;
                transform.localScale = Vector3.Lerp(_originalScale, peakScale, k);
                yield return null;
            }

            // 2단계: 피크 → 원 (되돌아오기)
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float k = half > 0f ? Mathf.Clamp01(t / half) : 1f;
                transform.localScale = Vector3.Lerp(peakScale, _originalScale, k);
                yield return null;
            }

            // 마무리: 부동소수점 오차 없이 정확히 원 스케일로 복원.
            transform.localScale = _originalScale;
            _routine = null;
        }

        /// <summary>
        /// 컴포넌트/오브젝트 비활성·파괴 시 원 스케일을 확실히 복원.
        /// 코루틴이 중간에 끊겨 부풀려진 스케일로 남는 것을 방지.
        /// </summary>
        private void OnDisable()
        {
            if (_cached)
                transform.localScale = _originalScale;
            _routine = null;
        }
    }
}
