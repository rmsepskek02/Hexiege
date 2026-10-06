// ============================================================================
// NetworkStatusUI.cs
// 네트워크 상태(Ping/RTT) 표시 및 연결 끊김 처리 UI.
//
// 역할:
//   1. 매 0.5초마다 NetworkGameManager.GetCurrentRttMs()로 RTT(ms) 읽어 텍스트 갱신
//   2. 싱글플레이/미연결 시 패널 전체 비활성화
//   3. 연결 끊김 감지:
//      - 서버: 상대방(클라이언트)이 나갔을 때 ReconnectionHandler에 위임 (이 UI는 발행 안 함)
//      - 클라이언트: 서버 연결이 끊겼을 때 NetworkGameManager.OnServerDisconnected 수신 → 팝업 표시
//      - 🔴 단, **경기가 끝나 결과 화면이 떠 있는 동안에는 이 팝업을 띄우지 않는다**
//        (공통 UI 규칙 D-1 — 아래 OnServerDisconnected 주석이 이유를 설명한다)
//
// 🔴 씬 배치 — 어느 씬·프리팹에도 없다 (실측):
//   이 스크립트의 guid 로 Assets 전체의 *.unity · *.prefab 을 검색하면 0건이다.
//   즉 코드는 있으나 어떤 GameObject 에도 붙어 있지 않아 Start() 부터 실행되지 않으며,
//   핑 표시도 아래 연결 끊김 팝업도 지금은 화면에 뜨지 않는다.
//   🔴 이 사실의 단일 소스는 Docs/TechnicalDesignDocument.md 「결과 화면 이탈 판정·통보 구조」 절의
//      2026-09-22 블록이고, 현황 표기는 Docs/PROJECT_STATUS.md 의 멀티플레이 Phase 표가 갖는다.
//      그 내용을 여기에 옮겨 적지 않는다(사본은 원본이 바뀌는 순간 거짓이 된다).
//
//   나중에 붙일 때 직렬화 필드가 요구하는 구성:
//     - 상태 패널 루트(_networkStatusPanel) + 그 안의 핑 수치 텍스트(_pingText)
//     - 연결 끊김 패널(_disconnectPanel) + 그 안의 복귀 버튼(_disconnectReturnButton)
//     ⚠️ 끊김 안내 문구용 직렬화 필드는 없다 — 문구는 이 스크립트가 쓰지 않으므로
//        패널 쪽 텍스트로 고정해 두어야 한다.
//
// 주의:
//   - NetworkBehaviour 불필요: 로컬 표시 전용 (Presentation 레이어)
//   - Unity.Netcode / UTP 직접 의존 없음 — NetworkGameManager API로 통일
//   - 씬 전환: SceneLoader.Load 사용 (로딩 인디케이터 자동 표시)
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 네트워크 상태 표시 및 연결 끊김 처리 UI 컴포넌트.
    /// 멀티플레이 미연결 시 자동으로 비활성화됨.
    /// </summary>
    public class NetworkStatusUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("Ping 표시")]
        [Tooltip("네트워크 상태 패널 루트 (멀티플레이에서만 표시)")]
        [SerializeField] private GameObject _networkStatusPanel;

        [Tooltip("Ping(RTT) 수치 텍스트 (예: '42ms')")]
        [SerializeField] private TextMeshProUGUI _pingText;

        [Header("연결 끊김 팝업")]
        [Tooltip("연결 끊김 알림 패널")]
        [SerializeField] private GameObject _disconnectPanel;

        [Tooltip("로비/메뉴로 돌아가기 버튼")]
        [SerializeField] private Button _disconnectReturnButton;

        [Header("설정")]
        [Tooltip("Ping 갱신 간격 (초)")]
        [SerializeField] private float _pingRefreshInterval = 0.5f;

        // TODO: 재접속(Reconnection) 기능 구현 시, 복구 가능 여부에 따라
        //       게임 씬(SceneLoader.Game) 또는 로비 씬(SceneLoader.Lobby)으로 분기 처리 필요.
        //       현재는 항상 로비로 복귀. ROADMAP B-2 참조.
        [Tooltip("연결 끊김 후 복귀할 씬 이름")]
        [SerializeField] private string _returnSceneName = SceneLoader.Lobby;

        // NetworkGameManager 주입 — NGM의 GetCurrentRttMs / OnServerDisconnected /
        //   IsNetworkRunning / ShutdownNetwork를 사용한다(NetworkManager.Singleton 직접 참조 회피).
        [Header("의존성")]
        [Tooltip("네트워크 게임 매니저. RTT 조회/연결 끊김 알림/Shutdown 위임용. Inspector 미연결 시 자동 탐색.")]
        [SerializeField] private NetworkGameManager _networkGameManager;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> Ping 갱신 코루틴 참조. 중단 시 사용. </summary>
        private Coroutine _pingCoroutine;

        /// <summary>
        /// 연결 끊김 알림을 이미 한 번 처리했는지 여부 (중복 처리 방지).
        /// ⚠️ "팝업을 띄웠는가"가 아니다 — 결과 화면이라 팝업을 일부러 건너뛴 경우에도
        ///    이 값은 true 가 된다. 즉 true 라고 해서 화면에 팝업이 떠 있다는 뜻은 아니다.
        /// </summary>
        private bool _disconnectHandled;

        /// <summary>
        /// 경기 종료 이벤트를 받은 뒤인지 여부(= 결과 화면이 뜰 시점을 지났는지).
        /// true 이면 연결 끊김 팝업을 띄우지 않는다 — 이유는 OnServerDisconnected 주석 참조.
        ///
        /// ⚠️ 결과 화면 자체의 표시 상태를 들여다보는 값이 아니다. 종료 이벤트를 받으면 켜지고
        ///    다시 꺼지는 자리가 없으므로, 한 번 끝난 뒤에는 이 컴포넌트가 사는 동안 계속 true 다.
        /// </summary>
        private bool _resultScreenShown;

        /// <summary> 게임 종료 이벤트 구독 해제용. </summary>
        private System.IDisposable _gameEndSubscription;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Start()
        {
            // 초기화: 팝업은 항상 숨김 상태로 시작
            if (_disconnectPanel != null)
                _disconnectPanel.SetActive(false);

            // 버튼 이벤트 등록
            if (_disconnectReturnButton != null)
                _disconnectReturnButton.onClick.AddListener(OnReturnButtonClicked);

            // NetworkGameManager 자동 탐색 (Inspector에 연결 안 된 경우)
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            // 네트워크 모드 확인 후 패널 활성/비활성 결정 — NetworkContext / NGM 사용
            // (이전: NetworkManager.Singleton.IsHost || IsClient 직접 호출)
            bool isNetworkMode = NetworkContext.IsNetworkActive ||
                                 (_networkGameManager != null && _networkGameManager.IsNetworkRunning);

            if (_networkStatusPanel != null)
                _networkStatusPanel.SetActive(isNetworkMode);

            if (!isNetworkMode)
            {
                // 싱글플레이: 아무 동작도 하지 않음
                return;
            }

            // 멀티플레이: Ping 갱신 코루틴 시작 + 서버 끊김 이벤트 구독
            _pingCoroutine = StartCoroutine(PingRefreshCoroutine());
            if (_networkGameManager != null)
                _networkGameManager.OnServerDisconnected += OnServerDisconnected;

            // [결과 화면 여부 추적] 경기가 끝났는지만 알면 되므로 GameEvents.OnGameEnd 를 구독한다.
            //   이 이벤트는 싱글/멀티, Host/Client 어느 쪽에서도 결과 화면이 뜨는 순간 발행되므로
            //   (서버는 OnGameEndServer 직전, 클라이언트는 AnnounceWinnerClientRpc 안에서 발행)
            //   역할에 따라 판정이 갈리지 않는다.
            //   🔴 GameEndUI 를 직접 참조하지 않는 이유: UI 컴포넌트끼리 서로를 붙잡으면
            //      배치 순서·파괴 순서에 따라 null 이 되기 때문이다.
            //      ⚠️ 2026-10-05 실측 정정 — 종전 주석은 여기에 "이 프로젝트의 UI 는 서로를
            //         직접 참조하지 않고 이벤트로만 소통하는 관례를 쓴다"고 덧붙이고 있었으나
            //         그 관례는 사실이 아니다. UI 컴포넌트를 직렬화 필드로 직접 붙잡는 자리가
            //         이 프로젝트에 여럿 있다(로비의 루트 뷰가 탭별 하위 뷰들을 들고 있는 구조,
            //         전투 씬 HUD 가 설정 메뉴를 들고 있는 것, UI 생명주기 매니저가 결과 화면을
            //         들고 있는 것 등). 🔴 그러므로 이 선택은 "프로젝트 관례라서"가 아니라
            //         "이 자리에서는 이벤트 쪽이 순서 문제에 안전해서"가 전부다.
            _gameEndSubscription = GameEvents.OnGameEnd
                .Subscribe(_ => _resultScreenShown = true);

            // [개발] Info + 개발. 멀티플레이 진입 시의 의도된 정상 흐름 통보다.
            GameLog.Dev.Info("Network", nameof(NetworkStatusUI), "네트워크 상태 모니터링 시작");
        }

        private void OnDestroy()
        {
            // 코루틴 정리
            if (_pingCoroutine != null)
            {
                StopCoroutine(_pingCoroutine);
                _pingCoroutine = null;
            }

            // 이벤트 구독 해제 (NGM이 살아있는 경우에만)
            if (_networkGameManager != null)
                _networkGameManager.OnServerDisconnected -= OnServerDisconnected;

            // 게임 종료 이벤트 구독 해제 (누수 방지)
            _gameEndSubscription?.Dispose();
        }

        // ====================================================================
        // Ping 갱신 코루틴
        // ====================================================================

        /// <summary>
        /// 매 _pingRefreshInterval초마다 RTT를 읽어 텍스트를 갱신한다.
        /// RTT 조회는 UnityTransport 를 직접 만지지 않고 NetworkGameManager 를 거친다(아래 갱신 메서드 참조).
        /// ⚠️ 이 루프에는 종료 조건이 없다 — 네트워크가 사라져도 코루틴은 계속 돌고,
        ///    그때는 갱신 메서드가 수치 대신 미연결 표기를 넣는다.
        ///    루프가 멈추는 것은 이 컴포넌트가 파괴돼 OnDestroy 가 코루틴을 중단할 때, 그리고
        ///    이 컴포넌트가 붙은 GameObject 가 비활성화될 때다(유니티는 오브젝트가 꺼지면 그 위의
        ///    코루틴을 멈춘다). 코루틴을 시작하는 곳은 Start 한 번뿐이라 비활성화로 멈춘 루프는
        ///    다시 켜도 저절로 재시작되지 않는다.
        /// </summary>
        private IEnumerator PingRefreshCoroutine()
        {
            var wait = new WaitForSeconds(_pingRefreshInterval);

            while (true)
            {
                yield return wait;
                UpdatePingDisplay();
            }
        }

        /// <summary>
        /// NetworkGameManager.GetCurrentRttMs()를 사용하여 RTT를 읽고 텍스트 갱신.
        /// Host에서는 자신(서버)에 대한 RTT(보통 0에 가까움), Client에서는 서버를 향한 RTT.
        /// NGM이 없거나 네트워크 미연결 시 수치 대신 미연결 표기를 넣는다.
        ///
        /// 이전에는 UnityTransport를 직접 참조해 GetCurrentRtt를 호출했으나,
        /// Presentation 레이어의 Unity.Netcode.Transports.UTP 직접 의존을 제거하고자
        /// 책임을 Infrastructure 레이어인 NGM으로 이전.
        /// </summary>
        private void UpdatePingDisplay()
        {
            if (_pingText == null) return;
            if (_networkGameManager == null || !_networkGameManager.IsNetworkRunning)
            {
                _pingText.text = "--ms";
                return;
            }

            ulong rttMs = _networkGameManager.GetCurrentRttMs();
            _pingText.text = $"{rttMs}ms";
        }

        // ====================================================================
        // 연결 끊김 처리
        // ====================================================================

        /// <summary>
        /// NetworkGameManager.OnServerDisconnected 이벤트 수신.
        /// NGM은 "클라이언트 측에서 서버 연결이 끊긴 경우"에만 이 이벤트를 발행한다.
        /// 서버(Host) 측 상대방 끊김은 ReconnectionHandler가 별도 처리하므로
        /// 본 핸들러에서는 추가 분기 없이 곧바로 팝업을 표시한다.
        ///
        /// 이전 OnClientDisconnected(ulong clientId)는 NetworkManager.IsServer 분기로
        /// 서버/클라이언트를 구분했으나, NGM이 책임을 가져가면서 본 메서드는 클라이언트 전용으로 단순화되었다.
        /// </summary>
        private void OnServerDisconnected()
        {
            if (_disconnectHandled) return;

            // ================================================================
            // 🔴 경기가 끝나 결과 화면이 떠 있으면 이 팝업을 띄우지 않는다 (공통 UI 규칙 D-1).
            //
            // [초급자용 설명] 왜 억제하는가
            //   결과 화면에서 상대가 나가면, 그 사실은 **결과 화면의 타이머 텍스트**가
            //   상대가 떠났다는 문구와 남은 시간을 함께 적어 알려 준다(규칙 D-1 —
            //   그 문구의 원본은 GameEndUI 의 OpponentLeftCountdownFormat 상수 한 곳뿐이다).
            //   규칙 D-1 은 그 알림을 위해 **별도 팝업을 새로 띄우지 말라**고 정한다 —
            //   「상대가 떠났다」와 「남은 시간」은 사용자에게 한 덩어리의 정보이고,
            //   두 자리로 나누면 서로를 가리기 때문이다.
            //   그런데 이탈 판정은 연결도 함께 종료한다 — 그 조항은 무작위 맵 규칙 문서의
            //   「결과 화면에서의 상대 이탈 — 판정과 통보」 규칙이 정한다(번호만 적지 않는 이유는
            //   규칙 문서마다 같은 번호가 전혀 다른 조항을 가리키기 때문이다 — 2026-10-05 대조).
            //   그 종료가 NetworkGameManager.HandleClientDisconnected 를 타고 이 핸들러까지 올라온다.
            //   (의도적으로 나갈 때도 OnClientDisconnectCallback 구독이 해제되지 않아
            //    같은 핸들러가 불린다 — NetworkGameManager 쪽 주석에 그 사실이 적혀 있다.)
            //   그대로 두면 결과 화면 위에 이 파일의 연결 끊김 팝업이 겹쳐 떠서
            //   규칙 D-1 의 문구를 가리게 된다. 그래서 **결과 화면일 때만** 팝업을 건너뛴다.
            //
            // 🔴 [지금 겹치고 있다는 뜻이 아니다 — 2026-10-05 실측 정정]
            //    이 컴포넌트는 어느 씬·프리팹에도 붙어 있지 않아 지금 실행되지 않는다. 변경 이력으로
            //    확인되는 범위(저장소 이력이 시작되는 2026-08-17 이후)에서는 붙은 적도 없다
            //    (머리말의 「씬 배치」 블록 참조). 따라서 위 겹침은 **지금 벌어지고 있는 일이
            //    아니라, 이 컴포넌트를 나중에 배치하면 벌어질 일**이다.
            //    ⚠️ 그래도 이 가드는 지우지 않는다 — 배치하면 겹침이 실제로 발생하므로
            //       유효한 예방이다. 이 정정이 코드를 걷어낼 근거가 되지는 않는다.
            //
            // 🔴 [무엇을 죽이지 않았는가] **경기 진행 중(결과 화면이 뜨기 전)의 진짜 연결 끊김에서는
            //    이 가드가 걸리지 않는다** — 아래 플래그는 종료 이벤트를 받은 뒤에만 true 가 되므로
            //    인게임 경로의 동작은 가드를 넣기 전과 같다.
            //    ⚠️ 다만 "그래서 인게임에서는 팝업이 뜬다"로 읽으면 안 된다. 위 미배치 때문에
            //       인게임 경로에서도 이 팝업은 지금 뜨지 않는다 — 코드 판독으로 참인 것은
            //       「가드가 인게임 경로를 막지 않는다」까지다.
            //
            // 사용자가 화면에 갇히지 않는 근거: 결과 화면의 로비 복귀 버튼은 어떤 상태에서도
            //    꺼지지 않으며(규칙 D-3), 카운트다운이 만료되면 자동으로 로비로 이동한다(규칙 D-4).
            // ================================================================
            if (_resultScreenShown)
            {
                // [개발] Info + 개발. 「팝업을 일부러 띄우지 않았다」는 판단을 남긴다 —
                //   화면에 아무 변화가 없는 분기라, 로그가 없으면 나중에 "팝업이 안 뜬다"는
                //   버그 제보와 이 의도된 억제를 구분할 수 없다.
                GameLog.Dev.Info("Network", nameof(NetworkStatusUI),
                    "서버 연결 끊김 알림 수신 — 결과 화면이 떠 있어 팝업을 띄우지 않는다(규칙 D-1)");
                _disconnectHandled = true;
                return;
            }

            // [개발] Info + 개발.
            //   축 A: 이 자리는 "끊김을 알리는 이벤트를 받았다"는 사실만 남기는 통보다.
            //         끊김 자체의 심각도 판정과 운영 기록은 NetworkGameManager /
            //         ReconnectionHandler 가 이미 갖고 있다.
            //   축 B: 원인(끊김 사유)은 이 자리에 도달하지 않으므로 최종 처리 지점이 아니다(1.3 ②).
            GameLog.Dev.Info("Network", nameof(NetworkStatusUI), "서버 연결 끊김 알림 수신 — 팝업 표시");
            _disconnectHandled = true;
            ShowDisconnectPopup();
        }

        /// <summary>
        /// 연결 끊김 팝업 표시. Time.timeScale은 건드리지 않음 (게임 중단 없이 알림).
        /// </summary>
        private void ShowDisconnectPopup()
        {
            if (_disconnectPanel != null)
                _disconnectPanel.SetActive(true);

            // [개발] Info + 개발. UI 상태 전이 통보.
            GameLog.Dev.Info("Network", nameof(NetworkStatusUI), "연결 끊김 팝업 표시");
        }

        // ====================================================================
        // 버튼 핸들러
        // ====================================================================

        /// <summary>
        /// 연결 끊김 팝업의 복귀 버튼(_disconnectReturnButton)을 눌렀을 때.
        /// NetworkGameManager.ShutdownNetwork()를 호출하고 지정된 씬으로 복귀.
        ///
        /// ⚠️ 2026-10-05 — 종전 주석은 이 버튼을 특정 라벨이 붙은 버튼으로 불렀으나, 그 라벨은
        ///    확인할 수 없다. 이 컴포넌트가 어느 씬에도 없어 버튼 오브젝트 자체가 존재하지 않고,
        ///    라벨을 코드에서 넣는 자리도 없다(라벨은 나중에 패널 쪽 텍스트로 고정해야 한다).
        ///    같은 파일 위쪽 필드의 Tooltip 은 이 버튼을 "돌아가기" 성격으로 적고 있어
        ///    종전 주석과 서로 달랐다 — 그래서 라벨을 적지 않고 필드 이름으로 가리킨다.
        ///
        /// 이전: NetworkManager.Singleton.Shutdown() 직접 호출
        /// 변경: NGM의 공개 래퍼 ShutdownNetwork() 호출 — Unity.Netcode 직접 의존 제거
        /// </summary>
        private void OnReturnButtonClicked()
        {
            // [개발] Info + 개발. 사용자가 직접 누른 버튼의 정상 흐름 통보.
            GameLog.Dev.Info("Network", nameof(NetworkStatusUI), "로비 복귀 버튼 클릭");

            // 시간 복원 (게임 종료 팝업과 함께 표시된 경우)
            Time.timeScale = 1f;

            // 네트워크 종료 — NGM에 위임
            if (_networkGameManager != null)
                _networkGameManager.ShutdownNetwork();

            // 복귀 씬 로드.
            // SceneLoader.Load 가 내부에서 로딩 인디케이터를 띄우므로 별도 ShowLoading 호출은 제거했다.
            // 연결 끊김 복귀도 네트워크 종료 + 씬 전환이 일어나므로 그 사이 사용자가 멈춘 화면을 보지 않게 된다.
            // 로딩을 끄는 책임은 목적지 씬의 초기화 완료 시점이 담당한다(UI 규칙 L-3).
            SceneLoader.Load(_returnSceneName, "로비로 이동 중...");
        }
    }
}
