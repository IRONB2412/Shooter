using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lets the player drag a panel anywhere on screen (kept fully inside its parent).
/// Used for the layout-edit toolbar so it can be moved off whatever it covers.
/// Drags that start on child buttons bubble up here too, so the whole bar is a handle.
/// Anchor and pivot must both be the centre (0.5, 0.5).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggablePanel : MonoBehaviour, IDragHandler
{
    private RectTransform _rt;
    private RectTransform _parent;
    private Canvas _canvas;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _parent = _rt.parent as RectTransform;
        var c = GetComponentInParent<Canvas>();
        _canvas = c != null ? c.rootCanvas : null;
    }

    public void OnDrag(PointerEventData e)
    {
        float scale = (_canvas != null && _canvas.scaleFactor > 0f) ? _canvas.scaleFactor : 1f;
        Vector2 pos = _rt.anchoredPosition + e.delta / scale;

        if (_parent != null)
        {
            Vector2 room = (_parent.rect.size - _rt.rect.size * _rt.localScale.x) * 0.5f;
            pos.x = Mathf.Clamp(pos.x, -Mathf.Max(0, room.x), Mathf.Max(0, room.x));
            pos.y = Mathf.Clamp(pos.y, -Mathf.Max(0, room.y), Mathf.Max(0, room.y));
        }
        _rt.anchoredPosition = pos;
    }
}
