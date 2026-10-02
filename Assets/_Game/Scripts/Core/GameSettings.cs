using UnityEngine;

/// <summary>
/// Player-adjustable control sensitivity, saved in PlayerPrefs. 1 = default; higher means
/// the stick reaches full tilt with less thumb travel (more responsive), lower is gentler.
/// </summary>
public static class GameSettings
{
    public const float MinSens = 0.5f, MaxSens = 2f;

    private static float _move = -1f, _aim = -1f;

    public static float MoveSens
    {
        get { if (_move < 0f) _move = Load("sens_move"); return _move; }
        set { _move = Clamp(value); PlayerPrefs.SetFloat("sens_move", _move); PlayerPrefs.Save(); }
    }

    public static float AimSens
    {
        get { if (_aim < 0f) _aim = Load("sens_aim"); return _aim; }
        set { _aim = Clamp(value); PlayerPrefs.SetFloat("sens_aim", _aim); PlayerPrefs.Save(); }
    }

    private static float Load(string key) => Clamp(PlayerPrefs.GetFloat(key, 1f));
    private static float Clamp(float v) => Mathf.Clamp(v, MinSens, MaxSens);
}
