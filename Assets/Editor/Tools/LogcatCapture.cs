// ============================================================================
// LogcatCapture.cs  (에디터 전용 · 상시 사용 도구)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) 기기를 USB 로 연결한다(USB 디버깅 ON).                                │
// │  2) 상단 메뉴  Hexiege > Logcat > 1. 버퍼 비우기   실행.                   │
// │  3) 기기에서 버그를 재현한다.                                             │
// │  4) 상단 메뉴  Hexiege > Logcat > 2. 파일로 저장   실행.                   │
// │     → Assets/_Project/Docs/_Logs/{날짜}/{시각}_logcat/RuntimeLog_device.txt │
// └────────────────────────────────────────────────────────────────────────┘
//
// ┌─ 곁들인 도구: 오래된 에디터 로그 정리 ──────────────────────────────────┐
// │  상단 메뉴  Hexiege > Logcat > 3. 오래된 에디터 로그 정리   실행.          │
// │  에디터에서 플레이할 때마다 자동으로 쌓이는 상시 로그                       │
// │  (Assets/_Project/Docs/_Logs/_editor/{날짜}/RuntimeLog.txt)가              │
// │  너무 많아졌을 때, 오래된 "날짜 폴더"만 골라 지운다(LogRules.md 1.10).      │
// │  · 자동 실행·주기 실행은 하지 않는다. 사용자가 메뉴를 누를 때만 동작한다.   │
// │  · 삭제 대상 목록을 먼저 보여 주고, 사용자가 확인한 뒤에만 지운다.          │
// │  · 위 [1]/[2] 로 뽑은 실기기 캡처 파일은 대상이 아니다(경로가 다르다).      │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가(유니티 초급자 기준):
//   실기기(Android) 테스트에서는 RuntimeLogger 가 파일을 만들지 못한다.
//   RuntimeLogger.cs 의 파일 쓰기 코드가 전부 #if UNITY_EDITOR 안에 있기 때문이다.
//   그래서 실기기 로그는 Debug.Log → Android Logcat 으로만 나간다.
//
//   지금까지는 사용자가 Logcat 창에서 로그를 드래그 복사해 채팅에 붙여넣어야 했는데,
//   분량 제한 때문에 로그 일부만 전달되어 원인 추적이 늦어지는 일이 있었다.
//   이 스크립트는 그 Logcat 을 통째로 파일로 뽑아 프로젝트 안에 저장한다.
//
//   내부적으로는 Android SDK 의 커맨드라인 도구 `adb`(Android Debug Bridge)를
//   찾아서 실행할 뿐이다. 사용자는 터미널을 열 필요가 없다.
//
// 왜 캡처 메뉴가 [1]/[2] 둘로 나뉘어 있는가:
//   Logcat 은 "링 버퍼"다. 기기가 계속 로그를 쌓다가 오래된 것부터 지운다.
//   그래서 테스트 직전에 [1] 로 한 번 비워야, [2] 로 저장했을 때
//   "이번 테스트에서 나온 로그만" 깔끔하게 담긴다.
//   비우지 않으면 과거 로그가 섞여 들어와 어디부터가 이번 재현인지 알 수 없다.
//   ([3] 은 실기기 캡처와 무관한 별개 도구다 — 위 「곁들인 도구」 참조)
//
// 이 스크립트가 하지 않는 것:
//   · 기기에 앱을 설치하거나 실행하지 않는다(빌드/설치는 기존 방식 그대로).
//   · 실시간 스트리밍(logcat 계속 보기)을 하지 않는다. 한 번 찍어서 파일로 뽑는 용도다.
//   · 캡처([1]/[2])는 기존 _Logs 파일을 지우거나 덮어쓰지 않는다(항상 새 파일에 쓴다).
//   · 파일을 지우는 것은 [3] 하나뿐이며, 그것도 _Logs/_editor/ 하위의 날짜 폴더로 한정된다.
//     자동·주기 삭제는 어떤 경우에도 하지 않는다(LogRules.md 1.10 「자동 삭제하지 않는다」).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization; // 날짜 폴더 이름을 문화권과 무관하게 해석하기 위해 필요(CultureInfo.InvariantCulture).
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Android 실기기의 Logcat 을 파일로 캡처하는 에디터 도구.
    /// adb 를 직접 찾아 실행하므로 사용자가 터미널을 다루지 않아도 된다.
    /// </summary>
    public static class LogcatCapture
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>대화상자 제목 접두사(모든 알림창 공통).</summary>
        private const string DialogTitle = "Hexiege Logcat";

        /// <summary>
        /// 로그 저장 폴더의 프로젝트 상대 경로 뿌리(LogRules.md 의 _Logs 규칙).
        /// Application.dataPath 가 "&lt;프로젝트&gt;/Assets" 이므로 그 아래 경로만 적는다.
        /// </summary>
        private const string LogsRootRelativeToAssets = "_Project/Docs/_Logs";

        /// <summary>
        /// 실기기 캡처 파일 이름(확장자 제외). LogRules.md 1.12 「실기기 로그」의 명명 규칙이다.
        ///
        /// ⚠️ 에디터 상시 로그의 파일명과 혼동하지 말 것.
        ///    에디터 쪽은 2026-08-14 개정으로 역할(host/client)을 뺀 `RuntimeLog.txt` 단일 이름이 되었지만
        ///    (LogRules.md 1.10), 이 실기기 캡처 파일은 그 개정과 무관한 별개 경로·별개 규정(1.12)이라
        ///    `RuntimeLog_device` 라는 이름을 그대로 유지한다.
        /// </summary>
        private const string LogFileBaseName = "RuntimeLog_device";

        /// <summary>
        /// 에디터 상시 로그의 뿌리 폴더(Assets 폴더 기준 상대 경로) — 메뉴 [3] 정리 대상의 유일한 범위.
        ///
        /// ⚠️ 이 값은 Infrastructure/Debug/FileSink.cs 의
        ///    `EditorLogsRootRelativeToAssets` 와 **같은 값이어야 한다.**
        ///    (FileSink 가 쓰는 곳 / 이 도구가 지우는 곳이 어긋나면,
        ///     "지워지지 않는 로그" 또는 "엉뚱한 폴더 삭제" 가 된다)
        ///    FileSink 쪽 상수가 private 이라 참조할 수 없어 값을 그대로 옮겨 적었다.
        ///    FileSink 의 경로가 바뀌면 이 상수도 함께 바꾼다.
        /// </summary>
        private const string EditorLogsRootRelativeToAssets = "_Project/Docs/_Logs/_editor";

        /// <summary>
        /// 정리 메뉴의 기본 보존 기간(일). 이 일수보다 오래된 날짜 폴더만 삭제 후보가 된다.
        ///
        /// 기본값을 30일로 넉넉히 잡은 이유:
        ///   지워진 로그는 복구되지 않는다. 기본값이 공격적이면
        ///   "확인 버튼을 습관적으로 누르다가 어제 로그를 날리는" 사고가 난다.
        ///   로그는 텍스트라 용량이 작으므로(LogRules.md 1.10 실측 약 54KB/파일)
        ///   오래 남겨 두는 쪽의 비용이 거의 없다.
        /// </summary>
        private const int DefaultRetentionDays = 30;

        /// <summary>정리 메뉴에서 고를 수 있는 더 보수적인 보존 기간(일).</summary>
        private const int LongRetentionDays = 90;

        /// <summary>
        /// 날짜 폴더 이름의 형식. FileSink 가 `DateTime.Now.ToString("yyyy-MM-dd")` 로 만든 이름과 같아야 한다.
        /// 이 형식으로 해석되지 않는 폴더는 "판정 불가"로 보고 **절대 삭제하지 않는다.**
        /// </summary>
        private const string DateFolderFormat = "yyyy-MM-dd";

        /// <summary>확인 대화상자에 이름을 그대로 나열할 폴더의 최대 개수(넘치면 "외 N개"로 줄인다).</summary>
        private const int MaxListedFolders = 20;

        /// <summary>`adb devices` 처럼 즉시 끝나는 명령의 제한 시간(밀리초).</summary>
        private const int ShortTimeoutMs = 10000;

        /// <summary>`adb logcat -d` 처럼 출력이 많을 수 있는 명령의 제한 시간(밀리초).</summary>
        private const int CaptureTimeoutMs = 30000;

        /// <summary>
        /// Logcat 필터 규격. "태그:최소우선순위" 를 나열하고 맨 끝의 `*:S` 로 나머지를 전부 침묵시킨다.
        ///
        /// 각 태그를 고른 이유:
        ///   Unity:V           — Unity 의 Debug.Log/LogWarning/LogError 는 Android 에서 전부 태그 "Unity" 로 나간다.
        ///                       즉 RuntimeLogger 가 남기는 우리 게임 로그가 여기 담긴다. (V = Verbose 이상 전부)
        ///   CRASH:V           — Unity 자체 크래시 핸들러가 쓰는 태그. 유니티 런타임이 죽는 순간의 정보.
        ///   DEBUG:V           — 안드로이드 debuggerd(툼스톤) 가 쓰는 태그. SIGSEGV/SIGABRT 같은
        ///                       네이티브 크래시의 시그널·레지스터·콜스택이 여기로 나온다.
        ///                       ★ 앱이 "그냥 꺼지는" 현상은 대부분 여기에만 단서가 남는다.
        ///   AndroidRuntime:E  — 자바(안드로이드) 측 미처리 예외("FATAL EXCEPTION"). 플러그인/권한 문제에서 나온다.
        ///   ActivityManager:E — 앱이 멈춰서 시스템이 강제로 끄는 ANR("ANR in com.xxx") 을 남기는 주체.
        ///   libc:F            — abort()/fdsan 등 C 런타임의 치명(Fatal) 메시지. 네이티브 크래시 직전 원인 문구.
        ///   art:E             — ART/JNI 치명 오류. IL2CPP 가 JNI 를 잘못 호출했을 때 여기서 잡힌다.
        ///   *:S               — 위에 나열하지 않은 모든 태그를 Silent 로 만들어 잡음을 제거한다.
        ///
        /// ⚠️ `adb logcat -s Unity` 형태의 축약(-s)은 쓰지 않는다.
        ///    -s 는 "나머지 전부 침묵 + 지정 태그만" 이라는 뜻이라 위 나열과 의미가 겹쳐 헷갈리고,
        ///    무엇보다 -s Unity 만 쓰면 네이티브 크래시(DEBUG/libc)가 통째로 빠진다.
        ///
        /// ⚠️ `*` 는 셸 와일드카드처럼 보이지만 확장되지 않는다.
        ///    아래 RunAdb 가 UseShellExecute=false 로 셸을 거치지 않고 adb 를 직접 실행하기 때문이다.
        /// </summary>
        private static readonly string[] LogcatTagFilters =
        {
            "Unity:V",
            "CRASH:V",
            "DEBUG:V",
            "AndroidRuntime:E",
            "ActivityManager:E",
            "libc:F",
            "art:E",
            "*:S"
        };

        /// <summary>
        /// 읽어 올 로그 버퍼 목록.
        ///
        /// Logcat 은 버퍼가 여러 개로 나뉘어 있고, 태그마다 들어가는 버퍼가 다르다.
        ///   main   — 앱 로그(Unity 태그)가 들어간다.
        ///   system — 시스템 서비스 로그(ActivityManager 의 ANR)가 들어간다.
        ///   crash  — 네이티브 크래시(DEBUG 태그 툼스톤)가 들어간다.
        /// 세 개를 모두 지정하지 않으면 "크래시 로그만 쏙 빠지는" 상황이 생긴다.
        ///
        /// 콤마 나열(-b main,system,crash) 대신 -b 를 세 번 반복하는 이유:
        ///   콤마 문법은 비교적 최근 adb 에서만 동작하고, 반복 지정은 예전 버전에서도 통한다.
        /// </summary>
        private static readonly string[] LogBuffers = { "main", "system", "crash" };

        // ====================================================================
        // 메뉴 1 — 버퍼 비우기
        // ====================================================================

        /// <summary>
        /// 기기의 Logcat 링 버퍼를 비운다(`adb logcat -c`).
        /// 실기기 테스트를 시작하기 "직전"에 실행해서, 이번 재현에서 나온 로그만 남게 만든다.
        /// </summary>
        [MenuItem("Hexiege/Logcat/1. 버퍼 비우기", false, 1)]
        public static void ClearBuffer()
        {
            // ── adb 확보 ───────────────────────────────────────────────────
            if (!TryResolveAdb(out string adbPath)) return;

            // ── 대상 기기 확보 ─────────────────────────────────────────────
            if (!TryResolveDevice(adbPath, out string serial, out string deviceLabel)) return;

            // ── 실행 ───────────────────────────────────────────────────────
            // -b 로 지정한 버퍼들을 함께 비운다. 버퍼를 지정하지 않으면 main 만 비워져
            // crash 버퍼에 남은 예전 툼스톤이 다음 캡처에 섞여 들어온다.
            string args = "logcat " + BuildBufferArgs() + " -c";
            AdbResult result = RunAdb(adbPath, serial, args, ShortTimeoutMs, "Logcat 버퍼를 비우는 중...");

            if (!result.Success)
            {
                ShowAdbFailure("버퍼 비우기", result);
                return;
            }

            EditorUtility.DisplayDialog(
                DialogTitle,
                "Logcat 버퍼를 비웠습니다.\n\n" +
                $"대상 기기: {deviceLabel}\n\n" +
                "이제 기기에서 버그를 재현한 뒤,\n" +
                "메뉴 [Hexiege > Logcat > 2. 파일로 저장] 을 실행하세요.",
                "확인");
        }

        // ====================================================================
        // 메뉴 2 — 파일로 저장
        // ====================================================================

        /// <summary>
        /// 현재 기기에 쌓여 있는 Logcat 을 한 번 읽어(`adb logcat -d`) 프로젝트 안 파일로 저장한다.
        /// 저장 경로는 LogRules.md 규칙에 따라 `_Logs/{yyyy-MM-dd}/{HH_mm}_logcat/` 아래이며,
        /// 폴더가 없으면 자동으로 만든다(사용자에게 경로를 묻지 않는다).
        /// </summary>
        [MenuItem("Hexiege/Logcat/2. 파일로 저장", false, 2)]
        public static void SaveToFile()
        {
            // ── adb 확보 ───────────────────────────────────────────────────
            if (!TryResolveAdb(out string adbPath)) return;

            // ── 대상 기기 확보 ─────────────────────────────────────────────
            if (!TryResolveDevice(adbPath, out string serial, out string deviceLabel)) return;

            // ── 로그 덤프 ──────────────────────────────────────────────────
            //   -d          : 버퍼에 있는 것만 뱉고 즉시 종료(dump). 이게 없으면 계속 붙어서 안 끝난다.
            //   -v threadtime: "날짜 시각 PID TID 우선순위 태그: 메시지" 형식. 시간과 스레드가 보여 추적이 쉽다.
            string args = "-d -v threadtime " + BuildBufferArgs() + " " + string.Join(" ", LogcatTagFilters);
            AdbResult result = RunAdb(adbPath, serial, "logcat " + args, CaptureTimeoutMs, "Logcat 을 읽는 중...");

            if (!result.Success)
            {
                ShowAdbFailure("로그 저장", result);
                return;
            }

            string logBody = result.StandardOutput;

            // ── 비어 있으면 파일을 만들지 않는다 ────────────────────────────
            //   빈 파일을 _Logs 에 남기면 나중에 "로그가 있는 줄 알았는데 비어 있는" 혼란만 생긴다.
            if (string.IsNullOrWhiteSpace(logBody))
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "받아온 로그가 비어 있습니다. 파일을 만들지 않았습니다.\n\n" +
                    $"대상 기기: {deviceLabel}\n\n" +
                    "다음을 확인하세요.\n" +
                    "  1) [1. 버퍼 비우기] 를 실행한 뒤에 실제로 앱을 실행하고 버그를 재현했나요?\n" +
                    "     (비운 직후 바로 저장하면 당연히 비어 있습니다)\n" +
                    "  2) 기기에 설치된 빌드가 Development Build 인가요?\n" +
                    "     Release 빌드는 Debug.Log 출력이 제거되어 있을 수 있습니다.\n" +
                    "  3) 앱을 실행한 기기와 지금 연결된 기기가 같은 기기인가요?",
                    "확인");
                return;
            }

            // ── 저장 경로 결정 + 폴더 생성 ─────────────────────────────────
            DateTime now = DateTime.Now;
            string folderAbsolute = Path.Combine(
                // ⚠️ Application 은 반드시 UnityEngine 으로 완전 수식한다.
                //    이 파일은 namespace Hexiege.EditorTools 안에 있어 바깥 네임스페이스 Hexiege 도
                //    이름 탐색 대상이 되고, 거기에 Hexiege.Application 네임스페이스가 존재한다.
                //    C# 은 using 보다 "자기를 감싸는 네임스페이스"를 먼저 찾으므로,
                //    그냥 Application 이라고 쓰면 UnityEngine.Application 이 아니라
                //    네임스페이스 Hexiege.Application 으로 해석되어 CS0234 가 난다.
                UnityEngine.Application.dataPath,
                LogsRootRelativeToAssets,
                now.ToString("yyyy-MM-dd"),
                now.ToString("HH_mm") + "_logcat");

            string fileAbsolute;
            try
            {
                Directory.CreateDirectory(folderAbsolute); // 중간 경로까지 한 번에 생성. 이미 있으면 아무 일도 없다.
                fileAbsolute = MakeNonOverwritingPath(folderAbsolute, LogFileBaseName, ".txt");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "로그를 저장할 폴더를 만들지 못했습니다.\n\n" +
                    $"경로: {folderAbsolute}\n" +
                    $"원인: {e.Message}\n\n" +
                    "폴더 쓰기 권한이 있는지, 경로가 다른 프로그램에 잠겨 있지 않은지 확인하세요.",
                    "확인");
                return;
            }

            // ── 파일 내용 조립(LogRules.md 헤더 형식) ──────────────────────
            var sb = new StringBuilder();
            sb.AppendLine("=== Android Logcat 캡처 ===");
            sb.AppendLine($"=== 저장 시각: {now:yyyy-MM-dd HH:mm:ss} ===");
            // 기기 정보 한 줄 추가: 기기가 여러 대일 때 "어느 기기의 로그인지" 가 나중에 반드시 문제가 된다.
            sb.AppendLine($"=== 대상 기기: {deviceLabel} ===");
            sb.AppendLine();
            sb.Append(logBody);

            // ── 파일 쓰기(UTF-8) ───────────────────────────────────────────
            //   Logcat 메시지에 한글이 섞여 있으므로 반드시 UTF-8 로 쓴다.
            //   BOM 없이 쓰는 이유: git diff 와 텍스트 도구에서 앞부분에 깨진 문자가 보이는 것을 피하기 위함.
            try
            {
                File.WriteAllText(fileAbsolute, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "로그 파일 쓰기에 실패했습니다.\n\n" +
                    $"경로: {fileAbsolute}\n" +
                    $"원인: {e.Message}",
                    "확인");
                return;
            }

            // ── 프로젝트 창에 즉시 반영 ────────────────────────────────────
            //   Unity 는 외부에서 만든 파일을 자동으로 알아채지 못할 때가 있다. Refresh 로 강제로 읽힌다.
            AssetDatabase.Refresh();

            string projectRelative = ToProjectRelativePath(fileAbsolute);
            int lineCount = CountLines(logBody);

            // 프로젝트 창에서 방금 만든 파일을 선택 상태로 만들어 준다(찾기 쉽게).
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(projectRelative);
            if (asset != null) Selection.activeObject = asset;

            Debug.Log($"[LogcatCapture] 저장 완료: {projectRelative} ({lineCount}줄, 기기={deviceLabel})");

            bool openFolder = EditorUtility.DisplayDialog(
                DialogTitle,
                "Logcat 을 파일로 저장했습니다.\n\n" +
                $"경로: {projectRelative}\n" +
                $"줄 수: {lineCount}줄\n" +
                $"대상 기기: {deviceLabel}",
                "폴더 열기", "닫기");

            if (openFolder) EditorUtility.RevealInFinder(fileAbsolute);
        }

        // ====================================================================
        // 메뉴 3 — 오래된 에디터 로그 정리 (LogRules.md 1.10 「수동 정리 메뉴」)
        //
        // 이 메뉴는 위의 [1]/[2](실기기 Logcat 캡처)와 아무 관계가 없다.
        // 지우는 대상은 오직 "에디터에서 플레이할 때마다 쌓이는 상시 로그" 뿐이다.
        //
        // ── 규정이 요구하는 4가지 (LogRules.md 1.10) ─────────────────────────
        //   ① 실행 시점 : 사용자가 메뉴를 직접 눌렀을 때만. 자동·주기 실행 금지.
        //   ② 사용자 확인 : 삭제 대상 목록을 먼저 보여 주고, 확인받은 뒤에만 삭제.
        //   ③ 정리 범위 : _Logs/_editor/ 하위로 한정. 삭제 "직전"에 다시 검사.
        //   ④ 판정 기준 : 날짜 폴더 단위. 파일 개수 기준(`최근 N개`)을 쓰지 않는다.
        //
        // ── ④ 를 왜 개수가 아니라 날짜로 하는가 (규정의 근거) ────────────────
        //   날짜 기준은 판정 근거가 **폴더 이름 그 자체**라, 사용자가 확인 화면에서
        //   눈으로 검증할 수 있다("2026-06-01 이 지워지는구나").
        //   개수 기준(`최근 N개만 남김`)은 목록의 **정렬 순서에 의존**하므로,
        //   정렬이 한 번 어긋나면 가장 최신 로그를 지워 버릴 수 있다.
        //   그리고 지워진 로그는 복구되지 않는다.
        //   되돌릴 수 없는 동작에는 눈으로 검증 가능한 기준을 쓴다.
        //
        // ── 이 프로젝트의 삭제 사고 이력 ────────────────────────────────────
        //   CLAUDE.md 규칙 5(git 명령 절대 금지)는 2026-03-03 에 git restore 를
        //   무단 실행해 커밋되지 않은 작업 전체가 복구 불가로 소실된 사건에서 나왔다.
        //   "되돌릴 수 없는 삭제를 사용자 확인 없이 수행하지 않는다" 는 원칙이
        //   이 프로젝트에는 이미 서 있다. 아래 코드는 그 원칙을 그대로 따른다.
        // ====================================================================

        /// <summary>
        /// `_Logs/_editor/` 아래에 쌓인 오래된 날짜 폴더를 사용자 확인을 받아 삭제한다.
        ///
        /// 처리 흐름(각 단계에서 조건이 안 맞으면 즉시 안내 후 중단한다):
        ///   1) 정리 대상 뿌리 폴더가 실제로 있는지 확인
        ///   2) 뿌리 바로 아래 폴더들을 훑어 이름을 날짜로 해석 (해석 실패 폴더는 대상에서 제외)
        ///   3) 사용자에게 보존 기간(30일 / 90일)을 고르게 함 — 여기서 취소 가능
        ///   4) 기준일보다 오래된 폴더 목록을 그대로 보여 주고 최종 확인 — 여기서도 취소 가능
        ///   5) 삭제 직전에 폴더마다 "정말 _editor/ 바로 아래인가" 를 다시 검사한 뒤 삭제
        ///   6) AssetDatabase.Refresh() 로 에디터에 반영하고 결과를 보고
        /// </summary>
        [MenuItem("Hexiege/Logcat/3. 오래된 에디터 로그 정리", false, 13)]
        public static void CleanOldEditorLogs()
        {
            // ── 1) 정리 대상 뿌리 폴더 확인 ────────────────────────────────
            //   경로 계산 방식은 FileSink.BeginSession 과 **동일**하게 맞춘다.
            //   (UnityEngine.Application.dataPath = "<프로젝트>/Assets" + 상대 경로)
            //   ⚠️ Application 완전 수식이 필요한 이유는 위 SaveToFile 의 주석 참조.
            string rootAbsolute = Path.Combine(
                UnityEngine.Application.dataPath, EditorLogsRootRelativeToAssets);

            if (!Directory.Exists(rootAbsolute))
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "정리할 에디터 로그 폴더가 아직 없습니다.\n\n" +
                    $"확인한 경로: {ToProjectRelativePath(rootAbsolute)}\n\n" +
                    "에디터에서 플레이 모드에 한 번도 들어가지 않았다면 정상입니다.",
                    "확인");
                return;
            }

            // ── 2) 날짜 폴더 수집 ─────────────────────────────────────────
            List<EditorLogFolder> dated;
            List<string> skippedNames;
            if (!TryCollectDateFolders(rootAbsolute, out dated, out skippedNames)) return;

            if (dated.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "삭제 판정을 할 수 있는 날짜 폴더가 없습니다.\n\n" +
                    $"경로: {ToProjectRelativePath(rootAbsolute)}\n" +
                    $"이름이 날짜(yyyy-MM-dd) 형식이 아니라 제외한 폴더: {skippedNames.Count}개\n\n" +
                    "이 도구는 폴더 이름을 날짜로 읽어서 판정합니다.\n" +
                    "이름이 날짜 형식이 아닌 폴더는 안전을 위해 절대 건드리지 않습니다.",
                    "확인");
                return;
            }

            // 오래된 것이 위로 오도록 정렬한다(사용자가 확인 화면에서 읽는 순서).
            dated.Sort((a, b) => a.Date.CompareTo(b.Date));

            // ── 3) 보존 기간 선택 ─────────────────────────────────────────
            int retentionDays;
            if (!TryAskRetentionDays(rootAbsolute, dated, skippedNames.Count, out retentionDays)) return;

            // 기준일 = 오늘 자정에서 보존 기간만큼 뺀 날. 이 날짜"보다 이전"만 삭제한다.
            // DateTime.Now.Date 를 쓰므로 오늘 폴더는 어떤 경우에도 대상이 될 수 없다.
            DateTime cutoffDate = DateTime.Now.Date.AddDays(-retentionDays);

            var targets = new List<EditorLogFolder>();
            for (int i = 0; i < dated.Count; i++)
            {
                if (dated[i].Date < cutoffDate) targets.Add(dated[i]);
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    $"{cutoffDate.ToString(DateFolderFormat)} 보다 오래된 날짜 폴더가 없습니다.\n\n" +
                    $"보존 기간: {retentionDays}일\n" +
                    $"현재 날짜 폴더: {dated.Count}개 " +
                    $"({dated[0].Name} ~ {dated[dated.Count - 1].Name})\n\n" +
                    "아무것도 삭제하지 않았습니다.",
                    "확인");
                return;
            }

            // ── 4) 삭제 대상 목록을 보여 주고 최종 확인 ────────────────────
            if (!ConfirmDeletion(rootAbsolute, targets, retentionDays, cutoffDate, skippedNames.Count)) return;

            // ── 5) 삭제 (폴더마다 경로 가드를 다시 통과해야 한다) ──────────
            int deletedFolders = 0;
            int deletedFiles = 0;
            var failures = new List<string>();

            for (int i = 0; i < targets.Count; i++)
            {
                EditorLogFolder target = targets[i];

                // ★ 마지막 안전장치 — 삭제 직전에 "정말 _editor/ 바로 아래 폴더인가" 를 다시 본다.
                //   목록을 만든 뒤 삭제하기까지 사이에 값이 바뀌었을 가능성까지 차단한다.
                //   여기서 한 건이라도 걸리면 나머지도 진행하지 않고 통째로 중단한다.
                //   (범위를 벗어난 경로가 섞였다는 것은 계산 자체가 틀렸다는 뜻이고,
                //    그 상태로 삭제를 계속하는 것은 되돌릴 수 없는 사고가 된다)
                if (!IsInsideEditorLogsRoot(target.AbsolutePath, rootAbsolute))
                {
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog(
                        DialogTitle,
                        "정리 범위를 벗어난 경로가 감지되어 삭제를 중단했습니다.\n\n" +
                        $"문제의 경로: {target.AbsolutePath}\n" +
                        $"허용 범위: {rootAbsolute} 바로 아래 폴더만\n\n" +
                        $"여기까지 삭제된 폴더: {deletedFolders}개\n" +
                        "이 상황은 정상적으로는 발생하지 않습니다. 개발자에게 알려 주세요.",
                        "확인");
                    Debug.LogError(
                        $"[LogcatCapture] 정리 범위 이탈로 중단 — 경로={target.AbsolutePath}, 허용 범위={rootAbsolute}");
                    return;
                }

                string failureReason;
                if (DeleteFolderWithMeta(target.AbsolutePath, out failureReason))
                {
                    deletedFolders++;
                    deletedFiles += target.FileCount;
                }
                else
                {
                    failures.Add($"{target.Name} — {failureReason}");
                }
            }

            // ── 6) 에디터에 반영 + 결과 보고 ───────────────────────────────
            //   외부에서 지운 파일을 Unity 가 자동으로 알아채지 못할 때가 있다.
            //   Refresh 를 호출해야 프로젝트 창에서 폴더가 실제로 사라진다.
            AssetDatabase.Refresh();

            var report = new StringBuilder();
            report.AppendLine("오래된 에디터 로그 정리를 마쳤습니다.");
            report.AppendLine();
            report.AppendLine($"삭제한 날짜 폴더: {deletedFolders}개 (로그 파일 {deletedFiles}개)");
            report.AppendLine($"기준: {cutoffDate.ToString(DateFolderFormat)} 보다 이전 (보존 {retentionDays}일)");

            if (failures.Count > 0)
            {
                report.AppendLine();
                report.AppendLine($"지우지 못한 폴더: {failures.Count}개");
                for (int i = 0; i < failures.Count && i < MaxListedFolders; i++)
                    report.AppendLine($"  · {failures[i]}");
                if (failures.Count > MaxListedFolders)
                    report.AppendLine($"  ... 외 {failures.Count - MaxListedFolders}개");
                report.AppendLine();
                report.AppendLine("파일이 다른 프로그램에서 열려 있는지 확인한 뒤 다시 실행해 보세요.");
            }

            EditorUtility.DisplayDialog(DialogTitle, report.ToString(), "확인");
            Debug.Log(
                $"[LogcatCapture] 에디터 로그 정리 완료 — 삭제 {deletedFolders}폴더 / {deletedFiles}파일, " +
                $"실패 {failures.Count}건, 기준일={cutoffDate.ToString(DateFolderFormat)}");
        }

        // ====================================================================
        // 메뉴 3 보조 — 날짜 폴더 수집
        // ====================================================================

        /// <summary>정리 판정에 쓰는 날짜 폴더 하나의 정보.</summary>
        private struct EditorLogFolder
        {
            /// <summary>폴더의 절대 경로(삭제 대상).</summary>
            public string AbsolutePath;

            /// <summary>폴더 이름(= 날짜 문자열). 사용자에게 그대로 보여 주는 판정 근거다.</summary>
            public string Name;

            /// <summary>폴더 이름을 해석한 날짜. 이 값만으로 삭제 여부를 판정한다.</summary>
            public DateTime Date;

            /// <summary>폴더 안의 로그 파일 개수(.meta 제외). 사용자 확인 화면에 표시한다.</summary>
            public int FileCount;
        }

        /// <summary>
        /// 뿌리 폴더 바로 아래의 폴더들을 훑어, 이름이 날짜(yyyy-MM-dd)로 해석되는 것만 모은다.
        ///
        /// 이름이 날짜가 아닌 폴더를 왜 그냥 건너뛰는가:
        ///   이 도구의 판정 근거는 **폴더 이름**뿐이다(LogRules.md 1.10 ④).
        ///   이름을 읽을 수 없는 폴더는 "오래된 것인지" 를 판정할 방법이 없고,
        ///   판정할 수 없는 대상을 지우는 것은 규정 위반이자 사고의 원인이다.
        ///   그래서 대상에서 빼고, 몇 개를 뺐는지만 사용자에게 알린다.
        /// </summary>
        /// <param name="rootAbsolute">`_Logs/_editor/` 의 절대 경로.</param>
        /// <param name="dated">날짜로 해석된 폴더 목록(출력).</param>
        /// <param name="skippedNames">날짜로 해석되지 않아 제외한 폴더 이름 목록(출력).</param>
        /// <returns>목록 조사에 성공하면 true. 실패하면 안내 후 false.</returns>
        private static bool TryCollectDateFolders(
            string rootAbsolute, out List<EditorLogFolder> dated, out List<string> skippedNames)
        {
            dated = new List<EditorLogFolder>();
            skippedNames = new List<string>();

            string[] subDirectories;
            try
            {
                // SearchOption 기본값은 TopDirectoryOnly — 뿌리 "바로 아래" 한 단계만 본다.
                // 하위까지 재귀로 훑으면 날짜 폴더 안의 폴더까지 후보가 되어 위험해진다.
                subDirectories = Directory.GetDirectories(rootAbsolute);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "로그 폴더 목록을 읽지 못했습니다.\n\n" +
                    $"경로: {rootAbsolute}\n" +
                    $"원인: {e.Message}",
                    "확인");
                return false;
            }

            for (int i = 0; i < subDirectories.Length; i++)
            {
                string folderName = Path.GetFileName(subDirectories[i].TrimEnd('/', '\\'));

                // InvariantCulture 를 쓰는 이유:
                //   폴더 이름은 FileSink 가 만든 고정 형식 문자열이지 사용자 지역 설정과 무관하다.
                //   현재 문화권으로 해석하면 PC 설정에 따라 같은 이름이 다르게 읽힐 수 있다.
                DateTime parsed;
                bool ok = DateTime.TryParseExact(
                    folderName, DateFolderFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed);

                if (!ok)
                {
                    skippedNames.Add(folderName);
                    continue;
                }

                dated.Add(new EditorLogFolder
                {
                    AbsolutePath = subDirectories[i],
                    Name = folderName,
                    Date = parsed.Date,
                    FileCount = CountLogFiles(subDirectories[i])
                });
            }

            return true;
        }

        /// <summary>
        /// 폴더 안의 로그 파일 개수를 센다(.meta 는 유니티가 만드는 부속 파일이라 제외).
        /// 세다가 실패해도 정리 작업 자체를 막지 않는다 — 개수는 표시용 정보일 뿐이다.
        /// </summary>
        /// <param name="folderAbsolute">셀 폴더의 절대 경로.</param>
        /// <returns>파일 개수(실패 시 0).</returns>
        private static int CountLogFiles(string folderAbsolute)
        {
            try
            {
                string[] files = Directory.GetFiles(folderAbsolute, "*", SearchOption.AllDirectories);
                int count = 0;
                for (int i = 0; i < files.Length; i++)
                {
                    if (files[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    count++;
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        // ====================================================================
        // 메뉴 3 보조 — 사용자 확인 (2단계)
        // ====================================================================

        /// <summary>
        /// 1단계 확인 — 보존 기간을 고르게 한다. 여기서 취소하면 아무것도 지우지 않는다.
        ///
        /// 왜 자유 입력이 아니라 버튼 두 개인가:
        ///   유니티 기본 API 에는 텍스트 입력 대화상자가 없어서 별도 창을 만들어야 하는데,
        ///   되돌릴 수 없는 삭제에서 "아무 숫자나 입력할 수 있는 칸" 은 오히려 위험하다.
        ///   (0 이나 1 을 입력하면 어제 로그까지 사라진다)
        ///   미리 정한 넉넉한 값 두 개 중에서 고르게 하면 그 사고가 구조적으로 불가능해진다.
        /// </summary>
        /// <param name="rootAbsolute">`_Logs/_editor/` 의 절대 경로(안내문 표시용).</param>
        /// <param name="dated">수집된 날짜 폴더 목록(오름차순 정렬 상태).</param>
        /// <param name="skippedCount">날짜로 해석되지 않아 제외한 폴더 개수.</param>
        /// <param name="retentionDays">사용자가 고른 보존 기간(출력).</param>
        /// <returns>사용자가 진행을 골랐으면 true, 취소했으면 false.</returns>
        private static bool TryAskRetentionDays(
            string rootAbsolute, List<EditorLogFolder> dated, int skippedCount, out int retentionDays)
        {
            retentionDays = 0;

            var msg = new StringBuilder();
            msg.AppendLine("에디터 상시 로그를 날짜 폴더 단위로 정리합니다.");
            msg.AppendLine();
            msg.AppendLine($"정리 범위: {ToProjectRelativePath(rootAbsolute)}/");
            msg.AppendLine($"현재 날짜 폴더: {dated.Count}개 ({dated[0].Name} ~ {dated[dated.Count - 1].Name})");
            if (skippedCount > 0)
                msg.AppendLine($"이름이 날짜 형식이 아니라 대상에서 제외한 폴더: {skippedCount}개");
            msg.AppendLine();
            msg.AppendLine("며칠보다 오래된 폴더를 지울까요?");
            msg.AppendLine("고른 기간보다 오래된 폴더만 대상이 되며, 오늘 폴더는 어떤 경우에도 지우지 않습니다.");
            msg.AppendLine();
            msg.AppendLine("다음 화면에서 삭제 대상 목록을 확인한 뒤 최종 결정할 수 있습니다.");

            // DisplayDialogComplex 의 반환값: 0=ok(첫 버튼), 1=cancel(둘째), 2=alt(셋째)
            int choice = EditorUtility.DisplayDialogComplex(
                DialogTitle,
                msg.ToString(),
                $"{DefaultRetentionDays}일보다 오래된 것",   // 0
                "취소",                                      // 1
                $"{LongRetentionDays}일보다 오래된 것");     // 2

            if (choice == 1) return false;

            retentionDays = (choice == 2) ? LongRetentionDays : DefaultRetentionDays;
            return true;
        }

        /// <summary>
        /// 2단계 확인 — 실제로 지울 폴더 목록을 그대로 보여 주고 마지막 동의를 받는다.
        /// 이 대화상자에서 [삭제] 를 누르기 전에는 파일이 하나도 지워지지 않는다
        /// (LogRules.md 1.10 「사용자 확인」).
        /// </summary>
        /// <param name="rootAbsolute">`_Logs/_editor/` 의 절대 경로(안내문 표시용).</param>
        /// <param name="targets">삭제 대상 폴더 목록.</param>
        /// <param name="retentionDays">사용자가 고른 보존 기간.</param>
        /// <param name="cutoffDate">이 날짜보다 이전 폴더가 대상이다.</param>
        /// <param name="skippedCount">날짜로 해석되지 않아 제외한 폴더 개수.</param>
        /// <returns>사용자가 삭제에 동의하면 true.</returns>
        private static bool ConfirmDeletion(
            string rootAbsolute, List<EditorLogFolder> targets,
            int retentionDays, DateTime cutoffDate, int skippedCount)
        {
            int totalFiles = 0;
            for (int i = 0; i < targets.Count; i++) totalFiles += targets[i].FileCount;

            var msg = new StringBuilder();
            msg.AppendLine($"아래 {targets.Count}개 날짜 폴더를 삭제합니다. 되돌릴 수 없습니다.");
            msg.AppendLine();
            msg.AppendLine($"정리 범위: {ToProjectRelativePath(rootAbsolute)}/");
            msg.AppendLine($"기준: {cutoffDate.ToString(DateFolderFormat)} 보다 이전 (보존 {retentionDays}일)");
            msg.AppendLine();
            msg.AppendLine("삭제 대상:");

            // 목록이 길면 대화상자가 화면을 넘어가므로 앞부분만 나열한다.
            // 다만 **총 개수는 어떤 경우에도 표시**한다 — 사용자가 규모를 모른 채 동의하면 안 된다.
            for (int i = 0; i < targets.Count && i < MaxListedFolders; i++)
                msg.AppendLine($"  · {targets[i].Name}  (파일 {targets[i].FileCount}개)");

            if (targets.Count > MaxListedFolders)
                msg.AppendLine($"  ... 외 {targets.Count - MaxListedFolders}개 폴더");

            msg.AppendLine();
            msg.AppendLine($"합계: 폴더 {targets.Count}개 / 로그 파일 {totalFiles}개");
            if (skippedCount > 0)
                msg.AppendLine($"(이름이 날짜 형식이 아닌 폴더 {skippedCount}개는 건드리지 않습니다)");
            msg.AppendLine();
            msg.AppendLine("⚠️ 공유하려고 커밋해 둔 로그 파일이 이 안에 있다면 함께 사라집니다.");
            msg.AppendLine("   남겨야 할 로그가 있는지 목록을 확인한 뒤 진행하세요.");

            return EditorUtility.DisplayDialog(DialogTitle, msg.ToString(), "삭제", "취소");
        }

        // ====================================================================
        // 메뉴 3 보조 — 경로 가드와 실제 삭제
        // ====================================================================

        /// <summary>
        /// 주어진 경로가 `_Logs/_editor/` **바로 아래의 폴더**인지 검사한다.
        /// 이 검사를 통과하지 못한 경로는 절대 삭제하지 않는다(LogRules.md 1.10 「정리 범위」).
        ///
        /// 세 가지를 모두 확인한다:
        ///   ① Path.GetFullPath 로 정규화한다 — "..\" 같은 상위 폴더 탈출 표기가
        ///      여기서 실제 경로로 펼쳐지므로, 문자열 비교만으로 빠져나갈 수 없다.
        ///      (예: ".../_editor/../../Scripts" 는 ".../Assets/_Project/Scripts" 로 펼쳐져
        ///       아래 StartsWith 검사에서 탈락한다)
        ///   ② 뿌리 폴더 그 자체는 대상이 될 수 없다 — _editor/ 통째로 지우는 사고 방지.
        ///   ③ 뿌리 "바로 아래" 한 단계여야 한다 — 날짜 폴더는 항상 한 단계 아래이고,
        ///      더 깊은 경로가 대상이 되는 것은 계산이 틀렸다는 뜻이다.
        /// </summary>
        /// <param name="candidateAbsolute">검사할 경로.</param>
        /// <param name="rootAbsolute">허용 범위인 `_Logs/_editor/` 의 절대 경로.</param>
        /// <returns>삭제해도 되는 위치면 true.</returns>
        private static bool IsInsideEditorLogsRoot(string candidateAbsolute, string rootAbsolute)
        {
            string root = NormalizeFullPath(rootAbsolute);
            string candidate = NormalizeFullPath(candidateAbsolute);

            // 정규화 자체가 실패했다면(잘못된 문자 등) 판정할 수 없으므로 거부한다.
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(candidate)) return false;

            // ② 뿌리 그 자체 거부
            if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)) return false;

            // ① 반드시 "뿌리/" 로 시작해야 한다.
            //   구분자 '/' 를 붙여 비교하는 이유: 붙이지 않으면 "_editor_backup" 같은
            //   형제 폴더가 "_editor" 로 시작한다는 이유만으로 통과해 버린다.
            string rootWithSeparator = root + "/";
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) return false;

            // ③ 뿌리 바로 아래 한 단계인지 — 남은 부분에 '/' 가 더 있으면 더 깊은 경로다.
            string remainder = candidate.Substring(rootWithSeparator.Length);
            return remainder.Length > 0 && remainder.IndexOf('/') < 0;
        }

        /// <summary>
        /// 경로를 비교 가능한 형태로 정규화한다.
        /// (상대 표기·"..\" 를 실제 경로로 펼치고, 구분자를 '/' 로 통일하고, 끝의 구분자를 없앤다)
        /// </summary>
        /// <param name="path">원본 경로.</param>
        /// <returns>정규화된 절대 경로. 정규화할 수 없으면 null.</returns>
        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
            }
            catch
            {
                // 경로에 허용되지 않는 문자가 있는 등, 펼칠 수 없는 경우.
                // 판정 불가 = 삭제 불가로 이어지도록 null 을 돌려준다.
                return null;
            }
        }

        /// <summary>
        /// 날짜 폴더 하나를 .meta 파일까지 함께 삭제한다.
        ///
        /// 왜 AssetDatabase.DeleteAsset 을 먼저 쓰는가:
        ///   이 폴더는 Assets/ 안에 있어서 유니티가 짝이 되는 `.meta` 파일을 함께 관리한다.
        ///   · AssetDatabase.DeleteAsset — 에셋과 `.meta` 를 **함께** 지우고 에셋 DB 도 정리한다.
        ///   · FileUtil.DeleteFileOrDirectory — 지정한 대상만 지운다. 폴더만 넘기면
        ///     `2026-06-01.meta` 가 그대로 남아 "짝 잃은 meta" 가 된다.
        ///   이 프로젝트는 _Tasks/_Logs 문서의 `.meta` 도 커밋하므로, 남은 meta 는
        ///   그대로 커밋 목록에 올라와 혼란을 만든다. 그래서 DeleteAsset 이 정답이다.
        ///
        /// 그래도 폴백을 두는 이유:
        ///   DeleteAsset 은 경로가 에셋으로 인식되지 않거나 파일이 잠겨 있으면 false 만 돌려준다.
        ///   그 경우 직접 지우되, `.meta` 도 **명시적으로 함께** 지워 위 문제를 피한다.
        /// </summary>
        /// <param name="folderAbsolute">삭제할 폴더의 절대 경로(경로 가드를 이미 통과한 값이어야 한다).</param>
        /// <param name="failureReason">실패했을 때의 이유(성공이면 null).</param>
        /// <returns>삭제에 성공하면 true.</returns>
        private static bool DeleteFolderWithMeta(string folderAbsolute, out string failureReason)
        {
            failureReason = null;

            // AssetDatabase 계열 API 는 "Assets/..." 형태의 프로젝트 상대 경로만 받는다.
            string projectRelative = ToProjectRelativePath(folderAbsolute);

            if (projectRelative.StartsWith("Assets/", StringComparison.Ordinal) &&
                AssetDatabase.DeleteAsset(projectRelative))
            {
                return true;
            }

            // ── 폴백 ──
            try
            {
                if (Directory.Exists(folderAbsolute))
                    FileUtil.DeleteFileOrDirectory(folderAbsolute);

                // 폴더의 .meta 는 폴더와 같은 위치에 "<폴더이름>.meta" 로 존재한다.
                string metaPath = folderAbsolute + ".meta";
                if (File.Exists(metaPath))
                    FileUtil.DeleteFileOrDirectory(metaPath);

                if (Directory.Exists(folderAbsolute))
                {
                    failureReason = "폴더가 그대로 남아 있습니다(다른 프로그램이 파일을 열고 있을 수 있습니다)";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                failureReason = e.Message;
                return false;
            }
        }

        // ====================================================================
        // adb 실행 파일 찾기
        // ====================================================================

        /// <summary>
        /// adb 실행 파일을 찾는다. 못 찾으면 사용자에게 안내 대화상자를 띄우고 false 를 반환한다.
        ///
        /// 왜 여러 곳을 뒤지는가:
        ///   adb 는 Android SDK 에 들어 있는 도구인데, 설치 방식에 따라 위치가 제각각이고
        ///   환경변수 PATH 에 등록되어 있지 않은 경우가 아주 흔하다.
        ///   (Unity Hub 로 Android Build Support 를 설치하면 유니티 안쪽에 조용히 깔린다.)
        ///   그래서 "가능성이 높은 순서대로" 차례로 확인한다.
        ///
        ///   1) Unity 가 기억하는 Android SDK 경로(EditorPrefs "AndroidSdkRoot")
        ///      → Preferences > External Tools 에서 지정한 값. 가장 정확하다.
        ///   2) 유니티에 번들로 딸려 온 SDK(PlaybackEngines/AndroidPlayer/SDK)
        ///      → Android Build Support 를 Hub 로 깔았다면 여기에 있다.
        ///   3) 환경변수 ANDROID_HOME / ANDROID_SDK_ROOT
        ///      → Android Studio 를 따로 설치한 사람들이 흔히 갖고 있는 값.
        ///   4) PATH 에 등록된 adb (그냥 "adb" 로 실행 시도)
        ///
        /// ⚠️ UnityEditor.Android 네임스페이스(AndroidExternalToolsSettings 등)는 쓰지 않는다.
        ///    Android 빌드 모듈이 설치돼 있지 않은 PC 에서는 그 어셈블리가 아예 없어서
        ///    이 스크립트뿐 아니라 프로젝트 전체 컴파일이 깨진다.
        ///    EditorPrefs 와 경로 조사만으로 처리하면 모듈 유무와 무관하게 항상 컴파일된다.
        /// </summary>
        /// <param name="adbPath">찾아낸 adb 경로(또는 PATH 사용 시 "adb"/"adb.exe").</param>
        /// <returns>찾았으면 true.</returns>
        private static bool TryResolveAdb(out string adbPath)
        {
            adbPath = null;

            // 운영체제마다 실행 파일 이름이 다르다. 윈도우만 .exe 확장자를 쓴다.
            // (Application 을 완전 수식하는 이유는 위 CaptureLogcat 의 주석 참조)
            bool isWindows = UnityEngine.Application.platform == RuntimePlatform.WindowsEditor;
            string exeName = isWindows ? "adb.exe" : "adb";

            // 어디를 뒤졌는지 사용자에게 보여 주기 위해 기록해 둔다(조용히 실패하지 않기 위함).
            var searched = new List<string>();

            // ── 1) Unity 가 기억하는 SDK 경로 ──────────────────────────────
            string sdkFromPrefs = EditorPrefs.GetString("AndroidSdkRoot", string.Empty);
            if (TryAdbInSdk(sdkFromPrefs, exeName, searched, out adbPath)) return true;

            // ── 2) 유니티 번들 SDK ─────────────────────────────────────────
            //   EditorApplication.applicationContentsPath
            //     Windows : ...\Editor\Data
            //     macOS   : /Applications/Unity/.../Unity.app/Contents
            string bundledSdk = Path.Combine(
                EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK");
            if (TryAdbInSdk(bundledSdk, exeName, searched, out adbPath)) return true;

            // ── 3) 환경변수로 지정된 SDK ───────────────────────────────────
            if (TryAdbInSdk(Environment.GetEnvironmentVariable("ANDROID_HOME"), exeName, searched, out adbPath)) return true;
            if (TryAdbInSdk(Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"), exeName, searched, out adbPath)) return true;

            // ── 4) PATH 에 등록된 adb ──────────────────────────────────────
            //   경로를 알 수 없으므로 파일 존재 확인이 불가능하다.
            //   대신 실제로 `adb version` 을 한 번 실행해 보고, 프로세스가 뜨면 사용 가능한 것으로 본다.
            searched.Add("환경변수 PATH 에 등록된 adb");
            AdbResult probe = RunAdb(exeName, null, "version", ShortTimeoutMs, null);
            if (probe.Success)
            {
                adbPath = exeName;
                return true;
            }

            // ── 전부 실패 → 무엇을 해야 하는지 구체적으로 안내 ─────────────
            var sb = new StringBuilder();
            sb.AppendLine("adb(Android Debug Bridge) 실행 파일을 찾지 못했습니다.");
            sb.AppendLine();
            sb.AppendLine("아래 위치를 확인했습니다:");
            for (int i = 0; i < searched.Count; i++) sb.AppendLine($"  {i + 1}. {searched[i]}");
            sb.AppendLine();
            sb.AppendLine("해결 방법 (둘 중 하나):");
            sb.AppendLine("  A) Unity Hub > 설치 > 현재 에디터 > 모듈 추가 에서");
            sb.AppendLine("     'Android Build Support' (하위의 Android SDK & NDK Tools 포함) 를 설치합니다.");
            sb.AppendLine("  B) Android Studio 등으로 SDK 를 이미 설치했다면");
            sb.AppendLine("     Unity 메뉴 Edit > Preferences > External Tools 에서");
            sb.AppendLine("     Android SDK 경로를 직접 지정합니다.");
            sb.AppendLine();
            sb.AppendLine("설정 후 이 메뉴를 다시 실행하세요.");

            EditorUtility.DisplayDialog(DialogTitle, sb.ToString(), "확인");
            return false;
        }

        /// <summary>
        /// SDK 루트 경로 아래 platform-tools 에 adb 가 실제로 있는지 확인한다.
        /// 확인한 경로는 searched 목록에 남겨, 실패 시 사용자에게 그대로 보여 준다.
        /// </summary>
        /// <param name="sdkRoot">Android SDK 루트 경로(비어 있으면 건너뛴다).</param>
        /// <param name="exeName">플랫폼별 실행 파일 이름("adb.exe" 또는 "adb").</param>
        /// <param name="searched">확인한 경로를 누적할 목록.</param>
        /// <param name="adbPath">찾았을 때의 전체 경로.</param>
        /// <returns>파일이 실제로 존재하면 true.</returns>
        private static bool TryAdbInSdk(string sdkRoot, string exeName, List<string> searched, out string adbPath)
        {
            adbPath = null;
            if (string.IsNullOrEmpty(sdkRoot)) return false;

            string candidate = Path.Combine(sdkRoot, "platform-tools", exeName);
            searched.Add(candidate);

            if (!File.Exists(candidate)) return false;

            adbPath = candidate;
            return true;
        }

        // ====================================================================
        // 연결된 기기 찾기
        // ====================================================================

        /// <summary>연결된 기기 한 대의 정보(직렬번호 / 상태 / 모델명).</summary>
        private struct DeviceEntry
        {
            /// <summary>기기 고유 직렬번호. adb -s 옵션에 그대로 넘긴다.</summary>
            public string Serial;

            /// <summary>adb 가 보고하는 상태 문자열("device" / "unauthorized" / "offline" 등).</summary>
            public string State;

            /// <summary>모델명 등 사람이 알아보기 쉬운 부가 설명(없을 수 있다).</summary>
            public string Model;

            /// <summary>대화상자·로그에 표시할 한 줄 라벨.</summary>
            public string Label => string.IsNullOrEmpty(Model) ? Serial : $"{Model} ({Serial})";
        }

        /// <summary>
        /// 명령을 보낼 대상 기기를 결정한다. 결정하지 못하면 안내 대화상자를 띄우고 false 를 반환한다.
        ///
        /// 기기가 여러 대일 때의 정책(중요):
        ///   그냥 진행하지 않는다. 어느 기기의 로그인지 모른 채 저장된 파일은
        ///   "형식은 멀쩡한데 내용이 엉뚱한" 최악의 자료가 되어, 이 도구를 만든 이유(잘못된/부분적인
        ///   로그로 원인 추적이 늦어짐)를 그대로 재현한다.
        ///   그래서 목록을 전부 보여 주고, 사용자가 [첫 번째 기기 사용] 을 직접 눌렀을 때만 진행한다.
        ///   선택한 기기는 이후 모든 안내문과 로그 파일 헤더에 항상 함께 표시한다.
        /// </summary>
        /// <param name="adbPath">앞서 찾은 adb 경로.</param>
        /// <param name="serial">선택된 기기의 직렬번호(1대뿐이어도 명시적으로 지정한다).</param>
        /// <param name="deviceLabel">표시용 기기 라벨.</param>
        /// <returns>대상이 확정되면 true.</returns>
        private static bool TryResolveDevice(string adbPath, out string serial, out string deviceLabel)
        {
            serial = null;
            deviceLabel = null;

            // -l 은 모델명 등 부가 정보까지 함께 출력한다(기기 구분에 필요).
            AdbResult result = RunAdb(adbPath, null, "devices -l", ShortTimeoutMs, "연결된 기기를 확인하는 중...");
            if (!result.Success)
            {
                ShowAdbFailure("기기 목록 조회", result);
                return false;
            }

            List<DeviceEntry> all = ParseDevices(result.StandardOutput);

            // 정상 상태("device")인 기기만 실제 대상이 될 수 있다.
            var ready = new List<DeviceEntry>();
            var notReady = new List<DeviceEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].State == "device") ready.Add(all[i]);
                else notReady.Add(all[i]);
            }

            // ── 사용할 수 있는 기기가 없음 ─────────────────────────────────
            if (ready.Count == 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("사용할 수 있는 Android 기기가 없습니다.");
                sb.AppendLine();

                if (notReady.Count > 0)
                {
                    sb.AppendLine("연결은 감지되었지만 아직 사용할 수 없는 기기:");
                    for (int i = 0; i < notReady.Count; i++)
                        sb.AppendLine($"  · {notReady[i].Label} — 상태: {notReady[i].State}");
                    sb.AppendLine();
                    sb.AppendLine("상태별 조치:");
                    sb.AppendLine("  · unauthorized : 기기 화면에 뜬 'USB 디버깅을 허용하시겠습니까?' 팝업에서");
                    sb.AppendLine("                   [항상 허용] 을 눌러 주세요. 팝업이 안 보이면 케이블을 뽑았다 다시 꽂습니다.");
                    sb.AppendLine("  · offline      : 케이블을 다시 연결하거나 기기를 재부팅해 보세요.");
                }
                else
                {
                    sb.AppendLine("확인할 사항:");
                    sb.AppendLine("  1) USB 케이블이 PC 와 기기에 제대로 연결되어 있나요?");
                    sb.AppendLine("     (충전 전용 케이블은 데이터 전송이 안 됩니다)");
                    sb.AppendLine("  2) 기기 설정 > 개발자 옵션 > 'USB 디버깅' 이 켜져 있나요?");
                    sb.AppendLine("     개발자 옵션이 안 보이면 설정 > 휴대전화 정보 > 빌드번호 를 7번 누릅니다.");
                    sb.AppendLine("  3) 기기 알림창의 USB 모드가 '파일 전송(MTP)' 또는 '충전 외' 로 되어 있나요?");
                }

                EditorUtility.DisplayDialog(DialogTitle, sb.ToString(), "확인");
                return false;
            }

            // ── 딱 한 대 → 그대로 사용(직렬번호를 명시해 모호함 제거) ──────
            if (ready.Count == 1)
            {
                serial = ready[0].Serial;
                deviceLabel = ready[0].Label;
                return true;
            }

            // ── 여러 대 → 사용자에게 확인받기 전에는 절대 진행하지 않는다 ───
            var msg = new StringBuilder();
            msg.AppendLine($"기기가 {ready.Count}대 연결되어 있습니다. 어느 기기의 로그인지 확정할 수 없습니다.");
            msg.AppendLine();
            msg.AppendLine("연결된 기기:");
            for (int i = 0; i < ready.Count; i++)
                msg.AppendLine($"  {i + 1}. {ready[i].Label}");
            msg.AppendLine();
            msg.AppendLine($"[{ready[0].Label}] 을(를) 대상으로 진행할까요?");
            msg.AppendLine();
            msg.AppendLine("다른 기기를 쓰려면 [취소] 후 나머지 기기의 USB 케이블을 뽑고 다시 실행하세요.");

            bool useFirst = EditorUtility.DisplayDialog(
                DialogTitle, msg.ToString(), "첫 번째 기기 사용", "취소");

            if (!useFirst) return false;

            serial = ready[0].Serial;
            deviceLabel = ready[0].Label;
            return true;
        }

        /// <summary>
        /// `adb devices -l` 의 출력 텍스트를 기기 목록으로 해석한다.
        ///
        /// 출력 예시:
        ///   List of devices attached
        ///   R3CN90ABCD    device product:x1q model:SM_G981N device:x1q transport_id:1
        ///   emulator-5554 device
        ///   ZY223KABCD    unauthorized
        /// </summary>
        /// <param name="output">adb 표준 출력 전체.</param>
        /// <returns>해석된 기기 목록(없으면 빈 목록).</returns>
        private static List<DeviceEntry> ParseDevices(string output)
        {
            var list = new List<DeviceEntry>();
            if (string.IsNullOrEmpty(output)) return list;

            string[] lines = output.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Length == 0) continue;
                if (line.StartsWith("List of devices")) continue;
                if (line.StartsWith("*")) continue; // "* daemon started successfully" 같은 안내 줄.

                // 공백/탭으로 잘라 [0]=직렬번호, [1]=상태, 나머지는 key:value 부가정보.
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var entry = new DeviceEntry { Serial = parts[0], State = parts[1], Model = null };

                for (int p = 2; p < parts.Length; p++)
                {
                    if (!parts[p].StartsWith("model:")) continue;
                    entry.Model = parts[p].Substring("model:".Length);
                    break;
                }

                list.Add(entry);
            }

            return list;
        }

        // ====================================================================
        // adb 프로세스 실행
        // ====================================================================

        /// <summary>adb 실행 결과를 담는 구조체.</summary>
        private struct AdbResult
        {
            /// <summary>프로세스가 정상적으로 끝났고 종료 코드가 0이면 true.</summary>
            public bool Success;

            /// <summary>표준 출력 전체(로그 본문 등).</summary>
            public string StandardOutput;

            /// <summary>표준 오류 전체(adb 가 남긴 오류 메시지).</summary>
            public string StandardError;

            /// <summary>프로세스 종료 코드(실행 자체를 못 했으면 -1).</summary>
            public int ExitCode;

            /// <summary>실행 자체가 실패한 경우의 설명(실행 파일 없음 / 시간 초과 등). 정상이면 null.</summary>
            public string FailureReason;

            /// <summary>실제로 실행한 명령줄(오류 안내에 그대로 보여 준다).</summary>
            public string CommandLine;
        }

        /// <summary>
        /// adb 를 실행하고 표준 출력·오류를 모두 회수한다.
        ///
        /// 왜 출력을 "비동기 이벤트"로 읽고 나서 WaitForExit 을 하는가(중요):
        ///   프로세스의 출력은 운영체제가 만든 파이프(작은 버퍼)를 거쳐 전달된다.
        ///   버퍼가 가득 차면 adb 는 "누군가 읽어 갈 때까지" 쓰기를 멈추고 대기한다.
        ///   그런데 우리가 먼저 WaitForExit() 으로 "끝날 때까지 기다리고 있으면",
        ///   adb 는 우리가 읽어 주기를 기다리고 우리는 adb 가 끝나기를 기다리는
        ///   교착 상태(deadlock)에 빠져 에디터가 통째로 멈춘다.
        ///   Logcat 출력은 수 MB 가 되기도 해서 이 문제가 실제로 발생한다.
        ///   그래서 OutputDataReceived / ErrorDataReceived 로 "나오는 즉시 계속 비워 주고",
        ///   그 뒤에 종료를 기다린다.
        ///
        ///   또한 시간 제한이 있는 WaitForExit(ms) 가 true 를 돌려준 뒤에는
        ///   인자 없는 WaitForExit() 을 한 번 더 호출한다. 이래야 비동기로 읽던 출력이
        ///   마지막까지 전부 도착한 것이 보장된다(.NET 의 정해진 사용법).
        /// </summary>
        /// <param name="adbPath">adb 실행 파일 경로(또는 PATH 를 쓸 때는 "adb"/"adb.exe").</param>
        /// <param name="serial">대상 기기 직렬번호(null 이면 기기 지정 없이 실행).</param>
        /// <param name="arguments">adb 에 넘길 인자 문자열.</param>
        /// <param name="timeoutMs">제한 시간(밀리초). 넘으면 프로세스를 강제 종료한다.</param>
        /// <param name="progressMessage">진행 표시줄에 띄울 문구(null 이면 표시하지 않음).</param>
        /// <returns>실행 결과.</returns>
        private static AdbResult RunAdb(
            string adbPath, string serial, string arguments, int timeoutMs, string progressMessage)
        {
            // `adb -s <직렬번호> <명령>` 순서다. -s 는 반드시 하위 명령보다 앞에 온다.
            // ⚠️ logcat 의 -s 옵션(태그 필터 축약)과 이름만 같을 뿐 전혀 다른 옵션이다. 위치로 구분된다.
            string fullArgs = string.IsNullOrEmpty(serial) ? arguments : $"-s {serial} {arguments}";

            var result = new AdbResult
            {
                Success = false,
                StandardOutput = string.Empty,
                StandardError = string.Empty,
                ExitCode = -1,
                FailureReason = null,
                CommandLine = $"\"{adbPath}\" {fullArgs}"
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            object gate = new object(); // 출력 이벤트는 다른 스레드에서 오므로 잠금이 필요하다.

            try
            {
                if (!string.IsNullOrEmpty(progressMessage))
                    EditorUtility.DisplayProgressBar(DialogTitle, progressMessage, 0.5f);

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = fullArgs,
                    UseShellExecute = false,   // 셸을 거치지 않고 직접 실행(출력 회수를 위해 필수).
                    CreateNoWindow = true,     // 검은 콘솔 창이 깜빡이지 않게 한다.
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    // Logcat 메시지에 한글이 섞여 있으므로 UTF-8 로 해석해야 글자가 깨지지 않는다.
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo = startInfo;

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null) return; // null 은 "스트림 끝" 신호다.
                        lock (gate) stdout.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null) return;
                        lock (gate) stderr.AppendLine(e.Data);
                    };

                    process.Start();

                    // 여기서부터 출력이 도착하는 즉시 위 핸들러가 호출되어 파이프를 계속 비워 준다.
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(timeoutMs))
                    {
                        // 제한 시간 초과 — 매달려 있으면 에디터가 멈추므로 강제 종료한다.
                        try { process.Kill(); } catch { /* 이미 끝났을 수 있다 — 무시 */ }

                        result.FailureReason =
                            $"adb 응답이 {timeoutMs / 1000}초 안에 오지 않아 중단했습니다.\n" +
                            "USB 연결이 불안정하거나 adb 서버가 멈춰 있을 수 있습니다.";
                        lock (gate)
                        {
                            result.StandardOutput = stdout.ToString();
                            result.StandardError = stderr.ToString();
                        }
                        return result;
                    }

                    // 시간 안에 끝났다 → 비동기로 읽던 출력이 전부 도착할 때까지 한 번 더 기다린다.
                    process.WaitForExit();

                    lock (gate)
                    {
                        result.StandardOutput = stdout.ToString();
                        result.StandardError = stderr.ToString();
                    }
                    result.ExitCode = process.ExitCode;
                    result.Success = process.ExitCode == 0;

                    if (!result.Success)
                        result.FailureReason = $"adb 가 오류 코드 {process.ExitCode} 로 종료했습니다.";
                }
            }
            catch (Exception e)
            {
                // 실행 파일이 없을 때(Win32Exception) 등 프로세스를 아예 띄우지 못한 경우.
                result.FailureReason = $"adb 를 실행하지 못했습니다: {e.Message}";
            }
            finally
            {
                if (!string.IsNullOrEmpty(progressMessage)) EditorUtility.ClearProgressBar();
            }

            return result;
        }

        /// <summary>
        /// adb 실행이 실패했을 때 원인을 그대로 보여 준다.
        /// 조용히 넘어가면 사용자는 "메뉴를 눌렀는데 아무 일도 안 일어났다" 고만 느끼게 된다.
        /// </summary>
        /// <param name="operationName">실패한 작업 이름(대화상자 문구용).</param>
        /// <param name="result">실패한 실행 결과.</param>
        private static void ShowAdbFailure(string operationName, AdbResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{operationName}에 실패했습니다.");
            sb.AppendLine();
            sb.AppendLine($"실행한 명령:\n{result.CommandLine}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(result.FailureReason))
            {
                sb.AppendLine($"원인: {result.FailureReason}");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                // adb 의 오류 메시지는 영어지만, 그대로 보여 주는 편이 검색·판단에 훨씬 유리하다.
                sb.AppendLine("adb 오류 출력:");
                sb.AppendLine(Truncate(result.StandardError.Trim(), 1200));
                sb.AppendLine();
            }

            sb.AppendLine("자주 있는 원인:");
            sb.AppendLine("  · 케이블이 중간에 빠졌거나 기기가 잠겨 화면이 꺼졌다");
            sb.AppendLine("  · 다른 프로그램(Android Studio, 스크린 미러링 앱 등)이 adb 를 붙잡고 있다");
            sb.AppendLine("  · adb 서버가 멈췄다 → 터미널에서 'adb kill-server' 후 다시 시도");

            EditorUtility.DisplayDialog(DialogTitle, sb.ToString(), "확인");
            Debug.LogWarning($"[LogcatCapture] {operationName} 실패 — {result.CommandLine}\n{result.FailureReason}\n{result.StandardError}");
        }

        // ====================================================================
        // 보조 유틸리티
        // ====================================================================

        /// <summary>`-b main -b system -b crash` 형태의 버퍼 인자 문자열을 만든다.</summary>
        /// <returns>조립된 인자 문자열.</returns>
        private static string BuildBufferArgs()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < LogBuffers.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append("-b ").Append(LogBuffers[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 같은 폴더에 이미 파일이 있으면 절대 덮어쓰지 않고 번호를 붙인 새 이름을 만든다.
        /// (같은 분(分)에 두 번 캡처하면 폴더 이름 {HH_mm}_logcat 이 같아지기 때문에 필요하다.)
        /// 예: RuntimeLog_device.txt → RuntimeLog_device_2.txt → RuntimeLog_device_3.txt
        /// </summary>
        /// <param name="folder">저장 폴더의 절대 경로.</param>
        /// <param name="baseName">확장자를 뺀 파일 이름.</param>
        /// <param name="extension">점을 포함한 확장자(예: ".txt").</param>
        /// <returns>아직 존재하지 않는 파일의 절대 경로.</returns>
        private static string MakeNonOverwritingPath(string folder, string baseName, string extension)
        {
            string candidate = Path.Combine(folder, baseName + extension);
            int index = 2;

            // 이론상 무한 루프가 되지 않도록 상한을 둔다(같은 분에 999번 캡처할 일은 없다).
            while (File.Exists(candidate) && index < 1000)
            {
                candidate = Path.Combine(folder, $"{baseName}_{index}{extension}");
                index++;
            }

            return candidate;
        }

        /// <summary>
        /// 절대 경로를 "Assets/..." 로 시작하는 프로젝트 상대 경로로 바꾼다.
        /// AssetDatabase 계열 API 와 사용자 안내문은 이 형식을 쓴다.
        /// </summary>
        /// <param name="absolutePath">파일의 절대 경로.</param>
        /// <returns>프로젝트 상대 경로(변환 불가 시 원본 그대로).</returns>
        private static string ToProjectRelativePath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');

            // UnityEngine.Application.dataPath 는 "<프로젝트>/Assets" 이므로, 그 부모가 프로젝트 루트다.
            // (완전 수식이 필요한 이유는 위 CaptureLogcat 의 주석 참조)
            string projectRoot = UnityEngine.Application.dataPath.Replace('\\', '/');
            int assetsIndex = projectRoot.LastIndexOf("/Assets", StringComparison.Ordinal);
            if (assetsIndex < 0) return normalized;

            projectRoot = projectRoot.Substring(0, assetsIndex + 1); // 끝의 '/' 포함.
            return normalized.StartsWith(projectRoot, StringComparison.Ordinal)
                ? normalized.Substring(projectRoot.Length)
                : normalized;
        }

        /// <summary>텍스트의 줄 수를 센다(마지막 줄바꿈 뒤의 빈 줄은 세지 않는다).</summary>
        /// <param name="text">셀 대상 텍스트.</param>
        /// <returns>줄 수.</returns>
        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int count = 0;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') count++;

            // 마지막 줄이 줄바꿈으로 끝나지 않았다면 그 줄도 한 줄로 센다.
            if (text[text.Length - 1] != '\n') count++;

            return count;
        }

        /// <summary>대화상자가 화면을 벗어나지 않도록 긴 문자열을 잘라 낸다.</summary>
        /// <param name="text">원본 문자열.</param>
        /// <param name="maxLength">최대 길이.</param>
        /// <returns>잘린 문자열(잘렸으면 말줄임 표시가 붙는다).</returns>
        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "\n... (이하 생략)";
        }
    }
}
