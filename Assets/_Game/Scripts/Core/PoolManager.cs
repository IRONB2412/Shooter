using System.Collections.Generic;
using UnityEngine;

/// <summary>Optional callbacks for pooled objects to reset themselves.</summary>
public interface IPoolable
{
    void OnSpawned();
    void OnDespawned();
}

/// <summary>
/// Simple prefab pool. Reusing bullets / effects / throwables instead of
/// Instantiate+Destroy keeps GC and frame spikes low on large maps.
/// Usage: PoolManager.Instance.Spawn(prefab, pos, rot) / Despawn(go).
/// </summary>
public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    // prefab -> ready-to-reuse instances
    private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();
    // spawned instance -> its source prefab (so Despawn knows where to return it)
    private readonly Dictionary<GameObject, GameObject> _origin = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;

        if (!_pools.TryGetValue(prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            _pools[prefab] = queue;
        }

        GameObject go = queue.Count > 0 ? queue.Dequeue() : Instantiate(prefab);
        _origin[go] = prefab;

        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);

        if (go.TryGetComponent<IPoolable>(out var p)) p.OnSpawned();
        return go;
    }

    public void Despawn(GameObject go)
    {
        if (go == null) return;

        if (go.TryGetComponent<IPoolable>(out var p)) p.OnDespawned();
        go.SetActive(false);

        // If it wasn't pooled (edge case) just disable it safely.
        if (_origin.TryGetValue(go, out var prefab) && _pools.TryGetValue(prefab, out var queue))
            queue.Enqueue(go);
    }

    /// <summary>Despawn after a delay (used by bullets/effects with a lifetime).</summary>
    public void DespawnDelayed(GameObject go, float delay)
    {
        if (go != null) StartCoroutine(DespawnRoutine(go, delay));
    }

    private System.Collections.IEnumerator DespawnRoutine(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null && go.activeSelf) Despawn(go);
    }
}
