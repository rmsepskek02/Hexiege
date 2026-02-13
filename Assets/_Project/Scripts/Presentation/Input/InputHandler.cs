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
//   2. 클릭 위치에 유닛이 있으면 → 유닛 선택
//   3. 유닛이 선택된 상태에서 빈 타일 클릭 → 이동 명령
//   4. 건물이 있는 타일 → 타일 선택
//   5. 자기 팀 빈 타일 → 건물 배치 팝업
//   6. 기타 → 타일 선택 (하이라이트)
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

namespace Hexiege.Presentation
{
    public class InputHandler : MonoBehaviour
    {
        // ====================================================================
        // 외부 의존성 (GameBootstrapper에서 주입)
        // ====================================================================

        /// <summary> 타일 선택 처리 UseCase. </summary>
        private GridInteractionUseCase _gridInteraction;

        /// <summary> 유닛 이동 처리 UseCase. </summary>
        private UnitMovementUseCase _unitMovement;

        /// <summary> 유닛 생성/조회 UseCase. </summary>
        private UnitSpawnUseCase _unitSpawn;

        /// <summary> 건물 배치 UseCase. </summary>
        private BuildingPlacementUseCase _buildingPlacement;

        /// <summary> 건물 선택 팝업 UI. </summary>
        private BuildingPlacementUI _buildingUI;

        /// <summary> 메인 카메라 참조 (ScreenToWorldPoint 변환용). </summary>
        private Camera _mainCamera;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 현재 선택된 유닛. null이면 유닛 미선택 상태. </summary>
        private UnitData _selectedUnit;

        /// <summary> 마우스 버튼을 누른 시점의 스크린 좌표. 클릭/드래그 판정용. </summary>
        private Vector2 _pointerDownPos;

        /// <summary>
        /// 클릭으로 인정하는 최대 이동 거리 (픽셀).
        /// 이 이상 움직이면 드래그로 간주하여 클릭 무시.
        /// </summary>
        private const float ClickThreshold = 10f;

        /// <summary> 자동 이동 모드 여부. T키로 토글. </summary>
        private bool _autoMoveMode;

        /// <summary> 자동 이동이 이미 실행되었는지 (중복 실행 방지). </summary>
        private bool _autoMoveStarted;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 외부 의존성 주입. GameBootstrapper에서 호출.
        /// </summary>
        public void Initialize(
            GridInteractionUseCase gridInteraction,
            UnitMovementUseCase unitMovement,
            UnitSpawnUseCase unitSpawn,
            Camera mainCamera,
            BuildingPlacementUseCase buildingPlacement,
            BuildingPlacementUI buildingUI)
        {
            _gridInteraction = gridInteraction;
            _unitMovement = unitMovement;
            _unitSpawn = unitSpawn;
            _mainCamera = mainCamera;
            _buildingPlacement = buildingPlacement;
            _buildingUI = buildingUI;
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

            // T키: 자동/수동 이동 모드 토글
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                _autoMoveMode = !_autoMoveMode;
                _autoMoveStarted = false;
                Debug.Log($"[InputHandler] 이동 모드 변경: {(_autoMoveMode ? "자동 (Auto)" : "수동 (Manual)")}");
            }

            // 자동 이동 모드: 모든 Blue 유닛을 적 Castle 방향으로 자동 이동
            if (_autoMoveMode && !_autoMoveStarted)
            {
                StartAutoMove();
                _autoMoveStarted = true;
            }

            // 수동 모드에서만 마우스 입력 처리
            if (_autoMoveMode) return;

            // 마우스가 연결되어 있지 않으면 무시
            var mouse = Mouse.current;
            if (mouse == null) return;

            // 마우스 버튼 누름 → 위치 기록
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _pointerDownPos = mouse.position.ReadValue();
            }

            // 마우스 버튼 뗌 → 클릭인지 드래그인지 판정
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                Vector2 currentPos = mouse.position.ReadValue();
                float dragDist = Vector2.Distance(_pointerDownPos, currentPos);

                // 이동 거리가 임계값 이하면 클릭으로 처리
                if (dragDist < ClickThreshold)
                {
                    HandleClick(currentPos);
                }
                // 초과하면 드래그 → CameraController가 처리하므로 여기서는 무시
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
        ///   2. 클릭 위치에 유닛이 있는가? → 유닛 선택
        ///   3. 유닛이 선택된 상태에서 빈 타일 클릭? → 이동 명령
        ///   4. 건물이 있는 타일 → 타일 선택
        ///   5. 자기 팀 빈 타일 → 건물 배치 팝업
        ///   6. 기타 → 타일 선택 (하이라이트)
        /// </summary>
        private void HandleClick(Vector2 screenPos)
        {
            // --------------------------------------------------------
            // 0. UI 위 클릭이면 게임 입력 무시 (UI EventSystem이 처리)
            //    New Input System에서는 IsPointerOverGameObject()가
            //    불안정하므로 RaycastAll로 직접 판정.
            // --------------------------------------------------------
            if (IsPointerOverUI(screenPos))
                return;

            // 스크린 좌표 → 월드 좌표 변환
            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, 0f));
            worldPos.z = 0f; // 2D이므로 z=0

            // 월드 좌표 → 헥스 좌표
            HexCoord clickedCoord = Core.HexMetrics.WorldToHex(worldPos);

            // --------------------------------------------------------
            // 2. 클릭 위치에 유닛이 있는지 확인
            // --------------------------------------------------------
            UnitData unitAtPos = _unitSpawn?.GetUnitAt(clickedCoord);

            if (unitAtPos != null)
            {
                // 유닛 선택 (이전 선택 해제)
                _selectedUnit = unitAtPos;
                _gridInteraction?.SelectTileAt(worldPos);
                return;
            }

            // --------------------------------------------------------
            // 3. 유닛이 선택된 상태에서 빈 타일 클릭 → 이동 명령
            // --------------------------------------------------------
            if (_selectedUnit != null)
            {
                // 선택된 유닛의 UnitView 찾기
                var unitObj = FindObjectsByType<UnitView>(FindObjectsSortMode.None);
                UnitView selectedView = null;
                foreach (var view in unitObj)
                {
                    if (view.Data != null && view.Data.Id == _selectedUnit.Id)
                    {
                        selectedView = view;
                        break;
                    }
                }

                // UnitView가 있고, 이동 중이 아닐 때만 이동 명령
                if (selectedView != null && !selectedView.IsMoving)
                {
                    List<HexCoord> path = _unitMovement?.RequestMove(_selectedUnit, clickedCoord);
                    if (path != null)
                    {
                        selectedView.MoveTo(path);
                        _selectedUnit = null; // 이동 시작 후 선택 해제
                        _gridInteraction?.Deselect();
                        return;
                    }
                }

                // 이동 불가 (경로 없음) → 선택 해제하고 타일 선택으로 전환
                _selectedUnit = null;
            }

            // --------------------------------------------------------
            // 4. 건물이 있는 타일 → 타일 선택
            // --------------------------------------------------------
            if (_buildingPlacement != null)
            {
                BuildingData buildingAtPos = _buildingPlacement.GetBuildingAt(clickedCoord);
                if (buildingAtPos != null)
                {
                    _gridInteraction?.SelectTileAt(worldPos);
                    return;
                }
            }

            // --------------------------------------------------------
            // 5. 자기 팀 빈 타일 → 건물 배치 팝업
            // --------------------------------------------------------
            if (_buildingPlacement != null &&
                _buildingPlacement.CanPlaceBuilding(clickedCoord, TeamId.Blue))
            {
                _buildingUI?.Show(clickedCoord, TeamId.Blue);
                _gridInteraction?.SelectTileAt(worldPos);
                return;
            }

            // --------------------------------------------------------
            // 6. 기타 → 타일 선택 (하이라이트)
            // --------------------------------------------------------
            _gridInteraction?.SelectTileAt(worldPos);
        }

        // ====================================================================
        // 자동 이동
        // ====================================================================

        /// <summary>
        /// 양 팀 모든 유닛을 상대 Castle 방향으로 자동 이동시킴.
        /// 건물 타일은 이동 불가이므로 Castle 인접 타일까지 이동.
        /// </summary>
        private void StartAutoMove()
        {
            if (_unitSpawn == null || _unitMovement == null || _buildingPlacement == null)
                return;

            // 각 팀의 Castle 위치 찾기
            HexCoord? blueCastlePos = null;
            HexCoord? redCastlePos = null;
            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building.Type != BuildingType.Castle || !building.IsAlive) continue;
                if (building.Team == TeamId.Blue) blueCastlePos = building.Position;
                else if (building.Team == TeamId.Red) redCastlePos = building.Position;
            }

            // 모든 유닛에게 상대 Castle 방향으로 이동 명령
            var unitViews = FindObjectsByType<UnitView>(FindObjectsSortMode.None);
            foreach (var view in unitViews)
            {
                if (view.Data == null || !view.Data.IsAlive) continue;
                if (view.IsMoving) continue;

                // 이 유닛의 상대 Castle 위치 결정
                HexCoord? targetCastle = (view.Data.Team == TeamId.Blue) ? redCastlePos : blueCastlePos;
                if (!targetCastle.HasValue) continue;

                // Castle 인접 타일 중 가장 가까운 곳을 목표로 설정
                HexCoord target = FindClosestWalkableNeighbor(view.Data.Position, targetCastle.Value);
                if (target == view.Data.Position) continue; // 이미 인접

                List<HexCoord> path = _unitMovement.RequestMove(view.Data, target);
                if (path != null)
                {
                    view.MoveTo(path);
                    Debug.Log($"[AutoMove] {view.Data.Team} Unit {view.Data.Id} → {target}");
                }
            }
        }

        /// <summary>
        /// 목표 좌표의 인접 6타일 중 출발지에서 가장 가까운 이동 가능 타일 반환.
        /// 건물 타일은 이동 불가이므로 인접 타일을 목표로 사용.
        /// </summary>
        private HexCoord FindClosestWalkableNeighbor(HexCoord from, HexCoord target)
        {
            HexCoord best = target;
            int bestDist = int.MaxValue;

            for (int i = 0; i < HexDirectionExtensions.Count; i++)
            {
                HexCoord neighbor = ((HexDirection)i).Neighbor(target);
                int dist = HexCoord.Distance(from, neighbor);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = neighbor;
                }
            }

            return best;
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
            Debug.Log($"[InputHandler] IsPointerOverUI pos={screenPos}, hits={results.Count}");
            for (int i = 0; i < results.Count; i++)
                Debug.Log($"  hit[{i}]: {results[i].gameObject.name}");
            return results.Count > 0;
        }
    }
}
