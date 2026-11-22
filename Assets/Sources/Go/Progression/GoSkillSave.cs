/// <summary>
/// Сохраняемый прогресс: ровно то, что уходит в хранилище.
/// Отдельный DTO, чтобы трекер не знал про формат ключей и PlayerPrefs.
/// </summary>
public readonly struct GoSkillSave
{
    public readonly int Level;
    public readonly int HighestDefeatedLevel;
    public readonly int RankedGames;
    public readonly bool TutorialCompleted;

    /// <summary>История последних партий: '1' — победа, '0' — поражение, от старой к новой.</summary>
    public readonly string History;

    public GoSkillSave(
        int level, int highestDefeatedLevel, int rankedGames, bool tutorialCompleted, string history)
    {
        Level = level;
        HighestDefeatedLevel = highestDefeatedLevel;
        RankedGames = rankedGames;
        TutorialCompleted = tutorialCompleted;
        History = history ?? string.Empty;
    }

    public static GoSkillSave Default => new GoSkillSave(1, 0, 0, false, string.Empty);
}
