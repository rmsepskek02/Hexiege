// ============================================================================
// EffectPreset.cs
// VFX 파티클 프리팹과 재생 시 위치·회전·크기 보정을 담는 ScriptableObject.
//
// ScriptableObject란?
//   Unity의 데이터 컨테이너 에셋. 씬에 붙이지 않고 .asset 파일로 저장되며,
//   Inspector에서 값을 편집할 수 있다. 코드 수정(재컴파일) 없이 이펙트 구성을
//   바꿀 수 있어 연출 작업에 유용하다.
//
// 이 에셋의 역할:
//   "공격 이펙트", "사망 이펙트" 같은 하나의 연출 단위를 표현한다.
//   VFX 프리팹과 재생 설정을 묶어두고,
//   UnitEffectConfig / BuildingEffectConfig / UiEffectConfig에서
//   상황에 맞는 이 에셋을 조회하여 EffectManager가 VFX를 재생한다.
//
//   SFX는 AudioManager가 별도로 설정·재생한다. 둘을 함께 재생할 때는
//   호출부에서 EffectManager와 AudioManager를 연달아 호출한다(사운드 규칙 15).
//
// 생성 방법:
//   Project 창에서 우클릭 → Create → Hexiege → Effects → EffectPreset
//
// Presentation 레이어 — Unity 의존 허용 (ScriptableObject).
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// VFX 프리팹을 담는 이펙트 프리셋 에셋. (SFX는 AudioManager/SoundConfig로 분리됨)
    /// 공격/사망/파괴/UI 등 각 연출 상황마다 하나씩 만들어 Config에 연결한다.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectPreset", menuName = "Hexiege/Effects/EffectPreset")]
    public class EffectPreset : ScriptableObject
    {
        [Tooltip("재생할 파티클 시스템 프리팹. 비워두면 아무 VFX도 재생되지 않음.")]
        [SerializeField] private GameObject _vfxPrefab;

        [Header("VFX Transform Tuning")]
        [Tooltip("Spawn point 기준 로컬 위치 보정값입니다. 공격 방향 회전이 적용된 뒤 월드 위치에 더해집니다.")]
        [SerializeField] private Vector3 _localPositionOffset = Vector3.zero;

        [Tooltip("Spawn point 회전에 추가할 Euler 회전 보정값(도)입니다.")]
        [SerializeField] private Vector3 _eulerRotationOffset = Vector3.zero;

        [Tooltip("VFX 프리팹 원본 로컬 스케일에 곱할 배율입니다. (1, 1, 1)은 원본 크기를 유지합니다.")]
        [SerializeField] private Vector3 _scaleMultiplier = Vector3.one;

        [Header("Optional VFX Forward Travel")]
        [Tooltip("연출 전용 전진 거리입니다. 0이면 이동하지 않습니다. 서버 판정에는 영향을 주지 않습니다.")]
        [Min(0f)]
        [SerializeField] private float _forwardTravelDistance;

        [Tooltip("전진 거리에 도달하는 시간(초)입니다. 거리 또는 시간이 0이면 이동하지 않습니다.")]
        [Min(0f)]
        [SerializeField] private float _forwardTravelDuration;

        // [2026-10-13 R2-1] 시전자(공격한 유닛)에게 "귀속"되는 이펙트인지 표시하는 체크박스.
        //   예: FoxMagician·InfernoSpirit의 "기 모으기(charge)" 이펙트는 공격 동작 앞부분에 시작해
        //   1~2초 동안 시전자 앞에서 재생된다. 그 사이에 시전자가 죽으면 이펙트만 혼자 남아 어색하다.
        //   이 값을 켠 프리셋은 시전자가 죽거나(사망) 화면에서 사라지면(제거·디스폰) 즉시 멈추고 풀로 돌아간다.
        //   [P4 — 2026-10-19 차지 유닛 발사 시점] 차지 도중 공격이 취소돼도(Stop·타겟 교체 등) 즉시 멈춘다(UnitView.StopOwnedChargeEffects).
        //   기본값 false — 트레이서·착탄·사망 이펙트처럼 "이미 시전자에게서 떨어져 나간" 이펙트는 그대로 끝까지 재생된다.
        //   화면 표현 전용이다. 피해·타겟·서버 판정과 무관하다.
        [Header("Caster Ownership")]
        [Tooltip("켜면 이 이펙트는 공격한 유닛(시전자)에게 귀속된다. 시전자가 죽거나 제거되면 즉시 멈추고 사라진다. "
            + "기 모으기(charge)처럼 시전자 몸 앞에서 재생되는 공격 이펙트에만 켠다. 트레이서·착탄·사망 이펙트에는 켜지 않는다.")]
        [SerializeField] private bool _stopWhenCasterDies;

        /// <summary> 재생할 파티클 시스템 프리팹. 없으면 null. </summary>
        public GameObject VfxPrefab => _vfxPrefab;

        /// <summary>공격 방향을 기준으로 적용할 VFX 로컬 위치 보정값.</summary>
        public Vector3 LocalPositionOffset => _localPositionOffset;

        /// <summary>스폰 회전에 추가할 Euler 회전 보정값(도).</summary>
        public Vector3 EulerRotationOffset => _eulerRotationOffset;

        /// <summary>
        /// 프리팹 원본 로컬 스케일에 곱할 재생별 배율.
        /// 새 필드가 없는 구버전 .asset이 (0,0,0)으로 역직렬화되더라도 기존 VFX가 사라지지 않도록
        /// 해당 값은 기본 배율 (1,1,1)로 취급한다.
        /// </summary>
        public Vector3 ScaleMultiplier => _scaleMultiplier == Vector3.zero ? Vector3.one : _scaleMultiplier;

        /// <summary>서버 판정과 무관한 VFX 전진 거리.</summary>
        public float ForwardTravelDistance => _forwardTravelDistance;

        /// <summary>VFX가 전진 거리에 도달하는 시간(초).</summary>
        public float ForwardTravelDuration => _forwardTravelDuration;

        /// <summary>
        /// 시전자 귀속 이펙트인지. true면 EffectManager가 시전자 사망/제거 시 이 이펙트를 즉시 멈추고 풀에 반환한다.
        /// 현재 공격 이펙트(UnitEffectConfig.attackPreset) 재생 경로에서만 읽는다.
        /// </summary>
        public bool StopWhenCasterDies => _stopWhenCasterDies;

        // SOUND_SYSTEM_REFACTOR: SFX는 AudioManager + SoundConfig로 이전 완료.
        // 검증(실기 테스트) 통과 전까지 기존 SFX 필드/프로퍼티를 주석으로 비활성화한다.
        // 통과 후 아래 주석 블록을 최종 삭제할 예정. (WORKFLOW.md "기존 로직 제거" 규칙)
        //   ⚠️ 기존 EffectPreset .asset에 SfxClip이 연결돼 있다면 데이터 소실 위험 →
        //      SoundConfig로 수동 이전 후 삭제할 것.
        // [Tooltip("재생할 사운드 클립. 비워두면 SFX 없이 VFX만 재생됨.")]
        // [SerializeField] private AudioClip _sfxClip;
        //
        // [Tooltip("사운드 볼륨 (0~1). 기본 1.0")]
        // [Range(0f, 1f)]
        // [SerializeField] private float _sfxVolume = 1f;
        //
        // /// <summary> 재생할 사운드 클립. 없으면 null. </summary>
        // public AudioClip SfxClip => _sfxClip;
        //
        // /// <summary> 사운드 볼륨 (0~1). </summary>
        // public float SfxVolume => _sfxVolume;
    }
}
