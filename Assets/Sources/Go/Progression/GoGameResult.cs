public readonly struct GoGameResult
{
    public readonly ScoreBreakdown Score;
    public readonly bool PlayerWon;
    public readonly bool WasTutorial;
    public readonly int LevelBefore;
    public readonly int LevelAfter;
    public readonly int CompletedGames;

    public GoGameResult(
        ScoreBreakdown score, bool playerWon, bool wasTutorial,
        int levelBefore, int levelAfter, int completedGames)
    {
        Score = score;
        PlayerWon = playerWon;
        WasTutorial = wasTutorial;
        LevelBefore = levelBefore;
        LevelAfter = levelAfter;
        CompletedGames = completedGames;
    }
}
