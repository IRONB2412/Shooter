using UnityEngine;

public enum BotDifficulty { Easy, Normal, Hard }

/// <summary>
/// Bot brain. Same body as the player (motor + weapon + throwable) but driven by
/// simple perception + steering instead of input. Difficulty scales perception,
/// aim accuracy, reaction speed and aggression — see ApplyDifficulty().
/// </summary>
[RequireComponent(typeof(CharacterMotor), typeof(WeaponController))]
public class BotAI : MonoBehaviour
{
    [SerializeField] private BotDifficulty difficulty = BotDifficulty.Normal;
    [Tooltip("Layers that block line of sight and movement (walls + destructible).")]
    [SerializeField] private LayerMask obstacleMask;

    private CharacterMotor _motor;
    private WeaponController _weapon;
    private ThrowableController _throwable;
    private IDamageable _self; // Health (offline) or NetworkFighter (online)

    // Difficulty-derived tuning.
    private float _viewRadius, _aimErrorDeg, _reactionTime, _preferredRange, _speedMult;
    private bool _useThrowables;

    private IDamageable _target;
    private float _thinkTimer;
    private float _blindTimer;
    private Vector2 _wanderDir = Vector2.right;
    private float _wanderTimer;

    public void Configure(BotDifficulty d) { difficulty = d; if (isActiveAndEnabled) ApplyDifficulty(); }

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor>();
        _weapon = GetComponent<WeaponController>();
        _throwable = GetComponent<ThrowableController>();
        _self = GetComponent<IDamageable>();
    }

    private void OnEnable()
    {
        ApplyDifficulty();
        Throwable.OnFlash += OnFlash;
    }

    private void OnDisable() => Throwable.OnFlash -= OnFlash;

    private void ApplyDifficulty()
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                _viewRadius = 8f;  _aimErrorDeg = 14f; _reactionTime = 0.55f;
                _preferredRange = 6f; _speedMult = 0.85f; _useThrowables = false; break;
            case BotDifficulty.Normal:
                _viewRadius = 12f; _aimErrorDeg = 6f;  _reactionTime = 0.28f;
                _preferredRange = 7f; _speedMult = 1f;    _useThrowables = true;  break;
            default: // Hard
                _viewRadius = 17f; _aimErrorDeg = 2f;  _reactionTime = 0.12f;
                _preferredRange = 8f; _speedMult = 1.1f;  _useThrowables = true;  break;
        }
        _motor.MoveSpeed *= _speedMult; // scaled once from the prefab's base speed
    }

    private void OnFlash(Vector2 center, float radius, float duration)
    {
        if (((Vector2)transform.position - center).sqrMagnitude <= radius * radius)
            _blindTimer = duration;
    }

    private void Update()
    {
        if (_self == null || !_self.IsAlive) { _motor.MoveInput = Vector2.zero; _weapon.SetTrigger(false); return; }

        // Blinded by a flash: stumble around, can't shoot.
        if (_blindTimer > 0f)
        {
            _blindTimer -= Time.deltaTime;
            Wander();
            _weapon.SetTrigger(false);
            return;
        }

        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer <= 0f)
        {
            _thinkTimer = _reactionTime;
            AcquireTarget();
        }

        if (_target != null && _target.IsAlive)
            Engage();
        else
            Wander();
    }

    /// <summary>Pick the nearest living enemy within view that we can see.</summary>
    private void AcquireTarget()
    {
        _target = null;
        float best = _viewRadius * _viewRadius;
        Vector2 pos = transform.position;

        foreach (var c in Combatants.All)
        {
            if (c == null || c == _self || !c.IsAlive || c.Team == _self.Team) continue;

            Vector2 cpos = c.Transform.position;
            float sqr = (cpos - pos).sqrMagnitude;
            if (sqr > best) continue;
            if (!HasLineOfSight(cpos)) continue;

            best = sqr;
            _target = c;
        }
    }

    private bool HasLineOfSight(Vector2 targetPos)
    {
        Vector2 pos = transform.position;
        Vector2 dir = targetPos - pos;
        var hit = Physics2D.Raycast(pos, dir.normalized, dir.magnitude, obstacleMask);
        return hit.collider == null; // nothing solid in the way
    }

    private void Engage()
    {
        Vector2 pos = transform.position;
        Vector2 targetPos = _target.Transform.position;
        Vector2 toTarget = targetPos - pos;
        float dist = toTarget.magnitude;
        Vector2 dir = toTarget.normalized;

        // Aim with difficulty-based error.
        float err = Random.Range(-_aimErrorDeg, _aimErrorDeg);
        _motor.AimDir = Rotate(dir, err);

        bool canSee = HasLineOfSight(targetPos);

        // Keep near preferred range: approach if far, back off if too close, strafe otherwise.
        Vector2 move;
        if (dist > _preferredRange + 1.5f) move = dir;
        else if (dist < _preferredRange - 1.5f) move = -dir;
        else move = new Vector2(-dir.y, dir.x) * (Mathf.Sin(Time.time * 2f) > 0 ? 1f : -1f);

        _motor.MoveInput = Avoid(move);
        _weapon.SetTrigger(canSee && dist <= _viewRadius);

        // Occasionally lob a throwable at a visible, in-range target.
        if (_useThrowables && canSee && _throwable != null &&
            dist < _throwableRange && Random.value < 0.004f)
            _throwable.Throw();
    }

    private const float _throwableRange = 9f;

    private void Wander()
    {
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
        {
            _wanderTimer = Random.Range(1f, 2.5f);
            _wanderDir = Random.insideUnitCircle.normalized;
        }
        _motor.MoveInput = Avoid(_wanderDir);
        _motor.AimDir = _wanderDir;
        _weapon.SetTrigger(false);
    }

    /// <summary>Nudge the desired direction away from walls straight ahead.</summary>
    private Vector2 Avoid(Vector2 desired)
    {
        Vector2 pos = transform.position;
        var hit = Physics2D.Raycast(pos, desired, 1.2f, obstacleMask);
        if (hit.collider != null)
        {
            // Slide along the wall instead of pushing into it.
            Vector2 along = new Vector2(-hit.normal.y, hit.normal.x);
            return (along + hit.normal * 0.5f).normalized;
        }
        return desired;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
