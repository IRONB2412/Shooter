using UnityEngine;

/// <summary>
/// Data-driven gun definition. To add a new gun you create ONE asset
/// (Assets > Create > Shooter > Weapon) and fill these fields — no new code.
/// Pistol, AR, SMG and Fire Gun are all just different values here.
/// </summary>
[CreateAssetMenu(menuName = "Shooter/Weapon", fileName = "Weapon")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Pistol";
    public Sprite icon;

    [Header("Firing")]
    [Tooltip("Shots per second.")]
    public float fireRate = 4f;
    public float damage = 20f;
    [Tooltip("Bullets per trigger pull (Fire Gun/shotgun > 1).")]
    public int pelletsPerShot = 1;
    [Tooltip("Random spread in degrees applied to each bullet.")]
    public float spreadAngle = 2f;
    [Tooltip("Hold to keep firing (AR/SMG/Fire Gun) vs tap per shot (Pistol).")]
    public bool automatic = true;

    [Header("Projectile")]
    public float projectileSpeed = 22f;
    [Tooltip("Seconds before the bullet despawns (controls range).")]
    public float projectileLifetime = 1.2f;
    public float projectileRadius = 0.08f;
    public Color projectileColor = Color.yellow;
    [Tooltip("How much destructible terrain one bullet chips (0 = none).")]
    public float terrainChipRadius = 0f;

    [Header("Ammo")]
    public int magazineSize = 12;
    public float reloadTime = 1.2f;

    /// <summary>Seconds between shots, derived from fire rate.</summary>
    public float ShotInterval => fireRate > 0f ? 1f / fireRate : 0.1f;
}
