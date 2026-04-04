// ============================================================================
// RaceSelectionViewModel.cs
// 로비 종족 선택 UI의 ViewModel.
//
// 역할:
//   - 현재 선택된 종족 상태 관리 (SelectedRace)
//   - 종족명 텍스트 자동 변환 (SelectedRaceName)
//   - 좌우 슬라이드 커맨드 처리 (CmdPrev / CmdNext)
//   - 종족 변경 시 LocalPlayerRace에 즉시 저장
//
// 종족 순환: Human(0) → Spirit(1) → Nature(2) → Human(0) ...
//
// 순수 C# 클래스 — MonoBehaviour 아님.
// Presentation 레이어.
// ============================================================================

using System;
using UniRx;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 로비 종족 선택 ViewModel. 종족 상태와 좌우 슬라이드 커맨드 관리.
    /// </summary>
    public class RaceSelectionViewModel : IDisposable
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>종족 총 개수. RaceId enum 값이 0~(Count-1) 연속임을 전제.</summary>
        private const int RaceCount = 3;

        // ====================================================================
        // 상태 (View가 구독)
        // ====================================================================

        /// <summary>현재 선택된 종족. LocalPlayerRace에 저장된 값으로 초기화.</summary>
        public ReactiveProperty<RaceId> SelectedRace { get; } = new(LocalPlayerRace.Current);

        /// <summary>
        /// 현재 선택된 종족의 한글 이름.
        /// SelectedRace가 변경될 때 자동으로 "인간" / "정령" / "자연" 중 하나로 갱신.
        /// </summary>
        public IReadOnlyReactiveProperty<string> SelectedRaceName { get; }

        // ====================================================================
        // 커맨드 (View → ViewModel)
        // ====================================================================

        /// <summary>이전 종족으로 슬라이드 (왼쪽 화살표).</summary>
        public Subject<Unit> CmdPrev { get; } = new();

        /// <summary>다음 종족으로 슬라이드 (오른쪽 화살표).</summary>
        public Subject<Unit> CmdNext { get; } = new();

        // ====================================================================
        // 내부 상태
        // ====================================================================

        private readonly CompositeDisposable _disposables = new();

        // ====================================================================
        // 생성자
        // ====================================================================

        /// <summary>
        /// RaceSelectionViewModel 생성.
        /// CmdPrev/CmdNext 구독으로 종족 순환 처리,
        /// SelectedRace 변경 시 LocalPlayerRace 즉시 갱신.
        /// </summary>
        public RaceSelectionViewModel()
        {
            // SelectedRace → 한글 종족명 변환 파이프라인
            SelectedRaceName = SelectedRace
                .Select(race => race switch
                {
                    RaceId.Human  => "인간",
                    RaceId.Spirit => "정령",
                    RaceId.Nature => "자연",
                    _             => "알 수 없음"
                })
                .ToReadOnlyReactiveProperty();
            _disposables.Add(SelectedRaceName as IDisposable);

            // 왼쪽 화살표 → 이전 종족으로 순환 (Human → Nature → Spirit → Human ...)
            CmdPrev
                .Subscribe(_ =>
                {
                    int current = (int)SelectedRace.Value;
                    // +RaceCount-1 은 -1과 동일하지만 음수 나머지 방지
                    int prev = (current + RaceCount - 1) % RaceCount;
                    SelectedRace.Value = (RaceId)prev;
                })
                .AddTo(_disposables);

            // 오른쪽 화살표 → 다음 종족으로 순환 (Human → Spirit → Nature → Human ...)
            CmdNext
                .Subscribe(_ =>
                {
                    int current = (int)SelectedRace.Value;
                    int next = (current + 1) % RaceCount;
                    SelectedRace.Value = (RaceId)next;
                })
                .AddTo(_disposables);

            // 종족 변경 시 LocalPlayerRace에 즉시 저장
            // 초기값 포함 모든 변경을 LocalPlayerRace에 저장
            SelectedRace
                .Subscribe(race => LocalPlayerRace.Set(race))
                .AddTo(_disposables);
        }

        // ====================================================================
        // 정리
        // ====================================================================

        /// <summary>
        /// 모든 구독 해제. BattleRootView.Unbind()에서 호출.
        /// </summary>
        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
