// ============================================================================
// ToastMessageConfig.cs
// 토스트 메시지 키(ToastKey) → 실제 표시 문구·표시 시간을 정의하는 ScriptableObject.
//
// 역할:
//   - "골드 부족", "인구 가득 참" 등 토스트로 보여줄 문구를 한곳에 모아 관리.
//   - 코드 변경 없이 Inspector에서 텍스트와 표시 시간을 조정 가능.
//
// 생성 방법:
//   1) Project 창에서 Assets/_Project/Resources/Config 폴더로 이동.
//   2) 우클릭 → Create → Hexiege/Config/ToastMessageConfig 선택.
//   3) 생성된 에셋 이름을 "ToastMessageConfig"로 저장(파일명 고정 — Resources.Load로 로드 시 필요).
//   4) Inspector의 Entries 리스트에 ToastKey마다 한 줄씩 채워 넣는다.
//      예) Key=GoldInsufficient, Message="골드가 부족합니다", Duration=2
//
// 사용 흐름:
//   1) GameBootstrapper(또는 ToastUI 본인)가 Resources.Load<ToastMessageConfig>("Config/ToastMessageConfig")로 로드.
//   2) ToastUI.Initialize(config)로 주입.
//   3) ToastUI가 Show(ToastKey)를 받으면 config.TryGet(key, out entry)로 문구/시간 조회.
//
// Infrastructure 레이어 — Unity 의존 (ScriptableObject).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Presentation;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 토스트 메시지의 문구와 표시 시간을 관리하는 ScriptableObject.
    /// ToastKey 단위로 항목을 등록하면, ToastUI가 해당 키를 받았을 때 자동으로 문구를 찾아 표시한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ToastMessageConfig",
                     menuName = "Hexiege/Config/ToastMessageConfig")]
    public class ToastMessageConfig : ScriptableObject
    {
        /// <summary>
        /// 토스트 한 종류에 대한 설정 한 줄.
        /// Inspector의 List에서 시각적으로 한 항목씩 추가하여 사용.
        /// </summary>
        [System.Serializable]
        public struct ToastEntry
        {
            [Tooltip("이 항목이 어떤 토스트인지 식별하는 키. 호출 코드에서 이 키로 토스트를 요청한다.")]
            public ToastKey key;

            [Tooltip("화면에 표시될 문구. 한국어 권장. 멀티라인 가능.")]
            [TextArea] public string message;

            [Tooltip("표시 시간(초). 최소 1초 — 너무 짧으면 사용자가 못 읽음.")]
            [Min(1f)] public float duration;
        }

        [Tooltip("토스트 키별 설정 리스트. 중복 키가 있으면 첫 번째 항목이 사용됨.")]
        [SerializeField] private List<ToastEntry> _entries = new List<ToastEntry>();

        /// <summary>
        /// 등록된 항목 수. 디버깅·검증 용도.
        /// </summary>
        public int Count => _entries != null ? _entries.Count : 0;

        /// <summary>
        /// 주어진 키에 해당하는 토스트 설정을 찾는다.
        /// 같은 키가 여러 번 등록되어 있으면 가장 앞쪽 항목을 반환.
        /// 못 찾으면 false 반환 + entry는 기본값(빈 메시지, duration=1).
        /// </summary>
        /// <param name="key">찾고자 하는 토스트 키.</param>
        /// <param name="entry">찾은 항목의 사본. 못 찾으면 안전한 기본값을 채움.</param>
        /// <returns>찾으면 true, 없으면 false.</returns>
        public bool TryGet(ToastKey key, out ToastEntry entry)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].key == key)
                    {
                        entry = _entries[i];
                        // 안전 가드 — Inspector 실수로 duration=0이 들어왔으면 1초로 보정.
                        if (entry.duration < 1f) entry.duration = 1f;
                        return true;
                    }
                }
            }

            // 항목이 정의되어 있지 않을 때의 폴백.
            // 운영상으로는 모든 ToastKey가 등록되어 있어야 하지만,
            // 빠진 항목이 있어도 게임이 죽지 않도록 안전한 기본값을 반환.
            entry = new ToastEntry
            {
                key = key,
                message = key.ToString(),
                duration = 1.5f
            };
            return false;
        }
    }
}
