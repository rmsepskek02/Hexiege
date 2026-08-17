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
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Hexiege.Application;

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

        /// <summary>Lobby Data 에서 MatchId 를 저장하는 키. S1 인덱스 필드로 검색 가능.</summary>
        public const string MatchIdKey = "MatchId";

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
            string lobbyName, int maxPlayers = 2, string relayJoinCode = null, string matchId = null)
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

                // MatchId 가 있으면 S1 인덱스 필드로 저장 (매칭 검색용)
                if (!string.IsNullOrEmpty(matchId))
                {
                    if (options.Data == null)
                        options.Data = new Dictionary<string, DataObject>();
                    options.Data[MatchIdKey] = new DataObject(
                        DataObject.VisibilityOptions.Public,
                        matchId,
                        DataObject.IndexOptions.S1);
                }

                CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(
                    lobbyName, maxPlayers, options);

                GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 생성 완료",
                                 $"Name={CurrentLobby.Name}, Code={CurrentLobby.LobbyCode}, LobbyId={CurrentLobby.Id}");

                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Error(LogEvent.LobbyServiceCallFailed, "Network", nameof(LobbyManager),
                                  "Lobby 생성 실패 — 호출부는 null 만 받는다", e);
                return null;
            }
        }

        // ====================================================================
        // Lobby CreateOrJoin (매칭 호스트 선점) — 2026-07-17 추가
        // ====================================================================

        /// <summary>
        /// matchId 를 Lobby 의 고유 ID(lobbyId)로 사용하여 "없으면 생성 / 있으면 참가"를
        /// 서버측에서 한 번에(원자적으로) 처리한다. 랜덤 매칭 후 호스트 결정용.
        ///
        /// 동작 원리:
        ///   - 두 플레이어가 매칭 폴링으로 받는 matchId 는 동일한 값이다.
        ///   - 그 matchId 를 lobbyId 로 넘겨 CreateOrJoin 을 호출하면,
        ///     먼저 도착한 쪽은 Lobby 를 "생성"하여 호스트가 되고,
        ///     나중에 도착한 쪽은 같은 ID 의 Lobby 가 이미 있으므로 "참가"하여 클라이언트가 된다.
        ///   - 이 선점 처리는 UGS 서버가 원자적으로 보장하므로, 두 클라이언트가 동시에
        ///     호출해도 정확히 한 명만 호스트가 되어 race condition 이 원천 차단된다.
        ///
        /// 호스트 여부 판별은 이 호출 이후 <see cref="IsHost"/>(CurrentLobby.HostId == 내 PlayerId)로 한다.
        ///
        /// 사용한 SDK 시그니처 (com.unity.services.multiplayer@2.0.0):
        ///   Task&lt;Lobby&gt; ILobbyService.CreateOrJoinLobbyAsync(
        ///       string lobbyId, string lobbyName, int maxPlayers, CreateLobbyOptions options = null)
        ///   - lobbyId: 호출자가 지정하는 커스텀 Lobby 고유 ID. 같은 ID 가 이미 있으면 참가.
        ///
        /// 주의: options.Data(아래 MatchId 저장)는 "생성" 시에만 반영되고 "참가" 시에는 무시된다.
        ///       (참가하는 쪽은 이미 존재하는 Lobby 의 Data 를 그대로 받는다.)
        ///       RelayJoinCode 는 호스트가 생성 직후 <see cref="UpdateRelayJoinCodeAsync"/>로 채우는
        ///       기존 방식을 그대로 유지한다.
        /// </summary>
        /// <param name="matchId">Matchmaker 가 발급한 Match ID. Lobby 의 고유 ID 로 사용.</param>
        /// <param name="lobbyName">표시될 방 이름 (생성 시에만 사용).</param>
        /// <param name="maxPlayers">최대 플레이어 수 (기본 2인).</param>
        /// <returns>생성 또는 참가한 Lobby 객체. 실패 시 null.</returns>
        public async Task<Lobby> CreateOrJoinLobbyByMatchIdAsync(
            string matchId, string lobbyName, int maxPlayers = 2)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                GameLog.Ops.Error(LogEvent.LobbyInvariantViolated, "Network", nameof(LobbyManager),
                                  "CreateOrJoinLobbyByMatchId 불변식 위반 — matchId 가 비어 있다");
                return null;
            }

            try
            {
                // 생성 시 적용될 옵션. MatchId 를 S1 인덱스 필드로 저장 —
                // 기존 CreateLobbyAsync(91~99행)의 저장 규칙을 동일하게 준수.
                var options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        [MatchIdKey] = new DataObject(
                            DataObject.VisibilityOptions.Public,
                            matchId,
                            DataObject.IndexOptions.S1)
                    }
                };

                // matchId 를 lobbyId 로 직접 사용 → "없으면 생성 / 있으면 참가" 원자 처리
                CurrentLobby = await LobbyService.Instance.CreateOrJoinLobbyAsync(
                    matchId, lobbyName, maxPlayers, options);

                // ⚠️ HostId 는 반드시 GameLog.HashId 를 거쳐서 넣는다 (LogRules.md 1.6 — 1단 방어).
                //   Lobby.HostId 는 "호스트인 사람의 UGS PlayerId 그 자체"다.
                //   근거: 이 파일 60행의 IsHost 판정이
                //         `CurrentLobby.HostId == AuthenticationService.Instance.PlayerId`
                //         로, HostId 를 내 PlayerId 와 직접 비교하고 있다.
                //   LogRules.md 1.6 은 UID·PlayerId 를 원본 그대로 남기지 말고 해시로 치환하라고 규정한다
                //   (로그 파일은 채팅에 붙여지고 리포지토리에 커밋되므로 유출 경로가 이미 여럿이다).
                //   해시는 "같은 사람인지"만 알려 주고 "누구인지"는 알려 주지 않으므로,
                //   호스트가 매번 같은 계정인지 추적하는 디버깅 용도는 그대로 유지된다.
                GameLog.Dev.Info("Network", nameof(LobbyManager), "CreateOrJoin 완료",
                                 $"MatchId={matchId}, LobbyId={CurrentLobby.Id}, HostId={GameLog.HashId(CurrentLobby.HostId)}, IsHost={IsHost}");

                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Error(LogEvent.MatchmakingLobbyJoinFailed, "Network", nameof(LobbyManager),
                                  "매칭 Lobby 생성·참가 실패", e, $"MatchId={matchId}");
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
                GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 목록 조회 완료 【호출처 0건】",
                                 $"Count={response.Results.Count}");
                return response.Results;
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Error(LogEvent.LobbyServiceCallFailed, "Network", nameof(LobbyManager),
                                  "Lobby 목록 조회 실패 — 빈 리스트를 반환한다", e);
                return new List<Lobby>();
            }
        }

        // ====================================================================
        // Lobby 검색 (매칭용)
        // ====================================================================

        /// <summary>
        /// MatchId 로 매칭 전용 Lobby 를 검색.
        /// S1 인덱스 필드에 저장된 MatchId 로 쿼리.
        /// </summary>
        /// <param name="matchId">Matchmaker 에서 발급한 Match ID.</param>
        /// <returns>Lobby 참가 코드. 못 찾으면 null.</returns>
        public async Task<string> FindLobbyByMatchIdAsync(string matchId)
        {
            try
            {
                // 빈 슬롯이 있는 공개 로비 전체 조회 후 클라이언트에서 matchId 필터링
                // S1 인덱스 쿼리 대신 전체 조회 사용 (S1 인덱스 전파 지연 문제 우회)
                var queryOptions = new QueryLobbiesOptions
                {
                    Count = 25,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(
                            QueryFilter.FieldOptions.AvailableSlots,
                            "0",
                            QueryFilter.OpOptions.GT)
                    }
                };

                var results = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
                GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 전체 조회 【호출처 0건】",
                                 $"Count={results.Results.Count}, MatchId={matchId}");

                foreach (var l in results.Results)
                {
                    string storedMatchId = l.Data != null && l.Data.ContainsKey(MatchIdKey)
                        ? l.Data[MatchIdKey].Value : "없음";
                    GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 후보 열거 【호출처 0건】",
                                 $"Name={l.Name}, MatchId={storedMatchId}");
                }

                var lobby = results.Results.FirstOrDefault(l =>
                    l.Data != null &&
                    l.Data.ContainsKey(MatchIdKey) &&
                    string.Equals(l.Data[MatchIdKey].Value.Trim(), matchId.Trim(),
                        StringComparison.OrdinalIgnoreCase));

                // 중괄호를 명시한다 — 원래 없었는데, 본문이 한 줄일 때 이후 편집에서
                // 문장을 덧붙이면 조건 밖으로 새기 쉽다(동작은 동일하다).
                if (lobby != null)
                {
                    GameLog.Dev.Info("Network", nameof(LobbyManager), "매칭 Lobby 발견 【호출처 0건】",
                                     $"Name={lobby.Name}, LobbyId={lobby.Id}");
                }
                else
                {
                    GameLog.Dev.Info("Network", nameof(LobbyManager), "매칭 Lobby 없음 — matchId 가 일치하는 Lobby 를 찾지 못했다 【호출처 0건】",
                                     $"MatchId={matchId}");
                }

                return lobby?.Id;
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Warn(LogEvent.MatchmakingLobbyJoinFailed, "Network", nameof(LobbyManager),
                                  "매칭 Lobby 검색 실패", e, $"MatchId={matchId}");
                return null;
            }
        }

        // ====================================================================
        // Lobby 참가
        // ====================================================================

        /// <summary>
        /// Lobby ID 로 방에 참가. QueryLobbiesAsync 결과의 Id 를 사용.
        /// </summary>
        /// <param name="lobbyId">로비 고유 ID.</param>
        /// <returns>참가한 Lobby 객체. 실패 시 null.</returns>
        public async Task<Lobby> JoinLobbyByIdAsync(string lobbyId)
        {
            try
            {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
                GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 참가 완료 (ID) 【호출처 0건】",
                                 $"Name={CurrentLobby.Name}");
                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Error(LogEvent.LobbyServiceCallFailed, "Network", nameof(LobbyManager),
                                  "Lobby 참가 실패 (ID)", e);
                return null;
            }
        }

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
                GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 참가 완료 (코드)",
                                 $"Name={CurrentLobby.Name}");
                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Error(LogEvent.LobbyServiceCallFailed, "Network", nameof(LobbyManager),
                                  "Lobby 참가 실패 (코드)", e);
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
                GameLog.Dev.Info("Network", nameof(LobbyManager), "빠른 매칭 성공 【호출처 0건】",
                                 $"Name={CurrentLobby.Name}");
                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Error(LogEvent.LobbyServiceCallFailed, "Network", nameof(LobbyManager),
                                  "빠른 매칭 실패", e);
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
                GameLog.Ops.Error(LogEvent.LobbyInvariantViolated, "Network", nameof(LobbyManager),
                                  "UpdateRelayJoinCode 불변식 위반 — 현재 Lobby 가 없다. 클라이언트가 코드를 영원히 받지 못한다");
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
                GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 에 Relay Join Code 저장 완료",
                                 $"RelayJoinCode={relayJoinCode}");
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Error(LogEvent.LobbyServiceCallFailed, "Network", nameof(LobbyManager),
                                  "Relay Join Code 업데이트 실패 — 클라이언트가 코드를 받지 못한다", e);
            }
        }

        /// <summary>
        /// 서버에서 현재 Lobby 의 최신 상태를 다시 받아와 <see cref="CurrentLobby"/>를 갱신한다.
        ///
        /// CurrentLobby 는 참가/생성 시점의 스냅샷이라, 호스트가 나중에
        /// <see cref="UpdateRelayJoinCodeAsync"/>로 기록한 RelayJoinCode 가 반영돼 있지 않다.
        /// 클라이언트가 RelayJoinCode 가 채워질 때까지 폴링할 때 이 메서드로 최신 Data 를 받아온 뒤
        /// <see cref="GetRelayJoinCode"/>로 값을 확인한다.
        /// </summary>
        /// <returns>갱신 성공 여부. 실패해도 기존 CurrentLobby 는 유지.</returns>
        public async Task<bool> RefreshCurrentLobbyAsync()
        {
            if (CurrentLobby == null)
            {
                GameLog.Dev.Warn("Network", nameof(LobbyManager), "RefreshCurrentLobby — 현재 Lobby 가 없다");
                return false;
            }

            try
            {
                CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                return true;
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Warn(LogEvent.LobbyServiceCallFailed, "Network", nameof(LobbyManager),
                                  "Lobby 갱신 실패 — 폴링 실패의 유일한 단서", e, $"LobbyId={CurrentLobby.Id}");
                return false;
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
                    GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 삭제 완료 (Host 퇴장)",
                                 $"LobbyId={lobbyId}");
                }
                else
                {
                    // Guest 는 자신만 Lobby 에서 제거
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                    GameLog.Dev.Info("Network", nameof(LobbyManager), "Lobby 나가기 완료 (Guest 퇴장)",
                                 $"LobbyId={lobbyId}");
                }
            }
            catch (LobbyServiceException e)
            {
                GameLog.Ops.Warn(LogEvent.LobbyServiceCallFailed, "Network", nameof(LobbyManager),
                                  "Lobby 나가기 실패 — 서버에 유령 Lobby 가 남는다", e);
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
                    GameLog.Ops.Warn(LogEvent.LobbyHeartbeatFailed, "Network", nameof(LobbyManager),
                            "Heartbeat 전송 실패 — 끊기면 Lobby 가 만료돼 매칭이 조용히 깨진다",
                            task.Exception);
                }
            }

            GameLog.Dev.Info("Network", nameof(LobbyManager), "Heartbeat 코루틴 종료");
        }
    }
}
