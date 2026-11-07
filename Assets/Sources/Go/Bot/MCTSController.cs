using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct MoveSearchResult
{
    public Vector2Int BestMove;
    public float BestScore;
    public int LegalMoveCount;
    public int Simulations;
    public List<(Vector2Int move, float winRate, int visits)> AllMoves;
}

public class MCTSController
{
    private readonly int _boardSize;
    private readonly System.Random _rng = new();
    private readonly float[] _influenceBuffer;
    private const float Exploration = 1.414f;

    // Переиспользуемые буферы — чтобы не аллоцировать в горячем пути.
    private GoLogic.Buffers _buffers;
    private readonly List<MCTSNode> _unexpandedScratch = new();

    public MCTSController(int boardSize)
    {
        _boardSize = boardSize;
        _influenceBuffer = new float[boardSize * boardSize];
        _buffers = GoLogic.Buffers.Allocate(boardSize);
    }

    public MoveSearchResult Search(
        IReadOnlyList<Stone> currentBoard, Stone player, int simulations,
        int capturedBlack = 0, int capturedWhite = 0)
    {
        int n = _boardSize * _boardSize;
        Stone[] rootBoard = currentBoard.ToArray();

        // 1. Собираем легальные ходы корня (для подсчёта LegalMoveCount и early-out).
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

        // 2. Считаем камни и захваты на корне (захваты из Board не передаются — считаем 0,
        //    но камни считаем точно).
        int blackStones = 0, whiteStones = 0;
        for (int k = 0; k < n; k++)
        {
            if (rootBoard[k] == Stone.Black) blackStones++;
            else if (rootBoard[k] == Stone.White) whiteStones++;
        }

        var root = new MCTSNode(null, -1, -1)
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

            // Selection + Expansion: спускаемся от корня, применяя ходы, и раскрываем один узел.
            // Возвращает узел, из которого надо начинать rollout, и состояние в нём.
            MCTSNode leaf = SelectAndExpand(root, simBoard);

            // Rollout: играем из состояния leaf.
            float score = Rollout(simBoard, leaf, player);

            // Backpropagation.
            MCTSNode node = leaf;
            while (node != null)
            {
                node.Visits++;
                node.TotalScore += score;
                node = node.Parent;
            }
        }

        // 4. Robust child: лучший ход по числу посещений (стабильнее, чем WinRate).
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
            BestScore = bestChild.WinRate,
            LegalMoveCount = legalMoves.Count,
            Simulations = simulations,
            AllMoves = all,
        };
    }

    /// <summary>
    /// Спуск по дереву с применением ходов на доске.
    /// Если в узле есть нераскрытые легальные ходы — раскрывает один случайный и возвращает его.
    /// Иначе идёт в лучшего ребёнка по UCB1 и продолжает спуск.
    /// </summary>
    private MCTSNode SelectAndExpand(MCTSNode root, Stone[] board)
    {
        MCTSNode node = root;

        while (true)
        {
            // 1. Проверяем, есть ли нераскрытые ходы из этого узла.
            _unexpandedScratch.Clear();

            // Ленивая инициализация ExpandedMoves (для узлов, созданных до этого кода).
            if (node.ExpandedMoves == null)
                node.ExpandedMoves = new bool[_boardSize * _boardSize];

            for (int y = 0; y < _boardSize; y++)
            {
                for (int x = 0; x < _boardSize; x++)
                {
                    int idx = y * _boardSize + x;
                    if (node.ExpandedMoves[idx]) continue;

                    if (GoLogic.IsLegalMove(board, _boardSize, x, y, node.PlayerToMove, ref _buffers, node.KoPoint))
                    {
                        // Создаём «кандидата» — но не добавляем в Children, пока не выберем.
                        var candidate = new MCTSNode(node, x, y);
                        _unexpandedScratch.Add(candidate);
                    }
                }
            }

            if (_unexpandedScratch.Count > 0)
            {
                var chosen = _unexpandedScratch[_rng.Next(_unexpandedScratch.Count)];
                node.Children.Add(chosen);
                node.ExpandedMoves[chosen.MoveY * _boardSize + chosen.MoveX] = true;

                // Применяем ход на доске и фиксируем состояние в узле.
                ApplyMoveIntoNode(board, chosen);

                return chosen;
            }

            // 2. Все ходы раскрыты — идём в лучшего ребёнка по UCB1.
            MCTSNode bestChild = null;
            float bestUcb = float.NegativeInfinity;
            bool isOpponentNode = node.PlayerToMove != root.PlayerToMove;

            float logN = Mathf.Log(Mathf.Max(1, node.Visits));

            foreach (var child in node.Children)
            {
                if (child.Visits == 0)
                {
                    // Неисследованный ребёнок — берём его (это ускоряет равномерное покрытие).
                    bestChild = child;
                    break;
                }

                float q = child.TotalScore / child.Visits;
                // Negamax: оценка TotalScore — с точки зрения корневого игрока.
                // В узле противника мы хотим её МИНИМИЗИРОВАТЬ, поэтому инвертируем.
                if (isOpponentNode) q = -q;

                float explorationTerm = Exploration * Mathf.Sqrt(logN / child.Visits);
                float ucb = q + explorationTerm;

                if (ucb > bestUcb)
                {
                    bestUcb = ucb;
                    bestChild = child;
                }
            }

            // Применяем ход выбранного ребёнка на доске.
            ApplyMoveIntoNode(board, bestChild);
            node = bestChild;
        }
    }

    /// <summary>
    /// Применяет ход, который привёл в узел, к доске.
    /// ВАЖНО: этот метод вызывается ровно один раз для каждого узла при спуске,
    /// поэтому счётчики камней/захватов в узле должны быть уже корректны (из родителя).
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

    private float Rollout(Stone[] board, MCTSNode leaf, Stone rootPlayer)
    {
        int n = _boardSize * _boardSize;
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
