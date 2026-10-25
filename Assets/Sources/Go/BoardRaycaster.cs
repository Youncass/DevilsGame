using UnityEngine;

public class BoardRaycaster
{
    private readonly Camera _camera;
    private readonly Board _board;
    private readonly Plane _plane;

    public BoardRaycaster(Camera camera, Board board)
    {
        _camera = camera;
        _board = board;
        _plane = new Plane(Vector3.up, board.Origin);
    }

    public bool TryGetCell(Vector2 screenPosition, out Vector2Int cell)
    {
        cell = default;

        Ray ray = _camera.ScreenPointToRay(screenPosition);
        if (_plane.Raycast(ray, out float enter) == false)
            return false;

        Vector3 world = ray.GetPoint(enter);
        if (_board.ContainsWorldPoint(world) == false)
            return false;

        cell = _board.WorldToCell(world);
        return _board.IsInside(cell);
    }
}
