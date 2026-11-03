// ============================================================================
// TowerAttackProfile.cs
// 방어 타워(AutoTower) 한 종족의 「공격 표현 프로필」 한 줄.
//
// 왜 필요한가 (유니티 초급자용):
//   세 종족의 방어 타워(인간 대포 타워 · 정령 룬 첨탑 · 초월 덩굴 타워)는 프로그램 안에서 모두 같은
//   건물 종류(BuildingType.AutoTower)다. 예전에는 공격할 때 화면에 무엇을 보여 줄지를
//   "인간이면 이렇게, 정령이면 저렇게, 초월이면 아무것도 안 함"처럼 여러 파일에서 종족 이름으로 직접 갈랐다.
//   이제는 타워마다 이 프로필 한 줄에
//     ① 어떤 방식으로 공격하는가(투사체 / 번개 / 지면 분출)
//     ② 어디서 재생하는가(타워 위치 / 공격 순간 대상 발밑 / 서버 포구)
//     ③ 어떤 이펙트 프리셋을 쓰는가(발사 · 투사체 · 착탄)
//   를 적어 두고, 화면 연출 코드(EffectManager · HitPresentationQueue)는 종족이 아니라 「공격 방식」을 보고 움직인다.
//   그러면 새 타워 연출(예: 덩굴 타워의 지면 분출)을 붙일 때 코드를 고치지 않고 이 프로필 한 줄만 채우면 된다.
//
// 어디에 저장되나:
//   BuildingEffectConfig.asset 의 「_towerAttackProfiles」 목록(Assets/_Project/Resources/Config/).
//
// 이 프로필이 정하지 않는 것:
//   피해가 언제·얼마나 들어가는지(서버 판정), 인간 타워가 서버 투사체를 쏘는지 여부(ServerProjectileDeliveryPolicy),
//   공격음(SoundConfig의 「타워 공격 SFX」 칸 — 사운드 규칙 1에 따라 소리는 이펙트 설정과 따로 둔다).
//
// 계획 원본: Assets/_Project/Docs/_Tasks/2026-11-02/15_48_tower-attack-presentation-restructure/Plan.md 3-1절
// 근거 규칙: GameSystemRules_Buildings.md 방어 타워 규칙 12(타워 발사 연출)
//
// Presentation 레이어 — Unity 의존 허용(EffectPreset = ScriptableObject 참조).
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 방어 타워의 공격 방식. 화면 연출 코드는 종족 대신 이 값으로 재생 방법을 고른다.
    /// 숫자는 에셋(YAML)에 그대로 저장되므로 기존 값의 숫자를 바꾸지 않는다.
    /// </summary>
    public enum TowerAttackKind
    {
        /// <summary>
        /// 비어 있음(0) — 아직 공격 방식을 정하지 않은 행. 새 행을 만들면 Unity가 0을 넣으므로,
        /// 실수로 「투사체」가 기본값이 되지 않도록 0을 「비어 있음」으로 둔다(Plan 13절 A-2).
        /// </summary>
        Unset = 0,

        /// <summary>
        /// 투사체(1) — 포탄처럼 날아가는 공격(인간 대포 타워).
        /// 실제 비행·착탄은 서버 투사체 경로(ProjectilePresentationPresenter)가 그린다.
        /// </summary>
        Projectile = 1,

        /// <summary>번개(2) — 타워에서 대상까지 이어지는 줄기. 원래 맞은 유닛을 끝점으로 따라간다(정령 룬 첨탑).</summary>
        Beam = 2,

        /// <summary>
        /// 지면 분출(3) — 공격 순간 대상 발밑에서 한 번 솟는 이펙트. 대상을 따라가지 않는다
        /// (덩굴 타워용 자리 — 2026-11-03 기준 데이터 없음).
        /// </summary>
        GroundBurst = 3,
    }

    /// <summary>
    /// 타워 발사 이펙트를 「어디서」 재생할지의 기준점.
    /// </summary>
    public enum TowerAttackAnchor
    {
        /// <summary>타워 위치(0) — 서버 공격 스냅샷의 Source.</summary>
        Source = 0,

        /// <summary>공격 순간 대상 발밑(1) — 서버 공격 스냅샷의 Impact. 한 번 놓은 뒤 대상을 다시 읽지 않는다.</summary>
        Impact = 1,

        /// <summary>
        /// 서버 포구(2) — 서버 투사체 발사 기록의 출발점. 스냅샷 경로에는 포구 값이 없으므로
        /// 스냅샷 경로에서는 타워 위치(Source)로 대신한다(Plan 3-3 (가) Projectile 행).
        /// </summary>
        Muzzle = 2,
    }

    /// <summary>
    /// 방어 타워 한 종족의 공격 표현 프로필(BuildingEffectConfig의 한 줄).
    /// 「비어 있음」 = 세 프리셋이 모두 비어 있는 행(공격 방식 값과 무관) — 이 경우 공격 표현을 재생하지 않는다.
    /// </summary>
    [System.Serializable]
    public struct TowerAttackProfile
    {
        [Tooltip("이 줄이 적용되는 타워 종족. 같은 종족 줄이 둘 이상이면 위의 첫 줄만 쓴다")]
        public RaceId race;

        [Tooltip("공격 방식. Unset=비어 있음 · Projectile=투사체(서버 투사체 경로) · Beam=번개(대상 추적) · GroundBurst=지면 분출(대상 발밑 1회)")]
        public TowerAttackKind kind;

        [Tooltip("발사 이펙트 재생 기준점. Source=타워 위치 · Impact=공격 순간 대상 발밑 · Muzzle=서버 포구(스냅샷 경로에서는 타워 위치)")]
        public TowerAttackAnchor anchor;

        [Tooltip("발사(공격 순간) 이펙트 프리셋. 투사체=포구 이펙트 · 번개=번개 줄기 · 지면 분출=솟는 이펙트")]
        public EffectPreset launchPreset;

        [Tooltip("서버 투사체 비행 중 보이는 트레이서 프리셋(투사체 방식만 사용). 비우면 투사체 없이 착탄만 보인다")]
        public EffectPreset tracerPreset;

        [Tooltip("서버 투사체 착탄 지점에서 1회 재생할 착탄 프리셋(투사체 방식만 사용). 비우면 착탄 이펙트 없음")]
        public EffectPreset impactPreset;

        /// <summary>
        /// 세 프리셋이 모두 비어 있으면 true(= 이 종족 타워는 공격 표현이 없다).
        /// EffectPreset은 Unity 오브젝트라 「== null」이 삭제된 에셋 참조(Missing)까지 비어 있음으로 본다.
        /// </summary>
        public bool IsEmpty => launchPreset == null && tracerPreset == null && impactPreset == null;
    }
}
