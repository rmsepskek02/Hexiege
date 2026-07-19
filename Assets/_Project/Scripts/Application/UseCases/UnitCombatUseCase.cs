// ============================================================================
// UnitCombatUseCase.cs
// 유닛 전투(공격/피격/사망)를 처리하는 UseCase.
//
// 흐름:
//   1. 유닛 이동 완료 후 UnitView가 TryAttack() 호출
//   2. 인접 타일에서 적 유닛 또는 건물을 탐색 (IDamageable)
//   3. 적이 있으면 공격 방향 계산 → 데미지 적용 → 이벤트 발행
//   4. 타겟 HP가 0 이하면 사망 이벤트 발행 (삭제는 각 Manager/UseCase가 처리)
//
// Application 레이어 — Domain에 의존.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛 전투(공격/피격/사망)를 처리하는 UseCase.
    /// NetworkManager에 직접 의존하지 않고 NetworkContext 정적 홀더를 통해
    /// 멀티플레이 서버 전용 분기를 처리 (Application → Infrastructure 역방향 의존 방지).
    /// </summary>
    public class UnitCombatUseCase
    {
        // 근접 유닛(AttackRange < 1.0) 전용 판정 거리 상수.
        // 유닛 타겟: 두 유닛 메시가 시각적으로 닿아 보이는 최소 거리.
        // 기존 0.483f(AttackRange 0.5 × TileHeight 0.866)에서는 두 유닛 사이에 시각적 여백이 생기므로,
        // 메시가 실제로 맞닿아 보이는 0.3f로 줄여서 자연스러운 근접 공격 연출을 달성.
        private const float MeleeContactDist = 0.3f;

        // 건물 타겟: 건물 메시가 유닛보다 크므로 유닛 타겟보다 일찍 감지해도 시각적으로 자연스러움.
        // MeleeContactDist + BuildingDetectionRadius = 0.5f 거리에서 건물 공격 시작.
        // 건물 메시가 크기 때문에 0.2f 일찍 감지해도 유닛이 건물 메시에 닿아 보임.
        private const float BuildingDetectionRadius = 0.2f;

        private readonly HexGrid _grid;
        private readonly UnitSpawnUseCase _unitSpawn;
        private readonly BuildingPlacementUseCase _buildingPlacement;
        private readonly IEntityPositionProvider _positionProvider;

        // 월드↔헥스 좌표 변환기. Core(HexMetrics / ViewConverter) 의존을 피하기 위한 인터페이스 주입.
        private readonly IHexCoordinateMapper _mapper;

        // 유닛 타입별 특수 공격(범위/파도 등) 핸들러 테이블.
        // 외부 의존이 없는 순수 전략 테이블이므로(정적 스탯 테이블과 같은 성격)
        // composition root를 거치지 않고 여기서 직접 생성한다.
        // 등록되지 않은 일반 유닛은 TryGet이 null → 특수 공격 없음(기존 동작 무변경).
        private readonly SpecialAttackRegistry _specialAttacks = new SpecialAttackRegistry();

        // 특수 공격(도끼병 휩쓸기) 튜닝값 — SpecialAttackConfig(SO)에서 GameBootstrapper가
        // 읽어 float로 주입한다. SO가 없으면 생성자 기본값(1.0 / 120)이 폴백으로 쓰인다.
        // Application이 Infrastructure의 SO를 직접 참조하지 않도록 원시값만 보관한다.
        private readonly float _sweepReach;
        private readonly float _sweepArcHalfAngle;

        // 특수 공격(TorrentSpirit 파도) 튜닝값 — 마찬가지로 SO에서 float로 주입(미주입 시 코드 폴백).
        private readonly float _waveWidth;
        private readonly float _waveLength;
        private readonly float _waveTravelTime;
        private readonly float _waveHeal;

        // 특수 유닛(BloomFairy 힐러) 지속 회복(HoT) 튜닝값 — SO에서 float로 주입(미주입 시 코드 폴백).
        // _bloomHealAmount: 지속 회복 총량(HP, 기본 20). _bloomHealDuration: 지속시간(초, 기본 3).
        private readonly int _bloomHealAmount;
        private readonly float _bloomHealDuration;

        /// <summary>
        /// 현재 진행 중인 파도(이동 전선) 목록. TorrentSpirit이 공격할 때 하나씩 추가되고,
        /// TickWaves()가 매 서버 틱마다 전진시키며 닿은 유닛에 효과를 1회 적용한 뒤 완료되면 제거한다.
        /// 서버(싱글=로컬 서버 / 멀티=서버)에서만 채워지고 소비된다.
        /// </summary>
        private readonly List<ActiveWave> _activeWaves = new List<ActiveWave>(4);

        // TickWaves 재사용 버퍼(모바일 GC 절감 + 순회 중 컬렉션 변경 회피).
        // 파도가 닿은 유닛을 먼저 수집한 뒤 일괄 적용한다(피해로 사망 시 Dictionary 변경 예외 방지).
        private readonly List<UnitData> _waveDamageBuffer = new List<UnitData>(8);
        private readonly List<UnitData> _waveHealBuffer = new List<UnitData>(8);
        // 파도가 닿은 "적 건물"을 담는 버퍼(BUG-002 — special-only 유닛도 공성 가능하도록). 유닛과 분리 수집.
        private readonly List<BuildingData> _waveBuildingBuffer = new List<BuildingData>(4);

        /// <summary>
        /// 싱글플레이 전용 전투 상태 추적.
        /// key=유닛Id, value=(타겟Id, 타겟이 유닛인지).
        /// 전투 중이 아닌 유닛은 Dictionary에 없음.
        /// 멀티플레이에서는 NetworkCombatController._unitCombatTargets가 이 역할을 수행.
        /// </summary>
        private readonly Dictionary<int, (int targetId, bool isUnit)> _combatTargets
            = new Dictionary<int, (int targetId, bool isUnit)>();

        /// <summary>
        /// 싱글플레이 전용: 데미지 적용 예약(타격 프레임 딜레이)을 위한 내부 구조체.
        /// 공격 애니메이션 사이클 안의 히트 프레임별로 1개씩 생성된다.
        ///
        /// TryAttack()에서 공격 애니메이션이 시작되는 순간,
        /// 각 HitFrameTimes 원소마다 PendingHit 하나를 _pendingHits 리스트에 등록한다.
        /// TickPendingHits()가 매 프레임 RemainingDelay를 감소시키며,
        /// 0 이하가 되면 데미지(ExecuteAttack)를 적용한 뒤 리스트에서 제거.
        ///
        /// 멀티플레이에서는 이 구조체 대신 MonoBehaviour 기반 코루틴을 사용한다
        /// (NetworkCombatController.DelayedAttackDamage).
        /// </summary>
        private struct PendingHit
        {
            /// <summary>공격자(이 유닛의 Attacker 스탯 기준으로 데미지 적용).</summary>
            public UnitData Attacker;
            /// <summary>타겟 엔티티(유닛 또는 건물)의 Id.</summary>
            public int TargetId;
            /// <summary>타겟이 유닛인지 여부. true=유닛, false=건물.</summary>
            public bool TargetIsUnit;
            /// <summary>남은 딜레이(초). 0 이하가 되면 데미지 적용.</summary>
            public float RemainingDelay;
        }

        /// <summary>
        /// 싱글플레이 전용: 대기 중인 데미지 적용 예약 목록.
        /// TickPendingHits()에서 매 프레임 타이머를 감소시켜 만료된 항목을 실제 데미지로 전환.
        /// </summary>
        private readonly List<PendingHit> _pendingHits = new List<PendingHit>();

        /// <param name="sweepReach">
        ///   도끼병 휩쓸기 전방 부채꼴 판정의 월드 반경. SpecialAttackConfig에서 주입.
        ///   미주입 시 기본값 1.0(코드 폴백) 사용.
        /// </param>
        /// <param name="sweepArcHalfAngle">
        ///   도끼병 휩쓸기 전방 부채꼴의 반각(도 단위). SpecialAttackConfig에서 주입.
        ///   미주입 시 기본값 120(코드 폴백) 사용.
        /// </param>
        /// <param name="waveWidth">TorrentSpirit 파도 가로 폭(월드). 미주입 시 기본값 3.</param>
        /// <param name="waveLength">TorrentSpirit 파도 전방 길이(월드). 미주입 시 기본값 3.</param>
        /// <param name="waveTravelTime">TorrentSpirit 파도 이동 시간(초). 미주입 시 기본값 0.5.</param>
        /// <param name="waveHeal">TorrentSpirit 파도 아군 회복량. 미주입 시 기본값 10.</param>
        /// <param name="bloomHealAmount">BloomFairy 지속 회복(HoT) 총량(HP). 미주입 시 기본값 20.</param>
        /// <param name="bloomHealDuration">BloomFairy 지속 회복(HoT) 지속시간(초). 미주입 시 기본값 3.</param>
        public UnitCombatUseCase(
            HexGrid grid,
            UnitSpawnUseCase unitSpawn,
            BuildingPlacementUseCase buildingPlacement,
            IEntityPositionProvider positionProvider,
            IHexCoordinateMapper mapper,
            float sweepReach = 1.0f,
            float sweepArcHalfAngle = 120f,
            float waveWidth = 3f,
            float waveLength = 3f,
            float waveTravelTime = 0.5f,
            float waveHeal = 10f,
            float bloomHealAmount = 20f,
            float bloomHealDuration = 3f)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _buildingPlacement = buildingPlacement;
            _positionProvider = positionProvider;
            _mapper = mapper;
            _sweepReach = sweepReach;
            _sweepArcHalfAngle = sweepArcHalfAngle;
            _waveWidth = waveWidth;
            _waveLength = waveLength;
            _waveTravelTime = waveTravelTime;
            _waveHeal = waveHeal;
            // 회복 총량은 정수 HP로 다룬다(반올림). 지속시간은 초 단위 실수.
            _bloomHealAmount = Mathf.Max(0, Mathf.RoundToInt(bloomHealAmount));
            _bloomHealDuration = bloomHealDuration;
        }

        // ====================================================================
        // 사거리 계산 헬퍼
        // ====================================================================

        /// <summary>
        /// 공격자의 사거리 기반으로 유닛/건물 타겟에 대한 최대 판정 거리를 계산.
        ///
        /// FindFirstEnemyTarget, FindFirstEnemyInDetectRange, IsTargetInRange 세 메서드가
        /// 동일한 "근접/원거리 분기 + 건물 여유 거리" 패턴을 공유하므로 단일 메서드로 통합.
        ///
        /// - 공격 판정(isDetect=false): 근접(AttackRange &lt; 1.0)은 MeleeContactDist,
        ///   원거리는 AttackRange × TileHeight. 건물은 +BuildingDetectionRadius.
        /// - 감지 판정(isDetect=true): DetectRange × TileHeight (근접/원거리 동일).
        ///   건물은 +BuildingDetectionRadius.
        /// </summary>
        /// <param name="attacker">공격자 유닛 데이터.</param>
        /// <param name="isDetect">
        ///   true: 감지 사거리(DetectRange) 기준 계산.
        ///   false: 공격 사거리(AttackRange) 기준 계산.
        /// </param>
        /// <returns>
        /// (unitMaxDist, buildingMaxDist) 튜플.
        /// Epsilon(0.05f) 포함 — 부동소수점 경계 오차 방지.
        /// </returns>
        private (float unitMaxDist, float buildingMaxDist) CalculateRangeLimits(
            UnitData attacker, bool isDetect)
        {
            // 부동소수점 오차 방지용 여유값 (Pistoleer 0.866 = FlatTop 인접 거리 경계 케이스)
            const float Epsilon = 0.05f;

            float baseDist;

            if (isDetect)
            {
                // 감지 판정: 근접/원거리 구분 없이 DetectRange × TileHeight 사용.
                // [2026-05-11 통합] 모든 유닛이 동일한 감지 공식 사용.
                baseDist = attacker.DetectRange * _mapper.TileHeight;
            }
            else
            {
                // 공격 판정: 근접 유닛은 MeleeContactDist, 원거리는 AttackRange × TileHeight.
                // 근접 유닛(AttackRange < 1.0)은 두 유닛 메시가 시각적으로 닿는 0.3f 거리 사용.
                bool isMelee = attacker.AttackRange < 1.0f;
                baseDist = isMelee
                    ? MeleeContactDist
                    : attacker.AttackRange * _mapper.TileHeight;
            }

            float unitMaxDist = baseDist + Epsilon;
            // 건물 메시가 유닛보다 크므로 BuildingDetectionRadius만큼 일찍 감지/공격 시작.
            float buildingMaxDist = baseDist + BuildingDetectionRadius + Epsilon;

            return (unitMaxDist, buildingMaxDist);
        }

        /// <summary>
        /// 유닛의 사거리 내에 적이 있으면 공격을 실행.
        /// 이동 완료 후 UnitView에서 호출.
        ///
        /// 멀티플레이 모드에서는 서버만 전투를 처리하여 권위 있는 판정 보장.
        /// 클라이언트는 UnitView 애니메이션(시각 효과)만 처리하고 데미지는 서버에 위임.
        /// </summary>
        /// <returns>공격 성공 시 (타겟 Id, 타겟이 유닛인지 여부) 튜플, 실패 시 null</returns>
        public (int id, bool isUnit)? TryAttack(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return null;

            // 멀티플레이 모드에서는 서버/클라이언트 구분 없이 TryAttack 완전 차단.
            // 네트워크 모드에서 데미지는 NetworkCombatController가 단독으로 처리하며,
            // HitFrameTimes 배열의 각 타이머 후 ApplyAttackDamage를 호출하는 것이 규칙.
            // HOST(서버)도 TryAttack을 통해 즉시 데미지를 주면 규칙 2 위반(이중 데미지 + 타이밍 불일치).
            if (NetworkContext.IsNetworkActive) return null;

            // 쿨다운 중이면 공격 불가
            if (attacker.AttackCooldownRemaining > 0f) return null;

            // IDamageable을 구현하는 모든 적을 찾도록 로직 변경
            IDamageable target = FindFirstEnemyTarget(attacker);
            if (target == null) return null;

            int targetId = target.Id;
            bool targetIsUnit = target is UnitData;

            // --- 싱글플레이 전투 상태 변화 감지 및 이벤트 발행 ---
            // 멀티플레이에서는 NetworkCombatController가 _unitCombatTargets Dictionary와
            // StartCombat/ChangeTarget/StopCombat ClientRpc로 이 역할을 수행.
            if (!NetworkContext.IsNetworkActive)
            {
                if (_combatTargets.TryGetValue(attacker.Id, out var prev))
                {
                    // 이전에 전투 중이었고 타겟이 다르면 → ChangeTarget 이벤트
                    if (prev.targetId != targetId)
                    {
                        _combatTargets[attacker.Id] = (targetId, targetIsUnit);
                        GameEvents.OnCombatTargetChanged.OnNext(
                            new CombatTargetChangedEvent(attacker.Id, targetId, targetIsUnit));
                    }
                    // 같은 타겟이면 이벤트 없음 — Attack 루프가 이미 재생 중
                }
                else
                {
                    // 이전에 전투 중이 아니었으면 → StartCombat 이벤트
                    _combatTargets[attacker.Id] = (targetId, targetIsUnit);
                    GameEvents.OnCombatStarted.OnNext(
                        new CombatStartedEvent(attacker.Id, targetId, targetIsUnit));
                }
            }

            // 다중 히트 예약:
            // 공격 애니메이션 사이클 안의 각 히트 프레임마다 PendingHit을 등록한다.
            // 예) FlameSpirit은 6개 타이머가 예약되어 각각 독립적으로 데미지 적용.
            //
            // HitFrameTimes가 null이거나 비어 있으면 안전망으로 즉시 1회 데미지를 적용한다
            // (Inspector 미설정 대비).
            float[] hitTimes = attacker.HitFrameTimes;
            if (hitTimes == null || hitTimes.Length == 0)
            {
                // 기존 동작 유지(즉시 1회 데미지)
                ExecuteAttack(attacker, target);
            }
            else
            {
                for (int i = 0; i < hitTimes.Length; i++)
                {
                    _pendingHits.Add(new PendingHit
                    {
                        Attacker = attacker,
                        TargetId = targetId,
                        TargetIsUnit = targetIsUnit,
                        // 타이머는 0 이하에서 실행되므로, 0f인 경우 최소 1프레임 뒤 실행되도록 아주 작은 양수로 보정.
                        RemainingDelay = hitTimes[i] > 0f ? hitTimes[i] : 0.0001f
                    });
                }
            }

            // 공격 성공 → 쿨다운 시작 (TryAttack에서 한 번만 리셋)
            attacker.AttackCooldownRemaining = attacker.AttackCooldown;

            return (targetId, targetIsUnit);
        }

        /// <summary>
        /// 타겟 탐색만 수행. 데미지 없음, 쿨다운 리셋 없음.
        /// 멀티플레이 서버(NetworkCombatController)가 애니메이션 RPC를 전송하기 전에
        /// 타겟 유효성을 확인하는 용도.
        /// 타겟이 발견되면 (타겟 Id, 타겟이 유닛인지 여부) 튜플을 반환.
        ///
        /// Guard 조건은 TryAttack()과 동일:
        ///   - 공격자가 null이거나 사망 → null
        ///   - 멀티플레이 클라이언트(서버가 아닌 쪽) → null (서버 권위 보장)
        ///   - 공격 쿨다운 중 → null
        /// </summary>
        /// <param name="attacker">공격을 시도하는 유닛</param>
        /// <returns>공격 가능한 타겟이 있으면 (타겟 Id, 타겟이 유닛인지), 없으면 null</returns>
        public (int id, bool isUnit)? TryFindTarget(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return null;

            // 멀티플레이 모드에서는 서버만 전투 처리.
            // NetworkContext 정적 홀더로 Application → Infrastructure 역방향 의존 방지.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;

            // 쿨다운 중이면 공격 불가
            if (attacker.AttackCooldownRemaining > 0f) return null;

            // 사거리 내 가장 가까운 적 탐색 (데미지 미적용)
            IDamageable target = FindFirstEnemyTarget(attacker);
            if (target == null) return null;

            return (target.Id, target is UnitData);
        }

        /// <summary>
        /// 쿨다운과 무관하게 사거리 내 가장 가까운 적을 반환.
        /// 공격 권한(서버/클라이언트) 체크 없이 순수하게 적 탐색만 수행.
        ///
        /// 사용처:
        ///   1. OnUnitEnteredCombatHandler — 쿨다운 중 적 감지 시 타겟 ID 확보 (StartCombatClientRpc 전송)
        ///   2. TickCombat — 쿨다운 중 타겟 변경 감지 (ChangeTargetClientRpc 전송)
        ///
        /// TryFindTarget과의 차이:
        ///   TryFindTarget: 서버 권한 체크 + 쿨다운 체크 포함 → 쿨다운 중이면 null
        ///   FindNearestEnemy: 쿨다운 체크 없음 → 사거리 내 적이 있으면 항상 반환
        /// </summary>
        /// <param name="attacker">탐색 기준이 되는 유닛</param>
        /// <returns>사거리 내 가장 가까운 적이 있으면 (타겟 Id, 타겟이 유닛인지), 없으면 null</returns>
        public (int id, bool isUnit)? FindNearestEnemy(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return null;

            IDamageable target = FindFirstEnemyTarget(attacker);
            if (target == null) return null;

            return (target.Id, target is UnitData);
        }

        /// <summary>
        /// 실제 데미지를 적용하는 메서드.
        /// 각 히트 프레임(HitFrameTimes의 원소마다) 딜레이가 만료된 시점에 호출된다.
        ///
        /// 호출 경로:
        ///   - 싱글플레이: TickPendingHits()에서 만료된 PendingHit 항목별로 호출.
        ///   - 멀티플레이: NetworkCombatController.DelayedAttackDamage 코루틴(히트 개수만큼)에서 호출.
        ///
        /// 타겟 고정(Target Lock) 설계:
        ///   공격 모션을 시작한 순간 타겟이 확정됨.
        ///   딜레이 중 타겟이 사거리를 벗어나도 데미지 적용.
        ///   단, 아래 두 경우에만 취소:
        ///   1. 공격자가 딜레이 중 사망
        ///   2. 타겟이 딜레이 중 사망 (다른 유닛에게 먼저 처치됨)
        ///
        /// 쿨다운 리셋 규칙:
        ///   쿨다운은 공격 모션이 시작되는 순간(=TryAttack 또는 NetworkCombatController.ExecuteAttack) 한 번만 리셋.
        ///   다중 히트의 경우 각 히트마다 이 메서드가 여러 번 호출되므로 여기서 리셋하면
        ///   마지막 히트 시점에 쿨다운이 다시 처음으로 돌아가는 문제가 발생한다.
        ///   따라서 이 메서드는 데미지만 적용하고 쿨다운을 건드리지 않는다.
        /// </summary>
        /// <param name="attacker">공격하는 유닛</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        public void ApplyAttackDamage(UnitData attacker, int targetId, bool targetIsUnit)
        {
            // 딜레이 동안 공격자가 사망했을 수 있으므로 재확인
            if (attacker == null || !attacker.IsAlive) return;

            // 타겟을 Id로 재탐색 — 딜레이 동안 다른 유닛에게 먼저 처치되었을 수 있음
            IDamageable target = FindTargetById(targetId, targetIsUnit);
            if (target == null || !target.IsAlive) return;

            // 사거리 체크 없음 — 타겟 고정 설계.
            // 공격 모션 시작 시 타겟이 확정되므로, 이후 이탈해도 데미지 적용.

            // 데미지 적용 + 이벤트 발행
            ExecuteAttack(attacker, target);

            // 쿨다운 리셋은 여기서 하지 않음 — 위 주석 참조(다중 히트 호환).
        }

        /// <summary>
        /// 싱글플레이 전용 쿨다운 감소.
        /// GameBootstrapper.Update()에서 매 프레임 호출하여 모든 살아있는 유닛의 쿨다운을 감소.
        ///
        /// 멀티플레이에서는 NetworkCombatController.TickCombat()이 서버 Tick 주기로
        /// 직접 쿨다운을 감소시키므로, 이 메서드를 호출하면 안 됨 (이중 감소 발생).
        ///
        /// 기존 UnitView.Update()에 분산되어 있던 쿨다운 감소를 한 곳에서 일괄 처리.
        /// </summary>
        /// <param name="dt">경과 시간 (초). 일반적으로 Time.deltaTime을 전달.</param>
        public void TickCooldowns(float dt)
        {
            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit.AttackCooldownRemaining > 0f)
                    unit.AttackCooldownRemaining = Mathf.Max(0f, unit.AttackCooldownRemaining - dt);
            }
        }

        /// <summary>
        /// 싱글플레이 전용: 대기 중인 데미지 예약(PendingHit)의 타이머를 감소시키고,
        /// 만료된 항목에 대해 실제 데미지를 적용한다.
        /// GameBootstrapper.Update()에서 TickCooldowns 바로 뒤에 매 프레임 호출.
        ///
        /// 멀티플레이에서는 NetworkCombatController의 코루틴이 같은 역할을 수행하므로
        /// 이 메서드가 호출되어도 _pendingHits가 비어 있어 아무 동작도 하지 않는다
        /// (TryAttack 초입의 NetworkActive 가드로 인해 등록조차 되지 않음).
        ///
        /// 순회 중 Remove 안정성:
        ///   역순으로 순회하면서 만료 항목을 그 자리에서 RemoveAt 하므로,
        ///   앞쪽 인덱스가 뒤로 밀릴 일이 없어 누락/중복 없이 안전하게 처리 가능.
        /// </summary>
        /// <param name="dt">경과 시간 (초). 일반적으로 Time.deltaTime을 전달.</param>
        public void TickPendingHits(float dt)
        {
            // 역순 순회: 중간에 원소를 제거해도 남은 인덱스가 흔들리지 않음.
            for (int i = _pendingHits.Count - 1; i >= 0; i--)
            {
                PendingHit hit = _pendingHits[i];
                hit.RemainingDelay -= dt;

                if (hit.RemainingDelay > 0f)
                {
                    // 아직 타이머가 남음 → 감소된 값을 다시 저장(struct는 값 복사이므로 재할당 필수).
                    _pendingHits[i] = hit;
                    continue;
                }

                // 타이머 만료 → 실제 데미지 적용 후 리스트에서 제거.
                // 쿨다운 리셋은 TryAttack()에서 이미 수행됐으므로 여기서는 하지 않음
                // (ApplyAttackDamage 내부의 쿨다운 리셋 로직도 아래에서 제거됨).
                //
                // 공격자 생존/타겟 생존/타겟 재탐색(Id 기반)은 ApplyAttackDamage 내부에서 처리.
                ApplyAttackDamage(hit.Attacker, hit.TargetId, hit.TargetIsUnit);
                _pendingHits.RemoveAt(i);
            }
        }

        /// <summary>
        /// 싱글플레이 전용: 유닛의 전투 상태를 해제하고 OnCombatStopped 이벤트를 발행.
        /// MoveAlongPath에서 HasEnemyInRange가 false가 되어 전투 루프를 탈출할 때 호출.
        /// 멀티플레이에서는 NetworkCombatController가 TickCombat에서 StopCombatClientRpc를 전송하므로 불필요.
        /// </summary>
        /// <param name="unitId">전투를 종료하는 유닛의 Id</param>
        public void ClearCombatState(int unitId)
        {
            // Dictionary에서 제거 성공 = 실제로 전투 중이었음 → 이벤트 발행
            if (_combatTargets.Remove(unitId))
            {
                GameEvents.OnCombatStopped.OnNext(unitId);
            }
        }

        /// <summary>
        /// 사거리 내에 적이 존재하는지 판정만 수행 (데미지 없음, 네트워크 권한 체크 없음).
        /// 클라이언트 측 UnitView Lerp에서 시각적 전투 대기에 사용.
        /// 서버 권위 전투와 무관하게 적 존재 여부만 반환.
        /// </summary>
        public bool HasEnemyInRange(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return false;
            return FindFirstEnemyTarget(attacker) != null;
        }

        /// <summary>
        /// 감지 사거리(DetectRange) 내에 적이 있는지 판정.
        ///
        /// HasEnemyInRange(AttackRange 기준)와의 차이:
        ///   HasEnemyInRange   — 공격 사거리(MeleeContactDist=0.3f) 내 적만 탐지 (실제 공격 성립 조건)
        ///   HasEnemyInDetectRange — 감지 사거리(근접=0.866f) 내 적 탐지 (경로 전환 조건)
        ///
        /// 근접유닛은 DetectRange > AttackRange이므로 두 결과가 다를 수 있다.
        /// 원거리유닛은 DetectRange == AttackRange이므로 두 결과가 항상 동일.
        ///
        /// 사용처: UnitView.MoveAlongPath()에서 이동 중 매 프레임 호출.
        /// 감지 → (공격 사거리 밖이면 접근 이동) → 공격 사거리 진입 → 전투 루프 진입.
        /// </summary>
        /// <param name="attacker">감지 주체 유닛</param>
        /// <returns>감지 사거리 내에 적이 있으면 true</returns>
        public bool HasEnemyInDetectRange(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return false;
            return FindFirstEnemyInDetectRange(attacker) != null;
        }

        /// <summary>
        /// 감지 사거리(DetectRange) 내 가장 가까운 적의 (Id, 유닛 여부)를 반환.
        ///
        /// UnitView에서 감지 후 재경로 대상을 특정하기 위해 사용.
        /// 반환된 Id로 UnitSpawnUseCase/BuildingPlacementUseCase에서 Position을 조회하여
        /// HexPathfinder로 해당 타일 근처까지 경로 재계산.
        ///
        /// FindNearestEnemy(AttackRange 기준)와의 차이는 HasEnemyInDetectRange 주석 참조.
        /// </summary>
        /// <param name="attacker">탐색 주체 유닛</param>
        /// <returns>감지 사거리 내 가장 가까운 적이 있으면 (Id, 유닛 여부), 없으면 null</returns>
        public (int id, bool isUnit)? FindNearestEnemyInDetectRange(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return null;
            IDamageable target = FindFirstEnemyInDetectRange(attacker);
            if (target == null) return null;
            return (target.Id, target is UnitData);
        }

        /// <summary>
        /// 감지 사거리(DetectRange) 내 가장 가까운 적의 HexCoord 위치를 반환.
        ///
        /// UnitView의 MoveAlongPath에서 감지 후 경로 재탐색 시 사용.
        /// FindNearestEnemyInDetectRange는 Id만 돌려주기 때문에 호출자가 Position을 다시 찾아야 하는데,
        /// UnitView는 UnitSpawnUseCase/BuildingPlacementUseCase 참조를 갖지 않으므로
        /// 이 메서드로 HexCoord를 직접 넘겨받아 RequestMove에 그대로 넣는다.
        /// </summary>
        /// <param name="attacker">탐색 주체 유닛</param>
        /// <returns>감지된 적의 HexCoord. 없으면 null.</returns>
        public HexCoord? FindNearestEnemyPositionInDetectRange(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return null;
            IDamageable target = FindFirstEnemyInDetectRange(attacker);
            if (target == null) return null;

            // target.Position(도메인 위치)은 ProcessStep 이후에만 갱신된다.
            // 적이 타일과 타일 사이(Lerp 중)에 있을 때는 이전 타일 좌표를 가리키므로
            // positionProvider를 통해 실제 transform 위치(뷰 좌표)를 취득하고,
            // FromView 역변환 → WorldToHex로 실제 위치의 타일 좌표를 구한다.
            if (_positionProvider != null)
            {
                Vector3 viewPos = Vector3.zero;

                // IDamageable이 유닛인지 건물인지에 따라 다른 provider 메서드 호출.
                if (target is UnitData unitTarget)
                    viewPos = _positionProvider.GetUnitWorldPosition(unitTarget.Id);
                else
                    viewPos = _positionProvider.GetBuildingWorldPosition(target.Id);

                if (viewPos != Vector3.zero)
                {
                    // 뷰 좌표(Red팀이면 반전 적용됨) → 도메인 월드 좌표 역변환 → HexCoord
                    Vector3 domainPos = _mapper.NormalizeToDomainPosition(viewPos);
                    return _mapper.WorldToHex(domainPos);
                }
            }

            // 폴백: positionProvider가 없거나 GameObject가 미등록 → 도메인 위치 사용
            return target.Position;
        }

        // ====================================================================
        // 부상 아군 탐색 — BloomFairy(힐러) 전용 (규칙: 적 탐색과 팀 필터 반대)
        // ⚠️ 위쪽 적 탐색 메서드들은 이 힐러 경로와 무관하게 무변경(회귀 방지).
        // ====================================================================

        /// <summary>
        /// 힐러(BloomFairy)의 힐 사거리(DetectRange) 안에 회복이 필요한 부상 아군이 있는지 판정한다.
        /// 상태머신(UnitView)이 "적 감지" 대신 이 판정으로 힐 루프 진입 여부를 결정한다.
        /// </summary>
        /// <param name="healer">힐러 유닛.</param>
        /// <returns>사거리 내에 부상 아군(Hp &lt; MaxHp)이 하나라도 있으면 true.</returns>
        public bool HasInjuredAllyInHealRange(UnitData healer)
        {
            if (healer == null || !healer.IsAlive) return false;
            return FindInjuredAllyToHeal(healer).HasValue;
        }

        /// <summary>
        /// 힐 사거리(DetectRange) 안에서 "가장 회복이 급한" 부상 아군 1명의 Id를 반환한다.
        ///
        /// 적 탐색(FindFirstEnemyInDetectRange 등)과의 차이(팀 필터를 정반대로):
        ///   - 적 탐색: `unit.Team == attacker.Team → 제외`(적만 탐색).
        ///   - 힐 탐색: `unit.Team == healer.Team`인 아군만, 그중 `Hp &lt; MaxHp`(부상)만.
        ///
        /// 규칙(설계 확정):
        ///   1) 대상 = 아군 유닛(건물 제외) + 살아있음 + 부상(Hp &lt; MaxHp).
        ///   2) 본인 포함(자가 회복 허용) — 적 탐색과 달리 self를 제외하지 않는다.
        ///   3) 우선순위 = 잃은 체력 비율((MaxHp-Hp)/MaxHp)이 최대인 대상.
        ///   4) 동률이면 거리(월드 XZ)가 가까운 대상.
        ///
        /// 사거리 판정 소스는 적 감지와 동일(IEntityPositionProvider 월드 좌표, DetectRange×TileHeight).
        /// </summary>
        /// <param name="healer">힐러 유닛(본인도 후보에 포함).</param>
        /// <returns>회복할 부상 아군의 Id. 없으면 null.</returns>
        public int? FindInjuredAllyToHeal(UnitData healer)
        {
            if (healer == null || !healer.IsAlive) return null;

            // 힐러의 월드 좌표. 미등록(Vector3.zero)이면 도메인 좌표로 폴백해 거리 판정에 사용.
            Vector3 healerPos = _positionProvider != null
                ? _positionProvider.GetUnitWorldPosition(healer.Id)
                : Vector3.zero;
            if (healerPos == Vector3.zero)
                healerPos = _mapper.HexToWorld(healer.Position);

            // 힐 사거리 = 적 감지와 동일 공식(DetectRange × TileHeight + Epsilon). 유닛 거리만 사용.
            var (healMaxDist, _) = CalculateRangeLimits(healer, isDetect: true);

            UnitData best = null;
            float bestLostRatio = -1f;   // 잃은 체력 비율(클수록 우선)
            float bestDist = float.MaxValue; // 동률 시 거리(작을수록 우선)

            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit == null || !unit.IsAlive) continue;
                if (unit.Team != healer.Team) continue;           // 아군만(적 제외)
                if (unit.Hp >= unit.MaxHp) continue;              // 부상 아군만(풀피 제외)
                // ⚠️ 본인(self) 제외하지 않음 — 자가 회복 허용(적 탐색과 반대).

                // 대상 월드 좌표(미등록이면 도메인 좌표 폴백).
                Vector3 targetPos = _positionProvider != null
                    ? _positionProvider.GetUnitWorldPosition(unit.Id)
                    : Vector3.zero;
                if (targetPos == Vector3.zero)
                    targetPos = _mapper.HexToWorld(unit.Position);

                float dist = Vector3.Distance(healerPos, targetPos);
                if (dist > healMaxDist) continue;                 // 사거리 밖

                // 잃은 체력 비율. MaxHp가 0인 비정상 데이터는 0으로 처리(방어적).
                float lostRatio = unit.MaxHp > 0
                    ? (float)(unit.MaxHp - unit.Hp) / unit.MaxHp
                    : 0f;

                // 우선순위: 잃은 비율 최대 → 동률(거의 같음)이면 거리 최소.
                // 부동소수점 동률 판정에 작은 Epsilon(0.0001)을 사용한다.
                bool better =
                    lostRatio > bestLostRatio + 0.0001f ||
                    (Mathf.Abs(lostRatio - bestLostRatio) <= 0.0001f && dist < bestDist);

                if (better)
                {
                    bestLostRatio = lostRatio;
                    bestDist = dist;
                    best = unit;
                }
            }

            return best != null ? (int?)best.Id : null;
        }

        /// <summary>
        /// 감지 사거리(DetectRange) 기준으로 가장 가까운 적 엔티티(유닛/건물)를 탐색.
        ///
        /// FindFirstEnemyTarget(AttackRange 기준)의 거리 상수만 DetectRange로 바꾼 버전.
        /// 모든 유닛: DetectRange × TileHeight를 감지 사거리로 사용 (근접/원거리 동일).
        ///
        /// 건물 감지: DetectRange × TileHeight + BuildingDetectionRadius(0.2f)
        ///   (건물 메시가 크므로 조금 더 일찍 감지, 기존 패턴 동일).
        /// </summary>
        private IDamageable FindFirstEnemyInDetectRange(UnitData attacker)
        {
            // _positionProvider가 없으면 HexCoord 기반 폴백.
            // HexCoord 폴백은 이미 threshold=1로 인접 타일까지 탐색하므로
            // DetectRange(1.0)와 일치 — 별도 폴백 메서드 불필요.
            if (_positionProvider == null)
                return FindFirstEnemyTargetByHexCoord(attacker);

            // 공격자 월드 좌표 취득
            Vector3 attackerWorldPos = _positionProvider.GetUnitWorldPosition(attacker.Id);

            // Vector3.zero = GameObject 미등록 상태 → HexCoord 폴백
            if (attackerWorldPos == Vector3.zero)
                return FindFirstEnemyTargetByHexCoord(attacker);

            // 감지 사거리 기반 판정 거리 계산 (DetectRange × TileHeight + 건물 여유 거리)
            var (unitMaxDist, buildingMaxDist) = CalculateRangeLimits(attacker, isDetect: true);

            IDamageable closestTarget = null;
            float minWorldDist = float.MaxValue;

            // 1. 모든 적 유닛을 월드 거리 기반으로 탐색
            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit.Team == attacker.Team || !unit.IsAlive) continue;

                Vector3 targetPos = _positionProvider.GetUnitWorldPosition(unit.Id);

                // 미등록 유닛은 HexCoord → 월드 좌표 변환으로 폴백
                if (targetPos == Vector3.zero)
                    targetPos = _mapper.HexToWorld(unit.Position);

                float dist = Vector3.Distance(attackerWorldPos, targetPos);

                if (dist <= unitMaxDist && dist < minWorldDist)
                {
                    minWorldDist = dist;
                    closestTarget = unit;
                }
            }

            // 2. 모든 적 건물을 월드 거리 기반으로 탐색
            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building.Team == attacker.Team || !building.IsAlive) continue;

                Vector3 targetPos = _positionProvider.GetBuildingWorldPosition(building.Id);

                if (targetPos == Vector3.zero)
                    targetPos = _mapper.HexToWorld(building.Position);

                float dist = Vector3.Distance(attackerWorldPos, targetPos);

                if (dist <= buildingMaxDist && dist < minWorldDist)
                {
                    minWorldDist = dist;
                    closestTarget = building;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// 공격자의 사거리 내에서 가장 가까운 적(유닛 또는 건물)을 탐색.
        ///
        /// _positionProvider가 있으면 월드 좌표(Vector3.Distance) 기반으로 판정.
        /// Lerp 중에도 실시간 Transform.position을 사용하므로 HexCoord 갱신 지연 문제 해결.
        /// _positionProvider가 null이면 기존 HexCoord.Distance 폴백.
        /// </summary>
        private IDamageable FindFirstEnemyTarget(UnitData attacker)
        {
            // _positionProvider가 없으면 기존 HexCoord 기반 판정으로 폴백
            if (_positionProvider == null)
                return FindFirstEnemyTargetByHexCoord(attacker);

            // 공격자 월드 좌표 취득
            Vector3 attackerWorldPos = _positionProvider.GetUnitWorldPosition(attacker.Id);

            // Vector3.zero는 GameObject 미등록 상태 → HexCoord 폴백
            if (attackerWorldPos == Vector3.zero)
                return FindFirstEnemyTargetByHexCoord(attacker);

            // 공격 사거리 기반 판정 거리 계산 (근접/원거리 + 건물 여유 거리 포함)
            var (unitMaxDist, buildingMaxDist) = CalculateRangeLimits(attacker, isDetect: false);

            IDamageable closestTarget = null;
            float minWorldDist = float.MaxValue;

            // 1. 모든 적 유닛 탐색 (월드 좌표 기반, unitMaxDist 사용)
            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit.Team == attacker.Team || !unit.IsAlive) continue;

                Vector3 targetPos = _positionProvider.GetUnitWorldPosition(unit.Id);

                // 미등록 유닛은 HexCoord → 월드 좌표 변환으로 폴백
                if (targetPos == Vector3.zero)
                    targetPos = _mapper.HexToWorld(unit.Position);

                float dist = Vector3.Distance(attackerWorldPos, targetPos);

                if (dist <= unitMaxDist && dist < minWorldDist)
                {
                    minWorldDist = dist;
                    closestTarget = unit;
                }
            }

            // 2. 모든 적 건물 탐색 (건물은 이동하지 않으므로 월드 좌표 사용 가능, buildingMaxDist 사용)
            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building.Team == attacker.Team || !building.IsAlive) continue;

                Vector3 targetPos = _positionProvider.GetBuildingWorldPosition(building.Id);

                // 미등록 건물은 HexCoord → 월드 좌표 변환으로 폴백
                if (targetPos == Vector3.zero)
                    targetPos = _mapper.HexToWorld(building.Position);

                float dist = Vector3.Distance(attackerWorldPos, targetPos);

                if (dist <= buildingMaxDist && dist < minWorldDist)
                {
                    minWorldDist = dist;
                    closestTarget = building;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// HexCoord.Distance 기반 폴백 판정.
        /// _positionProvider가 null이거나 공격자 월드 좌표를 얻을 수 없을 때 사용.
        ///
        /// HexCoord.Distance는 정수(int) 반환이므로, AttackRange가 1.0 미만(예: 0.5)인
        /// 근접 유닛은 distance <= 0.5 조건이 항상 false가 되는 버그 발생.
        /// 이를 방지하기 위해 rangeThreshold = Max(1, CeilToInt(AttackRange))로 보정.
        /// range 0.5 → threshold 1 (인접 타일까지 폴백 탐색 가능).
        /// </summary>
        private IDamageable FindFirstEnemyTargetByHexCoord(UnitData attacker)
        {
            // HexCoord.Distance는 정수이므로 float AttackRange를 정수 threshold로 변환.
            // range < 1.0인 근접 유닛도 최소 인접 타일(distance=1)까지 탐색 가능하도록 보정.
            int rangeThreshold = Mathf.Max(1, Mathf.CeilToInt(attacker.AttackRange));

            IDamageable closestTarget = null;
            int minDistance = int.MaxValue;

            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit.Team == attacker.Team || !unit.IsAlive) continue;

                int distance = HexCoord.Distance(attacker.Position, unit.Position);

                if (distance <= rangeThreshold && distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = unit;
                }
            }

            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building.Team == attacker.Team || !building.IsAlive) continue;

                int distance = HexCoord.Distance(attacker.Position, building.Position);

                if (distance <= rangeThreshold && distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = building;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// Id와 타입으로 타겟 엔티티를 탐색.
        /// ApplyAttackDamage()에서 딜레이 후 타겟을 재확인하는 데 사용.
        /// targetIsUnit이면 UnitSpawnUseCase의 Units에서, 아니면 BuildingPlacementUseCase의 Buildings에서 탐색.
        /// </summary>
        /// <param name="targetId">찾을 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛 Dictionary 탐색, false=건물 Dictionary 탐색</param>
        /// <returns>해당 Id의 엔티티. 없으면 null.</returns>
        private IDamageable FindTargetById(int targetId, bool targetIsUnit)
        {
            if (targetIsUnit)
            {
                // 유닛 Dictionary에서 탐색
                _unitSpawn.Units.TryGetValue(targetId, out UnitData unit);
                return unit;
            }
            else
            {
                // 건물 Dictionary에서 탐색
                _buildingPlacement.Buildings.TryGetValue(targetId, out BuildingData building);
                return building;
            }
        }

        /// <summary>
        /// 현재 공격 중인 타겟이 아직 유효한지 확인한다.
        /// '유효하다'는 것은 타겟이 살아있고 사거리 내에 있다는 의미다.
        ///
        /// TickCombat에서 타겟 교체 전에 호출하여,
        /// 현재 타겟이 살아있는 동안 더 가까운 새 유닛이 나타나도 타겟을 유지하도록 한다.
        /// </summary>
        /// <param name="attacker">공격하는 유닛</param>
        /// <param name="targetId">현재 타겟의 Id</param>
        /// <param name="targetIsUnit">true=유닛 타겟, false=건물 타겟</param>
        /// <returns>타겟이 살아있고 사거리 내에 있으면 true, 사망했거나 사거리를 벗어났으면 false</returns>
        public bool IsCurrentTargetStillValid(UnitData attacker, int targetId, bool targetIsUnit)
        {
            // Id로 타겟 도메인 객체 조회 — 이미 사망하여 Dictionary에서 제거됐다면 null 반환
            IDamageable target = FindTargetById(targetId, targetIsUnit);

            // 타겟이 없거나 사망했으면 유효하지 않음
            if (target == null || !target.IsAlive) return false;

            // 타겟이 살아있더라도 사거리를 벗어났으면 유효하지 않음
            return IsTargetInRange(attacker, target);
        }

        /// <summary>
        /// 공격자의 사거리 내에 타겟이 있는지 판정.
        /// ApplyAttackDamage()에서 딜레이 후 사거리 재확인에 사용.
        /// FindFirstEnemyTarget()과 동일한 거리 판정 기준 (월드 좌표 또는 HexCoord 폴백).
        /// </summary>
        /// <param name="attacker">공격하는 유닛</param>
        /// <param name="target">사거리 확인 대상</param>
        /// <returns>사거리 내에 있으면 true</returns>
        private bool IsTargetInRange(UnitData attacker, IDamageable target)
        {
            // 공격 사거리 기반 판정 거리 계산 — 타겟 타입(유닛/건물)에 따라 적합한 거리 선택.
            var (unitMaxDist, buildingMaxDist) = CalculateRangeLimits(attacker, isDetect: false);
            bool targetIsBuilding = target is BuildingData;
            float maxDist = targetIsBuilding ? buildingMaxDist : unitMaxDist;

            // _positionProvider가 있으면 월드 좌표 기반 판정 (Lerp 중에도 정확)
            if (_positionProvider != null)
            {
                Vector3 attackerWorldPos = _positionProvider.GetUnitWorldPosition(attacker.Id);

                // 공격자 월드 좌표를 얻을 수 있으면 월드 거리 판정
                if (attackerWorldPos != Vector3.zero)
                {
                    // 타겟이 유닛이면 유닛 월드 좌표, 건물이면 건물 월드 좌표 사용
                    Vector3 targetPos;
                    if (target is UnitData)
                        targetPos = _positionProvider.GetUnitWorldPosition(target.Id);
                    else
                        targetPos = _positionProvider.GetBuildingWorldPosition(target.Id);

                    // 미등록 엔티티는 HexCoord → 월드 좌표 변환으로 폴백
                    if (targetPos == Vector3.zero)
                        targetPos = _mapper.HexToWorld(target.Position);

                    return Vector3.Distance(attackerWorldPos, targetPos) <= maxDist;
                }
            }

            // 폴백: HexCoord.Distance 기반 판정.
            // HexCoord.Distance는 정수이므로 range < 1.0 근접 유닛도 인접 타일까지 탐색 가능하도록 보정.
            int fallbackThreshold = Mathf.Max(1, Mathf.CeilToInt(attacker.AttackRange));
            return HexCoord.Distance(attacker.Position, target.Position) <= fallbackThreshold;
        }

        /// <summary>
        /// 공격 실행. 주 타깃 방향으로 Facing을 갱신하고 단일 피해를 적용한 뒤,
        /// 도끼병 등 특수 공격 유닛이면 등록된 핸들러(범위 공격 등)를 이어서 실행한다.
        ///
        /// 이 메서드는 싱글/멀티 공통 수렴점이므로, 여기에 특수 공격 훅을 걸면
        /// 두 모드 모두에 동일하게 반영된다.
        /// </summary>
        private void ExecuteAttack(UnitData attacker, IDamageable target)
        {
            // 공격 방향 계산 — 주 타깃을 향하도록 Facing 갱신.
            // 특수 공격(휩쓸기)의 전방 부채꼴 판정은 "공격자 → 주 타깃" 월드 방향을 forward로
            // 쓰지만, 이 Facing 갱신도 회전 연출 등과 정합을 유지하도록 함께 갱신한다.
            // 반드시 주 타깃 피해 전에 여기서 한 번만 갱신한다(AoE 피해마다 갱신 금지).
            HexDirection attackDir = FacingDirection.FromCoords(attacker.Position, target.Position);
            attacker.Facing = attackDir;

            // 특수 공격 핸들러 조회 — 등록된 유닛(도끼병/물정령 등)만 존재, 일반 유닛은 null.
            ISpecialAttackBehavior special = _specialAttacks.TryGet(attacker.Type);

            // special-only 유닛(TorrentSpirit 파도)은 단일 대상 공격이 없다 →
            // 주 타깃 단일 피해를 "건너뛰고" 핸들러(파도)만 실행한다.
            // 그 외(일반/도끼병)는 기존과 동일하게 주 타깃 단일 피해를 먼저 적용한다(무변경).
            bool replacesPrimary = special != null && special.ReplacesPrimaryAttack;
            if (!replacesPrimary)
            {
                // 주 타깃 단일 피해 — 기존 경로. 재사용 헬퍼로 처리.
                ApplyDamageToVictim(attacker, target);
            }

            // 특수 공격 훅.
            //   - 도끼병(ReplacesPrimaryAttack=false): 주 타깃 피해 "직후" 실행 → 핸들러가 주 타깃 중복 제외.
            //   - 물정령(ReplacesPrimaryAttack=true): 주 타깃 피해를 건너뛴 뒤 파도만 실행.
            if (special != null)
            {
                var ctx = new SpecialAttackContext(
                    attacker, target, _unitSpawn.Units,
                    ApplyDamageToVictim, ResolveWorldPosition, SpawnWave,
                    _sweepReach, _sweepArcHalfAngle,
                    _waveWidth, _waveLength, _waveTravelTime, _waveHeal);
                special.Apply(ctx);
            }
        }

        /// <summary>
        /// 엔티티(유닛/건물)의 실제 월드 좌표를 반환한다. 특수 공격 핸들러(휩쓸기)의
        /// 월드 좌표 부채꼴 판정에 사용하며, SpecialAttackContext에 델리게이트로 주입된다.
        ///
        /// IsTargetInRange / FindFirstEnemyInDetectRange와 동일한 조회 규칙을 따른다:
        ///   1) _positionProvider로 실제 GameObject 위치(뷰 좌표)를 얻는다(Lerp 중에도 정확).
        ///   2) GameObject가 없어 Vector3.zero면 도메인 좌표(HexToWorld) 폴백을 쓴다.
        /// </summary>
        /// <param name="entity">월드 좌표를 구할 엔티티(유닛 또는 건물).</param>
        /// <returns>엔티티의 월드 좌표. 조회 불가 시 도메인 좌표 폴백값.</returns>
        private Vector3 ResolveWorldPosition(IDamageable entity)
        {
            if (entity == null) return Vector3.zero;

            Vector3 worldPos = Vector3.zero;
            if (_positionProvider != null)
            {
                worldPos = entity is UnitData
                    ? _positionProvider.GetUnitWorldPosition(entity.Id)
                    : _positionProvider.GetBuildingWorldPosition(entity.Id);
            }

            // 미등록(GameObject 없음)이면 도메인 좌표 → 월드 좌표 변환으로 폴백.
            if (worldPos == Vector3.zero)
                worldPos = _mapper.HexToWorld(entity.Position);

            return worldPos;
        }

        /// <summary>
        /// 단일 피해 대상 1명에게 "피해 적용 + 이벤트 발행 + 사망 처리 + 싱글 전투 상태 정리"를
        /// 한 번에 수행하는 재사용 헬퍼.
        ///
        /// 주 타깃 경로(ExecuteAttack)와 AoE 경로(SweepAttackBehavior)가 이 동일 헬퍼를 공유하여,
        /// 멀티플레이 HP 동기화(OnEntityDamaged 발행 형식)의 일관성을 보장한다.
        ///
        /// 주의: 이 헬퍼는 Facing을 갱신하지 않는다(방향은 주 타깃 기준으로 ExecuteAttack에서 1회만 갱신).
        /// </summary>
        /// <param name="attacker">공격자 유닛.</param>
        /// <param name="target">피해를 받을 대상(유닛 또는 건물).</param>
        private void ApplyDamageToVictim(UnitData attacker, IDamageable target)
            => ApplyDamageToVictim(attacker, target, immediatePresentation: false);

        /// <summary>
        /// <see cref="ApplyDamageToVictim(UnitData, IDamageable)"/>의 확장 오버로드.
        /// 피격 연출을 공격자 타격 프레임에 맞출지(false, 일반/휩쓸기) 즉시 방출할지(true, 파도)를 지정한다.
        /// 2-인자 오버로드가 SpecialAttackContext의 피해 델리게이트로 전달되므로(휩쓸기),
        /// 시그니처를 나눠 도끼병 경로는 immediatePresentation=false로 유지된다(회귀 없음).
        /// </summary>
        /// <param name="attacker">공격자 유닛.</param>
        /// <param name="target">피해를 받을 대상(유닛 또는 건물).</param>
        /// <param name="immediatePresentation">
        ///   true면 피격 표현 큐가 공격자 타격 프레임을 기다리지 않고 즉시 방출(파도 전용 경로 — 규칙 26).
        /// </param>
        private void ApplyDamageToVictim(UnitData attacker, IDamageable target, bool immediatePresentation)
        {
            if (attacker == null || target == null) return;

            // 데미지 적용 (인터페이스의 메서드 호출)
            target.TakeDamage(attacker.AttackPower);

            // 일반화된 공격 이벤트 발행
            GameEvents.OnEntityAttacked.OnNext(new EntityAttackedEvent(attacker, target));

            // 피격 이벤트 발행 — NetworkHealthSync가 구독하여 HP를 모든 클라이언트에 동기화
            // 공격자는 유닛이므로 AttackerId=attacker.Id, AttackerIsUnit=true를 함께 실어
            // 피격 표현 큐가 공격자별 큐를 구성할 수 있게 한다. (Phase 2 — 축 3)
            // 파도(immediatePresentation=true)는 공격자 타격 프레임에 종속하지 않고 즉시 방출된다.
            bool targetIsUnit = target is UnitData;
            GameEvents.OnEntityDamaged.OnNext(
                new EntityDamagedEvent(target, target.Hp, targetIsUnit,
                    attackerId: attacker.Id, attackerIsUnit: true,
                    immediatePresentation: immediatePresentation));

            // 타겟 사망 처리
            if (!target.IsAlive)
            {
                // 사망 이벤트 발행.
                // 유닛/건물 전용 이벤트 두 가지로 분리되어 있으므로 타겟 타입에 따라 분기 발행.
                //   - UnitData  → OnUnitDied(UnitDiedEvent)
                //   - BuildingData → OnBuildingDied(BuildingDiedEvent)
                // 데이터 정리(_unitSpawn.RemoveUnit / _buildingPlacement.RemoveBuilding) 직전에
                // 발행하여 구독자(UnitView/BuildingFactory/GameEndUseCase 등)가
                // 도메인 Dictionary에 아직 해당 엔티티가 존재하는 상태에서 후속 처리를 할 수 있도록 한다.
                if (target is UnitData diedUnit)
                {
                    GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(diedUnit));
                }
                else if (target is BuildingData diedBuilding)
                {
                    GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(diedBuilding));
                }

                // 싱글플레이: 사망한 유닛의 전투 상태 정리.
                // 사망한 유닛 자체가 전투 중이었다면 Dictionary에서 제거.
                // StopCombat 이벤트는 발행하지 않음 — EntityDiedClientRpc가 사망 처리를 담당.
                if (!NetworkContext.IsNetworkActive && target is UnitData deadUnit)
                {
                    _combatTargets.Remove(deadUnit.Id);
                }

                // 싱글플레이: 사망한 타겟을 공격 중이던 유닛들의 전투 상태는 제거하지 않음.
                // → 다음 TryAttack() 호출 시 새 타겟을 탐색하거나,
                //   HasEnemyInRange가 false가 되어 MoveAlongPath에서 ClearCombatState() 호출.
                // 이렇게 하면 사망 직후 즉시 StopCombat 이벤트가 발행되지 않고,
                // 자연스러운 타이밍(다음 프레임)에 전투 종료/새 타겟 전환이 발생.

                // 데이터 정리 — Dictionary에서 제거
                if (target is UnitData u)
                    _unitSpawn.RemoveUnit(u.Id);
                else if (target is BuildingData b)
                    _buildingPlacement.RemoveBuilding(b.Id);
            }
        }

        // ====================================================================
        // 힐(회복) 적용 — TorrentSpirit 파도 / 향후 BloomFairy 공용
        // ====================================================================

        /// <summary>
        /// 아군 유닛 1명에게 회복을 적용하고 OnEntityHealed 이벤트를 발행한다.
        /// 피해(ApplyDamageToVictim)와 대칭 구조의 "회복 수렴점"이며, 파도/힐 시스템이 공유한다.
        ///
        /// 멀티플레이: 서버에서 도메인 Hp를 즉시 올리고 이벤트를 발행하면, NetworkHealthSync가
        ///   절대 HP를 클라이언트에 전파(SyncHealClientRpc)하여 각 화면의 HP/치유 텍스트가 맞춰진다.
        /// </summary>
        /// <param name="healer">힐을 시전한 유닛(연출이 힐러를 알 수 있게 Id를 실어 보냄).</param>
        /// <param name="target">회복 대상 아군 유닛.</param>
        /// <param name="amount">회복량(양수).</param>
        /// <param name="showText">
        /// 부유 힐 텍스트를 띄울지 여부. 기본 true(즉발/파도 힐은 종전대로 텍스트 표시).
        /// HoT(지속 회복) 틱은 false로 호출하여 매 틱 텍스트가 뜨는 것을 막는다(HP 적용/동기화는 그대로 진행).
        /// </summary>
        private void ApplyHealToUnit(UnitData healer, UnitData target, int amount, bool showText = true)
        {
            if (target == null || amount <= 0) return;
            if (!target.IsAlive) return; // 죽은 유닛은 회복 대상 아님(Heal 내부에서도 재확인).

            target.Heal(amount);

            int healerId = healer != null ? healer.Id : -1;
            // showText=false여도 이벤트는 발행한다 — NetworkHealthSync가 이 이벤트로 HP를 클라에 동기화하기 때문.
            //   (FloatingHpTextSpawner만 ShowText 플래그를 보고 텍스트 표시를 건너뛴다.)
            GameEvents.OnEntityHealed.OnNext(
                new EntityHealedEvent(target, target.Hp, isUnit: true,
                    healerId: healerId, healerIsUnit: true,
                    showText: showText));
        }

        // ====================================================================
        // HoT/DoT 시간 지속 효과 공용 시스템 [핵심 신규]
        //   "유닛에 붙는 시간 지속 효과"를 서버 권위로 관리한다.
        //   - HoT(회복): BloomFairy가 부상 아군에 3초간 총 20HP 회복 버프를 건다.
        //   - DoT(피해): 후속 QuakeSpirit·MushroomBomber가 동일 구조를 재사용한다
        //               (이번 작업에서는 Heal만 실제 배선, Damage 분기는 구조로 수용).
        //   서버 틱 진입점: 싱글=GameBootstrapper.Update / 멀티=NetworkCombatController(IsServer).
        //   파도(TickWaves)와 동일한 소유·틱 패턴을 따른다(이중 틱 금지 — 호출부 가드).
        // ====================================================================

        /// <summary>
        /// 시간 지속 효과의 종류. Heal=지속 회복(HoT), Damage=지속 피해(DoT).
        /// 하나의 레코드/틱 로직이 두 종류를 모두 처리하도록 하여 후속 DoT 유닛이 그대로 재사용한다.
        /// </summary>
        public enum TimedEffectKind
        {
            /// <summary>지속 회복(HoT) — 매 틱 대상 Hp를 올린다. </summary>
            Heal,
            /// <summary>지속 피해(DoT) — 매 틱 대상 Hp를 깎는다(후속 유닛용). </summary>
            Damage
        }

        /// <summary>
        /// 진행 중인 시간 지속 효과 목록(대상별·종류별 1개). 서버(싱글=로컬 서버/멀티=서버)에서만
        /// 채워지고 TickTimedEffects()가 소비한다. 파도(_activeWaves)와 동일한 소유 패턴.
        /// </summary>
        private readonly List<ActiveTimedEffect> _activeTimedEffects = new List<ActiveTimedEffect>(8);

        /// <summary>
        /// BloomFairy 힐 시전 진입점. 상태머신(UnitView.EnterHealLoopV3)이 힐 타격 타이밍에 호출한다.
        /// 대상에게 "3초간 총 20HP 회복" HoT를 1회 부여(갱신 규칙 적용 — 아래 ApplyTimedEffect 참조).
        ///
        /// 서버 권위: UnitView의 이동/전투 코루틴은 서버(싱글=로컬 서버/멀티=서버)에서만 도므로
        /// 이 메서드도 서버에서만 호출된다. 클라이언트는 HP/치유 텍스트를 NetworkHealthSync로 수신한다.
        /// </summary>
        /// <param name="healer">힐을 시전한 힐러 유닛(연출/이벤트에 healerId로 실림).</param>
        /// <param name="targetId">회복 대상 아군 유닛의 Id.</param>
        public void CastHeal(UnitData healer, int targetId)
        {
            if (healer == null) return;
            if (!_unitSpawn.Units.TryGetValue(targetId, out UnitData target)) return;
            if (target == null || !target.IsAlive) return;

            // [이슈 3 수정 — 2026-07-18] 캐스트 대기(HitFrameTimes ≒ 1초) 사이에 대상이 이미
            //   풀피가 됐다면 HoT를 부여하지 않고 조용히 반환한다.
            //   왜 필요한가(초급자용 설명):
            //     힐러는 대상을 정한 뒤 힐 타격 타이밍(약 1초)까지 기다렸다가 이 CastHeal을 호출한다.
            //     그 1초 사이에 다른 힐이 대상을 MaxHp까지 채웠을 수 있는데, 그럼에도 HoT 레코드를
            //     만들면 다음 서버 틱에서 "이미 풀피" 판정으로 곧바로 제거되어 한 힐 사이클이 낭비된다.
            //     (데이터가 깨지지는 않지만 힐이 헛돌게 된다.) 여기서 미리 걸러 두면 상태머신이
            //     다음 재탐색에서 실제로 부상당한 다른 아군을 고르게 된다.
            //   (대상 사망/제거 시 무시하는 기존 동작은 위 IsAlive 가드가 그대로 담당한다.)
            if (target.Hp >= target.MaxHp) return;

            ApplyTimedEffect(healer, target, TimedEffectKind.Heal, _bloomHealAmount, _bloomHealDuration);
        }

        /// <summary>
        /// 대상 유닛에 시간 지속 효과(HoT/DoT)를 부여한다.
        ///
        /// 갱신 규칙(중첩 없음 — 설계 확정 8):
        ///   같은 대상에 같은 종류(Heal/Damage) 효과가 이미 있으면 새 레코드를 추가하지 않고
        ///   기존 레코드의 남은 시간·총량·누적량을 리셋(재부여)한다. → 대상별 동종 효과는 항상 1개.
        ///
        /// 총량 정확성(분할 오차 방지):
        ///   틱마다 "지금까지 적용됐어야 할 누적량 − 이미 적용한 량"의 차이(diff)만 적용하므로,
        ///   프레임 수·프레임레이트와 무관하게 지속시간이 끝나면 정확히 totalAmount에 도달한다.
        /// </summary>
        /// <param name="source">효과를 건 유닛(healer/attacker). 연출 attribution에 Id로 사용.</param>
        /// <param name="target">효과가 붙는 대상 유닛.</param>
        /// <param name="kind">Heal(HoT) 또는 Damage(DoT).</param>
        /// <param name="totalAmount">지속시간 동안 적용할 총량(HP, 양수).</param>
        /// <param name="duration">지속시간(초, 양수).</param>
        public void ApplyTimedEffect(UnitData source, UnitData target,
            TimedEffectKind kind, int totalAmount, float duration)
        {
            if (target == null || !target.IsAlive) return;
            if (totalAmount <= 0) return;

            int sourceId = source != null ? source.Id : -1;

            // 같은 대상 + 같은 종류 효과가 이미 있으면 리셋(중첩 방지).
            for (int i = 0; i < _activeTimedEffects.Count; i++)
            {
                ActiveTimedEffect e = _activeTimedEffects[i];
                if (e.TargetId == target.Id && e.Kind == kind)
                {
                    e.SourceId = sourceId;
                    e.TotalAmount = totalAmount;
                    e.Duration = duration;
                    e.Elapsed = 0f;
                    e.AppliedAmount = 0;
                    e.ActualHealed = 0; // 갱신(재부여) → 누적 회복량도 리셋. 다음 완료 시 이 생애의 총량만 표시.
                    return;
                }
            }

            // 신규 레코드 추가.
            _activeTimedEffects.Add(new ActiveTimedEffect
            {
                TargetId = target.Id,
                SourceId = sourceId,
                Kind = kind,
                TotalAmount = totalAmount,
                Duration = duration,
                Elapsed = 0f,
                AppliedAmount = 0,
                ActualHealed = 0
            });
        }

        /// <summary>
        /// 진행 중인 모든 시간 지속 효과(HoT/DoT)를 dt만큼 진행시키고 이번 틱 적용분을 반영한다.
        /// 완료/무효(대상 사망·풀피(HoT)·만료)된 레코드는 목록에서 제거한다.
        ///
        /// 호출 위치(서버 전용, 이중 틱 금지):
        ///   - 싱글플레이: GameBootstrapper.Update()에서 TickWaves 바로 옆에서 매 프레임 호출.
        ///   - 멀티플레이: NetworkCombatController.TickCombat()에서 서버 경과 시간으로 호출.
        /// </summary>
        /// <param name="dt">이번 서버 틱의 경과 시간(초).</param>
        public void TickTimedEffects(float dt)
        {
            if (_activeTimedEffects.Count == 0) return;

            // 역순 순회 — 만료/무효 레코드를 그 자리에서 제거해도 인덱스가 흔들리지 않음.
            for (int i = _activeTimedEffects.Count - 1; i >= 0; i--)
            {
                ActiveTimedEffect effect = _activeTimedEffects[i];

                // 대상 재확인 — 지속 도중 다른 원인으로 사망/제거됐을 수 있음.
                if (!_unitSpawn.Units.TryGetValue(effect.TargetId, out UnitData target)
                    || target == null || !target.IsAlive)
                {
                    _activeTimedEffects.RemoveAt(i);
                    continue;
                }

                // 경과 시간 진행(지속시간 상한). Duration이 비정상(0 이하)이면 즉시 완료로 처리.
                effect.Elapsed = Mathf.Min(effect.Duration, effect.Elapsed + dt);
                float ratio = effect.Duration > 0.0001f
                    ? effect.Elapsed / effect.Duration
                    : 1f;

                // "지금까지 적용됐어야 할 누적량"(반올림) − "이미 적용한 량" = 이번 틱 적용분.
                // 지속시간 종료 시 ratio=1 → 누적 목표=TotalAmount → 정확히 총량 도달.
                int cumulativeTarget = Mathf.RoundToInt(effect.TotalAmount * ratio);
                int diff = cumulativeTarget - effect.AppliedAmount;

                if (diff > 0)
                {
                    if (effect.Kind == TimedEffectKind.Heal)
                    {
                        // Heal → 기존 회복 수렴점 재사용(규칙 30). healer는 살아있으면 attribution용으로 전달.
                        _unitSpawn.Units.TryGetValue(effect.SourceId, out UnitData healer);

                        // [힐 텍스트 정리] 틱 회복은 텍스트를 억제(showText:false)한다 — HP 적용/동기화만 수행.
                        //   또한 이 효과가 "실제로 올린 HP"를 누적한다(회복이 MaxHp에서 상한에 걸리면
                        //   실제 증가분은 diff보다 작을 수 있으므로 Heal 전후 Hp 차이로 정확히 잰다).
                        int hpBefore = target.Hp;
                        ApplyHealToUnit(healer, target, diff, showText: false);
                        effect.ActualHealed += target.Hp - hpBefore;
                    }
                    else
                    {
                        // Damage(DoT) → 후속 유닛용. 임의 량 피해를 적용한다(파도와 동일하게 즉시 연출).
                        _unitSpawn.Units.TryGetValue(effect.SourceId, out UnitData attacker);
                        ApplyTimedDamageToUnit(attacker, target, diff);
                    }
                    effect.AppliedAmount = cumulativeTarget;
                }

                // 종료 판정 — 만료(총량 적용 완료) / 대상 풀피(HoT는 더 회복할 것 없음) / 대상 사망.
                bool expired = effect.Elapsed >= effect.Duration;
                bool fullyHealed = effect.Kind == TimedEffectKind.Heal && target.Hp >= target.MaxHp;
                bool targetDead = !target.IsAlive; // DoT가 대상을 죽였을 수 있음

                if (expired || fullyHealed || targetDead)
                {
                    // [힐 텍스트 정리] HoT가 "정상 종료"(만료 또는 완전 회복)되고 실제로 회복시킨 양이 있으면
                    //   그 효과의 총 회복량으로 부유 힐 텍스트를 딱 1회 표시한다.
                    //   ⚠️ 회복 도중 대상이 사망(targetDead)한 경우에는 텍스트를 생략한다(요구사항 3).
                    //   ⚠️ 위쪽 "대상 없음/사망" 조기 제거 경로(TryGetValue 실패)도 텍스트 없이 제거되므로 일관된다.
                    if (effect.Kind == TimedEffectKind.Heal && !targetDead && effect.ActualHealed > 0)
                    {
                        _unitSpawn.Units.TryGetValue(effect.SourceId, out UnitData healer);
                        PublishHealCompletionText(healer, target, effect.ActualHealed);
                    }

                    _activeTimedEffects.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 시간 지속 피해(DoT) 1틱 적용 — 후속 특수 유닛(QuakeSpirit·MushroomBomber)이 재사용할
        /// "임의 량 피해" 경로. 주 타깃 단일 피해(ApplyDamageToVictim)는 attackPower 고정이라
        /// DoT의 틱별 임의 량에는 쓸 수 없으므로, 사망 처리까지 포함한 자체 경로를 둔다.
        ///
        /// 파도 피해와 동일하게 즉시 연출(immediatePresentation=true)로 방출한다(대상별 타이밍 상이).
        /// ⚠️ 이번 BloomFairy 작업에서는 호출되지 않는다(Heal 전용) — DoT 유닛 구현 시 활성 경로가 된다.
        /// </summary>
        /// <param name="attacker">피해 원인 유닛(없으면 null — attribution만 영향).</param>
        /// <param name="target">피해 대상 유닛.</param>
        /// <param name="amount">이번 틱 피해량(양수).</param>
        private void ApplyTimedDamageToUnit(UnitData attacker, UnitData target, int amount)
        {
            if (target == null || !target.IsAlive || amount <= 0) return;

            target.TakeDamage(amount);

            int attackerId = attacker != null ? attacker.Id : -1;
            GameEvents.OnEntityDamaged.OnNext(
                new EntityDamagedEvent(target, target.Hp, isUnit: true,
                    attackerId: attackerId, attackerIsUnit: true,
                    immediatePresentation: true));

            // 사망 처리 — ApplyDamageToVictim의 유닛 사망 경로와 동일 순서(이벤트 → 상태 정리 → 제거).
            if (!target.IsAlive)
            {
                GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(target));

                if (!NetworkContext.IsNetworkActive)
                    _combatTargets.Remove(target.Id);

                _unitSpawn.RemoveUnit(target.Id);
            }
        }

        /// <summary>
        /// HoT(지속 회복)가 정상 종료될 때, 부유 힐 텍스트를 딱 1회 표시한다.
        /// 표시 형식은 즉발/파도 힐과 동일하게 "회복 후 현재 HP"(절대값)로 통일한다(사용자 결정 B).
        ///
        /// 텍스트 전용 경로(중요):
        ///   여기서는 target.Heal()을 호출하지 않는다 — HP는 이미 매 틱 ApplyHealToUnit으로 올려 두었으므로,
        ///   완료 텍스트가 HP를 다시 올리면 이중 회복이 된다. 따라서 OnEntityHealed만 ShowText=true로 발행해
        ///   현재 HP(target.Hp)를 실은 "텍스트만" 뜨게 한다.
        ///
        /// 멀티플레이 경로 일관성:
        ///   서버에서 이 이벤트를 발행하면 → (호스트 화면) FloatingHpTextSpawner가 직접 현재 HP를 그리고,
        ///   → NetworkHealthSync(서버)가 SyncHealClientRpc로 ShowText를 전파하여
        ///   클라이언트도 완료 텍스트를 1회만 그린다(HP가 이미 동기화돼 차이가 0이어도 텍스트는 표시).
        /// </summary>
        /// <param name="healer">힐을 시전한 힐러 유닛(없으면 null — attribution만 영향).</param>
        /// <param name="target">회복 대상 유닛.</param>
        /// <param name="totalHealed">이 효과가 실제로 회복시킨 총 HP(양수). 표시 여부 판정용 가드로만 쓰며, 텍스트에는 현재 HP를 표시한다.</param>
        private void PublishHealCompletionText(UnitData healer, UnitData target, int totalHealed)
        {
            if (target == null || totalHealed <= 0) return;

            int healerId = healer != null ? healer.Id : -1;
            GameEvents.OnEntityHealed.OnNext(
                new EntityHealedEvent(target, target.Hp, isUnit: true,
                    healerId: healerId, healerIsUnit: true,
                    showText: true));
        }

        /// <summary>
        /// 진행 중인 시간 지속 효과 1개의 서버 상태(데이터 전용).
        /// 대상별·종류별로 1개만 유지되며(갱신 규칙), 매 틱 Elapsed/AppliedAmount가 갱신된다.
        /// </summary>
        private sealed class ActiveTimedEffect
        {
            /// <summary> 효과가 붙은 대상 유닛 Id. </summary>
            public int TargetId;
            /// <summary> 효과를 건 원인 유닛 Id(연출 attribution용, 없으면 -1). </summary>
            public int SourceId;
            /// <summary> 효과 종류(Heal=HoT / Damage=DoT). </summary>
            public TimedEffectKind Kind;
            /// <summary> 지속시간 동안 적용할 총량(HP). </summary>
            public int TotalAmount;
            /// <summary> 지속시간(초). </summary>
            public float Duration;
            /// <summary> 부여 후 누적 경과 시간(초, 상한=Duration). </summary>
            public float Elapsed;
            /// <summary> 지금까지 실제로 적용한 누적량(HP). diff 계산 기준. </summary>
            public int AppliedAmount;
            /// <summary>
            /// 이 효과가 실제로 회복시킨 총 HP(HoT 전용). 매 틱 Heal 전후 Hp 차이를 누적한다.
            /// (MaxHp 상한 등으로 실제 증가분이 diff보다 작을 수 있어 AppliedAmount와 별개로 정확히 잰다.)
            /// 정상 종료 시 이 값이 0보다 크면 완료 텍스트(현재 HP)를 1회 표시할지 판정하는 데 쓴다(표시값은 아님). 갱신(재부여) 시 0으로 리셋.
            /// </summary>
            public int ActualHealed;
        }

        // ====================================================================
        // TorrentSpirit 파도(이동 전선) 서버 시뮬레이션
        // ====================================================================

        /// <summary>
        /// 파도(이동 전선)를 생성해 시뮬레이션 목록에 등록한다.
        /// SpecialAttackContext의 SpawnWave 델리게이트로 TorrentAttackBehavior가 호출한다.
        ///
        /// 서버 권위: 이 메서드는 데미지 판정과 동일한 수렴점(ExecuteAttack)에서만 호출되므로
        /// 싱글=로컬 서버 / 멀티=서버에서만 파도가 등록된다.
        /// </summary>
        /// <param name="req">파도 모양/방향/파라미터.</param>
        private void SpawnWave(WaveSpawnRequest req)
        {
            if (req.Caster == null) return;

            // 전선 진행 속도 = 전방 길이 / 이동 시간. travelTime이 0 이하(오설정)면 0 나눗셈을 막고 즉시 완주.
            float travelTime = req.TravelTime > 0.0001f ? req.TravelTime : 0.0001f;
            float speed = req.Length / travelTime;

            // 좌우 판정을 위한 우측 벡터(XZ 평면에서 forward에 수직). forward가 정규화돼 있으므로 크기 1.
            Vector3 right = new Vector3(req.Forward.z, 0f, -req.Forward.x);

            _activeWaves.Add(new ActiveWave
            {
                Caster = req.Caster,
                AttackerTeam = req.Caster.Team,
                CasterId = req.Caster.Id,
                Origin = req.Origin,
                Forward = req.Forward,
                Right = right,
                HalfWidth = req.Width * 0.5f,
                Length = req.Length,
                Speed = speed,
                TravelTime = travelTime,
                Heal = req.Heal,
                FrontDistance = 0f,
                Elapsed = 0f
            });
        }

        /// <summary>
        /// 진행 중인 모든 파도의 전선을 dt만큼 전진시키고, 이번 틱에 새로 닿은(아직 안 맞은) 유닛에
        /// 효과를 1회 적용한다. 완주한 파도는 목록에서 제거한다.
        ///
        /// 호출 위치(서버 전용):
        ///   - 싱글플레이: GameBootstrapper.Update()에서 매 프레임 Time.deltaTime으로 호출.
        ///   - 멀티플레이: NetworkCombatController.TickCombat()에서 서버 경과 시간으로 호출.
        ///   (클라이언트는 파도를 시뮬레이션하지 않는다 — 데미지/힐은 서버 권위, HP는 동기화로 수신.)
        ///
        /// 판정(월드 좌표 직사각형 + hit-set):
        ///   유닛의 공격자 기준 상대 벡터를 전방 성분 p / 좌우 성분 lateral로 분해하여,
        ///   0 ≤ p ≤ Length(직사각형 안) AND |lateral| ≤ HalfWidth(폭 안) AND p ≤ FrontDistance(전선이 지남)
        ///   이면 닿은 것으로 본다. 이미 맞은 유닛(hit-set)은 제외해 "각 유닛 1회"를 보장한다(D-5).
        ///   전선이 지난 유닛만 즉시 처리하므로, 파도 도중 진입/이동한 유닛도 다음 틱에 자연히 반영된다.
        /// </summary>
        /// <param name="dt">이번 서버 틱의 경과 시간(초).</param>
        public void TickWaves(float dt)
        {
            if (_activeWaves.Count == 0) return;

            // 역순 순회 — 완주한 파도를 그 자리에서 제거해도 남은 인덱스가 흔들리지 않음.
            for (int w = _activeWaves.Count - 1; w >= 0; w--)
            {
                ActiveWave wave = _activeWaves[w];

                wave.Elapsed += dt;
                // 전선 전진(전방 길이를 넘지 않도록 상한).
                wave.FrontDistance = Mathf.Min(wave.Length, wave.FrontDistance + wave.Speed * dt);

                // 1) 닿은 유닛 선수집(피해로 사망 시 Dictionary 변경 예외 방지 — 수집/적용 2단계 분리).
                _waveDamageBuffer.Clear();
                _waveHealBuffer.Clear();
                _waveBuildingBuffer.Clear();

                foreach (var unit in _unitSpawn.Units.Values)
                {
                    if (unit == null || !unit.IsAlive) continue;
                    if (unit.Id == wave.CasterId) continue;          // 시전자 자신 제외(D-7)
                    if (wave.Hit.Contains(unit.Id)) continue;        // 이미 맞음 → 제외(D-5)

                    // 공격자 기준 상대 위치를 XZ 평면에서 전방/좌우 성분으로 분해.
                    Vector3 rel = Flatten(ResolveWorldPosition(unit)) - wave.Origin;
                    float p = Vector3.Dot(rel, wave.Forward);        // 전방 성분(진행 방향 거리)
                    if (p < 0f || p > wave.Length) continue;         // 직사각형 전후 범위 밖
                    if (p > wave.FrontDistance) continue;            // 아직 전선이 도달하지 않음(다음 틱)

                    float lateral = Vector3.Dot(rel, wave.Right);    // 좌우 성분
                    if (Mathf.Abs(lateral) > wave.HalfWidth) continue; // 폭 밖

                    // 닿음 확정 → 1회만 적용되도록 hit-set에 기록하고 팀에 따라 버퍼 분류.
                    wave.Hit.Add(unit.Id);
                    if (unit.Team == wave.AttackerTeam)
                        _waveHealBuffer.Add(unit);   // 아군 → 힐
                    else
                        _waveDamageBuffer.Add(unit); // 적 → 피해
                }

                // 적 건물도 파도 경로에 들어오면 피해 대상(BUG-002 — special-only 공성 가능).
                // 건물은 정적이라 유닛과 동일한 직사각형/전선 판정을 적용한다. 단 힐 대상은 아니다(적 건물만 피해).
                foreach (var building in _buildingPlacement.Buildings.Values)
                {
                    if (building == null || !building.IsAlive) continue;
                    if (building.Team == wave.AttackerTeam) continue;        // 아군 건물 제외(적 건물만)
                    if (wave.HitBuildings.Contains(building.Id)) continue;   // 이미 맞음 → 제외(각 건물 1회)

                    Vector3 relB = Flatten(ResolveWorldPosition(building)) - wave.Origin;
                    float pB = Vector3.Dot(relB, wave.Forward);             // 전방 성분
                    if (pB < 0f || pB > wave.Length) continue;              // 직사각형 전후 범위 밖
                    if (pB > wave.FrontDistance) continue;                  // 아직 전선이 도달하지 않음(다음 틱)

                    float lateralB = Vector3.Dot(relB, wave.Right);         // 좌우 성분
                    if (Mathf.Abs(lateralB) > wave.HalfWidth) continue;     // 폭 밖

                    wave.HitBuildings.Add(building.Id);
                    _waveBuildingBuffer.Add(building);
                }

                // 2) 적용 — 피해는 파도 전용 즉시 연출(immediatePresentation=true)로 방출.
                //    공격자(시전자)가 파도 도중 사망했을 수 있으나, AttackPower/Id는 유효하므로 그대로 사용.
                for (int i = 0; i < _waveDamageBuffer.Count; i++)
                {
                    UnitData victim = _waveDamageBuffer[i];
                    if (victim == null || !victim.IsAlive) continue;
                    ApplyDamageToVictim(wave.Caster, victim, immediatePresentation: true);
                }
                for (int i = 0; i < _waveHealBuffer.Count; i++)
                {
                    UnitData ally = _waveHealBuffer[i];
                    if (ally == null || !ally.IsAlive) continue;
                    ApplyHealToUnit(wave.Caster, ally, wave.Heal);
                }
                // 적 건물 피해 — 유닛 피해와 동일하게 즉시 연출(immediatePresentation=true)로 방출.
                for (int i = 0; i < _waveBuildingBuffer.Count; i++)
                {
                    BuildingData bVictim = _waveBuildingBuffer[i];
                    if (bVictim == null || !bVictim.IsAlive) continue;
                    ApplyDamageToVictim(wave.Caster, bVictim, immediatePresentation: true);
                }

                // 3) 완주 판정 — 전선이 끝까지 갔고 이동 시간도 지났으면 제거.
                //    (FrontDistance가 Length에 도달하면 p ≤ Length 유닛은 모두 커버됨.)
                if (wave.FrontDistance >= wave.Length && wave.Elapsed >= wave.TravelTime)
                {
                    _activeWaves.RemoveAt(w);
                }
            }
        }

        /// <summary>
        /// 벡터의 Y 성분을 0으로 눌러 XZ 평면 벡터로 만든다(높이 무시). 파도 판정 공용.
        /// </summary>
        private static Vector3 Flatten(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        /// <summary>
        /// 진행 중인 파도 1개의 서버 상태(데이터 전용).
        /// 전선 위치(FrontDistance)와 이미 맞은 유닛 집합(Hit)을 들고 매 틱 갱신된다.
        /// </summary>
        private sealed class ActiveWave
        {
            /// <summary> 시전자(피해 공격자/힐 시전자). 사망해도 AttackPower/Id는 유효. </summary>
            public UnitData Caster;
            /// <summary> 시전자 팀(적/아군 구분 기준). </summary>
            public TeamId AttackerTeam;
            /// <summary> 시전자 Id(시전자 자신 제외 판정). </summary>
            public int CasterId;
            /// <summary> 전선 시작 지점(월드 XZ). </summary>
            public Vector3 Origin;
            /// <summary> 진행 방향(정규화된 XZ). </summary>
            public Vector3 Forward;
            /// <summary> 좌우 판정용 우측 벡터(정규화된 XZ, Forward에 수직). </summary>
            public Vector3 Right;
            /// <summary> 폭의 절반(좌우 각각 허용 거리). </summary>
            public float HalfWidth;
            /// <summary> 전방 길이. </summary>
            public float Length;
            /// <summary> 전선 진행 속도(Length / TravelTime). </summary>
            public float Speed;
            /// <summary> 이동 시간(초). </summary>
            public float TravelTime;
            /// <summary> 아군 회복량. </summary>
            public int Heal;
            /// <summary> 현재 전선이 시작 지점에서 나아간 거리. </summary>
            public float FrontDistance;
            /// <summary> 생성 후 누적 경과 시간(초). </summary>
            public float Elapsed;
            /// <summary> 이미 효과를 받은 유닛 Id 집합(중복 방지 — 각 유닛 1회). </summary>
            public readonly HashSet<int> Hit = new HashSet<int>();
            /// <summary>
            /// 이미 피해를 받은 "건물" Id 집합(중복 방지 — 각 건물 1회).
            /// 유닛 Id와 건물 Id는 서로 다른 카운터라 값이 겹칠 수 있으므로 유닛 Hit과 분리한 집합을 쓴다.
            /// </summary>
            public readonly HashSet<int> HitBuildings = new HashSet<int>();
        }
    }
}
