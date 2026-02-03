// ============================================================================
// FacingDirection.cs
// 유닛의 이동 방향(HexDirection, 6가지)을
// 아트 방향(ArtDirection, 3가지) + 좌우반전(flipX) 으로 매핑.
//
// 스프라이트는 3방향(NE, E, SE)만 제작하고,
// 나머지 3방향(NW, W, SW)은 flipX=true로 좌우 반전하여 재사용.
//
// 매핑 테이블:
//   이동 방향  →  아트 방향  |  flipX
//   ─────────────────────────────────
//   NE (↗)    →  NE         |  false   (원본 그대로)
//   E  (→)    →  E          |  false   (원본 그대로)
//   SE (↘)    →  SE         |  false   (원본 그대로)
//   SW (↙)    →  SE         |  true    (SE 스프라이트를 좌우 반전)
//   W  (←)    →  E          |  true    (E 스프라이트를 좌우 반전)
//   NW (↖)    →  NE         |  true    (NE 스프라이트를 좌우 반전)
//
// 사용 예시:
//   HexDirection moveDir = FacingDirection.FromCoords(from, to);
//   FacingInfo info = FacingDirection.FromHexDirection(moveDir);
//   spriteRenderer.flipX = info.FlipX;
//   animator.SetDirection(info.Art);
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 스프라이트가 실제로 존재하는 3가지 아트 방향.
    /// UnitAnimationData ScriptableObject의 배열 인덱스로 사용.
    /// </summary>
    public enum ArtDirection
    {
        NE = 0,   // 오른쪽 위를 바라보는 스프라이트
        E = 1,    // 오른쪽을 바라보는 스프라이트
        SE = 2    // 오른쪽 아래를 바라보는 스프라이트
    }

    /// <summary>
    /// 아트 방향 + flipX 여부를 묶은 값 객체.
    /// Presentation 레이어에서 SpriteRenderer.flipX에 직접 적용.
    /// </summary>
    public readonly struct FacingInfo
    {
        /// <summary> 사용할 스프라이트 방향 (NE, E, SE 중 하나) </summary>
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
    /// HexDirection(6방향) → FacingInfo(3아트방향 + flipX) 변환 유틸리티.
    /// </summary>
    public static class FacingDirection
    {
        // 인덱스가 HexDirection enum 값과 일치하는 매핑 테이블.
        private static readonly FacingInfo[] Mapping =
        {
            new FacingInfo(ArtDirection.NE, false), // [0] HexDirection.NE → NE 원본
            new FacingInfo(ArtDirection.E,  false), // [1] HexDirection.E  → E 원본
            new FacingInfo(ArtDirection.SE, false), // [2] HexDirection.SE → SE 원본
            new FacingInfo(ArtDirection.SE, true),  // [3] HexDirection.SW → SE 반전
            new FacingInfo(ArtDirection.E,  true),  // [4] HexDirection.W  → E 반전
            new FacingInfo(ArtDirection.NE, true),  // [5] HexDirection.NW → NE 반전
        };

        /// <summary>
        /// HexDirection → FacingInfo 변환.
        /// 예: FromHexDirection(HexDirection.SW) → { Art=SE, FlipX=true }
        /// </summary>
        public static FacingInfo FromHexDirection(HexDirection dir)
        {
            return Mapping[(int)dir];
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
            return EstimateDirection(delta);
        }

        /// <summary>
        /// 큐브 좌표 차이(delta)로부터 가장 가까운 방향을 추정.
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
        private static HexDirection EstimateDirection(HexCoord delta)
        {
            if (delta.Q > 0 && delta.R < 0) return HexDirection.NE;
            if (delta.Q > 0 && delta.R >= 0) return delta.R == 0 ? HexDirection.E : HexDirection.SE;
            if (delta.Q <= 0 && delta.R > 0) return delta.Q == 0 ? HexDirection.SE : HexDirection.SW;
            if (delta.Q < 0 && delta.R >= 0) return delta.R == 0 ? HexDirection.W : HexDirection.SW;
            if (delta.Q < 0 && delta.R < 0) return HexDirection.NW;
            if (delta.Q == 0 && delta.R < 0) return HexDirection.NW;
            return HexDirection.E; // 기본값
        }
    }
}
