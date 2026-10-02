using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Keeps a few health packs on the map — deliberately rare. Packs appear at designated
/// PickupSpot objects (e.g. inside buildings) and at random open ground, a limited number at a
/// time, and come back only after a long delay once taken. Offline matches only (online health is
/// server-owned).
/// </summary>
public class HealthPickupSpawner : MonoBehaviour
{
    [SerializeField] private Tilemap floor;
    [SerializeField] private Sprite packSprite;
    [SerializeField] private int maxAtOnce = 3;
    [SerializeField] private int initial = 2;
    [SerializeField] private Vector2 respawnDelay = new(45f, 80f);
    [SerializeField] private float healAmount = 40f;
    [SerializeField] private int randomSpots = 14;
    [SerializeField] private float minSpacing = 12f;

    private readonly List<Vector2> _spots = new();
    private readonly List<HealthPickup> _pool = new();
    private int _live;

    private IEnumerator Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) yield break; // offline only

        if (floor == null)
        {
            var go = GameObject.Find("Floor");
            if (go != null) floor = go.GetComponent<Tilemap>();
        }
        if (floor == null || packSprite == null) yield break;

        yield return new WaitForFixedUpdate();

        // Designated spots first (rewarding places), then random open ground.
        var marked = new List<Vector2>();
        foreach (var s in FindObjectsByType<PickupSpot>(FindObjectsSortMode.None))
            marked.Add(s.transform.position);
        _spots.AddRange(marked);

        var spawns = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        var b = floor.cellBounds;
        for (int i = 0, tries = 0; i < randomSpots && tries++ < randomSpots * 30;)
        {
            var cell = new Vector3Int(Random.Range(b.xMin + 3, b.xMax - 3), Random.Range(b.yMin + 3, b.yMax - 3), 0);
            if (!floor.HasTile(cell)) continue;
            Vector2 p = floor.GetCellCenterWorld(cell);
            if (Physics2D.OverlapCircle(p, 1.2f) != null) continue;
            if (NearSpawn(spawns, p, 6f)) continue;
            _spots.Add(p); i++;
        }

        // Start with a couple on the map — one inside a building when there is one.
        int toPlace = Mathf.Min(initial, maxAtOnce);
        if (marked.Count > 0 && toPlace > 0) { PlaceAt(marked[Random.Range(0, marked.Count)]); toPlace--; }
        for (int i = 0; i < toPlace; i++) PlaceRandom();
    }

    private static bool NearSpawn(SpawnPoint[] spawns, Vector2 p, float d)
    {
        foreach (var s in spawns)
            if (((Vector2)s.transform.position - p).sqrMagnitude < d * d) return true;
        return false;
    }

    private void PlaceRandom()
    {
        if (_spots.Count == 0 || _live >= maxAtOnce) return;
        for (int tries = 0; tries < 30; tries++)
        {
            Vector2 p = _spots[Random.Range(0, _spots.Count)];
            if (TooClose(p)) continue;
            PlaceAt(p);
            return;
        }
    }

    private bool TooClose(Vector2 p)
    {
        foreach (var a in HealthPickup.Active)
            if (((Vector2)a.transform.position - p).sqrMagnitude < minSpacing * minSpacing) return true;
        return false;
    }

    private void PlaceAt(Vector2 p)
    {
        HealthPickup pack = null;
        foreach (var c in _pool)
            if (!c.gameObject.activeSelf) { pack = c; break; }

        if (pack == null)
        {
            var go = new GameObject("HealthPack");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = packSprite;
            sr.sortingOrder = 7;
            go.AddComponent<CircleCollider2D>();
            pack = go.AddComponent<HealthPickup>();
            pack.Collected += OnCollected;
            _pool.Add(pack);
        }

        pack.HealAmount = healAmount;
        pack.gameObject.SetActive(true);
        pack.Place(p);
        _live++;
    }

    private void OnCollected(HealthPickup _)
    {
        _live--;
        StartCoroutine(RespawnLater());
    }

    private IEnumerator RespawnLater()
    {
        yield return new WaitForSeconds(Random.Range(respawnDelay.x, respawnDelay.y));
        PlaceRandom();
    }
}
