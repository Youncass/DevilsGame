using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Прогрессия игрока: уровень, параметры сложности бота и обучающие подсказки.
/// Скрывает связку GoSkillTracker + GoDifficultyProfile + GoTutorialTriggers.
/// </summary>
public sealed class GameProgress
{
    private readonly GoDifficultyProfile _difficulty;
    private readonly GoSkillTracker _skill;
    private readonly GoTutorialTriggers _triggers;

    public GameProgress(GoDifficultyProfile difficulty)
    {
        _difficulty = difficulty ?? new GoDifficultyProfile();
        _skill = new GoSkillTracker();
        _skill.Changed += s => SkillChanged?.Invoke(s);

        _triggers = new GoTutorialTriggers(_difficulty);
        _triggers.Raised += e => TutorialRaised?.Invoke(e);
    }

    public event Action<GoTutorialEvent> TutorialRaised;
    public event Action<GoSkillSnapshot> SkillChanged;

    public int Level => _skill.Level;
    public int CompletedGames => _skill.CompletedGames;
    public bool IsTutorial => _skill.IsTutorial;
    public int DifficultyLevel => _skill.IsTutorial ? 0 : _skill.Level;
    public int Simulations => _difficulty.GetSimulations(DifficultyLevel);
    public float BlunderChance => _difficulty.GetBlunderChance(DifficultyLevel);
    public float TutorialTimeBudgetMs => _difficulty.TutorialTimeBudgetMs;

    public void Load() => _skill.Load();

    public void BeginGame() => _triggers.BeginGame(_skill.CompletedGames);

    public void OnMove(
        Board board, PieceColor mover, Vector2Int move, IReadOnlyList<Vector2Int> captured) =>
        _triggers.OnMove(board, mover, move, captured);

    /// <summary>Записывает результат партии и возвращает снимок для UI и логов.</summary>
    public GoGameResult ReportResult(ScoreBreakdown score, bool playerWon)
    {
        bool wasTutorial = _skill.IsTutorial;
        int levelBefore = _skill.Level;

        if (wasTutorial) _skill.ReportTutorialResult(playerWon);
        else _skill.ReportResult(playerWon);

        _triggers.OnGameOver(score, playerWon);

        return new GoGameResult(
            score, playerWon, wasTutorial, levelBefore, _skill.Level, _skill.CompletedGames);
    }
}
