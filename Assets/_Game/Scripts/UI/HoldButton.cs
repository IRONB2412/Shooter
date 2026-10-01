using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A UI button that reports a *held* state (unlike Button which only fires on
/// release). Used for the fire button so holding keeps automatic weapons firing.
/// </summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool IsHeld { get; private set; }

    public void OnPointerDown(PointerEventData e) { if (!HUDLayout.EditMode) IsHeld = true; }
    public void OnPointerUp(PointerEventData e) => IsHeld = false;

    private void OnDisable() => IsHeld = false;

    private void Update()
    {
        // The fire button just mirrors its held state into the shared input.
        if (InputProvider.Instance != null) InputProvider.Instance.SetFire(IsHeld);
    }
}
