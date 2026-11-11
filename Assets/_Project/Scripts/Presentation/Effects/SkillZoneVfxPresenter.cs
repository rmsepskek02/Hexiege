// ============================================================================
// SkillZoneVfxPresenter.cs
// 지속 상태 구역(서리 감옥 · 수액 늪) "구역 VFX" 화면 구독자 — 구역이 남아 있는 동안 조준 지점 바닥에 반복 이펙트를 띄운다.
// 2026-11-11 지속 상태 구역 Task(_Tasks/2026-11-10/16_02_support-skill-persistent-zones) 구현 단위 ③.
//
// 무엇을 하나(초급자용 설명):
//   서리 감옥을 쓰면 서버가 그 자리에 4초짜리 얼음 구역을 만든다(서버 판정 — 이 파일과 무관).
//   이 컴포넌트는 그 발동 알림을 받아, 조준 지점 바닥에 스킬 자산 「구역 VFX」 칸의 프리팹을 반복 재생하고,
//   구역 지속 시간이 지나면 이펙트를 꼬리 정지한다(새 입자만 멈추고 남은 입자는 사그라진다).
//   칸이 비어 있으면 아무것도 띄우지 않고 진단 로그 「ZoneVfxSkipped Reason=NoPrefab」(Warn)을 1줄 남긴다(효과는 그대로 동작).
//
// 어떤 알림을 받나(사용자 결정 Z-2 N-B — 구역 VFX는 발동 채널 C-1 재사용):
//   - 싱글 · Host 화면: 「발동 확정」(GameEvents.OnSkillActivationConfirmed)만 들어온다.
//   - 멀티 순수 Client 화면: 「발동 수신」(GameEvents.OnSkillActivationReceived)만 들어온다(Host는 IsServer 가드로 발행하지 않는다).
//   알림에는 팀 · 슬롯 · 조준 좌표만 있으므로, 각 화면이 "자기 빌드"의 로드아웃에서 프리팹 · 반경 · 지속 시간을 다시 찾는다.
//   구역 끝 알림은 없다 — 받은 순간 + 구역 지속 시간이 되면 스스로 끈다(Z-7 「시전 건물 파괴 시에도 구역 유지」라 일찍 끝나지 않는다).
//   알려진 한계(EMP R-7과 같음): 순수 Client는 전송 지연만큼 늦게 시작하고 늦게 끝난다(길이는 같다).
//
// 정지 방식(사용자 결정 Z-8 · Plan 2절 Z-8 · Z-9 표):
//   - 구역 시간 끝 → 꼬리 정지 / 경기 종료 → 꼬리 정지(그 뒤 이번 경기에서는 새로 띄우지 않는다)
//   - 재경기(맵 다시 불러오기 · Initialize) · 파괴 → 즉시 정지
//
// 위치 · 크기(발동 VFX SkillActivationVfxPresenter와 같은 규칙):
//   위치 = 조준 좌표(도메인) → ViewConverter.ToView(빨강 화면이면 X · Z 반전) + 지면 높이(GameConfig.SkillVfxGroundYOffset 0.06).
//   크기 = 스킬 반경 ÷ VFX 기준 반경(기준 반경은 발동 VFX와 공유 — Z-13). 회전 = 항등.
//
// 예외 안전: Host · 싱글에서는 「발동 확정」이 서버 발동 처리(SkillActivationUseCase.Activate) 안에서 동기로 발행된다.
//   여기서 예외가 나도 서버 발동이 깨지지 않도록 이벤트 처리 · 매 프레임 처리를 try/catch로 감싸고 진단 줄만 남긴다.
//
// 배치: 씬에 수동 배치하지 않는다. GameBootstrapper(LoadMap)가 자기 GameObject에 AddComponent하고 맵마다 Initialize를 부른다.
//
// 진단 로그(WORKFLOW [4] — 개발 축, 머리 [SKILL-ZONE], Diag=SkillZone, DiagRev=1, Plan 5절 L14 · L15):
//   ZoneVfxStarted(루트 높이 · 타일 윗면 위인가 · 배율 · 뒤집힘 · 채널) · ZoneVfxStopped(경로 · 즉시 여부 · 확인 번호 일치) ·
//   ZoneVfxSkipped(NoPrefab · LoopContract는 Warn) · ZoneVfxFault(예외 — Warn). 모두 상태가 바뀌는 순간에만 남긴다.
//
// Presentation 레이어 — Application(이벤트 · UseCase · 값 객체 · GameLog · NetworkContext) + Core(ViewConverter) + Domain 의존.
//   서버 판정 · 구역 효과에는 손대지 않는 "표현 전용" 컴포넌트다.
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
    /// <summary>
    /// 지속 상태 구역 스킬의 발동 표현(확정 · 수신)을 받아 조준 지점 바닥에 구역 VFX를 지속 시간 동안 띄우는 화면 구독자.
    /// </summary>
    public sealed class SkillZoneVfxPresenter : MonoBehaviour
    {
        // 구역 하나의 재생 기록.
        private sealed class ActiveZoneVfx
        {
            public int Sequence;          // 구역 순번(= 발동 순번)
            public TeamId Team;           // 시전 팀(진단용)
            public int Slot;              // 시전 슬롯(진단용)
            public VfxPoolItem Item;      // 재생 중인 풀 항목
            public ulong Lease;           // 시작할 때 받은 확인 번호
            public float EndTime;         // 꼬리 정지할 게임 시간(Time.time 기준)
            public float StartTime;       // [진단] 시작 시각
        }

        // 의존성(Initialize로 주입).
        private SkillActivationUseCase _skillActivation; // 팀 · 슬롯 → 스킬 데이터(이 화면 빌드의 로드아웃)
        private EffectManager _effects;                  // VFX 시작 · 정지(풀 관리 포함)
        private float _groundYOffset = DefaultGroundYOffset;

        /// <summary> 지면 높이 기본값(GameConfig.SkillVfxGroundYOffset 초기값과 같은 0.06). </summary>
        public const float DefaultGroundYOffset = 0.06f;

        // 타일 메시 윗면 높이(실측 0.05 — SkillActivationVfxPresenter와 같은 값). 진단 비교 기준으로만 쓴다.
        private const float TileTopSurfaceY = 0.05f;

        // 지금 떠 있는 구역 VFX(구역 순번 → 기록).
        private readonly Dictionary<int, ActiveZoneVfx> _active = new Dictionary<int, ActiveZoneVfx>();
        private readonly List<int> _keyBuffer = new List<int>(4);

        // 경기가 끝났는가(끝난 뒤에는 이번 경기에서 새로 띄우지 않는다 — 다음 Initialize에서 풀린다).
        private bool _gameEnded;

        private IDisposable _confirmedSubscription;
        private IDisposable _receivedSubscription;
        private IDisposable _gameEndSubscription;

        private const string ChannelConfirmed = "Confirmed";
        private const string ChannelReceived = "Received";

        /// <summary>
        /// 의존성을 주입하고 이벤트 3개(발동 확정 · 발동 수신 · 경기 종료)를 구독한다.
        /// 맵을 불러올 때마다 GameBootstrapper가 호출한다(재호출 안전 — 이전 경기의 구역 VFX는 즉시 지운다).
        /// </summary>
        /// <param name="skillActivation">이번 맵의 스킬 발동 UseCase(스킬 데이터 조회용). null이면 띄우지 않는다.</param>
        /// <param name="effects">VFX 매니저. null이면 띄우지 않는다.</param>
        /// <param name="groundYOffset">지면 높이(GameConfig.SkillVfxGroundYOffset). NaN · 무한대면 기본값 0.06.</param>
        public void Initialize(SkillActivationUseCase skillActivation, EffectManager effects, float groundYOffset)
        {
            DisposeSubscriptions();
            // 이전 맵(경기)의 구역 VFX가 남아 있으면 즉시 지운다(재경기 — Plan Z-8 · Z-9 표 「재초기화 = 즉시」).
            StopAll(immediate: true, path: "Reinit");

            _skillActivation = skillActivation;
            _effects = effects;
            _groundYOffset = float.IsNaN(groundYOffset) || float.IsInfinity(groundYOffset) ? DefaultGroundYOffset : groundYOffset;
            _gameEnded = false;

            // 채널 이름을 진단 로그에 남기려고 블록 본문 람다로 감싼다(처리 자체는 두 채널이 같다 — LogRules 1.7 람다 주의).
            _confirmedSubscription = GameEvents.OnSkillActivationConfirmed.Subscribe(p => { OnSkillActivation(p, ChannelConfirmed); });
            _receivedSubscription = GameEvents.OnSkillActivationReceived.Subscribe(p => { OnSkillActivation(p, ChannelReceived); });
            _gameEndSubscription = GameEvents.OnGameEnd.Subscribe(OnGameEnd);
        }

        /// <summary> 파괴(씬 언로드) 시 정적 이벤트 구독을 해제하고 남은 이펙트를 지운다. </summary>
        private void OnDestroy()
        {
            DisposeSubscriptions();
            StopAll(immediate: true, path: "Destroy");
        }

        private void DisposeSubscriptions()
        {
            _confirmedSubscription?.Dispose();
            _confirmedSubscription = null;
            _receivedSubscription?.Dispose();
            _receivedSubscription = null;
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = null;
        }

        // ====================================================================
        // 발동 알림 처리
        // ====================================================================

        private void OnSkillActivation(SkillActivationPresentation p, string channel)
        {
            try
            {
                HandleActivation(p, channel);
            }
            catch (Exception ex)
            {
                // 표현 전용 실패 — 서버 판정 · 구역 효과에는 영향이 없다.
                LogFault("Activation", channel, p.Sequence, ex);
            }
        }

        private void HandleActivation(SkillActivationPresentation p, string channel)
        {
            if (this == null || _gameEnded) return;
            if (_skillActivation == null || _effects == null) return;

            // 팀 · 슬롯으로 이 화면 빌드의 스킬 데이터를 찾는다(못 찾으면 발동 VFX 구독자가 이미 NoSkillData Warn을 남긴다).
            SkillData skill = _skillActivation.FindSkillData(p.Team, p.Slot);
            if (skill == null || skill.Mechanic != SkillMechanicType.PersistentStatusZone) return;
            // 구역이 성립하지 않는 발동(조준 없음 · 지속 0) — 서버도 구역을 만들지 않는다.
            if (!p.HasAim || !(skill.Duration > 0f)) return;

            GameObject prefab = skill.ZoneVfxPrefab;
            if (prefab == null)
            {
                // [진단 L14] 칸이 비어 있다 — 효과는 그대로 동작하고 화면에만 바닥 이펙트가 없다(발동 1건당 화면마다 1줄).
                LogSkipped(p, channel, "NoPrefab", null);
                return;
            }

            // 같은 순번이 이미 떠 있으면(비정상 — 같은 알림 두 번) 새로 띄우지 않는다.
            if (_active.ContainsKey(p.Sequence)) return;

            // 위치 = 조준 좌표 → 이 화면 좌표 + 지면 높이. 크기 = 반경 ÷ 기준 반경(Z-13 공유).
            Vector3 viewPosition = ViewConverter.ToView(p.AimWorld);
            viewPosition.y += _groundYOffset;
            float scale = EffectManager.ComputeSkillVfxScale(skill.Radius, skill.VfxReferenceRadius);

            VfxPoolItem item = _effects.StartSkillZoneVfx(prefab, viewPosition, scale, out ulong lease, out string failReason);
            if (item == null || lease == 0)
            {
                LogSkipped(p, channel, failReason ?? "NotStarted", prefab.name);
                return;
            }

            float now = Time.time;
            _active[p.Sequence] = new ActiveZoneVfx
            {
                Sequence = p.Sequence, Team = p.Team, Slot = p.Slot, Item = item, Lease = lease,
                EndTime = now + skill.Duration, StartTime = now
            };
            // [진단 L15] 시작 1줄(루트 높이 · 타일 윗면 위 · 배율 · 뒤집힘 · 채널).
            LogStarted(p, channel, prefab.name, skill, scale, viewPosition);
        }

        // ====================================================================
        // 매 프레임 — 만료
        // ====================================================================

        /// <summary> 구역 시간이 지난 이펙트를 꼬리 정지한다. 기록이 없으면 바로 돌아간다(비용 없음). </summary>
        private void LateUpdate()
        {
            if (_active.Count == 0) return;
            try
            {
                float now = Time.time;
                _keyBuffer.Clear();
                foreach (KeyValuePair<int, ActiveZoneVfx> pair in _active)
                    if (now >= pair.Value.EndTime) _keyBuffer.Add(pair.Key);
                for (int i = 0; i < _keyBuffer.Count; i++)
                {
                    ActiveZoneVfx entry = _active[_keyBuffer[i]];
                    _active.Remove(_keyBuffer[i]);
                    StopEntry(entry, immediate: false, path: "Expire");
                }
                _keyBuffer.Clear();
            }
            catch (Exception ex)
            {
                LogFault("Expire", null, 0, ex);
                _keyBuffer.Clear();
            }
        }

        // ====================================================================
        // 정지 경로
        // ====================================================================

        private void OnGameEnd(GameEndEvent e)
        {
            try
            {
                if (this == null) return;
                _gameEnded = true;
                // [진단 L11] 경기 종료 — 꼬리 정지(Z-8).
                StopAll(immediate: false, path: "GameEnd");
            }
            catch (Exception ex)
            {
                LogFault("GameEnd", null, 0, ex);
            }
        }

        private void StopAll(bool immediate, string path)
        {
            if (_active.Count == 0) return;
            foreach (KeyValuePair<int, ActiveZoneVfx> pair in _active) StopEntry(pair.Value, immediate, path);
            _active.Clear();
        }

        private void StopEntry(ActiveZoneVfx entry, bool immediate, string path)
        {
            if (entry == null) return;
            bool stopped = _effects != null
                ? _effects.StopSkillZoneVfx(entry.Item, entry.Lease, immediate)
                : (entry.Item != null && entry.Item.StopStatusLoop(entry.Lease, immediate));
            entry.Item = null; // 정지 뒤에는 그 항목을 다시 건드리지 않는다(풀로 돌아가 남의 재생이 될 수 있다).
            LogStopped(entry, path, immediate, stopped);
        }

        // ====================================================================
        // 진단 로그(개발 축 — 릴리스 빌드에서는 호출 · 문자열 조립까지 통째로 사라진다, LogRules 1.7)
        // ====================================================================

        private static string DiagRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        private static int DiagMilli(float value) => Mathf.RoundToInt(value * 1000f);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStarted(SkillActivationPresentation p, string channel, string prefabName, SkillData skill,
                                       float scale, Vector3 viewPosition)
        {
            GameLog.Dev.Info("Skill", nameof(SkillZoneVfxPresenter), "[SKILL-ZONE] ZoneVfxStarted",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=ZoneVfxStarted, Role={DiagRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Prefab={prefabName}, Radiusmm={DiagMilli(skill.Radius)}, RefRadiusmm={DiagMilli(skill.VfxReferenceRadius)}, Scale={DiagMilli(scale)}, DurationMs={DiagMilli(skill.Duration)}, ViewFlipped={ViewConverter.IsFlipped}, AimXmm={DiagMilli(p.AimWorld.x)}, AimZmm={DiagMilli(p.AimWorld.z)}, ViewXmm={DiagMilli(viewPosition.x)}, ViewZmm={DiagMilli(viewPosition.z)}, RootYmm={DiagMilli(viewPosition.y)}, TileTopYmm={DiagMilli(TileTopSurfaceY)}, AboveTileTop={viewPosition.y > TileTopSurfaceY}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStopped(ActiveZoneVfx entry, string path, bool immediate, bool leaseMatched)
        {
            GameLog.Dev.Info("Skill", nameof(SkillZoneVfxPresenter), "[SKILL-ZONE] ZoneVfxStopped",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=ZoneVfxStopped, Role={DiagRole()}, Seq={entry.Sequence}, Team={entry.Team}, Slot={entry.Slot}, Path={path}, Immediate={immediate}, LeaseMatched={leaseMatched}, ActiveMs={DiagMilli(Time.time - entry.StartTime)}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSkipped(SkillActivationPresentation p, string channel, string reason, string prefabName)
        {
            GameLog.Dev.Warn("Skill", nameof(SkillZoneVfxPresenter), "[SKILL-ZONE] ZoneVfxSkipped",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=ZoneVfxSkipped, Role={DiagRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Reason={reason}, Prefab={(prefabName ?? "None")}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFault(string stage, string channel, int sequence, Exception ex)
        {
            GameLog.Dev.Warn("Skill", nameof(SkillZoneVfxPresenter), "[SKILL-ZONE] ZoneVfxFault",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=ZoneVfxFault, Role={DiagRole()}, Stage={stage}, Channel={(channel ?? "None")}, Seq={sequence}, Error={ex.GetType().Name}"));
        }
    }
}
