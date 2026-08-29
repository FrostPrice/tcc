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
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace SplatVRLab.Editor
{
    public static class SplatVRLabSetup
    {
        private const string SourcePlyRelativeToRepository =
            "experiments/baseline_v01/exp_ns_poster_baseline_v01_baseline_v01.ply";
        private const string ReferencePoseRelativeToRepository =
            "experiments/unitysplats_viability_v01/reference_pose.json";
        private const string LocomotionProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/locomotion_profile.json";
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
        private const string ExpectedLocomotionProfileSha256 =
            "7233b2fc9c092052fcf7a70dc8646f55aac068c910834fd686f7beb8ec9f6e41";
        private const string StationaryVariantId = "spark_baseline";
        private const string ExpectedLocomotionProfileId =
            "quest_continuous_move_snap_turn_v01";
        private const string ExpectedLocomotionVariantId = "spark_locomotion_v01";
        private const string ControllerInputActionManagerTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager";

        [Serializable]
        private sealed class LocomotionProfile
        {
            public string schema_version;
            public string profile_id;
            public string variant_id;
            public MovementSettings movement;
            public TurnSettings turn;
            public ProviderSettings providers;
            public string collision_policy;
        }

        [Serializable]
        private sealed class MovementSettings
        {
            public string mode;
            public string input;
            public string direction_reference;
            public float speed_unity_units_per_second;
            public bool strafe_enabled;
            public bool fly_enabled;
        }

        [Serializable]
        private sealed class TurnSettings
        {
            public string mode;
            public string input;
            public float angle_degrees;
            public float debounce_seconds;
            public bool turn_around_enabled;
        }

        [Serializable]
        private sealed class ProviderSettings
        {
            public bool gravity_enabled;
            public bool teleportation_enabled;
            public bool climb_enabled;
            public bool grab_move_enabled;
            public bool jump_enabled;
        }

        [MenuItem("SplatVRLab/Configure viability harness")]
        public static void Configure()
        {
            ConfigureVariant(null);
        }

        [MenuItem("SplatVRLab/Configure locomotion variant")]
        public static void ConfigureLocomotion()
        {
            ConfigureVariant(LoadLocomotionProfile());
        }

        private static void ConfigureVariant(LocomotionProfile locomotionProfile)
        {
            ConfigureProjectSettings();
            EnsureGsplatRendererFeatures();
            GsplatAsset baseline = ImportBaseline();
            ReferencePoseRecord referencePose = LoadReferencePose();
            CreateViabilityScene(baseline, referencePose, locomotionProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();

            string variantId = locomotionProfile?.variant_id ?? StationaryVariantId;
            Debug.Log(
                $"[SplatVRLab] SETUP_OK: Unity {Application.unityVersion}; " +
                $"{baseline.SplatCount} splats; pose={referencePose.reference_pose_id}; " +
                $"scalePolicy={referencePose.scale.policy}; variant={variantId}; " +
                $"scene={ViabilitySceneAssetPath}");
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
            if (metrics.ExperimentId != "unitysplats_viability_v01" ||
                metrics.SceneId != "poster_baseline" ||
                metrics.ReferencePoseId != referencePose.reference_pose_id ||
                metrics.ReferenceFrame != referencePose.selection.frame_file_path ||
                metrics.ScalePolicy != referencePose.scale.policy ||
                metrics.MetricScaleCalibrated != referencePose.scale.metric_calibrated ||
                Mathf.Abs(
                    metrics.MetersPerNerfstudioUnit -
                    referencePose.scale.meters_per_nerfstudio_unit) > 1e-6f)
                throw new InvalidOperationException("Frame metrics provenance does not match the reference pose.");

            if (metrics.VariantId == StationaryVariantId)
                ValidateStationaryConfiguration(scene, metrics);
            else if (metrics.VariantId == ExpectedLocomotionVariantId)
                ValidateLocomotionConfiguration(scene, metrics, LoadLocomotionProfile());
            else
                throw new InvalidOperationException($"Unknown harness variant: {metrics.VariantId}.");

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
                $"variant={metrics.VariantId}; locomotion={metrics.LocomotionMode}; " +
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

        private static LocomotionProfile LoadLocomotionProfile()
        {
            string path = ResolveRepositoryPath(LocomotionProfileRelativeToRepository);
            if (!File.Exists(path))
                throw new FileNotFoundException("Locomotion profile is missing.", path);
            if (!string.Equals(
                    ComputeSha256(path),
                    ExpectedLocomotionProfileSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Locomotion profile checksum differs from the recorded configuration.");

            LocomotionProfile profile = JsonUtility.FromJson<LocomotionProfile>(
                File.ReadAllText(path));
            if (profile == null || profile.movement == null || profile.turn == null ||
                profile.providers == null)
                throw new InvalidOperationException("Locomotion profile is incomplete.");
            if (profile.schema_version != "1.0" ||
                profile.profile_id != ExpectedLocomotionProfileId ||
                profile.variant_id != ExpectedLocomotionVariantId ||
                profile.movement.mode != "continuous" ||
                profile.movement.input != "left_thumbstick" ||
                profile.movement.direction_reference != "head_relative" ||
                profile.movement.speed_unity_units_per_second <= 0f ||
                !profile.movement.strafe_enabled || profile.movement.fly_enabled ||
                profile.turn.mode != "snap" ||
                profile.turn.input != "right_thumbstick" ||
                profile.turn.angle_degrees <= 0f || profile.turn.angle_degrees > 90f ||
                profile.turn.debounce_seconds < 0f || profile.turn.turn_around_enabled ||
                profile.providers.gravity_enabled ||
                profile.providers.teleportation_enabled ||
                profile.providers.climb_enabled ||
                profile.providers.grab_move_enabled ||
                profile.providers.jump_enabled ||
                profile.collision_policy != "no_scene_colliders_free_horizontal_motion")
                throw new InvalidOperationException(
                    "Locomotion profile differs from the supported controlled variant.");

            return profile;
        }

        private static void CreateViabilityScene(
            GsplatAsset baseline,
            ReferencePoseRecord referencePose,
            LocomotionProfile locomotionProfile)
        {
            Scene scene = EditorSceneManager.OpenScene(SourceSceneAssetPath, OpenSceneMode.Single);
            DestroyRootIfPresent(scene, "Plane");
            DestroyRootIfPresent(scene, "Directional Light");
            GameObject xrRoot = FindRequiredRoot(scene, "XR Origin (XR Rig)");
            if (locomotionProfile == null)
                ConfigureStationaryRig(scene);
            else
                ConfigureLocomotionRig(scene, locomotionProfile);

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
            metrics.VariantId = locomotionProfile?.variant_id ?? StationaryVariantId;
            metrics.ReferencePoseId = referencePose.reference_pose_id;
            metrics.ReferenceFrame = referencePose.selection.frame_file_path;
            metrics.ScalePolicy = referencePose.scale.policy;
            metrics.MetricScaleCalibrated = referencePose.scale.metric_calibrated;
            metrics.MetersPerNerfstudioUnit = referencePose.scale.meters_per_nerfstudio_unit;
            metrics.LocomotionEnabled = locomotionProfile != null;
            metrics.LocomotionProfileId =
                locomotionProfile?.profile_id ?? "stationary_baseline";
            metrics.LocomotionMode = locomotionProfile?.movement.mode ?? "stationary";
            metrics.MovementInput = locomotionProfile?.movement.input ?? "none";
            metrics.MoveSpeedUnityUnitsPerSecond =
                locomotionProfile?.movement.speed_unity_units_per_second ?? 0f;
            metrics.TurnMode = locomotionProfile?.turn.mode ?? "none";
            metrics.TurnInput = locomotionProfile?.turn.input ?? "none";
            metrics.SnapTurnDegrees = locomotionProfile?.turn.angle_degrees ?? 0f;
            metrics.GravityEnabled = locomotionProfile?.providers.gravity_enabled ?? false;
            metrics.CollisionPolicy =
                locomotionProfile?.collision_policy ?? "not_applicable_stationary";
            metrics.WarmupFrames = 180;
            metrics.MeasurementSeconds = 30f;

            Directory.CreateDirectory(Path.GetDirectoryName(ViabilitySceneAssetPath)!);
            EditorSceneManager.SaveScene(scene, ViabilitySceneAssetPath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ViabilitySceneAssetPath, enabled: true),
            };
        }

        private static void ConfigureStationaryRig(Scene scene)
        {
            FindRequiredChild(scene, "XR Origin (XR Rig)", "Locomotion").SetActive(false);
        }

        private static void ConfigureLocomotionRig(
            Scene scene,
            LocomotionProfile profile)
        {
            const string rootName = "XR Origin (XR Rig)";
            GameObject xrRoot = FindRequiredRoot(scene, rootName);
            GameObject locomotion = FindRequiredChild(scene, rootName, "Locomotion");
            GameObject moveObject = FindRequiredChild(scene, rootName, "Locomotion/Move");
            GameObject turnObject = FindRequiredChild(scene, rootName, "Locomotion/Turn");
            GameObject gravityObject = FindRequiredChild(scene, rootName, "Locomotion/Gravity");

            ContinuousMoveProvider moveProvider =
                moveObject.GetComponent<ContinuousMoveProvider>() ??
                throw new InvalidOperationException("Continuous Move Provider is missing.");
            GravityProvider gravityProvider = gravityObject.GetComponent<GravityProvider>() ??
                                              throw new InvalidOperationException(
                                                  "Gravity Provider is missing.");
            SnapTurnProvider snapTurnProvider = turnObject.GetComponent<SnapTurnProvider>() ??
                                                  throw new InvalidOperationException(
                                                      "Snap Turn Provider is missing.");
            ContinuousTurnProvider continuousTurnProvider =
                turnObject.GetComponent<ContinuousTurnProvider>() ??
                throw new InvalidOperationException("Continuous Turn Provider is missing.");

            moveProvider.moveSpeed = profile.movement.speed_unity_units_per_second;
            moveProvider.enableStrafe = profile.movement.strafe_enabled;
            moveProvider.enableFly = profile.movement.fly_enabled;
            moveProvider.leftHandMoveInput.inputSourceMode =
                XRInputValueReader.InputSourceMode.InputActionReference;
            moveProvider.rightHandMoveInput.inputSourceMode =
                XRInputValueReader.InputSourceMode.Unused;
            SetLegacyMoveGravity(moveProvider, enabled: false);

            gravityProvider.useGravity = profile.providers.gravity_enabled;
            snapTurnProvider.enabled = true;
            snapTurnProvider.turnAmount = profile.turn.angle_degrees;
            snapTurnProvider.debounceTime = profile.turn.debounce_seconds;
            snapTurnProvider.delayTime = 0f;
            snapTurnProvider.enableTurnLeftRight = true;
            snapTurnProvider.enableTurnAround = profile.turn.turn_around_enabled;
            snapTurnProvider.leftHandTurnInput.inputSourceMode =
                XRInputValueReader.InputSourceMode.Unused;
            snapTurnProvider.rightHandTurnInput.inputSourceMode =
                XRInputValueReader.InputSourceMode.InputActionReference;
            continuousTurnProvider.enabled = false;

            moveObject.SetActive(true);
            turnObject.SetActive(true);
            gravityObject.SetActive(profile.providers.gravity_enabled);
            FindRequiredChild(scene, rootName, "Locomotion/Teleportation")
                .SetActive(profile.providers.teleportation_enabled);
            FindRequiredChild(scene, rootName, "Locomotion/Climb")
                .SetActive(profile.providers.climb_enabled);
            FindRequiredChild(scene, rootName, "Locomotion/Grab Move")
                .SetActive(profile.providers.grab_move_enabled);
            FindRequiredChild(scene, rootName, "Locomotion/Jump")
                .SetActive(profile.providers.jump_enabled);

            MonoBehaviour[] controllerManagers = FindControllerInputActionManagers(xrRoot);
            if (controllerManagers.Length != 2)
                throw new InvalidOperationException(
                    $"Expected two controller input managers; found {controllerManagers.Length}.");
            foreach (MonoBehaviour manager in controllerManagers)
                manager.enabled = false;

            InputActionManager inputActionManager =
                xrRoot.GetComponentInChildren<InputActionManager>(includeInactive: true) ??
                throw new InvalidOperationException("XR Input Action Manager is missing.");
            inputActionManager.enabled = true;

            GameObject[] teleportInteractors =
                FindDescendantsByName(xrRoot, "Teleport Interactor");
            if (teleportInteractors.Length != 2)
                throw new InvalidOperationException(
                    $"Expected two teleport interactors; found {teleportInteractors.Length}.");
            foreach (GameObject teleportInteractor in teleportInteractors)
                teleportInteractor.SetActive(false);

            locomotion.SetActive(true);
            EditorUtility.SetDirty(moveProvider);
            EditorUtility.SetDirty(gravityProvider);
            EditorUtility.SetDirty(snapTurnProvider);
            EditorUtility.SetDirty(continuousTurnProvider);
        }

        private static void ValidateStationaryConfiguration(
            Scene scene,
            FrameMetricsRecorder metrics)
        {
            if (metrics.LocomotionEnabled ||
                metrics.LocomotionProfileId != "stationary_baseline" ||
                metrics.LocomotionMode != "stationary" ||
                metrics.MovementInput != "none" ||
                metrics.MoveSpeedUnityUnitsPerSecond != 0f ||
                metrics.TurnMode != "none" || metrics.TurnInput != "none" ||
                metrics.SnapTurnDegrees != 0f || metrics.GravityEnabled ||
                metrics.CollisionPolicy != "not_applicable_stationary")
                throw new InvalidOperationException(
                    "Stationary metric provenance is inconsistent.");
            if (FindRequiredChild(scene, "XR Origin (XR Rig)", "Locomotion").activeSelf)
                throw new InvalidOperationException(
                    "Locomotion must be disabled for the stationary baseline.");
        }

        private static void ValidateLocomotionConfiguration(
            Scene scene,
            FrameMetricsRecorder metrics,
            LocomotionProfile profile)
        {
            const string rootName = "XR Origin (XR Rig)";
            GameObject xrRoot = FindRequiredRoot(scene, rootName);
            GameObject locomotion = FindRequiredChild(scene, rootName, "Locomotion");
            GameObject moveObject = FindRequiredChild(scene, rootName, "Locomotion/Move");
            GameObject turnObject = FindRequiredChild(scene, rootName, "Locomotion/Turn");
            GameObject gravityObject = FindRequiredChild(scene, rootName, "Locomotion/Gravity");

            if (!metrics.LocomotionEnabled ||
                metrics.LocomotionProfileId != profile.profile_id ||
                metrics.LocomotionMode != profile.movement.mode ||
                metrics.MovementInput != profile.movement.input ||
                Mathf.Abs(
                    metrics.MoveSpeedUnityUnitsPerSecond -
                    profile.movement.speed_unity_units_per_second) > 1e-6f ||
                metrics.TurnMode != profile.turn.mode ||
                metrics.TurnInput != profile.turn.input ||
                Mathf.Abs(metrics.SnapTurnDegrees - profile.turn.angle_degrees) > 1e-6f ||
                metrics.GravityEnabled != profile.providers.gravity_enabled ||
                metrics.CollisionPolicy != profile.collision_policy)
                throw new InvalidOperationException(
                    "Locomotion metric provenance does not match the profile.");

            if (!locomotion.activeSelf || !moveObject.activeSelf || !turnObject.activeSelf ||
                gravityObject.activeSelf ||
                FindRequiredChild(scene, rootName, "Locomotion/Teleportation").activeSelf ||
                FindRequiredChild(scene, rootName, "Locomotion/Climb").activeSelf ||
                FindRequiredChild(scene, rootName, "Locomotion/Grab Move").activeSelf ||
                FindRequiredChild(scene, rootName, "Locomotion/Jump").activeSelf)
                throw new InvalidOperationException(
                    "The controlled locomotion provider activation state is inconsistent.");

            ContinuousMoveProvider moveProvider =
                moveObject.GetComponent<ContinuousMoveProvider>() ??
                throw new InvalidOperationException("Continuous Move Provider is missing.");
            GravityProvider gravityProvider = gravityObject.GetComponent<GravityProvider>() ??
                                              throw new InvalidOperationException(
                                                  "Gravity Provider is missing.");
            SnapTurnProvider snapTurnProvider = turnObject.GetComponent<SnapTurnProvider>() ??
                                                  throw new InvalidOperationException(
                                                      "Snap Turn Provider is missing.");
            ContinuousTurnProvider continuousTurnProvider =
                turnObject.GetComponent<ContinuousTurnProvider>() ??
                throw new InvalidOperationException("Continuous Turn Provider is missing.");

            if (Mathf.Abs(moveProvider.moveSpeed - profile.movement.speed_unity_units_per_second) >
                    1e-6f ||
                moveProvider.enableStrafe != profile.movement.strafe_enabled ||
                moveProvider.enableFly != profile.movement.fly_enabled ||
                moveProvider.leftHandMoveInput.inputSourceMode !=
                    XRInputValueReader.InputSourceMode.InputActionReference ||
                moveProvider.rightHandMoveInput.inputSourceMode !=
                    XRInputValueReader.InputSourceMode.Unused ||
                ReadLegacyMoveGravity(moveProvider) || gravityProvider.useGravity ||
                !snapTurnProvider.enabled || continuousTurnProvider.enabled ||
                Mathf.Abs(snapTurnProvider.turnAmount - profile.turn.angle_degrees) > 1e-6f ||
                Mathf.Abs(snapTurnProvider.debounceTime - profile.turn.debounce_seconds) > 1e-6f ||
                snapTurnProvider.enableTurnAround ||
                snapTurnProvider.leftHandTurnInput.inputSourceMode !=
                    XRInputValueReader.InputSourceMode.Unused ||
                snapTurnProvider.rightHandTurnInput.inputSourceMode !=
                    XRInputValueReader.InputSourceMode.InputActionReference)
                throw new InvalidOperationException(
                    "The controlled locomotion provider parameters are inconsistent.");

            MonoBehaviour[] controllerManagers = FindControllerInputActionManagers(xrRoot);
            if (controllerManagers.Length != 2 || controllerManagers.Any(manager => manager.enabled))
                throw new InvalidOperationException(
                    "Controller input managers must be disabled for split-stick locomotion.");
            InputActionManager inputActionManager =
                xrRoot.GetComponentInChildren<InputActionManager>(includeInactive: true) ??
                throw new InvalidOperationException("XR Input Action Manager is missing.");
            if (!inputActionManager.enabled)
                throw new InvalidOperationException("XR Input Action Manager must remain enabled.");
            GameObject[] teleportInteractors =
                FindDescendantsByName(xrRoot, "Teleport Interactor");
            if (teleportInteractors.Length != 2 ||
                teleportInteractors.Any(interactor => interactor.activeSelf))
                throw new InvalidOperationException(
                    "Teleport interactors must remain disabled in the locomotion variant.");
        }

        private static void SetLegacyMoveGravity(
            ContinuousMoveProvider provider,
            bool enabled)
        {
            var serializedProvider = new SerializedObject(provider);
            SerializedProperty useGravity = serializedProvider.FindProperty("m_UseGravity") ??
                                            throw new InvalidOperationException(
                                                "Continuous Move Provider gravity field is missing.");
            useGravity.boolValue = enabled;
            serializedProvider.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ReadLegacyMoveGravity(ContinuousMoveProvider provider)
        {
            var serializedProvider = new SerializedObject(provider);
            SerializedProperty useGravity = serializedProvider.FindProperty("m_UseGravity") ??
                                            throw new InvalidOperationException(
                                                "Continuous Move Provider gravity field is missing.");
            return useGravity.boolValue;
        }

        private static MonoBehaviour[] FindControllerInputActionManagers(GameObject xrRoot) =>
            xrRoot.GetComponentsInChildren<MonoBehaviour>(includeInactive: true)
                .Where(component => component &&
                                    component.GetType().FullName ==
                                    ControllerInputActionManagerTypeName)
                .ToArray();

        private static GameObject[] FindDescendantsByName(GameObject root, string objectName) =>
            root.GetComponentsInChildren<Transform>(includeInactive: true)
                .Where(transform => transform != root.transform && transform.name == objectName)
                .Select(transform => transform.gameObject)
                .ToArray();

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
