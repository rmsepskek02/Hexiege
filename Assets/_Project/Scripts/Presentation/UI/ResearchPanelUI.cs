// ============================================================================
// ResearchPanelUI.cs
// 연구소(Research 건물) 클릭 시 표시되는 유닛 강화(연구) 패널 UI의 "로직 코어".
//
// 역할(GameSystemRules_UI.md 생산 패널 패턴 + GameSystemRules_Upgrade.md 규칙 8):
//   - 연구소 클릭 → Open(lab): 소유 팀·종족을 확정하고 트랙(그룹×스탯 + 자연회복) 상태를 노출.
//   - 트랙별 현재 레벨/다음 비용·시간, 진행 중 트랙 잠금, 골드 부족 색상 판정을 제공.
//   - 연구 버튼 → TryResearch(group, stat): 싱글=UseCase 직접 착수 / 멀티=NetworkUpgradeController.RequestResearch.
//   - (연구는 특정 연구소 종속이 아니라 팀 트랙 단위 — 아무 연구소에서나 트랙을 연구할 수 있다.)
//
// ⚠️ 이 클래스는 "데이터/상호작용 로직"만 담는다. 실제 트랙 버튼/텍스트/진행 바 등의
//    비주얼 레이아웃(프리팹·씬 배치·SerializeField 배선)은 사용자 Unity 작업이 필요하다.
//    프리팹의 각 트랙 행(버튼)은 이 컴포넌트의 public API(TryResearch/Get*)를 호출·조회하고,
//    RefreshRequested(팀 상태 변경 시 발화)를 구독해 표시를 갱신하도록 배선한다.
//
// Presentation 레이어 — MonoBehaviour. Application/Infrastructure UseCase를 주입받아 사용.
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
    /// 연구 패널의 로직 코어. 트랙 상태 조회 + 연구 착수 라우팅 + 갱신 알림을 제공한다.
    /// </summary>
    public class ResearchPanelUI : MonoBehaviour
    {
        [Header("Panel Root")]
        [Tooltip("패널 표시/숨김에 쓰는 CanvasGroup. 숨김은 SetActive 대신 alpha=0 + interactable=false 권장.")]
        [SerializeField] private CanvasGroup _panelGroup;

        [Header("Color (optional)")]
        [Tooltip("골드 부족 시 비용 텍스트 색상 판정을 위한 UI 색상 설정(생산 패널과 동일).")]
        [SerializeField] private UIColorConfig _colorConfig;

        // 주입 의존성.
        private UnitUpgradeUseCase _upgrade;
        private ResourceUseCase _resource;
        private NetworkUpgradeController _networkController; // 멀티플레이에서만 non-null.

        // 현재 열려 있는 연구소.
        private BuildingData _currentLab;
        // 현재 패널이 다루는 팀(=연구소 소유 팀).
        private TeamId _team;

        // 멀티플레이 순수 클라이언트의 진행 표시용 로컬 타이머.
        //   서버 권위 틱이 클라 UseCase에는 없으므로(진행 상태 미보유), 착수 확정 이벤트로 받은 total을
        //   로컬에서 카운트다운한다. 완료(OnUpgradeChanged로 레벨 반영)나 패널 닫힘 시 소거된다.
        private bool _hasLocalProgress;
        private UpgradeGroup _localProgressGroup;
        private UnitUpgradeStat _localProgressStat;
        private float _localProgressRemaining;
        private float _localProgressTotal;

        private CompositeDisposable _subs;

        /// <summary>
        /// 팀 강화 상태가 바뀌어 표시를 갱신해야 함을 알리는 이벤트. 트랙 행(버튼) 컴포넌트가 구독한다.
        /// </summary>
        public event Action RefreshRequested;

        /// <summary> 현재 패널이 열려 있는지. </summary>
        public bool IsOpen => _currentLab != null;

        // ====================================================================
        // 초기화 / 주입
        // ====================================================================

        /// <summary>
        /// 의존성을 주입한다. GameBootstrapper(조합 루트)에서 1회 호출.
        /// </summary>
        /// <param name="upgrade">팀별 강화 상태 UseCase.</param>
        /// <param name="resource">골드 조회/검증용.</param>
        /// <param name="networkController">멀티플레이 연구 요청 중계용. 싱글은 null.</param>
        public void Initialize(UnitUpgradeUseCase upgrade, ResourceUseCase resource,
            NetworkUpgradeController networkController = null)
        {
            _upgrade = upgrade;
            _resource = resource;
            _networkController = networkController;

            _subs?.Dispose();
            _subs = new CompositeDisposable();

            // 팀 강화 상태 변경 → 표시 갱신.
            GameEvents.OnUpgradeChanged.Subscribe(team =>
            {
                if (_currentLab == null || team != _team) return;
                // 완료로 레벨이 오르면 로컬 진행 표시는 소거(서버 확정).
                _hasLocalProgress = false;
                RefreshRequested?.Invoke();
            }).AddTo(_subs);

            // 골드 변경 → 비용 색상 재평가(공통 UI 규칙 14).
            GameEvents.OnResourceChanged.Subscribe(e =>
            {
                if (_currentLab == null || e.Team != _team) return;
                RefreshRequested?.Invoke();
            }).AddTo(_subs);

            // 멀티 순수 클라이언트: 서버가 착수를 확정하면 로컬 진행 표시 시작.
            GameEvents.OnResearchStartedLocal.Subscribe(ev =>
            {
                if (_currentLab == null) return;
                _hasLocalProgress = true;
                _localProgressGroup = ev.Group;
                _localProgressStat = ev.Stat;
                _localProgressTotal = ev.Total;
                _localProgressRemaining = ev.Total;
                RefreshRequested?.Invoke();
            }).AddTo(_subs);

            HidePanel();
        }

        private void OnDestroy()
        {
            _subs?.Dispose();
            _subs = null;
        }

        private void Update()
        {
            // 멀티 클라이언트 로컬 진행 카운트다운(표시용).
            if (_hasLocalProgress)
            {
                _localProgressRemaining -= Time.deltaTime;
                if (_localProgressRemaining <= 0f)
                {
                    // 완료는 서버 브로드캐스트(OnUpgradeChanged)로 확정 — 여기서는 0에서 멈춰 대기.
                    _localProgressRemaining = 0f;
                }
            }
        }

        // ====================================================================
        // 열기 / 닫기
        // ====================================================================

        /// <summary>
        /// 연구소를 대상으로 패널을 연다. 소유 팀·종족을 확정하고 표시를 갱신한다.
        /// (연구는 연구소 종속이 아니지만, 착수한 연구소 Id는 파괴 시 취소·환불 기준으로 기록된다.)
        /// </summary>
        /// <param name="lab">클릭한 연구소(BuildingType.Research) 건물.</param>
        public void Open(BuildingData lab)
        {
            if (lab == null || lab.Type != BuildingType.Research) return;

            _currentLab = lab;
            _team = lab.Team;
            _hasLocalProgress = false;

            ShowPanel();
            RefreshRequested?.Invoke();
        }

        /// <summary> 패널을 닫는다. </summary>
        public void Close()
        {
            _currentLab = null;
            _hasLocalProgress = false;
            HidePanel();
        }

        private void ShowPanel()
        {
            if (_panelGroup == null) return;
            _panelGroup.alpha = 1f;
            _panelGroup.interactable = true;
            _panelGroup.blocksRaycasts = true;
        }

        private void HidePanel()
        {
            if (_panelGroup == null) return;
            _panelGroup.alpha = 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }

        // ====================================================================
        // 연구 착수 라우팅
        // ====================================================================

        /// <summary>
        /// 트랙 연구를 착수한다. 싱글=UseCase 직접, 멀티=NetworkUpgradeController 중계(서버 권위).
        /// UI 트랙 버튼 클릭 핸들러가 이 메서드를 호출한다.
        /// </summary>
        /// <param name="group">강화 그룹(Regen은 그룹 무시).</param>
        /// <param name="stat">강화 스탯.</param>
        /// <returns>요청을 보냈으면 true(멀티는 서버 검증 결과와 무관하게 요청 전송 여부).</returns>
        public bool TryResearch(UpgradeGroup group, UnitUpgradeStat stat)
        {
            if (_upgrade == null || _currentLab == null) return false;

            // 진행 중/최대 레벨은 UI에서도 1차 차단(서버가 최종 판정).
            if (!_upgrade.CanResearch(_team, group, stat)) return false;

            if (_networkController != null && NetworkContext.IsNetworkActive)
            {
                // 멀티: 서버로 요청. 성공/실패는 ClientRpc(진행 시작/토스트)로 회신된다.
                _networkController.RequestResearch(group, stat, _currentLab.Id, _team);
                return true;
            }

            // 싱글: 즉시 착수(골드 검증·차감·타이머 시작). 진행 상태는 UseCase가 보유·틱.
            bool ok = _upgrade.TryStartResearch(_team, group, stat, _currentLab.Id, _resource);
            if (ok) RefreshRequested?.Invoke();
            return ok;
        }

        // ====================================================================
        // 표시용 조회 API — 트랙 행(버튼) 컴포넌트가 사용
        // ====================================================================

        /// <summary> 현재 패널의 팀. </summary>
        public TeamId CurrentTeam => _team;

        /// <summary> 현재 팀 종족이 보유한 강화 그룹 목록(트랙 나열용). </summary>
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

        /// <summary> 골드 부족 여부(비용 텍스트 색상 판정용). </summary>
        public bool IsCostAffordable(UpgradeGroup group, UnitUpgradeStat stat)
        {
            int cost = GetNextCost(group, stat);
            if (cost < 0 || _resource == null) return false;
            return _resource.GetGold(_team) >= cost;
        }

        /// <summary> 골드 부족 색상(공통 UI 규칙 7·14). </summary>
        public Color GetInsufficientColor()
            => _colorConfig != null ? _colorConfig.goldInsufficientColor : Color.red;

        // Regen은 그룹 무관이므로 로컬 진행 비교 시 그룹 키를 정규화한다.
        private static UpgradeGroup Normalize(UpgradeGroup group, UnitUpgradeStat stat)
            => stat == UnitUpgradeStat.Regen ? UpgradeGroupHelper.RegenCanonicalGroup : group;
    }
}
