// ============================================================================
// ProductionState.cs
// 배럭 하나의 생산 상태를 담는 데이터 클래스.
//
// 각 배럭(Barracks)마다 독립적인 ProductionState를 가짐.
// UnitProductionUseCase가 Dictionary<int, ProductionState>로 관리.
//
// 생산 모드:
//   수동 (Manual): ManualQueue에 최대 3개 유닛을 추가, 순차 생산.
//   자동 (Auto): AutoTypes에 등록된 유닛을 순환 반복 생산.
//   수동 시작 시 자동 모드 해제 + 현재 자동 생산 취소.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
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

        /// <summary> 자동 생산에 등록된 유닛 타입들. </summary>
        public List<UnitType> AutoTypes { get; } = new List<UnitType>();

        /// <summary> 현재 자동 모드 활성 여부. </summary>
        public bool IsAutoMode { get; set; }

        /// <summary> 자동 순환 인덱스 (AutoTypes 내). </summary>
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

        public ProductionState(int barracksId, TeamId team, HexCoord barracksPosition)
        {
            BarracksId = barracksId;
            Team = team;
            BarracksPosition = barracksPosition;
        }
    }
}
