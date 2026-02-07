// ============================================================================
// HexOrientation.cs
// 헥스 그리드의 타일 방향(orientation)을 정의하는 열거형.
//
// PointyTop (꼭지점이 12시):
//     /\          even-r offset: 홀수 행이 반칸 오른쪽
//    /  \         방향: NE, E, SE, SW, W, NW
//   |    |
//    \  /
//     \/
//
// FlatTop (변이 12시):
//    ____          even-q offset: 홀수 열이 반칸 아래
//   /    \         방향: N, NE, SE, S, SW, NW
//  /      \
//  \      /
//   \____/
//
// 용도:
//   - GameConfig에서 맵별 orientation 설정
//   - HexGrid, HexMetrics에서 좌표 변환 분기
//   - FacingDirection에서 아트 방향 매핑 분기
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public enum HexOrientation
    {
        PointyTop = 0,  // 꼭지점이 12시 (even-r offset)
        FlatTop = 1     // 변이 12시 (even-q offset)
    }

    /// <summary>
    /// 현재 활성 orientation을 Domain 레이어에서 접근할 수 있도록 하는 정적 홀더.
    /// GameBootstrapper가 시작 시 설정.
    /// Core 레이어(HexMetrics)와 동일한 값을 유지하며,
    /// Domain 레이어(FacingDirection 등)에서 Core에 의존하지 않고 orientation을 확인 가능.
    /// </summary>
    public static class HexOrientationContext
    {
        public static HexOrientation Current = HexOrientation.PointyTop;
    }
}
