using System.Collections.Generic;

public class MCTSNode
{
    public MCTSNode Parent;
    public List<MCTSNode> Children = new();

    public int MoveX = -1;
    public int MoveY = -1;

    public int Visits;
    public float TotalScore;

    // Состояние, в котором находится узел (ПОСЛЕ хода, приведшего в узел).
    // Для корня — начальное состояние.
    public Stone PlayerToMove;          // кто ходит ИЗ этого узла
    public int KoPoint;                 // ko-запрет для хода из этого узла
    public int LastMoveX = -1;
    public int LastMoveY = -1;
    public int CapturedBlack;           // накопленные захваты чёрных (белыми) с начала партии
    public int CapturedWhite;           // накопленные захваты белых (чёрными)
    public int BlackStones;             // камней на доске
    public int WhiteStones;

    // Кэш легальных ходов из этого узла (вычисляется лениво при первом раскрытии).
    // null = ещё не вычисляли.
    public bool[] ExpandedMoves;        // размер n: true = уже добавлен в Children

    public float WinRate => Visits > 0 ? TotalScore / Visits : 0f;

    public MCTSNode(MCTSNode parent, int moveX, int moveY)
    {
        Parent = parent;
        MoveX = moveX;
        MoveY = moveY;
    }

    public bool IsFullyExpanded(int maxChildren)
    {
        return Children.Count >= maxChildren;
    }
}
