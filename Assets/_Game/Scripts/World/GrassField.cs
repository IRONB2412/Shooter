using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cluster of tall-grass clumps drawn ABOVE characters. Whoever stands inside is hidden from
/// view; bots also cannot see a target inside grass unless it is very close (see
/// <see cref="Conceals"/>). While the local player is inside, the whole field turns see-through so
/// they can still see themself.
/// </summary>
public class GrassField : MonoBehaviour
{
    private struct Circle { public Vector2 c; public float r; }

    /// <summary>All grass circles in the level, for AI line-of-sight checks.</summary>
    private static readonly List<GrassField> All = new();

    /// <summary>Beyond this distance a bot cannot spot a target standing in grass.</summary>
    public const float RevealDistance = 2.4f;

    private readonly List<Circle> _circles = new();
    private readonly List<SpriteRenderer> _renderers = new();
    private float _alpha = 1f;

    [SerializeField, Range(0.2f, 1f)] private float insideAlpha = 0.45f;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>Called by the spawner for each clump placed in this field.</summary>
    public void AddClump(SpriteRenderer sr, float radius)
    {
        _renderers.Add(sr);
        _circles.Add(new Circle { c = sr.transform.position, r = radius });
    }

    private bool Contains(Vector2 p)
    {
        foreach (var k in _circles)
            if ((p - k.c).sqrMagnitude <= k.r * k.r) return true;
        return false;
    }

    /// <summary>True if the point is standing in any grass.</summary>
    public static bool Conceals(Vector2 p)
    {
        foreach (var f in All)
            if (f.Contains(p)) return true;
        return false;
    }

    private void Update()
    {
        var player = LocalPlayerDriver.Current;
        float target = player != null && Contains(player.position) ? insideAlpha : 1f;
        if (Mathf.Approximately(_alpha, target)) return;

        _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime * 4f);
        foreach (var sr in _renderers)
        {
            var c = sr.color; c.a = _alpha; sr.color = c;
        }
    }
}
