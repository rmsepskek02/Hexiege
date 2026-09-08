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

        /// <summary> 스킬 건물(FlightFacility/MagicBuilding) 클릭 시 표시되는 전용 스킬 패널. </summary>
        private BuildingSkillPanelUI _skillPanelUI;

        /// <summary> MistShrine(HealShrine) 클릭 시 표시되는 전용 물안개 힐 패널. </summary>
        private MistShrinePanelUI _mistShrinePanelUI;

        /// <summary> 스킬 지점 조준 컨트롤러. 조준 중이면 타일 선택 입력을 억제한다. </summary>
        private SkillAimController _skillAimController;

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
            ResearchPanelUI researchPanelUI = null,
            BuildingSkillPanelUI skillPanelUI = null,
            SkillAimController skillAimController = null,
            MistShrinePanelUI mistShrinePanelUI = null)
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
            // 스킬 건물(FlightFacility/MagicBuilding) 클릭 라우팅용 전용 패널 — 없으면(null) 기존 액션 패널로 폴백.
            _skillPanelUI = skillPanelUI;
            // 스킬 지점 조준 컨트롤러 — 조준 중 타일 선택 억제 가드에 사용.
            _skillAimController = skillAimController;
            // MistShrine(HealShrine) 클릭 라우팅용 전용 패널 — 없으면(null) 기존 액션 패널로 폴백.
            _mistShrinePanelUI = mistShrinePanelUI;
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
        /// 판정 순서 — GameSystemRules_UI.md 「무작위 맵 타일 선택과 건설 패널」 규칙 5와
        /// 같은 순서이며, 아래 코드도 위에서부터 정확히 이 순서로 읽힌다.
        ///
        ///   (사전 처리) 스킬 조준 중 / 랠리포인트 지정 중 / UI 위 클릭 / 팝업 닫힘 프레임 → 통과 차단
        ///   1. 기존 building action 분기        — 건물이 있는 타일: 종류별 패널 + 타일 선택
        ///   2. MineKind 기반 MiningPost 자격 분기 — 광산 타일: 채굴소 배치 팝업 + 타일 선택
        ///   3. TileKind.Blocked  → 선택 불가. 선택 해제 + 배치 패널 닫기(토스트 없음)
        ///   4. TileKind.NoBuild  → 자기 팀: 선택 + ToastKey.BuildingNotAllowed
        ///                          중립·적: 선택만
        ///   5. TileKind.Normal   → 지금까지와 동일 (자기 팀 빈 타일이면 건물 배치 팝업)
        ///
        /// 🔴 1·2번이 3·4번보다 반드시 먼저다. 광산 위 채굴소는 NoBuild 타일에서도 지을 수 있는
        ///    예외인데, NoBuild 처리가 먼저 오면 그 예외가 통째로 막히기 때문이다.
        /// </summary>
        private void HandleClick(Vector2 screenPos)
        {
            // --------------------------------------------------------
            // 0.5. 랠리포인트 설정 모드 (UI 체크보다 우선)
            //    팝업이 닫힌 상태에서 타일을 선택해야 하므로
            //    IsPointerOverUI보다 먼저 처리해야 함.
            // --------------------------------------------------------
            // 스킬 지점 조준 중에는 타일 선택/건물 팝업을 억제한다(조준 컨트롤러가 입력을 소유).
            // 조준은 SkillAimController가 press/drag/release를 자체 처리하므로 여기서는 통과만 막는다.
            if (SkillAimController.IsAiming)
                return;

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
            //
            // ⚠️ 아래 뒤쪽 3항(연구·스킬·MistShrine)은 2026-08-10에 추가되었다.
            //   · 연구 패널·스킬 패널: 필드·주입·열기 호출은 모두 갖춰져 있는데 이 가드에서만 빠져 있던
            //     **기존 결손**이다. 두 패널을 배경 탭으로 닫으면 그 클릭이 같은 프레임에 뒤쪽 타일 선택으로
            //     새어 나갈 수 있었다. TechnicalDesignDocument.md "PopupClosedFrame(팝업 닫힘 프레임 보호)"
            //     패턴은 모든 팝업이 가드에 포함되어야 성립하므로 함께 보정했다(사용자 승인).
            //   · MistShrine 패널: 이번에 새로 추가된 팝업이라 같은 가드가 필요하다(MistShrine UI 규칙 4).
            //   각 항은 != null 가드를 동반하므로, 씬에 배선되지 않은 패널은 기존과 똑같이 무시된다.
            if ((_buildingUI != null && _buildingUI.ClosedFrame == frame)
                || (_productionUI != null && _productionUI.ClosedFrame == frame)
                || (_actionPanelUI != null && _actionPanelUI.ClosedFrame == frame)
                || (_researchPanelUI != null && _researchPanelUI.ClosedFrame == frame)
                || (_skillPanelUI != null && _skillPanelUI.ClosedFrame == frame)
                || (_mistShrinePanelUI != null && _mistShrinePanelUI.ClosedFrame == frame))
                return;

            // 스크린 좌표 → XZ 평면 레이캐스트 → 뷰 좌표 → 도메인 월드 좌표로 역변환
            Vector3 viewPos = ScreenToXZPlane(screenPos);
            Vector3 worldPos = ViewConverter.FromView(viewPos);

            // 도메인 월드 좌표 → 헥스 좌표
            HexCoord clickedCoord = HexMetrics.WorldToHex(worldPos);

            // --------------------------------------------------------
            // 1. 기존 building action 분기 (규칙 5의 1번)
            //    건물이 있는 타일 → 종류에 따라 분기
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
                        else if ((buildingAtPos.Type == BuildingType.FlightFacility
                                  || buildingAtPos.Type == BuildingType.MagicBuilding)
                            && _skillPanelUI != null)
                        {
                            // (2b) 스킬 건물(FlightFacility=Human / MagicBuilding=Spirit·Trans 공유) →
                            //   전용 스킬 패널. 종족 로드아웃으로 슬롯 1~5를 동적으로 채운다(규칙 1·6·8·9).
                            //   (_skillPanelUI 미배선 시엔 아래 액션 패널 분기로 폴백된다.)
                            _skillPanelUI.Show(buildingAtPos);
                        }
                        else if (buildingAtPos.Type == BuildingType.HealShrine
                            && _mistShrinePanelUI != null)
                        {
                            // (2c) MistShrine(초월 회복 건물) → 전용 물안개 힐 패널.
                            //   사용(탭=시전 / 롱프레스=자동 토글) + 철거 + 회복 범위 표시를 담당한다.
                            //   범위 표시는 이 분기까지 온 경우(= 내 팀 · 생존)만 켜지므로,
                            //   위 262행의 isMine 검사 덕분에 "적 MistShrine은 범위를 보여주지 않는다"
                            //   (UI 규칙 8)가 추가 코드 없이 성립한다.
                            //   (_mistShrinePanelUI 미배선 시엔 아래 액션 패널 분기로 폴백된다.)
                            _mistShrinePanelUI.Show(buildingAtPos);
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
            // 2. MineKind 기반 MiningPost 자격 분기 (규칙 5의 2번)
            //    금광 타일(건물 없음) → 채굴소 건설 팝업
            //
            //    🔴 이 분기가 아래 3·4번(Blocked/NoBuild)보다 먼저 있어야 한다.
            //       채굴소는 NoBuild 타일 위에서도 지을 수 있는 예외이기 때문이다.
            //       순서를 바꾸면 광산이 NoBuild 타일에 놓인 순간 채굴소를 못 짓게 된다.
            // --------------------------------------------------------
            if (_buildingPlacement != null &&
                _buildingPlacement.CanPlaceMiningPost(clickedCoord, LocalPlayerTeam.Current))
            {
                _buildingUI?.Show(clickedCoord, LocalPlayerTeam.Current);
                _gridInteraction?.SelectTileAt(worldPos);
                return;
            }

            // --------------------------------------------------------
            // 3~5. TileKind(지형 종류) 기반 판정 (규칙 5의 3~5번 / 규칙 6·7·8)
            //
            //   "어떤 타일인가"의 판정은 Application 레이어(GridInteractionUseCase)가 하고,
            //   그 결과를 받아 화면 반응(패널 닫기·토스트)만 여기서 처리한다.
            //   선택·하이라이트 이벤트 발행은 SelectTileByKind 안에서 이미 끝난다.
            //
            //   _gridInteraction이 아직 주입되지 않았다면(씬 초기화 전 등) 예전과 똑같이
            //   "선택은 못 하지만 흐름은 계속" 이어지도록 Normal로 간주한다.
            // --------------------------------------------------------
            TileClickOutcome outcome = _gridInteraction != null
                ? _gridInteraction.SelectTileByKind(worldPos, LocalPlayerTeam.Current)
                : TileClickOutcome.Normal;

            // 3. 막힌 타일(Blocked) 또는 격자 밖 빈 공간 → 선택 불가 (규칙 6)
            //    선택은 이미 해제됐고, 여기서는 열려 있던 배치 패널만 닫는다. 토스트는 없다.
            if (outcome == TileClickOutcome.NotSelectable)
            {
                CloseBuildingPlacementPanel();
                return;
            }

            // 4-a. 자기 팀 소유 건설 불가 타일(NoBuild) → 선택은 되지만 건설은 막는다 (규칙 7)
            if (outcome == TileClickOutcome.NoBuildOwnTeam)
            {
                CloseBuildingPlacementPanel();
                ToastUI.Show(ToastKey.BuildingNotAllowed);
                return;
            }

            // 4-b. 중립·적 소유 건설 불가 타일(NoBuild) → 선택만. 건설 의도가 없으니 토스트도 없다 (규칙 8)
            if (outcome == TileClickOutcome.NoBuildOther)
            {
                CloseBuildingPlacementPanel();
                return;
            }

            // --------------------------------------------------------
            // 5. 일반 타일(Normal) → 지금까지와 동일 (규칙 5의 5번)
            //    자기 팀 빈 타일이면 건물 배치 팝업을 연다.
            //    (선택·하이라이트는 위 SelectTileByKind에서 이미 처리됐으므로
            //     예전처럼 SelectTileAt을 다시 부르지 않는다 — 다시 부르면 같은 타일
            //     재클릭으로 간주돼 방금 켠 하이라이트가 즉시 꺼진다.)
            // --------------------------------------------------------
            if (_buildingPlacement != null &&
                _buildingPlacement.CanPlaceBuilding(clickedCoord, LocalPlayerTeam.Current))
            {
                _buildingUI?.Show(clickedCoord, LocalPlayerTeam.Current);
            }
        }

        /// <summary>
        /// 열려 있는 건물 배치 팝업을 닫는다.
        /// 규칙 6·7·8이 공통으로 요구하는 "이전 타일에서 열린 패널이 남아 있으면 닫는다"를 담당.
        ///
        /// IsOpen을 먼저 확인하는 이유:
        ///   BuildingPlacementUI.Close()는 UIManager의 공유 BlockingOverlay도 함께 숨긴다.
        ///   열려 있지도 않은데 호출하면 다른 UI가 쓰고 있는 오버레이까지 꺼질 수 있다.
        /// </summary>
        private void CloseBuildingPlacementPanel()
        {
            if (_buildingUI != null && _buildingUI.IsOpen)
            {
                _buildingUI.Close();
            }
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
                // [개발] Warn + 개발.
                //   축 A: UI 히트 판정을 포기하고 false(=UI 위가 아님)로 계속 진행한다 → 대체 경로 있음 → Warn.
                //   축 B: EventSystem 은 씬에 배치돼야 하는 오브젝트다. 없다면 씬 구성 오류이고
                //         모든 기기에서 똑같이 없다 → 축 B ① 이 "아니오" → 개발(1.3 원칙 3 단서).
                //   ⚠️ 이 메서드는 터치/클릭마다 호출된다. 매 프레임 스팸은 아니지만(입력 시점 한정)
                //      개발 로그라 릴리스에서는 [Conditional] 로 호출 자체가 사라진다(1.7).
                GameLog.Dev.Warn("Input", nameof(InputHandler),
                                 "EventSystem.current 가 null — UI 히트 판정을 건너뛴다");
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
