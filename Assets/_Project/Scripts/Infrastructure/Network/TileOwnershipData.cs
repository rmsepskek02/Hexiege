// ============================================================================
// TileOwnershipData.cs
// 타일 소유권 변경 정보를 네트워크로 전송하기 위한 직렬화 구조체.
//
// 역할:
//   - HexCoord(Q, R)와 TeamId를 int로 변환하여 네트워크 패킷에 직렬화
//   - NetworkTileSync의 ClientRpc 파라미터로 사용
//
// TeamId 매핑:
//   TeamId.Neutral = 0
//   TeamId.Blue    = 1
//   TeamId.Red     = 2
//   → (int)TeamId 직접 캐스팅 가능
//
// Infrastructure 레이어 — Unity.Netcode 의존 허용.
// ============================================================================

using Unity.Netcode;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// HexCoord + TeamId를 네트워크로 전송 가능한 구조체.
    /// INetworkSerializable 구현으로 NGO BufferSerializer를 통해 직렬화.
    /// </summary>
    public struct TileOwnershipData : INetworkSerializable
    {
        /// <summary> 타일 큐브 좌표 Q 축. </summary>
        public int Q;

        /// <summary> 타일 큐브 좌표 R 축. </summary>
        public int R;

        /// <summary>
        /// 소유 팀. TeamId를 int로 직렬화.
        /// Neutral=0, Blue=1, Red=2.
        /// </summary>
        public int TeamIndex;

        /// <summary>
        /// 편의 생성자. HexCoord와 TeamId를 받아 변환.
        /// </summary>
        public TileOwnershipData(HexCoord coord, TeamId team)
        {
            Q = coord.Q;
            R = coord.R;
            TeamIndex = (int)team;
        }

        /// <summary> 큐브 좌표로 복원. </summary>
        public HexCoord ToHexCoord() => new HexCoord(Q, R);

        /// <summary> TeamId로 복원. </summary>
        public TeamId ToTeamId() => (TeamId)TeamIndex;

        /// <summary>
        /// NGO 직렬화 구현.
        /// 송신 시: 버퍼에 값을 씀.
        /// 수신 시: 버퍼에서 값을 읽음.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Q);
            serializer.SerializeValue(ref R);
            serializer.SerializeValue(ref TeamIndex);
        }
    }
}
