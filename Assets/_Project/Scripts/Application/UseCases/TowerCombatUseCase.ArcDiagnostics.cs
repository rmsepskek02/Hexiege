// ============================================================================
// TowerCombatUseCase.ArcDiagnostics.cs
// [2026-11-09 Task `_Tasks/2026-11-09/22_19_cannon-tower-front-arc/` 커밋 2] 인간 대포탑 전방 120° 진단 로그 [TOWER-ARC].
//
// 왜 필요한가(유니티 초급자용 설명):
//   「대포탑이 정말 뒤로는 안 쏘는지」는 순간적으로 일어나 눈으로만 판단하기 어렵다.
//   WORKFLOW [4] 「테스트 항목 진단 로그 규칙」(사용자 결정 2026-11-09)에 따라, Plan 5절 대응표의 테스트 항목을
//   로그만 보고 판정할 수 있게 서버 판정 자리에서 줄을 남긴다. 존속 = `개발`(사용자 결정 Q11, EMP [EMP-DIAG] 선례).
//
// 남기는 줄(모두 GameLog.Dev.Info, 카테고리 Combat, 머리 [TOWER-ARC], Diag=TowerFrontArc, DiagRev=1):
//   · Fire      — 인간 대포탑이 실제로 발사할 때마다(쿨다운 5초라 탑당 최대 0.2줄/초). 경기당 앞 FireDetailLimit(40)줄만
//                 남기고, 그 뒤는 End 의 DetailOmitted 수에만 더한다. 다른 종족 탑의 공격은 줄 없이 End 합계에만 센다.
//   · HoldBegin — 쿨다운 0 · 사거리 안 적 ≥ 1 · 그중 전방 범위 안 0 이 "처음" 성립한 순간(탑마다 대기 1회당 1줄).
//   · HoldEnd   — 그 대기가 풀린 순간. Reason = Fire(앞에 들어와 발사) / NoEnemyInRange(사거리 안 적이 없어짐)
//                 / TowerGone(탑 파괴 · 철거) / FireRefused(앞 표적은 찾았으나 발사가 거절됨) / GameEnd(경기 종료 때 열려 있던 대기).
//   · End       — 경기 종료 때 1회. 종족별 합계와 Verdict(PASS = 인간 탑의 범위 밖 발사 0).
//   매 Tick 줄은 남기지 않는다(LogRules 1.14-8). 대기 중 Tick 수는 세기만 하고 HoldEnd · End 에 싣는다.
//
// 서버 전용이다:
//   판정(Tick)은 싱글 · 멀티 서버(Host)에서만 돈다. 그래서 Client 로그에 [TOWER-ARC] 는 0줄이 정상이다.
//   End 는 경기 종료 이벤트에서 불리는데 Client 에서도 그 이벤트가 오므로, Client 에서는 End 도 남기지 않는다.
//
// 릴리스 영향 0:
//   이 파일의 기록 메서드는 모두 [Conditional("UNITY_EDITOR")] + [Conditional("DEVELOPMENT_BUILD")] 다(LogRules 1.7).
//   릴리스 빌드에서는 호출과 인자 계산이 통째로 사라진다. 진단 상태는 판정 상태(BuildingData 쿨다운 등)와 따로 두고,
//   여기서 판정 값은 읽기만 한다 — 진단이 표적 선택 · 쿨다운 · 피해를 바꾸지 않는다.
//
// Application 레이어 — Domain · Application 만 쓴다(NetworkContext 정적 홀더로 역할을 읽는다).
// ============================================================================

using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class TowerCombatUseCase
    {
        /// <summary> [진단] 경기당 Fire 상세 줄 상한. 넘은 발사는 End 의 DetailOmitted 에만 센다(Plan 3-4 「앞 N줄」). </summary>
        private const int FireDetailLimit = 40;

        /// <summary> [진단] 탑 하나의 대기 상태(인간 대포탑만 만든다). </summary>
        private sealed class ArcHoldDiag
        {
            public TeamId Team;           // 탑 팀(TowerGone 줄에 쓴다 — 그때는 건물 기록이 이미 없을 수 있다)
            public bool Holding;          // 지금 "앞에 쏠 적이 없어 대기 중"인가
            public bool PendingFireEnd;   // 앞 표적을 찾아 대기가 풀리기 직전(이번 Tick 발사를 기다림)
            public float HoldStartedAt;   // 대기 시작 진단 시각(초)
            public int HeldTicks;         // 대기 중 탐색 Tick 수(시작 Tick 포함)
        }

        /// <summary> [진단] 종족 하나의 경기 합계. </summary>
        private sealed class ArcRaceDiag
        {
            public int Fires;             // 실제 공격 수
            public int InArcFires;        // 그중 공격 순간 대상이 정면 범위 안이던 수
            public int OutsideArcFires;   // 그중 범위 밖이던 수(인간은 언제나 0이어야 정상 — Verdict 기준)
            public int HoldEpisodes;      // 대기 시작 횟수(인간만)
            public int HeldTicks;         // 대기 중 Tick 합(인간만)
            public int ExcludedCloser;    // 발사 때 "고른 대상보다 가깝지만 범위 밖이라 뺀 적" 수의 합(인간만 — T4)
            public int ExcludedScans;     // 탐색마다 범위 밖이라 뺀 후보 수의 합(Tick 가중 — 인간만)
            public float MaxFireAngleDeg; // 공격 순간 정면과 대상 사이 최대 각도(T5 — 인간 ≤ 60.07)
        }

        private readonly Dictionary<int, ArcHoldDiag> _arcHold = new Dictionary<int, ArcHoldDiag>();
        private readonly ArcRaceDiag[] _arcRace = { new ArcRaceDiag(), new ArcRaceDiag(), new ArcRaceDiag() };
        private readonly List<int> _arcGoneBuffer = new List<int>(4);
        // 이번 탐색(FindNearestEnemyUnit 1회)에서 범위 밖이라 뺀 후보들의 거리 — 발사 때 ExcludedCloser 계산용(재사용 버퍼).
        private readonly List<float> _arcScanExcludedDist = new List<float>(8);
        private int _arcScanInRange;          // 이번 탐색의 사거리 안 후보 수(인간 대포탑만 센다)
        private int _arcScanInArc;            // 그중 범위 안 수
        private float _arcScanNearestExcludedDist; // 범위 밖 후보 중 가장 가까운 거리(없으면 MaxValue)
        private float _arcScanNearestExcludedDot;  // 그 후보의 정면 내적
        private float _arcDiagClock;          // 진단 시계(초) — Tick 의 dt 합(HeldMs 계산)
        private int _arcFireLines;            // 남긴 Fire 상세 줄 수
        private int _arcDetailOmitted;        // 상한을 넘어 줄 없이 센 Fire 수
        private bool _arcEndWritten;          // End 를 이미 남겼는지(싱글 · 멀티 서버 경기 종료 중복 방지 — 선례 EndWritten)

        /// <summary>
        /// [진단] 경기 종료 때 [TOWER-ARC] End 1줄을 남긴다(경기당 1회 — 두 번째 호출부터는 아무것도 하지 않는다).
        /// 조합 루트(GameBootstrapper)가 GameEvents.OnGameEnd 에서 부른다(싱글 · 멀티 서버 공통 구독).
        /// Client 에서는 남기지 않는다(판정을 하지 않았으므로 0줄이 정상). 개발 빌드 · 에디터 전용 — 릴리스에서는 호출째 사라진다.
        /// </summary>
        /// <param name="reason">종료 사유 토큰(예: GameEnd). 로그 Reason= 값.</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void DiagArcWriteEnd(string reason)
        {
            if (_arcEndWritten) return;
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return; // 순수 Client: 판정 없음 → 0줄
            _arcEndWritten = true;

            // 경기 종료 순간에도 대기 중이던 탑은 HoldEnd(Reason=경기 종료 사유)로 닫는다 — 대기 수와 끝 줄 수가 맞게.
            int openHolds = 0;
            foreach (KeyValuePair<int, ArcHoldDiag> pair in _arcHold)
            {
                if (!pair.Value.Holding) continue;
                openHolds++;
                DiagArcLogHoldEnd(pair.Key, pair.Value, reason);
            }

            ArcRaceDiag human = _arcRace[RaceSlot(RaceId.Human)];
            string verdict = human.OutsideArcFires == 0 ? "PASS" : "FAIL";
            GameLog.Dev.Info("Combat", nameof(TowerCombatUseCase), "[TOWER-ARC] End",
                System.FormattableString.Invariant(
                    $"Diag=TowerFrontArc, DiagRev=1, Event=End, Role={ServerProjectileDiagnostics.RoleName}, Reason={reason}, HalfAngleDeg={TowerFrontArc.HalfAngleDeg:F0}, LimitDeg={TowerFrontArc.DotToAngleDeg(TowerFrontArc.MinFrontDot):F2}, FireLines={_arcFireLines}, DetailOmitted={_arcDetailOmitted}, OpenHoldsAtEnd={openHolds}, Human={DiagArcRaceText(human)}, Spirit={DiagArcRaceText(_arcRace[RaceSlot(RaceId.Spirit)])}, Transcendence={DiagArcRaceText(_arcRace[RaceSlot(RaceId.Transcendence)])}, Verdict={verdict}"));
        }

        /// <summary> [진단] Tick 시작 — 진단 시계 진행 + 대기 중이던 탑이 사라졌으면(파괴 · 철거) HoldEnd(TowerGone). </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagArcBeginTick(float dt)
        {
            if (dt > 0f) _arcDiagClock += dt;
            if (_arcEndWritten || _arcHold.Count == 0) return;
            _arcGoneBuffer.Clear();
            foreach (KeyValuePair<int, ArcHoldDiag> pair in _arcHold)
            {
                BuildingData tower = _buildingPlacement.GetBuilding(pair.Key);
                if (tower == null || !tower.IsAlive) _arcGoneBuffer.Add(pair.Key);
            }
            for (int i = 0; i < _arcGoneBuffer.Count; i++)
            {
                int towerId = _arcGoneBuffer[i];
                ArcHoldDiag hold = _arcHold[towerId];
                if (hold.Holding) DiagArcLogHoldEnd(towerId, hold, "TowerGone");
                _arcHold.Remove(towerId);
            }
        }

        /// <summary> [진단] 표적 탐색 1회 시작 — 이번 탐색의 후보 집계를 비운다. </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagArcBeginScan()
        {
            _arcScanInRange = 0;
            _arcScanInArc = 0;
            _arcScanNearestExcludedDist = float.MaxValue;
            _arcScanNearestExcludedDot = 1f;
            _arcScanExcludedDist.Clear();
        }

        /// <summary> [진단] 인간 대포탑의 사거리 안 후보 1명(범위 안/밖 · 거리 · 정면 내적)을 센다. 줄은 남기지 않는다. </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagArcNoteCandidate(bool inArc, float dist, float frontDot)
        {
            _arcScanInRange++;
            if (inArc) { _arcScanInArc++; return; }
            _arcScanExcludedDist.Add(dist);
            if (dist < _arcScanNearestExcludedDist)
            {
                _arcScanNearestExcludedDist = dist;
                _arcScanNearestExcludedDot = frontDot;
            }
        }

        /// <summary>
        /// [진단] 탐색 직후 — 인간 대포탑의 대기 시작 · 계속 · 끝을 판정한다(HoldBegin · HoldEnd 줄은 상태가 바뀔 때만).
        /// 이 자리는 쿨다운 0 · EMP 관문 통과 뒤라, 여기서 본 "대기"는 곧 "쏠 수 있는데 앞에 적이 없다"는 뜻이다.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagArcAfterScan(BuildingData tower, UnitData target)
        {
            if (_arcEndWritten || tower == null) return;
            if (!TowerFrontArc.AppliesTo(tower.Type, ResolveRace(tower.Team))) return;

            ArcRaceDiag human = _arcRace[RaceSlot(RaceId.Human)];
            int excluded = _arcScanInRange - _arcScanInArc;
            human.ExcludedScans += excluded;

            _arcHold.TryGetValue(tower.Id, out ArcHoldDiag hold);
            // 지난 Tick 에 앞 표적을 찾았는데 발사가 안 됐다면(투사체 용량 · 서비스 닫힘) 그 대기는 FireRefused 로 닫는다.
            if (hold != null && hold.Holding && hold.PendingFireEnd)
            {
                DiagArcLogHoldEnd(tower.Id, hold, "FireRefused");
                hold.Holding = false;
                hold.PendingFireEnd = false;
            }

            bool holdingNow = target == null && excluded > 0;
            if (holdingNow)
            {
                if (hold == null) { hold = new ArcHoldDiag { Team = tower.Team }; _arcHold[tower.Id] = hold; }
                if (hold.Holding) { hold.HeldTicks++; return; }
                hold.Holding = true;
                hold.PendingFireEnd = false;
                hold.HoldStartedAt = _arcDiagClock;
                hold.HeldTicks = 1;
                human.HoldEpisodes++;
                GameLog.Dev.Info("Combat", nameof(TowerCombatUseCase), "[TOWER-ARC] HoldBegin",
                    System.FormattableString.Invariant(
                        $"Diag=TowerFrontArc, DiagRev=1, Event=HoldBegin, Role={ServerProjectileDiagnostics.RoleName}, Team={tower.Team}, Race=Human, TowerId={tower.Id}, Cooldown={tower.AttackCooldownRemaining:F2}, RangeEnemies={_arcScanInRange}, ArcEnemies={_arcScanInArc}, NearestDist={_arcScanNearestExcludedDist:F3}, NearestAngleDeg={TowerFrontArc.DotToAngleDeg(_arcScanNearestExcludedDot):F2}, LimitDeg={TowerFrontArc.DotToAngleDeg(TowerFrontArc.MinFrontDot):F2}"));
                return;
            }

            if (hold == null || !hold.Holding) return;
            if (target != null)
            {
                // 앞에 표적이 생겼다 — 이번 Tick 의 발사(DiagArcNoteFire)에서 Reason=Fire 로 닫는다.
                hold.HeldTicks++;
                hold.PendingFireEnd = true;
                return;
            }
            // 사거리 안 적이 모두 사라졌다(죽음 · 이탈).
            DiagArcLogHoldEnd(tower.Id, hold, "NoEnemyInRange");
            hold.Holding = false;
        }

        /// <summary>
        /// [진단] 탑의 실제 공격 1회(즉발 피해 또는 포탄 발사 성공 뒤). 종족 합계를 올리고, 인간 대포탑이면 Fire 줄을 남긴다
        /// (경기당 앞 FireDetailLimit 줄). 대기 중이던 탑이면 그 직전에 HoldEnd(Reason=Fire)를 남긴다.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagArcNoteFire(BuildingData tower, UnitData target)
        {
            if (_arcEndWritten || tower == null || target == null) return;
            RaceId race = ResolveRace(tower.Team);
            ArcRaceDiag stats = _arcRace[RaceSlot(race)];

            // 공격 순간 기하를 판정과 같은 규칙(위치 공급자 → 헥스 폴백, 공급자 값만 도메인으로 되돌림)으로 다시 잰다.
            DiagArcFireGeometry(tower, target, out float dist, out float frontDot);
            bool inArc = frontDot >= TowerFrontArc.MinFrontDot;
            float angle = TowerFrontArc.DotToAngleDeg(frontDot);
            stats.Fires++;
            if (inArc) stats.InArcFires++; else stats.OutsideArcFires++;
            if (angle > stats.MaxFireAngleDeg) stats.MaxFireAngleDeg = angle;

            if (!TowerFrontArc.AppliesTo(tower.Type, race)) return; // RuneSpire · VineTower: 합계만(360° 증거 — T7)

            // 고른 대상보다 가깝지만 범위 밖이라 뺀 적(T4 — 앞 · 뒤 동시에 있을 때 앞을 골랐다는 증거).
            int excludedCloser = 0;
            for (int i = 0; i < _arcScanExcludedDist.Count; i++)
                if (_arcScanExcludedDist[i] < dist) excludedCloser++;
            stats.ExcludedCloser += excludedCloser;

            if (_arcHold.TryGetValue(tower.Id, out ArcHoldDiag hold) && hold.Holding)
            {
                DiagArcLogHoldEnd(tower.Id, hold, "Fire");
                hold.Holding = false;
                hold.PendingFireEnd = false;
            }

            if (_arcFireLines >= FireDetailLimit) { _arcDetailOmitted++; return; }
            _arcFireLines++;
            GameLog.Dev.Info("Combat", nameof(TowerCombatUseCase), "[TOWER-ARC] Fire",
                System.FormattableString.Invariant(
                    $"Diag=TowerFrontArc, DiagRev=1, Event=Fire, Role={ServerProjectileDiagnostics.RoleName}, Team={tower.Team}, Race=Human, TowerId={tower.Id}, TargetId={target.Id}, Dist={dist:F3}, AngleDeg={angle:F2}, InArc={(inArc ? "true" : "false")}, RangeEnemies={_arcScanInRange}, ArcEnemies={_arcScanInArc}, ExcludedCloser={excludedCloser}, FireIndex={stats.Fires}"));
        }

        /// <summary> [진단] HoldEnd 1줄(대기 Tick 수 · 대기 시간 ms). 상태 갱신은 호출자가 한다. </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DiagArcLogHoldEnd(int towerId, ArcHoldDiag hold, string reason)
        {
            _arcRace[RaceSlot(RaceId.Human)].HeldTicks += hold.HeldTicks;
            GameLog.Dev.Info("Combat", nameof(TowerCombatUseCase), "[TOWER-ARC] HoldEnd",
                System.FormattableString.Invariant(
                    $"Diag=TowerFrontArc, DiagRev=1, Event=HoldEnd, Role={ServerProjectileDiagnostics.RoleName}, Team={hold.Team}, Race=Human, TowerId={towerId}, Reason={reason}, HeldTicks={hold.HeldTicks}, HeldMs={Mathf.RoundToInt((_arcDiagClock - hold.HoldStartedAt) * 1000f)}"));
        }

        /// <summary>
        /// [진단] 공격 순간 탑 → 대상의 3D 거리와 정면 내적. 판정(FindNearestEnemyUnit)과 같은 위치 규칙을 쓴다:
        /// 위치 공급자 값(화면 좌표)이 있으면 그것, 없으면 헥스 좌표 폴백. 각도용으로는 공급자 값만 도메인으로 되돌린다.
        /// (진단 전용 복제 — 판정 코드를 진단 때문에 바꾸지 않으려고 따로 둔다.)
        /// </summary>
        private void DiagArcFireGeometry(BuildingData tower, UnitData target, out float dist, out float frontDot)
        {
            Vector3 towerPos = _positionProvider != null ? _positionProvider.GetBuildingWorldPosition(tower.Id) : Vector3.zero;
            bool towerFromProvider = towerPos != Vector3.zero;
            if (!towerFromProvider) towerPos = _mapper.HexToWorld(tower.Position);
            Vector3 unitPos = _positionProvider != null ? _positionProvider.GetUnitWorldPosition(target.Id) : Vector3.zero;
            bool unitFromProvider = unitPos != Vector3.zero;
            if (!unitFromProvider) unitPos = _mapper.HexToWorld(target.Position);

            dist = Vector3.Distance(towerPos, unitPos);
            Vector3 towerDomain = towerFromProvider ? _mapper.NormalizeToDomainPosition(towerPos) : towerPos;
            Vector3 unitDomain = unitFromProvider ? _mapper.NormalizeToDomainPosition(unitPos) : unitPos;
            TowerFrontArc.IsInFrontArc(towerDomain, unitDomain, tower.Team, out frontDot);
        }

        /// <summary> [진단] 종족 합계 1칸 문자열(값 안에 ", " 를 넣지 않는다 — LogRules 1.4). </summary>
        private static string DiagArcRaceText(ArcRaceDiag r)
        {
            return System.FormattableString.Invariant(
                $"fires:{r.Fires}|inArcFires:{r.InArcFires}|outsideArcFires:{r.OutsideArcFires}|holdEpisodes:{r.HoldEpisodes}|heldTicks:{r.HeldTicks}|excludedCloser:{r.ExcludedCloser}|excludedScans:{r.ExcludedScans}|maxFireAngleDeg:{r.MaxFireAngleDeg:F2}");
        }

        /// <summary> [진단] 종족 → 합계 배열 칸(Human 0 · Spirit 1 · Transcendence 2, 그 밖은 Human 칸). </summary>
        private static int RaceSlot(RaceId race)
        {
            switch (race)
            {
                case RaceId.Spirit: return 1;
                case RaceId.Transcendence: return 2;
                default: return 0;
            }
        }
    }
}
