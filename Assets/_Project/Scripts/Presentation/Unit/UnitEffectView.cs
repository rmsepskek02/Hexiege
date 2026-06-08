// [DEPRECATED 2026-06-08] EffectManager로 대체됨. 사용자 테스트 통과 후 파일 삭제.
// 기존 OnEntityAttacked 구독은 서버 전용이라 멀티플레이 클라이언트에서 VFX가 보이지 않는 버그가 있었음.
// 새 방식: UnitView.OnAttackHit() Animation Event → EffectManager.PlayUnitAttack()
//
// 아래 코드 전체는 비활성화(주석 처리)되었습니다.
// 프리팹에서 이 컴포넌트를 제거하는 작업과 파일 최종 삭제는 사용자 테스트 통과 후 수행합니다.

/*
using UnityEngine;
using UniRx;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    public class UnitEffectView : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject _muzzleFlashPrefab;
        [SerializeField] private GameObject _hitEffectPrefab;

        [Header("References")]
        [SerializeField] private Transform _muzzleTransform;

        private UnitView _unitView;

        private void Awake()
        {
            _unitView = GetComponent<UnitView>();
        }

        private void Start()
        {
            // Subscribe to Attack Event for Muzzle Flash
            GameEvents.OnEntityAttacked
                .Where(e => e.Attacker is UnitData unit && _unitView != null && _unitView.Data != null && unit.Id == _unitView.Data.Id)
                .Subscribe(e => PlayMuzzleFlash())
                .AddTo(this);

            // Subscribe to Damaged Event for Hit Effect
            GameEvents.OnEntityDamaged
                .Where(e => e.Entity is UnitData unit && _unitView != null && _unitView.Data != null && unit.Id == _unitView.Data.Id)
                .Subscribe(e => PlayHitEffect())
                .AddTo(this);
        }

        private void PlayMuzzleFlash()
        {
            if (_muzzleFlashPrefab == null) return;

            Transform spawnPoint = _muzzleTransform != null ? _muzzleTransform : transform;
            Instantiate(_muzzleFlashPrefab, spawnPoint.position, spawnPoint.rotation);
        }

        private void PlayHitEffect()
        {
            if (_hitEffectPrefab == null) return;

            // Spawn at unit's center
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            Instantiate(_hitEffectPrefab, spawnPos, Quaternion.identity);
        }
    }
}
*/
