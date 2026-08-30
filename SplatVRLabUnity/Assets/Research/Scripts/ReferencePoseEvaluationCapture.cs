using System;
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
            Invoke(nameof(Capture), DelaySeconds);
        }

        private void Capture()
        {
            try
            {
                var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "SplatVRLabReferencePoseEvaluation",
                };
                var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                try
                {
                    Transform cameraTransform = EvaluationCamera.transform;
                    Vector3 previousPosition = cameraTransform.position;
                    Quaternion previousRotation = cameraTransform.rotation;
                    float previousFieldOfView = EvaluationCamera.fieldOfView;
                    float previousAspect = EvaluationCamera.aspect;
                    bool previousEnabled = EvaluationCamera.enabled;
                    RenderTexture previous = RenderTexture.active;
                    cameraTransform.SetPositionAndRotation(
                        ReferenceCameraPosition, ReferenceCameraRotation);
                    EvaluationCamera.enabled = false;
                    EvaluationCamera.fieldOfView = Mathf.Rad2Deg * 2f * Mathf.Atan(
                        Height / (2f * FocalLengthY));
                    EvaluationCamera.aspect = Width / (float)Height;
                    EvaluationCamera.targetTexture = target;
                    EvaluationCamera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    image.Apply(false, false);
                    RenderTexture.active = previous;
                    EvaluationCamera.targetTexture = null;
                    EvaluationCamera.fieldOfView = previousFieldOfView;
                    EvaluationCamera.aspect = previousAspect;
                    cameraTransform.SetPositionAndRotation(previousPosition, previousRotation);
                    EvaluationCamera.enabled = previousEnabled;

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
                    verticalFieldOfViewDegrees = EvaluationCamera.fieldOfView,
                    cameraPosition = ReferenceCameraPosition,
                    cameraRotation = ReferenceCameraRotation,
                    intrinsicsState = IntrinsicsState,
                    completedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    imageFile = Path.GetFileName(CaptureImagePath),
                    };
                    string recordPath = Path.Combine(directory, $"{prefix}.json");
                    File.WriteAllText(recordPath, JsonUtility.ToJson(record, true));
                    CaptureCompleted = true;
                    Debug.Log($"[SplatVRLab] REFERENCE_CAPTURE_OK: image={CaptureImagePath}; metadata={recordPath}; " +
                              $"pose={ReferencePoseId}; representation={RepresentationVariantId}");
                }
                finally
                {
                    EvaluationCamera.targetTexture = null;
                    RenderTexture.active = null;
                    Destroy(target);
                    Destroy(image);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SplatVRLab] REFERENCE_CAPTURE_FAILED: {exception}");
            }
        }
    }
}
