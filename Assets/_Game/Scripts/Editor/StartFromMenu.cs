using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Pressing Play in the editor always boots the Menu scene first — exactly like a real
/// build — no matter which scene (Game, a level...) you're currently editing. Without
/// this, Play starts whatever scene is open and skips the menu flow.
/// Toggle: Shooter > Always Play From Menu (on by default, remembered per machine).
/// </summary>
[InitializeOnLoad]
public static class StartFromMenu
{
    const string MenuScene = "Assets/_Game/Scenes/Menu.unity";
    const string PrefKey = "Shooter.AlwaysPlayFromMenu";
    const string MenuItem = "Shooter/Always Play From Menu";

    static StartFromMenu() => EditorApplication.delayCall += Apply; // wait for the AssetDatabase

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuItem)]
    static void Toggle() { Enabled = !Enabled; Apply(); }

    [MenuItem(MenuItem, true)]
    static bool ToggleValidate() { Menu.SetChecked(MenuItem, Enabled); return true; }

    public static void Apply()
    {
        EditorSceneManager.playModeStartScene =
            Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) : null;
    }
}
