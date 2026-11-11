// ============================================================================
// UnitBuffVfxPresenter.ZoneDriver.cs
// 유닛 상태 VFX 화면 구독자의 「구역 구성원 구동기」 — 지속 상태 구역(서리 감옥) 안 유닛 몸에 얼음 이펙트(V4)를 붙이고 뗀다.
// 2026-11-11 지속 상태 구역 Task(_Tasks/2026-11-10/16_02_support-skill-persistent-zones) 구현 단위 ④(사용자 결정 Z-21 (가)).
//
// 무엇을 하나(초급자용 설명):
//   서리 감옥 구역 안에 들어온 적 유닛은 얼어붙는다(서버 판정). 이 구동기는 그 유닛 몸에 스킬 자산 「유닛 상태 VFX」 칸의
//   프리팹(서리 감옥이면 얼음 결정 V4)을 붙이고, 구역에서 나가거나 구역이 끝나면 뗀다.
//   붙이기 · 따라가기 · 정지 · 사망 처리 자체는 공통부(UnitBuffVfxPresenter.cs)를 그대로 쓴다 — 강화 스킬과 같은 코드다.
//
// 언제 붙이고 떼나(Plan 2절 Z-8 · Z-9 표):
//   - 구역 생성: 스킬 발동 알림(확정 · 수신)으로 구역을 "등록"만 한다(구역 순번 · 팀 · 슬롯 · 프리팹 · 끝 시각 = 받은 순간 + 지속).
//   - 들어옴: 서버의 구역 구성원 변경 알림(GameEvents.OnSkillZoneMembership* — 확정 · 수신)의 EnteredIds → 붙인다.
//   - 나감:   같은 알림의 ExitedIds → 꼬리 정지(Path=Exit — Z-9). 그 유닛이 이미 죽었으면 즉시 정지(Path=UnitDied).
//   - 구역 끝: 등록한 끝 시각이 지나면 남은 구성원을 꼬리 정지(Path=Expire). 서버는 끝 알림을 보내지 않는다(Z-2 N-B).
//   - 유닛 사망 · 경기 종료 · 재경기: 공통부가 처리한다(사망 = 즉시, 경기 종료 = 꼬리, 재경기 = 즉시).
//   - 같은 스킬 구역이 겹침(같은 팀 · 같은 슬롯): 한 유닛에 이펙트는 하나만 붙이고, 그 유닛이 들어 있는 구역 순번을 센다(ZoneRefs).
//     하나라도 남아 있으면 계속 붙어 있고, 마지막 구역에서 나가거나 끝날 때 뗀다(Z-5 — 효과도 1번만).
//
// 순서(위험 R-10): 서버는 첫 구성원을 발동 다음 틱에 보내고, 같은 NetworkObject의 ClientRpc는 보낸 순서대로 도착하므로
//   구성원 알림이 발동 알림보다 먼저 오는 일은 없어야 한다. 그래도 모르는 구역 순번이 오면 Warn 1줄(ZoneMembershipUnknown)을 남기고 무시한다.
//
// 진단 로그(개발 축 — 머리 [SKILL-ZONE], Diag=SkillZone, DiagRev=1, Plan 5절 L16 · L17):
//   UnitZoneVfxStarted(구성원 변경 1건의 화면 요약) · UnitZoneVfxStopped(정지 — 같은 프레임 · 같은 경로 1줄) ·
//   UnitZoneVfxSkipped(NoPrefab · LoopContract — 구역 1건당 화면마다 Warn) · UnitZoneVfxPendingResolved · ZoneMembershipUnknown · UnitZoneVfxFault.
//
// 예외 안전: Host · 싱글에서는 구성원 「확정」이 서버 권위 틱(SkillZoneSystem.Tick) 안에서 동기로 발행된다 → 모든 처리를 try/catch로 감싼다.
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
    public sealed partial class UnitBuffVfxPresenter
    {
        // 이 화면에 등록된 구역 하나(발동 알림으로 등록, 끝 시각에 지운다).
        private sealed class ZoneRecord
        {
            public int Sequence;            // 구역 순번(= 발동 순번)
            public TeamId Team;             // 시전 팀(기록 열쇠의 팀)
            public int Slot;                // 시전 슬롯(기록 열쇠의 슬롯)
            public float EndTime;           // 구역이 끝나는 게임 시간(받은 순간 + 지속 — 로컬 타이머)
            public float StartTime;         // [진단] 등록 시각
            public GameObject Prefab;       // 유닛 상태 VFX 프리팹(없으면 null)
            public float Scale;             // 스킬 데이터 크기 배율
            public Vector3 Offset;          // 스킬 데이터 위치 보정
            public bool Usable;             // 프리팹이 있고 계약을 지키는가(false면 효과만 — 붙이지 않는다)
            public string PrefabName;       // [진단] 프리팹 이름
            public readonly HashSet<int> Members = new HashSet<int>(); // 이 화면이 알고 있는 구역 안 유닛 Id
        }

        // 구역 순번 → 기록.
        private readonly Dictionary<int, ZoneRecord> _zoneRecords = new Dictionary<int, ZoneRecord>();
        private readonly List<int> _zoneKeyBuffer = new List<int>(4);

        private IDisposable _zoneConfirmedSubscription;
        private IDisposable _zoneReceivedSubscription;

        // 공통부 Initialize가 부른다 — 이전 경기의 구역 기록을 비운다(구성원 이펙트는 공통부 StopAll이 이미 지웠다).
        private void ResetZoneDriver()
        {
            _zoneRecords.Clear();
        }

        // 공통부 Initialize가 부른다 — 구성원 변경 두 채널을 구독한다(채널 이름을 진단에 남기려고 블록 본문 람다).
        private void SubscribeZoneDriver()
        {
            _zoneConfirmedSubscription = GameEvents.OnSkillZoneMembershipConfirmed.Subscribe(p => { OnZoneMembership(p, ChannelConfirmed); });
            _zoneReceivedSubscription = GameEvents.OnSkillZoneMembershipReceived.Subscribe(p => { OnZoneMembership(p, ChannelReceived); });
        }

        // 공통부 DisposeSubscriptions가 부른다.
        private void DisposeZoneDriverSubscriptions()
        {
            _zoneConfirmedSubscription?.Dispose();
            _zoneConfirmedSubscription = null;
            _zoneReceivedSubscription?.Dispose();
            _zoneReceivedSubscription = null;
        }

        // ====================================================================
        // 구역 등록(발동 알림 — 공통부 HandleActivation이 부른다)
        // ====================================================================

        /// <summary>
        /// 지속 상태 구역 스킬의 발동 알림을 받아 이 화면에 구역을 등록한다(붙이기는 구성원 알림이 왔을 때).
        /// 유닛 상태 VFX 칸이 비었거나 계약을 어기면 등록은 하되 붙이지 않고 Warn 1줄을 남긴다(L17).
        /// </summary>
        private void RegisterZone(SkillActivationPresentation p, string channel, SkillData skill)
        {
            if (!(skill.Duration > 0f) || _zoneRecords.ContainsKey(p.Sequence)) return;

            float now = Time.time;
            var zone = new ZoneRecord
            {
                Sequence = p.Sequence, Team = p.Team, Slot = p.Slot, EndTime = now + skill.Duration, StartTime = now,
                Prefab = skill.StatusVfxPrefab, Scale = skill.StatusVfxScale, Offset = skill.StatusVfxOffset,
                PrefabName = skill.StatusVfxPrefab != null ? skill.StatusVfxPrefab.name : null
            };
            if (zone.Prefab == null)
            {
                // [진단 L17] 칸이 비어 있다 — 구역 효과는 그대로 동작하고 유닛 몸에만 이펙트가 없다(수액 늪은 일부러 비워 둔다 — Z-14).
                LogZoneUnitSkipped(p, channel, (StatusEffectKind)skill.StatusKind, "NoPrefab", null);
            }
            else if (!_effects.IsUnitBuffLoopContractSatisfied(zone.Prefab))
            {
                LogZoneUnitSkipped(p, channel, (StatusEffectKind)skill.StatusKind, "LoopContract", zone.PrefabName);
            }
            else
            {
                zone.Usable = true;
            }
            _zoneRecords[p.Sequence] = zone;
        }

        // ====================================================================
        // 구성원 변경(들어옴 → 붙임, 나감 → 꼬리 정지)
        // ====================================================================

        private void OnZoneMembership(SkillZoneMembershipPresentation p, string channel)
        {
            try
            {
                HandleZoneMembership(p, channel);
            }
            catch (Exception ex)
            {
                // 표현 전용 실패 — 서버 구역 효과에는 영향이 없다.
                LogZoneFault("Membership", channel, p.Sequence, ex);
            }
        }

        private void HandleZoneMembership(SkillZoneMembershipPresentation p, string channel)
        {
            if (this == null || _gameEnded) return;
            if (_effects == null || _units == null || _pose == null) return;

            if (!_zoneRecords.TryGetValue(p.Sequence, out ZoneRecord zone))
            {
                // 이 화면이 모르는 구역(발동 알림을 못 받았거나 이미 끝난 구역) — 정상이면 0건(위험 R-10).
                LogZoneMembershipUnknown(p, channel);
                return;
            }

            // ① 나감 — 그 유닛의 이 구역 참조를 뺀다. 마지막 참조였으면 이펙트를 뗀다(살아 있으면 꼬리 — Z-9, 죽었으면 즉시).
            IReadOnlyList<int> exited = p.ExitedIds;
            for (int i = 0; i < exited.Count; i++)
            {
                int unitId = exited[i];
                zone.Members.Remove(unitId);
                ReleaseZoneRef(zone, unitId, "Exit");
            }

            // ② 들어옴 — 처음이면 붙이고, 이미 같은 스킬 구역 때문에 붙어 있으면 참조만 더한다(Z-5).
            IReadOnlyList<int> entered = p.EnteredIds;
            int attached = 0, joined = 0, pending = 0, failed = 0, notAlive = 0, fitMissing = 0, poolCreated = 0;
            string firstFail = null;
            float now = Time.time;
            for (int i = 0; i < entered.Count; i++)
            {
                int unitId = entered[i];
                zone.Members.Add(unitId);
                if (!zone.Usable) continue;

                UnitData unit = _units.GetUnit(unitId);
                if (unit == null || !unit.IsAlive) { notAlive++; continue; }

                var key = new BuffKey(unitId, zone.Team, zone.Slot);
                if (_active.TryGetValue(key, out ActiveBuffVfx current))
                {
                    bool sameUnit = ReferenceEquals(current.Unit, unit);
                    bool playing = current.Item != null && current.Item.IsStatusLoopActive(current.Lease);
                    if (current.Driver == VfxDriver.Zone && sameUnit && (playing || current.Pending))
                    {
                        // 같은 스킬의 다른 구역에도 이미 들어 있다 — 다시 시작하지 않고 참조만 더한다.
                        current.ZoneRefs.Add(zone.Sequence);
                        joined++;
                        continue;
                    }
                    // 옛 기록(재생이 끝났거나 · 다른 유닛 · 다른 구동기) — 지우고 새로 시작한다.
                    _active.Remove(key);
                    StopAndGroup(key, current, immediate: true, path: "Replaced");
                }

                var entry = new ActiveBuffVfx
                {
                    Unit = unit, EndTime = float.PositiveInfinity, StartTime = now, Sequence = zone.Sequence,
                    Prefab = zone.Prefab, Scale = zone.Scale, Offset = zone.Offset,
                    Driver = VfxDriver.Zone, ZoneRefs = new HashSet<int> { zone.Sequence }
                };
                StartOutcome outcome = TryStart(entry, out bool fitFound, out bool createdNew, out string failReason);
                if (outcome == StartOutcome.Started)
                {
                    attached++;
                    if (!fitFound) fitMissing++;
                    if (createdNew) poolCreated++;
                    _active[key] = entry;
                }
                else if (outcome == StartOutcome.Pending)
                {
                    pending++;
                    _active[key] = entry; // 몸을 찾으면 공통부 LateUpdate가 다시 시작을 시도한다.
                }
                else
                {
                    failed++;
                    if (firstFail == null) firstFail = failReason;
                }
            }

            // [진단 L16] 이 화면의 구성원 변경 요약 1줄(들어온 유닛이 있을 때만).
            if (entered.Count > 0)
                LogZoneUnitStarted(zone, channel, entered.Count, exited.Count, attached, joined, pending, failed, notAlive,
                                   fitMissing, poolCreated, firstFail);
            FlushStopGroups();
        }

        /// <summary>
        /// 유닛 하나에서 이 구역의 참조를 뺀다. 남은 같은 스킬 구역이 없으면 이펙트를 뗀다.
        /// 유닛이 이미 죽었으면 즉시(UnitDied), 살아 있으면 주어진 경로로 꼬리 정지(Exit · Expire).
        /// </summary>
        private void ReleaseZoneRef(ZoneRecord zone, int unitId, string tailPath)
        {
            var key = new BuffKey(unitId, zone.Team, zone.Slot);
            if (!_active.TryGetValue(key, out ActiveBuffVfx entry) || entry.Driver != VfxDriver.Zone || entry.ZoneRefs == null) return;
            entry.ZoneRefs.Remove(zone.Sequence);
            if (entry.ZoneRefs.Count > 0) return; // 다른 같은 스킬 구역에 아직 들어 있다(Z-5)
            _active.Remove(key);
            bool alive = entry.Unit != null && entry.Unit.IsAlive;
            if (alive) StopAndGroup(key, entry, immediate: false, path: tailPath);
            else StopAndGroup(key, entry, immediate: true, path: "UnitDied");
        }

        // ====================================================================
        // 구역 끝(로컬 타이머 — 공통부 LateUpdate가 매 프레임 부른다)
        // ====================================================================

        private void TickZoneRecords(float now)
        {
            try
            {
                _zoneKeyBuffer.Clear();
                foreach (KeyValuePair<int, ZoneRecord> pair in _zoneRecords)
                    if (now >= pair.Value.EndTime) _zoneKeyBuffer.Add(pair.Key);
                if (_zoneKeyBuffer.Count == 0) return;

                for (int i = 0; i < _zoneKeyBuffer.Count; i++)
                {
                    ZoneRecord zone = _zoneRecords[_zoneKeyBuffer[i]];
                    _zoneRecords.Remove(_zoneKeyBuffer[i]);
                    foreach (int unitId in zone.Members) ReleaseZoneRef(zone, unitId, "Expire");
                }
                _zoneKeyBuffer.Clear();
                FlushStopGroups();
            }
            catch (Exception ex)
            {
                LogZoneFault("Expire", null, 0, ex);
                _zoneKeyBuffer.Clear();
            }
        }

        // ====================================================================
        // 진단 로그(개발 축 — [SKILL-ZONE], 릴리스 빌드에서는 호출 · 문자열 조립까지 사라진다)
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneUnitSkipped(SkillActivationPresentation p, string channel, StatusEffectKind kind, string reason,
                                               string prefabName)
        {
            GameLog.Dev.Warn("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-ZONE] UnitZoneVfxSkipped",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=UnitZoneVfxSkipped, Role={DiagRole()}, Channel={channel}, Seq={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Kind={kind}, Reason={reason}, Prefab={(prefabName ?? "None")}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneUnitStarted(ZoneRecord zone, string channel, int entered, int exited, int attached, int joined,
                                               int pending, int failed, int notAlive, int fitMissing, int poolCreated, string firstFail)
        {
            GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-ZONE] UnitZoneVfxStarted",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=UnitZoneVfxStarted, Role={DiagRole()}, Channel={channel}, Seq={zone.Sequence}, Team={zone.Team}, Slot={zone.Slot}, Prefab={(zone.PrefabName ?? "None")}, Usable={zone.Usable}, Entered={entered}, Exited={exited}, Attached={attached}, JoinedOverlap={joined}, PosePending={pending}, StartFailed={failed}, NotAlive={notAlive}, FailReason={(firstFail ?? "None")}, FitMissing={fitMissing}, PoolCreated={poolCreated}, ScaleMilli={DiagMilli(zone.Scale)}, ViewFlipped={ViewConverter.IsFlipped}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneStopGroup(StopGroup g)
        {
            GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-ZONE] UnitZoneVfxStopped",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=UnitZoneVfxStopped, Role={DiagRole()}, Team={g.CasterTeam}, Slot={g.Slot}, Seq={g.Sequence}, Path={g.Path}, Immediate={g.Immediate}, Count={g.Count}, LeaseMatched={g.LeaseMatched}, MaxActiveMs={g.MaxActiveMs}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogZonePendingResolved(int unitId, ActiveBuffVfx entry, bool fitFound)
        {
            GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-ZONE] UnitZoneVfxPendingResolved",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=UnitZoneVfxPendingResolved, Role={DiagRole()}, Seq={entry.Sequence}, UnitId={unitId}, WaitedMs={DiagMilli(Time.time - entry.StartTime)}, FitFound={fitFound}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneMembershipUnknown(SkillZoneMembershipPresentation p, string channel)
        {
            GameLog.Dev.Warn("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-ZONE] ZoneMembershipUnknown",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=ZoneMembershipUnknown, Role={DiagRole()}, Channel={channel}, Seq={p.Sequence}, Entered={p.EnteredCount}, Exited={p.ExitedCount}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneFault(string stage, string channel, int sequence, Exception ex)
        {
            GameLog.Dev.Warn("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-ZONE] UnitZoneVfxFault",
                FormattableString.Invariant(
                    $"Diag=SkillZone, DiagRev=1, Event=UnitZoneVfxFault, Role={DiagRole()}, Stage={stage}, Channel={(channel ?? "None")}, Seq={sequence}, Error={ex.GetType().Name}"));
        }
    }
}
