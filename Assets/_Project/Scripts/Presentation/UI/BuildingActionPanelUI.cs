// ============================================================================
// BuildingActionPanelUI.cs
// 비생산 건물(MiningPost / AutoTower / FlightFacility / Research / MagicBuilding /
// HealShrine 등)을 클릭했을 때 표시되는 공용 액션 팝업 UI.
//
// 왜 별도 클래스인가?
//   - 생산 건물은 유닛 버튼/큐/업그레이드 등 풀스펙 UI(ProductionPanelUI)를 사용한다.
//   - 비생산 건물은 "건물 이름 표시 + 철거" 정도의 간이 UI만 필요하다.
//   - 공통 요소(헤더/닫기/철거/환불)는 BuildingPanelBase에서 제공한다.
//
// 이 클래스에서 추가하는 동작은 없으며, 베이스의 기능만 사용한다.
// 향후 건물 타입별 특수 기능(예: AutoTower 사거리 표시 토글)이 필요해지면
// 이 클래스에서 직렬화 필드와 OnShow 오버라이드를 추가하면 된다.
//
// 표시 대상 판정은 InputHandler에서 BuildingTypeHelper.CanShowActionPanel()로 수행.
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Domain;
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
                 "현재: DestroyButton 1개. 나중에 건물 타입별로 다른 버튼을 추가할 수 있다.")]
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
