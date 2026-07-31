// ============================================================================
// InputHandler.cs
// 마우스/터치 입력을 받아 적절한 UseCase로 전달하는 입력 처리기.
//
// 부착 위치: [Input]/InputHandler
//
// 입력 판정 로직:
//   클릭(탭) vs 드래그를 구분해야 함.
//   - 마우스를 누른 채 일정 거리 이상 움직이면 → 드래그 (카메라 팬)
//   - 마우스를 짧게 누르고 떼면 → 클릭 (타일 선택 / 유닛 이동)
//
// 클릭 시 동작:
//   1. 건물 UI가 열려있으면 → 닫기
//   2. 건물이 있는 타일 → 타일 선택
//   3. 자기 팀 빈 타일 → 건물 배치 팝업
//   4. 기타 → 타일 선택 (하이라이트)
//
// 유닛 이동은 AI 전용 — 플레이어 직접 이동 없음.
//
// New Input System 사용:
//   UnityEngine.InputSystem 패키지의 Mouse, Touchscreen 클래스 사용.
//   레거시 Input 클래스 대신 Mouse.current / Touchscreen.current로 접근.
//
// Presentation 레이어 — Unity 의존.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class InputHandler : MonoBehaviour
    {
        // ====================================================================
        // 외부 의존성 (GameBootstrapper에서 주입)
        // ====================================================================

        /// <summary> 타일 선택 처리 UseCase. </summary>
        private GridInteractionUseCase _gridInteraction;

        /// <summary> 건물 배치 UseCase. </summary>
        private BuildingPlacementUseCase _buildingPlacement;

        /// <summary> 건물 선택 팝업 UI. </summary>
        private BuildingPlacementUI _buildingUI;

        /// <summary> 생산 패널 UI. </summary>
        private ProductionPanelUI _productionUI;

        /// <summary> 비생산 건물(MiningPost/Tower/특수건물) 클릭 시 표시되는 공용 액션 패널. </summary>
        private BuildingActionPanelUI _actionPanelUI;

        /// <summary> 연구소(Research) 클릭 시 표시되는 유닛 강화(연구) 패널. </summary>
        private ResearchPanelUI _researchPanelUI;

        /// <summary> 메인 카메라 참조 (ScreenToWorldPoint 변환용). </summary>
        private Camera _mainCamera;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 마우스 버튼을 누른 시점의 스크린 좌표. 클릭/드래그 판정용. </summary>
        private Vector2 _pointerDownPos;

        /// <summary>
        /// 클릭으로 인정하는 최대 이동 거리 (픽셀).
        /// 이 이상 움직이면 드래그로 간주하여 클릭 무시.
        /// </summary>
        private const float ClickThreshold = 10f;

        /// <summary> XZ 평면 (Y=0). 스크린 좌표 → 월드 좌표 변환에 사용. </summary>
        private static readonly Plane _xzPlane = new Plane(Vector3.up, Vector3.zero);

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 외부 의존성 주입. GameBootstrapper에서 호출.
        /// </summary>
        public void Initialize(
            GridInteractionUseCase gridInteraction,
            Camera mainCamera,
            BuildingPlacementUseCase buildingPlacement,
            BuildingPlacementUI buildingUI,
            ProductionPanelUI productionUI,
            BuildingActionPanelUI actionPanelUI,
            ResearchPanelUI researchPanelUI = null)
        {
            _gridInteraction = gridInteraction;
            _mainCamera = mainCamera;
            _buildingPlacement = buildingPlacement;
            _buildingUI = buildingUI;
            _productionUI = productionUI;
            // 비생산 건물용 공용 액션 패널 — Castle 제외, MiningPost/Tower/특수건물 클릭 시 표시.
            _actionPanelUI = actionPanelUI;
            // 연구소(Research) 클릭 라우팅용 강화 패널 — 없으면(null) 연구소는 기존 액션 패널로 폴백.
            _researchPanelUI = researchPanelUI;
        }

        // ====================================================================
        // 매 프레임 입력 처리
        // ====================================================================

        /// <summary>
        /// 매 프레임 마우스 입력을 확인하여 클릭/드래그를 판정.
        ///
        /// New Input System API:
        ///   Mouse.current          — 현재 마우스 디바이스 (null이면 마우스 미연결)
        ///   .leftButton            — 왼쪽 버튼 컨트롤
        ///   .wasPressedThisFrame   — 이번 프레임에 눌렸는지 (GetMouseButtonDown 대체)
        ///   .wasReleasedThisFrame  — 이번 프레임에 떼졌는지 (GetMouseButtonUp 대체)
        ///   .position.ReadValue()  — 현재 마우스 스크린 좌표 (Input.mousePosition 대체)
        /// </summary>
        private void Update()
        {
            // 의존성 미주입 상태면 무시
            if (_mainCamera == null) return;

            // 마우스 입력 처리 (에디터/PC)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _pointerDownPos = mouse.position.ReadValue();
                }

                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    Vector2 currentPos = mouse.position.ReadValue();
                    float dragDist = Vector2.Distance(_pointerDownPos, currentPos);
                    if (dragDist < ClickThreshold)
                        HandleClick(currentPos);
                }
            }

            // 터치 입력 처리 (모바일)
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var primaryTouch = touchscreen.primaryTouch;

                // 터치 시작 → 위치 기록
                if (primaryTouch.press.wasPressedThisFrame)
                {
                    _pointerDownPos = primaryTouch.position.ReadValue();
                }

                // 터치 끝 → 클릭인지 드래그인지 판정
                if (primaryTouch.press.wasReleasedThisFrame)
                {
                    Vector2 currentPos = primaryTouch.position.ReadValue();
                    float dragDist = Vector2.Distance(_pointerDownPos, currentPos);
                    if (dragDist < ClickThreshold)
                        HandleClick(currentPos);
                }
            }
        }

        // ====================================================================
        // 클릭 처리
        // ====================================================================

        /// <summary>
        /// 클릭된 스크린 좌표를 처리.
        ///
        /// 판정 순서:
        ///   1. 건물 UI가 열려있으면 → 닫기 (팝업 외부 탭)
        ///   2. 건물이 있는 타일 → 타일 선택
        ///   3. 자기 팀 빈 타일 → 건물 배치 팝업
        ///   4. 기타 → 타일 선택 (하이라이트)
        /// </summary>
        private void HandleClick(Vector2 screenPos)
        {
            // --------------------------------------------------------
            // 0.5. 랠리포인트 설정 모드 (UI 체크보다 우선)
            //    팝업이 닫힌 상태에서 타일을 선택해야 하므로
            //    IsPointerOverUI보다 먼저 처리해야 함.
            // --------------------------------------------------------
            if (_productionUI != null && _productionUI.IsSettingRallyPoint
                && Time.frameCount != _productionUI.RallyPointSetFrame)
            {
                // XZ 평면 레이캐스트 → 뷰 좌표 → 도메인 좌표로 역변환
                Vector3 rallyViewPos = ScreenToXZPlane(screenPos);
                Vector3 rallyWorldPos = ViewConverter.FromView(rallyViewPos);
                HexCoord rallyCoord = HexMetrics.WorldToHex(rallyWorldPos);

                _productionUI.CompleteRallyPointSetting(rallyCoord);
                _gridInteraction?.SelectTileAt(rallyWorldPos);
                return;
            }

            // --------------------------------------------------------
            // 0. UI 위 클릭이면 게임 입력 무시 (UI EventSystem이 처리)
            //    New Input System에서는 IsPointerOverGameObject()가
            //    불안정하므로 RaycastAll로 직접 판정.
            //    팝업이 같은 프레임에 닫힌 경우에도 클릭 통과 방지.
            // --------------------------------------------------------
            if (IsPointerOverUI(screenPos))
                return;

            int frame = Time.frameCount;
            // 같은 프레임에 팝업이 닫힌 경우, 그 닫힘 클릭이 다시 타일 선택으로 흘러가지 않도록 차단.
            // 비생산 건물 액션 패널도 동일하게 ClosedFrame 가드 적용.
            if ((_buildingUI != null && _buildingUI.ClosedFrame == frame)
                || (_productionUI != null && _productionUI.ClosedFrame == frame)
                || (_actionPanelUI != null && _actionPanelUI.ClosedFrame == frame))
                return;

            // 스크린 좌표 → XZ 평면 레이캐스트 → 뷰 좌표 → 도메인 월드 좌표로 역변환
            Vector3 viewPos = ScreenToXZPlane(screenPos);
            Vector3 worldPos = ViewConverter.FromView(viewPos);

            // 도메인 월드 좌표 → 헥스 좌표
            HexCoord clickedCoord = HexMetrics.WorldToHex(worldPos);

            // --------------------------------------------------------
            // 2. 건물이 있는 타일 → 종류에 따라 분기
            //    (1) 생산건물(IsProductionBuilding == true)
            //          : 풀스펙 생산 패널(유닛 버튼/큐/업그레이드 등) 표시
            //    (2) 비생산건물 중 Castle 제외 (CanShowActionPanel == true)
            //          : 공용 액션 패널(건물 이름 + 철거) 표시
            //    (3) Castle 등 액션 불가 건물
            //          : 어떤 팝업도 띄우지 않음 (클릭 무반응)
            //
            //    자기 팀이 아니거나 사망 상태(IsAlive == false)면 어느 팝업도 띄우지 않는다.
            // --------------------------------------------------------
            if (_buildingPlacement != null)
            {
                BuildingData buildingAtPos = _buildingPlacement.GetBuildingAt(clickedCoord);
                if (buildingAtPos != null)
                {
                    // 자기 팀 건물인지 + 살아있는지 사전 검사. 적 건물 클릭은 기존처럼 팝업 미표시.
                    bool isMine = buildingAtPos.Team == LocalPlayerTeam.Current;
                    bool isAlive = buildingAtPos.IsAlive;

                    if (isMine && isAlive)
                    {
                        if (BuildingTypeHelper.IsProductionBuilding(buildingAtPos.Type)
                            && _productionUI != null)
                        {
                            // (1) 생산 건물 → 생산 패널 (유닛 버튼, 큐, 업그레이드 포함 풀스펙)
                            _productionUI.Show(buildingAtPos);
                        }
                        else if (buildingAtPos.Type == BuildingType.Research
                            && _researchPanelUI != null)
                        {
                            // (2) 연구소(Research) → 유닛 강화(연구) 패널.
                            //   연구는 특정 연구소 종속이 아니라 팀 트랙 단위지만, 착수한 연구소 Id는
                            //   파괴 시 취소·환불 기준으로 기록되므로 클릭한 건물을 그대로 넘긴다.
                            //   (_researchPanelUI 미배선 시엔 아래 액션 패널 분기로 폴백된다.)
                            _researchPanelUI.Open(buildingAtPos);
                        }
                        else if (BuildingTypeHelper.CanShowActionPanel(buildingAtPos.Type)
                            && _actionPanelUI != null)
                        {
                            // (3) 비생산 건물 (Castle 제외) → 공용 액션 패널 (건물 이름 + 철거)
                            _actionPanelUI.Show(buildingAtPos);
                        }
                        // (4) Castle: CanShowActionPanel이 false 반환 → 어느 분기도 해당 없음 → 클릭 무반응
                    }
                    // 적 건물 클릭: 어떤 팝업도 띄우지 않음 (기존 동작 유지)

                    _gridInteraction?.SelectTileAt(worldPos);
                    return;
                }
            }

            // --------------------------------------------------------
            // 3. 금광 타일 (건물 없음) → 채굴소 건설 팝업
            // --------------------------------------------------------
            if (_buildingPlacement != null &&
                _buildingPlacement.CanPlaceMiningPost(clickedCoord, LocalPlayerTeam.Current))
            {
                _buildingUI?.Show(clickedCoord, LocalPlayerTeam.Current);
                _gridInteraction?.SelectTileAt(worldPos);
                return;
            }

            // --------------------------------------------------------
            // 4. 자기 팀 빈 타일 → 건물 배치 팝업
            // --------------------------------------------------------
            if (_buildingPlacement != null &&
                _buildingPlacement.CanPlaceBuilding(clickedCoord, LocalPlayerTeam.Current))
            {
                _buildingUI?.Show(clickedCoord, LocalPlayerTeam.Current);
                _gridInteraction?.SelectTileAt(worldPos);
                return;
            }

            // --------------------------------------------------------
            // 5. 기타 → 타일 선택 (하이라이트)
            // --------------------------------------------------------
            _gridInteraction?.SelectTileAt(worldPos);
        }

        // ====================================================================
        // UI 히트 판정
        // ====================================================================

        /// <summary>
        /// 스크린 좌표가 UI 요소 위에 있는지 판정.
        /// New Input System에서 IsPointerOverGameObject()가 불안정하므로
        /// EventSystem.RaycastAll을 사용하여 직접 판정.
        /// </summary>
        private bool IsPointerOverUI(Vector2 screenPos)
        {
            if (EventSystem.current == null)
            {
                Debug.Log("[InputHandler] EventSystem.current == null");
                return false;
            }

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPos
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        // ====================================================================
        // XZ 평면 레이캐스트
        // ====================================================================

        /// <summary>
        /// 스크린 좌표 → XZ 평면(Y=0) 위의 월드 좌표로 변환.
        /// 카메라 레이를 XZ 평면에 교차시켜 정확한 월드 위치를 얻음.
        /// 기존 ScreenToWorldPoint 방식을 대체 (3D 카메라 대응).
        /// </summary>
        private Vector3 ScreenToXZPlane(Vector2 screenPos)
        {
            Ray ray = _mainCamera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            if (_xzPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            // 레이캐스트 실패 시 폴백 (극단적 카메라 각도)
            return Vector3.zero;
        }
    }
}
