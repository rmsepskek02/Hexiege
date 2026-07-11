// ============================================================================
// HitPresentationQueue.cs
// 피격 "표현"(HP 텍스트·피격 VFX·타격 반응)을 공격자의 로컬 타격 프레임에 맞춰 방출하는 큐.
//
// 배경 (전투 타격 타이밍 동기화 Phase 2 — 축 3):
//   데미지와 HP는 서버 권위로 즉시 갱신된다(도메인 데이터). 하지만 "맞는 화면"의 연출은
//   공격자가 실제로 칼을 휘두르는 순간(로컬 애니메이션의 타격 프레임 = OnAttackHit)에 맞춰
//   터뜨려야 자연스럽다. 네트워크 지연/틱 격자 때문에 데미지 이벤트가 먼저 도착할 수 있으므로,
//   이 큐가 피격 표현을 "잠깐 보류"했다가 공격자의 로컬 타격 신호가 오면 방출한다.
//
// 동작 요약:
//   1) GameEvents.OnEntityDamaged 수신 → 표현 정보를 "공격자별 FIFO 큐"에 보류.
//   2) GameEvents.OnLocalAttackHit(공격자 Id) 수신 → 해당 공격자 큐에서 1건 방출.
//   3) 방출 시 ① HP 텍스트 ② 피격 VFX ③ 타격 반응(스케일 펀치)을 동시에 실행.
//
// 안전망(연출 유실 방지):
//   ⓐ 타임아웃 — 등록 후 공격자 AttackCooldown 1회분(조회 실패 시 3초)이 지나도 방출 신호가
//      오지 않으면 즉시 방출한다.
//   ⓑ 타겟 사망 — OnUnitDied/OnBuildingDied 수신 시 그 타겟을 겨눈 잔여 항목을 즉시 방출한다
//      (사망 연출 전 마지막 피격 표시 보장).
//   ⓒ 즉시 방출 — 공격자가 유닛이 아니거나(타워: 타격 프레임 없음) 공격자 GameObject가 없으면
//      보류하지 않고 곧바로 방출한다.
//
// 싱글플레이:
//   서버=로컬이라 데미지 이벤트와 로컬 OnAttackHit이 거의 동시에 발생 → 보류가 매우 짧거나
//   즉시 방출된다. 순서 역전(OnAttackHit이 이벤트보다 먼저 도착) 시에는 빈 신호를 버린다(아래 주석 참조).
//
// 생성/초기화:
//   씬에 수동 배치하지 않는다. GameBootstrapper(조합 루트)가 자신의 GameObject에 AddComponent 후
//   Initialize(...)로 의존성을 주입한다.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour). Application 이벤트/UseCase, Infrastructure 팩토리 참조.
// ============================================================================

using System.Collections.Generic;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 피격 표현(HP 텍스트·VFX·타격 반응)을 공격자의 로컬 타격 프레임에 맞춰 방출하는 큐.
    /// 도메인 HP는 이미 갱신된 상태이며, 이 큐는 오직 "표현"만 담당한다(서버 권위 불변).
    /// </summary>
    public class HitPresentationQueue : MonoBehaviour
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>
        /// 공격자 AttackCooldown 조회 실패 시 사용하는 타임아웃 폴백(초).
        /// 이 시간이 지나도 로컬 타격 신호가 오지 않으면 연출을 강제로 방출한다.
        /// </summary>
        private const float FallbackTimeout = 3f;

        // ====================================================================
        // 의존성 (Initialize()에서 주입)
        // ====================================================================

        /// <summary> HP 텍스트 표시를 위임할 스포너. 방출 시점에 ShowDamage()를 호출. </summary>
        private FloatingHpTextSpawner _hpTextSpawner;

        /// <summary> 유닛 GameObject 조회용(VFX 위치 + 스케일 펀치 대상). </summary>
        private UnitFactory _unitFactory;

        /// <summary> 건물 GameObject 조회용(스케일 펀치 대상 + 타워 발사 VFX 위치). </summary>
        private BuildingFactory _buildingFactory;

        /// <summary> 공격자 AttackCooldown 조회용(타임아웃 계산). </summary>
        private UnitSpawnUseCase _unitSpawn;

        /// <summary>
        /// 건물 데이터 조회용(타워 발사 VFX의 BuildingType 해석).
        /// 타워가 공격할 때 어떤 타입의 발사 이펙트를 재생할지 결정하는 데 쓴다.
        /// </summary>
        private BuildingPlacementUseCase _buildingPlacement;

        // ====================================================================
        // 보류 큐 상태
        // ====================================================================

        /// <summary>
        /// 공격자 Id → 그 공격자가 발생시킨 피격 표현들의 FIFO 큐.
        /// 공격자의 로컬 OnAttackHit 신호마다 앞에서 1건씩 방출한다.
        /// </summary>
        private readonly Dictionary<int, Queue<PendingHit>> _pendingByAttacker
            = new Dictionary<int, Queue<PendingHit>>();

        // ====================================================================
        // 초기화 — GameBootstrapper에서 호출
        // ====================================================================

        /// <summary>
        /// 큐 초기화. 의존성 저장 + 이벤트 구독.
        /// </summary>
        /// <param name="hpTextSpawner">HP 텍스트 스포너(표시 위임).</param>
        /// <param name="unitFactory">유닛 GameObject 조회 팩토리.</param>
        /// <param name="buildingFactory">건물 GameObject 조회 팩토리.</param>
        /// <param name="unitSpawn">공격자 쿨다운 조회용 UseCase.</param>
        /// <param name="buildingPlacement">타워 발사 VFX의 BuildingType 해석용 UseCase.</param>
        public void Initialize(
            FloatingHpTextSpawner hpTextSpawner,
            UnitFactory unitFactory,
            BuildingFactory buildingFactory,
            UnitSpawnUseCase unitSpawn,
            BuildingPlacementUseCase buildingPlacement)
        {
            _hpTextSpawner = hpTextSpawner;
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
            _unitSpawn = unitSpawn;
            _buildingPlacement = buildingPlacement;

            // 재초기화(맵 재로드) 대비 — 이전 보류 항목 정리.
            _pendingByAttacker.Clear();

            // 피격 이벤트 → 보류 큐 적재.
            // AddTo(this): 이 MonoBehaviour 파괴 시 자동 구독 해제.
            GameEvents.OnEntityDamaged
                .Subscribe(OnEntityDamaged)
                .AddTo(this);

            // 공격자의 로컬 타격 프레임 신호 → 해당 공격자 큐에서 1건 방출.
            GameEvents.OnLocalAttackHit
                .Subscribe(OnLocalAttackHit)
                .AddTo(this);

            // 타겟 사망 → 그 타겟을 겨눈 잔여 항목 즉시 방출(안전망 ⓑ).
            GameEvents.OnUnitDied
                .Subscribe(OnUnitDied)
                .AddTo(this);
            GameEvents.OnBuildingDied
                .Subscribe(OnBuildingDied)
                .AddTo(this);
        }

        // ====================================================================
        // 이벤트 핸들러 — 적재
        // ====================================================================

        /// <summary>
        /// 피격 이벤트 수신. 공격자별 큐에 보류하거나, 조건에 따라 즉시 방출한다.
        /// </summary>
        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (evt.Entity == null) return;

            // ⓒ 공격자가 유닛이 아니면(타워) 타격 애니메이션 프레임이 없다 → 보류 없이 즉시 방출.
            if (!evt.AttackerIsUnit)
            {
                // 타워 발사 연출(3-1): 타워가 즉발로 데미지를 주는 순간, 각 클라이언트 로컬에서
                //   발사 VFX(총구 화염 등)를 재생한다. 데미지 흐름은 그대로 두고 연출만 얹는다.
                //   (이 OnEntityDamaged는 호스트=UseCase 발행 1회, 클라=NetworkHealthSync 재발행 1회로
                //    각 머신에서 정확히 1번씩만 도달하므로 이중 재생이 없다.)
                PlayTowerAttackVfx(evt);

                LogEmit(evt, "타워즉시", 0f); // [TIMING-LOG]
                Emit(evt);
                return;
            }

            // ⓒ 공격자 GameObject가 없으면(스폰 전/파괴 등) 로컬 OnAttackHit 신호를 기대할 수 없다 → 즉시 방출.
            GameObject attackerGo = _unitFactory != null ? _unitFactory.GetUnitObject(evt.AttackerId) : null;
            if (attackerGo == null)
            {
                LogEmit(evt, "즉시(공격자GO없음)", 0f); // [TIMING-LOG]
                Emit(evt);
                return;
            }

            // 정상 경로: 공격자 큐에 보류. 공격자의 로컬 OnAttackHit에서 방출된다.
            float timeout = ResolveTimeout(evt.AttackerId);
            Queue<PendingHit> queue = GetOrCreateQueue(evt.AttackerId);
            queue.Enqueue(new PendingHit(evt, Time.time, timeout));

            // [TIMING-LOG] 보류 등록 계측 — 검증 완료 후 제거([TIMING-LOG] 마커 일괄 삭제)
            if (CombatTimingLog.Enabled)
                CombatTimingLog.Info("Combat/HitPresentationQueue",
                    $"보류 등록 | attacker={evt.AttackerId}, target={evt.Entity.Id}, hp={evt.CurrentHp}");
        }

        /// <summary>
        /// 공격자의 로컬 타격 프레임 신호 수신. 해당 공격자 큐에서 가장 오래된 1건을 방출한다.
        /// </summary>
        private void OnLocalAttackHit(int attackerId)
        {
            if (_pendingByAttacker.TryGetValue(attackerId, out Queue<PendingHit> queue) && queue.Count > 0)
            {
                // [TIMING-LOG] 대기시간 계측을 위해 항목을 지역 변수로 받는다(동작 동일: 1건 dequeue 후 방출).
                PendingHit dequeued = queue.Dequeue();
                LogEmit(dequeued.Event, "타격프레임", (Time.time - dequeued.EnqueueTime) * 1000f); // [TIMING-LOG]
                Emit(dequeued.Event);
                return;
            }

            // 큐가 비어 있는 경우: 방출 신호가 데미지 이벤트보다 먼저 도착(순서 역전)했거나,
            // 이미 모두 방출된 상태다.
            // [트레이드오프] 이 "빈 신호"를 저장해 두었다가 다음에 도착할 데미지 이벤트를 당겨 쓰면
            //   다음 사이클 연출이 한 박자 빨라지는 부작용이 생긴다. 그래서 빈 신호는 그냥 버린다.
            //   그 결과 순서 역전으로 도착한 데미지 이벤트는 이번 타격 신호를 놓치지만,
            //   다음 타격 신호 또는 타임아웃 안전망(ⓐ)이 반드시 방출을 보장하므로 연출이 유실되지 않는다.
        }

        // ====================================================================
        // 이벤트 핸들러 — 사망 시 즉시 방출 (안전망 ⓑ)
        // ====================================================================

        /// <summary> 유닛 사망 시 그 유닛을 겨눈 잔여 항목을 즉시 방출. </summary>
        private void OnUnitDied(UnitDiedEvent e)
        {
            if (e.Unit == null) return;
            FlushTarget(e.Unit.Id, targetIsUnit: true);
        }

        /// <summary> 건물 사망 시 그 건물을 겨눈 잔여 항목을 즉시 방출. </summary>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            if (e.Building == null) return;
            FlushTarget(e.Building.Id, targetIsUnit: false);
        }

        // ====================================================================
        // 타임아웃 방출 (안전망 ⓐ)
        // ====================================================================

        /// <summary>
        /// 매 프레임 각 공격자 큐의 앞쪽(가장 오래된)부터 타임아웃을 검사하여 강제 방출한다.
        /// </summary>
        private void Update()
        {
            if (_pendingByAttacker.Count == 0) return;

            float now = Time.time;

            // Dictionary의 값(큐) 내부만 변경하므로 순회 중 구조 변경 문제 없음(키 추가/삭제 안 함).
            foreach (KeyValuePair<int, Queue<PendingHit>> kv in _pendingByAttacker)
            {
                Queue<PendingHit> queue = kv.Value;
                // FIFO라 앞쪽이 가장 오래된 항목 → 앞에서부터 만료 검사.
                while (queue.Count > 0)
                {
                    PendingHit head = queue.Peek();
                    if (now - head.EnqueueTime < head.Timeout)
                        break; // 앞이 아직 안 지났으면 뒤도 안 지났음 → 이 큐는 종료.

                    queue.Dequeue();
                    LogEmit(head.Event, "타임아웃", (now - head.EnqueueTime) * 1000f); // [TIMING-LOG]
                    Emit(head.Event);
                }
            }
        }

        // ====================================================================
        // 방출 — 실제 표현 재생
        // ====================================================================

        /// <summary>
        /// [TIMING-LOG] 연출 방출 계측 헬퍼 — 검증 완료 후 제거([TIMING-LOG] 마커 일괄 삭제).
        /// 방출 경로별 사유와 큐 대기시간(ms)을 기록한다.
        /// 타임아웃 방출은 발생 자체가 이상 신호이므로 WARN, 그 외 경로는 INFO로 남긴다.
        /// (원거리 유닛의 트레이서 지연 방출도 큐 입장에선 "타격프레임" 사유로 찍힌다 — 로거 주석 참조.)
        /// </summary>
        /// <param name="evt">방출되는 피격 이벤트.</param>
        /// <param name="reason">방출 사유(타격프레임/타임아웃/사망방출/타워즉시/즉시(공격자GO없음)).</param>
        /// <param name="waitMs">큐 등록~방출까지 대기 시간(ms). 즉시 방출 경로는 0.</param>
        private void LogEmit(EntityDamagedEvent evt, string reason, float waitMs)
        {
            if (!CombatTimingLog.Enabled) return;
            int targetId = evt.Entity != null ? evt.Entity.Id : -1;
            string msg = $"연출 방출 | attacker={evt.AttackerId}, target={targetId}, 사유={reason}, 대기={waitMs:0}ms";
            if (reason == "타임아웃")
                CombatTimingLog.Warn("Combat/HitPresentationQueue", msg);
            else
                CombatTimingLog.Info("Combat/HitPresentationQueue", msg);
        }

        /// <summary>
        /// 피격 표현을 실제로 재생한다: ① HP 텍스트 ② 피격 VFX ③ 타격 반응(스케일 펀치).
        /// </summary>
        private void Emit(EntityDamagedEvent evt)
        {
            if (evt.Entity == null) return;

            // ① HP 텍스트 — 스포너에 위임(풀/팀 색상 로직 재사용). 표시의 유일한 진입점.
            _hpTextSpawner?.ShowDamage(evt);

            // 타겟 GameObject 조회 (VFX 위치 + 스케일 펀치 대상). 파괴되었으면 null.
            GameObject targetGo = evt.IsUnit
                ? (_unitFactory != null ? _unitFactory.GetUnitObject(evt.Entity.Id) : null)
                : (_buildingFactory != null ? _buildingFactory.GetBuildingObject(evt.Entity.Id) : null);

            // ② 피격 VFX — 유닛 타겟만 재생(PlayUnitHit은 UnitType 기반). 프리셋 미설정이면 내부에서 조용히 스킵.
            //    (건물 피격 VFX는 이번 범위 아님 — Phase 2는 유닛 피격 VFX만 다룬다.)
            if (evt.IsUnit && targetGo != null && evt.Entity is UnitData targetUnit)
            {
                EffectManager.Instance?.PlayUnitHit(targetUnit.Type, targetGo.transform.position);
            }

            // ③ 타격 반응 — 유닛/건물 GameObject에 짧은 스케일 펀치(원 스케일 캐시 후 복원 보장).
            if (targetGo != null)
            {
                HitReactionPunch.Play(targetGo);
            }
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 특정 타겟을 겨눈 모든 보류 항목을 즉시 방출한다. FIFO 순서를 보존하며 비매칭 항목은 재삽입.
        /// </summary>
        private void FlushTarget(int targetId, bool targetIsUnit)
        {
            foreach (KeyValuePair<int, Queue<PendingHit>> kv in _pendingByAttacker)
            {
                Queue<PendingHit> queue = kv.Value;
                int count = queue.Count;
                if (count == 0) continue;

                // 큐를 한 바퀴 돌리면서 매칭 항목은 방출, 비매칭 항목은 뒤로 재삽입 → 순서 유지.
                for (int i = 0; i < count; i++)
                {
                    PendingHit item = queue.Dequeue();
                    bool match = item.Event.IsUnit == targetIsUnit
                                 && item.Event.Entity != null
                                 && item.Event.Entity.Id == targetId;
                    if (match)
                    {
                        LogEmit(item.Event, "사망방출", (Time.time - item.EnqueueTime) * 1000f); // [TIMING-LOG]
                        Emit(item.Event);   // 사망 연출 전 마지막 피격 표시 보장.
                    }
                    else
                        queue.Enqueue(item);
                }
            }
        }

        /// <summary>
        /// 타워(건물) 공격 시 발사 VFX를 재생한다(3-1). 타워 위치에서, 타겟 방향을 바라보게 재생한다.
        ///
        /// 재생 위치: 타워 GameObject 위치(BuildingFactory 조회). 없으면 재생을 스킵한다.
        /// 재생 회전: 타워 → 타겟 방향(XZ 평면 기준 LookRotation). 타겟 GameObject가 없으면 회전 없음(identity).
        /// BuildingType: BuildingPlacementUseCase에서 해석. 해석 실패 시 스킵(어떤 발사 프리셋을 쓸지 알 수 없음).
        /// </summary>
        /// <param name="evt">피격 이벤트(AttackerId=타워 Id, Entity=피격 유닛).</param>
        private void PlayTowerAttackVfx(EntityDamagedEvent evt)
        {
            // 타워 GameObject 조회 — 없으면(스폰 전/파괴 등) 발사 VFX를 스킵한다.
            GameObject towerGo = _buildingFactory != null ? _buildingFactory.GetBuildingObject(evt.AttackerId) : null;
            if (towerGo == null) return;

            // 타워 타입 해석 — 어떤 발사 프리셋을 쓸지 결정하는 데 필요. 해석 실패 시 스킵.
            BuildingData tower = _buildingPlacement != null ? _buildingPlacement.GetBuilding(evt.AttackerId) : null;
            if (tower == null) return;

            Vector3 towerPos = towerGo.transform.position;

            // 회전 = 타워 → 타겟 방향(XZ 평면). 타겟 GameObject가 있으면 그 방향을 바라보게,
            //   없으면 회전 없이(identity) 재생한다.
            Quaternion rot = Quaternion.identity;
            GameObject targetGo = evt.Entity != null && evt.IsUnit && _unitFactory != null
                ? _unitFactory.GetUnitObject(evt.Entity.Id)
                : null;
            if (targetGo != null)
            {
                Vector3 dir = targetGo.transform.position - towerPos;
                dir.y = 0f; // XZ 평면 기준 — 높이 차이는 발사 방향에서 제외(수평 조준).
                if (dir.sqrMagnitude > 0.0001f)
                    rot = Quaternion.LookRotation(dir);
            }

            EffectManager.Instance?.PlayBuildingAttack(tower.Type, towerPos, rot);
        }

        /// <summary>
        /// 공격자 AttackCooldown 1회분을 타임아웃으로 반환. 조회 실패 시 상수 폴백(3초).
        /// </summary>
        private float ResolveTimeout(int attackerId)
        {
            if (_unitSpawn != null)
            {
                UnitData attacker = _unitSpawn.GetUnit(attackerId);
                if (attacker != null && attacker.AttackCooldown > 0f)
                    return attacker.AttackCooldown;
            }
            return FallbackTimeout;
        }

        /// <summary> 공격자 Id의 큐를 가져오거나 없으면 새로 만든다. </summary>
        private Queue<PendingHit> GetOrCreateQueue(int attackerId)
        {
            if (!_pendingByAttacker.TryGetValue(attackerId, out Queue<PendingHit> queue))
            {
                queue = new Queue<PendingHit>();
                _pendingByAttacker.Add(attackerId, queue);
            }
            return queue;
        }

        // ====================================================================
        // 보류 항목 데이터
        // ====================================================================

        /// <summary>
        /// 보류 중인 피격 표현 1건. 원본 피격 이벤트 + 등록 시각 + 타임아웃.
        /// </summary>
        private readonly struct PendingHit
        {
            /// <summary> 원본 피격 이벤트(피격 대상·HP·팀·공격자 정보 포함). </summary>
            public readonly EntityDamagedEvent Event;

            /// <summary> 큐에 등록된 시각(Time.time). 타임아웃 계산 기준. </summary>
            public readonly float EnqueueTime;

            /// <summary> 이 항목의 타임아웃(초). 등록 후 이 시간이 지나면 강제 방출. </summary>
            public readonly float Timeout;

            public PendingHit(EntityDamagedEvent evt, float enqueueTime, float timeout)
            {
                Event = evt;
                EnqueueTime = enqueueTime;
                Timeout = timeout;
            }
        }
    }
}
