// ============================================================================
// MatchmakerManager.cs
// Unity Matchmaker 서비스 연동 클래스.
//
// 역할:
//   - 매칭 티켓 생성 (CreateTicketAsync)
//   - 매칭 완료까지 폴링 (PollUntilMatchedAsync)
//   - 매칭 취소 (CancelTicketAsync)
//   - Host/Client 역할 결정 (DetermineIsHost)
//
// 위치: Infrastructure 레이어 — 외부 서비스 연동 담당
//
// 주의사항:
//   - MatchmakerService.Instance 사용 전에 UnityServices.InitializeAsync() 가 완료돼야 함
//   - GetTicketAsync 반환값은 TicketStatusResponse(IOneOf) —
//     가능한 타입: MatchIdAssignment (P2P/Relay용), MultiplayAssignment (전용 서버용)
//   - Hexiege는 P2P(Relay) 구조이므로 MatchIdAssignment 사용
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Unity Matchmaker 서비스 조작 클래스.
    /// 티켓 생성/폴링/취소 및 Host 역할 결정.
    /// </summary>
    public class MatchmakerManager
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>매칭 큐 이름. UGS Dashboard 에서 설정한 큐와 일치해야 함.</summary>
        private const string QueueName = "hexiege-random";

        /// <summary>폴링 간격 (밀리초). 매칭 상태를 확인하는 주기.</summary>
        private const int PollIntervalMs = 1000;

        // ====================================================================
        // 매칭 결과 데이터
        // ====================================================================

        /// <summary>마지막 매칭 결과의 MatchId. Host/Client 결정에 사용.</summary>
        public string LastMatchId { get; private set; }

        // ====================================================================
        // 티켓 생성
        // ====================================================================

        /// <summary>
        /// 매칭 티켓을 생성하여 큐에 등록.
        /// </summary>
        /// <returns>생성된 티켓 ID.</returns>
        public async Task<string> CreateTicketAsync()
        {
            var players = new System.Collections.Generic.List<Player>
            {
                new Player(AuthenticationService.Instance.PlayerId)
            };
            var options = new CreateTicketOptions(queueName: QueueName);
            var response = await MatchmakerService.Instance.CreateTicketAsync(players, options);
            return response.Id;
        }

        // ====================================================================
        // 폴링
        // ====================================================================

        /// <summary>
        /// 매칭이 완료될 때까지 주기적으로 상태를 폴링.
        /// MatchIdAssignment.Status == Found 이면 매칭 완료.
        /// </summary>
        /// <param name="ticketId">폴링할 티켓 ID.</param>
        /// <param name="ct">취소 토큰.</param>
        /// <param name="onWaitSecond">경과 시간(초) 콜백. UI 타이머 표시용.</param>
        /// <returns>매칭된 MatchId.</returns>
        public async Task<string> PollUntilMatchedAsync(
            string ticketId,
            CancellationToken ct,
            Action<int> onWaitSecond = null)
        {
            int elapsed = 0;

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollIntervalMs, ct);
                elapsed += PollIntervalMs / 1000;
                onWaitSecond?.Invoke(elapsed);

                var status = await MatchmakerService.Instance.GetTicketAsync(ticketId);

                // MatchIdAssignment: P2P(Relay) 매칭용 응답 타입
                if (status.Type == typeof(MatchIdAssignment))
                {
                    var assignment = (MatchIdAssignment)status.Value;

                    switch (assignment.Status)
                    {
                        case MatchIdAssignment.StatusOptions.Found:
                            LastMatchId = assignment.MatchId;
                            return assignment.MatchId;

                        case MatchIdAssignment.StatusOptions.Failed:
                            throw new Exception($"매칭 실패: {assignment.Message}");

                        case MatchIdAssignment.StatusOptions.Timeout:
                            throw new Exception("매칭 시간 초과. 다시 시도해주세요.");

                        case MatchIdAssignment.StatusOptions.InProgress:
                            // 계속 폴링
                            continue;
                    }
                }

                // MultiplayAssignment: 전용 서버 매칭용 (Hexiege에서는 사용하지 않지만 방어 코드)
                if (status.Type == typeof(MultiplayAssignment))
                {
                    var assignment = (MultiplayAssignment)status.Value;

                    switch (assignment.Status)
                    {
                        case MultiplayAssignment.StatusOptions.Found:
                            LastMatchId = assignment.MatchId;
                            return assignment.MatchId;

                        case MultiplayAssignment.StatusOptions.Failed:
                            throw new Exception($"매칭 실패: {assignment.Message}");

                        case MultiplayAssignment.StatusOptions.Timeout:
                            throw new Exception("매칭 시간 초과. 다시 시도해주세요.");

                        case MultiplayAssignment.StatusOptions.InProgress:
                            continue;
                    }
                }

                // 알 수 없는 응답 타입이면 계속 폴링
            }

            throw new OperationCanceledException();
        }

        // ====================================================================
        // 티켓 취소
        // ====================================================================

        /// <summary>
        /// 매칭 티켓을 삭제하여 매칭을 취소.
        /// </summary>
        /// <param name="ticketId">삭제할 티켓 ID. null/빈 문자열이면 무시.</param>
        public async Task CancelTicketAsync(string ticketId)
        {
            if (string.IsNullOrEmpty(ticketId)) return;
            await MatchmakerService.Instance.DeleteTicketAsync(ticketId);
        }

        // ====================================================================
        // Host 역할 결정
        // ====================================================================

        /// <summary>
        /// 매칭 결과에서 플레이어 목록을 조회하여 결정론적으로 Host 를 선택.
        /// 양쪽 클라이언트가 동일한 정렬 순서와 해시 인덱스로 동일 결과를 도출.
        /// </summary>
        /// <param name="matchId">매칭된 Match ID.</param>
        /// <returns>현재 플레이어가 Host 이면 true.</returns>
        public async Task<bool> DetermineIsHostAsync(string matchId)
        {
            // 매칭 결과에서 플레이어 목록 조회 (두 클라이언트 모두 동일한 목록을 받음)
            var results = await MatchmakerService.Instance.GetMatchmakingResultsAsync(matchId);
            string playerId = AuthenticationService.Instance.PlayerId;

            // MatchProperties.Players 에서 Id 기준 사전순 정렬 → 결정론적 순서 보장
            List<Player> sortedPlayers = results.MatchProperties.Players
                .OrderBy(p => p.Id, StringComparer.Ordinal)
                .ToList();

            if (sortedPlayers.Count == 0) return false;

            // MatchId(UGS 발급 UUID) 안정 해시로 Host 인덱스 결정
            // → 매 매치마다 다른 UUID → 50/50 무작위 분배 보장
            // → 크로스-플랫폼/프로세스에서 동일한 결과 보장
            int hostIndex = GetStableHash(matchId) % sortedPlayers.Count;
            return sortedPlayers[hostIndex].Id == playerId;
        }

        /// <summary>
        /// 크로스-플랫폼/크로스-프로세스 결정론적 해시.
        /// .NET string.GetHashCode()는 프로세스마다 다른 시드를 사용하므로 직접 구현.
        /// </summary>
        private static int GetStableHash(string s)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in s)
                    hash = hash * 31 + c;
                return Math.Abs(hash);
            }
        }
    }
}
