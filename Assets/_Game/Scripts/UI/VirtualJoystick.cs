using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// On-screen joystick for touch/mouse. Drag the handle inside the base; the
/// resulting normalized direction is pushed into InputProvider. One instance
/// drives movement (left stick), another drives aim+fire (right stick) — chosen
/// by <see cref="axis"/>. Works with mouse in the editor too.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum Axis { Move, Aim }

    [Tooltip("Move = left stick (movement). Aim = right stick (aim + fire while held).")]
    [SerializeField] private Axis axis = Axis.Move;
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [Tooltip("Max handle travel from centre, in pixels.")]
    [SerializeField] private float radius = 90f;

    private Vector2 _input;

    private void Reset() => background = transform as RectTransform;

    public void OnPointerDown(PointerEventData e) { if (HUDLayout.EditMode) return; OnDrag(e); }

    public void OnDrag(PointerEventData e)
    {
        if (HUDLayout.EditMode) return; // let the layout editor drag this widget instead

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, e.position, e.pressEventCamera, out Vector2 local);

        _input = Vector2.ClampMagnitude(local, radius) / radius;
        if (handle != null) handle.anchoredPosition = _input * radius;
        Push();
    }

    public void OnPointerUp(PointerEventData e)
    {
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
