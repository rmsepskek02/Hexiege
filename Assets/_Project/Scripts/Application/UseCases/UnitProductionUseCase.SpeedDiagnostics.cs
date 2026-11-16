// ============================================================================
// UnitProductionUseCase.SpeedDiagnostics.cs
// 전술 지령 생산 속도 진단 [SKILL-PROD] ProdCompleteSample (Diag=SkillProd, DiagRev=1 — 개발 축, Plan S-15).
// 2026-11-16 지원 스킬 새 상태 Task 단계 A — 구현 단위 A-3. Plan 7절 A-L16 · A-L17 · A-L18.
//
// 무엇을 하나(초급자용 설명):
//   「생산 중에 쓰면 그 생산도 바로 빨라진다」 · 「끝난 뒤에는 원래 속도」를 눈 대신 숫자로 판정하려고,
//   생산 1건이 끝날 때 다음을 1줄로 남긴다(서버 · 싱글만 — 순수 클라이언트는 생산을 완료하지 않는다).
//     RequiredMs = 필요 시간(유닛 표 값, 배율과 무관)
//     ActualMs   = 생산 시작부터 진행이 100%에 닿기까지 실제로 흐른 시간
//     BuffMs     = 그 가운데 생산 배율이 1이 아니었던 시간
//     ExpectedMs = RequiredMs − (배율 − 1) × BuffMs  (안 A의 기대값 — ActualMs가 이것과 프레임 하나 정도 차이면 정상)
//     SpawnWaitMs = 100%가 된 뒤 빈 칸이 없어 유닛 등장을 기다린 시간(있으면 ActualMs와 따로 본다)
//   매 생산마다 남기면 줄이 넘치므로 표본만 남긴다(LogRules 1.14-8):
//     - 효과를 받은 생산: 건물 · 발동 순번마다 처음 3건.
//     - 효과가 끝난 뒤: 건물마다 효과를 받았던 생산 뒤 첫 「배율 없는」 생산 1건(Phase=AfterBuff — A-L18).
//
// 지키는 것: [Conditional] 두 개(릴리스에서 통째로 사라짐) · 예외를 밖으로 던지지 않음 · 생산 상태를 바꾸지 않음(읽기만).
//
// Application 레이어.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class UnitProductionUseCase
    {
        // 건물 하나의 진행 시간 기록(진단 전용).
        private sealed class ProdSpeedTrack
        {
            public float RealSec;        // 이번 생산 시작 뒤 흐른 실제 시간
            public float BuffSec;        // 그 가운데 배율 ≠ 1 이던 시간
            public float BuffMul = 1f;   // 마지막으로 본 배율(≠ 1)
            public int BuffSeq;          // 마지막으로 본 발동 순번
            public float ReadySec = -1f; // 진행이 100%에 닿은 순간의 RealSec(아직이면 −1)
            public int LoggedSeq;        // 표본을 세는 발동 순번
            public int LoggedCount;      // 그 순번으로 남긴 표본 수
            public bool AfterPending;    // 효과를 받은 생산이 있었고 「끝난 뒤 첫 생산」 표본을 기다림
            public int AfterSeq;         // 기다리는 표본의 앞 발동 순번
        }

        private const int DiagProdSamplesPerSeq = 3;
        private readonly Dictionary<int, ProdSpeedTrack> _diagProdTracks = new Dictionary<int, ProdSpeedTrack>();

        private ProdSpeedTrack DiagProdTrack(int barracksId)
        {
            if (!_diagProdTracks.TryGetValue(barracksId, out ProdSpeedTrack t))
            {
                t = new ProdSpeedTrack();
                _diagProdTracks[barracksId] = t;
            }
            return t;
        }

        // 진행을 더하기 "전" — 진행 0이면 새 생산이 시작된 것이므로 기록을 처음부터 센다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagProductionTickBegin(ProductionState state)
        {
            try
            {
                if (_speedSource == null || state.ElapsedTime > 0f) return;
                ProdSpeedTrack t = DiagProdTrack(state.BarracksId);
                t.RealSec = 0f;
                t.BuffSec = 0f;
                t.BuffMul = 1f;
                t.BuffSeq = 0;
                t.ReadySec = -1f;
            }
            catch { /* 진단 전용 */ }
        }

        // 진행을 더한 "뒤" — 실제 시간 · 배율 시간을 쌓고, 100%에 처음 닿은 순간을 적는다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagProductionTickEnd(ProductionState state, float deltaTime, float speedMul)
        {
            try
            {
                if (_speedSource == null) return;
                ProdSpeedTrack t = DiagProdTrack(state.BarracksId);
                t.RealSec += deltaTime;
                if (speedMul != 1f)
                {
                    t.BuffSec += deltaTime;
                    t.BuffMul = speedMul;
                    t.BuffSeq = _speedSource.GetProductionSpeedSequence(state.Team);
                }
                if (t.ReadySec < 0f && state.ElapsedTime >= state.RequiredTime) t.ReadySec = t.RealSec;
            }
            catch { /* 진단 전용 */ }
        }

        // 생산 1건 완료(유닛 등장 직후, 상태 초기화 전).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagProductionCompleted(ProductionState state, UnitType type)
        {
            try
            {
                if (_speedSource == null) return;
                if (!_diagProdTracks.TryGetValue(state.BarracksId, out ProdSpeedTrack t)) return;

                string phase;
                int seq;
                if (t.BuffSec > 0f)
                {
                    if (t.LoggedSeq != t.BuffSeq) { t.LoggedSeq = t.BuffSeq; t.LoggedCount = 0; }
                    t.AfterPending = true;
                    t.AfterSeq = t.BuffSeq;
                    if (t.LoggedCount >= DiagProdSamplesPerSeq) return;
                    t.LoggedCount++;
                    phase = "Buffed";
                    seq = t.BuffSeq;
                }
                else
                {
                    if (!t.AfterPending) return; // 효과와 무관한 평소 생산 — 줄을 남기지 않는다.
                    t.AfterPending = false;
                    phase = "AfterBuff";
                    seq = t.AfterSeq;
                }

                float ready = t.ReadySec >= 0f ? t.ReadySec : t.RealSec;
                float mul = t.BuffSec > 0f ? t.BuffMul : 1f;
                float expected = state.RequiredTime - (mul - 1f) * t.BuffSec;
                string role = !NetworkContext.IsNetworkActive ? "Single" : (NetworkContext.IsNetworkServer ? "Host" : "Client");
                GameLog.Dev.Info("Skill", nameof(UnitProductionUseCase), "[SKILL-PROD] ProdCompleteSample",
                    System.FormattableString.Invariant(
                        $"Diag=SkillProd, DiagRev=1, Event=ProdCompleteSample, Role={role}, Phase={phase}, Seq={seq}, Team={state.Team}, BarracksId={state.BarracksId}, UnitType={type}, RequiredMs={Mathf.RoundToInt(state.RequiredTime * 1000f)}, ActualMs={Mathf.RoundToInt(ready * 1000f)}, BuffMs={Mathf.RoundToInt(t.BuffSec * 1000f)}, MagMilli={Mathf.RoundToInt(mul * 1000f)}, ExpectedMs={Mathf.RoundToInt(expected * 1000f)}, SpawnWaitMs={Mathf.RoundToInt((t.RealSec - ready) * 1000f)}"));
            }
            catch { /* 진단 전용 — 생산 완료를 막지 않는다 */ }
        }
    }
}
