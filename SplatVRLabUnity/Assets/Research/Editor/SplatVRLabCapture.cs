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
        [MenuItem("SplatVRLab/Capture reference-pose diagnostic")]
        public static void CaptureDesktopDiagnostic()
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
            camera.transform.SetPositionAndRotation(
                placement.ReferenceCameraPosition,
                placement.ReferenceCameraRotation);
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

                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                     ?? throw new InvalidOperationException("Cannot resolve project root.");
                string repositoryRoot = Directory.GetParent(projectRoot)?.FullName
                                        ?? throw new InvalidOperationException("Cannot resolve repository root.");
                string evidenceDirectory = Path.Combine(
                    repositoryRoot, "experiments", "unitysplats_viability_v01", "evidence");
                Directory.CreateDirectory(evidenceDirectory);
                string outputPath = Path.Combine(
                    evidenceDirectory, "desktop_reference_pose_diagnostic.png");
                File.WriteAllBytes(outputPath, image.EncodeToPNG());

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
                    $"utc={DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
            }
            finally
            {
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
