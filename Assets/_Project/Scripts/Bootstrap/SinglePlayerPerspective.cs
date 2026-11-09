// ============================================================================
// SinglePlayerPerspective.cs
// 싱글플레이 경기를 시작할 때 "로컬 팀"과 "화면 뒤집힘"을 기본값으로 되돌리는 정적 도우미.
//
// [왜 필요한가 — 초급 개발자용 설명]
//   LocalPlayerTeam(Infrastructure) 과 ViewConverter(Core) 는 둘 다 "static" 클래스다.
//   static 값은 씬을 다시 로드해도 지워지지 않고, 앱(프로세스)이 꺼질 때까지 남는다.
//
//   멀티플레이에서 Client 는 NetworkGameFlow 가 LocalPlayerTeam.Set(Red) 를 호출해 Red 가 된다.
//   그런데 경기가 끝나고 로비로 돌아가도 이 값을 Blue 로 되돌리는 코드가 한 곳도 없었다
//   (LocalPlayerTeam.Reset() 호출처 0건). 그래서 같은 실행에서 이어서 싱글을 시작하면
//   GameBootstrapper.LoadMap 의 싱글 분기가 남아 있던 Red 를 그대로 읽어
//   ViewConverter.Setup(isFlipped: true) 를 호출 → "싱글인데 내 팀이 Red, 화면도 뒤집힘" 버그.
//   (기기 로그 증거: [TOWER-RANGE] Show … Role=Single, LocalTeam=Red, ViewFlipped=true)
//
// [왜 "싱글 경기 시작" 지점에서 되돌리는가]
//   - 멀티 경기 끝(NetworkCombatController 디스폰)에서 되돌리면 안 된다:
//     ReconnectionHandler 가 연결이 끊긴 뒤에도 LocalPlayerTeam.Current 로 자기 팀을 다시 확인하고,
//     결과 화면도 경기 끝 직후 로컬 팀을 읽는다. 그 시점에 Blue 로 바꾸면 Client(Red) 의 판정이 틀어진다.
//   - 싱글플레이에서는 LocalPlayerTeam.Set() 을 부르는 코드가 없다(유일한 호출처는 멀티의
//     NetworkGameFlow.WaitForTeamAndSendReady). 즉 싱글 경기의 로컬 팀은 언제나 Blue 가 정답이다.
//   - 멀티 경기는 시작할 때마다 NetworkGameFlow 가 팀을 다시 Set 하고, GameBootstrapper.StartNetworkGame
//     이 ViewConverter 를 다시 Setup 하므로, 싱글 시작에서 Blue 로 되돌려도 다음 멀티 Client 는
//     정상적으로 다시 Red 가 된다.
//   → 그래서 "남아 있던 값을 실제로 읽어 쓰는 바로 그 자리(싱글 LoadMap)" 에서 되돌리는 것이 가장 안전하다.
//
// Bootstrap 레이어 — 조합 루트(composition root)라 Core(ViewConverter) 와
// Infrastructure(LocalPlayerTeam) 를 둘 다 참조할 수 있다. Domain 은 건드리지 않는다.
// 회귀 검사(Editor 의 RunUnitActionSelfValidation.ValidateSinglePlayerPerspectiveReset)가
// 이 클래스를 직접 호출해 확인하므로 public 으로 둔다.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Infrastructure;

namespace Hexiege.Bootstrap
{
    /// <summary>
    /// 싱글플레이 경기 시작 시 로컬 팀(Blue)과 화면 뒤집힘(없음)을 확정하는 정적 도우미.
    /// 이전 멀티 경기(Client=Red)에서 남은 정적 상태가 싱글로 새어 들어오는 것을 막는다.
    /// </summary>
    public static class SinglePlayerPerspective
    {
        /// <summary>
        /// 싱글 시점 적용 결과. 진단 로그([LOCAL-TEAM] MatchStart)와 회귀 검사가 함께 쓴다.
        /// "Previous*" 는 되돌리기 직전 값, "Current*" 는 적용 후 값이다.
        /// </summary>
        public readonly struct Result
        {
            /// <summary>되돌리기 직전 LocalPlayerTeam.Current.</summary>
            public readonly TeamId PreviousTeam;
            /// <summary>되돌리기 직전 LocalPlayerTeam.IsAssigned(멀티가 팀을 할당했던 흔적).</summary>
            public readonly bool PreviousAssigned;
            /// <summary>되돌리기 직전 ViewConverter.IsFlipped.</summary>
            public readonly bool PreviousFlipped;
            /// <summary>적용 후 LocalPlayerTeam.Current (정상이면 항상 Blue).</summary>
            public readonly TeamId CurrentTeam;
            /// <summary>적용 후 ViewConverter.IsFlipped (정상이면 항상 false).</summary>
            public readonly bool CurrentFlipped;

            /// <summary>
            /// 이전 경기(멀티)의 값이 남아 있어서 이번에 실제로 되돌렸는지.
            /// Blue 가 아니었거나 · 멀티가 할당한 흔적이 있었거나 · 화면이 뒤집혀 있었으면 true.
            /// </summary>
            public bool WasStale => PreviousTeam != TeamId.Blue || PreviousAssigned || PreviousFlipped;

            /// <summary>결과 구조체를 만든다(Apply 내부 전용).</summary>
            public Result(TeamId previousTeam, bool previousAssigned, bool previousFlipped,
                          TeamId currentTeam, bool currentFlipped)
            {
                PreviousTeam = previousTeam;
                PreviousAssigned = previousAssigned;
                PreviousFlipped = previousFlipped;
                CurrentTeam = currentTeam;
                CurrentFlipped = currentFlipped;
            }
        }

        /// <summary>
        /// 싱글플레이 경기의 시점을 확정한다.
        /// ① LocalPlayerTeam 을 기본값(Blue, 미할당)으로 되돌리고
        /// ② 기존 LoadMap 싱글 분기의 규칙 그대로 "LocalPlayerTeam 이 Red 면 뒤집기" 로 ViewConverter 를 설정한다
        ///    (①을 거쳤으므로 결과는 항상 뒤집힘 없음 + 이번 맵의 중심).
        /// 반드시 ApplyConfig() 이후(HexMetrics 준비 후 계산한 mapCenter)에, 맵 렌더링 전에 호출해야 한다.
        /// 멀티플레이 경로에서는 호출하지 않는다(멀티는 StartNetworkGame 이 팀 기준으로 직접 Setup 한다).
        /// </summary>
        /// <param name="mapCenter">HexMetrics.GridCenter 로 계산한 이번 맵의 중심 월드 좌표.</param>
        /// <returns>되돌리기 전후 값(진단 로그 · 회귀 검사용).</returns>
        public static Result Apply(Vector3 mapCenter)
        {
            // 되돌리기 전 값을 먼저 기록해 둔다 — "실제로 남아 있었는지" 를 로그로 판정하기 위해서다.
            TeamId previousTeam = LocalPlayerTeam.Current;
            bool previousAssigned = LocalPlayerTeam.IsAssigned;
            bool previousFlipped = ViewConverter.IsFlipped;

            // ① 이전 멀티 경기(Client=Red)가 남긴 팀 값을 싱글 기본값(Blue)으로 되돌린다.
            //    싱글에서 LocalPlayerTeam 을 Set 하는 코드는 없으므로 이 Reset 이 정답을 바꾸는 경우는 없다.
            LocalPlayerTeam.Reset();

            // ② 기존 LoadMap 싱글 분기 로직을 그대로 옮겨 온 부분(제거가 아니라 이동).
            //    "LocalPlayerTeam 이 Red 면 화면 뒤집기" 규칙은 유지하되, ①을 먼저 거치므로 여기서는 항상 false 가 된다.
            //    mapCenter 도 매번 새로 넣어 이전 경기의 맵 중심이 남지 않게 한다.
            bool isRed = (LocalPlayerTeam.Current == TeamId.Red);
            ViewConverter.Setup(isRed, mapCenter);

            return new Result(previousTeam, previousAssigned, previousFlipped,
                              LocalPlayerTeam.Current, ViewConverter.IsFlipped);
        }
    }
}
