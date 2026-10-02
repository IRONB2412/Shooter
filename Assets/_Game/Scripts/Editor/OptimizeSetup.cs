using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One-click low-end Android optimisation (idempotent, safe to re-run):
///  1. Merges every tilemap's per-tile colliders into one CompositeCollider2D per
///     map (hundreds of colliders -> a few shapes: cheaper physics, bullets & LOS).
///  2. Tunes the Mobile URP asset (no HDR / MSAA / shadows / extra textures,
///     dynamic batching + SRP batcher on).
///  3. Android player settings: landscape only, IL2CPP + ARMv7/ARM64, frame pacing.
/// Menu: Shooter > Optimize for Android. Re-run after adding new level scenes.
/// </summary>
public static class OptimizeSetup
{
    [MenuItem("Shooter/Optimize for Android")]
    public static void RunMenu() => Debug.Log(RunAll());

    public static string RunAll()
    {
        var log = new StringBuilder();
        log.AppendLine(MergeAllSceneColliders());
        log.AppendLine(TuneMobileRenderer());
        log.AppendLine(AndroidPlayerSettings());
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ------------------------------------------------------------ colliders
    /// <summary>Make a tilemap's collider a single merged composite shape.</summary>
    public static void MergeColliders(GameObject go)
    {
        if (!go.TryGetComponent<TilemapCollider2D>(out var tc)) return;

        if (!go.TryGetComponent<Rigidbody2D>(out var rb)) rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        if (!go.TryGetComponent<CompositeCollider2D>(out var cc)) cc = go.AddComponent<CompositeCollider2D>();
        cc.geometryType = CompositeCollider2D.GeometryType.Polygons;      // solid (bullets starting inside still hit)
        cc.generationType = CompositeCollider2D.GenerationType.Synchronous; // re-merges when tiles are carved

        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
    }

    static string MergeAllSceneColliders()
    {
        var active = EditorSceneManager.GetActiveScene().path;
        int maps = 0, scenes = 0;

        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled || string.IsNullOrEmpty(s.path)) continue;
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int before = maps;
            foreach (var tc in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None))
            {
                MergeColliders(tc.gameObject);
                maps++;
            }
            if (maps > before)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenes++;
            }
        }

        if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
        return $"Colliders: merged {maps} tilemap(s) in {scenes} scene(s).";
    }

    // ------------------------------------------------------------ renderer
    static string TuneMobileRenderer()
    {
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Mobile_RPAsset.asset");
        if (asset == null) return "URP: Mobile_RPAsset not found (skipped).";

        var so = new SerializedObject(asset);
        var sb = new StringBuilder("URP Mobile:");
        void Bool(string p, bool v) { var x = so.FindProperty(p); if (x != null) { x.boolValue = v; sb.Append($" {p}={v}"); } }
        void Int(string p, int v)   { var x = so.FindProperty(p); if (x != null) { x.intValue = v; sb.Append($" {p}={v}"); } }

        Bool("m_SupportsHDR", false);                 // big bandwidth saver on phones
        Int("m_MSAA", 1);                             // no MSAA
        Bool("m_SupportsCameraDepthTexture", false);
        Bool("m_SupportsCameraOpaqueTexture", false);
        Bool("m_MainLightShadowsSupported", false);   // 2D game: no shadows
        Bool("m_AdditionalLightShadowsSupported", false);
        Bool("m_SoftShadowsSupported", false);
        Bool("m_SupportsDynamicBatching", true);      // batches many small sprites
        Bool("m_UseSRPBatcher", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return sb.ToString();
    }

    // ------------------------------------------------------------ android
    static string AndroidPlayerSettings()
    {
        // Twin-stick shooter: landscape only.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // IL2CPP = faster code; ARMv7 keeps old/low-end phones, ARM64 is required by Play.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.optimizedFramePacing = true; // smoother frame delivery
        PlayerSettings.runInBackground = true;

        return "Android: landscape-only, IL2CPP, ARMv7+ARM64, optimized frame pacing.";
    }
}
