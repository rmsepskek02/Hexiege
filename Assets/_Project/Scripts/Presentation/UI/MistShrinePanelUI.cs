// ============================================================================
// MistShrinePanelUI.cs
// MistShrine(초월 종족 회복 건물, BuildingType.HealShrine)을 클릭하면 열리는 전용 패널.
//
// 무엇을 하나(유니티 초급자용 설명):
//   내 MistShrine을 누르면 이 창이 뜬다. 창에는 [사용] 버튼과 [철거] 버튼이 있다.
//     · 사용 버튼을 짧게 누르면  → 물안개를 한 번 깐다(수동 시전).
//     · 사용 버튼을 길게 누르면  → 자동 모드를 켜고 끈다(쿨다운이 끝나는 즉시 서버가 알아서 시전).
//     · 자동 모드일 때 짧게 누르면 → 자동 모드가 꺼진다(시전은 하지 않는다).
//   이 조작 규칙은 생산 패널의 유닛 버튼과 똑같이 통일한 것이다(GameSystemRules_UI.md MistShrine 규칙 5).
//   창이 열려 있는 동안에는 지도 위에 회복 범위가 반투명 원으로 표시된다(규칙 8).
//
// 왜 전용 패널인가(규칙 1·2):
//   스킬 패널은 "종족 로드아웃 5슬롯 + 지점 조준"을 전제로 하는데 MistShrine에는 그 전제가 하나도 없다.
//   그래서 스킬 패널을 재사용하지 않고 전용 패널을 만들되, 공통 프레임(헤더 · 닫기[X] · 철거 버튼 + 환불 표시 ·
//   건물 파괴 시 자동 닫힘 · IGameUI 생명주기)은 BuildingPanelBase에서 그대로 물려받는다.
//
// 슬롯 배치(규칙 10 — 3×3 그리드 9슬롯 규격은 공통을 따른다):
//   슬롯 1 = 사용(시전) 버튼 / 슬롯 2~5 = 예약(숨김) / 슬롯 6 = 철거(베이스 제공, 위치 고정) / 7~9 = 예약(숨김).
//   ⚠️ 안 쓰는 슬롯은 SetActive(false)가 아니라 CanvasGroup.alpha=0으로 숨긴다(규칙 11).
//      SetActive(false)는 GridLayout의 셀 정렬을 무너뜨려 남은 버튼이 앞으로 당겨진다.
//
// ⚠️ 이 클래스에 OnDestroy를 선언하지 말 것:
//   자식이 OnDestroy를 선언하면 BuildingPanelBase의 정리 로직이 숨겨져(hide) 실행되지 않는다.
//   구독이 필요하면 반드시 UniRx의 .AddTo(this)를 쓴다(BuildingPanelBase 112~115행 주석 참조).
//
// 미제작 항목:
//   · 사용 버튼 아이콘(규칙 15) — 임시 텍스트 라벨로 대체한다.
//   · 물안개 지속 VFX(Buildings 규칙 26) — 이번 범위 밖. 회복 여부는 HP 막대와 회복 텍스트로 확인한다.
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using System.Collections.Generic;
using TMPro;                       // 임시 라벨(TMP_Text) — 정식 아이콘 도입 시 제거.
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
    /// MistShrine 전용 패널. 사용(탭=시전 / 롱프레스=자동 토글) · 쿨다운 표시 · 회복 범위 표시를 담당한다.
    /// </summary>
    public sealed class MistShrinePanelUI : BuildingPanelBase
    {
        // ====================================================================
        // 직렬화 필드(Inspector 연결 — 에디터 셋업 스크립트가 자동 배선)
        // ====================================================================

        [Header("슬롯(3×3 그리드 9개)")]
        [Tooltip("3×3 그리드의 슬롯 버튼 전체(9개). 슬롯 1=사용, 6=철거, 나머지는 CanvasGroup.alpha=0으로 숨긴다.")]
        [SerializeField] private List<Button> _allSlotButtons = new List<Button>(9);

        [Header("사용(시전)")]
        [Tooltip("슬롯 1의 사용(시전) 버튼. 짧은 탭=시전 / 길게 누르기=자동 모드 토글.")]
        [SerializeField] private Button _useButton;

        [Tooltip("사용 버튼 위 쿨다운 표시(시계방향 radial fill + 남은 초). 스킬 패널과 같은 컴포넌트를 재사용한다.")]
        [SerializeField] private SkillCooldownOverlay _useCooldownOverlay;

        [Tooltip("⚠️ 임시 라벨 — 물안개 아이콘이 아직 없어서 버튼에 글자로 표시한다(UI 규칙 15). 정식 아이콘 도입 시 제거.")]
        [SerializeField] private TMP_Text _useLabel;

        [Header("자동 모드 표시")]
        [Tooltip("자동 모드일 때 사용 버튼에 보이는 테두리 회전 오버레이. " +
                 "⚠️ 오버레이 오브젝트는 '하나만' 두고 단일 경로로만 on/off 한다(UI 규칙 14). " +
                 "생산 패널처럼 도트 인디케이터와 이중 배선을 만들지 않는다.")]
        [SerializeField] private Image _autoBorderOverlay;

        [Header("회복 범위 표시")]
        [Tooltip("패널이 열려 있는 동안 지도에 표시할 회복 범위 원(UI 규칙 8·12). 미배선이어도 패널은 동작한다.")]
        [SerializeField] private MistShrineRangeIndicator _rangeIndicator;

        // ⚠️ 반경(_rangeRadius) Inspector 필드는 두지 않는다 — 값이 두 곳에 존재하면 반드시 어긋난다.
        //    화면의 원 크기는 MistShrineUseCase.GetRadius()(= SpecialAttackConfig.asset의 MistRadius를
        //    주입받은 값)에서 그때그때 읽어 쓴다. 밸런싱은 설정 에셋 한 곳만 고치면 된다.
        //    자세한 이유는 MistShrineUseCase.GetRadius()의 주석 참조.

        // ====================================================================
        // 의존성(Initialize 주입)
        // ====================================================================

        /// <summary>물안개 힐 로직(시전·쿨다운·자동 모드). 싱글에서는 여기로 직접 시전한다.</summary>
        private MistShrineUseCase _mistShrine;

        /// <summary>멀티플레이에서 시전/자동 토글 요청을 서버로 중계(싱글은 null).</summary>
        private INetworkMistShrineController _networkMistShrine;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>_allSlotButtons와 1:1로 매칭되는 CanvasGroup 캐시(GridLayout 보존형 숨김용).</summary>
        private List<CanvasGroup> _slotCanvasGroups;

        /// <summary>자동 모드 테두리 오버레이의 CanvasGroup 캐시. 이 하나로만 표시를 켜고 끈다(단일 경로 — 규칙 14).</summary>
        private CanvasGroup _autoBorderCanvasGroup;

        /// <summary>사용 버튼에 EventTrigger를 이미 붙였는지(중복 등록 방지).</summary>
        private bool _eventsWired;

        // ── 탭 / 롱프레스 판정(생산 패널과 동일한 방식 — UI 규칙 5) ──
        private const float LongPressThreshold = 0.5f;
        private bool _isPointerDown;
        private bool _longPressTriggered;
        private float _pointerDownTime;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 의존성 주입. GameBootstrapper(조합 루트)에서 호출한다.
        /// </summary>
        /// <param name="buildingPlacement">건물 도메인 상태 제어(철거) UseCase.</param>
        /// <param name="resource">자원(골드) UseCase — 철거 환불에 사용.</param>
        /// <param name="networkBuildingController">멀티플레이 철거 중계 컨트롤러(싱글은 null).</param>
        /// <param name="mistShrine">물안개 힐 UseCase(시전·쿨다운·자동 모드 조회).</param>
        /// <param name="networkMistShrine">멀티플레이 시전·자동 토글 중계 컨트롤러(싱글은 null).</param>
        public void Initialize(
            BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource,
            NetworkBuildingController networkBuildingController,
            MistShrineUseCase mistShrine,
            INetworkMistShrineController networkMistShrine = null)
        {
            // 베이스(헤더·닫기·철거·환불·건물 파괴 시 자동 닫힘) 의존성과 버튼 이벤트 연결.
            InitializeBase(buildingPlacement, resource, networkBuildingController);

            _mistShrine = mistShrine;
            _networkMistShrine = networkMistShrine;

            BuildSlotCanvasGroups();
            CacheAutoBorderCanvasGroup();
            WireUseButton();
        }

        /// <summary>
        /// 슬롯 버튼별 CanvasGroup을 캐시한다(없으면 자동 부착, 초기 alpha=0).
        /// SetActive(false)를 쓰지 않는 이유는 GridLayout 정렬이 무너지기 때문이다(UI 규칙 11).
        /// </summary>
        private void BuildSlotCanvasGroups()
        {
            _slotCanvasGroups = new List<CanvasGroup>(_allSlotButtons != null ? _allSlotButtons.Count : 0);
            if (_allSlotButtons == null) return;

            for (int i = 0; i < _allSlotButtons.Count; i++)
            {
                Button button = _allSlotButtons[i];
                if (button == null) { _slotCanvasGroups.Add(null); continue; } // 인덱스 정합성 유지.

                if (!button.TryGetComponent(out CanvasGroup cg))
                    cg = button.gameObject.AddComponent<CanvasGroup>();

                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                _slotCanvasGroups.Add(cg);
            }
        }

        /// <summary>
        /// 자동 모드 테두리 오버레이의 CanvasGroup을 확보한다(없으면 자동 부착).
        /// 표시 제어를 이 CanvasGroup "하나"로만 하기 위한 준비다(UI 규칙 14 — 단일 경로).
        /// </summary>
        private void CacheAutoBorderCanvasGroup()
        {
            if (_autoBorderOverlay == null) return;

            GameObject go = _autoBorderOverlay.gameObject;
            if (!go.TryGetComponent(out CanvasGroup cg))
                cg = go.AddComponent<CanvasGroup>();

            // 테두리는 장식이므로 버튼 입력을 절대 가로채지 않는다.
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;
            _autoBorderCanvasGroup = cg;
        }

        /// <summary>
        /// 사용 버튼에 PointerDown / PointerUp 이벤트를 1회 연결한다(중복 방지).
        /// EventTrigger를 프리팹에 미리 붙일 필요 없이 코드가 런타임에 부착한다
        /// (생산 패널 유닛 버튼과 완전히 같은 방식 — ProductionPanelUI.SetupUnitButtonBySlot).
        /// </summary>
        private void WireUseButton()
        {
            if (_eventsWired || _useButton == null) return;
            _eventsWired = true;

            EventTrigger trigger = _useButton.gameObject.GetComponent<EventTrigger>()
                                   ?? _useButton.gameObject.AddComponent<EventTrigger>();

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => OnUsePointerDown());
            trigger.triggers.Add(down);

            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => OnUsePointerUp());
            trigger.triggers.Add(up);

            // 탭/롱프레스를 PointerDown·PointerUp으로 직접 판정하므로 onClick은 쓰지 않는다.
            _useButton.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // Show / Close 훅
        // ====================================================================

        /// <summary>
        /// 베이스 Show()가 헤더·환불 금액을 갱신한 뒤 호출하는 자식 훅.
        /// 슬롯 표시 상태를 정리하고, 지도에 회복 범위 원을 켠다.
        /// </summary>
        /// <param name="building">표시 대상 MistShrine 건물.</param>
        protected override void OnShow(BuildingData building)
        {
            // 포인터 상태 초기화 — 이전에 열렸을 때의 눌림 상태가 남아 오작동하는 것을 막는다.
            _isPointerDown = false;
            _longPressTriggered = false;

            ApplySlotVisibility();

            // ⚠️ 임시 라벨 — 정식 아이콘이 나오면 이 줄과 _useLabel 필드를 함께 제거한다(UI 규칙 15).
            if (_useLabel != null)
            {
                _useLabel.text = "물안개";
                _useLabel.enabled = true;
            }

            // 오버레이는 Update가 매 프레임 갱신하므로 여기서는 초기 상태만 정리한다.
            _useCooldownOverlay?.Hide();
            ApplyAutoBorder(building != null && _mistShrine != null && _mistShrine.GetAutoMode(building.Id));

            ShowRangeIndicator(building);
        }

        /// <summary>
        /// 패널이 닫힐 때 회복 범위 원을 즉시 지운다(UI 규칙 8 — 패널을 닫으면 즉시 사라진다).
        /// 눌림 상태도 함께 정리해 다음에 열었을 때 롱프레스가 잘못 발화하지 않게 한다.
        /// </summary>
        protected override void OnBeforeClose()
        {
            _isPointerDown = false;
            _longPressTriggered = false;
            _rangeIndicator?.Hide();
        }

        /// <summary>
        /// 3×3 그리드 슬롯의 표시 상태를 규칙 10대로 적용한다.
        /// 슬롯 1(index 0) = 사용, 슬롯 6(index 5) = 철거만 보이고 나머지는 alpha=0으로 숨긴다.
        /// </summary>
        private void ApplySlotVisibility()
        {
            if (_slotCanvasGroups == null) return;

            for (int i = 0; i < _slotCanvasGroups.Count; i++)
            {
                CanvasGroup cg = _slotCanvasGroups[i];
                if (cg == null) continue;

                // index 0 = 슬롯 1(사용), index 5 = 슬롯 6(철거). 그 외는 예약 슬롯이라 숨긴다.
                bool visible = (i == 0) || (i == 5);
                cg.alpha = visible ? 1f : 0f;
                cg.interactable = visible;
                cg.blocksRaycasts = visible;
            }
        }

        /// <summary>
        /// 회복 범위 원을 건물 위치에 표시한다.
        ///
        /// 좌표 주의(중요):
        ///   회복 판정은 "도메인 월드 좌표"로 하지만, 화면에 물체를 놓을 때는 "뷰 좌표"를 써야 한다.
        ///   Red 팀은 맵을 뒤집어 보여 주므로(ViewConverter) 변환을 빠뜨리면 원이 엉뚱한 곳에 뜬다.
        ///
        /// 반경 주의(중요):
        ///   반경은 반드시 MistShrineUseCase.GetRadius()에서 읽는다. 패널이 자체 값을 들고 있으면
        ///   설정 에셋만 바꿨을 때 "보이는 원"과 "실제 회복 범위"가 조용히 어긋난다.
        /// </summary>
        /// <param name="building">범위를 표시할 MistShrine 건물.</param>
        private void ShowRangeIndicator(BuildingData building)
        {
            if (_rangeIndicator == null || building == null) return;

            // 주입 실패(UseCase null) 시에는 반경을 알 수 없다.
            // 이때 임의의 값으로 원을 그리면 "실제 회복 범위"를 잘못 알려 주는 셈이라 오히려 해롭다.
            // → 원을 그리지 않고 확실히 숨긴 뒤, 배선 문제를 알 수 있도록 경고만 남긴다.
            if (_mistShrine == null)
            {
                _rangeIndicator.Hide();
                // [개발] Warn + 개발.
                //   의존성 주입은 GameBootstrapper 의 배선이 결정하므로, null 이면 코드/씬 설정 오류다
                //   (1.3 원칙 3 단서). 모든 기기에 같은 배선이 나가 축 B ① 이 "아니오" → 개발.
                //   축 A: 원을 그리지 않고 확실히 숨기는 대체 경로로 계속 진행된다 → Warn.
                GameLog.Dev.Warn("UI", nameof(MistShrinePanelUI),
                                 "MistShrineUseCase 미주입 — 회복 범위 원을 표시할 수 없다. " +
                                 "GameBootstrapper 의 패널 초기화 배선을 확인해야 한다",
                                 "Field=_mistShrine");
                return;
            }

            Vector3 domainPos = HexMetrics.HexToWorld(building.Position);
            Vector3 viewPos = ViewConverter.ToView(domainPos);
            _rangeIndicator.Show(viewPos, _mistShrine.GetRadius());
        }

        // ====================================================================
        // 매 프레임 — 롱프레스 판정 + 쿨다운/자동 표시 갱신
        // ====================================================================

        private void Update()
        {
            // 롱프레스 판정: 누른 채로 0.5초가 지나면 자동 모드 토글(손을 떼기 전에 발화한다).
            //   Time.unscaledTime을 쓰는 이유는 게임이 일시정지(timeScale=0)여도 UI 조작은 살아 있어야 하기 때문이다.
            if (_isPointerDown && !_longPressTriggered
                && (Time.unscaledTime - _pointerDownTime >= LongPressThreshold))
            {
                _longPressTriggered = true;
                HandleToggleAuto();
            }

            if (!IsOpen || _currentBuilding == null || _mistShrine == null) return;

            int id = _currentBuilding.Id;

            // 쿨다운 표시(UI 규칙 7) — 남은 시간이 0이면 컴포넌트가 알아서 숨긴다.
            _useCooldownOverlay?.SetCooldown(
                _mistShrine.GetCooldownRemaining(id),
                _mistShrine.GetCooldownTotal(id));

            // 자동 모드 표시(UI 규칙 6·14) — 멀티에서는 서버 브로드캐스트로 상태가 바뀔 수 있으므로 매 프레임 반영한다.
            ApplyAutoBorder(_mistShrine.GetAutoMode(id));
        }

        /// <summary>
        /// 자동 모드 테두리 오버레이 표시를 갱신한다. 표시 제어 경로는 이 메서드 하나뿐이다(UI 규칙 14).
        /// </summary>
        /// <param name="isAuto">자동 모드가 켜져 있는지 여부.</param>
        private void ApplyAutoBorder(bool isAuto)
        {
            if (_autoBorderCanvasGroup == null) return;
            _autoBorderCanvasGroup.alpha = isAuto ? 1f : 0f;
        }

        // ====================================================================
        // 입력 처리(탭 / 롱프레스 — UI 규칙 5)
        // ====================================================================

        /// <summary> 사용 버튼을 누른 순간. 롱프레스 판정을 위한 타이머를 시작한다. </summary>
        private void OnUsePointerDown()
        {
            _pointerDownTime = Time.unscaledTime;
            _isPointerDown = true;
            _longPressTriggered = false;
        }

        /// <summary> 사용 버튼에서 손을 뗀 순간. 롱프레스가 이미 발화했으면 탭으로 치지 않는다. </summary>
        private void OnUsePointerUp()
        {
            if (!_isPointerDown) return;
            _isPointerDown = false;
            if (!_longPressTriggered) HandleTap();
        }

        /// <summary>
        /// 짧은 탭 처리.
        ///   · 자동 모드가 켜져 있으면 → 자동 모드를 끄기만 하고 시전하지 않는다(규칙 5).
        ///   · 쿨다운 중이면 → 아무 일도 하지 않는다(서버도 어차피 거부한다 — 규칙 21).
        ///   · 그 외 → 수동 시전 1회.
        /// </summary>
        private void HandleTap()
        {
            if (_currentBuilding == null || _mistShrine == null) return;
            int id = _currentBuilding.Id;

            // 자동 모드 중 탭 = 자동 해제(생산 패널의 동일 분기와 같은 형태).
            if (_mistShrine.GetAutoMode(id))
            {
                HandleToggleAuto();
                return;
            }

            // 쿨다운 중에는 시전하지 않는다. (전용 토스트 문구가 아직 없어 조용히 무시한다 —
            //  남은 시간은 버튼 위 쿨다운 오버레이 숫자로 이미 보이고 있다.)
            if (_mistShrine.IsOnCooldown(id)) return;

            HandleActivate();
        }

        /// <summary>
        /// 실제 시전 요청을 보낸다. 멀티는 서버 RPC로 중계하고, 싱글은 UseCase를 직접 호출한다(서버 권위).
        /// </summary>
        private void HandleActivate()
        {
            if (_currentBuilding == null) return;

            if (_networkMistShrine != null && NetworkContext.IsNetworkActive)
                _networkMistShrine.RequestActivate(_currentBuilding.Id, _currentBuilding.Team);
            else
                _mistShrine?.Activate(_currentBuilding.Id);
        }

        /// <summary>
        /// 자동 모드를 토글한다. 멀티는 서버가 상태를 바꾸고 양 클라에 브로드캐스트하므로 여기서 로컬 변경을 하지 않는다.
        /// </summary>
        private void HandleToggleAuto()
        {
            if (_currentBuilding == null) return;

            if (_networkMistShrine != null && NetworkContext.IsNetworkActive)
            {
                _networkMistShrine.RequestToggleAuto(_currentBuilding.Id, _currentBuilding.Team);
            }
            else
            {
                bool on = _mistShrine?.ToggleAutoMode(_currentBuilding.Id) ?? false;
                // 싱글은 즉시 반영(멀티는 브로드캐스트 수신 후 Update가 반영한다).
                ApplyAutoBorder(on);
            }
        }
    }
}
