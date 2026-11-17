using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управление партией без корутин: один простой state machine в Update().
/// Поиск бота всё ещё режется по кадрам через MCTSController.Step().
/// </summary>
public class GameSystem : MonoBehaviour
{
    private enum Phase
    {
        AwaitingPlayer,
        Thinking,
        GameOver,
    }

    [Header("References")]
    [SerializeField] private Board _board;
    [SerializeField] private PlayerInput _input;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private GoConfig _botConfig;
    [SerializeField] private PieceColor _playerColor = PieceColor.White;

    [Header("Search budgets")]
    [SerializeField, Min(1)] private float _botTimeBudgetMs = 1500f;
    [Tooltip("Сколько миллисекунд поиска можно съесть в одном кадре")]
    [SerializeField, Min(0.25f)] private float _frameBudgetMs = 4f;
    [SerializeField, Min(1)] private int _simsPerFrame = 128;

    [Header("Progression")]
    [SerializeField] private GoDifficultyProfile _difficulty = new();

    [Header("Between games")]
    [SerializeField] private bool _autoStartNewGame = true;
    [SerializeField, Min(0)] private float _newGameDelaySeconds = 2f;

    private BoardRaycaster _raycaster;
    private MCTSController _mcts;
    private GoSkillTracker _skill;
    private GoTutorialTriggers _triggers;
    private System.Random _rng = new();
    private readonly System.Diagnostics.Stopwatch _searchStopwatch = new();

    private Phase _phase;
    private PieceColor _currentTurn;
    private int _consecutivePasses;
    private bool _searchStarted;
    private float _gameOverTimer;

    private bool _clickQueued;
    private Vector2 _queuedClick;
    private bool _passQueued;
    private IReadOnlyList<Vector2Int> _lastCaptured;

    public event Action<GoTutorialEvent> TutorialEventRaised;
    public event Action<GoGameResult> GameFinished;
    public event Action<GoSkillSnapshot> SkillChanged;
    public event Action NewGameStarted;

    private int DifficultyLevel => _skill.IsTutorial ? 0 : _skill.Level;

    private static PieceColor Opponent(PieceColor c) =>
        c == PieceColor.Black ? PieceColor.White : PieceColor.Black;

    private static Stone ToStone(PieceColor c) =>
        c == PieceColor.Black ? Stone.Black : Stone.White;

    private void Awake()
    {
        if (_botConfig == null)
        {
            _botConfig = ScriptableObject.CreateInstance<GoConfig>();
            _botConfig.name = "Runtime GoConfig";
        }
        _difficulty ??= new GoDifficultyProfile();

        _raycaster = new BoardRaycaster(_mainCamera, _board);
        _mcts = new MCTSController(_board.FieldSize, _botConfig);
        _board.Config = _botConfig;

        _skill = new GoSkillTracker();
        _skill.Load();
        _skill.Changed += s => SkillChanged?.Invoke(s);

        _triggers = new GoTutorialTriggers(_difficulty);
        _triggers.Raised += OnTutorialEvent;

        _input.OnClick.AddListener(OnClick);
        _input.OnPass.AddListener(OnPass);
        _board.PiecesCaptured += OnPiecesCaptured;
    }

    private void Start() => StartNewGame();

    private void OnDestroy()
    {
        if (_input != null)
        {
            _input.OnClick.RemoveListener(OnClick);
            _input.OnPass.RemoveListener(OnPass);
        }
        if (_board != null) _board.PiecesCaptured -= OnPiecesCaptured;
        if (_triggers != null) _triggers.Raised -= OnTutorialEvent;
    }

    private void Update()
    {
        switch (_phase)
        {
            case Phase.AwaitingPlayer:
                TickPlayer();
                break;
            case Phase.Thinking:
                TickThinking();
                break;
            case Phase.GameOver:
                TickGameOver();
                break;
        }
    }

    private void StartNewGame()
    {
        _board.ResetBoard();
        _currentTurn = _playerColor;
        _consecutivePasses = 0;
        _searchStarted = false;
        _gameOverTimer = 0f;
        _clickQueued = false;
        _passQueued = false;
        _lastCaptured = null;

        _triggers.BeginGame(_skill.CompletedGames);
        _phase = Phase.AwaitingPlayer;

        Debug.Log($"[Go] New game #{_skill.CompletedGames + 1} " +
                  $"tutorial={_skill.IsTutorial} level={_skill.Level} " +
                  $"sims={_difficulty.GetSimulations(DifficultyLevel)}");
        NewGameStarted?.Invoke();
    }

    private void TickPlayer()
    {
        if (_currentTurn != _playerColor)
        {
            BeginThinking();
            return;
        }

        if (_passQueued)
        {
            _passQueued = false;
            _clickQueued = false;
            RegisterPass("Player");
            return;
        }

        if (!_clickQueued) return;
        _clickQueued = false;

        if (!_raycaster.TryGetCell(_queuedClick, out Vector2Int cell))
            return;

        if (!_board.TryPlacePiece(cell, _currentTurn))
            return;

        ApplyPlacedMove(cell, "Player");
    }

    private void BeginThinking()
    {
        _phase = Phase.Thinking;
        _searchStarted = false;
    }

    private void TickThinking()
    {
        if (_currentTurn == _playerColor)
        {
            _phase = Phase.AwaitingPlayer;
            return;
        }

        Stone botColor = _currentTurn == PieceColor.Black ? Stone.Black : Stone.White;

        if (!_searchStarted)
        {
            _searchStopwatch.Restart();
            float budget = _skill.IsTutorial
                ? _difficulty.TutorialTimeBudgetMs
                : _botTimeBudgetMs;

            _mcts.Begin(
                _board.Cells,
                botColor,
                _difficulty.GetSimulations(DifficultyLevel),
                _board.KoPoint,
                _board.CapturedBlack,
                _board.CapturedWhite,
                budget,
                _frameBudgetMs);
            _searchStarted = true;
        }

        if (!_mcts.Step(_simsPerFrame))
            return;

        MoveSearchResult search = _mcts.Result;
        _searchStopwatch.Stop();
        Vector2Int move = ChooseBotMove(search);

        if (move.x < 0)
        {
            RegisterPass("Bot");
            LogBotMove(search, _searchStopwatch.ElapsedMilliseconds);
            return;
        }

        _lastCaptured = null;
        if (!_board.TryPlacePiece(move, _currentTurn))
        {
            Debug.LogError($"[Go] Bot returned illegal move {move}. Fallback to any legal move.");
            if (!TryAnyLegalMove(out Vector2Int fallback) ||
                !_board.TryPlacePiece(fallback, _currentTurn))
            {
                RegisterPass("Bot");
                LogBotMove(search, _searchStopwatch.ElapsedMilliseconds);
                return;
            }
            move = fallback;
            Debug.Log($"[Go] Fallback move={fallback}");
        }

        ApplyPlacedMove(move, "Bot");
        LogBotMove(search, _searchStopwatch.ElapsedMilliseconds);
    }

    private Vector2Int ChooseBotMove(MoveSearchResult result)
    {
        if (result.BestMove.x < 0) return result.BestMove;

        float blunderChance = _difficulty.GetBlunderChance(DifficultyLevel);
        if (_rng.NextDouble() >= blunderChance) return result.BestMove;

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

    private void ApplyPlacedMove(Vector2Int move, string who)
    {
        PieceColor mover = _currentTurn;
        _consecutivePasses = 0;
        Debug.Log($"[{who}] move={move}");

        _triggers.OnMove(_board, mover, move, _lastCaptured ?? Array.Empty<Vector2Int>());
        MoveToOpponent();
    }

    private void RegisterPass(string who)
    {
        _consecutivePasses++;
        Debug.Log($"[{who}] pass ({_consecutivePasses}/2)");
        MoveToOpponent();
    }

    private void MoveToOpponent()
    {
        _currentTurn = Opponent(_currentTurn);
        if (_consecutivePasses >= 2)
        {
            FinishGame();
            return;
        }

        _phase = _currentTurn == _playerColor ? Phase.AwaitingPlayer : Phase.Thinking;
        if (_phase == Phase.Thinking) _searchStarted = false;
    }

    private void TickGameOver()
    {
        _gameOverTimer += Time.deltaTime;

        if (_clickQueued || _passQueued)
        {
            _clickQueued = false;
            _passQueued = false;
            StartNewGame();
            return;
        }

        if (_autoStartNewGame && _gameOverTimer >= _newGameDelaySeconds)
            StartNewGame();
    }

    private void FinishGame()
    {
        ScoreBreakdown score = _board.ComputeScore();
        bool playerWon = score.Diff(ToStone(_playerColor)) > 0;
        bool wasTutorial = _skill.IsTutorial;
        int levelBefore = _skill.Level;

        if (wasTutorial) _skill.ReportTutorialResult(playerWon);
        else _skill.ReportResult(playerWon);

        _triggers.OnGameOver(_board, score, playerWon);

        var result = new GoGameResult(
            score, playerWon, wasTutorial, levelBefore, _skill.Level, _skill.CompletedGames);
        GameFinished?.Invoke(result);

        LogScore("final");
        Debug.Log($"[Go] Game over (two passes). " +
                  $"playerWon={playerWon} tutorial={wasTutorial} " +
                  $"level {levelBefore} -> {_skill.Level}; next sims=" +
                  $"{_difficulty.GetSimulations(DifficultyLevel)}");
        _phase = Phase.GameOver;
        _gameOverTimer = 0f;
    }

    private bool TryAnyLegalMove(out Vector2Int move)
    {
        Stone player = _currentTurn == PieceColor.Black ? Stone.Black : Stone.White;
        int n = _board.FieldSize * _board.FieldSize;

        var cells = new Stone[n];
        for (int i = 0; i < n; i++) cells[i] = _board.Cells[i];

        var position = GoPosition.Create(cells, _board.FieldSize, player);
        position.KoPoint = _board.KoPoint;
        position.CapturedBlack = _board.CapturedBlack;
        position.CapturedWhite = _board.CapturedWhite;

        for (int y = 0; y < _board.FieldSize; y++)
        {
            for (int x = 0; x < _board.FieldSize; x++)
            {
                if (position.IsLegal(y * _board.FieldSize + x))
                {
                    move = new Vector2Int(x, y);
                    return true;
                }
            }
        }
        move = new Vector2Int(-1, -1);
        return false;
    }

    private void OnClick(Vector2 screenPosition)
    {
        if (_phase != Phase.AwaitingPlayer && _phase != Phase.GameOver) return;
        _queuedClick = screenPosition;
        _clickQueued = true;
    }

    private void OnPass()
    {
        if (_phase != Phase.AwaitingPlayer && _phase != Phase.GameOver) return;
        _passQueued = true;
    }

    private void OnPiecesCaptured(IReadOnlyList<Vector2Int> positions, PieceColor color) =>
        _lastCaptured = positions;

    private void OnTutorialEvent(GoTutorialEvent e)
    {
        Debug.Log($"[Tutorial #{e.GameNumber}] {e.Title}: {e.Message}");
        TutorialEventRaised?.Invoke(e);
    }

    private void LogScore(string label)
    {
        ScoreBreakdown b = _board.ComputeScore();
        Debug.Log(
            $"[Score:{label}] {b}\n" +
            $"        diff({_playerColor})={b.Diff(ToStone(_playerColor)):+0.0;-0.0;0.0}");
    }

    private void LogBotMove(MoveSearchResult r, long ms)
    {
        float second = 0f, worst = 0f;
        int secondIdx = -1, worstIdx = -1;

        var visitedMoves = r.AllMoves.FindAll(m => m.visits >= 10);

        for (int i = 0; i < visitedMoves.Count; i++)
        {
            var m = visitedMoves[i];
            if (m.move == r.BestMove) continue;

            if (secondIdx < 0 || m.winRate > second) { second = m.winRate; secondIdx = i; }
            if (worstIdx < 0 || m.winRate < worst) { worst = m.winRate; worstIdx = i; }
        }

        Debug.Log(
            $"[Bot] move={r.BestMove} score={r.BestWinRate:+0.00;-0.00;0.00} " +
            $"sims={r.Simulations} totalLegal={r.LegalMoveCount} time={ms}ms\n" +
            $"      2nd={(secondIdx >= 0 ? $"{visitedMoves[secondIdx].move} wr={second:+0.00;-0.00;0.00}" : "—")} " +
            $"worst={(worstIdx >= 0 ? $"{visitedMoves[worstIdx].move} wr={worst:+0.00;-0.00;0.00}" : "—")}");
    }
}
