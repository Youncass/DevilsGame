/// <summary>
/// Единая точка конверсий между слоями: представление (PieceColor) и правила (Stone).
/// Значения enum'ов совпадают, но полагаться на это без явного помощника не стоит.
/// </summary>
public static class GoConvert
{
    public static Stone ToStone(PieceColor color) =>
        color == PieceColor.Black ? Stone.Black : Stone.White;

    public static PieceColor ToPieceColor(Stone stone) =>
        stone == Stone.Black ? PieceColor.Black : PieceColor.White;

    public static PieceColor Opponent(PieceColor color) =>
        color == PieceColor.Black ? PieceColor.White : PieceColor.Black;
}
