using UnityEngine;

/// <summary>
/// Keeps a UI transform hovering above a target and optionally billboards toward the main camera.
/// Works best on World Space canvases.
/// </summary>
public class UIFollowTarget : MonoBehaviour
{
    [Tooltip("Transform to follow.")]
    public Transform target;
    [Tooltip("Local offset from the target position.")]
    public Vector3 offset = new Vector3(0f, 1.4f, 0f);
    [Tooltip("Smoothly move toward the target position.")]
    public bool smooth = true;
    [Tooltip("Units per second when smoothing.")]
    public float followSpeed = 6f;
    [Tooltip("Rotate to face the main camera.")]
    public bool billboard = true;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        if (smooth)
        {
            transform.position = Vector3.Lerp(transform.position, desired, Mathf.Clamp01(followSpeed * Time.deltaTime));
        }
        else
        {
            transform.position = desired;
        }

        if (billboard && Camera.main != null)
        {
            Vector3 lookDir = transform.position - Camera.main.transform.position;
            lookDir.y = 0f; // keep upright
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
        }
    }
}
