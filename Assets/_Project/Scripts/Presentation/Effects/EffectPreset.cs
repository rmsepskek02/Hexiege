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

        // [2026-10-26 상태 이펙트 폭 맞춤] 화상·독·힐처럼 "유닛 몸에 붙는 상태 이펙트" 전용 옵션.
        //   문제: 상태 이펙트 크기는 유닛 "키"로만 맞춘다(D-3 균등 배율). 그래서 키는 비슷한데 몸이 옆으로 넓은
        //         곰전사·탱크·대포수레는 넓은 몸통 한가운데에 좁은 불꽃 한 장만 보였다.
        //   해결: 이 옵션을 켜면 불꽃 "한 장의 크기"는 그대로 두고, 재생 시작 때
        //         ① 불꽃이 피어오르는 범위를 몸통 폭(UnitEffectConfig 크기 표의 radius)만큼 원판으로 넓히고
        //         ② 몸통 폭 ÷ 불꽃 폭(반올림)만큼 불꽃 개수(초당 방출 수·최대 입자 수)를 늘린다.
        //         계산 결과가 1장이면 아무것도 바꾸지 않는다 → 몸이 좁은 유닛은 예전과 똑같다.
        //   기본값 꺼짐 — 값을 넣지 않은 기존 프리셋(독·힐 포함)은 예전과 완전히 같다. 프리팹 자체는 고치지 않으며,
        //   바꾼 파티클 값은 풀 반환 때 원래 값으로 되돌린다(VfxPoolItem.TimedDamage.cs 「폭 맞춤」 절).
        //   화면 표현 전용이다. 피해량·지속시간·서버 판정과 무관하다.
        [Header("Recipient Width Fit (상태 이펙트 전용)")]
        [Tooltip("켜면 몸이 넓은 유닛에서 불꽃(파티클)이 몸통 폭만큼 넓은 범위에서 여러 장 피어오른다. "
            + "불꽃 한 장의 크기·모양은 그대로다. 몸이 좁아 계산상 1장이면 예전과 똑같다. "
            + "화상·독·힐 같은 대상 상태 이펙트 프리셋에서만 의미가 있다(공격·사망 이펙트에는 효과 없음).")]
        [SerializeField] private bool _widthFitEnabled;

        [Tooltip("폭 맞춤으로 늘어나는 불꽃 개수의 상한(배수). 1이면 늘리지 않는다. "
            + "새 대형 유닛이 생겨도 파티클 수가 무한히 늘지 않게 막는 안전장치다.")]
        [Min(1)]
        [SerializeField] private int _widthFitMaxCount = DefaultWidthFitMaxCount;

        /// <summary>폭 맞춤 개수 상한 기본값(2026-10-26 사용자 결정 D2 = 3). 현재 크기 표의 최대 필요 개수(탱크·대포수레)와 같다.</summary>
        public const int DefaultWidthFitMaxCount = 3;

        // [2026-10-26 D7 후속 — 상태 이펙트를 몸 앞에 그리기] 화상·독·힐처럼 "유닛 몸에 붙는 상태 이펙트" 전용 옵션.
        //   문제: 상태 이펙트는 유닛 몸 "한가운데"(발밑 기준 위치)에 서 있다. 유닛 몸은 불투명이라 먼저 그려지며
        //         "여기까지가 몸 표면"이라는 깊이 정보를 남기고, 불꽃(투명)은 나중에 그 깊이와 비교해
        //         몸 표면보다 카메라에서 먼 부분은 그리지 않는다. 그래서 몸 앞부분이 불꽃 대부분을 덮어 잘 안 보였다
        //         (곰전사 실기 2026-10-26 「몸 안에 가려져서 안 보인다」, Research 10-1·10-5).
        //   해결: 이 값을 0보다 크게 넣으면 재생 시작 때 이펙트 위치를 "카메라 쪽 방향"으로
        //         (크기 표의 몸통 반경 × 이 값)만큼 당긴다(사용자 결정 D7-1 (가)·D7-2 (a)).
        //   🔴 이 게임 카메라는 원근이 없는 직교(Orthographic) 카메라다. 직교 카메라에서는 물체를 카메라 쪽으로 옮겨도
        //      화면에서의 위치·크기·모양이 전혀 바뀌지 않고 "무엇의 앞에 그려지는지(깊이)"만 바뀐다.
        //      즉 불꽃 모양은 그대로이고, 몸에 덮이던 부분만 몸 앞에 드러난다.
        //      (카메라를 원근으로 바꾸면 이 전제가 깨져 당긴 만큼 화면 위치·크기가 조금 달라진다.)
        //   기본값 0 = 꺼짐 — 값을 넣지 않은 기존 프리셋(독·힐 포함)은 예전과 완전히 같다(D7-3 (a): 구조만 두고 화상만 켬).
        //   재질·셰이더·프리팹은 바꾸지 않는다. 화면 표현 전용이다. 피해량·지속시간·서버 판정과 무관하다.
        [Header("Recipient View Pull (상태 이펙트 전용)")]
        [Tooltip("0보다 크면 상태 이펙트를 카메라 쪽으로 (몸통 반경 × 이 값)만큼 당겨, 유닛 몸에 가려지던 부분을 몸 앞에 그린다. "
            + "직교 카메라에서는 화면 위치·크기가 바뀌지 않고 깊이(앞뒤)만 바뀐다. 0 = 꺼짐(예전과 같음). "
            + "권장 1.75 = 1 ÷ cos 55°(카메라 기울기 55°에서 몸 가운데 불꽃이 몸 앞면을 벗어나는 최소 비율). "
            + "더 잘 보이게 하려면 올리고, 앞을 지나가는 유닛·건물 앞으로 튀어나와 보이면 내린다. "
            + "화상·독·힐 같은 대상 상태 이펙트 프리셋에서만 의미가 있다(공격·사망 이펙트에는 효과 없음).")]
        [Min(0f)]
        [SerializeField] private float _viewPullRadiusRatio;

        /// <summary>
        /// 카메라 쪽 당김 비율 권장값(2026-10-26 사용자 결정 D7-2 = 1.75). 화상 프리셋에 넣은 값이다.
        /// 1 ÷ cos 55° ≈ 1.743 — 카메라가 55° 기울어져 있을 때, 몸통을 원기둥으로 본 근사에서
        /// 몸 가운데 불꽃이 몸 앞면을 벗어나기 위한 최소 비율을 조금 올려 잡은 값이다(Research 10-5).
        /// 필드 기본값은 이것이 아니라 0(꺼짐)이다 — 기존 프리셋이 저절로 바뀌지 않게 하기 위해서다.
        /// </summary>
        public const float RecommendedViewPullRadiusRatio = 1.75f;

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

        /// <summary>
        /// 상태 이펙트 폭 맞춤을 켤지. 화상·독(VfxPoolItem.PlayTimedDamage)과 힐(VfxPoolItem.PlayBloomLoop) 재생 경로에서만 읽는다.
        /// false(기본)면 그 재생 경로는 파티클 모듈 값을 전혀 바꾸지 않는다 — 예전과 같다.
        /// </summary>
        public bool WidthFitEnabled => _widthFitEnabled;

        /// <summary>
        /// 폭 맞춤 불꽃 개수 상한. Inspector에 1 미만이 들어가도(옛 에셋·직접 편집) 1 이상으로 보정한다.
        /// </summary>
        public int WidthFitMaxCount => Mathf.Max(1, _widthFitMaxCount);

        /// <summary>
        /// 상태 이펙트 카메라 쪽 당김 비율(몸통 반경에 곱하는 값). 화상·독(VfxPoolItem.PlayTimedDamage)과
        /// 힐(VfxPoolItem.PlayBloomLoop) 재생 경로에서만 읽는다.
        /// 0(기본) = 당기지 않음 — 예전과 같다. Inspector나 옛 에셋에 음수·NaN·무한대가 들어가도 0(꺼짐)으로 본다.
        /// </summary>
        public float ViewPullRadiusRatio => _viewPullRadiusRatio > 0f && !float.IsInfinity(_viewPullRadiusRatio)
            ? _viewPullRadiusRatio : 0f; // NaN은 "> 0" 비교가 false라 자동으로 0이 된다

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
