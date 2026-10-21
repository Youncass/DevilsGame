using System.Collections;
using UnityEngine;

public class GameSystem : MonoBehaviour
{
    [SerializeField] private Board _board;
    [SerializeField] private PieceColor _currentTurn;

    private void Start()
    {
        StartRound();
    }

    public void StartRound()
    {
        StartCoroutine(StartRound_Internal());

        IEnumerator StartRound_Internal()
        {
            while (true)
            {
                yield return _board.PerformMove(_currentTurn);

                if (_currentTurn == PieceColor.White)
                    _currentTurn = PieceColor.Black;
                else
                    _currentTurn = PieceColor.White;
            }
        }
    }
}
