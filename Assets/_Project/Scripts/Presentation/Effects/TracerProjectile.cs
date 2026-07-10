// ============================================================================
// TracerProjectile.cs
// 원거리 유닛이 공격할 때 "발사 지점 → 타겟 지점"으로 직선 비행하는
// 연출 전용 발사체(트레이서) 컴포넌트.
//
// 배경 (전투 타격 타이밍 동기화 Phase 3 — 축 4 / 3-2):
//   원거리 유닛(활/총 등)은 공격 순간 곧바로 데미지가 들어가면 화면상 어색하다.
//   실제로는 화살/탄환이 날아가서 "맞는 순간"에 피격 연출이 터져야 자연스럽다.
//   이 트레이서는 순수 시각 표현이며, 데미지 판정(서버 권위)에는 전혀 관여하지 않는다.
//
// 핵심 동작:
//   1) Launch(start, target, onArrive)로 발사 지점/도착 지점/착탄 콜백을 받는다.
//   2) 매 프레임 start→target을 향해 일정 속도로 직선 이동(Lerp)한다.
//   3) 도착하면 onArrive 콜백을 호출한다.
//      → 이 콜백이 HitPresentationQueue의 피격 연출 방출을 트리거한다
//        (UnitView가 OnLocalAttackHit 발행을 이 콜백 안에서 수행).
//   4) 착탄 후 Pool에 반환(콜백이 없으면 스스로 파괴)한다.
//
// 타겟이 비행 중 파괴되어도 문제없다:
//   Launch 시점의 타겟 "월드 좌표"를 값으로 복사해 두므로, 비행 중 타겟 GameObject가
//   사라져도 마지막으로 조준한 지점까지 그대로 날아가 소멸한다(댕글링 참조 없음).
//
// 풀링:
//   VfxPoolItem과 동일한 패턴. EffectManager가 Instantiate 시 이 컴포넌트가 없으면
//   자동 AddComponent하고, Setup(prefab, onReturn)으로 원본 프리팹/반환 콜백을 연결한다.
//   따라서 트레이서 프리팹에 이 컴포넌트를 미리 붙여둘 필요는 없다.
//   (다만 비행 속도(_speed)를 프리팹별로 조정하려면 프리팹에 미리 붙이고 값을 지정한다.)
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Coroutine 없이 Update 기반).
// ============================================================================

using System;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 원거리 공격 연출용 발사체(트레이서). 발사 지점에서 타겟 지점까지 직선 비행 후
    /// 착탄 콜백을 호출하고 Pool에 반환한다. 순수 시각 표현이며 데미지와 무관하다.
    /// </summary>
    public class TracerProjectile : MonoBehaviour
    {
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Tooltip("비행 속도 (월드 유닛/초). 한 타일 폭이 약 0.866이므로 14면 한 타일을 약 0.06초에 통과 — "
            + "연출용으로 짧게 잡는다. 프리팹별로 조정 가능.")]
        [SerializeField] private float _speed = 14f;

        // ====================================================================
        // 풀링 상태 (Setup()에서 주입)
        // ====================================================================

        /// <summary>
        /// 이 인스턴스의 원본 프리팹. EffectManager Pool Dictionary의 키.
        /// (같은 프리팹끼리만 같은 Queue에 반환해야 하므로 필요.)
        /// </summary>
        private GameObject _sourcePrefab;

        /// <summary> Pool 반환 콜백. EffectManager.OnTracerReturn으로 연결. null이면 파괴로 폴백. </summary>
        private Action<TracerProjectile> _onReturn;

        /// <summary> EffectManager가 Pool 반환 시 키로 사용하는 원본 프리팹. </summary>
        public GameObject SourcePrefab => _sourcePrefab;

        // ====================================================================
        // 비행 상태
        // ====================================================================

        /// <summary> 발사 지점(월드). </summary>
        private Vector3 _start;

        /// <summary> 도착(착탄) 지점(월드). Launch 시점의 타겟 위치를 값으로 복사. </summary>
        private Vector3 _target;

        /// <summary> 비행 시작 후 경과 시간(초). </summary>
        private float _elapsed;

        /// <summary> 총 비행 시간(초) = 거리 / 속도. 0이면 즉시 착탄. </summary>
        private float _duration;

        /// <summary> 착탄 시 1회 호출할 콜백(피격 연출 방출 트리거). </summary>
        private Action _onArrive;

        /// <summary> 현재 비행 중인지 여부. 중복 착탄/반환 방지 플래그. </summary>
        private bool _flying;

        // ====================================================================
        // 초기화 — EffectManager에서 호출
        // ====================================================================

        /// <summary>
        /// 생성 직후 1회 초기화. EffectManager가 인스턴스를 만들 때 호출.
        /// 원본 프리팹과 Pool 반환 콜백을 저장해 둔다.
        /// </summary>
        /// <param name="prefab">이 인스턴스의 원본 프리팹 (Pool 키).</param>
        /// <param name="onReturn">착탄 후 호출할 Pool 반환 콜백. null이면 Destroy로 폴백.</param>
        public void Setup(GameObject prefab, Action<TracerProjectile> onReturn)
        {
            _sourcePrefab = prefab;
            _onReturn = onReturn;
        }

        // ====================================================================
        // 발사
        // ====================================================================

        /// <summary>
        /// 트레이서를 발사한다. EffectManager가 Pool에서 꺼낸 직후 호출.
        /// 발사 지점에서 타겟 지점까지 직선 비행하며, 도착 시 onArrive를 1회 호출한다.
        /// </summary>
        /// <param name="start">발사 지점(월드). 보통 유닛의 총구/발사점.</param>
        /// <param name="target">도착 지점(월드). Launch 시점의 타겟 위치(값 복사).</param>
        /// <param name="onArrive">착탄 시 호출할 콜백. 피격 연출 방출을 트리거한다. null 허용.</param>
        public void Launch(Vector3 start, Vector3 target, Action onArrive)
        {
            _start = start;
            _target = target;
            _onArrive = onArrive;
            _elapsed = 0f;

            // 비행 시간 = 거리 / 속도. 속도가 0 이하(오설정)면 즉시 착탄으로 처리.
            float distance = Vector3.Distance(start, target);
            _duration = _speed > 0f ? distance / _speed : 0f;

            // 발사 지점으로 위치 스냅 + 진행 방향으로 회전(트레이서 메시가 forward를 향하도록).
            transform.position = start;
            Vector3 dir = target - start;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);

            gameObject.SetActive(true);
            _flying = true;

            // 거리가 0에 가까우면(발사=타겟) 비행 없이 즉시 착탄.
            if (_duration <= 0f)
                Arrive();
        }

        // ====================================================================
        // 매 프레임 비행
        // ====================================================================

        /// <summary>
        /// 매 프레임 start→target 직선 보간으로 이동. 진행도 t가 1이 되면 착탄.
        /// </summary>
        private void Update()
        {
            if (!_flying) return;

            _elapsed += Time.deltaTime;
            // _duration이 0이면 Launch에서 이미 Arrive 처리됐지만, 방어적으로 t=1 처리.
            float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            transform.position = Vector3.Lerp(_start, _target, t);

            if (t >= 1f)
                Arrive();
        }

        // ====================================================================
        // 착탄 + 반환
        // ====================================================================

        /// <summary>
        /// 착탄 처리: 착탄 콜백을 1회 호출한 뒤 Pool에 반환(또는 파괴).
        /// _flying 플래그로 중복 호출을 차단한다.
        /// </summary>
        private void Arrive()
        {
            if (!_flying) return;
            _flying = false;

            // 착탄 콜백 먼저 실행 — 피격 연출 방출 트리거(OnLocalAttackHit 발행 등).
            // 콜백을 지역 변수로 옮겨 null 처리 후 호출(재진입/중복 방지).
            Action cb = _onArrive;
            _onArrive = null;
            cb?.Invoke();

            // Pool 반환(콜백이 있으면) 또는 파괴(폴백). 비활성화 후 반환하여 다음 재사용 대비.
            if (_onReturn != null)
            {
                gameObject.SetActive(false);
                _onReturn(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
