// ============================================================================
// BuildingView.cs
// 건물의 비주얼을 담당하는 Presentation 컴포넌트.
//
// 현재 범위 (건물 배치만):
//   - BuildingData 참조 유지
//   - 스프라이트는 프리팹에 미리 설정 (SpriteRenderer)
//
// 향후 확장 (MVP):
//   - HP 바 표시
//   - 생산 진행률 표시
//   - 파괴 애니메이션
//
// 프리팹 구조:
//   Building_{Type} (GameObject)
//     ├─ SpriteRenderer (bld_{type}.png, Order in Layer: 50)
//     └─ BuildingView (이 스크립트)
//
// sortingOrder = 50: 타일(0~30) 위, 유닛(100) 아래.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    public class BuildingView : MonoBehaviour
    {
        /// <summary> 이 건물의 데이터. Initialize()에서 설정. </summary>
        public BuildingData Data { get; private set; }

        /// <summary>
        /// BuildingFactory에서 프리팹 Instantiate 후 호출.
        /// 건물 데이터 참조를 설정하고 이벤트 구독.
        /// </summary>
        public void Initialize(BuildingData data)
        {
            Data = data;

            // 사망 이벤트 구독 — 이 건물이 파괴되면 GameObject 제거
            GameEvents.OnEntityDied
                .Subscribe(e =>
                {
                    if (Data != null && e.Entity == (IDamageable)Data)
                        Destroy(gameObject);
                })
                .AddTo(this);
        }
    }
}
