// ============================================================================
// ProductionState.cs
// 배럭 하나의 생산 상태를 담는 데이터 클래스.
//
// 각 배럭(Barracks)마다 독립적인 ProductionState를 가짐.
// UnitProductionUseCase가 Dictionary<int, ProductionState>로 관리.
//
// 생산 모드:
//   수동 (Manual): ManualQueue에 최대 3개 유닛을 추가, 순차 생산.
//   자동 (Auto): AutoEntries에 등록된 유닛을 순환 반복 생산.
//   수동 시작 시 자동 모드 해제 — Rule 2에 따라 슬롯 표시된(골드 차감된) 항목은 ManualQueue로 이관.
//
// 자동 생산 골드 차감 추적:
//   AutoEntry.IsCharged 플래그로 관리.
//   - 슬롯에 표시 가능한 자동 항목 → 등록 시 즉시 골드 차감 (IsCharged=true)
//   - 큐 풀 대기 상태 → 골드 미차감 (IsCharged=false), TryStartNext 시 차감
//   - 취소 시 IsCharged=true면 환불, false면 환불 없음
//   - ManualQueue 이관 시 IsCharged=true 항목만 이관 (이미 차감됨)
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 자동 생산 항목. 유닛 타입 + 골드 차감 여부를 묶어 관리.
    /// 슬롯에 표시되는 시점에 골드가 차감되며, IsCharged로 차감 상태를 추적.
    /// </summary>
    public struct AutoEntry
    {
        /// <summary> 자동 생산 유닛 타입. </summary>
        public UnitType Type;

        /// <summary>
        /// 골드가 이미 차감되었는지 여부.
        /// true = 슬롯에 표시되어 골드 차감 완료 → 취소 시 환불 필요.
        /// false = 큐 풀 대기 중으로 골드 미차감 → 취소 시 환불 불필요.
        /// </summary>
        public bool IsCharged;

        public AutoEntry(UnitType type, bool isCharged)
        {
            Type = type;
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

        /// <summary> 수동 생산 큐. 최대 MaxQueueSize개. </summary>
        public List<UnitType> ManualQueue { get; } = new List<UnitType>();

        /// <summary>
        /// 자동 생산에 등록된 항목 리스트.
        /// 각 항목은 유닛 타입 + 골드 차감 여부(IsCharged)를 포함.
        /// 슬롯에 표시 가능한 항목은 등록 시점에 골드가 차감됨 (IsCharged=true).
        /// 큐 풀 상태에서 대기 중인 항목은 골드 미차감 (IsCharged=false).
        /// </summary>
        public List<AutoEntry> AutoEntries { get; } = new List<AutoEntry>();

        /// <summary> 현재 자동 모드 활성 여부. </summary>
        public bool IsAutoMode { get; set; }

        /// <summary> 자동 순환 인덱스 (AutoEntries 내). </summary>
        public int AutoIndex { get; set; }

        /// <summary> 현재 생산 중인 유닛 타입. null이면 대기 상태. </summary>
        public UnitType? CurrentProducing { get; set; }

        /// <summary> 현재 생산 경과 시간(초). </summary>
        public float ElapsedTime { get; set; }

        /// <summary> 현재 생산에 필요한 총 시간(초). </summary>
        public float RequiredTime { get; set; }

        /// <summary> 랠리 포인트. 생산 완료 후 유닛이 자동 이동할 목표. null이면 미설정. </summary>
        public HexCoord? RallyPoint { get; set; }

        /// <summary> 수동 큐 최대 크기. </summary>
        public const int MaxQueueSize = 3;

        /// <summary> 현재 생산 진행률 (0.0 ~ 1.0). </summary>
        public float Progress => RequiredTime > 0f ? ElapsedTime / RequiredTime : 0f;

        // ====================================================================
        // AutoEntries 편의 접근자
        // ====================================================================

        /// <summary>
        /// AutoEntries에 해당 유닛 타입이 등록되어 있는지 확인.
        /// ProductionPanelUI 등 외부에서 자동 등록 여부를 판별할 때 사용.
        /// </summary>
        public bool AutoContains(UnitType type)
        {
            for (int i = 0; i < AutoEntries.Count; i++)
            {
                if (AutoEntries[i].Type == type) return true;
            }
            return false;
        }

        /// <summary>
        /// AutoEntries에서 해당 유닛 타입의 인덱스를 반환. 없으면 -1.
        /// </summary>
        public int AutoIndexOf(UnitType type)
        {
            for (int i = 0; i < AutoEntries.Count; i++)
            {
                if (AutoEntries[i].Type == type) return i;
            }
            return -1;
        }

        /// <summary>
        /// AutoEntries의 해당 인덱스에 있는 유닛 타입 반환.
        /// 인덱스 유효성은 호출자가 보장해야 함.
        /// </summary>
        public UnitType AutoTypeAt(int index)
        {
            return AutoEntries[index].Type;
        }

        /// <summary>
        /// AutoEntries 항목 수.
        /// </summary>
        public int AutoCount => AutoEntries.Count;

        public ProductionState(int barracksId, TeamId team, HexCoord barracksPosition)
        {
            BarracksId = barracksId;
            Team = team;
            BarracksPosition = barracksPosition;
        }
    }
}
