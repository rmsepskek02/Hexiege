// ============================================================================
// VfxPoolItem.StatusLoop.cs
// 건물(방어탑) 위에 "계속" 떠 있는 상태 VFX — 시작 · 정지와 확인 번호(lease).
// 2026-11-04 EMP Task 커밋 3(화면 — Plan 3-5 V6 a)에서 새로 만들었다.
//
// 무엇을 하나(초급자용 설명):
//   EMP에 맞아 멈춘 적 방어탑 위에는 "합선 · 연기" 이펙트(V6, vfx_emp_disabled)가 무력화가 끝날 때까지 계속 보여야 한다.
//   이 프리팹의 파티클은 전부 반복 재생(Looping)이라, 한 번 재생하면 누가 멈추기 전까지 영원히 나온다.
//   그래서 "언제 멈출지"를 바깥(화면 구독자 TowerDisableVfxPresenter)이 정하고, 이 파일은 멈추는 방법 두 가지를 준다.
//     ① 꼬리 정지(clear = false): 새 입자만 그만 만들고, 이미 나온 입자는 제 수명대로 사라지게 둔다.
//        V6 입자 최대 수명이 0.8초라 "정지 후 꼬리 ≤ 0.8초"가 저절로 지켜진다. 입자가 다 사라지면
//        VfxPoolItem.Update의 기존 검사(AnyAlive == false → Return)가 스스로 풀에 돌려보낸다(새 코드 없음).
//        쓰는 곳: 무력화 시간이 끝났을 때, 경기가 끝났을 때(사용자 결정 Q5).
//     ② 즉시 정지(clear = true): 이미 나온 입자까지 그 자리에서 지우고 바로 풀에 돌려보낸다.
//        쓰는 곳: 무력화 중 탑이 파괴 · 철거됐을 때(사용자 결정 Q4 — 탑이 사라졌는데 스파크만 남지 않게).
//
// 확인 번호(lease)가 왜 필요한가:
//   풀 항목은 재사용된다. 꼬리 정지 뒤 0.8초 안에 풀로 돌아간 이 항목이 "다른 탑의 V6"나 "전혀 다른 이펙트"로
//   다시 쓰일 수 있다. 그때 옛 주인이 늦게 "멈춰"를 부르면 엉뚱한 재생이 꺼진다.
//   그래서 시작할 때마다 새 번호를 발급하고, 정지는 "내가 받은 번호 == 지금 번호"이고 아직 상태 루프가
//   켜져 있을 때만 받아 준다(힐 이펙트 _bloomLease 선례와 같은 방식).
//
// 쓰는 순서(반드시 이 순서):
//   EffectManager가 Play(...)로 재생한 "뒤에" BeginStatusLoop()를 부른다.
//   Play가 ResetTravelState → ResetStatusLoop로 이전 재생의 상태 루프 표시를 먼저 지우기 때문이다
//   (시전자 귀속 BeginCasterOwnership과 같은 규칙).
//
// 탑의 자식으로 붙이지 않는다:
//   이 항목은 EffectManager의 VFX 컨테이너 아래에 그대로 둔다. 탑 GameObject는 파괴 때 BuildingFactory가 Destroy하므로,
//   탑의 자식이면 풀 항목까지 함께 파괴돼 풀에서 사라진다(Research 6-2 · 위험 R-5).
//
// 화면 표현 전용이다. 탑 공격 판정(서버의 BuildingStatusEffectSystem)과 무관하다.
// Presentation 레이어 — Unity 의존 (MonoBehaviour, ParticleSystem).
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    public partial class VfxPoolItem
    {
        // ── 재생별 상태 루프 표시 (ResetStatusLoop에서 비운다) ──
        // 지금 이 재생이 "바깥이 멈출 때까지 계속 나오는 상태 VFX"인가. 정지(꼬리 · 즉시)하는 순간 false가 된다.
        private bool _statusLoop;

        // ── 확인 번호(lease) — 절대 되돌리지 않는다 ──
        // 시작할 때마다 1씩 올린다. 0은 "번호 없음(시작 실패)"으로 쓰므로 발급하지 않는다.
        // 풀 반환 · 재사용 때도 지우지 않는다(지우면 옛 번호와 새 번호가 같아질 수 있다).
        private ulong _statusLease;

        /// <summary>
        /// 읽기 전용 확인: 지금 재생 중이며, 주어진 확인 번호의 상태 루프가 아직 켜져 있는지(정지 전인지).
        /// 화면 구독자가 "내가 시작한 그 이펙트가 아직 살아 있나"를 물을 때 쓴다.
        /// </summary>
        /// <param name="lease">BeginStatusLoop가 돌려준 확인 번호.</param>
        /// <returns>번호가 맞고 아직 정지하지 않았으면 true.</returns>
        public bool IsStatusLoopActive(ulong lease)
            => _active && _statusLoop && lease != 0 && lease == _statusLease;

        /// <summary>
        /// 방금 Play()로 시작한 재생을 "상태 루프"로 표시하고 새 확인 번호를 발급한다.
        /// 반드시 Play() 뒤에 부른다(Play가 이전 재생의 표시를 먼저 지운다).
        /// </summary>
        /// <returns>새 확인 번호(1 이상). 재생 중이 아니면 0(시작 실패 — 호출자는 정지를 부르지 않는다).</returns>
        public ulong BeginStatusLoop()
        {
            if (!_active) return 0;
            _statusLease++;
            if (_statusLease == 0) _statusLease++; // 아주 오래 돌아 한 바퀴 넘쳐도 0(번호 없음)은 건너뛴다
            _statusLoop = true;
            return _statusLease;
        }

        /// <summary>
        /// 상태 루프를 멈춘다. 확인 번호가 지금 번호와 다르거나, 이미 멈췄거나, 재생 중이 아니면 아무것도 하지 않는다
        /// (이미 풀로 돌아가 다른 재생에 쓰이는 항목을 잘못 멈추지 않게).
        /// </summary>
        /// <param name="lease">BeginStatusLoop가 돌려준 확인 번호.</param>
        /// <param name="clear">
        /// false = 꼬리 정지(새 입자만 멈춤 — 남은 입자가 사라지면 기존 Update가 스스로 풀에 반환).
        /// true = 즉시 정지(남은 입자까지 지우고 바로 풀에 반환).
        /// </param>
        /// <returns>실제로 멈췄으면 true.</returns>
        public bool StopStatusLoop(ulong lease, bool clear)
        {
            if (!IsStatusLoopActive(lease)) return false;

            // 표시를 먼저 내린다 → 아래 Return() 도중이나 같은 프레임의 두 번째 정지 요청이 같은 일을 두 번 하지 않는다.
            _statusLoop = false;

            ParticleSystemStopBehavior behavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            // 재생 때와 같은 구조로 멈춘다: 루트 PS가 있으면 하위까지 한 번에, 없으면 형제 시스템을 각각.
            //   (VfxPoolItem.Play · StopCasterOwned와 같은 분기)
            if (_ps != null) _ps.Stop(true, behavior);
            else if (_childSystems != null)
                for (int i = 0; i < _childSystems.Length; i++)
                    if (_childSystems[i] != null) _childSystems[i].Stop(true, behavior);

            // 즉시 정지만 여기서 바로 반환한다. 꼬리 정지는 입자가 다 사라진 뒤 Update가 반환한다(기존 자동 반환 경로).
            if (clear) Return();
            return true;
        }

        /// <summary>
        /// 상태 루프 표시를 비운다(확인 번호는 그대로 둔다). 새 재생 시작 · 풀 반환이 모두 거치는
        /// ResetTravelState에서 호출되므로, 재사용된 항목에 옛 상태 루프 표시가 남지 않는다.
        /// </summary>
        private void ResetStatusLoop()
        {
            _statusLoop = false;
        }
    }
}
