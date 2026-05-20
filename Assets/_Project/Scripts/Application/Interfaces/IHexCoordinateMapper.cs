// ============================================================================
// IHexCoordinateMapper.cs
// 월드 좌표 ↔ 헥스 좌표 변환을 추상화한 인터페이스.
//
// 왜 필요한가:
//   Application 레이어는 Core 레이어(HexMetrics / ViewConverter)에 직접 의존하면
//   안 된다. 좌표 변환 로직을 인터페이스 뒤로 숨겨, UseCase가 Core를 모르게 한다.
//
//   - HexMetrics.WorldToHex      → IHexCoordinateMapper.WorldToHex
//   - HexMetrics.HexToWorld      → IHexCoordinateMapper.HexToWorld
//   - HexMetrics.TileHeight      → IHexCoordinateMapper.TileHeight
//   - ViewConverter.FromView     → IHexCoordinateMapper.NormalizeToDomainPosition
//
// 구현체:
//   HexMetricsCoordinateMapper (Core 레이어)
//
// 의존 주입:
//   GameBootstrapper(composition root)에서 1회 생성 후 UseCase 생성자에 주입.
//
// Application 레이어 — UnityEngine.Vector3만 사용 (Unity 의존 허용).
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 월드 좌표 ↔ 헥스 좌표 변환과 도메인 좌표 정규화를 제공하는 인터페이스.
    /// Application 레이어가 Core 레이어(HexMetrics / ViewConverter)에 직접
    /// 의존하지 않도록 좌표 변환을 추상화한다.
    /// </summary>
    public interface IHexCoordinateMapper
    {
        /// <summary>
        /// 월드 좌표(도메인 기준)를 HexCoord로 변환한다.
        /// HexMetrics.WorldToHex와 동일한 의미.
        /// </summary>
        /// <param name="worldPos">도메인 좌표계의 월드 위치</param>
        /// <returns>해당 위치가 속한 헥스 타일 좌표</returns>
        HexCoord WorldToHex(Vector3 worldPos);

        /// <summary>
        /// HexCoord를 월드 좌표(도메인 기준)로 변환한다.
        /// HexMetrics.HexToWorld와 동일한 의미.
        /// </summary>
        /// <param name="coord">헥스 타일 좌표</param>
        /// <returns>해당 타일의 중심 월드 위치</returns>
        Vector3 HexToWorld(HexCoord coord);

        /// <summary>
        /// 한 타일의 높이(또는 PointyTop에서의 행 간격).
        /// 사거리 계산 시 헥스 단위 → 월드 단위 변환 계수로 사용.
        /// HexMetrics.TileHeight와 동일한 값.
        /// </summary>
        float TileHeight { get; }

        /// <summary>
        /// 뷰 좌표(보이는 그대로)를 도메인 좌표(서버 기준 정규화)로 변환한다.
        /// Red 팀의 경우 X축 반전이 적용되므로, 점령 판정 등 도메인 로직에서
        /// 사용하기 전에 반드시 이 메서드를 거쳐야 한다.
        /// ViewConverter.FromView와 동일한 의미.
        /// </summary>
        /// <param name="viewPos">현재 보이는 그대로의 월드 좌표</param>
        /// <returns>도메인 기준으로 정규화된 월드 좌표</returns>
        Vector3 NormalizeToDomainPosition(Vector3 viewPos);
    }
}
