using System;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>공격 회차나 진단 번호와 별개인 서버 독립 전달체 식별자다.</summary>
    public readonly struct WaveDeliveryKey : IEquatable<WaveDeliveryKey>
    {
        public readonly ulong Epoch;
        public readonly ulong Id;
        public bool IsValid => Epoch != 0 && Id != 0;
        public WaveDeliveryKey(ulong epoch, ulong id) { Epoch = epoch; Id = id; }
        public bool Equals(WaveDeliveryKey other) => Epoch == other.Epoch && Id == other.Id;
        public override bool Equals(object obj) => obj is WaveDeliveryKey other && Equals(other);
        public override int GetHashCode() => Epoch.GetHashCode() * 397 ^ Id.GetHashCode();
        public override string ToString() => $"{Epoch:x16}:{Id}";
    }

    /// <summary>실제 생성된 파도의 불변 기하와 시각이다. 피해 대상은 예측하지 않는다.</summary>
    public readonly struct WavePresentationSpawn
    {
        public readonly WaveDeliveryKey Key;
        public readonly int CasterId;
        public readonly Vector3 Origin, Forward;
        public readonly float Width, Length, Duration;
        public readonly double SpawnTime;
        public bool IsValid => Key.IsValid && CasterId >= 0 && Finite(Origin) && Finite(Forward)
            && Math.Abs(Forward.sqrMagnitude - 1f) < .001f && Math.Abs(Forward.y) < .001f
            && Width > 0 && Length > 0 && Duration > 0 && Finite(Width) && Finite(Length)
            && Finite(Duration) && !double.IsNaN(SpawnTime) && !double.IsInfinity(SpawnTime) && SpawnTime >= 0;
        public WavePresentationSpawn(WaveDeliveryKey key, int caster, Vector3 origin, Vector3 forward,
            float width, float length, float duration, double time)
        { Key = key; CasterId = caster; Origin = origin; Forward = forward; Width = width; Length = length; Duration = duration; SpawnTime = time; }
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }

    /// <summary>writer가 적용한 뒤 저장하는 self-contained 접촉 표현이다. HP를 다시 쓰지 않는다.</summary>
    public readonly struct WavePresentationContact
    {
        public readonly WaveDeliveryKey Key;
        public readonly int Ordinal, VictimId, ResultingHp, AppliedAmount;
        public readonly bool VictimIsUnit, Heal;
        public readonly TeamId Team;
        public readonly Vector3 Position;
        public readonly double ContactTime;
        public bool IsValid => Key.IsValid && Ordinal > 0 && Ordinal <= 256 && VictimId >= 0
            && ResultingHp >= 0 && AppliedAmount > 0 && (Team == TeamId.Blue || Team == TeamId.Red)
            && (!Heal || VictimIsUnit) && WavePresentationSpawn.Finite(Position)
            && !double.IsNaN(ContactTime) && !double.IsInfinity(ContactTime) && ContactTime >= 0;
        public WavePresentationContact(WaveDeliveryKey key, int ordinal, int victim, bool unit,
            bool heal, TeamId team, Vector3 position, int hp, int amount, double time)
        { Key = key; Ordinal = ordinal; VictimId = victim; VictimIsUnit = unit; Heal = heal;
            Team = team; Position = position; ResultingHp = hp; AppliedAmount = amount; ContactTime = time; }
    }
}
