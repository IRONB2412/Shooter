using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-side match spawner for online play. This is a PLAIN component that sits
/// on the NetworkManager GameObject (not a NetworkBehaviour, so it needs no
/// NetworkObject — which keeps the NetworkManager object free of the hierarchy
/// NetworkObjects aren't allowed to share). It listens to NetworkManager events:
/// gives every connected client a player and spawns the chosen number of bots.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class NetworkGameManager : MonoBehaviour
{
    [SerializeField] private GameObject netPlayerPrefab;
    [SerializeField] private GameObject netBotPrefab;

    private NetworkManager _nm;
    private SpawnPoint[] _spawns;

    /// <summary>Set the networked prefabs when this is created at runtime by MatchBootstrap.</summary>
    public void Configure(GameObject playerPrefab, GameObject botPrefab)
    {
        netPlayerPrefab = playerPrefab;
        netBotPrefab = botPrefab;
    }

    private void Awake() => _nm = GetComponent<NetworkManager>();

    private void OnEnable()
    {
        _nm.OnServerStarted += HandleServerStarted;
        _nm.OnClientConnectedCallback += HandleClientConnected;
    }

    private void OnDisable()
    {
        if (_nm == null) return;
        _nm.OnServerStarted -= HandleServerStarted;
        _nm.OnClientConnectedCallback -= HandleClientConnected;
    }

    // Server only: bots spawn once the server is up.
    private void HandleServerStarted()
    {
        if (!_nm.IsServer) return;
        _spawns = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < MatchSettings.BotCount; i++) SpawnBot();
    }

    // Fires for the host and every joining client; the server spawns their player.
    private void HandleClientConnected(ulong clientId)
    {
        if (!_nm.IsServer) return;
        if (_spawns == null) _spawns = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        var go = Instantiate(netPlayerPrefab, RandomSpawn(), Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    private void SpawnBot()
    {
        var go = Instantiate(netBotPrefab, RandomSpawn(), Quaternion.identity);
        go.GetComponent<NetworkObject>().Spawn();
        if (go.TryGetComponent<BotAI>(out var ai)) ai.Configure(MatchSettings.Difficulty);
    }

    private Vector3 RandomSpawn()
    {
        return SpawnPoint.Pick(_spawns); // keep fighters apart
    }
}
