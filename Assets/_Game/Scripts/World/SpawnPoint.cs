using UnityEngine;

/// <summary>Marker placed in a level (by hand or the Level Builder) where fighters spawn.</summary>
public class SpawnPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.8f);
    }

    /// <summary>
    /// Picks the spawn that keeps fighters apart: a random point that is at least
    /// <paramref name="minDistance"/> from every living fighter, or — if none qualifies —
    /// the point farthest from the nearest one.
    /// </summary>
    public static Vector3 Pick(SpawnPoint[] points, float minDistance = 14f)
    {
        if (points == null || points.Length == 0) return Vector3.zero;

        float bestDist = -1f;
        Vector3 best = points[0].transform.position;
        int roomy = 0;
        Vector3 chosen = best;

        foreach (var p in points)
        {
            Vector3 pos = p.transform.position;
            float nearest = float.MaxValue;
            foreach (var c in Combatants.All)
            {
                if (c == null || !c.IsAlive) continue;
                float d = Vector2.Distance(pos, c.Transform.position);
                if (d < nearest) nearest = d;
            }

            if (nearest > bestDist) { bestDist = nearest; best = pos; }

            // Reservoir-sample uniformly among the points that are far enough from everyone.
            if (nearest >= minDistance && Random.Range(0, ++roomy) == 0) chosen = pos;
        }
        return roomy > 0 ? chosen : best;
    }
}
