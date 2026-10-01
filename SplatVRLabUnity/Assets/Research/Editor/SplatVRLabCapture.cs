using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Gsplat;
using UnityEditor;
using UnityEngine;

namespace SplatVRLab.Editor
{
    public static class SplatVRLabCapture
    {
        [Serializable]
        private sealed class OrbitCalibration
        {
            public string variantId;
            public string representationPlySha256;
            public string referencePoseId;
            public Vector3 chairPivotWorldPosition;
            public bool operatorConfirmed;
        }

        [MenuItem("SplatVRLab/Capture reference-pose diagnostic")]
        public static void CaptureDesktopDiagnostic() => CaptureDiagnostic(orbitPreview: false);

        [MenuItem("SplatVRLab/Chair orbit/Capture desktop orbit preview")]
        public static void CaptureChairOrbitPreview() => CaptureDiagnostic(orbitPreview: true);

        [MenuItem("SplatVRLab/Chair orbit/Capture desktop scene sweep")]
        public static void CaptureChairOrbitSweep()
        {
            string previousAngle = Environment.GetEnvironmentVariable(
                "SPLATVRLAB_ORBIT_PREVIEW_DEGREES");
            string previousFrame = Environment.GetEnvironmentVariable(
                "SPLATVRLAB_ORBIT_PREVIEW_FRAME_ID");
            try
            {
                for (int frame = 0; frame <= 20; frame++)
                {
                    int angle = frame <= 5 ? -8 * frame
                        : frame <= 10 ? -8 * (10 - frame)
                        : frame <= 15 ? 8 * (frame - 10)
                        : 8 * (20 - frame);
                    Environment.SetEnvironmentVariable(
                        "SPLATVRLAB_ORBIT_PREVIEW_DEGREES",
                        angle.ToString(CultureInfo.InvariantCulture));
                    Environment.SetEnvironmentVariable(
                        "SPLATVRLAB_ORBIT_PREVIEW_FRAME_ID",
                        frame.ToString("D4", CultureInfo.InvariantCulture));
                    CaptureDiagnostic(orbitPreview: true);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "SPLATVRLAB_ORBIT_PREVIEW_DEGREES", previousAngle);
                Environment.SetEnvironmentVariable(
                    "SPLATVRLAB_ORBIT_PREVIEW_FRAME_ID", previousFrame);
            }
        }

        [MenuItem("SplatVRLab/Chair orbit/Capture desktop full-circle survey")]
        public static void CaptureChairOrbitFullCircle()
        {
            string previousAngle = Environment.GetEnvironmentVariable(
                "SPLATVRLAB_ORBIT_PREVIEW_DEGREES");
            string previousFrame = Environment.GetEnvironmentVariable(
                "SPLATVRLAB_ORBIT_PREVIEW_FRAME_ID");
            try
            {
                for (int frame = 0; frame < 144; frame++)
                {
                    float angle = frame * 2.5f;
                    if (angle > 180f)
                        angle -= 360f;
                    Environment.SetEnvironmentVariable(
                        "SPLATVRLAB_ORBIT_PREVIEW_DEGREES",
                        angle.ToString("F1", CultureInfo.InvariantCulture));
                    Environment.SetEnvironmentVariable(
                        "SPLATVRLAB_ORBIT_PREVIEW_FRAME_ID",
                        frame.ToString("D4", CultureInfo.InvariantCulture));
                    CaptureDiagnostic(orbitPreview: true);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "SPLATVRLAB_ORBIT_PREVIEW_DEGREES", previousAngle);
                Environment.SetEnvironmentVariable(
                    "SPLATVRLAB_ORBIT_PREVIEW_FRAME_ID", previousFrame);
            }
        }

        private static void CaptureDiagnostic(bool orbitPreview)
        {
            SplatVRLabSetup.Validate();
            ReferencePoseRecord referencePose = SplatVRLabSetup.LoadReferencePose();
            ReferencePosePlacement placement = NerfstudioReferencePose.ComputePlacement(referencePose);
            GsplatRenderer splat = UnityEngine.Object.FindAnyObjectByType<GsplatRenderer>();
            if (!splat || !splat.GsplatAsset)
                throw new InvalidOperationException("The viability scene has no valid GsplatRenderer.");

            Bounds worldBounds = GsplatUtils.CalcWorldBounds(
                splat.GsplatAsset.Bounds, splat.transform);
            float radius = Mathf.Max(worldBounds.extents.magnitude, 0.1f);
            int width = referencePose.camera.width;
            int height = referencePose.camera.height;

            var cameraObject = new GameObject("ReferencePoseDiagnosticCamera");
            var camera = cameraObject.AddComponent<Camera>();
            string lateralOffsetText = Environment.GetEnvironmentVariable(
                "SPLATVRLAB_DIAGNOSTIC_LATERAL_OFFSET_UNITS");
            float lateralOffset = 0f;
            if (!string.IsNullOrEmpty(lateralOffsetText) &&
                (!float.TryParse(lateralOffsetText, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out lateralOffset) ||
                 float.IsNaN(lateralOffset) || float.IsInfinity(lateralOffset) ||
                 Mathf.Abs(lateralOffset) > 0.5f))
                throw new InvalidOperationException(
                    "Diagnostic lateral offset must be a finite value within +/-0.5 Unity units.");
            float orbitDegrees = 0f;
            float orbitRadiusScale = 1f;
            Vector3 orbitPivot = Vector3.zero;
            if (orbitPreview)
            {
                if (Mathf.Abs(lateralOffset) > 1e-6f)
                    throw new InvalidOperationException(
                        "Orbit preview cannot be combined with a lateral-offset survey.");
                string angleText = Environment.GetEnvironmentVariable(
                    "SPLATVRLAB_ORBIT_PREVIEW_DEGREES");
                if (!float.TryParse(angleText, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out orbitDegrees) ||
                    float.IsNaN(orbitDegrees) || float.IsInfinity(orbitDegrees) ||
                    Mathf.Abs(orbitDegrees) > 180f)
                    throw new InvalidOperationException(
                        "Orbit preview angle must be finite and within +/-180 degrees.");
                string radiusScaleText = Environment.GetEnvironmentVariable(
                    "SPLATVRLAB_ORBIT_PREVIEW_RADIUS_SCALE");
                if (!string.IsNullOrEmpty(radiusScaleText) &&
                    (!float.TryParse(radiusScaleText, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out orbitRadiusScale) ||
                     float.IsNaN(orbitRadiusScale) || float.IsInfinity(orbitRadiusScale) ||
                     orbitRadiusScale < 1f || orbitRadiusScale > 2f))
                    throw new InvalidOperationException(
                        "Orbit preview radius scale must be finite and within 1.0–2.0.");
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                     ?? throw new InvalidOperationException("Cannot resolve project root.");
                string repositoryRoot = Directory.GetParent(projectRoot)?.FullName
                                        ?? throw new InvalidOperationException("Cannot resolve repository root.");
                string calibrationPath = Path.Combine(repositoryRoot, "experiments",
                    "unitysplats_viability_v01", "chair_orbit_calibration_v01.json");
                if (!File.Exists(calibrationPath))
                    throw new FileNotFoundException("Chair orbit calibration is missing.", calibrationPath);
                OrbitCalibration calibration = JsonUtility.FromJson<OrbitCalibration>(
                    File.ReadAllText(calibrationPath));
                var marker = UnityEngine.Object.FindAnyObjectByType<ChairOrbitPivotMarker>();
                var metrics = UnityEngine.Object.FindAnyObjectByType<FrameMetricsRecorder>();
                if (calibration == null || !calibration.operatorConfirmed || !marker ||
                    !marker.CalibrationConfirmed || !metrics ||
                    calibration.variantId != metrics.VariantId ||
                    calibration.representationPlySha256 != metrics.RepresentationPlySha256 ||
                    calibration.referencePoseId != referencePose.reference_pose_id ||
                    Vector3.Distance(marker.transform.position,
                        calibration.chairPivotWorldPosition) > 1e-4f)
                    throw new InvalidOperationException(
                        "Saved chair pivot and calibration provenance do not match this scene.");
                orbitPivot = calibration.chairPivotWorldPosition;
                Vector3 radiusVector = placement.ReferenceCameraPosition - orbitPivot;
                Vector3 orbitPosition = orbitPivot +
                    Quaternion.AngleAxis(orbitDegrees, Vector3.up) *
                    (radiusVector * orbitRadiusScale);
                Vector3 horizontalToChair = Vector3.ProjectOnPlane(
                    orbitPivot - orbitPosition, Vector3.up).normalized;
                Vector3 referenceForward = placement.ReferenceCameraRotation * Vector3.forward;
                float referencePitch = Mathf.Atan2(-referenceForward.y,
                    Vector3.ProjectOnPlane(referenceForward, Vector3.up).magnitude) *
                    Mathf.Rad2Deg;
                Quaternion orbitRotation = Quaternion.LookRotation(horizontalToChair,
                    Vector3.up) * Quaternion.AngleAxis(referencePitch, Vector3.right);
                camera.transform.SetPositionAndRotation(orbitPosition, orbitRotation);
            }
            else
            {
                camera.transform.SetPositionAndRotation(
                    placement.ReferenceCameraPosition +
                    placement.ReferenceCameraRotation * Vector3.right * lateralOffset,
                    placement.ReferenceCameraRotation);
            }
            camera.nearClipPlane = Mathf.Max(0.001f, radius * 0.001f);
            camera.farClipPlane = Mathf.Max(100f, radius * 8f);
            camera.projectionMatrix = ProjectionFromIntrinsics(
                referencePose.camera,
                camera.nearClipPlane,
                camera.farClipPlane);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.035f, 0.035f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = true;

            var target = new RenderTexture(
                width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            Renderer[] xrVisuals = Array.Empty<Renderer>();
            bool[] xrVisualStates = Array.Empty<bool>();
            if (orbitPreview)
            {
                GameObject xrRig = GameObject.Find("XR Origin (XR Rig)");
                if (xrRig)
                {
                    xrVisuals = xrRig.GetComponentsInChildren<Renderer>(true);
                    xrVisualStates = new bool[xrVisuals.Length];
                    for (int index = 0; index < xrVisuals.Length; index++)
                    {
                        xrVisualStates[index] = xrVisuals[index].enabled;
                        xrVisuals[index].enabled = false;
                    }
                }
            }

            try
            {
                target.Create();
                camera.targetTexture = target;

                // UnitySplats binds GPU resources from GsplatRenderer.Update and queues its
                // procedural draw from the package's player-loop hook. A synchronous editor
                // Camera.Render call does not advance either callback on its own.
                for (int frame = 0; frame < 3; frame++)
                {
                    splat.Update();
                    GsplatSorter.Instance.Update();
                    camera.Render();
                }

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                RenderTexture.active = previous;

                Color32[] pixels = image.GetPixels32();
                Color32 background = camera.backgroundColor;
                int nonBackgroundPixels = 0;
                long rgbSum = 0;
                foreach (Color32 pixel in pixels)
                {
                    rgbSum += pixel.r + pixel.g + pixel.b;
                    int difference = Mathf.Abs(pixel.r - background.r) +
                                     Mathf.Abs(pixel.g - background.g) +
                                     Mathf.Abs(pixel.b - background.b);
                    if (difference > 12)
                        nonBackgroundPixels++;
                }

                double nonBackgroundFraction = nonBackgroundPixels / (double)pixels.Length;
                double meanRgb = rgbSum / (pixels.Length * 3.0);
                if (nonBackgroundFraction < 0.001)
                {
                    throw new InvalidOperationException(
                        "Diagnostic capture contains no meaningful pixels beyond the background.");
                }

                string markerWorldText = Environment.GetEnvironmentVariable(
                    "SPLATVRLAB_DIAGNOSTIC_MARKER_WORLD");
                if (!string.IsNullOrEmpty(markerWorldText))
                {
                    string[] coordinates = markerWorldText.Split(',');
                    if (coordinates.Length != 3 ||
                        !float.TryParse(coordinates[0], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float markerX) ||
                        !float.TryParse(coordinates[1], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float markerY) ||
                        !float.TryParse(coordinates[2], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float markerZ) ||
                        float.IsNaN(markerX) || float.IsInfinity(markerX) ||
                        float.IsNaN(markerY) || float.IsInfinity(markerY) ||
                        float.IsNaN(markerZ) || float.IsInfinity(markerZ))
                        throw new InvalidOperationException(
                            "Diagnostic marker must contain three finite comma-separated world coordinates.");
                    Vector3 markerScreen = camera.WorldToScreenPoint(
                        new Vector3(markerX, markerY, markerZ));
                    if (markerScreen.z > 0)
                    {
                        int pixelX = Mathf.RoundToInt(markerScreen.x);
                        int pixelY = Mathf.RoundToInt(markerScreen.y);
                        for (int delta = -10; delta <= 10; delta++)
                        {
                            if (pixelX + delta >= 0 && pixelX + delta < width &&
                                pixelY >= 0 && pixelY < height)
                                image.SetPixel(pixelX + delta, pixelY, Color.magenta);
                            if (pixelX >= 0 && pixelX < width &&
                                pixelY + delta >= 0 && pixelY + delta < height)
                                image.SetPixel(pixelX, pixelY + delta, Color.magenta);
                        }
                        image.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    }
                    Debug.Log($"[SplatVRLab] DIAGNOSTIC_MARKER: world=({markerWorldText}); " +
                        $"screen=({markerScreen.x:F1},{markerScreen.y:F1}); depth={markerScreen.z:F3}");
                }

                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                     ?? throw new InvalidOperationException("Cannot resolve project root.");
                string repositoryRoot = Directory.GetParent(projectRoot)?.FullName
                                        ?? throw new InvalidOperationException("Cannot resolve repository root.");
                string evidenceDirectory = Path.Combine(
                    repositoryRoot, "experiments", "unitysplats_viability_v01", "evidence");
                Directory.CreateDirectory(evidenceDirectory);
                string surveyId = Environment.GetEnvironmentVariable(
                    "SPLATVRLAB_DIAGNOSTIC_SURVEY_ID");
                if (orbitPreview && string.IsNullOrEmpty(surveyId))
                    throw new InvalidOperationException("Orbit preview requires a survey ID.");
                string outputPath;
                if (string.IsNullOrEmpty(surveyId))
                {
                    outputPath = Path.Combine(
                        evidenceDirectory, "desktop_reference_pose_diagnostic.png");
                    if (File.Exists(outputPath))
                        throw new IOException("Existing reference diagnostic will not be overwritten: " + outputPath);
                }
                else
                {
                    foreach (char character in surveyId)
                        if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                            throw new InvalidOperationException("Survey ID contains an invalid character.");
                    string surveyDirectory = Path.Combine(evidenceDirectory, surveyId);
                    Directory.CreateDirectory(surveyDirectory);
                    string imageName = orbitPreview
                        ? $"orbit_r{orbitRadiusScale.ToString("F2", CultureInfo.InvariantCulture)}_yaw_{orbitDegrees.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)}.png"
                        : $"reference_lateral_{lateralOffset.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)}.png";
                    if (orbitPreview)
                    {
                        string frameId = Environment.GetEnvironmentVariable(
                            "SPLATVRLAB_ORBIT_PREVIEW_FRAME_ID");
                        if (!string.IsNullOrEmpty(frameId))
                        {
                            if (frameId.Length != 4 ||
                                !int.TryParse(frameId, NumberStyles.None,
                                    CultureInfo.InvariantCulture, out _))
                                throw new InvalidOperationException(
                                    "Orbit preview frame ID must contain exactly four digits.");
                            imageName = $"frame_{frameId}_{imageName}";
                        }
                    }
                    outputPath = Path.Combine(surveyDirectory, imageName);
                }
                using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write))
                {
                    byte[] png = image.EncodeToPNG();
                    stream.Write(png, 0, png.Length);
                }

                string sha256;
                using (SHA256 hasher = SHA256.Create())
                using (FileStream stream = File.OpenRead(outputPath))
                    sha256 = BitConverter.ToString(hasher.ComputeHash(stream))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();

                Debug.Log(
                    $"[SplatVRLab] REFERENCE_CAPTURE_OK: {outputPath}; {width}x{height}; " +
                    $"sha256={sha256}; pose={referencePose.reference_pose_id}; " +
                    $"frame={referencePose.selection.frame_file_path}; " +
                    $"intrinsicsState={referencePose.camera.intrinsics_state}; " +
                    $"nonBackgroundFraction={nonBackgroundFraction.ToString("F6", CultureInfo.InvariantCulture)}; " +
                    $"meanRgb8={meanRgb.ToString("F3", CultureInfo.InvariantCulture)}; " +
                    $"lateralOffsetUnityUnits={lateralOffset.ToString("F3", CultureInfo.InvariantCulture)}; " +
                    $"orbitPreview={orbitPreview}; orbitDegrees={orbitDegrees.ToString("F1", CultureInfo.InvariantCulture)}; " +
                    $"orbitRadiusScale={orbitRadiusScale.ToString("F2", CultureInfo.InvariantCulture)}; " +
                    $"cameraPosition={camera.transform.position}; cameraRotation={camera.transform.rotation.eulerAngles}; " +
                    $"orbitPivot={orbitPivot}; " +
                    $"utc={DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
            }
            finally
            {
                for (int index = 0; index < xrVisuals.Length; index++)
                    if (xrVisuals[index])
                        xrVisuals[index].enabled = xrVisualStates[index];
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static Matrix4x4 ProjectionFromIntrinsics(
            ReferencePoseCamera intrinsics,
            float near,
            float far)
        {
            float left = -intrinsics.cx * near / intrinsics.fl_x;
            float right = (intrinsics.width - intrinsics.cx) * near / intrinsics.fl_x;
            float bottom = -(intrinsics.height - intrinsics.cy) * near / intrinsics.fl_y;
            float top = intrinsics.cy * near / intrinsics.fl_y;

            var matrix = new Matrix4x4();
            matrix[0, 0] = 2f * near / (right - left);
            matrix[0, 2] = (right + left) / (right - left);
            matrix[1, 1] = 2f * near / (top - bottom);
            matrix[1, 2] = (top + bottom) / (top - bottom);
            matrix[2, 2] = -(far + near) / (far - near);
            matrix[2, 3] = -(2f * far * near) / (far - near);
            matrix[3, 2] = -1f;
            return matrix;
        }
    }
}
