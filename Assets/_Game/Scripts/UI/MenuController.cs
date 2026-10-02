using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main-menu flow. Buttons call these methods to pick a mode, then load the Game
/// scene. Choices are stashed in MatchSettings, which the game scene reads.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject botsPanel;
    [SerializeField] private GameObject onlinePanel;

    [Header("Online")]
    [SerializeField] private InputField addressField;

    [Header("Bots")]
    [SerializeField] private Text botCountLabel;
    [SerializeField] private Text difficultyLabel;
    [Tooltip("Easy / Normal / Hard button faces — the chosen one is highlighted.")]
    [SerializeField] private Image[] difficultyButtons;

    private const string GameScene = "Game";

    private void Start() => ShowMain();

    // Android's Back button arrives as Escape: back out of a sub-panel, or quit from main.
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
        if (mainPanel != null && !mainPanel.activeSelf) ShowMain();
        else QuitGame();
    }

    // ---- Panel navigation ----
    public void ShowMain()   { Toggle(mainPanel, true);  Toggle(botsPanel, false); Toggle(onlinePanel, false); }
    public void ShowBots()   { Toggle(mainPanel, false); Toggle(botsPanel, true);  Toggle(onlinePanel, false); UpdateBotLabel(); UpdateDifficultyUI(); }
    public void ShowOnline() { Toggle(mainPanel, false); Toggle(botsPanel, false); Toggle(onlinePanel, true); }

    // ---- Bots mode ----
    public void SetDifficulty(int d)
    {
        MatchSettings.Difficulty = (BotDifficulty)Mathf.Clamp(d, 0, 2);
        UpdateDifficultyUI();
    }

    /// <summary>Show which difficulty is picked: label text + highlighted button.</summary>
    private void UpdateDifficultyUI()
    {
        if (difficultyLabel != null) difficultyLabel.text = "Difficulty: " + MatchSettings.Difficulty;
        if (difficultyButtons == null) return;
        for (int i = 0; i < difficultyButtons.Length; i++)
            if (difficultyButtons[i] != null)
                difficultyButtons[i].color = i == (int)MatchSettings.Difficulty
                    ? Color.white                       // selected: full colour
                    : new Color(1f, 1f, 1f, 0.4f);      // others: faded
    }

    public void ChangeBotCount(int delta)
    {
        MatchSettings.BotCount = Mathf.Clamp(MatchSettings.BotCount + delta, 1, 12);
        UpdateBotLabel();
    }

    public void PlayBots()
    {
        MatchSettings.Mode = GameMode.Bots;
        SceneManager.LoadScene(GameScene);
    }

    // ---- Online mode (netcode wired in Phase 4) ----
    public void HostOnline()
    {
        MatchSettings.Mode = GameMode.OnlineHost;
        SceneManager.LoadScene(GameScene);
    }

    public void JoinOnline()
    {
        MatchSettings.Mode = GameMode.OnlineJoin;
        if (addressField != null && !string.IsNullOrWhiteSpace(addressField.text))
            MatchSettings.JoinAddress = addressField.text.Trim();
        SceneManager.LoadScene(GameScene);
    }

    // ---- Exit ----
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void UpdateBotLabel()
    {
        if (botCountLabel != null) botCountLabel.text = "Bots: " + MatchSettings.BotCount;
    }

    private static void Toggle(GameObject go, bool on) { if (go != null) go.SetActive(on); }
}
