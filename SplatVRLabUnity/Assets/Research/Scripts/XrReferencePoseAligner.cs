using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace SplatVRLab
{
    /// <summary>
    /// Places the live tracked HMD at the canonical reference-camera position and
    /// aligns its horizontal gaze once tracking becomes valid. Pitch and roll stay
    /// under physical head tracking so the reconstructed world remains upright.
    /// </summary>
    public sealed class XrReferencePoseAligner : MonoBehaviour
    {
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
        [Min(1f)] public float TrackingWaitSeconds = 30f;

        public bool AlignmentCompleted { get; private set; }
        public float PositionErrorMeters { get; private set; }
        public float HorizontalForwardErrorDegrees { get; private set; }

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

            bool rotated = Origin.MatchOriginUpCameraForward(TargetUp, TargetHorizontalForward);
            bool moved = Origin.MoveCameraToWorldLocation(TargetCameraPosition);
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
            AlignmentCompleted = true;

            Debug.Log(
                $"[SplatVRLab] REFERENCE_POSE_ALIGNMENT_OK: pose={ReferencePoseId}; " +
                $"frame={ReferenceFrame}; positionErrorMeters={PositionErrorMeters:F6}; " +
                $"horizontalForwardErrorDegrees={HorizontalForwardErrorDegrees:F4}; " +
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
