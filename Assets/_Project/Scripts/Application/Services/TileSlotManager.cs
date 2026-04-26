// ============================================================================
// TileSlotManager.cs
// 같은 타일에 여러 유닛이 있을 때 시각적 위치를 분산시키는 슬롯 관리자.
//
// 사용 배경:
//   플로우 필드 도입 후 아군 유닛의 ClaimedTile 차단을 제거했기 때문에
//   여러 유닛이 같은 타일을 거쳐가는 상황이 자연스럽게 발생한다.
//   논리적으로는 같은 타일에 있어도 화면상 정확히 같은 위치에 겹쳐 보이면
//   "한 유닛만 있는 것처럼" 보이는 시각적 문제가 생긴다.
//
//   슬롯 매니저는 같은 타일에 여러 유닛이 있을 때 각 유닛에게 작은 위치
//   오프셋을 부여하여 시각적으로 분산시킨다. 도메인 좌표(HexCoord)와
//   전투 판정은 일절 변경하지 않고 시각 효과(transform.position)에만 영향.
//
// 슬롯 정의 (XZ 평면, 타일 중심 기준 비율값):
//   슬롯 0: (0,    0)     ← 중심 (첫 번째 유닛)
//   슬롯 1: (-0.3, 0)     ← 왼쪽
//   슬롯 2: (+0.3, 0)     ← 오른쪽
//   슬롯 3: (-0.15, +0.3) ← 앞-왼쪽 (Z+ 방향: "앞")
//   슬롯 4: (+0.15, +0.3) ← 앞-오른쪽
//   슬롯 5: (0,    -0.3)  ← 뒤쪽
//   (6번 이상: 슬롯 0으로 fallback — 시각적 겹침 허용)
//
// 단위:
//   비율값에 HexMetrics.TileWidth를 곱해 최종 월드 오프셋을 만든다.
//   타일 크기와 무관하게 동일한 시각적 비율을 유지하기 위함.
//
// 좌표축 매핑(중요):
//   3D 탑다운 뷰에서 맵은 XZ 평면에 펼쳐진다 (Y는 높이).
//   슬롯 정의의 두 번째 값은 Z축으로 매핑된다 (Y축은 항상 0).
//
// 멀티플레이 정책:
//   슬롯 할당은 시각 전용이므로 서버/클라이언트가 별도로 관리해도 무방하다.
//   단, NetworkTransform이 서버 위치를 보간 동기화하므로 클라이언트 슬롯 결과는
//   사실상 무시되고 서버의 슬롯 결과만 보인다 (싱글/Host 모드는 서버 그대로).
//
// Application 레이어 — UnityEngine.Vector3에 의존하나 MonoBehaviour는 아님.
// (선례: UnitCombatUseCase, IEntityPositionProvider도 UnityEngine 사용)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Application
{
    public class TileSlotManager
    {
        // 슬롯 정의 (XZ 평면 기준 비율값). 값은 HexMetrics.TileWidth로 스케일된다.
        // 인덱스 = 슬롯 번호. 길이 = 최대 분산 슬롯 수(여기서는 6).
        // (X 비율, Z 비율) 의미. Y는 항상 0 (높이 변경 없음).
        private static readonly Vector2[] SlotOffsetRatios = new Vector2[]
        {
            new Vector2( 0.0f,   0.0f),   // 0: 중심
            new Vector2(-0.3f,   0.0f),   // 1: 왼쪽
            new Vector2( 0.3f,   0.0f),   // 2: 오른쪽
            new Vector2(-0.15f,  0.3f),   // 3: 앞-왼쪽
            new Vector2( 0.15f,  0.3f),   // 4: 앞-오른쪽
            new Vector2( 0.0f,  -0.3f)    // 5: 뒤쪽
        };

        // 슬롯 점유 상태.
        // 외부 키: 타일 좌표
        // 내부 키: unitId / 내부 값: 그 유닛이 점유한 슬롯 번호
        // 같은 타일에 여러 유닛이 들어올 때 빈 슬롯을 빠르게 찾기 위해 사용한다.
        private readonly Dictionary<HexCoord, Dictionary<int, int>> _occupied
            = new Dictionary<HexCoord, Dictionary<int, int>>();

        /// <summary>
        /// 타일에서 비어있는 슬롯을 찾아 unitId에 할당하고, 해당 슬롯의
        /// 월드 오프셋(Vector3, XZ 평면)을 반환한다.
        ///
        /// 동작:
        ///   1) 해당 타일의 점유 사전을 가져온다 (없으면 새로 만든다).
        ///   2) 같은 unitId가 이미 등록돼 있으면 기존 슬롯을 재사용 — 중복 점유 방지.
        ///   3) 슬롯 0~5 중 비어있는 첫 번째 번호를 새로 할당한다.
        ///   4) 모두 점유 중이면 슬롯 0으로 fallback (시각적 겹침 허용).
        ///
        /// 반환:
        ///   해당 슬롯의 월드 오프셋. (Y=0, X/Z만 값)
        ///   호출 측은 타일 중심 뷰 좌표에 이 값을 더해 최종 위치를 만든다.
        /// </summary>
        /// <param name="tile">슬롯을 요청할 타일 좌표 (도메인).</param>
        /// <param name="unitId">슬롯을 점유할 유닛의 Id.</param>
        public Vector3 ClaimSlot(HexCoord tile, int unitId)
        {
            if (!_occupied.TryGetValue(tile, out var slots))
            {
                slots = new Dictionary<int, int>();
                _occupied[tile] = slots;
            }

            // 같은 유닛이 같은 타일에 두 번 Claim하면 기존 슬롯을 재사용.
            // (Phase 0 → Phase 1 → Phase 2 전환 등에서 호출이 중복될 수 있음)
            if (slots.TryGetValue(unitId, out int existingSlot))
                return GetSlotOffset(existingSlot);

            // 슬롯 0~5 중 비어있는 첫 번호를 찾는다.
            int chosenSlot = 0;
            bool found = false;
            for (int i = 0; i < SlotOffsetRatios.Length; i++)
            {
                bool taken = false;
                foreach (var pair in slots)
                {
                    if (pair.Value == i) { taken = true; break; }
                }
                if (!taken)
                {
                    chosenSlot = i;
                    found = true;
                    break;
                }
            }

            // 6명 이상이 한 타일에 있으면 모두 슬롯 0으로 — 시각적 겹침 허용.
            // 매우 드문 케이스이며, 게임플레이에 영향 없음.
            if (!found) chosenSlot = 0;

            slots[unitId] = chosenSlot;
            return GetSlotOffset(chosenSlot);
        }

        /// <summary>
        /// 해당 유닛이 점유 중이던 슬롯을 해제. 다른 유닛이 그 슬롯을 새로 가져갈 수 있게 된다.
        ///
        /// 호출 시점:
        ///   유닛이 from 타일에서 to 타일로 이동을 완료한 직후 from에 대해 호출.
        ///   유닛이 사망할 때 현재 점유 타일에 대해 호출.
        ///
        /// 사전이 비어있는 경우 자동으로 항목을 제거하여 메모리 누수를 방지한다.
        /// </summary>
        /// <param name="tile">슬롯을 해제할 타일 좌표.</param>
        /// <param name="unitId">슬롯을 해제할 유닛의 Id.</param>
        public void ReleaseSlot(HexCoord tile, int unitId)
        {
            if (!_occupied.TryGetValue(tile, out var slots)) return;

            slots.Remove(unitId);

            // 슬롯이 모두 비었으면 사전 항목 자체를 제거한다 (메모리 정리).
            if (slots.Count == 0)
                _occupied.Remove(tile);
        }

        /// <summary>
        /// 슬롯 번호 → 월드 좌표 오프셋 변환. 외부에서 슬롯만 알고 있을 때 사용.
        ///
        /// 변환:
        ///   비율값 × HexMetrics.TileWidth → XZ 평면 오프셋 (Y=0).
        ///   타일 크기가 변해도 동일한 시각적 비율을 유지한다.
        ///
        /// 잘못된 슬롯 번호(0~5 범위 밖)는 Vector3.zero를 반환한다.
        /// </summary>
        /// <param name="slotIndex">조회할 슬롯 번호.</param>
        /// <returns>해당 슬롯의 월드 오프셋 (XZ 평면, Y=0).</returns>
        public Vector3 GetSlotOffset(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotOffsetRatios.Length)
                return Vector3.zero;

            Vector2 ratio = SlotOffsetRatios[slotIndex];
            float scale = HexMetrics.TileWidth;

            // 두 번째 비율값은 Z축으로 매핑 (3D 탑다운 뷰에서 맵은 XZ 평면).
            return new Vector3(ratio.x * scale, 0f, ratio.y * scale);
        }

        /// <summary>
        /// 모든 슬롯 점유 정보를 초기화. 맵 전환/재경기 시 호출.
        /// </summary>
        public void Clear()
        {
            _occupied.Clear();
        }
    }
}
