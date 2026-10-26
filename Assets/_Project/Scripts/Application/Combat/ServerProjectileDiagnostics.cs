// ============================================================================
// ServerProjectileDiagnostics.cs
// [2026-10-20 서버 권위 투사체 — 단계 B 진단 보강] 읽기 전용 관측 도구 모음.
//
// [이 파일이 담는 것 — 유니티 초급자용]
//   1) ProjectileTimeDistribution : 시간 값(초)을 받아 최소·최대·평균·구간별 개수를 모으는 작은 통계 상자.
//      서버 비행 시간 분포, Client 화면 착탄 지연 분포 같은 "분포"를 로그 1줄에 담을 때 쓴다.
//   2) ServerProjectileDiagnostics: 여러 UnitView(유닛마다 1개)에서 흩어져 일어나는 "타격 marker" 사건을
//      한곳에 세는 정적 계수기. 화면 소비자(ProjectilePresentationPresenter)가 경기 종료 때 END 줄에 함께 싣는다.
//
// [지켜야 할 약속 — 진단은 게임플레이를 바꾸지 않는다]
//   · 여기 있는 어떤 값도 피해·HP·타겟·발사 판정에 쓰이지 않는다(읽기 전용 관측).
//   · 기록 메서드는 [Conditional("UNITY_EDITOR")] + [Conditional("DEVELOPMENT_BUILD")]라
//     릴리스 빌드에서는 호출 자체가 사라진다(문자열 보간·사전 조회 비용도 0).
//   · 메모리는 상한이 있다(공격자 표 2048행). 넘치면 "넘쳤다"는 사실만 센다(조용히 버리지 않는다).
//
// [로그 태그 — 다음 실기 분석에서 grep할 이름]
//   · [PROJECTILE-END] side=view ... 줄의 markers=/path=/legacyTracer= 조각이 이 파일의 값이다.
//
// Application 레이어 — 순수 C#(UnityEngine 타입 없음). Domain의 UnitType만 읽는다.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 시간 값(초) 분포 상자 — 최소·최대·평균과 정해진 구간별 개수를 모은다(진단 전용, 할당 없음).
    /// 구간 경계는 생성자에서 한 번 정하며, 값 v는 "v &lt; 경계"인 첫 구간에 들어가고 모두 넘으면 마지막 구간이다.
    /// </summary>
    public sealed class ProjectileTimeDistribution
    {
        private readonly double[] _bounds;
        private readonly int[] _buckets;
        private int _count;
        private double _min = double.PositiveInfinity;
        private double _max = double.NegativeInfinity;
        private double _sum;

        /// <summary>모은 값 개수.</summary>
        public int Count => _count;

        /// <summary>분포 상자를 만든다.</summary>
        /// <param name="bounds">오름차순 구간 경계(초). 예: 0.1, 0.2 → 「&lt;0.1」「&lt;0.2」「≥0.2」 세 구간.</param>
        public ProjectileTimeDistribution(params double[] bounds)
        {
            _bounds = bounds ?? Array.Empty<double>();
            _buckets = new int[_bounds.Length + 1];
        }

        /// <summary>값 하나를 넣는다. NaN·무한대는 넣지 않는다(분포를 오염시키지 않게).</summary>
        /// <param name="value">시간 값(초).</param>
        public void Add(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            _count++;
            _sum += value;
            if (value < _min) _min = value;
            if (value > _max) _max = value;
            int index = _bounds.Length;
            for (int i = 0; i < _bounds.Length; i++)
                if (value < _bounds[i]) { index = i; break; }
            _buckets[index]++;
        }

        /// <summary>모두 비운다(새 경기 시작).</summary>
        public void Clear()
        {
            _count = 0; _sum = 0d;
            _min = double.PositiveInfinity; _max = double.NegativeInfinity;
            Array.Clear(_buckets, 0, _buckets.Length);
        }

        /// <summary>
        /// 로그용 한 조각 — "n:5|min:0.041|max:0.687|mean:0.400|lt0.1:1|lt0.2:0|ge0.2:4".
        /// 값이 하나도 없으면 min/max/mean은 "na"다.
        /// </summary>
        public string Format()
        {
            var text = new StringBuilder(96);
            text.Append("n:").Append(_count.ToString(CultureInfo.InvariantCulture));
            text.Append("|min:").Append(_count > 0 ? _min.ToString("F3", CultureInfo.InvariantCulture) : "na");
            text.Append("|max:").Append(_count > 0 ? _max.ToString("F3", CultureInfo.InvariantCulture) : "na");
            text.Append("|mean:").Append(_count > 0 ? (_sum / _count).ToString("F3", CultureInfo.InvariantCulture) : "na");
            for (int i = 0; i < _buckets.Length; i++)
            {
                text.Append(i < _bounds.Length ? "|lt" : "|ge")
                    .Append((i < _bounds.Length ? _bounds[i] : _bounds.Length > 0 ? _bounds[_bounds.Length - 1] : 0d)
                        .ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(':').Append(_buckets[i].ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// 화면 쪽 "타격 marker" 사건을 유닛 종류·공격자별로 세는 정적 계수기(진단 전용, 읽기 전용 관측).
    ///
    /// [무엇을 확인하려는가 — 단계 A 분석의 미확인 항목]
    ///   · 의심 1: Client에서 서버가 발사를 취소한 공격에도 총구 VFX가 났는가 → 공격자별 "marker 수 − 서버 발사 수".
    ///   · 항목 7: 스위치가 꺼진 투사체 유닛이 기존 경로(로컬 트레이서)를 그대로 썼는가 → 종류별 server/legacy 개수.
    /// 화면 소비자(ProjectilePresentationPresenter)가 맵을 열 때 ResetView, 경기 종료 때 DescribeView로 END 줄에 싣는다.
    /// </summary>
    public static class ServerProjectileDiagnostics
    {
        /// <summary>공격자 표 상한(행). 넘치면 overflow만 센다.</summary>
        public const int AttackerLimit = 2048;

        // UnitType 값은 0~27이라 32칸 배열이면 충분하다(범위 밖 값은 세지 않고 outOfRange만 센다).
        private const int TypeSlots = 32;

        private sealed class AttackerRow { public int Markers; public int Launches; }

        private static readonly Dictionary<int, AttackerRow> Attackers = new Dictionary<int, AttackerRow>();
        private static readonly int[] ServerMarkersByType = new int[TypeSlots];
        private static readonly int[] LegacyMarkersByType = new int[TypeSlots];
        private static readonly int[] LegacyTracersByType = new int[TypeSlots];
        private static int _serverMarkers, _serverSourceOnlyMarkers, _serverMarkerMuzzle;
        private static int _attackerOverflow, _typeOutOfRange;

        /// <summary>새 맵(경기)을 시작한다 — 모든 계수를 0으로.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void ResetView()
        {
            Attackers.Clear();
            Array.Clear(ServerMarkersByType, 0, TypeSlots);
            Array.Clear(LegacyMarkersByType, 0, TypeSlots);
            Array.Clear(LegacyTracersByType, 0, TypeSlots);
            _serverMarkers = _serverSourceOnlyMarkers = _serverMarkerMuzzle = 0;
            _attackerOverflow = _typeOutOfRange = 0;
            ResetCharge();   // [단계 D]
        }

        /// <summary>
        /// 투사체 유닛(Q11 분류)의 "타격 marker"(차지 marker 제외) 1건을 센다. 비투사체 유닛은 무시한다.
        /// </summary>
        /// <param name="type">쏜 유닛 종류.</param>
        /// <param name="attackerId">쏜 유닛 Id(공격자별 marker − 발사 비교용).</param>
        /// <param name="serverPath">이행 스위치가 켜진 새 경로인가.</param>
        /// <param name="sourceOnly">멀티 Client에서 정지/대상 사망 통보가 marker보다 먼저 온 SourceOnly marker인가.</param>
        /// <param name="muzzleAtMarker">이 marker 자리에서 총구 VFX를 냈는가(단계 B 이후 새 경로는 항상 false여야 한다).</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordProjectileUnitMarker(UnitType type, int attackerId, bool serverPath,
            bool sourceOnly, bool muzzleAtMarker)
        {
            if (!ServerProjectileDeliveryPolicy.IsProjectileUnit(type)) return;
            int slot = (int)type;
            if (slot < 0 || slot >= TypeSlots) { _typeOutOfRange++; return; }
            if (!serverPath) { LegacyMarkersByType[slot]++; return; }
            ServerMarkersByType[slot]++;
            _serverMarkers++;
            if (sourceOnly) _serverSourceOnlyMarkers++;
            if (muzzleAtMarker) _serverMarkerMuzzle++;
            AttackerRow row = Row(attackerId);
            if (row != null) row.Markers++;
        }

        /// <summary>화면 소비자가 서버 발사 기록 1건을 받아들였을 때 그 공격자 몫으로 센다.</summary>
        /// <param name="attackerId">쏜 유닛 Id.</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordServerLaunchAccepted(int attackerId)
        {
            AttackerRow row = Row(attackerId);
            if (row != null) row.Launches++;
        }

        /// <summary>스위치가 꺼진 투사체 유닛이 기존 로컬 트레이서(UnitView 원거리 분기)를 쏜 1건을 센다.</summary>
        /// <param name="type">쏜 유닛 종류.</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordLegacyTracer(UnitType type)
        {
            if (!ServerProjectileDeliveryPolicy.IsProjectileUnit(type)) return;
            int slot = (int)type;
            if (slot < 0 || slot >= TypeSlots) { _typeOutOfRange++; return; }
            LegacyTracersByType[slot]++;
        }

        /// <summary>
        /// END 줄 조각 — "markers=server:N|sourceOnly:N|muzzleAtMarker:N|withoutLaunch:N|launchWithoutMarker:N|attackers:N|overflow:N
        /// path=6:s12/l0|7:s3/l0|... legacyTracer=12:5|...".
        /// withoutLaunch = 공격자마다 max(0, marker − 받은 발사)를 더한 값(= 서버가 발사하지 않은 공격의 marker 추정 수).
        /// </summary>
        public static string DescribeView()
        {
            int withoutLaunch = 0, launchWithoutMarker = 0;
            foreach (AttackerRow row in Attackers.Values)
            {
                if (row.Markers > row.Launches) withoutLaunch += row.Markers - row.Launches;
                else if (row.Launches > row.Markers) launchWithoutMarker += row.Launches - row.Markers;
            }
            var text = new StringBuilder(256);
            text.Append("markers=server:").Append(_serverMarkers)
                .Append("|sourceOnly:").Append(_serverSourceOnlyMarkers)
                .Append("|muzzleAtMarker:").Append(_serverMarkerMuzzle)
                .Append("|withoutLaunch:").Append(withoutLaunch)
                .Append("|launchWithoutMarker:").Append(launchWithoutMarker)
                .Append("|attackers:").Append(Attackers.Count)
                .Append("|overflow:").Append(_attackerOverflow)
                .Append(" path=");
            bool first = true;
            for (int slot = 0; slot < TypeSlots; slot++)
            {
                if (ServerMarkersByType[slot] == 0 && LegacyMarkersByType[slot] == 0) continue;
                if (!first) text.Append('|');
                first = false;
                text.Append(slot).Append(":s").Append(ServerMarkersByType[slot]).Append("/l").Append(LegacyMarkersByType[slot]);
            }
            if (first) text.Append("none");
            text.Append(" legacyTracer=");
            first = true;
            for (int slot = 0; slot < TypeSlots; slot++)
            {
                if (LegacyTracersByType[slot] == 0) continue;
                if (!first) text.Append('|');
                first = false;
                text.Append(slot).Append(':').Append(LegacyTracersByType[slot]);
            }
            if (first) text.Append("none");
            if (_typeOutOfRange > 0) text.Append(" typeOutOfRange=").Append(_typeOutOfRange);
            return text.ToString();
        }

        // ── [2026-10-21 단계 D] 차지 유닛 화면 쪽 취소 표현 계수(읽기 전용 관측) ──
        //   cancelRecv   = 화면이 받은 서버 발사 취소 통보 수(유닛 공격자, 차지 여부 무관)
        //   stopped      = 켜져 있던 차지 VFX를 그 통보로 끈 수
        //   nothingActive= 차지 유닛이지만 끌 차지가 없던 수(차지 신호 전 취소 — Client는 그 회차 차지를 막아 둠)
        //   stale        = 회차가 달라 적용하지 않은 수(더 새 공격 보호 — 0에 가까워야 정상)
        //   notCharge    = 차지 유닛이 아닌 공격자의 취소(Stream·Tank 등 — 할 일 없음)
        //   noView       = 공격자 화면을 못 찾았거나 이미 죽음
        //   suppressedAfterCancel = 서버가 이미 취소한 뒤 온 차지 신호를 막은 수(싱글·Host 게이트)
        private static int _chargeCancelRecv, _chargeCancelStopped, _chargeCancelNothingActive, _chargeCancelStale;
        private static int _chargeCancelNotCharge, _chargeCancelNoView, _chargeSuppressedAfterCancel;

        /// <summary>[진단 — 단계 D] 차지 표현 계수를 0으로(ResetView가 함께 부른다).</summary>
        private static void ResetCharge()
        {
            _chargeCancelRecv = _chargeCancelStopped = _chargeCancelNothingActive = _chargeCancelStale = 0;
            _chargeCancelNotCharge = _chargeCancelNoView = _chargeSuppressedAfterCancel = 0;
        }

        /// <summary>
        /// [진단 — 단계 D] 화면이 서버 발사 취소 통보 1건을 처리한 결과를 센다.
        /// result: 0 Stopped · 1 NothingActive · 2 StaleRevision · 3 NotChargeUnit · 4 Unavailable/화면 없음
        /// (Presentation의 ServerProjectileChargeCancelResult 순서와 같다 — Application이 Presentation 타입을 참조하지 않게 int로 받는다).
        /// </summary>
        /// <param name="result">처리 결과 번호.</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordChargeCancelApplied(int result)
        {
            _chargeCancelRecv++;
            switch (result)
            {
                case 0: _chargeCancelStopped++; break;
                case 1: _chargeCancelNothingActive++; break;
                case 2: _chargeCancelStale++; break;
                case 3: _chargeCancelNotCharge++; break;
                default: _chargeCancelNoView++; break;
            }
        }

        /// <summary>[진단 — 단계 D] 서버가 이미 발사를 취소한 공격의 차지 신호를 막은 1건(싱글·Host 게이트)을 센다.</summary>
        /// <param name="type">차지 유닛 종류(지금은 세지 않고 합계만 — 인자는 이후 분리용).</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void RecordChargeSuppressedAfterServerCancel(UnitType type) => _chargeSuppressedAfterCancel++;

        /// <summary>
        /// [진단 — 단계 D] END 줄 조각 — "chargeCancel=recv:N|stopped:N|nothingActive:N|stale:N|notCharge:N|noView:N|suppressedAfterCancel:N".
        /// </summary>
        public static string DescribeCharge()
            => string.Format(CultureInfo.InvariantCulture,
                "chargeCancel=recv:{0}|stopped:{1}|nothingActive:{2}|stale:{3}|notCharge:{4}|noView:{5}|suppressedAfterCancel:{6}",
                _chargeCancelRecv, _chargeCancelStopped, _chargeCancelNothingActive, _chargeCancelStale,
                _chargeCancelNotCharge, _chargeCancelNoView, _chargeSuppressedAfterCancel);

        /// <summary>새 경로 marker에서 총구 VFX가 난 수(0이어야 정상 — END verdict 판정용).</summary>
        public static int ServerMarkerMuzzleCount => _serverMarkerMuzzle;

        /// <summary>지금 실행 위치의 역할 이름(Single / Host / Client).</summary>
        public static string RoleName => !NetworkContext.IsNetworkActive ? "Single"
            : NetworkContext.IsNetworkServer ? "Host" : "Client";

        private static AttackerRow Row(int attackerId)
        {
            if (Attackers.TryGetValue(attackerId, out AttackerRow row)) return row;
            if (Attackers.Count >= AttackerLimit) { _attackerOverflow++; return null; }
            row = new AttackerRow();
            Attackers.Add(attackerId, row);
            return row;
        }
    }
}
