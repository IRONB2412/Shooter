using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game heads-up display. Binds to the local player (spawned at runtime) and
/// mirrors its health, ammo, current weapon, kills and selected throwable.
/// The on-screen buttons call the public methods here.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Readouts")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Text ammoText;
    [SerializeField] private Text weaponText;
    [SerializeField] private Text killsText;
    [SerializeField] private Text throwableText;
    [Tooltip("Label on the single FIRE/THROW button.")]
    [SerializeField] private Text fireLabel;

    private GameObject _player;
    private Health _health;
    private WeaponController _weapon;
    private ThrowableController _throwable;

    private void Update()
    {
        // The player spawns after the scene loads, so bind lazily then hold.
        if (_player == null)
        {
            if (GameManager.Instance == null || GameManager.Instance.LocalPlayer == null) return;
            Bind(GameManager.Instance.LocalPlayer);
        }
    }

    private void Bind(GameObject player)
    {
        _player = player;
        _health = player.GetComponent<Health>();
        _weapon = player.GetComponent<WeaponController>();
        _throwable = player.GetComponent<ThrowableController>();

        _health.OnChanged += OnHealth;
        _weapon.OnAmmoChanged += OnAmmo;
        _weapon.OnWeaponChanged += OnWeapon;
        _throwable.OnSelectionChanged += OnThrowable;
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerKillsChanged += OnKills;

        // Prime the display with current values.
        OnHealth(_health.Current, _health.Max);
        OnWeapon(_weapon.Current);
        OnAmmo(_weapon.Current, _weapon.CurrentAmmo);
        OnThrowable(_throwable.Selected, _throwable.SelectedIndex);
        OnKills(GameManager.Instance != null ? GameManager.Instance.PlayerKills : 0);
    }

    private void OnHealth(float cur, float max)
    {
        if (healthFill != null) healthFill.fillAmount = max > 0 ? cur / max : 0;
    }

    private void OnAmmo(WeaponData w, int ammo)
    {
        if (ammoText != null) ammoText.text = w != null ? $"{ammo}/{w.magazineSize}" : "";
    }

    private void OnWeapon(WeaponData w)
    {
        if (weaponText != null) weaponText.text = w != null ? w.displayName : "";
    }

    private void OnThrowable(ThrowableData t, int index)
    {
        RefreshMode();
    }

    private void OnKills(int kills)
    {
        if (killsText != null) killsText.text = "Kills: " + kills;
    }

    // ---- Button hooks ----
    public void SelectWeapon(int slot) => InputProvider.Instance?.SelectWeapon(slot);
    /// <summary>
    /// Mode button: cycles Gun -> each throwable -> Gun. The one action button then
    /// shoots in Gun mode and throws the selected item otherwise (never both at once).
    /// </summary>
    public void CycleThrowable()
    {
        var inp = InputProvider.Instance;
        if (inp == null || _throwable == null || _throwable.Items.Count == 0) return;

        if (!inp.ThrowMode) { inp.SetThrowMode(true); _throwable.Select(0); }
        else if (_throwable.SelectedIndex >= _throwable.Items.Count - 1) inp.SetThrowMode(false);
        else _throwable.CycleSelection();
        RefreshMode();
    }

    private void RefreshMode()
    {
        bool throwing = InputProvider.Instance != null && InputProvider.Instance.ThrowMode;
        if (throwableText != null)
            throwableText.text = throwing && _throwable?.Selected != null ? _throwable.Selected.displayName : "Gun";
        if (fireLabel != null) fireLabel.text = throwing ? "THROW" : "FIRE";
    }
    public void Reload() { if (_weapon != null) _weapon.Reload(); }
}
