using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Доска в сцене: где лежат клетки, что стоит на доске и какие события получает вид.
/// Правила и счёт живут в GoBoardState, геометрия — в BoardLayout.
/// </summary>
public sealed class Board : MonoBehaviour
{
    [SerializeField] private Transform _startCellPosition;
    [SerializeField, Range(2, 25)] private int _fieldSize = 9;
    [SerializeField, Min(0.01f)] private float _cellsOffset = 1f;

    private BoardLayout _layout;
    private GoBoardState _state;

    [HideInInspector] public GoConfig Config;

    public int FieldSize => _state != null ? _state.Size : _fieldSize;
    public float CellsOffset => _cellsOffset;
    public Vector3 Origin => StartPosition;
    public IReadOnlyList<Stone> Cells => _state != null ? _state.Cells : Array.Empty<Stone>();
    public int CapturedBlack => _state != null ? _state.CapturedBlack : 0;
    public int CapturedWhite => _state != null ? _state.CapturedWhite : 0;
    public int KoPoint => _state != null ? _state.KoPoint : -1;

    public BoardLayout Layout => EnsureLayout();
    public GoBoardState State => _state;

    public event Action<Vector2Int, PieceColor> PiecePlaced;
    public event Action<IReadOnlyList<Vector2Int>, PieceColor> PiecesCaptured;
    public event Action BoardReset;

    private Vector3 StartPosition =>
        _startCellPosition != null ? _startCellPosition.position : transform.position;

    private void Awake()
    {
        _layout = BuildLayout();
        _state = new GoBoardState(_fieldSize);
    }

    private BoardLayout BuildLayout() => new BoardLayout(StartPosition, _fieldSize, _cellsOffset);

    private BoardLayout EnsureLayout() => _layout ??= BuildLayout();

    public Vector3 CellToWorld(Vector2Int cell) => EnsureLayout().CellToWorld(cell);

    public Vector2Int WorldToCell(Vector3 world) => EnsureLayout().WorldToCell(world);

    public bool IsInside(Vector2Int cell) => EnsureLayout().IsInside(cell);

    public bool ContainsWorldPoint(Vector3 world) => EnsureLayout().ContainsWorldPoint(world);

    /// <summary>
    /// Ставит камень и отдаёт список снятых пленников (null, если захватов не было).
    /// </summary>
    public bool TryPlacePiece(
        Vector2Int cell, PieceColor color, out IReadOnlyList<Vector2Int> captured)
    {
        if (!_state.TryPlay(cell, color, out captured))
            return false;

        PiecePlaced?.Invoke(cell, color);

        if (captured != null && captured.Count > 0)
            PiecesCaptured?.Invoke(captured, GoConvert.Opponent(color));

        return true;
    }

    /// <summary>Первый легальный ход для указанного цвета — аварийный вариант для бота.</summary>
    public bool TryAnyLegalMove(PieceColor color, out Vector2Int cell) =>
        _state.TryAnyLegal(color, out cell);

    /// <summary>Пас снимает запрет ко, как и в правилах позиции.</summary>
    public void Pass() => _state.Pass();

    public ScoreBreakdown ComputeScore() => _state.ComputeScore(Config);

    /// <summary>Полная очистка доски для начала новой партии.</summary>
    public void ResetBoard()
    {
        _state.Reset();
        BoardReset?.Invoke();
    }

    private void OnDrawGizmos()
    {
        if (_startCellPosition == null) return;

        BoardLayout layout = BuildLayout();
        Vector3 origin = layout.Origin;
        float size = (layout.Size - 1) * layout.CellsOffset;

        Gizmos.color = Color.gray;
        for (int i = 0; i < layout.Size; i++)
        {
            float offset = i * layout.CellsOffset;
            Gizmos.DrawLine(new Vector3(origin.x, origin.y, origin.z + offset), new Vector3(origin.x + size, origin.y, origin.z + offset));
            Gizmos.DrawLine(new Vector3(origin.x + offset, origin.y, origin.z), new Vector3(origin.x + offset, origin.y, origin.z + size));
        }

        Gizmos.color = Color.yellow;
        for (int x = 0; x < layout.Size; x++)
            for (int y = 0; y < layout.Size; y++)
                Gizmos.DrawSphere(layout.CellToWorld(new Vector2Int(x, y)), layout.CellsOffset * 0.05f);
    }
}
