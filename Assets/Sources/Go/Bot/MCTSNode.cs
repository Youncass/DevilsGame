using System.Collections.Generic;

public class MCTSNode
{
    public MCTSNode Parent;
    public List<MCTSNode> Children = new();

    public int MoveX = -1;
    public int MoveY = -1;

    public int Visits;
    public float TotalScore;

    // === AMAF статистика ===
    // AmafVisits[moveIdx]  — сколько раз ход moveIdx встречался в симуляциях,
    //                        прошедших через этот узел.
    // AmafScore[moveIdx]   — суммарная оценка (с точки зрения корневого игрока)
    //                        для этих симуляций.
    public int[] AmafVisits;
    public float[] AmafScore;

    // Состояние в узле
    public Stone PlayerToMove;
    public int KoPoint;
    public int LastMoveX = -1;
    public int LastMoveY = -1;
    public int CapturedBlack;
    public int CapturedWhite;
    public int BlackStones;
    public int WhiteStones;
    public bool[] ExpandedMoves;

    public float WinRate => Visits > 0 ? TotalScore / Visits : 0f;

    public MCTSNode(MCTSNode parent, int moveX, int moveY, int boardCells)
    {
        Parent = parent;
        MoveX = moveX;
        MoveY = moveY;
        AmafVisits = new int[boardCells];
        AmafScore = new float[boardCells];
    }

    public bool IsFullyExpanded(int maxChildren)
    {
        return Children.Count >= maxChildren;
    }
}
