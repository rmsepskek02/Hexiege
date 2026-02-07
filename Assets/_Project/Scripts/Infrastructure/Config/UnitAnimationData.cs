// ============================================================================
// UnitAnimationData.cs
// 유닛의 방향별/상태별 스프라이트 배열을 저장하는 ScriptableObject.
//
// 구조:
//   PointyTop: 3방향(NE, E, SE) × 3상태(Idle, Walk, Attack) = 9개 Sprite[] 배열.
//   FlatTop:   5방향(NE, E, SE, N, S) × 3상태 = 15개 배열 (N/S 6개 추가).
//   각 배열에 해당 방향/상태의 프레임 스프라이트를 Inspector에서 드래그 앤 드롭.
//
// 권총병 스프라이트 현황 (AssetProductionGuide.md 기준):
//   Idle:   방향당 1프레임 (NE, E, SE 각 1장)
//   Walk:   E방향 2프레임, NE/SE 각 1프레임
//   Attack: 방향당 2프레임
//
// NW/W/SW 방향은 flipX=true로 처리하므로 별도 배열 불필요.
// FacingDirection.FromHexDirection()이 ArtDirection(NE/E/SE) + flipX를 반환하면,
// FrameAnimator가 이 ScriptableObject에서 해당 배열을 가져와 재생.
//
// 생성 방법 (Unity 에디터):
//   Assets 폴더 우클릭 → Create → Hexiege → UnitAnimationData
//   → Resources/Config/ 에 저장
//   → Inspector에서 각 배열에 스프라이트 드래그 앤 드롭
//
// 사용 흐름:
//   1. FrameAnimator가 현재 상태(idle/walk/attack) + 방향(NE/E/SE) 결정
//   2. GetSprites(state, artDir)로 해당 Sprite[] 배열 가져옴
//   3. FPS에 맞춰 프레임 인덱스 순환 → SpriteRenderer.sprite에 적용
//
// Infrastructure 레이어 — Unity 의존 (ScriptableObject, Sprite).
// ============================================================================

using System;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 유닛 애니메이션 상태. FrameAnimator에서 현재 상태 지정에 사용.
    /// </summary>
    public enum UnitAnimState
    {
        Idle = 0,     // 대기 (서있는 상태)
        Walk = 1,     // 이동 중
        Attack = 2    // 공격 중 (프로토타입에서는 사용하지 않지만 스프라이트는 있음)
    }

    [CreateAssetMenu(fileName = "UnitAnimationData", menuName = "Hexiege/UnitAnimationData")]
    public class UnitAnimationData : ScriptableObject
    {
        // ====================================================================
        // 스프라이트 배열
        // 각 배열은 Inspector에서 스프라이트를 드래그 앤 드롭하여 설정.
        //
        // 배열 이름 규칙: {상태}{방향}
        // 예: IdleNE = Idle 상태의 NE 방향 스프라이트 배열
        //
        // 배열 길이 = 해당 상태/방향의 프레임 수
        // 예: IdleNE는 1프레임 → Sprite[1]
        //     WalkE는 2프레임 → Sprite[2]
        //     AttackSE는 2프레임 → Sprite[2]
        // ====================================================================

        [Header("Idle (대기) - 방향당 1프레임")]
        [Tooltip("Idle NE 방향: pistoleer_idle_ne_01")]
        public Sprite[] IdleNE;

        [Tooltip("Idle E 방향: pistoleer_idle_e_01")]
        public Sprite[] IdleE;

        [Tooltip("Idle SE 방향: pistoleer_idle_se_01")]
        public Sprite[] IdleSE;

        [Header("Walk (이동) - E방향 2프레임, NE/SE 1프레임")]
        [Tooltip("Walk NE 방향: pistoleer_walk_ne_01")]
        public Sprite[] WalkNE;

        [Tooltip("Walk E 방향: pistoleer_walk_e_01, _02")]
        public Sprite[] WalkE;

        [Tooltip("Walk SE 방향: pistoleer_walk_se_01")]
        public Sprite[] WalkSE;

        [Header("Attack (공격) - 방향당 2프레임")]
        [Tooltip("Attack NE 방향: pistoleer_attack_ne_01, _02")]
        public Sprite[] AttackNE;

        [Tooltip("Attack E 방향: pistoleer_attack_e_01, _02")]
        public Sprite[] AttackE;

        [Tooltip("Attack SE 방향: pistoleer_attack_se_01, _02")]
        public Sprite[] AttackSE;

        // ====================================================================
        // N/S 방향 스프라이트 (flat-top 전용)
        // flat-top에서는 순수 상하(N/S) 방향이 존재하므로 별도 스프라이트 필요.
        // pointy-top에서는 사용하지 않으며, 비어있어도 무방.
        // ====================================================================

        [Header("Idle - N/S 방향 (flat-top 전용)")]
        [Tooltip("Idle N 방향 (위를 바라봄)")]
        public Sprite[] IdleN;

        [Tooltip("Idle S 방향 (아래를 바라봄)")]
        public Sprite[] IdleS;

        [Header("Walk - N/S 방향 (flat-top 전용)")]
        [Tooltip("Walk N 방향")]
        public Sprite[] WalkN;

        [Tooltip("Walk S 방향")]
        public Sprite[] WalkS;

        [Header("Attack - N/S 방향 (flat-top 전용)")]
        [Tooltip("Attack N 방향")]
        public Sprite[] AttackN;

        [Tooltip("Attack S 방향")]
        public Sprite[] AttackS;

        // ====================================================================
        // 배열을 2D로 접근하기 위한 조회 메서드
        // ====================================================================

        /// <summary>
        /// 애니메이션 상태 + 아트 방향으로 해당 스프라이트 배열을 반환.
        ///
        /// 조합 매트릭스:
        ///          NE       E        SE       N        S
        /// Idle   [ IdleNE,  IdleE,   IdleSE,  IdleN,   IdleS   ]
        /// Walk   [ WalkNE,  WalkE,   WalkSE,  WalkN,   WalkS   ]
        /// Attack [ AttackNE, AttackE, AttackSE, AttackN, AttackS ]
        ///
        /// N/S는 flat-top 전용. pointy-top에서는 사용되지 않음.
        /// 반환된 배열이 null이거나 길이가 0이면 빈 상태 (스프라이트 미설정).
        /// </summary>
        /// <param name="state">현재 애니메이션 상태</param>
        /// <param name="artDir">아트 방향 (NE/E/SE/N/S)</param>
        /// <returns>해당 조합의 스프라이트 배열</returns>
        public Sprite[] GetSprites(UnitAnimState state, Domain.ArtDirection artDir)
        {
            switch (state)
            {
                case UnitAnimState.Idle:
                    switch (artDir)
                    {
                        case Domain.ArtDirection.NE: return IdleNE;
                        case Domain.ArtDirection.E:  return IdleE;
                        case Domain.ArtDirection.SE: return IdleSE;
                        case Domain.ArtDirection.N:  return IdleN;
                        case Domain.ArtDirection.S:  return IdleS;
                    }
                    break;
                case UnitAnimState.Walk:
                    switch (artDir)
                    {
                        case Domain.ArtDirection.NE: return WalkNE;
                        case Domain.ArtDirection.E:  return WalkE;
                        case Domain.ArtDirection.SE: return WalkSE;
                        case Domain.ArtDirection.N:  return WalkN;
                        case Domain.ArtDirection.S:  return WalkS;
                    }
                    break;
                case UnitAnimState.Attack:
                    switch (artDir)
                    {
                        case Domain.ArtDirection.NE: return AttackNE;
                        case Domain.ArtDirection.E:  return AttackE;
                        case Domain.ArtDirection.SE: return AttackSE;
                        case Domain.ArtDirection.N:  return AttackN;
                        case Domain.ArtDirection.S:  return AttackS;
                    }
                    break;
            }
            return Array.Empty<Sprite>();
        }
    }
}
