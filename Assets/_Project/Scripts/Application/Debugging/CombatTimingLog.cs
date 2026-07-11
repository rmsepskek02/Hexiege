// ============================================================================
// CombatTimingLog.cs
//
// [TIMING-LOG] 전투 타격 타이밍 동기화 "검증 전용" 임시 런타임 로거.
//
//   ⚠ 검증 완료 후 이 파일과 모든 호출부를 제거할 것.
//     (LogRules.md — "로그를 출력하는 코드"는 작업 완료 후 반드시 제거)
//     제거 방법: 코드베이스 전체에서 문자열 "[TIMING-LOG]" 를 검색하면
//     이 파일 + 모든 계측 삽입 지점을 한 번에 찾아 지울 수 있다.
//
// 무엇을 하는가:
//   전투 타격 타이밍(공격 사이클 시작 → 데미지 적용 → 피격 연출 보류/방출)을
//   LogRules.md 형식에 맞춰 파일/Logcat으로 남긴다. 실기 테스트에서 육안으로
//   판정하기 어려운 "연출 방출 타이밍"을 로그로 계측하기 위한 도구다.
//
// 출력 형식 (LogRules.md 단일 기준):
//   [HH:mm:ss.fff] [LEVEL] [System/Class] 메시지 | key=value, key=value
//
// 출력 분기:
//   - 에디터 : 파일 append
//              (Assets/_Project/Docs/_Logs/2026-07-09/01_12_combat-hit-timing-sync/)
//              호스트/싱글 → RuntimeLog_host.txt
//              클라이언트   → RuntimeLog_client.txt
//   - 실기기 : 같은 문자열을 UnityEngine.Debug.Log 로 출력 (Logcat 수집용)
//
// 레이어:
//   Application 레이어 순수 정적 클래스. System.IO 사용.
//   ※ 네임스페이스가 Hexiege.Application 이므로 "Application" 이름은
//     UnityEngine.Application 을 가린다 → 반드시 UnityEngine.Application.xxx 로
//     명시해야 한다 (isEditor / dataPath).
//
// 알려진 한계 (로그 해석 시 유의):
//   원거리 유닛은 트레이서(발사체) 착탄 콜백으로 OnLocalAttackHit 이 지연 발행되지만,
//   HitPresentationQueue 입장에서는 그 신호가 "타격 프레임" 경로로 들어온다.
//   따라서 원거리 유닛의 지연 방출도 로그상 사유는 "타격프레임"으로 찍힌다.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace Hexiege.Application
{
    /// <summary>
    /// [TIMING-LOG] 전투 타격 타이밍 검증 전용 임시 정적 로거.
    /// 검증 완료 후 파일과 모든 호출부를 제거할 것(LogRules.md).
    /// </summary>
    public static class CombatTimingLog
    {
        /// <summary>
        /// 로그 on/off 스위치. 기본 ON.
        /// 호출부는 반드시 <c>if (CombatTimingLog.Enabled)</c> 로 가드하여,
        /// false일 때 문자열 포맷 비용조차 발생하지 않도록 한다.
        /// </summary>
        public static bool Enabled = true;

        /// <summary>
        /// 로그 저장 폴더(Assets 기준 상대 경로).
        /// LogRules.md 규칙: _Logs/YYYY-MM-DD/HH_MM_[작업명]/
        /// </summary>
        private const string LogRelativeDir =
            "_Project/Docs/_Logs/2026-07-09/01_12_combat-hit-timing-sync";

        /// <summary>
        /// 헤더 2줄을 이미 기록한 파일 경로 집합.
        /// 세션 동안 파일별로 헤더를 딱 1번만 쓰기 위한 플래그.
        /// (host / client 파일이 서로 다르므로 경로 단위로 추적한다.)
        /// </summary>
        private static readonly HashSet<string> _headerWrittenPaths = new HashSet<string>();

        /// <summary>
        /// INFO 레벨 로그. 정상 흐름(공격 사이클 시작, 데미지 적용, 연출 방출 등).
        /// </summary>
        /// <param name="category">[System/Class] 형식 카테고리(예: "Combat/UnitView").</param>
        /// <param name="message">메시지 본문("설명 | key=value, ..." 형식 권장).</param>
        public static void Info(string category, string message) => Write("INFO", category, message);

        /// <summary>
        /// WARN 레벨 로그. 예상 밖이지만 동작은 계속되는 상황
        /// (예: 타임아웃 방출, 피격 VFX 프리셋 미연결).
        /// </summary>
        /// <param name="category">[System/Class] 형식 카테고리.</param>
        /// <param name="message">메시지 본문.</param>
        public static void Warn(string category, string message) => Write("WARN", category, message);

        /// <summary>
        /// 실제 로그 라인을 조립하여 에디터=파일 / 실기기=Debug.Log 로 분기 출력한다.
        /// 예외는 모두 삼켜 게임 흐름에 절대 영향을 주지 않는다(디버그 전용).
        /// </summary>
        private static void Write(string level, string category, string message)
        {
            if (!Enabled) return;

            try
            {
                // LogRules.md 라인 형식: [HH:mm:ss.ms] [LEVEL] [System/Class] 메시지
                string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [{category}] {message}";

                if (UnityEngine.Application.isEditor)
                {
                    // 에디터: 파일에 append (host/client 파일 분리).
                    AppendToFile(line);
                }
                else
                {
                    // 실기기: 파일 저장이 불가능하므로 동일 포맷 문자열을 Logcat으로 출력.
                    UnityEngine.Debug.Log(line);
                }
            }
            catch
            {
                // 디버그 로그 실패가 전투 로직/타이밍에 영향을 주면 안 되므로 조용히 무시.
            }
        }

        /// <summary>
        /// 에디터 전용: 현재 역할(host/client)에 맞는 파일에 로그 라인을 append.
        /// 폴더가 없으면 생성하고, 파일별 최초 기록 시 헤더 2줄을 먼저 쓴다.
        /// </summary>
        private static void AppendToFile(string line)
        {
            // Application.dataPath 는 ".../Assets" 를 가리킨다 → Docs 하위 로그 폴더로 결합.
            string dir = Path.Combine(UnityEngine.Application.dataPath, LogRelativeDir);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // 역할 판정: 네트워크 활성 + 서버가 아니면 클라이언트, 그 외(호스트/싱글)는 host.
            bool isClient = NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer;
            string fileName = isClient ? "RuntimeLog_client.txt" : "RuntimeLog_host.txt";
            string path = Path.Combine(dir, fileName);

            // 파일별 최초 1회 헤더 2줄 기록(LogRules.md 헤더 규칙).
            if (_headerWrittenPaths.Add(path))
            {
                string header =
                    "=== 전투 타격 타이밍 동기화 검증 로그 ===" + Environment.NewLine +
                    $"=== 세션 시작: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===" + Environment.NewLine;
                File.AppendAllText(path, header);
            }

            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
