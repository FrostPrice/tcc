using System;
using System.Globalization;
using System.IO;
using Gsplat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SplatVRLab.Editor
{
    /// <summary>
    /// Places and exports a visually confirmed chair pivot. The generated record is
    /// deliberately separate from the renderer PLY and cannot overwrite an earlier
    /// calibration. No orbit build is created until this value is verified.
    /// </summary>
    public static class SplatVRLabChairOrbitCalibration
    {
        private const string ScenePath = "Assets/Research/Scenes/GsplatViability.unity";
        private const string MarkerName = "ChairOrbitPivot";
        private const string OutputRelativePath =
            "experiments/unitysplats_viability_v01/chair_orbit_calibration_v01.json";

        [Serializable]
        private sealed class CalibrationRecord
        {
            public string schemaVersion = "1.0";
            public string recordType = "chair_orbit_pivot_calibration";
            public string sceneId;
            public string variantId;
            public string representationVariantId;
            public string representationPlySha256;
            public string referencePoseId;
            public string referenceFrame;
            public Vector3 referenceCameraWorldPosition;
            public Quaternion referenceCameraWorldRotation;
            public Vector3 chairPivotWorldPosition;
            public float initialHorizontalRadiusUnityUnits;
            public bool metricScaleCalibrated;
            public bool operatorConfirmed;
            public string calibrationNotes;
            public string createdAtUtc;
            public string interpretationLimit =
                "Visual chair-center estimate in Unity units; not a metric 3D annotation or proof of collision-free orbit.";
        }

        [MenuItem("SplatVRLab/Chair orbit/Create or select pivot marker")]
        public static void CreateOrSelectPivotMarker()
        {
            RequireViabilityScene();
            var aligner = UnityEngine.Object.FindAnyObjectByType<XrReferencePoseAligner>();
            var splat = UnityEngine.Object.FindAnyObjectByType<GsplatRenderer>();
            if (!aligner || !splat || !splat.GsplatAsset)
                throw new InvalidOperationException(
                    "Configure the baseline full-pose scene before calibrating the chair pivot.");

            GameObject markerObject = GameObject.Find(MarkerName);
            if (!markerObject)
            {
                markerObject = new GameObject(MarkerName);
                markerObject.transform.position = aligner.TargetCameraPosition;
                EditorSceneManager.MarkSceneDirty(markerObject.scene);
            }
            if (!markerObject.GetComponent<ChairOrbitPivotMarker>())
            {
                markerObject.AddComponent<ChairOrbitPivotMarker>();
                EditorSceneManager.MarkSceneDirty(markerObject.scene);
            }
            Selection.activeGameObject = markerObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[SplatVRLab] Chair orbit pivot marker selected. Move it to the chair center " +
                "in the Scene view, inspect the splat from nearby views, then check CalibrationConfirmed. " +
                "The initial marker position is the camera reference, NOT a chair-center estimate.");
        }

        [MenuItem("SplatVRLab/Chair orbit/Export confirmed pivot calibration")]
        public static void ExportConfirmedPivotCalibration()
        {
            RequireViabilityScene();
            GameObject markerObject = GameObject.Find(MarkerName);
            ChairOrbitPivotMarker marker = markerObject
                ? markerObject.GetComponent<ChairOrbitPivotMarker>() : null;
            if (!marker || !marker.CalibrationConfirmed)
                throw new InvalidOperationException(
                    "Place ChairOrbitPivot at the visual center of the chair and confirm it in the Inspector first.");
            if (string.IsNullOrWhiteSpace(marker.CalibrationNotes) ||
                marker.CalibrationNotes == ChairOrbitPivotMarker.DefaultCalibrationNotes)
                throw new InvalidOperationException(
                    "Replace the placeholder calibration notes with how the chair center was identified.");

            var aligner = UnityEngine.Object.FindAnyObjectByType<XrReferencePoseAligner>();
            var metrics = UnityEngine.Object.FindAnyObjectByType<FrameMetricsRecorder>();
            var splat = UnityEngine.Object.FindAnyObjectByType<GsplatRenderer>();
            if (!aligner || !metrics || !splat || !splat.GsplatAsset ||
                !aligner.Origin || !aligner.Origin.Camera)
                throw new InvalidOperationException("Reference XR rig or splat provenance is incomplete.");

            Vector3 pivot = markerObject.transform.position;
            Vector3 camera = aligner.TargetCameraPosition;
            if (!IsFinite(pivot) || !IsFinite(camera))
                throw new InvalidOperationException("Pivot or reference camera contains a non-finite coordinate.");
            float radius = Vector3.ProjectOnPlane(pivot - camera, Vector3.up).magnitude;
            if (radius <= 0.2f)
                throw new InvalidOperationException(
                    "The chair pivot is too close to the reference camera. Move the marker onto the chair before export.");
            if (metrics.VariantId != "spark_baseline_visual_full_pose_v01" ||
                metrics.RepresentationVariantId != "baseline_v01" ||
                metrics.ReferencePoseId != aligner.ReferencePoseId)
                throw new InvalidOperationException(
                    "Calibrate from the baseline full-pose scene and matching reference pose only.");

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? throw new InvalidOperationException("Cannot resolve the Unity project root.");
            string repositoryRoot = Directory.GetParent(projectRoot)?.FullName
                                    ?? throw new InvalidOperationException("Cannot resolve the repository root.");
            string outputPath = Path.Combine(repositoryRoot, OutputRelativePath);
            if (File.Exists(outputPath))
                throw new InvalidOperationException(
                    "The calibration record already exists and will not be overwritten: " + outputPath);

            var record = new CalibrationRecord
            {
                sceneId = metrics.SceneId,
                variantId = metrics.VariantId,
                representationVariantId = metrics.RepresentationVariantId,
                representationPlySha256 = metrics.RepresentationPlySha256,
                referencePoseId = metrics.ReferencePoseId,
                referenceFrame = metrics.ReferenceFrame,
                referenceCameraWorldPosition = camera,
                referenceCameraWorldRotation = aligner.ExactReferenceCameraRotation,
                chairPivotWorldPosition = pivot,
                initialHorizontalRadiusUnityUnits = radius,
                metricScaleCalibrated = metrics.MetricScaleCalibrated,
                operatorConfirmed = true,
                calibrationNotes = marker.CalibrationNotes,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            EditorSceneManager.SaveScene(markerObject.scene);
            using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
                writer.Write(JsonUtility.ToJson(record, true) + Environment.NewLine);
            Debug.Log($"[SplatVRLab] CHAIR_ORBIT_CALIBRATION_OK: {outputPath}; " +
                $"pivot={pivot}; initialRadiusUnityUnits={radius:F4}; pose={record.referencePoseId}");
        }

        // Batch entry point for a candidate already reviewed against saved camera renders.
        // The explicit coordinates and review notes keep this operation auditable.
        public static void ExportReviewedCandidateFromEnvironment()
        {
            string coordinatesText = Environment.GetEnvironmentVariable(
                "SPLATVRLAB_CHAIR_PIVOT_WORLD");
            string reviewNotes = Environment.GetEnvironmentVariable(
                "SPLATVRLAB_CHAIR_PIVOT_REVIEW_NOTES");
            string[] coordinates = coordinatesText?.Split(',');
            if (coordinates == null || coordinates.Length != 3 ||
                !float.TryParse(coordinates[0], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(coordinates[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(coordinates[2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float z) ||
                !IsFinite(new Vector3(x, y, z)) ||
                string.IsNullOrWhiteSpace(reviewNotes))
                throw new InvalidOperationException(
                    "Provide finite SPLATVRLAB_CHAIR_PIVOT_WORLD=x,y,z and nonempty " +
                    "SPLATVRLAB_CHAIR_PIVOT_REVIEW_NOTES after reviewing saved renders.");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CreateOrSelectPivotMarker();
            ChairOrbitPivotMarker marker = GameObject.Find(MarkerName)
                .GetComponent<ChairOrbitPivotMarker>();
            if (marker.CalibrationConfirmed)
                throw new InvalidOperationException("A confirmed chair pivot already exists in the scene.");
            marker.transform.position = new Vector3(x, y, z);
            marker.CalibrationNotes = reviewNotes;
            marker.CalibrationConfirmed = true;
            EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
            ExportConfirmedPivotCalibration();
        }

        private static void RequireViabilityScene()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException(
                    "Open the configured GsplatViability scene before using chair-orbit calibration tools.");
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
