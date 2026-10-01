using UnityEngine;

/// <summary>
/// Generic pooled visual for explosions, smoke clouds and flashes: a coloured
/// circle that expands to a radius and fades over a duration, then returns to
/// the pool. Purely cosmetic — gameplay effects are applied by Throwable.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AreaEffect : MonoBehaviour, IPoolable
{
    private SpriteRenderer _sr;
    private float _duration, _t, _maxRadius;
    private Color _color;

    private void Awake() => _sr = GetComponent<SpriteRenderer>();

    public void Play(Color color, float radius, float duration)
    {
        _color = color;
        _maxRadius = radius;
        _duration = Mathf.Max(0.05f, duration);
        _t = 0f;
        transform.localScale = Vector3.zero;
    }

    public void OnSpawned() { }
    public void OnDespawned() { }

    private void Update()
    {
        _t += Time.deltaTime;
        float p = _t / _duration;                     // 0..1 progress

        // Quick expand, then hold; fade alpha out over the whole life.
        float scale = _maxRadius * 2f * Mathf.Clamp01(p * 4f);
        transform.localScale = new Vector3(scale, scale, 1f);

        var c = _color;
        c.a = _color.a * (1f - p);
        _sr.color = c;

        if (_t >= _duration)
        {
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
