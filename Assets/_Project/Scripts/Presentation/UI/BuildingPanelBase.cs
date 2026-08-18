// ============================================================================
// BuildingPanelBase.cs
// 건물 클릭 시 표시되는 패널 UI의 공통 베이스 클래스.
//
// 왜 베이스 클래스인가?
//   ProductionPanelUI(생산건물용 풀스펙 UI)와 BuildingActionPanelUI(비생산건물용
//   간이 UI)는 다음 요소를 공유한다.
//     - 팝업 등장/사라짐 애니메이션 (AnimatedPanel)
//     - 외부 탭 닫기 (SharedBackgroundButton)
//     - 헤더에 건물 이름 표시
//     - 닫기(X) 버튼
//     - 철거 버튼 + 환불 금액 표시
//   이 요소들을 한 곳에서 관리하여 두 UI가 동일하게 동작하도록 보장한다.
//
// 상속 측이 해야 할 일:
//   - Initialize(...) 메서드에서 InitializeBase(...) 호출
//   - 필요하면 OnShow(building) / OnBeforeClose() / BeforeDemolish() 훅을 오버라이드
//   - 이 베이스가 정의한 SerializeField는 Inspector에서 자식 객체로 연결
//
// 생명주기 흐름:
//   Show(building)
//     -> _currentBuilding 저장
//     -> 헤더 텍스트 갱신
//     -> AnimatedPanel.Show()
//     -> SharedBackgroundButton.Register(Close)
//     -> 환불 금액 텍스트 갱신
//     -> OnShow(building)  ← 자식 클래스 커스텀 처리
//
//   Close()
//     -> OnBeforeClose()   ← 자식 클래스 커스텀 정리
//     -> ClosedFrame 기록 (같은 프레임 입력 처리 방지)
//     -> SharedBackgroundButton.Unregister()
//     -> AnimatedPanel.Hide()
//     -> _currentBuilding = null
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 건물 패널 UI의 공통 베이스. 헤더/닫기/철거/환불 표시를 공유한다.
    /// 상속받는 클래스는 OnShow/OnBeforeClose/BeforeDemolish 훅을 오버라이드해
    /// 각자의 추가 로직을 수행한다.
    /// </summary>
    public abstract class BuildingPanelBase : MonoBehaviour, IGameUI
    {
        // ====================================================================
        // 직렬화 필드 (Inspector 연결 — 자식 UI 요소들)
        // ====================================================================

        [Header("Popup")]
        [Tooltip("팝업 등장/사라짐 애니메이션을 담당하는 컴포넌트.")]
        [SerializeField] protected AnimatedPanel _popup;

        [Header("Header")]
        [Tooltip("팝업 상단에 건물 이름을 표시. Show() 시 BuildingType.ToString()으로 갱신.")]
        [SerializeField] protected TextMeshProUGUI _headerText;

        [Header("Buttons")]
        [Tooltip("팝업 닫기 버튼 (X 버튼).")]
        [SerializeField] protected Button _cancelButton;

        [Header("Demolish")]
        [Tooltip("철거 버튼. 클릭 시 환불 후 건물 제거.")]
        [SerializeField] protected Button _demolishButton;

        [Tooltip("철거 시 받게 될 골드 환불액 텍스트.")]
        [SerializeField] protected TextMeshProUGUI _demolishRefundText;

        [Header("색상 설정")]
        [Tooltip("프로젝트 공용 UI 색상 설정 에셋. Resources/Config/UIColorConfig.asset 을 연결. " +
                 "철거 환불 텍스트와 자식 클래스의 비용 텍스트 색상이 이 에셋에서 결정된다. " +
                 "protected이므로 ProductionPanelUI / BuildingActionPanelUI 의 Inspector에도 동일 슬롯이 노출된다.")]
        [SerializeField] protected UIColorConfig _colorConfig;

        // ====================================================================
        // 의존성 (자식 클래스의 Initialize 호출 시 주입)
        // ====================================================================

        /// <summary>건물 도메인 상태 제거(철거)에 사용.</summary>
        protected BuildingPlacementUseCase _buildingPlacement;

        /// <summary>철거 환불 골드 지급에 사용.</summary>
        protected ResourceUseCase _resource;

        /// <summary>멀티플레이에서 철거 요청을 서버로 전송할 때 사용. 싱글플레이는 null.</summary>
        protected NetworkBuildingController _networkBuildingController;

        /// <summary>현재 패널이 표시 중인 건물 데이터. Close() 시 null로 초기화된다.</summary>
        protected BuildingData _currentBuilding;

        /// <summary>
        /// 건물 사망 이벤트(GameEvents.OnBuildingDied)를 이미 구독했는지 여부(중복 구독 방지 가드).
        ///
        /// 왜 IDisposable을 따로 보관하지 않나?
        ///   이 클래스는 MonoBehaviour이므로, 구독을 UniRx의 .AddTo(this)로
        ///   "이 컴포넌트의 수명"에 묶는다. 그러면 GameObject가 파괴될 때 구독이
        ///   자동으로 해제되어(별도 OnDestroy/Dispose 불필요) 누수가 생기지 않는다.
        ///   (이 프로젝트의 다른 MonoBehaviour들 — BuildingFactory/UnitFactory/
        ///    HitPresentationQueue 등 — 도 동일하게 .AddTo(this)를 사용한다.)
        ///
        ///   참고: 여기서 직접 OnDestroy를 정의하지 않는 이유는, 자식 중
        ///   ResearchPanelUI가 이미 자체 OnDestroy를 갖고 있어 베이스의 OnDestroy를
        ///   숨겨 버리기 때문이다(그 경우 베이스 정리가 실행되지 않는다).
        ///   .AddTo(this)는 이런 메서드 숨김과 무관하게 동작하므로 더 안전하다.
        ///
        /// InitializeBase가 자식마다 1회만 호출되는 것이 정상이지만,
        /// 방어적으로 이 플래그가 true면 재구독을 건너뛴다.
        /// </summary>
        private bool _buildingDiedSubscribed;

        // ====================================================================
        // 공개 상태 프로퍼티
        // ====================================================================

        /// <summary>팝업이 현재 열려 있는지 여부 (AnimatedPanel 상태 기준).</summary>
        public bool IsOpen => _popup != null && _popup.IsVisible;

        /// <summary>
        /// 팝업이 닫힌 시점의 Time.frameCount.
        /// InputHandler가 같은 프레임에 발생한 클릭을 무시할 때 사용한다.
        /// 한 번도 닫힌 적이 없으면 -1.
        /// </summary>
        public int ClosedFrame { get; protected set; } = -1;

        /// <summary>현재 패널이 보여주고 있는 건물의 Id. 패널이 닫혀 있으면 -1.</summary>
        public int CurrentBuildingId => _currentBuilding?.Id ?? -1;

        // ====================================================================
        // 초기화 — 자식 클래스의 Initialize에서 호출
        // ====================================================================

        /// <summary>
        /// 베이스 의존성 주입 + 공통 버튼 이벤트 연결.
        /// 자식 클래스의 Initialize(...) 가장 첫 줄에서 호출해야 한다.
        /// </summary>
        /// <param name="buildingPlacement">건물 도메인 상태 제어용 UseCase.</param>
        /// <param name="resource">자원(골드) 관리 UseCase.</param>
        /// <param name="networkBuildingController">멀티플레이 모드일 때만 주입. 싱글플레이는 null.</param>
        protected void InitializeBase(
            BuildingPlacementUseCase buildingPlacement,
            ResourceUseCase resource,
            NetworkBuildingController networkBuildingController)
        {
            _buildingPlacement = buildingPlacement;
            _resource = resource;
            _networkBuildingController = networkBuildingController;

            // 닫기 버튼 → Close() 연결
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);

            // 철거 버튼 → OnDemolishButtonClick() 연결
            if (_demolishButton != null)
                _demolishButton.onClick.AddListener(OnDemolishButtonClick);

            // 건물 사망 이벤트 구독 (중복 구독 방지 가드)
            //
            // 목적: 이 패널이 표시/조준 중인 건물이 파괴되면 패널을 자동으로 닫는다.
            //       (생산/액션/스킬/연구 패널 4종이 모두 이 베이스를 상속하므로
            //        여기 한 곳에서 구독하면 4종 전부가 커버된다.)
            //
            // 가드: 이미 구독했다면 재구독하지 않는다.
            //       InitializeBase가 실수로 두 번 호출되더라도 구독이 겹쳐 쌓이는 것을 막는다.
            // .AddTo(this): 이 컴포넌트(GameObject)가 파괴되면 구독을 자동 해제(누수 방지).
            if (!_buildingDiedSubscribed)
            {
                GameEvents.OnBuildingDied
                    .Subscribe(OnBuildingDied)
                    .AddTo(this);
                _buildingDiedSubscribed = true;
            }
        }

        // ====================================================================
        // Show / OnShow 훅
        // ====================================================================

        /// <summary>
        /// 패널을 연다. InputHandler에서 건물 클릭 시 호출.
        /// 헤더 텍스트와 환불 금액을 갱신한 뒤 자식 클래스의 OnShow를 호출한다.
        /// </summary>
        /// <param name="building">패널이 표시할 대상 건물 데이터.</param>
        public virtual void Show(BuildingData building)
        {
            _currentBuilding = building;

            // 헤더에 건물 이름 표시 (예: BuildingType.Garage -> "Garage")
            if (_headerText != null)
                _headerText.text = building.Type.ToString();

            // 팝업 등장 애니메이션
            _popup?.Show();

            // 외부 탭 닫기 콜백 등록 (Close 호출되도록)
            // UIManager 공유 BlockingOverlay를 Popup 모드로 표시(터치 시 Close 호출).
            UIManager.Instance?.ShowBlockingOverlay(Close);

            // 환불 금액 텍스트 갱신
            // 팀에 따라 해당 종족의 누적 투자 비용을 조회한다.
            RaceId race = (building.Team == TeamId.Blue)
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;
            UpdateDemolishRefund(race);

            // 자식 클래스 커스텀 처리 (생산 패널의 유닛 버튼 바인딩 등)
            OnShow(building);
        }

        /// <summary>
        /// 자식 클래스 전용 Show 훅. 기본 동작 없음.
        /// 생산 패널 등은 이 메서드를 오버라이드해 유닛 버튼/큐를 갱신한다.
        /// </summary>
        protected virtual void OnShow(BuildingData building) { }

        // ====================================================================
        // Close / OnBeforeClose 훅
        // ====================================================================

        /// <summary>
        /// 패널을 닫는다. 닫기 버튼/외부 탭/철거 후/게임 종료 시 호출.
        /// 자식 클래스의 OnBeforeClose 훅이 먼저 실행되어 정리할 기회를 갖는다.
        /// </summary>
        public virtual void Close()
        {
            // 자식 클래스 커스텀 정리 (랠리 마커 숨김, 모드 플래그 초기화 등)
            OnBeforeClose();

            // 같은 프레임에 발생할 수 있는 클릭이 빈 타일 선택으로 흐르는 것을 막기 위해
            // 닫힌 프레임 번호를 기록한다. InputHandler가 이 값을 참조한다.
            ClosedFrame = Time.frameCount;

            // 외부 탭 닫기 콜백 해제
            // UIManager 공유 BlockingOverlay 숨김.
            UIManager.Instance?.HideBlockingOverlay();

            // 팝업 사라짐 애니메이션
            _popup?.Hide();

            // 현재 건물 참조 정리
            _currentBuilding = null;
        }

        /// <summary>
        /// 자식 클래스 전용 Close 훅. 기본 동작 없음.
        /// 생산 패널 등은 이 메서드를 오버라이드해 랠리 모드/마커를 정리한다.
        /// </summary>
        protected virtual void OnBeforeClose() { }

        // ====================================================================
        // 환불 금액 텍스트 갱신
        // ====================================================================

        /// <summary>
        /// 철거 버튼 옆 환불 금액 텍스트를 갱신한다.
        /// 환불 금액 = (1단계 건설비 + 누적 업그레이드 비용) / 2.
        /// 누적 값은 GameBootstrapper 초기화 시점에 BuildingStats에 캐싱된다.
        /// </summary>
        protected void UpdateDemolishRefund(RaceId race)
        {
            if (_demolishRefundText == null || _currentBuilding == null) return;

            int totalInvested = BuildingStats.GetTotalInvestedCost(_currentBuilding.Type, race);
            int refund = totalInvested / 2;

            _demolishRefundText.text = $"{refund}";
            // 환불은 양의 자원 회수이므로 시각적으로 초록색 톤으로 표시한다.
            // _colorConfig가 연결되어 있으면 그 값을, 미연결 시 Color.green 폴백을 사용.
            _demolishRefundText.color = _colorConfig?.demolishRefundColor ?? Color.green;
        }

        // ====================================================================
        // 철거 처리
        // ====================================================================

        /// <summary>
        /// 철거 버튼 클릭 핸들러.
        ///
        /// 처리 흐름:
        ///   1) 현재 건물 데이터가 없으면 무시
        ///   2) 멀티플레이면 RequestDemolishServerRpc 송신 (서버에서 검증·실행·동기화)
        ///   3) 싱글플레이면 BeforeDemolish 훅 -> 골드 환불 -> DemolishBuilding 호출
        ///   4) Close() 로 패널 닫기
        ///
        /// 자식 클래스가 추가로 정리할 작업이 있으면(예: 생산 큐 취소) BeforeDemolish 훅을 사용한다.
        /// </summary>
        protected virtual void OnDemolishButtonClick()
        {
            if (_currentBuilding == null) return;

            // 멀티플레이 여부 — Application 레이어의 NetworkContext + 컨트롤러 주입 여부로 판단.
            // (이전: NetworkManager.Singleton.IsListening 직접 호출 — Presentation의 NGO 직접 의존)
            bool isNetworkMode = _networkBuildingController != null
                && NetworkContext.IsNetworkActive;

            if (isNetworkMode)
            {
                // 서버가 검증/큐취소/환불/철거/동기화를 모두 담당한다.
                // ServerRpc 직접 호출 대신 일반 래퍼(RequestDemolish) 사용으로 결합도 감소.
                _networkBuildingController.RequestDemolish(_currentBuilding.Id);
            }
            else
            {
                // 싱글플레이: 직접 모두 처리한다.
                // 1) 자식 클래스 커스텀 사전 정리 (예: 생산 큐 전체 취소 + 자체 환불)
                BeforeDemolish();

                // 2) 누적 투자 비용의 50% 골드 환불
                if (_resource != null)
                {
                    RaceId race = (_currentBuilding.Team == TeamId.Blue)
                        ? GameRaceContext.BlueRace
                        : GameRaceContext.RedRace;
                    int totalInvested = BuildingStats.GetTotalInvestedCost(_currentBuilding.Type, race);
                    int refund = totalInvested / 2;
                    _resource.AddGold(_currentBuilding.Team, refund);
                }

                // 3) 건물 도메인 상태 제거
                // DemolishBuilding = OnBuildingDied 발행(BuildingFactory가 GO 제거)
                //                  + RemoveBuilding(도메인 딕셔너리 제거 + 타일 복구)
                _buildingPlacement?.DemolishBuilding(_currentBuilding.Id);
            }

            // 건물이 사라지므로 패널을 유지할 이유가 없음
            Close();
        }

        /// <summary>
        /// 자식 클래스 전용 철거 사전 훅. 기본 동작 없음.
        /// 생산 패널은 이 훅에서 생산 큐 전체를 취소(이미 차감된 골드 환불 포함)한다.
        /// 멀티플레이 경로에서는 호출되지 않는다 — 서버가 동일 작업을 수행하기 때문.
        /// </summary>
        protected virtual void BeforeDemolish() { }

        // ====================================================================
        // IGameUI — 게임 시작/종료 시 패널을 닫는다
        // ====================================================================

        /// <summary>게임 시작 시 호출. 직전 게임에서 열려 있던 패널이 남지 않도록 닫는다.</summary>
        public virtual void OnGameStarted() => Close();

        /// <summary>게임 종료(승패 결정) 시 호출. 결과 화면이 표시되는 동안 패널을 닫는다.</summary>
        public virtual void OnGameEnded() => Close();

        // ====================================================================
        // 건물 사망 → 열린 패널/조준 UI 원복
        // ====================================================================

        /// <summary>
        /// 건물이 파괴/철거되었을 때 호출되는 핸들러 (GameEvents.OnBuildingDied 구독).
        ///
        /// 하는 일:
        ///   죽은 건물이 "지금 이 패널이 다루고 있는 바로 그 건물"이면 패널을 닫는다.
        ///
        /// 왜 IsOpen이 아니라 _currentBuilding.Id로 판정하나?
        ///   스킬 패널은 지점 조준 모드로 들어갈 때 팝업을 잠시 숨긴다(_popup.Hide()).
        ///   그러면 IsOpen(=팝업 가시성 기준)이 false가 되지만, _currentBuilding은
        ///   여전히 조준 대상 건물을 가리킨다. IsOpen으로 걸면 이 "조준 중" 케이스를
        ///   놓치므로, 화면 표시 여부와 무관한 _currentBuilding.Id 매칭으로 판정한다.
        ///   (패널이 완전히 닫혀 있으면 _currentBuilding == null이라 매칭이 자연히
        ///    실패하므로, 남의 건물이 죽어도 엉뚱하게 닫히지 않는다.)
        ///
        /// 왜 Close()만 불러도 조준까지 취소되나?
        ///   Close()는 맨 처음 OnBeforeClose()를 호출한다. 스킬 패널은 이 훅에서
        ///   _skillAimController.CancelAim()을, 생산 패널은 랠리 마커 숨김을 수행한다.
        ///   따라서 베이스에서 Close()만 호출해도 각 패널의 정리가 자동으로 따라온다.
        /// </summary>
        /// <param name="e">사망한 건물 정보가 담긴 이벤트.</param>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            // 죽은 건물이 있고, 이 패널이 현재 어떤 건물을 다루는 중이며,
            // 그 둘의 Id가 같을 때만 닫는다. (셋 중 하나라도 아니면 아무 동작 없음)
            if (e.Building != null
                && _currentBuilding != null
                && e.Building.Id == _currentBuilding.Id)
            {
                Close();
            }
        }
    }
}
