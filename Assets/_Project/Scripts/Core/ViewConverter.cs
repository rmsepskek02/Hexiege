// ============================================================================
// ViewConverter.cs
// 멀티플레이 팀별 관점 변환 시스템.
//
// 목적:
//   Blue팀(Host)은 자기 Castle이 맵 하단에 위치하고 화면에서도 하단에 보임.
//   Red팀(Client)은 자기 Castle이 맵 상단에 위치하지만 화면에서는 하단에 보여야 함.
//   카메라 Z축 회전 방식은 스프라이트가 뒤집혀 보이므로 사용하지 않음.
//
// 해결:
//   도메인 좌표(Blue 기준)는 그대로 유지하고,
//   Red 클라이언트에서 렌더링 위치만 맵 중심 기준으로 반전(Flip).
//   Flip(pos) = 2 * mapCenter - pos (X, Z 반전, Y는 높이로 통과, 역함수도 동일)
//
// 사용처:
//   - HexGridRenderer: 타일/금광 렌더링 위치
//   - UnitFactory / BuildingFactory: 스폰 위치
//   - UnitView: 이동 목표 위치, 방향 반전
//   - InputHandler: 화면 터치 → 도메인 좌표 역변환
//   - ProductionTicker: 랠리포인트 마커 위치
//
// Core 레이어 — Unity 의존 (Vector3, Mathf).
// ============================================================================

using UnityEngine;

namespace Hexiege.Core
{
    /// <summary>
    /// 팀별 관점 변환 정적 유틸리티.
    /// Red팀이면 맵 중심 기준으로 위치를 반전하여 자기 진영이 화면 하단에 보이도록 함.
    /// Blue팀(또는 싱글플레이)에서는 변환 없이 원래 좌표를 그대로 반환.
    /// </summary>
    public static class ViewConverter
    {
        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary> Red팀이면 true — 위치 반전 활성화. </summary>
        private static bool _isFlipped;

        /// <summary> 맵 중심 월드 좌표. Flip 공식의 축(pivot). </summary>
        private static Vector3 _mapCenter;

        /// <summary> 현재 반전 모드인지 여부 (외부 읽기 전용). </summary>
        public static bool IsFlipped => _isFlipped;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// ViewConverter 초기화. GameBootstrapper에서 맵 로드 후 호출.
        /// </summary>
        /// <param name="isFlipped">Red팀이면 true, Blue팀/싱글플레이면 false.</param>
        /// <param name="mapCenter">맵 중심 월드 좌표 (HexMetrics.GridCenter 결과).</param>
        public static void Setup(bool isFlipped, Vector3 mapCenter)
        {
            _isFlipped = isFlipped;
            _mapCenter = mapCenter;
        }

        /// <summary>
        /// ViewConverter를 기본 상태로 리셋 (싱글플레이 / 씬 전환 시).
        /// </summary>
        public static void Reset()
        {
            _isFlipped = false;
            _mapCenter = Vector3.zero;
        }

        // ====================================================================
        // 좌표 변환
        // ====================================================================

        /// <summary>
        /// 도메인 월드 좌표 → 뷰(렌더링) 좌표 변환.
        /// Red팀이면 맵 중심 기준으로 X, Z를 반전 (XZ 평면).
        /// Y축은 높이이므로 그대로 통과.
        /// Blue팀이면 변환 없이 그대로 반환.
        /// 공식: viewPos = 2 * mapCenter - domainPos (X, Z만 적용)
        /// </summary>
        /// <param name="domainPos">도메인 기준 월드 좌표 (HexMetrics.HexToWorld 결과).</param>
        /// <returns>화면에 배치할 뷰 좌표.</returns>
        public static Vector3 ToView(Vector3 domainPos)
        {
            if (!_isFlipped) return domainPos;

            return new Vector3(
                2f * _mapCenter.x - domainPos.x,
                domainPos.y, // Y축은 높이 — 반전하지 않음
                2f * _mapCenter.z - domainPos.z
            );
        }

        /// <summary>
        /// 뷰(렌더링) 좌표 → 도메인 월드 좌표 역변환.
        /// 수학적으로 ToView와 동일한 공식 (자기 역함수).
        /// 화면 터치 위치를 도메인 좌표로 변환할 때 사용.
        /// </summary>
        /// <param name="viewPos">화면상의 월드 좌표 (ScreenToWorldPoint 결과).</param>
        /// <returns>도메인 기준 월드 좌표.</returns>
        public static Vector3 FromView(Vector3 viewPos)
        {
            // Flip은 자기 역함수: f(f(x)) = x
            // 따라서 FromView = ToView
            return ToView(viewPos);
        }

        // ====================================================================
        // 방향 변환
        // ====================================================================

        /// <summary>
        /// 헥스 방향(0~5)을 팀 관점에 맞게 반전.
        /// Red팀이면 반대 방향으로 변환: (dir + 3) % 6.
        /// Blue팀이면 그대로 반환.
        /// 6방향 헥스에서 +3은 정반대 방향.
        /// </summary>
        /// <param name="dir">원본 HexDirection (도메인 기준).</param>
        /// <returns>뷰 기준 HexDirection.</returns>
        public static Hexiege.Domain.HexDirection FlipDirection(Hexiege.Domain.HexDirection dir)
        {
            if (!_isFlipped) return dir;

            return (Hexiege.Domain.HexDirection)(((int)dir + 3) % 6);
        }

        // [Phase 2] FlatTopSortingOrder() 제거 — 3D Z-buffer로 렌더 순서 자동 처리
    }
}
