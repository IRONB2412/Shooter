using UnityEngine;

/// <summary>
/// Pooled bullet. Moves by ray/circle-casting each physics step (no tunneling at
/// high speed), then resolves what it hit: a character (damage), destructible
/// terrain (chip it) or a solid wall (just stop).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Projectile : MonoBehaviour, IPoolable
{
    [Tooltip("Layers a bullet can hit: walls, destructible terrain, characters.")]
    [SerializeField] private LayerMask hitMask = ~0;

    private SpriteRenderer _sr;
    private TrailRenderer _trail;
    private Vector2 _velocity;
    private float _radius;
    private float _damage;
    private float _chipRadius;
    private float _life;
    private Team _team;
    private GameObject _owner;
    private Vector2 _castFrom;
    private bool _firstStep;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _trail = GetComponent<TrailRenderer>(); // optional glow trail
    }

    /// <summary>Configure a freshly-spawned bullet from weapon data.</summary>
    public void Init(WeaponData data, Vector2 dir, Team team, GameObject owner, Vector2? castFrom = null)
    {
        // First sweep starts at the shooter's body, so a muzzle point buried in a wall can't skip it.
        _castFrom = castFrom ?? (Vector2)transform.position;
        _firstStep = true;
        _velocity = dir.normalized * data.projectileSpeed;
        _radius = data.projectileRadius;
        _damage = data.damage;
        _chipRadius = data.terrainChipRadius;
        _life = data.projectileLifetime;
        _team = team;
        _owner = owner;

        _sr.color = data.projectileColor;
        if (_trail != null)
        {
            var c = data.projectileColor;
            _trail.startColor = new Color(c.r, c.g, c.b, 0.9f);
            _trail.endColor = new Color(c.r, c.g, c.b, 0f);
        }
        transform.localScale = Vector3.one * (data.projectileRadius * 2f);
        transform.right = dir; // orient sprite along travel
    }

    public void OnSpawned() { if (_trail != null) _trail.Clear(); } // no streak from the pooled bullet's last position
    public void OnDespawned() { _owner = null; }

    // Moved in Update (not FixedUpdate) so travel is smooth at any frame rate and
    // the sweep distance always matches what is drawn.
    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        _life -= dt;
        if (_life <= 0f) { Despawn(); return; }

        Vector2 pos = transform.position;
        Vector2 dir = _velocity.normalized;
        float step = _velocity.magnitude * dt;

        Vector2 from = pos;
        float dist = step;
        if (_firstStep) { _firstStep = false; from = _castFrom; dist += Vector2.Distance(_castFrom, pos); }

        bool first = from != pos;
        RaycastHit2D hit = Physics2D.CircleCast(from, _radius, dir, dist, hitMask);
        if (hit.collider != null && HandleHit(hit))
            return; // hit something that stops the bullet

        // Spawn frame: stay exactly on the muzzle (where the aim trail starts) — no head start.
        if (!first) transform.position = pos + dir * step;
    }

    /// <returns>true if the bullet should stop (was consumed).</returns>
    private bool HandleHit(RaycastHit2D hit)
    {
        var col = hit.collider;
        if (col.gameObject == _owner) return false; // never hit the shooter

        // Character hit?
        var dmg = col.GetComponentInParent<IDamageable>();
        if (dmg != null)
        {
            if (dmg.Team == _team) return false; // no friendly fire; pass through teammates
            dmg.TakeDamage(_damage, _owner);
            Despawn();
            return true;
        }

        // Destructible terrain?
        var terrain = col.GetComponent<DestructibleTilemap>();
        if (terrain != null && _chipRadius > 0f)
            terrain.CarveCircle(hit.point - dir_(hit) * 0.05f, _chipRadius);

        GameEffects.Impact(hit.point);
        Despawn();
        return true; // solid wall or terrain stops the bullet
    }

    // Small helper so the carve point sits slightly inside the surface.
    private Vector2 dir_(RaycastHit2D hit) => -hit.normal;

    private void Despawn()
    {
        if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
        else gameObject.SetActive(false);
    }
}
