using UnityEngine;

/// <summary>
/// Раскладка доски в мире: где стоит первая клетка, шаг сетки и попадание точки в доску.
/// Отдельный класс, чтобы Board не смешивал геометрию сцены с правилами.
/// </summary>
public sealed class BoardLayout
{
    public BoardLayout(Vector3 origin, int size, float cellsOffset)
    {
        Origin = origin;
        Size = size;
        CellsOffset = cellsOffset;
    }

    public Vector3 Origin { get; }
    public int Size { get; }
    public float CellsOffset { get; }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(
            Origin.x + cell.x * CellsOffset,
            Origin.y,
            Origin.z + cell.y * CellsOffset);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        int x = Mathf.RoundToInt((world.x - Origin.x) / CellsOffset);
        int y = Mathf.RoundToInt((world.z - Origin.z) / CellsOffset);
        return new Vector2Int(x, y);
    }

    public bool IsInside(Vector2Int cell) =>
        cell.x >= 0 && cell.x < Size &&
        cell.y >= 0 && cell.y < Size;

    public bool ContainsWorldPoint(Vector3 world)
    {
        float half = CellsOffset * 0.5f;
        return world.x >= Origin.x - half &&
               world.x <= Origin.x + (Size - 1) * CellsOffset + half &&
               world.z >= Origin.z - half &&
               world.z <= Origin.z + (Size - 1) * CellsOffset + half;
    }
}
