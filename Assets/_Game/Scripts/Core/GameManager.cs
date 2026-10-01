using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs an offline deathmatch: spawns the local player and N bots at spawn points,
/// respawns anyone who dies, and tracks kills. Reads MatchSettings for bot count
/// and difficulty. Online play swaps this out for the networked flow (Phase 4).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject botPrefab;

    [Header("Spawning")]
    [Tooltip("Assign spawn points, or leave empty to auto-collect all SpawnPoint objects.")]
    [SerializeField] private SpawnPoint[] spawnPoints;
    [SerializeField] private float respawnDelay = 3f;

    [Header("Overrides (editor testing; -1 = use MatchSettings)")]
    [SerializeField] private int botCountOverride = -1;

    public GameObject LocalPlayer { get; private set; }
    public int PlayerKills { get; private set; }

    /// <summary>Fired when the player's kill count changes (HUD listens).</summary>
    public event Action<int> OnPlayerKillsChanged;

    private void Awake()
    {
        Instance = this;
        if (spawnPoints == null || spawnPoints.Length == 0)
            spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
    }

    private void Start()
    {
        SpawnPlayer();

        int bots = botCountOverride >= 0 ? botCountOverride : MatchSettings.BotCount;
        for (int i = 0; i < bots; i++) SpawnBot();
    }

    private void SpawnPlayer()
    {
        var go = Instantiate(playerPrefab, RandomSpawn(), Quaternion.identity);
        HookDeath(go, isPlayer: true);
        LocalPlayer = go;

        // Point the camera at the player.
        var cam = FindFirstObjectByType<CameraFollow>();
        if (cam != null) cam.SetTarget(go.transform);
    }

    private void SpawnBot()
    {
        var go = Instantiate(botPrefab, RandomSpawn(), Quaternion.identity);
        if (go.TryGetComponent<BotAI>(out var ai))
            ai.Configure(MatchSettings.Difficulty);
        HookDeath(go, isPlayer: false);
    }

    private void HookDeath(GameObject go, bool isPlayer)
    {
        if (!go.TryGetComponent<Health>(out var hp)) return;
        hp.OnDied += killer => OnDeath(go, killer, isPlayer);
    }

    private void OnDeath(GameObject victim, GameObject killer, bool victimIsPlayer)
    {
        // Award a kill if the local player got the frag (and didn't suicide).
        if (killer != null && killer == LocalPlayer && killer != victim)
        {
            PlayerKills++;
            OnPlayerKillsChanged?.Invoke(PlayerKills);
        }
        StartCoroutine(Respawn(victim));
    }

    private IEnumerator Respawn(GameObject go)
    {
        // Deactivate hides + disables control; OnDisable/OnEnable on Health also
        // revives it and refreshes the target registry, so no extra reset needed.
        go.SetActive(false);
        yield return new WaitForSeconds(respawnDelay);
        if (go == null) yield break;
        go.transform.position = RandomSpawn();
        go.SetActive(true);
    }

    private Vector3 RandomSpawn()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return Vector3.zero;
        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform.position;
    }
}
