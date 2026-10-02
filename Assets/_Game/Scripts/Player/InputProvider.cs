using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Single source of local input, merging two schemes:
///   - Desktop: keyboard (WASD/arrows) to move, mouse to aim, LMB to fire.
///   - Touch:   on-screen joystick + buttons push values in via the Set* methods.
/// UI widgets and the local player both talk to InputProvider.Instance, so the
/// game plays the same on PC and phone without any per-platform branching elsewhere.
/// </summary>
[DefaultExecutionOrder(-50)] // gather input before LocalPlayerDriver uses it the same frame
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
    public bool CycleThrowablePressed { get; private set; }
    public int WeaponSlotPressed { get; private set; } = -1; // 0..n, or -1 = none

    // ---- Touch/UI backing fields (set by on-screen controls) ----
    private Vector2 _uiMove;   // left stick: movement only
    private Vector2 _uiAim;    // right stick: aim + fire while held
    private bool _uiFire;      // on-screen FIRE button currently held
    private bool _uiFireTap;   // FIRE was pressed since last frame (survives a same-frame release)
    private bool _uiThrowQueued;
    private int _uiWeaponSlot = -1;

    [Tooltip("Aim-stick tilt beyond which the player is considered to be aiming/firing.")]
    private const float AimDeadzone = 0.2f;

    // Where the mouse aims from (the local player sets this each frame).
    private Vector2 _aimOrigin;
    // True while the current left-mouse press began over a HUD control.
    private bool _mousePressOnUI;

    // Fire keys as InputActions: unlike raw key checks, actions record a press even
    // if it was released within the same frame (fast taps at low FPS still fire).
    private InputAction _keyFire, _mouseFire;

    private static bool PointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _keyFire = new InputAction("KeyFire", InputActionType.Button, "<Keyboard>/space");
        _mouseFire = new InputAction("MouseFire", InputActionType.Button, "<Mouse>/leftButton");
    }

    private void OnEnable() { _keyFire?.Enable(); _mouseFire?.Enable(); }
    private void OnDisable() { _keyFire?.Disable(); _mouseFire?.Disable(); }
    private void OnDestroy() { _keyFire?.Dispose(); _mouseFire?.Dispose(); }

    /// <summary>Local player calls this so mouse aim can be computed relative to it.</summary>
    public void SetAimOrigin(Vector2 worldPos) => _aimOrigin = worldPos;

    // ---- Called by touch UI ----
    /// <summary>Left stick: movement direction only.</summary>
    public void SetMove(Vector2 v) => _uiMove = v;
    /// <summary>Right stick: aim/facing direction only (firing is a separate button).</summary>
    public void SetAim(Vector2 v) => _uiAim = v;
    /// <summary>True while the single action button is armed to THROW instead of shoot.</summary>
    public bool ThrowMode { get; private set; }
    public void SetThrowMode(bool on) { ThrowMode = on; _uiFire = false; _uiFireTap = false; }
    public void SetFire(bool held) => _uiFire = held && !ThrowMode;
    /// <summary>A fire press happened (even if already released) — guarantees one shot.</summary>
    public void TapFire()
    {
        if (ThrowMode) _uiThrowQueued = true; // same button: throws when armed, shoots otherwise
        else _uiFireTap = true;
    }
    public void QueueThrow() => _uiThrowQueued = true;
    public void SelectWeapon(int slot) => _uiWeaponSlot = slot;

    private void Update()
    {
        // Reset one-shots; they're true for a single frame.
        ReloadPressed = false;
        ThrowPressed = _uiThrowQueued;
        CycleThrowablePressed = false;
        WeaponSlotPressed = _uiWeaponSlot;
        _uiThrowQueued = false;
        _uiWeaponSlot = -1;

        var kb = Keyboard.current;
        // The Device Simulator (and phones) DISABLE the real mouse — it becomes a touch.
        // Only treat the mouse as a mouse when it is actually an enabled device.
        var mouse = Mouse.current != null && Mouse.current.enabled ? Mouse.current : null;

        // --- Keyboard twin-stick (for laptop / simulator testing) ---
        //   WASD = move, Arrow keys = aim, Space = fire. All independent, all at once.
        Vector2 kbMove = Vector2.zero, kbAim = Vector2.zero;
        bool spaceFire = false;
        if (kb != null)
        {
            if (kb.wKey.isPressed) kbMove.y += 1;
            if (kb.sKey.isPressed) kbMove.y -= 1;
            if (kb.dKey.isPressed) kbMove.x += 1;
            if (kb.aKey.isPressed) kbMove.x -= 1;

            if (kb.upArrowKey.isPressed) kbAim.y += 1;
            if (kb.downArrowKey.isPressed) kbAim.y -= 1;
            if (kb.rightArrowKey.isPressed) kbAim.x += 1;
            if (kb.leftArrowKey.isPressed) kbAim.x -= 1;

            if (kb.rKey.wasPressedThisFrame) ReloadPressed = true;
            if (kb.gKey.wasPressedThisFrame) ThrowPressed = true;
            if (kb.qKey.wasPressedThisFrame) CycleThrowablePressed = true;
            for (int i = 0; i < 4; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) WeaponSlotPressed = i;
            spaceFire = _keyFire.IsPressed() || _keyFire.WasPressedThisFrame();
        }

        // Movement is driven ONLY by the left stick / WASD — never by aim.
        Move = _uiMove.sqrMagnitude > 0.01f ? _uiMove : kbMove.normalized;

        // A real-mouse press that STARTS on a HUD control belongs to the UI for its
        // whole duration: it must not fire a shot or swing the aim toward that control.
        bool mouseTapped = mouse != null && _mouseFire.WasPressedThisFrame();
        bool mouseHeld = mouse != null && _mouseFire.IsPressed();
        if (mouseTapped) _mousePressOnUI = PointerOverUI();
        else if (!mouseHeld) _mousePressOnUI = false;

        // --- Aim priority: aim stick > arrow keys > real mouse cursor > walk direction ---
        bool aimStickActive = _uiAim.sqrMagnitude > AimDeadzone * AimDeadzone;
        if (aimStickActive)
        {
            Aim = _uiAim.normalized;
        }
        else if (kbAim.sqrMagnitude > 0.01f)
        {
            Aim = kbAim.normalized;
        }
        else if (mouse != null && Camera.main != null && !_mousePressOnUI)
        {
            // Desktop: always aim at the cursor (no need to wiggle the mouse first).
            Vector3 world = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 dir = (Vector2)world - _aimOrigin;
            if (dir.sqrMagnitude > 0.001f) Aim = dir.normalized;
        }
        else if (mouse == null && Move.sqrMagnitude > 0.01f)
        {
            // Touch (Android) when not using the aim stick: face where you walk,
            // so run-and-gun shoots forward. Standing still keeps the last facing.
            Aim = Move.normalized;
        }

        // --- Fire: on-screen FIRE button OR Space OR real-mouse LMB (not on UI) ---
        bool mouseFire = !_mousePressOnUI && (mouseHeld || mouseTapped);
        FireHeld = !ThrowMode && (_uiFire || _uiFireTap || spaceFire || mouseFire);
        _uiFireTap = false; // the latched tap counts for exactly one frame
    }
}
