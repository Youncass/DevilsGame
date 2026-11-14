using System.Collections.Generic;
using UnityEngine;
using PrimeTween;

public class BoardView : MonoBehaviour
{
    [SerializeField] private Board _board;
    [SerializeField] private GameObject _whitePiecePrefab;
    [SerializeField] private GameObject _blackPiecePrefab;

    [Space(5)]
    [SerializeField, Min(0)] private float _pieceFallHeight;
    [SerializeField, Min(0)] private float _pieceFallTime;
    [SerializeField] private Ease _pieceFallEase;

    [Space(5)]
    [SerializeField, Min(0)] private float _captureTime;
    [SerializeField] private Ease _captureEase;

    private readonly Dictionary<Vector2Int, GameObject> _spawnedPieces = new();

    private void Start()
    {
        _board.PiecePlaced += OnPiecePlaced;
        _board.PiecesCaptured += OnPiecesCaptured;
    }

    private void OnPiecePlaced(Vector2Int coordinates, PieceColor color)
    {
        GameObject prefab = color == PieceColor.White ? _whitePiecePrefab : _blackPiecePrefab;
        Vector3 position = _board.CellToWorld(coordinates);

        GameObject piece = Instantiate(prefab, position + Vector3.up * _pieceFallHeight, Quaternion.identity, transform);
        _spawnedPieces[coordinates] = piece;

        Tween.PositionY(piece.transform, position.y, _pieceFallTime, Easing.Standard(_pieceFallEase));
    }

    private void OnPiecesCaptured(IReadOnlyList<Vector2Int> positions, PieceColor color)
    {
        foreach (var pos in positions)
        {
            if (_spawnedPieces.TryGetValue(pos, out GameObject piece))
            {
                _spawnedPieces.Remove(pos);

                // Анимация исчезновения: уменьшаем масштаб до нуля
                Tween.Scale(piece.transform, Vector3.zero, _captureTime, Easing.Standard(_captureEase))
                     .OnComplete(() => Destroy(piece));
            }
        }
    }

    private void OnDestroy()
    {
        if (_board != null)
        {
            _board.PiecePlaced -= OnPiecePlaced;
            _board.PiecesCaptured -= OnPiecesCaptured;
        }
    }
}
