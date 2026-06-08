// ============================================================================
// EffectPreset.cs
// VFX(파티클 프리팹) + SFX(사운드 클립)를 하나로 묶는 ScriptableObject.
//
// ScriptableObject란?
//   Unity의 데이터 컨테이너 에셋. 씬에 붙이지 않고 .asset 파일로 저장되며,
//   Inspector에서 값을 편집할 수 있다. 코드 수정(재컴파일) 없이 이펙트 구성을
//   바꿀 수 있어 연출 작업에 유용하다.
//
// 이 에셋의 역할:
//   "공격 이펙트", "사망 이펙트" 같은 하나의 연출 단위를 표현한다.
//   파티클(VFX)과 효과음(SFX)을 한 세트로 묶어두면,
//   UnitEffectConfig / BuildingEffectConfig / UiEffectConfig에서
//   이 에셋 하나만 연결하면 VFX + SFX가 함께 재생된다.
//
//   VFX 또는 SFX 중 하나만 필요하면 나머지 필드를 비워두면 된다.
//   (EffectManager가 null인 필드는 건너뛴다.)
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
    /// VFX 프리팹 + SFX 클립을 하나로 묶는 이펙트 프리셋 에셋.
    /// 공격/사망/파괴/UI 등 각 연출 상황마다 하나씩 만들어 Config에 연결한다.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectPreset", menuName = "Hexiege/Effects/EffectPreset")]
    public class EffectPreset : ScriptableObject
    {
        [Tooltip("재생할 파티클 시스템 프리팹. 비워두면 VFX 없이 SFX만 재생됨.")]
        [SerializeField] private GameObject _vfxPrefab;

        [Tooltip("재생할 사운드 클립. 비워두면 SFX 없이 VFX만 재생됨.")]
        [SerializeField] private AudioClip _sfxClip;

        [Tooltip("사운드 볼륨 (0~1). 기본 1.0")]
        [Range(0f, 1f)]
        [SerializeField] private float _sfxVolume = 1f;

        /// <summary> 재생할 파티클 시스템 프리팹. 없으면 null. </summary>
        public GameObject VfxPrefab => _vfxPrefab;

        /// <summary> 재생할 사운드 클립. 없으면 null. </summary>
        public AudioClip SfxClip => _sfxClip;

        /// <summary> 사운드 볼륨 (0~1). </summary>
        public float SfxVolume => _sfxVolume;
    }
}
