// ============================================================================
// ProductionSpeedMirror.cs
// 멀티 「순수 클라이언트」 화면의 생산 진행 막대를 서버와 같은 빠르기로 채우기 위한 로컬 거울(전술 지령 생산 속도).
// 2026-11-16 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 A — 구현 단위 A-3(Plan S-9).
//
// 왜 필요한가(초급자용 설명):
//   생산 완료(유닛 등장)는 서버가 정한다. 순수 클라이언트는 진행 막대만 혼자 채우며 흉내 낸다(UnitProductionUseCase.TickProgressOnly).
//   서버가 전술 지령으로 생산을 1.5배 빨리 하는데 클라이언트 막대가 보통 속도로 차면, 막대가 반쯤 찼을 때 유닛이 나오고
//   다음 생산 막대가 0%로 튄다(동작에는 문제가 없고 화면만 어긋남 — Research 6-4).
//   새 RPC를 만들지 않고(스키마 9 유지) 이미 오는 「스킬 발동 알림」을 받은 순간, 자기 빌드의 스킬 데이터에서
//   생산 배율 · 지속을 찾아 「팀 · 배율 · 남은 시간」을 기록한다(1차 강화 D2와 같은 방식). 전송 지연만큼 늦게 시작 · 끝난다.
//
// 이 거울은 화면 표시용이다 — 생산 완료 · 큐 · 골드에는 아무 영향이 없다.
//
// Application 레이어 — Domain(TeamId) 의존. 틱은 UnitProductionUseCase.TickProgressOnly(순수 클라 전용)가 부른다.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 순수 클라이언트용 팀 생산 속도 거울. 팀마다 배율 하나 · 남은 시간 하나(같은 팀에 다시 걸리면 교체 — 서버와 같은 규칙).
    /// </summary>
    public sealed class ProductionSpeedMirror
    {
        // 팀 수가 둘(Blue · Red)뿐이라 작은 배열로 둔다(정수 팀 값을 칸 번호로). 범위를 벗어난 팀은 무시한다.
        private const int TeamSlots = 4;
        private readonly float[] _multiplier = new float[TeamSlots];
        private readonly float[] _remaining = new float[TeamSlots];
        private readonly float[] _elapsed = new float[TeamSlots];     // 진단 ActualMs
        private readonly float[] _duration = new float[TeamSlots];    // 진단 DurationMs
        private readonly int[] _sequence = new int[TeamSlots];        // 진단 Seq
        private int _activeCount;
        private bool _closed;

        /// <summary> 거울 생성(맵마다 UnitProductionUseCase와 함께 새로 만들어진다). </summary>
        public ProductionSpeedMirror()
        {
            for (int i = 0; i < TeamSlots; i++) _multiplier[i] = 1f;
        }

        /// <summary>
        /// 발동 알림을 받은 순간 기록한다(같은 팀이면 교체). 닫힌 뒤 · 잘못된 값이면 무시한다.
        /// </summary>
        /// <param name="team">생산 속도를 받는 팀.</param>
        /// <param name="multiplier">생산 속도 배율(0 이상 · 유한).</param>
        /// <param name="duration">지속(초, 0보다 크고 유한).</param>
        /// <param name="sequence">발동 순번(진단용).</param>
        /// <param name="casterTeam">시전 팀(진단용).</param>
        /// <param name="slot">슬롯(진단용).</param>
        /// <param name="buildingId">시전 건물(진단용).</param>
        public void Apply(TeamId team, float multiplier, float duration, int sequence, TeamId casterTeam, int slot, int buildingId)
        {
            if (_closed) return;
            int i = (int)team;
            if (i < 0 || i >= TeamSlots) return;
            if (!(duration > 0f) || float.IsInfinity(duration) || !(multiplier >= 0f) || float.IsInfinity(multiplier)) return;

            bool refreshed = _remaining[i] > 0f;
            if (refreshed) DiagEnded(team, "Replaced");
            else _activeCount++;

            _multiplier[i] = multiplier;
            _remaining[i] = duration;
            _duration[i] = duration;
            _elapsed[i] = 0f;
            _sequence[i] = sequence;
            DiagApplied(team, casterTeam, slot, buildingId, refreshed);
        }

        /// <summary> 지금 그 팀의 거울 배율(없으면 정확히 1). </summary>
        /// <param name="team">생산 건물의 팀.</param>
        public float GetMultiplier(TeamId team)
        {
            if (_activeCount == 0) return 1f;
            int i = (int)team;
            if (i < 0 || i >= TeamSlots || !(_remaining[i] > 0f)) return 1f;
            return _multiplier[i];
        }

        /// <summary>
        /// 남은 시간을 줄인다(순수 클라 프레임마다 — UnitProductionUseCase.TickProgressOnly 끝에서). 다 되면 배율 1로 돌아온다.
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void Tick(float dt)
        {
            if (_activeCount == 0 || !(dt > 0f)) return;
            for (int i = 0; i < TeamSlots; i++)
            {
                if (!(_remaining[i] > 0f)) continue;
                _elapsed[i] += dt;
                _remaining[i] -= dt;
                if (_remaining[i] > 0f) continue;
                DiagEnded((TeamId)i, "Expire");
                Clear(i);
            }
        }

        /// <summary> 경기 종료 — 남은 거울을 모두 끝내고(진단 Reason=GameEnd) 이후 기록을 받지 않는다(멱등). </summary>
        public void Close()
        {
            if (_closed) return;
            _closed = true;
            for (int i = 0; i < TeamSlots; i++)
            {
                if (!(_remaining[i] > 0f)) continue;
                DiagEnded((TeamId)i, "GameEnd");
                Clear(i);
            }
        }

        private void Clear(int i)
        {
            _remaining[i] = 0f;
            _multiplier[i] = 1f;
            _activeCount--;
            if (_activeCount < 0) _activeCount = 0;
        }

        // ── 진단 [SKILL-PROD] ProdMirrorApplied / ProdMirrorEnded (Diag=SkillProd, DiagRev=1 — 개발 축, Plan 7절 A-L19) ──
        //   순수 클라이언트에서만 불린다(Role=Client). 같은 Seq의 Host ProdSpeedApplied와 팀 · 배율을 대조한다.

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagApplied(TeamId team, TeamId casterTeam, int slot, int buildingId, bool refreshed)
        {
            try
            {
                int i = (int)team;
                GameLog.Dev.Info("Skill", nameof(ProductionSpeedMirror), "[SKILL-PROD] ProdMirrorApplied",
                    System.FormattableString.Invariant(
                        $"Diag=SkillProd, DiagRev=1, Event=ProdMirrorApplied, Role={DiagRole()}, Seq={_sequence[i]}, Team={casterTeam}, TargetTeam={team}, Slot={slot}, BuildingId={buildingId}, MagMilli={Milli(_multiplier[i])}, DurationMs={Milli(_duration[i])}, Refreshed={refreshed}"));
            }
            catch { /* 진단 전용 */ }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagEnded(TeamId team, string reason)
        {
            try
            {
                int i = (int)team;
                GameLog.Dev.Info("Skill", nameof(ProductionSpeedMirror), "[SKILL-PROD] ProdMirrorEnded",
                    System.FormattableString.Invariant(
                        $"Diag=SkillProd, DiagRev=1, Event=ProdMirrorEnded, Role={DiagRole()}, Seq={_sequence[i]}, TargetTeam={team}, Reason={reason}, ActualMs={Milli(_elapsed[i])}, DurationMs={Milli(_duration[i])}, MagMilli={Milli(_multiplier[i])}"));
            }
            catch { /* 진단 전용 */ }
        }

        private static string DiagRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        private static int Milli(float value) => Mathf.RoundToInt(value * 1000f);
    }
}
