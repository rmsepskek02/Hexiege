// ============================================================================
// SupportSkillLoadoutSetup.cs
// [1회성 에디터 스크립트] 확정 지원 스킬 15개의 SkillDefinition 자산을 만들고,
// SkillLoadoutConfig.asset 의 세 종족 로드아웃(슬롯 1~5)을 그 15개로 바꿔 넣는다.
//
// 왜 스크립트로 하나(초급자용 설명):
//   스킬 자산 15개를 Inspector에서 손으로 만들고 값을 채우면 실수(오타 · 순서 틀림)가 나기 쉽다.
//   WORKFLOW [5-2] 규칙대로 "메뉴 한 번 누르면 끝나는" 에디터 스크립트로 만든다.
//   .asset 파일을 텍스트로 직접 쓰지 않고 Unity API(AssetDatabase · SerializedObject)로 만들기 때문에
//   GUID · 직렬화 형식이 Unity가 만든 것과 똑같다.
//
// 실행 방법:
//   Unity 상단 메뉴 → Hexiege → Setup → Skills → Create Support Skill Assets And Replace Loadout
//   Console에 "[SupportSkillSetup] 완료" 줄이 나오면 성공. 오류가 나면 "[SupportSkillSetup] 중단" 줄과
//   그 이유가 나오고, 그 경우 로드아웃은 바꾸지 않는다(자산 검사는 쓰기 전에 모두 끝낸다).
//
// 여러 번 실행해도 결과가 같다(멱등):
//   - 자산이 이미 있으면 새로 만들지 않고 값만 아래 표 값으로 다시 덮어쓴다(GUID 유지).
//   - 로드아웃은 매번 같은 15개 · 같은 순서로 다시 넣는다.
//
// 무엇을 바꾸지 않나:
//   - 기존 임시 스킬 자산 18개(Skill_*_Bomb/Freeze/Buff/Slow/Heal/Dot)는 지우지도 고치지도 않는다.
//     로드아웃에서 참조만 빠진다. 되돌리려면 실행 로그의 "교체 전 GUID" 줄에 찍힌 자산을 다시 넣으면 된다.
//   - 코드 동작은 바꾸지 않는다. 이 스크립트는 데이터만 만든다.
//
// 값의 출처:
//   - 슬롯 순서 · 이름: Task Plan 3-5 표 = 상위 Plan(_Tasks/2026-11-02/14_25_race-support-skills-design) 1-1~1-3.
//   - 포격 지원: 사용 가능 = true, 착탄 지연 0.4초, 발동 VFX = vfx_flightfacility_bombardment.prefab,
//     VFX 기준 반경 2.0. 피해 · 반경 · 쿨다운은 기존 Skill_Human_Bomb 값(반경 2 · 피해 150 · 쿨 10).
//   - 나머지 14개: 사용 가능 = false(사용자 결정 Q4 — 이번엔 포격만 사용 가능).
//     수치는 밸런싱 전 "자리표시"다. 조준이 지점이면 기존 Bomb 값(타입 A), 즉시(맵 전체)면
//     상태 종류 None인 타입 C(쿨 15)로 넣었다 — 사용 불가라 실제로는 발동되지 않는다.
//   - [2026-11-04 EMP Task 커밋 4 — 사용자 결정 Q10 「EMP 줄만 새 값으로」] EMP(인간 슬롯 3)는 자리표시에서
//     실제 값으로 바뀌었다: 사용 가능 = true, 타입 = 지점 범위 적 방어탑 무력화(AreaTowerDisable = 3),
//     무력화 시간 5초(Q1 — 시험용 임시 값, 최종 값은 밸런싱), 피해 0, 발동 VFX = vfx_emp_pulse.prefab.
//     반경 2 · VFX 기준 반경 2 · 착탄 지연 0(Q3 동시 시작) · 지점 조준 · 쿨 10은 자리표시 값 그대로 유지.
//     → 이 메뉴를 다시 실행해도 EMP가 자리표시(타입 A · 피해 150 · 사용 불가)로 되돌아가지 않는다(Plan 위험 R-9).
//     따라서 "사용 가능" 스킬은 이제 2개(포격 지원 · EMP)이고, 자리표시는 13개다.
//     Task: Assets/_Project/Docs/_Tasks/2026-11-04/03_54_emp-tower-disable/Plan.md 4절 커밋 4 · 7-1.
//   - 실행하면 값을 쓰기 전에 "표와 다른 칸"을 자산마다 로그로 먼저 남긴다(값이 바뀌는 자산 · 그대로인 자산 개수).
//     EMP 줄만 바꾼 뒤 처음 다시 실행하면 Skill_Human_Emp 하나만 "바뀜"으로 나와야 정상이다.
//   - 가칭 이름(화염의 축복 · 대지의 갑주)은 사용자 결정 Q6대로 그대로 쓴다.
//
// 사용 후 정리:
//   실행 · 확인이 끝나면 이 파일(과 .meta)을 지운다(WORKFLOW [5-2] · [4] 절차 — 삭제는 사용자 승인 후).
//   런타임 코드는 이 파일을 참조하지 않는다.
//
// 위치: Assets/Editor/Setup (Assembly-CSharp-Editor). 네임스페이스 Hexiege.EditorTools(셋업 스크립트 관례).
// Task: Assets/_Project/Docs/_Tasks/2026-11-03/05_24_support-skill-foundation-bombardment/Plan.md 커밋 2.
// ============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 확정 지원 스킬 15개 자산 생성 + 세 종족 로드아웃 교체를 수행하는 1회성 에디터 메뉴.
    /// </summary>
    public static class SupportSkillLoadoutSetup
    {
        // ── 경로 상수 ────────────────────────────────────────────────────────
        private const string MenuPath = "Hexiege/Setup/Skills/Create Support Skill Assets And Replace Loadout";
        private const string LogPrefix = "[SupportSkillSetup]";

        // 새 스킬 자산을 두는 폴더(기존 임시 스킬 자산과 같은 곳).
        private const string SkillFolder = "Assets/_Project/Resources/Config/Skills";
        private const string LoadoutPath = SkillFolder + "/SkillLoadoutConfig.asset";

        // 포격 지원 발동 VFX 프리팹과 그 GUID(Research 4-1에서 확인한 값). GUID까지 맞는지 확인해
        // 같은 이름의 다른 파일을 잘못 연결하지 않게 한다.
        private const string BombardmentVfxPath = "Assets/_Project/Prefabs/VFX/Buildings/vfx_flightfacility_bombardment.prefab";
        private const string BombardmentVfxGuid = "8d261a35773d38c449b73148b79cfa01";

        // [EMP 커밋 4] EMP 발동 VFX(V5 — 조준점에서 퍼지는 고리) 프리팹과 그 GUID(vfx_emp_pulse.prefab.meta 에서 확인).
        //   포격 VFX와 같은 이유로 경로 + GUID를 함께 확인한다.
        private const string EmpPulseVfxPath = "Assets/_Project/Prefabs/VFX/Skills/vfx_emp_pulse.prefab";
        private const string EmpPulseVfxGuid = "e246a9e13f016bd4196db7e80a16e362";

        // 표에서 "사용 가능"이어야 하는 스킬 수. [EMP 커밋 4] 1(포격 지원) → 2(포격 지원 · EMP).
        //   표를 고치다 실수로 다른 자리표시가 켜지거나 EMP가 꺼지면 ValidateSpecs 가 쓰기 전에 멈춘다.
        private const int ExpectedAvailableCount = 2;

        // ── SkillDefinition 의 직렬화 칸 이름(private 필드 이름과 같아야 한다) ──
        //   SerializedObject로 private 칸에 값을 넣기 때문에 이름이 틀리면 값이 안 들어간다.
        //   그래서 쓰기 전에 모든 이름이 실제로 있는지 먼저 확인한다(ValidateFieldNames).
        private const string F_Icon = "_icon";
        private const string F_DisplayName = "_displayName";
        private const string F_Description = "_description";
        private const string F_IsAvailable = "_isAvailable";
        private const string F_AimType = "_aimType";
        private const string F_Cooldown = "_cooldown";
        private const string F_Mechanic = "_mechanic";
        private const string F_Radius = "_radius";
        private const string F_Duration = "_duration";
        private const string F_TotalDamage = "_totalDamage";
        private const string F_DamagePerSecond = "_damagePerSecond";
        private const string F_StatusKind = "_statusKind";
        private const string F_Magnitude = "_magnitude";
        private const string F_TargetsAllies = "_targetsAllies";
        private const string F_ImpactDelay = "_impactDelay";
        private const string F_ActivationVfx = "_activationVfxPrefab";
        private const string F_VfxReferenceRadius = "_vfxReferenceRadius";

        private static readonly string[] AllFieldNames =
        {
            F_Icon, F_DisplayName, F_Description, F_IsAvailable, F_AimType, F_Cooldown, F_Mechanic,
            F_Radius, F_Duration, F_TotalDamage, F_DamagePerSecond, F_StatusKind, F_Magnitude,
            F_TargetsAllies, F_ImpactDelay, F_ActivationVfx, F_VfxReferenceRadius
        };

        // SkillLoadoutConfig 의 직렬화 칸 이름.
        private const string F_Entries = "_entries";
        private const string F_EntryRace = "race";
        private const string F_EntrySkills = "skills";

        // 종족당 슬롯 수(스킬 규칙 4 — 최대 5).
        private const int SlotsPerRace = 5;

        // VFX 기준 반경 기본값(SkillDefinition 초기값과 같은 2.0 — Plan 3-4).
        private const float DefaultVfxReferenceRadius = 2.0f;

        // ====================================================================
        // 스킬 한 개의 "넣을 값" 묶음
        // ====================================================================

        /// <summary>
        /// 자산 하나에 넣을 값. 아래 BuildSpecs()가 15개를 슬롯 순서대로 만든다.
        /// </summary>
        private sealed class SkillSpec
        {
            public string FileName;        // 자산 파일 이름(확장자 제외).
            public RaceId Race;            // 어느 종족 로드아웃에 들어가나.
            public int Slot;               // 슬롯 번호(0-based, 0~4).
            public string DisplayName;     // 스킬 이름(한글).
            public string Description;     // 스킬 설명(한두 문장).
            public bool IsAvailable;       // 사용 가능 여부.
            public SkillAimType AimType;
            public SkillMechanicType Mechanic;
            public float Cooldown;
            public float Radius;
            public float Duration;
            public int TotalDamage;
            public float DamagePerSecond;
            public int StatusKind;
            public float Magnitude;
            public bool TargetsAllies;
            public float ImpactDelay;      // 착탄 지연(초).
            public bool UseBombardmentVfx; // true면 포격 VFX 프리팹을 연결.
            public bool UseEmpPulseVfx;    // [EMP 커밋 4] true면 EMP 고리 VFX 프리팹을 연결(포격 VFX와 동시에 켜면 ValidateSpecs가 멈춘다).
        }

        // 자리표시 값 — 지점 조준 스킬용(기존 Skill_Human_Bomb 값: 타입 A · 반경 2 · 피해 150 · 쿨 10).
        private static SkillSpec PointPlaceholder(string file, RaceId race, int slot, string name, string desc)
        {
            return new SkillSpec
            {
                FileName = file, Race = race, Slot = slot, DisplayName = name, Description = desc,
                IsAvailable = false,
                AimType = SkillAimType.PointTarget,
                Mechanic = SkillMechanicType.InstantAreaDamage,
                Cooldown = 10f, Radius = 2f, Duration = 0f, TotalDamage = 150, DamagePerSecond = 0f,
                StatusKind = (int)StatusEffectKind.None, Magnitude = 0f, TargetsAllies = false,
                ImpactDelay = 0f, UseBombardmentVfx = false
            };
        }

        // 자리표시 값 — 즉시(맵 전체) 스킬용(타입 C · 상태 종류 None · 쿨 15 = 기존 Buff 쿨다운).
        //   상태 종류 None이면 실수로 켜져도 UnitCombatUseCase.ApplySkillGlobalStatus가 바로 return해
        //   아무 효과가 없다(Research 4-2). 사용 불가라 원래 발동도 거부된다.
        private static SkillSpec InstantPlaceholder(string file, RaceId race, int slot, string name, string desc, bool allies)
        {
            return new SkillSpec
            {
                FileName = file, Race = race, Slot = slot, DisplayName = name, Description = desc,
                IsAvailable = false,
                AimType = SkillAimType.Instant,
                Mechanic = SkillMechanicType.GlobalStatusChange,
                Cooldown = 15f, Radius = 0f, Duration = 0f, TotalDamage = 0, DamagePerSecond = 0f,
                StatusKind = (int)StatusEffectKind.None, Magnitude = 0f, TargetsAllies = allies,
                ImpactDelay = 0f, UseBombardmentVfx = false
            };
        }

        /// <summary>
        /// 확정 스킬 15개의 값 표. 순서 = 종족(인간 · 정령 · 초월) × 슬롯 1~5(Plan 3-5 표).
        /// </summary>
        private static List<SkillSpec> BuildSpecs()
        {
            var h = RaceId.Human;
            var s = RaceId.Spirit;
            var t = RaceId.Transcendence;

            // 포격 지원 — 이번 Task에서 유일하게 "사용 가능". 기존 Bomb 수치 + 착탄 지연 0.4 + 포격 VFX.
            var bombardment = new SkillSpec
            {
                FileName = "Skill_Human_Bombardment", Race = h, Slot = 0,
                DisplayName = "포격 지원",
                Description = "지정한 지점에 포탄 7발이 한꺼번에 떨어져, 착탄 순간 범위 안의 적 유닛과 적 건물에 피해를 준다.",
                IsAvailable = true,
                AimType = SkillAimType.PointTarget,
                Mechanic = SkillMechanicType.InstantAreaDamage,
                Cooldown = 10f, Radius = 2f, Duration = 0f, TotalDamage = 150, DamagePerSecond = 0f,
                StatusKind = (int)StatusEffectKind.None, Magnitude = 0f, TargetsAllies = false,
                ImpactDelay = 0.4f, UseBombardmentVfx = true
            };

            // EMP — [2026-11-04 EMP Task 커밋 4, 사용자 결정 Q10] 자리표시에서 실제 값으로 바꾼 두 번째 "사용 가능" 스킬.
            //   타입 3(AreaTowerDisable): 조준 지점 반경 안의 적 방어탑을 Duration 초 동안 공격 못 하게 한다
            //   (실행기 AreaTowerDisableExecutor 는 Duration ≤ 0 이거나 Radius ≤ 0 이면 아무것도 하지 않으므로 둘 다 0보다 커야 한다).
            //   피해 0 — EMP는 탑을 멈추기만 하고 체력을 깎지 않는다(타입 3 실행기는 피해 칸을 읽지 않지만,
            //   자산을 보는 사람이 "피해가 있는 스킬"로 오해하지 않게 0으로 둔다).
            //   착탄 지연 0 — V5 고리와 V6 탑 무력화를 동시에 시작한다(Q3). 지연 예약은 타입 A만 쓰므로 어차피 영향 없다.
            //   반경 2 · VFX 기준 반경 2 · 지점 조준 · 쿨 10 은 자리표시 시절 값 그대로(자산 GUID도 그대로 — 같은 파일을 갱신).
            var emp = new SkillSpec
            {
                FileName = "Skill_Human_Emp", Race = h, Slot = 2,
                DisplayName = "EMP",
                Description = "지정한 범위 안의 적 방어탑을 잠시 무력화한다.",
                IsAvailable = true,
                AimType = SkillAimType.PointTarget,
                Mechanic = SkillMechanicType.AreaTowerDisable,
                Cooldown = 10f, Radius = 2f, Duration = 5f, TotalDamage = 0, DamagePerSecond = 0f,
                StatusKind = (int)StatusEffectKind.None, Magnitude = 0f, TargetsAllies = false,
                ImpactDelay = 0f, UseBombardmentVfx = false, UseEmpPulseVfx = true
            };

            return new List<SkillSpec>
            {
                // ── 인간(race 0) — FlightFacility ──
                bombardment,
                PointPlaceholder("Skill_Human_AirStrike", h, 1, "공습",
                    "지정한 방향의 직선 범위를 폭격해 적 유닛과 적 건물에 피해를 준다."),
                emp,
                PointPlaceholder("Skill_Human_SmokeBomb", h, 3, "연막탄",
                    "연막 안의 아군 유닛이 원거리 단일 공격을 확률로 피한다."),
                InstantPlaceholder("Skill_Human_TacticalOrder", h, 4, "전술 지령",
                    "맵 전체 아군의 공격력과 생산 속도를 높인다.", allies: true),

                // ── 정령(race 1) — MagicSpirit ──
                PointPlaceholder("Skill_Spirit_Meteor", s, 0, "메테오",
                    "지정한 지점에 운석이 떨어져, 착탄 순간 범위 안의 적에게 피해와 화상을 준다."),
                PointPlaceholder("Skill_Spirit_FrostPrison", s, 1, "서리 감옥",
                    "바닥 얼음이 남아 있는 동안 구역 안의 적 유닛을 얼린다."),
                InstantPlaceholder("Skill_Spirit_FlameBlessing", s, 2, "화염의 축복",
                    "맵 전체 아군 유닛의 공격력을 높인다.", allies: true),
                InstantPlaceholder("Skill_Spirit_EarthArmor", s, 3, "대지의 갑주",
                    "맵 전체 아군 유닛이 받는 피해를 줄인다.", allies: true),
                // 원소 공명 — 기능 보류 · 이름만 등록(상위 결정 15). 대상이 정해지지 않아 targetsAllies=false.
                InstantPlaceholder("Skill_Spirit_ElementalResonance", s, 4, "원소 공명",
                    "기능 기획 보류 중인 스킬이다(이름만 등록).", allies: false),

                // ── 초월(race 2) — WillowShrine ──
                PointPlaceholder("Skill_Trans_SapSwamp", t, 0, "수액 늪",
                    "지정한 범위 바닥에 수액을 깔아, 안에 있는 적 유닛을 크게 느리게 한다."),
                InstantPlaceholder("Skill_Trans_RootsOfRegeneration", t, 1, "재생의 뿌리",
                    "맵 전체 아군 유닛과 건물에 체력 재생을 부여하고 증폭한다.", allies: true),
                InstantPlaceholder("Skill_Trans_SacredTreeAwakening", t, 2, "신목의 각성",
                    "맵 전체 아군 유닛과 건물에 쉴드를 씌운다.", allies: true),
                InstantPlaceholder("Skill_Trans_ForestRoar", t, 3, "숲의 포효",
                    "맵 전체 아군 유닛의 공격력을 높인다.", allies: true),
                InstantPlaceholder("Skill_Trans_ForestTailwind", t, 4, "숲의 순풍",
                    "맵 전체 아군 유닛의 이동 속도를 높인다.", allies: true),
            };
        }

        // ====================================================================
        // 메뉴 진입점
        // ====================================================================

        /// <summary>
        /// 메뉴 실행: ① 사전 검사(쓰기 전) → ② 자산 15개 생성/갱신 → ③ 로드아웃 교체 → ④ 다시 읽어 확인.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LogPrefix} 중단 — Play 모드에서는 실행할 수 없다. Play를 끄고 다시 실행하세요.");
                return;
            }

            List<SkillSpec> specs = BuildSpecs();

            // ① 사전 검사 — 하나라도 실패하면 아무것도 쓰지 않는다.
            if (!ValidateSpecs(specs)) return;
            if (!ValidateFieldNames()) return;

            GameObject bombardmentVfx = LoadBombardmentVfx();
            if (bombardmentVfx == null) return;

            // [EMP 커밋 4] EMP 고리 VFX도 쓰기 전에 경로 + GUID 확인.
            GameObject empPulseVfx = LoadEmpPulseVfx();
            if (empPulseVfx == null) return;

            SkillLoadoutConfig loadout = AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(LoadoutPath);
            if (loadout == null)
            {
                Debug.LogError($"{LogPrefix} 중단 — 로드아웃 자산을 찾지 못했다: {LoadoutPath}");
                return;
            }

            if (!ValidateTargetPaths(specs)) return;

            // ② 자산 15개 생성 또는 값 갱신.
            int created = 0;
            int updated = 0;
            int changed = 0;   // [EMP 커밋 4] 이미 있던 자산 중 값이 표와 달라 실제로 바뀐 자산 수.
            int unchanged = 0; // [EMP 커밋 4] 이미 있던 자산 중 값이 표와 이미 같았던 자산 수.
            var defsBySpec = new Dictionary<SkillSpec, SkillDefinition>();
            foreach (SkillSpec spec in specs)
            {
                SkillDefinition def = CreateOrLoad(spec, out bool wasCreated);
                if (def == null)
                {
                    Debug.LogError($"{LogPrefix} 중단 — 자산을 만들지 못했다: {AssetPathOf(spec)} (로드아웃은 바꾸지 않았다)");
                    return;
                }
                if (wasCreated) created++; else updated++;

                // [EMP 커밋 4] 쓰기 전에 "지금 값 → 표 값"이 다른 칸을 남긴다. 재실행 때 EMP 외의 자산이
                //   바뀌지 않았다는 것을 Console에서 바로 확인하기 위해서다(Q10 확인 항목).
                if (!wasCreated)
                {
                    string diff = DescribeChanges(def, spec, bombardmentVfx, empPulseVfx);
                    if (diff.Length > 0)
                    {
                        changed++;
                        Debug.Log($"{LogPrefix} 값이 바뀌는 자산: {spec.FileName} — {diff}");
                    }
                    else
                    {
                        unchanged++;
                    }
                }

                // WriteValues(def, spec, bombardmentVfx); // [EMP 커밋 4] EMP VFX 인자 추가로 아래 줄로 대체.
                WriteValues(def, spec, bombardmentVfx, empPulseVfx);
                defsBySpec[spec] = def;
            }
            Debug.Log($"{LogPrefix} 기존 자산 비교 — 값이 바뀐 자산 {changed}개 · 이미 표와 같던 자산 {unchanged}개 · 새로 만든 자산 {created}개.");

            // ③ 로드아웃 교체(교체 전 GUID를 먼저 로그로 남긴다 — 되돌리기용).
            if (!ReplaceLoadout(loadout, specs, defsBySpec)) return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ④ 다시 읽어 확인(런타임과 같은 GetLoadout 경로로 읽는다).
            // bool verified = VerifyLoadout(loadout, specs); // [EMP 커밋 4] VFX 프리팹까지 비교하도록 인자 추가 — 아래 줄로 대체.
            bool verified = VerifyLoadout(loadout, specs, bombardmentVfx, empPulseVfx);

            if (verified)
            {
                // [EMP 커밋 4] 사용 가능 스킬 수 문구 1개(포격 지원) → 2개(포격 지원 · EMP).
                Debug.Log($"{LogPrefix} 완료 — 새로 만든 자산 {created}개 · 값을 다시 쓴 자산 {updated}개 · " +
                          $"로드아웃 3종족 × {SlotsPerRace}슬롯 교체 · 사용 가능 스킬 {ExpectedAvailableCount}개(포격 지원 · EMP). " +
                          "기존 임시 스킬 자산 18개는 그대로 남아 있다.");
            }
            else
            {
                Debug.LogError($"{LogPrefix} 확인 실패 — 자산은 저장됐지만 다시 읽은 로드아웃이 표와 다르다. 위 오류 줄을 확인하세요.");
            }
        }

        // ====================================================================
        // ① 사전 검사
        // ====================================================================

        // 표 자체가 올바른지(종족마다 슬롯 0~4가 정확히 하나씩, 파일 이름 중복 없음, 사용 가능 1개).
        private static bool ValidateSpecs(List<SkillSpec> specs)
        {
            var fileNames = new HashSet<string>();
            var slotKeys = new HashSet<string>();
            int availableCount = 0;

            foreach (SkillSpec spec in specs)
            {
                if (!fileNames.Add(spec.FileName))
                {
                    Debug.LogError($"{LogPrefix} 중단 — 표에 같은 파일 이름이 두 번 있다: {spec.FileName}");
                    return false;
                }
                if (spec.Slot < 0 || spec.Slot >= SlotsPerRace || !slotKeys.Add($"{(int)spec.Race}:{spec.Slot}"))
                {
                    Debug.LogError($"{LogPrefix} 중단 — 표의 슬롯 번호가 잘못됐다: {spec.FileName} (race {(int)spec.Race}, slot {spec.Slot})");
                    return false;
                }
                if (spec.IsAvailable) availableCount++;

                // [EMP 커밋 4] 발동 VFX 칸은 하나뿐이라 두 VFX를 동시에 고를 수 없다.
                if (spec.UseBombardmentVfx && spec.UseEmpPulseVfx)
                {
                    Debug.LogError($"{LogPrefix} 중단 — 표의 {spec.FileName} 에 포격 VFX와 EMP VFX가 둘 다 켜져 있다(하나만 고를 것).");
                    return false;
                }
            }

            // [EMP 커밋 4] 사용 가능 기대 수를 1 → ExpectedAvailableCount(2)로 바꿨다. 종전 조건은 아래 주석.
            // if (slotKeys.Count != 3 * SlotsPerRace || availableCount != 1)
            if (slotKeys.Count != 3 * SlotsPerRace || availableCount != ExpectedAvailableCount)
            {
                Debug.LogError($"{LogPrefix} 중단 — 표가 3종족 × {SlotsPerRace}슬롯이 아니거나 사용 가능 스킬이 {ExpectedAvailableCount}개가 아니다 " +
                               $"(슬롯 {slotKeys.Count}개, 사용 가능 {availableCount}개).");
                return false;
            }
            return true;
        }

        // SkillDefinition 에 이 스크립트가 쓰려는 칸이 모두 있는지(코드가 컴파일된 최신 상태인지) 확인한다.
        //   임시 인스턴스를 만들어 SerializedObject로 칸 이름을 찾아 보고, 바로 지운다(자산으로 저장되지 않음).
        private static bool ValidateFieldNames()
        {
            SkillDefinition probe = ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                var so = new SerializedObject(probe);
                foreach (string field in AllFieldNames)
                {
                    if (so.FindProperty(field) == null)
                    {
                        Debug.LogError($"{LogPrefix} 중단 — SkillDefinition 에 '{field}' 칸이 없다. " +
                                       "스크립트 컴파일이 끝났는지(Console 오류 0) 확인한 뒤 다시 실행하세요.");
                        return false;
                    }
                }

                SkillLoadoutConfig loadoutProbe = ScriptableObject.CreateInstance<SkillLoadoutConfig>();
                try
                {
                    var lso = new SerializedObject(loadoutProbe);
                    if (lso.FindProperty(F_Entries) == null)
                    {
                        Debug.LogError($"{LogPrefix} 중단 — SkillLoadoutConfig 에 '{F_Entries}' 칸이 없다.");
                        return false;
                    }
                }
                finally
                {
                    Object.DestroyImmediate(loadoutProbe);
                }
                return true;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        // 포격 VFX 프리팹을 경로로 읽고, GUID까지 기대값과 같은지 확인한다.
        private static GameObject LoadBombardmentVfx()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BombardmentVfxPath);
            if (prefab == null)
            {
                Debug.LogError($"{LogPrefix} 중단 — 포격 VFX 프리팹을 찾지 못했다: {BombardmentVfxPath}");
                return null;
            }

            string guid = AssetDatabase.AssetPathToGUID(BombardmentVfxPath);
            if (guid != BombardmentVfxGuid)
            {
                Debug.LogError($"{LogPrefix} 중단 — 포격 VFX 프리팹의 GUID가 기대값과 다르다 " +
                               $"(기대 {BombardmentVfxGuid}, 실제 {guid}). 다른 파일일 수 있어 연결하지 않는다.");
                return null;
            }
            return prefab;
        }

        // [EMP 커밋 4] EMP 고리 VFX 프리팹을 경로로 읽고, GUID까지 기대값과 같은지 확인한다(LoadBombardmentVfx 와 같은 방식).
        private static GameObject LoadEmpPulseVfx()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmpPulseVfxPath);
            if (prefab == null)
            {
                Debug.LogError($"{LogPrefix} 중단 — EMP VFX 프리팹을 찾지 못했다: {EmpPulseVfxPath}");
                return null;
            }

            string guid = AssetDatabase.AssetPathToGUID(EmpPulseVfxPath);
            if (guid != EmpPulseVfxGuid)
            {
                Debug.LogError($"{LogPrefix} 중단 — EMP VFX 프리팹의 GUID가 기대값과 다르다 " +
                               $"(기대 {EmpPulseVfxGuid}, 실제 {guid}). 다른 파일일 수 있어 연결하지 않는다.");
                return null;
            }
            return prefab;
        }

        // [EMP 커밋 4] 표의 VFX 선택을 실제 프리팹으로 바꾼다(포격 · EMP · 없음). 쓰기와 확인이 같은 규칙을 쓰게 한 곳에 둔다.
        private static GameObject ResolveActivationVfx(SkillSpec spec, GameObject bombardmentVfx, GameObject empPulseVfx)
        {
            if (spec.UseBombardmentVfx) return bombardmentVfx;
            if (spec.UseEmpPulseVfx) return empPulseVfx;
            return null;
        }

        // 만들 자리에 "SkillDefinition 이 아닌 다른 파일"이 이미 있으면 덮어쓰지 않고 멈춘다.
        private static bool ValidateTargetPaths(List<SkillSpec> specs)
        {
            foreach (SkillSpec spec in specs)
            {
                string path = AssetPathOf(spec);
                Object existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && !(existing is SkillDefinition))
                {
                    Debug.LogError($"{LogPrefix} 중단 — {path} 에 SkillDefinition 이 아닌 자산({existing.GetType().Name})이 있다. " +
                                   "덮어쓰지 않는다.");
                    return false;
                }
            }
            return true;
        }

        // ====================================================================
        // ② 자산 생성 · 값 쓰기
        // ====================================================================

        private static string AssetPathOf(SkillSpec spec)
        {
            return $"{SkillFolder}/{spec.FileName}.asset";
        }

        // 자산이 있으면 그대로 불러오고(GUID 유지 — 멱등), 없으면 새로 만든다.
        private static SkillDefinition CreateOrLoad(SkillSpec spec, out bool wasCreated)
        {
            string path = AssetPathOf(spec);
            SkillDefinition def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (def != null)
            {
                wasCreated = false;
                return def;
            }

            def = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(def, path);
            wasCreated = true;
            return AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        }

        // 표의 값을 자산의 private 칸에 써 넣는다(SerializedObject — Inspector에서 입력한 것과 같은 효과).
        //   아이콘은 비워 둔다(아이콘 그림은 추후 UI 작업 — 상위 결정 16). 매번 모든 칸을 다시 쓰므로 멱등이다.
        //   [EMP 커밋 4] empPulseVfx 인자 추가 — 발동 VFX 칸을 ResolveActivationVfx 로 고른다.
        private static void WriteValues(SkillDefinition def, SkillSpec spec, GameObject bombardmentVfx, GameObject empPulseVfx)
        {
            var so = new SerializedObject(def);

            so.FindProperty(F_Icon).objectReferenceValue = null;
            so.FindProperty(F_DisplayName).stringValue = spec.DisplayName;
            so.FindProperty(F_Description).stringValue = spec.Description;
            so.FindProperty(F_IsAvailable).boolValue = spec.IsAvailable;
            // 열거형 칸은 정수 값으로 넣는다(enum 정의 순서가 아니라 "값"이 저장되도록 intValue 사용).
            so.FindProperty(F_AimType).intValue = (int)spec.AimType;
            so.FindProperty(F_Cooldown).floatValue = spec.Cooldown;
            so.FindProperty(F_Mechanic).intValue = (int)spec.Mechanic;
            so.FindProperty(F_Radius).floatValue = spec.Radius;
            so.FindProperty(F_Duration).floatValue = spec.Duration;
            so.FindProperty(F_TotalDamage).intValue = spec.TotalDamage;
            so.FindProperty(F_DamagePerSecond).floatValue = spec.DamagePerSecond;
            so.FindProperty(F_StatusKind).intValue = spec.StatusKind;
            so.FindProperty(F_Magnitude).floatValue = spec.Magnitude;
            so.FindProperty(F_TargetsAllies).boolValue = spec.TargetsAllies;
            so.FindProperty(F_ImpactDelay).floatValue = spec.ImpactDelay;
            // so.FindProperty(F_ActivationVfx).objectReferenceValue = spec.UseBombardmentVfx ? bombardmentVfx : null;
            //   ↑ [EMP 커밋 4] 포격 VFX만 고르던 종전 줄. EMP VFX도 고를 수 있게 아래 줄로 대체(포격 결과는 같다).
            so.FindProperty(F_ActivationVfx).objectReferenceValue = ResolveActivationVfx(spec, bombardmentVfx, empPulseVfx);
            so.FindProperty(F_VfxReferenceRadius).floatValue = DefaultVfxReferenceRadius;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def); // 저장 대상으로 표시(빠지면 SaveAssets가 파일에 쓰지 않는다).
        }

        // [EMP 커밋 4] 자산의 지금 값과 표 값을 칸마다 비교해, 다른 칸만 "칸: 지금 → 표" 목록 문자열로 만든다(로그용).
        //   아무것도 다르지 않으면 빈 문자열. 값을 쓰지 않는다(읽기만) — WriteValues 바로 앞에서 부른다.
        //   실수 비교는 Mathf.Approximately(아주 작은 반올림 차이는 같은 값으로 본다).
        private static string DescribeChanges(SkillDefinition def, SkillSpec spec, GameObject bombardmentVfx, GameObject empPulseVfx)
        {
            var so = new SerializedObject(def);
            var sb = new StringBuilder();

            void Note(string field, string before, string after)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{field} {before} → {after}");
            }
            void CheckFloat(string field, float expected)
            {
                float v = so.FindProperty(field).floatValue;
                if (!Mathf.Approximately(v, expected)) Note(field, v.ToString("0.###"), expected.ToString("0.###"));
            }
            void CheckInt(string field, int expected)
            {
                int v = so.FindProperty(field).intValue;
                if (v != expected) Note(field, v.ToString(), expected.ToString());
            }
            void CheckBool(string field, bool expected)
            {
                bool v = so.FindProperty(field).boolValue;
                if (v != expected) Note(field, v ? "1" : "0", expected ? "1" : "0");
            }
            void CheckString(string field, string expected)
            {
                string v = so.FindProperty(field).stringValue ?? string.Empty;
                if (v != (expected ?? string.Empty)) Note(field, $"'{v}'", $"'{expected}'");
            }
            void CheckRef(string field, Object expected)
            {
                Object v = so.FindProperty(field).objectReferenceValue;
                if (v != expected) Note(field, v != null ? v.name : "(없음)", expected != null ? expected.name : "(없음)");
            }

            CheckRef(F_Icon, null);
            CheckString(F_DisplayName, spec.DisplayName);
            CheckString(F_Description, spec.Description);
            CheckBool(F_IsAvailable, spec.IsAvailable);
            CheckInt(F_AimType, (int)spec.AimType);
            CheckFloat(F_Cooldown, spec.Cooldown);
            CheckInt(F_Mechanic, (int)spec.Mechanic);
            CheckFloat(F_Radius, spec.Radius);
            CheckFloat(F_Duration, spec.Duration);
            CheckInt(F_TotalDamage, spec.TotalDamage);
            CheckFloat(F_DamagePerSecond, spec.DamagePerSecond);
            CheckInt(F_StatusKind, spec.StatusKind);
            CheckFloat(F_Magnitude, spec.Magnitude);
            CheckBool(F_TargetsAllies, spec.TargetsAllies);
            CheckFloat(F_ImpactDelay, spec.ImpactDelay);
            CheckRef(F_ActivationVfx, ResolveActivationVfx(spec, bombardmentVfx, empPulseVfx));
            CheckFloat(F_VfxReferenceRadius, DefaultVfxReferenceRadius);

            return sb.ToString();
        }

        // ====================================================================
        // ③ 로드아웃 교체
        // ====================================================================

        // 세 종족 항목의 skills 배열을 표의 5개로 바꾼다. 종족 항목이 없으면 새로 추가한다.
        private static bool ReplaceLoadout(SkillLoadoutConfig loadout, List<SkillSpec> specs,
            Dictionary<SkillSpec, SkillDefinition> defsBySpec)
        {
            var so = new SerializedObject(loadout);
            SerializedProperty entries = so.FindProperty(F_Entries);
            if (entries == null || !entries.isArray)
            {
                Debug.LogError($"{LogPrefix} 중단 — 로드아웃의 '{F_Entries}' 칸을 읽지 못했다. (스킬 자산은 이미 만들어졌다 — 다시 실행하면 된다)");
                return false;
            }

            RaceId[] races = { RaceId.Human, RaceId.Spirit, RaceId.Transcendence };
            foreach (RaceId race in races)
            {
                SerializedProperty entry = FindOrAddRaceEntry(entries, race);
                SerializedProperty skills = entry.FindPropertyRelative(F_EntrySkills);
                if (skills == null || !skills.isArray)
                {
                    Debug.LogError($"{LogPrefix} 중단 — race {(int)race} 항목의 '{F_EntrySkills}' 칸을 읽지 못했다.");
                    return false;
                }

                // 되돌리기용 — 교체 전 참조를 GUID와 경로로 남긴다.
                Debug.Log($"{LogPrefix} race {(int)race} ({race}) 교체 전 GUID: {DescribeRefs(skills)}");

                skills.arraySize = SlotsPerRace;
                for (int slot = 0; slot < SlotsPerRace; slot++)
                {
                    SkillSpec spec = FindSpec(specs, race, slot);
                    skills.GetArrayElementAtIndex(slot).objectReferenceValue = defsBySpec[spec];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(loadout);

            // 에디터에서는 ScriptableObject 가 Play 사이에도 메모리에 남아, 예전에 만든 로드아웃 캐시가
            //   남아 있을 수 있다. 다음 Play에서 새 로드아웃을 읽도록 캐시를 비운다.
            loadout.ClearCache();
            return true;
        }

        // entries 배열에서 race 값이 같은 항목을 찾는다. 없으면 맨 뒤에 추가한다.
        private static SerializedProperty FindOrAddRaceEntry(SerializedProperty entries, RaceId race)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                SerializedProperty r = e.FindPropertyRelative(F_EntryRace);
                if (r != null && r.intValue == (int)race) return e;
            }

            int index = entries.arraySize;
            entries.arraySize = index + 1;
            SerializedProperty added = entries.GetArrayElementAtIndex(index);
            added.FindPropertyRelative(F_EntryRace).intValue = (int)race;
            Debug.LogWarning($"{LogPrefix} race {(int)race} ({race}) 항목이 없어 새로 추가했다.");
            return added;
        }

        private static SkillSpec FindSpec(List<SkillSpec> specs, RaceId race, int slot)
        {
            foreach (SkillSpec spec in specs)
            {
                if (spec.Race == race && spec.Slot == slot) return spec;
            }
            return null; // ValidateSpecs가 종족 × 슬롯을 모두 확인했으므로 여기 오지 않는다.
        }

        // skills 배열의 참조를 "슬롯:GUID(경로)" 목록 문자열로 만든다(로그용).
        private static string DescribeRefs(SerializedProperty skills)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < skills.arraySize; i++)
            {
                Object o = skills.GetArrayElementAtIndex(i).objectReferenceValue;
                string path = o != null ? AssetDatabase.GetAssetPath(o) : "(비어 있음)";
                string guid = o != null ? AssetDatabase.AssetPathToGUID(path) : "-";
                if (i > 0) sb.Append(" | ");
                sb.Append($"{i + 1}:{guid}({path})");
            }
            return sb.Length > 0 ? sb.ToString() : "(빈 배열)";
        }

        // ====================================================================
        // ④ 다시 읽어 확인
        // ====================================================================

        // 런타임이 쓰는 GetLoadout(SkillDefinition → SkillData 변환)으로 다시 읽어 표와 비교한다.
        //   [EMP 커밋 4] 두 VFX 프리팹 인자를 받아, 발동 VFX가 "있다/없다"가 아니라 "표가 고른 바로 그 프리팹"인지 본다.
        //   또 타입 · 조준 · 쿨다운 · 반경 · 지속 · 피해 · 착탄 지연도 표와 같은지 본다(재실행 때 15개 값이 그대로인지 확인).
        private static bool VerifyLoadout(SkillLoadoutConfig loadout, List<SkillSpec> specs,
            GameObject bombardmentVfx, GameObject empPulseVfx)
        {
            bool ok = true;
            loadout.ClearCache();

            RaceId[] races = { RaceId.Human, RaceId.Spirit, RaceId.Transcendence };
            foreach (RaceId race in races)
            {
                IReadOnlyList<SkillData> list = loadout.GetLoadout(race);
                var line = new StringBuilder();
                line.Append($"{LogPrefix} race {(int)race} ({race}) 교체 후: ");

                if (list == null || list.Count != SlotsPerRace)
                {
                    Debug.LogError($"{LogPrefix} race {(int)race} 로드아웃 개수가 {SlotsPerRace}가 아니다: {list?.Count ?? 0}");
                    ok = false;
                    continue;
                }

                for (int slot = 0; slot < SlotsPerRace; slot++)
                {
                    SkillSpec spec = FindSpec(specs, race, slot);
                    SkillData data = list[slot];
                    // 종전 비교(포격 VFX 유무만 봤다 — EMP VFX가 생겨 그대로 두면 EMP 슬롯이 틀렸다고 나온다):
                    // bool match = data != null
                    //              && data.DisplayName == spec.DisplayName
                    //              && data.IsAvailable == spec.IsAvailable
                    //              && (data.ActivationVfxPrefab != null) == spec.UseBombardmentVfx;
                    GameObject expectedVfx = ResolveActivationVfx(spec, bombardmentVfx, empPulseVfx);
                    bool match = data != null
                                 && data.DisplayName == spec.DisplayName
                                 && data.IsAvailable == spec.IsAvailable
                                 && data.ActivationVfxPrefab == expectedVfx
                                 && data.Mechanic == spec.Mechanic
                                 && data.AimType == spec.AimType
                                 && Mathf.Approximately(data.Cooldown, spec.Cooldown)
                                 && Mathf.Approximately(data.Radius, spec.Radius)
                                 && Mathf.Approximately(data.Duration, spec.Duration)
                                 && data.TotalDamage == spec.TotalDamage
                                 && Mathf.Approximately(data.ImpactDelay, spec.ImpactDelay);
                    if (!match)
                    {
                        Debug.LogError($"{LogPrefix} race {(int)race} 슬롯 {slot + 1} 이 표와 다르다 " +
                                       $"(기대 '{spec.DisplayName}', 사용 가능 {spec.IsAvailable}, 타입 {spec.Mechanic}).");
                        ok = false;
                    }

                    if (slot > 0) line.Append(" | ");
                    line.Append($"{slot + 1}:{data?.DisplayName}({(data != null && data.IsAvailable ? "사용 가능" : "사용 불가")}");
                    // [EMP 커밋 4] 타입 3(탑 무력화)이면 무력화 시간도 보여 준다.
                    if (data != null && data.Mechanic == SkillMechanicType.AreaTowerDisable)
                        line.Append($", 탑 무력화 {data.Duration:0.##}초");
                    if (data != null && data.ImpactDelay > 0f) line.Append($", 착탄 지연 {data.ImpactDelay:0.##}초");
                    if (data != null && data.ActivationVfxPrefab != null) line.Append($", VFX {data.ActivationVfxPrefab.name}");
                    line.Append(")");
                }
                Debug.Log(line.ToString());
            }

            // 확인용으로 만든 캐시를 비워, 다음 Play가 처음부터 다시 읽게 한다.
            loadout.ClearCache();
            return ok;
        }
    }
}
