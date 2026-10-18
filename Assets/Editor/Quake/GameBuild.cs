using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Windows build'i: Build/Keske/Keske.exe. Yanına "Kayit_icin_ac.bat" yazılır: oyunu tam ekran 1920 x 1080 ve
    /// geliştirici kısayollarıyla (-dev) açar (trailer çekimi). Oyuncu normalde exe'yi açar, kısayollar kapalıdır.
    /// Batch: -executeMethod Deprem.Quake.GameBuild.Build
    /// </summary>
    public static class GameBuild
    {
        private const string Folder = "Build/Keske";

        [MenuItem("Tools/Deprem/Windows build'i al")]
        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Folder + "/Keske.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            Debug.Log($"[GameBuild] {summary.result}: {summary.totalErrors} hata, {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalTime.TotalSeconds:0} sn");
            if (summary.result == BuildResult.Succeeded)
                File.WriteAllText(Folder + "/Kayit_icin_ac.bat",
                    "@echo off\r\nrem Trailer cekimi: tam ekran 1920x1080, gelistirici kisayollari acik (Ctrl+Shift+1..5, O, H, G)\r\n" +
                    "start \"\" \"%~dp0Keske.exe\" -dev -screen-fullscreen 1 -screen-width 1920 -screen-height 1080\r\n");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
