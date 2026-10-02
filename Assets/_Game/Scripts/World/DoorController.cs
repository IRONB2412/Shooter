using UnityEngine;

/// <summary>
/// Auto door: opens while any player or bot is within its trigger radius and
/// closes when everyone leaves. Closed = the solid (child) collider blocks
/// movement, bullets and line-of-sight like a wall; open = it's disabled and the
/// door fades. The proximity trigger sits on this (Default-layer) object so it
/// never blocks bullets/LOS itself — only the Obstacle-layer child does.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DoorController : MonoBehaviour
{
    [Tooltip("Child collider on the Obstacle layer that blocks when the door is shut.")]
    [SerializeField] private Collider2D solidCollider;
    [Tooltip("Layers that count as 'someone is here' (Player + Enemy).")]
    [SerializeField] private LayerMask characterMask;
    [SerializeField] private Color closedColor = new(0.95f, 0.65f, 0.2f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float openAlpha = 0.22f;

    private SpriteRenderer _sr;
    private int _near; // how many characters are currently in range

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        ApplyState();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsCharacter(other)) { _near++; ApplyState(); }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsCharacter(other)) { _near = Mathf.Max(0, _near - 1); ApplyState(); }
    }

    private bool IsCharacter(Collider2D other)
        => (characterMask.value & (1 << other.gameObject.layer)) != 0;

    private void ApplyState()
    {
        bool open = _near > 0;
        if (solidCollider != null) solidCollider.enabled = !open;
        if (_sr != null)
        {
            var c = closedColor;
            c.a = open ? openAlpha : closedColor.a;
            _sr.color = c;
        }
    }
}
