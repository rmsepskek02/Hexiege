// ============================================================================
// MovementLogger.cs
// 유닛 이동/전투(MoveAlongPathV2) 실기 테스트 로그를 파일로 기록하는 정적 유틸리티.
//
// 목적:
//   유니티 에디터/빌드에서 플레이 테스트할 때 발생하는 이동/전투 이벤트를
//   외부 텍스트 파일에 append 형식으로 기록한다.
//   → 개발 에이전트(Claude 등)가 나중에 RuntimeLog.txt 파일을 읽어 버그 원인을 분석할 수 있도록 함.
//
// 로그 파일 위치:
//   {ProjectRoot}/Assets/_Project/Docs/_Logs/2026-05-06/01_19_movement-slot-ghostfix/RuntimeLog.txt
//
// 로그 포맷 예시:
//   === SESSION START: 2026-05-05 14:32:11 ===
//   [14:32:12.345] [UnitID:7] [SPAWN] finalTarget=(Q=3,R=12,S=-15)
//   [14:32:12.456] [UnitID:7] [PATH_CALC] pathLength=8
//   [14:32:12.567] [UnitID:7] [TILE_MOVE] to=(Q=2,R=11,S=-13)
//   [14:32:13.012] [UnitID:7] [SLOT_CLAIM] tile=(Q=2,R=11,S=-13) slot=1 worldPos=(1.5, 0.0, 2.3)
//
// 동작 특성:
//   - 매 호출마다 파일을 열고 한 줄을 쓴 뒤 즉시 닫는다(File.AppendAllText).
//     게임플레이 중 갑작스런 종료(에디터 정지/크래시)에도 데이터 손실이 최소화됨.
//   - 파일 I/O 에러는 try/catch로 흡수 — 게임플레이를 절대 중단시키지 않는다.
//   - #if UNITY_EDITOR || DEVELOPMENT_BUILD 조건부 컴파일.
//     릴리즈 빌드에서는 빈 메서드로 컴파일되어 디스크 I/O 비용/위험이 0이 된다.
//
// 호출 측 사용법(예):
//   MovementLogger.Log(unit.Id, "SPAWN", $"finalTarget={target}");
//   MovementLogger.Log(unit.Id, "TILE_MOVE", $"to={tile}");
//
// Application 레이어 — UnityEngine.Application(.dataPath) 의존, MonoBehaviour 아님.
// (Domain 레이어가 아니므로 Unity 의존성 허용)
// ============================================================================

using System;
using System.IO;
using UnityEngine;

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛 이동/전투 이벤트를 외부 텍스트 파일에 기록하는 정적 로거.
    /// 에디터/개발 빌드에서만 동작하며, 릴리즈 빌드에서는 모든 메서드가 비워진다.
    /// </summary>
    public static class MovementLogger
    {
        // ────────────────────────────────────────────────────────────────────
        // 상수 — 로그 파일 위치
        // ────────────────────────────────────────────────────────────────────

        // 프로젝트 루트 기준 상대 경로(Application.dataPath = ".../Assets" 이므로 한 단계 상위로 ../).
        // 첫 호출 시 EnsureInitialized()에서 절대 경로로 캐싱한다.
        private const string LogRelativePath =
            "/../Assets/_Project/Docs/_Logs/2026-05-11/23_19_unit-movement-redesign/RuntimeLog.txt";

        // ────────────────────────────────────────────────────────────────────
        // 내부 상태
        // ────────────────────────────────────────────────────────────────────

        // 절대 경로 캐시. EnsureInitialized()가 한 번 채우면 이후 재계산하지 않는다.
        // 정적 필드라서 도메인 리로드/플레이 모드 진입 시 자동 리셋됨.
        private static string _logPath;

        // 디렉토리/세션 헤더 처리가 한 번이라도 수행됐는지 여부.
        // 매 Log 호출 시 검사하여 첫 호출에서만 헤더 라인을 기록한다.
        private static bool _initialized;

        // 동일 프레임에 대량 호출이 들어와도 파일 핸들 충돌이 나지 않도록 락.
        // 단일 인스턴스 정적 락이므로 작은 오버헤드만 추가된다.
        private static readonly object _writeLock = new object();

        // ────────────────────────────────────────────────────────────────────
        // 공개 API
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 한 줄의 이벤트 로그를 RuntimeLog.txt 끝에 추가한다.
        ///
        /// 포맷: [HH:mm:ss.fff] [UnitID:{unitId}] [{eventName}] {detail}
        ///
        /// 사용 예:
        ///   MovementLogger.Log(unit.Id, "SPAWN", $"finalTarget={target}");
        ///   MovementLogger.Log(unit.Id, "TILE_MOVE", $"to={tile}");
        ///
        /// 동작:
        ///   - 첫 호출 시 디렉토리 생성 + 세션 시작 헤더 기록을 자동 수행.
        ///   - 파일 I/O 에러가 발생하면 try/catch로 흡수하고 게임플레이는 그대로 진행한다.
        ///
        /// 릴리즈 빌드(UNITY_EDITOR도 DEVELOPMENT_BUILD도 아님)에서는 메서드 본문이
        /// 컴파일되지 않아 호출 자체가 무시된다.
        /// </summary>
        /// <param name="unitId">유닛 식별자(보통 UnitData.Id).</param>
        /// <param name="eventName">이벤트 카테고리 식별자(예: "SPAWN", "TILE_MOVE").</param>
        /// <param name="detail">이벤트 상세 정보(자유 형식 문자열).</param>
        public static void Log(int unitId, string eventName, string detail)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                EnsureInitialized();

                // 시각 포맷 — HH:mm:ss.fff (밀리초까지). 하루를 넘어가는 디버깅이 거의 없으므로 충분.
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string line = $"[{timestamp}] [UnitID:{unitId}] [{eventName}] {detail}";

                // 락으로 보호하여 동시 다발적인 호출이 와도 파일 깨짐을 방지.
                lock (_writeLock)
                {
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // 파일 I/O 실패는 게임플레이에 영향을 주지 않도록 침묵 처리.
                // 디스크 풀 / 권한 문제 / 디렉토리 잠김 등 모든 예외를 흡수한다.
                // 디버그 콘솔에 노출하면 매 프레임 수십 줄의 에러가 쏟아질 수 있어 의도적으로 무시.
            }
#endif
        }

        /// <summary>
        /// 다음 플레이 시작 시 새 세션 헤더가 다시 기록되도록 초기화 상태를 리셋한다.
        /// 일반적으로는 호출할 필요가 없다 — 정적 필드는 도메인 리로드 시 자동으로 리셋된다.
        /// 강제로 헤더를 재출력하고 싶을 때만 사용.
        /// </summary>
        public static void ResetSession()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (_writeLock)
            {
                _initialized = false;
            }
#endif
        }

        /// <summary>
        /// 게임 시작 시 1회 호출되는 세션 시작 마커.
        /// GameBootstrapper.Start() 등 게임 시작 진입점에서 호출하면,
        /// "이 시점부터 새 플레이 세션이 시작됐다"는 구분선이 로그 파일에 명시적으로 기록된다.
        /// 내부적으로는 ResetSession + EnsureInitialized 조합으로,
        /// 도메인 리로드가 정상 동작하더라도 명시적 호출을 우선한다.
        ///
        /// 동작:
        ///   1) 기존 _initialized 플래그를 강제로 false로 되돌린다.
        ///   2) 첫 Log() 호출이 일어나기 전에 EnsureInitialized를 미리 호출하여
        ///      세션 시작 헤더 라인이 즉시 파일에 기록되도록 한다.
        ///   3) 이후 들어오는 Log() 호출은 EnsureInitialized 분기에서 즉시 빠져나가
        ///      재초기화 비용이 들지 않는다.
        ///
        /// 릴리즈 빌드(UNITY_EDITOR도 DEVELOPMENT_BUILD도 아님)에서는 메서드 본문이
        /// 컴파일되지 않아 호출 자체가 무시된다.
        /// </summary>
        public static void SessionStart()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (_writeLock)
            {
                // 기존 상태 무시하고 강제로 새 세션 헤더가 한 줄 더 들어가도록 리셋.
                _initialized = false;

                try
                {
                    // 헤더 즉시 기록 — 첫 Log 호출을 기다리지 않고 명시적으로 한 줄 남긴다.
                    EnsureInitialized();
                }
                catch (Exception)
                {
                    // 디스크 I/O 실패는 그대로 흡수 — 게임플레이를 중단시키지 않는다.
                }
            }
#endif
        }

        // ────────────────────────────────────────────────────────────────────
        // 내부 헬퍼
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 첫 Log 호출 시 한 번만 수행되는 초기화.
        /// 1) Application.dataPath 기준으로 절대 경로 계산.
        /// 2) 상위 디렉토리들이 없으면 생성.
        /// 3) 세션 시작 구분선을 파일 끝에 추가.
        /// </summary>
        private static void EnsureInitialized()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_initialized) return;

            // 절대 경로 1회 계산.
            // Application.dataPath = "{ProjectRoot}/Assets" 이므로 "/../Assets/..." 가 프로젝트 루트 기준 경로가 된다.
            _logPath = UnityEngine.Application.dataPath + LogRelativePath;

            // 상위 디렉토리 자동 생성. Path.GetDirectoryName이 "/../" 같은 상대 표기를 그대로 보존하므로
            // Directory.CreateDirectory가 정상적으로 처리한다.
            string dir = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 세션 시작 헤더(append 모드 — 기존 데이터 보존).
            string sessionStart = $"=== SESSION START: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}";
            File.AppendAllText(_logPath, sessionStart);

            _initialized = true;
#endif
        }
    }
}
