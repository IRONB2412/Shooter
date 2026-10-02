using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable health/damage component for players and bots.
/// Destructible tiles use their own path (see DestructibleTilemap) since they
/// aren't GameObjects, but everything else that fights uses this.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private Team team = Team.Enemy;
    [SerializeField] private float maxHealth = 100f;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public Team Team => team;
    public bool IsAlive => Current > 0f;
    public Transform Transform => transform;

    /// <summary>(current, max) — HUD listens to update the health bar.</summary>
    public event Action<float, float> OnChanged;
    /// <summary>Fired whenever damage lands: (amount, attacker — may be null). Bots use it to notice who shot them.</summary>
    public event Action<float, GameObject> OnDamaged;
    /// <summary>Fired once when health reaches 0. Arg = killer (may be null).</summary>
    public event Action<GameObject> OnDied;

    private void OnEnable()
    {
        Current = maxHealth;
        OnChanged?.Invoke(Current, maxHealth);
        Combatants.Register(this);
    }

    private void OnDisable() => Combatants.Unregister(this);

    public void SetTeam(Team t) => team = t;

    public void TakeDamage(float amount, GameObject source)
    {
        if (!IsAlive || amount <= 0f) return;

        Current = Mathf.Max(0f, Current - amount);
        OnChanged?.Invoke(Current, maxHealth);
        GameEffects.Blood(transform.position);
        OnDamaged?.Invoke(amount, source);

        if (Current <= 0f)
            OnDied?.Invoke(source);
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        OnChanged?.Invoke(Current, maxHealth);
    }

    /// <summary>Full reset used by the respawn flow.</summary>
    public void Revive()
    {
        Current = maxHealth;
        OnChanged?.Invoke(Current, maxHealth);
    }
}
