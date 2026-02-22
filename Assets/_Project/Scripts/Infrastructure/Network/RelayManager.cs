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
                Debug.Log("[Network] Relay 할당 생성 시작...");

                // Relay 서버에 할당 요청
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);

                // Join Code 발급 (다른 플레이어가 이 코드로 접속)
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                Debug.Log($"[Network] Relay 할당 완료. Join Code: {joinCode}");

                // UnityTransport 에 Host 용 Relay 데이터 설정
                SetTransportAsHost(allocation);

                return joinCode;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[Network] Relay 할당 생성 실패: {e.Message}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] Relay 예외 발생: {e.Message}");
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
                Debug.LogError("[Network] JoinRelay: Join Code 가 비어 있습니다.");
                return false;
            }

            try
            {
                Debug.Log($"[Network] Relay 참가 시도. Join Code: {joinCode}");

                // Join Code 로 Relay 참가 할당 조회
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                Debug.Log("[Network] Relay 참가 할당 획득 완료.");

                // UnityTransport 에 Client 용 Relay 데이터 설정
                SetTransportAsClient(joinAllocation);

                return true;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[Network] Relay 참가 실패: {e.Message}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] Relay 참가 예외 발생: {e.Message}");
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

            Debug.Log("[Network] UnityTransport Host 설정 완료.");
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

            Debug.Log("[Network] UnityTransport Client 설정 완료.");
        }

        /// <summary>
        /// NetworkManager.Singleton 에서 UnityTransport 컴포넌트를 가져옴.
        /// NetworkManager 가 씬에 없거나 UnityTransport 가 없으면 에러 로그 출력.
        /// </summary>
        private UnityTransport GetUnityTransport()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[Network] GetUnityTransport: NetworkManager.Singleton 이 null 입니다. " +
                               "씬에 NetworkManager GameObject 를 추가하세요.");
                return null;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[Network] GetUnityTransport: NetworkManager 에 UnityTransport 컴포넌트가 없습니다.");
            }

            return transport;
        }
    }
}
