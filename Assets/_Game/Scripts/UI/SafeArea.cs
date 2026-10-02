using UnityEngine;

/// <summary>
/// Fits this RectTransform to the device's safe area so HUD controls never sit
/// under a phone's notch, punch-hole camera or rounded corners. Re-applies if the
/// safe area or resolution changes (e.g. rotating between landscape left/right).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform _rt;
    private Rect _lastArea;
    private Vector2Int _lastSize;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastArea || Screen.width != _lastSize.x || Screen.height != _lastSize.y)
            Apply();
    }

    private void Apply()
    {
        _lastArea = Screen.safeArea;
        _lastSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = _lastArea.position;
        Vector2 max = _lastArea.position + _lastArea.size;
        min.x /= Screen.width;  min.y /= Screen.height;
        max.x /= Screen.width;  max.y /= Screen.height;

        _rt.anchorMin = min;
        _rt.anchorMax = max;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}
