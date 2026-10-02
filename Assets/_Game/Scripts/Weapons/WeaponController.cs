using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds a loadout of WeaponData and fires the currently equipped one.
/// Shared by players and bots — the driver just tells it whether the trigger is
/// held; automatic vs semi-auto, spread, ammo and reload are handled here.
/// Aim direction is read from the CharacterMotor on the same object.
/// </summary>
[RequireComponent(typeof(CharacterMotor))]
public class WeaponController : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("How far in front of the character bullets spawn.")]
    [SerializeField] private float muzzleOffset = 0.6f;
    [SerializeField] private List<WeaponData> loadout = new();

    public event Action<WeaponData, int> OnAmmoChanged;   // (weapon, currentAmmo)
    public event Action<WeaponData> OnWeaponChanged;

    public WeaponData Current => (_index >= 0 && _index < loadout.Count) ? loadout[_index] : null;
    public float MuzzleOffset => muzzleOffset;
    public int CurrentAmmo { get; private set; }
    public IReadOnlyList<WeaponData> Loadout => loadout;

    private CharacterMotor _motor;
    private Team _team = Team.Enemy;
    private int _index = -1;
    private float _cooldown;
    private float _reloadTimer;
    private bool _reloading;
    private bool _triggerWasHeld;

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor>();
        if (TryGetComponent<IDamageable>(out var dmg)) _team = dmg.Team;
    }

    private void OnEnable()
    {
        // Refresh team (respawn may have changed it) and equip first weapon.
        if (TryGetComponent<IDamageable>(out var dmg)) _team = dmg.Team;
        if (loadout.Count > 0) Equip(0);
    }

    public void Equip(int slot)
    {
        if (slot < 0 || slot >= loadout.Count || slot == _index) return;
        _index = slot;
        _reloading = false;
        CurrentAmmo = Current.magazineSize;
        OnWeaponChanged?.Invoke(Current);
        OnAmmoChanged?.Invoke(Current, CurrentAmmo);
    }

    public void Reload()
    {
        if (_reloading || Current == null || CurrentAmmo >= Current.magazineSize) return;
        _reloading = true;
        _reloadTimer = Current.reloadTime;
    }

    /// <summary>Called by the driver every frame with the trigger state.</summary>
    public void SetTrigger(bool held)
    {
        if (Current == null) return;

        if (_reloading)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                CurrentAmmo = Current.magazineSize;
                _reloading = false;
                OnAmmoChanged?.Invoke(Current, CurrentAmmo);
            }
            _triggerWasHeld = held;
            return;
        }

        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        bool wantsToFire = Current.automatic ? held : (held && !_triggerWasHeld);
        if (wantsToFire && _cooldown <= 0f)
        {
            if (CurrentAmmo > 0) Fire();
            else Reload(); // auto-reload when the mag is empty
        }

        _triggerWasHeld = held;
    }

    private void Fire()
    {
        Vector2 aim = _motor.AimDir.sqrMagnitude > 0.001f ? _motor.AimDir.normalized : Vector2.right;
        Vector2 origin = (Vector2)transform.position + aim * muzzleOffset;

        GameEffects.Muzzle(origin, aim);

        for (int i = 0; i < Current.pelletsPerShot; i++)
        {
            float spread = UnityEngine.Random.Range(-Current.spreadAngle, Current.spreadAngle);
            Vector2 dir = Rotate(aim, spread);

            var go = PoolManager.Instance.Spawn(projectilePrefab, origin, Quaternion.identity);
            if (go != null && go.TryGetComponent<Projectile>(out var p))
                p.Init(Current, dir, _team, gameObject, transform.position);
        }

        CurrentAmmo--;
        _cooldown = Current.ShotInterval;
        OnAmmoChanged?.Invoke(Current, CurrentAmmo);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
