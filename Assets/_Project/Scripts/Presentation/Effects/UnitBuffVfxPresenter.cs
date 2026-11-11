// ============================================================================
// UnitBuffVfxPresenter.cs
// 강화 스킬(타입 C — 전술 지령 · 화염의 축복 · 숲의 포효 · 숲의 순풍 등) "강화 상태 VFX" 화면 구독자.
// 2026-11-10 지원 스킬 1차 강화 Task(_Tasks/2026-11-10/04_20_support-buff-skills-status-vfx) 구현 단위 ②.
//
// 무엇을 하나(초급자용 설명):
//   강화 스킬을 쓰면 서버가 그 순간 살아 있는 아군 유닛 전체에 「공격력 1.5배」 같은 상태를 건다(서버 판정 — 이 파일과 무관).
//   이 컴포넌트는 그 발동 알림을 받아, 효과를 받은 유닛마다 몸에 "강화 상태 이펙트"를 붙이고,
//   유닛이 걸어 다니면 따라가게 하고, 효과 시간이 끝나거나 유닛이 죽으면 이펙트를 뗀다.
//   이펙트 그림(프리팹)은 스킬 자산의 「강화 상태 VFX」 칸(SkillData.StatusVfxPrefab)에 스킬마다 따로 넣는다.
//   칸이 비어 있으면 아무것도 붙이지 않고 진단 로그 「BuffVfxSkipped Reason=NoPrefab」을 1줄 남긴다(효과는 그대로 걸린다).
//
// 어떤 알림을 받나(사용자 결정 D2 안 A — RPC · 스키마 변경 없음):
//   - 싱글 · Host 화면: 서버 권위 쪽이라 「발동 확정」(GameEvents.OnSkillActivationConfirmed)만 들어온다.
//   - 멀티 순수 Client 화면: 「발동 수신」(GameEvents.OnSkillActivationReceived)만 들어온다
//     (Host는 ClientRpc 수신부의 IsServer 가드로 「수신」을 발행하지 않는다 — 어느 화면에서도 같은 발동이 두 번 처리되지 않는다).
//   알림에는 팀 · 슬롯만 있으므로, 각 화면이 "자기 빌드"의 로드아웃에서 스킬 데이터(프리팹 · 지속 시간 · 대상 팀)를 다시 찾는다
//   (SkillActivationUseCase.FindSkillData — 발동 VFX 구독자 SkillActivationVfxPresenter와 같은 방식).
//   대상 유닛 = 이 화면 도메인에서 "알림을 받은 순간" 살아 있는 대상 팀 유닛 전부(사용자 결정 D1 — 발동 순간의 아군만).
//   알려진 한계(EMP R-7과 같음): Client는 전송 지연만큼 늦게 붙고 늦게 떨어진다. 발동 순간 경계의 유닛(막 죽거나 막 생긴)은
//   두 화면의 대상 목록이 다를 수 있다. 효과(피해 숫자 · HP)는 서버 계산이라 양쪽에 같게 보인다.
//
// 정지 방식(사용자 결정 D8 — EMP 선례):
//   - 효과 시간 끝(받은 순간 + 지속 시간)  → 꼬리 정지(새 입자만 멈추고 남은 입자는 사그라진다).
//   - 경기 종료                           → 꼬리 정지. 그 뒤 이번 경기에서는 새로 붙이지 않는다.
//   - 유닛 사망                           → 즉시 정지(입자까지 지운다 — 죽은 유닛 몸에 이펙트가 남지 않게, 위험 R-8).
//   - 재경기(맵 다시 불러오기 · Initialize) → 즉시 정지.
//   - 같은 스킬을 다시 사용               → 이펙트를 다시 시작하지 않고 끝나는 시각만 "지금 + 새 지속 시간"으로 바꾼다.
//     (서버도 같은 종류 상태를 새 값으로 통째로 교체한다 — UnitStatusState.ApplyOrRefresh.)
//
// 기록의 열쇠(키) = 유닛 Id + 시전 팀 + 슬롯(= 발동 스킬). 그래서 한 유닛에 서로 다른 강화 스킬 이펙트가 함께 붙을 수 있다.
//
// 뒤집힌(빨강) Client 화면:
//   유닛의 보이는 몸(Visual Root)은 실제 위치 루트의 자식이라, 루트가 도는 프레임에는 VisualRootProjector가 바로잡기 전
//   틀린 자리에 있을 수 있다. 그래서 이펙트를 시작 · 따라가기 "직전에" 그 유닛의 VisualRootProjector.ProjectNow()를 부른다
//   (VFXSFXGuide 「코드 계약 — 표현 pose를 저장해 매 프레임 읽는 소비자」 · 선례 TimedDamageEffectPresenter).
//   Host · 싱글 · 뒤집히지 않은 화면에서는 결과가 바뀌지 않는다.
//
// 예외 안전:
//   Host · 싱글에서는 「발동 확정」이 서버 발동 처리(SkillActivationUseCase.Activate) 안에서 동기로 발행된다.
//   여기서 예외가 나도 서버 발동이 깨지지 않도록, 이벤트 처리 · 매 프레임 처리는 모두 try/catch로 감싸 진단 줄만 남긴다.
//
// 배치: 씬에 수동 배치하지 않는다. GameBootstrapper(LoadMap)가 자기 GameObject에 AddComponent하고
//   맵을 불러올 때마다 Initialize를 부른다(재호출 안전 — 이전 구독 · 이펙트를 먼저 정리한다).
//
// 진단 로그(WORKFLOW [4] 「테스트 항목 진단 로그 규칙」 — 개발 축, 머리 [SKILL-BUFF], Diag=SkillBuff, DiagRev=1, Plan 5절):
//   BuffVfxStarted(발동 1건 화면 요약 — L11 · L12 · L15) · BuffVfxExtended(재사용 연장 — L6) ·
//   BuffVfxSkipped(못 붙임 — L10, NoPrefab · LoopContract는 Warn) · BuffVfxPendingResolved(몸을 늦게 찾아 붙임) ·
//   BuffVfxStopped(정지 — L13, 같은 프레임 · 같은 경로는 1줄로 묶음) · BuffVfxFault(예외 — Warn).
//   모두 상태가 바뀌는 순간에만 남기고, 매 프레임 따라가기에는 줄이 없다(LogRules 1.14-8).
//
// 2026-11-11 재구성(지속 상태 구역 Task 구현 단위 ④ — 사용자 결정 Z-21 (가)):
//   이 파일은 이제 "공통부 + 두 구동기"다. 붙이기 · 따라가기 · 정지 · 사망 · 경기 종료 · 재경기 처리(공통부)는 한 곳에 두고,
//   "누구에게 언제 붙이고 언제 떼는가"만 구동기가 정한다.
//     · 발동 구동기(이 파일 HandleActivation — 종전 강화 스킬 경로 그대로): 발동 순간 대상 팀 전원 · 끝 시각 = 받은 순간 + 지속.
//     · 구역 구성원 구동기(UnitBuffVfxPresenter.ZoneDriver.cs — 새 경로, 서리 감옥 얼음 V4):
//       서버가 보낸 구역 구성원 변경(들어옴 → 붙임, 나감 → 꼬리 정지, Z-9)과 구역 지속 시간(끝 → 꼬리 정지)으로 붙이고 뗀다.
//       같은 스킬 구역이 겹치면 한 유닛에 하나만 붙이고 "몇 개 구역에 들어 있나"를 센다(Z-5).
//   강화 스킬 쪽 동작 · 로그 키([SKILL-BUFF])는 바뀌지 않는다. 구역 쪽 로그 키는 [SKILL-ZONE]이다.
//   기록 하나가 어느 구동기 것인지는 ActiveBuffVfx.Driver로 구분한다(정지 줄 · 대기 해소 줄의 머리가 이것으로 갈린다).
//
// Presentation 레이어 — Application(이벤트 · UseCase · 값 객체 · GameLog · NetworkContext) + Core(ViewConverter) + Domain 의존.
//   서버 판정 · 피해 · HP · 상태 시간에는 손대지 않는 "표현 전용" 컴포넌트다.
// ============================================================================

using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 강화 스킬 발동 표현(확정 · 수신)을 받아 효과를 받은 유닛마다 강화 상태 VFX를 붙이고 · 따라가게 하고 · 떼는 화면 구독자.
    /// </summary>
    // [2026-11-11 Z-21 (가)] partial — 구역 구성원 구동기를 UnitBuffVfxPresenter.ZoneDriver.cs로 나눴다.
    public sealed partial class UnitBuffVfxPresenter : MonoBehaviour
    {
        // ── 기록 열쇠: 유닛 Id + 시전 팀 + 슬롯 ──
        //   readonly struct + IEquatable — Dictionary 키로 쓸 때 상자 씌우기(박싱) 할당이 생기지 않게.
        private readonly struct BuffKey : IEquatable<BuffKey>
        {
            public readonly int UnitId;
            public readonly TeamId CasterTeam;
            public readonly int Slot;

            public BuffKey(int unitId, TeamId casterTeam, int slot)
            {
                UnitId = unitId;
                CasterTeam = casterTeam;
                Slot = slot;
            }

            public bool Equals(BuffKey other)
                => UnitId == other.UnitId && CasterTeam == other.CasterTeam && Slot == other.Slot;

            public override bool Equals(object obj) => obj is BuffKey other && Equals(other);

            public override int GetHashCode() => (UnitId * 397) ^ ((int)CasterTeam * 31) ^ Slot;
        }

        // ── 유닛 하나 · 스킬 하나의 재생 기록 ──
        //   class로 둔 이유: 재사용(연장) 때 사전에서 꺼낸 뒤 끝나는 시각만 고쳐 쓰기 위해서다.
        private sealed class ActiveBuffVfx
        {
            public UnitData Unit;                // 이펙트를 붙인 유닛(참조로 들고 있어 Id 재사용에도 안전)
            public Transform Pose;               // 그 유닛의 보이는 몸(Visual Root). 처음 1번만 얻는다.
            public VisualRootProjector Projector; // Pose를 바로잡아 주는 그 유닛의 투영기(없으면 null — 옛 프리팹 등)
            public VfxPoolItem Item;             // 재생 중인 풀 항목(VFX 컨테이너 아래 — 유닛의 자식 아님). 대기 중이면 null.
            public ulong Lease;                  // 시작할 때 받은 확인 번호(정지 · 따라가기는 이 번호로만)
            public bool Pending;                 // 몸(Pose)을 아직 못 찾아 시작을 미루는 중인가
            public float EndTime;                // 꼬리 정지할 게임 시간(Time.time 기준)
            public float StartTime;              // [진단] 처음 기록한 게임 시간(정지 줄의 ActiveMs 계산용)
            public int Sequence;                 // [진단] 마지막으로 시작 · 연장시킨 발동 순번
            public GameObject Prefab;            // 붙일 프리팹(대기 뒤 늦게 시작할 때 쓴다)
            public float Scale;                  // 스킬 데이터 크기 배율
            public Vector3 Offset;               // 스킬 데이터 위치 보정
            // [2026-11-11 Z-21 (가)] 이 기록을 만든 구동기(정지 · 대기 해소 진단 줄의 머리를 고른다).
            public VfxDriver Driver = VfxDriver.Activation;
            // [2026-11-11] 구역 구동기 전용 — 이 유닛이 들어 있는 같은 스킬 구역 순번들(겹침 참조 수 — Z-5). 발동 구동기는 null.
            public HashSet<int> ZoneRefs;
        }

        // 기록을 만든 구동기(2026-11-11 Z-21 (가)).
        private enum VfxDriver { Activation, Zone }

        // 정지 줄을 "같은 프레임 · 같은 스킬 · 같은 경로"끼리 1줄로 묶기 위한 계수(만료는 보통 대상 전원이 같은 프레임에 끝난다).
        private struct StopGroup
        {
            public TeamId CasterTeam;
            public int Slot;
            public int Sequence;
            public string Path;
            public bool Immediate;
            public int Count;          // 정지한 기록 수
            public int LeaseMatched;   // 확인 번호가 맞아 실제로 멈춘 수(대기 중이던 기록은 멈출 이펙트가 없어 0)
            public int MaxActiveMs;    // 가장 오래 붙어 있던 기록의 시간(ms)
            public VfxDriver Driver;   // [2026-11-11] 어느 구동기의 정지인가(줄 머리 [SKILL-BUFF] / [SKILL-ZONE])
        }

        // 의존성(Initialize로 주입).
        private SkillActivationUseCase _skillActivation; // 팀 · 슬롯 → 스킬 데이터(이 화면 빌드의 로드아웃)
        private EffectManager _effects;                  // VFX 시작 · 정지(풀 관리 포함)
        private UnitSpawnUseCase _units;                 // 이 화면 도메인의 유닛 목록(대상 유닛 찾기)
        private Func<UnitData, Transform> _pose;         // 유닛 → 보이는 몸 Transform(조합 루트의 공통 pose 함수)

        // 지금 붙어 있는(또는 대기 중인) 기록.
        private readonly Dictionary<BuffKey, ActiveBuffVfx> _active = new Dictionary<BuffKey, ActiveBuffVfx>();
        // 매 프레임 순회용 재사용 버퍼(순회 중 사전을 바꾸지 않기 위해 — 매 프레임 new 하지 않음).
        private readonly List<BuffKey> _keyBuffer = new List<BuffKey>();
        // 유닛 사망 처리 전용 버퍼(사망 이벤트가 어느 시점에 오더라도 위 순회 버퍼와 섞이지 않게 따로 둔다).
        private readonly List<BuffKey> _deathKeyBuffer = new List<BuffKey>();
        // 정지 줄 묶음 재사용 버퍼.
        private readonly List<StopGroup> _stopGroups = new List<StopGroup>(4);

        // 경기가 끝났는가. 끝난 뒤에는 이번 경기에서 새로 붙이지 않는다(다음 Initialize에서 풀린다).
        private bool _gameEnded;

        // 구독 핸들. 맵을 다시 불러와 Initialize가 또 불리면 먼저 해제해 중복 처리를 막는다.
        private IDisposable _confirmedSubscription;
        private IDisposable _receivedSubscription;
        private IDisposable _unitDiedSubscription;
        private IDisposable _gameEndSubscription;

        // 진단 로그의 Channel= 값(확정 = 싱글 · Host 화면, 수신 = 순수 Client 화면).
        private const string ChannelConfirmed = "Confirmed";
        private const string ChannelReceived = "Received";

        /// <summary>
        /// 의존성을 주입하고 이벤트 4개(발동 확정 · 발동 수신 · 유닛 사망 · 경기 종료)를 구독한다.
        /// 맵을 불러올 때마다 GameBootstrapper가 호출한다(재호출 안전 — 이전 경기의 이펙트는 입자까지 즉시 지운다).
        /// </summary>
        /// <param name="skillActivation">이번 맵의 스킬 발동 UseCase(스킬 데이터 조회용). null이면 붙이지 않는다.</param>
        /// <param name="effects">VFX 매니저. null이면 붙이지 않는다.</param>
        /// <param name="units">이번 맵의 유닛 목록 UseCase(대상 유닛 찾기). null이면 붙이지 않는다.</param>
        /// <param name="pose">유닛 → 보이는 몸 Transform 함수(없거나 비활성이면 null을 돌려준다). null이면 붙이지 않는다.</param>
        public void Initialize(
            SkillActivationUseCase skillActivation,
            EffectManager effects,
            UnitSpawnUseCase units,
            Func<UnitData, Transform> pose)
        {
            DisposeSubscriptions();
            // 이전 맵(경기)의 이펙트가 남아 있으면 지운다 — 그 유닛들은 맵 정리 때 이미 사라졌으므로 사망과 같게 즉시(D8).
            StopAll(immediate: true, path: "Reinit");

            _skillActivation = skillActivation;
            _effects = effects;
            _units = units;
            _pose = pose;
            _gameEnded = false;

            // 채널 이름을 진단 로그에 남기려고 블록 본문 람다로 감싼다(처리 자체는 두 채널이 같다 — LogRules 1.7 람다 주의).
            _confirmedSubscription = GameEvents.OnSkillActivationConfirmed.Subscribe(p => { OnSkillActivation(p, ChannelConfirmed); });
            _receivedSubscription = GameEvents.OnSkillActivationReceived.Subscribe(p => { OnSkillActivation(p, ChannelReceived); });
            _unitDiedSubscription = GameEvents.OnUnitDied.Subscribe(OnUnitDied);
            _gameEndSubscription = GameEvents.OnGameEnd.Subscribe(OnGameEnd);

            // [2026-11-11 Z-21 (가)] 구역 구성원 구동기 — 이전 경기 구역 기록을 비우고 구성원 변경 두 채널(확정 · 수신)을 구독한다.
            ResetZoneDriver();
            SubscribeZoneDriver();
        }

        /// <summary> 파괴(씬 언로드) 시 정적 이벤트 허브 구독을 해제하고 남은 이펙트를 지운다. </summary>
        private void OnDestroy()
        {
            DisposeSubscriptions();
            // 씬 정리 중이면 풀 항목도 함께 파괴되는 중일 수 있다 — StopUnitBuffVfx가 파괴된 항목은 건너뛴다.
            StopAll(immediate: true, path: "Destroy");
        }

        private void DisposeSubscriptions()
        {
            _confirmedSubscription?.Dispose();
            _confirmedSubscription = null;
            _receivedSubscription?.Dispose();
            _receivedSubscription = null;
            _unitDiedSubscription?.Dispose();
            _unitDiedSubscription = null;
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = null;
            // [2026-11-11] 구역 구성원 변경 구독 해제(ZoneDriver.cs).
            DisposeZoneDriverSubscriptions();
        }

        // ====================================================================
        // 발동 알림 처리
        // ====================================================================

        /// <summary>
        /// 발동 표현 1건을 받아, 강화 스킬이면 대상 유닛마다 이펙트를 붙이거나(처음) 끝 시각만 늦춘다(이미 붙어 있음).
        /// Host · 싱글에서는 서버 발동 처리 안에서 불리므로 예외를 밖으로 던지지 않는다.
        /// </summary>
        /// <param name="p">서버가 확정한(또는 Client가 받은) 발동 표현 값.</param>
        /// <param name="channel">들어온 채널 이름(진단 로그용 — Confirmed 또는 Received).</param>
        private void OnSkillActivation(SkillActivationPresentation p, string channel)
        {
            try
            {
                HandleActivation(p, channel);
            }
            catch (Exception ex)
            {
                // 표현 전용 실패 — 서버 판정 · 피해 · 쿨다운에는 영향이 없다. 어떤 발동에서 났는지 남긴다.
                LogFault("Activation", channel, p.Team, p.Slot, p.Sequence, ex);
            }
        }

        private void HandleActivation(SkillActivationPresentation p, string channel)
        {
            // 컴포넌트가 이미 파괴됐거나(정적 이벤트가 늦게 들어오는 경우) 경기가 끝났으면 아무것도 하지 않는다.
            if (this == null || _gameEnded) return;
            if (_skillActivation == null || _effects == null || _units == null || _pose == null) return;

            // 팀 · 슬롯으로 이 화면 빌드의 스킬 데이터를 찾는다. 못 찾으면 발동 VFX 구독자가 이미 NoSkillData Warn을 남기므로
            //   같은 사건을 두 곳에서 남기지 않도록 여기서는 조용히 끝낸다(LogRules 1.14-9).
            SkillData skill = _skillActivation.FindSkillData(p.Team, p.Slot);
            if (skill == null) return;
            // [2026-11-11 Z-21 (가)] 지속 상태 구역 스킬이면 구역 구성원 구동기에 구역을 등록하고 끝낸다
            //   (붙이기는 구성원 변경 알림이 왔을 때 — ZoneDriver.cs). 강화 스킬(타입 C)은 아래 종전 발동 구동기 그대로다.
            if (skill.Mechanic == SkillMechanicType.PersistentStatusZone)
            {
                RegisterZone(p, channel, skill);
                return;
            }
            // 강화 상태 VFX는 타입 C(전역 상태변경) 스킬만 쓴다. 포격 · EMP 등은 이 구독자와 무관하다.
            if (skill.Mechanic != SkillMechanicType.GlobalStatusChange) return;

            var kind = (StatusEffectKind)skill.StatusKind;
            // 상태가 성립하지 않는 데이터(종류 없음 · 지속 0) — 서버도 아무 효과를 걸지 않는다(서버 쪽 BuffNoEffect Warn이 따로 있다).
            if (kind == StatusEffectKind.None || !(skill.Duration > 0f))
            {
                LogSkipped(p, channel, kind, "NoStatusEffect", null, isWarning: false);
                return;
            }
            // 회복(HealOverTime)은 상태 시스템이 아니라 기존 지속 회복(HoT) 경로를 쓰고, 그 경로의 힐 이펙트가 따로 있다.
            if (kind == StatusEffectKind.HealOverTime)
            {
                LogSkipped(p, channel, kind, "HealOverTimeKind", null, isWarning: false);
                return;
            }

            GameObject prefab = skill.StatusVfxPrefab;
            if (prefab == null)
            {
                // [진단 L10] 칸이 비어 있다 — 효과는 그대로 걸렸고 화면에만 이펙트가 없다(발동 1건당 화면마다 1줄).
                LogSkipped(p, channel, kind, "NoPrefab", null, isWarning: true);
                return;
            }
            if (!_effects.IsUnitBuffLoopContractSatisfied(prefab))
            {
                // 계약(Looping ON · Stop Action None) 위반 프리팹 — 재생하지 않는다(화상 · 독 · 힐과 같은 원칙).
                LogSkipped(p, channel, kind, "LoopContract", prefab.name, isWarning: true);
                return;
            }

            // 대상 팀: 아군 대상이면 시전 팀, 적 대상이면 반대 팀(실행기 GlobalStatusChangeExecutor와 같은 규칙).
            TeamId targetTeam = skill.TargetsAllies ? p.Team : OppositeTeam(p.Team);
            float now = Time.time;
            float endTime = now + skill.Duration;

            int targets = 0, attached = 0, extended = 0, pending = 0, fitMissing = 0, failed = 0, poolCreated = 0;
            string firstFail = null;
            foreach (UnitData unit in _units.Units.Values)
            {
                if (unit == null || !unit.IsAlive || unit.Team != targetTeam) continue;
                targets++;

                var key = new BuffKey(unit.Id, p.Team, p.Slot);
                if (_active.TryGetValue(key, out ActiveBuffVfx current))
                {
                    // 같은 유닛 · 같은 스킬이 이미 붙어 있다(또는 대기 중) — 다시 시작하지 않고 끝 시각만 바꾼다(D8 재사용 연장).
                    //   유닛 참조가 같을 때만 연장한다(같은 Id의 다른 유닛 — 재사용 — 이면 옛 기록을 버리고 새로 시작).
                    bool sameUnit = ReferenceEquals(current.Unit, unit);
                    bool playing = current.Item != null && current.Item.IsStatusLoopActive(current.Lease);
                    if (sameUnit && (playing || current.Pending))
                    {
                        current.EndTime = endTime;
                        current.Sequence = p.Sequence;
                        if (playing) extended++; else pending++;
                        continue;
                    }
                    // 기록은 있는데 재생이 이미 끝났다(비정상 — 항목 파괴 등) 또는 다른 유닛 — 옛 기록을 지우고 아래에서 새로 시작한다.
                    _active.Remove(key);
                    StopAndGroup(key, current, immediate: true, path: "Replaced");
                }

                var entry = new ActiveBuffVfx
                {
                    Unit = unit, EndTime = endTime, StartTime = now, Sequence = p.Sequence,
                    Prefab = prefab, Scale = skill.StatusVfxScale, Offset = skill.StatusVfxOffset
                };
                StartOutcome outcome = TryStart(entry, out bool fitFound, out bool createdNew, out string failReason);
                if (outcome == StartOutcome.Started)
                {
                    attached++;
                    if (!fitFound) fitMissing++;
                    if (createdNew) poolCreated++;
                    _active[key] = entry;
                }
                else if (outcome == StartOutcome.Pending)
                {
                    pending++;
                    _active[key] = entry; // 몸을 찾으면 LateUpdate가 다시 시작을 시도한다.
                }
                else
                {
                    failed++;
                    if (firstFail == null) firstFail = failReason;
                }
            }

            // [진단 L11 · L12 · L15] 발동 1건의 이 화면 요약 1줄.
            LogStarted(p, channel, kind, targetTeam, prefab.name, skill, targets, attached, extended, pending,
                fitMissing, failed, firstFail, poolCreated);
            // [진단 L6] 재사용 — 재시작 없이 끝 시각만 바꾼 유닛이 있을 때 1줄.
            if (extended > 0) LogExtended(p, channel, extended, skill.Duration);
            FlushStopGroups(); // 위에서 바꿔 끼운(Replaced) 기록이 있으면 그 정지 줄을 남긴다.
        }

        // 시작 결과.
        private enum StartOutcome { Started, Pending, Failed }

        /// <summary>
        /// 기록 하나의 이펙트를 시작해 본다. 몸(Pose)을 아직 못 찾으면 대기(Pending)로 둔다.
        /// 시작 직전에 ProjectNow()로 보이는 몸을 이번 프레임의 화면 위치로 바로잡는다(뒤집힌 Client 계약).
        /// </summary>
        private StartOutcome TryStart(ActiveBuffVfx entry, out bool fitFound, out bool createdNew, out string failReason)
        {
            fitFound = false;
            createdNew = false;
            failReason = null;

            // 몸을 아직 얻지 않았거나 얻은 몸이 비활성이면 다시 얻는다(얻을 때 짝이 되는 투영기도 1번만 찾는다).
            if (entry.Pose == null || !entry.Pose.gameObject.activeInHierarchy)
            {
                entry.Pose = _pose?.Invoke(entry.Unit);
                entry.Projector = ResolveProjector(entry.Pose);
            }
            if (entry.Pose == null || !entry.Pose.gameObject.activeInHierarchy)
            {
                // 유닛 화면 오브젝트가 아직 없다(Client 생성 순서 등) — 다음 프레임에 다시 시도한다.
                entry.Pose = null;
                entry.Projector = null;
                entry.Pending = true;
                return StartOutcome.Pending;
            }

            // 읽기 직전 바로잡기(VFXSFXGuide 「코드 계약 — 표현 pose를 저장해 매 프레임 읽는 소비자」).
            if (entry.Projector != null) entry.Projector.ProjectNow();

            VfxPoolItem item = _effects.StartUnitBuffVfx(entry.Prefab, entry.Pose, entry.Unit.Type, entry.Scale, entry.Offset,
                out ulong lease, out fitFound, out createdNew, out failReason);
            if (item == null || lease == 0)
            {
                entry.Pending = false;
                return StartOutcome.Failed;
            }
            entry.Item = item;
            entry.Lease = lease;
            entry.Pending = false;
            return StartOutcome.Started;
        }

        // ====================================================================
        // 매 프레임 — 따라가기 · 만료 · 대기 재시도
        // ====================================================================

        /// <summary>
        /// 매 프레임(유닛 이동 · 투영이 끝난 뒤인 LateUpdate): 몸을 따라가고, 시간이 지난 기록은 꼬리 정지,
        /// 죽은 유닛은 즉시 정지, 대기 중인 기록은 다시 시작을 시도한다. 기록이 없으면 바로 돌아간다(비용 없음).
        /// </summary>
        private void LateUpdate()
        {
            // [2026-11-11 Z-21 (가)] 구역 구성원 구동기 — 지속 시간이 지난 구역의 구성원 이펙트를 꼬리 정지한다(ZoneDriver.cs).
            //   아래 「기록이 없으면 바로 돌아간다」보다 앞이어야 한다(구역 기록만 남은 경우에도 끝을 처리).
            if (_zoneRecords.Count > 0) TickZoneRecords(Time.time);

            if (_active.Count == 0) return;

            float now = Time.time;
            _keyBuffer.Clear();
            foreach (KeyValuePair<BuffKey, ActiveBuffVfx> pair in _active) _keyBuffer.Add(pair.Key);

            for (int i = 0; i < _keyBuffer.Count; i++)
            {
                BuffKey key = _keyBuffer[i];
                if (!_active.TryGetValue(key, out ActiveBuffVfx entry)) continue;
                try
                {
                    TickEntry(key, entry, now);
                }
                catch (Exception ex)
                {
                    // 한 기록의 실패가 다른 기록 · 다음 프레임을 막지 않게, 그 기록만 즉시 정지하고 버린다.
                    // [2026-11-11] 구역 구동기 기록이면 [SKILL-ZONE] 머리로 남긴다. 옛 줄(기록용):
                    // LogFault("Follow", null, key.CasterTeam, key.Slot, entry.Sequence, ex);
                    if (entry.Driver == VfxDriver.Zone) LogZoneFault("Follow", null, entry.Sequence, ex);
                    else LogFault("Follow", null, key.CasterTeam, key.Slot, entry.Sequence, ex);
                    _active.Remove(key);
                    try { StopEntry(entry, immediate: true, path: "Fault"); } catch { /* 정지마저 실패하면 풀이 정리한다 */ }
                }
            }
            _keyBuffer.Clear();
            FlushStopGroups();
        }

        private void TickEntry(BuffKey key, ActiveBuffVfx entry, float now)
        {
            // ① 유닛 사망 — 즉시 정지(D8). 사망 이벤트가 먼저 처리하지만, 놓친 경우의 안전망이다.
            if (entry.Unit == null || !entry.Unit.IsAlive)
            {
                _active.Remove(key);
                StopAndGroup(key, entry, immediate: true, path: "UnitDied");
                return;
            }
            // ② 효과 시간 끝 — 꼬리 정지(D8).
            if (now >= entry.EndTime)
            {
                _active.Remove(key);
                StopAndGroup(key, entry, immediate: false, path: "Expire");
                return;
            }
            // ③ 대기 중 — 몸을 찾았으면 이제 시작한다.
            if (entry.Pending)
            {
                StartOutcome outcome = TryStart(entry, out bool fitFound, out _, out string failReason);
                // [2026-11-11] 구역 구동기 기록이면 [SKILL-ZONE] 머리로 남긴다. 옛 줄(기록용):
                // if (outcome == StartOutcome.Started) LogPendingResolved(key, entry, fitFound);
                if (outcome == StartOutcome.Started)
                {
                    if (entry.Driver == VfxDriver.Zone) LogZonePendingResolved(key.UnitId, entry, fitFound);
                    else LogPendingResolved(key, entry, fitFound);
                }
                else if (outcome == StartOutcome.Failed)
                {
                    _active.Remove(key);
                    StopAndGroup(key, entry, immediate: true, path: "StartFailed" + (failReason != null ? "-" + failReason : string.Empty));
                }
                return;
            }
            // ④ 재생이 바깥에서 끝났다(항목 파괴 등 비정상) — 기록만 버린다.
            if (entry.Item == null || !entry.Item.IsStatusLoopActive(entry.Lease))
            {
                _active.Remove(key);
                StopAndGroup(key, entry, immediate: true, path: "Lost");
                return;
            }
            // ⑤ 몸이 사라졌거나 비활성(유닛 화면 오브젝트 정리 등) — 빈 자리에 이펙트가 남지 않게 즉시 정지.
            if (entry.Pose == null || !entry.Pose.gameObject.activeInHierarchy)
            {
                _active.Remove(key);
                StopAndGroup(key, entry, immediate: true, path: "PoseLost");
                return;
            }
            // ⑥ 따라가기 — 읽기 직전에 보이는 몸을 이번 프레임의 화면 위치로 바로잡는다(뒤집힌 Client 계약, 위험 R-7).
            if (entry.Projector != null) entry.Projector.ProjectNow();
            entry.Item.FollowStatusLoop(entry.Lease, entry.Pose);
        }

        // ====================================================================
        // 정지 경로
        // ====================================================================

        /// <summary>
        /// 유닛이 죽으면 그 유닛의 강화 상태 이펙트를 모두 즉시 지운다(D8 — 죽은 몸에 이펙트가 남지 않게, 위험 R-8).
        /// </summary>
        /// <param name="e">유닛 사망 이벤트(서버 · Client 모두 발행).</param>
        private void OnUnitDied(UnitDiedEvent e)
        {
            try
            {
                if (this == null || e.Unit == null || _active.Count == 0) return;
                _deathKeyBuffer.Clear();
                foreach (KeyValuePair<BuffKey, ActiveBuffVfx> pair in _active)
                    if (pair.Key.UnitId == e.Unit.Id && ReferenceEquals(pair.Value.Unit, e.Unit)) _deathKeyBuffer.Add(pair.Key);
                for (int i = 0; i < _deathKeyBuffer.Count; i++)
                {
                    BuffKey key = _deathKeyBuffer[i];
                    if (!_active.TryGetValue(key, out ActiveBuffVfx entry)) continue;
                    _active.Remove(key);
                    StopAndGroup(key, entry, immediate: true, path: "UnitDied");
                }
                _deathKeyBuffer.Clear();
                FlushStopGroups();
            }
            catch (Exception ex)
            {
                LogFault("UnitDied", null, TeamId.Neutral, -1, 0, ex);
            }
        }

        /// <summary>
        /// 경기가 끝나면 남은 이펙트를 모두 꼬리 정지하고(D8), 이번 경기에서는 새로 붙이지 않는다.
        /// Client에서도 결과 발표 수신 때 OnGameEnd가 발행된다.
        /// </summary>
        /// <param name="e">경기 종료 이벤트(내용은 쓰지 않는다).</param>
        private void OnGameEnd(GameEndEvent e)
        {
            try
            {
                if (this == null) return;
                _gameEnded = true;
                StopAll(immediate: false, path: "GameEnd");
                // [2026-11-11] 구역 기록도 비운다(구성원 이펙트는 위 StopAll이 꼬리 정지했다 — Z-8).
                _zoneRecords.Clear();
            }
            catch (Exception ex)
            {
                LogFault("GameEnd", null, TeamId.Neutral, -1, 0, ex);
            }
        }

        /// <summary>
        /// 남은 기록을 모두 멈추고 비운다.
        /// </summary>
        /// <param name="immediate">true = 입자까지 즉시 지움(재경기 · 파괴), false = 꼬리 정지(경기 종료).</param>
        /// <param name="path">정지 경로 이름(진단 로그용 — GameEnd · Reinit · Destroy).</param>
        private void StopAll(bool immediate, string path)
        {
            if (_active.Count == 0) return;
            foreach (KeyValuePair<BuffKey, ActiveBuffVfx> pair in _active)
                StopAndGroup(pair.Key, pair.Value, immediate, path);
            _active.Clear();
            FlushStopGroups();
        }

        /// <summary> 기록 하나를 멈추고 정지 줄 묶음에 센다. </summary>
        private void StopAndGroup(BuffKey key, ActiveBuffVfx entry, bool immediate, string path)
        {
            bool stopped = StopEntry(entry, immediate, path);
            AddStopGroup(key, entry, immediate, path, stopped);
        }

        /// <summary>
        /// 기록 하나의 이펙트를 멈춘다(확인 번호가 맞을 때만 실제로 멈춘다). 대기 중이던 기록은 멈출 이펙트가 없다.
        /// EffectManager는 Unity 오브젝트라 ?. 대신 == null 검사(파괴된 객체도 null로 보는 Unity 방식)를 쓴다.
        /// </summary>
        /// <returns>실제로 멈췄으면 true.</returns>
        private bool StopEntry(ActiveBuffVfx entry, bool immediate, string path)
        {
            if (entry == null || entry.Item == null) return false;
            bool stopped = _effects != null
                ? _effects.StopUnitBuffVfx(entry.Item, entry.Lease, immediate)
                : entry.Item.StopStatusLoop(entry.Lease, immediate);
            entry.Item = null; // 정지 뒤에는 이 기록이 그 항목을 다시 건드리지 않는다(풀로 돌아가 남의 재생이 될 수 있다).
            return stopped;
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary>
        /// 보이는 몸(Pose)을 바로잡아 주는 그 유닛의 VisualRootProjector를 찾는다(TimedDamageEffectPresenter와 같은 규칙).
        /// 찾은 투영기가 정말 이 Pose의 주인인지(투영기가 돌려주는 화면 Transform이 이 Pose와 같은지) 확인한 뒤에만 돌려준다.
        /// </summary>
        /// <param name="pose">pose 함수가 돌려준 Transform(보통 Visual Root).</param>
        /// <returns>이 Pose를 쓰는 투영기. 없거나 Pose가 Visual Root가 아니면 null(그때는 바로잡기 없이 읽는다).</returns>
        private static VisualRootProjector ResolveProjector(Transform pose)
        {
            if (pose == null) return null;
            VisualRootProjector projector = pose.GetComponentInParent<VisualRootProjector>(true);
            if (projector == null || projector.transform == pose) return null;
            return projector.PresentationTransform == pose ? projector : null;
        }

        /// <summary> 상대 팀(Blue↔Red). Neutral은 그대로. </summary>
        private static TeamId OppositeTeam(TeamId team)
        {
            if (team == TeamId.Blue) return TeamId.Red;
            if (team == TeamId.Red) return TeamId.Blue;
            return team;
        }

        // ====================================================================
        // 진단 로그(개발 축 — 릴리스 빌드에서는 호출 · 문자열 조립까지 통째로 사라진다, LogRules 1.7)
        //   머리 [SKILL-BUFF], Diag=SkillBuff, DiagRev=1. 시간 · 배율 · 위치는 밀리 정수.
        // ====================================================================

        /// <summary> 현재 화면의 역할(Single · Host · Client). </summary>
        private static string DiagRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Host" : "Client";
        }

        /// <summary> 월드 단위 · 초 단위 실수 → 밀리 단위 정수. </summary>
        private static int DiagMilli(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        // 정지 줄 묶음에 1건 더한다(같은 스킬 · 같은 경로면 계수만 올린다). 진단 전용이라 [Conditional] — 릴리스에서는 호출이 사라진다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void AddStopGroup(BuffKey key, ActiveBuffVfx entry, bool immediate, string path, bool stopped)
        {
            int activeMs = DiagMilli(Time.time - entry.StartTime);
            for (int i = 0; i < _stopGroups.Count; i++)
            {
                StopGroup g = _stopGroups[i];
                // [2026-11-11] 구동기도 같아야 같은 묶음이다.
                if (g.CasterTeam == key.CasterTeam && g.Slot == key.Slot && g.Path == path && g.Immediate == immediate
                    && g.Driver == entry.Driver)
                {
                    g.Count++;
                    if (stopped) g.LeaseMatched++;
                    if (activeMs > g.MaxActiveMs) g.MaxActiveMs = activeMs;
                    if (entry.Sequence > g.Sequence) g.Sequence = entry.Sequence;
                    _stopGroups[i] = g;
                    return;
                }
            }
            _stopGroups.Add(new StopGroup
            {
                CasterTeam = key.CasterTeam, Slot = key.Slot, Sequence = entry.Sequence, Path = path, Immediate = immediate,
                Count = 1, LeaseMatched = stopped ? 1 : 0, MaxActiveMs = activeMs, Driver = entry.Driver
            });
        }

        // 모아 둔 정지 줄을 남기고 비운다. [진단 L13]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void FlushStopGroups()
        {
            for (int i = 0; i < _stopGroups.Count; i++)
            {
                StopGroup g = _stopGroups[i];
                // [2026-11-11] 구역 구동기 정지는 [SKILL-ZONE] UnitZoneVfxStopped로 남긴다(ZoneDriver.cs — L16).
                if (g.Driver == VfxDriver.Zone) { LogZoneStopGroup(g); continue; }
                GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-BUFF] BuffVfxStopped",
                    FormattableString.Invariant(
                        $"Diag=SkillBuff, DiagRev=1, Event=BuffVfxStopped, Role={DiagRole()}, Team={g.CasterTeam}, Slot={g.Slot}, Sequence={g.Sequence}, Path={g.Path}, Immediate={g.Immediate}, Count={g.Count}, LeaseMatched={g.LeaseMatched}, MaxActiveMs={g.MaxActiveMs}"));
            }
            _stopGroups.Clear();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStarted(SkillActivationPresentation p, string channel, StatusEffectKind kind, TeamId targetTeam,
            string prefabName, SkillData skill, int targets, int attached, int extended, int pending, int fitMissing,
            int failed, string firstFail, int poolCreated)
        {
            GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-BUFF] BuffVfxStarted",
                FormattableString.Invariant(
                    $"Diag=SkillBuff, DiagRev=1, Event=BuffVfxStarted, Role={DiagRole()}, Channel={channel}, Sequence={p.Sequence}, Team={p.Team}, Slot={p.Slot}, BuildingId={p.BuildingId}, Kind={kind}, TargetTeam={targetTeam}, Prefab={prefabName}, DurationMs={DiagMilli(skill.Duration)}, ScaleMilli={DiagMilli(skill.StatusVfxScale)}, Targets={targets}, Attached={attached}, Extended={extended}, PosePending={pending}, FitMissing={fitMissing}, StartFailed={failed}, FailReason={(firstFail ?? "None")}, PoolCreated={poolCreated}, ViewFlipped={ViewConverter.IsFlipped}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogExtended(SkillActivationPresentation p, string channel, int extended, float duration)
        {
            GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-BUFF] BuffVfxExtended",
                FormattableString.Invariant(
                    $"Diag=SkillBuff, DiagRev=1, Event=BuffVfxExtended, Role={DiagRole()}, Channel={channel}, Sequence={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Count={extended}, DurationMs={DiagMilli(duration)}, Restarted=False"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSkipped(SkillActivationPresentation p, string channel, StatusEffectKind kind, string reason,
            string prefabName, bool isWarning)
        {
            string data = FormattableString.Invariant(
                $"Diag=SkillBuff, DiagRev=1, Event=BuffVfxSkipped, Role={DiagRole()}, Channel={channel}, Sequence={p.Sequence}, Team={p.Team}, Slot={p.Slot}, Kind={kind}, Reason={reason}, Prefab={(prefabName ?? "None")}");
            if (isWarning)
                GameLog.Dev.Warn("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-BUFF] BuffVfxSkipped", data);
            else
                GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-BUFF] BuffVfxSkipped", data);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPendingResolved(BuffKey key, ActiveBuffVfx entry, bool fitFound)
        {
            GameLog.Dev.Info("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-BUFF] BuffVfxPendingResolved",
                FormattableString.Invariant(
                    $"Diag=SkillBuff, DiagRev=1, Event=BuffVfxPendingResolved, Role={DiagRole()}, Team={key.CasterTeam}, Slot={key.Slot}, Sequence={entry.Sequence}, UnitId={key.UnitId}, WaitedMs={DiagMilli(Time.time - entry.StartTime)}, FitFound={fitFound}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFault(string stage, string channel, TeamId team, int slot, int sequence, Exception ex)
        {
            GameLog.Dev.Warn("Skill", nameof(UnitBuffVfxPresenter), "[SKILL-BUFF] BuffVfxFault",
                FormattableString.Invariant(
                    $"Diag=SkillBuff, DiagRev=1, Event=BuffVfxFault, Role={DiagRole()}, Stage={stage}, Channel={(channel ?? "None")}, Team={team}, Slot={slot}, Sequence={sequence}, Error={ex.GetType().Name}"));
        }
    }
}
