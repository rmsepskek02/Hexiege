// ============================================================================
// IPlayerProfileService.cs
// 플레이어 프로필(닉네임/전적) 저장소 접근 추상화 인터페이스.
//
// 이 인터페이스가 필요한 이유:
//   실제 저장소 구현(PlayerProfileService)은 UGS Cloud Save 를 사용하는
//   Infrastructure 레이어 클래스다. Application 레이어의 UseCase 가 그 구체 클래스를
//   직접 참조하면 Application → Infrastructure 역방향 의존이 생긴다(MEMORY.md 제약).
//   이를 막기 위해 UseCase 는 이 인터페이스에만 의존하고, Infrastructure 가 구현한다
//   (의존성 역전 — 인터페이스는 Application, 구현은 Infrastructure).
//
// Application 레이어 — 순수 C# + Task.
// ============================================================================

using System.Threading.Tasks;

namespace Hexiege.Application
{
    /// <summary>
    /// 플레이어 프로필 데이터를 읽고 쓰는 저장소 추상화.
    /// Infrastructure 의 PlayerProfileService(UGS Cloud Save)가 이 인터페이스를 구현한다.
    /// </summary>
    public interface IPlayerProfileService
    {
        /// <summary>
        /// 현재 로그인된 플레이어의 프로필(닉네임/코드/전적)을 로드한다.
        /// 저장된 데이터가 없으면 필드가 비어 있는 PlayerProfileData 를 반환한다(null 아님).
        /// </summary>
        Task<PlayerProfileData> LoadProfileAsync();

        /// <summary>
        /// 닉네임과 코드를 Cloud Save 에 저장한다.
        /// </summary>
        /// <param name="nickname">저장할 닉네임(검증은 UseCase 에서 선행).</param>
        /// <param name="code">4자리 숫자 코드.</param>
        Task SaveNicknameAsync(string nickname, string code);

        /// <summary>
        /// 닉네임/코드와 함께 "무료 닉네임 변경 사용 여부(hasUsedFreeNicknameChange)"를 저장한다.
        ///   - 닉네임 변경(무료 1회) 흐름에서 사용한다. 변경을 저장하는 동시에
        ///     무료 소진 플래그를 true 로 함께 기록하기 위한 오버로드다.
        ///   - 기존 2인자 오버로드는 최초 닉네임 설정(플래그 변경 불필요)용으로 그대로 유지된다.
        /// </summary>
        /// <param name="nickname">저장할 닉네임(검증은 UseCase 에서 선행).</param>
        /// <param name="code">4자리 숫자 코드(변경 시에도 기존 값을 유지해 전달).</param>
        /// <param name="hasUsedFreeNicknameChange">무료 닉네임 변경을 사용했는지 여부.</param>
        Task SaveNicknameAsync(string nickname, string code, bool hasUsedFreeNicknameChange);
    }
}
