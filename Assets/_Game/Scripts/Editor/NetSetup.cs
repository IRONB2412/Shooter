using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds the online layer on top of the offline game: creates networked Player/Bot
/// prefabs (NetworkObject + NetworkFighter + owner-authoritative transform) and
/// wires the Game scene with a NetworkManager, transport, spawner and the
/// MatchBootstrap that switches between offline and online. Idempotent.
/// Menu: Shooter > Build Networking (run AFTER Build Game).
/// </summary>
public static class NetSetup
{
    const string PrefabDir = "Assets/_Game/Prefabs";
    const string ScenePath = "Assets/_Game/Scenes/Game.unity";

    [MenuItem("Shooter/Build Networking")]
    public static void BuildAll()
    {
        var playerNet = MakeNetPrefab("Player", "PlayerNet", Team.Player, isBot: false);
        var botNet = MakeNetPrefab("Bot", "BotNet", Team.Enemy, isBot: true);
        SetupScene(playerNet, botNet);
        AssetDatabase.SaveAssets();
        Debug.Log("Networking build complete.");
    }

    /// <summary>Clone a base fighter prefab and turn it into a networked one.</summary>
    static GameObject MakeNetPrefab(string baseName, string netName, Team team, bool isBot)
    {
        string basePath = $"{PrefabDir}/{baseName}.prefab";
        string netPath = $"{PrefabDir}/{netName}.prefab";

        var contents = PrefabUtility.LoadPrefabContents(basePath);

        // Health -> NetworkFighter (same IDamageable role, but networked).
        var health = contents.GetComponent<Health>();
        if (health != null) Object.DestroyImmediate(health, true);

        if (contents.GetComponent<NetworkObject>() == null) contents.AddComponent<NetworkObject>();
        if (contents.GetComponent<OwnerNetworkTransform>() == null) contents.AddComponent<OwnerNetworkTransform>();

        var fighter = contents.GetComponent<NetworkFighter>() ?? contents.AddComponent<NetworkFighter>();
        Set(fighter, "team", team);
        Set(fighter, "isBot", isBot);
        Set(fighter, "maxHealth", 100f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(contents, netPath);
        PrefabUtility.UnloadPrefabContents(contents);
        return prefab;
    }

    static void SetupScene(GameObject playerNet, GameObject botNet)
    {
        // Operate on the Game scene if it's already open (avoids reloading a possibly
        // invalid on-disk version, which would pop Netcode's validation dialog).
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Game" || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath);

        // Clean previous online objects so re-runs don't stack.
        foreach (var n in new[] { "Online", "NetworkRig", "NetworkManager", "NetGameManager", "OfflineManager", "Bootstrap" })
        {
            var old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }

        // Keep PoolManager + InputProvider always-on; move GameManager to its own toggled object.
        var systems = GameObject.Find("Systems");
        var oldGm = systems != null ? systems.GetComponent<GameManager>() : null;
        GameObject basePlayer = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Player.prefab");
        GameObject baseBot = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Bot.prefab");
        var spawns = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        if (oldGm != null) Object.DestroyImmediate(oldGm);

        // Offline manager (vs-bots path)
        var offlineGO = new GameObject("OfflineManager");
        var gm = offlineGO.AddComponent<GameManager>();
        Set(gm, "playerPrefab", basePlayer);
        Set(gm, "botPrefab", baseBot);
        Set(gm, "spawnPoints", spawns);

        // No NetworkManager/NetworkObject is placed in the scene — MatchBootstrap builds
        // the whole online rig at runtime (see MatchBootstrap / memory ngo-hierarchy-rules),
        // so the editor never runs Netcode's hierarchy validation on this scene.
        var bootGO = new GameObject("Bootstrap");
        var boot = bootGO.AddComponent<MatchBootstrap>();
        Set(boot, "offlineManager", offlineGO);
        Set(boot, "netPlayerPrefab", playerNet);
        Set(boot, "netBotPrefab", botNet);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void Set(Component c, string field, object value)
    {
        var f = c.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning($"No field '{field}' on {c.GetType().Name}"); return; }
        f.SetValue(c, value);
    }
}
