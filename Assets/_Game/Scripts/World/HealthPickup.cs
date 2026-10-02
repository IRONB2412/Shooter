using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A health pack lying on the map. Anyone (the player or a bot) who walks over it while hurt
/// gets healed and the pack disappears; the spawner brings new ones back later.
/// </summary>
[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class HealthPickup : MonoBehaviour
{
    /// <summary>Packs currently lying around (bots look here when they are hurt).</summary>
    public static readonly List<HealthPickup> Active = new();

    public float HealAmount = 40f;
    public System.Action<HealthPickup> Collected;

    private Vector3 _basePos;
    private Vector3 _baseScale;

    private void Awake()
    {
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.55f;
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        _basePos = transform.position;
        Active.Add(this);
    }

    /// <summary>Move the pack to a new resting spot (the bobbing animation is relative to it).</summary>
    public void Place(Vector2 p)
    {
        _basePos = p;
        transform.position = p;
    }

    private void OnDisable() => Active.Remove(this);

    private void Update()
    {
        // Gentle bob + pulse so it catches the eye.
        float t = Time.time * 3f;
        transform.position = _basePos + new Vector3(0f, Mathf.Sin(t) * 0.06f, 0f);
        transform.localScale = _baseScale * (1f + Mathf.Sin(t * 1.3f) * 0.06f);
    }

    private void OnTriggerEnter2D(Collider2D other) => TryTake(other);
    private void OnTriggerStay2D(Collider2D other) => TryTake(other);

    private void TryTake(Collider2D other)
    {
        if (!isActiveAndEnabled) return;
        var hp = other.GetComponentInParent<Health>();
        if (hp == null || !hp.IsAlive || hp.Current >= hp.Max - 0.5f) return; // full health: leave it for later

        hp.Heal(HealAmount);
        GameEffects.Impact(transform.position);
        Collected?.Invoke(this);
        gameObject.SetActive(false);
    }
}
