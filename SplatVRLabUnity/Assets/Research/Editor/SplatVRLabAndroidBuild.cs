using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SplatVRLab.Editor
{
    public static class SplatVRLabAndroidBuild
    {
        private const string OutputRelativePath = "Builds/Android/SplatVRLabUnity-dev.apk";

        [MenuItem("SplatVRLab/Build Android development APK")]
        public static void BuildDevelopmentApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException(
                    "Android Build Support is not installed for this Unity editor. " +
                    "Add Android Build Support, Android SDK & NDK Tools, and OpenJDK in Unity Hub.");
            }

            SplatVRLabSetup.Configure();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Unity could not switch the active target to Android.");

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scene is configured for the Android build.");

            string outputPath = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory,
                OutputRelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android build ended with {report.summary.result} and " +
                    $"{report.summary.totalErrors} reported errors. See the Unity Editor log.");
            }

            long apkSizeBytes = new FileInfo(outputPath).Length;
            string apkSha256;
            using (SHA256 hasher = SHA256.Create())
            using (FileStream stream = File.OpenRead(outputPath))
                apkSha256 = BitConverter.ToString(hasher.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();

            Debug.Log(
                $"[SplatVRLab] ANDROID_BUILD_OK: {outputPath}; " +
                $"apkBytes={apkSizeBytes}; sha256={apkSha256}; " +
                $"reportTotalBytes={report.summary.totalSize}; duration={report.summary.totalTime}; " +
                $"warnings={report.summary.totalWarnings}");
        }
    }
}
