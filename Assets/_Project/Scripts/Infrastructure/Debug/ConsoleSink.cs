// ============================================================================
// ConsoleSink.cs
// GameLog 의 로그를 유니티 콘솔(에디터 Console / 실기기 Logcat)로 내보내는 sink.
//
// 역할(의존성 역전의 "구현 쪽"):
//   Application 레이어는 ILogSink 라는 계약만 알고, 실제 출력 수단은 모른다.
//   이 클래스가 그 계약을 구현하고 UnityEngine.Debug 로 위임한다.
//   조합 루트(GameBootstrapper)가 인스턴스를 만들어 GameLog.AddSink 로 등록한다.
//   (IUnitFactory→UnitFactory 와 동일한 구조)
//
// ⚠️ enum 이름 충돌 주의 (이 프로젝트에서 실제로 겪은 함정)
//   LogLevel 이 Hexiege.Application 과 Hexiege.Infrastructure 양쪽에 존재한다.
//   이 파일이 속한 네임스페이스는 Hexiege.Infrastructure 이므로,
//   그냥 `LogLevel` 이라고 쓰면 Infrastructure 쪽 enum 으로 해석된다(enclosing 우선).
//   그러면 인터페이스 시그니처와 타입이 달라져 "구현하지 않았다"는 컴파일 에러가 난다.
//   → 인터페이스를 구현하는 자리에서는 반드시 Hexiege.Application.LogLevel 로 완전 수식한다.
//
// 규격 출처: Assets/_Project/Docs/LogRules.md (1.4 형식 / 1.8 sink 구조 / 1.9 예외 처리)
//
// Infrastructure 레이어 — Application(안쪽)을 참조하는 것은 정상 방향이다.
// ============================================================================

using System;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 유니티 콘솔 전용 sink. 파일에는 쓰지 않는다(파일은 FileSink 담당).
    /// 실기기(빌드)에서는 이 출력이 그대로 Android Logcat 에 나타난다.
    /// </summary>
    public sealed class ConsoleSink : Hexiege.Application.ILogSink
    {
        /// <summary>
        /// 로그 한 건을 콘솔에 출력한다.
        ///
        /// 로그 한 줄의 조립은 직접 하지 않고 GameLog.Compose 를 호출한다 —
        /// 형식 규정(LogRules.md 1.4)을 여러 파일에 복사해 두면
        /// 나중에 한쪽만 고쳐져 파일마다 형식이 달라지기 때문이다.
        /// </summary>
        /// <param name="level">심각도(Application 중립 enum).</param>
        /// <param name="system">시스템 영역. 예: "Network".</param>
        /// <param name="className">클래스 이름. 예: "LobbyManager".</param>
        /// <param name="message">메시지 본문(GameLog 가 이미 민감 데이터를 걸러 둔 상태).</param>
        /// <param name="data">"key=value" 구조화 필드(Event=... 이 이미 병합되어 있다).</param>
        /// <param name="exception">함께 남길 예외. 없으면 null.</param>
        public void Write(Hexiege.Application.LogLevel level, string system, string className,
                          string message, string data, Exception exception)
        {
            string line = Hexiege.Application.GameLog.Compose(level, system, className, message, data);

            // 심각도에 맞는 메서드로 출력한다.
            // 이렇게 해야 에디터 콘솔의 필터(Info/Warning/Error 탭)가 제대로 동작하고,
            // Logcat 에서도 우선순위(I/W/E)가 구분된다.
            switch (level)
            {
                case Hexiege.Application.LogLevel.Warn:
                    UnityEngine.Debug.LogWarning(line);
                    break;
                case Hexiege.Application.LogLevel.Error:
                    UnityEngine.Debug.LogError(line);
                    break;
                default:
                    UnityEngine.Debug.Log(line);
                    break;
            }

            // ── 예외는 왜 LogException 으로 따로 내보내는가 (LogRules.md 1.9) ──
            //   Debug.LogError(e.ToString()) 로 눌러 담으면 크래시 리포터가 그것을
            //   "그냥 긴 문자열"로 볼 뿐 예외로 인식하지 못한다.
            //   그러면 스택 심볼화(symbolication)가 되지 않고,
            //   난독화·최적화된 릴리스 빌드에서는 읽을 수 없는 주소 나열만 남는다.
            //
            //   위의 line 출력과 합치지 않고 두 줄로 내보내는 이유:
            //     · line       → Event= / key=value 같은 **집계용 맥락**을 담는다.
            //     · LogException → 리포터가 심볼화할 수 있는 **예외 객체**를 담는다.
            //   둘은 소비자가 서로 달라서, 하나로 합치면 한쪽을 반드시 잃는다.
            //
            //   ⚠️ LogException 은 LogType.Exception 을 발생시켜
            //      GameBootstrapper 의 전역 예외 훅을 되불린다.
            //      그 되먹임은 GameLog 의 재진입 가드(_isEmitting)가 끊는다.
            //      이 sink 는 GameLog.Emit 내부에서 호출되므로 가드가 이미 켜져 있다.
            if (exception != null)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }
    }
}
