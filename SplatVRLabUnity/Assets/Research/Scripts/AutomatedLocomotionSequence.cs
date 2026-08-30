using Unity.XR.CoreUtils;
using UnityEngine;

namespace SplatVRLab
{
    /// <summary>
    /// Applies a deterministic camera-origin motion only during the recorder's
    /// post-warmup window. It is an automated rendering workload, not a test of
    /// controller input or user comfort.
    /// </summary>
    public sealed class AutomatedLocomotionSequence : MonoBehaviour
    {
        public enum SequenceMode { StaticReference, ContinuousWalk, SnapTurn }

        public FrameMetricsRecorder Metrics;
        public XrReferencePoseAligner ReferencePoseAligner;
        public XROrigin Origin;
        public SequenceMode Mode;
        [Min(0f)] public float TranslationSpeedUnityUnitsPerSecond = 0.25f;
        [Min(0f)] public float SnapTurnDegrees = 30f;
        [Min(0.01f)] public float SnapTurnIntervalSeconds = 0.75f;

        public bool SequenceStarted { get; private set; }
        public float TranslationDistanceUnityUnits { get; private set; }
        public int SnapTurnsExecuted { get; private set; }

        private float _nextSnapTurnAt;

        private void Update()
        {
            if (!SequenceStarted)
            {
                if (!Metrics || !Metrics.MeasurementWindowStarted || !Origin ||
                    (ReferencePoseAligner && !ReferencePoseAligner.AlignmentCompleted))
                    return;

                SequenceStarted = true;
                _nextSnapTurnAt = Metrics.MeasurementElapsedSeconds;
                Debug.Log($"[SplatVRLab] Automated sequence started: mode={Mode}");
            }

            if (Mode == SequenceMode.StaticReference)
                return;

            if (Mode == SequenceMode.ContinuousWalk)
            {
                Vector3 forward = Vector3.ProjectOnPlane(Origin.transform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.000001f)
                    return;
                float distance = TranslationSpeedUnityUnitsPerSecond * Time.unscaledDeltaTime;
                Origin.transform.position += forward.normalized * distance;
                TranslationDistanceUnityUnits += distance;
                return;
            }

            if (Metrics.MeasurementElapsedSeconds < _nextSnapTurnAt)
                return;

            Transform cameraTransform = Origin.Camera ? Origin.Camera.transform : Origin.transform;
            Origin.transform.RotateAround(cameraTransform.position, Vector3.up, SnapTurnDegrees);
            SnapTurnsExecuted++;
            _nextSnapTurnAt += SnapTurnIntervalSeconds;
        }
    }
}
