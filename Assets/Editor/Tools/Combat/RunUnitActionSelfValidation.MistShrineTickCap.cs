// ============================================================================
// RunUnitActionSelfValidation.MistShrineTickCap.cs
// [2026-11-25 MistShrine 물안개 회복 횟수 10회 고정] 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-25/01_19_mistshrine-heal-tick-cap/ (Plan 2절 A-1 ~ A-5 · 7절 · 8절 U-5).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   MistShrine 물안개 1회는 「1초마다 10 회복, 모두 10번」이어야 한다(사용자 확정 기획).
//   그런데 이미 다른 물안개가 깔린 상태에서 시전한 물안개는 박자를 맞추느라(위상 정렬) 첫 회복이 시작 직후에 오고,
//   멀티 Host(서버 틱 약 0.1초)에서 가끔 마지막 프레임에 한 번 더 — **11번** — 회복했다(Research 3절).
//   수정은 물안개마다 회복 횟수를 세어 상한(지속시간 ÷ 1초, 지금 10)에서 멈추는 것이다.
//   이 상황은 「겹친 물안개 + 위상 0.9 이상 + 특정 프레임 간격」이 맞아야만 생겨 사람이 실기에서 일부러 만들기 어렵다
//   (Plan 6-1절 ② · ⑦). 그래서 실제 MistShrineUseCase를 직접 만들어 고정 dt로 끝까지 돌려 숫자로 확인한다.
//
// 무엇을 확인하는가:
//   MT-1 상한 값 — 지속시간 10 → 10, 10.5 → 10(소수점 버림), 9.9999995 → 10(여유값), 0 · 음수 → 0.
//   MT-2 물안개 1회 = 10회 회복 — 고정 dt 0.1초 · 1/60초 × 위상 0 · 0.5 · 0.923 · 0.99(Plan 7절) → 회복 10번 · +100 · 텍스트 3번.
//        (예외 1칸 — 아래 「알려진 별도 결함」.)
//   MT-3 상한이 실제로 막는다 — 실측 재현(위상 0.923 · dt 0.112초 = 실측 10080ms ÷ 90프레임) → 수정 전이라면 11번이었을 물안개가
//        회복 10번 · 막힌 발화 1번(진단 칸 DiagTickCapBlocked)으로 끝난다.
//   MT-4 겹침(Plan 2절 A-4 · 6-1절 ⑦) — 상한에 닿은 물안개 A가 걷히는 그 프레임에, A와 B가 함께 덮는 대상(A가 더 가까움)이
//        B의 회복을 받는다(가로막히지 않는다). 한 프레임에 한 대상이 두 번 회복받는 일은 없다(중첩 해소 유지).
//   MT-5 위상 불변식 — 상한에 닿은 물안개도 누적기가 다른 물안개와 똑같다 · 그때 새로 깐 물안개도 같은 위상에서 출발한다.
//
// 알려진 별도 결함(이 Task에서 고치지 않음 — Plan 4절 3행 「상한으로는 막지 못한다 … 별도 결함으로 보고한다」):
//   정확히 1/60초 고정 dt · 위상 0에서는 float 누적 오차로 「남은 시간」이 「누적기」보다 먼저 0을 넘어,
//   10번째 회복 직전에 물안개가 걷혀 **9번**이 된다(구현 세션 시뮬레이션: 누적기 0.99999756 · 남은 시간 −0.0000195).
//   상한은 「많아지는 것」만 막으므로 이 칸은 「상한(10)을 넘지 않는다」만 확인한다. 고칠지는 사용자 결정 사항이다.
//
// 실제 씬 · 멀티 전송 · HP 동기화 · 화면 텍스트는 여기서 확인하지 않는다(사용자 실기 항목 — Plan 9절).
// 모든 fixture는 새 인스턴스를 만든다. 실행 중(Play)에는 돌지 않는다.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hexiege.Application;
using Hexiege.Domain;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        // 실제 설정 에셋(SpecialAttackConfig.asset)과 같은 임시값 — 회복 10/초 · 지속 10초 · 쿨다운 20초 · 반경 3 · 텍스트 3초.
        private const float MistCapHealPerSecond = 10f;
        private const float MistCapDuration = 10f;
        private const float MistCapCooldown = 20f;
        private const float MistCapRadius = 3f;
        private const float MistCapTextInterval = 3f;

        /// <summary>
        /// MistShrine 물안개 회복 횟수 상한 회귀 검증 진입점(MT-1 ~ MT-5).
        /// </summary>
        private static void ValidateMistShrineHealTickCap()
        {
            Require(!UnityEngine.Application.isPlaying, "MistShrine tick-cap fixture must run outside a live match.");
            // 물안개 시전 · 틱은 「서버(싱글 포함)」에서만 돈다 — 네트워크 상태가 남아 있으면 Activate/TickMists가 그냥 빠진다.
            Require(!NetworkContext.IsNetworkActive, "MT-0 NetworkContext must be inactive (single / local-server path) for the mist fixture.");
            // Tank HP를 10000으로 고정한다(빈 표에서는 폴백 HP 10이라 한 번 회복하면 가득 차 횟수를 셀 수 없다).
            using var fixtureStats = new SupportFixtureStatsScope();

            ValidateMistCapValue();          // MT-1
            ValidateMistCapTenHeals();       // MT-2
            ValidateMistCapBlocksEleventh(); // MT-3
            ValidateMistCapOverlapHandOff(); // MT-4 · MT-5
            Debug.Log("[UAS-DIAG][MIST-CAP] self-validation PASS: tick cap = floor(duration / 1 s) (10 → 10, 10.5 → 10, 9.9999995 → 10, 0 / negative → 0) (MT-1), one mist heals exactly 10 times (+100, text 3) at fixed 0.1 s and 1/60 s with phase 0 / 0.5 / 0.923 / 0.99 — except the known 1/60 s phase-0 float case (9, reported separately), which only checks ≤ 10 (MT-2), the observed phase 0.923 / 0.112 s case stops at 10 with 1 blocked fire (MT-3), a capped mist leaving in its blocked frame hands the shared target to the overlapping mist (healed once by B, never twice in a frame) (MT-4), capped mists keep the shared phase and a mist cast meanwhile starts on it (MT-5).");
        }

        // ── fixture ─────────────────────────────────────────────────────

        /// <summary>
        /// 물안개 fixture 한 벌 — 새 유닛 · 건물 UseCase와 새 MistShrineUseCase.
        /// 그리드는 12×12 FlatTop이다(even-q 배치라 (q, 0)은 q = 0 ~ 11 모두 타일이 있다).
        /// 좌표 변환은 ProjectileFixtureMapper — 헥스 (q, r)를 그대로 월드 (x = q, z = r)로 쓴다 → 거리는 (q, r) 평면 유클리드 거리.
        /// </summary>
        private sealed class MistCapFixture
        {
            public readonly UnitSpawnUseCase Spawn;
            public readonly BuildingPlacementUseCase Buildings;
            public readonly MistShrineUseCase Mist;

            public MistCapFixture(float duration = MistCapDuration)
            {
                var grid = new HexGrid(12, 12, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(grid);
                Buildings = new BuildingPlacementUseCase(grid);
                Mist = new MistShrineUseCase(Buildings, Spawn, new ProjectileFixtureMapper(),
                    MistCapHealPerSecond, duration, MistCapCooldown, MistCapRadius, MistCapTextInterval);
                // [진단 MIST-DIAG] 이 fixture에서는 진단 줄을 끈다(경기 종료 정리를 미리 불러 「줄을 더 남기지 않음」 상태로).
                //   에디터에서 한 메서드 안의 반복은 Time.frameCount가 그대로라, 그대로 두면 매 TickMists가 「같은 프레임 2회 통과」로 보여
                //   DoubleTick 경고 · MistEnded 경고 줄이 쌓인다(게임 결함이 아니라 fixture 특성). 진단 칸 집계(DiagTickCapBlocked 등)는 그대로 돈다.
                Mist.DiagGameEnd();
            }

            /// <summary>파란 팀 MistShrine을 (q, 0) 타일에 놓는다(체력은 가득 → 자기 자신은 회복 대상이 아니다 — 규칙 5).</summary>
            public BuildingData Shrine(int id, int q)
            {
                BuildingData shrine = Buildings.PlaceBuildingWithId(id, BuildingType.HealShrine, TeamId.Blue, new HexCoord(q, 0), RaceId.Transcendence);
                Require(shrine != null && shrine.IsAlive && shrine.Type == BuildingType.HealShrine,
                    "MT-0 fixture MistShrine " + id + " must be placed alive at (" + q + ", 0).");
                return shrine;
            }

            /// <summary>HP 1000까지 깎은 파란 팀 Tank를 (q, r) 판정 좌표에 둔다(최대 HP 10000 — 회복 10번 = +100이 넉넉히 들어간다).</summary>
            public UnitData WoundedTank(int id, int q, int r)
            {
                UnitData unit = Spawn.SpawnUnitWithId(id, UnitType.Tank, TeamId.Blue, new HexCoord(0, 0));
                Require(unit != null && unit.MaxHp == SupportFixtureStatsScope.VictimTankMaxHp,
                    "MT-0 fixture Tank " + id + " must spawn with the pinned max HP.");
                unit.Position = new HexCoord(q, r); // 물안개 범위 판정은 Position만 읽는다(다른 fixture와 같은 방식).
                unit.TakeDamage(unit.MaxHp - 1000);
                Require(unit.Hp == 1000, "MT-0 fixture Tank " + id + " must be wounded to 1000.");
                return unit;
            }

            /// <summary>시전 — 성공해야 한다(살아 있는 MistShrine · 쿨다운 없음).</summary>
            public void Cast(int shrineId)
            {
                Require(Mist.Activate(shrineId), "MT-0 MistShrine " + shrineId + " must cast (alive, correct type, no cooldown).");
            }
        }

        // ── 비공개 상태 읽기(리플렉션 — 판정 코드를 공개하지 않으려고 검사에서만 읽는다) ──

        private static readonly FieldInfo MistCapActiveMistsField =
            typeof(MistShrineUseCase).GetField("_activeMists", SupportNonPublic);
        private static readonly FieldInfo MistCapCapField =
            typeof(MistShrineUseCase).GetField("_healTickCap", SupportNonPublic);

        // 지정 신전이 만든 물안개 객체(없으면 null). 객체 참조를 쥐고 있으면 목록에서 지워진 뒤에도 마지막 값을 읽을 수 있다.
        private static object MistCapFindMist(MistShrineUseCase mist, int shrineId)
        {
            Require(MistCapActiveMistsField != null, "MT-0 MistShrineUseCase._activeMists must exist.");
            var list = (IList)MistCapActiveMistsField.GetValue(mist);
            foreach (object m in list)
            {
                if (MistCapField<int>(m, "SourceBuildingId") == shrineId) return m;
            }
            return null;
        }

        private static T MistCapField<T>(object activeMist, string name)
        {
            FieldInfo f = activeMist.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Require(f != null, "MT-0 MistShrineUseCase.ActiveMist." + name + " must exist.");
            return (T)f.GetValue(activeMist);
        }

        private static int MistCapCapOf(float duration)
        {
            Require(MistCapCapField != null, "MT-0 MistShrineUseCase._healTickCap must exist.");
            return (int)MistCapCapField.GetValue(new MistCapFixture(duration).Mist);
        }

        // ── MT-1 ────────────────────────────────────────────────────────

        /// <summary> MT-1 — 상한 = 지속시간 ÷ 1초 소수점 버림(여유값 포함) · 0 이하는 0. </summary>
        private static void ValidateMistCapValue()
        {
            Require(MistCapCapOf(10f) == 10, "MT-1 duration 10 s → cap 10.");
            Require(MistCapCapOf(10.5f) == 10, "MT-1 duration 10.5 s → cap 10 (floor, not round — every phase gets the same count, Plan U-2).");
            Require(MistCapCapOf(9.9999995f) == 10, "MT-1 duration 9.9999995 s → cap 10 (float margin before floor).");
            Require(MistCapCapOf(0f) == 0 && MistCapCapOf(-1f) == 0, "MT-1 duration 0 / negative → cap 0 (no negative cap).");
        }

        // ── MT-2 · MT-3 공용 — 위상 P를 받은 물안개 1개를 끝까지 돌린다 ──

        /// <summary>
        /// 위상 P를 받은 물안개(신전 2)를 고정 dt로 끝까지 돌리고, 그 물안개가 대상에게 준 회복을 센다.
        /// P &gt; 0이면 먼저 멀리 떨어진 신전 1의 물안개를 깔고 dt = P로 한 번만 진행시켜(1초 미만이라 발화 없음) 누적기를 P로 만든 뒤
        /// 신전 2를 시전한다 → 위상 정렬로 신전 2의 물안개가 P에서 출발한다(실제 Activate 경로 그대로).
        /// </summary>
        private static (int heals, int gained, int texts, object mistRef) MistCapRunOne(float phase, float dt)
        {
            var f = new MistCapFixture();
            f.Shrine(1, 0);  // 위상을 만들 앞선 물안개(대상 없음 — 대상과 8 떨어져 반경 3 밖)
            f.Shrine(2, 8);  // 검사 대상 물안개
            UnitData target = f.WoundedTank(9900, 8, 0);

            if (phase > 0f)
            {
                f.Cast(1);
                f.Mist.TickMists(phase);
            }
            f.Cast(2);
            object mistRef = MistCapFindMist(f.Mist, 2);
            Require(mistRef != null, "MT-2 the cast mist must be in the active list.");
            Require(MistCapField<float>(mistRef, "TickAccum") == phase,
                "MT-2 the new mist must start on the shared phase " + phase + " (got " + MistCapField<float>(mistRef, "TickAccum") + ").");

            int heals = 0, texts = 0;
            using (var capture = new RegenHealedCapture())
            {
                for (int i = 0; i < 100000 && f.Mist.IsMistActive(2); i++) f.Mist.TickMists(dt);
                Require(!f.Mist.IsMistActive(2), "MT-2 the mist must expire.");
                foreach (EntityHealedEvent e in capture.Events)
                {
                    if (!e.IsUnit || e.Entity == null || e.Entity.Id != target.Id) continue;
                    Require(e.HealerId == 2 && !e.HealerIsUnit, "MT-2 the target is healed only by shrine 2 (got healer " + e.HealerId + ").");
                    heals++;
                    if (e.ShowText) texts++;
                }
            }
            return (heals, target.Hp - 1000, texts, mistRef);
        }

        // ── MT-2 ────────────────────────────────────────────────────────

        /// <summary> MT-2 — 고정 dt 0.1초 · 1/60초 × 위상 0 · 0.5 · 0.923 · 0.99 → 회복 10번(알려진 1칸 제외). </summary>
        private static void ValidateMistCapTenHeals()
        {
            float[] dts = { 0.1f, 1f / 60f };
            float[] phases = { 0f, 0.5f, 0.923f, 0.99f };
            foreach (float dt in dts)
            {
                foreach (float phase in phases)
                {
                    var (heals, gained, texts, mistRef) = MistCapRunOne(phase, dt);
                    string label = "dt " + dt.ToString("R") + ", phase " + phase.ToString("R");
                    int counted = MistCapField<int>(mistRef, "HealTickCount");
                    Require(heals <= 10 && counted <= 10 && gained == heals * 10,
                        "MT-2 a mist never heals more than the cap 10 (" + label + ": heals " + heals + ", HealTickCount " + counted + ", +" + gained + ").");

                    // 알려진 별도 결함(파일 머리 설명) — 정확히 1/60초 · 위상 0은 float 오차로 9번. 이 Task 범위 밖이라 「≤ 10」만 본다.
                    bool knownFloatShortCase = phase == 0f && dt == 1f / 60f;
                    if (knownFloatShortCase) continue;

                    Require(heals == 10 && gained == 100 && counted == 10,
                        "MT-2 one mist heals exactly 10 times (+100) (" + label + ": heals " + heals + ", +" + gained + ", HealTickCount " + counted + ").");
                    Require(texts == 3, "MT-2 the floating text still shows on heals 3 / 6 / 9 (" + label + ": texts " + texts + ").");
                }
            }
        }

        // ── MT-3 ────────────────────────────────────────────────────────

        /// <summary> MT-3 — 실측 재현(위상 0.923 · dt 0.112초): 수정 전 11번이던 물안개가 10번 + 막힌 발화 1번. </summary>
        private static void ValidateMistCapBlocksEleventh()
        {
            var (heals, gained, texts, mistRef) = MistCapRunOne(0.923f, 0.112f);
            int blocked = MistCapField<int>(mistRef, "DiagTickCapBlocked");
            Require(heals == 10 && gained == 100 && MistCapField<int>(mistRef, "HealTickCount") == 10,
                "MT-3 the observed case (phase 0.923, 0.112 s frames) heals exactly 10 times (+100) (heals " + heals + ", +" + gained + ").");
            // 진단 칸은 에디터(UNITY_EDITOR)에서 채워진다 — 1이면 「11번째 차례가 실제로 왔고 상한이 막았다」는 뜻이다(수정 전이라면 11번).
            Require(blocked == 1, "MT-3 the cap really blocked the 11th fire in this case (DiagTickCapBlocked " + blocked + ").");
            Require(texts == 3, "MT-3 the blocked fire adds no text (texts " + texts + ").");
        }

        // ── MT-4 · MT-5 ─────────────────────────────────────────────────

        /// <summary>
        /// MT-4 · MT-5 — 상한에 닿은 물안개 A(신전 2)가 걷히는 프레임에, A와 B(신전 3)가 함께 덮는 대상 X(A가 더 가까움)가 B의 회복을 받는다.
        /// 배치: C(신전 1, 위상 만들기) (0, 0) · A (6, 0) · B (8, 0) · D(신전 4, MT-5 새 시전) (11, 0) · X (6, 1) — A까지 1, B까지 √5 ≈ 2.24(둘 다 반경 3 안),
        /// C · D까지는 3보다 멀다. 프레임 간격 0.112초 · A 위상 0.923 → A의 11번째 차례가 A가 걷히는 프레임에 온다(MT-3과 같은 조건).
        /// </summary>
        private static void ValidateMistCapOverlapHandOff()
        {
            const float dt = 0.112f;
            var f = new MistCapFixture();
            f.Shrine(1, 0);
            f.Shrine(2, 6);
            f.Shrine(3, 8);
            f.Shrine(4, 11);
            UnitData x = f.WoundedTank(9910, 6, 1);

            f.Cast(1);
            f.Mist.TickMists(0.923f);  // C의 누적기 0.923(발화 없음)
            f.Cast(2);                 // A — 위상 0.923에서 출발
            object a = MistCapFindMist(f.Mist, 2);
            Require(a != null && MistCapField<float>(a, "TickAccum") == 0.923f, "MT-4 mist A must start on phase 0.923.");

            object b = null;
            bool castD = false, checkedCappedPhase = false, handOffChecked = false;
            int healsFromA = 0;
            using (var capture = new RegenHealedCapture())
            {
                for (int frame = 1; frame <= 400 && !handOffChecked; frame++)
                {
                    bool aWasActive = f.Mist.IsMistActive(2);
                    int before = capture.Events.Count;
                    f.Mist.TickMists(dt);

                    // 이번 프레임에 X가 받은 회복(중첩 해소 — 한 프레임에 최대 1번).
                    int xHeals = 0, lastHealer = -1;
                    for (int i = before; i < capture.Events.Count; i++)
                    {
                        EntityHealedEvent e = capture.Events[i];
                        if (!e.IsUnit || e.Entity == null || e.Entity.Id != x.Id) continue;
                        xHeals++;
                        lastHealer = e.HealerId;
                        if (e.HealerId == 2) healsFromA++;
                    }
                    Require(xHeals <= 1, "MT-4 the shared target is never healed twice in one frame (frame " + frame + ": " + xHeals + ").");

                    // 약 3초 뒤 B를 깐다(같은 위상 — A보다 늦게 끝나므로 A가 걷히는 프레임에 B는 아직 상한 아래다).
                    if (frame == 27)
                    {
                        f.Cast(3);
                        b = MistCapFindMist(f.Mist, 3);
                        Require(b != null && MistCapField<float>(b, "TickAccum") == MistCapField<float>(a, "TickAccum"),
                            "MT-4 mist B must start on A's phase.");
                    }

                    // MT-5 — A가 상한(10)에 닿은 뒤 아직 걷히기 전: 누적기가 B와 같고, 이때 새로 깐 D도 같은 위상에서 출발한다.
                    if (b != null && f.Mist.IsMistActive(2) && MistCapField<int>(a, "HealTickCount") == 10)
                    {
                        float accA = MistCapField<float>(a, "TickAccum");
                        Require(accA == MistCapField<float>(b, "TickAccum"),
                            "MT-5 a capped mist keeps the same tick phase as the others (A " + accA + ", B " + MistCapField<float>(b, "TickAccum") + ").");
                        if (!castD)
                        {
                            castD = true;
                            f.Cast(4);
                            object d = MistCapFindMist(f.Mist, 4);
                            Require(d != null && MistCapField<float>(d, "TickAccum") == accA,
                                "MT-5 a mist cast while a capped mist is first in the list starts on the shared phase.");
                        }
                        checkedCappedPhase = true;
                    }

                    // MT-4 — A가 이번 프레임에 걷혔다 = 상한 때문에 막힌 마지막 프레임. X는 B의 회복을 받아야 한다.
                    if (aWasActive && !f.Mist.IsMistActive(2))
                    {
                        Require(MistCapField<int>(a, "DiagTickCapBlocked") == 1 && MistCapField<int>(a, "HealTickCount") == 10,
                            "MT-4 setup: A must leave in the frame where the cap blocked its 11th fire (blocked " + MistCapField<int>(a, "DiagTickCapBlocked") + ").");
                        Require(xHeals == 1 && lastHealer == 3,
                            "MT-4 in that frame the shared target is healed by the overlapping mist B, not blocked by the capped A (heals " + xHeals + ", healer " + lastHealer + ").");
                        handOffChecked = true;
                    }
                }
            }
            Require(handOffChecked, "MT-4 mist A must expire within the fixture loop.");
            Require(checkedCappedPhase && castD, "MT-5 the capped-but-alive window must be observed.");
            Require(healsFromA == 10, "MT-4 the target got exactly 10 heals from A while A was its nearest mist (got " + healsFromA + ").");
        }
    }
}
