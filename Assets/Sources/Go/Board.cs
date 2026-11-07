using System;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    [SerializeField] private Transform _startCellPosition;
    [SerializeField, Min(1)] private int _fieldSize;
    [SerializeField] private float _cellsOffset;

    private Stone[] _cells;
    private int _koPoint = -1;
    private int _capturedBlack;   // сколько чёрных камней захвачено белыми
    private int _capturedWhite;   // сколько белых камней захвачено чёрными

    // Кэш буферов — чтобы не аллоцировать на каждый ход.
    private GoLogic.Buffers _buffers;
    private bool[] _scoreVisited;
    private int[] _scoreQueue;
    private float[] _scoreInfluence;

    public int FieldSize => _fieldSize;
    public float CellsOffset => _cellsOffset;
    public Vector3 Origin => _startCellPosition.position;
    public IReadOnlyList<Stone> Cells => _cells;
    public int CapturedBlack => _capturedBlack;
    public int CapturedWhite => _capturedWhite;

    public event Action<Vector2Int, PieceColor> PiecePlaced;
    public event Action<IReadOnlyList<Vector2Int>, PieceColor> PiecesCaptured;

    private void Awake()
    {
        int n = _fieldSize * _fieldSize;
        _cells = new Stone[n];
        _buffers = GoLogic.Buffers.Allocate(_fieldSize);
        _scoreVisited = new bool[n];
        _scoreQueue = new int[n];
        _scoreInfluence = new float[n];
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

    public bool TryPlacePiece(Vector2Int cell, PieceColor color)
    {
        if (!IsInside(cell)) return false;
        int idx = cell.y * _fieldSize + cell.x;
        if (_cells[idx] != Stone.Empty) return false;

        Stone player = color == PieceColor.Black ? Stone.Black : Stone.White;

        if (!GoLogic.IsLegalMove(_cells, _fieldSize, cell.x, cell.y, player, ref _buffers, _koPoint))
            return false;

        int n = _fieldSize * _fieldSize;
        int blackStones = 0, whiteStones = 0;
        for (int i = 0; i < n; i++)
        {
            if (_cells[i] == Stone.Black) blackStones++;
            else if (_cells[i] == Stone.White) whiteStones++;
        }

        // Проверяем, может ли ход захватить: у соседа-противника ровно 1 либерти
        // (наш ход займёт её) ИЛИ 0 либерти (защита от промежуточных состояний).
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;
        bool mightCapture = false;
        for (int d = 0; d < 4; d++)
        {
            int nx = cell.x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = cell.y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= _fieldSize || ny < 0 || ny >= _fieldSize) continue;
            int nIdx = ny * _fieldSize + nx;
            if (_cells[nIdx] != opponent) continue;

            int libs = GoLogic.CountLiberties(_cells, _fieldSize, nx, ny, opponent, ref _buffers, maxCount: 2);
            if (libs <= 1)
            {
                mightCapture = true;
                break;
            }
        }

        int capB = _capturedBlack;
        int capW = _capturedWhite;
        int newKo;

        if (mightCapture)
        {
            // Клон нужен, чтобы знать старые позиции захваченных для события.
            Stone[] snapshot = (Stone[])_cells.Clone();
            Stone[] tempCells = (Stone[])_cells.Clone();
            newKo = GoLogic.ApplyMove(tempCells, _fieldSize, cell.x, cell.y, player,
                ref _buffers, ref capB, ref capW, ref blackStones, ref whiteStones,
                out int capturedCount);

            var capturedPositions = new List<Vector2Int>(capturedCount);
            for (int i = 0; i < n; i++)
            {
                if (snapshot[i] != Stone.Empty && tempCells[i] == Stone.Empty)
                    capturedPositions.Add(new Vector2Int(i % _fieldSize, i / _fieldSize));
            }

            Array.Copy(tempCells, _cells, n);
            _capturedBlack = capB;
            _capturedWhite = capW;
            _koPoint = newKo;

            PiecePlaced?.Invoke(cell, color);
            if (capturedPositions.Count > 0)
            {
                PieceColor capturedColor = color == PieceColor.Black ? PieceColor.White : PieceColor.Black;
                PiecesCaptured?.Invoke(capturedPositions, capturedColor);
            }
        }
        else
        {
            newKo = GoLogic.ApplyMove(_cells, _fieldSize, cell.x, cell.y, player,
                ref _buffers, ref capB, ref capW, ref blackStones, ref whiteStones,
                out _);
            _capturedBlack = capB;
            _capturedWhite = capW;
            _koPoint = newKo;
            PiecePlaced?.Invoke(cell, color);
        }

        return true;
    }

    public ScoreBreakdown ComputeScore()
    {
        int n = _fieldSize * _fieldSize;
        int blackStones = 0, whiteStones = 0;
        for (int i = 0; i < n; i++)
        {
            if (_cells[i] == Stone.Black) blackStones++;
            else if (_cells[i] == Stone.White) whiteStones++;
        }

        return GoScore.ComputeBreakdown(
            _cells, _fieldSize, blackStones, whiteStones,
            _capturedBlack, _capturedWhite,
            _scoreVisited, _scoreQueue, _scoreInfluence);
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
