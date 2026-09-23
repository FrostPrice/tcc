using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace SplatVRLab.Editor
{
    /// <summary>
    /// Produces isolated Windows/Meta Horizon Link diagnostics.
    /// It intentionally does not modify or replace Android build outputs.
    /// </summary>
    public static class SplatVRLabWindowsBuild
    {
        private sealed class BuildSpec
        {
            public string DiagnosticVariantId;
            public string OutputDirectoryRelativePath;
            public string ExecutableName;
            public Action Configure;
        }

        private static readonly BuildSpec Baseline = new()
        {
            DiagnosticVariantId = "desktop_horizon_link_baseline_visual_full_pose_v01",
            OutputDirectoryRelativePath =
                "Builds/Windows/SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v01",
            ExecutableName = "SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v01.exe",
            Configure = SplatVRLabSetup.ConfigureVisualBaselineFullPose,
        };

        private static readonly BuildSpec Pruned100k = new()
        {
            DiagnosticVariantId = "desktop_horizon_link_opacity_topk_100k_visual_full_pose_v01",
            OutputDirectoryRelativePath =
                "Builds/Windows/SplatVRLabUnity-horizon-link-pruned100k-fullpose-dev-v01",
            ExecutableName = "SplatVRLabUnity-horizon-link-pruned100k-fullpose-dev-v01.exe",
            Configure = SplatVRLabSetup.ConfigureVisualPrunedFullPose,
        };

        private static readonly BuildSpec Pruned50k = new()
        {
            DiagnosticVariantId = "desktop_horizon_link_opacity_topk_50k_visual_full_pose_v01",
            OutputDirectoryRelativePath =
                "Builds/Windows/SplatVRLabUnity-horizon-link-pruned50k-fullpose-dev-v01",
            ExecutableName = "SplatVRLabUnity-horizon-link-pruned50k-fullpose-dev-v01.exe",
            Configure = SplatVRLabSetup.ConfigureVisualPruned50kFullPose,
        };

        private static readonly BuildSpec SplatfactoBig = new()
        {
            DiagnosticVariantId = "desktop_horizon_link_splatfacto_big_visual_full_pose_v01",
            OutputDirectoryRelativePath =
                "Builds/Windows/SplatVRLabUnity-horizon-link-splatfacto-big-fullpose-dev-v01",
            ExecutableName = "SplatVRLabUnity-horizon-link-splatfacto-big-fullpose-dev-v01.exe",
            Configure = SplatVRLabSetup.ConfigureVisualSplatfactoBigFullPose,
        };

        [Serializable]
        private sealed class BuildRecord
        {
            public string schemaVersion = "1.0";
            public string recordType = "desktop_horizon_link_windows_build";
            public string variantId;
            public string buildTarget;
            public string graphicsApi;
            public string scriptingBackend;
            public string unityVersion;
            public string executable;
            public long executableBytes;
            public string executableSha256;
            public long reportTotalBytes;
            public double durationSeconds;
            public int warnings;
            public string createdAtUtc;
            public string interpretationLimit =
                "Desktop rendering transmitted through Meta Horizon Link; not an Android/Vulkan standalone Quest measurement.";
        }

        [MenuItem("SplatVRLab/Build Windows Horizon Link baseline visual full-pose")]
        public static void BuildHorizonLinkBaselineVisualFullPose()
        {
            BuildVisualFullPose(Baseline);
        }

        [MenuItem("SplatVRLab/Build Windows Horizon Link pruned-100k visual full-pose")]
        public static void BuildHorizonLinkPruned100kVisualFullPose()
        {
            BuildVisualFullPose(Pruned100k);
        }

        [MenuItem("SplatVRLab/Build Windows Horizon Link pruned-50k visual full-pose")]
        public static void BuildHorizonLinkPruned50kVisualFullPose()
        {
            BuildVisualFullPose(Pruned50k);
        }

        [MenuItem("SplatVRLab/Build Windows Horizon Link splatfacto-big visual full-pose")]
        public static void BuildHorizonLinkSplatfactoBigVisualFullPose()
        {
            BuildVisualFullPose(SplatfactoBig);
        }

        private static void BuildVisualFullPose(BuildSpec spec)
        {
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException(
                    "Windows Build Support is not installed for this Unity editor. " +
                    "Add Windows Build Support (IL2CPP) in Unity Hub before building.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("Cannot resolve project root.");
            string outputDirectory = Path.GetFullPath(Path.Combine(
                projectRoot, spec.OutputDirectoryRelativePath));
            if (Directory.Exists(outputDirectory) &&
                Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                throw new InvalidOperationException(
                    "The Windows Horizon Link output already exists and will not be overwritten: " +
                    outputDirectory);
            }

            // Reuses the versioned PLY, reference pose and FullPoseOnce profile.
            spec.Configure();

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException("Unity could not switch the active target to Windows x86_64.");
            }

            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12 });
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length != 1)
            {
                throw new InvalidOperationException(
                    "The Horizon Link diagnostic build requires exactly one enabled viability scene.");
            }

            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, spec.ExecutableName);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows build ended with {report.summary.result} and " +
                    $"{report.summary.totalErrors} reported errors. See the Unity Editor log.");
            }

            var executable = new FileInfo(executablePath);
            if (!executable.Exists)
            {
                throw new FileNotFoundException(
                    "Windows build reported success without executable output.", executablePath);
            }

            var record = new BuildRecord
            {
                variantId = spec.DiagnosticVariantId,
                buildTarget = BuildTarget.StandaloneWindows64.ToString(),
                graphicsApi = GraphicsDeviceType.Direct3D12.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString(),
                unityVersion = Application.unityVersion,
                executable = Path.GetRelativePath(projectRoot, executablePath),
                executableBytes = executable.Length,
                executableSha256 = Sha256(executablePath),
                reportTotalBytes = checked((long)report.summary.totalSize),
                durationSeconds = report.summary.totalTime.TotalSeconds,
                warnings = report.summary.totalWarnings,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };
            string recordPath = Path.Combine(outputDirectory, "build_record.json");
            File.WriteAllText(recordPath, JsonUtility.ToJson(record, true) + Environment.NewLine);

            Debug.Log(
                $"[SplatVRLab] WINDOWS_HORIZON_LINK_BUILD_OK: {executablePath}; " +
                $"variant={spec.DiagnosticVariantId}; executableBytes={record.executableBytes}; " +
                $"sha256={record.executableSha256}; reportTotalBytes={report.summary.totalSize}; " +
                $"duration={report.summary.totalTime}; warnings={report.summary.totalWarnings}");
        }

        private static string Sha256(string path)
        {
            using SHA256 hasher = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(hasher.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
