// ============================================================================
// BuildingView.cs
// 건물의 비주얼을 담당하는 Presentation 컴포넌트.
//
// 현재 범위 (건물 배치만):
//   - BuildingData 참조 유지
//   - 메시/렌더러는 프리팹에 미리 설정
//
// 향후 확장 (MVP):
//   - HP 바 표시
//   - 생산 진행률 표시
//   - 파괴 애니메이션
//
// 프리팹 구조 (3D 전환 후):
//   Building_{Type} (GameObject)
//     ├─ MeshFilter + MeshRenderer (3D 건물 메시)
//     ├─ Collider (3D 클릭 판정)
//     └─ BuildingView (이 스크립트)
//
// [Phase 2] 3D 전환: sortingOrder 제거 — Z-buffer로 렌더 순서 자동 처리.
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
