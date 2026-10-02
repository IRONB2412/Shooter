using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>
/// Applies the Kenney-based art pass to the project (idempotent, safe to re-run):
///   1. texture import settings for every art folder,
///   2. Sprite Atlases (gameplay + UI) so sprites batch into very few draw calls,
///   3. particle effects rebuilt on the shared FX sheet,
///   4. weapon-pose / random skins on players and bots, glow trail on bullets,
///   5. forest decor + background colour in the Game scene, forest backdrop in the Menu.
/// Folder layout (Assets/_Game/Art): characters, environment, props, fx, ui, backgrounds, atlases.
/// Menu: Shooter > Apply Art Pass
/// </summary>
public static class ArtPipeline
{
    const string Art = "Assets/_Game/Art";
    const string Prefabs = "Assets/_Game/Prefabs";

    [MenuItem("Shooter/Apply Art Pass")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        ConfigureImporters();
        CreateAtlases();
        EffectsSetup.BuildAll();          // opens + saves the Game scene
        WirePrefabs();
        SetupGameScene();
        SetupMenuScene();

        // Old single-circle particle material is no longer used.
        if (AssetDatabase.LoadAssetAtPath<Material>(Art + "/FXParticle.mat") != null)
            AssetDatabase.DeleteAsset(Art + "/FXParticle.mat");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Art pass complete.");
    }

    // `a.GetComponent<T>() ?? a.AddComponent<T>()` is unsafe with Unity objects (fake-null), so use TryGetComponent.
    static T GetOrAdd<T>(GameObject go) where T : Component
        => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    // ------------------------------------------------------------------ 1. importers
    static void ConfigureImporters()
    {
        Sprites("characters", 48);
        Sprites("environment", 64);
        Sprites("props", 64);
        Sprites("backgrounds", 100, maxSize: 2048, packed: false);

        // FX sheet is sampled by particle materials (not a sprite).
        string sheet = Art + "/fx/fx_sheet.png";
        if (AssetImporter.GetAtPath(sheet) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.SaveAndReimport();
        }
    }

    static void Sprites(string sub, float ppu, int maxSize = 1024, bool packed = true)
    {
        string folder = Art + "/" + sub;
        if (!AssetDatabase.IsValidFolder(folder)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = maxSize;
            // Atlased sources stay uncompressed; the atlas texture itself is what gets compressed.
            if (packed) ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ 2. atlases
    static void CreateAtlases()
    {
        EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2; // pack in the editor too

        string dir = Art + "/atlases";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Art, "atlases");

        MakeAtlas(dir + "/Gameplay.spriteatlasv2", "characters", "environment", "props");
        MakeAtlas(dir + "/UI.spriteatlasv2", "ui");
    }

    static void MakeAtlas(string path, params string[] folders)
    {
        var objs = new List<Object>();
        foreach (var f in folders)
        {
            var folder = AssetDatabase.LoadAssetAtPath<Object>(Art + "/" + f);
            if (folder != null) objs.Add(folder);
        }

        // Always start from a fresh asset so the packed folders exactly match this list (settings are re-applied below).
        var asset = new SpriteAtlasAsset();
        asset.Add(objs.ToArray());
        SpriteAtlasAsset.Save(asset, path);
        AssetDatabase.ImportAsset(path);

        if (AssetImporter.GetAtPath(path) is SpriteAtlasImporter imp)
        {
            imp.packingSettings = new SpriteAtlasPackingSettings
            {
                padding = 4,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true,
                blockOffset = 1,
            };
            imp.textureSettings = new SpriteAtlasTextureSettings
            {
                generateMipMaps = false,
                filterMode = FilterMode.Bilinear,
                sRGB = true,
                readable = false,
            };
            imp.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ 4. prefabs
    static Sprite Load(string rel) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/characters/" + rel);

    static CharacterSkin.Skin PlayerSkin() => new CharacterSkin.Skin
    {
        pistol = Load("player.png"), rifle = Load("player_rifle.png"), smg = Load("player_smg.png"),
    };

    static CharacterSkin.Skin BotSkin(string name) => new CharacterSkin.Skin
    {
        pistol = Load(name == "hitman" ? "bot.png" : $"bot_{name}_pistol.png"),
        rifle = Load($"bot_{name}_rifle.png"),
        smg = Load($"bot_{name}_smg.png"),
    };

    static void WirePrefabs()
    {
        var player = new[] { PlayerSkin() };
        var bots = new[] { BotSkin("hitman"), BotSkin("robot"), BotSkin("zombie"), BotSkin("brown") };

        WireSkin(Prefabs + "/Player.prefab", player, false);
        WireSkin(Prefabs + "/PlayerNet.prefab", player, false);
        WireSkin(Prefabs + "/Bot.prefab", bots, true);
        WireSkin(Prefabs + "/BotNet.prefab", bots, false); // networked bots look identical on every client

        WireBulletTrail(Prefabs + "/Bullet.prefab");
    }

    static void WireSkin(string path, CharacterSkin.Skin[] skins, bool random)
    {
        if (!File.Exists(path)) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var skin = GetOrAdd<CharacterSkin>(root);
            var sr = root.GetComponentInChildren<SpriteRenderer>(true);

            var so = new SerializedObject(skin);
            so.FindProperty("target").objectReferenceValue = sr;
            so.FindProperty("randomPerSpawn").boolValue = random;
            var arr = so.FindProperty("skins");
            arr.arraySize = skins.Length;
            for (int i = 0; i < skins.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("pistol").objectReferenceValue = skins[i].pistol;
                e.FindPropertyRelative("rifle").objectReferenceValue = skins[i].rifle;
                e.FindPropertyRelative("smg").objectReferenceValue = skins[i].smg;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void WireBulletTrail(string path)
    {
        if (!File.Exists(path)) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var trail = GetOrAdd<TrailRenderer>(root);
            trail.time = 0.09f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = 0.14f;
            trail.numCapVertices = 2;
            trail.sortingOrder = 8;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = TrailMaterial();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Material TrailMaterial()
    {
        string path = Art + "/fx/FX_Trail.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Mobile/Particles/Additive"));
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    // ------------------------------------------------------------------ 5. scenes
    static void SetupGameScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Game.unity");

        // Same green as the grass: hides hairline gaps between tilemap tiles.
        var cam = Camera.main;
        if (cam != null) cam.backgroundColor = new Color(0.16f, 0.58f, 0.34f);

        var existing = Object.FindFirstObjectByType<ForestDecor>();
        var decor = existing != null ? existing.gameObject : new GameObject("ForestDecor");
        var comp = GetOrAdd<ForestDecor>(decor);

        var floor = GameObject.Find("Floor");
        var so = new SerializedObject(comp);
        if (floor != null) so.FindProperty("floor").objectReferenceValue = floor.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        SetSprites(so.FindProperty("small"), "decor_bush_green_small", "decor_bush_orange_small", "decor_rock_a", "decor_rock_b", "decor_rock_c");
        SetSprites(so.FindProperty("medium"), "decor_bush_green_medium", "decor_bush_orange_medium", "decor_rock_a", "decor_rock_c");
        SetSprites(so.FindProperty("large"), "decor_bush_green_large", "decor_bush_orange_large");
        so.ApplyModifiedPropertiesWithoutUndo();

        WireWalls();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>Adds the top-down roof auto-tiler to the Walls tilemap and fills its 256-entry sprite tables.</summary>
    static void WireWalls()
    {
        var walls = GameObject.Find("Walls");
        if (walls == null) { Debug.LogWarning("No 'Walls' tilemap in the Game scene."); return; }

        var tiler = GetOrAdd<WallAutoTiler>(walls);
        var so = new SerializedObject(tiler);
        so.FindProperty("edgeTile").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>("Assets/_Game/Tiles/EdgeBlocker.asset");
        FillWallTable(so.FindProperty("wallSprites"), "wall");
        FillWallTable(so.FindProperty("edgeSprites"), "edge");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void FillWallTable(SerializedProperty arr, string style)
    {
        arr.arraySize = 256;
        for (int k = 0; k < 256; k++)
        {
            int c = WallAutoTiler.Canonical(k);
            arr.GetArrayElementAtIndex(k).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/environment/walls/{style}_{c:000}.png");
        }
    }

    static void SetSprites(SerializedProperty arr, params string[] names)
    {
        arr.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/props/{names[i]}.png");
    }

    static void SetupMenuScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Menu.unity");
        var canvas = GameObject.Find("Menu Canvas");
        if (canvas != null) AddMenuBackground(canvas);
        var cam = Camera.main;
        if (cam != null) cam.backgroundColor = new Color(0.04f, 0.16f, 0.16f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>Adds (or refreshes) the full-screen forest backdrop as the first child of the menu canvas.</summary>
    public static void AddMenuBackground(GameObject canvas)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/backgrounds/forest_bg.png");
        if (sprite == null) return;

        var t = canvas.transform.Find("Background");
        GameObject go = t != null ? t.gameObject : new GameObject("Background", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling();

        var img = GetOrAdd<Image>(go);
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1920, 1080);

        // Cover any aspect ratio without stretching.
        var fit = GetOrAdd<AspectRatioFitter>(go);
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 1920f / 1080f;
    }
}
