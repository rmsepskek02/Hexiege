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
//   - [2026-11-10 비활성화 — 아래 「즉시 발동 스킬」 참고] 조준 좌표가 없는 즉시 발동 스킬(HasAim=false): 재생 위치 규칙이 아직 없다(지금 켜진 즉시 발동 스킬도 없다).
//   - 스킬 데이터에 발동 VFX 프리팹이 비어 있는 스킬: 연출 없음(기존 스킬 자산은 모두 비어 있다 → 동작 무변경).
//
// 즉시 발동 스킬(조준 없음 — 전술 지령 · 화염의 축복 · 숲의 포효 · 숲의 순풍 등)의 재생 위치(2026-11-10 추가):
//   사용자 결정 D3(_Tasks/2026-11-10/04_20_support-buff-skills-status-vfx Plan) — 「시전 건물 위, 양쪽 화면」.
//   발동 알림에 이미 실려 있는 건물 Id(BuildingId — 스킬 규칙 25)로 이 화면의 건물 데이터를 찾아,
//   그 건물 타일 중심(도메인) → ToView(이 화면 좌표) + 지면 높이 0.06(아래 「지면 높이 보정」 공통 규칙 그대로)에서 재생한다.
//   건물 데이터 조회 함수는 조합 루트가 Initialize의 넷째 인자로 넣는다(없으면 종전처럼 재생하지 않고 진단 줄만 남긴다).
//   예전에는 조준이 없으면 맨 먼저 「NoAim」(Info)으로 끝냈다 — 그 return은 주석으로 남겨 두었다(WORKFLOW [4]).
//   지금 즉시 발동 스킬의 발동 VFX 칸은 모두 비어 있어, 화면 결과가 바뀌는 스킬은 없다(칸이 빈 즉시 스킬은 NoPrefab Warn 1줄).
//
// 배치: 씬에 수동 배치하지 않는다. GameBootstrapper(LoadMap)가 자기 GameObject에 AddComponent하고
//   Initialize를 부른다(WavePresentationPresenter · BloomHealEffectPresenter와 같은 방식).
//
// 지면 높이 보정(2026-11-09 EMP 발사 이펙트 「타일에 절반 잘림」 수정):
//   조준 좌표의 높이는 0(땅 평면)인데 타일 윗면은 +0.05이고 불투명 + 깊이 기록이다. 그래서 Y = 0에서 재생하면
//   바닥에 눕는 고리 · 빛은 타일에 통째로 가려지고, 카메라를 보는 섬광은 아래 절반이 잘려 보였다.
//   → 재생 직전에 Y에 「스킬 VFX 지면 높이」(GameConfig.SkillVfxGroundYOffset, 기본 0.06 = 윗면 0.05 + 여유 0.01)를 더한다.
//   이 한 곳에서 더하므로 포격 · EMP · 앞으로의 스킬 발동 VFX에 모두 적용된다(EMP 탑 무력화 V6는 탑 위치 기준이라 무관).
//   X · Z는 건드리지 않으므로 빨강(뒤집힌) 화면 결과도 같다(ToView는 Y를 바꾸지 않는다).
//
// 진단 로그(2026-11-09, WORKFLOW [4] 「테스트 항목 진단 로그 규칙」 — 개발 축, 머리 [SKILL-VFX], Diag=SkillVfx):
//   재생 1회마다 ActivationVfxPlayed 1줄(루트 높이 · 타일 윗면 높이 · 「윗면 위인가」 포함 — 잘림 수정의 로그 판정),
//   재생하지 못하면 ActivationVfxSkipped 1줄. 같은 발동이 Host에서 두 줄이 되지 않는 이유는 위 「두 채널」 설명과 같다.
//
// Presentation 레이어 — Application(이벤트 · UseCase · 값 객체 · GameLog · NetworkContext) + Core(ViewConverter) 의존.
//   피해 · HP · 쿨다운에는 손대지 않는 "표현 전용" 컴포넌트다.
// ============================================================================

using System;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Domain; // [2026-11-10 D3] BuildingData(즉시 발동 스킬의 시전 건물 위치)

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

        // 스킬 발동 VFX 루트에 더할 높이(월드). 조합 루트가 GameConfig.SkillVfxGroundYOffset을 Initialize로 넘긴다.
        //   초기값은 Initialize가 불리기 전 방어용이며 GameConfig 기본값과 같은 0.06이다.
        private float _groundYOffset = DefaultGroundYOffset;

        /// <summary> 지면 높이의 기본값(GameConfig.SkillVfxGroundYOffset 초기값과 같다 — 값이 잘못됐을 때 대신 쓴다). </summary>
        public const float DefaultGroundYOffset = 0.06f;

        // 타일 메시 윗면 높이(월드). HexTile 프리팹의 ProBuilder 정점 Y가 ±0.05 두 값뿐이라는 실측값이다
        //   (HexGridRenderer의 금지 빗금 높이 주석에도 같은 실측이 있다). 코드에 이 값을 가진 상수가 따로 없어서
        //   진단 로그의 비교 기준(TileTopYmm · AboveTileTop)으로만 여기 둔다 — 재생 위치 계산에는 쓰지 않는다.
        private const float TileTopSurfaceY = 0.05f;

        // 두 채널 구독 핸들. 맵을 다시 불러와 Initialize가 또 불리면 먼저 해제해 중복 재생을 막는다.
        private IDisposable _confirmedSubscription;
        private IDisposable _receivedSubscription;

        // [2026-11-10 D3] 건물 Id → 이 화면의 건물 데이터(즉시 발동 스킬의 재생 위치 = 시전 건물). null이면 즉시 발동은 재생하지 않는다.
        private Func<int, BuildingData> _findBuilding;

        /// <summary>
        /// 의존성을 주입하고 두 채널(확정 · 수신)을 구독한다. 맵을 불러올 때마다 GameBootstrapper가 호출한다(재호출 안전).
        /// </summary>
        /// <param name="skillActivation">이번 맵의 스킬 발동 UseCase(SkillData 조회용). null이면 재생하지 않는다.</param>
        /// <param name="effects">VFX 재생 매니저. null이면 재생하지 않는다.</param>
        /// <param name="groundYOffset">
        /// 스킬 발동 VFX 루트에 더할 높이(월드 — GameConfig.SkillVfxGroundYOffset). NaN · 무한대면 기본값 0.06을 쓴다.
        /// 0이나 음수도 그대로 받는다(Inspector에서 일부러 내린 값 존중 — 그 경우 진단 로그 AboveTileTop=False로 드러난다).
        /// </param>
        /// <param name="findBuilding">
        /// [2026-11-10 D3] 건물 Id → 이 화면의 건물 데이터. 즉시 발동(조준 없음) 스킬의 발동 VFX를 시전 건물 위에서 재생할 때 쓴다.
        /// 생략(null)하면 즉시 발동 스킬은 재생하지 않고 진단 줄(NoCasterBuilding)만 남긴다.
        /// </param>
        public void Initialize(SkillActivationUseCase skillActivation, EffectManager effects, float groundYOffset,
                               Func<int, BuildingData> findBuilding = null)
        {
            DisposeSubscriptions();

            _skillActivation = skillActivation;
            _effects = effects;
            _findBuilding = findBuilding;
            // NaN · 무한대는 Inspector 입력 실수로 보고 기본값으로 되돌린다(이펙트가 화면 밖으로 날아가지 않게).
            _groundYOffset = float.IsNaN(groundYOffset) || float.IsInfinity(groundYOffset)
                ? DefaultGroundYOffset
                : groundYOffset;

            // 채널 이름을 진단 로그에 남기려고 블록 본문 람다로 감싼다(처리 자체는 두 채널이 같다).
            _confirmedSubscription = GameEvents.OnSkillActivationConfirmed.Subscribe(p => { OnSkillActivation(p, ChannelConfirmed); });
            _receivedSubscription = GameEvents.OnSkillActivationReceived.Subscribe(p => { OnSkillActivation(p, ChannelReceived); });
        }

        // 진단 로그의 Channel= 값(확정 = 싱글 · Host 화면, 수신 = 순수 클라이언트 화면).
        private const string ChannelConfirmed = "Confirmed";
        private const string ChannelReceived = "Received";

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
        /// <param name="channel">들어온 채널 이름(진단 로그용 — Confirmed 또는 Received).</param>
        private void OnSkillActivation(SkillActivationPresentation p, string channel)
        {
            // 컴포넌트가 이미 파괴됐거나 의존성이 없으면 아무것도 하지 않는다.
            //   (this == null 은 Unity의 "파괴된 객체" 검사 — 정적 이벤트가 늦게 들어오는 경우 방어.)
            if (this == null || _skillActivation == null || _effects == null) return;

            // [2026-11-10 D3 — 비활성화] 즉시 발동(조준 없음)은 재생 위치가 없어 여기서 끝냈다. 이제는 시전 건물 위에서 재생하므로
            //   이 return을 주석으로 막았다(아래 「재생 위치 정하기」가 대신한다 — 머리말 「즉시 발동 스킬」). 옛 줄(기록용):
            // if (!p.HasAim)
            // {
            //     LogVfxSkipped(p, channel, "NoAim", isWarning: false);
            //     return;
            // }

            // 팀 · 슬롯으로 이 화면 빌드의 스킬 데이터를 찾는다(두 기기 빌드가 같다는 것은 스키마 7 일치 확인이 보장).
            SkillData skill = _skillActivation.FindSkillData(p.Team, p.Slot);
            if (skill == null || skill.ActivationVfxPrefab == null)
            {
                LogVfxSkipped(p, channel, skill == null ? "NoSkillData" : "NoPrefab", isWarning: true);
                return;
            }

            // 재생 위치 정하기(2026-11-10 D3):
            //   지점 지정 스킬 = 조준 좌표(종전 그대로). 즉시 발동 스킬 = 시전 건물 타일 중심(도메인 좌표).
            //   건물을 못 찾으면(건물 조회 함수 미배선 · 늦게 도착한 알림으로 이미 사라진 건물) 재생하지 않고 Warn 1줄.
            Vector3 domainPosition;
            string anchor;
            if (p.HasAim)
            {
                domainPosition = p.AimWorld;
                anchor = AnchorAim;
            }
            else
            {
                BuildingData caster = _findBuilding != null ? _findBuilding(p.BuildingId) : null;
                if (caster == null)
                {
                    LogVfxSkipped(p, channel, "NoCasterBuilding", isWarning: true);
                    return;
                }
                // 타일 중심(높이 0). 건물 GameObject 위치를 쓰지 않는 이유: 건물 높이 오프셋이 섞이지 않고,
                //   지점 지정 스킬과 같은 "지면 + 0.06" 규칙(루트 높이 60mm)을 그대로 지키기 위해서다(Plan 단위 ③).
                domainPosition = HexMetrics.HexToWorld(caster.Position);
                anchor = AnchorCasterBuilding;
            }

            // 도메인 좌표 → 이 화면 좌표(빨강 화면이면 맵 중심 기준 X · Z 반전). 높이(Y)는 그대로.
            //   옛 줄(기록용 — 2026-11-10 D3로 재생 위치를 위에서 고르게 바꿈): Vector3 viewPosition = ViewConverter.ToView(p.AimWorld);
            Vector3 viewPosition = ViewConverter.ToView(domainPosition);
            // 지면 높이 보정 — 타일 윗면(0.05) 위로 루트를 올린다(머리말 「지면 높이 보정」). X · Z는 그대로.
            //   조준 좌표의 Y는 맵 경계 보정에서 항상 0으로 맞춰지므로 결과는 "지면 높이 그 자체"가 된다
            //   (유닛 · 건물이 HexToWorld 결과에 높이 오프셋을 "더하는" 방식과 같게 더하기로 쓴다).
            viewPosition.y += _groundYOffset;
            // 판정 반경에 맞게 VFX 크기를 맞춘다(반경 ÷ 기준 반경, 기준이 0 이하면 1).
            float scale = EffectManager.ComputeSkillVfxScale(skill.Radius, skill.VfxReferenceRadius);

            bool played = _effects.PlaySkillActivationVfx(skill.ActivationVfxPrefab, viewPosition, scale);
            // 스킬 SFX 없음(Plan Q5 ①). 효과음이 생기면 위 VFX 호출 바로 아래에서 짝으로 부른다(사운드 규칙 15).

            LogVfxPlayed(p, channel, skill.ActivationVfxPrefab.name, played, scale, viewPosition, anchor);
        }

        // 진단 로그의 Anchor= 값(2026-11-10 D3) — 재생 위치를 무엇으로 정했는가.
        private const string AnchorAim = "Aim";
        private const string AnchorCasterBuilding = "CasterBuilding";

        // ====================================================================
        // 진단 로그(개발 축 — 릴리스 빌드에서는 호출 · 문자열 조립까지 통째로 사라진다, LogRules 1.7)
        // ====================================================================

        /// <summary> 현재 화면의 역할(Single · Host · Client) — 진단 로그 Role= 값. </summary>
        private static string CurrentRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        /// <summary> 월드 단위 실수를 밀리 단위 정수로 바꾼다(로그 값에 소수점 · 문화권 차이가 끼지 않게). </summary>
        private static int ToMm(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        /// <summary>
        /// [진단 L4] 스킬 발동 VFX를 재생한 순간 1줄. RootYmm(루트 높이) · TileTopYmm(타일 윗면) · AboveTileTop으로
        /// 「타일에 잘리지 않는 높이에서 재생했는가」를 로그만으로 판정한다(모든 스킬 공통).
        /// RootY는 풀 항목에 넘긴 재생 위치의 Y다 — 스킬 VFX는 오프셋 0 · 회전 항등이라 루트 Transform 높이와 같다(VfxPoolItem.Play).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVfxPlayed(SkillActivationPresentation p, string channel, string prefabName, bool played,
                                         float scale, Vector3 viewPosition, string anchor)
        {
            bool aboveTileTop = viewPosition.y > TileTopSurfaceY;
            GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter),
                "[SKILL-VFX] ActivationVfxPlayed",
                FormattableString.Invariant(
                    $"Diag=SkillVfx, DiagRev=1, Event=ActivationVfxPlayed, Role={CurrentRole()}, Channel={channel}, Sequence={p.Sequence}, Team={p.Team}, BuildingId={p.BuildingId}, Slot={p.Slot}, Prefab={prefabName}, Played={played}, Scale={scale:F2}, ViewFlipped={ViewConverter.IsFlipped}, AimXmm={ToMm(p.AimWorld.x)}, AimZmm={ToMm(p.AimWorld.z)}, ViewXmm={ToMm(viewPosition.x)}, ViewZmm={ToMm(viewPosition.z)}, RootYmm={ToMm(viewPosition.y)}, TileTopYmm={ToMm(TileTopSurfaceY)}, AboveTileTop={aboveTileTop}, Anchor={anchor}"));
        }

        /// <summary>
        /// [진단 L4w] 스킬 발동 VFX를 재생하지 못한 경우 1줄. 조준 없음(즉시 발동)은 정상 경로라 Info,
        /// 스킬 데이터 · 프리팹이 없어 못 낸 경우는 연출 누락이라 Warn이다(LogRules 축 A).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVfxSkipped(SkillActivationPresentation p, string channel, string reason, bool isWarning)
        {
            string data = FormattableString.Invariant(
                $"Diag=SkillVfx, DiagRev=1, Event=ActivationVfxSkipped, Role={CurrentRole()}, Channel={channel}, Sequence={p.Sequence}, Team={p.Team}, Slot={p.Slot}, HasAim={p.HasAim}, Reason={reason}");
            if (isWarning)
                GameLog.Dev.Warn("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-VFX] ActivationVfxSkipped", data);
            else
                GameLog.Dev.Info("Skill", nameof(SkillActivationVfxPresenter), "[SKILL-VFX] ActivationVfxSkipped", data);
        }
    }
}
