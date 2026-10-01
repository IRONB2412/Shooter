using UnityEngine;

/// <summary>
/// Drives the local human player. Reads InputProvider each frame and pushes the
/// values into the shared motor / weapon / throwable components. This is the ONLY
/// difference between a human and a bot — everything below it is identical.
/// </summary>
[RequireComponent(typeof(CharacterMotor), typeof(WeaponController))]
public class LocalPlayerDriver : MonoBehaviour
{
    private CharacterMotor _motor;
    private WeaponController _weapon;
    private ThrowableController _throwable;

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor>();
        _weapon = GetComponent<WeaponController>();
        _throwable = GetComponent<ThrowableController>();
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
    }
}
