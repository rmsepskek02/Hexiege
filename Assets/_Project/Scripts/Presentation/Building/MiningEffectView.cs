using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    public class MiningEffectView : MonoBehaviour
    {
        [SerializeField] private GameObject _goldEffectPrefab;
        [SerializeField] private Vector3 _offset = new Vector3(0, 1.5f, 0);

        private BuildingView _buildingView;
        private GameObject _effectInstance;

        private void Awake()
        {
            _buildingView = GetComponent<BuildingView>();
        }

        private void Start()
        {
            if (_buildingView != null && _buildingView.Data != null)
            {
                if (_buildingView.Data.Type == BuildingType.MiningPost)
                {
                    SpawnEffect();
                }
            }
        }

        private void SpawnEffect()
        {
            if (_goldEffectPrefab == null) return;

            _effectInstance = Instantiate(_goldEffectPrefab, transform.position + _offset, Quaternion.identity, transform);
        }

        private void OnDestroy()
        {
            if (_effectInstance != null)
            {
                Destroy(_effectInstance);
            }
        }
    }
}
