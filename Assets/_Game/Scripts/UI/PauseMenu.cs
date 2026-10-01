using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game pause overlay: freezes time and offers Resume / Back to Menu / Exit.
/// Toggled by the on-screen pause button or the Escape key.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private string menuScene = "Menu";

    public bool IsPaused { get; private set; }

    private void Start()
    {
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Toggle();
    }

    public void Toggle() => SetPaused(!IsPaused);
    public void Resume() => SetPaused(false);

    private void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (panel != null) panel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
