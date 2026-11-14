using System.Collections.Generic;

public class MCTSNode
{
    public MCTSNode Parent;
    public List<MCTSNode> Children = new();

    public int MoveX = -1;
    public int MoveY = -1;

    public int Visits;
    public float TotalScore;
    public int EffectiveVisits;

    public int[] AmafVisits;
    public float[] AmafScore;

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
}
