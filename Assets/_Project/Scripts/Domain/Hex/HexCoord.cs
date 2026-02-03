// ============================================================================
// HexCoord.cs
// 육각형 타일의 위치를 나타내는 큐브 좌표(Cube Coordinates) 값 객체.
//
// 큐브 좌표계란?
//   육각형 그리드에서 각 타일의 위치를 (Q, R, S) 세 축으로 표현.
//   항상 Q + R + S = 0 제약을 만족하므로, S는 저장하지 않고 -Q-R로 계산.
//
//       NW(0,-1)   NE(+1,-1)
//            \       /
//     W(-1,0) (0,0) E(+1,0)
//            /       \
//       SW(-1,+1)  SE(0,+1)
//
// Dictionary<HexCoord, HexTile>의 키로 사용되므로
// Equals, GetHashCode, IEquatable<T> 구현이 필수.
// (struct의 기본 Equals는 리플렉션 기반이라 느림)
//
// 사용 예시:
//   var a = new HexCoord(2, 3);
//   var b = new HexCoord(4, 1);
//   int dist = HexCoord.Distance(a, b);  // 두 타일 사이 거리
//   var c = a + b;                        // 좌표 덧셈
// ============================================================================

using System;

namespace Hexiege.Domain
{
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;          // 열 축 (좌→우 방향)
        public readonly int R;          // 행 축 (좌상→우하 방향)
        public int S => -Q - R;         // 세 번째 축 (Q+R+S=0 제약에 의해 자동 계산)

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        // ====================================================================
        // 거리 계산
        // 큐브 좌표에서 두 타일 사이의 거리 = 세 축 차이의 절대값 합 / 2
        // 인접 타일 = 1, 두 칸 떨어진 타일 = 2
        // ====================================================================
        public static int Distance(HexCoord a, HexCoord b)
        {
            return (Math.Abs(a.Q - b.Q) + Math.Abs(a.R - b.R) + Math.Abs(a.S - b.S)) / 2;
        }

        // ====================================================================
        // 연산자 오버로드
        // 좌표끼리 더하기/빼기 (방향 오프셋 적용 등에 사용)
        // ====================================================================
        public static HexCoord operator +(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.Q + b.Q, a.R + b.R);
        }

        public static HexCoord operator -(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.Q - b.Q, a.R - b.R);
        }

        public static bool operator ==(HexCoord a, HexCoord b)
        {
            return a.Q == b.Q && a.R == b.R;
        }

        public static bool operator !=(HexCoord a, HexCoord b)
        {
            return !(a == b);
        }

        // ====================================================================
        // IEquatable<HexCoord> 구현
        // Dictionary 키 조회 시 박싱 없이 빠르게 비교하기 위해 필요.
        // 박싱(Boxing): struct를 object로 변환하는 과정 → 힙 할당 발생 → 느림
        // IEquatable<T>를 구현하면 박싱 없이 직접 비교 가능.
        // ====================================================================
        public bool Equals(HexCoord other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoord other && Equals(other);
        }

        // GetHashCode: Dictionary가 내부적으로 버킷을 결정할 때 사용.
        // Q와 R을 조합하여 고유한 해시값 생성. 397은 소수로 해시 충돌을 줄임.
        public override int GetHashCode()
        {
            return Q * 397 ^ R;
        }

        public override string ToString()
        {
            return $"({Q}, {R})";
        }
    }
}
