using System;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    [SerializeField] private Transform _startCellPosition;
    [SerializeField, Min(1)] private int _fieldSize;
    [SerializeField] private float _cellsOffset;

    private readonly Dictionary<Vector2Int, PieceColor> _cells = new();

    public int FieldSize => _fieldSize;
    public float CellsOffset => _cellsOffset;
    public Vector3 Origin => _startCellPosition.position;

    public event Action<Vector2Int, PieceColor> PiecePlaced;

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

    public bool TryPlacePiece(Vector2Int cell, PieceColor color)
    {
        if (IsInside(cell) == false || _cells.ContainsKey(cell))
            return false;

        _cells.Add(cell, color);
        PiecePlaced?.Invoke(cell, color);
        return true;
    }

    private void OnDrawGizmos()
    {
        if (_startCellPosition == null)
            return;

        Vector3 origin = Origin;
        float size = (_fieldSize - 1) * _cellsOffset;

        Gizmos.color = Color.gray;
        for (int i = 0; i < _fieldSize; i++)
        {
            float offset = i * _cellsOffset;
            Gizmos.DrawLine(
                new Vector3(origin.x, origin.y, origin.z + offset),
                new Vector3(origin.x + size, origin.y, origin.z + offset));
            Gizmos.DrawLine(
                new Vector3(origin.x + offset, origin.y, origin.z),
                new Vector3(origin.x + offset, origin.y, origin.z + size));
        }

        Gizmos.color = Color.yellow;
        for (int x = 0; x < _fieldSize; x++)
            for (int y = 0; y < _fieldSize; y++)
                Gizmos.DrawSphere(CellToWorld(new Vector2Int(x, y)), _cellsOffset * 0.05f);
    }
}
