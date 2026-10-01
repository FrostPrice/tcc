using System;
using System.IO;
using System.Security.Cryptography;
using Gsplat;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SplatVRLab.Editor
{
    public static class SplatVRLabNativeOrbitBuild
    {
        private const string SourceScene = "Assets/Research/Scenes/GsplatViability.unity";
        private const string OrbitScene = "Assets/Research/Scenes/GsplatNativeOrbitBaseline.unity";
        private const string OutputRelative =
            "Builds/Android/SplatVRLabUnity-orbit-baseline-fullcircle-dev-v01.apk";

        private sealed class AdditionalVariant
        {
            public string RepresentationId;
            public string VariantId;
            public string AssetPath;
            public string ScenePath;
            public string OutputPath;
            public string ExpectedSha256;
            public uint ExpectedCount;
        }

        private static readonly AdditionalVariant Pruned100k = new()
        {
            RepresentationId = "opacity_topk_100k_v01",
            VariantId = "spark_opacity_topk_100k_orbit_full_circle_v01",
            AssetPath = "Assets/Research/Data/poster_opacity_topk_100k_v01.ply",
            ScenePath = "Assets/Research/Scenes/GsplatNativeOrbitPruned100k.unity",
            OutputPath = "Builds/Android/SplatVRLabUnity-orbit-pruned100k-fullcircle-dev-v01.apk",
            ExpectedSha256 = "b2af0f8f9bda2ab2cc54db3e34147b6e02ea73cd71eb682ae803739f4f34c1d3",
            ExpectedCount = 100000,
        };

        private static readonly AdditionalVariant Pruned50k = new()
        {
            RepresentationId = "opacity_topk_50k_v01",
            VariantId = "spark_opacity_topk_50k_orbit_full_circle_v01",
            AssetPath = "Assets/Research/Data/poster_opacity_topk_50k_v01.ply",
            ScenePath = "Assets/Research/Scenes/GsplatNativeOrbitPruned50k.unity",
            OutputPath = "Builds/Android/SplatVRLabUnity-orbit-pruned50k-fullcircle-dev-v01.apk",
            ExpectedSha256 = "f4a5a1f80cd2d448338c22b2b21a777e2151f0171ad42dfd74046623742b26e5",
            ExpectedCount = 50000,
        };

        private static readonly AdditionalVariant SplatfactoBig = new()
        {
            RepresentationId = "splatfacto_big_v01",
            VariantId = "spark_splatfacto_big_orbit_full_circle_v01",
            AssetPath = "Assets/Research/Data/poster_splatfacto_big_v01.ply",
            ScenePath = "Assets/Research/Scenes/GsplatNativeOrbitSplatfactoBig.unity",
            OutputPath = "Builds/Android/SplatVRLabUnity-orbit-splatfacto-big-fullcircle-dev-v01.apk",
            ExpectedSha256 = "70716105acdaa18caa3523b52c69cd8d46ab96650bbf4c6ad42a17868a651505",
            ExpectedCount = 470962,
        };

        [MenuItem("SplatVRLab/Chair orbit/Build native pruned-100k full-circle APK")]
        public static void BuildNativePruned100kFullCircleApk() => BuildAdditionalVariant(Pruned100k);

        [MenuItem("SplatVRLab/Chair orbit/Build native pruned-50k full-circle APK")]
        public static void BuildNativePruned50kFullCircleApk() => BuildAdditionalVariant(Pruned50k);

        [MenuItem("SplatVRLab/Chair orbit/Build native splatfacto-big full-circle APK")]
        public static void BuildNativeSplatfactoBigFullCircleApk() => BuildAdditionalVariant(SplatfactoBig);

        [MenuItem("SplatVRLab/Chair orbit/Build all remaining native full-circle APKs")]
        public static void BuildAllRemainingNativeFullCircleApks()
        {
            BuildAdditionalVariant(Pruned100k);
            BuildAdditionalVariant(Pruned50k);
            BuildAdditionalVariant(SplatfactoBig);
        }

        [MenuItem("SplatVRLab/Chair orbit/Build native baseline full-circle APK")]
        public static void BuildNativeBaselineFullCircleApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Android Build Support is not installed.");

            EditorSceneManager.OpenScene(SourceScene);
            SplatVRLabSetup.Validate();
            FrameMetricsRecorder sourceMetrics = UnityEngine.Object
                .FindAnyObjectByType<FrameMetricsRecorder>();
            if (!sourceMetrics || sourceMetrics.VariantId !=
                    "spark_baseline_visual_full_pose_v01" ||
                sourceMetrics.RepresentationVariantId != "baseline_v01")
                throw new InvalidOperationException("Source scene is not the full-pose baseline.");

            // Never configure or rewrite the calibrated source scene during this build.
            if (!File.Exists(OrbitScene) && !AssetDatabase.CopyAsset(SourceScene, OrbitScene))
                throw new InvalidOperationException("Could not copy the baseline scene.");
            EditorSceneManager.OpenScene(OrbitScene);

            FrameMetricsRecorder metrics = UnityEngine.Object
                .FindAnyObjectByType<FrameMetricsRecorder>();
            XrReferencePoseAligner aligner = UnityEngine.Object
                .FindAnyObjectByType<XrReferencePoseAligner>();
            ChairOrbitPivotMarker pivot = UnityEngine.Object
                .FindAnyObjectByType<ChairOrbitPivotMarker>();
            if (!metrics || !aligner || !pivot || !pivot.CalibrationConfirmed ||
                aligner.Mode != XrReferencePoseAligner.AlignmentMode.FullPoseOnce ||
                metrics.RepresentationVariantId != "baseline_v01")
                throw new InvalidOperationException("Native orbit scene lacks baseline calibration.");

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root unavailable.");
            string calibrationPath = Path.GetFullPath(Path.Combine(projectRoot, "..",
                "experiments", "unitysplats_viability_v01", "chair_orbit_calibration_v01.json"));
            if (!File.Exists(calibrationPath))
                throw new FileNotFoundException("Chair orbit calibration missing.", calibrationPath);
            string calibration = File.ReadAllText(calibrationPath);
            if (!calibration.Contains("\"operatorConfirmed\": true") ||
                !calibration.Contains(metrics.RepresentationPlySha256) ||
                !calibration.Contains(aligner.ReferencePoseId) ||
                Vector3.Distance(pivot.transform.position,
                    JsonUtility.FromJson<CalibrationPosition>(calibration)
                        .chairPivotWorldPosition) > 0.0001f)
                throw new InvalidOperationException("Orbit calibration differs from the scene.");

            Disable<AutomatedLocomotionSequence>();
            Disable<TrackedPoseCaptureMarker>();
            Disable<ReferencePoseEvaluationCapture>();
            metrics.VariantId = "spark_baseline_orbit_full_circle_v01";
            metrics.ConditionId = "orbit_full_circle_capture";
            metrics.AutomatedSequenceId = "chair_orbit_full_circle_r125_v01";
            metrics.MeasurementSeconds = 1800f;
            metrics.LocomotionEnabled = false;
            metrics.LocomotionProfileId = "automated_visual_orbit_not_locomotion";
            metrics.LocomotionMode = "orbit_visual_capture";
            metrics.MovementInput = "automated_xr_origin_pose";

            NativeChairOrbitSequence orbit = UnityEngine.Object
                .FindAnyObjectByType<NativeChairOrbitSequence>();
            if (!orbit)
                orbit = new GameObject("NativeChairOrbitSequence")
                    .AddComponent<NativeChairOrbitSequence>();
            orbit.Origin = aligner.Origin;
            orbit.Aligner = aligner;
            orbit.Metrics = metrics;
            orbit.PivotMarker = pivot;
            var orbitScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(orbitScene);
            if (!EditorSceneManager.SaveScene(orbitScene, OrbitScene))
                throw new InvalidOperationException("Native orbit scene could not be saved.");
            string savedScene = File.ReadAllText(OrbitScene);
            if (!savedScene.Contains("VariantId: spark_baseline_orbit_full_circle_v01") ||
                !savedScene.Contains("Assembly-CSharp::SplatVRLab.NativeChairOrbitSequence"))
                throw new InvalidOperationException(
                    "Saved native orbit scene does not contain its variant and sequence.");

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Unity could not switch to Android.");
            string output = Path.Combine(projectRoot, OutputRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { OrbitScene },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Native orbit build failed: " +
                    report.summary.result + "; errors=" + report.summary.totalErrors);

            using var hasher = SHA256.Create();
            using var stream = File.OpenRead(output);
            string sha = BitConverter.ToString(hasher.ComputeHash(stream))
                .Replace("-", "").ToLowerInvariant();
            Debug.Log("[SplatVRLab] NATIVE_ORBIT_BUILD_OK: " + output +
                "; variant=spark_baseline_orbit_full_circle_v01; sha256=" + sha +
                "; bytes=" + stream.Length + "; warnings=" + report.summary.totalWarnings);
        }

        private static void BuildAdditionalVariant(AdditionalVariant spec)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Android Build Support is not installed.");
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root unavailable.");
            string output = Path.Combine(projectRoot, spec.OutputPath);
            if (File.Exists(output))
                throw new InvalidOperationException("APK already exists and will not be overwritten: " + output);
            string assetFile = Path.Combine(projectRoot, spec.AssetPath);
            if (!File.Exists(assetFile) || ComputeSha256(assetFile) != spec.ExpectedSha256)
                throw new InvalidOperationException("Imported representation PLY hash mismatch: " + assetFile);

            GsplatAsset asset = AssetDatabase.LoadAssetAtPath<GsplatAsset>(spec.AssetPath)
                ?? throw new InvalidOperationException("UnitySplats asset is not imported: " + spec.AssetPath);
            if (asset.SplatCount != spec.ExpectedCount ||
                asset.Compression != CompressionMode.Spark || asset.SHBands < 3)
                throw new InvalidOperationException("Imported representation properties differ from the protocol.");
            VerifyReferencePoseCompatibility(projectRoot, spec);

            if (!File.Exists(OrbitScene))
                throw new FileNotFoundException("Validated baseline orbit scene is missing.", OrbitScene);
            if (!File.Exists(spec.ScenePath) && !AssetDatabase.CopyAsset(OrbitScene, spec.ScenePath))
                throw new InvalidOperationException("Could not copy the baseline orbit scene.");
            var scene = EditorSceneManager.OpenScene(spec.ScenePath);
            FrameMetricsRecorder metrics = UnityEngine.Object.FindAnyObjectByType<FrameMetricsRecorder>();
            NativeChairOrbitSequence orbit = UnityEngine.Object.FindAnyObjectByType<NativeChairOrbitSequence>();
            XrReferencePoseAligner aligner = UnityEngine.Object.FindAnyObjectByType<XrReferencePoseAligner>();
            ChairOrbitPivotMarker pivot = UnityEngine.Object.FindAnyObjectByType<ChairOrbitPivotMarker>();
            GsplatRenderer renderer = UnityEngine.Object.FindAnyObjectByType<GsplatRenderer>();
            if (!metrics || !orbit || !aligner || !pivot || !pivot.CalibrationConfirmed || !renderer ||
                orbit.FrameCount != 144 || !Mathf.Approximately(orbit.StepDegrees, 2.5f) ||
                !Mathf.Approximately(orbit.RadiusScale, 1.25f) ||
                aligner.Mode != XrReferencePoseAligner.AlignmentMode.FullPoseOnce ||
                metrics.ReferencePoseId != "ns_poster_test_frame_00001")
                throw new InvalidOperationException("Orbit scene does not match the validated trajectory.");
            string calibrationPath = Path.GetFullPath(Path.Combine(projectRoot, "..", "experiments",
                "unitysplats_viability_v01", "chair_orbit_calibration_v01.json"));
            if (!File.Exists(calibrationPath))
                throw new FileNotFoundException("Chair pivot calibration is missing.", calibrationPath);
            CalibrationPosition calibration = JsonUtility.FromJson<CalibrationPosition>(
                File.ReadAllText(calibrationPath));
            if (calibration == null || !calibration.operatorConfirmed ||
                Vector3.Distance(pivot.transform.position, calibration.chairPivotWorldPosition) > 0.0001f)
                throw new InvalidOperationException("Chair pivot differs from the baseline calibration.");

            renderer.GsplatAsset = asset;
            renderer.gameObject.name = "Poster_" + spec.RepresentationId + "_Gsplat";
            metrics.VariantId = spec.VariantId;
            metrics.RepresentationVariantId = spec.RepresentationId;
            metrics.RepresentationGaussianCount = (int)spec.ExpectedCount;
            metrics.RepresentationPlySha256 = spec.ExpectedSha256;
            metrics.RepresentationImportCompression = asset.Compression.ToString();
            metrics.RendererShDegree = renderer.SHDegree;
            orbit.VariantId = spec.VariantId;
            orbit.RepresentationVariantId = spec.RepresentationId;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, spec.ScenePath))
                throw new InvalidOperationException("Variant orbit scene could not be saved.");
            string saved = File.ReadAllText(spec.ScenePath);
            if (!saved.Contains("VariantId: " + spec.VariantId) ||
                !saved.Contains("RepresentationPlySha256: " + spec.ExpectedSha256))
                throw new InvalidOperationException("Saved orbit scene lacks the expected provenance.");

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Unity could not switch to Android.");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { spec.ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Native orbit build failed: " +
                    report.summary.result + "; errors=" + report.summary.totalErrors);
            Debug.Log("[SplatVRLab] NATIVE_ORBIT_BUILD_OK: " + output +
                "; variant=" + spec.VariantId + "; representation=" + spec.RepresentationId +
                "; sha256=" + ComputeSha256(output) + "; bytes=" + new FileInfo(output).Length +
                "; warnings=" + report.summary.totalWarnings);
        }

        private static void VerifyReferencePoseCompatibility(string projectRoot, AdditionalVariant spec)
        {
            if (spec != SplatfactoBig)
                return;
            string repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            string baselinePath = Path.Combine(repositoryRoot, "experiments",
                "unitysplats_viability_v01", "reference_pose.json");
            string bigPath = Path.Combine(repositoryRoot, "experiments", "colab", "records",
                "exp_ns_poster_splatfacto_big_v01", "integration", "reference_pose.json");
            if (!File.Exists(baselinePath) || !File.Exists(bigPath))
                throw new FileNotFoundException("Reference pose record is missing.");
            ReferencePoseRecord baseline = JsonUtility.FromJson<ReferencePoseRecord>(
                File.ReadAllText(baselinePath));
            ReferencePoseRecord big = JsonUtility.FromJson<ReferencePoseRecord>(
                File.ReadAllText(bigPath));
            ReferencePosePlacement a = NerfstudioReferencePose.ComputePlacement(baseline);
            ReferencePosePlacement b = NerfstudioReferencePose.ComputePlacement(big);
            if (baseline.reference_pose_id != big.reference_pose_id ||
                baseline.selection.frame_file_path != big.selection.frame_file_path ||
                big.source.gaussian_splat_sha256 != spec.ExpectedSha256 ||
                Vector3.Distance(a.ModelPosition, b.ModelPosition) > 0.0001f ||
                Quaternion.Angle(a.ModelRotation, b.ModelRotation) > 0.01f ||
                Mathf.Abs(a.ModelScale - b.ModelScale) > 0.0001f ||
                Vector3.Distance(a.ReferenceCameraPosition, b.ReferenceCameraPosition) > 0.0001f ||
                Quaternion.Angle(a.ReferenceCameraRotation, b.ReferenceCameraRotation) > 0.01f)
                throw new InvalidOperationException(
                    "Splatfacto-big reference placement differs; the calibrated orbit cannot be reused.");
        }

        private static string ComputeSha256(string path)
        {
            using var hasher = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(hasher.ComputeHash(stream))
                .Replace("-", "").ToLowerInvariant();
        }

        [Serializable]
        private sealed class CalibrationPosition
        {
            public Vector3 chairPivotWorldPosition;
            public bool operatorConfirmed;
        }

        private static void Disable<T>() where T : MonoBehaviour
        {
            T component = UnityEngine.Object.FindAnyObjectByType<T>();
            if (component)
                component.enabled = false;
        }
    }
}
