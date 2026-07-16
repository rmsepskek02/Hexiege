// ============================================================================
// SweepAttackArea.cs
// 도끼병(BattleAxe)류 "휩쓸기형 AoE"의 판정 타일 집합을 계산하는 순수 함수.
//
// 판정 범위(사용자 확정 2026-07-16):
//   공격자를 둘러싼 이웃 6타일 중 "등 뒤(Facing의 반대 방향)" 1타일을 제외한
//   전방 5타일 + 공격자 자기 타일 = 최대 6개 타일.
//
// 왜 자기 타일도 포함하는가:
//   유닛 겹침이 허용되는 구조라 근접 교전은 같은 타일에서 자주 발생한다.
//   바로 붙어 겹친 적이 안 맞으면 어색하므로 자기 타일도 판정에 포함한다(D-1).
//
// 왜 Domain 순수 함수로 분리하는가:
//   좌표 계산은 UnityEngine·Core 의존이 전혀 없는 순수 로직이므로
//   Domain에 두면 단위 테스트가 쉽고, 다른 특수 유닛(파도형 등)에서도 재사용 가능하다.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 휩쓸기형 AoE의 판정 타일 집합(전방 5타일 + 자기 타일)을 계산하는 정적 헬퍼.
    /// </summary>
    public static class SweepAttackArea
    {
        /// <summary>
        /// 공격자의 위치와 바라보는 방향을 기준으로 휩쓸기 판정 타일을 수집한다.
        ///
        /// 결과 = 공격자 자기 타일 1개 + (이웃 6방향 중 등 뒤 방향을 뺀) 전방 5타일.
        /// 등 뒤 방향은 <paramref name="facing"/>의 반대 방향(Opposite)이다.
        ///
        /// 매 공격마다 새 리스트를 할당하지 않도록, 호출자가 재사용 버퍼를 넘겨준다
        /// (모바일 GC 부담 최소화). 이 메서드가 버퍼를 Clear한 뒤 채운다.
        /// </summary>
        /// <param name="origin">공격자의 현재 타일 좌표.</param>
        /// <param name="facing">
        ///   공격자가 바라보는 방향. 도끼병의 경우 ExecuteAttack이 데미지 직전
        ///   타겟 방향으로 갱신한 값을 넘겨야 타겟이 항상 전방 5타일에 포함된다.
        /// </param>
        /// <param name="buffer">
        ///   결과를 담을 재사용 리스트. null이면 안 되며, 호출 시 내부에서 Clear된다.
        /// </param>
        public static void Collect(HexCoord origin, HexDirection facing, List<HexCoord> buffer)
        {
            if (buffer == null) return;

            buffer.Clear();

            // 1) 공격자 자기 타일 (겹친 적 포함 — D-1)
            buffer.Add(origin);

            // 2) 등 뒤(반대) 방향 = 판정에서 제외할 한 칸.
            HexDirection back = facing.Opposite();

            // 3) 6방향 이웃 중 등 뒤 방향만 빼고 전방 5타일을 추가.
            for (int i = 0; i < HexDirectionExtensions.Count; i++)
            {
                HexDirection dir = (HexDirection)i;
                if (dir == back) continue; // 등 뒤 타일 제외
                buffer.Add(dir.Neighbor(origin));
            }
        }
    }
}
