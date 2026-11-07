using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct MoveSearchResult
{
    public Vector2Int BestMove;
    public float BestWinRate;      // на самом деле это score-diff (разница очков)
    public int LegalMoveCount;
    public int Simulations;
    public List<(Vector2Int move, float winRate, int visits)> AllMoves;
}

public class MCTSController
{
    private readonly int _boardSize;
    private readonly int _boardCells;
    private readonly System.Random _rng = new();
    private readonly float[] _influenceBuffer;
    private const float Exploration = 1.414f;

    // GRAVE параметры
    private const int GraveRefThreshold = 25;   // узел с Visits > порога становится tref
    private const float GraveBias = 1e-4f;      // bias в формуле β

    // Переиспользуемые буферы
    private GoLogic.Buffers _buffers;
    private readonly List<MCTSNode> _unexpandedScratch = new();
    private readonly List<int> _rolloutMovesScratch = new();

    public MCTSController(int boardSize)
    {
        _boardSize = boardSize;
        _boardCells = boardSize * boardSize;
        _influenceBuffer = new float[_boardCells];
        _buffers = GoLogic.Buffers.Allocate(boardSize);
    }

    public MoveSearchResult Search(
        IReadOnlyList<Stone> currentBoard,
        Stone player,
        int simulations,
        int capturedBlack = 0,
        int capturedWhite = 0)
    {
        int n = _boardCells;
        Stone[] rootBoard = currentBoard.ToArray();

        // 1. Собираем легальные ходы корня.
        var legalMoves = new List<(int x, int y)>();
        for (int y = 0; y < _boardSize; y++)
            for (int x = 0; x < _boardSize; x++)
                if (GoLogic.IsLegalMove(rootBoard, _boardSize, x, y, player, ref _buffers))
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

        // 2. Считаем камни.
        int blackStones = 0, whiteStones = 0;
        for (int k = 0; k < n; k++)
        {
            if (rootBoard[k] == Stone.Black) blackStones++;
            else if (rootBoard[k] == Stone.White) whiteStones++;
        }

        var root = new MCTSNode(null, -1, -1, n)
        {
            PlayerToMove = player,
            KoPoint = -1,
            LastMoveX = -1,
            LastMoveY = -1,
            CapturedBlack = capturedBlack,
            CapturedWhite = capturedWhite,
            BlackStones = blackStones,
            WhiteStones = whiteStones,
            ExpandedMoves = new bool[n],
        };

        // 3. Основной цикл MCTS.
        for (int i = 0; i < simulations; i++)
        {
            Stone[] simBoard = (Stone[])rootBoard.Clone();

            MCTSNode leaf = SelectAndExpand(root, simBoard, player);

            float score = Rollout(simBoard, leaf, player, _rolloutMovesScratch);

            // Backpropagation с AMAF
            MCTSNode node = leaf;
            while (node != null)
            {
                node.Visits++;
                node.TotalScore += score;

                // Обновляем AMAF по всем ходам симуляции.
                for (int m = 0; m < _rolloutMovesScratch.Count; m++)
                {
                    int moveIdx = _rolloutMovesScratch[m];
                    node.AmafVisits[moveIdx]++;
                    node.AmafScore[moveIdx] += score;
                }

                node = node.Parent;
            }
        }

        // 4. Robust child: лучший ход по Visits.
        MCTSNode bestChild = null;
        int bestVisits = -1;
        var all = new List<(Vector2Int, float, int)>(root.Children.Count);

        foreach (var child in root.Children)
        {
            float wr = child.Visits > 0 ? child.TotalScore / child.Visits : -999f;
            all.Add((new Vector2Int(child.MoveX, child.MoveY), wr, child.Visits));
            if (child.Visits > bestVisits)
            {
                bestVisits = child.Visits;
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

    /// <summary>
    /// Спуск по дереву с GRAVE-выбором.
    /// rootPlayer — цвет корневого игрока (для negamax-инверсии в узлах противника).
    /// </summary>
    private MCTSNode SelectAndExpand(MCTSNode root, Stone[] board, Stone rootPlayer)
    {
        MCTSNode node = root;
        MCTSNode tref = root; // предок, чьи AMAF используем

        while (true)
        {
            // Обновляем tref: если узел достаточно посещён, его AMAF надёжнее.
            if (node.Visits > GraveRefThreshold)
                tref = node;

            // 1. Раскрытие: ищем нераскрытые легальные ходы.
            _unexpandedScratch.Clear();
            if (node.ExpandedMoves == null)
                node.ExpandedMoves = new bool[_boardCells];

            for (int y = 0; y < _boardSize; y++)
            {
                for (int x = 0; x < _boardSize; x++)
                {
                    int idx = y * _boardSize + x;
                    if (node.ExpandedMoves[idx]) continue;
                    if (GoLogic.IsLegalMove(board, _boardSize, x, y, node.PlayerToMove, ref _buffers, node.KoPoint))
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
                ApplyMoveIntoNode(board, chosen);
                return chosen;
            }

            // 2. GRAVE-выбор лучшего ребёнка.
            MCTSNode bestChild = null;
            float bestValue = float.NegativeInfinity;
            bool isOpponentNode = node.PlayerToMove != rootPlayer;

            foreach (var child in node.Children)
            {
                int moveIdx = child.MoveY * _boardSize + child.MoveX;

                float mean = child.Visits > 0 ? child.TotalScore / child.Visits : 0f;

                float amaf = 0f;
                int pAmaf = tref.AmafVisits[moveIdx];
                if (pAmaf > 0)
                    amaf = tref.AmafScore[moveIdx] / pAmaf;

                float beta = 0f;
                if (pAmaf > 0)
                    beta = pAmaf / (pAmaf + child.Visits + GraveBias * pAmaf * child.Visits);

                float value = (1f - beta) * mean + beta * amaf;

                // Negamax: в узле противника инвертируем.
                if (isOpponentNode) value = -value;

                if (value > bestValue)
                {
                    bestValue = value;
                    bestChild = child;
                }
            }

            if (bestChild == null && node.Children.Count > 0)
                bestChild = node.Children[0];

            ApplyMoveIntoNode(board, bestChild);
            node = bestChild;
        }
    }

    /// <summary>
    /// Применяет ход узла к доске, наследуя счётчики из родителя.
    /// </summary>
    private void ApplyMoveIntoNode(Stone[] board, MCTSNode node)
    {
        int capB = node.Parent.CapturedBlack;
        int capW = node.Parent.CapturedWhite;
        int bStones = node.Parent.BlackStones;
        int wStones = node.Parent.WhiteStones;

        int newKo = GoLogic.ApplyMove(
            board, _boardSize, node.MoveX, node.MoveY, node.PlayerToMove,
            ref _buffers,
            ref capB, ref capW, ref bStones, ref wStones,
            out _);

        node.KoPoint = newKo;
        node.LastMoveX = node.MoveX;
        node.LastMoveY = node.MoveY;
        node.CapturedBlack = capB;
        node.CapturedWhite = capW;
        node.BlackStones = bStones;
        node.WhiteStones = wStones;
        node.PlayerToMove = node.PlayerToMove == Stone.Black ? Stone.White : Stone.Black;
    }

    /// <summary>
    /// Rollout с записью всех ходов в movesOut (для AMAF).
    /// </summary>
    private float Rollout(Stone[] board, MCTSNode leaf, Stone rootPlayer, List<int> movesOut)
    {
        movesOut.Clear();

        int n = _boardCells;
        Stone current = leaf.PlayerToMove;
        int koPoint = leaf.KoPoint;
        int lastX = leaf.LastMoveX;
        int lastY = leaf.LastMoveY;

        int capB = leaf.CapturedBlack;
        int capW = leaf.CapturedWhite;
        int blackStones = leaf.BlackStones;
        int whiteStones = leaf.WhiteStones;

        bool playerPassed = false;
        bool opponentPassed = false;
        int maxMoves = n * 2;
        int moves = 0;

        while (moves < maxMoves && !(playerPassed && opponentPassed))
        {
            if (GoLogic.SmartRandomMove(board, _boardSize, current, ref _buffers, _rng,
                                        koPoint, lastX, lastY, 0.4f, out int bx, out int by))
            {
                movesOut.Add(by * _boardSize + bx);

                koPoint = GoLogic.ApplyMove(board, _boardSize, bx, by, current, ref _buffers,
                                            ref capB, ref capW, ref blackStones, ref whiteStones, out _);
                lastX = bx; lastY = by;
                if (current == rootPlayer) playerPassed = false;
                else opponentPassed = false;
            }
            else
            {
                koPoint = -1;
                if (current == rootPlayer) playerPassed = true;
                else opponentPassed = true;
            }

            current = current == Stone.Black ? Stone.White : Stone.Black;
            moves++;
        }

        return GoScore.Evaluate(board, _boardSize, rootPlayer,
                                blackStones, whiteStones, capB, capW,
                                _buffers.Visited, _buffers.Queue, _influenceBuffer);
    }
}
