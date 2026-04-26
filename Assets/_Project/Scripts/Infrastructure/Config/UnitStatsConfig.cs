// ============================================================================
// UnitStatsConfig.cs
// 유닛 전투/생산 수치를 Unity Inspector에서 편집 가능하게 만드는 ScriptableObject.
//
// ScriptableObject란?
//   Unity의 데이터 컨테이너 에셋. 씬에 붙이지 않고 .asset 파일로 저장되며,
//   Inspector에서 값을 편집할 수 있다. 코드(재컴파일) 없이 수치를 조정 가능하므로
//   밸런싱 작업에 유용하다.
//
// 이 에셋의 역할:
//   과거 UnitStats.cs / UnitProductionStats.cs 내부에 switch 표현식으로 하드코딩돼
//   있던 모든 수치를 이 에셋 하나로 옮겨둔다.
//   게임 시작 시 GameBootstrapper가 이 에셋을 읽어서 UnitStats / UnitProductionStats
//   정적 Dictionary로 주입(Initialize)한다.
//   이후 UnitData / UnitSpawnUseCase 등에서 UnitStats.GetMaxHp(...) 같은 기존
//   API를 호출하면 이 에셋의 값이 그대로 반환된다.
//
// 생성 방법:
//   메뉴 `Hexiege/Setup/UnitStatsConfig 생성` 실행 시
//   `Assets/_Project/Resources/Config/UnitStatsConfig.asset`로 자동 생성되며
//   현재 코드 기준 스탯값이 기본 입력된다.
//
// Infrastructure 레이어 — Unity 의존 허용 (ScriptableObject).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    // ========================================================================
    // UnitStatEntry
    // 한 유닛(UnitType) 하나에 대한 모든 수치를 묶은 구조체.
    // List<UnitStatEntry> 형태로 UnitStatsConfig에 보관되며 Inspector에서 편집.
    //
    // [System.Serializable] 속성이 있어야 Inspector에 노출됨.
    // ========================================================================
    [System.Serializable]
    public struct UnitStatEntry
    {
        [Tooltip("이 항목이 적용되는 유닛 타입")]
        public UnitType unitType;

        // ── 전투 스탯 ──────────────────────────────────────────────
        [Header("Combat")]

        [Tooltip("최대 체력")]
        public int maxHp;

        [Tooltip("공격력 (다중 히트 유닛은 히트당 데미지)")]
        public int attackPower;

        [Tooltip("공격 사거리 (타일 단위). 0.5 = 근접 / 1.0 이상 = 원거리")]
        public float attackRange;

        [Tooltip("적 감지 사거리 (타일 단위). 공격 사거리 이상이어야 함")]
        public float detectRange;

        [Tooltip("이동 속도 (칸/초). 높을수록 빠름")]
        public float moveSpeed;

        [Tooltip("Animator 클립이 있으면 UnitFactory에서 런타임에 덮어씌워짐. 클립 없을 때 fallback")]
        public float attackCooldown;

        [Tooltip("공격 애니 내 타격 프레임 시간(초) 배열. 다중 히트는 오름차순")]
        public float[] hitFrameTimes;

        // ── 점유 정보 ──────────────────────────────────────────────
        // 한 타일의 최대 점유 합계는 3. 소형(1) / 중형(2) / 대형(3) 기준.
        // TileOccupancyManager가 "다음 타일에 들어갈 수 있는가"를 판단할 때만 사용.
        [Tooltip("타일당 점유 크기. 소형=1 / 중형=2 / 대형=3. 한 타일 최대 합계=3")]
        public float occupancySize;

        // ── 생산 스탯 ──────────────────────────────────────────────
        [Header("Production")]

        [Tooltip("생산에 걸리는 시간(초)")]
        public float productionTime;

        [Tooltip("생산 골드 비용")]
        public int goldCost;

        [Tooltip("생산 인구 비용")]
        public int populationCost;
    }

    // ========================================================================
    // UnitStatsConfig
    // 모든 UnitType의 UnitStatEntry를 한곳에 모으는 ScriptableObject.
    // 에셋 경로(권장): Assets/_Project/Resources/Config/UnitStatsConfig.asset
    // ========================================================================
    [CreateAssetMenu(fileName = "UnitStatsConfig", menuName = "Hexiege/UnitStatsConfig")]
    public class UnitStatsConfig : ScriptableObject
    {
        [Tooltip("각 유닛 타입별 전투/생산 수치. Inspector에서 편집")]
        [SerializeField] private List<UnitStatEntry> _stats = new List<UnitStatEntry>();

        /// <summary> 외부에서 읽기 전용으로 항목 목록에 접근할 때 사용. </summary>
        public IReadOnlyList<UnitStatEntry> Stats => _stats;
    }
}
