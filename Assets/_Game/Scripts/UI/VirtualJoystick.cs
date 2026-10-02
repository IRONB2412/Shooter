using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// On-screen floating joystick for touch/mouse. When a finger lands on the stick the
/// ring re-centres under that finger, so the handle starts at zero and follows the
/// thumb 1:1 (no jump / offset). Dragging past the ring edge drags the ring along, so
/// direction always reflects where the thumb is relative to the ring. On release the
/// ring snaps back to its home position. One instance drives movement (left stick),
/// another drives aim (right stick) — chosen by <see cref="axis"/>. Each stick tracks
/// the single pointer that pressed it, so two thumbs never interfere.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum Axis { Move, Aim }

    [Tooltip("Move = left stick (movement). Aim = right stick (aim).")]
    [SerializeField] private Axis axis = Axis.Move;
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [Tooltip("Max handle travel from centre, in canvas units.")]
    [SerializeField] private float radius = 90f;

    private const int NoPointer = int.MinValue;

    private Vector2 _input;
    private int _pointerId = NoPointer;   // the one finger (or mouse) currently driving this stick
    private Vector3 _homeLocalPos;        // ring position before the press, restored on release
    private Vector2 _center;              // ring centre in parent-local space while pressed
    private RectTransform _parent;

    private void Reset() => background = transform as RectTransform;

    private bool Pressed => _pointerId != NoPointer;

    public void OnPointerDown(PointerEventData e)
    {
        if (HUDLayout.EditMode || Pressed) return; // already owned by another finger
        if (background == null) return;

        _parent = background.parent as RectTransform;
        if (_parent == null || !ToParentLocal(e, out Vector2 p)) return;

        _pointerId = e.pointerId;
        _homeLocalPos = background.localPosition;

        // Ring centre goes under the finger (kept fully on-screen).
        Vector2 half = background.rect.size * 0.5f;
        Rect pr = _parent.rect;
        p.x = Mathf.Clamp(p.x, pr.xMin + half.x, pr.xMax - half.x);
        p.y = Mathf.Clamp(p.y, pr.yMin + half.y, pr.yMax - half.y);
        _center = p;
        background.localPosition = _center - background.rect.center;

        Apply(e);
    }

    public void OnDrag(PointerEventData e)
    {
        // Edit mode lets the layout editor drag the widget; ignore any other finger.
        if (HUDLayout.EditMode || e.pointerId != _pointerId) return;
        Apply(e);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != _pointerId) return;
        Release();
    }

    private void OnDisable() => Release();

    private bool ToParentLocal(PointerEventData e, out Vector2 local)
        => RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, e.position, e.pressEventCamera, out local);

    private void Apply(PointerEventData e)
    {
        if (!ToParentLocal(e, out Vector2 p)) return;

        Vector2 delta = p - _center;
        float mag = delta.magnitude;
        if (mag > radius)
        {
            // Thumb went past the edge: trail the ring behind it.
            _center += delta / mag * (mag - radius);
            background.localPosition = _center - background.rect.center;
            delta = p - _center;
        }

        // Sensitivity: higher = full tilt with less travel. The handle graphic still stops at the ring edge.
        float sens = axis == Axis.Move ? GameSettings.MoveSens : GameSettings.AimSens;
        _input = Vector2.ClampMagnitude(delta * sens / radius, 1f);
        if (handle != null) handle.anchoredPosition = Vector2.ClampMagnitude(delta, radius);
        Push();
    }

    private void Release()
    {
        if (Pressed && background != null) background.localPosition = _homeLocalPos;
        _pointerId = NoPointer;
        _input = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        Push();
    }

    private void Push()
    {
        if (InputProvider.Instance == null) return;
        if (axis == Axis.Move) InputProvider.Instance.SetMove(_input);
        else InputProvider.Instance.SetAim(_input);
    }
}
