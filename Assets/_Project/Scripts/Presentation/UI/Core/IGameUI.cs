// ============================================================================
// IGameUI.cs
// 게임 UI 생명주기 인터페이스.
//
// 역할:
//   게임의 주요 상태 변경(시작, 종료, 일시정지, 재개) 시
//   각 UI 컴포넌트가 자신의 상태를 적절히 처리할 수 있도록 콜백을 제공.
//
// 사용 방법:
//   1. UI MonoBehaviour에 IGameUI 인터페이스를 구현
//   2. GameUIManager에 Register()로 등록
//   3. 게임 상태 변경 시 GameUIManager가 등록된 모든 UI의 콜백을 호출
//
// default interface implementation (C# 8+):
//   모든 메서드에 기본 빈 구현이 있으므로,
//   필요한 콜백만 오버라이드하면 됨. 나머지는 아무것도 하지 않음.
//
// 예시:
//   public class MyUI : MonoBehaviour, IGameUI
//   {
//       // 게임 종료 시 팝업 닫기만 필요한 경우
//       public void OnGameEnded() { Close(); }
//       // OnGameStarted, OnGamePaused, OnGameResumed는 default 빈 구현 사용
//   }
//
// Presentation 레이어 — Unity 의존 없음 (순수 C# 인터페이스).
// ============================================================================

namespace Hexiege.Presentation
{
    /// <summary>
    /// 게임 UI 생명주기 인터페이스.
    /// 게임 상태 변경 시 각 UI가 자신의 상태를 정리/초기화할 수 있도록 콜백 제공.
    /// </summary>
    public interface IGameUI
    {
        /// <summary>
        /// 게임 시작 또는 재시작 시 호출.
        /// 맵 로드가 완전히 끝난 뒤 발행되므로, 이 시점에서 모든 시스템이 준비된 상태.
        /// 기본 구현: 아무것도 하지 않음.
        /// </summary>
        void OnGameStarted() { }

        /// <summary>
        /// 게임 종료 시 호출 (Castle 파괴로 승패 결정).
        /// 열려있는 팝업을 닫거나, 입력을 비활성화하는 등의 정리 작업에 사용.
        /// 기본 구현: 아무것도 하지 않음.
        /// </summary>
        void OnGameEnded() { }

        /// <summary>
        /// 게임 일시정지 시 호출 (확장용, 현재 미사용).
        /// 기본 구현: 아무것도 하지 않음.
        /// </summary>
        void OnGamePaused() { }

        /// <summary>
        /// 게임 재개 시 호출 (확장용, 현재 미사용).
        /// 기본 구현: 아무것도 하지 않음.
        /// </summary>
        void OnGameResumed() { }
    }
}
