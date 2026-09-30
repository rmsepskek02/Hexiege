namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>
    /// 서버 공격 writer가 쿨다운과 타격 예약을 시작해도 되는지를 순수 값으로 판정한다.
    /// 첫 표본 하나만으로는 유닛이 실제로 멈췄는지 증명할 수 없으므로 반드시 직전 표본이 필요하다.
    /// 이 클래스는 Unity Transform이나 네트워크 상태를 쓰지 않으며, 호출자가 같은 서버 Tick 경계에서
    /// Attack 표현과 Legacy 피해 예약을 함께 시작할 때 사용하는 fail-closed 계약이다.
    /// </summary>
    public static class UnitAttackStartGate
    {
        // UnitAttackShadowCoordinator가 정지 판정에 사용하는 값과 동일하다.
        // 제곱 거리이므로 실제 허용 이동량은 0.0001 world unit이다.
        private const double StationaryPositionEpsilonSquared = 0.00000001d;

        public static UnitAttackStartGateResult Evaluate(
            UnitActionPoseSample sample,
            bool targetAlive,
            bool targetValid,
            bool hasPreviousAttackerPosition,
            WorldPointXZ previousAttackerPosition)
        {
            if (!sample.IsValid)
                return UnitAttackStartGateResult.InvalidPose;

            if (!targetAlive || !targetValid)
                return UnitAttackStartGateResult.InvalidTarget;

            // 첫 표본은 위치 변화량을 비교할 기준이 없다. 다음 서버 표본까지 기다려야
            // 이동→공격 경계에서 마지막 이동 frame과 공격 frame이 겹치지 않는다.
            if (!hasPreviousAttackerPosition || !previousAttackerPosition.IsValid)
                return UnitAttackStartGateResult.AwaitingStationarySample;

            double deltaX = sample.AttackerPosition.X - previousAttackerPosition.X;
            double deltaZ = sample.AttackerPosition.Z - previousAttackerPosition.Z;
            double squaredMovement = deltaX * deltaX + deltaZ * deltaZ;
            if (squaredMovement > StationaryPositionEpsilonSquared)
                return UnitAttackStartGateResult.Moving;

            if (!UnitActionAngleHysteresis.AllowsAttackAlignment(
                    sample.FacingToAimYawDegrees,
                    isCurrentlyAligned: false))
            {
                return UnitAttackStartGateResult.Misaligned;
            }

            return UnitAttackStartGateResult.Ready;
        }
    }

    /// <summary>공격 시작이 보류되거나 허용된 정확한 이유다.</summary>
    public enum UnitAttackStartGateResult : byte
    {
        InvalidPose = 0,
        InvalidTarget = 1,
        AwaitingStationarySample = 2,
        Moving = 3,
        Misaligned = 4,
        Ready = 5
    }
}
