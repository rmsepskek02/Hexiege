// ============================================================================
// LobbyManager.cs
// Unity Lobby 서비스 연동 클래스.
//
// 역할:
//   - 방(Lobby) 생성, 조회, 참가, 나가기 기능 제공
//   - Host 전용 Heartbeat 코루틴으로 Lobby 활성 상태 유지
//   - Relay Join Code 를 Lobby Data 에 저장/추출하는 컨벤션 관리
//   - CurrentLobby 프로퍼티로 현재 방 상태 노출
//
// 위치: Infrastructure 레이어 — 외부 서비스 연동 담당
//
// 주의사항:
//   - LobbyService.Instance 사용 전에 반드시 UnityServicesInitializer.InitializeAsync() 가 완료돼야 함
//   - Heartbeat 는 MonoBehaviour 의 코루틴에 의존하므로 NetworkGameManager 에서 관리
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Unity Lobby 서비스 조작 클래스.
    /// 방 생성/참가/나가기 및 Heartbeat 관리.
    /// </summary>
    public class LobbyManager
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>Lobby Data 에서 Relay Join Code 를 저장하는 키 이름.</summary>
        public const string RelayJoinCodeKey = "RelayJoinCode";

        /// <summary>Heartbeat 전송 간격 (초). Lobby 활성 상태를 서버에 알림.</summary>
        private const float HeartbeatIntervalSeconds = 25f;

        // ====================================================================
        // 상태 프로퍼티
        // ====================================================================

        /// <summary>현재 참가 중인 Lobby. 없으면 null.</summary>
        public Lobby CurrentLobby { get; private set; }

        /// <summary>현재 내가 Host 인지 여부.</summary>
        public bool IsHost =>
            CurrentLobby != null &&
            CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;

        // ====================================================================
        // Lobby 생성
        // ====================================================================

        /// <summary>
        /// 공개 Lobby 를 생성하고 현재 Lobby 로 설정.
        /// Relay Join Code 가 있으면 Lobby Data 에 저장.
        /// </summary>
        /// <param name="lobbyName">표시될 방 이름.</param>
        /// <param name="maxPlayers">최대 플레이어 수 (기본 2인).</param>
        /// <param name="relayJoinCode">Relay Join Code. null 이면 나중에 별도 업데이트.</param>
        /// <returns>생성된 Lobby 객체. 실패 시 null.</returns>
        public async Task<Lobby> CreateLobbyAsync(
            string lobbyName, int maxPlayers = 2, string relayJoinCode = null)
        {
            try
            {
                var options = new CreateLobbyOptions { IsPrivate = false };

                // Relay Join Code 가 있으면 바로 Lobby Data 에 포함
                if (!string.IsNullOrEmpty(relayJoinCode))
                {
                    options.Data = new Dictionary<string, DataObject>
                    {
                        [RelayJoinCodeKey] = new DataObject(
                            DataObject.VisibilityOptions.Public, relayJoinCode)
                    };
                }

                CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(
                    lobbyName, maxPlayers, options);

                Debug.Log($"[Network] Lobby 생성 완료. 이름: {CurrentLobby.Name}, " +
                          $"코드: {CurrentLobby.LobbyCode}, ID: {CurrentLobby.Id}");

                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Network] Lobby 생성 실패: {e.Message}");
                return null;
            }
        }

        // ====================================================================
        // Lobby 조회
        // ====================================================================

        /// <summary>
        /// 공개 Lobby 목록을 조회.
        /// 빈 슬롯이 있는 방만 필터링.
        /// </summary>
        /// <param name="maxCount">조회할 최대 방 개수 (기본 20).</param>
        /// <returns>조회된 Lobby 목록. 실패 시 빈 리스트.</returns>
        public async Task<List<Lobby>> GetLobbiesAsync(int maxCount = 20)
        {
            try
            {
                var options = new QueryLobbiesOptions
                {
                    Count = maxCount,
                    // 빈 슬롯이 있는 방만 표시
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(
                            QueryFilter.FieldOptions.AvailableSlots,
                            "0",
                            QueryFilter.OpOptions.GT)
                    }
                };

                var response = await LobbyService.Instance.QueryLobbiesAsync(options);
                Debug.Log($"[Network] Lobby 목록 조회 완료. 총 {response.Results.Count}개.");
                return response.Results;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Network] Lobby 목록 조회 실패: {e.Message}");
                return new List<Lobby>();
            }
        }

        // ====================================================================
        // Lobby 참가
        // ====================================================================

        /// <summary>
        /// Lobby 코드로 방에 참가.
        /// </summary>
        /// <param name="lobbyCode">호스트가 공유한 로비 참가 코드.</param>
        /// <returns>참가한 Lobby 객체. 실패 시 null.</returns>
        public async Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode)
        {
            try
            {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
                Debug.Log($"[Network] Lobby 참가 완료 (코드). 이름: {CurrentLobby.Name}");
                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Network] Lobby 참가 실패 (코드): {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 빠른 매칭 — 빈 슬롯이 있는 공개 방에 자동으로 참가.
        /// </summary>
        /// <returns>참가한 Lobby 객체. 실패 시 null.</returns>
        public async Task<Lobby> QuickJoinAsync()
        {
            try
            {
                CurrentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
                Debug.Log($"[Network] 빠른 매칭 성공. 방: {CurrentLobby.Name}");
                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Network] 빠른 매칭 실패: {e.Message}");
                return null;
            }
        }

        // ====================================================================
        // Lobby 업데이트
        // ====================================================================

        /// <summary>
        /// 현재 Lobby 의 Relay Join Code 를 업데이트.
        /// Host 가 Relay 할당 후 Join Code 를 Lobby 에 기록할 때 사용.
        /// </summary>
        /// <param name="relayJoinCode">Relay 서버에서 발급받은 Join Code.</param>
        public async Task UpdateRelayJoinCodeAsync(string relayJoinCode)
        {
            if (CurrentLobby == null)
            {
                Debug.LogError("[Network] UpdateRelayJoinCode: 현재 참가 중인 Lobby 가 없습니다.");
                return;
            }

            try
            {
                var options = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        [RelayJoinCodeKey] = new DataObject(
                            DataObject.VisibilityOptions.Public, relayJoinCode)
                    }
                };

                CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, options);
                Debug.Log($"[Network] Lobby 에 Relay Join Code 저장 완료: {relayJoinCode}");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Network] Relay Join Code 업데이트 실패: {e.Message}");
            }
        }

        // ====================================================================
        // Lobby 나가기
        // ====================================================================

        /// <summary>
        /// 현재 참가 중인 Lobby 에서 나가기.
        /// Host 이면 Lobby 를 삭제, Guest 이면 자신만 나감.
        /// </summary>
        public async Task LeaveLobbyAsync()
        {
            if (CurrentLobby == null) return;

            string lobbyId = CurrentLobby.Id;
            string playerId = AuthenticationService.Instance.PlayerId;

            try
            {
                if (IsHost)
                {
                    // Host 가 나가면 Lobby 자체를 삭제
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                    Debug.Log($"[Network] Lobby 삭제 완료 (Host 퇴장). ID: {lobbyId}");
                }
                else
                {
                    // Guest 는 자신만 Lobby 에서 제거
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                    Debug.Log($"[Network] Lobby 나가기 완료 (Guest 퇴장). ID: {lobbyId}");
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[Network] Lobby 나가기 실패: {e.Message}");
            }
            finally
            {
                // 실패하더라도 로컬 상태 초기화
                CurrentLobby = null;
            }
        }

        // ====================================================================
        // Relay Join Code 추출
        // ====================================================================

        /// <summary>
        /// 현재 Lobby Data 에서 Relay Join Code 를 읽어옴.
        /// Client 가 Lobby 참가 후 Relay 에 접속하기 위해 사용.
        /// </summary>
        /// <returns>Relay Join Code. 없으면 null.</returns>
        public string GetRelayJoinCode()
        {
            if (CurrentLobby?.Data == null) return null;

            if (CurrentLobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject dataObj))
                return dataObj.Value;

            return null;
        }

        // ====================================================================
        // Heartbeat 코루틴
        // ====================================================================

        /// <summary>
        /// Host 전용 Heartbeat 코루틴. 주기적으로 Lobby 에 ping 을 보내 활성 상태를 유지.
        /// NetworkGameManager 의 StartCoroutine 으로 실행해야 함.
        /// </summary>
        public IEnumerator HeartbeatCoroutine()
        {
            var wait = new WaitForSeconds(HeartbeatIntervalSeconds);

            while (CurrentLobby != null && IsHost)
            {
                yield return wait;

                if (CurrentLobby == null) break;

                // 비동기 Task 를 코루틴 내에서 실행 (완료 대기)
                var task = LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                yield return new WaitUntil(() => task.IsCompleted);

                if (task.Exception != null)
                {
                    Debug.LogWarning($"[Network] Heartbeat 전송 실패: {task.Exception.Message}");
                }
            }

            Debug.Log("[Network] Heartbeat 코루틴 종료.");
        }
    }
}
