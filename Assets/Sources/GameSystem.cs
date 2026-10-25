using System.Collections;
using UnityEngine;

public class GameSystem : MonoBehaviour
{
    [SerializeField] private Board _board;
    [SerializeField] private PlayerInput _input;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private PieceColor _currentTurn = PieceColor.White;

    private PlayerMoveProvider _playerMoves;
    private BoardRaycaster _raycaster;

    private void Awake()
    {
        _playerMoves = new PlayerMoveProvider(_input, _raycaster);
        _raycaster = new BoardRaycaster(_mainCamera, _board);
    }

    private void Start() => StartCoroutine(GameLoop());

    private IEnumerator GameLoop()
    {
        while (true)
        {
            Vector2Int? move = null;
            yield return _playerMoves.WaitForMove(m => move = m);

            if (move.HasValue == false || _board.TryPlacePiece(move.Value, _currentTurn) == false)
                continue;

            _currentTurn = _currentTurn == PieceColor.White
                ? PieceColor.Black
                : PieceColor.White;
        }
    }
}
