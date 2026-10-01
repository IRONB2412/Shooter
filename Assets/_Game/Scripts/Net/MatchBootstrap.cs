using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Decides — from the menu's MatchSettings — whether this match is offline (vs
/// bots) or online, and starts the right systems. Offline keeps using the plain
/// GameManager; online starts Netcode as host or client.
/// </summary>
public class MatchBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject offlineManager;   // holds GameManager
    [SerializeField] private GameObject networkRig;       // holds NetworkManager + NetworkGameManager
    [SerializeField] private ushort port = 7777;

    private void Start()
    {
        bool online = MatchSettings.Mode != GameMode.Bots;

        if (offlineManager != null) offlineManager.SetActive(!online);
        if (networkRig != null) networkRig.SetActive(online);
        if (!online) return;

        var nm = networkRig.GetComponent<NetworkManager>();
        var transport = networkRig.GetComponent<UnityTransport>();

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
