/// <summary>
/// Plain static carrier for the choices made in the menu, read by the game scene
/// after it loads. Not a MonoBehaviour, so it survives the scene change for free.
/// </summary>
public enum GameMode { Bots, OnlineHost, OnlineJoin }

public static class MatchSettings
{
    public static GameMode Mode = GameMode.Bots;
    public static BotDifficulty Difficulty = BotDifficulty.Normal;
    public static int BotCount = 5;
    public static string JoinAddress = "127.0.0.1";
}
