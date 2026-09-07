// ============================================================================
// DecorationDefinition.cs
// 맵 위에 놓인 "장식물" 하나의 정의(바위, 나무 같은 순수 시각 요소).
//
// 왜 전부 정수(int) ID인가:
//   장식물의 실제 재질(Material), 크기(scale), 회전 각도는 소수점 값(float)이다.
//   그런데 소수점 값은 기기·플랫폼마다 미세하게 다르게 계산될 수 있어서,
//   같은 맵인데도 해시(SHA-256)가 달라질 위험이 있다.
//   그래서 canonical 데이터에는 "몇 번 재질", "몇 번째 크기 단계"처럼 정수 ID만 넣고,
//   실제 float 값은 Presentation 레이어가 그 ID를 보고 찾아 쓰도록 분리했다.
//
// ⚠️ 이 파일은 1단계에서 "만들어만 두는" 자료구조다.
//    최초 구현의 맵 생성기는 항상 빈 장식 목록을 만들며, 아직 아무도 이 타입을
//    사용하지 않는다(장식 배치는 에셋과 유형별 테마가 확정된 뒤의 작업).
//
// 근거: TechnicalDesignDocument.md 「MapDefinition 정규 데이터 계약」 오브젝트 배치,
//       GameSystemRules/GameSystemRules_RandomMap.md 규칙 15
//
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;

namespace Hexiege.Domain
{
    /// <summary>
    /// 장식물 한 개의 정의. 위치와 네 가지 정수 ID로만 구성된다.
    /// 값이 바뀌지 않는 읽기 전용 구조체이므로 만든 뒤에는 수정할 수 없다.
    /// </summary>
    public readonly struct DecorationDefinition : IEquatable<DecorationDefinition>
    {
        /// <summary>
        /// 장식물이 놓인 타일의 row-major 인덱스. (index = row * Width + col)
        /// 좌표를 두 개(col, row)로 들고 다니지 않고 인덱스 하나로 통일한 이유는
        /// canonical 정렬의 첫 번째 기준이 바로 이 인덱스이기 때문이다.
        /// </summary>
        public int TileIndex { get; }

        /// <summary> 장식물 종류 ID (예: 0=바위, 1=나무 …). 실제 프리팹 매핑은 Presentation 담당. </summary>
        public int TypeId { get; }

        /// <summary> 재질 변형 ID. 같은 종류라도 색/질감을 다르게 하기 위한 번호. </summary>
        public int MaterialVariantId { get; }

        /// <summary> 크기 단계 ID. 실제 scale 소수값이 아니라 "몇 번째 단계"인지의 번호. </summary>
        public int ScaleStepId { get; }

        /// <summary> 회전 단계 ID. 실제 각도(float)가 아니라 "몇 번째 단계"인지의 번호. </summary>
        public int RotationStepId { get; }

        /// <summary> 장식물 정의를 만든다. 모든 값은 고정폭 정수 ID다. </summary>
        /// <param name="tileIndex">놓인 타일의 row-major 인덱스</param>
        /// <param name="typeId">장식물 종류 ID</param>
        /// <param name="materialVariantId">재질 변형 ID</param>
        /// <param name="scaleStepId">크기 단계 ID</param>
        /// <param name="rotationStepId">회전 단계 ID</param>
        public DecorationDefinition(int tileIndex, int typeId, int materialVariantId,
            int scaleStepId, int rotationStepId)
        {
            TileIndex = tileIndex;
            TypeId = typeId;
            MaterialVariantId = materialVariantId;
            ScaleStepId = scaleStepId;
            RotationStepId = rotationStepId;
        }

        /// <summary>
        /// canonical 정렬 기준으로 두 장식물을 비교한다.
        /// 정렬 순서: TileIndex → TypeId → MaterialVariantId → ScaleStepId → RotationStepId.
        /// 이 순서는 직렬화 규약(TDD)에 고정되어 있으므로 임의로 바꾸면 해시가 달라진다.
        /// </summary>
        /// <param name="a">비교 대상 1</param>
        /// <param name="b">비교 대상 2</param>
        /// <returns>a가 앞이면 음수, 뒤면 양수, 같으면 0</returns>
        public static int CompareCanonical(DecorationDefinition a, DecorationDefinition b)
        {
            int c = a.TileIndex.CompareTo(b.TileIndex);
            if (c != 0) return c;
            c = a.TypeId.CompareTo(b.TypeId);
            if (c != 0) return c;
            c = a.MaterialVariantId.CompareTo(b.MaterialVariantId);
            if (c != 0) return c;
            c = a.ScaleStepId.CompareTo(b.ScaleStepId);
            if (c != 0) return c;
            return a.RotationStepId.CompareTo(b.RotationStepId);
        }

        /// <summary> 다섯 값이 모두 같은지 비교한다. </summary>
        /// <param name="other">비교 대상</param>
        /// <returns>모든 값이 같으면 true</returns>
        public bool Equals(DecorationDefinition other)
        {
            return TileIndex == other.TileIndex
                && TypeId == other.TypeId
                && MaterialVariantId == other.MaterialVariantId
                && ScaleStepId == other.ScaleStepId
                && RotationStepId == other.RotationStepId;
        }

        /// <summary> object 기반 동등 비교(박싱 경로). </summary>
        /// <param name="obj">비교 대상</param>
        /// <returns>같은 타입이며 모든 값이 같으면 true</returns>
        public override bool Equals(object obj)
        {
            return obj is DecorationDefinition other && Equals(other);
        }

        /// <summary> 다섯 값을 섞은 해시 코드. Dictionary/HashSet 키로 쓸 때 사용된다. </summary>
        /// <returns>해시 코드</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = TileIndex;
                h = (h * 397) ^ TypeId;
                h = (h * 397) ^ MaterialVariantId;
                h = (h * 397) ^ ScaleStepId;
                h = (h * 397) ^ RotationStepId;
                return h;
            }
        }
    }
}
