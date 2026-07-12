// ============================================================================
// MoveAnimSyncLog.cs
// [MOVESYNC-LOG] 이동/Walk 애니메이션 동기화 진단 "전용 임시" 로거.
//
// 이 파일은 무엇인가:
//   멀티플레이에서 "걷기 애니메이션이 안 나온다 / 유닛이 뒤로 밀린다" 증상의 원인을
//   숫자(로그)로 실증하기 위해 잠시 넣어 두는 계측 도구입니다.
//   검증이 끝나면 이 파일과, 이 파일을 호출하는 // [MOVESYNC-LOG] 마커 코드를
//   전량 삭제합니다. (LogRules.md — 로그 출력 코드는 작업 완료 후 반드시 제거)
//
// 왜 Application 레이어에 두는가:
//   직전 태스크의 동일 목적 로거(CombatTimingLog)와 같은 위치/패턴을 재사용합니다.
//   host/client 판정을 위해 Application 레이어의 NetworkContext 정적 홀더를 읽어야 하고,
//   서버(Infrastructure)와 클라이언트 표현(Presentation) 양쪽에서 모두 호출하므로
//   두 레이어가 공통으로 참조할 수 있는 Application 레이어가 적합합니다.
//   (레이어 규칙상 Application은 Unity.Netcode를 직접 참조하지 않습니다 — NetworkContext만 사용)
//
// 출력 규칙(LogRules.md 1. 런타임 로그 형식 준수):
//   - 라인 형식: [HH:mm:ss.fff] [LEVEL] [System/Class] 메시지 | key=value
//   - 에디터: 파일에 append (RuntimeLog_host.txt / RuntimeLog_client.txt)
//   - 실기기(빌드): 파일 저장 불가 → Debug.Log(Logcat)로 동일 형식 출력
//   - 파일별 최초 1회 헤더 2줄 기록, 저장 폴더는 없으면 자동 생성
//
// 주의:
//   - 예외는 모두 삼킨다(로깅 실패가 게임을 멈추지 않도록).
//   - 호출부는 반드시 `if (MoveAnimSyncLog.Enabled)`로 감싸고 // [MOVESYNC-LOG] 마커를 단다.
// ============================================================================

using System;
using System.IO;
using UnityEngine;

namespace Hexiege.Application
{
    /// <summary>
    /// [MOVESYNC-LOG] 이동/Walk 애니메이션 동기화 진단 전용 임시 정적 로거.
    /// 검증 완료 후 이 클래스와 모든 호출부(// [MOVESYNC-LOG])를 함께 제거한다.
    /// </summary>
    public static class MoveAnimSyncLog
    {
        /// <summary>
        /// 로깅 활성화 스위치. 기본값 true.
        /// 호출부는 `if (MoveAnimSyncLog.Enabled)` 가드로 감싸 계측 오버헤드를 최소화한다.
        /// (false로 바꾸면 계측 자체를 손쉽게 끌 수 있다.)
        /// </summary>
        public static bool Enabled = true;

#if UNITY_EDITOR
        // 로그 저장 폴더 — 이 태스크 전용 경로(LogRules.md 저장 위치 규칙).
        // 임시 로거이므로 경로를 하드코딩한다(검증 후 파일과 함께 제거 예정).
        private const string LogFolder =
            "Assets/_Project/Docs/_Logs/2026-07-12/07_55_movement-walk-anim-sync";

        // 현재 열려 있는 파일 쓰기 스트림. null이면 아직 파일을 열지 않은 상태.
        // (에디터에서만 파일을 쓰므로 #if UNITY_EDITOR 안에 둔다.)
        private static StreamWriter _writer;

        // 현재 스트림이 어느 역할("host"/"client")로 열렸는지 기억.
        // 한 프로세스는 세션 중 host 또는 client 중 하나로 고정되므로 보통 바뀌지 않지만,
        // 만약 바뀌면(재경기 등) 스트림을 다시 연다.
        private static string _openedRole;
#endif

        /// <summary>
        /// INFO 레벨 로그 한 줄 기록.
        /// </summary>
        /// <param name="category">"System/Class" 형식(예: "Move/UnitView"). 대괄호는 자동으로 붙는다.</param>
        /// <param name="message">본문 메시지(뒤에 " | key=value"를 직접 이어 붙여도 됨).</param>
        public static void Info(string category, string message)
        {
            Write("INFO", category, message);
        }

        /// <summary>
        /// WARN 레벨 로그 한 줄 기록. 예상 밖이지만 동작은 계속되는 상황(예: 역방향 이동 감지).
        /// </summary>
        /// <param name="category">"System/Class" 형식(예: "Move/UnitView").</param>
        /// <param name="message">본문 메시지.</param>
        public static void Warn(string category, string message)
        {
            Write("WARN", category, message);
        }

        /// <summary>
        /// 실제 로그 조립 + 출력. 에디터는 파일, 실기기는 Debug.Log.
        /// 어떤 예외가 나도 게임을 멈추지 않도록 전부 삼킨다.
        /// </summary>
        private static void Write(string level, string category, string message)
        {
            if (!Enabled) return;

            try
            {
                // LogRules 라인 형식: [HH:mm:ss.fff] [LEVEL] [System/Class] 메시지
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string line = $"[{timestamp}] [{level}] [{category}] {message}";

#if UNITY_EDITOR
                // 에디터: host/client 판정 후 해당 파일에 append.
                string role = ResolveRole();
                EnsureWriter(role);
                _writer?.WriteLine(line);
#else
                // 실기기(빌드): 파일 저장 불가 → Logcat에 동일 형식으로 출력.
                Debug.Log(line);
#endif
            }
            catch
            {
                // 로깅 실패는 무시(게임 진행 우선). 콘솔 경고조차 남기지 않는다(스팸 방지).
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// NetworkContext로 현재 프로세스가 host인지 client인지 판정한다.
        /// 규칙: 네트워크 활성 && 서버 아님 → "client", 그 외(호스트/싱글) → "host".
        /// </summary>
        private static string ResolveRole()
        {
            bool isClient = NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer;
            return isClient ? "client" : "host";
        }

        /// <summary>
        /// 역할에 맞는 로그 파일 스트림을 준비한다.
        /// 이미 같은 역할로 열려 있으면 재사용하고, 처음이거나 역할이 바뀌면 새로 연다.
        /// 파일이 새로(빈 파일) 만들어질 때만 헤더 2줄을 기록한다(파일별 최초 1회).
        /// </summary>
        private static void EnsureWriter(string role)
        {
            // 이미 같은 역할로 열려 있으면 그대로 사용.
            if (_writer != null && _openedRole == role) return;

            // 역할이 바뀐 경우: 기존 스트림을 닫는다.
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }

            // 저장 폴더가 없으면 중간 경로까지 모두 생성.
            if (!Directory.Exists(LogFolder))
                Directory.CreateDirectory(LogFolder);

            string fileName = role == "client" ? "RuntimeLog_client.txt" : "RuntimeLog_host.txt";
            string fullPath = Path.Combine(LogFolder, fileName);

            // 파일이 없거나 비어 있으면 이번에 헤더를 써야 한다(파일별 최초 1회).
            bool needHeader = !File.Exists(fullPath) || new FileInfo(fullPath).Length == 0;

            // append: true → 기존 기록 보존하며 이어쓰기.
            // AutoFlush: true → 매 줄 즉시 디스크 반영(에디터 크래시에도 유실 방지).
            _writer = new StreamWriter(fullPath, append: true) { AutoFlush = true };
            _openedRole = role;

            if (needHeader)
            {
                // LogRules 헤더 2줄.
                _writer.WriteLine("=== 이동/Walk 애니메이션 동기화 진단 로그 ===");
                _writer.WriteLine($"=== 세션 시작: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            }
        }
#endif
    }
}
