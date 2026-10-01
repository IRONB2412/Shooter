using UnityEngine;

public enum ThrowableKind { Grenade, Smoke, Flash }

/// <summary>
/// Data-driven throwable. One script (Throwable) reads this and behaves as a
/// grenade, smoke or flash depending on 'kind'. Add variants as assets.
/// </summary>
[CreateAssetMenu(menuName = "Shooter/Throwable", fileName = "Throwable")]
public class ThrowableData : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Grenade";
    public Sprite icon;
    public ThrowableKind kind = ThrowableKind.Grenade;
    public Color color = Color.green;

    [Header("Throw")]
    public float throwSpeed = 10f;
    [Tooltip("Top-down 'air drag' that slows the throw to a stop.")]
    public float drag = 3f;
    public float fuseTime = 1.6f;

    [Header("Effect")]
    [Tooltip("Radius of the blast / cloud / flash.")]
    public float radius = 3.5f;
    [Tooltip("Grenade only: damage at the center (falls off to the edge).")]
    public float damage = 90f;
    [Tooltip("Grenade only: how much destructible terrain the blast carves.")]
    public float terrainCarveRadius = 2.5f;
    [Tooltip("Smoke/Flash only: how long the cloud/flash lasts.")]
    public float effectDuration = 5f;
}
