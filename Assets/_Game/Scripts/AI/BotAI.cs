using System.Collections.Generic;
using UnityEngine;

public enum BotDifficulty { Easy, Normal, Hard }

/// <summary>
/// Bot brain with a small state machine:
///   Patrol  – roam around its home spot; scans a vision cone for enemies.
///   Chase   – once it SEES an enemy (range, facing cone, line of sight, not hidden in grass or
///             smoke) it pursues along real paths, strafes and shoots with a little target lead.
///   Search  – lost the enemy: go to where it was last seen, then sweep the area for a while.
///   Heal    – badly hurt: run to the nearest health pack.
///   Return  – nothing found: walk back home and resume patrolling.
/// Movement uses NavGrid (A*) whenever the straight line is blocked, so bots route around walls
/// and through doorways instead of pushing against them.
/// Difficulty scales vision, aim accuracy, reaction time, speed and give-up time.
/// Same body as the player (motor + weapon), just driven by this instead of input.
/// </summary>
[RequireComponent(typeof(CharacterMotor), typeof(WeaponController))]
public class BotAI : MonoBehaviour
{
    enum State { Patrol, Chase, Search, Heal, Return }

    [SerializeField] private BotDifficulty difficulty = BotDifficulty.Normal;
    [Tooltip("Layers that block line of sight and movement (walls + destructible).")]
    [SerializeField] private LayerMask obstacleMask;

    private CharacterMotor _motor;
    private WeaponController _weapon;
    private ThrowableController _throwable;
    private IDamageable _self;
    private Health _hp;
    private float _baseSpeed;

    // Difficulty-derived tuning.
    private float _viewRadius, _viewHalfAngle, _aimErrorDeg, _reactionTime, _preferredRange, _speedMult, _giveUpTime, _patrolRadius, _searchTime, _leadFactor;
    private bool _useThrowables;

    private State _state;
    private Vector2 _home, _patrolPoint;
    private IDamageable _target;
    private Rigidbody2D _targetRb;
    private float _thinkTimer, _blindTimer, _lostTimer, _patrolWait;
    private int _steerSide;
    private float _escapeTimer, _stuckTime, _stuckSampleTimer;
    private Vector2 _escapeDir, _lastSamplePos;

    // Memory of the last place the enemy was seen, and the search that follows.
    private Vector2 _lastSeen, _lastSeenVel;
    private float _searchTimer, _searchPickAt;
    private Vector2 _searchPoint;

    // A target found hiding in grass stays 'spotted' briefly.
    private IDamageable _spottedTarget;
    private float _spottedUntil;

    // Strafing
    private float _strafeSign = 1f, _strafeFlipAt;

    // Healing
    private HealthPickup _healTarget;

    // Path following
    private readonly List<Vector2> _path = new();
    private int _pathIdx;
    private bool _hasPath;
    private float _repathAt;
    private Vector2 _pathGoal;

    public void Configure(BotDifficulty d) { difficulty = d; if (isActiveAndEnabled) ApplyDifficulty(); }

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor>();
        _weapon = GetComponent<WeaponController>();
        _throwable = GetComponent<ThrowableController>();
        _self = GetComponent<IDamageable>();
        _hp = GetComponent<Health>();
        _baseSpeed = _motor.MoveSpeed;
    }

    private void OnEnable()
    {
        ApplyDifficulty();
        _home = transform.position;
        _patrolPoint = _home;
        _state = State.Patrol;
        _patrolWait = 0f;
        _escapeTimer = _stuckTime = 0f;
        _lastSamplePos = _home;
        _hasPath = false;
        _target = null;
        _thinkTimer = Random.Range(0f, 0.3f); // desynchronise bots so their path searches don't pile up
        Throwable.OnFlash += OnFlash;
        if (_hp != null) _hp.OnDamaged += OnHit;
    }

    private void OnDisable()
    {
        Throwable.OnFlash -= OnFlash;
        if (_hp != null) _hp.OnDamaged -= OnHit;
    }

    /// <summary>
    /// Got shot by someone it hasn't spotted (e.g. a player hiding in grass): it now KNOWS where the
    /// shot came from, turns hostile and goes to find the attacker. Seeing them still needs line of
    /// sight — a target deep in grass is only spotted once the bot gets close.
    /// </summary>
    private void OnHit(float amount, GameObject source)
    {
        if (source == null || source == gameObject || _self == null || !_self.IsAlive) return;
        if (!OnScreen(transform.position, ScreenMargin)) return;
        if (!source.TryGetComponent<IDamageable>(out var attacker) || !attacker.IsAlive || attacker.Team == _self.Team) return;

        // Already in a fight with someone it can see? Don't get distracted.
        bool seesTarget = _state == State.Chase && _target != null && _target != attacker && _target.IsAlive
                          && CanSee(_target, ignoreCone: true, _viewRadius * 1.3f);
        if (seesTarget) return;

        _target = attacker;
        _targetRb = attacker.Transform.GetComponent<Rigidbody2D>();
        _lastSeen = attacker.Transform.position;
        _lastSeenVel = Vector2.zero;
        _lostTimer = 0f;
        _hasPath = false;
        _thinkTimer = 0f;
        // Hurt badly bots may still prefer a health pack; otherwise hunt the shooter.
        if (_state != State.Heal) _state = State.Chase;
    }

    private void ApplyDifficulty()
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                _viewRadius = 7f;  _viewHalfAngle = 50f; _aimErrorDeg = 14f; _reactionTime = 0.55f; _searchTime = 3f; _leadFactor = 0f;
                _preferredRange = 6f; _speedMult = 0.8f; _giveUpTime = 2.5f; _patrolRadius = 5f; _useThrowables = false; break;
            case BotDifficulty.Normal:
                _viewRadius = 11f; _viewHalfAngle = 65f; _aimErrorDeg = 6f;  _reactionTime = 0.3f; _searchTime = 5f; _leadFactor = 0.6f;
                _preferredRange = 7f; _speedMult = 0.9f;   _giveUpTime = 3.5f; _patrolRadius = 6f; _useThrowables = true; break;
            default: // Hard
                _viewRadius = 16f; _viewHalfAngle = 80f; _aimErrorDeg = 2f;  _reactionTime = 0.12f; _searchTime = 7f; _leadFactor = 1f;
                _preferredRange = 8f; _speedMult = 1.0f;  _giveUpTime = 5f;   _patrolRadius = 8f; _useThrowables = true; break;
        }
        _motor.MoveSpeed = _baseSpeed * _speedMult; // absolute (no compounding on respawn)
    }

    private void OnFlash(Vector2 center, float radius, float duration)
    {
        Vector2 pos = transform.position;
        if ((pos - center).sqrMagnitude > radius * radius) return;
        if (Physics2D.Linecast(center, pos, obstacleMask).collider != null) return; // walls shield the eyes
        _blindTimer = duration;
    }

    private void Update()
    {
        if (_self == null || !_self.IsAlive) { _motor.MoveInput = Vector2.zero; _weapon.SetTrigger(false); return; }

        UpdateStuck();

        if (_blindTimer > 0f)            // flashed: stumble, can't shoot
        {
            _blindTimer -= Time.deltaTime;
            Patrol(scanning: false);
            _weapon.SetTrigger(false);
            return;
        }

        // Think (perception) on the reaction-time cadence, not every frame.
        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer <= 0f) { _thinkTimer = _reactionTime; Think(); }

        switch (_state)
        {
            case State.Chase:  Chase();  break;
            case State.Search: Search(); break;
            case State.Heal:   Heal();   break;
            default:           Patrol(scanning: true); break;
        }
    }

    // ================================================================== perception
    /// <summary>Perception + state transitions.</summary>
    private void Think()
    {
        // Hurt? Go find a health pack (unless a fight is right in our face and we're not desperate).
        if (_hp != null && _state != State.Heal && _hp.Current < _hp.Max * 0.45f)
        {
            bool desperate = _hp.Current < _hp.Max * 0.3f;
            bool enemyClose = _state == State.Chase && _target != null && _target.IsAlive
                              && Vector2.Distance(_target.Transform.position, transform.position) < 6f;
            var pack = NearestPack(22f);
            if (pack != null && (desperate || !enemyClose)) { _healTarget = pack; _state = State.Heal; _hasPath = false; }
        }

        // Awareness is tied to the screen: a bot that leaves the screen (the player outran it, or it
        // wandered off) forgets the player and goes back to roaming — no hidden "how far is too far".
        if (OnScreen(transform.position, ScreenMargin)) _offScreenTime = 0f;
        else
        {
            _offScreenTime += _reactionTime;
            if (_offScreenTime >= OffScreenGrace && (_target != null || _state == State.Chase || _state == State.Search))
            {
                if (_state == State.Heal) { _target = null; }
                else { LoseTrack(); return; }
            }
        }

        // Still engaged with a visible target? (once chasing we don't need the cone.)
        if ((_state == State.Chase || _state == State.Heal) && _target != null && _target.IsAlive
            && CanSee(_target, ignoreCone: true, _viewRadius * 1.3f))
        {
            if (_state != State.Heal) _state = State.Chase;
            _lostTimer = 0f;
            RememberTarget();
            return;
        }

        // Lost the current target: keep heading to where it was, then search.
        if (_state == State.Chase)
        {
            _lostTimer += _reactionTime;
            if (_lostTimer >= _giveUpTime || _target == null || !_target.IsAlive)
            {
                _state = State.Search;
                _searchTimer = _searchTime;
                _searchPoint = _lastSeen;
                _searchPickAt = 0f;
                _hasPath = false;
            }
        }

        // Scan the vision cone for a (new) enemy.
        var found = ScanCone();
        if (found != null)
        {
            _target = found;
            _targetRb = found.Transform.GetComponent<Rigidbody2D>();
            if (_state != State.Heal) _state = State.Chase;
            _lostTimer = 0f;
            RememberTarget();
        }
    }

    private const float ScreenMargin = 1.0f;   // world units beyond the screen edge that still count as "on screen"
    private const float OffScreenGrace = 0.5f; // seconds off-screen before the bot forgets the player
    private float _offScreenTime;

    /// <summary>True if the point is inside the camera view (plus a small margin).</summary>
    private static bool OnScreen(Vector2 p, float margin)
    {
        var cam = Camera.main;
        if (cam == null) return true;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        return Mathf.Abs(p.x - c.x) <= w + margin && Mathf.Abs(p.y - c.y) <= h + margin;
    }

    /// <summary>Give up completely: unaware again, back to wandering the map.</summary>
    private void LoseTrack() => ResumePatrol(Random.Range(1f, 2f));

    /// <summary>Forget any target and set off to wander somewhere else (after an optional pause to look around).</summary>
    private void ResumePatrol(float pause)
    {
        _target = null;
        _targetRb = null;
        _lostTimer = 0f;
        _spottedTarget = null;
        _state = State.Patrol;
        _patrolWait = pause;
        _weapon.SetTrigger(false);
        _hasPath = false;
        PickPatrolPoint();
    }

    private void RememberTarget()
    {
        _lastSeen = _target.Transform.position;
        _lastSeenVel = _targetRb != null ? _targetRb.linearVelocity : Vector2.zero;
    }

    private HealthPickup NearestPack(float maxDist)
    {
        HealthPickup best = null;
        float bestSqr = maxDist * maxDist;
        Vector2 pos = transform.position;
        foreach (var p in HealthPickup.Active)
        {
            float sqr = ((Vector2)p.transform.position - pos).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = p; }
        }
        return best;
    }

    private IDamageable ScanCone()
    {
        if (!OnScreen(transform.position, ScreenMargin)) return null; // bots off-screen can't spot you
        IDamageable best = null;
        float bestSqr = _viewRadius * _viewRadius;
        Vector2 pos = transform.position;

        foreach (var c in Combatants.All)
        {
            if (c == null || c == _self || !c.IsAlive || c.Team == _self.Team) continue;
            if (!CanSee(c, ignoreCone: false, _viewRadius)) continue;

            float sqr = ((Vector2)c.Transform.position - pos).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = c; }
        }
        return best;
    }

    /// <summary>Range + optional facing-cone + line-of-sight test (smoke and tall grass hide targets).</summary>
    private bool CanSee(IDamageable target, bool ignoreCone, float radius)
    {
        Vector2 pos = transform.position;
        Vector2 tpos = target.Transform.position;
        Vector2 to = tpos - pos;
        float dist = to.magnitude;
        if (dist > radius) return false;

        if (!ignoreCone && dist > 0.1f)
        {
            float facing = Vector2.Angle(_motor.AimDir, to);
            if (facing > _viewHalfAngle) return false;
        }
        // Someone crouched in tall grass is only spotted from close by — but once found, the bot
        // keeps them in its sights for a moment instead of losing them again as it re-positions.
        bool inGrass = GrassField.Conceals(tpos);
        if (inGrass && dist > GrassField.RevealDistance
            && !(target == _spottedTarget && Time.time < _spottedUntil)) return false;
        if (SmokeZones.Blocks(pos, tpos)) return false; // smoke hides targets in/behind it
        if (Physics2D.Raycast(pos, to.normalized, dist, obstacleMask).collider != null) return false;

        if (inGrass && dist <= GrassField.RevealDistance) { _spottedTarget = target; _spottedUntil = Time.time + 2.5f; }
        return true;
    }

    // ================================================================== behaviours
    private void Chase()
    {
        if (_target == null || !_target.IsAlive) { _state = State.Search; _searchTimer = _searchTime; _searchPoint = _lastSeen; _hasPath = false; return; }

        Vector2 pos = transform.position;
        Vector2 targetPos = _target.Transform.position;
        float dist = Vector2.Distance(pos, targetPos);

        bool canSee = CanSee(_target, ignoreCone: true, _viewRadius * 1.3f);

        if (!canSee)
        {
            // Out of sight: run to where it was last seen (a little ahead, along its heading).
            Vector2 goal = _lastSeen + _lastSeenVel * 0.35f;
            Vector2 dir = Navigate(goal);
            _motor.MoveInput = Avoid(dir);
            if (dir.sqrMagnitude > 0.01f) _motor.AimDir = dir;
            _weapon.SetTrigger(false);
            return;
        }

        // Aim a little ahead of a moving target (scaled by difficulty), plus some human error.
        Vector2 aimPoint = targetPos;
        if (_leadFactor > 0f && _targetRb != null && _weapon.Current != null)
        {
            float t = Mathf.Min(dist / Mathf.Max(1f, _weapon.Current.projectileSpeed), 0.6f);
            aimPoint += _targetRb.linearVelocity * t * _leadFactor;
        }
        Vector2 aimDir = (aimPoint - pos).normalized;
        _motor.AimDir = Rotate(aimDir, Random.Range(-_aimErrorDeg, _aimErrorDeg));

        Vector2 dirTo = (targetPos - pos).normalized;
        // Someone hiding in grass: push in close to keep them found instead of backing off.
        float pref = GrassField.Conceals(targetPos) ? Mathf.Min(_preferredRange, 3.4f) : _preferredRange;
        Vector2 move;
        if (dist > pref + 1.5f) move = Navigate(targetPos);                  // close in (paths round walls)
        else if (dist < pref - 1.5f) move = -dirTo;                          // back off
        else move = Strafe(dirTo);                                           // hold range, side-step

        _motor.MoveInput = Avoid(move);
        _weapon.SetTrigger(dist <= _viewRadius);

        if (_useThrowables && _throwable != null && dist < 9f && Random.value < 0.004f)
            _throwable.Throw();
    }

    /// <summary>Side-step around the target; keeps one direction for a while and flips when blocked.</summary>
    private Vector2 Strafe(Vector2 dirTo)
    {
        if (Time.time >= _strafeFlipAt)
        {
            _strafeSign = Random.value < 0.5f ? 1f : -1f;
            _strafeFlipAt = Time.time + Random.Range(1.2f, 2.6f);
        }
        Vector2 side = new Vector2(-dirTo.y, dirTo.x) * _strafeSign;
        if (!PathClear(side, 1.0f, BodyRadius()))
        {
            _strafeSign = -_strafeSign;
            side = -side;
            _strafeFlipAt = Time.time + Random.Range(1.2f, 2.6f);
        }
        return side;
    }

    private void Search()
    {
        _weapon.SetTrigger(false);
        _searchTimer -= Time.deltaTime;
        if (_searchTimer <= 0f) { ResumePatrol(0.6f); return; }

        Vector2 pos = transform.position;
        bool arrived = (_searchPoint - pos).sqrMagnitude < 1.2f * 1.2f;
        bool timeToMove = _searchPickAt > 0f && Time.time >= _searchPickAt;
        if (arrived || timeToMove)
        {
            // Reached the spot (or it's time to look elsewhere): pick a nearby point to check next.
            _searchPoint = RandomWalkablePoint(_lastSeen, 4.5f);
            _searchPickAt = Time.time + Random.Range(2f, 3.5f);
            _hasPath = false;
        }
        else if (_searchPickAt <= 0f) _searchPickAt = Time.time + 4f;

        Vector2 dir = Navigate(_searchPoint);
        _motor.MoveInput = Avoid(dir) * 0.8f;
        // Sweep the view cone left and right while moving so the bot actually looks around.
        Vector2 look = dir.sqrMagnitude > 0.01f ? dir : _motor.AimDir;
        _motor.AimDir = Rotate(look.normalized, Mathf.Sin(Time.time * 2.2f) * 70f);
    }

    private void Heal()
    {
        if (_healTarget == null || !_healTarget.gameObject.activeSelf || (_hp != null && _hp.Current >= _hp.Max * 0.85f))
        {
            _healTarget = null;
            _hasPath = false;
            if (_target != null && _target.IsAlive && CanSee(_target, ignoreCone: true, _viewRadius * 1.3f)) _state = State.Chase;
            else ResumePatrol(0.6f);
            return;
        }

        Vector2 dir = Navigate(_healTarget.transform.position);
        _motor.MoveInput = Avoid(dir);

        // Shoot back if someone is in view; otherwise look where we're running.
        if (_target != null && _target.IsAlive && CanSee(_target, ignoreCone: true, _viewRadius))
        {
            _motor.AimDir = ((Vector2)_target.Transform.position - (Vector2)transform.position).normalized;
            _weapon.SetTrigger(true);
        }
        else
        {
            if (dir.sqrMagnitude > 0.01f) _motor.AimDir = dir;
            _weapon.SetTrigger(false);
        }
    }

    private void Patrol(bool scanning)
    {
        _weapon.SetTrigger(false);

        // Pause briefly at each patrol point, then pick a new one nearby.
        if (_patrolWait > 0f) { _patrolWait -= Time.deltaTime; _motor.MoveInput = Vector2.zero; return; }

        Vector2 to = _patrolPoint - (Vector2)transform.position;
        if (to.magnitude < 0.8f) { _patrolWait = Random.Range(0.4f, 1.3f); PickPatrolPoint(); return; }

        Vector2 dir = Navigate(_patrolPoint);
        _motor.MoveInput = Avoid(dir);
        if (dir.sqrMagnitude > 0.01f) _motor.AimDir = dir; // face where it walks, so the vision cone sweeps around
    }

    /// <summary>Pick the next place to wander to: somewhere well away from here, anywhere on the map.</summary>
    private void PickPatrolPoint()
    {
        var nav = NavGrid.Get();
        Vector2 pos = transform.position;
        if (nav != null && nav.Ready && nav.TryRandomPoint(pos, _patrolRadius + 2f, _patrolRadius * 4f, out var p))
            _patrolPoint = p;
        else
            _patrolPoint = RandomWalkablePoint(pos, _patrolRadius);
        _hasPath = false;
    }

    private Vector2 RandomWalkablePoint(Vector2 around, float radius)
    {
        var nav = NavGrid.Get();
        for (int i = 0; i < 10; i++)
        {
            Vector2 p = around + Random.insideUnitCircle * radius;
            if (nav != null && nav.Ready)
            {
                if (nav.IsWalkable(p)) return p;
            }
            else if (Physics2D.OverlapCircle(p, BodyRadius(), obstacleMask) == null) return p;
        }
        return around;
    }

    // ================================================================== navigation
    /// <summary>
    /// Direction to walk toward 'goal': the straight line if it's clear, otherwise the next
    /// waypoint of an A* path (re-planned every second or when the goal moves).
    /// </summary>
    private Vector2 Navigate(Vector2 goal)
    {
        Vector2 pos = transform.position;
        Vector2 to = goal - pos;
        float dist = to.magnitude;
        if (dist < 0.05f) return Vector2.zero;
        Vector2 dir = to / dist;

        if (dist < 16f && PathClear(dir, dist, BodyRadius())) { _hasPath = false; return dir; }

        var nav = NavGrid.Get();
        if (nav == null || !nav.Ready) return dir;

        if (!_hasPath || Time.time >= _repathAt || (goal - _pathGoal).sqrMagnitude > 2.25f)
        {
            if (nav.CanPathNow)
            {
                _hasPath = nav.FindPath(pos, goal, _path, BodyRadius());
                _pathIdx = 0;
                _pathGoal = goal;
                _repathAt = Time.time + Random.Range(0.7f, 1.1f);
            }
            else _repathAt = Time.time + 0.03f; // someone else planned this frame; try again next frame
        }
        if (!_hasPath || _path.Count == 0) return dir;

        while (_pathIdx < _path.Count - 1 && (_path[_pathIdx] - pos).sqrMagnitude < 0.55f * 0.55f) _pathIdx++;
        Vector2 wp = _path[Mathf.Min(_pathIdx, _path.Count - 1)] - pos;
        return wp.sqrMagnitude < 0.0004f ? dir : wp.normalized;
    }

    private const float ProbeDist = 1.6f;
    private static readonly float[] SteerAngles = { 25f, -25f, 50f, -50f, 80f, -80f, 110f, -110f, 140f, -140f };

    private float _bodyRadius;
    private float BodyRadius()
    {
        if (_bodyRadius <= 0f)
        {
            var col = GetComponent<Collider2D>();
            _bodyRadius = col != null ? Mathf.Max(0.15f, col.bounds.extents.x) : 0.3f;
        }
        return _bodyRadius;
    }

    private bool PathClear(Vector2 dir, float dist, float r)
        => Physics2D.CircleCast(transform.position, r, dir, dist, obstacleMask).collider == null;

    /// <summary>
    /// Local steering: turn a desired heading into one that doesn't run into walls (fans out to the
    /// nearest clear angle, remembering which side it chose so it doesn't flip-flop at corners).
    /// While "unstuck" is active it follows the escape heading instead.
    /// </summary>
    private Vector2 Avoid(Vector2 desired)
    {
        if (desired.sqrMagnitude < 0.0001f) return Vector2.zero;
        float mag = Mathf.Min(1f, desired.magnitude);
        desired.Normalize();

        if (_escapeTimer > 0f) return _escapeDir;

        float r = BodyRadius();
        if (PathClear(desired, ProbeDist, r)) { _steerSide = 0; return desired * mag; }

        for (int i = 0; i < SteerAngles.Length; i += 2)
        {
            float a = SteerAngles[i], b = SteerAngles[i + 1];
            if (_steerSide < 0) { float t = a; a = b; b = t; }
            Vector2 da = Rotate(desired, a);
            if (PathClear(da, ProbeDist, r)) { _steerSide = a > 0 ? 1 : -1; return da * mag; }
            Vector2 db = Rotate(desired, b);
            if (PathClear(db, ProbeDist, r)) { _steerSide = b > 0 ? 1 : -1; return db * mag; }
        }

        // Boxed in: slide along the wall in front.
        var hit = Physics2D.CircleCast(transform.position, r, desired, ProbeDist, obstacleMask);
        if (hit.collider != null)
        {
            Vector2 along = new Vector2(-hit.normal.y, hit.normal.x);
            if (Vector2.Dot(along, desired) < 0f) along = -along;
            return along;
        }
        return desired * mag;
    }

    /// <summary>Detects "pushing but not moving" and picks a clear random heading for a moment.</summary>
    private void UpdateStuck()
    {
        if (_escapeTimer > 0f)
        {
            _escapeTimer -= Time.deltaTime;
            if (_escapeTimer <= 0f) { _stuckTime = 0f; _hasPath = false; if (_state == State.Patrol) PickPatrolPoint(); }
            return;
        }

        _stuckSampleTimer -= Time.deltaTime;
        if (_stuckSampleTimer > 0f) return;
        _stuckSampleTimer = 0.25f;

        Vector2 pos = transform.position;
        bool trying = _motor.MoveInput.sqrMagnitude > 0.1f;
        bool moved = (pos - _lastSamplePos).sqrMagnitude > 0.05f * 0.05f * (_motor.MoveSpeed / 6f);
        _lastSamplePos = pos;

        if (trying && !moved) _stuckTime += 0.25f; else _stuckTime = 0f;
        if (_stuckTime < 0.75f) return;

        // Pick a random clear direction to shake free.
        float r = BodyRadius();
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < 12; i++)
        {
            Vector2 d = Rotate(Vector2.right, start + i * 30f);
            if (PathClear(d, 1.2f, r)) { _escapeDir = d; _escapeTimer = Random.Range(0.4f, 0.8f); return; }
        }
        _stuckTime = 0f;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
