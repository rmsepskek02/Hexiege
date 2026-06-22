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
        /// <summary>
        /// [DEBUG-TEMP] 한 줄 메시지를 로그 파일 끝에 덧붙인다(append).
        /// 파일이 아직 없으면 헤더를 먼저 쓴 뒤 기록한다.
        /// </summary>
        /// <param name="message">기록할 한 줄 로그 메시지.</param>
        internal static void Write(string message)
        {
            try
            {
                // static readonly 필드로 선언하면 클래스 로드 시점(런타임 전)에
                // Application.dataPath 가 아직 초기화되지 않아 빈 문자열이 될 수 있으므로,
                // 호출 시점마다 경로를 계산한다.
                // UnityEngine.Application 을 명시 — Hexiege.Application 네임스페이스와 이름이 겹침.
                string logDir = System.IO.Path.Combine(
                    UnityEngine.Application.dataPath,
                    "_Project/Docs/_Logs/2026-06-22/09_32_canvas-sorting-order-fix");
                string logPath = System.IO.Path.Combine(logDir, "RuntimeLog_host.txt");

                // 폴더가 없으면 생성한다(이미 있으면 아무 일도 일어나지 않음).
                System.IO.Directory.CreateDirectory(logDir);

                // 파일이 처음 생성되는 경우, 세션 헤더를 먼저 기록한다.
                if (!System.IO.File.Exists(logPath))
                {
                    string header =
                        "=== BlockingOverlay 렌더링 디버깅 로그 ===\n" +
                        $"=== 세션 시작: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
                    System.IO.File.WriteAllText(logPath, header);
                }

                // 메시지를 줄바꿈과 함께 파일 끝에 덧붙인다.
                System.IO.File.AppendAllText(logPath, message + "\n");
            }
            catch (System.Exception e)
            {
                // [DEBUG-TEMP] 파일 기록 실패 원인 파악용 — 원인 확인 후 제거
                UnityEngine.Debug.LogError($"[RuntimeLogWriter] 파일 기록 실패: {e}");
            }
        }
    }
}
