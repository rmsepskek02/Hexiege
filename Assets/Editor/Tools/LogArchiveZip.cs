// ============================================================================
// LogArchiveZip.cs  (에디터 전용 · 상시 사용 도구)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) Hexiege > Logs > Create Log Archive ZIP 실행 → 작은 창이 뜬다.         │
// │  2) 작업명을 입력한다(영문·숫자·하이픈만. 예: vfx-followups).             │
// │     「시작 날짜」는 보통 기본값(2026-10-20) 그대로 둔다.                   │
// │     보관이 빈 기간을 한 번 묶을 때만 더 이른 날짜(예: 2026-10-13)를 넣는다.│
// │  3) [ZIP 생성] 을 누른다.                                                 │
// │     → Assets/_Project/Docs/_Logs/_zip/{오늘 날짜}/                         │
// │         {날짜}_{HH}_{MM}_{작업명}_{회차}.zip                               │
// │         {날짜}_{HH}_{MM}_{작업명}_{회차}.manifest.json                     │
// │  4) 결과 창을 닫으면 그 폴더와 이번 달 Release 페이지가 열린다.            │
// │     ZIP 과 manifest 업로드는 **사용자가 직접** 한다(이 도구는 업로드 안 함).│
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가(유니티 초급자 기준):
//   테스트 로그 원본(.txt)은 소스 Git 에 올리지 않고(.gitignore),
//   로그 전용 private 저장소 rmsepskek02/Hexiege-Logs 의 "월별 Release" 에 ZIP 으로 보관한다.
//   (규칙 단일 원본: Assets/_Project/Docs/LogRules.md 「원시 테스트 로그 공유·보관」)
//   이 도구는 그 ZIP 과, ZIP 에 무엇이 들었는지 적은 영수증(manifest.json)을
//   규칙대로 만들어 주는 일만 한다.
//
// 어떤 파일을 넣는가(창의 「시작 날짜」 당일부터의 날짜 폴더만. 기본 2026-10-20 —
//                    이유는 상수 DefaultStartDateText 주석):
//   · 실기기 캡처 로그  Assets/_Project/Docs/_Logs/{YYYY-MM-DD}/{폴더}/RuntimeLog_device*.txt
//                       (RuntimeLog_device.txt, RuntimeLog_device_2.txt … 전부)
//   · 진단 세션 로그    Assets/_Project/Docs/_Logs/{YYYY-MM-DD}/{HH_MM_작업명}/RuntimeLog.txt
//   · 에디터 상시 로그  Assets/_Project/Docs/_Logs/_editor/{YYYY-MM-DD}/RuntimeLog.txt
//   _zip 폴더(이 도구의 산출물)와 .meta 파일은 넣지 않는다.
//   그중 **이미 이전 ZIP 에 같은 내용으로 들어간 파일은 뺀다.**
//   판단 기준은 기존 _zip 폴더 안의 모든 *.manifest.json 에 적힌 (경로, SHA256) 쌍이다.
//   → 새로 생긴 파일, 또는 같은 경로라도 내용이 바뀐 파일(예: 오늘 계속 쌓이는 에디터 로그)만 들어간다.
//
// 이 도구가 하지 않는 것:
//   · 원본 로그를 고치거나 지우거나 옮기지 않는다(읽기만 한다).
//   · 업로드·Git 명령·네트워크 전송을 하지 않는다. Release 페이지를 브라우저로 "열기"만 한다.
//   · 인증값(토큰 등)을 다루지 않으며 manifest 에도 적지 않는다.
//   · 빌드가 성공했는지, 테스트가 통과했는지 판단하지 않는다(manifest 에 "추정하지 않음" 으로 적는다).
//   · 실패하거나 취소되면 반쯤 만든 ZIP/manifest 를 남기지 않는다
//     (프로젝트의 Temp 폴더에 먼저 만든 뒤, 검증까지 끝나야 _zip 폴더로 옮긴다).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization; // 날짜 문자열을 문화권(한국어/영어 설정)과 무관하게 같은 형식으로 쓰고 읽기 위해 필요.
using System.IO;
using System.IO.Compression; // ZipArchive. 같은 프로젝트의 Meshy 에디터 플러그인도 이 네임스페이스를 쓰고 있다.
using System.Security.Cryptography; // SHA256.
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 테스트 로그 원본을 ZIP + manifest 로 묶는 에디터 창.
    ///
    /// 클래스 이름을 파일 이름(LogArchiveZip)과 똑같이 둔 이유:
    ///   EditorWindow 도 ScriptableObject 의 일종이라, Unity 가 창 배치(Layout)를 저장했다가
    ///   에디터를 다시 열 때 "파일 이름 = 클래스 이름" 인 스크립트를 찾아 창을 복원한다.
    ///   이름이 다르면 복원 때 스크립트를 찾지 못해 빈 창/경고가 생길 수 있다.
    /// </summary>
    public sealed class LogArchiveZip : EditorWindow
    {
        // ====================================================================
        // 상수 — 경로·이름 규칙 (LogRules.md 「원시 테스트 로그 공유·보관」 과 같아야 한다)
        // ====================================================================

        /// <summary>대화상자 제목(모든 알림창 공통).</summary>
        private const string DialogTitle = "Hexiege Log Archive";

        /// <summary>
        /// 로그 뿌리 폴더(Assets 폴더 기준 상대 경로).
        /// LogcatCapture.cs 의 LogsRootRelativeToAssets 와 같은 값이다.
        /// </summary>
        private const string LogsRootRelativeToAssets = "_Project/Docs/_Logs";

        /// <summary>에디터 상시 로그가 쌓이는 하위 폴더 이름(FileSink 가 쓰는 곳).</summary>
        private const string EditorLogsFolderName = "_editor";

        /// <summary>ZIP 과 manifest 를 내보내는 하위 폴더 이름(.gitignore 로 소스 Git 에서 제외됨).</summary>
        private const string ZipFolderName = "_zip";

        /// <summary>
        /// 에디터 상시 로그(LogRules.md 1.10)와 진단 세션 로그(_Logs/날짜/HH_MM_작업명/)가 같이 쓰는 파일 이름.
        /// </summary>
        private const string EditorLogFileName = "RuntimeLog.txt";

        /// <summary>manifest 파일 확장자. ZIP 과 같은 이름 뒤에 붙는다.</summary>
        private const string ManifestExtension = ".manifest.json";

        /// <summary>ZIP 확장자.</summary>
        private const string ZipExtension = ".zip";

        /// <summary>날짜 폴더 이름 형식. 이 형식으로 읽히지 않는 폴더는 대상에서 뺀다.</summary>
        private const string DateFolderFormat = "yyyy-MM-dd";

        /// <summary>
        /// 창의 「시작 날짜」 기본값. 이 날짜(당일 포함) 이후의 날짜 폴더만 대상으로 삼는다.
        /// 실기기·진단 세션 폴더(_Logs/날짜/)와 에디터 일일 로그(_Logs/_editor/날짜/)에 똑같이 적용한다.
        /// 사용자 결정 2026-10-20.
        ///
        /// 왜 시작 날짜가 필요한가:
        ///   그 이전 로그는 2026-10-12 에 "기존 로그 일회성 보관"(YYYY-MM-DD_기존로그.zip,
        ///   Tools/archive_runtime_logs.py) 등으로 이미 따로 보관했다. 그런데 그 manifest 는 이 도구가 읽는
        ///   _zip 폴더 밖에 있어서, 시작 날짜가 없으면 첫 실행 때 옛 로그 전부를 "아직 보관 안 됨" 으로 보고
        ///   다시 묶어 버린다(중복 보관). 그래서 기본은 이 날짜부터의 로그만 다룬다(첫 실행 포함).
        ///
        /// 창에서 더 이른 날짜(예: 2026-10-13)를 한 번 지정해 보관이 비어 있는 기간을 묶을 수 있다.
        /// 그때 이전 보관분과 겹치는지는 이 도구가 확인하지 않는다(manifest 에 그렇게 적는다 —
        /// 업로드 후 기존 manifest 와 대조하는 것은 사람/메인 세션의 일).
        /// 시작 날짜를 EditorPrefs 에 저장하지 않는 이유: 한 번 쓴 이른 날짜가 다음 실행에 남으면
        /// 의도치 않게 넓은 범위를 다시 훑게 되므로, 창을 열 때마다 이 기본값으로 시작한다.
        /// </summary>
        private const string DefaultStartDateText = "2026-10-20";

        /// <summary>manifest 에 적는 "이전 보관분과의 중복 확인" 범위 안내.</summary>
        private const string PriorArchiveDedupeNote = "prior-archive dedupe: not checked by tool";

        /// <summary>실기기 캡처 파일 이름 접두사. RuntimeLog_device.txt, RuntimeLog_device_2.txt … 를 모두 잡는다.</summary>
        private const string DeviceLogFilePrefix = "RuntimeLog_device";

        /// <summary>로그 원본 확장자.</summary>
        private const string LogFileExtension = ".txt";

        /// <summary>manifest 형식 식별자. 형식을 바꾸면 숫자를 올린다(읽는 쪽이 구분할 수 있게).</summary>
        private const string ManifestSchema = "hexiege-log-archive-v1";

        /// <summary>업로드 대상 저장소 소유자.</summary>
        private const string UploadOwner = "rmsepskek02";

        /// <summary>업로드 대상 저장소 이름(private 로그 전용 저장소).</summary>
        private const string UploadRepo = "Hexiege-Logs";

        /// <summary>월별 Release 제목 접두사. 뒤에 "YYYY-MM" 이 붙는다.</summary>
        private const string ReleaseTitlePrefix = "로그 보관 ";

        /// <summary>월별 Release 태그 접두사. 뒤에 "YYYY-MM" 이 붙는다.</summary>
        private const string ReleaseTagPrefix = "logs-";

        /// <summary>마지막으로 입력한 작업명을 기억하는 EditorPrefs 키(이 PC 에만 저장된다).</summary>
        private const string LastTaskNamePrefsKey = "Hexiege.LogArchiveZip.LastTaskName";

        /// <summary>
        /// 작업명 최대 길이. 파일 이름이 너무 길어지면 Windows 경로 길이 제한(260자)에 걸릴 수 있어 자른다.
        /// </summary>
        private const int MaxTaskNameLength = 60;

        /// <summary>
        /// 원본 1개를 메모리에 통째로 읽을 수 있는 최대 크기(바이트). 256MB.
        /// 로그는 보통 수 MB 이하라 넉넉한 값이며, 비정상적으로 큰 파일로 에디터가 멈추는 것을 막는다.
        /// </summary>
        private const long MaxSnapshotBytes = 256L * 1024 * 1024;

        /// <summary>창의 최소 크기(픽셀).</summary>
        private static readonly Vector2 WindowMinSize = new Vector2(380f, 220f);

        // ====================================================================
        // 창 상태(인스턴스 필드)
        // ====================================================================

        /// <summary>사용자가 입력 중인 작업명(정규화 전 원문).</summary>
        private string _taskNameInput = string.Empty;

        /// <summary>사용자가 입력 중인 시작 날짜(YYYY-MM-DD 문자열). 창을 열 때마다 기본값으로 시작한다.</summary>
        private string _startDateInput = DefaultStartDateText;

        /// <summary>ZIP 생성이 진행 중인지. 버튼 연타로 같은 작업이 두 번 도는 것을 막는다.</summary>
        private bool _isRunning;

        // ====================================================================
        // 메뉴 · 창 그리기
        // ====================================================================

        /// <summary>
        /// 메뉴 Hexiege &gt; Logs &gt; Create Log Archive ZIP — 작업명 입력 창을 연다.
        /// 우선순위 24 는 기존 Logs 메뉴(1, 2, 13)와 10 넘게 떨어뜨려 구분선 아래 별도 묶음으로 보이게 한 값이다.
        /// </summary>
        [MenuItem("Hexiege/Logs/Create Log Archive ZIP", false, 24)]
        public static void OpenWindow()
        {
            // utility=true: 도킹되지 않는 작은 떠 있는 창으로 연다(잠깐 쓰고 닫는 도구라서).
            var window = GetWindow<LogArchiveZip>(true, "Log Archive ZIP", true);
            window.minSize = WindowMinSize;
            window.Show();
        }

        /// <summary>
        /// 창이 열릴 때(또는 스크립트 재컴파일 후) 마지막 작업명을 불러온다.
        /// 시작 날짜는 저장하지 않고 필드 초기값(기본값)을 그대로 쓴다 — 이유는 DefaultStartDateText 주석.
        /// </summary>
        private void OnEnable()
        {
            _taskNameInput = EditorPrefs.GetString(LastTaskNamePrefsKey, string.Empty);
            if (string.IsNullOrEmpty(_startDateInput)) _startDateInput = DefaultStartDateText;
        }

        /// <summary>창 내용을 그린다. Unity 가 화면 갱신 때마다 자동으로 부른다.</summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("테스트 로그 보관 ZIP 만들기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "시작 날짜 당일부터의 날짜 폴더에 있는 원본 로그(실기기 RuntimeLog_device*.txt · " +
                "진단 세션/에디터 RuntimeLog.txt) 중 아직 보관하지 않은 것만 골라 " +
                "ZIP 과 manifest 를 _Logs/_zip/오늘날짜/ 에 만듭니다.\n" +
                "업로드는 하지 않습니다. 완료 후 열리는 Release 페이지에 직접 올려 주세요.",
                MessageType.Info);

            // 입력이 바뀌면 그 즉시 EditorPrefs 에 저장한다(창을 닫았다 열어도 마지막 값이 남도록).
            EditorGUI.BeginChangeCheck();
            _taskNameInput = EditorGUILayout.TextField("작업명", _taskNameInput);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(LastTaskNamePrefsKey, _taskNameInput ?? string.Empty);

            string normalized = NormalizeTaskName(_taskNameInput);
            DrawTaskNamePreview(normalized);

            EditorGUILayout.Space();
            bool startDateValid = DrawStartDateField(out DateTime startDate);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_isRunning || normalized.Length == 0 || !startDateValid))
            {
                if (GUILayout.Button(_isRunning ? "생성 중..." : "ZIP 생성", GUILayout.Height(30f)))
                    ScheduleArchive(normalized, startDate);
            }
        }

        /// <summary>
        /// 「시작 날짜」 입력 칸과 [기본값] 버튼을 그리고, 입력이 올바른 날짜인지 검사한다.
        /// </summary>
        /// <param name="startDate">올바르면 해석한 날짜(시각 부분은 0시).</param>
        /// <returns>YYYY-MM-DD 형식의 실제 날짜이면 true.</returns>
        private bool DrawStartDateField(out DateTime startDate)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _startDateInput = EditorGUILayout.TextField("시작 날짜", _startDateInput);
                if (GUILayout.Button("기본값", GUILayout.Width(60f)))
                {
                    _startDateInput = DefaultStartDateText;
                    // 입력 칸에 커서가 있으면 Unity 가 이전 글자를 계속 보여 주므로 포커스를 풀어 새 값이 보이게 한다.
                    GUI.FocusControl(null);
                }
            }

            if (!TryParseDate(_startDateInput, out startDate))
            {
                EditorGUILayout.HelpBox("시작 날짜를 YYYY-MM-DD 형식의 실제 날짜로 입력하세요(예: 2026-10-20).", MessageType.Warning);
                return false;
            }

            DateTime defaultDate = ParseDateOrThrow(DefaultStartDateText);
            string scope = $"{startDate.ToString(DateFolderFormat, CultureInfo.InvariantCulture)} 당일부터의 날짜 폴더(실기기·진단 세션·에디터 일일 로그 모두)만 찾습니다.";
            if (startDate < defaultDate)
            {
                EditorGUILayout.HelpBox(scope + "\n기본값보다 이른 날짜입니다. 이전 보관분(예: 2026-10-12 기존로그)과 겹치는지는 " +
                                        "이 도구가 확인하지 않습니다 — 업로드 후 기존 manifest 와 대조하세요.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(scope, EditorStyles.miniLabel);
            }
            return true;
        }

        /// <summary>문자열이 정확히 YYYY-MM-DD 형식의 실제 날짜인지(2026-02-30 같은 날짜는 거부).</summary>
        private static bool TryParseDate(string text, out DateTime date)
        {
            return DateTime.TryParseExact((text ?? string.Empty).Trim(), DateFolderFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);
        }

        /// <summary>코드에 적힌 날짜 상수를 해석한다(상수가 잘못되면 개발 중 바로 드러나도록 예외).</summary>
        private static DateTime ParseDateOrThrow(string text)
        {
            return DateTime.ParseExact(text, DateFolderFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>정규화된 작업명과 만들어질 파일 이름 예시를 보여 준다.</summary>
        /// <param name="normalized">정규화된 작업명(비어 있을 수 있음).</param>
        private void DrawTaskNamePreview(string normalized)
        {
            if (normalized.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "작업명을 영문·숫자·하이픈(-)으로 입력하세요. 한글·기호는 파일 이름에 쓰지 않습니다.",
                    MessageType.Warning);
                return;
            }

            // 원문과 다르면(공백→하이픈, 한글·기호 제거 등) 사용자가 알아챌 수 있게 경고를 보인다.
            string trimmed = (_taskNameInput ?? string.Empty).Trim();
            if (!string.Equals(trimmed, normalized, StringComparison.Ordinal))
            {
                EditorGUILayout.HelpBox(
                    $"허용되지 않는 문자를 정리했습니다 → \"{normalized}\"\n" +
                    "(공백·밑줄은 하이픈으로 바뀌고, 그 밖의 문자는 빠집니다)",
                    MessageType.None);
            }

            DateTime now = DateTime.Now;
            string example = $"{now.ToString(DateFolderFormat, CultureInfo.InvariantCulture)}_" +
                             $"{now.ToString("HH_mm", CultureInfo.InvariantCulture)}_{normalized}_NN{ZipExtension}";
            EditorGUILayout.LabelField("파일 이름 예시", example, EditorStyles.miniLabel);
        }

        /// <summary>
        /// 버튼을 누른 그 GUI 그리기 도중이 아니라, 그리기가 끝난 직후에 작업을 실행하도록 예약한다.
        ///
        /// 왜 바로 실행하지 않는가:
        ///   OnGUI 안에서 대화상자(DisplayDialog)나 진행 막대를 띄우면, GUI 레이아웃 그룹이
        ///   열린 채로 다른 창이 끼어들어 "EndLayoutGroup" 류 오류가 콘솔에 찍히곤 한다.
        ///   EditorApplication.delayCall 은 "현재 GUI 처리가 끝난 뒤 한 번" 실행되므로 그 문제를 피한다.
        /// </summary>
        /// <param name="normalizedTaskName">정규화된 작업명(비어 있지 않음).</param>
        /// <param name="startDate">시작 날짜(당일 포함).</param>
        private void ScheduleArchive(string normalizedTaskName, DateTime startDate)
        {
            _isRunning = true;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    CreateArchive(normalizedTaskName, startDate);
                }
                finally
                {
                    // 그사이 창이 닫혔을 수 있다. Unity 객체는 파괴되면 == null 이 true 가 된다.
                    if (this != null)
                    {
                        _isRunning = false;
                        Repaint();
                    }
                }
            };
        }

        // ====================================================================
        // 작업명 정규화
        // ====================================================================

        /// <summary>
        /// 사용자가 입력한 작업명을 파일 이름에 쓸 수 있는 형태로 정리한다.
        /// 규칙: 영문(대소문자 유지)·숫자는 그대로, 공백·밑줄·하이픈은 하이픈 1개로 합치고,
        ///       그 밖의 문자(한글·기호 등)는 뺀다. 앞뒤 하이픈은 제거하고 최대 60자로 자른다.
        ///
        /// 밑줄(_)을 허용하지 않는 이유:
        ///   파일 이름 "YYYY-MM-DD_HH_MM_작업명_회차" 에서 밑줄이 칸 구분자라,
        ///   작업명 안에 밑줄이 있으면 어디까지가 작업명인지 기계적으로 가를 수 없다.
        /// 한글을 허용하지 않는 이유:
        ///   LogRules 「플랫폼 파일명 정규화」 — GitHub 는 업로드 파일 이름의 한글을 지운다.
        ///   처음부터 ASCII 로 만들면 로컬 이름과 원격 이름이 같아 대응표가 필요 없다.
        /// </summary>
        /// <param name="raw">입력 원문(null 가능).</param>
        /// <returns>정규화된 작업명. 쓸 수 있는 글자가 없으면 빈 문자열.</returns>
        public static string NormalizeTaskName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw.Trim())
            {
                if (IsAsciiLetterOrDigit(ch))
                {
                    sb.Append(ch);
                }
                else if (ch == '-' || ch == '_' || char.IsWhiteSpace(ch))
                {
                    // 맨 앞이거나 직전 글자가 이미 하이픈이면 덧붙이지 않는다(하이픈 연속 방지).
                    if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
                }
                // 그 밖의 문자는 버린다.
            }

            if (sb.Length > MaxTaskNameLength) sb.Length = MaxTaskNameLength;
            return sb.ToString().Trim('-');
        }

        /// <summary>ASCII 영문자 또는 숫자인지(char.IsLetterOrDigit 는 한글도 참이라 쓰지 않는다).</summary>
        private static bool IsAsciiLetterOrDigit(char ch)
        {
            return (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
        }

        // ====================================================================
        // 전체 흐름
        // ====================================================================

        /// <summary>
        /// ZIP + manifest 생성의 전체 흐름. 단계마다 실패하면 안내하고 아무 파일도 남기지 않는다.
        ///   1) 이미 보관된 (경로, SHA256) 목록을 기존 manifest 에서 읽는다.
        ///   2) 대상 원본 후보를 찾는다.
        ///   3) 이름(회차)을 정하고 Temp 에 ZIP 을 만든다(이미 보관된 내용은 건너뜀).
        ///   4) 만든 ZIP 을 다시 열어 내용이 기록과 같은지 검증한다.
        ///   5) manifest 를 쓰고, 둘 다 _zip 폴더로 옮긴다.
        ///   6) 결과를 보여 주고 폴더·Release 페이지를 연다.
        /// </summary>
        /// <param name="taskName">정규화된 작업명.</param>
        /// <param name="startDate">이 날짜(당일 포함) 이후의 날짜 폴더만 대상으로 삼는다.</param>
        public static void CreateArchive(string taskName, DateTime startDate)
        {
            var paths = ArchivePaths.Resolve();
            DateTimeOffset now = DateTimeOffset.Now;
            startDate = startDate.Date; // 시각 부분이 섞여 들어와도 "그날 0시" 기준으로 비교한다.
            string startDateText = startDate.ToString(DateFolderFormat, CultureInfo.InvariantCulture);

            // ── 1) 이미 보관된 목록 ────────────────────────────────────────
            ArchivedIndex archived = LoadArchivedIndex(paths);
            if (archived.UnreadableManifests.Count > 0 && !ConfirmUnreadableManifests(archived)) return;

            // ── 2) 후보 찾기 ───────────────────────────────────────────────
            List<CandidateSource> candidates = FindCandidates(paths, startDate, out int skippedLinks);
            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "보관할 원본 로그 파일을 찾지 못했습니다. 아무 파일도 만들지 않았습니다.\n\n" +
                    $"찾는 위치({startDateText} 당일부터의 날짜 폴더만):\n" +
                    $"  {paths.LogsRootRelative}/YYYY-MM-DD/*/{DeviceLogFilePrefix}*{LogFileExtension}\n" +
                    $"  {paths.LogsRootRelative}/YYYY-MM-DD/*/{EditorLogFileName}\n" +
                    $"  {paths.LogsRootRelative}/{EditorLogsFolderName}/YYYY-MM-DD/{EditorLogFileName}",
                    "확인");
                return;
            }

            // ── 3)~5) 생성 · 검증 · 이동 ───────────────────────────────────
            ArchiveNames names = ArchiveNames.Create(paths, now, taskName);
            ArchiveResult result = BuildAndPublishLocally(paths, names, now, taskName, startDateText, candidates, archived);
            if (result == null) return; // 안내는 BuildAndPublishLocally 안에서 이미 했다.

            result.SkippedLinks = skippedLinks;
            result.StartDateText = startDateText;
            ShowResultAndOpen(paths, names, result);
        }

        /// <summary>
        /// 3)~5) 단계. Temp 에 ZIP·manifest 를 만들고 검증한 뒤 최종 위치로 옮긴다.
        /// 성공하면 결과를, 실패·취소·대상 없음이면 안내 후 null 을 돌려준다.
        /// 어느 경우든 Temp 의 중간 파일은 지운다(원본 로그는 절대 건드리지 않는다).
        /// </summary>
        private static ArchiveResult BuildAndPublishLocally(
            ArchivePaths paths, ArchiveNames names, DateTimeOffset now, string taskName, string startDateText,
            List<CandidateSource> candidates, ArchivedIndex archived)
        {
            string tempZip = null;
            string tempManifest = null;
            bool createdOutputFolder = false;
            try
            {
                if (File.Exists(names.ZipAbsolute) || File.Exists(names.ManifestAbsolute))
                    throw new IOException("같은 이름의 ZIP/manifest 가 이미 있습니다(덮어쓰지 않음): " + names.ZipFileName);

                Directory.CreateDirectory(paths.TempFolderAbsolute);
                string tempStem = Path.Combine(paths.TempFolderAbsolute, names.BaseName + "." + Guid.NewGuid().ToString("N"));
                tempZip = tempStem + ".zip.tmp";
                tempManifest = tempStem + ".manifest.tmp";

                List<SourceRecord> records = WriteZip(tempZip, candidates, archived, out int skippedArchived);
                if (records.Count == 0)
                {
                    ShowNothingNew(candidates.Count, skippedArchived);
                    return null;
                }

                VerifyZip(tempZip, records);
                var result = new ArchiveResult
                {
                    Records = records,
                    SkippedArchived = skippedArchived,
                    ZipBytes = new FileInfo(tempZip).Length,
                    ZipSha256 = ComputeFileSha256(tempZip),
                    ManifestsScanned = archived.ManifestsScanned,
                    UnreadableManifests = archived.UnreadableManifests,
                    StartDateText = startDateText,
                };

                string json = BuildManifestJson(names, now, taskName, result);
                WriteNewTextFile(tempManifest, json);

                createdOutputFolder = !Directory.Exists(names.OutputFolderAbsolute);
                Directory.CreateDirectory(names.OutputFolderAbsolute);
                MoveBothOrNothing(tempZip, names.ZipAbsolute, tempManifest, names.ManifestAbsolute);
                createdOutputFolder = false; // 성공했으니 아래 정리에서 폴더를 지우지 않는다.
                return result;
            }
            catch (OperationCanceledException)
            {
                EditorUtility.DisplayDialog(DialogTitle, "취소했습니다. 아무 파일도 만들지 않았습니다.", "확인");
                return null;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "ZIP 생성에 실패했습니다. 만들던 파일은 남기지 않았습니다(원본 로그는 그대로입니다).\n\n" +
                    $"원인: {e.GetType().Name}: {e.Message}",
                    "확인");
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                TryDeleteFile(tempZip);
                TryDeleteFile(tempManifest);
                if (createdOutputFolder) TryDeleteEmptyFolder(names.OutputFolderAbsolute);
            }
        }

        /// <summary>
        /// 검증을 마친 Temp 의 ZIP·manifest 를 최종 위치로 옮긴다. 둘 중 하나만 남는 일이 없게 한다.
        /// File.Move 는 대상 파일이 이미 있으면 예외를 던지므로 기존 파일을 덮어쓰는 일도 없다.
        /// </summary>
        private static void MoveBothOrNothing(string tempZip, string finalZip, string tempManifest, string finalManifest)
        {
            File.Move(tempZip, finalZip);
            try
            {
                File.Move(tempManifest, finalManifest);
            }
            catch
            {
                // manifest 없이 ZIP 만 남으면 다음 회차의 "이미 보관됨" 판정이 그 ZIP 을 모르게 된다.
                // 방금 이 도구가 옮긴 파일이므로 지워도 사용자의 다른 자료를 잃지 않는다.
                TryDeleteFile(finalZip);
                throw;
            }
        }

        // ====================================================================
        // 1) 이미 보관된 목록 읽기
        // ====================================================================

        /// <summary>
        /// _zip 아래 모든 *.manifest.json 을 읽어 (저장소 상대 경로, SHA256) 쌍을 모은다.
        /// 이 도구의 manifest("sources") 와 Tools/archive_runtime_logs.py 의 manifest("entries")를 모두 읽는다.
        /// 읽지 못한 manifest 는 목록으로 돌려줘 사용자가 계속할지 정하게 한다.
        /// </summary>
        private static ArchivedIndex LoadArchivedIndex(ArchivePaths paths)
        {
            var index = new ArchivedIndex();
            if (!Directory.Exists(paths.ZipRootAbsolute)) return index;

            string[] files = Directory.GetFiles(paths.ZipRootAbsolute, "*" + ManifestExtension, SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                // 검색 패턴만 믿지 않고 이름 끝을 한 번 더 확인한다(.meta 등 다른 파일 배제).
                if (!file.EndsWith(ManifestExtension, StringComparison.OrdinalIgnoreCase)) continue;
                index.ManifestsScanned++;
                if (!TryReadManifestKeys(file, index.Keys))
                    index.UnreadableManifests.Add(paths.ToRepoRelative(file));
            }
            return index;
        }

        /// <summary>manifest 1개에서 키를 읽어 keys 에 더한다. 읽기 실패·원본 목록 없음이면 false.</summary>
        private static bool TryReadManifestKeys(string manifestAbsolute, HashSet<string> keys)
        {
            try
            {
                string text = File.ReadAllText(manifestAbsolute, Encoding.UTF8);
                var model = JsonUtility.FromJson<ManifestReadModel>(text);
                if (model == null) return false;

                int added = AddKeys(model.sources, keys) + AddKeys(model.entries, keys);
                return added > 0; // 원본이 0개인 manifest 는 정상 산출물이 아니므로 "읽지 못함" 으로 취급.
            }
            catch (Exception)
            {
                // JSON 형식 오류(ArgumentException), 파일 잠김(IOException) 등.
                return false;
            }
        }

        /// <summary>읽어 온 원본 항목들을 키로 바꿔 더하고, 유효한 항목 수를 돌려준다.</summary>
        private static int AddKeys(ManifestReadSource[] items, HashSet<string> keys)
        {
            if (items == null) return 0;
            int count = 0;
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.path) || string.IsNullOrEmpty(item.sha256)) continue;
                keys.Add(MakeKey(item.path, item.sha256));
                count++;
            }
            return count;
        }

        /// <summary>
        /// "이미 보관됨" 판정 키. 경로 구분자는 '/' 로, SHA256 은 소문자로 맞춰
        /// Windows/macOS 어디서 만든 manifest 든 같은 파일이면 같은 키가 되게 한다.
        /// </summary>
        private static string MakeKey(string repoRelativePath, string sha256)
        {
            return repoRelativePath.Replace('\\', '/') + "\n" + sha256.Trim().ToLowerInvariant();
        }

        /// <summary>읽지 못한 manifest 가 있을 때 계속할지 묻는다. 계속하면 그 manifest 의 파일이 다시 들어갈 수 있다.</summary>
        private static bool ConfirmUnreadableManifests(ArchivedIndex archived)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"읽지 못한 manifest 가 {archived.UnreadableManifests.Count}개 있습니다.");
            sb.AppendLine("계속하면 그 manifest 에 적힌 로그가 이번 ZIP 에 다시 들어갈 수 있습니다(중복 보관).");
            sb.AppendLine();
            for (int i = 0; i < archived.UnreadableManifests.Count && i < 10; i++)
                sb.AppendLine("  " + archived.UnreadableManifests[i]);
            if (archived.UnreadableManifests.Count > 10)
                sb.AppendLine($"  ... 외 {archived.UnreadableManifests.Count - 10}개");
            return EditorUtility.DisplayDialog(DialogTitle, sb.ToString(), "계속", "취소");
        }

        // ====================================================================
        // 2) 후보 찾기
        // ====================================================================

        /// <summary>
        /// 대상 원본 후보를 찾는다(내용은 아직 읽지 않는다). 결과는 경로 순으로 정렬해
        /// 같은 상태에서 몇 번을 돌려도 ZIP 안의 순서가 같게 한다.
        /// </summary>
        /// <param name="paths">경로 모음.</param>
        /// <param name="startDate">이 날짜(당일 포함) 이후의 날짜 폴더만 본다.</param>
        /// <param name="skippedLinks">심볼릭 링크/정션이라 건너뛴 폴더·파일 수.</param>
        private static List<CandidateSource> FindCandidates(ArchivePaths paths, DateTime startDate, out int skippedLinks)
        {
            var list = new List<CandidateSource>();
            int links = 0;

            // _Logs/{날짜}/{폴더}/ (딱 한 단계 아래만):
            //   · RuntimeLog_device*.txt — 실기기 캡처(같은 분에 두 번 캡처하면 _2, _3 … 이 붙는다)
            //   · RuntimeLog.txt          — 진단 세션 로그(_Logs/날짜/HH_MM_작업명/)
            foreach (string dateDir in EnumerateDateFolders(paths.LogsRootAbsolute, startDate, ref links))
            {
                foreach (string sessionDir in EnumerateRealSubfolders(dateDir, ref links))
                {
                    foreach (string file in Directory.GetFiles(sessionDir, DeviceLogFilePrefix + "*" + LogFileExtension, SearchOption.TopDirectoryOnly))
                    {
                        // 검색 패턴만 믿지 않고 이름을 다시 확인한다(.txt.meta 등 배제).
                        string name = Path.GetFileName(file);
                        if (name.StartsWith(DeviceLogFilePrefix, StringComparison.Ordinal) &&
                            name.EndsWith(LogFileExtension, StringComparison.Ordinal))
                            TryAddCandidate(paths, file, SourceKind.DeviceCapture, list, ref links);
                    }
                    TryAddCandidate(paths, Path.Combine(sessionDir, EditorLogFileName), SourceKind.DiagnosticSession, list, ref links);
                }
            }

            // 에디터: _Logs/_editor/{날짜}/RuntimeLog.txt
            string editorRoot = Path.Combine(paths.LogsRootAbsolute, EditorLogsFolderName);
            foreach (string dateDir in EnumerateDateFolders(editorRoot, startDate, ref links))
                TryAddCandidate(paths, Path.Combine(dateDir, EditorLogFileName), SourceKind.EditorDaily, list, ref links);

            list.Sort((a, b) => string.CompareOrdinal(a.RepoRelativePath, b.RepoRelativePath));
            skippedLinks = links;
            return list;
        }

        /// <summary>
        /// 폴더 바로 아래에서 이름이 YYYY-MM-DD 이고 시작 날짜(당일 포함) 이후인 실제 폴더만 돌려준다.
        /// 이름이 날짜가 아닌 _editor, _zip 등은 여기서 자연히 빠진다(그래서 _zip 안의 ZIP·manifest·.meta 는 대상이 아니다).
        /// </summary>
        private static List<string> EnumerateDateFolders(string parent, DateTime startDate, ref int links)
        {
            var result = new List<string>();
            foreach (string dir in EnumerateRealSubfolders(parent, ref links))
            {
                if (TryParseDate(Path.GetFileName(dir), out DateTime date) && date >= startDate)
                    result.Add(dir);
            }
            return result;
        }

        /// <summary>
        /// 폴더 바로 아래의 하위 폴더 중 심볼릭 링크/정션이 아닌 것만 돌려준다.
        /// 링크를 따라가면 로그 폴더 밖의 파일이 ZIP 에 섞일 수 있어 따라가지 않는다.
        /// </summary>
        private static List<string> EnumerateRealSubfolders(string parent, ref int links)
        {
            var result = new List<string>();
            if (!Directory.Exists(parent)) return result;
            foreach (string dir in Directory.GetDirectories(parent, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsLink(dir)) { links++; continue; }
                result.Add(dir);
            }
            return result;
        }

        /// <summary>파일이 실제로 있고 링크가 아니면 후보에 추가한다.</summary>
        private static void TryAddCandidate(ArchivePaths paths, string fileAbsolute, SourceKind kind,
            List<CandidateSource> list, ref int links)
        {
            if (!File.Exists(fileAbsolute)) return;
            if (IsLink(fileAbsolute)) { links++; return; }
            list.Add(new CandidateSource(fileAbsolute, paths.ToRepoRelative(fileAbsolute), kind));
        }

        /// <summary>심볼릭 링크·정션(ReparsePoint 속성)인지.</summary>
        private static bool IsLink(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        // ====================================================================
        // 3) ZIP 쓰기
        // ====================================================================

        /// <summary>
        /// 후보를 하나씩 "스냅샷"으로 읽어 SHA256 을 구하고, 아직 보관되지 않은 것만 ZIP 에 쓴다.
        ///
        /// 핵심: 해시를 계산한 바로 그 바이트 배열을 그대로 ZIP 에 쓴다.
        ///   에디터 로그는 Unity 가 지금도 뒤에 덧붙이는(append) 중일 수 있다.
        ///   해시용으로 한 번, 압축용으로 또 한 번 파일을 읽으면 그 사이에 내용이 늘어나
        ///   "manifest 의 해시 ≠ ZIP 안의 내용" 이 될 수 있다. 한 번만 읽으면 그런 일이 없다.
        ///
        /// ZIP 파일은 넣을 파일이 처음 나왔을 때 만든다 → 새 파일이 0개면 Temp 에도 아무것도 생기지 않는다.
        /// 메모리에는 한 번에 원본 1개만 올라간다(다 넣고 나면 버린다).
        /// </summary>
        private static List<SourceRecord> WriteZip(string tempZip, List<CandidateSource> candidates,
            ArchivedIndex archived, out int skippedArchived)
        {
            var records = new List<SourceRecord>();
            skippedArchived = 0;
            FileStream zipStream = null;
            ZipArchive zip = null;
            try
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    CandidateSource candidate = candidates[i];
                    ReportProgress($"읽는 중 ({i + 1}/{candidates.Count}): {candidate.RepoRelativePath}", i, candidates.Count);

                    SourceRecord record = TakeSnapshot(candidate, out byte[] bytes);
                    if (archived.Keys.Contains(MakeKey(record.RepoRelativePath, record.Sha256)))
                    {
                        skippedArchived++;
                        continue;
                    }

                    if (zip == null)
                    {
                        // CreateNew: 혹시 같은 이름이 있으면 덮어쓰지 않고 예외(이름에 GUID 가 있어 사실상 없음).
                        zipStream = new FileStream(tempZip, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                        zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true, Encoding.UTF8);
                    }
                    AddEntry(zip, record, bytes);
                    records.Add(record);
                }
            }
            finally
            {
                // ZipArchive 를 Dispose 해야 ZIP 끝의 "목차(central directory)" 가 기록된다.
                // 예외로 빠져나가는 경우에도 파일 핸들을 닫아야 Temp 정리(삭제)가 가능하다.
                zip?.Dispose();
                zipStream?.Dispose();
            }
            return records;
        }

        /// <summary>
        /// 원본 1개를 지금 이 순간의 내용 그대로 메모리에 읽는다(스냅샷).
        ///
        /// FileShare.ReadWrite 가 필요한 이유:
        ///   RuntimeLogger 는 에디터 로그를 StreamWriter(append) 로 열어 둔 채 계속 쓴다.
        ///   우리가 "다른 프로그램은 쓰지 마라"(FileShare.Read) 로 열려고 하면,
        ///   이미 쓰기용으로 열려 있는 파일이라 열기 자체가 실패한다.
        ///   ReadWrite 는 "남이 쓰고 있어도 괜찮다" 는 뜻이라 열 수 있다. 우리는 읽기만 한다.
        /// FileShare.Delete 는 사용자가 그 사이 파일을 지우거나 이름을 바꾸는 것을 막지 않기 위해 넣었다.
        /// </summary>
        private static SourceRecord TakeSnapshot(CandidateSource candidate, out byte[] bytes)
        {
            using (var stream = new FileStream(candidate.AbsolutePath, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length > MaxSnapshotBytes)
                    throw new IOException($"파일이 너무 큽니다({stream.Length:N0} bytes > {MaxSnapshotBytes:N0}): {candidate.RepoRelativePath}");

                // 길이를 잰 뒤에도 Unity 가 덧붙일 수 있으므로, 고정 길이가 아니라 "읽히는 끝까지" 읽는다.
                var buffer = new MemoryStream((int)stream.Length);
                stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            }

            DateTimeOffset snapshotAt = DateTimeOffset.Now;
            return new SourceRecord
            {
                RepoRelativePath = candidate.RepoRelativePath,
                Kind = candidate.Kind,
                Bytes = bytes.LongLength,
                Sha256 = ComputeSha256(bytes),
                SnapshotAt = snapshotAt,
                SourceLastWriteUtc = File.GetLastWriteTimeUtc(candidate.AbsolutePath),
            };
        }

        /// <summary>스냅샷 바이트를 ZIP 항목 하나로 쓴다. 항목 경로 = 저장소 상대 경로 그대로.</summary>
        private static void AddEntry(ZipArchive zip, SourceRecord record, byte[] bytes)
        {
            // UnityEngine 에도 CompressionLevel 이라는 이름의 형식이 있을 수 있어 완전 수식으로 쓴다.
            ZipArchiveEntry entry = zip.CreateEntry(record.RepoRelativePath, System.IO.Compression.CompressionLevel.Optimal);

            // ZIP 형식은 1980년 이전 시각을 담지 못한다. 범위 밖이면 기본값(현재 시각)을 그대로 둔다.
            DateTime localWrite = record.SourceLastWriteUtc.ToLocalTime();
            if (localWrite.Year >= 1980 && localWrite.Year <= 2107) entry.LastWriteTime = new DateTimeOffset(localWrite);

            using (Stream entryStream = entry.Open())
                entryStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>취소 가능한 진행 막대를 갱신한다. 사용자가 취소를 누르면 OperationCanceledException.</summary>
        private static void ReportProgress(string message, int done, int total)
        {
            float progress = total <= 0 ? 0f : (float)done / total;
            if (EditorUtility.DisplayCancelableProgressBar(DialogTitle, message, progress))
                throw new OperationCanceledException();
        }

        // ====================================================================
        // 4) ZIP 검증
        // ====================================================================

        /// <summary>
        /// 방금 만든 ZIP 을 다시 열어, 항목 개수·경로·크기·SHA256 이 기록과 정확히 같은지 확인한다.
        /// 하나라도 다르면 예외 → 상위에서 Temp 파일을 지우고 실패를 안내한다.
        /// </summary>
        private static void VerifyZip(string tempZip, List<SourceRecord> records)
        {
            ReportProgress("ZIP 검증 중...", records.Count, records.Count);
            var expected = new Dictionary<string, SourceRecord>(StringComparer.Ordinal);
            foreach (SourceRecord r in records) expected.Add(r.RepoRelativePath, r);

            using (var stream = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8))
            {
                if (zip.Entries.Count != records.Count)
                    throw new InvalidDataException($"ZIP 항목 수 불일치: {zip.Entries.Count} != {records.Count}");

                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    // Remove 로 꺼내므로 같은 경로가 두 번 들어 있으면 두 번째에서 실패한다(중복 검출).
                    if (!expected.TryGetValue(entry.FullName, out SourceRecord record) || !expected.Remove(entry.FullName))
                        throw new InvalidDataException("ZIP 에 예상하지 않은(또는 중복된) 항목: " + entry.FullName);
                    if (entry.Length != record.Bytes)
                        throw new InvalidDataException("ZIP 항목 크기 불일치: " + entry.FullName);

                    string sha;
                    using (Stream entryStream = entry.Open()) sha = ComputeSha256(entryStream);
                    if (!string.Equals(sha, record.Sha256, StringComparison.Ordinal))
                        throw new InvalidDataException("ZIP 항목 SHA256 불일치: " + entry.FullName);
                }
            }
        }

        // ====================================================================
        // 5) manifest
        // ====================================================================

        /// <summary>manifest JSON 문자열을 만든다(사람이 읽기 쉽게 들여쓰기 포함).</summary>
        private static string BuildManifestJson(ArchiveNames names, DateTimeOffset now, string taskName, ArchiveResult result)
        {
            long totalBytes = 0;
            var sources = new ManifestSource[result.Records.Count];
            for (int i = 0; i < result.Records.Count; i++)
            {
                sources[i] = ToManifestSource(result.Records[i]);
                totalBytes += result.Records[i].Bytes;
            }

            var manifest = new LogArchiveManifest
            {
                schema = ManifestSchema,
                tool = "Assets/Editor/Tools/LogArchiveZip.cs",
                created_at_local = FormatLocal(now),
                created_at_utc = FormatUtc(now),
                task_name = taskName,
                sequence = names.Sequence,
                file_count = sources.Length,
                total_source_bytes = totalBytes,
                sources = sources,
                start_date = result.StartDateText,
                start_date_rule = "date folders on or after start_date (inclusive); applied to _Logs/YYYY-MM-DD/ and _Logs/_editor/YYYY-MM-DD/",
                dedupe_scope = "path+sha256 already listed in Assets/_Project/Docs/_Logs/_zip/**/*.manifest.json",
                prior_archive_dedupe = PriorArchiveDedupeNote,
                skipped_already_archived = result.SkippedArchived,
                manifests_scanned = result.ManifestsScanned,
                unreadable_manifests = result.UnreadableManifests.ToArray(),
                archive = new ManifestArchive
                {
                    name = names.ZipFileName,
                    manifest_name = names.ManifestFileName,
                    local_path = names.ZipRepoRelative,
                    bytes = result.ZipBytes,
                    sha256 = result.ZipSha256,
                    entry_path_rule = "repository-relative path, unchanged",
                    local_verification = "entry count/path/bytes/SHA256 re-read from the ZIP before move",
                },
                upload_target = ManifestUploadTarget.For(names.MonthText),
                build_status = "not inferred by archive",
                session_role = "not inspected by archive",
                limits = new[]
                {
                    "upload performed by user; this tool does not upload, run Git, or verify the remote repository",
                    "before upload, confirm the target repository is private (LogRules)",
                    "after upload, download ZIP and manifest and compare sizes/SHA256 before recording archive success",
                    "editor daily logs may still be appended by Unity; each entry is a snapshot (bytes/sha256 at snapshot time)",
                },
            };
            return JsonUtility.ToJson(manifest, true);
        }

        /// <summary>
        /// 내부 기록을 manifest 항목으로 옮긴다.
        /// (SourceRecord 가 private 형식이라 public 인 ManifestSource 안에 두면 접근 수준 오류 CS0051 이 나서 바깥에 둔다)
        /// </summary>
        private static ManifestSource ToManifestSource(SourceRecord r)
        {
            // 에디터가 쓰는 두 종류(상시·진단 세션)는 Unity 가 아직 덧붙이는 중일 수 있다.
            bool editor = r.Kind != SourceKind.DeviceCapture;
            return new ManifestSource
            {
                path = r.RepoRelativePath,
                kind = KindText(r.Kind),
                bytes = r.Bytes,
                sha256 = r.Sha256,
                snapshot_at_local = FormatLocal(r.SnapshotAt),
                snapshot_at_utc = FormatUtc(r.SnapshotAt),
                source_last_write_utc = FormatUtc(r.SourceLastWriteUtc),
                snapshot_note = editor
                    ? "snapshot of a file Unity may still append to; later bytes are not included and the last line may be partial"
                    : "device capture file read as-is",
            };
        }

        /// <summary>원본 종류를 manifest 의 kind 문자열로.</summary>
        private static string KindText(SourceKind kind)
        {
            switch (kind)
            {
                case SourceKind.EditorDaily: return "editor_daily";
                case SourceKind.DiagnosticSession: return "diagnostic_session";
                default: return "device_capture";
            }
        }

        /// <summary>새 텍스트 파일을 UTF-8(BOM 없음)로 쓴다. 이미 있으면 덮어쓰지 않고 예외.</summary>
        private static void WriteNewTextFile(string path, string text)
        {
            byte[] data = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(data, 0, data.Length);
        }

        /// <summary>로컬 시각을 시간대 포함 ISO 8601 로(예: 2026-10-20T14:05:33.120+09:00).</summary>
        private static string FormatLocal(DateTimeOffset value)
        {
            return value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
        }

        /// <summary>UTC 시각을 ISO 8601 로(예: 2026-10-20T05:05:33.120Z).</summary>
        private static string FormatUtc(DateTimeOffset value)
        {
            return value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>UTC DateTime 을 ISO 8601 로.</summary>
        private static string FormatUtc(DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        // ====================================================================
        // 6) 결과 안내
        // ====================================================================

        /// <summary>새로 넣을 파일이 없을 때의 안내. 이때는 어떤 파일도 만들지 않는다.</summary>
        private static void ShowNothingNew(int candidateCount, int skippedArchived)
        {
            EditorUtility.DisplayDialog(DialogTitle,
                "새로 보관할 로그가 없습니다. 아무 파일도 만들지 않았습니다.\n\n" +
                $"찾은 원본 {candidateCount}개 중 {skippedArchived}개는 기존 manifest 에 같은 내용(경로·SHA256)으로 이미 기록돼 있습니다.",
                "확인");
        }

        /// <summary>결과 요약을 보여 주고, 확인 후 출력 폴더와 이번 달 Release 페이지를 연다.</summary>
        private static void ShowResultAndOpen(ArchivePaths paths, ArchiveNames names, ArchiveResult result)
        {
            long totalBytes = 0;
            foreach (SourceRecord r in result.Records) totalBytes += r.Bytes;
            string releaseUrl = ManifestUploadTarget.ReleaseUrlFor(names.MonthText);

            var sb = new StringBuilder();
            sb.AppendLine("로그 보관 ZIP 을 만들었습니다.");
            sb.AppendLine();
            sb.AppendLine("ZIP: " + names.ZipRepoRelative);
            sb.AppendLine("manifest: " + names.ManifestFileName);
            sb.AppendLine($"포함 파일: {result.Records.Count}개 (원본 합계 {totalBytes:N0} bytes)");
            sb.AppendLine($"시작 날짜: {result.StartDateText} (당일 포함)");
            sb.AppendLine($"이미 보관돼 제외: {result.SkippedArchived}개 (_zip 안의 manifest 기준 — 그 밖의 이전 보관분과의 중복은 확인하지 않음)");
            if (result.SkippedLinks > 0) sb.AppendLine($"링크라서 건너뜀: {result.SkippedLinks}개");
            if (result.UnreadableManifests.Count > 0) sb.AppendLine($"읽지 못한 manifest: {result.UnreadableManifests.Count}개");
            sb.AppendLine($"ZIP 크기: {result.ZipBytes:N0} bytes");
            sb.AppendLine("ZIP SHA256: " + result.ZipSha256);
            sb.AppendLine();
            sb.AppendLine("업로드는 이 도구가 하지 않습니다. [확인] 후 폴더와 Release 페이지가 열립니다.");
            sb.AppendLine($"· 업로드 전 {UploadOwner}/{UploadRepo} 가 private 인지 확인하세요.");
            sb.AppendLine("· ZIP 과 manifest 를 함께 올리고, 다시 내려받아 크기·SHA256 을 대조하세요.");
            sb.AppendLine($"· 이번 달 Release 가 아직 없으면 페이지가 404 입니다 → 제목 「{ReleaseTitlePrefix}{names.MonthText}」, " +
                          $"태그 {ReleaseTagPrefix}{names.MonthText} 로 새로 만드세요.");
            EditorUtility.DisplayDialog(DialogTitle, sb.ToString(), "확인");

            Debug.Log($"[LogArchiveZip] 생성 완료: {names.ZipRepoRelative} (파일 {result.Records.Count}개, SHA256={result.ZipSha256})");

            // AssetDatabase.Refresh() 를 일부러 부르지 않는다:
            //   이 도구는 만든 파일을 AssetDatabase API 로 다루지 않고(File API 만 사용), 다음 실행의
            //   "이미 보관됨" 판정도 파일을 직접 읽는다. 전체 Refresh 는 프로젝트 전체를 다시 훑는 비싼 작업이라
            //   생략하고, Unity 의 평소 자동 갱신(에디터 창에 다시 포커스가 올 때)이 .meta 를 만들게 둔다.
            //   _zip 폴더는 .gitignore 대상이라 .meta 가 언제 생기든 소스 Git 에는 영향이 없다.
            EditorUtility.RevealInFinder(names.ZipAbsolute);
            UnityEngine.Application.OpenURL(releaseUrl);
        }

        // ====================================================================
        // 공용 유틸
        // ====================================================================

        /// <summary>바이트 배열의 SHA256 을 소문자 16진수로.</summary>
        private static string ComputeSha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create()) return ToHex(sha.ComputeHash(data));
        }

        /// <summary>스트림 전체의 SHA256 을 소문자 16진수로.</summary>
        private static string ComputeSha256(Stream stream)
        {
            using (SHA256 sha = SHA256.Create()) return ToHex(sha.ComputeHash(stream));
        }

        /// <summary>파일 전체의 SHA256 을 소문자 16진수로.</summary>
        private static string ComputeFileSha256(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return ComputeSha256(stream);
        }

        /// <summary>바이트 배열을 소문자 16진수 문자열로.</summary>
        private static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>파일이 있으면 지운다. 정리 단계라 실패해도 예외를 밖으로 던지지 않는다.</summary>
        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { /* Temp 폴더는 Unity 종료 시 비워지므로 남아도 무해하다. */ }
        }

        /// <summary>이번 실행이 만든 폴더가 비어 있으면 지운다(실패 시 빈 날짜 폴더가 남지 않게).</summary>
        private static void TryDeleteEmptyFolder(string path)
        {
            try
            {
                if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0) Directory.Delete(path);
            }
            catch (Exception) { /* 비어 있는 폴더 하나라 남아도 무해하다. */ }
        }

        // ====================================================================
        // 내부 자료형 — 경로·이름·기록
        // ====================================================================

        /// <summary>원본 종류. manifest 의 kind 값으로도 쓴다.</summary>
        private enum SourceKind
        {
            /// <summary>실기기 Logcat 캡처(LogcatCapture 가 만든 RuntimeLog_device.txt).</summary>
            DeviceCapture,

            /// <summary>에디터 일 단위 상시 로그(_editor/날짜/RuntimeLog.txt). Unity 가 append 중일 수 있다.</summary>
            EditorDaily,

            /// <summary>진단 세션 로그(날짜/HH_MM_작업명/RuntimeLog.txt). 세션이 열려 있으면 append 중일 수 있다.</summary>
            DiagnosticSession,
        }

        /// <summary>이 도구가 쓰는 절대/상대 경로 모음. 한 번 계산해 모든 단계가 같은 값을 쓴다.</summary>
        private sealed class ArchivePaths
        {
            public string ProjectRootAbsolute;
            public string LogsRootAbsolute;
            public string LogsRootRelative;
            public string ZipRootAbsolute;
            public string TempFolderAbsolute;

            /// <summary>현재 프로젝트 기준으로 경로를 계산한다.</summary>
            public static ArchivePaths Resolve()
            {
                // ⚠️ Application 은 반드시 UnityEngine 으로 완전 수식한다.
                //    이 파일은 namespace Hexiege.EditorTools 안에 있어서, 그냥 Application 이라고 쓰면
                //    C# 이 먼저 바깥 네임스페이스의 Hexiege.Application(게임 코드 네임스페이스)으로 해석해
                //    컴파일 오류(CS0234)가 난다. (LogcatCapture.cs 와 같은 이유)
                string dataPath = Path.GetFullPath(UnityEngine.Application.dataPath); // "<프로젝트>/Assets"
                string projectRoot = Path.GetDirectoryName(dataPath);
                string logsRoot = Path.Combine(dataPath, LogsRootRelativeToAssets.Replace('/', Path.DirectorySeparatorChar));
                return new ArchivePaths
                {
                    ProjectRootAbsolute = projectRoot,
                    LogsRootAbsolute = logsRoot,
                    LogsRootRelative = "Assets/" + LogsRootRelativeToAssets,
                    ZipRootAbsolute = Path.Combine(logsRoot, ZipFolderName),
                    // 프로젝트의 Temp 폴더: Unity 가 쓰는 임시 폴더라 Assets 밖이어서 import 대상이 아니고 Git 에도 안 들어간다.
                    TempFolderAbsolute = Path.Combine(projectRoot, "Temp", "LogArchiveZip"),
                };
            }

            /// <summary>
            /// 절대 경로를 저장소(=Unity 프로젝트 루트) 상대 경로 "Assets/..." 로 바꾼다. 구분자는 항상 '/'.
            /// 프로젝트 밖 경로면 예외(링크 등으로 밖의 파일이 섞이는 것을 막는 마지막 방어선).
            /// </summary>
            public string ToRepoRelative(string absolutePath)
            {
                string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
                string root = ProjectRootAbsolute.Replace('\\', '/').TrimEnd('/') + "/";
                // Windows 는 대소문자를 구분하지 않으므로 비교만 무시하고, 잘라 낸 결과는 실제 대소문자를 유지한다.
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("프로젝트 밖 경로입니다: " + absolutePath);
                return full.Substring(root.Length);
            }
        }

        /// <summary>이번 회차의 날짜·회차·파일 이름·최종 경로.</summary>
        private sealed class ArchiveNames
        {
            public string DateText;
            public string MonthText;
            public int Sequence;
            public string BaseName;
            public string ZipFileName;
            public string ManifestFileName;
            public string OutputFolderAbsolute;
            public string ZipAbsolute;
            public string ManifestAbsolute;
            public string ZipRepoRelative;

            /// <summary>
            /// 이름을 정한다: YYYY-MM-DD_HH_MM_작업명_NN.
            /// NN 은 오늘 폴더에 이미 있는 "같은 날짜 + 같은 작업명" 파일의 가장 큰 번호 + 1 (없으면 01).
            /// 시각(HH_MM)은 회차 계산에 쓰지 않는다 — 같은 작업을 하루에 여러 번 묶으면 01, 02, 03 으로 이어진다.
            /// </summary>
            public static ArchiveNames Create(ArchivePaths paths, DateTimeOffset now, string taskName)
            {
                string date = now.ToString(DateFolderFormat, CultureInfo.InvariantCulture);
                string folder = Path.Combine(paths.ZipRootAbsolute, date);
                int sequence = NextSequence(folder, date, taskName);
                string baseName = $"{date}_{now.ToString("HH_mm", CultureInfo.InvariantCulture)}_{taskName}_" +
                                  sequence.ToString("D2", CultureInfo.InvariantCulture);
                var names = new ArchiveNames
                {
                    DateText = date,
                    MonthText = now.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    Sequence = sequence,
                    BaseName = baseName,
                    ZipFileName = baseName + ZipExtension,
                    ManifestFileName = baseName + ManifestExtension,
                    OutputFolderAbsolute = folder,
                };
                names.ZipAbsolute = Path.Combine(folder, names.ZipFileName);
                names.ManifestAbsolute = Path.Combine(folder, names.ManifestFileName);
                names.ZipRepoRelative = paths.ToRepoRelative(names.ZipAbsolute);
                return names;
            }

            /// <summary>오늘 폴더에서 같은 날짜·작업명의 다음 회차 번호를 구한다(.meta 는 정규식에서 자연히 빠짐).</summary>
            private static int NextSequence(string folder, string date, string taskName)
            {
                if (!Directory.Exists(folder)) return 1;

                var pattern = new Regex(
                    "^" + Regex.Escape(date) + @"_\d{2}_\d{2}_" + Regex.Escape(taskName) + @"_(\d{2,})\.(zip|manifest\.json)$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                int max = 0;
                foreach (string file in Directory.GetFiles(folder))
                {
                    Match m = pattern.Match(Path.GetFileName(file));
                    if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                        max = Math.Max(max, n);
                }
                return max + 1;
            }
        }

        /// <summary>대상 후보 1개(아직 내용을 읽기 전).</summary>
        private sealed class CandidateSource
        {
            public readonly string AbsolutePath;
            public readonly string RepoRelativePath;
            public readonly SourceKind Kind;

            public CandidateSource(string absolutePath, string repoRelativePath, SourceKind kind)
            {
                AbsolutePath = absolutePath;
                RepoRelativePath = repoRelativePath;
                Kind = kind;
            }
        }

        /// <summary>ZIP 에 들어간 원본 1개의 스냅샷 기록.</summary>
        private sealed class SourceRecord
        {
            public string RepoRelativePath;
            public SourceKind Kind;
            public long Bytes;
            public string Sha256;
            public DateTimeOffset SnapshotAt;
            public DateTime SourceLastWriteUtc;
        }

        /// <summary>기존 manifest 에서 읽은 "이미 보관됨" 키 모음.</summary>
        private sealed class ArchivedIndex
        {
            public readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<string> UnreadableManifests = new List<string>();
            public int ManifestsScanned;
        }

        /// <summary>한 회차 생성 결과.</summary>
        private sealed class ArchiveResult
        {
            public List<SourceRecord> Records;
            public int SkippedArchived;
            public int SkippedLinks;
            public long ZipBytes;
            public string ZipSha256;
            public int ManifestsScanned;
            public List<string> UnreadableManifests;
            public string StartDateText;
        }

        // ====================================================================
        // manifest JSON 형식 (JsonUtility 용)
        //
        // 필드 이름이 snake_case 인 이유:
        //   Unity 의 JsonUtility 는 "필드 이름 = JSON 키" 로 그대로 쓴다(이름을 바꿔 주는 속성이 없다).
        //   기존 Tools/archive_runtime_logs.py 의 manifest 키(path, sha256, file_count …)와 같은 표기를
        //   쓰려고 C# 관례(PascalCase/_camelCase) 대신 snake_case 로 두었다.
        //   JsonUtility 는 [Serializable] 클래스의 public 필드만 읽고 쓴다.
        // ====================================================================

        /// <summary>manifest 최상위.</summary>
        [Serializable]
        public sealed class LogArchiveManifest
        {
            public string schema;
            public string tool;
            public string created_at_local;
            public string created_at_utc;
            public string task_name;
            public int sequence;
            public int file_count;
            public long total_source_bytes;
            public ManifestSource[] sources;
            public string start_date;
            public string start_date_rule;
            public string dedupe_scope;
            public string prior_archive_dedupe;
            public int skipped_already_archived;
            public int manifests_scanned;
            public string[] unreadable_manifests;
            public ManifestArchive archive;
            public ManifestUploadTarget upload_target;
            public string build_status;
            public string session_role;
            public string[] limits;
        }

        /// <summary>manifest 의 원본 1개.</summary>
        [Serializable]
        public sealed class ManifestSource
        {
            public string path;
            public string kind;
            public long bytes;
            public string sha256;
            public string snapshot_at_local;
            public string snapshot_at_utc;
            public string source_last_write_utc;
            public string snapshot_note;
        }

        /// <summary>manifest 의 ZIP 정보.</summary>
        [Serializable]
        public sealed class ManifestArchive
        {
            public string name;
            public string manifest_name;
            public string local_path;
            public long bytes;
            public string sha256;
            public string entry_path_rule;
            public string local_verification;
        }

        /// <summary>manifest 의 업로드 대상(사용자가 직접 올릴 곳).</summary>
        [Serializable]
        public sealed class ManifestUploadTarget
        {
            public string owner;
            public string repo;
            public string required_visibility;
            public string release_title;
            public string release_tag;
            public string release_url;
            public string upload;
            public string remote_verification;

            /// <summary>해당 월(YYYY-MM)의 업로드 대상 정보를 만든다.</summary>
            internal static ManifestUploadTarget For(string month)
            {
                return new ManifestUploadTarget
                {
                    owner = UploadOwner,
                    repo = UploadRepo,
                    required_visibility = "private",
                    release_title = ReleaseTitlePrefix + month,
                    release_tag = ReleaseTagPrefix + month,
                    release_url = ReleaseUrlFor(month),
                    upload = "upload performed by user",
                    remote_verification = "not performed by archive tool",
                };
            }

            /// <summary>해당 월 Release 페이지 주소(인증 정보가 없는 공개 형식 URL).</summary>
            internal static string ReleaseUrlFor(string month)
            {
                return $"https://github.com/{UploadOwner}/{UploadRepo}/releases/tag/{ReleaseTagPrefix}{month}";
            }
        }

        /// <summary>기존 manifest 읽기용(필요한 키만). 없는 키는 JsonUtility 가 무시한다.</summary>
        [Serializable]
        public sealed class ManifestReadModel
        {
            /// <summary>이 도구가 쓰는 원본 목록 키.</summary>
            public ManifestReadSource[] sources;

            /// <summary>Tools/archive_runtime_logs.py 가 쓰는 원본 목록 키.</summary>
            public ManifestReadSource[] entries;
        }

        /// <summary>기존 manifest 의 원본 1개(경로·해시만).</summary>
        [Serializable]
        public sealed class ManifestReadSource
        {
            public string path;
            public string sha256;
        }
    }
}
