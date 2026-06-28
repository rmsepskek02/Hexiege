namespace Hexiege.Presentation
{
    /// <summary>
    /// 씬 전환을 담당하는 정적 유틸리티.
    /// 모든 씬 전환은 이 클래스를 통해서만 수행하여 로딩 인디케이터가 자동으로 표시되도록 한다.
    ///
    /// 직접 UnityEngine.SceneManagement.SceneManager.LoadScene 을 호출하면
    /// 로딩 인디케이터를 매번 수동으로 켜야 하고, 빠뜨리면 사용자가 멈춘 화면을 보게 된다.
    /// 이 클래스를 사용하면 ShowLoading(true) 호출이 항상 함께 일어나므로 누락을 방지한다.
    ///
    /// 주의: 멀티플레이 씬 전환(NetworkManager.Singleton.SceneManager.LoadScene)은
    /// NGO(Netcode for GameObjects)가 별도로 관리하므로 이 클래스를 거치지 않는다.
    /// </summary>
    public static class SceneLoader
    {
        // 씬 이름 상수 — 문자열 오타로 인한 씬 로드 실패를 방지한다.
        public const string Login = "Login";
        public const string Lobby = "Lobby";
        public const string Game = "Game";

        /// <summary>
        /// 씬 전환 시 로딩 인디케이터를 표시하고 씬을 로드한다.
        /// message 를 지정하지 않으면 씬 이름에 따라 기본 메시지가 사용된다.
        ///
        /// 로딩 인디케이터를 끄는 책임은 목적지 씬의 초기화 완료 시점이 담당한다(UI 규칙 L-3).
        /// 예) LoginBootstrapper.ShowLoginSelect(), LobbyRootView.Start(), GameBootstrapper 맵 로드 완료.
        /// </summary>
        /// <param name="sceneName">로드할 씬 이름. 상수(Login/Lobby/Game) 사용 권장.</param>
        /// <param name="message">로딩 인디케이터에 표시할 메시지. null 이면 씬별 기본 메시지 사용.</param>
        public static void Load(string sceneName, string message = null)
        {
            // 메시지가 지정되지 않았으면 씬에 맞는 기본 안내 문구를 사용한다.
            string msg = message ?? GetDefaultMessage(sceneName);

            // UIManager 가 있으면 로딩 인디케이터 표시 → 1초 대기 → 씬 전환을 코루틴으로 처리한다.
            //   ShowLoading(true) 직후 바로 LoadScene을 호출하면 로딩 화면이 그려지기도 전에
            //   씬이 전환되어 사용자가 로딩을 인식하지 못한다. 그래서 코루틴 대기가 필요하다.
            //   (SceneLoader는 정적 클래스라 코루틴을 직접 실행할 수 없으므로 UIManager에 위임한다.)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.LoadSceneWithDelay(sceneName, msg);
                return;
            }

            // UIManager 가 아직 생성되지 않은 상황(씬 직접 진입 등)을 대비한 fallback —
            // 로딩 인디케이터 없이 곧바로 씬을 전환한다(UI 규칙 L-4).
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// 씬 이름에 대응하는 기본 로딩 메시지를 반환한다.
        /// 알 수 없는 씬 이름이면 범용 "로딩 중..." 문구를 사용한다.
        /// </summary>
        private static string GetDefaultMessage(string sceneName) => sceneName switch
        {
            Login => "로그인 화면으로 이동 중...",
            Lobby => "로비로 이동 중...",
            Game  => "게임을 불러오는 중...",
            _     => "로딩 중...",
        };
    }
}
