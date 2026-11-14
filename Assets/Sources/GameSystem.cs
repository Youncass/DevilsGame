using System.Collections;
using UnityEngine;

public class GameSystem : MonoBehaviour
{
    [SerializeField] private Board _board;
    [SerializeField] private PlayerInput _input;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private PieceColor _currentTurn = PieceColor.White;

    [SerializeField] private GoConfig _botConfig;
    [SerializeField, Min(1)] private int _botSimulations = 5000;

    private BoardRaycaster _raycaster;
    private PlayerMoveProvider _playerMoves;
    private MCTSController _mcts;

    private void Awake()
    {
        _raycaster = new BoardRaycaster(_mainCamera, _board);
        _playerMoves = new PlayerMoveProvider(_input, _raycaster);
        _mcts = new MCTSController(_board.FieldSize, _botConfig);
        _board.Config = _botConfig;
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
                    _board.KoPoint,
                    _board.CapturedBlack,
                    _board.CapturedWhite);

                sw.Stop();

                if (search.BestMove.x < 0)
                {
                    Debug.LogWarning("[Go] Bot has no legal move — pass.");
                    _currentTurn = _currentTurn == PieceColor.White
                        ? PieceColor.Black
                        : PieceColor.White;
                    continue;
                }

                if (!_board.TryPlacePiece(search.BestMove, _currentTurn))
                {
                    Debug.LogError($"[Go] Bot returned illegal move {search.BestMove}. Fallback to any legal move.");
                    if (!TryAnyLegalMove(out Vector2Int fallback))
                        Debug.LogWarning("[Go] No legal moves at all — pass.");
                    else
                    {
                        _board.TryPlacePiece(fallback, _currentTurn);
                        Debug.Log($"[Go] Fallback move={fallback}");
                    }
                    LogScore("after bot (fallback)");
                    _currentTurn = _currentTurn == PieceColor.White
                        ? PieceColor.Black
                        : PieceColor.White;
                    continue;
                }

                LogBotMove(search, sw.ElapsedMilliseconds);
                LogScore("after bot");
            }

            _currentTurn = _currentTurn == PieceColor.White
                ? PieceColor.Black
                : PieceColor.White;
        }
    }

    private bool TryAnyLegalMove(out Vector2Int move)
    {
        Stone player = _currentTurn == PieceColor.Black ? Stone.Black : Stone.White;

        // Создаём локальный массив клеток (не трогаем board напрямую).
        int n = _board.FieldSize * _board.FieldSize;
        Stone[] cells = new Stone[n];
        var cellsReadonly = _board.Cells;
        for (int i = 0; i < n; i++) cells[i] = cellsReadonly[i];

        var state = GameState.Create(cells, _board.FieldSize, player);
        state.KoPoint = _board.KoPoint;
        state.CapturedBlack = _board.CapturedBlack;
        state.CapturedWhite = _board.CapturedWhite;

        for (int y = 0; y < _board.FieldSize; y++)
        {
            for (int x = 0; x < _board.FieldSize; x++)
            {
                if (GoLogic.IsLegalMove(ref state, x, y))
                {
                    move = new Vector2Int(x, y);
                    return true;
                }
            }
        }
        move = new Vector2Int(-1, -1);
        return false;
    }

    private void LogScore(string label)
    {
        var b = _board.ComputeScore();
        float blackDiff = b.Diff(Stone.Black);
        Debug.Log(
            $"[Score:{label}] {b}\n" +
            $"        diff(black)={blackDiff:+0.0;-0.0;0.0}");
    }

    private void LogBotMove(MoveSearchResult r, long ms)
    {
        float second = 0f, worst = 0f;
        int secondIdx = -1, worstIdx = -1;

        var visitedMoves = r.AllMoves.FindAll(m => m.visits >= 10);

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
