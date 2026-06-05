// ============================================================================
// ProductionState.cs
// 배럭 하나의 생산 상태를 담는 데이터 클래스.
//
// 각 배럭(Barracks)마다 독립적인 ProductionState를 가짐.
// UnitProductionUseCase가 Dictionary<int, ProductionState>로 관리.
//
// ──────────────────────────────────────────────────────────────────────────
// [재작성 — 2026-04-19]
// 이전의 이중 큐 구조(ManualQueue + AutoEntries + AutoIndex)를 단일 PendingQueue로 통합.
// isNormalAutoState 계산에서 발생하던 반복 버그를 구조 자체에서 제거.
//
// 핵심 불변식:
//   PendingQueue[0] = 항상 슬롯1 표시 대상
//   PendingQueue[1] = 항상 슬롯2 표시 대상
//   → UI는 PendingQueue 앞 2개만 읽으면 되고, 취소도 PendingQueue[idx]를 그대로 제거.
//
// 슬롯 구성:
//   슬롯0 = CurrentProducing (현재 생산 중인 유닛)
//   슬롯1 = PendingQueue[0] (대기 1순위)
//   슬롯2 = PendingQueue[1] (대기 2순위)
//   PendingQueue[2..] = 자동 순환 대기 항목 (골드 미차감 상태일 수 있음)
//
// 생산 모드:
//   수동 모드: 사용자가 탭으로 직접 추가. IsAuto=false.
//   자동 모드: AutoTypes에 등록된 타입이 순환 반복 생산됨. IsAuto=true.
//   수동 추가 시 자동 모드 전체 해제(Rule 3). 단, 이미 골드 차감된 자동 항목은 수동으로 이관.
//
// 골드 차감 시점(Rule 5):
//   수동 항목: 등록(EnqueueUnit) 시 즉시 차감 → 항상 IsCharged=true.
//   자동 항목: 슬롯1/슬롯2에 표시되는 시점에 차감.
//              대기(PendingQueue[2+]) 상태에서는 IsCharged=false.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 생산 대기 큐의 한 항목을 표현하는 구조체.
    /// 수동/자동 구분(IsAuto)과 골드 차감 여부(IsCharged)를 타입과 함께 묶어 관리.
    /// </summary>
    public struct QueueSlot
    {
        /// <summary> 대기 중인 유닛 타입. </summary>
        public UnitType Type;

        /// <summary>
        /// 자동 등록 항목인지 여부.
        /// true  = 자동 모드에서 등록된 항목(ToggleAutoProduction 경로)
        /// false = 수동으로 등록된 항목(EnqueueUnit 경로)
        /// 자동→수동 "이관"(Rule 2)이 발생하면 false로 전환됨.
        /// </summary>
        public bool IsAuto;

        /// <summary>
        /// 골드가 이미 차감되었는지 여부.
        /// true  = 슬롯에 표시되어 골드 차감 완료 → 취소 시 환불 필요.
        /// false = 큐 뒤쪽 대기 중으로 골드 미차감 → 슬롯 진입 시 차감되고 true로 전환.
        /// 수동 항목은 생성과 동시에 true. 자동 항목은 슬롯 표시 가능 여부에 따라 결정.
        /// </summary>
        public bool IsCharged;

        public QueueSlot(UnitType type, bool isAuto, bool isCharged)
        {
            Type = type;
            IsAuto = isAuto;
            IsCharged = isCharged;
        }
    }

    public class ProductionState
    {
        /// <summary> 연결된 배럭의 BuildingData.Id. </summary>
        public int BarracksId { get; }

        /// <summary> 배럭 소속 팀. </summary>
        public TeamId Team { get; }

        /// <summary> 배럭 위치 (스폰 타일 탐색용). </summary>
        public HexCoord BarracksPosition { get; }

        /// <summary>
        /// 대기 큐. PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2.
        /// PendingQueue[2] 이상은 자동 순환 대기(골드 미차감 상태일 수 있음).
        /// 이 리스트의 앞 순서가 곧 슬롯에 표시되는 순서이므로 UI/취소 로직이 단순해짐.
        /// </summary>
        public List<QueueSlot> PendingQueue { get; } = new List<QueueSlot>();

        /// <summary>
        /// 자동 생산에 등록된 유닛 타입 목록.
        /// 순환 생산 대상과 자동 인디케이터 판정에 사용.
        /// 이 리스트가 비면 자동 모드 OFF(IsAutoMode=false).
        /// </summary>
        public List<UnitType> AutoTypes { get; } = new List<UnitType>();

        /// <summary>
        /// 자동 순환 인덱스. 다음에 PendingQueue에 추가할 AutoTypes 위치를 가리킴.
        /// (주의: 표시용 인덱스가 아님. 슬롯 표시는 PendingQueue가 직접 담당.)
        /// CompleteProduction에서 TryStartNext가 PendingQueue를 채울 때 사용.
        /// </summary>
        public int AutoCycleIndex { get; set; }

        /// <summary>
        /// 자동 모드 활성 여부 — AutoTypes.Count &gt; 0으로 자동 계산.
        /// 별도 필드로 두지 않고 AutoTypes 상태만 진실 소스로 유지하여 불일치 원천 차단.
        /// </summary>
        public bool IsAutoMode => AutoTypes.Count > 0;

        /// <summary> 현재 생산 중인 유닛 타입. null이면 대기 상태. </summary>
        public UnitType? CurrentProducing { get; set; }

        /// <summary>
        /// 현재 슬롯0(CurrentProducing)이 자동 생산으로 시작되었는지 여부.
        ///
        /// [2026-06-05 구조 개선] IsAutoMode와 동일한 파생 계산 방식으로 전환.
        /// 기존에는 별도 bool 필드를 수동으로 관리했는데, 자동 해제 경로
        /// (UnregisterAutoType, DisableAutoMode 등)에서 reset이 누락되면
        /// AutoTypes 상태와 불일치가 생겨 슬롯 중복/누락 버그가 발생했다.
        ///
        /// getter: backing flag가 true이고, 현재 생산 중이고, 해당 타입이 AutoTypes에
        ///         아직 등록된 경우에만 true를 반환한다.
        ///         → AutoTypes에서 타입이 제거되는 순간 자동으로 false 반환 (수동 reset 불필요).
        /// setter: _currentIsAutoFlag만 갱신. 기존 코드(state.CurrentIsAuto = true/false)와 완전 호환.
        /// </summary>
        public bool CurrentIsAuto
        {
            get => _currentIsAutoFlag
                   && CurrentProducing.HasValue
                   && AutoTypes.Contains(CurrentProducing.Value);
            set => _currentIsAutoFlag = value;
        }

        /// <summary>
        /// CurrentIsAuto getter의 backing field.
        /// "이 생산이 자동 경로에서 시작됐는가"라는 의도만 저장한다.
        /// 실제 자동 여부는 AutoTypes와 함께 getter에서 계산된다.
        /// </summary>
        private bool _currentIsAutoFlag;

        /// <summary> 현재 생산 경과 시간(초). </summary>
        public float ElapsedTime { get; set; }

        /// <summary> 현재 생산에 필요한 총 시간(초). </summary>
        public float RequiredTime { get; set; }

        /// <summary> 랠리 포인트. 생산 완료 후 유닛이 자동 이동할 목표. null이면 미설정. </summary>
        public HexCoord? RallyPoint { get; set; }

        /// <summary>
        /// 생산 큐 최대 크기. CurrentProducing + IsCharged=true 항목 수의 합산 상한.
        /// (IsCharged=false 자동 대기 항목은 합산에서 제외 — Rule 4)
        /// </summary>
        public const int MaxQueueSize = 3;

        /// <summary> 현재 생산 진행률 (0.0 ~ 1.0). </summary>
        public float Progress => RequiredTime > 0f ? ElapsedTime / RequiredTime : 0f;

        public ProductionState(int barracksId, TeamId team, HexCoord barracksPosition)
        {
            BarracksId = barracksId;
            Team = team;
            BarracksPosition = barracksPosition;
        }

        // ====================================================================
        // 편의 접근자
        // ====================================================================

        /// <summary>
        /// 해당 유닛 타입이 자동 생산 목록에 등록되어 있는지 확인.
        /// ProductionPanelUI의 자동 인디케이터 점등 판정에 사용.
        /// </summary>
        public bool AutoContains(UnitType type)
        {
            for (int i = 0; i < AutoTypes.Count; i++)
            {
                if (AutoTypes[i] == type) return true;
            }
            return false;
        }

        /// <summary>
        /// PendingQueue 내의 IsCharged=true(골드 이미 차감됨) 항목 수.
        /// Rule 4 큐 크기 상한 체크 및 슬롯 여유 공간 계산에 사용.
        /// </summary>
        public int ChargedPendingCount()
        {
            int count = 0;
            for (int i = 0; i < PendingQueue.Count; i++)
            {
                if (PendingQueue[i].IsCharged) count++;
            }
            return count;
        }
    }
}
