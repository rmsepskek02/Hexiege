// ============================================================================
// SkillActivationUseCase.LineStrike.cs
// 공습(직선 연속 폭격 — SkillMechanicType.LineStrike) 의 예약 · 서버 권위 처리 · 정리 · 진단 로그.
// (2026-11-17 지원 스킬 2-L 공습 Task — _Tasks/2026-11-17/09_12_support-skill-air-strike, Plan L-1 · P-3 · P-9 · P-12 · P-13 · U-12 · U-14)
//
// 흐름(초급자용 설명):
//   ① Activate(서버)가 공습이면 방향을 재검증 · 정규화한 뒤 실행기를 부른다 → 실행기가 「직선 연속 폭격 예약」 다리를 부른다.
//   ② 다리(ScheduleLineStrikeBridge)가 폭격 계획 값을 검사하고, 맵 안 유효 길이(U-14)를 한 번 계산해 「시전 상태」(LineStrikeCast)를
//      만든 뒤, 폭격 1회당 착탄 예약(PendingImpact) 1개씩 N개를 넣는다 — k번째 예약의 남은 시간 = 첫 지연 + k × 간격.
//      (포격의 착탄 예약 장치를 그대로 재사용 — Plan P-3. 그래서 서버 권위 틱 · 경기 종료 닫힘 · 처리 중 닫힘 규칙이 포격과 같다.)
//   ③ 권위 전용 틱(TickPendingImpacts — 싱글 GameBootstrapper.Update · 멀티 서버 NetworkCombatController.TickCombat)이
//      k번째 예약 시간이 되면 ProcessLineStrikeImpact를 부른다.
//        - k번째 구간이 맵 안 유효 길이 밖이면 피해 없이 건너뛴다(LineStrikeSkipped Reason=OutOfMap).
//        - 아니면 UnitCombatUseCase.ApplySkillLineStrike로 그 구간 안 적에게 피해 1회 — 이번 시전에서 이미 맞은 대상은 건너뛴다(U-12).
//   ④ N개를 모두 처리하면 시전 상태를 지우고 LineStrikeEnded Reason=Complete 1줄.
//      경기가 끝나면(ClosePendingImpacts) 남은 폭격을 버리고 Reason=GameEnd, 초기화(ClearAll)면 Reason=Cleared.
//
//   지킨 규칙:
//     - 쿨다운은 발동 순간 시작한다(스킬 규칙 3 — Activate ⑧. 폭격이 끝날 때가 아니다).
//     - 폭격 중 시전 건물이 무너져도 남은 폭격은 그대로 떨어진다(P-12 — 포격 결정 a와 같다. 건물 생존을 다시 보지 않는다).
//     - 판정 · 피해는 서버에서만 한다(스킬 규칙 25). 순수 클라이언트에는 예약이 없다(피해는 서버 HP 동기화로 받는다).
//
// 진단 로그(개발 축 · [Conditional] — 릴리스 빌드에서는 호출 · 문자열 조립이 모두 사라진다. 상태가 바뀔 때만 1줄):
//   머리 [SKILL-LINE] · Diag=SkillLine · DiagRev=1 — LineStrikeScheduled · LineStrikeHit · LineStrikeSkipped · LineStrikeEnded ·
//   LineStrikeRejected(Warn — 자산 값이 잘못돼 폭격을 예약하지 않음, 정상이면 0건). 길이 · 거리 · 시간은 밀리 정수.
//   잘못된 방향 거부는 기존 [SKILL-VFX] ActivationRejected(Reason=InvalidDirection — SkillActivationUseCase.cs ⑤-1)에 남는다.
//
// Application 레이어 — Domain/Application 의존. Unity.Netcode 직접 참조 없음.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public sealed partial class SkillActivationUseCase
    {
        // 공습 폭격 예약을 "다 됐다"로 보는 여유(초). TickPendingImpacts의 설명 참고 — float 오차로 한 틱 늦어지는 것을 막는다.
        private const float LineStrikeDueEpsilon = 1e-4f;

        // 맵 경계 사각형을 알려 주는 함수(조합 루트가 SetMapWorldBoundsProvider로 주입). null이면 맵 밖 잘라내기 없이 원래 길이를 쓴다.
        //   생성자가 아니라 설정자로 받는 이유는 다른 서비스와 같다(생성자를 바꾸면 Editor 회귀 fixture가 깨진다 · null = 안전한 기본값).
        private MapWorldBoundsProvider _mapWorldBounds;

        /// <summary>
        /// 맵 경계 사각형 함수를 주입한다(GameBootstrapper에서 맵마다 1회 — 2026-11-17 공습 Task L-1).
        /// 공습 직선 중 맵 밖 부분을 잘라내는 데 쓴다(U-14). 주입하지 않으면 잘라내지 않는다(진단 MapBounds=Missing).
        /// </summary>
        /// <param name="provider">지금 맵의 경계(HexMetrics.ComputeMapWorldBounds와 같은 값)를 돌려주는 함수.</param>
        public void SetMapWorldBoundsProvider(MapWorldBoundsProvider provider)
        {
            _mapWorldBounds = provider;
        }

        // 공습 1번 시전의 상태(발동 순번으로 찾는다). 직선 모양과 「이미 맞은 대상」 기록 · 진행 계수를 한 곳에 둔다.
        private sealed class LineStrikeCast
        {
            public int Sequence;               // 발동 순번(= 예약의 Sequence)
            public TeamId Team;                // 시전 팀
            public int BuildingId;             // 시전 건물 Id(피해 출처 표기 — 폭격 중 무너져도 그대로 쓴다, P-12)
            public int Slot;                   // 시전 슬롯
            public Vector3 Start;              // 시작점(도메인 월드, Y = 0)
            public Vector3 Direction;          // 정규화 방향(Y = 0)
            public float Length;               // 원래 길이 L(구간은 이 길이로 나눈다 — P-13)
            public float HalfWidth;            // 반폭
            public float EffectiveLength;      // 맵 안 유효 길이(U-14)
            public int Count;                  // 폭격 횟수
            public int RawDamage;              // 1회 원본 피해
            public readonly HashSet<int> HitUnits = new HashSet<int>();     // 이번 시전에서 맞은 유닛 Id(U-12)
            public readonly HashSet<int> HitBuildings = new HashSet<int>(); // 이번 시전에서 맞은 건물 Id(유닛과 따로 — Id가 겹칠 수 있다)
            public int Done;                   // 판정한 폭격 수
            public int Skipped;                // 맵 밖이라 건너뛴 폭격 수
            public int AlreadyHitSkipped;      // 구간 안이지만 이미 맞아 건너뛴 대상 수의 합
            public bool Processing;            // 지금 이 시전의 폭격 피해를 주는 중인가(그 사이 경기 종료가 오면 마감을 미룬다)
            public string PendingCloseReason;  // 처리 중에 닫힘이 왔으면 그 이유(처리를 마친 뒤 마감)
        }

        private readonly Dictionary<int, LineStrikeCast> _lineStrikeCasts = new Dictionary<int, LineStrikeCast>(2);
        private readonly List<LineStrikeCast> _lineStrikeCloseBuffer = new List<LineStrikeCast>(2);

        /// <summary> 아직 끝나지 않은 공습 시전 수(확인 · 검증용). </summary>
        public int ActiveLineStrikeCount => _lineStrikeCasts.Count;

        /// <summary> 가장 최근에 끝난 공습 시전의 집계(확인 · 검증용 — 끝난 시전이 없으면 Sequence 0). </summary>
        public LineStrikeEndedRecord LastLineStrikeEnded { get; private set; }

        /// <summary>
        /// 공습 실행기 → 이 UseCase 다리. 폭격 계획을 검사하고 맵 안 유효 길이를 계산해 착탄 예약 N개를 넣는다.
        /// 계획 값이 잘못됐거나 예약이 닫혔으면 아무것도 예약하지 않고 진단 Warn 1줄을 남긴다(쿨다운은 이미 확정 흐름이라 그대로 돈다 —
        /// 타입 C 「BuffNoEffect」와 같은 처리. 잘못된 자산은 정상이면 0건).
        /// </summary>
        private void ScheduleLineStrikeBridge(TeamId team, Vector3 start, Vector3 direction, LineStrikePlan plan,
                                              int sequence, int slot, int buildingId)
        {
            string reason = null;
            if (_impactsClosed) reason = "Closed";
            else if (!(plan.Length > 0f)) reason = "LengthNotPositive";
            else if (!(plan.Width > 0f)) reason = "WidthNotPositive";
            else if (plan.StrikeCount < 1) reason = "StrikeCountZero";
            else if (plan.RawDamage <= 0) reason = "DamageNotPositive";
            else if (!LineStrikeWorld.TryNormalizeDirection(direction, out direction)) reason = "DirectionInvalid";
            if (reason != null)
            {
                LogLineStrikeRejected(team, sequence, slot, reason);
                return;
            }

            Vector3 s = new Vector3(start.x, 0f, start.z);
            float effective = LineStrikeWorld.ComputeEffectiveLength(s, direction, plan.Length, _mapWorldBounds, out bool boundsKnown);
            var cast = new LineStrikeCast
            {
                Sequence = sequence,
                Team = team,
                BuildingId = buildingId,
                Slot = slot,
                Start = s,
                Direction = direction,
                Length = plan.Length,
                HalfWidth = plan.HalfWidth,
                EffectiveLength = effective,
                Count = plan.StrikeCount,
                RawDamage = plan.RawDamage
            };
            _lineStrikeCasts[sequence] = cast;

            // 폭격 1회당 예약 1개 — k번째 시각 = 첫 지연 + k × 간격(첫 지연 · 간격은 SkillData가 음수 · NaN을 0으로 고친 값).
            float first = plan.FirstDelay > 0f ? plan.FirstDelay : 0f;
            float interval = plan.Interval > 0f ? plan.Interval : 0f;
            for (int k = 0; k < plan.StrikeCount; k++)
            {
                float delay = first + k * interval;
                _pendingImpacts.Add(new PendingImpact
                {
                    Team = team,
                    Center = s,
                    Radius = 0f,
                    RawDamage = plan.RawDamage,
                    SourceBuildingId = buildingId,
                    Sequence = sequence,
                    Remaining = delay,
                    Delay = delay,
                    IsLineStrike = true,
                    StrikeIndex = k
                });
            }
            // [진단 L-L9 · L-L25] 예약 1줄 — 시작점 · 방향 · 길이 · 유효 길이 · 폭 · 횟수 · 지연 · 간격.
            LogLineStrikeScheduled(cast, first, interval, boundsKnown);
        }

        /// <summary>
        /// (권위 전용 틱에서) 다 된 공습 폭격 1회를 처리한다 — 맵 밖 구간이면 건너뛰고, 아니면 그 구간 안 적에게 피해 1회(시전당 1회).
        /// </summary>
        private void ProcessLineStrikeImpact(PendingImpact p)
        {
            // 시전 상태가 없으면(초기화 · 경기 종료로 이미 마감) 아무것도 하지 않는다.
            if (!_lineStrikeCasts.TryGetValue(p.Sequence, out LineStrikeCast cast)) return;

            int index = p.StrikeIndex;
            LineStrikeGeometry.GetSegmentBounds(index, cast.Count, cast.Length, out float segFrom, out float segTo);
            if (!LineStrikeGeometry.IsStrikeInRange(index, cast.Count, cast.Length, cast.EffectiveLength))
            {
                cast.Skipped++;
                // [진단 L-L10 · L-L25] 맵 밖 구간이라 건너뛴 폭격 1줄(피해 0).
                LogLineStrikeSkipped(cast, p, segFrom, segTo, "OutOfMap");
                FinishLineStrikeIfDone(cast);
                return;
            }

            LineStrikeHitResult r = default;
            cast.Processing = true;
            try
            {
                if (_combat != null)
                {
                    r = _combat.ApplySkillLineStrike(cast.Team, cast.Start, cast.Direction, cast.Length, cast.HalfWidth,
                        cast.EffectiveLength, index, cast.Count, cast.RawDamage, cast.BuildingId, cast.HitUnits, cast.HitBuildings);
                }
            }
            finally
            {
                cast.Processing = false;
            }
            cast.Done++;
            cast.AlreadyHitSkipped += r.AlreadyHitSkipped;
            // [진단 L-L10 · L-L11] 폭격 1회 결과 1줄.
            LogLineStrikeHit(cast, p, segFrom, segTo, r);
            FinishLineStrikeIfDone(cast);
        }

        // 마감 조건을 본다 — 처리 중에 닫힘이 왔으면 그 이유로, 아니면 N개를 모두 처리했을 때 Complete로 마감한다.
        private void FinishLineStrikeIfDone(LineStrikeCast cast)
        {
            if (cast.PendingCloseReason != null) EndLineStrike(cast, cast.PendingCloseReason);
            else if (cast.Done + cast.Skipped >= cast.Count) EndLineStrike(cast, "Complete");
        }

        // 시전 1개를 마감한다 — 집계를 남기고 상태를 지운다. 남은 폭격 수 = 버림(Dropped).
        private void EndLineStrike(LineStrikeCast cast, string reason)
        {
            if (!_lineStrikeCasts.Remove(cast.Sequence)) return; // 이미 마감됨(멱등)
            int dropped = cast.Count - cast.Done - cast.Skipped;
            if (dropped < 0) dropped = 0;
            LastLineStrikeEnded = new LineStrikeEndedRecord(cast.Sequence, cast.Done, cast.Skipped, dropped,
                                                            cast.HitUnits.Count, cast.HitBuildings.Count, reason);
            // [진단 L-L11 · L-L12] 시전 마감 1줄.
            LogLineStrikeEnded(cast, reason, dropped);
        }

        // 아직 끝나지 않은 시전을 모두 마감한다(경기 종료 · 초기화). 처리 중인 시전은 그 폭격을 마친 뒤 마감한다.
        private void CloseLineStrikes(string reason)
        {
            if (_lineStrikeCasts.Count == 0) return;
            _lineStrikeCloseBuffer.Clear();
            foreach (var kv in _lineStrikeCasts) _lineStrikeCloseBuffer.Add(kv.Value); // 순회 중 제거를 피하려 먼저 옮긴다
            for (int i = 0; i < _lineStrikeCloseBuffer.Count; i++)
            {
                LineStrikeCast cast = _lineStrikeCloseBuffer[i];
                if (cast.Processing) cast.PendingCloseReason = reason;
                else EndLineStrike(cast, reason);
            }
            _lineStrikeCloseBuffer.Clear();
        }

        // ====================================================================
        // 진단 로그 [SKILL-LINE] — 개발 축 · [Conditional]
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineStrikeScheduled(LineStrikeCast c, float firstDelay, float interval, bool boundsKnown)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-LINE] LineStrikeScheduled",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineStrikeScheduled, Role={DiagRole()}, Seq={c.Sequence}, Team={c.Team}, BuildingId={c.BuildingId}, Slot={c.Slot}, StartXmm={DiagMilli(c.Start.x)}, StartZmm={DiagMilli(c.Start.z)}, DirXMilli={DiagMilli(c.Direction.x)}, DirZMilli={DiagMilli(c.Direction.z)}, LengthMilli={DiagMilli(c.Length)}, EffLengthMilli={DiagMilli(c.EffectiveLength)}, WidthMilli={DiagMilli(c.HalfWidth * 2f)}, Strikes={c.Count}, FirstDelayMs={DiagMilli(firstDelay)}, IntervalMs={DiagMilli(interval)}, RawDamage={c.RawDamage}, MapBounds={(boundsKnown ? "Known" : "Missing")}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogLineStrikeHit(LineStrikeCast c, PendingImpact p, float segFrom, float segTo, LineStrikeHitResult r)
        {
            BuildingData caster = _buildingPlacement?.GetBuilding(c.BuildingId);
            bool casterAlive = caster != null && caster.IsAlive;
            float judgeTo = segTo < c.EffectiveLength ? segTo : c.EffectiveLength;
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-LINE] LineStrikeHit",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineStrikeHit, Role={DiagRole()}, Seq={c.Sequence}, Team={c.Team}, Index={p.StrikeIndex + 1}, Strikes={c.Count}, ElapsedMs={DiagMilli(p.Delay - p.Remaining)}, SegFromMilli={DiagMilli(segFrom)}, SegToMilli={DiagMilli(segTo)}, JudgeToMilli={DiagMilli(judgeTo)}, Units={r.Units}, Buildings={r.Buildings}, AlreadyHitSkipped={r.AlreadyHitSkipped}, OutOfMapSkipped={r.OutOfMapSkipped}, AlliesInSegment={r.AlliesInSegment}, UnitKills={r.UnitKills}, BuildingKills={r.BuildingKills}, CasterAlive={casterAlive}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineStrikeSkipped(LineStrikeCast c, PendingImpact p, float segFrom, float segTo, string reason)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-LINE] LineStrikeSkipped",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineStrikeSkipped, Role={DiagRole()}, Seq={c.Sequence}, Team={c.Team}, Index={p.StrikeIndex + 1}, Strikes={c.Count}, ElapsedMs={DiagMilli(p.Delay - p.Remaining)}, SegFromMilli={DiagMilli(segFrom)}, SegToMilli={DiagMilli(segTo)}, EffLengthMilli={DiagMilli(c.EffectiveLength)}, Reason={reason}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineStrikeEnded(LineStrikeCast c, string reason, int dropped)
        {
            int unique = c.HitUnits.Count + c.HitBuildings.Count;
            GameLog.Dev.Info("Skill", nameof(SkillActivationUseCase), "[SKILL-LINE] LineStrikeEnded",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineStrikeEnded, Role={DiagRole()}, Seq={c.Sequence}, Team={c.Team}, Reason={reason}, Strikes={c.Count}, Done={c.Done}, Skipped={c.Skipped}, Dropped={dropped}, UniqueTargets={unique}, UniqueUnits={c.HitUnits.Count}, UniqueBuildings={c.HitBuildings.Count}, AlreadyHitSkipped={c.AlreadyHitSkipped}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineStrikeRejected(TeamId team, int sequence, int slot, string reason)
        {
            GameLog.Dev.Warn("Skill", nameof(SkillActivationUseCase), "[SKILL-LINE] LineStrikeRejected",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineStrikeRejected, Role={DiagRole()}, Seq={sequence}, Team={team}, Slot={slot}, Reason={reason}, CooldownStarted=True"));
        }
    }
}
