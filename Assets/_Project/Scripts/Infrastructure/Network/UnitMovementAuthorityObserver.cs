#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Core;
using Hexiege.Domain;
using Unity.Netcode;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// B3 production decision/write를 읽기만 하는 bounded observer다. 별도 reducer를
    /// 실행하지 않으며 transform, phase, 경로 또는 NetworkVariable을 쓰지 않는다.
    /// </summary>
    public static class UnitMovementAuthorityObserver
    {
        // v2: rejected/invalid frame의 독립 failure evidence와
        // root 정지 중 Walk 진행 위반을 손실 없이 관측한다.
        // v3: gameplay action ownership gate, retry-stable movement scope commit,
        // held Walk reassertion correction evidence.
        // v4: trajectory-corridor 후보 축소와 제자리 정렬 fallback 사용량을 관측한다.
        // v5: A* path[0] non-walkable start-tile egress frame을 별도로 관측한다.
        // v6: terminal corridor 실패에 Root/Domain/UnitData/path/sample 근거를 보존한다.
        // v7: 이동 중 path-start 보정이 논리 위치를 선행 커밋하지 않는 빌드를 식별한다.
        // v8: route checkpoint 소비와 실제 spatial Root 타일 커밋을 분리해 관측한다.
        // v9: 후보 probe는 기록하지 않고 최종 stage/recoverable repath/fatal만 분리한다.
        // v10: 중심 checkpoint 실제 도달 오차와 Chase direct-safe/path 선택을 bounded 집계한다.
        // v11: 서버 공격 진입의 Host 로컬 표현 handoff 실패를 독립 FAIL로 보존한다.
        // v12: Action 전 정상 후보 보류와 Action 후 실제 target 적용 실패를 분리한다.
        // v13: 순수 Client의 Root 정지 frame과 provisional Attack 표시 순서를 직접 관측한다.
        // v14: 1~3 frame 전송/보간 차이를 gameplay 정지 실패와 별도 집계한다.
        /// <summary>
        /// 런타임 증거와 Editor 교차 감사가 함께 사용하는 현재 production schema.
        /// 문자열을 복제하지 않아 오래된 양쪽 빌드가 서로 일치한다는 이유만으로 통과하지 않게 한다.
        /// </summary>
        public const string ProductionSchema = "b3-movement-authority-v15";
        private const int MaximumLines = 768;
        private const int ReservedTerminalLines = 32;
        private const int MaximumNormalLines = MaximumLines - ReservedTerminalLines;
        private const int MaximumDetails = 128;
        private const int MaximumFailureDetails = 64;
        private const int MaximumManifestChunks = 26;
        private const int PayloadLimit = 700;
        private const int NonManifestTerminalLines = 6;
        private const int MaximumFullLineUtf8BytesExclusive = 1000;
        private static readonly Dictionary<string, Coverage> Coverages =
            new Dictionary<string, Coverage>();
        private static readonly HashSet<string> PhaseTransitions =
            new HashSet<string>();
        private static readonly Dictionary<ulong, ServerPhaseSnapshot> LastPhases =
            new Dictionary<ulong, ServerPhaseSnapshot>();
        private static readonly Dictionary<ulong, ClientReplicatedSnapshot>
            ClientReplicatedSnapshots =
                new Dictionary<ulong, ClientReplicatedSnapshot>();
        private static readonly Dictionary<ulong, ClientAttackEntryPresentationOrderTracker>
            ClientAttackEntryTrackers =
                new Dictionary<ulong, ClientAttackEntryPresentationOrderTracker>();
        private static readonly UnitAttackEntryHandoffFailureEpisodes
            ActiveHandoffFailureEpisodes =
                new UnitAttackEntryHandoffFailureEpisodes();

        // 개발 전용: 실제 꺾임에 처음 참여한 타입별 유닛 순번 1/4/16/64를 고른다.
        // 경로·위치를 쓰지 않고, 이미 고른 유닛의 첫 꺾임과 제한된 경로 교체만 관측한다.
        internal const string DirectionTraceSchema = "movement-direction-v1";
        private sealed class DirectionTypeBudget
        {
            internal readonly HashSet<int> Participants = new HashSet<int>();
            internal int Changes;
            internal bool InitialHeading;
        }
        internal sealed class DirectionTraceState
        {
            internal int Unit, Type, LastIndex, FollowCount;
            internal ulong Command, Segment, Revision;
            internal IReadOnlyList<HexCoord> Path;
            internal HexCoord Goal;
            internal UnitMovementIntentReason Reason;
            internal long Sample;
            internal string Follow = "";
            internal int Phase;
            internal bool MovementObserved;
        }
        internal readonly struct DirectionTraceToken
        {
            internal readonly DirectionTraceState State;
            internal readonly bool First;
            internal readonly long Sample;
            internal readonly string Run;
            internal bool IsActive => State != null;
            internal DirectionTraceToken(DirectionTraceState state, bool first, long sample, string run)
            { State = state; First = first; Sample = sample; Run = run; }
        }
        private static readonly Dictionary<int, DirectionTypeBudget> DirectionTypes =
            new Dictionary<int, DirectionTypeBudget>();
        private static readonly Dictionary<int, DirectionTraceState> DirectionUnits =
            new Dictionary<int, DirectionTraceState>();
        // 일반 walkable A*의 최대 이탈 두 건만 값으로 보존한다. 경로 참조는 저장하지 않는다.
        private struct MotionPeak
        {
            internal bool Known, Curve, HasNext, Flip;
            internal int Frame, Unit, Type, Team, Index, Count;
            internal ulong Command, Segment, Revision;
            internal HexCoord Previous, Target, Next, Goal, PathStart, Spatial;
            internal UnitMovementIntentReason Intent;
            internal Vector3 Before, After, FromCenter, Desired;
            internal float YawBefore, YawAfter;
            internal double Cross;
            internal string Reason;
        }
        private static MotionPeak _curvePeak, _fallbackPeak;

        internal static void ObserveMotionPeak(UnitData unit, IReadOnlyList<HexCoord> path,
            int index, UnitMovementIntentReason reason, bool cellsWalkable, bool curve,
            string fallbackReason, ulong command, ulong segment, ulong revision,
            Vector3 before, Vector3 after, float yawBefore, float yawAfter,
            Vector3 fromCenter, Vector3 desired, bool flip)
        {
            if (!_active || !_isServer || _gameplayEnded || unit == null || !cellsWalkable
                || (reason != UnitMovementIntentReason.AStarPath && reason != UnitMovementIntentReason.PendingRepath)
                || path == null || index < 1 || index >= path.Count) return;
            double dx = desired.x - fromCenter.x, dz = desired.z - fromCenter.z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (!(length > 0d)) return;
            double cross = Math.Abs(dx * (after.z - fromCenter.z) - dz * (after.x - fromCenter.x)) / length;
            if (double.IsNaN(cross) || double.IsInfinity(cross)) return;
            MotionPeak old = curve ? _curvePeak : _fallbackPeak;
            if (old.Known && cross <= old.Cross) return;
            var value = new MotionPeak {
                Known = true, Curve = curve, Flip = flip, Frame = Time.frameCount, Unit = unit.Id,
                Type = (int)unit.Type, Team = (int)unit.Team, Index = index, Count = path.Count,
                Command = command, Segment = segment, Revision = revision,
                Previous = path[index - 1], Target = path[index], Goal = path[path.Count - 1],
                PathStart = path[0], Spatial = unit.Position, Intent = reason,
                HasNext = index + 1 < path.Count, Next = index + 1 < path.Count ? path[index + 1] : default,
                Before = before, After = after, FromCenter = fromCenter, Desired = desired,
                YawBefore = yawBefore, YawAfter = yawAfter, Cross = cross, Reason = fallbackReason
            };
            if (curve) _curvePeak = value; else _fallbackPeak = value;
        }

        private static void EmitMotionPeak(MotionPeak value)
        {
            if (!value.Known) return;
            Log(LogLevel.Info, "motion-peak-route", FormattableString.Invariant(
                $"schema=movement-direction-v1, scope=walkable-AStarPath-or-PendingRepath-END-only, role=ServerOrHost, flip={value.Flip}, curve={value.Curve}, fallbackReason={value.Reason}, f={value.Frame}, unit={value.Unit}, type={value.Type}, team={value.Team}, reason={value.Intent}, index={value.Index}, count={value.Count}, prev={value.Previous.Q}:{value.Previous.R}, target={value.Target.Q}:{value.Target.R}, hasNext={value.HasNext}, next={value.Next.Q}:{value.Next.R}, path0={value.PathStart.Q}:{value.PathStart.R}, goal={value.Goal.Q}:{value.Goal.R}, spatial={value.Spatial.Q}:{value.Spatial.R}, cmd={value.Command}, seg={value.Segment}, rev={value.Revision}"));
            Log(LogLevel.Info, "motion-peak-pose", FormattableString.Invariant(
                $"f={value.Frame}, unit={value.Unit}, curve={value.Curve}, before={value.Before.x:R}:{value.Before.z:R}, after={value.After.x:R}:{value.After.z:R}, delta={value.After.x-value.Before.x:R}:{value.After.z-value.Before.z:R}, yawBefore={value.YawBefore:R}, yawAfter={value.YawAfter:R}, fromCenter={value.FromCenter.x:R}:{value.FromCenter.z:R}, desired={value.Desired.x:R}:{value.Desired.z:R}, crossWorld={value.Cross:R}, metric=incoming-center-line-perpendicular, visual=unknown"));
        }

        private static long _directionSample;
        private static int _directionLines, _directionSkipped;
        private static bool _directionBudgetReported;

        internal static DirectionTraceToken BeginDirectionTrace(
            UnitData unit, IReadOnlyList<HexCoord> path, int index,
            ulong command, ulong segment, ulong revision, UnitMovementIntentReason reason,
            IReadOnlyList<HexCoord> pending, HexCoord destination, HexCoord? rally, int phase,
            Vector3 rootBefore, Vector3 rootForward, Vector3 desiredTarget)
        {
            if (!_active || !_isServer || _gameplayEnded || unit == null
                || path == null || index < 1 || index >= path.Count) return default;
            string runAtEntry = _runId;
            int type = (int)unit.Type;
            if (!DirectionTypes.TryGetValue(type, out DirectionTypeBudget budget))
            {
                if (DirectionTypes.Count >= 25) return default;
                budget = new DirectionTypeBudget();
                DirectionTypes.Add(type, budget);
            }
            bool outgoingCorner = index >= 2
                && (path[index-1].Q-path[index-2].Q != path[index].Q-path[index-1].Q
                    || path[index-1].R-path[index-2].R != path[index].R-path[index-1].R);
            bool first = false;
            string trigger = "follow";
            DirectionUnits.TryGetValue(unit.Id, out DirectionTraceState state);
            // 초기 정렬은 꺾임 표본과 독립 예산이다. 기존 15도 이동 안전 기준을 관측에만 재사용한다.
            Vector3 toTarget = desiredTarget-rootBefore;
            bool initialStart = index == 1 && command == 0UL;
            if (!budget.InitialHeading && index == 1
                && (initialStart || (ActionDirectionXZ.TryCreate(rootForward.x, rootForward.z, out var bodyDirection)
                && ActionDirectionXZ.TryCreate(toTarget.x, toTarget.z, out var targetDirection)
                && !UnitActionAngleHysteresis.AllowsMovement(
                    UnitActionPoseSample.GetYawDegrees(bodyDirection, targetDirection), true))))
            {
                if (state == null)
                {
                    state = new DirectionTraceState { Unit=unit.Id, Type=type };
                    DirectionUnits.Add(unit.Id, state);
                }
                budget.InitialHeading = true;
                first = true;
                trigger = initialStart ? "initial-start" : "initial-heading";
            }
            if (outgoingCorner && !budget.Participants.Contains(unit.Id)
                && budget.Participants.Count < 64)
            {
                budget.Participants.Add(unit.Id);
                int ordinal = budget.Participants.Count;
                if (ordinal == 1 || ordinal == 4 || ordinal == 16 || ordinal == 64)
                {
                    if (state == null)
                    {
                        state = new DirectionTraceState { Unit=unit.Id, Type=type };
                        DirectionUnits.Add(unit.Id, state);
                    }
                    first = true;
                    trigger = "first-actual-corner";
                }
                else
                {
                    _directionSkipped++;
                    if (ordinal == 2 || ordinal == 63) DirectionLog("direction-coverage",
                        $"schema={DirectionTraceSchema}, type={type}, participant={ordinal}, skipped={_directionSkipped}, reason=type-unit-ordinal-not-selected, selected=1/4/16/64");
                }
            }
            if (state == null) return default;
            if (!first && state.Path != null
                && (!ReferenceEquals(state.Path, path) || state.Command != command
                    || state.Segment != segment || state.Revision != revision
                    || state.Goal != path[path.Count-1] || state.Reason != reason
                    || state.Phase != phase))
            {
                if (budget.Changes < 2)
                {
                    budget.Changes++;
                    first = true;
                    trigger = "route-scope-phase-change";
                }
                else
                {
                    _directionSkipped++;
                    if (state.FollowCount > 0 && state.FollowCount < 3)
                        DirectionLog("direction-window", $"sample={state.Sample}, unit={state.Unit}, type={state.Type}, partial=true, reason=route-scope-phase-budget, actualMovementObserved={state.MovementObserved}, {state.Follow}");
                    state.FollowCount = 3;
                    state.Follow = "";
                }
            }
            if (first && state.FollowCount > 0 && state.FollowCount < 3)
                DirectionLog("direction-window", $"sample={state.Sample}, unit={state.Unit}, type={state.Type}, partial=true, actualMovementObserved={state.MovementObserved}, {state.Follow}");
            state.Path = path;
            state.Command = command;
            state.Segment = segment;
            state.Revision = revision;
            state.Goal = path[path.Count-1];
            state.Reason = reason;
            state.Phase = phase;
            state.LastIndex = index;
            long sample = state.Sample;
            if (first)
            {
                state.Sample = ++_directionSample;
                sample = state.Sample;
                state.FollowCount = 0;
                state.Follow = "";
                state.MovementObserved = false;
                string next = index+1 < path.Count ? TraceHex(path[index+1]) : "unknown";
                string older = index >= 2 ? TraceHex(path[index-2]) : "unknown";
                DirectionLog("direction-route", FormattableString.Invariant(
                    $"schema={DirectionTraceSchema}, policy=see-step, sample={state.Sample}, f={Time.frameCount}, unit={unit.Id}, type={type}, team={unit.Team}, role={(_networkManager != null && _networkManager.IsHost ? "host" : "server")}, flip={ViewConverter.IsFlipped}, trigger={trigger}, cmdBefore={command}, segBefore={segment}, rev={revision}, reason={reason}, phaseBefore={phase}, index={index}, count={path.Count}, path0={TraceHex(path[0])}, incoming={older}, prev={TraceHex(path[index-1])}, current={TraceHex(path[index])}, next={next}, goal={TraceHex(state.Goal)}, spatial={TraceHex(unit.Position)}, destination={TraceHex(destination)}, pendingKnown={pending != null}, pendingCount={pending?.Count ?? 0}, pendingGoal={(pending != null && pending.Count > 0 ? TraceHex(pending[pending.Count-1]) : "unknown")}, rally={(rally.HasValue ? TraceHex(rally.Value) : "unknown")}, source=authority-input, tileMeshFeet=unknown"));
            }
            return first || state.FollowCount < 3
                ? new DirectionTraceToken(state, first, sample, runAtEntry) : default;
        }

        internal static void EndDirectionTrace(DirectionTraceToken token,
            Vector3 before, float yawBefore, Vector3 desired, Vector3? nextDesired, Vector3 candidate,
            bool candidateKnown, Vector3 after, float yawAfter, Vector3 forward,
            Vector3 visual, bool visualKnown, bool staged, string verdict,
            bool committed, bool faulted, int phase, int walkable, int nextWalkable,
            HexCoord spatialAfter, bool spatialKnown, ulong commandAfter, ulong segmentAfter, float deltaTime, string trajectoryPolicy, string curveStatus)
        {
            if (!token.IsActive) return;
            if (token.Run != _runId) { _directionSkipped++; return; }
            DirectionTraceState state = token.State;
            if (state.Sample != token.Sample)
            {
                _directionSkipped++;
                DirectionLog("direction-coverage", $"schema={DirectionTraceSchema}, reason=nested-sample-replaced, unit={state.Unit}, oldSample={token.Sample}, liveSample={state.Sample}, skipped={_directionSkipped}");
                return;
            }
            double dx = after.x-before.x, dz = after.z-before.z;
            double tx = desired.x-before.x, tz = desired.z-before.z;
            double length = Math.Sqrt(dx*dx+dz*dz), targetLength = Math.Sqrt(tx*tx+tz*tz);
            bool angleKnown = length > 0.000001d && targetLength > 0.000001d;
            state.MovementObserved |= length > 0.000001d;
            double bodyAngle = targetLength > 0.000001d
                ? Math.Acos(Math.Max(-1d, Math.Min(1d,
                    (Math.Sin(yawBefore*Math.PI/180d)*tx+Math.Cos(yawBefore*Math.PI/180d)*tz)/targetLength)))*180d/Math.PI : 0d;
            double angle = angleKnown ? Math.Acos(Math.Max(-1d, Math.Min(1d,
                (dx*tx+dz*tz)/(length*targetLength))))*180d/Math.PI : 0d;
            string step = FormattableString.Invariant(
                $"f={Time.frameCount};dt={deltaTime:G9};b={TraceXZ(before)};a={TraceXZ(after)};c={(candidateKnown ? TraceXZ(candidate) : "unknown")};d={TraceXZ(desired)};yaw={yawBefore:G9}/{yawAfter:G9};fw={TraceXZ(forward)};delta={dx:G9}:{dz:G9};angle={(angleKnown ? angle.ToString("G9", CultureInfo.InvariantCulture) : "unknown")};bodyTargetAngle={(targetLength > 0.000001d ? bodyAngle.ToString("G9", CultureInfo.InvariantCulture) : "unknown")};v={(visualKnown ? TraceXZ(visual) : "unknown")};phase={phase};stage={(staged ? verdict : "unknown")};cmdAfter={commandAfter};segAfter={segmentAfter};scopeCommit={committed};throw={faulted};tile={(spatialKnown ? TraceHex(spatialAfter) : "unknown")};walk={(walkable < 0 ? "unknown" : walkable.ToString(CultureInfo.InvariantCulture))}/{(nextWalkable < 0 ? "unknown" : nextWalkable.ToString(CultureInfo.InvariantCulture))}");
            if (token.First)
                DirectionLog("direction-step", $"sample={state.Sample}, unit={state.Unit}, type={state.Type}, coordinate=authority-root, trajectoryPolicy={trajectoryPolicy}, curveStatus={curveStatus}, visual=readonly-observed-preLateUpdate, nextTarget={(nextDesired.HasValue ? TraceXZ(nextDesired.Value) : "unknown")}, {step}");
            else
            {
                // 후속 두 frame은 위치·목표·각도만 묶는다. route와 첫 표본의 나머지 입력은 sample로 연결한다.
                string follow = FormattableString.Invariant($"f={Time.frameCount};dt={deltaTime:G9};b={TraceXZ(before)};a={TraceXZ(after)};c={(candidateKnown ? TraceXZ(candidate) : "unknown")};d={TraceXZ(desired)};yaw={yawBefore:G9}/{yawAfter:G9};angle={(angleKnown ? angle.ToString("G9", CultureInfo.InvariantCulture) : "unknown")};stage={(staged ? verdict : "unknown")};throw={faulted}");
                state.Follow += (state.Follow.Length == 0 ? "" : " | ") + follow;
            }
            if (state.Sample != token.Sample) { _directionSkipped++; return; }
            state.FollowCount++;
            if (state.FollowCount == 3)
            {
                DirectionLog("direction-window", $"sample={state.Sample}, unit={state.Unit}, type={state.Type}, partial=false, actualMovementObserved={state.MovementObserved}, {state.Follow}");
                state.Follow = "";
            }
        }

        internal static void ObserveCenterCurveFrame(bool selected)
        {
            if (_active && _isServer && !_gameplayEnded && selected)
                IncrementBounded(ref _centerCurveFrames);
        }

        private static string TraceHex(HexCoord point) => $"{point.Q}:{point.R}";
        private static string TraceXZ(Vector3 point) => FormattableString.Invariant($"{point.x:G9}:{point.z:G9}");
        private static void DirectionLog(string name, string data)
        {
            // 본문 길이와 기존 전체 예산을 모두 지킨다. 예산 초과는 자료 없음이며 성공이 아니다.
            string full = $"[UAS-MOVE-AUTH] {name} | runId={_runId}, sharedSessionKey={_sessionKey}, startedAt={_startedAt:F6}, {data}";
            if (_lines >= MaximumNormalLines)
            { _directionSkipped++; _dropped++; return; }
            // 마지막 한 줄은 END 없이도 예산 소진을 식별할 수 있게 남겨 둔다.
            if (_directionLines >= 524)
            {
                _directionSkipped++;
                if (!_directionBudgetReported)
                {
                    _directionBudgetReported = true;
                    _directionLines++;
                    Log(LogLevel.Info, "direction-budget", $"schema={DirectionTraceSchema}, limit=525, emitted={_directionLines}, skipped={_directionSkipped}, reason=trace-line-cap, globalNormalLimit=736, source={name}");
                }
                return;
            }
            int bytes = Encoding.UTF8.GetByteCount(full);
            if (bytes >= MaximumFullLineUtf8BytesExclusive)
            {
                _directionSkipped++;
                _directionLines++;
                Log(LogLevel.Info, "direction-coverage", $"schema={DirectionTraceSchema}, reason=entry-utf8-limit, source={name}, requiredBytes={bytes}, skipped={_directionSkipped}");
                return;
            }
            _directionLines++;
            Log(LogLevel.Info, name, data);
        }

        private static NetworkManager _networkManager;
        private static bool _active;
        private static bool _isServer;
        private static string _runId;
        private static string _sessionKey;
        private static double _startedAt;
        private static int _lines;
        private static int _dropped;
        private static int _details;
        private static int _failureDetails;
        private static int _failureEvidenceOverflow;
        private static int _frames;
        private static int _centerCurveFrames;
        private static int _align;
        private static int _move;
        private static int _noIntent;
        private static int _endpoint;
        private static int _targetPriority;
        private static int _rejected;
        private static int _invalid;
        private static int _gateFailures;
        private static int _writerSelectionFailures;
        private static int _clientWriteAttempts;
        private static int _attackWriterOwnershipConflicts;
        private static int _ignoredServerTargetEvents;
        private static int _deferredCombatTargetChanges;
        private static int _combatPresentationHandoffFailures;
        private static int _heldFrames;
        private static int _stationaryWalkViolations;
        private static int _corridorFullSmooth;
        private static int _corridorReducedSmooth;
        private static int _corridorFullDirect;
        private static int _corridorReducedDirect;
        private static int _corridorStationaryAlignment;
        private static int _corridorRepathRequired;
        private static int _corridorStartTileEgressFrames;
        private static int _centerCheckpointsReached;
        private static double _maximumCenterCheckpointErrorWorld;
        private static int _chaseDirectSafeFrames;
        private static int _chasePathFrames;
        private static int _cornerSpeedFrames, _cornerStarts, _cornerCompleted;
        private static int _cornerNormalStops, _cornerSafetyStops, _cornerSummaryLines;
        private static double _cornerMinSpeed = double.PositiveInfinity;
        private static double _cornerMinRatio = 1d, _cornerMaxCross, _cornerMaxTurnExcess, _cornerSeconds;

        // Root 속도만 관측한다. Animator rate나 실제 게임 결과를 이 진단에서 변경하지 않는다.
        internal static void ObserveCornerSpeedFrame(int unitId, UnitType type,
            UnitCornerSpeedState previous, UnitCornerSpeedState current, UnitCornerSpeedProfile profile,
            Vector3 before, Vector3 after, double dt, double nominalTravel, double cap,
            double turnDegrees, double maximumTurnDegrees, bool outgoing, bool safetyStop,
            bool acquired, bool reachedWaypoint)
        {
            if (!_active || !_isServer || dt <= 0d || !profile.Valid) return;
            IncrementBounded(ref _cornerSpeedFrames);
            bool changed = previous.IsActive && current.IsActive
                && (previous.ObservedCorner.Corner.X != current.ObservedCorner.Corner.X
                    || previous.ObservedCorner.Corner.Z != current.ObservedCorner.Corner.Z);
            if (!previous.IsActive || changed) IncrementBounded(ref _cornerStarts);
            bool ended = previous.IsActive && (!current.IsActive || changed);
            bool complete = ended && previous.Outgoing.Valid && nominalTravel > 0d && !acquired;
            if (complete) IncrementBounded(ref _cornerCompleted);
            double dx = after.x - before.x;
            double dz = after.z - before.z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            if (nominalTravel > 0d && !double.IsInfinity(cap))
                _cornerMinRatio = Math.Min(_cornerMinRatio, cap / nominalTravel);
            if (!reachedWaypoint && distance > UnitMovementEvaluationAdapter.PositionTolerance)
                _cornerMinSpeed = Math.Min(_cornerMinSpeed, distance / dt);
            if (nominalTravel > 0d && !acquired && !reachedWaypoint
                && distance <= UnitMovementEvaluationAdapter.PositionTolerance)
            {
                if (safetyStop) IncrementBounded(ref _cornerSafetyStops);
                else IncrementBounded(ref _cornerNormalStops);
            }
            if (outgoing)
            {
                double vx = profile.Next.X - profile.Corner.X;
                double vz = profile.Next.Z - profile.Corner.Z;
                double length = Math.Sqrt(vx * vx + vz * vz);
                if (length > UnitMovementEvaluationAdapter.PositionTolerance)
                    _cornerMaxCross = Math.Max(_cornerMaxCross,
                        Math.Abs((after.x - profile.Corner.X) * vz - (after.z - profile.Corner.Z) * vx) / length);
            }
            _cornerMaxTurnExcess = Math.Max(_cornerMaxTurnExcess, turnDegrees - maximumTurnDegrees);
            _cornerSeconds += dt;
            if (ended && _cornerSummaryLines++ < MaximumDetails)
                Log(LogLevel.Info, "corner-end",
                    FormattableString.Invariant($"directionTraceSchema={DirectionTraceSchema}, f={Time.frameCount}, policy={UnitCornerSpeedPolicy.Version}, unitId={unitId}, type={type}, complete={complete}, cornerX={previous.ObservedCorner.Corner.X:G9}, cornerZ={previous.ObservedCorner.Corner.Z:G9}"));
        }
        private static int _spatialTransitionsPlanned;
        private static int _spatialTransitionsCommitted;
        private static int _spatialNoTransitionPlans;
        private static int _spatialTerminalContacts;
        private static int _spatialPreflightFailures;
        private static int _spatialCommitFailures;
        private static int _spatialRecoveries;
        private static int _spatialFinalRecoverableRepaths;
        private static int _spatialRepeatedRecoverableRepaths;
        private static int _spatialFatalPreflights;
        private static int _spatialStagesForCommit;
        private static int _spatialCleanupAfterRecoverable;
        private static int _spatialSameFrameRetries;
        private static int _spatialStageDivergences;
        private static readonly Dictionary<int, string> LastRecoverableSignatures =
            new Dictionary<int, string>();
        private static readonly Dictionary<int, int> LastRecoverableFrames =
            new Dictionary<int, int>();
        private static int _terminalLines;
        private static int _terminalOverflow;
        private static int _manifestPreflightFailures;
        private static int _terminalPreflightFailures;
        private static int _clientReplicatedSamples;
        private static int _clientReplicatedInvalid;
        private static int _clientReplicatedDuplicates;
        private static int _clientRevisionZero;
        private static int _clientRevisionRegressions;
        private static int _clientSameRevisionConflicts;
        private static int _clientInvalidPhase;
        private static int _clientPresentationFrames;
        private static int _clientAttackPresentationStarts;
        private static int _clientAttackEntryTransportGaps;
        private static int _clientAttackEntryOrderViolations;
        // 같은 이동 실패가 여러 frame/lifecycle callback에서 반복되어도 원인 Error는
        // 세션당 한 번만 남긴다. 뒤의 증거는 Warn, 최종 END는 별도 Error 한 번이다.
        private static bool _causalErrorEmitted;
        private static int _clientUnitUninitialized;
        private static int _clientInvalidScope;
        private static int _clientIdentityConflicts;
        private static int _clientScopeRegressions;
        private static int _lifecycleIdentityConflicts;
        private static int _adapterFailures;
        private static bool _gameplayEnded;
        private static int _postGameAdapterFailuresSuppressed;

        internal static void BeginSession(NetworkManager networkManager, bool isServer)
        {
            Reset();
            _networkManager = networkManager;
            _isServer = isServer;
            _active = networkManager != null && networkManager.IsListening;
            _runId = Guid.NewGuid().ToString("N");
            _sessionKey = CreateSharedSessionKey();
            _startedAt = ReadServerTime();
            if (_active)
            {
                Log(LogLevel.Info, "BEGIN",
                    $"role={(_isServer ? "host" : "client")}, observerSchema={ProductionSchema}, " +
                    $"mode=ReducerAuthoritative, serverTime={_startedAt:F6}, directionTraceSchema={DirectionTraceSchema}, directionSampling=type-first-corner-unit-1/4/16/64+2route-phase-changes+1initial-start-or-heading, directionLineCap=525, normalLineCap=736, followCalls=2, clientPath=unknown, curvePolicy={UnitCenterCurvePlan.Version}, cornerMetrics=fallback-only, initialStartOrHeading=independent-shared-budget, motionPeakScope=walkable-AStarPath-or-PendingRepath-END-only, motionPeakCases=curve1/fallback1, budgetOmission=coverage-or-global-drop");
            }
        }

        internal static void ObserveFrame(
            int unitId,
            ulong networkObjectId,
            UnitType unitType,
            TeamId team,
            UnitMovementIntentReason reason,
            ulong commandRevision,
            ulong segmentRevision,
            UnitMovementEvaluation evaluation,
            Vector3 desiredTarget,
            float expectedPositionDeltaWorld,
            double observedServerTime,
            UnitMovementSnapshot reducerSnapshotBefore,
            Vector3 positionBefore,
            Quaternion rotationBefore,
            Vector3 positionAfter,
            Quaternion rotationAfter,
            bool authorityWriterSelected,
            bool walkAnimationAdvancing)
        {
            if (!_active) return;
            if (!_isServer)
            {
                _clientWriteAttempts++;
                return;
            }

            _frames++;
            if (!authorityWriterSelected)
                _writerSelectionFailures++;

            bool accepted = evaluation.ReducerInvoked
                && (evaluation.Status == UnitMovementReducerStatus.Accepted
                    || evaluation.Status == UnitMovementReducerStatus.Duplicate)
                && evaluation.Decision.IsValid;
            if (!accepted)
            {
                _rejected++;
                if (!evaluation.ReducerInvoked
                    || evaluation.Status == UnitMovementReducerStatus.InvalidInput
                    || evaluation.Status == UnitMovementReducerStatus.InvalidTime)
                    _invalid++;
                LogFailure("movement-decision-rejected",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                    $"unitType={unitType}, team={team}, reason={reason}, " +
                    $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                    $"reducerInvoked={evaluation.ReducerInvoked}, reducerStatus={evaluation.Status}, " +
                    $"decisionValid={evaluation.Decision.IsValid}, phase={evaluation.Decision.Phase}, " +
                    $"evaluatedReason={evaluation.EvaluatedIntentReason}, " +
                    $"evaluatedHasIntent={evaluation.EvaluatedHasIntent}, " +
                    $"endpointNormalized={evaluation.EndpointNormalized}, " +
                    $"commitsAcquireCandidate={evaluation.CommitsAcquireCandidate}, " +
                    $"position=({positionBefore.x:F6},{positionBefore.z:F6}), " +
                    $"desiredTarget=({desiredTarget.x:F6},{desiredTarget.z:F6}), " +
                    $"expectedDelta={expectedPositionDeltaWorld:F6}, " +
                    $"observedServerTime={observedServerTime:F9}, " +
                    $"previousAcceptedTime={reducerSnapshotBefore.LastObservedServerTime:F9}, " +
                    $"hadAcceptedObservation={reducerSnapshotBefore.HasAcceptedObservation}, " +
                    $"previousCommandRevision={reducerSnapshotBefore.CommandRevision}, " +
                    $"previousSegmentRevision={reducerSnapshotBefore.SegmentRevision}, " +
                    $"facingValid={evaluation.SimulationFacing.IsValid}, " +
                    $"desiredDirectionValid={evaluation.DesiredMoveDirection.IsValid}");
                return;
            }

            UnitMovementPhase phase = evaluation.Decision.Phase;
            if (phase == UnitMovementPhase.AlignToMove) _align++;
            else if (phase == UnitMovementPhase.Move) _move++;
            else if (phase == UnitMovementPhase.NoIntent) _noIntent++;
            if (evaluation.EndpointNormalized) _endpoint++;
            if (evaluation.Decision.IsTargetAcquirePriority) _targetPriority++;

            float positionDelta = Vector2.Distance(
                new Vector2(positionBefore.x, positionBefore.z),
                new Vector2(positionAfter.x, positionAfter.z));
            bool shouldHoldWalk = UnitMovementPresentationPolicy.ShouldHoldWalk(
                evaluation.Decision.IsValid,
                evaluation.Decision.AllowsMovement,
                evaluation.CommitsAcquireCandidate,
                evaluation.Decision.IsTargetAcquirePriority);
            if (shouldHoldWalk) _heldFrames++;
            if (shouldHoldWalk && walkAnimationAdvancing)
            {
                _stationaryWalkViolations++;
                LogFailure("stationary-walk-animation",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                    $"unitType={unitType}, team={team}, reason={reason}, " +
                    $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                    $"phase={phase}, positionDelta={positionDelta:F6}");
            }
            if ((!evaluation.Decision.AllowsMovement
                    && !evaluation.CommitsAcquireCandidate
                    && positionDelta > 0.00001f)
                || (phase == UnitMovementPhase.Move
                    && evaluation.Decision.YawErrorDegrees > 15.000001d)
                || (phase == UnitMovementPhase.AlignToMove
                    && evaluation.Decision.YawErrorDegrees <= 10d))
            {
                _gateFailures++;
            }

            string key = $"{unitType}|{team}|{reason}";
            if (!Coverages.TryGetValue(key, out Coverage coverage))
            {
                coverage = new Coverage();
                Coverages.Add(key, coverage);
            }
            coverage.Frames++;
            if (phase == UnitMovementPhase.AlignToMove) coverage.Align++;
            if (phase == UnitMovementPhase.Move) coverage.Move++;
            if (phase == UnitMovementPhase.NoIntent) coverage.NoIntent++;
            if (evaluation.EndpointNormalized) coverage.Endpoint++;
            if (evaluation.Decision.IsTargetAcquirePriority) coverage.TargetPriority++;

            bool hasPreviousPhase = LastPhases.TryGetValue(
                networkObjectId, out ServerPhaseSnapshot previousSnapshot);
            if (hasPreviousPhase && previousSnapshot.UnitId != unitId)
            {
                _lifecycleIdentityConflicts++;
                if (_details++ < MaximumDetails)
                {
                    LogCausalFailure("server-lifecycle-identity-crossing",
                        $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                        $"baselineUnitId={previousSnapshot.UnitId}");
                }
                return;
            }

            UnitMovementPhase previous = hasPreviousPhase
                ? previousSnapshot.Phase
                : UnitMovementPhase.Invalid;
            LastPhases[networkObjectId] =
                new ServerPhaseSnapshot(unitId, phase);
            string transition = $"{reason}|{previous}->{phase}";
            if (previous != phase && PhaseTransitions.Add(transition)
                && _details < MaximumDetails)
            {
                _details++;
                Log(LogLevel.Info, "phase-transition",
                    $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                    $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                    $"reason={reason}, previous={previous}, phase={phase}, " +
                    $"yaw={evaluation.Decision.YawErrorDegrees:F6}, positionDelta={positionDelta:F6}");
            }
        }

        internal static void ObserveLegacyWriterSelection()
        {
            if (_active) _writerSelectionFailures++;
        }

        internal static void ObserveClientWriteAttempt()
        {
            if (_active) _clientWriteAttempts++;
        }

        internal static void ObserveAttackWriterOwnershipConflict(int unitId)
        {
            if (!_active) return;
            _attackWriterOwnershipConflicts++;
            if (_details++ < MaximumDetails)
                LogCausalFailure("double-writer-attempt", $"unitId={unitId}");
        }

        internal static void ObservePublicationFailure(int unitId)
        {
            if (!_active) return;
            _rejected++;
            LogFailure("movement-publication-failed", $"unitId={unitId}");
        }

        /// <summary>
        /// UnitView adapter가 fail-closed로 중단한 경계를 공용 bounded failure 채널에 기록한다.
        /// 게임 상태를 쓰지 않으며 server authority observer가 활성인 경우에만 집계한다.
        /// </summary>
        internal static void ObserveAdapterFailure(string kind, string context)
        {
            if (!_active || !_isServer) return;
            if (_gameplayEnded)
            {
                IncrementBounded(ref _postGameAdapterFailuresSuppressed);
                return;
            }
            RecordAdapterFailure(kind, context, emitLog: true);
        }

        /// <summary>
        /// GameEnd 이후 남은 프레임/이벤트는 더 이상 gameplay 이동 계약의 표본이 아니다.
        /// 세션 terminal은 유지하되 뒤늦은 adapter callback만 별도 suppressed 카운터로 분리한다.
        /// </summary>
        internal static void MarkGameplayEnded()
        {
            if (_active && _isServer)
                _gameplayEnded = true;
        }

        private static void RecordAdapterFailure(
            string kind,
            string context,
            bool emitLog)
        {
            IncrementBounded(ref _adapterFailures);
            if (emitLog)
            {
                LogFailure(
                    "movement-adapter-failure",
                    $"kind={kind ?? "unknown"}, context={context ?? "unavailable"}");
                return;
            }

            if (_failureDetails >= MaximumFailureDetails)
                _failureEvidenceOverflow++;
            else
                _failureDetails++;
        }

        internal static string ValidateAdapterFailureStateForValidation(
            int observations)
        {
            if (observations < 0) return "Invalid";
            Reset();
            _active = true;
            _isServer = true;
            for (int index = 0; index < observations; index++)
                RecordAdapterFailure("fixture", "fixture", emitLog: false);
            int details = _failureDetails;
            int overflow = _failureEvidenceOverflow;
            int failures = _adapterFailures;
            bool failure = HasAdapterFailure();
            Reset();
            bool reset = _adapterFailures == 0
                && _failureDetails == 0
                && _failureEvidenceOverflow == 0
                && !_active;
            return $"details={details},overflow={overflow},adapter={failures}," +
                   $"failure={failure},reset={reset}";
        }

        internal static string ValidatePostGameAdapterFailureForValidation()
        {
            Reset();
            _active = true;
            _isServer = true;
            MarkGameplayEnded();
            ObserveAdapterFailure("fixture-after-game-end", "fixture");
            bool valid = _adapterFailures == 0
                && _failureDetails == 0
                && _failureEvidenceOverflow == 0
                && _postGameAdapterFailuresSuppressed == 1;
            Reset();
            bool reset = !_gameplayEnded && _postGameAdapterFailuresSuppressed == 0;
            return valid && reset ? "PASS" : "FAIL";
        }

        private static bool HasAdapterFailure() =>
            _adapterFailures != 0 || _failureEvidenceOverflow != 0;

        internal static void ObserveCorridorResolution(
            UnitTrajectoryCorridorResolution resolution,
            bool usedStartTileEgress)
        {
            if (!_active || !_isServer) return;

            if (usedStartTileEgress
                && resolution != UnitTrajectoryCorridorResolution.Invalid
                && resolution != UnitTrajectoryCorridorResolution.RepathRequired)
            {
                _corridorStartTileEgressFrames++;
            }

            switch (resolution)
            {
                case UnitTrajectoryCorridorResolution.FullSmooth:
                    _corridorFullSmooth++;
                    break;
                case UnitTrajectoryCorridorResolution.ReducedSmooth:
                    _corridorReducedSmooth++;
                    break;
                case UnitTrajectoryCorridorResolution.FullDirect:
                    _corridorFullDirect++;
                    break;
                case UnitTrajectoryCorridorResolution.ReducedDirect:
                    _corridorReducedDirect++;
                    break;
                case UnitTrajectoryCorridorResolution.StationaryAlignment:
                    _corridorStationaryAlignment++;
                    break;
                case UnitTrajectoryCorridorResolution.RepathRequired:
                    _corridorRepathRequired++;
                    break;
            }
        }

        internal static void ObserveCenterCheckpoint(double errorWorld)
        {
            if (!_active || !_isServer) return;
            if (double.IsNaN(errorWorld) || double.IsInfinity(errorWorld)
                || errorWorld < 0d)
            {
                RecordAdapterFailure(
                    "center-checkpoint-error-invalid",
                    $"errorWorld={errorWorld}",
                    emitLog: true);
                return;
            }

            IncrementBounded(ref _centerCheckpointsReached);
            _maximumCenterCheckpointErrorWorld = Math.Max(
                _maximumCenterCheckpointErrorWorld,
                errorWorld);
            if (errorWorld > UnitMovementEvaluationAdapter.PositionTolerance)
            {
                RecordAdapterFailure(
                    "center-checkpoint-outside-tolerance",
                    $"errorWorld={errorWorld:F9}, " +
                    $"tolerance={UnitMovementEvaluationAdapter.PositionTolerance:F9}",
                    emitLog: true);
            }
        }

        internal static void ObserveChaseApproach(bool directSafe)
        {
            if (!_active || !_isServer) return;
            if (directSafe)
                IncrementBounded(ref _chaseDirectSafeFrames);
            else
                IncrementBounded(ref _chasePathFrames);
        }

        internal static void ObserveSpatialFinalCandidate(
            int unitId,
            UnitSpatialCandidateVerdict verdict,
            UnitSpatialTransitionPlanStatus status,
            UnitSpatialPreflightResult preflight,
            int transitionCount,
            HexCoord committedPosition,
            HexCoord rootBeforeHex,
            HexCoord candidateHex,
            bool repathRequired)
        {
            if (!_active || !_isServer) return;

            if (verdict == UnitSpatialCandidateVerdict.Accepted
                && preflight.IsSuccess
                && !repathRequired)
            {
                IncrementBounded(ref _spatialStagesForCommit);
                if (status == UnitSpatialTransitionPlanStatus.NoTransition)
                    IncrementBounded(ref _spatialNoTransitionPlans);
                if (status == UnitSpatialTransitionPlanStatus.TerminalContact)
                    IncrementBounded(ref _spatialTerminalContacts);
                return;
            }

            if (repathRequired
                && (verdict == UnitSpatialCandidateVerdict.RouteInvalidated
                    || verdict == UnitSpatialCandidateVerdict.CandidateUnsafe))
            {
                IncrementBounded(ref _spatialFinalRecoverableRepaths);
                string signature = $"{verdict}|{status}|{preflight.Status}|" +
                    $"{preflight.OffendingTile}|{preflight.OffendingTransitionIndex}|" +
                    $"{committedPosition}|{rootBeforeHex}|{candidateHex}";
                if (LastRecoverableSignatures.TryGetValue(unitId, out string previous)
                    && string.Equals(previous, signature, StringComparison.Ordinal))
                {
                    IncrementBounded(ref _spatialRepeatedRecoverableRepaths);
                    if (LastRecoverableFrames.TryGetValue(unitId, out int previousFrame)
                        && previousFrame == Time.frameCount)
                    {
                        IncrementBounded(ref _spatialSameFrameRetries);
                    }
                    LogFailure("spatial-recoverable-repeated",
                        $"unitId={unitId}, verdict={verdict}, status={status}, " +
                        $"preflight={preflight.Status}, offendingTile={preflight.OffendingTile}, " +
                        $"offendingIndex={preflight.OffendingTransitionIndex}, " +
                        $"committedPosition={committedPosition}, rootBeforeHex={rootBeforeHex}, " +
                        $"candidateHex={candidateHex}, sameFrame={previousFrame == Time.frameCount}");
                }
                LastRecoverableSignatures[unitId] = signature;
                LastRecoverableFrames[unitId] = Time.frameCount;
                return;
            }

            IncrementBounded(ref _spatialFatalPreflights);
            IncrementBounded(ref _spatialPreflightFailures);
            LogFailure("spatial-preflight-fatal",
                $"unitId={unitId}, verdict={verdict}, status={status}, " +
                $"preflight={preflight.Status}, offendingTile={preflight.OffendingTile}, " +
                $"offendingIndex={preflight.OffendingTransitionIndex}, " +
                $"transitionCount={transitionCount}, committedPosition={committedPosition}, " +
                $"rootBeforeHex={rootBeforeHex}, candidateHex={candidateHex}");
        }

        internal static void ObserveSpatialCleanupAfterRecoverable(int unitId)
        {
            if (!_active || !_isServer) return;
            IncrementBounded(ref _spatialCleanupAfterRecoverable);
            LogFailure("spatial-cleanup-after-recoverable", $"unitId={unitId}");
        }

        internal static void ObserveSpatialStageDivergence(
            int unitId,
            UnitSpatialCandidateVerdict probeVerdict,
            UnitSpatialCandidateVerdict stageVerdict,
            UnitSpatialPreflightStatus probePreflight,
            UnitSpatialPreflightStatus stagePreflight)
        {
            if (!_active || !_isServer) return;
            IncrementBounded(ref _spatialStageDivergences);
            LogFailure("spatial-probe-stage-divergence",
                $"unitId={unitId}, probeVerdict={probeVerdict}, stageVerdict={stageVerdict}, " +
                $"probePreflight={probePreflight}, stagePreflight={stagePreflight}");
        }

        internal static void ObserveSpatialCommit(
            int unitId,
            int transitionCount,
            HexCoord committedPositionBefore,
            HexCoord committedPositionAfter,
            bool succeeded)
        {
            if (!_active || !_isServer) return;

            if (succeeded)
            {
                AddBounded(
                    ref _spatialTransitionsCommitted,
                    UnitSpatialDiagnosticAccountingPolicy
                        .CommittedDeltaAtCommitResult(transitionCount, true));
                if (UnitSpatialDiagnosticAccountingPolicy
                    .ClearsRecoverableHistory(transitionCount, true))
                {
                    LastRecoverableSignatures.Remove(unitId);
                    LastRecoverableFrames.Remove(unitId);
                }
                return;
            }

            IncrementBounded(ref _spatialCommitFailures);
            LogFailure("spatial-commit-failed",
                $"unitId={unitId}, transitionCount={transitionCount}, " +
                $"committedPositionBefore={committedPositionBefore}, " +
                $"committedPositionAfter={committedPositionAfter}");
        }

        internal static void ObserveSpatialCommitAttempt(
            int unitId,
            int transitionCount,
            HexCoord committedPosition)
        {
            if (!_active || !_isServer) return;

            int plannedDelta = UnitSpatialDiagnosticAccountingPolicy
                .PlannedDeltaAtCommitAttempt(transitionCount);
            if (plannedDelta <= 0)
            {
                IncrementBounded(ref _spatialPreflightFailures);
                LogFailure("spatial-commit-attempt-invalid",
                    $"unitId={unitId}, transitionCount={transitionCount}, " +
                    $"committedPosition={committedPosition}");
                return;
            }

            AddBounded(ref _spatialTransitionsPlanned, plannedDelta);
        }

        private static void IncrementBounded(ref int value)
        {
            if (value < int.MaxValue)
                value++;
        }

        private static void AddBounded(ref int value, int amount)
        {
            if (amount <= 0 || value == int.MaxValue) return;
            value = amount >= int.MaxValue - value
                ? int.MaxValue
                : value + amount;
        }

        internal static void ObserveIgnoredServerTargetEvent(
            int unitId,
            string eventKind)
        {
            if (!_active || !_isServer) return;
            _ignoredServerTargetEvents++;
            if (_details < MaximumDetails)
            {
                _details++;
                Log(LogLevel.Info, "ignored-stale-action-target-event",
                    $"unitId={unitId}, eventKind={eventKind}");
            }
        }

        internal static void ObserveCombatPresentationHandoffFailure(
            int unitId,
            string boundary)
        {
            if (!_active || !_isServer) return;
            // callback 재시도는 같은 미해결 원인을 매 frame 다시 통과할 수 있다.
            // 성공 없이 이어진 같은 유닛의 반복은 최초 한 건만 causal failure다.
            if (!ActiveHandoffFailureEpisodes.ObserveFailure(unitId)) return;
            IncrementBounded(ref _combatPresentationHandoffFailures);
            LogFailure(
                "combat-presentation-handoff-failed",
                $"unitId={unitId}, boundary={boundary}");
        }

        internal static void ObserveCombatPresentationHandoffSuccess(int unitId)
        {
            if (!_active || !_isServer) return;
            ActiveHandoffFailureEpisodes.ObserveSuccess(unitId);
        }

        internal static void ObserveDeferredCombatTargetChange(
            int unitId,
            string reason)
        {
            if (!_active || !_isServer) return;
            IncrementBounded(ref _deferredCombatTargetChanges);
            // 정상적인 틱 순서에서 반복될 수 있으므로 전체 detail 예산을 소모하지 않고
            // 대표 1건과 terminal 총계만 남긴다.
            if (_deferredCombatTargetChanges == 1 && _details < MaximumDetails)
            {
                _details++;
                Log(LogLevel.Info, "combat-target-change-deferred",
                    $"unitId={unitId}, reason={reason}");
            }
        }

        private static void LogFailure(string message, string data)
        {
            if (_failureDetails >= MaximumFailureDetails)
            {
                _failureEvidenceOverflow++;
                return;
            }

            _failureDetails++;
            LogCausalFailure(message, data);
        }

        internal static void ObserveClientReplicatedState(
            int unitId,
            ulong networkObjectId,
            UnitMovementPhase phase,
            ulong commandRevision,
            ulong segmentRevision,
            ulong semanticRevision)
        {
            if (!_active || _isServer) return;
            _clientReplicatedSamples++;
            bool hasPrevious = ClientReplicatedSnapshots.TryGetValue(
                networkObjectId, out ClientReplicatedSnapshot previous);
            ClientReplicationIssue issue = ClassifyClientReplicatedState(
                hasPrevious,
                previous,
                unitId,
                phase,
                commandRevision,
                segmentRevision,
                semanticRevision);
            bool duplicate = (issue & ClientReplicationIssue.Duplicate) != 0;
            ClientReplicationIssue failures =
                issue & ~ClientReplicationIssue.Duplicate;

            if (duplicate) _clientReplicatedDuplicates++;
            if ((failures & ClientReplicationIssue.RevisionZero) != 0)
                _clientRevisionZero++;
            if ((failures & ClientReplicationIssue.RevisionRegression) != 0)
                _clientRevisionRegressions++;
            if ((failures & ClientReplicationIssue.SameRevisionConflict) != 0)
                _clientSameRevisionConflicts++;
            if ((failures & ClientReplicationIssue.InvalidPhase) != 0)
                _clientInvalidPhase++;
            if ((failures & ClientReplicationIssue.UnitUninitialized) != 0)
                _clientUnitUninitialized++;
            if ((failures & ClientReplicationIssue.InvalidScope) != 0)
                _clientInvalidScope++;
            if ((failures & ClientReplicationIssue.IdentityConflict) != 0)
                _clientIdentityConflicts++;
            if ((failures & ClientReplicationIssue.ScopeRegression) != 0)
                _clientScopeRegressions++;
            if (failures != ClientReplicationIssue.None)
                _clientReplicatedInvalid++;

            if (failures == ClientReplicationIssue.None && !duplicate)
            {
                ClientReplicatedSnapshots[networkObjectId] =
                    new ClientReplicatedSnapshot(
                        unitId,
                        phase,
                        commandRevision,
                        segmentRevision,
                        semanticRevision);
            }

            if (_details++ < MaximumDetails)
            {
                string data =
                    $"unitId={unitId}, networkObjectId={networkObjectId}, phase={phase}, " +
                    $"commandRevision={commandRevision}, segmentRevision={segmentRevision}, " +
                    $"semanticRevision={semanticRevision}, classification={issue}";
                if (failures == ClientReplicationIssue.None)
                    Log(LogLevel.Info, "client-replicated-state", data);
                else
                    LogCausalFailure("client-replicated-state", data);
            }
        }

        internal static bool ShouldObserveClientReplicatedState(
            int unitId,
            ulong semanticRevision)
            => unitId >= 0 && semanticRevision != 0UL;

        /// <summary>
        /// 순수 Client에서 실제로 렌더될 Simulation Root 위치와 Animator 표시를 frame마다
        /// 함께 수집한다. 서버 reducer 결과를 추측하거나 어떤 gameplay 값도 쓰지 않는다.
        /// </summary>
        internal static void ObserveClientAttackPresentationFrame(
            int unitId,
            ulong networkObjectId,
            Vector3 rootPosition,
            bool walkPresentationVisible,
            bool attackPresentationVisible,
            UnitMovementPhase movementPhase,
            ulong commandRevision,
            ulong segmentRevision,
            ulong semanticRevision)
        {
            if (!_active || _isServer || unitId < 0 || networkObjectId == 0UL)
                return;
            if (!WorldPointXZ.TryCreate(
                    rootPosition.x,
                    rootPosition.z,
                    out WorldPointXZ position))
                return;

            _clientPresentationFrames++;
            if (!ClientAttackEntryTrackers.TryGetValue(
                    networkObjectId,
                    out ClientAttackEntryPresentationOrderTracker tracker))
            {
                tracker = new ClientAttackEntryPresentationOrderTracker();
                ClientAttackEntryTrackers.Add(networkObjectId, tracker);
            }
            tracker.ObserveFrame(
                position,
                walkPresentationVisible,
                attackPresentationVisible,
                movementPhase,
                commandRevision,
                segmentRevision,
                semanticRevision);
        }

        /// <summary>
        /// 원자 Start payload가 순수 Client Animator에 최초 적용된 순간을 기록한다.
        /// 직전 렌더 frame에 Root stationary + Walk가 있었다면 1건부터 terminal FAIL이다.
        /// </summary>
        internal static void ObserveClientAttackPresentationStarted(
            int unitId,
            ulong networkObjectId)
        {
            if (!_active || _isServer || unitId < 0 || networkObjectId == 0UL)
                return;

            _clientAttackPresentationStarts++;
            if (!ClientAttackEntryTrackers.TryGetValue(
                    networkObjectId,
                    out ClientAttackEntryPresentationOrderTracker tracker))
                return;
            ClientAttackEntryGapKind gapKind =
                tracker.ClassifyAttackPresentationStarted(
                    out int stationaryGapFrames);
            if (gapKind == ClientAttackEntryGapKind.None)
                return;

            if (gapKind == ClientAttackEntryGapKind.TransportOrInterpolation)
            {
                _clientAttackEntryTransportGaps++;
                return;
            }

            _clientAttackEntryOrderViolations++;
            LogFailure(
                "client-attack-entry-order",
                $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                $"stationaryGapFrames={stationaryGapFrames}");
        }

        internal static void RetireUnitLifecycle(
            int unitId,
            ulong networkObjectId)
        {
            if (!_active) return;

            ActiveHandoffFailureEpisodes.Retire(unitId);

            if (_isServer)
            {
                RetireRecoverableHistory(
                    LastRecoverableSignatures,
                    LastRecoverableFrames,
                    unitId);
                LifecycleRetireResult serverResult = RetireUnitLifecycleCore(
                    LastPhases,
                    networkObjectId,
                    unitId,
                    snapshot => snapshot.UnitId,
                    out int baselineUnitId);
                if (serverResult == LifecycleRetireResult.IdentityConflictRetired)
                {
                    _lifecycleIdentityConflicts++;
                    if (_details++ < MaximumDetails)
                    {
                        LogCausalFailure("server-lifecycle-retire-conflict",
                            $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                            $"baselineUnitId={baselineUnitId}");
                    }
                }
                return;
            }

            LifecycleRetireResult clientResult = RetireUnitLifecycleCore(
                ClientReplicatedSnapshots,
                networkObjectId,
                unitId,
                snapshot => snapshot.UnitId,
                out int clientBaselineUnitId);
            ClientAttackEntryTrackers.Remove(networkObjectId);
            if (clientResult == LifecycleRetireResult.IdentityConflictRetired)
            {
                _clientReplicatedInvalid++;
                _clientIdentityConflicts++;
                _lifecycleIdentityConflicts++;
                if (_details++ < MaximumDetails)
                {
                    LogCausalFailure("client-lifecycle-retire-conflict",
                        $"unitId={unitId}, networkObjectId={networkObjectId}, " +
                        $"baselineUnitId={clientBaselineUnitId}");
                }
            }
        }

        private static void RetireRecoverableHistory(
            IDictionary<int, string> signatures,
            IDictionary<int, int> frames,
            int unitId)
        {
            signatures?.Remove(unitId);
            frames?.Remove(unitId);
        }

        private static LifecycleRetireResult RetireUnitLifecycleCore<TSnapshot>(
            IDictionary<ulong, TSnapshot> snapshots,
            ulong networkObjectId,
            int unitId,
            Func<TSnapshot, int> selectUnitId,
            out int baselineUnitId)
        {
            baselineUnitId = -1;
            if (!snapshots.TryGetValue(networkObjectId, out TSnapshot previous))
                return LifecycleRetireResult.NotFound;

            baselineUnitId = selectUnitId(previous);
            snapshots.Remove(networkObjectId);
            return baselineUnitId == unitId
                ? LifecycleRetireResult.Retired
                : LifecycleRetireResult.IdentityConflictRetired;
        }

        private static ClientReplicationIssue ClassifyClientReplicatedState(
            bool hasPrevious,
            ClientReplicatedSnapshot previous,
            int unitId,
            UnitMovementPhase phase,
            ulong commandRevision,
            ulong segmentRevision,
            ulong semanticRevision)
        {
            ClientReplicationIssue issue = ClientReplicationIssue.None;
            if (unitId < 0)
                issue |= ClientReplicationIssue.UnitUninitialized;
            if (hasPrevious && unitId != previous.UnitId)
                issue |= ClientReplicationIssue.IdentityConflict;
            if (!Enum.IsDefined(typeof(UnitMovementPhase), phase)
                || phase == UnitMovementPhase.Invalid)
                issue |= ClientReplicationIssue.InvalidPhase;
            if (commandRevision == 0UL || segmentRevision == 0UL)
                issue |= ClientReplicationIssue.InvalidScope;
            if (semanticRevision == 0UL)
                issue |= ClientReplicationIssue.RevisionZero;
            else if (hasPrevious && semanticRevision < previous.SemanticRevision)
                issue |= ClientReplicationIssue.RevisionRegression;
            else if (hasPrevious && semanticRevision == previous.SemanticRevision)
            {
                if (phase == previous.Phase
                    && unitId == previous.UnitId
                    && commandRevision == previous.CommandRevision
                    && segmentRevision == previous.SegmentRevision)
                    issue |= ClientReplicationIssue.Duplicate;
                else
                    issue |= ClientReplicationIssue.SameRevisionConflict;
            }
            else if (semanticRevision != 0UL
                && hasPrevious
                && semanticRevision > previous.SemanticRevision)
            {
                // NGO는 중간 semantic snapshot을 coalesce할 수 있다. 따라서 클라이언트는
                // 연속 증가(+1)를 강제하지 않고 관찰 가능한 scope의 사전식 단조성만 검증한다.
                if (commandRevision < previous.CommandRevision
                    || (commandRevision == previous.CommandRevision
                        && segmentRevision < previous.SegmentRevision))
                {
                    issue |= ClientReplicationIssue.ScopeRegression;
                }
            }

            return issue;
        }

        // Editor self-validation이 production과 동일한 순수 판정기를 호출하는 reflection seam.
        private static string ClassifyClientReplicatedStateForValidation(
            bool hasPrevious,
            int previousUnitId,
            int previousPhase,
            ulong previousCommandRevision,
            ulong previousSegmentRevision,
            ulong previousSemanticRevision,
            int unitId,
            int phase,
            ulong commandRevision,
            ulong segmentRevision,
            ulong semanticRevision)
        {
            ClientReplicationIssue issue = ClassifyClientReplicatedState(
                hasPrevious,
                new ClientReplicatedSnapshot(
                    previousUnitId,
                    (UnitMovementPhase)previousPhase,
                    previousCommandRevision,
                    previousSegmentRevision,
                    previousSemanticRevision),
                unitId,
                (UnitMovementPhase)phase,
                commandRevision,
                segmentRevision,
                semanticRevision);
            return issue.ToString();
        }

        private static string RetireUnitLifecycleForValidation()
        {
            const ulong networkObjectId = 42UL;
            var snapshots =
                new Dictionary<ulong, ClientReplicatedSnapshot>();
            snapshots.Add(
                networkObjectId,
                new ClientReplicatedSnapshot(
                    7,
                    UnitMovementPhase.Move,
                    2UL,
                    3UL,
                    9UL));

            LifecycleRetireResult wrongIdentity = RetireUnitLifecycleCore(
                snapshots,
                networkObjectId,
                8,
                snapshot => snapshot.UnitId,
                out _);
            bool wrongIdentityRetired =
                wrongIdentity == LifecycleRetireResult.IdentityConflictRetired
                && !snapshots.ContainsKey(networkObjectId);

            snapshots.Add(
                networkObjectId,
                new ClientReplicatedSnapshot(
                    7,
                    UnitMovementPhase.Move,
                    2UL,
                    3UL,
                    9UL));
            LifecycleRetireResult correctIdentity = RetireUnitLifecycleCore(
                snapshots,
                networkObjectId,
                7,
                snapshot => snapshot.UnitId,
                out _);
            bool retired = correctIdentity == LifecycleRetireResult.Retired
                && !snapshots.ContainsKey(networkObjectId);

            var recoverableSignatures = new Dictionary<int, string>
            {
                [7] = "old-lifecycle"
            };
            var recoverableFrames = new Dictionary<int, int>
            {
                [7] = 123
            };
            RetireRecoverableHistory(
                recoverableSignatures,
                recoverableFrames,
                unitId: 7);
            bool recoverableHistoryRetired =
                !recoverableSignatures.ContainsKey(7)
                && !recoverableFrames.ContainsKey(7);

            ClientReplicationIssue reused = ClassifyClientReplicatedState(
                hasPrevious: false,
                previous: default,
                unitId: 8,
                phase: UnitMovementPhase.Move,
                commandRevision: 1UL,
                segmentRevision: 1UL,
                semanticRevision: 1UL);
            return wrongIdentityRetired && retired
                && recoverableHistoryRetired
                && reused == ClientReplicationIssue.None
                    ? "PASS"
                    : "FAIL";
        }

        internal static void EndSession(string reason)
        {
            if (!_active) return;
            // END 미도달 시 미관측이며 기존 normal 예산만 사용한다.
            try { EmitMotionPeak(_curvePeak); EmitMotionPeak(_fallbackPeak); } catch { }
            var entries = new List<string>();
            foreach (KeyValuePair<string, Coverage> pair in Coverages)
            {
                Coverage value = pair.Value;
                entries.Add($"{pair.Key}|frames={value.Frames}|align={value.Align}|move={value.Move}|noIntent={value.NoIntent}|endpoint={value.Endpoint}|targetPriority={value.TargetPriority}");
            }
            entries.Sort(StringComparer.Ordinal);
            string joined = string.Join("\n", entries);
            string sha = Sha256(joined);
            bool manifestValid = UnitMovementManifestChunker.TryBuild(
                entries,
                PayloadLimit,
                MaximumManifestChunks,
                ReservedTerminalLines,
                nonManifestTerminalLines: NonManifestTerminalLines,
                out IReadOnlyList<string> chunks);
            if (!manifestValid)
            {
                _manifestPreflightFailures++;
                chunks = Array.Empty<string>();
            }
            bool failure = _rejected != 0 || _invalid != 0 || _gateFailures != 0
                || _writerSelectionFailures != 0 || _clientWriteAttempts != 0
                || _attackWriterOwnershipConflicts != 0 || _dropped != 0
                || _combatPresentationHandoffFailures != 0
                || _stationaryWalkViolations != 0
                || _clientAttackEntryOrderViolations != 0
                || _spatialPreflightFailures != 0
                || _spatialCommitFailures != 0
                || _spatialRecoveries != 0
                || _spatialRepeatedRecoverableRepaths != 0
                || _spatialFatalPreflights != 0
                || _spatialCleanupAfterRecoverable != 0
                || _spatialSameFrameRetries != 0
                || _spatialStageDivergences != 0
                || _spatialTransitionsPlanned != _spatialTransitionsCommitted
                || HasAdapterFailure()
                || !manifestValid || _manifestPreflightFailures != 0
                || _terminalOverflow != 0 || _clientReplicatedInvalid != 0
                || _lifecycleIdentityConflicts != 0
                || (_isServer && (_frames == 0 || _align == 0 || _move == 0
                    || _noIntent == 0 || entries.Count == 0))
                || (!_isServer && _clientReplicatedSamples == 0);

            List<TerminalRecord> records = BuildTerminalRecords(
                chunks,
                entries.Count,
                sha,
                reason,
                failure);
            if (!TryPreflightTerminalRecords(records, out _))
            {
                _terminalPreflightFailures++;
                failure = true;
                records = BuildTerminalRecords(
                    chunks,
                    entries.Count,
                    sha,
                    "terminal-preflight-failed",
                    failure);
            }

            bool terminalSafe = TryPreflightTerminalRecords(records, out _);
            if (!terminalSafe)
            {
                _terminalOverflow++;
                LogTerminal(
                    LogLevel.Error,
                    "END",
                    $"reason=terminal-preflight-unrecoverable, " +
                    $"role={(_isServer ? "host" : "client")}, verdict=FAIL");
                Reset();
                return;
            }

            foreach (TerminalRecord record in records)
            {
                // manifest/axis summaries are parser evidence, not independent causes.
                // FAIL 경기에서도 이들을 ERROR로 반복하면 한 adapter failure가 수십 개의
                // Unity stack line으로 증폭된다. 내용/schema는 유지하고 최종 END 한 줄만
                // 이 축의 ERROR verdict로 남긴다.
                LogLevel recordLevel = record.Message == "END" && failure
                    ? LogLevel.Error
                    : LogLevel.Info;
                LogTerminal(recordLevel, record.Message, record.Data);
            }
            Reset();
        }

        private static List<TerminalRecord> BuildTerminalRecords(
            IReadOnlyList<string> chunks,
            int coverageEntries,
            string coverageSha,
            string reason,
            bool failure)
        {
            var records = new List<TerminalRecord>(
                (chunks?.Count ?? 0) + NonManifestTerminalLines);
            if (chunks != null)
            {
                for (int index = 0; index < chunks.Count; index++)
                {
                    records.Add(new TerminalRecord(
                        "coverage-manifest",
                        $"chunk={index + 1}/{chunks.Count}, entries={chunks[index]}"));
                }
            }

            records.Add(new TerminalRecord(
                "terminal-summary-core",
                $"manifestChunks={chunks?.Count ?? 0}, " +
                $"manifestPreflightFailures={_manifestPreflightFailures}, " +
                $"terminalPreflightFailures={_terminalPreflightFailures}, " +
                $"frames={_frames}, align={_align}, move={_move}, noIntent={_noIntent}, " +
                $"endpoint={_endpoint}, targetPriority={_targetPriority}, rejected={_rejected}, " +
                $"invalid={_invalid}, gateFailures={_gateFailures}, " +
                $"writerSelectionFailures={_writerSelectionFailures}, " +
                $"clientRootOrReducerWriteAttempts={_clientWriteAttempts}, " +
                $"attackWriterOwnershipConflicts={_attackWriterOwnershipConflicts}, " +
                $"ignoredServerTargetEvents={_ignoredServerTargetEvents}, " +
                $"deferredCombatTargetChanges={_deferredCombatTargetChanges}, " +
                $"combatPresentationHandoffFailures={_combatPresentationHandoffFailures}, " +
                $"heldFrames={_heldFrames}, stationaryWalkViolations={_stationaryWalkViolations}, " +
                $"failureDetails={_failureDetails}, " +
                $"failureEvidenceOverflow={_failureEvidenceOverflow}, " +
                $"adapterFailures={_adapterFailures}, " +
                $"postGameAdapterFailuresSuppressed={_postGameAdapterFailuresSuppressed}, " +
                $"droppedLogs={_dropped}"));
            records.Add(new TerminalRecord(
                "terminal-summary-corridor",
                $"corridorFullSmooth={_corridorFullSmooth}, " +
                $"corridorReducedSmooth={_corridorReducedSmooth}, " +
                $"corridorFullDirect={_corridorFullDirect}, " +
                $"corridorReducedDirect={_corridorReducedDirect}, " +
                $"corridorStationaryAlignment={_corridorStationaryAlignment}, " +
                $"corridorRepathRequired={_corridorRepathRequired}, " +
                $"corridorStartTileEgressFrames={_corridorStartTileEgressFrames}, " +
                $"centerCheckpointsReached={_centerCheckpointsReached}, " +
                $"maximumCenterCheckpointErrorWorld={_maximumCenterCheckpointErrorWorld:F9}, " +
                $"chaseDirectSafeFrames={_chaseDirectSafeFrames}, " +
                $"chasePathFrames={_chasePathFrames}"));
            records.Add(new TerminalRecord(
                "terminal-summary-spatial-commit",
                $"spatialTransitionsPlanned={_spatialTransitionsPlanned}, " +
                $"spatialTransitionsCommitted={_spatialTransitionsCommitted}, " +
                $"spatialNoTransitionPlans={_spatialNoTransitionPlans}, " +
                $"spatialTerminalContacts={_spatialTerminalContacts}, " +
                $"spatialPreflightFailures={_spatialPreflightFailures}, " +
                $"spatialCommitFailures={_spatialCommitFailures}, " +
                $"spatialRecoveries={_spatialRecoveries}, " +
                $"spatialStagesForCommit={_spatialStagesForCommit}, " +
                FormattableString.Invariant($"cornerMinMovingSpeed={(double.IsPositiveInfinity(_cornerMinSpeed) ? 0d : _cornerMinSpeed):F6}, cornerMinRatio={_cornerMinRatio:F6}, cornerMaxCross={_cornerMaxCross:F6}, cornerMaxTurnExcess={_cornerMaxTurnExcess:F6}, cornerFrameTimeSum={_cornerSeconds:F6}")));
            records.Add(new TerminalRecord(
                "terminal-summary-spatial-repath",
                $"spatialFinalRecoverableRepaths={_spatialFinalRecoverableRepaths}, " +
                $"spatialRepeatedRecoverableRepaths={_spatialRepeatedRecoverableRepaths}, " +
                $"spatialFatalPreflights={_spatialFatalPreflights}, " +
                $"spatialCleanupAfterRecoverable={_spatialCleanupAfterRecoverable}, " +
                $"spatialSameFrameRetries={_spatialSameFrameRetries}, " +
                $"spatialStageDivergences={_spatialStageDivergences}, " +
                $"cornerPolicy=fallback-v1, curveFrames={_centerCurveFrames}, cornerFrames={_cornerSpeedFrames}, " +
                $"cornerStarts={_cornerStarts}, cornerCompleted={_cornerCompleted}, " +
                $"cornerIncomplete={Math.Max(0, _cornerStarts-_cornerCompleted)}, " +
                $"cornerNormalStops={_cornerNormalStops}, cornerSafetyStops={_cornerSafetyStops}"));
            records.Add(new TerminalRecord(
                "terminal-summary-replication",
                $"clientReplicatedSamples={_clientReplicatedSamples}, " +
                $"clientReplicatedInvalid={_clientReplicatedInvalid}, " +
                $"clientReplicatedDuplicates={_clientReplicatedDuplicates}, " +
                $"clientRevisionZero={_clientRevisionZero}, " +
                $"clientRevisionRegressions={_clientRevisionRegressions}, " +
                $"clientSameRevisionConflicts={_clientSameRevisionConflicts}, " +
                $"clientInvalidPhase={_clientInvalidPhase}, " +
                $"clientUnitUninitialized={_clientUnitUninitialized}, " +
                $"clientInvalidScope={_clientInvalidScope}, " +
                $"clientIdentityConflicts={_clientIdentityConflicts}, " +
                $"clientScopeRegressions={_clientScopeRegressions}, " +
                $"clientPresentationFrames={_clientPresentationFrames}, " +
                $"clientAttackPresentationStarts={_clientAttackPresentationStarts}, " +
                $"clientAttackEntryTransportGaps={_clientAttackEntryTransportGaps}, " +
                $"clientAttackEntryOrderViolations={_clientAttackEntryOrderViolations}, " +
                $"lifecycleIdentityConflicts={_lifecycleIdentityConflicts}, " +
                $"coverageEntries={coverageEntries}, coverageSha256={coverageSha}, " +
                $"terminalOverflow={_terminalOverflow}"));
            records.Add(new TerminalRecord(
                "END",
                $"reason={NormalizeTerminalToken(reason)}, " +
                $"role={(_isServer ? "host" : "client")}, " +
                $"verdict={(failure ? "FAIL" : "EVIDENCE")}"));
            return records;
        }

        private static bool TryPreflightTerminalRecords(
            IReadOnlyList<TerminalRecord> records,
            out int maximumUtf8Bytes)
        {
            maximumUtf8Bytes = 0;
            if (records == null || records.Count > ReservedTerminalLines)
                return false;
            for (int index = 0; index < records.Count; index++)
            {
                TerminalRecord record = records[index];
                int bytes = Encoding.UTF8.GetByteCount(FormatFullTerminalLine(
                    record.Message,
                    record.Data,
                    _runId,
                    _sessionKey,
                    _startedAt));
                maximumUtf8Bytes = Math.Max(maximumUtf8Bytes, bytes);
                if (bytes >= MaximumFullLineUtf8BytesExclusive)
                    return false;
            }
            return true;
        }

        private static string FormatFullTerminalLine(
            string message,
            string data,
            string runId,
            string sessionKey,
            double startedAt)
        {
            return
                "[00:00:00.000] [ERROR] [Network/UnitMovementAuthorityObserver] " +
                $"[UAS-MOVE-AUTH] {message} | " +
                $"runId={runId ?? "unavailable"}, " +
                $"sharedSessionKey={sessionKey ?? "unavailable"}, " +
                $"startedAt={startedAt:F6}, {data}";
        }

        private static string NormalizeTerminalToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unavailable";
            string normalized = value.Replace(',', '_').Replace('\r', '_').Replace('\n', '_');
            return normalized.Length <= 64 ? normalized : normalized.Substring(0, 64);
        }

        internal static string ValidateTerminalPreflightForValidation(
            int manifestChunks,
            string payload)
        {
            if (manifestChunks < 0 || payload == null) return "Invalid";
            var records = new List<TerminalRecord>(manifestChunks + NonManifestTerminalLines);
            for (int index = 0; index < manifestChunks; index++)
            {
                records.Add(new TerminalRecord(
                    "coverage-manifest",
                    $"chunk={index + 1}/{manifestChunks}, entries={payload}"));
            }
            records.Add(new TerminalRecord("terminal-summary-core", "value=1"));
            records.Add(new TerminalRecord("terminal-summary-corridor", "value=1"));
            records.Add(new TerminalRecord("terminal-summary-spatial-commit", "value=1"));
            records.Add(new TerminalRecord("terminal-summary-spatial-repath", "value=1"));
            records.Add(new TerminalRecord("terminal-summary-replication", "value=1"));
            records.Add(new TerminalRecord(
                "END",
                "reason=fixture, role=host, verdict=EVIDENCE"));

            string previousRunId = _runId;
            string previousSessionKey = _sessionKey;
            double previousStartedAt = _startedAt;
            _runId = new string('r', 32);
            _sessionKey = new string('s', 64);
            _startedAt = 123456.123456d;
            bool valid = TryPreflightTerminalRecords(records, out int maximumBytes);
            _runId = previousRunId;
            _sessionKey = previousSessionKey;
            _startedAt = previousStartedAt;
            return $"valid={valid},lines={records.Count},maxBytes={maximumBytes}";
        }

        internal static string ValidateProductionTerminalRecordsForValidation()
        {
            Reset();
            _runId = new string('r', 32);
            _sessionKey = new string('s', 64);
            _startedAt = 123456.123456d;
            var chunks = new List<string>(MaximumManifestChunks);
            for (int index = 0; index < MaximumManifestChunks; index++)
                chunks.Add(new string('x', PayloadLimit));
            List<TerminalRecord> records = BuildTerminalRecords(
                chunks,
                coverageEntries: int.MaxValue,
                coverageSha: new string('a', 64),
                reason: new string('r', 64),
                failure: true);
            bool valid = TryPreflightTerminalRecords(records, out int maximumBytes);
            int lines = records.Count;
            Reset();
            bool reset = _runId == null
                && _sessionKey == null
                && _terminalLines == 0
                && _terminalPreflightFailures == 0;
            return $"valid={valid},lines={lines},maxBytes={maximumBytes},reset={reset}";
        }

        private static void Log(LogLevel level, string message, string data)
        {
            if (_lines >= MaximumNormalLines)
            {
                _dropped++;
                return;
            }
            _lines++;
            RuntimeLogger.Log(level, "Network", nameof(UnitMovementAuthorityObserver),
                $"[UAS-MOVE-AUTH] {message}",
                $"runId={_runId}, sharedSessionKey={_sessionKey}, startedAt={_startedAt:F6}, {data}");
        }

        private static void LogCausalFailure(string message, string data)
        {
            Log(
                _causalErrorEmitted ? LogLevel.Warn : LogLevel.Error,
                message,
                data);
            _causalErrorEmitted = true;
        }

        private static void LogTerminal(LogLevel level, string message, string data)
        {
            if (_terminalLines >= ReservedTerminalLines)
            {
                _terminalOverflow++;
                return;
            }
            _terminalLines++;
            _lines++;
            RuntimeLogger.Log(level, "Network", nameof(UnitMovementAuthorityObserver),
                $"[UAS-MOVE-AUTH] {message}",
                $"runId={_runId}, sharedSessionKey={_sessionKey}, startedAt={_startedAt:F6}, {data}");
        }

        private static double ReadServerTime()
            => _networkManager != null && _networkManager.IsListening
                ? _networkManager.ServerTime.Time
                : double.NaN;

        private static string CreateSharedSessionKey()
        {
            NetworkGameManager manager = UnityEngine.Object.FindFirstObjectByType<NetworkGameManager>();
            string lobbyId = manager?.CurrentLobby?.Id;
            return string.IsNullOrEmpty(lobbyId) ? "unavailable" : Sha256(lobbyId);
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash)
                    text.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static void Reset()
        {
            _curvePeak = _fallbackPeak = default;
            DirectionTypes.Clear();
            DirectionUnits.Clear();
            _centerCurveFrames = 0;
            _directionSample = 0;
            _directionBudgetReported = false;
            _directionLines = _directionSkipped = 0;
            Coverages.Clear();
            PhaseTransitions.Clear();
            LastPhases.Clear();
            ClientReplicatedSnapshots.Clear();
            ClientAttackEntryTrackers.Clear();
            ActiveHandoffFailureEpisodes.Clear();
            LastRecoverableSignatures.Clear();
            LastRecoverableFrames.Clear();
            _networkManager = null;
            _active = false;
            _isServer = false;
            _runId = null;
            _sessionKey = null;
            _startedAt = 0d;
            _lines = _dropped = _details = _frames = _align = _move = _noIntent = 0;
            _failureDetails = _failureEvidenceOverflow = 0;
            _endpoint = _targetPriority = _rejected = _invalid = _gateFailures = 0;
            _writerSelectionFailures = _clientWriteAttempts = 0;
            _attackWriterOwnershipConflicts = 0;
            _ignoredServerTargetEvents = 0;
            _deferredCombatTargetChanges = 0;
            _combatPresentationHandoffFailures = 0;
            _heldFrames = _stationaryWalkViolations = 0;
            _corridorFullSmooth = _corridorReducedSmooth = 0;
            _corridorFullDirect = _corridorReducedDirect = 0;
            _corridorStationaryAlignment = _corridorRepathRequired = 0;
            _corridorStartTileEgressFrames = 0;
            _centerCheckpointsReached = 0;
            _maximumCenterCheckpointErrorWorld = 0d;
            _chaseDirectSafeFrames = _chasePathFrames = 0;
            _cornerSpeedFrames = _cornerStarts = _cornerCompleted = 0;
            _cornerNormalStops = _cornerSafetyStops = _cornerSummaryLines = 0;
            _cornerMinSpeed = double.PositiveInfinity;
            _cornerMinRatio = 1d;
            _cornerMaxCross = _cornerMaxTurnExcess = _cornerSeconds = 0d;
            _spatialTransitionsPlanned = _spatialTransitionsCommitted = 0;
            _spatialNoTransitionPlans = _spatialTerminalContacts = 0;
            _spatialPreflightFailures = _spatialCommitFailures = 0;
            _spatialRecoveries = 0;
            _spatialFinalRecoverableRepaths = 0;
            _spatialRepeatedRecoverableRepaths = 0;
            _spatialFatalPreflights = 0;
            _spatialStagesForCommit = 0;
            _spatialCleanupAfterRecoverable = 0;
            _spatialSameFrameRetries = 0;
            _spatialStageDivergences = 0;
            _terminalLines = _terminalOverflow = _manifestPreflightFailures = 0;
            _terminalPreflightFailures = 0;
            _clientReplicatedSamples = _clientReplicatedInvalid = 0;
            _clientReplicatedDuplicates = _clientRevisionZero = 0;
            _clientRevisionRegressions = _clientSameRevisionConflicts = 0;
            _clientInvalidPhase = _clientUnitUninitialized = _clientInvalidScope = 0;
            _clientPresentationFrames = _clientAttackPresentationStarts = 0;
            _clientAttackEntryTransportGaps = 0;
            _clientAttackEntryOrderViolations = 0;
            _clientIdentityConflicts = _clientScopeRegressions = 0;
            _lifecycleIdentityConflicts = 0;
            _adapterFailures = 0;
            _gameplayEnded = false;
            _postGameAdapterFailuresSuppressed = 0;
            _causalErrorEmitted = false;
        }

        [Flags]
        private enum ClientReplicationIssue
        {
            None = 0,
            Duplicate = 1 << 0,
            RevisionZero = 1 << 1,
            RevisionRegression = 1 << 2,
            SameRevisionConflict = 1 << 3,
            InvalidPhase = 1 << 4,
            UnitUninitialized = 1 << 5,
            InvalidScope = 1 << 6,
            IdentityConflict = 1 << 7,
            ScopeRegression = 1 << 8
        }

        private enum LifecycleRetireResult
        {
            NotFound = 0,
            Retired = 1,
            IdentityConflictRetired = 2
        }

        private readonly struct TerminalRecord
        {
            internal readonly string Message;
            internal readonly string Data;

            internal TerminalRecord(string message, string data)
            {
                Message = message;
                Data = data;
            }
        }

        private readonly struct ServerPhaseSnapshot
        {
            internal readonly int UnitId;
            internal readonly UnitMovementPhase Phase;

            internal ServerPhaseSnapshot(int unitId, UnitMovementPhase phase)
            {
                UnitId = unitId;
                Phase = phase;
            }
        }

        private readonly struct ClientReplicatedSnapshot
        {
            internal readonly int UnitId;
            internal readonly UnitMovementPhase Phase;
            internal readonly ulong CommandRevision;
            internal readonly ulong SegmentRevision;
            internal readonly ulong SemanticRevision;

            internal ClientReplicatedSnapshot(
                int unitId,
                UnitMovementPhase phase,
                ulong commandRevision,
                ulong segmentRevision,
                ulong semanticRevision)
            {
                UnitId = unitId;
                Phase = phase;
                CommandRevision = commandRevision;
                SegmentRevision = segmentRevision;
                SemanticRevision = semanticRevision;
            }
        }

        private sealed class Coverage
        {
            internal int Frames;
            internal int Align;
            internal int Move;
            internal int NoIntent;
            internal int Endpoint;
            internal int TargetPriority;
        }
    }
}
#endif
