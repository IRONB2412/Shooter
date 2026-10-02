using UnityEngine;

/// <summary>
/// Central spawner for particle effects. Gameplay scripts call the static helpers
/// (e.g. GameEffects.Muzzle(pos, dir)); if no GameEffects is in the scene the
/// calls are simply ignored, so nothing hard-depends on it.
/// </summary>
public class GameEffects : MonoBehaviour
{
    public static GameEffects Instance { get; private set; }

    [SerializeField] private GameObject muzzle;
    [SerializeField] private GameObject impact;
    [SerializeField] private GameObject explosion;
    [SerializeField] private GameObject blood;
    [SerializeField] private GameObject smoke;
    [SerializeField] private GameObject flash;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Spam-prone effects (rapid-fire impacts / blood) are rate-capped so low-end
    // phones don't drown in particles. Value = earliest time the next one may spawn.
    private float _nextImpact, _nextBlood;
    private const float ImpactInterval = 0.035f; // ≈ 28 per second max
    private const float BloodInterval = 0.06f;   // ≈ 16 per second max

    private void Play(GameObject prefab, Vector2 pos, float angle)
    {
        if (prefab == null || PoolManager.Instance == null) return;
        PoolManager.Instance.Spawn(prefab, pos, Quaternion.Euler(0, 0, angle));
    }

    private static bool Throttle(ref float next, float interval)
    {
        if (Time.time < next) return false;
        next = Time.time + interval;
        return true;
    }

    // ---- static, null-safe entry points ----
    public static void Muzzle(Vector2 pos, Vector2 dir)
        => Instance?.Play(Instance.muzzle, pos, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    public static void Impact(Vector2 pos)
    {
        if (Instance != null && Throttle(ref Instance._nextImpact, ImpactInterval)) Instance.Play(Instance.impact, pos, 0f);
    }
    public static void Explosion(Vector2 pos)
    {
        Instance?.Play(Instance.explosion, pos, 0f);
        var cam = Camera.main;
        if (cam != null) // shake falls off with distance from the blast
        {
            float d = Vector2.Distance(cam.transform.position, pos);
            CameraFollow.Shake(0.45f * Mathf.Clamp01(1f - d / 20f), 0.35f);
        }
    }
    public static void Blood(Vector2 pos)
    {
        if (Instance != null && Throttle(ref Instance._nextBlood, BloodInterval)) Instance.Play(Instance.blood, pos, 0f);
    }
    public static void Smoke(Vector2 pos) => Instance?.Play(Instance.smoke, pos, 0f);
    public static void Flash(Vector2 pos) => Instance?.Play(Instance.flash, pos, 0f);
}
