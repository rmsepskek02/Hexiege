using System;

namespace Hexiege.Application.Combat.Sequencing
{
    /// <summary>
    /// 서버가 가진 Unity 오브젝트를 Application 계층에 노출하지 않고 현재 행동 pose만 읽는 좁은 포트다.
    /// 구현자는 위치나 회전을 쓰지 않으며, 요청한 타겟을 찾지 못하면 반드시 false를 반환한다.
    /// </summary>
    public interface IUnitActionPoseSource
    {
        bool TryCaptureUnitActionPose(EntityRef target, out UnitActionPoseSample sample);
    }

    /// <summary>
    /// 공격자 root와 타겟 root를 같은 순간에 읽은 순수 XZ 표본이다.
    /// 좌표는 원본 월드 좌표이며 ViewConverter나 네트워크 보간 값을 추가 적용하지 않는다.
    /// </summary>
    public readonly struct UnitActionPoseSample
    {
        public EntityRef Target { get; }
        public WorldPointXZ AttackerPosition { get; }
        public ActionDirectionXZ SimulationFacing { get; }
        public WorldPointXZ TargetPosition { get; }
        public ActionDirectionXZ TargetAimDirection { get; }
        public bool HasDesiredMoveDirection { get; }
        public ActionDirectionXZ DesiredMoveDirection { get; }
        public double TargetSquaredDistance { get; }
        public double FacingToAimYawDegrees { get; }
        public bool IsValid { get; }

        private UnitActionPoseSample(
            EntityRef target,
            WorldPointXZ attackerPosition,
            ActionDirectionXZ simulationFacing,
            WorldPointXZ targetPosition,
            ActionDirectionXZ targetAimDirection,
            bool hasDesiredMoveDirection,
            ActionDirectionXZ desiredMoveDirection,
            double targetSquaredDistance,
            double facingToAimYawDegrees)
        {
            Target = target;
            AttackerPosition = attackerPosition;
            SimulationFacing = simulationFacing;
            TargetPosition = targetPosition;
            TargetAimDirection = targetAimDirection;
            HasDesiredMoveDirection = hasDesiredMoveDirection;
            DesiredMoveDirection = desiredMoveDirection;
            TargetSquaredDistance = targetSquaredDistance;
            FacingToAimYawDegrees = facingToAimYawDegrees;
            IsValid = true;
        }

        /// <summary>
        /// raw root 좌표와 forward, Legacy가 실제로 선택한 목표 좌표를 검증하고 정규화한다.
        /// 타겟이 공격자와 같은 XZ이면 조준 방향을 정할 수 없으므로 fail-closed 한다.
        /// 이동 목표는 optional이며, 있다고 표시한 경우에만 같은 검증을 적용한다.
        /// </summary>
        public static bool TryCreate(
            EntityRef target,
            double attackerX,
            double attackerZ,
            double facingX,
            double facingZ,
            double targetX,
            double targetZ,
            bool hasDesiredMoveDirection,
            double desiredTargetX,
            double desiredTargetZ,
            out UnitActionPoseSample sample)
        {
            sample = default;
            if (!target.IsValid) return false;
            if (!WorldPointXZ.TryCreate(attackerX, attackerZ, out WorldPointXZ attackerPosition)
                || !WorldPointXZ.TryCreate(targetX, targetZ, out WorldPointXZ targetPosition))
                return false;
            if (!ActionDirectionXZ.TryCreate(facingX, facingZ, out ActionDirectionXZ facing))
                return false;

            double targetDeltaX = targetX - attackerX;
            double targetDeltaZ = targetZ - attackerZ;
            if (!ActionDirectionXZ.TryCreate(targetDeltaX, targetDeltaZ, out ActionDirectionXZ targetAim))
                return false;

            ActionDirectionXZ desired = default;
            if (hasDesiredMoveDirection)
            {
                double desiredDeltaX = desiredTargetX - attackerX;
                double desiredDeltaZ = desiredTargetZ - attackerZ;
                if (!ActionDirectionXZ.TryCreate(desiredDeltaX, desiredDeltaZ, out desired))
                    return false;
            }

            double squaredDistance = targetDeltaX * targetDeltaX + targetDeltaZ * targetDeltaZ;
            if (!IsFinite(squaredDistance)) return false;
            double yaw = GetYawDegrees(facing, targetAim);
            if (!IsFinite(yaw)) return false;

            sample = new UnitActionPoseSample(
                target, attackerPosition, facing, targetPosition, targetAim,
                hasDesiredMoveDirection, desired,
                squaredDistance, yaw);
            return true;
        }

        /// <summary>두 정규화 방향 사이의 최소 XZ 각도를 0~180도로 반환한다.</summary>
        public static double GetYawDegrees(ActionDirectionXZ from, ActionDirectionXZ to)
        {
            if (!from.IsValid || !to.IsValid) return double.NaN;
            double dot = from.X * to.X + from.Z * to.Z;
            if (!IsFinite(dot)) return double.NaN;
            dot = Math.Max(-1d, Math.Min(1d, dot));
            return Math.Acos(dot) * (180d / Math.PI);
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
