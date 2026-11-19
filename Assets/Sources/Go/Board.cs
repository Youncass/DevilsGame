using System;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    [SerializeField] private Transform _startCellPosition;
    [SerializeField, Min(2)] private int _fieldSize;
    [SerializeField] private float _cellsOffset;

    private Stone[] _cells;
    private GoGeometry _geometry;
    private int _koPoint = -1;
    private int _capturedBlack;
    private int _capturedWhite;

    [HideInInspector] public GoConfig Config;

    public int FieldSize => _fieldSize;
    public float CellsOffset => _cellsOffset;
    public Vector3 Origin => _startCellPosition.position;
    public IReadOnlyList<Stone> Cells => _cells;
    public int CapturedBlack => _capturedBlack;
    public int CapturedWhite => _capturedWhite;
    public int KoPoint => _koPoint;

    public event Action<Vector2Int, PieceColor> PiecePlaced;
    public event Action<IReadOnlyList<Vector2Int>, PieceColor> PiecesCaptured;
    public event Action BoardReset;

    private void Awake()
    {
        _cells = new Stone[_fieldSize * _fieldSize];
        _geometry = GoGeometry.Get(_fieldSize);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        Vector3 origin = Origin;
        return new Vector3(
            origin.x + cell.x * _cellsOffset,
            origin.y,
            origin.z + cell.y * _cellsOffset);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 origin = Origin;
        int x = Mathf.RoundToInt((world.x - origin.x) / _cellsOffset);
        int y = Mathf.RoundToInt((world.z - origin.z) / _cellsOffset);
        return new Vector2Int(x, y);
    }

    public bool IsInside(Vector2Int cell) =>
        cell.x >= 0 && cell.x < _fieldSize &&
        cell.y >= 0 && cell.y < _fieldSize;

    public bool ContainsWorldPoint(Vector3 world)
    {
        Vector3 origin = Origin;
        float half = _cellsOffset * 0.5f;
        return world.x >= origin.x - half &&
               world.x <= origin.x + (_fieldSize - 1) * _cellsOffset + half &&
               world.z >= origin.z - half &&
               world.z <= origin.z + (_fieldSize - 1) * _cellsOffset + half;
    }

    public bool TryPlacePiece(Vector2Int cell, PieceColor color) =>
        TryPlacePiece(cell, color, out _);

    /// <summary>
    /// Ставит камень и отдаёт список снятых пленников (null, если захватов не было).
    /// </summary>
    public bool TryPlacePiece(
        Vector2Int cell, PieceColor color, out IReadOnlyList<Vector2Int> captured)
    {
        captured = null;
        if (!IsInside(cell)) return false;

        Stone player = color == PieceColor.Black ? Stone.Black : Stone.White;
        var position = GoPosition.Create(_cells, _fieldSize, player, _geometry);
        position.KoPoint = _koPoint;
        position.CapturedBlack = _capturedBlack;
        position.CapturedWhite = _capturedWhite;

        if (!position.Play(cell.y * _fieldSize + cell.x, out _))
            return false;

        Array.Copy(position.Cells, _cells, _cells.Length);
        _koPoint = position.KoPoint;
        _capturedBlack = position.CapturedBlack;
        _capturedWhite = position.CapturedWhite;

        PiecePlaced?.Invoke(cell, color);

        if (position.CapturedCount > 0)
        {
            var capturedCells = new List<Vector2Int>(position.CapturedCount);
            for (int i = 0; i < position.CapturedCount; i++)
            {
                int idx = position.CapturedIndices[i];
                capturedCells.Add(new Vector2Int(idx % _fieldSize, idx / _fieldSize));
            }
            captured = capturedCells;
            PieceColor capturedColor = color == PieceColor.Black ? PieceColor.White : PieceColor.Black;
            PiecesCaptured?.Invoke(capturedCells, capturedColor);
        }

        return true;
    }

    /// <summary>Первый легальный ход для указанного цвета — аварийный вариант для бота.</summary>
    public bool TryAnyLegalMove(PieceColor color, out Vector2Int cell)
    {
        Stone player = color == PieceColor.Black ? Stone.Black : Stone.White;
        var position = GoPosition.Create(_cells, _fieldSize, player, _geometry);
        position.KoPoint = _koPoint;
        position.CapturedBlack = _capturedBlack;
        position.CapturedWhite = _capturedWhite;

        int count = _fieldSize * _fieldSize;
        for (int i = 0; i < count; i++)
        {
            if (!position.IsLegal(i)) continue;
            cell = new Vector2Int(i % _fieldSize, i / _fieldSize);
            return true;
        }

        cell = new Vector2Int(-1, -1);
        return false;
    }

    public ScoreBreakdown ComputeScore()
    {
        var position = GoPosition.Create(_cells, _fieldSize, Stone.Empty, _geometry);
        position.KoPoint = _koPoint;
        position.CapturedBlack = _capturedBlack;
        position.CapturedWhite = _capturedWhite;
        return GoScoring.ComputeBreakdown(ref position, Config, removeDead: false);
    }

    /// <summary>Полная очистка доски для начала новой партии.</summary>
    public void ResetBoard()
    {
        Array.Clear(_cells, 0, _cells.Length);
        _koPoint = -1;
        _capturedBlack = 0;
        _capturedWhite = 0;
        BoardReset?.Invoke();
    }

    private void OnDrawGizmos()
    {
        if (_startCellPosition == null) return;
        Vector3 origin = Origin;
        float size = (_fieldSize - 1) * _cellsOffset;
        Gizmos.color = Color.gray;
        for (int i = 0; i < _fieldSize; i++)
        {
            float offset = i * _cellsOffset;
            Gizmos.DrawLine(new Vector3(origin.x, origin.y, origin.z + offset), new Vector3(origin.x + size, origin.y, origin.z + offset));
            Gizmos.DrawLine(new Vector3(origin.x + offset, origin.y, origin.z), new Vector3(origin.x + offset, origin.y, origin.z + size));
        }
        Gizmos.color = Color.yellow;
        for (int x = 0; x < _fieldSize; x++)
            for (int y = 0; y < _fieldSize; y++)
                Gizmos.DrawSphere(CellToWorld(new Vector2Int(x, y)), _cellsOffset * 0.05f);
    }
}
