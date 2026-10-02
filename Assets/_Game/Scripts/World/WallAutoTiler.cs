using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Turns the plain Wall / Edge tiles painted in the level into a top-down "roof" look: solid flat
/// tops with a thin dark rim only where the wall meets open ground (plus a soft drop shadow), like
/// the walls of a house seen from above. Runs once at load, so levels are still painted with the
/// ordinary Wall and EdgeBlocker tiles. Colliders are unchanged (Grid, one cell per tile).
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class WallAutoTiler : MonoBehaviour
{
    // Neighbour bits. Index into the sprite arrays = Canonical(mask).
    const int N = 1, E = 2, S = 4, W = 8, NE = 16, SE = 32, SW = 64, NW = 128;

    [SerializeField] private TileBase edgeTile;
    [Tooltip("256 entries indexed by neighbour mask (filled by Shooter > Apply Art Pass).")]
    [SerializeField] private Sprite[] wallSprites;
    [SerializeField] private Sprite[] edgeSprites;

    /// <summary>A diagonal neighbour only changes the look when both adjacent sides are connected.</summary>
    public static int Canonical(int k)
    {
        if (!((k & N) != 0 && (k & E) != 0)) k &= ~NE;
        if (!((k & S) != 0 && (k & E) != 0)) k &= ~SE;
        if (!((k & S) != 0 && (k & W) != 0)) k &= ~SW;
        if (!((k & N) != 0 && (k & W) != 0)) k &= ~NW;
        return k;
    }

    private void Start() => Apply();

    public void Apply()
    {
        var map = GetComponent<Tilemap>();
        if (wallSprites == null || wallSprites.Length < 256) return;

        var cache = new Dictionary<Sprite, Tile>();
        var changes = new List<TileChangeData>();

        map.CompressBounds();
        foreach (var pos in map.cellBounds.allPositionsWithin)
        {
            var tile = map.GetTile(pos);
            if (tile == null) continue;

            bool isEdge = edgeTile != null && tile == edgeTile;
            var sprites = isEdge && edgeSprites != null && edgeSprites.Length >= 256 ? edgeSprites : wallSprites;
            var sprite = sprites[Canonical(Mask(map, pos))];
            if (sprite == null) continue;

            if (!cache.TryGetValue(sprite, out var variant))
            {
                variant = ScriptableObject.CreateInstance<Tile>();
                variant.sprite = sprite;
                variant.colliderType = Tile.ColliderType.Grid; // keep the solid cell collision
                variant.flags = TileFlags.LockAll;
                cache[sprite] = variant;
            }
            changes.Add(new TileChangeData(pos, variant, Color.white, Matrix4x4.identity));
        }

        // Neighbours were all read above, so replacing in one batch can't skew the masks.
        map.SetTiles(changes.ToArray(), true);
    }

    private static int Mask(Tilemap map, Vector3Int p)
    {
        int k = 0;
        if (map.HasTile(p + new Vector3Int(0, 1, 0))) k |= N;
        if (map.HasTile(p + new Vector3Int(1, 0, 0))) k |= E;
        if (map.HasTile(p + new Vector3Int(0, -1, 0))) k |= S;
        if (map.HasTile(p + new Vector3Int(-1, 0, 0))) k |= W;
        if (map.HasTile(p + new Vector3Int(1, 1, 0))) k |= NE;
        if (map.HasTile(p + new Vector3Int(1, -1, 0))) k |= SE;
        if (map.HasTile(p + new Vector3Int(-1, -1, 0))) k |= SW;
        if (map.HasTile(p + new Vector3Int(-1, 1, 0))) k |= NW;
        return k;
    }
}
