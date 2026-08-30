using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

namespace SplatVRLab
{
    /// <summary>
    /// Records application frame intervals and Unity FrameTimingManager samples.
    /// The resulting JSON contains both raw samples and derived summaries. Unity's
    /// reported graphics memory is kept under its API name and is not treated as
    /// dedicated Quest VRAM.
    /// </summary>
    public sealed class FrameMetricsRecorder : MonoBehaviour
    {
        [Serializable]
        private sealed class MetricSummary
        {
            public int sampleCount;
            public float meanMs;
            public float medianMs;
            public float p95Ms;
            public float maximumMs;
        }

        [Serializable]
        private sealed class FrameMetricsReport
        {
            public string schemaVersion = "1.0";
            public string experimentId;
            public string sceneId;
            public string variantId;
            public string representationVariantId;
            public int representationGaussianCount;
            public string representationPlySha256;
            public string referencePoseId;
            public string referenceFrame;
            public string scalePolicy;
            public bool metricScaleCalibrated;
            public float metersPerNerfstudioUnit;
            public bool locomotionEnabled;
            public string locomotionProfileId;
            public string locomotionMode;
            public string movementInput;
            public float moveSpeedUnityUnitsPerSecond;
            public string turnMode;
            public string turnInput;
            public float snapTurnDegrees;
            public bool gravityEnabled;
            public string collisionPolicy;
            public string conditionId;
            public string automatedSequenceId;
            public bool automatedSequenceStarted;
            public float automatedTranslationDistanceUnityUnits;
            public int automatedSnapTurnsExecuted;
            public bool referencePoseAlignmentCompleted;
            public float referencePosePositionErrorMeters;
            public float referencePoseHorizontalForwardErrorDegrees;
            public string startedAtUtc;
            public string measurementStartedAtUtc;
            public string finishedAtUtc;
            public string completionReason;
            public string unityVersion;
            public string platform;
            public string operatingSystem;
            public string deviceModel;
            public string graphicsDeviceName;
            public string graphicsDeviceType;
            public int systemMemoryMb;
            public int unityReportedGraphicsMemoryMb;
            public float xrDisplayRefreshRateHz;
            public int warmupFrames;
            public float requestedMeasurementSeconds;
            public MetricSummary applicationFrameInterval;
            public MetricSummary unityCpuFrameTime;
            public MetricSummary unityGpuFrameTime;
            public float[] applicationFrameIntervalMs;
            public float[] unityCpuFrameTimeMs;
            public float[] unityGpuFrameTimeMs;
        }

        [Header("Identificação experimental")]
        public string ExperimentId = "unitysplats_viability_v01";
        public string SceneId = "poster_baseline";
        public string VariantId = "spark_baseline";
        public string RepresentationVariantId = "baseline_v01";
        [Min(1)] public int RepresentationGaussianCount = 195760;
        public string RepresentationPlySha256;

        [Header("Pose de referencia e escala")]
        public string ReferencePoseId;
        public string ReferenceFrame;
        public string ScalePolicy = "canonical_non_metric";
        public bool MetricScaleCalibrated;
        [Min(0.000001f)] public float MetersPerNerfstudioUnit = 1f;

        [Header("Locomoção")]
        public bool LocomotionEnabled;
        public string LocomotionProfileId = "stationary_baseline";
        public string LocomotionMode = "stationary";
        public string MovementInput = "none";
        [Min(0f)] public float MoveSpeedUnityUnitsPerSecond;
        public string TurnMode = "none";
        public string TurnInput = "none";
        [Min(0f)] public float SnapTurnDegrees;
        public bool GravityEnabled;
        public string CollisionPolicy = "not_applicable_stationary";

        [Header("Condição automatizada")]
        public string ConditionId = "static_reference";
        public string AutomatedSequenceId = "none";

        [Header("Janela de medição")]
        [Min(0)] public int WarmupFrames = 180;
        [Min(1f)] public float MeasurementSeconds = 30f;

        private readonly List<float> _applicationFrameIntervals = new();
        private readonly List<float> _cpuFrameTimes = new();
        private readonly List<float> _gpuFrameTimes = new();
        private readonly FrameTiming[] _latestTiming = new FrameTiming[1];
        private int _framesSeen;
        private float _measurementElapsed;
        private bool _reportWritten;
        private bool _measurementStarted;
        private string _startedAtUtc;
        private string _measurementStartedAtUtc;

        public bool MeasurementWindowStarted => _measurementStarted;
        public float MeasurementElapsedSeconds => _measurementElapsed;

        private void Start()
        {
            _startedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }

        private void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            _framesSeen++;
            if (_framesSeen <= WarmupFrames || _reportWritten)
                return;

            if (!_measurementStarted)
            {
                _measurementStarted = true;
                _measurementStartedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                Debug.Log($"[SplatVRLab] Frame metrics measurement window started: {_measurementStartedAtUtc}");
            }

            float frameIntervalMs = Time.unscaledDeltaTime * 1000f;
            _applicationFrameIntervals.Add(frameIntervalMs);
            _measurementElapsed += Time.unscaledDeltaTime;

            uint timingCount = FrameTimingManager.GetLatestTimings(1, _latestTiming);
            if (timingCount > 0)
            {
                if (_latestTiming[0].cpuFrameTime > 0d)
                    _cpuFrameTimes.Add((float)_latestTiming[0].cpuFrameTime);
                if (_latestTiming[0].gpuFrameTime > 0d)
                    _gpuFrameTimes.Add((float)_latestTiming[0].gpuFrameTime);
            }

            if (_measurementElapsed >= MeasurementSeconds)
                WriteReport("measurement_window_completed");
        }

        private void OnApplicationQuit()
        {
            if (!_reportWritten && _applicationFrameIntervals.Count > 0)
                WriteReport("application_quit_before_window_completed");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && !_reportWritten && _applicationFrameIntervals.Count > 0)
                WriteReport("application_paused_before_window_completed");
        }

        private void WriteReport(string completionReason)
        {
            _reportWritten = true;
            XrReferencePoseAligner aligner = FindAnyObjectByType<XrReferencePoseAligner>();
            AutomatedLocomotionSequence automatedSequence =
                FindAnyObjectByType<AutomatedLocomotionSequence>();
            var report = new FrameMetricsReport
            {
                experimentId = ExperimentId,
                sceneId = SceneId,
                variantId = VariantId,
                representationVariantId = RepresentationVariantId,
                representationGaussianCount = RepresentationGaussianCount,
                representationPlySha256 = RepresentationPlySha256,
                referencePoseId = ReferencePoseId,
                referenceFrame = ReferenceFrame,
                scalePolicy = ScalePolicy,
                metricScaleCalibrated = MetricScaleCalibrated,
                metersPerNerfstudioUnit = MetersPerNerfstudioUnit,
                locomotionEnabled = LocomotionEnabled,
                locomotionProfileId = LocomotionProfileId,
                locomotionMode = LocomotionMode,
                movementInput = MovementInput,
                moveSpeedUnityUnitsPerSecond = MoveSpeedUnityUnitsPerSecond,
                turnMode = TurnMode,
                turnInput = TurnInput,
                snapTurnDegrees = SnapTurnDegrees,
                gravityEnabled = GravityEnabled,
                collisionPolicy = CollisionPolicy,
                conditionId = ConditionId,
                automatedSequenceId = AutomatedSequenceId,
                automatedSequenceStarted = automatedSequence && automatedSequence.SequenceStarted,
                automatedTranslationDistanceUnityUnits =
                    automatedSequence ? automatedSequence.TranslationDistanceUnityUnits : 0f,
                automatedSnapTurnsExecuted =
                    automatedSequence ? automatedSequence.SnapTurnsExecuted : 0,
                referencePoseAlignmentCompleted = aligner && aligner.AlignmentCompleted,
                referencePosePositionErrorMeters = aligner ? aligner.PositionErrorMeters : 0f,
                referencePoseHorizontalForwardErrorDegrees =
                    aligner ? aligner.HorizontalForwardErrorDegrees : 0f,
                startedAtUtc = _startedAtUtc,
                measurementStartedAtUtc = _measurementStartedAtUtc,
                finishedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                completionReason = completionReason,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                deviceModel = SystemInfo.deviceModel,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                systemMemoryMb = SystemInfo.systemMemorySize,
                unityReportedGraphicsMemoryMb = SystemInfo.graphicsMemorySize,
                xrDisplayRefreshRateHz = ReadXrRefreshRate(),
                warmupFrames = WarmupFrames,
                requestedMeasurementSeconds = MeasurementSeconds,
                applicationFrameInterval = Summarize(_applicationFrameIntervals),
                unityCpuFrameTime = Summarize(_cpuFrameTimes),
                unityGpuFrameTime = Summarize(_gpuFrameTimes),
                applicationFrameIntervalMs = _applicationFrameIntervals.ToArray(),
                unityCpuFrameTimeMs = _cpuFrameTimes.ToArray(),
                unityGpuFrameTimeMs = _gpuFrameTimes.ToArray(),
            };

            string directory = Path.Combine(Application.persistentDataPath, "measurements");
            Directory.CreateDirectory(directory);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            string path = Path.Combine(directory, $"{ExperimentId}_{VariantId}_{timestamp}.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log($"[SplatVRLab] Frame metrics written to {path}");
        }

        private static float ReadXrRefreshRate()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
            {
                if (display.running && display.TryGetDisplayRefreshRate(out float refreshRate))
                    return refreshRate;
            }

            return 0f;
        }

        private static MetricSummary Summarize(List<float> values)
        {
            if (values.Count == 0)
                return new MetricSummary();

            float[] sorted = values.ToArray();
            Array.Sort(sorted);
            double sum = 0d;
            foreach (float value in sorted)
                sum += value;

            return new MetricSummary
            {
                sampleCount = sorted.Length,
                meanMs = (float)(sum / sorted.Length),
                medianMs = Percentile(sorted, 0.50f),
                p95Ms = Percentile(sorted, 0.95f),
                maximumMs = sorted[^1],
            };
        }

        private static float Percentile(float[] sorted, float percentile)
        {
            if (sorted.Length == 1)
                return sorted[0];

            float position = (sorted.Length - 1) * percentile;
            int lower = Mathf.FloorToInt(position);
            int upper = Mathf.CeilToInt(position);
            if (lower == upper)
                return sorted[lower];

            return Mathf.Lerp(sorted[lower], sorted[upper], position - lower);
        }
    }
}
