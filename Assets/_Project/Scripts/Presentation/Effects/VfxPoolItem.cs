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
        /// <summary> 이 오브젝트가 제어하는 파티클 시스템. Awake에서 캐시. </summary>
        private ParticleSystem _ps;

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

        /// <summary> EffectManager가 Pool 반환 시 키로 사용하는 원본 프리팹. </summary>
        public GameObject SourcePrefab => _sourcePrefab;

        /// <summary>
        /// 파티클 시스템 캐시.
        /// 루트에 없으면 자식에서 탐색 (프리팹 구조 유연성 확보).
        /// </summary>
        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            if (_ps == null)
                _ps = GetComponentInChildren<ParticleSystem>();
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
        public void Play(Vector3 pos, Quaternion rot)
        {
            transform.position = pos;
            transform.rotation = rot;

            gameObject.SetActive(true);
            _active = true;

            if (_ps != null)
            {
                // 이전 재생 잔여 파티클을 제거하고 처음부터 재생.
                _ps.Clear();
                _ps.Play();
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

            // _ps가 없으면(프리팹에 파티클이 없는 비정상 케이스) 즉시 반환하여 영구 점유 방지.
            if (_ps == null || !_ps.IsAlive(true))
            {
                Return();
            }
        }

        /// <summary>
        /// 비활성화 후 Pool에 반환.
        /// 중복 호출을 _active 플래그로 차단한다.
        /// </summary>
        public void Return()
        {
            if (!_active) return;
            _active = false;

            gameObject.SetActive(false);
            _onReturn?.Invoke(this);
        }
    }
}
