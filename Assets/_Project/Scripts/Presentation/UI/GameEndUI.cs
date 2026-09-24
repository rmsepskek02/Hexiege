// ============================================================================
// GameEndUI.cs
// 게임 종료 시 승리/패배 팝업을 표시하고 다시하기 버튼을 제공.
//
// 역할:
//   1. Initialize()에서 OnGameEnd 이벤트 구독 → 승리/패배 텍스트 표시
//   2. Time.timeScale = 0 으로 게임 일시정지
//   3. 다시하기 버튼 → GameBootstrapper.LoadMap() 호출로 재시작
//
// 씬 구조 (Inspector에서 수동 배치):
//   [UI] Canvas
//     └─ GameEndPanel (비활성 상태)
//         ├─ Background (전체 화면, 반투명 검정)
//         ├─ ResultText (TMP - "승리!" / "패배!")
//         └─ RestartButton (버튼 - "다시하기")
//
// 초기화 방식:
//   GameBootstrapper.LoadMap()에서 Initialize() 호출.
//   다른 UI 컴포넌트(GameHudUI, ProductionPanelUI)와 동일한 패턴.
//   Awake()를 사용하지 않음 — 패널이 비활성 상태로 시작할 수 있으므로.
//
// 플레이어 = Blue 팀 고정.
//   Blue Castle 파괴 = 패배, Red Castle 파괴 = 승리.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Bootstrap;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class GameEndUI : MonoBehaviour, IGameUI
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("UI References")]
        [Tooltip("게임 종료 패널 (AnimatedPanel 부착, Show()/Hide()로 토글)")]
        [SerializeField] private AnimatedPanel _panel;

        [Tooltip("승리/패배 결과 텍스트")]
        [SerializeField] private TextMeshProUGUI _resultText;

        [Tooltip("다시하기 버튼")]
        [SerializeField] private Button _restartButton;

        [Header("Dependencies")]
        [Tooltip("GameBootstrapper (재시작용)")]
        [SerializeField] private GameBootstrapper _bootstrapper;

        // NGO 종료는 NetworkGameManager.BackToLobby()에 위임한다(GameEndUI는 Unity.Netcode를 직접 참조하지 않음).
        // (NetworkGameManager는 Infrastructure 레이어 컴포넌트로 NGO를 안전하게 종료하는 책임을 가짐)
        [Tooltip("네트워크 게임 매니저. 멀티플레이 시 로비 복귀 처리 위임용. 싱글플레이면 null이어도 됨.")]
        [SerializeField] private NetworkGameManager _networkGameManager;

        [Header("로비 복귀")]
        [Tooltip("로비로 돌아가기 버튼")]
        [SerializeField] private Button _backToLobbyButton;

        [Tooltip("자동 복귀 카운트다운 텍스트 (예: '30초 후 로비로 돌아갑니다.')")]
        [SerializeField] private TextMeshProUGUI _countdownText;

        // 공통 UI 규칙 D-4 「자동 로비 복귀 카운트다운 — 기본 60초」.
        // ⚠️ 이 값은 씬(Game.unity)에 직렬화된 Inspector 값이 우선한다.
        //    여기만 고치면 실제 동작은 바뀌지 않으므로 씬 값도 함께 맞춰야 한다.
        [Tooltip("자동 복귀까지 대기 시간 (초). 기본 60초.")]
        [SerializeField] private float _autoReturnSeconds = 60f;

        [Header("다시하기 버튼 텍스트")]
        [Tooltip("다시하기 버튼의 텍스트 컴포넌트. 요청 중 상태 표시용.")]
        [SerializeField] private TextMeshProUGUI _restartButtonText;

        // ====================================================================
        // 상대 이탈 관련 고정값 (공통 UI 규칙 D-1 · D-4)
        //
        // [초급자용 설명] 왜 [SerializeField] 가 아니라 const 인가
        //   [SerializeField] 로 두면 그 값이 씬 파일(Game.unity)에 저장되고,
        //   런타임에는 **씬에 저장된 값이 코드 기본값을 덮어쓴다.**
        //   그래서 코드만 고치면 실제 동작이 바뀌지 않는 함정이 생긴다
        //   (바로 위 _autoReturnSeconds 가 실제로 그 함정에 걸려 코드와 씬을 모두 고쳐야 했다).
        //   아래 두 값은 규칙이 고정한 값이라 Inspector 에서 조절할 이유가 없으므로
        //   const 로 둬서 「코드 한 자리만 고치면 끝」이 되게 한다(씬 작업 불필요).
        // ====================================================================

        /// <summary>
        /// 상대 이탈 시 타이머 텍스트 문구 형식 (공통 UI 규칙 D-1).
        /// <c>{0}</c> 자리에 남은 초가 들어간다.
        ///
        /// 🔴 <b>이 문자열은 코드 전체에 이 한 곳에만 둔다.</b> 평시 문구와는
        /// <c>CountdownCoroutine</c> 안의 분기 하나로만 갈린다 — 같은 문구를 두 곳에 적으면
        /// 한쪽만 고쳐졌을 때 화면에 두 가지 표현이 섞여 나온다.
        /// </summary>
        private const string OpponentLeftCountdownFormat = "상대방이 떠났습니다. {0}초 뒤 로비로 이동합니다.";

        /// <summary>
        /// 상대 이탈 판정 시 자동 로비 복귀 카운트다운을 다시 시작할 길이(초) — 공통 UI 규칙 D-4.
        ///
        /// [초급자용 설명] 왜 남은 시간을 그대로 쓰지 않는가
        ///   이탈은 카운트다운이 거의 끝나갈 때 판정될 수도 있다. 그때 남은 시간을 그대로 두면
        ///   바뀐 타이머 문구를 읽기도 전에 화면이 사라져 버린다. 그래서 판정 시점의
        ///   남은 시간이 얼마였든 **무조건 30초로 다시 시작**해 읽을 시간을 보장한다.
        ///
        /// ⚠️ <b>규칙 M-3(재경기 맵 준비 실패)의 카운트다운 재시작과 섞지 않는다.</b>
        ///    그쪽은 <b>전체 길이</b>(_autoReturnSeconds)로 재시작하고 이쪽은 <b>30초</b>다.
        ///    재시작 시점도 길이도 다르므로 진입 메서드를 공유하지 않는다
        ///    (이탈 전용 진입점은 <c>RestartCountdownForOpponentLeft()</c> 하나뿐이다).
        /// </summary>
        private const float OpponentLeftCountdownSeconds = 30f;

        /// <summary>
        /// 상대 이탈 알림 팝업의 타이틀 (공통 UI 규칙 D-6 2항).
        /// </summary>
        private const string OpponentLeftAlertTitle = "알림";

        /// <summary>
        /// 상대 이탈 알림 팝업의 본문 (공통 UI 규칙 D-6 2항).
        ///
        /// ⚠️ 위 <see cref="OpponentLeftCountdownFormat"/> 의 앞부분과 겹쳐 보이지만
        ///    <b>같은 문자열이 아니다.</b> 타이머 쪽은 남은 초가 함께 들어가는 형식 문자열이고,
        ///    이쪽은 남은 초가 없는 한 문장이다. 규칙 D-1 과 규칙 D-6 이 두 자리의 문구를
        ///    각각 따로 정하고 있으므로 하나로 합치지 않는다.
        /// </summary>
        private const string OpponentLeftAlertMessage = "상대방이 떠났습니다.";

        /// <summary>
        /// 상대 이탈 알림 팝업의 버튼 라벨 (공통 UI 규칙 D-6 2항). 누르면 로비로 이동한다.
        /// </summary>
        private const string OpponentLeftAlertButtonLabel = "로비로";

        // ====================================================================
        // 재경기 수락 접수 이후의 「재경기 준비 중」 상태 (2026-09-22)
        //
        // [초급자용 설명] 이 상태가 왜 필요한가
        //   재경기는 ① 한 쪽이 요청하고 ② 다른 쪽이 수락하면 성립한다. 수락이 접수되면
        //   서버가 새 맵을 만들어 상대에게 보내고 검증까지 마친 뒤에야 씬이 재로드된다.
        //   그 사이에 자동 로비 복귀 카운트다운이 그대로 돌면, 맵을 만드는 도중에
        //   혼자 로비로 나가 버린다. 그래서 이 구간에는 **타이머를 멈추고**
        //   상태 줄을 「재경기 준비 중...」으로 바꾼다.
        //
        // 🔴 판단 기준 — 「사람을 기다리면 타이머가 돈다. 시스템을 기다리면 타이머가 멈춘다.」
        //   · 재경기를 **요청만** 해 둔 동안에는 *상대가 수락할지*(사람)를 기다린다.
        //     상대가 팝업을 띄운 채 아무것도 누르지 않으면 답이 영영 오지 않으므로
        //     **타이머가 계속 돌아야** 사용자가 갇히지 않는다(그래서 요청 직후에는 이 상태로 가지 않는다).
        //   · 수락이 접수된 뒤에는 *서버가 맵을 만드는 것*(시스템)을 기다린다.
        //     끝나는 시점이 정해져 있으므로 **타이머를 멈춘다.**
        //
        // 🔴 이 상태로 들어가는 경로가 두 쪽이 다르다 — 이것이 2026-09-22 수정의 핵심이다.
        //   · **수락한 쪽**: 수락 버튼을 누른 **즉시**(OnLocalRematchAccepted) 자기 화면을 바꾼다.
        //     서버 응답을 기다리지 않는다 — 수락자가 Client 이고 Host 가 이미 떠났으면
        //     수락 ServerRpc 가 받을 서버 자체가 없어 **응답이 영영 오지 않기 때문**이다.
        //     기다리게 만들면 **수락자가 Host 냐 Client 냐에 따라 화면이 달라진다.**
        //   · **요청한 쪽**: 서버가 수락을 접수했다는 **통보**(OnNetworkRematchAccepted)를 받고 바꾼다.
        //     요청자는 달리 수락 사실을 알 길이 없어서, 지금까지는 수락이 됐는데도
        //     자기 60초 타이머가 만료되면 혼자 로비로 나가 버렸다.
        // ====================================================================

        /// <summary>
        /// 재경기 수락이 접수된 뒤 상태 줄(<see cref="_countdownText"/>)에 표시할 문구.
        ///
        /// ⚠️ 같은 문구가 <c>OnNetworkRematchStarting</c> 구독의 전역 로딩 메시지에도 쓰이지만
        ///    <b>그 자리는 이번 범위가 아니라 손대지 않았다.</b> 둘은 서로 다른 자리다 —
        ///    이쪽은 결과 화면 위에 그대로 떠 있는 **상태 줄**이고, 그쪽은 씬 재로드 직전에
        ///    화면을 덮는 **로딩 화면**이다.
        ///
        /// 🔴 <b>로딩 화면(<c>ShowLoading</c>)을 이 상태에 띄우지 않는다.</b>
        ///    <c>LoadingScreen.Show()</c> 가 <c>blocksRaycasts = true</c> 로 입력을 막아
        ///    「로비로」 버튼을 누를 수 없게 되는데, 그것은 규칙 D-3(로비 복귀 버튼은 항상 활성)
        ///    위반이다. 그래서 상태 줄 문구만 바꾼다.
        /// </summary>
        private const string RematchPreparingStatusText = "재경기 준비 중...";

        /// <summary>
        /// 재경기가 <b>성립하지 못했을 때</b> 상태 줄에 표시할 문구. <c>{0}</c> 자리에 남은 초가 들어간다.
        ///
        /// <para>
        /// 🔴 <b>재경기가 안 되는 길은 셋인데 이 문구 하나로 끝난다.</b>
        /// <list type="number">
        ///   <item>수락을 서버로 <b>아예 못 보냈다</b>(연결이 이미 내려가는 중).</item>
        ///   <item>서버가 <b>새 맵 준비에 실패</b>했다고 통보해 왔다.</item>
        ///   <item><b>준비 한도가 지났는데 아무 통보도 오지 않았다.</b></item>
        /// </list>
        /// 사용자는 이 셋을 구분할 수 없고 <b>구분할 필요도 없다</b> — 전부 「재경기가 안 됐다」다.
        /// 그래서 셋이 <b>같은 화면으로 끝난다</b>(공통 진입점은 <see cref="EnterRematchFailedState"/>).
        /// 이것은 이번 작업의 대전제 — <b>같은 결과면 같은 화면</b> — 의 연장이다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>실패를 알리는 팝업(<c>ShowAlert</c>)을 띄우지 않는다.</b> 팝업은 화면을 덮어
        /// 「로비로」 버튼을 가리거나 못 누르게 만들고, 그것은 규칙 D-3(로비 복귀 버튼은 항상 활성)
        /// 위반이다. 그래서 <b>이미 떠 있는 상태 줄 자리</b>를 쓴다 — 규칙 D-1 이 상대 이탈을 알릴 때
        /// 쓴 것과 같은 판단이다. <b>나중에 여기에 팝업을 다시 넣지 말 것.</b>
        /// (규칙 M-3 이 2026-09-16 에 「알림 팝업으로 알린다」로 확정했던 것을 상태 줄로 바꾸는
        ///  개정이며, 문서 반영은 별도로 진행된다.)
        /// </para>
        ///
        /// ⚠️ 위 <see cref="OpponentLeftCountdownFormat"/> 과 같은 모양의 형식 문자열이지만
        ///    <b>다른 사실을 알린다.</b> 「상대가 떠났다」와 「재경기를 시작할 수 없다」는 원인이 다르고,
        ///    둘이 동시에 성립할 수도 있다(그때의 우선순위는 <see cref="CountdownCoroutine"/> 참조).
        /// </summary>
        private const string RematchFailedCountdownFormat = "재경기를 시작할 수 없습니다. {0}초 후 로비로 돌아갑니다.";

        /// <summary>
        /// 재경기가 <b>상대가 나가서</b> 성립하지 못했을 때 상태 줄에 표시할 문구.
        /// <c>{0}</c> 자리에 남은 초가 들어간다.
        ///
        /// <para>
        /// 🔴 <b>왜 실패 문구를 둘로 가르는가</b> —
        /// <c>GameSystemRules_RandomMap.md</c> 규칙 18: *"플레이어에게 「맵 준비가 실패했다」와
        /// 「상대가 나갔다」는 **할 수 있는 일이 다르다.** 앞쪽은 다시 시도할 여지가 있고 뒤쪽은 없다."*
        /// 그런데 <b>되돌리는 절차 자체는 두 경우가 완전히 같으므로</b>, 절차를 복제하지 않고
        /// <b>표시만</b> 가른다(같은 규칙: *"가르는 것은 화면 문구 하나뿐이다"*).
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>이 문구를 쓰는 조건은 하나뿐이다</b> — 실패 사유가
        /// <see cref="RematchMapFailureCause.OpponentDisconnected"/> 일 때. 사유를 <b>모를 때는
        /// 이 문구를 쓰지 않는다</b>(위 <see cref="RematchFailedCountdownFormat"/> 을 쓴다).
        /// 모르는데 「상대가 나갔다」고 쓰면 <b>거짓을 말할 수 있기</b> 때문이다
        /// (<c>CLAUDE.md</c> 규칙 10 — 추정 금지. 자세한 근거는
        /// <c>NetworkGameEndController.SendAcceptRematchSafely</c> 의 catch 주석).
        /// </para>
        ///
        /// ⚠️ <b>이 문구를 주석이나 다른 파일에 베껴 적지 말 것.</b> 사본이 하나라도 생기면
        ///    「문구가 코드에 한 곳만 있는가」를 확인하는 grep 이 무용해진다
        ///    (<see cref="OpponentLeftCountdownFormat"/> 에서 실제로 겪은 일이다).
        ///    가리켜야 할 때는 문구 대신 <b>이 상수 이름</b>을 쓴다.
        ///
        /// ⚠️ <see cref="OpponentLeftCountdownFormat"/>(규칙 D-1)과 <b>다른 자리·다른 사실</b>이다.
        ///    그쪽은 「경기가 끝난 뒤 상대가 사라졌다」는 이탈 판정 문구이고, 이쪽은
        ///    「재경기를 만들다가 상대가 사라져 재경기가 안 됐다」는 실패 문구다.
        ///    둘이 동시에 성립하면 <b>이탈 문구가 이긴다</b>(우선순위는 <see cref="CountdownCoroutine"/> 참조).
        /// </summary>
        private const string RematchFailedByOpponentLeftCountdownFormat =
            "상대방이 나가서 재경기를 시작할 수 없습니다. {0}초 후 로비로 돌아갑니다.";

        // ====================================================================
        // 색상 설정
        // ====================================================================

        [Header("색상 설정")]
        [Tooltip("프로젝트 공용 UI 색상 설정 에셋. Resources/Config/UIColorConfig.asset 을 연결. " +
                 "승리/패배 결과 텍스트 색상이 이 에셋에서 결정된다.")]
        [SerializeField] private UIColorConfig _colorConfig;

        /// <summary> 현재 이벤트 구독. 재초기화 시 이전 구독 정리용. </summary>
        private System.IDisposable _gameEndSubscription;

        /// <summary> 멀티플레이 재경기 활성화 이벤트 구독 해제용. </summary>
        private System.IDisposable _rematchAvailableSubscription;

        /// <summary> 멀티플레이 재경기 거절 이벤트 구독 해제용. </summary>
        private System.IDisposable _rematchDeclinedSubscription;

        /// <summary>
        /// [수락한 쪽] 로컬 재경기 수락(팝업의 「수락」 버튼) 이벤트 구독 해제용.
        /// 🔴 <b>서버 응답이 아니라 자기 버튼 입력</b>을 듣는다 — 이유는 위 「재경기 준비 중」 절 참조.
        /// </summary>
        private System.IDisposable _localRematchAcceptedSubscription;

        /// <summary>
        /// [요청한 쪽] 서버의 재경기 수락 접수 통보 구독 해제용.
        /// </summary>
        private System.IDisposable _rematchAcceptedSubscription;

        /// <summary> 멀티플레이 재경기 맵 준비 실패 이벤트 구독 해제용(재경기 맵 C 단계). </summary>
        private System.IDisposable _rematchMapFailedSubscription;

        /// <summary> 멀티플레이 재경기 시작(씬 재로드 직전) 이벤트 구독 해제용. </summary>
        private System.IDisposable _rematchStartingSubscription;

        /// <summary> 네트워크 로비 복귀(씬 전환) 이벤트 구독 해제용. </summary>
        private System.IDisposable _backToLobbySubscription;

        /// <summary> 결과 화면에서의 상대 이탈 알림 구독 해제용 (공통 UI 규칙 D-1 · D-2 · D-4 · D-6). </summary>
        private System.IDisposable _opponentLeftSubscription;

        /// <summary>
        /// 상대가 이탈한 것으로 판정됐는지 여부.
        /// 카운트다운 문구를 평시/이탈 중 어느 쪽으로 쓸지 가르고(규칙 D-1),
        /// 이탈 뒤에 재경기 버튼이 다시 켜지지 않게 막는 데도 쓴다(규칙 D-2).
        /// </summary>
        private bool _opponentLeft;

        /// <summary> 자동 로비 복귀 카운트다운 코루틴. </summary>
        private Coroutine _countdownCoroutine;

        /// <summary>
        /// 지금 「재경기 준비 중」 상태인가. 같은 상태로 두 번 들어가지 않게 막는 깃발이다.
        ///
        /// [초급자용 설명] 왜 필요한가 — 수락한 쪽은 <b>버튼을 누른 즉시</b> 이 상태에 들어가고,
        ///   그 직후 서버가 보낸 「수락 접수」 통보를 <b>수락한 쪽도 함께</b> 받는다(대상을
        ///   한 쪽으로 좁히지 않기 때문이다). 깃발이 없으면 그 두 번째 신호로 맵 준비 한도가
        ///   처음부터 다시 시작돼, 한도가 실제보다 길어진다.
        /// </summary>
        private bool _rematchPreparing;

        /// <summary>
        /// 재경기가 성립하지 못했는가. 상태 줄 문구를 <see cref="RematchFailedCountdownFormat"/> 으로
        /// 가르는 데 쓴다(실패 3경로가 모두 이 깃발 하나를 세운다).
        ///
        /// <para>
        /// 🔴 <b>내리는 자리 — 다시 시도할 때 반드시 내려야 한다.</b> 사용자가 실패 문구를 보고
        /// 「다시하기」를 다시 눌렀는데(상대가 살아 있으면 가능하다) 이 깃발이 남아 있으면
        /// <b>이미 다시 시도하는 중인데도 화면은 계속 실패를 말한다.</b>
        /// <list type="bullet">
        ///   <item><see cref="SetupRematchButton"/> 이 설치한 onClick — 버튼을 다시 누른 순간.</item>
        ///   <item><see cref="EnterRematchPreparingState"/> — 준비 상태로 다시 들어가는 순간.</item>
        ///   <item><c>Initialize()</c> — 새 판이 시작될 때(<c>_opponentLeft</c> 와 같은 자리).</item>
        /// </list>
        /// </para>
        /// </summary>
        private bool _rematchFailed;

        /// <summary>
        /// 재경기가 실패했다면 <b>그 사유</b>. 상태 줄이 실패 문구 <b>둘 중 하나</b>를 고를 때만 쓴다
        /// (규칙 18 — 가르는 것은 화면 문구 하나뿐이다).
        ///
        /// <para>
        /// 🔴 <b>가르는 기준은 「연결 끊김인가 아닌가」 하나뿐이다.</b>
        /// <see cref="RematchMapFailureCause.OpponentDisconnected"/> 면
        /// <see cref="RematchFailedByOpponentLeftCountdownFormat"/>, 그 밖이면
        /// <see cref="RematchFailedCountdownFormat"/> 을 쓴다.
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>기본값이 <see cref="RematchMapFailureCause.Unknown"/> 인 것이 중요하다.</b>
        /// 실패 3경로 중 <b>둘은 사유를 모른다</b> — ① 수락을 서버로 못 보낸 경우와
        /// ③ 준비 한도가 만료된 경우다. 모를 때 「상대가 나갔다」로 단정하면 거짓이 될 수 있으므로
        /// <b>중립적인 기본 문구</b>로 떨어진다(CLAUDE.md 규칙 10 — 추정 금지).
        /// </para>
        ///
        /// ⚠️ 내리는(되돌리는) 자리는 <see cref="_rematchFailed"/> 와 완전히 같다 —
        ///    깃발이 내려가면 이 값도 의미가 없어지므로 함께 되돌린다.
        /// </summary>
        private RematchMapFailureCause _rematchFailureCause = RematchMapFailureCause.Unknown;

        /// <summary>
        /// 「재경기 준비 중」 상태의 맵 준비 한도 코루틴. null 이면 돌고 있지 않다.
        ///
        /// 🔴 <b>이 시계는 아무것도 판정하지 않는다.</b> 승패도, 상대 이탈도, 연결 종료도 하지 않는다.
        ///    <b>하는 일은 상태 줄 문구를 평시로 되돌리는 것 하나뿐</b>이다
        ///    (되돌리는 방법은 자동 복귀 카운트다운을 전체 길이로 다시 시작하는 것이며,
        ///     그 카운트다운이 매 초 평시 문구를 다시 쓴다 — 규칙 M-3).
        ///    🔴 <b>나중에 여기에 판정을 얹지 말 것.</b> 이 화면에서 무엇을 판정하는 시계는
        ///    이미 두 개(자동 복귀 카운트다운 · 결과 화면 이탈 감시) 있고, 세 번째가 생기면
        ///    같은 사건을 서로 다른 시계가 두 번 결론 내리게 된다.
        /// </summary>
        private Coroutine _rematchPreparingCoroutine;

        /// <summary>
        /// 재경기 요청 수락/거절 팝업. 공통 UI 규칙 D-6 에서 「응답 전 요청자 이탈」일 때 닫을 대상이다.
        ///
        /// [초급자용 설명] 왜 <c>[SerializeField]</c> 로 Inspector 배선을 하지 않는가
        ///   <c>[SerializeField]</c> 로 두면 그 참조가 씬 파일(Game.unity)에 저장되고,
        ///   <b>씬을 손으로 배선하지 않으면 런타임에 null</b> 이 되어 규칙이 조용히 성립하지 않는다.
        ///   이 팝업은 Game 씬의 <c>[UI]</c> 아래에 이미 활성 상태로 놓여 있으므로
        ///   런타임 탐색(<c>FindFirstObjectByType</c>)만으로 확실히 찾을 수 있다.
        ///   바로 위 <c>_networkGameManager</c> 가 쓰는 것과 같은 탐색 방식이며,
        ///   덕분에 이 단계는 <b>씬 작업이 필요 없다.</b>
        ///
        ///   탐색은 필요한 순간(이탈 통보 수신)에 1회만 하고 이 필드에 캐시한다 —
        ///   <c>FindFirstObjectByType</c> 은 씬 전체를 훑는 무거운 호출이라 매번 부르지 않는다.
        /// </summary>
        private RematchRequestPopup _rematchRequestPopup;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. 이벤트 구독 + 패널 숨김.
        /// LoadMap() 때마다 호출되므로 이전 구독을 정리 후 재구독.
        /// </summary>
        public void Initialize()
        {
            // NetworkGameManager 자동 탐색 (Inspector에 연결 안 된 경우)
            // NetworkGameManager는 DontDestroyOnLoad 오브젝트라 Game 씬 인스펙터에서 연결이 불가능하다.
            // 따라서 미연결 시 런타임에 직접 탐색해야 한다. 미탐색이면 로비 복귀 시 NGO Shutdown이 누락되어
            // 두 번째 매칭에서 "Cannot start Host while an instance is already running" 에러가 발생한다.
            // (LobbyUI.cs와 동일한 패턴)
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            // 멀티플레이에서 NGM을 못 찾으면 로비 복귀 시 네트워크 종료가 누락되므로 경고만 남긴다.
            // (싱글플레이는 NGM이 없는 것이 정상이므로 NetworkContext.IsNetworkActive로 분기)
            if (_networkGameManager == null && NetworkContext.IsNetworkActive)
            {
                // [개발] Warn + 개발 — LobbyUI · LobbyRootView 와 같은 사건, 같은 판정이다.
                //   FindFirstObjectByType 이 null 이라는 것은 "씬(또는 DontDestroyOnLoad)에 없다" 는 뜻이고,
                //   그건 배치 누락이라는 설정 오류다(1.3 원칙 3 단서) → Warn + 개발.
                GameLog.Dev.Warn("Network", nameof(GameEndUI),
                                 "NetworkGameManager 를 찾을 수 없다 — 로비 복귀 시 NGO Shutdown 이 누락될 수 있다");
            }

            // 이전 구독 정리 (재시작 시 중복 방지)
            _gameEndSubscription?.Dispose();
            _rematchAvailableSubscription?.Dispose();
            _rematchDeclinedSubscription?.Dispose();
            _localRematchAcceptedSubscription?.Dispose();
            _rematchAcceptedSubscription?.Dispose();
            _rematchMapFailedSubscription?.Dispose();
            _rematchStartingSubscription?.Dispose();
            _backToLobbySubscription?.Dispose();
            _opponentLeftSubscription?.Dispose();

            // 새 판이 시작되므로 지난 판의 이탈 상태를 지운다.
            // (이 플래그가 남아 있으면 새 결과 화면이 처음부터 이탈 문구로 뜬다)
            _opponentLeft = false;

            // 🔴 [실패 깃발 내리는 자리] 지난 판의 실패가 새 결과 화면까지 따라오면 안 된다.
            _rematchFailed = false;
            // 깃발과 **같은 자리에서** 사유도 되돌린다(지난 판의 사유가 새 화면 문구를 고르면 안 된다).
            _rematchFailureCause = RematchMapFailureCause.Unknown;

            // 지난 판의 「재경기 준비 중」 시계가 남아 있으면 여기서 확실히 끊는다.
            // (재경기로 씬이 재로드되면 이 컴포넌트도 새로 만들어지지만,
            //  같은 씬에서 Initialize 가 다시 불리는 경로도 있으므로 방어해 둔다)
            StopRematchPreparingLimit();

            // 게임 종료 이벤트 구독
            // NetworkGameEndController가 GameEvents.OnGameEnd를 발행하므로 싱글/멀티 모두 본 구독으로 ShowResult 진입한다.
            _gameEndSubscription = GameEvents.OnGameEnd
                .Subscribe(OnGameEnd);

            // 멀티 재경기 버튼 활성화 신호 구독.
            _rematchAvailableSubscription = GameEvents.OnNetworkRematchAvailable
                .Subscribe(e => SetupRematchButton(e.IsRandomMatch));

            // 멀티 재경기 거절 신호 구독 — 버튼/카운트다운 상태 복원.
            _rematchDeclinedSubscription = GameEvents.OnNetworkRematchDeclined
                .Subscribe(_ => RestoreRematchButton());

            // [재경기 맵 준비 실패] 새 맵을 못 만들었거나 전송·검증이 실패했다.
            //   씬은 재로드되지 않고 결과 화면이 그대로 있으므로, 여기서는 **눌리기 전 상태로
            //   되돌리는 것**만 한다(GameSystemRules_UI.md 「공통 UI 규칙」 규칙 M-3 —
            //   "결과 화면의 기존 선택지를 모두 복원한다").
            //   🔴 거절과 같은 메서드(RestoreRematchButton)를 부르지만 **이벤트 채널은 다르다** —
            //      거절과 실패는 원인도 다르고 나중에 붙을 안내 문구도 다르다.
            //   ⚠️ 실패를 알리는 팝업·문구는 여전히 이번 범위가 아니다(규칙 M-3 의 "표시 문구 미정").
            //   ✅ **[2026-09-22 갱신]** 종전 주석은 "자동 로비 복귀 카운트다운 재시작도 이번 범위가
            //      아니다" 였으나 **더 이상 사실이 아니다** — 규칙 M-3 의 「전체 길이로 다시 시작」을
            //      이번에 구현했다. 처리는 OnRematchMapFailed() 가 모아서 한다.
            //   🔴 [2026-09-24] 이 채널이 **실패 사유를 함께 나른다**(규칙 18 — 사유에 따라 문구를
            //      가른다). 채널을 새로 만들지 않고 **기존 채널의 타입만** 바뀌었으므로
            //      🔴 구독은 여전히 **이 한 곳뿐**이다(늘거나 줄지 않았다).
            _rematchMapFailedSubscription = GameEvents.OnNetworkRematchMapFailed
                .Subscribe(e => OnRematchMapFailed(e.Cause));

            // ----------------------------------------------------------------
            // [재경기 수락 — 수락한 쪽] 🔴 자기 버튼 입력을 듣는다. 서버 응답이 아니다.
            //
            //   RematchRequestPopup 의 「수락」 버튼이 OnLocalRematchAccepted 를 발행하고,
            //   NetworkGameEndController 도 같은 이벤트를 구독해 ServerRpc 로 바꿔 보낸다.
            //   즉 이 구독은 **ServerRpc 와 나란히** 달리는 것이지 그 결과를 기다리는 것이 아니다.
            //
            //   🔴 왜 서버 응답을 기다리지 않는가:
            //     수락한 사람이 Client 이고 Host 가 이미 떠났다면 ServerRpc 는 받을 서버가 없어
            //     **증발한다.** 그러면 응답이 영영 오지 않으므로, 응답을 기다리게 만든 화면은
            //     수락을 눌러도 아무 반응이 없다. 반대로 수락한 사람이 Host 면 ServerRpc 가
            //     자기 자신에게 가므로 곧바로 실행된다 — 즉 **역할에 따라 화면이 갈린다.**
            //     플레이어는 자기가 Host 인지 Client 인지 알 수 없으므로 그 자체로 결함이다.
            // ----------------------------------------------------------------
            _localRematchAcceptedSubscription = GameEvents.OnLocalRematchAccepted
                .Subscribe(_ => EnterRematchPreparingState());

            // ----------------------------------------------------------------
            // [재경기 수락 — 요청한 쪽] 서버가 수락을 접수했다는 통보를 받고 상태를 바꾼다.
            //
            //   요청자는 달리 「상대가 수락했다」를 알 길이 없다. 그래서 지금까지는 수락이
            //   성사됐는데도 자기 자동 복귀 카운트다운(60초)이 만료되면 맵 준비 도중에
            //   혼자 로비로 나가 버렸다.
            //
            //   ⚠️ 이 통보는 양쪽 모두에게 간다 — 수락한 쪽도 받는다. 수락한 쪽은 위 구독으로
            //      이미 같은 상태에 들어가 있으므로 EnterRematchPreparingState 의 멱등 가드가
            //      두 번째 신호를 그냥 무시한다.
            // ----------------------------------------------------------------
            _rematchAcceptedSubscription = GameEvents.OnNetworkRematchAccepted
                .Subscribe(_ => EnterRematchPreparingState());

            // [재경기 로딩] 서버가 재경기를 시작(씬 재로드 직전)하면 모든 클라이언트가
            // 전역 로딩 인디케이터를 표시한다. 씬이 재로드되어 새 GameBootstrapper.LoadMap()이
            // 완료되면 자동으로 꺼진다(UI 규칙 L-3).
            //
            // 🔴 [맵 준비 한도 정지 자리 ②/④] 재경기가 실제로 시작되면 「재경기 준비 중」 구간은
            //    끝났으므로 맵 준비 한도를 멈춘다. 멈추지 않으면 씬 재로드 뒤까지 살아남을 수는 없지만,
            //    재로드 직전 몇 프레임 동안 **아무것도 지키지 않는 죽은 시계**로 남는다.
            //    ⚠️ 아래 ShowLoading 호출(기존 동작)은 한 글자도 바꾸지 않았다.
            _rematchStartingSubscription = GameEvents.OnNetworkRematchStarting
                .Subscribe(_ =>
                {
                    StopRematchPreparingLimit();
                    UIManager.Instance?.ShowLoading(true, "재경기 준비 중...");
                });

            // [로비 복귀] NetworkGameManager.BackToLobby(Infrastructure)가 NGO Shutdown 완료 후
            //   본 이벤트를 발행한다. 씬 전환(SceneLoader)은 Presentation 책임이므로
            //   Infrastructure가 직접 호출하지 않고 이 구독을 통해 처리한다(UI 규칙 L-4).
            _backToLobbySubscription = GameEvents.OnNetworkBackToLobby
                .Subscribe(sceneName => SceneLoader.Load(sceneName));

            // [상대 이탈] 결과 화면이 떠 있는 동안 상대가 사라졌다는 통보.
            //   발행자는 Infrastructure 의 NetworkGameEndController 이며 두 갈래가 이 한 채널을 쓴다 —
            //     ① 상대가 로비 복귀 버튼으로 스스로 나간 정상 퇴장(상대가 나가기 직전에 보낸 통보)
            //     ② 30초 동안 상대의 신호가 끊긴 무반응 이탈(내 쪽에서 직접 판정)
            //   🔴 화면이 해야 할 일은 두 갈래가 완전히 같으므로(규칙 17) 구독도 하나만 둔다.
            //   🔴 규칙 D-6(응답 전 요청자 이탈)도 이 구독 하나에 이어 붙였다 — OnOpponentLeft() 안에서
            //      처리하며, 같은 신호에 구독을 새로 만들지 않는다(처리 순서를 보장할 수 없게 된다).
            _opponentLeftSubscription = GameEvents.OnNetworkOpponentLeft
                .Subscribe(_ => OnOpponentLeft());

            // 다시하기 버튼 이벤트 (중복 등록 방지)
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(OnRestartClicked);
                _restartButton.onClick.AddListener(OnRestartClicked);
            }

            // 로비 복귀 버튼 이벤트 (중복 등록 방지)
            if (_backToLobbyButton != null)
            {
                _backToLobbyButton.onClick.RemoveListener(OnBackToLobbyClicked);
                _backToLobbyButton.onClick.AddListener(OnBackToLobbyClicked);
            }

            // 패널 숨김
            Hide();
        }

        private void OnDestroy()
        {
            StopCountdown();

            // 🔴 [맵 준비 한도 정지 자리 ④/④] 오브젝트가 사라질 때 시계도 함께 끊는다.
            //    파괴되면 코루틴도 함께 멈추지만, 「켜는 자리마다 끄는 자리를 짝지어 둔다」는
            //    규칙을 지켜야 나중에 이 코루틴이 DontDestroyOnLoad 쪽으로 옮겨져도 안전하다.
            StopRematchPreparingLimit();

            _gameEndSubscription?.Dispose();
            _rematchAvailableSubscription?.Dispose();
            _rematchDeclinedSubscription?.Dispose();
            _localRematchAcceptedSubscription?.Dispose();
            _rematchAcceptedSubscription?.Dispose();
            _rematchMapFailedSubscription?.Dispose();
            _rematchStartingSubscription?.Dispose();
            _backToLobbySubscription?.Dispose();
            _opponentLeftSubscription?.Dispose();
        }

        // ====================================================================
        // IGameUI 구현
        // ====================================================================

        /// <summary>
        /// 게임 시작/재시작 시 호출.
        /// 재경기(Rematch) 시 이전 게임의 결과 패널이 남아있는 것을 숨김.
        /// </summary>
        public void OnGameStarted()
        {
            Hide();
        }

        // OnGameEnded(): GameUIManager에서 호출 제외 대상.
        // GameEndUI는 게임 종료 시 "표시"되어야 하는 UI이므로 닫기 동작이 아닌
        // 자체 OnGameEnd 이벤트 구독으로 결과 패널을 표시함.
        // IGameUI의 default 빈 구현을 그대로 사용.

        // ====================================================================
        // 이벤트 핸들러
        // ====================================================================

        /// <summary>
        /// 게임 종료 시 호출. 승리/패배 텍스트 표시 + 게임 일시정지.
        ///
        /// 싱글/멀티 모두 GameEvents.OnGameEnd 발행으로 본 핸들러에서 처리된다.
        /// 로컬 팀 비교는 LocalPlayerTeam.Current(멀티)로 처리하되, 싱글은 LocalPlayerTeam이
        /// 설정되지 않으므로 Blue 고정 폴백을 사용한다.
        /// </summary>
        private void OnGameEnd(GameEndEvent e)
        {
            if (_panel == null) return;

            // 멀티플레이면 LocalPlayerTeam.Current(자신의 팀)과 비교, 싱글이면 Blue 기본.
            TeamId localTeam = NetworkContext.IsNetworkActive ? LocalPlayerTeam.Current : TeamId.Blue;
            bool isWin = (e.Winner == localTeam);

            if (_resultText != null)
            {
                _resultText.text = isWin ? "승리!" : "패배!";
                // 색상 설정 에셋이 연결되어 있으면 그 값을, 아니면 합리적인 폴백 색을 사용한다.
                // (Inspector 미연결 시에도 시각적으로 승/패 구분이 가능하도록 안전 가드.)
                if (_colorConfig != null)
                    _resultText.color = isWin ? _colorConfig.winColor : _colorConfig.loseColor;
                else
                    _resultText.color = isWin ? new Color(0.3f, 0.5f, 0.9f) : new Color(0.9f, 0.3f, 0.3f);
            }

            _panel?.Show();
            // 게임 일시정지
            Time.timeScale = 0f;

            // 자동 로비 복귀 카운트다운 시작 (평시 = 전체 길이)
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(_autoReturnSeconds));
        }

        /// <summary>
        /// 다시하기 버튼 클릭 시 게임 재시작.
        /// </summary>
        private void OnRestartClicked()
        {
            StopCountdown();

            // 시간 복원
            Time.timeScale = 1f;

            // 패널 닫기
            Hide();

            // 맵 재로드 (전체 재초기화)
            if (_bootstrapper != null)
                _bootstrapper.LoadMap(HexOrientation.FlatTop);
        }

        /// <summary>
        /// "로비로 돌아가기" 버튼 클릭 처리.
        /// 네트워크 활성 여부와 무관하게 로컬에서 독립 처리.
        /// </summary>
        private void OnBackToLobbyClicked()
        {
            ReturnToLobby();
        }

        // ====================================================================
        // 공개 메서드
        // ====================================================================

        /// <summary>
        /// 패널 숨김. 재시작 시 GameBootstrapper에서도 호출.
        /// </summary>
        public void Hide()
        {
            StopCountdown();
            _panel?.Hide();
        }

        /// <summary>
        /// 네트워크 모드에서 서버 권위의 승자 팀과 로컬 팀을 비교하여 결과 표시.
        /// 싱글플레이 OnGameEnd는 Blue 팀 고정이지만,
        /// 멀티플레이에서는 Red 팀 플레이어도 자신의 승/패를 올바르게 확인해야 함.
        /// </summary>
        /// <param name="winnerTeam">서버에서 확정된 승리 팀.</param>
        /// <param name="localTeam">이 클라이언트의 로컬 팀.</param>
        public void ShowResult(TeamId winnerTeam, TeamId localTeam)
        {
            if (_panel == null) return;

            bool isWin = (winnerTeam == localTeam);

            if (_resultText != null)
            {
                _resultText.text = isWin ? "승리!" : "패배!";
                // 색상 설정 에셋이 연결되어 있으면 그 값을, 아니면 합리적인 폴백 색을 사용한다.
                // (Inspector 미연결 시에도 시각적으로 승/패 구분이 가능하도록 안전 가드.)
                if (_colorConfig != null)
                    _resultText.color = isWin ? _colorConfig.winColor : _colorConfig.loseColor;
                else
                    _resultText.color = isWin ? new Color(0.3f, 0.5f, 0.9f) : new Color(0.9f, 0.3f, 0.3f);
            }

            _panel?.Show();
            // 게임 일시정지
            Time.timeScale = 0f;

            // 자동 로비 복귀 카운트다운 시작 (평시 = 전체 길이)
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(_autoReturnSeconds));
        }

        // ====================================================================
        // 로비 복귀 + 카운트다운
        // ====================================================================

        /// <summary>
        /// 로비로 즉시 복귀. 네트워크 활성 여부와 무관하게 로컬 독립 처리.
        ///
        /// 네트워크 활성 시:
        ///   NetworkGameManager.BackToLobby() 호출 — 내부에서 OnClientConnectedCallback 해제,
        ///   Heartbeat 정지, Lobby 퇴장, Shutdown, 씬 전환을 순서대로 안전하게 처리.
        /// 싱글플레이 시:
        ///   SceneLoader.Load(SceneLoader.Lobby) 호출 (로딩 인디케이터 자동 표시).
        ///
        /// 이전에는 NetworkManager.Singleton.Shutdown()을 직접 호출했으나,
        /// Unity.Netcode 직접 의존을 제거하고자 Application 레이어 NetworkContext로 분기하고,
        /// 실제 Shutdown 책임은 Infrastructure 레이어 NetworkGameManager로 위임.
        /// </summary>
        private void ReturnToLobby()
        {
            StopCountdown();
            Time.timeScale = 1f;
            Hide();

            // 🔴 결과 화면 위에 떠 있었을지 모르는 공통 팝업(확인 팝업 · 알림 팝업)을 닫는다.
            //
            //   왜 필요한가:
            //     공통 팝업의 실체는 UIManager 가 들고 있는 ConfirmPopup 하나이고,
            //     UIManager 는 DontDestroyOnLoad 라 <b>씬이 바뀌어도 파괴되지 않는다.</b>
            //     그래서 닫지 않은 채 로비로 넘어가면 결과 화면에서 띄운 팝업이
            //     로비 화면 위에 그대로 남는다(반투명 배경까지 함께).
            //     버튼으로 닫는 경로는 ConfirmPopup.OnConfirmClicked() 가 Hide() 를 먼저 부르므로
            //     이미 닫히지만, <b>카운트다운 만료로 자동 복귀하는 경로에는 닫는 사람이 없었다.</b>
            //
            //   🔴 여기(ReturnToLobby) 한 곳에만 넣는 이유 — 이 메서드는
            //     자동 복귀(CountdownCoroutine 만료)와 버튼 클릭(OnBackToLobbyClicked)이
            //     <b>둘 다 반드시 지나가는 길목</b>이다. 그래서 여기 한 줄이면 두 경로가 모두 덮인다.
            //     CountdownCoroutine 쪽에 같은 호출을 또 넣지 말 것 —
            //     두 곳에 흩어지면 나중에 한쪽만 고쳐져 경로별로 동작이 갈린다.
            //
            //   🔴 아래 ShowLoading(true) 보다 <b>반드시 앞</b>이어야 한다 —
            //     ConfirmPopup.Hide() 안에서 UIManager.HideBlockingOverlay() 가 불리는데,
            //     이 프로젝트의 BlockingOverlay 는 <b>참조 카운터</b>로 중첩을 관리한다.
            //     화면 점유를 정리하는 일(팝업 닫기)은 새 점유를 만드는 일(로딩 표시)보다
            //     먼저 끝나 있어야 두 점유가 섞이지 않는다. 순서를 바꾸지 말 것.
            //
            //   조건 없이 무조건 호출한다. ReturnToLobby 는 「이 화면을 떠난다」는 뜻이고,
            //   떠 있지 않았다면 아무 일도 일어나지 않으므로 검사할 이유가 없다.
            UIManager.Instance?.HideConfirmOrAlert();

            // 로비 복귀는 씬 전환(멀티는 네트워크 종료 포함)이 일어나므로
            // 그 사이 사용자가 멈춘 화면을 보지 않도록 전역 로딩 인디케이터를 띄운다.
            // 로딩을 끄는 책임은 목적지 씬(Lobby)의 LobbyRootView 초기화 완료 시점이 담당한다(UI 규칙 L-3).
            UIManager.Instance?.ShowLoading(true, "로비로 이동 중...");

            // 멀티플레이 활성 + NGM 주입되어 있으면 NGM에 위임 (BackToLobby가 내부에서 씬 전환까지 처리)
            if (NetworkContext.IsNetworkActive && _networkGameManager != null)
            {
                _networkGameManager.BackToLobby("Lobby");
                return;
            }

            // 싱글플레이 또는 NGM 미연결: 씬 전환만 수행.
            // 위에서 이미 ShowLoading(true)를 호출했지만, SceneLoader.Load 가 다시 호출해도
            // 같은 메시지로 갱신될 뿐이므로 부작용은 없다.
            SceneLoader.Load(SceneLoader.Lobby, "로비로 이동 중...");
        }

        /// <summary>
        /// 진행 중인 카운트다운 코루틴 정지 및 텍스트 초기화.
        /// </summary>
        private void StopCountdown()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }
            if (_countdownText != null)
                _countdownText.text = "";
        }

        /// <summary>
        /// 자동 로비 복귀 카운트다운. WaitForSecondsRealtime 사용 (timeScale=0 대응).
        ///
        /// 문구는 <b>상대 이탈 여부에 따라 이 메서드 안의 분기 하나로만</b> 갈린다(공통 UI 규칙 D-1).
        /// 이탈 사실과 남은 시간은 사용자에게 한 덩어리의 정보라, 별도 팝업을 새로 띄우지 않고
        /// <b>이미 떠 있는 타이머 텍스트 자리</b>를 그대로 쓴다.
        /// </summary>
        /// <param name="totalSeconds">
        /// 카운트다운 전체 길이(초). 평시에는 <c>_autoReturnSeconds</c>(규칙 D-4 의 60초),
        /// 상대 이탈 판정 시에는 <c>OpponentLeftCountdownSeconds</c>(30초)가 들어온다.
        /// </param>
        private IEnumerator CountdownCoroutine(float totalSeconds)
        {
            float remaining = totalSeconds;
            while (remaining > 0f)
            {
                if (_countdownText != null)
                {
                    int seconds = Mathf.CeilToInt(remaining);

                    // 🔴 문구는 여기 **한 자리의 3분기**로만 갈린다. 우선순위는 「이탈 > 실패 > 평시」다.
                    //
                    //   [초급자용 설명] 왜 이탈이 실패를 덮는가
                    //     두 사실은 동시에 성립할 수 있다. 재경기 맵 준비 한도가 먼저 지나
                    //     「재경기를 시작할 수 없습니다」가 떠 있는 동안, 상대 이탈 판정(30초)이
                    //     그 뒤에 내려오는 순서가 실제로 존재한다.
                    //     그때 화면에 남아야 하는 것은 **더 나중에 밝혀진, 더 근본적인 사실**이다 —
                    //     「재경기가 안 됐다」의 **이유가** 「상대가 떠났다」이기 때문이다.
                    //     결과보다 원인을 보여 주는 쪽이 사용자에게 쓸모 있으므로 이탈이 이긴다.
                    //     (반대로 두면 상대가 떠난 것을 알려 줄 기회가 영영 사라진다.)
                    //
                    //   🔴 문구 상수는 각각 코드에 한 곳에만 있다
                    //      (OpponentLeftCountdownFormat / RematchFailedCountdownFormat /
                    //       RematchFailedByOpponentLeftCountdownFormat).
                    //      같은 문구를 두 곳에 적으면 한쪽만 고쳐졌을 때 화면에 두 표현이 섞여 나온다.
                    //
                    //   🔴 [2026-09-24] 「실패」 안에서 문구가 **둘로** 갈린다 — 규칙 18.
                    //      기준은 **실패 사유가 연결 끊김인가 아닌가 하나뿐**이다.
                    //      「맵 준비가 실패했다」와 「상대가 나갔다」는 사용자가 **할 수 있는 일이 다르다** —
                    //      앞쪽은 다시 시도할 여지가 있고 뒤쪽은 없다. 그래서 문구만 가른다.
                    //      ⚠️ 우선순위(이탈 > 실패 > 평시)는 **바뀌지 않았다.** 갈라진 것은
                    //         「실패」 분기 **안쪽**뿐이다.
                    //      ⚠️ 사유를 **모르면** 기본 문구를 쓴다 — _rematchFailureCause 주석 참조.
                    _countdownText.text =
                          _opponentLeft  ? string.Format(OpponentLeftCountdownFormat, seconds)
                        : _rematchFailed ? string.Format(SelectRematchFailedFormat(), seconds)
                        :                  $"{seconds}초 후 로비로 돌아갑니다.";
                }
                yield return new WaitForSecondsRealtime(1f);
                remaining -= 1f;
            }
            ReturnToLobby();
        }

        /// <summary>
        /// 실패 상태의 상태 줄 문구 <b>형식 문자열</b>을 고른다(규칙 18).
        ///
        /// <para>
        /// 🔴 <b>이 메서드는 「문구를 고르는」 일만 한다 — 화면에 쓰지 않는다.</b>
        /// 실제로 쓰는 자리는 <see cref="CountdownCoroutine"/> 안의 그 한 줄뿐이며,
        /// <b>문구 분기가 한 자리에 모여 있다는 성질을 깨지 않으려고</b> 형식 문자열만 돌려준다.
        /// (본문에 <c>if</c> 를 늘어놓는 대신 삼항 연산자 한 줄로 유지하기 위한 분리다.)
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>가르는 기준은 하나뿐</b> — 사유가 연결 끊김인가.
        /// 그 밖이면(사유를 <b>모르는 경우도 포함해</b>) 기존 실패 문구를 그대로 쓴다.
        /// </para>
        /// </summary>
        /// <returns>남은 초를 <c>{0}</c> 에 채워 쓸 형식 문자열</returns>
        private string SelectRematchFailedFormat()
        {
            return _rematchFailureCause == RematchMapFailureCause.OpponentDisconnected
                ? RematchFailedByOpponentLeftCountdownFormat
                : RematchFailedCountdownFormat;
        }

        // ====================================================================
        // 상대 이탈 반영 (공통 UI 규칙 D-1 · D-2 · D-3 · D-4 · D-6)
        // ====================================================================

        /// <summary>
        /// 결과 화면이 떠 있는 동안 상대가 사라졌다는 통보를 받았을 때의 화면 처리.
        ///
        /// 하는 일은 넷이다.
        ///   1. <b>재경기 버튼 비활성화</b>(규칙 D-2) — 상대가 없으니 요청해도 받을 사람이 없다.
        ///      누를 수 있게 두면 응답이 영영 오지 않는 요청으로 사용자를 또 기다리게 만든다.
        ///   2. <b>카운트다운을 30초로 다시 시작</b>(규칙 D-4).
        ///   3. <b>타이머 문구 교체</b>(규칙 D-1) — 문구 자체는 아래 플래그를 보고
        ///      <c>CountdownCoroutine</c> 이 매 초 갱신하므로 여기서 직접 쓰지 않는다.
        ///   4. <b>응답 전이던 재경기 요청 팝업 정리 + 알림 팝업</b>(규칙 D-6) —
        ///      <see cref="HandleUnansweredRematchRequestOnOpponentLeft"/> 가 담당한다.
        ///      🔴 이 이벤트 구독은 <b>단계 7 에서 만든 한 건뿐</b>이며, 규칙 D-6 을 위해
        ///      구독을 새로 늘리지 않고 <b>이 핸들러에 이어 붙였다.</b> 같은 이탈 신호에
        ///      구독이 둘이면 처리 순서를 아무도 보장할 수 없어, 팝업을 닫는 쪽과
        ///      알림을 띄우는 쪽이 뒤바뀔 수 있다.
        ///
        /// 🔴 <b>로비 복귀 버튼은 여기서 끄지 않는다</b>(규칙 D-3). 이탈 판정 뒤에도 켜 둔다 —
        ///    두 버튼이 동시에 꺼지면 사용자가 스스로 화면을 빠져나갈 방법이 없어진다.
        ///    이탈로 꺼지는 것은 재경기 버튼뿐이다.
        /// </summary>
        private void OnOpponentLeft()
        {
            // 같은 사건이 두 번 도달할 수 있다 — 상대의 정상 퇴장 통보가 먼저 오고,
            // 그 직후 내 쪽 무반응 감시가 같은 침묵을 이탈로 판정하는 경우가 그렇다.
            // 두 번째 신호로 카운트다운이 30초부터 다시 시작되면 화면이 영영 안 닫힐 수 있으니 막는다.
            if (_opponentLeft) return;
            _opponentLeft = true;

            // 🔴 [맵 준비 한도 정지 자리 ③/④] 「재경기 준비 중」 시계를 멈춘다.
            //   상대가 없는 것이 확정됐으니 맵이 준비되기를 기다릴 이유가 사라졌다.
            //   멈추지 않으면 바로 아래에서 시작하는 30초 카운트다운과 나란히 돌다가,
            //   그 한도가 만료되면 그 시계가 카운트다운을 전체 길이로 갈아엎으려 든다
            //   (그쪽 RestartCountdownFromFullLength 에도 _opponentLeft 가드가 있지만,
            //    **켜 둔 시계는 반드시 끈다**는 원칙을 지키기 위해 여기서도 명시적으로 끊는다).
            StopRematchPreparingLimit();

            // 규칙 D-2 — 재경기 버튼 비활성화 + 버튼 문구를 「다시하기」로 되돌린다.
            //
            //   [초급자용 설명] 왜 문구를 되돌리는가
            //     재경기를 요청한 직후라면 버튼 문구가 「요청 중...」으로 바뀌어 있다.
            //     그대로 두면 상대가 이미 떠났는데도 화면에는 **아직 요청이 진행 중인 것처럼**
            //     보이는 문구가 굳어 버린다. 그래서 문구는 원래대로 돌리고,
            //     **버튼 자체는 끈 채로 둔다**(받을 사람이 없으므로 — 규칙 D-2).
            //
            //   🔴 RestoreRematchButton() 은 위에서 _opponentLeft 를 true 로 만든 뒤에 부른다.
            //      그 메서드가 그 깃발을 보고 interactable 을 false 로 두기 때문이다.
            //      순서를 바꾸면 버튼이 다시 켜져 규칙 D-2 를 어긴다.
            RestoreRematchButton();

            // 규칙 D-4 — 카운트다운 30초 재시작. 이때부터 문구가 이탈 문구로 바뀐다(규칙 D-1).
            RestartCountdownForOpponentLeft();

            // 규칙 D-6 — 재경기 요청에 응답하기 전에 요청자가 이탈한 경우의 추가 처리.
            //   🔴 여기서 별도의 타이머를 만들지 않는다. 규칙 D-6 3항은 이 시점의 카운트다운이
            //      「규칙 D-4 의 이탈 재시작과 같은 시점·같은 값(30초)」이라고 정하고 있으며,
            //      그 재시작은 바로 위 한 줄이 이미 끝냈다. 알림 팝업에 자기 전용 타이머를 붙이면
            //      같은 30초를 세는 시계가 두 개가 되어 한쪽을 고칠 때 다른 쪽이 조용히 어긋난다.
            HandleUnansweredRematchRequestOnOpponentLeft();
        }

        /// <summary>
        /// 상대 이탈 판정 시 자동 로비 복귀 카운트다운을 <b>30초로</b> 다시 시작한다(규칙 D-4).
        ///
        /// ⚠️ <b>이탈 전용 진입점이다.</b> 규칙 M-3(재경기 맵 준비 실패)의 카운트다운 재시작은
        ///    <b>전체 길이</b>로 다시 시작하는 별개의 규정이므로 이 메서드를 쓰지 않는다
        ///    (그쪽은 아직 미구현이며, 구현할 때도 이 메서드를 재사용하지 말 것 —
        ///     길이가 달라 한쪽을 고치면 다른 쪽이 조용히 망가진다).
        /// </summary>
        private void RestartCountdownForOpponentLeft()
        {
            // 돌고 있던 카운트다운(평시 60초)을 멈추고 30초로 새로 시작한다.
            // StopCountdown() 이 텍스트를 비우지만, 아래 코루틴이 첫 yield 전에 다시 채우므로
            // 사용자에게는 빈 텍스트가 보이지 않는다.
            StopCountdown();
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(OpponentLeftCountdownSeconds));
        }

        // ====================================================================
        // 「재경기 준비 중」 상태 (2026-09-22)
        // 배경·판단 기준은 이 파일 위쪽 RematchPreparingStatusText 절의 주석 참조.
        // ====================================================================

        /// <summary>
        /// 「재경기 준비 중」 상태로 들어간다 — <b>자동 로비 복귀 타이머를 멈추고</b>
        /// 상태 줄을 <see cref="RematchPreparingStatusText"/> 로 바꾼 뒤 맵 준비 한도를 건다.
        ///
        /// <para>
        /// 부르는 곳은 둘이고 <b>두 쪽이 서로 다른 신호로 들어온다</b>(이것이 이번 수정의 핵심이다).
        /// <list type="bullet">
        ///   <item><b>수락한 쪽</b> — <c>OnLocalRematchAccepted</c>(자기 버튼 입력). 서버 응답을 기다리지 않는다.</item>
        ///   <item><b>요청한 쪽</b> — <c>OnNetworkRematchAccepted</c>(서버의 수락 접수 통보).</item>
        /// </list>
        /// 자세한 이유는 <c>Initialize()</c> 의 두 구독 위 주석에 적어 두었다.
        /// </para>
        ///
        /// 🔴 <b>「로비로」 버튼은 여기서 끄지 않는다</b>(규칙 D-3). 맵이 만들어지기를 기다리는 동안에도
        ///    사용자는 언제든 스스로 나갈 수 있어야 한다. 같은 이유로 입력을 막는
        ///    로딩 화면도 띄우지 않는다.
        /// </summary>
        private void EnterRematchPreparingState()
        {
            // 멱등 — 같은 상태에 두 번 들어가지 않는다.
            //   수락한 쪽은 버튼 입력으로 한 번, 서버 통보로 또 한 번 이 메서드에 도달한다.
            //   막지 않으면 맵 준비 한도가 두 번째 신호에서 처음부터 다시 시작된다.
            if (_rematchPreparing) return;

            // 상대가 이미 떠난 것으로 판정된 뒤라면 이 상태로 들어가지 않는다.
            //   그 화면은 이미 이탈 문구 + 30초 카운트다운(규칙 D-1 · D-4)을 보여 주고 있고,
            //   여기서 덮으면 사용자가 방금 읽은 「상대방이 떠났습니다」가 사라져 버린다.
            if (_opponentLeft) return;

            _rematchPreparing = true;

            // 🔴 [실패 깃발 내리는 자리] 지난 시도가 실패했더라도 이제 다시 준비에 들어갔다.
            //    내리지 않으면 아래에서 세울 「재경기 준비 중...」 뒤에도 실패 문구가 되살아난다.
            _rematchFailed = false;
            _rematchFailureCause = RematchMapFailureCause.Unknown;

            // 🔴 자동 로비 복귀 카운트다운을 멈춘다 — 「시스템을 기다리는」 구간이기 때문이다.
            //    StopCountdown() 이 상태 줄을 빈 문자열로 만들므로, 곧바로 아래에서 다시 채운다.
            StopCountdown();

            if (_countdownText != null)
                _countdownText.text = RematchPreparingStatusText;

            _rematchPreparingCoroutine = StartCoroutine(RematchPreparingLimitCoroutine());
        }

        /// <summary>
        /// 「재경기 준비 중」 상태의 맵 준비 한도를 멈춘다. 돌고 있지 않으면 아무 일도 하지 않는다(멱등).
        ///
        /// <para>
        /// 🔴 <b>부르는 자리 네 곳</b> — 하나라도 빠지면 <b>아무것도 지키지 않는 죽은 시계</b>가 남는다.
        /// <list type="number">
        ///   <item>맵 준비 실패 통보 수신 — <see cref="OnRematchMapFailed"/></item>
        ///   <item>재경기 시작 통보 수신 — <c>Initialize()</c> 의 <c>OnNetworkRematchStarting</c> 구독</item>
        ///   <item>상대 이탈 판정 — <see cref="OnOpponentLeft"/></item>
        ///   <item>오브젝트 파괴 — <c>OnDestroy()</c></item>
        /// </list>
        /// (여기에 더해 <c>Initialize()</c> 의 재초기화 자리에서도 한 번 정리한다.)
        /// </para>
        /// </summary>
        private void StopRematchPreparingLimit()
        {
            _rematchPreparing = false;

            if (_rematchPreparingCoroutine != null)
            {
                StopCoroutine(_rematchPreparingCoroutine);
                _rematchPreparingCoroutine = null;
            }
        }

        /// <summary>
        /// 「재경기 준비 중」 상태의 <b>맵 준비 한도</b>.
        ///
        /// <para>
        /// 🔴 <b>이 시계는 아무것도 판정하지 않는다</b> — 승패도, 상대 이탈도, 연결 종료도 하지 않는다.
        /// <b>하는 일은 상태 줄 문구를 평시로 되돌리는 것 하나뿐</b>이다.
        /// 🔴 <b>나중에 여기에 판정을 얹지 말 것.</b> 판정은 서버(맵 전송 계층)와
        /// 결과 화면 이탈 감시가 각각 이미 하고 있으며, 세 번째 시계가 같은 사건에
        /// 다른 결론을 내리기 시작하면 원인을 추적할 수 없게 된다.
        /// </para>
        ///
        /// <para>
        /// [초급자용 설명] 한도를 왜 <b>곱셈</b>으로 구하는가 —
        /// 서버가 재경기용 새 맵을 보내고 <b>응답을 기다리는 창 하나</b>의 길이가
        /// <see cref="NetworkMapTransfer.TransferTimeoutSeconds"/> 이고,
        /// 그 창이 <b>몇 개</b>인지는 <b>최초 전송 1회 + 재전송
        /// <see cref="NetworkMapTransfer.MaxResendCount"/> 회</b>로 정해진다.
        /// 그래서 <c>창의 길이 × 창의 개수</c> 가 「실패가 확정될 때까지 걸릴 수 있는 최대 시간」이다.
        /// (식의 <c>+ 1</c> 이 바로 그 <b>최초 전송분</b>이다 — 재전송 횟수에는 최초 전송이 포함되지 않는다.)
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>한 창만 재면 정상 경로에서 거짓 신호가 난다.</b> 상대가 멀쩡히 있어도 패킷이 한 번
        /// 유실되면 재전송이 일어나 맵 준비가 한 창을 넘긴다. 그때 한도를 한 창으로 잡아 두면
        /// 「재경기 준비 중...」이 먼저 사라져 평시 카운트다운으로 돌아갔다가, 잠시 뒤 갑자기
        /// 씬이 재로드된다 — <b>화면이 튀고 사용자에게 거짓말을 한 셈</b>이 된다.
        /// </para>
        ///
        /// <para>
        /// ✅ <b>덤으로 두 역할의 시간이 정확히 맞는다</b> — 수락자가 <b>Host</b> 면 맵 준비가 실제로
        /// 시작돼 timeout + 재전송을 다 쓰고 실패 통보가 오고, 수락자가 <b>Client</b>(Host 가 이미 떠남)면
        /// 수락 ServerRpc 가 증발해 맵 준비가 시작조차 못 한 채 이 한도가 만료된다.
        /// <b>두 경우의 대기 시간이 같아진다</b> — 그것이 이번 수정의 목적이다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>숫자를 여기에 베껴 쓰지 않고 두 상수를 직접 참조한다.</b> 규칙 16 이 나중에
        /// 재전송 횟수를 바꾸면 이 한도가 <b>저절로</b> 따라가야 한다. 계산 결과를 숫자로 박아 두면
        /// 그때 아무도 모르게 어긋난다.
        /// </para>
        /// </summary>
        private IEnumerator RematchPreparingLimitCoroutine()
        {
            // 한 창의 길이 × 창의 개수(최초 전송 1회 + 재전송 MaxResendCount 회).
            // 🔴 계산 결과를 숫자로 쓰지 않는다 — 위 XML 주석의 마지막 문단 참조.
            float limitSeconds = NetworkMapTransfer.TransferTimeoutSeconds
                                 * (NetworkMapTransfer.MaxResendCount + 1);

            // timeScale = 0 인 결과 화면이므로 Realtime 을 쓴다(이 파일 CountdownCoroutine 과 같은 이유).
            yield return new WaitForSecondsRealtime(limitSeconds);

            // 🔴 자기 핸들을 먼저 비운다. 아래 EnterRematchFailedState 가 부르는
            //    StopRematchPreparingLimit() 이 StopCoroutine(자기 자신)을 실행하면
            //    이 코루틴이 그 자리에서 끊겨 뒤 코드가 실행되지 않는다.
            _rematchPreparingCoroutine = null;

            // [실패 3경로 중 ③] 한도가 지났는데 아무 통보도 오지 않았다.
            //   🔴 사유는 Unknown 이다 — **무엇이 실패했는지 통보가 오지 않았다는 뜻** 그대로다.
            //      상대가 나갔을 수도 있지만 내 회선 문제일 수도 있어 구분할 근거가 없으므로,
            //      「상대가 나갔다」로 단정하지 않고 중립적인 기본 문구를 쓴다
            //      (CLAUDE.md 규칙 10 — 추정 금지).
            EnterRematchFailedState(RematchMapFailureCause.Unknown);
        }

        /// <summary>
        /// 자동 로비 복귀 카운트다운을 <b>전체 길이</b>(<c>_autoReturnSeconds</c>)로 다시 시작한다 —
        /// 공통 UI 규칙 M-3 「자동 로비 복귀 countdown 을 전체 길이로 다시 시작한다」.
        ///
        /// ⚠️ <b>규칙 D-4 의 이탈 재시작(30초)과 섞지 않는다.</b> 재시작 시점도 길이도 다르므로
        ///    진입점을 공유하지 않는다(이탈 전용 진입점은 <see cref="RestartCountdownForOpponentLeft"/>).
        /// </summary>
        private void RestartCountdownFromFullLength()
        {
            // 🔴 상대 이탈이 이미 판정된 뒤라면 손대지 않는다.
            //    그 화면은 규칙 D-4 에 따라 이미 30초로 다시 시작해 이탈 문구를 보여 주고 있다.
            //    여기서 전체 길이로 덮으면 사용자가 읽을 시간을 보장하려던 그 규정이 조용히 뒤집힌다.
            if (_opponentLeft) return;

            StopCountdown();
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(_autoReturnSeconds));
        }

        /// <summary>
        /// 재경기용 새 맵 준비·전송·검증이 실패했다는 통보를 받았을 때의 화면 처리.
        ///
        /// <para>하는 일은 셋이다(공통 UI 규칙 M-3).</para>
        /// <list type="number">
        ///   <item>🔴 <b>[맵 준비 한도 정지 자리 ①/④]</b> 「재경기 준비 중」 시계를 멈춘다.</item>
        ///   <item><b>결과 화면의 기존 선택지를 복원</b>한다 — <see cref="RestoreRematchButton"/>.</item>
        ///   <item><b>자동 로비 복귀 카운트다운을 전체 길이로 다시 시작</b>한다.</item>
        /// </list>
        ///
        /// ⚠️ <b>실패를 알리는 팝업은 이번 범위가 아니다</b> — 규칙 M-3 은 팝업을 띄우기로 확정했지만
        ///    <b>표시 문구와 버튼 라벨이 아직 미정</b>이다. 정해지기 전에 문구를 지어내지 않는다.
        /// </summary>
        /// <param name="cause">
        /// 서버(또는 로컬 발행 경로)가 실어 보낸 실패 사유. 🔴 <b>화면 문구를 고르는 데만</b> 쓴다.
        /// ⚠️ 경로 ① 은 사유를 모르므로 <see cref="RematchMapFailureCause.Unknown"/> 이 들어온다.
        /// </param>
        private void OnRematchMapFailed(RematchMapFailureCause cause)
        {
            // [실패 3경로 중 ① · ②] 이 한 채널이 두 경로를 나른다.
            //   ② 서버가 맵 준비 실패를 ClientRpc 로 통보한 경우(원래 용도)
            //   ① 수락을 서버로 아예 못 보내 컨트롤러가 **로컬에서** 같은 채널을 발행한 경우
            //      (RPC 를 보낼 수 없어서 생긴 실패라 RPC 로 알릴 수 없다 —
            //       NetworkGameEndController.SendAcceptRematchSafely 의 catch 주석 참조)
            //   🔴 두 경로의 **처리**를 가르지 않는다. 되돌리는 절차는 완전히 같다(규칙 18).
            //      갈리는 것은 상태 줄 문구 하나뿐이고, 그 판정 재료가 이 cause 다.
            EnterRematchFailedState(cause);
        }

        /// <summary>
        /// 🔴 <b>재경기 실패 3경로의 공통 진입점.</b> 실패를 화면에 드러내고 결과 화면을 되살린다.
        ///
        /// <para>들어오는 길 셋 — <b>세 곳에 같은 코드를 쓰지 않고 전부 이리로 모은다.</b></para>
        /// <list type="number">
        ///   <item>수락을 서버로 <b>못 보냈다</b> → 컨트롤러가 로컬 발행 → <see cref="OnRematchMapFailed"/></item>
        ///   <item>서버의 <b>맵 준비 실패 통보</b> → <see cref="OnRematchMapFailed"/></item>
        ///   <item><b>준비 한도 만료</b> → <see cref="RematchPreparingLimitCoroutine"/></item>
        /// </list>
        ///
        /// <para>하는 일은 넷이다.</para>
        /// <list type="number">
        ///   <item><b>준비 한도 시계를 멈춘다</b> — 이미 결론이 났으므로 지킬 것이 없다.</item>
        ///   <item><b>실패 깃발을 세운다</b> → 상태 줄이 <see cref="RematchFailedCountdownFormat"/> 로 바뀐다.</item>
        ///   <item><b>재경기 버튼을 복원한다</b> — 상대가 살아 있으면 다시 시도할 수 있어야 한다.
        ///         상대가 이탈한 뒤라면 <see cref="RestoreRematchButton"/> 이 문구만 되돌리고 비활성으로 남긴다(규칙 D-2).</item>
        ///   <item><b>카운트다운을 전체 길이로 다시 시작한다</b>(규칙 M-3).</item>
        /// </list>
        ///
        /// 🔴 <b>순서가 중요하다</b> — 깃발을 세우는 것이 카운트다운 재시작보다 <b>앞</b>이어야 한다.
        ///    카운트다운 코루틴은 매 초 이 깃발을 읽어 문구를 고르므로, 뒤에 세우면 첫 1초 동안
        ///    평시 문구가 보였다가 바뀌어 화면이 깜빡인다.
        ///
        /// 🔴 <b>팝업을 띄우지 않는다</b> — 이유는 <see cref="RematchFailedCountdownFormat"/> 주석 참조.
        /// </summary>
        /// <param name="cause">
        /// 왜 실패했는가. 🔴 <b>하는 일은 상태 줄 문구를 고르는 것 하나뿐</b>이다(규칙 18) —
        /// 되돌리는 절차(버튼 복원 · 카운트다운 재시작 · 한도 정지)는 사유와 무관하게 완전히 같다.
        /// ⚠️ 실패 3경로 중 <b>둘은 사유를 모르므로</b>
        /// <see cref="RematchMapFailureCause.Unknown"/> 을 넘긴다 — 그때는 중립적인 기본 문구가 뜬다.
        /// </param>
        private void EnterRematchFailedState(RematchMapFailureCause cause)
        {
            StopRematchPreparingLimit();

            // 🔴 사유를 깃발보다 **먼저** 보관한다. 아래 카운트다운 코루틴이 매 초 이 값을 읽어
            //    문구를 고르므로, 깃발이 먼저 서면 첫 1초 동안 잘못된 문구가 보일 수 있다.
            //    (깃발이 카운트다운 재시작보다 앞이어야 하는 것과 같은 이유의 순서다.)
            _rematchFailureCause = cause;

            _rematchFailed = true;

            RestoreRematchButton();
            RestartCountdownFromFullLength();
        }

        /// <summary>
        /// 공통 UI 규칙 D-6 — <b>재경기 요청에 응답하기 전에 요청자가 이탈한 경우</b>의 처리.
        ///
        /// <para>
        /// 규칙이 정한 순서 그대로 두 가지를 한다.
        ///   1. 떠 있던 <b>재경기 요청 팝업을 닫는다.</b>
        ///   2. <b>알림 팝업</b>(규칙 D-5)을 띄운다 — 타이틀 · 본문 · 버튼 1개.
        /// </para>
        ///
        /// <para>
        /// [초급자용 설명] 왜 팝업을 닫는 것이 먼저인가
        ///   요청을 보낸 상대가 이미 떠났으니 수락을 눌러도 그 응답이 닿을 곳이 없다.
        ///   팝업을 그대로 두면 사용자는 아직 고를 수 있다고 믿고 수락을 누르고,
        ///   아무 반응도 없는 화면 앞에서 또 기다린다. 그래서 <b>닿을 곳이 없어진 선택지를
        ///   먼저 치우고</b>, 그 다음에 무슨 일이 있었는지 알린다.
        /// </para>
        ///
        /// <para>
        /// [초급자용 설명] 왜 별도의 타이머를 만들지 않는가
        ///   규칙 D-6 3항이 「이 시점의 자동 로비 복귀 카운트다운은 30초이며 여기에 별도의
        ///   타이머를 두지 않는다」고 못 박고 있다. 그 30초는 호출부(<see cref="OnOpponentLeft"/>)의
        ///   <see cref="RestartCountdownForOpponentLeft"/> 가 이미 다시 시작했다.
        ///   그러므로 이 알림 팝업은 <b>스스로 닫히지 않는다</b> — 사용자가 버튼을 누르면 로비로 가고,
        ///   누르지 않아도 그 카운트다운이 만료되면 로비로 간다(규칙 D-4). 어느 쪽이든 갇히지 않는다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>요청 팝업이 떠 있지 않았다면 알림 팝업도 띄우지 않는다.</b> 규칙 D-6 은 제목 그대로
        ///    「요청 팝업이 떠 있는 상태에서 응답하기 전에」로 적용 범위가 한정돼 있고, 그 밖의
        ///    이탈은 규칙 D-1 의 타이머 텍스트가 알리기로 정해져 있다(규칙 D-1 은 「별도 팝업을 새로
        ///    띄우지 않고 이미 떠 있는 타이머 텍스트 자리를 쓴다」고 명시한다). 요청이 없던 이탈에도
        ///    팝업을 띄우면 그 규정과 어긋난다.
        /// </para>
        /// </summary>
        private void HandleUnansweredRematchRequestOnOpponentLeft()
        {
            // 씬에 놓인 팝업을 필요한 순간에 1회만 찾아 캐시한다(Inspector 배선 불필요).
            if (_rematchRequestPopup == null)
                _rematchRequestPopup = FindFirstObjectByType<RematchRequestPopup>();

            if (_rematchRequestPopup == null)
            {
                // [개발] Warn + 개발 — 씬에서 팝업을 찾지 못한 것은 배치 누락이라는 설정 오류다
                //   (위 NetworkGameManager 탐색 실패와 같은 사건·같은 판정).
                //   이 로그는 이탈 통보를 받은 순간에만 찍히고, 중복 통보는 호출부의
                //   _opponentLeft 가드가 막으므로 한 경기에 최대 1줄이다.
                GameLog.Dev.Warn("UI", nameof(GameEndUI),
                                 "RematchRequestPopup 을 찾을 수 없다 — 규칙 D-6 의 요청 팝업 닫기와 알림 팝업을 건너뛴다");
                return;
            }

            // 1항 — 응답 대기 중이던 요청 팝업을 닫는다.
            //   떠 있지 않았다면 이번 이탈은 규칙 D-6 의 상황이 아니므로 여기서 끝낸다
            //   (이탈 사실은 규칙 D-1 의 타이머 텍스트가 이미 알리고 있다).
            if (!_rematchRequestPopup.TryCloseUnansweredRequest())
                return;

            // 2항 — 알림 팝업(규칙 D-5). 타이틀 + 본문 + 버튼 1개, 배경 탭으로 닫히지 않는 모달이다.
            //   🔴 호출은 규칙 D-5 가 정한 null-safe 패턴을 쓴다 — Game 씬에 직접 진입하면
            //      Login 씬에서 만들어지는 UIManager 가 없어 Instance 가 null 일 수 있다.
            //   🔴 버튼 콜백은 <b>로비 복귀 버튼과 똑같은 핸들러</b>(OnBackToLobbyClicked)를 그대로 넘긴다.
            //      새 이동 경로를 만들면 카운트다운 정지 · timeScale 복원 · 로딩 인디케이터 ·
            //      멀티에서의 NGO 종료 위임 중 하나라도 빠질 수 있고, 두 경로가 갈리면
            //      한쪽만 고쳐졌을 때 버튼에 따라 동작이 달라진다.
            UIManager.Instance?.ShowAlert(OpponentLeftAlertTitle,
                                          OpponentLeftAlertMessage,
                                          OnBackToLobbyClicked,
                                          OpponentLeftAlertButtonLabel);
        }

        /// <summary>
        /// 멀티플레이 재경기 버튼 설정. 게임 모드에 따라 버튼 동작 분기.
        /// 랜덤매칭: 다시하기 버튼 숨김 (로비 복귀만 가능) — 현재는 동일 동작으로 유지.
        /// 커스텀게임: 재경기 요청 발행 + 요청 중 상태 표시.
        ///
        /// GameEvents.OnNetworkRematchAvailable 구독 시 자동 호출되며, 재경기 요청은
        /// OnLocalRematchRequested 이벤트로 발행해 컨트롤러가 ServerRpc로 변환한다.
        /// </summary>
        /// <param name="isRandomMatch">랜덤 매칭 여부. 현재 미사용 — 랜덤/커스텀 모두 동일 동작.</param>
        public void SetupRematchButton(bool isRandomMatch)
        {
            if (_restartButton == null) return;

            // 멀티플레이: 재경기 요청 이벤트 발행 (랜덤/커스텀 동일)
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() =>
            {
                // 버튼 텍스트 → "요청 중..." + 비활성화
                if (_restartButtonText != null)
                    _restartButtonText.text = "요청 중...";
                _restartButton.interactable = false;

                // 🔴 [실패 깃발 내리는 자리] 지난 시도가 실패해 상태 줄에 「재경기를 시작할 수
                //    없습니다」가 떠 있을 수 있다. 다시 누른 이 순간부터는 그 말이 거짓이 되므로
                //    깃발을 내려 상태 줄을 평시 문구로 돌려놓는다.
                //    (다음 초에 카운트다운 코루틴이 새 문구를 쓴다 — 이 깃발이 그 분기를 가른다.)
                _rematchFailed = false;
                _rematchFailureCause = RematchMapFailureCause.Unknown;

                // 🔴 로비 복귀 버튼은 여기서 끄지 않는다 (공통 UI 규칙 D-3 「로비 복귀 버튼은 항상 활성」).
                //    종전에는 "재경기 응답 대기 중"이라는 이유로 이 버튼도 함께 껐는데,
                //    그러면 다시하기 버튼(바로 위에서 꺼진다)과 로비 버튼이 동시에 잠겨
                //    사용자가 스스로 결과 화면을 빠져나갈 방법이 사라진다.
                //    상대가 응답하지 않으면 자동 복귀 카운트다운이 끝날 때까지 갇히게 되고,
                //    상대가 이미 나가 버린 경우에는 응답 자체가 영영 오지 않는다.
                //    기다리는 것은 사용자의 선택이어야 하므로 나가는 길은 항상 열어 둔다.

                // 컨트롤러 직접 호출 대신 이벤트 발행 → NetworkGameEndController가 구독 후 ServerRpc 전송
                GameEvents.OnLocalRematchRequested.OnNext(Unit.Default);
            });
        }

        /// <summary>
        /// 재경기 버튼 상태 복원 — <b>문구는 언제나 되돌리고, 다시 누를 수 있는지는 상황에 따라 가른다.</b>
        ///
        /// <para>부르는 곳은 셋이다.</para>
        /// <list type="bullet">
        ///   <item>재경기 <b>거절</b> 통보 — 다시 요청할 수 있어야 한다.</item>
        ///   <item>재경기 <b>맵 준비 실패</b> 통보 — 규칙 M-3 「기존 선택지를 모두 복원한다」.</item>
        ///   <item><b>상대 이탈</b> 판정 — 문구만 되돌리고 버튼은 꺼 둔다(규칙 D-2).</item>
        /// </list>
        ///
        /// <para>
        /// 🔴 <b>[2026-09-22 수정] 문구 복원을 <c>_opponentLeft</c> 가드 밖으로 꺼냈다.</b>
        /// 종전에는 문구와 <c>interactable</c> 이 같은 <c>if</c> 안에 묶여 있어서,
        /// 상대 이탈이 판정되면 <b>문구까지 함께 묶여 되돌아오지 않았다</b> —
        /// 「요청 중...」이 화면에 굳은 채 남았다.
        /// 🔴 <b>가드 자체는 없애지 않았다.</b> 버튼을 다시 누를 수 있게 만들면 받을 사람이 없는
        /// 요청을 또 보내게 되어 규칙 D-2 위반이다. <b>문구만 되돌리고 버튼은 끈 채로 둔다.</b>
        /// </para>
        /// </summary>
        public void RestoreRematchButton()
        {
            // ① 문구는 상황과 무관하게 언제나 「다시하기」로 되돌린다.
            //    화면에 남은 「요청 중...」은 이미 끝난 요청을 가리키는 거짓 정보이기 때문이다.
            if (_restartButtonText != null)
                _restartButtonText.text = "다시하기";

            // ② 다시 누를 수 있는지는 「상대가 아직 있는가」로 가른다(규칙 D-2).
            //    상대가 떠난 뒤에는 꺼 둔 버튼이 다시 켜지면 안 된다 — 거절·맵 실패 통보가
            //    이탈 통보보다 늦게 도착하는 경우가 실제로 있다.
            if (_restartButton != null)
                _restartButton.interactable = !_opponentLeft;

            // ③ 로비 복귀 버튼은 언제나 켠다(규칙 D-3 「어떤 상태에서도 비활성화하지 않는다」).
            if (_backToLobbyButton != null)
                _backToLobbyButton.interactable = true;
        }
    }
}
