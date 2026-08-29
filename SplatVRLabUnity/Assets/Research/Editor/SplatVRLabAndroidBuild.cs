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
        private const string StationaryOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-dev.apk";
        private const string LocomotionOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-locomotion-dev.apk";

        [MenuItem("SplatVRLab/Build Android development APK")]
        public static void BuildDevelopmentApk()
        {
            BuildDevelopmentApk(
                StationaryOutputRelativePath,
                "spark_baseline",
                SplatVRLabSetup.Configure);
        }

        [MenuItem("SplatVRLab/Build Android locomotion development APK")]
        public static void BuildLocomotionDevelopmentApk()
        {
            BuildDevelopmentApk(
                LocomotionOutputRelativePath,
                "spark_locomotion_v01",
                SplatVRLabSetup.ConfigureLocomotion);
        }

        private static void BuildDevelopmentApk(
            string outputRelativePath,
            string variantId,
            Action configure)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException(
                    "Android Build Support is not installed for this Unity editor. " +
                    "Add Android Build Support, Android SDK & NDK Tools, and OpenJDK in Unity Hub.");
            }

            configure();
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
                outputRelativePath));
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
                $"variant={variantId}; " +
                $"apkBytes={apkSizeBytes}; sha256={apkSha256}; " +
                $"reportTotalBytes={report.summary.totalSize}; duration={report.summary.totalTime}; " +
                $"warnings={report.summary.totalWarnings}");
        }
    }
}
