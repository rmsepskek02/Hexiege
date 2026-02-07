// ============================================================================
// FacingDirection.cs
// 유닛의 이동 방향(HexDirection, 6가지)을
// 아트 방향(ArtDirection) + 좌우반전(flipX) 으로 매핑.
//
// Orientation에 따라 다른 매핑 테이블 사용:
//
// PointyTop (3아트방향: NE, E, SE):
//   스프라이트는 3방향(NE, E, SE)만 제작하고,
//   나머지 3방향(NW, W, SW)은 flipX=true로 좌우 반전하여 재사용.
//
//   이동 방향  →  아트 방향  |  flipX
//   ─────────────────────────────────
//   NE (↗)    →  NE         |  false
//   E  (→)    →  E          |  false
//   SE (↘)    →  SE         |  false
//   SW (↙)    →  SE         |  true
//   W  (←)    →  E          |  true
//   NW (↖)    →  NE         |  true
//
// FlatTop (4아트방향: N, NE, SE, S):
//   flat-top에서는 N/S 순수 상하 방향이 존재하므로 4방향 스프라이트 필요.
//   N↔N(대칭), NE↔NW(flipX), SE↔SW(flipX), S↔S(대칭).
//
//   HexDirection  큐브오프셋    시각방향  →  아트방향  |  flipX
//   ─────────────────────────────────────────────────────────
//   NE (=0)       (+1,-1)      NE       →  NE        |  false
//   E  (=1)       (+1, 0)      SE       →  SE        |  false
//   SE (=2)       ( 0,+1)      S        →  S         |  false
//   SW (=3)       (-1,+1)      SW       →  SE        |  true
//   W  (=4)       (-1, 0)      NW       →  NE        |  true
//   NW (=5)       ( 0,-1)      N        →  N         |  false
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// HexOrientationContext.Current로 현재 orientation 확인 (Domain 내부 참조).
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 스프라이트가 실제로 존재하는 아트 방향.
    /// PointyTop: NE, E, SE (3방향)
    /// FlatTop: N, NE, SE, S (4방향)
    /// UnitAnimationData ScriptableObject의 배열 인덱스로 사용.
    /// </summary>
    public enum ArtDirection
    {
        NE = 0,   // 오른쪽 위를 바라보는 스프라이트
        E = 1,    // 오른쪽을 바라보는 스프라이트 (pointy-top 전용)
        SE = 2,   // 오른쪽 아래를 바라보는 스프라이트
        N = 3,    // 위를 바라보는 스프라이트 (flat-top 전용)
        S = 4     // 아래를 바라보는 스프라이트 (flat-top 전용)
    }

    /// <summary>
    /// 아트 방향 + flipX 여부를 묶은 값 객체.
    /// Presentation 레이어에서 SpriteRenderer.flipX에 직접 적용.
    /// </summary>
    public readonly struct FacingInfo
    {
        /// <summary> 사용할 스프라이트 방향 </summary>
        public readonly ArtDirection Art;

        /// <summary> true면 스프라이트를 좌우 반전 (왼쪽 방향 표현) </summary>
        public readonly bool FlipX;

        public FacingInfo(ArtDirection art, bool flipX)
        {
            Art = art;
            FlipX = flipX;
        }
    }

    /// <summary>
    /// HexDirection(6방향) → FacingInfo(아트방향 + flipX) 변환 유틸리티.
    /// Orientation에 따라 PointyTop 또는 FlatTop 매핑 테이블 사용.
    /// </summary>
    public static class FacingDirection
    {
        // ====================================================================
        // PointyTop 매핑 (3아트방향: NE, E, SE)
        // ====================================================================

        // 인덱스가 HexDirection enum 값과 일치하는 매핑 테이블.
        private static readonly FacingInfo[] PointyTopMapping =
        {
            new FacingInfo(ArtDirection.NE, false), // [0] HexDirection.NE → NE 원본
            new FacingInfo(ArtDirection.E,  false), // [1] HexDirection.E  → E 원본
            new FacingInfo(ArtDirection.SE, false), // [2] HexDirection.SE → SE 원본
            new FacingInfo(ArtDirection.SE, true),  // [3] HexDirection.SW → SE 반전
            new FacingInfo(ArtDirection.E,  true),  // [4] HexDirection.W  → E 반전
            new FacingInfo(ArtDirection.NE, true),  // [5] HexDirection.NW → NE 반전
        };

        // ====================================================================
        // FlatTop 매핑 (4아트방향: N, NE, SE, S)
        // ====================================================================

        // flat-top에서 HexDirection의 큐브 오프셋은 동일하지만 시각적 방향이 다름.
        // NE↔NW(flip), SE↔SW(flip), N=대칭, S=대칭.
        private static readonly FacingInfo[] FlatTopMapping =
        {
            new FacingInfo(ArtDirection.NE, false), // [0] NE(+1,-1) → flat: NE
            new FacingInfo(ArtDirection.SE, false), // [1] E (+1, 0) → flat: SE
            new FacingInfo(ArtDirection.S,  false), // [2] SE( 0,+1) → flat: S
            new FacingInfo(ArtDirection.SE, true),  // [3] SW(-1,+1) → flat: SW → SE 반전
            new FacingInfo(ArtDirection.NE, true),  // [4] W (-1, 0) → flat: NW → NE 반전
            new FacingInfo(ArtDirection.N,  false), // [5] NW( 0,-1) → flat: N
        };

        /// <summary>
        /// HexDirection → FacingInfo 변환.
        /// Orientation에 따라 PointyTop 또는 FlatTop 매핑 사용.
        /// </summary>
        public static FacingInfo FromHexDirection(HexDirection dir)
        {
            if (HexOrientationContext.Current == HexOrientation.FlatTop)
                return FlatTopMapping[(int)dir];
            return PointyTopMapping[(int)dir];
        }

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

        // ====================================================================
        // PointyTop 방향 추정
        // ====================================================================

        /// <summary>
        /// PointyTop: 큐브 좌표 차이(delta)로부터 가장 가까운 방향을 추정.
        /// Q와 R의 부호 조합으로 판단.
        ///
        /// Q>0, R<0 → 오른쪽 위 → NE
        /// Q>0, R=0 → 오른쪽   → E
        /// Q>0, R>0 → 오른쪽 아래 → SE
        /// Q<0, R>0 → 왼쪽 아래 → SW
        /// Q<0, R=0 → 왼쪽     → W
        /// Q<0, R<0 → 왼쪽 위  → NW
        /// Q=0, R>0 → 아래     → SE
        /// Q=0, R<0 → 위       → NW
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

        // ====================================================================
        // FlatTop 방향 추정
        // ====================================================================

        /// <summary>
        /// FlatTop: 큐브 좌표 차이(delta)로부터 가장 가까운 방향을 추정.
        /// flat-top에서 큐브 오프셋의 시각적 의미가 다르므로 별도 로직.
        ///
        /// 큐브 오프셋 → flat-top 시각방향 → HexDirection:
        /// R<0, Q=0 → 순수 위(N)     → NW
        /// R>0, Q=0 → 순수 아래(S)   → SE
        /// Q>0, R<0 → 오른쪽 위(NE)  → NE
        /// Q>0, R≥0 → 오른쪽 아래(SE)→ E
        /// Q<0, R>0 → 왼쪽 아래(SW)  → SW
        /// Q<0, R≤0 → 왼쪽 위(NW)   → W
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
