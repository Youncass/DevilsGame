using System;
using UnityEngine;

/// <summary>
/// Преобразование уровня игрока в параметры бота. Все поля вынесены в инспектор,
/// чтобы баланс можно было крутить без пересборки логики.
/// </summary>
[Serializable]
public class GoDifficultyProfile
{
    [Header("Обучение")]
    [Tooltip("Симуляций за ход в первом (обучающем) раунде")] [Min(1)] public int TutorialSimulations = 80;
    [Tooltip("Бюджет времени на ход в обучении, мс")] [Min(1)] public float TutorialTimeBudgetMs = 200f;
    [Range(0f, 1f)] [Tooltip("Вероятность намеренно слабого хода в обучении")]
    public float TutorialBlunderChance = 0.7f;

    [Header("Ранговые партии")]
    [Tooltip("Симуляции на 1-м уровне")] [Min(1)] public int BaseSimulations = 400;
    [Tooltip("Прирост симуляций за уровень")] [Min(0)] public int SimulationsPerLevel = 600;
    [Tooltip("Максимум симуляций (технический потолок)")] [Min(1)] public int MaxSimulations = 5000;

    [Tooltip("Базовая вероятность ошибки на 1-м уровне")] [Range(0f, 1f)] public float BlunderChanceBase = 0.12f;
    [Tooltip("На сколько снижать вероятность ошибки за уровень")] [Range(0f, 0.1f)] public float BlunderChancePerLevel = 0.015f;

    [Tooltip("Сколько первых партий показывать обучающие триггеры")] [Min(0)] public int TriggerGames = 2;

    public int GetSimulations(int level)
    {
        if (level <= 0) return TutorialSimulations;
        long value = BaseSimulations + (long)Math.Max(0, level - 1) * SimulationsPerLevel;
        return (int)ClampLong(value, 1, MaxSimulations);
    }

    public float GetBlunderChance(int level)
    {
        if (level <= 0) return TutorialBlunderChance;
        float chance = BlunderChanceBase - Math.Max(0, level - 1) * BlunderChancePerLevel;
        return ClampFloat(chance, 0f, 1f);
    }

    /// <param name="completedGames">Сколько партий уже завершено, включая обучение.</param>
    public bool ShouldRaiseTriggers(int completedGames) => completedGames < TriggerGames;

    private static long ClampLong(long value, long min, long max) =>
        value < min ? min : value > max ? max : value;

    private static float ClampFloat(float value, float min, float max) =>
        value < min ? min : value > max ? max : value;
}
