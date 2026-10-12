using System;
using Unity.Netcode;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>One recipient current value, not a begin-event RPC or a client-side timer.</summary>
    public struct NetworkBloomHealEffectState : INetworkSerializable, IEquatable<NetworkBloomHealEffectState>
    {
        public BloomHealEffectPresentation Value;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Value.MatchGeneration);
            serializer.SerializeValue(ref Value.TargetLifetime);
            serializer.SerializeValue(ref Value.EffectInstanceId);
            serializer.SerializeValue(ref Value.Revision);
            serializer.SerializeValue(ref Value.TargetId);
            serializer.SerializeValue(ref Value.BloomHealKind);
            serializer.SerializeValue(ref Value.Active);
            serializer.SerializeValue(ref Value.StopReason);
        }
        public bool Equals(NetworkBloomHealEffectState other) => Value.Equals(other.Value);
    }
}
