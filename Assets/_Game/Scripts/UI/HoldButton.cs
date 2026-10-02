using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A UI button that reports a *held* state (unlike Button which only fires on
/// release). Used for the fire button so holding keeps automatic weapons firing.
/// Locks to the finger that pressed it, so other fingers (moving / aiming) never
/// interfere — essential for multi-touch on phones.
/// </summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private const int NoPointer = int.MinValue;
    private int _pointerId = NoPointer;

    public bool IsHeld => _pointerId != NoPointer;

    public void OnPointerDown(PointerEventData e)
    {
        if (HUDLayout.EditMode || _pointerId != NoPointer) return;
        _pointerId = e.pointerId;
        // Latch the press: a quick tap (down+up inside one frame — common at low FPS)
        // would otherwise be missed by the per-frame "held" check and fire nothing.
        if (InputProvider.Instance != null) InputProvider.Instance.TapFire();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId == _pointerId) _pointerId = NoPointer;
    }

    private void OnDisable() => _pointerId = NoPointer;

    private void Update()
    {
        // Mirror the held state into the shared input every frame.
        if (InputProvider.Instance != null) InputProvider.Instance.SetFire(IsHeld);
    }
}
