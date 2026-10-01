using UnityEngine;

/// <summary>Teams decide who can hurt whom (no friendly fire).</summary>
public enum Team { Player, Enemy, Neutral }

/// <summary>
/// Anything that can fight: players and bots, offline (Health) or networked
/// (NetworkFighter). Sharing this interface lets weapons, bots and bullets treat
/// both the same, so the gameplay code is identical online and offline.
/// </summary>
public interface IDamageable
{
    Team Team { get; }
    bool IsAlive { get; }
    /// <summary>The fighter's transform (for aiming / distance checks).</summary>
    Transform Transform { get; }
    /// <param name="amount">Damage in HP.</param>
    /// <param name="source">Who caused it (for scoring / avoiding self-hits). May be null.</param>
    void TakeDamage(float amount, GameObject source);
}

/// <summary>Live registry of every fighter, so bots can find targets cheaply.</summary>
public static class Combatants
{
    public static readonly System.Collections.Generic.List<IDamageable> All = new();
    public static void Register(IDamageable c) { if (!All.Contains(c)) All.Add(c); }
    public static void Unregister(IDamageable c) => All.Remove(c);
}
