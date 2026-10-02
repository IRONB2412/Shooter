using UnityEngine;

/// <summary>
/// Drives the local human player. Reads InputProvider each frame and pushes the
/// values into the shared motor / weapon / throwable components. This is the ONLY
/// difference between a human and a bot — everything below it is identical.
/// </summary>
[RequireComponent(typeof(CharacterMotor), typeof(WeaponController))]
public class LocalPlayerDriver : MonoBehaviour
{
    /// <summary>The human-controlled fighter on this machine (null if none is active).</summary>
    public static Transform Current { get; private set; }

    private SpriteRenderer _body;
    private int _bodyOrder;

    private void OnEnable()
    {
        Current = transform;
        // Draw the local player above grass, smoke and bots so they can never be hidden from their own view.
        if (_body == null) _body = GetComponent<SpriteRenderer>();
        if (_body != null) { _bodyOrder = _body.sortingOrder; _body.sortingOrder = SortingOrders.LocalPlayer; }
    }

    private void OnDisable()
    {
        if (Current == transform) Current = null;
        if (_body != null) _body.sortingOrder = _bodyOrder;
    }

    private CharacterMotor _motor;
    private WeaponController _weapon;
    private ThrowableController _throwable;

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor>();
        _weapon = GetComponent<WeaponController>();
        _throwable = GetComponent<ThrowableController>();

        // Aim trail so the player can see exactly where they're shooting.
        if (!TryGetComponent<AimLine>(out _)) gameObject.AddComponent<AimLine>();
    }

    private void Update()
    {
        var inp = InputProvider.Instance;
        if (inp == null) return;

        inp.SetAimOrigin(transform.position);

        _motor.MoveInput = inp.Move;
        if (inp.Aim.sqrMagnitude > 0.001f) _motor.AimDir = inp.Aim;

        _weapon.SetTrigger(inp.FireHeld);
        if (inp.ReloadPressed) _weapon.Reload();
        if (inp.WeaponSlotPressed >= 0) _weapon.Equip(inp.WeaponSlotPressed);

        if (inp.ThrowPressed && _throwable != null) _throwable.Throw();
        if (inp.CycleThrowablePressed && _throwable != null) _throwable.CycleSelection();
    }
}
