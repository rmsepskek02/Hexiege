// ============================================================================
// ProjectilePresentationCoordinator.cs
// 서버 투사체 "화면 표현"의 순수 C# 일정표 — 발사 기록·착탄 결과를 받아 언제 그리고 언제 끝낼지 정한다.
//
// [왜 순수 C#으로 따로 두는가 — 유니티 초급자용]
//   화면 소비자(ProjectilePresentationPresenter, MonoBehaviour)는 Unity 오브젝트(트레이서·이펙트)를 다루므로
//   Edit Mode에서 시간 흐름을 재현하기 어렵다. "몇 초에 시작, 진행도, 결과가 늦으면 끝점에서 대기, 중복 거부,
//   늦은 접속 중간 시작" 같은 규칙만 여기 모아 두면, 검증(fixture)이 가짜 시계로 그대로 돌려 볼 수 있다.
//   이 클래스는 HP·피해·타겟을 읽거나 쓰지 않는다(표현 전용 — 서버 권위 불변).
//
// [시계 두 개]
//   · presentationTime: 서버 시계(발사/착탄 시각과 같은 시계) 기준 "지금 화면에 그릴 시각".
//   · local           : 이 기기의 실제 경과 시계(Time.realtimeSinceStartupAsDouble) — 대기 시간 상한에만 쓴다.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Application
{
    /// <summary>
    /// 화면 소비자가 구현하는 "그리기 창구". 일정표가 시점을 정하고, 실제 그리기는 이 창구가 한다.
    /// </summary>
    public interface IProjectilePresentationSink
    {
        /// <summary>투사체를 처음 화면에 꺼낸다(발사 시각 도달 또는 늦은 접속으로 비행 중간에 시작).</summary>
        void Begin(ProjectilePresentationCoordinator.Slot slot);

        /// <summary>매 프레임 진행도(0~1)에 맞춰 위치를 갱신한다.</summary>
        void Advance(ProjectilePresentationCoordinator.Slot slot, float progress, double local);

        /// <summary>
        /// 화면상 착탄. withResult=true면 서버 결과가 있으므로 착탄 이펙트(+명중이면 숫자)를 낸다.
        /// false면 결과가 끝내 오지 않아(대기 상한 초과) 투사체만 거둔다.
        /// </summary>
        void Finish(ProjectilePresentationCoordinator.Slot slot, bool withResult);

        /// <summary>게임 종료·epoch 교체 등으로 표현을 즉시 거둔다(착탄 이펙트 없음).</summary>
        void Abort(ProjectilePresentationCoordinator.Slot slot);
    }

    /// <summary>
    /// 서버 투사체 표현 일정표. 키(epoch:id)마다 슬롯 1개. 상한이 있어 메모리가 무한히 늘지 않는다.
    /// </summary>
    public sealed class ProjectilePresentationCoordinator
    {
        /// <summary>동시에 그릴 수 있는 투사체 상한.</summary>
        public const int MaxSlots = 512;

        /// <summary>끝점에 도착했는데 서버 결과가 아직 없을 때 기다리는 최대 실제 시간(초).</summary>
        public const double ResultWaitSeconds = 1.5d;

        /// <summary>발사 기록 없이 결과만 먼저 온 경우(늦은 접속 직후 등) 보관하는 최대 실제 시간(초).</summary>
        public const double OrphanResultSeconds = 3d;

        /// <summary>끝난 키를 기억하는 개수(늦게 온 중복 기록을 거부하기 위한 짧은 기억).</summary>
        public const int FinishedMemory = 1024;

        /// <summary>
        /// 투사체 1발의 표현 상태. 화면 소비자는 <see cref="Visual"/>에 자기 오브젝트를 붙여 쓴다.
        /// </summary>
        public sealed class Slot
        {
            public ProjectileDeliveryKey Key;
            public bool HasLaunch;
            public ProjectileLaunchPresentation Launch;
            public bool HasResult;
            public ProjectileImpactPresentation Result;
            public double ResultReceivedLocal = double.NaN;
            public double CreatedLocal;
            public bool Started;
            public double ArrivedLocal = double.NaN;
            public object Visual;
        }

        private readonly Dictionary<ulong, Slot> _slots = new Dictionary<ulong, Slot>();
        private readonly HashSet<ulong> _finished = new HashSet<ulong>();
        private readonly Queue<ulong> _finishedOrder = new Queue<ulong>();
        private readonly List<ulong> _remove = new List<ulong>();
        private ulong _epoch;

        /// <summary>지금 기억 중인 슬롯 수(진단·검증용).</summary>
        public int Count => _slots.Count;

        /// <summary>현재 epoch.</summary>
        public ulong Epoch => _epoch;

        /// <summary>중복·epoch 불일치·상한 초과로 거부한 수(진단용).</summary>
        public int Rejected { get; private set; }

        /// <summary>결과를 끝내 받지 못해 투사체만 거둔 수(진단용).</summary>
        public int ResultTimeouts { get; private set; }

        /// <summary>새 epoch로 시작한다. 남은 슬롯은 sink로 거둔다(sink가 null이면 기록만 지운다).</summary>
        /// <param name="epoch">새 epoch.</param>
        /// <param name="sink">남은 표현을 거둘 창구(null 허용).</param>
        public void Reset(ulong epoch, IProjectilePresentationSink sink)
        {
            Close(sink);
            _epoch = epoch;
            _finished.Clear();
            _finishedOrder.Clear();
            Rejected = 0;
            ResultTimeouts = 0;
        }

        /// <summary>남은 표현을 모두 거둔다(게임 종료 등). epoch는 유지한다.</summary>
        /// <param name="sink">표현을 거둘 창구(null 허용).</param>
        public void Close(IProjectilePresentationSink sink)
        {
            foreach (Slot slot in _slots.Values)
                if (slot.Started && sink != null) sink.Abort(slot);
            _slots.Clear();
        }

        /// <summary>발사 기록을 받는다. 중복·다른 epoch·상한 초과면 false.</summary>
        /// <param name="launch">서버 발사 기록.</param>
        /// <param name="local">받은 순간의 실제 시계.</param>
        public bool AcceptLaunch(ProjectileLaunchPresentation launch, double local)
        {
            if (!launch.IsValid || launch.Key.Epoch != _epoch || _finished.Contains(launch.Key.Id))
            { Rejected++; return false; }
            if (_slots.TryGetValue(launch.Key.Id, out Slot slot))
            {
                if (slot.HasLaunch) { Rejected++; return false; }   // 정상 전송 + 늦은 접속 스냅샷 중복
                slot.Launch = launch; slot.HasLaunch = true;
                return true;
            }
            if (_slots.Count >= MaxSlots) { Rejected++; return false; }
            _slots.Add(launch.Key.Id, new Slot { Key = launch.Key, HasLaunch = true, Launch = launch, CreatedLocal = local });
            return true;
        }

        /// <summary>착탄 결과를 받는다. 중복·다른 epoch·상한 초과면 false. 발사보다 먼저 와도 잠시 보관한다.</summary>
        /// <param name="result">서버 착탄 결과.</param>
        /// <param name="local">받은 순간의 실제 시계.</param>
        public bool AcceptResult(ProjectileImpactPresentation result, double local)
        {
            if (!result.IsValid || result.Key.Epoch != _epoch || _finished.Contains(result.Key.Id))
            { Rejected++; return false; }
            if (!_slots.TryGetValue(result.Key.Id, out Slot slot))
            {
                if (_slots.Count >= MaxSlots) { Rejected++; return false; }
                slot = new Slot { Key = result.Key, CreatedLocal = local };
                _slots.Add(result.Key.Id, slot);
            }
            if (slot.HasResult) { Rejected++; return false; }
            slot.Result = result; slot.HasResult = true; slot.ResultReceivedLocal = local;
            return true;
        }

        /// <summary>
        /// 매 프레임 진행. 발사 시각이 된 것은 꺼내고(Begin), 진행도에 맞춰 옮기고(Advance),
        /// 끝점에 닿으면 결과가 있을 때 착탄(Finish)한다. 결과가 없으면 상한까지 끝점에서 기다린다.
        /// </summary>
        /// <param name="presentationTime">서버 시계 기준 화면 시각.</param>
        /// <param name="local">실제 경과 시계.</param>
        /// <param name="sink">그리기 창구.</param>
        public void Tick(double presentationTime, double local, IProjectilePresentationSink sink)
        {
            if (sink == null || double.IsNaN(presentationTime) || double.IsInfinity(presentationTime)) return;
            _remove.Clear();
            foreach (Slot slot in _slots.Values)
            {
                if (!slot.HasLaunch)
                {
                    if (local - slot.CreatedLocal > OrphanResultSeconds) _remove.Add(slot.Key.Id);
                    continue;
                }
                if (!slot.Started)
                {
                    if (presentationTime < slot.Launch.LaunchTime) continue;
                    slot.Started = true;
                    sink.Begin(slot);
                }
                float progress = Progress(slot.Launch, presentationTime);
                sink.Advance(slot, progress, local);
                if (progress < 1f) continue;
                if (slot.HasResult)
                {
                    sink.Finish(slot, true);
                    _remove.Add(slot.Key.Id);
                    continue;
                }
                if (double.IsNaN(slot.ArrivedLocal)) slot.ArrivedLocal = local;
                if (local - slot.ArrivedLocal > ResultWaitSeconds)
                {
                    ResultTimeouts++;
                    sink.Finish(slot, false);
                    _remove.Add(slot.Key.Id);
                }
            }
            for (int i = 0; i < _remove.Count; i++) Retire(_remove[i]);
        }

        /// <summary>진행도 = (화면 시각 − 발사) ÷ (착탄 − 발사), 0~1로 자른다. 비행 시간이 0이면 1.</summary>
        /// <param name="launch">발사 기록.</param>
        /// <param name="presentationTime">화면 시각.</param>
        public static float Progress(ProjectileLaunchPresentation launch, double presentationTime)
        {
            double duration = launch.ImpactTime - launch.LaunchTime;
            if (duration <= 0.000001d) return 1f;
            double value = (presentationTime - launch.LaunchTime) / duration;
            return (float)Math.Max(0d, Math.Min(1d, value));
        }

        private void Retire(ulong id)
        {
            _slots.Remove(id);
            if (_finished.Add(id))
            {
                _finishedOrder.Enqueue(id);
                while (_finishedOrder.Count > FinishedMemory) _finished.Remove(_finishedOrder.Dequeue());
            }
        }
    }

    /// <summary>
    /// 투사체 착탄으로 생긴 피격 표현(숫자·피격 VFX·펀치)을 "화면상 착탄 순간"까지 잡아 두는 보관함.
    /// HP는 이미 적용된 뒤라 이 보관함은 표시 시점만 정한다. 순서가 뒤바뀌어(화면 착탄이 먼저) 와도 한 번만 방출한다.
    /// </summary>
    /// <typeparam name="T">보관할 표현 항목 타입(Presentation의 피격 이벤트).</typeparam>
    public sealed class ProjectileImpactPresentationHold<T>
    {
        /// <summary>보관 상한(키 수). 넘치면 바로 방출하도록 호출자에게 알린다.</summary>
        public const int Capacity = 1024;

        /// <summary>화면 착탄 신호가 끝내 오지 않을 때 강제 방출까지 기다리는 실제 시간(초).</summary>
        public const double TimeoutSeconds = 2d;

        private sealed class Entry
        {
            public readonly List<T> Items = new List<T>(2);
            public double Since;
        }

        private readonly Dictionary<ProjectileDeliveryKey, Entry> _held = new Dictionary<ProjectileDeliveryKey, Entry>();
        private readonly Dictionary<ProjectileDeliveryKey, double> _releasedEarly = new Dictionary<ProjectileDeliveryKey, double>();
        private readonly List<ProjectileDeliveryKey> _expired = new List<ProjectileDeliveryKey>();

        /// <summary>보관 중인 키 수(진단·검증용).</summary>
        public int HeldKeys => _held.Count;

        /// <summary>
        /// 항목을 받는다. 이미 화면 착탄 신호가 먼저 왔으면 true(= 지금 바로 방출하라). 아니면 보관하고 false.
        /// 상한을 넘으면 보관하지 않고 true(= 표시 유실을 막기 위해 바로 방출)를 돌려준다.
        /// </summary>
        /// <param name="key">투사체 번호표.</param>
        /// <param name="item">보관할 표현 항목.</param>
        /// <param name="local">실제 시계.</param>
        public bool HoldOrRelease(ProjectileDeliveryKey key, T item, double local)
        {
            if (_releasedEarly.ContainsKey(key)) return true;
            if (!_held.TryGetValue(key, out Entry entry))
            {
                if (_held.Count >= Capacity) return true;
                entry = new Entry { Since = local };
                _held.Add(key, entry);
            }
            entry.Items.Add(item);
            return false;
        }

        /// <summary>
        /// 화면상 착탄 신호. 보관 중인 항목을 output에 모두 꺼낸다. 아직 항목이 없으면 "먼저 왔다"고 기억해 둔다.
        /// </summary>
        /// <param name="key">투사체 번호표.</param>
        /// <param name="local">실제 시계.</param>
        /// <param name="output">방출할 항목을 담을 목록(호출자가 비워 둔다).</param>
        public void Release(ProjectileDeliveryKey key, double local, List<T> output)
        {
            if (_held.TryGetValue(key, out Entry entry))
            {
                output.AddRange(entry.Items);
                _held.Remove(key);
            }
            // 같은 착탄의 다른 대상(범위 피해 등) 이벤트가 조금 뒤에 와도 바로 방출되도록 잠시 기억한다.
            if (_releasedEarly.Count < Capacity) _releasedEarly[key] = local;
        }

        /// <summary>시간이 넘은 보관 항목을 꺼내고(강제 방출), 오래된 "먼저 온 신호" 기억을 지운다.</summary>
        /// <param name="local">실제 시계.</param>
        /// <param name="output">강제 방출할 항목을 담을 목록.</param>
        public void CollectExpired(double local, List<T> output)
        {
            _expired.Clear();
            foreach (var pair in _held)
                if (local - pair.Value.Since > TimeoutSeconds) _expired.Add(pair.Key);
            for (int i = 0; i < _expired.Count; i++)
            {
                output.AddRange(_held[_expired[i]].Items);
                _held.Remove(_expired[i]);
            }
            _expired.Clear();
            foreach (var pair in _releasedEarly)
                if (local - pair.Value > TimeoutSeconds) _expired.Add(pair.Key);
            for (int i = 0; i < _expired.Count; i++) _releasedEarly.Remove(_expired[i]);
        }

        /// <summary>모든 보관을 버린다(재초기화·파괴). 버린 항목은 방출하지 않는다.</summary>
        public void Clear()
        {
            _held.Clear();
            _releasedEarly.Clear();
            _expired.Clear();
        }
    }
}
