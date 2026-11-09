// ============================================================================
// BuildingStatusEffectSystem.cs
// 건물(지금은 방어탑)에 걸리는 상태효과를 서버 권위로 부여 · 갱신 · 만료 · 정리하는 서비스.
// 2026-11-04 EMP Task 커밋 1(Plan 3-2 B1)에서 새로 만들었다.
//
// 무엇을 하나(유니티 초급자용 설명):
//   인간 지원 스킬 EMP를 쓰면, 조준한 원 안에 있던 적 방어탑이 몇 초 동안 공격을 멈춘다.
//   "어떤 탑이 앞으로 몇 초 더 멈춰 있는가"를 기억해 두는 곳이 이 클래스다.
//     · 걸기   : DisableEnemyTowersInRadius — 발동 순간 원 안의 적 탑을 골라 "공격 불가"를 건다.
//     · 묻기   : CanAttack — 방어탑 공격 UseCase(TowerCombatUseCase.Tick)가 쏘기 직전에 물어본다.
//     · 줄이기 : Tick — 권위 틱마다 남은 시간을 줄이고, 0 이하가 된 탑은 목록에서 뺀다(= 다시 쏜다).
//     · 빼기   : Remove — 탑이 파괴 · 철거되면 그 탑의 기록을 지운다(누수 방지).
//     · 닫기   : Close — 경기가 끝나면 전부 비우고, 이후 새로 거는 것도 받지 않는다.
//
// 왜 유닛 상태 시스템(StatusEffectSystem)을 같이 쓰지 않는가(Research 4-1):
//   ① 유닛 Id와 건물 Id는 서로 다른 카운터에서 나와 "같은 숫자"가 될 수 있다.
//      한 딕셔너리에 섞으면 유닛 5번의 빙결이 건물 5번에도 걸린 것처럼 읽힌다.
//   ② 유닛 상태 틱은 멀티 순수 클라이언트의 "미러"에서도 돈다(GameBootstrapper.Update).
//      탑 무력화는 서버만 판정하므로, 클라이언트에서 도는 틱 묶음에 얹으면 안 된다.
//   그래서 건물 Id 키를 쓰는 별도 저장소를 두고, 틱도 권위 전용 자리(싱글 Update · 멀티 TickCombat)에서만 돌린다.
//
// 값은 기존 Domain 타입을 그대로 쓴다:
//   상태 1건 = StatusEffect(종류 AttackDisabled · 강도 0 · 남은 시간 · 건 팀). EMP는 종류가 하나뿐이라
//   건물마다 StatusEffect 1개만 들면 충분하다(유닛처럼 종류별 목록을 둘 필요가 없다 — Plan 3-2 제안).
//
// 대상 판정(Plan 3-2 표):
//   적 팀(시전 팀이 아닌 건물) · 살아 있는 · BuildingType.AutoTower만.
//   거리 = "탑이 서 있는 타일의 중심" ↔ "조준점" (도메인 월드 XZ 평면).
//   타입 A(포격) 건물 판정(UnitCombatUseCase.CollectEnemyBuildingsInRadiusDomain)과 같은 기준이라
//   화면의 조준 원과 실제 결과가 맞는다. 탑 모델 가장자리만 원에 걸치면 대상이 아니다(R-13, 의도된 일관성).
//
// 다시 걸림(Plan 3-2):
//   같은 탑에 다시 걸면 남은 시간을 "새 지속 시간"으로 교체한다(UnitStatusState.ApplyOrRefresh 선례).
//   스킬이 EMP 하나뿐이라 "교체"와 "더 긴 쪽"의 결과가 같다. 다른 탑은 각자 따로 센다.
//
// 회귀 안전(핵심 — StatusEffectSystem과 같은 원칙):
//   걸린 탑이 하나도 없으면 CanAttack은 딕셔너리 조회도 하지 않고 바로 true를 돌려준다.
//   → EMP를 쓰기 전에는 방어탑 동작이 이 클래스가 없던 때와 완전히 같다.
//
// 이 단계(커밋 1)에서 하지 않는 것:
//   클라이언트 알림(확정 / 수신 이벤트 · ClientRpc)과 화면 이펙트(V6)는 다음 커밋(2 · 3)이 붙인다.
//   그래서 DisableEnemyTowersInRadius는 "실제로 무력화한 탑 Id 목록"을 호출자에게 채워 돌려주기만 한다.
//
// Application 레이어 — Domain · Application 의존. UnityEngine은 Vector3 계산에만 쓴다(Unity 의존 허용,
//   SkillActivationContext · MistShrineUseCase 선례). Netcode · Core · Infrastructure 참조 없음.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 건물 Id별 상태효과(지금은 EMP 공격 불가)를 서버 권위로 부여·갱신·만료·정리하고,
    /// 방어탑 공격 직전 관문(CanAttack)에 답하는 서비스.
    /// </summary>
    public sealed class BuildingStatusEffectSystem
    {
        // ── 의존성(조합 루트에서 주입) ──────────────────────────────────────
        // 건물 목록(종류 · 팀 · 생존 · 타일 좌표) 조회용. 맵을 불러올 때마다 이 서비스와 함께 새로 만들어진다.
        private readonly BuildingPlacementUseCase _buildingPlacement;
        // 타일 좌표 → 도메인 월드 좌표 변환(타일 중심 계산). Application이 Core(HexMetrics)를 모르게 하는 인터페이스.
        private readonly IHexCoordinateMapper _mapper;

        // ── 상태 ───────────────────────────────────────────────────────────
        // 건물 Id → 그 건물에 걸린 "공격 불가" 상태 1건. 걸린 건물만 항목을 갖는다(없으면 = 정상).
        private readonly Dictionary<int, StatusEffect> _attackDisabled = new Dictionary<int, StatusEffect>();

        // Tick 중 딕셔너리를 순회하면서 지우면 예외가 나므로, 키를 먼저 복사해 두는 재사용 버퍼.
        //   서버 / 싱글은 한 스레드에서만 돌기 때문에 버퍼를 계속 재사용해도 안전하다(GC 절감).
        private readonly List<int> _tickKeyBuffer = new List<int>(8);
        private readonly List<int> _expiredKeyBuffer = new List<int>(8);

        // 경기 종료로 닫혔는지. 닫힌 뒤에는 새로 걸지 않는다(ClosePendingImpacts 선례 — Plan 3-2 「경기 종료」).
        private bool _closed;

        /// <summary>
        /// 건물 상태 서비스 생성. 조합 루트(GameBootstrapper.Setup)가 맵을 불러올 때마다 새로 만든다.
        /// </summary>
        /// <param name="buildingPlacement">건물 목록 조회 UseCase. null이면 범위 무력화가 아무 탑도 찾지 못한다(안전).</param>
        /// <param name="mapper">타일 → 도메인 월드 좌표 변환기. null이면 범위 무력화가 아무 탑도 찾지 못한다(안전).</param>
        public BuildingStatusEffectSystem(BuildingPlacementUseCase buildingPlacement, IHexCoordinateMapper mapper)
        {
            _buildingPlacement = buildingPlacement;
            _mapper = mapper;
        }

        // ====================================================================
        // 걸기
        // ====================================================================

        /// <summary>
        /// (서버 권위) 조준 중심의 원형 반경 안에 있는 적 방어탑을 지금 골라, 지속시간 동안 공격 불가로 만든다.
        /// 대상은 이 호출 순간에 정해진다(발동 순간 스냅샷 — 상위 결정 22). 이미 걸린 탑은 남은 시간을 새 값으로 교체한다.
        /// </summary>
        /// <param name="casterTeam">스킬을 쓴 팀. 이 팀의 탑은 대상에서 빠진다(아군 탑 무영향).</param>
        /// <param name="center">조준 중심(도메인 월드 좌표, XZ 평면 — Y는 무시한다).</param>
        /// <param name="radius">원형 반경(월드 단위). 0 이하 · NaN이면 아무 탑도 걸리지 않는다.</param>
        /// <param name="duration">무력화 시간(초). 0 이하 · NaN이면 아무 탑도 걸리지 않는다.</param>
        /// <param name="disabledTowerIds">
        /// 이번에 실제로 무력화(또는 갱신)한 탑 Id를 담을 목록. 호출자가 넘기면 이 메서드가 먼저 비우고 채운다.
        /// null이면 채우지 않는다. (다음 커밋에서 클라이언트 알림에 이 목록을 쓴다.)
        /// </param>
        /// <returns>이번에 무력화(또는 갱신)한 탑 수.</returns>
        public int DisableEnemyTowersInRadius(TeamId casterTeam, Vector3 center, float radius, float duration,
                                              List<int> disabledTowerIds)
        {
            disabledTowerIds?.Clear();

            // 경기가 끝났거나 · 값이 잘못됐거나 · 의존성이 없으면 아무것도 하지 않는다(방어적).
            //   !(x > 0f) 형태는 NaN도 함께 걸러낸다(NaN은 어떤 비교도 false).
            if (_closed || !(radius > 0f) || !(duration > 0f)) return 0;
            if (_buildingPlacement == null || _mapper == null) return 0;

            // XZ 평면 거리로만 판정한다(조준점 Y가 0이 아니어도 결과가 같게).
            Vector3 centerFlat = new Vector3(center.x, 0f, center.z);
            float radiusSqr = radius * radius;
            int count = 0;

            // 건물 딕셔너리를 순회하는 동안에는 상태 딕셔너리(_attackDisabled)만 바꾼다
            //   → 순회 중인 건물 컬렉션 자체는 바뀌지 않으므로 바로 순회해도 안전하다.
            foreach (BuildingData building in _buildingPlacement.Buildings.Values)
            {
                if (!IsEnemyLivingTower(building, casterTeam)) continue;

                // 탑이 서 있는 타일의 "중심" ↔ 조준점 거리(타입 A와 같은 기준 — R-13).
                Vector3 tileCenter = _mapper.HexToWorld(building.Position);
                float dx = tileCenter.x - centerFlat.x;
                float dz = tileCenter.z - centerFlat.z;
                if (dx * dx + dz * dz > radiusSqr) continue;

                ApplyAttackDisabled(building.Id, duration, casterTeam);
                disabledTowerIds?.Add(building.Id);
                count++;
            }
            return count;
        }

        /// <summary>
        /// (서버 권위) 건물 1개에 "공격 불가"를 건다. 이미 걸려 있으면 남은 시간을 새 지속시간으로 교체한다(중첩 없음).
        /// 범위 판정 없이 Id로 직접 거는 경로다 — 지금은 <see cref="DisableEnemyTowersInRadius"/>만 이것을 부른다.
        /// </summary>
        /// <param name="buildingId">대상 건물 Id.</param>
        /// <param name="duration">무력화 시간(초). 0 이하 · NaN이면 무시한다.</param>
        /// <param name="sourceTeam">상태를 건 팀(기록용).</param>
        /// <returns>걸었으면 true, 닫혔거나 값이 잘못돼 무시했으면 false.</returns>
        public bool ApplyAttackDisabled(int buildingId, float duration, TeamId sourceTeam)
        {
            if (_closed || !(duration > 0f)) return false;

            // 같은 건물이면 기존 기록을 새 기록으로 "통째로 교체"한다 → 남은 시간이 새 지속시간으로 다시 시작된다.
            //   강도(Magnitude)는 AttackDisabled에서 쓰지 않으므로 0으로 둔다(StatusEffectKind 주석 참조).
            _attackDisabled[buildingId] = new StatusEffect(StatusEffectKind.AttackDisabled, 0f, duration, sourceTeam);
            return true;
        }

        // ====================================================================
        // 묻기 — 방어탑 공격 관문
        // ====================================================================

        /// <summary>
        /// 지정 건물이 지금 공격할 수 있는지 답한다. 걸린 상태가 없으면 true(정상), 무력화 중이면 false.
        /// TowerCombatUseCase.Tick이 "쿨다운 감소 뒤 · 적 탐색 앞"에서 매 탑마다 부른다(Plan 3-3 K1).
        /// </summary>
        /// <param name="buildingId">확인할 건물 Id.</param>
        public bool CanAttack(int buildingId)
        {
            // 걸린 탑이 하나도 없으면(평상시 거의 항상) 조회 없이 바로 통과 — 탑 Tick 비용이 사실상 늘지 않는다(R-3).
            if (_attackDisabled.Count == 0) return true;
            return !_attackDisabled.ContainsKey(buildingId);
        }

        /// <summary>
        /// 지정 건물의 남은 무력화 시간(초). 걸려 있지 않으면 0. (확인 · 다음 커밋의 알림 · 검증용)
        /// </summary>
        /// <param name="buildingId">확인할 건물 Id.</param>
        public float GetAttackDisabledRemaining(int buildingId)
        {
            return _attackDisabled.TryGetValue(buildingId, out StatusEffect effect) ? effect.RemainingDuration : 0f;
        }

        /// <summary> 지금 무력화 중인 건물 수(확인 · 검증용). </summary>
        public int AttackDisabledCount => _attackDisabled.Count;

        /// <summary> 경기 종료로 닫혔는지(닫힌 뒤에는 새로 걸지 않는다). </summary>
        public bool IsClosed => _closed;

        // ====================================================================
        // 줄이기 — 권위 전용 틱
        // ====================================================================

        /// <summary>
        /// (권위 전용 틱) 모든 무력화의 남은 시간을 dt만큼 줄이고, 0 이하가 된 건물은 목록에서 뺀다(= 다시 공격 가능).
        /// 반드시 서버 권위 쪽에서만, 그리고 "방어탑 틱 바로 앞"에서 부른다(Plan 3-2 · 위험 R-12):
        ///   싱글 = GameBootstrapper.Update의 탑 틱 바로 앞, 멀티 서버 = NetworkCombatController.TickCombat의 tower?.Tick 바로 앞.
        /// 멀티 순수 클라이언트에서는 부르지 않는다(클라이언트에는 이 상태가 없다 — 탑 공격은 서버만 판정).
        /// </summary>
        /// <param name="dt">경과 시간(초). 0 이하 · NaN이면 아무것도 하지 않는다.</param>
        public void Tick(float dt)
        {
            if (!(dt > 0f) || _attackDisabled.Count == 0) return;

            // ① 키 복사 — 딕셔너리를 순회하면서 값을 바꾸거나 지우지 않기 위해서.
            _tickKeyBuffer.Clear();
            foreach (int key in _attackDisabled.Keys) _tickKeyBuffer.Add(key);

            // ② 남은 시간 감소 + 만료 수집. StatusEffect는 클래스(참조)라 RemainingDuration만 고치면 딕셔너리 값도 바뀐다.
            _expiredKeyBuffer.Clear();
            for (int i = 0; i < _tickKeyBuffer.Count; i++)
            {
                int id = _tickKeyBuffer[i];
                StatusEffect effect = _attackDisabled[id];
                effect.RemainingDuration -= dt;
                if (effect.RemainingDuration <= 0f) _expiredKeyBuffer.Add(id);
            }

            // ③ 만료된 건물 제거 → 바로 다음 탑 틱에서 CanAttack이 true가 되어 다시 쏜다.
            for (int i = 0; i < _expiredKeyBuffer.Count; i++)
                _attackDisabled.Remove(_expiredKeyBuffer[i]);

            _expiredKeyBuffer.Clear();
            _tickKeyBuffer.Clear();
        }

        // ====================================================================
        // 빼기 · 닫기 — 정리
        // ====================================================================

        /// <summary>
        /// 지정 건물의 상태를 지운다. 조합 루트가 GameEvents.OnBuildingDied(파괴 · 철거)에서 부른다(누수 방지).
        /// 걸려 있지 않은 건물이면 아무 일도 없다(여러 번 불러도 안전).
        /// </summary>
        /// <param name="buildingId">파괴 · 철거된 건물 Id.</param>
        public void Remove(int buildingId)
        {
            _attackDisabled.Remove(buildingId);
        }

        /// <summary>
        /// 경기 종료 시 모든 상태를 비우고, 이후 새로 거는 것을 막는다(멱등 — 여러 번 불러도 안전).
        /// 조합 루트(GameBootstrapper)가 GameEvents.OnGameEnd에서 부른다(싱글 · 멀티 서버 공통 — 사용자 결정 Q5).
        /// 다음 경기는 맵을 다시 불러오며 이 서비스가 새로 만들어지므로 다시 열 필요가 없다.
        /// </summary>
        public void Close()
        {
            _closed = true;
            _attackDisabled.Clear();
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 무력화 대상이 될 수 있는 건물인지: 살아 있는 · 시전 팀이 아닌(적) · 방어탑(AutoTower).
        /// 아군 판정은 타입 A 건물 판정과 같다("시전 팀과 같으면 제외").
        /// </summary>
        private static bool IsEnemyLivingTower(BuildingData building, TeamId casterTeam)
        {
            if (building == null || !building.IsAlive) return false;
            if (building.Type != BuildingType.AutoTower) return false;
            return building.Team != casterTeam;
        }
    }
}
