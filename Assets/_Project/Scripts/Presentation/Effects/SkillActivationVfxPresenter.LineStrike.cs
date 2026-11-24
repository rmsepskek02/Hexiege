// ============================================================================
// SkillActivationVfxPresenter.LineStrike.cs
// 공습(직선 연속 폭격 — SkillMechanicType.LineStrike) 의 화면 연출: 직선 경고 1회 + 폭격 1회분 N회.
// (2026-11-17 지원 스킬 2-L 공습 Task — _Tasks/2026-11-17/09_12_support-skill-air-strike, Plan L-5 · P-6 · U-14)
//
// 무엇을 하나(초급자용 설명):
//   서버가 공습 발동을 확정하면 발동 표현 값(SkillActivationPresentation)이 이 화면에 들어온다
//   (싱글 · Host = 「확정」 채널, 순수 Client = 「수신」 채널 — 본 파일 SkillActivationVfxPresenter.cs 머리말 「두 채널」).
//   그 값에는 시작점(AimWorld) · 방향(AimDirection) · 팀 · 슬롯이 있다. 길이 · 폭 · 폭격 횟수 · 간격 · 프리팹은
//   이 화면 빌드의 스킬 데이터(팀 + 슬롯으로 다시 찾는다 — 경기 시작 스키마 일치가 두 빌드 데이터가 같음을 보장)에서 읽는다.
//   (2026-11-24 정정 — 직전 「스키마 11 일치」. 스키마 값은 2026-11-23 연막탄으로 12가 됐다 — UnitAttackShadowProfileResolver.)
//     ① 직선 경고 — 발동을 받은 순간 1번. 스킬 데이터 「발동 VFX」 칸 프리팹을
//        직선 가운데(시작점 + 방향 × 유효 길이 ÷ 2)에, 앞(+Z)이 직선 방향을 보게 돌려, 길이 방향(Z)만 「유효 길이 ÷ 기준 길이」배로 재생한다.
//     ② 폭격 1회분 — k번째(0부터) 폭격을 「받은 시각 + 착탄 지연 + k × 폭격 간격」에 1번씩. 스킬 데이터 「폭격 VFX」 칸 프리팹을
//        k번째 구간의 맵 안 부분 가운데에, 직선 방향으로 돌려, 원본 크기로 재생한다.
//        맵 밖 구간(서버도 건너뛰는 폭격 — LineStrikeGeometry.IsStrikeInRange == false)은 재생하지 않는다(U-14).
//   「맵 안 유효 길이」는 서버 판정과 조준 표시가 쓰는 같은 순수 함수(LineStrikeWorld.ComputeEffectiveLength)로 구한다(Plan R-14) —
//   그래서 화면 경고 길이 · 폭격 자리가 서버가 실제로 때린 범위와 같다.
//
// 피해와의 관계:
//   이 파일은 화면 표현만 한다. 피해 · HP · 쿨다운에는 손대지 않는다(피해는 서버 SkillActivationUseCase.LineStrike.cs).
//   그래서 두 칸(경고 · 폭격)이 모두 비어 있어도 피해는 정상으로 들어간다 — 그때는 진단 줄(NoPrefab)만 남는다.
//   순수 Client 화면은 발동 알림이 전송 지연만큼 늦게 오므로 연출도 그만큼 늦게 시작한다(Plan R-9 — 피해는 서버 시각 그대로).
//
// 빨강(뒤집힌) 화면:
//   위치는 도메인 좌표를 ViewConverter.ToView로, 방향은 SkillLineAimMath.DomainDirectionToView(두 점을 모두 ToView 한 차)로 바꾼다.
//   조준 표시(SkillLineAimReticle)와 같은 변환이라 조준 때 본 직사각형과 같은 자리 · 같은 방향에 이펙트가 놓인다.
//
// 대기열 정리:
//   경기가 끝나면(GameEvents.OnGameEnd) 아직 재생하지 않은 폭격 연출을 버린다(서버도 남은 폭격을 버린다 — ClosePendingImpacts).
//   맵을 다시 불러와 Initialize가 다시 불려도 버린다(이전 경기의 연출이 새 경기에 뜨지 않게).
//
// 진단 로그(개발 축 · [Conditional] — 릴리스 빌드에서는 호출 · 문자열 조립이 모두 사라진다. 상태가 바뀔 때만 1줄):
//   머리 [SKILL-LINE] · Diag=SkillLine · DiagRev=1 (서버 줄과 같은 머리 — 같은 Seq로 서버 줄과 짝지어 본다)
//     - LineVfxScheduled     : 발동 1건을 받아 연출을 준비한 순간 1줄(시작점 · 방향 · 길이 · 유효 길이 · 횟수 · 맵 안 폭격 수 · 두 칸의 프리팹).
//     - LineVfxWarnPlayed    : 직선 경고를 재생한 순간 1줄(위치 · 화면 방향 · 길이 배율).
//     - LineVfxWarnSkipped   : 경고를 재생하지 못한 이유(ZeroLength). 경고 칸이 비었으면 기존 [SKILL-VFX] ActivationVfxSkipped Reason=NoPrefab.
//     - LineVfxStrikePlayed  : 폭격 1회분을 재생한 순간마다 1줄(Index 1 → N, 받은 뒤 실제 경과 ms) — 줄 수 = 서버 LineStrikeHit 수.
//     - LineVfxStrikeSkipped : 맵 밖 구간이라 재생하지 않은 폭격마다 1줄(Reason=OutOfMap) — 줄 수 = 서버 LineStrikeSkipped 수.
//                              폭격 칸이 비었으면 발동 1건에 1줄(Index=0, Reason=NoPrefab).
//     - LineVfxStrikesDropped: 경기 종료 · 재초기화로 재생하지 않고 버린 폭격 연출 수(있을 때만 1줄).
//
// Presentation 레이어 — Application(이벤트 · 값 객체 · LineStrikeWorld · GameLog) + Domain(LineStrikeGeometry) + Core(ViewConverter) 의존.
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
        // 맵 경계 사각형 함수(공습 「맵 안 유효 길이」용 — 서버와 같은 HexMetrics.ComputeMapWorldBounds 값). null이면 잘라내지 않는다.
        //   조합 루트(GameBootstrapper.Map)가 SetMapWorldBoundsProvider로 넣는다. 서버 쪽도 경계를 모르면 잘라내지 않으므로 기준이 같다.
        private MapWorldBoundsProvider _lineMapBounds;

        // 아직 재생하지 않은 폭격 1회분 연출 1건(재생 위치 · 회전은 받는 순간 미리 계산해 둔다 — 경기 중 뷰 뒤집힘은 바뀌지 않는다).
        private struct PendingStrikeVfx
        {
            public int Sequence;          // 발동 순번(서버 [SKILL-LINE] 줄과 짝짓는 열쇠)
            public TeamId Team;           // 시전 팀
            public int Slot;              // 시전 슬롯
            public string Channel;        // 들어온 채널(Confirmed · Received)
            public GameObject Prefab;     // 폭격 VFX 프리팹(null이면 대기열에 넣지 않는다)
            public Vector3 ViewPosition;  // 재생 위치(이 화면 좌표, 지면 높이 포함)
            public Quaternion ViewRotation; // 재생 회전(앞 = 직선 방향)
            public float ReceivedTime;    // 발동을 받은 시각(Time.time)
            public float DueTime;         // 재생할 시각(Time.time 기준 = 받은 시각 + 첫 지연 + k × 간격)
            public int Index;             // 몇 번째 폭격인가(0부터)
            public int Count;             // 폭격 횟수
            public float SegFrom;         // 구간 시작 진행 거리(원래 길이 기준)
            public float JudgeTo;         // 구간 끝 진행 거리 ∩ 유효 길이(맵 안 부분의 끝)
            public float CenterAlong;     // 재생 위치의 진행 거리(맵 안 부분 가운데)
        }

        // 재생 대기열(받은 순서 = 폭격 번호 순서로 들어 있다). 비울 때 할당이 없게 리스트를 재사용한다.
        private readonly List<PendingStrikeVfx> _pendingStrikeVfx = new List<PendingStrikeVfx>(10);

        // 경기 종료 구독(남은 폭격 연출 버림). Initialize마다 다시 건다.
        private IDisposable _lineGameEndSubscription;

        /// <summary>
        /// 공습 「맵 안 유효 길이」 계산에 쓸 맵 경계 사각형 함수를 주입한다(GameBootstrapper.Map에서 맵마다 1회 — 2026-11-17 공습 L-5).
        /// 서버 판정(SkillActivationUseCase.SetMapWorldBoundsProvider)과 같은 계산(HexMetrics.ComputeMapWorldBounds)을 넣어야
        /// 화면 경고 길이 · 폭격 자리가 서버가 때린 범위와 같아진다. 주입하지 않으면 잘라내지 않는다(진단 MapBounds=Missing).
        /// </summary>
        /// <param name="provider">지금 맵의 경계를 돌려주는 함수(null 허용).</param>
        public void SetMapWorldBoundsProvider(MapWorldBoundsProvider provider)
        {
            _lineMapBounds = provider;
        }

        // Initialize에서 부른다 — 이전 맵의 남은 폭격 연출을 버리고 경기 종료 구독을 다시 건다.
        private void InitializeLineStrike()
        {
            DropPendingStrikeVfx("Reinit");
            _lineGameEndSubscription?.Dispose();
            _lineGameEndSubscription = GameEvents.OnGameEnd.Subscribe(_ => DropPendingStrikeVfx("GameEnd"));
        }

        // DisposeSubscriptions에서 부른다(파괴 · 재초기화).
        private void DisposeLineStrikeSubscription()
        {
            _lineGameEndSubscription?.Dispose();
            _lineGameEndSubscription = null;
        }

        // 아직 재생하지 않은 폭격 연출을 모두 버린다(있을 때만 진단 1줄).
        private void DropPendingStrikeVfx(string reason)
        {
            int dropped = _pendingStrikeVfx.Count;
            if (dropped == 0) return;
            _pendingStrikeVfx.Clear();
            LogLineVfxStrikesDropped(reason, dropped);
        }

        /// <summary>
        /// 공습 발동 1건을 받아 직선 경고를 재생하고 폭격 1회분 연출을 예약한다(OnSkillActivation의 LineStrike 분기에서 부른다).
        /// </summary>
        /// <param name="p">발동 표현 값(시작점 · 방향 · 순번).</param>
        /// <param name="channel">들어온 채널(Confirmed · Received).</param>
        /// <param name="skill">이 화면 빌드의 공습 스킬 데이터(null이 아님 — 호출 측이 확인).</param>
        private void PlayLineStrike(SkillActivationPresentation p, string channel, SkillData skill)
        {
            // 방향 · 직선 값 검사 — 서버가 이미 거른 값이라 정상이면 걸리지 않는다(걸리면 Warn 1줄로 드러낸다).
            if (!p.HasAim || !LineStrikeWorld.TryNormalizeDirection(p.AimDirection, out Vector3 dir))
            {
                LogLineVfxStrikeSkipped(p, channel, 0, skill.LineStrikeCount, "NoDirection", 0f, 0f, 0f, isWarning: true);
                return;
            }
            if (!(skill.LineLength > 0f) || skill.LineStrikeCount < 1)
            {
                LogLineVfxStrikeSkipped(p, channel, 0, skill.LineStrikeCount, "InvalidLineData", 0f, 0f, 0f, isWarning: true);
                return;
            }

            // 시작점(도메인, 높이 0) · 맵 안 유효 길이(서버와 같은 순수 함수 — R-14).
            Vector3 start = new Vector3(p.AimWorld.x, 0f, p.AimWorld.z);
            float length = skill.LineLength;
            float eff = LineStrikeWorld.ComputeEffectiveLength(start, dir, length, _lineMapBounds, out bool boundsKnown);
            int count = skill.LineStrikeCount;

            // 이 화면에서의 직선 방향 · 회전(빨강 화면이면 도메인 방향의 반대로 보인다 — 조준 표시와 같은 변환).
            Vector3 viewDir = SkillLineAimMath.DomainDirectionToView(start, dir);
            Quaternion viewRot = Quaternion.LookRotation(viewDir, Vector3.up);

            // 맵 안 폭격 수(진단용 — 서버 Done 예상치).
            int inRange = 0;
            for (int k = 0; k < count; k++)
                if (LineStrikeGeometry.IsStrikeInRange(k, count, length, eff)) inRange++;

            // [진단 L-L16 · L-L17] 받은 발동 1줄 — Client의 시작점 · 방향이 같은 Seq의 서버 LineStrikeScheduled와 같은지 본다.
            LogLineVfxScheduled(p, channel, start, dir, viewDir, length, eff, boundsKnown, count, inRange, skill);

            // ① 직선 경고(발동 VFX 칸).
            PlayLineWarning(p, channel, skill, start, dir, eff, viewRot, viewDir);

            // ② 폭격 1회분 연출 예약(폭격 VFX 칸).
            ScheduleStrikeVfx(p, channel, skill, start, dir, length, eff, count, viewRot);
        }

        // ① 직선 경고 — 직선 가운데 · 직선 방향 · 길이 방향 배율(유효 길이 ÷ 기준 길이).
        private void PlayLineWarning(SkillActivationPresentation p, string channel, SkillData skill, Vector3 start, Vector3 dir,
                                     float eff, Quaternion viewRot, Vector3 viewDir)
        {
            if (skill.ActivationVfxPrefab == null)
            {
                // 경고 칸이 비었다 — 기존 발동 VFX 「프리팹 없음」 줄과 같은 줄을 쓴다(Plan L-L17 Reason=NoPrefab).
                LogVfxSkipped(p, channel, "NoPrefab", isWarning: true);
                return;
            }
            if (!(eff > LineStrikeGeometry.BoundaryEpsilon))
            {
                // 유효 길이가 0(맵 경계 위 시작점에서 바깥쪽을 향함) — 늘일 길이가 없어 경고를 띄우지 않는다(폭격 1회째는 그대로 예약된다).
                LogLineVfxWarnSkipped(p, channel, "ZeroLength", eff);
                return;
            }

            // 가운데 = 시작점 + 방향 × (유효 길이 ÷ 2). 도메인 → 이 화면 좌표, 지면 높이(0.06)를 더한다(원형 발동 VFX와 같은 규칙).
            Vector3 viewCenter = ViewConverter.ToView(start + dir * (eff * 0.5f));
            viewCenter.y += _groundYOffset;
            // 길이 방향(로컬 Z)만 늘이고 줄인다. 기준 길이가 0 이하면 배율 1(ComputeSkillVfxScale 규칙 — 반경 대신 길이를 넣는다).
            float zScale = EffectManager.ComputeSkillVfxScale(eff, skill.LineVfxReferenceLength);
            Vector3 scale = new Vector3(1f, 1f, zScale);

            bool played = _effects.PlaySkillVfxOriented(skill.ActivationVfxPrefab, viewCenter, viewRot, scale);
            // 스킬 SFX 없음. 효과음이 생기면 위 VFX 호출 바로 아래에서 짝으로 부른다(사운드 규칙 15).

            // [진단 L-L17] 경고 재생 1줄.
            LogLineVfxWarnPlayed(p, channel, skill.ActivationVfxPrefab.name, played, eff, skill.LineVfxReferenceLength, zScale,
                viewCenter, viewDir);
        }

        // ② 폭격 1회분 연출 예약 — 맵 안 구간만, 시각 = 받은 시각 + 첫 지연 + k × 간격.
        private void ScheduleStrikeVfx(SkillActivationPresentation p, string channel, SkillData skill, Vector3 start, Vector3 dir,
                                       float length, float eff, int count, Quaternion viewRot)
        {
            if (skill.StrikeVfxPrefab == null)
            {
                // 폭격 칸이 비었다 — 발동 1건에 1줄만 남긴다(폭격마다 남기면 같은 사실이 N줄이 된다).
                LogLineVfxStrikeSkipped(p, channel, 0, count, "NoPrefab", 0f, 0f, eff, isWarning: true);
                return;
            }

            // 첫 지연 · 간격은 서버 예약(ScheduleLineStrikeBridge)과 같은 정리 규칙(음수 · NaN → 0)을 쓴다.
            //   첫 지연 = 스킬 데이터의 「착탄 지연」(공습은 이 칸을 첫 폭격까지의 시간으로 쓴다 — SkillData 주석).
            float first = skill.ImpactDelay > 0f ? skill.ImpactDelay : 0f;
            float interval = skill.LineStrikeInterval > 0f ? skill.LineStrikeInterval : 0f;
            float now = Time.time;

            for (int k = 0; k < count; k++)
            {
                LineStrikeGeometry.GetSegmentBounds(k, count, length, out float segFrom, out float segTo);
                if (!LineStrikeGeometry.IsStrikeInRange(k, count, length, eff))
                {
                    // [진단 L-L17 · L-L25] 맵 밖 구간 — 서버도 이 폭격을 건너뛴다(LineStrikeSkipped Reason=OutOfMap).
                    LogLineVfxStrikeSkipped(p, channel, k + 1, count, "OutOfMap", segFrom, segTo, eff, isWarning: false);
                    continue;
                }

                // 맵 안 부분 = [구간 시작, min(구간 끝, 유효 길이)] — 그 가운데에 재생한다.
                float judgeTo = segTo < eff ? segTo : eff;
                if (judgeTo < segFrom) judgeTo = segFrom; // 첫 폭격(k = 0)은 유효 길이 0이어도 판정한다 — 그때는 시작점.
                float centerAlong = (segFrom + judgeTo) * 0.5f;
                Vector3 viewPos = ViewConverter.ToView(start + dir * centerAlong);
                viewPos.y += _groundYOffset;

                _pendingStrikeVfx.Add(new PendingStrikeVfx
                {
                    Sequence = p.Sequence,
                    Team = p.Team,
                    Slot = p.Slot,
                    Channel = channel,
                    Prefab = skill.StrikeVfxPrefab,
                    ViewPosition = viewPos,
                    ViewRotation = viewRot,
                    ReceivedTime = now,
                    DueTime = now + first + k * interval,
                    Index = k,
                    Count = count,
                    SegFrom = segFrom,
                    JudgeTo = judgeTo,
                    CenterAlong = centerAlong
                });
            }
        }

        /// <summary>
        /// 매 프레임 재생 시각이 된 폭격 1회분 연출을 재생한다(받은 순서 = 폭격 번호 순서).
        /// 대기열이 비어 있으면 바로 끝난다(평소 비용 없음).
        /// </summary>
        private void Update()
        {
            // [2026-11-18 메테오 Task] 메테오 몸체 정지 · 착탄 폭발 재생 시각도 같은 Update에서 본다(SkillActivationVfxPresenter.Meteor.cs).
            //   한 컴포넌트에 Update는 하나뿐이라 여기서 먼저 부르고, 아래 공습 처리(대기열이 비면 바로 끝남)는 그대로 둔다.
            UpdateMeteorVfx();
            if (_pendingStrikeVfx.Count == 0) return;
            // 의존성이 사라졌으면(파괴 직전 등) 남은 연출을 조용히 버린다 — 재생할 수단이 없다.
            if (_effects == null)
            {
                _pendingStrikeVfx.Clear();
                return;
            }

            float now = Time.time;
            int keep = 0;
            for (int i = 0; i < _pendingStrikeVfx.Count; i++)
            {
                PendingStrikeVfx s = _pendingStrikeVfx[i];
                if (now < s.DueTime)
                {
                    _pendingStrikeVfx[keep++] = s; // 아직 이르다 — 남긴다(앞으로 당겨 담아 할당 없이 정리)
                    continue;
                }
                bool played = _effects.PlaySkillVfxOriented(s.Prefab, s.ViewPosition, s.ViewRotation, Vector3.one);
                // [진단 L-L17] 폭격 1회분 재생 1줄.
                LogLineVfxStrikePlayed(s, played, now);
            }
            _pendingStrikeVfx.RemoveRange(keep, _pendingStrikeVfx.Count - keep);
        }

        // ====================================================================
        // 진단 로그 [SKILL-LINE] (화면 쪽) — 개발 축 · [Conditional]
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineVfxScheduled(SkillActivationPresentation p, string channel, Vector3 start, Vector3 dir, Vector3 viewDir,
                                                float length, float eff, bool boundsKnown, int count, int inRange, SkillData skill)
        {
            string warn = skill.ActivationVfxPrefab != null ? skill.ActivationVfxPrefab.name : "None";
            string strike = skill.StrikeVfxPrefab != null ? skill.StrikeVfxPrefab.name : "None";
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-LINE] LineVfxScheduled",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineVfxScheduled, Role={CurrentRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, BuildingId={p.BuildingId}, Slot={p.Slot}, StartXmm={ToMm(start.x)}, StartZmm={ToMm(start.z)}, DirXMilli={ToMm(dir.x)}, DirZMilli={ToMm(dir.z)}, ViewDirXMilli={ToMm(viewDir.x)}, ViewDirZMilli={ToMm(viewDir.z)}, LengthMilli={ToMm(length)}, EffLengthMilli={ToMm(eff)}, MapBounds={(boundsKnown ? "Known" : "Missing")}, Strikes={count}, InRange={inRange}, FirstDelayMs={ToMm(skill.ImpactDelay)}, IntervalMs={ToMm(skill.LineStrikeInterval)}, WarnPrefab={warn}, StrikePrefab={strike}, ViewFlipped={ViewConverter.IsFlipped}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineVfxWarnPlayed(SkillActivationPresentation p, string channel, string prefabName, bool played, float eff,
                                                 float referenceLength, float zScale, Vector3 viewCenter, Vector3 viewDir)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-LINE] LineVfxWarnPlayed",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineVfxWarnPlayed, Role={CurrentRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Prefab={prefabName}, Played={played}, EffLengthMilli={ToMm(eff)}, RefLengthMilli={ToMm(referenceLength)}, ScaleZMilli={ToMm(zScale)}, CenterViewXmm={ToMm(viewCenter.x)}, CenterViewZmm={ToMm(viewCenter.z)}, RootYmm={ToMm(viewCenter.y)}, ViewDirXMilli={ToMm(viewDir.x)}, ViewDirZMilli={ToMm(viewDir.z)}, ViewFlipped={ViewConverter.IsFlipped}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineVfxWarnSkipped(SkillActivationPresentation p, string channel, string reason, float eff)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-LINE] LineVfxWarnSkipped",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineVfxWarnSkipped, Role={CurrentRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Reason={reason}, EffLengthMilli={ToMm(eff)}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineVfxStrikePlayed(PendingStrikeVfx s, bool played, float now)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-LINE] LineVfxStrikePlayed",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineVfxStrikePlayed, Role={CurrentRole()}, Channel={s.Channel}, Seq={s.Sequence}, Team={s.Team}, Slot={s.Slot}, Index={s.Index + 1}, Strikes={s.Count}, Prefab={(s.Prefab != null ? s.Prefab.name : "None")}, Played={played}, PlannedMs={ToMm(s.DueTime - s.ReceivedTime)}, ElapsedMs={ToMm(now - s.ReceivedTime)}, SegFromMilli={ToMm(s.SegFrom)}, JudgeToMilli={ToMm(s.JudgeTo)}, CenterAlongMilli={ToMm(s.CenterAlong)}, ViewXmm={ToMm(s.ViewPosition.x)}, ViewZmm={ToMm(s.ViewPosition.z)}, RootYmm={ToMm(s.ViewPosition.y)}"));
        }

        /// <summary>
        /// 폭격 연출을 재생하지 않은 경우 1줄. 맵 밖 구간(OutOfMap)은 정상 경로라 Info, 칸 비어 있음 · 잘못된 값은 Warn(LogRules 축 A).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineVfxStrikeSkipped(SkillActivationPresentation p, string channel, int index, int count, string reason,
                                                    float segFrom, float segTo, float eff, bool isWarning)
        {
            string data = FormattableString.Invariant(
                $"Diag=SkillLine, DiagRev=1, Event=LineVfxStrikeSkipped, Role={CurrentRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Index={index}, Strikes={count}, Reason={reason}, SegFromMilli={ToMm(segFrom)}, SegToMilli={ToMm(segTo)}, EffLengthMilli={ToMm(eff)}");
            if (isWarning)
                GameLog.Dev.Warn("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-LINE] LineVfxStrikeSkipped", data);
            else
                GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-LINE] LineVfxStrikeSkipped", data);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogLineVfxStrikesDropped(string reason, int dropped)
        {
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-LINE] LineVfxStrikesDropped",
                FormattableString.Invariant(
                    $"Diag=SkillLine, DiagRev=1, Event=LineVfxStrikesDropped, Role={CurrentRole()}, Reason={reason}, Dropped={dropped}"));
        }
    }
}
