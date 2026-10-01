using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single source of local input, merging two schemes:
///   - Desktop: keyboard (WASD/arrows) to move, mouse to aim, LMB to fire.
///   - Touch:   on-screen joystick + buttons push values in via the Set* methods.
/// UI widgets and the local player both talk to InputProvider.Instance, so the
/// game plays the same on PC and phone without any per-platform branching elsewhere.
/// </summary>
public class InputProvider : MonoBehaviour
{
    public static InputProvider Instance { get; private set; }

    // ---- Values the local player reads every frame ----
    public Vector2 Move { get; private set; }
    public Vector2 Aim { get; private set; } = Vector2.right;
    public bool FireHeld { get; private set; }

    // One-shot actions consumed by the player driver.
    public bool ReloadPressed { get; private set; }
    public bool ThrowPressed { get; private set; }
    public int WeaponSlotPressed { get; private set; } = -1; // 0..n, or -1 = none

    // ---- Touch/UI backing fields (set by on-screen controls) ----
    private Vector2 _uiMove;   // left stick: movement only
    private Vector2 _uiAim;    // right stick: aim + fire while held
    private bool _uiFire;      // legacy fire button (unused with twin-stick)
    private bool _uiThrowQueued;
    private int _uiWeaponSlot = -1;

    [Tooltip("Aim-stick tilt beyond which the player is considered to be aiming/firing.")]
    private const float AimDeadzone = 0.2f;

    // Where the mouse aims from (the local player sets this each frame).
    private Vector2 _aimOrigin;
    private bool _useMouseAim;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Local player calls this so mouse aim can be computed relative to it.</summary>
    public void SetAimOrigin(Vector2 worldPos) => _aimOrigin = worldPos;

    // ---- Called by touch UI ----
    /// <summary>Left stick: movement direction only.</summary>
    public void SetMove(Vector2 v) => _uiMove = v;
    /// <summary>Right stick: aim/facing direction only (firing is a separate button).</summary>
    public void SetAim(Vector2 v) => _uiAim = v;
    public void SetFire(bool held) => _uiFire = held;
    public void QueueThrow() => _uiThrowQueued = true;
    public void SelectWeapon(int slot) => _uiWeaponSlot = slot;

    private void Update()
    {
        // Reset one-shots; they're true for a single frame.
        ReloadPressed = false;
        ThrowPressed = _uiThrowQueued;
        WeaponSlotPressed = _uiWeaponSlot;
        _uiThrowQueued = false;
        _uiWeaponSlot = -1;

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // --- Movement: keyboard OR UI joystick (whichever is active) ---
        Vector2 kbMove = Vector2.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) kbMove.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) kbMove.y -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbMove.x += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) kbMove.x -= 1;
            if (kb.rKey.wasPressedThisFrame) ReloadPressed = true;
            if (kb.gKey.wasPressedThisFrame) ThrowPressed = true;
            for (int i = 0; i < 4; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) WeaponSlotPressed = i;
        }

        // Movement is driven ONLY by the left stick / keyboard — never by aim.
        Move = _uiMove.sqrMagnitude > 0.01f ? _uiMove : kbMove.normalized;

        // --- Aim (twin-stick): right stick > mouse > keyboard-move fallback ---
        bool aimStickActive = _uiAim.sqrMagnitude > AimDeadzone * AimDeadzone;

        if (mouse != null && mouse.delta.ReadValue() != Vector2.zero)
            _useMouseAim = true;
        if (aimStickActive)
            _useMouseAim = false; // the aim stick takes over from the mouse

        if (aimStickActive)
        {
            Aim = _uiAim.normalized; // rotate facing toward the right stick
        }
        else if (_useMouseAim && mouse != null && Camera.main != null)
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 dir = (Vector2)world - _aimOrigin;
            if (dir.sqrMagnitude > 0.001f) Aim = dir.normalized;
        }
        else if (kbMove.sqrMagnitude > 0.01f && !_useMouseAim)
        {
            Aim = kbMove.normalized; // keyboard-only fallback: face where we walk
        }
        // (otherwise Aim keeps its last value, so facing holds when idle)

        // --- Fire: dedicated fire button (touch) OR mouse LMB. Aiming never fires. ---
        bool mouseFire = mouse != null && mouse.leftButton.isPressed;
        FireHeld = mouseFire || _uiFire;
    }
}
