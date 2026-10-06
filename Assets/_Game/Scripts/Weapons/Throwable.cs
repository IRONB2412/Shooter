using System;
using UnityEngine;

/// <summary>
/// Pooled thrown item. Slides from the thrower, slows to a stop (top-down "drag"),
/// then after the fuse resolves by kind:
///   Grenade -> radial damage + carve destructible terrain.
///   Smoke   -> lingering cloud (vision blocker flag for bots).
///   Flash   -> bright flash that blinds nearby characters (and the screen).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Throwable : MonoBehaviour, IPoolable
{
    [Tooltip("Prefab used for the explosion / cloud / flash visual (has AreaEffect).")]
    [SerializeField] private GameObject areaEffectPrefab;
    [Tooltip("Layers that contain characters (for grenade damage & flash blind).")]
    [SerializeField] private LayerMask characterMask = ~0;

    /// <summary>Flash grenades raise this so the HUD can flash the screen and bots go blind.
    /// Args: world centre, radius, blind duration.</summary>
    public static event Action<Vector2, float, float> OnFlash;
    /// <summary>Smoke raised so bots can treat the area as vision-blocking. Args: centre, radius, duration.</summary>
    public static event Action<Vector2, float, float> OnSmoke;

    private SpriteRenderer _sr;
    private ThrowableData _data;
    private Team _team;
    private GameObject _owner;
    private Vector2 _velocity;
    private float _fuse;
    private Vector2 _castFrom;
    private bool _firstStep;
    private const float Radius = 0.2f;
    private static int _wallMask = -1;

    private void Awake() => _sr = GetComponent<SpriteRenderer>();

    public void Init(ThrowableData data, Vector2 dir, Team team, GameObject owner, Vector2? castFrom = null)
    {
        _castFrom = castFrom ?? (Vector2)transform.position;
        _firstStep = true;
        _data = data;
        _team = team;
        _owner = owner;
        _velocity = dir.normalized * data.throwSpeed;
        _fuse = data.fuseTime;

        _sr.color = data.color;
        transform.localScale = Vector3.one * 0.4f;
    }

    public void OnSpawned() { }
    public void OnDespawned() { _owner = null; }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (_wallMask < 0) _wallMask = LayerMask.GetMask("Obstacle", "Destructible");

        Vector2 pos = transform.position;
        float step = _velocity.magnitude * dt;
        if (step > 0.0001f)
        {
            Vector2 dir = _velocity / (step / dt);
            Vector2 from = pos;
            float dist = step;
            if (_firstStep) { from = _castFrom; dist += Vector2.Distance(_castFrom, pos); }

            var hit = Physics2D.CircleCast(from, Radius, dir, dist, _wallMask);
            if (hit.collider != null)
            {
                // Bounce off the wall, losing some energy; sit just outside the surface.
                pos = hit.point + hit.normal * Radius;
                _velocity = Vector2.Reflect(_velocity, hit.normal) * 0.45f;
            }
            else pos += dir * step;
        }
        _firstStep = false;
        transform.position = pos;

        // Exponential drag (frame-rate independent) so it eases to a stop.
        _velocity *= Mathf.Exp(-_data.drag * dt);

        _fuse -= Time.deltaTime; // real time: the fuse must not stretch on slow frames (dt above is clamped for movement only)
        if (_fuse <= 0f) Detonate();
    }

    private void Detonate()
    {
        Vector2 center = transform.position;

        switch (_data.kind)
        {
            case ThrowableKind.Grenade:
                ApplyGrenade(center);
                GameEffects.Explosion(center); // particle blast replaces the old disk
                break;

            case ThrowableKind.Smoke:
                SmokeZones.Add(center, _data.radius, _data.effectDuration);
                OnSmoke?.Invoke(center, _data.radius, _data.effectDuration);
                SpawnVisual(center, _data.color, _data.radius, _data.effectDuration);
                GameEffects.Smoke(center);
                break;

            case ThrowableKind.Flash:
                OnFlash?.Invoke(center, _data.radius, _data.effectDuration);
                SpawnVisual(center, _data.color, _data.radius, 0.6f);
                GameEffects.Flash(center);
                break;
        }

        Despawn();
    }

    private void ApplyGrenade(Vector2 center)
    {
        // Damage every character in range, falling off with distance.
        var hits = Physics2D.OverlapCircleAll(center, _data.radius, characterMask);
        foreach (var h in hits)
        {
            var dmg = h.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) continue; // friendly fire on purpose: the thrower and teammates get hurt too
            // Walls shield you from the blast.
            if (Physics2D.Linecast(center, h.transform.position, _wallMask).collider != null) continue;

            float dist = Vector2.Distance(center, h.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / _data.radius);
            dmg.TakeDamage(_data.damage * falloff, _owner);
        }

        // Carve any destructible tilemaps in range.
        foreach (var terrain in DestructibleTilemap.All)
            terrain.CarveCircle(center, _data.terrainCarveRadius);
    }

    private void SpawnVisual(Vector2 center, Color color, float radius, float duration)
    {
        if (areaEffectPrefab == null) return;
        var go = PoolManager.Instance.Spawn(areaEffectPrefab, center, Quaternion.identity);
        if (go != null && go.TryGetComponent<AreaEffect>(out var fx))
            fx.Play(color, radius, duration);
    }

    private void Despawn()
    {
        if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
        else gameObject.SetActive(false);
    }
}
