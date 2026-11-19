using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Одна партия без корутин: фазы, очередь хода, пасы и запуск новой игры.
/// Ввод приходит через QueueClick/QueuePass, поиск бота тикает из Tick().
/// </summary>
public sealed class MatchLoop
{
    public enum Phase
    {
        AwaitingPlayer,
        Thinking,
        GameOver,
    }

    private readonly Board _board;
    private readonly PlayerTurn _player;
    private readonly BotTurn _bot;
    private readonly GameProgress _progress;
    private readonly MatchSettings _settings;

    private Phase _phase;
    private PieceColor _currentTurn;
    private int _consecutivePasses;
    private float _gameOverTimer;
    private bool _restartRequested;

    public MatchLoop(
        Board board,
        PlayerTurn player,
        BotTurn bot,
        GameProgress progress,
        MatchSettings settings)
    {
        _board = board;
        _player = player;
        _bot = bot;
        _progress = progress;
        _settings = settings;
    }

    public Phase CurrentPhase => _phase;

    public event Action<GoGameResult> GameFinished;
    public event Action NewGameStarted;

    public void StartNewGame()
    {
        _board.ResetBoard();
        _currentTurn = _settings.PlayerColor;
        _consecutivePasses = 0;
        _gameOverTimer = 0f;
        _restartRequested = false;
        _player.Clear();

        _progress.BeginGame();
        _phase = Phase.AwaitingPlayer;

        MatchLog.NewGame(
            _progress.CompletedGames + 1,
            _progress.IsTutorial,
            _progress.Level,
            _progress.Simulations);
        NewGameStarted?.Invoke();
    }

    public void Tick(float deltaTime)
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
                TickGameOver(deltaTime);
                break;
        }
    }

    /// <summary>Клик по доске. В GameOver жест запрашивает новую партию.</summary>
    public void QueueClick(Vector2 screenPosition)
    {
        if (_phase == Phase.GameOver)
        {
            _restartRequested = true;
            return;
        }

        if (_phase != Phase.AwaitingPlayer) return;
        _player.QueueClick(screenPosition);
    }

    /// <summary>Жест паса. В GameOver жест запрашивает новую партию.</summary>
    public void QueuePass()
    {
        if (_phase == Phase.GameOver)
        {
            _restartRequested = true;
            return;
        }

        if (_phase != Phase.AwaitingPlayer) return;
        _player.QueuePass();
    }

    private void TickPlayer()
    {
        if (_currentTurn != _settings.PlayerColor)
        {
            BeginThinking();
            return;
        }

        PlayerAction action = _player.Consume();
        switch (action.Kind)
        {
            case PlayerActionKind.Pass:
                RegisterPass("Player");
                break;
            case PlayerActionKind.Play:
                if (_board.TryPlacePiece(action.Cell, _currentTurn, out var captured))
                    ApplyPlacedMove(action.Cell, "Player", captured);
                break;
        }
    }

    private void BeginThinking() => _phase = Phase.Thinking;

    private void TickThinking()
    {
        if (_currentTurn == _settings.PlayerColor)
        {
            _phase = Phase.AwaitingPlayer;
            return;
        }

        if (!_bot.IsSearching)
        {
            float budget = _progress.IsTutorial
                ? _progress.TutorialTimeBudgetMs
                : _settings.BotTimeBudgetMs;

            _bot.Begin(
                _currentTurn,
                _progress.Simulations,
                budget,
                _settings.FrameBudgetMs);
        }

        if (!_bot.TryPoll(_settings.SimulationsPerFrame, out BotMove move))
            return;

        if (!TryApplyBotMove(move))
            RegisterPass("Bot");

        MatchLog.BotMove(move.Search, move.ElapsedMs);
    }

    /// <summary>Ставит ход бота на доску. false — ход недоступен, нужен пас.</summary>
    private bool TryApplyBotMove(BotMove move)
    {
        if (move.IsPass) return false;

        if (_board.TryPlacePiece(move.Cell, _currentTurn, out var captured))
        {
            ApplyPlacedMove(move.Cell, "Bot", captured);
            return true;
        }

        MatchLog.IllegalBotMove(move.Cell);
        if (!_board.TryAnyLegalMove(_currentTurn, out Vector2Int fallback) ||
            !_board.TryPlacePiece(fallback, _currentTurn, out captured))
            return false;

        MatchLog.Fallback(fallback);
        ApplyPlacedMove(fallback, "Bot", captured);
        return true;
    }

    private void ApplyPlacedMove(Vector2Int move, string who, IReadOnlyList<Vector2Int> captured)
    {
        PieceColor mover = _currentTurn;
        _consecutivePasses = 0;
        MatchLog.Move(who, move);

        _progress.OnMove(_board, mover, move, captured ?? Array.Empty<Vector2Int>());
        MoveToOpponent();
    }

    private void RegisterPass(string who)
    {
        _consecutivePasses++;
        MatchLog.Pass(who, _consecutivePasses);
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

        _phase = _currentTurn == _settings.PlayerColor ? Phase.AwaitingPlayer : Phase.Thinking;
    }

    private void TickGameOver(float deltaTime)
    {
        _gameOverTimer += deltaTime;

        if (_restartRequested)
        {
            StartNewGame();
            return;
        }

        if (_settings.AutoStartNewGame && _gameOverTimer >= _settings.NewGameDelaySeconds)
            StartNewGame();
    }

    private void FinishGame()
    {
        ScoreBreakdown score = _board.ComputeScore();
        bool playerWon = score.Diff(ToStone(_settings.PlayerColor)) > 0;

        GoGameResult result = _progress.ReportResult(score, playerWon);
        GameFinished?.Invoke(result);

        MatchLog.Score(score, _settings.PlayerColor, "final");
        MatchLog.GameOver(
            playerWon,
            result.WasTutorial,
            result.LevelBefore,
            result.LevelAfter,
            _progress.Simulations);

        _phase = Phase.GameOver;
        _gameOverTimer = 0f;
    }

    private static PieceColor Opponent(PieceColor c) =>
        c == PieceColor.Black ? PieceColor.White : PieceColor.Black;

    private static Stone ToStone(PieceColor c) =>
        c == PieceColor.Black ? Stone.Black : Stone.White;
}
