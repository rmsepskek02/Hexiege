// ============================================================================
// PlayerProfileService.cs
// UGS Cloud Save 를 통해 플레이어 프로필(닉네임/전적)을 읽고 쓰는 서비스.
//
// 역할:
//   - Cloud Save Player Data 에서 닉네임/코드/전적 필드를 로드한다.
//   - 닉네임/코드를 Cloud Save 에 저장한다.
//   - IPlayerProfileService(Application) 인터페이스를 구현한다(의존성 역전).
//
// 접근 방식(패키지: com.unity.services.cloudsave 3.4.1):
//   CloudSaveService.Instance.Data.Player.<API>
//   - LoadAsync(ISet<string> keys)  → Dictionary<string, Item>
//   - SaveAsync(IDictionary<string, object> data)
//
// 주의:
//   - UGS SDK 의 정확한 API 시그니처(반환 타입, Item 값 접근 방식 등)는 실기 검증이
//     필요하다. SDK 접근부는 아래 Load/Save 헬퍼 메서드로 감쌌으며,
//     불확실한 지점에 // TODO: UGS SDK API 시그니처 실기 검증 필요 주석을 남겼다.
//   - MonoBehaviour 가 아닌 일반 C# 클래스다(Cloud Save 는 네트워크 리소스 접근만 수행).
//
// Infrastructure 레이어 — 외부 서비스(UGS) 연동 담당.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// UGS Cloud Save 기반 플레이어 프로필 저장소.
    /// IPlayerProfileService(Application) 구현체.
    /// </summary>
    public class PlayerProfileService : IPlayerProfileService
    {
        // ====================================================================
        // Cloud Save 키 상수
        // ====================================================================
        //
        // 주의(문서 불일치): AuthSystemRules.md 닉네임 규칙 6 은 코드 필드명을 "code" 로
        //   기술하지만, 본 작업의 Plan.md 스키마는 "nicknameCode" 로 명시한다. 여기서는
        //   Plan.md 를 따른다. 실제 UGS Dashboard 스키마와 반드시 일치시켜야 하므로,
        //   Dashboard 확정 시 이 상수값을 최종 확인할 것.
        //   // TODO: UGS Dashboard Cloud Save 키 스키마와 값 일치 여부 실기 검증 필요

        private const string KeyNickname = "nickname";
        private const string KeyNicknameCode = "nicknameCode";
        private const string KeyTotalGames = "totalGames";
        private const string KeyWins = "wins";
        private const string KeyLosses = "losses";
        private const string KeyLastSessionEndAt = "lastSessionEndAt";
        private const string KeyHasUsedFreeNicknameChange = "hasUsedFreeNicknameChange";

        // ====================================================================
        // 공개 API (IPlayerProfileService 구현)
        // ====================================================================

        /// <summary>
        /// 현재 로그인된 플레이어의 프로필을 Cloud Save 에서 로드한다.
        /// 실패하거나 데이터가 없으면 필드가 비어 있는 PlayerProfileData 를 반환한다.
        /// </summary>
        public async Task<PlayerProfileData> LoadProfileAsync()
        {
            var profile = new PlayerProfileData();

            try
            {
                // 필요한 키만 지정해서 로드(전체 로드보다 트래픽 절약).
                var keys = new HashSet<string>
                {
                    KeyNickname, KeyNicknameCode, KeyTotalGames,
                    KeyWins, KeyLosses, KeyLastSessionEndAt, KeyHasUsedFreeNicknameChange
                };

                // TODO: UGS SDK API 시그니처 실기 검증 필요
                //   3.4.1 기준: Data.Player.LoadAsync(ISet<string>) → Dictionary<string, Item>
                Dictionary<string, Unity.Services.CloudSave.Models.Item> data =
                    await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                profile.Nickname = GetString(data, KeyNickname, string.Empty);
                profile.NicknameCode = GetString(data, KeyNicknameCode, string.Empty);
                profile.TotalGames = GetInt(data, KeyTotalGames, 0);
                profile.Wins = GetInt(data, KeyWins, 0);
                profile.Losses = GetInt(data, KeyLosses, 0);
                profile.LastSessionEndAt = GetString(data, KeyLastSessionEndAt, string.Empty);
                profile.HasUsedFreeNicknameChange = GetBool(data, KeyHasUsedFreeNicknameChange, false);
            }
            catch (Exception e)
            {
                // 최초 로그인(데이터 없음) 또는 네트워크 오류. 빈 프로필로 처리하고 로깅만 한다.
                Debug.LogWarning($"[PlayerProfileService] 프로필 로드 실패(빈 프로필 반환): {e.Message}");
            }

            return profile;
        }

        /// <summary>
        /// 닉네임과 코드를 Cloud Save 에 저장한다.
        /// </summary>
        public async Task SaveNicknameAsync(string nickname, string code)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    { KeyNickname, nickname },
                    { KeyNicknameCode, code }
                };

                // TODO: UGS SDK API 시그니처 실기 검증 필요
                //   3.4.1 기준: Data.Player.SaveAsync(IDictionary<string, object>)
                //   최종 닉네임 확정/코드 중복 방지는 서버(Cloud Code)에서 수행하는 것이 원칙이나,
                //   현재는 클라이언트 Cloud Save 직접 저장으로 구현한다.
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                Debug.Log($"[PlayerProfileService] 닉네임 저장 완료: {nickname}#{code}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerProfileService] 닉네임 저장 실패: {e.Message}");
                throw; // 상위(UseCase/View)가 실패를 인지하고 안내하도록 재던진다.
            }
        }

        // ====================================================================
        // Cloud Save Item 파싱 헬퍼
        // ====================================================================
        //
        // Item.Value 는 저장된 원본 값을 여러 타입으로 변환해 주는 래퍼다.
        //   - GetAs<T>() 로 지정 타입 변환
        //   - GetAsString() 으로 문자열 변환
        // SDK 버전별로 접근 방식이 다를 수 있으므로 예외를 방어적으로 처리한다.
        // TODO: UGS SDK API 시그니처 실기 검증 필요 (Item.Value.GetAs<T>() 존재 여부/시그니처)

        /// <summary>지정 키의 문자열 값을 반환한다. 없거나 변환 실패 시 기본값.</summary>
        private static string GetString(
            Dictionary<string, Unity.Services.CloudSave.Models.Item> data, string key, string fallback)
        {
            if (data == null || !data.TryGetValue(key, out var item) || item?.Value == null)
                return fallback;

            try { return item.Value.GetAsString(); }
            catch { return fallback; }
        }

        /// <summary>지정 키의 정수 값을 반환한다. 없거나 변환 실패 시 기본값.</summary>
        private static int GetInt(
            Dictionary<string, Unity.Services.CloudSave.Models.Item> data, string key, int fallback)
        {
            if (data == null || !data.TryGetValue(key, out var item) || item?.Value == null)
                return fallback;

            try { return item.Value.GetAs<int>(); }
            catch
            {
                // 숫자가 문자열로 저장된 경우를 대비한 폴백 파싱.
                try { return int.TryParse(item.Value.GetAsString(), out int v) ? v : fallback; }
                catch { return fallback; }
            }
        }

        /// <summary>지정 키의 bool 값을 반환한다. 없거나 변환 실패 시 기본값.</summary>
        private static bool GetBool(
            Dictionary<string, Unity.Services.CloudSave.Models.Item> data, string key, bool fallback)
        {
            if (data == null || !data.TryGetValue(key, out var item) || item?.Value == null)
                return fallback;

            try { return item.Value.GetAs<bool>(); }
            catch
            {
                try { return bool.TryParse(item.Value.GetAsString(), out bool v) ? v : fallback; }
                catch { return fallback; }
            }
        }
    }
}
