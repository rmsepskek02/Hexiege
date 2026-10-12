using System;
using Unity.Netcode;
using Hexiege.Application;
namespace Hexiege.Infrastructure
{
    public struct NetworkTimedDamageEffectState : INetworkSerializable, IEquatable<NetworkTimedDamageEffectState>
    {
        public TimedDamageEffectPresentation Value;
        public bool Equals(NetworkTimedDamageEffectState other) => Value.Equals(other.Value);
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Value.Match); s.SerializeValue(ref Value.Lifetime);
            s.SerializeValue(ref Value.Instance); s.SerializeValue(ref Value.Revision);
            s.SerializeValue(ref Value.TargetId); s.SerializeValue(ref Value.SourceId);
            s.SerializeValue(ref Value.AttackMatch); s.SerializeValue(ref Value.AttackLifetime); s.SerializeValue(ref Value.AttackSequence);
            s.SerializeValue(ref Value.Active);
            s.SerializeValue(ref Value.StartTime); s.SerializeValue(ref Value.EndTime);
            byte visual = (byte)Value.Visual; s.SerializeValue(ref visual); Value.Visual = (TimedDamageVisual)visual;
        }
    }
}
