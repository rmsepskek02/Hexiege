// ============================================================================
// RelayManager.cs
// Unity Relay 서비스 연동 클래스.
//
// 역할:
//   - Host: Relay 서버 할당 생성 + Join Code 발급 + UnityTransport 설정
//   - Client: Join Code 로 Relay 서버 참가 + UnityTransport 설정
//   - NetworkManager.Singleton 의 UnityTransport 컴포넌트에 Relay 데이터 주입
//
// 위치: Infrastructure 레이어 — 외부 서비스 연동 담당
//
// 주의사항:
//   - RelayService.Instance 사용 전에 UnityServicesInitializer.InitializeAsync() 완료 필요
//   - NetworkManager GameObject 가 씬에 존재하고 UnityTransport 가 설정돼야 함
//   - 모바일(비 WebGL)에서는 "dtls" 프로토콜 사용 (암호화 UDP)
// ============================================================================

using System;
using System.Threading.Tasks;
using Hexiege.Application;   // GameLog · LogEvent (로그 파사드 — LogRules.md 1.5 / 1.8)
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Unity Relay 서버 할당 및 연결 설정 담당 클래스.
    /// Host 는 CreateRelayAsync, Client 는 JoinRelayAsync 를 호출.
    /// </summary>
    public class RelayManager
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>
        /// Relay 연결 프로토콜. 모바일에서는 DTLS (암호화 UDP) 사용.
        /// WebGL 빌드라면 "wss" 를 사용해야 하나, 이 프로젝트는 모바일 타겟.
        /// </summary>
        private const string ConnectionType = "dtls";

        /// <summary>Host 를 제외한 최대 동시 접속 클라이언트 수.</summary>
        private const int MaxConnections = 1; // 1v1 게임이므로 Host + 1 Client

        // ====================================================================
        // Relay 할당 생성 (Host 전용)
        // ====================================================================

        /// <summary>
        /// Relay 서버에 할당을 생성하고 Join Code 를 반환.
        /// Host 측에서 호출. 반환된 Join Code 를 Lobby Data 에 저장해 Client 에게 공유.
        /// 내부적으로 NetworkManager 의 UnityTransport 에 Relay 서버 데이터를 설정.
        /// </summary>
        /// <returns>Join Code 문자열. 실패 시 null.</returns>
        public async Task<string> CreateRelayAsync()
        {
            try
            {
                // [개발] 진입 흔적. 에디터에서 그대로 재현되므로 릴리스에 남길 필요가 없다(LogRules 1.2 축 B ①).
                GameLog.Dev.Info("Network", nameof(RelayManager), "Relay 할당 생성 시작");

                // Relay 서버에 할당 요청
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);

                // Join Code 발급 (다른 플레이어가 이 코드로 접속)
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                // [개발] 성공 통보. 성공 여부는 이후 흐름(Lobby 기록 → Client 접속)으로 드러난다.
                // Join Code 는 LogRules 1.6 의 규정 항목(이메일 / UID·PlayerId / 토큰)이 아니다 —
                // 선행 task 가 LobbyManager 의 RelayJoinCode= · NetworkGameManager 의 LobbyCode= 를
                // 같은 근거로 평문 유지하기로 확정했으므로 그 표기를 그대로 따른다.
                GameLog.Dev.Info("Network", nameof(RelayManager), "Relay 할당 완료",
                                 $"RelayJoinCode={joinCode}");

                // UnityTransport 에 Host 용 Relay 데이터 설정
                SetTransportAsHost(allocation);

                return joinCode;
            }
            catch (RelayServiceException e)
            {
                // [운영] 여기가 "최종 처리 지점"이다(LogRules 1.3 ② — 예외 객체를 직접 쥔 계층).
                //   호출부(NetworkGameManager)는 null 만 받아 "다시 시도해주세요" 를 띄울 뿐이라,
                //   실패 원인은 이 줄에만 남는다 → 축 B 두 문장 모두 "예" → 운영.
                //   복구 경로가 없어(null 반환 → 세션 자체를 못 연다) 축 A 는 Error.
                // ⚠️ e.Message 로 눌러 담지 않고 예외 객체를 그대로 넘긴다 —
                //    예외 "타입"이 서버 집계의 핵심 축이기 때문이다(LogRules 1.9).
                GameLog.Ops.Error(LogEvent.RelaySetupFailed, "Network", nameof(RelayManager),
                                  "Relay 할당 생성 실패 — 세션을 열 수 없다", e, "Stage=Allocate");
                return null;
            }
            catch (Exception e)
            {
                // [운영] 위와 같은 사건(Relay 할당 단계 실패)이므로 같은 키를 쓴다.
                //   키를 나누면 "Relay 때문에 게임에 못 들어간 횟수" 지표가 쪼개진다(LogRules 1.5).
                //   RelayServiceException 인지 아닌지는 ExceptionType 필드로 구분된다.
                GameLog.Ops.Error(LogEvent.RelaySetupFailed, "Network", nameof(RelayManager),
                                  "Relay 할당 중 예외 발생 — 세션을 열 수 없다", e, "Stage=Allocate");
                return null;
            }
        }

        // ====================================================================
        // Relay 참가 (Client 전용)
        // ====================================================================

        /// <summary>
        /// Join Code 로 Relay 서버에 참가.
        /// Client 측에서 호출. 내부적으로 NetworkManager 의 UnityTransport 에 설정 주입.
        /// </summary>
        /// <param name="joinCode">Host 가 공유한 Relay Join Code.</param>
        /// <returns>참가 성공 여부.</returns>
        public async Task<bool> JoinRelayAsync(string joinCode)
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                // [운영] 호출부 계약 위반 = 불변식 위반이다.
                //   현재 호출부 3곳이 모두 사전에 빈 문자열을 걸러 내므로 정상 흐름에서는 도달할 수 없고,
                //   도달했다면 호출 경로가 깨진 것이다. 여기서 false 를 반환해 참가가 죽고 복구 경로가 없다.
                //   선행 판정 선례: LoginUseCase 의 "firebaseUID 가 비어 있음" → Error + 운영
                //   (LogAudit.md 3-6 / UgsBridgeMissingFirebaseUid).
                GameLog.Ops.Error(LogEvent.RelaySetupFailed, "Network", nameof(RelayManager),
                                  "JoinRelay 호출에 Join Code 가 비어 있다 — 호출부 계약 위반",
                                  "Stage=CodeMissing");
                return false;
            }

            try
            {
                // [개발] 진입 흔적.
                GameLog.Dev.Info("Network", nameof(RelayManager), "Relay 참가 시도",
                                 $"RelayJoinCode={joinCode}");

                // Join Code 로 Relay 참가 할당 조회
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                // [개발] 성공 통보. 실패하면 아래 catch 가 원인과 함께 운영으로 남긴다.
                GameLog.Dev.Info("Network", nameof(RelayManager), "Relay 참가 할당 획득 완료");

                // UnityTransport 에 Client 용 Relay 데이터 설정
                SetTransportAsClient(joinAllocation);

                return true;
            }
            catch (RelayServiceException e)
            {
                // [운영] 할당 실패(위)와 같은 이유로 최종 처리 지점이다. 단계만 Join 으로 다르다.
                GameLog.Ops.Error(LogEvent.RelaySetupFailed, "Network", nameof(RelayManager),
                                  "Relay 참가 실패 — 세션을 열 수 없다", e, "Stage=Join");
                return false;
            }
            catch (Exception e)
            {
                // [운영] 위와 같은 사건 → 같은 키. 예외 종류는 ExceptionType 필드로 갈린다.
                GameLog.Ops.Error(LogEvent.RelaySetupFailed, "Network", nameof(RelayManager),
                                  "Relay 참가 중 예외 발생 — 세션을 열 수 없다", e, "Stage=Join");
                return false;
            }
        }

        // ====================================================================
        // UnityTransport 설정 내부 메서드
        // ====================================================================

        /// <summary>
        /// NetworkManager 의 UnityTransport 컴포넌트를 Host 용 Relay 데이터로 설정.
        /// AllocationUtils.ToRelayServerData 확장 메서드로 RelayServerData 를 빌드.
        /// </summary>
        private void SetTransportAsHost(Allocation allocation)
        {
            UnityTransport transport = GetUnityTransport();
            if (transport == null) return;

            // AllocationUtils 확장 메서드: Allocation → RelayServerData 변환
            var relayServerData = allocation.ToRelayServerData(ConnectionType);
            transport.SetRelayServerData(relayServerData);

            // [개발] 성공 통보. 설정이 안 됐다면 곧바로 접속 실패로 드러난다.
            GameLog.Dev.Info("Network", nameof(RelayManager), "UnityTransport Host 설정 완료");
        }

        /// <summary>
        /// NetworkManager 의 UnityTransport 컴포넌트를 Client 용 Relay 데이터로 설정.
        /// JoinAllocation 에는 HostConnectionData 가 포함돼 있음.
        /// </summary>
        private void SetTransportAsClient(JoinAllocation joinAllocation)
        {
            UnityTransport transport = GetUnityTransport();
            if (transport == null) return;

            // AllocationUtils 확장 메서드: JoinAllocation → RelayServerData 변환
            var relayServerData = joinAllocation.ToRelayServerData(ConnectionType);
            transport.SetRelayServerData(relayServerData);

            // [개발] 성공 통보.
            GameLog.Dev.Info("Network", nameof(RelayManager), "UnityTransport Client 설정 완료");
        }

        /// <summary>
        /// NetworkManager.Singleton 에서 UnityTransport 컴포넌트를 가져옴.
        /// NetworkManager 가 씬에 없거나 UnityTransport 가 없으면 에러 로그 출력.
        /// </summary>
        private UnityTransport GetUnityTransport()
        {
            if (NetworkManager.Singleton == null)
            {
                // [운영] 불변식 위반. "GameBootstrapper 가 유일한 조합 루트" 라는 전제가 깨진 상태다.
                //   여기서 null 을 돌려주면 Relay 데이터가 Transport 에 끝내 설정되지 않는데,
                //   호출부(SetTransportAsHost/Client)는 조용히 빠져나가므로 접속이 원인 없이 죽는다.
                //   선행 판정 선례와 같은 사건이므로 같은 키를 쓴다
                //   (LogAudit.md 3-8 — NetworkGameManager 의 Singleton null 2건 / NetworkManagerSingletonMissing).
                GameLog.Ops.Error(LogEvent.NetworkManagerSingletonMissing, "Network", nameof(RelayManager),
                                  "GetUnityTransport: NetworkManager.Singleton 이 null 이다 — " +
                                  "씬에 NetworkManager GameObject 를 추가해야 한다");
                return null;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                // ⚠️ [잠정 판정] 개발 + Warn (원래 호출은 Error 레벨이었다 → 축 A 하향).
                //   근거: LogRules 1.3 원칙 3 단서 —
                //   "Inspector 배선 누락 같은 설정 오류는 Warn + 개발로 낮춘다."
                //   NetworkManager 오브젝트에 UnityTransport 컴포넌트를 붙이는 것은 에디터 설정이고,
                //   빠져 있으면 에디터 실행 첫 회에 100% 재현되므로 축 B ①("플레이어 기기에서만
                //   벌어지는가")이 "아니오"다. 바로 위 Singleton null 과 달리 런타임 타이밍에
                //   좌우되지 않는다는 점이 두 자리를 가르는 근거다.
                GameLog.Dev.Warn("Network", nameof(RelayManager),
                                 "GetUnityTransport: NetworkManager 에 UnityTransport 컴포넌트가 없다");
            }

            return transport;
        }
    }
}
