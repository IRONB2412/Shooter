using UnityEngine;

/// <summary>
/// Aim trail for the LOCAL player: a fading laser from the gun along the exact
/// firing direction, stopping at the first wall. Bullets travel along this same
/// line, so what you see is where you'll hit. One raycast per frame (cheap).
/// Added automatically by LocalPlayerDriver; hidden for remote players / bots.
/// </summary>
[RequireComponent(typeof(CharacterMotor))]
public class AimLine : MonoBehaviour
{
    [SerializeField] private float maxLength = 9f;
    [SerializeField] private float width = 0.06f;
    [SerializeField] private Color color = new(1f, 0.25f, 0.2f, 0.75f);

    private CharacterMotor _motor;
    private WeaponController _weapon;
    private LocalPlayerDriver _driver;
    private LineRenderer _line;
    private int _wallMask;

    private void Awake()
    {
        _motor = GetComponent<CharacterMotor>();
        _weapon = GetComponent<WeaponController>();
        _driver = GetComponent<LocalPlayerDriver>();
        _wallMask = LayerMask.GetMask("Obstacle", "Destructible");

        var go = new GameObject("AimLine");
        go.transform.SetParent(transform, false);
        _line = go.AddComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.useWorldSpace = true;
        _line.widthMultiplier = width;
        _line.numCapVertices = 2;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.sortingOrder = 9; // under characters (10), over the floor/walls

        // Reuse the character's sprite material: always included in builds, no Shader.Find.
        if (TryGetComponent<SpriteRenderer>(out var sr)) _line.sharedMaterial = sr.sharedMaterial;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        _line.colorGradient = fade;
    }

    private void LateUpdate()
    {
        bool show = _driver != null && _driver.isActiveAndEnabled && _motor.enabled;
        _line.enabled = show;
        if (!show) return;

        Vector2 dir = _motor.AimDir.sqrMagnitude > 0.001f ? _motor.AimDir.normalized : Vector2.right;
        Vector2 start = (Vector2)transform.position + dir * (_weapon != null ? _weapon.MuzzleOffset : 0.6f); // identical to where bullets spawn
        var hit = Physics2D.Raycast(start, dir, maxLength, _wallMask);
        Vector2 end = hit.collider != null ? hit.point : start + dir * maxLength;

        _line.SetPosition(0, start);
        _line.SetPosition(1, end);
    }
}
