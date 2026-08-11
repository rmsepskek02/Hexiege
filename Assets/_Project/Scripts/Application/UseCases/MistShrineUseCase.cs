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
        private readonly List<ActiveMist> _firingBuffer = new List<ActiveMist>(4);
        private readonly List<UnitData> _unitBuffer = new List<UnitData>(16);
        private readonly List<float> _unitDistBuffer = new List<float>(16);
        private readonly List<BuildingData> _buildingBuffer = new List<BuildingData>(16);
        private readonly List<float> _buildingDistBuffer = new List<float>(16);
        private readonly Dictionary<int, PickedUnit> _bestUnitByTarget = new Dictionary<int, PickedUnit>();
        private readonly Dictionary<int, PickedBuilding> _bestBuildingByTarget = new Dictionary<int, PickedBuilding>();

        /// <summary>물안개를 시전 건물 Id 오름차순으로 정렬하는 비교자(중첩 해소 결정성 — 규칙 13).</summary>
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
            RemoveMistsOfShrine(buildingId);

            // ⑤ 물안개 생성 — 중심은 시전 건물 타일의 "도메인" 월드 좌표다(규칙 3·10 — 고정 원형, 이동 없음).
            Vector3 center = Flatten(_mapper.HexToWorld(building.Position));
            _activeMists.Add(new ActiveMist(buildingId, building.Team, center, _mistDuration));

            // ⑥ 쿨다운 시작(규칙 21). 시전 비용은 없다(규칙 11 — 골드 소모 없음).
            StartCooldownLocal(buildingId, _cooldown);

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

            for (int i = 0; i < _autoCastBuffer.Count; i++)
            {
                // Activate 안에서 건물 생존·타입·쿨다운을 다시 확인하므로 여기서 중복 검사하지 않는다.
                Activate(_autoCastBuffer[i]);
            }
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
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void TickMists(float dt)
        {
            // 서버 전용 가드(2중 방어) — 클라이언트에서 돌면 회복이 두 번 적용된다.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            if (dt <= 0f || _activeMists.Count == 0) return;

            // ── 1) 시간 진행 + 이번 틱에 회복을 발화할 물안개 수집 ───────────────
            _firingBuffer.Clear();
            for (int i = 0; i < _activeMists.Count; i++)
            {
                ActiveMist mist = _activeMists[i];
                mist.Remaining -= dt;
                mist.TickAccum += dt;

                // 1초가 찼으면 이번 틱에 회복을 적용한다(누적분에서 정확히 1초만 뺀다 — 이월분 보존).
                if (mist.TickAccum >= HealTickInterval)
                {
                    mist.TickAccum -= HealTickInterval;

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

                    _firingBuffer.Add(mist);
                }
            }

            // ── 2) 회복 적용(중첩 해소 포함) ─────────────────────────────────────
            if (_firingBuffer.Count > 0)
            {
                ApplyHealTick();
                _firingBuffer.Clear();
            }

            // ── 3) 지속시간이 끝난 물안개 제거(뒤에서부터 지워 인덱스가 밀리지 않게 한다) ──
            for (int i = _activeMists.Count - 1; i >= 0; i--)
            {
                if (_activeMists[i].Remaining <= 0f)
                    _activeMists.RemoveAt(i);
            }
        }

        /// <summary>
        /// 이번 틱에 발화한 물안개들의 회복을 대상별로 1회씩만 적용한다(중첩 해소 — 규칙 13).
        ///
        /// 절차:
        ///   ① 시전 건물 Id 오름차순으로 물안개를 순회한다(거리 동률 시 Id 작은 쪽이 이기도록).
        ///   ② 각 물안개의 범위 안 아군 유닛·아군 건물을 수집하며 중심까지의 거리제곱을 함께 얻는다.
        ///   ③ 대상별로 "가장 가까운 물안개"만 남긴다(엄격 부등호 &lt; 이므로 동률이면 먼저 온 쪽 유지).
        ///   ④ 남은 조합에 회복을 적용한다.
        /// </summary>
        private void ApplyHealTick()
        {
            if (_healPerTick <= 0) return;

            _bestUnitByTarget.Clear();
            _bestBuildingByTarget.Clear();

            // ① 결정적 순회 순서 확보.
            _firingBuffer.Sort(MistIdAscending);

            for (int m = 0; m < _firingBuffer.Count; m++)
            {
                ActiveMist mist = _firingBuffer[m];

                // ② 아군 유닛 수집.
                CollectAllyUnitsInRadius(mist.Team, mist.CenterWorld, _unitBuffer, _unitDistBuffer);
                for (int i = 0; i < _unitBuffer.Count; i++)
                {
                    UnitData unit = _unitBuffer[i];
                    float d2 = _unitDistBuffer[i];
                    // ③ 더 가까운 물안개만 남긴다. 동률(<가 거짓)이면 먼저 온(Id 작은) 물안개가 유지된다.
                    if (_bestUnitByTarget.TryGetValue(unit.Id, out PickedUnit prev) && d2 >= prev.DistSqr)
                        continue;
                    _bestUnitByTarget[unit.Id] = new PickedUnit(unit, d2, mist.SourceBuildingId, mist.ShowTextThisTick);
                }

                // ② 아군 건물 수집(시전 건물 자신·Castle도 그대로 포함 — 규칙 4).
                CollectAllyBuildingsInRadius(mist.Team, mist.CenterWorld, _buildingBuffer, _buildingDistBuffer);
                for (int i = 0; i < _buildingBuffer.Count; i++)
                {
                    BuildingData building = _buildingBuffer[i];
                    float d2 = _buildingDistBuffer[i];
                    if (_bestBuildingByTarget.TryGetValue(building.Id, out PickedBuilding prev) && d2 >= prev.DistSqr)
                        continue;
                    _bestBuildingByTarget[building.Id] = new PickedBuilding(building, d2, mist.SourceBuildingId, mist.ShowTextThisTick);
                }
            }

            // ④ 실제 회복 적용. 대상마다 정확히 1회만 호출된다.
            foreach (var kv in _bestUnitByTarget)
            {
                PickedUnit pick = kv.Value;
                ApplyHealToUnitEntity(pick.Unit, pick.SourceBuildingId, pick.ShowText);
            }
            foreach (var kv in _bestBuildingByTarget)
            {
                PickedBuilding pick = kv.Value;
                ApplyHealToBuildingEntity(pick.Building, pick.SourceBuildingId, pick.ShowText);
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
            target.Heal(_healPerTick);
            // 실제로 회복이 일어난 대상만 이벤트를 발행한다(규칙 5·15 — 풀피 대상은 텍스트도 없다).
            if (target.Hp == before) return;

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
            target.Heal(_healPerTick);
            if (target.Hp == before) return;

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
            RemoveMistsOfShrine(buildingId);
            _autoMode.Remove(buildingId);
            _cooldownRemaining.Remove(buildingId);
            _cooldownTotal.Remove(buildingId);
        }

        /// <summary> 재경기·맵 전환 시 모든 물안개·자동 모드·쿨다운 상태를 초기화한다. </summary>
        public void ClearAll()
        {
            _activeMists.Clear();
            _autoMode.Clear();
            _cooldownRemaining.Clear();
            _cooldownTotal.Clear();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

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

            /// <summary>회복 틱(1초) 누적기. 이 물안개만의 독립 누적기다(규칙 8-1·14).</summary>
            public float TickAccum;

            /// <summary>회복 텍스트 표시 주기 누적기. 회복 틱과 별개로 센다(규칙 15).</summary>
            public float TextAccum;

            /// <summary>이번 틱의 회복에 텍스트를 띄울지 여부(TickMists가 매 틱 갱신).</summary>
            public bool ShowTextThisTick;

            public ActiveMist(int sourceBuildingId, TeamId team, Vector3 centerWorld, float duration)
            {
                SourceBuildingId = sourceBuildingId;
                Team = team;
                CenterWorld = centerWorld;
                Remaining = duration;
                TickAccum = 0f;
                TextAccum = 0f;
                ShowTextThisTick = false;
            }
        }

        /// <summary> 중첩 해소 결과 — 유닛 1명에게 최종 선택된 물안개 정보. </summary>
        private readonly struct PickedUnit
        {
            public readonly UnitData Unit;
            public readonly float DistSqr;
            public readonly int SourceBuildingId;
            public readonly bool ShowText;

            public PickedUnit(UnitData unit, float distSqr, int sourceBuildingId, bool showText)
            {
                Unit = unit;
                DistSqr = distSqr;
                SourceBuildingId = sourceBuildingId;
                ShowText = showText;
            }
        }

        /// <summary> 중첩 해소 결과 — 건물 1채에게 최종 선택된 물안개 정보. </summary>
        private readonly struct PickedBuilding
        {
            public readonly BuildingData Building;
            public readonly float DistSqr;
            public readonly int SourceBuildingId;
            public readonly bool ShowText;

            public PickedBuilding(BuildingData building, float distSqr, int sourceBuildingId, bool showText)
            {
                Building = building;
                DistSqr = distSqr;
                SourceBuildingId = sourceBuildingId;
                ShowText = showText;
            }
        }
    }
}
