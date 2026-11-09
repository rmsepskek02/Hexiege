// ============================================================================
// RunUnitActionSelfValidation.LocalTeamReset.cs
// [2026-11-10 버그 수정 — 멀티 Client(Red) 뒤 같은 실행의 싱글이 Red · 화면 뒤집힘으로 시작] 회귀 검증(fixture).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   LocalPlayerTeam(로컬 팀) · ViewConverter(화면 뒤집힘)는 static 이라 씬이 바뀌어도 값이 남는다.
//   멀티 Client 경기에서 Red 로 Set 된 값이 되돌려지지 않아, 이어서 시작한 싱글이 그 Red 를 읽고
//   화면을 뒤집었다. 수정은 싱글 LoadMap 이 SinglePlayerPerspective.Apply 로 팀을 Blue 로 되돌린 뒤
//   ViewConverter 를 설정하게 한 것이다.
//
// 무엇을 확인하는가:
//   L-1  남은 Red · 뒤집힘(멀티 Client 흔적) → Apply → Blue · 미할당 · 뒤집힘 없음 · ToView 항등, WasStale=true.
//   L-2  이미 기본값인 상태에서 Apply → 값 그대로, WasStale=false(정상 첫 싱글은 "AlreadyDefault" 로 판정).
//   L-3  Apply 뒤에도 멀티 흐름(LocalPlayerTeam.Set(Red) + ViewConverter.Setup(true))은 그대로 Red · 뒤집힘이 된다
//        (= 싱글에서 되돌린 것이 다음 멀티 Client 를 깨지 않는다).
//   L-4  소스 게이트: GameBootstrapper.Map.cs 의 싱글 분기가 Apply 를 호출하고, 남은 팀을 그대로 읽어
//        뒤집던 옛 두 줄이 LoadMap 에 다시 생기지 않았다.
// 실제 씬 로드 · 로비 복귀 · 카메라 화면은 여기서 확인하지 않는다(사용자 실기 항목).
// 검사 뒤 정적 상태는 EditMode 기본값(Blue · 미할당 / 뒤집힘 없음)으로 되돌린다
//   (ViewConverter 는 맵 중심을 읽는 API 가 없어 이전 값을 그대로 복원할 수 없다 — VfxConnections 검사와 같은 방식).
// ============================================================================

using System.IO;
using Hexiege.Bootstrap;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        /// <summary>
        /// 싱글 경기 시작 시 로컬 팀 · 화면 뒤집힘 초기화(SinglePlayerPerspective.Apply)를 검증한다.
        /// 순수 정적 상태만 다루므로 씬 · 프리팹 · NGO 가 필요 없다.
        /// </summary>
        private static void ValidateSinglePlayerPerspectiveReset()
        {
            // 원래 값을 기록해 두고 finally 에서 되돌린다(다른 검사에 영향을 주지 않게).
            TeamId originalTeam = LocalPlayerTeam.Current;
            bool originalAssigned = LocalPlayerTeam.IsAssigned;
            try
            {
                Vector3 oldCenter = new Vector3(7f, 0f, 11f);
                Vector3 newCenter = new Vector3(3f, 0f, 5f);
                Vector3 probe = new Vector3(1.5f, 0.2f, -2.5f);

                // ── L-1: 멀티 Client 경기가 남긴 상태(Red · 할당됨 · 뒤집힘)를 흉내 낸 뒤 싱글 시작 ──
                LocalPlayerTeam.Set(TeamId.Red);
                ViewConverter.Setup(true, oldCenter);
                SinglePlayerPerspective.Result stale = SinglePlayerPerspective.Apply(newCenter);
                Require(stale.PreviousTeam == TeamId.Red && stale.PreviousAssigned && stale.PreviousFlipped
                        && stale.WasStale,
                    "L-1 SinglePlayerPerspective must report the stale Red/assigned/flipped state left by a Client match.");
                Require(LocalPlayerTeam.Current == TeamId.Blue && !LocalPlayerTeam.IsAssigned
                        && stale.CurrentTeam == TeamId.Blue,
                    "L-1 single match start must reset LocalPlayerTeam to Blue and unassigned.");
                Require(!ViewConverter.IsFlipped && !stale.CurrentFlipped && ViewConverter.ToView(probe) == probe,
                    "L-1 single match start must leave the view unflipped (ToView identity).");

                // ── L-2: 이미 기본값인 정상 첫 싱글 — 바뀌는 것이 없고 WasStale=false ──
                SinglePlayerPerspective.Result clean = SinglePlayerPerspective.Apply(newCenter);
                Require(!clean.WasStale && clean.CurrentTeam == TeamId.Blue && !clean.CurrentFlipped,
                    "L-2 applying on default state must keep Blue/unflipped and report AlreadyDefault (WasStale=false).");

                // ── L-3: 되돌린 뒤에도 다음 멀티 Client 흐름(Set Red → Setup 뒤집힘)은 그대로 동작 ──
                LocalPlayerTeam.Set(TeamId.Red);
                ViewConverter.Setup(true, newCenter);
                Vector3 flipped = ViewConverter.ToView(probe);
                Require(LocalPlayerTeam.Current == TeamId.Red && LocalPlayerTeam.IsAssigned && ViewConverter.IsFlipped
                        && Mathf.Approximately(flipped.x, 2f * newCenter.x - probe.x)
                        && Mathf.Approximately(flipped.y, probe.y)
                        && Mathf.Approximately(flipped.z, 2f * newCenter.z - probe.z),
                    "L-3 a later network Client match must still become Red and flipped after the single reset.");

                // ── L-4: 소스 게이트 — 싱글 LoadMap 분기가 Apply 를 거친다 ──
                string mapSource = File.ReadAllText("Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs");
                Require(mapSource.Contains("SinglePlayerPerspective.Apply(mapCenter)")
                        && mapSource.Contains("DiagLocalTeamSingleMatchStart(perspective)"),
                    "L-4 GameBootstrapper.LoadMap single branch must call SinglePlayerPerspective.Apply and log [LOCAL-TEAM] MatchStart.");
                Require(!mapSource.Contains("bool isRed = (LocalPlayerTeam.Current == TeamId.Red);"),
                    "L-4 GameBootstrapper.LoadMap must not read a possibly stale LocalPlayerTeam directly for the single view flip.");
            }
            finally
            {
                // EditMode 기본값으로 복원. 원래 할당돼 있었다면 그 팀을 다시 Set 한다.
                if (originalAssigned) LocalPlayerTeam.Set(originalTeam);
                else LocalPlayerTeam.Reset();
                ViewConverter.Reset();
            }

            Debug.Log("[UAS-DIAG][LOCAL-TEAM] self-validation PASS: single match start resets a stale network Red team and view flip to Blue/unflipped (L-1), reports AlreadyDefault on a clean start (L-2), keeps the next network Client Red/flipped flow intact (L-3), and LoadMap's single branch routes through SinglePlayerPerspective.Apply (L-4).");
        }
    }
}
