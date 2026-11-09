// ============================================================================
// TowerFrontArc.cs
// 인간 방어탑(CannonTower) "전방 120° 사격 범위"의 정의를 한곳에 모은 정적 도우미.
// 2026-11-09 Task `_Tasks/2026-11-09/22_19_cannon-tower-front-arc/` 커밋 1(판정)에서 새로 만들었다.
//
// 무엇을 하나(유니티 초급자용 설명):
//   인간 대포탑 모델은 배치될 때 상대 진영 쪽(정면)을 향해 고정되고, 그 뒤로는 돌지 않는다.
//   그래서 적이 탑 뒤에 서 있어도 쏘면 포탄이 포신 끝에서 나와 뒤로 날아가는 어색한 장면이 생겼다.
//   이 파일은 "탑의 정면은 어느 쪽인가", "정면에서 좌우 몇 도까지를 앞으로 보는가",
//   "이 적은 앞쪽에 있는가"를 계산하는 규칙만 담는다.
//     · 판정(서버)  : TowerCombatUseCase.FindNearestEnemyUnit 이 후보 유닛마다 IsInFrontArc 를 묻는다.
//     · 화면(표시)  : 탑을 눌렀을 때 그려지는 범위 표시(TowerRangeIndicator)가 같은 각도 · 정면을 읽는다.
//   두 곳이 같은 상수를 쓰므로 "보이는 부채꼴"과 "실제로 쏘는 범위"가 조용히 어긋나지 않는다.
//
// 사용자 결정(2026-11-09, Plan 7절 Q1~Q6):
//   Q1 적용 = Human CannonTower(BuildingType.AutoTower + RaceId.Human)만. RuneSpire · VineTower 는 360° 그대로.
//   Q2 기준점 = 탑 중심(루트)의 수평(XZ) 위치 — 사거리와 같은 점. 높이(Y)는 무시한다.
//   Q3 정면 = 팀 진영 고정. 도메인 좌표로 Blue 탑 = +Z, Red 탑 = −Z
//            (ProjectileSettings.ResolveTowerMuzzle 의 부호 규칙과 같다 — Blue 오프셋 그대로, Red x·z 부호 반전).
//   Q4 경계 = 60° 포함 + 작은 허용 오차. 헥스 대각 앞 이웃이 60.0007°에 떨어지기 때문이다(아래 MinFrontDot 설명).
//
// 왜 ProjectileDeliveryContracts.cs(ProjectileSettings 옆)가 아니라 새 파일인가(Plan 2절 #2와의 차이):
//   이 규칙은 투사체 전달 계약이 아니라 "표적 선택"과 "화면 범위 표시" 두 곳이 함께 읽는 규칙이다.
//   투사체 파일에 두면 범위 표시(Presentation)가 투사체 계약 파일에 기대게 되어 관심사가 섞인다.
//   ResolveTowerMuzzle 은 그대로 두고 바꾸지 않는다(R6 — 바꾸는 것은 범위 확장이라 사용자 확인 사항).
//
// Application 레이어 — Domain(BuildingType · RaceId · TeamId)과 UnityEngine.Vector3 · Mathf 만 쓴다.
//   Core · Infrastructure · Netcode 참조 없음. 상태가 없는 순수 계산이라 어디서 불러도 안전하다.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 인간 방어탑(CannonTower) 전방 사격 범위(정면 좌우 60°, 합 120°)의 정의와 판정식.
    /// 서버 표적 선택과 화면 범위 표시가 함께 읽는 단일 원본이다.
    /// </summary>
    public static class TowerFrontArc
    {
        /// <summary>
        /// 정면에서 한쪽으로 허용하는 각도(도). 좌우 합 120°의 절반.
        /// (사용자 결정 Q8 = 코드 상수. 튜닝이 필요해지면 BuildingStatsConfig 칸으로 옮기는 것은 후속 결정이다.)
        /// </summary>
        public const float HalfAngleDeg = 60f;

        /// <summary>
        /// 경계 허용 오차(코사인 값 기준). cos 60° = 0.5 에서 이만큼 낮춘 값까지 "앞"으로 본다 — 약 60.07°.
        /// </summary>
        public const float CosTolerance = 0.001f;

        /// <summary>
        /// "앞"으로 인정하는 최소 내적값 = cos 60° − 허용 오차 = 0.499.
        ///
        /// [왜 허용 오차가 필요한가 — 유니티 초급자용]
        ///   실제 경기 격자(FlatTop, GameConfig 타일 높이 0.866)에서 탑의 대각 앞 이웃 타일까지의 방향은
        ///   (0.75, 0.433)이다. 이 방향과 정면(0, 1)의 내적은 0.433 / 0.866019 = 0.499989 로, 0.5 보다 아주 조금 작다
        ///   (= 60.0007°). 타일 높이가 √3/2(0.8660254)가 아니라 0.866 으로 반올림돼 있어서 생기는 차이다.
        ///   허용 오차 없이 "≥ 0.5"로 비교하면 북동 · 북서 대각선 위의 타일이 전부 "뒤"로 빠진다(Research 2-1).
        ///   0.499 까지 받으면 60.0007° 는 들어오고 61° (0.4848)는 빠진다.
        /// </summary>
        public const float MinFrontDot = 0.5f - CosTolerance;

        /// <summary>
        /// 유닛이 탑 중심과 거의 겹쳐 방향을 정할 수 없다고 보는 수평 거리의 제곱(월드 m²). 0.001 m 미만.
        /// 이 경우는 "앞"으로 본다(구현 때 정함 — Plan 3-1 「거리가 0에 가까운 경우」: 방향이 없으니 막을 근거도 없다).
        /// </summary>
        public const float DegenerateDistanceSqr = 1e-6f;

        /// <summary>
        /// 이 탑이 전방 사격 범위 제한을 받는지(사용자 결정 Q1 — Human CannonTower만).
        /// </summary>
        /// <param name="type">건물 종류.</param>
        /// <param name="race">건물(소유 팀)의 종족.</param>
        /// <returns>AutoTower 이면서 Human 이면 true. 그 밖(RuneSpire · VineTower · 다른 건물)은 false = 360°.</returns>
        public static bool AppliesTo(BuildingType type, RaceId race)
        {
            return type == BuildingType.AutoTower && race == RaceId.Human;
        }

        /// <summary>
        /// 탑 정면의 수평 단위벡터(도메인 좌표, 사용자 결정 Q3). Blue = (0, 0, +1), Red = (0, 0, −1).
        /// 화면 좌표로 바꿀 때는 호출자가 ViewConverter 로 변환한다(Red 화면은 맵이 뒤집혀 있어 Red 탑도 화면 위쪽을 향한다).
        /// </summary>
        /// <param name="team">탑 소유 팀.</param>
        /// <returns>도메인 정면 벡터(Y = 0, 길이 1).</returns>
        public static Vector3 ResolveDomainForward(TeamId team)
        {
            // ResolveTowerMuzzle 과 같은 규칙: Red 만 부호를 뒤집는다(그 밖은 Blue 와 같다).
            return team == TeamId.Red ? new Vector3(0f, 0f, -1f) : new Vector3(0f, 0f, 1f);
        }

        /// <summary>
        /// 대상이 탑의 전방 사격 범위(정면 좌우 60° + 허용 오차) 안에 있는지 판정한다. 두 위치 모두 도메인 좌표여야 한다.
        /// 높이(Y)는 무시하고 수평(XZ) 방향만 본다(사용자 결정 Q2).
        /// </summary>
        /// <param name="towerDomainPos">탑 중심(루트) 위치 — 도메인 월드 좌표.</param>
        /// <param name="targetDomainPos">대상 유닛 위치 — 도메인 월드 좌표.</param>
        /// <param name="team">탑 소유 팀(정면 방향을 정한다).</param>
        /// <param name="frontDot">정면과 대상 방향의 내적(−1 ~ 1). 겹친 경우 1. 진단 로그의 각도 계산에 쓴다.</param>
        /// <returns>범위 안이면 true.</returns>
        public static bool IsInFrontArc(Vector3 towerDomainPos, Vector3 targetDomainPos, TeamId team, out float frontDot)
        {
            float dx = targetDomainPos.x - towerDomainPos.x;
            float dz = targetDomainPos.z - towerDomainPos.z;
            float lengthSqr = dx * dx + dz * dz;

            // 대상이 탑 중심과 겹쳐 방향이 없으면 앞으로 본다(DegenerateDistanceSqr 설명).
            if (lengthSqr < DegenerateDistanceSqr)
            {
                frontDot = 1f;
                return true;
            }

            // 정면 벡터는 (0, 0, ±1)이라 내적 = dz × 부호. 일반식으로 써 두어 정면 정의가 바뀌어도 이 식은 그대로 맞다.
            Vector3 forward = ResolveDomainForward(team);
            frontDot = (dx * forward.x + dz * forward.z) / Mathf.Sqrt(lengthSqr);
            return frontDot >= MinFrontDot;
        }

        /// <summary>
        /// 내적값을 정면과의 사이각(도, 0 ~ 180)으로 바꾼다. 진단 로그 표시용(판정에는 쓰지 않는다 — 판정은 내적으로만 한다).
        /// </summary>
        /// <param name="frontDot">IsInFrontArc 가 돌려준 내적값.</param>
        /// <returns>사이각(도).</returns>
        public static float DotToAngleDeg(float frontDot)
        {
            return Mathf.Acos(Mathf.Clamp(frontDot, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
