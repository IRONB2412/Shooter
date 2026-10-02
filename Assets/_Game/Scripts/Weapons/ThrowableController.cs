using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the character's throwables (grenade / smoke / flash) and throws the
/// selected one in the aim direction. Shared by players and bots.
/// </summary>
[RequireComponent(typeof(CharacterMotor))]
public class ThrowableController : MonoBehaviour
{
    [SerializeField] private GameObject throwablePrefab;
    [SerializeField] private float spawnOffset = 0.5f;
    [SerializeField] private List<ThrowableData> items = new();
    [Tooltip("Cooldown between throws so holding the button doesn't spam.")]
    [SerializeField] private float throwCooldown = 0.6f;

    public event Action<ThrowableData, int> OnSelectionChanged; // (item, index)

    public IReadOnlyList<ThrowableData> Items => items;
    public int SelectedIndex { get; private set; }
    public ThrowableData Selected => (items.Count > 0) ? items[SelectedIndex] : null;

    private CharacterMotor _motor;
    private Team _team = Team.Enemy;
    private float _cooldown;

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor>();
        if (TryGetComponent<IDamageable>(out var dmg)) _team = dmg.Team;
    }

    private void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
    }

    public void Select(int index)
    {
        if (items.Count == 0) return;
        SelectedIndex = Mathf.Clamp(index, 0, items.Count - 1);
        OnSelectionChanged?.Invoke(Selected, SelectedIndex);
    }

    public void CycleSelection()
    {
        if (items.Count == 0) return;
        Select((SelectedIndex + 1) % items.Count);
    }

    /// <returns>true if a throw happened.</returns>
    public bool Throw()
    {
        if (Selected == null || _cooldown > 0f) return false;

        Vector2 aim = _motor.AimDir.sqrMagnitude > 0.001f ? _motor.AimDir.normalized : Vector2.right;
        Vector2 origin = (Vector2)transform.position + aim * spawnOffset;

        var go = PoolManager.Instance.Spawn(throwablePrefab, origin, Quaternion.identity);
        if (go != null && go.TryGetComponent<Throwable>(out var t))
            t.Init(Selected, aim, _team, gameObject, transform.position);

        _cooldown = throwCooldown;
        return true;
    }
}
