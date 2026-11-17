using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public struct MoveSearchResult
{
    public Vector2Int BestMove;      // (-1,-1) = пас
    public float BestWinRate;
    public int LegalMoveCount;
    public int Simulations;
    public List<(Vector2Int move, float winRate, int visits)> AllMoves;
}

/// <summary>
/// RAVE-статистика, разделённая по цвету. Общая для чёрных и белых статистика
/// (как в старом коде) даёт неверные приоритеты: один и тот же пункт в разных
/// симуляциях играют разные игроки.
/// </summary>
internal sealed class GoAmaf
{
    private readonly int[] _visitsBlack;
    private readonly int[] _visitsWhite;
    private readonly float[] _rewardBlack;
    private readonly float[] _rewardWhite;

    public GoAmaf(int count)
    {
        _visitsBlack = new int[count];
        _visitsWhite = new int[count];
        _rewardBlack = new float[count];
        _rewardWhite = new float[count];
    }

    public void Add(Stone player, int move, float reward)
    {
        if (player == Stone.Black)
        {
            _visitsBlack[move]++;
            _rewardBlack[move] += reward;
        }
        else
        {
            _visitsWhite[move]++;
            _rewardWhite[move] += reward;
        }
    }

    public bool TryGet(Stone player, int move, out int visits, out float mean)
    {
        if (player == Stone.Black)
        {
            visits = _visitsBlack[move];
            mean = visits > 0 ? _rewardBlack[move] / visits : 0f;
        }
        else
        {
            visits = _visitsWhite[move];
            mean = visits > 0 ? _rewardWhite[move] / visits : 0f;
        }
        return visits > 0;
    }
}

internal sealed class GoNode
{
    public GoNode[] Children = new GoNode[4];
    public int ChildCount;

    public readonly int Move;         // индекс пункта; Count = пас
    public readonly Stone PlayerToMove;
    public readonly float Prior;

    public int Visits;
    public float TotalReward;         // награда [0..1] с точки зрения корневого игрока
    public float TotalScore;          // сырая разница очков с точки зрения корневого игрока

    public int[] Untried;
    public float[] UntriedPrior;
    public int UntriedCount;

    public GoAmaf Amaf;

    public GoNode(int move, Stone playerToMove, float prior)
    {
        Move = move;
        PlayerToMove = playerToMove;
        Prior = prior;
    }
}

/// <summary>
/// MCTS с UCT + RAVE. Поиск можно разбивать по кадрам: Begin -> Step(batch) ...
/// Синхронный Search() оставлен для тестов и вызовов вне main loop.
/// </summary>
public class MCTSController
{
    private readonly int _size;
    private readonly int _count;
    private readonly GoConfig _config;
    private readonly GoGeometry _geometry;
    private readonly GoMoveGenerator _generator;

    private Stone[] _rootCells;
    private GoPosition _root;
    private Stone _rootPlayer;
    private GoNode _rootNode;
    private GoNode _tref;

    private int[] _movesScratch;
    private float[] _priorsScratch;

    private GoNode[] _path;
    private int _pathLength;

    private GoNode[] _recordNode;
    private int[] _recordMove;
    private Stone[] _recordPlayer;
    private int _recordCount;

    private int[] _rolloutMove;
    private Stone[] _rolloutPlayer;
    private int _rolloutCount;

    private GoRng _rng;
    private int _maxSimulations;
    private int _simulationsDone;
    private long _elapsedTicks;
    private long _frameTicks;
    private long _totalTicks;
    private bool _done;
    private MoveSearchResult _result;
    private int _legalRootMoves;

    public MCTSController(int boardSize, GoConfig config)
    {
        _size = boardSize;
        _count = boardSize * boardSize;
        _config = config;
        _geometry = GoGeometry.Get(boardSize);
        _generator = new GoMoveGenerator(_count);

        _movesScratch = new int[_count + 1];
        _priorsScratch = new float[_count + 1];
        _path = new GoNode[2 * _count + 16];
        _recordNode = new GoNode[2 * _count + 16];
        _recordMove = new int[2 * _count + 16];
        _recordPlayer = new Stone[2 * _count + 16];
        int rolloutCapacity = _count + Math.Max(64, config.RolloutExtraMoves + 8);
        _rolloutMove = new int[rolloutCapacity];
        _rolloutPlayer = new Stone[rolloutCapacity];
    }

    public bool IsDone => _done;
    public int SimulationsDone => _simulationsDone;

    public MoveSearchResult Result => _done ? _result : BuildResult();

    public MoveSearchResult Search(
        IReadOnlyList<Stone> currentBoard, Stone player, int simulations,
        int koPoint, int capturedBlack = 0, int capturedWhite = 0)
    {
        Begin(currentBoard, player, simulations, koPoint, capturedBlack, capturedWhite);
        while (!Step(int.MaxValue)) { }
        return _result;
    }

    public void Begin(
        IReadOnlyList<Stone> board, Stone player, int simulations,
        int koPoint, int capturedBlack = 0, int capturedWhite = 0,
        float timeBudgetMs = 0f, float frameBudgetMs = 4f)
    {
        _rootCells = new Stone[_count];
        uint hash = 2166136261u;
        for (int i = 0; i < _count; i++)
        {
            _rootCells[i] = board[i];
            hash = (hash ^ (byte)board[i]) * 16777619u;
        }
        hash ^= (uint)player * 2654435761u;
        hash ^= (uint)(koPoint + 1) * 2246822519u;
        _rng = new GoRng(hash);

        _rootPlayer = player;
        _root = GoPosition.Create(_rootCells, _size, player, _geometry);
        _root.KoPoint = koPoint;
        _root.CapturedBlack = capturedBlack;
        _root.CapturedWhite = capturedWhite;

        _rootNode = new GoNode(-1, player, 0f);
        GenerateMoves(_rootNode, ref _root);
        _legalRootMoves = Math.Max(0, _rootNode.UntriedCount - 1); // минус пас
        _rootNode.Amaf = new GoAmaf(_count);

        _maxSimulations = Math.Max(0, simulations);
        _simulationsDone = 0;
        _elapsedTicks = 0;
        _frameTicks = Math.Max(1, (long)(TimeSpan.TicksPerMillisecond * Math.Max(0.25f, frameBudgetMs)));
        _totalTicks = timeBudgetMs > 0f
            ? Math.Max(1, (long)(TimeSpan.TicksPerMillisecond * timeBudgetMs))
            : long.MaxValue;
        _done = _maxSimulations == 0 || _legalRootMoves == 0;
        _result = _done ? BuildResult() : default;
    }

    /// <summary>Выполняет до batch симуляций, но не дольше frame/time-бюджета.</summary>
    public bool Step(int batch)
    {
        if (_done) return true;

        var sw = Stopwatch.StartNew();
        int remaining = _maxSimulations - _simulationsDone;
        int target = batch >= remaining
            ? _maxSimulations
            : _simulationsDone + Math.Max(1, batch);

        while (_simulationsDone < target)
        {
            RunSimulation();
            _simulationsDone++;

            if ((_simulationsDone & 7) == 0)
            {
                long elapsed = _elapsedTicks + sw.ElapsedTicks;
                if (elapsed >= _totalTicks || sw.ElapsedTicks >= _frameTicks)
                    break;
            }
        }

        _elapsedTicks += sw.ElapsedTicks;

        if (_simulationsDone >= _maxSimulations || _elapsedTicks >= _totalTicks)
        {
            _done = true;
            _result = BuildResult();
        }
        return _done;
    }

    private void GenerateMoves(GoNode node, ref GoPosition pos)
    {
        int count = _generator.Generate(ref pos, pos.LastMove, _rootPlayer, _config,
            _movesScratch, _priorsScratch);

        node.Untried = new int[count];
        node.UntriedPrior = new float[count];
        int dst = 0;
        for (int i = count - 1; i >= 0; i--) // генератор сортирует по возрастанию
        {
            node.Untried[dst] = _movesScratch[i];
            node.UntriedPrior[dst] = _priorsScratch[i];
            dst++;
        }
        node.UntriedCount = count;
    }

    private void RunSimulation()
    {
        GoPosition sim = _root;       // массивы общие, Undo() всё вернёт
        sim.BeginSimulation();

        _tref = _rootNode;
        _recordCount = 0;
        _rolloutCount = 0;
        _pathLength = 0;
        _path[_pathLength++] = _rootNode;

        GoNode leaf = SelectAndExpand(ref sim);

        float score = Rollout(ref sim, leaf.Move);
        float reward = RewardFromScore(score);

        for (int i = 0; i < _pathLength; i++)
        {
            GoNode node = _path[i];
            node.Visits++;
            node.TotalReward += reward;
            node.TotalScore += score;
        }

        if (_tref.Amaf == null) _tref.Amaf = new GoAmaf(_count);

        for (int i = 0; i < _recordCount; i++)
        {
            int move = _recordMove[i];
            if (move >= _count) continue;
            Stone player = _recordPlayer[i];
            _tref.Amaf.Add(player, move, reward);
            if (_recordNode[i].Amaf != null)
                _recordNode[i].Amaf.Add(player, move, reward);
        }
        for (int i = 0; i < _rolloutCount; i++)
        {
            int move = _rolloutMove[i];
            if (move >= _count) continue;
            _tref.Amaf.Add(_rolloutPlayer[i], move, reward);
        }

        sim.Undo();
    }

    private GoNode SelectAndExpand(ref GoPosition sim)
    {
        GoNode node = _rootNode;

        while (true)
        {
            if (node.Visits >= _config.RaveThreshold && node.Amaf == null)
                node.Amaf = new GoAmaf(_count);
            if (node.Amaf != null)
                _tref = node;

            if (node.UntriedCount > 0)
            {
                int pick = PickUntried(node);
                int move = node.Untried[pick];
                float prior = node.UntriedPrior[pick];

                int last = node.UntriedCount - 1;
                node.Untried[pick] = node.Untried[last];
                node.UntriedPrior[pick] = node.UntriedPrior[last];
                node.UntriedCount--;

                var child = new GoNode(move, node.PlayerToMove, prior);
                AddChild(node, child);
                PlayTreeMove(ref sim, node, move);
                _path[_pathLength++] = child;
                return child;
            }

            if (node.ChildCount == 0)
                return node; // тупик: пасовали оба, дальше rollout не нужен

            GoNode best = SelectChild(node);
            PlayTreeMove(ref sim, node, best.Move);
            _path[_pathLength++] = best;
            node = best;
        }
    }

    private void AddChild(GoNode parent, GoNode child)
    {
        if (parent.ChildCount == parent.Children.Length)
            Array.Resize(ref parent.Children, parent.Children.Length * 2);
        parent.Children[parent.ChildCount++] = child;
    }

    private void PlayTreeMove(ref GoPosition sim, GoNode node, int move)
    {
        Stone player = node.PlayerToMove;
        if (move >= _count) sim.Pass();
        else sim.Play(move, out _);

        _recordNode[_recordCount] = node;
        _recordMove[_recordCount] = move;
        _recordPlayer[_recordCount] = player;
        _recordCount++;
    }

    private int PickUntried(GoNode node)
    {
        int k = Math.Min(4, node.UntriedCount);
        int best = k - 1;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < k; i++)
        {
            float s = node.UntriedPrior[i] + _rng.NextFloat() * 6f;
            if (s > bestScore) { bestScore = s; best = i; }
        }
        return best;
    }

    private GoNode SelectChild(GoNode node)
    {
        double logN = Math.Log(node.Visits + 1.0);
        GoAmaf amaf = node.Amaf ?? _tref.Amaf;
        Stone mover = node.PlayerToMove;

        GoNode best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < node.ChildCount; i++)
        {
            GoNode child = node.Children[i];
            float mean = child.Visits > 0 ? child.TotalReward / child.Visits : 0.5f;
            float value = mean;

            if (amaf != null && child.Move < _count &&
                amaf.TryGet(mover, child.Move, out int p, out float amafMean))
            {
                float beta = p / (p + child.Visits + _config.RaveBias * p * child.Visits);
                value = (1f - beta) * mean + beta * amafMean;
            }

            // Все награды хранятся с точки зрения корневого игрока. На узлах
            // противника минимизируем эту величину.
            if (mover != _rootPlayer) value = -value;

            float explore = _config.Exploration *
                (float)Math.Sqrt(logN / (child.Visits + 1));
            float prior = child.Prior * _config.PriorWeight / (child.Visits + 1);
            float score = value + explore + prior;

            if (score > bestScore) { bestScore = score; best = child; }
        }
        return best;
    }

    private float Rollout(ref GoPosition sim, int leafMove)
    {
        bool rootPassed = false;
        bool opponentPassed = false;
        int maxMoves = _count + Math.Max(0, _config.RolloutExtraMoves);
        int last = leafMove < _count ? leafMove : -1;

        for (int moveNumber = 0;
             moveNumber < maxMoves && !(rootPassed && opponentPassed) &&
             _rolloutCount < _rolloutMove.Length;
             moveNumber++)
        {
            int move = _generator.PickRolloutMove(ref sim, last, ref _rng, _config);
            Stone player = sim.SideToMove;

            _rolloutMove[_rolloutCount] = move;
            _rolloutPlayer[_rolloutCount] = player;
            _rolloutCount++;

            if (move >= _count)
            {
                sim.Pass();
                if (player == _rootPlayer) rootPassed = true;
                else opponentPassed = true;
                last = -1;
            }
            else
            {
                sim.Play(move, out _);
                rootPassed = false;
                opponentPassed = false;
                last = move;
            }
        }

        return GoScoring.Evaluate(ref sim, _rootPlayer, _config, removeDead: true);
    }

    private float RewardFromScore(float score)
    {
        float scale = Math.Max(1f, _config.ScoreScale);
        // System.Math.Tanh есть и в Unity, и в обычном .NET.
        return 0.5f + 0.5f * (float)Math.Tanh(score / scale);
    }

    private MoveSearchResult BuildResult()
    {
        var all = new List<(Vector2Int, float, int)>(Math.Max(0, _rootNode?.ChildCount ?? 0));
        if (_rootNode == null)
            return new MoveSearchResult { BestMove = new Vector2Int(-1, -1), AllMoves = all };

        GoNode best = null;
        float bestValue = float.NegativeInfinity;
        int minVisits = Math.Max(1, (int)(_simulationsDone * _config.MinVisitsRatio));

        for (int i = 0; i < _rootNode.ChildCount; i++)
        {
            GoNode child = _rootNode.Children[i];
            float reward = child.Visits > 0 ? child.TotalReward / child.Visits : 0f;
            float score = child.Visits > 0 ? child.TotalScore / child.Visits : 0f;
            var cell = child.Move < _count
                ? new Vector2Int(child.Move % _size, child.Move / _size)
                : new Vector2Int(-1, -1);
            all.Add((cell, reward, child.Visits));

            if (child.Visits < minVisits) continue;
            float value = reward + score / (Math.Max(1f, _config.ScoreScale) * 100f);
            if (value > bestValue) { bestValue = value; best = child; }
        }

        if (best == null)
        {
            for (int i = 0; i < _rootNode.ChildCount; i++)
                if (best == null || _rootNode.Children[i].Visits > best.Visits)
                    best = _rootNode.Children[i];
        }

        Vector2Int bestMove = best == null || best.Move >= _count
            ? new Vector2Int(-1, -1)
            : new Vector2Int(best.Move % _size, best.Move / _size);

        return new MoveSearchResult
        {
            BestMove = bestMove,
            BestWinRate = best != null && best.Visits > 0 ? best.TotalReward / best.Visits : 0f,
            LegalMoveCount = _legalRootMoves,
            Simulations = _simulationsDone,
            AllMoves = all,
        };
    }
}
