using System;
using System.Collections;
using System.Globalization;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace SplatVRLab
{
    /// <summary>
    /// Native XR orbit for visual documentation. Each pose is held until the host
    /// captures it and writes an acknowledgement; this is not a locomotion or
    /// comfort test. Head tracking remains live, and actual camera poses are logged.
    /// </summary>
    public sealed class NativeChairOrbitSequence : MonoBehaviour
    {
        [Serializable]
        private sealed class PoseRecord
        {
            public string schemaVersion = "1.0";
            public string trajectoryId;
            public string variantId;
            public string representationVariantId;
            public string referencePoseId;
            public int frameIndex;
            public int frameCount;
            public float angleDegrees;
            public float radiusScale;
            public Vector3 chairPivotWorldPosition;
            public Vector3 targetCameraWorldPosition;
            public Quaternion targetCameraWorldRotation;
            public Vector3 actualCameraWorldPosition;
            public Quaternion actualCameraWorldRotation;
            public float positionErrorUnityUnits;
            public float rotationErrorDegrees;
            public string readyAtUtc;
        }

        [Serializable]
        private sealed class CompletionRecord
        {
            public string schemaVersion = "1.0";
            public string trajectoryId;
            public string variantId;
            public string representationVariantId;
            public string status;
            public string error;
            public int acknowledgedFrames;
            public int expectedFrames;
            public string completedAtUtc;
        }

        public XROrigin Origin;
        public XrReferencePoseAligner Aligner;
        public FrameMetricsRecorder Metrics;
        public ChairOrbitPivotMarker PivotMarker;
        public string TrajectoryId = "chair_orbit_full_circle_r125_v01";
        public string VariantId = "spark_baseline_orbit_full_circle_v01";
        public string RepresentationVariantId = "baseline_v01";
        public int FrameCount = 144;
        public float StepDegrees = 2.5f;
        public float RadiusScale = 1.25f;
        public float SettleSeconds = 0.4f;
        public float AcknowledgementTimeoutSeconds = 60f;

        private string _runDirectory;
        private int _acknowledgedFrames;

        private IEnumerator Start()
        {
            if (!Origin || !Origin.Camera || !Aligner || !Metrics || !PivotMarker ||
                !PivotMarker.CalibrationConfirmed || FrameCount != 144 ||
                !Mathf.Approximately(StepDegrees, 2.5f) ||
                !Mathf.Approximately(RadiusScale, 1.25f) ||
                Aligner.Mode != XrReferencePoseAligner.AlignmentMode.FullPoseOnce ||
                Metrics.VariantId != VariantId ||
                Metrics.RepresentationVariantId != RepresentationVariantId)
            {
                Debug.LogError("[SplatVRLab] ORBIT_INVALID_CONFIGURATION");
                yield break;
            }

            _runDirectory = Path.Combine(Application.persistentDataPath,
                "visual_evaluation", "orbit_full_circle_" +
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(_runDirectory);
            Debug.Log("[SplatVRLab] ORBIT_RUN_STARTED: " + _runDirectory);

            float alignmentDeadline = Time.realtimeSinceStartup + 90f;
            while ((!Aligner.AlignmentCompleted || !Metrics.MeasurementWindowStarted) &&
                   Time.realtimeSinceStartup < alignmentDeadline)
                yield return null;
            if (!Aligner.AlignmentCompleted || !Metrics.MeasurementWindowStarted)
            {
                Finish("failed", "tracking_or_measurement_not_ready");
                yield break;
            }

            Transform origin = Origin.transform;
            Transform camera = Origin.Camera.transform;
            Vector3 localCameraPosition = origin.InverseTransformPoint(camera.position);
            Quaternion localCameraRotation = Quaternion.Inverse(origin.rotation) * camera.rotation;
            Vector3 pivot = PivotMarker.transform.position;
            Vector3 radiusVector = Aligner.TargetCameraPosition - pivot;
            Vector3 referenceForward = Aligner.ExactReferenceCameraRotation * Vector3.forward;
            float referencePitch = Mathf.Atan2(-referenceForward.y,
                Vector3.ProjectOnPlane(referenceForward, Vector3.up).magnitude) * Mathf.Rad2Deg;

            for (int index = 0; index < FrameCount; index++)
            {
                float angle = index * StepDegrees;
                Vector3 targetPosition = pivot + Quaternion.AngleAxis(angle, Vector3.up) *
                    (radiusVector * RadiusScale);
                Vector3 horizontalToChair = Vector3.ProjectOnPlane(
                    pivot - targetPosition, Vector3.up).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(horizontalToChair,
                    Vector3.up) * Quaternion.AngleAxis(referencePitch, Vector3.right);

                // Change only the XR origin: never override the tracked HMD transform.
                origin.rotation = targetRotation * Quaternion.Inverse(localCameraRotation);
                origin.position = targetPosition - origin.rotation * localCameraPosition;
                yield return new WaitForSecondsRealtime(SettleSeconds);
                yield return new WaitForEndOfFrame();

                var record = new PoseRecord
                {
                    trajectoryId = TrajectoryId,
                    variantId = VariantId,
                    representationVariantId = RepresentationVariantId,
                    referencePoseId = Aligner.ReferencePoseId,
                    frameIndex = index,
                    frameCount = FrameCount,
                    angleDegrees = angle,
                    radiusScale = RadiusScale,
                    chairPivotWorldPosition = pivot,
                    targetCameraWorldPosition = targetPosition,
                    targetCameraWorldRotation = targetRotation,
                    actualCameraWorldPosition = camera.position,
                    actualCameraWorldRotation = camera.rotation,
                    positionErrorUnityUnits = Vector3.Distance(camera.position, targetPosition),
                    rotationErrorDegrees = Quaternion.Angle(camera.rotation, targetRotation),
                    readyAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                };
                string stem = "pose_" + index.ToString("D4", CultureInfo.InvariantCulture);
                string readyPath = Path.Combine(_runDirectory, stem + ".ready.json");
                string ackPath = Path.Combine(_runDirectory, stem + ".ack");
                WriteAtomic(readyPath, JsonUtility.ToJson(record, true));
                Debug.Log($"[SplatVRLab] ORBIT_POSE_READY: {index}/{FrameCount}; angle={angle:F1}");

                float deadline = Time.realtimeSinceStartup + AcknowledgementTimeoutSeconds;
                while (!File.Exists(ackPath) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (!File.Exists(ackPath))
                {
                    Finish("failed", "acknowledgement_timeout_at_" + stem);
                    yield break;
                }
                _acknowledgedFrames++;
            }

            Finish("complete", "");
        }

        private void Finish(string status, string error)
        {
            var result = new CompletionRecord
            {
                trajectoryId = TrajectoryId,
                variantId = VariantId,
                representationVariantId = RepresentationVariantId,
                status = status,
                error = error,
                acknowledgedFrames = _acknowledgedFrames,
                expectedFrames = FrameCount,
                completedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            };
            WriteAtomic(Path.Combine(_runDirectory, "completion.json"),
                JsonUtility.ToJson(result, true));
            Metrics.CompleteExternalSequence(status == "complete"
                ? "native_orbit_capture_completed" : "native_orbit_capture_failed");
            Debug.Log($"[SplatVRLab] ORBIT_RUN_{status.ToUpperInvariant()}: " +
                _acknowledgedFrames + "/" + FrameCount + "; " + error);
        }

        private static void WriteAtomic(string path, string contents)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, contents);
            File.Move(temporary, path);
        }
    }
}
