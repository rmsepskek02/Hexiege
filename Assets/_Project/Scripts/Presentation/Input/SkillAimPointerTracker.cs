// ============================================================================
// SkillAimPointerTracker.cs
// 스킬 조준 중 "손가락(포인터)이 닿고 떨어지는 순서"를 판정하는 순수 상태 머신.
// (2026-11-17 지원 스킬 2-L 공습 Task L-3 — _Tasks/2026-11-17/09_12_support-skill-air-strike,
//  Plan U-15 · U-16 · U-17 · U-18, 메인 세션 검토 b, 자가 검증 LS-T13)
//
// 왜 따로 떼었나(유니티 초급자용 설명):
//   예전 조준 코드는 "첫 번째 손가락(Touchscreen.primaryTouch)" 하나만 읽었다. 그런데 두 손가락으로 화면을
//   옮기다가 첫 손가락을 먼저 떼면, 유니티가 primaryTouch 를 "남은 손가락"으로 바꿔 보고할 수 있다.
//   그러면 「첫 손가락을 뗐다 = 발동」 판정이 잘못되거나, 마지막으로 기억해 둔 좌표가 남은 손가락 좌표로
//   바뀌어 엉뚱한 곳에 발동될 수 있다(Plan R-16).
//   그래서 이 클래스는 손가락을 "터치 Id(손가락마다 붙는 번호)"로 따라간다. 처음 닿은 손가락의 Id 를 기억하고,
//   그 Id 의 좌표만 캐시한다. 다른 손가락이 무엇을 하든 캐시는 오염되지 않는다.
//
//   또 이 클래스는 유니티 입력 장치(Touchscreen · Mouse)를 직접 읽지 않는다. 매 프레임 "지금 눌려 있는 손가락
//   목록(Id + 화면 좌표)"만 받아서 판정한다. 그래서 에디터 회귀 스위트가 가짜 손가락 목록을 넣어
//   떼는 순서(LS-T13)를 숫자로 확인할 수 있다.
//
// 판정 규칙(사용자 결정 U-16 「한 손가락만 남아야 발동」):
//   ① 첫 손가락(추적 손가락)이 "혼자 남은 상태"에서 떨어지면 → Released(발동 후보 — 취소 X · 공습 최소 드래그는 호출자가 본다).
//   ② 첫 손가락이 "다른 손가락이 아직 닿아 있는 상태"에서 먼저 떨어지면 → ReleasedNoFire(TrackedLiftedFirst).
//      그 뒤 남은 손가락을 떼도 아무 일도 없다. 손가락이 모두 떨어진 뒤 "새로 터치"해야 다시 조준이 시작된다.
//   ③ 첫 손가락과 다른 손가락이 "같은 프레임에 함께" 떨어지면 → ReleasedNoFire(LiftedTogether) — 한 손가락만 남은 적이 없으므로 발동하지 않는다.
//   ④ 두 번째 손가락을 먼저 떼면 추적 손가락은 그대로 → 다시 한 손가락 조준(Moved)으로 돌아간다.
//
// 이 파일은 UnityEngine.Vector2(값 타입)만 쓴다. MonoBehaviour · 입력 장치 · 씬 의존 없음.
// Presentation 레이어(조준 입력 전용 도우미).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 이번 프레임에 "눌려 있는" 포인터 하나(터치 Id + 화면 좌표). 마우스 왼쪽 버튼은 <see cref="SkillAimPointerTracker.MousePointerId"/>로 넣는다.
    /// </summary>
    public readonly struct AimPointerSample
    {
        /// <summary> 포인터 Id(터치 Id, 마우스는 MousePointerId). </summary>
        public int Id { get; }

        /// <summary> 화면 좌표(픽셀). </summary>
        public Vector2 ScreenPos { get; }

        /// <summary> 포인터 샘플 생성. </summary>
        /// <param name="id">포인터 Id.</param>
        /// <param name="screenPos">화면 좌표.</param>
        public AimPointerSample(int id, Vector2 screenPos)
        {
            Id = id;
            ScreenPos = screenPos;
        }
    }

    /// <summary> 한 프레임 판정 결과의 종류. </summary>
    public enum AimPointerEvent
    {
        /// <summary> 추적 중인 손가락 없음(대기 중). </summary>
        None = 0,
        /// <summary> 새 접촉 시작 — 이 프레임에 추적 손가락이 정해졌다(누른 순간). </summary>
        Began = 1,
        /// <summary> 추적 손가락이 계속 눌려 있다(드래그 중 — 한 손가락 또는 두 손가락). </summary>
        Moved = 2,
        /// <summary> 추적 손가락이 혼자 남은 상태에서 떨어졌다 — 발동 후보(U-16 ①). </summary>
        Released = 3,
        /// <summary> 추적 손가락이 다른 손가락보다 먼저(또는 함께) 떨어졌다 — 발동하지 않고 조준 유지(U-16 ②). </summary>
        ReleasedNoFire = 4,
    }

    /// <summary> 새 접촉을 받지 않고 "모든 손가락이 떨어지기를" 기다리는 이유. </summary>
    public enum AimPointerWaitReason
    {
        /// <summary> 기다리지 않는다(새 터치를 받을 수 있다). </summary>
        None = 0,
        /// <summary> 조준 진입 버튼을 누른 손가락이 아직 닿아 있다(그 gesture는 무시 — 옛 _pendingButtonRelease). </summary>
        ButtonGesture = 1,
        /// <summary> 추적 손가락이 먼저 떨어져 남은 손가락만 닿아 있다(U-16 ② — 남은 손가락은 조준에 쓰지 않는다). </summary>
        TrackedLiftedFirst = 2,
        /// <summary> 발동 후보 release 와 같은 프레임에 이미 다른 손가락이 닿아 있었다(그 손가락은 새 접촉으로 쓰지 않는다). </summary>
        AfterRelease = 3,
    }

    /// <summary> 한 프레임 판정 결과(값 타입 — 매 프레임 할당 없음). </summary>
    public readonly struct AimPointerFrame
    {
        /// <summary> 이번 프레임 사건. </summary>
        public AimPointerEvent Event { get; }
        /// <summary> 추적 손가락의 마지막 화면 좌표(release 프레임에는 "떨어지기 직전" 캐시 값 — 규칙 20 · 20-1). </summary>
        public Vector2 TrackedPos { get; }
        /// <summary> 추적 손가락 Id(없으면 -1 — 단, MousePointerId 도 음수다). </summary>
        public int TrackedId { get; }
        /// <summary> 지난 프레임에 눌려 있던 손가락 수. </summary>
        public int CountFrom { get; }
        /// <summary> 이번 프레임에 눌려 있는 손가락 수. </summary>
        public int CountTo { get; }
        /// <summary> 손가락 수가 바뀌었는가(From ≠ To). </summary>
        public bool CountChanged => CountFrom != CountTo;
        /// <summary> 손가락 수가 줄었을 때, 떨어진 손가락 중에 추적 손가락이 있었는가. </summary>
        public bool TrackedLifted { get; }
        /// <summary> 추적 손가락이 눌린 채 다른 손가락도 닿아 있다(두 손가락 이상 — U-15 · U-18). </summary>
        public bool MultiTouch { get; }
        /// <summary> ReleasedNoFire 의 사유(TrackedLiftedFirst · LiftedTogether). 그 밖에는 빈 문자열. </summary>
        public string NoFireReason { get; }
        /// <summary> 이 프레임 판정 뒤 대기 이유(진단 로그용). </summary>
        public AimPointerWaitReason WaitReason { get; }

        /// <summary> 결과 생성. </summary>
        public AimPointerFrame(AimPointerEvent ev, Vector2 trackedPos, int trackedId, int countFrom, int countTo,
                               bool trackedLifted, bool multiTouch, string noFireReason, AimPointerWaitReason waitReason)
        {
            Event = ev;
            TrackedPos = trackedPos;
            TrackedId = trackedId;
            CountFrom = countFrom;
            CountTo = countTo;
            TrackedLifted = trackedLifted;
            MultiTouch = multiTouch;
            NoFireReason = noFireReason ?? string.Empty;
            WaitReason = waitReason;
        }
    }

    /// <summary>
    /// 스킬 조준용 포인터 상태 머신 — 추적 손가락을 터치 Id 로 따라가고, 떼는 순서로 발동 / 미발동을 판정한다.
    /// </summary>
    public sealed class SkillAimPointerTracker
    {
        /// <summary> 마우스 왼쪽 버튼을 손가락 하나처럼 넣을 때 쓰는 Id(터치 Id 와 겹치지 않는 음수). </summary>
        public const int MousePointerId = -1000;

        /// <summary> ReleasedNoFire 사유 — 추적 손가락이 다른 손가락보다 먼저 떨어졌다(U-16 ②). </summary>
        public const string ReasonTrackedLiftedFirst = "TrackedLiftedFirst";

        /// <summary> ReleasedNoFire 사유 — 추적 손가락과 다른 손가락이 같은 프레임에 함께 떨어졌다. </summary>
        public const string ReasonLiftedTogether = "LiftedTogether";

        private bool _hasTracked;
        private int _trackedId = -1;
        private Vector2 _trackedPos;
        private int _prevCount;
        private AimPointerWaitReason _wait;

        /// <summary> 지금 추적 중인 손가락이 있는가. </summary>
        public bool HasTracked => _hasTracked;

        /// <summary> 추적 손가락의 마지막 화면 좌표(캐시 — 추적 Id 의 좌표만 담긴다). </summary>
        public Vector2 TrackedPos => _trackedPos;

        /// <summary> 지금 새 접촉을 받지 않고 기다리는 이유. </summary>
        public AimPointerWaitReason WaitReason => _wait;

        /// <summary>
        /// 상태를 처음으로 되돌린다(조준 시작 · 종료).
        /// </summary>
        /// <param name="waitForButtonRelease">
        /// true면 "지금 닿아 있는 손가락(조준 진입 버튼을 누른 손가락)이 모두 떨어질 때까지" 새 접촉을 받지 않는다(규칙 17 — 버튼 gesture 무시).
        /// </param>
        public void Reset(bool waitForButtonRelease)
        {
            _hasTracked = false;
            _trackedId = -1;
            _prevCount = 0;
            _wait = waitForButtonRelease ? AimPointerWaitReason.ButtonGesture : AimPointerWaitReason.None;
            // _trackedPos 는 지우지 않는다 — 호출자가 "마지막 좌표" 로 쓰는 일은 없고, 지워도 얻는 것이 없다.
        }

        /// <summary>
        /// 한 프레임을 판정한다. <paramref name="pressed"/>는 "이번 프레임에 눌려 있는" 포인터 전부다
        /// (이번 프레임에 떨어진 손가락은 넣지 않는다 — 떨어진 손가락의 좌표는 믿을 수 없기 때문이다. 규칙 20-1).
        /// </summary>
        /// <param name="pressed">눌려 있는 포인터 목록(순서 무관, null 이면 0개로 본다).</param>
        /// <returns>이번 프레임 판정 결과.</returns>
        public AimPointerFrame Step(IReadOnlyList<AimPointerSample> pressed)
        {
            int count = pressed != null ? pressed.Count : 0;
            int from = _prevCount;
            _prevCount = count;

            // ── 1) 기다리는 중: 손가락이 모두 떨어지면 기다림을 끝낸다. 이 프레임에는 새 접촉을 받지 않는다 ──
            //   (같은 프레임에 0 이 되었다가 바로 새로 누르는 일은 한 프레임 안에서 일어날 수 없다 —
            //    "모두 뗀 프레임"과 "새로 누른 프레임"은 서로 다른 프레임이다.)
            if (_wait != AimPointerWaitReason.None)
            {
                AimPointerWaitReason waitedFor = _wait;
                if (count == 0) _wait = AimPointerWaitReason.None;
                return new AimPointerFrame(AimPointerEvent.None, _trackedPos, -1, from, count,
                    false, false, string.Empty, waitedFor);
            }

            // ── 2) 추적 중인 손가락이 있다 ──
            if (_hasTracked)
            {
                if (TryFind(pressed, _trackedId, out Vector2 pos))
                {
                    // 추적 손가락이 아직 눌려 있다 → 좌표 캐시 갱신(추적 Id 의 좌표만 — R-16).
                    _trackedPos = pos;
                    return new AimPointerFrame(AimPointerEvent.Moved, _trackedPos, _trackedId, from, count,
                        false, count >= 2, string.Empty, AimPointerWaitReason.None);
                }

                // 추적 손가락이 떨어졌다. 캐시(_trackedPos)는 "떨어지기 직전"의 마지막 좌표 그대로다.
                int liftedId = _trackedId;
                _hasTracked = false;
                _trackedId = -1;

                if (from <= 1)
                {
                    // ① 지난 프레임에 추적 손가락 혼자였다 → 발동 후보.
                    //   이번 프레임에 이미 다른 손가락이 새로 닿아 있으면 그 손가락은 새 접촉으로 쓰지 않는다(다 뗀 뒤 다시 터치).
                    _wait = count > 0 ? AimPointerWaitReason.AfterRelease : AimPointerWaitReason.None;
                    return new AimPointerFrame(AimPointerEvent.Released, _trackedPos, liftedId, from, count,
                        true, false, string.Empty, _wait);
                }

                // ② · ③ 다른 손가락과 함께 닿아 있던 중에 추적 손가락이 떨어졌다 → 발동하지 않는다(조준 유지).
                string reason = count > 0 ? ReasonTrackedLiftedFirst : ReasonLiftedTogether;
                _wait = count > 0 ? AimPointerWaitReason.TrackedLiftedFirst : AimPointerWaitReason.None;
                return new AimPointerFrame(AimPointerEvent.ReleasedNoFire, _trackedPos, liftedId, from, count,
                    true, false, reason, _wait);
            }

            // ── 3) 추적 중인 손가락이 없고 기다리지도 않는다 → 새로 닿은 손가락이 있으면 그 손가락을 추적한다 ──
            if (count > 0)
            {
                // 한 프레임에 두 손가락이 동시에 닿은 드문 경우에는 Id 가 가장 작은 손가락을 "첫 손가락"으로 정한다
                //   (목록 순서에 기대지 않고 항상 같은 손가락을 고르기 위해서다).
                AimPointerSample first = pressed[0];
                for (int i = 1; i < count; i++)
                    if (pressed[i].Id < first.Id) first = pressed[i];

                _hasTracked = true;
                _trackedId = first.Id;
                _trackedPos = first.ScreenPos;
                return new AimPointerFrame(AimPointerEvent.Began, _trackedPos, _trackedId, from, count,
                    false, count >= 2, string.Empty, AimPointerWaitReason.None);
            }

            return new AimPointerFrame(AimPointerEvent.None, _trackedPos, -1, from, count,
                false, false, string.Empty, AimPointerWaitReason.None);
        }

        /// <summary> 목록에서 Id 가 같은 포인터를 찾는다(할당 없는 선형 탐색 — 손가락은 많아야 몇 개). </summary>
        private static bool TryFind(IReadOnlyList<AimPointerSample> pressed, int id, out Vector2 pos)
        {
            if (pressed != null)
            {
                for (int i = 0; i < pressed.Count; i++)
                {
                    if (pressed[i].Id == id)
                    {
                        pos = pressed[i].ScreenPos;
                        return true;
                    }
                }
            }
            pos = default;
            return false;
        }
    }
}
