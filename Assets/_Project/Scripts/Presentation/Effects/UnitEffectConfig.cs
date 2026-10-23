// ============================================================================
// UnitEffectConfig.cs
// UnitType(유닛 종류)별 공격/사망 이펙트를 Inspector에서 설정하는 ScriptableObject.
//
// 구조:
//   UnitStatsConfig와 동일한 List<Entry> 패턴.
//   각 항목(UnitEffectEntry)은 "유닛 종류 + 공격 프리셋 + 사망 프리셋"을 묶는다.
//
// 조회 성능:
//   런타임에 List를 매번 순회하면 느리므로, Initialize()에서 한 번
//   Dictionary<UnitType, UnitEffectEntry>로 캐싱하여 O(1) 조회를 보장한다.
//
// 사용 흐름:
//   GameBootstrapper가 이 에셋을 EffectManager.Initialize()에 전달 →
//   EffectManager가 Initialize()를 호출해 Dictionary를 빌드 →
//   이후 PlayUnitAttack/PlayUnitDeath에서 GetAttack/GetDeath로 프리셋 조회.
//
// 에셋 경로: Assets/_Project/Resources/Config/UnitEffectConfig.asset
//
// Presentation 레이어 — Unity 의존 허용 (ScriptableObject).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    // ========================================================================
    // UnitEffectEntry
    // 한 유닛 종류(UnitType)에 대한 공격/사망 이펙트 프리셋을 묶은 구조체.
    // List<UnitEffectEntry> 형태로 UnitEffectConfig에 보관되며 Inspector에서 편집.
    //
    // [System.Serializable]이 있어야 Inspector에 노출됨.
    // ========================================================================
    [System.Serializable]
    public struct UnitEffectEntry
    {
        [Tooltip("이 항목이 적용되는 유닛 타입")]
        public UnitType unitType;

        [Tooltip("공격 시 재생할 이펙트 (VFX). 없으면 비워둠")]
        public EffectPreset attackPreset;

        [Tooltip("실제 Bloom HoT 대상의 지속 힐 표현. 공격 marker와 독립인 슬롯")]
        public EffectPreset healPreset;
        public EffectPreset timedDamagePreset;
        public EffectPreset impactPreset;

        [Tooltip("사망 시 재생할 이펙트 (VFX). 없으면 비워둠")]
        public EffectPreset deathPreset;

        [Tooltip("피격(맞았을 때) 시 재생할 이펙트 (VFX). 없으면 비워둠 — 조용히 스킵됨")]
        public EffectPreset hitPreset;

        [Tooltip("원거리 공격 시 발사할 트레이서(발사체) 이펙트. VfxPrefab 슬롯에 트레이서 프리팹을 연결한다. "
            + "비워두면 트레이서 없이 기존처럼 즉시 피격 연출(폴백). 근접 유닛은 사용하지 않음")]
        public EffectPreset tracerPreset;
    }

    // ========================================================================
    // UnitRecipientFitEntry (2026-10-13 vfx-code-followups C)
    // 화상·독·힐처럼 "유닛 몸에 붙는 상태 이펙트"를 그 유닛 크기에 맞추기 위한 유닛 종류별 크기 표의 한 줄.
    //
    // 값은 처음에 에디터 메뉴 「Hexiege/Effects/Measure Unit Recipient Fit」이 유닛 프리팹을 재서 채운다.
    // 그 뒤에는 Inspector에서 사람이 직접 고쳐도 된다(게임 중에는 이 표만 읽고, 매 프레임 크기를 재지 않는다).
    // 측정 기준 좌표 = 유닛의 VisualRoot(발밑 pivot) 기준, 단위 = 월드 미터.
    // ========================================================================
    [System.Serializable]
    public struct UnitRecipientFitEntry
    {
        [Tooltip("이 크기 값이 적용되는 유닛 타입")]
        public UnitType unitType;

        [Tooltip("몸통 반경(미터). 측정 도구가 채운다. 현재 배율 계산에는 쓰지 않고 참고·튜닝용으로 보관한다(D-3 균등 배율)")]
        public float radius;

        [Tooltip("몸통 키(미터). 자동 배율 = 키 ÷ 1.0(이펙트 제작 기준 키). 0 이하면 이 줄은 무시된다")]
        public float height;

        [Tooltip("VisualRoot 기준 발밑 위치 보정(미터). 측정 도구가 몸통 아래 가운데로 채운다")]
        public Vector3 footOffset;

        [Tooltip("0 이하 = 자동(키 비율). 양수 = 이 값을 배율로 그대로 사용(사람이 직접 덮어쓰기)")]
        public float manualScale;
    }

    // ========================================================================
    // RecipientFit
    // 크기 표에서 계산한 "실제로 적용할 값" 묶음. VfxPoolItem이 재생 시작 때 1번 받는다.
    //
    // 🔴 default(RecipientFit) = Identity = 배율 1, 발밑 보정 0 → 예전과 완전히 같은 크기·위치.
    //   그래서 표에 없는 유닛이나 크기를 모르는 호출은 아무것도 안 바꾼 것과 같다(안전한 기본값).
    // ========================================================================
    public readonly struct RecipientFit
    {
        /// <summary>표 값이 잘못 들어가도 이펙트가 사라지거나(0) 화면을 덮지(거대) 않게 막는 하한.</summary>
        public const float MinScale = 0.1f;
        /// <summary>배율 상한.</summary>
        public const float MaxScale = 10f;

        /// <summary>원본 배율 값(검증 전). 0·음수·NaN이면 UniformScale은 1이 된다.</summary>
        public readonly float Scale;
        /// <summary>원본 발밑 보정 값(검증 전).</summary>
        public readonly Vector3 FootOffset;

        /// <summary>배율과 발밑 보정으로 맞춤 값을 만든다.</summary>
        public RecipientFit(float scale, Vector3 footOffset)
        {
            Scale = scale;
            FootOffset = footOffset;
        }

        /// <summary>크기 맞춤을 하지 않는 값(배율 1, 보정 0). default와 같다.</summary>
        public static RecipientFit Identity => default;

        /// <summary>실제로 곱할 균등 배율. 유효하지 않으면 1, 유효하면 [MinScale, MaxScale]로 제한.</summary>
        public float UniformScale => Scale > 0f && !float.IsNaN(Scale) && !float.IsInfinity(Scale)
            ? Mathf.Clamp(Scale, MinScale, MaxScale) : 1f;

        /// <summary>실제로 더할 발밑 보정. NaN/무한대가 섞이면 0(보정 없음).</summary>
        public Vector3 SafeFootOffset => IsFinite(FootOffset) ? FootOffset : Vector3.zero;

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
            && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }

    // ========================================================================
    // UnitEffectConfig
    // 모든 UnitType의 UnitEffectEntry를 한곳에 모으는 ScriptableObject.
    // ========================================================================
    [CreateAssetMenu(fileName = "UnitEffectConfig", menuName = "Hexiege/Effects/UnitEffectConfig")]
    public class UnitEffectConfig : ScriptableObject
    {
        [Tooltip("각 유닛 타입별 공격/사망 이펙트 프리셋. Inspector에서 편집")]
        [SerializeField] private List<UnitEffectEntry> _entries = new List<UnitEffectEntry>();

        /// <summary>
        /// 빠른 조회를 위한 캐시. Initialize()에서 빌드.
        /// 중복 키가 있으면 처음 항목만 유지.
        /// </summary>
        private Dictionary<UnitType, UnitEffectEntry> _cache;

        /// <summary>상태 이펙트(화상·독·힐) 프리팹을 만들 때 기준으로 삼은 유닛 키(미터). 이 키면 배율 1.</summary>
        public const float RecipientReferenceHeight = 1.0f;
        /// <summary>상태 이펙트 프리팹 제작 기준 반경(미터). 참고용(현재 배율 계산은 키 기준 균등).</summary>
        public const float RecipientReferenceRadius = 0.25f;

        [Header("상태 이펙트 크기 맞춤 (화상·독·힐)")]
        [Tooltip("유닛 종류별 몸 크기 표. 메뉴 Hexiege/Effects/Measure Unit Recipient Fit 이 채우고, 이후 직접 조정 가능. "
            + "표에 없는 유닛은 배율 1(예전 크기)로 재생된다")]
        [SerializeField] private List<UnitRecipientFitEntry> _recipientFits = new List<UnitRecipientFitEntry>();

        /// <summary>크기 표 조회 캐시. Initialize()에서 빌드.</summary>
        private Dictionary<UnitType, UnitRecipientFitEntry> _fitCache;

        // ── [2026-10-13 R2-2] 정령 타워 번개가 맞는 "가슴 높이" ──
        //   번개 끝점 = 맞은 유닛 발밑 + 위쪽 × (그 유닛 키 × 이 비율).
        //   키는 바로 위 「상태 이펙트 크기 맞춤」 표를 그대로 쓴다(같은 표 하나로 유닛 크기를 관리).

        /// <summary>가슴 높이 비율 기본값. 유닛 키의 60% 높이 ≈ 가슴.</summary>
        public const float DefaultChestHeightRatio = 0.6f;

        /// <summary>크기 표에 그 유닛 줄이 없을 때 쓰는 가슴 높이(월드 미터). 기준 키 1.0 × 0.6과 같은 값.</summary>
        public const float FallbackChestHeight = 0.6f;

        [Header("타워 번개 조준 높이")]
        [Tooltip("정령 타워(RuneSpire) 번개가 맞는 높이 = 맞은 유닛 키(위 크기 표) × 이 비율. 0.6 ≈ 가슴. "
            + "0이면 발밑, 1이면 머리 꼭대기. 크기 표에 없는 유닛은 0.6m 높이를 쓴다")]
        [Min(0f)]
        [SerializeField] private float _chestHeightRatio = DefaultChestHeightRatio;

        /// <summary>
        /// Inspector의 가슴 높이 비율. 음수·NaN·무한대처럼 쓸 수 없는 값이면 기본값 0.6을 돌려준다.
        /// (옛 에셋에 이 필드가 없으면 Unity가 위 초기값 0.6을 그대로 쓴다.)
        /// </summary>
        public float ChestHeightRatio => _chestHeightRatio >= 0f && !float.IsNaN(_chestHeightRatio)
            && !float.IsInfinity(_chestHeightRatio) ? _chestHeightRatio : DefaultChestHeightRatio;

        /// <summary>
        /// 이 유닛 종류의 "가슴 높이"(발밑 기준, 월드 미터)를 돌려준다. 정령 타워 번개 끝점에 쓴다.
        /// = 크기 표 조회(TryGetRecipientFit)가 주는 키(균등 배율 × 기준 키 1.0) × ChestHeightRatio.
        /// 표에 그 유닛이 없거나 값이 쓸 수 없으면 FallbackChestHeight(0.6m).
        /// </summary>
        /// <param name="type">번개를 맞는 유닛 타입.</param>
        public float GetChestHeight(UnitType type)
        {
            if (!TryGetRecipientFit(type, out RecipientFit fit)) return FallbackChestHeight;
            // UniformScale은 이미 [0.1, 10]으로 제한된 값이라 키가 터무니없이 커지지 않는다.
            return fit.UniformScale * RecipientReferenceHeight * ChestHeightRatio;
        }

        /// <summary>
        /// 이 유닛 종류에 쓸 상태 이펙트 크기 맞춤 값을 반환한다.
        /// 표에 없거나 값이 쓸 수 없으면 false + Identity(배율 1, 보정 0).
        /// 배율 = manualScale(양수일 때) 우선, 아니면 키 ÷ RecipientReferenceHeight (D-3 균등 배율).
        /// </summary>
        /// <param name="type">이펙트가 붙을 대상 유닛 타입.</param>
        /// <param name="fit">적용할 맞춤 값.</param>
        /// <returns>표의 유효한 줄을 찾았으면 true.</returns>
        public bool TryGetRecipientFit(UnitType type, out RecipientFit fit)
        {
            fit = RecipientFit.Identity;
            if (_fitCache == null || !_fitCache.TryGetValue(type, out UnitRecipientFitEntry entry)) return false;
            float scale = IsPositiveFinite(entry.manualScale) ? entry.manualScale
                : IsPositiveFinite(entry.height) ? entry.height / RecipientReferenceHeight : 0f;
            if (scale <= 0f) return false; // 키도 수동 배율도 없는 줄 → 맞춤 없음(배율 1)
            fit = new RecipientFit(scale, entry.footOffset);
            return true;
        }

        /// <summary>TryGetRecipientFit의 간단판 — 못 찾으면 Identity(배율 1)를 돌려준다.</summary>
        /// <param name="type">이펙트가 붙을 대상 유닛 타입.</param>
        public RecipientFit GetRecipientFit(UnitType type)
        {
            TryGetRecipientFit(type, out RecipientFit fit);
            return fit;
        }

        /// <summary>크기 표에 줄이 하나라도 있는 유닛인지(검증기의 「25종 표 완비」 경고용).</summary>
        /// <param name="type">확인할 유닛 타입.</param>
        public bool HasRecipientFitEntry(UnitType type) => _fitCache != null && _fitCache.ContainsKey(type);

        private static bool IsPositiveFinite(float v) => v > 0f && !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>지속 피해를 일으킨 유닛 타입의 VFX 프리셋을 조회한다. 없으면 null.</summary>
        public EffectPreset GetTimedDamage(UnitType sourceType)
        { return _cache != null && _cache.TryGetValue(sourceType, out var entry) ? entry.timedDamagePreset : null; }
        /// <summary>유닛 타입의 착탄 VFX 프리셋을 조회한다. 없으면 null.</summary>
        public EffectPreset GetImpact(UnitType sourceType)
        { return _cache != null && _cache.TryGetValue(sourceType, out var entry) ? entry.impactPreset : null; }
        /// <summary>
        /// List를 Dictionary로 변환하여 O(1) 조회를 준비한다.
        /// EffectManager.Initialize()에서 호출.
        /// 여러 번 호출되어도 안전하도록 매번 새로 빌드한다.
        /// </summary>
        public void Initialize()
        {
            _cache = new Dictionary<UnitType, UnitEffectEntry>(_entries.Count);
            foreach (UnitEffectEntry entry in _entries)
            {
                // 중복 unitType이 들어와도 예외 없이 첫 항목만 유지.
                if (!_cache.ContainsKey(entry.unitType))
                    _cache.Add(entry.unitType, entry);
            }

            // 상태 이펙트 크기 표도 같은 방식(중복은 첫 줄만)으로 캐시한다.
            //   옛 에셋에는 _recipientFits 필드가 없어 null로 읽힐 수 있으므로 방어한다.
            _fitCache = new Dictionary<UnitType, UnitRecipientFitEntry>(_recipientFits?.Count ?? 0);
            if (_recipientFits != null)
                foreach (UnitRecipientFitEntry fit in _recipientFits)
                    if (!_fitCache.ContainsKey(fit.unitType))
                        _fitCache.Add(fit.unitType, fit);
        }

        /// <summary>
        /// 지정한 유닛 타입의 공격 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>공격 EffectPreset 또는 null.</returns>
        public EffectPreset GetAttack(UnitType type)
        {
            // Initialize() 전에 호출되면 캐시가 없으므로 안전하게 null 반환.
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out UnitEffectEntry entry) ? entry.attackPreset : null;
        }

        public EffectPreset GetHeal(UnitType type) => _cache != null
            && _cache.TryGetValue(type, out UnitEffectEntry entry) ? entry.healPreset : null;

        /// <summary>
        /// 지정한 유닛 타입의 사망 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>사망 EffectPreset 또는 null.</returns>
        public EffectPreset GetDeath(UnitType type)
        {
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out UnitEffectEntry entry) ? entry.deathPreset : null;
        }

        /// <summary>
        /// 지정한 유닛 타입의 피격 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null (호출부에서 조용히 스킵).
        /// </summary>
        /// <param name="type">조회할(피격당한) 유닛 타입.</param>
        /// <returns>피격 EffectPreset 또는 null.</returns>
        public EffectPreset GetHit(UnitType type)
        {
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out UnitEffectEntry entry) ? entry.hitPreset : null;
        }

        /// <summary>
        /// 지정한 유닛 타입의 트레이서(원거리 발사체) 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null.
        /// null이면 트레이서 없이 즉시 피격 연출로 폴백한다(호출부에서 처리).
        /// </summary>
        /// <param name="type">조회할(공격하는) 원거리 유닛 타입.</param>
        /// <returns>트레이서 EffectPreset 또는 null.</returns>
        public EffectPreset GetTracer(UnitType type)
        {
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out UnitEffectEntry entry) ? entry.tracerPreset : null;
        }
    }
}
