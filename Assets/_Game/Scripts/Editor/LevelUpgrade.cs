using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One-click level upgrade (idempotent):
///   - turns every solid wall block into a hollow ROOM with an indoor wood floor, two doorways,
///     some crates for cover and a health-pack spot in the middle;
///   - adds tall-grass fields (hide from bots / from the player) and the rare health-pack spawner.
/// Menu: Shooter > Upgrade Level
/// </summary>
public static class LevelUpgrade
{
    const string Art = "Assets/_Game/Art";

    [MenuItem("Shooter/Upgrade Level")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Game.unity");
        AssetDatabase.Refresh();

        var walls = Map("Walls");
        var floor = Map("Floor");
        if (walls == null || floor == null) { Debug.LogError("Game scene needs 'Walls' and 'Floor' tilemaps."); return; }

        var indoor = EnsureIndoorTile();
        int rooms = BuildRooms(walls, floor, indoor);
        WireSystems(floor);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Level upgrade complete: {rooms} room(s) built.");
    }

    static Tilemap Map(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Tilemap>() : null;
    }

    static TileBase EnsureIndoorTile()
    {
        const string path = "Assets/_Game/Tiles/FloorIndoor.asset";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/environment/floor_indoor.png");
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    // ------------------------------------------------------------------ rooms
    static int BuildRooms(Tilemap walls, Tilemap floor, TileBase indoor)
    {
        var crate = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/props/prop_crate.png");
        var propPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Prop.prefab");
        int built = 0;

        foreach (var box in SolidBlocks(walls))
        {
            int x0 = box.xMin, x1 = box.xMax - 1, y0 = box.yMin, y1 = box.yMax - 1;
            int cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;

            // Hollow out the inside.
            for (int x = x0 + 1; x <= x1 - 1; x++)
            for (int y = y0 + 1; y <= y1 - 1; y++)
            {
                walls.SetTile(new Vector3Int(x, y, 0), null);
                floor.SetTile(new Vector3Int(x, y, 0), indoor);
            }

            // Doorway 1: south wall. Doorway 2: alternate sides so rooms have a back way out.
            Door(walls, floor, indoor, new Vector3Int(cx - 1, y0, 0), new Vector3Int(cx, y0, 0));
            bool west = built % 2 == 0;
            int sx = west ? x0 : x1;
            Door(walls, floor, indoor, new Vector3Int(sx, cy, 0), new Vector3Int(sx, cy - 1, 0));

            // Cover crates in the back corners (away from the south door).
            if (propPrefab != null && crate != null)
            {
                PlaceProp(propPrefab, crate, floor, new Vector3Int(x0 + 1, y1 - 1, 0));
                PlaceProp(propPrefab, crate, floor, new Vector3Int(x1 - 1, y1 - 1, 0));
            }

            // Health-pack spot in the middle of the room.
            var spots = GameObject.Find("PickupSpots") ?? new GameObject("PickupSpots");
            var spot = new GameObject("PickupSpot");
            spot.AddComponent<PickupSpot>();
            spot.transform.SetParent(spots.transform);
            spot.transform.position = floor.GetCellCenterWorld(new Vector3Int(cx, cy, 0));

            built++;
        }
        return built;
    }

    static void Door(Tilemap walls, Tilemap floor, TileBase indoor, params Vector3Int[] cells)
    {
        foreach (var c in cells)
        {
            walls.SetTile(c, null);
            floor.SetTile(c, indoor);
        }
    }

    static void PlaceProp(GameObject prefab, Sprite sprite, Tilemap floor, Vector3Int cell)
    {
        var parent = GameObject.Find("Props") ?? new GameObject("Props");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent.transform);
        go.transform.position = floor.GetCellCenterWorld(cell);
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) { sr.sprite = sprite; sr.color = Color.white; }
    }

    /// <summary>Finds fully solid rectangular wall blocks (not the map border) big enough to be a room.</summary>
    static List<BoundsInt> SolidBlocks(Tilemap map)
    {
        var result = new List<BoundsInt>();
        var seen = new HashSet<Vector3Int>();
        var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (var start in map.cellBounds.allPositionsWithin)
        {
            if (!map.HasTile(start) || !seen.Add(start)) continue;

            var queue = new Queue<Vector3Int>();
            queue.Enqueue(start);
            int n = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                n++;
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
                foreach (var d in dirs)
                {
                    var r = p + d;
                    if (map.HasTile(r) && seen.Add(r)) queue.Enqueue(r);
                }
            }

            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            bool solid = n == w * h;                 // a ring (already a room) is not solid
            bool roomSized = w >= 5 && h >= 4 && w * h < 200; // excludes thin bars and the big border ring
            if (solid && roomSized) result.Add(new BoundsInt(x0, y0, 0, w, h, 1));
        }
        return result;
    }

    // ------------------------------------------------------------------ grass + pickups
    static void WireSystems(Tilemap floor)
    {
        var clumps = new[] { "grass_a", "grass_b", "grass_c" };

        var grass = Object.FindFirstObjectByType<GrassPatcher>();
        var grassGo = grass != null ? grass.gameObject : new GameObject("Grass");
        if (grass == null) grass = grassGo.AddComponent<GrassPatcher>();
        var so = new SerializedObject(grass);
        so.FindProperty("floor").objectReferenceValue = floor;
        so.FindProperty("clumpRadius").floatValue = 1.1f;   // matches the 192px clump art
        so.FindProperty("fieldRadius").floatValue = 2.3f;
        var arr = so.FindProperty("clumps");
        arr.arraySize = clumps.Length;
        for (int i = 0; i < clumps.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/props/{clumps[i]}.png");
        so.ApplyModifiedPropertiesWithoutUndo();

        var pickups = Object.FindFirstObjectByType<HealthPickupSpawner>();
        var pickGo = pickups != null ? pickups.gameObject : new GameObject("HealthPickups");
        if (pickups == null) pickups = pickGo.AddComponent<HealthPickupSpawner>();
        var ps = new SerializedObject(pickups);
        ps.FindProperty("floor").objectReferenceValue = floor;
        ps.FindProperty("packSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/props/pickup_health.png");
        ps.ApplyModifiedPropertiesWithoutUndo();
    }
}
