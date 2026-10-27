// ============================================================================
// DeferredUnitDeathPresenter.cs
// [2026-10-26 서버 권위 투사체 — 단계 D 후속, 수정안 (a)] 투사체 사망타의 "화면 사망 표현"을
// 그 투사체가 화면에서 닿는 순간까지 미루는 화면 쪽 조율자.
//
// [무엇이 문제였나 — 유니티 초급자용]
//   Client는 투사체를 서버보다 약 0.10~0.13초 늦게 그린다. 그런데 사망 통보·NGO 디스폰은 받자마자 처리돼,
//   InfernoSpirit이 FoxMagician을 한 방에 죽이면 기기 화면에서 Fox가 투사체가 닿기 전에 사라지고 사망 이펙트가 먼저 나왔다.
//
// [어떻게 고치나 — 시각 대리체(사망 대역, ghost) 방식]
//   1) 사망 통보(OnUnitDied)를 받은 UnitView가 이 조율자에게 "미룰까?"를 묻는다(TryDefer).
//      이 대상에게 아직 화면에 안 나온 투사체 사망타(HitPresentationQueue 보관함의 HP 0 이하 번호표)가 있을 때만 미룬다.
//   2) 미루면 UnitView가 자기 Visual Root(메시·Animator가 든 자식)를 떼어 "사망 대역"으로 남긴다.
//      원래 유닛 오브젝트(NetworkObject)는 예전처럼 NGO 디스폰·Destroy로 즉시 사라진다 — 서버·도메인·타겟팅·관찰자 Retire는 무변경.
//   3) 그 번호표의 피해 숫자가 화면에 나오는 순간(HitPresentationQueue.ProjectileHitShown) 대역이
//      예전과 같은 사망 표현(차지 VFX 정지 → IsDead → 사망 VFX·SFX → 소멸)을 한 번 한다.
//   4) 상한 0.3초를 넘기면 착탄을 기다리지 않고 진행한다. 게임 종료면 남은 것을 지금 진행하고,
//      맵 재초기화(재경기)·파괴면 표현 없이 대역만 치운다.
//
// [이 컴포넌트가 하지 않는 것]
//   HP·피해·사망 판정·도메인 제거·서버 Despawn 시각·타겟 선택을 읽거나 바꾸지 않는다(표현 전용).
//   비투사체 사망(근접·즉발·DoT 틱)은 번호표가 없어 TryDefer가 false — 기존 즉시 경로 그대로다.
//
// [진단] 경기 종료(또는 재초기화) 때 [PROJECTILE-DEATH-HOLD] side=view 1줄(Dev 전용).
//
// 씬에 배치하지 않는다 — GameBootstrapper(조합 루트)가 AddComponent 후 Initialize한다. Presentation 레이어(MonoBehaviour).
// ============================================================================

using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 미뤄 둔 사망 표현 1건의 주인(UnitView가 구현). 조율자는 "언제"만 정하고 실제 재생은 주인이 한다.
    /// </summary>
    public interface IDeferredUnitDeathOwner
    {
        /// <summary>미뤄 둔 사망 표현을 지금 1회 진행한다(차지 정지 · IsDead · 사망 VFX·SFX · 대역 소멸).</summary>
        /// <param name="reason">진행 사유 토큰(at-impact · cap · game-end).</param>
        void ReleaseDeferredDeathPresentation(string reason);

        /// <summary>표현 없이 대역만 치운다(맵 재초기화·파괴).</summary>
        void DiscardDeferredDeathPresentation();
    }

    /// <summary>
    /// 투사체 사망타 사망 표현 보류 조율자. 판단 규칙은 순수 C# 일정표(ProjectileKillPresentationSchedule)에 있다.
    /// </summary>
    public sealed class DeferredUnitDeathPresenter : MonoBehaviour
    {
        /// <summary>
        /// 지금 경기의 조율자(없으면 null — UnitView는 기존 즉시 경로를 탄다).
        /// EffectManager.Instance와 같은 "조합 루트가 만든 화면 서비스" 접근 방식이다.
        /// </summary>
        public static DeferredUnitDeathPresenter Current { get; private set; }

        private readonly ProjectileKillPresentationSchedule<IDeferredUnitDeathOwner> _schedule =
            new ProjectileKillPresentationSchedule<IDeferredUnitDeathOwner>();
        private readonly List<ProjectileDeliveryKey> _keys = new List<ProjectileDeliveryKey>(2);
        private readonly List<IDeferredUnitDeathOwner> _released = new List<IDeferredUnitDeathOwner>();
        private HitPresentationQueue _hits;
        private IDisposable _endSubscription;
        private bool _ended;
        private bool _diagnosticWritten;
        private int _notReady, _noVisualRoot, _faults;
        private Func<double> _clock = () => Time.realtimeSinceStartupAsDouble;

        /// <summary>지금 미뤄 둔 사망 수(진단·검증용).</summary>
        public int HeldCount => _schedule.HeldCount;

        /// <summary>
        /// 조합 루트가 맵을 로드할 때마다 호출한다. 이전 경기의 미룬 표현은 표현 없이 치우고 새로 시작한다.
        /// </summary>
        /// <param name="hits">피격 표현 큐(사망타 번호표 조회 · 화면 방출 통보).</param>
        public void Initialize(HitPresentationQueue hits)
        {
            Detach("reinitialize");
            _hits = hits;
            _ended = false;
            _diagnosticWritten = false;
            _notReady = _noVisualRoot = _faults = 0;
            _schedule.ResetCounters();
            ProjectileAlignedTimedDamageDiagnostics.Reset();
            if (hits == null) return;
            hits.ProjectileHitShown += OnProjectileHitShown;
            _endSubscription = GameEvents.OnGameEnd.Subscribe(_ => OnGameEnd());
            Current = this;
        }

        /// <summary>
        /// 사망 표현을 미룰지 묻는다. true면 호출자(UnitView)가 대역을 떼어 두고, 나중에 owner.Release…가 불린다.
        /// false면 호출자는 기존 즉시 경로를 그대로 탄다.
        /// </summary>
        /// <param name="unit">사망한 유닛 데이터(객체 참조로 대상 식별).</param>
        /// <param name="owner">미룬 표현의 주인.</param>
        /// <param name="ghostAvailable">떼어 낼 Visual Root가 있는가(없으면 미루지 않는다).</param>
        public bool TryDefer(UnitData unit, IDeferredUnitDeathOwner owner, bool ghostAvailable)
        {
            if (_ended || _hits == null || unit == null || owner == null) { _notReady++; return false; }
            _keys.Clear();
            _hits.CollectHeldKillShotKeys(unit, _keys);
            // 사망타가 보관 중인데 대역을 만들 수 없으면(옛 프리팹) 미루지 않는다 — 사라짐 없이 사망 표현만 늦출 수는 없다.
            if (_keys.Count > 0 && !ghostAvailable) { _noVisualRoot++; _keys.Clear(); }
            bool deferred = _schedule.TryDefer(unit, owner, _keys, _clock());
            _keys.Clear();
            return deferred;
        }

        private void OnProjectileHitShown(ProjectileDeliveryKey key, EntityDamagedEvent evt)
        {
            // 사망타(유닛, 남은 HP 0 이하)만 본다. 살아 있는 대상의 숫자는 사망 표현과 무관하다.
            if (!evt.IsUnit || evt.CurrentHp > 0 || !(evt.Entity is UnitData)) return;
            _released.Clear();
            _schedule.NotifyKillShotShown(key, evt.Entity, _clock(), _released);
            ReleaseCollected("at-impact");
        }

        private void Update() => Tick(_clock());

        private void Tick(double now)
        {
            if (_hits == null) return;
            _released.Clear();
            _schedule.CollectExpired(now, _released);
            ReleaseCollected("cap");
        }

        private void OnGameEnd()
        {
            // 게임 종료 — 투사체 화면 소비자도 남은 투사체를 거둔다(착탄 없음). 미뤄 둔 사망은 지금 진행한다(사망 자체는 일어났다).
            _ended = true;
            _released.Clear();
            _schedule.ReleaseAll(_clock(), _released);
            ReleaseCollected("game-end");
            WriteDiagnostic("game-end");
        }

        private void ReleaseCollected(string reason)
        {
            for (int i = 0; i < _released.Count; i++)
            {
                try { _released[i].ReleaseDeferredDeathPresentation(reason); }
                catch (Exception exception) { Fault("release", exception); }
            }
            _released.Clear();
        }

        private void Detach(string reason)
        {
            if (_hits != null) _hits.ProjectileHitShown -= OnProjectileHitShown;
            _endSubscription?.Dispose();
            _endSubscription = null;
            _released.Clear();
            _schedule.Discard(_released);
            for (int i = 0; i < _released.Count; i++)
            {
                try { _released[i].DiscardDeferredDeathPresentation(); }
                catch (Exception exception) { Fault("discard", exception); }
            }
            _released.Clear();
            if (_hits != null) WriteDiagnostic(reason);
            _hits = null;
            if (ReferenceEquals(Current, this)) Current = null;
        }

        private void OnDestroy() => Detach("destroy");

        private void Fault(string stage, Exception exception)
        {
            _faults++;
            if (_faults != 1 && _faults % 64 != 0) return;
            GameLog.Dev.Warn("Combat", nameof(DeferredUnitDeathPresenter),
                "Deferred death presentation degraded; HP/death authority unchanged",
                $"stage={stage}, faults={_faults}, error={exception.GetType().Name}:{exception.Message}");
        }

        // ====================================================================
        // [진단] 읽기 전용 관측 — 릴리스 빌드에서는 호출이 사라진다.
        //   [PROJECTILE-DEATH-HOLD] side=view v=1 role=.. reason=..
        //     death=deferred|atImpact|cap|forced|discarded|heldNow      — 미룬 수 · 착탄 순간 진행 · 상한 초과 · 게임 종료 · 버림 · 남은 수
        //     immediate=noHeldKillShot|afterImpact|capacity             — 즉시 진행(보관 사망타 없음 · 그중 착탄이 먼저였음 · 상한)
        //     gate=notReady|noVisualRoot                                 — 조율자 미준비 · 대역 불가(옛 프리팹)로 즉시 진행한 수
        //     holdDuration=분포   deathMinusImpact=분포(0 = 착탄과 같은 순간, 음수 = 사망 표현이 먼저)
        //     burn=held|released|cap|immediate                          — 화상 VFX 시작 보류(TimedDamageEffectPresenter)
        //     poison=held|released|cap|immediate                        — [2026-10-27 단계 E] 독 VFX 시작 보류(같은 presenter)
        //     verdict=PASS | ATTENTION attention=cap,burn-cap,poison-cap,no-visual-root,faults
        //   [단계 E] poison 칸이 늘어 형식이 바뀌었으므로 v=1 → v=2(선례: [PROJECTILE-END] side=view v=1 → v=2).
        //     옛 형식(기록용): "side=view v=1 ... burn=held:N|released:N|cap:N|immediate:N verdict=..."
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WriteDiagnostic(string reason)
        {
            if (_diagnosticWritten) return;
            _diagnosticWritten = true;
            GameLog.Dev.Info("Combat", nameof(DeferredUnitDeathPresenter), "[PROJECTILE-DEATH-HOLD]",
                DescribeForDiagnostics(reason));
        }

        /// <summary>[진단] END 줄 본문(검증이 형식을 확인한다).</summary>
        /// <param name="reason">줄을 남기는 사유.</param>
        public string DescribeForDiagnostics(string reason)
        {
            var attention = new List<string>(5);
            if (_schedule.ReleasedCap > 0) attention.Add("cap");
            if (ProjectileAlignedTimedDamageDiagnostics.CapExceeded > 0) attention.Add("burn-cap");
            if (ProjectileAlignedTimedDamageDiagnostics.PoisonCapExceeded > 0) attention.Add("poison-cap");   // [단계 E]
            if (_noVisualRoot > 0) attention.Add("no-visual-root");
            if (_faults > 0) attention.Add("faults");
            // [단계 E] 옛 머리(기록용): $"side=view v=1 role={...} reason={reason} "
            return FormattableString.Invariant(
                       $"side=view v=2 role={ServerProjectileDiagnostics.RoleName} reason={reason} ")
                   + _schedule.DescribeForDiagnostics()
                   + FormattableString.Invariant($" gate=notReady:{_notReady}|noVisualRoot:{_noVisualRoot} faults={_faults} ")
                   + ProjectileAlignedTimedDamageDiagnostics.Describe()
                   + " verdict=" + (attention.Count == 0 ? "PASS" : "ATTENTION attention=" + string.Join(",", attention));
        }

#if UNITY_EDITOR
        /// <summary>[Editor 전용] fixture가 가짜 실제 시계를 넣는다(null이면 실제 시계로 돌아간다).</summary>
        /// <param name="clock">실제 시계 대체 함수.</param>
        public void SetClockForValidation(Func<double> clock)
            => _clock = clock ?? (() => Time.realtimeSinceStartupAsDouble);

        /// <summary>[Editor 전용] Update를 기다리지 않고 상한 검사를 지정 시각으로 돌린다.</summary>
        /// <param name="now">실제 시계 값.</param>
        public void TickForValidation(double now) => Tick(now);

        /// <summary>[Editor 전용] 전역 OnGameEnd를 쏘지 않고(다른 구독자에 영향 없이) 게임 종료 처리만 돌린다.</summary>
        public void SimulateGameEndForValidation() => OnGameEnd();

        /// <summary>[Editor 전용] Edit Mode fixture가 DestroyImmediate 전에 직접 정리한다.</summary>
        public void DetachForValidation() => Detach("fixture");
#endif
    }
}
