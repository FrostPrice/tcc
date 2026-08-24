using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Gsplat;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace SplatVRLab.Editor
{
    public static class SplatVRLabSetup
    {
        private const string SourcePlyRelativeToRepository =
            "experiments/baseline_v01/exp_ns_poster_baseline_v01_baseline_v01.ply";
        private const string ReferencePoseRelativeToRepository =
            "experiments/unitysplats_viability_v01/reference_pose.json";
        private const string ImportedPlyAssetPath =
            "Assets/Research/Data/poster_baseline_v01.ply";
        private const string SourceSceneAssetPath = "Assets/Scenes/BasicScene.unity";
        private const string ViabilitySceneAssetPath =
            "Assets/Research/Scenes/GsplatViability.unity";
        private const uint ExpectedSplatCount = 195760;
        private const string ExpectedSceneId = "ns_poster";
        private const string ExpectedExperimentId = "exp_ns_poster_baseline_v01";
        private const string ExpectedVariantId = "baseline_v01";
        private const string ExpectedSplatSha256 =
            "23e3b3d3cd47e1aa0ad1daf7df96c4bd620af9edaf7996ccb865f5b60b80bebb";

        [MenuItem("SplatVRLab/Configure viability harness")]
        public static void Configure()
        {
            ConfigureProjectSettings();
            EnsureGsplatRendererFeatures();
            GsplatAsset baseline = ImportBaseline();
            ReferencePoseRecord referencePose = LoadReferencePose();
            CreateViabilityScene(baseline, referencePose);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();

            Debug.Log(
                $"[SplatVRLab] SETUP_OK: Unity {Application.unityVersion}; " +
                $"{baseline.SplatCount} splats; pose={referencePose.reference_pose_id}; " +
                $"scalePolicy={referencePose.scale.policy}; scene {ViabilitySceneAssetPath}");
        }

        [MenuItem("SplatVRLab/Validate viability harness")]
        public static void Validate()
        {
            GsplatAsset baseline = AssetDatabase.LoadAssetAtPath<GsplatAsset>(ImportedPlyAssetPath);
            if (!baseline)
                throw new InvalidOperationException($"Missing imported baseline: {ImportedPlyAssetPath}");
            if (baseline.SplatCount != ExpectedSplatCount)
            {
                throw new InvalidOperationException(
                    $"Unexpected splat count: {baseline.SplatCount}; expected {ExpectedSplatCount}.");
            }

            foreach (UniversalRendererData rendererData in FindProjectRendererData())
            {
                bool hasFeature = rendererData.rendererFeatures.Any(
                    feature => feature && feature.GetType().FullName == "Gsplat.GsplatURPFeature");
                if (!hasFeature)
                    throw new InvalidOperationException($"Gsplat URP Feature missing in {rendererData.name}.");
            }

            if (!File.Exists(ViabilitySceneAssetPath))
                throw new InvalidOperationException($"Missing viability scene: {ViabilitySceneAssetPath}");

            ReferencePoseRecord referencePose = LoadReferencePose();
            ReferencePosePlacement expectedPlacement =
                NerfstudioReferencePose.ComputePlacement(referencePose);
            Scene scene = EditorSceneManager.OpenScene(ViabilitySceneAssetPath, OpenSceneMode.Single);
            GsplatRenderer renderer = UnityEngine.Object.FindAnyObjectByType<GsplatRenderer>();
            if (!renderer || renderer.GsplatAsset != baseline)
                throw new InvalidOperationException("The viability scene does not reference the baseline splat.");
            if (Vector3.Distance(renderer.transform.position, expectedPlacement.ModelPosition) > 1e-4f ||
                Quaternion.Angle(renderer.transform.rotation, expectedPlacement.ModelRotation) > 0.01f ||
                Vector3.Distance(
                    renderer.transform.localScale,
                    Vector3.one * expectedPlacement.ModelScale) > 1e-5f)
                throw new InvalidOperationException("The splat transform does not match the reference pose.");
            FrameMetricsRecorder metrics =
                UnityEngine.Object.FindAnyObjectByType<FrameMetricsRecorder>();
            if (!metrics)
                throw new InvalidOperationException("The viability scene is missing FrameMetricsRecorder.");
            if (metrics.ReferencePoseId != referencePose.reference_pose_id ||
                metrics.ReferenceFrame != referencePose.selection.frame_file_path ||
                metrics.ScalePolicy != referencePose.scale.policy ||
                metrics.MetricScaleCalibrated != referencePose.scale.metric_calibrated ||
                Mathf.Abs(
                    metrics.MetersPerNerfstudioUnit -
                    referencePose.scale.meters_per_nerfstudio_unit) > 1e-6f)
                throw new InvalidOperationException("Frame metrics provenance does not match the reference pose.");
            if (FindRequiredChild(scene, "XR Origin (XR Rig)", "Locomotion").activeSelf)
                throw new InvalidOperationException("Locomotion must be disabled for the stationary baseline.");

            XrReferencePoseAligner aligner =
                UnityEngine.Object.FindAnyObjectByType<XrReferencePoseAligner>();
            Vector3 expectedHorizontalForward =
                new(
                    referencePose.unity.canonical_horizontal_forward[0],
                    referencePose.unity.canonical_horizontal_forward[1],
                    referencePose.unity.canonical_horizontal_forward[2]);
            if (!aligner || !aligner.Origin ||
                aligner.ReferencePoseId != referencePose.reference_pose_id ||
                aligner.ReferenceFrame != referencePose.selection.frame_file_path ||
                aligner.ScalePolicy != referencePose.scale.policy ||
                aligner.MetricScaleCalibrated != referencePose.scale.metric_calibrated ||
                Mathf.Abs(
                    aligner.MetersPerNerfstudioUnit -
                    referencePose.scale.meters_per_nerfstudio_unit) > 1e-6f ||
                Vector3.Distance(
                    aligner.TargetCameraPosition,
                    expectedPlacement.ReferenceCameraPosition) > 1e-5f ||
                Vector3.Angle(
                    aligner.TargetHorizontalForward,
                    expectedHorizontalForward) > 0.001f ||
                Quaternion.Angle(
                    aligner.ExactReferenceCameraRotation,
                    expectedPlacement.ReferenceCameraRotation) > 0.01f)
                throw new InvalidOperationException("XR reference-pose alignment is missing or inconsistent.");

            bool buildSceneConfigured = EditorBuildSettings.scenes.Length == 1 &&
                                        EditorBuildSettings.scenes[0].enabled &&
                                        EditorBuildSettings.scenes[0].path == ViabilitySceneAssetPath;
            if (!buildSceneConfigured)
                throw new InvalidOperationException("The viability scene is not the sole enabled build scene.");

            bool androidModuleInstalled = BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log(
                $"[SplatVRLab] VALIDATION_OK: splats={baseline.SplatCount}; " +
                $"shBands={baseline.SHBands}; pose={referencePose.reference_pose_id}; " +
                $"frame={referencePose.selection.frame_file_path}; " +
                $"metersPerNerfstudioUnit={referencePose.scale.meters_per_nerfstudio_unit:F6}; " +
                $"metricScaleCalibrated={referencePose.scale.metric_calibrated}; " +
                $"AndroidModuleInstalled={androidModuleInstalled}");
        }

        private static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "UNIVALI";
            PlayerSettings.productName = "SplatVRLab";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "br.edu.univali.splatvrlab");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        }

        private static void EnsureGsplatRendererFeatures()
        {
            Type featureType = typeof(GsplatRenderer).Assembly.GetType(
                "Gsplat.GsplatURPFeature", throwOnError: true);

            UniversalRendererData[] rendererDataAssets = FindProjectRendererData();
            if (rendererDataAssets.Length == 0)
                throw new InvalidOperationException("No project UniversalRendererData assets were found.");

            foreach (UniversalRendererData rendererData in rendererDataAssets)
            {
                if (rendererData.rendererFeatures.Any(
                        feature => feature && feature.GetType() == featureType))
                    continue;

                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(featureType);
                feature.name = "Gsplat URP Feature";
                feature.SetActive(true);
                feature.Create();
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var serializedData = new SerializedObject(rendererData);
                SerializedProperty features = serializedData.FindProperty("m_RendererFeatures");
                SerializedProperty featureMap = serializedData.FindProperty("m_RendererFeatureMap");
                int index = features.arraySize;
                features.InsertArrayElementAtIndex(index);
                features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
                featureMap.InsertArrayElementAtIndex(index);
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
                serializedData.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
                rendererData.SetDirty();
            }
        }

        private static UniversalRendererData[] FindProjectRendererData()
        {
            return AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UniversalRendererData>)
                .Where(asset => asset)
                .OrderBy(asset => asset.name, StringComparer.Ordinal)
                .ToArray();
        }

        private static GsplatAsset ImportBaseline()
        {
            string projectRoot = ResolveProjectRoot();
            string sourcePath = ResolveRepositoryPath(SourcePlyRelativeToRepository);
            string destinationPath = Path.Combine(projectRoot, ImportedPlyAssetPath);

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Baseline PLY is missing.", sourcePath);
            if (!string.Equals(
                    ComputeSha256(sourcePath), ExpectedSplatSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Baseline PLY checksum differs from the recorded artifact.");

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var sourceInfo = new FileInfo(sourcePath);
            var destinationInfo = new FileInfo(destinationPath);
            if (!destinationInfo.Exists || destinationInfo.Length != sourceInfo.Length)
                File.Copy(sourcePath, destinationPath, overwrite: true);

            AssetDatabase.ImportAsset(
                ImportedPlyAssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            AssetImporter importer = AssetImporter.GetAtPath(ImportedPlyAssetPath);
            var serializedImporter = new SerializedObject(importer);
            SerializedProperty compression = serializedImporter.FindProperty("Compression");
            SerializedProperty coordinates = serializedImporter.FindProperty("SourceCoordinates");
            bool importerChanged = compression.enumValueIndex != (int)Gsplat.CompressionMode.Spark ||
                                   coordinates.enumValueIndex != (int)SourceCoordinates.RUB;
            if (importerChanged)
            {
                compression.enumValueIndex = (int)Gsplat.CompressionMode.Spark;
                coordinates.enumValueIndex = (int)SourceCoordinates.RUB;
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<GsplatAsset>(ImportedPlyAssetPath)
                   ?? throw new InvalidOperationException("UnitySplats did not create a GsplatAsset.");
        }

        internal static ReferencePoseRecord LoadReferencePose()
        {
            string path = ResolveRepositoryPath(ReferencePoseRelativeToRepository);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Reference pose is missing. Export or recover it before configuring Unity.", path);

            return NerfstudioReferencePose.ParseAndValidate(
                File.ReadAllText(path),
                ExpectedSceneId,
                ExpectedExperimentId,
                ExpectedVariantId,
                ExpectedSplatSha256);
        }

        private static void CreateViabilityScene(
            GsplatAsset baseline,
            ReferencePoseRecord referencePose)
        {
            Scene scene = EditorSceneManager.OpenScene(SourceSceneAssetPath, OpenSceneMode.Single);
            DestroyRootIfPresent(scene, "Plane");
            DestroyRootIfPresent(scene, "Directional Light");
            GameObject xrRoot = FindRequiredRoot(scene, "XR Origin (XR Rig)");
            FindRequiredChild(scene, "XR Origin (XR Rig)", "Locomotion").SetActive(false);

            GameObject splatObject = GameObject.Find("PosterBaseline_Gsplat") ??
                                     new GameObject("PosterBaseline_Gsplat");
            ReferencePosePlacement placement =
                NerfstudioReferencePose.ComputePlacement(referencePose);
            splatObject.transform.SetPositionAndRotation(
                placement.ModelPosition, placement.ModelRotation);
            splatObject.transform.localScale = Vector3.one * placement.ModelScale;
            GsplatRenderer renderer = splatObject.GetComponent<GsplatRenderer>() ??
                                      splatObject.AddComponent<GsplatRenderer>();
            renderer.GsplatAsset = baseline;
            renderer.SHDegree = Mathf.Clamp(3, 0, baseline.SHBands);
            renderer.Brightness = 1f;
            renderer.SplatDownscaleFactor = 0f;
            renderer.GammaToLinear = true;
            renderer.AsyncUpload = false;

            XROrigin origin = xrRoot.GetComponent<XROrigin>() ??
                              throw new InvalidOperationException("XR Origin component is missing.");
            XrReferencePoseAligner aligner =
                xrRoot.GetComponent<XrReferencePoseAligner>() ??
                xrRoot.AddComponent<XrReferencePoseAligner>();
            aligner.ReferencePoseId = referencePose.reference_pose_id;
            aligner.ReferenceFrame = referencePose.selection.frame_file_path;
            aligner.ScalePolicy = referencePose.scale.policy;
            aligner.MetricScaleCalibrated = referencePose.scale.metric_calibrated;
            aligner.MetersPerNerfstudioUnit = referencePose.scale.meters_per_nerfstudio_unit;
            aligner.Origin = origin;
            aligner.TargetCameraPosition = placement.ReferenceCameraPosition;
            aligner.TargetUp = Vector3.up;
            aligner.TargetHorizontalForward =
                new Vector3(
                    referencePose.unity.canonical_horizontal_forward[0],
                    referencePose.unity.canonical_horizontal_forward[1],
                    referencePose.unity.canonical_horizontal_forward[2]).normalized;
            aligner.ExactReferenceCameraRotation = placement.ReferenceCameraRotation;
            aligner.TrackingWaitSeconds = 30f;

            GameObject metricsObject = GameObject.Find("ExperimentMetrics") ??
                                       new GameObject("ExperimentMetrics");
            FrameMetricsRecorder metrics = metricsObject.GetComponent<FrameMetricsRecorder>() ??
                                           metricsObject.AddComponent<FrameMetricsRecorder>();
            metrics.ExperimentId = "unitysplats_viability_v01";
            metrics.SceneId = "poster_baseline";
            metrics.VariantId = "spark_baseline";
            metrics.ReferencePoseId = referencePose.reference_pose_id;
            metrics.ReferenceFrame = referencePose.selection.frame_file_path;
            metrics.ScalePolicy = referencePose.scale.policy;
            metrics.MetricScaleCalibrated = referencePose.scale.metric_calibrated;
            metrics.MetersPerNerfstudioUnit = referencePose.scale.meters_per_nerfstudio_unit;
            metrics.WarmupFrames = 180;
            metrics.MeasurementSeconds = 30f;

            Directory.CreateDirectory(Path.GetDirectoryName(ViabilitySceneAssetPath)!);
            EditorSceneManager.SaveScene(scene, ViabilitySceneAssetPath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ViabilitySceneAssetPath, enabled: true),
            };
        }

        private static void DestroyRootIfPresent(Scene scene, string objectName)
        {
            GameObject root = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == objectName);
            if (root)
                UnityEngine.Object.DestroyImmediate(root);
        }

        private static GameObject FindRequiredChild(Scene scene, string rootName, string childPath)
        {
            GameObject root = FindRequiredRoot(scene, rootName);
            Transform child = root.transform.Find(childPath);
            if (!child)
            {
                throw new InvalidOperationException(
                    $"Required child object '{rootName}/{childPath}' is missing from {scene.path}.");
            }

            return child.gameObject;
        }

        private static GameObject FindRequiredRoot(Scene scene, string rootName) =>
            scene.GetRootGameObjects()
                .SingleOrDefault(candidate => candidate.name == rootName)
            ?? throw new InvalidOperationException(
                $"Required root object '{rootName}' is missing from {scene.path}.");

        private static string ResolveProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Cannot resolve Unity project root.");

        private static string ResolveRepositoryPath(string relativePath)
        {
            string repositoryRoot = Directory.GetParent(ResolveProjectRoot())?.FullName
                                    ?? throw new InvalidOperationException("Cannot resolve repository root.");
            return Path.Combine(repositoryRoot, relativePath);
        }

        private static string ComputeSha256(string path)
        {
            using SHA256 hasher = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(hasher.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
