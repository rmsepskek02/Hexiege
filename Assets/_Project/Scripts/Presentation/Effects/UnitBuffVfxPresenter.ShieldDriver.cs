// ============================================================================
// UnitBuffVfxPresenter.ShieldDriver.cs
// 유닛 상태 VFX 구독자의 「쉴드 깨짐 → 그 유닛만 즉시 정지」 진입점(UnitBuffVfxPresenter의 partial 조각).
// 2026-11-16 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 C — 구현 단위 C-3, Plan 6-1-3.
//
// 무엇을 하나(초급자용 설명):
//   신목의 각성 유닛 쉴드 이펙트는 기존 「발동 구동기」가 그대로 붙인다(발동 알림 → 시전 팀 유닛마다 · 끝 = 받은 순간 + 8초).
//   그런데 쉴드는 시간이 남아 있어도 다 깎이면(깨지면) 끝난다. 서버만 쉴드 양을 알기 때문에, 서버가 「깨진 유닛 Id 목록」을
//   알려 주면(GameEvents.OnShieldBrokenConfirmed — 싱글 · Host / OnShieldBrokenReceived — 순수 Client) 이 조각이
//   그 유닛의 이펙트만 「즉시」 정지한다(Path=ShieldBroken, Immediate=True). 다른 유닛 · 다른 스킬의 이펙트는 건드리지 않는다.
//
// 왜 발동 순번을 비교하나:
//   깨진 바로 뒤 같은 유닛에 새 쉴드가 걸렸다면(같은 종족 스킬 건물이 둘 — 다시 사용), 기록의 순번은 이미 새 발동으로 바뀌어 있다
//   (재사용 연장 때 Sequence를 새 값으로 바꾼다). 옛 순번의 깨짐 알림이 늦게 와도 새 쉴드 이펙트를 끄지 않게 순번이 같은 기록만 끈다.
//
// 진단(WORKFLOW [4] — 개발 축): 정지 자체는 기존 [SKILL-BUFF] BuffVfxStopped(Path=ShieldBroken) 묶음 줄로 남고,
//   알림 1건마다 [SKILL-SHIELD] UnitShieldVfxBrokenStop 요약 1줄(요청 수 · 실제 정지 수 · 기록 없음 수)을 더 남긴다(C-L9).
//
// Presentation 레이어 — Application(이벤트 · 값 객체 · GameLog).
// ============================================================================

using System;
using System.Collections.Generic;
using UniRx;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    public sealed partial class UnitBuffVfxPresenter
    {
        // 쉴드 깨짐 두 채널 구독 핸들(Initialize · OnDestroy에서 정리).
        private IDisposable _shieldBrokenConfirmedSubscription;
        private IDisposable _shieldBrokenReceivedSubscription;
        // 깨짐 처리 전용 키 버퍼(다른 순회 버퍼와 섞이지 않게 따로 둔다 — 매번 new 하지 않음).
        private readonly List<BuffKey> _shieldKeyBuffer = new List<BuffKey>();

        // Initialize에서 부른다 — 이전 구독을 먼저 풀고 두 채널을 다시 건다.
        private void SubscribeShieldDriver()
        {
            DisposeShieldDriverSubscriptions();
            _shieldBrokenConfirmedSubscription = GameEvents.OnShieldBrokenConfirmed.Subscribe(p => { OnShieldBroken(p, ChannelConfirmed); });
            _shieldBrokenReceivedSubscription = GameEvents.OnShieldBrokenReceived.Subscribe(p => { OnShieldBroken(p, ChannelReceived); });
        }

        private void DisposeShieldDriverSubscriptions()
        {
            _shieldBrokenConfirmedSubscription?.Dispose();
            _shieldBrokenConfirmedSubscription = null;
            _shieldBrokenReceivedSubscription?.Dispose();
            _shieldBrokenReceivedSubscription = null;
        }

        /// <summary>
        /// 쉴드 깨짐 알림 1건: 깨진 유닛마다 같은 발동 순번으로 붙인 발동 구동기 기록을 찾아 즉시 정지한다.
        /// Host · 싱글에서는 서버 권위 틱 안에서 동기로 불리므로 예외를 밖으로 던지지 않는다.
        /// </summary>
        /// <param name="p">깨진 유닛 · 건물 목록(이 조각은 유닛만 본다 — 건물은 BuildingStatusVfxPresenter).</param>
        /// <param name="channel">들어온 채널(진단용 — Confirmed 또는 Received).</param>
        private void OnShieldBroken(ShieldBrokenPresentation p, string channel)
        {
            try
            {
                if (this == null || p.UnitCount == 0) return;
                int requested = p.UnitCount, stopped = 0, notFound = 0;
                IReadOnlyList<int> ids = p.UnitIds;
                for (int i = 0; i < ids.Count; i++)
                {
                    int unitId = ids[i];
                    _shieldKeyBuffer.Clear();
                    foreach (KeyValuePair<BuffKey, ActiveBuffVfx> pair in _active)
                    {
                        ActiveBuffVfx entry = pair.Value;
                        // 같은 유닛 · 발동 구동기 기록 · 같은 발동 순번만(구역 구동기 기록과 다른 발동의 이펙트는 건드리지 않는다).
                        if (pair.Key.UnitId == unitId && entry.Driver == VfxDriver.Activation && entry.Sequence == p.Sequence)
                            _shieldKeyBuffer.Add(pair.Key);
                    }
                    if (_shieldKeyBuffer.Count == 0) { notFound++; continue; }
                    for (int k = 0; k < _shieldKeyBuffer.Count; k++)
                    {
                        BuffKey key = _shieldKeyBuffer[k];
                        if (!_active.TryGetValue(key, out ActiveBuffVfx entry)) continue;
                        _active.Remove(key);
                        // 즉시 정지(입자까지 지움) — 깨진 쉴드가 남아 보이지 않게(Plan 6-1-3 「깨짐 = 즉시」).
                        StopAndGroup(key, entry, immediate: true, path: "ShieldBroken");
                        stopped++;
                    }
                }
                _shieldKeyBuffer.Clear();
                FlushStopGroups();
                LogUnitShieldVfxBrokenStop(channel, p.Sequence, requested, stopped, notFound);
            }
            catch (Exception ex)
            {
                // 표현 전용 실패 — 서버 판정에는 영향이 없다.
                LogFault("ShieldBroken", channel, Hexiege.Domain.TeamId.Neutral, -1, p.Sequence, ex);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogUnitShieldVfxBrokenStop(string channel, int sequence, int requested, int stopped, int notFound)
        {
            GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-SHIELD] UnitShieldVfxBrokenStop",
                FormattableString.Invariant(
                    $"Diag=SkillShield, DiagRev=1, Event=UnitShieldVfxBrokenStop, Role={DiagRole()}, Channel={channel}, Seq={sequence}, Requested={requested}, Stopped={stopped}, NotFound={notFound}, Immediate=True"));
        }
    }
}
