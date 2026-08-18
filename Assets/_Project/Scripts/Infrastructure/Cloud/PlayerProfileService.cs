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
                //
                // [운영] 축 A: 빈 프로필로 폴백해 화면은 계속 뜬다 → 복구됨 → Warn.
                //   축 B: ① UGS 회선·계정 상태에 좌우돼 개발 기기에서 재현되지 않는다.
                //         ② 호출부는 빈 PlayerProfileData 만 받아 "왜 비었는지"를 알 수 없고,
                //            화면에도 아무 안내가 뜨지 않는다 → 이 로그가 유일한 기록이다.
                //   예외 객체를 그대로 넘기는 이유: 예외 "타입"이 텔레메트리의 핵심 축이라
                //   "데이터 없음(최초 로그인)"과 "네트워크 오류"를 집계에서 구분할 수 있다(1.9).
                GameLog.Ops.Warn(LogEvent.CloudSaveOperationFailed, "Cloud", nameof(PlayerProfileService),
                                 "프로필 로드 실패 — 빈 프로필을 반환한다", e,
                                 "Operation=LoadProfile");
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
                // [개발] 성공 통보 → Info + 개발.
                //   ⚠️ 닉네임은 그대로 남긴다 — LogRules.md 1.6 이 규정한 세 항목
                //      (이메일 / UID·PlayerId / 토큰) 어디에도 해당하지 않고,
                //      유저가 게임 안에서 스스로 공개하는 이름이기 때문이다(사용자 결정, 2026-08-18).
                GameLog.Dev.Info("Cloud", nameof(PlayerProfileService), "닉네임 저장 완료",
                                 $"Nickname={nickname}#{code}");
            }
            catch (Exception e)
            {
                // [운영] ⚠️ 잠정 판정 — Warn + 운영.
                //   축 A: 저장은 실패했지만 예외를 다시 던져 화면이 "다시 시도하세요"를 띄우고,
                //         사용자는 같은 화면에서 곧바로 재시도할 수 있다 → 복구 경로 있음 → Warn.
                //         (원본은 LogError 였지만 축 A 의 기준은 "복구되었나?" 하나뿐이다 — 1.2)
                //   축 B: "Cloud Save 저장이 실패했다"는 맥락과 예외 타입을 아는 계층은 여기뿐이다.
                //         상위(NicknameSetupView)는 같은 예외를 받지만 무엇이 실패했는지 모른다.
                //
                //   ⚠️ 범위 밖 후속 항목: Presentation/UI/Views/Login/NicknameSetupView.cs 도
                //      같은 사건을 옛 방식(콘솔 전용)으로 남긴다. 그 파일은 배치 3 대상이며,
                //      이관 시 개발(GameLog.Dev)로 내려야 집계가 두 배로 부풀지 않는다(1.14 금지 9).
                GameLog.Ops.Warn(LogEvent.CloudSaveOperationFailed, "Cloud", nameof(PlayerProfileService),
                                 "닉네임 저장 실패 — 예외를 그대로 다시 던져 화면이 재시도를 안내한다", e,
                                 "Operation=SaveNickname");
                throw; // 상위(UseCase/View)가 실패를 인지하고 안내하도록 재던진다.
            }
        }

        /// <summary>
        /// 닉네임/코드와 함께 "무료 닉네임 변경 사용 여부"를 Cloud Save 에 저장한다.
        ///   - 닉네임 변경(무료 1회) 흐름에서 호출된다.
        ///   - KeyHasUsedFreeNicknameChange 는 그동안 읽기(LoadProfileAsync)만 되고
        ///     저장 경로가 없던 키였다(Research 2-3). 이 오버로드에서 처음으로 함께 저장한다.
        /// </summary>
        public async Task SaveNicknameAsync(string nickname, string code, bool hasUsedFreeNicknameChange)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    { KeyNickname, nickname },
                    { KeyNicknameCode, code },
                    { KeyHasUsedFreeNicknameChange, hasUsedFreeNicknameChange }
                };

                // TODO: UGS SDK API 시그니처 실기 검증 필요
                //   3.4.1 기준: Data.Player.SaveAsync(IDictionary<string, object>)
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                // [개발] 위 오버로드와 같은 사건이므로 판정도 같다(Info + 개발). 닉네임은 그대로 남긴다.
                GameLog.Dev.Info("Cloud", nameof(PlayerProfileService), "닉네임 저장 완료",
                                 $"Nickname={nickname}#{code}, HasUsedFreeNicknameChange={hasUsedFreeNicknameChange}");
            }
            catch (Exception e)
            {
                // [운영] 위 오버로드와 완전히 같은 사건 — 같은 키·같은 축 값·같은 Operation 값을 쓴다.
                //   같은 사건에 다른 값을 주면 한 지표가 조용히 둘로 갈라진다(1.4).
                GameLog.Ops.Warn(LogEvent.CloudSaveOperationFailed, "Cloud", nameof(PlayerProfileService),
                                 "닉네임 저장 실패 — 예외를 그대로 다시 던져 화면이 재시도를 안내한다", e,
                                 "Operation=SaveNickname");
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
            catch (Exception e)
            {
                // [운영 · 신규 추가] 삼킨 예외 — LogRules.md 1.3 분류 원칙 4 가 "반드시 운영 로그"를 요구하고,
                //   1.2 조합표가 바로 이 자리를 Warn + 운영 으로 이미 판정해 두었다.
                //   여기에 로그가 없으면 변환이 실패해도 프로필이 "멀쩡한 기본값"으로 보여
                //   실패했다는 사실 자체가 아무 데도 남지 않는다.
                //
                //   ⚠️ catch 를 catch (Exception e) 로 바꾼 것은 예외 객체를 로그에 넘기기 위한
                //      최소 변경이며, return fallback 동작은 그대로다.
                GameLog.Ops.Warn(LogEvent.CloudSaveValueParseFailed, "Cloud", nameof(PlayerProfileService),
                                 "Cloud Save 값 변환 실패 — 기본값을 사용한다", e,
                                 $"Key={key}, Type=String, Fallback=Default");
                return fallback;
            }
        }

        /// <summary>지정 키의 정수 값을 반환한다. 없거나 변환 실패 시 기본값.</summary>
        private static int GetInt(
            Dictionary<string, Unity.Services.CloudSave.Models.Item> data, string key, int fallback)
        {
            if (data == null || !data.TryGetValue(key, out var item) || item?.Value == null)
                return fallback;

            try { return item.Value.GetAs<int>(); }
            catch (Exception e)
            {
                // [운영 · 신규 추가] 1차 변환(GetAs<int>) 실패 지점. 여기 한 곳에서만 로그를 남긴다.
                //
                // ⚠️ 왜 안쪽 catch 가 아니라 바깥 catch 에서 남기는가 (초급 개발자용 설명):
                //    이 메서드가 기본값으로 떨어지는 길은 세 갈래인데,
                //      ① GetAsString() 이 예외를 던짐        → 안쪽 catch 로 감
                //      ② GetAsString() 은 되는데 int.TryParse 가 false → **아무 catch 도 타지 않음**
                //      ③ int.TryParse 가 성공             → 문자열 폴백으로 복구됨
                //    안쪽 catch 에만 로그를 달면 ②가 통째로 누락된다. 반면 바깥 catch 는
                //    ①②③ 을 전부 지나가므로 "1차 변환이 깨졌다"는 사실을 한 번도 놓치지 않는다.
                //    그리고 사건 하나에 로그 한 줄이라 집계가 두 배로 부풀지 않는다(1.14 금지 사항 9).
                //
                //    축 A 가 Warn 인 이유도 여기서 갈리지 않는다 — 문자열 폴백으로 복구되든
                //    기본값으로 떨어지든 "대체 경로로 계속 진행"이므로 둘 다 Warn 이다(1.2).
                //
                //    ⚠️ catch → catch (Exception e) 는 예외 객체를 넘기기 위한 최소 변경이고,
                //       아래 폴백 파싱과 return 동작은 한 글자도 바뀌지 않았다.
                GameLog.Ops.Warn(LogEvent.CloudSaveValueParseFailed, "Cloud", nameof(PlayerProfileService),
                                 "Cloud Save 값 변환 실패 — 문자열 폴백 파싱을 시도한다", e,
                                 $"Key={key}, Type=Int32, Fallback=StringParse");

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
            catch (Exception e)
            {
                // [운영 · 신규 추가] GetInt 와 완전히 같은 구조·같은 사건이므로
                //   같은 키·같은 축 값·같은 로그 위치(바깥 catch)를 쓴다. 근거는 GetInt 주석 참조.
                GameLog.Ops.Warn(LogEvent.CloudSaveValueParseFailed, "Cloud", nameof(PlayerProfileService),
                                 "Cloud Save 값 변환 실패 — 문자열 폴백 파싱을 시도한다", e,
                                 $"Key={key}, Type=Boolean, Fallback=StringParse");

                try { return bool.TryParse(item.Value.GetAsString(), out bool v) ? v : fallback; }
                catch { return fallback; }
            }
        }
    }
}
