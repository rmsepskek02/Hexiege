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
        // Host 역할 결정 — [비활성화됨] 2026-07-17 매치메이킹 404 수정 (A방식 전환)
        // ====================================================================
        //
        // 아래 DetermineIsHostAsync / GetStableHash 두 메서드는 "매치 결과 조회 후
        // 해시로 호스트를 계산"하던 기존 방식이다. 이 방식은 다음 이유로 폐기되어
        // 호스트 결정 경로에서 제거되었다.
        //
        //   1) DetermineIsHostAsync 내부의 GetMatchmakingResultsAsync 호출은
        //      전용 서버(Multiplay)가 매치 결과를 조회할 때 쓰는 서버 지향 API다.
        //      우리 게임은 P2P(Relay) 구조라 일반 플레이어가 호출하면 조회 대상
        //      리소스가 없어 404(Not Found)가 반환되고, 그 지점에서 연결이 끊겼다.
        //   2) 이 API가 주던 "전체 플레이어 목록"이 사라지면 해시 방식은 입력을
        //      잃어 성립 자체가 불가능하다.
        //
        // 대체 방식(A방식): 모든 플레이어가 같은 matchId를 키로 Lobby에
        // CreateOrJoin(없으면 생성=호스트 / 있으면 참가=클라이언트)을 요청한다.
        // 서버가 원자적으로 처리하므로 정확히 한 명만 호스트가 된다.
        // (구현 위치: LobbyManager.CreateOrJoinLobbyByMatchIdAsync +
        //  NetworkGameManager.StartMatchmadeGameAsync)
        //
        // ⚠️ 즉시 삭제가 아니라 "비활성화(주석)"다. 사용자 실기 테스트 통과 후
        //    별도 단계에서 최종 삭제한다 (WORKFLOW [4] 규칙).
        //    참고 문서: Docs/_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/
        // ====================================================================

        /*
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
        */
    }
}
