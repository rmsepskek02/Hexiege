// ============================================================================
// MigrateBlockingOverlayToUIManager.cs
// UIManager 단일 소유 BlockingOverlay 마이그레이션 안내 에디터 스크립트.
//
// 목적:
//   각 씬에서 UIManager Canvas에 BlockingOverlay GameObject를 추가하는
//   Inspector 작업 가이드를 제공한다.
//   자동화 불가 항목(씬 열기 / Inspector 연결)은 체크리스트로 안내한다.
//
// 사용 방법:
//   메뉴: Hexiege > Setup > BlockingOverlay UIManager 마이그레이션 가이드
//
// ⚠️  씬 직접 수정은 하지 않는다 — 씬 파일 자동 저장 시 예기치 않은 충돌이
//      발생할 수 있으므로 Inspector를 통해 수동으로 진행한다.
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Setup
{
    /// <summary>
    /// BlockingOverlay를 UIManager로 마이그레이션하기 위한 Inspector 작업 가이드.
    /// </summary>
    public static class MigrateBlockingOverlayToUIManager
    {
        // ====================================================================
        // 메뉴 항목
        // ====================================================================

        [MenuItem("Hexiege/Setup/BlockingOverlay UIManager 마이그레이션 가이드")]
        private static void ShowMigrationGuide()
        {
            // 에디터 다이얼로그로 체크리스트 표시
            string guide = BuildGuideText();
            EditorUtility.DisplayDialog(
                "BlockingOverlay UIManager 마이그레이션 가이드",
                guide,
                "확인"
            );

            // 콘솔에도 출력 (복사/참조용)
            Debug.Log("[MigrateBlockingOverlayToUIManager]\n" + guide);
        }

        // ====================================================================
        // 가이드 텍스트 빌드
        // ====================================================================

        private static string BuildGuideText()
        {
            return
                "=== UIManager BlockingOverlay 마이그레이션 체크리스트 ===\n\n" +

                "[ 공통 — UIManager 설정 ]\n" +
                "□ UIManager가 위치한 Canvas의 직속 자식으로 'BlockingOverlay' GameObject를 추가한다.\n" +
                "  - Canvas > (Inspector) Sort Order = 100 이상으로 설정\n" +
                "  - RectTransform: anchorMin=(0,0) anchorMax=(1,1) offsetMin=offsetMax=(0,0)\n" +
                "  - Image 컴포넌트 추가: Color=(0,0,0,0.5), Raycast Target=true\n" +
                "  - CanvasGroup 컴포넌트 추가: alpha=0, blocksRaycasts=false, interactable=false\n" +
                "  - Button 컴포넌트 추가 (터치 닫기 Popup 모드용)\n" +
                "  - SafeAreaContainer 바깥(Canvas 직속)에 위치해야 함 (Rule 4)\n\n" +

                "□ UIManager Inspector에서 두 필드를 연결한다.\n" +
                "  - _blockingOverlay → 위에서 만든 BlockingOverlay의 CanvasGroup\n" +
                "  - _blockingOverlayButton → BlockingOverlay의 Button 컴포넌트\n\n" +

                "[ Login 씬 — Assets/Scenes/Login.unity ]\n" +
                "□ UIManager Canvas에 BlockingOverlay를 추가하고 Inspector 연결\n" +
                "□ (구) AnonymousWarningPopup 안에 있던 BlockingOverlay Image가 있다면\n" +
                "  Raycast Target=false로 설정하거나 제거한다.\n" +
                "□ (구) ConfirmPopup 안에 있던 BlockingOverlay Image가 있다면 동일 처리\n\n" +

                "[ Game 씬 — Assets/Scenes/Game.unity ]\n" +
                "□ UIManager Canvas에 BlockingOverlay를 추가하고 Inspector 연결\n" +
                "□ (구) RematchRequestPopup > Overlay GameObject:\n" +
                "  - Image의 Raycast Target=false로 설정 (입력 차단 역할 제거)\n" +
                "  - 또는 삭제 (코드에서 이미 참조 제거됨)\n" +
                "□ (구) SharedBackground 오브젝트 (Canvas 직속 Background+Button):\n" +
                "  - 해당 GameObject를 비활성화하거나 삭제\n" +
                "  - InGameSettingsUI, BuildingPlacementUI, BuildingPanelBase 모두\n" +
                "    UIManager.ShowBlockingOverlay()로 교체 완료됨\n\n" +

                "[ Lobby 씬 — Assets/Scenes/Lobby.unity ]\n" +
                "□ UIManager가 존재하는지 확인\n" +
                "□ 존재한다면 동일하게 BlockingOverlay 추가 및 Inspector 연결\n\n" +

                "[ 완료 후 검증 ]\n" +
                "□ Login > 익명 로그인 경고 팝업 → 배경이 전체화면을 덮는지 확인\n" +
                "□ Game > 포기 확인 팝업 → 배경이 노치/홈바 영역까지 덮는지 확인\n" +
                "□ Game > 설정 메뉴 → 배경 탭 시 닫히는지 확인\n" +
                "□ Game > 건물 클릭 패널 → 배경 탭 시 닫히는지 확인\n" +
                "□ Game > 재경기 요청 팝업 → 배경 탭이 차단되는지 확인\n\n" +

                "참고: GameSystemRules_UI.md 규칙 4, 규칙 5 참조";
        }
    }
}
