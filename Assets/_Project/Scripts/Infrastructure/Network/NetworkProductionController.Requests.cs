// ============================================================================
// NetworkProductionController.Requests.cs
// 생산 큐 추가·취소, 랠리포인트 설정, 자동 생산 토글의 서버 요청 처리.
// ============================================================================

using Unity.Netcode;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    public partial class NetworkProductionController
    {
        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 유닛 생산 큐 추가 요청. 클라이언트 UI에서 호출.
        /// 서버에서 검증 후 UnitProductionUseCase.EnqueueUnit() 실행.
        /// RequireOwnership=false: 모든 클라이언트에서 호출 가능.
        /// </summary>
        /// <param name="barracksId">생산 요청 배럭의 BuildingData Id</param>
        /// <param name="unitTypeInt">UnitType 열거형 정수값</param>
        /// <param name="teamIndex">TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void RequestEnqueueServerRpc(
            int barracksId,
            int unitTypeInt,
            int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "유닛 생산 큐 요청 수신",
                             $"ClientId={senderClientId}, BarracksId={barracksId}, UnitType={unitTypeInt}, Team={teamIndex}");

            // ----------------------------------------------------------------
            // 1. 부트스트래퍼 및 UseCase 확인
            // ----------------------------------------------------------------
            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkProductionController),
                                  "RequestEnqueueServerRpc — IGameServices 를 얻지 못했다. 이후 생산 요청이 모두 같은 자리에서 죽는다",
                                  $"Request=Enqueue, ClientId={senderClientId}");
                SendEnqueueFailed(senderClientId, "서버 초기화 오류");
                return;
            }

            UnitProductionUseCase production = _services.GetUnitProduction();
            ResourceUseCase resource = _services.GetResource();
            PopulationUseCase population = _services.GetPopulation();

            if (production == null || resource == null || population == null)
            {
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkProductionController),
                                  "RequestEnqueueServerRpc — UseCase 가 null 이다. 맵 로드 전일 수 있다",
                                  $"Request=Enqueue, ClientId={senderClientId}");
                SendEnqueueFailed(senderClientId, "맵 로드 중");
                return;
            }

            // ----------------------------------------------------------------
            // 2. 파라미터 변환
            // ----------------------------------------------------------------
            UnitType unitType = (UnitType)unitTypeInt;
            TeamId team = (TeamId)teamIndex;

            // ----------------------------------------------------------------
            // 3. 팀 소유권 검증 (Host=Blue, Client=Red)
            // ----------------------------------------------------------------
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (team != expectedTeam)
            {
                // 요청만 거부하고 게임은 계속된다 → Warn. 다만 정상 클라이언트는 보낼 수 없는 요청이다.
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkProductionController),
                                 "생산 요청 거부 — 요청 팀이 발신자의 팀과 다르다",
                                 $"Request=Enqueue, Reason=Team, ClientId={senderClientId}, RequestedTeam={team}, ExpectedTeam={expectedTeam}");
                SendEnqueueFailed(senderClientId, "팀 불일치");
                return;
            }

            // ----------------------------------------------------------------
            // 4. 배럭 생산 상태 존재 여부 확인
            //    ProductionState는 OnBuildingPlaced 이벤트로 등록됨
            // ----------------------------------------------------------------
            var state = production.GetState(barracksId);
            if (state == null)
            {
                // 클라이언트는 존재하는 배럭의 패널만 열 수 있다 → 서버에 없다는 것은 상태 불일치다.
                GameLog.Ops.Warn(LogEvent.ServerRejectedTargetNotFound, "Network", nameof(NetworkProductionController),
                                 "생산 요청 거부 — 배럭 생산 상태가 서버에 없다",
                                 $"Request=Enqueue, ClientId={senderClientId}, BarracksId={barracksId}");
                SendEnqueueFailed(senderClientId, "배럭 없음");
                return;
            }

            // 발신자 팀과 서버 생산 상태의 실제 소유 팀을 함께 검증한다.
            if (state.Team != expectedTeam)
            {
                SendEnqueueFailed(senderClientId, "팀 불일치");
                return;
            }

            // ----------------------------------------------------------------
            // 5. 골드 확인 (EnqueueUnit 내부에서도 검증하지만 로그를 위해 미리 확인)
            // ----------------------------------------------------------------
            int cost = UnitProductionStats.GetGoldCost(unitType);
            if (!resource.CanAfford(team, cost))
            {
                // ProductionPanelUI 가 CanAfford 로 이미 막았는데 서버가 거부했다 = 클라·서버 골드 상태 불일치.
                GameLog.Ops.Warn(LogEvent.ServerRejectedInsufficientResource, "Network", nameof(NetworkProductionController),
                                 "생산 요청 거부 — 골드 부족(클라이언트가 이미 막았어야 한다)",
                                 $"Request=Enqueue, Reason=Gold, Team={team}, Required={cost}, Current={resource.GetGold(team)}");
                SendEnqueueFailed(senderClientId, "골드 부족");
                return;
            }

            // ----------------------------------------------------------------
            // 6. 인구 확인 (EnqueueUnit 내부에서도 검증)
            // ----------------------------------------------------------------
            int popCost = UnitProductionStats.GetPopulationCost(unitType);
            if (!population.HasPopulation(team, popCost))
            {
                // 골드와 같은 성격(클라이언트 HasPopulation 이 이미 막았어야 한다)이라 같은 키로 묶고
                // 무엇이 부족했는지는 Reason 필드로 분해한다.
                GameLog.Ops.Warn(LogEvent.ServerRejectedInsufficientResource, "Network", nameof(NetworkProductionController),
                                 "생산 요청 거부 — 인구 부족(클라이언트가 이미 막았어야 한다)",
                                 $"Request=Enqueue, Reason=Population, Team={team}, Required={popCost}");
                SendEnqueueFailed(senderClientId, "인구 부족");
                return;
            }

            // ----------------------------------------------------------------
            // 7. 서버에서 EnqueueUnit 실행 (골드 즉시 차감, 큐 등록)
            //    EnqueueUnit은 내부에서 CanAfford·HasPopulation을 재확인하므로 안전.
            // ----------------------------------------------------------------
            bool success = production.EnqueueUnit(barracksId, unitType);
            if (!success)
            {
                // 큐 용량도 자원의 일종이라 같은 키로 묶는다.
                // EnqueueFailedClientRpc 의 매핑 문자열("큐 가득")과 아래 사유가 일치하지 않아
                // 토스트가 발행되지 않는다 → 플레이어는 이 실패를 알 수 없다(운영 판정 근거).
                GameLog.Ops.Warn(LogEvent.ServerRejectedInsufficientResource, "Network", nameof(NetworkProductionController),
                                 "생산 요청 거부 — 생산 큐가 가득 찼다",
                                 $"Request=Enqueue, Reason=QueueFull, BarracksId={barracksId}, UnitType={unitType}");
                SendEnqueueFailed(senderClientId, "큐 추가 실패 (큐 가득 참)");
                return;
            }

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "서버: 유닛 생산 큐 추가 성공",
                             $"BarracksId={barracksId}, UnitType={unitType}, Team={team}");

            // 큐 변경 이벤트는 EnqueueUnit 내부에서 이미 발행됨
            // → OnProductionQueueChanged 구독(서버) → SyncQueueStateClientRpc → 클라이언트 UI 즉시 갱신
        }

        // ====================================================================
        // 큐 슬롯 취소 — ServerRpc (BUG-14 수정)
        // ====================================================================

        /// <summary>
        /// 큐 슬롯 취소 요청. 클라이언트 UI에서 호출.
        /// 서버에서 팀 소유권을 검증한 뒤 UnitProductionUseCase.CancelQueueAt()을 실행.
        /// CancelQueueAt 내부에서 골드 환불 + OnProductionQueueChanged 이벤트 발행
        /// → OnProductionQueueChanged 구독(서버) → SyncQueueStateClientRpc로 전체 클라이언트에 자동 전파.
        /// </summary>
        /// <param name="barracksId">취소 대상 배럭의 BuildingData Id</param>
        /// <param name="slotIndex">취소할 큐 슬롯 인덱스 (0=생산 중, 1~2=대기 큐)</param>
        /// <param name="teamIndex">TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void CancelSlotServerRpc(
            int barracksId,
            int slotIndex,
            int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // 팀 소유권 검증: Host(ClientId=0)=Blue, 그 외=Red
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if ((TeamId)teamIndex != expectedTeam)
            {
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkProductionController),
                                 "큐 슬롯 취소 거부 — 요청 팀이 발신자의 팀과 다르다",
                                 $"Request=CancelSlot, Reason=Team, ClientId={senderClientId}, RequestedTeam={(TeamId)teamIndex}, ExpectedTeam={expectedTeam}");
                return;
            }

            // 부트스트래퍼 및 UseCase 확인
            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _services?.GetUnitProduction();
            if (production == null)
            {
                // ⚠️ 레벨 승격(Warn → Error, LogAudit.md §3-3 :708).
                //    RequestEnqueueServerRpc 의 같은 불변식 위반과 동일한 사건인데 이 자리만 레벨이 낮았다.
                //    취소 요청이 통째로 무시되고 복구 경로가 없다.
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkProductionController),
                                  "CancelSlotServerRpc — UnitProductionUseCase 가 null 이다. 취소 요청이 통째로 무시된다",
                                  $"Request=CancelSlot, ClientId={senderClientId}, BarracksId={barracksId}");
                return;
            }

            // 다른 팀의 생산 큐와 음수 슬롯 인덱스는 상태 변경 전에 거부한다.
            var state = production.GetState(barracksId);
            if (state == null || state.Team != expectedTeam || slotIndex < 0) return;

            // 서버에서 CancelQueueAt 실행
            // 내부에서 골드 환불 + OnProductionQueueChanged 이벤트 발행
            // → OnProductionQueueChanged 구독 → SyncQueueStateClientRpc 자동 전파
            bool success = production.CancelQueueAt(barracksId, slotIndex);
            if (!success)
            {
                // 요청만 거부되고 게임은 계속된다 → Warn.
                // 다만 클라이언트 UI 는 취소 성공을 가정하므로 상태가 갈리고, 통지 경로가 없다 → 운영.
                GameLog.Ops.Warn(LogEvent.ServerActionExecutionFailed, "Network", nameof(NetworkProductionController),
                                 "CancelQueueAt 실행 실패 — 클라이언트 UI 는 취소 성공을 가정해 상태가 갈린다",
                                 $"Request=CancelSlot, BarracksId={barracksId}, SlotIndex={slotIndex}");
                return;
            }

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "서버: 큐 슬롯 취소 성공",
                             $"BarracksId={barracksId}, SlotIndex={slotIndex}");
        }

        // ====================================================================
        // 랠리포인트 설정 — ServerRpc
        // ====================================================================

        /// <summary>
        /// 랠리포인트 설정 요청. 클라이언트 UI에서 호출.
        /// 서버에서 팀 소유권을 검증한 뒤 UnitProductionUseCase.SetRallyPoint()를 실행.
        ///
        /// ClientRpc가 필요 없는 이유:
        ///   랠리포인트는 서버 생산 완료 시 state.RallyPoint를 읽어
        ///   SpawnUnitClientRpc의 hasRally/rallyQ/rallyR 파라미터로 전달됨.
        ///   따라서 서버의 ProductionState.RallyPoint만 정확하면 모든 클라이언트에서 올바르게 동작.
        ///   화면에 표시되는 랠리 마커(OnRallyPointChanged)는 클라이언트 로컬에서
        ///   _production.SetRallyPoint()를 함께 호출하여 표시만 갱신함.
        ///
        /// RequireOwnership=false: 모든 클라이언트에서 호출 가능 (발신자 ClientId로 팀 검증).
        /// </summary>
        /// <param name="barracksId">랠리 설정 대상 배럭의 BuildingData Id</param>
        /// <param name="q">랠리 타겟 타일의 Q 좌표</param>
        /// <param name="r">랠리 타겟 타일의 R 좌표</param>
        /// <param name="teamIndex">TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="rpcParams">서버 RPC 파라미터 (발신자 ClientId 포함)</param>
        [ServerRpc(RequireOwnership = false)]
        public void SetRallyPointServerRpc(
            int barracksId,
            int q,
            int r,
            int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // ----------------------------------------------------------------
            // 1. 팀 소유권 검증 (Host=Blue, Client=Red)
            //    다른 팀 배럭의 랠리포인트를 임의 변경하지 못하도록 발신자 팀과 일치 여부 확인.
            // ----------------------------------------------------------------
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if ((TeamId)teamIndex != expectedTeam)
            {
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkProductionController),
                                 "랠리포인트 설정 거부 — 요청 팀이 발신자의 팀과 다르다",
                                 $"Request=SetRally, Reason=Team, ClientId={senderClientId}, RequestedTeam={(TeamId)teamIndex}, ExpectedTeam={expectedTeam}");
                return;
            }

            // ----------------------------------------------------------------
            // 2. 부트스트래퍼 및 UseCase 확인
            //    맵 로드 타이밍 이슈로 초기화가 늦을 수 있으므로 Find로 재시도.
            // ----------------------------------------------------------------
            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _services?.GetUnitProduction();
            if (production == null)
            {
                // ⚠️ 레벨 승격(Warn → Error, LogAudit.md §3-3 :776). :708 과 동일한 불변식 위반이다.
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkProductionController),
                                  "SetRallyPointServerRpc — UnitProductionUseCase 가 null 이다. 랠리 설정이 통째로 무시된다",
                                  $"Request=SetRally, ClientId={senderClientId}, BarracksId={barracksId}");
                return;
            }

            // 요청 인수의 팀이 맞아도 대상 생산건물이 다른 팀이면 변경하지 않는다.
            var state = production.GetState(barracksId);
            if (state == null || state.Team != expectedTeam) return;

            // ----------------------------------------------------------------
            // 3. 서버 상태에 랠리포인트 반영
            //    HexCoord는 Hexiege.Domain 네임스페이스의 값 객체.
            //    Application 레이어의 SetRallyPoint가 배럭 존재 여부를 내부에서 처리.
            // ----------------------------------------------------------------
            HexCoord target = new HexCoord(q, r);

            // ----------------------------------------------------------------
            // 3-1. 좌표 재검증 — 깃발을 꽂을 수 없는 좌표(맵 밖·막힌 타일)면 거부
            //    입력 쪽(생산 패널)이 이미 같은 판정(UnitProductionUseCase.CanSetRallyPointAt)으로
            //    걸러 토스트를 띄우므로, 정상 클라이언트는 이런 좌표를 보내지 않는다.
            //    그래도 서버가 같은 기준으로 한 번 더 확인해 Host·Client 결과가 역할과 무관하게 같도록 한다.
            //    정상 클라이언트가 보낼 수 없는 요청이므로 팀 불일치 거부와 같은 로그 이벤트(변조 탐지 신호)로 남긴다.
            //    (SetRallyPoint 내부에도 같은 검사가 있지만 그쪽은 조용히 무시하므로, 로그는 여기서 남긴다.)
            // ----------------------------------------------------------------
            if (!production.CanSetRallyPointAt(target))
            {
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkProductionController),
                                 "랠리포인트 설정 거부 — 지정할 수 없는 좌표다",
                                 $"Request=SetRally, Reason=Coord, ClientId={senderClientId}, BarracksId={barracksId}, TargetQ={q}, TargetR={r}");
                return;
            }

            production.SetRallyPoint(barracksId, target);

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "서버: 랠리포인트 설정 완료",
                             $"BarracksId={barracksId}, TargetQ={q}, TargetR={r}, Team={expectedTeam}");
        }

        // ====================================================================
        // 자동 생산 토글 — ServerRpc + ClientRpc
        // ====================================================================

        /// <summary>
        /// 자동 생산 토글 요청. 클라이언트 UI에서 호출.
        /// 서버에서 검증 후 ToggleAutoProduction 실행, 결과를 전체 클라이언트에 동기화.
        /// unitTypeInt 파라미터로 토글 대상 유닛 타입을 지정.
        /// </summary>
        /// <param name="barracksId">배럭 BuildingData Id</param>
        /// <param name="unitTypeInt">토글 대상 유닛 타입 (UnitType 정수값)</param>
        /// <param name="teamIndex">TeamId 정수값</param>
        /// <param name="rpcParams">서버 RPC 파라미터</param>
        [ServerRpc(RequireOwnership = false)]
        public void ToggleAutoServerRpc(
            int barracksId,
            int unitTypeInt,
            int teamIndex,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // 팀 소유권 검증
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if ((TeamId)teamIndex != expectedTeam)
            {
                GameLog.Ops.Warn(LogEvent.ServerRejectedUnauthorizedRequest, "Network", nameof(NetworkProductionController),
                                 "자동 생산 토글 거부 — 요청 팀이 발신자의 팀과 다르다",
                                 $"Request=ToggleAuto, Reason=Team, ClientId={senderClientId}, RequestedTeam={(TeamId)teamIndex}, ExpectedTeam={expectedTeam}");
                return;
            }

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _services?.GetUnitProduction();
            if (production == null)
            {
                // ⚠️ 레벨 승격(Warn → Error, LogAudit.md §3-3 :825). :708 과 동일한 불변식 위반이다.
                GameLog.Ops.Error(LogEvent.ServerRpcGameServicesMissing, "Network", nameof(NetworkProductionController),
                                  "ToggleAutoServerRpc — UnitProductionUseCase 가 null 이다. 토글 요청이 통째로 무시된다",
                                  $"Request=ToggleAuto, ClientId={senderClientId}, BarracksId={barracksId}");
                return;
            }

            // 자동 생산도 서버 생산 상태의 실제 소유 팀을 확인한 뒤 변경한다.
            var state = production.GetState(barracksId);
            if (state == null || state.Team != expectedTeam) return;

            // unitTypeInt를 UnitType으로 변환하여 토글
            UnitType unitType = (UnitType)unitTypeInt;
            bool success = production.ToggleAutoProduction(barracksId, unitType);
            if (!success)
            {
                // 요청 거부 후 게임은 계속된다 → Warn. 다만 클라이언트 토글 상태와 갈리고 통지 경로가 없다 → 운영.
                GameLog.Ops.Warn(LogEvent.ServerActionExecutionFailed, "Network", nameof(NetworkProductionController),
                                 "ToggleAutoProduction 실행 실패 — 클라이언트 토글 상태와 갈린다",
                                 $"Request=ToggleAuto, BarracksId={barracksId}, UnitType={unitType}");
                return;
            }

            state = production.GetState(barracksId);
            bool isAuto = state?.IsAutoMode ?? false;

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "서버: 자동 생산 토글 완료",
                             $"BarracksId={barracksId}, UnitType={unitType}, IsAuto={isAuto}");
            AutoProductionChangedClientRpc(barracksId, isAuto, unitTypeInt);
        }

    }
}
