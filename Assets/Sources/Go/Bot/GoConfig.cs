using UnityEngine;

[CreateAssetMenu(fileName = "GoConfig", menuName = "Go/Config")]
public class GoConfig : ScriptableObject
{
    [Header("Komi")]
    [Tooltip("Komi для досок <= 9x9")] public float Komi9 = 5.5f;
    [Tooltip("Komi для досок <= 13x13")] public float Komi13 = 6.5f;
    [Tooltip("Komi для досок > 13x13")] public float Komi19 = 7.5f;

    [Header("MCTS")]
    [Tooltip("Коэффициент исследования UCT")] public float Exploration = 1.4f;
    [Tooltip("С какого числа визитов узел начинает копить RAVE-статистику")] public int RaveThreshold = 20;
    [Tooltip("Равновесие RAVE: p / (p + n + bias * p * n)")] public float RaveBias = 0.00015f;
    [Tooltip("Масштаб перевода разницы очков в награду [0..1]")] public float ScoreScale = 12f;
    [Tooltip("Вес статического приора хода (убывает с визитами)")] public float PriorWeight = 0.012f;
    [Tooltip("Минимальная доля визитов от лучшего ребёнка при выборе хода")] public float MinVisitsRatio = 0.04f;

    [Header("Rollout")]
    [Tooltip("Дополнительные ходы сверх числа клеток доски")] public int RolloutExtraMoves = 12;
    [Tooltip("Вероятность локального хода вместо случайного по доске")] public float LocalBias = 0.8f;
    [Tooltip("Вероятность сразу захватить группу противника в атари")] public float CaptureProbability = 0.95f;
    [Tooltip("Вероятность спасти свою группу из атари")] public float AtariSaveProbability = 0.98f;
    [Tooltip("Порожок заполненности доски, когда бот начинает рассматривать пас")] public float PassEmptyRatio = 0.12f;

    [Header("Move policy")]
    public bool UseEyeFilter = true;
    public bool UseSelfAtariFilter = true;
    [Tooltip("Радиус локальных кандидатов вокруг камней")] public int CandidateRadius = 2;
    public float CaptureBonus = 40f;
    public float SaveAtariBonus = 35f;
    public float ThreatBonus = 18f;
    public float LocalBonus = 1.5f;
    public float CenterBonus = 3f;
    public float FirstLinePenalty = 9f;
    public float SecondLinePenalty = 2f;
    public float PassPenalty = 12f;

    public float GetKomi(int boardSize) => boardSize switch
    {
        <= 9 => Komi9,
        <= 13 => Komi13,
        _ => Komi19,
    };
}
