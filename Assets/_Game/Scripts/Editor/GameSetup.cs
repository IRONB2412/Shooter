using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One-click builder for the whole game: physics layers, demo sprites, tiles,
/// weapon/throwable data, prefabs (bullet/effect/throwable/player/bot) and a
/// playable Game scene with a large destructible map. Idempotent — safe to re-run.
/// Menu: Shooter > Build Game (or call GameSetup.BuildAll()).
/// This lives in an Editor folder so it never ships in a build.
/// </summary>
public static class GameSetup
{
    const string Root = "Assets/_Game";
    const string ArtDir = Root + "/Art";
    const string TileDir = Root + "/Tiles";
    const string DataDir = Root + "/Data";
    const string PrefabDir = Root + "/Prefabs";
    const string SceneDir = Root + "/Scenes";

    // Layer indices (user layers start at 8).
    const int L_Obstacle = 8, L_Destructible = 9, L_Player = 10, L_Enemy = 11;

    [MenuItem("Shooter/Build Game")]
    public static void BuildMenu() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        var log = new StringBuilder();
        EnsureDirs();

        EnsureLayers(); log.AppendLine("Layers OK");

        Sprite circle = MakeSprite(ArtDir + "/circle.png", 64, true);
        Sprite square = MakeSprite(ArtDir + "/square.png", 64, false);
        log.AppendLine("Sprites OK");

        TileBase floor = MakeTile(TileDir + "/Floor.asset", square, new Color(0.16f, 0.17f, 0.22f), Tile.ColliderType.None);
        TileBase wall = MakeTile(TileDir + "/Wall.asset", square, new Color(0.35f, 0.37f, 0.45f), Tile.ColliderType.Grid);
        TileBase destr = MakeTile(TileDir + "/Destructible.asset", square, new Color(0.55f, 0.40f, 0.25f), Tile.ColliderType.Grid);
        log.AppendLine("Tiles OK");

        BuildWeapons(); BuildThrowables(); log.AppendLine("Data OK");

        var bullet = BuildBullet(circle);
        var area = BuildAreaEffect(circle);
        var throwable = BuildThrowablePrefab(circle, area);
        var player = BuildFighter(circle, bullet, throwable, isPlayer: true);
        var bot = BuildFighter(circle, bullet, throwable, isPlayer: false);
        log.AppendLine("Prefabs OK");

        EnsureLevelKit(square);
        log.AppendLine("Level kit OK");

        BuildScene(floor, wall, destr, player, bot);
        log.AppendLine("Scene OK");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "BUILD COMPLETE\n" + log;
    }

    // ---------------------------------------------------------------- helpers
    static void EnsureDirs()
    {
        foreach (var d in new[] { ArtDir, TileDir, DataDir, PrefabDir, SceneDir })
            if (!AssetDatabase.IsValidFolder(d))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(d).Replace("\\", "/"), Path.GetFileName(d));
    }

    static void EnsureLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        void SetLayer(int i, string name) => layers.GetArrayElementAtIndex(i).stringValue = name;
        SetLayer(L_Obstacle, "Obstacle");
        SetLayer(L_Destructible, "Destructible");
        SetLayer(L_Player, "Player");
        SetLayer(L_Enemy, "Enemy");
        tm.ApplyModifiedProperties();
    }

    static Sprite MakeSprite(string path, int size, bool circle)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f, cx = r, cy = r;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool inside = !circle || ((x - cx + 0.5f) * (x - cx + 0.5f) + (y - cy + 0.5f) * (y - cy + 0.5f) <= r * r);
            tex.SetPixel(x, y, inside ? Color.white : Color.clear);
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = size;          // -> sprite is 1x1 world unit
        imp.filterMode = FilterMode.Bilinear;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static TileBase MakeTile(string path, Sprite s, Color c, Tile.ColliderType collider)
    {
        var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
        bool isNew = t == null;
        if (isNew) t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = s; t.color = c; t.colliderType = collider;
        if (isNew) AssetDatabase.CreateAsset(t, path);
        else EditorUtility.SetDirty(t);
        return t;
    }

    /// <summary>Set a private [SerializeField] field via reflection (editor-only convenience).</summary>
    static void Set(Component c, string field, object value)
    {
        var f = c.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning($"No field '{field}' on {c.GetType().Name}"); return; }
        if (f.FieldType == typeof(LayerMask)) value = (LayerMask)(int)value;
        f.SetValue(c, value);
    }

    static T CreateOrLoadSO<T>(string path, System.Action<T> fill) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        bool isNew = a == null;
        if (isNew) a = ScriptableObject.CreateInstance<T>();
        fill(a);
        if (isNew) AssetDatabase.CreateAsset(a, path);
        else EditorUtility.SetDirty(a);
        return a;
    }

    // ---------------------------------------------------------------- data
    static void BuildWeapons()
    {
        CreateOrLoadSO<WeaponData>(DataDir + "/Pistol.asset", w => {
            w.displayName = "Pistol"; w.fireRate = 4f; w.damage = 24f; w.automatic = false;
            w.pelletsPerShot = 1; w.spreadAngle = 1.5f; w.projectileSpeed = 24f; w.projectileLifetime = 1.1f;
            w.projectileColor = new Color(1f, 0.9f, 0.4f); w.magazineSize = 12; w.reloadTime = 1.1f;
        });
        CreateOrLoadSO<WeaponData>(DataDir + "/AR.asset", w => {
            w.displayName = "AR"; w.fireRate = 9f; w.damage = 18f; w.automatic = true;
            w.pelletsPerShot = 1; w.spreadAngle = 3f; w.projectileSpeed = 28f; w.projectileLifetime = 1.4f;
            w.projectileColor = new Color(1f, 0.6f, 0.3f); w.magazineSize = 30; w.reloadTime = 1.8f;
        });
        CreateOrLoadSO<WeaponData>(DataDir + "/SMG.asset", w => {
            w.displayName = "SMG"; w.fireRate = 14f; w.damage = 11f; w.automatic = true;
            w.pelletsPerShot = 1; w.spreadAngle = 6f; w.projectileSpeed = 26f; w.projectileLifetime = 0.9f;
            w.projectileColor = new Color(0.6f, 0.9f, 1f); w.magazineSize = 35; w.reloadTime = 1.6f;
        });
        CreateOrLoadSO<WeaponData>(DataDir + "/FireGun.asset", w => {
            w.displayName = "Fire Gun"; w.fireRate = 16f; w.damage = 6f; w.automatic = true;
            w.pelletsPerShot = 3; w.spreadAngle = 12f; w.projectileSpeed = 12f; w.projectileLifetime = 0.4f;
            w.projectileColor = new Color(1f, 0.4f, 0.1f); w.magazineSize = 100; w.reloadTime = 2.2f;
            w.terrainChipRadius = 0.3f;
        });
    }

    static void BuildThrowables()
    {
        CreateOrLoadSO<ThrowableData>(DataDir + "/Grenade.asset", t => {
            t.displayName = "Grenade"; t.kind = ThrowableKind.Grenade; t.color = new Color(0.3f, 0.8f, 0.3f);
            t.throwSpeed = 11f; t.drag = 3f; t.fuseTime = 1.5f; t.radius = 3.5f; t.damage = 95f; t.terrainCarveRadius = 2.4f;
        });
        CreateOrLoadSO<ThrowableData>(DataDir + "/Smoke.asset", t => {
            t.displayName = "Smoke"; t.kind = ThrowableKind.Smoke; t.color = new Color(0.7f, 0.7f, 0.75f, 0.8f);
            t.throwSpeed = 10f; t.drag = 3.5f; t.fuseTime = 1.2f; t.radius = 4f; t.effectDuration = 6f;
        });
        CreateOrLoadSO<ThrowableData>(DataDir + "/Flash.asset", t => {
            t.displayName = "Flash"; t.kind = ThrowableKind.Flash; t.color = new Color(1f, 1f, 0.9f, 0.95f);
            t.throwSpeed = 12f; t.drag = 3f; t.fuseTime = 1.2f; t.radius = 5f; t.effectDuration = 2f;
        });
    }

    static WeaponData LoadW(string n) => AssetDatabase.LoadAssetAtPath<WeaponData>(DataDir + "/" + n + ".asset");
    static ThrowableData LoadT(string n) => AssetDatabase.LoadAssetAtPath<ThrowableData>(DataDir + "/" + n + ".asset");

    // ---------------------------------------------------------------- prefabs
    static int Mask(params int[] layers) { int m = 0; foreach (var l in layers) m |= 1 << l; return m; }

    static GameObject SavePrefab(GameObject go, string name)
    {
        string path = PrefabDir + "/" + name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------------------------------------------------------------- level kit
    /// <summary>Public entry so the Level Builder can lazily create the kit if missing.</summary>
    public static void EnsureLevelKit()
    {
        var square = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/square.png");
        if (square != null) EnsureLevelKit(square);
    }

    /// <summary>Creates the extra tiles/objects used by the Level Builder (idempotent).</summary>
    static void EnsureLevelKit(Sprite square)
    {
        EnsureDirs();

        // Boundary / edge-blocker tile (solid, indestructible) on the Walls map.
        MakeTile(TileDir + "/EdgeBlocker.asset", square, new Color(0.22f, 0.24f, 0.30f), Tile.ColliderType.Grid);

        // Door prefab: trigger on the root (Default layer), blocker on an Obstacle child.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Door.prefab") == null)
        {
            var root = new GameObject("Door"); // Default layer (0): its trigger won't block bullets/LOS
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = square; sr.color = new Color(0.95f, 0.65f, 0.2f); sr.sortingOrder = 5;
            var trigger = root.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true; trigger.radius = 1.3f;

            var solidGO = new GameObject("Solid");
            solidGO.transform.SetParent(root.transform);
            solidGO.layer = L_Obstacle;
            var box = solidGO.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;

            var door = root.AddComponent<DoorController>();
            Set(door, "solidCollider", box);
            Set(door, "characterMask", Mask(L_Player, L_Enemy));
            SavePrefab(root, "Door");
        }

        // Prop prefab: solid, indestructible, sprite swapped per placement by the builder.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Prop.prefab") == null)
        {
            var go = new GameObject("Prop");
            go.layer = L_Obstacle;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square; sr.color = new Color(0.6f, 0.5f, 0.35f); sr.sortingOrder = 6;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.92f, 0.92f);
            go.AddComponent<Prop>();
            SavePrefab(go, "Prop");
        }

        AssetDatabase.SaveAssets();
    }

    static GameObject BuildBullet(Sprite circle)
    {
        var go = new GameObject("Bullet");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle; sr.color = Color.yellow; sr.sortingOrder = 9;
        var p = go.AddComponent<Projectile>();
        Set(p, "hitMask", Mask(L_Obstacle, L_Destructible, L_Player, L_Enemy));
        return SavePrefab(go, "Bullet");
    }

    static GameObject BuildAreaEffect(Sprite circle)
    {
        var go = new GameObject("AreaEffect");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle; sr.sortingOrder = 8;
        go.AddComponent<AreaEffect>();
        return SavePrefab(go, "AreaEffect");
    }

    static GameObject BuildThrowablePrefab(Sprite circle, GameObject area)
    {
        var go = new GameObject("Throwable");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle; sr.sortingOrder = 9;
        var t = go.AddComponent<Throwable>();
        Set(t, "areaEffectPrefab", area);
        Set(t, "characterMask", Mask(L_Player, L_Enemy));
        return SavePrefab(go, "Throwable");
    }

    static GameObject BuildFighter(Sprite circle, GameObject bullet, GameObject throwable, bool isPlayer)
    {
        var go = new GameObject(isPlayer ? "Player" : "Bot");
        go.layer = isPlayer ? L_Player : L_Enemy;

        // Body: circle, tinted by team.
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle;
        sr.color = isPlayer ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.4f, 0.4f);
        sr.sortingOrder = 10;

        // A small "barrel" child that rotates to show facing/aim direction.
        var barrel = new GameObject("Barrel");
        barrel.transform.SetParent(go.transform);
        barrel.transform.localPosition = Vector3.zero;
        var bsr = barrel.AddComponent<SpriteRenderer>();
        bsr.sprite = circle; bsr.color = new Color(0.9f, 0.9f, 0.9f);
        bsr.sortingOrder = 11;
        barrel.transform.localScale = new Vector3(0.9f, 0.25f, 1f);
        // offset so it points out to the right (+X) which is AimDir's zero angle
        var pivot = new GameObject("AimPivot");
        pivot.transform.SetParent(go.transform);
        pivot.transform.localPosition = Vector3.zero;
        barrel.transform.SetParent(pivot.transform);
        barrel.transform.localPosition = new Vector3(0.45f, 0f, 0f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;

        var hp = go.AddComponent<Health>();
        Set(hp, "team", isPlayer ? Team.Player : Team.Enemy);
        Set(hp, "maxHealth", 100f);

        var motor = go.AddComponent<CharacterMotor>();
        // The player is a little faster than any bot, so running away always works.
        Set(motor, "moveSpeed", isPlayer ? 6.6f : 6f);
        Set(motor, "visual", pivot.transform); // rotate the aim pivot to face AimDir

        var weapon = go.AddComponent<WeaponController>();
        Set(weapon, "projectilePrefab", bullet);
        Set(weapon, "muzzleOffset", 0.6f);
        Set(weapon, "loadout", new System.Collections.Generic.List<WeaponData> {
            LoadW("Pistol"), LoadW("AR"), LoadW("SMG"), LoadW("FireGun") });

        var thrower = go.AddComponent<ThrowableController>();
        Set(thrower, "throwablePrefab", throwable);
        Set(thrower, "items", new System.Collections.Generic.List<ThrowableData> {
            LoadT("Grenade"), LoadT("Smoke"), LoadT("Flash") });

        if (isPlayer)
        {
            go.AddComponent<LocalPlayerDriver>();
        }
        else
        {
            var ai = go.AddComponent<BotAI>();
            Set(ai, "obstacleMask", Mask(L_Obstacle, L_Destructible));
        }

        return SavePrefab(go, isPlayer ? "Player" : "Bot");
    }

    // ---------------------------------------------------------------- scene
    static void BuildScene(TileBase floor, TileBase wall, TileBase destr, GameObject player, GameObject bot)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 9f;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGO.transform.position = new Vector3(0, 0, -10);
        var follow = camGO.AddComponent<CameraFollow>();

        // Optional URP 2D global light (best-effort; sprites render fine without it).
        var lightType = System.Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
        if (lightType != null)
        {
            var lgo = new GameObject("Global Light 2D");
            var l = lgo.AddComponent(lightType);
            var prop = lightType.GetProperty("lightType");
            var enumType = prop?.PropertyType;
            if (enumType != null) prop.SetValue(l, System.Enum.Parse(enumType, "Global"));
        }

        // Grid + tilemaps
        var grid = new GameObject("Grid").AddComponent<Grid>();
        var floorMap = MakeTilemap(grid.transform, "Floor", 0, 0, addCollider: false, destructible: false);
        var wallMap = MakeTilemap(grid.transform, "Walls", 1, L_Obstacle, addCollider: true, destructible: false);
        var destrMap = MakeTilemap(grid.transform, "Destructible", 1, L_Destructible, addCollider: true, destructible: true);

        // ---- Paint a large arena (60x60) ----
        int half = 30;
        FillRect(floorMap, floor, -half, -half, half, half);          // floor everywhere
        // Perimeter walls (thickness 1)
        for (int x = -half; x <= half; x++) { SetTile(wallMap, wall, x, half); SetTile(wallMap, wall, x, -half); }
        for (int y = -half; y <= half; y++) { SetTile(wallMap, wall, half, y); SetTile(wallMap, wall, -half, y); }

        // Interior solid cover blocks (kept clear of spawns / centre lanes)
        FillRect(wallMap, wall, -18, 8, -12, 12);
        FillRect(wallMap, wall, 12, -12, 18, -8);
        FillRect(wallMap, wall, -3, -3, 3, -1);

        // Destructible clusters the player can blow open with grenades
        FillRect(destrMap, destr, 6, 6, 12, 12);
        FillRect(destrMap, destr, -14, -14, -8, -8);
        FillRect(destrMap, destr, 8, -6, 12, -2);
        FillRect(destrMap, destr, -8, 2, -6, 8);

        // Spawn points spread across the map (on open floor)
        Vector2[] spawns = {
            new(0,0), new(22,22), new(-22,22), new(22,-22), new(-22,-22),
            new(0,24), new(0,-24), new(24,0), new(-24,0)
        };
        var spawnObjs = new SpawnPoint[spawns.Length];
        var spawnParent = new GameObject("SpawnPoints").transform;
        for (int i = 0; i < spawns.Length; i++)
        {
            var s = new GameObject("Spawn" + i);
            s.transform.SetParent(spawnParent);
            s.transform.position = spawns[i];
            spawnObjs[i] = s.AddComponent<SpawnPoint>();
        }

        // Systems
        var systems = new GameObject("Systems");
        systems.AddComponent<PoolManager>();
        systems.AddComponent<InputProvider>();
        var gm = systems.AddComponent<GameManager>();
        Set(gm, "playerPrefab", player);
        Set(gm, "botPrefab", bot);
        Set(gm, "spawnPoints", spawnObjs);
        Set(gm, "respawnDelay", 3f);

        // Camera bounds = full map
        follow.SetBounds(new Rect(-half, -half, half * 2, half * 2));

        // Physics: top-down, no gravity
        Physics2D.gravity = Vector2.zero;

        // Force serialization of the freshly-painted tilemaps, then save.
        EditorUtility.SetDirty(floorMap);
        EditorUtility.SetDirty(wallMap);
        EditorUtility.SetDirty(destrMap);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, SceneDir + "/Game.unity");
        AddSceneToBuild(SceneDir + "/Game.unity");
    }

    static Tilemap MakeTilemap(Transform parent, string name, int order, int layer, bool addCollider, bool destructible)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        if (layer > 0) go.layer = layer;
        var map = go.AddComponent<Tilemap>();
        var rend = go.AddComponent<TilemapRenderer>();
        rend.sortingOrder = order;
        if (addCollider)
        {
            go.AddComponent<TilemapCollider2D>();
            OptimizeSetup.MergeColliders(go); // one composite shape instead of a collider per tile
            if (destructible) go.AddComponent<DestructibleTilemap>();
        }
        return map;
    }

    static void SetTile(Tilemap map, TileBase tile, int x, int y) => map.SetTile(new Vector3Int(x, y, 0), tile);

    static void FillRect(Tilemap map, TileBase tile, int x0, int y0, int x1, int y1)
    {
        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
            map.SetTile(new Vector3Int(x, y, 0), tile);
    }

    static void AddSceneToBuild(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == path))
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
