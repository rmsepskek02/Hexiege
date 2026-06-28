// ============================================================================
// IUnitFactory.cs
// 유닛 생성·조회·뷰 초기화를 담당하는 팩토리의 인터페이스.
//
// 이 인터페이스가 필요한 이유:
//   Infrastructure 레이어(NetworkXxx.cs)가 UnitFactory(Infrastructure)의
//   구체 클래스를 직접 참조하면 같은 레이어 안에서만 결합되므로 괜찮지만,
//   IGameServices(Application 계층 인터페이스)가 UnitFactory를 반환 타입으로
//   노출하면 Application→Infrastructure 역방향 의존이 생긴다.
//   이를 막기 위해 필요한 메서드만 이 인터페이스로 추상화하고,
//   IGameServices는 IUnitFactory를 반환하도록 한다.
//
// Application 레이어 — UnityEngine.GameObject 참조 허용.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛 팩토리 추상화. Infrastructure의 UnitFactory가 이 인터페이스를 구현한다.
    /// Network 계층에서 IGameServices를 통해 팩토리에 접근할 때 사용.
    /// </summary>
    public interface IUnitFactory
    {
        /// <summary>
        /// 유닛 ID에 해당하는 씬 내 GameObject를 반환한다.
        /// 해당 유닛 GameObject가 없으면 null.
        /// </summary>
        GameObject GetUnitObject(int unitId);

        /// <summary>
        /// 유닛 ID와 GameObject를 팩토리 내부 테이블에 등록한다.
        /// NGO가 프리팹을 스폰한 직후 NetworkUnit.OnNetworkSpawn에서 호출한다.
        /// </summary>
        void RegisterUnitObject(int unitId, GameObject unitObj);

        /// <summary>
        /// UnitData에 맞는 UnitView(시각적 표현)를 초기화한다.
        /// 성공 여부를 bool로 반환한다.
        /// </summary>
        bool InitializeUnitView(UnitData unitData);
    }
}
