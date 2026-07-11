// ============================================================================
// PlayerProfileUseCase.cs
// 닉네임 초기화 / 조회 / 전적 조회를 담당하는 Application 레이어 UseCase.
//
// 역할:
//   - 프로필(닉네임/전적) 로드
//   - 닉네임 유효성 검증 후 저장(코드 자동 생성)
//   - 스킵 시 자동 닉네임 생성
//   - 최초 로그인 여부 판정(Cloud Save 에 nickname 없음)
//
// 의존성:
//   - IPlayerProfileService(Application 인터페이스). 구현은 Infrastructure 의
//     PlayerProfileService(UGS Cloud Save). 의존성 역전으로 Infrastructure 역참조 없음.
//
// Application 레이어 — 순수 C# + Task.
// ============================================================================

using System;
using System.Text;
using System.Threading.Tasks;

namespace Hexiege.Application
{
    /// <summary>
    /// 닉네임 검증 결과.
    /// View 가 결과별 안내 메시지를 표시하는 데 사용한다.
    /// </summary>
    public enum NicknameValidation
    {
        /// <summary>유효한 닉네임.</summary>
        Valid,
        /// <summary>빈 값.</summary>
        Empty,
        /// <summary>길이 규칙 위반(한글 2~12자 / 영문·숫자 2~24자).</summary>
        LengthOutOfRange,
        /// <summary>허용되지 않는 특수문자 포함.</summary>
        InvalidCharacter
    }

    /// <summary>
    /// 플레이어 프로필/닉네임 관련 UseCase.
    /// </summary>
    public class PlayerProfileUseCase
    {
        // ====================================================================
        // 닉네임 규칙 상수 (AuthSystemRules.md 닉네임 규칙 1)
        // ====================================================================

        /// <summary>공통 최소 길이.</summary>
        private const int MinLength = 2;

        /// <summary>한글이 포함된 경우 최대 길이.</summary>
        private const int MaxLengthKorean = 12;

        /// <summary>영문/숫자만인 경우 최대 길이.</summary>
        private const int MaxLengthAlnum = 24;

        // ====================================================================
        // 의존성
        // ====================================================================

        private readonly IPlayerProfileService _profileService;

        // 자동 닉네임 접미사/코드 생성용 난수. Application 은 메인 스레드 종속이 아니므로 System.Random 사용.
        private static readonly Random _rng = new Random();

        /// <summary>
        /// 생성자. IPlayerProfileService 를 주입받는다(Bootstrap 이 구현체를 넣어준다).
        /// </summary>
        public PlayerProfileUseCase(IPlayerProfileService profileService)
        {
            _profileService = profileService;
        }

        // ====================================================================
        // 조회
        // ====================================================================

        /// <summary>
        /// 현재 플레이어의 프로필(닉네임/전적)을 로드한다.
        /// </summary>
        public Task<PlayerProfileData> LoadProfileAsync()
        {
            return _profileService.LoadProfileAsync();
        }

        /// <summary>
        /// 최초 로그인 여부를 판정한다.
        /// Cloud Save 에 nickname 값이 없거나 비어 있으면 true(최초 로그인).
        /// </summary>
        public async Task<bool> IsFirstLogin()
        {
            PlayerProfileData profile = await _profileService.LoadProfileAsync();
            return profile == null || !profile.HasNickname;
        }

        // ====================================================================
        // 저장
        // ====================================================================

        /// <summary>
        /// 닉네임을 검증한 뒤 4자리 코드를 자동 생성하여 저장한다.
        /// 검증 실패 시 저장하지 않고 실패 결과를 반환한다.
        /// </summary>
        /// <param name="nickname">사용자가 입력한 닉네임.</param>
        /// <returns>검증 결과. Valid 이면 저장까지 완료된 것이다.</returns>
        public async Task<NicknameValidation> SaveNicknameAsync(string nickname)
        {
            NicknameValidation validation = ValidateNickname(nickname);
            if (validation != NicknameValidation.Valid)
                return validation;

            // 4자리 코드 자동 생성.
            //   최종 저장 및 같은 닉네임 내 코드 중복 방지는 서버(Cloud Code)에서 처리하는 것이
            //   원칙이나, 현재는 클라이언트에서 임의 코드를 생성해 Cloud Save 에 직접 저장한다.
            //   // TODO: 서버(Cloud Code)에서 코드 중복 없는 최종 할당으로 대체 필요
            string code = GenerateCode();

            await _profileService.SaveNicknameAsync(nickname.Trim(), code);
            return NicknameValidation.Valid;
        }

        /// <summary>
        /// 자동 생성 닉네임(스킵용)을 만들어 저장한다.
        /// 형식: {prefix}_{임의문자숫자} (예: 구글_a3F9kd)
        /// </summary>
        /// <param name="prefix">접두사(예: "구글", "사용자", "게스트").</param>
        public async Task SaveAutoNicknameAsync(string prefix)
        {
            string auto = GenerateAutoNickname(prefix);
            string code = GenerateCode();
            await _profileService.SaveNicknameAsync(auto, code);
        }

        // ====================================================================
        // 생성 헬퍼
        // ====================================================================

        /// <summary>
        /// 자동 닉네임을 생성한다. 형식: {prefix}_{임의 영숫자 6자}.
        /// </summary>
        public string GenerateAutoNickname(string prefix)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var sb = new StringBuilder();
            for (int i = 0; i < 6; i++)
                sb.Append(chars[_rng.Next(chars.Length)]);

            string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "사용자" : prefix;
            return $"{safePrefix}_{sb}";
        }

        /// <summary>0000~9999 범위의 4자리 코드 문자열을 생성한다.</summary>
        private static string GenerateCode()
        {
            return _rng.Next(0, 10000).ToString("D4");
        }

        // ====================================================================
        // 검증 (AuthSystemRules.md 닉네임 규칙 1)
        // ====================================================================

        /// <summary>
        /// 닉네임 유효성을 검증한다.
        ///   - 빈 값 불가
        ///   - 허용 문자: 한글(가-힣), 영문(a-zA-Z), 숫자(0-9). 그 외 특수문자 불가.
        ///   - 길이: 한글 포함 시 2~12자, 영문/숫자만이면 2~24자(혼용은 한글 규칙 적용).
        /// </summary>
        public NicknameValidation ValidateNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return NicknameValidation.Empty;

            string trimmed = nickname.Trim();

            bool hasKorean = false;
            foreach (char c in trimmed)
            {
                if (IsKorean(c))
                {
                    hasKorean = true;
                }
                else if (!IsAlnum(c))
                {
                    // 한글/영문/숫자가 아닌 문자 → 특수문자(공백 포함)로 간주.
                    return NicknameValidation.InvalidCharacter;
                }
            }

            int len = trimmed.Length;
            int max = hasKorean ? MaxLengthKorean : MaxLengthAlnum;
            if (len < MinLength || len > max)
                return NicknameValidation.LengthOutOfRange;

            return NicknameValidation.Valid;
        }

        /// <summary>한글 완성형 음절(가-힣) 여부.</summary>
        private static bool IsKorean(char c) => c >= '가' && c <= '힣';

        /// <summary>영문 또는 숫자 여부.</summary>
        private static bool IsAlnum(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }
}
