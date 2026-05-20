// ============================================================================
// HexMetricsCoordinateMapper.cs
// IHexCoordinateMapper 인터페이스(Application 레이어)의 Core 레이어 구현체.
//
// 역할:
//   Application 레이어의 UseCase들이 Core 레이어(HexMetrics / ViewConverter)에
//   직접 의존하지 않도록, IHexCoordinateMapper 인터페이스를 통한 호출을
//   실제 HexMetrics / ViewConverter 호출로 위임한다.
//
// 생성/주입 위치:
//   GameBootstrapper에서 1회 생성 후 모든 UseCase 생성자에 주입.
//
// Core 레이어 — HexMetrics / ViewConverter / IHexCoordinateMapper(Application) 의존.
// Application 인터페이스를 Core에서 구현하는 것은 Clean Architecture에서 권장되는
// "의존성 역전 원칙(DIP)"의 표준 패턴이다.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Core
{
    /// <summary>
    /// IHexCoordinateMapper의 기본 구현체.
    /// HexMetrics / ViewConverter 정적 호출을 인스턴스 메서드 호출로 래핑한다.
    /// </summary>
    public class HexMetricsCoordinateMapper : IHexCoordinateMapper
    {
        /// <summary>
        /// 월드 좌표 → HexCoord 변환. HexMetrics.WorldToHex 위임.
        /// </summary>
        public HexCoord WorldToHex(Vector3 worldPos)
            => HexMetrics.WorldToHex(worldPos);

        /// <summary>
        /// HexCoord → 월드 좌표 변환. HexMetrics.HexToWorld 위임.
        /// </summary>
        public Vector3 HexToWorld(HexCoord coord)
            => HexMetrics.HexToWorld(coord);

        /// <summary>
        /// 한 타일의 높이. HexMetrics.TileHeight 위임.
        /// </summary>
        public float TileHeight => HexMetrics.TileHeight;

        /// <summary>
        /// 뷰 좌표 → 도메인 좌표 정규화. ViewConverter.FromView 위임.
        /// Red 팀이면 내부적으로 X축 반전이 적용된다 — 호출자(Application)는
        /// 반전 여부를 알 필요 없이 "도메인 기준 좌표"로만 다루면 된다.
        /// </summary>
        public Vector3 NormalizeToDomainPosition(Vector3 viewPos)
            => ViewConverter.FromView(viewPos);
    }
}
