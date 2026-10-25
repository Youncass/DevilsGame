using PrimeTween;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    [SerializeField] private Board _board;
    [SerializeField] private GameObject _whitePiecePrefab;
    [SerializeField] private GameObject _blackPiecePrefab;

    [Space(5)]
    [SerializeField, Min(0)] private float _pieceFallHeight;
    [SerializeField, Min(0)] private float _pieceFallTime;
    [SerializeField] private Ease _pieceFallEase;

    private void Start()
    {
        _board.PiecePlaced += OnPiecePlaced;
    }

    private void OnPiecePlaced(Vector2Int coordinates, PieceColor color)
    {
        GameObject prefab = color == PieceColor.White
            ? _whitePiecePrefab
            : _blackPiecePrefab;

        Vector3 position = _board.CellToWorld(coordinates);
        GameObject piece = Instantiate(prefab, position + Vector3.up * _pieceFallHeight, Quaternion.identity, transform);
        Tween.PositionY(piece.transform, position.y, _pieceFallTime, Easing.Standard(_pieceFallEase));
    }

    private void OnDestroy()
    {
        _board.PiecePlaced -= OnPiecePlaced;
    }
}
