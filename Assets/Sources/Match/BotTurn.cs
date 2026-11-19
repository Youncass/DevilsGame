using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>Готовый ход бота вместе с результатами поиска для лога.</summary>
public readonly struct BotMove
{
    public readonly Vector2Int Cell; // (-1,-1) = пас
    public readonly MoveSearchResult Search;
    public readonly long ElapsedMs;

    public BotMove(Vector2Int cell, MoveSearchResult search, long elapsedMs)
    {
        Cell = cell;
        Search = search;
        ElapsedMs = elapsedMs;
    }

    public bool IsPass => Cell.x < 0;
}

/// <summary>
/// Ведёт поиск MCTS по кадрам и выбирает ход с учётом сложности.
/// Доску не меняет — установку камня и fallback выполняет MatchLoop.
/// </summary>
public sealed class BotTurn
{
    private readonly Board _board;
    private readonly GameProgress _progress;
    private readonly MCTSController _mcts;
    private readonly System.Random _rng = new();
    private readonly Stopwatch _stopwatch = new();

    private bool _searching;

    public BotTurn(Board board, GoConfig config, GameProgress progress)
    {
        _board = board;
        _progress = progress;
        _mcts = new MCTSController(board.FieldSize, config);
    }

    public bool IsSearching => _searching;

    public void Begin(PieceColor side, int simulations, float timeBudgetMs, float frameBudgetMs)
    {
        _searching = true;
        _stopwatch.Restart();

        _mcts.Begin(
            _board.Cells,
            ToStone(side),
            simulations,
            _board.KoPoint,
            _board.CapturedBlack,
            _board.CapturedWhite,
            timeBudgetMs,
            frameBudgetMs);
    }

    /// <summary>Один шаг поиска. false — бот ещё думает.</summary>
    public bool TryPoll(int simulationsPerFrame, out BotMove move)
    {
        move = default;
        if (!_searching) return false;
        if (!_mcts.Step(simulationsPerFrame)) return false;

        _searching = false;
        _stopwatch.Stop();

        MoveSearchResult result = _mcts.Result;
        move = new BotMove(ChooseMove(result), result, _stopwatch.ElapsedMilliseconds);
        return true;
    }

    private Vector2Int ChooseMove(MoveSearchResult result)
    {
        if (result.BestMove.x < 0) return result.BestMove;
        if (_rng.NextDouble() >= _progress.BlunderChance) return result.BestMove;

        // Нарочно слабый ход: худший из реально посещённых вариантов.
        Vector2Int worst = result.BestMove;
        float worstWinRate = float.MaxValue;
        for (int i = 0; i < result.AllMoves.Count; i++)
        {
            var candidate = result.AllMoves[i];
            if (candidate.visits < 2 || candidate.move.x < 0) continue;
            if (candidate.winRate < worstWinRate)
            {
                worstWinRate = candidate.winRate;
                worst = candidate.move;
            }
        }
        return worst;
    }

    private static Stone ToStone(PieceColor c) =>
        c == PieceColor.Black ? Stone.Black : Stone.White;
}
