using System;
using System.Globalization;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace SplatVRLab
{
    /// <summary>Writes a timestamped marker for a host-side stereo screencap.</summary>
    public sealed class TrackedPoseCaptureMarker : MonoBehaviour
    {
        [Serializable] private sealed class Record
        {
            public string schemaVersion = "1.0";
            public string captureKind = "tracked_xr_stereo_screencap_marker";
            public string variantId, representationVariantId, referencePoseId, referenceFrame, completedAtUtc;
            public string alignmentMode;
            public int representationGaussianCount;
            public Vector3 targetPosition, observedPosition;
            public Quaternion targetRotation, observedRotation;
            public float positionErrorUnityUnits, rotationErrorDegrees;
            public bool alignmentCompleted;
        }

        public XROrigin Origin;
        public XrReferencePoseAligner Aligner;
        public string VariantId, RepresentationVariantId, ReferencePoseId, ReferenceFrame;
        public int RepresentationGaussianCount;
        public Vector3 TargetPosition;
        public Quaternion TargetRotation = Quaternion.identity;
        public string AlignmentMode;
        [Min(0f)] public float DelaySeconds = 12f;

        private void Start() => Invoke(nameof(WriteMarker), DelaySeconds);

        private void WriteMarker()
        {
            if (!Origin || !Origin.Camera)
            {
                Debug.LogError("[SplatVRLab] TRACKED_POSE_MARKER_FAILED: XR camera is missing.");
                return;
            }
            Transform camera = Origin.Camera.transform;
            var record = new Record
            {
                variantId = VariantId,
                representationVariantId = RepresentationVariantId,
                representationGaussianCount = RepresentationGaussianCount,
                referencePoseId = ReferencePoseId,
                referenceFrame = ReferenceFrame,
                alignmentMode = AlignmentMode,
                alignmentCompleted = Aligner && Aligner.AlignmentCompleted,
                targetPosition = TargetPosition,
                observedPosition = camera.position,
                targetRotation = TargetRotation,
                observedRotation = camera.rotation,
                positionErrorUnityUnits = Vector3.Distance(TargetPosition, camera.position),
                rotationErrorDegrees = Quaternion.Angle(TargetRotation, camera.rotation),
                completedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            };
            string directory = Path.Combine(Application.persistentDataPath, "visual_evaluation");
            Directory.CreateDirectory(directory);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            string path = Path.Combine(directory, $"tracked_pose_{VariantId}_{timestamp}.json");
            File.WriteAllText(path, JsonUtility.ToJson(record, true));
            Debug.Log(
                $"[SplatVRLab] TRACKED_POSE_MARKER_OK: {path}; mode={record.alignmentMode}; " +
                $"alignmentCompleted={record.alignmentCompleted}; " +
                $"positionError={record.positionErrorUnityUnits:F6}; " +
                $"rotationError={record.rotationErrorDegrees:F4}");
        }
    }
}
