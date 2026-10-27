// ============================================================================
// ProjectileKillPresentationSchedule.cs
// [2026-10-26 서버 권위 투사체 — 단계 D 후속, 수정안 (a)] 투사체 "사망타"의 화면 사망 표현을
// 그 투사체가 화면에서 닿는 순간까지 잠깐 미루는 순수 C# 일정표 + 화상 VFX 시작 판정.
//
// [왜 필요한가 — 유니티 초급자용]
//   서버는 투사체가 닿는 순간 피해를 주고, 그 자리에서 대상이 죽으면 곧바로 사망을 알린다.
//   그런데 Client 화면은 투사체를 "서버 시각 − 0.10초"에 그린다(ProjectilePresentationPresenter).
//   그래서 Client에서는 사망 표현(쓰러짐·사망 이펙트·사망음·오브젝트 소멸)이 투사체가 닿기 0.04~0.17초 전에 나왔다
//   (2026-10-26 실기 로그 분석). 피해 숫자는 이미 "번호표(ProjectileDeliveryKey) 보관함"으로 착탄 순간에 맞춰져 있으므로,
//   사망 표현도 같은 번호표가 화면에 나오는 순간(= 숫자가 나오는 순간)까지 미룬다.
//
// [지켜야 할 약속]
//   · 이 일정표는 HP·피해·사망 판정·도메인 제거 시각을 읽거나 바꾸지 않는다(표현 전용 — 서버 권위 불변).
//     도메인에서는 대상이 이미 제거된 상태 그대로이고, 미루는 것은 "화면 표현"뿐이다.
//   · 미루는 대상은 "투사체 번호표가 붙은 사망타 피해가 아직 화면에 안 나온" 대상뿐이다.
//     근접·즉발·DoT 틱 사망처럼 번호표가 없으면 즉시 진행한다(기존 동작 그대로).
//   · 상한 MaxHoldSeconds(0.3초)를 넘기면 화면 착탄을 기다리지 않고 즉시 진행한다(표현이 멈춰 버리는 일이 없게).
//   · 항목 수 상한(Capacity)이 있다. 넘치면 미루지 않고 즉시 진행한다(조용히 버리지 않고 센다).
//
// [시계] local = 이 기기의 실제 경과 시계(Time.realtimeSinceStartupAsDouble). 서버 시계와 섞지 않는다.
//
// Application 레이어 — 순수 C#(UnityEngine 타입 없음). 검증(fixture)이 가짜 시계로 그대로 돌릴 수 있다.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using Hexiege.Domain;   // [2026-10-27 단계 E] 지속 피해 종류 → 그 효과를 거는 유닛 종류(UnitType) 대응표용

namespace Hexiege.Application
{
    /// <summary>미뤄 둔 사망 표현이 풀린 이유.</summary>
    public enum DeferredDeathReleaseReason : byte
    {
        /// <summary>그 투사체가 화면에서 닿았다(정상 — 사망 표현과 착탄이 같은 순간).</summary>
        AtImpact = 0,
        /// <summary>상한(MaxHoldSeconds)을 넘겼다 — 화면 착탄을 더 기다리지 않고 진행.</summary>
        Cap = 1,
        /// <summary>게임 종료 — 남은 것을 모두 지금 진행.</summary>
        Forced = 2
    }

    /// <summary>
    /// 투사체 사망타 사망 표현 일정표. T = 화면 쪽이 넘기는 "미룬 표현 1건"(Presentation이 실제 재생을 맡는다).
    /// 대상 동일성은 ID가 아니라 객체 참조(UnitData)로 본다 — ID 재사용·재경기에서 다른 유닛과 섞이지 않는다.
    /// </summary>
    /// <typeparam name="T">미룬 사망 표현을 담는 화면 쪽 객체 타입.</typeparam>
    public sealed class ProjectileKillPresentationSchedule<T> where T : class
    {
        /// <summary>사망 표현을 미룰 수 있는 최대 실제 시간(초). 넘기면 즉시 진행한다.</summary>
        public const double MaxHoldSeconds = 0.3d;

        /// <summary>동시에 미룰 수 있는 대상 수 상한.</summary>
        public const int Capacity = 256;

        /// <summary>
        /// [진단] "화면 착탄 기억"을 유지하는 시간(초)과 개수. 사망 통보가 착탄보다 늦게 와서 즉시 진행한 경우(Host 등),
        /// 또는 상한을 넘겨 먼저 진행한 뒤 착탄이 나중에 나온 경우의 시각 차를 재기 위한 짧은 기억이다(판단에는 쓰지 않는다).
        /// </summary>
        public const double MemorySeconds = 2d;

        /// <summary>[진단] 기억 개수 상한.</summary>
        public const int MemoryCapacity = 256;

        private sealed class Entry
        {
            public T Payload;
            public object Target;
            public readonly List<ProjectileDeliveryKey> Keys = new List<ProjectileDeliveryKey>(2);
            public double Since;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _work = new List<Entry>();
        // [진단] 상한으로 먼저 진행한 대상의 남은 번호표 → 진행한 시각. 나중에 화면 착탄이 오면 "사망 표현 − 착탄"(음수)을 잰다.
        private readonly Dictionary<ProjectileDeliveryKey, double> _cappedAt = new Dictionary<ProjectileDeliveryKey, double>();
        // [진단] 미뤄 둔 사망이 없는 대상의 사망타가 화면에 나온 시각. 그 뒤 사망 통보가 오면 "사망 표현 − 착탄"(양수)을 잰다.
        private readonly Dictionary<object, double> _shownWithoutEntry = new Dictionary<object, double>();
        private readonly List<ProjectileDeliveryKey> _expiredKeys = new List<ProjectileDeliveryKey>();
        private readonly List<object> _expiredTargets = new List<object>();

        // ── 진단 계수(읽기 전용 관측 — 판단에 쓰지 않는다) ──
        /// <summary>사망 표현을 미룬 수.</summary>
        public int Deferred { get; private set; }
        /// <summary>화면 착탄 순간에 풀린 수(정상).</summary>
        public int ReleasedAtImpact { get; private set; }
        /// <summary>상한을 넘겨 착탄 전에 풀린 수(= 상한 초과 수).</summary>
        public int ReleasedCap { get; private set; }
        /// <summary>게임 종료로 한꺼번에 풀린 수.</summary>
        public int ReleasedForced { get; private set; }
        /// <summary>재초기화·파괴로 표현 없이 버린 수.</summary>
        public int Discarded { get; private set; }
        /// <summary>보관 중인 사망타가 없어 즉시 진행한 수(근접·DoT·비투사체 사망, 또는 이미 화면 착탄이 끝난 사망 포함).</summary>
        public int ImmediateNoHeldKillShot { get; private set; }
        /// <summary>즉시 진행 중 "그 대상의 사망타가 이미 화면에 나온 뒤"였던 수(Host·싱글처럼 착탄이 먼저인 경우).</summary>
        public int ImmediateAfterImpact { get; private set; }
        /// <summary>상한(Capacity) 초과로 즉시 진행한 수.</summary>
        public int ImmediateCapacity { get; private set; }

        /// <summary>미뤄 둔 시간 분포(초) — 사망 통보를 받은 순간부터 풀린 순간까지.</summary>
        public readonly ProjectileTimeDistribution HoldDuration =
            new ProjectileTimeDistribution(0.05d, 0.1d, 0.15d, 0.2d, MaxHoldSeconds);

        /// <summary>
        /// "사망 표현 시각 − 그 사망타의 화면 착탄 시각" 분포(초). 0 = 같은 순간(목표), 음수 = 사망 표현이 착탄보다 먼저.
        /// 화면 착탄 시각을 알 수 있는 사망만 들어간다(착탄 순간 해제·상한 해제 뒤 착탄·착탄 뒤 즉시 진행).
        /// </summary>
        public readonly ProjectileTimeDistribution DeathMinusImpact =
            new ProjectileTimeDistribution(-0.1d, -0.05d, -0.001d, 0.001d, 0.05d, 0.1d);

        /// <summary>지금 미뤄 둔 대상 수.</summary>
        public int HeldCount => _entries.Count;

        /// <summary>
        /// 사망 표현을 미룰지 정한다. heldKillShotKeys = 이 대상에게 아직 화면에 안 나온 "사망타" 번호표 목록.
        /// 비어 있으면 미루지 않는다(false — 호출자가 기존대로 즉시 진행). 미루면 true.
        /// </summary>
        /// <param name="target">사망한 대상(동일성 비교용 객체 — UnitData).</param>
        /// <param name="payload">미룬 표현 1건(풀릴 때 돌려준다).</param>
        /// <param name="heldKillShotKeys">보관 중인 사망타 번호표.</param>
        /// <param name="local">실제 시계.</param>
        public bool TryDefer(object target, T payload, IReadOnlyList<ProjectileDeliveryKey> heldKillShotKeys, double local)
        {
            if (target == null || payload == null) { ImmediateNoHeldKillShot++; return false; }
            if (heldKillShotKeys == null || heldKillShotKeys.Count == 0)
            {
                ImmediateNoHeldKillShot++;
                // [진단] 이 대상의 사망타가 이미 화면에 나왔다면(착탄이 사망 통보보다 먼저) 그 시각 차(양수)를 잰다.
                if (_shownWithoutEntry.TryGetValue(target, out double shownAt))
                {
                    _shownWithoutEntry.Remove(target);
                    ImmediateAfterImpact++;
                    DeathMinusImpact.Add(local - shownAt);
                }
                return false;
            }
            if (_entries.Count >= Capacity) { ImmediateCapacity++; return false; }
            var entry = new Entry { Payload = payload, Target = target, Since = local };
            for (int i = 0; i < heldKillShotKeys.Count; i++)
                if (heldKillShotKeys[i].IsValid && !entry.Keys.Contains(heldKillShotKeys[i])) entry.Keys.Add(heldKillShotKeys[i]);
            if (entry.Keys.Count == 0) { ImmediateNoHeldKillShot++; return false; }
            _entries.Add(entry);
            Deferred++;
            return true;
        }

        /// <summary>
        /// 사망타 번호표 하나가 화면에 나왔다(피해 숫자 방출 순간). 그 번호표만 기다리던 대상의 사망 표현을 output에 꺼낸다.
        /// </summary>
        /// <param name="key">화면에 나온 사망타 번호표.</param>
        /// <param name="target">그 피해의 대상(UnitData).</param>
        /// <param name="local">실제 시계.</param>
        /// <param name="output">지금 진행할 표현을 담을 목록(호출자가 비워 둔다).</param>
        public void NotifyKillShotShown(ProjectileDeliveryKey key, object target, double local, List<T> output)
        {
            if (!key.IsValid) return;
            bool matched = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                if (!entry.Keys.Remove(key)) continue;
                matched = true;
                if (entry.Keys.Count > 0) continue;
                _entries.RemoveAt(i);
                ReleasedAtImpact++;
                HoldDuration.Add(local - entry.Since);
                DeathMinusImpact.Add(0d);
                output?.Add(entry.Payload);
            }
            if (matched) return;
            // [진단] 상한으로 먼저 진행했던 사망의 착탄이 이제 나왔다 → 사망 표현이 착탄보다 얼마나 먼저였는가(음수).
            if (_cappedAt.TryGetValue(key, out double cappedAt))
            {
                _cappedAt.Remove(key);
                DeathMinusImpact.Add(cappedAt - local);
                return;
            }
            // [진단] 아직 사망 통보가 오지 않은 대상(Host·싱글은 화면 착탄이 먼저일 수 있다) — 짧게 기억해 둔다.
            if (target != null && (_shownWithoutEntry.Count < MemoryCapacity || _shownWithoutEntry.ContainsKey(target)))
                _shownWithoutEntry[target] = local;
        }

        /// <summary>상한을 넘긴 항목을 output에 꺼낸다(즉시 진행). 오래된 진단 기억도 지운다.</summary>
        /// <param name="local">실제 시계.</param>
        /// <param name="output">지금 진행할 표현을 담을 목록.</param>
        public void CollectExpired(double local, List<T> output)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                if (local - entry.Since < MaxHoldSeconds) continue;
                _entries.RemoveAt(i);
                ReleasedCap++;
                HoldDuration.Add(local - entry.Since);
                for (int k = 0; k < entry.Keys.Count; k++)
                    if (_cappedAt.Count < MemoryCapacity) _cappedAt[entry.Keys[k]] = local;
                output?.Add(entry.Payload);
            }
            if (_cappedAt.Count > 0)
            {
                _expiredKeys.Clear();
                foreach (KeyValuePair<ProjectileDeliveryKey, double> pair in _cappedAt)
                    if (local - pair.Value > MemorySeconds) _expiredKeys.Add(pair.Key);
                for (int i = 0; i < _expiredKeys.Count; i++) _cappedAt.Remove(_expiredKeys[i]);
                _expiredKeys.Clear();
            }
            if (_shownWithoutEntry.Count > 0)
            {
                _expiredTargets.Clear();
                foreach (KeyValuePair<object, double> pair in _shownWithoutEntry)
                    if (local - pair.Value > MemorySeconds) _expiredTargets.Add(pair.Key);
                for (int i = 0; i < _expiredTargets.Count; i++) _shownWithoutEntry.Remove(_expiredTargets[i]);
                _expiredTargets.Clear();
            }
        }

        /// <summary>게임 종료 — 남은 항목을 모두 output에 꺼낸다(지금 진행).</summary>
        /// <param name="local">실제 시계.</param>
        /// <param name="output">지금 진행할 표현을 담을 목록.</param>
        public void ReleaseAll(double local, List<T> output)
        {
            _work.Clear();
            _work.AddRange(_entries);
            _entries.Clear();
            for (int i = 0; i < _work.Count; i++)
            {
                ReleasedForced++;
                HoldDuration.Add(local - _work[i].Since);
                output?.Add(_work[i].Payload);
            }
            _work.Clear();
        }

        /// <summary>재초기화·파괴 — 남은 항목을 표현 없이 버린다(discarded에 담아 호출자가 화면 오브젝트를 치우게 한다).</summary>
        /// <param name="discarded">버린 표현을 담을 목록(null 허용).</param>
        public void Discard(List<T> discarded)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Discarded++;
                discarded?.Add(_entries[i].Payload);
            }
            _entries.Clear();
            _cappedAt.Clear();
            _shownWithoutEntry.Clear();
        }

        /// <summary>진단 계수와 분포를 0으로(새 경기 시작). 남은 항목은 건드리지 않는다 — 먼저 Discard를 부른다.</summary>
        public void ResetCounters()
        {
            Deferred = ReleasedAtImpact = ReleasedCap = ReleasedForced = Discarded = 0;
            ImmediateNoHeldKillShot = ImmediateAfterImpact = ImmediateCapacity = 0;
            HoldDuration.Clear();
            DeathMinusImpact.Clear();
        }

        /// <summary>
        /// [진단] 로그 조각 — "death=deferred:N|atImpact:N|cap:N|forced:N|discarded:N|heldNow:N
        /// immediate=noHeldKillShot:N|afterImpact:N|capacity:N holdDuration=... deathMinusImpact=...".
        /// afterImpact는 noHeldKillShot 안에 포함된 부분 집합이다.
        /// </summary>
        public string DescribeForDiagnostics()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "death=deferred:{0}|atImpact:{1}|cap:{2}|forced:{3}|discarded:{4}|heldNow:{5} "
                + "immediate=noHeldKillShot:{6}|afterImpact:{7}|capacity:{8} holdDuration={9} deathMinusImpact={10}",
                Deferred, ReleasedAtImpact, ReleasedCap, ReleasedForced, Discarded, _entries.Count,
                ImmediateNoHeldKillShot, ImmediateAfterImpact, ImmediateCapacity,
                HoldDuration.Format(), DeathMinusImpact.Format());
        }
    }

    /// <summary>
    /// [2026-10-26 수정안 (a)] InfernoSpirit 화상(Burn) 지속 VFX의 "화면 시작" 판정(순수 함수).
    ///
    /// 서버는 투사체가 닿는 순간 화상을 건다(StartTime = 서버 착탄 처리 시각). Client가 받자마자 켜면
    /// 화면의 투사체(서버 시각 − 0.10초로 그림)가 닿기 전에 불이 붙어 보인다. 그래서
    ///   ① 화면 시각(서버 시각 − 표시 지연)이 StartTime에 닿을 때까지, 그리고
    ///   ② 같은 공격자의 투사체 피해가 아직 화면 착탄을 기다리는 중(보관함에 있음)이면 그 착탄까지
    /// 시작을 미룬다. 단, 받은 뒤 MaxHoldSeconds(0.3초)를 넘기면 기다리지 않고 시작한다.
    /// 싱글·Host는 표시 지연이 0이라 ①은 항상 통과하고, ②도 길어야 한 프레임이다.
    /// 표현 전용 — 화상 피해·틱·지속 시간은 바꾸지 않는다.
    ///
    /// [2026-10-27 서버 권위 투사체 단계 E — 사용자 결정 E-B (a)] MushroomBomber 독(Poison)도 같은 규칙으로 미룬다.
    ///   독은 버섯 포자 투사체가 닿는 순간 서버가 건다(BlastAttackBehavior — 주 타깃 위치 중심 반경 안의 적 전원).
    ///   화상과 같은 수신 경로(지속 피해 상태 → 화면)라 Client에서 포자가 닿기 전에 독이 먼저 보일 수 있었다.
    ///   · 주 타깃: ①(화면 시각) + ②(그 공격자의 투사체 피해 보관 중) 둘 다 본다 — 화상과 같다.
    ///   · 반경 안 다른 적: 직접 피해가 없어 보관된 번호표가 없으므로 ①만 본다(Client에서 이 시각 ≈ 화면 착탄).
    ///   상한 0.3초도 화상과 같다. 독 피해·틱·지속 시간은 바꾸지 않는다.
    /// </summary>
    public static class ProjectileAlignedTimedDamageStart
    {
        /// <summary>화상·독 VFX 시작을 미룰 수 있는 최대 실제 시간(초). 사망 표현 상한과 같다.</summary>
        public const double MaxHoldSeconds = 0.3d;

        /// <summary>
        /// 이 지속 피해 종류가 "서버 투사체 착탄으로 시작"하는가(= 화면 착탄에 맞춰 시작을 미룰 대상인가).
        /// 화상 = InfernoSpirit, 독 = MushroomBomber가 거는 유일한 지속 피해이므로, 그 유닛이 서버 투사체 스위치에 켜져 있을 때만 true다.
        /// (지속 피해 종류 → 거는 유닛 대응은 이 한 곳에만 둔다 — 화면 쪽(TimedDamageEffectPresenter)이 이 함수를 부른다.)
        /// </summary>
        /// <param name="visual">지속 피해 종류.</param>
        /// <returns>화면 착탄 정렬 대상이면 true. None·알 수 없는 값이면 false(즉시 시작 — 예전 동작).</returns>
        public static bool IsProjectileAligned(TimedDamageVisual visual)
        {
            switch (visual)
            {
                case TimedDamageVisual.Burn: return ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.InfernoSpirit);
                case TimedDamageVisual.Poison: return ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.MushroomBomber);
                default: return false;
            }
        }

        /// <summary>
        /// 지금 시작을 미뤄야 하는가.
        /// </summary>
        /// <param name="visual">지속 피해 종류(화상·독이 대상 — 그 밖은 즉시). 스위치 확인은 호출자가 IsProjectileAligned로 한다.</param>
        /// <param name="effectStart">서버가 정한 시작 시각(서버 시계).</param>
        /// <param name="presentationNow">이 화면의 표시 시각(서버 시계 − 표시 지연). NaN·무한대면 ①은 보지 않는다.</param>
        /// <param name="heldProjectileHit">같은 공격자의 투사체 피해가 대상에게 아직 보관 중인가.</param>
        /// <param name="waitedSeconds">받은 뒤 지난 실제 시간(초).</param>
        public static bool ShouldWait(TimedDamageVisual visual, double effectStart, double presentationNow,
            bool heldProjectileHit, double waitedSeconds)
        {
            // [단계 E] 화상과 독(MushroomBomber) 둘 다 미룬다(단계 D까지는 화상만이었다).
            if (visual != TimedDamageVisual.Burn && visual != TimedDamageVisual.Poison) return false;
            if (double.IsNaN(waitedSeconds) || waitedSeconds >= MaxHoldSeconds) return false;
            if (heldProjectileHit) return true;
            if (double.IsNaN(presentationNow) || double.IsInfinity(presentationNow)) return false;
            if (double.IsNaN(effectStart) || double.IsInfinity(effectStart)) return false;
            return presentationNow < effectStart;
        }
    }

    /// <summary>
    /// [2026-10-26 수정안 (a) 진단] 화상 VFX 시작 보류 계수(정적 — 진단 전용, 판단에 쓰지 않는다).
    /// 릴리스 빌드에서는 기록 호출이 사라진다.
    ///
    /// [2026-10-27 단계 E — 독 보류 추가] 화상(burn)과 독(poison)을 따로 센다. 독도 같은 보류 장치를 타므로
    ///   한 계수에 섞으면 "Client 독 held = released, cap 0" 같은 실기 확인을 할 수 없다.
    /// </summary>
    public static class ProjectileAlignedTimedDamageDiagnostics
    {
        // 화상(InfernoSpirit) 계수.
        private static int _burnHeld, _burnReleasedInTime, _burnReleasedCap, _burnImmediate;
        // [단계 E] 독(MushroomBomber) 계수.
        private static int _poisonHeld, _poisonReleasedInTime, _poisonReleasedCap, _poisonImmediate;

        /// <summary>새 경기 — 0으로.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Reset()
        {
            _burnHeld = _burnReleasedInTime = _burnReleasedCap = _burnImmediate = 0;
            _poisonHeld = _poisonReleasedInTime = _poisonReleasedCap = _poisonImmediate = 0;
        }

        /// <summary>화상·독 VFX 시작을 처음 미룬 1건.</summary>
        /// <param name="visual">지속 피해 종류(화상·독 밖의 값은 세지 않는다).</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void RecordHeld(TimedDamageVisual visual)
        {
            if (visual == TimedDamageVisual.Burn) _burnHeld++;
            else if (visual == TimedDamageVisual.Poison) _poisonHeld++;
        }

        /// <summary>미뤘던 화상·독 VFX를 시작한 1건(capExceeded = 상한을 넘겨 시작했는가).</summary>
        /// <param name="visual">지속 피해 종류.</param>
        /// <param name="capExceeded">상한 초과로 시작했으면 true.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void RecordReleased(TimedDamageVisual visual, bool capExceeded)
        {
            if (visual == TimedDamageVisual.Burn) { if (capExceeded) _burnReleasedCap++; else _burnReleasedInTime++; }
            else if (visual == TimedDamageVisual.Poison) { if (capExceeded) _poisonReleasedCap++; else _poisonReleasedInTime++; }
        }

        /// <summary>미룰 필요 없이 바로 시작한 화상·독 VFX 1건.</summary>
        /// <param name="visual">지속 피해 종류.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void RecordImmediate(TimedDamageVisual visual)
        {
            if (visual == TimedDamageVisual.Burn) _burnImmediate++;
            else if (visual == TimedDamageVisual.Poison) _poisonImmediate++;
        }

        /// <summary>화상 VFX가 상한 초과로 시작한 수(verdict 판정용). 옛 이름 CapExceeded의 뜻(화상만)을 그대로 유지한다.</summary>
        public static int CapExceeded => _burnReleasedCap;

        /// <summary>[단계 E] 독 VFX가 상한 초과로 시작한 수(verdict 판정용).</summary>
        public static int PoisonCapExceeded => _poisonReleasedCap;

        /// <summary>
        /// 로그 조각 — "burn=held:N|released:N|cap:N|immediate:N poison=held:N|released:N|cap:N|immediate:N".
        /// [단계 E] 뒤의 poison 칸은 단계 E에서 더했다.
        /// </summary>
        public static string Describe()
            => string.Format(CultureInfo.InvariantCulture,
                "burn=held:{0}|released:{1}|cap:{2}|immediate:{3} poison=held:{4}|released:{5}|cap:{6}|immediate:{7}",
                _burnHeld, _burnReleasedInTime, _burnReleasedCap, _burnImmediate,
                _poisonHeld, _poisonReleasedInTime, _poisonReleasedCap, _poisonImmediate);
    }
}
