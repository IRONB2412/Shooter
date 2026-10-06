using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Whites-out the screen when a flash grenade goes off near the local player.
/// Listens to Throwable.OnFlash and fades out over the effect duration.
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenFlash : MonoBehaviour
{
    private Image _img;
    private float _alpha;
    private float _fadeSpeed;
    private float _hold;
    private static int _wallMask = -1;

    private void Awake()
    {
        _img = GetComponent<Image>();
        SetAlpha(0f);
    }

    private void OnEnable() => Throwable.OnFlash += OnFlash;
    private void OnDisable() => Throwable.OnFlash -= OnFlash;

    private void OnFlash(Vector2 center, float radius, float duration)
    {
        Transform player = LocalPlayerDriver.Current;
        if (player == null && GameManager.Instance != null && GameManager.Instance.LocalPlayer != null)
            player = GameManager.Instance.LocalPlayer.transform;
        if (player == null) return;

        // Only blind the player if they were within the flash radius...
        Vector2 pos = player.position;
        float dist = Vector2.Distance(pos, center);
        if (dist > radius) return;

        // ...and a wall between them and the burst shields their eyes.
        if (_wallMask < 0) _wallMask = LayerMask.GetMask("Obstacle", "Destructible");
        if (Physics2D.Linecast(center, pos, _wallMask).collider != null) return;

        // Whiteout starts THIS frame at full strength; closer to the burst = blinded for longer.
        float closeness = 1f - dist / Mathf.Max(radius, 0.01f);
        float total = duration * Mathf.Lerp(0.6f, 1f, closeness);
        _alpha = 1f;
        _hold = total * 0.4f;                       // stay fully white first, then fade out
        _fadeSpeed = 1f / Mathf.Max(0.2f, total * 0.6f);
        SetAlpha(1f);                               // show it immediately (the hold used to delay this)
    }

    private void Update()
    {
        if (_alpha <= 0f) return;
        if (_hold > 0f) { _hold -= Time.unscaledDeltaTime; return; }
        _alpha = Mathf.Max(0f, _alpha - _fadeSpeed * Time.unscaledDeltaTime);
        SetAlpha(_alpha);
    }

    private void SetAlpha(float a)
    {
        var c = _img.color; c.a = a; _img.color = c;
        _img.raycastTarget = false; // never block touches
    }
}
