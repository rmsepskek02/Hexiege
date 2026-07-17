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
//   ⓐ 타임아웃 — 등록 후 공격자 AttackCooldown의 1.5배(조회 실패 시 3초)가 지나도 방출 신호가
//      오지 않으면 즉시 방출한다. 1.5배인 이유는 TimeoutCooldownMultiplier 상수 주석 참조
//      (1.0배면 "다음 타격 이벤트"와 동률 경주가 되어 표시가 모션과 어긋날 수 있음).
//   ⓑ 타겟 사망 — OnUnitDied/OnBuildingDied 수신 시 그 타겟을 겨눈 잔여 항목을 즉시 방출한다
//      (사망 연출 전 마지막 피격 표시 보장).
//   ⓒ 즉시 방출 — 공격자가 유닛이 아니거나(타워: 타격 프레임 없음) 공격자 GameObject가 없으면
//      보류하지 않고 곧바로 방출한다.
//   ⓓ 공격자 소멸 — 공격자가 죽거나(OnUnitDied) 전투를 중단하면(OnCombat/OnNetworkCombatStopped)
//      그 공격자는 다음 타격 프레임(OnLocalAttackHit)을 영영 보내지 않는다. 그 공격자 큐에 남은
//      항목을 즉시 방출하여 타임아웃(쿨다운의 1.5배)까지 HP 텍스트가 지연되는 것을 막는다.
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
        /// 타임아웃 임계값 배율 — 공격자 AttackCooldown에 곱해 "이 항목을 강제 방출할 시점"을 정한다.
        ///
        /// 왜 1.0배가 아니라 1.5배인가? (초급자 설명)
        ///   보류된 항목은 원래 "공격자의 다음 타격 프레임(OnLocalAttackHit)"이 오면 방출된다.
        ///   그런데 네트워크 지연/틱 격자 때문에 "데미지 이벤트"와 "타격 프레임 신호"의 도착 순서가
        ///   뒤바뀔 수 있다(순서 역전). 순서가 역전되면 이번 사이클의 타격 신호는 빈 큐에 도착해 버려지고
        ///   (OnLocalAttackHit 주석 참조), 이 항목은 "다음 사이클의 타격 프레임"을 기다리게 된다.
        ///   그 다음 타격 프레임은 공격자 쿨다운(예: Assault 0.333초)이 지난 뒤에 도착한다.
        ///
        ///   만약 타임아웃을 쿨다운 × 1.0으로 두면:
        ///     · "다음 타격 이벤트(쿨다운 뒤 도착)"와 "타임아웃(= 쿨다운)"이 정확히 같은 시점에 걸린다.
        ///     · 둘 중 누가 먼저냐가 프레임 순서에 좌우되는 동전 던지기가 된다.
        ///     · 타임아웃이 이기면 HP 표시가 타격 모션과 무관한 순간에 튀어나온다(어색함).
        ///
        ///   1.5배로 두면:
        ///     · "다음 타격 이벤트"는 쿨다운(1.0배) 시점에 도착하므로 타임아웃(1.5배)보다 항상 먼저 온다.
        ///     · 따라서 표시는 반드시 타격 모션에 붙어서 방출되고, 타임아웃은 "진짜 이상 상황"
        ///       (신호가 영영 안 오는 경우)에만 발동하는 순수 안전망(계기판)으로 복원된다.
        ///
        ///   대가: 진짜 이상 상황에서 안전망 발동이 0.5배(쿨다운의 절반)만큼 늦어진다. 하지만
        ///   공격자 사망/전투 중단 시에는 FlushAttacker가 즉시 방출(안전망 ⓓ)하므로 대부분의
        ///   이상 케이스는 타임아웃보다 먼저 커버된다 → 이 지연은 허용 가능하다.
        /// </summary>
        private const float TimeoutCooldownMultiplier = 1.5f;

        /// <summary>
        /// 공격자 AttackCooldown 조회 실패 시 사용하는 타임아웃 폴백(초).
        /// 이 시간이 지나도 로컬 타격 신호가 오지 않으면 연출을 강제로 방출한다.
        ///
        /// 정책 일관성 주석: 이 폴백은 쿨다운을 알 수 없을 때만 쓰는 "고정 비상값"이라
        ///   TimeoutCooldownMultiplier를 곱하지 않는다(곱할 쿨다운 값 자체가 없기 때문).
        ///   3초는 어떤 유닛 쿨다운의 1.5배보다도 충분히 큰 값이라, 배율 정책이 지향하는
        ///   "다음 타격 이벤트보다 타임아웃이 항상 늦게 온다"는 성질을 자연히 만족한다.
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
            // ※ OnUnitDied는 "공격자 사망"(안전망 ⓓ) 처리도 겸한다 — OnUnitDied 핸들러 참조.
            GameEvents.OnUnitDied
                .Subscribe(OnUnitDied)
                .AddTo(this);
            GameEvents.OnBuildingDied
                .Subscribe(OnBuildingDied)
                .AddTo(this);

            // 공격자 전투 중단 → 그 공격자의 잔여 보류 항목 즉시 방출(안전망 ⓓ).
            //   멀티: OnNetworkCombatStopped(서버 StopCombatClientRpc 경유), 싱글: OnCombatStopped.
            //
            // ※ 안전성 근거: StopCombat 신호는 "진행 중 사이클의 타격 프레임"보다 먼저 도착할 수 있으나,
            //   그 시점엔 해당 사이클의 데미지가 아직 적용되지 않아(=큐가 비어 있어) flush가 no-op이므로 안전하다.
            //   실제로 위험한 케이스는 "이미 등록된 항목이 있는데 다음 OnLocalAttackHit이 영영 오지 않는" 경우이며,
            //   이 구독이 바로 그 케이스를 커버한다(타임아웃까지 기다리지 않고 즉시 방출).
            GameEvents.OnNetworkCombatStopped
                .Subscribe(e => FlushAttacker(e.UnitId))
                .AddTo(this);
            GameEvents.OnCombatStopped
                .Subscribe(unitId => FlushAttacker(unitId))
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

                Emit(evt);
                return;
            }

            // ⓒ 공격자 GameObject가 없으면(스폰 전/파괴 등) 로컬 OnAttackHit 신호를 기대할 수 없다 → 즉시 방출.
            GameObject attackerGo = _unitFactory != null ? _unitFactory.GetUnitObject(evt.AttackerId) : null;
            if (attackerGo == null)
            {
                Emit(evt);
                return;
            }

            // 정상 경로: 공격자 큐에 보류. 공격자의 로컬 OnAttackHit에서 방출된다.
            float timeout = ResolveTimeout(evt.AttackerId);
            Queue<PendingHit> queue = GetOrCreateQueue(evt.AttackerId);
            queue.Enqueue(new PendingHit(evt, Time.time, timeout));
        }

        /// <summary>
        /// 공격자의 로컬 타격 프레임 신호 수신. 공격자의 "타격 프레임 수"에 따라
        /// 큐에서 1건만(다중 히트) 또는 전부(단일 히트) 방출한다.
        /// </summary>
        private void OnLocalAttackHit(int attackerId)
        {
            if (_pendingByAttacker.TryGetValue(attackerId, out Queue<PendingHit> queue) && queue.Count > 0)
            {
                // ── 왜 공격자의 HitFrameTimes.Length로 분기하는가? (유니티 초급자 설명) ──
                //
                // OnLocalAttackHit은 공격자 애니메이션의 "타격 프레임(칼/도끼가 실제로 닿는 순간)"이
                // 지나갈 때마다 정확히 1번씩 온다. HitFrameTimes 배열은 그 타격 프레임이 한 스윙에
                // 몇 개 있는지를 나타낸다(원소 1개 = 타격 순간 1번, 원소 N개 = 타격 순간 N번).
                //
                //  · 단일 타격 프레임(Length <= 1): 한 스윙에 타격 순간이 딱 1번뿐이다.
                //      일반 단일 타깃 유닛은 그 1번의 타격으로 1마리만 때리므로 큐에도 1건뿐이다.
                //      그러나 휩쓸기(도끼병)는 "그 1번의 스윙"으로 여러 적을 동시에 벤다 →
                //      한 타격 프레임에 대응하는 피해 이벤트가 큐에 N건 쌓인다. 이 N건은 모두
                //      "같은 타격 순간"에 속하므로 이번 신호에서 전부 방출해야 N마리 피격 연출이
                //      타격 모션에 맞춰 '동시에' 뜬다.
                //
                //  · 다중 타격 프레임(Length > 1, 예: LionKnight 2타 / FlameSpirit 6타):
                //      한 스윙 안에 타격 순간이 여러 번이고 각 타격 프레임이 각자의 피해 1건에
                //      1:1로 대응한다. 따라서 신호당 1건씩만 방출해야 "N번째 타격 모션 = N번째 피격
                //      연출"로 위상이 맞는다(기존 동작 그대로 → 다중 히트 유닛 회귀 없음).
                //
                // 공격자 조회 실패(null)나 HitFrameTimes 미설정 등 '판별 불가' 시에는 값을 신뢰할 수
                // 없으므로 보수적으로 기존 동작(1건 방출)으로 폴백한다.
                UnitData attacker = _unitSpawn != null ? _unitSpawn.GetUnit(attackerId) : null;
                bool singleHitFrame = attacker?.HitFrameTimes != null && attacker.HitFrameTimes.Length <= 1;

                if (singleHitFrame)
                {
                    // 단일 타격 프레임 → 이번 스윙의 보류 항목을 전부 방출(휩쓸기 N마리 동시 표시).
                    // 일반 단일 타깃 유닛은 스윙당 큐에 1건뿐이라 "전부 방출 == 1건 방출"로 동작이
                    // 완전히 같다(회귀 없음). 순서 역전으로 이전 스윙 건이 남아 있었다면 함께 따라잡아
                    // 방출되므로 오히려 지연이 줄어든다.
                    while (queue.Count > 0)
                        Emit(queue.Dequeue().Event);
                }
                else
                {
                    // 다중 타격 프레임(또는 판별 불가 폴백) → 기존대로 FIFO 가장 오래된 1건만 방출.
                    Emit(queue.Dequeue().Event);
                }
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

        /// <summary>
        /// 유닛 사망 처리. 두 가지 안전망을 겸한다:
        ///   ⓑ 이 유닛을 "타겟"으로 겨눈 잔여 항목을 즉시 방출(사망 연출 전 마지막 피격 표시 보장).
        ///   ⓓ 이 유닛이 "공격자"였던 큐의 잔여 항목을 즉시 방출 — 죽은 공격자는 다음 타격 프레임을
        ///      보내지 않으므로 타임아웃까지 방치하지 않고 지금 방출한다.
        /// 두 큐는 서로 독립적이다(같은 유닛이 자신을 공격하는 경우는 없음).
        /// </summary>
        private void OnUnitDied(UnitDiedEvent e)
        {
            if (e.Unit == null) return;
            FlushTarget(e.Unit.Id, targetIsUnit: true);   // ⓑ 타겟으로서
            FlushAttacker(e.Unit.Id);                      // ⓓ 공격자로서
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
                    Emit(head.Event);
                }
            }
        }

        // ====================================================================
        // 방출 — 실제 표현 재생
        // ====================================================================

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
                        Emit(item.Event);   // 사망 연출 전 마지막 피격 표시 보장.
                    else
                        queue.Enqueue(item);
                }
            }
        }

        /// <summary>
        /// 특정 "공격자"의 큐에 남은 모든 보류 항목을 즉시 방출하고 그 큐를 제거한다(안전망 ⓓ).
        ///
        /// 사용처: 공격자 사망(공격자사망) / 공격자 전투 중단(전투중단).
        /// 두 경우 모두 그 공격자는 더 이상 타격 프레임(OnLocalAttackHit)을 보내지 않으므로,
        /// 잔여 항목을 지금 방출하지 않으면 타임아웃(쿨다운의 1.5배)까지 HP 텍스트가 지연된다.
        /// 큐가 없으면(보류 항목 없음) 아무것도 하지 않는다(no-op) — StopCombat이 타격 프레임보다
        /// 먼저 도착한 정상 케이스가 여기에 해당하며 안전하다.
        /// </summary>
        /// <param name="attackerId">방출 대상 공격자 Id(=_pendingByAttacker의 키).</param>
        private void FlushAttacker(int attackerId)
        {
            if (!_pendingByAttacker.TryGetValue(attackerId, out Queue<PendingHit> queue))
                return;

            // FIFO 순서 그대로 앞에서부터 전부 방출.
            while (queue.Count > 0)
                Emit(queue.Dequeue().Event);

            // 공격자 큐 자체를 제거 — 이 공격자는 이번 전투에서 더 이상 항목을 쌓지 않는다.
            // (재교전 시 GetOrCreateQueue가 새 큐를 다시 만든다.)
            _pendingByAttacker.Remove(attackerId);
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
        /// 공격자 AttackCooldown × TimeoutCooldownMultiplier(1.5배)를 타임아웃으로 반환.
        /// 조회 실패 시 상수 폴백(3초). 배율을 곱하는 이유는 TimeoutCooldownMultiplier 주석 참조
        /// (요약: 1.0배면 "다음 타격 이벤트"와 동률 경주 → 1.5배면 타격 이벤트가 항상 먼저 도착).
        /// </summary>
        private float ResolveTimeout(int attackerId)
        {
            if (_unitSpawn != null)
            {
                UnitData attacker = _unitSpawn.GetUnit(attackerId);
                if (attacker != null && attacker.AttackCooldown > 0f)
                    return attacker.AttackCooldown * TimeoutCooldownMultiplier;
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
