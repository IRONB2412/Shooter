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
    static Sprite _btnRect, _btnSquare, _panel, _knob, _ring, _btnFire;
    static Sprite[] _weaponIcons;
    static readonly Color _ink = new Color(0.15f, 0.17f, 0.22f); // dark text on light panels

    [MenuItem("Shooter/Build UI")]
    public static void BuildAll()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/square.png");
        _circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/circle.png");

        const string ui = "Assets/_Game/Art/ui/";
        _btnRect   = AssetDatabase.LoadAssetAtPath<Sprite>(ui + "btn_rect.png");
        _btnSquare = AssetDatabase.LoadAssetAtPath<Sprite>(ui + "btn_square.png");
        _panel     = AssetDatabase.LoadAssetAtPath<Sprite>(ui + "panel.png");
        _knob      = AssetDatabase.LoadAssetAtPath<Sprite>(ui + "knob.png");
        _ring      = AssetDatabase.LoadAssetAtPath<Sprite>(ui + "ring.png");
        _btnFire   = AssetDatabase.LoadAssetAtPath<Sprite>(ui + "btn_fire.png");
        _weaponIcons = new[] {
            AssetDatabase.LoadAssetAtPath<Sprite>(ui + "wpn_pistol.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>(ui + "wpn_ar.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>(ui + "wpn_smg.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>(ui + "wpn_ar.png"), // Fire reuses the machine icon
        };

        BuildMenuScene();
        BuildHUD();
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Menu.unity"); // leave the entry scene open
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

        // framed card behind the menu buttons
        var card = MakeImage(canvas.transform, "Card", _panel, new Color(1, 1, 1, 0.95f));
        card.type = Image.Type.Sliced;
        SetRect(card, Center(), Center(), Center(), new Vector2(0, -30), new Vector2(560, 520));

        // ---- Main panel ----
        var main = MakePanel(canvas.transform, "MainPanel", new Color(0, 0, 0, 0));
        var bPlayBots = MakeButton(main.transform, "PlayBots", "Play vs Bots", new Vector2(0, 110), new Vector2(360, 80));
        var bOnline = MakeButton(main.transform, "PlayOnline", "Play with Friends", new Vector2(0, 10), new Vector2(360, 80));
        var bSettings = MakeButton(main.transform, "Settings", "Settings", new Vector2(0, -90), new Vector2(360, 80));
        var bExit = MakeButton(main.transform, "Exit", "Exit", new Vector2(0, -190), new Vector2(360, 80));
        Void(bPlayBots, menu.ShowBots);
        Void(bOnline, menu.ShowOnline);
        Void(bExit, menu.QuitGame);

        // ---- Bots panel (difficulty + count) ----
        var bots = MakePanel(canvas.transform, "BotsPanel", new Color(0, 0, 0, 0));
        var diffLabel = MakeText(bots.transform, "Diff", "Difficulty: Normal", Center(), Center(), Center(), new Vector2(0, 170), new Vector2(400, 50), 30, TextAnchor.MiddleCenter);
        diffLabel.color = _ink;
        var e = MakeButton(bots.transform, "Easy", "Easy", new Vector2(-140, 100), new Vector2(120, 70));
        var n = MakeButton(bots.transform, "Normal", "Normal", new Vector2(0, 100), new Vector2(120, 70));
        var h = MakeButton(bots.transform, "Hard", "Hard", new Vector2(140, 100), new Vector2(120, 70));
        Int(e, menu.SetDifficulty, 0); Int(n, menu.SetDifficulty, 1); Int(h, menu.SetDifficulty, 2);

        var minus = MakeButton(bots.transform, "Minus", "-", new Vector2(-140, 10), new Vector2(70, 70));
        var countLabel = MakeText(bots.transform, "BotCount", "Bots: 5", Center(), Center(), Center(), new Vector2(0, 10), new Vector2(200, 60), 28, TextAnchor.MiddleCenter);
        countLabel.color = _ink;
        var plus = MakeButton(bots.transform, "Plus", "+", new Vector2(140, 10), new Vector2(70, 70));
        Int(minus, menu.ChangeBotCount, -1); Int(plus, menu.ChangeBotCount, 1);

        var start = MakeButton(bots.transform, "Start", "Start Match", new Vector2(0, -90), new Vector2(300, 80));
        var back1 = MakeButton(bots.transform, "Back", "Back", new Vector2(0, -180), new Vector2(200, 60));
        Void(start, menu.PlayBots); Void(back1, menu.ShowMain);

        // ---- Online panel ----
        var online = MakePanel(canvas.transform, "OnlinePanel", new Color(0, 0, 0, 0));
        MakeText(online.transform, "OLabel", "Play with Friends: Host, or Join by IP", Center(), Center(), Center(), new Vector2(0, 150), new Vector2(520, 50), 26, TextAnchor.MiddleCenter).color = _ink;
        var host = MakeButton(online.transform, "Host", "Host Game", new Vector2(0, 80), new Vector2(300, 70));
        var addr = MakeInputField(online.transform, "Address", "127.0.0.1", new Vector2(0, 0), new Vector2(360, 60));
        var join = MakeButton(online.transform, "Join", "Join Game", new Vector2(0, -80), new Vector2(300, 70));
        var back2 = MakeButton(online.transform, "Back", "Back", new Vector2(0, -170), new Vector2(200, 60));
        Void(host, menu.HostOnline); Void(join, menu.JoinOnline); Void(back2, menu.ShowMain);

        // Wire panels into the controller.
        var menuSettings = MakeSettingsPanel(canvas.transform);
        Void(bSettings, menuSettings.Open);
        Set(menu, "mainPanel", main.gameObject); Set(menu, "botsPanel", bots.gameObject); Set(menu, "onlinePanel", online.gameObject);
        Set(menu, "addressField", addr); Set(menu, "botCountLabel", countLabel);
        Set(menu, "difficultyLabel", diffLabel);
        Set(menu, "difficultyButtons", new[] { e.GetComponent<Image>(), n.GetComponent<Image>(), h.GetComponent<Image>() });

        ArtPipeline.AddMenuBackground(canvas.gameObject); // forest backdrop behind the menu
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

        // All controls live inside the device safe area (notches / rounded corners).
        // Full-screen overlays (pause dim, flash) stay on the canvas itself.
        var safe = MakeUI(canvas.transform, "SafeArea");
        SetRect(safe, Vector2.zero, Vector2.one, Center(), Vector2.zero, Vector2.zero);
        safe.gameObject.AddComponent<SafeArea>();
        Transform hudRoot = safe;

        // ---- Health bar (top-centre) ----
        var barBg = MakeImage(hudRoot, "HealthBg", _panel, new Color(0.1f, 0.12f, 0.16f, 0.9f));
        barBg.type = Image.Type.Sliced; barBg.raycastTarget = false;
        SetRect(barBg, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(420, 36));
        var barFill = MakeImage(barBg.transform, "HealthFill", _square, new Color(0.3f, 0.9f, 0.35f));
        SetRect(barFill, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6, -6));
        barFill.type = Image.Type.Filled; barFill.fillMethod = Image.FillMethod.Horizontal; barFill.fillOrigin = 0; barFill.fillAmount = 1f;
        barFill.raycastTarget = false;
        Isolate(barBg.gameObject, interactive: false); // fill changes on every hit

        var weaponText = MakeText(hudRoot, "WeaponText", "Pistol", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(300, 30), 22, TextAnchor.MiddleCenter);
        weaponText.color = Color.white;
        Isolate(weaponText.gameObject, interactive: false);
        var killsText = MakeText(hudRoot, "KillsText", "Kills: 0", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(220, 34), 24, TextAnchor.MiddleLeft);
        killsText.color = Color.white;
        Isolate(killsText.gameObject, interactive: false);

        // ---- Left stick: MOVEMENT ONLY (bottom-left) ----
        MakeStick(hudRoot, "MoveStick", new Vector2(0, 0), new Vector2(40, 40),
            new Color(1, 1, 1, 0.18f), new Color(1, 1, 1, 0.45f), VirtualJoystick.Axis.Move, "MOVE");

        // ---- Right stick: AIM only (bottom-right) ----
        MakeStick(hudRoot, "AimStick", new Vector2(1, 0), new Vector2(-40, 40),
            new Color(0.3f, 0.6f, 0.9f, 0.2f), new Color(0.5f, 0.75f, 1f, 0.6f), VirtualJoystick.Axis.Aim, "AIM");

        // ---- Fire button (separate, above the aim stick) ----
        var fire = MakeImage(hudRoot, "FireButton", _btnFire, Color.white);
        SetRect(fire, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 310), new Vector2(150, 150));
        fire.gameObject.AddComponent<HoldButton>();
        fire.gameObject.AddComponent<DraggableHUDElement>();
        AddLabel(fire.transform, "FIRE", 26);
        var fireLabel = fire.transform.Find("Label").GetComponent<Text>();

        // ---- Action buttons (bottom-centre, clear of both sticks) ----
        var reload = MakeButton(hudRoot, "ReloadButton", "RELOAD", Vector2.zero, new Vector2(150, 78));
        SetRect(reload.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 130), new Vector2(150, 78));
        Void(reload, hud.Reload); Draggable(reload);

        // One action button (FIRE) shoots or throws; this mode button cycles Gun / Grenade / Smoke / Flash.
        var cycle = MakeButton(hudRoot, "CycleThrowable", "Gun", Vector2.zero, new Vector2(150, 78));
        SetRect(cycle.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-240, 330), new Vector2(150, 78));
        Void(cycle, hud.CycleThrowable); Draggable(cycle);
        var throwableText = cycle.transform.Find("Label").GetComponent<Text>();

        var ammoText = MakeText(hudRoot, "AmmoText", "12/12", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(200, 34), 26, TextAnchor.MiddleCenter);
        ammoText.color = Color.white;
        Isolate(ammoText.gameObject, interactive: false); // changes on every shot

        // ---- Weapon switch buttons (top-left row) ----
        string[] names = { "Pistol", "AR", "SMG", "Fire" };
        for (int i = 0; i < names.Length; i++)
        {
            var wb = MakeButton(hudRoot, "Weapon" + i, names[i], Vector2.zero, new Vector2(96, 72));
            wb.GetComponent<Image>().sprite = _btnSquare; // square face for weapon slots
            var rt = wb.GetComponent<RectTransform>();
            SetRect(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24 + i * 104, -70), new Vector2(96, 72));

            // weapon icon on top, name shrunk below
            if (_weaponIcons != null && _weaponIcons[i] != null)
            {
                var icon = MakeImage(wb.transform, "Icon", _weaponIcons[i], Color.white);
                icon.preserveAspect = true;
                icon.raycastTarget = false; // the button face receives the tap
                SetRect(icon, Center(), Center(), Center(), new Vector2(0, 8), new Vector2(60, 40));
            }
            var lbl = wb.transform.Find("Label") as RectTransform;
            if (lbl != null) { var lt = lbl.GetComponent<Text>(); lt.fontSize = 15; lt.color = _ink; SetRect(lbl, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(96, 20)); }

            Int(wb, hud.SelectWeapon, i); Draggable(wb);
        }

        // ---- Pause button (top-right) ----
        var pauseBtn = MakeButton(hudRoot, "PauseButton", "II", Vector2.zero, new Vector2(70, 70));
        pauseBtn.GetComponent<Image>().sprite = _btnSquare;
        var pbl = pauseBtn.transform.Find("Label"); if (pbl != null) pbl.GetComponent<Text>().color = _ink;
        SetRect(pauseBtn.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(70, 70));
        Void(pauseBtn, pause.Toggle); Draggable(pauseBtn);

        // ---- Pause panel (hidden) ----
        var pausePanel = MakeImage(canvas.transform, "PausePanel", _square, new Color(0, 0, 0, 0.75f));
        SetRect(pausePanel, Vector2.zero, Vector2.one, Center(), Vector2.zero, Vector2.zero);
        // a framed panel card behind the pause buttons
        var pauseCard = MakeImage(pausePanel.transform, "Card", _panel, new Color(1, 1, 1, 0.97f));
        pauseCard.type = Image.Type.Sliced;
        SetRect(pauseCard, Center(), Center(), Center(), Vector2.zero, new Vector2(420, 560));
        var resume = MakeButton(pausePanel.transform, "Resume", "Resume", new Vector2(0, 180), new Vector2(320, 74));
        var editLayout = MakeButton(pausePanel.transform, "EditLayout", "Edit Layout", new Vector2(0, 90), new Vector2(320, 74));
        var settingsBtn = MakeButton(pausePanel.transform, "Settings", "Settings", new Vector2(0, 0), new Vector2(320, 74));
        var toMenu = MakeButton(pausePanel.transform, "Menu", "Back to Menu", new Vector2(0, -90), new Vector2(320, 74));
        var quit = MakeButton(pausePanel.transform, "Quit", "Exit", new Vector2(0, -180), new Vector2(320, 74));
        Void(resume, pause.Resume); Void(toMenu, pause.BackToMenu); Void(quit, pause.QuitGame);
        Void(editLayout, layout.EnterEdit);
        pausePanel.gameObject.SetActive(false);
        var settingsPanel = MakeSettingsPanel(canvas.transform);
        Void(settingsBtn, settingsPanel.Open);

        // ---- Layout edit bar (hidden until Edit Layout is chosen) ----
        // Compact, and draggable by any part of it, so it can be moved off the controls being arranged.
        var editBar = MakeImage(hudRoot, "EditBar", _panel, new Color(0.85f, 0.9f, 1f, 0.96f));
        editBar.type = Image.Type.Sliced;
        SetRect(editBar, Center(), Center(), Center(), new Vector2(0, 120), new Vector2(520, 118));
        editBar.gameObject.AddComponent<DraggablePanel>();
        var selLabel = MakeText(editBar.transform, "Selected", "Drag me  •  drag controls to move  •  tap one, then − / +", Center(), Center(), Center(), new Vector2(0, 38), new Vector2(500, 30), 17, TextAnchor.MiddleCenter);
        selLabel.color = new Color(0.15f, 0.3f, 0.6f);

        var reset = MakeButton(editBar.transform, "Reset", "Reset", Vector2.zero, new Vector2(120, 56));
        SetRect(reset.GetComponent<RectTransform>(), Center(), Center(), Center(), new Vector2(-150, -20), new Vector2(120, 56));
        var minus = MakeButton(editBar.transform, "Minus", "−", Vector2.zero, new Vector2(80, 56));
        SetRect(minus.GetComponent<RectTransform>(), Center(), Center(), Center(), new Vector2(-45, -20), new Vector2(80, 56));
        var plus = MakeButton(editBar.transform, "Plus", "+", Vector2.zero, new Vector2(80, 56));
        SetRect(plus.GetComponent<RectTransform>(), Center(), Center(), Center(), new Vector2(45, -20), new Vector2(80, 56));
        var done = MakeButton(editBar.transform, "Done", "Done", Vector2.zero, new Vector2(120, 56));
        SetRect(done.GetComponent<RectTransform>(), Center(), Center(), Center(), new Vector2(150, -20), new Vector2(120, 56));
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
        Set(hud, "fireLabel", fireLabel);
        Set(pause, "panel", pausePanel.gameObject);
        Set(pause, "menuScene", "Menu");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // =============================================================== UI helpers
    /// <summary>Full-screen dimmed overlay with move/aim sensitivity sliders (hidden by default).</summary>
    static SettingsPanel MakeSettingsPanel(Transform parent)
    {
        var dim = MakeImage(parent, "SettingsPanel", _square, new Color(0, 0, 0, 0.75f));
        SetRect(dim, Vector2.zero, Vector2.one, Center(), Vector2.zero, Vector2.zero);
        var card = MakeImage(dim.transform, "Card", _panel, new Color(1, 1, 1, 0.97f));
        card.type = Image.Type.Sliced;
        SetRect(card, Center(), Center(), Center(), Vector2.zero, new Vector2(680, 480));

        MakeText(card.transform, "Title", "Settings", Center(), Center(), Center(), new Vector2(0, 190), new Vector2(500, 50), 36, TextAnchor.MiddleCenter).color = _ink;
        var moveLabel = MakeText(card.transform, "MoveLabel", "Move sensitivity: 1.0x", Center(), Center(), Center(), new Vector2(0, 110), new Vector2(560, 40), 28, TextAnchor.MiddleCenter);
        moveLabel.color = _ink;
        var moveSlider = MakeSlider(card.transform, "MoveSlider", new Vector2(0, 55));
        var aimLabel = MakeText(card.transform, "AimLabel", "Aim sensitivity: 1.0x", Center(), Center(), Center(), new Vector2(0, -30), new Vector2(560, 40), 28, TextAnchor.MiddleCenter);
        aimLabel.color = _ink;
        var aimSlider = MakeSlider(card.transform, "AimSlider", new Vector2(0, -85));
        var close = MakeButton(card.transform, "Close", "Done", new Vector2(0, -175), new Vector2(240, 70));

        var sp = dim.gameObject.AddComponent<SettingsPanel>();
        Set(sp, "moveSlider", moveSlider); Set(sp, "aimSlider", aimSlider);
        Set(sp, "moveLabel", moveLabel); Set(sp, "aimLabel", aimLabel);
        Void(close, sp.Close);
        dim.gameObject.SetActive(false);
        return sp;
    }

    static Slider MakeSlider(Transform parent, string name, Vector2 pos)
    {
        var root = MakeUI(parent, name);
        SetRect(root, Center(), Center(), Center(), pos, new Vector2(520, 48));

        var bg = MakeImage(root, "Background", _square, new Color(0.7f, 0.74f, 0.82f, 1f));
        SetRect(bg, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Center(), Vector2.zero, new Vector2(0, 14));

        var fillArea = MakeUI(root, "Fill Area");
        SetRect(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Center(), Vector2.zero, new Vector2(-24, 14));
        var fill = MakeImage(fillArea, "Fill", _square, new Color(0.25f, 0.55f, 0.95f, 1f));
        SetRect(fill, Vector2.zero, new Vector2(0, 1), Center(), Vector2.zero, new Vector2(10, 0));

        var handleArea = MakeUI(root, "Handle Slide Area");
        SetRect(handleArea, Vector2.zero, Vector2.one, Center(), Vector2.zero, new Vector2(-24, 0));
        var handle = MakeImage(handleArea, "Handle", _knob, Color.white);
        SetRect(handle, new Vector2(0, 0), new Vector2(0, 1), Center(), Vector2.zero, new Vector2(44, 0));

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = GameSettings.MinSens; slider.maxValue = GameSettings.MaxSens; slider.value = 1f;
        return slider;
    }

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
        es.AddComponent<UIInputGuard>(); // keyboard stays for gameplay; UI is touch/pointer only
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
        t.raycastTarget = false; // labels never need touches (buttons get them via their face)
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(rt, aMin, aMax, pivot, pos, size);
        return t;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Vector2 centerPos, Vector2 size)
    {
        var img = MakeImage(parent, name, _btnRect, Color.white);
        img.type = Image.Type.Sliced;
        SetRect(img, Center(), Center(), Center(), centerPos, size);
        var btn = img.gameObject.AddComponent<Button>();
        btn.navigation = new Navigation { mode = Navigation.Mode.None }; // touch/pointer only
        AddLabel(img.transform, label, 24);
        return img.gameObject;
    }

    /// <summary>
    /// Give a frequently-changing widget its own sub-canvas so updating it doesn't
    /// rebuild the whole HUD mesh (a real CPU cost on low-end phones). Interactive
    /// widgets also need their own GraphicRaycaster to keep receiving touches.
    /// </summary>
    static void Isolate(GameObject go, bool interactive)
    {
        if (go.GetComponent<Canvas>() == null) go.AddComponent<Canvas>();
        if (interactive && go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
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
        var b = MakeImage(parent, name, _ring, new Color(1, 1, 1, 0.5f));
        SetRect(b, anchor, anchor, anchor, offset, new Vector2(240, 240));
        var h = MakeImage(b.transform, "Handle", _knob, Color.white);
        SetRect(h, Center(), Center(), Center(), Vector2.zero, new Vector2(120, 120));

        var j = b.gameObject.AddComponent<VirtualJoystick>();
        Set(j, "axis", axis);
        Set(j, "background", b.GetComponent<RectTransform>());
        Set(j, "handle", h.GetComponent<RectTransform>());
        Set(j, "radius", 90f);
        b.gameObject.AddComponent<DraggableHUDElement>(); // movable in layout editor
        Isolate(b.gameObject, interactive: true);         // handle moves every drag frame

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
