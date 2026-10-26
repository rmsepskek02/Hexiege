using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using UnityEditor;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        private const string RefactoringGeneration = "refactor-verify-v1";
        private static string _refactoringRunId;
        private static int _refactoringCases;
        private static int _refactoringPassed;
        private static int _refactoringFailures;
        private static bool _refactoringVerificationRunning;

        /// <summary>
        /// 일반 조작으로 만들기 어려운 경계 조건을 실제 구현에 전달하는 집중 검사다.
        /// 실행 중인 경기에는 전역 이벤트를 보내지 않으며, 임시 파일도 운영 로그와 분리한다.
        /// 각 결과는 Console에서 같은 표식을 복사해 전달할 수 있도록 한 줄씩 남긴다.
        /// </summary>
        [MenuItem("Hexiege/Combat/Diagnostics/Run Refactoring Verification")]
        public static void RunRefactoringVerification()
        {
            if (_refactoringVerificationRunning) return;
            BeginRefactoringVerification("Offline");
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode
                    || NetworkContext.IsNetworkActive
                    || GameServicesLocator.Current != null
                    || (Unity.Netcode.NetworkManager.Singleton != null
                        && Unity.Netcode.NetworkManager.Singleton.IsListening))
                {
                    RefactoringReceipt("ENVIRONMENT", "NOT_RUN", "StopPlayAndNetworkBeforeOfflineChecks");
                    return;
                }

                RunRefactoringCase("EDITOR-FIFO", ValidateRefactoringReservationLifecycle);
                RunRefactoringCase("EDITOR-LOGCAT", ValidateRefactoringLogcatNames);
                RunRefactoringCase("EDITOR-ROOT-PARSER", ValidateRefactoringRootPhaseParser);
                RunRefactoringCase("EDITOR-BUILDING-REMOVAL", ValidateB3BuildingRemovalRepathLifecycle);
                RunRefactoringCase("EDITOR-BLOOM-NETWORK", ValidateBloomHoTWriterAndNetwork);
                RunRefactoringRuntimeOfflineCases();
            }
            catch (Exception exception)
            {
                // 검사 등록이나 공통 준비에서 실패해도 일부 성공만으로 전체 PASS를 남기지 않는다.
                _refactoringFailures++;
                RefactoringReceipt("HARNESS", "FAIL", exception.GetType().Name + ":" + exception.Message);
            }
            finally
            {
                EndRefactoringVerification("Offline");
            }
        }

        private static void BeginRefactoringVerification(string mode)
        {
            if (_refactoringVerificationRunning)
                throw new InvalidOperationException("A refactoring verification run is already active.");
            _refactoringVerificationRunning = true;
            _refactoringRunId = Guid.NewGuid().ToString("N");
            _refactoringCases = 0;
            _refactoringPassed = 0;
            _refactoringFailures = 0;
            RefactoringReceipt("BEGIN", "STARTED", mode);
        }

        private static void EndRefactoringVerification(string mode)
        {
            string status = _refactoringFailures > 0 ? "FAIL"
                : _refactoringCases == 0 ? "NOT_RUN" : "PASS";
            RefactoringReceipt("END", status,
                $"{mode};Cases:{_refactoringCases};Passed:{_refactoringPassed};Failures:{_refactoringFailures}");
            _refactoringVerificationRunning = false;
        }

        /// <summary>한 검사 실패로 다음 검사를 생략하지 않고 예외 종류와 짧은 이유를 기록한다.</summary>
        private static void RunRefactoringCase(string caseId, Action validation)
        {
            _refactoringCases++;
            try
            {
                validation();
                _refactoringPassed++;
                RefactoringReceipt(caseId, "PASS", "AssertionsCompleted");
            }
            catch (Exception exception)
            {
                _refactoringFailures++;
                Exception cause = exception is TargetInvocationException && exception.InnerException != null
                    ? exception.InnerException : exception;
                RefactoringReceipt(caseId, "FAIL", cause.GetType().Name + ":" + cause.Message);
            }
        }

        /// <summary>
        /// 기존 GameLog 경로를 사용하므로 별도 파일 세션이나 sink를 만들지 않는다.
        /// 긴 예외 경로가 한 줄 예산을 넘기지 않게 잘라내고 데이터 구분자도 치환한다.
        /// </summary>
        private static void RefactoringReceipt(string caseId, string status, string detail)
        {
            string safe = (detail ?? "None").Replace('\r', ' ').Replace('\n', ' ').Replace(',', ';');
            if (safe.Length > 120) safe = safe.Substring(0, 120);
            string data = $"Generation={RefactoringGeneration}, Run={_refactoringRunId}, Case={caseId}, Status={status}, Detail={safe}";
            if (status == "FAIL")
                GameLog.Dev.Error("Combat", nameof(RunUnitActionSelfValidation), "[REFACTOR-VERIFY]", data);
            else
                GameLog.Dev.Info("Combat", nameof(RunUnitActionSelfValidation), "[REFACTOR-VERIFY]", data);
        }

        /// <summary>
        /// 공개 예약·완료·사망·퇴역 API를 실행한 뒤 내부 저장소는 읽기만 한다.
        /// 같은 ID의 새 예약 앞에 과거 순서가 남으면 상한 처리에서 새 예약이 먼저 사라지는
        /// 회귀를 실제로 만들고, 현재 살아 있는 예약 중 가장 오래된 것만 퇴출되는지 확인한다.
        /// </summary>
        private static void ValidateRefactoringReservationLifecycle()
        {
            var coordinator = new UnitAttackShadowCoordinator();
            var first = new AttackerInstanceId(99001UL);
            var oldest = new AttackerInstanceId(99002UL);
            var binding = new AttackTargetBinding(AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 99003));
            Require(UnitActionPoseSample.TryCreate(binding.Target,
                0d, 0d, 1d, 0d, 0.5d, 0d, false, 0d, 0d, out UnitActionPoseSample sample),
                "FIFO fixture pose must be valid.");

            LegacyAttackToken completed = RefactoringPrepareReservation(coordinator, first, binding, sample);
            double due = 10d + (double)0.2f;
            var dispatched = coordinator.DispatchLegacyImpact(completed, 0, sample, true, true, due);
            Require(dispatched.EvaluateStatus == UnitActionReducerStatus.Accepted,
                "FIFO fixture must dispatch an actual authorized impact.");
            var result = coordinator.CompleteLegacyImpact(completed, 0, AppliedDamage(1, 99, 0.5d, 0d), due + 0.001d);
            Require(result.CompletionStatus == UnitActionReducerStatus.Accepted && result.HasResult,
                "Completed impact must produce its canonical result.");
            RefactoringAssertReservationStorage(coordinator, 0);
            coordinator.Retire(first);

            RefactoringPrepareReservation(coordinator, first, binding, sample);
            coordinator.Retire(first);
            RefactoringAssertReservationStorage(coordinator, 0);

            LegacyAttackToken oldestToken = RefactoringPrepareReservation(coordinator, oldest, binding, sample);
            LegacyAttackToken reused = RefactoringPrepareReservation(coordinator, first, binding, sample);
            Require(reused.Value == 1UL, "Retired attacker fixture must actually reuse its first token key.");
            for (int index = 0; index < 511; index++)
                Require(coordinator.ScheduleLegacyAttack(oldest, binding, 1, 10d, out _).IsValid,
                    "FIFO fixture must schedule every reservation at the production API.");
            RefactoringAssertReservationStorage(coordinator, 512);
            Require(coordinator.DispatchLegacyImpact(oldestToken, 0, sample, true, true, due).Reason == "legacy-reservation-missing",
                "Capacity overflow must evict the oldest live reservation.");
            Require(coordinator.DispatchLegacyImpact(reused, 0, sample, true, true, due).EvaluateStatus == UnitActionReducerStatus.Accepted,
                "A retired key must not evict the newer reservation that reuses it.");
            coordinator.Retire(first);
            RefactoringAssertReservationStorage(coordinator, 511);
            coordinator.Clear();
            RefactoringAssertReservationStorage(coordinator, 0);
            Require(coordinator.ActiveAttackerCount == 0, "Clear must retire attacker entries too.");

            LegacyAttackToken cancelled = RefactoringPrepareReservation(coordinator, first, binding, sample);
            coordinator.MarkDead(first);
            Require(coordinator.TryConsumeLegacyImpactCancellation(cancelled, 0),
                "Actual death must cancel the undispatched impact.");
            RefactoringAssertReservationStorage(coordinator, 0);
            Require(coordinator.TryConsumeLegacyImpactCancellation(cancelled, 0),
                "Repeated cancelled wake must retain its tombstone decision.");
            coordinator.Clear();
            Require(!coordinator.TryConsumeLegacyImpactCancellation(cancelled, 0),
                "Clear must also discard cancellation history.");
        }

        private static LegacyAttackToken RefactoringPrepareReservation(UnitAttackShadowCoordinator coordinator,
            AttackerInstanceId attacker, AttackTargetBinding binding, UnitActionPoseSample sample)
        {
            var unit = new UnitData((int)attacker.Value, UnitType.SpearMan, TeamId.Blue,
                new HexCoord(0, 0), 100, 10, 0.5f, 1f)
            {
                AttackCooldown = 1f,
                HitFrameTimes = new[] { 0.2f }
            };
            var committed = coordinator.PrepareLegacyAttack(attacker, unit, binding, sample, true, true, 10d);
            Require(committed.CommittedNow, "FIFO fixture must commit a real attack sequence.");
            LegacyAttackToken token = coordinator.ScheduleLegacyAttack(attacker, binding, 1, 10d, out _);
            Require(token.IsValid, "FIFO fixture must create a live reservation.");
            return token;
        }

        private static void RefactoringAssertReservationStorage(UnitAttackShadowCoordinator coordinator, int expected)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var reservations = (IDictionary)typeof(UnitAttackShadowCoordinator).GetField("_reservations", flags).GetValue(coordinator);
            object order = typeof(UnitAttackShadowCoordinator).GetField("_reservationOrder", flags).GetValue(coordinator);
            int ordered = (int)order.GetType().GetProperty("Count").GetValue(order);
            Require(reservations.Count == expected && ordered == expected
                && coordinator.PendingLegacyAttackCount == expected,
                "Live reservation dictionary and FIFO must have the same expected count.");
            var unique = new HashSet<object>();
            foreach (object key in (IEnumerable)order)
            {
                Require(unique.Add(key) && reservations.Contains(key), "FIFO must contain each live reservation exactly once.");
                object reservation = reservations[key];
                object node = reservation.GetType().GetField("OrderNode").GetValue(reservation);
                Require(node != null && ReferenceEquals(node.GetType().GetProperty("List").GetValue(node), order)
                    && key.Equals(node.GetType().GetProperty("Value").GetValue(node)),
                    "Reservation must own its exact live FIFO node.");
            }
        }

        private static void ValidateRefactoringLogcatNames()
        {
            string folder = Path.Combine(Path.GetTempPath(), "HexiegeRefactoringVerification-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            Exception failure = null;
            try
            {
                MethodInfo method = typeof(Hexiege.EditorTools.LogcatCapture).GetMethod("MakeNonOverwritingPath", BindingFlags.Static | BindingFlags.NonPublic);
                Require(method != null, "Production Logcat path allocator must exist.");
                Func<string> allocate = () => (string)method.Invoke(null, new object[] { folder, "RuntimeLog_device", ".txt" });
                Require(allocate() == Path.Combine(folder, "RuntimeLog_device.txt"), "Empty directory must use the base log name.");
                for (int index = 1; index <= 998; index++)
                {
                    string name = index == 1 ? "RuntimeLog_device.txt" : $"RuntimeLog_device_{index}.txt";
                    File.WriteAllText(Path.Combine(folder, name), "keep-" + index);
                    if (index == 1)
                        Require(allocate() == Path.Combine(folder, "RuntimeLog_device_2.txt"), "Occupied base name must use suffix two.");
                }
                Require(allocate() == Path.Combine(folder, "RuntimeLog_device_999.txt"), "Last free name must remain available.");
                File.WriteAllText(Path.Combine(folder, "RuntimeLog_device_999.txt"), "keep-999");
                bool exhausted = false;
                try { allocate(); }
                catch (TargetInvocationException exception) when (exception.InnerException is IOException) { exhausted = true; }
                Require(exhausted, "Exhausted names must fail before returning an existing path.");
                Require(Directory.GetFiles(folder).Length == 999, "Name lookup must not add or remove files.");
                for (int index = 1; index <= 999; index++)
                {
                    string name = index == 1 ? "RuntimeLog_device.txt" : $"RuntimeLog_device_{index}.txt";
                    Require(File.ReadAllText(Path.Combine(folder, name)) == "keep-" + index,
                        "Exhaustion must preserve every original log payload.");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                // 이 검사에서 만든 고유 임시 폴더만 지운다. 프로젝트 로그 폴더는 열지 않는다.
                // 정리 오류가 나더라도 앞에서 발견한 원래 실패를 덮어쓰지 않는다.
                try { Directory.Delete(folder, true); }
                catch (Exception cleanup)
                {
                    failure = failure == null ? cleanup : new AggregateException(failure, cleanup);
                }
            }
            if (failure != null) throw failure;
        }

        private static void ValidateRefactoringRootPhaseParser()
        {
            MethodInfo parse = typeof(RunUnitRootPoseCrossAudit).GetMethod("ParseRotationReplicationEvidence", BindingFlags.Static | BindingFlags.NonPublic);
            Require(parse != null, "Actual Root Pose evidence parser must exist.");
            const string valid = "rotationEvidenceSchema=root-rotation-replication-v1, observation=server-checkpoint, unitId=9001, "
                + "networkObjectId=9001, serverTime=10.000, networkTick=300, movementPhase=NoIntent, commandRevision=1, "
                + "segmentRevision=1, semanticRevision=1, rootRotation=(0;0;0;1), "
                + "stableWindowStartServerTime=7.000, stableQualifiedServerTime=10.000";
            Require(parse.Invoke(null, new object[] { valid }) != null, "Valid phase evidence must still parse.");
            foreach (string phase in new[] { "2147483647", "-1", "Invalid" })
            {
                bool rejected = false;
                try { parse.Invoke(null, new object[] { valid.Replace("movementPhase=NoIntent", "movementPhase=" + phase) }); }
                catch (TargetInvocationException exception) when (exception.InnerException is FormatException) { rejected = true; }
                Require(rejected, "Undefined or Invalid movement phase evidence must be rejected by the actual parser.");
            }
        }
    }
}
