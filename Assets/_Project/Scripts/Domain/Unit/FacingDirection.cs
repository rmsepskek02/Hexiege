// ============================================================================
// FacingDirection.cs
// 두 헥스 좌표 간 이동 방향(HexDirection)을 추정하는 유틸리티.
//
// FromCoords(from, to): 인접 타일이면 정확한 방향, 원거리면 추정 방향 반환.
// Orientation에 따라 PointyTop / FlatTop 추정 로직 분기.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// HexOrientationContext.Current로 현재 orientation 확인 (Domain 내부 참조).
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// HexDirection 방향 추정 유틸리티.
    /// 두 좌표 간 이동 방향을 계산한다.
    /// </summary>
    public static class FacingDirection
    {
        /// <summary>
        /// 두 좌표의 차이로부터 이동 방향(HexDirection)을 결정.
        /// 인접 타일이면 정확한 방향, 멀면 추정 방향 반환.
        /// UnitView에서 이전 타일 → 현재 타일 방향 계산에 사용.
        /// </summary>
        public static HexDirection FromCoords(HexCoord from, HexCoord to)
        {
            HexCoord delta = to - from;

            // 인접 타일이면 정확한 방향 매칭
            for (int i = 0; i < HexDirectionExtensions.Count; i++)
            {
                HexDirection dir = (HexDirection)i;
                if (dir.Offset() == delta)
                    return dir;
            }

            // 인접하지 않은 경우 (경로 건너뛰기 등) 대략적인 방향 추정
            if (HexOrientationContext.Current == HexOrientation.FlatTop)
                return EstimateFlatTopDirection(delta);
            return EstimatePointyTopDirection(delta);
        }

        /// <summary>
        /// PointyTop: 큐브 좌표 차이(delta)로부터 가장 가까운 방향을 추정.
        /// Q와 R의 부호 조합으로 판단.
        ///
        /// Q>0, R&lt;0 → NE / Q>0, R=0 → E / Q>0, R>0 → SE
        /// Q&lt;0, R>0 → SW / Q&lt;0, R=0 → W / Q&lt;0, R&lt;0 → NW
        /// Q=0, R>0 → SE / Q=0, R&lt;0 → NW
        /// </summary>
        private static HexDirection EstimatePointyTopDirection(HexCoord delta)
        {
            if (delta.Q > 0 && delta.R < 0) return HexDirection.NE;
            if (delta.Q > 0 && delta.R >= 0) return delta.R == 0 ? HexDirection.E : HexDirection.SE;
            if (delta.Q <= 0 && delta.R > 0) return delta.Q == 0 ? HexDirection.SE : HexDirection.SW;
            if (delta.Q < 0 && delta.R >= 0) return delta.R == 0 ? HexDirection.W : HexDirection.SW;
            if (delta.Q < 0 && delta.R < 0) return HexDirection.NW;
            if (delta.Q == 0 && delta.R < 0) return HexDirection.NW;
            return HexDirection.E; // 기본값
        }

        /// <summary>
        /// FlatTop: 큐브 좌표 차이(delta)로부터 가장 가까운 방향을 추정.
        /// flat-top에서 큐브 오프셋의 시각적 의미가 다르므로 별도 로직.
        ///
        /// R&lt;0, Q=0 → NW(N) / R>0, Q=0 → SE(S)
        /// Q>0, R&lt;0 → NE / Q>0, R>=0 → E
        /// Q&lt;0, R>0 → SW / Q&lt;0, R&lt;=0 → W
        /// </summary>
        private static HexDirection EstimateFlatTopDirection(HexCoord delta)
        {
            if (delta.R < 0 && delta.Q == 0) return HexDirection.NW;  // 순수 위 → N
            if (delta.R > 0 && delta.Q == 0) return HexDirection.SE;  // 순수 아래 → S
            if (delta.Q > 0 && delta.R < 0) return HexDirection.NE;   // 오른쪽 위
            if (delta.Q > 0 && delta.R >= 0) return HexDirection.E;   // 오른쪽 아래
            if (delta.Q < 0 && delta.R > 0) return HexDirection.SW;   // 왼쪽 아래
            if (delta.Q < 0 && delta.R <= 0) return HexDirection.W;   // 왼쪽 위
            return HexDirection.NE; // 기본값
        }
    }
}
