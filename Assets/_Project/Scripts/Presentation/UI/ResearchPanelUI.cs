// ============================================================================
// ResearchPanelUI.cs
// 연구소(Research 건물) 클릭 시 표시되는 유닛 강화(연구) 패널의 "로직 코어 + 패널 프레임".
//
// ── 2026-07-28 재구성(확정 설계) ────────────────────────────────────────────
//   [구조] 기존 독립 MonoBehaviour → BuildingPanelBase 상속으로 전환.
//     · 공통 프레임(헤더 제목 + 닫기[X] + 하단 철거 버튼 + 환불액)을 BuildingPanelBase에서 상속.
//       → 비생산 건물 액션 패널/생산 패널과 동일한 위치·스타일·동작을 공유한다.
//     · 철거(하단) = 건물 파괴(건설비 환불 + 진행 중 연구도 취소·환불).
//       (연구 취소·환불은 GameBootstrapper의 OnBuildingDied→OnLabDestroyed 구독이 담당 — 회귀 없음.)
//
//   [본문 2-레이어] 공통 프레임은 고정, 본문만 두 레이어로 전환(연구소 단위):
//     (a) 매트릭스 레이어: 행=현재 종족 그룹, 열=공격력/방어력/이동속도. 각 칸=연구 버튼.
//         초월은 2×3 격자 + 자연회복 전체폭 버튼 1개. (ResearchMatrixView/ResearchCellView가 렌더)
//     (b) 진행 레이어: 해당 연구소가 연구 중일 때 표시. 이름 + Lv X→X+1 + 게이지 + 남은 시간 + [취소].
//         취소 = 연구만 취소(투입 골드 환불, 건물 유지). (ResearchProgressView가 렌더)
//     레이어 전환은 Open(building) 시점 + 상태 변화 시 UpdateLayerVisibility()로 결정한다.
//
//   [buildingId ↔ 진행 연구 매핑]
//     · 싱글/호스트: UnitUpgradeUseCase.TryGetActiveResearchByBuilding(buildingId) 로 조회.
//     · 멀티 순수 클라: UseCase에 진행 상태가 없으므로, 서버가 착수 확정 시 내려준 buildingId를
//       로컬 진행(OnResearchStartedLocal)에 귀속해 두고 그 값으로 판정한다.
//
// 이 클래스는 "데이터/상호작용 로직 + 레이어 오케스트레이션"을 담고, 실제 셀/진행 비주얼은
// 자식 뷰(ResearchMatrixView/ResearchCellView/ResearchProgressView)가 담당한다(역할 분리).
//
// Presentation 레이어 — MonoBehaviour(BuildingPanelBase). Application/Infrastructure UseCase 주입.
// ============================================================================

using System;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 연구 패널. 공통 프레임(BuildingPanelBase) + 트랙 상태 조회 API + 연구 착수/취소 라우팅 +
    /// 매트릭스/진행 레이어 전환을 제공한다.
    /// </summary>
    public class ResearchPanelUI : BuildingPanelBase
    {
        // ====================================================================
        // 직렬화 필드 (본문 레이어 — Inspector/Wire 배선)
        // ====================================================================

        [Header("Research Body Layers")]
        [Tooltip("매트릭스(연구 버튼 격자) 레이어 CanvasGroup. 연구 중이 아닐 때 표시.")]
        [SerializeField] private CanvasGroup _matrixLayerGroup;

        [Tooltip("진행(게이지) 레이어 CanvasGroup. 이 연구소가 연구 중일 때 표시.")]
        [SerializeField] private CanvasGroup _progressLayerGroup;

        // ====================================================================
        // 주입 의존성 (연구 고유)
        // ====================================================================

        private UnitUpgradeUseCase _upgrade;
        private NetworkUpgradeController _networkController; // 멀티플레이에서만 non-null.

        // 현재 패널이 다루는 팀(=연구소 소유 팀).
        private TeamId _team;

        // ────────────────────────────────────────────────────────────────────
        // 멀티플레이 순수 클라이언트의 진행 표시용 로컬 타이머.
        //   서버 권위 틱이 클라 UseCase에는 없으므로(진행 상태 미보유), 착수 확정 이벤트로 받은 total을
        //   로컬에서 카운트다운한다. buildingId를 함께 보관해 "이 연구소가 연구 중"인지 판정에 쓴다.
        //   완료(OnUpgradeChanged)나 취소(ResearchCanceledClientRpc→OnUpgradeChanged) 시 소거된다.
        // ────────────────────────────────────────────────────────────────────
        private bool _hasLocalProgress;
        private UpgradeGroup _localProgressGroup;
        private UnitUpgradeStat _localProgressStat;
        private float _localProgressRemaining;
        private float _localProgressTotal;
        private int _localProgressBuildingId = -1;

        private CompositeDisposable _subs;

        /// <summary>
        /// 팀 강화 상태가 바뀌어 표시를 갱신해야 함을 알리는 이벤트. 매트릭스/진행 뷰가 구독한다.
        /// </summary>
        public event Action RefreshRequested;

        // ====================================================================
        // 초기화 / 주입
        // ====================================================================

        /// <summary>
        /// 의존성을 주입한다. GameBootstrapper(조합 루트)에서 1회 호출.
        /// 베이스 의존성(건물 철거/자원/네트워크 건물 컨트롤러)과 연구 고유 의존성을 함께 받는다.
        /// </summary>
        /// <param name="upgrade">팀별 강화 상태 UseCase.</param>
        /// <param name="resource">골드 조회/검증/환불용.</param>
        /// <param name="networkController">멀티플레이 연구 요청 중계용. 싱글은 null.</param>
        /// <param name="buildingPlacement">철거(건물 제거)용. BuildingPanelBase가 사용.</param>
        /// <param name="networkBuildingController">멀티플레이 철거 요청 중계용. 싱글은 null.</param>
        public void Initialize(UnitUpgradeUseCase upgrade, ResourceUseCase resource,
            NetworkUpgradeController networkController = null,
            BuildingPlacementUseCase buildingPlacement = null,
            NetworkBuildingController networkBuildingController = null)
        {
            // 베이스 의존성/공통 버튼(닫기·철거) 등록.
            InitializeBase(buildingPlacement, resource, networkBuildingController);

            _upgrade = upgrade;
            _networkController = networkController;

            _subs?.Dispose();
            _subs = new CompositeDisposable();

            // 팀 강화 상태 변경(착수/완료/취소/레벨 동기화) → 표시 갱신 + 레이어 재평가.
            //   완료/취소는 이 팀의 로컬 진행 표시를 소거한다(서버/호스트 확정).
            GameEvents.OnUpgradeChanged.Subscribe(team =>
            {
                if (_currentBuilding == null || team != _team) return;
                _hasLocalProgress = false;
                Refresh();
            }).AddTo(_subs);

            // 골드 변경 → 비용 색상 재평가(공통 UI 규칙 14).
            GameEvents.OnResourceChanged.Subscribe(e =>
            {
                if (_currentBuilding == null || e.Team != _team) return;
                Refresh();
            }).AddTo(_subs);

            // 멀티 순수 클라이언트: 서버가 착수를 확정하면 로컬 진행 표시 시작(연구소 귀속).
            GameEvents.OnResearchStartedLocal.Subscribe(ev =>
            {
                if (_currentBuilding == null) return;
                _hasLocalProgress = true;
                _localProgressGroup = ev.Group;
                _localProgressStat = ev.Stat;
                _localProgressTotal = ev.Total;
                _localProgressRemaining = ev.Total;
                _localProgressBuildingId = ev.BuildingId;
                Refresh();
            }).AddTo(_subs);
        }

        private void OnDestroy()
        {
            _subs?.Dispose();
            _subs = null;
        }

        private void Update()
        {
            // 멀티 클라이언트 로컬 진행 카운트다운(표시용). 완료는 서버 브로드캐스트로 확정.
            if (_hasLocalProgress)
            {
                _localProgressRemaining -= Time.deltaTime;
                if (_localProgressRemaining < 0f) _localProgressRemaining = 0f;
            }
        }

        // ====================================================================
        // 열기 — InputHandler 호환 진입점
        // ====================================================================

        /// <summary>
        /// 연구소를 대상으로 패널을 연다(InputHandler 호출 진입점). 타입 검증 후 베이스 Show로 위임.
        /// (연구는 연구소 종속이 아니지만, 착수한 연구소 Id는 파괴/취소 시 환불 기준으로 기록된다.)
        /// </summary>
        /// <param name="lab">클릭한 연구소(BuildingType.Research) 건물.</param>
        public void Open(BuildingData lab)
        {
            if (lab == null || lab.Type != BuildingType.Research) return;
            Show(lab);
        }

        // ====================================================================
        // 베이스 훅 — Show / Close
        // ====================================================================

        /// <summary>
        /// 베이스 Show()가 _currentBuilding 저장 + 헤더 + 팝업 + 오버레이 + 환불 표시까지 끝낸 뒤 호출.
        /// 팀을 확정하고 헤더 제목을 연구 패널용으로 덮어쓴 뒤, 레이어를 결정하고 표시를 갱신한다.
        /// </summary>
        protected override void OnShow(BuildingData building)
        {
            _team = building.Team;

            // 베이스는 헤더를 BuildingType.ToString()("Research")로 채우므로, 연구 패널 제목으로 덮어쓴다.
            if (_headerText != null) _headerText.text = "연구소 강화";

            Refresh();
        }

        /// <summary>
        /// 베이스 Close()가 팝업을 숨기기 전에 호출되는 정리 훅.
        /// 로컬 진행(_hasLocalProgress)은 의도적으로 유지한다 — 클라이언트가 패널을 닫았다 다시 열어도
        /// 진행 중 연구를 계속 표시해야 하기 때문. (완료/취소 시에만 소거된다.)
        /// </summary>
        protected override void OnBeforeClose()
        {
            // 특별한 정리 없음. (RefreshRequested 구독자는 IsOpen=false를 보고 스스로 갱신을 건너뛴다.)
        }

        // ====================================================================
        // 레이어 전환 + 갱신
        // ====================================================================

        /// <summary>
        /// 레이어 가시성을 재평가하고 뷰 갱신을 요청한다. 내부 상태 변화 시 이 메서드로 일원화한다.
        /// </summary>
        private void Refresh()
        {
            UpdateLayerVisibility();
            RefreshRequested?.Invoke();
        }

        /// <summary>
        /// 현재 연구소가 연구 중이면 진행 레이어를, 아니면 매트릭스 레이어를 표시한다.
        /// </summary>
        private void UpdateLayerVisibility()
        {
            if (_currentBuilding == null) return;

            bool researching = TryGetCurrentLabResearch(out _, out _);
            SetLayer(_progressLayerGroup, researching);
            SetLayer(_matrixLayerGroup, !researching);
        }

        // CanvasGroup 가시성(+레이캐스트) 일괄 설정. null 안전.
        private static void SetLayer(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        // ====================================================================
        // 연구 착수 / 취소 라우팅
        // ====================================================================

        /// <summary>
        /// 트랙 연구를 착수한다. 싱글=UseCase 직접, 멀티=NetworkUpgradeController 중계(서버 권위).
        /// 매트릭스 셀(ResearchCellView)의 버튼 클릭 핸들러가 이 메서드를 호출한다.
        /// </summary>
        /// <param name="group">강화 그룹(Regen은 그룹 무시).</param>
        /// <param name="stat">강화 스탯.</param>
        /// <returns>요청을 보냈으면 true.</returns>
        public bool TryResearch(UpgradeGroup group, UnitUpgradeStat stat)
        {
            if (_upgrade == null || _currentBuilding == null) return false;

            // 진행 중/최대 레벨은 UI에서도 1차 차단(서버가 최종 판정).
            if (!_upgrade.CanResearch(_team, group, stat)) return false;

            if (_networkController != null && NetworkContext.IsNetworkActive)
            {
                // 멀티: 서버로 요청. 성공/실패는 ClientRpc(진행 시작/토스트)로 회신된다.
                _networkController.RequestResearch(group, stat, _currentBuilding.Id, _team);
                return true;
            }

            // 싱글: 즉시 착수(골드 검증·차감·타이머 시작). 진행 상태는 UseCase가 보유·틱.
            bool ok = _upgrade.TryStartResearch(_team, group, stat, _currentBuilding.Id, _resource);
            if (ok) Refresh();
            return ok;
        }

        /// <summary>
        /// 현재 연구소가 진행 중인 연구를 취소한다(건물 유지, 투입 골드 환불).
        /// 진행 레이어(ResearchProgressView)의 [취소] 버튼이 호출한다.
        ///   - 멀티: 서버로 취소 요청(서버가 환불·정리 후 클라에 통지).
        ///   - 싱글/호스트: UseCase 직접 취소·환불 후 매트릭스로 복귀.
        /// </summary>
        /// <returns>취소 요청을 보냈거나 성공했으면 true.</returns>
        public bool TryCancelCurrentLabResearch()
        {
            if (_upgrade == null || _currentBuilding == null) return false;

            if (_networkController != null && NetworkContext.IsNetworkActive)
            {
                // 멀티: 서버가 검증·취소·환불. 완료 통지는 ResearchCanceledClientRpc→OnUpgradeChanged로 회신.
                _networkController.RequestCancelResearch(_currentBuilding.Id, _team);
                return true;
            }

            // 싱글/호스트: 즉시 취소·환불. CancelResearchByBuilding이 OnUpgradeChanged를 발행 → Refresh.
            bool ok = _upgrade.CancelResearchByBuilding(_currentBuilding.Id, _resource);
            if (ok)
            {
                _hasLocalProgress = false;
                Refresh();
            }
            return ok;
        }

        // ====================================================================
        // 진행 레이어 조회 API — ResearchProgressView 가 사용
        // ====================================================================

        /// <summary>
        /// 현재 연구소가 진행 중인 연구의 그룹/스탯을 반환한다(진행 레이어/레이어 전환 판정용).
        ///   - 싱글/호스트: UseCase에서 buildingId로 조회.
        ///   - 멀티 순수 클라: 로컬 진행이 이 연구소에 귀속돼 있으면 그 값을 반환.
        /// </summary>
        public bool TryGetCurrentLabResearch(out UpgradeGroup group, out UnitUpgradeStat stat)
        {
            if (_upgrade != null && _currentBuilding != null
                && _upgrade.TryGetActiveResearchByBuilding(_currentBuilding.Id, out group, out stat))
                return true;

            if (_hasLocalProgress && _currentBuilding != null
                && _localProgressBuildingId == _currentBuilding.Id)
            {
                group = _localProgressGroup;
                stat = _localProgressStat;
                return true;
            }

            group = default;
            stat = default;
            return false;
        }

        // ====================================================================
        // 표시용 조회 API — 매트릭스/진행 뷰가 사용
        // ====================================================================

        /// <summary> 현재 패널의 팀. </summary>
        public TeamId CurrentTeam => _team;

        /// <summary> 패널이 열려 있는지(베이스 팝업 상태 기준). 뷰가 갱신 스킵 판단에 사용. </summary>
        public bool IsPanelOpen => IsOpen;

        /// <summary> 현재 팀 종족이 보유한 강화 그룹 목록(매트릭스 행 나열용). </summary>
        public UpgradeGroup[] GetGroupsForCurrentTeam()
        {
            RaceId race = _team == TeamId.Blue ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
            return UpgradeGroupHelper.GetGroupsForRace(race);
        }

        /// <summary> 현재 팀이 초월계인지(자연회복 트랙 노출 여부). </summary>
        public bool CurrentTeamIsTranscendence()
        {
            RaceId race = _team == TeamId.Blue ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
            return race == RaceId.Transcendence;
        }

        /// <summary> 트랙 현재 레벨(0~5). </summary>
        public int GetLevel(UpgradeGroup group, UnitUpgradeStat stat)
            => _upgrade != null ? _upgrade.GetLevel(_team, group, stat) : 0;

        /// <summary> 다음 레벨 비용(골드). 최대 레벨이면 -1. </summary>
        public int GetNextCost(UpgradeGroup group, UnitUpgradeStat stat)
            => _upgrade != null ? _upgrade.GetNextLevelCost(_team, group, stat) : -1;

        /// <summary> 다음 레벨 연구 시간(초). 최대 레벨이면 -1. </summary>
        public float GetNextTime(UpgradeGroup group, UnitUpgradeStat stat)
            => _upgrade != null ? _upgrade.GetNextLevelTime(_team, group, stat) : -1f;

        /// <summary> 트랙이 연구 진행 중인지(팀 잠금 — 규칙 8). 멀티 클라 로컬 표시도 포함. </summary>
        public bool IsResearching(UpgradeGroup group, UnitUpgradeStat stat)
        {
            if (_upgrade != null && _upgrade.IsResearching(_team, group, stat)) return true;
            // 멀티 순수 클라의 로컬 진행 표시(서버 권위 진행이 UseCase에 없을 때).
            return _hasLocalProgress && _localProgressGroup == Normalize(group, stat) && _localProgressStat == stat;
        }

        /// <summary>
        /// 진행 표시 값을 조회한다(남은/전체 초). 진행 중이 아니면 false.
        /// 싱글/호스트는 UseCase에서, 멀티 순수 클라는 로컬 타이머에서 읽는다.
        /// </summary>
        public bool TryGetDisplayProgress(UpgradeGroup group, UnitUpgradeStat stat,
            out float remaining, out float total)
        {
            if (_upgrade != null && _upgrade.TryGetProgress(_team, group, stat, out remaining, out total))
                return true;

            if (_hasLocalProgress && _localProgressGroup == Normalize(group, stat) && _localProgressStat == stat)
            {
                remaining = _localProgressRemaining;
                total = _localProgressTotal;
                return true;
            }

            remaining = 0f;
            total = 0f;
            return false;
        }

        /// <summary> 골드 부족 여부(비용 텍스트 색상/버튼 활성 판정용). </summary>
        public bool IsCostAffordable(UpgradeGroup group, UnitUpgradeStat stat)
        {
            int cost = GetNextCost(group, stat);
            if (cost < 0 || _resource == null) return false;
            return _resource.GetGold(_team) >= cost;
        }

        /// <summary> 골드 부족 색상(공통 UI 규칙 7·14). 베이스 _colorConfig 사용. </summary>
        public Color GetInsufficientColor()
            => _colorConfig != null ? _colorConfig.goldInsufficientColor : Color.red;

        /// <summary> 일반(충분) 텍스트 색상. 베이스 _colorConfig 사용, 미연결 시 흰색. </summary>
        public Color GetNormalColor()
            => _colorConfig != null ? _colorConfig.normalTextColor : Color.white;

        // Regen은 그룹 무관이므로 로컬 진행 비교 시 그룹 키를 정규화한다.
        private static UpgradeGroup Normalize(UpgradeGroup group, UnitUpgradeStat stat)
            => stat == UnitUpgradeStat.Regen ? UpgradeGroupHelper.RegenCanonicalGroup : group;

        // ====================================================================
        // 표시 이름(한국어) — 매트릭스 헤더/셀/진행 뷰 공용 정적 헬퍼
        // ====================================================================

        /// <summary> 강화 그룹의 한국어 표시명(매트릭스 행 헤더용). </summary>
        public static string GroupDisplayName(UpgradeGroup group)
        {
            switch (group)
            {
                case UpgradeGroup.HumanMelee: return "근접";
                case UpgradeGroup.HumanRanged: return "원거리";
                case UpgradeGroup.HumanVehicle: return "탈것";
                case UpgradeGroup.SpiritFire: return "불";
                case UpgradeGroup.SpiritWater: return "물";
                case UpgradeGroup.SpiritEarth: return "땅";
                case UpgradeGroup.TransAnimal: return "동물";
                case UpgradeGroup.TransPlant: return "식물";
                default: return group.ToString();
            }
        }

        /// <summary> 강화 스탯의 한국어 표시명(매트릭스 열 헤더용). </summary>
        public static string StatDisplayName(UnitUpgradeStat stat)
        {
            switch (stat)
            {
                case UnitUpgradeStat.Attack: return "공격력";
                case UnitUpgradeStat.Defense: return "방어력";
                case UnitUpgradeStat.MoveSpeed: return "이동속도";
                case UnitUpgradeStat.Regen: return "자연회복";
                default: return stat.ToString();
            }
        }

        /// <summary> 트랙 전체 표시명(그룹+스탯). 자연회복은 그룹 무관이라 스탯명만. </summary>
        public static string TrackDisplayName(UpgradeGroup group, UnitUpgradeStat stat)
        {
            if (stat == UnitUpgradeStat.Regen) return StatDisplayName(stat);
            return $"{GroupDisplayName(group)} {StatDisplayName(stat)}";
        }
    }
}
