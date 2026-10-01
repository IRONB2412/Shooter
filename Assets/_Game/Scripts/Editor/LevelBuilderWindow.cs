using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Level Builder: create and paint 2D maps from inside the editor.
///
/// Workflow:
///   1. Shooter > Level Builder to open this window.
///   2. "New Level" copies the ready-made Game template (camera, HUD, systems,
///      spawn points all pre-wired) into a new scene so a level is instantly
///      playable — you only design the map.
///   3. Pick a brush (Floor / Wall / Destructible / Spawn / Erase), enable
///      "Paint in Scene View", then left-click/drag in the Scene view to build.
///   4. To use YOUR OWN art: drop a Sprite in "Custom sprite", choose a category,
///      "Create Tile" — it becomes the brush for that category.
///
/// It paints into the active scene's Floor / Walls / Destructible tilemaps, so a
/// scene made by GameSetup (or a New Level copy) works out of the box.
/// </summary>
public class LevelBuilderWindow : EditorWindow
{
    enum Brush { Floor, Wall, Destructible, Spawn, Erase }

    Brush _brush = Brush.Wall;
    bool _painting;

    TileBase _floor, _wall, _destr;

    // Custom-art authoring
    Sprite _customSprite;
    int _customCategory; // 0 Floor, 1 Wall, 2 Destructible
    readonly string[] _categories = { "Floor", "Wall", "Destructible" };

    int _mapHalf = 30;

    [MenuItem("Shooter/Level Builder")]
    static void Open() => GetWindow<LevelBuilderWindow>("Level Builder");

    void OnEnable()
    {
        LoadDefaultTiles();
        SceneView.duringSceneGui += OnScene;
    }

    void OnDisable() => SceneView.duringSceneGui -= OnScene;

    void LoadDefaultTiles()
    {
        _floor ??= AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/Floor.asset");
        _wall  ??= AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/Wall.asset");
        _destr ??= AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/Destructible.asset");
    }

    // =============================================================== window UI
    void OnGUI()
    {
        EditorGUILayout.LabelField("Level Builder", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            HasTilemaps()
                ? "Editing the active scene's tilemaps. Enable painting and click in the Scene view."
                : "Active scene has no Floor/Walls/Destructible tilemaps. Use 'New Level' to start one.",
            HasTilemaps() ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space();
        if (GUILayout.Button("New Level (copy playable template)", GUILayout.Height(28))) NewLevel();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        _brush = (Brush)GUILayout.Toolbar((int)_brush, System.Enum.GetNames(typeof(Brush)));
        _painting = EditorGUILayout.ToggleLeft("Paint in Scene View (left = paint, right = erase)", _painting);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Assigned tiles", EditorStyles.boldLabel);
        _floor = (TileBase)EditorGUILayout.ObjectField("Floor", _floor, typeof(TileBase), false);
        _wall  = (TileBase)EditorGUILayout.ObjectField("Wall",  _wall,  typeof(TileBase), false);
        _destr = (TileBase)EditorGUILayout.ObjectField("Destructible", _destr, typeof(TileBase), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Use your own sprite", EditorStyles.boldLabel);
        _customSprite = (Sprite)EditorGUILayout.ObjectField("Custom sprite", _customSprite, typeof(Sprite), false);
        _customCategory = EditorGUILayout.Popup("Category", _customCategory, _categories);
        using (new EditorGUI.DisabledScope(_customSprite == null))
            if (GUILayout.Button("Create Tile from sprite → assign as brush")) CreateTileFromSprite();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bulk tools", EditorStyles.boldLabel);
        _mapHalf = EditorGUILayout.IntSlider("Map half-size", _mapHalf, 8, 60);
        using (new EditorGUI.DisabledScope(!HasTilemaps()))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill Floor")) FillFloor();
                if (GUILayout.Button("Border Walls")) BorderWalls();
                if (GUILayout.Button("Clear All")) ClearAll();
            }
            if (GUILayout.Button("Add Spawn Point at origin")) AddSpawn(Vector3Int.zero);
        }
    }

    // =============================================================== scene painting
    void OnScene(SceneView view)
    {
        if (!_painting || !HasTilemaps()) return;

        // Keep clicks from selecting/deselecting objects while painting.
        int id = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(id);

        Event e = Event.current;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if (e.button != 0 && e.button != 1) return;

        // Mouse -> world -> cell (intersect the z=0 plane).
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float t = Mathf.Approximately(ray.direction.z, 0) ? 0 : -ray.origin.z / ray.direction.z;
        Vector3 world = ray.GetPoint(t);
        Vector3Int cell = FloorMap().WorldToCell(world);

        bool erase = _brush == Brush.Erase || e.button == 1;
        if (_brush == Brush.Spawn && !erase) AddSpawn(cell);
        else Paint(cell, erase);

        e.Use();
        view.Repaint();
    }

    void Paint(Vector3Int cell, bool erase)
    {
        if (erase)
        {
            SetTile(FloorMap(), cell, null);
            SetTile(WallMap(), cell, null);
            SetTile(DestrMap(), cell, null);
            return;
        }
        switch (_brush)
        {
            case Brush.Floor: SetTile(FloorMap(), cell, _floor); break;
            case Brush.Wall:  SetTile(WallMap(), cell, _wall); break;
            case Brush.Destructible: SetTile(DestrMap(), cell, _destr); break;
        }
    }

    void SetTile(Tilemap map, Vector3Int cell, TileBase tile)
    {
        if (map == null) return;
        Undo.RegisterCompleteObjectUndo(map, "Paint Tile");
        map.SetTile(cell, tile);
        EditorUtility.SetDirty(map);
        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
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
            SetTile(WallMap(), new Vector3Int(x, _mapHalf, 0), _wall);
            SetTile(WallMap(), new Vector3Int(x, -_mapHalf, 0), _wall);
        }
        for (int y = -_mapHalf; y <= _mapHalf; y++)
        {
            SetTile(WallMap(), new Vector3Int(_mapHalf, y, 0), _wall);
            SetTile(WallMap(), new Vector3Int(-_mapHalf, y, 0), _wall);
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
        var parent = GameObject.Find("SpawnPoints");
        if (parent == null) parent = new GameObject("SpawnPoints");
        var s = new GameObject("Spawn" + parent.transform.childCount);
        s.transform.SetParent(parent.transform);
        s.transform.position = FloorMap().GetCellCenterWorld(cell);
        s.AddComponent<SpawnPoint>();
        Undo.RegisterCreatedObjectUndo(s, "Add Spawn");
        EditorSceneManager.MarkSceneDirty(s.scene);
    }

    // =============================================================== custom sprite -> tile
    void CreateTileFromSprite()
    {
        string dir = "Assets/_Game/Tiles";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Game", "Tiles");

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = _customSprite;
        tile.colliderType = _customCategory == 0 ? Tile.ColliderType.None : Tile.ColliderType.Grid;

        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{_customSprite.name}_{_categories[_customCategory]}.asset");
        AssetDatabase.CreateAsset(tile, path);
        AssetDatabase.SaveAssets();

        // Assign the new tile as the active brush tile for its category.
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
            EditorUtility.DisplayDialog("No template", "Run 'Shooter > Build Game' first to create the template scene.", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject("New Level", "Level", "unity", "Where to save the new level?", "Assets/_Game/Scenes");
        if (string.IsNullOrEmpty(path)) return;

        // Copy the fully-wired template, open it, then wipe the demo map to a blank floor.
        AssetDatabase.CopyAsset(template, path);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene(path);

        WallMap().ClearAllTiles();
        DestrMap().ClearAllTiles();
        FloorMap().ClearAllTiles();
        FillFloor();
        BorderWalls();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // Make sure the new level is included in builds.
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log("New level created: " + path);
    }

    // =============================================================== helpers
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
