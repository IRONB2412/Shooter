using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Reserves the keyboard for GAMEPLAY; UI is driven only by pointer / touch.
///
/// Without this, Unity's UI input module maps WASD/arrows to "navigate" and
/// Space/Enter to "submit". After tapping any HUD button it stays selected, so
/// pressing Space to fire would also click that button (Reload, Pause...) and WASD
/// would hop the selection around. Text fields keep their selection so typing works.
/// Sits on the EventSystem.
/// </summary>
[RequireComponent(typeof(EventSystem))]
public class UIInputGuard : MonoBehaviour
{
    private void Start()
    {
        var module = GetComponent<InputSystemUIInputModule>();
        if (module == null) return;
        module.move = null;    // no keyboard/gamepad UI navigation
        module.submit = null;  // Space/Enter never clicks a button
        module.cancel = null;  // Esc/Back is handled by the menus themselves
    }

    private void LateUpdate()
    {
        var es = EventSystem.current;
        if (es == null) return;
        var selected = es.currentSelectedGameObject;
        // Drop button selection right after a tap; keep it for text input fields.
        if (selected != null && selected.GetComponent<InputField>() == null)
            es.SetSelectedGameObject(null);
    }
}
