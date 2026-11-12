using System;
using System.Collections.Generic;

using System.Reflection;
using Hexiege.Application;
using Hexiege.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.EditorTools
{
    // 실제 프리팹을 쓰는 정지 상태 집중 검사. 씬/에셋을 저장하지 않는다.
    internal static class RankingLazyPoolNativeFixture
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Read(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Write(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Flags).Invoke(target, null);
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }

        private static bool SameRows(List<RankRowView> rows, RankRowView[] original, int count)
        {
            for (int i = 0; i < count; i++) if (rows[i] != original[i]) return false;
            return true;
        }

        [MenuItem("Hexiege/Combat/Diagnostics/Self Validate Ranking Lazy Pool")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEditorInternal.ProfilerDriver.enabled
                || NetworkContext.IsNetworkActive || GameServicesLocator.Current != null
                || (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening))
            { Debug.LogWarning("Ranking fixture requires stopped Play/network/services."); return; }
            string run = Guid.NewGuid().ToString("N");
            bool passed = false;
            bool cleanupCompleted = false;
            Debug.Log("[RANKING-LAZY-VERIFY] BEGIN Run=" + run + " Mode=EditModeExplicitCleanup");
            GameObject root = null;
            RankingView view = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/RankRow.prefab");
                Require(prefab != null && prefab.GetComponent<RankRowView>() != null, "Actual RankRow prefab must exist.");
                root = new GameObject("PrivateRankingLazyFixture", typeof(RectTransform))
                    { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false);
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(root.transform, false);
                view = root.AddComponent<RankingView>();
                Write(view, "_rowPrefab", prefab.GetComponent<RankRowView>());
                Write(view, "_content", content.GetComponent<RectTransform>());
                Invoke(view, "InitializeIfNeeded");
                Invoke(view, "InitializeIfNeeded");
                var rows = (List<RankRowView>)Read(view, "_rows");
                Require(rows.Count == 0 && content.transform.childCount == 0, "Initialization must create no hidden rows.");
                view.enabled = false;
                view.enabled = true;
                Require(rows.Count == 0, "Disable/enable must not create the row pool.");
                Invoke(view, "RenderCurrentPage");
                Require(rows.Count == 10 && content.transform.childCount == 10, "First empty render must prepare ten rows.");
                var original = rows.ToArray();
                foreach (var row in rows)
                {
                    var layout = row.GetComponent<LayoutElement>();
                    Require(layout != null && layout.minHeight == 96 && layout.preferredHeight == -1
                        && layout.flexibleHeight == 0, "Actual prefab row height must match.");
                    foreach (Transform column in row.transform)
                        if (column.TryGetComponent(out LayoutElement cell))
                            Require(cell.minWidth == 0 && cell.preferredWidth == 0, "Actual column widths must match.");
                }
                var entries = (List<LeaderboardEntry>)Read(view, "_entries");
                for (int i = 0; i < 25; i++) entries.Add(new LeaderboardEntry
                    { Rank = i + 1, Nickname = "Fixture", NicknameCode = "0000", TotalGames = 10, Wins = 5, Losses = 5, WinRate = 50 });
                Invoke(view, "RenderCurrentPage");
                Write(view, "_currentPage", 1);
                Invoke(view, "RenderCurrentPage");
                Invoke(view, "ApplyLayoutPreview");
                Require(SameRows(rows, original, 10) && content.transform.childCount == 10, "Page/preview must reuse all actual instances.");
                // native 생성 실패 주입 대신 실제 partial-slot 상태를 구성한다. 예외 주입은 CLR 검사에서 수행한다.
                for (int i = 9; i >= 3; i--) UnityEngine.Object.DestroyImmediate(rows[i].gameObject);
                rows.RemoveRange(3, 7);
                Write(view, "_rowPoolPrepared", false);
                Invoke(view, "RenderCurrentPage");
                Require(rows.Count == 10 && content.transform.childCount == 10
                    && SameRows(rows, original, 3), "Partial slots must resume without duplicate children.");
                Invoke(view, "OnDestroy");
                Write(view, "_rowPoolPrepared", false);
                Invoke(view, "RenderCurrentPage");
                Require(content.transform.childCount == 10, "Closed owner must not create more rows.");
                passed = true;
                Debug.Log("[RANKING-LAZY-VERIFY] ASSERTIONS-PASS Run=" + run
                    + " InitRows=0 FirstRows=10 ReusedRows=10 PartialRetained=3 FinalRows=10 Preview=True Closed=True Mode=EditModeExplicitCleanup NotPlayLifecycleOrVisualAcceptance=True");
            }
            finally
            {
                try
                {
                    try { if (view != null) Invoke(view, "OnDestroy"); }
                    finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
                    cleanupCompleted = true;
                }
                finally
                {
                    Debug.Log("[RANKING-LAZY-VERIFY] END Run=" + run + " Passed=" + (passed && cleanupCompleted)
                        + " AssertionsPassed=" + passed + " CleanupCompleted=" + cleanupCompleted);
                }
            }
        }
    }
}