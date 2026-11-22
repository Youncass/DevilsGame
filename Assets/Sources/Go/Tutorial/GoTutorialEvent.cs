using System.Collections.Generic;
using UnityEngine;

public enum GoTutorialEventId
{
    FirstStone,
    Connection,
    Atari,
    Capture,
    TwoEyes,
    Territory,
    Ko,
    Endgame,
    GameOver,
}

public readonly struct GoTutorialEvent
{
    public readonly GoTutorialEventId Id;
    public readonly string Title;
    public readonly string Message;
    public readonly PieceColor Color;
    public readonly IReadOnlyList<Vector2Int> Cells;
    public readonly int GameNumber;

    public GoTutorialEvent(
        GoTutorialEventId id, string title, string message,
        PieceColor color, IReadOnlyList<Vector2Int> cells, int gameNumber)
    {
        Id = id;
        Title = title;
        Message = message;
        Color = color;
        Cells = cells;
        GameNumber = gameNumber;
    }

    public override string ToString() => $"[{Id}] {Title}: {Message}";
}
