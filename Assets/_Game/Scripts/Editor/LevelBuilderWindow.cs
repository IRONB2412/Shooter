using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Level Builder: create and paint 2D maps from inside the editor.
///
/// Tiles (1×1, painted on the tilemaps):
///   Floor        - walkable ground.
///   Edge         - boundary blocker (solid, indestructible).
///   Wall         - indestructible solid wall.
///   Destructible - wall that grenades/Fire Gun can carve.
/// Objects (placed prefabs):
///   Door         - opens/closes automatically as players/bots approach.
///   Prop         - solid, indestructible obstacle (cabinet, trolley...) whose
///                  sprite you choose per placement via "Prop graphic".
///
/// GRAPHICS: the "Tile graphics" section swaps a tile type's sprite — every tile
/// of that type already placed updates instantly (they share one asset). Assigned
/// sprites are forced to 1×1 so they fill a cell exactly.
/// </summary>
public class LevelBuilderWindow : EditorWindow
{
    enum Brush { Floor, Edge, Wall, Destructible, Door, Prop, Spawn, Erase }

    Brush _brush = Brush.Wall;
    bool _painting;

    TileBase _floor, _edge, _wall, _destr;
    GameObject _doorPrefab, _propPrefab;
    Sprite _propSprite;

    // Custom-art authoring (make a brand-new tile asset from a sprite)
    Sprite _customSprite;
    int _customCategory;
    readonly string[] _categories = { "Floor", "Wall", "Destructible" };

    int _mapHalf = 30;

    [MenuItem("Shooter/Level Builder")]
    static void Open() => GetWindow<LevelBuilderWindow>("Level Builder");

    void OnEnable()
    {
        GameSetup.EnsureLevelKit();      // make sure the tiles/prefabs exist
        LoadAssets();
        SceneView.duringSceneGui += OnScene;
    }

    void OnDisable() => SceneView.duringSceneGui -= OnScene;

    void LoadAssets()
    {
        _floor ??= AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/Floor.asset");
        _edge  ??= AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/EdgeBlocker.asset");
        _wall  ??= AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/Wall.asset");
        _destr ??= AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/Destructible.asset");
        _doorPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Door.prefab");
        _propPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Prop.prefab");
    }

    // =============================================================== window UI
    void OnGUI()
    {
        EditorGUILayout.LabelField("Level Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            HasTilemaps()
                ? "Editing the active scene. Enable painting, then click/drag in the Scene view."
                : "No Floor/Walls/Destructible tilemaps here. Use 'New Level' to start one.",
            HasTilemaps() ? MessageType.Info : MessageType.Warning);

        if (GUILayout.Button("New Level (copy playable template)", GUILayout.Height(28))) NewLevel();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        _brush = (Brush)GUILayout.SelectionGrid((int)_brush, System.Enum.GetNames(typeof(Brush)), 4);
        _painting = EditorGUILayout.ToggleLeft("Paint in Scene View (left = paint, right = erase)", _painting);
        if (_brush == Brush.Prop)
            _propSprite = (Sprite)EditorGUILayout.ObjectField("Prop graphic", _propSprite, typeof(Sprite), false);

        EditorGUILayout.Space();
        DrawReskinSection();

        EditorGUILayout.Space();
        DrawNewTileSection();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bulk tools", EditorStyles.boldLabel);
        _mapHalf = EditorGUILayout.IntSlider("Map half-size", _mapHalf, 8, 60);
        using (new EditorGUI.DisabledScope(!HasTilemaps()))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fill Floor")) FillFloor();
            if (GUILayout.Button("Border Walls")) BorderWalls();
            if (GUILayout.Button("Clear All")) ClearAll();
        }
    }

    /// <summary>Swap a tile type's sprite — updates every placed tile of that type.</summary>
    void DrawReskinSection()
    {
        EditorGUILayout.LabelField("Tile graphics  (changes ALL placed tiles of that type)", EditorStyles.boldLabel);
        _floor = ReskinRow("Floor", _floor);
        _edge  = ReskinRow("Edge / Boundary", _edge);
        _wall  = ReskinRow("Wall (solid)", _wall);
        _destr = ReskinRow("Destructible", _destr);
    }

    TileBase ReskinRow(string label, TileBase tileBase)
    {
        var tile = tileBase as Tile;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(130));
            Sprite cur = tile != null ? tile.sprite : null;
            var next = (Sprite)EditorGUILayout.ObjectField(cur, typeof(Sprite), false);
            if (tile != null && next != cur) ApplyReskin(tile, next);
        }
        return tileBase;
    }

    void ApplyReskin(Tile tile, Sprite sprite)
    {
        if (sprite != null) ForceOneUnit(sprite);
        tile.sprite = sprite;
        tile.color = Color.white;              // show the new art true-to-colour
        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();
        RefreshAllMaps();
    }

    /// <summary>Re-import a sprite so it is exactly 1 world unit (fills one cell).</summary>
    static void ForceOneUnit(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        if (AssetImporter.GetAtPath(path) is not TextureImporter imp) return;
        float ppu = sprite.rect.width > 0 ? sprite.rect.width : sprite.texture.width;
        if (!Mathf.Approximately(imp.spritePixelsPerUnit, ppu))
        {
            imp.spritePixelsPerUnit = ppu;
            imp.SaveAndReimport();
        }
    }

    void DrawNewTileSection()
    {
        EditorGUILayout.LabelField("Create a NEW tile from a sprite", EditorStyles.boldLabel);
        _customSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", _customSprite, typeof(Sprite), false);
        _customCategory = EditorGUILayout.Popup("Category", _customCategory, _categories);
        using (new EditorGUI.DisabledScope(_customSprite == null))
            if (GUILayout.Button("Create Tile → assign as brush")) CreateTileFromSprite();
    }

    // =============================================================== scene painting
    void OnScene(SceneView view)
    {
        if (!_painting || !HasTilemaps()) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Event e = Event.current;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if (e.button != 0 && e.button != 1) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float t = Mathf.Approximately(ray.direction.z, 0) ? 0 : -ray.origin.z / ray.direction.z;
        Vector3Int cell = FloorMap().WorldToCell(ray.GetPoint(t));

        bool erase = _brush == Brush.Erase || e.button == 1;

        if (erase) EraseCell(cell);
        else switch (_brush)
        {
            case Brush.Floor: SetTile(FloorMap(), cell, _floor); break;
            case Brush.Edge:  SetTile(WallMap(), cell, _edge); break;
            case Brush.Wall:  SetTile(WallMap(), cell, _wall); break;
            case Brush.Destructible: SetTile(DestrMap(), cell, _destr); break;
            case Brush.Door:  PlaceObject(_doorPrefab, cell, null, "Doors"); break;
            case Brush.Prop:  PlaceObject(_propPrefab, cell, _propSprite, "Props"); break;
            case Brush.Spawn: AddSpawn(cell); break;
        }

        e.Use();
        view.Repaint();
    }

    void EraseCell(Vector3Int cell)
    {
        SetTile(FloorMap(), cell, null);
        SetTile(WallMap(), cell, null);
        SetTile(DestrMap(), cell, null);
        RemoveObjectsAt(cell);
    }

    void SetTile(Tilemap map, Vector3Int cell, TileBase tile)
    {
        if (map == null) return;
        Undo.RegisterCompleteObjectUndo(map, "Paint Tile");
        map.SetTile(cell, tile);
        EditorUtility.SetDirty(map);
        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
    }

    // =============================================================== object placement
    void PlaceObject(GameObject prefab, Vector3Int cell, Sprite graphic, string container)
    {
        if (prefab == null) return;
        RemoveObjectsAt(cell); // one object per cell

        var parent = GameObject.Find(container) ?? new GameObject(container);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent.transform);
        go.transform.position = FloorMap().GetCellCenterWorld(cell);

        if (graphic != null)
        {
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sprite = graphic; // per-instance graphic (cabinet, trolley...)
        }

        Undo.RegisterCreatedObjectUndo(go, "Place Object");
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    void RemoveObjectsAt(Vector3Int cell)
    {
        Vector3 c = FloorMap().GetCellCenterWorld(cell);
        foreach (var name in new[] { "Doors", "Props" })
        {
            var parent = GameObject.Find(name);
            if (parent == null) continue;
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                var child = parent.transform.GetChild(i);
                if (((Vector2)child.position - (Vector2)c).sqrMagnitude < 0.25f)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    // =============================================================== bulk ops
    void FillFloor()
    {
        for (int x = -_mapHalf; x <= _mapHalf; x++)
        for (int y = -_mapHalf; y <= _mapHalf; y++)
            SetTile(FloorMap(), new Vector3Int(x, y, 0), _floor);
    }

    void BorderWalls()
    {
        for (int x = -_mapHalf; x <= _mapHalf; x++)
        {
            SetTile(WallMap(), new Vector3Int(x, _mapHalf, 0), _edge);
            SetTile(WallMap(), new Vector3Int(x, -_mapHalf, 0), _edge);
        }
        for (int y = -_mapHalf; y <= _mapHalf; y++)
        {
            SetTile(WallMap(), new Vector3Int(_mapHalf, y, 0), _edge);
            SetTile(WallMap(), new Vector3Int(-_mapHalf, y, 0), _edge);
        }
    }

    void ClearAll()
    {
        if (!EditorUtility.DisplayDialog("Clear All", "Erase every tile in this level?", "Clear", "Cancel")) return;
        FloorMap().ClearAllTiles(); WallMap().ClearAllTiles(); DestrMap().ClearAllTiles();
        EditorSceneManager.MarkSceneDirty(FloorMap().gameObject.scene);
    }

    void AddSpawn(Vector3Int cell)
    {
        var parent = GameObject.Find("SpawnPoints") ?? new GameObject("SpawnPoints");
        var s = new GameObject("Spawn" + parent.transform.childCount);
        s.transform.SetParent(parent.transform);
        s.transform.position = FloorMap().GetCellCenterWorld(cell);
        s.AddComponent<SpawnPoint>();
        Undo.RegisterCreatedObjectUndo(s, "Add Spawn");
        EditorSceneManager.MarkSceneDirty(s.scene);
    }

    // =============================================================== custom sprite -> new tile
    void CreateTileFromSprite()
    {
        const string dir = "Assets/_Game/Tiles";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Game", "Tiles");
        ForceOneUnit(_customSprite);

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = _customSprite;
        tile.colliderType = _customCategory == 0 ? Tile.ColliderType.None : Tile.ColliderType.Grid;

        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{_customSprite.name}_{_categories[_customCategory]}.asset");
        AssetDatabase.CreateAsset(tile, path);
        AssetDatabase.SaveAssets();

        if (_customCategory == 0) { _floor = tile; _brush = Brush.Floor; }
        else if (_customCategory == 1) { _wall = tile; _brush = Brush.Wall; }
        else { _destr = tile; _brush = Brush.Destructible; }

        Debug.Log($"Created tile {path} and set it as the {_categories[_customCategory]} brush.");
    }

    // =============================================================== new level
    void NewLevel()
    {
        const string template = "Assets/_Game/Scenes/Game.unity";
        if (!File.Exists(template))
        {
            EditorUtility.DisplayDialog("No template", "Run 'Shooter > Build Game' first.", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject("New Level", "Level", "unity", "Where to save?", "Assets/_Game/Scenes");
        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CopyAsset(template, path);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene(path);

        WallMap().ClearAllTiles(); DestrMap().ClearAllTiles(); FloorMap().ClearAllTiles();
        FillFloor();
        BorderWalls();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("New level created: " + path);
    }

    // =============================================================== helpers
    void RefreshAllMaps()
    {
        foreach (var m in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            m.RefreshAllTiles();
        SceneView.RepaintAll();
    }

    static Tilemap Map(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Tilemap>() : null;
    }
    Tilemap FloorMap() => Map("Floor");
    Tilemap WallMap() => Map("Walls");
    Tilemap DestrMap() => Map("Destructible");
    bool HasTilemaps() => FloorMap() && WallMap() && DestrMap();
}
