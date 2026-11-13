//using Unity.Burst;
//using Unity.Collections;
//using Unity.Jobs;
//using Unity.Mathematics;

//[BurstCompile]
//public struct MCTSJob : IJobParallelFor
//{
//    [ReadOnly] public NativeArray<Stone> InitialBoard;
//    public int BoardSize;
//    public Stone Player;
//    public int MoveX;
//    public int MoveY;
//    public uint Seed;

//    public NativeArray<float> Scores;
//    public NativeArray<int> Valids;

//    public NativeArray<Stone> PoolBoards;
//    public NativeArray<bool> PoolVisited;
//    public NativeArray<int> PoolQueue;
//    public NativeArray<int> PoolLegalMoves;

//    public void Execute(int index)
//    {
//        int n = InitialBoard.Length;
//        int offset = index * n;

//        if (offset + n > PoolBoards.Length)
//            return;

//        var rng = new Random(Seed + (uint)index * 7919u);

//        var board = PoolBoards.GetSubArray(offset, n);
//        var visited = PoolVisited.GetSubArray(offset, n);
//        var queue = PoolQueue.GetSubArray(offset, n);
//        var legal = PoolLegalMoves.GetSubArray(offset, n);

//        var buffers = new GoLogic.Buffers
//        {
//            Visited = visited,
//            Queue = queue,
//            LegalMoves = legal
//        };

//        NativeArray<Stone>.Copy(InitialBoard, board);

//        int blackStones = 0, whiteStones = 0;
//        for (int i = 0; i < n; i++)
//        {
//            if (board[i] == Stone.Black) blackStones++;
//            else if (board[i] == Stone.White) whiteStones++;
//        }

//        int capB = 0, capW = 0;
//        int koPoint = GoLogic.ApplyMove(
//            board, BoardSize, MoveX, MoveY, Player,
//            ref buffers, ref capB, ref capW, ref blackStones, ref whiteStones, out _);

//        Stone current = Player == Stone.Black ? Stone.White : Stone.Black;
//        bool playerPassed = false;
//        bool opponentPassed = false;
//        int maxMoves = BoardSize * BoardSize * 4;
//        int moves = 0;

//        while (moves < maxMoves && !(playerPassed && opponentPassed))
//        {
//            if (GoLogic.FastRandomMove(board, BoardSize, current, ref buffers, ref rng, koPoint, out int bx, out int by))
//            {
//                koPoint = GoLogic.ApplyMove(
//                    board, BoardSize, bx, by, current,
//                    ref buffers, ref capB, ref capW, ref blackStones, ref whiteStones, out _);

//                if (current == Player) playerPassed = false;
//                else opponentPassed = false;
//            }
//            else
//            {
//                koPoint = -1;
//                if (current == Player) playerPassed = true;
//                else opponentPassed = true;
//            }
//            current = current == Stone.Black ? Stone.White : Stone.Black;
//            moves++;
//        }

//        float score = GoScore.Evaluate(
//            board, BoardSize, Player,
//            blackStones, whiteStones, capB, capW,
//            buffers.Visited, buffers.Queue);

//        Scores[index] = score;
//        Valids[index] = 1;
//    }
//}
