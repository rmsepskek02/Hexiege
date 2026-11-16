// ============================================================================
// SupportSkillEffectSystem.cs
// 지원 스킬의 「서버만 계산하는 새 효과」(받는 피해 배율 · 생산 속도 배율 …)를 서버 권위로 걸고 · 세고 · 끝내는 서비스.
// 2026-11-16 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 A — 구현 단위 A-1(Plan S-13).
//
// 무엇을 하나(유니티 초급자용 설명):
//   정령 「대지의 갑주」를 쓰면 그 순간 살아 있는 아군 유닛 전부가 8초 동안 피해를 0.7배만 받는다.
//   인간 「전술 지령」을 쓰면 8초 동안 그 팀 생산 건물 전부가 유닛을 1.5배 빠르게 만든다.
//   이 클래스가 "지금 누구에게 어떤 효과가 몇 초 남았는가"를 기억한다.
//     · 걸기   : Apply — 실행기(GlobalStatusChangeExecutor)가 발동 순간 부른다(SkillActivationUseCase의 다리를 거쳐).
//                대상이 「유닛」이면 그 순간 살아 있는 대상 팀 유닛 각각에(새로 생산되는 유닛은 제외 — Plan S-11),
//                대상이 「팀」이면 팀 자체에 건다(그 팀 생산 건물 전부 — 새로 짓거나 업그레이드한 건물도 포함, Plan S-9).
//     · 다시 걸기 : 같은 대상에 같은 종류가 이미 있으면 강도 · 남은 시간을 새 값으로 바꾼다(스킬 규칙 13 「다시 걸릴 때」 교체).
//     · 세기   : Tick — 권위 틱마다 남은 시간을 줄이고, 다 된 효과 · 죽은 유닛의 효과를 지운다.
//     · 묻기   : GetIncomingDamageMultiplier(피해 지점 6곳의 헬퍼 IncomingDamage가 묻는다) ·
//                GetProductionSpeedMultiplier(생산 UseCase가 매 틱 묻는다). 효과가 없으면 정확히 1.
//     · 사망   : NotifyUnitDied — 유닛이 죽으면 그 유닛의 효과를 바로 지운다.
//     · 닫기   : Close — 경기가 끝나면 모든 효과를 지우고, 이후 새 효과를 받지 않는다.
//
// 왜 유닛 상태 시스템(StatusEffectSystem)에 넣지 않는가(Plan S-13 · Research 6-5):
//   상태 시스템은 순수 클라이언트의 「거울」 틱에서도 돌고, 상태 RPC로 클라이언트에 복제된다. 받는 피해 · 생산 진행은
//   서버만 계산해야 하는 값이라 거기 넣으면 의미 없는 복제가 생기고, 단계 C(쉴드 — 줄어드는 양) · 단계 B(재생 누적)는
//   애초에 넣을 수도 없다. 그래서 서버 권위 틱에서만 도는 이 서비스 하나에 모은다(지속 상태 구역 Z3 선례). 기존 상태 시스템 무변경.
//
// 단계 C · B가 얹힐 자리(이번 회차에는 구현하지 않는다):
//   - 저장소가 「대상 키 → 효과 기록 목록」 구조라, 건물 키 저장소(_buildingEffects)와 종류(Shield · Regen · HealAmp)를 더하면 된다.
//   - 효과 기록(EffectRecord)에 「남은 양」(쉴드)이나 「소수 이월」(재생) 칸을 더하면 된다.
//   - IsSupported(대상, 종류) 표에 조합을 더하면 실행기 · 다리는 고치지 않아도 된다.
//
// 2026-11-16 단계 C(신목의 각성 — Plan 6-1)에서 실제로 얹은 것:
//   - 종류 Shield(유닛 · 건물), 건물 저장소 _buildingEffects(유닛 Id와 겹칠 수 있어 따로), 건물 목록 설정자 SetBuildingPlacement.
//   - AbsorbShield: 피해 헬퍼가 「받는 피해 배율 뒤」 값을 넘기면 쉴드가 먼저 받고 나머지를 돌려준다. 0이 되면 깨짐 →
//     다음 Tick 머리에서 GameEvents.OnShieldBrokenConfirmed로 묶어 알린다(멀티 서버는 NetworkSkillController가 Client로 중계).
//   - 만료 = 남은 양 버림, 재적용 = 교체(새 양 · 새 시간), 건물 파괴 · 업그레이드 = NotifyBuildingRemoved, 경기 종료 = Close.
//   - 진단 [SKILL-SHIELD]는 SupportSkillEffectSystem.ShieldDiagnostics.cs.
//
// 「묶음(batch)」이란:
//   발동 한 번이 건 효과들의 모임이다(발동 순번 · 시전 팀 · 슬롯 · 대상 수 · 경과 시간). 진단 로그가
//   「적용 → 끝」을 한 쌍으로 남기려고 쓴다(ArmorApplied ↔ ArmorEnded). 묶음의 대상이 모두 끝나면(만료 · 교체 · 사망) 그 묶음이 끝난다.
//
// 회귀 안전(핵심): 효과가 하나도 없으면 두 조회는 사전(Dictionary) 조회 없이 바로 1을 돌려준다 → 두 스킬을 쓰기 전에는
//   전투 · 생산이 이 클래스가 없던 때와 완전히 같다.
//
// Application 레이어 — Domain 의존. Unity.Netcode 직접 참조 없음(역할은 NetworkContext 정적 홀더로만 읽는다 — 진단용).
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 지원 스킬 새 효과(받는 피해 배율 · 생산 속도 배율)의 서버 권위 저장소.
    /// 같은 경기의 SkillActivationUseCase(쓰기 · 틱 · 닫기) · UnitCombatUseCase/TowerCombatUseCase(받는 피해 읽기) ·
    /// UnitProductionUseCase(생산 속도 읽기)가 같은 인스턴스를 받는다(조합 루트 GameBootstrapper가 맵마다 새로 만든다).
    /// </summary>
    public sealed partial class SupportSkillEffectSystem : IProductionSpeedSource
    {
        // ====================================================================
        // 지원하는 종류 · 조합 표 — 단계 C · B는 여기에 줄을 더한다
        // ====================================================================

        /// <summary>
        /// 이 서비스가 보관하는 종류인가(기존 상태 시스템 대신 이 서비스로 보내야 하는가). 실행기가 주 효과를 나눌 때 쓴다.
        /// </summary>
        /// <param name="kind">상태 종류.</param>
        public static bool IsServiceKind(StatusEffectKind kind)
        {
            // [2026-11-16 단계 C 비활성화 — 쉴드 종류 추가(Plan 6-1)] 옛 식:
            // return kind == StatusEffectKind.IncomingDamageMul
            //     || kind == StatusEffectKind.ProductionSpeedMul;
            return kind == StatusEffectKind.IncomingDamageMul
                || kind == StatusEffectKind.ProductionSpeedMul
                || kind == StatusEffectKind.Shield;
        }

        /// <summary>
        /// 대상 종류 × 효과 종류 조합을 지금 지원하는가. 단계 A: 유닛 × 받는 피해 배율, 팀 × 생산 속도 배율.
        /// (건물 대상은 단계 C · B에서 더한다 — 지금 들어오면 거부하고 진단 Warn을 남긴다.)
        /// </summary>
        /// <param name="target">대상 종류.</param>
        /// <param name="kind">효과 종류.</param>
        public static bool IsSupported(SkillEffectTarget target, StatusEffectKind kind)
        {
            // [2026-11-16 단계 C 비활성화 — 유닛 쉴드 · 건물 쉴드 조합 추가(Plan 6-1)] 옛 줄:
            // if (target == SkillEffectTarget.Unit) return kind == StatusEffectKind.IncomingDamageMul;
            if (target == SkillEffectTarget.Unit)
                return kind == StatusEffectKind.IncomingDamageMul || kind == StatusEffectKind.Shield;
            // 건물 대상(단계 C): 지금은 쉴드만. (단계 B 재생의 뿌리는 여기에 재생 종류를 더한다.)
            if (target == SkillEffectTarget.Building) return kind == StatusEffectKind.Shield;
            if (target == SkillEffectTarget.Team) return kind == StatusEffectKind.ProductionSpeedMul;
            return false;
        }

        // ====================================================================
        // 저장 구조
        // ====================================================================

        // 발동 한 번이 건 효과 모임(진단의 적용 ↔ 끝 한 쌍 · 「모든 대상이 끝나면 묶음도 끝」 판정용).
        private sealed class EffectBatch
        {
            public int Sequence;              // 발동 순번(화면 발동 알림 · [SKILL-BUFF] 줄과 같은 값)
            public TeamId CasterTeam;         // 시전 팀
            public TeamId TargetTeam;         // 효과를 받는 팀(아군 대상이면 시전 팀)
            public int Slot;                  // 스킬 슬롯(0부터)
            public int BuildingId;            // 시전 건물 Id(표기용 — 건물이 사라져도 효과는 시간까지 남는다)
            public SkillEffectTarget Target;  // 대상 종류
            public StatusEffectKind Kind;     // 효과 종류
            public float Magnitude;           // 강도(배율)
            public float Duration;            // 지속(초)
            public float Elapsed;             // 걸린 뒤 흐른 권위 틱 시간(초) — 진단 ActualMs
            public int TargetCount;           // 걸린 대상 수(발동 순간)
            public int LiveCount;             // 아직 이 묶음 효과를 가진 대상 수(0이 되면 묶음 끝)
            public int Replaced;              // 뒤 발동에 교체된 대상 수
            public int DeadTargets;           // 효과 중 죽은 대상 수
            public bool Ended;                // 끝 처리가 끝났는가(중복 끝 방지)

            // ── 진단 전용 칸(개발 빌드에서만 채워진다 — 판정과 무관) ──
            public int DiagRefreshed;               // 걸 때 같은 종류가 이미 있어 교체한 대상 수
            public float DiagPrevRemaining;         // 교체된 이전 효과의 남은 시간 중 가장 긴 값(초)
            public readonly int[] DiagCounts = new int[16]; // 피해 지점(쓰는 곳)별 감소 횟수 — IncomingDamageWriter 정수로 센다
            public int DiagReduced;                 // 감소가 적용된 피격 총수
            public int DiagNewUnitHits;             // 효과 중 같은 팀의 「대상이 아닌(새로 나온) 유닛」이 맞은 횟수
            public int DiagNewUnitSamples;          // 그 표본 줄 수
            public int DiagSamples;                 // 감소 표본 줄 수
            public int DiagSamplesDropped;          // 상한 때문에 남기지 않은 표본 수
            public HashSet<long> DiagSampled;       // 표본을 남긴 (유닛 Id, 쓰는 곳) 쌍

            // ── [2026-11-16 단계 C] 쉴드 묶음 전용 칸 ──
            public int ShieldAmount;                // 대상 하나에 거는 쉴드 양(강도를 정수로 반올림한 값 — 예: 150)
            public int Broken;                      // 효과 중 쉴드가 0이 되어 깨진 대상 수(판정 · 진단 공용 계수)
            public int Expired;                     // 시간이 다 되어 남은 양을 버리고 끝난 대상 수
            public int Upgraded;                    // 건물 업그레이드로 버려진 대상 수(S-11 — 새 Id에는 넘기지 않는다)
            // 진단 전용(개발 빌드에서만 채워진다 — 판정과 무관)
            public long DiagIncomingTotal;          // 쉴드가 있던 대상이 받은 피해 합(헬퍼에 들어온 값 — 배율 뒤)
            public long DiagAbsorbedTotal;          // 그중 쉴드가 흡수한 합
            public long DiagHpDamageTotal;          // 그중 HP로 넘어간 합(들어온 = 흡수 + HP 피해)
            public int DiagAbsorbHits;              // 쉴드가 1 이상 흡수한 피격 수
            public long DiagDiscardedTotal;         // 만료 · 교체 · 업그레이드로 버린 남은 양 합
            public int DiagAbsorbSamples;           // ShieldAbsorbSample 줄 수
            public int DiagAbsorbSamplesDropped;    // 상한 때문에 남기지 않은 표본 수
            public int DiagBrokenLines;             // ShieldBroken 줄 수
            public int DiagExpiredLines;            // ShieldExpired 줄 수
            public int DiagNewTargetHits;           // 효과 중 같은 팀 「대상이 아닌」 유닛 · 건물(새로 나온 것)이 맞은 횟수(C-L7)
            public HashSet<long> DiagShieldSampled; // 첫 흡수 표본을 남긴 (대상 종류, Id) 키
        }

        // 대상 1개에 걸린 효과 1개(종류마다 하나 — 같은 종류는 교체).
        private sealed class EffectRecord
        {
            public StatusEffectKind Kind;
            public float Magnitude;
            public float Remaining;   // 남은 시간(초). 0 이하가 되면 끝.
            public EffectBatch Batch; // 이 효과를 건 발동 묶음
            // [2026-11-16 단계 C] 쉴드 종류만 쓴다 — 남은 흡수량(피해를 받을 때마다 줄어든다. 0이 되면 깨짐).
            //   StatusEffect의 강도(Magnitude)는 고정값이라 「줄어드는 양」을 담을 수 없어 따로 둔다(Research 6-2).
            public int ShieldRemaining;
        }

        private readonly UnitSpawnUseCase _unitSpawn;

        // [2026-11-16 단계 C] 건물 목록(발동 순간 대상 수집 · 틱의 생존 확인). 설정자로 주입한다 — 생성자를 바꾸면
        //   이 서비스를 직접 만드는 Editor 자가 검증(fixture)이 깨지므로(단계 A와 같은 이유) 생성자는 그대로 둔다.
        //   null이면 건물 대상 효과(건물 쉴드)는 아무에게도 걸리지 않는다(무효과 — 진단 Targets=0).
        private BuildingPlacementUseCase _buildingPlacement;

        // [2026-11-16 단계 C] 건물 Id → 그 건물의 효과 목록. 유닛 Id와 건물 Id는 다른 계수기에서 나와 값이 겹칠 수 있으므로
        //   유닛 저장소와 반드시 따로 둔다(상위 Plan 4절 13 · Plan 6-1-2 「저장」).
        private readonly Dictionary<int, List<EffectRecord>> _buildingEffects = new Dictionary<int, List<EffectRecord>>();

        // [2026-11-16 단계 C] 이번 권위 틱 동안 깨진 쉴드(대상 Id + 그 쉴드를 건 발동 순번). 다음 Tick 머리에서 한 번에 알린다
        //   (틱 단위 묶음 — Plan 6-1-2 「소진 알림」). 발동 순번을 함께 들고 있는 이유: 깨진 뒤 같은 대상에 새 쉴드가 걸려도
        //   화면이 "옛 쉴드의 깨짐"으로 새 이펙트를 끄지 않게 하려는 것이다(화면은 순번이 같은 기록만 끈다).
        private readonly List<BrokenShield> _pendingBroken = new List<BrokenShield>(8);

        // 깨진 쉴드 한 건(알림 대기).
        private struct BrokenShield
        {
            public int Sequence;   // 그 쉴드를 건 발동 순번
            public int TargetId;   // 유닛 Id 또는 건물 Id
            public bool IsUnit;    // true = 유닛, false = 건물
        }

        // 알림을 순번별로 묶을 때 쓰는 재사용 버퍼(매 틱 new 하지 않는다 — 깨짐이 있는 틱에만 배열을 만든다).
        private readonly List<int> _brokenUnitBuffer = new List<int>(8);
        private readonly List<int> _brokenBuildingBuffer = new List<int>(8);

        // 유닛 Id → 그 유닛의 효과 목록. (단계 C에서 건물 Id 저장소를 따로 둔다 — 유닛 Id와 건물 Id는 값이 겹칠 수 있다.)
        private readonly Dictionary<int, List<EffectRecord>> _unitEffects = new Dictionary<int, List<EffectRecord>>();
        // 팀(정수) → 그 팀의 효과 목록. 열거형 대신 정수 키를 써서 사전 비교가 상자(boxing) 없이 돈다.
        private readonly Dictionary<int, List<EffectRecord>> _teamEffects = new Dictionary<int, List<EffectRecord>>();
        // 아직 끝나지 않은 묶음(대개 0~2개).
        private readonly List<EffectBatch> _activeBatches = new List<EffectBatch>(4);

        // 틱 · 닫기 때 쓰는 재사용 버퍼(사전을 돌면서 지우면 예외가 나므로 키를 먼저 옮겨 담는다 — 매 틱 할당 없음).
        private readonly List<int> _keyBuffer = new List<int>(32);
        private readonly List<EffectBatch> _batchBuffer = new List<EffectBatch>(4);

        // 경기 종료로 닫혔는지. 닫힌 뒤에는 새 효과를 받지 않는다(맵을 다시 불러오면 서비스가 새로 만들어진다).
        private bool _closed;

        /// <summary>
        /// 서비스 생성. 맵을 불러올 때마다 조합 루트가 새로 만든다(이전 경기의 효과가 남지 않는다).
        /// </summary>
        /// <param name="unitSpawn">유닛 목록(발동 순간 대상 수집 · 틱의 생존 확인). null이면 유닛 대상 효과는 아무에게도 걸리지 않는다.</param>
        public SupportSkillEffectSystem(UnitSpawnUseCase unitSpawn)
        {
            _unitSpawn = unitSpawn;
        }

        /// <summary> 경기 종료로 닫혔는지(확인 · 검증용). </summary>
        public bool IsClosed => _closed;

        /// <summary> 아직 끝나지 않은 발동 묶음 수(확인 · 검증용). </summary>
        public int ActiveBatchCount => _activeBatches.Count;

        /// <summary> 효과를 가진 유닛 수(확인 · 검증용). </summary>
        public int UnitRecordCount => _unitEffects.Count;

        /// <summary> 효과를 가진 건물 수(확인 · 검증용 — 2026-11-16 단계 C). </summary>
        public int BuildingRecordCount => _buildingEffects.Count;

        /// <summary> 아직 화면에 알리지 않은 깨진 쉴드 수(확인 · 검증용 — 다음 Tick 머리에서 알리고 0이 된다). </summary>
        public int PendingBrokenCount => _pendingBroken.Count;

        /// <summary>
        /// 건물 목록을 주입한다(2026-11-16 단계 C — 건물 쉴드 대상 수집 · 생존 확인). 조합 루트가 맵마다 1회 부른다.
        /// 생성자 대신 설정자인 이유: 생성자를 바꾸면 이 서비스를 직접 만드는 Editor 자가 검증이 깨진다.
        /// </summary>
        /// <param name="buildingPlacement">같은 경기의 건물 배치 UseCase. null이면 건물 대상 효과는 걸리지 않는다.</param>
        public void SetBuildingPlacement(BuildingPlacementUseCase buildingPlacement)
        {
            _buildingPlacement = buildingPlacement;
        }

        // ====================================================================
        // 걸기
        // ====================================================================

        /// <summary>
        /// 효과를 건다(서버 권위 — 발동 순간 1회). 대상이 「유닛」이면 대상 팀의 지금 살아 있는 유닛 각각에, 「팀」이면 팀 자체에.
        /// 같은 대상에 같은 종류가 이미 있으면 강도 · 남은 시간을 새 값으로 바꾼다(더하지 않는다 — 스킬 규칙 13).
        /// 닫힌 뒤 · 지원하지 않는 조합 · 잘못된 값이면 아무것도 걸지 않고 진단 Warn 1줄을 남긴다.
        /// </summary>
        /// <param name="sequence">발동 순번(진단 · 화면 짝짓기용).</param>
        /// <param name="casterTeam">시전 팀.</param>
        /// <param name="slot">스킬 슬롯(0부터).</param>
        /// <param name="buildingId">시전 건물 Id(표기용).</param>
        /// <param name="targetTeam">효과를 받는 팀(실행기가 아군/적 판정을 마친 결과).</param>
        /// <param name="target">대상 종류.</param>
        /// <param name="kind">효과 종류.</param>
        /// <param name="magnitude">강도(배율, 0 이상 · 유한).</param>
        /// <param name="duration">지속(초, 0보다 크고 유한).</param>
        /// <returns>효과가 걸린 대상 수(팀 대상이면 1). 거부되면 0.</returns>
        public int Apply(int sequence, TeamId casterTeam, int slot, int buildingId, TeamId targetTeam,
                         SkillEffectTarget target, StatusEffectKind kind, float magnitude, float duration)
        {
            if (_closed)
            {
                DiagRejected(sequence, casterTeam, slot, target, kind, magnitude, duration, "Closed");
                return 0;
            }
            if (!IsSupported(target, kind))
            {
                DiagRejected(sequence, casterTeam, slot, target, kind, magnitude, duration, "UnsupportedTarget");
                return 0;
            }
            // Inspector 오입력 방어: 지속은 0보다 크고 유한, 강도는 0 이상 유한(NaN은 비교가 모두 거짓이라 함께 걸러진다).
            if (!(duration > 0f) || float.IsInfinity(duration) || !(magnitude >= 0f) || float.IsInfinity(magnitude))
            {
                DiagRejected(sequence, casterTeam, slot, target, kind, magnitude, duration, "InvalidValue");
                return 0;
            }
            // [2026-11-16 단계 C] 쉴드 양은 정수(HP와 같은 단위)로 반올림해 쓴다. 1보다 작으면 막을 것이 없으므로 거부한다.
            int shieldAmount = 0;
            if (kind == StatusEffectKind.Shield)
            {
                double rounded = System.Math.Round((double)magnitude, System.MidpointRounding.AwayFromZero);
                shieldAmount = rounded >= int.MaxValue ? int.MaxValue : (int)rounded;
                if (shieldAmount < 1)
                {
                    DiagRejected(sequence, casterTeam, slot, target, kind, magnitude, duration, "InvalidValue");
                    return 0;
                }
            }

            var batch = new EffectBatch
            {
                Sequence = sequence,
                CasterTeam = casterTeam,
                TargetTeam = targetTeam,
                Slot = slot,
                BuildingId = buildingId,
                Target = target,
                Kind = kind,
                Magnitude = magnitude,
                Duration = duration,
                ShieldAmount = shieldAmount // [단계 C] 쉴드가 아니면 0(쓰지 않는다)
            };

            if (target == SkillEffectTarget.Unit)
            {
                // 발동 순간 살아 있는 대상 팀 유닛만(새로 생산되는 유닛은 대상이 아니다 — 스킬 규칙 13 · Plan S-11).
                //   유닛 목록 자체를 바꾸지 않으므로 순회 중 변경 문제는 없다(이 서비스의 사전만 바꾼다).
                if (_unitSpawn != null)
                {
                    foreach (UnitData unit in _unitSpawn.Units.Values)
                    {
                        if (unit == null || !unit.IsAlive || unit.Team != targetTeam) continue;
                        SetRecord(_unitEffects, unit.Id, batch);
                    }
                }
            }
            // [2026-11-16 단계 C 추가] 건물 대상(건물 쉴드): 발동 순간 살아 있는 대상 팀 건물 각각. 효과 중 새로 짓거나
            //   업그레이드한 건물(업그레이드하면 Id가 바뀐다)은 대상이 아니다(Plan S-11 · 6-1-2).
            else if (target == SkillEffectTarget.Building)
            {
                if (_buildingPlacement != null)
                {
                    foreach (BuildingData building in _buildingPlacement.Buildings.Values)
                    {
                        if (building == null || !building.IsAlive || building.Team != targetTeam) continue;
                        SetRecord(_buildingEffects, building.Id, batch);
                    }
                }
            }
            else
            {
                // 팀 대상(생산 속도): 건물 Id를 쓰지 않는다 → 효과 중 새로 짓거나 업그레이드한 생산 건물에도 걸린다(Plan S-9).
                SetRecord(_teamEffects, (int)targetTeam, batch);
            }

            batch.LiveCount = batch.TargetCount;
            // [진단] 적용 직후 1줄([SKILL-ARMOR] ArmorApplied · [SKILL-PROD] ProdSpeedApplied — 개발 빌드 전용).
            DiagBatchApplied(batch);

            if (batch.TargetCount == 0) EndBatch(batch, "NoTargets"); // 걸 대상이 없었다 — 바로 끝(진단 끝 줄 1개).
            else _activeBatches.Add(batch);
            return batch.TargetCount;
        }

        // 한 대상에 효과 기록을 넣거나(처음) 새 값으로 바꾼다(같은 종류가 이미 있음 — 교체).
        private void SetRecord(Dictionary<int, List<EffectRecord>> store, int key, EffectBatch batch)
        {
            if (!store.TryGetValue(key, out List<EffectRecord> records))
            {
                records = new List<EffectRecord>(2);
                store[key] = records;
            }

            batch.TargetCount++;
            for (int i = 0; i < records.Count; i++)
            {
                EffectRecord existing = records[i];
                if (existing.Kind != batch.Kind) continue;

                // 교체: 이전 묶음에서 이 대상을 빼고(그 묶음의 마지막 대상이었으면 그 묶음은 「Replaced」로 끝), 새 값으로 덮는다.
                batch.DiagRefreshed++;
                if (existing.Remaining > batch.DiagPrevRemaining) batch.DiagPrevRemaining = existing.Remaining;
                EffectBatch previous = existing.Batch;
                // [2026-11-16 단계 C] 쉴드 교체(S-5): 남아 있던 양은 버리고(진단 합계에만 센다) 새 양으로 바꾼다 — 더하지 않는다.
                if (previous != null && existing.Kind == StatusEffectKind.Shield)
                    previous.DiagDiscardedTotal += existing.ShieldRemaining;
                existing.ShieldRemaining = batch.ShieldAmount;
                existing.Magnitude = batch.Magnitude;
                existing.Remaining = batch.Duration;
                existing.Batch = batch;
                if (previous != null)
                {
                    previous.Replaced++;
                    ReleaseFromBatch(previous, died: false, endReason: "Replaced");
                }
                return;
            }

            records.Add(new EffectRecord
            {
                Kind = batch.Kind,
                Magnitude = batch.Magnitude,
                Remaining = batch.Duration,
                Batch = batch,
                ShieldRemaining = batch.ShieldAmount // [단계 C] 쉴드가 아니면 0
            });
        }

        // ====================================================================
        // 권위 틱 · 사망 · 닫기
        // ====================================================================

        /// <summary>
        /// (권위 전용 틱) 남은 시간을 줄이고, 다 된 효과와 죽은 유닛의 효과를 지운다.
        /// 반드시 서버 권위 쪽에서만 부른다 — 싱글 = GameBootstrapper.Update(!IsNetworkMode),
        /// 멀티 서버 = NetworkCombatController.TickCombat. 순수 클라이언트에서는 부르지 않는다(클라에는 효과가 없다).
        /// </summary>
        /// <param name="dt">경과 시간(초). 0 이하 · NaN이면 아무것도 하지 않는다.</param>
        public void Tick(float dt)
        {
            // [2026-11-16 단계 C 비활성화 — 깨진 쉴드 알림을 「묶음이 모두 끝난 뒤」에도 보내야 해서 조건을 둘로 나눴다] 옛 줄:
            // if (_closed || !(dt > 0f) || _activeBatches.Count == 0) return;
            //   (마지막 대상의 쉴드가 깨지면 그 묶음이 끝나 _activeBatches가 0이 된다 — 옛 줄 그대로면 그 깨짐 알림이 영영 나가지 않는다.)
            if (_closed || !(dt > 0f)) return;
            // [단계 C] 지난 틱 사이에 깨진 쉴드를 한 번에 알린다(틱 단위 묶음 — 깨짐이 없으면 아무 일도 없다).
            FlushBrokenShields();
            if (_activeBatches.Count == 0) return;

            for (int i = 0; i < _activeBatches.Count; i++) _activeBatches[i].Elapsed += dt;

            TickStore(_unitEffects, dt, checkUnitAlive: true);
            TickStore(_teamEffects, dt, checkUnitAlive: false);
            // [단계 C] 건물 저장소 — 목록에 없거나(파괴 · 철거 · 업그레이드로 Id가 바뀜) 죽은 건물의 효과도 지운다(통보를 놓친 경우의 안전망).
            TickStore(_buildingEffects, dt, checkUnitAlive: false, checkBuildingAlive: true);
        }

        // [2026-11-16 단계 C] 맨 뒤 선택 인자 checkBuildingAlive 추가(건물 저장소용). 기존 두 호출은 인자가 그대로라 동작이 같다.
        private void TickStore(Dictionary<int, List<EffectRecord>> store, float dt, bool checkUnitAlive, bool checkBuildingAlive = false)
        {
            if (store.Count == 0) return;

            _keyBuffer.Clear();
            foreach (var kv in store) _keyBuffer.Add(kv.Key);

            for (int k = 0; k < _keyBuffer.Count; k++)
            {
                int key = _keyBuffer[k];
                if (!store.TryGetValue(key, out List<EffectRecord> records)) continue;

                // 사망 통보(NotifyUnitDied)를 놓친 경우의 안전망 — 유닛 목록에 없거나 죽었으면 효과를 지운다.
                // [2026-11-16 단계 C 비활성화 — 건물 생존 확인을 더했다] 옛 줄:
                // bool gone = checkUnitAlive && !IsUnitAlive(key);
                bool gone = (checkUnitAlive && !IsUnitAlive(key)) || (checkBuildingAlive && !IsBuildingAlive(key));
                for (int i = records.Count - 1; i >= 0; i--)
                {
                    EffectRecord record = records[i];
                    if (gone)
                    {
                        records.RemoveAt(i);
                        ReleaseFromBatch(record.Batch, died: true, endReason: "AllTargetsGone");
                        continue;
                    }
                    record.Remaining -= dt;
                    if (record.Remaining <= 0f)
                    {
                        records.RemoveAt(i);
                        // [단계 C] 쉴드 만료 — 남은 양은 버린다(Plan S-5 · 6-1-2). 계수 + 진단 표본 1줄(상한) — 판정 저장소는 이미 지웠다.
                        if (record.Kind == StatusEffectKind.Shield && record.Batch != null)
                        {
                            record.Batch.Expired++;
                            record.Batch.DiagDiscardedTotal += record.ShieldRemaining;
                            DiagShieldExpired(record.Batch, key, isUnit: checkUnitAlive, discarded: record.ShieldRemaining);
                        }
                        ReleaseFromBatch(record.Batch, died: false, endReason: "Expire");
                    }
                }
                if (records.Count == 0) store.Remove(key);
            }
            _keyBuffer.Clear();
        }

        private bool IsUnitAlive(int unitId)
        {
            if (_unitSpawn == null) return false;
            return _unitSpawn.Units.TryGetValue(unitId, out UnitData unit) && unit != null && unit.IsAlive;
        }

        // [2026-11-16 단계 C] 건물이 아직 목록에 있고 살아 있는가. 업그레이드하면 옛 Id가 목록에서 빠지므로 여기서 false가 된다.
        private bool IsBuildingAlive(int buildingId)
        {
            if (_buildingPlacement == null) return false;
            BuildingData building = _buildingPlacement.GetBuilding(buildingId);
            return building != null && building.IsAlive;
        }

        /// <summary>
        /// 건물이 파괴 · 철거되거나 업그레이드로 사라지면 그 건물의 효과(건물 쉴드)를 바로 지운다(2026-11-16 단계 C).
        /// 조합 루트의 OnBuildingDied · OnBuildingUpgraded(옛 Id) 구독이 부른다. 효과가 없는 건물이면 아무 일도 없다.
        /// 업그레이드는 새 Id에 효과를 넘기지 않고 버린다(Plan S-11 · 6-1-2 — 업그레이드 순간 옛 건물의 쉴드는 끝난다).
        /// </summary>
        /// <param name="buildingId">사라진 건물 Id(업그레이드면 옛 Id).</param>
        /// <param name="upgraded">true = 업그레이드로 사라짐, false = 파괴 · 철거.</param>
        public void NotifyBuildingRemoved(int buildingId, bool upgraded)
        {
            if (!_buildingEffects.TryGetValue(buildingId, out List<EffectRecord> records)) return;
            _buildingEffects.Remove(buildingId);
            for (int i = 0; i < records.Count; i++)
            {
                EffectRecord record = records[i];
                if (record.Batch == null) continue;
                if (upgraded)
                {
                    // 업그레이드 — 「사망」이 아니라 「버림」으로 센다(ShieldEnded Upgraded= · 버린 양 합계).
                    record.Batch.Upgraded++;
                    if (record.Kind == StatusEffectKind.Shield) record.Batch.DiagDiscardedTotal += record.ShieldRemaining;
                    ReleaseFromBatch(record.Batch, died: false, endReason: "AllTargetsGone");
                }
                else
                {
                    ReleaseFromBatch(record.Batch, died: true, endReason: "AllTargetsGone");
                }
            }
        }

        // ====================================================================
        // [2026-11-16 단계 C] 쉴드 흡수 — 피해 지점 헬퍼(IncomingDamage.Apply)가 부른다
        // ====================================================================

        /// <summary>
        /// 쉴드가 피해를 먼저 받는다(서버 권위 피해 경로 전용 — Plan S-14 순서의 「쉴드 흡수」 단계).
        /// 흡수 = min(남은 쉴드, 들어온 피해), 돌려주는 값 = HP로 넘길 나머지. 쉴드가 0이 되면 「깨짐」 —
        /// 기록을 지우고 다음 권위 틱 머리에서 화면에 알린다(틱 단위 묶음).
        /// 🔴 회귀 안전: 쉴드 기록이 하나도 없거나(사전 조회 없이 바로) 이 대상에 쉴드가 없으면 들어온 값을 그대로 돌려준다.
        /// </summary>
        /// <param name="target">피해를 받는 대상(유닛 · 건물).</param>
        /// <param name="incoming">방어 감쇄 · 받는 피해 배율까지 끝난 피해(HP에 들어가기 직전 값).</param>
        /// <param name="writer">피해 지점(진단 표본용 — 판정에는 쓰지 않는다).</param>
        /// <returns>HP에 넣을 피해(쉴드가 흡수하고 남은 양).</returns>
        public int AbsorbShield(IDamageable target, int incoming, IncomingDamageWriter writer)
        {
            if (target == null || incoming <= 0) return incoming;
            // 이미 죽은 대상은 TakeDamage가 아무것도 하지 않으므로 쉴드도 깎지 않는다(값 그대로 — 기존과 같음).
            if (!target.IsAlive) return incoming;

            Dictionary<int, List<EffectRecord>> store;
            bool isUnit;
            if (target is UnitData) { store = _unitEffects; isUnit = true; }
            else if (target is BuildingData) { store = _buildingEffects; isUnit = false; }
            else return incoming;

            if (store.Count == 0)
            {
                // 쉴드가 하나도 없다 — 진단만(같은 팀 쉴드 묶음이 진행 중이면 「새 대상 피격」으로 센다 — C-L7).
                DiagShieldUntargetedHit(target, isUnit, writer, incoming);
                return incoming;
            }
            if (!store.TryGetValue(target.Id, out List<EffectRecord> records))
            {
                DiagShieldUntargetedHit(target, isUnit, writer, incoming);
                return incoming;
            }

            for (int i = 0; i < records.Count; i++)
            {
                EffectRecord record = records[i];
                if (record.Kind != StatusEffectKind.Shield) continue;

                int before = record.ShieldRemaining;
                int absorbed = before < incoming ? before : incoming;
                if (absorbed < 0) absorbed = 0;
                record.ShieldRemaining = before - absorbed;
                int hpDamage = incoming - absorbed;
                EffectBatch batch = record.Batch;
                bool broken = record.ShieldRemaining <= 0;

                // [진단] 흡수 표본(대상별 첫 흡수 + 깨지는 순간 — 상한) · 합계(개발 빌드 전용 — 판정 무관).
                DiagShieldAbsorb(batch, target, isUnit, writer, incoming, absorbed, before, record.ShieldRemaining, hpDamage, broken);

                if (broken)
                {
                    // 깨짐: 기록을 지우고(이 대상은 더는 흡수하지 않는다) 알림 대기열에 넣는다.
                    records.RemoveAt(i);
                    if (records.Count == 0) store.Remove(target.Id);
                    if (batch != null)
                    {
                        batch.Broken++;
                        _pendingBroken.Add(new BrokenShield { Sequence = batch.Sequence, TargetId = target.Id, IsUnit = isUnit });
                        ReleaseFromBatch(batch, died: false, endReason: "AllBroken");
                    }
                }
                return hpDamage;
            }

            // 이 대상에 다른 효과(받는 피해 배율)만 있고 쉴드는 없다 — 값 그대로.
            DiagShieldUntargetedHit(target, isUnit, writer, incoming);
            return incoming;
        }

        /// <summary> 대상에 쉴드가 남아 있으면 그 양, 없으면 0(확인 · 검증용 — 2026-11-16 단계 C). </summary>
        /// <param name="targetId">유닛 Id 또는 건물 Id.</param>
        /// <param name="isUnit">true = 유닛 저장소, false = 건물 저장소.</param>
        public int GetShieldRemaining(int targetId, bool isUnit)
        {
            Dictionary<int, List<EffectRecord>> store = isUnit ? _unitEffects : _buildingEffects;
            if (store.Count == 0 || !store.TryGetValue(targetId, out List<EffectRecord> records)) return 0;
            for (int i = 0; i < records.Count; i++)
                if (records[i].Kind == StatusEffectKind.Shield) return records[i].ShieldRemaining;
            return 0;
        }

        // 깨진 쉴드를 발동 순번별로 묶어 「확정」 이벤트로 알린다(서버 권위 쪽 — 싱글 · Host 화면이 직접 받고, 멀티 서버는
        //   NetworkSkillController가 이 이벤트를 받아 ShieldBrokenClientRpc로 순수 Client에 보낸다). 깨짐이 없으면 아무것도 하지 않는다.
        private void FlushBrokenShields()
        {
            if (_pendingBroken.Count == 0) return;
            // 대개 순번은 하나뿐이다(한 번의 발동). 여러 개면 순번마다 한 건씩 보낸다.
            while (_pendingBroken.Count > 0)
            {
                int sequence = _pendingBroken[0].Sequence;
                _brokenUnitBuffer.Clear();
                _brokenBuildingBuffer.Clear();
                for (int i = _pendingBroken.Count - 1; i >= 0; i--)
                {
                    BrokenShield b = _pendingBroken[i];
                    if (b.Sequence != sequence) continue;
                    if (b.IsUnit) _brokenUnitBuffer.Insert(0, b.TargetId); else _brokenBuildingBuffer.Insert(0, b.TargetId);
                    _pendingBroken.RemoveAt(i);
                }
                var presentation = new ShieldBrokenPresentation(sequence, _brokenUnitBuffer, _brokenBuildingBuffer);
                // [진단] 서버가 알린 Id 목록 1줄(Client ShieldBrokenReceived와 대조 — C-L8).
                DiagShieldBrokenNotified(presentation);
                try
                {
                    GameEvents.OnShieldBrokenConfirmed.OnNext(presentation);
                }
                catch (System.Exception ex)
                {
                    // 화면 · 전송 구독자의 실패가 서버 권위 틱(효과 만료)을 깨지 않게 삼키고 Warn 1줄만 남긴다(구역 알림 선례).
                    GameLog.Dev.Warn("Skill", nameof(SupportSkillEffectSystem),
                                     "[SKILL-SHIELD] 쉴드 깨짐 알림 구독자 예외 — 효과 판정은 유지",
                                     $"Diag=SkillShield, DiagRev=1, Seq={sequence}, Units={presentation.UnitCount}, Buildings={presentation.BuildingCount}, Error={ex.GetType().Name}");
                }
            }
            _brokenUnitBuffer.Clear();
            _brokenBuildingBuffer.Clear();
        }

        /// <summary>
        /// 유닛이 죽으면 그 유닛의 효과를 바로 지운다(조합 루트의 OnUnitDied 구독이 부른다). 효과가 없는 유닛이면 아무 일도 없다.
        /// 서버 가드가 필요 없다 — 순수 클라이언트의 서비스에는 효과가 걸리지 않으므로 지울 것도 없다.
        /// </summary>
        /// <param name="unitId">죽은 유닛 Id.</param>
        public void NotifyUnitDied(int unitId)
        {
            if (!_unitEffects.TryGetValue(unitId, out List<EffectRecord> records)) return;
            _unitEffects.Remove(unitId);
            for (int i = 0; i < records.Count; i++)
                ReleaseFromBatch(records[i].Batch, died: true, endReason: "AllTargetsGone");
        }

        /// <summary>
        /// 경기 종료 — 남은 효과를 모두 지우고(진단 끝 줄 Reason=GameEnd) 이후 새 효과를 받지 않는다(멱등).
        /// 조합 루트가 GameEvents.OnGameEnd에서 부른다.
        /// </summary>
        public void Close()
        {
            if (_closed) return;
            _closed = true;

            _batchBuffer.Clear();
            _batchBuffer.AddRange(_activeBatches);
            for (int i = 0; i < _batchBuffer.Count; i++) EndBatch(_batchBuffer[i], "GameEnd");
            _batchBuffer.Clear();

            _activeBatches.Clear();
            _unitEffects.Clear();
            _teamEffects.Clear();
            // [2026-11-16 단계 C] 건물 효과와 아직 알리지 않은 깨짐도 버린다 — 경기 종료 때 화면은 모든 쉴드 이펙트를
            //   스스로 꼬리 정지하므로(OnGameEnd) 깨짐 알림을 따로 보낼 필요가 없다.
            _buildingEffects.Clear();
            _pendingBroken.Clear();
        }

        // 묶음에서 대상 하나가 빠졌다(만료 · 교체 · 사망). 마지막 대상이었으면 묶음을 끝낸다.
        private void ReleaseFromBatch(EffectBatch batch, bool died, string endReason)
        {
            if (batch == null || batch.Ended) return;
            if (died) batch.DeadTargets++;
            batch.LiveCount--;
            if (batch.LiveCount <= 0) EndBatch(batch, endReason);
        }

        private void EndBatch(EffectBatch batch, string reason)
        {
            if (batch.Ended) return;
            batch.Ended = true;
            _activeBatches.Remove(batch);
            // [진단] 묶음 끝 1줄([SKILL-ARMOR] ArmorEnded · [SKILL-PROD] ProdSpeedEnded — 개발 빌드 전용).
            DiagBatchEnded(batch, reason);
        }

        // ====================================================================
        // 조회
        // ====================================================================

        /// <summary>
        /// 유닛의 받는 피해 배율(대지의 갑주). 효과가 없으면 정확히 1(사전 조회 없이 바로 — 회귀 안전).
        /// 피해 지점 6곳의 헬퍼 IncomingDamage.Apply가 방어 감쇄 "뒤"에 곱한다.
        /// </summary>
        /// <param name="unitId">피격 유닛 Id.</param>
        public float GetIncomingDamageMultiplier(int unitId)
        {
            if (_unitEffects.Count == 0) return 1f;
            return FindMagnitude(_unitEffects, unitId, StatusEffectKind.IncomingDamageMul, out _);
        }

        /// <summary>
        /// 팀의 생산 속도 배율(전술 지령). 효과가 없으면 정확히 1(사전 조회 없이 바로 — 회귀 안전).
        /// </summary>
        /// <param name="team">생산 건물의 팀.</param>
        public float GetProductionSpeedMultiplier(TeamId team)
        {
            if (_teamEffects.Count == 0) return 1f;
            return FindMagnitude(_teamEffects, (int)team, StatusEffectKind.ProductionSpeedMul, out _);
        }

        /// <summary> (진단용) 그 팀에 지금 생산 속도를 건 발동 순번. 없으면 0. </summary>
        /// <param name="team">생산 건물의 팀.</param>
        public int GetProductionSpeedSequence(TeamId team)
        {
            if (_teamEffects.Count == 0) return 0;
            FindMagnitude(_teamEffects, (int)team, StatusEffectKind.ProductionSpeedMul, out EffectBatch batch);
            return batch != null ? batch.Sequence : 0;
        }

        /// <summary> 유닛에 지정 종류 효과가 걸려 있는지(확인 · 검증용). </summary>
        public bool UnitHasEffect(int unitId, StatusEffectKind kind)
        {
            if (_unitEffects.Count == 0) return false;
            FindMagnitude(_unitEffects, unitId, kind, out EffectBatch batch);
            return batch != null;
        }

        /// <summary> 건물에 지정 종류 효과가 걸려 있는지(확인 · 검증용 — 2026-11-16 단계 C). </summary>
        public bool BuildingHasEffect(int buildingId, StatusEffectKind kind)
        {
            if (_buildingEffects.Count == 0) return false;
            FindMagnitude(_buildingEffects, buildingId, kind, out EffectBatch batch);
            return batch != null;
        }

        // 저장소에서 대상 · 종류의 강도를 찾는다. 없으면 1과 null 묶음.
        private static float FindMagnitude(Dictionary<int, List<EffectRecord>> store, int key, StatusEffectKind kind,
                                           out EffectBatch batch)
        {
            batch = null;
            if (!store.TryGetValue(key, out List<EffectRecord> records)) return 1f;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Kind != kind) continue;
                batch = records[i].Batch;
                return records[i].Magnitude;
            }
            return 1f;
        }
    }
}
