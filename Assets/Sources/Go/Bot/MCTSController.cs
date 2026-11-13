using System.Collections.Generic;
using UnityEngine;

public struct MoveSearchResult
{
    public Vector2Int BestMove;
    public float BestWinRate;
    public int LegalMoveCount;
    public int Simulations;
    public List<(Vector2Int move, float winRate, int visits)> AllMoves;
}

public class MCTSController
{
    private readonly int _boardSize;
    private readonly int _boardCells;
    private readonly System.Random _rng = new();
    private const int GraveRefThreshold = 25;
    private const float GraveBias = 1e-4f;

    private readonly List<MCTSNode> _unexpandedScratch = new();
    private readonly List<int> _rolloutMovesScratch = new();

    public MCTSController(int boardSize)
    {
        _boardSize = boardSize;
        _boardCells = boardSize * boardSize;
    }

    public MoveSearchResult Search(
        IReadOnlyList<Stone> currentBoard,
        Stone player,
        int simulations,
        int koPoint,
        MoveTechnique rolloutTechniques,
        MoveTechnique rootTechniques,
        int capturedBlack = 0,
        int capturedWhite = 0)
    {
        int n = _boardCells;
        Stone[] rootCells = new Stone[n];
        for (int i = 0; i < n; i++) rootCells[i] = currentBoard[i];

        var rootState = GameState.Create(rootCells, _boardSize, player);
        rootState.KoPoint = koPoint;
        rootState.CapturedBlack = capturedBlack;
        rootState.CapturedWhite = capturedWhite;

        // Собираем легальные ходы корня.
        var legalMoves = new List<(int x, int y)>();
        for (int y = 0; y < _boardSize; y++)
            for (int x = 0; x < _boardSize; x++)
                if (GoLogic.IsLegalMove(ref rootState, x, y))
                    legalMoves.Add((x, y));

        if (legalMoves.Count == 0)
            return new MoveSearchResult { BestMove = new Vector2Int(-1, -1), LegalMoveCount = 0 };

        if (legalMoves.Count == 1)
        {
            return new MoveSearchResult
            {
                BestMove = new Vector2Int(legalMoves[0].x, legalMoves[0].y),
                LegalMoveCount = 1,
                Simulations = 0,
                AllMoves = new List<(Vector2Int, float, int)>()
            };
        }

        var root = new MCTSNode(null, -1, -1, n)
        {
            ExpandedMoves = new bool[n],
        };

        for (int i = 0; i < simulations; i++)
        {
            // Нет клона! Работаем с rootState напрямую.
            var simState = rootState;
            simState.SaveScalars();

            var (leaf, tref) = SelectAndExpand(root, ref simState, player);

            float score = Rollout(ref simState, leaf, player, _rolloutMovesScratch, rolloutTechniques);

            // Откат всех ходов симуляции.
            simState.UndoLastMove();

            MCTSNode node = leaf;
            while (node != null)
            {
                node.Visits++;
                node.TotalScore += score;

                if (node.MoveX >= 0)
                {
                    int moveIdx = node.MoveY * _boardSize + node.MoveX;
                    node.AmafVisits[moveIdx]++;
                    node.AmafScore[moveIdx] += score;
                }

                node = node.Parent;
            }

            if (tref != null && tref != leaf)
            {
                for (int m = 0; m < _rolloutMovesScratch.Count; m++)
                {
                    int moveIdx = _rolloutMovesScratch[m];
                    tref.AmafVisits[moveIdx]++;
                    tref.AmafScore[moveIdx] += score;
                }
            }
        }

        // === Эвристический бонус за спасение из atari ===
        // Для каждого ребёнка корня проверяем: если у корня была atari-группа,
        // спасает ли этот ход её? Если да — бонус; если нет — штраф.
        // Это даёт MCTS прямой сигнал: «спасай свои группы».

        const int AtariSaveBonus = 50;
        const int AtariIgnorePenalty = 30;

        var rootStateForCheck = rootState;
        bool rootHasAtari = GoLogic.HasAnyGroupInAtari(ref rootStateForCheck, player);

        int saveX = -1, saveY = -1;
        bool hasSaveMove = rootHasAtari && GoLogic.FindAtariEscape(ref rootStateForCheck, out saveX, out saveY);

        // Сначала копируем реальные Visits в EffectiveVisits.
        foreach (var child in root.Children)
            child.EffectiveVisits = child.Visits;

        // Применяем бонусы.
        if (rootHasAtari)
        {
            foreach (var child in root.Children)
            {
                bool isSaveMove = hasSaveMove && child.MoveX == saveX && child.MoveY == saveY;
                if (isSaveMove)
                    child.EffectiveVisits += AtariSaveBonus;
                else
                    child.EffectiveVisits = Mathf.Max(0, child.EffectiveVisits - AtariIgnorePenalty);
            }
        }

        // Выбор best по EffectiveVisits.
        MCTSNode bestChild = null;
        int bestVisits = -1;
        var all = new List<(Vector2Int, float, int)>(root.Children.Count);

        foreach (var child in root.Children)
        {
            float wr = child.Visits > 0 ? child.TotalScore / child.Visits : -999f;
            all.Add((new Vector2Int(child.MoveX, child.MoveY), wr, child.Visits));
            if (child.EffectiveVisits > bestVisits)
            {
                bestVisits = child.EffectiveVisits;
                bestChild = child;
            }
        }

        return new MoveSearchResult
        {
            BestMove = new Vector2Int(bestChild.MoveX, bestChild.MoveY),
            BestWinRate = bestChild.WinRate,
            LegalMoveCount = legalMoves.Count,
            Simulations = simulations,
            AllMoves = all,
        };
    }

    private (MCTSNode leaf, MCTSNode tref) SelectAndExpand(
        MCTSNode root, ref GameState board, Stone rootPlayer)
    {
        MCTSNode node = root;
        MCTSNode tref = root;
        int depth = 0;

        while (true)
        {
            if (node.Visits > GraveRefThreshold)
                tref = node;

            _unexpandedScratch.Clear();
            if (node.ExpandedMoves == null)
                node.ExpandedMoves = new bool[_boardCells];

            for (int y = 0; y < _boardSize; y++)
            {
                for (int x = 0; x < _boardSize; x++)
                {
                    int idx = y * _boardSize + x;
                    if (node.ExpandedMoves[idx]) continue;
                    if (GoLogic.IsLegalMove(ref board, x, y))
                    {
                        var candidate = new MCTSNode(node, x, y, _boardCells);
                        _unexpandedScratch.Add(candidate);
                    }
                }
            }

            if (_unexpandedScratch.Count > 0)
            {
                var chosen = _unexpandedScratch[_rng.Next(_unexpandedScratch.Count)];
                node.Children.Add(chosen);
                node.ExpandedMoves[chosen.MoveY * _boardSize + chosen.MoveX] = true;

                GoLogic.ApplyMove(ref board, chosen.MoveX, chosen.MoveY, out _);

                // SnapshotForNode убран — узел не хранит State.
                return (chosen, tref);
            }

            MCTSNode bestChild = null;
            float bestValue = float.NegativeInfinity;
            // Negamax через чётность глубины, а не через node.State.PlayerToMove.
            bool isOpponentNode = (depth % 2) == 1;

            foreach (var child in node.Children)
            {
                int moveIdx = child.MoveY * _boardSize + child.MoveX;

                float mean = child.Visits > 0 ? child.TotalScore / child.Visits : 0f;

                float amaf = 0f;
                int pAmaf = tref.AmafVisits[moveIdx];
                if (pAmaf > 0) amaf = tref.AmafScore[moveIdx] / pAmaf;

                float beta = 0f;
                if (pAmaf > 0)
                    beta = pAmaf / (pAmaf + child.Visits + GraveBias * pAmaf * child.Visits);

                float value = (1f - beta) * mean + beta * amaf;
                if (isOpponentNode) value = -value;

                if (value > bestValue)
                {
                    bestValue = value;
                    bestChild = child;
                }
            }

            if (bestChild == null && node.Children.Count > 0)
                bestChild = node.Children[0];

            GoLogic.ApplyMove(ref board, bestChild.MoveX, bestChild.MoveY, out _);
            node = bestChild;
            depth++;
        }
    }

    private float Rollout(ref GameState board, MCTSNode leaf, Stone rootPlayer, List<int> movesOut, MoveTechnique techniques)
    {
        movesOut.Clear();

        int n = _boardCells;
        int maxMoves = n + 10;
        int moves = 0;

        bool playerPassed = false;
        bool opponentPassed = false;

        int lastX = leaf.MoveX;
        int lastY = leaf.MoveY;

        while (moves < maxMoves && !(playerPassed && opponentPassed))
        {
            if (GoLogic.SmartRandomMove(ref board, _rng, lastX, lastY, 0f,
                techniques, out int bx, out int by))
            {
                movesOut.Add(by * _boardSize + bx);
                GoLogic.ApplyMove(ref board, bx, by, out _);
                lastX = bx; lastY = by;
                if (board.PlayerToMove == rootPlayer) playerPassed = false;
                else opponentPassed = false;
            }
            else
            {
                board.KoPoint = -1;
                board.PlayerToMove = board.PlayerToMove == Stone.Black ? Stone.White : Stone.Black;
                if (board.PlayerToMove == rootPlayer) playerPassed = true;
                else opponentPassed = true;
            }
            moves++;
        }

        return GoScore.Evaluate(ref board, rootPlayer);
    }
}
