using UnityEngine;

namespace SplatVRLab
{
    /// <summary>
    /// Editor-visible calibration marker. It is not an orbit controller and does not
    /// move the XR rig. The chair center must be confirmed visually before export.
    /// </summary>
    public sealed class ChairOrbitPivotMarker : MonoBehaviour
    {
        public const string DefaultCalibrationNotes =
            "Move this marker to the visual center of the chair.";
        public bool CalibrationConfirmed;
        public string CalibrationNotes = DefaultCalibrationNotes;

        private void OnDrawGizmos()
        {
            Gizmos.color = CalibrationConfirmed ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.08f);
            Gizmos.DrawLine(transform.position + Vector3.left * 0.15f,
                transform.position + Vector3.right * 0.15f);
            Gizmos.DrawLine(transform.position + Vector3.back * 0.15f,
                transform.position + Vector3.forward * 0.15f);
        }
    }
}
