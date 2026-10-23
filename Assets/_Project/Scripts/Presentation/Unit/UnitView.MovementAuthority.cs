using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public partial class UnitView
    {
        /// <summary>
        /// Legacy가 이미 계산한 실제 목표만 복사한다. A2를 위해 새 경로나 좌표 변환을 만들지 않는다.
        /// </summary>
        private void CaptureUnitActionShadowDesiredTarget(Vector3 target)
        {
            if (!IsFiniteXZ(target))
            {
                ClearUnitActionShadowDesiredTarget();
                return;
            }

            _unitActionShadowDesiredTarget = target;
            _hasUnitActionShadowDesiredTarget = true;
        }

        private void ClearUnitActionShadowDesiredTarget()
        {
            _unitActionShadowDesiredTarget = default;
            _hasUnitActionShadowDesiredTarget = false;
            _movementIntentReason = UnitMovementIntentReason.None;
        }

        private static bool IsFiniteXZ(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void BeginMovementShadowCommand(UnitMovementIntentReason intentReason)
        {
            // Commit diagnostic revisions only when a Legacy writer is actually observed.
            // Empty or rejected commands therefore cannot create reducer-visible gaps.
            _movementPendingCommand = true;
            _movementPendingSegment = false;
            _movementIntentReason = intentReason;
        }

        private void BeginMovementShadowSegment(UnitMovementIntentReason intentReason)
        {
            if (_movementCommandRevision == 0UL || _movementPendingCommand)
            {
                _movementPendingCommand = true;
                _movementPendingSegment = false;
                _movementIntentReason = intentReason;
                return;
            }

            _movementPendingSegment = true;
            _movementIntentReason = intentReason;
        }

        private bool TryCommitMovementScope()
        {
            if (_movementPendingCommand)
            {
                if (_movementCommandRevision == ulong.MaxValue)
                    return false;

                _movementCommandRevision++;
                _movementSegmentRevision = 1UL;
                _movementPendingCommand = false;
                _movementPendingSegment = false;
                return true;
            }

            if (!_movementPendingSegment)
                return _movementCommandRevision != 0UL
                    && _movementSegmentRevision != 0UL;

            if (_movementSegmentRevision == ulong.MaxValue)
                return false;

            _movementSegmentRevision++;
            _movementPendingSegment = false;
            return true;
        }

        private bool TryPrepareMovementScope(
            out ulong preparedCommandRevision,
            out ulong preparedSegmentRevision)
            => UnitMovementScopePolicy.TryPrepare(
                _movementCommandRevision,
                _movementSegmentRevision,
                _movementPendingCommand,
                _movementPendingSegment,
                out preparedCommandRevision,
                out preparedSegmentRevision);

        private void CommitPreparedMovementScope(
            ulong preparedCommandRevision,
            ulong preparedSegmentRevision)
        {
            _movementCommandRevision = preparedCommandRevision;
            _movementSegmentRevision = preparedSegmentRevision;
            _movementPendingCommand = false;
            _movementPendingSegment = false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void BeginSoloRecoveryEvidence()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkContext.IsNetworkActive || _unitData == null) return;
            if (_soloRecoveryPending) _soloRecoveryOpen++;
            _soloRecoveryPending = true;
            _soloRecoveryEpisode++;
            _soloRecoveryLastReason = "none";
            SoloRecoveryEvidence("start", "combat-return");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void SoloRecoveryEvidence(string stage, string reason)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkContext.IsNetworkActive || _unitData == null || _soloRecoveryEpisode == 0) return;
            // 누적 판정은 출력 예산과 독립이다. 상세가 생략돼도 미종결을 성공 0으로 숨기지 않는다.
            if (stage == "blocked")
            {
                if (_soloRecoveryLastReason == reason) return;
                _soloRecoveryBlocked++;
            }
            if (stage == "resume-success")
            {
                if (!_soloRecoveryPending) return;
                _soloRecoveryPending = false;
                _soloRecoveryResumed++;
            }
            if (stage == "completed" && _soloRecoveryPending)
            {
                // 이미 목적 중심인 정상 완료는 '걸어서 재개'와 별도로 닫는다.
                _soloRecoveryPending = false;
                _soloRecoveryCompleted++;
            }
            _soloRecoveryLastReason = reason;
            if (stage != "end" && !UnitSoloPursuitStep.TakeEvidence(
                ref _soloRecoveryLines, ref _soloRecoveryOmitted, out bool firstOmission))
            {
                if (firstOmission)
                    GameLog.Dev.Warn("Combat", nameof(UnitView), "[UAS-SOLO-RETURN] 상세 상한",
                        $"Stage=cap, Unit={_unitData.Id}, Episode={_soloRecoveryEpisode}, Limit=64, Coverage=incomplete");
                return;
            }
            Vector3 domain = ViewConverter.FromView(transform.position);
            HexCoord root = HexMetrics.WorldToHex(domain);
            HexCoord logical = _unitData.Position;
            string data = $"Stage={stage}, Unit={_unitData.Id}, Type={(int)_unitData.Type}, Mode={_lockedMovementPipelineMode}, Episode={_soloRecoveryEpisode}, RootHex={root.Q};{root.R}, LogicalHex={logical.Q};{logical.R}, Distance={HexCoord.Distance(root, logical)}, Provider={_lastPathRequestResult.Status}, Reason={reason}, Started={_soloRecoveryEpisode}, Resumed={_soloRecoveryResumed}, Blocked={_soloRecoveryBlocked}, Open={_soloRecoveryOpen + (_soloRecoveryPending ? 1 : 0)}, Omitted={_soloRecoveryOmitted}, Coverage={(_soloRecoveryOmitted > 0 || _soloRecoveryFormatRejected > 0 ? "incomplete" : "bounded")}, Frame={Time.frameCount}";
            data += $", Completed={_soloRecoveryCompleted}, FormatRejected={_soloRecoveryFormatRejected}";
            if (!UnitSoloPursuitStep.FitsEvidence(data))
            {
                _soloRecoveryFormatRejected++;
                data = $"Stage={stage}, Unit={_unitData.Id}, Episode={_soloRecoveryEpisode}, Started={_soloRecoveryEpisode}, Resumed={_soloRecoveryResumed}, Blocked={_soloRecoveryBlocked}, Completed={_soloRecoveryCompleted}, Open={_soloRecoveryOpen + (_soloRecoveryPending ? 1 : 0)}, Omitted={_soloRecoveryOmitted}, FormatRejected={_soloRecoveryFormatRejected}, Coverage=incomplete";
            }
            GameLog.Dev.Info("Combat", nameof(UnitView), "[UAS-SOLO-RETURN] 싱글 이동 복귀", data);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void EndSoloRecoveryEvidence()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_soloRecoveryEnded) return;
            SoloRecoveryEvidence("end", "view-disable-or-reuse");
            _soloRecoveryEnded = true;
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void BeginSoloPursuitEvidence(int target, bool isUnit)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkContext.IsNetworkActive || _unitData == null) return;
            _soloPursuitEvidenceActive = true;
            _soloPursuitEpisode++;
            _soloPursuitTarget = target; _soloPursuitTargetIsUnit = isUnit;
            _soloPursuitIndex = -1; _soloPursuitHasRoute = false;
            _soloPursuitWaypoint = transform.position;
            _soloPursuitStalls = 0; _soloPursuitProgressTime = Time.timeAsDouble;
            SoloPursuitEvidence("start");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void SoloPursuitEvidence(string stage, bool reached = false, bool consume = false)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkContext.IsNetworkActive || _unitData == null || !_soloPursuitEvidenceActive) return;
            if (!UnitSoloPursuitStep.TakeEvidence(ref _soloPursuitLines, ref _soloPursuitOmitted,
                    out bool firstOmission))
            {
                if (firstOmission)
                    GameLog.Dev.Warn("Combat", nameof(UnitView), "[UAS-SOLO-PURSUIT] 상세 상한 도달",
                        $"Unit={_unitData.Id}, Episode={_soloPursuitEpisode}, Stage=cap, Limit=64, Omitted={_soloPursuitOmitted}, Coverage=incomplete, Frame={Time.frameCount}");
                return;
            }
            Vector3 root = transform.position;
            Vector3 domain = ViewConverter.FromView(root);
            HexCoord rootTile = HexMetrics.WorldToHex(domain);
            string data = System.FormattableString.Invariant($"Stage={stage}, Unit={_unitData.Id}, Type={(int)_unitData.Type}, Episode={_soloPursuitEpisode}, NetworkActive=false, Mode={_lockedMovementPipelineMode}, Target={_soloPursuitTarget}, TargetIsUnit={_soloPursuitTargetIsUnit}, Goal={_soloPursuitGoal.Q};{_soloPursuitGoal.R}, HasRoute={_soloPursuitHasRoute}, Index={_soloPursuitIndex}, Waypoint={_soloPursuitWaypoint.x:G9};{_soloPursuitWaypoint.z:G9}, Reached={reached}, Consume={consume}, Root={root.x:G9};{root.y:G9};{root.z:G9}, DomainXZ={domain.x:G9};{domain.z:G9}, UnitTile={_unitData.Position.Q};{_unitData.Position.R}, RootTile={rootTile.Q};{rootTile.R}, Revision={_soloPursuitRouteRevision}, LiveRevision={_navigationEnvironmentRevision}, Invalidated={_combatPursuitPathInvalidated}, ActionOwnsRoot={_rootRotationOwnership.ActionOwnsRoot}, Frame={Time.frameCount}, Detail={_soloPursuitLines}, Omitted={_soloPursuitOmitted}, Coverage=bounded");
            if (!UnitSoloPursuitStep.FitsEvidence(data))
            {
                if (_soloPursuitFormatRejected < int.MaxValue) _soloPursuitFormatRejected++;
                data = $"Stage={stage}, Unit={_unitData.Id}, Episode={_soloPursuitEpisode}, FormatRejected={_soloPursuitFormatRejected}, Coverage=incomplete, Frame={Time.frameCount}";
            }
            GameLog.Dev.Info("Combat", nameof(UnitView), "[UAS-SOLO-PURSUIT] 싱글 추격 경계",
                data);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveSoloPursuitStall()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!_soloPursuitEvidenceActive || _soloPursuitStalls >= 2
                || Time.timeAsDouble - _soloPursuitProgressTime < 2d) return;
            _soloPursuitStalls++;
            _soloPursuitProgressTime = Time.timeAsDouble;
            SoloPursuitEvidence("stall");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void CloseSoloPursuitEvidence(string reason)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            SoloPursuitEvidence(reason);
            _soloPursuitEvidenceActive = false;
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void EndSoloPursuitEvidence()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_soloPursuitEvidenceEnded || _soloPursuitEpisode == 0 || _unitData == null) return;
            CloseSoloPursuitEvidence("view-disable");
            _soloPursuitEvidenceEnded = true;
            // 별도 terminal 한 줄은 상세 예산 밖에 예약한다. 수명당 최대 64 detail + cap + end.
            GameLog.Dev.Info("Combat", nameof(UnitView), "[UAS-SOLO-PURSUIT] 관측 수명 종료",
                $"Stage=end, Unit={_unitData.Id}, Episodes={_soloPursuitEpisode}, Detail={_soloPursuitLines}, Omitted={_soloPursuitOmitted}, FormatRejected={_soloPursuitFormatRejected}, Limit=64, Coverage={(_soloPursuitOmitted > 0 || _soloPursuitFormatRejected > 0 ? "incomplete" : "bounded")}, Frame={Time.frameCount}");
#endif
        }

        /// <summary>
        /// A*·전투 복귀·추격이 공유하는 유일한 이동 pose writer seam이다.
        /// Legacy 경기에는 기존 후보 pose를 그대로 쓰고, ReducerAuthoritative 경기에는
        /// 서버 reducer가 허용한 frame만 위치를 전진시킨다. 클라이언트는 어떤 분기에서도
        /// Simulation Root를 쓰지 않는다.
        /// </summary>
        private MovementWriteOutcome ApplyMovementAuthorityFrame(
            Vector3 desiredTarget,
            Vector3 candidatePosition,
            Quaternion targetRotation,
            UnitMovementIntentReason fallbackIntentReason,
            bool targetAcquirePriority,
            Vector3? nextTrajectoryTarget = null,
            float maximumTravelDistanceWorld = -1f,
            IReadOnlyList<HexCoord> logicalCorridorPath = null,
            int logicalCorridorWaypointIndex = -1,
            Func<Vector3, bool> candidateAcquirePredicate = null,
            Func<bool> targetAcquirePresentationHandoff = null)
        {
            _lastMovementTrajectoryReachedWaypoint = false;
            _lastMovementCandidateAcquiredTarget = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired)
            {
                _postCombatTrace.HasSpatial = false;
                _postCombatTrace.Candidate = candidatePosition;
            }
#endif
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveClientWriteAttempt();
#endif
                return MovementWriteOutcome.Rejected;
            }

            // 건물 변경 이벤트가 랠리 경유 종료를 예약한 뒤에는 이전 경로로 한 프레임도
            // 더 쓰지 않는다. 기존 RepathRequired 탈출을 통해 소유 코루틴이 정리하며,
            // 여기서 콜백을 호출하거나 코루틴을 강제로 교체하지 않는다.
            if (_rallyPolicyCompletionPending
                || (_activeRallyPoint.HasValue && !IsInCombat()
                    && _pendingPath != null && _pendingPath.Count == 1))
                return MovementWriteOutcome.RepathRequired;

            // 공격/힐 행동이 회전을 소유하는 동안 이동 코루틴은 살아 있어도 pose를 쓰지 않는다.
            // Held는 진행량을 소비하지 않으므로 행동 종료 뒤 같은 navigation objective에서 재개된다.
            if (_rootRotationOwnership.ActionOwnsRoot)
                return MovementWriteOutcome.Held;

            bool reducerAuthoritative = _lockedMovementPipelineMode
                == UnitMovementPipelineMode.ReducerAuthoritative;
            if (!reducerAuthoritative)
            {
                Vector3 soloPositionBefore = transform.position;
                // SOLO도 Root 선분과 실제 공간 점유를 같은 경계에서 검증한다.
                // 중심 checkpoint와 별개이므로 추격 직전 A*의 반 타일 이동도 누락하지 않는다.
                int soloTransitionCount = 0;
                if (!NetworkContext.IsNetworkActive
                    && !TryStageSoloSpatialCommit(transform.position, candidatePosition,
                        logicalCorridorPath, logicalCorridorWaypointIndex,
                        out soloTransitionCount, out SpatialCandidateEvaluation soloSpatial))
                {
                    SoloRecoveryEvidence("blocked", "spatial-" + soloSpatial.Verdict);
                    return soloSpatial.Verdict == UnitSpatialCandidateVerdict.CandidateUnsafe
                        || soloSpatial.Verdict == UnitSpatialCandidateVerdict.RouteInvalidated
                        ? MovementWriteOutcome.RepathRequired : MovementWriteOutcome.Rejected;
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveLegacyWriterSelection();
#endif
                transform.position = candidatePosition;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeed * Time.deltaTime);
                if (!NetworkContext.IsNetworkActive && soloTransitionCount > 0)
                {
                    _movementUseCase.CommitPreflightedSpatialTransitionsPreservingFacing(
                        _unitData, _spatialTransitionBuffer, soloTransitionCount);
                    if (_isAStarMoving && logicalCorridorPath != null)
                    {
                        for (int index = 0; index < soloTransitionCount; index++)
                            GameEvents.OnUnitEnteredTile.OnNext(new UnitEnteredTileEvent(
                                _unitData.Id, _spatialTransitionBuffer[index]));
                    }
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (!NetworkContext.IsNetworkActive && _soloRecoveryPending && _isAStarMoving
                    && !_isInCombatPursuit && fallbackIntentReason != UnitMovementIntentReason.Chase
                    && CalculateMovementPositionDeltaXZ(soloPositionBefore, transform.position) > 0.000001f
                    && HexMetrics.WorldToHex(ViewConverter.FromView(transform.position)) == _unitData.Position)
                    SoloRecoveryEvidence("resume-success", "positive-navigation-progress");
#endif
                // 싱글 추격만 실제 writer 결과를 checkpoint 소비자에게 전달한다.
                // 네트워크 Legacy rollback과 Reducer의 판정/콜백은 변경하지 않는다.
                if (!NetworkContext.IsNetworkActive
                    && fallbackIntentReason == UnitMovementIntentReason.Chase
                    && !targetAcquirePriority)
                {
                    _lastMovementCandidateAcquiredTarget = candidateAcquirePredicate != null
                        && candidateAcquirePredicate(transform.position);
                    _lastMovementTrajectoryReachedWaypoint = !_lastMovementCandidateAcquiredTarget
                        && UnitSoloPursuitStep.Reached(transform.position.x, transform.position.z,
                            desiredTarget.x, desiredTarget.z, maximumTravelDistanceWorld);
                }
                return MovementWriteOutcome.Advanced;
            }

            if (_movementAuthorityReducer == null)
                _movementAuthorityReducer = new UnitMovementReducer();
            if (!IsFiniteXZ(transform.position)
                || !IsFiniteXZ(candidatePosition)
                || !IsFiniteXZ(desiredTarget)
                || !IsFiniteQuaternion(transform.rotation)
                || !IsFiniteQuaternion(targetRotation))
            {
                return MovementWriteOutcome.Rejected;
            }
            if (_movementIntentReason == UnitMovementIntentReason.None)
                BeginMovementShadowSegment(fallbackIntentReason);
            if (!TryPrepareMovementScope(
                    out ulong evaluatedCommandRevision,
                    out ulong evaluatedSegmentRevision))
                return MovementWriteOutcome.Rejected;

            Vector3 facing = transform.forward;
            Vector3 positionBefore = transform.position;
            Quaternion rotationBefore = transform.rotation;
            double observedServerTime = Time.timeAsDouble;
            UnitMovementSnapshot reducerSnapshotBefore =
                _movementAuthorityReducer.Snapshot;
            float expectedDelta = CalculateMovementPositionDeltaXZ(
                transform.position, candidatePosition);
            float planningDistance = maximumTravelDistanceWorld >= 0f
                ? maximumTravelDistanceWorld
                : expectedDelta;
            UnitTrajectoryStep trajectoryStep = default;
            bool hasTrajectoryStep = false;
            int spatialTransitionCount = 0;
            UnitMovementEvaluation evaluation;
            UnitMovementPreparedTransition preparedTransition = default;
            SpatialCandidateEvaluation selectedProbeEvaluation = default;
            bool hasSelectedProbeEvaluation = false;

            // AcquireTarget와 실제 endpoint는 기존 NoIntent adapter를 그대로 사용한다.
            // 정상 이동 의도만 pure trajectory planner를 통과시켜, reducer가 raw waypoint
            // 방향 점프가 아니라 회전 속도 안에서 만들어진 실제 이동 접선을 판정하게 한다.
            float desiredDistance = CalculateMovementPositionDeltaXZ(
                transform.position, desiredTarget);
            bool endpoint = desiredDistance
                    <= (float)UnitMovementEvaluationAdapter.PositionTolerance
                && planningDistance
                    <= (float)UnitMovementEvaluationAdapter.PositionTolerance;
            if (targetAcquirePriority || endpoint)
            {
                // 이미 현재 Root에서 획득한 타겟은 이동하지 않고 Action으로 넘긴다.
                // pure planner에서 후보 이동으로 처음 획득하는 별도 분기는 그대로 유지한다.
                if (targetAcquirePriority) candidatePosition = positionBefore;
                // endpoint/우선 타겟 획득도 adapter가 candidate pose를 커밋할 수 있으므로
                // reducer 호출 전에 실제 후보 선분을 검증한다. 일반 NoIntent/Held는 아래에서
                // 위치를 적용하지 않으므로 미리 계산한 전이도 커밋되지 않는다.
                if (!TryStageSpatialCommit(
                        positionBefore,
                        candidatePosition,
                        logicalCorridorPath,
                        logicalCorridorWaypointIndex,
                        out spatialTransitionCount,
                        out SpatialCandidateEvaluation endpointSpatialEvaluation))
                {
                    return endpointSpatialEvaluation.Verdict
                            == UnitSpatialCandidateVerdict.RouteInvalidated
                        || endpointSpatialEvaluation.Verdict
                            == UnitSpatialCandidateVerdict.CandidateUnsafe
                        ? MovementWriteOutcome.RepathRequired
                        : MovementWriteOutcome.Rejected;
                }

                evaluation = UnitMovementEvaluationAdapter.Prepare(
                    _movementAuthorityReducer,
                    evaluatedCommandRevision,
                    evaluatedSegmentRevision,
                    _movementIntentReason,
                    transform.position.x,
                    transform.position.z,
                    facing.x,
                    facing.z,
                    desiredTarget.x,
                    desiredTarget.z,
                    targetAcquirePriority,
                    planningDistance,
                    observedServerTime,
                    out preparedTransition);
            }
            else
            {
                // C# definite-assignment 규칙은 && 뒤쪽 TryCreate가 실행되지 않을 수 있다고
                // 판단한다. planner 호출 전에 세 값을 default로 명시해, 실패 입력에서는
                // validPureInput=false로 닫히면서도 컴파일러가 모든 경로의 할당을 증명하게 한다.
                WorldPointXZ currentPoint = default;
                ActionDirectionXZ currentFacing = default;
                WorldPointXZ waypoint = default;
                bool validPureInput = WorldPointXZ.TryCreate(
                        transform.position.x,
                        transform.position.z,
                        out currentPoint)
                    && ActionDirectionXZ.TryCreate(
                        facing.x,
                        facing.z,
                        out currentFacing)
                    && WorldPointXZ.TryCreate(
                        desiredTarget.x,
                        desiredTarget.z,
                        out waypoint);
                bool hasNext = nextTrajectoryTarget.HasValue;
                WorldPointXZ nextWaypoint = default;
                if (validPureInput && hasNext)
                {
                    Vector3 next = nextTrajectoryTarget.Value;
                    validPureInput = IsFiniteXZ(next)
                        && WorldPointXZ.TryCreate(next.x, next.z, out nextWaypoint);
                }

                if (!validPureInput)
                    return MovementWriteOutcome.Rejected;

                if (logicalCorridorPath != null)
                {
                    _lastCorridorRepathEvidence = default;
                    bool selectedStartTileEgress = false;
                    UnitTrajectoryCorridorResolution corridorResolution;
                    UnitSpatialCandidateVerdict corridorFinalVerdict;
                    SpatialCandidateEvaluation corridorFinalEvaluation = default;
                    _corridorStepValidator ??= ValidateActiveCorridorStep;
                    _activeCorridorPath = logicalCorridorPath;
                    _activeCorridorWaypointIndex = logicalCorridorWaypointIndex;
                    _activeCorridorSelectedStartTileEgress = false;
                    _activeCorridorHasEvaluation = false;
                    try
                    {
                        corridorResolution = UnitTrajectoryCorridorResolver.Resolve(
                            currentPoint,
                            currentFacing,
                            waypoint,
                            hasNext,
                            nextWaypoint,
                            planningDistance,
                            _rotationSpeed * Time.deltaTime,
                            HexMetrics.TileHeight * 0.35d,
                            _corridorStepValidator,
                            out trajectoryStep,
                            out corridorFinalVerdict);
                        selectedStartTileEgress =
                            _activeCorridorSelectedStartTileEgress;
                        corridorFinalEvaluation =
                            _activeCorridorLastEvaluation;
                        if (!_activeCorridorHasEvaluation)
                        {
                            var invalidPreflight = new UnitSpatialPreflightResult(
                                UnitSpatialPreflightStatus.InvalidChain,
                                default,
                                -1);
                            corridorFinalEvaluation = new SpatialCandidateEvaluation(
                                UnitSpatialCandidateVerdict.Fatal,
                                UnitSpatialTransitionPlanStatus.InvalidInput,
                                invalidPreflight,
                                0,
                                _unitData != null ? _unitData.Position : default,
                                _unitData != null ? _unitData.Position : default,
                                false,
                                default);
                            corridorFinalVerdict = UnitSpatialCandidateVerdict.Fatal;
                        }
                    }
                    finally
                    {
                        _activeCorridorPath = null;
                        _activeCorridorWaypointIndex = -1;
                        _activeCorridorSelectedStartTileEgress = false;
                        _activeCorridorLastEvaluation = default;
                        _activeCorridorHasEvaluation = false;
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveCorridorResolution(
                        corridorResolution,
                        selectedStartTileEgress);
#endif
                    if (corridorResolution == UnitTrajectoryCorridorResolution.Invalid
                        || corridorFinalVerdict == UnitSpatialCandidateVerdict.Fatal)
                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        ObserveFinalSpatialCandidate(
                            corridorFinalEvaluation,
                            repathRequired: false);
#endif
                        return MovementWriteOutcome.Rejected;
                    }
                    if (corridorResolution == UnitTrajectoryCorridorResolution.RepathRequired)
                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        ObserveFinalSpatialCandidate(
                            corridorFinalEvaluation,
                            repathRequired: true);
#endif
                        return MovementWriteOutcome.RepathRequired;
                    }
                    selectedProbeEvaluation = corridorFinalEvaluation;
                    hasSelectedProbeEvaluation = true;
                }
                else if (!UnitServerTrajectoryPlanner.TryPlan(
                             currentPoint,
                             currentFacing,
                             waypoint,
                             hasNext,
                             nextWaypoint,
                             planningDistance,
                             _rotationSpeed * Time.deltaTime,
                             HexMetrics.TileHeight * 0.35d,
                             out trajectoryStep)
                         || !trajectoryStep.IsValid)
                {
                    return MovementWriteOutcome.Rejected;
                }

                Vector3 plannedCandidate = transform.position;
                plannedCandidate.x = (float)trajectoryStep.CandidatePosition.X;
                plannedCandidate.z = (float)trajectoryStep.CandidatePosition.Z;

                // planner가 실제 후보 pose를 확정한 직후, stateful reducer보다 먼저
                // Root 경계 전이 전체를 검증한다. 실패하면 scope/reducer/pose/domain 모두 그대로다.
                if (!TryStageSpatialCommit(
                        positionBefore,
                        plannedCandidate,
                        logicalCorridorPath,
                        logicalCorridorWaypointIndex,
                        out spatialTransitionCount,
                        out SpatialCandidateEvaluation stagedSpatialEvaluation))
                {
                    return stagedSpatialEvaluation.Verdict
                            == UnitSpatialCandidateVerdict.RouteInvalidated
                        || stagedSpatialEvaluation.Verdict
                            == UnitSpatialCandidateVerdict.CandidateUnsafe
                        ? MovementWriteOutcome.RepathRequired
                        : MovementWriteOutcome.Rejected;
                }
                if (hasSelectedProbeEvaluation
                    && (selectedProbeEvaluation.Verdict != stagedSpatialEvaluation.Verdict
                        || selectedProbeEvaluation.TransitionStatus
                            != stagedSpatialEvaluation.TransitionStatus
                        || selectedProbeEvaluation.TransitionCount
                            != stagedSpatialEvaluation.TransitionCount
                        || selectedProbeEvaluation.Preflight.Status
                            != stagedSpatialEvaluation.Preflight.Status
                        || selectedProbeEvaluation.Preflight.OffendingTile
                            != stagedSpatialEvaluation.Preflight.OffendingTile
                        || selectedProbeEvaluation.Preflight.OffendingTransitionIndex
                            != stagedSpatialEvaluation.Preflight.OffendingTransitionIndex
                        || selectedProbeEvaluation.RootBeforeHex
                            != stagedSpatialEvaluation.RootBeforeHex
                        || selectedProbeEvaluation.CandidateHex
                            != stagedSpatialEvaluation.CandidateHex))
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveSpatialStageDivergence(
                        _unitData != null ? _unitData.Id : -1,
                        selectedProbeEvaluation.Verdict,
                        stagedSpatialEvaluation.Verdict,
                        selectedProbeEvaluation.Preflight.Status,
                        stagedSpatialEvaluation.Preflight.Status);
#endif
                    throw new InvalidOperationException(
                        "Corridor probe and final spatial stage diverged.");
                }

                bool acquireFromCandidate = candidateAcquirePredicate != null
                    && candidateAcquirePredicate(plannedCandidate);

                hasTrajectoryStep = true;
                ActionDirectionXZ reducerDirection =
                    trajectoryStep.TrajectoryDirection;
                // 150도 이상 반전은 planner가 위치 진행을 0으로 막는다. reducer에도
                // raw 목표 방향을 전달해 AlignToMove 상태가 명시적으로 게시되게 한다.
                if (trajectoryStep.RequiresStationaryAlignment
                    && !ActionDirectionXZ.TryCreate(
                        desiredTarget.x - transform.position.x,
                        desiredTarget.z - transform.position.z,
                        out reducerDirection))
                {
                    return MovementWriteOutcome.Rejected;
                }

                UnitMovementReducerStatus status = _movementAuthorityReducer.Prepare(
                    _movementAuthorityReducer.Snapshot.Revision,
                    evaluatedCommandRevision,
                    evaluatedSegmentRevision,
                    _movementIntentReason,
                    true,
                    currentFacing,
                    reducerDirection,
                    acquireFromCandidate,
                    observedServerTime,
                    out preparedTransition);
                evaluation = new UnitMovementEvaluation(
                    status,
                    preparedTransition.Decision,
                    _movementIntentReason,
                    true,
                    currentFacing,
                    reducerDirection,
                    false,
                    true,
                    acquireFromCandidate);
            }

            bool accepted = evaluation.ReducerInvoked
                && (evaluation.Status == UnitMovementReducerStatus.Accepted
                    || evaluation.Status == UnitMovementReducerStatus.Duplicate)
                && evaluation.Decision.IsValid;
            if (!accepted)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveFrame(
                    _unitData != null ? _unitData.Id : -1,
                    _movementNetworkUnit != null ? _movementNetworkUnit.NetworkObjectId : 0UL,
                    _unitData != null ? _unitData.Type : default,
                    _unitData != null ? _unitData.Team : default,
                    _movementIntentReason,
                    evaluatedCommandRevision,
                    evaluatedSegmentRevision,
                    evaluation,
                    desiredTarget,
                    planningDistance,
                    observedServerTime,
                    reducerSnapshotBefore,
                    positionBefore,
                    rotationBefore,
                    transform.position,
                    transform.rotation,
                    authorityWriterSelected: true,
                    walkAnimationAdvancing: _animator != null
                        && _currentAnimStateHash == StateWalk
                        && _animator.speed > 0.0001f);
#endif
                return MovementWriteOutcome.Rejected;
            }

            if (!_movementAuthorityReducer.CanCommitPrepared(preparedTransition))
                return MovementWriteOutcome.Rejected;

            // 공격 범위에 처음 들어온 Chase 후보는 Root의 마지막 위치/NoIntent보다 먼저
            // provisional Start를 Host와 원격 Client 전달 큐에 확정해야 한다. callback이
            // 실패하면 아직 movement snapshot, reducer, Root 어느 것도 커밋하지 않았으므로
            // 현재 이동 pose를 보존한 채 다음 정상 frame에서 같은 후보를 다시 평가한다.
            bool requiresAttackEntryHandoff =
                UnitAttackEntryCommitOrderPolicy.RequiresPresentationHandoff(
                    fallbackIntentReason,
                    evaluation.CommitsAcquireCandidate);
            bool attackEntryHandoffCompleted = !requiresAttackEntryHandoff
                || (targetAcquirePresentationHandoff != null
                    && targetAcquirePresentationHandoff());
            if (!UnitAttackEntryCommitOrderPolicy.CanCommitRootStop(
                    requiresAttackEntryHandoff,
                    attackEntryHandoffCompleted))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveCombatPresentationHandoffFailure(
                    _unitData != null ? _unitData.Id : -1,
                    "client-visible-entry-order");
#endif
                return MovementWriteOutcome.DeferredAttackHandoff;
            }

            // planner/spatial/reducer 준비가 모두 성공해도 아직 reducer/scope/Root/Domain은
            // 변경되지 않았다. 준비 snapshot 게시가 성공해야만 로컬 권위 상태를 커밋한다.
            if (_movementNetworkUnit == null
                || !_movementNetworkUnit.PublishMovementSnapshot(
                    preparedTransition.PreparedSnapshot))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObservePublicationFailure(
                    _unitData != null ? _unitData.Id : -1);
#endif
                return MovementWriteOutcome.Rejected;
            }

            if (!_movementAuthorityReducer.TryCommitPrepared(preparedTransition))
            {
                throw new InvalidOperationException(
                    "Prepared movement transition became stale after publication.");
            }
            CommitPreparedMovementScope(
                evaluatedCommandRevision,
                evaluatedSegmentRevision);

            if (hasTrajectoryStep)
            {
                ActionDirectionXZ trajectoryDirection =
                    trajectoryStep.TrajectoryDirection;
                float trajectoryAngle = Mathf.Atan2(
                    (float)trajectoryDirection.X,
                    (float)trajectoryDirection.Z) * Mathf.Rad2Deg;
                // planner가 이미 270°/s 제한을 적용했으므로 여기서 다시 보간하면
                // 한 프레임의 위치 방향과 rotation이 달라진다. 같은 접선을 원자 적용한다.
                transform.rotation = Quaternion.Euler(0f, trajectoryAngle, 0f);
                SynchronizeDomainFacingFromSimulationForward();
            }
            else if (evaluation.Decision.Phase == UnitMovementPhase.AlignToMove
                || evaluation.Decision.Phase == UnitMovementPhase.Move)
            {
                ActionDirectionXZ desiredDirection =
                    evaluation.DesiredMoveDirection;
                float authorityAngle = Mathf.Atan2(
                    (float)desiredDirection.X,
                    (float)desiredDirection.Z) * Mathf.Rad2Deg;
                Quaternion authorityTargetRotation =
                    Quaternion.Euler(0f, authorityAngle, 0f);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    authorityTargetRotation,
                    _rotationSpeed * Time.deltaTime);
                SynchronizeDomainFacingFromSimulationForward();
            }

            MovementWriteOutcome outcome = MovementWriteOutcome.Held;
            if (evaluation.Decision.AllowsMovement
                || evaluation.CommitsAcquireCandidate)
            {
                if (hasTrajectoryStep)
                {
                    Vector3 plannedPosition = transform.position;
                    plannedPosition.x = (float)trajectoryStep.CandidatePosition.X;
                    plannedPosition.z = (float)trajectoryStep.CandidatePosition.Z;
                    transform.position = plannedPosition;
                    _lastMovementTrajectoryReachedWaypoint =
                        trajectoryStep.ReachedCurrentWaypoint;
                    _lastMovementCandidateAcquiredTarget =
                        evaluation.CommitsAcquireCandidate;
                }
                else
                {
                    transform.position = candidatePosition;
                }
                outcome = MovementWriteOutcome.Advanced;
            }

            if (outcome == MovementWriteOutcome.Advanced
                && spatialTransitionCount > 0)
            {
                // 같은 동기 호출 안에서 preflight한 버퍼를 커밋한다. 이 지점까지 await/yield가 없으므로
                // 정상 경로에서는 재검증이 실패하지 않는다. Facing은 새 API가 현재 값을 보존한다.
                HexCoord committedPositionBefore = _unitData != null
                    ? _unitData.Position
                    : default;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveSpatialCommitAttempt(
                    _unitData != null ? _unitData.Id : -1,
                    spatialTransitionCount,
                    committedPositionBefore);
#endif
                try
                {
                    _movementUseCase.CommitPreflightedSpatialTransitionsPreservingFacing(
                        _unitData,
                        _spatialTransitionBuffer,
                        spatialTransitionCount);
                }
                catch (Exception)
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveSpatialCommit(
                        _unitData != null ? _unitData.Id : -1,
                        spatialTransitionCount,
                        committedPositionBefore,
                        _unitData != null ? _unitData.Position : default,
                        succeeded: false);
#endif
                    throw;
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                UnitMovementAuthorityObserver.ObserveSpatialCommit(
                    _unitData != null ? _unitData.Id : -1,
                    spatialTransitionCount,
                    committedPositionBefore,
                    _unitData != null ? _unitData.Position : default,
                    succeeded: true);
#endif

                if (_isAStarMoving && logicalCorridorPath != null)
                {
                    for (int index = 0; index < spatialTransitionCount; index++)
                    {
                        GameEvents.OnUnitEnteredTile.OnNext(
                            new UnitEnteredTileEvent(
                                _unitData.Id,
                                _spatialTransitionBuffer[index]));
                    }
                }
            }

            // 권위 위치와 표현 상태를 같은 commit 경계에서 바꾼다. AlignToMove/NoIntent는
            // 위치가 멈추므로 Walk 시간도 정지하고, 실제 전진 frame에서만 다시 재생한다.
            SetMovementHeldAnimation(
                UnitMovementPresentationPolicy.ShouldHoldWalk(
                    evaluation.Decision.IsValid,
                    evaluation.Decision.AllowsMovement,
                    evaluation.CommitsAcquireCandidate,
                    evaluation.Decision.IsTargetAcquirePriority));

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            UnitMovementAuthorityObserver.ObserveFrame(
                _unitData != null ? _unitData.Id : -1,
                _movementNetworkUnit != null ? _movementNetworkUnit.NetworkObjectId : 0UL,
                _unitData != null ? _unitData.Type : default,
                _unitData != null ? _unitData.Team : default,
                _movementIntentReason,
                evaluatedCommandRevision,
                evaluatedSegmentRevision,
                evaluation,
                desiredTarget,
                planningDistance,
                observedServerTime,
                reducerSnapshotBefore,
                positionBefore,
                rotationBefore,
                transform.position,
                transform.rotation,
                authorityWriterSelected: true,
                walkAnimationAdvancing: _animator != null
                    && _currentAnimStateHash == StateWalk
                    && _animator.speed > 0.0001f);
#endif
            if (evaluation.Decision.Phase == UnitMovementPhase.NoIntent)
            {
                _movementIntentReason = UnitMovementIntentReason.None;
                _movementPendingCommand = false;
                _movementPendingSegment = false;
            }
            return outcome;
        }

        /// <summary>
        /// 서버 Root 후보 선분을 bounded 간격으로 표본화하고 실제 인접 타일 전이를 사전검증한다.
        /// non-walkable 경로 마지막 타일은 접촉 목표로만 인정하며 출력 전이에는 넣지 않는다.
        /// </summary>
        // 마지막 이동 불가 목적지는 실제 점유가 아니라 기존 terminal contact다.
        // 이미 진입한 접촉 타일 내부에서 다음 frame/endpoint를 처리할 때도 이 의미를 보존한다.
        // 중간 차단·비인접 Root·경로 밖 진입은 이 예외로 수용하지 않는다.
        private bool TryStageSoloSpatialCommit(Vector3 before, Vector3 candidate,
            IReadOnlyList<HexCoord> path, int waypoint,
            out int count, out SpatialCandidateEvaluation evaluation)
        {
            count = 0; evaluation = default;
            if (_unitData != null && _unitData.IsAlive && _movementUseCase != null
                && IsFiniteXZ(before) && IsFiniteXZ(candidate)
                && path != null && path.Count >= 2 && waypoint == path.Count - 1)
            {
                HexCoord terminal = path[path.Count - 1];
                if (!_movementUseCase.IsWalkable(terminal)
                    && _unitData.Position == path[path.Count - 2]
                    && HexCoord.Distance(_unitData.Position, terminal) == 1
                    && HexMetrics.WorldToHex(ViewConverter.FromView(before)) == terminal
                    && HexMetrics.WorldToHex(ViewConverter.FromView(candidate)) == terminal
                    && IsTrajectoryPointSweepInsidePath(before, candidate, path, waypoint,
                        out _, out _))
                    return true;
            }
            return TryStageSpatialCommit(before, candidate, path, waypoint, out count, out evaluation);
        }

        private bool TryStageSpatialCommit(
            Vector3 positionBefore,
            Vector3 authoritativeCandidate,
            IReadOnlyList<HexCoord> logicalPath,
            int logicalWaypointIndex,
            out int transitionCount,
            out SpatialCandidateEvaluation evaluation)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired)
                _postCombatTrace.Candidate = authoritativeCandidate;
#endif
            evaluation = EvaluateSpatialCandidate(
                positionBefore,
                authoritativeCandidate,
                logicalPath,
                logicalWaypointIndex,
                _spatialTileSamples,
                _spatialTransitionBuffer);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ObserveFinalSpatialCandidate(
                evaluation,
                evaluation.Verdict == UnitSpatialCandidateVerdict.RouteInvalidated
                    || evaluation.Verdict == UnitSpatialCandidateVerdict.CandidateUnsafe);
#endif
            transitionCount = evaluation.TransitionCount;
            if (evaluation.Verdict == UnitSpatialCandidateVerdict.Accepted)
                return true;

            if (evaluation.Verdict == UnitSpatialCandidateVerdict.CandidateUnsafe
                || evaluation.Verdict == UnitSpatialCandidateVerdict.RouteInvalidated)
            {
                _lastCorridorRepathEvidence = evaluation.Evidence;
            }
            transitionCount = 0;
            return false;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void ObserveFinalSpatialCandidate(
            SpatialCandidateEvaluation evaluation,
            bool repathRequired)
        {
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired)
            {
                _postCombatTrace.Spatial = evaluation;
                _postCombatTrace.SpatialFrame = Time.frameCount;
                _postCombatTrace.SpatialTime = Time.timeAsDouble;
                _postCombatTrace.HasSpatial = true;
            }
            if (repathRequired)
                _spatialRecoverablePending = true;
            else if (evaluation.Verdict == UnitSpatialCandidateVerdict.Accepted)
                _spatialRecoverablePending = false;
            UnitMovementAuthorityObserver.ObserveSpatialFinalCandidate(
                _unitData != null ? _unitData.Id : -1,
                evaluation.Verdict,
                evaluation.TransitionStatus,
                evaluation.Preflight,
                evaluation.TransitionCount,
                _unitData != null ? _unitData.Position : default,
                evaluation.RootBeforeHex,
                evaluation.CandidateHex,
                repathRequired);
        }
#endif

        /// <summary>
        /// 후보 pose 하나를 관측만 한다. reducer/scope/Root/UnitData/index/event/observer를
        /// 변경하지 않으며 caller-owned scratch buffer에만 샘플과 전이를 기록한다.
        /// </summary>
        private SpatialCandidateEvaluation EvaluateSpatialCandidate(
            Vector3 positionBefore,
            Vector3 authoritativeCandidate,
            IReadOnlyList<HexCoord> logicalPath,
            int logicalWaypointIndex,
            List<HexCoord> sampleBuffer,
            HexCoord[] transitionBuffer)
        {
            sampleBuffer.Clear();
            if (_movementUseCase == null
                || _unitData == null
                || !_unitData.IsAlive
                || !IsFiniteXZ(positionBefore)
                || !IsFiniteXZ(authoritativeCandidate))
            {
                var invalid = new UnitSpatialPreflightResult(
                    UnitSpatialPreflightStatus.InvalidUnitOrDependency,
                    default,
                    -1);
                return new SpatialCandidateEvaluation(
                    UnitSpatialCandidateVerdict.Fatal,
                    UnitSpatialTransitionPlanStatus.InvalidInput,
                    invalid,
                    0,
                    default,
                    default,
                    false,
                    default);
            }

            float distance = CalculateMovementPositionDeltaXZ(
                positionBefore, authoritativeCandidate);
            float sampleSpacing = Mathf.Max(
                HexMetrics.TileHeight * 0.1f, 0.01f);
            int segmentCount = Mathf.Max(
                1, Mathf.CeilToInt(distance / sampleSpacing));
            if (segmentCount + 1 > SpatialSampleCapacity)
            {
                var invalid = new UnitSpatialPreflightResult(
                    UnitSpatialPreflightStatus.InvalidChain,
                    default,
                    -1);
                return new SpatialCandidateEvaluation(
                    UnitSpatialCandidateVerdict.Fatal,
                    UnitSpatialTransitionPlanStatus.InsufficientCapacity,
                    invalid,
                    0,
                    HexMetrics.WorldToHex(ViewConverter.FromView(positionBefore)),
                    HexMetrics.WorldToHex(ViewConverter.FromView(authoritativeCandidate)),
                    false,
                    default);
            }

            for (int sampleIndex = 0; sampleIndex <= segmentCount; sampleIndex++)
            {
                float t = sampleIndex / (float)segmentCount;
                Vector3 viewPoint = Vector3.Lerp(
                    positionBefore, authoritativeCandidate, t);
                Vector3 domainPoint = ViewConverter.FromView(viewPoint);
                sampleBuffer.Add(HexMetrics.WorldToHex(domainPoint));
            }

            HexCoord rootBeforeHex = sampleBuffer[0];
            HexCoord candidateHex =
                sampleBuffer[sampleBuffer.Count - 1];

            bool usedStartTileEgress = false;
            CorridorRepathEvidence evidence = default;
            if (logicalPath != null
                && !IsTrajectoryPointSweepInsidePath(
                    positionBefore,
                    authoritativeCandidate,
                    logicalPath,
                    logicalWaypointIndex,
                    out usedStartTileEgress,
                    out evidence))
            {
                return new SpatialCandidateEvaluation(
                    UnitSpatialCandidateVerdict.CandidateUnsafe,
                    UnitSpatialTransitionPlanStatus.OutsideLogicalPath,
                    UnitSpatialPreflightResult.Success(),
                    0,
                    rootBeforeHex,
                    candidateHex,
                    usedStartTileEgress,
                    evidence);
            }

            bool hasTerminalContact = false;
            HexCoord terminalTile = default;
            if (logicalPath != null && logicalPath.Count > 0)
            {
                HexCoord lastSample =
                    sampleBuffer[sampleBuffer.Count - 1];
                HexCoord logicalFinal = logicalPath[logicalPath.Count - 1];
                if (lastSample == logicalFinal
                    && !_movementUseCase.IsWalkable(lastSample))
                {
                    hasTerminalContact = true;
                    terminalTile = lastSample;
                }
            }

            UnitSpatialTransitionPlanStatus status =
                UnitSpatialTransitionPolicy.TryBuild(
                    _unitData.Position,
                    sampleBuffer,
                    sampleBuffer.Count,
                    logicalPath,
                    hasTerminalContact,
                    terminalTile,
                    transitionBuffer,
                    out int transitionCount);
            UnitSpatialPreflightResult preflight =
                status == UnitSpatialTransitionPlanStatus.Ready
                    || status == UnitSpatialTransitionPlanStatus.NoTransition
                    || status == UnitSpatialTransitionPlanStatus.TerminalContact
                ? _movementUseCase.PreflightSpatialTransitions(
                    _unitData,
                    transitionBuffer,
                    transitionCount)
                : UnitSpatialPreflightResult.Success();
            UnitSpatialCandidateVerdict verdict =
                UnitSpatialCandidatePolicy.Classify(status, preflight);
            return new SpatialCandidateEvaluation(
                verdict,
                status,
                preflight,
                transitionCount,
                rootBeforeHex,
                candidateHex,
                usedStartTileEgress,
                evidence);
        }

        private static float CalculateMovementPositionDeltaXZ(
            Vector3 from,
            Vector3 to)
        {
            float deltaX = to.x - from.x;
            float deltaZ = to.z - from.z;
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        private UnitSpatialCandidateVerdict ValidateActiveCorridorStep(
            UnitTrajectoryStep step)
        {
            if (_activeCorridorPath == null
                || _activeCorridorWaypointIndex < 0)
            {
                return UnitSpatialCandidateVerdict.Fatal;
            }

            Vector3 candidate = transform.position;
            candidate.x = (float)step.CandidatePosition.X;
            candidate.z = (float)step.CandidatePosition.Z;
            SpatialCandidateEvaluation evaluation = EvaluateSpatialCandidate(
                transform.position,
                candidate,
                _activeCorridorPath,
                _activeCorridorWaypointIndex,
                _spatialProbeTileSamples,
                _spatialProbeTransitionBuffer);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired)
                _postCombatTrace.Candidate = candidate;
#endif
            _activeCorridorLastEvaluation = evaluation;
            _activeCorridorHasEvaluation = true;
            _activeCorridorSelectedStartTileEgress =
                evaluation.UsedStartTileEgress;
            return evaluation.Verdict;
        }

        /// <summary>
        /// reducer mode의 이산 Domain Facing을 이미 commit한 SimulationFacing에 가장 가까운
        /// 헥스 방향으로 갱신한다. waypoint 시작 시 raw 선분 방향을 먼저 쓰지 않기 때문에
        /// Domain 방향이 실제 서버 rotation보다 한 단계 앞서는 상태가 생기지 않는다.
        /// </summary>
        private void SynchronizeDomainFacingFromSimulationForward()
        {
            if (_unitData == null) return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.000001f) return;
            forward.Normalize();

            Vector3 centerDomain = HexMetrics.HexToWorld(_unitData.Position);
            Vector3 centerView = ViewConverter.ToView(centerDomain);
            float bestDot = float.NegativeInfinity;
            HexDirection bestDirection = _unitData.Facing;
            for (int index = 0; index < HexDirectionExtensions.Count; index++)
            {
                HexDirection domainDirection = (HexDirection)index;
                HexCoord neighbor = _unitData.Position + domainDirection.Offset();
                Vector3 neighborView = ViewConverter.ToView(
                    HexMetrics.HexToWorld(neighbor));
                Vector3 direction = neighborView - centerView;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.000001f) continue;

                float dot = Vector3.Dot(forward, direction.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    // 기존 A* writer와 같은 진영 변환 의미를 보존한다.
                    bestDirection = ViewConverter.FlipDirection(domainDirection);
                }
            }

            _unitData.Facing = bestDirection;
        }

        /// <summary>
        /// Collider 없이 Simulation Root point의 선분만 검사한다. 선분을 짧게 나눠
        /// 각 표본이 현재 A* path의 인접 타일 영역 중 하나에 속하는지 확인한다.
        /// 다른 유닛은 경로 차단 요소가 아니며, path 밖 타일과 새로 막힌 중간 타일만 거부한다.
        /// </summary>
        private bool IsTrajectoryPointSweepInsidePath(
            Vector3 fromView,
            Vector3 toView,
            IReadOnlyList<HexCoord> path,
            int waypointIndex,
            out bool usedStartTileEgress,
            out CorridorRepathEvidence evidence)
        {
            usedStartTileEgress = false;
            evidence = default;
            if (path == null
                || path.Count < 2
                || waypointIndex < 1
                || waypointIndex >= path.Count
                || !IsFiniteXZ(fromView)
                || !IsFiniteXZ(toView))
            {
                return false;
            }

            float distance = CalculateMovementPositionDeltaXZ(fromView, toView);
            float sampleSpacing = Mathf.Max(HexMetrics.TileHeight * 0.1f, 0.01f);
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / sampleSpacing));
            int firstAllowed = Mathf.Max(0, waypointIndex - 1);
            int lastAllowed = Mathf.Min(path.Count - 1, waypointIndex + 1);
            for (int sample = 0; sample <= sampleCount; sample++)
            {
                float t = sample / (float)sampleCount;
                Vector3 viewPoint = Vector3.Lerp(fromView, toView, t);
                Vector3 domainPoint = ViewConverter.FromView(viewPoint);
                HexCoord tile = HexMetrics.WorldToHex(domainPoint);

                int matchedPathIndex = -1;
                for (int index = firstAllowed; index <= lastAllowed; index++)
                {
                    if (path[index] == tile)
                    {
                        matchedPathIndex = index;
                        break;
                    }
                }
                bool isWalkable = _movementUseCase == null
                    || _movementUseCase.IsWalkable(tile);
                UnitTrajectoryCorridorSampleResult sampleResult =
                    UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        path.Count,
                        waypointIndex,
                        matchedPathIndex,
                        isWalkable);
                if (sampleResult == UnitTrajectoryCorridorSampleResult.OutsideCorridor
                    || sampleResult == UnitTrajectoryCorridorSampleResult.Blocked)
                {
                    Vector3 rootDomain = ViewConverter.FromView(fromView);
                    UnitPathSignature.TryCreate(
                        path, out UnitPathSignature sourcePathSignature);
                    evidence = new CorridorRepathEvidence
                    {
                        IsAvailable = true,
                        Frame = Time.frameCount,
                        RootView = fromView,
                        RootDomain = rootDomain,
                        RootHex = HexMetrics.WorldToHex(rootDomain),
                        UnitDataPosition = _unitData != null
                            ? _unitData.Position
                            : default,
                        PathCount = path.Count,
                        WaypointIndex = waypointIndex,
                        PathStart = path[0],
                        CurrentWaypoint = path[waypointIndex],
                        SourcePathSignature = sourcePathSignature,
                        SampleView = viewPoint,
                        SampleHex = tile,
                        SampleIndex = sample,
                        SampleCount = sampleCount,
                        MatchedPathIndex = matchedPathIndex,
                        FirstAllowedPathIndex = firstAllowed,
                        LastAllowedPathIndex = lastAllowed,
                        IsWalkable = isWalkable,
                        SampleResult = sampleResult
                    };
                    return false;
                }
                if (sampleResult == UnitTrajectoryCorridorSampleResult.StartTileEgress)
                    usedStartTileEgress = true;
            }

            return true;
        }

        /// <summary>
        /// 살아 있는 유닛이 이동/추격을 정상 종료할 때 마지막 권위 상태를 NoIntent로 닫는다.
        /// 종료 경계가 publication에 실패하면 이전 Move/Align 상태를 숨기지 않고 fail-closed한다.
        /// </summary>
        private bool TryCloseMovementAuthorityIntent(
            UnitMovementIntentReason fallbackIntentReason,
            string boundary)
        {
            if (_unitData == null || !_unitData.IsAlive)
                return true;

            if (_movementAuthorityReducer != null)
            {
                UnitMovementSnapshot current = _movementAuthorityReducer.Snapshot;
                if (current.HasAcceptedObservation
                    && current.Phase == UnitMovementPhase.NoIntent
                    && current.CommandRevision == _movementCommandRevision
                    && current.SegmentRevision == _movementSegmentRevision)
                {
                    _movementIntentReason = UnitMovementIntentReason.None;
                    _movementPendingCommand = false;
                    _movementPendingSegment = false;
                    return true;
                }
            }

            MovementWriteOutcome outcome = ApplyMovementAuthorityFrame(
                transform.position,
                transform.position,
                transform.rotation,
                fallbackIntentReason,
                targetAcquirePriority: false);
            if (outcome != MovementWriteOutcome.Rejected)
                return true;

            ObserveMovementAuthorityAdapterFailure("completion-no-intent-rejected",
                $"[UAS-MOVE] 이동 종료 NoIntent 게시 거부. " +
                $"unitId={_unitData.Id}, boundary={boundary}, " +
                $"commandRevision={_movementCommandRevision}, " +
                $"segmentRevision={_movementSegmentRevision}");
            return false;
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z)
                && !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void RetireMovementShadowUnit()
        {
            if (_unitData == null
                || _movementNetworkUnit == null
                || !_movementNetworkUnit.IsSpawned)
                return;

            UnitMovementShadowObserver.RetireUnit(
                _unitData.Id,
                _movementNetworkUnit.NetworkObjectId);
        }

        private static float CalculateMovementShadowPositionDeltaXZ(
            Vector3 from,
            Vector3 to)
            => CalculateMovementPositionDeltaXZ(from, to);

        private UnitMovementShadowObserver.LegacyFrameToken BeginMovementShadowLegacyFrame(
            Vector3 desiredTarget,
            UnitMovementIntentReason fallbackIntentReason,
            float expectedPositionDeltaWorld)
        {
            if (!NetworkContext.IsNetworkActive
                || !NetworkContext.IsNetworkServer
                || _unitData == null
                || !_unitData.IsAlive
                || _movementNetworkUnit == null
                || !_movementNetworkUnit.IsSpawned)
            {
                return default;
            }

            if (_movementIntentReason == UnitMovementIntentReason.None)
                BeginMovementShadowSegment(fallbackIntentReason);

            if (!TryCommitMovementScope())
                return default;
            return UnitMovementShadowObserver.BeginLegacyFrame(
                _unitData.Id,
                _movementNetworkUnit.NetworkObjectId,
                _unitData.Type,
                _unitData.Team,
                _movementCommandRevision,
                _movementSegmentRevision,
                _movementIntentReason,
                transform.position,
                transform.rotation,
                desiredTarget,
                targetAcquirePriority: false,
                expectedPositionDeltaWorld: expectedPositionDeltaWorld);
        }

        private void CompleteMovementShadowLegacyFrame(
            UnitMovementShadowObserver.LegacyFrameToken token)
        {
            UnitMovementShadowObserver.CompleteLegacyFrame(
                token,
                transform.position,
                transform.rotation);
        }
#endif

    }
}
