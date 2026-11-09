// ============================================================================
// BuildingActionPanelUI.cs
// 비생산 건물 중 전용 패널이 없는 것(MiningPost / AutoTower)을 클릭했을 때 표시되는
// 공용 액션 팝업 UI.
//   - Castle 은 철거도 생산도 불가해 애초에 어떤 팝업도 열리지 않는다.
//   - Research / FlightFacility / MagicBuilding / HealShrine 은 각자의 전용 패널
//     (연구 강화 · 스킬 · 물안개 신전)이 입력 분기에서 먼저 가로채므로 이 팝업으로 오지 않는다.
//     단 그 전용 패널이 씬에 미배선이면 이 팝업으로 폴백되므로,
//     표시 자격 판정 자체는 위 네 타입에도 true 를 돌려준다.
//
// 왜 별도 클래스인가?
//   - 생산 건물은 유닛 버튼/큐/업그레이드 등 풀스펙 UI(ProductionPanelUI)를 사용한다.
//   - 비생산 건물은 "건물 이름 표시 + 철거" 정도의 간이 UI만 필요하다.
//   - 공통 요소(헤더/닫기/철거/환불)는 BuildingPanelBase에서 제공한다.
//
// 이 클래스가 베이스에 더하는 것은 3x3 그리드 슬롯의 표시 제어 하나뿐이다 —
// 슬롯 버튼 목록 두 개를 직렬화 필드로 받고, OnShow 를 오버라이드해 활성 목록에 든 슬롯만 켠다.
// 건물 타입별 특수 기능(예: AutoTower 사거리 표시 토글)은 아직 없으며,
// 필요해지면 같은 자리에 직렬화 필드와 훅을 더 붙이면 된다.
//   [2026-11-09 갱신 — 위 문단은 그때의 기록으로 두고 덧붙인다] AutoTower 공격 범위 표시가 생겼다(토글이 아니라
//   "탑 패널이 열려 있는 동안 항상" 표시 — Task `_Tasks/2026-11-09/22_19_cannon-tower-front-arc/` 커밋 3, 추가 범위 (가)).
//     · 탑 3종 모두 사거리 원, 인간 대포탑은 그 위에 전방 120° 부채꼴(그 팀 정면 — Red 화면에서도 화면상 올바른 방향).
//     · 반경 · 부채꼴 여부는 TowerCombatUseCase.GetTowerRangeDisplay(판정과 같은 원본), 각도는 TowerFrontArc 에서 읽는다.
//     · 표시물(TowerRangeIndicator)은 처음 필요할 때 코드로 만든다 → 씬 · 프리팹 · Inspector 작업이 없다.
//     · 패널이 닫히는 모든 경로(닫기 · 바깥 탭 · 철거 · 탑 파괴 · 경기 시작/종료)에서 OnBeforeClose 가 숨긴다.
//     · 화면 전용이라 판정에는 영향이 없다. 진단 로그 [TOWER-RANGE] Show / Hide(숨김 사유)를 남긴다(개발 축).
//
// 표시 대상 판정은 InputHandler에서 BuildingTypeHelper.CanShowActionPanel()로 수행.
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 비생산 건물 클릭 시 표시되는 공용 액션 패널.
    /// 현재 지원 동작: 건물 이름 표시(헤더) + 철거.
    /// 모든 공통 동작은 BuildingPanelBase에 위치한다.
    /// </summary>
    public class BuildingActionPanelUI : BuildingPanelBase
    {
        // ====================================================================
        // 슬롯 버튼 (3x3 그리드의 런타임 표시 제어)
        // ====================================================================

        [Header("Slot Buttons")]
        [Tooltip("3x3 그리드의 슬롯 버튼 전체 (9개). " +
                 "Initialize()에서 각 버튼의 CanvasGroup을 캐시하고 초기값 alpha=0으로 설정한다.")]
        [SerializeField] private List<Button> _allSlotButtons;

        [Tooltip("실제 활성화할 버튼 목록. Inspector에서 연결하면 Show() 시 해당 버튼만 표시된다. " +
                 "현재는 철거 버튼이 놓인 슬롯 하나만 연결돼 있다. 나중에 건물 타입별로 다른 버튼을 추가할 수 있다.")]
        [SerializeField] private List<Button> _activeSlotButtons;

        /// <summary>
        /// _allSlotButtons와 1:1로 매칭되는 CanvasGroup 캐시 리스트.
        /// Initialize()에서 자동으로 채워진다.
        ///
        /// [왜 SetActive(false) 대신 CanvasGroup.alpha=0 을 쓰는가?]
        ///   - LayoutGroup(예: GridLayoutGroup)은 SetActive(false) 자식을 레이아웃 계산에서
        ///     완전히 제외한다. 그 결과 일부 슬롯만 숨기면 나머지 버튼이 빈 공간을 채우며
        ///     그리드 정렬이 깨진다.
        ///   - CanvasGroup은 GameObject 활성 상태를 유지하므로 RectTransform 크기/위치가
        ///     보존되고, alpha/interactable/blocksRaycasts만 0으로 만들어
        ///     시각적·상호작용적으로만 숨김 처리한다.
        ///
        /// Inspector에는 노출하지 않으며, 버튼에 CanvasGroup이 없으면 자동으로 부착한다.
        /// (BuildingPlacementUI._buttonCanvasGroups 와 동일한 패턴)
        /// </summary>
        private List<CanvasGroup> _slotCanvasGroups;

        // ====================================================================
        // [2026-11-09] AutoTower 공격 범위 표시 — 직렬화하지 않는 순수 런타임 상태
        // ====================================================================

        /// <summary>
        /// 범위 표시물. 처음 탑 패널이 열릴 때 TowerRangeIndicator.CreateRuntime()으로 만든다(씬에 미리 둘 필요 없음).
        /// 씬이 바뀌어 파괴되면 Unity null 이 되므로, 쓰기 전에 매번 `== null`(Unity 검사)로 확인하고 다시 만든다.
        /// </summary>
        private TowerRangeIndicator _towerRangeIndicator;

        /// <summary>
        /// 지금 표시 중인 탑의 공격 UseCase 를 돌려주는 함수. 조합 루트(GameBootstrapper)가 SetTowerRangeSource 로 넣는다.
        /// UseCase 가 맵을 불러올 때마다 새로 만들어지므로 객체가 아니라 "그때의 필드를 읽는 함수"를 받는다.
        /// </summary>
        private Func<TowerCombatUseCase> _towerRangeSource;

        /// <summary> 지금 범위를 보여 주는 탑 Id(-1 = 표시 없음). 숨김 사유 판정 · 진단 로그용. </summary>
        private int _rangeShownTowerId = -1;

        /// <summary> 지금 범위를 보여 주기 시작한 시각(Time.unscaledTime) — Hide 로그의 ShownMs. </summary>
        private float _rangeShownAt;

        /// <summary>
        /// 다음 숨김의 사유(진단 로그 Reason=). null 이면 "PanelClosed"(닫기 버튼 · 바깥 탭 등 일반 닫기).
        /// 철거 · 탑 파괴 · 경기 시작/종료는 닫기 직전에 이 값을 먼저 정해 둔다.
        /// </summary>
        private string _pendingRangeHideReason;

        /// <summary> 탑 파괴 구독을 이미 걸었는지(Initialize 가 여러 번 불려도 한 번만). </summary>
        private bool _rangeDiedSubscribed;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 의존성 주입. GameBootstrapper에서 호출한다.
        /// 멀티플레이 모드라면 networkBuildingController를 함께 전달하여
        /// 철거 요청이 서버 RPC를 거치도록 한다.
        /// 싱글플레이 모드라면 마지막 인자는 null로 호출한다.
        /// </summary>
        /// <param name="buildingPlacement">건물 도메인 상태 제어 UseCase.</param>
        /// <param name="resource">자원(골드) 관리 UseCase.</param>
        /// <param name="networkBuildingController">멀티플레이 컨트롤러. 싱글플레이 시 null.</param>
        public void Initialize(
            BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource,
            NetworkBuildingController networkBuildingController = null)
        {
            // [2026-11-09 범위 표시] 표시 중인 탑이 파괴되면 숨김 사유를 "TowerDied"로 정해 둔다.
            //   ⚠️ 반드시 InitializeBase 보다 "먼저" 구독한다: 베이스도 같은 이벤트(OnBuildingDied)를 구독해 그 자리에서 Close 를 부르고,
            //   UniRx 는 구독한 순서대로 알린다. 이 구독이 먼저여야 Close → OnBeforeClose 가 돌 때 사유가 이미 정해져 있다.
            //   (패널 · 표시물 정리 자체는 베이스의 Close 가 한다 — 여기서는 사유만 기록한다.)
            //   .AddTo(this): 이 컴포넌트가 파괴되면 구독도 자동 해제(BuildingPanelBase 와 같은 관례).
            if (!_rangeDiedSubscribed)
            {
                GameEvents.OnBuildingDied
                    .Subscribe(e =>
                    {
                        if (e.Building != null && e.Building.Id == _rangeShownTowerId && _pendingRangeHideReason == null)
                            _pendingRangeHideReason = "TowerDied";
                    })
                    .AddTo(this);
                _rangeDiedSubscribed = true;
            }

            // 베이스의 의존성/버튼 이벤트 연결을 수행한다.
            InitializeBase(buildingPlacement, resource, networkBuildingController);

            // 슬롯 버튼별 CanvasGroup 캐시 구성.
            // (BuildingPlacementUI.Initialize() 의 _buttonCanvasGroups 구성 로직과 동일)
            BuildSlotCanvasGroups();
        }

        /// <summary>
        /// _allSlotButtons를 순회하며 각 버튼의 CanvasGroup을 캐시한다.
        /// CanvasGroup이 없는 버튼에는 자동으로 부착하고, 초기값을 모두 alpha=0(숨김)으로 둔다.
        ///
        /// [왜 Awake()가 아니라 Initialize()에서 처리하는가?]
        ///   GameBootstrapper가 UseCase 등 외부 의존성을 주입하는 시점이 Awake() 이후이므로,
        ///   Initialize()에서 의존성 주입과 캐시 구성을 함께 처리한다.
        ///   (BuildingPlacementUI와 동일한 이유)
        /// </summary>
        private void BuildSlotCanvasGroups()
        {
            // 슬롯 버튼이 미배선이면 빈 리스트로 초기화하고 종료. (NRE 방지)
            if (_allSlotButtons == null)
            {
                _slotCanvasGroups = new List<CanvasGroup>();
                return;
            }

            _slotCanvasGroups = new List<CanvasGroup>(_allSlotButtons.Count);

            for (int i = 0; i < _allSlotButtons.Count; i++)
            {
                var button = _allSlotButtons[i];
                if (button == null)
                {
                    // 빈 슬롯 자리에는 null을 추가해 인덱스 정합성을 유지.
                    _slotCanvasGroups.Add(null);
                    continue;
                }

                // 버튼 GameObject에서 CanvasGroup 조회 → 없으면 자동 부착.
                // TryGetComponent는 가비지 할당이 없는 권장 패턴.
                if (!button.TryGetComponent(out CanvasGroup cg))
                {
                    cg = button.gameObject.AddComponent<CanvasGroup>();
                }

                // 초기 상태: 완전히 숨김. OnShow()에서 필요한 슬롯만 alpha=1로 켠다.
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;

                _slotCanvasGroups.Add(cg);
            }
        }

        // ====================================================================
        // Show 훅 — 슬롯 표시 제어
        // ====================================================================

        /// <summary>
        /// 베이스 Show()가 헤더/환불 텍스트를 갱신한 뒤 호출하는 자식 훅.
        ///
        /// 동작:
        ///   1) _allSlotButtons 전체를 alpha=0으로 초기화하여 모두 숨긴다.
        ///   2) _activeSlotButtons에 포함된 버튼만 alpha=1로 켜서 표시한다.
        ///
        /// 안전 처리:
        ///   - _allSlotButtons가 null/비어있으면 아무것도 하지 않는다.
        ///   - _activeSlotButtons가 null/비어있으면 모든 슬롯이 숨겨진 상태로 유지된다.
        /// </summary>
        /// <param name="building">표시 대상 건물 데이터(현재 슬롯 제어에는 사용하지 않음).</param>
        protected override void OnShow(BuildingData building)
        {
            // [2026-11-09] AutoTower 면 공격 범위를 지면에 표시하고, 다른 건물이면(채굴소 등) 남아 있던 표시를 끈다.
            //   슬롯 처리보다 먼저 둔다 — 아래 슬롯 버튼 미배선 조기 반환에 범위 표시가 함께 막히지 않게.
            if (building != null && building.Type == BuildingType.AutoTower) ShowTowerRange(building);
            else HideTowerRange("NotTower");

            // 슬롯 버튼이 미배선이면 처리할 것이 없다.
            if (_allSlotButtons == null) return;

            // 1) 모든 슬롯을 먼저 숨긴다. (이전 Show()에서 켜진 슬롯 잔존 방지)
            SetAllSlotsHidden();

            // 2) 활성 목록이 없으면 모두 숨겨진 상태로 종료.
            if (_activeSlotButtons == null) return;

            // 3) 활성 목록에 포함된 버튼만 켠다.
            //    _allSlotButtons에서 해당 버튼의 인덱스를 찾아 같은 인덱스의 CanvasGroup을 켠다.
            for (int i = 0; i < _allSlotButtons.Count; i++)
            {
                var button = _allSlotButtons[i];
                if (button == null) continue;

                // 이 버튼이 활성 목록에 포함되어 있는지 확인.
                if (!_activeSlotButtons.Contains(button)) continue;

                // 인덱스 범위/null 체크 후 슬롯을 표시 상태로 전환.
                if (_slotCanvasGroups != null
                    && i < _slotCanvasGroups.Count
                    && _slotCanvasGroups[i] != null)
                {
                    _slotCanvasGroups[i].alpha = 1f;
                    _slotCanvasGroups[i].interactable = true;
                    _slotCanvasGroups[i].blocksRaycasts = true;
                }
            }
        }

        // ====================================================================
        // [2026-11-09] AutoTower 공격 범위 표시 — 열기 · 닫기 훅
        // ====================================================================

        /// <summary>
        /// 범위 표시에 쓸 탑 공격 UseCase 공급 함수를 넣는다. 조합 루트(GameBootstrapper)가 1회 부른다.
        /// 함수는 호출 시점의 UseCase 를 돌려줘야 한다(맵을 다시 불러오면 UseCase 가 새로 만들어진다).
        /// 넣지 않으면(null) 범위를 그리지 않는다 — 반경을 모르는 채로 그리면 실제 범위를 잘못 알려 주기 때문이다.
        /// </summary>
        /// <param name="source">현재 TowerCombatUseCase 를 돌려주는 함수.</param>
        public void SetTowerRangeSource(Func<TowerCombatUseCase> source)
        {
            _towerRangeSource = source;
        }

        /// <summary>
        /// 패널이 닫힐 때(베이스 Close 의 첫 단계) 범위 표시를 즉시 숨긴다.
        /// 닫기 버튼 · 바깥 탭 · 철거 · 탑 파괴(베이스의 건물 사망 처리) · 경기 시작/종료가 모두 이 길을 지난다.
        /// </summary>
        protected override void OnBeforeClose()
        {
            HideTowerRange(_pendingRangeHideReason ?? "PanelClosed");
        }

        /// <summary> 철거 버튼 — 숨김 사유를 Demolish 로 정한 뒤 베이스의 철거(싱글 직접 · 멀티 서버 요청 → Close)를 그대로 한다. </summary>
        protected override void OnDemolishButtonClick()
        {
            if (_rangeShownTowerId >= 0) _pendingRangeHideReason = "Demolish";
            base.OnDemolishButtonClick();
        }

        /// <summary> 경기 종료 — 숨김 사유를 GameEnd 로 정한 뒤 베이스대로 패널을 닫는다. </summary>
        public override void OnGameEnded()
        {
            if (_rangeShownTowerId >= 0) _pendingRangeHideReason = "GameEnd";
            base.OnGameEnded();
        }

        /// <summary> 경기 시작 — 숨김 사유를 GameStart 로 정한 뒤 베이스대로 패널을 닫는다. </summary>
        public override void OnGameStarted()
        {
            if (_rangeShownTowerId >= 0) _pendingRangeHideReason = "GameStart";
            base.OnGameStarted();
        }

        /// <summary>
        /// 탑의 공격 범위를 지면에 그린다(화면 전용).
        ///
        /// 좌표 주의(MistShrine 범위 원 선례): 판정 값은 도메인 좌표 기준이지만 화면에 물체를 놓을 때는 뷰 좌표를 쓴다.
        ///   Red 화면은 맵을 점대칭으로 뒤집어 보여 주므로, 중심뿐 아니라 "정면 방향"도 같은 변환을 거쳐야 화면상 올바른 쪽을 향한다.
        ///   방향은 "중심"과 "중심 + 정면" 두 점을 각각 화면 좌표로 바꾼 뒤 그 차이로 구한다(반전 여부와 무관하게 맞는 방법).
        /// </summary>
        /// <param name="tower">표시할 내 AutoTower.</param>
        private void ShowTowerRange(BuildingData tower)
        {
            // 다른 탑을 보여 주던 중이면(패널이 닫히지 않고 다른 탑으로 바로 열린 경우) 앞 표시를 사유와 함께 정리한다.
            if (_rangeShownTowerId >= 0 && _rangeShownTowerId != tower.Id) HideTowerRange("Retarget");
            _pendingRangeHideReason = null;

            TowerCombatUseCase source = _towerRangeSource != null ? _towerRangeSource() : null;
            if (source == null || !source.GetTowerRangeDisplay(tower, out float rangeTiles, out float rangeWorld, out bool hasFrontArc))
            {
                // 반경을 모르면 그리지 않는다(잘못된 범위를 보여 주는 것보다 안 보여 주는 편이 안전 — MistShrine 선례).
                // [개발] Warn + 개발 — 조합 루트 배선 · 설정 문제(모든 기기 공통)이고, 패널 자체는 정상 동작한다(대체 경로 있음).
                GameLog.Dev.Warn("UI", nameof(BuildingActionPanelUI),
                                 "[TOWER-RANGE] ShowSkipped — 탑 사거리를 알 수 없어 범위를 표시하지 않는다",
                                 $"Diag=TowerRangeDisplay, DiagRev=1, Event=ShowSkipped, TowerId={tower.Id}, Reason={(source == null ? "NoRangeSource" : "NoRange")}");
                HideTowerRange("NoRange");
                return;
            }

            if (_towerRangeIndicator == null) _towerRangeIndicator = TowerRangeIndicator.CreateRuntime();

            Vector3 domainCenter = HexMetrics.HexToWorld(tower.Position);
            Vector3 viewCenter = ViewConverter.ToView(domainCenter);
            Vector3 domainForward = TowerFrontArc.ResolveDomainForward(tower.Team);
            Vector3 viewForward = ViewConverter.ToView(domainCenter + domainForward) - viewCenter;
            viewForward.y = 0f;

            _towerRangeIndicator.Show(viewCenter, rangeWorld, hasFrontArc, TowerFrontArc.HalfAngleDeg, viewForward);
            _rangeShownTowerId = tower.Id;
            _rangeShownAt = Time.unscaledTime;
            DiagRangeShow(tower, rangeTiles, rangeWorld, hasFrontArc, domainForward, viewForward);
        }

        /// <summary> 범위 표시를 숨긴다. 실제로 보이던 표시가 꺼질 때만 Hide 진단 줄을 남긴다(사유 포함). </summary>
        /// <param name="reason">숨김 사유 토큰(PanelClosed · Demolish · TowerDied · GameEnd · GameStart · NotTower · Retarget · NoRange).</param>
        private void HideTowerRange(string reason)
        {
            _pendingRangeHideReason = null;
            bool wasShown = _towerRangeIndicator != null && _towerRangeIndicator.IsShown;
            if (_towerRangeIndicator != null) _towerRangeIndicator.Hide();
            int towerId = _rangeShownTowerId;
            _rangeShownTowerId = -1;
            if (!wasShown || towerId < 0) return;
            DiagRangeHide(towerId, reason);
        }

        /// <summary>
        /// [진단 — [TOWER-RANGE] Show] 범위 표시를 켠 순간 1줄. 표시 반경과 판정 사거리가 같은 값인지, 부채꼴 · 정면 방향이
        /// 화면에서 어느 쪽인지(Red 화면에서도 화면 위쪽이어야 한다)를 로그로 판정할 수 있게 한다. 개발 빌드 · 에디터 전용.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagRangeShow(BuildingData tower, float rangeTiles, float rangeWorld, bool hasFrontArc,
                                   Vector3 domainForward, Vector3 viewForward)
        {
            RaceId race = tower.Team == TeamId.Blue ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
            // 카메라는 +Z 쪽을 화면 위로 보여 준다(틸트 카메라, 회전 없음) → 화면 방향 z > 0 이면 "Up".
            string screen = !hasFrontArc ? "None" : viewForward.z > 0.001f ? "Up" : viewForward.z < -0.001f ? "Down" : "Side";
            GameLog.Dev.Info("UI", nameof(BuildingActionPanelUI), "[TOWER-RANGE] Show",
                System.FormattableString.Invariant(
                    $"Diag=TowerRangeDisplay, DiagRev=1, Event=Show, Role={ServerProjectileDiagnostics.RoleName}, TowerId={tower.Id}, Team={tower.Team}, Race={race}, LocalTeam={LocalPlayerTeam.Current}, RangeTiles={rangeTiles:F2}, JudgeRange={rangeWorld:F3}, DisplayRadius={rangeWorld:F3}, FrontArc={(hasFrontArc ? "true" : "false")}, HalfAngleDeg={(hasFrontArc ? TowerFrontArc.HalfAngleDeg : 0f):F0}, DomainForward={(domainForward.z >= 0f ? "+Z" : "-Z")}, ViewForward={viewForward.x:F2}|{viewForward.z:F2}, ScreenForward={screen}, ViewFlipped={(ViewConverter.IsFlipped ? "true" : "false")}"));
        }

        /// <summary> [진단 — [TOWER-RANGE] Hide] 보이던 범위 표시가 꺼진 순간 1줄(사유 · 표시 시간). 개발 빌드 · 에디터 전용. </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagRangeHide(int towerId, string reason)
        {
            GameLog.Dev.Info("UI", nameof(BuildingActionPanelUI), "[TOWER-RANGE] Hide",
                System.FormattableString.Invariant(
                    $"Diag=TowerRangeDisplay, DiagRev=1, Event=Hide, Role={ServerProjectileDiagnostics.RoleName}, TowerId={towerId}, Reason={reason}, ShownMs={Mathf.RoundToInt((Time.unscaledTime - _rangeShownAt) * 1000f)}"));
        }

        /// <summary>
        /// 모든 슬롯 CanvasGroup을 숨김 상태(alpha=0, 상호작용 불가)로 만든다.
        /// GameObject는 활성 상태를 유지하므로 레이아웃 공간은 보존된다.
        /// </summary>
        private void SetAllSlotsHidden()
        {
            if (_slotCanvasGroups == null) return;

            for (int i = 0; i < _slotCanvasGroups.Count; i++)
            {
                var cg = _slotCanvasGroups[i];
                if (cg == null) continue;

                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }
    }
}
