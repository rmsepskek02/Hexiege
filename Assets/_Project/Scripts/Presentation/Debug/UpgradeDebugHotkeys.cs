// ============================================================================
// UpgradeDebugHotkeys.cs
// 연구소 유닛 강화 시스템을 UI 없이 빠르게 검증하기 위한 디버그 핫키 컴포넌트.
//
// 사용법:
//   에디터 메뉴 Hexiege > Setup > Add Upgrade Debug Hotkeys (Game) 로 부착하거나,
//   임의의 씬 GameObject 에 직접 부착한다. 플레이 모드에서:
//     F1 → 지정 팀/그룹의 공격력(Attack)  레벨 +1
//     F2 → 지정 팀/그룹의 방어력(Defense)  레벨 +1
//     F3 → 지정 팀/그룹의 이동속도(MoveSpeed) 레벨 +1
//     F4 → 지정 팀의 자연회복(Regen) 레벨 +1 (그룹 무관)
//   레벨은 UnitUpgradeUseCase 를 통해 즉시 반영되며 UpgradeGroupHelper.MaxLevel 로 클램프된다.
//
// ⚠️ 디버그 전용. 실제 연구(골드 차감/타이머)를 거치지 않고 레벨만 직접 올리므로
//    밸런스 검증이 아니라 "강화 효과가 전투/이동/힐에 반영되는지" 확인용이다.
//    빌드 출시 전 오브젝트에서 제거할 것(플레이어 빌드에서는 입력 로직이 컴파일 제외됨).
//
// Presentation 레이어 — MonoBehaviour. Application(GameServicesLocator)만 참조.
// ============================================================================

using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;              // 프로젝트 표준 New Input System
#endif
using Hexiege.Domain;                       // TeamId, UpgradeGroup, UnitUpgradeStat, UpgradeGroupHelper
using Hexiege.Application;                  // UnitUpgradeUseCase, IGameServices, GameServicesLocator

namespace Hexiege.Presentation
{
    /// <summary>
    /// F1~F4 로 특정 팀/그룹의 강화 트랙 레벨을 +1 하는 디버그 핫키(에디터/개발 전용).
    /// </summary>
    public class UpgradeDebugHotkeys : MonoBehaviour
    {
        [Header("대상 트랙(디버그)")]
        [Tooltip("레벨을 올릴 팀. 보통 로컬 플레이어 팀(Blue)을 검증한다.")]
        [SerializeField] private TeamId _team = TeamId.Blue;

        [Tooltip("F1~F3 이 적용될 강화 그룹. 팀 종족에 존재하는 그룹이어야 효과가 나타난다. " +
                 "(예: 인간이면 HumanMelee/HumanRanged/HumanVehicle)")]
        [SerializeField] private UpgradeGroup _group = UpgradeGroup.HumanMelee;

        private void Update()
        {
#if UNITY_EDITOR
            // New Input System — 키보드 미연결(모바일 등) 시 조용히 무시.
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) BumpLevel(_group, UnitUpgradeStat.Attack);
            if (kb.f2Key.wasPressedThisFrame) BumpLevel(_group, UnitUpgradeStat.Defense);
            if (kb.f3Key.wasPressedThisFrame) BumpLevel(_group, UnitUpgradeStat.MoveSpeed);
            // 자연회복은 그룹 무관 — 정규 그룹 키로 올린다(UseCase 내부 정규화와 일치).
            if (kb.f4Key.wasPressedThisFrame)
                BumpLevel(UpgradeGroupHelper.RegenCanonicalGroup, UnitUpgradeStat.Regen);
#endif
        }

        /// <summary>
        /// 지정 트랙의 현재 레벨을 조회해 +1(최대 레벨 클램프)로 직접 설정한다.
        /// UnitUpgradeUseCase 는 GameServicesLocator(조합 루트 등록)를 통해 얻는다.
        /// </summary>
        private void BumpLevel(UpgradeGroup group, UnitUpgradeStat stat)
        {
            IGameServices services = GameServicesLocator.Current;
            if (services == null)
            {
                Debug.LogWarning("[UpgradeDebug] GameServicesLocator 가 아직 등록되지 않았습니다(맵 로드 전?).");
                return;
            }

            UnitUpgradeUseCase upgrade = services.GetUpgradeUseCase();
            if (upgrade == null)
            {
                Debug.LogWarning("[UpgradeDebug] UnitUpgradeUseCase 가 아직 생성되지 않았습니다.");
                return;
            }

            int current = upgrade.GetLevel(_team, group, stat);
            int next = Mathf.Min(current + 1, UpgradeGroupHelper.MaxLevel);
            if (next == current)
            {
                Debug.Log($"[UpgradeDebug] {_team}/{group}/{stat} 이미 최대 레벨({current}).");
                return;
            }

            upgrade.SetLevel(_team, group, stat, next);
            Debug.Log($"[UpgradeDebug] {_team}/{group}/{stat} 레벨 {current} → {next}.");
        }
    }
}
