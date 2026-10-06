// ============================================================================
// GameUIManager.cs
// 게임 UI 생명주기 매니저.
//
// 역할:
//   1. IGameUI를 구현한 UI 컴포넌트들을 등록 관리
//   2. GameEvents의 게임 상태 이벤트를 구독 — 구독하는 것은 게임 종료와 게임 시작,
//      이 두 가지뿐이다(2026-10-05 실측: Initialize() 의 구독이 정확히 두 개다).
//   3. 이벤트 발생 시 등록된 UI에 적절한 콜백 호출
//      ⚠️ "등록된 전부"가 되는 것은 시작 콜백뿐이다 — 종료 콜백에는 일부러 건너뛰는 UI가
//         하나 있다(바로 아래 「게임 종료 시 동작」 참조).
//
// 게임 종료 시 동작:
//   등록된 모든 IGameUI.OnGameEnded() 호출.
//   단, GameEndUI는 게임 종료 시 "표시되어야 하는" UI이므로 OnGameEnded() 호출에서 제외.
//   (GameEndUI의 표시는 GameEndUI 자체의 OnGameEnd 이벤트 구독으로 처리)
//
// 게임 시작 시 동작:
//   등록된 모든 IGameUI.OnGameStarted() 호출.
//   재시작(Rematch) 시 이전 게임의 UI 잔재를 정리하는 용도.
//
// 씬 배치 (2026-10-05, 스크립트 guid 로 씬·프리팹 전수 검색해 확인한 실측):
//   이 컴포넌트는 두 씬에 각각 하나씩 놓여 있고, 둘 다 [Managers] 아래의
//   Transform 과 이 컴포넌트만 가진 빈 GameObject 다.
//     - 전투 씬: _gameEndUI 가 결과 화면 컴포넌트에 연결돼 있고, 조합 루트가 맵 로드 때
//       Register()/Initialize() 를 불러 준다. 실제로 동작하는 쪽은 이것 하나다.
//     - 로비 씬: _gameEndUI 가 비어 있고, Initialize() 를 불러 주는 곳이 아무데도 없다
//       (조합 루트는 전투 씬에만 있다). 즉 구독을 걸지 않아 아무 일도 하지 않는다.
//   ⚠️ 그래서 "_gameEndUI 연결 필수"는 "지금 둘 다 연결돼 있다"가 아니라
//      "동작시키려면 연결해야 한다"는 전제다. 로비 쪽을 쓰게 되면 먼저 연결해야 한다.
//
// 구독 중복 방지:
//   Initialize()는 LoadMap() 때마다 호출될 수 있으므로,
//   기존 구독을 Dispose 후 새로 구독하여 중복 방지.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour), UniRx 의존.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 게임 UI 생명주기 매니저.
    /// IGameUI 구현체들을 등록하고, 게임 상태 변경 시 일괄 콜백 호출.
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("제외 대상")]
        [Tooltip("게임 종료 UI. 게임 종료 시 OnGameEnded() 호출 대상에서 제외됨 (게임 종료 시 표시되어야 하므로).")]
        [SerializeField] private GameEndUI _gameEndUI;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// 등록된 IGameUI 구현체 목록.
        /// Register()로 추가, 중복 등록 방지.
        /// </summary>
        private readonly List<IGameUI> _uis = new List<IGameUI>();

        /// <summary>
        /// 현재 활성화된 이벤트 구독들.
        /// Initialize() 재호출 시 기존 구독을 정리하기 위해 CompositeDisposable 사용.
        /// </summary>
        private CompositeDisposable _subscriptions;

        // ====================================================================
        // 등록
        // ====================================================================

        /// <summary>
        /// IGameUI 구현체를 등록.
        /// 이미 등록된 인스턴스는 무시 (중복 등록 방지).
        /// null 참조도 무시.
        /// </summary>
        /// <param name="ui">등록할 UI 컴포넌트. IGameUI를 구현해야 함.</param>
        public void Register(IGameUI ui)
        {
            // Inspector 배선이 비어 있어 참조가 아예 없는 경우를 목록에 넣지 않으려는 방어.
            //
            // 🔴 2026-10-05 실측 정정 — 종전 주석은 "유니티 컴포넌트라서 유니티의 특수한 null
            //    비교가 필요하다"고 적었으나, 그 특수 비교는 이 자리에서 일어나지 않는다.
            //    매개변수의 정적 타입이 클래스가 아니라 인터페이스여서, 같음 비교가 유니티 쪽
            //    연산자 오버로드로 해석되지 않고 평범한 참조 비교로 컴파일되기 때문이다.
            //    (넘어오는 실제 값이 유니티 컴포넌트인지 여부와 무관하다.)
            //
            // ⚠️ 그래서 "이미 파괴된 컴포넌트"는 이 가드를 그대로 통과한다 — 유니티에서 파괴된
            //    컴포넌트의 참조는 비워지는 것이 아니라 "파괴됨" 표시만 달린 채 남기 때문이다.
            //    통과하면 아래 목록에 남고, 나중에 콜백을 부를 때 그 콜백이 유니티 쪽 멤버(트랜스폼·
            //    게임오브젝트 등)를 건드리는 순간 예외가 된다(콜백이 순수 C# 필드만 만지면 예외 없이 지나간다).
            //    ✅ 지금 실제로 터지지는 않는다 — 등록을 호출하는 곳은 조합 루트 한 군데뿐이고
            //       거기서 넘기는 것은 전부 전투 씬에 살아 있는 오브젝트다.
            //    🔴 가드를 보강하는 것은 동작 변경이므로 이 주석 정리 작업의 범위가 아니다(보고만 함).
            if (ui == null) return;

            // 이미 등록된 경우 중복 방지
            if (_uis.Contains(ui)) return;

            _uis.Add(ui);
        }

        // ====================================================================
        // 초기화 (이벤트 구독)
        // ====================================================================

        /// <summary>
        /// GameEvents 이벤트 구독 시작.
        /// LoadMap() 때마다 호출될 수 있으므로, 기존 구독을 정리 후 새로 구독.
        /// </summary>
        public void Initialize()
        {
            // 기존 구독 정리 (재초기화 시 중복 구독 방지)
            _subscriptions?.Dispose();
            _subscriptions = new CompositeDisposable();

            // 게임 종료 이벤트 구독 → 등록된 UI에 OnGameEnded() 호출 (결과 화면 UI 하나는 제외 — NotifyGameEnded 참조)
            GameEvents.OnGameEnd
                .Subscribe(_ => NotifyGameEnded())
                .AddTo(_subscriptions);

            // 게임 시작 이벤트 구독 → 등록된 모든 UI에 OnGameStarted() 호출
            GameEvents.OnGameStarted
                .Subscribe(_ => NotifyGameStarted())
                .AddTo(_subscriptions);
        }

        // ====================================================================
        // 정리
        // ====================================================================

        /// <summary>
        /// MonoBehaviour 파괴 시 모든 이벤트 구독 해제.
        /// 씬 전환이나 오브젝트 파괴 시 메모리 누수 방지.
        /// </summary>
        private void OnDestroy()
        {
            _subscriptions?.Dispose();
            _subscriptions = null;
        }

        // ====================================================================
        // 이벤트 알림 (내부)
        // ====================================================================

        /// <summary>
        /// 등록된 모든 UI에 게임 종료를 알림.
        /// GameEndUI는 게임 종료 시 표시되어야 하므로 호출에서 제외.
        ///
        /// 싱글/멀티 모두 이 클래스가 Initialize()에서 걸어 둔 GameEvents.OnGameEnd 구독으로만
        /// 호출된다. 멀티의 클라이언트 쪽도 승자를 알리는 ClientRpc 안에서 같은 이벤트가 발행되고
        /// (서버 쪽은 그보다 앞서 이미 발행된 상태), 그 구독이 여기로 들어온다.
        ///
        /// ⚠️ 이 메서드는 public 이지만 이 클래스 밖에서 부르는 곳은 코드·씬·프리팹 전체에 없다.
        ///    종전 주석은 네트워크 종료 컨트롤러가 이 메서드를 직접 부른다고 적고 있었으나
        ///    그런 호출부는 존재하지 않으며, 따라서 "중복 호출" 상황도 생기지 않는다.
        /// </summary>
        public void NotifyGameEnded()
        {
            for (int i = 0; i < _uis.Count; i++)
            {
                IGameUI ui = _uis[i];

                // GameEndUI는 게임 종료 시 "표시"되어야 하는 UI이므로
                // OnGameEnded() (= 닫기/정리)를 호출하면 안 됨.
                // GameEndUI의 표시는 자체 OnGameEnd 이벤트 구독으로 처리됨.
                if (_gameEndUI != null && ReferenceEquals(ui, _gameEndUI))
                    continue;

                ui.OnGameEnded();
            }
        }

        /// <summary>
        /// 등록된 모든 UI에 게임 시작을 알림.
        /// 재시작(Rematch) 시 이전 게임의 UI 잔재를 정리하는 용도.
        /// </summary>
        private void NotifyGameStarted()
        {
            for (int i = 0; i < _uis.Count; i++)
            {
                _uis[i].OnGameStarted();
            }
        }
    }
}
