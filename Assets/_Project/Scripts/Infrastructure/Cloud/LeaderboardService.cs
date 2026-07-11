// ============================================================================
// LeaderboardService.cs
// UGS Leaderboards 를 통해 랭킹 데이터를 조회하는 서비스.
//
// 역할:
//   - 상위 랭킹 목록 조회(승률 내림차순)
//   - 현재 플레이어의 순위 조회
//   - ILeaderboardService(Application) 인터페이스를 구현한다(의존성 역전).
//
// 접근 방식(패키지: com.unity.services.leaderboards 2.3.3):
//   LeaderboardsService.Instance.<API>
//   - GetScoresAsync(leaderboardId, GetScoresOptions) → LeaderboardScoresPage
//   - GetPlayerScoreAsync(leaderboardId, GetPlayerScoreOptions) → LeaderboardEntry(SDK)
//
// 데이터 스키마 전제:
//   Leaderboard 점수(score) = 승률(%). 닉네임/전적 등 부가 정보는 각 엔트리의
//   Metadata(JSON 문자열)에 담아 저장한다(Cloud Code recordMatchResult 에서 기록).
//     Metadata 예: {"nickname":"전사","nicknameCode":"4729","totalGames":30,"wins":18,"losses":12}
//
// 이름 주의:
//   SDK 의 엔트리 타입(Unity.Services.Leaderboards.Models.LeaderboardEntry)과
//   우리 DTO(Hexiege.Application.LeaderboardEntry)가 동명이다. 혼동을 피하기 위해
//   본 파일은 SDK Models 네임스페이스를 using 하지 않고, foreach 의 var 로만 SDK 엔트리를
//   다룬 뒤 우리 DTO 로 변환한다.
//
// 주의:
//   - UGS SDK 의 정확한 API 시그니처는 실기 검증이 필요하다. SDK 접근부를 헬퍼로 감쌌으며
//     불확실한 지점에 // TODO: UGS SDK API 시그니처 실기 검증 필요 주석을 남겼다.
//   - MonoBehaviour 가 아닌 일반 C# 클래스다.
//
// Infrastructure 레이어 — 외부 서비스(UGS) 연동 담당.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using UnityEngine;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// UGS Leaderboards 기반 랭킹 조회 서비스.
    /// ILeaderboardService(Application) 구현체.
    /// </summary>
    public class LeaderboardService : ILeaderboardService
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>
        /// UGS Dashboard 에서 생성한 Leaderboard ID.
        /// // TODO: UGS Dashboard 에 생성한 실제 Leaderboard ID 로 교체/검증 필요
        /// </summary>
        private const string LeaderboardId = "hexiege_ranking";

        // ====================================================================
        // 공개 API (ILeaderboardService 구현)
        // ====================================================================

        /// <summary>
        /// 상위 랭킹 목록을 조회한다(승률 내림차순, 상위 limit 명).
        /// 실패 시 빈 목록을 반환한다.
        /// </summary>
        public async Task<List<LeaderboardEntry>> GetTopRankingsAsync(int limit)
        {
            var result = new List<LeaderboardEntry>();

            try
            {
                // TODO: UGS SDK API 시그니처 실기 검증 필요
                //   2.3.3 기준: GetScoresAsync(string, GetScoresOptions) → LeaderboardScoresPage
                //   Offset=0 부터 Limit 개, Metadata 포함 요청.
                var options = new GetScoresOptions
                {
                    Offset = 0,
                    Limit = Mathf.Max(1, limit),
                    IncludeMetadata = true
                };

                var page = await LeaderboardsService.Instance.GetScoresAsync(LeaderboardId, options);

                if (page?.Results != null)
                {
                    // page.Results 요소 타입은 SDK 의 LeaderboardEntry — var 로만 다룬다.
                    foreach (var sdkEntry in page.Results)
                    {
                        result.Add(ConvertToDto(sdkEntry));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardService] 랭킹 목록 조회 실패(빈 목록 반환): {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// 현재 로그인된 플레이어의 순위를 조회한다.
        /// 랭킹에 등재되지 않았거나 조회 실패 시 -1 을 반환한다.
        /// </summary>
        public async Task<int> GetPlayerRankAsync()
        {
            try
            {
                // TODO: UGS SDK API 시그니처 실기 검증 필요
                //   2.3.3 기준: GetPlayerScoreAsync(string, GetPlayerScoreOptions) → LeaderboardEntry(SDK)
                //   현재 로그인된 플레이어 기준으로 조회되므로 playerId 인자는 필요 없다.
                var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(
                    LeaderboardId,
                    new GetPlayerScoreOptions { IncludeMetadata = false });

                if (entry == null)
                    return -1;

                // SDK Rank 는 0-base(0위=1등)일 수 있으므로 표시용 1-base 로 보정한다.
                // TODO: UGS SDK Rank 가 0-base 인지 1-base 인지 실기 검증 필요
                return entry.Rank + 1;
            }
            catch (Exception e)
            {
                // 점수가 없는 플레이어(전적 부족 등)는 예외가 던져질 수 있다 → 미등재로 간주.
                Debug.Log($"[LeaderboardService] 내 순위 없음 또는 조회 실패: {e.Message}");
                return -1;
            }
        }

        // ====================================================================
        // 변환 헬퍼
        // ====================================================================

        /// <summary>
        /// SDK 리더보드 엔트리를 우리 DTO(LeaderboardEntry)로 변환한다.
        /// 파라미터 타입을 명시하지 않고 dynamic-free 하게 접근하기 위해, 필요한 속성만 읽는다.
        /// </summary>
        /// <param name="sdkEntry">SDK 의 LeaderboardEntry(익명 var 로 전달).</param>
        private static LeaderboardEntry ConvertToDto(
            Unity.Services.Leaderboards.Models.LeaderboardEntry sdkEntry)
        {
            var dto = new LeaderboardEntry
            {
                // SDK Rank 0-base → 표시용 1-base 보정.
                // TODO: UGS SDK Rank base 실기 검증 필요
                Rank = sdkEntry.Rank + 1,
                PlayerId = sdkEntry.PlayerId,
                WinRate = (float)sdkEntry.Score
            };

            // 부가 정보(닉네임/전적)는 Metadata JSON 에서 파싱한다.
            ApplyMetadata(dto, sdkEntry.Metadata);
            return dto;
        }

        /// <summary>
        /// Metadata JSON 문자열을 파싱해 DTO 의 닉네임/전적 필드를 채운다.
        /// 파싱 실패 시 필드를 비워 두고(기본값) 넘어간다.
        /// </summary>
        private static void ApplyMetadata(LeaderboardEntry dto, string metadataJson)
        {
            if (string.IsNullOrEmpty(metadataJson))
                return;

            try
            {
                // JsonUtility 는 필드명이 일치하는 [Serializable] 클래스로만 역직렬화된다.
                var meta = JsonUtility.FromJson<RankMetadata>(metadataJson);
                if (meta != null)
                {
                    dto.Nickname = meta.nickname;
                    dto.NicknameCode = meta.nicknameCode;
                    dto.TotalGames = meta.totalGames;
                    dto.Wins = meta.wins;
                    dto.Losses = meta.losses;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardService] Metadata 파싱 실패: {e.Message}");
            }
        }

        // ====================================================================
        // Metadata 역직렬화용 내부 구조
        // ====================================================================

        /// <summary>
        /// Leaderboard 엔트리 Metadata(JSON) 역직렬화용 컨테이너.
        /// 필드명은 Cloud Code 가 기록하는 JSON 키와 정확히 일치해야 한다.
        /// // TODO: Cloud Code recordMatchResult 의 Metadata JSON 키와 일치 여부 실기 검증 필요
        /// </summary>
        [Serializable]
        private class RankMetadata
        {
            public string nickname;
            public string nicknameCode;
            public int totalGames;
            public int wins;
            public int losses;
        }
    }
}
