using System;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>적 공격 회차와 분리된 서버 힐 표현의 불변 입력이다. HP 결과를 포함하지 않는다.</summary>
    public struct UnitHealCastPresentation
    {
        public ulong Lifetime, Sequence, Revision;
        public int UnitId, TargetId;
        public double StartedAt;
        public double EndedAt;
        public float AimX, AimZ, MarkerOffset, ClipDuration;
        public bool Active;
        // NetworkUnit의 기존 byte ordinals: Walk=1, Frozen=3, Held=4.
        public byte Priority;
        public byte EndReason;

        public bool IsValid => Lifetime != 0 && Sequence != 0 && Revision != 0
            && UnitId >= 0 && TargetId >= 0 && Finite(StartedAt) && Finite(EndedAt)
            && Finite(AimX) && Finite(AimZ) && Finite(MarkerOffset)
            && Finite(ClipDuration) && MarkerOffset >= 0 && ClipDuration > MarkerOffset
            && (Priority == 1 || Priority == 3 || Priority == 4);

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>한 객체 수명의 힐 회차 상한과 재생 성공을 별도로 추적한다.</summary>
    public sealed class UnitHealCastPresentationCursor
    {
        public UnitHealCastPresentation Current { get; private set; }
        public ulong PlayedSequence { get; private set; }
        /// <summary>미래 입력은 보관만 하고 서버 시작 시각 뒤에 재생한다.</summary>
        public bool IsDue(double now) => Current.Active && !_closed
            && !double.IsNaN(now) && !double.IsInfinity(now) && now >= Current.StartedAt;
        private bool _closed, _markerConsumed;

        /// <summary>중복·역순·다른 객체 수명을 거부한다. 준비 전 수신도 재생 성공으로 세지 않는다.</summary>
        public bool Accept(UnitHealCastPresentation state)
        {
            if (_closed || !state.IsValid) return false;
            if (Current.Lifetime != 0 && (state.Lifetime != Current.Lifetime
                || state.Sequence < Current.Sequence || state.Revision <= Current.Revision
                || (state.Sequence == Current.Sequence && !Current.Active && state.Active))) return false;
            if (state.Sequence != Current.Sequence) _markerConsumed = false;
            Current = state;
            return true;
        }

        /// <summary>실제 Animator 적용에 성공한 뒤에만 호출한다.</summary>
        public void MarkPlayed(double now)
        {
            PlayedSequence = Current.Sequence;
            if (now - Current.StartedAt >= Current.MarkerOffset) _markerConsumed = true;
        }

        /// <summary>loop와 중복 이벤트가 같은 힐 회차의 source 표현을 다시 방출하지 못하게 한다.</summary>
        public bool ConsumeMarker()
        {
            if (_closed || !Current.Active || Current.Priority != 1 || _markerConsumed
                || PlayedSequence != Current.Sequence) return false;
            _markerConsumed = true;
            return true;
        }

        /// <summary>사망·게임 종료·despawn 뒤 늦은 snapshot으로 부활하지 못하게 한다.</summary>
        public void Close() { _closed = true; _markerConsumed = true; }

        /// <summary>현재 시전의 clip 시간이다. 루프 나머지로 되감지 않는다.</summary>
        public float Offset(double now) => (float)Math.Max(0, Math.Min(Current.ClipDuration, now - Current.StartedAt));
    }
}
