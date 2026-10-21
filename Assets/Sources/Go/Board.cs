using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Sources;

public class Board : MonoBehaviour
{
    public int FieldSize => _fieldSize;

    //public UnityEvent<Vector2Int, Vector3, PieceColor> PiecePlaced { get; private set; } = new();

    [SerializeField] private GameObject _whitePiecePrefab;
    [SerializeField] private GameObject _blackPiecePrefab;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private PlayerInput _input;
    [SerializeField] private Transform _startCellPosition;
    [SerializeField, Min(1)] private int _fieldSize;
    [SerializeField] private float _cellsOffset;

    public IEnumerator PerformMove(PieceColor color)
    {
        yield return WaitForPlayersMove(color);
    }

    private IEnumerator WaitForPlayersMove(PieceColor color)
    {
        Plane plane = new(inNormal: Vector2.up,
                          d: -_startCellPosition.position.y);

        Vector2 position = default;
        _input.OnClick.AddListener(RegisterClick);
        yield return new WaitWhile(() => position == default);
        _input.OnClick.RemoveListener(RegisterClick);

        Ray ray = _mainCamera.ScreenPointToRay(position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 enterPoint = ray.GetPoint(enter);
            if (IsWithinBoard(enterPoint))
            {
                enterPoint = SnapToBoard(enterPoint);

                GameObject prefab = color == PieceColor.White ? _whitePiecePrefab : _blackPiecePrefab;
                Instantiate(prefab, enterPoint, Quaternion.identity, transform);
            }
        }


        void RegisterClick(Vector2 p) => position = p;
    }

    private bool IsWithinBoard(Vector3 position)
    {
        Vector3 scp = _startCellPosition.position;
        return position.x > scp.x - _cellsOffset &&
               position.x < scp.x + (_fieldSize - 0.5f) * _cellsOffset &&
               position.z > scp.z - _cellsOffset &&
               position.z < scp.z + (_fieldSize - 0.5f) * _cellsOffset;
    }

    private Vector3 SnapToBoard(Vector3 position)
    {
        position.x = (position.x - _startCellPosition.position.x).Snap(_cellsOffset) + _startCellPosition.position.x;
        position.y = _startCellPosition.position.y;
        position.z = (position.z - _startCellPosition.position.z).Snap(_cellsOffset) + _startCellPosition.position.z;
        return position;
    }

    private void OnDrawGizmos()
    {
        Vector3 scp = _startCellPosition.position;

        for (int i = 0; i < _fieldSize; i++)
        {
            Gizmos.DrawLine(
                from: new(scp.x, scp.y, scp.z + i * _cellsOffset),
                to: new(scp.x + (_fieldSize - 1) * _cellsOffset, scp.y, scp.z + i * _cellsOffset));

            Gizmos.DrawLine(
                from: new(scp.x + i * _cellsOffset, scp.y, scp.z),
                to: new(scp.x + i * _cellsOffset, scp.y, scp.z + (_fieldSize - 1) * _cellsOffset));
        }
    }
}
