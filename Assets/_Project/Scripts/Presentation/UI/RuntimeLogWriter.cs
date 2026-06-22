// ============================================================================
// RuntimeLogWriter.cs
// [DEBUG-TEMP] 런타임 로그 유틸리티 — 디버깅 완료 후 이 파일을 통째로 삭제할 것.
//
// 목적:
//   BlockingOverlay 렌더링 디버깅을 위해, 런타임 흐름을 Debug.Log(콘솔)가 아닌
//   "파일"로 기록한다. (LogRules.md 규칙: Debug.Log 사용 금지, 파일로만 기록)
//
//   여러 UI 클래스(UIManager, BuildingPanelBase, BuildingPlacementUI,
//   InGameSettingsUI)가 같은 로그 파일에 기록해야 하므로,
//   기록 로직을 이 static 헬퍼 한 곳에 모아 공유한다.
//
// 사용법:
//   RuntimeLogWriter.Write("[12:34:56.789] [INFO] [UI/Xxx] 메시지 | key=value");
// ============================================================================

using UnityEngine; // Application.dataPath 사용을 위해 필요

namespace Hexiege.Presentation
{
    /// <summary>
    /// [DEBUG-TEMP] 런타임 로그를 파일에 누적 기록하는 static 헬퍼.
    /// 디버깅 완료 후 호출부와 함께 제거한다.
    /// </summary>
    internal static class RuntimeLogWriter
    {
        // 로그가 저장될 폴더 경로.
        // Application.dataPath 는 "<프로젝트>/Assets" 를 가리키므로,
        // 그 아래 _Project/Docs/_Logs/... 경로를 조합한다.
        private static readonly string LogDir = System.IO.Path.Combine(
            Application.dataPath,
            "_Project/Docs/_Logs/2026-06-22/09_32_canvas-sorting-order-fix");

        // 실제 로그가 기록될 파일 경로.
        private static readonly string LogPath = System.IO.Path.Combine(LogDir, "RuntimeLog_host.txt");

        /// <summary>
        /// [DEBUG-TEMP] 한 줄 메시지를 로그 파일 끝에 덧붙인다(append).
        /// 파일이 아직 없으면 헤더를 먼저 쓴 뒤 기록한다.
        /// </summary>
        /// <param name="message">기록할 한 줄 로그 메시지.</param>
        internal static void Write(string message)
        {
            try
            {
                // 폴더가 없으면 생성한다(이미 있으면 아무 일도 일어나지 않음).
                System.IO.Directory.CreateDirectory(LogDir);

                // 파일이 처음 생성되는 경우, 세션 헤더를 먼저 기록한다.
                if (!System.IO.File.Exists(LogPath))
                {
                    string header =
                        "=== BlockingOverlay 렌더링 디버깅 로그 ===\n" +
                        $"=== 세션 시작: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
                    System.IO.File.WriteAllText(LogPath, header);
                }

                // 메시지를 줄바꿈과 함께 파일 끝에 덧붙인다.
                System.IO.File.AppendAllText(LogPath, message + "\n");
            }
            catch
            {
                // 로그 기록 실패는 게임 동작에 영향을 주지 않도록 무시한다.
            }
        }
    }
}
