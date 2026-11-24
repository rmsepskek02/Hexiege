// ============================================================================
// SkillAimVisualMath.cs
// 스킬 조준 "표시"에 쓰는 순수 계산 모음 — 맞는 타일 고르기 · 폭격 눈금 위치 · 상태 색 판정.
// (2026-11-18 지원 스킬 2-L 공습 Task 단계 L-7 조준 UI 개선 — _Tasks/2026-11-17/09_12_support-skill-air-strike, Plan 14절 UI-2a · UI-2c · UI-2d)
//
// 왜 따로 떼어 냈나(유니티 초급자용 설명):
//   조준 화면에 "이 타일들이 맞는다" · "여기에 폭탄이 떨어진다" · "지금 쏠 수 있다(빨강)/없다(회색)"를 보여 주려면
//   숫자 계산이 필요하다. 이 계산을 MonoBehaviour(화면 오브젝트) 안에 섞어 두면 에디터 메뉴 검사(회귀 스위트)로
//   확인할 수 없다. 그래서 입력 장치 · 씬 · 렌더러와 무관한 "순수 함수"로 이 파일에 모았다(SkillLineAimMath 선례).
//
// 🔴 가장 중요한 약속 — 「보이는 곳 = 맞는 곳」(Plan 14-1 둘째 줄):
//   하이라이트 타일은 서버가 피해를 줄 대상을 고를 때와 "똑같은 기준"으로 고른다.
//     · 원형(포격 · EMP · 서리 감옥 · 수액 늪): 대상이 선 "타일 중심"과 조준점의 XZ 거리² ≤ 반경²
//         (서버 UnitCombatUseCase.CollectEnemyUnitsInRadiusDomain 과 같은 식 — Flatten 후 sqrMagnitude <= radius*radius).
//     · 직선(공습): 타일 중심이 직사각형 안(LineStrikeGeometry.IsInside) + 맵 안 유효 길이 안(along ≤ 유효 길이 + 여유)
//         + 그 점이 속한 폭격 구간이 건너뛰지 않는 폭격(LineStrikeGeometry.IsStrikeInRange)
//         (서버 UnitCombatUseCase.IsInLineStrikeSegment · SkillActivationUseCase 의 폭격 건너뛰기와 같은 함수).
//   그래서 이 파일은 판정 함수를 새로 만들지 않고 Domain 의 LineStrikeGeometry 를 그대로 부른다(Plan R-14).
//
// 이 파일은 표시 전용이다 — 서버 판정 · 발사 판정(L-3)에는 쓰지 않는다(바뀌는 것이 없다).
// Presentation 레이어. UnityEngine.Vector3 만 쓴다(입력 장치 · 씬 의존 없음).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 조준 표시의 상태(색을 고르는 기준). 진단 로그 [SKILL-AIM] AimVisualState 의 From · To 값과 같은 이름이다.
    /// </summary>
    public enum SkillAimVisualState
    {
        /// <summary> 공습 조준에 막 들어와 아직 화면을 누르지 않은 상태 — 흐릿한(고스트) 직사각형(UI-1). </summary>
        Ghost = 0,

        /// <summary> 지금 손을 떼면 발사되는 상태 — 빨강(UI-2d). 원형 조준은 X 위가 아니면 언제나 이 상태다. </summary>
        Armed = 1,

        /// <summary> 공습에서 최소 드래그(약 0.5칸)보다 덜 끈 상태 — 손을 떼도 발사되지 않는다 — 회색/흰색(UI-2d · U-5). </summary>
        BelowThreshold = 2,

        /// <summary> 손가락이 하단 취소(X) 버튼 위 — 떼면 취소된다 — 회색으로 흐려짐(UI-2d · 규칙 20-1). </summary>
        CancelHover = 3,
    }

    /// <summary>
    /// 조준 표시용 순수 계산(상태 없음). 하이라이트 타일 선정 · 폭격 눈금 위치 · 상태 판정.
    /// </summary>
    public static class SkillAimVisualMath
    {
        /// <summary>
        /// 조준 표시 상태를 정한다(UI-2d). 발사 판정 자체(SkillAimController.ResolveLineRelease)와 같은 조건을 "미리" 보여 줄 뿐이다.
        ///   우선순위: X 위(CancelHover) → 공습 고스트(Ghost) → 공습 최소 드래그 미만(BelowThreshold) → 그 밖(Armed).
        ///   X 위를 가장 먼저 보는 이유: 손을 떼면 "취소"가 가장 먼저 판정되기 때문이다(ResolveLineRelease · ResolveRelease 순서와 같다).
        /// </summary>
        /// <param name="lineMode">공습(직선) 조준인가. false 면 원형 조준.</param>
        /// <param name="lineGhost">공습에서 아직 첫 터치 전인가(고스트 표시 중).</param>
        /// <param name="overCancel">추적 손가락이 지금 X 버튼 위인가.</param>
        /// <param name="dragDistance">공습 시작점 → 손가락 거리(월드). 원형이면 무시.</param>
        /// <param name="threshold">공습 최소 드래그 임계값(월드). 원형이면 무시.</param>
        /// <returns>표시 상태.</returns>
        public static SkillAimVisualState ResolveVisualState(bool lineMode, bool lineGhost, bool overCancel,
                                                             float dragDistance, float threshold)
        {
            if (overCancel) return SkillAimVisualState.CancelHover;
            if (!lineMode) return SkillAimVisualState.Armed;
            if (lineGhost) return SkillAimVisualState.Ghost;
            // 발사 판정과 "같은 함수"(경계값 = 임계값이면 발사) — 화면 색과 실제 발사가 어긋나지 않게.
            return SkillLineAimMath.IsDragEnough(dragDistance, threshold)
                ? SkillAimVisualState.Armed
                : SkillAimVisualState.BelowThreshold;
        }

        /// <summary>
        /// 원형 조준 — 반경 안에 중심이 드는 타일의 번호(목록 안 위치)를 모은다(UI-2a).
        /// 서버와 같은 식: (타일 중심 − 조준점)의 XZ 거리² ≤ 반경². 경계 위(같음)는 맞는다.
        /// </summary>
        /// <param name="tileCentersDomain">모든 타일 중심(도메인 월드, HexMetrics.HexToWorld 결과).</param>
        /// <param name="aimDomain">조준점(도메인 월드 — 서버로 보내는 값과 같은 것).</param>
        /// <param name="radius">반경(스킬 데이터 Radius).</param>
        /// <param name="result">결과 번호를 담을 목록(먼저 비운다 — 호출자가 재사용, 할당 없음).</param>
        /// <returns>모은 타일 수.</returns>
        public static int CollectCircleTiles(IReadOnlyList<Vector3> tileCentersDomain, Vector3 aimDomain, float radius,
                                             List<int> result)
        {
            result.Clear();
            if (tileCentersDomain == null || !(radius > 0f)) return 0; // 서버도 radius <= 0 이면 아무도 때리지 않는다
            // 서버 ApplySkillInstantAreaDamage 와 같은 순서 — 중심을 Y=0 으로 눕힌 뒤 거리² 비교.
            Vector3 center = new Vector3(aimDomain.x, 0f, aimDomain.z);
            float radiusSqr = radius * radius;
            for (int i = 0; i < tileCentersDomain.Count; i++)
            {
                Vector3 c = tileCentersDomain[i];
                Vector3 rel = new Vector3(c.x, 0f, c.z) - center;
                if (rel.sqrMagnitude <= radiusSqr) result.Add(i);
            }
            return result.Count;
        }

        /// <summary>
        /// 공습 — 한 타일 중심이 이번 공습의 어느 폭격에든 맞는 자리인지(UI-2a). 서버 판정 3단계를 그대로 따른다:
        ///   ① 직사각형 안(LineStrikeGeometry.IsInside — 경계 포함) ② 맵 안 유효 길이 안(along ≤ 유효 길이 + BoundaryEpsilon)
        ///   ③ 그 점의 구간(SegmentIndexOf)의 폭격이 맵 밖이라 통째로 건너뛰는 폭격이 아님(IsStrikeInRange).
        /// </summary>
        /// <param name="startDomain">시작점(도메인 월드).</param>
        /// <param name="directionDomain">정규화된 방향(도메인).</param>
        /// <param name="length">원래 길이 L(구간은 이 길이로 나눈다 — P-13).</param>
        /// <param name="halfWidth">반폭(폭 ÷ 2).</param>
        /// <param name="effectiveLength">맵 안 유효 길이(LineStrikeWorld.ComputeEffectiveLength 결과).</param>
        /// <param name="strikeCount">폭격 횟수(스킬 데이터 LineStrikeCount).</param>
        /// <param name="tileCenterDomain">판정할 타일 중심(도메인 월드).</param>
        /// <returns>맞는 자리면 true.</returns>
        public static bool IsLineTileHit(Vector3 startDomain, Vector3 directionDomain, float length, float halfWidth,
                                         float effectiveLength, int strikeCount, Vector3 tileCenterDomain)
        {
            // 폭격 횟수 0 = 폭격 없음(SkillData 가 0 ~ MaxLineStrikeCount 로 자른다) → 아무 타일도 맞지 않는다.
            if (strikeCount < 1) return false;
            if (!LineStrikeGeometry.IsInside(startDomain.x, startDomain.z, directionDomain.x, directionDomain.z,
                    length, halfWidth, tileCenterDomain.x, tileCenterDomain.z, out float along))
                return false;
            // 서버 IsInLineStrikeSegment: 구간은 맞아도 유효 길이 밖이면 맞지 않는다(맵 밖 잘라내기 — U-14).
            if (along > effectiveLength + LineStrikeGeometry.BoundaryEpsilon) return false;
            // 서버 폭격 루프: 구간 ∩ [0, 유효 길이] 가 비는 폭격은 통째로 건너뛴다(그 구간의 대상은 아무도 맞지 않는다).
            int segment = LineStrikeGeometry.SegmentIndexOf(along, length, strikeCount);
            return LineStrikeGeometry.IsStrikeInRange(segment, strikeCount, length, effectiveLength);
        }

        /// <summary>
        /// 공습 — 맞는 자리의 타일 번호를 모은다(UI-2a). 판정은 IsLineTileHit 하나로만 한다.
        /// </summary>
        /// <param name="tileCentersDomain">모든 타일 중심(도메인 월드).</param>
        /// <param name="startDomain">시작점(도메인).</param>
        /// <param name="directionDomain">정규화된 방향(도메인).</param>
        /// <param name="length">원래 길이 L.</param>
        /// <param name="halfWidth">반폭.</param>
        /// <param name="effectiveLength">맵 안 유효 길이.</param>
        /// <param name="strikeCount">폭격 횟수.</param>
        /// <param name="result">결과 번호 목록(먼저 비운다 — 재사용).</param>
        /// <returns>모은 타일 수.</returns>
        public static int CollectLineTiles(IReadOnlyList<Vector3> tileCentersDomain, Vector3 startDomain, Vector3 directionDomain,
                                           float length, float halfWidth, float effectiveLength, int strikeCount, List<int> result)
        {
            result.Clear();
            if (tileCentersDomain == null) return 0;
            for (int i = 0; i < tileCentersDomain.Count; i++)
            {
                if (IsLineTileHit(startDomain, directionDomain, length, halfWidth, effectiveLength, strikeCount, tileCentersDomain[i]))
                    result.Add(i);
            }
            return result.Count;
        }

        /// <summary>
        /// 공습 — k번째 폭격 눈금의 진행 거리(시작점에서 방향으로 몇 m 인가, UI-2c).
        /// 화면 폭격 VFX(SkillActivationVfxPresenter.ScheduleStrikeVfx)가 폭격을 재생하는 자리와 같은 식이다:
        ///   맵 안 부분 = [구간 시작, min(구간 끝, 유효 길이)] 의 가운데. 맵 밖이라 서버가 건너뛰는 폭격이면 false(눈금을 그리지 않는다).
        /// </summary>
        /// <param name="index">폭격 번호(0부터).</param>
        /// <param name="count">폭격 횟수.</param>
        /// <param name="length">원래 길이 L.</param>
        /// <param name="effectiveLength">맵 안 유효 길이.</param>
        /// <param name="along">눈금 진행 거리(false 면 0).</param>
        /// <returns>눈금을 그려야 하면 true.</returns>
        public static bool TryGetStrikeTickAlong(int index, int count, float length, float effectiveLength, out float along)
        {
            along = 0f;
            if (count < 1 || index < 0 || index >= count) return false;
            if (!(length > 0f)) return false;
            if (!LineStrikeGeometry.IsStrikeInRange(index, count, length, effectiveLength)) return false;
            LineStrikeGeometry.GetSegmentBounds(index, count, length, out float segFrom, out float segTo);
            float judgeTo = segTo < effectiveLength ? segTo : effectiveLength;
            if (judgeTo < segFrom) judgeTo = segFrom; // 첫 폭격(k = 0)은 유효 길이 0이어도 판정한다 — 그때는 시작점(VFX 와 같음)
            along = (segFrom + judgeTo) * 0.5f;
            return true;
        }

        /// <summary>
        /// 두 번호 목록이 같은지(순서까지). 하이라이트 타일 집합이 바뀌지 않았으면 메시를 다시 만들지 않으려고 쓴다.
        /// </summary>
        /// <param name="a">목록 A.</param>
        /// <param name="b">목록 B.</param>
        /// <returns>같으면 true.</returns>
        public static bool SameIndices(List<int> a, List<int> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
