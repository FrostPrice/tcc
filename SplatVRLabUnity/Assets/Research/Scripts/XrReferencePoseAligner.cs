using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace SplatVRLab
{
    /// <summary>
    /// Applies one reference-frame offset after tracking becomes valid. The default
    /// yaw-only mode keeps the physical pitch and roll upright; FullPoseOnce is a
    /// diagnostic mode that matches the complete Nerfstudio camera orientation at
    /// that instant while preserving all later tracked head-motion deltas.
    /// </summary>
    public sealed class XrReferencePoseAligner : MonoBehaviour
    {
        public enum AlignmentMode
        {
            YawOnly,
            FullPoseOnce,
        }

        [Header("Reference pose provenance")]
        public string ReferencePoseId;
        public string ReferenceFrame;
        public string ScalePolicy;
        public bool MetricScaleCalibrated;
        public float MetersPerNerfstudioUnit = 1f;

        [Header("Canonical XR placement")]
        public XROrigin Origin;
        public Vector3 TargetCameraPosition = new(0f, 1.6f, 0f);
        public Vector3 TargetUp = Vector3.up;
        public Vector3 TargetHorizontalForward = Vector3.forward;
        public Quaternion ExactReferenceCameraRotation = Quaternion.identity;
        public AlignmentMode Mode = AlignmentMode.YawOnly;
        [Min(1f)] public float TrackingWaitSeconds = 30f;

        public bool AlignmentCompleted { get; private set; }
        public float PositionErrorMeters { get; private set; }
        public float HorizontalForwardErrorDegrees { get; private set; }
        public float FullRotationErrorDegrees { get; private set; }

        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + TrackingWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (IsHeadTracked())
                {
                    // Wait until the end of the tracked frame so the origin methods
                    // operate on the latest pose supplied by OpenXR.
                    yield return new WaitForEndOfFrame();
                    AlignTrackedCamera();
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                $"[SplatVRLab] REFERENCE_POSE_ALIGNMENT_PENDING: pose={ReferencePoseId}; " +
                $"no tracked HMD was available within {TrackingWaitSeconds:F1}s.");
        }

        private void AlignTrackedCamera()
        {
            if (!Origin || !Origin.Camera)
            {
                Debug.LogError("[SplatVRLab] Reference pose alignment has no valid XROrigin camera.");
                return;
            }

            bool rotated;
            bool moved;
            if (Mode == AlignmentMode.FullPoseOnce)
            {
                Transform originTransform = Origin.transform;
                Transform cameraTransform = Origin.Camera.transform;
                Quaternion correction = ExactReferenceCameraRotation *
                                        Quaternion.Inverse(cameraTransform.rotation);
                originTransform.rotation = correction * originTransform.rotation;
                originTransform.position += TargetCameraPosition - cameraTransform.position;
                rotated = true;
                moved = true;
            }
            else
            {
                rotated = Origin.MatchOriginUpCameraForward(TargetUp, TargetHorizontalForward);
                moved = Origin.MoveCameraToWorldLocation(TargetCameraPosition);
            }

            if (!rotated || !moved)
            {
                Debug.LogError(
                    $"[SplatVRLab] REFERENCE_POSE_ALIGNMENT_FAILED: pose={ReferencePoseId}; " +
                    $"rotated={rotated}; moved={moved}");
                return;
            }

            Vector3 actualHorizontalForward = Vector3.ProjectOnPlane(
                Origin.Camera.transform.forward, TargetUp).normalized;
            PositionErrorMeters = Vector3.Distance(
                Origin.Camera.transform.position, TargetCameraPosition);
            HorizontalForwardErrorDegrees = Vector3.Angle(
                actualHorizontalForward, TargetHorizontalForward);
            FullRotationErrorDegrees = Quaternion.Angle(
                Origin.Camera.transform.rotation, ExactReferenceCameraRotation);
            AlignmentCompleted = true;

            Debug.Log(
                $"[SplatVRLab] REFERENCE_POSE_ALIGNMENT_OK: pose={ReferencePoseId}; " +
                $"mode={Mode}; " +
                $"frame={ReferenceFrame}; positionErrorMeters={PositionErrorMeters:F6}; " +
                $"horizontalForwardErrorDegrees={HorizontalForwardErrorDegrees:F4}; " +
                $"fullRotationErrorDegrees={FullRotationErrorDegrees:F4}; " +
                $"scalePolicy={ScalePolicy}; metersPerNerfstudioUnit={MetersPerNerfstudioUnit:F6}; " +
                $"metricScaleCalibrated={MetricScaleCalibrated}");
        }

        private static bool IsHeadTracked()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return head.isValid &&
                   head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) &&
                   tracked;
        }
    }
}
