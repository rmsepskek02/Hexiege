// ============================================================================
// NetworkLinkState.cs
// 인게임 「상대와의 연결 상태」를 전역으로 알려 주는 정적 홀더 (2026-11-26 신설).
//
// [이 파일이 왜 있나 — 초급 개발자용 설명]
//   경기 중 상대 신호가 끊기면(지연) 화면에 반투명 덮개를 띄우고 게임 조작을 막아야 한다.
//   그런데 「지금 지연 중인가」를 아는 것은 Infrastructure 의 감시기(InGameLinkWatch)이고,
//   그 사실을 알아야 하는 쪽은 Presentation(덮개 UI · 결과 화면)과 Bootstrap(입력 차단)이다.
//   서로 직접 참조하면 레이어 방향이 꼬이므로, 이미 같은 목적으로 쓰이는 NetworkContext 처럼
//   Application 레이어에 「현재 상태를 적어 두는 칸」을 하나 두고 모두가 그 칸을 읽는다.
//
//   ✅ 감시기만 이 칸을 쓴다(Set 계열 메서드). 나머지는 읽기만 한다.
//   ✅ 상태가 바뀌면 GameEvents.OnNetworkLinkStateChanged 로도 알린다(덮개 UI 가 구독).
//
// [결과 사유(MatchEndCause)를 왜 여기 두는가]
//   연결 판정으로 경기가 끝나면 결과 화면이 「승리 + 상대방이 떠났습니다」 또는
//   「(승패 없이) 연결이 끊겼습니다」를 골라 보여 줘야 한다. 결과 화면은 GameEvents.OnGameEnd
//   하나를 받아 화면을 그리므로, 그 이벤트가 발행되기 **직전에** 사유를 이 칸에 적어 두면
//   결과 화면이 이벤트를 받는 순간 한 번에 올바른 화면을 그릴 수 있다.
//   (사유를 별도 이벤트로 나중에 보내면 「패배!」가 잠깐 보였다가 바뀌는 깜빡임이 생기고,
//    진단 로그 ResultShown 도 사유 없이 한 줄, 사유와 함께 또 한 줄로 갈라진다.)
//
// [2026-11-26 2차 수정 — 「문제가 생긴 쪽만 막고 피해 쪽은 막지 않는다」 (사양 ②)]
//   1차는 「지연 · 판정 중이면 무조건 반투명 덮개 + 조작 차단」이었다. 그런데 그러면 Client 가 일부러 자기 네트워크를
//   끊었을 때, 게임이 그대로 진행되는 Host 쪽 조작까지 최대 15초 막혀 버린다(악용 우려 — 사용자 지적 (다)).
//   그래서 2차부터는 「무엇을 보여 주나 · 조작을 막나」를 **차단 방식(BlockMode)** 으로 따로 정한다.
//     Overlay : 기존 반투명 덮개 「네트워크 지연」 + 조작 차단(설정 버튼만 가능) — 내 쪽 문제 · Client 가 Host 를 잃음 · 복구 중
//     Banner  : 화면 상단 알림 띠 「상대 연결 지연」만 — 조작을 막지 않는다(입력이 띠를 통과한다) — Host 이고 상대 쪽 문제 · RTT 만 느림
//     None    : 평소 화면
//   방식을 정하는 규칙은 순수 C# 정책 InGameLinkBlockPolicy 가 갖고, 감시기가 그 결과를 여기에 적는다.
//   🔴 BlocksPlayerCommands(조작 차단 — 월드 입력 · 12개 조작 입구가 읽는다)는 이제 「Overlay 일 때만」 true 다.
//
// task: Assets/_Project/Docs/_Tasks/2026-11-26/10_31_ingame-leave-judgment/Plan.md 3절 1·4단계 (+ 같은 Plan 「2차 수정」 절)
// Application 레이어 — 순수 C# + UniRx(GameEvents). Unity.Netcode 참조 없음.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 인게임 연결 감시 상태(Plan 2-2). Host·Client 가 같은 값을 같은 뜻으로 쓴다.
    /// </summary>
    public enum InGameLinkPhase
    {
        /// <summary>감시 중이 아님(싱글플레이 · 로비 · 감시기 생성 전).</summary>
        Inactive = 0,

        /// <summary>경기 시작 ~ 상대 신호 첫 수신 전. 침묵을 재지 않는다(경기 시작 직후 오판 방지).</summary>
        Waiting = 1,

        /// <summary>상대 신호를 정상적으로 받고 있다.</summary>
        Normal = 2,

        /// <summary>지연 — 덮개 표시 + 조작 차단. 게임은 뒤에서 계속 진행된다.</summary>
        Delayed = 3,

        /// <summary>판정 중 — 인터넷 도달 확인을 기다리는 중. 덮개 유지 + 조작 차단.</summary>
        Judging = 4,

        /// <summary>경기 결과가 정해졌다(정상 종료 · 포기 · 연결 판정). 더 이상 감시하지 않는다.</summary>
        Ended = 5
    }

    /// <summary>
    /// [2026-11-26 2차] 연결 지연 중 화면을 어떻게 막는가(사양 ②). 감시기가 InGameLinkBlockPolicy 로 정한다.
    /// </summary>
    public enum InGameLinkBlockMode
    {
        /// <summary>막지 않는다 · 아무것도 띄우지 않는다(평소 화면).</summary>
        None = 0,

        /// <summary>화면 상단 알림 띠만 띄운다 — 조작을 막지 않는다(입력이 띠를 통과한다).</summary>
        Banner = 1,

        /// <summary>반투명 덮개 + 가운데 문구 + 조작 차단(설정 버튼만 가능) — 1차의 지연 화면 그대로.</summary>
        Overlay = 2
    }

    /// <summary>
    /// [2026-11-26 2차] 차단 방식을 고른 이유(진단 로그 LinkBlockMode 의 Reason 값).
    /// </summary>
    public enum InGameLinkBlockReason
    {
        /// <summary>막을 이유가 없다(평소).</summary>
        None = 0,

        /// <summary>이번 끊김 동안 내 인터넷 확인이 한 번이라도 실패했다 — 내 쪽 문제 → 덮개.</summary>
        SelfProbeFailed = 1,

        /// <summary>내가 Host 이고 내 인터넷 확인이 모두 성공했다 — 상대 쪽 문제 → 알림 띠(내 게임은 계속 진행된다).</summary>
        PeerSide = 2,

        /// <summary>내가 Client 이고 Host 신호가 끊겼다 — 양쪽 게임이 멈춘다 → 덮개(악용 대상 아님 — 사용자 결정).</summary>
        HostLost = 3,

        /// <summary>
        /// 내가 Host 이고 아직 첫 인터넷 확인 결과가 없다 → 알림 띠로 시작(실패하면 덮개로 바뀐다).
        /// [2026-11-26 2차 보완] 끊김 구간 시작 뒤 2.0초까지만 — 그 뒤에도 결과가 없으면 <see cref="ProbeTimeout"/>.
        /// </summary>
        PendingProbe = 4,

        /// <summary>신호는 오지만 왕복 시간(RTT)만 느리다 → 알림 띠(지연이 크다는 이유로 조작을 막지 않는다).</summary>
        Rtt = 5,

        /// <summary>내 앱이 잠깐(13초 미만) 멈췄다가 돌아왔다 → 「연결 복구 중」 덮개(1차 그대로).</summary>
        Recovering = 6,

        /// <summary>
        /// [2026-11-26 2차 보완 — 사용자 승인] 내가 Host 이고 끊김 구간 시작 뒤 2.0초(InGameLinkBlockPolicy.PendingProbeBannerMaxSec)가
        /// 지났는데 인터넷 확인 결과가 하나도 없다 → 덮개(조작 차단). 「상대 쪽 문제라는 확인 전에는 내 쪽 문제로 본다」 —
        /// 와이파이는 붙었는데 인터넷이 안 되면 확인 응답이 최대 10초까지 늦어, 그동안 Host 가 혼자 조작하는 악용을 막는다.
        /// 이후 확인이 성공으로 오면 <see cref="PeerSide"/>(띠)로 내려간다.
        /// </summary>
        ProbeTimeout = 7
    }

    /// <summary>
    /// 결과 화면이 고를 「경기가 끝난 사유」. 연결 판정 경로만 Normal 이 아닌 값을 쓴다.
    /// </summary>
    public enum MatchEndCause
    {
        /// <summary>성 파괴·포기(연결 있음) 등 연결 판정이 아닌 모든 종료.</summary>
        Normal = 0,

        /// <summary>상대 신호가 끊겼고 내 인터넷은 된다 → 상대 이탈 → 내 승리.</summary>
        OpponentLeft = 1,

        /// <summary>상대 신호가 끊겼고 내 인터넷 확인이 실패(또는 확인 불가)했다 → 내 이탈.</summary>
        SelfDisconnected = 2,

        /// <summary>앱이 13초 이상 멈췄다가 돌아왔다 → 스스로 패배 처리.</summary>
        SelfPauseDefeat = 3,

        /// <summary>연결이 없는 상태(지연 중 포함)에서 포기했다 → 그 기기에서 즉시 처리.</summary>
        OfflineForfeit = 4
    }

    /// <summary>
    /// 연결 상태가 바뀌었음을 알리는 이벤트 데이터(GameEvents.OnNetworkLinkStateChanged).
    /// </summary>
    public readonly struct NetworkLinkStateChangedEvent
    {
        /// <summary>새 감시 상태.</summary>
        public readonly InGameLinkPhase Phase;

        /// <summary>「연결 복구 중」 문구를 보여야 하는가(13초 미만으로 앱이 돌아온 직후).</summary>
        public readonly bool IsRecovering;

        /// <summary>이벤트 데이터를 만든다.</summary>
        /// <param name="phase">새 감시 상태.</param>
        /// <param name="isRecovering">「연결 복구 중」 표시 여부.</param>
        public NetworkLinkStateChangedEvent(InGameLinkPhase phase, bool isRecovering)
            : this(phase, isRecovering, LegacyBlockMode(phase), InGameLinkBlockReason.None)
        {
            // [2026-11-26 2차] 1차 생성자(인자 2개)는 그대로 둔다. 차단 방식은 1차 규칙(지연 · 판정 중 = 덮개)으로 채운다.
            // 옛 본문(기록용):
            // Phase = phase;
            // IsRecovering = isRecovering;
        }

        /// <summary>[2026-11-26 2차] 차단 방식(BlockMode)까지 담는 이벤트 데이터를 만든다.</summary>
        /// <param name="phase">새 감시 상태.</param>
        /// <param name="isRecovering">「연결 복구 중」 표시 여부.</param>
        /// <param name="blockMode">화면을 막는 방식(덮개 · 알림 띠 · 없음).</param>
        /// <param name="blockReason">그 방식을 고른 이유.</param>
        public NetworkLinkStateChangedEvent(InGameLinkPhase phase, bool isRecovering,
            InGameLinkBlockMode blockMode, InGameLinkBlockReason blockReason)
        {
            Phase = phase;
            IsRecovering = isRecovering;
            BlockMode = blockMode;
            BlockReason = blockReason;
        }

        /// <summary>[2026-11-26 2차] 화면을 막는 방식.</summary>
        public readonly InGameLinkBlockMode BlockMode;

        /// <summary>[2026-11-26 2차] 그 방식을 고른 이유.</summary>
        public readonly InGameLinkBlockReason BlockReason;

        /// <summary>1차 규칙 — 지연 · 판정 중이면 덮개, 아니면 없음.</summary>
        internal static InGameLinkBlockMode LegacyBlockMode(InGameLinkPhase phase)
            => phase == InGameLinkPhase.Delayed || phase == InGameLinkPhase.Judging
                ? InGameLinkBlockMode.Overlay : InGameLinkBlockMode.None;
    }

    /// <summary>
    /// 인게임 연결 상태의 전역 읽기 창구. 쓰는 쪽은 감시기(Infrastructure InGameLinkWatch) 하나뿐이다.
    /// </summary>
    public static class NetworkLinkState
    {
        /// <summary>현재 감시 상태. 싱글플레이·로비에서는 Inactive.</summary>
        public static InGameLinkPhase Phase { get; private set; } = InGameLinkPhase.Inactive;

        /// <summary>「연결 복구 중」 문구를 보여야 하는가.</summary>
        public static bool IsRecovering { get; private set; }

        /// <summary>
        /// 지금이 멀티플레이 경기 씬인가. 감시기가 만들어질 때 켜지고 Game 씬이 내려갈 때 꺼진다.
        /// 🔴 NetworkContext.IsNetworkActive 와 다른 점: 연결이 끊겨 NGO 가 내려간 뒤에도
        ///    씬이 남아 있는 동안은 계속 true 다. 결과 화면이 「끊긴 뒤에도」 카운트다운·로비 정리를
        ///    멀티 방식으로 하려면 이 값이 필요하다(Plan 3절 9단계).
        /// </summary>
        public static bool IsMultiplayerMatch { get; private set; }

        /// <summary>
        /// 이번 경기 결과가 이미 정해졌는가(어떤 경로든 GameEvents.OnGameEnd 가 한 번 발행됐는가).
        /// 연결 판정이 정상 결과를 덮어쓰지 않게 하는 중복 깃발이다(Plan 2-2 「정상 경기 종료가 우선」, U-10).
        /// </summary>
        public static bool IsMatchResultDecided { get; private set; }

        /// <summary>결과 화면이 고를 종료 사유. OnGameEnd 발행 직전에 정해진다.</summary>
        public static MatchEndCause MatchEndCause { get; private set; } = MatchEndCause.Normal;

        /// <summary>
        /// 지연 덮개 UI 가 씬에 있는가. 덮개가 Awake 에서 직접 알린다.
        /// 감시기는 이 값이 false 면 「덮개 없이 조작만 막는다」는 경고를 한 번 남긴다.
        /// </summary>
        public static bool HasLagOverlay => s_lagOverlayCount > 0;

        /// <summary>
        /// 씬에 있는 덮개 수. bool 이 아니라 수로 세는 이유: 재경기로 같은 씬을 다시 불러올 때 새 덮개의 Awake 와
        /// 옛 덮개의 OnDestroy 중 무엇이 먼저 불리는지 코드로 확정할 수 없어서, 순서와 무관하게 맞게 하기 위함이다.
        /// </summary>
        private static int s_lagOverlayCount;

        /// <summary>
        /// 지금 플레이어 조작(건설·생산·연구·스킬 등) 요청을 서버로 보내면 안 되는가.
        /// 1차: 지연·판정 중일 때만 true(Plan 2-6 「지연 중 누른 조작은 반영하지 않고 재전송하지 않는다」 — U-5).
        /// 2차: 차단 방식이 Overlay(덮개)일 때만 true — 아래 remarks.
        /// </summary>
        /// <remarks>
        /// [2026-11-26 2차 — 사양 ②] 「Overlay(덮개) 방식일 때만」 막는다. Banner(알림 띠)는 막지 않는다 —
        /// 상대 쪽 문제로 지연이 보이는 Host 의 조작을 막으면 일부러 끊는 쪽이 상대 조작을 묶어 둘 수 있기 때문이다(악용 방지).
        /// 옛 식(1차 — 지연 · 판정 중이면 무조건 막음, 기록용):
        /// <code>IsMultiplayerMatch &amp;&amp; (Phase == InGameLinkPhase.Delayed || Phase == InGameLinkPhase.Judging)</code>
        /// </remarks>
        // public static bool BlocksPlayerCommands =>
        //     IsMultiplayerMatch && (Phase == InGameLinkPhase.Delayed || Phase == InGameLinkPhase.Judging);
        public static bool BlocksPlayerCommands =>
            IsMultiplayerMatch && BlockMode == InGameLinkBlockMode.Overlay;

        /// <summary>지연 덮개를 보여야 하는가. 결과가 정해졌으면 결과 화면이 우선이라 보이지 않는다.</summary>
        public static bool ShowsLagOverlay => BlocksPlayerCommands && !IsMatchResultDecided;

        /// <summary>
        /// [2026-11-26 2차] 화면 상단 알림 띠(조작을 막지 않음)를 보여야 하는가. 결과가 정해졌으면 보이지 않는다.
        /// </summary>
        public static bool ShowsLagBanner =>
            IsMultiplayerMatch && BlockMode == InGameLinkBlockMode.Banner && !IsMatchResultDecided;

        /// <summary>[2026-11-26 2차] 지금 화면을 막는 방식(덮개 · 알림 띠 · 없음). 감시기가 정한다.</summary>
        public static InGameLinkBlockMode BlockMode { get; private set; } = InGameLinkBlockMode.None;

        /// <summary>[2026-11-26 2차] 그 방식을 고른 이유(진단 · 문구 선택용).</summary>
        public static InGameLinkBlockReason BlockReason { get; private set; } = InGameLinkBlockReason.None;

        /// <summary>[2026-11-26 2차] 알림 띠 UI 가 씬에 있는가. 띠가 Awake 에서 직접 알린다(덮개와 같은 방식).</summary>
        public static bool HasLagBanner => s_lagBannerCount > 0;

        /// <summary>[2026-11-26 2차] 씬에 있는 알림 띠 수(덮개 수를 세는 이유와 같다 — 재경기 씬 재로드 순서 무관).</summary>
        private static int s_lagBannerCount;

        // ====================================================================
        // 쓰기 — 감시기(InGameLinkWatch) 전용
        // ====================================================================

        /// <summary>
        /// 새 멀티플레이 경기 시작 — 지난 경기 값을 모두 지우고 Waiting 으로 시작한다.
        /// </summary>
        public static void BeginMatch()
        {
            IsMultiplayerMatch = true;
            IsMatchResultDecided = false;
            MatchEndCause = MatchEndCause.Normal;
            // [2026-11-26 2차] 차단 방식까지 함께 비운다. 옛 줄: SetPhase(InGameLinkPhase.Waiting, false);
            SetLinkState(InGameLinkPhase.Waiting, false, InGameLinkBlockMode.None, InGameLinkBlockReason.None);
        }

        /// <summary>
        /// 경기 씬이 내려갔다 — 감시를 끝내고 싱글플레이 기본값으로 되돌린다.
        /// </summary>
        public static void EndMatch()
        {
            IsMultiplayerMatch = false;
            IsMatchResultDecided = false;
            MatchEndCause = MatchEndCause.Normal;
            // [2026-11-26 2차] 차단 방식까지 함께 비운다. 옛 줄: SetPhase(InGameLinkPhase.Inactive, false);
            SetLinkState(InGameLinkPhase.Inactive, false, InGameLinkBlockMode.None, InGameLinkBlockReason.None);
        }

        /// <summary>
        /// 감시 상태를 바꾼다. 실제로 바뀌었을 때만 GameEvents.OnNetworkLinkStateChanged 를 발행한다.
        /// </summary>
        /// <param name="phase">새 감시 상태.</param>
        /// <param name="isRecovering">「연결 복구 중」 문구 표시 여부.</param>
        /// <remarks>
        /// [2026-11-26 2차] 1차 호환용으로 남긴다 — 차단 방식은 1차 규칙(지연 · 판정 중 = 덮개)으로 채운다.
        /// 2차의 감시기는 이 메서드 대신 <see cref="SetLinkState"/> 를 부른다.
        /// </remarks>
        public static void SetPhase(InGameLinkPhase phase, bool isRecovering)
        {
            // 옛 본문(1차 — 기록용):
            // if (Phase == phase && IsRecovering == isRecovering) return;
            // Phase = phase;
            // IsRecovering = isRecovering;
            // GameEvents.OnNetworkLinkStateChanged.OnNext(new NetworkLinkStateChangedEvent(phase, isRecovering));
            SetLinkState(phase, isRecovering, NetworkLinkStateChangedEvent.LegacyBlockMode(phase), InGameLinkBlockReason.None);
        }

        /// <summary>
        /// [2026-11-26 2차] 감시 상태와 차단 방식을 함께 바꾼다. 넷 중 하나라도 실제로 바뀌었을 때만
        /// GameEvents.OnNetworkLinkStateChanged 를 발행한다(덮개 · 알림 띠 UI 가 이 이벤트로 즉시 켜지고 꺼진다).
        /// </summary>
        /// <param name="phase">새 감시 상태.</param>
        /// <param name="isRecovering">「연결 복구 중」 문구 표시 여부.</param>
        /// <param name="blockMode">화면을 막는 방식.</param>
        /// <param name="blockReason">그 방식을 고른 이유.</param>
        public static void SetLinkState(InGameLinkPhase phase, bool isRecovering,
            InGameLinkBlockMode blockMode, InGameLinkBlockReason blockReason)
        {
            if (Phase == phase && IsRecovering == isRecovering && BlockMode == blockMode && BlockReason == blockReason) return;
            Phase = phase;
            IsRecovering = isRecovering;
            BlockMode = blockMode;
            BlockReason = blockReason;
            GameEvents.OnNetworkLinkStateChanged.OnNext(
                new NetworkLinkStateChangedEvent(phase, isRecovering, blockMode, blockReason));
        }

        /// <summary>
        /// 결과 종료 사유를 적는다. 🔴 반드시 GameEvents.OnGameEnd 발행 <b>직전</b>에 부른다.
        /// </summary>
        /// <param name="cause">종료 사유.</param>
        public static void SetMatchEndCause(MatchEndCause cause)
        {
            MatchEndCause = cause;
        }

        /// <summary>경기 결과가 정해졌다고 표시한다(어떤 경로의 OnGameEnd 든).</summary>
        public static void MarkMatchResultDecided()
        {
            IsMatchResultDecided = true;
        }

        /// <summary>지연 덮개 UI 가 씬에 생겼다/사라졌다를 알린다(덮개 UI 의 Awake/OnDestroy).</summary>
        /// <param name="present">덮개가 있으면 true.</param>
        public static void SetLagOverlayPresent(bool present)
        {
            s_lagOverlayCount += present ? 1 : -1;
            if (s_lagOverlayCount < 0) s_lagOverlayCount = 0;
        }

        /// <summary>[2026-11-26 2차] 알림 띠 UI 가 씬에 생겼다/사라졌다를 알린다(띠 UI 의 Awake/OnDestroy).</summary>
        /// <param name="present">띠가 있으면 true.</param>
        public static void SetLagBannerPresent(bool present)
        {
            s_lagBannerCount += present ? 1 : -1;
            if (s_lagBannerCount < 0) s_lagBannerCount = 0;
        }

        /// <summary>
        /// 에디터 「도메인 리로드 없이 플레이」 대비 — 플레이 시작 때 정적 값을 비운다.
        /// (Application 은 UnityEngine 속성을 쓰지 않으므로 Infrastructure 감시기 파일이 이 메서드를 부른다.)
        /// </summary>
        public static void ResetForNewRuntime()
        {
            IsMultiplayerMatch = false;
            IsMatchResultDecided = false;
            MatchEndCause = MatchEndCause.Normal;
            s_lagOverlayCount = 0;
            s_lagBannerCount = 0;                       // [2026-11-26 2차]
            Phase = InGameLinkPhase.Inactive;
            IsRecovering = false;
            BlockMode = InGameLinkBlockMode.None;       // [2026-11-26 2차]
            BlockReason = InGameLinkBlockReason.None;   // [2026-11-26 2차]
        }
    }
}
