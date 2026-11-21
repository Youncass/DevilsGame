using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Состояние партии на доске: камни, ko и счётчики захватов.
/// Правила применяются через GoPosition, поэтому всякая работа со снимком позиции
/// идёт через CreatePosition/Commit, а не размазана по вызывающему коду.
/// </summary>
public sealed class GoBoardState
{
    private readonly int _size;
    private readonly GoGeometry _geometry;
    private readonly Stone[] _cells;

    private int _koPoint = -1;
    private int _capturedBlack;
    private int _capturedWhite;

    public GoBoardState(int size)
    {
        _geometry = GoGeometry.Get(size);
        _size = _geometry.Size;
        _cells = new Stone[_geometry.Count];
    }

    public int Size => _size;
    public IReadOnlyList<Stone> Cells => _cells;
    public int KoPoint => _koPoint;
    public int CapturedBlack => _capturedBlack;
    public int CapturedWhite => _capturedWhite;

    /// <summary>Полная очистка доски для начала новой партии.</summary>
    public void Reset()
    {
        Array.Clear(_cells, 0, _cells.Length);
        _koPoint = -1;
        _capturedBlack = 0;
        _capturedWhite = 0;
    }

    public bool IsInside(Vector2Int cell) =>
        cell.x >= 0 && cell.x < _size &&
        cell.y >= 0 && cell.y < _size;

    /// <summary>
    /// Ставит камень и отдаёт список снятых пленников (null, если захватов не было).
    /// </summary>
    public bool TryPlay(Vector2Int cell, PieceColor color, out IReadOnlyList<Vector2Int> captured)
    {
        captured = null;
        if (!IsInside(cell)) return false;

        GoPosition position = CreatePosition(GoConvert.ToStone(color));
        if (!position.Play(ToIndex(cell), out _)) return false;

        Commit(position);

        if (position.CapturedCount > 0)
            captured = CollectCaptured(position);

        return true;
    }

    /// <summary>Первый легальный ход для указанного цвета — аварийный вариант для бота.</summary>
    public bool TryAnyLegal(PieceColor color, out Vector2Int cell)
    {
        GoPosition position = CreatePosition(GoConvert.ToStone(color));

        for (int i = 0; i < _cells.Length; i++)
        {
            if (!position.IsLegal(i)) continue;

            cell = ToCell(i);
            return true;
        }

        cell = new Vector2Int(-1, -1);
        return false;
    }

    public ScoreBreakdown ComputeScore(GoConfig config)
    {
        GoPosition position = CreatePosition(Stone.Empty);
        return GoScoring.ComputeBreakdown(ref position, config, removeDead: false);
    }

    private GoPosition CreatePosition(Stone sideToMove)
    {
        GoPosition position = GoPosition.Create(_cells, _size, sideToMove, _geometry);
        position.KoPoint = _koPoint;
        position.CapturedBlack = _capturedBlack;
        position.CapturedWhite = _capturedWhite;
        return position;
    }

    private void Commit(GoPosition position)
    {
        Array.Copy(position.Cells, _cells, _cells.Length);
        _koPoint = position.KoPoint;
        _capturedBlack = position.CapturedBlack;
        _capturedWhite = position.CapturedWhite;
    }

    private IReadOnlyList<Vector2Int> CollectCaptured(GoPosition position)
    {
        var capturedCells = new List<Vector2Int>(position.CapturedCount);
        for (int i = 0; i < position.CapturedCount; i++)
            capturedCells.Add(ToCell(position.CapturedIndices[i]));

        return capturedCells;
    }

    private int ToIndex(Vector2Int cell) => cell.y * _size + cell.x;

    private Vector2Int ToCell(int index) => new Vector2Int(index % _size, index / _size);
}
