using UnityEngine;

/// <summary>
/// Smoothly follows a target (the local player) and clamps to the map bounds so
/// the camera never shows past the edges of a large level.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.06f;
    [Tooltip("World-space rectangle the camera centre is kept inside. Set to the map size.")]
    [SerializeField] private Rect bounds = new Rect(-50, -50, 100, 100);
    [SerializeField] private bool useBounds = true;

    private Vector3 _velocity;
    private Vector3 _basePos;
    private bool _hasBase;
    private static float _shakeAmp, _shakeTime;

    /// <summary>Screen shake (explosions). Amplitude in world units, duration in seconds.</summary>
    public static void Shake(float amplitude, float duration)
    {
        if (amplitude > _shakeAmp) _shakeAmp = amplitude;
        _shakeTime = Mathf.Max(_shakeTime, duration);
    }

    public void SetTarget(Transform t) => target = t;
    public void SetBounds(Rect r) { bounds = r; useBounds = true; }

    private void LateUpdate()
    {
        if (target == null) return;
        if (!_hasBase) { _basePos = transform.position; _hasBase = true; }

        Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);

        if (useBounds && Camera.main != null)
        {
            // Keep the visible area within bounds (accounts for the camera size).
            float halfH = Camera.main.orthographicSize;
            float halfW = halfH * Camera.main.aspect;
            goal.x = Mathf.Clamp(goal.x, bounds.xMin + halfW, bounds.xMax - halfW);
            goal.y = Mathf.Clamp(goal.y, bounds.yMin + halfH, bounds.yMax - halfH);
        }

        Vector3 pos = Vector3.SmoothDamp(_basePos, goal, ref _velocity, smoothTime);
        _basePos = pos;

        if (_shakeTime > 0f)
        {
            _shakeTime -= Time.unscaledDeltaTime;
            pos += (Vector3)(Random.insideUnitCircle * _shakeAmp);
            _shakeAmp = Mathf.MoveTowards(_shakeAmp, 0f, _shakeAmp * 4f * Time.unscaledDeltaTime);
            if (_shakeTime <= 0f) _shakeAmp = 0f;
        }
        transform.position = pos;
    }
}
