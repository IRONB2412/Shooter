using UnityEngine;

/// <summary>
/// Smoothly follows a target (the local player) and clamps to the map bounds so
/// the camera never shows past the edges of a large level.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.15f;
    [Tooltip("World-space rectangle the camera centre is kept inside. Set to the map size.")]
    [SerializeField] private Rect bounds = new Rect(-50, -50, 100, 100);
    [SerializeField] private bool useBounds = true;

    private Vector3 _velocity;

    public void SetTarget(Transform t) => target = t;
    public void SetBounds(Rect r) { bounds = r; useBounds = true; }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);

        if (useBounds && Camera.main != null)
        {
            // Keep the visible area within bounds (accounts for the camera size).
            float halfH = Camera.main.orthographicSize;
            float halfW = halfH * Camera.main.aspect;
            goal.x = Mathf.Clamp(goal.x, bounds.xMin + halfW, bounds.xMax - halfW);
            goal.y = Mathf.Clamp(goal.y, bounds.yMin + halfH, bounds.yMax - halfH);
        }

        transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, smoothTime);
    }
}
