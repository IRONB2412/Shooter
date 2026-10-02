using UnityEngine;

/// <summary>
/// A one-shot ParticleSystem that plays when spawned from the pool and returns
/// itself to the pool once all its particles die. Lets us fire muzzle flashes,
/// impacts, explosions and blood without any Instantiate/Destroy churn.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class PooledParticle : MonoBehaviour, IPoolable
{
    private ParticleSystem _ps;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
    }

    public void OnSpawned() { _ps.Clear(); _ps.Play(); }
    public void OnDespawned() { _ps.Clear(); }

    // Effects are made of several child systems, so wait until ALL of them are done
    // (a single root-system callback would cut the longer-lived children short).
    private void Update()
    {
        if (!_ps.IsAlive(true)) Return();
    }

    private void Return()
    {
        if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
        else gameObject.SetActive(false);
    }
}
