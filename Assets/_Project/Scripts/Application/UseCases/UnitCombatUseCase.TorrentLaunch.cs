using System;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class UnitCombatUseCase
    {
        // C3 scope가 아닌 서버 gameplay 예약이다. 서비스와 객체 수명이 ID 재사용을 차단한다.
        private const int TorrentLaunchCapacity = 1024;
        private struct TorrentLaunch
        {
            internal ulong Key;
            internal UnitData Attacker;
            internal IDamageable Primary;
            internal int PrimaryId;
            internal bool PrimaryIsUnit;
            internal double Due;
            internal Vector3 LastAim;
            internal double AimTime;
        }
        private TorrentLaunch[] _torrentLaunches;
        private static ulong _nextTorrentLaunchKey;
        private int _torrentLaunchCount;
        private bool _torrentLaunchClosed;
        private readonly string _torrentLaunchEpoch = Guid.NewGuid().ToString("N");

        /// <summary>승인 경계에서 용량을 먼저 확보한다. 실패하면 cooldown/공격 커밋하지 않는다.</summary>
        public bool TryReserveTorrentLaunch(UnitData attacker, int primaryId,
            bool primaryIsUnit, double now, double overshoot)
        {
            if (_torrentLaunchClosed || attacker == null || !attacker.IsAlive
                || attacker.Type != UnitType.TorrentSpirit || !IsCurrentTorrentAttacker(attacker))
                return false;
            IDamageable primary = FindTargetById(primaryId, primaryIsUnit);
            float[] offsets = attacker.HitFrameTimes;
            // 파도는 공격당 하나다. 잘못된 다중 marker 설정을 중복 wave로 바꾸지 않는다.
            if (primary == null || !primary.IsAlive || (offsets != null && offsets.Length > 1)
                || _torrentLaunchCount >= TorrentLaunchCapacity || _nextTorrentLaunchKey == ulong.MaxValue)
            {
                TorrentLaunchLog("reserve-rejected", 0, attacker.Id, primaryId, now);
                return false;
            }
            Vector3 aim = ResolveWorldPosition(primary) - ResolveWorldPosition(attacker);
            aim.y = 0f;
            if (!IsValidTorrentAim(aim) || double.IsNaN(now) || double.IsInfinity(now)) return false;
            double offset = offsets != null && offsets.Length == 1 ? offsets[0] : 0d;
            if (double.IsNaN(offset) || double.IsInfinity(offset) || offset < 0d
                || double.IsNaN(overshoot) || double.IsInfinity(overshoot) || overshoot < 0d)
                return false;
            double due = now + Math.Max(0d, offset - overshoot);
            if (double.IsNaN(due) || double.IsInfinity(due)) return false;
            if (_torrentLaunches == null) _torrentLaunches = new TorrentLaunch[TorrentLaunchCapacity];
            for (int index = 0; index < _torrentLaunches.Length; index++)
            {
                if (_torrentLaunches[index].Key != 0) continue;
                var launch = new TorrentLaunch
                {
                    Key = ++_nextTorrentLaunchKey, Attacker = attacker, Primary = primary,
                    PrimaryId = primaryId, PrimaryIsUnit = primaryIsUnit,
                    Due = due, LastAim = aim.normalized, AimTime = now
                };
                _torrentLaunches[index] = launch;
                _torrentLaunchCount++;
                TorrentLaunchLog("reserved", launch.Key, attacker.Id, primaryId, launch.Due);
                return true;
            }
            return false;
        }

        private bool IsCurrentTorrentAttacker(UnitData attacker)
        {
            return _unitSpawn.Units.TryGetValue(attacker.Id, out UnitData current)
                && ReferenceEquals(current, attacker);
        }

        private static bool IsValidTorrentAim(Vector3 aim)
        {
            return !float.IsNaN(aim.x) && !float.IsInfinity(aim.x)
                && !float.IsNaN(aim.z) && !float.IsInfinity(aim.z)
                && aim.sqrMagnitude > 0.0001f;
        }

        /// <summary>windup 동안 원래 객체의 실제 서버 방향을 갱신하고 due 예약을 한번만 발사한다.</summary>
        public void TickTorrentLaunches(double now)
        {
            if (_torrentLaunchClosed || _torrentLaunchCount == 0
                || double.IsNaN(now) || double.IsInfinity(now)) return;
            for (int index = 0; index < _torrentLaunches.Length; index++)
            {
                TorrentLaunch launch = _torrentLaunches[index];
                if (launch.Key == 0) continue;
                if (!launch.Attacker.IsAlive || !IsCurrentTorrentAttacker(launch.Attacker))
                {
                    _torrentLaunches[index] = default;
                    _torrentLaunchCount--;
                    TorrentLaunchLog("attacker-unavailable", launch.Key, launch.Attacker.Id, launch.PrimaryId, now);
                    continue;
                }
                IDamageable current = FindTargetById(launch.PrimaryId, launch.PrimaryIsUnit);
                bool liveOriginal = ReferenceEquals(current, launch.Primary) && current != null && current.IsAlive;
                if (liveOriginal)
                {
                    Vector3 aim = ResolveWorldPosition(current) - ResolveWorldPosition(launch.Attacker);
                    aim.y = 0f;
                    if (IsValidTorrentAim(aim)) { launch.LastAim = aim.normalized; launch.AimTime = now; }
                }
                if (now < launch.Due)
                {
                    _torrentLaunches[index] = launch;
                    continue;
                }
                // 외부 이벤트 재진입 전에 소비한다. Stop/새 target은 이 예약의 소유자가 아니다.
                _torrentLaunches[index] = default;
                _torrentLaunchCount--;
                TorrentLaunchLog(liveOriginal ? "launch-current-aim" : "launch-last-valid-aim",
                    launch.Key, launch.Attacker.Id, launch.PrimaryId, now);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[TORRENT-LAUNCH] aim",
                    FormattableString.Invariant($"service={_torrentLaunchEpoch}, reservation={launch.Key}, due={launch.Due:G17}, aimTime={launch.AimTime:G17}, fx={launch.LastAim.x:F6}, fz={launch.LastAim.z:F6}"));
#endif
                SpawnWave(TorrentAttackBehavior.CreateWaveRequest(launch.Attacker,
                    ResolveWorldPosition(launch.Attacker), launch.LastAim,
                    _waveWidth, _waveLength, _waveTravelTime, _waveHeal,
                    launch.PrimaryId, launch.PrimaryIsUnit, launch.Key));
                // Spawn/contact 경계에서 gameend가 동기 발행되면 다음 예약도 즉시 닫힌다.
                if (_torrentLaunchClosed) return;
            }
        }

        /// <summary>게임 종료/서비스 교체 전에 모든 미래 발사 허가를 영구 닫는다.</summary>
        public void CloseTorrentLaunches(string reason = "lifecycle-close")
        {
            _torrentLaunchClosed = true;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_torrentLaunchCount > 0)
                GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[TORRENT-LAUNCH] close",
                    $"service={_torrentLaunchEpoch}, reason={reason}, cancelled={_torrentLaunchCount}, capacity={TorrentLaunchCapacity}");
#endif
            if (_torrentLaunches != null) Array.Clear(_torrentLaunches, 0, _torrentLaunches.Length);
            _torrentLaunchCount = 0;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void TorrentLaunchLog(string reason, ulong key, int attacker, int primary, double time)
        {
            GameLog.Dev.Info("Combat", nameof(UnitCombatUseCase), "[TORRENT-LAUNCH]",
                FormattableString.Invariant($"service={_torrentLaunchEpoch}, reason={reason}, reservation={key}, attacker={attacker}, originalPrimary={primary}, time={time:G17}"));
        }
    }
}
