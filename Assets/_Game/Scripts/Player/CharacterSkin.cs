using System;
using UnityEngine;

/// <summary>
/// Swaps a fighter's body sprite to match the equipped weapon (pistol / rifle / SMG pose)
/// and optionally picks a random skin each spawn (used by bots for variety).
/// Wired up by the editor's Shooter > Apply Art Pass.
/// </summary>
[RequireComponent(typeof(WeaponController))]
public class CharacterSkin : MonoBehaviour
{
    [Serializable]
    public struct Skin { public Sprite pistol, rifle, smg; }

    [SerializeField] private SpriteRenderer target;
    [SerializeField] private Skin[] skins;
    [Tooltip("Pick a random skin every time this fighter (re)spawns.")]
    [SerializeField] private bool randomPerSpawn;

    private WeaponController _weapon;
    private int _skin;

    private void Awake()
    {
        _weapon = GetComponent<WeaponController>();
        if (target == null) target = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        if (skins == null || skins.Length == 0) return;
        _skin = randomPerSpawn ? UnityEngine.Random.Range(0, skins.Length) : 0;
        _weapon.OnWeaponChanged += Apply;
        if (_weapon.Current != null) Apply(_weapon.Current); // weapon may already be equipped
    }

    private void OnDisable()
    {
        if (_weapon != null) _weapon.OnWeaponChanged -= Apply;
    }

    private void Apply(WeaponData w)
    {
        if (target == null || skins == null || skins.Length == 0) return;
        var s = skins[Mathf.Clamp(_skin, 0, skins.Length - 1)];

        int slot = 0;
        for (int i = 0; i < _weapon.Loadout.Count; i++)
            if (_weapon.Loadout[i] == w) { slot = i; break; }

        // Slot order from GameSetup: 0 Pistol, 1 AR, 2 SMG, 3 Fire gun.
        Sprite pick = slot == 0 ? s.pistol : slot == 2 ? s.smg : s.rifle;
        if (pick != null) target.sprite = pick;
    }
}
