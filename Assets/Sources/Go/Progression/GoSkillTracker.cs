using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public struct GoSkillSnapshot
{
    public int Level;
    public int HighestDefeatedLevel;
    public int RankedGames;
    public int CompletedGames;
    public bool TutorialCompleted;
    public bool IsTutorial => !TutorialCompleted;
    public int RecentWins;
    public int RecentLosses;

    public override string ToString() =>
        $"level={Level} floor={HighestDefeatedLevel} games={RankedGames} " +
        $"tutorial={!TutorialCompleted} recent W/L={RecentWins}/{RecentLosses}";
}

/// <summary>
/// Уровень игрока по результатам нескольких партий. Пол ("уже побеждённый уровень")
/// не даёт сложности опуститься ниже того, что игрок уже обыграл.
/// </summary>
public sealed class GoSkillTracker
{
    public const int MaxLevel = 8;
    private const int HistoryWindow = 3;
    private const int PromotionWins = 2;
    private const int DemotionLosses = 2;

    private const string DefaultKeyPrefix = "Go.Skill";
    private const string KeyLevel = "Level";
    private const string KeyFloor = "Floor";
    private const string KeyGames = "RankedGames";
    private const string KeyHistory = "History";
    private const string KeyTutorial = "TutorialDone";

    private readonly string _prefixLevel;
    private readonly string _prefixFloor;
    private readonly string _prefixGames;
    private readonly string _prefixHistory;
    private readonly string _prefixTutorial;
    private readonly List<bool> _recent = new();

    public GoSkillTracker(string keyPrefix = DefaultKeyPrefix)
    {
        if (string.IsNullOrEmpty(keyPrefix)) keyPrefix = DefaultKeyPrefix;
        if (!keyPrefix.EndsWith(".", StringComparison.Ordinal)) keyPrefix += ".";

        _prefixLevel = keyPrefix + KeyLevel;
        _prefixFloor = keyPrefix + KeyFloor;
        _prefixGames = keyPrefix + KeyGames;
        _prefixHistory = keyPrefix + KeyHistory;
        _prefixTutorial = keyPrefix + KeyTutorial;
    }

    public int Level { get; private set; } = 1;
    public int HighestDefeatedLevel { get; private set; }
    public int RankedGames { get; private set; }
    public bool TutorialCompleted { get; private set; }
    public int CompletedGames => TutorialCompleted ? RankedGames + 1 : 0;
    public bool IsTutorial => !TutorialCompleted;

    public event Action<GoSkillSnapshot> Changed;

    public GoSkillSnapshot Snapshot
    {
        get
        {
            int wins = 0, losses = 0;
            for (int i = 0; i < _recent.Count; i++)
            {
                if (_recent[i]) wins++; else losses++;
            }
            return new GoSkillSnapshot
            {
                Level = Level,
                HighestDefeatedLevel = HighestDefeatedLevel,
                RankedGames = RankedGames,
                CompletedGames = CompletedGames,
                TutorialCompleted = TutorialCompleted,
                RecentWins = wins,
                RecentLosses = losses,
            };
        }
    }

    public void Load()
    {
        TutorialCompleted = PlayerPrefs.GetInt(_prefixTutorial, 0) == 1;
        Level = ClampInt(PlayerPrefs.GetInt(_prefixLevel, 1), 1, MaxLevel);
        HighestDefeatedLevel = ClampInt(PlayerPrefs.GetInt(_prefixFloor, 0), 0, Level);
        RankedGames = Math.Max(0, PlayerPrefs.GetInt(_prefixGames, 0));

        _recent.Clear();
        string raw = PlayerPrefs.GetString(_prefixHistory, "");
        for (int i = 0; i < raw.Length && _recent.Count < HistoryWindow; i++)
            _recent.Add(raw[i] == '1');

        Changed?.Invoke(Snapshot);
    }

    /// <summary>Обучение всегда завершается переходом на 1-й уровень.</summary>
    public void ReportTutorialResult(bool playerWon)
    {
        if (TutorialCompleted) return;
        TutorialCompleted = true;
        Level = 1;
        HighestDefeatedLevel = 0;
        _recent.Clear();
        Save();
        Changed?.Invoke(Snapshot);
    }

    /// <summary>
    /// Победа игрока повышает пол и может повысить уровень. Поражение снижает
    /// уровень на 1, но никогда не ниже уже побеждённого уровня.
    /// </summary>
    public void ReportResult(bool playerWon)
    {
        if (!TutorialCompleted) ReportTutorialResult(playerWon);

        RankedGames++;
        if (playerWon && Level > HighestDefeatedLevel)
            HighestDefeatedLevel = Level;

        AddToHistory(playerWon);
        var s = Snapshot;
        bool changed = false;

        if (s.RecentWins >= PromotionWins && Level < MaxLevel)
        {
            Level++;
            changed = true;
        }
        else if (s.RecentLosses >= DemotionLosses && Level > HighestDefeatedLevel)
        {
            Level--;
            changed = true;
        }

        if (changed) _recent.Clear();
        Level = ClampInt(Level, HighestDefeatedLevel, MaxLevel);

        Save();
        Changed?.Invoke(Snapshot);
    }

    public void ResetAll()
    {
        PlayerPrefs.DeleteKey(_prefixLevel);
        PlayerPrefs.DeleteKey(_prefixFloor);
        PlayerPrefs.DeleteKey(_prefixGames);
        PlayerPrefs.DeleteKey(_prefixHistory);
        PlayerPrefs.DeleteKey(_prefixTutorial);
        PlayerPrefs.Save();

        Level = 1;
        HighestDefeatedLevel = 0;
        RankedGames = 0;
        TutorialCompleted = false;
        _recent.Clear();
        Changed?.Invoke(Snapshot);
    }

    private void AddToHistory(bool won)
    {
        _recent.Add(won);
        while (_recent.Count > HistoryWindow) _recent.RemoveAt(0);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(_prefixLevel, Level);
        PlayerPrefs.SetInt(_prefixFloor, HighestDefeatedLevel);
        PlayerPrefs.SetInt(_prefixGames, RankedGames);
        PlayerPrefs.SetInt(_prefixTutorial, TutorialCompleted ? 1 : 0);

        var sb = new StringBuilder(_recent.Count);
        for (int i = 0; i < _recent.Count; i++) sb.Append(_recent[i] ? '1' : '0');
        PlayerPrefs.SetString(_prefixHistory, sb.ToString());
        PlayerPrefs.Save();
    }

    private static int ClampInt(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
}
