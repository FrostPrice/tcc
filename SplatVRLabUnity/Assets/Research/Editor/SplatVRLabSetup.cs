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
        private const string PrunedPlyRelativeToRepository =
            "experiments/pruning_v01/exp_ns_poster_opacity_topk_100k_v01.ply";
        private const string Pruned50kPlyRelativeToRepository =
            "experiments/pruning_50k_v01/exp_ns_poster_opacity_topk_50k_v01.ply";
        private const string ReferencePoseRelativeToRepository =
            "experiments/unitysplats_viability_v01/reference_pose.json";
        private const string LocomotionProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/locomotion_profile.json";
        private const string AutomatedWalkProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/automated_continuous_walk_profile.json";
        private const string AutomatedStaticProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/automated_static_reference_profile.json";
        private const string PrunedAutomatedStaticProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/automated_static_pruned_profile.json";
        private const string VisualBaselineProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_baseline_profile.json";
        private const string VisualPrunedProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_pruned100k_profile.json";
        private const string VisualBaselineFullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_baseline_full_pose_profile.json";
        private const string VisualPrunedFullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_pruned100k_full_pose_profile.json";
        private const string VisualPruned50kFullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_pruned50k_full_pose_profile.json";
        private const string VisualBaselineGammaLinearOffFullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_baseline_gamma_linear_off_full_pose_profile.json";
        private const string VisualBaselineSh0FullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_baseline_sh0_full_pose_profile.json";
        private const string VisualBaselineUncompressedFullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_baseline_uncompressed_full_pose_profile.json";
        private const string VisualBaselineMonoscopicOutputFullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_baseline_monoscopic_output_full_pose_profile.json";
        private const string VisualBaselinePerRendererSortFullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_baseline_per_renderer_sort_full_pose_profile.json";
        private const string VisualPrunedPerRendererSortFullPoseProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/visual_pruned100k_per_renderer_sort_full_pose_profile.json";
        private const string AutomatedSnapTurnProfileRelativeToRepository =
            "experiments/unitysplats_viability_v01/automated_snap_turn_profile.json";
        private const string ImportedPlyAssetPath =
            "Assets/Research/Data/poster_baseline_v01.ply";
        private const string ImportedPrunedPlyAssetPath =
            "Assets/Research/Data/poster_opacity_topk_100k_v01.ply";
        private const string ImportedPruned50kPlyAssetPath =
            "Assets/Research/Data/poster_opacity_topk_50k_v01.ply";
        private const string SourceSceneAssetPath = "Assets/Scenes/BasicScene.unity";
        private const string ViabilitySceneAssetPath =
            "Assets/Research/Scenes/GsplatViability.unity";
        private const uint ExpectedSplatCount = 195760;
        private const uint ExpectedPrunedSplatCount = 100000;
        private const uint ExpectedPruned50kSplatCount = 50000;
        private const string ExpectedSceneId = "ns_poster";
        private const string ExpectedExperimentId = "exp_ns_poster_baseline_v01";
        private const string ExpectedVariantId = "baseline_v01";
        private const string ExpectedSplatSha256 =
            "23e3b3d3cd47e1aa0ad1daf7df96c4bd620af9edaf7996ccb865f5b60b80bebb";
        private const string ExpectedPrunedSplatSha256 =
            "b2af0f8f9bda2ab2cc54db3e34147b6e02ea73cd71eb682ae803739f4f34c1d3";
        private const string ExpectedPruned50kSplatSha256 =
            "f4a5a1f80cd2d448338c22b2b21a777e2151f0171ad42dfd74046623742b26e5";
        private const string ExpectedLocomotionProfileSha256 =
            "7233b2fc9c092052fcf7a70dc8646f55aac068c910834fd686f7beb8ec9f6e41";
        private const string ExpectedAutomatedWalkProfileSha256 =
            "279a9cc47c0828d2836573d7159c41be68825397a8d7063d6cf3680ec3803b9f";
        private const string ExpectedAutomatedStaticProfileSha256 =
            "cb58fd1aee6fcee8146fe53751f7fd9713cb00915d6e9aa6075cc07afc086ea6";
        private const string ExpectedPrunedAutomatedStaticProfileSha256 =
            "7f7f1dd174e40e05a1df820f8c0caec19bb34af00e7891e2737056515e10b87b";
        private const string ExpectedVisualBaselineProfileSha256 =
            "e2ec9073dc6d2b8d248acab04ec552b0f4531cf938ae98a0bf42e7a97a2447d0";
        private const string ExpectedVisualPrunedProfileSha256 =
            "25c7b5618ff88253557437f6cad08a58e77fd88fbd5d8a8acc9152fcc688ad00";
        private const string ExpectedVisualBaselineFullPoseProfileSha256 =
            "2c38a5e8822b4f08b46e18a895e08802ccbf90e336589ea4d115e5018252fbb4";
        private const string ExpectedVisualPrunedFullPoseProfileSha256 =
            "4194128708cd761815a881bfc5ec2faf983c3555d518a37538945db92252f2d4";
        private const string ExpectedVisualPruned50kFullPoseProfileSha256 =
            "f9381a035e54d6556f998702b26c3b46e92f7acbdaac94884e5566e940b64664";
        private const string ExpectedVisualBaselineGammaLinearOffFullPoseProfileSha256 =
            "4eb4a67f8b2d6204f984dc1e438894dc6bfa301b8911eb676016e5cb6fa4b7ee";
        private const string ExpectedVisualBaselineSh0FullPoseProfileSha256 =
            "a5a66ea5590eeb90aa035241ae434efc265203c3a18297535490b4efe7f61dce";
        private const string ExpectedVisualBaselineUncompressedFullPoseProfileSha256 =
            "c9fa9660ce3e688a8efe6789618964510a802437d7822606f72e665e85b2d468";
        private const string ExpectedVisualBaselineMonoscopicOutputFullPoseProfileSha256 =
            "ed9abd42243daad00597cb570c99a7199c475c6a1172aca2a3d0dfdd468ef361";
        private const string ExpectedVisualBaselinePerRendererSortFullPoseProfileSha256 =
            "53ddebfba96eddc1a40bedac85b3c96d506b6477fe66fde1dd9ef5e82c1227be";
        private const string ExpectedVisualPrunedPerRendererSortFullPoseProfileSha256 =
            "e73716380cfc92717b6d1d477d73f3edb2c1d8c8409ae1a9f292e06da12774b3";
        private const string ExpectedAutomatedSnapTurnProfileSha256 =
            "31a6ab51ff51d8a7966744f49276297fadbc770c9fcee94b039631f82372c47b";
        private const string StationaryVariantId = "spark_baseline";
        private const string ExpectedLocomotionProfileId =
            "quest_continuous_move_snap_turn_v01";
        private const string ExpectedLocomotionVariantId = "spark_locomotion_v01";
        private const string AutomatedWalkVariantId = "spark_automated_continuous_walk_v01";
        private const string AutomatedStaticVariantId = "spark_automated_static_reference_v01";
        private const string PrunedAutomatedStaticVariantId =
            "spark_opacity_topk_100k_automated_static_v01";
        private const string VisualBaselineVariantId = "spark_baseline_visual_reference_v01";
        private const string VisualPrunedVariantId = "spark_opacity_topk_100k_visual_reference_v01";
        private const string VisualBaselineFullPoseVariantId =
            "spark_baseline_visual_full_pose_v01";
        private const string VisualPrunedFullPoseVariantId =
            "spark_opacity_topk_100k_visual_full_pose_v01";
        private const string VisualPruned50kFullPoseVariantId =
            "spark_opacity_topk_50k_visual_full_pose_v01";
        private const string VisualBaselineGammaLinearOffFullPoseVariantId =
            "spark_baseline_visual_gamma_linear_off_full_pose_v01";
        private const string VisualBaselineSh0FullPoseVariantId =
            "spark_baseline_visual_sh0_full_pose_v01";
        private const string VisualBaselineUncompressedFullPoseVariantId =
            "uncompressed_baseline_visual_full_pose_v01";
        private const string VisualBaselineMonoscopicOutputFullPoseVariantId =
            "spark_baseline_monoscopic_output_full_pose_v01";
        private const string VisualBaselinePerRendererSortFullPoseVariantId =
            "spark_baseline_per_renderer_sort_full_pose_v01";
        private const string VisualPrunedPerRendererSortFullPoseVariantId =
            "spark_opacity_topk_100k_per_renderer_sort_full_pose_v01";
        private const string AutomatedSnapTurnVariantId = "spark_automated_snap_turn_v01";
        private const string ControllerInputActionManagerTypeName =
            "UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager";

        private sealed class RepresentationSpec
        {
            public string VariantId;
            public string SourceRelativePath;
            public string ImportedAssetPath;
            public string Sha256;
            public uint SplatCount;
            public CompressionMode Compression;
        }

        private static readonly RepresentationSpec BaselineRepresentation = new()
        {
            VariantId = "baseline_v01",
            SourceRelativePath = SourcePlyRelativeToRepository,
            ImportedAssetPath = ImportedPlyAssetPath,
            Sha256 = ExpectedSplatSha256,
            SplatCount = ExpectedSplatCount,
            Compression = CompressionMode.Spark,
        };

        private static readonly RepresentationSpec PrunedRepresentation = new()
        {
            VariantId = "opacity_topk_100k_v01",
            SourceRelativePath = PrunedPlyRelativeToRepository,
            ImportedAssetPath = ImportedPrunedPlyAssetPath,
            Sha256 = ExpectedPrunedSplatSha256,
            SplatCount = ExpectedPrunedSplatCount,
            Compression = CompressionMode.Spark,
        };

        private static readonly RepresentationSpec Pruned50kRepresentation = new()
        {
            VariantId = "opacity_topk_50k_v01",
            SourceRelativePath = Pruned50kPlyRelativeToRepository,
            ImportedAssetPath = ImportedPruned50kPlyAssetPath,
            Sha256 = ExpectedPruned50kSplatSha256,
            SplatCount = ExpectedPruned50kSplatCount,
            Compression = CompressionMode.Spark,
        };

        private static readonly RepresentationSpec BaselineUncompressedRepresentation = new()
        {
            VariantId = "baseline_uncompressed_import_v01",
            SourceRelativePath = SourcePlyRelativeToRepository,
            ImportedAssetPath = ImportedPlyAssetPath,
            Sha256 = ExpectedSplatSha256,
            SplatCount = ExpectedSplatCount,
            Compression = CompressionMode.Uncompressed,
        };

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
            public AutomationSettings automation;
            public string xr_alignment_mode;
            public RendererSettings renderer;
        }

        [Serializable]
        private sealed class RendererSettings
        {
            public bool gamma_to_linear = true;
            public int sh_degree = 3;
            public float brightness = 1f;
            public float splat_downscale_factor;
            public bool async_upload;
            public string sort_mode = "always";
            public uint sort_refresh_rate = 1;
            public uint cutouts_refresh_rate = 1;
            public uint render_order;
        }

        private static readonly RendererSettings DefaultRendererSettings = new();

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

        [Serializable]
        private sealed class AutomationSettings
        {
            public string condition_id;
            public string sequence_id;
            public string mode;
            public float translation_speed_unity_units_per_second;
            public float snap_turn_degrees;
            public float snap_turn_interval_seconds;
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

        [MenuItem("SplatVRLab/Configure automated continuous-walk metrics variant")]
        public static void ConfigureAutomatedContinuousWalk()
        {
            ConfigureVariant(LoadAutomatedProfile(
                AutomatedWalkProfileRelativeToRepository, ExpectedAutomatedWalkProfileSha256,
                AutomatedWalkVariantId, "continuous_walk", "continuous_walk"));
        }

        [MenuItem("SplatVRLab/Configure automated stationary-control metrics variant")]
        public static void ConfigureAutomatedStaticReference()
        {
            ConfigureVariant(LoadAutomatedProfile(
                AutomatedStaticProfileRelativeToRepository, ExpectedAutomatedStaticProfileSha256,
                AutomatedStaticVariantId, "static_reference", "static_reference"));
        }

        [MenuItem("SplatVRLab/Configure pruned automated stationary metrics variant")]
        public static void ConfigurePrunedAutomatedStaticReference()
        {
            ConfigureVariant(PrunedRepresentation, LoadAutomatedProfile(
                PrunedAutomatedStaticProfileRelativeToRepository, ExpectedPrunedAutomatedStaticProfileSha256,
                PrunedAutomatedStaticVariantId, "static_reference", "static_reference"));
        }

        [MenuItem("SplatVRLab/Configure baseline visual-reference variant")]
        public static void ConfigureVisualBaseline()
        {
            ConfigureVariant(BaselineRepresentation, LoadAutomatedProfile(
                VisualBaselineProfileRelativeToRepository, ExpectedVisualBaselineProfileSha256,
                VisualBaselineVariantId, "static_reference", "static_reference"), true);
        }

        [MenuItem("SplatVRLab/Configure pruned-100k visual-reference variant")]
        public static void ConfigureVisualPruned()
        {
            ConfigureVariant(PrunedRepresentation, LoadAutomatedProfile(
                VisualPrunedProfileRelativeToRepository, ExpectedVisualPrunedProfileSha256,
                VisualPrunedVariantId, "static_reference", "static_reference"), true);
        }

        [MenuItem("SplatVRLab/Configure baseline visual full-pose variant")]
        public static void ConfigureVisualBaselineFullPose()
        {
            ConfigureVariant(BaselineRepresentation, LoadVisualFullPoseProfile(
                VisualBaselineFullPoseProfileRelativeToRepository,
                ExpectedVisualBaselineFullPoseProfileSha256,
                VisualBaselineFullPoseVariantId), true);
        }

        [MenuItem("SplatVRLab/Configure pruned-100k visual full-pose variant")]
        public static void ConfigureVisualPrunedFullPose()
        {
            ConfigureVariant(PrunedRepresentation, LoadVisualFullPoseProfile(
                VisualPrunedFullPoseProfileRelativeToRepository,
                ExpectedVisualPrunedFullPoseProfileSha256,
                VisualPrunedFullPoseVariantId), true);
        }

        [MenuItem("SplatVRLab/Configure pruned-50k visual full-pose variant")]
        public static void ConfigureVisualPruned50kFullPose()
        {
            ConfigureVariant(Pruned50kRepresentation, LoadVisualFullPoseProfile(
                VisualPruned50kFullPoseProfileRelativeToRepository,
                ExpectedVisualPruned50kFullPoseProfileSha256,
                VisualPruned50kFullPoseVariantId), true);
        }

        [MenuItem("SplatVRLab/Configure baseline visual gamma-linear-off full-pose variant")]
        public static void ConfigureVisualBaselineGammaLinearOffFullPose()
        {
            ConfigureVariant(BaselineRepresentation, LoadVisualDiagnosticProfile(
                VisualBaselineGammaLinearOffFullPoseProfileRelativeToRepository,
                ExpectedVisualBaselineGammaLinearOffFullPoseProfileSha256,
                VisualBaselineGammaLinearOffFullPoseVariantId, gammaToLinear: false, shDegree: 3), true);
        }

        [MenuItem("SplatVRLab/Configure baseline visual SH0 full-pose variant")]
        public static void ConfigureVisualBaselineSh0FullPose()
        {
            ConfigureVariant(BaselineRepresentation, LoadVisualDiagnosticProfile(
                VisualBaselineSh0FullPoseProfileRelativeToRepository,
                ExpectedVisualBaselineSh0FullPoseProfileSha256,
                VisualBaselineSh0FullPoseVariantId, gammaToLinear: true, shDegree: 0), true);
        }

        [MenuItem("SplatVRLab/Configure baseline uncompressed visual full-pose variant")]
        public static void ConfigureVisualBaselineUncompressedFullPose()
        {
            ConfigureVariant(BaselineUncompressedRepresentation, LoadVisualDiagnosticProfile(
                VisualBaselineUncompressedFullPoseProfileRelativeToRepository,
                ExpectedVisualBaselineUncompressedFullPoseProfileSha256,
                VisualBaselineUncompressedFullPoseVariantId, gammaToLinear: true, shDegree: 3), true);
        }

        [MenuItem("SplatVRLab/Configure baseline monoscopic-output visual full-pose variant")]
        public static void ConfigureVisualBaselineMonoscopicOutputFullPose()
        {
            ConfigureVariant(BaselineRepresentation, LoadVisualDiagnosticProfile(
                VisualBaselineMonoscopicOutputFullPoseProfileRelativeToRepository,
                ExpectedVisualBaselineMonoscopicOutputFullPoseProfileSha256,
                VisualBaselineMonoscopicOutputFullPoseVariantId, gammaToLinear: true, shDegree: 3),
                enableReferenceCapture: true, enableMonoscopicReferenceCapture: true);
        }

        [MenuItem("SplatVRLab/Configure baseline per-renderer-sort visual full-pose variant")]
        public static void ConfigureVisualBaselinePerRendererSortFullPose()
        {
            ConfigureVariant(BaselineRepresentation, LoadVisualSortDiagnosticProfile(
                VisualBaselinePerRendererSortFullPoseProfileRelativeToRepository,
                ExpectedVisualBaselinePerRendererSortFullPoseProfileSha256,
                VisualBaselinePerRendererSortFullPoseVariantId, renderOrder: 1), true);
        }

        [MenuItem("SplatVRLab/Configure pruned-100k per-renderer-sort visual full-pose variant")]
        public static void ConfigureVisualPrunedPerRendererSortFullPose()
        {
            ConfigureVariant(PrunedRepresentation, LoadVisualSortDiagnosticProfile(
                VisualPrunedPerRendererSortFullPoseProfileRelativeToRepository,
                ExpectedVisualPrunedPerRendererSortFullPoseProfileSha256,
                VisualPrunedPerRendererSortFullPoseVariantId, renderOrder: 1), true);
        }

        [MenuItem("SplatVRLab/Configure automated snap-turn metrics variant")]
        public static void ConfigureAutomatedSnapTurn()
        {
            ConfigureVariant(LoadAutomatedProfile(
                AutomatedSnapTurnProfileRelativeToRepository, ExpectedAutomatedSnapTurnProfileSha256,
                AutomatedSnapTurnVariantId, "snap_turn", "snap_turn"));
        }

        private static void ConfigureVariant(LocomotionProfile locomotionProfile)
        {
            ConfigureVariant(BaselineRepresentation, locomotionProfile);
        }

        private static void ConfigureVariant(
            RepresentationSpec representation,
            LocomotionProfile locomotionProfile,
            bool enableReferenceCapture = false,
            bool enableMonoscopicReferenceCapture = false)
        {
            ConfigureProjectSettings();
            EnsureGsplatRendererFeatures();
            GsplatAsset splatAsset = ImportRepresentation(representation);
            ReferencePoseRecord referencePose = LoadReferencePose();
            CreateViabilityScene(splatAsset, representation, referencePose, locomotionProfile,
                ResolveRendererSettings(locomotionProfile), enableReferenceCapture,
                enableMonoscopicReferenceCapture);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();

            string variantId = locomotionProfile?.variant_id ?? StationaryVariantId;
            Debug.Log(
                $"[SplatVRLab] SETUP_OK: Unity {Application.unityVersion}; " +
                $"{splatAsset.SplatCount} splats; representation={representation.VariantId}; pose={referencePose.reference_pose_id}; " +
                $"scalePolicy={referencePose.scale.policy}; variant={variantId}; " +
                $"scene={ViabilitySceneAssetPath}");
        }

        [MenuItem("SplatVRLab/Validate viability harness")]
        public static void Validate()
        {
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
            FrameMetricsRecorder metrics =
                UnityEngine.Object.FindAnyObjectByType<FrameMetricsRecorder>();
            if (!metrics)
                throw new InvalidOperationException("The viability scene is missing FrameMetricsRecorder.");
            RepresentationSpec representation = ResolveRepresentation(metrics.RepresentationVariantId);
            GsplatAsset splatAsset = AssetDatabase.LoadAssetAtPath<GsplatAsset>(representation.ImportedAssetPath);
            if (!splatAsset || splatAsset.SplatCount != representation.SplatCount)
                throw new InvalidOperationException(
                    $"The imported representation is missing or has an unexpected splat count: {representation.ImportedAssetPath}.");
            GsplatRenderer renderer = UnityEngine.Object.FindAnyObjectByType<GsplatRenderer>();
            if (!renderer || renderer.GsplatAsset != splatAsset)
                throw new InvalidOperationException("The viability scene does not reference the recorded splat representation.");
            if (Vector3.Distance(renderer.transform.position, expectedPlacement.ModelPosition) > 1e-4f ||
                Quaternion.Angle(renderer.transform.rotation, expectedPlacement.ModelRotation) > 0.01f ||
                Vector3.Distance(
                    renderer.transform.localScale,
                    Vector3.one * expectedPlacement.ModelScale) > 1e-5f)
                throw new InvalidOperationException("The splat transform does not match the reference pose.");
            if (metrics.ExperimentId != "unitysplats_viability_v01" ||
                metrics.SceneId != "poster_baseline" ||
                metrics.RepresentationGaussianCount != (int)representation.SplatCount ||
                metrics.RepresentationPlySha256 != representation.Sha256 ||
                metrics.ReferencePoseId != referencePose.reference_pose_id ||
                metrics.ReferenceFrame != referencePose.selection.frame_file_path ||
                metrics.ScalePolicy != referencePose.scale.policy ||
                metrics.MetricScaleCalibrated != referencePose.scale.metric_calibrated ||
                Mathf.Abs(
                    metrics.MetersPerNerfstudioUnit -
                    referencePose.scale.meters_per_nerfstudio_unit) > 1e-6f)
                throw new InvalidOperationException("Frame metrics provenance does not match the reference pose.");

            RendererSettings expectedRendererSettings = DefaultRendererSettings;
            if (metrics.VariantId == StationaryVariantId)
                ValidateStationaryConfiguration(scene, metrics);
            else if (metrics.VariantId == ExpectedLocomotionVariantId)
                ValidateLocomotionConfiguration(scene, metrics, LoadLocomotionProfile());
            else if (metrics.VariantId == AutomatedWalkVariantId)
                ValidateAutomatedConfiguration(scene, metrics, LoadAutomatedProfile(
                    AutomatedWalkProfileRelativeToRepository, ExpectedAutomatedWalkProfileSha256, AutomatedWalkVariantId,
                    "continuous_walk", "continuous_walk"));
            else if (metrics.VariantId == AutomatedStaticVariantId)
                ValidateAutomatedConfiguration(scene, metrics, LoadAutomatedProfile(
                    AutomatedStaticProfileRelativeToRepository, ExpectedAutomatedStaticProfileSha256,
                    AutomatedStaticVariantId, "static_reference", "static_reference"));
            else if (metrics.VariantId == PrunedAutomatedStaticVariantId)
                ValidateAutomatedConfiguration(scene, metrics, LoadAutomatedProfile(
                    PrunedAutomatedStaticProfileRelativeToRepository,
                    ExpectedPrunedAutomatedStaticProfileSha256,
                    PrunedAutomatedStaticVariantId, "static_reference", "static_reference"));
            else if (metrics.VariantId == VisualBaselineVariantId)
                ValidateVisualReferenceConfiguration(scene, metrics, LoadAutomatedProfile(
                    VisualBaselineProfileRelativeToRepository, ExpectedVisualBaselineProfileSha256,
                    VisualBaselineVariantId, "static_reference", "static_reference"), referencePose, representation);
            else if (metrics.VariantId == VisualPrunedVariantId)
                ValidateVisualReferenceConfiguration(scene, metrics, LoadAutomatedProfile(
                    VisualPrunedProfileRelativeToRepository, ExpectedVisualPrunedProfileSha256,
                    VisualPrunedVariantId, "static_reference", "static_reference"), referencePose, representation);
            else if (metrics.VariantId == VisualBaselineFullPoseVariantId)
                ValidateVisualReferenceConfiguration(scene, metrics, LoadVisualFullPoseProfile(
                    VisualBaselineFullPoseProfileRelativeToRepository,
                    ExpectedVisualBaselineFullPoseProfileSha256,
                    VisualBaselineFullPoseVariantId), referencePose, representation);
            else if (metrics.VariantId == VisualPrunedFullPoseVariantId)
                ValidateVisualReferenceConfiguration(scene, metrics, LoadVisualFullPoseProfile(
                    VisualPrunedFullPoseProfileRelativeToRepository,
                    ExpectedVisualPrunedFullPoseProfileSha256,
                    VisualPrunedFullPoseVariantId), referencePose, representation);
            else if (metrics.VariantId == VisualPruned50kFullPoseVariantId)
                ValidateVisualReferenceConfiguration(scene, metrics, LoadVisualFullPoseProfile(
                    VisualPruned50kFullPoseProfileRelativeToRepository,
                    ExpectedVisualPruned50kFullPoseProfileSha256,
                    VisualPruned50kFullPoseVariantId), referencePose, representation);
            else if (metrics.VariantId == VisualBaselineGammaLinearOffFullPoseVariantId)
            {
                LocomotionProfile profile = LoadVisualDiagnosticProfile(
                    VisualBaselineGammaLinearOffFullPoseProfileRelativeToRepository,
                    ExpectedVisualBaselineGammaLinearOffFullPoseProfileSha256,
                    VisualBaselineGammaLinearOffFullPoseVariantId, gammaToLinear: false, shDegree: 3);
                ValidateVisualReferenceConfiguration(scene, metrics, profile, referencePose, representation);
                expectedRendererSettings = ResolveRendererSettings(profile);
            }
            else if (metrics.VariantId == VisualBaselineSh0FullPoseVariantId)
            {
                LocomotionProfile profile = LoadVisualDiagnosticProfile(
                    VisualBaselineSh0FullPoseProfileRelativeToRepository,
                    ExpectedVisualBaselineSh0FullPoseProfileSha256,
                    VisualBaselineSh0FullPoseVariantId, gammaToLinear: true, shDegree: 0);
                ValidateVisualReferenceConfiguration(scene, metrics, profile, referencePose, representation);
                expectedRendererSettings = ResolveRendererSettings(profile);
            }
            else if (metrics.VariantId == VisualBaselineUncompressedFullPoseVariantId)
            {
                LocomotionProfile profile = LoadVisualDiagnosticProfile(
                    VisualBaselineUncompressedFullPoseProfileRelativeToRepository,
                    ExpectedVisualBaselineUncompressedFullPoseProfileSha256,
                    VisualBaselineUncompressedFullPoseVariantId, gammaToLinear: true, shDegree: 3);
                ValidateVisualReferenceConfiguration(scene, metrics, profile, referencePose, representation);
                expectedRendererSettings = ResolveRendererSettings(profile);
            }
            else if (metrics.VariantId == VisualBaselineMonoscopicOutputFullPoseVariantId)
            {
                LocomotionProfile profile = LoadVisualDiagnosticProfile(
                    VisualBaselineMonoscopicOutputFullPoseProfileRelativeToRepository,
                    ExpectedVisualBaselineMonoscopicOutputFullPoseProfileSha256,
                    VisualBaselineMonoscopicOutputFullPoseVariantId, gammaToLinear: true, shDegree: 3);
                ValidateVisualReferenceConfiguration(scene, metrics, profile, referencePose, representation);
                ValidateMonoscopicReferenceOutputCapture(referencePose, representation, profile);
                expectedRendererSettings = ResolveRendererSettings(profile);
            }
            else if (metrics.VariantId == VisualBaselinePerRendererSortFullPoseVariantId)
            {
                LocomotionProfile profile = LoadVisualSortDiagnosticProfile(
                    VisualBaselinePerRendererSortFullPoseProfileRelativeToRepository,
                    ExpectedVisualBaselinePerRendererSortFullPoseProfileSha256,
                    VisualBaselinePerRendererSortFullPoseVariantId, renderOrder: 1);
                ValidateVisualReferenceConfiguration(scene, metrics, profile, referencePose, representation);
                expectedRendererSettings = ResolveRendererSettings(profile);
            }
            else if (metrics.VariantId == VisualPrunedPerRendererSortFullPoseVariantId)
            {
                LocomotionProfile profile = LoadVisualSortDiagnosticProfile(
                    VisualPrunedPerRendererSortFullPoseProfileRelativeToRepository,
                    ExpectedVisualPrunedPerRendererSortFullPoseProfileSha256,
                    VisualPrunedPerRendererSortFullPoseVariantId, renderOrder: 1);
                ValidateVisualReferenceConfiguration(scene, metrics, profile, referencePose, representation);
                expectedRendererSettings = ResolveRendererSettings(profile);
            }
            else if (metrics.VariantId == AutomatedSnapTurnVariantId)
                ValidateAutomatedConfiguration(scene, metrics, LoadAutomatedProfile(
                    AutomatedSnapTurnProfileRelativeToRepository, ExpectedAutomatedSnapTurnProfileSha256, AutomatedSnapTurnVariantId,
                    "snap_turn", "snap_turn"));
            else
                throw new InvalidOperationException($"Unknown harness variant: {metrics.VariantId}.");

            ValidateRendererSettings(renderer, metrics, splatAsset, representation, expectedRendererSettings);

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
                $"[SplatVRLab] VALIDATION_OK: splats={splatAsset.SplatCount}; " +
                $"representation={representation.VariantId}; compression={splatAsset.Compression}; shBands={splatAsset.SHBands}; pose={referencePose.reference_pose_id}; " +
                $"variant={metrics.VariantId}; locomotion={metrics.LocomotionMode}; " +
                $"gammaToLinear={renderer.GammaToLinear}; shDegree={renderer.SHDegree}; " +
                $"brightness={renderer.Brightness:F3}; splatDownscaleFactor={renderer.SplatDownscaleFactor:F3}; " +
                $"sortMode={renderer.SortMode}; sortRefreshRate={renderer.SortRefreshRate}; " +
                $"cutoutsRefreshRate={renderer.CutoutsRefreshRate}; renderOrder={renderer.RenderOrder}; " +
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
            return ImportRepresentation(BaselineRepresentation);
        }

        private static RepresentationSpec ResolveRepresentation(string variantId)
        {
            if (variantId == BaselineRepresentation.VariantId)
                return BaselineRepresentation;
            if (variantId == PrunedRepresentation.VariantId)
                return PrunedRepresentation;
            if (variantId == Pruned50kRepresentation.VariantId)
                return Pruned50kRepresentation;
            if (variantId == BaselineUncompressedRepresentation.VariantId)
                return BaselineUncompressedRepresentation;
            throw new InvalidOperationException($"Unknown splat representation: {variantId}.");
        }

        private static GsplatAsset ImportRepresentation(RepresentationSpec representation)
        {
            string projectRoot = ResolveProjectRoot();
            string sourcePath = ResolveRepositoryPath(representation.SourceRelativePath);
            string destinationPath = Path.Combine(projectRoot, representation.ImportedAssetPath);

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Representation PLY is missing.", sourcePath);
            if (!string.Equals(
                    ComputeSha256(sourcePath), representation.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Representation PLY checksum differs from the recorded artifact.");

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var sourceInfo = new FileInfo(sourcePath);
            var destinationInfo = new FileInfo(destinationPath);
            if (!destinationInfo.Exists || destinationInfo.Length != sourceInfo.Length)
                File.Copy(sourcePath, destinationPath, overwrite: true);

            AssetDatabase.ImportAsset(
                representation.ImportedAssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            AssetImporter importer = AssetImporter.GetAtPath(representation.ImportedAssetPath);
            var serializedImporter = new SerializedObject(importer);
            SerializedProperty compression = serializedImporter.FindProperty("Compression");
            SerializedProperty coordinates = serializedImporter.FindProperty("SourceCoordinates");
            bool importerChanged = compression.enumValueIndex != (int)representation.Compression ||
                                   coordinates.enumValueIndex != (int)SourceCoordinates.RUB;
            if (importerChanged)
            {
                compression.enumValueIndex = (int)representation.Compression;
                coordinates.enumValueIndex = (int)SourceCoordinates.RUB;
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }

            GsplatAsset imported = AssetDatabase.LoadAssetAtPath<GsplatAsset>(representation.ImportedAssetPath)
                   ?? throw new InvalidOperationException("UnitySplats did not create a GsplatAsset.");
            if (imported.SplatCount != representation.SplatCount)
                throw new InvalidOperationException(
                    $"Unexpected splat count: {imported.SplatCount}; expected {representation.SplatCount}.");
            if (imported.Compression != representation.Compression)
                throw new InvalidOperationException(
                    $"Unexpected import compression: {imported.Compression}; expected {representation.Compression}.");
            return imported;
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

        private static LocomotionProfile LoadAutomatedProfile(
            string relativePath, string expectedSha256, string variantId, string conditionId, string mode)
        {
            string path = ResolveRepositoryPath(relativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException("Automated locomotion profile is missing.", path);
            if (!string.Equals(ComputeSha256(path), expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Automated locomotion profile checksum differs from the recorded configuration.");
            LocomotionProfile profile = JsonUtility.FromJson<LocomotionProfile>(File.ReadAllText(path));
            if (profile == null || profile.movement == null || profile.turn == null ||
                profile.providers == null || profile.automation == null ||
                profile.schema_version != "1.0" || profile.variant_id != variantId ||
                profile.automation.condition_id != conditionId || profile.automation.mode != mode ||
                profile.providers.gravity_enabled || profile.providers.teleportation_enabled ||
                profile.providers.climb_enabled || profile.providers.grab_move_enabled ||
                profile.providers.jump_enabled ||
                profile.collision_policy != "no_scene_colliders_free_horizontal_motion")
                throw new InvalidOperationException("Automated locomotion profile is inconsistent.");

            if (mode == "continuous_walk" &&
                (profile.automation.translation_speed_unity_units_per_second <= 0f ||
                 profile.automation.snap_turn_degrees != 0f))
                throw new InvalidOperationException("Automated walk profile has invalid motion parameters.");
            if (mode == "static_reference" &&
                (profile.automation.translation_speed_unity_units_per_second != 0f ||
                 profile.automation.snap_turn_degrees != 0f ||
                 profile.automation.snap_turn_interval_seconds != 0f))
                throw new InvalidOperationException("Automated static profile has invalid motion parameters.");
            if (mode == "snap_turn" &&
                (profile.automation.snap_turn_degrees <= 0f ||
                 profile.automation.snap_turn_interval_seconds <= 0f ||
                 profile.automation.translation_speed_unity_units_per_second != 0f))
                throw new InvalidOperationException("Automated turn profile has invalid motion parameters.");
            return profile;
        }

        private static LocomotionProfile LoadVisualFullPoseProfile(
            string relativePath, string expectedSha256, string variantId)
        {
            LocomotionProfile profile = LoadAutomatedProfile(
                relativePath, expectedSha256, variantId, "static_reference", "static_reference");
            if (profile.xr_alignment_mode != "full_pose_once")
                throw new InvalidOperationException(
                    "Visual full-pose profile must request full_pose_once alignment.");
            return profile;
        }

        private static LocomotionProfile LoadVisualDiagnosticProfile(
            string relativePath, string expectedSha256, string variantId, bool gammaToLinear, int shDegree)
        {
            LocomotionProfile profile = LoadVisualFullPoseProfile(relativePath, expectedSha256, variantId);
            RendererSettings settings = profile.renderer ?? throw new InvalidOperationException(
                "Visual diagnostic profile must declare renderer settings.");
            if (settings.gamma_to_linear != gammaToLinear || settings.sh_degree != shDegree ||
                Mathf.Abs(settings.brightness - 1f) > 1e-6f ||
                Mathf.Abs(settings.splat_downscale_factor) > 1e-6f || settings.async_upload)
                throw new InvalidOperationException(
                    "Visual diagnostic profile contains changes beyond its declared single renderer factor.");
            return profile;
        }

        private static LocomotionProfile LoadVisualSortDiagnosticProfile(
            string relativePath, string expectedSha256, string variantId, uint renderOrder)
        {
            LocomotionProfile profile = LoadVisualDiagnosticProfile(
                relativePath, expectedSha256, variantId, gammaToLinear: true, shDegree: 3);
            RendererSettings settings = profile.renderer;
            if (settings.sort_mode != "always" || settings.sort_refresh_rate != 1 ||
                settings.cutouts_refresh_rate != 1 || settings.render_order != renderOrder)
                throw new InvalidOperationException(
                    "Visual sort diagnostic profile differs from the controlled per-renderer variant.");
            return profile;
        }

        private static GsplatRenderer.GsplatSortMode ResolveSortMode(string value) => value switch
        {
            "always" => GsplatRenderer.GsplatSortMode.Always,
            "every_n_frames" => GsplatRenderer.GsplatSortMode.SortEveryNFrames,
            "cutouts_every_n_sorts" => GsplatRenderer.GsplatSortMode.CutoutsEveryNSorts,
            _ => throw new InvalidOperationException($"Unsupported UnitySplats sort mode: {value}."),
        };

        private static RendererSettings ResolveRendererSettings(LocomotionProfile profile) =>
            profile?.renderer ?? DefaultRendererSettings;

        private static void ValidateRendererSettings(
            GsplatRenderer renderer, FrameMetricsRecorder metrics, GsplatAsset splatAsset,
            RepresentationSpec representation, RendererSettings expected)
        {
            if (splatAsset.Compression != representation.Compression ||
                metrics.RepresentationImportCompression != representation.Compression.ToString() ||
                renderer.GammaToLinear != expected.gamma_to_linear ||
                renderer.SHDegree != Mathf.Clamp(expected.sh_degree, 0, splatAsset.SHBands) ||
                Mathf.Abs(renderer.Brightness - expected.brightness) > 1e-6f ||
                Mathf.Abs(renderer.SplatDownscaleFactor - expected.splat_downscale_factor) > 1e-6f ||
                renderer.AsyncUpload != expected.async_upload ||
                renderer.SortMode != ResolveSortMode(expected.sort_mode) ||
                renderer.SortRefreshRate != expected.sort_refresh_rate ||
                renderer.CutoutsRefreshRate != expected.cutouts_refresh_rate ||
                renderer.RenderOrder != expected.render_order ||
                metrics.RendererGammaToLinear != renderer.GammaToLinear ||
                metrics.RendererShDegree != renderer.SHDegree ||
                Mathf.Abs(metrics.RendererBrightness - renderer.Brightness) > 1e-6f ||
                Mathf.Abs(metrics.RendererSplatDownscaleFactor - renderer.SplatDownscaleFactor) > 1e-6f ||
                metrics.RendererAsyncUpload != renderer.AsyncUpload ||
                metrics.RendererSortMode != renderer.SortMode.ToString() ||
                metrics.RendererSortRefreshRate != (int)renderer.SortRefreshRate ||
                metrics.RendererCutoutsRefreshRate != (int)renderer.CutoutsRefreshRate ||
                metrics.RendererRenderOrder != (int)renderer.RenderOrder)
                throw new InvalidOperationException("Gsplat renderer settings do not match the variant profile.");
        }

        private static void CreateViabilityScene(
            GsplatAsset splatAsset,
            RepresentationSpec representation,
            ReferencePoseRecord referencePose,
            LocomotionProfile locomotionProfile,
            RendererSettings rendererSettings,
            bool enableReferenceCapture,
            bool enableMonoscopicReferenceCapture)
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
            splatObject.name = $"Poster_{representation.VariantId}_Gsplat";
            ReferencePosePlacement placement =
                NerfstudioReferencePose.ComputePlacement(referencePose);
            splatObject.transform.SetPositionAndRotation(
                placement.ModelPosition, placement.ModelRotation);
            splatObject.transform.localScale = Vector3.one * placement.ModelScale;
            GsplatRenderer renderer = splatObject.GetComponent<GsplatRenderer>() ??
                                      splatObject.AddComponent<GsplatRenderer>();
            renderer.GsplatAsset = splatAsset;
            renderer.SHDegree = Mathf.Clamp(rendererSettings.sh_degree, 0, splatAsset.SHBands);
            renderer.Brightness = rendererSettings.brightness;
            renderer.SplatDownscaleFactor = rendererSettings.splat_downscale_factor;
            renderer.GammaToLinear = rendererSettings.gamma_to_linear;
            renderer.AsyncUpload = rendererSettings.async_upload;
            renderer.SortMode = ResolveSortMode(rendererSettings.sort_mode);
            renderer.SortRefreshRate = rendererSettings.sort_refresh_rate;
            renderer.CutoutsRefreshRate = rendererSettings.cutouts_refresh_rate;
            renderer.RenderOrder = rendererSettings.render_order;

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
            aligner.Mode = locomotionProfile?.xr_alignment_mode == "full_pose_once"
                ? XrReferencePoseAligner.AlignmentMode.FullPoseOnce
                : XrReferencePoseAligner.AlignmentMode.YawOnly;
            aligner.TrackingWaitSeconds = 30f;

            ConfigureReferenceEvaluationCapture(
                scene, origin, aligner, representation, referencePose, placement, locomotionProfile, enableReferenceCapture);
            ConfigureMonoscopicReferenceOutputCapture(
                representation, referencePose, placement, locomotionProfile,
                enableMonoscopicReferenceCapture);

            GameObject metricsObject = GameObject.Find("ExperimentMetrics") ??
                                       new GameObject("ExperimentMetrics");
            FrameMetricsRecorder metrics = metricsObject.GetComponent<FrameMetricsRecorder>() ??
                                           metricsObject.AddComponent<FrameMetricsRecorder>();
            metrics.ExperimentId = "unitysplats_viability_v01";
            metrics.SceneId = "poster_baseline";
            metrics.VariantId = locomotionProfile?.variant_id ?? StationaryVariantId;
            metrics.RepresentationVariantId = representation.VariantId;
            metrics.RepresentationGaussianCount = (int)representation.SplatCount;
            metrics.RepresentationPlySha256 = representation.Sha256;
            metrics.RepresentationImportCompression = splatAsset.Compression.ToString();
            metrics.RendererGammaToLinear = renderer.GammaToLinear;
            metrics.RendererShDegree = renderer.SHDegree;
            metrics.RendererBrightness = renderer.Brightness;
            metrics.RendererSplatDownscaleFactor = renderer.SplatDownscaleFactor;
            metrics.RendererAsyncUpload = renderer.AsyncUpload;
            metrics.RendererSortMode = renderer.SortMode.ToString();
            metrics.RendererSortRefreshRate = (int)renderer.SortRefreshRate;
            metrics.RendererCutoutsRefreshRate = (int)renderer.CutoutsRefreshRate;
            metrics.RendererRenderOrder = (int)renderer.RenderOrder;
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
            metrics.ConditionId = locomotionProfile?.automation?.condition_id ?? "static_reference";
            metrics.AutomatedSequenceId = locomotionProfile?.automation?.sequence_id ?? "none";
            metrics.WarmupFrames = 180;
            metrics.MeasurementSeconds = 30f;

            AutomatedLocomotionSequence automated =
                xrRoot.GetComponent<AutomatedLocomotionSequence>();
            if (locomotionProfile?.automation == null)
            {
                if (automated)
                    UnityEngine.Object.DestroyImmediate(automated);
            }
            else
            {
                automated ??= xrRoot.AddComponent<AutomatedLocomotionSequence>();
                automated.Metrics = metrics;
                automated.ReferencePoseAligner = aligner;
                automated.Origin = origin;
                automated.Mode = locomotionProfile.automation.mode == "continuous_walk"
                    ? AutomatedLocomotionSequence.SequenceMode.ContinuousWalk
                    : locomotionProfile.automation.mode == "snap_turn"
                        ? AutomatedLocomotionSequence.SequenceMode.SnapTurn
                        : AutomatedLocomotionSequence.SequenceMode.StaticReference;
                automated.TranslationSpeedUnityUnitsPerSecond =
                    locomotionProfile.automation.translation_speed_unity_units_per_second;
                automated.SnapTurnDegrees = locomotionProfile.automation.snap_turn_degrees;
                automated.SnapTurnIntervalSeconds =
                    locomotionProfile.automation.snap_turn_interval_seconds;
                DisableManualMotionProviders(scene);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ViabilitySceneAssetPath)!);
            EditorSceneManager.SaveScene(scene, ViabilitySceneAssetPath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ViabilitySceneAssetPath, enabled: true),
            };
        }

        private static void ConfigureReferenceEvaluationCapture(
            Scene scene,
            XROrigin origin,
            XrReferencePoseAligner aligner,
            RepresentationSpec representation,
            ReferencePoseRecord referencePose,
            ReferencePosePlacement placement,
            LocomotionProfile locomotionProfile,
            bool enabled)
        {
            GameObject existing = GameObject.Find("TrackedPoseCaptureMarker");
            GameObject legacy = GameObject.Find("ReferencePoseEvaluationCamera");
            if (legacy)
                UnityEngine.Object.DestroyImmediate(legacy);
            if (!enabled)
            {
                if (existing)
                    UnityEngine.Object.DestroyImmediate(existing);
                return;
            }

            GameObject markerObject = existing ?? new GameObject("TrackedPoseCaptureMarker");
            TrackedPoseCaptureMarker marker = markerObject.GetComponent<TrackedPoseCaptureMarker>() ??
                                              markerObject.AddComponent<TrackedPoseCaptureMarker>();
            marker.Origin = origin;
            marker.Aligner = aligner;
            marker.VariantId = locomotionProfile.variant_id;
            marker.RepresentationVariantId = representation.VariantId;
            marker.RepresentationGaussianCount = (int)representation.SplatCount;
            marker.ReferencePoseId = referencePose.reference_pose_id;
            marker.ReferenceFrame = referencePose.selection.frame_file_path;
            marker.TargetPosition = placement.ReferenceCameraPosition;
            marker.TargetRotation = placement.ReferenceCameraRotation;
            marker.AlignmentMode = aligner.Mode.ToString();
            marker.DelaySeconds = 12f;
        }

        private static void ConfigureMonoscopicReferenceOutputCapture(
            RepresentationSpec representation,
            ReferencePoseRecord referencePose,
            ReferencePosePlacement placement,
            LocomotionProfile locomotionProfile,
            bool enabled)
        {
            const string captureName = "MonoscopicReferenceOutputCamera";
            GameObject existing = GameObject.Find(captureName);
            if (!enabled)
            {
                if (existing)
                    UnityEngine.Object.DestroyImmediate(existing);
                return;
            }

            GameObject captureObject = existing ?? new GameObject(captureName);
            Camera captureCamera = captureObject.GetComponent<Camera>();
            if (!captureCamera)
                captureCamera = captureObject.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.black;
            captureCamera.allowHDR = false;
            captureCamera.allowMSAA = true;
            captureCamera.nearClipPlane = 0.01f;
            captureCamera.farClipPlane = 1000f;

            ReferencePoseEvaluationCapture capture =
                captureObject.GetComponent<ReferencePoseEvaluationCapture>();
            if (!capture)
                capture = captureObject.AddComponent<ReferencePoseEvaluationCapture>();
            capture.EvaluationCamera = captureCamera;
            capture.ReferenceCameraPosition = placement.ReferenceCameraPosition;
            capture.ReferenceCameraRotation = placement.ReferenceCameraRotation;
            capture.Width = referencePose.camera.width;
            capture.Height = referencePose.camera.height;
            capture.FocalLengthY = referencePose.camera.fl_y;
            capture.DelaySeconds = 15f;
            capture.CaptureId = "ns_poster_test_frame_00001_monoscopic_quest_output_v01";
            capture.VariantId = locomotionProfile.variant_id;
            capture.RepresentationVariantId = representation.VariantId;
            capture.RepresentationGaussianCount = (int)representation.SplatCount;
            capture.RepresentationPlySha256 = representation.Sha256;
            capture.ReferencePoseId = referencePose.reference_pose_id;
            capture.ReferenceFrame = referencePose.selection.frame_file_path;
            capture.IntrinsicsState = referencePose.camera.intrinsics_state +
                "; monoscopic_offscreen_output_diagnostic_no_pixel_metrics";
        }

        private static void ConfigureStationaryRig(Scene scene)
        {
            FindRequiredChild(scene, "XR Origin (XR Rig)", "Locomotion").SetActive(false);
        }

        private static void DisableManualMotionProviders(Scene scene)
        {
            const string rootName = "XR Origin (XR Rig)";
            FindRequiredChild(scene, rootName, "Locomotion/Move")
                .GetComponent<ContinuousMoveProvider>().enabled = false;
            FindRequiredChild(scene, rootName, "Locomotion/Turn")
                .GetComponent<SnapTurnProvider>().enabled = false;
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
            LocomotionProfile profile,
            bool manualProvidersEnabled = true)
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
                (manualProvidersEnabled ? !snapTurnProvider.enabled : snapTurnProvider.enabled) ||
                (manualProvidersEnabled ? !moveProvider.enabled : moveProvider.enabled) ||
                continuousTurnProvider.enabled ||
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

        private static void ValidateAutomatedConfiguration(
            Scene scene, FrameMetricsRecorder metrics, LocomotionProfile profile)
        {
            ValidateLocomotionConfiguration(scene, metrics, profile, manualProvidersEnabled: false);
            const string rootName = "XR Origin (XR Rig)";
            ContinuousMoveProvider moveProvider = FindRequiredChild(scene, rootName, "Locomotion/Move")
                .GetComponent<ContinuousMoveProvider>();
            SnapTurnProvider snapTurnProvider = FindRequiredChild(scene, rootName, "Locomotion/Turn")
                .GetComponent<SnapTurnProvider>();
            AutomatedLocomotionSequence sequence =
                UnityEngine.Object.FindAnyObjectByType<AutomatedLocomotionSequence>();
            if (!sequence || sequence.Metrics != metrics || !sequence.Origin ||
                metrics.ConditionId != profile.automation.condition_id ||
                metrics.AutomatedSequenceId != profile.automation.sequence_id ||
                moveProvider.enabled || snapTurnProvider.enabled)
                throw new InvalidOperationException(
                    "Automated locomotion sequence provenance or provider state is inconsistent.");
        }

        private static void ValidateVisualReferenceConfiguration(
            Scene scene,
            FrameMetricsRecorder metrics,
            LocomotionProfile profile,
            ReferencePoseRecord referencePose,
            RepresentationSpec representation)
        {
            ValidateAutomatedConfiguration(scene, metrics, profile);
            ReferencePosePlacement placement = NerfstudioReferencePose.ComputePlacement(referencePose);
            TrackedPoseCaptureMarker marker =
                UnityEngine.Object.FindAnyObjectByType<TrackedPoseCaptureMarker>();
            XrReferencePoseAligner.AlignmentMode expectedAlignmentMode =
                profile.xr_alignment_mode == "full_pose_once"
                    ? XrReferencePoseAligner.AlignmentMode.FullPoseOnce
                    : XrReferencePoseAligner.AlignmentMode.YawOnly;
            if (!marker || !marker.Origin || !marker.Aligner ||
                marker.Aligner.Mode != expectedAlignmentMode ||
                marker.AlignmentMode != expectedAlignmentMode.ToString() ||
                marker.VariantId != profile.variant_id ||
                marker.RepresentationVariantId != representation.VariantId ||
                marker.RepresentationGaussianCount != (int)representation.SplatCount ||
                marker.ReferencePoseId != referencePose.reference_pose_id ||
                marker.ReferenceFrame != referencePose.selection.frame_file_path ||
                Vector3.Distance(marker.TargetPosition, placement.ReferenceCameraPosition) > 1e-5f ||
                Quaternion.Angle(marker.TargetRotation, placement.ReferenceCameraRotation) > 0.01f)
                throw new InvalidOperationException("Tracked-pose capture marker is missing or inconsistent.");
        }

        private static void ValidateMonoscopicReferenceOutputCapture(
            ReferencePoseRecord referencePose,
            RepresentationSpec representation,
            LocomotionProfile profile)
        {
            ReferencePoseEvaluationCapture capture =
                UnityEngine.Object.FindAnyObjectByType<ReferencePoseEvaluationCapture>();
            if (!capture || !capture.EvaluationCamera || capture.EvaluationCamera.enabled ||
                capture.VariantId != profile.variant_id ||
                capture.RepresentationVariantId != representation.VariantId ||
                capture.RepresentationGaussianCount != (int)representation.SplatCount ||
                capture.RepresentationPlySha256 != representation.Sha256 ||
                capture.ReferencePoseId != referencePose.reference_pose_id ||
                capture.ReferenceFrame != referencePose.selection.frame_file_path ||
                capture.Width != referencePose.camera.width ||
                capture.Height != referencePose.camera.height ||
                Mathf.Abs(capture.FocalLengthY - referencePose.camera.fl_y) > 1e-6f ||
                capture.DelaySeconds < 12f)
                throw new InvalidOperationException(
                    "Monoscopic reference-output capture is missing or inconsistent.");
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
