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
        private const string AutomatedWalkOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-automated-walk-dev.apk";
        private const string AutomatedStaticOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-automated-static-dev.apk";
        private const string PrunedAutomatedStaticOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-pruned-100k-static-dev.apk";
        private const string VisualBaselineOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-baseline-dev.apk";
        private const string VisualPrunedOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-pruned100k-dev.apk";
        private const string VisualBaselineFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-baseline-fullpose-dev.apk";
        private const string VisualPrunedFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-pruned100k-fullpose-dev.apk";
        private const string VisualPruned50kFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-pruned50k-fullpose-dev.apk";
        private const string VisualSplatfactoBigFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-splatfacto-big-fullpose-dev.apk";
        private const string VisualBaselineGammaLinearOffFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-baseline-gamma-linear-off-fullpose-dev.apk";
        private const string VisualBaselineSh0FullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-baseline-sh0-fullpose-dev.apk";
        private const string VisualBaselineUncompressedFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-baseline-uncompressed-fullpose-dev.apk";
        private const string VisualBaselineMonoscopicOutputFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-baseline-monoscopic-output-fullpose-dev.apk";
        private const string VisualBaselinePerRendererSortFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-baseline-per-renderer-sort-fullpose-dev.apk";
        private const string VisualPrunedPerRendererSortFullPoseOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-visual-pruned100k-per-renderer-sort-fullpose-dev.apk";
        private const string AutomatedSnapTurnOutputRelativePath =
            "Builds/Android/SplatVRLabUnity-automated-snapturn-dev.apk";

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

        [MenuItem("SplatVRLab/Build Android automated continuous-walk metrics APK")]
        public static void BuildAutomatedContinuousWalkDevelopmentApk()
        {
            BuildDevelopmentApk(
                AutomatedWalkOutputRelativePath,
                "spark_automated_continuous_walk_v01",
                SplatVRLabSetup.ConfigureAutomatedContinuousWalk);
        }

        [MenuItem("SplatVRLab/Build Android automated stationary-control metrics APK")]
        public static void BuildAutomatedStaticReferenceDevelopmentApk()
        {
            BuildDevelopmentApk(
                AutomatedStaticOutputRelativePath,
                "spark_automated_static_reference_v01",
                SplatVRLabSetup.ConfigureAutomatedStaticReference);
        }

        [MenuItem("SplatVRLab/Build Android pruned-100k stationary metrics APK")]
        public static void BuildPrunedAutomatedStaticReferenceDevelopmentApk()
        {
            BuildDevelopmentApk(
                PrunedAutomatedStaticOutputRelativePath,
                "spark_opacity_topk_100k_automated_static_v01",
                SplatVRLabSetup.ConfigurePrunedAutomatedStaticReference);
        }

        [MenuItem("SplatVRLab/Build Android baseline visual-reference APK")]
        public static void BuildVisualBaselineDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualBaselineOutputRelativePath,
                "spark_baseline_visual_reference_v01",
                SplatVRLabSetup.ConfigureVisualBaseline);
        }

        [MenuItem("SplatVRLab/Build Android pruned-100k visual-reference APK")]
        public static void BuildVisualPrunedDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualPrunedOutputRelativePath,
                "spark_opacity_topk_100k_visual_reference_v01",
                SplatVRLabSetup.ConfigureVisualPruned);
        }

        [MenuItem("SplatVRLab/Build Android baseline visual full-pose APK")]
        public static void BuildVisualBaselineFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualBaselineFullPoseOutputRelativePath,
                "spark_baseline_visual_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualBaselineFullPose);
        }

        [MenuItem("SplatVRLab/Build Android pruned-100k visual full-pose APK")]
        public static void BuildVisualPrunedFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualPrunedFullPoseOutputRelativePath,
                "spark_opacity_topk_100k_visual_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualPrunedFullPose);
        }

        [MenuItem("SplatVRLab/Build Android pruned-50k visual full-pose APK")]
        public static void BuildVisualPruned50kFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualPruned50kFullPoseOutputRelativePath,
                "spark_opacity_topk_50k_visual_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualPruned50kFullPose);
        }

        [MenuItem("SplatVRLab/Build Android splatfacto-big visual full-pose APK")]
        public static void BuildVisualSplatfactoBigFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualSplatfactoBigFullPoseOutputRelativePath,
                "spark_splatfacto_big_visual_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualSplatfactoBigFullPose);
        }

        [MenuItem("SplatVRLab/Build Android baseline visual gamma-linear-off full-pose APK")]
        public static void BuildVisualBaselineGammaLinearOffFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualBaselineGammaLinearOffFullPoseOutputRelativePath,
                "spark_baseline_visual_gamma_linear_off_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualBaselineGammaLinearOffFullPose);
        }

        [MenuItem("SplatVRLab/Build Android baseline visual SH0 full-pose APK")]
        public static void BuildVisualBaselineSh0FullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualBaselineSh0FullPoseOutputRelativePath,
                "spark_baseline_visual_sh0_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualBaselineSh0FullPose);
        }

        [MenuItem("SplatVRLab/Build Android baseline uncompressed visual full-pose APK")]
        public static void BuildVisualBaselineUncompressedFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualBaselineUncompressedFullPoseOutputRelativePath,
                "uncompressed_baseline_visual_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualBaselineUncompressedFullPose);
        }

        [MenuItem("SplatVRLab/Build Android baseline monoscopic-output visual full-pose APK")]
        public static void BuildVisualBaselineMonoscopicOutputFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualBaselineMonoscopicOutputFullPoseOutputRelativePath,
                "spark_baseline_monoscopic_output_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualBaselineMonoscopicOutputFullPose);
        }

        [MenuItem("SplatVRLab/Build Android baseline per-renderer-sort visual full-pose APK")]
        public static void BuildVisualBaselinePerRendererSortFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualBaselinePerRendererSortFullPoseOutputRelativePath,
                "spark_baseline_per_renderer_sort_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualBaselinePerRendererSortFullPose);
        }

        [MenuItem("SplatVRLab/Build Android pruned-100k per-renderer-sort visual full-pose APK")]
        public static void BuildVisualPrunedPerRendererSortFullPoseDevelopmentApk()
        {
            BuildDevelopmentApk(
                VisualPrunedPerRendererSortFullPoseOutputRelativePath,
                "spark_opacity_topk_100k_per_renderer_sort_full_pose_v01",
                SplatVRLabSetup.ConfigureVisualPrunedPerRendererSortFullPose);
        }

        [MenuItem("SplatVRLab/Build Android automated snap-turn metrics APK")]
        public static void BuildAutomatedSnapTurnDevelopmentApk()
        {
            BuildDevelopmentApk(
                AutomatedSnapTurnOutputRelativePath,
                "spark_automated_snap_turn_v01",
                SplatVRLabSetup.ConfigureAutomatedSnapTurn);
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
