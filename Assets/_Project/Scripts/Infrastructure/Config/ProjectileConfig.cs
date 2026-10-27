// ============================================================================
// ProjectileConfig.cs
// 서버 권위 투사체(옵션 C)의 "서버 판정용" 수치를 Inspector에서 편집하는 ScriptableObject.
//
// 담는 값 (모두 서버만 읽는다 — Client 화면은 서버가 보낸 발사/착탄 시각만 쓴다):
//   · 유닛 종류별 투사체 속도(월드 미터/초) — 2026-10-20 사용자 결정 Q2 「Inspector에서 조절, 서버 권위 데이터」.
//       초기값: StreamSpirit 4 · Tank 4 · CannonCart 4 · InfernoSpirit 5 · FoxMagician 5 · EagleArcher 6 · MushroomBomber 5.
//   · 인간 방어타워 투사체 속도(초기 4) — 2026-10-21 단계 C(인간 타워 전환)부터 실제로 쓴다.
//   · 인간 방어타워 포구 위치(발사 원점, 단계 C) — 타워 루트 기준 "타워 정면" 좌표계 오프셋.
//       서버 발사 위치와 화면 발사 이펙트·투사체 출발점이 모두 이 한 값을 쓴다(사용자 결정 「대포 입구에서 발사」).
//       초기값 (0, 0.405, 0.17)은 CannonTower 메시 포신 끝을 측정한 값이다(ProjectileSettings.DefaultHumanTowerMuzzleOffset 주석).
//   · 대상 반경 표 — 투사체가 "대상 표면"에 닿는 순간을 계산할 때 비행 거리에서 빼는 값(Q12).
//       유닛: UnitEffectConfig._recipientFits의 radius(측정 도구 값)를 복제해 둔 표(RM-12 — Presentation 값을 서버가 직접 읽지 않으려고 옮김).
//       건물: 종류별 반경을 새로 둔 표. 초기값은 모두 같은 임시값이며 실기에서 튜닝한다(측정 근거 없음 — 미확인).
//
// 왜 UnitStatsConfig가 아니라 별도 에셋인가:
//   속도·유닛 반경·건물 반경·(다음 단계의) 타워 값이 모두 "투사체 비행 계산" 한 가지 용도라 한곳에 모았다.
//   UnitStatsConfig에 넣으면 Domain UnitStats 주입 구조(StatValues)까지 바꿔야 하고, 건물 값은 또 다른 에셋에 흩어진다.
//   (Plan 3절 초안의 「UnitStatsConfig 속도 필드」와 다른 선택 — 보고서에 이유를 적었다.)
//
// 로드 방식:
//   GameBootstrapper의 Inspector 슬롯(_projectileConfig)에 연결하면 그 값을 쓰고,
//   비어 있으면 Resources/Config/ProjectileConfig.asset을 Resources.Load로 찾는다(AIConfig와 같은 폴백 선례).
//   둘 다 없으면 코드 기본값(ProjectileSettings.CreateDefault)으로 동작한다.
//
// Infrastructure 레이어 — Unity 의존 허용(ScriptableObject).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 서버 권위 투사체 수치(속도·대상 반경) 에셋. 서버가 비행 시간(착탄 시각)을 계산할 때만 읽는다.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileConfig", menuName = "Hexiege/ProjectileConfig")]
    public class ProjectileConfig : ScriptableObject
    {
        /// <summary>유닛 종류별 투사체 속도 한 줄.</summary>
        [System.Serializable]
        public struct UnitSpeedEntry
        {
            [Tooltip("이 속도가 적용되는 투사체 유닛 종류")]
            public UnitType unitType;

            [Tooltip("투사체 비행 속도(월드 미터/초). 눈으로 보이는 속도로 튜닝한다. 0 이하면 코드 안전값(4)을 쓴다")]
            public float speed;
        }

        /// <summary>유닛 종류별 대상 반경 한 줄.</summary>
        [System.Serializable]
        public struct UnitRadiusEntry
        {
            [Tooltip("이 반경이 적용되는 유닛 종류(이 유닛이 투사체에 '맞을 때')")]
            public UnitType unitType;

            [Tooltip("몸통 반경(월드 미터). 서버가 비행 거리에서 빼고, 착탄 이펙트도 이 표면 지점에 낸다")]
            public float radius;
        }

        /// <summary>건물 종류별 대상 반경 한 줄.</summary>
        [System.Serializable]
        public struct BuildingRadiusEntry
        {
            [Tooltip("이 반경이 적용되는 건물 종류")]
            public BuildingType buildingType;

            [Tooltip("건물 반경(월드 미터). 큰 건물일수록 크게 — 투사체가 건물 표면에 닿는 순간 피해가 들어간다")]
            public float radius;
        }

        [Header("투사체 속도 (서버 판정 전용)")]
        [Tooltip("유닛 종류별 투사체 속도. 표에 없는 유닛은 코드 안전값(4)을 쓴다")]
        [SerializeField] private List<UnitSpeedEntry> _unitSpeeds = new List<UnitSpeedEntry>();

        [Tooltip("인간 방어타워(CannonTower) 투사체 속도(월드 미터/초). 0 이하면 코드 안전값(4)을 쓴다")]
        [SerializeField] private float _humanTowerSpeed = 4f;

        [Header("인간 방어타워 발사 원점 (서버 판정 + 화면 공용)")]
        [Tooltip("CannonTower 포구(대포 입구) 위치 — 타워 루트 기준, x=타워 오른쪽 · y=위 · z=대포가 향한 앞쪽(월드 미터). "
            + "Red 타워는 180° 돌아 놓이므로 코드가 x·z 부호를 뒤집는다. 서버 발사 위치와 화면 발사 이펙트가 같은 값을 쓴다")]
        [SerializeField] private Vector3 _humanTowerMuzzleOffset = new Vector3(0f, 0.405f, 0.17f);

        [Header("대상 반경 (서버 판정 전용 — 표면 착탄)")]
        [Tooltip("유닛 종류별 몸통 반경. UnitEffectConfig 크기 표(radius)를 복제한 값이다")]
        [SerializeField] private List<UnitRadiusEntry> _unitRadii = new List<UnitRadiusEntry>();

        [Tooltip("표에 없는 유닛의 반경(월드 미터)")]
        [SerializeField] private float _defaultUnitRadius = ProjectileSettings.FallbackUnitRadius;

        [Tooltip("건물 종류별 반경. 초기값은 모두 같은 임시값 — 실기에서 튜닝")]
        [SerializeField] private List<BuildingRadiusEntry> _buildingRadii = new List<BuildingRadiusEntry>();

        [Tooltip("표에 없는 건물의 반경(월드 미터)")]
        [SerializeField] private float _defaultBuildingRadius = ProjectileSettings.FallbackBuildingRadius;

        /// <summary>유닛 속도 표(읽기 전용 — 검증용).</summary>
        public IReadOnlyList<UnitSpeedEntry> UnitSpeeds => _unitSpeeds;

        /// <summary>유닛 반경 표(읽기 전용 — 검증용).</summary>
        public IReadOnlyList<UnitRadiusEntry> UnitRadii => _unitRadii;

        /// <summary>건물 반경 표(읽기 전용 — 검증용).</summary>
        public IReadOnlyList<BuildingRadiusEntry> BuildingRadii => _buildingRadii;

        /// <summary>[단계 C] 인간 방어타워 포구 위치(읽기 전용 — 검증용).</summary>
        public Vector3 HumanTowerMuzzleOffset => _humanTowerMuzzleOffset;

        /// <summary>
        /// Inspector 값을 Application 순수 데이터(ProjectileSettings)로 바꾼다. 조합 루트가 UseCase에 주입한다.
        /// 표에 같은 종류가 두 번 있으면 뒤의 값이 이긴다. 잘못된 값(0 이하 속도 등)은 ProjectileSettings가 무시한다.
        /// </summary>
        /// <returns>서버 비행 계산용 값 묶음.</returns>
        public ProjectileSettings ToSettings()
        {
            var settings = new ProjectileSettings(_defaultUnitRadius, _defaultBuildingRadius, _humanTowerSpeed,
                _humanTowerMuzzleOffset);
            // 표가 비어 있어도 결정된 초기 속도는 유지되도록 코드 기본값을 먼저 깔고 Inspector 값으로 덮는다.
            ProjectileSettings defaults = ProjectileSettings.CreateDefault();
            foreach (UnitType type in new[] { UnitType.StreamSpirit, UnitType.Tank, UnitType.CannonCart,
                         UnitType.InfernoSpirit, UnitType.FoxMagician, UnitType.EagleArcher, UnitType.MushroomBomber })
                settings.SetUnitSpeed(type, defaults.GetUnitSpeed(type));
            if (_unitSpeeds != null)
                foreach (UnitSpeedEntry entry in _unitSpeeds) settings.SetUnitSpeed(entry.unitType, entry.speed);
            if (_unitRadii != null)
                foreach (UnitRadiusEntry entry in _unitRadii) settings.SetUnitRadius(entry.unitType, entry.radius);
            if (_buildingRadii != null)
                foreach (BuildingRadiusEntry entry in _buildingRadii) settings.SetBuildingRadius(entry.buildingType, entry.radius);
            return settings;
        }
    }
}
