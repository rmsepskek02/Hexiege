// ============================================================================
// NetworkMapTransfer.cs
// Host 가 확정한 맵을 Client 에게 "조각으로 나눠 보내고, 잘 받았다는 답을 받는"
// 왕복 전체를 소유하는 NetworkBehaviour.
//
// 🔴 이 파일은 3단계 B 에서 「뼈대」로 태어났고, F·G·H 에서 알맹이가, I 에서 게이트가 채워졌다.
//   지금 이 클래스가 실제로 하는 일은 다음과 같다.
//     [Host]   로비에서 맵을 준비한다 → 헤더를 알리고 조각을 보낸다 →
//              10초 안에 답이 없으면 **1회만** 다시 보낸다 → 답을 받아 결말을 판정한다 →
//              **성공일 때만** 씬 전환 게이트를 연다(OnHostTransferSucceeded)
//     [Client] 헤더를 받아 재조립기를 만든다 → 조각을 다 모은다 → 길이·형식 버전 확인 →
//              SHA-256 대조 → 역직렬화 → 「D 방식」 재생성 검증 → 결과를 답으로 보낸다
//
// 🔴 **이 클래스가 씬 전환 게이트의 주인이다(3단계 I).**
//    규칙 16 은 *"해시가 같을 때만 전투 씬 전환을 시작한다"* 와 *"어떤 실패에서도
//    전투 씬으로 전환하지 않는다"* 를 요구한다. 그 판정을 내리는 곳이 여기이므로,
//    바깥(NetworkGameManager)은 아래 두 이벤트를 구독해 **통보받은 대로만** 움직인다.
//      · OnHostTransferSucceeded → NetworkGameManager.LoadGameScene()
//      · OnHostTransferFailed    → 씬을 넘기지 않고 로비 유지 + 로딩 UI 내리기
//    ⚠️ 바깥이 스스로 "이제 됐겠지" 하고 씬을 넘기는 길을 새로 만들지 말 것.
//       그 순간 게이트가 우회되고 규칙 16 이 코드에서 사라진다.
//
// ── 단계별 소유 범위 ──────────────────────────────────────────────────────
//   [B 에서]  스폰·디스폰 수명, 상태 기계와 전이 로그, nonce(전송 회차 번호),
//             RPC 3종의 왕복, MapChunkAssembler 를 이용한 조각 분할·재조립,
//             전송 한도를 재기 위한 프로브(더미 바이트)
//   [F 에서]  Host 권위 맵 준비(MapPreparationUseCase) · 진짜 canonical 바이트 싣기 ·
//             package 헤더(형식 버전·전체 길이·조각 수·SHA-256) · timeout 10초 감시 ·
//             1회 재전송 · 연결 끊김 즉시 실패 · MapHandoff 에 확정 맵 심기 · 운영 로그 5종
//   [G 에서]  Client 의 길이·형식 버전 확인 → SHA-256 대조 → 역직렬화 → MapReady ACK
//   [H 에서]  Client 의 「D 방식」 재생성 검증(MapVerificationUseCase)
//   [I 에서]  씬 전환 게이트(아래 OnHostTransferSucceeded / OnHostTransferFailed) ·
//             Host 결말을 한 번만 통보하는 NotifyHostOutcomeOnce.
//             바깥쪽 배선(스폰 주체 · 로비 진입점 · Client 투영 · 임시 고정 seed 비활성화)은
//             NetworkGameManager.cs · BattleViewModel.cs · GameBootstrapper.Map.cs 에 있다.
//
// ── 왜 Infrastructure 레이어인가 ──────────────────────────────────────────
//   NetworkBehaviour 는 이 프로젝트에서 **Infrastructure 에만** 둘 수 있다.
//   (Application 이 Unity.Netcode 를 직접 참조하는 것도 금지되어 있다.)
//   맵을 "만드는" 쪽은 Application(MapPreparationUseCase, 순수 C#)이고,
//   맵을 "보내는" 쪽이 여기다. 방향이 Infrastructure → Application 이라 역참조가 아니다.
//
// ── ⚠️ 이 파일은 이 저장소 환경에서 컴파일 검증이 불가능하다 ───────────────
//   Unity.Netcode 를 참조하므로 mcs/mono 로 빌드할 수 없다. **Unity 에서만 확인된다.**
//   그래서 "검증 가능한 계산 부분"은 일부러 MapChunkAssembler.cs(순수 C#)로 빼 두었고,
//   그쪽은 조각 1개/여러 개/중복/역순/하나 빠짐/길이 불일치 6가지 입력으로 실제 실행 검증을 마쳤다.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 맵 전송 왕복의 진행 단계.
    /// 상태가 바뀔 때마다 로그를 한 줄 남긴다(LogRules 1.14 금지 8 — 매 틱이 아니라 **전이** 시점에만).
    /// </summary>
    public enum MapTransferState
    {
        /// <summary>아무것도 하고 있지 않다. 스폰 직후의 상태.</summary>
        Idle,

        /// <summary>[Host] 시작을 알리고 조각을 보내는 중이다.</summary>
        Sending,

        /// <summary>[Host] 조각을 다 보냈고 상대의 MapReady 응답을 기다리는 중이다.</summary>
        WaitingReady,

        /// <summary>[Client] 시작 통보를 받아 조각을 모으는 중이다.</summary>
        Receiving,

        /// <summary>한 회차가 정상적으로 끝났다.</summary>
        Completed,

        /// <summary>한 회차가 실패로 끝났다.</summary>
        Failed
    }

    /// <summary>
    /// 맵 전송·검증이 실패했을 때의 내부 error code.
    /// Client 가 Host 에게 <b>정수값 그대로</b> 실어 보내며(RPC 인자), 로그의 <c>ErrorCode=</c> 필드가 된다.
    ///
    /// 🔴 숫자 값은 로그와 RPC 양쪽에 그대로 남으므로 <b>바꾸지 말 것.</b> 새 사유는 뒤에 추가한다.
    /// 🔴 규칙 16 이 「재전송해도 되는 실패」와 「즉시 실패」를 가르므로, 각 멤버에 어느 쪽인지 적어 둔다.
    ///    재전송이 허용되는 것은 <b>timeout 과 조각 불완전 수신뿐</b>이다.
    /// </summary>
    public enum MapTransferErrorCode
    {
        /// <summary>실패가 아니다(성공).</summary>
        None = 0,

        /// <summary>package 헤더가 올바르지 않다(크기·조각 수·해시 길이). → <b>즉시 실패</b></summary>
        InvalidHeader = 1,

        /// <summary>지원하지 않는 canonical 형식 버전이다. → <b>즉시 실패</b></summary>
        UnsupportedMapVersion = 2,

        /// <summary>헤더의 형식 버전과 본문(canonical)의 형식 버전이 다르다. → <b>즉시 실패</b></summary>
        MapVersionHeaderMismatch = 3,

        /// <summary>선언된 용량 한도를 넘었다. → <b>즉시 실패</b></summary>
        PackageTooLarge = 4,

        /// <summary>Host/Client 해시가 다르다. → <b>즉시 실패</b>(같은 데이터를 다시 보내도 결과가 같다)</summary>
        HashMismatch = 5,

        /// <summary>받은 바이트를 맵 정의로 해석하지 못했다. → <b>즉시 실패</b></summary>
        DecodeFailed = 6,

        /// <summary>「D 방식」 재생성 불일치 또는 공정성 검증 실패. → <b>즉시 실패</b></summary>
        VerificationFailed = 7,

        /// <summary>10초 안에 응답이 오지 않았다. → <b>재전송 1회 허용</b>(규칙 16)</summary>
        ResponseTimeout = 8,

        /// <summary>조각이 다 모였다고 판정됐는데 재조립 결과를 꺼내지 못했다(재조립기 버그). → <b>즉시 실패</b></summary>
        AssembleFailed = 9,

        /// <summary>전송 중 연결이 끊겼다. → <b>즉시 실패</b>(보낼 상대가 없다)</summary>
        Disconnected = 10
    }

    /// <summary>
    /// 맵 전송 전용 NetworkObject. Host 가 동적으로 스폰해서 쓰며,
    /// 최초 경기와 재경기(NewMap)가 **같은 객체·같은 프로토콜**을 공유한다
    /// (GameSystemRules_RandomMap.md 규칙 16 — "씬에 종속되지 않는 공용 전송 경로").
    ///
    /// ⚠️ 이 클래스는 "씬 재로드를 넘어 살아남는 방법"을 스스로 정하지 않는다.
    ///    그 조건은 3단계 A 에서 사용자 실기로 확인하기로 한 항목이고 **아직 확정되지 않았다.**
    ///    확정되기 전에 DontDestroyOnLoad 같은 것을 임의로 넣으면 근거 없는 결정이 박힌다.
    /// </summary>
    public class NetworkMapTransfer : NetworkBehaviour
    {
        // ====================================================================
        // 로그 상수
        //
        // 🔴 이 파일에는 System 값이 **두 개** 있다. 일부러 그렇게 두었다.
        //    LogRules 1.4 는 System 을 「그 로그가 다루는 기능」으로 정하라고 하는데,
        //    이 클래스에는 성격이 다른 두 부류의 로그가 섞여 있기 때문이다.
        //      · "스폰됐다 / RPC 가 왕복했다 / 조각이 도착했다" → **전송 장치**에 관한 일 → "Network"
        //      · "이 판의 맵이 확정됐다 / 해시가 다르다 / 검증에 떨어졌다" → **맵**에 관한 일 → "Map"
        //    ⚠️ 값을 늘리는 것은 지표를 쪼갤 위험이 있어 가볍게 할 일이 아니다. 그래도 나눈 이유는
        //       2단계 K 가 이미 맵 준비 로그에 "Map" 을 쓰고 있어서, 맵 전송·검증 결말을
        //       "Network" 로 적으면 **같은 「맵이 준비됐는가」 지표가 두 System 으로 갈리기** 때문이다.
        //       나누는 기준은 "코드가 어느 폴더에 있는가"가 아니라 "무엇에 관한 로그인가"다.
        // ====================================================================

        /// <summary>
        /// 전송 장치 자체(스폰·디스폰·RPC 왕복·조각 처리)에 관한 로그의 System 값.
        /// 이 부류는 전부 <b>개발 축</b>이다 — 에디터 2인 구성에서 그대로 재현되기 때문이다.
        /// </summary>
        private const string TransferLogSystem = "Network";

        /// <summary>
        /// 이 판의 맵 자체(준비 결과·해시 대조·Client 검증)에 관한 로그의 System 값.
        /// 2단계 K 가 <c>Bootstrap/GameBootstrapper.Map.cs</c> 에서 쓰기 시작한 값과 같은 값이며,
        /// 오타 하나로 지표가 갈라지지 않도록 상수로 묶어 둔다.
        /// </summary>
        private const string MapLogSystem = "Map";

        /// <summary>
        /// 해시를 로그 필드에 실을 때 쓰는 길이(16진수 자릿수).
        /// 🔴 <b>대조는 원본 32바이트로 하고 로그에는 앞 16자만 싣는다</b> — 2단계 K 가 정한 규약이며
        ///    <c>GameBootstrapper.Map.cs</c> 의 <c>ToMapHashField</c> 와 같은 값이어야 한다.
        ///    원본은 64자라 그대로 실으면 로그 한 줄이 세 배로 길어진다.
        /// </summary>
        private const int MapHashLogLength = 16;

        // ====================================================================
        // 전송 한도 — 🔴 아직 실측 전이다. 여기 있는 숫자는 전부 「잠정값」이다.
        // ====================================================================

        /// <summary>
        /// 🔴🔴 **실측이 끝났는가. 지금은 false 다.** 🔴🔴
        ///
        /// 아래 조각 크기를 확정하려면 두 숫자를 실제로 재야 하는데, 둘 다
        /// **사용자 실기 2대로만 잴 수 있고 이 저장소 환경에서는 잴 방법이 없다.**
        ///   ① NGO RPC 한 번에 실제로 실려 가는 페이로드 상한
        ///      (씬의 UnityTransport 설정값 m_MaxPayloadSize = 6144 는 **설정값이지 실효 상한이 아니다.**
        ///       NGO 헤더와 FastBufferWriter 오버헤드가 그 안에서 먼저 소비된다.)
        ///   ② Relay 를 경유했을 때의 실효 MTU
        ///      (로컬 127.0.0.1 로 잰 값을 Relay 값으로 삼으면 안 된다.)
        ///
        /// 재는 방법은 아래 RunTransferProbe 가 준비해 두었다.
        /// 실측이 끝나면 ⓐ 이 값을 true 로 바꾸고 ⓑ 조각 크기를 확정값으로 교체하며
        /// ⓒ 두 숫자와 **측정 방법**을 TechnicalDesignDocument.md 「전송 프로토콜」 절에 적는다.
        /// (규칙 16 이 "값은 구현 시 NGO 실측으로 확정하고 근거와 함께 TDD 에 기록한다"고 지시한다.)
        /// </summary>
        public const bool IsChunkSizeMeasured = false;

        /// <summary>
        /// 🔴 **잠정** 조각 크기(바이트). 이름에 Provisional 을 붙인 것은 실수 방지 장치다 —
        /// 이 이름을 쓰는 코드를 읽는 사람이 "아직 안 정해진 값"임을 바로 알아채게 하려는 것이다.
        ///
        /// 왜 하필 1024 인가: **근거가 없다.** 그래서 확정값이 아니라 잠정값이다.
        /// 규칙 16 이 경계한 것이 바로 "근거 없는 숫자를 근거 없는 다른 숫자로 바꾸는 일"이므로,
        /// 실측 전까지는 값을 못 박지 않고 「잠정」이라는 사실 자체를 코드에 남긴다.
        ///
        /// ⚠️ 참고로 실전에서 조각은 거의 항상 **1개**다. 맵 canonical 바이트가
        ///    323~343바이트(공식 319 + 4N + 20D, 장식 D=0)라서 어떤 잠정값을 넣어도
        ///    한 조각에 들어간다. 그래도 조각 구조를 만들어 두는 이유는 규칙 16 이
        ///    조각 전송을 요구하고, 장식이 도입되면 조각 수가 자연히 늘기 때문이다.
        /// </summary>
        public const int ProvisionalChunkSizeBytes = 1024;

        /// <summary>
        /// 🔴 **잠정** package 전체 한도(바이트). 규칙 16 의 「용량 한도 초과 = 즉시 실패」를
        /// 판정하려면 한도 숫자가 하나 있어야 해서 둔 값이다.
        ///
        /// 왜 하필 65536 인가: 규칙 16 의 주석이 *"현재 값(조각 1KB · 전체 64KB)은 왜 그 값인지의
        /// 근거가 문서 어디에도 없다"* 고 적어 둔 그 64KB 를 **잠정값으로 그대로 이어받은 것**이다.
        /// 즉 위 조각 크기와 마찬가지로 **근거가 없어서 잠정**이며, 확정은 실측 뒤에 한다.
        ///
        /// ⚠️ 실전 canonical 바이트는 323~343바이트라 이 한도의 **0.6% 도 쓰지 않는다.**
        ///    그래서 이 판정은 정상 경로에서는 절대 걸리지 않고, **손상되거나 위변조된 헤더**가
        ///    말도 안 되는 크기를 선언했을 때 메모리를 통째로 잡아먹는 것을 막는 역할을 한다.
        /// </summary>
        public const int ProvisionalMaxPackageBytes = 65536;

        /// <summary>
        /// Host 가 응답을 기다리는 시간(초). 규칙 16 이 **10초**로 정한 값이라 이것은 잠정값이 아니다.
        /// </summary>
        public const int TransferTimeoutSeconds = 10;

        /// <summary>
        /// 같은 준비 작업을 다시 보낼 수 있는 횟수. 규칙 16 이 **1회**로 못 박았다.
        /// 🔴 "1회 더"는 **timeout 또는 조각 불완전 수신일 때만**이다. 미지원 MapVersion ·
        ///    용량 한도 초과 · 해시 불일치 · 역직렬화 실패 · 공정성 검증 실패 · 연결 끊김은
        ///    **즉시 실패**이며 같은 데이터를 다시 보내지 않는다(같은 데이터를 다시 보내 봐야
        ///    결과가 같기 때문이다).
        /// </summary>
        public const int MaxResendCount = 1;

        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>현재 진행 단계. 바뀔 때는 반드시 <see cref="SetState"/> 를 거친다(로그를 놓치지 않으려고).</summary>
        private MapTransferState _state = MapTransferState.Idle;

        /// <summary>
        /// 전송 회차 번호. Host 가 회차를 시작할 때마다 1씩 올린다.
        ///
        /// 왜 필요한가(초급자용 설명): 재전송이나 재경기 때문에 **지난 회차의 조각이 뒤늦게**
        /// 도착할 수 있다. 번호가 없으면 그 낡은 조각을 이번 회차 버퍼에 섞어 넣게 되고,
        /// 그러면 해시가 어긋나 원인을 찾기 어려운 실패가 된다. 번호가 다르면 그냥 버린다.
        /// </summary>
        private ulong _activeNonce;

        /// <summary>
        /// [Client 전용] 지금 회차의 조각을 모으는 재조립기.
        /// 🔴 이 객체는 **완성되기 전에는 절대 바이트를 내주지 않는다**(MapChunkAssembler 참조).
        /// 회차가 없을 때는 null 이다.
        /// </summary>
        private MapChunkAssembler _assembler;

        /// <summary>
        /// 이번 회차의 결말 로그를 이미 남겼는가.
        /// 🔴 결말 키 4종은 **서로 배타적**이라 한 회차에 정확히 한 줄만 남아야 한다
        ///    (LogRules 1.5 의 3단계 E 블록 「4 + 1 구조」). 재전송·중복 응답·연결 끊김이
        ///    겹치면 결말 자리를 두 번 밟을 수 있어서, 그 자리를 이 깃발 하나로 막는다.
        /// </summary>
        private bool _outcomeLogged;

        /// <summary>
        /// [Host] 이번 회차의 결말을 씬 전환 게이트에 이미 통보했는가(3단계 I).
        ///
        /// 🔴 <see cref="_outcomeLogged"/> 와 <b>같은 목적의 짝</b>이다 — 그쪽은 "결말 로그를
        ///    한 줄만 남긴다", 이쪽은 "결말 통보를 한 번만 한다". 굳이 깃발을 따로 둔 이유는,
        ///    로그 억제와 게이트 통보가 <b>서로 다른 이유로 두 번 밟힐 수 있기</b> 때문이다.
        ///    (예: 응답이 늦게 도착한 뒤 연결이 끊기면 결말 자리를 두 번 지나간다.)
        ///    통보가 두 번 가면 "실패로 로비에 남는다"고 판정한 뒤에 성공 통보가 뒤따라 와
        ///    <b>실패한 판인데 전투 씬으로 넘어가는</b> 최악의 경우가 생긴다.
        /// </summary>
        private bool _hostOutcomeNotified;

        // ── [Host 전용] 이번 회차의 package ────────────────────────────────

        /// <summary>[Host] 이번 회차에 보내는 확정 맵. 회차가 없으면 null.</summary>
        private MapPreparationResult _hostPrepared;

        /// <summary>[Host] 이번 회차의 조각들. 재전송 때 **똑같은 것을 다시 보낸다**.</summary>
        private byte[][] _hostChunks;

        /// <summary>[Host] 이번 회차의 조각 크기.</summary>
        private int _hostChunkSize;

        /// <summary>[Host] 이번 회차 payload 의 전체 바이트 수.</summary>
        private int _hostTotalBytes;

        /// <summary>[Host] 이번 회차 payload 의 SHA-256 원본 32바이트. 🔴 대조는 이 값으로 한다.</summary>
        private byte[] _hostHash;

        /// <summary>[Host] 이번 회차의 canonical 형식 버전(package 헤더에 싣는 값).</summary>
        private int _hostMapVersion;

        /// <summary>[Host] 이번 회차에 보낸 횟수(최초 1 + 재전송). 규칙 12 로그 항목 「전송 횟수」.</summary>
        private int _hostSendCount;

        /// <summary>[Host] 이번 회차의 재전송 횟수. 규칙 12 로그 항목 「재전송 횟수」. 상한은 <see cref="MaxResendCount"/>.</summary>
        private int _hostResendCount;

        /// <summary>
        /// [Host] 응답을 기다리는 마감 시각(초, <see cref="Time.realtimeSinceStartup"/> 기준).
        ///
        /// ⚠️ <b>realtime 을 쓰는 이유</b>: 로딩 중에는 <c>Time.timeScale</c> 이 0 일 수 있고,
        ///    그러면 <c>Time.time</c> 은 아예 흐르지 않아 10초가 영영 지나지 않는다.
        ///    (같은 이유로 GameEndUI 의 countdown 도 WaitForSecondsRealtime 을 쓴다.)
        /// </summary>
        private float _hostResponseDeadline;

        /// <summary>[Host] 이번 회차가 한도 실측용 프로브인가(진짜 맵이 아니다).</summary>
        private bool _hostIsProbe;

        // ── [Client 전용] 이번 회차의 헤더 ────────────────────────────────

        /// <summary>[Client] Host 가 알려 준 기대 해시(원본 32바이트).</summary>
        private byte[] _clientExpectedHash;

        /// <summary>[Client] Host 가 알려 준 canonical 형식 버전(package 헤더 값).</summary>
        private int _clientHeaderMapVersion;

        /// <summary>[Client] Host 가 알려 준 조각 수.</summary>
        private int _clientExpectedChunkCount;

        /// <summary>[Client] 이번 회차가 한도 실측용 프로브인가.</summary>
        private bool _clientIsProbe;

        /// <summary>
        /// [Client] 같은 nonce 의 시작 통보를 몇 번 받았는가(= Host 가 몇 번 보냈는가).
        /// 규칙 12 의 「전송/재전송 횟수」를 Client 쪽 로그에도 채우기 위한 값이다.
        /// ⚠️ 권위 있는 값은 Host 로그 쪽이다. 이것은 **Client 가 관측한** 횟수다.
        /// </summary>
        private int _clientReceiveCount;

        // ====================================================================
        // 읽기 전용 진행 상황 (테스트·로그·I 단계 배선에서 쓴다)
        // ====================================================================

        /// <summary>현재 진행 단계.</summary>
        public MapTransferState State { get { return _state; } }

        /// <summary>현재 전송 회차 번호. 아직 한 번도 시작하지 않았으면 0.</summary>
        public ulong ActiveNonce { get { return _activeNonce; } }

        /// <summary>
        /// [Host] 이번 회차에 확정된 맵. 아직 없으면 null.
        /// ⚠️ 전투 씬으로의 인계는 이 프로퍼티가 아니라 <see cref="MapHandoff"/> 가 맡는다
        ///    (씬 재로드를 넘어야 하므로). 여기 남는 것은 진단·재현용 참조 하나다.
        /// </summary>
        public MapPreparationResult HostPreparedMap { get { return _hostPrepared; } }

        /// <summary>이번 회차에 보낸 횟수(최초 1 + 재전송). 규칙 12 로그 항목.</summary>
        public int SendCount { get { return _hostSendCount; } }

        /// <summary>이번 회차의 재전송 횟수. 규칙 12 로그 항목.</summary>
        public int ResendCount { get { return _hostResendCount; } }

        // ====================================================================
        // [Host] 씬 전환 게이트가 구독하는 결말 알림 — 🔴 3단계 I
        //
        // 🔴 이 두 이벤트가 「씬 전환 게이트」의 전부다.
        //    규칙 16 은 *"해시가 같을 때만 전투 씬 전환을 시작한다"* 와
        //    *"어떤 실패에서도 전투 씬으로 전환하지 않는다"* 를 요구한다.
        //    그 판정을 내리는 주체가 이 클래스이므로(게이트의 주인 = Infrastructure),
        //    바깥(NetworkGameManager)은 **판정 결과를 통보받아 씬을 로드할 뿐**이다.
        //    바깥이 스스로 "이제 됐겠지" 하고 씬을 넘기는 길을 만들지 말 것 —
        //    그 순간 게이트가 우회되고 규칙 16 이 코드에서 사라진다.
        //
        // ⚠️ Host 쪽에서만 발행된다. Client 는 씬 전환을 스스로 시작하지 않는다
        //    (NGO SceneManager 가 서버 주도로 모든 클라이언트를 함께 옮긴다).
        // ⚠️ 프로브 회차(RunTransferProbe)에서는 발행하지 않는다 — 실측 작업이
        //    게임 흐름(씬 전환)을 건드리면 안 된다.
        // ====================================================================

        /// <summary>
        /// [Host] 이번 회차가 <b>성공</b>으로 끝났다. Host/Client 해시가 일치했고
        /// 확정 맵이 <see cref="MapHandoff"/> 에 심어진 뒤에 발행된다.
        /// 구독자는 이때 비로소 전투 씬 로드를 시작해도 된다.
        /// </summary>
        public event System.Action OnHostTransferSucceeded;

        /// <summary>
        /// [Host] 이번 회차가 <b>실패</b>로 끝났다. 인자는 실패 사유(내부 error code)다.
        /// 구독자는 <b>씬을 전환하지 않고</b> 로비를 유지해야 한다(규칙 16).
        /// </summary>
        public event System.Action<MapTransferErrorCode> OnHostTransferFailed;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출된다. Host 가 이 프리팹을 Spawn 하면 양쪽에서 각각 한 번씩 불린다.
        ///
        /// 축 판정(LogRules 1.2 — 두 축을 **둘 다** 정해야 한다):
        ///   축 A = Info — 의도된 흐름이다. 문제가 아니다.
        ///   축 B = **개발** — 두 질문 중 ①에서 "아니오"가 나온다.
        ///     ① 플레이어 기기에서만 벌어지는가? → **아니오.** 스폰·디스폰은 에디터 2인 구성
        ///        (에디터 Host + 또 하나의 클라이언트)에서 Relay 없이도 그대로 재현된다.
        ///        회선 품질이나 그 판의 seed 같은 "그 기기에서만 생기는 조건"에 달려 있지 않다.
        ///     → 하나라도 "아니오"면 개발이다. 선례와도 일치한다 —
        ///        「스폰/디스폰/구독완료/RPC 수신 덤프」는 이 프로젝트에서 계속 개발 축으로 판정해 왔다.
        ///   ⚠️ 운영 키 5종은 **전송의 결말**용이다. 스폰됐다는 사실에 그 키를 붙이면
        ///      장애 지표가 정상 사건으로 오염된다.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // 🔴 규칙 16 의 「연결 끊김은 즉시 실패」를 실제로 판정하려면 끊김을 알아야 한다.
                //    서버만 구독한다 — 실패 판정을 내리는 주체가 Host 이기 때문이다.
                //    (선례: ReconnectionHandler 도 같은 자리에서 같은 방식으로 구독·해제한다.)
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "맵 전송 객체 스폰 완료",
                             $"Role={(IsServer ? "Host" : "Client")}, IsServer={IsServer}, " +
                             $"NetworkObjectId={NetworkObjectId}, State={_state}");
        }

        /// <summary>
        /// 네트워크 디스폰 시 호출된다. 회차 상태를 남기지 않고 깨끗이 지운다.
        ///
        /// ⚠️ 왜 여기서 반드시 비워야 하는가(초급자용):
        ///    재조립기를 들고 있는 채로 디스폰되면, 다음 회차에 **지난 회차의 절반짜리 버퍼**가
        ///    살아 있을 수 있다. 규칙 16 이 "부분 데이터는 어떤 검증이나 맵 구성에도 쓰지 않는다"고
        ///    못 박은 이유가 그것이다. 참조를 끊어 두는 것이 가장 확실한 예방이다.
        ///
        /// 🔴 구독 해제를 빠뜨리면 디스폰된 객체가 계속 콜백을 받아 이미 끝난 회차를
        ///    실패로 만들거나 예외를 낸다. 구독한 자리(OnNetworkSpawn)와 **같은 조건**으로 푼다.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "맵 전송 객체 디스폰",
                             $"Role={(IsServer ? "Host" : "Client")}, " +
                             $"Nonce={_activeNonce}, State={_state}");

            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            ResetSession();

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// [Host 전용] 응답 timeout 감시. 규칙 16 의 「10초」가 실제로 흐르는 자리다.
        ///
        /// ⚠️ <b>가드에는 로그를 넣지 않는다.</b> 여기에 걸리는 것은 정상 흐름이고
        ///    상태 <b>전이</b> 지점이 아니라, 매 프레임 남기면 LogRules 1.14 금지 8(스팸)에 걸린다.
        /// ⚠️ IsSpawned 를 **앞에** 두는 이유는 단락 평가 때문이다 — 스폰되지 않은 상태(싱글플레이)
        ///    에서는 IsServer 를 아예 건드리지 않게 된다. NetworkManager 가 멈춘 뒤에도 IsServer 가
        ///    참일 수 있어 RPC 발신이 예외로 터지는 사고가 이 프로젝트에 실제로 있었다.
        /// </summary>
        private void Update()
        {
            if (!IsSpawned || !IsServer) return;
            if (_state != MapTransferState.WaitingReady) return;
            if (Time.realtimeSinceStartup < _hostResponseDeadline) return;

            HandleHostTimeout();
        }

        // ====================================================================
        // 상태 전이
        // ====================================================================

        /// <summary>
        /// 상태를 바꾸고 그 전이를 로그로 한 줄 남긴다.
        ///
        /// 같은 값으로 바꾸는 호출은 **아무 일도 하지 않는다.** 이유는 LogRules 1.14 금지 8 —
        /// 상태가 실제로 바뀌지 않았는데 줄을 남기면 그게 곧 스팸이고, 정작 필요한 줄이 묻힌다.
        /// </summary>
        /// <param name="next">바꿔 넣을 상태.</param>
        /// <param name="reason">왜 바뀌었는지(사람이 읽을 문장). 집계 키가 아니라 메시지 쪽에 들어간다.</param>
        private void SetState(MapTransferState next, string reason)
        {
            if (_state == next)
            {
                return;
            }

            MapTransferState previous = _state;
            _state = next;

            // 축 A = Info(의도된 흐름) · 축 B = 개발(위 OnNetworkSpawn 의 판정과 같은 이유).
            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             $"맵 전송 상태 전이 — {reason}",
                             $"Role={(IsServer ? "Host" : "Client")}, " +
                             $"PreviousState={previous}, State={next}, Nonce={_activeNonce}");
        }

        /// <summary>
        /// 회차 상태를 비운다. 회차가 끝났거나 디스폰될 때 부른다.
        /// nonce 는 **일부러 유지한다** — 낡은 조각을 구분하는 기준이라 0 으로 되돌리면
        /// 지난 회차 조각을 이번 것으로 착각할 수 있다.
        ///
        /// 🔴 Host 의 package(_hostChunks 등)는 여기서 비우지 않는다.
        ///    응답이 늦게 오거나 재전송을 해야 할 수 있기 때문이다. package 는 회차가
        ///    **결말을 맺을 때**(성공·실패 판정) 비운다 — <see cref="ClearHostPackage"/>.
        /// </summary>
        private void ResetSession()
        {
            _assembler = null;
            _clientExpectedHash = null;
            _clientExpectedChunkCount = 0;
            _clientHeaderMapVersion = 0;
            _clientIsProbe = false;
            _clientReceiveCount = 0;
        }

        /// <summary>
        /// [Host] 회차가 결말을 맺은 뒤 package 를 비운다.
        /// ⚠️ _hostPrepared 는 **비우지 않는다** — 성공한 회차의 확정 맵은 I 단계의 씬 전환
        ///    게이트가 읽어야 하고, 실패한 회차의 값도 로그·진단에 쓰이기 때문이다.
        ///    (인계는 MapHandoff 가 맡는다. 여기 남는 것은 참조 하나뿐이다.)
        /// </summary>
        private void ClearHostPackage()
        {
            _hostChunks = null;
        }

        // ====================================================================
        // [Host] 맵 준비 + 전송 시작 — 🔴 3단계 F
        // ====================================================================

        /// <summary>
        /// 🔴 **Host 권위 맵 준비·전송의 유일한 진입점.** 로비에서 부른다.
        ///
        /// 하는 일 순서:
        ///   ① <see cref="MapPreparationUseCase.Prepare"/> 로 이번 판의 맵을 만든다(Host 만 만든다).
        ///   ② 규칙 12 의 맵 준비 로그를 한 줄 낸다(성공 / 폴백 / 실패 중 정확히 하나).
        ///   ③ 결과에 **이미 들어 있는** CanonicalBytes · Hash 를 그대로 package 로 삼는다.
        ///      🔴 여기서 바이트를 새로 만들거나 해시를 새로 계산하지 않는다 — 두 번 계산하면
        ///         "어느 쪽이 진짜인가"라는 문제가 생기고, 그 순간 해시 대조가 의미를 잃는다.
        ///   ④ 시작 통보 → 조각 → 응답 대기(10초) 로 들어간다.
        ///
        /// ⚠️ <b>유일한 호출부는 <c>NetworkGameManager.BeginMapTransferAndLoadGameScene()</c> 다</b>
        ///    (3단계 I 에서 배선됨). 그쪽이 이 객체를 로비에서 동적 스폰하고, 이 메서드를 부르고,
        ///    아래 결말 이벤트 두 개를 구독해 씬 전환 여부를 결정한다.
        ///
        /// 🔴 <b>미해결 판단 하나를 여기 남긴다(계획서 §9-마).</b>
        ///    이 메서드는 <see cref="MapPreparationUseCase"/> 와 그 의존
        ///    <see cref="ResourcesMapFallbackTemplateSource"/> 를 **스스로 조립한다.**
        ///    로비 씬에는 GameBootstrapper(이 프로젝트의 유일한 의존성 조합 루트)가 없기 때문이다.
        ///    ⚠️ "조합 루트는 하나"라는 제약과 마찰이 있으며, <b>예외로 둘지 / 전송 객체가
        ///       로비 한정 조합 루트를 겸하는 것으로 볼지는 아직 사용자 확인 전이다.</b>
        ///       확인 결과에 따라 이 두 줄이 "밖에서 주입받는" 형태로 바뀔 수 있다.
        /// </summary>
        /// <param name="rootSeed">이 경기의 64비트 root seed. Host 가 뽑아서 넘긴다</param>
        /// <param name="mapTestModeEnabled">맵 테스트 모드 여부(GameConfig.MapTestModeEnabled)</param>
        /// <returns>전송을 시작했으면 true. 맵 준비 실패 등으로 시작하지 못했으면 false</returns>
        public bool BeginHostMapTransfer(ulong rootSeed, bool mapTestModeEnabled)
        {
            if (!IsSpawned || !IsServer)
            {
                return false;
            }

            // ── ① 맵 준비 (Host 가 유일한 권위자 — 규칙 12) ────────────────
            var preparation = new MapPreparationUseCase(new ResourcesMapFallbackTemplateSource());
            MapPreparationResult prepared = preparation.Prepare(rootSeed, mapTestModeEnabled);

            _hostPrepared = prepared;

            // ── ② 규칙 12 「로그 필수 항목」 ────────────────────────────────
            // 🔴 필드 집합·순서는 Bootstrap/GameBootstrapper.Map.cs 의 BuildMapPreparationLogData
            //    와 **반드시 같아야 한다.** 같은 키(MapPreparationSucceeded 등)를 쓰는
            //    같은 지표이므로, 한쪽만 필드를 바꾸면 집계가 조용히 갈라진다.
            //    (두 자리가 생긴 이유: 싱글은 전투 씬에서, 멀티 Host 는 로비에서 준비한다.)
            string preparationData = BuildMapPreparationLogData(prepared);

            if (!prepared.IsSucceeded)
            {
                // [운영/Error] 맵을 못 만들었다 = 경기가 성립하지 않는다.
                //   ⚠️ 실패 사유는 자유 문장이라 message 쪽에 넣는다(쉼표가 들어 있어
                //      key=value 구분자와 충돌한다 — LogRules 1.4).
                GameLog.Ops.Error(LogEvent.MapPreparationFailed, MapLogSystem, nameof(NetworkMapTransfer),
                                  "Host 맵 준비 실패 — 전송을 시작하지 않는다: " +
                                  (prepared.FailureReason ?? "사유 없음"), preparationData);
                return false;
            }

            if (prepared.UsedFallback)
            {
                // [운영/Warn] 100회가 모두 거부되어 폴백 템플릿으로 경기를 연다. 경기는 진행된다.
                GameLog.Ops.Warn(LogEvent.MapPreparationUsedFallbackTemplate, MapLogSystem,
                                 nameof(NetworkMapTransfer),
                                 "Host 맵 생성 시도가 모두 거부되어 폴백 템플릿을 사용했다", preparationData);
            }
            else
            {
                // [운영/Info] 정상 경로로 맵이 확정됐다. 위 두 키의 발생률을 계산할 때의 분모다.
                GameLog.Ops.Info(LogEvent.MapPreparationSucceeded, MapLogSystem,
                                 nameof(NetworkMapTransfer), "Host 맵 준비 완료", preparationData);
            }

            // ── ③④ package 로 삼아 전송 시작 ───────────────────────────────
            return StartHostRound(prepared.CanonicalBytes, prepared.Hash, prepared.MapVersion,
                                  ProvisionalChunkSizeBytes, false);
        }

        /// <summary>
        /// [Host] 회차 하나를 시작한다 — nonce 를 올리고 조각을 잘라 첫 전송을 내보낸다.
        /// 진짜 맵과 한도 실측 프로브가 **같은 길**을 쓴다(프로브 전용 경로를 따로 만들면
        /// 정작 실전에서 쓰는 경로를 잰 것이 아니게 된다).
        /// </summary>
        /// <param name="payload">보낼 바이트(진짜 맵의 canonical 바이트 또는 프로브 더미)</param>
        /// <param name="hash">payload 의 SHA-256 원본 32바이트</param>
        /// <param name="mapVersion">package 헤더에 실을 canonical 형식 버전</param>
        /// <param name="chunkSize">조각 하나의 최대 크기</param>
        /// <param name="isProbe">한도 실측용 프로브인가(진짜 맵이 아니면 true)</param>
        /// <returns>전송을 시작했으면 true</returns>
        private bool StartHostRound(byte[] payload, byte[] hash, int mapVersion, int chunkSize,
                                    bool isProbe)
        {
            if (payload == null || payload.Length < 1 || chunkSize < 1 || hash == null)
            {
                // 축 A = Warn(요청만 거부하고 객체는 계속 산다) · 축 B = 개발(호출부 실수).
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "전송 시작 인자가 올바르지 않아 회차를 시작하지 않았다",
                                 $"PayloadBytes={(payload == null ? -1 : payload.Length)}, " +
                                 $"ChunkSize={chunkSize}, HashBytes={(hash == null ? -1 : hash.Length)}");
                return false;
            }

            if (payload.Length > ProvisionalMaxPackageBytes)
            {
                // 규칙 16 「용량 한도 초과 = 즉시 실패」. 재전송하지 않는다.
                _activeNonce++;
                _outcomeLogged = false;
                _hostOutcomeNotified = false;
                // 🔴 3단계 I 에서 추가: 이 분기에도 _hostIsProbe 를 반드시 갱신한다.
                //    이 분기는 회차를 "시작하자마자 실패로 끝내는" 길인데, 여기서 갱신하지 않으면
                //    **지난 회차의 값이 그대로 남는다.** 직전이 실측 프로브였다면 진짜 맵의 실패가
                //    프로브로 오인되어 씬 전환 게이트에 통보가 가지 않고, 로비가 로딩 화면인 채
                //    영영 멈춘다(게이트는 성공·실패 둘 중 하나의 통보를 반드시 받아야 한다).
                _hostIsProbe = isProbe;
                _hostTotalBytes = payload.Length;
                _hostHash = hash;
                _hostMapVersion = mapVersion;
                _hostSendCount = 0;
                _hostResendCount = 0;
                FailHostRound(MapTransferErrorCode.PackageTooLarge, LogEvent.MapTransferFailed,
                              "보낼 맵이 용량 한도를 넘었다 — 즉시 실패(재전송 없음)", null);
                return false;
            }

            _activeNonce++;
            _outcomeLogged = false;
            _hostOutcomeNotified = false;
            _hostIsProbe = isProbe;
            _hostChunkSize = chunkSize;
            _hostTotalBytes = payload.Length;
            _hostHash = hash;
            _hostMapVersion = mapVersion;
            _hostChunks = MapChunkAssembler.Split(payload, chunkSize);
            _hostSendCount = 0;
            _hostResendCount = 0;

            ResetSession();

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "맵 전송 회차 시작",
                             $"Nonce={_activeNonce}, IsProbe={isProbe}, MapVersion={mapVersion}, " +
                             $"TotalBytes={_hostTotalBytes}, ChunkSize={chunkSize}, " +
                             $"ChunkCount={_hostChunks.Length}, Hash={ToHashField(hash)}");

            SendHostPackage("최초 전송");
            return true;
        }

        /// <summary>
        /// [Host] 시작 통보 + 조각 전부를 한 번 내보내고 응답 마감 시각을 다시 건다.
        /// 최초 전송과 재전송이 **완전히 같은 코드**를 쓴다 — 규칙 16 이 "같은 nonce·같은 package 를
        /// 1회 재전송" 이라고 정했으므로, 다시 만들지 않고 <b>들고 있던 것을 그대로</b> 보낸다.
        /// </summary>
        /// <param name="reason">상태 전이 로그에 남길 사람이 읽는 사유</param>
        private void SendHostPackage(string reason)
        {
            _hostSendCount++;

            SetState(MapTransferState.Sending, reason);

            MapPrepareBeginClientRpc(_activeNonce, _hostMapVersion, _hostTotalBytes,
                                     _hostChunkSize, _hostChunks.Length, _hostHash, _hostIsProbe);

            for (int i = 0; i < _hostChunks.Length; i++)
            {
                MapChunkClientRpc(_activeNonce, i, _hostChunks[i]);
            }

            // 규칙 16: Host 는 활성 준비 작업의 응답을 10초 기다린다.
            _hostResponseDeadline = Time.realtimeSinceStartup + TransferTimeoutSeconds;

            SetState(MapTransferState.WaitingReady, "조각을 다 보내고 응답 대기");
        }

        // ====================================================================
        // [Host] timeout · 재전송 · 연결 끊김 — 🔴 3단계 F
        // ====================================================================

        /// <summary>
        /// [Host] 10초 안에 응답이 오지 않았다.
        ///
        /// 규칙 16 의 규정 그대로다 — <b>timeout 또는 조각 불완전 수신일 때만</b>
        /// 같은 nonce·같은 package 를 <b>1회</b> 재전송하고, 두 번째 10초에도 실패하면 끝낸다.
        /// </summary>
        private void HandleHostTimeout()
        {
            if (_hostResendCount < MaxResendCount && _hostChunks != null)
            {
                _hostResendCount++;

                // [운영/Warn] 🔴 이 키만 **결말이 아니다.** 복구 경로가 아직 남아 있는
                //   중간 상태 전이이므로, 한 회차에 「재전송 1줄 + 결말 1줄」 최대 2줄이 남는다
                //   (LogRules 1.5 의 3단계 E 블록 「4 + 1 구조」). 금지 9 위반이 아니다.
                GameLog.Ops.Warn(LogEvent.MapTransferRetried, MapLogSystem, nameof(NetworkMapTransfer),
                                 "응답이 10초 안에 오지 않아 같은 package 를 1회 재전송한다",
                                 BuildTransferLogData(_hostHash, null, MapTransferErrorCode.ResponseTimeout,
                                                      null));

                SendHostPackage("timeout — 1회 재전송");
                return;
            }

            FailHostRound(MapTransferErrorCode.ResponseTimeout, LogEvent.MapTransferFailed,
                          "재전송 뒤에도 응답이 오지 않아 전송을 실패로 끝낸다", null);
        }

        /// <summary>
        /// [Host] 상대 연결이 끊겼다. 규칙 16 은 <b>연결 끊김을 즉시 실패</b>로 정하며
        /// 재전송 대상에서 명시적으로 제외한다(보낼 상대가 없으므로 다시 보내 봐야 의미가 없다).
        /// </summary>
        /// <param name="clientId">끊긴 클라이언트 id</param>
        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsSpawned || !IsServer) return;

            // 전송 중이 아닐 때의 끊김은 이 클래스가 관여할 일이 아니다(정상 종료 흐름).
            // 🔴 가드에 로그를 넣지 않는다 — 상태 전이 지점이 아니다(LogRules 1.14 금지 8).
            if (_state != MapTransferState.Sending && _state != MapTransferState.WaitingReady) return;

            FailHostRound(MapTransferErrorCode.Disconnected, LogEvent.MapTransferFailed,
                          "전송 중 상대 연결이 끊겨 즉시 실패로 끝낸다(재전송 없음)", clientId);
        }

        /// <summary>
        /// [Host] 이번 회차를 실패로 끝낸다. 결말 로그를 <b>정확히 한 줄</b> 남긴다.
        /// </summary>
        /// <param name="code">내부 error code</param>
        /// <param name="outcomeKey">결말 키(MapTransferFailed / MapHashMismatch / MapClientVerificationFailed)</param>
        /// <param name="message">사람이 읽는 결말 설명</param>
        /// <param name="clientId">관련 클라이언트 id(없으면 null)</param>
        private void FailHostRound(MapTransferErrorCode code, LogEvent outcomeKey, string message,
                                   ulong? clientId)
        {
            SetState(MapTransferState.Failed, "전송 실패");

            LogOutcomeOnce(outcomeKey, true, message,
                           BuildTransferLogData(_hostHash, null, code,
                                                clientId.HasValue ? "ClientId=" + clientId.Value : null));

            ClearHostPackage();

            NotifyHostOutcomeOnce(false, code);
        }

        /// <summary>
        /// [Host] 이번 회차의 결말을 씬 전환 게이트에 <b>정확히 한 번</b> 통보한다(3단계 I).
        ///
        /// 🔴 게이트는 "성공 통보를 받으면 씬을 로드하고, 실패 통보를 받으면 로비에 남는다"로
        ///    동작한다. 그러므로 이 함수는 두 가지를 모두 지켜야 한다.
        ///      ① <b>한 번만</b> 불려야 한다 — 실패 통보 뒤에 성공 통보가 따라오면
        ///         실패한 판인데 전투 씬으로 넘어간다(규칙 16 정면 위반).
        ///      ② <b>반드시 한 번은</b> 불려야 한다 — 아무 통보도 가지 않으면 로비가
        ///         로딩 화면인 채 영영 멈춘다. 그래서 Host 쪽 결말 자리 네 곳
        ///         (FailHostRound · 응답=실패 · 응답=해시 불일치 · 응답=성공)에서 모두 부른다.
        ///
        /// ⚠️ 프로브 회차는 통보하지 않는다 — 한도 실측은 게임 흐름이 아니다.
        ///    (프로브 도중 게이트가 열려 로비에서 전투 씬으로 넘어가 버리면 실측이 끊긴다.)
        /// </summary>
        /// <param name="success">해시까지 일치해 이번 판의 맵이 확정됐는가</param>
        /// <param name="code">실패 사유(성공이면 None). 로비 쪽 진단 로그에 그대로 실린다</param>
        private void NotifyHostOutcomeOnce(bool success, MapTransferErrorCode code)
        {
            if (_hostIsProbe) return;
            if (_hostOutcomeNotified) return;

            _hostOutcomeNotified = true;

            if (success)
            {
                OnHostTransferSucceeded?.Invoke();
                return;
            }

            OnHostTransferFailed?.Invoke(code);
        }

        // ====================================================================
        // [Host] 전송 한도 실측 프로브 (3단계 B)
        // ====================================================================

        /// <summary>
        /// 🔴 **전송 한도 실측용 프로브.** 진짜 맵이 아니라 **더미 바이트**를 보낸다.
        ///
        /// 이 메서드가 존재하는 이유는 하나다 — 위 IsChunkSizeMeasured 가 말한 두 숫자
        /// (NGO RPC 실효 페이로드 상한 · Relay 실효 MTU)는 **실제로 보내 봐야만** 알 수 있고,
        /// 그건 사용자 실기 2대에서만 가능하기 때문이다.
        ///
        /// 쓰는 법(사용자):
        ///   1) Host·Client 를 연결한다(로컬이면 NGO 실효 상한, Relay 면 Relay MTU 를 재는 것이다).
        ///   2) 하이러키에서 스폰된 이 객체를 고르고 인스펙터 컨텍스트 메뉴 항목을 누른다.
        ///   3) 로그에서 어느 크기까지 MapReady 가 돌아오는지 본다. 돌아오지 않는 첫 크기가 상한이다.
        ///
        /// ⚠️ 실전 경로와 **같은 RPC** 를 쓴다. 일부러 그렇게 했다 — 프로브 전용 RPC 를 따로 만들면
        ///    정작 실전에서 쓰는 경로의 한도를 잰 것이 아니게 된다.
        ///
        /// 🔴 다만 package 헤더에 <c>isProbe=true</c> 를 실어 **받는 쪽이 역직렬화·검증까지는
        ///    가지 않게** 한다. 더미 바이트는 맵이 아니므로 Decode 가 반드시 실패하는데,
        ///    그대로 두면 프로브를 한 번 돌릴 때마다 <c>MapClientVerificationFailed</c>(운영/Error)
        ///    가 한 줄씩 쌓여 **장애 지표가 실측 작업으로 오염된다.**
        ///    ⚠️ 이것은 "조각 여러 개를 억지로 만들려고 조각 크기를 줄이는 시험 전용 경로"와
        ///       다르다(계획서 §9-나 가 금지한 그것). 여기서 건너뛰는 것은 <b>검증 단계뿐</b>이고
        ///       재고 싶은 대상인 <b>전송 경로는 실전과 완전히 같다.</b>
        /// </summary>
        /// <param name="probeBytes">보낼 더미 데이터의 전체 바이트 수(1 이상).</param>
        /// <param name="chunkSize">조각 하나의 최대 크기(1 이상). 기본은 위 잠정값.</param>
        public void RunTransferProbe(int probeBytes, int chunkSize)
        {
            // IsSpawned 를 **앞에** 두는 이유: 단락 평가로, 스폰되지 않은 상태(싱글플레이 등)에서는
            // IsServer 를 아예 건드리지 않게 된다. NetworkManager 가 멈춘 뒤에도 IsServer 가
            // 참일 수 있어서 RPC 발신이 예외로 터지는 사고가 이 프로젝트에 실제로 있었다.
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            if (probeBytes < 1 || chunkSize < 1)
            {
                // 축 A = Warn(요청만 거부하고 객체는 계속 산다) · 축 B = 개발(호출부 실수라 에디터에서 드러난다).
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "전송 프로브 인자가 올바르지 않아 요청을 무시했다",
                                 $"ProbeBytes={probeBytes}, ChunkSize={chunkSize}");
                return;
            }

            // 더미 데이터. 0 이 아니라 규칙적인 값을 넣는 이유는, 받는 쪽에서 "버퍼가 안 채워진 것"과
            // "0 이 들어온 것"을 눈으로 구분할 수 있게 하기 위해서다.
            byte[] probe = new byte[probeBytes];
            for (int i = 0; i < probe.Length; i++)
            {
                probe[i] = (byte)((i % 251) + 1);
            }

            // 프로브도 해시를 제대로 실어 보낸다 — 받는 쪽의 해시 대조까지가 "전송이 제대로 됐는가"의
            // 판정이기 때문이다. 여기서 건너뛰는 것은 그 뒤의 역직렬화·공정성 검증뿐이다.
            byte[] probeHash = MapDefinitionCodec.ComputeHash(probe);

            StartHostRound(probe, probeHash, MapDefinition.CurrentMapVersion, chunkSize, true);
        }

        /// <summary>
        /// 인스펙터 컨텍스트 메뉴에서 한 번에 실행하기 위한 편의 진입점.
        /// 잠정 조각 크기로 잠정 조각 크기만큼(= 조각 1개) 보낸다.
        /// 한도를 찾을 때는 위 <see cref="RunTransferProbe"/> 를 크기를 키워 가며 부른다.
        /// </summary>
        [ContextMenu("전송 프로브 1회 실행 (잠정 조각 크기)")]
        private void RunTransferProbeWithProvisionalSize()
        {
            RunTransferProbe(ProvisionalChunkSizeBytes, ProvisionalChunkSizeBytes);
        }

        // ====================================================================
        // RPC ① 시작 통보 (Host → Client)
        // ====================================================================

        /// <summary>
        /// [Host → Client] package 헤더. "이제부터 전체 totalBytes 바이트를 chunkSize 단위로
        /// chunkCount 조각에 나눠 보낸다. 다 합치면 해시는 이 값이다" 라는 예고다.
        /// 받는 쪽은 이 값으로 재조립기를 만들고, 나중에 대조할 기대 해시를 보관한다.
        ///
        /// 🔴 <b>헤더의 mapVersion 과 본문(canonical) 의 MapVersion 은 반드시 같아야 한다.</b>
        ///    TDD 가 그렇게 규정한다. 값이 두 군데가 되었으므로 「불일치 시 실패」 판정이
        ///    새로 필요하며, 그 판정은 조각이 다 모인 뒤 아래 <see cref="TryVerifyReceivedPackage"/>
        ///    에서 한다(본문을 해석해 봐야 비교할 수 있기 때문이다).
        ///
        /// ⚠️ 메서드 이름이 반드시 ClientRpc 로 끝나야 한다. NGO 의 규약이고, 어기면 컴파일되지 않는다.
        /// ⚠️ 맨 앞의 서버 되돌림은 **가드**다. Host 는 서버이자 클라이언트라 자기가 보낸 ClientRpc 를
        ///    자기도 받는데, 그대로 두면 Host 가 자기 자신의 조각을 모으고 로그도 같은 사건에 두 줄이 된다
        ///    (LogRules 1.14 금지 9). 가드 자체에는 로그를 넣지 않는다 — 가드에 걸리는 것은 정상 흐름이라
        ///    상태 전이 지점이 아니고, 매번 남기면 금지 8(스팸)에 걸린다.
        /// </summary>
        /// <param name="nonce">전송 회차 번호.</param>
        /// <param name="mapVersion">canonical 형식 버전(헤더 값).</param>
        /// <param name="totalBytes">전부 합쳤을 때의 바이트 수.</param>
        /// <param name="chunkSize">조각 하나의 최대 크기.</param>
        /// <param name="chunkCount">보낼 조각 수(헤더 값 — 받는 쪽이 자기 계산과 대조한다).</param>
        /// <param name="expectedHash">payload 전체의 SHA-256 원본 32바이트.</param>
        /// <param name="isProbe">한도 실측용 프로브인가. true 면 받는 쪽이 해시 대조까지만 한다.</param>
        [ClientRpc]
        private void MapPrepareBeginClientRpc(ulong nonce, int mapVersion, int totalBytes,
                                              int chunkSize, int chunkCount, byte[] expectedHash,
                                              bool isProbe)
        {
            if (IsServer)
            {
                return;
            }

            // ── 헤더 검사 ① 크기·조각 수 ───────────────────────────────────
            // 규칙 16 의 「용량 한도 초과 = 즉시 실패」와, 손상된 헤더가 말도 안 되는 크기를
            // 선언해 메모리를 통째로 잡아먹는 것을 여기서 함께 막는다.
            if (totalBytes < 1 || chunkSize < 1 || totalBytes > ProvisionalMaxPackageBytes)
            {
                FailClientRound(nonce, MapTransferErrorCode.InvalidHeader, LogEvent.MapTransferFailed,
                                "시작 통보의 크기 값이 올바르지 않아 회차를 시작하지 않았다",
                                expectedHash, null,
                                $"TotalBytes={totalBytes}, ChunkSize={chunkSize}, " +
                                $"MaxPackageBytes={ProvisionalMaxPackageBytes}");
                return;
            }

            // 헤더가 말한 조각 수와 "크기로 계산한" 조각 수가 다르면 보내는 쪽과 받는 쪽의
            // 계약이 이미 어긋난 것이다. 조각을 모으기 전에 여기서 끊는다.
            int computedChunkCount = MapChunkAssembler.CalculateChunkCount(totalBytes, chunkSize);
            if (chunkCount != computedChunkCount)
            {
                FailClientRound(nonce, MapTransferErrorCode.InvalidHeader, LogEvent.MapTransferFailed,
                                "시작 통보의 조각 수가 크기 계산과 맞지 않아 회차를 시작하지 않았다",
                                expectedHash, null,
                                $"HeaderChunkCount={chunkCount}, ComputedChunkCount={computedChunkCount}");
                return;
            }

            // ── 헤더 검사 ② 해시 길이 ──────────────────────────────────────
            // 대조는 원본 32바이트로 한다. 길이가 다르면 대조 자체가 성립하지 않는다.
            if (expectedHash == null || expectedHash.Length != MapDefinitionCodecHashLength)
            {
                FailClientRound(nonce, MapTransferErrorCode.InvalidHeader, LogEvent.MapTransferFailed,
                                "시작 통보의 기대 해시 길이가 올바르지 않아 회차를 시작하지 않았다",
                                expectedHash, null,
                                $"HashBytes={(expectedHash == null ? -1 : expectedHash.Length)}");
                return;
            }

            // ── 헤더 검사 ③ 지원하는 형식 버전인가 (규칙 16 — 즉시 실패) ───
            if (mapVersion != MapDefinition.CurrentMapVersion)
            {
                FailClientRound(nonce, MapTransferErrorCode.UnsupportedMapVersion, LogEvent.MapTransferFailed,
                                "지원하지 않는 canonical 형식 버전이라 회차를 시작하지 않았다",
                                expectedHash, null,
                                $"HeaderMapVersion={mapVersion}, SupportedMapVersion={MapDefinition.CurrentMapVersion}");
                return;
            }

            // ── 회차 시작 ──────────────────────────────────────────────────
            // 같은 nonce 로 다시 왔다면 그것은 Host 의 재전송이다. 재조립기를 새로 만들어
            // **처음부터 다시 모은다** — 절반만 모인 지난 버퍼에 새 조각을 섞으면
            // 규칙 16 의 "부분 데이터 금지"를 어기는 가장 조용한 방법이 된다.
            bool isResend = (nonce == _activeNonce) && (_clientReceiveCount > 0);

            if (!isResend)
            {
                _clientReceiveCount = 0;
                _outcomeLogged = false;
            }

            _activeNonce = nonce;
            _clientReceiveCount++;
            _clientExpectedHash = expectedHash;
            _clientHeaderMapVersion = mapVersion;
            _clientExpectedChunkCount = chunkCount;
            _clientIsProbe = isProbe;
            _assembler = new MapChunkAssembler(totalBytes, chunkSize);

            SetState(MapTransferState.Receiving, isResend ? "시작 통보 재수신(재전송)" : "시작 통보 수신");

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "시작 통보 수신 — 재조립기를 준비했다",
                             $"Nonce={nonce}, MapVersion={mapVersion}, TotalBytes={totalBytes}, " +
                             $"ChunkSize={chunkSize}, ChunkCount={_assembler.ChunkCount}, " +
                             $"IsProbe={isProbe}, ReceiveCount={_clientReceiveCount}, " +
                             $"Hash={ToHashField(expectedHash)}");
        }

        // ====================================================================
        // RPC ② 조각 (Host → Client)
        // ====================================================================

        /// <summary>
        /// [Host → Client] 조각 하나. 도착 순서는 보장되지 않아도 되고, 같은 조각이 두 번 와도 된다.
        /// 그 처리는 전부 <see cref="MapChunkAssembler"/> 가 맡는다(중복 무시·index 자리 채우기·길이 검사).
        ///
        /// 🔴 완성 판정은 "조각 수"만이 아니라 **바이트 합계가 선언된 크기와 같은가**까지 본다.
        ///    규칙 16 의 "모든 조각이 모여 선언된 크기와 일치한 뒤에야"를 그대로 옮긴 것이며,
        ///    그 판정은 재조립기 안에 들어 있어 이 자리에서 실수로 건너뛸 수 없다.
        /// </summary>
        /// <param name="nonce">전송 회차 번호. 지금 회차와 다르면 낡은 조각이므로 버린다.</param>
        /// <param name="index">조각 번호.</param>
        /// <param name="data">조각 내용.</param>
        [ClientRpc]
        private void MapChunkClientRpc(ulong nonce, int index, byte[] data)
        {
            if (IsServer)
            {
                return;
            }

            if (_assembler == null || nonce != _activeNonce)
            {
                // 낡은 회차의 조각이거나 시작 통보를 못 받은 상태다.
                // 축 A = Warn(이 조각만 버리고 계속 간다) · 축 B = 개발
                // (회차 번호가 어긋나는 것은 에디터 2인 구성에서도 그대로 재현된다).
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "지금 회차의 조각이 아니어서 버렸다",
                                 $"Nonce={nonce}, ActiveNonce={_activeNonce}, ChunkIndex={index}");
                return;
            }

            MapChunkAcceptResult result = _assembler.Accept(index, data);

            if (result != MapChunkAcceptResult.Accepted)
            {
                // 중복은 오류가 아니지만(신뢰성 전송이라도 재전송이 겹치면 생긴다),
                // 길이 불일치·범위 밖은 보내는 쪽과 받는 쪽의 계약이 어긋났다는 신호다.
                // 둘 다 이 조각만 버리고 계속 가므로 축 A = Warn, 축 B = 개발이다.
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "조각을 받아들이지 않았다",
                                 $"Nonce={nonce}, ChunkIndex={index}, Result={result}, " +
                                 $"ReceivedChunkCount={_assembler.ReceivedChunkCount}, " +
                                 $"ChunkCount={_assembler.ChunkCount}");
                return;
            }

            if (!_assembler.IsComplete)
            {
                // 🔴 아직 다 모이지 않았다. 여기서는 **아무것도 꺼내지 않는다.**
                //    조각마다 로그를 남기지도 않는다 — 조각이 많아지면 그게 곧 스팸이다(금지 8).
                //    조각이 끝내 다 안 모이면 Host 쪽 10초 timeout 이 재전송을 걸어 준다
                //    (규칙 16 의 "조각 불완전 수신"이 재전송 사유인 두 경우 중 하나다).
                return;
            }

            byte[] payload;
            if (!_assembler.TryGetAssembled(out payload))
            {
                // 바로 위에서 완성을 확인했으므로 여기 도달하면 재조립기 자체의 버그다.
                FailClientRound(nonce, MapTransferErrorCode.AssembleFailed, LogEvent.MapTransferFailed,
                                "완성으로 판정됐는데 재조립 결과를 꺼내지 못했다",
                                _clientExpectedHash, null,
                                $"TotalBytes={_assembler.TotalBytes}, " +
                                $"ReceivedBytes={_assembler.ReceivedByteCount}");
                return;
            }

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "조각 재조립 완료 — 검증을 시작한다",
                             $"Nonce={nonce}, TotalBytes={payload.Length}, " +
                             $"ChunkCount={_assembler.ChunkCount}");

            VerifyAndAnswer(nonce, payload);
        }

        // ====================================================================
        // [Client] 검증 파이프라인 — 🔴 3단계 G · H
        // ====================================================================

        /// <summary>
        /// [Client] 조각이 다 모인 뒤의 검증 전 과정을 순서대로 태우고 결과를 Host 에게 답한다.
        ///
        /// 🔴 <b>순서를 바꾸지 말 것.</b> 규칙 16 이 *"모든 조각이 모여 선언된 크기와 일치한 뒤에야
        ///    해시 검증 → 역직렬화 → 공정성 검증을 순서대로 수행한다"* 고 정한다. 순서가 뒤집히면
        ///    예를 들어 손상된 바이트를 먼저 해석하려다 엉뚱한 예외가 나고, 정작 "해시가 달랐다"는
        ///    진짜 원인이 로그에서 사라진다.
        /// </summary>
        /// <param name="nonce">이번 회차 번호</param>
        /// <param name="payload">재조립이 끝난 canonical 바이트</param>
        private void VerifyAndAnswer(ulong nonce, byte[] payload)
        {
            // ── ① 해시 대조 (G) ────────────────────────────────────────────
            // 🔴 대조는 **원본 32바이트**로 한다. 로그에 싣는 16자 문자열로 비교하면
            //    앞 8바이트만 같아도 통과해 버린다(2단계 K 가 정한 규약).
            byte[] clientHash = MapDefinitionCodec.ComputeHash(payload);

            if (!MapDefinitionCodec.HashEquals(clientHash, _clientExpectedHash))
            {
                // 규칙 16: 해시 불일치는 **즉시 실패**다. 같은 데이터를 다시 받아 봐야 결과가 같다.
                FailClientRound(nonce, MapTransferErrorCode.HashMismatch, LogEvent.MapHashMismatch,
                                "받은 맵의 해시가 Host 가 알려 준 해시와 다르다 — 즉시 실패(재전송 없음)",
                                _clientExpectedHash, clientHash, null);
                return;
            }

            // ── 프로브는 여기까지다 ────────────────────────────────────────
            // 더미 바이트는 맵이 아니므로 해석할 수 없다. 실측에 필요한 것은 "여기까지 왔는가"이고,
            // 그 답은 이미 나왔다. (자세한 이유는 RunTransferProbe 주석 참조.)
            if (_clientIsProbe)
            {
                SetState(MapTransferState.Completed, "프로브 수신·해시 대조 완료");

                GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "전송 프로브 수신 완료 — 해시까지 일치한다",
                                 $"Nonce={nonce}, TotalBytes={payload.Length}, " +
                                 $"ChunkCount={_clientExpectedChunkCount}");

                MapReadyServerRpc(nonce, true, payload.Length, clientHash,
                                  (int)MapTransferErrorCode.None);
                ResetSession();
                return;
            }

            // ── ② 역직렬화 + ③ 헤더/본문 형식 버전 대조 + ④ D 방식 검증 (G · H) ──
            MapVerificationResult verification;
            MapTransferErrorCode failureCode;
            string failureExtra;

            if (!TryVerifyReceivedPackage(payload, out verification, out failureCode, out failureExtra))
            {
                LogEvent outcomeKey = (failureCode == MapTransferErrorCode.MapVersionHeaderMismatch)
                    ? LogEvent.MapTransferFailed
                    : LogEvent.MapClientVerificationFailed;

                FailClientRound(nonce, failureCode, outcomeKey,
                                "받은 맵을 쓸 수 없다 — 즉시 실패(재전송 없음): " +
                                (verification == null ? "(해석 실패)" : verification.FailureReason ?? "사유 없음"),
                                _clientExpectedHash, clientHash, failureExtra);
                return;
            }

            // ── ⑤ 전부 통과 ───────────────────────────────────────────────
            // 확정된 맵을 인계 홀더에 심는다. 전투 씬의 GameBootstrapper 가 꺼내 격자에 새긴다.
            // ⚠️ **꺼내 쓰는 쪽(TryTake → ProjectMap)은 아직 배선하지 않았다(I 단계).**
            //    지금은 심어 두기만 하므로 게임 동작이 바뀌지 않는다.
            MapHandoff.Set(BuildClientPreparedMap(verification, payload, clientHash));

            SetState(MapTransferState.Completed, "수신·검증 완료");

            // [운영/Info] 이 판의 맵 전송이 정상으로 끝났다. 위 실패 3종의 **분모**가 된다.
            LogOutcomeOnce(LogEvent.MapTransferSucceeded, false,
                           "받은 맵의 해시·복원·공정성 검증을 모두 통과했다",
                           BuildTransferLogData(_clientExpectedHash, clientHash,
                                                MapTransferErrorCode.None,
                                                BuildVerificationFields(verification)));

            MapReadyServerRpc(nonce, true, payload.Length, clientHash, (int)MapTransferErrorCode.None);

            ResetSession();
        }

        /// <summary>
        /// [Client] 역직렬화 → 헤더/본문 형식 버전 대조 → 「D 방식」 재생성 검증을 한다.
        ///
        /// 🔴 <b>헤더/본문 형식 버전 이중화</b>: TDD 는 package 헤더의 <c>mapVersion</c> 이
        ///    canonical <c>MapDefinition.MapVersion</c> 과 <b>일치해야 한다</b>고 정한다.
        ///    값이 두 군데가 되었으므로 「불일치 시 실패」 판정이 필요하다.
        ///    ⚠️ 지금은 <c>Decode</c> 가 미지원 버전을 이미 걸러내고 시작 통보에서도 헤더 버전을
        ///       확인하므로 이 판정에 걸릴 길이 사실상 없다. 그래도 남겨 두는 이유는,
        ///       지원 버전이 둘 이상이 되는 날 이 줄이 없으면 <b>헤더는 v1 이라 해 놓고 본문은 v2</b>
        ///       인 package 가 조용히 통과하기 때문이다. "어차피 안 걸리니까"라며 지우지 말 것.
        /// </summary>
        /// <param name="payload">재조립이 끝난 canonical 바이트(해시 대조를 이미 통과한 것)</param>
        /// <param name="verification">D 방식 검증 결과(해석 자체에 실패하면 null)</param>
        /// <param name="failureCode">실패 시 내부 error code</param>
        /// <param name="failureExtra">실패 시 로그에 덧붙일 key=value 조각(없으면 null)</param>
        /// <returns>전부 통과하면 true</returns>
        private bool TryVerifyReceivedPackage(byte[] payload, out MapVerificationResult verification,
                                              out MapTransferErrorCode failureCode,
                                              out string failureExtra)
        {
            failureExtra = null;

            // 🔴 D 방식 검증기가 Decode 부터 전부 한다. 여기서 따로 Decode 하지 않는 이유는,
            //    두 번 해석하면 "검증한 정의"와 "실제로 쓰는 정의"가 다른 객체가 되어
            //    둘이 어긋날 수 있는 길이 생기기 때문이다.
            verification = MapVerificationUseCase.Verify(payload);

            if (!verification.IsSucceeded)
            {
                // 세 갈래(역직렬화 실패 / 재생성 불일치 / 공정성 검증 실패)를 **키로 쪼개지 않는다.**
                // 전부 「받은 맵을 쓸 수 없어 경기가 성립하지 않았다」는 한 사건이고,
                // 쪼개면 그 장애 지표 자체가 만들어지지 않는다(LogRules 1.5 의 3단계 E 블록).
                // 어느 갈래였는지는 아래 BuildVerificationFields 의 VerifyStage= 필드가 담는다.
                failureCode = (verification.Stage == MapVerificationStage.Decode)
                    ? MapTransferErrorCode.DecodeFailed
                    : MapTransferErrorCode.VerificationFailed;

                failureExtra = BuildVerificationFields(verification);
                return false;
            }

            if (verification.MapVersion != _clientHeaderMapVersion)
            {
                failureCode = MapTransferErrorCode.MapVersionHeaderMismatch;
                failureExtra = $"HeaderMapVersion={_clientHeaderMapVersion}, " +
                               $"BodyMapVersion={verification.MapVersion}";
                return false;
            }

            failureCode = MapTransferErrorCode.None;
            return true;
        }

        /// <summary>
        /// [Client] 검증을 통과한 맵을 전투 씬으로 넘기기 위한 <see cref="MapPreparationResult"/> 로 감싼다.
        ///
        /// ⚠️ <b>생성 계통 3항목(생성 소요 시간 · 시도 횟수 · 폴백 사용 여부)은 0 / 0 / false 다.</b>
        ///    Client 는 맵을 <b>만들지 않았으므로</b> 그 값을 가질 수 없다. 여기에 Client 의
        ///    검증 비용(복원 탐색 횟수 등)을 대신 넣지 않는다 — 같은 필드 이름이 판마다 다른 것을
        ///    재게 되어 집계가 조용히 망가진다. 그 판의 진짜 값은 <b>Host 로그</b>에 있다.
        /// </summary>
        /// <param name="verification">D 방식 검증 결과(통과한 것)</param>
        /// <param name="payload">받은 canonical 바이트</param>
        /// <param name="hash">그 바이트의 SHA-256 원본 32바이트</param>
        /// <returns>전투 씬 투영에 쓸 수 있는 준비 결과</returns>
        private static MapPreparationResult BuildClientPreparedMap(MapVerificationResult verification,
                                                                   byte[] payload, byte[] hash)
        {
            // 정의에도 해시를 채워 둔다(Host 쪽 BuildSuccess 가 하는 것과 같은 처리).
            verification.Definition.Hash = hash;

            return new MapPreparationResult(
                true, MapPreparationErrorCode.None, null,
                verification.Definition, payload, hash,
                verification.MapVersion, verification.RootSeed, verification.MapType,
                verification.NeutralMineCount, verification.StartingMineSide,
                verification.TestModeFlag == MapDefinitionValidator.TestModeFlag,
                verification.InitialGold,
                0L, 0, false);
        }

        /// <summary>
        /// [Client] 이번 회차를 실패로 끝내고 Host 에게 실패를 답한다. 결말 로그를 <b>정확히 한 줄</b> 남긴다.
        /// </summary>
        /// <param name="nonce">이번 회차 번호</param>
        /// <param name="code">내부 error code(Host 에게도 그대로 전달된다)</param>
        /// <param name="outcomeKey">결말 키</param>
        /// <param name="message">사람이 읽는 결말 설명</param>
        /// <param name="expectedHash">Host 가 알려 준 해시(없으면 null)</param>
        /// <param name="clientHash">Client 가 계산한 해시(계산 전이면 null)</param>
        /// <param name="extraFields">덧붙일 key=value 조각(없으면 null)</param>
        private void FailClientRound(ulong nonce, MapTransferErrorCode code, LogEvent outcomeKey,
                                     string message, byte[] expectedHash, byte[] clientHash,
                                     string extraFields)
        {
            SetState(MapTransferState.Failed, "수신·검증 실패");

            LogOutcomeOnce(outcomeKey, false, message,
                           BuildTransferLogData(expectedHash, clientHash, code, extraFields));

            // 🔴 Host 에게 반드시 답한다. 답이 없으면 Host 는 timeout 으로 **같은 데이터를
            //    한 번 더 보내게** 되는데, 규칙 16 은 해시 불일치·검증 실패에 대해 재전송을
            //    금지한다. "실패했다"고 즉시 알려 주는 것이 그 규정을 지키는 방법이다.
            MapReadyServerRpc(nonce, false, _assembler == null ? 0 : _assembler.ReceivedByteCount,
                              clientHash ?? new byte[0], (int)code);

            ResetSession();
        }

        // ====================================================================
        // RPC ③ 수신 완료 통보 (Client → Host)
        // ====================================================================

        /// <summary>
        /// [Client → Host] "그 회차 잘 받았다 / 못 받았다"는 답.
        ///
        /// ⚠️ RequireOwnership = false 가 **반드시** 필요하다.
        ///    이 객체는 Host 가 스폰했으므로 소유자도 Host 다. 기본 설정(소유자만 호출 가능)으로 두면
        ///    정작 답을 보내야 할 Client 가 이 RPC 를 부르지 못하고 조용히 막힌다.
        /// </summary>
        /// <param name="nonce">응답하는 회차 번호.</param>
        /// <param name="success">정상적으로 다 받고 검증까지 통과했는가.</param>
        /// <param name="receivedBytes">받은 전체 바이트 수(로그·실측 대조용).</param>
        /// <param name="clientHash">Client 가 계산한 해시(실패로 계산 전이면 빈 배열).</param>
        /// <param name="errorCode">Client 쪽 내부 error code(<see cref="MapTransferErrorCode"/> 의 정수값).</param>
        /// <param name="rpcParams">NGO 가 채워 주는 발신자 정보. 누가 보냈는지 로그에 남기려고 받는다.</param>
        [ServerRpc(RequireOwnership = false)]
        private void MapReadyServerRpc(ulong nonce, bool success, int receivedBytes,
                                       byte[] clientHash, int errorCode,
                                       ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (nonce != _activeNonce)
            {
                // 낡은 회차에 대한 답이다. 축 A = Warn(무시하고 계속) · 축 B = 개발.
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "지금 회차의 응답이 아니어서 무시했다",
                                 $"Nonce={nonce}, ActiveNonce={_activeNonce}, ClientId={senderClientId}");
                return;
            }

            GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                             "수신 완료 응답을 받았다",
                             $"Nonce={nonce}, ClientId={senderClientId}, Success={success}, " +
                             $"ReceivedBytes={receivedBytes}, ErrorCode={(MapTransferErrorCode)errorCode}");

            string clientIdField = "ClientId=" + senderClientId;

            if (!success)
            {
                // 🔴 Client 가 실패를 알려 왔다. **재전송하지 않는다.**
                //    규칙 16 이 재전송을 허용하는 것은 timeout 과 조각 불완전 수신뿐이고,
                //    해시 불일치·역직렬화 실패·공정성 검증 실패·미지원 버전은 즉시 실패다.
                //    같은 데이터를 다시 보내 봐야 같은 결과가 나오기 때문이다.
                MapTransferErrorCode code = (MapTransferErrorCode)errorCode;

                SetState(MapTransferState.Failed, "응답 수신(실패)");

                LogOutcomeOnce(ToOutcomeKey(code), true,
                               "Client 가 실패를 알려 와 전송을 실패로 끝낸다(재전송 없음)",
                               BuildTransferLogData(_hostHash, clientHash, code, clientIdField));

                ClearHostPackage();

                // [3단계 I] 씬 전환 게이트에 "이 판은 성립하지 않았다"고 알린다 → 로비 유지.
                NotifyHostOutcomeOnce(false, code);
                return;
            }

            // 🔴 Client 가 성공이라고 답했더라도 **Host 가 직접 원본 32바이트로 한 번 더 대조한다.**
            //    "상대가 괜찮다고 했다"를 그대로 믿으면 Host/Client 해시 비교라는 절차 자체가
            //    Client 의 자기 신고가 되어 버린다(규칙 16 이 요구하는 것은 양측 값의 비교다).
            if (!MapDefinitionCodec.HashEquals(clientHash, _hostHash))
            {
                SetState(MapTransferState.Failed, "응답 수신(해시 불일치)");

                LogOutcomeOnce(LogEvent.MapHashMismatch, true,
                               "Client 가 성공이라고 답했지만 해시가 Host 값과 다르다 — 즉시 실패",
                               BuildTransferLogData(_hostHash, clientHash,
                                                    MapTransferErrorCode.HashMismatch, clientIdField));

                ClearHostPackage();

                // [3단계 I] 규칙 16 "해시가 다르면 전투 씬으로 이동하지 않고 기존 로비를 유지한다".
                NotifyHostOutcomeOnce(false, MapTransferErrorCode.HashMismatch);
                return;
            }

            SetState(MapTransferState.Completed, "응답 수신(성공)");

            if (_hostIsProbe)
            {
                // 프로브는 운영 지표가 아니다 — 실측 작업이 장애 지표를 오염시키면 안 된다.
                GameLog.Dev.Info(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "전송 프로브 왕복 성공",
                                 $"Nonce={nonce}, ClientId={senderClientId}, " +
                                 $"TotalBytes={_hostTotalBytes}, ChunkSize={_hostChunkSize}, " +
                                 $"ChunkCount={(_hostChunks == null ? 0 : _hostChunks.Length)}");

                ClearHostPackage();
                return;
            }

            // 확정된 맵을 인계 홀더에 심는다(Host 쪽). 꺼내 쓰는 쪽은 I 단계다.
            MapHandoff.Set(_hostPrepared);

            // [운영/Info] 이 판의 맵 전송이 정상으로 끝났다. 실패 3종의 **분모**가 된다.
            LogOutcomeOnce(LogEvent.MapTransferSucceeded, true,
                           "Host 와 Client 의 최종 맵 해시가 일치한다",
                           BuildTransferLogData(_hostHash, clientHash, MapTransferErrorCode.None,
                                                clientIdField));

            ClearHostPackage();

            // ── [3단계 I] 🔴 씬 전환 게이트가 열리는 자리 ─────────────────────
            //    규칙 16 *"해시가 같을 때만 전투 씬 전환을 시작한다"*.
            //    여기까지 왔다는 것은 다음이 **전부** 확인됐다는 뜻이다.
            //      · Client 가 모든 조각을 받아 선언된 크기와 일치했다
            //      · Client 가 계산한 해시가 Host 의 것과 같았다(Client 가 확인)
            //      · 그 해시를 Host 가 원본 32바이트로 **한 번 더** 직접 대조했다(바로 위)
            //      · Client 가 「D 방식」 재생성·공정성 검증까지 통과했다
            //      · 양쪽 모두 확정 맵을 MapHandoff 에 심었다(전투 씬이 꺼내 갈 준비 완료)
            //    이 통보를 받은 NetworkGameManager 가 비로소 LoadGameScene() 을 부른다.
            //
            // ⚠️ 순서가 중요하다 — MapHandoff.Set 이 **이 줄보다 위**에 있어야 한다.
            //    통보가 먼저 가면 씬 로드가 시작된 뒤에 맵을 심게 되어,
            //    전투 씬이 "인계된 맵이 없다"고 판정할 여지가 생긴다.
            NotifyHostOutcomeOnce(true, MapTransferErrorCode.None);
        }

        // ====================================================================
        // 결말 로그 — 🔴 한 회차에 정확히 한 줄
        // ====================================================================

        /// <summary>
        /// 결말 로그를 <b>이번 회차에 한 번만</b> 남긴다.
        ///
        /// 🔴 결말 키 4종(<c>MapTransferSucceeded</c> · <c>MapTransferFailed</c> ·
        ///    <c>MapHashMismatch</c> · <c>MapClientVerificationFailed</c>)은 서로 배타적이라
        ///    한 번의 전송은 그중 <b>정확히 하나</b>로 끝나야 한다. 두 줄이 남는 코드 경로가
        ///    있으면 그것은 버그다(LogRules 1.5 의 3단계 E 블록).
        ///    재전송·중복 응답·연결 끊김이 겹치면 결말 자리를 두 번 밟을 수 있어서,
        ///    깃발 하나로 그 가능성을 구조적으로 막는다.
        ///
        /// ⚠️ <c>MapTransferRetried</c> 는 <b>여기를 거치지 않는다.</b> 그것만 결말이 아니라
        ///    중간 상태 전이라서, 한 회차에 「재전송 1줄 + 결말 1줄」 최대 2줄이 정상이다.
        /// </summary>
        /// <param name="outcomeKey">결말 키</param>
        /// <param name="isHostSide">Host 쪽 결말인가(로그 클래스명·역할 표기에 쓴다)</param>
        /// <param name="message">사람이 읽는 결말 설명</param>
        /// <param name="data">key=value 구조화 필드</param>
        private void LogOutcomeOnce(LogEvent outcomeKey, bool isHostSide, string message, string data)
        {
            if (_outcomeLogged)
            {
                // 이미 결말이 남았다. 두 번째 줄은 남기지 않되, 무엇이 또 오려 했는지는
                // 개발 축으로 남겨 둔다 — 조용히 삼키면 "결말이 두 번 났다"는 버그를 못 찾는다.
                GameLog.Dev.Warn(TransferLogSystem, nameof(NetworkMapTransfer),
                                 "결말 로그가 이미 남아 있어 두 번째 결말을 남기지 않았다",
                                 $"Role={(isHostSide ? "Host" : "Client")}, " +
                                 $"SuppressedEvent={outcomeKey}, Nonce={_activeNonce}");
                return;
            }

            _outcomeLogged = true;

            // 축 A 는 키마다 이미 정해져 있다(LogRules 1.5) — 성공은 Info, 나머지 셋은 Error.
            if (outcomeKey == LogEvent.MapTransferSucceeded)
            {
                GameLog.Ops.Info(outcomeKey, MapLogSystem, nameof(NetworkMapTransfer), message, data);
                return;
            }

            GameLog.Ops.Error(outcomeKey, MapLogSystem, nameof(NetworkMapTransfer), message, data);
        }

        /// <summary>
        /// Client 가 알려 온 내부 error code 를 Host 쪽 결말 키로 옮긴다.
        ///
        /// 🔴 원인 계통이 다르면 키도 다르다(LogRules 1.5 의 3단계 E 블록).
        ///    · 바이트가 다 도착하지 못했다 / 헤더가 어긋났다 → 회선·프로토콜 계통 → MapTransferFailed
        ///    · 다 도착했는데 내용이 다르다                   → 직렬화·해시 계약 계통 → MapHashMismatch
        ///    · 회선도 바이트도 정상인데 검증에서 떨어졌다     → 생성기·검증기 계통 → MapClientVerificationFailed
        /// </summary>
        /// <param name="code">Client 가 보내 온 내부 error code</param>
        /// <returns>결말 키</returns>
        private static LogEvent ToOutcomeKey(MapTransferErrorCode code)
        {
            switch (code)
            {
                case MapTransferErrorCode.HashMismatch:
                    return LogEvent.MapHashMismatch;

                case MapTransferErrorCode.DecodeFailed:
                case MapTransferErrorCode.VerificationFailed:
                    return LogEvent.MapClientVerificationFailed;

                default:
                    return LogEvent.MapTransferFailed;
            }
        }

        // ====================================================================
        // 로그 필드 조립
        //
        // LogRules 1.4: "| key=value, key=value" 는 사람이 읽으라고 붙인 꼬리표가 아니라
        // **서버로 보낼 때 그대로 구조화 필드가 되는 부분**이다. 그래서 아래를 지킨다.
        //   · 키 이름과 값 표기를 그때그때 바꾸지 않는다(한 지표가 조용히 둘로 갈라진다).
        //   · 값에 구분자 ", " 를 넣지 않는다. 자유 문장은 message 쪽으로 보낸다.
        //   · 실수(float)를 값에 그대로 넣지 않는다 — 문화권에 따라 소수점이 ',' 가 된다.
        //
        // 🔴 이 헬퍼들에는 [Conditional] 을 붙이지 않는다(LogRules 1.14 금지 7).
        //    운영 로그의 재료이므로 릴리스 빌드에서도 살아 있어야 한다.
        // ====================================================================

        /// <summary>SHA-256 원본 바이트 수. 대조는 이 길이 전체로 한다.</summary>
        private const int MapDefinitionCodecHashLength = 32;

        /// <summary>
        /// 규칙 12 가 요구하는 전송 관련 로그 항목을 "key=value, key=value" 로 조립한다.
        ///
        /// 2단계 K 가 맵 준비 쪽 11항목을 채웠고, 여기서 나머지 두 항목
        /// <b>「전송/재전송 횟수」</b>(SendCount·ResendCount)와
        /// <b>「Host/Client 해시 비교 결과」</b>(HostHash·ClientHash·HashMatch)를 채운다.
        /// </summary>
        /// <param name="hostHash">Host 쪽 해시(원본 32바이트). 없으면 null</param>
        /// <param name="clientHash">Client 쪽 해시(원본 32바이트). 아직 계산 전이면 null</param>
        /// <param name="code">내부 error code</param>
        /// <param name="extraFields">덧붙일 key=value 조각(없으면 null)</param>
        /// <returns>구조화 필드 문자열</returns>
        private string BuildTransferLogData(byte[] hostHash, byte[] clientHash,
                                            MapTransferErrorCode code, string extraFields)
        {
            // Host 는 자기가 보낸 횟수를, Client 는 자기가 받은 횟수를 싣는다.
            // ⚠️ 권위 있는 값은 Host 쪽이다. Client 값은 "관측한 횟수"이며, 회선이 나쁘면
            //    Host 가 두 번 보냈는데 Client 는 한 번만 받았을 수 있다 — 그 차이 자체가 단서다.
            int sendCount = IsServer ? _hostSendCount : _clientReceiveCount;
            int resendCount = IsServer ? _hostResendCount : (_clientReceiveCount > 0 ? _clientReceiveCount - 1 : 0);

            int chunkCount = IsServer
                ? (_hostChunks == null ? 0 : _hostChunks.Length)
                : _clientExpectedChunkCount;

            int totalBytes = IsServer ? _hostTotalBytes : (_assembler == null ? 0 : _assembler.TotalBytes);
            int mapVersion = IsServer ? _hostMapVersion : _clientHeaderMapVersion;

            string data =
                "Role=" + (IsServer ? "Host" : "Client") +
                ", Nonce=" + _activeNonce +
                ", MapVersion=" + mapVersion +
                ", TotalBytes=" + totalBytes +
                ", ChunkCount=" + chunkCount +
                ", SendCount=" + sendCount +
                ", ResendCount=" + resendCount +
                ", HostHash=" + ToHashField(hostHash) +
                ", ClientHash=" + ToHashField(clientHash) +
                ", HashMatch=" + ToHashMatchField(hostHash, clientHash) +
                ", ErrorCode=" + code;

            return extraFields == null ? data : data + ", " + extraFields;
        }

        /// <summary>
        /// D 방식 검증 결과를 key=value 조각으로 만든다.
        /// 🔴 실패 세 갈래를 가르는 것이 <c>VerifyStage=</c> 다 — 키를 쪼개지 않고 필드로 가른다.
        /// </summary>
        /// <param name="verification">검증 결과(null 이면 빈 문자열)</param>
        /// <returns>key=value 조각</returns>
        private static string BuildVerificationFields(MapVerificationResult verification)
        {
            if (verification == null) return null;

            return
                "VerifyStage=" + verification.Stage +
                ", VerifyError=" + verification.ErrorCode +
                ", Seed=" + verification.RootSeed +
                ", MapType=" + verification.MapType +
                ", NeutralMineCount=" + verification.NeutralMineCount +
                ", StartingMineSide=" + verification.StartingMineSide +
                ", AttemptIndex=" + verification.AttemptIndex +
                ", SearchedCombinations=" + verification.SearchedCombinationCount;
        }

        /// <summary>
        /// 해시를 로그 필드용 문자열로 바꾼다. 🔴 <b>앞 16자만</b> 싣는다(2단계 K 규약).
        /// 값이 없으면 "-" 를 낸다 — 빈 값으로 두면 필드가 통째로 사라져 집계 쪽에서
        /// "없는 필드"와 "값이 빈 필드"를 구분하는 처리가 또 필요해진다.
        /// </summary>
        /// <param name="hash">해시 원본 바이트(없으면 null)</param>
        /// <returns>앞 16자 16진수 문자열 또는 "-"</returns>
        private static string ToHashField(byte[] hash)
        {
            if (hash == null || hash.Length == 0) return "-";

            string hex = MapFallbackTemplateFactory.ToHex(hash);

            return hex.Length <= MapHashLogLength ? hex : hex.Substring(0, MapHashLogLength);
        }

        /// <summary>
        /// Host/Client 해시 비교 결과를 값으로 만든다(규칙 12 로그 항목).
        /// 🔴 <b>대조는 원본 32바이트로 한다</b> — 위 16자 표기로 비교하면 앞 8바이트만 같아도 통과한다.
        /// 아직 비교할 수 없는 시점이면 <c>NotCompared</c> 를 낸다. False 로 적으면
        /// "비교했더니 달랐다"와 "아직 비교 못 했다"가 한 값으로 뭉개진다.
        /// </summary>
        /// <param name="hostHash">Host 쪽 해시</param>
        /// <param name="clientHash">Client 쪽 해시</param>
        /// <returns>True / False / NotCompared</returns>
        private static string ToHashMatchField(byte[] hostHash, byte[] clientHash)
        {
            if (hostHash == null || hostHash.Length == 0) return "NotCompared";
            if (clientHash == null || clientHash.Length == 0) return "NotCompared";

            return MapDefinitionCodec.HashEquals(hostHash, clientHash) ? "True" : "False";
        }

        /// <summary>
        /// 규칙 12 「로그 필수 항목」 중 맵 준비 쪽 12개 필드를 조립한다.
        ///
        /// 🔴 <b>필드 집합·순서는 <c>Bootstrap/GameBootstrapper.Map.cs</c> 의
        ///    <c>BuildMapPreparationLogData</c> 와 반드시 같아야 한다.</b>
        ///    같은 키를 쓰는 같은 지표이므로 한쪽만 바꾸면 집계가 조용히 갈라진다.
        ///    (자리가 둘인 이유: 싱글은 전투 씬에서, 멀티 Host 는 로비에서 맵을 준비한다.)
        /// </summary>
        /// <param name="prepared">맵 준비 결과(성공/실패 모두 받는다)</param>
        /// <returns>구조화 필드 문자열</returns>
        private static string BuildMapPreparationLogData(MapPreparationResult prepared)
        {
            return
                "MapVersion=" + prepared.MapVersion +
                ", Seed=" + prepared.RootSeed +
                ", MapType=" + prepared.MapType +
                ", NeutralMineCount=" + prepared.NeutralMineCount +
                ", StartingMineSide=" + prepared.StartingMineSide +
                ", TestMode=" + prepared.MapTestModeEnabled +
                ", InitialGold=" + prepared.InitialGold +
                ", ElapsedMs=" + prepared.ElapsedMilliseconds +
                ", AttemptCount=" + prepared.AttemptCount +
                ", UsedFallback=" + prepared.UsedFallback +
                ", Hash=" + ToHashField(prepared.Hash) +
                ", ErrorCode=" + prepared.ErrorCode;
        }
    }
}
