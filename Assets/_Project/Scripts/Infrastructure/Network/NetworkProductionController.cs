// ============================================================================
// NetworkProductionController.cs
// 네트워크 모드에서 유닛 생산 큐 요청을 서버로 중계하고 생산 완료 시 전체 클라이언트에 동기화.
//
// 흐름:
//   1. 클라이언트 UI → RequestEnqueueServerRpc(barracksId, unitType, teamIndex)
//   2. 서버: 팀 소유권·골드·인구·배럭 존재 검증
//      → UnitProductionUseCase.EnqueueUnit() 실행 (골드 즉시 차감)
//   3. 서버: GameEvents.OnUnitProduced 구독
//      → 유닛 생산 완료 시 SpawnUnitClientRpc로 전파
//   4. 모든 클라이언트: SpawnUnitWithId()로 동일 Id의 UnitData 재생성
//      + GameEvents.OnUnitProduced 발행 → ProductionTicker가 랠리포인트 이동 처리
//
// 생산 Tick 처리:
//   ProductionTicker.Update()에서 서버 전용 분기를 통해
//   클라이언트는 Tick을 실행하지 않고 서버만 실행.
//
// 배치:
//   씬에 빈 GameObject "NetworkProductionController" 생성.
//   NetworkObject 컴포넌트 + 이 스크립트를 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 유닛 생산 네트워크 컨트롤러.
    /// 클라이언트의 생산 큐 요청을 서버에서 검증·실행하고,
    /// 생산 완료 시 모든 클라이언트에 유닛 스폰을 동기화.
    /// </summary>
    public partial class NetworkProductionController : NetworkBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>게임 서비스 참조. UseCase 접근에 사용. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.</summary>
        private IGameServices _services;

        private long _retryGeneration;
        private bool _retryLifetimeOpen;
        private bool _retryMatchEnded;
        private System.IDisposable _retryGameEndSubscription, _retryGameStartSubscription;
        private readonly System.Collections.Generic.List<UnitViewRetryOwner> _retryOwners = new();

        // 같은 서비스가 다음 경기에도 재사용될 수 있다. 대기 후에는 회차와
        // 실제 UnitData 객체까지 확인하여 예전 작업이 새 유닛에 적용되지 않게 한다.
        public sealed class UnitViewRetryOwner
        {
            public readonly long Generation;
            public readonly IGameServices Services;
            public readonly UnitSpawnUseCase Spawn;
            public readonly IUnitFactory Factory;
            public readonly UnitData Unit;
            private readonly System.Func<long> _generation;
            private readonly System.Func<bool> _controllerAlive, _sessionActive;
            private readonly System.Func<IGameServices> _services;
            private readonly System.Action<string, float> _terminal;
            public bool Finished { get; private set; }
            public float Elapsed { get; internal set; }
            public UnitViewRetryOwner(long generation, IGameServices services, UnitSpawnUseCase spawn,
                IUnitFactory factory, UnitData unit, System.Func<long> readGeneration,
                System.Func<bool> controllerAlive, System.Func<bool> sessionActive,
                System.Func<IGameServices> readServices, System.Action<string, float> terminal)
            {
                Generation = generation; Services = services; Spawn = spawn; Factory = factory; Unit = unit;
                _generation = readGeneration; _controllerAlive = controllerAlive;
                _sessionActive = sessionActive; _services = readServices; _terminal = terminal;
            }
            public string ClosedReason()
            {
                if (Finished) return "already-terminal";
                if (_generation() != Generation) return "owner-generation-replaced";
                if (!_controllerAlive()) return "controller-retired";
                if (!_sessionActive()) return "session-ended";
                if (Services == null || !ReferenceEquals(_services(), Services)) return "services-replaced";
                if (Spawn == null || !ReferenceEquals(Services.GetUnitSpawn(), Spawn)) return "spawn-service-replaced";
                if (Factory == null || !ReferenceEquals(Services.GetUnitFactory(), Factory)) return "factory-replaced";
                if (Unit == null || !Unit.IsAlive) return "unit-dead";
                var current = Spawn.GetUnit(Unit.Id);
                if (current == null) return "unit-retired";
                if (!ReferenceEquals(current, Unit)) return "unit-identity-replaced";
                return null;
            }
            public void Finish(string reason)
            {
                if (Finished) return;
                Finished = true;
                _terminal?.Invoke(reason, Elapsed);
            }
        }

        private void CloseRetryLifetime(string reason)
        {
            _retryLifetimeOpen = false;
            _retryGeneration++;
            // 종료 콜백은 자기 작업만 제거한다. 목록 복사로 순회 중 변경을 피한다.
            foreach (var owner in _retryOwners.ToArray()) owner.Finish(reason);
        }

        /// <summary> OnUnitProduced 구독 해제용 Disposable. </summary>
        private System.IDisposable _unitProducedSubscription;

        /// <summary> OnProductionStarted 구독 해제용 Disposable. </summary>
        private System.IDisposable _productionStartedSubscription;

        /// <summary> OnProductionQueueChanged 구독 해제용 Disposable (큐 취소 동기화). </summary>
        private System.IDisposable _queueChangedSubscription;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper를 탐색하고,
        /// 서버라면 OnUnitProduced 이벤트를 구독하여 클라이언트에 전파 준비.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            CloseRetryLifetime("controller-respawn");
            _retryMatchEnded = false;
            _retryLifetimeOpen = true;
            _retryGameEndSubscription?.Dispose();
            _retryGameStartSubscription?.Dispose();
            _retryGameEndSubscription = GameEvents.OnGameEnd.Subscribe(_ =>
            {
                _retryMatchEnded = true;
                CloseRetryLifetime("game-end");
            });
            _retryGameStartSubscription = GameEvents.OnGameStarted.Subscribe(_ =>
            {
                // 살아 있는 경기의 중복 시작 알림은 회차를 바꾸지 않는다.
                _retryMatchEnded = false;
                if (!_retryLifetimeOpen) { _retryGeneration++; _retryLifetimeOpen = true; }
            });

            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                // 스폰은 계속되지만(요청 전이라 아직 기능이 죽지는 않았다) 이후 모든 RPC 가 같은 자리에서 죽는다.
                // NGO 스폰 타이밍은 회선 상태에 좌우돼 플레이어 기기에서만 어긋날 수 있고 통지 경로가 없다 → 운영.
                GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices, "Network", nameof(NetworkProductionController),
                                 "스폰 시점에 GameServicesLocator 에서 IGameServices 를 얻지 못했다");
            }

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "스폰 완료",
                             $"IsServer={IsServer}");

            // 서버만 생산 완료 이벤트를 구독하여 클라이언트에 동기화
            if (IsServer)
            {
                SubscribeToProductionEvents();
            }
        }

        // ====================================================================
        // 외부 호출용 래퍼 메서드 — UI 등 Presentation 레이어에서 사용
        // ====================================================================
        //
        // 왜 래퍼 메서드인가?
        //   ProductionPanelUI 등 UI(Presentation)가 *ServerRpc 메서드를 직접 호출하면
        //   NGO API에 결합된다. 래퍼는 UI에게 일반 메서드 호출 형태를 제공하여
        //   Presentation의 Unity.Netcode 직접 의존을 제거한다.
        //
        //   각 래퍼는 enum 타입을 그대로 받아 내부에서 int 캐스트하여 ServerRpc로 전달한다.
        //
        // ====================================================================

        /// <summary>
        /// 유닛 생산 큐 추가 요청 — UI 래퍼.
        /// 실제로는 RequestEnqueueServerRpc로 위임된다.
        /// </summary>
        /// <param name="barracksId">생산 요청 배럭 Id.</param>
        /// <param name="unitType">생산할 유닛 종류.</param>
        /// <param name="team">요청 팀.</param>
        public void RequestEnqueue(int barracksId, UnitType unitType, TeamId team)
        {
            RequestEnqueueServerRpc(barracksId, (int)unitType, (int)team);
        }

        /// <summary>
        /// 유닛 생산 큐 슬롯 취소 요청 — UI 래퍼.
        /// 실제로는 CancelSlotServerRpc로 위임된다.
        /// </summary>
        /// <param name="barracksId">대상 배럭 Id.</param>
        /// <param name="slotIndex">취소할 큐 슬롯 인덱스(0=생산 중, 1~2=대기 큐).</param>
        /// <param name="team">요청 팀.</param>
        public void RequestCancelSlot(int barracksId, int slotIndex, TeamId team)
        {
            CancelSlotServerRpc(barracksId, slotIndex, (int)team);
        }

        /// <summary>
        /// 랠리포인트 설정 요청 — UI 래퍼.
        /// 실제로는 SetRallyPointServerRpc로 위임된다.
        /// </summary>
        /// <param name="barracksId">대상 배럭 Id.</param>
        /// <param name="q">랠리 타겟 좌표 Q.</param>
        /// <param name="r">랠리 타겟 좌표 R.</param>
        /// <param name="team">요청 팀.</param>
        public void RequestSetRallyPoint(int barracksId, int q, int r, TeamId team)
        {
            SetRallyPointServerRpc(barracksId, q, r, (int)team);
        }

        /// <summary>
        /// 자동 생산 토글 요청 — UI 래퍼.
        /// 실제로는 ToggleAutoServerRpc로 위임된다.
        /// </summary>
        /// <param name="barracksId">대상 배럭 Id.</param>
        /// <param name="unitType">토글 대상 유닛 종류.</param>
        /// <param name="team">요청 팀.</param>
        public void RequestToggleAuto(int barracksId, UnitType unitType, TeamId team)
        {
            ToggleAutoServerRpc(barracksId, (int)unitType, (int)team);
        }

        /// <summary>
        /// 네트워크 디스폰 시 이벤트 구독 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            CloseRetryLifetime("controller-despawn");
            _retryGameEndSubscription?.Dispose(); _retryGameEndSubscription = null;
            _retryGameStartSubscription?.Dispose(); _retryGameStartSubscription = null;
            base.OnNetworkDespawn();
            _unitProducedSubscription?.Dispose();
            _unitProducedSubscription = null;
            _productionStartedSubscription?.Dispose();
            _productionStartedSubscription = null;
            _queueChangedSubscription?.Dispose();
            _queueChangedSubscription = null;
        }

        public override void OnDestroy()
        {
            // 비활성화 중에도 GameEnd를 놓치지 않도록 구독은 유지하지만,
            // 파괴 시에는 반드시 해제한다. NGO의 자체 정리도 생략하지 않는다.
            CloseRetryLifetime("controller-destroyed");
            _retryGameEndSubscription?.Dispose(); _retryGameEndSubscription = null;
            _retryGameStartSubscription?.Dispose(); _retryGameStartSubscription = null;
            base.OnDestroy();
        }

        // ====================================================================
        // 이벤트 구독 (서버 전용)
        // ====================================================================

        /// <summary>
        /// 서버에서 GameEvents.OnUnitProduced를 구독하여
        /// 생산 완료 시 SpawnUnitClientRpc를 전송.
        /// 맵 로드 전(UseCase null)에 스폰될 수 있으므로
        /// 이벤트 발행 시점에 UseCase 존재를 재확인.
        /// </summary>
        private void SubscribeToProductionEvents()
        {
            _unitProducedSubscription = GameEvents.OnUnitProduced
                .Subscribe(OnUnitProduced);

            // 생산 시작 이벤트 → 클라이언트에 타이머 시작 알림 (프로그레스 바 동기화)
            _productionStartedSubscription = GameEvents.OnProductionStarted
                .Subscribe(OnProductionStarted);

            // 큐 변경 이벤트 → 클라이언트에 전체 큐 상태 동기화 (취소 등 대응)
            _queueChangedSubscription = GameEvents.OnProductionQueueChanged
                .Subscribe(OnProductionQueueChanged);

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "서버 측 생산 이벤트 구독 완료");
        }

        /// <summary>
        /// 생산 시작 이벤트 핸들러 (서버 전용).
        /// 클라이언트에 생산 시작 정보를 전파하여 프로그레스 바를 동기화.
        /// </summary>
        private void OnProductionStarted(ProductionStartedEvent e)
        {
            // 이 오브젝트가 아직 네트워크에 살아 있고, 내가 서버일 때만 진행한다.
            //   이 핸들러는 아래에서 ProductionStartedClientRpc 를 전송한다.
            //
            //   이유는 NetworkCombatController.Update() 진입부 가드와 같다 — 그쪽의 상세 주석 참조.
            //   (요약: IsServer 는 "내가 서버 역할인가" 이지 "이 오브젝트가 아직 살아 있는가" 가 아니다.
            //    NetworkManager.Shutdown() 이 불린 뒤에도 디스폰 전까지 IsServer 는 여전히 참이므로,
            //    위험 구간은 NetworkManager.Shutdown() 과 디스폰 사이다.)
            //
            //   ※ IsSpawned 를 앞에 두는 이유 — || 는 앞 조건이 참이면 뒤를 평가하지 않는다(단락 평가).
            //     스폰되지 않은 상태에서 IsServer 를 건드리지 않고 곧바로 반환하기 위해서다
            //     (NetworkUnit.cs:291 이 같은 이유로 같은 순서를 쓴다).
            //
            //   ※ 아래 production == null / state == null 가드는 순서를 포함해 그대로 둔다.
            if (!IsSpawned || !IsServer) return;

            UnitProductionUseCase production = _services?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(e.BarracksId);
            if (state == null) return;

            ProductionStartedClientRpc(
                e.BarracksId,
                (int)e.Type,
                state.RequiredTime);
        }

        /// <summary>
        /// 큐 변경 이벤트 핸들러 (서버 전용).
        /// 클라이언트에 전체 큐 상태를 스냅샷으로 전파.
        ///
        /// [재작성 — 2026-04-19]
        /// 새 단일 PendingQueue 구조에 맞춰 전송 포맷 변경:
        ///   CurrentProducing (Type, IsAuto)
        ///   PendingQueue[0..2] (Type, IsAuto, IsCharged)
        ///   AutoTypes[0..2] (Type)
        ///   AutoCycleIndex
        /// PendingQueue는 슬롯1/슬롯2 + 자동 순환 대기(최대 1개)를 합쳐 최대 3개면 충분하므로
        /// 고정 크기 인자로 직렬화(없는 슬롯은 typeInt=-1).
        /// </summary>
        private void OnProductionQueueChanged(ProductionQueueChangedEvent e)
        {
            // 이 오브젝트가 아직 네트워크에 살아 있고, 내가 서버일 때만 진행한다.
            //   이 핸들러는 아래에서 SyncQueueStateClientRpc 를 전송한다.
            //
            //   이유는 NetworkCombatController.Update() 진입부 가드와 같다 — 그쪽의 상세 주석 참조.
            //   (요약: IsServer 는 "내가 서버 역할인가" 이지 "이 오브젝트가 아직 살아 있는가" 가 아니다.
            //    NetworkManager.Shutdown() 이 불린 뒤에도 디스폰 전까지 IsServer 는 여전히 참이므로,
            //    위험 구간은 NetworkManager.Shutdown() 과 디스폰 사이다.)
            //
            //   ※ IsSpawned 를 앞에 두는 이유 — || 는 앞 조건이 참이면 뒤를 평가하지 않는다(단락 평가).
            //     스폰되지 않은 상태에서 IsServer 를 건드리지 않고 곧바로 반환하기 위해서다
            //     (NetworkUnit.cs:291 이 같은 이유로 같은 순서를 쓴다).
            //
            //   ※ 같은 SubscribeToProductionEvents() 블록에서 구독되는 형제 셋
            //     (OnProductionStarted · OnUnitProduced · OnProductionQueueChanged)의 가드 모양을 통일한다.
            //   ※ 아래 production == null / state == null 가드는 순서를 포함해 그대로 둔다.
            if (!IsSpawned || !IsServer) return;

            UnitProductionUseCase production = _services?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(e.BarracksId);
            if (state == null) return;

            // CurrentProducing: -1이면 없음
            int currentType = state.CurrentProducing.HasValue ? (int)state.CurrentProducing.Value : -1;

            // PendingQueue: 최대 3개, 각 슬롯마다 (Type, IsAuto, IsCharged) 3필드 전송.
            // 인덱스 0→슬롯1, 1→슬롯2, 2→추가 대기(예: 수동 2개 + 자동 등록 1개의 경우 미차감 자동).
            int p0Type = -1, p1Type = -1, p2Type = -1;
            bool p0Auto = false, p1Auto = false, p2Auto = false;
            bool p0Charged = false, p1Charged = false, p2Charged = false;

            if (state.PendingQueue.Count > 0)
            {
                var s = state.PendingQueue[0];
                p0Type = (int)s.Type; p0Auto = s.IsAuto; p0Charged = s.IsCharged;
            }
            if (state.PendingQueue.Count > 1)
            {
                var s = state.PendingQueue[1];
                p1Type = (int)s.Type; p1Auto = s.IsAuto; p1Charged = s.IsCharged;
            }
            if (state.PendingQueue.Count > 2)
            {
                var s = state.PendingQueue[2];
                p2Type = (int)s.Type; p2Auto = s.IsAuto; p2Charged = s.IsCharged;
            }

            // AutoTypes: 최대 3개, 없는 슬롯은 -1
            int auto0 = state.AutoTypes.Count > 0 ? (int)state.AutoTypes[0] : -1;
            int auto1 = state.AutoTypes.Count > 1 ? (int)state.AutoTypes[1] : -1;
            int auto2 = state.AutoTypes.Count > 2 ? (int)state.AutoTypes[2] : -1;

            SyncQueueStateClientRpc(
                e.BarracksId,
                currentType,
                state.CurrentIsAuto,
                p0Type, p0Auto, p0Charged,
                p1Type, p1Auto, p1Charged,
                p2Type, p2Auto, p2Charged,
                auto0, auto1, auto2,
                state.AutoCycleIndex);
        }

        /// <summary>
        /// 유닛 생산 완료 이벤트 핸들러 (서버 전용).
        /// 생산된 유닛 정보를 모든 클라이언트에 전파.
        /// </summary>
        private void OnUnitProduced(UnitProducedEvent e)
        {
            // 이 오브젝트가 아직 네트워크에 살아 있고, 내가 서버일 때만 진행한다.
            //   이 핸들러는 아래에서 SpawnUnitClientRpc 를 전송한다.
            //
            //   이유는 NetworkCombatController.Update() 진입부 가드와 같다 — 그쪽의 상세 주석 참조.
            //   (요약: IsServer 는 "내가 서버 역할인가" 이지 "이 오브젝트가 아직 살아 있는가" 가 아니다.
            //    NetworkManager.Shutdown() 이 불린 뒤에도 디스폰 전까지 IsServer 는 여전히 참이므로,
            //    위험 구간은 NetworkManager.Shutdown() 과 디스폰 사이다.)
            //
            //   ※ IsSpawned 를 앞에 두는 이유 — || 는 앞 조건이 참이면 뒤를 평가하지 않는다(단락 평가).
            //     스폰되지 않은 상태에서 IsServer 를 건드리지 않고 곧바로 반환하기 위해서다
            //     (NetworkUnit.cs:291 이 같은 이유로 같은 순서를 쓴다).
            if (!IsSpawned || !IsServer) return;

            UnitData unit = e.Unit;
            HexCoord? rally = e.RallyPoint;

            // ⚠️ 좌표를 HexCoord 구조체 통째로 한 필드에 넣지 않는다
            //   (LogRules 1.4 — 값 안에 구분자 ", " 금지).
            //   unit.Position 은 HexCoord 구조체이고 그 ToString() 은 "(4, 15)" 를 돌려준다.
            //   로그 라인은 key=value 쌍을 ", " 로 잇는 형식이라, 값 안에 같은 문자열이 들어가면
            //   파싱이 그 자리에서 쪼개져 필드 하나가 둘로 보인다(실제 로그 파일에서 확인된 위반).
            //   → 이 파일의 다른 로그(예: SpawnUnitClientRpc)가 이미 쓰는 Q=/R= 표기로 통일한다.
            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "서버 유닛 생산 완료",
                             $"UnitId={unit.Id}, UnitType={unit.Type}, Team={unit.Team}, " +
                             $"Q={unit.Position.Q}, R={unit.Position.R}");

            // 랠리포인트 좌표 (없으면 0,0으로 전달하고 hasRally=false)
            int rallyQ = rally.HasValue ? rally.Value.Q : 0;
            int rallyR = rally.HasValue ? rally.Value.R : 0;
            bool hasRally = rally.HasValue;

            SpawnUnitClientRpc(
                unit.Id,
                (int)unit.Type,
                (int)unit.Team,
                unit.Position.Q,
                unit.Position.R,
                rallyQ,
                rallyR,
                hasRally);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 유닛 생산 완료 후 모든 클라이언트에 UnitData 초기화 정보 전송.
        ///
        /// NGO 전환 후 변경:
        ///   [이전] 클라이언트가 Instantiate(prefab) 직접 실행
        ///   [이후] NGO가 NetworkObject.Spawn()으로 프리팹을 자동 생성.
        ///          이 RPC는 UnitData 생성 + UnitView 초기화만 수행.
        ///          GameObject는 NetworkUnit.OnNetworkSpawn()에서 이미 딕셔너리에 등록됨.
        ///
        /// 서버는 UseCase에서 이미 처리 완료되었으므로 스킵.
        /// 클라이언트는 SpawnUnitWithId로 동일 Id의 UnitData를 재생성하고,
        /// UnitFactory.InitializeUnitView()로 NGO가 생성한 GameObject에 UnitView를 초기화.
        /// GameEvents.OnUnitProduced를 발행하여 ProductionTicker가 랠리 이동을 처리하도록 함.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id</param>
        /// <param name="unitTypeInt">UnitType 열거형 정수값</param>
        /// <param name="teamIndex">TeamId 정수값</param>
        /// <param name="q">스폰 좌표 Q</param>
        /// <param name="r">스폰 좌표 R</param>
        /// <param name="rallyQ">랠리포인트 좌표 Q (hasRally=false면 무시)</param>
        /// <param name="rallyR">랠리포인트 좌표 R (hasRally=false면 무시)</param>
        /// <param name="hasRally">랠리포인트 유무</param>
        [ClientRpc]
        private void SpawnUnitClientRpc(
            int unitId,
            int unitTypeInt,
            int teamIndex,
            int q,
            int r,
            int rallyQ,
            int rallyR,
            bool hasRally)
        {
            // 서버는 이미 UseCase에서 처리 완료 → 중복 방지
            if (IsServer) return;

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "SpawnUnitClientRpc 수신",
                             $"UnitId={unitId}, UnitType={unitTypeInt}, Team={teamIndex}, Q={q}, R={r}");

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkProductionController),
                                  "SpawnUnitClientRpc — IGameServices 를 얻지 못했다. 생산된 유닛이 이 클라이언트에 나타나지 않는다",
                                  $"Request=SpawnUnit, UnitId={unitId}");
                return;
            }

            UnitSpawnUseCase unitSpawn = _services.GetUnitSpawn();
            IUnitFactory unitFactory = _services.GetUnitFactory();

            if (unitSpawn == null)
            {
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkProductionController),
                                  "SpawnUnitClientRpc — UnitSpawnUseCase 가 null 이다",
                                  $"Request=SpawnUnit, UnitId={unitId}");
                return;
            }

            // 파라미터 변환
            UnitType unitType = (UnitType)unitTypeInt;
            TeamId team = (TeamId)teamIndex;
            HexCoord spawnCoord = new HexCoord(q, r);

            // UnitData 재생성 (도메인 데이터만 생성, GameObject Instantiate는 하지 않음)
            // SpawnUnitWithId는 내부에서 GameEvents.OnUnitSpawned를 발행하지만,
            // 클라이언트의 UnitFactory.CreateUnitObject()는 NetworkContext 분기에 의해
            // 아무것도 하지 않음 (NGO가 이미 GameObject를 생성했으므로)
            UnitData unit = unitSpawn.SpawnUnitWithId(unitId, unitType, team, spawnCoord);
            if (unit == null)
            {
                // ⚠️ 레벨 승격(Warn → Error, LogAudit.md §3-3 :484).
                //    재시도 경로가 없다. 이 유닛은 이 클라이언트에서 영구히 누락된다
                //    → "복구되었나?" 아니오 → Error.
                //
                //    데이터에 유닛 종류·팀·좌표를 함께 남긴다(2026-08-18 보강):
                //      실패 원인은 UnitSpawnUseCase.SpawnUnitWithId 의 "그 좌표 타일이 클라이언트 그리드에 없다"
                //      이므로, 좌표가 있어야 맵 desync 인지 아닌지를 릴리스 로그만 보고 판단할 수 있다.
                //      UnitType·Team 은 507~508행에서 변환된 enum 변수를 쓴다(정수보다 읽기 쉽고 프로젝트 최빈 표기).
                //      Q·R 표기는 같은 메서드 위쪽 개발 로그와 일치시킨다.
                GameLog.Ops.Error(LogEvent.ClientStateSyncApplyFailed, "Network", nameof(NetworkProductionController),
                                  "SpawnUnitClientRpc — SpawnUnitWithId 실패. 이 유닛은 클라이언트에서 영구히 누락된다",
                                  $"Request=SpawnUnit, UnitId={unitId}, UnitType={unitType}, Team={team}, Q={q}, R={r}");
                return;
            }

            // NGO가 생성한 GameObject에 UnitView 초기화
            // NetworkUnit.OnNetworkSpawn()에서 RegisterUnitObject()로 이미 딕셔너리에 등록됨
            if (unitFactory != null)
            {
                // StartCoroutine executes up to the first yield immediately. Both the
                // initial attempt and later retries now use the same owner guard.
                StartCoroutine(RetryInitializeUnitView(unitFactory, unit));
            }

            // 랠리포인트 재구성
            HexCoord? rallyPoint = hasRally ? (HexCoord?)new HexCoord(rallyQ, rallyR) : null;

            // OnUnitProduced 발행 → ProductionTicker가 랠리포인트 자동 이동 처리
            // 멀티플레이 클라이언트 경로에는 배럭 Id가 동기화되지 않으며, AI는 싱글플레이 전용이라
            // 이 경로에서 BarracksId를 사용하는 구독자가 없으므로 -1(미지정)을 전달한다.
            GameEvents.OnUnitProduced.OnNext(new UnitProducedEvent(unit, rallyPoint, -1));

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "클라이언트: 유닛 데이터 재생성 완료",
                             $"UnitId={unit.Id}, UnitType={unitType}, Team={team}");
        }

        /// <summary>
        /// UnitView 초기화 재시도 코루틴.
        /// NGO가 프리팹을 아직 전달하지 않은 경우 (SpawnUnitClientRpc가 먼저 도착),
        /// NetworkUnit.OnNetworkSpawn()에서 RegisterUnitObject()가 호출될 때까지 대기.
        /// 최대 3초 대기 후 실패 로그 출력.
        /// </summary>
        /// <param name="unitFactory">IUnitFactory 참조</param>
        /// <param name="unitData">초기화할 UnitData</param>
        private System.Collections.IEnumerator RetryInitializeUnitView(IUnitFactory unitFactory, UnitData unitData)
        {
            var network = NetworkManager;
            UnitViewRetryOwner owner = null;
            owner = new UnitViewRetryOwner(_retryGeneration, _services, _services?.GetUnitSpawn(),
                unitFactory, unitData, () => _retryGeneration,
                () => this != null && isActiveAndEnabled && IsSpawned && _retryLifetimeOpen,
                () => NetworkContext.IsNetworkActive && network != null
                    && ReferenceEquals(NetworkManager, network) && network.IsListening
                    && !network.ShutdownInProgress && network.IsConnectedClient && !IsServer,
                () => ReferenceEquals(_services, GameServicesLocator.Current) ? _services : null,
                (reason, elapsed) =>
                {
                    _retryOwners.Remove(owner);
                    string data = $"UnitId={unitData.Id}, UnitType={(int)unitData.Type}, Generation={owner.Generation}, Reason={reason}, WaitSeconds={elapsed:F3}, MaxWaitSeconds=3";
                    if (reason == "timeout")
                        GameLog.Ops.Error(LogEvent.UnitViewInitializeTimeout, "Network", nameof(NetworkProductionController),
                            "RetryInitializeUnitView — live exact owner timed out awaiting UnitView", data);
                    else
                        GameLog.Dev.Info("Network", nameof(NetworkProductionController), "UnitView retry terminal", data);
                });
            _retryOwners.Add(owner);
            return RunUnitViewInitializationRetry(owner, () => UnityEngine.Time.deltaTime);
        }

        // 실제 생산 경로와 기존 회귀 검증이 같은 IEnumerator를 실행한다.
        // 검증에서 대기 시간만 바꾸어도 객체 수명 검사는 건너뛰지 않는다.
        public static System.Collections.IEnumerator RunUnitViewInitializationRetry(
            UnitViewRetryOwner owner, System.Func<float> deltaTime)
        {
            try
            {
                string initial = owner.ClosedReason();
                if (initial != null) { owner.Finish(initial); yield break; }
                if (owner.Factory.InitializeUnitView(owner.Unit))
                { owner.Finish("initialized"); yield break; }
                while (owner.Elapsed < 3f)
                {
                    string closed = owner.ClosedReason();
                    if (closed != null) { owner.Finish(closed); yield break; }
                    yield return null;
                    closed = owner.ClosedReason();
                    if (closed != null) { owner.Finish(closed); yield break; }
                    float delta = deltaTime();
                    if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0)
                    { owner.Finish("invalid-wait-clock"); yield break; }
                    owner.Elapsed += delta;
                    if (owner.Factory.InitializeUnitView(owner.Unit))
                    { owner.Finish("initialized"); yield break; }
                }
                string terminal = owner.ClosedReason();
                owner.Finish(terminal ?? "timeout");
            }
            finally { if (!owner.Finished) owner.Finish("coroutine-disposed"); }
        }

        private void OnDisable() => CloseRetryLifetime("controller-disabled");

        // 단순 비활성화는 이전 작업만 닫는다. 같은 경기에서 다시 켜면 새 작업을
        // 받을 수 있지만, 경기 종료 후 Enable만으로는 경기를 다시 열지 않는다.
        public static bool CanResumeUnitViewRetry(bool matchEnded, bool spawned, bool sessionActive)
            => !matchEnded && spawned && sessionActive;

        private void OnEnable()
        {
            var network = NetworkManager;
            if (!_retryLifetimeOpen && CanResumeUnitViewRetry(_retryMatchEnded, IsSpawned,
                NetworkContext.IsNetworkActive && network != null && network.IsListening
                && !network.ShutdownInProgress && network.IsConnectedClient && !IsServer))
            {
                _retryGeneration++;
                _retryLifetimeOpen = true;
            }
        }

        // ====================================================================
        // ClientRpc — 생산 UI 동기화 (큐/프로그레스)
        // ====================================================================

        /// <summary>
        /// 서버에서 생산 시작 시 클라이언트에 전파.
        /// 클라이언트 측 ProductionState에 타이머 정보를 설정하여
        /// Update()에서 프로그레스 바를 로컬 시뮬레이션 가능하게 함.
        /// </summary>
        [ClientRpc]
        private void ProductionStartedClientRpc(
            int barracksId,
            int unitTypeInt,
            float requiredTime)
        {
            if (IsServer) return;

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _services?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(barracksId);
            if (state == null) return;

            // 클라이언트 측 ProductionState에 생산 중 정보 설정 (프로그레스 바 시뮬레이션용)
            state.CurrentProducing = (UnitType)unitTypeInt;
            state.ElapsedTime = 0f;
            state.RequiredTime = requiredTime;

            // UI 갱신 이벤트 발행
            GameEvents.OnProductionStarted.OnNext(
                new ProductionStartedEvent(barracksId, (UnitType)unitTypeInt));
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "클라이언트: 생산 시작 동기화",
                             $"BarracksId={barracksId}, UnitType={unitTypeInt}, RequiredTime={requiredTime}");
        }

        /// <summary>
        /// 서버에서 큐 상태 변경 시 전체 스냅샷을 클라이언트에 전파.
        /// 큐 추가, 취소, 생산 완료 등 모든 큐 변경에 대응.
        ///
        /// [재작성 — 2026-04-19]
        /// 새 단일 PendingQueue 구조에 맞춰 전송 포맷 변경:
        ///   - PendingQueue: 최대 3개 슬롯 × (Type, IsAuto, IsCharged)
        ///   - AutoTypes: 최대 3개 (Type만)
        ///   - AutoCycleIndex + CurrentIsAuto 추가
        /// 클라이언트는 전송받은 값으로 ProductionState를 그대로 덮어쓰기.
        /// IsAutoMode는 AutoTypes.Count로 자동 계산되므로 별도 전송 불필요.
        /// </summary>
        /// <param name="barracksId">배럭 Id</param>
        /// <param name="currentTypeInt">현재 생산 중 유닛 타입 (-1=없음)</param>
        /// <param name="currentIsAuto">현재 생산 중 유닛이 자동 경로로 시작되었는지</param>
        /// <param name="p0TypeInt">PendingQueue[0].Type (-1=없음)</param>
        /// <param name="p0IsAuto">PendingQueue[0].IsAuto</param>
        /// <param name="p0IsCharged">PendingQueue[0].IsCharged</param>
        /// <param name="p1TypeInt">PendingQueue[1].Type (-1=없음)</param>
        /// <param name="p1IsAuto">PendingQueue[1].IsAuto</param>
        /// <param name="p1IsCharged">PendingQueue[1].IsCharged</param>
        /// <param name="p2TypeInt">PendingQueue[2].Type (-1=없음)</param>
        /// <param name="p2IsAuto">PendingQueue[2].IsAuto</param>
        /// <param name="p2IsCharged">PendingQueue[2].IsCharged</param>
        /// <param name="auto0TypeInt">AutoTypes[0] (-1=없음)</param>
        /// <param name="auto1TypeInt">AutoTypes[1] (-1=없음)</param>
        /// <param name="auto2TypeInt">AutoTypes[2] (-1=없음)</param>
        /// <param name="autoCycleIndex">AutoCycleIndex (다음 자동 순환 위치)</param>
        [ClientRpc]
        private void SyncQueueStateClientRpc(
            int barracksId,
            int currentTypeInt,
            bool currentIsAuto,
            int p0TypeInt, bool p0IsAuto, bool p0IsCharged,
            int p1TypeInt, bool p1IsAuto, bool p1IsCharged,
            int p2TypeInt, bool p2IsAuto, bool p2IsCharged,
            int auto0TypeInt,
            int auto1TypeInt,
            int auto2TypeInt,
            int autoCycleIndex)
        {
            if (IsServer) return;

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            UnitProductionUseCase production = _services?.GetUnitProduction();
            if (production == null) return;

            var state = production.GetState(barracksId);
            if (state == null) return;

            // ── PendingQueue 덮어쓰기 ────────────────────────────────────
            // 클라이언트는 화면 표시용이므로 서버 스냅샷을 그대로 신뢰.
            state.PendingQueue.Clear();
            if (p0TypeInt >= 0)
                state.PendingQueue.Add(new QueueSlot((UnitType)p0TypeInt, p0IsAuto, p0IsCharged));
            if (p1TypeInt >= 0)
                state.PendingQueue.Add(new QueueSlot((UnitType)p1TypeInt, p1IsAuto, p1IsCharged));
            if (p2TypeInt >= 0)
                state.PendingQueue.Add(new QueueSlot((UnitType)p2TypeInt, p2IsAuto, p2IsCharged));

            // ── AutoTypes 덮어쓰기 ───────────────────────────────────────
            // AutoTypes.Count > 0이면 IsAutoMode가 자동으로 true가 됨.
            state.AutoTypes.Clear();
            if (auto0TypeInt >= 0) state.AutoTypes.Add((UnitType)auto0TypeInt);
            if (auto1TypeInt >= 0) state.AutoTypes.Add((UnitType)auto1TypeInt);
            if (auto2TypeInt >= 0) state.AutoTypes.Add((UnitType)auto2TypeInt);
            state.AutoCycleIndex = autoCycleIndex;

            // ── CurrentProducing 동기화 ─────────────────────────────────
            if (currentTypeInt >= 0)
            {
                // ProductionStartedClientRpc에서 이미 타이머가 세팅되었을 수 있으므로
                // CurrentProducing이 비어 있을 때만 설정 (RequiredTime 보존)
                if (!state.CurrentProducing.HasValue)
                    state.CurrentProducing = (UnitType)currentTypeInt;
                state.CurrentIsAuto = currentIsAuto;
            }
            else
            {
                // 생산 종료/취소 상태 — 타이머 초기화
                state.CurrentProducing = null;
                state.CurrentIsAuto = false;
                state.ElapsedTime = 0f;
                state.RequiredTime = 0f;
            }

            // UI 갱신 이벤트 발행 — ProductionPanelUI가 구독 중
            GameEvents.OnProductionQueueChanged.OnNext(
                new ProductionQueueChangedEvent(barracksId));
        }

        /// <summary>
        /// 자동 생산 상태 변경을 모든 클라이언트에 전파.
        ///
        /// [재작성 — 2026-04-19]
        /// 새 구조에서는 IsAutoMode가 AutoTypes.Count > 0으로 자동 계산되는 읽기 전용 프로퍼티이므로
        /// 여기서 별도로 상태를 변경할 수 없음.
        /// AutoTypes와 PendingQueue 모두 SyncQueueStateClientRpc가 토글과 함께 전파되므로
        /// 이 RPC는 UI 갱신 이벤트 발행 및 로그 출력 용도로만 사용.
        /// </summary>
        /// <param name="barracksId">배럭 Id</param>
        /// <param name="isAuto">토글 후 자동 모드 활성 여부 (로그 전용)</param>
        /// <param name="unitTypeInt">토글 대상 유닛 타입 정수값 (로그 전용)</param>
        [ClientRpc]
        private void AutoProductionChangedClientRpc(int barracksId, bool isAuto, int unitTypeInt)
        {
            // 서버는 ToggleAutoServerRpc에서 이미 처리 완료
            if (IsServer) return;

            UnitType unitType = (UnitType)unitTypeInt;

            // 상태 덮어쓰기는 SyncQueueStateClientRpc가 담당 — 여기서는 UI 갱신 이벤트만 추가 발행.
            // (SyncQueueStateClientRpc가 도착하지 않은 경우 대비해 리프레시 시그널을 한번 더 보냄)
            GameEvents.OnProductionQueueChanged.OnNext(new ProductionQueueChangedEvent(barracksId));

            GameLog.Dev.Info("Network", nameof(NetworkProductionController), "클라이언트: 자동 생산 상태 동기화",
                             $"BarracksId={barracksId}, UnitType={unitType}, IsAuto={isAuto}");
        }

        // ====================================================================
        // 실패 피드백 — 요청자 클라이언트에게만 전송
        // ====================================================================

        /// <summary>
        /// 생산 큐 추가 실패를 요청한 클라이언트에게만 알림.
        /// </summary>
        private void SendEnqueueFailed(ulong targetClientId, string reason)
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };
            EnqueueFailedClientRpc(reason, clientRpcParams);
        }

        /// <summary>
        /// 생산 큐 추가 실패 알림. 요청한 클라이언트에게만 전송.
        /// 클라이언트는 GameEvents.OnToastRequested 이벤트를 발행하여 ToastUI에 표시한다.
        ///
        /// reason 문자열을 ToastKey로 매핑:
        ///   "골드 부족" → GoldInsufficient
        ///   "인구 부족"/"인구 한계" → PopulationFull
        ///   "큐 가득" → ProductionQueueFull
        ///   그 외는 로그만 남기고 토스트 생략.
        /// </summary>
        [ClientRpc]
        private void EnqueueFailedClientRpc(string reason, ClientRpcParams clientRpcParams = default)
        {
            // 개발인 이유(원칙 1): 같은 거부를 서버 쪽 RequestEnqueueServerRpc 가 이미 더 상세히 남긴다.
            // 양쪽을 운영으로 두면 한 사건이 두 번 집계된다.
            GameLog.Dev.Warn("Network", nameof(NetworkProductionController), "클라이언트: 유닛 생산 큐 추가 실패 알림 수신",
                             $"Reason={reason}");

            // reason → ToastKey 매핑. 매핑이 정의된 사유만 토스트 발행.
            if (reason == "골드 부족")
                GameEvents.OnToastRequested.OnNext(ToastKey.GoldInsufficient);
            else if (reason == "인구 부족" || reason == "인구 한계")
                GameEvents.OnToastRequested.OnNext(ToastKey.PopulationFull);
            else if (reason == "큐 가득")
                GameEvents.OnToastRequested.OnNext(ToastKey.ProductionQueueFull);
        }
    }
}
