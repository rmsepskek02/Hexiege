// ============================================================================
// NetworkContext.cs
// 네트워크 모드 여부를 Application 레이어에서 Unity.Netcode 없이 참조하기 위한 정적 홀더.
//
// 역할:
//   - Infrastructure 레이어(NetworkCombatController)가 게임 시작 시 IsNetworkServer를 설정
//   - Application 레이어(UnitCombatUseCase)가 이 값을 읽어 서버 전용 분기 처리
//
// 패턴:
//   HexOrientationContext와 동일한 패턴 — 외부 레이어가 내부 레이어에 값을 주입하는 방식.
//   Domain / Application 레이어는 Unity나 Netcode에 의존하지 않음.
//
// Application 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 네트워크 서버 여부를 전역으로 참조하기 위한 정적 홀더.
    /// Infrastructure 레이어가 NetworkBehaviour.OnNetworkSpawn에서 설정.
    /// Application 레이어의 UseCase가 멀티플레이 서버 분기 처리에 사용.
    /// </summary>
    public static class NetworkContext
    {
        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>
        /// 현재 실행이 네트워크 서버(Host 포함)인지 여부.
        /// 싱글플레이: false (기본값)
        /// Host/Server: true
        /// Pure Client: false
        /// </summary>
        public static bool IsNetworkServer { get; private set; } = false;

        /// <summary>
        /// 현재 네트워크 세션이 활성화되어 있는지 여부.
        /// 멀티플레이 중(Host 또는 Client)이면 true.
        /// 싱글플레이: false (기본값)
        /// </summary>
        public static bool IsNetworkActive { get; private set; } = false;

        // ====================================================================
        // API
        // ====================================================================

        /// <summary>
        /// 네트워크 상태를 설정.
        /// NetworkCombatController.OnNetworkSpawn()에서 호출.
        /// </summary>
        /// <param name="isServer">서버(Host 포함)이면 true</param>
        /// <param name="isActive">네트워크 세션이 활성화되어 있으면 true</param>
        public static void Set(bool isServer, bool isActive)
        {
            IsNetworkServer = isServer;
            IsNetworkActive = isActive;
        }

        /// <summary>
        /// 싱글플레이 기본값으로 초기화.
        /// 씬 전환 또는 연결 해제 시 호출.
        /// </summary>
        public static void Reset()
        {
            IsNetworkServer = false;
            IsNetworkActive = false;
        }
    }
}
