// ============================================================================
// ToastKey.cs
// 토스트 메시지 식별용 열거형(enum).
//
// 역할:
//   - 게임에서 발생하는 "사용자에게 즉시 알려야 할 짧은 알림" 종류를 코드로 표현.
//   - 실제 표시 문구·표시 시간은 ToastMessageConfig.asset(ScriptableObject)에서
//     이 키와 매칭되어 결정됨.
//
// 왜 enum으로 분리하나요?
//   - 호출 코드(예: ProductionPanelUI)는 "어떤 상황인지"만 알면 되고,
//     "어떤 문구로 보여줄지"는 신경 쓰지 않아도 됩니다.
//   - 문구를 ScriptableObject에서 관리하므로 기획자가 코드 수정 없이 텍스트만 바꿀 수 있습니다.
//
// Presentation 레이어 — Unity 의존 없음(순수 enum).
// ============================================================================

namespace Hexiege.Presentation
{
    /// <summary>
    /// 토스트 메시지 종류. ToastMessageConfig에서 이 키별로 실제 텍스트/표시 시간을 정의한다.
    /// 새 토스트 메시지가 필요하면 이 enum에 항목을 추가하고
    /// ToastMessageConfig.asset에도 대응 항목을 추가해야 한다.
    /// </summary>
    public enum ToastKey
    {
        /// <summary>골드 부족 — 수동으로 유닛을 생산하려 했으나 골드가 모자랄 때.</summary>
        GoldInsufficient,

        /// <summary>인구 한계 도달 — 인구수가 가득 차 더 이상 유닛을 생산할 수 없을 때.</summary>
        PopulationFull,

        /// <summary>생산 대기열 가득 — 생산 큐가 이미 3개로 꽉 차 추가 등록을 거부할 때.</summary>
        ProductionQueueFull,

        /// <summary>업그레이드 필요 — 현재 건물 단계로는 생산할 수 없는 유닛을 탭했을 때.</summary>
        UpgradeRequired
    }
}
