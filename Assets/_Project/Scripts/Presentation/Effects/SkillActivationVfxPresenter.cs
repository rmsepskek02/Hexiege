// ============================================================================
// SkillActivationVfxPresenter.cs
// 스킬 발동 VFX 화면 구독자(2026-11-03 지원 스킬 Task 커밋 4 — C-1 발동 연출 동기화의 "화면" 끝).
//
// 무엇을 하나(초급자용 설명):
//   서버가 스킬 발동을 확정하면 "발동 표현 값"(SkillActivationPresentation)이 이벤트로 흘러온다.
//   이 컴포넌트는 그 값을 받아
//     ① 팀 · 슬롯으로 이 화면 빌드의 SkillData(VFX 프리팹 · 반경 · 기준 반경)를 다시 찾고,
//     ② 조준 좌표(도메인)를 ViewConverter.ToView로 이 화면 좌표로 바꾼 뒤,
//     ③ EffectManager.PlaySkillActivationVfx로 VFX를 1회 재생한다.
//
// 왜 두 채널(확정 · 수신)을 모두 구독하나:
//   - 싱글 · Host 화면: 서버 권위 쪽이라 「확정」(OnSkillActivationConfirmed)만 들어온다.
//   - 멀티 순수 클라이언트 화면: Activate를 돌리지 않으므로 「확정」은 없고, 서버 ClientRpc를 받아
//     NetworkSkillController가 발행하는 「수신」(OnSkillActivationReceived)만 들어온다.
//   Host는 ClientRpc 수신부의 IsServer 가드로 「수신」을 발행하지 않으므로, 어느 화면에서도 같은 발동이
//   두 번 재생되지 않는다(공격 표현 Confirmed/Received 선례와 같은 구조).
//
// 빨강(뒤집힌) 화면:
//   페이로드 좌표는 도메인 좌표 그대로다. 여기서 ToView로 바꾸면 빨강 화면에서도 올바른 자리에 나온다.
//   회전은 항등이다(프리팹이 원형). 포탄 낙하 무늬가 180° 돌아 보일 수 있다 — 연출 전용 [추정 — 게임에서 확인].
//
// 이번 범위에서 재생하지 않는 경우:
//   - 조준 좌표가 없는 즉시 발동 스킬(HasAim=false): 재생 위치 규칙이 아직 없다(지금 켜진 즉시 발동 스킬도 없다).
//   - 스킬 데이터에 발동 VFX 프리팹이 비어 있는 스킬: 연출 없음(기존 스킬 자산은 모두 비어 있다 → 동작 무변경).
//
// 배치: 씬에 수동 배치하지 않는다. GameBootstrapper(LoadMap)가 자기 GameObject에 AddComponent하고
//   Initialize를 부른다(WavePresentationPresenter · BloomHealEffectPresenter와 같은 방식).
//
// Presentation 레이어 — Application(이벤트 · UseCase · 값 객체) + Core(ViewConverter) 의존.
//   피해 · HP · 쿨다운에는 손대지 않는 "표현 전용" 컴포넌트다.
// ============================================================================

using System;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Core;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 스킬 발동 표현 이벤트(확정 · 수신)를 받아 조준 지점에 스킬 발동 VFX를 재생하는 화면 구독자.
    /// </summary>
    public sealed class SkillActivationVfxPresenter : MonoBehaviour
    {
        // 팀 · 슬롯 → SkillData 조회에 쓰는 UseCase(로드아웃 제공자 · 팀→종족 변환을 서버 판정과 공유).
        private SkillActivationUseCase _skillActivation;
        // VFX 재생 담당(풀 관리 포함).
        private EffectManager _effects;

        // 두 채널 구독 핸들. 맵을 다시 불러와 Initialize가 또 불리면 먼저 해제해 중복 재생을 막는다.
        private IDisposable _confirmedSubscription;
        private IDisposable _receivedSubscription;

        /// <summary>
        /// 의존성을 주입하고 두 채널(확정 · 수신)을 구독한다. 맵을 불러올 때마다 GameBootstrapper가 호출한다(재호출 안전).
        /// </summary>
        /// <param name="skillActivation">이번 맵의 스킬 발동 UseCase(SkillData 조회용). null이면 재생하지 않는다.</param>
        /// <param name="effects">VFX 재생 매니저. null이면 재생하지 않는다.</param>
        public void Initialize(SkillActivationUseCase skillActivation, EffectManager effects)
        {
            DisposeSubscriptions();

            _skillActivation = skillActivation;
            _effects = effects;

            _confirmedSubscription = GameEvents.OnSkillActivationConfirmed.Subscribe(OnSkillActivation);
            _receivedSubscription = GameEvents.OnSkillActivationReceived.Subscribe(OnSkillActivation);
        }

        /// <summary> 파괴(씬 언로드) 시 정적 이벤트 허브 구독을 해제한다 — 해제하지 않으면 파괴된 객체가 계속 불린다. </summary>
        private void OnDestroy()
        {
            DisposeSubscriptions();
        }

        private void DisposeSubscriptions()
        {
            _confirmedSubscription?.Dispose();
            _confirmedSubscription = null;
            _receivedSubscription?.Dispose();
            _receivedSubscription = null;
        }

        /// <summary>
        /// 발동 표현 1건을 받아 VFX를 재생한다. 두 채널이 같은 처리를 쓴다(채널 구분은 위 머리말 참고).
        /// </summary>
        /// <param name="p">서버가 확정한(또는 클라이언트가 받은) 발동 표현 값.</param>
        private void OnSkillActivation(SkillActivationPresentation p)
        {
            // 컴포넌트가 이미 파괴됐거나 의존성이 없으면 아무것도 하지 않는다.
            //   (this == null 은 Unity의 "파괴된 객체" 검사 — 정적 이벤트가 늦게 들어오는 경우 방어.)
            if (this == null || _skillActivation == null || _effects == null) return;

            // 즉시 발동(조준 없음)은 이번 범위에서 재생 위치가 없다 — 머리말 참고.
            if (!p.HasAim) return;

            // 팀 · 슬롯으로 이 화면 빌드의 스킬 데이터를 찾는다(두 기기 빌드가 같다는 것은 스키마 7 일치 확인이 보장).
            SkillData skill = _skillActivation.FindSkillData(p.Team, p.Slot);
            if (skill == null || skill.ActivationVfxPrefab == null) return;

            // 도메인 좌표 → 이 화면 좌표(빨강 화면이면 맵 중심 기준 X · Z 반전). 높이(Y)는 그대로.
            Vector3 viewPosition = ViewConverter.ToView(p.AimWorld);
            // 판정 반경에 맞게 VFX 크기를 맞춘다(반경 ÷ 기준 반경, 기준이 0 이하면 1).
            float scale = EffectManager.ComputeSkillVfxScale(skill.Radius, skill.VfxReferenceRadius);

            _effects.PlaySkillActivationVfx(skill.ActivationVfxPrefab, viewPosition, scale);
            // 스킬 SFX 없음(Plan Q5 ①). 효과음이 생기면 위 VFX 호출 바로 아래에서 짝으로 부른다(사운드 규칙 15).
        }
    }
}
