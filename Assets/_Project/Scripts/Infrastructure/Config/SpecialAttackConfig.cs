// ============================================================================
// SpecialAttackConfig.cs
// 특수 공격(도끼병 휩쓸기 등)의 튜닝 수치를 Unity Inspector에서 편집 가능하게
// 만드는 ScriptableObject.
//
// ScriptableObject란?
//   Unity의 데이터 컨테이너 에셋. 씬에 붙이지 않고 .asset 파일로 저장되며,
//   Inspector에서 값을 편집할 수 있다. 코드(재컴파일) 없이 수치를 조정 가능하므로
//   밸런싱 작업에 유용하다. (UnitStatsConfig와 동일한 패턴)
//
// 이 에셋의 역할:
//   도끼병(BattleAxe)의 "휩쓸기형 AoE" 판정에 쓰이는 두 값을 담는다.
//     - sweepReach      : 전방 부채꼴 판정의 월드 반경(공격자로부터의 XZ 평면 거리 한계)
//     - sweepArcHalfAngle: 전방 부채꼴의 반각(도 단위). forward(주 타깃 방향)와 이루는
//                          각도가 이 값 이하이면 부채꼴 안으로 판정.
//   게임 시작 시 GameBootstrapper가 이 에셋의 값을 읽어 UnitCombatUseCase에
//   float 원시값으로 주입한다. (Application 레이어는 Infrastructure의 SO를 직접
//   참조하지 않는다 — MEMORY 레이어 규칙: UnitStatsConfig→UnitStats.Initialize와 동일 패턴.)
//
// 생성 방법:
//   Project 창에서 우클릭 → Create → Hexiege → SpecialAttackConfig
//   (또는 CreateAssetMenu 경로). 권장 저장 위치는
//   Assets/_Project/Resources/Config/SpecialAttackConfig.asset.
//   에셋을 만든 뒤 GameBootstrapper의 Config 섹션 필드에 연결한다.
//   ※ 에셋을 연결하지 않아도 코드 기본값(1.0 / 120)이 폴백으로 동작한다.
//
// Infrastructure 레이어 — Unity 의존 허용 (ScriptableObject).
// ============================================================================

using UnityEngine;

namespace Hexiege.Infrastructure
{
    // ========================================================================
    // SpecialAttackConfig
    // 특수 공격 튜닝 수치를 담는 ScriptableObject.
    // 향후 QuakeSpirit 착탄 반경 등 다른 특수 유닛 파라미터의 공용 자리로 확장 가능.
    // ========================================================================
    [CreateAssetMenu(fileName = "SpecialAttackConfig", menuName = "Hexiege/SpecialAttackConfig")]
    public class SpecialAttackConfig : ScriptableObject
    {
        // ── 도끼병(BattleAxe) 휩쓸기형 AoE ─────────────────────────────
        [Header("BattleAxe Sweep (휩쓸기)")]

        [Tooltip("휩쓸기 전방 부채꼴 판정의 월드 반경. 공격자로부터의 XZ 평면 거리가 이 값 이하인 적만 대상. " +
                 "인접 타일 중심 간 거리(≈0.9~1.0)를 기준으로 조정. 기본값 1.0")]
        [SerializeField] private float _sweepReach = 1.0f;

        [Tooltip("휩쓸기 전방 부채꼴의 반각(도 단위). forward(주 타깃 방향)와 적이 이루는 각도가 " +
                 "이 값 이하이면 피격. 120이면 전방 240° 범위(등 뒤 60° 제외). 기본값 120")]
        [SerializeField] private float _sweepArcHalfAngle = 120f;

        // ── 물의 상급 정령(TorrentSpirit) 파도형 이동 AoE ─────────────
        [Header("TorrentSpirit Wave (파도)")]

        [Tooltip("파도 가로 폭(월드 단위). 전선이 좌우로 이만큼 훑는다(절반씩 좌우 판정). 기본값 3")]
        [SerializeField] private float _waveWidth = 3f;

        [Tooltip("파도 전방 길이(월드 단위). 전선이 공격자 앞에서 이 거리만큼 전진하며 훑는다. 기본값 3")]
        [SerializeField] private float _waveLength = 3f;

        [Tooltip("파도 전선이 전방 길이를 전부 지나가는 데 걸리는 시간(초). 연출(파티클)과 시각적으로 맞추는 튜닝값. " +
                 "작을수록 파도가 빠르게 훑는다. 기본값 0.5")]
        [SerializeField] private float _waveTravelTime = 0.5f;

        [Tooltip("파도가 아군에 닿을 때의 회복량(HP). 적에게는 대신 attackPower만큼 피해. 기본값 10")]
        [SerializeField] private float _waveHeal = 10f;

        // ── 꽃요정(BloomFairy) 힐러 HoT(지속 회복) ────────────────────
        [Header("BloomFairy Heal (지속 회복 HoT)")]

        [Tooltip("BloomFairy가 부상 아군에게 거는 지속 회복(HoT) 총 회복량(HP). " +
                 "지속시간 동안 서버 틱으로 나눠 회복되며 최종적으로 정확히 이 값에 도달. 기본값 20")]
        [SerializeField] private float _bloomHealAmount = 20f;

        [Tooltip("BloomFairy 지속 회복(HoT)의 지속시간(초). 이 시간 동안 총 회복량이 서서히 적용된다. 기본값 3")]
        [SerializeField] private float _bloomHealDuration = 3f;

        // ── 버섯폭격기(MushroomBomber) 착탄형 범위 DoT ─────────────────
        [Header("MushroomBomber Blast (착탄 DoT)")]

        [Tooltip("착탄 폭발의 월드 반경. 착탄 중심(주 타깃 위치)에서 XZ 평면 거리가 이 값 이하인 " +
                 "적 유닛에게 DoT를 부여한다. 기본값 = '인접 1칸' 타일 거리(≈1.0). 기본값 1.0")]
        [SerializeField] private float _blastRadius = 1.0f;

        [Tooltip("착탄 DoT의 초당 피해량(HP/초). 매초 뚝뚝 들어가며 소수점은 올림 처리된다. 기본값 2")]
        [SerializeField] private float _blastDotPerSecond = 2f;

        [Tooltip("착탄 DoT의 지속시간(초). 총 피해 = 초당 피해 × 지속(예: 2×3 = 6). 기본값 3")]
        [SerializeField] private float _blastDotDuration = 3f;

        // ── 지옥불 정령(InfernoSpirit) 피격 대상 DoT ──────────────────
        [Header("InfernoSpirit DoT (단일 대상 지속 피해)")]

        [Tooltip("InfernoSpirit이 때린 적 유닛 1명에게 거는 DoT의 초당 피해량(HP/초). " +
                 "매초 뚝뚝 들어가며 소수점은 올림 처리된다. 기본값 5")]
        [SerializeField] private float _infernoDotPerSecond = 5f;

        [Tooltip("InfernoSpirit DoT의 지속시간(초). 총 피해 = 초당 피해 × 지속(예: 5×3 = 15). 기본값 3")]
        [SerializeField] private float _infernoDotDuration = 3f;

        /// <summary> 휩쓸기 전방 부채꼴 판정의 월드 반경(XZ 평면 거리 한계). </summary>
        public float SweepReach => _sweepReach;

        /// <summary> 휩쓸기 전방 부채꼴의 반각(도 단위). </summary>
        public float SweepArcHalfAngle => _sweepArcHalfAngle;

        /// <summary> TorrentSpirit 파도 가로 폭(월드 단위). </summary>
        public float WaveWidth => _waveWidth;

        /// <summary> TorrentSpirit 파도 전방 길이(월드 단위). </summary>
        public float WaveLength => _waveLength;

        /// <summary> TorrentSpirit 파도 전선이 전방 길이를 지나가는 시간(초). </summary>
        public float WaveTravelTime => _waveTravelTime;

        /// <summary> TorrentSpirit 파도 아군 회복량(HP). </summary>
        public float WaveHeal => _waveHeal;

        /// <summary> BloomFairy 지속 회복(HoT) 총 회복량(HP). </summary>
        public float BloomHealAmount => _bloomHealAmount;

        /// <summary> BloomFairy 지속 회복(HoT) 지속시간(초). </summary>
        public float BloomHealDuration => _bloomHealDuration;

        /// <summary> MushroomBomber 착탄 폭발 판정의 월드 반경(중심으로부터 XZ 거리 한계). </summary>
        public float BlastRadius => _blastRadius;

        /// <summary> MushroomBomber 착탄 DoT의 초당 피해량(HP/초). </summary>
        public float BlastDotPerSecond => _blastDotPerSecond;

        /// <summary> MushroomBomber 착탄 DoT의 지속시간(초). </summary>
        public float BlastDotDuration => _blastDotDuration;

        /// <summary> InfernoSpirit 단일 대상 DoT의 초당 피해량(HP/초). </summary>
        public float InfernoDotPerSecond => _infernoDotPerSecond;

        /// <summary> InfernoSpirit 단일 대상 DoT의 지속시간(초). </summary>
        public float InfernoDotDuration => _infernoDotDuration;
    }
}
