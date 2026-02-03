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
// 클릭 시 동작 (프로토타입):
//   1. 클릭 위치에 유닛이 있으면 → 유닛 선택
//   2. 유닛이 선택된 상태에서 빈 타일 클릭 → 이동 명령
//   3. 유닛이 없고 선택도 안 된 상태 → 타일 선택 (하이라이트)
//
// New Input System 사용:
//   UnityEngine.InputSystem 패키지의 Mouse, Touchscreen 클래스 사용.
//   레거시 Input 클래스 대신 Mouse.current / Touchscreen.current로 접근.
//
// Presentation 레이어 — Unity 의존.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
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
            Camera mainCamera)
        {
            _gridInteraction = gridInteraction;
            _unitMovement = unitMovement;
            _unitSpawn = unitSpawn;
            _mainCamera = mainCamera;
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

            // 테스트용 키보드 단축키 처리
            HandleDebugKeys();

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
        // 테스트용 키보드 단축키
        // ====================================================================

        /// <summary>
        /// 프로토타입 테스트용 키보드 단축키 처리.
        ///
        /// A 키: 선택된 유닛의 Attack 애니메이션 재생
        /// I 키: 선택된 유닛의 Idle 애니메이션 복귀
        ///
        /// New Input System API:
        ///   Keyboard.current — 현재 키보드 디바이스
        ///   .aKey.wasPressedThisFrame — A 키가 이번 프레임에 눌렸는지
        /// </summary>
        private void HandleDebugKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // A 키나 I 키가 눌렸을 때 디버그 출력
            if (keyboard.aKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame)
            {
                string key = keyboard.aKey.wasPressedThisFrame ? "A" : "I";

                if (_selectedUnit == null)
                {
                    Debug.Log($"[InputHandler] {key} 키 입력 - 유닛 미선택 상태. 먼저 유닛을 클릭하세요.");
                    return;
                }

                UnitView selectedView = FindSelectedUnitView();
                if (selectedView == null)
                {
                    Debug.LogWarning($"[InputHandler] {key} 키 입력 - UnitView를 찾을 수 없음.");
                    return;
                }

                if (keyboard.aKey.wasPressedThisFrame)
                {
                    Debug.Log($"[InputHandler] A 키 → Attack 애니메이션 재생");
                    selectedView.PlayAttack();
                }
                else if (keyboard.iKey.wasPressedThisFrame)
                {
                    Debug.Log($"[InputHandler] I 키 → Idle 애니메이션 복귀");
                    selectedView.PlayIdle();
                }
            }
        }

        /// <summary>
        /// 현재 선택된 유닛의 UnitView를 찾아 반환.
        /// </summary>
        private UnitView FindSelectedUnitView()
        {
            if (_selectedUnit == null) return null;

            var unitViews = FindObjectsByType<UnitView>(FindObjectsSortMode.None);
            foreach (var view in unitViews)
            {
                if (view.Data != null && view.Data.Id == _selectedUnit.Id)
                {
                    return view;
                }
            }
            return null;
        }

        // ====================================================================
        // 클릭 처리
        // ====================================================================

        /// <summary>
        /// 클릭된 스크린 좌표를 처리.
        ///
        /// 판정 순서:
        ///   1. 클릭 위치에 유닛이 있는가?
        ///      → 있으면 해당 유닛 선택 (기존 선택 해제)
        ///   2. 유닛이 선택된 상태에서 빈 타일 클릭?
        ///      → 이동 명령 (A* 경로 계산 → 이동 시작)
        ///   3. 유닛 미선택 + 빈 타일?
        ///      → 타일 선택 (하이라이트)
        /// </summary>
        private void HandleClick(Vector2 screenPos)
        {
            // 스크린 좌표 → 월드 좌표 변환
            // ScreenToWorldPoint에 Vector3 필요. z는 카메라와의 거리.
            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, 0f));
            worldPos.z = 0f; // 2D이므로 z=0

            // 월드 좌표 → 헥스 좌표
            HexCoord clickedCoord = Core.HexMetrics.WorldToHex(worldPos);

            // --------------------------------------------------------
            // 1. 클릭 위치에 유닛이 있는지 확인
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
            // 2. 유닛이 선택된 상태에서 빈 타일 클릭 → 이동 명령
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
            // 3. 유닛 미선택 → 타일 선택 (하이라이트)
            // --------------------------------------------------------
            _gridInteraction?.SelectTileAt(worldPos);
        }
    }
}
