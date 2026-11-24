// ============================================================================
// SkillLineAimMath.cs
// 공습(직선 조준) 입력의 좌표 계산 — 화면(뷰) 좌표와 도메인 좌표 사이의 방향 변환을 한곳에 모은 순수 함수.
// (2026-11-17 지원 스킬 2-L 공습 Task L-3 — Plan P-10 · U-3 · U-5 · R-3, 자가 검증 「P-10 뷰 변환」)
//
// 왜 필요한가(유니티 초급자용 설명):
//   멀티플레이에서 빨강(Red) 팀 화면은 맵이 180° 뒤집혀 보인다(ViewConverter — 맵 중심 기준 X · Z 반전).
//   그래서 화면에서 "위쪽으로 끈" 방향이 도메인(서버가 쓰는 좌표)에서는 "아래쪽"이다.
//   방향을 화면 좌표끼리 빼서 보내면 빨강 화면에서는 서버 · 상대 화면과 반대 방향이 된다(Plan R-3).
//   그래서 Plan P-10 대로 "시작점과 손가락 위치를 둘 다 도메인 좌표로 바꾼 뒤" 그 차를 방향으로 쓴다.
//   반대로 화면에 그릴 때는 도메인 방향을 다시 화면 방향으로 바꾼다(DomainDirectionToView).
//
// 이 파일은 ViewConverter(Core 정적 변환기)만 읽는다. 입력 장치 · 씬 의존 없음 → 에디터 스위트에서 바로 부를 수 있다.
// Presentation 레이어.
// ============================================================================

using UnityEngine;
using Hexiege.Core;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 공습 직선 조준의 방향 계산(뷰 ↔ 도메인). 상태 없음.
    /// </summary>
    public static class SkillLineAimMath
    {
        /// <summary>
        /// 시작점(도메인)에서 손가락 아래 지점(뷰 좌표 — 화면에서 XZ 평면으로 쏜 점)까지의 도메인 방향과 거리를 구한다(P-10).
        /// 손가락 위치를 먼저 ViewConverter.FromView 로 도메인으로 바꾼 뒤 시작점과의 차를 구한다 — 시작점도 같은 변환으로 만들어진
        /// 도메인 값이므로, 두 점이 같은 좌표계에서 빼진다(빨강 화면에서도 서버 · 상대 화면과 같은 방향).
        /// </summary>
        /// <param name="anchorDomain">직사각형 시작점(도메인 월드, 이미 맵 경계 clamp 된 값).</param>
        /// <param name="fingerView">손가락 아래 지점(뷰 좌표, Y 무시).</param>
        /// <param name="direction">정규화된 도메인 방향(Y = 0). 거리가 너무 짧으면 0 벡터.</param>
        /// <param name="distance">시작점 → 손가락 거리(도메인 월드 단위, XZ 만). 최소 드래그 판정(U-5)에 쓴다.</param>
        /// <returns>방향을 정할 수 있으면 true(거리가 0 에 가깝거나 NaN 이면 false).</returns>
        public static bool TryComputeDomainDirection(Vector3 anchorDomain, Vector3 fingerView,
                                                     out Vector3 direction, out float distance)
        {
            Vector3 fingerDomain = ViewConverter.FromView(fingerView);
            float dx = fingerDomain.x - anchorDomain.x;
            float dz = fingerDomain.z - anchorDomain.z;
            distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (float.IsNaN(distance) || float.IsInfinity(distance)) distance = 0f;
            // 정규화는 서버와 같은 함수(LineStrikeWorld → Domain LineStrikeGeometry)를 쓴다 — 0 · NaN · 너무 짧음을 같은 기준으로 거른다.
            return Hexiege.Application.LineStrikeWorld.TryNormalizeDirection(new Vector3(dx, 0f, dz), out direction);
        }

        /// <summary>
        /// 도메인 방향을 이 화면(뷰)의 방향으로 바꾼다 — 화면에 직사각형 · 화살표를 그릴 때 쓴다.
        /// 뒤집힌 화면이면 방향이 반대가 된다(ToView 는 점 변환이라 "시작점 + 방향"을 각각 바꾼 뒤 뺀다).
        /// </summary>
        /// <param name="anchorDomain">시작점(도메인).</param>
        /// <param name="directionDomain">방향(도메인, 정규화).</param>
        /// <returns>뷰 방향(Y = 0, 정규화). 바꿀 수 없으면 뷰 +Z.</returns>
        public static Vector3 DomainDirectionToView(Vector3 anchorDomain, Vector3 directionDomain)
        {
            Vector3 a = ViewConverter.ToView(anchorDomain);
            Vector3 b = ViewConverter.ToView(anchorDomain + directionDomain);
            Vector3 d = new Vector3(b.x - a.x, 0f, b.z - a.z);
            return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
        }

        /// <summary>
        /// 첫 터치 직후(아직 끌지 않음) 직사각형이 향할 기본 방향 — 화면 위쪽(뷰 +Z)을 도메인으로 바꾼 값.
        /// 빨강 화면이면 도메인 −Z 가 된다(화면에서는 언제나 위쪽을 향해 보인다).
        /// </summary>
        /// <param name="anchorDomain">시작점(도메인).</param>
        /// <returns>도메인 방향(정규화, Y = 0).</returns>
        public static Vector3 DefaultDomainDirection(Vector3 anchorDomain)
        {
            Vector3 anchorView = ViewConverter.ToView(anchorDomain);
            Vector3 upDomain = ViewConverter.FromView(anchorView + Vector3.forward);
            Vector3 d = new Vector3(upDomain.x - anchorDomain.x, 0f, upDomain.z - anchorDomain.z);
            return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
        }

        /// <summary>
        /// 최소 드래그 판정(U-5 · U-10). 경계값(거리 = 임계값)은 발사다(「임계값 이상이면 발사」).
        /// </summary>
        /// <param name="distance">시작점 → 손을 뗀 지점 거리(도메인 월드).</param>
        /// <param name="threshold">임계값(월드).</param>
        /// <returns>발사할 만큼 끌었으면 true.</returns>
        public static bool IsDragEnough(float distance, float threshold)
        {
            return distance >= threshold;
        }
    }
}
