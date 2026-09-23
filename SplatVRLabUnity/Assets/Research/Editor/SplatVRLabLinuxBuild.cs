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
    /// Produces a Linux/OpenXR diagnostic build for the WiVRn contingency path.
    /// It intentionally remains separate from all Android APK build commands.
    /// </summary>
    public static class SplatVRLabLinuxBuild
    {
        private const string VariantId = "desktop_wivrn_baseline_visual_full_pose_v01";
        private const string OutputDirectoryRelativePath =
            "Builds/Linux/SplatVRLabUnity-wivrn-baseline-fullpose-dev-v01";
        private const string ExecutableName =
            "SplatVRLabUnity-wivrn-baseline-fullpose-dev-v01.x86_64";

        [Serializable]
        private sealed class BuildRecord
        {
            public string schemaVersion = "1.0";
            public string recordType = "desktop_wivrn_linux_build";
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
                "Desktop rendering streamed through WiVRn; not an Android/Vulkan standalone Quest measurement.";
        }

        [MenuItem("SplatVRLab/Build Linux WiVRn baseline visual full-pose")]
        public static void BuildWiVRnBaselineVisualFullPose()
        {
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                throw new InvalidOperationException(
                    "Linux Build Support is not installed for this Unity editor. " +
                    "Add Linux Build Support in Unity Hub before building.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("Cannot resolve project root.");
            string outputDirectory = Path.GetFullPath(Path.Combine(
                projectRoot, OutputDirectoryRelativePath));
            if (Directory.Exists(outputDirectory) &&
                Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                throw new InvalidOperationException(
                    "The Linux WiVRn v01 output already exists and will not be overwritten: " +
                    outputDirectory);
            }

            // Reuses the versioned baseline PLY, reference pose and FullPoseOnce profile.
            // This routine changes Android-only settings while constructing the scene, then
            // selects the independent Linux target before the player is built.
            SplatVRLabSetup.ConfigureVisualBaselineFullPose();

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                throw new InvalidOperationException("Unity could not switch the active target to Linux x86_64.");
            }

            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneLinux64, new[] { GraphicsDeviceType.Vulkan });
            // Linux IL2CPP support is not installed in the validated local editor.
            // Mono keeps this contingency build runnable; the selected backend is
            // persisted in build_record.json and must not be compared to Android IL2CPP.
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length != 1)
                throw new InvalidOperationException(
                    "The WiVRn diagnostic build requires exactly one enabled viability scene.");

            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, ExecutableName);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executablePath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Linux build ended with {report.summary.result} and " +
                    $"{report.summary.totalErrors} reported errors. See the Unity Editor log.");
            }

            var executable = new FileInfo(executablePath);
            if (!executable.Exists)
                throw new FileNotFoundException("Linux build reported success without executable output.", executablePath);

            var record = new BuildRecord
            {
                variantId = VariantId,
                buildTarget = BuildTarget.StandaloneLinux64.ToString(),
                graphicsApi = GraphicsDeviceType.Vulkan.ToString(),
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
                $"[SplatVRLab] LINUX_WIVRN_BUILD_OK: {executablePath}; " +
                $"variant={VariantId}; executableBytes={record.executableBytes}; " +
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
