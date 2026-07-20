// ============================================================================
// SpecialAttackContext.cs
// 특수 공격 핸들러(ISpecialAttackBehavior)가 동작에 필요한 것을 담아 전달하는 컨텍스트.
//
// 담는 것:
//   - 공격자(UnitData)
//   - 주 타깃(IDamageable) — 이미 단일 피해가 적용된 대상. 중복 타격 방지에 사용.
//   - 전체 유닛 조회 수단(읽기 전용 Dictionary) — 판정 범위 안의 적을 순회 수집.
//   - 재사용 피해 헬퍼(델리게이트) — UnitCombatUseCase가 단일 타깃에 쓰는 것과 "같은" 절차.
//     (피해 적용 + 이벤트 발행 + 사망 처리 + 싱글플레이 전투 상태 정리)
//   - 월드 좌표 조회 델리게이트 — 유닛/건물의 실제 월드 좌표(Vector3)를 반환.
//     휩쓸기(SweepAttackBehavior)의 "월드 좌표 전방 부채꼴" 판정에 사용한다.
//   - 휩쓸기 튜닝값(reach/arcHalfAngle) — SpecialAttackConfig에서 읽어 주입된 float.
//
// 왜 피해 적용/좌표 조회를 델리게이트로 주입하는가:
//   핸들러가 UnitCombatUseCase의 private 로직(포지션 프로바이더, 좌표 매퍼 등)에
//   직접 접근하지 않도록 하여 결합을 낮춘다. 주 타깃 경로와 AoE 경로가 "동일 헬퍼"를
//   공유하므로 멀티플레이 HP 동기화(OnEntityDamaged 발행 형식)가 일관된다.
//
// Application 레이어 — Domain 의존 + UnityEngine.Vector3만 사용(Unity 의존 허용).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 특수 공격 핸들러 실행에 필요한 데이터·피해 적용·좌표 조회 수단을 담는 컨텍스트.
    /// 이번(도끼병)엔 피해만 사용하지만, 향후 힐/DoT용 확장 여지를 둔다.
    /// </summary>
    public sealed class SpecialAttackContext
    {
        /// <summary> 특수 공격을 발동한 공격자. Facing은 주 타깃 방향으로 갱신된 상태. </summary>
        public UnitData Attacker { get; }

        /// <summary>
        /// 주 타깃(기존 단일 경로로 이미 1회 피해를 받은 대상).
        /// 유닛일 수도 건물일 수도 있다. 핸들러는 이 대상을 중복 타격하지 않아야 한다(D-3).
        /// 휩쓸기에서는 이 대상의 월드 좌표로 forward(기준 방향)를 구한다.
        /// </summary>
        public IDamageable PrimaryTarget { get; }

        /// <summary> 현재 살아있는 모든 유닛(읽기 전용). 판정 범위 안의 적 수집에 사용. </summary>
        public IReadOnlyDictionary<int, UnitData> Units { get; }

        /// <summary>
        /// 휩쓸기 전방 부채꼴 판정의 월드 반경(공격자로부터의 XZ 평면 거리 한계).
        /// SpecialAttackConfig.SweepReach에서 주입(미주입 시 코드 기본값 1.0).
        /// </summary>
        public float SweepReach { get; }

        /// <summary>
        /// 휩쓸기 전방 부채꼴의 반각(도 단위). forward와 적이 이루는 각도가 이 값 이하이면 피격.
        /// SpecialAttackConfig.SweepArcHalfAngle에서 주입(미주입 시 코드 기본값 120).
        /// </summary>
        public float SweepArcHalfAngle { get; }

        /// <summary> TorrentSpirit 파도 가로 폭(월드). SpecialAttackConfig.WaveWidth에서 주입. </summary>
        public float WaveWidth { get; }

        /// <summary> TorrentSpirit 파도 전방 길이(월드). SpecialAttackConfig.WaveLength에서 주입. </summary>
        public float WaveLength { get; }

        /// <summary> TorrentSpirit 파도가 전방 길이를 전부 지나가는 시간(초). SpecialAttackConfig.WaveTravelTime에서 주입. </summary>
        public float WaveTravelTime { get; }

        /// <summary> TorrentSpirit 파도가 아군에 닿을 때의 회복량. SpecialAttackConfig.WaveHeal에서 주입. </summary>
        public float WaveHeal { get; }

        /// <summary>
        /// MushroomBomber 착탄 폭발의 월드 반경. 착탄 중심(주 타깃 위치)에서 XZ 평면 거리가
        /// 이 값 이하인 적 유닛에게 DoT를 부여한다. SpecialAttackConfig.BlastRadius에서 주입.
        /// (DoT의 초당 피해/지속/틱 간격은 UseCase 내부에 있고, 핸들러는 "누구에게" 걸지만 결정한다.)
        /// </summary>
        public float BlastRadius { get; }

        // 단일 대상 1회 피해 절차(피해+이벤트+사망 처리)를 수행하는 재사용 헬퍼.
        // UnitCombatUseCase.ApplyDamageToVictim을 그대로 넘겨받는다.
        private readonly Action<UnitData, IDamageable> _applyDamage;

        // 유닛/건물의 실제 월드 좌표를 반환하는 델리게이트.
        // UnitCombatUseCase.ResolveWorldPosition을 그대로 넘겨받는다.
        // (내부적으로 IEntityPositionProvider 우선, 미등록 시 HexToWorld 폴백)
        private readonly Func<IDamageable, Vector3> _worldPositionOf;

        // 파도(이동 전선)를 서버 시뮬레이터에 등록하는 델리게이트.
        // UnitCombatUseCase.SpawnWave를 그대로 넘겨받는다. 핸들러는 모양/방향만 계산해 요청을 넘기고,
        // 전선 전진·닿은 유닛 판정·피해/힐 적용은 UseCase 내부 시뮬레이터가 담당한다(서버 권위).
        private readonly Action<WaveSpawnRequest> _spawnWave;

        // 대상 유닛에 착탄 DoT(지속 피해)를 부여하는 델리게이트. 인자는 (공격자, 피해 대상).
        // UnitCombatUseCase.ApplyBlastDot을 그대로 넘겨받는다. DoT의 초당 피해/지속/틱 간격은
        // UseCase 내부에 캡슐화돼 있고, 핸들러는 반경 판정으로 "누구에게" 걸지만 결정한다
        // (파도의 SpawnWave와 같은 역할 분담 — 서버 권위 효과는 UseCase 소유).
        private readonly Action<UnitData, UnitData> _applyDot;

        // 대상 유닛에 InfernoSpirit 단일 대상 DoT를 부여하는 델리게이트. 인자는 (공격자, 피해 대상).
        // UnitCombatUseCase.ApplyInfernoDot을 그대로 넘겨받는다.
        // ⚠️ MushroomBomber의 _applyDot(→ApplyBlastDot, 초당 2/3초)와는 "별도" 진입점이다.
        //    InfernoSpirit은 자기 튜닝값(초당 5/3초)을 써야 하므로 델리게이트를 분리해
        //    MushroomBomber 값이 섞이지 않도록 한다(값 회귀 방지).
        private readonly Action<UnitData, UnitData> _applyInfernoDot;

        /// <summary>
        /// 컨텍스트 생성.
        /// </summary>
        /// <param name="attacker">공격자 유닛.</param>
        /// <param name="primaryTarget">주 타깃(이미 단일 피해 적용됨).</param>
        /// <param name="units">전체 유닛 읽기 전용 목록.</param>
        /// <param name="applyDamage">단일 대상 피해 헬퍼(피해+이벤트+사망 처리).</param>
        /// <param name="worldPositionOf">엔티티(유닛/건물)의 월드 좌표를 반환하는 델리게이트.</param>
        /// <param name="spawnWave">파도(이동 전선) 생성 요청 델리게이트.</param>
        /// <param name="sweepReach">휩쓸기 부채꼴 판정 반경(월드).</param>
        /// <param name="sweepArcHalfAngle">휩쓸기 부채꼴 반각(도).</param>
        /// <param name="waveWidth">파도 가로 폭(월드).</param>
        /// <param name="waveLength">파도 전방 길이(월드).</param>
        /// <param name="waveTravelTime">파도 전선이 전방 길이를 지나가는 시간(초).</param>
        /// <param name="waveHeal">파도 아군 회복량.</param>
        /// <param name="applyDot">대상 유닛에 착탄 DoT를 부여하는 델리게이트(공격자, 대상).</param>
        /// <param name="blastRadius">착탄 폭발 판정의 월드 반경(중심으로부터 XZ 거리 한계).</param>
        /// <param name="applyInfernoDot">대상 유닛에 InfernoSpirit 단일 대상 DoT를 부여하는 델리게이트(공격자, 대상). MushroomBomber와 값이 다른 별도 진입점.</param>
        public SpecialAttackContext(
            UnitData attacker,
            IDamageable primaryTarget,
            IReadOnlyDictionary<int, UnitData> units,
            Action<UnitData, IDamageable> applyDamage,
            Func<IDamageable, Vector3> worldPositionOf,
            Action<WaveSpawnRequest> spawnWave,
            float sweepReach,
            float sweepArcHalfAngle,
            float waveWidth,
            float waveLength,
            float waveTravelTime,
            float waveHeal,
            Action<UnitData, UnitData> applyDot,
            float blastRadius,
            Action<UnitData, UnitData> applyInfernoDot)
        {
            Attacker = attacker;
            PrimaryTarget = primaryTarget;
            Units = units;
            _applyDamage = applyDamage;
            _worldPositionOf = worldPositionOf;
            _spawnWave = spawnWave;
            SweepReach = sweepReach;
            SweepArcHalfAngle = sweepArcHalfAngle;
            WaveWidth = waveWidth;
            WaveLength = waveLength;
            WaveTravelTime = waveTravelTime;
            WaveHeal = waveHeal;
            _applyDot = applyDot;
            BlastRadius = blastRadius;
            _applyInfernoDot = applyInfernoDot;
        }

        /// <summary>
        /// 지정한 피해 대상에게 "단일 타깃과 동일한" 피해 절차를 적용한다.
        /// (내부적으로 UnitCombatUseCase의 재사용 헬퍼를 호출)
        /// </summary>
        /// <param name="attacker">공격자(보통 <see cref="Attacker"/>와 동일).</param>
        /// <param name="victim">피해를 받을 대상.</param>
        public void ApplyDamage(UnitData attacker, IDamageable victim)
        {
            _applyDamage?.Invoke(attacker, victim);
        }

        /// <summary>
        /// 엔티티(유닛/건물)의 현재 월드 좌표를 반환한다.
        /// GameObject가 없으면 도메인 좌표(HexToWorld) 폴백을 거쳐 값을 돌려준다.
        /// 조회 수단이 주입되지 않았으면 Vector3.zero.
        /// </summary>
        /// <param name="entity">월드 좌표를 구할 엔티티.</param>
        public Vector3 WorldPositionOf(IDamageable entity)
        {
            return _worldPositionOf != null ? _worldPositionOf(entity) : Vector3.zero;
        }

        /// <summary>
        /// 파도(이동 전선)를 서버 시뮬레이터에 생성 요청한다.
        /// 실제 전선 전진·닿은 유닛 판정·피해/힐 적용은 UnitCombatUseCase가 매 서버 틱마다 처리한다.
        /// </summary>
        /// <param name="request">파도 모양/방향/파라미터.</param>
        public void SpawnWave(WaveSpawnRequest request)
        {
            _spawnWave?.Invoke(request);
        }

        /// <summary>
        /// 대상 유닛에 착탄 DoT(지속 피해)를 부여한다. 실제 초당 피해/지속/틱 간격은
        /// UnitCombatUseCase 내부에 캡슐화돼 있으며, 이 델리게이트는 "누구에게 걸지"만 전달한다.
        /// </summary>
        /// <param name="attacker">DoT를 건 공격자(보통 <see cref="Attacker"/>와 동일).</param>
        /// <param name="victim">DoT를 받을 적 유닛.</param>
        public void ApplyDot(UnitData attacker, UnitData victim)
        {
            _applyDot?.Invoke(attacker, victim);
        }

        /// <summary>
        /// 대상 유닛에 InfernoSpirit 단일 대상 DoT(지속 피해)를 부여한다.
        /// MushroomBomber의 <see cref="ApplyDot"/>(초당 2/3초)와 값이 다른 "별도" 진입점으로,
        /// InfernoSpirit 자기 튜닝값(초당 5/3초)을 사용한다. 초당 피해/지속/틱 간격은
        /// UnitCombatUseCase 내부에 캡슐화돼 있고, 이 델리게이트는 "누구에게 걸지"만 전달한다.
        /// </summary>
        /// <param name="attacker">DoT를 건 공격자(보통 <see cref="Attacker"/>와 동일).</param>
        /// <param name="victim">DoT를 받을 적 유닛.</param>
        public void ApplyInfernoDot(UnitData attacker, UnitData victim)
        {
            _applyInfernoDot?.Invoke(attacker, victim);
        }
    }
}
