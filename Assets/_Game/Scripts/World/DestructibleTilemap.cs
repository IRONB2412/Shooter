using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Put this on a Tilemap whose tiles should be destroyable. Grenades (and bullets
/// with a chip radius) call CarveCircle to erase tiles in an area. Pair the tilemap
/// with a TilemapCollider2D + CompositeCollider2D so the collision updates itself.
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class DestructibleTilemap : MonoBehaviour
{
    /// <summary>Live registry so explosions don't need FindObjectsByType.</summary>
    public static readonly System.Collections.Generic.List<DestructibleTilemap> All = new();

    /// <summary>Raised after tiles are carved away (centre, radius) so AI navigation can refresh.</summary>
    public static event System.Action<Vector2, float> Carved;

    private Tilemap _map;

    private void Awake() => _map = GetComponent<Tilemap>();
    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>Erase every tile whose cell centre is within 'radius' of the point.</summary>
    public void CarveCircle(Vector2 worldCenter, float radius)
    {
        if (_map == null) _map = GetComponent<Tilemap>();

        float cell = Mathf.Max(_map.cellSize.x, 0.01f);
        int reach = Mathf.CeilToInt(radius / cell) + 1;
        Vector3Int center = _map.WorldToCell(worldCenter);
        float sqr = radius * radius;

        for (int x = -reach; x <= reach; x++)
        for (int y = -reach; y <= reach; y++)
        {
            var c = new Vector3Int(center.x + x, center.y + y, center.z);
            if (!_map.HasTile(c)) continue;

            Vector2 wc = _map.GetCellCenterWorld(c);
            if ((wc - worldCenter).sqrMagnitude <= sqr)
                _map.SetTile(c, null);
        }
        Carved?.Invoke(worldCenter, radius);
    }
}
