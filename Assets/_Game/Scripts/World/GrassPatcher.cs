using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Scatters fields of tall grass over open ground. Fields keep clear of walls, props and spawn
/// points so nobody spawns hidden and the grass never blocks a doorway. Deterministic (fixed
/// seed) so every client builds the same layout.
/// </summary>
public class GrassPatcher : MonoBehaviour
{
    [SerializeField] private Tilemap floor;
    [SerializeField] private Sprite[] clumps;
    [SerializeField] private int fields = 9;
    [SerializeField] private int clumpsPerField = 5;
    [SerializeField] private float fieldRadius = 2.3f;
    [SerializeField] private float clumpRadius = 1.1f;
    [SerializeField] private float spawnClearance = 6f;
    [SerializeField] private int seed = 4242;
    [Tooltip("Above characters (10) and bullets (9), below effects.")]
    [SerializeField] private int sortingOrder = 12;

    private IEnumerator Start()
    {
        if (floor == null)
        {
            var go = GameObject.Find("Floor");
            if (go != null) floor = go.GetComponent<Tilemap>();
        }
        if (floor == null || clumps == null || clumps.Length == 0) yield break;

        yield return new WaitForFixedUpdate(); // tilemap colliders must exist before we test against them

        var rng = new System.Random(seed);
        var spawns = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        var b = floor.cellBounds;
        var centres = new System.Collections.Generic.List<Vector2>();
        int tries = 0;

        while (centres.Count < fields && tries++ < fields * 60)
        {
            var cell = new Vector3Int(rng.Next(b.xMin + 4, b.xMax - 4), rng.Next(b.yMin + 4, b.yMax - 4), 0);
            if (!floor.HasTile(cell)) continue;
            Vector2 p = floor.GetCellCenterWorld(cell);

            if (Physics2D.OverlapCircle(p, fieldRadius + 1.4f) != null) continue;   // clear of walls / props
            if (NearAny(spawns, p, spawnClearance)) continue;
            bool crowded = false;
            foreach (var c in centres)
                if ((c - p).sqrMagnitude < 8f * 8f) { crowded = true; break; }
            if (crowded) continue;

            centres.Add(p);
            BuildField(p, rng);
        }
    }

    private static bool NearAny(SpawnPoint[] spawns, Vector2 p, float d)
    {
        foreach (var s in spawns)
            if (((Vector2)s.transform.position - p).sqrMagnitude < d * d) return true;
        return false;
    }

    private void BuildField(Vector2 centre, System.Random rng)
    {
        var fieldGo = new GameObject("GrassField");
        fieldGo.transform.SetParent(transform, false);
        fieldGo.transform.position = centre;
        var field = fieldGo.AddComponent<GrassField>();

        for (int i = 0; i < clumpsPerField; i++)
        {
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float rad = (i == 0 ? 0f : 0.6f + (float)rng.NextDouble() * 0.4f) * fieldRadius;
            Vector2 pos = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;

            var go = new GameObject("Clump");
            go.transform.SetParent(fieldGo.transform, false);
            go.transform.position = pos;
            float s = 1.0f + (float)rng.NextDouble() * 0.35f;
            go.transform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -s : s, s, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = clumps[rng.Next(clumps.Length)];
            sr.sortingOrder = sortingOrder + (pos.y < centre.y ? 1 : 0); // lower clumps overlap higher ones
            field.AddClump(sr, clumpRadius * s);
        }
    }
}
