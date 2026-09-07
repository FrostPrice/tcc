using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace SplatVRLab
{
    /// <summary>
    /// Renders one diagnostic, monoscopic image from the exact transformed
    /// Nerfstudio reference camera. It is independent of the tracked HMD pose
    /// and is not a stereo or pixel-metric validation while intrinsics remain
    /// pre-undistortion.
    /// </summary>
    public sealed class ReferencePoseEvaluationCapture : MonoBehaviour
    {
        [Serializable]
        private sealed class CaptureRecord
        {
            public string schemaVersion = "1.0";
            public string captureId;
            public string variantId;
            public string representationVariantId;
            public int representationGaussianCount;
            public string representationPlySha256;
            public string referencePoseId;
            public string referenceFrame;
            public int width;
            public int height;
            public float verticalFieldOfViewDegrees;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public string captureKind = "monoscopic_reference_pose_diagnostic";
            public string intrinsicsState;
            public float nonBackgroundFraction;
            public string completedAtUtc;
            public string imageFile;
        }

        [Header("Evaluation camera")]
        public Camera EvaluationCamera;
        public Vector3 ReferenceCameraPosition;
        public Quaternion ReferenceCameraRotation = Quaternion.identity;
        [Min(1)] public int Width = 540;
        [Min(1)] public int Height = 960;
        [Min(0.001f)] public float FocalLengthY = 579.4126f;
        [Min(0f)] public float DelaySeconds = 12f;

        [Header("Provenance")]
        public string CaptureId = "ns_poster_test_frame_00001_monoscopic_v01";
        public string VariantId;
        public string RepresentationVariantId;
        public int RepresentationGaussianCount;
        public string RepresentationPlySha256;
        public string ReferencePoseId;
        public string ReferenceFrame;
        public string IntrinsicsState;

        public bool CaptureCompleted { get; private set; }
        public string CaptureImagePath { get; private set; }

        private void Start()
        {
            if (!EvaluationCamera)
            {
                Debug.LogError("[SplatVRLab] REFERENCE_CAPTURE_FAILED: evaluation camera is missing.");
                return;
            }
            Invoke(nameof(BeginCapture), DelaySeconds);
        }

        private void BeginCapture()
        {
            StartCoroutine(CaptureAfterUrpFrames());
        }

        private IEnumerator CaptureAfterUrpFrames()
        {
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = null;
            Transform cameraTransform = null;
            Vector3 previousPosition = default;
            Quaternion previousRotation = default;
            float previousFieldOfView = 0f;
            float previousAspect = 0f;
            bool previousEnabled = false;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "SplatVRLabReferencePoseEvaluation",
            };
            image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
            cameraTransform = EvaluationCamera.transform;
            previousPosition = cameraTransform.position;
            previousRotation = cameraTransform.rotation;
            previousFieldOfView = EvaluationCamera.fieldOfView;
            previousAspect = EvaluationCamera.aspect;
            previousEnabled = EvaluationCamera.enabled;
            previous = RenderTexture.active;
            cameraTransform.SetPositionAndRotation(ReferenceCameraPosition, ReferenceCameraRotation);
            EvaluationCamera.fieldOfView = Mathf.Rad2Deg * 2f * Mathf.Atan(
                Height / (2f * FocalLengthY));
            float captureFieldOfView = EvaluationCamera.fieldOfView;
            EvaluationCamera.aspect = Width / (float)Height;
            EvaluationCamera.targetTexture = target;
            // Camera.Render() bypasses UnitySplats' URP renderer feature on Android.
            // Let the enabled camera traverse the normal URP camera loop first.
            EvaluationCamera.enabled = true;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            try
            {
                EvaluationCamera.enabled = false;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply(false, false);
                Color32 background = EvaluationCamera.backgroundColor;
                int nonBackgroundPixels = 0;
                foreach (Color32 pixel in image.GetPixels32())
                {
                    int difference = Mathf.Abs(pixel.r - background.r) +
                                     Mathf.Abs(pixel.g - background.g) +
                                     Mathf.Abs(pixel.b - background.b);
                    if (difference > 12)
                        nonBackgroundPixels++;
                }
                float nonBackgroundFraction = nonBackgroundPixels / (float)(Width * Height);
                if (nonBackgroundFraction < 0.001f)
                    throw new InvalidOperationException(
                        "Monoscopic offscreen capture contains no meaningful pixels beyond the background.");

                string directory = Path.Combine(Application.persistentDataPath, "visual_evaluation");
                Directory.CreateDirectory(directory);
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
                string prefix = $"{CaptureId}_{VariantId}_{timestamp}";
                CaptureImagePath = Path.Combine(directory, $"{prefix}.png");
                File.WriteAllBytes(CaptureImagePath, image.EncodeToPNG());
                var record = new CaptureRecord
                {
                    captureId = CaptureId,
                    variantId = VariantId,
                    representationVariantId = RepresentationVariantId,
                    representationGaussianCount = RepresentationGaussianCount,
                    representationPlySha256 = RepresentationPlySha256,
                    referencePoseId = ReferencePoseId,
                    referenceFrame = ReferenceFrame,
                    width = Width,
                    height = Height,
                    verticalFieldOfViewDegrees = captureFieldOfView,
                    cameraPosition = ReferenceCameraPosition,
                    cameraRotation = ReferenceCameraRotation,
                    intrinsicsState = IntrinsicsState,
                    nonBackgroundFraction = nonBackgroundFraction,
                    completedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    imageFile = Path.GetFileName(CaptureImagePath),
                };
                string recordPath = Path.Combine(directory, $"{prefix}.json");
                File.WriteAllText(recordPath, JsonUtility.ToJson(record, true));
                CaptureCompleted = true;
                Debug.Log($"[SplatVRLab] REFERENCE_CAPTURE_OK: image={CaptureImagePath}; metadata={recordPath}; " +
                          $"pose={ReferencePoseId}; representation={RepresentationVariantId}; " +
                          $"nonBackgroundFraction={nonBackgroundFraction:F6}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SplatVRLab] REFERENCE_CAPTURE_FAILED: {exception}");
            }
            finally
            {
                if (EvaluationCamera)
                {
                    EvaluationCamera.enabled = previousEnabled;
                    EvaluationCamera.targetTexture = null;
                    EvaluationCamera.fieldOfView = previousFieldOfView;
                    EvaluationCamera.aspect = previousAspect;
                }
                if (cameraTransform)
                    cameraTransform.SetPositionAndRotation(previousPosition, previousRotation);
                RenderTexture.active = previous;
                if (target)
                    Destroy(target);
                if (image)
                    Destroy(image);
            }
        }
    }
}
