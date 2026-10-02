using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the particle-effect prefabs (muzzle flash, impact, explosion, blood, smoke, flash) and
/// wires a GameEffects spawner into the Game scene. Idempotent.
///
/// Every effect is made of a few layered particle systems that all read the SAME texture
/// (Art/fx/fx_sheet.png, a 4x4 grid of Kenney particle sprites) through just two materials
/// (alpha + additive), so a whole firefight costs a handful of draw calls.
/// Menu: Shooter > Build Effects (also run by Shooter > Apply Art Pass).
/// </summary>
public static class EffectsSetup
{
    const string FxArt = "Assets/_Game/Art/fx";
    const string FxDir = "Assets/_Game/Prefabs/FX";

    // Cells of fx_sheet.png (row * 4 + column, counted from the top-left).
    const int Smoke0 = 0, Smoke1 = 3;       // row 0: smoke puffs
    const int Fire0 = 4, Fire1 = 6;         // row 1: fire / flame
    const int MuzzleA = 8, MuzzleB = 9;     // row 2: muzzle flames (pre-rotated to point along +X)
    const int Flare = 10, Soft = 11;        // big star flare, soft dot
    const int Plank = 12, RubbleA = 13, RubbleB = 14, Splat = 15;

    enum Curve { Shrink, Grow, Constant, Pop }

    class Layer
    {
        public string name = "Layer";
        public bool additive;
        public int frame0, frame1;
        public int count = 1;
        public float delay;
        public float life0 = 0.3f, life1 = 0.3f;
        public float speed0, speed1;
        public float size0 = 0.3f, size1 = 0.3f;
        public Color from = Color.white, to = Color.white;
        public float drag;
        public float gravity;
        public float spin;                    // deg/s, +/- random
        public bool randomRotation = true;
        public Curve sizeCurve = Curve.Shrink;
        public float radius = 0.05f;          // circle emitter radius
        public float cone;                    // >0 => cone of this half-angle emitting along +X
        public float offsetX;                 // shift emitter along local +X (muzzle flames)
        public bool stretch; public float stretchLen = 2.5f;
        public bool local;                    // quads follow the prefab rotation (directional flashes)
        public bool fadeIn;
        public int order = 22;
    }

    [MenuItem("Shooter/Build Effects")]
    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(FxDir))
            AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "FX");

        var alpha = GetMaterial("FX_Alpha", "Sprites/Default", true);
        var add = GetMaterial("FX_Additive", "Mobile/Particles/Additive", true);

        var muzzle = Build("MuzzleFX", alpha, add,
            new Layer { name = "Flame", additive = true, frame0 = MuzzleA, frame1 = MuzzleB, count = 1, life0 = 0.07f, life1 = 0.07f,
                size0 = 0.8f, size1 = 1.0f, from = new Color(1f, 0.95f, 0.6f), to = new Color(1f, 0.55f, 0.15f), local = true,
                randomRotation = false, offsetX = 0.38f, sizeCurve = Curve.Pop, order = 24 },
            new Layer { name = "Sparks", additive = true, frame0 = Soft, frame1 = Soft, count = 5, life0 = 0.10f, life1 = 0.2f,
                speed0 = 9f, speed1 = 15f, size0 = 0.07f, size1 = 0.12f, from = new Color(1f, 0.9f, 0.5f), to = new Color(1f, 0.4f, 0.1f),
                cone = 16f, stretch = true, stretchLen = 3f, order = 23 },
            new Layer { name = "Puff", frame0 = Smoke0, frame1 = Smoke1, count = 2, life0 = 0.3f, life1 = 0.45f, speed0 = 0.4f, speed1 = 1.1f,
                size0 = 0.25f, size1 = 0.4f, from = new Color(0.85f, 0.85f, 0.85f, 0.35f), to = new Color(0.6f, 0.6f, 0.6f, 0f),
                cone = 25f, sizeCurve = Curve.Grow, spin = 40f, order = 21 });

        var impact = Build("ImpactFX", alpha, add,
            new Layer { name = "Sparks", additive = true, frame0 = Soft, frame1 = Soft, count = 7, life0 = 0.12f, life1 = 0.28f,
                speed0 = 3f, speed1 = 7.5f, size0 = 0.06f, size1 = 0.1f, from = new Color(1f, 0.9f, 0.5f), to = new Color(1f, 0.45f, 0.1f),
                radius = 0.04f, stretch = true, stretchLen = 2.5f, order = 23 },
            new Layer { name = "Dust", frame0 = Smoke0, frame1 = Smoke1, count = 3, life0 = 0.3f, life1 = 0.5f, speed0 = 0.3f, speed1 = 1f,
                size0 = 0.3f, size1 = 0.5f, from = new Color(0.78f, 0.68f, 0.52f, 0.5f), to = new Color(0.6f, 0.52f, 0.4f, 0f),
                sizeCurve = Curve.Grow, spin = 60f, order = 21 },
            new Layer { name = "Chips", frame0 = RubbleA, frame1 = RubbleB, count = 3, life0 = 0.3f, life1 = 0.45f, speed0 = 2f, speed1 = 4.5f,
                size0 = 0.1f, size1 = 0.16f, from = Color.white, to = new Color(1, 1, 1, 0f), drag = 2f, spin = 360f, order = 22 });

        var explosion = Build("ExplosionFX", alpha, add,
            new Layer { name = "Flash", additive = true, frame0 = Flare, frame1 = Flare, count = 1, life0 = 0.18f, life1 = 0.18f,
                size0 = 3.6f, size1 = 4.2f, from = new Color(1f, 0.9f, 0.6f), to = new Color(1f, 0.5f, 0.1f), sizeCurve = Curve.Pop, order = 26 },
            new Layer { name = "Fireball", additive = true, frame0 = Fire0, frame1 = Fire1, count = 12, life0 = 0.4f, life1 = 0.7f,
                speed0 = 1.5f, speed1 = 5f, size0 = 1.0f, size1 = 1.7f, from = new Color(1f, 0.75f, 0.3f), to = new Color(0.8f, 0.15f, 0.05f, 0f),
                drag = 3f, sizeCurve = Curve.Grow, spin = 70f, radius = 0.25f, order = 25 },
            new Layer { name = "Smoke", frame0 = Smoke0, frame1 = Smoke1, count = 14, delay = 0.05f, life0 = 1.0f, life1 = 1.7f,
                speed0 = 0.5f, speed1 = 2.6f, size0 = 1.2f, size1 = 2.1f, from = new Color(0.3f, 0.27f, 0.25f, 0.75f), to = new Color(0.12f, 0.12f, 0.12f, 0f),
                drag = 2f, sizeCurve = Curve.Grow, spin = 25f, radius = 0.4f, order = 24 },
            new Layer { name = "Sparks", additive = true, frame0 = Soft, frame1 = Soft, count = 26, life0 = 0.3f, life1 = 0.7f,
                speed0 = 7f, speed1 = 16f, size0 = 0.1f, size1 = 0.18f, from = new Color(1f, 0.85f, 0.35f), to = new Color(1f, 0.3f, 0.05f),
                drag = 1.5f, stretch = true, stretchLen = 3f, radius = 0.1f, order = 25 },
            new Layer { name = "Debris", frame0 = Plank, frame1 = RubbleB, count = 12, life0 = 0.6f, life1 = 0.95f, speed0 = 4f, speed1 = 9f,
                size0 = 0.2f, size1 = 0.36f, from = Color.white, to = new Color(1, 1, 1, 0f), drag = 2f, spin = 420f, sizeCurve = Curve.Constant,
                radius = 0.2f, order = 24 });

        var blood = Build("BloodFX", alpha, add,
            new Layer { name = "Drops", frame0 = Soft, frame1 = Soft, count = 9, life0 = 0.3f, life1 = 0.5f, speed0 = 2f, speed1 = 5f,
                size0 = 0.1f, size1 = 0.2f, from = new Color(0.78f, 0.05f, 0.06f), to = new Color(0.4f, 0f, 0f, 0f), drag = 3f, order = 15 },
            new Layer { name = "Splat", frame0 = Splat, frame1 = Splat, count = 2, life0 = 0.9f, life1 = 1.3f, speed0 = 0f, speed1 = 0.3f,
                size0 = 0.45f, size1 = 0.75f, from = new Color(0.65f, 0f, 0.02f, 0.85f), to = new Color(0.4f, 0f, 0f, 0f), sizeCurve = Curve.Pop, order = 14 });

        var smoke = Build("SmokeFX", alpha, add,
            new Layer { name = "Cloud", frame0 = Smoke0, frame1 = Smoke1, count = 44, life0 = 4.4f, life1 = 5.4f, speed0 = 0.1f, speed1 = 0.5f,
                size0 = 3.2f, size1 = 4.6f, from = new Color(0.86f, 0.88f, 0.9f, 0.82f), to = new Color(0.7f, 0.72f, 0.75f, 0f),
                sizeCurve = Curve.Grow, spin = 8f, radius = 2.8f, fadeIn = true, order = 19 },
            new Layer { name = "Wisps", frame0 = Smoke0, frame1 = Smoke1, count = 22, delay = 0.35f, life0 = 3.6f, life1 = 4.8f, speed0 = 0.1f, speed1 = 0.4f,
                size0 = 3.0f, size1 = 4.2f, from = new Color(0.8f, 0.83f, 0.86f, 0.7f), to = new Color(0.7f, 0.72f, 0.75f, 0f),
                sizeCurve = Curve.Grow, spin = 10f, radius = 3.3f, fadeIn = true, order = 19 });

        var flash = Build("FlashFX", alpha, add,
            new Layer { name = "Flare", additive = true, frame0 = Flare, frame1 = Flare, count = 1, life0 = 0.4f, life1 = 0.4f,
                size0 = 7f, size1 = 8f, from = Color.white, to = new Color(1f, 1f, 0.8f, 0f), sizeCurve = Curve.Pop, randomRotation = false, order = 27 },
            new Layer { name = "Core", additive = true, frame0 = Soft, frame1 = Soft, count = 1, life0 = 0.25f, life1 = 0.25f,
                size0 = 5f, size1 = 5f, from = Color.white, to = new Color(1f, 1f, 1f, 0f), sizeCurve = Curve.Shrink, order = 26 },
            new Layer { name = "Rays", additive = true, frame0 = Soft, frame1 = Soft, count = 18, life0 = 0.2f, life1 = 0.35f,
                speed0 = 10f, speed1 = 18f, size0 = 0.12f, size1 = 0.2f, from = Color.white, to = new Color(1f, 1f, 0.8f, 0f),
                stretch = true, stretchLen = 4f, order = 26 });

        AssetDatabase.SaveAssets();
        WireScene(muzzle, impact, explosion, blood, smoke, flash);
        Debug.Log("Effects build complete.");
    }

    // ------------------------------------------------------------------ materials
    static Material GetMaterial(string name, string shaderName, bool useSheet)
    {
        string path = FxArt + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(shaderName);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null && mat.shader != shader) mat.shader = shader;

        if (useSheet) mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FxArt + "/fx_sheet.png");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------------ prefab building
    static GameObject Build(string name, Material alpha, Material add, params Layer[] layers)
    {
        var root = new GameObject(name);
        for (int i = 0; i < layers.Length; i++)
        {
            var l = layers[i];
            GameObject go = root;
            if (i > 0)
            {
                go = new GameObject(l.name);
                go.transform.SetParent(root.transform, false);
            }
            var ps = go.TryGetComponent(out ParticleSystem existing) ? existing : go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Configure(ps, l, l.additive ? add : alpha);
        }
        // The root owns the pooled lifecycle: PooledParticle returns it once every child system has finished.
        root.AddComponent<PooledParticle>();

        string path = FxDir + "/" + name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void Configure(ParticleSystem ps, Layer l, Material mat)
    {
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.1f, l.delay + 0.05f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(l.life0, l.life1);
        main.startSpeed = new ParticleSystem.MinMaxCurve(l.speed0, l.speed1);
        main.startSize = new ParticleSystem.MinMaxCurve(l.size0, l.size1);
        main.startColor = new ParticleSystem.MinMaxGradient(l.from);
        main.gravityModifier = l.gravity;
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // burst stays where it happened
        main.stopAction = ParticleSystemStopAction.None;
        main.startRotation = l.randomRotation
            ? new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad)
            : new ParticleSystem.MinMaxCurve(0f);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(l.delay, (short)l.count) });

        var shape = ps.shape;
        shape.enabled = true;
        if (l.cone > 0f)
        {
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = l.cone;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(0f, 90f, 0f); // cone axis (+Z) -> +X, i.e. along the aim direction
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = l.radius;
            shape.radiusThickness = 1f;
        }
        shape.position = new Vector3(l.offsetX, 0f, 0f);

        // Colour: from -> to over the particle's life (fade-in optional).
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        float a0 = l.from.a, a1 = l.to.a;
        // 'from' is applied via startColor; this gradient multiplies it, so it tints toward 'to'.
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Tint(l.from, l.to), 1f) },
            l.fadeIn
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(Ratio(a1, a0), 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(Ratio(a1, a0), 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sol = ps.sizeOverLifetime;
        sol.enabled = l.sizeCurve != Curve.Constant;
        AnimationCurve c = l.sizeCurve switch
        {
            Curve.Grow => new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.35f, 0.9f), new Keyframe(1f, 1f)),
            Curve.Pop => new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.55f)),
            _ => new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)),
        };
        sol.size = new ParticleSystem.MinMaxCurve(1f, c);

        var rot = ps.rotationOverLifetime;
        rot.enabled = l.spin > 0f;
        if (l.spin > 0f) rot.z = new ParticleSystem.MinMaxCurve(-l.spin * Mathf.Deg2Rad, l.spin * Mathf.Deg2Rad);

        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = l.drag > 0f;
        if (l.drag > 0f) { lim.limit = 100f; lim.drag = l.drag; }

        // Pick a random cell from this layer's range of the 4x4 sheet.
        var ts = ps.textureSheetAnimation;
        ts.enabled = true;
        ts.mode = ParticleSystemAnimationMode.Grid;
        ts.numTilesX = 4; ts.numTilesY = 4;
        ts.animation = ParticleSystemAnimationType.WholeSheet;
        ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        ts.startFrame = new ParticleSystem.MinMaxCurve(l.frame0 / 16f, (l.frame1 + 0.99f) / 16f);
        ts.cycleCount = 1;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = l.order;
        r.alignment = l.local ? ParticleSystemRenderSpace.Local : ParticleSystemRenderSpace.View;
        r.renderMode = l.stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (l.stretch) { r.lengthScale = l.stretchLen; r.velocityScale = 0.05f; }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static float Ratio(float a1, float a0) => Mathf.Clamp01(a0 > 0.001f ? a1 / a0 : 0f);

    // The gradient is multiplied with startColor, so it only needs the *relative* tint end colour.
    static Color Tint(Color from, Color to)
    {
        return new Color(
            from.r > 0.001f ? Mathf.Clamp01(to.r / from.r) : 1f,
            from.g > 0.001f ? Mathf.Clamp01(to.g / from.g) : 1f,
            from.b > 0.001f ? Mathf.Clamp01(to.b / from.b) : 1f, 1f);
    }

    // ------------------------------------------------------------------ scene wiring
    static void WireScene(GameObject muzzle, GameObject impact, GameObject explosion, GameObject blood, GameObject smoke, GameObject flash)
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Game.unity");
        var systems = GameObject.Find("Systems");
        if (systems == null) { Debug.LogWarning("No 'Systems' object in Game scene."); return; }

        var fx = systems.TryGetComponent(out GameEffects gfx) ? gfx : systems.AddComponent<GameEffects>();
        Set(fx, "muzzle", muzzle); Set(fx, "impact", impact); Set(fx, "explosion", explosion);
        Set(fx, "blood", blood);   Set(fx, "smoke", smoke);   Set(fx, "flash", flash);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void Set(Component c, string field, object value)
    {
        var f = c.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (f != null) f.SetValue(c, value);
    }
}
