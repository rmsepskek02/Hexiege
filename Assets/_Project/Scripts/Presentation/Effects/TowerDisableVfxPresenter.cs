// ============================================================================
// TowerDisableVfxPresenter.cs
// EMP 방어탑 무력화 VFX(V6) 화면 구독자 — 2026-11-04 EMP Task 커밋 3(화면, Plan 3-5 V6 c).
//
// 무엇을 하나(초급자용 설명):
//   서버가 EMP로 적 방어탑을 멈추게 하면 "무력화 표현 값"(TowersDisabledPresentation — 탑 Id 목록 + 지속 시간)이
//   이벤트로 흘러온다. 이 컴포넌트는 그 값을 받아 탑마다
//     ① 탑 Id로 이 화면의 건물 데이터를 찾아 "탑 자신의 팀"으로 종족을 정하고,
//     ② 탑 GameObject의 위치(이미 이 화면 좌표)에
//     ③ EffectManager.StartTowerStatusVfx로 합선 · 연기 이펙트(반복 재생)를 시작한 뒤,
//     ④ "받은 순간 + 지속 시간"이 지나면 꼬리 정지(새 입자만 멈춤 → 0.8초 안에 사라짐)한다.
//   종료 알림은 서버에서 오지 않는다 — 이 컴포넌트가 끝나는 시각을 들고 있다가 스스로 끈다(사용자 결정 N1).
//
// 사용자 결정(Plan 7-1)과 이 파일의 대응:
//   - Q3 V5(발동 고리)와 V6를 동시에 시작 → 서버가 발동 순간에 무력화를 걸고 같은 순간 이벤트를 낸다.
//     V5는 기존 SkillActivationVfxPresenter 경로 그대로다(이 파일은 V6만 다룬다).
//   - 같은 탑에 다시 걸리면 → 이펙트를 다시 시작하지 않고 끝나는 시각만 "지금 + 새 지속 시간"으로 바꾼다(끊김 없음).
//     서버도 같은 탑의 남은 시간을 새 지속 시간으로 교체한다(BuildingStatusEffectSystem).
//   - Q4 무력화 중 탑 파괴 · 철거 → 입자까지 즉시 지운다(OnBuildingDied).
//   - Q5 경기 종료 → 남은 V6를 모두 꼬리 정지한다(OnGameEnd). 그 뒤 이 경기에서는 새로 시작하지 않는다
//     (서버도 경기 종료 뒤에는 무력화를 새로 걸지 않는다 — BuildingStatusEffectSystem.Close).
//   - Q11 탑 재가동 연출 없음 → 정지만 한다.
//
// 왜 두 채널(확정 · 수신)을 모두 구독하나:
//   - 싱글 · Host 화면: 서버 권위 쪽이라 「확정」(OnTowersDisabledConfirmed)만 들어온다.
//   - 멀티 순수 클라이언트 화면: 「수신」(OnTowersDisabledReceived)만 들어온다(Host는 ClientRpc 수신부 IsServer 가드로 발행 안 함).
//   → 어느 화면에서도 같은 무력화가 두 번 시작되지 않는다(SkillActivationVfxPresenter와 같은 구조).
//
// 🔴 시전 팀(CasterTeam)을 쓰지 않는다:
//   「수신」 채널의 CasterTeam은 RPC에 팀이 없어 항상 Neutral이다(TowersDisabledPresentation 주석).
//   그래서 종족 · 위치는 언제나 "탑 Id로 찾은 건물 자신의 팀 · 위치"로 정한다.
//
// 빨강(뒤집힌) 화면:
//   건물 GameObject는 BuildingFactory가 만들 때 이미 ViewConverter.ToView로 이 화면 좌표에 놓였다(+ 건물 높이 오프셋).
//   건물은 NetworkObject가 아니라 각 화면의 로컬 인스턴스이고 움직이지 않으므로, 유닛처럼 VisualRootProjector로
//   다시 투영할 필요가 없다. 그래서 탑 GO 위치를 그대로 쓰면 두 화면 모두 같은 탑 위에 나온다.
//   GO가 없을 때(프리팹 누락 등)만 타일 좌표를 ToView로 바꿔 대신 쓴다(이 경우 건물 높이 오프셋은 빠진다).
//
// 끝나는 시각의 시계:
//   Time.time(일시정지 · 배속을 따르는 게임 시간). 서버의 무력화 만료 틱도 게임 시간(deltaTime)으로 줄어든다.
//   순수 클라이언트는 전송 지연만큼 늦게 시작하고 늦게 끝난다(길이는 같다 — 알려진 한계 R-7).
//
// 배치: 씬에 수동 배치하지 않는다. GameBootstrapper(LoadMap)가 자기 GameObject에 AddComponent하고
//   맵을 불러올 때마다 Initialize를 부른다(재호출 안전 — 이전 구독 · 이펙트를 먼저 정리한다).
//
// 진단 로그(2026-11-09, WORKFLOW [4] 「테스트 항목 진단 로그 규칙」 — 개발 축, 머리 [EMP-DIAG], Diag=EmpTowerDisable, DiagRev=1):
//   TowerVfxStarted(처음 시작 — 실제 루트 위치 · 배율) · TowerVfxExtended(재적용 — 재시작 없이 끝 시각만 연장) ·
//   TowerVfxStopped(정지 — 경로 Expire/TowerDied/GameEnd/Reinit/Destroy) · TowerVfxResolveFallback/TowerVfxSkipped(Warn — 탑 GO · 프리셋 없음).
//   모두 상태가 바뀌는 순간 1줄이고, 매 프레임 Update에는 줄이 없다(LogRules 1.14-8).
//
// Presentation 레이어 — Application(이벤트 · 값 객체 · IBuildingObjectProvider) + Core(ViewConverter · HexMetrics) + Domain.
//   탑 공격 판정 · HP · 쿨다운에는 손대지 않는 "표현 전용" 컴포넌트다.
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
    /// EMP 방어탑 무력화 표현 이벤트(확정 · 수신)를 받아 탑마다 무력화 상태 VFX(V6)를 시작 · 연장 · 정지하는 화면 구독자.
    /// </summary>
    public sealed class TowerDisableVfxPresenter : MonoBehaviour
    {
        // ── 탑 하나의 재생 기록 ──
        // class로 둔 이유: 재적용 때 사전(Dictionary)에서 꺼낸 뒤 끝나는 시각만 고쳐 쓰기 위해서다(struct면 다시 넣어야 한다).
        private sealed class ActiveTowerVfx
        {
            public VfxPoolItem Item;   // 재생 중인 풀 항목(VFX 컨테이너 아래 — 탑의 자식 아님)
            public ulong Lease;        // 시작할 때 받은 확인 번호(정지 때 이 번호로만 멈춘다)
            public float EndTime;      // 꼬리 정지할 게임 시간(Time.time 기준)
            public float StartTime;    // [진단] 처음 시작한 게임 시간(정지 줄의 ActiveMs 계산용 — 동작에는 쓰지 않는다)
            public int Sequence;       // [진단] 마지막으로 시작 · 연장시킨 발동 순번(동작에는 쓰지 않는다)
        }

        // VFX 시작 · 정지 담당(풀 관리 포함).
        private EffectManager _effects;
        // 탑 Id → 이 화면의 건물 데이터(팀 · 종류 · 생존 · 타일 좌표). 클라이언트도 같은 Id로 건물 데이터를 갖고 있다.
        private Func<int, BuildingData> _findBuilding;
        // 탑 Id → 이 화면의 건물 GameObject(위치용). 없으면 null.
        private IBuildingObjectProvider _buildingObjects;
        // 팀 → 종족(조합 루트가 주입 — Presentation이 Infrastructure의 GameRaceContext를 직접 부르지 않게).
        private Func<TeamId, RaceId> _raceOfTeam;

        // 탑 Id → 지금 재생 중인 기록. 건물 Id 키라 유닛 Id와 섞이지 않는다.
        private readonly Dictionary<int, ActiveTowerVfx> _active = new Dictionary<int, ActiveTowerVfx>();
        // Update에서 끝난 탑 Id를 모아 두는 재사용 버퍼(순회 중 사전을 바꾸지 않기 위해 — 매 프레임 new 하지 않음).
        private readonly List<int> _expiredBuffer = new List<int>();

        // 경기가 끝났는가. 끝난 뒤에는 이번 경기에서 새 V6를 시작하지 않는다(다음 Initialize에서 풀린다).
        private bool _gameEnded;

        // 구독 핸들. 맵을 다시 불러와 Initialize가 또 불리면 먼저 해제해 중복 처리를 막는다.
        private IDisposable _confirmedSubscription;
        private IDisposable _receivedSubscription;
        private IDisposable _buildingDiedSubscription;
        private IDisposable _gameEndSubscription;

        /// <summary>
        /// 의존성을 주입하고 이벤트 4개(확정 · 수신 · 건물 사망 · 경기 종료)를 구독한다.
        /// 맵을 불러올 때마다 GameBootstrapper가 호출한다(재호출 안전 — 이전 경기의 V6는 입자까지 즉시 지운다).
        /// </summary>
        /// <param name="effects">VFX 매니저. null이면 재생하지 않는다.</param>
        /// <param name="findBuilding">건물 Id → 건물 데이터 조회. null이면 재생하지 않는다.</param>
        /// <param name="buildingObjects">건물 Id → 건물 GameObject 조회(위치용). null이면 타일 좌표로 대신한다.</param>
        /// <param name="raceOfTeam">팀 → 종족 변환. null이면 재생하지 않는다.</param>
        public void Initialize(
            EffectManager effects,
            Func<int, BuildingData> findBuilding,
            IBuildingObjectProvider buildingObjects,
            Func<TeamId, RaceId> raceOfTeam)
        {
            DisposeSubscriptions();
            // 이전 맵(경기)의 V6가 남아 있으면 지운다 — 그 탑들은 맵 정리 때 이미 사라졌으므로 탑 파괴와 같게 입자까지 즉시.
            StopAll(immediate: true, path: "Reinit");

            _effects = effects;
            _findBuilding = findBuilding;
            _buildingObjects = buildingObjects;
            _raceOfTeam = raceOfTeam;
            _gameEnded = false;

            // 채널 이름을 진단 로그에 남기려고 블록 본문 람다로 감싼다(처리 자체는 두 채널이 같다).
            _confirmedSubscription = GameEvents.OnTowersDisabledConfirmed.Subscribe(p => { OnTowersDisabled(p, "Confirmed"); });
            _receivedSubscription = GameEvents.OnTowersDisabledReceived.Subscribe(p => { OnTowersDisabled(p, "Received"); });
            _buildingDiedSubscription = GameEvents.OnBuildingDied.Subscribe(OnBuildingDied);
            _gameEndSubscription = GameEvents.OnGameEnd.Subscribe(OnGameEnd);
        }

        /// <summary> 파괴(씬 언로드) 시 정적 이벤트 허브 구독을 해제하고 남은 V6를 지운다. </summary>
        private void OnDestroy()
        {
            DisposeSubscriptions();
            // 씬 정리 중이면 풀 항목도 함께 파괴되는 중일 수 있다 — StopTowerStatusVfx가 파괴된 항목은 건너뛴다.
            StopAll(immediate: true, path: "Destroy");
        }

        private void DisposeSubscriptions()
        {
            _confirmedSubscription?.Dispose();
            _confirmedSubscription = null;
            _receivedSubscription?.Dispose();
            _receivedSubscription = null;
            _buildingDiedSubscription?.Dispose();
            _buildingDiedSubscription = null;
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = null;
        }

        /// <summary>
        /// 무력화 표현 1건을 받아 탑마다 V6를 시작하거나(처음) 끝나는 시각만 늦춘다(이미 재생 중). 두 채널이 같은 처리를 쓴다.
        /// </summary>
        /// <param name="p">서버가 확정한(또는 클라이언트가 받은) 무력화 표현 값.</param>
        /// <param name="channel">들어온 채널 이름(진단 로그용 — Confirmed 또는 Received).</param>
        private void OnTowersDisabled(TowersDisabledPresentation p, string channel)
        {
            // 컴포넌트가 이미 파괴됐거나(정적 이벤트가 늦게 들어오는 경우) 경기가 끝났으면 아무것도 하지 않는다.
            if (this == null || _gameEnded) return;
            // 지속 시간이 0이면 보여 줄 시간이 없다(값 객체가 음수 · NaN을 0으로 고친다).
            if (!(p.Duration > 0f)) return;

            float endTime = Time.time + p.Duration;
            IReadOnlyList<int> ids = p.TowerIds;
            for (int i = 0; i < ids.Count; i++)
                StartOrExtend(ids[i], endTime, p.Sequence, p.Duration, channel);
        }

        /// <summary>
        /// 탑 하나: 이미 재생 중이면 끝나는 시각만 바꾸고(재시작 없음), 아니면 새로 시작한다.
        /// </summary>
        /// <param name="towerId">무력화된 탑의 건물 Id.</param>
        /// <param name="endTime">꼬리 정지할 게임 시간(Time.time 기준).</param>
        /// <param name="sequence">발동 순번(진단 로그용).</param>
        /// <param name="duration">무력화 지속 시간(초 — 진단 로그용).</param>
        /// <param name="channel">들어온 채널 이름(진단 로그용).</param>
        private void StartOrExtend(int towerId, float endTime, int sequence, float duration, string channel)
        {
            // 재적용: 같은 확인 번호의 재생이 아직 살아 있으면 끝나는 시각만 "지금 + 새 지속 시간"으로 교체한다.
            //   (서버가 남은 시간을 새 지속 시간으로 교체하는 것과 같은 규칙 — 이펙트는 끊기지 않는다.)
            if (_active.TryGetValue(towerId, out ActiveTowerVfx current))
            {
                if (current.Item != null && current.Item.IsStatusLoopActive(current.Lease))
                {
                    current.EndTime = endTime;
                    current.Sequence = sequence;
                    // [진단 L15] 재적용 — 재시작 없이 끝 시각만 바꿨다는 1줄(EMP 시나리오 6 「끊김 없음」).
                    LogTowerVfxExtended(towerId, sequence, duration, channel, endTime - current.StartTime);
                    return;
                }
                // 기록은 있는데 재생이 이미 끝났다(비정상 — 항목 파괴 등). 기록을 버리고 아래에서 새로 시작한다.
                _active.Remove(towerId);
            }

            if (!TryResolveTower(towerId, out RaceId race, out Vector3 viewPosition, out bool usedTileCenter))
            {
                // [진단 L15w] 살아 있는 방어탑을 찾지 못해 띄우지 않았다(늦게 도착한 알림 · 배선 누락).
                LogTowerVfxSkipped(towerId, sequence, channel, "TowerNotResolved");
                return;
            }
            if (_effects == null) return;
            // [진단 L15w] 탑 GO가 없어 타일 중심으로 대신 띄운다(건물 높이 오프셋이 빠진다) — Warn 1줄.
            if (usedTileCenter) LogTowerVfxFallback(towerId, sequence, channel);

            VfxPoolItem item = _effects.StartTowerStatusVfx(race, viewPosition, out ulong lease);
            // 프리셋이 비어 있으면(에셋 값을 아직 넣지 않은 상태 포함) 재생 없음 — 기록도 남기지 않는다.
            if (item == null || lease == 0)
            {
                // [진단 L15w] 종족 프리셋 · 프리팹이 비어 V6를 못 냈다 — Warn 1줄(EMP 시나리오 8 「Preset=Missing」).
                LogTowerVfxSkipped(towerId, sequence, channel, "PresetMissing");
                return;
            }

            _active[towerId] = new ActiveTowerVfx
            {
                Item = item, Lease = lease, EndTime = endTime, StartTime = Time.time, Sequence = sequence
            };
            // [진단 L15] 시작 1줄 — 넘긴 탑 위치와 실제 풀 항목 루트 위치 · 배율(EMP 시나리오 2 · 8).
            LogTowerVfxStarted(towerId, sequence, duration, channel, race, viewPosition, item.transform, lease);
            // EMP SFX 없음(사용자 결정 Q7). 생기면 V5(발동 고리) 재생 바로 아래에서 짝으로 부른다(사운드 규칙 15).
        }

        /// <summary>
        /// 탑 Id로 종족과 이 화면 좌표의 위치를 정한다. 살아 있는 방어탑(AutoTower)이 아니면 false.
        /// </summary>
        /// <param name="towerId">건물 Id.</param>
        /// <param name="race">탑 자신의 팀으로 정한 종족.</param>
        /// <param name="viewPosition">탑 위치(이 화면 좌표).</param>
        /// <param name="usedTileCenter">탑 GameObject가 없어 타일 중심으로 대신했으면 true(진단 로그용).</param>
        /// <returns>재생할 수 있는 탑이면 true.</returns>
        private bool TryResolveTower(int towerId, out RaceId race, out Vector3 viewPosition, out bool usedTileCenter)
        {
            race = default;
            viewPosition = default;
            usedTileCenter = false;
            if (_findBuilding == null || _raceOfTeam == null) return false;

            // 이 화면의 건물 데이터 — 서버 · 클라이언트 모두 같은 Id로 갖고 있다(NetworkBuildingController가 같은 Id로 생성).
            BuildingData building = _findBuilding(towerId);
            // 이미 파괴됐거나(늦게 도착한 알림) 방어탑이 아니면 띄우지 않는다(서버도 살아 있는 AutoTower만 무력화한다).
            if (building == null || !building.IsAlive || building.Type != BuildingType.AutoTower) return false;

            // 🔴 시전 팀이 아니라 "탑 자신의 팀"으로 종족을 정한다(「수신」 채널의 시전 팀은 항상 Neutral).
            race = _raceOfTeam(building.Team);

            // 위치: 탑 GameObject(이미 이 화면 좌표 + 건물 높이 오프셋)를 그대로 쓴다. 없으면 타일 중심을 화면 좌표로 바꾼다.
            GameObject towerObject = _buildingObjects?.GetBuildingObject(towerId);
            viewPosition = towerObject != null
                ? towerObject.transform.position
                : ViewConverter.ToView(HexMetrics.HexToWorld(building.Position));
            usedTileCenter = towerObject == null;
            return true;
        }

        /// <summary>
        /// 무력화 중인 탑이 파괴 · 철거되면 그 탑의 V6를 입자까지 즉시 지운다(사용자 결정 Q4).
        /// 탑 GameObject는 BuildingFactory가 같은 이벤트에서 파괴한다 — V6는 탑의 자식이 아니라서 함께 파괴되지 않으므로 여기서 끈다.
        /// </summary>
        /// <param name="e">건물 사망 이벤트(서버 · 클라이언트 모두 발행).</param>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            if (this == null || e.Building == null) return;
            int id = e.Building.Id;
            if (!_active.TryGetValue(id, out ActiveTowerVfx entry)) return;
            _active.Remove(id);
            StopEntry(id, entry, immediate: true, path: "TowerDied");
        }

        /// <summary>
        /// 경기가 끝나면 남은 V6를 모두 꼬리 정지하고(사용자 결정 Q5), 이번 경기에서는 새로 시작하지 않는다.
        /// 클라이언트에서도 결과 발표 수신 때 OnGameEnd가 발행된다(NetworkGameEndController.AnnounceWinnerClientRpc).
        /// </summary>
        /// <param name="e">경기 종료 이벤트(내용은 쓰지 않는다).</param>
        private void OnGameEnd(GameEndEvent e)
        {
            if (this == null) return;
            _gameEnded = true;
            StopAll(immediate: false, path: "GameEnd");
        }

        /// <summary>
        /// 매 프레임: 끝나는 시각이 지난 탑의 V6를 꼬리 정지한다. 재생 중인 V6가 없으면 바로 돌아간다(비용 없음).
        /// </summary>
        private void Update()
        {
            if (_active.Count == 0) return;

            float now = Time.time;
            _expiredBuffer.Clear();
            foreach (KeyValuePair<int, ActiveTowerVfx> pair in _active)
            {
                // 끝나는 시각이 지났거나, 재생이 이미 바깥에서 끝난(항목 파괴 등) 기록을 모은다.
                ActiveTowerVfx entry = pair.Value;
                if (now >= entry.EndTime || entry.Item == null || !entry.Item.IsStatusLoopActive(entry.Lease))
                    _expiredBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                int id = _expiredBuffer[i];
                ActiveTowerVfx entry = _active[id];
                _active.Remove(id);
                // 꼬리 정지 — 새 입자만 멈추고, 남은 입자(최대 수명 0.8초)가 사라지면 풀 항목이 스스로 돌아간다.
                //   재가동 연출은 없다(사용자 결정 Q11). 이미 끝난 재생이면 확인 번호가 맞지 않아 아무 일도 없다.
                StopEntry(id, entry, immediate: false, path: "Expire");
            }
            _expiredBuffer.Clear();
        }

        /// <summary>
        /// 재생 중인 V6를 모두 멈추고 기록을 비운다.
        /// </summary>
        /// <param name="immediate">true = 입자까지 즉시 지움(맵 재초기화 · 파괴), false = 꼬리 정지(경기 종료).</param>
        /// <param name="path">정지 경로 이름(진단 로그용 — GameEnd · Reinit · Destroy).</param>
        private void StopAll(bool immediate, string path)
        {
            if (_active.Count == 0) return;
            foreach (KeyValuePair<int, ActiveTowerVfx> pair in _active)
                StopEntry(pair.Key, pair.Value, immediate, path);
            _active.Clear();
        }

        /// <summary>
        /// 기록 하나의 V6를 멈춘다(확인 번호가 맞을 때만 실제로 멈춘다).
        /// EffectManager는 Unity 오브젝트라 ?. 대신 == null 검사(파괴된 객체도 null로 보는 Unity 방식)를 쓴다.
        /// 씬 정리 중 EffectManager가 먼저 파괴됐어도 항목에 직접 멈춤을 요청한다 — 항목까지 파괴됐으면 건너뛴다.
        /// </summary>
        /// <param name="towerId">탑 Id(진단 로그용).</param>
        /// <param name="entry">멈출 기록.</param>
        /// <param name="immediate">true = 입자까지 즉시 지움, false = 꼬리 정지.</param>
        /// <param name="path">정지 경로 이름(진단 로그용 — Expire · TowerDied · GameEnd · Reinit · Destroy).</param>
        private void StopEntry(int towerId, ActiveTowerVfx entry, bool immediate, string path)
        {
            if (entry == null) return;
            // 정지 결과(확인 번호가 맞아 실제로 멈췄는가)를 진단 줄에 싣는다 — 멈추는 동작 자체는 종전과 같다.
            bool stopped = false;
            if (_effects != null) stopped = _effects.StopTowerStatusVfx(entry.Item, entry.Lease, immediate);
            else if (entry.Item != null) stopped = entry.Item.StopStatusLoop(entry.Lease, immediate);
            // [진단 L16] 정지 1줄(EMP 시나리오 5 · 7 · 9).
            LogTowerVfxStopped(towerId, entry, immediate, path, stopped);
        }

        // ====================================================================
        // 진단 로그(개발 축 — 릴리스 빌드에서는 호출 · 문자열 조립까지 통째로 사라진다, LogRules 1.7)
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTowerVfxStarted(int towerId, int sequence, float duration, string channel, RaceId race,
                                               Vector3 towerPosition, Transform root, ulong lease)
        {
            Vector3 rootPos = root != null ? root.position : towerPosition;
            float rootScale = root != null ? root.localScale.x : 0f;
            GameLog.Dev.Info("Skill", nameof(TowerDisableVfxPresenter), "[EMP-DIAG] TowerVfxStarted",
                FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowerVfxStarted, Role={DiagRole()}, Channel={channel}, Sequence={sequence}, TowerId={towerId}, Race={race}, Preset=Found, DurationMs={DiagMilli(duration)}, ViewFlipped={ViewConverter.IsFlipped}, TowerXmm={DiagMilli(towerPosition.x)}, TowerYmm={DiagMilli(towerPosition.y)}, TowerZmm={DiagMilli(towerPosition.z)}, RootXmm={DiagMilli(rootPos.x)}, RootYmm={DiagMilli(rootPos.y)}, RootZmm={DiagMilli(rootPos.z)}, OffsetYmm={DiagMilli(rootPos.y - towerPosition.y)}, RootScale={rootScale:F2}, Lease={lease}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTowerVfxExtended(int towerId, int sequence, float duration, string channel, float newEndAfterStart)
        {
            GameLog.Dev.Info("Skill", nameof(TowerDisableVfxPresenter), "[EMP-DIAG] TowerVfxExtended",
                FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowerVfxExtended, Role={DiagRole()}, Channel={channel}, Sequence={sequence}, TowerId={towerId}, DurationMs={DiagMilli(duration)}, NewEndAfterStartMs={DiagMilli(newEndAfterStart)}, Restarted=False"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTowerVfxFallback(int towerId, int sequence, string channel)
        {
            GameLog.Dev.Warn("Skill", nameof(TowerDisableVfxPresenter), "[EMP-DIAG] TowerVfxResolveFallback",
                FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowerVfxResolveFallback, Role={DiagRole()}, Channel={channel}, Sequence={sequence}, TowerId={towerId}, Reason=TowerObjectMissing, UsedTileCenter=True"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTowerVfxSkipped(int towerId, int sequence, string channel, string reason)
        {
            GameLog.Dev.Warn("Skill", nameof(TowerDisableVfxPresenter), "[EMP-DIAG] TowerVfxSkipped",
                FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowerVfxSkipped, Role={DiagRole()}, Channel={channel}, Sequence={sequence}, TowerId={towerId}, Reason={reason}, Preset={(reason == "PresetMissing" ? "Missing" : "Unknown")}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTowerVfxStopped(int towerId, ActiveTowerVfx entry, bool immediate, string path, bool stopped)
        {
            GameLog.Dev.Info("Skill", nameof(TowerDisableVfxPresenter), "[EMP-DIAG] TowerVfxStopped",
                FormattableString.Invariant(
                    $"Diag=EmpTowerDisable, DiagRev=1, Event=TowerVfxStopped, Role={DiagRole()}, Sequence={entry.Sequence}, TowerId={towerId}, Path={path}, Immediate={immediate}, ActiveMs={DiagMilli(Time.time - entry.StartTime)}, LeaseMatched={stopped}"));
        }
    }
}
