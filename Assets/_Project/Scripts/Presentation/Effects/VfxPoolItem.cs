// ============================================================================
// VfxPoolItem.cs
// 파티클(VFX) 하나의 재생 수명을 관리하고, 재생이 끝나면 스스로 Pool에 반환하는
// 컴포넌트.
//
// 왜 필요한가:
//   매번 Instantiate/Destroy를 하면 GC(가비지 컬렉션) 부하가 발생한다.
//   미리 만들어 둔 파티클 오브젝트를 EffectManager의 Pool에 보관하고,
//   필요할 때 꺼내 재생하고, 재생이 끝나면 다시 Pool에 반환하여 재사용한다.
//   FloatingHpTextSpawner와 동일한 Object Pool 패턴이다.
//
// 자동 반환 원리:
//   ParticleSystem.IsAlive(true) → 파티클이 아직 살아있는 동안 true,
//   모든 파티클이 소멸하면 false를 반환한다.
//   Update()에서 매 프레임 검사하여 false가 되는 순간 Return()을 호출한다.
//
// 부착 위치:
//   EffectManager가 VFX 프리팹을 Instantiate할 때 이 컴포넌트가 없으면
//   자동으로 AddComponent한다. 따라서 프리팹에 미리 붙여둘 필요는 없다.
//   (다만 ParticleSystem 컴포넌트는 프리팹 루트에 있어야 한다.)
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, ParticleSystem).
// ============================================================================

using System;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 파티클 재생이 끝나면 스스로 EffectManager Pool에 반환하는 컴포넌트.
    /// EffectManager의 VFX Pool 항목 단위.
    /// </summary>
    public class VfxPoolItem : MonoBehaviour
    {
        /// <summary> 루트에 있는 파티클 시스템. 있으면 이것만 Play(하위 포함)로 재생. Awake에서 캐시. </summary>
        private ParticleSystem _ps;

        /// <summary>
        /// 루트에 파티클 시스템이 없고 "여러 형제 시스템"으로 구성된 프리팹을 위한 직속 자식 시스템 목록.
        /// 예: TorrentSpirit 파도(vfx_torrentspirit_attack) — 빈 루트 아래
        ///     Main_Water_Surge / Ground_Impact_Ripples / Front_Foam_Crest 3개가 형제로 붙어 있다.
        /// ParticleSystem.Play()는 "형제" 시스템을 재생하지 않으므로, 이 경우 각 형제를 개별 재생해야
        /// 한다. (안 그러면 첫 번째 하나만 보이고 나머지는 안 나온다.)
        /// </summary>
        private ParticleSystem[] _childSystems;

        /// <summary>
        /// 이 인스턴스가 어떤 원본 프리팹에서 만들어졌는지.
        /// EffectManager의 Pool Dictionary 키로 사용된다.
        /// (같은 프리팹끼리만 같은 Queue에 반환해야 하므로 필요.)
        /// </summary>
        private GameObject _sourcePrefab;

        /// <summary> Pool 반환 콜백. EffectManager.OnVfxReturn으로 연결. </summary>
        private Action<VfxPoolItem> _onReturn;

        /// <summary>
        /// 현재 재생 중인지 여부.
        /// 중복 반환(Return이 두 번 호출되는 것)을 막기 위한 플래그.
        /// </summary>
        private bool _active;

        /// <summary>프리팹이 가진 원본 로컬 스케일. 풀 재사용 때 이전 프리셋 배율이 누적되지 않도록 기준으로 사용한다.</summary>
        private Vector3 _originalLocalScale;

        /// <summary>현재 재생의 전진 이동 시작 위치와 방향.</summary>
        private Vector3 _travelStartPosition;
        private Vector3 _travelDirection;
        private float _travelDistance;
        private float _travelDuration;
        private float _travelElapsed;
        private bool _isTravelling;

        /// <summary> EffectManager가 Pool 반환 시 키로 사용하는 원본 프리팹. </summary>
        public GameObject SourcePrefab => _sourcePrefab;

        /// <summary>
        /// 파티클 시스템 캐시.
        /// 루트에 없으면 자식에서 탐색 (프리팹 구조 유연성 확보).
        /// </summary>
        private void Awake()
        {
            _originalLocalScale = transform.localScale;
            // 1순위: 루트에 파티클 시스템이 있으면 그것으로 충분(Play(true)가 하위 시스템까지 재생).
            _ps = GetComponent<ParticleSystem>();

            // 루트에 없으면: 빈 루트 아래 여러 형제 파티클 시스템으로 구성된 프리팹이다.
            //   GetComponentInChildren는 "첫 번째" 하나만 반환하고 Play()도 형제는 재생하지 않으므로,
            //   직속 자식 파티클 시스템을 전부 모아 각각 재생해야 한다(안 그러면 일부만 보인다).
            if (_ps == null)
            {
                var list = new System.Collections.Generic.List<ParticleSystem>();
                foreach (Transform child in transform)
                {
                    var childPs = child.GetComponent<ParticleSystem>();
                    if (childPs != null) list.Add(childPs);
                }

                // 직속 자식에도 없으면(더 깊이 중첩된 구조) 마지막 폴백으로 하위 전체에서 하나 탐색.
                if (list.Count == 0)
                {
                    var nested = GetComponentInChildren<ParticleSystem>();
                    if (nested != null) list.Add(nested);
                }

                _childSystems = list.ToArray();
            }
        }

        /// <summary>
        /// 생성 직후 1회 초기화. EffectManager가 인스턴스를 만들 때 호출.
        /// 원본 프리팹과 반환 콜백을 저장해 둔다.
        /// </summary>
        /// <param name="prefab">이 인스턴스의 원본 프리팹 (Pool 키).</param>
        /// <param name="onReturn">재생 완료 시 호출할 Pool 반환 콜백.</param>
        public void Setup(GameObject prefab, Action<VfxPoolItem> onReturn)
        {
            _sourcePrefab = prefab;
            _onReturn = onReturn;
        }

        /// <summary>
        /// 지정한 위치/회전에서 파티클을 재생한다.
        /// EffectManager가 Pool에서 꺼낸 직후 호출.
        /// </summary>
        /// <param name="pos">재생할 월드 좌표.</param>
        /// <param name="rot">재생할 회전.</param>
        /// <param name="localPositionOffset">rot을 기준으로 월드 변환할 로컬 위치 보정값.</param>
        /// <param name="eulerRotationOffset">rot 뒤에 추가로 적용할 Euler 회전 보정값(도).</param>
        /// <param name="scaleMultiplier">프리팹 원본 로컬 스케일에 곱할 재생별 배율.</param>
        /// <param name="forwardTravelDistance">rot의 forward 방향으로 이동할 연출 거리. 0이면 이동하지 않음.</param>
        /// <param name="forwardTravelDuration">전진 거리에 도달할 시간(초). 0이면 이동하지 않음.</param>
        public void Play(
            Vector3 pos,
            Quaternion rot,
            Vector3 localPositionOffset,
            Vector3 eulerRotationOffset,
            Vector3 scaleMultiplier,
            float forwardTravelDistance,
            float forwardTravelDuration)
        {
            // 풀에서 직전에 사용한 프리셋의 위치/회전/스케일/이동 상태가 남지 않도록
            // 모든 재생별 상태를 원본 기준으로 명시 초기화한다.
            ResetTravelState();
            transform.position = pos + rot * localPositionOffset;
            transform.rotation = rot * Quaternion.Euler(eulerRotationOffset);
            transform.localScale = Vector3.Scale(_originalLocalScale, scaleMultiplier);

            _travelStartPosition = transform.position;
            _travelDirection = rot * Vector3.forward;
            _travelDistance = Mathf.Max(0f, forwardTravelDistance);
            _travelDuration = Mathf.Max(0f, forwardTravelDuration);
            _isTravelling = _travelDistance > 0f && _travelDuration > 0f;

            gameObject.SetActive(true);
            _active = true;

            // 루트 PS가 있으면 그것만(하위 포함) 재생. 없으면 형제 시스템을 전부 재생.
            if (_ps != null)
            {
                // 이전 재생 잔여 파티클을 제거하고 처음부터 재생. Play()=Play(true)로 하위까지 함께.
                _ps.Clear();
                _ps.Play();
            }
            else if (_childSystems != null)
            {
                // 형제 파티클 시스템을 각각 개별 재생(각자의 하위 포함). 하나만 재생하면 나머지가 안 보인다.
                for (int i = 0; i < _childSystems.Length; i++)
                {
                    var cs = _childSystems[i];
                    if (cs == null) continue;
                    cs.Clear();
                    cs.Play();
                }
            }
        }

        /// <summary>
        /// 매 프레임 파티클 소멸 여부를 검사.
        /// 재생 중(_active)이고 파티클이 모두 사라졌으면(IsAlive == false) Pool에 반환.
        /// IsAlive(true): 자식 파티클까지 포함하여 살아있는지 확인.
        /// </summary>
        private void Update()
        {
            if (!_active) return;

            UpdateForwardTravel();

            // 파티클이 모두 소멸했으면 Pool에 반환. 루트 PS든 형제 시스템 묶음이든 "하나라도 살아있으면" 유지.
            // 파티클 시스템이 아예 없는 비정상 케이스는 AnyAlive가 false → 즉시 반환하여 영구 점유 방지.
            if (!AnyAlive())
            {
                Return();
            }
        }

        /// <summary>
        /// 현재 VFX를 공격 당시 forward 방향으로 선형 이동시킨다.
        /// 이 이동은 화면 연출에만 사용되며 게임의 피해/힐 판정이나 유닛 위치를 변경하지 않는다.
        /// </summary>
        private void UpdateForwardTravel()
        {
            if (!_isTravelling) return;

            _travelElapsed = Mathf.Min(_travelElapsed + Time.deltaTime, _travelDuration);
            float progress = _travelElapsed / _travelDuration;
            transform.position = _travelStartPosition + _travelDirection * (_travelDistance * progress);

            if (_travelElapsed >= _travelDuration)
                _isTravelling = false;
        }

        /// <summary>풀 반환 또는 새 재생 전에 모든 전진 이동 상태를 기본값으로 되돌린다.</summary>
        private void ResetTravelState()
        {
            _travelStartPosition = Vector3.zero;
            _travelDirection = Vector3.zero;
            _travelDistance = 0f;
            _travelDuration = 0f;
            _travelElapsed = 0f;
            _isTravelling = false;
        }

        /// <summary>
        /// 이 VFX의 파티클이 하나라도 살아있는지 검사. 루트 PS(_ps) 또는 형제 시스템(_childSystems)
        /// 전체를 확인하여, 전부 소멸하면 false(= Pool 반환 시점)를 반환한다.
        /// </summary>
        private bool AnyAlive()
        {
            if (_ps != null) return _ps.IsAlive(true);

            if (_childSystems != null)
            {
                for (int i = 0; i < _childSystems.Length; i++)
                {
                    if (_childSystems[i] != null && _childSystems[i].IsAlive(true))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 비활성화 후 Pool에 반환.
        /// 중복 호출을 _active 플래그로 차단한다.
        /// </summary>
        public void Return()
        {
            if (!_active) return;
            _active = false;

            ResetTravelState();
            transform.localScale = _originalLocalScale;

            gameObject.SetActive(false);
            _onReturn?.Invoke(this);
        }
    }
}
