/// <summary>Настройки партии, снятые с инспектора GameSystem.</summary>
public readonly struct MatchSettings
{
    public readonly PieceColor PlayerColor;
    public readonly float BotTimeBudgetMs;
    public readonly float FrameBudgetMs;
    public readonly int SimulationsPerFrame;
    public readonly bool AutoStartNewGame;
    public readonly float NewGameDelaySeconds;

    public MatchSettings(
        PieceColor playerColor,
        float botTimeBudgetMs,
        float frameBudgetMs,
        int simulationsPerFrame,
        bool autoStartNewGame,
        float newGameDelaySeconds)
    {
        PlayerColor = playerColor;
        BotTimeBudgetMs = botTimeBudgetMs;
        FrameBudgetMs = frameBudgetMs;
        SimulationsPerFrame = simulationsPerFrame;
        AutoStartNewGame = autoStartNewGame;
        NewGameDelaySeconds = newGameDelaySeconds;
    }
}
