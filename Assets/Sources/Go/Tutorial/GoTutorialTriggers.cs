using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Распознаёт узнаваемые ситуации на доске и выдаёт обучающие подсказки.
/// По умолчанию активно только в обучении и первой ранговой партии.
/// </summary>
public sealed class GoTutorialTriggers
{
    private readonly GoDifficultyProfile _profile;
    private readonly HashSet<GoTutorialEventId> _fired = new();

    private int _gameNumber;
    private int _moveCount;
    private bool _enabled;

    public event Action<GoTutorialEvent> Raised;
    public IReadOnlyCollection<GoTutorialEventId> Fired => _fired;

    public GoTutorialTriggers(GoDifficultyProfile profile)
    {
        _profile = profile ?? new GoDifficultyProfile();
    }

    public void BeginGame(int completedGames)
    {
        _gameNumber = completedGames + 1;
        _enabled = _profile.ShouldRaiseTriggers(completedGames);
        _fired.Clear();
        _moveCount = 0;
    }

    public void OnMove(Board board, PieceColor mover, Vector2Int move, IReadOnlyList<Vector2Int> captured)
    {
        // Подсказки нужны только в первых партиях: не копируем доску, если они выключены.
        if (!_enabled || board == null) return;

        int size = board.FieldSize;
        OnMove(board.Cells, size, board.KoPoint, board.Config, mover, move, captured);
    }

    /// <summary>Тестовый/универсальный вход без MonoBehaviour-доски.</summary>
    public void OnMove(IReadOnlyList<Stone> cells, int size, int koPoint, GoConfig config,
        PieceColor mover, Vector2Int move, IReadOnlyList<Vector2Int> captured)
    {
        if (!_enabled || cells == null) return;
        _moveCount++;

        var pos = GoPosition.Create(cells, size, Stone.Empty);
        pos.KoPoint = koPoint;

        if (_moveCount == 1)
            Fire(GoTutorialEventId.FirstStone, "Первый камень",
                "Камни захватывают территорию, когда соединяются с соседними. Пустые точки рядом с вашей цепочкой постепенно становятся вашими.",
                mover, new[] { move });

        int n = pos.Count;
        var group = new int[n];
        var liberties = new int[n];
        var visited = new bool[n];

        bool atariFired = _fired.Contains(GoTutorialEventId.Atari);
        bool eyesFired = _fired.Contains(GoTutorialEventId.TwoEyes);
        bool connectionFired = _fired.Contains(GoTutorialEventId.Connection);

        for (int i = 0; i < n && (!atariFired || !eyesFired || !connectionFired); i++)
        {
            if (visited[i] || pos.Cells[i] == Stone.Empty) continue;
            pos.GetGroupInfo(i, group, liberties, out int groupCount, out int libertyCount);
            for (int k = 0; k < groupCount; k++) visited[group[k]] = true;

            PieceColor color = pos.Cells[i] == Stone.Black ? PieceColor.Black : PieceColor.White;
            var highlight = new List<Vector2Int>(Math.Min(groupCount, 12));
            AddCells(highlight, group, groupCount, size);

            if (!connectionFired && groupCount >= 2)
            {
                connectionFired = Fire(GoTutorialEventId.Connection, "Соединение",
                    $"{Name(color)} соединили {groupCount} камня. Камни одной группы делят свободы — их нельзя захватить по отдельности.",
                    color, highlight);
            }

            if (!atariFired && libertyCount == 1)
            {
                var cellsToHighlight = new List<Vector2Int>(groupCount + 1);
                AddCells(cellsToHighlight, group, groupCount, size);
                cellsToHighlight.Add(IndexToCell(liberties[0], size));
                atariFired = Fire(GoTutorialEventId.Atari, "Группа в атари",
                    $"У этой группы {Name(color)} осталась одна свобода. Ход в неё захватит всю группу.",
                    color, cellsToHighlight);
            }

            if (!eyesFired && groupCount >= 3 &&
                GoStoneEvaluator.TryFindTwoEyes(ref pos, i, out int eyeA, out int eyeB))
            {
                var cellsToHighlight = new List<Vector2Int>(groupCount + 2);
                AddCells(cellsToHighlight, group, groupCount, size);
                cellsToHighlight.Add(IndexToCell(eyeA, size));
                cellsToHighlight.Add(IndexToCell(eyeB, size));
                eyesFired = Fire(GoTutorialEventId.TwoEyes, "Два глаза",
                    $"У группы {Name(color)} два глаза. Пока оба глаза свободны, противник не может её захватить.",
                    color, cellsToHighlight);
            }
        }

        if (!_fired.Contains(GoTutorialEventId.Capture) && captured != null && captured.Count > 0)
        {
            Fire(GoTutorialEventId.Capture, "Захват",
                $"Камни {Name(mover == PieceColor.Black ? PieceColor.White : PieceColor.Black)} без свобод снимаются с доски и становятся пленниками.",
                mover, captured);
        }

        if (!_fired.Contains(GoTutorialEventId.Territory) && pos.EmptyCount < n * 0.8f)
        {
            int minRegion = Math.Max(4, n / 12);
            var regionCells = new int[n];
            Stone[] colors = { Stone.White, Stone.Black };
            for (int c = 0; c < colors.Length; c++)
            {
                int regionSize = GoStoneEvaluator.FindLargestTerritory(
                    ref pos, colors[c], minRegion, regionCells);
                if (regionSize <= 0 || regionSize > n / 2) continue;

                var cellsToHighlight = new List<Vector2Int>(Math.Min(regionSize, 12));
                for (int k = 0; k < cellsToHighlight.Capacity; k++)
                    cellsToHighlight.Add(IndexToCell(regionCells[k], size));

                Fire(GoTutorialEventId.Territory, "Территория",
                    $"Пустой район из {regionSize} пунктов окружён камнями только одного цвета. " +
                    "В конце партии такие пункты считаются чьей-то территорией.",
                    colors[c] == Stone.White ? PieceColor.White : PieceColor.Black, cellsToHighlight);
                break;
            }
        }

        if (!_fired.Contains(GoTutorialEventId.Ko) && pos.KoPoint >= 0)
        {
            Fire(GoTutorialEventId.Ko, "Ко",
                "Нельзя сразу взять один камень и тут же взять его обратно — это повтор позиции. Сделайте ход в другом месте.",
                mover, new[] { IndexToCell(pos.KoPoint, size) });
        }

        float passRatio = config != null ? config.PassEmptyRatio : 0.12f;
        if (!_fired.Contains(GoTutorialEventId.Endgame) && pos.EmptyCount <= n * passRatio)
        {
            Fire(GoTutorialEventId.Endgame, "Финал партии",
                "Свободных пунктов почти не осталось. Дальше важнее всего посчитать территорию и не играть внутри своих глаз.",
                mover, Array.Empty<Vector2Int>());
        }
    }

    public void OnGameOver(Board board, ScoreBreakdown score, bool playerWon)
    {
        if (board == null) return;
        OnGameOver(score, playerWon);
    }

    /// <summary>Тестовый/универсальный вход без MonoBehaviour-доски.</summary>
    public void OnGameOver(ScoreBreakdown score, bool playerWon)
    {
        if (!_enabled) return;
        float diff = score.Diff(Stone.White);
        Fire(GoTutorialEventId.GameOver, "Партия окончена",
            $"Счёт по площади: белые {score.WhiteTotal:0} против чёрных {score.BlackTotal:0} " +
            $"(разница для белых {diff:+0.0;-0.0;0.0}). {(playerWon ? "Вы победили!" : "На этот раз победил бот.")} " +
            "После короткой паузы начнётся новая партия.",
            PieceColor.White, Array.Empty<Vector2Int>());
    }

    private bool Fire(GoTutorialEventId id, string title, string message,
        PieceColor color, IReadOnlyList<Vector2Int> cells)
    {
        if (_fired.Contains(id)) return false;
        _fired.Add(id);
        Raised?.Invoke(new GoTutorialEvent(id, title, message, color, cells, _gameNumber));
        return true;
    }

    private static void AddCells(List<Vector2Int> output, int[] indices, int count, int size)
    {
        for (int i = 0; i < count; i++) output.Add(IndexToCell(indices[i], size));
    }

    private static Vector2Int IndexToCell(int idx, int size) =>
        new Vector2Int(idx % size, idx / size);

    private static string Name(PieceColor color) => color == PieceColor.Black ? "чёрных" : "белых";
}
