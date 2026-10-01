using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Makes a HUD widget movable AND resizable while the layout editor is active,
/// and remembers both across sessions (PlayerPrefs, keyed by the object's name):
///  - Drag it to move.
///  - Tap it to select, then use the edit bar's − / + to resize.
/// Attach to any control the player should be able to arrange.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggableHUDElement : MonoBehaviour, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    private RectTransform _rt;
    private Canvas _canvas;
    private Vector2 _defaultPos;
    private Vector3 _defaultScale;
    private string _key;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        _defaultPos = _rt.anchoredPosition;
        _defaultScale = _rt.localScale;
        _key = "hud_" + gameObject.name;
        HUDLayout.Elements.Add(this);
    }

    private void OnDestroy() => HUDLayout.Elements.Remove(this);

    private void Start() => Load();

    public void OnPointerDown(PointerEventData e)
    {
        if (HUDLayout.EditMode) HUDLayout.Selected = this; // pick this one to resize
    }

    public void OnDrag(PointerEventData e)
    {
        if (!HUDLayout.EditMode) return;
        float scale = (_canvas != null && _canvas.scaleFactor > 0f) ? _canvas.scaleFactor : 1f;
        _rt.anchoredPosition += e.delta / scale;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (HUDLayout.EditMode) Save();
    }

    /// <summary>Scale the widget up/down (called by the edit bar's − / + buttons).</summary>
    public void ResizeBy(float factor)
    {
        float s = Mathf.Clamp(_rt.localScale.x * factor, 0.5f, 2.5f);
        _rt.localScale = new Vector3(s, s, 1f);
        Save();
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(_key + "_x", _rt.anchoredPosition.x);
        PlayerPrefs.SetFloat(_key + "_y", _rt.anchoredPosition.y);
        PlayerPrefs.SetFloat(_key + "_s", _rt.localScale.x);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (PlayerPrefs.HasKey(_key + "_x"))
            _rt.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(_key + "_x"), PlayerPrefs.GetFloat(_key + "_y"));
        if (PlayerPrefs.HasKey(_key + "_s"))
        {
            float s = PlayerPrefs.GetFloat(_key + "_s");
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Restore built-in position + size and forget the saved values.</summary>
    public void ResetToDefault()
    {
        _rt.anchoredPosition = _defaultPos;
        _rt.localScale = _defaultScale;
        PlayerPrefs.DeleteKey(_key + "_x");
        PlayerPrefs.DeleteKey(_key + "_y");
        PlayerPrefs.DeleteKey(_key + "_s");
        PlayerPrefs.Save();
    }
}
