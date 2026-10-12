using System;
using Unity.Netcode;
using Hexiege.Application.Combat.Sequencing;

namespace Hexiege.Infrastructure
{
    /// <summary>힐 회차·시각·active·우선순위를 하나의 NGO 값으로 전송한다.</summary>
    public struct NetworkUnitHealCastState : INetworkSerializable, IEquatable<NetworkUnitHealCastState>
    {
        public UnitHealCastPresentation Value;

        /// <summary>scalar partial arrival로 다른 회차 필드가 혼합되지 않도록 전체를 직렬화한다.</summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Value.Lifetime);
            serializer.SerializeValue(ref Value.Sequence);
            serializer.SerializeValue(ref Value.Revision);
            serializer.SerializeValue(ref Value.UnitId);
            serializer.SerializeValue(ref Value.TargetId);
            serializer.SerializeValue(ref Value.StartedAt);
            serializer.SerializeValue(ref Value.EndedAt);
            serializer.SerializeValue(ref Value.EndReason);
            serializer.SerializeValue(ref Value.AimX);
            serializer.SerializeValue(ref Value.AimZ);
            serializer.SerializeValue(ref Value.MarkerOffset);
            serializer.SerializeValue(ref Value.ClipDuration);
            serializer.SerializeValue(ref Value.Active);
            serializer.SerializeValue(ref Value.Priority);
        }

        /// <summary>같은 값 게시로 불필요한 델타가 생기지 않게 한다.</summary>
        public bool Equals(NetworkUnitHealCastState other) => Value.Equals(other.Value);
    }
}
