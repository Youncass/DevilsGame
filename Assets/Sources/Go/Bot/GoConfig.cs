using UnityEngine;

[CreateAssetMenu(fileName = "GoConfig", menuName = "Go/Config")]
public class GoConfig : ScriptableObject
{
    [Header("Komi")]
    [Tooltip("Komi для досок <= 9x9")]
    public float Komi9 = 5.5f;
    [Tooltip("Komi для досок <= 13x13")]
    public float Komi13 = 6.5f;
    [Tooltip("Komi для досок > 13x13")]
    public float Komi19 = 7.5f;

    [Header("Scoring")]
    [Tooltip("Множитель штрафа за камни в atari (base часть: stones * base)")]
    public float AtariPenaltyBase = 5f;
    [Tooltip("Дополнительный штраф за маленькие группы: stones * (bonus / sqrt(stones))")]
    public float AtariPenaltyBonus = 5f;

    [Header("MCTS / GRAVE")]
    public int GraveRefThreshold = 25;
    public float GraveBias = 1e-4f;
    public float Exploration = 1.414f;

    [Header("Rollout")]
    [Tooltip("Максимум ходов в rollout: n + RolloutExtraMoves (n = клеток доски)")]
    public int RolloutExtraMoves = 10;
    [Tooltip("Вероятность локального хода в корне (не в rollout)")]
    public float LocalBias = 0.4f;

    [Header("Rollout Techniques")]
    public MoveTechnique RolloutTechniques = MoveTechnique.FastRollout;
    public MoveTechnique RootTechniques = MoveTechnique.FullRoot;

    [Header("Atari Heuristics (корень)")]
    [Tooltip("Бонус к EffectiveVisits за спасение своей группы из atari")]
    public int AtariSaveBonus = 50;
    [Tooltip("Штраф к EffectiveVisits, если ход не спасает группу из atari")]
    public int AtariIgnorePenalty = 30;
    [Tooltip("Максимальная дистанция influence-распространения (size / InfluenceRangeDivisor)")]
    public float InfluenceRangeDivisor = 3f;

    public float GetKomi(int boardSize) => boardSize switch
    {
        <= 9 => Komi9,
        <= 13 => Komi13,
        _ => Komi19,
    };
}
