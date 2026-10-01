using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked replacement for Health on online fighters. Implements the same
/// IDamageable interface so weapons, bots and bullets treat it identically.
///
/// Model (simple, good for a friends match):
///  - Movement/aim replicate via OwnerNetworkTransform (owner-authoritative).
///  - Only the owner reads input (LocalPlayerDriver enabled for owner only).
///  - Bots think on the server only.
///  - Health is a server-owned NetworkVariable; damage is submitted to the server
///    by ServerRpc, so hits are authoritative even though bullets are local visuals.
/// </summary>
[RequireComponent(typeof(CharacterMotor), typeof(WeaponController), typeof(NetworkObject))]
public class NetworkFighter : NetworkBehaviour, IDamageable
{
    [SerializeField] private Team team = Team.Player;
    [SerializeField] private float maxHealth = 100f;
    [Tooltip("True for AI fighters (think on server, no camera/input).")]
    [SerializeField] private bool isBot;

    private readonly NetworkVariable<float> _hp = new(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public Team Team => team;
    public bool IsAlive => _hp.Value > 0f;
    public Transform Transform => transform;

    private CharacterMotor _motor;
    private SpriteRenderer _sprite;
    private Collider2D _collider;

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
    }

    public override void OnNetworkSpawn()
    {
        Combatants.Register(this);
        _hp.OnValueChanged += OnHpChanged;
        if (IsServer) _hp.Value = maxHealth;

        // Control gating: humans drive only their own; bots think only on server.
        var driver = GetComponent<LocalPlayerDriver>();
        if (driver != null) driver.enabled = IsOwner && !isBot;

        var bot = GetComponent<BotAI>();
        if (bot != null) bot.enabled = IsServer && isBot;

        // The local human player owns the camera.
        if (IsOwner && !isBot)
        {
            var cam = FindFirstObjectByType<CameraFollow>();
            if (cam != null) cam.SetTarget(transform);
        }
    }

    public override void OnNetworkDespawn()
    {
        Combatants.Unregister(this);
        _hp.OnValueChanged -= OnHpChanged;
    }

    // ---- IDamageable ----
    public void TakeDamage(float amount, GameObject source)
    {
        if (!IsAlive || amount <= 0f) return;
        if (IsServer) ServerApplyDamage(amount);
        else SubmitDamageServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitDamageServerRpc(float amount) => ServerApplyDamage(amount);

    private void ServerApplyDamage(float amount)
    {
        if (_hp.Value <= 0f) return;
        _hp.Value = Mathf.Max(0f, _hp.Value - amount);
        if (_hp.Value <= 0f) StartCoroutine(ServerRespawn());
    }

    private IEnumerator ServerRespawn()
    {
        yield return new WaitForSeconds(3f);
        var spawns = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        Vector3 pos = spawns.Length > 0 ? spawns[Random.Range(0, spawns.Length)].transform.position : Vector3.zero;
        TeleportClientRpc(pos);
        _hp.Value = maxHealth;
    }

    // Runs on the owner (host for bots, the player's client for humans).
    [ClientRpc]
    private void TeleportClientRpc(Vector3 pos)
    {
        if (IsOwner) transform.position = pos;
    }

    private void OnHpChanged(float _, float now)
    {
        SetDowned(now <= 0f);
    }

    /// <summary>Hide + disable a downed fighter until it respawns.</summary>
    private void SetDowned(bool downed)
    {
        if (_sprite != null) _sprite.enabled = !downed;
        if (_collider != null) _collider.enabled = !downed;
        if (_motor != null) _motor.enabled = !downed;
    }
}
