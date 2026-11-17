// ============================================================================
// LineStrikePlan.cs
// 공습(직선 연속 폭격 — SkillMechanicType.LineStrike) 이 Application 레이어에서 주고받는 작은 값 · 도우미 모음.
// (2026-11-17 지원 스킬 2-L 공습 Task — _Tasks/2026-11-17/09_12_support-skill-air-strike, Plan L-1 · L-2)
//
// 들어 있는 것(초급자용 설명):
//   - LineStrikePlan        : 실행기(LineStrikeExecutor)가 스킬 데이터에서 읽어 "이렇게 폭격해 줘"라고 넘기는 값 묶음
//                             (길이 · 폭 · 폭격 횟수 · 첫 폭격 지연 · 간격 · 1회 피해).
//   - MapWorldBoundsProvider: "지금 맵의 경계 사각형(min · max)을 알려 줘" 함수 모양. 맵 경계 계산(HexMetrics)은 Core에 있어
//                             Application이 직접 부를 수 없으므로 조합 루트(GameBootstrapper)가 이 모양의 함수를 넣어 준다.
//   - LineStrikeWorld       : Domain 순수 함수(LineStrikeGeometry — float 두 개로 받는다)를 Vector3로 부르는 얇은 래퍼.
//                             서버 판정과 다음 단계의 조준 표시(L-4) · 화면 VFX(L-5)가 같은 계산을 쓰게 하는 입구다(Plan R-14).
//   - LineStrikeHitResult   : 폭격 1회의 결과(맞은 유닛 · 건물 수, 이미 맞아 건너뛴 수, 사망 수 등 — 진단 로그용).
//   - LineStrikeEndedRecord : 한 번 시전(폭격 5회)이 끝났을 때의 집계(완료 · 건너뜀 · 버림 · 고유 대상 수 · 끝난 이유 — 확인 · 검증용).
//
// Application 레이어 — Domain 의존 + UnityEngine.Vector3(값 타입)만 사용. Netcode · Core 참조 없음.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 공습 1회 시전의 폭격 계획(실행기 → 발동 UseCase). 스킬 데이터(SkillData)의 직선 칸 값을 그대로 담는다.
    /// </summary>
    public readonly struct LineStrikePlan
    {
        /// <summary> 직선 길이(월드 단위). 시작점에서 방향으로 이만큼 뻗는다. </summary>
        public float Length { get; }

        /// <summary> 직선 폭(월드 단위). 직선에서 양옆으로 폭 ÷ 2까지가 범위다. </summary>
        public float Width { get; }

        /// <summary> 반폭(폭 ÷ 2). 판정 함수가 쓰는 값. </summary>
        public float HalfWidth => Width * 0.5f;

        /// <summary> 폭격 횟수(= 직선을 나누는 구간 수). </summary>
        public int StrikeCount { get; }

        /// <summary> 발동부터 첫 폭격까지의 시간(초) — 스킬 데이터의 「착탄 지연」 칸 값. </summary>
        public float FirstDelay { get; }

        /// <summary> 폭격 사이 간격(초). k번째 폭격 시각 = FirstDelay + k × Interval. </summary>
        public float Interval { get; }

        /// <summary> 폭격 1회가 대상 1개에 주는 원본 피해(감쇄 전, ×10 스케일). 한 시전에 대상당 1회만 맞는다(U-12). </summary>
        public int RawDamage { get; }

        /// <summary>
        /// 폭격 계획 생성(값 검사는 하지 않는다 — 받는 쪽 SkillActivationUseCase가 검사하고 잘못되면 진단 Warn을 남긴다).
        /// </summary>
        /// <param name="length">직선 길이.</param>
        /// <param name="width">직선 폭.</param>
        /// <param name="strikeCount">폭격 횟수.</param>
        /// <param name="firstDelay">첫 폭격 지연(초).</param>
        /// <param name="interval">폭격 간격(초).</param>
        /// <param name="rawDamage">1회 원본 피해.</param>
        public LineStrikePlan(float length, float width, int strikeCount, float firstDelay, float interval, int rawDamage)
        {
            Length = length;
            Width = width;
            StrikeCount = strikeCount;
            FirstDelay = firstDelay;
            Interval = interval;
            RawDamage = rawDamage;
        }
    }

    /// <summary>
    /// 지금 맵의 경계 사각형(도메인 월드 AABB — HexMetrics.ComputeMapWorldBounds와 같은 값)을 알려 주는 함수 모양.
    /// 맵이 아직 없으면 false를 돌려준다(그때 공습은 맵 밖 잘라내기 없이 원래 길이를 쓴다 — 진단 로그 MapBounds=Missing).
    /// </summary>
    /// <param name="min">경계 최소 모서리(작은 X · Z).</param>
    /// <param name="max">경계 최대 모서리(큰 X · Z).</param>
    /// <returns>경계를 알면 true.</returns>
    public delegate bool MapWorldBoundsProvider(out Vector3 min, out Vector3 max);

    /// <summary>
    /// Domain 순수 함수 LineStrikeGeometry를 Vector3(도메인 월드 · XZ 평면)로 부르는 래퍼.
    /// 서버 판정 · 조준 표시 · 화면 VFX가 이 래퍼(=같은 순수 함수)를 함께 쓴다(Plan P-13 · R-14).
    /// </summary>
    public static class LineStrikeWorld
    {
        /// <summary>
        /// 방향 벡터의 X · Z만 보고 길이 1로 정규화한다(Y는 버린다). 0 · 너무 짧음 · NaN · 무한대면 실패(서버 방향 재검증 — P-11).
        /// </summary>
        /// <param name="direction">방향(도메인 월드). Y 성분은 무시된다.</param>
        /// <param name="normalized">정규화된 방향(Y = 0). 실패하면 0 벡터.</param>
        /// <returns>유효한 방향이면 true.</returns>
        public static bool TryNormalizeDirection(Vector3 direction, out Vector3 normalized)
        {
            bool ok = LineStrikeGeometry.TryNormalizeDirection(direction.x, direction.z, out float nx, out float nz);
            normalized = new Vector3(nx, 0f, nz);
            return ok;
        }

        /// <summary>
        /// 맵 안 유효 길이(시작점부터 맵 경계를 처음 벗어나기 직전까지, 최대 length — U-14 · P-13).
        /// </summary>
        /// <param name="start">시작점(도메인 월드).</param>
        /// <param name="direction">정규화된 방향(도메인 월드, TryNormalizeDirection 결과).</param>
        /// <param name="length">원래 길이.</param>
        /// <param name="boundsMin">맵 경계 최소 모서리.</param>
        /// <param name="boundsMax">맵 경계 최대 모서리.</param>
        /// <returns>유효 길이(0 ~ length).</returns>
        public static float ComputeEffectiveLength(Vector3 start, Vector3 direction, float length, Vector3 boundsMin, Vector3 boundsMax)
        {
            return LineStrikeGeometry.ComputeEffectiveLength(start.x, start.z, direction.x, direction.z, length,
                boundsMin.x, boundsMin.z, boundsMax.x, boundsMax.z);
        }

        /// <summary>
        /// 경계 함수(MapWorldBoundsProvider)로 맵 안 유효 길이를 계산한다. 경계를 모르면(함수 없음 · 맵 없음) 원래 길이를 돌려준다.
        /// </summary>
        /// <param name="start">시작점(도메인 월드).</param>
        /// <param name="direction">정규화된 방향.</param>
        /// <param name="length">원래 길이.</param>
        /// <param name="bounds">맵 경계 함수(null 허용).</param>
        /// <param name="boundsKnown">경계를 알아서 잘라냈으면 true(진단 로그 MapBounds=Known/Missing).</param>
        /// <returns>유효 길이.</returns>
        public static float ComputeEffectiveLength(Vector3 start, Vector3 direction, float length, MapWorldBoundsProvider bounds,
                                                   out bool boundsKnown)
        {
            boundsKnown = false;
            if (bounds != null && bounds(out Vector3 min, out Vector3 max))
            {
                boundsKnown = true;
                return ComputeEffectiveLength(start, direction, length, min, max);
            }
            return length > 0f ? length : 0f;
        }

        /// <summary>
        /// 점이 직선 직사각형 안인지 판정하고 진행 거리를 돌려준다(경계 포함 — LS-T1).
        /// </summary>
        /// <param name="start">시작점.</param>
        /// <param name="direction">정규화된 방향.</param>
        /// <param name="length">길이.</param>
        /// <param name="halfWidth">반폭.</param>
        /// <param name="point">판정할 점(Y 무시).</param>
        /// <param name="along">진행 거리([0, length]).</param>
        /// <returns>안이면 true.</returns>
        public static bool IsInside(Vector3 start, Vector3 direction, float length, float halfWidth, Vector3 point, out float along)
        {
            return LineStrikeGeometry.IsInside(start.x, start.z, direction.x, direction.z, length, halfWidth,
                point.x, point.z, out along);
        }
    }

    /// <summary>
    /// 폭격 1회의 결과(UnitCombatUseCase.ApplySkillLineStrike → SkillActivationUseCase 진단 로그).
    /// </summary>
    public readonly struct LineStrikeHitResult
    {
        /// <summary> 이번 폭격에 맞은 적 유닛 수(이번 시전에서 처음 맞은 것만). </summary>
        public int Units { get; }
        /// <summary> 이번 폭격에 맞은 적 건물 수. </summary>
        public int Buildings { get; }
        /// <summary> 구간 안에 있었지만 앞 폭격에서 이미 맞아 건너뛴 대상 수(U-12). </summary>
        public int AlreadyHitSkipped { get; }
        /// <summary> 구간 안에 있었지만 맵 안 유효 길이 밖이라 맞지 않은 대상 수(U-14). </summary>
        public int OutOfMapSkipped { get; }
        /// <summary> 구간 안 아군 수(맞지 않는다 — 「아군 0」 판정 근거). </summary>
        public int AlliesInSegment { get; }
        /// <summary> 이번 폭격으로 죽은 유닛 수. </summary>
        public int UnitKills { get; }
        /// <summary> 이번 폭격으로 무너진 건물 수. </summary>
        public int BuildingKills { get; }

        /// <summary> 결과 생성. </summary>
        public LineStrikeHitResult(int units, int buildings, int alreadyHitSkipped, int outOfMapSkipped, int alliesInSegment,
                                   int unitKills, int buildingKills)
        {
            Units = units;
            Buildings = buildings;
            AlreadyHitSkipped = alreadyHitSkipped;
            OutOfMapSkipped = outOfMapSkipped;
            AlliesInSegment = alliesInSegment;
            UnitKills = unitKills;
            BuildingKills = buildingKills;
        }
    }

    /// <summary>
    /// 한 번 시전(폭격 N회)이 끝났을 때의 집계(확인 · 검증용 — SkillActivationUseCase.LastLineStrikeEnded).
    /// </summary>
    public readonly struct LineStrikeEndedRecord
    {
        /// <summary> 발동 순번. 0이면 아직 끝난 시전이 없다. </summary>
        public int Sequence { get; }
        /// <summary> 실제로 판정한 폭격 수(맞은 대상이 0이어도 판정했으면 센다). </summary>
        public int Done { get; }
        /// <summary> 맵 밖이라 건너뛴 폭격 수(U-14). </summary>
        public int Skipped { get; }
        /// <summary> 경기 종료 · 초기화로 버린 폭격 수. </summary>
        public int Dropped { get; }
        /// <summary> 이번 시전에서 맞은 고유 유닛 수. </summary>
        public int UniqueUnits { get; }
        /// <summary> 이번 시전에서 맞은 고유 건물 수. </summary>
        public int UniqueBuildings { get; }
        /// <summary> 끝난 이유(Complete · GameEnd · Cleared). </summary>
        public string Reason { get; }

        /// <summary> 집계 생성. </summary>
        public LineStrikeEndedRecord(int sequence, int done, int skipped, int dropped, int uniqueUnits, int uniqueBuildings, string reason)
        {
            Sequence = sequence;
            Done = done;
            Skipped = skipped;
            Dropped = dropped;
            UniqueUnits = uniqueUnits;
            UniqueBuildings = uniqueBuildings;
            Reason = reason ?? string.Empty;
        }
    }
}
