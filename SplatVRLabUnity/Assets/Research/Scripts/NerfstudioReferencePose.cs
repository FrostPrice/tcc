using System;
using System.Linq;
using UnityEngine;

namespace SplatVRLab
{
    [Serializable]
    public sealed class ReferencePoseRecord
    {
        public string schema_version;
        public string record_type;
        public string scene_id;
        public string experiment_id;
        public string variant_id;
        public string reference_pose_id;
        public ReferencePoseSource source;
        public ReferencePoseSelection selection;
        public ReferencePoseCamera camera;
        public ReferencePoseDataparser dataparser;
        public ReferencePoseScale scale;
        public ReferencePoseUnity unity;
        public string[] limitations;
    }

    [Serializable]
    public sealed class ReferencePoseSource
    {
        public string model;
        public string config;
        public string transforms_sha256;
        public string gaussian_splat_sha256;
        public string derivation;
        public string verification_status;
    }

    [Serializable]
    public sealed class ReferencePoseSelection
    {
        public string split;
        public string policy;
        public int index_in_split;
        public string frame_file_path;
    }

    [Serializable]
    public sealed class ReferencePoseCamera
    {
        public string coordinate_convention;
        public float[] camera_to_world;
        public float fl_x;
        public float fl_y;
        public float cx;
        public float cy;
        public int width;
        public int height;
        public float[] distortion_params;
        public string intrinsics_state;
    }

    [Serializable]
    public sealed class ReferencePoseDataparser
    {
        public string orientation_method;
        public string center_method;
        public bool auto_scale_poses;
        public float[] transform;
        public float scale;
    }

    [Serializable]
    public sealed class ReferencePoseScale
    {
        public string policy;
        public float meters_per_nerfstudio_unit;
        public bool metric_calibrated;
        public string evidence;
    }

    [Serializable]
    public sealed class ReferencePoseUnity
    {
        public string ply_source_coordinates;
        public string nerfstudio_world_up;
        public string unity_world_up;
        public float[] canonical_camera_position;
        public float[] canonical_horizontal_forward;
    }

    public readonly struct ReferencePosePlacement
    {
        public ReferencePosePlacement(
            Vector3 modelPosition,
            Quaternion modelRotation,
            float modelScale,
            Vector3 referenceCameraPosition,
            Quaternion referenceCameraRotation)
        {
            ModelPosition = modelPosition;
            ModelRotation = modelRotation;
            ModelScale = modelScale;
            ReferenceCameraPosition = referenceCameraPosition;
            ReferenceCameraRotation = referenceCameraRotation;
        }

        public Vector3 ModelPosition { get; }
        public Quaternion ModelRotation { get; }
        public float ModelScale { get; }
        public Vector3 ReferenceCameraPosition { get; }
        public Quaternion ReferenceCameraRotation { get; }
    }

    public static class NerfstudioReferencePose
    {
        private const float OrthonormalTolerance = 0.002f;

        public static ReferencePoseRecord ParseAndValidate(
            string json,
            string expectedSceneId,
            string expectedExperimentId,
            string expectedVariantId,
            string expectedSplatSha256)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Reference pose JSON is empty.");

            ReferencePoseRecord record = JsonUtility.FromJson<ReferencePoseRecord>(json)
                                         ?? throw new InvalidOperationException(
                                             "Unity could not deserialize the reference pose JSON.");
            ValidateIdentity(record, expectedSceneId, expectedExperimentId, expectedVariantId);

            if (record.schema_version != "1.0" || record.record_type != "nerfstudio_reference_pose")
                throw new InvalidOperationException("Unsupported reference pose schema or record type.");
            if (record.source == null ||
                !string.Equals(
                    record.source.gaussian_splat_sha256,
                    expectedSplatSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Reference pose PLY checksum does not match the baseline.");
            if (record.selection == null || record.selection.split != "test" ||
                string.IsNullOrWhiteSpace(record.selection.frame_file_path))
                throw new InvalidOperationException("Reference pose must identify a test frame.");
            if (record.camera == null || record.camera.coordinate_convention != "RUB" ||
                record.camera.camera_to_world == null || record.camera.camera_to_world.Length != 12)
                throw new InvalidOperationException("Reference camera must contain a 3x4 RUB camera-to-world matrix.");
            if (record.camera.width <= 0 || record.camera.height <= 0 ||
                !IsPositiveFinite(record.camera.fl_x) || !IsPositiveFinite(record.camera.fl_y))
                throw new InvalidOperationException("Reference camera intrinsics are invalid.");
            if (record.dataparser == null || record.dataparser.transform == null ||
                record.dataparser.transform.Length != 12 || !IsPositiveFinite(record.dataparser.scale))
                throw new InvalidOperationException("Dataparser transform or scale is invalid.");
            if (record.scale == null || !IsPositiveFinite(record.scale.meters_per_nerfstudio_unit) ||
                string.IsNullOrWhiteSpace(record.scale.policy))
                throw new InvalidOperationException("Reference pose does not declare a usable scale policy.");
            bool policyIsMetric = record.scale.policy == "metric_calibrated";
            if (record.scale.metric_calibrated != policyIsMetric ||
                (!policyIsMetric && record.scale.policy != "canonical_non_metric"))
                throw new InvalidOperationException("Reference pose scale policy and calibration flag disagree.");
            if (record.unity == null || record.unity.ply_source_coordinates != "RUB" ||
                record.unity.nerfstudio_world_up != "+Z" || record.unity.unity_world_up != "+Y" ||
                record.unity.canonical_camera_position == null ||
                record.unity.canonical_camera_position.Length != 3 ||
                record.unity.canonical_horizontal_forward == null ||
                record.unity.canonical_horizontal_forward.Length != 3)
                throw new InvalidOperationException("Unity coordinate convention is missing or unsupported.");

            ValidateFinite(record.camera.camera_to_world, "camera-to-world");
            ValidateFinite(record.dataparser.transform, "dataparser transform");
            ValidateRotation(record.camera.camera_to_world);
            ComputePlacement(record);
            return record;
        }

        public static ReferencePosePlacement ComputePlacement(ReferencePoseRecord record)
        {
            float[] matrix = record.camera.camera_to_world;
            Vector3 cameraRight = Column(matrix, 0);
            Vector3 cameraUp = Column(matrix, 1);
            Vector3 cameraBack = Column(matrix, 2);
            Vector3 cameraOrigin = new(matrix[3], matrix[7], matrix[11]);
            Vector3 cameraForward = -cameraBack;

            Vector3 nerfstudioUp = Vector3.forward;
            Vector3 horizontalForward = Vector3.ProjectOnPlane(cameraForward, nerfstudioUp);
            if (horizontalForward.sqrMagnitude < 1e-6f)
                throw new InvalidOperationException(
                    "Reference camera looks parallel to Nerfstudio world up and cannot define a stable yaw.");
            horizontalForward.Normalize();
            Vector3 horizontalRight = Vector3.Cross(horizontalForward, nerfstudioUp).normalized;

            Vector3 canonicalPosition = VectorFromArray(record.unity.canonical_camera_position);
            Vector3 canonicalForward = VectorFromArray(record.unity.canonical_horizontal_forward).normalized;
            if (canonicalForward.sqrMagnitude < 0.999f ||
                Mathf.Abs(Vector3.Dot(canonicalForward, Vector3.up)) > 1e-4f)
                throw new InvalidOperationException("Canonical Unity forward must be a horizontal unit vector.");

            Quaternion canonicalYaw = Quaternion.FromToRotation(Vector3.forward, canonicalForward);
            Vector3 ConvertWorld(Vector3 value)
            {
                Vector3 canonical = new(
                    Vector3.Dot(horizontalRight, value),
                    Vector3.Dot(nerfstudioUp, value),
                    Vector3.Dot(horizontalForward, value));
                return canonicalYaw * canonical;
            }

            // UnitySplats already converts imported RUB positions to Unity RUF by
            // flipping Z. Apply the remaining proper rotation to the GameObject.
            Vector3 objectRight = ConvertWorld(Vector3.right);
            Vector3 objectUp = ConvertWorld(Vector3.up);
            Vector3 objectForward = ConvertWorld(Vector3.back);
            Quaternion objectRotation = Quaternion.LookRotation(objectForward, objectUp);

            float uniformScale = record.scale.meters_per_nerfstudio_unit;
            Vector3 objectPosition = canonicalPosition - uniformScale * ConvertWorld(cameraOrigin);
            Quaternion referenceCameraRotation = Quaternion.LookRotation(
                ConvertWorld(cameraForward), ConvertWorld(cameraUp));

            return new ReferencePosePlacement(
                objectPosition,
                objectRotation,
                uniformScale,
                canonicalPosition,
                referenceCameraRotation);
        }

        private static void ValidateIdentity(
            ReferencePoseRecord record,
            string sceneId,
            string experimentId,
            string variantId)
        {
            if (record.scene_id != sceneId || record.experiment_id != experimentId ||
                record.variant_id != variantId)
            {
                throw new InvalidOperationException(
                    $"Reference pose identity mismatch: {record.scene_id}/" +
                    $"{record.experiment_id}/{record.variant_id}.");
            }
        }

        private static void ValidateRotation(float[] matrix)
        {
            Vector3 x = Column(matrix, 0);
            Vector3 y = Column(matrix, 1);
            Vector3 z = Column(matrix, 2);
            if (Mathf.Abs(x.magnitude - 1f) > OrthonormalTolerance ||
                Mathf.Abs(y.magnitude - 1f) > OrthonormalTolerance ||
                Mathf.Abs(z.magnitude - 1f) > OrthonormalTolerance ||
                Mathf.Abs(Vector3.Dot(x, y)) > OrthonormalTolerance ||
                Mathf.Abs(Vector3.Dot(x, z)) > OrthonormalTolerance ||
                Mathf.Abs(Vector3.Dot(y, z)) > OrthonormalTolerance ||
                Mathf.Abs(Vector3.Dot(x, Vector3.Cross(y, z)) - 1f) > OrthonormalTolerance)
                throw new InvalidOperationException("Reference camera rotation is not right-handed orthonormal.");
        }

        private static Vector3 Column(float[] matrix, int column) =>
            new(matrix[column], matrix[4 + column], matrix[8 + column]);

        private static Vector3 VectorFromArray(float[] values) =>
            new(values[0], values[1], values[2]);

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateFinite(float[] values, string label)
        {
            if (values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidOperationException($"Reference pose {label} contains a non-finite value.");
        }
    }
}
