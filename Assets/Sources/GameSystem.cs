using System.Collections;
using System.Linq;
using UnityEngine;

public class GameSystem : MonoBehaviour
{
    [SerializeField] private Board _board;
    [SerializeField] private PlayerInput _input;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private PieceColor _currentTurn = PieceColor.White;
    [SerializeField, Min(1)] private int _botSimulations = 500;

    private BoardRaycaster _raycaster;
    private PlayerMoveProvider _playerMoves;
    private MCTSController _mcts;

    private void Awake()
    {
        _raycaster = new BoardRaycaster(_mainCamera, _board);
        _playerMoves = new PlayerMoveProvider(_input, _raycaster);
        _mcts = new MCTSController(_board.FieldSize);
    }

    private void Start() => StartCoroutine(GameLoop());

    private IEnumerator GameLoop()
    {
        var botThinkingTime = new WaitForSeconds(1);

        while (true)
        {
            if (_currentTurn == PieceColor.White)
            {
                Vector2Int? move = null;
                yield return _playerMoves.WaitForMove(m => move = m);

                if (move.HasValue == false || _board.TryPlacePiece(move.Value, _currentTurn) == false)
                    continue;

                Debug.Log($"[Player] move={move.Value}");
            }
            else
            {
                LogScore("before bot");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var search = _mcts.Search(
                    _board.Cells,
                    _currentTurn == PieceColor.Black ? Stone.Black : Stone.White,
                    _botSimulations,
                    _board.CapturedBlack,
                    _board.CapturedWhite);

                sw.Stop();

                if (search.BestMove.x < 0)
                {
                    Debug.LogWarning("[Go] Bot has no legal move — pass.");
                    // TODO: обработать пас
                    continue;
                }

                if (!_board.TryPlacePiece(search.BestMove, _currentTurn))
                {
                    Debug.LogError($"[Go] Bot returned illegal move {search.BestMove}");
                    continue;
                }

                LogBotMove(search, sw.ElapsedMilliseconds);
                LogScore("after bot");

                //yield return botThinkingTime;
            }

            _currentTurn = _currentTurn == PieceColor.White
                ? PieceColor.Black
                : PieceColor.White;
        }
    }

    private void LogScore(string label)
    {
        var b = _board.ComputeScore();
        float blackDiff = b.Diff(Stone.Black); // + => чёрные впереди
        Debug.Log(
            $"[Score:{label}] {b}\n" +
            $"        diff(black)={blackDiff:+0.0;-0.0;0.0}");
    }

    private void LogBotMove(MoveSearchResult r, long ms)
    {
        float second = 0f, worst = 0f;
        int secondIdx = -1, worstIdx = -1;

        // Берем только ходы с достаточным числом посещений
        var visitedMoves = r.AllMoves.Where(m => m.visits >= 10).ToList();

        for (int i = 0; i < visitedMoves.Count; i++)
        {
            var m = visitedMoves[i];
            if (m.move == r.BestMove) continue;

            if (secondIdx < 0 || m.winRate > second) { second = m.winRate; secondIdx = i; }
            if (worstIdx < 0 || m.winRate < worst) { worst = m.winRate; worstIdx = i; }
        }

        Debug.Log(
            $"[Bot] move={r.BestMove} score={r.BestWinRate:+0.00;-0.00;0.00} " +
            $"sims={r.Simulations} totalLegal={r.LegalMoveCount} time={ms}ms\n" +
            $"      2nd={(secondIdx >= 0 ? $"{visitedMoves[secondIdx].move} wr={second:+0.00;-0.00;0.00}" : "—")} " +
            $"worst={(worstIdx >= 0 ? $"{visitedMoves[worstIdx].move} wr={worst:+0.00;-0.00;0.00}" : "—")}");
    }
}
