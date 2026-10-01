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

    private void Awake()
    {
        _img = GetComponent<Image>();
        SetAlpha(0f);
    }

    private void OnEnable() => Throwable.OnFlash += OnFlash;
    private void OnDisable() => Throwable.OnFlash -= OnFlash;

    private void OnFlash(Vector2 center, float radius, float duration)
    {
        var player = GameManager.Instance != null ? GameManager.Instance.LocalPlayer : null;
        if (player == null) return;

        // Only blind the player if they were within the flash radius.
        if (((Vector2)player.transform.position - center).sqrMagnitude > radius * radius) return;

        _alpha = 1f;
        _fadeSpeed = 1f / Mathf.Max(0.2f, duration);
    }

    private void Update()
    {
        if (_alpha <= 0f) return;
        _alpha = Mathf.Max(0f, _alpha - _fadeSpeed * Time.unscaledDeltaTime);
        SetAlpha(_alpha);
    }

    private void SetAlpha(float a)
    {
        var c = _img.color; c.a = a; _img.color = c;
        _img.raycastTarget = false; // never block touches
    }
}
