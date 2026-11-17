// ============================================================================
// RunUnitActionSelfValidation.AirStrikeAim.cs
// [2026-11-17 지원 스킬 2-L 공습 — L-3 조준 입력 · 두 손가락 카메라 + L-4 조준 표시] 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-17/09_12_support-skill-air-strike/ (Plan 7절 LS-T13 · LS-T1 의 「빨강 뷰 변환 뒤 도메인 방향」(P-10)).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   손가락 입력은 에디터 메뉴로 재현할 수 없다(마우스뿐이고, 두 손가락은 기기에서만 된다). 그래서 L-3 에서 "손가락이 닿고 떨어지는
//   순서"의 판정을 입력 장치와 떼어 낸 순수 상태 머신(SkillAimPointerTracker)으로 만들었다. 이 검사는 그 상태 머신에
//   가짜 손가락 목록(터치 Id + 화면 좌표)을 프레임마다 넣어 떼는 순서별 결과를 숫자로 확인한다.
//   방향 계산(SkillLineAimMath)도 ViewConverter 만 읽는 순수 함수라, 뒤집힌(빨강) 화면을 흉내 내어 도메인 방향을 확인한다.
//
// 무엇을 확인하는가:
//   LS-T13 ① A 누름 → B 누름 → B 뗌 → A 뗌            : 발동 후보 1회(Released), 두 손가락 동안 MultiTouch, B 를 뗀 뒤 한 손가락 복귀.
//          ② A → B → A 뗌 → B 뗌 → 새 터치 C → C 뗌     : A 를 뗄 때 ReleasedNoFire(TrackedLiftedFirst), B 를 떼도 0회, C 로 발동 1회.
//          ③ ①에서 A 를 X 위에서 뗌                      : release 좌표 = A 의 마지막 좌표(X 영역 안) — 취소 판정은 그 좌표로 한다.
//          ④ A 를 뗀 프레임에 B 만 남음(primaryTouch 가 B 로 바뀌는 순서) : 캐시 좌표는 A 의 마지막 좌표(B 좌표로 오염 없음),
//             그 뒤 B 가 움직여도 캐시 · 추적 없음.
//          + 버튼 gesture 대기 · 같은 프레임에 함께 뗌(LiftedTogether) · release 와 같은 프레임의 새 손가락(AfterRelease) ·
//            한 프레임에 두 손가락 동시 터치(작은 Id 추적) · 마우스 Id 경로.
//   P-10   빨강(뒤집힌) 화면에서 시작점 · 손가락을 둘 다 도메인으로 바꾼 방향 = 화면 방향의 반대, 거리 보존,
//          도메인 → 화면 방향 왕복, 첫 터치 기본 방향(화면 위쪽), 최소 드래그 경계(= 임계값이면 발사). 파랑(뒤집힘 없음)은 그대로.
//   소스 게이트: 조준 컨트롤러가 추적기로 판정 · 옛 primaryTouch 판정 호출 0 · 카메라 두 손가락 / 오른쪽 버튼 분기 ·
//          2터치 팬 금지 가드 비활성 · 패널 공습 분기와 방향 전달 · 조합 루트 맵 경계 배선 · 탭 오발동 억제.
//
// 하지 않는 것: 실제 Touchscreen 장치 · 카메라 이동량 · 조준 표시 모양(육안) — 사용자 실기 항목(Plan 8절 10~14 · 20~25).
// ViewConverter 는 맵 중심을 읽는 API 가 없어 이전 값을 되돌릴 수 없다 → 끝나면 EditMode 기본값으로 초기화한다(VfxConnections · LocalTeamReset 선례).
// 실행 중(Play)에는 돌지 않는다.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using Hexiege.Core;
using Hexiege.Presentation;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        /// <summary>
        /// 공습 L-3 · L-4 조준 입력 회귀 검증 진입점(LS-T13 · P-10 · 소스 게이트).
        /// </summary>
        private static void ValidateAirStrikeAimInput()
        {
            Require(!UnityEngine.Application.isPlaying, "Air strike aim fixture must run outside a live match.");
            ValidateAimTrackerReleaseOrder();   // LS-T13 ① ② ③
            ValidateAimTrackerCacheIsolation(); // LS-T13 ④
            ValidateAimTrackerEdgeCases();      // 버튼 대기 · 함께 뗌 · release 프레임 새 손가락 · 동시 터치 · 마우스
            ValidateLineAimViewConversion();    // P-10
            ValidateAirStrikeAimSourceGates();  // 배선 · 비활성화 소스 게이트
            Debug.Log("[UAS-DIAG][SKILL-AIM] self-validation PASS: second finger lifted first returns to one-finger aim and the last lift fires once, tracked finger lifted first never fires (even after the other finger lifts) until a new touch, X release uses the tracked finger's last position (LS-T13 ① ② ③), the release cache stays on the tracked id when the remaining finger moves (LS-T13 ④), button-gesture wait / lifted-together / same-frame new finger / simultaneous touch / mouse id paths, red-view direction computed in domain space with distance preserved and round trip (P-10), and the input / camera / panel / bootstrap / tap-suppression source gates.");
        }

        // ── fixture 도우미 ─────────────────────────────────────────────────

        // 한 프레임 동안 "눌려 있는" 손가락 목록을 만든다(Id, x, y 를 차례로).
        private static List<AimPointerSample> AimFrame(params float[] idXY)
        {
            var list = new List<AimPointerSample>();
            for (int i = 0; i + 2 < idXY.Length; i += 3)
                list.Add(new AimPointerSample((int)idXY[i], new Vector2(idXY[i + 1], idXY[i + 2])));
            return list;
        }

        private static readonly List<AimPointerSample> NoFingers = new List<AimPointerSample>();

        // LS-T13 ① ② ③.
        private static void ValidateAimTrackerReleaseOrder()
        {
            const int A = 3, B = 7, C = 11;

            // ① A 누름 → B 누름 → B 뗌 → A 뗌.
            var t = new SkillAimPointerTracker();
            t.Reset(waitForButtonRelease: false);
            AimPointerFrame f = t.Step(AimFrame(A, 100, 100));
            Require(f.Event == AimPointerEvent.Began && f.TrackedId == A && !f.MultiTouch,
                "LS-T13 ① the first finger A must begin tracking (one finger).");
            f = t.Step(AimFrame(A, 120, 100, B, 400, 400));
            Require(f.Event == AimPointerEvent.Moved && f.MultiTouch && f.CountFrom == 1 && f.CountTo == 2 && f.TrackedPos == new Vector2(120, 100),
                "LS-T13 ① a second finger B must keep the aim (Moved, MultiTouch) and the tracked position must stay A's.");
            f = t.Step(AimFrame(A, 140, 100));
            Require(f.Event == AimPointerEvent.Moved && !f.MultiTouch && f.CountChanged && !f.TrackedLifted,
                "LS-T13 ① lifting B first must return to one-finger aim (Moved, no MultiTouch, Lifted=Other).");
            f = t.Step(NoFingers);
            Require(f.Event == AimPointerEvent.Released && f.TrackedLifted && f.TrackedPos == new Vector2(140, 100),
                "LS-T13 ① lifting the last finger A must be a fire candidate at A's last cached position.");
            f = t.Step(NoFingers);
            Require(f.Event == AimPointerEvent.None, "LS-T13 ① nothing more must happen after the release.");

            // ② A → B → A 뗌 → B 뗌 → C 터치 → C 뗌.
            t.Reset(waitForButtonRelease: false);
            t.Step(AimFrame(A, 100, 100));
            t.Step(AimFrame(A, 110, 100, B, 400, 400));
            f = t.Step(AimFrame(B, 410, 400));
            Require(f.Event == AimPointerEvent.ReleasedNoFire && f.NoFireReason == SkillAimPointerTracker.ReasonTrackedLiftedFirst
                    && f.TrackedLifted && f.WaitReason == AimPointerWaitReason.TrackedLiftedFirst,
                "LS-T13 ② lifting the tracked finger A first must not fire (TrackedLiftedFirst) and must wait for all fingers.");
            f = t.Step(AimFrame(B, 430, 400));
            Require(f.Event == AimPointerEvent.None && !t.HasTracked, "LS-T13 ② the remaining finger B must not be used for aiming.");
            f = t.Step(NoFingers);
            Require(f.Event == AimPointerEvent.None, "LS-T13 ② lifting the remaining finger B must not fire.");
            f = t.Step(AimFrame(C, 200, 200));
            Require(f.Event == AimPointerEvent.Began && f.TrackedId == C, "LS-T13 ② a new touch C after all fingers lifted must begin aiming again.");
            t.Step(AimFrame(C, 220, 200));
            f = t.Step(NoFingers);
            Require(f.Event == AimPointerEvent.Released && f.TrackedPos == new Vector2(220, 200),
                "LS-T13 ② releasing the new touch C must be the fire candidate.");

            // ③ ①에서 A 를 X(취소 버튼) 위에서 뗌 — release 좌표 = A 의 마지막 좌표. X 영역 = 화면 아래 가운데 (450~630, 0~120) 가정.
            var cancelRect = new Rect(450f, 0f, 180f, 120f);
            t.Reset(waitForButtonRelease: false);
            t.Step(AimFrame(A, 300, 600));
            t.Step(AimFrame(A, 310, 600, B, 900, 900));
            t.Step(AimFrame(A, 540, 60));          // B 를 먼저 뗌 + A 를 X 위로 옮김
            f = t.Step(NoFingers);
            Require(f.Event == AimPointerEvent.Released && cancelRect.Contains(f.TrackedPos),
                "LS-T13 ③ releasing A over X must report A's last position inside the cancel rect (the controller cancels from it).");
        }

        // LS-T13 ④ — A 를 뗀 프레임에 B 만 남는 순서(실기에서 primaryTouch 가 B 로 바뀌는 순서). 캐시 = A 의 마지막 좌표.
        private static void ValidateAimTrackerCacheIsolation()
        {
            const int A = 1, B = 2;
            var t = new SkillAimPointerTracker();
            t.Reset(waitForButtonRelease: false);
            t.Step(AimFrame(A, 50, 50));
            t.Step(AimFrame(A, 60, 55, B, 700, 800));
            AimPointerFrame f = t.Step(AimFrame(B, 705, 805)); // A 뗌 — 목록의 첫 손가락(= primaryTouch 자리)은 이제 B
            Require(f.Event == AimPointerEvent.ReleasedNoFire && f.TrackedPos == new Vector2(60, 55) && t.TrackedPos == new Vector2(60, 55),
                "LS-T13 ④ the release cache must stay at A's last position, not the remaining finger B.");
            t.Step(AimFrame(B, 900, 950));
            Require(t.TrackedPos == new Vector2(60, 55) && !t.HasTracked,
                "LS-T13 ④ moving the remaining finger B must not overwrite the cache or start tracking.");
        }

        // 버튼 gesture 대기 · 함께 뗌 · release 와 같은 프레임의 새 손가락 · 동시 터치 · 마우스 Id.
        private static void ValidateAimTrackerEdgeCases()
        {
            var t = new SkillAimPointerTracker();

            // 조준 진입 버튼을 누른 손가락(Id 5)은 조준에 쓰지 않는다 — 떨어진 뒤 새 터치부터.
            t.Reset(waitForButtonRelease: true);
            AimPointerFrame f = t.Step(AimFrame(5, 10, 10));
            Require(f.Event == AimPointerEvent.None && f.WaitReason == AimPointerWaitReason.ButtonGesture,
                "Aim edge: the skill-button finger must be ignored while the button gesture is held.");
            f = t.Step(NoFingers);
            Require(f.Event == AimPointerEvent.None && t.WaitReason == AimPointerWaitReason.None,
                "Aim edge: releasing the button finger must not fire and must end the wait.");
            f = t.Step(AimFrame(6, 300, 300));
            Require(f.Event == AimPointerEvent.Began && f.TrackedId == 6, "Aim edge: the next new touch must begin aiming.");

            // 두 손가락이 같은 프레임에 함께 떨어짐 → 한 손가락만 남은 적이 없으므로 발동 안 함(LiftedTogether).
            t.Step(AimFrame(6, 300, 300, 8, 500, 500));
            f = t.Step(NoFingers);
            Require(f.Event == AimPointerEvent.ReleasedNoFire && f.NoFireReason == SkillAimPointerTracker.ReasonLiftedTogether
                    && t.WaitReason == AimPointerWaitReason.None,
                "Aim edge: both fingers lifted in the same frame must not fire (LiftedTogether) and a new touch is accepted next.");

            // release 와 같은 프레임에 이미 새 손가락이 닿아 있으면 발동 후보는 그대로, 새 손가락은 다 뗀 뒤에야 받는다.
            t.Reset(waitForButtonRelease: false);
            t.Step(AimFrame(9, 100, 100));
            f = t.Step(AimFrame(12, 800, 800));
            Require(f.Event == AimPointerEvent.Released && f.TrackedPos == new Vector2(100, 100) && t.WaitReason == AimPointerWaitReason.AfterRelease,
                "Aim edge: the lone tracked finger released while another lands in the same frame must still be the fire candidate.");
            f = t.Step(AimFrame(12, 810, 800));
            Require(f.Event == AimPointerEvent.None && !t.HasTracked, "Aim edge: the same-frame new finger must not be tracked.");

            // 한 프레임에 두 손가락이 동시에 닿음 → 작은 Id 를 추적, 처음부터 MultiTouch.
            t.Reset(waitForButtonRelease: false);
            f = t.Step(AimFrame(20, 400, 400, 14, 100, 100));
            Require(f.Event == AimPointerEvent.Began && f.TrackedId == 14 && f.MultiTouch && f.TrackedPos == new Vector2(100, 100),
                "Aim edge: two fingers landing in the same frame must track the smaller id and start as MultiTouch.");

            // 마우스 왼쪽 버튼(에디터)도 손가락 하나처럼 — 누름 → 끌기 → 뗌.
            t.Reset(waitForButtonRelease: false);
            int m = SkillAimPointerTracker.MousePointerId;
            f = t.Step(AimFrame(m, 10, 20));
            Require(f.Event == AimPointerEvent.Began && f.TrackedId == m, "Aim edge: the mouse pointer id must begin tracking.");
            t.Step(AimFrame(m, 30, 40));
            f = t.Step(NoFingers);
            Require(f.Event == AimPointerEvent.Released && f.TrackedPos == new Vector2(30, 40),
                "Aim edge: releasing the mouse must be the fire candidate at its last position.");
        }

        // P-10 — 시작점 · 손가락을 둘 다 도메인으로 바꾼 뒤의 방향.
        private static void ValidateLineAimViewConversion()
        {
            try
            {
                // 빨강(뒤집힌) 화면 — 맵 중심 (5, 0, 7).
                ViewConverter.Setup(true, new Vector3(5f, 0f, 7f));
                Vector3 anchorView = new Vector3(2f, 0f, 3f);
                Vector3 fingerView = new Vector3(5f, 0f, 7f); // 화면에서 (+3, +4) 방향으로 5 끌었다
                Vector3 anchorDomain = ViewConverter.FromView(anchorView); // 조준 컨트롤러와 같은 변환(시작점)
                bool ok = SkillLineAimMath.TryComputeDomainDirection(anchorDomain, fingerView, out Vector3 dir, out float dist);
                Require(ok && Mathf.Abs(dist - 5f) < 1e-4f && Mathf.Abs(dir.x + 0.6f) < 1e-4f && Mathf.Abs(dir.z + 0.8f) < 1e-4f && dir.y == 0f,
                    "P-10 on the flipped (red) view the domain direction must be the opposite of the view drag (-0.6, -0.8) with distance 5 preserved.");
                Vector3 back = SkillLineAimMath.DomainDirectionToView(anchorDomain, dir);
                Require(Mathf.Abs(back.x - 0.6f) < 1e-4f && Mathf.Abs(back.z - 0.8f) < 1e-4f,
                    "P-10 converting the domain direction back to the flipped view must point along the finger drag (0.6, 0.8).");
                Vector3 def = SkillLineAimMath.DefaultDomainDirection(anchorDomain);
                Require(Mathf.Abs(def.x) < 1e-4f && Mathf.Abs(def.z + 1f) < 1e-4f,
                    "P-10 the first-touch default direction must be screen-up, i.e. domain -Z on the flipped view.");

                // 파랑(뒤집힘 없음) — 화면 방향 = 도메인 방향.
                ViewConverter.Reset();
                anchorDomain = ViewConverter.FromView(anchorView);
                ok = SkillLineAimMath.TryComputeDomainDirection(anchorDomain, fingerView, out dir, out dist);
                Require(ok && Mathf.Abs(dist - 5f) < 1e-4f && Mathf.Abs(dir.x - 0.6f) < 1e-4f && Mathf.Abs(dir.z - 0.8f) < 1e-4f,
                    "P-10 on the unflipped (blue / single) view the domain direction must equal the view drag (0.6, 0.8).");
                def = SkillLineAimMath.DefaultDomainDirection(anchorDomain);
                Require(Mathf.Abs(def.z - 1f) < 1e-4f, "P-10 the default direction must be domain +Z on the unflipped view.");

                // 손가락이 시작점 위(거리 0) → 방향 없음.
                ok = SkillLineAimMath.TryComputeDomainDirection(anchorDomain, anchorView, out dir, out dist);
                Require(!ok && dist == 0f && dir == Vector3.zero, "P-10 a zero drag must give no direction.");

                // 최소 드래그 경계(U-5 · U-10): = 임계값이면 발사, 그보다 작으면 미발사.
                Require(SkillLineAimMath.IsDragEnough(0.5f, 0.5f) && !SkillLineAimMath.IsDragEnough(0.4999f, 0.5f),
                    "U-5 the minimum drag must fire at exactly the threshold and not below it.");
            }
            finally
            {
                // 맵 중심을 읽는 API 가 없어 이전 값으로 되돌릴 수 없다 → EditMode 기본값(뒤집힘 없음)으로 초기화.
                ViewConverter.Reset();
            }
        }

        // 소스 게이트 — 배선과 비활성화가 실제 코드 줄(주석 아님)에 들어 있는지.
        private static void ValidateAirStrikeAimSourceGates()
        {
            const string root = "Assets/_Project/Scripts/";
            string aim = File.ReadAllText(root + "Presentation/Input/SkillAimController.cs");
            Require(SupportActiveLineCount(aim, "AimPointerFrame frame = _pointerTracker.Step(_pressedSamples);") == 1
                    && SupportActiveLineCount(aim, "GatherPressedPointers(_pressedSamples);") == 1
                    && SupportActiveLineCount(aim, "var touches = touchscreen.touches;") == 1
                    && SupportActiveLineCount(aim, "ResolveRelease(_lastDragScreenPos);") == 0
                    && SupportActiveLineCount(aim, "if (WasPointerReleasedThisFrame())") == 0
                    && SupportActiveLineCount(aim, "if (WasPointerPressedThisFrame())") == 0,
                "LS-T13 the aim controller must judge contacts through the touch-id tracker, with the old primaryTouch release / press checks disabled.");
            Require(SupportActiveLineCount(aim, "if (!frame.MultiTouch && _cameraController != null)") == 1,
                "U-18 the circle aim must edge-scroll only while one finger is down.");

            string cam = File.ReadAllText(root + "Presentation/Camera/CameraController.cs");
            Require(SupportActiveLineCount(cam, "HandleTwoFingerGesture(activeTouches, aiming, panSpeed);") == 1
                    && SupportActiveLineCount(cam, "HandleRightDragPan(mouse, aiming, panSpeed);") == 1
                    && SupportActiveLineCount(cam, "if (activeTouches.Count >= 2)") == 0
                    && SupportActiveLineCount(cam, "ApplyZoomDelta((prevDist - currDist) * zoomSpeed * 0.01f, zoomMin, zoomMax);") == 1,
                "U-11 the camera must run the two-finger pan + zoom and right-drag pan, with the old two-touch pan guard disabled and the pinch formula kept.");

            string panel = File.ReadAllText(root + "Presentation/UI/BuildingSkillPanelUI.cs");
            Require(SupportActiveLineCount(panel, "if (skill.Mechanic == SkillMechanicType.LineStrike)") == 1
                    && SupportActiveLineCount(panel, "_skillAimController.BeginLineAim(buildingId, slotIndex, skill.Radius,") == 1
                    && SupportActiveLineCount(panel, "ActivateSkill(buildingId, slotIndex, startWorld, true, direction);") == 1,
                "L-3 the skill panel must route the air strike to the line aim and pass the direction to ActivateSkill.");

            string boot = File.ReadAllText(root + "Bootstrap/GameBootstrapper.Setup.cs");
            Require(SupportActiveLineCount(boot, "_skillAimController.SetMapWorldBoundsProvider(") == 1,
                "L-4 the composition root must give the aim controller the same map bounds as the server (R-14).");

            string input = File.ReadAllText(root + "Presentation/Input/InputHandler.cs");
            Require(SupportActiveLineCount(input, "if (_contactHadMultiTouch) LogTapSuppressed(") == 2,
                "R-18 both the touch and mouse release paths must suppress the click after a two-finger gesture.");
        }
    }
}
