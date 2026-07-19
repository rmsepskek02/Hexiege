// Unity Editor 전용 일회성 VFX 튜닝 도구.
// 실제 UnitSpawnUseCase를 사용하므로 전투 탐색 -> 공격 애니메이션 -> Animation Event -> VFX 경로를 그대로 검증한다.

using System.Collections.Generic;
using Hexiege.Application;
using Hexiege.Bootstrap;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Presentation;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    public static class SpiritAttackVfxTestSpawner
    {
        private const string MarkerName = "[VFX Test] Spirit Attack Units Spawned";

        [MenuItem("Hexiege/VFX/Spawn Spirit Attack Test")]
        private static void SpawnSpiritAttackTest()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX Test] Play Mode에서만 실행할 수 있습니다.");
                return;
            }

            if (false && GameObject.Find(MarkerName) != null)
            {
                Debug.LogWarning("[VFX Test] 테스트 유닛을 이미 생성했습니다. Play Mode를 다시 시작한 뒤 재실행하세요.");
                return;
            }

            GameBootstrapper bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>();
            HexGrid grid = bootstrapper != null ? bootstrapper.GetGrid() : null;
            UnitSpawnUseCase spawn = bootstrapper != null ? bootstrapper.GetUnitSpawn() : null;
            if (grid == null || spawn == null)
            {
                Debug.LogWarning("[VFX Test] GameBootstrapper 초기화와 맵 로드가 끝난 뒤 실행하세요.");
                return;
            }

            List<TilePair> candidates = FindFreeAdjacentPairs(grid, spawn);
            if (candidates.Count < 2)
            {
                Debug.LogWarning("[VFX Test] 비어 있는 보행 가능 인접 타일 쌍이 부족합니다.");
                return;
            }

            int centerIndex = candidates.Count / 2;
            TilePair first = candidates[centerIndex];
            TilePair second = candidates[Mathf.Min(candidates.Count - 1, centerIndex + 2)];

            int viewCountBefore = Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Length;
            // 인접 타일에 적을 두어 두 정령 모두 즉시 공격 사거리 안에서 실제 전투를 시작한다.
            bool success = SpawnPair(spawn, UnitType.StreamSpirit, first)
                && SpawnPair(spawn, UnitType.TorrentSpirit, second);

            var marker = new GameObject(MarkerName) { hideFlags = HideFlags.HideAndDontSave };
            int createdViewCount = Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Length - viewCountBefore;
            if (!success || createdViewCount < 4)
            {
                CreateDirectVisualPreview(marker, first, second);
                Debug.LogWarning("[VFX Test] UnitFactory 프리팹 누락을 감지해 직접 시각 프리뷰로 전환했습니다. 이 모드는 Animator와 EffectManager 경로만 반복 검증합니다.", marker);
                return;
            }

            Debug.Log($"[VFX Test] 실제 전투: StreamSpirit {first.Blue}->{first.Red}, TorrentSpirit {second.Blue}->{second.Red}에 생성했습니다.", marker);
        }

        private static void CreateDirectVisualPreview(GameObject marker, TilePair streamPair, TilePair torrentPair)
        {
            PreviewEntry stream = CreatePreviewEntry(
                "Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Blue.prefab",
                streamPair, UnitType.StreamSpirit, 0.5f, marker.transform);
            PreviewEntry torrent = CreatePreviewEntry(
                "Assets/_Project/Prefabs/Units/Spirit/Unit_TorrentSpirit_Blue.prefab",
                torrentPair, UnitType.TorrentSpirit, 0.5f, marker.transform);

            // 적 프리팹은 방향과 거리 확인용 시각 기준점이다. 전투 로직이나 UnitView 초기화는 수행하지 않는다.
            CreateReferenceTarget("Stream Target", streamPair.Red, marker.transform);
            CreateReferenceTarget("Torrent Target", torrentPair.Red, marker.transform);
            marker.AddComponent<SpiritAttackPreviewDriver>().Configure(stream, torrent);
        }

        private static PreviewEntry CreatePreviewEntry(string path, TilePair pair, UnitType type, float hitTime, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return default;

            Vector3 position = ToViewPosition(pair.Blue);
            Vector3 target = ToViewPosition(pair.Red);
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity, parent);
            instance.name = $"[VFX Preview] {type}";
            DisableUnitViews(instance);
            Quaternion rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
            instance.transform.rotation = rotation;
            return new PreviewEntry(instance.GetComponentInChildren<Animator>(), type, position, rotation, hitTime);
        }

        private static void CreateReferenceTarget(string name, HexCoord coord, Transform parent)
        {
            const string path = "Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Red.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            GameObject instance = Object.Instantiate(prefab, ToViewPosition(coord), Quaternion.identity, parent);
            instance.name = $"[VFX Preview] {name}";
            DisableUnitViews(instance);
        }

        private static Vector3 ToViewPosition(HexCoord coord)
        {
            Vector3 position = ViewConverter.ToView(HexMetrics.HexToWorld(coord));
            position.y += HexMetrics.UnitYOffset;
            return position;
        }

        private static void DisableUnitViews(GameObject instance)
        {
            UnitView[] views = instance.GetComponentsInChildren<UnitView>(true);
            for (int i = 0; i < views.Length; i++) views[i].enabled = false;
        }

        private static bool SpawnPair(UnitSpawnUseCase spawn, UnitType attackerType, TilePair pair)
        {
            UnitData attacker = spawn.SpawnUnit(attackerType, TeamId.Blue, pair.Blue);
            UnitData target = spawn.SpawnUnit(UnitType.Pistoleer, TeamId.Red, pair.Red);
            return attacker != null && target != null;
        }

        private static List<TilePair> FindFreeAdjacentPairs(HexGrid grid, UnitSpawnUseCase spawn)
        {
            var result = new List<TilePair>();
            foreach (KeyValuePair<HexCoord, HexTile> entry in grid.Tiles)
            {
                if (!entry.Value.IsWalkable || spawn.GetUnitAt(entry.Key) != null) continue;
                List<HexTile> neighbors = grid.GetNeighbors(entry.Key);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    HexTile neighbor = neighbors[i];
                    if (neighbor.IsWalkable && spawn.GetUnitAt(neighbor.Coord) == null)
                    {
                        result.Add(new TilePair(entry.Key, neighbor.Coord));
                        break;
                    }
                }
            }
            return result;
        }

        private static TilePair FindMostDistantPair(List<TilePair> candidates, TilePair first)
        {
            TilePair best = candidates[1];
            int bestDistance = -1;
            for (int i = 1; i < candidates.Count; i++)
            {
                TilePair candidate = candidates[i];
                int distance = Mathf.Min(
                    HexCoord.Distance(first.Blue, candidate.Blue),
                    HexCoord.Distance(first.Red, candidate.Red));
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        private readonly struct TilePair
        {
            public readonly HexCoord Blue;
            public readonly HexCoord Red;

            public TilePair(HexCoord blue, HexCoord red)
            {
                Blue = blue;
                Red = red;
            }
        }

        public readonly struct PreviewEntry
        {
            public readonly Animator Animator;
            public readonly UnitType Type;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float HitTime;

            public PreviewEntry(Animator animator, UnitType type, Vector3 position, Quaternion rotation, float hitTime)
            {
                Animator = animator;
                Type = type;
                Position = position;
                Rotation = rotation;
                HitTime = hitTime;
            }
        }
    }

    /// <summary>
    /// UnitFactory 프리팹 설정이 불완전할 때 사용하는 Editor 전용 반복 드라이버.
    /// UnitView/전투 상태는 초기화하지 않고 Attack Animator와 EffectManager 호출만 정확한 타이밍으로 재현한다.
    /// </summary>
    public sealed class SpiritAttackPreviewDriver : MonoBehaviour
    {
        private SpiritAttackVfxTestSpawner.PreviewEntry[] _entries;
        private float _cycleStart;
        private bool[] _fired;
        private const float CycleDuration = 2f;

        public void Configure(params SpiritAttackVfxTestSpawner.PreviewEntry[] entries)
        {
            _entries = entries;
            _fired = new bool[entries.Length];
            BeginCycle();
        }

        private void Update()
        {
            if (_entries == null) return;
            float elapsed = Time.time - _cycleStart;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_fired[i] || elapsed < _entries[i].HitTime) continue;
                _fired[i] = true;
                EffectManager.Instance?.PlayUnitAttack(
                    _entries[i].Type, _entries[i].Position, _entries[i].Rotation);
            }

            if (elapsed >= CycleDuration) BeginCycle();
        }

        private void BeginCycle()
        {
            _cycleStart = Time.time;
            for (int i = 0; i < _fired.Length; i++)
            {
                _fired[i] = false;
                Animator animator = _entries[i].Animator;
                if (animator != null) animator.Play("Attack", 0, 0f);
            }
        }
    }
}
