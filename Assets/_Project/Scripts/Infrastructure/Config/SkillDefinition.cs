// ============================================================================
// SkillDefinition.cs
// 스킬 1개를 데이터로 저작하는 ScriptableObject(규칙 7).
//
// 무엇인가(초급자용 설명):
//   스킬의 "그릇(틀)"이다. 아이콘·조준 방식·쿨다운·타입·수치(반경/피해/지속/상태)를 Inspector에서
//   채워 넣는다. 코드는 타입별 실행기만 구현하고, 개별 스킬 값은 이 SO로 주입한다.
//   (SpecialAttackConfig / BuildingStatsConfig와 동일한 Infrastructure Config SO 패턴.)
//
//   ⚠️ ×10 스케일 주의(main 병합): 전투 스탯이 ×10로 커졌고 DamageCalculator(K=120)가 그 스케일에
//      맞춰졌다. TotalDamage / DamagePerSecond / Magnitude(힐량 등)는 반드시 ×10 스케일로 저작한다
//      (구 감각 "10 피해" ≈ 신 100). 유닛 공격력(UnitStatsConfig, ×10 반영값)과 같은 축으로 맞출 것.
//
//   타입 C(전역 상태변경) 필드(StatusKind/Magnitude/TargetsAllies)는 스키마에 함께 두며,
//   타입 C 실행기(GlobalStatusChangeExecutor)가 읽어 상태효과를 부여한다. 타입 A·B 실행기는 이 필드를 쓰지 않는다.
//
// 개별 스킬 목록·수치는 이번 범위 밖(데이터로 별도 확정). 이 SO는 스키마만 정의한다.
//
// 2026-11-03 추가 칸(지원 스킬 Task — _Tasks/2026-11-03/05_24_support-skill-foundation-bombardment):
//   이름 · 설명 · 사용 가능 · 착탄 지연(초) · 발동 VFX 프리팹 · VFX 기준 반경.
//   모든 새 칸의 초기값은 "지금 동작 그대로"가 되도록 골랐다(사용 가능 true · 지연 0 · 프리팹 없음).
//   그래서 이 칸이 생기기 전에 만든 자산은 다시 저장하지 않아도 예전과 똑같이 동작한다.
//
// 2026-11-16 추가 칸(지원 스킬 새 상태 Task — _Tasks/2026-11-16/01_00_support-skill-new-statuses, 단계 A):
//   「추가 효과 목록」(_extraEffects) — 주 효과 말고 함께 거는 효과(대상 종류 · 상태 종류 · 강도). 초기값은 빈 목록이라 기존 자산 무변경.
//   단계 C: 「건물 상태 VFX」(_buildingStatusVfxPrefab · _buildingStatusVfxScale · _buildingStatusVfxOffsetY). 초기값은 프리팹 없음.
//
// 2026-11-17 추가 칸(지원 스킬 2-L 공습 Task — _Tasks/2026-11-17/09_12_support-skill-air-strike, L-1):
//   「직선 연속 폭격」(_lineLength · _lineWidth · _lineStrikeCount · _lineStrikeInterval). 메커니즘 LineStrike(5)만 읽으므로
//   다른 스킬 자산은 이 칸이 생겨도 동작이 같다.
//   L-5: 「공습 VFX」(_lineVfxReferenceLength · _strikeVfxPrefab). 직선 경고는 기존 「발동 VFX」 칸을 쓰고(회전 + 길이 배율),
//   폭격 1회분은 새 「폭격 VFX」 칸을 쓴다. 초기값은 기준 길이 6(= 임시 길이 → 배율 1) · 프리팹 없음이라 기존 자산 무변경.
//
// 2026-11-18 추가 칸(지원 스킬 2-D 메테오 Task — _Tasks/2026-11-18/03_34_support-skill-meteor):
//   「착탄 화상」(_impactBurnDamagePerSecond · _impactBurnDuration) — 타입 A 착탄 순간 살아 남은 적 유닛에게 화상. 초기값 0 = 화상 없음.
//   「착탄 예약 연출」(_warningVfxReferenceTime · _fallingBodyVfxPrefab · _fallingBodyStartOffset · _fallingBodyEulerOffset · _impactVfxPrefab)
//   — 예고 재생 속도 맞춤 · 떨어지는 몸체 · 착탄 폭발. 초기값은 기준 시간 0(맞춤 안 함) · 프리팹 없음이라 기존 자산 무변경.
//
// Infrastructure 레이어 — ScriptableObject. Application(SkillData)/Domain(enum) 참조.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 스킬 1개의 데이터 정의(ScriptableObject). Inspector에서 저작하고, 런타임에 SkillData로 변환된다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDefinition", menuName = "Hexiege/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Header("표시")]
        [Tooltip("스킬 버튼에 표시할 아이콘(플레이스홀더 가능, 비워도 됨).")]
        [SerializeField] private Sprite _icon;

        // 스킬 이름 · 설명 칸(2026-11-03 추가 — 지원 스킬 Task 커밋 1).
        //   왜 데이터에 두는가: 스킬 규칙 7(데이터 주도) — 스킬 하나의 정보는 한 자산에 모은다.
        //   이름은 스킬 패널 슬롯 버튼 글자로 쓰인다(2026-11-11 — BuildingSkillPanelUI.ResolveSlotLabel).
        //   이름이 비어 있으면 버튼은 예전처럼 실행 방식으로 지어 낸 "테스트용 임시 라벨"로 폴백한다.
        //   설명은 아직 화면에 쓰이지 않는다 — 아이콘 · 설명 탭은 추후 별도 UI 작업 범위다.
        //   기존 자산(이 칸이 없던 때 만든 것)은 빈 문자열로 읽힌다 — 버튼은 임시 라벨로 보이고 동작에는 영향이 없다.
        [Tooltip("스킬 이름(화면 표시용 한글 이름). 스킬 패널 버튼 글자로 쓰인다. 비우면 실행 방식으로 지은 임시 라벨이 보인다.")]
        [SerializeField] private string _displayName = string.Empty;

        [Tooltip("스킬 설명(무엇을 하는 스킬인지 한두 문장). 지금은 화면에 쓰이지 않고, 추후 설명 탭에서 표시한다.")]
        [TextArea(2, 5)]
        [SerializeField] private string _description = string.Empty;

        [Header("사용 가능 여부")]
        // 「사용 가능」 칸(2026-11-03 추가). 기능 코드가 아직 없는 스킬을 로드아웃에 "이름만" 넣어 둘 때 끈다.
        //   false면 서버 발동(SkillActivationUseCase.Activate)이 거부하고 쿨다운도 돌지 않으며,
        //   스킬 패널은 버튼을 눌러도 조준 모드에 들어가지 않는다.
        //   🔴 기본값 true인 이유(사용자 결정 Q2): 이 칸이 생기기 전에 만든 기존 스킬 자산 18개는 파일 안에
        //      이 칸이 없다. Unity는 파일에 없는 칸을 아래 초기값(true)으로 채우므로, 기존 자산은 지금처럼
        //      "사용 가능"으로 동작한다 — 기존 자산을 로드아웃에 다시 넣으면 그대로 되살아난다.
        [Tooltip("체크 해제 시 이 스킬은 눌러도 발동되지 않는다(기능 미구현 스킬용). 서버도 발동을 거부하고 쿨다운이 돌지 않는다.")]
        [SerializeField] private bool _isAvailable = true;

        [Header("발동")]
        [Tooltip("조준 방식. Instant=버튼 탭 즉시(타입 C), PointTarget=지점 지정(타입 A·B).")]
        [SerializeField] private SkillAimType _aimType = SkillAimType.PointTarget;

        [Tooltip("발동 후 건물 글로벌 쿨다운(초). 규칙 3 — 이 건물의 모든 스킬이 함께 잠긴다.")]
        [SerializeField] private float _cooldown = 10f;

        [Header("메커니즘")]
        [Tooltip("실행 방식. A=즉발 범위 피해 / B=범위 지속 피해(장판) / C=전역 상태변경(Phase 2).")]
        [SerializeField] private SkillMechanicType _mechanic = SkillMechanicType.InstantAreaDamage;

        [Header("범위/지속 (타입 A·B)")]
        [Tooltip("(A/B) 원형 반경(월드 단위). 헥스 인접 1칸이 대략 1.0.")]
        [SerializeField] private float _radius = 1.0f;

        [Tooltip("(B) DoT 지속시간 / (C) 상태 지속시간(초).")]
        [SerializeField] private float _duration = 3f;

        [Header("피해 (×10 스케일로 저작)")]
        [Tooltip("(A) 즉발 1회 총 피해. ×10 스케일 — 유닛 공격력과 같은 축으로 저작.")]
        [SerializeField] private int _totalDamage = 100;

        [Tooltip("(B) 초당 피해. ×10 스케일.")]
        [SerializeField] private float _damagePerSecond = 20f;

        [Header("상태변경 (타입 C — Phase 2 사용)")]
        [Tooltip("(C) 상태 종류(정수 코드). GlobalStatusChangeExecutor가 StatusEffectKind로 변환해 사용한다.")]
        [SerializeField] private int _statusKind = 0;

        [Tooltip("(C) 강도(이속 배율·힐량 등, ×10 스케일).")]
        [SerializeField] private float _magnitude = 0f;

        [Tooltip("(C) 대상. true=아군(긍정), false=적(부정).")]
        [SerializeField] private bool _targetsAllies = false;

        [Header("착탄 지연 (타입 A — 포격 · 메테오용)")]
        // 착탄 지연 칸(2026-11-03 추가). "발동한 순간"과 "포탄이 땅에 닿는 순간" 사이의 시간(초).
        //   예: 포격 지원은 포탄이 0.4초 동안 떨어진 뒤 터지므로 0.4.
        //   읽는 곳(2026-11-18 기준):
        //     - 서버 — SkillActivationUseCase가 타입 A 피해를 이 시간만큼 늦춘다(착탄 예약 → 권위 전용 틱에서 착탄 순간 판정 · 피해).
        //       포격 지원 0.4초 · 메테오 1.5초(메테오는 착탄 순간 화상도 함께). 공습(LineStrike)은 이 값을 「첫 폭격까지의 시간」으로 쓴다.
        //     - 화면 — SkillActivationVfxPresenter가 공습 폭격 VFX 재생 시각(스킬 데이터 값), 메테오 몸체 이동 시간 · 착탄 VFX 재생 시각 ·
        //       예고 재생 속도(기준 시간 ÷ 이 값 — 발동 알림에 실린 값, 없으면 스킬 데이터 값)에 쓴다.
        //   *(2026-11-18 갱신 — 직전 「🔴 지금(커밋 1·2)은 이 값을 읽는 코드가 아직 없다 — 서버가 이 시간만큼 피해를 늦추는 장치는
        //    같은 Task의 다음 단계(커밋 4)에서 붙는다. 그전까지는 값이 무엇이든 지금처럼 즉시 피해다.」 — 2026-11-03 포격 커밋 4에서 이미 연결됐다)*
        //   0이면 지연 없음(지금과 같은 즉발). 음수는 SkillData에서 0으로 고친다.
        //   기존 자산은 파일에 이 칸이 없어 초기값 0으로 읽힌다 → 동작 무변경.
        [Tooltip("발동부터 착탄(피해)까지 걸리는 시간(초). 0=즉발. 서버가 타입 A 피해를 이만큼 늦춘다(착탄 예약). 공습은 첫 폭격까지의 시간.")]
        [Min(0f)]
        [SerializeField] private float _impactDelay = 0f;

        [Header("발동 VFX (화면 연출)")]
        // 발동 VFX 칸(2026-11-03 추가). 스킬을 쓴 자리에 띄울 이펙트 프리팹과, 그 프리팹이 어느 반경 기준으로
        //   만들어졌는지(기준 반경)를 적는다. 화면에서는 "실제 반경 ÷ 기준 반경" 배율로 이펙트 크기를 맞춘다.
        //   예: 기준 반경 2.0으로 만든 포격 이펙트를 반경 3.0 스킬에 쓰면 1.5배로 키워 판정 범위와 맞춘다.
        //   읽어 재생하는 곳(2026-11-18 기준): SkillActivationVfxPresenter — 원형 스킬은 조준점에 1회(즉시 발동 스킬은 시전 건물 위치),
        //     공습은 직선 경고(회전 + 길이 배율), 메테오는 바닥 예고(재생 속도 맞춤)로 이 칸의 프리팹을 재생한다.
        //   *(2026-11-18 갱신 — 직전 「🔴 지금(커밋 1·2)은 이 값을 읽어 재생하는 코드가 아직 없다 — 화면 재생 연결은 같은 Task의 다음 단계.」
        //    — 2026-11-03 같은 Task에서 이미 연결됐다)*
        //   기존 자산은 프리팹 없음(null) · 기준 반경 2.0으로 읽힌다 → 동작 무변경.
        [Tooltip("발동 지점에 재생할 VFX 프리팹(비워도 됨 — 비우면 연출 없음).")]
        [SerializeField] private GameObject _activationVfxPrefab;

        [Tooltip("VFX 프리팹이 제작된 기준 반경(월드 단위). 재생 배율 = 스킬 반경 ÷ 이 값. 0 이하면 배율 1.")]
        [SerializeField] private float _vfxReferenceRadius = 2.0f;

        [Header("구역 VFX (지속 상태 구역 — 구역이 남아 있는 동안 바닥에 깔리는 반복 이펙트)")]
        // 구역 VFX 칸(2026-11-11 추가 — 지속 상태 구역 Task, 사용자 결정 Z-13).
        //
        // 무엇인가(초급자용 설명):
        //   서리 감옥 · 수액 늪을 쓰면 조준한 자리에 구역이 몇 초 동안 남는다. 그동안 바닥에 깔려 있을 이펙트(얼음 · 호박색 수액)를
        //   넣는 칸이다. 발동 순간 한 번 터지는 「발동 VFX」와 달리 구역이 끝날 때까지 반복 재생되고, 끝나면 사그라진다.
        //   크기 = 스킬 반경 ÷ 위 「VFX 기준 반경」(발동 VFX와 같은 기준을 쓴다). 높이 = 지면 + 0.06(타일 윗면 0.05 위).
        //
        //   비워 두면(기본값) 효과(빙결 · 둔화)는 그대로 걸리고 화면에는 바닥 이펙트가 없다 — 진단 「ZoneVfxSkipped Reason=NoPrefab」.
        //   🔴 넣는 프리팹은 「상태 이펙트 계약」을 지켜야 한다(VFXSFXGuide 「코드 계약」 1): 모든 Particle System Looping ON ·
        //      Stop Action None · Scaling Mode Hierarchy. 어기면 재생하지 않는다(「ZoneVfxSkipped Reason=LoopContract」).
        //   기존 자산은 파일에 이 칸이 없어 초기값(프리팹 없음)으로 읽힌다 → 동작 무변경. 셋업 도구는 이 칸을 쓰지 않는다.
        [Tooltip("지속 상태 구역이 남아 있는 동안 조준 지점 바닥에 반복 재생할 VFX 프리팹(Looping). 비우면 이펙트 없이 효과만 동작.")]
        [SerializeField] private GameObject _zoneVfxPrefab;

        // [2026-11-11 지속 상태 구역 Task — Z-14] 헤더 · 툴팁의 「타입 C」 문구를 일반화했다(구역 스킬도 이 칸을 쓴다 — 서리 감옥 얼음 V4).
        //   직렬화 키(_statusVfxPrefab 등)는 그대로라 자산은 바뀌지 않는다. 옛 헤더(기록용):
        // [Header("강화 상태 VFX (타입 C — 효과를 받는 유닛 몸에 붙는 반복 이펙트)")]
        [Header("유닛 상태 VFX (강화 스킬 · 지속 상태 구역 — 효과를 받는 유닛 몸에 붙는 반복 이펙트)")]
        // 강화 상태 VFX 칸(2026-11-10 추가 — 지원 스킬 1차 강화 Task
        //   _Tasks/2026-11-10/04_20_support-buff-skills-status-vfx, Plan 구현 단위 ①).
        //
        // 무엇인가(초급자용 설명):
        //   「전술 지령」 같은 강화 스킬을 쓰면 효과를 받은 아군 유닛마다 몸에 이펙트(빛 · 바람 · 잎 등)가 붙어
        //   효과가 끝날 때까지 따라다닌다. 그 이펙트 그림을 "스킬마다" 따로 넣는 칸이다.
        //   그림은 스킬마다 다르고(사용자: 「각 종족별 강화 vfx는 다른 이펙트」), 붙이고 · 따라가고 · 떼는 장치
        //   (UnitBuffVfxPresenter)는 모든 강화 스킬이 함께 쓴다.
        //
        //   비워 두면(기본값) 효과(공격력 · 이동 속도)는 그대로 걸리고, 화면에는 이펙트가 없다.
        //   이때 화면 쪽 구독자가 진단 로그 「BuffVfxSkipped Reason=NoPrefab」을 1줄 남긴다.
        //
        //   🔴 넣는 프리팹은 「상태 이펙트 계약」을 지켜야 한다(VFXSFXGuide 「코드 계약」 1 · 2):
        //      모든 Particle System Looping ON · Stop Action None · Scaling Mode Hierarchy, 루트 pivot = 유닛 발밑.
        //      Looping이 꺼져 있으면 효과가 끝나기 전에 이펙트가 저절로 사라지고, Hierarchy가 아니면 유닛 크기 맞춤이 먹지 않는다.
        //
        //   크기 · 위치 맞춤:
        //     최종 크기 = 프리팹 원본 크기 × 아래 「크기 배율」 × 유닛 크기 표(UnitEffectConfig 「상태 이펙트 크기 맞춤」)의 균등 배율.
        //     최종 위치 = 유닛 발밑 + 유닛 회전 × (아래 「위치 보정」 + 크기 표의 발밑 보정).
        //     (화상 · 독 · 힐 이펙트가 EffectPreset의 Scale Multiplier · Local Position Offset으로 맞추는 것과 같은 계산이다.
        //      스킬 데이터는 Presentation 타입인 EffectPreset을 참조할 수 없어서 — 레이어 규칙 — 숫자 칸 두 개로 둔다.)
        //
        //   기존 자산은 파일에 이 칸이 없어 아래 초기값(프리팹 없음 · 배율 1 · 보정 0)으로 읽힌다 → 동작 무변경.
        //   일회성 셋업 도구(SupportSkillLoadoutSetup)는 이 칸을 쓰지 않는다 — 도구를 다시 실행해도 넣은 프리팹이 지워지지 않는다.
        //   [2026-11-11 Z-14] 구역 스킬(서리 감옥 등)이면 "구역 안에 있는 동안" 붙는다 — 들어오면 붙고 나가거나 구역이 끝나면 떨어진다.
        //   옛 툴팁(기록용): "효과를 받은 유닛 몸에 붙일 반복(Looping) VFX 프리팹. 비우면 이펙트 없이 효과만 걸린다."
        [Tooltip("효과를 받은 유닛 몸에 붙일 반복(Looping) VFX 프리팹. 강화 스킬(타입 C)은 효과 시간 동안, 지속 상태 구역은 구역 안에 있는 동안 붙는다. 비우면 이펙트 없이 효과만 걸린다.")]
        [SerializeField] private GameObject _statusVfxPrefab;

        //   옛 툴팁(기록용): "강화 상태 VFX 크기 배율(프리팹 원본 × 이 값 × 유닛 크기 표 배율). 0 이하 · NaN이면 1로 쓴다."
        [Tooltip("유닛 상태 VFX 크기 배율(프리팹 원본 × 이 값 × 유닛 크기 표 배율). 0 이하 · NaN이면 1로 쓴다.")]
        [SerializeField] private float _statusVfxScale = 1f;

        //   옛 툴팁(기록용): "강화 상태 VFX 위치 보정(유닛 기준 로컬 좌표, 월드 단위). 예: (0, 0.1, 0) = 발밑보다 0.1 위."
        [Tooltip("유닛 상태 VFX 위치 보정(유닛 기준 로컬 좌표, 월드 단위). 예: (0, 0.1, 0) = 발밑보다 0.1 위.")]
        [SerializeField] private Vector3 _statusVfxOffset = Vector3.zero;

        // ── 2026-11-16 추가: 추가 효과 목록(지원 스킬 새 상태 Task 단계 A — 구현 단위 A-1, Plan S-13) ──
        //   위 「상태변경」 칸(주 효과)은 효과를 하나만 담는다. 전술 지령처럼 「공격력 + 생산 속도」를 함께 거는 스킬은
        //   두 번째 효과부터 이 목록에 한 줄씩 적는다. 지속시간 · 아군/적은 주 효과 칸(_duration · _targetsAllies)을 함께 쓴다.
        //   기존 자산은 파일에 이 칸이 없어 빈 목록으로 읽힌다 → 동작 무변경.

        /// <summary>
        /// 추가 효과 한 줄(직렬화용). 정수 코드로 저장한다 — 주 효과 칸(_statusKind)과 같은 방식(열거형 이름이 바뀌어도 자산이 깨지지 않게).
        /// </summary>
        [System.Serializable]
        public sealed class ExtraEffectEntry
        {
            // [2026-11-16 단계 C] 건물 대상(1)이 쉴드에 한해 허용됐다 — 툴팁 갱신. 옛 툴팁(기록용):
            //   "대상 종류 정수 코드. 0 = 대상 팀 유닛 각각 · 1 = 대상 팀 건물 각각(단계 C·B용 — 지금은 거부됨) · 2 = 대상 팀 자체(예: 생산 속도)."
            // [2026-11-17 단계 B] 건물 대상에 재생 · 받는 회복 배율이 더해졌다 — 툴팁 갱신. 옛 툴팁(기록용):
            //   "대상 종류 정수 코드. 0 = 대상 팀 유닛 각각 · 1 = 대상 팀 건물 각각(지금은 쉴드만 — 신목의 각성) · 2 = 대상 팀 자체(예: 생산 속도)."
            [Tooltip("대상 종류 정수 코드. 0 = 대상 팀 유닛 각각 · 1 = 대상 팀 건물 각각(쉴드 · 재생 · 받는 회복 배율) · 2 = 대상 팀 자체(예: 생산 속도).")]
            public int target = (int)SkillEffectTarget.Team;

            // [2026-11-16 단계 C] 8 = 쉴드 추가. 옛 툴팁(기록용): "상태 종류 정수 코드(StatusEffectKind). 6 = 받는 피해 배율 · 7 = 생산 속도 배율."
            // [2026-11-17 단계 B] 9 = 재생 · 10 = 받는 회복 배율 추가. 옛 툴팁(기록용):
            //   "상태 종류 정수 코드(StatusEffectKind). 6 = 받는 피해 배율 · 7 = 생산 속도 배율 · 8 = 쉴드(강도 = 흡수량)."
            [Tooltip("상태 종류 정수 코드(StatusEffectKind). 6 = 받는 피해 배율 · 7 = 생산 속도 배율 · 8 = 쉴드(강도 = 흡수량) · 9 = 재생(강도 = 초당 회복량) · 10 = 받는 회복 배율(예: 1.5).")]
            public int statusKind = 0;

            [Tooltip("강도(배율). 예: 1.5 = 1.5배.")]
            public float magnitude = 0f;
        }

        [Header("추가 효과 목록 (타입 C — 주 효과 말고 더 거는 효과)")]
        [Tooltip("주 효과(위 「상태변경」) 말고 함께 거는 효과들. 지속시간 · 아군/적은 주 효과와 같다. 예: 전술 지령 = [대상 2(팀) · 종류 7(생산 속도) · 1.5].")]
        [SerializeField] private ExtraEffectEntry[] _extraEffects = new ExtraEffectEntry[0];

        [Header("건물 상태 VFX (효과를 받는 건물 위에 효과 시간 동안 붙는 반복 이펙트)")]
        // 건물 상태 VFX 칸(2026-11-16 추가 — 지원 스킬 새 상태 Task 단계 C, Plan 6-1-3 · S-10 · S-12).
        //
        // 무엇인가(초급자용 설명):
        //   신목의 각성을 쓰면 아군 건물에도 쉴드가 씌워진다. 그 쉴드가 남아 있는 동안 건물 위에 보여 줄 이펙트를 넣는 칸이다.
        //   유닛 몸에 붙는 「유닛 상태 VFX」와 따로 둔다(건물은 크기 · 모양이 유닛과 전혀 다르다).
        //   붙이고 · 떼는 장치(BuildingStatusVfxPresenter)는 이 칸을 쓰는 모든 스킬이 함께 쓴다
        //   (단계 B 재생의 뿌리 건물 재생도 같은 칸 · 장치를 쓴다).
        //
        //   언제 쓰이나: 스킬의 「추가 효과 목록」에 대상 1(건물 각각) 줄이 있을 때만 붙인다(지금은 신목의 각성).
        //     전술 지령(생산 건물 쪽 이펙트 — S-10)은 칸만 있고 비워 둔다. 「어느 건물에 붙일지」는 아직 정해지지 않아(상위 Plan 4절 20)
        //     전술 지령 칸에 프리팹을 넣어도 지금은 붙지 않는다.
        //
        //   정지: 효과 시간 끝 = 꼬리 정지 · 쉴드 깨짐 · 건물 파괴/철거 · 업그레이드 · 재경기 = 즉시 · 경기 종료 = 꼬리.
        //
        //   🔴 넣는 프리팹은 「상태 이펙트 계약」을 지켜야 한다(VFXSFXGuide 「코드 계약」 1): 모든 Particle System Looping ON ·
        //      Stop Action None · Scaling Mode Hierarchy. 어기면 재생하지 않는다(진단 「BuildingVfxSkipped Reason=LoopContract」).
        //   크기 = 프리팹 원본 × 아래 배율(건물 종류와 무관한 하나의 배율 — S-12). 위치 = 건물 GameObject 위치 + 아래 높이 보정(Y만).
        //   기존 자산은 파일에 이 칸이 없어 초기값(프리팹 없음 · 배율 1 · 보정 0)으로 읽힌다 → 동작 무변경. 셋업 도구는 이 칸을 쓰지 않는다.
        [Tooltip("효과를 받은 건물 위에 효과 시간 동안 반복 재생할 VFX 프리팹(Looping). 추가 효과에 건물 대상 줄이 있는 스킬만 쓴다. 비우면 이펙트 없이 효과만 걸린다.")]
        [SerializeField] private GameObject _buildingStatusVfxPrefab;

        [Tooltip("건물 상태 VFX 크기 배율(프리팹 원본 × 이 값 — 모든 건물에 같은 배율). 0 이하 · NaN이면 1로 쓴다.")]
        [SerializeField] private float _buildingStatusVfxScale = 1f;

        [Tooltip("건물 상태 VFX 높이 보정(건물 위치 기준 Y만, 월드 단위). 예: 0.1 = 건물 바닥보다 0.1 위.")]
        [SerializeField] private float _buildingStatusVfxOffsetY = 0f;

        [Header("직선 연속 폭격 (공습 — 메커니즘 LineStrike 전용)")]
        // 직선 연속 폭격 칸(2026-11-17 추가 — 지원 스킬 2-L 공습 Task, Plan L-1 · P-3 · P-7).
        //
        // 무엇인가(초급자용 설명):
        //   공습은 원이 아니라 길쭉한 직사각형을 폭격한다. 플레이어가 누른 자리가 시작점, 끈 쪽이 방향이고,
        //   아래 「길이」만큼 뻗은 직선을 「폭격 횟수」개의 같은 길이 구간으로 나눠 시작점 쪽 구간부터 차례로 폭격한다.
        //     - k번째 폭격 시각 = 위 「착탄 지연」(첫 폭격까지 — 공습은 0.4) + k × 아래 「폭격 간격」.
        //     - 한 번 시전에 같은 대상은 최대 1회만 맞는다(U-12) — 1회 피해 = 위 「즉발 1회 총 피해」 칸 값(공습 임시 150).
        //     - 직선 중 맵 밖 부분은 잘라내고 맵 안 부분에서만 폭격한다(U-14).
        //   서버 틱이 0.1초라 간격은 0.1의 배수(0.2 등)로 두면 깔끔하다(Research 3절 4).
        //
        //   이 칸은 메커니즘이 LineStrike(5)인 스킬만 읽는다 — 다른 스킬 자산에 값이 있어도 아무 영향이 없다.
        //   🔴 아래 초기값은 공습 시험용 임시값(P-7: 길이 6 · 폭 2 · 5회 · 간격 0.2)이다. 이 칸이 없는 자산은 이 값으로 읽힌다.
        //      수치는 밸런싱 단계에서 정한다(Plan O-9). 0 이하로 넣으면 폭격하지 않는다(진단 「LineStrikeRejected」 Warn).
        //   이번 단계(L-1)에서는 공습 자산이 아직 「사용 불가」라 이 값으로 폭격이 일어나지 않는다(자산 켜기 = Plan L-6).
        //   [2026-11-17 L-6 정정 — 위 줄은 L-1 때의 상태] 이제 공습 자산은 「사용 가능」이고 이 네 칸 값(6 · 2 · 5 · 0.2)을 자산에 적어 두었다.
        [Tooltip("(공습) 직선 길이(월드 단위). 시작점에서 끈 방향으로 이만큼 뻗는다. 헥스 인접 1칸이 대략 1.0. 임시 6.")]
        [SerializeField] private float _lineLength = 6f;

        [Tooltip("(공습) 직선 폭(월드 단위). 직선 양옆으로 폭 ÷ 2까지 맞는다. 임시 2.")]
        [SerializeField] private float _lineWidth = 2f;

        [Tooltip("(공습) 폭격 횟수 = 직선을 같은 길이로 나누는 구간 수. 시작점 쪽 구간부터 차례로 폭격한다. 임시 5(최대 20).")]
        [Min(0)]
        [SerializeField] private int _lineStrikeCount = 5;

        [Tooltip("(공습) 폭격 사이 간격(초). 첫 폭격은 위 「착탄 지연」 뒤. 서버 틱 0.1초의 배수 권장. 임시 0.2.")]
        [Min(0f)]
        [SerializeField] private float _lineStrikeInterval = 0.2f;

        [Header("공습 VFX (직선 경고 · 폭격 1회분 — 메커니즘 LineStrike 전용)")]
        // 공습 VFX 칸(2026-11-17 추가 — 지원 스킬 2-L 공습 Task, Plan L-5 · P-6).
        //
        // 무엇인가(초급자용 설명):
        //   공습은 화면에 이펙트를 두 종류 띄운다. 둘 다 서버가 확정한 발동 알림을 받은 각 화면(Host · Client)이 스스로 재생한다.
        //     ① 직선 경고 = 위 「발동 VFX」 칸(_activationVfxPrefab)의 프리팹을 발동 순간 1번 재생한다.
        //        - 위치 = 직선의 가운데(시작점에서 「맵 안 유효 길이」의 절반만큼 나아간 곳), 높이 = 지면 + 0.06.
        //        - 회전 = 프리팹의 앞(로컬 +Z)이 직선 방향을 보게 돌린다.
        //        - 크기 = 길이 방향(로컬 Z)만 「맵 안 유효 길이 ÷ 아래 기준 길이」배로 늘이고 줄인다(옆 · 높이는 원본 그대로).
        //          → 프리팹은 「가운데가 원점 · 앞(+Z)으로 기준 길이만큼 길쭉한 모양」으로 만들면 직선과 정확히 겹친다.
        //        - 포격 · EMP처럼 「기준 반경」 균일 배율은 쓰지 않는다(공습만 이 규칙 — 다른 스킬 재생은 그대로).
        //     ② 폭격 1회분 = 아래 「폭격 VFX」 칸의 프리팹을 폭격마다 1번씩 재생한다(5회면 5번).
        //        - 시각 = 발동 알림을 받은 때부터 「착탄 지연」 + k × 「폭격 간격」(서버 폭격 시각과 같은 계산 — 코드가 정한다).
        //        - 위치 = k번째 구간의 맵 안 부분 가운데, 회전 = 직선 방향, 크기 = 프리팹 원본 그대로(배율 1).
        //        - 맵 밖 구간(서버가 건너뛰는 폭격)은 재생하지 않는다(U-14).
        //   두 칸 모두 비워도 피해는 그대로 들어간다(이펙트는 화면 표현일 뿐이다). 비어 있으면 진단 줄(NoPrefab)만 남는다.
        //   🔴 프리팹의 파티클은 Scaling Mode = Hierarchy여야 루트 배율이 파티클에 먹는다(포격 프리팹과 같은 조건).
        //      프리팹은 반복(Looping)하지 않아야 한다 — 1회 재생 뒤 풀로 돌아가는 방식이다.
        [Tooltip("(공습) 직선 경고 VFX(위 「발동 VFX」 칸)가 만들어진 기준 길이(월드 단위). 길이 방향 배율 = 맵 안 유효 길이 ÷ 이 값. 0 이하면 배율 1. 초기 6(= 임시 직선 길이).")]
        [SerializeField] private float _lineVfxReferenceLength = 6f;

        [Tooltip("(공습) 폭격 1회분 VFX 프리팹. 폭격마다 그 구간 가운데에 1번 재생한다(비워도 피해는 동작).")]
        [SerializeField] private GameObject _strikeVfxPrefab;

        [Header("착탄 화상 (타입 A — 메테오용 · 착탄 순간 살아 남은 적 유닛에게)")]
        // 착탄 화상 칸(2026-11-18 추가 — 지원 스킬 2-D 메테오 Task, Plan M-3 · P-2 · P-3 · U-7).
        //
        // 무엇인가(초급자용 설명):
        //   메테오는 떨어지는 순간 원 안의 적에게 피해를 주고, 그 피해를 맞고도 「살아 남은 적 유닛」에게 화상을 건다.
        //   화상은 InfernoSpirit이 거는 화상과 같은 장치 · 같은 이펙트다(1초마다 「초당 피해」만큼 · 「지속」초 동안 — 유닛 규칙 40).
        //   이미 화상이 붙어 있는 유닛에 또 걸리면 화상이 둘이 되지 않고 하나만 남아 시간이 처음부터 다시 돈다(갱신 — 값은 새로 걸린 쪽).
        //   건물에는 걸지 않는다(상위 결정 18). 착탄 뒤에 원에 들어온 유닛에게도 걸지 않는다(착탄 순간 판정).
        //
        //   두 칸이 모두 0보다 커야 화상이 걸린다. 0(기본값)이면 화상 없음 → 포격 등 다른 스킬은 이 칸이 생겨도 동작이 같다.
        //   🔴 InfernoSpirit 설정(SpecialAttackConfig)을 읽지 않고 여기 따로 둔다 — 유닛 밸런스를 바꿔도 스킬 화상이 함께 바뀌지 않게(M-3).
        //   메테오 값(초당 50 × 3초 = 총 150)은 시험용 임시값이다(U-7 — 밸런싱 단계에서 정한다). ×10 스케일로 저작한다.
        //   셋업 도구(SupportSkillLoadoutSetup)가 이 두 칸을 표 값으로 다시 쓴다(메테오 50 · 3, 나머지 0).
        [Tooltip("(타입 A) 착탄 화상 초당 피해(×10 스케일). 착탄 순간 원 안에서 살아 남은 적 유닛에게 건다. 0이면 화상 없음. 메테오 임시 50.")]
        [Min(0f)]
        [SerializeField] private float _impactBurnDamagePerSecond = 0f;

        [Tooltip("(타입 A) 착탄 화상 지속 시간(초). 0이면 화상 없음. 메테오 임시 3.")]
        [Min(0f)]
        [SerializeField] private float _impactBurnDuration = 0f;

        [Header("착탄 예약 연출 (타입 A — 메테오용 · 예고 재생 속도 · 떨어지는 몸체 · 착탄 폭발)")]
        // 착탄 예약 연출 칸(2026-11-18 추가 — 메테오 Task, Plan P-4 · P-5 · P-15 · U-6 · U-10, 결정 27 · 28 · 29).
        //
        // 무엇인가(초급자용 설명):
        //   메테오는 화면에 이펙트를 세 가지 띄운다. 모두 서버가 확정한 발동 알림을 받은 각 화면(Host · Client)이 스스로 재생한다.
        //     ① 예고 = 위 「발동 VFX」 칸의 프리팹. 받은 순간 조준점 바닥(지면 + 0.06)에 1번, 크기 = 반경 ÷ 기준 반경(포격과 같다).
        //        재생 속도 = 아래 「예고 기준 시간」 ÷ 착탄 지연. 예고 프리팹이 1.5초 낙하에 맞춰 만들어졌는데 착탄 지연을 0.75초로
        //        줄이면 예고를 2배 빠르게 틀어 운석 도착과 예고 끝이 맞는다. 풀로 돌아갈 때 원래 속도로 되돌린다.
        //     ② 떨어지는 몸체 = 아래 「몸체 VFX」 칸(반복 재생 프리팹). 받은 순간 하늘 한쪽(착탄 지점 + 「시작 위치 보정」)에서 출발해
        //        착탄 지연 동안 착탄 지점까지 곧게(선형) 비스듬히 이동하고, 착탄 시각에 꼬리 정지(새 입자만 멈춤)한다.
        //        안전장치: 착탄 지연 + 0.5초가 지나도 아직 돌고 있으면 스스로 꼬리 정지한다(반복 프리팹이 풀에 영영 안 돌아오는 것 방지).
        //     ③ 착탄 = 아래 「착탄 VFX」 칸(1회 재생). 받은 시각 + 착탄 지연에 조준점 바닥에서 1번, 크기 = 반경 ÷ 기준 반경.
        //   세 칸이 모두 비어도 피해 · 화상은 그대로다(이펙트는 화면 표현일 뿐). 비어 있으면 진단 줄(MeteorVfxSkipped NoPrefab)만 남는다.
        //   🔴 예고 · 착탄 프리팹은 반복(Looping)하지 않아야 한다(1회 재생 뒤 풀로 돌아간다). 몸체 프리팹은 반대로 모든 파티클이
        //      Looping ON · Stop Action None이어야 한다(「상태 이펙트 계약」 — 어기면 재생하지 않는다, MeteorVfxSkipped LoopContract).
        //   이 세 칸 중 하나라도 채워진 타입 A 착탄 지연 스킬만 이 연출 경로를 탄다(SkillData.UsesImpactPresentation) — 포격은 그대로.
        //   셋업 도구는 이 칸들을 쓰지 않는다(다시 실행해도 넣은 값 · 프리팹이 남는다). 단 「발동 VFX」(예고) 칸은 도구가 다시 쓴다.
        [Tooltip("예고 VFX(위 「발동 VFX」 칸)가 만들어진 기준 시간(초). 예고 재생 속도 = 이 값 ÷ 착탄 지연. 0이면 재생 속도 맞춤 안 함(배율 1). 메테오 1.5.")]
        [Min(0f)]
        [SerializeField] private float _warningVfxReferenceTime = 0f;

        [Tooltip("떨어지는 몸체 VFX(반복 재생 · Looping). 하늘 한쪽에서 착탄 지점까지 착탄 지연 동안 비스듬히 이동하고 착탄 때 꼬리 정지한다. 비워도 피해는 동작.")]
        [SerializeField] private GameObject _fallingBodyVfxPrefab;

        [Tooltip("몸체 출발 위치 = 착탄 지점 + 이 값(화면 기준 월드 단위: X 오른쪽 · Y 위 · Z 화면 위쪽). 양쪽 화면에서 같은 쪽 하늘에서 떨어진다.")]
        [SerializeField] private Vector3 _fallingBodyStartOffset = new Vector3(-3f, 6f, 2f);

        [Tooltip("몸체 회전 보정(도). 몸체 앞(로컬 +Z)이 낙하 방향을 본 뒤 이만큼 더 돌린다(프리팹 모양 맞춤용).")]
        [SerializeField] private Vector3 _fallingBodyEulerOffset = Vector3.zero;

        [Tooltip("착탄 VFX(1회 재생). 착탄 시각에 조준점 바닥에서 1번 재생한다(크기 = 반경 ÷ 기준 반경). 비워도 피해는 동작.")]
        [SerializeField] private GameObject _impactVfxPrefab;

        /// <summary>
        /// 이 SO의 값을 Application 레이어 값 객체(SkillData)로 변환한다.
        /// 실행기/유스케이스/UI가 Infrastructure SO에 직접 의존하지 않도록 하는 경계(의존성 역전).
        /// </summary>
        /// <returns>이 정의로 만든 불변 SkillData.</returns>
        public SkillData ToSkillData()
        {
            return new SkillData(
                _icon,
                _aimType,
                _cooldown,
                _mechanic,
                _radius,
                _duration,
                _totalDamage,
                _damagePerSecond,
                _statusKind,
                _magnitude,
                _targetsAllies,
                _displayName,
                _description,
                _isAvailable,
                _impactDelay,
                _activationVfxPrefab,
                _vfxReferenceRadius,
                // 2026-11-10 강화 상태 VFX 칸 3개(위 「강화 상태 VFX」 머리말).
                _statusVfxPrefab,
                _statusVfxScale,
                _statusVfxOffset,
                // 2026-11-11 구역 VFX 칸(위 「구역 VFX」 머리말).
                _zoneVfxPrefab,
                // 2026-11-16 추가 효과 목록(위 「추가 효과 목록」 머리말).
                BuildExtraEffects(),
                // 2026-11-16 단계 C 건물 상태 VFX 칸 3개(위 「건물 상태 VFX」 머리말).
                _buildingStatusVfxPrefab,
                _buildingStatusVfxScale,
                _buildingStatusVfxOffsetY,
                // 2026-11-17 공습 직선 연속 폭격 칸 4개(위 「직선 연속 폭격」 머리말).
                _lineLength,
                _lineWidth,
                _lineStrikeCount,
                _lineStrikeInterval,
                // 2026-11-17 공습 L-5 VFX 칸 2개(위 「공습 VFX」 머리말).
                _lineVfxReferenceLength,
                _strikeVfxPrefab,
                // 2026-11-18 메테오 착탄 화상 칸 2개 · 착탄 예약 연출 칸 5개(위 두 머리말).
                _impactBurnDamagePerSecond,
                _impactBurnDuration,
                _warningVfxReferenceTime,
                _fallingBodyVfxPrefab,
                _fallingBodyStartOffset,
                _fallingBodyEulerOffset,
                _impactVfxPrefab);
        }

        /// <summary>
        /// 직렬화된 추가 효과 줄을 Application 값(SkillExtraEffect) 배열로 옮긴다. 비었거나 null 줄은 건너뛴다.
        /// 정수 코드 → 열거형 변환만 하고 값 검사(지원하는 조합인가)는 서버 서비스가 한다(잘못된 줄은 서버가 거부 · 진단 Warn).
        /// </summary>
        private SkillExtraEffect[] BuildExtraEffects()
        {
            if (_extraEffects == null || _extraEffects.Length == 0) return null; // SkillData가 빈 목록으로 바꾼다.
            var list = new System.Collections.Generic.List<SkillExtraEffect>(_extraEffects.Length);
            for (int i = 0; i < _extraEffects.Length; i++)
            {
                ExtraEffectEntry e = _extraEffects[i];
                if (e == null) continue;
                list.Add(new SkillExtraEffect((SkillEffectTarget)e.target, (StatusEffectKind)e.statusKind, e.magnitude));
            }
            return list.ToArray();
        }
    }
}
