// ============================================================================
// BuildingSkillPanelUI.cs
// 스킬 건물(FlightFacility / MagicSpirit / WillowShrine) 클릭 시 표시되는 전용 스킬 패널.
//
// 왜 전용 패널인가(안 B 확정):
//   스킬 건물은 동적 슬롯(종족 로드아웃)·쿨다운 오버레이·지점 조준 연동이 많아, 범용 액션 패널을
//   확장하기보다 전용 패널을 신설하는 편이 응집도·완성도에서 유리하다(절대규칙 7).
//   공통 요소(헤더/닫기/철거/환불/생명주기/IGameUI)는 BuildingPanelBase에서 그대로 재사용한다.
//
// 슬롯 배치(규칙 9):
//   슬롯 1~5 = 스킬 버튼(종족 로드아웃, 최대 5). 슬롯 6 = 철거(베이스 _demolishButton, 고정). 7~9 예약.
//   로드아웃이 5개 미만이면 앞 슬롯부터 채우고 남는 스킬 슬롯은 숨긴다(CanvasGroup alpha).
//
// 발동 흐름:
//   - 지점 지정 스킬(타입 A·B): 스킬 버튼 PointerDown → SkillAimController.BeginAim(조준) →
//     손을 떼면 콜백으로 좌표 확정 → (싱글) SkillActivationUseCase.Activate / (멀티) INetworkSkillController.
//   - 즉시 발동 스킬(타입 C, Phase 2): 버튼 탭 즉시 발동(좌표 없음). Phase 1엔 실행기 미등록이라 무효.
//
// 쿨다운 표시(규칙 3·10):
//   패널이 열려 있는 동안 매 프레임 SkillActivationUseCase에서 남은 쿨다운을 읽어 모든 스킬 슬롯 위
//   오버레이(radial + 숫자)를 갱신한다. 글로벌 쿨다운이므로 슬롯 1~5 전부에 동일 표시.
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 스킬 건물 전용 패널. 종족 로드아웃으로 슬롯 1~5를 동적으로 채우고, 조준/발동/쿨다운을 연동한다.
    /// </summary>
    public sealed class BuildingSkillPanelUI : BuildingPanelBase
    {
        // ====================================================================
        // 직렬화 필드(Inspector 연결)
        // ====================================================================

        [Header("스킬 슬롯(1~5)")]
        [Tooltip("스킬 버튼 5개(슬롯 1~5). 배열 순서 = 슬롯 번호(0-based).")]
        [SerializeField] private List<Button> _skillSlotButtons = new List<Button>(5);

        [Tooltip("각 스킬 슬롯의 아이콘 Image(버튼과 1:1). SkillData.Icon으로 갱신. 비어도 동작한다.")]
        [SerializeField] private List<Image> _skillSlotIcons = new List<Image>(5);

        [Tooltip("각 스킬 슬롯의 쿨다운 오버레이(버튼과 1:1). 글로벌 쿨다운을 표시.")]
        [SerializeField] private List<SkillCooldownOverlay> _skillSlotOverlays = new List<SkillCooldownOverlay>(5);

        // ====================================================================
        // 의존성(Initialize 주입)
        // ====================================================================

        private SkillActivationUseCase _skillActivation;
        private ISkillDataProvider _dataProvider;
        private SkillAimController _skillAimController;
        // 멀티플레이에서 발동 요청을 서버로 중계(싱글은 null).
        private INetworkSkillController _networkSkillController;

        // 현재 표시 중인 건물의 종족 로드아웃(OnShow에서 캐시).
        private IReadOnlyList<SkillData> _currentLoadout;

        // 슬롯 버튼 CanvasGroup 캐시(GridLayout 보존형 숨김 — 액션 패널과 동일 패턴).
        private List<CanvasGroup> _slotCanvasGroups;

        private bool _eventsWired;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 의존성 주입. GameBootstrapper에서 호출한다.
        /// </summary>
        /// <param name="buildingPlacement">건물 도메인 상태 제어(철거) UseCase.</param>
        /// <param name="resource">자원(골드) UseCase.</param>
        /// <param name="networkBuildingController">멀티플레이 철거 중계 컨트롤러(싱글은 null).</param>
        /// <param name="skillActivation">스킬 발동/쿨다운 UseCase.</param>
        /// <param name="dataProvider">종족별 스킬 로드아웃 제공자.</param>
        /// <param name="skillAimController">지점 조준 컨트롤러.</param>
        /// <param name="networkSkillController">멀티플레이 발동 중계 컨트롤러(싱글은 null).</param>
        public void Initialize(
            BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource,
            NetworkBuildingController networkBuildingController,
            SkillActivationUseCase skillActivation,
            ISkillDataProvider dataProvider,
            SkillAimController skillAimController,
            INetworkSkillController networkSkillController = null)
        {
            // 베이스(헤더/닫기/철거/환불) 의존성·버튼 이벤트 연결.
            InitializeBase(buildingPlacement, resource, networkBuildingController);

            _skillActivation = skillActivation;
            _dataProvider = dataProvider;
            _skillAimController = skillAimController;
            _networkSkillController = networkSkillController;

            BuildSlotCanvasGroups();
            WireSkillButtons();
        }

        /// <summary>
        /// 스킬 슬롯 버튼별 CanvasGroup을 캐시한다(없으면 자동 부착, 초기 alpha=0).
        /// SetActive(false)는 GridLayout 정렬을 깨므로 CanvasGroup alpha로 숨긴다(액션 패널과 동일 이유).
        /// </summary>
        private void BuildSlotCanvasGroups()
        {
            _slotCanvasGroups = new List<CanvasGroup>(_skillSlotButtons.Count);
            for (int i = 0; i < _skillSlotButtons.Count; i++)
            {
                Button button = _skillSlotButtons[i];
                if (button == null) { _slotCanvasGroups.Add(null); continue; }

                if (!button.TryGetComponent(out CanvasGroup cg))
                    cg = button.gameObject.AddComponent<CanvasGroup>();

                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                _slotCanvasGroups.Add(cg);
            }
        }

        /// <summary>
        /// 각 스킬 슬롯 버튼에 PointerDown 이벤트를 1회 연결한다(중복 방지).
        /// PointerDown 시점의 슬롯 인덱스로 조준/발동을 시작한다(생산 버튼 EventTrigger 패턴과 동일).
        /// </summary>
        private void WireSkillButtons()
        {
            if (_eventsWired) return;
            _eventsWired = true;

            for (int i = 0; i < _skillSlotButtons.Count; i++)
            {
                Button button = _skillSlotButtons[i];
                if (button == null) continue;

                int slotIndex = i; // 클로저 캡처용 지역 복사.
                var trigger = button.gameObject.GetComponent<EventTrigger>()
                              ?? button.gameObject.AddComponent<EventTrigger>();

                var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                down.callback.AddListener(_ => OnSkillPointerDown(slotIndex));
                trigger.triggers.Add(down);

                // 스킬은 조준/발동을 PointerDown에서 시작하므로 onClick은 사용하지 않는다.
                button.onClick.RemoveAllListeners();
            }
        }

        // ====================================================================
        // Show 훅 — 슬롯 동적 채움
        // ====================================================================

        /// <summary>
        /// 베이스 Show()가 헤더/환불을 갱신한 뒤 호출하는 자식 훅.
        /// 건물 종족의 로드아웃으로 슬롯 1~5의 아이콘을 채우고, 스킬 수만큼만 슬롯을 표시한다.
        /// </summary>
        /// <param name="building">표시 대상 스킬 건물.</param>
        protected override void OnShow(BuildingData building)
        {
            _currentLoadout = null;

            // 종족 키 분기(규칙 1·6): 건물 팀 → 종족 → 로드아웃.
            RaceId race = (building.Team == TeamId.Blue)
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            if (_dataProvider != null)
                _currentLoadout = _dataProvider.GetLoadout(race);

            int skillCount = _currentLoadout?.Count ?? 0;

            // 슬롯 채움: 스킬이 있는 슬롯만 아이콘 설정 + 표시, 나머지는 숨김.
            for (int i = 0; i < _skillSlotButtons.Count; i++)
            {
                bool hasSkill = i < skillCount;

                // 아이콘 설정.
                if (i < _skillSlotIcons.Count && _skillSlotIcons[i] != null)
                {
                    Sprite icon = hasSkill ? _currentLoadout[i].Icon : null;
                    _skillSlotIcons[i].sprite = icon;
                    _skillSlotIcons[i].enabled = icon != null;
                }

                // 슬롯 표시/숨김(CanvasGroup).
                if (_slotCanvasGroups != null && i < _slotCanvasGroups.Count && _slotCanvasGroups[i] != null)
                {
                    CanvasGroup cg = _slotCanvasGroups[i];
                    cg.alpha = hasSkill ? 1f : 0f;
                    cg.interactable = hasSkill;
                    cg.blocksRaycasts = hasSkill;
                }

                // 오버레이는 매 프레임 Update에서 갱신되므로 여기서는 초기 상태만 정리.
                if (i < _skillSlotOverlays.Count && _skillSlotOverlays[i] != null)
                    _skillSlotOverlays[i].Hide();
            }
        }

        /// <summary>
        /// 패널이 닫힐 때 진행 중인 조준을 취소해 입력 잠금이 남지 않도록 한다.
        /// </summary>
        protected override void OnBeforeClose()
        {
            _skillAimController?.CancelAim();
        }

        // ====================================================================
        // 매 프레임 — 쿨다운 오버레이 갱신
        // ====================================================================

        private void Update()
        {
            if (!IsOpen || _currentBuilding == null || _skillActivation == null) return;

            int id = _currentBuilding.Id;
            float remaining = _skillActivation.GetCooldownRemaining(id);
            float total = _skillActivation.GetCooldownTotal(id);

            // 글로벌 쿨다운 — 슬롯 1~5 전부에 동일 표시(규칙 3·10). 스킬이 없는 슬롯은 숨김이라 무해.
            int skillCount = _currentLoadout?.Count ?? 0;
            for (int i = 0; i < _skillSlotOverlays.Count; i++)
            {
                SkillCooldownOverlay overlay = _skillSlotOverlays[i];
                if (overlay == null) continue;

                if (i < skillCount)
                    overlay.SetCooldown(remaining, total);
                else
                    overlay.Hide();
            }
        }

        // ====================================================================
        // 발동 흐름
        // ====================================================================

        /// <summary>
        /// 스킬 슬롯 버튼을 누른 순간 호출. 쿨다운 중이면 무시하고, 아니면 조준(지점 지정) 또는
        /// 즉시 발동(타입 C)을 시작한다.
        /// </summary>
        /// <param name="slotIndex">누른 슬롯 번호(0-based).</param>
        private void OnSkillPointerDown(int slotIndex)
        {
            if (_currentBuilding == null || _currentLoadout == null) return;
            if (slotIndex < 0 || slotIndex >= _currentLoadout.Count) return;

            int buildingId = _currentBuilding.Id;

            // 글로벌 쿨다운 중이면 발동 불가(규칙 3). (오버레이 raycast 차단과 이중 가드.)
            //   조용히 return하지 않고, 사용자가 왜 안 되는지 알 수 있도록 토스트로 안내한다.
            //   이 가드는 즉시발동/지점지정 진입보다 앞에 있으므로 두 스킬 타입 모두를 커버한다.
            if (_skillActivation != null && _skillActivation.IsOnCooldown(buildingId))
            {
                ToastUI.Show(ToastKey.SkillOnCooldown);
                return;
            }

            SkillData skill = _currentLoadout[slotIndex];
            if (skill == null) return;

            if (skill.AimType == SkillAimType.PointTarget)
            {
                // 지점 지정 — 조준 모드 진입. 조준 중 맵이 보이도록 팝업은 숨긴다(랠리 패턴).
                _popup?.Hide();

                // 공유 BlockingOverlay(패널 열릴 때 표시됨)는 "탭하면 Close" 콜백을 갖는다.
                //   조준 중 맵을 드래그/탭하면 이 오버레이가 먼저 먹어 패널을 닫아버리므로,
                //   조준 진입 시 오버레이를 숨겨 조준 입력이 맵으로 전달되게 한다(랠리와 달리 조준은
                //   HandleClick이 아니라 SkillAimController가 직접 포인터를 읽으므로 오버레이 제거 필요).
                UIManager.Instance?.HideBlockingOverlay();

                if (_skillAimController != null)
                {
                    // fallback = 시전 건물 위치(화면 중앙이 맵 밖일 때 조준 원 기본 위치).
                    //   조준은 연속 도메인 월드 좌표로 다루므로, 건물 타일(HexCoord)을 도메인 월드로 변환해 넘긴다.
                    Vector3 fallbackDomain = HexMetrics.HexToWorld(_currentBuilding.Position);
                    _skillAimController.BeginAim(buildingId, slotIndex, skill.Radius,
                        fallbackDomain, OnAimConfirm, OnAimCancel);
                }
                else
                {
                    // 조준 컨트롤러가 없으면 조준할 수 없으므로 패널을 닫아 정리(안전장치).
                    Close();
                }
            }
            else
            {
                // 즉시 발동(타입 C, Phase 2). 좌표 없음. Phase 1엔 실행기 미등록이라 무효 처리된다.
                ActivateSkill(buildingId, slotIndex, default, false);
                Close();
            }
        }

        /// <summary>
        /// 조준 확정(손을 뗌) 콜백 — 확정 좌표(도메인 월드, 연속)에 스킬을 발동하고 패널을 닫는다.
        /// </summary>
        private void OnAimConfirm(int buildingId, int slotIndex, Vector3 aimWorld)
        {
            ActivateSkill(buildingId, slotIndex, aimWorld, true);
            Close();
        }

        /// <summary>
        /// 조준 취소(하단 X/무효 위치) 콜백 — 발동하지 않고 패널을 닫는다.
        /// </summary>
        private void OnAimCancel()
        {
            Close();
        }

        /// <summary>
        /// 실제 발동 요청을 보낸다. 멀티는 서버 RPC 중계, 싱글은 UseCase 직접 호출(서버 권위).
        /// </summary>
        /// <param name="buildingId">스킬 건물 Id.</param>
        /// <param name="slotIndex">슬롯 번호.</param>
        /// <param name="aimWorld">조준 좌표(도메인 월드 Vector3, 지점 지정에서만 유효).</param>
        /// <param name="hasAim">조준 좌표 유효 여부.</param>
        private void ActivateSkill(int buildingId, int slotIndex, Vector3 aimWorld, bool hasAim)
        {
            bool isNetworkMode = _networkSkillController != null && NetworkContext.IsNetworkActive;

            if (isNetworkMode)
            {
                // 서버가 재검증·실행·쿨다운 브로드캐스트를 담당한다(규칙 25·26).
                _networkSkillController.RequestActivateSkill(buildingId, slotIndex, aimWorld, hasAim);
            }
            else
            {
                // 싱글플레이: 로컬 서버 권위로 직접 발동.
                _skillActivation?.Activate(buildingId, slotIndex, hasAim ? aimWorld : (Vector3?)null);
            }
        }
    }
}
