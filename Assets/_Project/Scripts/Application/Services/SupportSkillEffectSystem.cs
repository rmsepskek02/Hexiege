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
            return kind == StatusEffectKind.IncomingDamageMul
                || kind == StatusEffectKind.ProductionSpeedMul;
        }

        /// <summary>
        /// 대상 종류 × 효과 종류 조합을 지금 지원하는가. 단계 A: 유닛 × 받는 피해 배율, 팀 × 생산 속도 배율.
        /// (건물 대상은 단계 C · B에서 더한다 — 지금 들어오면 거부하고 진단 Warn을 남긴다.)
        /// </summary>
        /// <param name="target">대상 종류.</param>
        /// <param name="kind">효과 종류.</param>
        public static bool IsSupported(SkillEffectTarget target, StatusEffectKind kind)
        {
            if (target == SkillEffectTarget.Unit) return kind == StatusEffectKind.IncomingDamageMul;
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
        }

        // 대상 1개에 걸린 효과 1개(종류마다 하나 — 같은 종류는 교체).
        private sealed class EffectRecord
        {
            public StatusEffectKind Kind;
            public float Magnitude;
            public float Remaining;   // 남은 시간(초). 0 이하가 되면 끝.
            public EffectBatch Batch; // 이 효과를 건 발동 묶음
        }

        private readonly UnitSpawnUseCase _unitSpawn;

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
                Duration = duration
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
                Batch = batch
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
            if (_closed || !(dt > 0f) || _activeBatches.Count == 0) return;

            for (int i = 0; i < _activeBatches.Count; i++) _activeBatches[i].Elapsed += dt;

            TickStore(_unitEffects, dt, checkUnitAlive: true);
            TickStore(_teamEffects, dt, checkUnitAlive: false);
        }

        private void TickStore(Dictionary<int, List<EffectRecord>> store, float dt, bool checkUnitAlive)
        {
            if (store.Count == 0) return;

            _keyBuffer.Clear();
            foreach (var kv in store) _keyBuffer.Add(kv.Key);

            for (int k = 0; k < _keyBuffer.Count; k++)
            {
                int key = _keyBuffer[k];
                if (!store.TryGetValue(key, out List<EffectRecord> records)) continue;

                // 사망 통보(NotifyUnitDied)를 놓친 경우의 안전망 — 유닛 목록에 없거나 죽었으면 효과를 지운다.
                bool gone = checkUnitAlive && !IsUnitAlive(key);
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
