using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Live registry of smoke clouds. Anything standing inside a cloud, or whose line of
/// sight passes through one, is hidden from bots until the smoke expires.
/// </summary>
public static class SmokeZones
{
    private struct Zone { public Vector2 center; public float radius; public float expires; }
    private static readonly List<Zone> _zones = new();

    public static void Add(Vector2 center, float radius, float duration)
        => _zones.Add(new Zone { center = center, radius = radius, expires = Time.time + duration });

    /// <summary>True if the straight line a→b touches any active smoke cloud.</summary>
    public static bool Blocks(Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSqr = ab.sqrMagnitude;

        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            var z = _zones[i];
            if (Time.time >= z.expires) { _zones.RemoveAt(i); continue; }

            // Closest point on the segment to the cloud centre.
            float t = lenSqr > 0.0001f ? Mathf.Clamp01(Vector2.Dot(z.center - a, ab) / lenSqr) : 0f;
            Vector2 closest = a + ab * t;
            if ((closest - z.center).sqrMagnitude <= z.radius * z.radius) return true;
        }
        return false;
    }
}
