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
