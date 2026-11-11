// ============================================================================
// SkillZoneSystem.cs
// 지속 상태 구역(서리 감옥 · 수액 늪)을 서버 권위로 만들고 · 구성원을 다시 세고 · 끝내는 서비스.
// 2026-11-11 지속 상태 구역 Task(_Tasks/2026-11-10/16_02_support-skill-persistent-zones) 구현 단위 ①에서 새로 만들었다.
//
// 무엇을 하나(유니티 초급자용 설명):
//   서리 감옥을 지도 한 지점에 쓰면 그 자리에 "얼음 구역"이 4초 동안 남는다. 그동안 구역 안에 있는 적 유닛은 얼어서
//   움직이지도 공격하지도 못한다. 나중에 걸어 들어온 적도 얼고, 구역에서 나가거나 구역이 끝나면 풀린다.
//   수액 늪도 같고, 얼리는 대신 이동 속도를 0.4배로 줄인다.
//   이 클래스가 "지금 어떤 구역이 있고, 각 구역 안에 누가 있는가"를 기억한다.
//     · 만들기   : CreateZone — 실행기(PersistentStatusZoneExecutor)가 발동 순간 부른다. 그 순간 구역 안 유닛이 바로 걸린다.
//     · 다시 세기: Tick — 권위 틱마다 남은 시간을 줄이고, 구역마다 안에 있는 유닛을 다시 센다(들어옴 · 나감).
//     · 묻기     : GetMoveSpeedMultiplier · CanAttack — UnitCombatUseCase의 접근자 두 곳이 "이 유닛의 구역 효과"를 묻는다.
//     · 사망     : NotifyUnitDied — 유닛이 죽으면 구성원에서 바로 뺀다.
//     · 닫기     : Close — 경기가 끝나면 모든 구역을 풀고, 이후 새 구역을 만들지 않는다(사용자 결정 Z-8).
//
// 왜 유닛 상태 시스템(StatusEffectSystem)에 같이 넣지 않는가(사용자 결정 Z-1 — Z3 별도 구역 서비스):
//   상태 시스템은 같은 종류 상태를 "통째로 교체"한다(UnitStatusState.ApplyOrRefresh). 수액 늪(이동 배율)을 거기에 넣으면
//   같은 종류인 숲의 순풍(이동 1.3배) 기록을 덮어써, 늪을 나간 뒤 순풍이 사라진다(Research 5-5 Z1의 버그).
//   그래서 구역 효과는 이 서비스가 따로 들고, UnitCombatUseCase 접근자가 「연구 × 상태 × 구역」을 곱해 합친다(Z-6 곱 합성).
//   기존 상태 시스템 · 1차 강화 4종 동작은 그대로다.
//
// 지키는 결정(사용자 「추천대로 진행하자」, Plan 1절):
//   Z-3 「구역 안」 = 유닛이 선 타일의 중심 ↔ 조준점 거리 ≤ 반경(EMP · 포격과 같은 기준).
//   Z-4 판정은 서버 권위 틱마다(멀티 = 서버 전투 틱, 상태 틱 앞 · 싱글 = Update 매 프레임, 상태 틱 앞).
//   Z-5 같은 스킬(같은 팀 · 같은 슬롯) 구역이 겹쳐도 효과는 1번만(수액 0.4배가 0.16배가 되지 않는다).
//   Z-6 다른 출처의 같은 종류는 곱한다(순풍 1.3 × 수액 0.4 = 0.52). 빙결은 이동 0 · 공격 불가로 최우선.
//   Z-7 시전 건물이 파괴 · 철거돼도 구역은 시간이 다 될 때까지 남는다(건물을 보지 않는다).
//   Z-8 경기 종료 = 모든 구역 해제 + 새 구역 거부.
//
// 클라이언트 알림(Z-2 N-B):
//   구성원이 바뀐 틱에만 GameEvents.OnSkillZoneMembershipConfirmed(구역 순번 · 들어온 Id · 나간 Id)를 발행한다.
//   멀티 서버에서는 NetworkSkillController가 그것을 ClientRpc로 순수 클라이언트에 전한다. 구역 생성 · 끝 알림은 없다
//   (생성 = 스킬 발동 표현, 끝 = 화면 로컬 타이머). 첫 구성원(발동 순간 안에 있던 유닛)은 "다음 틱"에 알린다 —
//   스킬 발동 알림(SkillActivatedClientRpc)이 구성원 알림보다 먼저 클라이언트에 가도록 하기 위해서다(위험 R-10).
//   (효과 자체는 발동 순간 바로 걸린다 — 알림만 한 틱 늦다.)
//
// 회귀 안전(핵심): 구역이 하나도 없으면 GetMoveSpeedMultiplier는 1, CanAttack은 true를 조회 없이 바로 돌려준다.
//   → 두 스킬을 쓰기 전에는 전투 · 이동이 이 클래스가 없던 때와 완전히 같다.
//
// Application 레이어 — Domain · Application 의존. UnityEngine은 Vector3 계산에만 쓴다(BuildingStatusEffectSystem 선례).
//   Netcode · Core · Infrastructure 참조 없음. 클라이언트에는 구역이 생기지 않는다(발동은 서버만 — 스킬 규칙 25).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 지속 상태 구역(서리 감옥 · 수액 늪)을 서버 권위로 생성 · 구성원 판정 · 만료 · 정리하고,
    /// 유닛별 구역 효과(이동 배율 · 공격 가능)를 접근자에 제공하는 서비스.
    /// </summary>
    public sealed partial class SkillZoneSystem
    {
        // ── 의존성(조합 루트에서 주입) ──────────────────────────────────────
        // 유닛 목록(팀 · 생존 · 타일 좌표) 조회용. 맵을 불러올 때마다 이 서비스와 함께 새로 만들어진다.
        private readonly UnitSpawnUseCase _units;
        // 타일 좌표 → 도메인 월드 좌표(타일 중심 계산). Application이 Core(HexMetrics)를 모르게 하는 인터페이스.
        private readonly IHexCoordinateMapper _mapper;
        // [진단 전용] 시전 건물이 아직 살아 있는지 로그에 적기 위한 건물 조회(판정에는 쓰지 않는다 — Z-7). null 허용.
        private readonly BuildingPlacementUseCase _buildings;

        // ── 구역 1개 ───────────────────────────────────────────────────────
        private sealed class Zone
        {
            public int Sequence;              // 구역 순번 = 발동 순번(화면이 발동 알림과 짝짓는 열쇠)
            public TeamId CasterTeam;         // 시전 팀
            public TeamId TargetTeam;         // 효과를 받는 팀(적 대상 스킬이면 상대 팀)
            public int Slot;                  // 시전 슬롯(같은 스킬 판정 열쇠 — Z-5)
            public int CasterBuildingId;      // 시전 건물 Id(기록 · 진단용 — 파괴돼도 구역은 남는다, Z-7)
            public Vector3 Center;            // 중심(도메인 월드, Y는 무시)
            public float Radius;              // 반경(월드)
            public float RadiusSqr;           // 반경 제곱(거리 비교용)
            public float Duration;            // 전체 지속(초)
            public float Remaining;           // 남은 시간(초)
            public StatusEffectKind Kind;     // 효과 종류(빙결 · 이동 배율)
            public float Magnitude;           // 강도(이동 배율 값 — 빙결이면 쓰지 않는다)
            public bool Announced;            // 첫 구성원을 클라이언트에 알렸는가(첫 틱에 알린다 — 위험 R-10)
            public readonly HashSet<int> Members = new HashSet<int>();     // 지금 구역 안(효과를 받는) 유닛 Id
            public readonly List<int> PendingDiedExits = new List<int>(2); // 사망 통보로 뺐지만 아직 알리지 않은 Id
            public ZoneDiag Diag;             // [진단 전용] 개발 빌드에서만 채운다(판정과 무관)
        }

        // 유닛 하나의 구역 효과 합(빠른 조회용 캐시). 구성원이 바뀐 순간에만 다시 계산한다.
        private struct UnitZoneEffect
        {
            public float MoveMultiplier; // 이동 배율(빙결이면 0)
            public bool CanAttack;       // 공격 가능(빙결이면 false)
        }

        // 지금 살아 있는 구역(만든 순서). 구역 수는 많아야 몇 개라 목록 순회로 충분하다.
        private readonly List<Zone> _zones = new List<Zone>(4);
        // 유닛 Id → 구역 효과 합. 구역 안에 있는 유닛만 항목을 갖는다(없으면 = 영향 없음).
        private readonly Dictionary<int, UnitZoneEffect> _effects = new Dictionary<int, UnitZoneEffect>();

        // 재사용 버퍼(서버 · 싱글은 한 스레드 — 계속 재사용해도 안전, GC 절감).
        private readonly HashSet<int> _scratchMembers = new HashSet<int>();
        private readonly List<int> _entered = new List<int>(8);
        private readonly List<int> _exitedWalked = new List<int>(8);
        private readonly List<int> _exitedDied = new List<int>(8);
        private readonly List<int> _exitedAll = new List<int>(8);
        private readonly List<int> _removeBuffer = new List<int>(8);
        // 효과 합 다시 계산 때 "이 유닛에 이 스킬 효과를 이미 넣었는가"(같은 스킬 겹침 1회 — Z-5). 키 = 유닛 Id × 2^16 + 스킬 키.
        private readonly HashSet<long> _appliedSkillPerUnit = new HashSet<long>();

        // 경기 종료로 닫혔는가. 닫힌 뒤에는 새 구역을 만들지 않는다(Z-8).
        private bool _closed;

        /// <summary>
        /// 구역 서비스 생성. 조합 루트(GameBootstrapper.Setup)가 맵을 불러올 때마다 새로 만든다(재경기 = 새 인스턴스).
        /// </summary>
        /// <param name="units">유닛 목록 UseCase. null이면 구역이 아무 유닛도 찾지 못한다(안전).</param>
        /// <param name="mapper">타일 → 도메인 월드 좌표 변환기. null이면 구역이 아무 유닛도 찾지 못한다(안전).</param>
        /// <param name="buildings">[진단 전용] 건물 조회(시전 건물 생존 표기). null 허용.</param>
        public SkillZoneSystem(UnitSpawnUseCase units, IHexCoordinateMapper mapper, BuildingPlacementUseCase buildings = null)
        {
            _units = units;
            _mapper = mapper;
            _buildings = buildings;
        }

        /// <summary> 지금 살아 있는 구역 수(확인 · 검증용). </summary>
        public int ZoneCount => _zones.Count;

        /// <summary> 경기 종료로 닫혔는지(닫힌 뒤에는 새 구역을 만들지 않는다). </summary>
        public bool IsClosed => _closed;

        // ====================================================================
        // 만들기
        // ====================================================================

        /// <summary>
        /// (서버 권위) 조준 지점에 지속 상태 구역을 만든다. 그 순간 구역 안의 대상 유닛에게 바로 효과가 걸린다.
        /// 클라이언트 알림(첫 구성원)은 다음 틱에 보낸다(스킬 발동 알림보다 먼저 가지 않게 — 위험 R-10).
        /// </summary>
        /// <param name="sequence">발동 순번(구역 순번으로 쓴다).</param>
        /// <param name="casterTeam">시전 팀.</param>
        /// <param name="slot">시전 슬롯(같은 스킬 판정 — Z-5).</param>
        /// <param name="casterBuildingId">시전 건물 Id(기록용 — 건물이 사라져도 구역은 남는다, Z-7).</param>
        /// <param name="center">조준 중심(도메인 월드, Y 무시).</param>
        /// <param name="radius">반경(월드). 0 이하 · NaN이면 만들지 않는다.</param>
        /// <param name="duration">지속(초). 0 이하 · NaN이면 만들지 않는다.</param>
        /// <param name="kind">효과 종류(Freeze · MoveSpeedMul · AttackDisabled만 효과가 있다).</param>
        /// <param name="magnitude">강도(MoveSpeedMul의 배율). 음수 · NaN이면 만들지 않는다.</param>
        /// <param name="targetsAllies">true면 시전 팀 유닛, false면 상대 팀 유닛이 대상.</param>
        /// <returns>만들었으면 발동 순간 구역 안 유닛 수(0 이상), 만들지 않았으면 -1.</returns>
        public int CreateZone(int sequence, TeamId casterTeam, int slot, int casterBuildingId, Vector3 center,
                              float radius, float duration, StatusEffectKind kind, float magnitude, bool targetsAllies)
        {
            // 경기가 끝났거나 · 값이 잘못됐거나 · 의존성이 없으면 만들지 않는다(방어적). !(x > 0)은 NaN도 거른다.
            if (_closed || !(radius > 0f) || !(duration > 0f)) { DiagCreateRejected(sequence, casterTeam, slot, _closed ? "Closed" : "InvalidValue"); return -1; }
            if (!(magnitude >= 0f) || float.IsInfinity(magnitude)) { DiagCreateRejected(sequence, casterTeam, slot, "InvalidMagnitude"); return -1; }
            if (_units == null || _mapper == null) { DiagCreateRejected(sequence, casterTeam, slot, "Unwired"); return -1; }

            var zone = new Zone
            {
                Sequence = sequence,
                CasterTeam = casterTeam,
                TargetTeam = targetsAllies ? casterTeam : OppositeTeam(casterTeam),
                Slot = slot,
                CasterBuildingId = casterBuildingId,
                Center = new Vector3(center.x, 0f, center.z),
                Radius = radius,
                RadiusSqr = radius * radius,
                Duration = duration,
                Remaining = duration,
                Kind = kind,
                Magnitude = magnitude,
                Announced = false
            };

            // 발동 순간 구역 안 유닛을 바로 구성원으로 넣는다(상위 결정 4 — 지금 안에 있는 유닛도 당연히 걸린다).
            CollectInside(zone, zone.Members);
            _zones.Add(zone);
            RebuildEffects();

            // [진단 L1 · L2] 구역 생성 1줄(초기 구성원 수 · 반경 · 지속 · 종류).
            DiagOnCreated(zone);
            return zone.Members.Count;
        }

        // ====================================================================
        // 다시 세기 — 권위 전용 틱
        // ====================================================================

        /// <summary>
        /// (권위 전용 틱) 구역마다 남은 시간을 줄이고(끝난 구역은 구성원을 모두 풀고 지운다),
        /// 남은 구역은 안에 있는 유닛을 다시 세어 들어옴 · 나감을 반영한다. 구성원이 바뀐 구역은 화면 알림 이벤트를 1건 발행한다.
        /// 반드시 서버 권위 쪽에서만 부른다 — 싱글 = GameBootstrapper.Update(!IsNetworkMode), 멀티 서버 = NetworkCombatController.TickCombat.
        /// 멀티 순수 클라이언트에서는 부르지 않는다(클라이언트에는 구역이 없다).
        /// </summary>
        /// <param name="dt">경과 시간(초). 0 이하 · NaN이면 아무것도 하지 않는다.</param>
        public void Tick(float dt)
        {
            if (!(dt > 0f) || _zones.Count == 0) return;

            bool effectsChanged = false;

            // ① 만료 — 뒤에서부터 지운다(앞쪽 인덱스가 밀리지 않게).
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                Zone zone = _zones[i];
                zone.Remaining -= dt;
                if (zone.Remaining > 0f) continue;
                // [진단 L7 · L12] 만료 1줄(시전 건물이 사라졌어도 Reason=Expire — Z-7).
                DiagOnEnded(zone, "Expire");
                _zones.RemoveAt(i);
                effectsChanged = true;
            }

            // ② 남은 구역 — 구성원 다시 세기(들어옴 · 걸어 나감 · 사망).
            for (int i = 0; i < _zones.Count; i++)
            {
                if (UpdateMembership(_zones[i])) effectsChanged = true;
            }

            if (effectsChanged) RebuildEffects();
        }

        /// <summary>
        /// 구역 하나의 구성원을 다시 세고 바뀐 것을 반영 · 알린다.
        /// </summary>
        /// <returns>효과를 받는 구성원 집합이 바뀌었으면 true(효과 합을 다시 계산해야 한다).</returns>
        private bool UpdateMembership(Zone zone)
        {
            _scratchMembers.Clear();
            CollectInside(zone, _scratchMembers);

            _entered.Clear();
            _exitedWalked.Clear();
            _exitedDied.Clear();

            bool firstAnnounce = !zone.Announced;
            bool membersChanged = false;

            // 나감 — 지난 틱 구성원 중 지금 안에 없는 유닛. 살아 있으면 "걸어 나감", 죽었거나 목록에서 사라졌으면 "사망".
            foreach (int id in zone.Members)
            {
                if (_scratchMembers.Contains(id)) continue;
                membersChanged = true;
                // 첫 알림 전에 나간 유닛은 클라이언트가 들어온 줄도 모르므로 알리지 않는다(효과만 푼다).
                if (firstAnnounce) continue;
                UnitData unit = _units.GetUnit(id);
                if (unit == null || !unit.IsAlive) _exitedDied.Add(id);
                else _exitedWalked.Add(id);
            }
            // 사망 통보(NotifyUnitDied)로 이미 뺀 유닛 — 알림에만 더한다.
            if (zone.PendingDiedExits.Count > 0)
            {
                if (!firstAnnounce)
                    for (int i = 0; i < zone.PendingDiedExits.Count; i++)
                        if (!_exitedDied.Contains(zone.PendingDiedExits[i])) _exitedDied.Add(zone.PendingDiedExits[i]);
                zone.PendingDiedExits.Clear();
            }

            // 들어옴 — 첫 알림이면 지금 구성원 전부(발동 순간 + 그 뒤 들어온 유닛), 아니면 새로 들어온 유닛만.
            foreach (int id in _scratchMembers)
            {
                bool isNew = !zone.Members.Contains(id);
                if (isNew) membersChanged = true;
                if (firstAnnounce || isNew) _entered.Add(id);
            }

            if (membersChanged)
            {
                zone.Members.Clear();
                foreach (int id in _scratchMembers) zone.Members.Add(id);
            }
            zone.Announced = true;

            // 알릴 것이 있으면 진단 1줄 + 화면 이벤트 1건(빈 알림은 보내지 않는다).
            if (_entered.Count > 0 || _exitedWalked.Count > 0 || _exitedDied.Count > 0)
            {
                // [진단 L3 · L6 · L10] 구성원 변경 1줄(Reason=Initial · Walked · Died).
                DiagOnMembersChanged(zone, firstAnnounce, _entered, _exitedWalked, _exitedDied);
                _exitedAll.Clear();
                _exitedAll.AddRange(_exitedWalked);
                _exitedAll.AddRange(_exitedDied);
                PublishMembershipChanged(zone.Sequence, _entered, _exitedAll);
            }
            return membersChanged;
        }

        /// <summary>
        /// 구성원 변경 표현을 GameEvents.OnSkillZoneMembershipConfirmed로 발행한다.
        /// 구독자(멀티 서버 브로드캐스트 · 화면)에서 예외가 나도 서버 틱(효과 판정)은 계속되게 감싼다.
        /// </summary>
        private static void PublishMembershipChanged(int sequence, List<int> entered, List<int> exited)
        {
            var presentation = new SkillZoneMembershipPresentation(sequence, entered, exited);
            try
            {
                GameEvents.OnSkillZoneMembershipConfirmed.OnNext(presentation);
            }
            catch (Exception ex)
            {
                // [개발] Warn + 개발. 화면 표현 구독자의 실패라 구역 효과(게임 결과)에는 영향이 없다.
                GameLog.Dev.Warn("Skill", nameof(SkillZoneSystem),
                                 "구역 구성원 변경 이벤트 구독자 예외 — 구역 효과는 유지",
                                 $"Sequence={sequence}, Entered={presentation.EnteredCount}, Exited={presentation.ExitedCount}, Error={ex.Message}");
            }
        }

        /// <summary>
        /// 구역 안에 있는 대상 유닛 Id를 모은다 — 살아 있는 · 대상 팀 · 타일 중심 ↔ 조준점 거리 ≤ 반경(Z-3).
        /// </summary>
        private void CollectInside(Zone zone, HashSet<int> result)
        {
            result.Clear();
            if (_units == null || _mapper == null) return;
            foreach (UnitData unit in _units.Units.Values)
            {
                if (unit == null || !unit.IsAlive || unit.Team != zone.TargetTeam) continue;
                // 유닛이 서 있는 타일의 "중심" ↔ 조준점(XZ 평면). 화면 몸 위치는 서버에 없다 — 타일 경계를 넘을 때 바뀐다(Plan Z-3).
                Vector3 tileCenter = _mapper.HexToWorld(unit.Position);
                float dx = tileCenter.x - zone.Center.x;
                float dz = tileCenter.z - zone.Center.z;
                if (dx * dx + dz * dz <= zone.RadiusSqr) result.Add(unit.Id);
            }
        }

        // ====================================================================
        // 묻기 — UnitCombatUseCase 접근자 두 곳(이동 배율 · 공격 관문)
        // ====================================================================

        /// <summary>
        /// 이 유닛의 구역 이동 배율. 구역 밖이면 1, 빙결 구역 안이면 0, 이동 배율 구역 안이면 그 배율(다른 스킬끼리는 곱 — Z-6).
        /// UnitCombatUseCase.GetUnitMoveSpeedMultiplier가 연구 × 상태 결과에 곱한다.
        /// </summary>
        /// <param name="unitId">유닛 Id.</param>
        public float GetMoveSpeedMultiplier(int unitId)
        {
            // 구역 효과를 받는 유닛이 하나도 없으면(평상시 거의 항상) 조회 없이 1 — 기존 이동과 완전히 같다.
            if (_effects.Count == 0) return 1f;
            return _effects.TryGetValue(unitId, out UnitZoneEffect e) ? e.MoveMultiplier : 1f;
        }

        /// <summary>
        /// 이 유닛이 구역 때문에 공격을 못 하는가의 반대(구역 밖 · 이동 배율 구역 = true, 빙결 구역 안 = false).
        /// UnitCombatUseCase.CanAttack이 상태 시스템 결과와 함께(둘 다 true여야 공격) 본다.
        /// </summary>
        /// <param name="unitId">유닛 Id.</param>
        public bool CanAttack(int unitId)
        {
            if (_effects.Count == 0) return true;
            return !_effects.TryGetValue(unitId, out UnitZoneEffect e) || e.CanAttack;
        }

        /// <summary> 이 유닛이 지금 어느 구역의 구성원인가(확인 · 검증용). </summary>
        /// <param name="unitId">유닛 Id.</param>
        public bool IsInAnyZone(int unitId) => _effects.ContainsKey(unitId);

        /// <summary>
        /// 유닛별 구역 효과 합을 처음부터 다시 계산한다(구성원이 바뀐 순간에만 — 매 틱이 아니다).
        /// 같은 스킬(팀 · 슬롯) 효과는 유닛당 1번만(Z-5), 다른 스킬의 이동 배율은 곱하고, 빙결은 이동 0 · 공격 불가(Z-6).
        /// </summary>
        private void RebuildEffects()
        {
            _effects.Clear();
            _appliedSkillPerUnit.Clear();
            for (int z = 0; z < _zones.Count; z++)
            {
                Zone zone = _zones[z];
                int skillKey = SkillKey(zone.CasterTeam, zone.Slot);
                foreach (int unitId in zone.Members)
                {
                    long pair = ((long)unitId << 16) | (uint)skillKey;
                    if (!_appliedSkillPerUnit.Add(pair))
                    {
                        // 같은 스킬 구역이 이 유닛 위에서 겹쳤다 — 효과를 다시 곱하지 않는다(Z-5). [진단 L8] 처음 겹친 순간 1줄.
                        DiagOnOverlap(zone, unitId);
                        continue;
                    }
                    UnitZoneEffect effect = _effects.TryGetValue(unitId, out UnitZoneEffect current)
                        ? current
                        : new UnitZoneEffect { MoveMultiplier = 1f, CanAttack = true };
                    ApplyKind(ref effect, zone.Kind, zone.Magnitude);
                    _effects[unitId] = effect;
                }
            }
        }

        // 효과 종류 하나를 합에 넣는다. 빙결은 UnitStatusState와 같은 뜻(이동 0 + 공격 불가)을 재사용한다(Z-17).
        private static void ApplyKind(ref UnitZoneEffect effect, StatusEffectKind kind, float magnitude)
        {
            switch (kind)
            {
                case StatusEffectKind.Freeze:
                    effect.MoveMultiplier = 0f;
                    effect.CanAttack = false;
                    break;
                case StatusEffectKind.MoveSpeedMul:
                    effect.MoveMultiplier *= magnitude;
                    break;
                case StatusEffectKind.AttackDisabled:
                    effect.CanAttack = false;
                    break;
                // 그 밖의 종류(공격 배율 · 회복 · 없음)는 구역 효과로 다루지 않는다(접근자 두 곳만 합성 — Z-1).
            }
        }

        // ====================================================================
        // 사망 · 닫기
        // ====================================================================

        /// <summary>
        /// 유닛이 죽으면 모든 구역 구성원에서 바로 뺀다(효과 즉시 정리). 클라이언트 알림은 다음 틱에 「나감」으로 보낸다.
        /// 조합 루트가 GameEvents.OnUnitDied에서 부른다. 구역에 없던 유닛이면 아무 일도 없다(여러 번 불러도 안전).
        /// </summary>
        /// <param name="unitId">죽은 유닛 Id.</param>
        public void NotifyUnitDied(int unitId)
        {
            if (_zones.Count == 0) return;
            bool removed = false;
            for (int i = 0; i < _zones.Count; i++)
            {
                Zone zone = _zones[i];
                if (!zone.Members.Remove(unitId)) continue;
                removed = true;
                // 이미 클라이언트에 알린 구역이면 다음 틱에 「나감」으로 알린다(첫 알림 전이면 알릴 필요가 없다).
                if (zone.Announced) zone.PendingDiedExits.Add(unitId);
            }
            if (removed) RebuildEffects();
        }

        /// <summary>
        /// 경기 종료 시 모든 구역을 풀고 비우며, 이후 새 구역을 만들지 않는다(Z-8 — 멱등).
        /// 조합 루트(GameBootstrapper)가 GameEvents.OnGameEnd에서 부른다(싱글 · 멀티 서버 공통).
        /// 클라이언트 알림은 보내지 않는다 — 화면도 OnGameEnd로 구역 · 얼음 VFX를 꼬리 정지한다.
        /// </summary>
        public void Close()
        {
            if (!_closed)
            {
                // [진단 L11] 구역마다 끝 줄(Reason=GameEnd).
                for (int i = 0; i < _zones.Count; i++) DiagOnEnded(_zones[i], "GameEnd");
            }
            _closed = true;
            _zones.Clear();
            _effects.Clear();
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        // 같은 스킬 판정 열쇠 = 시전 팀 × 8 + 슬롯(슬롯은 0~4라 겹치지 않는다).
        private static int SkillKey(TeamId team, int slot) => (int)team * 8 + (slot & 7);

        /// <summary> 상대 팀(Blue↔Red). Neutral은 그대로. </summary>
        private static TeamId OppositeTeam(TeamId team)
        {
            if (team == TeamId.Blue) return TeamId.Red;
            if (team == TeamId.Red) return TeamId.Blue;
            return team;
        }
    }
}
