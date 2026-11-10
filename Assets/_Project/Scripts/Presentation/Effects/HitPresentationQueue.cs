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
using System;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 피격 표현(HP 텍스트·VFX·타격 반응)을 공격자의 로컬 타격 프레임에 맞춰 방출하는 큐.
    /// 도메인 HP는 이미 갱신된 상태이며, 이 큐는 오직 "표현"만 담당한다(서버 권위 불변).
    /// </summary>
    public class HitPresentationQueue : MonoBehaviour, IDisposable
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

        // 여우마법사 표시 지연 재검증용: 큐 인스턴스당 최대 128개 경계만 기록한다.
        // 공격 회차 키가 없는 Legacy이므로 회차 상관이나 화면 픽셀 표시를 주장하지 않는다.
        private const int FoxLegacyDiagnosticLimit = 128;
        private int _foxLegacyDiagnosticCount;

        // 결과가 정상 도착했지만 View 생성/복제 순서만 늦은 경우의 재시도 창이다.
        // Coordinator의 기존 catch-up 계약(0.50초)과 같은 경계를 사용하며 임의로 늘리지 않는다.
        private const double AuthoritativeViewRetrySeconds =
            AuthoritativeAttackPresentationCoordinator.CatchUpSeconds;

        // ====================================================================
        // 의존성 (Initialize()에서 주입)
        // ====================================================================

        /// <summary> HP 텍스트 표시를 위임할 스포너. 방출 시점에 ShowDamage()를 호출. </summary>
        private FloatingHpTextSpawner _hpTextSpawner;
        private Func<bool> _waveOwnerReady;
        /// <summary>조합 루트에서 로컬 wave 소비자 준비 조건을 전달한다.</summary>
        public void SetWaveOwnerReady(Func<bool> ready) => _waveOwnerReady = ready;

        // ────────────────────────────────────────────────────────────────
        // [2026-10-20 서버 권위 투사체 — Q7 · RM-7] 투사체 착탄 피해의 표현 보관함.
        //   투사체 번호표가 붙은 피해 이벤트는 공격자별 FIFO(로컬 타격 신호 대기)에 넣지 않는다.
        //   FIFO에 넣으면 "빈 큐 신호 버림"(아래 OnLocalAttackHit 주석) 때문에 숫자가 다음 공격·타임아웃까지 밀린다(위험 1).
        //   대신 투사체 화면 소비자가 "화면상 착탄 순간"에 ReleaseProjectileImpact를 불러 1회 방출한다.
        //   [2026-10-21 제안 A] 사망타는 그 순간 대상 오브젝트가 이미 디스폰돼 있을 수 있다(Client 0.10초 지연).
        //   그때는 화면 착탄 지점(또는 보관 때 기억한 대상 위치)에 숫자 "0"을 낸다(EmitProjectile 참조).
        //   비행 중 대상이 먼저 죽은 투사체는 서버가 TargetLost로 끝내 HP 이벤트 자체가 없다 → 숫자도 없다(착탄 VFX만).
        //   소비자가 준비되지 않았으면(_projectileOwnerReady=false) 받는 즉시 방출해 표시가 사라지지 않게 한다.
        //   FIFO 코드 자체는 다른 유닛이 계속 쓰므로 그대로 둔다.
        // ────────────────────────────────────────────────────────────────
        private Func<bool> _projectileOwnerReady;
        // [2026-10-21 사망타 숫자 누락 수정 — 제안 A] 보관함에는 피해 이벤트만이 아니라
        //   "보관한 순간(HP 이벤트 도착 시점)의 대상 화면 위치"도 같이 넣는다(ProjectileHeldHit 참조).
        private readonly ProjectileImpactPresentationHold<ProjectileHeldHit> _projectileHold =
            new ProjectileImpactPresentationHold<ProjectileHeldHit>();
        private readonly List<ProjectileHeldHit> _projectileReleased = new List<ProjectileHeldHit>();

        // [제안 A] 화면 착탄 신호(Release)가 HP 이벤트보다 먼저 온 경우(순서 역전) 그 착탄 지점을 잠시 기억한다.
        //   나중에 HP 이벤트가 와서 "바로 방출"될 때 대상이 이미 사라졌으면 이 지점에 숫자를 낸다.
        //   보관함의 "먼저 온 신호" 기억과 같은 시간(TimeoutSeconds)만 유지하고, 같은 상한(Capacity)을 넘기지 않는다.
        private readonly Dictionary<ProjectileDeliveryKey, ReleasePoint> _projectileReleasePoints =
            new Dictionary<ProjectileDeliveryKey, ReleasePoint>();
        private readonly List<ProjectileDeliveryKey> _projectileReleasePointsExpired = new List<ProjectileDeliveryKey>();

        // [제안 A 진단] 투사체 피해 숫자의 실제 표시 결과(읽기 전용 관측 — 방출 판단에는 쓰지 않는다).
        //   played      = 살아 있는 대상의 현재 화면 위치에 표시(기존과 같은 정상 경로).
        //   fallback    = 대상 화면 오브젝트가 이미 없어(사망 디스폰·건물 파괴) 대체 지점(화면 착탄 지점 또는 보관 때 기억한 위치)에 표시.
        //   missing     = 결국 표시하지 못한 수(스포너 미준비·대체 지점 없음 등 모든 사유).
        //   missingDead = missing 중 "대상 화면 오브젝트가 없었던" 수(이번 버그의 증상 그대로 — 0이어야 정상).
        //   불변식: played + fallback + missing = atImpact + immediateAfterRelease + immediateCapacity + timeout(보관함 계수).
        private int _projectileTextPlayed, _projectileTextFallback, _projectileTextMissing, _projectileTextMissingDead;

        /// <summary>조합 루트에서 투사체 화면 소비자 준비 조건을 전달한다.</summary>
        /// <param name="ready">소비자가 준비됐으면 true를 돌려주는 함수.</param>
        public void SetProjectileOwnerReady(Func<bool> ready) => _projectileOwnerReady = ready;

        // [2026-10-20 단계 B 진단] 투사체 번호표가 붙은 피해 이벤트가 왔는데 화면 소비자가 준비되지 않아(경기 종료 뒤 등)
        //   보관함을 거치지 않고 기존 경로로 흘러간 수. 읽기 전용 관측이며 방출 판단에는 쓰지 않는다.
        private int _projectileOwnerNotReadyEvents;

        /// <summary>
        /// [진단] 투사체 피해 숫자 보관함의 방출 경로별 계수 — END 줄 조각
        /// "held:N|atImpact:N|immediateAfterRelease:N|immediateCapacity:N|timeout:N|emptyRelease:N|heldNow:N|ownerNotReady:N".
        /// 착탄 방출 = atImpact, 즉시 방출 = immediateAfterRelease + immediateCapacity, 타임아웃 방출 = timeout.
        /// </summary>
        public string DescribeProjectileHoldForDiagnostics()
            => _projectileHold.DescribeForDiagnostics() + "|ownerNotReady:" + _projectileOwnerNotReadyEvents;

        /// <summary>[진단] 타임아웃 방출 수(END verdict 판정용).</summary>
        public int ProjectileHoldTimeoutItems => _projectileHold.TimeoutItems;

        /// <summary>
        /// [진단] 투사체 피해 숫자의 표시 결과 END 줄 조각 — "played:N|fallback:N|missing:N|missingDead:N".
        /// END 줄에서는 "text=" 뒤에 붙는다(hold= 바로 다음 토큰).
        /// </summary>
        public string DescribeProjectileTextForDiagnostics()
            => string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "played:{0}|fallback:{1}|missing:{2}|missingDead:{3}",
                _projectileTextPlayed, _projectileTextFallback, _projectileTextMissing, _projectileTextMissingDead);

        /// <summary>[진단] 표시하지 못한 투사체 피해 숫자 수(END verdict 판정용).</summary>
        public int ProjectileTextMissingItems => _projectileTextMissing;

        // ────────────────────────────────────────────────────────────────
        // [2026-10-26 수정안 (a)] 투사체 사망타의 "사망 표현"과 화상 VFX 시작을 화면 착탄에 맞추기 위한 창구.
        //   이 큐는 이미 번호표가 붙은 피해를 "화면 착탄 순간"까지 보관한다. 사망 표현 보류 장치
        //   (DeferredUnitDeathPresenter)와 화상 VFX 소비자(TimedDamageEffectPresenter)는
        //   ① 지금 보관 중인 번호표를 묻고(CollectHeldKillShotKeys · HasHeldProjectileHit)
        //   ② 보관 항목이 화면에 나오는 순간을 통보받는다(ProjectileHitShown).
        //   보관·방출 판단 자체는 바뀌지 않는다(읽기 + 통보만 추가).
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 투사체 번호표가 붙은 보관 피해가 화면에 방출된 순간(착탄·순서 역전 즉시·타임아웃·상한 초과 모두) 1건마다 알린다.
        /// 인자: 번호표, 원본 피해 이벤트(대상·남은 HP). 구독자의 예외는 여기서 막아 숫자 방출을 깨뜨리지 않는다.
        /// </summary>
        public event Action<ProjectileDeliveryKey, EntityDamagedEvent> ProjectileHitShown;

        /// <summary>
        /// 이 유닛을 죽인 "사망타"(남은 HP 0 이하) 중 아직 화면 착탄을 기다리며 보관 중인 번호표를 output에 모은다.
        /// 대상은 객체 참조로 비교한다(같은 Id의 다른 유닛 — 재경기·ID 재사용 — 과 섞이지 않는다).
        /// </summary>
        /// <param name="unit">사망한 유닛 데이터.</param>
        /// <param name="output">번호표를 담을 목록(호출자가 비워 둔다).</param>
        public void CollectHeldKillShotKeys(UnitData unit, List<ProjectileDeliveryKey> output)
        {
            if (unit == null || output == null) return;
            _projectileHold.CollectKeys(
                static (held, target) => held.Event.IsUnit && held.Event.CurrentHp <= 0
                                         && ReferenceEquals(held.Event.Entity, target),
                unit, output);
        }

        /// <summary>
        /// 이 대상에게 attackerId(유닛 공격자)의 투사체 피해가 아직 화면 착탄을 기다리며 보관 중인가.
        /// 화상 VFX 시작 보류 판단에 쓴다(같은 공격자의 착탄이 화면에 나올 때까지 불을 켜지 않는다).
        /// </summary>
        /// <param name="target">피해 대상.</param>
        /// <param name="attackerId">공격자 유닛 Id.</param>
        public bool HasHeldProjectileHit(IDamageable target, int attackerId)
        {
            if (target == null) return false;
            return _projectileHold.Any(
                static (held, query) => held.Event.AttackerIsUnit && held.Event.AttackerId == query.attackerId
                                        && ReferenceEquals(held.Event.Entity, query.target),
                (target, attackerId));
        }

        private void NotifyProjectileHitShown(ProjectileDeliveryKey key, EntityDamagedEvent evt)
        {
            Action<ProjectileDeliveryKey, EntityDamagedEvent> handlers = ProjectileHitShown;
            if (handlers == null || !key.IsValid) return;
            foreach (Action<ProjectileDeliveryKey, EntityDamagedEvent> handler in handlers.GetInvocationList())
            {
                try { handler(key, evt); }
                catch (Exception exception)
                {
                    GameLog.Dev.Warn("Combat", nameof(HitPresentationQueue),
                        "Projectile hit-shown subscriber failed; number presentation unchanged",
                        $"projectile={key}, error={exception.GetType().Name}:{exception.Message}");
                }
            }
        }

        /// <summary>
        /// 투사체 화면 소비자가 "화면상 착탄 순간"에 부른다. 그 투사체로 생긴 피격 표현(숫자·피격 VFX·펀치)을 한 번 방출한다.
        /// 피해 이벤트가 아직 안 왔으면(순서 역전) 잠시 기억했다가 도착하는 즉시 방출한다.
        ///
        /// [2026-10-21 제안 A] screenImpactPoint = 화면에서 투사체가 실제로 닿은 지점(숫자·피격 VFX의 대체 위치).
        ///   왜 필요한가(초급자 설명): 서버는 대상을 죽이는 한 발을 처리하면서 같은 호출 안에서 유닛 오브젝트를 디스폰한다.
        ///   Client 화면은 0.10초 늦게 착탄을 그리므로, 이 함수가 불릴 때 대상 오브젝트는 이미 파괴돼 위치를 알 수 없다.
        ///   그때 이 지점에 숫자("0")를 낸다. 대상이 살아 있으면 지금까지처럼 대상 위치를 쓴다(이 지점은 쓰지 않는다).
        ///   NaN·무한대 값이면 "지점 없음"으로 본다(보관할 때 기억한 위치를 대신 쓴다).
        /// </summary>
        /// <param name="key">투사체 번호표.</param>
        /// <param name="screenImpactPoint">화면 착탄 지점(화면 좌표 — Red Client 반전이 이미 적용된 값).</param>
        public void ReleaseProjectileImpact(ProjectileDeliveryKey key, Vector3 screenImpactPoint)
        {
            if (!key.IsValid) return;
            bool hasPoint = IsFinite(screenImpactPoint);
            double now = Time.realtimeSinceStartupAsDouble;
            // 순서 역전 대비: HP 이벤트가 나중에 오면 이 지점을 대체 위치로 쓴다(상한을 넘으면 기억하지 않는다 — 기억한 위치로 대신한다).
            if (hasPoint && (_projectileReleasePoints.Count < ProjectileImpactPresentationHold<ProjectileHeldHit>.Capacity
                             || _projectileReleasePoints.ContainsKey(key)))
                _projectileReleasePoints[key] = new ReleasePoint(screenImpactPoint, now);
            _projectileReleased.Clear();
            _projectileHold.Release(key, now, _projectileReleased);
            EmitProjectileReleased(LegacyPresentationObservationKind.EmittedMarker, hasPoint, screenImpactPoint);
        }

        // 꺼낸 보관 항목을 차례로 방출한다. hasPoint=false(타임아웃 등)면 각 항목이 보관 때 기억한 위치를 대체값으로 쓴다.
        private void EmitProjectileReleased(LegacyPresentationObservationKind kind, bool hasPoint, Vector3 point)
        {
            for (int i = 0; i < _projectileReleased.Count; i++)
                EmitProjectile(_projectileReleased[i], kind, hasPoint, point);
            _projectileReleased.Clear();
        }

        // 화면 착탄 신호가 끝내 오지 않은 보관 항목을 강제 방출하고, 오래된 "먼저 온 착탄 지점" 기억을 지운다.
        private void TickProjectileHold(double now)
        {
            _projectileReleased.Clear();
            _projectileHold.CollectExpired(now, _projectileReleased);
            // 타임아웃 방출은 착탄 지점이 없다 → 각 항목이 보관 때 기억한 위치가 대체값이 된다(제안 A 범위 2).
            EmitProjectileReleased(LegacyPresentationObservationKind.EmittedTimeout, false, default);
            if (_projectileReleasePoints.Count == 0) return;
            _projectileReleasePointsExpired.Clear();
            foreach (KeyValuePair<ProjectileDeliveryKey, ReleasePoint> pair in _projectileReleasePoints)
                if (now - pair.Value.At > ProjectileImpactPresentationHold<ProjectileHeldHit>.TimeoutSeconds)
                    _projectileReleasePointsExpired.Add(pair.Key);
            for (int i = 0; i < _projectileReleasePointsExpired.Count; i++)
                _projectileReleasePoints.Remove(_projectileReleasePointsExpired[i]);
            _projectileReleasePointsExpired.Clear();
        }

        private static bool IsFinite(Vector3 v)
            => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                 || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        /// <summary> 유닛 GameObject 조회용(VFX 위치 + 스케일 펀치 대상). </summary>
        private UnitFactory _unitFactory;

        /// <summary> 건물 GameObject 조회용(스케일 펀치 대상 + 타워 발사 VFX 위치). </summary>
        private BuildingFactory _buildingFactory;

        /// <summary>
        /// 로컬 화면에 그려지는 유닛/건물 pose 조회용.
        /// Simulation용 IEntityPositionProvider와 분리하여 전투 권위 좌표를 오염시키지 않는다.
        /// </summary>
        private IPresentationPoseProvider _presentationPoseProvider;

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
        /// 싱글플레이 또는 정규 결과 키가 없는 기존 특수 경로만 사용하는 공격자 FIFO다.
        /// 멀티플레이 정규 결과는 아래 exact-scope rendezvous만 사용한다.
        /// </summary>
        private readonly Dictionary<int, Queue<PendingHit>> _pendingByAttacker
            = new Dictionary<int, Queue<PendingHit>>();

        /// <summary>
        /// 멀티플레이 결과와 로컬 marker/tracer를 `(instance, sequence, hitIndex)`로만
        /// 결합한다. 공격자 Id FIFO를 사용하지 않으므로 다음 공격 marker가 이전 결과를
        /// 꺼내는 0.5~2초 시각 지연을 구조적으로 막는다.
        /// </summary>
        private readonly AttackPresentationImpactRendezvous<EntityDamagedEvent>
            _exactRendezvous = new AttackPresentationImpactRendezvous<EntityDamagedEvent>();
        private readonly List<EntityDamagedEvent> _releasedExact =
            new List<EntityDamagedEvent>();
        // 확정 결과가 사망 이벤트보다 늦게 도착해도 데이터 객체를 잃지 않는다.
        // 객체의 HP를 쓰지 않고 표시 위치/종류 해석에만 사용한다. 경기 종료 시 해제한다.
        private readonly Dictionary<int, IDamageable> _confirmedUnitViews = new();
        private readonly Dictionary<int, IDamageable> _confirmedBuildingViews = new();
        private readonly Dictionary<AttackPresentationScope, PendingAuthoritativeBundle>
            _pendingAuthoritative = new();
        private readonly List<AttackPresentationScope> _completedAuthoritative = new();
        private readonly List<PreparedAuthoritativePresentation> _preparedAuthoritative = new();
        private readonly List<AttackResultPresentationInput> _visualAuthoritativeResults = new();
        // AddTo(this)는 Play Mode 수명에는 충분하지만 Edit Mode에서 DestroyImmediate로
        // 픽스처를 반복 생성/파괴하는 검증에서는 해제 시점이 Unity 생명주기에 의존한다.
        // 이 컴포넌트가 구독을 직접 소유해 재초기화와 파괴 모두에서 동기적으로 끊는다.
        private readonly CompositeDisposable _subscriptions = new();
        private readonly HashSet<(ulong Match, ulong Lifetime, ulong Sequence)> _attackEffectKeys = new();
        private readonly List<AttackEffectPresentation> _attackEffectPending = new();
        private readonly Dictionary<(ulong, ulong, ulong), double> _attackEffectReceivedAt = new();
        private System.Func<double> _attackEffectClock = () => Time.timeAsDouble;
        private bool _attackEffectsClosed;
        private int _attackEffectFailures, _attackEffectOverflow;
        private bool _attackClockFaultReported;
        private double ReadAttackEffectClock()
        {
            try { return _attackEffectClock(); }
            catch (Exception ex)
            {
                if (!_attackClockFaultReported) { _attackClockFaultReported = true; _attackEffectFailures++; AttackEffectIdentity.Record("Fault", default, "CanonicalClock:" + ex.Message); }
                return double.NaN;
            }
        }
        private void CloseAttackEffects(string reason)
        {
            if (_attackEffectsClosed) return;
            _attackEffectsClosed = true;
            foreach (var s in _attackEffectPending) AttackEffectIdentity.Record("Skip", s, reason + "; NotStarted=True");
            AttackEffectIdentity.End(_attackEffectPending.Count, _attackEffectFailures, _attackEffectOverflow, reason, _attackEffectKeys.Count);
            _attackEffectPending.Clear(); _attackEffectReceivedAt.Clear();
            // [2026-11-10 F0 · [BOLT-END]] 경기가 끝나면 재생하지 못한 번개의 보관 값도 함께 비운다(Plan 7절 R2).
            //   End 줄은 비우기 전에 남겨야 "받았지만 재생 못 한 수(NotStarted)"를 셀 수 있다.
            DiagBoltEnd(reason);
            _boltTargetTypes.Clear();
            ClearBoltDiagnosticEntries();
        }
        public void SetAttackEffectClock(System.Func<double> clock) => _attackEffectClock = clock ?? throw new System.ArgumentNullException(nameof(clock));
        private void ReceiveAttackEffect(AttackEffectPresentation s)
        {
            if (_attackEffectsClosed || !s.IsValid) { AttackEffectIdentity.Record("Skip", s, "ClosedOrInvalidSnapshot"); return; }
            var key = (s.Match, s.Lifetime, s.Sequence);
            if (_attackEffectKeys.Contains(key)) { AttackEffectIdentity.Record("Skip", s, "Duplicate"); return; }
            if (_attackEffectKeys.Count >= 8192 || _attackEffectPending.Count >= 256)
            { _attackEffectOverflow++; AttackEffectIdentity.Record("Fault", s, "CapacityFailure; Completeness=Unknown"); return; }
            _attackEffectKeys.Add(key);
            // 화면으로 오는 공격 표현 스냅샷은 즉발 타워 발사(Tower)뿐이다 — 받은 즉시 아래 EmitAttackEffects가 재생한다.
            //   (MushroomBomber 착탄은 서버 투사체 기록 경로가 그리므로 스냅샷이 오지 않는다 — UnitCombatUseCase.ExecuteAttack.)
            _attackEffectReceivedAt[key] = Time.realtimeSinceStartupAsDouble;
            _attackEffectPending.Add(s);
            // [2026-11-10 F0] 번개 타워 스냅샷이면 지금(받는 순간) 대상 유닛 종류를 잡아 둔다 — 재생 때는 이미 화면 오브젝트가
            //   사라졌을 수 있다. 🔴 싱글 · Host에서는 이 줄이 서버 ExecuteTowerAttack 안에서 동기로 실행되므로
            //   두 메서드 모두 안에서 예외를 삼킨다(밖으로 던지면 피해 · 사망 처리가 멈출 수 있다 — Plan 7절 R1).
            CaptureBoltTargetType(s, key);
            DiagBoltReceive(s, key); // [BOLT-END] Receive — 개발 빌드 · 에디터 전용(릴리스에서는 호출 자체가 사라진다)
        }
        private void EmitAttackEffects()
        {
            double now = ReadAttackEffectClock();
            for (int i = _attackEffectPending.Count - 1; i >= 0; i--)
            {
                var s = _attackEffectPending[i];
                var key = (s.Match, s.Lifetime, s.Sequence);
                if (Time.realtimeSinceStartupAsDouble - _attackEffectReceivedAt[key] >= AuthoritativeAttackPresentationCoordinator.MissingScheduleDeadlineSeconds)
                { _attackEffectFailures++; AttackEffectIdentity.Record("Skip", s, "PresentationClockWaitExpired; Completeness=Unknown", now); _attackEffectPending.RemoveAt(i); _attackEffectReceivedAt.Remove(key); ForgetBoltCapture(key); continue; }
                // 타워 스냅샷은 표시 시각을 기다리지 않고 바로 재생한다(공격 순간 = 발사 이펙트). 시계 값이 비정상이면 다음 프레임에 다시 본다.
                if (double.IsNaN(now) || double.IsInfinity(now)) continue;
                Vector3 source = s.Source, impact = s.Impact;
                if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer && ViewConverter.IsFlipped)
                { source = ViewConverter.ToView(source); impact = ViewConverter.ToView(impact); }
                string outcome = "ManagerUnavailable";
                try
                {
                    // [2026-10-13 B] 번개 타워만 "원래 맞은 유닛"의 화면 오브젝트를 재생 1회 시점에 해석해 넘긴다.
                    //   해석 실패면 default → 추적 없이 스냅샷 Impact까지 길이만 맞춘다(NET-PRESENT-003).
                    //   다른 공격 방식(투사체 · 지면 분출)은 해석하지 않으며 PlayAttackEffect도 이 인자를 무시한다.
                    //   지면 분출은 공격 순간 대상 발밑(스냅샷 Impact)에 한 번 놓고 끝이라 대상을 찾을 필요가 없다.
                    TowerBoltTarget boltTarget = default;
                    string boltUntracked = null;
                    // 그 종족 타워의 공격 프로필이 「번개(Beam)」면 해석한다. EffectManager의 번개 분기와
                    //   같은 판단(TowerAttackNeedsTargetTracking)을 써서 두 곳이 어긋나지 않게 한다(Plan 10절 K-1).
                    if (s.Visual == AttackEffectVisual.Tower && EffectManager.Instance != null
                        && EffectManager.Instance.TowerAttackNeedsTargetTracking(s.Race))
                        boltTarget = ResolveTowerBoltTarget(s.TargetId, out boltUntracked);
                    // [2026-11-10 F0] 받는 순간 잡아 둔 대상 종류(없으면 null). 번개 고정 갈래의 가슴 높이에만 쓰인다.
                    UnitType? capturedBoltType = TryGetCapturedBoltType(key);
                    // [F0 비활성화 — 2026-11-10] 잡아 둔 종류를 넘기지 않던 옛 호출. 최종 삭제는 사용자 테스트 통과 후(WORKFLOW [4]).
                    // bool played = EffectManager.Instance != null && EffectManager.Instance.PlayAttackEffect(s, source, impact, out outcome,
                    //     boltTarget, boltUntracked);
                    bool played = EffectManager.Instance != null && EffectManager.Instance.PlayAttackEffect(s, source, impact, out outcome,
                        boltTarget, boltUntracked, capturedBoltType);
                    // [2026-11-03 — Plan 6-3 P-2] 타워 공격음: 발사 VFX를 부른 바로 이 자리에서 짝으로 호출한다(사운드 규칙 15).
                    //   클립은 SoundConfig 「타워 공격 SFX」 칸(건물 종류 + 종족)에서 고른다 — 비어 있으면 무음.
                    //   Human · RuneSpire 클립은 효과음 2단계에서, VineTower 클립은 덩굴 VFX 확정 뒤에 넣는다(2026-11-03 현재 칸 비어 있음).
                    //   VFX 재생 성공 여부와 무관하게 부른다(유닛 발사음과 같은 방식 — Plan 13절 A-5).
                    //   각 화면이 자기 표현 시점에 로컬로 낸다 — 네트워크로 소리를 보내지 않는다(사운드 규칙 14).
                    //   이중 재생 없음: 인간 대포 타워는 서버 투사체 발사기가 연결돼 있으면 이 스냅샷을 내지 않으므로
                    //   (TowerCombatUseCase) 공격음은 ProjectilePresentationPresenter의 발사 신호 자리에서만 난다.
                    if (s.Visual == AttackEffectVisual.Tower)
                        AudioManager.Instance?.PlayTowerAttackSfx(BuildingType.AutoTower, s.Race);
                    // 프로필이 비어 있는 타워(지금은 덩굴 타워)는 「재생할 것이 없음」이라 실패로 세지 않는다.
                    //   이 토큰을 빼지 않으면 덩굴 타워가 공격할 때마다 진단 END 줄의 실패 수가 늘어난다(Plan 10절 K-5).
                    if (!played && outcome != "TowerProfileEmpty") _attackEffectFailures++;
                    AttackEffectIdentity.Record(played ? "Play" : "Skip", s, outcome, now);
                    DiagBoltBegin(s, key, outcome, boltTarget, capturedBoltType); // [BOLT-END] Begin — 개발 빌드 · 에디터 전용
                }
                catch (Exception ex) { _attackEffectFailures++; AttackEffectIdentity.Record("Fault", s, "Impact:" + ex.Message, now); }
                _attackEffectPending.RemoveAt(i);
                _attackEffectReceivedAt.Remove(key);
                ForgetBoltCapture(key); // [F0] 재생이 끝난 스냅샷의 보관 값은 바로 지운다(쌓이지 않게 — Plan 7절 R2)
            }
        }
        /// <summary>
        /// [2026-10-13 B] 타워 스냅샷의 TargetId(서버 발급 유닛 ID — Client UnitFactory도 같은 ID로 등록한다)로
        /// 이 화면의 유닛 오브젝트를 "재생 1회 시점에만" 찾아, 그 객체 참조를 고정해 돌려준다.
        /// 이후 번개는 ID로 다시 찾지 않고 이 객체만 따라가므로 ID 재사용에 휩쓸리지 않는다(K-2).
        /// 화면 표현 전용 — 서버 타겟 선택·피해 판정에 쓰이지 않는다(NET-AUTH-001, 규칙 12의 "현재 target
        /// 재조회 금지"는 타워의 새 타겟 탐색을 뜻하며 이것은 원래 맞은 대상의 시각 추적이다 — D-5).
        /// </summary>
        /// <param name="targetId">스냅샷의 대상 유닛 ID.</param>
        /// <param name="reason">실패 사유(로그용 짧은 토큰). 성공이면 null.</param>
        /// <returns>추적 대상. 실패면 default(HasReference=false).</returns>
        private TowerBoltTarget ResolveTowerBoltTarget(int targetId, out string reason)
        {
            reason = null;
            if (_unitFactory == null || _presentationPoseProvider == null) { reason = "NoResolver"; return default; }
            GameObject go = _unitFactory.GetUnitObject(targetId);
            if (go == null) { reason = "TargetViewMissing"; return default; }
            UnitView view = go.GetComponent<UnitView>();
            UnitData data = view != null ? view.Data : null;
            // 같은 객체 수명 확인: View가 들고 있는 Data의 ID가 스냅샷과 같고 살아 있어야 한다.
            //   (GameBootstrapper.Map의 화상/독/힐 pose 해석과 같은 원칙 — 동일 객체 확인 후에만 붙는다)
            if (data == null || data.Id != targetId) { reason = "TargetDataMismatch"; return default; }
            if (!data.IsAlive) { reason = "TargetDead"; return default; }
            if (!go.activeInHierarchy) { reason = "TargetInactive"; return default; }
            Transform pose = _presentationPoseProvider.GetUnitTransform(targetId);
            if (pose == null) { reason = "PoseUnavailable"; return default; }
            return new TowerBoltTarget(pose, view, data, go.GetComponent<VisualRootProjector>());
        }

        // ====================================================================
        // [2026-11-10 F0 — runespire-bolt-death-endpoint Plan 3절] 번개 대상 유닛 종류를 "받는 순간" 잡아 둔다.
        //
        // 왜 필요한가(초급자 설명):
        //   RuneSpire 번개는 맞은 유닛의 가슴 높이를 겨눈다. 가슴 높이는 유닛 종류마다 다르다(크기 표 UnitEffectConfig).
        //   그런데 번개 한 방이 유닛을 죽이면, 번개가 그려지는 순간(다음 프레임 Update)에는 그 유닛이 이미 죽었거나
        //   화면 오브젝트가 사라져 "누구였는지"를 알 수 없었다 → 모든 유닛에 같은 0.6m를 써서 끝이 약 0.3~0.4m 위로 떴다.
        //   서버 스냅샷에는 유닛 종류가 들어 있지 않다(RPC 모양을 바꾸지 않기 위해 넣지 않는다 — 스키마 8 유지).
        //   그래서 스냅샷을 "받는 순간"(싱글 · Host는 서버 피해 처리 직후 · 도메인 목록에서 지우기 전) 화면 오브젝트나
        //   도메인 유닛 목록에서 종류를 읽어 스냅샷 키별로 보관했다가, 재생할 때 EffectManager에 넘긴다.
        //
        // 무엇을 바꾸지 않는가: 피해 · 타겟 · HP · 스냅샷 · RPC · 번개 끝점의 수평 위치(여전히 스냅샷 Impact).
        // 종류를 끝내 모르면(예: Client에서 사망 처리가 먼저 와서 오브젝트 · 데이터가 모두 없을 때) 예전처럼 0.6m다.
        // ====================================================================

        /// <summary>받는 순간 잡아 둔 번개 대상의 유닛 종류와, 어디서 찾았는지("View" 화면 오브젝트 / "Domain" 도메인 목록).</summary>
        private readonly struct BoltTargetTypeCapture
        {
            public readonly UnitType Type;
            public readonly string Source;
            public BoltTargetTypeCapture(UnitType type, string source) { Type = type; Source = source; }
        }

        /// <summary>
        /// 스냅샷 키(Match, Lifetime, Sequence)별 대상 종류 보관함. 재생(EmitAttackEffects) · 시간 초과 · 경기 종료 · 재초기화 때 지운다.
        /// 보류 목록(_attackEffectPending, 최대 256)에 있는 항목만 들어가므로 그 이상 쌓이지 않는다.
        /// </summary>
        private readonly Dictionary<(ulong, ulong, ulong), BoltTargetTypeCapture> _boltTargetTypes = new();

        /// <summary>이 스냅샷이 "원래 맞은 유닛을 따라가는 번개(Beam)" 타워의 것인가. EmitAttackEffects의 판단과 같은 식이다.</summary>
        private static bool IsBoltSnapshot(AttackEffectPresentation s)
            => s.Visual == AttackEffectVisual.Tower && EffectManager.Instance != null
               && EffectManager.Instance.TowerAttackNeedsTargetTracking(s.Race);

        /// <summary>
        /// [F0] 번개 스냅샷을 받는 순간 대상 유닛 종류를 찾아 보관한다. 예외는 여기서 삼킨다 —
        /// 싱글 · Host에서는 서버 공격 처리(ExecuteTowerAttack) 안에서 동기로 불리기 때문이다.
        /// 실패하면 보관하지 않으므로 재생 때 0.6m(예전 동작)가 된다.
        /// </summary>
        private void CaptureBoltTargetType(AttackEffectPresentation s, (ulong, ulong, ulong) key)
        {
            try
            {
                if (!IsBoltSnapshot(s)) return;
                if (TryFindBoltTargetType(s.TargetId, out UnitType type, out string source))
                    _boltTargetTypes[key] = new BoltTargetTypeCapture(type, source);
            }
            catch (Exception ex)
            {
                _boltTargetTypes.Remove(key);
                DiagBoltFault("CaptureType", ex);
            }
        }

        /// <summary>
        /// 대상 유닛 종류를 찾는다. ① 화면 오브젝트(UnitView)의 데이터 — ID가 같을 때만,
        /// ② 없으면 도메인 유닛 목록(UnitSpawnUseCase). 죽은 상태여도 종류는 읽을 수 있다(종류만 읽고 HP는 보지 않는다).
        /// </summary>
        /// <param name="targetId">스냅샷의 대상 유닛 ID.</param>
        /// <param name="type">찾은 유닛 종류.</param>
        /// <param name="source">"View" / "Domain" / 못 찾으면 "None".</param>
        /// <returns>찾았으면 true.</returns>
        private bool TryFindBoltTargetType(int targetId, out UnitType type, out string source)
        {
            type = default; source = "None";
            GameObject go = _unitFactory != null ? _unitFactory.GetUnitObject(targetId) : null;
            UnitView view = go != null ? go.GetComponent<UnitView>() : null;
            UnitData viewData = view != null ? view.Data : null;
            if (viewData != null && viewData.Id == targetId) { type = viewData.Type; source = "View"; return true; }
            UnitData domain = _unitSpawn?.GetUnit(targetId);
            if (domain != null && domain.Id == targetId) { type = domain.Type; source = "Domain"; return true; }
            return false;
        }

        /// <summary>[F0] 이 스냅샷 키로 보관한 대상 종류. 없으면 null(= 종류 모름 → 0.6m).</summary>
        private UnitType? TryGetCapturedBoltType((ulong, ulong, ulong) key)
            => _boltTargetTypes.TryGetValue(key, out BoltTargetTypeCapture capture) ? capture.Type : (UnitType?)null;

        /// <summary>[F0] 재생 · 시간 초과로 목록에서 빠진 스냅샷의 보관 값(종류 · 진단)을 지운다.</summary>
        private void ForgetBoltCapture((ulong, ulong, ulong) key)
        {
            _boltTargetTypes.Remove(key);
            _boltDiagReceives.Remove(key);
        }

        // ====================================================================
        // [2026-11-10 진단 로그 [BOLT-END] — runespire-bolt-death-endpoint Plan 2절 · 6절]
        //   축(LogRules §1.2): 심각도 Info(실패 1줄만 Warn), 존속 「개발」(사용자 결정 2026-11-10). 아래 Diag* 메서드에는
        //   [Conditional] 두 개가 붙어 있어 릴리스 빌드에서는 호출과 인자 계산이 통째로 사라진다(§1.7).
        //   동작은 바꾸지 않는다 — 읽고 기록만 한다. 각 메서드는 안에서 예외를 삼킨다(Host에서는 서버 공격 처리 안에서 불린다).
        //
        //   이벤트(경기 하나 기준):
        //     Receive — 번개 스냅샷을 받은 직후 1건당 1줄(받는 순간의 생존 · 화면 위치 · 종류).
        //     Begin   — 재생(PlayAttackEffect) 직후 1건당 1줄(추적/고정 갈래 · 실제 가슴 높이 · 끝점).
        //     Limit   — 상세 줄 상한을 처음 넘은 순간 1줄(치명 · 고정 128건, 비치명 대조군 32건 — Receive · Begin 한 쌍 = 1건).
        //     End     — 경기 종료 때 1줄(개수 요약). 번개를 하나도 받지 않은 경기는 남기지 않는다.
        //     Fault   — 진단 · 종류 캡처 중 예외가 나면 경기당 1줄만.
        //   값 표기: 좌표 · 길이는 밀리미터 정수, 축은 '|'로 잇는다(값 안에 ", "를 넣지 않는다 — §1.4). 모르면 None.
        //   비교 좌표계: 이 화면에 그리는 좌표(Client 반전 화면이면 Impact를 ToView로 바꾼 뒤 비교). Impactmm는 원래 값 그대로.
        // ====================================================================

        private const int BoltDiagLethalLimit = 128;
        private const int BoltDiagControlLimit = 32;

        /// <summary>[진단] 받는 순간의 값 중 재생 때 다시 필요한 것(키별 보관, 재생 · 종료 때 지운다).</summary>
        private sealed class BoltDiagReceive
        {
            public bool Detailed;        // 이 건의 상세 줄(Receive · Begin)을 남기는가(상한 안이었는가)
            public string AliveAtReceive; // "true" / "false" / "Unknown"
            public double ReceivedAt;    // Time.realtimeSinceStartupAsDouble
        }

        private readonly Dictionary<(ulong, ulong, ulong), BoltDiagReceive> _boltDiagReceives = new();
        private readonly Dictionary<string, int> _boltDiagUntrackedByReason = new();
        private int _boltDiagReceived, _boltDiagBegun, _boltDiagTracked, _boltDiagLethalUntracked, _boltDiagNonLethalDeadAtBegin;
        private int _boltDiagChestTable, _boltDiagChestFallback, _boltDiagFallbackKnownType, _boltDiagTypeUnknownAtReceive;
        private int _boltDiagLethalDetails, _boltDiagControlDetails, _boltDiagOmitted;
        private int _boltDiagMaxAbsChestDeltaMm = -1, _boltDiagMaxHDiffMm = -1; // -1 = 아직 값 없음(End에 None)
        private bool _boltDiagLethalLimitLogged, _boltDiagControlLimitLogged, _boltDiagFaultLogged, _boltDiagEnded;

        /// <summary>[진단] 새 경기 시작(Initialize) 때 개수 · 플래그를 0으로 되돌린다. 화면 동작과 무관한 값만 만진다.</summary>
        private void ResetBoltDiagnostics()
        {
            ClearBoltDiagnosticEntries();
            _boltDiagReceived = _boltDiagBegun = _boltDiagTracked = _boltDiagLethalUntracked = _boltDiagNonLethalDeadAtBegin = 0;
            _boltDiagChestTable = _boltDiagChestFallback = _boltDiagFallbackKnownType = _boltDiagTypeUnknownAtReceive = 0;
            _boltDiagLethalDetails = _boltDiagControlDetails = _boltDiagOmitted = 0;
            _boltDiagMaxAbsChestDeltaMm = _boltDiagMaxHDiffMm = -1;
            _boltDiagLethalLimitLogged = _boltDiagControlLimitLogged = _boltDiagFaultLogged = _boltDiagEnded = false;
        }

        /// <summary>[진단] 키별 보관 값과 사유별 개수를 비운다(경기 종료 · 재초기화).</summary>
        private void ClearBoltDiagnosticEntries()
        {
            _boltDiagReceives.Clear();
            _boltDiagUntrackedByReason.Clear();
        }

        /// <summary>[진단] 월드 미터 → 밀리미터 정수.</summary>
        private static int BoltMm(float meters) => Mathf.RoundToInt(meters * 1000f);

        /// <summary>[진단] 좌표를 "x|y|z" 밀리미터 정수 문자열로.</summary>
        private static string BoltMmText(Vector3 v)
            => FormattableString.Invariant($"{BoltMm(v.x)}|{BoltMm(v.y)}|{BoltMm(v.z)}");

        /// <summary>[진단] 두 점의 수평(XZ) 거리 — 밀리미터 정수.</summary>
        private static int BoltHorizontalMm(Vector3 a, Vector3 b)
        {
            Vector3 d = a - b; d.y = 0f;
            return BoltMm(d.magnitude);
        }

        /// <summary>[진단] 이 화면이 Client 반전 화면인가(EmitAttackEffects가 Impact를 ToView로 바꾸는 조건과 같다).</summary>
        private static bool BoltDiagClientFlipped
            => NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer && ViewConverter.IsFlipped;

        /// <summary>
        /// [진단] 대상 유닛의 지금 화면 위치(VisualRootProjector.PresentationPosition — 이 화면 좌표)를 읽는다.
        /// PresentationPosition은 읽을 때 ProjectNow를 부르는데, 같은 루트 위치에서 같은 값을 다시 쓰는 것뿐이라
        /// 화면에는 변화가 없다(VisualRootProjector 주석 「멱등」 — Plan 7절 R6 재확인).
        /// </summary>
        private static bool TryReadBoltViewPosition(GameObject go, out Vector3 position)
        {
            position = default;
            if (go == null) return false;
            VisualRootProjector projector = go.GetComponent<VisualRootProjector>();
            if (projector == null) return false;
            position = projector.PresentationPosition;
            return true;
        }

        /// <summary>
        /// [진단] 화면 오브젝트 · 도메인 목록에서 대상 데이터를 찾는다(ID가 같을 때만). 생존 여부 기록용.
        /// viewState = "Present"(화면 오브젝트의 데이터가 그 유닛) / "Missing"(화면 오브젝트 없음) / "Mismatch"(다른 데이터).
        /// </summary>
        private UnitData FindBoltDiagTarget(int targetId, out GameObject go, out string viewState)
        {
            go = _unitFactory != null ? _unitFactory.GetUnitObject(targetId) : null;
            UnitView view = go != null ? go.GetComponent<UnitView>() : null;
            UnitData viewData = view != null ? view.Data : null;
            viewState = go == null ? "Missing" : viewData != null && viewData.Id == targetId ? "Present" : "Mismatch";
            if (viewState == "Present") return viewData;
            UnitData domain = _unitSpawn?.GetUnit(targetId);
            return domain != null && domain.Id == targetId ? domain : null;
        }

        /// <summary>[진단] 상세 줄 1건(Receive · Begin 한 쌍)을 쓸 자리가 남았는지 확인하고 차지한다. 넘으면 Limit 1줄.</summary>
        private bool ReserveBoltDiagDetail(bool lethalKind)
        {
            if (lethalKind && _boltDiagLethalDetails < BoltDiagLethalLimit) { _boltDiagLethalDetails++; return true; }
            if (!lethalKind && _boltDiagControlDetails < BoltDiagControlLimit) { _boltDiagControlDetails++; return true; }
            _boltDiagOmitted++;
            if (lethalKind ? !_boltDiagLethalLimitLogged : !_boltDiagControlLimitLogged)
            {
                if (lethalKind) _boltDiagLethalLimitLogged = true; else _boltDiagControlLimitLogged = true;
                GameLog.Dev.Info("Combat", nameof(HitPresentationQueue), "[BOLT-END] Limit",
                    FormattableString.Invariant(
                        $"Diag=TowerBoltEnd, DiagRev=1, Fix=F0, Event=Limit, Role={ServerProjectileDiagnostics.RoleName}, DiagnosticLimit={(lethalKind ? BoltDiagLethalLimit : BoltDiagControlLimit)}, Kind={(lethalKind ? "Lethal" : "Control")}, Completeness=Unknown"));
            }
            return false;
        }

        /// <summary>
        /// [진단 — [BOLT-END] Receive] 번개 스냅샷을 받은 직후 1줄. 받는 순간 대상이 살아 있었는지, 화면 오브젝트가 있었는지,
        /// 종류를 어디서 찾았는지, 스냅샷 Impact와 그 순간 화면 위치가 수평 · 수직으로 얼마나 다른지를 남긴다.
        /// 치명(ResultHp ≤ 0) 또는 받는 순간 이미 죽어 있던 건은 「치명」 상한(128), 나머지는 「대조군」 상한(32)으로 센다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBoltReceive(AttackEffectPresentation s, (ulong, ulong, ulong) key)
        {
            try
            {
                if (!IsBoltSnapshot(s)) return;
                _boltDiagReceived++;
                UnitData data = FindBoltDiagTarget(s.TargetId, out GameObject go, out string viewState);
                string alive = data == null ? "Unknown" : data.IsAlive ? "true" : "false";
                bool lethal = s.ResultHp <= 0;
                bool hasType = _boltTargetTypes.TryGetValue(key, out BoltTargetTypeCapture capture);
                if (!hasType) _boltDiagTypeUnknownAtReceive++;
                var entry = new BoltDiagReceive { AliveAtReceive = alive, ReceivedAt = Time.realtimeSinceStartupAsDouble };
                _boltDiagReceives[key] = entry;

                bool flipped = BoltDiagClientFlipped;
                Vector3 impactView = flipped ? ViewConverter.ToView(s.Impact) : s.Impact;
                Vector3 recvView = default;
                bool hasView = viewState == "Present" && TryReadBoltViewPosition(go, out recvView);
                Transform pose = go != null ? _presentationPoseProvider?.GetUnitTransform(s.TargetId) : null;
                string hDiff = "None", vDiff = "None";
                if (hasView)
                {
                    int h = BoltHorizontalMm(impactView, recvView);
                    if (h > _boltDiagMaxHDiffMm) _boltDiagMaxHDiffMm = h;
                    hDiff = h.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    vDiff = BoltMm(recvView.y - impactView.y).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                string chestTable = "None";
                if (hasType && EffectManager.Instance != null && EffectManager.Instance.TryGetTableChestHeight(capture.Type, out float table))
                    chestTable = BoltMm(table).ToString(System.Globalization.CultureInfo.InvariantCulture);

                entry.Detailed = ReserveBoltDiagDetail(lethal || alive != "true");
                if (!entry.Detailed) return;
                GameLog.Dev.Info("Combat", nameof(HitPresentationQueue), "[BOLT-END] Receive",
                    FormattableString.Invariant(
                        $"Diag=TowerBoltEnd, DiagRev=1, Fix=F0, Event=Receive, Role={ServerProjectileDiagnostics.RoleName}, ViewFlipped={(flipped ? "true" : "false")}, Race={s.Race}, Match={s.Match}, Lifetime={s.Lifetime}, Sequence={s.Sequence}, TargetId={s.TargetId}, ResultHp={s.ResultHp}, Lethal={(lethal ? "true" : "false")}, AliveAtReceive={alive}, ViewAtReceive={viewState}, UnitType={(hasType ? capture.Type.ToString() : "Unknown")}, TypeSource={(hasType ? capture.Source : "None")}, Impactmm={BoltMmText(s.Impact)}, RecvViewmm={(hasView ? BoltMmText(recvView) : "None")}, RecvPosemm={(pose != null ? BoltMmText(pose.position) : "None")}, HDiffmm={hDiff}, VDiffBasemm={vDiff}, ChestTablemm={chestTable}"));
            }
            catch (Exception ex) { DiagBoltFault("Receive", ex); }
        }

        /// <summary>
        /// [진단 — [BOLT-END] Begin] 재생(PlayAttackEffect) 직후 1줄. 어느 갈래(추적/고정)로 시작했는지, 실제로 쓴 가슴 높이와
        /// 표 가슴 높이의 차(ChestDeltamm — F0 뒤에는 0이 정상), 끝점 높이, 그 순간 화면 위치와 끝점의 수평 차를 남긴다.
        /// 개수(End 요약)는 상한과 상관없이 항상 센다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBoltBegin(AttackEffectPresentation s, (ulong, ulong, ulong) key, string outcome,
            TowerBoltTarget boltTarget, UnitType? capturedType)
        {
            try
            {
                if (!IsBoltSnapshot(s)) return;
                _boltDiagReceives.TryGetValue(key, out BoltDiagReceive entry);
                _boltDiagReceives.Remove(key);
                EffectManager manager = EffectManager.Instance;
                TowerBoltBeginInfo info = manager != null ? manager.LastTowerBoltBegin : default;
                bool started = info.IsFor(s.Match, s.Lifetime, s.Sequence);
                bool lethal = s.ResultHp <= 0;

                // 갈래와 사유: "Tracked" / "Untracked(사유)" / 번개를 시작하지 못함(NotStarted — 사유는 outcome 첫 토큰).
                string branch, reason;
                if (!started) { branch = "NotStarted"; reason = BoltOutcomeToken(outcome); }
                else if (info.EndpointState == "Tracked") { branch = "Tracked"; reason = "None"; }
                else
                {
                    branch = "Untracked";
                    string state = info.EndpointState;
                    int open = state.IndexOf('('), close = state.LastIndexOf(')');
                    reason = open >= 0 && close > open ? state.Substring(open + 1, close - open - 1) : state;
                }

                // 재생 순간의 생존: 추적 대상 데이터가 있으면 그것(살아 있어야 추적된다), 없으면 화면 · 도메인에서 다시 찾는다.
                GameObject go;
                string viewState;
                UnitData data = boltTarget.Data;
                if (data != null) { go = boltTarget.View != null ? boltTarget.View.gameObject : null; viewState = "Present"; }
                else data = FindBoltDiagTarget(s.TargetId, out go, out viewState);
                string aliveAtBegin = data == null ? "Unknown" : data.IsAlive ? "true" : "false";

                // 가슴 높이 비교: 실제 사용 값 vs 표 값. 표 값은 "가슴 높이를 고를 때 쓴 종류"(없으면 받는 순간 잡아 둔 종류)로 찾는다.
                UnitType? typeForTable = info.ChestType ?? capturedType;
                bool hasTable = false; float table = 0f;
                if (typeForTable.HasValue && manager != null) hasTable = manager.TryGetTableChestHeight(typeForTable.Value, out table);
                bool chestFromTable = started && info.ChestType.HasValue && hasTable;
                string chestSource = !started ? "None" : chestFromTable ? "Table" : "Fallback";
                string chestUsed = started ? BoltMm(info.ChestHeight).ToString(System.Globalization.CultureInfo.InvariantCulture) : "None";
                string chestTable = hasTable ? BoltMm(table).ToString(System.Globalization.CultureInfo.InvariantCulture) : "None";
                string chestDelta = "None";
                if (started && hasTable)
                {
                    int delta = BoltMm(info.ChestHeight) - BoltMm(table);
                    int absDelta = delta < 0 ? -delta : delta;
                    if (absDelta > _boltDiagMaxAbsChestDeltaMm) _boltDiagMaxAbsChestDeltaMm = absDelta;
                    chestDelta = delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                // 끝점과 "그 순간 화면 위치"의 수평 차(화면 오브젝트가 없으면 None).
                string endY = started && info.HasEndpoint ? BoltMm(info.Endpoint.y).ToString(System.Globalization.CultureInfo.InvariantCulture) : "None";
                string endHDiff = "None";
                if (started && info.HasEndpoint && viewState == "Present" && TryReadBoltViewPosition(go, out Vector3 viewNow))
                {
                    int h = BoltHorizontalMm(info.Endpoint, viewNow);
                    if (h > _boltDiagMaxHDiffMm) _boltDiagMaxHDiffMm = h;
                    endHDiff = h.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                string recvToBegin = entry != null
                    ? Mathf.RoundToInt((float)((Time.realtimeSinceStartupAsDouble - entry.ReceivedAt) * 1000d)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "None";

                // End 요약용 개수(상한과 무관하게 항상 센다).
                if (started)
                {
                    _boltDiagBegun++;
                    if (branch == "Tracked") _boltDiagTracked++;
                    else
                    {
                        _boltDiagUntrackedByReason.TryGetValue(reason, out int n);
                        _boltDiagUntrackedByReason[reason] = n + 1;
                        if (lethal) _boltDiagLethalUntracked++;
                    }
                    if (chestFromTable) _boltDiagChestTable++;
                    else
                    {
                        _boltDiagChestFallback++;
                        if (typeForTable.HasValue) _boltDiagFallbackKnownType++; // 종류를 아는데 0.6m — F0 뒤에는 0이 정상
                    }
                }
                if (!lethal && aliveAtBegin == "false") _boltDiagNonLethalDeadAtBegin++;

                if (entry == null || !entry.Detailed) return;
                GameLog.Dev.Info("Combat", nameof(HitPresentationQueue), "[BOLT-END] Begin",
                    FormattableString.Invariant(
                        $"Diag=TowerBoltEnd, DiagRev=1, Fix=F0, Event=Begin, Role={ServerProjectileDiagnostics.RoleName}, ViewFlipped={(BoltDiagClientFlipped ? "true" : "false")}, Race={s.Race}, Match={s.Match}, Lifetime={s.Lifetime}, Sequence={s.Sequence}, TargetId={s.TargetId}, Lethal={(lethal ? "true" : "false")}, UnitType={(typeForTable.HasValue ? typeForTable.Value.ToString() : "Unknown")}, Branch={branch}, Reason={reason}, AliveAtBegin={aliveAtBegin}, ChestUsedmm={chestUsed}, ChestTablemm={chestTable}, ChestDeltamm={chestDelta}, ChestSource={chestSource}, EndYmm={endY}, EndHDiffmm={endHDiff}, RecvToBeginMs={recvToBegin}"));
            }
            catch (Exception ex) { DiagBoltFault("Begin", ex); }
        }

        /// <summary>[진단] outcome 문자열의 첫 토큰(';' 앞, 공백 · 쉼표 제거) — NotStarted 사유 표기용.</summary>
        private static string BoltOutcomeToken(string outcome)
        {
            if (string.IsNullOrEmpty(outcome)) return "None";
            int cut = outcome.IndexOf(';');
            string token = (cut >= 0 ? outcome.Substring(0, cut) : outcome).Trim();
            return token.Length == 0 ? "None" : token.Replace(",", "").Replace(" ", "");
        }

        /// <summary>
        /// [진단 — [BOLT-END] End] 경기 종료(또는 종료 신호 없는 재초기화) 때 1줄. 번개를 하나도 받지 않았으면 남기지 않는다.
        /// 안 B(F0) 기준 정상: FallbackKnownType=0, Fault 줄 0.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBoltEnd(string reason)
        {
            try
            {
                if (_boltDiagEnded || _boltDiagReceived == 0) return;
                _boltDiagEnded = true;
                string byReason = null;
                foreach (KeyValuePair<string, int> pair in _boltDiagUntrackedByReason)
                    byReason = (byReason == null ? "" : byReason + "|") + pair.Key + ":" + pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                GameLog.Dev.Info("Combat", nameof(HitPresentationQueue), "[BOLT-END] End",
                    FormattableString.Invariant(
                        $"Diag=TowerBoltEnd, DiagRev=1, Fix=F0, Event=End, Role={ServerProjectileDiagnostics.RoleName}, Reason={BoltOutcomeToken(reason)}, Received={_boltDiagReceived}, Begun={_boltDiagBegun}, NotStarted={_boltDiagReceived - _boltDiagBegun}, Tracked={_boltDiagTracked}, UntrackedByReason={byReason ?? "None"}, LethalUntracked={_boltDiagLethalUntracked}, NonLethalDeadAtBegin={_boltDiagNonLethalDeadAtBegin}, ChestSourceTable={_boltDiagChestTable}, ChestSourceFallback={_boltDiagChestFallback}, FallbackKnownType={_boltDiagFallbackKnownType}, TypeUnknownAtReceive={_boltDiagTypeUnknownAtReceive}, MaxAbsChestDeltamm={(_boltDiagMaxAbsChestDeltaMm < 0 ? "None" : _boltDiagMaxAbsChestDeltaMm.ToString(System.Globalization.CultureInfo.InvariantCulture))}, MaxHDiffmm={(_boltDiagMaxHDiffMm < 0 ? "None" : _boltDiagMaxHDiffMm.ToString(System.Globalization.CultureInfo.InvariantCulture))}, Omitted={_boltDiagOmitted}"));
            }
            catch (Exception ex) { DiagBoltFault("End", ex); }
        }

        /// <summary>
        /// [진단 — [BOLT-END] Fault] 진단 · 종류 캡처 중 예외가 나면 경기당 1줄만 남긴다(예외는 다시 던지지 않는다).
        /// 예외 메시지는 쉼표가 섞일 수 있어 넣지 않고 예외 타입 이름만 남긴다(§1.4).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBoltFault(string stage, Exception ex)
        {
            try
            {
                if (_boltDiagFaultLogged) return;
                _boltDiagFaultLogged = true;
                GameLog.Dev.Warn("Combat", nameof(HitPresentationQueue), "[BOLT-END] Fault — 진단 중 예외(화면 동작은 계속된다)",
                    FormattableString.Invariant(
                        $"Diag=TowerBoltEnd, DiagRev=1, Fix=F0, Event=Fault, Role={ServerProjectileDiagnostics.RoleName}, Stage={stage}, Error={(ex != null ? ex.GetType().Name : "None")}"));
            }
            catch (Exception) { /* 진단 기록 실패는 무시한다 — 게임 흐름을 막지 않는다 */ }
        }

        public int AuthoritativeEmits { get; private set; }
        public int AuthoritativeViewUnavailable { get; private set; }
#if UNITY_EDITOR
        private Func<UnitType, Vector3, bool> _authoritativeUnitVfxValidationEmitter;

        /// <summary>Editor 회귀가 실제 방출 성공/실패 경계를 결정적으로 재현하는 전용 seam.</summary>
        public void SetAuthoritativeUnitVfxEmitterForValidation(
            Func<UnitType, Vector3, bool> emitter)
            => _authoritativeUnitVfxValidationEmitter = emitter;

        /// <summary>프레임 대기 없이 transient View 재시도를 검증하는 Editor 전용 seam.</summary>
        public void RetryAuthoritativePresentationForValidation(double now)
            => RetryAuthoritativePresentations(now);

        // [제안 A 검증 seam] Edit Mode fixture는 실제 텍스트 프리팹·VFX 프리셋·코루틴을 쓰지 않는다.
        //   이 셋이 설정되면 "그리기 직전" 호출만 가로채 위치·값을 기록한다. 위치를 고르는 판단 코드는 실제와 같다.
        private Func<EntityDamagedEvent, Vector3, bool> _projectileTextDrawForValidation;
        private Action<UnitType, Vector3> _projectileHitVfxForValidation;
        private Action<GameObject> _projectilePunchForValidation;

        /// <summary>
        /// [Editor 전용] 피해 숫자·피격 VFX·펀치의 최종 그리기 호출을 가로챈다(fixture 전용). null을 넣으면 실제 경로로 돌아간다.
        /// </summary>
        /// <param name="text">숫자 그리기(이벤트, 표시 위치) → 성공 여부.</param>
        /// <param name="hitVfx">유닛 피격 VFX(유닛 종류, 위치).</param>
        /// <param name="punch">스케일 펀치 대상.</param>
        public void SetPresentationChannelsForValidation(Func<EntityDamagedEvent, Vector3, bool> text,
            Action<UnitType, Vector3> hitVfx, Action<GameObject> punch)
        {
            _projectileTextDrawForValidation = text;
            _projectileHitVfxForValidation = hitVfx;
            _projectilePunchForValidation = punch;
        }

        /// <summary>[Editor 전용] Update를 기다리지 않고 투사체 보관함 타임아웃 검사를 지정 시각으로 돌린다.</summary>
        /// <param name="now">실제 시계(Time.realtimeSinceStartupAsDouble 기준) 값.</param>
        public void TickProjectileHoldForValidation(double now) => TickProjectileHold(now);
#endif

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
            BuildingPlacementUseCase buildingPlacement,
            IPresentationPoseProvider presentationPoseProvider)
        {
            _subscriptions.Clear();
            if (_attackEffectPending.Count != 0) CloseAttackEffects("Reinitialize");
            // [2026-11-10 [BOLT-END]] 경기 종료 신호 없이 재초기화되면(보류 항목도 없어 위 Close가 안 불린 경우) 지난 경기의 End 줄을
            //   여기서 남긴다. 이미 End를 남겼거나 번개를 하나도 받지 않았으면 아무것도 하지 않는다.
            DiagBoltEnd("Reinitialize");
            _boltTargetTypes.Clear();
            ResetBoltDiagnostics();
            _attackEffectKeys.Clear(); _attackEffectPending.Clear(); _attackEffectReceivedAt.Clear(); _attackEffectsClosed = false;
            _attackEffectFailures = _attackEffectOverflow = 0;
            _attackClockFaultReported = false;
            AttackEffectIdentity.BeginLocalMatch();
            GameEvents.OnGameEnd.Subscribe(_ => CloseAttackEffects("GameEnd")).AddTo(_subscriptions);
            GameEvents.OnAttackEffectConfirmed.Subscribe(s =>
            { if (!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer) ReceiveAttackEffect(s); }).AddTo(_subscriptions);
            GameEvents.OnAttackEffectReceived.Subscribe(ReceiveAttackEffect).AddTo(_subscriptions);
            _hpTextSpawner = hpTextSpawner;
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
            _unitSpawn = unitSpawn;
            _buildingPlacement = buildingPlacement;
            _presentationPoseProvider = presentationPoseProvider;

            // 재초기화(맵 재로드) 대비 — 이전 보류 항목 정리.
            _pendingByAttacker.Clear();
            _projectileHold.Clear();
            _projectileReleased.Clear();
            _projectileReleasePoints.Clear();
            _projectileReleasePointsExpired.Clear();
            _projectileTextPlayed = _projectileTextFallback = _projectileTextMissing = _projectileTextMissingDead = 0;
            _projectileOwnerNotReadyEvents = 0;
            _exactRendezvous.Clear();
            _releasedExact.Clear();
            _confirmedUnitViews.Clear();
            _confirmedBuildingViews.Clear();
            _pendingAuthoritative.Clear();
            _completedAuthoritative.Clear();
            _preparedAuthoritative.Clear();
            _visualAuthoritativeResults.Clear();
            AuthoritativeEmits = AuthoritativeViewUnavailable = 0;
            UnitAttackResultPresentationShadowBridge.Coordinator.BundleReady -= OnAuthoritativeBundle;
            UnitAttackResultPresentationShadowBridge.Coordinator.BundleReady += OnAuthoritativeBundle;

            // 피격 이벤트 → 보류 큐 적재.
            // 명시적 CompositeDisposable: Edit Mode 반복 검증에서도 즉시 구독 해제.
            GameEvents.OnEntityDamaged
                .Subscribe(OnEntityDamaged)
                .AddTo(_subscriptions);

            // 공격자의 로컬 타격 프레임 신호 → 해당 공격자 큐에서 1건 방출.
            GameEvents.OnLocalAttackHit
                .Subscribe(OnLocalAttackHit)
                .AddTo(_subscriptions);

            // UnitView가 타겟 변경·사망·Stop으로 닫은 exact 회차를 함께 폐기한다.
            // 결과가 아직 없는 marker 신호도 제거해 늦은 구회차 결과가 timeout으로
            // 다시 나타나지 않게 한다. 서버 피해나 HP에는 관여하지 않는다.
            GameEvents.OnLocalAttackPresentationScopeRetired
                .Subscribe(OnPresentationScopeRetired)
                .AddTo(_subscriptions);

            // 타겟 사망 → 그 타겟을 겨눈 잔여 항목 즉시 방출(안전망 ⓑ).
            // ※ OnUnitDied는 "공격자 사망"(안전망 ⓓ) 처리도 겸한다 — OnUnitDied 핸들러 참조.
            GameEvents.OnUnitDied
                .Subscribe(OnUnitDied)
                .AddTo(_subscriptions);
            GameEvents.OnBuildingDied
                .Subscribe(OnBuildingDied)
                .AddTo(_subscriptions);

            // 공격자 전투 중단 → 그 공격자의 잔여 보류 항목 즉시 방출(안전망 ⓓ).
            //   멀티: OnNetworkCombatStopped(서버 StopCombatClientRpc 경유), 싱글: OnCombatStopped.
            //
            // ※ 안전성 근거: StopCombat 신호는 "진행 중 사이클의 타격 프레임"보다 먼저 도착할 수 있으나,
            //   그 시점엔 해당 사이클의 데미지가 아직 적용되지 않아(=큐가 비어 있어) flush가 no-op이므로 안전하다.
            //   실제로 위험한 케이스는 "이미 등록된 항목이 있는데 다음 OnLocalAttackHit이 영영 오지 않는" 경우이며,
            //   이 구독이 바로 그 케이스를 커버한다(타임아웃까지 기다리지 않고 즉시 방출).
            GameEvents.OnNetworkCombatStopped
                .Subscribe(e => FlushAttacker(e.UnitId))
                .AddTo(_subscriptions);
            GameEvents.OnCombatStopped
                .Subscribe(unitId => FlushAttacker(unitId))
                .AddTo(_subscriptions);
        }

        // ====================================================================
        // 이벤트 핸들러 — 적재
        // ====================================================================

        /// <summary>
        /// 피격 이벤트 수신. 공격자별 큐에 보류하거나, 조건에 따라 즉시 방출한다.
        /// </summary>
        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            // 공격자 조회 없이 writer/RPC가 전달한 파도 표현 소유권만 따른다. HP 적용은 이미 끝났다.
            if (evt.WavePresentationKey.IsValid && _waveOwnerReady?.Invoke() == true) return;
            if (evt.Entity == null) return;

            // [2026-10-20 서버 권위 투사체] 투사체 착탄 피해는 화면상 착탄 순간까지 보관했다가 1회 방출한다(Q7).
            //   이미 화면 착탄 신호가 먼저 왔거나 보관함이 가득 찼으면 지금 방출한다.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // [단계 B 진단] 번호표가 있는데 소비자가 준비되지 않은 경우(아래 기존 경로로 흘러간다)를 센다. 판단은 바꾸지 않는다.
            if (evt.ProjectilePresentationKey.IsValid && _projectileOwnerReady?.Invoke() != true)
                _projectileOwnerNotReadyEvents++;
#endif
            if (evt.ProjectilePresentationKey.IsValid && _projectileOwnerReady?.Invoke() == true)
            {
                ObserveLegacy(evt, LegacyPresentationObservationKind.Enqueued);
                // [제안 A] 지금(HP 이벤트 도착 순간) 대상 화면 위치를 기억해 둔다. 화면 착탄 지점 없이 방출되는 경로
                //   (타임아웃·보관 상한 초과)에서 대상이 이미 사라졌을 때의 대체 위치다. 대상이 없으면 기억하지 않는다.
                var held = new ProjectileHeldHit(evt, TryReadTargetPosition(evt, out Vector3 remembered), remembered);
                if (_projectileHold.HoldOrRelease(evt.ProjectilePresentationKey, held, Time.realtimeSinceStartupAsDouble))
                {
                    // 바로 방출: 화면 착탄 신호가 먼저 왔으면(순서 역전) 그때 기억한 착탄 지점을 대체 위치로 쓴다.
                    bool hasPoint = _projectileReleasePoints.TryGetValue(evt.ProjectilePresentationKey, out ReleasePoint early);
                    EmitProjectile(held, LegacyPresentationObservationKind.EmittedImmediate, hasPoint, early.Point);
                }
                return;
            }
            ObserveFoxLegacyPresentation(evt, "received", "not-dispatched");
            if (evt.AttackerIsUnit && UnitAttackResultPresentationShadowBridge.OwnsAttacker(evt.AttackerId))
            {
                var victims = evt.IsUnit ? _confirmedUnitViews : _confirmedBuildingViews;
                if (victims.Count < 32768 || victims.ContainsKey(evt.Entity.Id)) victims[evt.Entity.Id] = evt.Entity;
                else RecordStructuralFailure(evt, LegacyPresentationObservationKind.StructuralCapacityExceeded);
                // Supported 경기는 확정 bundle만 아래 단일 소비자를 호출한다.
                // 이 경로에서 FIFO/marker/tracer/사망 flush로 넘어가면 중복 방출된다.
                return;
            }

            // 파도 등 이동형 AoE(규칙 26): 공격자 타격 프레임에 종속하지 않고 "닿는 시점"에 즉시 방출.
            //   파도 피해는 공격자 스윙(OnLocalAttackHit)보다 한참 뒤(파도 이동 중)에 발생하므로,
            //   공격자 큐에 보류하면 다음 사이클/타임아웃까지 HP 텍스트가 지연된다 → 즉시 방출로 우회한다.
            //   (VFX/HP텍스트/펀치는 Emit이 그대로 처리하며, 타워 VFX 경로는 타지 않는다 — 공격자가 유닛이므로.)
            if (evt.ImmediatePresentation)
            {
                ObserveLegacy(evt, LegacyPresentationObservationKind.Enqueued);
                Emit(evt, LegacyPresentationObservationKind.EmittedImmediate);
                return;
            }

            // ⓒ 공격자가 유닛이 아니면(타워) 타격 애니메이션 프레임이 없다 → 보류 없이 즉시 방출.
            if (!evt.AttackerIsUnit)
            {
                // 타워 발사 이펙트는 이 HP 이벤트에서 내지 않는다. 발사 이펙트를 내는 곳은 두 곳뿐이다:
                //   ① 즉발 타워(번개 등) = 서버가 확정한 공격 스냅샷(아래 EmitAttackEffects → EffectManager.PlayAttackEffect)
                //   ② 투사체 타워(인간 대포) = 서버 투사체 발사 기록(ProjectilePresentationPresenter → PlayServerTowerLaunchCue)
                //   여기서는 HP 텍스트 · 피격 VFX 같은 피격 표현만 보류 없이 즉시 방출한다.
                //   (2026-11-03 정리 — 이 자리의 옛 「타워 발사 연출(3-1)」 설명은 꺼진 옛 설계라 지웠다. Plan RM-5.)

                ObserveLegacy(evt, LegacyPresentationObservationKind.Enqueued);
                Emit(evt, LegacyPresentationObservationKind.EmittedImmediate);
                return;
            }

            // ⓒ 공격자 GameObject가 없으면(스폰 전/파괴 등) 로컬 OnAttackHit 신호를 기대할 수 없다 → 즉시 방출.
            GameObject attackerGo = _unitFactory != null ? _unitFactory.GetUnitObject(evt.AttackerId) : null;
            if (attackerGo == null)
            {
                ObserveLegacy(evt, LegacyPresentationObservationKind.Enqueued);
                Emit(evt, LegacyPresentationObservationKind.EmittedImmediate);
                return;
            }

            // 관측은 실제 표현 저장 방식과 무관하게 결과별 정규 키로 한 번 남긴다.
            float timeout = ResolveTimeout(evt.AttackerId);
            ObserveLegacy(evt, LegacyPresentationObservationKind.Enqueued);

            if (evt.PresentationResultKey.IsValid)
            {
                // 멀티플레이 정규 경로. 결과가 marker보다 먼저면 exact scope에 보류하고,
                // marker가 먼저였다면 같은 scope 신호를 찾아 지금 즉시 방출한다. 다른
                // sequence/hitIndex는 시간상 가깝더라도 절대 후보가 아니다.
                _releasedExact.Clear();
                AttackPresentationRendezvousStatus status = _exactRendezvous.ObserveResult(
                    evt.AttackerId,
                    evt.PresentationResultKey,
                    evt,
                    Time.realtimeSinceStartupAsDouble,
                    timeout,
                    _releasedExact);
                if (status == AttackPresentationRendezvousStatus.Released)
                    EmitReleasedExact(LegacyPresentationObservationKind.EmittedMarker);
                else if (status == AttackPresentationRendezvousStatus.Invalid
                    || status == AttackPresentationRendezvousStatus.CapacityExceeded)
                {
                    // 손상된 정규 입력을 공격자 FIFO로 우회하면 다음 회차 결과를 잘못
                    // 소비한다. 표현 유실만 막는 timeout 복구로 즉시 격리하고 C3 FAIL을 남긴다.
                    RecordStructuralFailure(
                        evt,
                        status == AttackPresentationRendezvousStatus.CapacityExceeded
                            ? LegacyPresentationObservationKind.StructuralCapacityExceeded
                            : LegacyPresentationObservationKind.StructuralInvalid);
                    _releasedExact.Clear();
                    Emit(evt, LegacyPresentationObservationKind.EmittedTimeout);
                }
                return;
            }

            // 싱글플레이/구형 특수 경로만 공격자 FIFO를 보존한다.
            Queue<PendingHit> queue = GetOrCreateQueue(evt.AttackerId);
            queue.Enqueue(new PendingHit(evt, Time.time, timeout));
        }

        /// <summary>
        /// 공격자의 로컬 타격 프레임 신호 수신. 공격자의 "타격 프레임 수"에 따라
        /// 큐에서 1건만(다중 히트) 또는 전부(단일 히트) 방출한다.
        /// </summary>
        private void OnLocalAttackHit(LocalAttackHitPresentationEvent hit)
        {
            int attackerId = hit.AttackerId;
            if (UnitAttackResultPresentationShadowBridge.OwnsAttacker(attackerId)) return;
            if (hit.HasPresentationScope)
            {
                _releasedExact.Clear();
                AttackPresentationRendezvousStatus status = _exactRendezvous.ObserveSignal(
                    attackerId,
                    hit.PresentationScope,
                    Time.realtimeSinceStartupAsDouble,
                    _releasedExact);
                if (status == AttackPresentationRendezvousStatus.Released)
                    EmitReleasedExact(LegacyPresentationObservationKind.EmittedMarker);
                else if (status == AttackPresentationRendezvousStatus.Invalid
                    || status == AttackPresentationRendezvousStatus.CapacityExceeded)
                {
                    RecordStructuralFailure(
                        hit,
                        status == AttackPresentationRendezvousStatus.CapacityExceeded
                            ? LegacyPresentationObservationKind.StructuralCapacityExceeded
                            : LegacyPresentationObservationKind.StructuralInvalid);
                }

                // exact scope가 있는 멀티플레이 신호는 결과가 아직 없어도 여기서 끝낸다.
                // 공격자 FIFO로 떨어뜨리면 다음 회차의 오래된 결과를 다시 소비하게 된다.
                return;
            }

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
            // [2026-10-20] 화면 착탄 신호가 끝내 오지 않은 투사체 피해 표현은 상한 시간 뒤 강제 방출한다(표시 유실 방지).
            //   이 일과 착탄 지점 기억 정리는 TickProjectileHold가 함께 한다.
            TickProjectileHold(Time.realtimeSinceStartupAsDouble);
            EmitAttackEffects();
            RetryAuthoritativePresentations(Time.realtimeSinceStartupAsDouble);
            _releasedExact.Clear();
            _exactRendezvous.CollectExpired(
                Time.realtimeSinceStartupAsDouble,
                _releasedExact);
            EmitReleasedExact(LegacyPresentationObservationKind.EmittedTimeout);

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
                    Emit(head.Event, LegacyPresentationObservationKind.EmittedTimeout);
                }
            }
        }

        // ====================================================================
        // 방출 — 실제 표현 재생
        // ====================================================================

        /// <summary>
        /// 피격 표현을 실제로 재생한다: ① HP 텍스트 ② 피격 VFX ③ 타격 반응(스케일 펀치).
        /// </summary>
        private void OnAuthoritativeBundle(AttackResultPresentationInput[] bundle)
        {
            if (!UnitAttackResultPresentationShadowBridge.UsesAuthoritativeEmitter) return;
            if (TryEmitAuthoritativeBundle(bundle, out bool terminal) || terminal) return;
            AttackPresentationScope scope = AttackPresentationScope.FromResultKey(bundle[0].Key);
            if (!_pendingAuthoritative.ContainsKey(scope))
                _pendingAuthoritative.Add(scope,
                    new PendingAuthoritativeBundle(
                        (AttackResultPresentationInput[])bundle.Clone(),
                        Time.realtimeSinceStartupAsDouble));
        }

        private void RetryAuthoritativePresentations(double now)
        {
            if (_pendingAuthoritative.Count == 0) return;
            _completedAuthoritative.Clear();
            foreach (KeyValuePair<AttackPresentationScope, PendingAuthoritativeBundle> pair
                in _pendingAuthoritative)
            {
                PendingAuthoritativeBundle pending = pair.Value;
                if (now - pending.FirstAttemptLocalTime
                    > AuthoritativeViewRetrySeconds)
                {
                    // 기존 catch-up 경계를 넘긴 뒤 View가 생겨도 오래된 공격을 화면에
                    // 되살리지 않는다. 묶음 전체를 미표시 실패로 닫는다.
                    for (int i = 0; i < pending.Results.Length; i++)
                    {
                        AttackResultPresentationInput result = pending.Results[i];
                        if (!IsVisualResult(result)) continue;
                        AuthoritativeViewUnavailable++;
                        UnitAttackResultPresentationShadowBridge.RecordPresentationFailure(
                            ClassifyAuthoritativeFailure(result));
                    }
                    _completedAuthoritative.Add(pair.Key);
                    continue;
                }
                if (TryEmitAuthoritativeBundle(pending.Results, out bool terminal) || terminal)
                {
                    _completedAuthoritative.Add(pair.Key);
                    continue;
                }
            }
            for (int i = 0; i < _completedAuthoritative.Count; i++)
                _pendingAuthoritative.Remove(_completedAuthoritative[i]);
            _completedAuthoritative.Clear();
        }

        /// <summary>
        /// C3 확정 결과가 보존한 위치·타입·팀·결과 HP만으로 필수 연출을 준비한 뒤 exact key를
        /// 소비한다. 살아 있는 View는 같은 Domain 인스턴스임이 확인될 때 선택적인 punch에만
        /// 사용한다. 따라서 사망·Despawn·ID 재사용은 HP 텍스트/VFX를 막지 않는다.
        /// </summary>
        private bool TryEmitAuthoritativeBundle(
            AttackResultPresentationInput[] bundle,
            out bool terminal)
        {
            terminal = false;
            if (bundle == null || bundle.Length == 0)
            {
                terminal = true;
                return false;
            }
            _preparedAuthoritative.Clear();
            _visualAuthoritativeResults.Clear();
            for (int i = 0; i < bundle.Length; i++)
            {
                AttackResultPresentationInput result = bundle[i];
                if (!IsVisualResult(result)) continue;
                if (!TryPrepareAuthoritative(result,
                        out PreparedAuthoritativePresentation prepared))
                {
                    _preparedAuthoritative.Clear();
                    _visualAuthoritativeResults.Clear();
                    return false;
                }
                _preparedAuthoritative.Add(prepared);
                _visualAuthoritativeResults.Add(result);
            }

            // Miss/취소만 든 완결 묶음은 화면 작업도 key 소비도 필요 없다.
            if (_visualAuthoritativeResults.Count == 0) return true;
            if (!UnitAttackResultPresentationShadowBridge.TryConsumePresentationBundle(
                    _visualAuthoritativeResults.ToArray()))
            {
                terminal = true;
                return false;
            }

            bool allDisplayed = true;
            for (int i = 0; i < _preparedAuthoritative.Count; i++)
                allDisplayed &= EmitPreparedAuthoritative(_preparedAuthoritative[i]);
            _preparedAuthoritative.Clear();
            _visualAuthoritativeResults.Clear();
            if (!allDisplayed)
            {
                // readiness 선검사 뒤 같은 호출 안에서 Unity 객체가 파괴된 극단적 경주다.
                // 이미 나온 다른 피해자 연출을 되돌릴 수 없으므로 재시도해 중복을 만들지 않고
                // 실패를 명시한다. 정상 경로에서는 각 준비 항목마다 실제 채널 하나가 보장된다.
                terminal = true;
                return false;
            }
            return true;
        }

        private bool TryPrepareAuthoritative(
            AttackResultPresentationInput result,
            out PreparedAuthoritativePresentation prepared)
        {
            prepared = default;
            bool isUnit = result.Key.VictimKind == 1;
            if (!result.HasImpactPosition || !result.HasPresentationSnapshot)
                return false;

            if (!Enum.IsDefined(typeof(TeamId), result.VictimTeam))
                return false;
            if (isUnit && !Enum.IsDefined(typeof(UnitType), result.VictimPresentationType))
                return false;

            // 필수 연출 위치의 원본은 현재 View가 아니라 서버가 타격 결과에 고정한 위치다.
            // View는 결과 도착 전에 사라지거나 같은 ID로 교체될 수 있으므로 위치 권위가 될 수 없다.
            Vector3 displayPosition = new Vector3(
                (float)result.ImpactPosition.X,
                0f,
                (float)result.ImpactPosition.Z);

            // 서버의 ImpactPosition은 모든 참가자가 공유하는 Blue 기준 canonical 좌표다.
            // Red pure Client에서는 유닛의 VisualRoot만 ViewConverter를 통해 맵 중심 기준으로
            // 반전되어 보이므로, 화면에 그리는 HP 텍스트와 피격 VFX도 이 C3 표현 경계에서
            // 정확히 한 번 같은 변환을 적용해야 유닛 위에 겹친다.
            //
            // Host/Server는 canonical Simulation Root와 같은 위치를 사용하고, Blue Client는
            // ViewConverter가 비반전 상태이므로 기존 좌표를 그대로 유지한다. 또한 싱글플레이는
            // 네트워크 경기 자체가 아니므로 이 분기에 들어오지 않는다. 이렇게 역할까지 함께
            // 확인하면 이미 view 좌표인 값을 실수로 두 번 반전하는 문제를 막을 수 있다.
            if (NetworkContext.IsNetworkActive
                && !NetworkContext.IsNetworkServer
                && ViewConverter.IsFlipped)
            {
                displayPosition = ViewConverter.ToView(displayPosition);
            }
            TeamId victimTeam = (TeamId)result.VictimTeam;
            UnitType victimUnitType = isUnit
                ? (UnitType)result.VictimPresentationType
                : default;

            bool canShowText = _hpTextSpawner != null
                && _hpTextSpawner.IsReadyForDamagePresentation;
            EffectManager effects = EffectManager.Instance;
            bool canShowUnitVfx = false;
            if (isUnit)
            {
#if UNITY_EDITOR
                canShowUnitVfx = _authoritativeUnitVfxValidationEmitter != null
                    || (effects != null && effects.CanPlayUnitHit(victimUnitType));
#else
                canShowUnitVfx = effects != null && effects.CanPlayUnitHit(victimUnitType);
#endif
            }

            // HP 텍스트 또는 유닛 피격 VFX 중 적어도 하나가 준비돼야 결과를 소비한다.
            // punch는 살아 있는 View가 없으면 생략 가능한 보조 연출이므로 단독 성공 조건이 아니다.
            if (!canShowText && !canShowUnitVfx) return false;

            // OnEntityDamaged에서 기억한 객체와 현재 등록 객체가 정확히 같은 경우에만 View를 사용한다.
            // ID만 같고 객체가 바뀐 경우(사망 직후 ID 재사용)에 새 유닛을 때리는 잘못된 punch를 막는다.
            Dictionary<int, IDamageable> victims = isUnit
                ? _confirmedUnitViews : _confirmedBuildingViews;
            victims.TryGetValue(result.Key.VictimId, out IDamageable entity);
            IDamageable currentEntity = isUnit
                ? (IDamageable)_unitSpawn?.GetUnit(result.Key.VictimId)
                : _buildingPlacement?.GetBuilding(result.Key.VictimId);
            Transform targetPresentation = null;
            if (entity != null && ReferenceEquals(entity, currentEntity))
            {
                targetPresentation = isUnit
                    ? _presentationPoseProvider?.GetUnitTransform(result.Key.VictimId)
                    : _presentationPoseProvider?.GetBuildingTransform(result.Key.VictimId);
                GameObject targetGo = isUnit
                    ? (_unitFactory != null ? _unitFactory.GetUnitObject(result.Key.VictimId) : null)
                    : (_buildingFactory != null ? _buildingFactory.GetBuildingObject(result.Key.VictimId) : null);
                if (targetPresentation == null && targetGo != null)
                    targetPresentation = targetGo.transform;
            }

            bool canPunch = targetPresentation != null;
            if (targetPresentation != null)
            {
                bool spatialMatch = UnitAttackResultPresentationShadowBridge
                    .RecordPresentationSpatialSample(
                    displayPosition.x,
                    displayPosition.z,
                    targetPresentation.position.x,
                    targetPresentation.position.z);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (!spatialMatch)
                {
                    UnitAttackShadowObserver.ObservePresentationSpatialMismatch(
                        result.Key,
                        displayPosition,
                        targetPresentation.position,
                        ViewConverter.IsFlipped);
                }
#endif
            }
            prepared = new PreparedAuthoritativePresentation(
                result, targetPresentation, displayPosition, victimTeam, victimUnitType,
                isUnit, canShowText, canShowUnitVfx, canPunch);
            return true;
        }

        private bool EmitPreparedAuthoritative(PreparedAuthoritativePresentation prepared)
        {
            AttackResultPresentationInput result = prepared.Result;
            EffectManager effects = EffectManager.Instance;
            bool displayed = false;
            bool unitVfxEmitted = false;
            if (prepared.CanShowText)
                displayed |= _hpTextSpawner.TryShowDamageAt(
                    result.ResultingHp,
                    prepared.VictimTeam,
                    prepared.DisplayPosition);
            if (prepared.CanShowUnitVfx)
            {
#if UNITY_EDITOR
                unitVfxEmitted = _authoritativeUnitVfxValidationEmitter != null
                    ? _authoritativeUnitVfxValidationEmitter(prepared.VictimUnitType, prepared.DisplayPosition)
                    : effects != null
                        && effects.TryPlayUnitHit(prepared.VictimUnitType, prepared.DisplayPosition);
#else
                unitVfxEmitted = effects != null
                    && effects.TryPlayUnitHit(prepared.VictimUnitType, prepared.DisplayPosition);
#endif
                displayed |= unitVfxEmitted;
            }
            if (prepared.IsUnitVictim)
                UnitAttackResultPresentationShadowBridge.RecordHitVfxOutcome(
                    prepared.CanShowUnitVfx, unitVfxEmitted);
            if (prepared.CanPunch)
            {
                HitReactionPunch.Play(prepared.TargetPresentation.gameObject);
            }
            else
            {
                UnitAttackResultPresentationShadowBridge.RecordOptionalViewSkipped();
            }

            if (prepared.CanShowUnitVfx && !unitVfxEmitted)
            {
                AuthoritativeViewUnavailable++;
                UnitAttackResultPresentationShadowBridge.RecordPresentationFailure(
                    AttackPresentationDispatchFailure.ChannelEmissionFailed);
                return false;
            }

            if (displayed)
            {
                AuthoritativeEmits++;
                UnitAttackResultPresentationShadowBridge.RecordPresentationDispatch(result, true);
                return true;
            }

            // 사전 검사 뒤 같은 호출 안에서 실제 채널이 실패한 극단적 경주다.
            AuthoritativeViewUnavailable++;
            UnitAttackResultPresentationShadowBridge.RecordPresentationFailure(
                AttackPresentationDispatchFailure.ChannelEmissionFailed);
            return false;
        }

        private static AttackPresentationDispatchFailure ClassifyAuthoritativeFailure(
            AttackResultPresentationInput result)
        {
            bool isUnit = result.Key.VictimKind == 1;
            if (!result.HasImpactPosition || !result.HasPresentationSnapshot
                || !Enum.IsDefined(typeof(TeamId), result.VictimTeam)
                || (isUnit && !Enum.IsDefined(typeof(UnitType), result.VictimPresentationType)))
                return AttackPresentationDispatchFailure.SnapshotInvalid;
            return AttackPresentationDispatchFailure.PresenterUnavailable;
        }

        private static bool IsVisualResult(AttackResultPresentationInput result)
            => result.IsVisualResult;

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 이벤트 구독 수명을 명시적으로 종료한다. 일반 런타임에서는 OnDestroy가 호출하고,
        /// Edit Mode 회귀 픽스처는 DestroyImmediate 전에 직접 호출한다.
        /// </summary>
        public void Dispose()
        {
            CloseAttackEffects("Dispose"); _attackEffectKeys.Clear();
            UnitAttackResultPresentationShadowBridge.Coordinator.BundleReady -= OnAuthoritativeBundle;
            _subscriptions.Dispose();
        }

        private void Emit(
            EntityDamagedEvent evt,
            LegacyPresentationObservationKind observationKind =
                LegacyPresentationObservationKind.EmittedMarker,
            bool observeLegacy = true)
        {
            if (evt.Entity == null) return;

            // C3 read-only seam. 이 호출은 기존 효과 방출보다 먼저 관측만 남기며 효과 의존성이 없다.
            if (observeLegacy) ObserveLegacy(evt, observationKind);

            // ① HP 텍스트 — 스포너에 위임(풀/팀 색상 로직 재사용). 표시의 유일한 진입점.
            bool textDisplayed = _hpTextSpawner != null && _hpTextSpawner.TryShowDamage(evt);
            ObserveFoxLegacyPresentation(evt, "emitted",
                textDisplayed ? "text-played" : "text-missing");

            // [제안 A] 타겟 조회와 ②·③은 아래 두 헬퍼가 한다(투사체 보관 항목 방출도 같은 코드를 쓰게).
            Transform targetPresentation = ResolveTargetPresentation(evt);
            PlayHitReaction(evt, targetPresentation, false, default);
        }

        /// <summary>
        /// [2026-10-21 제안 A] 투사체 보관함에서 꺼낸 피격 표현 1건을 방출한다(숫자 → 피격 VFX → 펀치 순서는 Emit과 같다).
        ///
        /// 숫자 위치를 고르는 규칙(초급자 설명):
        ///   1) 대상 화면 오브젝트가 살아 있으면 → 지금 대상 위치(기존과 같다. 싱글·Host 대부분이 여기).
        ///   2) 대상이 이미 사라졌으면(사망 디스폰·건물 파괴) → 대체 위치.
        ///      · 화면 착탄 지점이 있으면 그 지점(착탄 방출·순서 역전 즉시 방출).
        ///      · 없으면(타임아웃·상한 초과) HP 이벤트가 왔을 때 기억해 둔 대상 위치.
        ///   3) 대체 위치도 없으면 숫자를 내지 못한다(missing으로 센다 — 조용히 사라지지 않게).
        /// 숫자 값은 언제나 이벤트의 남은 HP(evt.CurrentHp)다 — 사망타면 "0".
        /// 펀치는 흔들 대상이 없으면 생략한다. 이 함수는 HP·서버 판정을 바꾸지 않는다(표현 전용).
        /// </summary>
        private void EmitProjectile(ProjectileHeldHit held, LegacyPresentationObservationKind kind,
            bool hasImpactPoint, Vector3 impactPoint)
        {
            EntityDamagedEvent evt = held.Event;
            if (evt.Entity == null) return;
            ObserveLegacy(evt, kind);

            Transform targetPresentation = ResolveTargetPresentation(evt);
            bool targetAvailable = targetPresentation != null && targetPresentation.position != Vector3.zero;
            bool hasFallback = hasImpactPoint && IsFinite(impactPoint);
            Vector3 fallback = impactPoint;
            if (!hasFallback && held.HasRememberedPosition)
            {
                hasFallback = true;
                fallback = held.RememberedPosition;
            }

            // ① 숫자. 살아 있는 대상이면 그 위치(= 기존 TryShowDamage와 같은 pose 제공자·같은 zero 판정), 아니면 대체 위치.
            bool shown;
            if (targetAvailable)
            {
                shown = DrawDamageText(evt, targetPresentation.position);
                if (shown) _projectileTextPlayed++;
            }
            else
            {
                shown = hasFallback && DrawDamageText(evt, fallback);
                if (shown) _projectileTextFallback++;
            }
            if (!shown)
            {
                _projectileTextMissing++;
                if (!targetAvailable) _projectileTextMissingDead++;
            }
            ObserveFoxLegacyPresentation(evt, "emitted", shown ? "text-played" : "text-missing");

            // ②·③ 피격 VFX(대상이 없으면 같은 대체 위치) · 펀치(대상이 없으면 생략).
            PlayHitReaction(evt, targetPresentation, hasFallback, fallback);

            // ④ [2026-10-26 수정안 (a)] 이 번호표가 화면에 나왔음을 알린다 — 미뤄 둔 사망 표현이 지금(숫자와 같은 순간) 진행된다.
            //   숫자·VFX를 다 낸 뒤에 알리므로 "0" 숫자와 사망 이펙트가 같은 프레임에 나온다.
            NotifyProjectileHitShown(evt.ProjectilePresentationKey, evt);
        }

        // 숫자 그리기 단일 지점. 실제로는 스포너의 위치 지정 경로(TryShowDamageAt — C3가 쓰는 경로)를 쓴다.
        private bool DrawDamageText(EntityDamagedEvent evt, Vector3 position)
        {
#if UNITY_EDITOR
            if (_projectileTextDrawForValidation != null) return _projectileTextDrawForValidation(evt, position);
#endif
            return _hpTextSpawner != null && _hpTextSpawner.TryShowDamageAt(evt, position);
        }

        // 피격 대상의 화면 Transform(VFX 위치 + 펀치 대상). 파괴됐으면 null(Unity null 비교).
        private Transform ResolveTargetPresentation(EntityDamagedEvent evt)
        {
            if (evt.Entity == null) return null;
            GameObject targetGo = evt.IsUnit
                ? (_unitFactory != null ? _unitFactory.GetUnitObject(evt.Entity.Id) : null)
                : (_buildingFactory != null ? _buildingFactory.GetBuildingObject(evt.Entity.Id) : null);
            Transform targetPresentation = evt.IsUnit
                ? _presentationPoseProvider?.GetUnitTransform(evt.Entity.Id)
                : _presentationPoseProvider?.GetBuildingTransform(evt.Entity.Id);
            if (targetPresentation == null && targetGo != null)
                targetPresentation = targetGo.transform;
            return targetPresentation;
        }

        // 지금 대상 화면 위치를 읽는다(HP 이벤트 시점에 "기억할 위치"용). 없거나 zero면 false(FloatingHpTextSpawner와 같은 규칙).
        private bool TryReadTargetPosition(EntityDamagedEvent evt, out Vector3 position)
        {
            Transform target = ResolveTargetPresentation(evt);
            position = target != null ? target.position : default;
            return target != null && position != Vector3.zero;
        }

        /// <summary>
        /// ② 피격 VFX ③ 스케일 펀치. targetPresentation이 있으면 기존과 같다.
        /// 없을 때 hasFallback이면 유닛 피격 VFX만 대체 위치에서 낸다(투사체 사망타 전용 — 일반 Emit은 false를 넘긴다).
        /// </summary>
        private void PlayHitReaction(EntityDamagedEvent evt, Transform targetPresentation, bool hasFallback, Vector3 fallback)
        {
            // ② 피격 VFX — 유닛 타겟만 재생(PlayUnitHit은 UnitType 기반). 프리셋 미설정이면 내부에서 조용히 스킵.
            //    (건물 피격 VFX는 이번 범위 아님 — Phase 2는 유닛 피격 VFX만 다룬다.)
            //    대상이 사라졌어도 evt.Entity(UnitData)는 남아 있어 유닛 종류를 알 수 있다.
            if (evt.IsUnit && evt.Entity is UnitData targetUnit && (targetPresentation != null || hasFallback))
                PlayUnitHitVfx(targetUnit.Type, targetPresentation != null ? targetPresentation.position : fallback);

            // ③ 타격 반응 — 유닛/건물 GameObject에 짧은 스케일 펀치(원 스케일 캐시 후 복원 보장). 대상이 없으면 생략.
            if (targetPresentation != null)
            {
#if UNITY_EDITOR
                if (_projectilePunchForValidation != null) { _projectilePunchForValidation(targetPresentation.gameObject); return; }
#endif
                HitReactionPunch.Play(targetPresentation.gameObject);
            }
        }

        // 유닛 피격 VFX 재생 단일 지점(Editor fixture는 위치만 기록한다).
        private void PlayUnitHitVfx(UnitType type, Vector3 position)
        {
#if UNITY_EDITOR
            if (_projectileHitVfxForValidation != null) { _projectileHitVfxForValidation(type, position); return; }
#endif
            EffectManager.Instance?.PlayUnitHit(type, position);
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 여우마법사의 스코프 없는 피해 수신/방출을 제한적으로 관측한다.
        /// 텍스트 풀 오브젝트의 재생 성공을 기록하며 화면 픽셀 가시성이나 VFX 종료는 측정하지 않는다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveFoxLegacyPresentation(EntityDamagedEvent evt, string stage, string dispatch)
        {
            if (_foxLegacyDiagnosticCount >= FoxLegacyDiagnosticLimit
                || evt.Entity == null || !evt.AttackerIsUnit || evt.PresentationResultKey.IsValid
                || _unitSpawn?.GetUnit(evt.AttackerId)?.Type != UnitType.FoxMagician) return;
            _foxLegacyDiagnosticCount++;
            GameLog.Dev.Info("Combat", nameof(HitPresentationQueue), "[FOX-LEGACY-PRESENTATION]",
                $"stage={stage}, scope=unscoped, attackerId={evt.AttackerId}, "
                + $"victimId={evt.Entity.Id}, victimIsUnit={evt.IsUnit}, hp={evt.CurrentHp}, "
                + $"immediate={evt.ImmediatePresentation}, dispatch={dispatch}, "
                + $"frame={Time.frameCount}, localTime={Time.realtimeSinceStartupAsDouble:F6}, "
                + $"detail={_foxLegacyDiagnosticCount}, limit={FoxLegacyDiagnosticLimit}");
        }

        /// <summary>
        /// 특정 타겟을 겨눈 모든 보류 항목을 즉시 방출한다. FIFO 순서를 보존하며 비매칭 항목은 재삽입.
        /// </summary>
        private void FlushTarget(int targetId, bool targetIsUnit)
        {
            _releasedExact.Clear();
            _exactRendezvous.FlushWhere(
                evt => evt.IsUnit == targetIsUnit
                    && evt.Entity != null
                    && evt.Entity.Id == targetId,
                _releasedExact);
            EmitReleasedExact(LegacyPresentationObservationKind.EmittedTargetDeath);

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
                        Emit(item.Event, LegacyPresentationObservationKind.EmittedTargetDeath);
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
            _releasedExact.Clear();
            _exactRendezvous.FlushAttacker(attackerId, _releasedExact);
            EmitReleasedExact(LegacyPresentationObservationKind.EmittedAttackerStop);

            if (!_pendingByAttacker.TryGetValue(attackerId, out Queue<PendingHit> queue))
                return;

            // FIFO 순서 그대로 앞에서부터 전부 방출.
            while (queue.Count > 0)
                Emit(queue.Dequeue().Event, LegacyPresentationObservationKind.EmittedAttackerStop);

            // 공격자 큐 자체를 제거 — 이 공격자는 이번 전투에서 더 이상 항목을 쌓지 않는다.
            // (재교전 시 GetOrCreateQueue가 새 큐를 다시 만든다.)
            _pendingByAttacker.Remove(attackerId);
        }

        private void OnPresentationScopeRetired(
            LocalAttackPresentationScopeRetiredEvent retired)
        {
            AttackPresentationRendezvousStatus status =
                _exactRendezvous.RetireSequence(
                    retired.AttackerId,
                    retired.PresentationScope);
            if (status == AttackPresentationRendezvousStatus.Invalid
                || status == AttackPresentationRendezvousStatus.CapacityExceeded)
            {
                RecordStructuralFailure(
                    new LocalAttackHitPresentationEvent(
                        retired.AttackerId,
                        retired.PresentationScope),
                    status == AttackPresentationRendezvousStatus.CapacityExceeded
                        ? LegacyPresentationObservationKind.StructuralCapacityExceeded
                        : LegacyPresentationObservationKind.StructuralInvalid);
            }
        }

        /// <summary>
        /// 순수 rendezvous가 반환한 exact-scope 결과만 실제 표현으로 방출한다. 호출자가
        /// 지정한 사유는 정상 marker, timeout, target death, attacker stop을 C3에서 서로
        /// 구분하기 위한 것이며 HP나 서버 결과를 변경하지 않는다.
        /// </summary>
        private void EmitReleasedExact(LegacyPresentationObservationKind observationKind)
        {
            for (int index = 0; index < _releasedExact.Count; index++)
                Emit(_releasedExact[index], observationKind);
            _releasedExact.Clear();
        }

        private static void ObserveLegacy(
            EntityDamagedEvent evt,
            LegacyPresentationObservationKind kind)
        {
            if (!UnitAttackResultPresentationShadowBridge.IsActive
                || evt.Entity == null || !evt.AttackerIsUnit)
                return;
            UnitAttackResultPresentationShadowBridge.ObserveLegacy(
                new LegacyPresentationObservation(
                    kind,
                    evt.AttackerId,
                    evt.IsUnit ? 1 : 2,
                    evt.Entity.Id,
                    evt.CurrentHp,
                    Time.realtimeSinceStartupAsDouble,
                    false,
                    default,
                    evt.PresentationResultKey,
                    AttackPresentationScope.FromResultKey(
                        evt.PresentationResultKey)));
        }

        private static void RecordStructuralFailure(
            EntityDamagedEvent evt,
            LegacyPresentationObservationKind kind)
        {
            if (!UnitAttackResultPresentationShadowBridge.IsActive
                || evt.Entity == null)
                return;
            UnitAttackResultPresentationShadowBridge.ObserveStructuralFailure(
                new LegacyPresentationObservation(
                    kind,
                    evt.AttackerId,
                    evt.IsUnit ? 1 : 2,
                    evt.Entity.Id,
                    evt.CurrentHp,
                    Time.realtimeSinceStartupAsDouble,
                    false,
                    default,
                    evt.PresentationResultKey,
                    AttackPresentationScope.FromResultKey(
                        evt.PresentationResultKey)));
        }

        private static void RecordStructuralFailure(
            LocalAttackHitPresentationEvent hit,
            LegacyPresentationObservationKind kind)
        {
            if (!UnitAttackResultPresentationShadowBridge.IsActive)
                return;
            UnitAttackResultPresentationShadowBridge.ObserveStructuralFailure(
                new LegacyPresentationObservation(
                    kind,
                    hit.AttackerId,
                    0,
                    -1,
                    -1,
                    Time.realtimeSinceStartupAsDouble,
                    false,
                    default,
                    default,
                    hit.PresentationScope,
                    hit.ReplicatedSnapshot));
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

        /// <summary>
        /// [2026-10-21 제안 A] 투사체 보관함에 넣는 1건 = 피해 이벤트 + 보관한 순간의 대상 화면 위치.
        /// 위치는 "화면 착탄 지점 없이 방출될 때 대상이 이미 사라졌으면" 쓰는 대체값이다.
        /// </summary>
        private readonly struct ProjectileHeldHit
        {
            /// <summary>원본 피해 이벤트(대상·남은 HP·팀·투사체 번호표).</summary>
            public readonly EntityDamagedEvent Event;

            /// <summary>보관 순간 대상 위치를 읽었는가(대상이 이미 없었으면 false).</summary>
            public readonly bool HasRememberedPosition;

            /// <summary>보관 순간 대상 화면 위치(HasRememberedPosition이 true일 때만 의미가 있다).</summary>
            public readonly Vector3 RememberedPosition;

            public ProjectileHeldHit(EntityDamagedEvent evt, bool hasRememberedPosition, Vector3 rememberedPosition)
            {
                Event = evt;
                HasRememberedPosition = hasRememberedPosition;
                RememberedPosition = rememberedPosition;
            }
        }

        /// <summary>[제안 A] 먼저 온 화면 착탄 신호의 지점과 받은 시각(실제 시계).</summary>
        private readonly struct ReleasePoint
        {
            public readonly Vector3 Point;
            public readonly double At;

            public ReleasePoint(Vector3 point, double at)
            {
                Point = point;
                At = at;
            }
        }

        private readonly struct PendingAuthoritativeBundle
        {
            public readonly AttackResultPresentationInput[] Results;
            public readonly double FirstAttemptLocalTime;

            public PendingAuthoritativeBundle(
                AttackResultPresentationInput[] results,
                double firstAttemptLocalTime)
            {
                Results = results;
                FirstAttemptLocalTime = firstAttemptLocalTime;
            }
        }

        private readonly struct PreparedAuthoritativePresentation
        {
            public readonly AttackResultPresentationInput Result;
            public readonly Transform TargetPresentation;
            public readonly Vector3 DisplayPosition;
            public readonly TeamId VictimTeam;
            public readonly UnitType VictimUnitType;
            public readonly bool IsUnitVictim;
            public readonly bool CanShowText;
            public readonly bool CanShowUnitVfx;
            public readonly bool CanPunch;

            public PreparedAuthoritativePresentation(
                AttackResultPresentationInput result,
                Transform targetPresentation,
                Vector3 displayPosition,
                TeamId victimTeam,
                UnitType victimUnitType,
                bool isUnitVictim,
                bool canShowText,
                bool canShowUnitVfx,
                bool canPunch)
            {
                Result = result;
                TargetPresentation = targetPresentation;
                DisplayPosition = displayPosition;
                VictimTeam = victimTeam;
                VictimUnitType = victimUnitType;
                IsUnitVictim = isUnitVictim;
                CanShowText = canShowText;
                CanShowUnitVfx = canShowUnitVfx;
                CanPunch = canPunch;
            }
        }
    }
}
