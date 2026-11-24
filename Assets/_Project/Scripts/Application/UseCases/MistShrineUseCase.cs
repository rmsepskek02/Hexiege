// ============================================================================
// MistShrineUseCase.cs
// MistShrine(초월 종족 회복 건물)의 "물안개 힐" 전용 유스케이스.
//
// 무엇을 하나(유니티 초급자용 설명):
//   플레이어가 MistShrine의 [사용] 버튼을 누르면 그 건물 주위에 **물안개**가 깔린다.
//   물안개가 깔려 있는 동안, 그 범위 안에 있는 **내 편 유닛과 내 편 건물**이 1초마다 체력을 회복한다.
//   (시전한 건물 자신과 본진 Castle도 내 것이므로 회복 대상이다 — 규칙 4.)
//   물안개는 정해진 시간이 지나면 걷히고, 쿨다운이 끝나야 다시 쓸 수 있다.
//
//   이 클래스는 위 동작의 "머리" 역할만 한다:
//     · 시전 재검증(건물이 살아 있는가 / MistShrine이 맞는가 / 쿨다운이 끝났는가)
//     · 물안개 목록 관리(생성 · 시간 진행 · 소멸)
//     · 매 1초 회복 적용 + 회복 텍스트 주기 제어
//     · 쿨다운 · 자동 모드(bool 1개) 상태 보관
//   화면 표시(패널·범위 원)는 Presentation이, 멀티 동기화는 Infrastructure가 맡는다.
//
// ── 왜 기존 HoT/DoT 시스템(_activeTimedEffects)을 쓰지 않는가 (중요) ──
//   GameSystemRules_Buildings.md MistShrine 규칙 8-2가 **명시적으로 금지**한다. 이유는 세 가지다.
//     ① UnitCombatUseCase의 discrete 틱 분기는 효과 종류(Kind)를 검사하지 않고 무조건 "피해"를 적용한다
//        → Kind=Heal 레코드를 넣으면 회복시켜야 할 대상이 매초 피해를 입는다.
//     ② 그 시스템의 대상 조회가 유닛 딕셔너리 전용이라 **건물이 대상이 될 수 없다**(규칙 4 위반).
//     ③ "총량을 나눠 준다" 모델이라 **범위를 벗어나면 즉시 끊기는 아우라**(규칙 9)를 표현할 수 없다.
//   그래서 자연회복(UnitCombatUseCase.TickNaturalRegen)이 이미 쓰고 있는
//   **"독립 누적기 + 매 틱 대상 재수집"** 모델을 그대로 따른다(규칙 8-1).
//   자연회복과 모델이 닮았을 뿐 **누적기·채널은 완전히 독립**이므로, 자연회복·BloomFairy 힐과
//   서로 덮어쓰지 않고 함께 적용된다(규칙 14).
//
// ── 중첩 해소(규칙 13) ──
//   물안개가 2개 이상 겹쳐도 한 대상은 회복을 **한 번만** 받는다. 대상마다 "가장 가까운 물안개"를 고르고,
//   거리가 완전히 같으면 **시전 건물 Id가 작은 쪽**을 쓴다. 딕셔너리 순회 순서에 맡기면 서버와
//   클라이언트의 판정이 갈릴 수 있으므로, 시전 건물 Id 오름차순 순회 + 엄격 부등호(<)로 못 박는다.
//
//   ⚠️ 2026-08-12 버그 수정 — 중첩 해소가 "돌 기회조차 없던" 문제 (초당 회복량 2배)
//     증상: 반경이 거의 완전히 겹치는 신전 2개 아래에 있던 유닛이 초당 10이 아니라 **초당 20** 회복.
//     원인: 회복 틱 누적기(ActiveMist.TickAccum)가 물안개마다 독립인데 **시전 시각이 다르면 1초 경계를
//           넘는 프레임도 서로 달라진다**(실측: 신전6 = 매초 .36초 지점, 신전10 = 매초 .73초 지점).
//           그래서 한 프레임에는 언제나 물안개가 하나만 발화했고, "같은 프레임에 발화한 물안개들끼리"만
//           비교하던 중첩 해소 코드는 비교 대상이 늘 1개라 **사실상 죽은 코드**였다.
//     수정: 두 겹으로 막는다.
//       ① **위상(phase) 정렬** — 새 물안개는 이미 깔려 있는 물안개와 **같은 누적기 위상**으로 시작한다
//          (Activate에서 GetSharedTickPhase()로 시드). 누적기 자체는 규칙 8-1대로 여전히 물안개마다
//          하나씩이며(개수 = 물안개 개수 — 규칙 14 표), 시작 위상만 맞춘다. 모든 활성 물안개가 같은
//          프레임에 발화하므로 중첩 해소가 실제로 동작하고, 물안개가 생기거나 걷힐 때도 회복 간격이
//          끊기거나 겹치지 않는다.
//       ② **소유권 판정 범위 확대** — "이번 틱에 발화한 물안개들 중 가장 가까운 것"이 아니라
//          **"지금 깔려 있는 물안개 전체 중 가장 가까운 것"** 을 대상의 소유자로 정하고, 그 소유자가
//          이번 틱에 발화했을 때만 회복한다. ①이 지켜지는 한 두 집합은 항상 같지만, 혹시라도 위상이
//          어긋나더라도 "먼저 틱한 물안개가 이긴다" 같은 순서 의존 판정이나 이중 회복이 생기지 않는다.
//
//   ⚠️ 2026-11-25 버그 수정 — 위상 정렬 때문에 물안개 1개가 가끔 **11번** 회복하던 문제
//     (Task `_Tasks/2026-11-25/01_19_mistshrine-heal-tick-cap` — Research 3절 · Plan 2절 추천안 A)
//     증상: 멀티 Host(서버 틱 0.1초)에서 물안개 31개 중 1개가 10번이 아니라 11번 회복(대상당 100이 아니라 110).
//     원인: 위 ①(위상 정렬)로 새 물안개가 「이미 0.9초 넘게 차 있는 누적기」를 받으면 첫 회복이 시작 직후에 온다.
//           그러면 11번째 회복 차례가 지속시간이 끝나는 마지막 프레임과 같은 프레임에 들어오는데,
//           TickMists는 「회복 발화 판정」을 「소멸 판정」보다 먼저 하므로 한 번 더 회복해 버렸다.
//     수정: 물안개마다 「실제로 회복한 횟수」를 세고, **지속시간 ÷ 1초(소수점 버림, 지금 10)** 번을 채우면
//           남은 시간이 있어도 더는 회복하지 않는다(상한). 사용자 확정 기획: 「1초마다 10 회복, 모두 10번」.
//           위상 정렬(①)과 소유권 판정(②)은 **그대로 둔다** — 되돌리면 2026-08-12의 2배 회복이 재발한다.
//           · 상한에 닿은 물안개도 누적기는 다른 물안개와 똑같이 진행·차감한다(위상 불변식 유지 — TickMists 주석).
//           · 상한에 닿았고 이번 프레임에 회복하지 않은 물안개는 사실상 「이미 걷힌 물안개」로 보고
//             소유권 후보에서 뺀다(ApplyHealTick) — 그 물안개가 겹친 대상을 붙잡고 다른 물안개의 회복을 막지 않게.
//
// ── 서버 권위(규칙 22) ──
//   시전 판정·대상 수집·회복 적용·자동 시전은 서버(싱글=로컬 서버 / 멀티=서버)에서만 실행한다.
//   멀티 클라이언트는 쿨다운 미러·자동 모드 미러·HP 수신만 한다.
//   호출부 실수로 클라이언트에서 틱이 돌더라도 이중 회복이 생기지 않도록 내부에도 가드를 둔다.
//
// Application 레이어 — Domain/Application 의존. Unity.Netcode 직접 참조 없음(NetworkContext 정적 홀더만 사용).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// MistShrine 물안개 힐의 시전·물안개 수명·회복 적용·쿨다운·자동 모드를 담당하는 서버 권위 유스케이스.
    /// </summary>
    public sealed class MistShrineUseCase
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>
        /// 회복 틱 간격(초). 설계 고정값 1초이며 튜닝 대상이 아니다(규칙 8 — 회복 주기 1초).
        /// </summary>
        private const float HealTickInterval = 1.0f;

        // ====================================================================
        // 의존성(조합 루트 GameBootstrapper에서 생성자 주입)
        // ====================================================================

        /// <summary>건물 조회·순회(시전 건물 재검증 + 아군 건물 회복 대상 수집).</summary>
        private readonly BuildingPlacementUseCase _buildingPlacement;

        /// <summary>유닛 순회(아군 유닛 회복 대상 수집).</summary>
        private readonly UnitSpawnUseCase _unitSpawn;

        /// <summary>헥스 좌표 → 도메인 월드 좌표 변환기. Application이 Core를 직접 참조하지 않기 위한 인터페이스.</summary>
        private readonly IHexCoordinateMapper _mapper;

        // ── 튜닝값(SpecialAttackConfig에서 float 원시값으로 주입 — 전부 임시값, 밸런싱 미확정) ──
        private readonly float _healPerSecond;   // 초당 회복량(HP/초)
        private readonly float _mistDuration;    // 물안개 지속시간(초)
        private readonly float _cooldown;        // 시전 쿨다운(초)
        private readonly float _radius;          // 회복 범위 반경(월드 단위, 1.0 ≈ 인접 1타일)
        private readonly float _textInterval;    // 회복 텍스트 표시 주기(초)

        /// <summary>회복 반경의 제곱값. 매 틱 제곱근 계산을 피하기 위해 생성 시 1회 계산해 둔다.</summary>
        private readonly float _radiusSqr;

        /// <summary>1초 틱 1회당 실제 적용할 회복량(HP, 정수). 소수점은 올림한다(DoT 틱 관례와 동일).</summary>
        private readonly int _healPerTick;

        /// <summary>
        /// 물안개 1개가 회복할 수 있는 최대 횟수(= 지속시간 ÷ 회복 간격 1초, 소수점 버림). 생성 시 1회 계산한다.
        ///
        /// 왜 필요한가(유니티 초급자용 설명, 2026-11-25):
        ///   기획은 「물안개 1회 = 1초마다 회복, 모두 (지속시간 ÷ 1초)번」이다(지금 임시값으로 10초 → 10번).
        ///   그런데 새 물안개는 이미 깔린 물안개와 박자를 맞추느라(위상 정렬) 첫 회복이 시작 직후에 올 수 있고,
        ///   그러면 마지막 프레임에 한 번 더(11번째) 회복하는 경우가 생겼다. 그래서 횟수 자체에 상한을 둔다.
        ///
        /// 왜 반올림이 아니라 버림인가:
        ///   지속시간이 10.5초처럼 소수이면, 위상 0으로 시작한 물안개는 10번밖에 회복하지 못한다(10.5초 안에 1초가 10번).
        ///   반올림(11)을 쓰면 위상에 따라 10번 · 11번이 갈리지만, 버림(10)이면 모든 물안개가 같은 횟수가 된다.
        /// </summary>
        private readonly int _healTickCap;

        /// <summary>
        /// 상한 계산의 부동소수 여유값. 예: 지속시간이 설정 파일에서 9.9999995처럼 읽혀도
        /// 10 ÷ 1 = 9.99… → 버림 9가 되지 않도록 아주 작은 값을 더한 뒤 버린다.
        /// </summary>
        private const float HealTickCapEpsilon = 1e-4f;

        // ====================================================================
        // 상태(전부 private — 서버 권위. 쿨다운/자동 모드만 멀티 클라 미러로도 쓰인다)
        // ====================================================================

        /// <summary>건물 Id → 자동 모드 on/off. 기능이 하나뿐이라 리스트가 아닌 bool 1개다(규칙 19).</summary>
        private readonly Dictionary<int, bool> _autoMode = new Dictionary<int, bool>();

        /// <summary>건물 Id → 남은 쿨다운(초).</summary>
        private readonly Dictionary<int, float> _cooldownRemaining = new Dictionary<int, float>();

        /// <summary>건물 Id → 발동 시점의 총 쿨다운(초). 쿨다운 오버레이 radial fill 비율 계산용.</summary>
        private readonly Dictionary<int, float> _cooldownTotal = new Dictionary<int, float>();

        /// <summary>현재 깔려 있는 물안개 목록(서버 전용). 물안개마다 자기 누적기를 갖는다(규칙 8-1).</summary>
        private readonly List<ActiveMist> _activeMists = new List<ActiveMist>(4);

        // ── 재사용 버퍼(GC 절감). 서버/클라 모두 단일 스레드 갱신이라 재사용이 안전하다. ──
        private readonly List<int> _tickKeyBuffer = new List<int>(8);
        private readonly List<int> _expiredBuffer = new List<int>(8);
        private readonly List<int> _autoCastBuffer = new List<int>(4);
        private readonly List<UnitData> _unitBuffer = new List<UnitData>(16);
        private readonly List<float> _unitDistBuffer = new List<float>(16);
        private readonly List<BuildingData> _buildingBuffer = new List<BuildingData>(16);
        private readonly List<float> _buildingDistBuffer = new List<float>(16);
        private readonly Dictionary<int, PickedUnit> _bestUnitByTarget = new Dictionary<int, PickedUnit>();
        private readonly Dictionary<int, PickedBuilding> _bestBuildingByTarget = new Dictionary<int, PickedBuilding>();

        /// <summary>
        /// 물안개를 시전 건물 Id 오름차순으로 정렬하는 비교자(중첩 해소 결정성 — 규칙 13).
        /// 거리가 완전히 같을 때 "Id가 작은 쪽"이 이기게 하려면, 비교를 Id 오름차순으로 돌면서
        /// 엄격 부등호(&lt;)로만 갱신하면 된다(먼저 온 = Id가 작은 물안개가 그대로 유지된다).
        /// </summary>
        private static readonly Comparison<ActiveMist> MistIdAscending =
            (a, b) => a.SourceBuildingId.CompareTo(b.SourceBuildingId);

        /// <summary>
        /// 서버에서 물안개 시전이 성공했을 때 발화되는 훅. 인자: (시전 건물 Id, 총 쿨다운).
        ///
        /// 왜 이벤트인가:
        ///   자동 모드 시전은 서버가 스스로 하기 때문에 RPC 왕복이 없다. 그러면 멀티 클라이언트의
        ///   쿨다운 오버레이가 갱신되지 않는다. 그래서 Infrastructure(NetworkMistShrineController)가
        ///   이 훅을 구독해 양 클라에 브로드캐스트한다.
        ///   (UnitCombatUseCase.OnGlobalStatusApplied / UnitUpgradeUseCase.OnResearchCompleted와 같은
        ///    Application → Infrastructure 의존성 역전 패턴이다.)
        ///   싱글플레이에서는 구독자가 없어 아무 일도 일어나지 않는다.
        /// </summary>
        public event Action<int, float> OnMistActivated;

        // ====================================================================
        // [2026-11-17 지원 스킬 새 상태 Task 단계 B — 구현 단위 B-2 · Plan 6-2-4] 지원 스킬 효과 서비스 — 받는 회복 배율(재생의 뿌리).
        //   물안개 회복이 HP에 들어가는 두 자리(유닛 · 건물)가 회복 직전에 이 서비스에 「이 대상의 받는 회복 배율」을 묻는다.
        //   설정자로 주입한다 — 생성자를 바꾸면 이 UseCase를 만드는 다른 코드(조합 루트 · Editor 검사)가 함께 깨지므로 생성자는 그대로 둔다.
        //   미주입(null) · 효과 없는 대상이면 회복량을 곱하지 않는다 → 기존 물안개 회복(틱당 10)과 정확히 같다(회귀 안전).
        //   물안개 자체의 규칙(1초 주기 · 중첩 해소 · 범위 · 표시 주기)은 바꾸지 않는다 — 배율은 「이 대상이 받는 양」에만 곱한다.
        // ====================================================================
        private SupportSkillEffectSystem _supportEffects;

        /// <summary>
        /// 지원 스킬 효과 서비스를 주입한다(GameBootstrapper에서 맵마다 1회 — 전투 · 스킬 UseCase와 같은 인스턴스).
        /// 미주입(null)이면 받는 회복 배율이 없다 → 기존 물안개 회복과 완전히 같다(하위호환).
        /// </summary>
        /// <param name="supportEffects">같은 경기의 지원 스킬 효과 서비스.</param>
        public void SetSupportSkillEffectSystem(SupportSkillEffectSystem supportEffects)
        {
            _supportEffects = supportEffects;
        }

        // 물안개 1틱 회복량에 받는 회복 배율을 거친 값(배율이 없으면 _healPerTick 그대로).
        private int AmplifiedMistHeal(IDamageable target)
        {
            return _supportEffects != null
                ? _supportEffects.AmplifyHeal(target, _healPerTick, HealAmpSource.Mist)
                : _healPerTick;
        }

        // ====================================================================
        // 생성
        // ====================================================================

        /// <summary>
        /// MistShrine 물안개 힐 유스케이스 생성.
        /// </summary>
        /// <param name="buildingPlacement">건물 조회·순회 UseCase.</param>
        /// <param name="unitSpawn">유닛 조회·순회 UseCase.</param>
        /// <param name="mapper">헥스 → 도메인 월드 좌표 변환기.</param>
        /// <param name="healPerSecond">초당 회복량(HP/초). 임시값.</param>
        /// <param name="mistDuration">물안개 지속시간(초). 임시값.</param>
        /// <param name="cooldown">시전 쿨다운(초). 임시값.</param>
        /// <param name="radius">회복 범위 반경(월드 단위). 임시값.</param>
        /// <param name="textInterval">회복 텍스트 표시 주기(초). 임시값.</param>
        public MistShrineUseCase(
            BuildingPlacementUseCase buildingPlacement,
            UnitSpawnUseCase unitSpawn,
            IHexCoordinateMapper mapper,
            float healPerSecond,
            float mistDuration,
            float cooldown,
            float radius,
            float textInterval)
        {
            _buildingPlacement = buildingPlacement;
            _unitSpawn = unitSpawn;
            _mapper = mapper;

            _healPerSecond = healPerSecond;
            _mistDuration = mistDuration;
            _cooldown = cooldown;
            _radius = radius;
            // 텍스트 주기가 0 이하로 잘못 설정되면 매 틱 텍스트가 도배되므로 최소 1틱(1초)으로 보정한다.
            _textInterval = textInterval > 0f ? textInterval : HealTickInterval;

            _radiusSqr = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            // 틱 간격이 정확히 1초이므로 "초당 회복량 = 틱당 회복량"이다. 소수점은 올림(DoT 틱과 같은 관례).
            _healPerTick = Mathf.Max(0, Mathf.CeilToInt(healPerSecond));
            // 물안개 1개의 회복 횟수 상한 = 지속시간 ÷ 1초의 소수점 버림(지금 10초 → 10번).
            //   지속시간이 0 이하(또는 잘못된 값)이면 0 → 그 물안개는 회복하지 않는다(음수 상한 방지로 Max(0, …)).
            _healTickCap = Mathf.Max(0, Mathf.FloorToInt(mistDuration / HealTickInterval + HealTickCapEpsilon));
        }

        // ====================================================================
        // 시전(서버 권위 단일 진입점 — 플레이어 수동 시전 / 자동 시전 공용)
        // ====================================================================

        /// <summary>
        /// MistShrine을 시전해 물안개를 깐다(서버 권위). 재검증을 모두 통과하면 물안개를 만들고 쿨다운을 건다.
        /// </summary>
        /// <param name="buildingId">시전한 MistShrine의 건물 Id.</param>
        /// <returns>시전에 성공하면 true(호출자가 쿨다운 브로드캐스트에 사용), 재검증 실패면 false.</returns>
        public bool Activate(int buildingId)
        {
            // [진단 MIST-DIAG] 아래 재검증 ①~③ 중 하나에 막히면 그 사유를 CastResult Result=Reject* 1줄로 남긴다.
            //   아래 if 문들을 고치지 않으려고, 같은 조건을 「읽기만」 해서 미리 판정한다(개발 빌드 전용 — 릴리스에서는 호출째 사라진다).
            DiagCastPrecheck(buildingId);

            // 서버 전용 가드(2중 방어) — 호출부가 실수로 클라이언트에서 불러도 물안개가 생기지 않는다.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return false;

            // ① 건물 존재·생존 재검증(클라 입력 불신뢰).
            BuildingData building = _buildingPlacement?.GetBuilding(buildingId);
            if (building == null || !building.IsAlive) return false;

            // ② MistShrine이 맞는지 재검증(다른 건물에서의 오발동 차단 — 규칙 1).
            if (building.Type != BuildingType.HealShrine) return false;

            // ③ 쿨다운 만료 재검증(규칙 21).
            if (GetCooldownRemaining(buildingId) > 0f) return false;

            // ④ 같은 건물이 만든 이전 물안개가 남아 있으면 정리한다.
            //    지속시간 < 쿨다운(규칙 7)이라 정상적으로는 겹칠 수 없지만, 튜닝값을 잘못 넣어
            //    지속 ≥ 쿨다운이 되더라도 한 건물이 물안개를 두 겹 만들지 않도록 방어한다.
            // [진단 MIST-DIAG] 지워지기 직전의 이전 물안개가 있으면 MistEnded Reason=Replaced 1줄(정상 설정값에서는 나오지 않는다).
            DiagEndMistsOfShrine(buildingId, "Replaced");
            RemoveMistsOfShrine(buildingId);

            // ⑤ 물안개 생성 — 중심은 시전 건물 타일의 "도메인" 월드 좌표다(규칙 3·10 — 고정 원형, 이동 없음).
            //    회복 틱 누적기는 **이미 깔려 있는 물안개와 같은 위상**에서 출발시킨다(GetSharedTickPhase).
            //    이유: 물안개마다 시전 시각이 달라 1초 경계를 넘는 프레임이 어긋나면, 겹친 대상이
            //    각 물안개에서 따로 회복을 받아 초당 회복량이 배로 뛴다(2026-08-12 버그 — 파일 상단 주석).
            //    누적기는 규칙 8-1대로 여전히 물안개마다 하나씩이고, "시작 위상"만 맞추는 것이다.
            Vector3 center = Flatten(_mapper.HexToWorld(building.Position));
            _activeMists.Add(new ActiveMist(buildingId, building.Team, center, _mistDuration, GetSharedTickPhase()));

            // ⑥ 쿨다운 시작(규칙 21). 시전 비용은 없다(규칙 11 — 골드 소모 없음).
            StartCooldownLocal(buildingId, _cooldown);

            // [진단 MIST-DIAG] 시전 성공 1줄(CastResult Result=Ok · CastNo). 훅(⑦)보다 먼저 남겨 CastNo가 먼저 정해지게 한다
            //   — 훅을 받는 네트워크 컨트롤러가 ActivateSent 줄에 같은 CastNo를 싣는다.
            DiagCastOk(buildingId);

            // ⑦ 멀티 클라 쿨다운 미러용 훅 발화(싱글은 구독자 없음).
            OnMistActivated?.Invoke(buildingId, _cooldown);
            return true;
        }

        // ====================================================================
        // 자동 모드 상태(규칙 18·19)
        // ====================================================================

        /// <summary>
        /// 지정 건물의 자동 모드를 설정한다. 멀티 클라이언트는 서버 브로드캐스트를 받아 이 메서드로 미러링한다.
        /// (기본값은 OFF이므로 false는 딕셔너리에서 제거해 상태를 깨끗하게 유지한다.)
        /// </summary>
        /// <param name="buildingId">대상 MistShrine 건물 Id.</param>
        /// <param name="on">자동 모드를 켤지 여부.</param>
        public void SetAutoMode(int buildingId, bool on)
        {
            if (on) _autoMode[buildingId] = true;
            else _autoMode.Remove(buildingId);
        }

        /// <summary> 지정 건물의 자동 모드가 켜져 있는지 여부. 건설 직후 기본값은 OFF다(규칙 18). </summary>
        /// <param name="buildingId">대상 MistShrine 건물 Id.</param>
        public bool GetAutoMode(int buildingId)
        {
            return _autoMode.TryGetValue(buildingId, out bool on) && on;
        }

        /// <summary>
        /// 지정 건물의 자동 모드를 토글하고 그 결과를 돌려준다(서버가 브로드캐스트할 값).
        /// </summary>
        /// <param name="buildingId">대상 MistShrine 건물 Id.</param>
        /// <returns>토글 후의 자동 모드 상태.</returns>
        public bool ToggleAutoMode(int buildingId)
        {
            bool next = !GetAutoMode(buildingId);
            SetAutoMode(buildingId, next);
            return next;
        }

        // ====================================================================
        // 쿨다운 상태(규칙 21 — 스킬 시스템과 동일 패턴)
        // ====================================================================

        /// <summary>
        /// 지정 건물의 쿨다운을 시작(또는 갱신)한다.
        /// 서버 시전 시에 호출되고, 멀티 클라이언트는 브로드캐스트를 받아 "표시용 로컬 미러"로도 호출한다.
        /// (이름의 Local은 SkillActivationUseCase.StartCooldownLocal과 같은 의미다.)
        /// </summary>
        /// <param name="buildingId">쿨다운을 걸 건물 Id.</param>
        /// <param name="cooldown">쿨다운 시간(초). 0 이하이면 즉시 사용 가능 상태로 정리한다.</param>
        public void StartCooldownLocal(int buildingId, float cooldown)
        {
            if (cooldown <= 0f)
            {
                _cooldownRemaining.Remove(buildingId);
                _cooldownTotal.Remove(buildingId);
                return;
            }
            _cooldownRemaining[buildingId] = cooldown;
            _cooldownTotal[buildingId] = cooldown;
            // [진단 MIST-DIAG] 쿨다운 시작 시점의 진단 시계를 적어 둔다(만료 때 ElapsedMs 계산용 — 줄은 남기지 않는다).
            DiagCooldownStarted(buildingId);
        }

        /// <summary>
        /// 모든 MistShrine의 쿨다운을 감소시킨다.
        /// 호출 위치: 싱글 = GameBootstrapper.Update / 멀티 서버 = NetworkCombatController.TickCombat /
        ///            멀티 순수 클라 = GameBootstrapper.Update(표시용 로컬 미러).
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void TickCooldowns(float dt)
        {
            if (dt <= 0f || _cooldownRemaining.Count == 0) return;

            // [진단 MIST-DIAG] 쿨다운 진단 시계를 이번 dt만큼 진행한다(줄 없음 — 만료 줄의 ElapsedMs 재료).
            DiagCooldownAdvance(dt);

            // 순회 중 제거는 예외를 일으키므로, 키를 모아 두고 값 갱신 → 만료분 일괄 제거 순으로 처리한다.
            _tickKeyBuffer.Clear();
            _expiredBuffer.Clear();

            foreach (var kv in _cooldownRemaining)
                _tickKeyBuffer.Add(kv.Key);

            for (int i = 0; i < _tickKeyBuffer.Count; i++)
            {
                int id = _tickKeyBuffer[i];
                float remaining = _cooldownRemaining[id] - dt;
                if (remaining <= 0f) _expiredBuffer.Add(id);
                else _cooldownRemaining[id] = remaining;
            }
            _tickKeyBuffer.Clear();

            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                // [진단 MIST-DIAG] 만료 1회 = CooldownExpired 1줄. 총 쿨다운 값을 읽어야 하므로 지우기 직전에 부른다.
                DiagCooldownExpired(_expiredBuffer[i]);
                _cooldownRemaining.Remove(_expiredBuffer[i]);
                _cooldownTotal.Remove(_expiredBuffer[i]);
            }
            _expiredBuffer.Clear();
        }

        /// <summary> 지정 건물의 남은 쿨다운(초). 쿨다운이 없으면 0. </summary>
        /// <param name="buildingId">조회할 건물 Id.</param>
        public float GetCooldownRemaining(int buildingId)
        {
            return _cooldownRemaining.TryGetValue(buildingId, out float r) ? r : 0f;
        }

        /// <summary> 지정 건물의 발동 시점 총 쿨다운(초). 오버레이 radial fill 비율 계산용. 없으면 0. </summary>
        /// <param name="buildingId">조회할 건물 Id.</param>
        public float GetCooldownTotal(int buildingId)
        {
            return _cooldownTotal.TryGetValue(buildingId, out float t) ? t : 0f;
        }

        /// <summary> 지정 건물이 현재 쿨다운 중인지 여부(규칙 21). </summary>
        /// <param name="buildingId">조회할 건물 Id.</param>
        public bool IsOnCooldown(int buildingId)
        {
            return GetCooldownRemaining(buildingId) > 0f;
        }

        /// <summary>
        /// 회복 범위 반경(월드 단위)을 반환한다. 실제 회복 판정에 쓰이는 값 그대로다.
        ///
        /// 왜 공개하나(중요):
        ///   패널이 지도에 그리는 범위 원과 실제 회복 판정 범위는 "반드시" 같아야 한다.
        ///   예전에는 패널이 자기 Inspector 값(_rangeRadius)을 따로 들고 있었는데,
        ///   밸런싱으로 SpecialAttackConfig.asset의 MistRadius만 바꾸면 두 값이 조용히 어긋나
        ///   "원 밖인데 회복되거나 / 원 안인데 회복되지 않는" 상태가 되었다.
        ///   그래서 반경의 단일 소스를 이 UseCase(=설정 에셋에서 주입받은 값)로 통일했다.
        /// </summary>
        public float GetRadius()
        {
            return _radius;
        }

        // ====================================================================
        // 자동 시전 틱(규칙 18 — 쿨다운이 끝나는 즉시 서버가 자동으로 시전)
        // ====================================================================

        /// <summary>
        /// 자동 모드가 켜져 있고 쿨다운이 끝난 MistShrine을 자동으로 시전한다(서버 전용).
        /// 시전 순서는 건물 Id 오름차순으로 고정해 실행 순서가 매번 같도록 한다.
        /// </summary>
        /// <param name="dt">경과 시간(초). 현재는 쿨다운 판정만 하므로 직접 쓰지 않지만 다른 틱과 시그니처를 맞춘다.</param>
        public void TickAutoCast(float dt)
        {
            // 서버 전용 가드(2중 방어) — 클라이언트가 멋대로 자동 시전하지 않도록.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            if (_autoMode.Count == 0) return;

            _autoCastBuffer.Clear();
            foreach (var kv in _autoMode)
            {
                if (!kv.Value) continue;
                if (IsOnCooldown(kv.Key)) continue;
                _autoCastBuffer.Add(kv.Key);
            }
            if (_autoCastBuffer.Count == 0) return;

            // 딕셔너리 순회 순서에 의존하지 않도록 Id 오름차순으로 정렬한다(결정성).
            _autoCastBuffer.Sort();

            // [진단 MIST-DIAG] 이 반복 안의 Activate 호출은 자동 시전이다 → CastResult 줄의 Origin=Auto.
            //   Activate의 시그니처를 바꾸지 않으려고 진단 전용 깃발 하나로 알린다(반복이 끝나면 바로 내린다).
            DiagSetAutoOrigin(true);
            for (int i = 0; i < _autoCastBuffer.Count; i++)
            {
                // Activate 안에서 건물 생존·타입·쿨다운을 다시 확인하므로 여기서 중복 검사하지 않는다.
                Activate(_autoCastBuffer[i]);
            }
            DiagSetAutoOrigin(false);
            _autoCastBuffer.Clear();
        }

        // ====================================================================
        // 물안개 틱(핵심 — 규칙 6·8·9·13·15)
        // ====================================================================

        /// <summary>
        /// 물안개의 시간을 진행시키고, 1초가 찰 때마다 그 시점의 범위 안 아군을 다시 수집해 회복시킨다.
        /// 지속시간이 끝난 물안개는 제거한다.
        ///
        /// 매 틱 대상을 "다시" 수집하는 것이 핵심이다 — 그래야 범위를 벗어난 대상은 즉시 회복이 끊기고,
        /// 다시 들어오면 다시 회복된다(규칙 9 아우라). 중첩 해소도 매 틱 다시 판정된다(규칙 13).
        ///
        /// 위상 불변식(2026-08-12):
        ///   활성 물안개들의 TickAccum은 항상 서로 같다. 아래 루프가 모든 물안개에 **같은 dt**를 더하고
        ///   같은 프레임에 똑같이 1초를 빼며, 새 물안개도 생성 시 같은 위상에서 출발하기 때문이다(Activate ⑤).
        ///   이 불변식 덕분에 활성 물안개는 전부 같은 프레임에 발화하고, 중첩 해소가 실제로 동작한다.
        ///
        /// 회복 횟수 상한(2026-11-25):
        ///   물안개 1개는 최대 _healTickCap번(지속시간 ÷ 1초, 지금 10번)만 회복한다. 상한에 닿은 물안개는
        ///   남은 시간이 있어도 회복을 「발화」하지 않지만, **누적기 진행과 1초 차감은 다른 물안개와 똑같이 한다**.
        ///   그래야 위 위상 불변식이 깨지지 않는다(GetSharedTickPhase가 Id가 가장 작은 물안개 하나만 읽기 때문 —
        ///   그 물안개가 상한에 닿은 물안개일 수도 있다).
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void TickMists(float dt)
        {
            // 서버 전용 가드(2중 방어) — 클라이언트에서 돌면 회복이 두 번 적용된다.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            if (dt <= 0f || _activeMists.Count == 0) return;

            // [진단 MIST-DIAG] 가드를 통과한 호출 1회 기록 — 같은 프레임 2회 통과(이중 틱)면 DoubleTick Warn 첫 1줄,
            //   물안개마다 받은 dt 합(SumDtMs) 누적. 판정 값(Remaining · TickAccum)은 건드리지 않는다.
            DiagTickEnter(dt);

            // ── 1) 시간 진행 + 이번 틱에 회복을 발화할 물안개 표시 ───────────────
            //    발화 여부를 리스트에 모으지 않고 물안개 자신에게 표시(FiredThisTick)해 두는 이유:
            //    중첩 해소는 "발화한 물안개들"이 아니라 **"활성 물안개 전체"** 를 놓고 가장 가까운 것을
            //    골라야 하기 때문이다(규칙 13). 발화 여부는 마지막 적용 단계에서만 본다.
            bool anyFired = false;
            for (int i = 0; i < _activeMists.Count; i++)
            {
                ActiveMist mist = _activeMists[i];
                mist.Remaining -= dt;
                mist.TickAccum += dt;

                // 아직 1초가 차지 않았으면 이번 프레임에는 회복하지 않는다.
                // (위상 불변식이 지켜지는 한 활성 물안개는 전부 함께 통과하거나 전부 함께 여기서 빠진다.)
                if (mist.TickAccum < HealTickInterval)
                {
                    mist.FiredThisTick = false;
                    mist.ShowTextThisTick = false;
                    continue;
                }

                // 1초가 찼다 — 누적분에서 정확히 1초만 뺀다(이월분 보존).
                //   ⚠️ 이 차감은 아래 「회복 횟수 상한」 검사보다 **반드시 먼저** 한다(2026-11-25).
                //   상한에 닿은 물안개만 차감을 건너뛰면 그 물안개의 누적기가 다른 물안개와 달라져
                //   위상 불변식이 깨지고, 그 물안개가 목록 맨 앞([0])이면 새 물안개가 틀린 위상을 받는다.
                mist.TickAccum -= HealTickInterval;

                // 회복 횟수 상한(2026-11-25 — 규칙 6 · 8, Plan 2절 A-3).
                //   이미 상한(지금 10번)만큼 회복한 물안개는 남은 시간이 있어도 이번 회복을 발화하지 않는다.
                //   텍스트 주기 누적기(TextAccum)도 쌓지 않는다 — 「실제로 회복한 시간」만 세는 누적기이기 때문이다(규칙 15).
                if (mist.HealTickCount >= _healTickCap)
                {
                    mist.FiredThisTick = false;
                    mist.ShowTextThisTick = false;
                    // [진단 MIST-DIAG] 상한 때문에 막은 발화 1회 기록(줄 없음 — MistEnded의 TickCapBlocked 합계).
                    DiagMistCapBlocked(mist);
                    continue;
                }

                // 상한 아래 — 회복 횟수를 1 올리고 이번 프레임에 회복을 발화한다.
                //   (진단 칸 DiagTicks와 별개인 「판정용」 칸이다 — 릴리스 빌드에서도 동작해야 하므로.)
                mist.HealTickCount++;
                mist.FiredThisTick = true;
                anyFired = true;

                // 회복 텍스트는 회복 틱과 별도 주기다(규칙 15 / UI 규칙 9).
                //   TextAccum은 "회복이 실제로 일어난 시간"만 쌓아 주기를 센다.
                mist.TextAccum += HealTickInterval;
                if (mist.TextAccum >= _textInterval)
                {
                    mist.TextAccum -= _textInterval;
                    mist.ShowTextThisTick = true;
                }
                else
                {
                    mist.ShowTextThisTick = false;
                }

                // [진단 MIST-DIAG] 이 물안개의 발화 1회 기록(Ticks · 발화 간격 · 텍스트 수 — 줄 없음, MistEnded에 합계).
                DiagMistFired(mist);
            }

            // ── 2) 회복 적용(중첩 해소 포함) ─────────────────────────────────────
            if (anyFired)
            {
                ApplyHealTick();
            }

            // ── 3) 지속시간이 끝난 물안개 제거(뒤에서부터 지워 인덱스가 밀리지 않게 한다) ──
            // [진단 MIST-DIAG] 아래 반복이 지울 물안개(Remaining ≤ 0)마다 MistEnded Reason=Expire 1줄을 먼저 남긴다.
            DiagEndExpiredMists();
            for (int i = _activeMists.Count - 1; i >= 0; i--)
            {
                if (_activeMists[i].Remaining <= 0f)
                    _activeMists.RemoveAt(i);
            }
        }

        /// <summary>
        /// 이번 틱의 회복을 대상별로 1회씩만 적용한다(중첩 해소 — 규칙 13). 유닛·건물 모두 같은 규칙을 쓴다.
        ///
        /// 절차:
        ///   ① 시전 건물 Id 오름차순으로 **지금 깔려 있는 물안개 전체**를 순회한다
        ///      (거리 동률 시 Id 작은 쪽이 이기도록 — 규칙 13).
        ///   ② 각 물안개의 범위 안 아군 유닛·아군 건물을 수집하며 중심까지의 거리제곱을 함께 얻는다.
        ///   ③ 대상별로 "가장 가까운 물안개"만 남긴다(엄격 부등호 &lt; 이므로 동률이면 먼저 온 쪽 유지).
        ///   ④ 남은 조합 중 **그 물안개가 이번 틱에 발화한 경우에만** 회복을 적용한다.
        ///
        /// ①에서 "발화한 물안개"가 아니라 "활성 물안개 전체"를 도는 것이 이 메서드의 핵심이다.
        /// 발화한 것들끼리만 비교하면 "먼저 틱한 물안개가 이긴다"가 되어 규칙 13의 결정성이 깨지고,
        /// 실제로 2026-08-12 이전에는 프레임마다 발화 물안개가 1개뿐이라 중첩 해소가 아예 돌지 못했다.
        /// 위상 불변식(TickMists 주석)이 지켜지면 "활성 = 발화"라 ④의 게이트는 항상 통과하며,
        /// 혹시 위상이 어긋나더라도 이중 회복 대신 "이번 틱 건너뜀"으로 안전하게 떨어진다.
        /// </summary>
        private void ApplyHealTick()
        {
            if (_healPerTick <= 0) return;

            _bestUnitByTarget.Clear();
            _bestBuildingByTarget.Clear();

            // ① 결정적 순회 순서 확보(거리 동률 시 Id 작은 물안개가 이기도록).
            _activeMists.Sort(MistIdAscending);

            for (int m = 0; m < _activeMists.Count; m++)
            {
                ActiveMist mist = _activeMists[m];

                // 회복 횟수 상한에 닿았고 이번 프레임에 회복하지 않은 물안개는 소유자 후보에서 뺀다(2026-11-25 — Plan 2절 A-4).
                //   이런 물안개는 더 이상 아무도 회복시키지 않으므로 사실상 「이미 걷힌 물안개」다. 후보로 남겨 두면,
                //   그 물안개가 겹친 대상의 「가장 가까운 물안개」가 되어 같은 대상을 덮고 있는 다른 물안개의 회복까지 막는다
                //   (그 대상 입장에서 회복 간격이 한 번 2초가 된다 — 규칙 8-1-a 「대상 입장의 회복 간격 1초 유지」 위반).
                //   ⚠️ 기준을 「남은 시간 ≤ 0」으로 잡으면 안 된다: 위상 0으로 시작한 물안개는 10번째 회복이 정확히
                //   소멸 프레임에 오므로, 그 프레임에 후보에서 빼면 10번째 회복을 잃는다.
                //   그래서 「상한 도달 **그리고** 이번 프레임 미발화」 두 조건을 함께 본다 — 이번 프레임에 10번째를 발화한 물안개는 후보로 남는다.
                if (mist.HealTickCount >= _healTickCap && !mist.FiredThisTick) continue;

                // ② 아군 유닛 수집.
                CollectAllyUnitsInRadius(mist.Team, mist.CenterWorld, _unitBuffer, _unitDistBuffer);
                for (int i = 0; i < _unitBuffer.Count; i++)
                {
                    UnitData unit = _unitBuffer[i];
                    float d2 = _unitDistBuffer[i];

                    // ③ 더 가까운 물안개만 남긴다. 동률(<가 거짓)이면 먼저 온(Id 작은) 물안개가 유지된다.
                    if (_bestUnitByTarget.TryGetValue(unit.Id, out PickedUnit prev) && d2 >= prev.DistSqr)
                        continue;
                    _bestUnitByTarget[unit.Id] = new PickedUnit(unit, d2, mist);
                }

                // ② 아군 건물 수집(시전 건물 자신·Castle도 그대로 포함 — 규칙 4).
                CollectAllyBuildingsInRadius(mist.Team, mist.CenterWorld, _buildingBuffer, _buildingDistBuffer);
                for (int i = 0; i < _buildingBuffer.Count; i++)
                {
                    BuildingData building = _buildingBuffer[i];
                    float d2 = _buildingDistBuffer[i];
                    if (_bestBuildingByTarget.TryGetValue(building.Id, out PickedBuilding prev) && d2 >= prev.DistSqr)
                        continue;
                    _bestBuildingByTarget[building.Id] = new PickedBuilding(building, d2, mist);
                }
            }

            // ④ 실제 회복 적용. 대상마다 정확히 1회만 호출된다.
            //    소유 물안개가 이번 틱에 발화하지 않았다면 이번 틱은 건너뛴다(다음 발화 때 회복된다).
            foreach (var kv in _bestUnitByTarget)
            {
                PickedUnit pick = kv.Value;
                ActiveMist source = pick.Source;
                if (!source.FiredThisTick) continue;
                ApplyHealToUnitEntity(pick.Unit, source.SourceBuildingId, source.ShowTextThisTick);
            }
            foreach (var kv in _bestBuildingByTarget)
            {
                PickedBuilding pick = kv.Value;
                ActiveMist source = pick.Source;
                if (!source.FiredThisTick) continue;
                ApplyHealToBuildingEntity(pick.Building, source.SourceBuildingId, source.ShowTextThisTick);
            }

            _bestUnitByTarget.Clear();
            _bestBuildingByTarget.Clear();
        }

        // ====================================================================
        // 회복 적용
        // ====================================================================

        /// <summary>
        /// 아군 유닛 1명을 회복시키고 회복 이벤트를 발행한다.
        ///
        /// UnitCombatUseCase.ApplyHealToUnit을 재사용하지 못하는 이유:
        ///   그 메서드는 private인 데다 힐러가 "유닛"이라고 하드코딩되어 있다(healerIsUnit: true).
        ///   물안개의 시전자는 건물이므로 healerIsUnit: false로 실어 보내야 한다.
        /// </summary>
        /// <param name="target">회복시킬 아군 유닛.</param>
        /// <param name="shrineId">물안개를 만든 MistShrine 건물 Id(연출이 시전자를 알 수 있게 실어 보낸다).</param>
        /// <param name="showText">부유 회복 텍스트를 띄울지 여부(표시 주기가 찬 틱에서만 true).</param>
        private void ApplyHealToUnitEntity(UnitData target, int shrineId, bool showText)
        {
            if (target == null || !target.IsAlive) return;

            int before = target.Hp;
            // [2026-11-17 단계 B — 회복 지점 ②] 받는 회복 배율(재생의 뿌리)을 거친 양으로 회복한다(효과 없으면 _healPerTick 그대로).
            target.Heal(AmplifiedMistHeal(target));
            // 실제로 회복이 일어난 대상만 이벤트를 발행한다(규칙 5·15 — 풀피 대상은 텍스트도 없다).
            if (target.Hp == before) return;

            // [진단 MIST-DIAG] 실제로 오른 회복 1회 집계 + 물안개당 대상별 첫 회복이면 HealSample 1줄(상한 있음).
            DiagHealApplied(target, true, shrineId, before);

            // showText=false여도 이벤트는 발행한다 — NetworkHealthSync가 이 이벤트로 HP를 클라에 동기화하기 때문.
            //   (FloatingHpTextSpawner만 ShowText 플래그를 보고 텍스트 표시를 건너뛴다.)
            GameEvents.OnEntityHealed.OnNext(
                new EntityHealedEvent(target, target.Hp, isUnit: true,
                    healerId: shrineId, healerIsUnit: false,
                    showText: showText));
        }

        /// <summary>
        /// 아군 건물 1채를 회복시키고 회복 이벤트를 발행한다(프로젝트 최초의 건물 회복 경로 — 규칙 24).
        /// </summary>
        /// <param name="target">회복시킬 아군 건물(시전 건물 자신·Castle 포함).</param>
        /// <param name="shrineId">물안개를 만든 MistShrine 건물 Id.</param>
        /// <param name="showText">부유 회복 텍스트를 띄울지 여부.</param>
        private void ApplyHealToBuildingEntity(BuildingData target, int shrineId, bool showText)
        {
            if (target == null || !target.IsAlive) return;

            int before = target.Hp;
            // [2026-11-17 단계 B — 회복 지점 ③] 건물도 받는 회복 배율을 거친다(Plan S-3 「건물 포함」).
            target.Heal(AmplifiedMistHeal(target));
            if (target.Hp == before) return;

            // [진단 MIST-DIAG] 건물 회복 1회 집계 + 물안개당 건물별 첫 회복이면 HealSample Target=Building 1줄.
            DiagHealApplied(target, false, shrineId, before);

            GameEvents.OnEntityHealed.OnNext(
                new EntityHealedEvent(target, target.Hp, isUnit: false,
                    healerId: shrineId, healerIsUnit: false,
                    showText: showText));
        }

        // ====================================================================
        // 아군 대상 수집 헬퍼(규칙 23 — 기존 헬퍼는 전부 "적 전용"이라 신규 작성)
        // ====================================================================

        /// <summary>
        /// 물안개 중심에서 반경 안에 있는 아군 유닛을 수집한다(도메인 월드 좌표 기준).
        /// UnitCombatUseCase.CollectEnemyUnitsInRadiusDomain과 알고리즘은 같고 팀 조건만 반대다.
        /// 이미 체력이 가득 찬 유닛은 미리 걸러 낸다(규칙 5 — 회복도 텍스트도 없음).
        /// </summary>
        /// <param name="team">물안개를 만든 팀(이 팀의 유닛만 대상).</param>
        /// <param name="centerWorld">물안개 중심(도메인 월드, Y=0으로 평탄화된 값).</param>
        /// <param name="result">수집 결과를 담을 버퍼(호출 전 자동으로 비운다).</param>
        /// <param name="distSqrResult">각 대상의 중심까지 거리제곱을 담을 버퍼(result와 같은 인덱스).</param>
        private void CollectAllyUnitsInRadius(TeamId team, Vector3 centerWorld,
            List<UnitData> result, List<float> distSqrResult)
        {
            result.Clear();
            distSqrResult.Clear();
            if (_unitSpawn == null || _mapper == null) return;

            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit == null || !unit.IsAlive) continue;
                if (unit.Team != team) continue;          // 아군만(적 헬퍼와 정반대 조건).
                if (unit.Hp >= unit.MaxHp) continue;      // 풀피는 회복 대상이 아니다(규칙 5).

                Vector3 rel = Flatten(_mapper.HexToWorld(unit.Position)) - centerWorld;
                float d2 = rel.sqrMagnitude;
                if (d2 > _radiusSqr) continue;

                result.Add(unit);
                distSqrResult.Add(d2);
            }
        }

        /// <summary>
        /// 물안개 중심에서 반경 안에 있는 아군 건물을 수집한다(도메인 월드 좌표 기준).
        ///
        /// ⚠️ 시전 건물 자신과 본기지(Castle)를 **일부러 제외하지 않는다** — 규칙 4가 둘 다 회복 대상으로 정한다.
        /// 이미 체력이 가득 찬 건물은 미리 걸러 낸다(규칙 5).
        /// </summary>
        /// <param name="team">물안개를 만든 팀(이 팀의 건물만 대상).</param>
        /// <param name="centerWorld">물안개 중심(도메인 월드, Y=0으로 평탄화된 값).</param>
        /// <param name="result">수집 결과를 담을 버퍼(호출 전 자동으로 비운다).</param>
        /// <param name="distSqrResult">각 대상의 중심까지 거리제곱을 담을 버퍼(result와 같은 인덱스).</param>
        private void CollectAllyBuildingsInRadius(TeamId team, Vector3 centerWorld,
            List<BuildingData> result, List<float> distSqrResult)
        {
            result.Clear();
            distSqrResult.Clear();
            if (_buildingPlacement == null || _mapper == null) return;

            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building == null || !building.IsAlive) continue;
                if (building.Team != team) continue;                  // 아군만.
                if (building.Hp >= building.MaxHp) continue;          // 풀피는 회복 대상이 아니다(규칙 5).

                Vector3 rel = Flatten(_mapper.HexToWorld(building.Position)) - centerWorld;
                float d2 = rel.sqrMagnitude;
                if (d2 > _radiusSqr) continue;

                result.Add(building);
                distSqrResult.Add(d2);
            }
        }

        // ====================================================================
        // 상태 조회 / 정리
        // ====================================================================

        /// <summary>
        /// 지정 MistShrine이 만든 물안개가 지금 깔려 있는지 여부(규칙 6).
        /// UI는 이 값을 쓰지 않는다(UI 규칙 13 — 물안개 지속 표시를 따로 두지 않음). 디버그·AI용 조회다.
        /// </summary>
        /// <param name="buildingId">조회할 MistShrine 건물 Id.</param>
        public bool IsMistActive(int buildingId)
        {
            for (int i = 0; i < _activeMists.Count; i++)
            {
                if (_activeMists[i].SourceBuildingId == buildingId) return true;
            }
            return false;
        }

        /// <summary>
        /// MistShrine이 파괴·철거되었을 때의 정리(규칙 12·25).
        /// ① 그 건물이 만든 물안개를 즉시 제거 ② 자동 모드 제거 ③ 쿨다운 제거.
        /// </summary>
        /// <param name="buildingId">파괴·철거된 MistShrine 건물 Id.</param>
        public void OnShrineDestroyed(int buildingId)
        {
            // [진단 MIST-DIAG] 신전이 사라져 지워지는 물안개가 있으면 MistEnded Reason=ShrineDestroyed 1줄.
            DiagEndMistsOfShrine(buildingId, "ShrineDestroyed");
            RemoveMistsOfShrine(buildingId);
            _autoMode.Remove(buildingId);
            _cooldownRemaining.Remove(buildingId);
            _cooldownTotal.Remove(buildingId);
        }

        /// <summary> 재경기·맵 전환 시 모든 물안개·자동 모드·쿨다운 상태를 초기화한다. </summary>
        public void ClearAll()
        {
            // [진단 MIST-DIAG] 남은 물안개를 MistEnded Reason=MapClear로 닫고, 진단 상태(순번 · 시계 · 경기 종료 깃발)를 비운다.
            DiagClearAll();
            _activeMists.Clear();
            _autoMode.Clear();
            _cooldownRemaining.Clear();
            _cooldownTotal.Clear();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 지금 깔려 있는 물안개들이 공유하는 회복 틱 위상(TickAccum 값)을 돌려준다. 깔린 물안개가 없으면 0.
        ///
        /// 왜 아무 물안개나 하나만 봐도 되나:
        ///   TickMists가 모든 활성 물안개에 **같은 dt**를 더하고 같은 프레임에 똑같이 1초를 빼며,
        ///   새 물안개도 이 값으로 출발하기 때문에 활성 물안개들의 TickAccum은 항상 서로 같다(위상 불변식).
        ///   float 연산도 "같은 값 + 같은 값"이라 결과가 완전히 동일하므로 시간이 지나도 벌어지지 않는다.
        /// </summary>
        private float GetSharedTickPhase()
        {
            return _activeMists.Count > 0 ? _activeMists[0].TickAccum : 0f;
        }

        /// <summary> 지정 건물이 만든 물안개를 전부 제거한다(뒤에서부터 지워 인덱스가 밀리지 않게 한다). </summary>
        private void RemoveMistsOfShrine(int buildingId)
        {
            for (int i = _activeMists.Count - 1; i >= 0; i--)
            {
                if (_activeMists[i].SourceBuildingId == buildingId)
                    _activeMists.RemoveAt(i);
            }
        }

        /// <summary>
        /// Y축(높이) 성분을 0으로 눌러 XZ 평면 좌표로 만든다.
        /// 이 프로젝트는 Y=0이 바닥인 XZ 평면 좌표계라, 거리 판정은 항상 평면에서 한다.
        /// </summary>
        private static Vector3 Flatten(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        // ====================================================================
        // [진단 MIST-DIAG] MistShrine 멀티 검증용 개발 진단 로그 (Diag=MistShrine, DiagRev=2 — 개발 축)
        //   2026-11-24 task `_Tasks/2026-11-24/11_28_mistshrine-multi-diag` Plan 2절 로그 지점 표.
        //
        //   남기는 줄(상태가 바뀔 때 · 물안개당 · 경기당만 — 매 틱 · 매 회복 줄은 없다, LogRules 1.14):
        //     CastResult      시전 호출 1회당 1줄. Origin=Manual|Auto, Result=Ok|RejectClientGuard|RejectMissing|RejectType|RejectCooldown.
        //     CooldownExpired 쿨다운 만료 1회당 1줄. CdMs(총 쿨다운) · ElapsedMs(시작 이후 이 기기가 받은 dt 합).
        //     HealSample      물안개당 대상별 「첫 회복」 1줄(물안개당 상한 16 — 넘으면 SamplesDropped로만 센다).
        //     DoubleTick      같은 프레임에 TickMists 가드를 두 번 통과한 첫 1회(Warn). 정상이면 0줄이다.
        //     MistEnded       물안개 1개가 끝날 때 1줄. Reason=Expire|Replaced|ShrineDestroyed|MapClear|GameEnd.
        //                     2026-11-25(DiagRev=2): TickCap(회복 횟수 상한) · TickCapBlocked(상한 때문에 막은 발화 수) 키 추가.
        //                     정상 판정: Reason=Expire 줄은 Ticks=TickCap(지금 10). 막힌 발화는 Ticks에 세지 않는다.
        //
        //   DiagRev(진단 세대 번호): 1 → 2 (2026-11-25 회복 횟수 상한 수정 — Task `_Tasks/2026-11-25/01_19_mistshrine-heal-tick-cap`).
        //     같은 [MIST-DIAG] 줄을 쓰는 4개 파일(이 파일 · NetworkMistShrineController · NetworkHealthSync · MistShrinePanelUI)이
        //     함께 올린다 — 한 경기 로그에서 Host · Client 어느 쪽 줄로도 「수정 뒤 빌드인지」를 가를 수 있게.
        //
        //   지키는 것:
        //     · 모든 진입 메서드에 [Conditional] 두 개 → 릴리스 빌드에서는 호출 · 인자 계산 · 문자열 조립이 통째로 사라진다(LogRules 1.7).
        //     · 판정 값(남은 시간 · 누적기 · 쿨다운 사전 · HP · 배율 나머지)을 바꾸지 않는다 — 읽기와 진단 전용 칸만 쓴다.
        //       (받는 회복 배율은 부작용이 있는 AmplifyHeal을 다시 부르지 않고, 읽기 전용 GetHealAmpMultiplier만 쓴다.)
        //     · 예외를 밖으로 던지지 않는다 — 서버 회복 경로 안에서 돌기 때문에 각 메서드가 삼킨다.
        //     · 경기 종료(DiagGameEnd) 뒤에는 줄을 더 남기지 않는다(싱글은 결과 화면 뒤에서도 틱이 계속 돈다).
        //     · 시간은 모두 「이 기기 기준 상대 ms」다(서버 시각 필드 없음). 기기 사이 대조는 순번 · 개수 · 경과 ms로 한다.
        // ====================================================================

        /// <summary>물안개 하나가 남길 HealSample 줄 상한.</summary>
        private const int DiagHealSampleCap = 16;

        /// <summary>같은 대상이 이 시간(초, 물안개 진단 시계) 안에 다시 회복되면 「중복 회복」으로 센다. 정상 주기는 1초다.</summary>
        private const double DiagDupHealWindow = 0.5;

        // 진단 전용 상태 — 전부 지연 생성(릴리스에서는 null · 0 그대로 남는다). 판정 코드는 이 필드들을 읽지 않는다.
        private Dictionary<int, int> _diagCastCount;         // 신전 Id → 성공 시전 수(= 마지막 CastNo).
        private Dictionary<int, double> _diagCdStartClock;   // 신전 Id → 쿨다운 시작 때의 쿨다운 진단 시계 값(초).
        private Dictionary<long, double> _diagLastHealClock; // 대상 키 → 마지막 물안개 회복 때의 물안개 진단 시계 값(초).
        private double _diagCdClock;                         // TickCooldowns가 받은 dt 합(초).
        private double _diagMistClock;                       // TickMists가 받은 dt 합(초).
        private bool _diagAutoOrigin;                        // true = 지금 Activate 호출은 TickAutoCast에서 왔다.
        private int _diagLastTickFrame = -1;                 // TickMists 가드를 마지막으로 통과한 프레임 번호.
        private int _diagTickCallsInFrame;                   // 그 프레임에서 가드를 통과한 횟수.
        private int _diagDoubleTickFrames;                   // 가드를 2회 이상 통과한 프레임 수(정상이면 0).
        private bool _diagGameEnded;                         // 경기 종료 정리 뒤에는 줄을 남기지 않는다.

        /// <summary>
        /// [진단 전용] 신전별 성공 시전 수(= 가장 최근 CastNo)를 돌려준다. 개발 빌드가 아니면 늘 0이다.
        /// 네트워크 컨트롤러가 ActivateSent 줄에 CastResult와 같은 CastNo를 싣기 위해 읽는다 — 게임 판정에는 쓰지 않는다.
        /// </summary>
        /// <param name="buildingId">MistShrine 건물 Id.</param>
        public int GetDiagCastCount(int buildingId)
        {
            return _diagCastCount != null && _diagCastCount.TryGetValue(buildingId, out int n) ? n : 0;
        }

        /// <summary>
        /// [진단 전용] 경기 종료 정리 — 아직 깔려 있는 물안개마다 MistEnded Reason=GameEnd 1줄을 쓰고, 이후 진단 줄을 멈춘다.
        /// 멀티 Host는 경기가 끝나면 전투 틱이 멈춰 물안개가 「끝나지 않은 채」 남기 때문에, 요약을 여기서 한 번 닫아 쓴다.
        /// GameBootstrapper의 OnGameEnd 구독(블록 본문 람다)이 부른다. 개발 빌드가 아니면 호출째 사라진다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void DiagGameEnd()
        {
            if (_diagGameEnded) return;
            for (int i = 0; i < _activeMists.Count; i++)
                DiagWriteMistEnded(_activeMists[i], "GameEnd");
            _diagGameEnded = true;
        }

        // 진단 줄 Role= 값(Single · Host · Client). 다른 진단(SkillVfx · SkillRegen)과 같은 표기.
        private static string DiagMistRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        // 초 → 정수 ms(로그 값에 소수점 · 문화권 차이가 끼지 않게).
        private static long DiagMs(double seconds)
        {
            return (long)Math.Round(seconds * 1000.0, MidpointRounding.AwayFromZero);
        }

        // 「값 없음」(음수)은 -1로, 그 밖에는 ms로.
        private static long DiagMsOrNone(double seconds)
        {
            return seconds < 0 ? -1 : DiagMs(seconds);
        }

        private string DiagOrigin()
        {
            return _diagAutoOrigin ? "Auto" : "Manual";
        }

        // 신전 Id로 지금 깔려 있는 물안개를 찾는다(신전마다 최대 1개 — Activate ④가 보장). 없으면 null.
        private ActiveMist DiagFindMist(int shrineId)
        {
            for (int i = 0; i < _activeMists.Count; i++)
            {
                if (_activeMists[i].SourceBuildingId == shrineId) return _activeMists[i];
            }
            return null;
        }

        /// <summary>
        /// Activate의 재검증 ①~③과 「같은 조건을 같은 순서로 읽기만」 해서, 막히는 경우 그 사유를 CastResult Reject* 1줄로 남긴다.
        /// ⚠️ Activate의 가드를 바꾸면 이 메서드도 함께 맞춰야 한다(진단 줄이 실제 사유와 달라지지 않게).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagCastPrecheck(int buildingId)
        {
            if (_diagGameEnded) return;
            try
            {
                string reject = null;
                float cdRemain = 0f;
                if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
                {
                    reject = "RejectClientGuard"; // 순수 Client에서 직접 경로로 불렸다(컨트롤러 배선 누락 신호).
                }
                else
                {
                    BuildingData b = _buildingPlacement?.GetBuilding(buildingId);
                    if (b == null || !b.IsAlive) reject = "RejectMissing";
                    else if (b.Type != BuildingType.HealShrine) reject = "RejectType";
                    else
                    {
                        cdRemain = GetCooldownRemaining(buildingId);
                        if (cdRemain > 0f) reject = "RejectCooldown";
                    }
                }
                if (reject == null) return; // 통과 — 성공 줄은 DiagCastOk가 남긴다.

                GameLog.Dev.Warn("Combat", nameof(MistShrineUseCase), "[MIST-DIAG] CastResult",
                    FormattableString.Invariant(
                        $"Diag=MistShrine, DiagRev=2, Event=CastResult, Role={DiagMistRole()}, BuildingId={buildingId}, Origin={DiagOrigin()}, Result={reject}, CastNo={GetDiagCastCount(buildingId)}, CdRemainMs={DiagMs(cdRemain)}"));
            }
            catch (Exception)
            {
                // 진단 실패가 시전을 막으면 안 된다 — 삼킨다.
            }
        }

        /// <summary> 시전 성공 1줄(CastResult Result=Ok) + 새 물안개의 진단 칸 초기화. CastNo는 신전별 1, 2, … 순번이다. </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagCastOk(int buildingId)
        {
            try
            {
                if (_diagCastCount == null) _diagCastCount = new Dictionary<int, int>();
                _diagCastCount.TryGetValue(buildingId, out int castNo);
                castNo++;
                _diagCastCount[buildingId] = castNo;

                ActiveMist mist = DiagFindMist(buildingId);
                if (mist != null)
                {
                    mist.DiagCastNo = castNo;
                    mist.DiagStartWall = UnityEngine.Time.timeAsDouble;
                }
                if (_diagGameEnded) return;

                BuildingData b = _buildingPlacement?.GetBuilding(buildingId);
                string team = b != null ? b.Team.ToString() : "None";
                double phase = mist != null ? mist.TickAccum : 0.0;
                GameLog.Dev.Info("Combat", nameof(MistShrineUseCase), "[MIST-DIAG] CastResult",
                    FormattableString.Invariant(
                        $"Diag=MistShrine, DiagRev=2, Event=CastResult, Role={DiagMistRole()}, BuildingId={buildingId}, Team={team}, Origin={DiagOrigin()}, Result=Ok, CastNo={castNo}, CdMs={DiagMs(_cooldown)}, DurationMs={DiagMs(_mistDuration)}, HealPerTick={_healPerTick}, ActiveMists={_activeMists.Count}, PhaseMs={DiagMs(phase)}"));
            }
            catch (Exception)
            {
            }
        }

        // TickAutoCast가 자동 시전 반복 앞뒤로 세우고 내린다(CastResult Origin= 값).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagSetAutoOrigin(bool on)
        {
            _diagAutoOrigin = on;
        }

        // 쿨다운 시작(서버 시전 · Client 미러 공통) — 그 순간의 쿨다운 진단 시계를 적어 둔다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagCooldownStarted(int buildingId)
        {
            if (_diagCdStartClock == null) _diagCdStartClock = new Dictionary<int, double>();
            _diagCdStartClock[buildingId] = _diagCdClock;
        }

        // 쿨다운 진단 시계 진행(TickCooldowns가 실제로 감소를 할 때만 불린다 → 쿨다운이 있는 동안의 dt는 빠짐없이 쌓인다).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagCooldownAdvance(float dt)
        {
            _diagCdClock += dt;
        }

        /// <summary> 쿨다운 만료 1줄. ElapsedMs ≈ CdMs이면 이 기기의 감소 속도가 정상이다(Host는 전투 틱 간격만큼 늦을 수 있다). </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagCooldownExpired(int buildingId)
        {
            try
            {
                double start = 0.0;
                bool known = _diagCdStartClock != null && _diagCdStartClock.TryGetValue(buildingId, out start);
                if (known) _diagCdStartClock.Remove(buildingId);
                if (_diagGameEnded) return;

                long elapsedMs = known ? DiagMs(_diagCdClock - start) : -1;
                GameLog.Dev.Info("Combat", nameof(MistShrineUseCase), "[MIST-DIAG] CooldownExpired",
                    FormattableString.Invariant(
                        $"Diag=MistShrine, DiagRev=2, Event=CooldownExpired, Role={DiagMistRole()}, BuildingId={buildingId}, CdMs={DiagMs(GetCooldownTotal(buildingId))}, ElapsedMs={elapsedMs}"));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// TickMists 가드를 통과한 호출 1회 기록. 같은 프레임에 두 번째로 통과하면 이중 틱이다 → 첫 1회만 DoubleTick Warn.
        /// 물안개마다 받은 dt 합(SumDt)도 여기서 쌓는다(판정 필드 Remaining과 별개의 진단 칸).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagTickEnter(float dt)
        {
            try
            {
                int frame = UnityEngine.Time.frameCount;
                if (frame == _diagLastTickFrame)
                {
                    _diagTickCallsInFrame++;
                    if (_diagTickCallsInFrame == 2)
                    {
                        _diagDoubleTickFrames++;
                        if (_diagDoubleTickFrames == 1 && !_diagGameEnded)
                        {
                            GameLog.Dev.Warn("Combat", nameof(MistShrineUseCase), "[MIST-DIAG] DoubleTick",
                                FormattableString.Invariant(
                                    $"Diag=MistShrine, DiagRev=2, Event=DoubleTick, Role={DiagMistRole()}, Frame={frame}, Calls={_diagTickCallsInFrame}, ActiveMists={_activeMists.Count}, DtMs={DiagMs(dt)}"));
                        }
                    }
                }
                else
                {
                    _diagLastTickFrame = frame;
                    _diagTickCallsInFrame = 1;
                }

                _diagMistClock += dt;
                for (int i = 0; i < _activeMists.Count; i++)
                    _activeMists[i].DiagSumDt += dt;
            }
            catch (Exception)
            {
            }
        }

        // 물안개 발화 1회 기록(줄 없음). 발화 간격은 이 기기의 게임 시계(Time.timeAsDouble) 차이다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagMistFired(ActiveMist mist)
        {
            try
            {
                double now = UnityEngine.Time.timeAsDouble;
                mist.DiagTicks++;
                if (mist.DiagFirstFireSumDt < 0) mist.DiagFirstFireSumDt = mist.DiagSumDt;
                if (mist.DiagLastFireWall >= 0)
                {
                    double gap = now - mist.DiagLastFireWall;
                    if (mist.DiagMinGap < 0 || gap < mist.DiagMinGap) mist.DiagMinGap = gap;
                    if (gap > mist.DiagMaxGap) mist.DiagMaxGap = gap;
                }
                mist.DiagLastFireWall = now;
                if (mist.ShowTextThisTick) mist.DiagTexts++;
            }
            catch (Exception)
            {
            }
        }

        // 회복 횟수 상한 때문에 막은 발화 1회 기록(줄 없음 — MistEnded의 TickCapBlocked). 판정 칸(HealTickCount 등)은 건드리지 않는다.
        //   정상 동작에서 이 값이 1이 되는 것은 「위상이 큰(첫 회복이 시작 직후에 온) 물안개의 마지막 프레임」뿐이다(Research 5절).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagMistCapBlocked(ActiveMist mist)
        {
            if (mist == null) return;
            mist.DiagTickCapBlocked++;
        }

        /// <summary>
        /// 실제로 HP가 오른 물안개 회복 1회를 그 물안개에 집계하고, 물안개당 대상별 첫 회복이면 HealSample 1줄을 남긴다.
        /// Amplified(Heal에 넘긴 양)는 배율이 1이면 Base 그대로, 배율이 있으면 상한에 닿지 않은 경우 Gained와 정확히 같다
        /// (Heal은 「더한 뒤 최대 체력으로 자른다」뿐이다). 상한에 닿아 알 수 없으면 -1 · Check=Capped.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagHealApplied(IDamageable target, bool isUnit, int shrineId, int hpBefore)
        {
            if (_diagGameEnded || target == null) return;
            try
            {
                int hpAfter = target.Hp;
                int gained = hpAfter - hpBefore;
                long key = ((isUnit ? 1L : 2L) << 32) | (uint)target.Id;

                // 중복 회복: 같은 대상이 0.5초(물안개 진단 시계) 안에 다시 회복됐다 — 2배 회복(R3)의 직접 신호다.
                if (_diagLastHealClock == null) _diagLastHealClock = new Dictionary<long, double>();
                bool dup = _diagLastHealClock.TryGetValue(key, out double last)
                           && _diagMistClock - last < DiagDupHealWindow;
                _diagLastHealClock[key] = _diagMistClock;

                ActiveMist mist = DiagFindMist(shrineId);
                if (mist == null) return; // 물안개 밖 호출(검사 코드의 직접 호출 등) — 집계할 물안개가 없다.

                if (isUnit) mist.DiagUnitHeals++;
                else mist.DiagBuildingHeals++;
                mist.DiagBaseTotal += _healPerTick;
                mist.DiagGainedTotal += gained;
                if (dup) mist.DiagDupTargetHeals++;

                if (mist.DiagSampled == null) mist.DiagSampled = new HashSet<long>();
                if (!mist.DiagSampled.Add(key)) return;             // 이 대상은 이미 표본을 남겼다.
                if (mist.DiagSamples >= DiagHealSampleCap) { mist.DiagSamplesDropped++; return; }
                mist.DiagSamples++;

                float mul = _supportEffects != null ? _supportEffects.GetHealAmpMultiplier(target.Id, isUnit) : 1f;
                int amplified = mul == 1f ? _healPerTick : (hpAfter < target.MaxHp ? gained : -1);
                string check = amplified < 0 ? "Capped"
                    : (gained == Math.Min(amplified, target.MaxHp - hpBefore) ? "Match" : "Mismatch");
                string type = target is UnitData u ? u.Type.ToString()
                    : target is BuildingData bd ? bd.Type.ToString() : "Unknown";

                string data = FormattableString.Invariant(
                    $"Diag=MistShrine, DiagRev=2, Event=HealSample, Role={DiagMistRole()}, ShrineId={shrineId}, CastNo={mist.DiagCastNo}, Target={(isUnit ? "Unit" : "Building")}, Id={target.Id}, Type={type}, Base={_healPerTick}, AmpMulMilli={Mathf.RoundToInt(mul * 1000f)}, Amplified={amplified}, Gained={gained}, HpBefore={hpBefore}, HpAfter={hpAfter}, MaxHp={target.MaxHp}, Tick={mist.DiagTicks}, Dup={dup}, Check={check}");
                if (check == "Mismatch" || dup)
                    GameLog.Dev.Warn("Combat", nameof(MistShrineUseCase), "[MIST-DIAG] HealSample", data);
                else
                    GameLog.Dev.Info("Combat", nameof(MistShrineUseCase), "[MIST-DIAG] HealSample", data);
            }
            catch (Exception)
            {
            }
        }

        // 지속시간이 끝나 이번 TickMists 3)에서 지워질 물안개마다 MistEnded Reason=Expire.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagEndExpiredMists()
        {
            for (int i = 0; i < _activeMists.Count; i++)
            {
                if (_activeMists[i].Remaining <= 0f) DiagWriteMistEnded(_activeMists[i], "Expire");
            }
        }

        // 지정 신전의 물안개가 지워지기 직전(재시전 · 신전 파괴)에 MistEnded 1줄.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagEndMistsOfShrine(int buildingId, string reason)
        {
            for (int i = 0; i < _activeMists.Count; i++)
            {
                if (_activeMists[i].SourceBuildingId == buildingId) DiagWriteMistEnded(_activeMists[i], reason);
            }
        }

        // 맵 정리: 남은 물안개를 MapClear로 닫고 진단 상태를 처음으로 되돌린다(다음 경기의 순번은 1부터).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagClearAll()
        {
            for (int i = 0; i < _activeMists.Count; i++)
                DiagWriteMistEnded(_activeMists[i], "MapClear");

            _diagCastCount?.Clear();
            _diagCdStartClock?.Clear();
            _diagLastHealClock?.Clear();
            _diagCdClock = 0.0;
            _diagMistClock = 0.0;
            _diagAutoOrigin = false;
            _diagLastTickFrame = -1;
            _diagTickCallsInFrame = 0;
            _diagDoubleTickFrames = 0;
            _diagGameEnded = false;
        }

        /// <summary>
        /// 물안개 1개의 요약 1줄(MistEnded). ③ 판정의 재료: Ticks · MinGapMs/MaxGapMs(≈1000) · SumDtMs ≈ WallMs · DupTargetHeals=0.
        /// ② 판정의 재료: BuildingHeals(= 이 물안개가 Host에서 일으킨 건물 회복 수). 물안개마다 한 번만 쓴다(DiagEnded).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagWriteMistEnded(ActiveMist mist, string reason)
        {
            if (mist == null || mist.DiagEnded || _diagGameEnded) return;
            mist.DiagEnded = true;
            try
            {
                double wall = mist.DiagStartWall >= 0 ? UnityEngine.Time.timeAsDouble - mist.DiagStartWall : -1.0;
                string data = FormattableString.Invariant(
                    $"Diag=MistShrine, DiagRev=2, Event=MistEnded, Role={DiagMistRole()}, ShrineId={mist.SourceBuildingId}, Team={mist.Team}, CastNo={mist.DiagCastNo}, Reason={reason}, DurationMs={DiagMs(_mistDuration)}, HealPerTick={_healPerTick}, Ticks={mist.DiagTicks}, TickCap={_healTickCap}, TickCapBlocked={mist.DiagTickCapBlocked}, FirstTickMs={DiagMsOrNone(mist.DiagFirstFireSumDt)}, MinGapMs={DiagMsOrNone(mist.DiagMinGap)}, MaxGapMs={DiagMsOrNone(mist.DiagMaxGap)}, SumDtMs={DiagMs(mist.DiagSumDt)}, WallMs={DiagMsOrNone(wall)}, UnitHeals={mist.DiagUnitHeals}, BuildingHeals={mist.DiagBuildingHeals}, BaseTotal={mist.DiagBaseTotal}, GainedTotal={mist.DiagGainedTotal}, Targets={mist.DiagSamples + mist.DiagSamplesDropped}, Texts={mist.DiagTexts}, DupTargetHeals={mist.DiagDupTargetHeals}, DoubleTickFrames={_diagDoubleTickFrames}, Samples={mist.DiagSamples}, SamplesDropped={mist.DiagSamplesDropped}");
                if (mist.DiagDupTargetHeals > 0 || _diagDoubleTickFrames > 0)
                    GameLog.Dev.Warn("Combat", nameof(MistShrineUseCase), "[MIST-DIAG] MistEnded", data);
                else
                    GameLog.Dev.Info("Combat", nameof(MistShrineUseCase), "[MIST-DIAG] MistEnded", data);
            }
            catch (Exception)
            {
            }
        }

        // ====================================================================
        // 내부 자료구조
        // ====================================================================

        /// <summary>
        /// 지금 깔려 있는 물안개 1개의 서버 상태(데이터 전용).
        /// 값을 계속 바꿔야 하므로 struct가 아니라 class다(리스트에 담은 채로 필드를 갱신하기 위함).
        /// </summary>
        private sealed class ActiveMist
        {
            /// <summary>이 물안개를 만든 MistShrine 건물 Id(중첩 해소 동률 판정 기준 — 규칙 13).</summary>
            public readonly int SourceBuildingId;

            /// <summary>물안개를 만든 팀. 이 팀의 유닛·건물만 회복 대상이다(규칙 4).</summary>
            public readonly TeamId Team;

            /// <summary>물안개 중심(도메인 월드, Y=0 평탄화). 생성 위치에 고정된다(규칙 10).</summary>
            public readonly Vector3 CenterWorld;

            /// <summary>남은 지속시간(초). 0 이하가 되면 물안개가 걷힌다.</summary>
            public float Remaining;

            /// <summary>
            /// 회복 틱(1초) 누적기. 이 물안개만의 독립 누적기다(규칙 8-1·14 — 누적기 개수 = 물안개 개수).
            ///
            /// ⚠️ 단, **시작 위상은 이미 깔려 있는 물안개와 맞춘다**(생성자 initialTickAccum).
            /// 누적기를 공유하는 것이 아니라 각자 갖되 위상만 같게 하는 것이며, 이래야 활성 물안개가
            /// 전부 같은 프레임에 발화해 중첩 해소(규칙 13)가 실제로 성립한다. 자세한 사유는 파일 상단 주석 참조.
            /// </summary>
            public float TickAccum;

            /// <summary>회복 텍스트 표시 주기 누적기. 회복 틱과 별개로 센다(규칙 15).</summary>
            public float TextAccum;

            /// <summary>이번 틱의 회복에 텍스트를 띄울지 여부(TickMists가 매 틱 갱신).</summary>
            public bool ShowTextThisTick;

            /// <summary>
            /// 이번 프레임에 1초 경계를 넘어 회복을 발화했는지 여부(TickMists가 매 프레임 갱신).
            /// 중첩 해소는 활성 물안개 전체를 놓고 하되, 실제 회복은 이 값이 true인 물안개에서만 나간다.
            /// </summary>
            public bool FiredThisTick;

            /// <summary>
            /// 이 물안개가 지금까지 회복을 발화한 횟수(**판정용** — 2026-11-25 회복 횟수 상한).
            /// TickMists가 상한(_healTickCap, 지금 10)과 비교해 그 이상이면 더 발화하지 않고,
            /// ApplyHealTick이 「상한 도달 + 이번 프레임 미발화」 물안개를 소유자 후보에서 뺄 때 읽는다.
            /// 아래 진단 칸 DiagTicks와 값이 같아 보이지만 별개다 — DiagTicks는 개발 빌드에서만 채워지고,
            /// 이 칸은 릴리스 빌드에서도 게임 규칙으로 동작해야 하기 때문이다.
            /// </summary>
            public int HealTickCount;

            // ── [진단 MIST-DIAG] 진단 전용 칸 — 판정(위 필드들)은 이 칸을 읽지 않는다. 개발 빌드에서만 값이 채워진다. ──
            public int DiagCastNo;              // 이 물안개를 만든 시전의 신전별 순번(CastResult · ActivateSent와 같은 값).
            public double DiagStartWall = -1;   // 생성 시각(Time.timeAsDouble, 초). -1 = 진단이 채우지 않음.
            public double DiagSumDt;            // 이 물안개가 TickMists에서 받은 dt 합(초).
            public double DiagFirstFireSumDt = -1; // 첫 발화 때까지 받은 dt 합(초). -1 = 아직 발화 안 함.
            public double DiagLastFireWall = -1;   // 직전 발화 시각(Time.timeAsDouble, 초).
            public double DiagMinGap = -1;      // 발화 간격 최소(초). -1 = 간격이 아직 없음(발화 2회 미만).
            public double DiagMaxGap = -1;      // 발화 간격 최대(초).
            public int DiagTicks;               // 발화 횟수(상한 때문에 막힌 발화는 세지 않는다 — 아래 DiagTickCapBlocked).
            public int DiagTickCapBlocked;      // 회복 횟수 상한 때문에 막은 발화 수(2026-11-25). DiagTicks + 이 값 = 수정 전이라면 나왔을 횟수.
            public int DiagTexts;               // 회복 텍스트 표시 틱 수.
            public int DiagUnitHeals;           // 실제로 HP가 오른 유닛 회복 횟수.
            public int DiagBuildingHeals;       // 실제로 HP가 오른 건물 회복 횟수.
            public long DiagBaseTotal;          // 회복 횟수 × 틱당 기본 회복량 합.
            public long DiagGainedTotal;        // 실제로 오른 HP 합.
            public int DiagDupTargetHeals;      // 같은 대상이 0.5초(진단 시계) 안에 다시 회복된 횟수 — 정상이면 0.
            public int DiagSamples;             // 남긴 HealSample 줄 수.
            public int DiagSamplesDropped;      // 상한 때문에 남기지 못한 HealSample 수.
            public HashSet<long> DiagSampled;   // HealSample을 이미 남긴 대상 키(지연 생성 — 릴리스에서는 null 그대로).
            public bool DiagEnded;              // MistEnded를 이미 남겼는지(경기 종료 정리 뒤 중복 방지).

            /// <param name="sourceBuildingId">이 물안개를 만든 MistShrine 건물 Id.</param>
            /// <param name="team">물안개를 만든 팀.</param>
            /// <param name="centerWorld">물안개 중심(도메인 월드, Y=0 평탄화).</param>
            /// <param name="duration">지속시간(초).</param>
            /// <param name="initialTickAccum">
            /// 회복 틱 누적기의 시작값. 이미 깔려 있는 물안개가 있으면 그 위상을 그대로 받아
            /// 같은 프레임에 함께 발화하게 한다. 깔린 물안개가 없으면 0(= 정확히 1초 뒤 첫 회복).
            /// </param>
            public ActiveMist(int sourceBuildingId, TeamId team, Vector3 centerWorld, float duration,
                float initialTickAccum)
            {
                SourceBuildingId = sourceBuildingId;
                Team = team;
                CenterWorld = centerWorld;
                Remaining = duration;
                TickAccum = initialTickAccum;
                TextAccum = 0f;
                ShowTextThisTick = false;
                FiredThisTick = false;
                HealTickCount = 0; // 새 물안개는 아직 한 번도 회복하지 않았다.
            }
        }

        /// <summary>
        /// 중첩 해소 결과 — 유닛 1명에게 최종 선택된 물안개.
        /// 물안개 Id·텍스트 표시 여부를 따로 복사하지 않고 <see cref="ActiveMist"/> 참조를 그대로 들고 있는다.
        /// 선택된 물안개가 "이번 틱에 발화했는지"(FiredThisTick)까지 적용 단계에서 봐야 하기 때문이다.
        /// </summary>
        private readonly struct PickedUnit
        {
            public readonly UnitData Unit;
            public readonly float DistSqr;
            public readonly ActiveMist Source;

            public PickedUnit(UnitData unit, float distSqr, ActiveMist source)
            {
                Unit = unit;
                DistSqr = distSqr;
                Source = source;
            }
        }

        /// <summary> 중첩 해소 결과 — 건물 1채에게 최종 선택된 물안개(PickedUnit과 같은 구조). </summary>
        private readonly struct PickedBuilding
        {
            public readonly BuildingData Building;
            public readonly float DistSqr;
            public readonly ActiveMist Source;

            public PickedBuilding(BuildingData building, float distSqr, ActiveMist source)
            {
                Building = building;
                DistSqr = distSqr;
                Source = source;
            }
        }
    }
}
