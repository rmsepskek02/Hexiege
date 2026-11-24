// ============================================================================
// LineStrikeGeometry.cs
// 공습(직선 연속 폭격 — SkillMechanicType.LineStrike) 의 "직선 판정"을 계산하는 순수 함수 모음.
// (2026-11-17 지원 스킬 2-L 공습 Task — _Tasks/2026-11-17/09_12_support-skill-air-strike, Plan L-1 · P-2 · P-9 · P-13 · U-14)
//
// 무엇을 하나(초급자용 설명):
//   공습은 원이 아니라 "길쭉한 직사각형" 범위를 폭격한다. 직사각형은 아래 네 값으로 정해진다.
//     - 시작점(start)      : 플레이어가 화면을 누른 자리(도메인 월드 좌표의 X · Z).
//     - 방향(dir)          : 시작점 → 손가락 쪽으로 정규화한(길이 1) 방향.
//     - 길이(length)       : 시작점에서 방향으로 몇 칸까지인가(월드 단위).
//     - 반폭(halfWidth)    : 직선에서 옆으로 얼마나 떨어져도 맞는가(폭 ÷ 2).
//   어떤 점 P가 직사각형 안인지는 "두 숫자"로 판단한다.
//     - 진행 거리 t  = (P − 시작점) · 방향   → 시작점에서 방향으로 얼마나 나아간 곳인가(0 이면 시작점, length 면 끝점).
//     - 옆 거리   d  = |(P − 시작점) × 방향| → 직선에서 옆으로 얼마나 떨어져 있는가.
//     → 0 ≤ t ≤ length 이고 d ≤ halfWidth 면 안이다(경계 위 포함 — Plan LS-T1).
//
//   연속 폭격(5회)은 직선을 같은 길이의 구간 5개로 나눠 "k번째 폭격 = k번째 구간"만 친다(P-3 · P-9).
//     구간 k = [k·L/N, (k+1)·L/N) — 반열린 구간(왼쪽 포함 · 오른쪽 제외), 마지막 구간만 끝점 포함(닫힌 구간).
//     그래서 직선 위의 모든 점은 "정확히 한 구간"에만 속한다(정지한 대상이 경계에 있어도 한 폭격에만 걸린다).
//     구간 번호는 SegmentIndexOf 하나로만 계산한다 — 같은 함수를 쓰므로 두 구간에 동시에 속하는 일이 생기지 않는다.
//
//   맵 밖 잘라내기(U-14 · P-13): 직선이 맵 밖으로 나가면 맵 안 부분만 쓴다.
//     「유효 길이」 = 시작점에서 방향으로 나아가며 맵 경계를 처음 벗어나기 직전까지의 거리(최대 length).
//     맵 경계는 HexMetrics.ComputeMapWorldBounds가 만드는 축 정렬 사각형(AABB)이라, 직선이 한 번 나갔다가 다시 들어오는
//     모양은 생기지 않는다(사각형은 볼록 도형 — 직선과 만나는 구간이 언제나 한 덩어리다).
//     구간 5개는 「원래 길이 L」 기준으로 나눈 그대로 두고, 각 폭격의 판정 범위 = 자기 구간 ∩ [0, 유효 길이] 이다.
//     그 교집합이 비면 그 폭격은 피해 · VFX 없이 건너뛴다(IsStrikeInRange == false).
//
//   이 파일 하나를 서버 판정(SkillActivationUseCase · UnitCombatUseCase), 다음 단계의 조준 표시(L-4) · 화면 VFX(L-5)가
//   "같은 함수"로 함께 쓴다(Plan R-14 — 계산 위치가 갈라져 서로 다른 값이 나오는 것을 막는다).
//
// 좌표 · 단위: 모두 도메인 월드(XZ 평면, Blue 기준). 빨강 화면 뒤집힘(ViewConverter)은 화면 단계에서만 한다.
//
// Domain 레이어 — 순수 C#. UnityEngine / Hexiege.Core 참조 금지(그래서 Vector3 대신 float 두 개(X · Z)로 받는다).
//   Vector3로 부르고 싶은 Application · Presentation 쪽은 Application/Skill/LineStrikePlan.cs 의 LineStrikeWorld 래퍼를 쓴다.
// ============================================================================

using System;

namespace Hexiege.Domain
{
    /// <summary>
    /// 공습 직선(직사각형) 판정 · 구간 분할 · 맵 안 유효 길이를 계산하는 순수 함수 모음(상태 없음).
    /// </summary>
    public static class LineStrikeGeometry
    {
        /// <summary>
        /// 경계 판정 여유(월드 단위). 실수 계산 오차(예: 0.1을 여러 번 더한 값) 때문에 "정확히 경계 위"인 점이
        /// 아주 조금 밖으로 계산되는 것을 막는다. 0.0001칸은 화면에서 구별할 수 없는 크기다.
        /// </summary>
        public const float BoundaryEpsilon = 1e-4f;

        /// <summary>
        /// 방향으로 인정하는 최소 길이. 이보다 짧은 벡터(0 벡터 포함)는 "방향이 없다"로 보고 거부한다(Plan P-11).
        /// </summary>
        public const float MinDirectionLength = 1e-4f;

        // 구간 번호 계산의 여유(구간 길이 대비 비율). SegmentIndexOf 설명 참고.
        private const double SegmentEpsilon = 1e-6;

        /// <summary>
        /// XZ 방향 벡터를 길이 1로 정규화한다. 0 벡터 · 너무 짧은 벡터 · NaN · 무한대가 섞이면 실패한다(서버 방향 재검증 — P-11).
        /// </summary>
        /// <param name="x">방향 X 성분(도메인 월드).</param>
        /// <param name="z">방향 Z 성분(도메인 월드).</param>
        /// <param name="nx">정규화된 X 성분(실패하면 0).</param>
        /// <param name="nz">정규화된 Z 성분(실패하면 0).</param>
        /// <returns>유효한 방향이면 true.</returns>
        public static bool TryNormalizeDirection(float x, float z, out float nx, out float nz)
        {
            nx = 0f;
            nz = 0f;
            // NaN · 무한대 거부 — NaN은 어떤 비교도 false라 아래 길이 검사만으로는 걸러지지 않을 수 있어 먼저 본다.
            if (!IsFinite(x) || !IsFinite(z)) return false;
            // double로 길이를 잰다(아주 큰 값의 제곱이 float 범위를 넘지 않게).
            double len = Math.Sqrt((double)x * x + (double)z * z);
            if (!(len >= MinDirectionLength) || double.IsInfinity(len)) return false;
            nx = (float)(x / len);
            nz = (float)(z / len);
            return true;
        }

        /// <summary>
        /// 점 P가 직선 직사각형(시작점 · 방향 · 길이 · 반폭) 안인지 판정하고, 안이면 진행 거리 t를 돌려준다.
        /// 경계 위(t = 0, t = 길이, 옆 거리 = 반폭)는 안이다(LS-T1). 방향은 정규화돼 있어야 한다(TryNormalizeDirection 결과).
        /// </summary>
        /// <param name="startX">시작점 X.</param>
        /// <param name="startZ">시작점 Z.</param>
        /// <param name="dirX">정규화된 방향 X.</param>
        /// <param name="dirZ">정규화된 방향 Z.</param>
        /// <param name="length">직선 길이(월드 단위, 0 이상).</param>
        /// <param name="halfWidth">반폭(폭 ÷ 2, 월드 단위, 0 이상).</param>
        /// <param name="px">판정할 점 X(대상이 선 타일 중심 — 스킬 규칙 27 · 28과 같은 기준).</param>
        /// <param name="pz">판정할 점 Z.</param>
        /// <param name="along">안이면 진행 거리 t를 [0, length]로 잘라 돌려준다(밖이면 0).</param>
        /// <returns>직사각형 안이면 true.</returns>
        public static bool IsInside(float startX, float startZ, float dirX, float dirZ, float length, float halfWidth,
                                    float px, float pz, out float along)
        {
            along = 0f;
            if (!(length >= 0f) || !(halfWidth >= 0f)) return false; // 음수 · NaN 길이/폭은 판정 불가
            float relX = px - startX;
            float relZ = pz - startZ;
            float t = relX * dirX + relZ * dirZ;           // 진행 거리(내적)
            float d = Math.Abs(relX * dirZ - relZ * dirX); // 옆 거리(2차원 외적의 크기)
            // NaN이 섞이면 아래 비교가 모두 false라 자동으로 "밖"이 된다.
            if (!(t >= -BoundaryEpsilon) || !(t <= length + BoundaryEpsilon)) return false;
            if (!(d <= halfWidth + BoundaryEpsilon)) return false;
            along = t < 0f ? 0f : (t > length ? length : t);
            return true;
        }

        /// <summary>
        /// 진행 거리 t가 몇 번째 구간인지 계산한다(0부터). 구간 k = [k·L/N, (k+1)·L/N), 마지막 구간만 끝점 포함(P-9).
        /// 직선 위의 모든 점은 정확히 한 구간에 속한다 — 구간 판정은 반드시 이 함수 하나로만 한다.
        /// </summary>
        /// <param name="along">진행 거리(IsInside가 돌려준 값 — [0, length]).</param>
        /// <param name="length">직선 길이(원래 길이 L — 유효 길이가 아니다, P-13).</param>
        /// <param name="count">구간 수(폭격 횟수). 1 이하면 언제나 0.</param>
        /// <returns>구간 번호(0 ~ count−1).</returns>
        public static int SegmentIndexOf(float along, float length, int count)
        {
            if (count <= 1 || !(length > 0f)) return 0;
            if (!(along > 0f)) return 0; // 음수 · NaN · 0 → 첫 구간
            // 구간 번호 = 내림(t × N ÷ L). 경계값(예: 3.6 = 6 × 3 ÷ 5)은 float로 저장되면 3.5999999처럼 아주 조금 작아져
            //   그대로 내림하면 앞 구간(2)으로 밀린다(자가 검증 LS-T2에서 실제로 걸렸다). 그래서 몫에 아주 작은 여유(SegmentEpsilon,
            //   구간 길이의 100만분의 1)를 더해 "경계 위 점은 뒤 구간"이라는 반열린 구간 약속을 지킨다.
            //   이 여유보다 더 경계에 가까운 점(월드 0.000001칸 수준)이 뒤 구간으로 가는 것은 화면 · 판정에서 구별되지 않는다.
            //   어떤 점이든 이 한 함수로만 구간을 정하므로 「정확히 한 구간」 성질은 그대로다.
            int index = (int)Math.Floor((double)along * count / length + SegmentEpsilon);
            if (index < 0) return 0;
            if (index >= count) return count - 1; // t = L(끝점)은 마지막 구간(닫힌 구간)
            return index;
        }

        /// <summary>
        /// k번째 구간의 시작 · 끝 진행 거리(원래 길이 L 기준). 진단 로그 · 화면 VFX 위치 계산용.
        /// </summary>
        /// <param name="index">구간 번호(0부터).</param>
        /// <param name="count">구간 수(1 이상이어야 의미가 있다 — 1 미만이면 전체 [0, L]).</param>
        /// <param name="length">원래 길이 L.</param>
        /// <param name="from">구간 시작 진행 거리.</param>
        /// <param name="to">구간 끝 진행 거리.</param>
        public static void GetSegmentBounds(int index, int count, float length, out float from, out float to)
        {
            if (count < 1 || !(length > 0f))
            {
                from = 0f;
                to = length > 0f ? length : 0f;
                return;
            }
            from = (float)((double)length * index / count);
            to = (float)((double)length * (index + 1) / count);
        }

        /// <summary>
        /// k번째 폭격의 판정 범위(자기 구간 ∩ [0, 유효 길이])가 비어 있지 않은지 — 비면 그 폭격은 건너뛴다(U-14 · P-13).
        /// 첫 폭격(k = 0)은 시작점이 언제나 맵 안이므로(서버가 시작점을 맵 경계 안 점으로 재검증 — 규칙 26) 건너뛰지 않는다.
        /// </summary>
        /// <param name="index">폭격 번호(0부터).</param>
        /// <param name="count">폭격 횟수.</param>
        /// <param name="length">원래 길이 L.</param>
        /// <param name="effectiveLength">맵 안 유효 길이(ComputeEffectiveLength 결과).</param>
        /// <returns>판정할 범위가 있으면 true, 맵 밖이라 건너뛰어야 하면 false.</returns>
        public static bool IsStrikeInRange(int index, int count, float length, float effectiveLength)
        {
            if (index <= 0) return true;
            GetSegmentBounds(index, count, length, out float from, out _);
            // 구간 시작이 유효 길이보다 앞이어야 겹치는 부분(길이 > 0)이 있다. 같으면 한 점뿐이라 건너뛴다.
            return from < effectiveLength;
        }

        /// <summary>
        /// 맵 안 유효 길이 — 시작점에서 방향으로 나아가며 맵 경계(축 정렬 사각형)를 처음 벗어나기 직전까지의 거리(최대 length).
        /// 서버 판정 · 조준 표시 · 화면 VFX가 함께 쓰는 단일 함수다(P-13 · R-14).
        /// 시작점이 이미 맵 밖이거나 값이 잘못되면(NaN 등) 0. 경계 위 시작점에서 바깥쪽을 향하면 0.
        /// </summary>
        /// <param name="startX">시작점 X(도메인 월드).</param>
        /// <param name="startZ">시작점 Z.</param>
        /// <param name="dirX">정규화된 방향 X.</param>
        /// <param name="dirZ">정규화된 방향 Z.</param>
        /// <param name="length">원래 길이 L.</param>
        /// <param name="minX">맵 경계 최소 X(HexMetrics.ComputeMapWorldBounds의 min.x).</param>
        /// <param name="minZ">맵 경계 최소 Z.</param>
        /// <param name="maxX">맵 경계 최대 X.</param>
        /// <param name="maxZ">맵 경계 최대 Z.</param>
        /// <returns>유효 길이(0 ~ length).</returns>
        public static float ComputeEffectiveLength(float startX, float startZ, float dirX, float dirZ, float length,
                                                   float minX, float minZ, float maxX, float maxZ)
        {
            if (!(length > 0f) || float.IsInfinity(length)) return 0f;
            if (!IsFinite(startX) || !IsFinite(startZ) || !IsFinite(dirX) || !IsFinite(dirZ)) return 0f;
            // 시작점이 맵 밖이면 맵 안 부분이 없다(서버는 시작점을 미리 재검증하므로 정상 조작에서는 나오지 않는다).
            if (startX < minX || startX > maxX || startZ < minZ || startZ > maxZ) return 0f;

            // 「슬랩(slab) 방법」: X 방향 벽 · Z 방향 벽 각각에 닿는 진행 거리를 구해 더 가까운 쪽에서 나간다.
            //   방향 성분이 0이면 그 축의 벽과는 평행이라 영원히 닿지 않는다(제한 없음).
            double exit = double.PositiveInfinity;
            const double parallel = 1e-9;
            if (dirX > parallel) exit = Math.Min(exit, (maxX - (double)startX) / dirX);
            else if (dirX < -parallel) exit = Math.Min(exit, (minX - (double)startX) / dirX);
            if (dirZ > parallel) exit = Math.Min(exit, (maxZ - (double)startZ) / dirZ);
            else if (dirZ < -parallel) exit = Math.Min(exit, (minZ - (double)startZ) / dirZ);

            if (exit < 0d) exit = 0d;          // 실수 오차로 아주 작은 음수가 나오면 0
            if (exit > length) exit = length;  // 맵 안에서 끝나면 원래 길이 그대로
            return (float)exit;
        }

        // NaN · 무한대가 아닌 보통 숫자인지.
        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
