using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
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
        private const string NativeOrbitScene =
            "Assets/Research/Scenes/GsplatNativeOrbitBaseline.unity";
        private const string WindowsOrbitScene =
            "Assets/Research/Scenes/GsplatWindowsOrbitBaseline.unity";
        private const string WindowsOrbitVariant =
            "desktop_horizon_link_baseline_orbit_full_circle_v01";
        private const string WindowsOrbitOutputDirectory =
            "Builds/Windows/SplatVRLabUnity-horizon-link-orbit-baseline-fullcircle-dev-v01";
        private const string WindowsOrbitExecutable =
            "SplatVRLabUnity-horizon-link-orbit-baseline-fullcircle-dev-v01.exe";

        private sealed class OrbitBuildSpec
        {
            public string NativeScene;
            public string WindowsScene;
            public string VariantId;
            public string NativeVariantId;
            public string RepresentationId;
            public string PlySha256;
            public int GaussianCount;
            public string OutputDirectory;
            public string Executable;
        }

        private static readonly OrbitBuildSpec[] AdditionalOrbits =
        {
            new()
            {
                NativeScene = "Assets/Research/Scenes/GsplatNativeOrbitPruned100k.unity",
                WindowsScene = "Assets/Research/Scenes/GsplatWindowsOrbitPruned100k.unity",
                VariantId = "desktop_horizon_link_opacity_topk_100k_orbit_full_circle_v01",
                NativeVariantId = "spark_opacity_topk_100k_orbit_full_circle_v01",
                RepresentationId = "opacity_topk_100k_v01",
                PlySha256 = "b2af0f8f9bda2ab2cc54db3e34147b6e02ea73cd71eb682ae803739f4f34c1d3",
                GaussianCount = 100000,
                OutputDirectory = "Builds/Windows/SplatVRLabUnity-horizon-link-orbit-pruned100k-fullcircle-dev-v01",
                Executable = "SplatVRLabUnity-horizon-link-orbit-pruned100k-fullcircle-dev-v01.exe",
            },
            new()
            {
                NativeScene = "Assets/Research/Scenes/GsplatNativeOrbitPruned50k.unity",
                WindowsScene = "Assets/Research/Scenes/GsplatWindowsOrbitPruned50k.unity",
                VariantId = "desktop_horizon_link_opacity_topk_50k_orbit_full_circle_v01",
                NativeVariantId = "spark_opacity_topk_50k_orbit_full_circle_v01",
                RepresentationId = "opacity_topk_50k_v01",
                PlySha256 = "f4a5a1f80cd2d448338c22b2b21a777e2151f0171ad42dfd74046623742b26e5",
                GaussianCount = 50000,
                OutputDirectory = "Builds/Windows/SplatVRLabUnity-horizon-link-orbit-pruned50k-fullcircle-dev-v01",
                Executable = "SplatVRLabUnity-horizon-link-orbit-pruned50k-fullcircle-dev-v01.exe",
            },
            new()
            {
                NativeScene = "Assets/Research/Scenes/GsplatNativeOrbitSplatfactoBig.unity",
                WindowsScene = "Assets/Research/Scenes/GsplatWindowsOrbitSplatfactoBig.unity",
                VariantId = "desktop_horizon_link_splatfacto_big_orbit_full_circle_v01",
                NativeVariantId = "spark_splatfacto_big_orbit_full_circle_v01",
                RepresentationId = "splatfacto_big_v01",
                PlySha256 = "70716105acdaa18caa3523b52c69cd8d46ab96650bbf4c6ad42a17868a651505",
                GaussianCount = 470962,
                OutputDirectory = "Builds/Windows/SplatVRLabUnity-horizon-link-orbit-splatfacto-big-fullcircle-dev-v01",
                Executable = "SplatVRLabUnity-horizon-link-orbit-splatfacto-big-fullcircle-dev-v01.exe",
            },
        };

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
                "Builds/Windows/SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v02",
            ExecutableName = "SplatVRLabUnity-horizon-link-baseline-fullpose-dev-v02.exe",
            Configure = SplatVRLabSetup.ConfigureVisualBaselineFullPose,
        };

        private static readonly BuildSpec Pruned100k = new()
        {
            DiagnosticVariantId = "desktop_horizon_link_opacity_topk_100k_visual_full_pose_v01",
            OutputDirectoryRelativePath =
                "Builds/Windows/SplatVRLabUnity-horizon-link-pruned100k-fullpose-dev-v02",
            ExecutableName = "SplatVRLabUnity-horizon-link-pruned100k-fullpose-dev-v02.exe",
            Configure = SplatVRLabSetup.ConfigureVisualPrunedFullPose,
        };

        private static readonly BuildSpec Pruned50k = new()
        {
            DiagnosticVariantId = "desktop_horizon_link_opacity_topk_50k_visual_full_pose_v01",
            OutputDirectoryRelativePath =
                "Builds/Windows/SplatVRLabUnity-horizon-link-pruned50k-fullpose-dev-v02",
            ExecutableName = "SplatVRLabUnity-horizon-link-pruned50k-fullpose-dev-v02.exe",
            Configure = SplatVRLabSetup.ConfigureVisualPruned50kFullPose,
        };

        private static readonly BuildSpec SplatfactoBig = new()
        {
            DiagnosticVariantId = "desktop_horizon_link_splatfacto_big_visual_full_pose_v01",
            OutputDirectoryRelativePath =
                "Builds/Windows/SplatVRLabUnity-horizon-link-splatfacto-big-fullpose-dev-v02",
            ExecutableName = "SplatVRLabUnity-horizon-link-splatfacto-big-fullpose-dev-v02.exe",
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

        [MenuItem("SplatVRLab/Chair orbit/Build Windows Horizon Link baseline full-circle EXE")]
        public static void BuildHorizonLinkBaselineFullCircleOrbit()
        {
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows Build Support (IL2CPP) is not installed.");

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Cannot resolve project root.");
            string outputDirectory = Path.Combine(projectRoot, WindowsOrbitOutputDirectory);
            if (Directory.Exists(outputDirectory) &&
                Directory.EnumerateFileSystemEntries(outputDirectory).Any())
                throw new InvalidOperationException(
                    "Windows orbit output already exists and will not be overwritten: " +
                    outputDirectory);
            PrepareHorizonLinkBaselineFullCircleOrbitScene();

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Unity could not switch to Windows x86_64.");
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,
                ScriptingImplementation.IL2CPP);

            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, WindowsOrbitExecutable);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { WindowsOrbitScene },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows orbit build failed: " +
                    report.summary.result + "; errors=" + report.summary.totalErrors);

            var executable = new FileInfo(executablePath);
            var record = new BuildRecord
            {
                variantId = WindowsOrbitVariant,
                buildTarget = BuildTarget.StandaloneWindows64.ToString(),
                graphicsApi = GraphicsDeviceType.Direct3D12.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(
                    NamedBuildTarget.Standalone).ToString(),
                unityVersion = Application.unityVersion,
                executable = Path.GetRelativePath(projectRoot, executablePath),
                executableBytes = executable.Length,
                executableSha256 = Sha256(executablePath),
                reportTotalBytes = checked((long)report.summary.totalSize),
                durationSeconds = report.summary.totalTime.TotalSeconds,
                warnings = report.summary.totalWarnings,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };
            File.WriteAllText(Path.Combine(outputDirectory, "build_record.json"),
                JsonUtility.ToJson(record, true) + Environment.NewLine);
            Debug.Log("[SplatVRLab] WINDOWS_ORBIT_BUILD_OK: " + executablePath +
                "; variant=" + WindowsOrbitVariant + "; sha256=" +
                record.executableSha256 + "; warnings=" + report.summary.totalWarnings);
        }

        [MenuItem("SplatVRLab/Chair orbit/Build Windows Horizon Link pruned-100k full-circle EXE")]
        public static void BuildHorizonLinkPruned100kFullCircleOrbit() =>
            BuildAdditionalOrbit(AdditionalOrbits[0]);

        [MenuItem("SplatVRLab/Chair orbit/Build Windows Horizon Link pruned-50k full-circle EXE")]
        public static void BuildHorizonLinkPruned50kFullCircleOrbit() =>
            BuildAdditionalOrbit(AdditionalOrbits[1]);

        [MenuItem("SplatVRLab/Chair orbit/Build Windows Horizon Link splatfacto-big full-circle EXE")]
        public static void BuildHorizonLinkSplatfactoBigFullCircleOrbit() =>
            BuildAdditionalOrbit(AdditionalOrbits[2]);

        private static void BuildAdditionalOrbit(OrbitBuildSpec spec)
        {
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows Build Support (IL2CPP) is not installed.");

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Cannot resolve project root.");
            string outputDirectory = Path.Combine(projectRoot, spec.OutputDirectory);
            if (Directory.Exists(outputDirectory) &&
                Directory.EnumerateFileSystemEntries(outputDirectory).Any())
                throw new InvalidOperationException(
                    "Windows orbit output already exists and will not be overwritten: " + outputDirectory);
            if (!File.Exists(spec.NativeScene))
                throw new FileNotFoundException("Validated native orbit scene is missing.", spec.NativeScene);
            if (!File.Exists(spec.WindowsScene) &&
                !AssetDatabase.CopyAsset(spec.NativeScene, spec.WindowsScene))
                throw new InvalidOperationException("Could not copy the native orbit scene.");

            var scene = EditorSceneManager.OpenScene(spec.WindowsScene);
            FrameMetricsRecorder metrics = UnityEngine.Object.FindAnyObjectByType<FrameMetricsRecorder>();
            NativeChairOrbitSequence orbit = UnityEngine.Object.FindAnyObjectByType<NativeChairOrbitSequence>();
            XrReferencePoseAligner aligner = UnityEngine.Object.FindAnyObjectByType<XrReferencePoseAligner>();
            ChairOrbitPivotMarker pivot = UnityEngine.Object.FindAnyObjectByType<ChairOrbitPivotMarker>();
            if (!metrics || !orbit || !aligner || !pivot || !pivot.CalibrationConfirmed ||
                metrics.RepresentationVariantId != spec.RepresentationId ||
                metrics.RepresentationGaussianCount != spec.GaussianCount ||
                metrics.RepresentationPlySha256 != spec.PlySha256 ||
                orbit.RepresentationVariantId != spec.RepresentationId ||
                orbit.FrameCount != 144 || !Mathf.Approximately(orbit.StepDegrees, 2.5f) ||
                !Mathf.Approximately(orbit.RadiusScale, 1.25f) ||
                aligner.Mode != XrReferencePoseAligner.AlignmentMode.FullPoseOnce ||
                (metrics.VariantId != spec.NativeVariantId && metrics.VariantId != spec.VariantId) ||
                (orbit.VariantId != spec.NativeVariantId && orbit.VariantId != spec.VariantId))
                throw new InvalidOperationException(
                    "Windows orbit scene does not match the validated native variant.");

            metrics.VariantId = spec.VariantId;
            orbit.VariantId = spec.VariantId;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, spec.WindowsScene))
                throw new InvalidOperationException("Windows orbit scene could not be saved.");
            string saved = File.ReadAllText(spec.WindowsScene);
            if (!saved.Contains("VariantId: " + spec.VariantId) ||
                !saved.Contains("RepresentationPlySha256: " + spec.PlySha256))
                throw new InvalidOperationException("Saved scene lacks the expected provenance.");

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Unity could not switch to Windows x86_64.");
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,
                ScriptingImplementation.IL2CPP);
            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, spec.Executable);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { spec.WindowsScene },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows orbit build failed: " +
                    report.summary.result + "; errors=" + report.summary.totalErrors);

            var executable = new FileInfo(executablePath);
            var record = new BuildRecord
            {
                variantId = spec.VariantId,
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
            File.WriteAllText(Path.Combine(outputDirectory, "build_record.json"),
                JsonUtility.ToJson(record, true) + Environment.NewLine);
            Debug.Log("[SplatVRLab] WINDOWS_ORBIT_BUILD_OK: " + executablePath +
                "; variant=" + spec.VariantId + "; sha256=" + record.executableSha256);
        }

        [MenuItem("SplatVRLab/Chair orbit/Prepare Windows Horizon Link baseline full-circle scene")]
        public static void PrepareHorizonLinkBaselineFullCircleOrbitScene()
        {
            if (!File.Exists(NativeOrbitScene))
                throw new FileNotFoundException(
                    "Validated native orbit scene is missing.", NativeOrbitScene);
            if (!File.Exists(WindowsOrbitScene) &&
                !AssetDatabase.CopyAsset(NativeOrbitScene, WindowsOrbitScene))
                throw new InvalidOperationException("Could not copy the native orbit scene.");

            var scene = EditorSceneManager.OpenScene(WindowsOrbitScene);
            FrameMetricsRecorder metrics = UnityEngine.Object
                .FindAnyObjectByType<FrameMetricsRecorder>();
            NativeChairOrbitSequence orbit = UnityEngine.Object
                .FindAnyObjectByType<NativeChairOrbitSequence>();
            XrReferencePoseAligner aligner = UnityEngine.Object
                .FindAnyObjectByType<XrReferencePoseAligner>();
            ChairOrbitPivotMarker pivot = UnityEngine.Object
                .FindAnyObjectByType<ChairOrbitPivotMarker>();
            if (!metrics || !orbit || !aligner || !pivot ||
                !pivot.CalibrationConfirmed ||
                metrics.RepresentationVariantId != "baseline_v01" ||
                orbit.RepresentationVariantId != "baseline_v01" ||
                metrics.RepresentationPlySha256 !=
                    "23e3b3d3cd47e1aa0ad1daf7df96c4bd620af9edaf7996ccb865f5b60b80bebb" ||
                orbit.FrameCount != 144 ||
                !Mathf.Approximately(orbit.StepDegrees, 2.5f) ||
                !Mathf.Approximately(orbit.RadiusScale, 1.25f) ||
                aligner.Mode != XrReferencePoseAligner.AlignmentMode.FullPoseOnce)
                throw new InvalidOperationException(
                    "Windows orbit scene does not match the validated native baseline trajectory.");

            metrics.VariantId = WindowsOrbitVariant;
            orbit.VariantId = WindowsOrbitVariant;
            metrics.ConditionId = "orbit_full_circle_capture";
            metrics.AutomatedSequenceId = "chair_orbit_full_circle_r125_v01";
            metrics.MeasurementSeconds = 1800f;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, WindowsOrbitScene) ||
                !File.ReadAllText(WindowsOrbitScene).Contains(
                    "VariantId: " + WindowsOrbitVariant))
                throw new InvalidOperationException("Windows orbit scene was not saved correctly.");
            Debug.Log("[SplatVRLab] WINDOWS_ORBIT_SCENE_READY: " + WindowsOrbitScene);
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
