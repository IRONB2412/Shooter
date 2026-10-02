using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Scatters non-colliding foliage and rocks over the free floor so the level feels like a
/// forest clearing instead of a bare grid. Deterministic (fixed seed) so every client sees the
/// same layout, and all pieces share one sprite atlas so they batch into very few draw calls.
/// </summary>
public class ForestDecor : MonoBehaviour
{
    [SerializeField] private Tilemap floor;
    [SerializeField] private Sprite[] small;
    [SerializeField] private Sprite[] medium;
    [SerializeField] private Sprite[] large;   // only placed hugging walls / props
    [SerializeField] private int count = 70;
    [SerializeField] private int seed = 1337;
    [Tooltip("Keep decor this far from spawn points so fights never start inside a bush.")]
    [SerializeField] private float spawnClearance = 3.5f;
    [SerializeField] private int sortingOrder = 3; // above floor/walls, below props & characters

    private IEnumerator Start()
    {
        if (floor == null)
        {
            var go = GameObject.Find("Floor");
            if (go != null) floor = go.GetComponent<Tilemap>();
        }
        if (floor == null) yield break;

        yield return new WaitForFixedUpdate(); // let tilemap colliders build first

        var rng = new System.Random(seed);
        var spawns = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        var b = floor.cellBounds;
        int placed = 0, tries = 0;

        while (placed < count && tries++ < count * 25)
        {
            var cell = new Vector3Int(rng.Next(b.xMin, b.xMax), rng.Next(b.yMin, b.yMax), 0);
            if (!floor.HasTile(cell)) continue;

            Vector2 pos = floor.GetCellCenterWorld(cell)
                        + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.8f;

            if (Physics2D.OverlapCircle(pos, 0.75f) != null) continue; // never on walls, props or fighters

            bool tooClose = false;
            foreach (var s in spawns)
                if (((Vector2)s.transform.position - pos).sqrMagnitude < spawnClearance * spawnClearance) { tooClose = true; break; }
            if (tooClose) continue;

            bool nearSolid = Physics2D.OverlapCircle(pos, 1.5f) != null;
            Sprite[] pool = nearSolid && rng.NextDouble() < 0.6 ? large
                          : rng.NextDouble() < 0.5 ? medium : small;
            if (pool == null || pool.Length == 0) continue;

            Spawn(pool[rng.Next(pool.Length)], pos, rng);
            placed++;
        }
    }

    private void Spawn(Sprite sprite, Vector2 pos, System.Random rng)
    {
        var go = new GameObject("Decor");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        // No rotation: the baked drop shadow must keep pointing the same way. Flip for variety instead.
        float s = 0.85f + (float)rng.NextDouble() * 0.3f;
        go.transform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -s : s, s, 1f);
        go.isStatic = true;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
    }
}
