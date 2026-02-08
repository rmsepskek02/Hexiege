// ============================================================================
// BuildingPlacementUI.cs
// 자기 팀 빈 타일을 탭했을 때 건물 선택 팝업을 표시하는 UI 컴포넌트.
//
// 인터랙션 흐름:
//   1. InputHandler가 자기 팀 빈 타일 탭 감지
//   2. BuildingPlacementUI.Show(coord, team) 호출 → 팝업 표시
//   3. 플레이어가 배럭/채굴소 버튼 탭 → PlaceAndClose() → 건물 배치 + 팝업 닫기
//   4. Background 터치, CancelButton → Close()
//
// Castle은 게임 시작 시 자동 배치되므로 이 UI에 포함하지 않음.
//
// UI 계층 구조 (에디터에서 생성):
//   [UI] (Canvas - Screen Space Overlay)
//     ├─ BuildingPopup (_popup → 이 하나만 토글)
//     │   ├─ Background (전체화면 검은 오버레이 + Button → Close)
//     │   ├─ CancelButton
//     │   └─ BuildingPanel
//     │       └─ VerticalButtons (Vertical Layout Group)
//     │           ├─ Buttons1 (Horizontal Layout) — Castle, Barracks, MiningPost
//     │           └─ Buttons2 (Horizontal Layout) — Castle, Barracks, MiningPost
//     └─ EventSystem
//
// BuildingPopup 래퍼 하나를 SetActive 토글하면 하위 전체(Background + Panel)가 함께 제어됨.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, UI).
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    public class BuildingPlacementUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector에서 설정할 UI 참조
        // ====================================================================

        [Header("UI References")]
        [Tooltip("팝업 래퍼 (Background + BuildingPanel 포함, 활성/비활성 토글)")]
        [SerializeField] private GameObject _popup;

        [Tooltip("배경 버튼 (터치 시 팝업 닫기)")]
        [SerializeField] private Button _backgroundButton;

        [Tooltip("배럭 건설 버튼")]
        [SerializeField] private Button _barracksButton;

        [Tooltip("채굴소 건설 버튼")]
        [SerializeField] private Button _miningPostButton;

        [Tooltip("취소 버튼")]
        [SerializeField] private Button _cancelButton;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 건물 배치 UseCase 참조. </summary>
        private BuildingPlacementUseCase _buildingPlacement;

        /// <summary> 현재 선택된 타일 좌표 (팝업 표시 중). </summary>
        private HexCoord _targetCoord;

        /// <summary> 현재 배치 중인 팀. </summary>
        private TeamId _currentTeam;

        /// <summary> 팝업이 열려있는지 여부. InputHandler에서 확인. </summary>
        public bool IsOpen => _popup != null && _popup.activeSelf;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. UseCase 참조 설정 및 버튼 이벤트 연결.
        /// </summary>
        public void Initialize(BuildingPlacementUseCase buildingPlacement)
        {
            _buildingPlacement = buildingPlacement;

            // 시작 시 팝업 비활성
            if (_popup != null)
                _popup.SetActive(false);

            // 버튼 이벤트 연결
            if (_backgroundButton != null)
                _backgroundButton.onClick.AddListener(Close);

            if (_barracksButton != null)
                _barracksButton.onClick.AddListener(() => PlaceAndClose(BuildingType.Barracks));

            if (_miningPostButton != null)
                _miningPostButton.onClick.AddListener(() => PlaceAndClose(BuildingType.MiningPost));

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);
        }

        // ====================================================================
        // 팝업 표시/닫기
        // ====================================================================

        /// <summary>
        /// 건물 선택 팝업 표시. InputHandler에서 호출.
        /// </summary>
        /// <param name="coord">건물을 배치할 타일 좌표</param>
        /// <param name="team">배치하는 팀</param>
        public void Show(HexCoord coord, TeamId team)
        {
            _targetCoord = coord;
            _currentTeam = team;

            if (_popup != null)
                _popup.SetActive(true);
        }

        /// <summary>
        /// 팝업 닫기. Background 터치, 취소 버튼, 또는 건물 배치 후 호출.
        /// </summary>
        public void Close()
        {
            Debug.Log($"[BuildingUI] Close() 호출됨\n{System.Environment.StackTrace}");
            if (_popup != null)
                _popup.SetActive(false);
        }

        // ====================================================================
        // 건물 배치
        // ====================================================================

        /// <summary>
        /// 선택된 건물 타입을 배치하고 팝업 닫기.
        /// </summary>
        private void PlaceAndClose(BuildingType type)
        {
            _buildingPlacement?.PlaceBuilding(type, _currentTeam, _targetCoord);
            Close();
        }
    }
}
