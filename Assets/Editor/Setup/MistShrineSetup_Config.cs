// ============================================================================
// MistShrineSetup_Config.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  상단 메뉴  Hexiege > MistShrine > 1. Apply Config Values (SpecialAttackConfig) │
// │  실행. (씬을 열 필요 없다 — 에셋 파일 하나만 건드린다.)                        │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가(유니티 초급자 기준):
//   MistShrine(물안개 신전)의 밸런싱 수치 5개를 기존 설정 에셋
//   Assets/_Project/Resources/Config/SpecialAttackConfig.asset 에 기록한다.
//
//   왜 필요한가:
//     이 프로젝트는 "Inspector(에셋) 값이 코드 기본값보다 우선"하는 구조다.
//     C# 코드(SpecialAttackConfig.cs)에 필드를 새로 추가해도, 이미 만들어져 있던
//     .asset 파일에는 그 필드가 아직 기록돼 있지 않다(위 asset 을 열어 보면
//     _mistHealPerSecond 같은 줄이 없다). 그 상태에서도 Unity 는 코드 기본값을
//     쓰지만, "값이 에셋에 적혀 있는지"가 눈으로 확인되지 않아 나중에 밸런싱
//     담당자가 어디를 고쳐야 하는지 알 수 없다.
//     이 스크립트는 5개 값을 에셋에 실제로 써 넣어, 앞으로는 **이 에셋 한 파일만**
//     고치면 밸런싱이 끝나도록 만든다(Plan §3-1).
//
// 기록하는 값(전부 임시값 — 밸런싱 미확정, Plan §3):
//   _mistHealPerSecond    = 10   (초당 회복량 HP/s)
//   _mistDuration         = 10   (물안개 지속시간, 초)
//   _mistCooldown         = 20   (재사용 대기시간, 초 — 지속시간보다 길어야 함)
//   _mistRadius           =  3   (회복 범위 반경, 월드 단위)
//   _mistHealTextInterval =  3   (회복 텍스트 표시 주기, 초)
//
// 기존 값 보호(중요):
//   이 스크립트는 위 5개 "MistShrine 필드"만 건드린다. 에셋에 이미 들어 있는
//   다른 값(_sweepReach, _waveHeal, _quakeRadius 등)은 읽지도 쓰지도 않는다.
//   또한 MistShrine 필드라도 **이미 임시값과 다른 값이 들어 있으면 덮어쓰지 않고
//   경고만 남긴다.** (누군가 밸런싱을 반영해 둔 값을 이 스크립트 재실행이
//   되돌려 버리는 사고를 막기 위함이다.)
//
// 멱등성(여러 번 실행해도 안전):
//   이미 같은 값이면 아무것도 쓰지 않고 넘어간다. 값이 다르면 위 규칙대로 보호한다.
// ============================================================================

using UnityEditor;
using UnityEngine;
using Hexiege.Infrastructure;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// MistShrine 물안개 힐 임시 수치 5개를 SpecialAttackConfig.asset 에 기록하는 1회성 에디터 스크립트.
    /// </summary>
    public static class MistShrineSetup_Config
    {
        /// <summary>수치를 기록할 대상 설정 에셋 경로.</summary>
        private const string ConfigAssetPath = "Assets/_Project/Resources/Config/SpecialAttackConfig.asset";

        /// <summary>에셋이 없을 때 사용자에게 안내할 선행 메뉴 경로.</summary>
        private const string CreateAssetMenuPath = "Hexiege/Setup/Create SpecialAttackConfig Asset (Game)";

        /// <summary>
        /// 기록 대상 (필드명, 임시값) 목록. Plan §3 의 임시값 표와 1:1로 대응한다.
        /// 여기에 없는 필드는 절대 건드리지 않는다.
        /// </summary>
        private static readonly (string Field, float Value, string Note)[] MistValues =
        {
            ("_mistHealPerSecond",   10f, "초당 회복량(HP/s)"),
            ("_mistDuration",        10f, "물안개 지속시간(초)"),
            ("_mistCooldown",        20f, "재사용 대기시간(초)"),
            ("_mistRadius",           3f, "회복 범위 반경(월드 단위)"),
            ("_mistHealTextInterval", 3f, "회복 텍스트 표시 주기(초)"),
        };

        [MenuItem("Hexiege/MistShrine/1. Apply Config Values (SpecialAttackConfig)")]
        public static void Run()
        {
            // ── 1) 대상 에셋 확보 ─────────────────────────────────────────
            var config = AssetDatabase.LoadAssetAtPath<SpecialAttackConfig>(ConfigAssetPath);
            if (config == null)
            {
                Debug.LogError(
                    $"[MistShrine Setup] SpecialAttackConfig 에셋을 찾지 못했습니다: {ConfigAssetPath}\n" +
                    $"  → 먼저 메뉴 '{CreateAssetMenuPath}' 를 실행해 에셋을 만든 뒤 다시 시도하세요.");
                return;
            }

            // ── 2) 필드별 기록(멱등 + 기존 값 보호) ────────────────────────
            //   SerializedObject 를 쓰는 이유: 대상 필드가 private [SerializeField] 라
            //   C# 코드에서 직접 대입할 수 없기 때문이다. Unity 의 직렬화 계층을 통해
            //   Inspector 가 하는 것과 똑같은 방식으로 값을 쓴다.
            var so = new SerializedObject(config);

            int written = 0;      // 이번 실행에서 실제로 기록한 개수.
            int unchanged = 0;    // 이미 같은 값이라 건너뛴 개수(멱등).
            int protectedCount = 0; // 다른 값이 있어 보호(미변경)한 개수.

            for (int i = 0; i < MistValues.Length; i++)
            {
                (string field, float value, string note) = MistValues[i];

                SerializedProperty prop = so.FindProperty(field);
                if (prop == null)
                {
                    // 코드(SpecialAttackConfig.cs)에 필드가 없는 상태 — 스크립트가 아직
                    // 컴파일되지 않았거나 필드명이 바뀐 경우다. 조용히 넘기지 않고 알린다.
                    Debug.LogError(
                        $"[MistShrine Setup] SpecialAttackConfig 에 '{field}' 필드가 없습니다({note}). " +
                        "코드가 최신인지(컴파일 완료 여부) 확인하세요.");
                    continue;
                }

                float current = prop.floatValue;

                // (a) 이미 같은 값 → 아무것도 하지 않는다(멱등).
                if (Mathf.Approximately(current, value))
                {
                    unchanged++;
                    continue;
                }

                // (b) 0 이 아닌 다른 값이 이미 들어 있다 → 밸런싱 반영으로 보고 보호한다.
                //     (0 은 "아직 에셋에 기록된 적 없는 상태"의 기본값이라 덮어써도 안전하다.)
                if (!Mathf.Approximately(current, 0f))
                {
                    Debug.LogWarning(
                        $"[MistShrine Setup] '{field}' 는 이미 {current} 로 설정돼 있어 덮어쓰지 않았습니다" +
                        $"({note}, 임시값 {value}). 밸런싱 반영값으로 보입니다. " +
                        "임시값으로 되돌리려면 Inspector 에서 직접 수정하세요.");
                    protectedCount++;
                    continue;
                }

                // (c) 미기록(0) → 임시값 기록.
                prop.floatValue = value;
                written++;
            }

            // ── 3) 저장 반영 ──────────────────────────────────────────────
            //   ApplyModifiedProperties 만으로는 .asset 파일이 디스크에 갱신되지 않는다.
            //   SetDirty + SaveAssets 까지 해야 실제 파일에 기록된다(에디터 스크립트 필수 절차).
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[MistShrine Setup] SpecialAttackConfig 물안개 수치 반영 완료.\n" +
                $"  · 기록 {written}개 / 이미 동일 {unchanged}개 / 기존 값 보호 {protectedCount}개.\n" +
                $"  · 대상 에셋: {ConfigAssetPath}\n" +
                "  · 이 5개는 전부 임시값(밸런싱 미확정)이며, 확정 시 이 에셋만 고치면 됩니다(코드 수정 불필요).");

            Selection.activeObject = config;
        }
    }
}
