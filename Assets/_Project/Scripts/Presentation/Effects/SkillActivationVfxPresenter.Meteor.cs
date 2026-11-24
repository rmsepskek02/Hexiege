// ============================================================================
// SkillActivationVfxPresenter.Meteor.cs
// 메테오(타입 A + 착탄 지연 + 착탄 예약 연출)의 화면 연출: 예고 1회 · 떨어지는 몸체(반복) · 착탄 폭발 1회.
// (2026-11-18 지원 스킬 2-D 메테오 Task — _Tasks/2026-11-18/03_34_support-skill-meteor, Plan P-4 · P-5 · P-15 · U-1 · U-6 · U-10,
//  상위 결정 27 · 28 · 29)
//
// 무엇을 하나(초급자용 설명):
//   서버가 메테오 발동을 확정하면 발동 표현 값(SkillActivationPresentation — 조준점 · 착탄 지연 · 팀 · 슬롯 · 순번)이 이 화면에 들어온다
//   (싱글 · Host = 「확정」 채널, 순수 Client = 「수신」 채널 — 본 파일 SkillActivationVfxPresenter.cs 머리말 「두 채널」).
//   프리팹 · 반경 · 몸체 시작 위치 보정 · 예고 기준 시간은 이 화면 빌드의 스킬 데이터(팀 + 슬롯으로 다시 찾는다)에서 읽는다.
//     ① 예고 — 받은 순간 1번. 「발동 VFX」 칸 프리팹을 조준점 바닥(지면 + 0.06)에 「반경 ÷ 기준 반경」 크기로 재생하되,
//        재생 속도 = 예고 기준 시간 ÷ 착탄 지연(SkillData.ComputeWarningPlaybackSpeed — U-10). 지금 값(1.5 ÷ 1.5)이면 배율 1.
//        바꾼 속도는 그 풀 항목이 풀로 돌아갈 때 원래대로 돌아간다(VfxPoolItem.PlaybackSpeed.cs — 위험 R-7).
//     ② 떨어지는 몸체 — 받은 순간 시작. 「몸체 VFX」 칸 반복 프리팹을 (조준점 바닥 + 시작 위치 보정)에서 조준점 바닥까지
//        착탄 지연 동안 곧게(선형 — P-5) 옮기고, 착탄 시각에 꼬리 정지한다(새 입자만 멈추고 남은 입자는 제 수명대로 사라진다).
//        시작 위치 보정은 「화면 기준」(X 오른쪽 · Y 위 · Z 화면 위쪽)이라 빨강(뒤집힌) 화면에서도 같은 쪽 하늘에서 떨어져 보인다.
//        반복 프리팹이라 확인 번호(lease)로만 멈추고, 이 컴포넌트가 못 멈추는 경우를 대비해 항목 스스로 안전 마감(착탄 지연 + 0.5초 — O-7).
//     ③ 착탄 폭발 — 받은 시각 + 착탄 지연에 1번. 「착탄 VFX」 칸 프리팹을 조준점 바닥에 「반경 ÷ 기준 반경」 크기로 재생한다
//        (공습 폭격 1회분 PendingStrikeVfx와 같은 시각 예약 방식).
//
// 피해와의 관계:
//   이 파일은 화면 표현만 한다. 피해 · 화상은 서버(SkillActivationUseCase.ImpactBurn.cs · UnitCombatUseCase.ImpactBurn.cs)가 준다.
//   세 칸이 모두 비어 있어도 피해 · 화상은 그대로 들어간다 — 그때는 진단 줄(MeteorVfxSkipped Reason=NoPrefab)만 남는다.
//   순수 Client 화면은 발동 알림이 전송 지연만큼 늦게 오므로 연출도 그만큼 늦게 시작한다(공습과 같음). 서버 착탄은 서버 틱(0.1초)
//   만큼 늦을 수 있다(R-8 — 포격과 같은 정상 동작).
//
// 어떤 스킬이 이 경로를 타나: SkillData.UsesImpactPresentation(타입 A · 착탄 지연 > 0 · 세 칸 중 하나라도 채워짐)인 스킬만.
//   포격(세 칸 모두 비어 있음)은 이 경로를 타지 않고 본 파일의 원형 재생 그대로다.
//
// 대기열 정리(경기 종료 · 재초기화):
//   경기가 끝나면(GameEvents.OnGameEnd) 돌고 있는 몸체는 꼬리 정지하고 아직 재생하지 않은 착탄 폭발은 버린다(서버도 남은 착탄을 버린다).
//   맵을 다시 불러와 Initialize가 다시 불리면 몸체를 즉시 정지하고 착탄 폭발을 버린다(이전 경기의 연출이 새 경기에 남지 않게).
//
// 진단 로그(개발 축 · [Conditional] — 릴리스 빌드에서는 호출 · 문자열 조립이 사라진다. 상태가 바뀔 때만 1줄, U-9 존속 = 개발):
//   머리 [SKILL-METEOR] · Diag=SkillMeteor · DiagRev=1 (서버 MeteorBurnApplied와 같은 머리 — 같은 Seq로 짝지어 본다)
//     - MeteorVfxScheduled     : 발동 1건을 받은 순간 1줄(세 칸 프리팹 이름 · 착탄 지연 · 예고 기준 시간 · 재생 속도 · 몸체 시작 보정).
//     - MeteorWarningPlayed    : 예고를 재생한 순간 1줄(위치 · 크기 · 요청 속도 · 실제 적용 속도).
//     - MeteorBodyStarted      : 몸체를 시작한 순간 1줄(출발 · 도착 위치 · 거리 · 이동 시간 · 안전 마감).
//     - MeteorBodyStopped      : 몸체를 멈춘 순간 1줄(Reason=Impact · GameEnd · Reinit · AlreadyStopped. 안전 마감은 풀 항목이
//                                Reason=Timeout, Source=PoolItem 줄을 따로 남긴다 — 정상 경로 0건).
//     - MeteorImpactVfxPlayed  : 착탄 폭발을 재생한 순간 1줄(받은 뒤 실제 경과 ms · 계획 ms).
//     - MeteorVfxSkipped       : 칸이 비었거나 재생하지 못한 이유(Slot=Warning · Body · Impact, Reason=NoPrefab · LoopContract ·
//                                ZeroTravel · NotStarted · NoAim).
//     - MeteorVfxDropped       : 경기 종료 · 재초기화로 멈추거나 버린 수(있을 때만 1줄).
//
// Presentation 레이어 — Application(이벤트 · 값 객체 · GameLog) + Core(ViewConverter) 의존. 피해 · HP · 쿨다운에는 손대지 않는다.
// ============================================================================

using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    public sealed partial class SkillActivationVfxPresenter
    {
        /// <summary> 몸체 안전 마감 = 착탄 지연 + 이 값(초) — Plan O-7 「착탄 지연 + 0.5초」. </summary>
        public const float MeteorBodySafetyExtraSeconds = 0.5f;

        // 메테오 발동 1건의 남은 연출(몸체 정지 · 착탄 폭발 재생)을 기다리는 항목.
        private struct PendingMeteorVfx
        {
            public int Sequence;            // 발동 순번(서버 [SKILL-METEOR] 줄과 짝짓는 열쇠)
            public TeamId Team;             // 시전 팀
            public int Slot;                // 시전 슬롯
            public string Channel;          // 들어온 채널(Confirmed · Received)
            public float ReceivedTime;      // 발동을 받은 시각(Time.time)
            public float ImpactTime;        // 착탄 시각(받은 시각 + 착탄 지연) — 몸체 정지 · 착탄 폭발 재생 시각
            public VfxPoolItem Body;        // 돌고 있는 몸체 풀 항목(없으면 null)
            public ulong BodyLease;         // 몸체 확인 번호(0 = 몸체 없음)
            public string BodyPrefabName;   // 진단용 프리팹 이름
            public GameObject ImpactPrefab; // 착탄 폭발 프리팹(null이면 재생할 것 없음)
            public Vector3 ImpactViewPosition; // 착탄 폭발 위치(이 화면 좌표, 지면 높이 포함)
            public float ImpactScale;       // 착탄 폭발 루트 균일 배율(반경 ÷ 기준 반경)
        }

        // 대기 목록(받은 순서). 비울 때 할당이 없게 리스트를 재사용한다.
        private readonly List<PendingMeteorVfx> _pendingMeteorVfx = new List<PendingMeteorVfx>(4);

        // 경기 종료 구독(돌고 있는 몸체 꼬리 정지 · 남은 착탄 폭발 버림). Initialize마다 다시 건다.
        private IDisposable _meteorGameEndSubscription;

        // Initialize에서 부른다 — 이전 맵의 남은 메테오 연출을 즉시 정리하고 경기 종료 구독을 다시 건다.
        private void InitializeMeteor()
        {
            DropPendingMeteorVfx("Reinit", immediate: true);
            _meteorGameEndSubscription?.Dispose();
            _meteorGameEndSubscription = GameEvents.OnGameEnd.Subscribe(_ => DropPendingMeteorVfx("GameEnd", immediate: false));
        }

        // DisposeSubscriptions에서 부른다(파괴 · 재초기화). 남은 몸체는 여기서 멈추지 않는다 — 재초기화는 바로 뒤 InitializeMeteor가
        //   즉시 정지하고, 파괴(씬 정리)일 때는 풀 항목의 안전 마감이 스스로 꼬리 정지한다(VfxPoolItem.PlaybackSpeed.cs).
        private void DisposeMeteorSubscription()
        {
            _meteorGameEndSubscription?.Dispose();
            _meteorGameEndSubscription = null;
        }

        /// <summary>
        /// 남은 메테오 연출을 모두 정리한다 — 돌고 있는 몸체는 멈추고(즉시 또는 꼬리), 아직 재생하지 않은 착탄 폭발은 버린다.
        /// 정리할 것이 있을 때만 진단 줄을 남긴다.
        /// </summary>
        /// <param name="reason">정리 이유(GameEnd · Reinit).</param>
        /// <param name="immediate">true = 몸체 즉시 정지(입자까지 지움), false = 꼬리 정지.</param>
        private void DropPendingMeteorVfx(string reason, bool immediate)
        {
            int count = _pendingMeteorVfx.Count;
            if (count == 0) return;
            int bodies = 0, impacts = 0;
            float now = Time.time;
            for (int i = 0; i < count; i++)
            {
                PendingMeteorVfx e = _pendingMeteorVfx[i];
                if (e.BodyLease != 0)
                {
                    bool stopped = _effects != null && _effects.StopSkillTravelLoopVfx(e.Body, e.BodyLease, immediate);
                    if (stopped) bodies++;
                    LogMeteorBodyStopped(e, stopped ? reason : "AlreadyStopped", stopped, now);
                }
                if (e.ImpactPrefab != null) impacts++;
            }
            _pendingMeteorVfx.Clear();
            LogMeteorVfxDropped(reason, count, bodies, impacts);
        }

        /// <summary>
        /// 메테오 발동 1건을 받아 예고를 재생하고, 몸체를 시작하고, 착탄 폭발을 예약한다(OnSkillActivation의 메테오 분기에서 부른다).
        /// </summary>
        /// <param name="p">발동 표현 값(조준점 · 착탄 지연 · 순번).</param>
        /// <param name="channel">들어온 채널(Confirmed · Received).</param>
        /// <param name="skill">이 화면 빌드의 스킬 데이터(null이 아님 — 호출 측이 확인).</param>
        private void PlayMeteor(SkillActivationPresentation p, string channel, SkillData skill)
        {
            // 메테오는 지점 지정 스킬이다 — 조준점이 없으면(정상 경로에서는 없다) 재생할 자리가 없다.
            if (!p.HasAim)
            {
                LogMeteorVfxSkipped(p, channel, "All", "NoAim", isWarning: true);
                return;
            }

            // 착탄 지연 = 서버가 알림에 실은 값(같은 빌드면 스킬 데이터 값과 같다). 혹시 0이면 스킬 데이터 값을 쓴다.
            float delay = p.ImpactDelay > 0f ? p.ImpactDelay : skill.ImpactDelay;
            // 조준점(도메인) → 이 화면 좌표, 지면 높이(0.06)를 더한다 — 원형 발동 VFX와 같은 규칙(머리말 「지면 높이 보정」).
            Vector3 viewAim = ViewConverter.ToView(p.AimWorld);
            viewAim.y += _groundYOffset;
            float scale = EffectManager.ComputeSkillVfxScale(skill.Radius, skill.VfxReferenceRadius);
            float speed = SkillData.ComputeWarningPlaybackSpeed(skill.WarningVfxReferenceTime, delay);
            float now = Time.time;

            // [진단 T2-L9] 받은 발동 1줄(세 칸 프리팹 이름 · 지연 · 재생 속도).
            LogMeteorVfxScheduled(p, channel, skill, delay, speed, viewAim);

            // ① 예고(발동 VFX 칸) — 재생 속도 맞춤.
            if (skill.ActivationVfxPrefab == null)
            {
                LogMeteorVfxSkipped(p, channel, "Warning", "NoPrefab", isWarning: true);
            }
            else
            {
                bool played = _effects.PlaySkillActivationVfx(skill.ActivationVfxPrefab, viewAim, scale, speed, out float applied);
                // 스킬 SFX 없음(P-8). 효과음이 생기면 위 VFX 호출 바로 아래에서 짝으로 부른다(사운드 규칙 15).
                LogMeteorWarningPlayed(p, channel, skill.ActivationVfxPrefab.name, played, scale, speed, applied, viewAim);
            }

            // ② 떨어지는 몸체(반복) — 하늘 한쪽(화면 기준 보정)에서 조준점까지 착탄 지연 동안.
            VfxPoolItem body = null;
            ulong lease = 0;
            string bodyName = skill.FallingBodyVfxPrefab != null ? skill.FallingBodyVfxPrefab.name : "None";
            if (skill.FallingBodyVfxPrefab == null)
            {
                LogMeteorVfxSkipped(p, channel, "Body", "NoPrefab", isWarning: true);
            }
            else
            {
                Vector3 startView = viewAim + skill.FallingBodyStartOffset;
                float safety = delay + MeteorBodySafetyExtraSeconds;
                body = _effects.StartSkillTravelLoopVfx(skill.FallingBodyVfxPrefab, startView, viewAim, skill.FallingBodyEulerOffset,
                    delay, safety, FormattableString.Invariant($"Seq={p.Sequence}, Channel={channel}"), out lease, out string fail);
                if (body == null) LogMeteorVfxSkipped(p, channel, "Body", fail ?? "NotStarted", isWarning: true);
                else LogMeteorBodyStarted(p, channel, bodyName, lease, startView, viewAim, delay, safety);
            }

            // ③ 착탄 폭발 — 받은 시각 + 착탄 지연에 1번(아래 Update가 재생).
            if (skill.ImpactVfxPrefab == null) LogMeteorVfxSkipped(p, channel, "Impact", "NoPrefab", isWarning: true);

            if (lease == 0 && skill.ImpactVfxPrefab == null) return; // 기다릴 것이 없다(몸체 · 착탄 둘 다 없음).
            _pendingMeteorVfx.Add(new PendingMeteorVfx
            {
                Sequence = p.Sequence,
                Team = p.Team,
                Slot = p.Slot,
                Channel = channel,
                ReceivedTime = now,
                ImpactTime = now + delay,
                Body = body,
                BodyLease = lease,
                BodyPrefabName = bodyName,
                ImpactPrefab = skill.ImpactVfxPrefab,
                ImpactViewPosition = viewAim,
                ImpactScale = scale
            });
        }

        /// <summary>
        /// 매 프레임(Update — SkillActivationVfxPresenter.LineStrike.cs) 착탄 시각이 된 메테오의 몸체를 꼬리 정지하고 착탄 폭발을 재생한다.
        /// 대기 목록이 비어 있으면 바로 끝난다(평소 비용 없음).
        /// </summary>
        private void UpdateMeteorVfx()
        {
            if (_pendingMeteorVfx.Count == 0) return;
            // 의존성이 사라졌으면(파괴 직전 등) 조용히 비운다 — 남은 몸체는 풀 항목의 안전 마감이 스스로 멈춘다.
            if (_effects == null)
            {
                _pendingMeteorVfx.Clear();
                return;
            }

            float now = Time.time;
            int keep = 0;
            for (int i = 0; i < _pendingMeteorVfx.Count; i++)
            {
                PendingMeteorVfx e = _pendingMeteorVfx[i];
                if (now < e.ImpactTime)
                {
                    _pendingMeteorVfx[keep++] = e; // 아직 이르다 — 앞으로 당겨 담아 할당 없이 정리
                    continue;
                }

                // 몸체 — 착탄 시각에 꼬리 정지(새 입자만 멈춤). 이미 멈춰 있으면(안전 마감 · 항목 파괴) AlreadyStopped.
                if (e.BodyLease != 0)
                {
                    bool stopped = _effects.StopSkillTravelLoopVfx(e.Body, e.BodyLease, false);
                    LogMeteorBodyStopped(e, stopped ? "Impact" : "AlreadyStopped", stopped, now);
                }

                // 착탄 폭발 — 1번 재생(원형 발동 VFX와 같은 재생 함수).
                if (e.ImpactPrefab != null)
                {
                    bool played = _effects.PlaySkillActivationVfx(e.ImpactPrefab, e.ImpactViewPosition, e.ImpactScale);
                    LogMeteorImpactVfxPlayed(e, played, now);
                }
            }
            _pendingMeteorVfx.RemoveRange(keep, _pendingMeteorVfx.Count - keep);
        }

        // ====================================================================
        // 진단 로그 [SKILL-METEOR] (화면 쪽) — 개발 축 · [Conditional]
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMeteorVfxScheduled(SkillActivationPresentation p, string channel, SkillData skill, float delay, float speed,
                                                  Vector3 viewAim)
        {
            string warn = skill.ActivationVfxPrefab != null ? skill.ActivationVfxPrefab.name : "None";
            string body = skill.FallingBodyVfxPrefab != null ? skill.FallingBodyVfxPrefab.name : "None";
            string impact = skill.ImpactVfxPrefab != null ? skill.ImpactVfxPrefab.name : "None";
            Vector3 o = skill.FallingBodyStartOffset;
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorVfxScheduled",
                FormattableString.Invariant(
                    $"Diag=SkillMeteor, DiagRev=1, Event=MeteorVfxScheduled, Role={CurrentRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, BuildingId={p.BuildingId}, Slot={p.Slot}, DelayMs={ToMm(delay)}, WarnRefMs={ToMm(skill.WarningVfxReferenceTime)}, WarningSpeedMilli={ToMm(speed)}, WarnPrefab={warn}, BodyPrefab={body}, ImpactPrefab={impact}, BodyOffsetXmm={ToMm(o.x)}, BodyOffsetYmm={ToMm(o.y)}, BodyOffsetZmm={ToMm(o.z)}, AimXmm={ToMm(p.AimWorld.x)}, AimZmm={ToMm(p.AimWorld.z)}, ViewXmm={ToMm(viewAim.x)}, ViewZmm={ToMm(viewAim.z)}, ViewFlipped={ViewConverter.IsFlipped}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMeteorWarningPlayed(SkillActivationPresentation p, string channel, string prefabName, bool played, float scale,
                                                   float requestedSpeed, float appliedSpeed, Vector3 viewAim)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorWarningPlayed",
                FormattableString.Invariant(
                    $"Diag=SkillMeteor, DiagRev=1, Event=MeteorWarningPlayed, Role={CurrentRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Prefab={prefabName}, Played={played}, Scale={scale:F2}, WarningSpeedMilli={ToMm(requestedSpeed)}, AppliedSpeedMilli={ToMm(appliedSpeed)}, ViewXmm={ToMm(viewAim.x)}, ViewZmm={ToMm(viewAim.z)}, RootYmm={ToMm(viewAim.y)}, AboveTileTop={viewAim.y > TileTopSurfaceY}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMeteorBodyStarted(SkillActivationPresentation p, string channel, string prefabName, ulong lease,
                                                 Vector3 startView, Vector3 endView, float duration, float safety)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorBodyStarted",
                FormattableString.Invariant(
                    $"Diag=SkillMeteor, DiagRev=1, Event=MeteorBodyStarted, Role={CurrentRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Prefab={prefabName}, Lease={lease}, StartViewXmm={ToMm(startView.x)}, StartViewYmm={ToMm(startView.y)}, StartViewZmm={ToMm(startView.z)}, EndViewXmm={ToMm(endView.x)}, EndViewYmm={ToMm(endView.y)}, EndViewZmm={ToMm(endView.z)}, DistanceMilli={ToMm((endView - startView).magnitude)}, TravelMs={ToMm(duration)}, SafetyMs={ToMm(safety)}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMeteorBodyStopped(PendingMeteorVfx e, string reason, bool stopped, float now)
        {
            string data = FormattableString.Invariant(
                $"Diag=SkillMeteor, DiagRev=1, Event=MeteorBodyStopped, Role={CurrentRole()}, Channel={e.Channel}, Seq={e.Sequence}, Team={e.Team}, Slot={e.Slot}, Prefab={e.BodyPrefabName}, Lease={e.BodyLease}, Reason={reason}, Stopped={stopped}, PlannedMs={ToMm(e.ImpactTime - e.ReceivedTime)}, MsAfterReceived={ToMm(now - e.ReceivedTime)}");
            // 정상 경로(Impact · GameEnd · Reinit)는 Info, 이미 멈춰 있던 경우(안전 마감 · 항목 파괴)는 이상 신호라 Warn.
            if (stopped) GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorBodyStopped", data);
            else GameLog.Dev.Warn("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorBodyStopped", data);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMeteorImpactVfxPlayed(PendingMeteorVfx e, bool played, float now)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorImpactVfxPlayed",
                FormattableString.Invariant(
                    $"Diag=SkillMeteor, DiagRev=1, Event=MeteorImpactVfxPlayed, Role={CurrentRole()}, Channel={e.Channel}, Seq={e.Sequence}, Team={e.Team}, Slot={e.Slot}, Prefab={(e.ImpactPrefab != null ? e.ImpactPrefab.name : "None")}, Played={played}, Scale={e.ImpactScale:F2}, PlannedMs={ToMm(e.ImpactTime - e.ReceivedTime)}, MsAfterReceived={ToMm(now - e.ReceivedTime)}, ViewXmm={ToMm(e.ImpactViewPosition.x)}, ViewZmm={ToMm(e.ImpactViewPosition.z)}, RootYmm={ToMm(e.ImpactViewPosition.y)}"));
        }

        /// <summary>
        /// 메테오 연출 칸을 재생하지 못한 경우 1줄. 칸이 비었거나 계약 위반 · 시작 실패는 연출 누락이라 Warn(LogRules 축 A).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMeteorVfxSkipped(SkillActivationPresentation p, string channel, string slotName, string reason, bool isWarning)
        {
            string data = FormattableString.Invariant(
                $"Diag=SkillMeteor, DiagRev=1, Event=MeteorVfxSkipped, Role={CurrentRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, VfxSlot={slotName}, Reason={reason}");
            if (isWarning) GameLog.Dev.Warn("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorVfxSkipped", data);
            else GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorVfxSkipped", data);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMeteorVfxDropped(string reason, int casts, int bodies, int impacts)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-METEOR] MeteorVfxDropped",
                FormattableString.Invariant(
                    $"Diag=SkillMeteor, DiagRev=1, Event=MeteorVfxDropped, Role={CurrentRole()}, Reason={reason}, Casts={casts}, BodiesStopped={bodies}, ImpactsDropped={impacts}"));
        }
    }
}
