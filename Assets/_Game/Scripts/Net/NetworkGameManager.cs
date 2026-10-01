using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-side match spawner for online play. Gives every connected client a
/// player object and spawns the chosen number of bots. Runs only on the server;
/// clients receive the spawned objects automatically via Netcode.
/// </summary>
public class NetworkGameManager : NetworkBehaviour
{
    [SerializeField] private GameObject netPlayerPrefab;
    [SerializeField] private GameObject netBotPrefab;

    private SpawnPoint[] _spawns;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        _spawns = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

        // A player for the host and anyone already connected, plus future joiners.
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds) SpawnPlayer(id);
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;

        for (int i = 0; i < MatchSettings.BotCount; i++) SpawnBot();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
    }

    private void SpawnPlayer(ulong clientId)
    {
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
        if (_spawns == null || _spawns.Length == 0) return Vector3.zero;
        return _spawns[Random.Range(0, _spawns.Length)].transform.position;
    }
}
