// ============================================================================
// UiEffectConfig.cs
// UI 상황별 이펙트를 enum 키로 조회하는 ScriptableObject.
//
// UnitEffectConfig가 UnitType으로 조회한다면, 이 Config는 UiEffectKey(enum)로
// 조회한다. 버튼 클릭/패널 열기/골드 획득 등 정해진 UI 상황마다 키를 부여하고
// 그에 맞는 EffectPreset(주로 SFX)을 연결한다.
//
// 에셋 경로: Assets/_Project/Resources/Config/UiEffectConfig.asset
//
// Presentation 레이어 — Unity 의존 허용 (ScriptableObject).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Hexiege.Presentation
{
    // ========================================================================
    // UiEffectKey
    // UI 이펙트를 식별하는 키. 정해진 UI 상황마다 하나씩 대응한다.
    // 새 UI 연출이 필요하면 여기에 항목을 추가하고 Config에서 프리셋을 연결한다.
    // ========================================================================
    public enum UiEffectKey
    {
        ButtonClick,    // 일반 버튼 클릭
        ButtonConfirm,  // 확인/수락 버튼
        ButtonCancel,   // 취소/닫기 버튼
        PanelOpen,      // 패널/팝업 열기
        PanelClose,     // 패널/팝업 닫기
        GoldGain,       // 골드 획득
        TimerComplete,  // 타이머/생산 완료
    }

    // ========================================================================
    // UiEffectEntry
    // 한 UI 키(UiEffectKey)에 대응하는 이펙트 프리셋.
    // ========================================================================
    [System.Serializable]
    public struct UiEffectEntry
    {
        [Tooltip("이 항목이 적용되는 UI 이펙트 키")]
        public UiEffectKey key;

        [Tooltip("재생할 이펙트 (UI는 주로 SFX). 없으면 비워둠")]
        public EffectPreset preset;
    }

    // ========================================================================
    // UiEffectConfig
    // 모든 UiEffectKey의 UiEffectEntry를 모으는 ScriptableObject.
    // ========================================================================
    [CreateAssetMenu(fileName = "UiEffectConfig", menuName = "Hexiege/Effects/UiEffectConfig")]
    public class UiEffectConfig : ScriptableObject
    {
        [Tooltip("각 UI 키별 이펙트 프리셋. Inspector에서 편집")]
        [SerializeField] private List<UiEffectEntry> _entries = new List<UiEffectEntry>();

        /// <summary> 빠른 조회를 위한 캐시. Initialize()에서 빌드. </summary>
        private Dictionary<UiEffectKey, EffectPreset> _cache;

        /// <summary>
        /// List를 Dictionary로 변환하여 O(1) 조회를 준비한다.
        /// EffectManager.Initialize()에서 호출.
        /// </summary>
        public void Initialize()
        {
            _cache = new Dictionary<UiEffectKey, EffectPreset>(_entries.Count);
            foreach (UiEffectEntry entry in _entries)
            {
                if (!_cache.ContainsKey(entry.key))
                    _cache.Add(entry.key, entry.preset);
            }
        }

        /// <summary>
        /// 지정한 UI 키의 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null.
        /// </summary>
        /// <param name="key">조회할 UI 이펙트 키.</param>
        /// <returns>EffectPreset 또는 null.</returns>
        public EffectPreset Get(UiEffectKey key)
        {
            if (_cache == null) return null;
            return _cache.TryGetValue(key, out EffectPreset preset) ? preset : null;
        }
    }
}
