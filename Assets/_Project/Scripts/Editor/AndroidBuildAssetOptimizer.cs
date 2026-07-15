#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Editor-only utility for auditing large assets and applying conservative Android texture import defaults.
    /// </summary>
    public static class AndroidBuildAssetOptimizer
    {
        private const string ReportPath = "Assets/_Project/Docs/BuildAssetOptimizationReport.md";

        private static readonly string[] TextureRoots =
        {
            "Assets/_Project/Texture",
            "Assets/_Project/Sprites"
        };

        [MenuItem("Hexiege/Build Size/Generate Asset Size Report")]
        public static void GenerateAssetSizeReport()
        {
            var rows = CollectTextureRows()
                .OrderByDescending(row => row.SourceBytes)
                .ToList();

            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(ReportPath, BuildReport(rows), Encoding.UTF8);
            AssetDatabase.ImportAsset(ReportPath);

            Debug.Log($"[AndroidBuildAssetOptimizer] Report written: {ReportPath} ({rows.Count} textures).");
        }

        [MenuItem("Hexiege/Build Size/Apply Android Texture Defaults")]
        public static void ApplyAndroidTextureDefaults()
        {
            int scanned = 0;
            int changed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", TextureRoots))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (ShouldSkip(path))
                    {
                        continue;
                    }

                    scanned++;
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        continue;
                    }

                    TextureRule rule = GetRule(path);
                    if (ApplyRule(importer, rule))
                    {
                        changed++;
                        importer.SaveAndReimport();
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            GenerateAssetSizeReport();

            Debug.Log($"[AndroidBuildAssetOptimizer] Android texture defaults applied. Scanned: {scanned}, changed: {changed}.");
        }

        private static bool ApplyRule(TextureImporter importer, TextureRule rule)
        {
            bool changed = false;

            if (importer.maxTextureSize > rule.DefaultMaxTextureSize)
            {
                importer.maxTextureSize = rule.DefaultMaxTextureSize;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Compressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                changed = true;
            }

            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Android");
            if (settings.name != "Android")
            {
                settings.name = "Android";
                changed = true;
            }

            if (!settings.overridden)
            {
                settings.overridden = true;
                changed = true;
            }

            if (settings.maxTextureSize != rule.AndroidMaxTextureSize)
            {
                settings.maxTextureSize = rule.AndroidMaxTextureSize;
                changed = true;
            }

            if (settings.format != TextureImporterFormat.Automatic)
            {
                settings.format = TextureImporterFormat.Automatic;
                changed = true;
            }

            if (settings.textureCompression != TextureImporterCompression.Compressed)
            {
                settings.textureCompression = TextureImporterCompression.Compressed;
                changed = true;
            }

            if (settings.compressionQuality != rule.CompressionQuality)
            {
                settings.compressionQuality = rule.CompressionQuality;
                changed = true;
            }

            if (settings.crunchedCompression)
            {
                settings.crunchedCompression = false;
                changed = true;
            }

            if (changed)
            {
                importer.SetPlatformTextureSettings(settings);
            }

            return changed;
        }

        private static IEnumerable<TextureRow> CollectTextureRows()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", TextureRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (ShouldSkip(path))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                FileInfo file = new FileInfo(path);
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                TextureRule rule = GetRule(path);

                yield return new TextureRow(
                    path,
                    file.Exists ? file.Length : 0L,
                    importer.maxTextureSize,
                    android.overridden,
                    android.maxTextureSize,
                    android.textureCompression,
                    android.format,
                    rule.AndroidMaxTextureSize);
            }
        }

        private static string BuildReport(IReadOnlyList<TextureRow> rows)
        {
            long totalBytes = rows.Sum(row => row.SourceBytes);
            var builder = new StringBuilder();
            builder.AppendLine("# Build Asset Optimization Report");
            builder.AppendLine();
            builder.AppendLine("Generated by `Hexiege/Build Size/Generate Asset Size Report`.");
            builder.AppendLine();
            builder.AppendLine($"- Texture count: {rows.Count}");
            builder.AppendLine($"- Source PNG total: {FormatBytes(totalBytes)}");
            builder.AppendLine("- Scope: `Assets/_Project/Texture`, `Assets/_Project/Sprites`");
            builder.AppendLine();
            builder.AppendLine("## Largest Textures");
            builder.AppendLine();
            builder.AppendLine("| Source size | Current max | Android override | Android max | Target max | Compression | Format | Path |");
            builder.AppendLine("|---:|---:|:---:|---:|---:|---|---|---|");

            foreach (TextureRow row in rows.Take(80))
            {
                builder.Append("| ");
                builder.Append(FormatBytes(row.SourceBytes));
                builder.Append(" | ");
                builder.Append(row.DefaultMaxTextureSize);
                builder.Append(" | ");
                builder.Append(row.AndroidOverridden ? "yes" : "no");
                builder.Append(" | ");
                builder.Append(row.AndroidMaxTextureSize);
                builder.Append(" | ");
                builder.Append(row.TargetAndroidMaxTextureSize);
                builder.Append(" | ");
                builder.Append(row.AndroidCompression);
                builder.Append(" | ");
                builder.Append(row.AndroidFormat);
                builder.Append(" | `");
                builder.Append(row.Path);
                builder.AppendLine("` |");
            }

            return builder.ToString();
        }

        private static TextureRule GetRule(string path)
        {
            string normalized = path.Replace('\\', '/');
            bool isUiBackground = ContainsIgnoreCase(normalized, "/Sprites/UI/Backgrounds/");
            bool isStoreGraphic = ContainsIgnoreCase(normalized, "/Sprites/UI/store_");
            bool isUiSprite = normalized.StartsWith("Assets/_Project/Sprites/", StringComparison.OrdinalIgnoreCase);
            bool isModelTexture = normalized.StartsWith("Assets/_Project/Texture/Buildings/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Assets/_Project/Texture/Units/", StringComparison.OrdinalIgnoreCase);

            if (isUiBackground || isStoreGraphic)
            {
                return new TextureRule(defaultMaxTextureSize: 2048, androidMaxTextureSize: 2048, compressionQuality: 50);
            }

            if (isUiSprite)
            {
                return new TextureRule(defaultMaxTextureSize: 2048, androidMaxTextureSize: 1024, compressionQuality: 50);
            }

            if (isModelTexture)
            {
                return new TextureRule(defaultMaxTextureSize: 2048, androidMaxTextureSize: 512, compressionQuality: 50);
            }

            return new TextureRule(defaultMaxTextureSize: 2048, androidMaxTextureSize: 1024, compressionQuality: 50);
        }

        private static bool ShouldSkip(string path)
        {
            string normalized = path.Replace('\\', '/');
            return ContainsIgnoreCase(normalized, "/_Old/")
                || normalized.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsIgnoreCase(string value, string match)
        {
            return value.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatBytes(long bytes)
        {
            const double OneMegabyte = 1024d * 1024d;
            return $"{bytes / OneMegabyte:0.##} MB";
        }

        private readonly struct TextureRule
        {
            public TextureRule(int defaultMaxTextureSize, int androidMaxTextureSize, int compressionQuality)
            {
                DefaultMaxTextureSize = defaultMaxTextureSize;
                AndroidMaxTextureSize = androidMaxTextureSize;
                CompressionQuality = compressionQuality;
            }

            public int DefaultMaxTextureSize { get; }
            public int AndroidMaxTextureSize { get; }
            public int CompressionQuality { get; }
        }

        private readonly struct TextureRow
        {
            public TextureRow(
                string path,
                long sourceBytes,
                int defaultMaxTextureSize,
                bool androidOverridden,
                int androidMaxTextureSize,
                TextureImporterCompression androidCompression,
                TextureImporterFormat androidFormat,
                int targetAndroidMaxTextureSize)
            {
                Path = path;
                SourceBytes = sourceBytes;
                DefaultMaxTextureSize = defaultMaxTextureSize;
                AndroidOverridden = androidOverridden;
                AndroidMaxTextureSize = androidMaxTextureSize;
                AndroidCompression = androidCompression;
                AndroidFormat = androidFormat;
                TargetAndroidMaxTextureSize = targetAndroidMaxTextureSize;
            }

            public string Path { get; }
            public long SourceBytes { get; }
            public int DefaultMaxTextureSize { get; }
            public bool AndroidOverridden { get; }
            public int AndroidMaxTextureSize { get; }
            public TextureImporterCompression AndroidCompression { get; }
            public TextureImporterFormat AndroidFormat { get; }
            public int TargetAndroidMaxTextureSize { get; }
        }
    }
}
#endif
