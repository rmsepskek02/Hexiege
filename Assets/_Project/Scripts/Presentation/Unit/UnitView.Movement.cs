using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public partial class UnitView
    {
        // ====================================================================
        // 이동
        // ====================================================================

        /// <summary>
        /// 경로를 따라 유닛을 시각적으로 이동시킴.
        /// 멀티플레이에서는 서버만 실행 — 클라이언트는 NetworkTransform이
        /// 서버 위치를 자동으로 보간·동기화하므로 이동 로직을 실행하지 않음.
        /// </summary>
        /// <param name="path">A* 경로 (시작점 포함)</param>
        public void MoveTo(List<HexCoord> path)
        {
            _activeRallyPoint = null;
            _rallyPolicyCompletionPending = false;
            MoveToInternal(path, UnitMovementIntentReason.AStarPath, startNewCommand: true);
        }

        /// <summary>
        /// 생산 유닛의 랠리 경유를 시작한다. 재경로는 실제 경로 끝이 아니라 이 깃발을
        /// 기준으로 다시 선택한다. 일반 MoveTo와 완료 정리가 문맥을 해제한다.
        /// </summary>
        public void MoveToRally(List<HexCoord> path, HexCoord rallyPoint)
        {
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            _activeRallyPoint = rallyPoint;
            _rallyPolicyCompletionPending = false;
            MoveToInternal(path, UnitMovementIntentReason.AStarPath, startNewCommand: true);
        }

        /// <summary>
        /// Starts or restarts the existing Legacy coroutine while keeping B2 command/segment
        /// correlation explicit. The diagnostic scope does not affect the path or writer.
        /// </summary>
        private void MoveToInternal(
            List<HexCoord> path,
            UnitMovementIntentReason intentReason,
            bool startNewCommand)
        {
            // 멀티플레이에서는 서버만 이동 로직을 실행.
            // 클라이언트는 NetworkTransform이 서버 위치를 자동으로 보간·동기화.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;

            // 중복 command 판정은 AlignPathStartToTransform보다 먼저 수행한다. 이미 활성인 같은
            // 목표는 현재 코루틴과 도착-커밋 경계를 그대로 보존해야 한다.
            if (startNewCommand
                && path != null
                && path.Count > 0
                && _navigationObjective != null
                && _navigationObjective.IsActive
                && _navigationObjective.Matches(path[path.Count - 1]))
            {
                _navigationObjective.NotifyEnvironmentChanged(
                    _navigationEnvironmentRevision);
                if (_navigationObjective.SuppressesDuplicateCommand(
                        path[path.Count - 1], _navigationEnvironmentRevision))
                {
                    if (_moveCoroutine != null
                        && _navigationObjective.CanQueueSuppressedPath(
                            path[path.Count - 1],
                            _navigationEnvironmentRevision))
                    {
                        _pendingPath = path;
                        _navigationObjective.MarkWaitingRepath();
                    }
                    return;
                }
            }

            // ────────────────────────────────────────────────────────────
            // [Phase 3 이동 보정] 경로 출발점을 유닛의 실제 transform 위치와 정합.
            //
            //   왜 필요한가:
            //     RequestMove는 경로를 "도메인 타일"(_unitData.Position) 기준으로 계산한다.
            //     그런데 유닛이 두 타일 사이를 이동하는 도중(transform이 도메인 타일보다 목적지 쪽으로
            //     앞서 있음)에 새 경로가 발급되면, 경로 첫 타일(path[1])이 실제 위치보다 "뒤"에 놓여
            //     첫 걸음이 목적지 역방향으로 향한다("뒤로 밀림", 규칙 11 위반).
            //
            //   동작:
            //     AlignPathStartToTransform이 이 "역방향 첫 스텝" 케이스만 감지해 현재 논리 타일에서
            //     앞쪽 인접 타일까지의 도착 segment와 그 이후 경로를 합성한다. 정방향/정합 상태는
            //     원본 path를 그대로 반환하므로 일반 이동은 전혀 건드리지 않는다.
            // ────────────────────────────────────────────────────────────
            if (startNewCommand)
                _navigationObjective?.Cancel();

            path = AlignPathStartToTransform(path);

            // 외부에서 이 유닛이 어디로 가는지 알 수 있도록 최종 목적지를 저장.
            // 빈 경로/null 안전 처리: path가 비정상이면 코루틴 동작이 알아서 종료된다.
            if (path != null && path.Count > 0)
            {
                HexCoord destination = path[path.Count - 1];

                _currentDestination = destination;
                _hasDestination = true;

                if (startNewCommand)
                {
                    _navigationObjective = new UnitNavigationObjective(
                        destination, path, _navigationEnvironmentRevision);
                }
                else if (_activeRallyPoint.HasValue)
                {
                    _navigationObjective?.RetargetRallyEndpoint(destination);
                }
            }

            // 여기까지 왔다는 것은 새 objective 또는 같은 objective의 명시적 segment 교체다.
            // 중복 command는 위에서 기존 코루틴을 보존한 채 반환했으므로, 이 시점부터만
            // 예약 상태를 비우고 기존 코루틴을 교체할 수 있다.
            _pendingPath = null;
            _currentNextTileCoord = null;

            if (_moveCoroutine != null)
            {
                EndHealPresentation("movement-replaced");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                PostCombatTraceEvent("movement-replaced", close: true);
#endif
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            if (startNewCommand)
                BeginMovementShadowCommand(intentReason);
            else
                BeginMovementShadowSegment(intentReason);

            // 이동/전투 흐름 코루틴 시작.
            //   - GameSystemRules.md 규칙 1~18을 그대로 따른다.
            //   - 외부 while 안에 타일 순회를 직접 인라인하여 가독성을 높였다.
            _moveCoroutine = StartCoroutine(MoveAlongPathV3(path));
        }

        /// <summary>
        /// [Phase 3 이동 보정] 새 경로의 출발점을 유닛의 실제 transform 위치와 정합시킨다.
        ///
        /// 배경(왜 필요한가 — 초급자용 설명):
        ///   경로 계산(RequestMove)은 항상 "도메인 타일"(_unitData.Position)을 출발점으로 삼는다.
        ///   그런데 유닛의 도메인 위치는 "타일 중심에 도착했을 때"만 갱신되므로, 유닛이 두 타일
        ///   사이를 이동하는 도중(=transform이 도메인 타일보다 목적지 쪽으로 앞서 있음)에 새 경로가
        ///   발급되면, 경로의 첫 타일(path[1])이 실제 위치보다 "뒤"에 놓일 수 있다. 이 경우 유닛이
        ///   첫 걸음을 목적지 반대 방향(뒤)으로 내딛어 "뒤로 밀리는" 것처럼 보인다.
        ///   (규칙 11: A* 재개 시 뒤쪽 타일로 복귀 금지 — 이 위반을 강제로 바로잡는다.)
        ///
        /// 동작:
        ///   1) 첫 스텝(transform → path[1])이 최종 목적지 기준 "역방향"(XZ 내적&lt;0)인지 판정한다.
        ///      역방향이 아니면(정상) 원본 path를 그대로 반환 — 일반 이동은 전혀 건드리지 않는다.
        ///   2) 역방향이면 실제 transform 위치 기준 "앞쪽 가장 가까운 타일"을 구한다.
        ///      그 타일에서 목적지까지의 후속 경로는 상태를 바꾸지 않고 계산하고, 현재 논리 타일에서
        ///      앞쪽 타일까지를 첫 segment로 앞에 붙인다. Root가 실제로 그 waypoint에 도착한 뒤에만
        ///      MoveAlongPathV3의 기존 checkpoint/ProcessStep 경계가 도메인 위치와 점유를 커밋한다.
        ///
        /// 반환: 보정된 경로(역방향 케이스) 또는 원본 경로(정상/보정 불가 케이스).
        /// </summary>
        /// <param name="path">MoveTo가 받은 원본 경로(시작점 포함).</param>
        /// <returns>보정 경로 또는 원본 경로.</returns>
        private List<HexCoord> AlignPathStartToTransform(List<HexCoord> path)
        {
            // 방어: 경로 비정상 / 의존성 없음 / 유닛 사망 → 원본 그대로.
            if (path == null || path.Count < 2) return path;
            if (_movementUseCase == null || _unitData == null || !_unitData.IsAlive) return path;

            // 최종 목적지 — "앞쪽/뒤쪽" 판정의 기준.
            HexCoord finalTarget = path[path.Count - 1];

            // path[1] 중심과 최종 목적지 중심을 뷰 좌표로 변환.
            Vector3 step1View = TileCenterView(path[1]);
            Vector3 finalView = TileCenterView(finalTarget);

            // 첫 스텝 방향(transform → path[1])과 목적지 방향(transform → finalTarget)의 XZ 내적.
            //   내적 ≥ 0 : 정방향(또는 옆) — 정상 이동이므로 보정하지 않는다.
            //   내적 < 0 : 첫 스텝이 목적지 반대쪽 — "뒤로 밀림" 버그이므로 보정 대상.
            Vector3 toStep1 = step1View - transform.position; toStep1.y = 0f;
            Vector3 toFinal = finalView - transform.position; toFinal.y = 0f;
            float dot = toStep1.x * toFinal.x + toStep1.z * toFinal.z;
            if (dot >= 0f) return path;   // 정방향 — 원본 그대로.

            // ── 여기부터 역방향(보정) 케이스 ──

            // 실제 transform 위치 → 도메인 좌표 → 앞쪽 가장 가까운 타일.
            //   (ResumeFromForwardTileV3의 forwardTile 계산과 동일한 방식.)
            Vector3 domainPos = ViewConverter.FromView(transform.position);
            if (!_movementUseCase.TryFindReachableForwardRejoinTile(
                    _unitData,
                    domainPos,
                    finalTarget,
                    out HexCoord forwardTile))
            {
                return path;
            }

            // 앞쪽 타일 이후의 경로만 계산한다. RequestMoveFrom은 UnitData.Position과 점유를
            // 변경하지 않는다. forwardTile이 최종 목표면 그 타일 하나가 후속 경로 전체다.
            List<HexCoord> routeFromForward = forwardTile == finalTarget
                ? new List<HexCoord> { forwardTile }
                : _movementUseCase.RequestMoveFrom(forwardTile, finalTarget);

            // 현재 논리 타일 → 앞쪽 인접 타일 → 후속 경로를 합성한다. 비인접 후보나 잘못된
            // 후속 경로는 원본을 유지해 fail-closed하며 논리 위치를 절대 미리 넘기지 않는다.
            if (!UnitPathStartTransitionPolicy.TryBuildArrivalCommittedPath(
                    _unitData.Position,
                    forwardTile,
                    routeFromForward,
                    out List<HexCoord> corrected))
            {
                return path;
            }

            return corrected;
        }

        /// <summary>
        /// 타일 좌표를 유닛이 서는 뷰 좌표(타일 중심 + 유닛 높이 보정)로 변환하는 소형 헬퍼.
        /// (도메인 → 월드(HexToWorld) → 뷰(ToView) + UnitYOffset. 반복되던 3줄 변환을 모은 것.)
        /// </summary>
        private static Vector3 TileCenterView(HexCoord coord)
        {
            Vector3 world = HexMetrics.HexToWorld(coord);
            Vector3 view = ViewConverter.ToView(world);
            view.y += HexMetrics.UnitYOffset;
            return view;
        }

        /// <summary>
        /// 외부(예: GameBootstrapper / FlowField 갱신 트리거)에서
        /// "현재 진행 중인 경로가 무효화됐으니 동일 destination으로 다시 RequestMove해서
        ///  새 경로로 이동을 재시작하라"고 알려주는 메서드.
        ///
        /// 호출 조건:
        ///   - HasDestination == true (이미 이동 중인 유닛만)
        ///   - 서버(또는 싱글플레이)에서만. 클라이언트는 NetworkTransform 보간 동기화 결과만 본다.
        ///
        /// 동작:
        ///   1) 현재 destination을 그대로 두고 RequestMove 재호출.
        ///   2) 결과 path가 유효하면 MoveTo(path)로 새 경로 시작 — 기존 코루틴 자동 정지.
        ///   3) 경로 못 찾으면 그대로 두고 다음 트리거를 기다림(=대기).
        ///
        /// 주의:
        ///   - 새 코루틴(MoveAlongPathV3)이 도입된 후에도 동일하게 동작.
        ///   - 전투 중 유닛에 대해 호출되어도 안전: MoveTo가 코루틴을 교체하므로 전투 루프가
        ///     자연스럽게 새 path 기반 외부 while로 대체됨.
        /// </summary>
        public void OnPathInvalidated()
        {
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            if (!_hasDestination) return;
            if (_unitData == null || !_unitData.IsAlive) return;
            if (_movementUseCase == null) return;

            // 건물 생성·파괴 한 번을 이 유닛이 관측한 walkability revision으로 기록한다.
            // ulong 고갈은 현실적으로 도달하지 않지만 wrap하면 오래된 revision과 구분할 수 없으므로
            // 최대값에서 고정한다.
            if (_navigationEnvironmentRevision < ulong.MaxValue)
                _navigationEnvironmentRevision++;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent("environment-invalidated");
#endif
            _navigationObjective?.NotifyEnvironmentChanged(
                _navigationEnvironmentRevision);

            // 랠리 목적지가 바뀌는 질의에서는 이전 환경의 예약 경로를 이월하지 않는다.
            // 전투 중이면 아래에서 새 예약 없이 복귀 경계의 최신 질의를 기다린다.
            if (_activeRallyPoint.HasValue)
                _pendingPath = null;

            // ────────────────────────────────────────────────────────────
            // 전투 중인 유닛에 대한 repath 차단.
            //
            // 왜 필요한가:
            //   건물 건설/파괴 시 GameBootstrapper.RepathAllAliveUnits()가
            //   살아있는 모든 유닛의 OnPathInvalidated()를 호출한다.
            //   여기서 그대로 MoveTo(newPath)를 부르면 기존 전투 코루틴이 종료되지 않은 채
            //   새 이동 코루틴이 시작되어 두 코루틴이 동시에 실행된다.
            //   결과: 전투 타일과 별도로 다음 타일에 이동 슬롯이 추가로 잡혀 유령 점유가 생긴다.
            //
            // 전투 상태 판정:
            //   - 근접: _v2AttackSlotTargetCoord.HasValue (EnterMeleePursuit 동안 set).
            //   - 원거리: _v2InStationaryCombat (EnterStationaryCombat 진입 직전에 true).
            //
            // 동작:
            //   전투 중이면 즉시 return. 전투 종료 후 ResumeFromForwardTile / 자연 진행으로 path가
            //   다시 정상 흐름을 타기 때문에, 이 시점에 굳이 새 path를 강제로 끼워 넣지 않는다.
            // ────────────────────────────────────────────────────────────
            if (IsInCombat())
            {
                _combatPursuitPathInvalidated = true;
                return;
            }

            HexCoord dest = _currentDestination;
            List<HexCoord> newPath = RequestLatestAuthoritativeRepath(dest, resolveRallyGoal: true);
            if (newPath == null || newPath.Count == 0
                || (!_activeRallyPoint.HasValue && newPath.Count < 2))
            {
                // 일반 실패는 기존 대기를 유지한다. 랠리 후보 소진은 별도 예약을 통해
                // 소유 코루틴이 안전하게 종료하므로 같은 null이라도 의미를 섞지 않는다.
                return;
            }
            if (_activeRallyPoint.HasValue && newPath.Count == 1)
            {
                // 같은 타일 안의 중심 완료도 기존 코루틴에서 소비한다.
                // 여기서 즉시 재시작하면 이미 중심에 있는 경우 완료 콜백이 재진입할 수 있다.
                _pendingPath = newPath;
                return;
            }

            // ────────────────────────────────────────────────────────────
            // 부드러운 경로 교체 — 건물 생성/파괴 시 멈춤 현상 제거.
            //
            // 기본 동작:
            //   _pendingPath에 새 경로를 "예약"만 한다.
            //   MoveAlongPathV3 코루틴은 그대로 돌면서, 매 타일 도착 직후
            //   _pendingPath를 검사해 비어있지 않으면 path 변수를 교체하고
            //   외부 while로 재진입한다. 코루틴 자체는 중단되지 않으므로
            //   StopCoroutine + StartCoroutine에 의한 1~2 프레임 공백이 사라진다.
            //
            // 예외(즉시 MoveTo 필요):
            //   지금 Lerp 중인 다음 도착 타일(_currentNextTileCoord)에
            //   바로 건물이 새로 생긴 경우. 이때는 다음 타일 도착까지 기다리면
            //   유닛이 건물을 뚫고 그 타일 중심까지 이동해 버린다.
            //   따라서 이 케이스에만 기존처럼 코루틴을 즉시 교체한다.
            // ────────────────────────────────────────────────────────────
            if (_currentNextTileCoord.HasValue
                && !_movementUseCase.IsWalkable(_currentNextTileCoord.Value))
            {
                // 다음 도착 타일이 막혔으므로 부드러운 교체 불가 — 즉시 코루틴 재시작.
                MoveToInternal(
                    newPath,
                    UnitMovementIntentReason.BlockedRepath,
                    startNewCommand: false);
                return;
            }

            // 그 외 일반 케이스: 코루틴을 그대로 두고 새 path만 예약.
            // 이후 매 타일 도착 시점에 MoveAlongPathV3가 이 값을 소비한다.
            _pendingPath = newPath;
        }

        /// <summary>
        /// 현재 유닛이 전투 중(공격 또는 전투 이동 중)인지 판정.
        /// 공격 타겟 추적 상태로 판정합니다.
        ///
        /// 판정 기준:
        ///   - `_combatTargetTransform != null`: 전투/추격 코루틴이 진행 중일 때
        ///     `StartCombatAnimation`이 set하고, `StopCombatAnimation`이 null로 리셋합니다.
        ///   - `_isInCombatPursuit == true`: 추격 단계(아직 공격 사거리 미도달)
        ///     인 동안 EnterCombatPursuitV3가 set합니다.
        ///     이 플래그가 없으면 추격 중에 건물 변화 → RepathAllAliveUnits이 트리거되어
        ///     OnPathInvalidated가 새 path로 코루틴을 교체하고, 결과적으로 추격 코루틴이
        ///     중단되어 유닛이 한 프레임 시각적으로 멈추는 버그가 발생합니다.
        ///
        /// 호출 측(OnPathInvalidated)은 이 결과로 "이동 코루틴을 새로 시작하면 안 되는 상태"를
        /// 빠르게 식별하여, 전투 중 RepathAllAliveUnits에 의한 강제 종료를 방지합니다.
        /// </summary>
        private bool IsInCombat()
        {
            // 공격 애니메이션 중(_combatTargetTransform != null) 또는
            // 추격 코루틴 실행 중(_isInCombatPursuit) — 둘 중 하나만 참이어도 전투 상태.
            return _combatTargetTransform != null || _isInCombatPursuit;
        }

        /// <summary>
        /// 재탐색은 오래된 논리 시작점이나 FlowField cache만 보지 않고, 현재 서버
        /// Simulation Root와 이 유닛이 관측한 최신 walkability revision을 함께 검증한다.
        /// typed 결과는 실패 terminal에 보존하며 성공 path만 기존 이동 writer에 전달한다.
        /// </summary>
        private List<HexCoord> RequestLatestAuthoritativeRepath(HexCoord destination,
            bool resolveRallyGoal = false)
        {
            if (_movementUseCase == null || _unitData == null)
            {
                _lastPathRequestResult = new UnitPathRequestResult(
                    UnitPathRequestStatus.ProviderFailure,
                    null,
                    _unitData != null ? _unitData.Position : default,
                    default,
                    destination,
                    _navigationEnvironmentRevision);
                return null;
            }

            Vector3 rootDomain = ViewConverter.FromView(transform.position);
            if (resolveRallyGoal && _activeRallyPoint.HasValue)
            {
                UnitRallyPathResult rally = _movementUseCase.RequestRallyPath(_unitData,
                    _activeRallyPoint.Value, rootDomain, _navigationEnvironmentRevision);
                _lastPathRequestResult = rally.Request;
                _rallyPolicyCompletionPending = rally.Status == UnitRallyPathStatus.NoReachableCandidate;
                if (_rallyPolicyCompletionPending)
                {
                    // 일반 경로 실패나 Blocked 상태를 정상 도착으로 처리하는 분기가 아니다.
                    // 후보 소진으로 랠리를 생략하는 정책을 코루틴 종료 경계에서 한 번 소비한다.
                    // 現在 Root を同一タイルの中心に偽装した singleton は作らない。
                    _pendingPath = null;
                    return null;
                }
                if (rally.Status == UnitRallyPathStatus.CurrentTileSelected
                    && CanCompleteWithinAuthoritativeTile(rally.Request.Goal))
                    return new List<HexCoord> { rally.Request.Goal };
                if (rally.Status != UnitRallyPathStatus.PathFound)
                    return null;
                return new List<HexCoord>(rally.Request.Path);
            }
            _lastPathRequestResult = _movementUseCase.RequestAuthoritativeRepath(
                _unitData,
                rootDomain,
                destination,
                _navigationEnvironmentRevision);
            SoloRecoveryEvidence("provider", _lastPathRequestResult.Status.ToString());
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ObservePostCombatProvider();
#endif
            if (_lastPathRequestResult.Status == UnitPathRequestStatus.EmptySameTile
                && CanCompleteWithinAuthoritativeTile(destination))
                return new List<HexCoord> { destination };
            if (!_lastPathRequestResult.IsSuccess)
                return null;
            return _lastPathRequestResult.Path as List<HexCoord>
                ?? new List<HexCoord>(_lastPathRequestResult.Path);
        }

        /// <summary>
        /// 최신 권위 Root에서 앞쪽 route 중심까지 새로 찾은 prefix와, 그 중심 뒤에
        /// 남아 있는 원래 route suffix를 하나의 staged 경로로 결합한다. 이 메서드는
        /// UnitData/Transform을 쓰지 않으며, 결합 결과도 기존 이동 writer와 corridor
        /// preflight를 통과하기 전에는 적용되지 않는다.
        /// </summary>
        private static List<HexCoord> ComposePostCombatRecoveryRoute(
            IReadOnlyList<HexCoord> recoveredPrefix,
            IReadOnlyList<HexCoord> activeRoute,
            int nextWaypointIndex,
            HexCoord rejoinCandidate)
        {
            if (recoveredPrefix == null
                || recoveredPrefix.Count < 2
                || recoveredPrefix[recoveredPrefix.Count - 1] != rejoinCandidate
                || activeRoute == null
                || activeRoute.Count < 2
                || nextWaypointIndex < 1
                || nextWaypointIndex >= activeRoute.Count)
            {
                return null;
            }

            int rejoinIndex = -1;
            for (int index = nextWaypointIndex; index < activeRoute.Count; index++)
            {
                if (activeRoute[index] == rejoinCandidate)
                {
                    rejoinIndex = index;
                    break;
                }
            }
            if (rejoinIndex < 0)
                return null;

            var composed = new List<HexCoord>(
                recoveredPrefix.Count + activeRoute.Count - rejoinIndex - 1);
            for (int index = 0; index < recoveredPrefix.Count; index++)
                composed.Add(recoveredPrefix[index]);
            for (int index = rejoinIndex + 1; index < activeRoute.Count; index++)
                composed.Add(activeRoute[index]);

            return UnitPathSignature.TryCreate(composed, out _)
                ? composed
                : null;
        }

        /// <summary>
        /// 이동 중 "멈춰서 무언가를 해야 하는 대상"이 사거리 안에 있는지 판정한다(상태머신 진입 조건).
        ///
        /// 일반 전투 유닛: 감지 사거리 내 "적"이 있으면 true → 전투 이동/공격 흐름 진입.
        /// 힐러(IsHealer): 힐 사거리 내 "부상 아군"이 있으면 true → 힐 루프 진입.
        ///   힐러는 적을 감지/공격하지 않으므로 적 탐색을 아예 타지 않는다(적 공격 흐름과 분리).
        ///
        /// 이 한 메서드로 두 종류의 진입 판정을 통일하여, 이동 코루틴의 감지 지점들이
        /// 유닛 역할에 맞는 올바른 대상을 검사하도록 한다.
        /// </summary>
        private bool ShouldEngage()
        {
            if (_combatUseCase == null || _unitData == null || !_unitData.IsAlive) return false;

            return _unitData.IsHealer
                ? _combatUseCase.HasInjuredAllyInHealRange(_unitData)
                : _combatUseCase.HasEnemyInDetectRange(_unitData);
        }

        /// <summary>
        /// 아직 Transform에 적용하지 않은 서버 trajectory candidate를 기준으로 감지한다.
        /// 현재 pose를 임시로 덮어쓰지 않으므로, candidate까지 이동한 바로 그 틱에
        /// 위치를 commit하고 AcquireTarget으로 전환할 수 있다.
        /// </summary>
        private bool ShouldEngageAt(Vector3 candidatePosition)
        {
            if (_combatUseCase == null || _unitData == null || !_unitData.IsAlive)
                return false;

            return _unitData.IsHealer
                ? _combatUseCase.HasInjuredAllyInHealRangeAt(
                    _unitData, candidatePosition)
                : _combatUseCase.HasEnemyInDetectRangeAt(
                    _unitData, candidatePosition);
        }

        // ====================================================================
        // MoveAlongPathV3 — 단일 상태 머신
        // ====================================================================
        // GameSystemRules.md 새 규칙 7~10에 따른 통합 상태 머신:
        //
        //   [A* 이동] : Legacy는 타일 중심 Lerp, ReducerAuthoritative는 실제 거리 기반
        //              연속 trajectory와 waypoint 통과 checkpoint를 사용한다.
        //              두 모드 모두 매 프레임 감지하며 서버만 pose를 쓴다.
        //              감지되면 → [전투 이동]으로 전환.
        //
        //   [전투 이동] : 적의 월드 위치를 향해 직선 이동 (근접/원거리 동일).
        //                매 프레임 HasEnemyInRange 체크.
        //                사거리 진입 → [공격]으로 전환.
        //                적이 detect 사거리 이탈 → [A* 이동 재개].
        //                타겟 사망 → detect 내 다른 적 재선택 또는 [A* 이동 재개].
        //
        //   [공격] : 멈춘 채로 공격/쿨다운 루프.
        //            적이 사거리 이탈 → [전투 이동] 또는 [A* 이동 재개].
        //
        // 특징:
        //   - 근접/원거리(AttackKind) 차이는 공격 사거리 수치뿐.
        //   - 타일 점유 없이 단순 타일 중심 이동 — 다른 유닛과 같은 타일에 들어가도 문제 없음.
        //   - 원거리도 detect 진입 후 전투 이동으로 접근한다.
        //
        // 헬퍼 구조:
        //   MoveAlongPathV3            — 메인 코루틴 (서버 가드 + path 순회 + cleanup)
        //   ├─ EnterCombatPursuitV3    — [전투 이동] 적 월드 위치로 직선 이동 + 사거리 도달 시 공격
        //   │     └─ EnterCombatLoopV3 — [공격] 사거리 안에 있는 동안 공격/쿨다운 루프
        //   ├─ ResumeFromForwardTileV3 — [A* 이동 재개] 현재 위치 기준 앞쪽 타일에서 새 경로 시작
        //   └─ MoveCleanupAndCompleteV3 — 코루틴 정리 + OnMoveComplete 콜백
        // ====================================================================

        /// <summary>
        /// [2026-05-11 재설계] 단일 상태 머신 메인 코루틴.
        ///
        /// 동작 흐름:
        ///   1) 멀티플레이 가드 — 서버가 아니면 즉시 yield break.
        ///   2) path 유효성 체크 — 비정상이면 cleanup 후 종료.
        ///   3) Walk 애니메이션 시작 (1회만 CrossFade).
        ///   4) 외부 while: path를 순회한다. Legacy는 중심 Lerp, 신규 권위 모드는
        ///      다음 선분 look-ahead를 포함한 거리 기반 trajectory로 진행한다.
        ///      - 매 프레임 detect 사거리 내 적 확인.
        ///      - 감지되면 EnterCombatPursuitV3로 전투 이동 → 공격 흐름 위임.
        ///      - 전투 종료 후 ResumeFromForwardTileV3로 앞쪽 타일에서 새 경로 받아 재개.
        ///   5) 정상 완주 또는 사망이면 cleanup.
        /// </summary>
        /// <param name="path">A* 경로 (시작점 포함, 길이 ≥ 2).</param>
        // singleton은 typed 중심 완료 의도이며 A* signature/반복 예산을 위조하지 않는다.
        private bool CanCompleteWithinAuthoritativeTile(HexCoord goal)
        {
            return _unitData != null && _unitData.IsAlive && goal == _unitData.Position
                && _movementUseCase != null && _movementUseCase.IsWalkable(goal)
                && HexMetrics.WorldToHex(ViewConverter.FromView(transform.position)) == goal;
        }

        private bool TryAcceptRepath(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            out UnitRepathDecision decision)
        {
            if (candidatePath != null && candidatePath.Count == 1
                && _lastPathRequestResult.Status == UnitPathRequestStatus.EmptySameTile
                && CanCompleteWithinAuthoritativeTile(candidatePath[0]))
            {
                decision = UnitRepathDecision.AcceptedNextFrame;
                _navigationObjective?.MarkWaitingRepath();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                PostCombatTraceEvent("same-tile-center-intent");
#endif
                return true;
            }
            decision = guard != null
                ? guard.Evaluate(Time.frameCount, candidatePath)
                : UnitRepathDecision.RejectedInvalidPath;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent($"general-guard:{source}:{decision}");
#endif
            return TryAcceptRepathDecision(
                guard,
                candidatePath,
                source,
                decision);
        }

        /// <summary>
        /// spatial preflight가 recoverable unsafe로 거부한 직후의 전용 경계다.
        /// 같은 환경에서 현재 active path와 같은 후보를 다시 실행하지 않고 Blocked로
        /// 보류한다. 이 보류는 adapter failure가 아니며 환경 revision 변경만 재개시킨다.
        /// </summary>
        private bool TryAcceptSpatialRecoverableRepath(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            out UnitRepathDecision decision)
        {
            // 랠리의 현재 후보 칸 안에서 중심으로 수렴하는 입력은 이미 존재하는
            // typed singleton 계약으로만 수용한다. 일반 spatial guard를 우회하지 않는다.
            if (_activeRallyPoint.HasValue && candidatePath != null && candidatePath.Count == 1)
                return TryAcceptRepath(guard, candidatePath, source, out decision);
            decision = guard != null
                ? guard.Evaluate(
                    Time.frameCount,
                    candidatePath,
                    UnitRepathEvaluationContext.SpatialRecoverable)
                : UnitRepathDecision.RejectedInvalidPath;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent($"spatial-guard:{source}:{decision}");
#endif
            if (decision == UnitRepathDecision.RejectedRepeatedPath)
            {
                // 동일 route는 이 단계에서 곧바로 영구 Blocked가 아니다. post-combat
                // recovery planner가 다음 전방 checkpoint를 평가하고, 전체 후보의 반복
                // 소진을 확인한 뒤에만 Blocked를 승인한다.
                _navigationObjective?.MarkWaitingRepath();
                return false;
            }

            return TryAcceptRepathDecision(
                guard,
                candidatePath,
                source,
                decision);
        }

        private bool TryAcceptRepathDecision(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            UnitRepathDecision decision)
        {
            if (decision == UnitRepathDecision.AcceptedNextFrame)
            {
                _navigationObjective?.MarkWaitingRepath();
                return true;
            }

            if (decision == UnitRepathDecision.RejectedFrameBudget
                && candidatePath != null)
            {
                _pendingPath = candidatePath as List<HexCoord>
                    ?? new List<HexCoord>(candidatePath);
                _navigationObjective?.MarkWaitingRepath();
                return false;
            }

            _navigationObjective?.MarkBlocked();
            LogRepathFailClosed(guard, candidatePath, source, decision);
            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ObserveMovementAuthorityAdapterFailure(
            string kind,
            string context)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            UnitMovementAuthorityObserver.ObserveAdapterFailure(kind, context);
#endif
        }

        private void LogRepathFailClosed(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> candidatePath,
            string source,
            UnitRepathDecision decision,
            string failedWaypointOverride = null)
        {
            string unitId = _unitData != null ? _unitData.Id.ToString() : "null";
            UnitPathSignature.TryCreate(
                candidatePath, out UnitPathSignature candidateSignature);
            string failedWaypoint = failedWaypointOverride
                ?? (candidatePath != null && candidatePath.Count > 1
                    ? candidatePath[1].ToString()
                    : "unavailable");
            string candidatePathStart = candidatePath != null
                && candidatePath.Count > 0
                    ? candidatePath[0].ToString()
                    : "unavailable";
            string corridorEvidence = source == "astar-corridor"
                ? FormatCorridorRepathEvidence(_lastCorridorRepathEvidence)
                : "not-applicable";
            ObserveMovementAuthorityAdapterFailure("repath-fail-closed",
                $"[UAS-MOVE][REPATH-FAIL-CLOSED] unitId={unitId}, " +
                $"commandRevision={_movementCommandRevision}, " +
                $"segmentRevision={_movementSegmentRevision}, " +
                $"source={source}, decision={decision}, frame={Time.frameCount}, " +
                $"failedWaypoint={failedWaypoint}, " +
                $"candidatePathStart={candidatePathStart}, " +
                $"previousPath={FormatPathSignature(guard?.PreviousPath ?? default)}, " +
                $"currentPath={FormatPathSignature(guard?.CurrentPath ?? default)}, " +
                $"candidatePath={FormatPathSignature(candidateSignature)}, " +
                $"acceptedInFrame={guard?.AcceptedInFrame ?? 0}, " +
                $"acceptedWithoutProgress={guard?.AcceptedWithoutProgress ?? 0}, " +
                $"pathStatus={_lastPathRequestResult.Status}, " +
                $"authoritativeStart={_lastPathRequestResult.AuthoritativeStart}, " +
                $"simulationRootTile={_lastPathRequestResult.RootTile}, " +
                $"goal={_lastPathRequestResult.Goal}, " +
                $"environmentRevision={_lastPathRequestResult.EnvironmentRevision}, " +
                $"corridorEvidence={corridorEvidence}");
        }

        private static string FormatCorridorRepathEvidence(
            CorridorRepathEvidence evidence)
        {
            if (!evidence.IsAvailable)
                return "unavailable";

            return
                $"frame:{evidence.Frame}|" +
                $"rootView:{FormatVector3(evidence.RootView)}|" +
                $"rootDomain:{FormatVector3(evidence.RootDomain)}|" +
                $"rootHex:{evidence.RootHex}|" +
                $"unitDataPosition:{evidence.UnitDataPosition}|" +
                $"sourcePathStart:{evidence.PathStart}|" +
                $"sourceWaypoint:{evidence.CurrentWaypoint}|" +
                $"sourceWaypointIndex:{evidence.WaypointIndex}/{evidence.PathCount - 1}|" +
                $"sourcePath:{FormatPathSignature(evidence.SourcePathSignature)}|" +
                $"sampleView:{FormatVector3(evidence.SampleView)}|" +
                $"sampleHex:{evidence.SampleHex}|" +
                $"sampleIndex:{evidence.SampleIndex}/{evidence.SampleCount}|" +
                $"matchedPathIndex:{evidence.MatchedPathIndex}|" +
                $"allowedPathIndices:{evidence.FirstAllowedPathIndex}-{evidence.LastAllowedPathIndex}|" +
                $"sampleWalkable:{evidence.IsWalkable}|" +
                $"sampleResult:{evidence.SampleResult}";
        }

        private static string FormatVector3(Vector3 value)
            => $"({value.x:F6},{value.y:F6},{value.z:F6})";

        private static string FormatPathSignature(UnitPathSignature signature)
            => signature.IsValid
                ? $"{signature.WaypointCount}:{signature.OrderedHash:x16}"
                : "invalid";

        private void ObserveNavigationProgress(
            UnitRepathProgressGuard guard,
            IReadOnlyList<HexCoord> path)
        {
            if (_navigationObjective != null && _navigationObjective.IsActive)
                _navigationObjective.ObserveProgress(path);
            else
                guard?.ObserveProgress(path);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_postCombatTrace != null && _postCombatTrace.Active && !_postCombatTrace.Retired
                && !_postCombatTrace.ProgressObserved)
            {
                _postCombatTrace.ProgressObserved = true;
                PostCombatTraceEvent("first-positive-navigation-progress");
            }
#endif
        }

        private IEnumerator MoveAlongPathV3(List<HexCoord> path)
        {
            // ── 멀티플레이 가드 ────────────────────────────────────────────
            // 서버가 아닌 클라이언트는 NetworkTransform이 위치를 보간 동기화하므로 코루틴을 돌리지 않는다.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            {
                _moveCoroutine = null;
                yield break;
            }

            // path가 비정상(null / 길이 1 이하 / 유닛 사망 등)이면 즉시 cleanup 후 종료.
            if (path == null || path.Count == 0 || _unitData == null || !_unitData.IsAlive
                || (path.Count == 1 && !CanCompleteWithinAuthoritativeTile(path[0])))
            {
                MoveCleanupAndCompleteV3();
                yield break;
            }

            _rootRotationOwnership.TryAcquireMovement();

            // 멀티플레이 서버: 이동 시작 이벤트 발행 → NetworkCombatController가
            // _animState(=Walk) 레벨 동기화로 모든 클라이언트에 Walk 시작 전파.
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // Walk 애니메이션 시작 — 이동 시작 시 1회만 CrossFade.
            // 매 타일마다 CrossFade하면 블렌딩이 매번 리셋되어 Walk 모션이 끊긴다.
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
                _currentAnimStateHash = StateWalk;
            }

            // 혼잡도 기여 활성화 — 본 코루틴이 도는 동안 새 타일 진입 시
            // GameEvents.OnUnitEnteredTile을 발행한다. 전투 추격 진입 시 false로 바뀌고,
            // resumePath로 A* 재개 시 다시 true로 돌아온다.
            _isAStarMoving = true;

            // 최종 목적지(외부 while 동안 변하지 않음). 우회/재경로 모두 이 좌표를 기준.
            HexCoord finalTarget = path[path.Count - 1];
            var pathCheckpoint = new UnitPathCheckpointTracker();
            var repathGuard = _navigationObjective != null
                && _navigationObjective.IsActive
                ? _navigationObjective.RepathGuard
                : new UnitRepathProgressGuard(path);
            // 전투 위치에서 기존 route로 돌아갈 때 단일 A* 실패를 영구 Blocked로
            // 바꾸지 않도록, 현재 이동 코루틴 수명 동안 복귀 후보 소진 이력을 보존한다.
            // 순수 planner는 후보만 고르고 실제 Root/path commit은 아래의 기존 writer와
            // corridor preflight가 계속 담당한다.
            var postCombatRecoveryPlanner = new UnitPostCombatRecoveryPlanner();
            bool movementFailedClosed = false;
            bool navigationBlocked = false;
            SetMovementHeldAnimation(false);

            // Legacy 시간 환산과 신규 trajectory가 같은 기본 MoveSpeed를 사용한다.
            // 연구·둔화·빙결 배율은 각 frame에서 다시 읽어 즉시 반영한다.
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;
            float worldSpeed = HexMetrics.TileHeight / moveSeconds;

            // ────────────────────────────────────────────────────────────────
            // 외부 while — path가 새로 발급될 때마다 위로 돌아와 새 path로 순회.
            //   재경로 사유:
            //     (A) 전투 종료 후 ResumeFromForwardTileV3가 새 path를 반환.
            //   정상 완주 또는 사망이면 외부 while 자체가 break.
            //
            //   타일에 다른 유닛이 있어도 그대로 진입하며, 같은 타일에서 시각적으로 겹치는 것은
            //   허용된다(분산은 별도 시스템 책임이 아님).
            // ────────────────────────────────────────────────────────────────
            while (_unitData != null && _unitData.IsAlive)
            {
                // 랠리 후보가 없거나 현재 권위 타일을 후보로 고른 정책 결과만 여기서
                // 정상 종료한다. 일반 경로 실패/Blocked는 이 조건을 세우지 않는다.
                if (_rallyPolicyCompletionPending) break;
                // 여러 frame의 무진전 상한 또는 invalid path에 도달하면 코루틴을 종료하지 않는다.
                // 종료하면 IsMoving이 false가 되어 공성 ticker가 같은 목표를 새 command로 재발행하고
                // guard가 초기화된다. 현재 pose와 코루틴을 유지한 채 walkability revision 또는
                // OnPathInvalidated가 제공한 새 PendingPath만 기다린다.
                if (navigationBlocked)
                {
                    SoloRecoveryEvidence("blocked", "navigation-blocked-loop");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    ObservePostCombatBlocked(repathGuard, -1, "navigation-blocked-loop");
#endif
                    SetMovementHeldAnimation(true);
                    ulong blockedEnvironmentRevision =
                        _navigationEnvironmentRevision;
                    while (_unitData != null
                        && _unitData.IsAlive
                        && _pendingPath == null
                        && !_rallyPolicyCompletionPending
                        && _navigationEnvironmentRevision
                            == blockedEnvironmentRevision)
                    {
                        yield return null;
                    }

                    if (_unitData == null || !_unitData.IsAlive || _rallyPolicyCompletionPending)
                        break;

                    List<HexCoord> recoveryPath = _pendingPath;
                    _pendingPath = null;
                    if (recoveryPath == null || recoveryPath.Count < 2)
                    {
                        recoveryPath = RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                        if (_rallyPolicyCompletionPending) goto cleanup;
                    }

                    if (!TryAcceptRepath(
                            repathGuard,
                            recoveryPath,
                            "blocked-environment-resume",
                            out UnitRepathDecision blockedDecision))
                    {
                        // 새 환경에서도 아직 경로가 없으면 다시 Blocked로 남는다. 로그는 환경 변화
                        // 한 번당 terminal 1회만 기록되고 매 frame 재시도하지 않는다.
                        yield return null;
                        continue;
                    }

                    BeginMovementShadowSegment(
                        UnitMovementIntentReason.BlockedRepath);
                    path = recoveryPath;
                    finalTarget = path[path.Count - 1];
                    pathCheckpoint.Reset();
                    navigationBlocked = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    PostCombatTraceEvent("blocked-environment-path-accepted");
#endif
                    _navigationObjective?.MarkWaitingRepath();

                    // 수락한 재경로는 반드시 다음 frame부터 planner에 입력한다.
                    yield return null;
                }

                if (_activeRallyPoint.HasValue && path != null && path.Count > 0)
                {
                    // 새 경로가 수용돼 outer loop에 들어온 시점에 경로/목적지/objective를
                    // 함께 맞춘다. 같은 guard 객체와 환경 revision은 그대로 유지한다.
                    finalTarget = path[path.Count - 1];
                    _currentDestination = finalTarget;
                    _navigationObjective?.RetargetRallyEndpoint(finalTarget);
                    if (path.Count == 1 && _pendingPath != null && _pendingPath.Count == 1)
                        _pendingPath = null;
                }
                _navigationObjective?.MarkNavigating();
                SetMovementHeldAnimation(false);

                if (path.Count == 1)
                {
                    Vector3 center = ViewConverter.ToView(HexMetrics.HexToWorld(finalTarget));
                    center.y += HexMetrics.UnitYOffset;
                    // 이 배열은 경로가 아닌 한 타일 내부 sweep의 두 끝점이다.
                    var centerCorridor = new List<HexCoord> { finalTarget, finalTarget };
                    BeginMovementShadowSegment(UnitMovementIntentReason.PostCombatResume);
                    while (_unitData != null && _unitData.IsAlive
                        && CalculateMovementPositionDeltaXZ(transform.position, center)
                            > (float)UnitMovementEvaluationAdapter.PositionTolerance)
                    {
                        if (ShouldEngage())
                        {
                            yield return EnterCombatPursuitV3();
                            if (!CanCompleteWithinAuthoritativeTile(finalTarget))
                            {
                                path = RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                                if (_rallyPolicyCompletionPending) goto cleanup;
                                navigationBlocked = path == null;
                                break;
                            }
                            BeginMovementShadowSegment(UnitMovementIntentReason.PostCombatResume);
                        }
                        if (!CanCompleteWithinAuthoritativeTile(finalTarget))
                        {
                            navigationBlocked = true;
                            break;
                        }
                        float multiplier = _combatUseCase != null
                            ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData)) : 1f;
                        SetFrozenAnimation(multiplier <= MoveFreezeEpsilon);
                        Vector3 centerCandidate = Vector3.MoveTowards(transform.position, center,
                            worldSpeed * multiplier * Time.deltaTime);
                        Quaternion centerRotation = transform.rotation;
                        Vector3 centerDirection = center - transform.position;
                        centerDirection.y = 0f;
                        if (centerDirection.sqrMagnitude > 0f)
                            centerRotation = Quaternion.LookRotation(centerDirection);
                        MovementWriteOutcome outcome = ApplyMovementAuthorityFrame(center,
                            centerCandidate, centerRotation, UnitMovementIntentReason.PostCombatResume,
                            false, maximumTravelDistanceWorld: worldSpeed * multiplier * Time.deltaTime,
                            logicalCorridorPath: centerCorridor, logicalCorridorWaypointIndex: 1);
                        if (outcome == MovementWriteOutcome.Rejected || outcome == MovementWriteOutcome.RepathRequired)
                        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            CapturePostCombatTrace(path, centerCorridor, 1, finalTarget, false, outcome);
#endif
                            navigationBlocked = true;
                            break;
                        }
                        yield return null;
                    }
                    if (navigationBlocked) continue;
                    if (path != null && path.Count > 1) { yield return null; continue; }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    PostCombatTraceEvent("same-tile-center-completed");
#endif
                    SoloRecoveryEvidence("completed", "same-tile-center-completed");
                    break;
                }

                // 새 path를 받아 다시 시작할지 결정하는 플래그.
                // for 안에서 true로 바꾼 뒤 break하면 외부 while로 빠져 새 path로 continue.
                bool needRepath = false;

                // 멀티 Legacy만 중심 도착의 from을 유지하고 SOLO/Reducer는 실제 공간 커밋을 따른다.
                HexCoord prevActualTile = _unitData.Position;

                // ────────────────────────────────────────────────────────────
                // 타일 순회 for — path[0]은 시작점이므로 1부터.
                // ────────────────────────────────────────────────────────────
                for (int i = 1; i < path.Count; i++)
                {
                    // 사망/소실 체크 — 매 타일 진입 시 안전 장치.
                    if (_unitData == null || !_unitData.IsAlive) break;

                    HexCoord from = prevActualTile;
                    HexCoord to = path[i];

                    // 마지막 스텝이 non-walkable(성/건물)인지 — ProcessStep을 건너뛰는 기준.
                    bool isLastStep = (i == path.Count - 1);
                    bool isLastStepToNonWalkable = isLastStep
                        && (_movementUseCase != null && !_movementUseCase.IsWalkable(to));

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] 도메인 방향(_unitData.Facing) 갱신 — HexDirection 단위의 도메인 상태 추적용.
                    //   시각적 회전은 ApplyDirection으로 즉시 스냅하지 않고,
                    //   아래 Lerp 루프에서 매 프레임 RotateTowards로 부드럽게 회전한다.
                    //   (즉시 스냅하면 타일 전환 순간 Y축 회전이 점프하여 시각적으로 부자연스럽다.)
                    // ──────────────────────────────────────────────────────
                    HexDirection dir = FacingDirection.FromCoords(from, to);
                    dir = ViewConverter.FlipDirection(dir);
                    if (_lockedMovementPipelineMode
                        != UnitMovementPipelineMode.ReducerAuthoritative)
                    {
                        _unitData.Facing = dir;
                    }

                    // ──────────────────────────────────────────────────────
                    // [A* 경로 입력] 모든 waypoint는 타일 중심의 권위 checkpoint다.
                    // 현재 점과 다음 점으로 corridor 안의 연속 trajectory를 만들되,
                    // 중간·마지막 목적지 모두 실제 중심 도달 뒤에만 소비한다.
                    // ──────────────────────────────────────────────────────
                    Vector3 toDomain = HexMetrics.HexToWorld(to);

                    // Lerp 출발점은 현재 위치(직전 도착 위치를 그대로 이어받음).
                    Vector3 fromPos = transform.position;

                    // 타일 중심 = 도메인 좌표 → 뷰 좌표 변환 + 유닛 높이 보정.
                    Vector3 toPos = ViewConverter.ToView(toDomain);
                    toPos.y += HexMetrics.UnitYOffset;
                    CaptureUnitActionShadowDesiredTarget(toPos);

                    // 정상 코너는 현재 타일 중심에 도착한 뒤 다음 방향을 처음 보는 것이 아니라,
                    // 현재 waypoint에 접근하는 동안 다음 선분을 미리 보아야 연속 선회가 된다.
                    // 마지막 waypoint에는 다음 점이 없으므로 정확한 목적지 중심에 수렴한다.
                    Vector3? nextTrajectoryTarget = null;
                    if (i + 1 < path.Count)
                    {
                        Vector3 nextDomain = HexMetrics.HexToWorld(path[i + 1]);
                        Vector3 nextView = ViewConverter.ToView(nextDomain);
                        nextView.y += HexMetrics.UnitYOffset;
                        nextTrajectoryTarget = nextView;
                    }

                    // ──────────────────────────────────────────────────────
                    // [회전 시스템 변경 — 2026-05-14] 목표 각도 계산 — toPos 정의 직후 1회만.
                    //   CalculateAttackAngle은 (toPos - 현재 위치) 벡터의 XZ 평면 Atan2 결과를 도 단위로 반환한다.
                    //   이 각도는 Lerp 루프 시작 시점의 출발 위치 기준이며, 루프 도중에는 갱신하지 않는다.
                    //   (1 타일 거리이므로 출발 시점 방향과 도착 시점 방향이 거의 동일.)
                    // ──────────────────────────────────────────────────────
                    float targetAngle = CalculateAttackAngle(toPos);
                    Quaternion targetRot = Quaternion.Euler(0f, targetAngle, 0f);

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] Legacy Lerp 또는 신규 거리 기반 trajectory + 매 프레임 감지.
                    //   - detect 진입 → EnterCombatPursuitV3로 [전투 이동] 위임.
                    //   - 근접/원거리 구분 없음. 차이는 EnterCombatPursuitV3 내부의 사거리 수치뿐.
                    // ──────────────────────────────────────────────────────
                    float elapsed = 0f;
                    float targetDuration = moveSeconds;
                    bool reducerTrajectory = _lockedMovementPipelineMode
                        == UnitMovementPipelineMode.ReducerAuthoritative;
                    bool authoritativeWaypointReached = false;
                    Func<Vector3, bool> candidateEngagePredicate = reducerTrajectory
                        ? (Func<Vector3, bool>)ShouldEngageAt
                        : null;

                    // [전투 이동]으로 전환되었음을 표시. true면 Lerp while 탈출 후
                    // toPos 강제 스냅과 ProcessStep을 모두 건너뛰고 새 path로 외부 while 재진입.
                    bool interruptedByCombat = false;

                    // 지금 Lerp 중인 "다음 도착 타일" 좌표를 기록.
                    // OnPathInvalidated가 호출됐을 때 이 타일에 건물이 새로 생긴 경우만
                    // 즉시 코루틴을 재시작해야 하므로 외부에서 검사할 수 있도록 노출한다.
                    _currentNextTileCoord = to;

                    while ((reducerTrajectory
                            ? !authoritativeWaypointReached
                            : elapsed < targetDuration)
                        && _unitData != null
                        && _unitData.IsAlive)
                    {
                        float liveMoveMultiplier = _combatUseCase != null
                            ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData))
                            : 1f;
                        bool movementFrozen = liveMoveMultiplier <= MoveFreezeEpsilon;
                        SetFrozenAnimation(movementFrozen);

                        float candidateElapsed = elapsed
                            + (movementFrozen ? 0f : Time.deltaTime * liveMoveMultiplier);
                        float t = Mathf.Clamp01(candidateElapsed / targetDuration);
                        Vector3 candidatePosition = Vector3.Lerp(fromPos, toPos, t);
                        bool shouldEngage = ShouldEngage();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        UnitMovementShadowObserver.LegacyFrameToken movementShadowToken = default;
                        if (_lockedMovementPipelineMode
                            != UnitMovementPipelineMode.ReducerAuthoritative)
                        {
                            movementShadowToken = BeginMovementShadowLegacyFrame(
                                    toPos,
                                    UnitMovementIntentReason.AStarPath,
                                    CalculateMovementShadowPositionDeltaXZ(
                                        transform.position,
                                        candidatePosition));
                        }
#endif
                        Vector3 positionBeforeAuthorityFrame = transform.position;
                        MovementWriteOutcome movementOutcome = ApplyMovementAuthorityFrame(
                            toPos,
                            candidatePosition,
                            targetRot,
                            UnitMovementIntentReason.AStarPath,
                            shouldEngage,
                            nextTrajectoryTarget,
                            reducerTrajectory
                                ? worldSpeed * liveMoveMultiplier * Time.deltaTime
                                : -1f,
                            reducerTrajectory ? path : null,
                            reducerTrajectory ? i : -1,
                            candidateEngagePredicate);
                        if (movementOutcome == MovementWriteOutcome.RepathRequired)
                        {
                            List<HexCoord> blockedRepath =
                                RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                            if (_rallyPolicyCompletionPending) goto cleanup;
                            if (!TryAcceptSpatialRecoverableRepath(
                                    repathGuard,
                                    blockedRepath,
                                    "astar-corridor",
                                    out UnitRepathDecision repathDecision))
                            {
                                navigationBlocked = true;
                                needRepath = true;
                                _currentNextTileCoord = null;
                                break;
                            }

                            BeginMovementShadowSegment(
                                UnitMovementIntentReason.BlockedRepath);
                            path = blockedRepath;
                            pathCheckpoint.Reset();
                            needRepath = true;
                            SetMovementHeldAnimation(true);
                            _currentNextTileCoord = null;
                            break;
                        }
                        if (movementOutcome == MovementWriteOutcome.Rejected)
                        {
                            ObserveMovementAuthorityAdapterFailure("authority-frame-rejected",
                                $"[UAS-MOVE] 권위 이동 frame 거부. unitId={_unitData.Id}, " +
                                $"commandRevision={_movementCommandRevision}, " +
                                $"segmentRevision={_movementSegmentRevision}");
                            movementFailedClosed = true;
                            goto cleanup;
                        }
                        if (movementOutcome == MovementWriteOutcome.Advanced)
                        {
                            Vector3 committedDelta =
                                transform.position - positionBeforeAuthorityFrame;
                            if (_lastMovementTrajectoryReachedWaypoint
                                || committedDelta.x * committedDelta.x
                                    + committedDelta.z * committedDelta.z
                                    > (float)(UnitMovementEvaluationAdapter.PositionTolerance
                                        * UnitMovementEvaluationAdapter.PositionTolerance))
                            {
                                ObserveNavigationProgress(repathGuard, path);
                            }

                            if (reducerTrajectory)
                            {
                                authoritativeWaypointReached =
                                    _lastMovementTrajectoryReachedWaypoint;
                            }
                            else
                            {
                                elapsed = candidateElapsed;
                            }
                        }
                        shouldEngage = shouldEngage
                            || _lastMovementCandidateAcquiredTarget;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        CompleteMovementShadowLegacyFrame(movementShadowToken);
#endif

                        // ── [A* 이동] → [전투 이동 / 힐] 전환 ──
                        // 감지 사거리 안에 대상(일반=적 / 힐러=부상 아군)이 있으면 즉시 위임.
                        // ShouldEngage()가 유닛 역할에 맞는 대상을 검사한다(힐러는 적을 타지 않음).
                        if (shouldEngage)
                        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            UnitMovementShadowObserver.ObserveTargetAcquirePriority(
                                movementShadowToken);
#endif
                            // 혼잡도 기여 일시 중단 — 추격 단계는 타일을 거치지 않는
                            // 직선 이동이므로 OnUnitEnteredTile 발행을 막는다. resumePath에서 다시 true.
                            _isAStarMoving = false;

                            // 전투 추격 진입 — 더 이상 "다음 도착 타일"을 향해 가지 않으므로
                            // OnPathInvalidated가 잘못된 타일을 검사하지 않도록 즉시 null로 비운다.
                            // (IsInCombat()가 true가 되어 OnPathInvalidated가 사실상 early return하지만,
                            //  방어적으로 정합 상태 유지.)
                            _currentNextTileCoord = null;

                            if (_unitData.IsHealer)
                            {
                                // [힐러] 부상 아군 힐 루프에 위임 — 적 공격 흐름과 완전히 분리된 경로.
                                //   정지 → 힐 애니 → HoT 부여 → 쿨다운 → 재탐색을 EnterHealLoopV3가 담당한다.
                                _rootRotationOwnership.TransferToAction();
                                yield return EnterHealLoopV3();
                                _rootRotationOwnership.ReleaseAction(
                                    resumeMovement: _unitData != null
                                        && _unitData.IsAlive);
                            }
                            else
                            {
                                // [전투 이동] → [공격] 흐름을 EnterCombatPursuitV3에 전부 위임.
                                yield return EnterCombatPursuitV3();

                                // 전투 종료 후 회전 정합성 유지.
                                // _combatTargetTransform이 null이 아닌 동안 Update()가 매 프레임 회전을 덮어쓰므로
                                // 전투 추격 내부의 gameplay 종료 경계가 추적과 소유권을 직접 닫는다.
                            }

                            if (_unitData == null || !_unitData.IsAlive) break;

                            // Chase의 실제 Root 전이가 Missing/NonWalkable로 무효화된 경우에는
                            // Rejected cleanup이나 복귀 정렬을 수행하지 않는다. 추격 코루틴이 이미
                            // 최소 한 frame held했으므로 여기서 기존 objective/guard를 통해 새 A*를
                            // 받고 다음 외부 while부터 적용한다.
                            if (_combatPursuitRequiresRepath)
                            {
                                _combatPursuitRequiresRepath = false;
                                List<HexCoord> pursuitRepath =
                                    RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                                if (_rallyPolicyCompletionPending) goto cleanup;
                                if (!TryAcceptSpatialRecoverableRepath(
                                        repathGuard,
                                        pursuitRepath,
                                        "chase-spatial-route-invalidated",
                                        out UnitRepathDecision pursuitRepathDecision))
                                {
                                    navigationBlocked = true;
                                    needRepath = true;
                                    _currentNextTileCoord = null;
                                    break;
                                }

                                BeginMovementShadowSegment(
                                    UnitMovementIntentReason.BlockedRepath);
                                path = pursuitRepath;
                                pathCheckpoint.Reset();
                                needRepath = true;
                                _isAStarMoving = true;
                                SetMovementHeldAnimation(true);
                                _currentNextTileCoord = null;
                                break;
                            }

                            // 전투 후 복귀 중 대상을 다시 발견해도 이 MoveAlongPathV3 수명은
                            // 끝내지 않는다. 아래 순수 flow가 재추격과 새 복귀 계산 사이에
                            // 최소 한 frame 경계를 강제하여, 즉시 반환되는 추격에서도 같은
                            // frame 무한 반복이나 objective 조기 Complete가 생기지 않게 한다.
                            var postCombatReentryFlow = new UnitPostCombatReentryFlow();
                            UnitPostCombatReentryResult recoveryStart =
                                postCombatReentryFlow.BeginRecovery(Time.frameCount);
                            if (recoveryStart.Action
                                != UnitPostCombatReentryAction.RecalculateRecovery)
                            {
                                ObserveMovementAuthorityAdapterFailure(
                                    "post-combat-reentry-start-invalid",
                                    $"[UAS-MOVE] 전투 후 복귀 수명 시작이 유효하지 않습니다. " +
                                    $"unitId={_unitData.Id}, frame={Time.frameCount}");
                                movementFailedClosed = true;
                                goto cleanup;
                            }

                            BeginSoloRecoveryEvidence();
                        recalculatePostCombatRecovery:
                            BeginMovementShadowSegment(UnitMovementIntentReason.PostCombatResume);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            BeginPostCombatTrace(path, i, finalTarget);
#endif
                            // ──────────────────────────────────────────────────────────
                            // [BUG-002 수정 — 2026-05-13] 전투 종료 → 앞쪽 타일까지 "걸어서" 정렬.
                            //
                            //   왜 필요한가:
                            //     전투는 타일 중심 밖에서 끝날 수 있다. 현재 SOLO/Reducer는 실제
                            //     경계 전이를 이미 커밋하지만, 경로의 중심 checkpoint는 별개다.
                            //     현재 pose에서 안전한 복귀 중심까지 걸어간 뒤 경로를 이어야 한다.
                            //
                            //   해결:
                            //     1) walkable·전방·도달 가능 조건을 만족하는 인접 중심을 1회 결정.
                            //     2) 그 타일 중심 뷰 좌표(alignView)까지 동일 이동 속도로 Lerp.
                            //        (규칙 5: A* 이동과 동일 속도 — moveSeconds는 1칸 이동 시간이므로
                            //         실제 거리 / TileHeight 비율을 곱해 시간 환산.)
                            //     3) Lerp 도중에도 detect 사거리 적 감지 시 즉시 중단 → 새 추격 진입.
                            //     4) planner가 남은 이동량을 중심까지 제한해 실제로 도달.
                            //     5) 같은 forwardTile을 ResumeFromForwardTileV3에 전달해 도메인 갱신 +
                            //        새 path 발급. (복귀 후보 이중 계산 방지)
                            // ──────────────────────────────────────────────────────────

                            // 1) 안전한 forwardTile 계산 — 호출은 단 1회. 후보가 없으면
                            // nearestTile을 성공으로 오인하지 않고 현재 objective의 서버 A*를
                            // 재평가한다.
                            Vector3 unitDomainPos = ViewConverter.FromView(transform.position);
                            if (!_movementUseCase.TryFindReachableForwardRejoinTile(
                                    _unitData,
                                    unitDomainPos,
                                    finalTarget,
                                    out HexCoord forwardTile,
                                    path))
                            {
                                List<HexCoord> safeResumePath =
                                    RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                                if (_rallyPolicyCompletionPending) goto cleanup;
                                // 직접 중심 후보 부재는 spatial unsafe 증거가 아니다.
                                // 유효 동일 route는 다음 frame의 최신 pose에서 안전 검사한다.
                                if (!TryAcceptRepath(
                                        repathGuard,
                                        safeResumePath,
                                        "post-combat-no-safe-forward-center",
                                        out UnitRepathDecision safeResumeDecision))
                                {
                                    navigationBlocked = safeResumeDecision != UnitRepathDecision.RejectedFrameBudget;
                                }

                                path = safeResumePath;
                                pathCheckpoint.Reset();
                                needRepath = true;
                                SetMovementHeldAnimation(true);
                                _currentNextTileCoord = null;
                                break;
                            }

                            // 2) forwardTile 중심 뷰 좌표.
                            Vector3 forwardWorld = HexMetrics.HexToWorld(forwardTile);
                            Vector3 alignView = ViewConverter.ToView(forwardWorld);
                            alignView.y += HexMetrics.UnitYOffset;
                            CaptureUnitActionShadowDesiredTarget(alignView);

                            Vector3 alignFromPos = transform.position;
                            float alignDist = Vector3.Distance(alignFromPos, alignView);

                            // 동일 이동 속도 환산: moveSeconds는 1 타일 거리 이동에 필요한 시간이므로,
                            // 실제 정렬 거리에 비례해 시간을 줄이거나 늘린다.
                            // 거리 0(또는 매우 작음)이면 Lerp 자체를 생략하고 즉시 스냅.
                            float alignDuration = (HexMetrics.TileHeight > 0f)
                                ? (alignDist / HexMetrics.TileHeight) * moveSeconds
                                : 0f;

                            // 정렬 Lerp 중에 새 적이 감지되면 다시 추격에 진입해야 하므로 별도 플래그.
                            bool alignInterruptedByCombat = false;

                            // 정렬 방향 계산 — unitDomainPos가 속한 타일에서 forwardTile로 가는 HexDirection.
                            HexCoord nearestTile = HexMetrics.WorldToHex(unitDomainPos);
                            HexDirection alignDir = FacingDirection.FromCoords(nearestTile, forwardTile);
                            // 전투 위치에서 A*로 복귀하는 구간도 Collider가 아니라 현재/앞쪽
                            // 타일을 연결한 logical corridor로 Simulation Root point를 검사한다.
                            var resumeCorridor = new List<HexCoord>(2)
                            {
                                nearestTile,
                                forwardTile
                            };
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            if (_postCombatTrace != null && !_postCombatTrace.Retired)
                            {
                                _postCombatTrace.Center = alignView;
                                _postCombatTrace.HasCenter = true;
                            }
                            CapturePostCombatTrace(path, resumeCorridor, i, finalTarget, false, MovementWriteOutcome.Held);
#endif

                            // ──────────────────────────────────────────────────────
                            // [회전 시스템 변경 — 2026-05-14] 정렬 회전을 RotateTowards로 점진 회전.
                            //   기존: ApplyDirection으로 Lerp 시작 직전에 Y축 회전을 즉시 스냅.
                            //     문제 - 전투 종료 후 적 방향(스냅된 회전)에서 정렬 방향으로 점프하여 시각적으로 부자연스러움.
                            //   변경: 목표 각도(alignTargetRot)를 정해두고 Lerp 루프에서 매 프레임 누적 회전.
                            //     CalculateAttackAngle(alignView): 현재 위치에서 정렬 목적지 뷰 좌표까지의 XZ 방향 각도.
                            //   _unitData.Facing은 도메인 상태 추적 목적으로 기존처럼 HexDirection으로 갱신 유지.
                            // ──────────────────────────────────────────────────────
                            HexDirection alignViewDir = ViewConverter.FlipDirection(alignDir);
                            if (_lockedMovementPipelineMode
                                != UnitMovementPipelineMode.ReducerAuthoritative)
                            {
                                _unitData.Facing = alignViewDir;
                            }

                            float alignTargetAngle = CalculateAttackAngle(alignView);
                            Quaternion alignTargetRot = Quaternion.Euler(0f, alignTargetAngle, 0f);

                            if (alignDuration > 0.0001f)
                            {
                                float alignElapsed = 0f;
                                bool reducerResumeTrajectory = _lockedMovementPipelineMode
                                    == UnitMovementPipelineMode.ReducerAuthoritative;
                                bool resumeWaypointReached = false;
                                while ((reducerResumeTrajectory
                                        ? !resumeWaypointReached
                                        : alignElapsed < alignDuration)
                                    && _unitData != null
                                    && _unitData.IsAlive)
                                {
                                    float alignMoveMultiplier = _combatUseCase != null
                                        ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData))
                                        : 1f;
                                    bool alignMovementFrozen =
                                        alignMoveMultiplier <= MoveFreezeEpsilon;
                                    SetFrozenAnimation(alignMovementFrozen);

                                    float candidateAlignElapsed = alignElapsed
                                        + (alignMovementFrozen
                                            ? 0f
                                            : Time.deltaTime * alignMoveMultiplier);
                                    float at = Mathf.Clamp01(candidateAlignElapsed / alignDuration);
                                    Vector3 candidateAlignPosition = Vector3.Lerp(
                                        alignFromPos,
                                        alignView,
                                        at);
                                    bool shouldReengage = ShouldEngage();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                    UnitMovementShadowObserver.LegacyFrameToken resumeShadowToken = default;
                                    if (_lockedMovementPipelineMode
                                        != UnitMovementPipelineMode.ReducerAuthoritative)
                                    {
                                        resumeShadowToken = BeginMovementShadowLegacyFrame(
                                            alignView,
                                            UnitMovementIntentReason.PostCombatResume,
                                            CalculateMovementShadowPositionDeltaXZ(
                                                transform.position,
                                                candidateAlignPosition));
                                    }
#endif
                                    Vector3 positionBeforeAlignFrame = transform.position;
                                    MovementWriteOutcome alignOutcome = ApplyMovementAuthorityFrame(
                                        alignView,
                                        candidateAlignPosition,
                                        alignTargetRot,
                                        UnitMovementIntentReason.PostCombatResume,
                                        shouldReengage,
                                        nextTrajectoryTarget: null,
                                        maximumTravelDistanceWorld:
                                            reducerResumeTrajectory
                                                ? worldSpeed * alignMoveMultiplier * Time.deltaTime
                                                : -1f,
                                        logicalCorridorPath:
                                            reducerResumeTrajectory
                                                ? resumeCorridor
                                                : null,
                                        logicalCorridorWaypointIndex:
                                            reducerResumeTrajectory ? 1 : -1,
                                        candidateAcquirePredicate:
                                            reducerResumeTrajectory
                                                ? (Func<Vector3, bool>)ShouldEngageAt
                                                : null);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                    // 재탐색 전에 최종 corridor 판정을 저장하고 최초 실패를 내보낸다.
                                    if (alignOutcome == MovementWriteOutcome.RepathRequired
                                        || alignOutcome == MovementWriteOutcome.Rejected
                                        || shouldReengage || _lastMovementCandidateAcquiredTarget || _lastMovementTrajectoryReachedWaypoint)
                                        CapturePostCombatTrace(path, resumeCorridor, i, finalTarget,
                                            shouldReengage || _lastMovementCandidateAcquiredTarget, alignOutcome);
#endif
                                    if (alignOutcome == MovementWriteOutcome.RepathRequired)
                                    {
                                        // 건물 등으로 통로가 바뀌면 기존 route의 앞쪽 중심을
                                        // 결정적인 순서로 frame당 하나씩 질의한다. 한 번의
                                        // Unreachable은 Waiting일 뿐이며, 같은 환경/Root에서
                                        // 전체 후보를 두 차례 소진한 경우에만 Blocked를 확정한다.
                                        List<HexCoord> blockedResumePath = null;
                                        UnitPostCombatRecoveryResult recovery = default;
                                        while (_unitData != null && _unitData.IsAlive)
                                        {
                                            HexCoord recoveryRoot = HexMetrics.WorldToHex(
                                                ViewConverter.FromView(transform.position));
                                            recovery = postCombatRecoveryPlanner.Evaluate(
                                                Time.frameCount,
                                                _navigationEnvironmentRevision,
                                                recoveryRoot,
                                                path,
                                                i,
                                                finalTarget,
                                                candidate =>
                                                {
                                                    List<HexCoord> candidatePrefix =
                                                        RequestLatestAuthoritativeRepath(candidate);
                                                    if (!UnitPathSignature.TryCreate(
                                                            candidatePrefix,
                                                            out UnitPathSignature candidateSignature)
                                                        || candidateSignature
                                                            == repathGuard.CurrentPath)
                                                    {
                                                        return null;
                                                    }
                                                    return candidatePrefix;
                                                });
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                            ObservePostCombatPlanner(recovery);
#endif

                                            if (recovery.Status
                                                == UnitPostCombatRecoveryStatus.Recovered)
                                            {
                                                blockedResumePath =
                                                    ComposePostCombatRecoveryRoute(
                                                        recovery.Path,
                                                        path,
                                                        i,
                                                        recovery.Candidate);
                                                bool composedPathIsNew =
                                                    UnitPathSignature.TryCreate(
                                                        blockedResumePath,
                                                        out UnitPathSignature composedSignature)
                                                    && composedSignature
                                                        != repathGuard.CurrentPath;
                                                if (!composedPathIsNew)
                                                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                                    PostCombatTraceEvent("planner-composed-route-not-new");
#endif
                                                    postCombatRecoveryPlanner
                                                        .RejectStagedPath();
                                                    _navigationObjective?.MarkWaitingRepath();
                                                    SetMovementHeldAnimation(true);
                                                    yield return null;
                                                    continue;
                                                }

                                                if (TryAcceptSpatialRecoverableRepath(
                                                        repathGuard,
                                                        blockedResumePath,
                                                        "post-combat-corridor",
                                                        out UnitRepathDecision recoveryDecision))
                                                {
                                                    postCombatRecoveryPlanner
                                                        .AcceptStagedPath();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                                    PostCombatTraceEvent("planner-path-accepted");
#endif
                                                    break;
                                                }

                                                // A* prefix가 존재해도 전체 route가 이전 unsafe
                                                // path와 같거나 guard가 같은 frame 적용을 거부하면
                                                // 성공으로 초기화하지 않고 다음 후보를 본다.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                                PostCombatTraceEvent($"planner-guard-rejected-{recoveryDecision}");
#endif
                                                postCombatRecoveryPlanner
                                                    .RejectStagedPath();
                                                if (recoveryDecision
                                                        == UnitRepathDecision.RejectedRepeatedPath
                                                    || recoveryDecision
                                                        == UnitRepathDecision.RejectedFrameBudget)
                                                {
                                                    _navigationObjective?.MarkWaitingRepath();
                                                    SetMovementHeldAnimation(true);
                                                    yield return null;
                                                    continue;
                                                }

                                                navigationBlocked = true;
                                                break;
                                            }

                                            if (recovery.Status
                                                == UnitPostCombatRecoveryStatus.ConfirmedBlocked)
                                            {
                                                _navigationObjective?.MarkBlocked();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                                ObservePostCombatBlocked(repathGuard, recovery.CompletedSweeps, "planner-confirmed-manual-label-not-guard-limit");
#endif
                                                LogRepathFailClosed(
                                                    repathGuard,
                                                    null,
                                                    "post-combat-corridor-confirmed",
                                                    UnitRepathDecision.RejectedNoProgressBudget,
                                                    recovery.Candidate.ToString());
                                                navigationBlocked = true;
                                                break;
                                            }

                                            if (recovery.Status
                                                == UnitPostCombatRecoveryStatus.Invalid)
                                            {
                                                ObserveMovementAuthorityAdapterFailure(
                                                    "post-combat-recovery-invalid",
                                                    $"[UAS-MOVE] 전투 후 복귀 planner 입력/수명이 " +
                                                    $"유효하지 않습니다. unitId={_unitData.Id}, " +
                                                    $"frame={Time.frameCount}");
                                                movementFailedClosed = true;
                                                goto cleanup;
                                            }

                                            _navigationObjective?.MarkWaitingRepath();
                                            SetMovementHeldAnimation(true);
                                            yield return null;
                                        }

                                        if (_unitData == null || !_unitData.IsAlive)
                                            break;

                                        if (navigationBlocked)
                                        {
                                            needRepath = true;
                                            _currentNextTileCoord = null;
                                            break;
                                        }

                                        BeginMovementShadowSegment(
                                            UnitMovementIntentReason.BlockedRepath);
                                        path = blockedResumePath;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                        CapturePostCombatTrace(path, resumeCorridor, i, finalTarget, false, MovementWriteOutcome.Held);
                                        PostCombatTraceEvent("blocked-route-installed");
#endif
                                        pathCheckpoint.Reset();
                                        needRepath = true;
                                        SetMovementHeldAnimation(true);
                                        break;
                                    }
                                    if (alignOutcome == MovementWriteOutcome.Rejected)
                                    {
                                        ObserveMovementAuthorityAdapterFailure("resume-align-rejected",
                                            $"[UAS-MOVE] 권위 복귀 정렬 frame 거부. " +
                                            $"unitId={_unitData.Id}, " +
                                            $"commandRevision={_movementCommandRevision}, " +
                                            $"segmentRevision={_movementSegmentRevision}");
                                        movementFailedClosed = true;
                                        goto cleanup;
                                    }
                                    if (alignOutcome == MovementWriteOutcome.Advanced)
                                    {
                                        Vector3 committedAlignDelta =
                                            transform.position - positionBeforeAlignFrame;
                                        if (_lastMovementTrajectoryReachedWaypoint
                                            || committedAlignDelta.x * committedAlignDelta.x
                                                + committedAlignDelta.z * committedAlignDelta.z
                                                > (float)(UnitMovementEvaluationAdapter.PositionTolerance
                                                    * UnitMovementEvaluationAdapter.PositionTolerance))
                                        {
                                            ObserveNavigationProgress(repathGuard, path);
                                        }

                                        if (reducerResumeTrajectory)
                                        {
                                            resumeWaypointReached =
                                                _lastMovementTrajectoryReachedWaypoint;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                            if (resumeWaypointReached)
                                            {
                                                UnitMovementAuthorityObserver.ObserveCenterCheckpoint(
                                                    CalculateMovementShadowPositionDeltaXZ(
                                                        transform.position,
                                                        alignView));
                                                PostCombatTraceEvent("center-checkpoint");
                                            }
#endif
                                        }
                                        else
                                        {
                                            alignElapsed = candidateAlignElapsed;
                                        }
                                    }
                                    shouldReengage = shouldReengage
                                        || _lastMovementCandidateAcquiredTarget;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                    CompleteMovementShadowLegacyFrame(resumeShadowToken);
#endif

                                    // 정렬 도중에도 새 대상(일반=적 / 힐러=부상 아군) 감지 시 즉시 다음 사이클로 넘긴다.
                                    if (shouldReengage)
                                    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                        PostCombatTraceEvent("reengage-priority", close: true);
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                        UnitMovementShadowObserver.ObserveTargetAcquirePriority(
                                            resumeShadowToken);
#endif
                                        alignInterruptedByCombat = true;
                                        break;
                                    }

                                    yield return null;
                                }
                            }

                            if (needRepath)
                                break;

                            if (_unitData == null || !_unitData.IsAlive) break;

                            // 정렬 Lerp 도중 새 대상이 감지된 경우에는 외부 while와 cleanup으로
                            // 빠지지 않고 현재 MoveAlongPathV3 안에서 즉시 역할별 행동에 재진입한다.
                            // 행동이 끝난 뒤에는 현재 Root/Domain으로 이 블록의 모든 지역값을
                            // 다시 계산하므로, 이전 forwardTile/alignView를 재사용하지 않는다.
                            if (alignInterruptedByCombat)
                            {
                                UnitPostCombatReentryResult reentry =
                                    postCombatReentryFlow.ObserveTargetAcquired(
                                        Time.frameCount);
                                if (reentry.Action
                                    != UnitPostCombatReentryAction.EnterPursuit)
                                {
                                    ObserveMovementAuthorityAdapterFailure(
                                        "post-combat-reentry-transition-invalid",
                                        $"[UAS-MOVE] 복귀 중 행동 재진입 전이가 유효하지 않습니다. " +
                                        $"unitId={_unitData.Id}, frame={Time.frameCount}");
                                    movementFailedClosed = true;
                                    goto cleanup;
                                }

                                _isAStarMoving = false;
                                _currentNextTileCoord = null;
                                bool reentryWasHealer = _unitData.IsHealer;
                                if (reentryWasHealer)
                                {
                                    // 힐러는 적 추격으로 보내지 않고 기존 부상 아군 힐 루프를
                                    // 그대로 사용한다. Action 회전 소유권 인계/반환 순서도
                                    // 최초 A* 감지 경계와 동일하게 보존한다.
                                    _rootRotationOwnership.TransferToAction();
                                    yield return EnterHealLoopV3();
                                    _rootRotationOwnership.ReleaseAction(
                                        resumeMovement: _unitData != null
                                            && _unitData.IsAlive);
                                }
                                else
                                {
                                    yield return EnterCombatPursuitV3();
                                }

                                bool unitAliveAfterReentry = _unitData != null
                                    && _unitData.IsAlive;
                                UnitPostCombatReentryResult pursuitCompleted =
                                    postCombatReentryFlow.ObservePursuitCompleted(
                                        Time.frameCount,
                                        unitAliveAfterReentry,
                                        reentryWasHealer,
                                        _combatPursuitRequiresRepath);
                                if (!unitAliveAfterReentry)
                                    break;
                                if (pursuitCompleted.Action
                                    == UnitPostCombatReentryAction
                                        .RepathBeforeRecovery)
                                {
                                    // 최초 전투 진입 경계와 동일하게 Chase가 남긴 공간 재경로
                                    // 요구를 recovery 계산보다 먼저 소비한다. 그렇지 않으면 stale
                                    // flag와 전투 전 forwardTile/alignView가 복귀 판단에 섞인다.
                                    _combatPursuitRequiresRepath = false;
                                    List<HexCoord> reentryPursuitRepath =
                                        RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                                    if (_rallyPolicyCompletionPending) goto cleanup;
                                    if (!TryAcceptSpatialRecoverableRepath(
                                            repathGuard,
                                            reentryPursuitRepath,
                                            "post-combat-reentry-chase-spatial-route-invalidated",
                                            out UnitRepathDecision reentryPursuitRepathDecision))
                                    {
                                        navigationBlocked = true;
                                        needRepath = true;
                                        _currentNextTileCoord = null;
                                        break;
                                    }

                                    BeginMovementShadowSegment(
                                        UnitMovementIntentReason.BlockedRepath);
                                    path = reentryPursuitRepath;
                                    pathCheckpoint.Reset();
                                    needRepath = true;
                                    _isAStarMoving = true;
                                    SetMovementHeldAnimation(true);
                                    _currentNextTileCoord = null;
                                    break;
                                }
                                if (pursuitCompleted.Action
                                    != UnitPostCombatReentryAction
                                        .DeferRecoveryUntilNextFrame)
                                {
                                    ObserveMovementAuthorityAdapterFailure(
                                        "post-combat-reentry-completion-invalid",
                                        $"[UAS-MOVE] 재진입 행동 종료 전이가 유효하지 않습니다. " +
                                        $"unitId={_unitData.Id}, frame={Time.frameCount}");
                                    movementFailedClosed = true;
                                    goto cleanup;
                                }

                                UnitPostCombatReentryResult nextRecovery;
                                do
                                {
                                    nextRecovery = postCombatReentryFlow.BeginRecovery(
                                        Time.frameCount);
                                    if (nextRecovery.Action
                                        == UnitPostCombatReentryAction
                                            .DeferRecoveryUntilNextFrame)
                                    {
                                        yield return null;
                                    }
                                }
                                while (nextRecovery.Action
                                    == UnitPostCombatReentryAction
                                        .DeferRecoveryUntilNextFrame);

                                if (nextRecovery.Action
                                    != UnitPostCombatReentryAction.RecalculateRecovery)
                                {
                                    ObserveMovementAuthorityAdapterFailure(
                                        "post-combat-recovery-restart-invalid",
                                        $"[UAS-MOVE] 재진입 뒤 새 복귀 계산 전이가 유효하지 않습니다. " +
                                        $"unitId={_unitData.Id}, frame={Time.frameCount}");
                                    movementFailedClosed = true;
                                    goto cleanup;
                                }

                                // 이전 복귀 후보의 소진/성공 staged 상태 역시 현재 전투 위치와
                                // 무관해졌으므로 함께 폐기한다. 아래 label부터 Root/Domain,
                                // forwardTile, alignView, corridor를 전부 새로 계산한다.
                                postCombatRecoveryPlanner.Reset();
                                goto recalculatePostCombatRecovery;
                            }

                            // 4) 최종 부동소수점 보정도 같은 writer seam을 통과한다.
                            MovementWriteOutcome alignEndpointOutcome =
                                ApplyMovementAuthorityFrame(
                                alignView,
                                alignView,
                                transform.rotation,
                                UnitMovementIntentReason.PostCombatResume,
                                targetAcquirePriority: false);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            CapturePostCombatTrace(path, resumeCorridor, i, finalTarget, false, alignEndpointOutcome);
#endif
                            if (alignEndpointOutcome == MovementWriteOutcome.Rejected)
                            {
                                ObserveMovementAuthorityAdapterFailure("resume-endpoint-rejected",
                                    $"[UAS-MOVE] 권위 복귀 endpoint 거부. unitId={_unitData.Id}");
                                movementFailedClosed = true;
                                goto cleanup;
                            }

                            // 5) 같은 forwardTile로 도메인 갱신 + 새 path 발급.
                            List<HexCoord> resumePath = ResumeFromForwardTileV3(finalTarget, forwardTile);
                            if (_rallyPolicyCompletionPending) goto cleanup;
                            if (resumePath != null && resumePath.Count > 0)
                            {
                                if (!TryAcceptRepath(
                                        repathGuard,
                                        resumePath,
                                        "post-combat-resume",
                                        out UnitRepathDecision repathDecision))
                                {
                                    navigationBlocked = true;
                                    needRepath = true;
                                    _currentNextTileCoord = null;
                                    break;
                                }

                                path = resumePath;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                                CapturePostCombatTrace(path, resumeCorridor, i, finalTarget, false, MovementWriteOutcome.Held);
                                PostCombatTraceEvent("post-combat-route-installed");
#endif
                                pathCheckpoint.Reset();
                                needRepath = true;
                                interruptedByCombat = true;
                                SetMovementHeldAnimation(true);
                                // 혼잡도 기여 재활성화 — 전투 종료 후 정상 A* 재개.
                                _isAStarMoving = true;
                                break;  // Lerp while 탈출
                            }

                            // 새 path를 만들지 못함 → 이동 종료.
                            goto cleanup;
                        }

                        yield return null;
                    }

                    if (_unitData == null || !_unitData.IsAlive) break;

                    // corridor가 경기 중 막혀 새 path를 받은 경우 현재 waypoint의
                    // route checkpoint도 아직 소비하면 안 된다. 외부 while에서
                    // 새 path index 1부터 다시 시작한다.
                    if (needRepath) break;

                    // [전투 이동]으로 중단된 경우: toPos 강제 스냅과 ProcessStep을 건너뛰고
                    // for 루프를 빠져나가 외부 while에서 새 path로 재진입.
                    if (interruptedByCombat)
                    {
                        break;
                    }

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] 정상 waypoint 완료.
                    // ──────────────────────────────────────────────────────
                    // Legacy Lerp만 기존 타일 중심 endpoint 보정을 유지한다. Reducer trajectory는
                    // planner의 이동량 제한으로 중심에 도달하며 별도 위치 스냅을 하지 않는다.
                    if (!reducerTrajectory)
                    {
                        MovementWriteOutcome endpointOutcome = ApplyMovementAuthorityFrame(
                            toPos,
                            toPos,
                            transform.rotation,
                            UnitMovementIntentReason.AStarPath,
                            targetAcquirePriority: false);
                        if (endpointOutcome == MovementWriteOutcome.Rejected)
                        {
                            ObserveMovementAuthorityAdapterFailure("movement-endpoint-rejected",
                                $"[UAS-MOVE] 권위 이동 endpoint 거부. unitId={_unitData.Id}");
                            movementFailedClosed = true;
                            goto cleanup;
                        }
                    }

                    // Reducer 경로의 waypoint 완료는 route checkpoint만 소비한다.
                    // 실제 Domain 위치와 혼잡도는 매 frame Root 경계 전이 시 공용 writer가 이미 커밋했다.
                    if (reducerTrajectory
                        && !pathCheckpoint.TryConsume(
                            i,
                            authoritativeWaypointReached))
                    {
                        ObserveMovementAuthorityAdapterFailure("trajectory-checkpoint-rejected",
                            $"[UAS-MOVE] trajectory waypoint 순서 거부. " +
                            $"unitId={_unitData.Id}, index={i}, " +
                            $"expected={pathCheckpoint.NextWaypointIndex}");
                        movementFailedClosed = true;
                        goto cleanup;
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (reducerTrajectory)
                    {
                        UnitMovementAuthorityObserver.ObserveCenterCheckpoint(
                            CalculateMovementShadowPositionDeltaXZ(
                                transform.position,
                                toPos));
                    }
#endif
                    ObserveNavigationProgress(repathGuard, path);
                    if (!reducerTrajectory && NetworkContext.IsNetworkActive)
                    {
                        // 멀티 Legacy rollback만 기존 중심 도착 의미를 보존한다.
                        // SOLO는 공용 writer가 실제 경계에서 이미 커밋하므로 중복 발행하지 않는다.
                        if (_movementUseCase != null
                            && !(isLastStep && isLastStepToNonWalkable))
                        {
                            _movementUseCase.ProcessStep(_unitData, from, to);
                        }

                        if (_isAStarMoving)
                        {
                            GameEvents.OnUnitEnteredTile.OnNext(
                                new UnitEnteredTileEvent(_unitData.Id, to));
                        }
                    }

                    prevActualTile = reducerTrajectory || !NetworkContext.IsNetworkActive
                        ? _unitData.Position
                        : to;

                    // 타일 도착 완료 — 다음 Lerp 시작 전까지는 "다음 도착 타일" 정보 없음.
                    // null로 비워 두지 않으면 OnPathInvalidated가 이미 지나간 타일을 검사할 수 있다.
                    _currentNextTileCoord = null;

                    // ──────────────────────────────────────────────────────
                    // 부드러운 경로 교체 — OnPathInvalidated가 예약한 새 path 소비.
                    //
                    //   OnPathInvalidated가 _pendingPath에 새 경로를 담아 두면,
                    //   여기서(타일 중심 도착 직후) 그것을 꺼내 path 변수를 교체한다.
                    //   현재 위치(_unitData.Position == to)가 새 path 어디에 해당하는지 찾아
                    //   거기서부터 이어가도록 외부 while로 재진입한다.
                    //
                    //   현재 위치를 새 path에서 찾지 못하는 경우(드물지만 가능):
                    //     - 새 경로 자체가 다른 시작점부터 만들어진 경우 등.
                    //     - 이때는 안전하게 MoveTo(_pendingPath)로 코루틴을 재시작한다.
                    //       (이 경로에서는 시각적 끊김이 있지만, 데이터 무결성이 우선.)
                    // ──────────────────────────────────────────────────────
                    if (_pendingPath != null)
                    {
                        List<HexCoord> pending = _pendingPath;
                        _pendingPath = null;

                        // 현재 도메인 위치가 새 path의 어느 인덱스에 있는지 검색.
                        // 찾으면 그 지점부터 이어 돌도록 path를 잘라낸다.
                        int startIdx = -1;
                        for (int k = 0; k < pending.Count; k++)
                        {
                            if (pending[k].Q == _unitData.Position.Q
                                && pending[k].R == _unitData.Position.R)
                            {
                                startIdx = k;
                                break;
                            }
                        }

                        if (startIdx >= 0 && startIdx < pending.Count - 1)
                        {
                            BeginMovementShadowSegment(UnitMovementIntentReason.PendingRepath);
                            // 정상 케이스: 현재 위치를 새 path의 startIdx로 두고,
                            // 그 다음 타일(startIdx+1)부터 진행하도록 path 교체.
                            // path[0]은 항상 시작점(현재 위치) — 슬라이스 후에도 이 규약 유지.
                            List<HexCoord> sliced = new List<HexCoord>(pending.Count - startIdx);
                            for (int k = startIdx; k < pending.Count; k++)
                            {
                                sliced.Add(pending[k]);
                            }
                            if (!TryAcceptRepath(
                                    repathGuard,
                                    sliced,
                                    "pending-path",
                                    out UnitRepathDecision repathDecision))
                            {
                                navigationBlocked = true;
                                needRepath = true;
                                _currentNextTileCoord = null;
                                break;
                            }

                            path = sliced;
                            pathCheckpoint.Reset();
                            SetMovementHeldAnimation(true);

                            // 최종 목적지가 바뀌었을 수도 있으므로 갱신.
                            finalTarget = path[path.Count - 1];

                            // 외부 while 재진입 → 새 path 기준으로 i=1부터 다시 순회.
                            needRepath = true;
                            break;  // for 탈출 → 아래 `if (needRepath) continue;`로 외부 while 재진입.
                        }
                        else
                        {
                            // 현재 위치를 포함하지 않는 pending path는 이어 달릴 수 없는 입력이다.
                            // 같은 프레임에 새 코루틴을 재시작하면 무제한 재시작 고리가 생길 수
                            // 있으므로 이 유닛만 현재 위치에서 fail-closed 한다.
                            List<HexCoord> refreshed =
                                RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
                            if (_rallyPolicyCompletionPending) goto cleanup;
                            if (TryAcceptRepath(
                                    repathGuard,
                                    refreshed,
                                    "pending-path-current-position-refresh",
                                    out UnitRepathDecision refreshDecision))
                            {
                                BeginMovementShadowSegment(
                                    UnitMovementIntentReason.PendingRepath);
                                path = refreshed;
                                finalTarget = path[path.Count - 1];
                                pathCheckpoint.Reset();
                                needRepath = true;
                                SetMovementHeldAnimation(true);
                                break;
                            }

                            navigationBlocked = true;
                            needRepath = true;
                            _currentNextTileCoord = null;
                            break;
                        }
                    }

                    // [2026-05-11 비활성화] 우회(didDetour)/재경로 분기는 제거됐습니다.
                    // 새 규칙에서는 같은 타일에 여러 유닛이 들어가도 우회하지 않으므로 재계산 불필요.
                }  // end of for (path 순회)

                // for를 정상 종료(완주) 또는 사망/탈출 — 외부 while 분기.
                if (needRepath)
                {
                    // 수락한 재경로는 반드시 다음 Unity frame부터 처리한다. 이 경계가
                    // RequestMove → RepathRequired의 단일-frame 무한 반복을 차단한다.
                    yield return null;
                    continue;
                }
                break;                       // 정상 완주 또는 사망 — 외부 while 종료.
            }

            cleanup:
            // 랠리 생략 예약을 먼저 소비해야 마지막 NoIntent 게시도 이동 작성 경계를
            // 실제로 통과한다. 이전 경로를 막는 예약 flag로 종료 게시까지 막지 않는다.
            _rallyPolicyCompletionPending = false;
            if (_activeRallyPoint.HasValue && _pendingPath != null && _pendingPath.Count == 1)
                _pendingPath = null;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            PostCombatTraceEvent(_unitData == null || !_unitData.IsAlive ? "death-cleanup" :
                movementFailedClosed ? "cancelled-cleanup" : "completion-cleanup", close: true);
#endif
            // 정상/사망/완주/RESUME 실패 등 모든 종료 경로에서 공통 cleanup.
            //   여기서 _moveCoroutine을 null로 비우고 OnMoveComplete 콜백(랠리→성 자동 이동/공성 체인)을
            //   발행한다. 콜백이 새 이동(MoveTo)을 시작하면 _moveCoroutine이 다시 non-null이 된다.
            if (_unitData != null && _unitData.IsAlive)
            {
                bool closed = TryCloseMovementAuthorityIntent(
                    UnitMovementIntentReason.AStarPath,
                    "path-cleanup");
                if (_activeRallyPoint.HasValue)
                {
                    // 랠리 경유 종료는 실제 종료 게시가 확인된 경우에만 성 진군으로 잇는다.
                    // Reducer 모드에서는 반환값뿐 아니라 최신 수용 상태도 함께 확인한다.
                    if (_lockedMovementPipelineMode == UnitMovementPipelineMode.ReducerAuthoritative)
                    {
                        closed = closed && _movementAuthorityReducer != null
                            && _movementAuthorityReducer.Snapshot.HasAcceptedObservation
                            && _movementAuthorityReducer.Snapshot.Phase == UnitMovementPhase.NoIntent
                            && _movementAuthorityReducer.Snapshot.CommandRevision == _movementCommandRevision
                            && _movementAuthorityReducer.Snapshot.SegmentRevision == _movementSegmentRevision;
                    }
                    if (!closed) movementFailedClosed = true;
                }
            }
            if (_unitData == null || !_unitData.IsAlive)
                _navigationObjective?.Cancel();
            else if (movementFailedClosed)
                _navigationObjective?.Cancel();
            else
                _navigationObjective?.Complete();
            MoveCleanupAndCompleteV3(invokeCompletion: !movementFailedClosed);

            // ────────────────────────────────────────────────────────────────
            // [이슈 2 수정 — 2026-07-18] 힐러(BloomFairy) 유휴 감시 진입.
            //
            //   왜 필요한가(초급자용 설명):
            //     힐러는 적 성을 공격하지 않으므로, 부상 아군 없이 경로 끝(성 인접 타일)에 도달하면
            //     위 cleanup에서 이동 코루틴이 "완전히 끝나" 버렸다. 그 뒤에는 근처 아군이 다쳐도
            //     힐러가 다시 반응하지 못하는 "영구 유휴" 상태가 됐다(일반 유닛은 성 앞에서 무한 전투
            //     루프를 돌기 때문에 이 함정이 없다 — 힐러만 구조적으로 노출됐다).
            //
            //   해결:
            //     힐러가 "살아있는 동안 부상 아군을 계속 지켜보도록" 별도의 유휴 감시 코루틴을 시작해
            //     _moveCoroutine으로 계속 추적한다. 감시 중 부상 아군이 생기면 힐 루프로 재진입하고,
            //     이동을 재개할 새 경로(_pendingPath)가 예약되면 이동으로 돌아간다(아래 코루틴 참조).
            //
            //   비힐러 회귀 방지:
            //     - 아래 조건에 IsHealer가 있어 일반 유닛은 이 분기를 절대 타지 않는다(기존 종료 동작 그대로).
            //     - OnMoveComplete 콜백이 이미 새 이동을 시작한 경우(_moveCoroutine != null)에는 진입하지
            //       않는다. 즉 랠리 도착·공성 진행처럼 "다음 이동이 이어지는 중간 구간"에서는 감시가 끼어들지
            //       않아 기존 랠리/공성 체인이 그대로 보존된다. 순수 종착(더 이동할 곳 없음)일 때만 감시한다.
            // ────────────────────────────────────────────────────────────────
            if (!movementFailedClosed
                && _moveCoroutine == null
                && _unitData != null
                && _unitData.IsAlive
                && _unitData.IsHealer)
            {
                _moveCoroutine = StartCoroutine(HealerIdleWatchV3());
            }
        }

        /// <summary>
        /// [2026-05-11 신규] 통합 전투 추격 코루틴 (근접/원거리 공통).
        ///
        /// 새 규칙(GameSystemRules.md 규칙 7)에 따른 [전투 이동] → [공격] 단일 상태 머신.
        /// 근접/원거리 구분 없음 — 차이는 UnitData.AttackRange 수치뿐.
        ///
        /// 동작:
        ///   1) FindNearestEnemyInDetectRange로 첫 타겟 선택.
        ///   2) 매 프레임:
        ///      - 타겟이 detect 사거리 밖이면 종료(→ A* 재개).
        ///      - 타겟이 사망/파괴되면 detect 내 다른 적 재선택. 없으면 종료.
        ///      - 타겟이 공격 사거리(HasEnemyInRange) 안이면 EnterCombatLoopV3 호출.
        ///         · 전투 종료 후 detect 내 다른 적이 있으면 타겟 교체 후 continue.
        ///         · 없으면 종료.
        ///      - 그 외(detect 안, 사거리 밖) → 같은 타일은 안전한 직선, 다른 타일은
        ///        타겟 접근 서버 경로로 이동.
        ///
        /// 슬롯/유닛 점유 차단 없음 — 같은 적을 향해 여러 유닛이 같은 위치로 모일 수 있습니다.
        /// </summary>
        private IEnumerator EnterCombatPursuitV3()
        {
            // ────────────────────────────────────────────────────────────
            // [BUG-001 — 2026-05-12] 추격 단계 진입 마킹.
            //   IsInCombat()가 true를 반환하도록 하여, 건물 변화로 인한
            //   RepathAllAliveUnits → OnPathInvalidated에서 추격 코루틴이 끊기지 않게 한다.
            //   Unity 코루틴(IEnumerator)은 try/finally가 동작하지 않으므로,
            //   각 yield break 직전 + 함수 끝에서 false로 명시적 리셋해야 한다.
            // ────────────────────────────────────────────────────────────
            _isInCombatPursuit = true;
            _combatPursuitRequiresRepath = false;
            BeginMovementShadowSegment(UnitMovementIntentReason.Chase);

            // 필수 의존성 가드 — _positionProvider는 적 월드 좌표 조회에 사용.
            if (_combatUseCase == null || _positionProvider == null || _unitData == null)
            {
                _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                yield break;
            }

            // 첫 타겟 선택 — 감지 사거리 내 가장 가까운 적.
            var firstTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
            if (!firstTarget.HasValue)
            {
                TryCloseMovementAuthorityIntent(
                    UnitMovementIntentReason.Chase,
                    "pursuit-no-first-target");
                _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                yield break;
            }

            int targetId = firstTarget.Value.id;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _postCombatLastTarget = targetId;
            _postCombatExitReason = "pursuit-active";
            PostCombatTraceEvent("pursuit-target-selected");
#endif
            bool targetIsUnit = firstTarget.Value.isUnit;
            BeginSoloPursuitEvidence(targetId, targetIsUnit);

            // 기본 이동 속도. 연구·둔화·빙결 배율은 아래 frame 루프에서 다시 읽는다.
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;
            float worldSpeed = HexMetrics.TileHeight / moveSeconds;

            // Chase도 A*와 같은 logical corridor를 사용한다. 타겟 타일 또는 환경 revision이
            // 바뀔 때만 경로를 갱신하여 움직이는 타겟 때문에 매 frame FlowField를 요청하거나,
            // 동일한 막힌 후보를 무한 반복하지 않게 한다.
            List<HexCoord> pursuitPath = null;
            int pursuitWaypointIndex = 1;
            HexCoord pursuitTargetTile = default;
            bool hasPursuitTargetTile = false;
            ulong pursuitEnvironmentRevision = _navigationEnvironmentRevision;
            var pursuitCheckpoint = new UnitPathCheckpointTracker();
            int pursuitRouteTargetId = targetId;
            bool pursuitRouteTargetIsUnit = targetIsUnit;
            _combatPursuitPathInvalidated = false;

            // 매 프레임 루프 — 적 상태 확인 + 전투 이동 또는 공격으로 분기.
            while (_unitData != null && _unitData.IsAlive)
            {
                // ── 1) 적의 최신 월드 좌표 조회 ──
                ObserveSoloPursuitStall();
                // 적이 이동 중일 수 있으므로 매 프레임 갱신.
                Vector3 enemyViewPos = targetIsUnit
                    ? _positionProvider.GetUnitWorldPosition(targetId)
                    : _positionProvider.GetBuildingWorldPosition(targetId);

                // ── 2) 타겟 파괴 → 다음 타겟 재선택 or 종료 ──
                if (enemyViewPos == Vector3.zero)
                {
                    var next = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!next.HasValue)
                    {
                        // detect 내 다른 적 없음 → A* 재개로 위임.
                        TryCloseMovementAuthorityIntent(
                            UnitMovementIntentReason.Chase,
                            "pursuit-target-lost");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        _postCombatExitReason = "pursuit-target-lost";
                        PostCombatTraceEvent("pursuit-target-lost");
#endif
                        CloseSoloPursuitEvidence("close-target-lost");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    targetId = next.Value.id;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    _postCombatLastTarget = targetId;
                    PostCombatTraceEvent("pursuit-target-reselected");
#endif
                    targetIsUnit = next.Value.isUnit;
                    CloseSoloPursuitEvidence("close-target-replaced");
                    BeginSoloPursuitEvidence(targetId, targetIsUnit);
                    pursuitPath = null;
                    pursuitWaypointIndex = 1;
                    pursuitCheckpoint.Reset();
                    hasPursuitTargetTile = false;

                    yield return null;
                    continue;
                }

                // ── 3) 적이 detect 사거리 밖으로 이탈 → 종료 ──
                if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                {
                    TryCloseMovementAuthorityIntent(
                        UnitMovementIntentReason.Chase,
                        "pursuit-detect-range-lost");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    _postCombatExitReason = "pursuit-detect-range-lost";
                    PostCombatTraceEvent("pursuit-detect-range-lost");
#endif
                    CloseSoloPursuitEvidence("close-detect-lost");
                    _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                    yield break;
                }

                // ── 4) 공격 사거리 도달 → [공격] 루프 ──
                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    MovementWriteOutcome acquireOutcome = ApplyMovementAuthorityFrame(
                        enemyViewPos,
                        transform.position,
                        transform.rotation,
                        UnitMovementIntentReason.Chase,
                        targetAcquirePriority: true,
                        targetAcquirePresentationHandoff: () =>
                            TryBeginServerAttackEntryBeforeRootStop(
                                targetId,
                                targetIsUnit));
                    if (acquireOutcome == MovementWriteOutcome.DeferredAttackHandoff)
                    {
                        // Reducer/Root/NoIntent는 아직 전혀 커밋되지 않았다. 현재 Walk pose를
                        // 보존하고 다음 정상 frame에 동일한 direct-in-range 진입을 재시도한다.
                        yield return null;
                        continue;
                    }
                    if (acquireOutcome == MovementWriteOutcome.Rejected)
                    {
                        ObserveMovementAuthorityAdapterFailure("target-acquire-no-intent-rejected",
                            $"[UAS-MOVE] 공격 범위 진입 NoIntent 게시 거부. unitId={_unitData.Id}");
                        CloseSoloPursuitEvidence("failure-attack-entry");
                        _isInCombatPursuit = false;
                        yield break;
                    }

                    // ReducerAuthoritative 네트워크 경로는 위 callback이 이미 Action 소유권과
                    // provisional Start를 열었다. Legacy/싱글 경로는 callback을 실행하지 않으므로
                    // 기존 순서대로 여기서 Action 회전을 시작한다.
                    if (!_rootRotationOwnership.ActionOwnsRoot)
                        BeginServerActionRotation(targetId, targetIsUnit);
                    SoloPursuitEvidence("attack-entry");
                    yield return EnterCombatLoopV3();
                    EndServerActionRotation();
                    _rootRotationOwnership.TryAcquireMovement();

                    if (_unitData == null || !_unitData.IsAlive)
                    {
                        CloseSoloPursuitEvidence("close-attacker-dead");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    // 전투 종료 후 detect 내에 다른 적이 남았으면 새 타겟 선택 후 continue.
                    if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                    {
                        TryCloseMovementAuthorityIntent(
                            UnitMovementIntentReason.Chase,
                            "post-combat-detect-range-lost");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        _postCombatExitReason = "post-combat-detect-range-lost";
                        PostCombatTraceEvent("post-combat-detect-range-lost");
#endif
                        CloseSoloPursuitEvidence("close-post-combat-detect-lost");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    var nextEnemy = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!nextEnemy.HasValue)
                    {
                        TryCloseMovementAuthorityIntent(
                            UnitMovementIntentReason.Chase,
                            "post-combat-no-next-target");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        _postCombatExitReason = "post-combat-no-next-target";
                        PostCombatTraceEvent("post-combat-no-next-target");
#endif
                        CloseSoloPursuitEvidence("close-no-next-target");
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    targetId = nextEnemy.Value.id;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    _postCombatLastTarget = targetId;
                    PostCombatTraceEvent("pursuit-target-reselected");
#endif
                    targetIsUnit = nextEnemy.Value.isUnit;
                    CloseSoloPursuitEvidence("close-post-combat-reselect");
                    BeginSoloPursuitEvidence(targetId, targetIsUnit);
                    pursuitPath = null;
                    pursuitWaypointIndex = 1;
                    pursuitCheckpoint.Reset();
                    hasPursuitTargetTile = false;

                    yield return null;
                    continue;
                }

                // ── 5) [전투 이동] 안전한 서버 경로로 공격 접근 ──
                // 타겟과 같은 walkable 타일 안에 있을 때만 짧은 direct 이동을 허용한다.
                // 타일이 다르면 타겟 타일까지의 FlowField 경로를 사용하므로 중간 건물이나
                // 완전 차단 지형을 향해 직선 후보를 반복하지 않는다. 다른 유닛의 점유는
                // 여전히 차단하지 않으므로 슬롯 없는 유닛 겹침 규칙은 그대로다.
                Vector3 moveDir = enemyViewPos - transform.position;
                moveDir.y = 0f;
                CaptureUnitActionShadowDesiredTarget(enemyViewPos);
                float dist = moveDir.magnitude;
                if (dist > 0.01f)
                {
                    float liveMoveMultiplier = _combatUseCase != null
                        ? Mathf.Max(0f, _combatUseCase.GetUnitMoveSpeedMultiplier(_unitData))
                        : 1f;
                    bool movementFrozen = liveMoveMultiplier <= MoveFreezeEpsilon;
                    SetFrozenAnimation(movementFrozen);

                    Vector3 enemyDomainPos = ViewConverter.FromView(enemyViewPos);
                    HexCoord liveTargetTile = HexMetrics.WorldToHex(enemyDomainPos);
                    Vector3 rootDomainPos = ViewConverter.FromView(transform.position);
                    HexCoord liveRootTile = HexMetrics.WorldToHex(rootDomainPos);
                    bool directInsideSameTile = liveRootTile == liveTargetTile
                        && _movementUseCase.IsWalkable(liveRootTile);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementAuthorityObserver.ObserveChaseApproach(
                        directInsideSameTile);
#endif

                    Vector3 pursuitWaypointView = enemyViewPos;
                    Vector3? nextPursuitWaypointView = null;
                    IReadOnlyList<HexCoord> pursuitCorridor = null;
                    int corridorWaypointIndex = -1;

                    if (!directInsideSameTile)
                    {
                        bool routeChanged = !hasPursuitTargetTile
                            || liveTargetTile != pursuitTargetTile
                            || _combatPursuitPathInvalidated
                            || pursuitEnvironmentRevision
                                != _navigationEnvironmentRevision;
                        if (routeChanged)
                        {
                            pursuitPath = _movementUseCase.RequestCombatApproachPath(
                                _unitData,
                                liveTargetTile);
                            pursuitTargetTile = liveTargetTile;
                            hasPursuitTargetTile = true;
                            pursuitEnvironmentRevision =
                                _navigationEnvironmentRevision;
                            pursuitWaypointIndex = 1;
                            pursuitCheckpoint.Reset();
                            pursuitRouteTargetId = targetId;
                            pursuitRouteTargetIsUnit = targetIsUnit;
                            _combatPursuitPathInvalidated = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            if (!NetworkContext.IsNetworkActive)
                            {
                                _soloPursuitGoal = liveTargetTile;
                                _soloPursuitHasRoute = pursuitPath != null && pursuitPath.Count >= 2;
                                _soloPursuitRouteRevision = pursuitEnvironmentRevision;
                                _soloPursuitIndex = 1;
                                _soloPursuitWaypoint = _soloPursuitHasRoute
                                    ? TileCenterView(pursuitPath[1]) : transform.position;
                                _soloPursuitProgressTime = Time.timeAsDouble;
                                SoloPursuitEvidence("repath");
                            }
#endif
                            if (pursuitPath != null && pursuitPath.Count >= 2)
                                _navigationObjective?.MarkNavigating();
                        }

                        if (pursuitPath == null || pursuitPath.Count < 2)
                        {
                            _navigationObjective?.MarkBlocked();
                            SetMovementHeldAnimation(true);
                            yield return null;
                            continue;
                        }

                        if (pursuitWaypointIndex >= pursuitPath.Count)
                        {
                            SoloPursuitEvidence("route-exhausted");
                            pursuitPath = null;
                            hasPursuitTargetTile = false;
                            yield return null;
                            continue;
                        }

                        pursuitWaypointView = TileCenterView(
                            pursuitPath[pursuitWaypointIndex]);
                        if (pursuitWaypointIndex + 1 < pursuitPath.Count)
                        {
                            nextPursuitWaypointView = TileCenterView(
                                pursuitPath[pursuitWaypointIndex + 1]);
                        }
                        pursuitCorridor = pursuitPath;
                        corridorWaypointIndex = pursuitWaypointIndex;
                    }

                    SetMovementHeldAnimation(false);
                    float pursuitAngle = CalculateAttackAngle(pursuitWaypointView);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (!NetworkContext.IsNetworkActive)
                    {
                        _soloPursuitIndex = pursuitWaypointIndex;
                        _soloPursuitWaypoint = pursuitWaypointView;
                        _soloPursuitGoal = liveTargetTile;
                    }
#endif
                    Quaternion targetRot = Quaternion.Euler(0f, pursuitAngle, 0f);
                    Vector3 pursuitMoveDir = pursuitWaypointView - transform.position;
                    pursuitMoveDir.y = 0f;
                    Vector3 candidatePursuitPosition = pursuitMoveDir.sqrMagnitude > 0.0001f
                        ? transform.position
                            + pursuitMoveDir.normalized
                                * worldSpeed
                                * liveMoveMultiplier
                                * Time.deltaTime
                        : transform.position;
                    bool soloPursuit = !NetworkContext.IsNetworkActive
                        && _lockedMovementPipelineMode == UnitMovementPipelineMode.Legacy;
                    if (soloPursuit)
                    {
                        UnitSoloPursuitStep.Plan(transform.position.x, transform.position.z,
                            pursuitWaypointView.x, pursuitWaypointView.z,
                            worldSpeed * liveMoveMultiplier * Time.deltaTime,
                            out double nextX, out double nextZ);
                        candidatePursuitPosition = new Vector3((float)nextX,
                            transform.position.y, (float)nextZ);
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitMovementShadowObserver.LegacyFrameToken pursuitShadowToken = default;
                    if (_lockedMovementPipelineMode
                        != UnitMovementPipelineMode.ReducerAuthoritative)
                    {
                        pursuitShadowToken = BeginMovementShadowLegacyFrame(
                            pursuitWaypointView,
                            UnitMovementIntentReason.Chase,
                            CalculateMovementShadowPositionDeltaXZ(
                                transform.position,
                                candidatePursuitPosition));
                    }
#endif
                    Vector3 pursuitBefore = transform.position;
                    MovementWriteOutcome pursuitOutcome = ApplyMovementAuthorityFrame(
                        pursuitWaypointView,
                        candidatePursuitPosition,
                        targetRot,
                        UnitMovementIntentReason.Chase,
                        targetAcquirePriority: false,
                        nextTrajectoryTarget: nextPursuitWaypointView,
                        maximumTravelDistanceWorld:
                            worldSpeed * liveMoveMultiplier * Time.deltaTime,
                        logicalCorridorPath: pursuitCorridor,
                        logicalCorridorWaypointIndex: corridorWaypointIndex,
                        candidateAcquirePredicate: candidate =>
                            _combatUseCase.HasEnemyInRangeAt(_unitData, candidate),
                        targetAcquirePresentationHandoff: () =>
                            TryBeginServerAttackEntryBeforeRootStop(
                                targetId,
                                targetIsUnit));
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (soloPursuit && pursuitOutcome == MovementWriteOutcome.Advanced
                        && CalculateMovementPositionDeltaXZ(pursuitBefore, transform.position) > 0.000001f)
                        _soloPursuitProgressTime = Time.timeAsDouble;
#endif
                    if (pursuitOutcome == MovementWriteOutcome.RepathRequired)
                    {
                        SoloPursuitEvidence("route-invalidated");
                        // 현재 Chase 경로가 동적 건물로 무효화된 경우 같은 frame에는 다시
                        // 실행하지 않는다. 코루틴과 타겟은 유지하되 같은 타겟·같은 환경에서는
                        // 실패한 경로를 반복하지 않고, 타겟 이동 또는 환경 revision 변경을 기다린다.
                        _navigationObjective?.MarkWaitingRepath();
                        SetMovementHeldAnimation(true);
                        pursuitPath = null;
                        // 같은 타겟·같은 environment revision에서는 이 실패한 route를
                        // 다시 요청하지 않는다. 타겟 타일 이동 또는 OnPathInvalidated가
                        // 새 revision을 알릴 때만 routeChanged가 다시 참이 된다.
                        pursuitTargetTile = liveTargetTile;
                        hasPursuitTargetTile = true;
                        pursuitEnvironmentRevision =
                            _navigationEnvironmentRevision;
                        _combatPursuitPathInvalidated = false;
                        yield return null;
                        continue;
                    }
                    if (pursuitOutcome == MovementWriteOutcome.DeferredAttackHandoff)
                    {
                        // Root/NoIntent/reducer가 아직 커밋되지 않았다. 현재 Walk와 이동 pose를
                        // 그대로 보존하고 다음 frame에 같은 후보를 다시 평가한다.
                        yield return null;
                        continue;
                    }
                    if (pursuitOutcome == MovementWriteOutcome.Rejected)
                    {
                        ObserveMovementAuthorityAdapterFailure("pursuit-frame-rejected",
                            $"[UAS-MOVE] 권위 추격 frame 거부. unitId={_unitData.Id}, " +
                            $"commandRevision={_movementCommandRevision}, " +
                            $"segmentRevision={_movementSegmentRevision}");
                        CloseSoloPursuitEvidence("failure-writer-rejected");
                        _isInCombatPursuit = false;
                        yield break;
                    }
                    bool consumePursuitWaypoint = soloPursuit
                        ? UnitSoloPursuitStep.TryConsume(pursuitCheckpoint, pursuitWaypointIndex,
                            pursuitOutcome == MovementWriteOutcome.Advanced,
                            _lastMovementTrajectoryReachedWaypoint,
                            _lastMovementCandidateAcquiredTarget, directInsideSameTile,
                            !_combatPursuitPathInvalidated
                                && pursuitEnvironmentRevision == _navigationEnvironmentRevision
                                && hasPursuitTargetTile && pursuitTargetTile == liveTargetTile
                                && pursuitRouteTargetId == targetId
                                && pursuitRouteTargetIsUnit == targetIsUnit)
                        : !directInsideSameTile
                            && pursuitOutcome == MovementWriteOutcome.Advanced
                            && _lastMovementTrajectoryReachedWaypoint
                            && pursuitCheckpoint.TryConsume(pursuitWaypointIndex, reachedWaypoint: true);
                    if (consumePursuitWaypoint)
                    {
                        SoloPursuitEvidence("checkpoint", reached: true, consume: true);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        if (soloPursuit) _soloPursuitProgressTime = Time.timeAsDouble;
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                        UnitMovementAuthorityObserver.ObserveCenterCheckpoint(
                            CalculateMovementShadowPositionDeltaXZ(
                                transform.position,
                                pursuitWaypointView));
#endif
                        pursuitWaypointIndex++;
                    }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    CompleteMovementShadowLegacyFrame(pursuitShadowToken);
#endif

                    // 후보 위치가 공격 범위에 들어온 frame에는 위 callback이 provisional
                    // Start를 먼저 확정했다. frame을 넘기지 않고 while 선두로 돌아가면
                    // 같은 Unity frame에서 EnterCombatLoopV3까지 진입한다. 선두의 중복 start는
                    // 기존 _attackPresentationRestartPending 가드가 흡수하므로 revision과
                    // Attack clip을 다시 시작하지 않는다.
                    if (_lastMovementCandidateAcquiredTarget)
                        continue;
                }

                yield return null;
            }

            // ────────────────────────────────────────────────────────────
            // [BUG-001 — 2026-05-12] while 루프가 자연 종료(_unitData null/사망)된 경우의
            // 안전망 리셋. 위쪽의 모든 명시적 yield break 경로에서도 false로 set하지만,
            // 함수 마지막에서도 반드시 false임을 보장하여 다음 추격 진입 시 잔존하지 않도록 한다.
            // ────────────────────────────────────────────────────────────
            CloseSoloPursuitEvidence("close-pursuit-ended");
            _isInCombatPursuit = false;
        }

        /// <summary>
        /// [V3] 공격 사거리 도달 후 공격/쿨다운 루프 (규칙 13 — 공격 중 이동 없음).
        ///
        /// 멀티플레이/싱글플레이 분기:
        ///   - 멀티플레이: OnUnitEnteredCombat 1회 발행 → NetworkCombatController가 RPC 전파.
        ///                 사거리 내인 동안 yield, 쿨다운 만료 후 사거리 재확인 시 재진입,
        ///                 사거리 이탈 시 OnUnitWalkStarted 발행 후 종료.
        ///   - 싱글플레이: 매 프레임 TryAttack(쿨다운 내부 체크), 쿨다운 대기, ClearCombatState로 종료.
        ///
        /// 재진입은 재귀 호출 대신 yield return으로 꼬리 재진입(스택 안전).
        /// </summary>
        private IEnumerator EnterCombatLoopV3()
        {
            if (_combatUseCase == null || _unitData == null) yield break;
            if (!_combatUseCase.HasEnemyInRange(_unitData)) yield break;
            // 공격 중에는 실제 이동 목표가 없다. 마지막 추격 목적지를 남기면 A2가 이를
            // 현재 공격 desired로 오해하므로 전투 정지 경계에서 optional 값을 명시적으로 비운다.
            ClearUnitActionShadowDesiredTarget();

            if (NetworkContext.IsNetworkActive)
            {
                // 멀티플레이 서버: 현재 Action이 소유하고 있는 공개 타겟을 그대로 전달한다.
                // 여기서 새 타겟을 다시 찾으면 이동 후보 위치에서 확정한 대상과 다른 대상을
                // 공격 시작에 사용할 수 있으므로, 최초 진입과 같은 typed payload를 사용한다.
                GameEvents.OnUnitEnteredCombat.OnNext(new UnitEnteredCombatEvent(
                    _unitData.Id,
                    _combatTargetId,
                    _combatTargetIsUnit));

                // 사거리 내인 동안 대기 (이동 없음 — 규칙 13).
                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                // 현재 공격 사이클이 끝날 때까지 쿨다운 대기.
                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                // 사이클 완료 후 사거리에 다시 들어왔으면 같은 루프 재진입 (꼬리 재진입).
                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    yield return EnterCombatLoopV3();
                    yield break;
                }

                // 이동 재개 애니메이션 — Walk CrossFade.
                ResumeWalkAnimation();
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
            }
            else
            {
                // 싱글플레이: 매 프레임 TryAttack(내부 쿨다운 체크).
                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                {
                    _combatUseCase.TryAttack(_unitData, Time.timeAsDouble);
                    yield return null;
                }

                if (!_unitData.IsAlive) yield break;

                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    yield return EnterCombatLoopV3();
                    yield break;
                }

                _combatUseCase.ClearCombatState(_unitData.Id);

                ResumeWalkAnimation();
            }
        }

        /// <summary>
        /// [V3] 근접 전투 종료 후 A* 재개 시 출발 타일을 결정한다 (규칙 11/15).
        ///
        /// 동작:
        ///   1) 호출 측이 미리 계산한 forwardTile을 받는다 — 이중 계산 방지.
        ///   2) ReducerAuthoritative는 정렬 frame에서 커밋된 실제 공간 위치를 그대로 사용한다.
        ///      Legacy rollback만 기존 ProcessStep waypoint 의미를 유지한다.
        ///   3) Walk 애니메이션을 재개한다.
        ///   4) 현재 커밋 위치에서 finalTarget으로 새 path를 받아 외부 while로 재진입한다.
        ///
        /// 뒤쪽 타일로 복귀하지 않는다(규칙 11/15).
        ///
        /// [BUG-002 — 2026-05-13]
        ///   이전에는 transform.position을 forwardTile에 즉시 스냅했으나, 이는 규칙 9(걸어서 재개)
        ///   위반이며 1타일 순간이동의 직접 원인이었다. 이제 transform.position 정렬과 공간
        ///   타일 커밋은 호출 측 공용 writer가 처리하며, 이 함수는 path 재발급만 담당한다.
        ///
        /// [중요] forwardTile 이중 계산 방지:
        ///   호출 측은 Lerp 시작 전에 안전한 복귀 후보를 1회 계산하여 forwardTile을 결정한 뒤,
        ///   그 값으로 transform을 정렬하고 동일한 forwardTile을 이 함수에 전달해야 한다.
        ///   만약 이 함수가 복귀 후보를 다시 계산하면, 정렬 후의 transform.position
        ///   기준으로 한 타일 더 앞쪽이 선택될 수 있어 도메인-뷰 좌표가 다시 어긋난다.
        /// </summary>
        private List<HexCoord> ResumeFromForwardTileV3(HexCoord finalTarget, HexCoord forwardTile)
        {
            if (_movementUseCase == null || _unitData == null || !_unitData.IsAlive)
                return null;

            // 멀티 Legacy rollback만 waypoint 의미의 직접 도메인 갱신을 유지한다.
            // ReducerAuthoritative는 앞선 정렬 frame의 실제 Root 경계 전이가 이미 커밋했으므로
            // forwardTile을 미리 점유하거나 UnitData.Position에 기록하지 않는다.
            if (_lockedMovementPipelineMode != UnitMovementPipelineMode.ReducerAuthoritative
                && NetworkContext.IsNetworkActive
                && forwardTile != _unitData.Position)
            {
                _movementUseCase.ProcessStep(_unitData, _unitData.Position, forwardTile);
            }

            // ────────────────────────────────────────────────────────────
            // [BUG-002 수정 — 2026-05-13] 즉시 스냅 제거.
            //
            //   이전 구현:
            //     Vector3 forwardWorld = HexMetrics.HexToWorld(forwardTile);
            //     Vector3 forwardView = ViewConverter.ToView(forwardWorld);
            //     forwardView.y += HexMetrics.UnitYOffset;
            //     transform.position = forwardView;   ← 1타일 순간이동의 원인.
            //
            //   현재:
            //     호출 측(MoveAlongPathV3)의 공용 writer가 forwardTile 방향으로 걸으며
            //     실제 Root 경계만 Domain에 커밋한다. 여기서는 path만 다시 발급한다.
            // ────────────────────────────────────────────────────────────

            // Walk 애니메이션 재개 — 전투 중 Attack 상태였을 수 있음.
            ResumeWalkAnimation();
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // 새 path 계산 — finalTarget으로 RequestMove. 실패 시 null 반환(호출 측이 종료 처리).
            List<HexCoord> newPath = RequestLatestAuthoritativeRepath(finalTarget, resolveRallyGoal: true);
            return newPath;
        }

        /// <summary>
        /// 이동 종료 시 정리 + 1회성 콜백 호출.
        /// 외부 while 종료(완주/사망/RESUME 실패) 직후 + path 비정상 종료 직후 등에서 호출.
        /// </summary>
        private void MoveCleanupAndCompleteV3(bool invokeCompletion = true)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (UnitSpatialDiagnosticAccountingPolicy
                .IsUnexpectedRecoverableCleanup(
                    _spatialRecoverablePending,
                    _unitData != null && _unitData.IsAlive))
            {
                UnitMovementAuthorityObserver.ObserveSpatialCleanupAfterRecoverable(
                    _unitData != null ? _unitData.Id : -1);
            }
#endif
            _spatialRecoverablePending = false;
            _rootRotationOwnership.ReleaseAll();
            ClearCombatTargetTracking();
            _moveCoroutine = null;
            ClearUnitActionShadowDesiredTarget();
            SetMovementHeldAnimation(true);

            // 혼잡도 기여 종료 — 이동이 끝났으니 새 코루틴 전까지는 발행하지 않는다.
            _isAStarMoving = false;

            // 이동이 완전히 종료되었으므로 부드러운 교체 관련 상태도 모두 초기화.
            // 이렇게 해두지 않으면 다음에 MoveTo가 호출되기 전에 OnPathInvalidated가 끼어들었을 때
            // 이미 종료된 코루틴의 잔재 정보를 보고 잘못된 분기를 탈 수 있다.
            _pendingPath = null;
            _currentNextTileCoord = null;

            // 1회성 이동 완료 콜백 (랠리→Castle 자동 이동 체인 등에서 사용).
            var callback = OnMoveComplete;
            OnMoveComplete = null;
            _activeRallyPoint = null;
            _rallyPolicyCompletionPending = false;
            if (invokeCompletion)
                callback?.Invoke();
        }

    }
}
