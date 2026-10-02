using UnityEngine;
using UnityEngine.UI;

/// <summary>Overlay with sensitivity sliders. Open/Close are wired to buttons by UIBuilder.</summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider moveSlider;
    [SerializeField] private Slider aimSlider;
    [SerializeField] private Text moveLabel;
    [SerializeField] private Text aimLabel;

    private void Awake()
    {
        if (moveSlider != null) moveSlider.onValueChanged.AddListener(v => { GameSettings.MoveSens = v; Refresh(); });
        if (aimSlider != null) aimSlider.onValueChanged.AddListener(v => { GameSettings.AimSens = v; Refresh(); });
    }

    private void OnEnable()
    {
        if (moveSlider != null) moveSlider.SetValueWithoutNotify(GameSettings.MoveSens);
        if (aimSlider != null) aimSlider.SetValueWithoutNotify(GameSettings.AimSens);
        Refresh();
    }

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    private void Refresh()
    {
        if (moveLabel != null) moveLabel.text = $"Move sensitivity: {GameSettings.MoveSens:0.0}x";
        if (aimLabel != null) aimLabel.text = $"Aim sensitivity: {GameSettings.AimSens:0.0}x";
    }
}
