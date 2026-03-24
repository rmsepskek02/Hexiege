// ============================================================================
// GameUIManager.cs
// 게임 UI 생명주기 매니저.
//
// 역할:
//   1. IGameUI를 구현한 UI 컴포넌트들을 등록 관리
//   2. GameEvents의 게임 상태 이벤트(OnGameEnd, OnGameStarted 등)를 구독
//   3. 이벤트 발생 시 등록된 모든 UI에 적절한 콜백 호출
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
// 씬 배치:
//   [Managers] 하위에 빈 GameObject + 이 컴포넌트 부착.
//   Inspector에서 _gameEndUI 필드에 GameEndUI 참조 연결 필수.
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
            // null 체크 — MonoBehaviour는 Unity의 특수 null 비교 필요
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

            // 게임 종료 이벤트 구독 → 등록된 모든 UI에 OnGameEnded() 호출
            GameEvents.OnGameEnd
                .Subscribe(_ => NotifyGameEnded())
                .AddTo(_subscriptions);

            // 게임 시작 이벤트 구독 → 등록된 모든 UI에 OnGameStarted() 호출
            GameEvents.OnGameStarted
                .Subscribe(_ => NotifyGameStarted())
                .AddTo(_subscriptions);

            // 일시정지/재개 이벤트 구독 (확장용, 현재는 선언만 되어 있음)
            GameEvents.OnGamePaused
                .Subscribe(_ => NotifyGamePaused())
                .AddTo(_subscriptions);

            GameEvents.OnGameResumed
                .Subscribe(_ => NotifyGameResumed())
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
        /// 싱글플레이: GameEvents.OnGameEnd 구독에 의해 내부 호출.
        /// 멀티플레이: 클라이언트에서는 GameEvents.OnGameEnd가 발행되지 않으므로
        ///            NetworkGameEndController가 직접 호출.
        ///            (Host에서는 OnGameEnd 구독으로 이미 1회 호출되나, 중복 호출해도 안전
        ///             — 이미 닫힌 UI에 OnGameEnded()를 다시 호출해도 부작용 없음)
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

        /// <summary>
        /// 등록된 모든 UI에 일시정지를 알림. (확장용)
        /// </summary>
        private void NotifyGamePaused()
        {
            for (int i = 0; i < _uis.Count; i++)
            {
                _uis[i].OnGamePaused();
            }
        }

        /// <summary>
        /// 등록된 모든 UI에 재개를 알림. (확장용)
        /// </summary>
        private void NotifyGameResumed()
        {
            for (int i = 0; i < _uis.Count; i++)
            {
                _uis[i].OnGameResumed();
            }
        }
    }
}
