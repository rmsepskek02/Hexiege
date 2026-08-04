// ============================================================================
// SkillActivationUseCase.cs
// 스킬 발동 오케스트레이션의 단일 진입점 + 건물 글로벌 쿨다운 상태 보관소.
//
// 무엇을 하나(초급자용 설명):
//   "이 건물의 이 슬롯 스킬을, (필요하면) 이 좌표에 발동해줘"라는 요청 하나를 받아
//   ① 재검증(건물 생존·스킬 건물 여부·슬롯 유효·글로벌 쿨다운·좌표 유효) →
//   ② 실행기(A/B/C) 호출 → ③ 글로벌 쿨다운 설정을 수행한다.
//   피해/DoT의 실제 적용은 UnitCombatUseCase가 담당하고, 여기서는 흐름만 조율한다.
//
// 글로벌 쿨다운(규칙 3):
//   쿨다운은 스킬 단위가 아니라 "건물 단위"로 잠긴다. 한 건물의 어떤 스킬을 써도 그 건물의
//   모든 스킬 슬롯이 쿨다운 동안 함께 잠긴다. 상태는 이 UseCase가 Dictionary<건물Id,남은시간>으로
//   보관하고(도메인 BuildingData를 늘리지 않음 — 응집도 유지), 서버 틱에서 감소시킨다.
//
// 플레이어/AI 공유 진입점(§3-8):
//   Activate는 "누가 요청했는지"(플레이어 터치/AI)와 무관하게 동일 경로다.
//   - 플레이어(싱글): 조준 컨트롤러가 좌표를 만든 뒤 이 Activate를 직접 호출.
//   - 플레이어(멀티): NetworkSkillController가 서버에서 재검증 후 이 Activate를 호출.
//   - AI: 서버 권위 컨텍스트에서 좌표를 직접 계산해 이 Activate를 호출(RPC 왕복 불필요).
//
// 서버 권위(규칙 25·26):
//   Activate는 서버(싱글=로컬 서버/멀티=서버)에서만 호출된다. 클라이언트는 발동을 직접 하지 않고,
//   쿨다운 표시용 로컬 미러(StartCooldownLocal)만 브로드캐스트로 받는다.
//
// Application 레이어 — Domain/Application 의존. Unity.Netcode 직접 참조 없음.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 스킬 발동 재검증·실행·쿨다운을 담당하는 서버 권위 유스케이스.
    /// </summary>
    public sealed class SkillActivationUseCase
    {
        // ── 의존성(조합 루트에서 주입) ──────────────────────────────────────
        private readonly BuildingPlacementUseCase _buildingPlacement;
        // 조준 연속 좌표(도메인 월드)가 맵 경계 안인지 판정하는 함수(규칙 22·26 서버 재검증).
        //   좌표화 이전에는 HexGrid.HasTile(정수 타일)로 판정했으나, 연속 좌표에는 "맵 경계 안 점" 판정이 필요하다.
        //   경계 계산(HexMetrics.IsWithinMapBounds)은 Core에 있고, 조합 루트가 클로저로 주입한다(Core→Application 역전).
        private readonly Func<Vector3, bool> _isWithinMapBounds;
        private readonly UnitCombatUseCase _combat;
        private readonly ISkillDataProvider _dataProvider;
        // 팀 → 종족 변환(로드아웃 조회용). 조합 루트가 GameRaceContext 매핑을 주입(TowerCombatUseCase 선례).
        private readonly Func<TeamId, RaceId> _teamToRace;

        // 스킬 실행기 조회 테이블(A·B 등록, C는 Phase 2). 외부 의존이 없어 내부 생성.
        private readonly SkillExecutorRegistry _registry = new SkillExecutorRegistry();

        // ── 건물 글로벌 쿨다운 상태(규칙 3) ────────────────────────────────
        //   Key=건물 Id, Value=남은 시간(초). 서버 틱(TickCooldowns)이 감소시킨다.
        private readonly Dictionary<int, float> _cooldownRemaining = new Dictionary<int, float>();
        //   쿨다운 오버레이의 radial fill 비율 계산을 위해 발동 시점의 총 쿨다운도 보관한다.
        private readonly Dictionary<int, float> _cooldownTotal = new Dictionary<int, float>();

        /// <summary>
        /// 스킬 발동 유스케이스 생성.
        /// </summary>
        /// <param name="buildingPlacement">건물 조회(생존/타입/좌표) UseCase.</param>
        /// <param name="isWithinMapBounds">조준 연속 좌표(도메인 월드)가 맵 경계 안인지 판정(규칙 22·26). null 허용(항상 통과).</param>
        /// <param name="combat">피해/DoT 실제 적용을 담당하는 전투 UseCase.</param>
        /// <param name="dataProvider">종족별 스킬 로드아웃 제공자(Infrastructure 구현).</param>
        /// <param name="teamToRace">팀 → 종족 변환 함수(로드아웃 분기, 규칙 1·6).</param>
        public SkillActivationUseCase(
            BuildingPlacementUseCase buildingPlacement,
            Func<Vector3, bool> isWithinMapBounds,
            UnitCombatUseCase combat,
            ISkillDataProvider dataProvider,
            Func<TeamId, RaceId> teamToRace)
        {
            _buildingPlacement = buildingPlacement;
            _isWithinMapBounds = isWithinMapBounds;
            _combat = combat;
            _dataProvider = dataProvider;
            _teamToRace = teamToRace;
        }

        // ====================================================================
        // 발동(단일 진입점 — 플레이어/AI 공유)
        // ====================================================================

        /// <summary>
        /// 스킬을 발동한다(서버 권위). 재검증을 모두 통과하면 실행기를 호출하고 글로벌 쿨다운을 설정한다.
        /// </summary>
        /// <param name="buildingId">발동한 스킬 건물의 Id.</param>
        /// <param name="skillSlot">발동한 슬롯 번호(0-based, 0~4).</param>
        /// <param name="aimWorld">지점 지정 스킬의 조준 좌표(도메인 월드 Vector3, 연속). 즉시 발동이면 null.</param>
        /// <returns>발동에 성공하면 true(호출자가 쿨다운 브로드캐스트에 사용), 재검증 실패면 false.</returns>
        public bool Activate(int buildingId, int skillSlot, Vector3? aimWorld)
        {
            // ① 건물 생존 재검증(규칙 26).
            BuildingData building = _buildingPlacement?.GetBuilding(buildingId);
            if (building == null || !building.IsAlive) return false;

            // ② 스킬 건물 여부 재검증(비스킬 건물에서의 오발동 차단).
            if (!IsSkillBuilding(building.Type)) return false;

            // ③ 슬롯 유효성 + 스킬 데이터 조회(종족 키 분기 — 규칙 1·6).
            if (_dataProvider == null || _teamToRace == null) return false;
            RaceId race = _teamToRace(building.Team);
            IReadOnlyList<SkillData> loadout = _dataProvider.GetLoadout(race);
            if (loadout == null || skillSlot < 0 || skillSlot >= loadout.Count) return false;
            SkillData skill = loadout[skillSlot];
            if (skill == null) return false;

            // ④ 글로벌 쿨다운 만료 재검증(규칙 3·26).
            if (GetCooldownRemaining(buildingId) > 0f) return false;

            // ⑤ 지점 지정 스킬이면 조준 좌표(연속)가 맵 경계 안 점인지 재확인(규칙 22·26).
            //   좌표화 이전의 "유효 타일(HasTile)" 판정 → "맵 경계 안 점(point-in-bounds)" 판정으로 교체.
            //   (재검증 자체는 유지 — 클라 입력 불신뢰 원칙 존속. 판정 기준만 연속 좌표에 맞게 바뀐다.)
            bool hasAim = false;
            Vector3 aim = default;
            if (skill.AimType == SkillAimType.PointTarget)
            {
                if (!aimWorld.HasValue) return false;             // 지점 지정인데 좌표 없음 → 실패
                aim = aimWorld.Value;
                if (_isWithinMapBounds != null && !_isWithinMapBounds(aim)) return false; // 맵 밖 점 → 실패(서버 재검증)
                hasAim = true;
            }

            // ⑥ 실행기 조회(미등록 타입 = 발동 무효 — Phase 1엔 타입 C 미구현).
            ISkillExecutor executor = _registry.TryGet(skill.Mechanic);
            if (executor == null) return false;

            // ⑦ 실행 — 피해/DoT의 실제 적용은 UnitCombatUseCase가 담당(델리게이트 주입).
            var ctx = new SkillActivationContext(
                skill,
                building.Team,
                buildingId,
                aim,
                hasAim,
                ApplyInstantAreaDamageBridge,
                ApplyAreaDotBridge);
            executor.Execute(ctx);

            // ⑧ 글로벌 쿨다운 설정(규칙 3).
            StartCooldownLocal(buildingId, skill.Cooldown);
            return true;
        }

        // 실행기 → UnitCombatUseCase 다리(델리게이트). 건물/스킬 출처 전용 피해 경로를 호출한다.
        //   center는 도메인 월드 좌표(연속 Vector3)다 — UnitCombatUseCase가 뷰 변환 없이 그대로 판정에 쓴다.
        private void ApplyInstantAreaDamageBridge(TeamId team, Vector3 center, float radius, int rawDamage, int sourceBuildingId)
        {
            _combat?.ApplySkillInstantAreaDamage(team, center, radius, rawDamage, sourceBuildingId);
        }

        private void ApplyAreaDotBridge(TeamId team, Vector3 center, float radius, float dps, float duration)
        {
            _combat?.ApplySkillAreaDot(team, center, radius, dps, duration);
        }

        // ====================================================================
        // 글로벌 쿨다운 상태 API
        // ====================================================================

        /// <summary>
        /// 지정 건물의 글로벌 쿨다운을 시작(또는 갱신)한다.
        /// 서버 Activate가 발동 성공 시 호출하며, 멀티 클라이언트는 브로드캐스트를 받아 로컬 미러로 호출한다.
        /// </summary>
        /// <param name="buildingId">쿨다운을 걸 건물 Id.</param>
        /// <param name="cooldown">쿨다운 시간(초). 0 이하면 즉시 사용 가능 상태로 정리.</param>
        public void StartCooldownLocal(int buildingId, float cooldown)
        {
            if (cooldown <= 0f)
            {
                _cooldownRemaining.Remove(buildingId);
                _cooldownTotal.Remove(buildingId);
                return;
            }
            _cooldownRemaining[buildingId] = cooldown;
            _cooldownTotal[buildingId] = cooldown;
        }

        /// <summary>
        /// 매 서버 틱(싱글=GameBootstrapper.Update / 멀티 서버=NetworkCombatController.TickCombat,
        /// 멀티 클라=로컬 미러 감소)에서 호출되어 모든 건물 쿨다운을 감소시킨다.
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void TickCooldowns(float dt)
        {
            if (dt <= 0f || _cooldownRemaining.Count == 0) return;

            // 순회 중 제거를 위해 만료된 키를 모아 뒤에서 제거한다(컬렉션 변경 예외 회피).
            _expiredBuffer.Clear();
            // Dictionary 키 순회를 위해 키 복사 없이 값만 갱신하고, 만료 키만 별도 수집한다.
            foreach (var kv in _cooldownRemaining)
            {
                _tickKeyBuffer.Add(kv.Key);
            }
            for (int i = 0; i < _tickKeyBuffer.Count; i++)
            {
                int id = _tickKeyBuffer[i];
                float remaining = _cooldownRemaining[id] - dt;
                if (remaining <= 0f)
                    _expiredBuffer.Add(id);
                else
                    _cooldownRemaining[id] = remaining;
            }
            _tickKeyBuffer.Clear();

            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                _cooldownRemaining.Remove(_expiredBuffer[i]);
                _cooldownTotal.Remove(_expiredBuffer[i]);
            }
            _expiredBuffer.Clear();
        }

        // TickCooldowns의 GC 절감용 재사용 버퍼(서버/클라 단일 스레드 갱신이라 재사용 안전).
        private readonly List<int> _tickKeyBuffer = new List<int>(8);
        private readonly List<int> _expiredBuffer = new List<int>(8);

        /// <summary> 지정 건물의 남은 쿨다운(초). 쿨다운이 없으면 0. </summary>
        public float GetCooldownRemaining(int buildingId)
        {
            return _cooldownRemaining.TryGetValue(buildingId, out float r) ? r : 0f;
        }

        /// <summary> 지정 건물의 발동 시점 총 쿨다운(초). 오버레이 radial fill 비율 계산용. 없으면 0. </summary>
        public float GetCooldownTotal(int buildingId)
        {
            return _cooldownTotal.TryGetValue(buildingId, out float t) ? t : 0f;
        }

        /// <summary> 지정 건물이 현재 쿨다운 중인지 여부(규칙 3). </summary>
        public bool IsOnCooldown(int buildingId)
        {
            return GetCooldownRemaining(buildingId) > 0f;
        }

        /// <summary> 게임 재시작/맵 전환 시 쿨다운 상태를 전부 초기화한다. </summary>
        public void ClearAll()
        {
            _cooldownRemaining.Clear();
            _cooldownTotal.Clear();
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 스킬 건물(FlightFacility / MagicBuilding)인지 여부.
        /// 종족별 스킬 건물은 정확히 이 두 enum 값 중 하나다(규칙 1 — MagicBuilding은 Spirit/Trans 공유).
        /// </summary>
        /// <param name="type">건물 타입.</param>
        private static bool IsSkillBuilding(BuildingType type)
        {
            return type == BuildingType.FlightFacility || type == BuildingType.MagicBuilding;
        }
    }
}
