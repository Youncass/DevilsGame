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
    [Tooltip("Общий бюджет времени на ход бота, мс")] [SerializeField, Min(1)] private float _botTimeBudgetMs = 1500f;
    [Tooltip("Сколько миллисекунд поиска можно съесть в одном кадре")] [SerializeField, Min(0.25f)] private float _frameBudgetMs = 4f;
    [SerializeField, Min(1)] private int _simsPerFrame = 128;

    private BoardRaycaster _raycaster;
    private PlayerMoveProvider _playerMoves;
    private MCTSController _mcts;

    private bool _waitingForPlayer;
    private bool _passRequested;
    private int _consecutivePasses;

    private static PieceColor Opponent(PieceColor c) => c == PieceColor.Black ? PieceColor.White : PieceColor.Black;

    private void Awake()
    {
        _raycaster = new BoardRaycaster(_mainCamera, _board);
        _playerMoves = new PlayerMoveProvider(_input, _raycaster);
        _mcts = new MCTSController(_board.FieldSize, _botConfig);
        _board.Config = _botConfig;

        _input.OnPass.AddListener(() =>
        {
            if (_waitingForPlayer) _passRequested = true;
        });
    }

    private void Start() => StartCoroutine(GameLoop());

    private IEnumerator GameLoop()
    {
        _consecutivePasses = 0;

        while (true)
        {
            if (_currentTurn == PieceColor.White)
            {
                Vector2Int? move = null;
                _passRequested = false;
                _waitingForPlayer = true;
                yield return _playerMoves.WaitForMove(m => move = m, () => _passRequested);
                _waitingForPlayer = false;

                if (_passRequested)
                {
                    RegisterPass("Player");
                    if (_consecutivePasses >= 2) break;
                    continue;
                }

                if (move.HasValue == false || _board.TryPlacePiece(move.Value, _currentTurn) == false)
                    continue;

                Debug.Log($"[Player] move={move.Value}");
                _consecutivePasses = 0;
            }
            else
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                Stone botColor = _currentTurn == PieceColor.Black ? Stone.Black : Stone.White;
                _mcts.Begin(
                    _board.Cells,
                    botColor,
                    _botSimulations,
                    _board.KoPoint,
                    _board.CapturedBlack,
                    _board.CapturedWhite,
                    _botTimeBudgetMs,
                    _frameBudgetMs);

                // Поиск режется по кадрам: main thread остаётся отзывчивым на мобильном.
                while (!_mcts.Step(_simsPerFrame))
                    yield return null;

                var search = _mcts.Result;
                sw.Stop();

                if (search.BestMove.x < 0)
                {
                    RegisterPass("Bot");
                    LogBotMove(search, sw.ElapsedMilliseconds);
                    if (_consecutivePasses >= 2) break;
                    continue;
                }

                if (!_board.TryPlacePiece(search.BestMove, _currentTurn))
                {
                    Debug.LogError($"[Go] Bot returned illegal move {search.BestMove}. Fallback to any legal move.");
                    if (!TryAnyLegalMove(out Vector2Int fallback))
                    {
                        RegisterPass("Bot");
                        if (_consecutivePasses >= 2) break;
                        continue;
                    }
                    _board.TryPlacePiece(fallback, _currentTurn);
                    Debug.Log($"[Go] Fallback move={fallback}");
                }

                LogBotMove(search, sw.ElapsedMilliseconds);
                _consecutivePasses = 0;
            }

            _currentTurn = Opponent(_currentTurn);
            if (_consecutivePasses >= 2) break;
        }

        LogScore("final");
        Debug.Log("[Go] Game over (two consecutive passes).");
    }

    private void RegisterPass(string who)
    {
        _consecutivePasses++;
        Debug.Log($"[{who}] pass ({_consecutivePasses}/2)");
        _currentTurn = Opponent(_currentTurn);
    }

    private bool TryAnyLegalMove(out Vector2Int move)
    {
        Stone player = _currentTurn == PieceColor.Black ? Stone.Black : Stone.White;
        int n = _board.FieldSize * _board.FieldSize;

        var cells = new Stone[n];
        for (int i = 0; i < n; i++) cells[i] = _board.Cells[i];

        var position = GoPosition.Create(cells, _board.FieldSize, player);
        position.KoPoint = _board.KoPoint;
        position.CapturedBlack = _board.CapturedBlack;
        position.CapturedWhite = _board.CapturedWhite;

        for (int y = 0; y < _board.FieldSize; y++)
        {
            for (int x = 0; x < _board.FieldSize; x++)
            {
                if (position.IsLegal(y * _board.FieldSize + x))
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
