// ============================================================================
// SfxNormalizeTool.cs  (영구 보존 도구 — Assets/Editor/Tools/, 삭제 금지)
//
// 무엇을 하는가 (2026-10-27, 사용자 승인 기준):
//   Project 창에서 고른 효과음 wav 파일들을 "같은 기준"으로 맞춘다.
//     모노로 합치기 → 앞·뒤 무음 자르기(+끝 페이드아웃) → 체감 크기 -26dB → 피크 -1dBFS 이하 → 44100Hz 16bit 모노 저장
//   실제 계산(읽기·측정·게인·쓰기 바이트 만들기)은 SfxNormalizeCore.cs 가 하고,
//   이 파일은 메뉴·확인 창·백업·덮어쓰기·재임포트·로그만 담당한다.
//   기준값(-26dB, -1dBFS, 여유·페이드 길이)은 SfxNormalizeCore 상단 상수에 모여 있다.
//
// 언제 쓰나:
//   Unity AI 로 효과음을 새로 만들었을 때(예: 유닛 공격음). 만든 wav 를 Project 창에서 고르고 메뉴를 누른다.
//   같은 파일에 여러 번 돌려도 결과가 같다(이미 맞춘 파일은 "변경 없음"으로 건너뛰고 백업도 만들지 않는다).
//
// 무엇을 고를 수 있나:
//   - wav 파일 여러 개를 직접 골라도 되고,
//   - 폴더를 고르면 그 폴더와 "모든 하위 폴더"의 .wav 를 대상으로 한다
//     (예: SFX/Units 폴더 하나만 골라 유닛 공격음 전체를 한 번에 맞출 수 있게). 대상 수는 확인 창에 먼저 보여준다.
//   - .mp3/.ogg 등 wav 가 아닌 오디오는 대상이 아니다(조용히 제외).
//
// 원본 보호:
//   덮어쓰기 직전에 원본을 Assets/_Project/Audio/.backups/ 에 "원래이름_시각.wav" 로 복사한다.
//   폴더 이름이 점(.)으로 시작하면 Unity 가 에셋으로 가져오지 않는다(.meta 도 안 생기고 빌드에도 안 들어간다).
//   백업 복사에 실패한 파일은 덮어쓰지 않는다.
//   .meta(임포트 설정·GUID)는 건드리지 않으므로 SoundConfig 등의 기존 연결은 그대로 유지된다.
//
// 건너뛰는 경우(경고 로그):
//   44100Hz 가 아님 / 16bit PCM 이 아님(변환하지 않는다) / 무음·너무 작음 / wav 형식이 아님.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hexiege.Application;
using UnityEditor;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Project 창에서 고른 효과음 wav 를 같은 기준(모노·무음 트림·체감 크기·피크 상한)으로 맞추는 메뉴 도구.
    /// </summary>
    public static class SfxNormalizeTool
    {
        private const string MenuPath = "Hexiege/Audio/Normalize Selected SFX";
        private const string DialogTitle = "Normalize SFX";
        private const string LogSystem = "Audio";
        private const string LogClass = nameof(SfxNormalizeTool);

        /// <summary>원본 백업 폴더. 점(.)으로 시작하는 폴더라 Unity 가 에셋으로 가져오지 않는다.</summary>
        private const string BackupFolder = "Assets/_Project/Audio/.backups";

        /// <summary>확인 창에 이름을 직접 보여줄 최대 파일 수(그 이상은 "외 N개"로 줄인다).</summary>
        private const int MaxNamesInDialog = 12;

        /// <summary>
        /// 메뉴: 선택한 wav(폴더면 하위 wav 포함)를 정규화하고 원본은 백업한 뒤 덮어쓴다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void NormalizeSelected()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Play 모드에서는 실행할 수 없습니다. Play를 멈춘 뒤 다시 실행하세요.", "확인");
                return;
            }

            List<string> targets = CollectSelectedWavPaths();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, "선택한 항목에 .wav 파일이 없습니다.\nProject 창에서 wav 파일이나 폴더를 고른 뒤 다시 실행하세요.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog(DialogTitle, BuildConfirmMessage(targets), "정규화 후 덮어쓰기", "취소"))
                return;

            RunBatch(targets);
        }

        /// <summary>
        /// 메뉴 활성화 조건: Play 중이 아니고 Project 창에서 무언가를 골랐을 때만 메뉴를 누를 수 있게 한다.
        /// (실제 wav 가 있는지는 누른 뒤 검사한다 — 폴더 안을 뒤지는 일을 메뉴를 열 때마다 하지 않기 위해.)
        /// </summary>
        [MenuItem(MenuPath, true)]
        private static bool NormalizeSelectedValidate()
        {
            return !EditorApplication.isPlaying && Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
        }

        // ─────────────────────────────────────────────────────────────────
        //  대상 모으기 / 확인 창
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 선택된 에셋에서 .wav 경로를 모은다. 폴더는 하위 폴더까지 뒤진다. 중복은 하나로, 순서는 경로순으로 고정한다.
        /// </summary>
        private static List<string> CollectSelectedWavPaths()
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.IsValidFolder(path))
                {
                    // FindAssets 는 지정 폴더의 모든 하위 폴더까지 찾는다. 점(.) 폴더(백업)는 Unity 가 모르므로 자연히 빠진다.
                    foreach (string clipGuid in AssetDatabase.FindAssets("t:AudioClip", new[] { path }))
                        AddIfWav(set, AssetDatabase.GUIDToAssetPath(clipGuid));
                }
                else
                {
                    AddIfWav(set, path);
                }
            }
            return new List<string>(set);
        }

        private static void AddIfWav(SortedSet<string> set, string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                set.Add(assetPath);
        }

        /// <summary>확인 창 문구: 대상 파일 수·이름 일부·적용 기준값·백업 위치.</summary>
        private static string BuildConfirmMessage(List<string> targets)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"대상 wav {targets.Count}개를 아래 기준으로 맞춘 뒤 원본을 덮어씁니다.");
            sb.AppendLine();
            int shown = Math.Min(targets.Count, MaxNamesInDialog);
            for (int i = 0; i < shown; i++) sb.AppendLine("  · " + Path.GetFileName(targets[i]));
            if (targets.Count > shown) sb.AppendLine($"  · 외 {targets.Count - shown}개");
            sb.AppendLine();
            sb.AppendLine("[기준]");
            sb.AppendLine("  · 모노(채널 평균)로 합침");
            sb.AppendLine($"  · 앞 무음 자르기(소리 시작 앞 {SfxNormalizeCore.LeadPaddingMs:0}ms 남김)");
            sb.AppendLine($"  · 뒤 무음 자르기(소리 끝 뒤 {SfxNormalizeCore.TailPaddingMs:0}ms 남김, 끝 {SfxNormalizeCore.FadeOutMs:0}ms 페이드아웃)");
            sb.AppendLine($"  · 체감 크기(50ms 최대 RMS) {SfxNormalizeCore.Fmt(SfxNormalizeCore.TargetLoudnessDb)} dB");
            sb.AppendLine($"  · 피크 {SfxNormalizeCore.Fmt(SfxNormalizeCore.PeakCeilingDbfs)} dBFS 이하(넘으면 게인을 낮추고 경고)");
            sb.AppendLine($"  · {SfxNormalizeCore.RequiredSampleRate}Hz / {SfxNormalizeCore.RequiredBitsPerSample}bit / 모노 PCM 으로 저장");
            sb.AppendLine();
            sb.AppendLine("원본은 " + BackupFolder + "/ 에 시각이 붙은 이름으로 백업합니다.");
            sb.AppendLine("44100Hz·16bit PCM 이 아닌 파일과 무음 파일은 건너뜁니다.");
            sb.AppendLine();
            sb.Append("진행할까요?");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        //  실행
        // ─────────────────────────────────────────────────────────────────

        /// <summary>대상 파일들을 하나씩 처리하고, 바꾼 파일만 모아 재임포트한 뒤 요약을 보여준다.</summary>
        private static void RunBatch(List<string> targets)
        {
            // 한 번 실행에서 백업 시각은 하나로 통일한다(같은 회차의 백업끼리 묶어 찾기 쉽게).
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var written = new List<string>();
            int unchanged = 0, skipped = 0, limited = 0, failed = 0;

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    string path = targets[i];
                    EditorUtility.DisplayProgressBar(DialogTitle, Path.GetFileName(path), (float)i / targets.Count);
                    switch (ProcessOne(path, stamp, out bool peakLimited))
                    {
                        case FileStatus.Written: written.Add(path); break;
                        case FileStatus.Unchanged: unchanged++; break;
                        case FileStatus.Skipped: skipped++; break;
                        default: failed++; break;
                    }
                    if (peakLimited) limited++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ReimportWritten(written);

            string summary = $"Targets={targets.Count}, Written={written.Count}, Unchanged={unchanged}, Skipped={skipped}, "
                             + $"PeakLimited={limited}, Failed={failed}, BackupStamp={stamp}";
            GameLog.Dev.Info(LogSystem, LogClass, "효과음 정규화 실행 완료", summary);
            EditorUtility.DisplayDialog(DialogTitle,
                $"완료: 대상 {targets.Count}개\n  · 덮어씀 {written.Count}개\n  · 이미 기준에 맞음(변경 없음) {unchanged}개\n"
                + $"  · 건너뜀 {skipped}개\n  · 피크 제한 경고 {limited}개\n  · 실패 {failed}개\n\n"
                + "파일별 처리 전/후 수치는 Console 로그를 확인하세요.", "확인");
        }

        private enum FileStatus { Written, Unchanged, Skipped, Failed }

        /// <summary>파일 하나를 읽고 정규화하고, 필요하면 백업 후 덮어쓴다. 결과를 로그로 남긴다.</summary>
        private static FileStatus ProcessOne(string assetPath, string stamp, out bool peakLimited)
        {
            peakLimited = false;
            string fullPath = ToFullPath(assetPath);
            byte[] input;
            try
            {
                input = File.ReadAllBytes(fullPath);
            }
            catch (Exception e)
            {
                GameLog.Dev.Warn(LogSystem, LogClass, "wav 파일을 읽지 못해 건너뛴다", $"Path={assetPath}, Error={e.GetType().Name}: {e.Message}");
                return FileStatus.Failed;
            }

            SfxNormalizeCore.Result r = SfxNormalizeCore.Normalize(input);
            if (r.Outcome == SfxNormalizeCore.Outcome.Skipped)
            {
                GameLog.Dev.Warn(LogSystem, LogClass, "효과음 정규화 건너뜀 — " + r.SkipReason, $"Path={assetPath}, {FormatStats("Before", r.Before)}");
                return FileStatus.Skipped;
            }

            peakLimited = r.PeakLimited;
            if (r.PeakLimited)
            {
                GameLog.Dev.Warn(LogSystem, LogClass,
                    "피크 상한 때문에 게인을 목표보다 낮췄다 — 체감 크기가 목표보다 작다(리미터는 쓰지 않음)",
                    $"Path={assetPath}, TargetGainDb={SfxNormalizeCore.Fmt(r.TargetGainDb)}, AppliedGainDb={SfxNormalizeCore.Fmt(r.AppliedGainDb)}, "
                    + $"AfterLoudnessDb={SfxNormalizeCore.Fmt(r.After.LoudnessDb)}");
            }

            if (r.Outcome == SfxNormalizeCore.Outcome.NoChange)
            {
                GameLog.Dev.Info(LogSystem, LogClass, "효과음이 이미 기준에 맞아 변경하지 않았다", BuildDetail(assetPath, r, null));
                return FileStatus.Unchanged;
            }

            if (!TryBackup(assetPath, fullPath, stamp, out string backupPath))
                return FileStatus.Failed;
            try
            {
                File.WriteAllBytes(fullPath, r.OutputBytes);
            }
            catch (Exception e)
            {
                GameLog.Dev.Warn(LogSystem, LogClass, "정규화 결과를 쓰지 못했다 — 원본은 백업에 있다",
                    $"Path={assetPath}, Backup={backupPath}, Error={e.GetType().Name}: {e.Message}");
                return FileStatus.Failed;
            }
            GameLog.Dev.Info(LogSystem, LogClass, "효과음 정규화 완료", BuildDetail(assetPath, r, backupPath));
            return FileStatus.Written;
        }

        /// <summary>
        /// 원본을 백업 폴더에 "원래이름_시각.wav" 로 복사한다. 같은 이름이 있으면 _2, _3 … 을 붙인다
        /// (다른 폴더에 같은 파일명이 있어도 덮어쓰지 않게). 실패하면 false — 그 파일은 덮어쓰지 않는다.
        /// </summary>
        private static bool TryBackup(string assetPath, string fullPath, string stamp, out string backupAssetPath)
        {
            backupAssetPath = null;
            try
            {
                string backupDir = ToFullPath(BackupFolder);
                Directory.CreateDirectory(backupDir);
                string baseName = Path.GetFileNameWithoutExtension(assetPath) + "_" + stamp;
                string fileName = baseName + ".wav";
                for (int n = 2; File.Exists(Path.Combine(backupDir, fileName)); n++)
                    fileName = baseName + "_" + n + ".wav";
                File.Copy(fullPath, Path.Combine(backupDir, fileName), false);
                backupAssetPath = BackupFolder + "/" + fileName;
                return true;
            }
            catch (Exception e)
            {
                GameLog.Dev.Warn(LogSystem, LogClass, "원본 백업에 실패해 이 파일은 덮어쓰지 않는다",
                    $"Path={assetPath}, Error={e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 덮어쓴 파일만 다시 가져온다(.meta 의 임포트 설정·GUID 는 그대로 — 내용만 새로 읽는다).
        /// Start/StopAssetEditing 으로 묶어 여러 파일을 한 번에 임포트한다.
        /// </summary>
        private static void ReimportWritten(List<string> written)
        {
            if (written.Count == 0) return;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in written)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  로그 문자열 / 경로 도우미
        // ─────────────────────────────────────────────────────────────────

        /// <summary>파일 하나의 처리 전/후 수치를 "key=value" 한 줄로 만든다.</summary>
        private static string BuildDetail(string assetPath, SfxNormalizeCore.Result r, string backupPath)
        {
            return $"Path={assetPath}, {FormatStats("Before", r.Before)}, {FormatStats("After", r.After)}, "
                   + $"TargetGainDb={SfxNormalizeCore.Fmt(r.TargetGainDb)}, AppliedGainDb={SfxNormalizeCore.Fmt(r.AppliedGainDb)}, "
                   + $"PeakLimited={r.PeakLimited}, TrimLeadMs={Ms(r.TrimmedLeadSec)}, TrimTailMs={Ms(r.TrimmedTailSec)}, "
                   + $"Backup={(backupPath ?? "none")}";
        }

        private static string FormatStats(string prefix, SfxNormalizeCore.Stats s)
        {
            return $"{prefix}Ch={s.Channels}, {prefix}Sec={s.DurationSec.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}, "
                   + $"{prefix}PeakDbfs={SfxNormalizeCore.Fmt(s.PeakDbfs)}, {prefix}RawPeakDbfs={SfxNormalizeCore.Fmt(s.RawPeakDbfs)}, "
                   + $"{prefix}LoudnessDb={SfxNormalizeCore.Fmt(s.LoudnessDb)}, "
                   + $"{prefix}SoundMs={Ms(s.SoundStartSec)}~{Ms(s.SoundEndSec)}";
        }

        private static string Ms(double sec) => (sec * 1000.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// "Assets/..." 에셋 경로를 디스크 절대 경로로 바꾼다.
        /// 주의: 이 네임스페이스 안에서 그냥 Application 이라고 쓰면 Hexiege.Application 네임스페이스로 해석되므로
        /// UnityEngine.Application 으로 완전히 적는다.
        /// </summary>
        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
