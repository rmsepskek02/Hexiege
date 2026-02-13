// Assets/_Project/Scripts/Domain/Common/IDamageable.cs

namespace Hexiege.Domain
{
    /// <summary>
    /// 유닛, 건물을 포함하여 HP를 가지고 피해를 받을 수 있는 모든 객체에 대한 인터페이스
    /// </summary>
    public interface IDamageable
    {
        int Id { get; }
        TeamId Team { get; }
        HexCoord Position { get; }
        int Hp { get; }
        int MaxHp { get; }
        bool IsAlive { get; }

        /// <summary>
        /// 이 객체에 데미지를 적용합니다.
        /// </summary>
        /// <param name="damage">받을 데미지 양</param>
        void TakeDamage(int damage);
    }
}
