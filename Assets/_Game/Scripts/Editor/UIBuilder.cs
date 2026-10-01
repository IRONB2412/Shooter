using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the Menu scene and the in-game HUD (joystick, fire/throw/reload/pause
/// buttons, weapon switch, health/ammo/kills readouts, pause overlay, flash).
/// Idempotent. Menu: Shooter > Build UI. Run AFTER Shooter > Build Game.
/// </summary>
public static class UIBuilder
{
    const string Data = "Assets/_Game/Data";
    static Font _font;
    static Sprite _square, _circle;

    [MenuItem("Shooter/Build UI")]
    public static void BuildAll()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/square.png");
        _circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/circle.png");

        BuildMenuScene();
        BuildHUD();
        Debug.Log("UI build complete.");
    }

    // =============================================================== MENU
    static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var c = cam.AddComponent<Camera>();
        c.orthographic = true; c.backgroundColor = new Color(0.06f, 0.07f, 0.1f); c.clearFlags = CameraClearFlags.SolidColor;

        var canvas = MakeCanvas("Menu Canvas");
        MakeEventSystem();

        var menuGO = new GameObject("MenuController");
        var menu = menuGO.AddComponent<MenuController>();

        // Title
        var title = MakeText(canvas.transform, "Title", "2D SHOOTER", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(800, 120), 64, TextAnchor.MiddleCenter);
        title.color = Color.white;

        // ---- Main panel ----
        var main = MakePanel(canvas.transform, "MainPanel", new Color(0, 0, 0, 0));
        var bPlayBots = MakeButton(main.transform, "PlayBots", "Play vs Bots", new Vector2(0, 80), new Vector2(360, 80));
        var bOnline = MakeButton(main.transform, "PlayOnline", "Play Online", new Vector2(0, -20), new Vector2(360, 80));
        var bExit = MakeButton(main.transform, "Exit", "Exit", new Vector2(0, -120), new Vector2(360, 80));
        Void(bPlayBots, menu.ShowBots);
        Void(bOnline, menu.ShowOnline);
        Void(bExit, menu.QuitGame);

        // ---- Bots panel (difficulty + count) ----
        var bots = MakePanel(canvas.transform, "BotsPanel", new Color(0, 0, 0, 0));
        MakeText(bots.transform, "Diff", "Difficulty", Center(), Center(), Center(), new Vector2(0, 170), new Vector2(400, 50), 30, TextAnchor.MiddleCenter).color = Color.white;
        var e = MakeButton(bots.transform, "Easy", "Easy", new Vector2(-140, 100), new Vector2(120, 70));
        var n = MakeButton(bots.transform, "Normal", "Normal", new Vector2(0, 100), new Vector2(120, 70));
        var h = MakeButton(bots.transform, "Hard", "Hard", new Vector2(140, 100), new Vector2(120, 70));
        Int(e, menu.SetDifficulty, 0); Int(n, menu.SetDifficulty, 1); Int(h, menu.SetDifficulty, 2);

        var minus = MakeButton(bots.transform, "Minus", "-", new Vector2(-140, 10), new Vector2(70, 70));
        var countLabel = MakeText(bots.transform, "BotCount", "Bots: 5", Center(), Center(), Center(), new Vector2(0, 10), new Vector2(200, 60), 28, TextAnchor.MiddleCenter);
        countLabel.color = Color.white;
        var plus = MakeButton(bots.transform, "Plus", "+", new Vector2(140, 10), new Vector2(70, 70));
        Int(minus, menu.ChangeBotCount, -1); Int(plus, menu.ChangeBotCount, 1);

        var start = MakeButton(bots.transform, "Start", "Start Match", new Vector2(0, -90), new Vector2(300, 80));
        var back1 = MakeButton(bots.transform, "Back", "Back", new Vector2(0, -180), new Vector2(200, 60));
        Void(start, menu.PlayBots); Void(back1, menu.ShowMain);

        // ---- Online panel ----
        var online = MakePanel(canvas.transform, "OnlinePanel", new Color(0, 0, 0, 0));
        MakeText(online.transform, "OLabel", "Host, or join by IP", Center(), Center(), Center(), new Vector2(0, 150), new Vector2(500, 50), 28, TextAnchor.MiddleCenter).color = Color.white;
        var host = MakeButton(online.transform, "Host", "Host Game", new Vector2(0, 80), new Vector2(300, 70));
        var addr = MakeInputField(online.transform, "Address", "127.0.0.1", new Vector2(0, 0), new Vector2(360, 60));
        var join = MakeButton(online.transform, "Join", "Join Game", new Vector2(0, -80), new Vector2(300, 70));
        var back2 = MakeButton(online.transform, "Back", "Back", new Vector2(0, -170), new Vector2(200, 60));
        Void(host, menu.HostOnline); Void(join, menu.JoinOnline); Void(back2, menu.ShowMain);

        // Wire panels into the controller.
        Set(menu, "mainPanel", main.gameObject); Set(menu, "botsPanel", bots.gameObject); Set(menu, "onlinePanel", online.gameObject);
        Set(menu, "addressField", addr); Set(menu, "botCountLabel", countLabel);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Menu.unity");
        AddSceneToBuildFirst("Assets/_Game/Scenes/Menu.unity");
    }

    // =============================================================== HUD
    static void BuildHUD()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Game.unity");

        // Clean any previous HUD so re-runs don't stack.
        foreach (var n in new[] { "HUD Canvas", "EventSystem" })
        {
            var old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }

        var canvas = MakeCanvas("HUD Canvas");
        MakeEventSystem();

        var hud = canvas.gameObject.AddComponent<HUDController>();
        var pause = canvas.gameObject.AddComponent<PauseMenu>();
        var layout = canvas.gameObject.AddComponent<HUDLayoutManager>();

        // ---- Health bar (top-centre) ----
        var barBg = MakeImage(canvas.transform, "HealthBg", _square, new Color(0, 0, 0, 0.5f));
        SetRect(barBg, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(420, 34));
        var barFill = MakeImage(barBg.transform, "HealthFill", _square, new Color(0.3f, 0.9f, 0.35f));
        SetRect(barFill, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6, -6));
        barFill.type = Image.Type.Filled; barFill.fillMethod = Image.FillMethod.Horizontal; barFill.fillOrigin = 0; barFill.fillAmount = 1f;

        var weaponText = MakeText(canvas.transform, "WeaponText", "Pistol", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(300, 30), 22, TextAnchor.MiddleCenter);
        weaponText.color = Color.white;
        var killsText = MakeText(canvas.transform, "KillsText", "Kills: 0", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(220, 34), 24, TextAnchor.MiddleLeft);
        killsText.color = Color.white;

        // ---- Left stick: MOVEMENT ONLY (bottom-left) ----
        MakeStick(canvas.transform, "MoveStick", new Vector2(0, 0), new Vector2(40, 40),
            new Color(1, 1, 1, 0.18f), new Color(1, 1, 1, 0.45f), VirtualJoystick.Axis.Move, "MOVE");

        // ---- Right stick: AIM only (bottom-right) ----
        MakeStick(canvas.transform, "AimStick", new Vector2(1, 0), new Vector2(-40, 40),
            new Color(0.3f, 0.6f, 0.9f, 0.2f), new Color(0.5f, 0.75f, 1f, 0.6f), VirtualJoystick.Axis.Aim, "AIM");

        // ---- Fire button (separate, above the aim stick) ----
        var fire = MakeImage(canvas.transform, "FireButton", _circle, new Color(0.9f, 0.3f, 0.3f, 0.85f));
        SetRect(fire, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 310), new Vector2(150, 150));
        fire.gameObject.AddComponent<HoldButton>();
        fire.gameObject.AddComponent<DraggableHUDElement>();
        AddLabel(fire.transform, "FIRE", 26);

        // ---- Action buttons (bottom-centre, clear of both sticks) ----
        var reload = MakeButton(canvas.transform, "ReloadButton", "RELOAD", Vector2.zero, new Vector2(150, 78));
        SetRect(reload.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 130), new Vector2(150, 78));
        Void(reload, hud.Reload); Draggable(reload);

        var throwBtn = MakeButton(canvas.transform, "ThrowButton", "THROW", Vector2.zero, new Vector2(150, 90));
        SetRect(throwBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(120, 130), new Vector2(150, 90));
        Void(throwBtn, hud.ThrowSelected); Draggable(throwBtn);

        var throwableText = MakeText(throwBtn.transform, "ThrowableText", "Grenade", Center(), Center(), Center(), new Vector2(0, 62), new Vector2(160, 26), 18, TextAnchor.MiddleCenter);
        throwableText.color = Color.white;
        var cycle = MakeButton(canvas.transform, "CycleThrowable", "Swap", Vector2.zero, new Vector2(120, 60));
        SetRect(cycle.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(120, 235), new Vector2(120, 60));
        Void(cycle, hud.CycleThrowable); Draggable(cycle);

        var ammoText = MakeText(canvas.transform, "AmmoText", "12/12", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(200, 34), 26, TextAnchor.MiddleCenter);
        ammoText.color = Color.white;

        // ---- Weapon switch buttons (top-left row) ----
        string[] names = { "Pistol", "AR", "SMG", "Fire" };
        for (int i = 0; i < names.Length; i++)
        {
            var wb = MakeButton(canvas.transform, "Weapon" + i, names[i], Vector2.zero, new Vector2(110, 56));
            var rt = wb.GetComponent<RectTransform>();
            SetRect(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24 + i * 120, -70), new Vector2(110, 56));
            Int(wb, hud.SelectWeapon, i); Draggable(wb);
        }

        // ---- Pause button (top-right) ----
        var pauseBtn = MakeButton(canvas.transform, "PauseButton", "II", Vector2.zero, new Vector2(70, 70));
        SetRect(pauseBtn.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(70, 70));
        Void(pauseBtn, pause.Toggle); Draggable(pauseBtn);

        // ---- Pause panel (hidden) ----
        var pausePanel = MakeImage(canvas.transform, "PausePanel", _square, new Color(0, 0, 0, 0.75f));
        SetRect(pausePanel, Vector2.zero, Vector2.one, Center(), Vector2.zero, Vector2.zero);
        var resume = MakeButton(pausePanel.transform, "Resume", "Resume", new Vector2(0, 135), new Vector2(320, 74));
        var editLayout = MakeButton(pausePanel.transform, "EditLayout", "Edit Layout", new Vector2(0, 45), new Vector2(320, 74));
        var toMenu = MakeButton(pausePanel.transform, "Menu", "Back to Menu", new Vector2(0, -45), new Vector2(320, 74));
        var quit = MakeButton(pausePanel.transform, "Quit", "Exit", new Vector2(0, -135), new Vector2(320, 74));
        Void(resume, pause.Resume); Void(toMenu, pause.BackToMenu); Void(quit, pause.QuitGame);
        Void(editLayout, layout.EnterEdit);
        pausePanel.gameObject.SetActive(false);

        // ---- Layout edit bar (hidden until Edit Layout is chosen) ----
        var editBar = MakeImage(canvas.transform, "EditBar", _square, new Color(0.1f, 0.12f, 0.16f, 0.95f));
        SetRect(editBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(0, 100));
        MakeText(editBar.transform, "Hint", "Drag to move  •  tap then − / + to resize", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 18), new Vector2(700, 32), 22, TextAnchor.MiddleLeft).color = Color.white;
        var selLabel = MakeText(editBar.transform, "Selected", "Tap a control to select", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, -18), new Vector2(700, 30), 20, TextAnchor.MiddleLeft);
        selLabel.color = new Color(0.7f, 0.85f, 1f);

        var reset = MakeButton(editBar.transform, "Reset", "Reset", Vector2.zero, new Vector2(140, 64));
        SetRect(reset.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-430, 0), new Vector2(140, 64));
        var minus = MakeButton(editBar.transform, "Minus", "−", Vector2.zero, new Vector2(80, 64));
        SetRect(minus.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-300, 0), new Vector2(80, 64));
        var plus = MakeButton(editBar.transform, "Plus", "+", Vector2.zero, new Vector2(80, 64));
        SetRect(plus.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-205, 0), new Vector2(80, 64));
        var done = MakeButton(editBar.transform, "Done", "Done", Vector2.zero, new Vector2(150, 64));
        SetRect(done.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-25, 0), new Vector2(150, 64));
        Void(reset, layout.ResetLayout); Void(minus, layout.ResizeSmaller); Void(plus, layout.ResizeBigger); Void(done, layout.ExitEdit);
        editBar.gameObject.SetActive(false);

        Set(layout, "pausePanel", pausePanel.gameObject);
        Set(layout, "editBar", editBar.gameObject);
        Set(layout, "selectedLabel", selLabel);

        // ---- Screen flash (full-screen white, on top) ----
        var flash = MakeImage(canvas.transform, "ScreenFlash", _square, new Color(1, 1, 1, 0));
        SetRect(flash, Vector2.zero, Vector2.one, Center(), Vector2.zero, Vector2.zero);
        flash.raycastTarget = false;
        flash.gameObject.AddComponent<ScreenFlash>();
        flash.transform.SetAsLastSibling();

        // Wire HUD + pause references.
        Set(hud, "healthFill", barFill);
        Set(hud, "ammoText", ammoText);
        Set(hud, "weaponText", weaponText);
        Set(hud, "killsText", killsText);
        Set(hud, "throwableText", throwableText);
        Set(pause, "panel", pausePanel.gameObject);
        Set(pause, "menuScene", "Menu");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // =============================================================== UI helpers
    static Vector2 Center() => new Vector2(0.5f, 0.5f);

    static Canvas MakeCanvas(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void MakeEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    static RectTransform MakeUI(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform SetRect(Component c, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = c is RectTransform r ? r : c.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var rt = MakeUI(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.type = Image.Type.Simple;
        return img;
    }

    static Image MakePanel(Transform parent, string name, Color color)
    {
        var img = MakeImage(parent, name, _square, color);
        SetRect(img, Vector2.zero, Vector2.one, Center(), Vector2.zero, Vector2.zero);
        return img;
    }

    static Text MakeText(Transform parent, string name, string text, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
    {
        var rt = MakeUI(parent, name);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text; t.font = _font; t.fontSize = fontSize; t.alignment = align; t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(rt, aMin, aMax, pivot, pos, size);
        return t;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Vector2 centerPos, Vector2 size)
    {
        var img = MakeImage(parent, name, _square, new Color(0.2f, 0.45f, 0.75f, 0.95f));
        SetRect(img, Center(), Center(), Center(), centerPos, size);
        img.gameObject.AddComponent<Button>();
        AddLabel(img.transform, label, 24);
        return img.gameObject;
    }

    static void Draggable(GameObject go)
    {
        if (go.GetComponent<DraggableHUDElement>() == null) go.AddComponent<DraggableHUDElement>();
    }

    static void AddLabel(Transform parent, string label, int size)
    {
        var t = MakeText(parent, "Label", label, Vector2.zero, Vector2.one, Center(), Vector2.zero, Vector2.zero, size, TextAnchor.MiddleCenter);
        t.color = Color.white;
    }

    static InputField MakeInputField(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size)
    {
        var img = MakeImage(parent, name, _square, new Color(1, 1, 1, 0.9f));
        SetRect(img, Center(), Center(), Center(), pos, size);
        var field = img.gameObject.AddComponent<InputField>();
        var text = MakeText(img.transform, "Text", placeholder, Vector2.zero, Vector2.one, Center(), Vector2.zero, new Vector2(-20, 0), 24, TextAnchor.MiddleLeft);
        text.color = Color.black;
        field.textComponent = text;
        field.text = placeholder;
        return field;
    }

    /// <summary>Anchor a control to the bottom-right corner at an offset.</summary>
    static void AnchorBR(GameObject go, Vector2 offset, Vector2 size)
        => SetRect(go.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), offset, size);

    /// <summary>Build a virtual joystick (base + handle + label) anchored at a corner.</summary>
    static void MakeStick(Transform parent, string name, Vector2 anchor, Vector2 offset,
        Color baseColor, Color handleColor, VirtualJoystick.Axis axis, string label)
    {
        var b = MakeImage(parent, name, _circle, baseColor);
        SetRect(b, anchor, anchor, anchor, offset, new Vector2(240, 240));
        var h = MakeImage(b.transform, "Handle", _circle, handleColor);
        SetRect(h, Center(), Center(), Center(), Vector2.zero, new Vector2(110, 110));

        var j = b.gameObject.AddComponent<VirtualJoystick>();
        Set(j, "axis", axis);
        Set(j, "background", b.GetComponent<RectTransform>());
        Set(j, "handle", h.GetComponent<RectTransform>());
        Set(j, "radius", 90f);
        b.gameObject.AddComponent<DraggableHUDElement>(); // movable in layout editor

        var t = MakeText(b.transform, "Label", label, Center(), Center(), Center(), new Vector2(0, 150), new Vector2(240, 26), 18, TextAnchor.MiddleCenter);
        t.color = new Color(1, 1, 1, 0.8f);
    }

    // Persistent button listeners (survive into the saved scene).
    static void Void(GameObject btn, UnityEngine.Events.UnityAction call)
        => UnityEventTools.AddVoidPersistentListener(btn.GetComponent<Button>().onClick, call);

    static void Int(GameObject btn, UnityEngine.Events.UnityAction<int> call, int arg)
        => UnityEventTools.AddIntPersistentListener(btn.GetComponent<Button>().onClick, call, arg);

    // Same private-field setter used by GameSetup.
    static void Set(Component c, string field, object value)
    {
        var f = c.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (f == null) { Debug.LogWarning($"No field '{field}' on {c.GetType().Name}"); return; }
        f.SetValue(c, value);
    }

    static void AddSceneToBuildFirst(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
