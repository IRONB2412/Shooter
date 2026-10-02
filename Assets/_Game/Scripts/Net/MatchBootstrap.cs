using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Decides — from the menu's MatchSettings — whether this match is offline (vs
/// bots) or online, and starts the right systems.
///
/// The online rig (NetworkManager + transport + spawner) is built entirely at
/// RUNTIME here, so the saved scene contains no NetworkManager/NetworkObject. That
/// keeps the editor free of Netcode's hierarchy validation (NetworkManager must be
/// a root object and may not share a hierarchy with a NetworkObject). See the
/// project memory "ngo-hierarchy-rules".
/// </summary>
public class MatchBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject offlineManager;   // holds the offline GameManager
    [SerializeField] private GameObject netPlayerPrefab;  // PlayerNet (has NetworkObject)
    [SerializeField] private GameObject netBotPrefab;     // BotNet (has NetworkObject)
    [SerializeField] private ushort port = 7777;

    private void Start()
    {
        bool online = MatchSettings.Mode != GameMode.Bots;
        if (offlineManager != null) offlineManager.SetActive(!online);
        if (online) BuildAndStartNetwork();
    }

    private void BuildAndStartNetwork()
    {
        // Single root object: NetworkManager + transport + a plain spawner. No
        // NetworkObject here, and it's a root — both NGO rules satisfied.
        var go = new GameObject("NetworkManager");
        var nm = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();

        nm.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = true,
            ConnectionApproval = false,
            Prefabs = new NetworkPrefabs()
        };
        nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = netPlayerPrefab });
        nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = netBotPrefab });

        var spawner = go.AddComponent<NetworkGameManager>();
        spawner.Configure(netPlayerPrefab, netBotPrefab);

        if (MatchSettings.Mode == GameMode.OnlineHost)
        {
            transport.SetConnectionData("0.0.0.0", port); // listen on all interfaces
            nm.StartHost();
        }
        else
        {
            transport.SetConnectionData(MatchSettings.JoinAddress, port);
            nm.StartClient();
        }
    }
}
