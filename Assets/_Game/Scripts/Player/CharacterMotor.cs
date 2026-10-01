using UnityEngine;

/// <summary>
/// Shared movement + facing for BOTH players and bots.
/// Drivers (LocalPlayerDriver / BotAI) just set MoveInput and AimDir each frame;
/// this component turns that into physics motion. Keeping the motor separate from
/// the input source is what lets players and bots reuse the exact same body.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CharacterMotor : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [Tooltip("Optional child sprite that rotates to face AimDir. Leave empty to not rotate visuals.")]
    [SerializeField] private Transform visual;

    /// <summary>Desired move direction, magnitude 0..1. Set by the driver.</summary>
    public Vector2 MoveInput { get; set; }
    /// <summary>Normalized facing direction (where weapons fire). Set by the driver.</summary>
    public Vector2 AimDir { get; set; } = Vector2.right;

    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;               // top-down: no gravity
        _rb.freezeRotation = true;           // body never spins; only the visual turns
    }

    private void FixedUpdate()
    {
        Vector2 move = Vector2.ClampMagnitude(MoveInput, 1f);
        _rb.linearVelocity = move * moveSpeed;
    }

    private void LateUpdate()
    {
        if (visual != null && AimDir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(AimDir.y, AimDir.x) * Mathf.Rad2Deg;
            visual.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
