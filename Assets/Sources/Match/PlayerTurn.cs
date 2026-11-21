using UnityEngine;

public enum PlayerActionKind
{
    None,
    Play,
    Pass,
}

/// <summary>Одно действие игрока, готовое к применению на доске.</summary>
public readonly struct PlayerAction
{
    public readonly PlayerActionKind Kind;
    public readonly Vector2Int Cell;

    private PlayerAction(PlayerActionKind kind, Vector2Int cell)
    {
        Kind = kind;
        Cell = cell;
    }

    public static PlayerAction None() => new PlayerAction(PlayerActionKind.None, default);
    public static PlayerAction Pass() => new PlayerAction(PlayerActionKind.Pass, default);
    public static PlayerAction Play(Vector2Int cell) => new PlayerAction(PlayerActionKind.Play, cell);
}

/// <summary>
/// Очередь ввода игрока: клик и пас превращаются в действие на доске.
/// Доску не меняет — установку камня выполняет MatchLoop.
/// </summary>
public sealed class PlayerTurn
{
    private readonly BoardRaycaster _raycaster;

    private bool _clickQueued;
    private Vector2 _queuedClick;
    private bool _passQueued;

    public PlayerTurn(BoardRaycaster raycaster)
    {
        _raycaster = raycaster;
    }

    public void QueueClick(Vector2 screenPosition)
    {
        _queuedClick = screenPosition;
        _clickQueued = true;
    }

    public void QueuePass()
    {
        _passQueued = true;
    }

    public void Clear()
    {
        _clickQueued = false;
        _passQueued = false;
    }

    /// <summary>Забирает отложенный ввод. Пас имеет приоритет над кликом.</summary>
    public PlayerAction Consume()
    {
        if (_passQueued)
        {
            Clear();
            return PlayerAction.Pass();
        }

        if (!_clickQueued) return PlayerAction.None();
        _clickQueued = false;

        return _raycaster.TryGetCell(_queuedClick, out Vector2Int cell)
            ? PlayerAction.Play(cell)
            : PlayerAction.None();
    }
}
