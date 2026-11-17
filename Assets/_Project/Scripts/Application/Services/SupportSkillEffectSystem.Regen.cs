// ============================================================================
// SupportSkillEffectSystem.Regen.cs
// 재생의 뿌리 — 「독립 재생 채널」(1초마다 회복)과 「받는 회복 배율」(회복 증폭 + 대상별 소수 이월).
// 2026-11-17 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 B — 구현 단위 B-1 · B-2(Plan 6-2).
//
// 무엇을 하나(유니티 초급자용 설명):
//   초월 「재생의 뿌리」를 쓰면 그 순간 살아 있는 아군 유닛 · 건물에 두 효과가 8초 동안 걸린다(실행기가 이 서비스의 Apply를 부른다).
//     ① 재생(Regen)        : 1초마다 「초당 회복량」만큼 체력을 되찾는다(유닛 20 · 건물 40 — 시험용 임시값).
//     ② 받는 회복 배율(HealAmpMul) : 그 대상이 받는 회복이 전부 1.5배가 된다 — 연구소 자연회복, MistShrine 물안개,
//                                    BloomFairy 힐(지속 회복), 그리고 ①의 재생 자신까지.
//
// ① 재생 — 왜 「1초 주기」인가:
//   물안개 규칙 8-1 「독립 누적기 + 매 틱 대상 재수집」 모델을 따랐다. 물안개도 1초마다 한 번 회복한다(규칙 8).
//   매 서버 틱(멀티 0.1초 · 싱글 매 프레임)마다 조금씩 회복하면 대상마다 초당 10번 이상 HP 동기화 RPC가 나가 모바일 네트워크에
//   부담이 된다(유닛 30기면 초당 300건). 1초 주기면 대상당 초당 1건이다. 8초 효과면 정확히 8번 회복한다(총량 = 초당 × 8).
//   기존 「지속 회복(HealOverTime)」 · 지속 효과 목록(_activeTimedEffects)은 쓰지 않는다(물안개 규칙 8-2 · Plan 6-2-2).
//   다른 회복(자연회복 · 물안개 · BloomFairy)과는 누적기 · 채널이 완전히 따로라 서로 덮어쓰지 않고 함께 적용된다(물안개 규칙 14).
//
// ① 재생 — 체력이 가득일 때:
//   그 1초 주기는 회복하지 않고 지나간다. 효과(기록)는 지우지 않는다 → 8초 안에 맞으면 다음 주기부터 다시 회복한다(Plan 6-2-2 · 물안개 규칙 5).
//
// ① 재생 — 회복 텍스트(물안개 규칙 15 · Plan RB-6):
//   실제로 HP가 오른 주기만 회복 이벤트(OnEntityHealed)를 낸다(멀티는 이 이벤트로 HP가 Client에 동기화된다 — 새 RPC 없음).
//   부유 회복 텍스트는 3주기마다 1번만 켠다(물안개 표시 주기 3초 — SpecialAttackConfig._mistHealTextInterval 기본값과 같게).
//   매 주기 텍스트를 띄우면 화면이 시끄럽기 때문이다. 텍스트가 꺼진 주기도 HP 동기화는 그대로 나간다.
//
// ② 받는 회복 배율 — 「한 번만」 · 「소수 이월」:
//   AmplifyHeal 하나가 맡는다. 회복이 HP에 들어가는 자리가 Heal 바로 앞에서 부른다:
//     UnitCombatUseCase.ApplyHealToUnit(자연회복 · BloomFairy 지속 회복 · 파도) · MistShrineUseCase 유닛 · 건물 · 아래 재생 주기.
//   재생 주기는 ApplyHealToUnit을 거치지 않고 여기서 직접 Heal하므로, 한 번의 회복에 배율이 두 번 곱해지는 일이 없다(RB-5).
//   회복은 정수 HP다. 자연회복은 한 번에 1씩 들어오는데, 매번 「1 × 1.5 = 1.5 → 반올림 2」를 하면 실제로는 2배가 된다(RB-4).
//   그래서 「회복량 × 배율 + 지난번 나머지」의 정수 부분만 회복시키고, 1 미만 나머지는 그 대상 기록에 모아 다음 회복에 더한다.
//     예) 1, 1, 1, 1 → 1.5(1, 남 0.5) · 2.0(2, 남 0) · 1.5(1, 남 0.5) · 2.0(2, 남 0) = 합 6 = 4 × 1.5.
//   나머지는 효과가 끝나거나(만료 · 교체 · 사망 · 파괴 · 업그레이드 · 경기 종료) 기록이 지워질 때 함께 버린다(1 미만 — RB-1).
//
// 회귀 안전(핵심):
//   - 받는 회복 배율 기록이 없는 대상은 AmplifyHeal이 받은 값을 「그대로」 돌려준다(곱하지 않는다) → 기존 회복과 정확히 같다.
//   - 배율이 정확히 1이어도 곱하지 않고 그대로 돌려준다.
//   - 재생 기록이 하나도 없으면 TickRegen은 진행 중인 묶음(대개 0~2개)만 훑고 바로 끝난다.
//
// 서버 권위: 이 파일의 회복은 서버 권위 틱(Tick)과 서버의 회복 자리에서만 돈다. 순수 Client의 서비스에는 효과가 걸리지 않으므로
//   AmplifyHeal이 불려도 값 그대로다(Client 회복 자리는 HP 동기화뿐이고 이 메서드를 부르지 않는다).
//
// Application 레이어 — Domain 의존. Unity.Netcode 직접 참조 없음.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 회복이 어디서 왔는가(받는 회복 배율 진단 · 판정 로그의 Source= 값). 회복 계산에는 쓰지 않는다 — 모든 출처에 같은 배율을 곱한다.
    /// </summary>
    public enum HealAmpSource
    {
        /// <summary> 출처를 넘기지 않은 호출(기본값). </summary>
        Unspecified = 0,
        /// <summary> 연구소 자연회복(초월 유닛 상시 회복). </summary>
        Natural = 1,
        /// <summary> MistShrine 물안개(유닛 · 건물). </summary>
        Mist = 2,
        /// <summary> BloomFairy 힐(지속 회복 — 시전자가 BloomFairy). </summary>
        Bloom = 3,
        /// <summary> 그 밖의 지속 회복(시전자 없음 · 다른 유닛 — 예: 타입 C 회복 스킬). </summary>
        HoT = 4,
        /// <summary> 재생의 뿌리 자신의 재생. </summary>
        SkillRegen = 5,
        /// <summary> TorrentSpirit 파도 회복(정령 — 초월 팀과 만나지 않는다). </summary>
        Wave = 6
    }

    public sealed partial class SupportSkillEffectSystem
    {
        // 재생 회복 주기(초). 물안개 회복 주기와 같은 1초(물안개 규칙 8 — 설계 고정값).
        private const float RegenPulseInterval = 1f;
        // 마지막 틱에서 「1초가 거의 찼다」를 1초로 쳐 주는 허용치(초). 프레임 dt를 여러 번 더하면 float 오차로 7.9999초에서
        //   끝나 8번째 회복을 놓칠 수 있어서다. 마지막 틱(남은 시간 ≤ dt)에만 쓴다.
        private const float RegenFinalPulseTolerance = 0.001f;
        // 부유 회복 텍스트를 몇 주기마다 한 번 켤지(물안개 표시 주기 3초와 같게 — 물안개 규칙 15).
        private const int RegenTextEveryPulses = 3;
        // 한 틱에 한 대상이 돌 수 있는 회복 주기 상한(아주 큰 dt가 들어와도 반복이 끝없이 돌지 않게 — 방어용).
        private const int RegenMaxPulsesPerTick = 16;

        // 재생 틱이 쓰는 키 버퍼(TickStore의 _keyBuffer와 따로 — 같은 Tick 안에서 연달아 쓰므로 섞이지 않게). 매 틱 new 하지 않는다.
        private readonly List<int> _regenKeyBuffer = new List<int>(32);

        // ====================================================================
        // ① 독립 재생 채널 — 권위 Tick이 만료 처리(TickStore)보다 먼저 부른다
        // ====================================================================

        // 진행 중인 재생 묶음이 하나라도 있을 때만 두 저장소를 훑는다(대지의 갑주 · 쉴드만 있을 때는 묶음 목록만 보고 끝).
        private void TickRegen(float dt)
        {
            bool anyRegen = false;
            for (int i = 0; i < _activeBatches.Count; i++)
            {
                if (_activeBatches[i].Kind == StatusEffectKind.Regen) { anyRegen = true; break; }
            }
            if (!anyRegen) return;

            if (_unitEffects.Count > 0) TickRegenStore(_unitEffects, dt, isUnit: true);
            if (_buildingEffects.Count > 0) TickRegenStore(_buildingEffects, dt, isUnit: false);
        }

        // 저장소 하나(유닛 또는 건물)의 재생 기록마다 누적기를 진행하고, 1초가 차면 한 번 회복한다.
        private void TickRegenStore(Dictionary<int, List<EffectRecord>> store, float dt, bool isUnit)
        {
            // 회복 이벤트 구독자가 이 서비스의 사전을 바꿀 가능성에 대비해 키를 먼저 옮겨 담고 돈다(TickStore와 같은 방식).
            _regenKeyBuffer.Clear();
            foreach (var kv in store) _regenKeyBuffer.Add(kv.Key);

            for (int k = 0; k < _regenKeyBuffer.Count; k++)
            {
                int key = _regenKeyBuffer[k];
                if (!store.TryGetValue(key, out List<EffectRecord> records)) continue;
                EffectRecord regen = FindRecord(records, StatusEffectKind.Regen);
                if (regen == null || regen.Batch == null) continue;

                // 대상이 사라졌거나 죽었으면 회복하지 않는다(기록 정리는 바로 뒤 TickStore의 안전망이 한다).
                IDamageable target = isUnit ? (IDamageable)GetAliveUnit(key) : GetAliveBuilding(key);
                if (target == null) continue;

                // 이번 틱에 효과가 실제로 살아 있던 시간만 센다 — 마지막 틱은 남은 시간만큼만(총 회복 주기 = 지속시간 ÷ 1초).
                float slice = regen.Remaining < dt ? regen.Remaining : dt;
                if (!(slice > 0f)) continue;
                bool lastSlice = regen.Remaining <= dt;
                regen.RegenClock += slice;

                float threshold = lastSlice ? RegenPulseInterval - RegenFinalPulseTolerance : RegenPulseInterval;
                int pulses = 0;
                while (regen.RegenClock >= threshold && pulses < RegenMaxPulsesPerTick)
                {
                    regen.RegenClock -= RegenPulseInterval;
                    if (regen.RegenClock < 0f) regen.RegenClock = 0f;
                    pulses++;
                    PulseRegen(regen, target, isUnit);
                }
            }
            _regenKeyBuffer.Clear();
        }

        // 재생 1주기(1초) — 「초당 회복량」(+ 지난 소수)의 정수만큼, 받는 회복 배율을 한 번 거쳐 회복한다.
        private void PulseRegen(EffectRecord regen, IDamageable target, bool isUnit)
        {
            EffectBatch batch = regen.Batch;
            regen.RegenPulses++;

            // 증폭 전 재생량(정수). 초당 회복량이 소수면 나머지를 다음 주기로 넘긴다(지금 값 20 · 40은 나머지 0).
            double exact = (double)regen.Magnitude * RegenPulseInterval + regen.RegenBaseCarry;
            double whole = System.Math.Floor(exact);
            if (!(whole >= 0d)) whole = 0d; // NaN 방어(강도는 Apply에서 0 이상 · 유한으로 검사했다)
            int baseAmount = whole >= int.MaxValue ? int.MaxValue : (int)whole;
            regen.RegenBaseCarry = exact - whole;
            if (baseAmount <= 0) return;

            // 체력이 가득 — 이번 주기는 회복하지 않고 지나간다. 효과는 그대로 남는다(Plan 6-2-2 · B-L7).
            //   받는 회복 배율도 부르지 않는다(가득인 대상에 소수 이월만 쌓이지 않게).
            if (target.Hp >= target.MaxHp)
            {
                DiagRegenFullSkip(batch, regen);
                return;
            }

            // 받는 회복 배율(같은 대상의 HealAmpMul 기록)을 한 번만 곱한다 — ApplyHealToUnit을 거치지 않으므로 두 번 곱해지지 않는다(RB-5).
            int amount = AmplifyHeal(target, baseAmount, HealAmpSource.SkillRegen);
            int hpBefore = target.Hp;
            if (target is UnitData unit) unit.Heal(amount);              // 최대 HP에서 멈춘다(UnitData.Heal 클램프)
            else if (target is BuildingData building) building.Heal(amount); // 최대 HP에서 멈춘다(BuildingData.Heal 클램프)
            else return;
            int gained = target.Hp - hpBefore;

            // 텍스트는 3주기마다(물안개 표시 주기 선례). 실제로 오른 주기에서만 켠다.
            bool showText = gained > 0 && regen.RegenPulses % RegenTextEveryPulses == 0;
            DiagRegenPulse(batch, regen, target, isUnit, baseAmount, amount, hpBefore, gained, showText);
            if (gained <= 0) return; // 실제로 회복된 대상만 이벤트 · 동기화(물안개 규칙 15)

            // 회복 이벤트 — 멀티 서버는 NetworkHealthSync가 이 이벤트로 Client HP를 맞춘다(유닛 SyncHealClientRpc · 건물
            //   SyncBuildingHealClientRpc — 기존 경로, 새 RPC 없음 → 스키마 10 그대로). 힐러 = 시전 건물(유닛 아님).
            try
            {
                GameEvents.OnEntityHealed.OnNext(new EntityHealedEvent(target, target.Hp, isUnit,
                    healerId: batch != null ? batch.BuildingId : -1, healerIsUnit: false, showText: showText));
            }
            catch (System.Exception ex)
            {
                // 화면 · 전송 구독자의 실패가 서버 권위 틱(다른 대상 회복 · 효과 만료)을 깨지 않게 삼키고 Warn 1줄만 남긴다(쉴드 알림 선례).
                DiagRegenEventFault(batch);
                GameLog.Dev.Warn("Skill", nameof(SupportSkillEffectSystem),
                                 "[SKILL-REGEN] 재생 회복 이벤트 구독자 예외 — HP 회복은 이미 적용됨",
                                 $"Diag=SkillRegen, DiagRev=1, Seq={(batch != null ? batch.Sequence : 0)}, Target={(isUnit ? "Unit" : "Building")}, Id={target.Id}, Error={ex.GetType().Name}");
            }
        }

        // ====================================================================
        // ② 받는 회복 배율 — 회복이 HP에 들어가는 모든 서버 자리가 Heal 직전에 부른다
        // ====================================================================

        /// <summary>
        /// 이 대상이 받을 회복량에 받는 회복 배율(재생의 뿌리)을 한 번 곱해 정수로 돌려준다(서버 권위 회복 자리 전용).
        /// 「회복량 × 배율 + 이 대상의 지난 나머지」의 정수 부분을 돌려주고, 1 미만 나머지는 대상 기록에 남겨 다음 회복에 더한다(소수 이월).
        /// 🔴 회귀 안전: 받는 회복 배율 기록이 없는 대상 · 배율이 정확히 1인 대상 · 회복량 0 이하 · 죽은 대상이면 받은 값을 「그대로」 돌려준다.
        /// </summary>
        /// <param name="target">회복을 받는 대상(유닛 · 건물).</param>
        /// <param name="amount">원래 회복량(정수 HP).</param>
        /// <param name="source">회복 출처(진단 Source= 값 — 계산에는 쓰지 않는다).</param>
        /// <returns>Heal에 넘길 회복량.</returns>
        public int AmplifyHeal(IDamageable target, int amount, HealAmpSource source)
        {
            if (target == null || amount <= 0) return amount;
            if (!target.IsAlive) return amount; // 죽은 대상은 Heal이 아무것도 하지 않는다 — 나머지도 쌓지 않는다.

            Dictionary<int, List<EffectRecord>> store;
            bool isUnit;
            if (target is UnitData) { store = _unitEffects; isUnit = true; }
            else if (target is BuildingData) { store = _buildingEffects; isUnit = false; }
            else return amount;

            if (store.Count == 0 || !store.TryGetValue(target.Id, out List<EffectRecord> records))
            {
                // 배율이 없는 대상 — 진단만(같은 팀 증폭 묶음이 진행 중이면 「발동 뒤 새로 나온 대상」으로 센다 — B-L8).
                DiagAmpUntargeted(target, isUnit, source, amount);
                return amount;
            }
            EffectRecord amp = FindRecord(records, StatusEffectKind.HealAmpMul);
            if (amp == null)
            {
                DiagAmpUntargeted(target, isUnit, source, amount);
                return amount;
            }

            float mag = amp.Magnitude;
            if (mag == 1f) return amount; // 배율 1이면 곱하지 않는다(값 그대로 — 회귀 안전).

            double carryBefore = amp.AmpCarry;
            double exact = amount * (double)mag + carryBefore;
            double whole = System.Math.Floor(exact);
            if (!(whole >= 0d)) whole = 0d; // NaN · 음수 방어(강도는 Apply에서 0 이상 · 유한으로 검사했다)
            int result = whole >= int.MaxValue ? int.MaxValue : (int)whole;
            amp.AmpCarry = exact - whole;
            if (amp.AmpCarry < 0d) amp.AmpCarry = 0d;
            amp.AmpReqTotal += amount;
            amp.AmpOutTotal += result;

            // [진단] 출처별 합계 + (대상 · 출처)별 첫 표본 1줄(개발 빌드 전용 — 판정 값을 바꾸지 않는다).
            DiagAmpApplied(amp, target, isUnit, source, amount, result, carryBefore);
            return result;
        }

        /// <summary> 대상의 받는 회복 배율(효과가 없으면 정확히 1 — 확인 · 검증용). </summary>
        /// <param name="targetId">유닛 Id 또는 건물 Id.</param>
        /// <param name="isUnit">true = 유닛 저장소, false = 건물 저장소.</param>
        public float GetHealAmpMultiplier(int targetId, bool isUnit)
        {
            Dictionary<int, List<EffectRecord>> store = isUnit ? _unitEffects : _buildingEffects;
            if (store.Count == 0 || !store.TryGetValue(targetId, out List<EffectRecord> records)) return 1f;
            EffectRecord amp = FindRecord(records, StatusEffectKind.HealAmpMul);
            return amp != null ? amp.Magnitude : 1f;
        }

        /// <summary> 대상에 남아 있는 회복 소수 이월(0 이상 1 미만, 효과가 없으면 0 — 확인 · 검증용). </summary>
        /// <param name="targetId">유닛 Id 또는 건물 Id.</param>
        /// <param name="isUnit">true = 유닛 저장소, false = 건물 저장소.</param>
        public double GetHealAmpCarry(int targetId, bool isUnit)
        {
            Dictionary<int, List<EffectRecord>> store = isUnit ? _unitEffects : _buildingEffects;
            if (store.Count == 0 || !store.TryGetValue(targetId, out List<EffectRecord> records)) return 0d;
            EffectRecord amp = FindRecord(records, StatusEffectKind.HealAmpMul);
            return amp != null ? amp.AmpCarry : 0d;
        }

        // 대상 기록 목록에서 지정 종류 기록을 찾는다(종류마다 하나 — 없으면 null).
        private static EffectRecord FindRecord(List<EffectRecord> records, StatusEffectKind kind)
        {
            for (int i = 0; i < records.Count; i++)
                if (records[i].Kind == kind) return records[i];
            return null;
        }

        // 살아 있는 유닛(없거나 죽었으면 null).
        private UnitData GetAliveUnit(int unitId)
        {
            if (_unitSpawn == null) return null;
            return _unitSpawn.Units.TryGetValue(unitId, out UnitData unit) && unit != null && unit.IsAlive ? unit : null;
        }

        // 살아 있는 건물(목록에 없거나 · 업그레이드로 Id가 바뀌었거나 · 파괴됐으면 null).
        private BuildingData GetAliveBuilding(int buildingId)
        {
            if (_buildingPlacement == null) return null;
            BuildingData building = _buildingPlacement.GetBuilding(buildingId);
            return building != null && building.IsAlive ? building : null;
        }
    }
}
