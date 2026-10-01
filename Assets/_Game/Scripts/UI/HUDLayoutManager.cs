using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the HUD layout editor: entered from the pause menu, shows an edit bar
/// (Reset / − / + / Done), and freezes the game while arranging. Buttons call
/// these methods. Move = drag a widget; resize = select a widget then − / +.
/// </summary>
public class HUDLayoutManager : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject editBar;
    [SerializeField] private Text selectedLabel;

    public void EnterEdit()
    {
        HUDLayout.EditMode = true;
        HUDLayout.Selected = null;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (editBar != null) editBar.SetActive(true);
        Time.timeScale = 0f; // freeze while arranging
    }

    public void ExitEdit()
    {
        HUDLayout.EditMode = false;
        HUDLayout.Selected = null;
        if (editBar != null) editBar.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ResizeBigger() => HUDLayout.Selected?.ResizeBy(1.12f);
    public void ResizeSmaller() => HUDLayout.Selected?.ResizeBy(1f / 1.12f);

    public void ResetLayout()
    {
        foreach (var e in HUDLayout.Elements) e.ResetToDefault();
    }

    private void Update()
    {
        if (selectedLabel == null || !HUDLayout.EditMode) return;
        selectedLabel.text = HUDLayout.Selected != null
            ? "Selected: " + HUDLayout.Selected.name
            : "Tap a control to select";
    }
}
