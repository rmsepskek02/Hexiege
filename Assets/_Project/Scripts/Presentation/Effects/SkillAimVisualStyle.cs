// ============================================================================
// SkillAimVisualStyle.cs
// 스킬 조준 표시(원형 조준원 · 공습 직사각형 · 타일 하이라이트)가 함께 쓰는 "색 · 머티리얼 · 애니메이션" 규칙.
// (2026-11-18 지원 스킬 2-L 공습 Task 단계 L-7 조준 UI 개선 — Plan 14절 UI-1 · UI-2b · UI-2c · UI-2d)
//
// 무엇을 하나(유니티 초급자용 설명):
//   · 상태 색(UI-2d): 발사 가능 = 빨강, 최소 드래그 미만 = 회색/흰색, X 위 = 회색으로 흐려짐, 공습 첫 화면 = 흐릿한 빨강(고스트).
//     세 표시물이 같은 색표를 써야 "지금 쏠 수 있는가"가 화면 전체에서 한 가지 색으로 읽힌다 → 한곳(이 파일)에 모았다.
//   · 색은 "모양(정점 색) × 상태 색(머티리얼 _Color)"으로 나온다:
//       메시의 정점 색에는 흰색 + 알파(어디가 진하고 어디가 투명한지 = 그라데이션 모양)만 넣고,
//       상태가 바뀌면 머티리얼의 _Color 하나만 바꾼다. 그래서 상태가 바뀌어도 메시를 다시 만들 필요가 없다(할당 없음).
//       (지면 데칼 셰이더 Hexiege/SkillAimOverlay 는 "텍스처 × 정점 색 × _Color"로 색을 낸다 — 텍스처가 없으면 흰색.)
//   · 색이 바뀔 때 뚝 끊기지 않게 매 프레임 목표 색으로 부드럽게 따라간다(TickColor — 프레임 독립 보간).
//   · 등장 애니메이션(UI-2d): 나타날 때 작게(StartScale) 시작해 짧은 시간(AppearDuration) 동안 커진다(AppearScale).
//
// 새 아트 에셋 · 새 셰이더 없이 만든다 — 머티리얼은 기존 지면 데칼 머티리얼을 "복사"해 쓴다(TowerRangeIndicator 선례).
// Presentation 레이어. 화면 전용(판정에 아무 영향 없음).
// ============================================================================

using UnityEngine;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 조준 표시 공통 색표 · 머티리얼 복사 · 등장 애니메이션 계산(상태 없음).
    /// </summary>
    public static class SkillAimVisualStyle
    {
        // ====================================================================
        // 상태 색(UI-2d) — 화면 전용 임시값(아트 단계에서 바꿀 수 있다)
        //   "주색" = 채움 · 테두리 · 바깥 링(머티리얼 1), "강조색" = 화살표 · 쉐브론(머티리얼 2), "타일색" = 타일 하이라이트 테두리.
        //   알파(a)는 그 표시 전체의 진하기 배수다 — 고스트 · X 위는 알파를 낮춰 "흐릿하게" 보인다.
        // ====================================================================

        // 발사 가능(빨강).
        private static readonly Color ArmedMain = new Color(0.95f, 0.16f, 0.13f, 1f);
        private static readonly Color ArmedAccent = new Color(1f, 0.88f, 0.82f, 1f);
        private static readonly Color ArmedTile = new Color(1f, 0.32f, 0.26f, 0.95f);

        // 최소 드래그 미만(회색/흰색 — 손을 떼도 발사되지 않는다).
        private static readonly Color BelowMain = new Color(0.86f, 0.86f, 0.88f, 0.9f);
        private static readonly Color BelowAccent = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color BelowTile = new Color(0.92f, 0.92f, 0.95f, 0.8f);

        // X 위 hover(회색으로 흐려짐 — 손을 떼면 취소).
        private static readonly Color CancelMain = new Color(0.5f, 0.5f, 0.52f, 0.4f);
        private static readonly Color CancelAccent = new Color(0.72f, 0.72f, 0.74f, 0.45f);
        private static readonly Color CancelTile = new Color(0.6f, 0.6f, 0.62f, 0.35f);

        // 공습 첫 화면(고스트 — 흐릿한 빨강, 아직 누르지 않음 — UI-1).
        private static readonly Color GhostMain = new Color(0.95f, 0.3f, 0.26f, 0.38f);
        private static readonly Color GhostAccent = new Color(1f, 0.9f, 0.86f, 0.42f);
        private static readonly Color GhostTile = new Color(1f, 0.45f, 0.4f, 0.35f);

        /// <summary> 상태 색이 목표로 따라가는 속도(클수록 빠름). 1 − exp(−속도 × 시간) 보간에 쓴다. </summary>
        public const float ColorLerpSpeed = 16f;

        /// <summary> 등장 애니메이션 길이(초). 너무 길면 조준이 굼떠 보인다. </summary>
        public const float AppearDuration = 0.18f;

        /// <summary> 등장 애니메이션 시작 크기(최종 크기 대비). </summary>
        public const float AppearStartScale = 0.55f;

        /// <summary> 지면 데칼 머티리얼 에셋 경로(Resources — TowerRangeIndicator · SkillLineAimReticle 과 같은 에셋, 복사해서 쓴다). </summary>
        private const string OverlayMaterialResourcePath = "Materials/NoBuildHatch";

        /// <summary>
        /// 상태의 주색(채움 · 테두리 · 바깥 링).
        /// </summary>
        /// <param name="state">조준 표시 상태.</param>
        /// <returns>주색.</returns>
        public static Color MainColor(SkillAimVisualState state)
        {
            switch (state)
            {
                case SkillAimVisualState.Ghost: return GhostMain;
                case SkillAimVisualState.BelowThreshold: return BelowMain;
                case SkillAimVisualState.CancelHover: return CancelMain;
                default: return ArmedMain;
            }
        }

        /// <summary>
        /// 상태의 강조색(화살표 · 쉐브론 · 중앙 점).
        /// </summary>
        /// <param name="state">조준 표시 상태.</param>
        /// <returns>강조색.</returns>
        public static Color AccentColor(SkillAimVisualState state)
        {
            switch (state)
            {
                case SkillAimVisualState.Ghost: return GhostAccent;
                case SkillAimVisualState.BelowThreshold: return BelowAccent;
                case SkillAimVisualState.CancelHover: return CancelAccent;
                default: return ArmedAccent;
            }
        }

        /// <summary>
        /// 상태의 타일 하이라이트 테두리 색.
        /// </summary>
        /// <param name="state">조준 표시 상태.</param>
        /// <returns>타일 테두리 색.</returns>
        public static Color TileColor(SkillAimVisualState state)
        {
            switch (state)
            {
                case SkillAimVisualState.Ghost: return GhostTile;
                case SkillAimVisualState.BelowThreshold: return BelowTile;
                case SkillAimVisualState.CancelHover: return CancelTile;
                default: return ArmedTile;
            }
        }

        /// <summary>
        /// 현재 색을 목표 색으로 한 프레임만큼 부드럽게 옮긴다(프레임 속도와 무관한 감쇠 — 취소 버튼 확대와 같은 방식).
        /// </summary>
        /// <param name="current">지금 색.</param>
        /// <param name="target">목표 색.</param>
        /// <param name="deltaTime">이번 프레임 시간(초).</param>
        /// <returns>새 색.</returns>
        public static Color TickColor(Color current, Color target, float deltaTime)
        {
            float t = 1f - Mathf.Exp(-ColorLerpSpeed * Mathf.Max(0f, deltaTime));
            return Color.Lerp(current, target, t);
        }

        /// <summary>
        /// 등장 애니메이션 크기 배수 — 시작(0초)에 AppearStartScale, AppearDuration 이 지나면 1.
        /// 끝으로 갈수록 천천히 멈추는 곡선(ease-out cubic)이라 "툭 튀어나와 자리 잡는" 느낌이 난다.
        /// </summary>
        /// <param name="elapsed">나타난 뒤 지난 시간(초).</param>
        /// <returns>크기 배수(AppearStartScale ~ 1).</returns>
        public static float AppearScale(float elapsed)
        {
            if (!(elapsed > 0f)) return AppearStartScale;
            if (elapsed >= AppearDuration) return 1f;
            float t = elapsed / AppearDuration;
            float inv = 1f - t;
            float eased = 1f - inv * inv * inv; // ease-out cubic
            return Mathf.LerpUnclamped(AppearStartScale, 1f, eased);
        }

        /// <summary>
        /// 지면 데칼 머티리얼 복사본을 만든다. 순서: ① 넘겨받은 머티리얼(조준원의 씬 배선 _overlayMaterial) 복사
        /// ② Resources 의 NoBuildHatch.mat 복사 ③ 셰이더 Hexiege/SkillAimOverlay 직접 찾기. 모두 실패하면 null.
        /// 복사본은 _Color 를 흰색으로, 텍스처를 비워(= 흰색) "정점 색 × _Color"만 남긴다. 다 쓰면 호출자가 Destroy 한다.
        /// </summary>
        /// <param name="preferred">우선 복사할 머티리얼(null 허용).</param>
        /// <param name="ownerName">진단 로그에 남길 호출자 이름.</param>
        /// <param name="materialName">복사본 이름(프로파일러 · 디버그용).</param>
        /// <returns>새 머티리얼 또는 null.</returns>
        public static Material CreateOverlayMaterial(Material preferred, string ownerName, string materialName)
        {
            Material material = null;
            if (preferred != null)
            {
                material = new Material(preferred);
            }
            else
            {
                Material source = Resources.Load<Material>(OverlayMaterialResourcePath);
                if (source != null)
                {
                    material = new Material(source);
                }
                else
                {
                    Shader shader = Shader.Find("Hexiege/SkillAimOverlay");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    if (shader != null) material = new Material(shader);
                    // [개발] Warn — 에셋 구성 문제(모든 기기 공통). 셰이더를 찾았으면 계속 보이므로 대체 경로가 있다.
                    GameLog.Dev.Warn("Skill", ownerName,
                                     "[SKILL-AIM] 조준 표시용 지면 데칼 머티리얼 에셋을 찾지 못했다 — 셰이더를 직접 찾아 대체한다",
                                     $"Diag=SkillAim, DiagRev=1, Event=AimVisualMaterialFallback, Owner={ownerName}, Path=Resources/{OverlayMaterialResourcePath}, ShaderFound={(material != null)}");
                }
            }
            if (material == null) return null;
            material.name = materialName;
            // 텍스처를 비우면 셰이더 기본값 "white" 가 쓰인다 → 색 = 정점 색 × _Color 만 남는다.
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            SetTint(material, Color.white);
            return material;
        }

        /// <summary>
        /// 머티리얼의 틴트(_Color / _BaseColor)를 바꾼다. 상태 색 · 맥동 알파를 넣는 유일한 자리.
        /// </summary>
        /// <param name="material">대상 머티리얼(null 이면 무시).</param>
        /// <param name="color">틴트 색.</param>
        public static void SetTint(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }
    }
}
