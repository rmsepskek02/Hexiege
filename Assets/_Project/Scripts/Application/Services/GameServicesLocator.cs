// ============================================================================
// GameServicesLocator.cs
// IGameServices를 전역에서 접근 가능하게 하는 정적 서비스 로케이터.
//
// 왜 이게 필요한가?
//   Unity의 FindFirstObjectByType<T>()는 인터페이스 타입(T = 인터페이스)을 지원하지 않는다.
//   따라서 Infrastructure 파일들이 GameBootstrapper를 직접 찾지 않고도
//   IGameServices에 접근하려면 "등록·조회" 창구가 별도로 필요하다.
//
//   사용 방법:
//     - Bootstrap: GameBootstrapper.Awake()에서 Register(this) 호출.
//     - Bootstrap: GameBootstrapper.OnDestroy()에서 Unregister() 호출.
//     - Infrastructure: GameServicesLocator.Current로 IGameServices 접근.
//
//   이 클래스는 Application 계층에 위치하므로
//   Bootstrap(등록자)과 Infrastructure(사용자) 양쪽 모두 참조 가능하다.
//
// Application 레이어 — Unity 의존 없음(순수 C#).
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// IGameServices의 단일 전역 접근 창구.
    /// GameBootstrapper가 자신을 등록하고, Infrastructure가 꺼내 쓴다.
    /// </summary>
    public static class GameServicesLocator
    {
        /// <summary>
        /// 현재 등록된 IGameServices 인스턴스.
        /// GameBootstrapper.Awake() 이전에 접근하면 null.
        /// 씬 언로드 후 GameBootstrapper.OnDestroy()가 호출되면 다시 null이 된다.
        /// </summary>
        public static IGameServices Current { get; private set; }

        /// <summary>
        /// IGameServices 구현체(GameBootstrapper)를 등록한다.
        /// GameBootstrapper.Awake()에서 1회 호출.
        /// </summary>
        public static void Register(IGameServices services)
        {
            Current = services;
        }

        /// <summary>
        /// 등록된 IGameServices를 해제한다.
        /// GameBootstrapper.OnDestroy()에서 호출하여 씬 전환 후 stale 참조를 방지한다.
        /// </summary>
        public static void Unregister()
        {
            Current = null;
        }
    }
}
