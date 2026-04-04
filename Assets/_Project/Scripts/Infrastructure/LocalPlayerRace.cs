// ============================================================================
// LocalPlayerRace.cs
// 현재 로컬 플레이어의 종족 정보를 전역으로 접근하기 위한 정적 홀더.
//
// 역할:
//   - 로비 종족 선택 UI에서 종족이 결정되면 여기에 저장
//   - 게임 내 어느 시스템이든 LocalPlayerRace.Current로 로컬 종족 조회 가능
//   - 싱글/멀티플레이 모두 이 값을 사용하여 게임 시작 시 종족 정보 전달
//
// 사용 예시:
//   // 종족 설정 (RaceSelectionViewModel에서 호출)
//   LocalPlayerRace.Set(RaceId.Spirit);
//
//   // 종족 조회 (게임 시작, 유닛 생성 등에서)
//   if (LocalPlayerRace.Current == RaceId.Nature) { ... }
//
// Infrastructure 레이어 — 로비 종족 선택 전역 접근점.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 현재 로컬 플레이어의 종족을 전역에서 접근 가능하게 하는 정적 홀더.
    /// RaceSelectionViewModel에서 종족을 선택하면 Set()을 호출해 갱신.
    /// 기본값: Human (인간).
    /// </summary>
    public static class LocalPlayerRace
    {
        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>현재 로컬 플레이어의 종족. 기본값은 Human(인간).</summary>
        public static RaceId Current { get; private set; } = RaceId.Human;

        /// <summary>로비에서 실제로 종족을 선택했는지 여부.</summary>
        public static bool IsAssigned { get; private set; } = false;

        // ====================================================================
        // API
        // ====================================================================

        /// <summary>
        /// 로컬 플레이어 종족을 설정.
        /// RaceSelectionViewModel에서 종족 변경 시 호출.
        /// </summary>
        /// <param name="race">선택된 종족.</param>
        public static void Set(RaceId race)
        {
            Current = race;
            IsAssigned = true;
        }

        /// <summary>
        /// 종족을 기본값(Human)으로 초기화.
        /// 로비 복귀 또는 연결 해제 시 호출.
        /// </summary>
        public static void Reset()
        {
            Current = RaceId.Human;
            IsAssigned = false;
        }
    }
}
