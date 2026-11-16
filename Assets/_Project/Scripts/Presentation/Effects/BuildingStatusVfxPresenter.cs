// ============================================================================
// BuildingStatusVfxPresenter.cs
// 지원 스킬 「건물 상태 VFX」 화면 구독자 — 효과를 받은 일반 건물 위에 효과 시간 동안 반복 이펙트를 붙이고 뗀다.
// 2026-11-16 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 C — 구현 단위 C-3, Plan 6-1-3.
//
// 무엇을 하나(초급자용 설명):
//   신목의 각성을 쓰면 서버가 그 순간 살아 있는 아군 건물마다 쉴드 500을 건다(서버 판정 — 이 파일과 무관).
//   이 컴포넌트는 그 발동 알림을 받아, 이 화면의 시전 팀 건물마다 스킬 자산 「건물 상태 VFX」 칸의 이펙트를 건물 위에 띄우고,
//   효과 시간이 끝나거나 · 쉴드가 깨지거나 · 건물이 사라지면 이펙트를 뗀다.
//   칸이 비어 있으면 아무것도 붙이지 않고 진단 「BuildingVfxSkipped Reason=NoPrefab」 1줄을 남긴다(효과는 그대로 걸린다).
//
// 왜 EMP 탑 장치(TowerDisableVfxPresenter)를 넓히지 않고 새로 만들었나(Plan 6-1-3 · Research 8절):
//   탑 장치는 「방어탑만 · 종족 프리셋 · EMP 무력화 알림」에 묶여 있다. 넓히면 EMP 판정 · 프리셋과 얽혀 이미 검증된 EMP 동작에
//   회귀 위험이 생긴다. 이 장치는 「스킬 데이터의 프리팹 · 배율 · 높이 보정」과 「스킬 발동 알림」만 쓰는 일반 건물용이다.
//   단계 B(재생의 뿌리 건물 재생)도 같은 칸 · 같은 장치를 쓴다(스킬 단위로 만들었다).
//
// 언제 붙이나(어떤 스킬에):
//   타입 C(전역 상태변경) 스킬 가운데 「추가 효과 목록」에 대상 = 건물 각각(SkillEffectTarget.Building) 줄이 있는 스킬만(SkillData.HasBuildingEffect).
//   그 밖의 스킬(전술 지령 등)은 조용히 지나간다 — 전술 지령 생산 건물 이펙트(S-10)는 「어느 건물에 붙일지」가 아직 정해지지 않아
//   칸만 있고 붙이지 않는다(상위 Plan 4절 20 — 기존 스킬 로그도 바뀌지 않는다).
//
// 어떤 알림을 받나(유닛 상태 VFX와 같은 방식 — RPC · 스키마 변경 없이 발동 알림 재사용):
//   - 싱글 · Host 화면: 「발동 확정」(OnSkillActivationConfirmed)만 · 멀티 순수 Client 화면: 「발동 수신」(OnSkillActivationReceived)만.
//   - 대상 건물 = 이 화면 도메인에서 "알림을 받은 순간" 살아 있는 대상 팀 건물 전부(S-11 — 그 뒤 새로 지은 건물에는 없다).
//   - 쉴드 깨짐: 「깨짐 확정」(OnShieldBrokenConfirmed) · 「깨짐 수신」(OnShieldBrokenReceived) — 건물 Id + 발동 순번이 같은 기록만 즉시 정지.
//   알려진 한계: Client는 전송 지연만큼 늦게 붙고 늦게 떨어진다(EMP R-7과 같다).
//
// 정지 방식(Plan 6-1-3 표):
//   만료(받은 순간 + 지속) = 꼬리 · 쉴드 깨짐 = 즉시 · 건물 파괴/철거(OnBuildingDied) = 즉시 · 업그레이드(OnBuildingUpgraded — 옛 Id) = 즉시 ·
//   경기 종료 = 꼬리(이후 이번 경기에서는 새로 붙이지 않는다) · 재경기(Initialize) = 즉시.
//   같은 스킬을 효과 중 다시 쓰면 → 다시 시작하지 않고 끝 시각과 순번만 바꾼다(서버도 쉴드를 새 양 · 새 시간으로 교체한다 — S-5).
//
// 위치 · 크기(S-12):
//   위치 = 건물 GameObject 위치(이미 이 화면 좌표 + 건물 높이 오프셋 — 빨강 화면도 그대로 맞다) + 높이 보정(Y만).
//   건물 GO가 없을 때만 타일 중심을 ViewConverter.ToView로 바꿔 대신 쓴다(진단 Fallback 계수).
//   크기 = 프리팹 원본 × 스킬 배율 하나(건물 종류별 표는 프리팹이 나온 뒤 필요하면 — S-12). 건물은 움직이지 않으므로 따라가기는 없다.
//
// 진단 로그(WORKFLOW [4] — 개발 축, 머리 [SKILL-BVFX], Diag=BuildingStatusVfx, DiagRev=1 — Plan 6-1-6 C-L10 · C-L11):
//   BuildingVfxStarted(발동 1건 화면 요약) · BuildingVfxExtended(재사용 연장) · BuildingVfxSkipped(NoPrefab · LoopContract — Warn) ·
//   BuildingVfxStopped(정지 — 같은 프레임 · 같은 스킬 · 같은 경로는 1줄로 묶음, Path=Expire/Broken/Died/Upgraded/GameEnd/Reinit/Gone/Lost/Destroy) ·
//   BuildingVfxFault(예외 — Warn). 모두 상태가 바뀌는 순간에만 남기고 매 프레임 줄은 없다(LogRules 1.14-8).
//   머리를 [SKILL-SHIELD]가 아니라 [SKILL-BVFX]로 둔 이유: 이 장치는 쉴드 전용이 아니라 단계 B 건물 재생도 함께 쓴다.
//
// 예외 안전: Host · 싱글에서는 발동 확정 · 깨짐 확정이 서버 처리 안에서 동기로 발행된다 — 모든 이벤트 처리 · 매 프레임 처리는
//   try/catch로 감싸 진단 줄만 남긴다(서버 판정이 깨지지 않게).
//
// 배치: 씬에 수동 배치하지 않는다. GameBootstrapper(LoadMap)가 자기 GameObject에 AddComponent하고 맵마다 Initialize를 부른다.
//
// Presentation 레이어 — Application(이벤트 · UseCase · 값 객체 · IBuildingObjectProvider · GameLog · NetworkContext) + Core(ViewConverter · HexMetrics) + Domain.
//   서버 판정 · 피해 · HP · 쉴드 양에는 손대지 않는 "표현 전용" 컴포넌트다.
// ============================================================================

using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 건물 대상 효과가 있는 지원 스킬의 발동 표현(확정 · 수신)을 받아 대상 건물마다 건물 상태 VFX를 붙이고 · 떼는 화면 구독자.
    /// </summary>
    public sealed class BuildingStatusVfxPresenter : MonoBehaviour
    {
        // ── 기록 열쇠: 건물 Id + 시전 팀 + 슬롯(= 발동 스킬) — 한 건물에 서로 다른 스킬 이펙트가 함께 붙을 수 있다 ──
        //   readonly struct + IEquatable — Dictionary 키로 쓸 때 상자 씌우기(박싱) 할당이 생기지 않게.
        private readonly struct BuildingVfxKey : IEquatable<BuildingVfxKey>
        {
            public readonly int BuildingId;
            public readonly TeamId CasterTeam;
            public readonly int Slot;

            public BuildingVfxKey(int buildingId, TeamId casterTeam, int slot)
            {
                BuildingId = buildingId;
                CasterTeam = casterTeam;
                Slot = slot;
            }

            public bool Equals(BuildingVfxKey other)
                => BuildingId == other.BuildingId && CasterTeam == other.CasterTeam && Slot == other.Slot;

            public override bool Equals(object obj) => obj is BuildingVfxKey other && Equals(other);

            public override int GetHashCode() => (BuildingId * 397) ^ ((int)CasterTeam * 31) ^ Slot;
        }

        // ── 건물 하나 · 스킬 하나의 재생 기록(재사용 때 끝 시각만 고쳐 쓰려고 class) ──
        private sealed class ActiveBuildingVfx
        {
            public BuildingData Building;   // 이펙트를 붙인 건물(참조로 들고 있다 — 업그레이드 · 재생성 판별)
            public VfxPoolItem Item;        // 재생 중인 풀 항목(VFX 컨테이너 아래 — 건물의 자식 아님)
            public ulong Lease;             // 시작할 때 받은 확인 번호(정지는 이 번호로만)
            public float EndTime;           // 꼬리 정지할 게임 시간(Time.time 기준)
            public float StartTime;         // [진단] 처음 시작한 게임 시간
            public int Sequence;            // 마지막으로 시작 · 연장시킨 발동 순번(깨짐 알림과 맞춰 본다)
            public bool Shielded;           // 이 스킬의 건물 효과에 쉴드가 있는가(깨짐 알림으로 끌 대상인가)
        }

        // 정지 줄을 "같은 프레임 · 같은 스킬 · 같은 경로"끼리 1줄로 묶는 계수.
        private struct StopGroup
        {
            public TeamId CasterTeam;
            public int Slot;
            public int Sequence;
            public string Path;
            public bool Immediate;
            public int Count;
            public int LeaseMatched;
            public int MaxActiveMs;
        }

        // 의존성(Initialize로 주입).
        private SkillActivationUseCase _skillActivation;      // 팀 · 슬롯 → 스킬 데이터(이 화면 빌드의 로드아웃)
        private EffectManager _effects;                       // VFX 시작 · 정지(풀 관리 포함)
        private BuildingPlacementUseCase _buildings;          // 이 화면 도메인의 건물 목록(대상 찾기 · 생존 확인)
        private IBuildingObjectProvider _buildingObjects;     // 건물 Id → 건물 GameObject(위치용). null이면 타일 중심으로 대신

        private readonly Dictionary<BuildingVfxKey, ActiveBuildingVfx> _active = new Dictionary<BuildingVfxKey, ActiveBuildingVfx>();
        private readonly List<BuildingVfxKey> _keyBuffer = new List<BuildingVfxKey>();      // 매 프레임 순회용
        private readonly List<BuildingVfxKey> _eventKeyBuffer = new List<BuildingVfxKey>(); // 이벤트(파괴 · 업그레이드 · 깨짐) 처리용
        private readonly List<StopGroup> _stopGroups = new List<StopGroup>(4);

        private bool _gameEnded;

        private IDisposable _confirmedSubscription;
        private IDisposable _receivedSubscription;
        private IDisposable _brokenConfirmedSubscription;
        private IDisposable _brokenReceivedSubscription;
        private IDisposable _buildingDiedSubscription;
        private IDisposable _buildingUpgradedSubscription;
        private IDisposable _gameEndSubscription;

        private const string ChannelConfirmed = "Confirmed";
        private const string ChannelReceived = "Received";

        /// <summary>
        /// 의존성을 주입하고 이벤트 7개(발동 확정 · 수신, 깨짐 확정 · 수신, 건물 파괴, 건물 업그레이드, 경기 종료)를 구독한다.
        /// 맵을 불러올 때마다 GameBootstrapper가 호출한다(재호출 안전 — 이전 경기의 이펙트는 입자까지 즉시 지운다).
        /// </summary>
        /// <param name="skillActivation">이번 맵의 스킬 발동 UseCase(스킬 데이터 조회용). null이면 붙이지 않는다.</param>
        /// <param name="effects">VFX 매니저. null이면 붙이지 않는다.</param>
        /// <param name="buildings">이번 맵의 건물 배치 UseCase(대상 건물 찾기). null이면 붙이지 않는다.</param>
        /// <param name="buildingObjects">건물 Id → 건물 GameObject(위치용). null이면 타일 중심으로 대신한다.</param>
        public void Initialize(
            SkillActivationUseCase skillActivation,
            EffectManager effects,
            BuildingPlacementUseCase buildings,
            IBuildingObjectProvider buildingObjects)
        {
            DisposeSubscriptions();
            // 이전 맵(경기)의 이펙트가 남아 있으면 지운다 — 그 건물들은 맵 정리 때 이미 사라졌다(재경기 = 즉시).
            StopAll(immediate: true, path: "Reinit");

            _skillActivation = skillActivation;
            _effects = effects;
            _buildings = buildings;
            _buildingObjects = buildingObjects;
            _gameEnded = false;

            // 채널 이름을 진단 로그에 남기려고 블록 본문 람다로 감싼다(처리 자체는 두 채널이 같다 — LogRules 1.7 람다 주의).
            _confirmedSubscription = GameEvents.OnSkillActivationConfirmed.Subscribe(p => { OnSkillActivation(p, ChannelConfirmed); });
            _receivedSubscription = GameEvents.OnSkillActivationReceived.Subscribe(p => { OnSkillActivation(p, ChannelReceived); });
            _brokenConfirmedSubscription = GameEvents.OnShieldBrokenConfirmed.Subscribe(p => { OnShieldBroken(p, ChannelConfirmed); });
            _brokenReceivedSubscription = GameEvents.OnShieldBrokenReceived.Subscribe(p => { OnShieldBroken(p, ChannelReceived); });
            _buildingDiedSubscription = GameEvents.OnBuildingDied.Subscribe(OnBuildingDied);
            _buildingUpgradedSubscription = GameEvents.OnBuildingUpgraded.Subscribe(OnBuildingUpgraded);
            _gameEndSubscription = GameEvents.OnGameEnd.Subscribe(OnGameEnd);
        }

        /// <summary> 파괴(씬 언로드) 시 정적 이벤트 허브 구독을 해제하고 남은 이펙트를 지운다. </summary>
        private void OnDestroy()
        {
            DisposeSubscriptions();
            StopAll(immediate: true, path: "Destroy");
        }

        private void DisposeSubscriptions()
        {
            _confirmedSubscription?.Dispose(); _confirmedSubscription = null;
            _receivedSubscription?.Dispose(); _receivedSubscription = null;
            _brokenConfirmedSubscription?.Dispose(); _brokenConfirmedSubscription = null;
            _brokenReceivedSubscription?.Dispose(); _brokenReceivedSubscription = null;
            _buildingDiedSubscription?.Dispose(); _buildingDiedSubscription = null;
            _buildingUpgradedSubscription?.Dispose(); _buildingUpgradedSubscription = null;
            _gameEndSubscription?.Dispose(); _gameEndSubscription = null;
        }

        // ====================================================================
        // 발동 알림 → 붙이기 · 연장
        // ====================================================================

        private void OnSkillActivation(SkillActivationPresentation p, string channel)
        {
            try
            {
                HandleActivation(p, channel);
            }
            catch (Exception ex)
            {
                LogFault("Activation", channel, p.Team, p.Slot, p.Sequence, ex);
            }
        }

        private void HandleActivation(SkillActivationPresentation p, string channel)
        {
            if (this == null || _gameEnded) return;
            if (_skillActivation == null || _effects == null || _buildings == null) return;

            // 팀 · 슬롯으로 이 화면 빌드의 스킬 데이터를 찾는다. 못 찾으면 발동 VFX 구독자가 이미 Warn을 남긴다(같은 사건 중복 금지).
            SkillData skill = _skillActivation.FindSkillData(p.Team, p.Slot);
            if (skill == null) return;
            // 타입 C이고 「건물 각각」 대상 효과가 있는 스킬만(신목의 각성 · 단계 B 재생의 뿌리). 그 밖의 스킬은 조용히 지나간다
            //   — 포격 · EMP · 강화 4종 · 전술 지령 · 대지의 갑주의 로그가 이 장치 때문에 늘지 않는다.
            if (skill.Mechanic != SkillMechanicType.GlobalStatusChange || !skill.HasBuildingEffect()) return;
            // 지속이 없으면 서버도 효과를 걸지 않는다(서비스가 InvalidValue로 거부) — 붙이지 않는다.
            if (!(skill.Duration > 0f)) return;

            GameObject prefab = skill.BuildingStatusVfxPrefab;
            if (prefab == null)
            {
                // [진단 C-L11] 칸이 비어 있다 — 효과(건물 쉴드)는 그대로 걸렸고 화면에만 이펙트가 없다(발동 1건당 화면마다 1줄).
                LogSkipped(p, channel, "NoPrefab", null);
                return;
            }
            if (!_effects.IsUnitBuffLoopContractSatisfied(prefab))
            {
                // 계약(Looping ON · Stop Action None) 위반 프리팹 — 재생하지 않는다(유닛 상태 VFX와 같은 원칙).
                LogSkipped(p, channel, "LoopContract", prefab.name);
                return;
            }

            TeamId targetTeam = skill.ResolveTargetTeam(p.Team);
            bool shielded = skill.HasBuildingShield();
            float now = Time.time;
            float endTime = now + skill.Duration;

            int targets = 0, attached = 0, extended = 0, fallback = 0, failed = 0, poolCreated = 0;
            string firstFail = null;
            foreach (BuildingData building in _buildings.Buildings.Values)
            {
                if (building == null || !building.IsAlive || building.Team != targetTeam) continue;
                targets++;

                var key = new BuildingVfxKey(building.Id, p.Team, p.Slot);
                if (_active.TryGetValue(key, out ActiveBuildingVfx current))
                {
                    // 같은 건물 · 같은 스킬이 이미 붙어 있다 — 다시 시작하지 않고 끝 시각 · 순번만 바꾼다(재사용 연장).
                    bool sameBuilding = ReferenceEquals(current.Building, building);
                    if (sameBuilding && current.Item != null && current.Item.IsStatusLoopActive(current.Lease))
                    {
                        current.EndTime = endTime;
                        current.Sequence = p.Sequence;
                        current.Shielded = shielded;
                        extended++;
                        continue;
                    }
                    // 재생이 바깥에서 끝났거나 다른 건물 — 옛 기록을 지우고 아래에서 새로 시작한다.
                    _active.Remove(key);
                    StopAndGroup(key, current, immediate: true, path: "Replaced");
                }

                Vector3 position = ResolveBuildingPosition(building, out bool usedTileCenter);
                if (usedTileCenter) fallback++;
                VfxPoolItem item = _effects.StartBuildingStatusVfx(prefab, position, skill.BuildingStatusVfxScale,
                    skill.BuildingStatusVfxOffsetY, out ulong lease, out bool createdNew, out string failReason);
                if (item == null || lease == 0)
                {
                    failed++;
                    if (firstFail == null) firstFail = failReason ?? "NotStarted";
                    continue;
                }
                if (createdNew) poolCreated++;
                attached++;
                _active[key] = new ActiveBuildingVfx
                {
                    Building = building, Item = item, Lease = lease, EndTime = endTime, StartTime = now,
                    Sequence = p.Sequence, Shielded = shielded
                };
            }

            // [진단 C-L10] 발동 1건의 이 화면 요약 1줄.
            LogStarted(p, channel, targetTeam, prefab.name, skill, targets, attached, extended, fallback, failed, firstFail, poolCreated);
            if (extended > 0) LogExtended(p, channel, extended, skill.Duration);
            FlushStopGroups();
        }

        // 건물 위치(이 화면 좌표). 건물 GO가 없으면 타일 중심을 화면 좌표로 바꿔 대신 쓴다(이 경우 건물 높이 오프셋은 빠진다).
        private Vector3 ResolveBuildingPosition(BuildingData building, out bool usedTileCenter)
        {
            GameObject go = _buildingObjects?.GetBuildingObject(building.Id);
            usedTileCenter = go == null;
            return go != null ? go.transform.position : ViewConverter.ToView(HexMetrics.HexToWorld(building.Position));
        }

        // ====================================================================
        // 정지 경로 — 깨짐 · 파괴 · 업그레이드 · 경기 종료 · 매 프레임(만료)
        // ====================================================================

        /// <summary>
        /// 쉴드 깨짐 알림: 깨진 건물마다 같은 발동 순번으로 붙인(쉴드가 있는 스킬의) 기록을 즉시 정지한다(Path=Broken).
        /// </summary>
        private void OnShieldBroken(ShieldBrokenPresentation p, string channel)
        {
            try
            {
                if (this == null || p.BuildingCount == 0 || _active.Count == 0) return;
                IReadOnlyList<int> ids = p.BuildingIds;
                for (int i = 0; i < ids.Count; i++)
                    StopMatching(ids[i], p.Sequence, requireShield: true, path: "Broken");
                FlushStopGroups();
            }
            catch (Exception ex)
            {
                LogFault("ShieldBroken", channel, TeamId.Neutral, -1, p.Sequence, ex);
            }
        }

        /// <summary> 건물 파괴 · 철거 → 그 건물의 모든 건물 상태 이펙트를 즉시 정지(Path=Died). </summary>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            try
            {
                if (this == null || e.Building == null || _active.Count == 0) return;
                StopMatching(e.Building.Id, 0, requireShield: false, path: "Died");
                FlushStopGroups();
            }
            catch (Exception ex)
            {
                LogFault("BuildingDied", null, TeamId.Neutral, -1, 0, ex);
            }
        }

        /// <summary>
        /// 건물 업그레이드(옛 Id가 사라지고 새 Id가 생김) → 옛 건물의 이펙트를 즉시 정지(Path=Upgraded).
        /// 새 건물에는 붙이지 않는다 — 서버도 업그레이드 순간 옛 건물의 쉴드를 버린다(Plan S-11).
        /// </summary>
        private void OnBuildingUpgraded(BuildingUpgradedEvent e)
        {
            try
            {
                if (this == null || _active.Count == 0) return;
                StopMatching(e.OldBuildingId, 0, requireShield: false, path: "Upgraded");
                FlushStopGroups();
            }
            catch (Exception ex)
            {
                LogFault("BuildingUpgraded", null, TeamId.Neutral, -1, 0, ex);
            }
        }

        // 건물 Id가 같은 기록을 즉시 정지한다. sequence > 0이면 순번도 같아야 하고, requireShield면 쉴드가 있는 스킬의 기록만.
        private void StopMatching(int buildingId, int sequence, bool requireShield, string path)
        {
            _eventKeyBuffer.Clear();
            foreach (KeyValuePair<BuildingVfxKey, ActiveBuildingVfx> pair in _active)
            {
                if (pair.Key.BuildingId != buildingId) continue;
                if (sequence > 0 && pair.Value.Sequence != sequence) continue;
                if (requireShield && !pair.Value.Shielded) continue;
                _eventKeyBuffer.Add(pair.Key);
            }
            for (int i = 0; i < _eventKeyBuffer.Count; i++)
            {
                BuildingVfxKey key = _eventKeyBuffer[i];
                if (!_active.TryGetValue(key, out ActiveBuildingVfx entry)) continue;
                _active.Remove(key);
                StopAndGroup(key, entry, immediate: true, path: path);
            }
            _eventKeyBuffer.Clear();
        }

        /// <summary> 경기가 끝나면 남은 이펙트를 모두 꼬리 정지하고, 이번 경기에서는 새로 붙이지 않는다. </summary>
        private void OnGameEnd(GameEndEvent e)
        {
            try
            {
                if (this == null) return;
                _gameEnded = true;
                StopAll(immediate: false, path: "GameEnd");
            }
            catch (Exception ex)
            {
                LogFault("GameEnd", null, TeamId.Neutral, -1, 0, ex);
            }
        }

        /// <summary>
        /// 매 프레임: 끝 시각이 지난 기록은 꼬리 정지(Expire), 건물이 목록에서 사라졌거나 죽은 기록은 즉시 정지(Gone — 이벤트를 놓친 경우의
        /// 안전망), 재생이 바깥에서 끝난 기록은 버린다(Lost). 기록이 없으면 바로 돌아간다(비용 없음).
        /// </summary>
        private void Update()
        {
            if (_active.Count == 0) return;
            float now = Time.time;
            _keyBuffer.Clear();
            foreach (KeyValuePair<BuildingVfxKey, ActiveBuildingVfx> pair in _active) _keyBuffer.Add(pair.Key);
            for (int i = 0; i < _keyBuffer.Count; i++)
            {
                BuildingVfxKey key = _keyBuffer[i];
                if (!_active.TryGetValue(key, out ActiveBuildingVfx entry)) continue;
                try
                {
                    TickEntry(key, entry, now);
                }
                catch (Exception ex)
                {
                    LogFault("Tick", null, key.CasterTeam, key.Slot, entry.Sequence, ex);
                    _active.Remove(key);
                    try { StopEntry(entry, immediate: true); } catch { /* 정지마저 실패하면 풀이 정리한다 */ }
                }
            }
            _keyBuffer.Clear();
            FlushStopGroups();
        }

        private void TickEntry(BuildingVfxKey key, ActiveBuildingVfx entry, float now)
        {
            // ① 건물이 사라졌다(이벤트를 놓친 경우) — 즉시.
            BuildingData current = _buildings != null ? _buildings.GetBuilding(key.BuildingId) : null;
            if (entry.Building == null || !entry.Building.IsAlive || !ReferenceEquals(current, entry.Building))
            {
                _active.Remove(key);
                StopAndGroup(key, entry, immediate: true, path: "Gone");
                return;
            }
            // ② 효과 시간 끝 — 꼬리.
            if (now >= entry.EndTime)
            {
                _active.Remove(key);
                StopAndGroup(key, entry, immediate: false, path: "Expire");
                return;
            }
            // ③ 재생이 바깥에서 끝났다(항목 파괴 등 비정상) — 기록만 버린다.
            if (entry.Item == null || !entry.Item.IsStatusLoopActive(entry.Lease))
            {
                _active.Remove(key);
                StopAndGroup(key, entry, immediate: true, path: "Lost");
            }
        }

        private void StopAll(bool immediate, string path)
        {
            if (_active.Count == 0) return;
            foreach (KeyValuePair<BuildingVfxKey, ActiveBuildingVfx> pair in _active)
                StopAndGroup(pair.Key, pair.Value, immediate, path);
            _active.Clear();
            FlushStopGroups();
        }

        private void StopAndGroup(BuildingVfxKey key, ActiveBuildingVfx entry, bool immediate, string path)
        {
            bool stopped = StopEntry(entry, immediate);
            AddStopGroup(key, entry, immediate, path, stopped);
        }

        /// <summary>
        /// 기록 하나의 이펙트를 멈춘다(확인 번호가 맞을 때만 실제로 멈춘다).
        /// EffectManager는 Unity 오브젝트라 ?. 대신 == null 검사(파괴된 객체도 null로 보는 Unity 방식)를 쓴다.
        /// </summary>
        /// <returns>실제로 멈췄으면 true.</returns>
        private bool StopEntry(ActiveBuildingVfx entry, bool immediate)
        {
            if (entry == null || entry.Item == null) return false;
            bool stopped = _effects != null
                ? _effects.StopBuildingStatusVfx(entry.Item, entry.Lease, immediate)
                : entry.Item.StopStatusLoop(entry.Lease, immediate);
            entry.Item = null; // 정지 뒤에는 이 기록이 그 항목을 다시 건드리지 않는다(풀로 돌아가 남의 재생이 될 수 있다).
            return stopped;
        }

        // ====================================================================
        // 진단 로그(개발 축 — 릴리스 빌드에서는 호출 · 문자열 조립까지 통째로 사라진다, LogRules 1.7)
        //   머리 [SKILL-BVFX], Diag=BuildingStatusVfx, DiagRev=1. 시간 · 배율 · 위치는 밀리 정수.
        // ====================================================================

        private static string DiagRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        private static int DiagMilli(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return -1;
            return Mathf.RoundToInt(value * 1000f);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void AddStopGroup(BuildingVfxKey key, ActiveBuildingVfx entry, bool immediate, string path, bool stopped)
        {
            int activeMs = DiagMilli(Time.time - entry.StartTime);
            for (int i = 0; i < _stopGroups.Count; i++)
            {
                StopGroup g = _stopGroups[i];
                if (g.CasterTeam == key.CasterTeam && g.Slot == key.Slot && g.Path == path && g.Immediate == immediate)
                {
                    g.Count++;
                    if (stopped) g.LeaseMatched++;
                    if (activeMs > g.MaxActiveMs) g.MaxActiveMs = activeMs;
                    if (entry.Sequence > g.Sequence) g.Sequence = entry.Sequence;
                    _stopGroups[i] = g;
                    return;
                }
            }
            _stopGroups.Add(new StopGroup
            {
                CasterTeam = key.CasterTeam, Slot = key.Slot, Sequence = entry.Sequence, Path = path, Immediate = immediate,
                Count = 1, LeaseMatched = stopped ? 1 : 0, MaxActiveMs = activeMs
            });
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void FlushStopGroups()
        {
            for (int i = 0; i < _stopGroups.Count; i++)
            {
                StopGroup g = _stopGroups[i];
                GameLog.Dev.Info("Skill", nameof(BuildingStatusVfxPresenter), "[SKILL-BVFX] BuildingVfxStopped",
                    FormattableString.Invariant(
                        $"Diag=BuildingStatusVfx, DiagRev=1, Event=BuildingVfxStopped, Role={DiagRole()}, Team={g.CasterTeam}, Slot={g.Slot}, Sequence={g.Sequence}, Path={g.Path}, Immediate={g.Immediate}, Count={g.Count}, LeaseMatched={g.LeaseMatched}, MaxActiveMs={g.MaxActiveMs}"));
            }
            _stopGroups.Clear();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStarted(SkillActivationPresentation p, string channel, TeamId targetTeam, string prefabName, SkillData skill,
            int targets, int attached, int extended, int fallback, int failed, string firstFail, int poolCreated)
        {
            GameLog.Dev.Info("Skill", nameof(BuildingStatusVfxPresenter), "[SKILL-BVFX] BuildingVfxStarted",
                FormattableString.Invariant(
                    $"Diag=BuildingStatusVfx, DiagRev=1, Event=BuildingVfxStarted, Role={DiagRole()}, Channel={channel}, Sequence={p.Sequence}, Team={p.Team}, Slot={p.Slot}, BuildingId={p.BuildingId}, TargetTeam={targetTeam}, Prefab={prefabName}, DurationMs={DiagMilli(skill.Duration)}, ScaleMilli={DiagMilli(skill.BuildingStatusVfxScale)}, OffsetYmm={DiagMilli(skill.BuildingStatusVfxOffsetY)}, Targets={targets}, Attached={attached}, Extended={extended}, TileCenterFallback={fallback}, StartFailed={failed}, FailReason={(firstFail ?? "None")}, PoolCreated={poolCreated}, ViewFlipped={ViewConverter.IsFlipped}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogExtended(SkillActivationPresentation p, string channel, int extended, float duration)
        {
            GameLog.Dev.Info("Skill", nameof(BuildingStatusVfxPresenter), "[SKILL-BVFX] BuildingVfxExtended",
                FormattableString.Invariant(
                    $"Diag=BuildingStatusVfx, DiagRev=1, Event=BuildingVfxExtended, Role={DiagRole()}, Channel={channel}, Sequence={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Count={extended}, DurationMs={DiagMilli(duration)}, Restarted=False"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSkipped(SkillActivationPresentation p, string channel, string reason, string prefabName)
        {
            GameLog.Dev.Warn("Skill", nameof(BuildingStatusVfxPresenter), "[SKILL-BVFX] BuildingVfxSkipped",
                FormattableString.Invariant(
                    $"Diag=BuildingStatusVfx, DiagRev=1, Event=BuildingVfxSkipped, Role={DiagRole()}, Channel={channel}, Sequence={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Reason={reason}, Prefab={(prefabName ?? "None")}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFault(string stage, string channel, TeamId team, int slot, int sequence, Exception ex)
        {
            GameLog.Dev.Warn("Skill", nameof(BuildingStatusVfxPresenter), "[SKILL-BVFX] BuildingVfxFault",
                FormattableString.Invariant(
                    $"Diag=BuildingStatusVfx, DiagRev=1, Event=BuildingVfxFault, Role={DiagRole()}, Stage={stage}, Channel={(channel ?? "None")}, Team={team}, Slot={slot}, Sequence={sequence}, Error={ex.GetType().Name}"));
        }
    }
}
