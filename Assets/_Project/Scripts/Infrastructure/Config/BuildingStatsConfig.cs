// ============================================================================
// BuildingStatsConfig.cs
// 건물 타입별 스탯(HP / 골드 비용 / 공격력)을 종족별로 관리하는 ScriptableObject.
//
// ScriptableObject란?
//   Unity의 데이터 컨테이너. 씬에 부착하지 않고 .asset 파일로 저장.
//   Inspector에서 값을 편집할 수 있어 코드 수정 없이 밸런싱 가능.
//
// 생성 방법:
//   Assets 폴더 우클릭 → Create → Hexiege → BuildingStatsConfig
//   또는 상단 메뉴 Hexiege/Setup/BuildingStatsConfig 생성
//   → Assets/_Project/Resources/Config/BuildingStatsConfig.asset 로 저장.
//
// 설계 패턴:
//   UnitStatsConfig와 동일한 "건물 타입별 종족 값 묶음" 구조를 사용.
//   건물 타입마다 Human/Spirit/Transcendence 3종족의 값을 한 행에 묶음.
//   → Inspector에서 건물 타입 단위로 시각적으로 비교/편집 용이.
//
// 사용 흐름:
//   1. GameBootstrapper의 [SerializeField] _buildingStatsConfig 에 이 에셋 연결
//   2. Start()에서 InitializeBuildingStatsFromConfig() 호출
//   3. 각 entry를 (BuildingType, RaceId) 키의 Dictionary로 변환
//   4. Domain의 BuildingStats.Initialize(dict) 호출
//   5. 이후 BuildingStats.GetMaxHp / GetGoldCost / GetAttackPower 호출 시
//      Dictionary에서 값 조회
//
// Infrastructure 레이어 — Unity 의존 (ScriptableObject).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    // ========================================================================
    // BuildingTypeEntry
    // 한 건물 타입에 대한 종족별 스탯을 묶는 구조체.
    // Inspector의 List에서 한 줄(하나의 건물 타입)에 모든 종족 값이 나란히 노출됨.
    // ========================================================================

    /// <summary>
    /// 건물 타입 하나에 대한 종족별 스탯 묶음.
    /// 한 건물 타입마다 Human / Spirit / Transcendence 3종족의 수치를 한 항목에 모음.
    /// </summary>
    [System.Serializable]
    public struct BuildingTypeEntry
    {
        [Tooltip("대상 건물 타입 (Castle / Barracks / MiningPost)")]
        public BuildingType buildingType;

        [Header("HP")]
        [Tooltip("인간 종족 최대 체력")]
        public int humanMaxHp;

        [Tooltip("정령 종족 최대 체력")]
        public int spiritMaxHp;

        [Tooltip("초월 종족 최대 체력")]
        public int transcendenceMaxHp;

        [Header("Gold Cost")]
        [Tooltip("인간 종족 건설 비용 (골드). Castle은 자동 배치이므로 0.")]
        public int humanGoldCost;

        [Tooltip("정령 종족 건설 비용 (골드)")]
        public int spiritGoldCost;

        [Tooltip("초월 종족 건설 비용 (골드)")]
        public int transcendenceGoldCost;

        [Header("Attack Power (향후 타워 기능 대비)")]
        [Tooltip("인간 종족 공격력. 현재 미사용, 타워 기능 도입 시 사용.")]
        public int humanAttackPower;

        [Tooltip("정령 종족 공격력. 현재 미사용.")]
        public int spiritAttackPower;

        [Tooltip("초월 종족 공격력. 현재 미사용.")]
        public int transcendenceAttackPower;
    }

    // ========================================================================
    // BuildingStatsConfig
    // 전체 건물 스탯 테이블을 담는 ScriptableObject 본체.
    // ========================================================================

    /// <summary>
    /// 모든 건물 타입의 종족별 스탯을 담는 ScriptableObject.
    /// GameBootstrapper가 이 에셋을 읽어 Domain의 BuildingStats에 주입한다.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingStatsConfig", menuName = "Hexiege/BuildingStatsConfig")]
    public class BuildingStatsConfig : ScriptableObject
    {
        [Tooltip("건물 타입별 종족별 스탯 리스트. 각 항목은 하나의 건물 타입 = 3종족 값 묶음.")]
        [SerializeField] private List<BuildingTypeEntry> _stats = new List<BuildingTypeEntry>();

        /// <summary>
        /// 읽기 전용 리스트 반환.
        /// GameBootstrapper가 순회하며 Dictionary로 변환할 때 사용.
        /// </summary>
        public IReadOnlyList<BuildingTypeEntry> Stats => _stats;
    }
}
