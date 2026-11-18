/// <summary>
/// Оценка по правилам площади (area scoring, как в китайских правилах):
/// камни + территория. Пленники НЕ добавляются отдельно —otherwise захват
/// считался бы дважды (пустая точка уже стала территорией).
/// </summary>
public static class GoScoring
{
    public static ScoreBreakdown ComputeBreakdown(ref GoPosition pos, GoConfig config, bool removeDead = false)
    {
        if (removeDead) RemoveAtariGroups(ref pos);

        Stone[] board = pos.Cells;
        int n = pos.Count;
        int size = pos.Geometry.Size;
        int[] mark = pos.GroupMark;
        int[] queue = pos.Queue;
        int[] nb = pos.Geometry.Neighbors;

        int stamp = pos.NextStamp();
        int blackTerritory = 0;
        int whiteTerritory = 0;

        for (int i = 0; i < n; i++)
        {
            if (board[i] != Stone.Empty || mark[i] == stamp) continue;

            int head = 0, tail = 0;
            queue[tail++] = i;
            mark[i] = stamp;

            int regionSize = 0;
            bool touchesBlack = false;
            bool touchesWhite = false;

            while (head < tail)
            {
                int idx = queue[head++];
                regionSize++;
                int b = idx * 4;

                for (int d = 0; d < 4; d++)
                {
                    int ni = nb[b + d];
                    if (ni < 0) continue;
                    Stone v = board[ni];
                    if (v == Stone.Black) touchesBlack = true;
                    else if (v == Stone.White) touchesWhite = true;
                    else if (mark[ni] != stamp)
                    {
                        mark[ni] = stamp;
                        queue[tail++] = ni;
                    }
                }
            }

            if (touchesBlack && !touchesWhite) blackTerritory += regionSize;
            else if (touchesWhite && !touchesBlack) whiteTerritory += regionSize;
            // Регион, граничащий с обоими цветами ( или ни с одним ), нейтрален.
        }

        return new ScoreBreakdown
        {
            BlackTerritory = blackTerritory,
            WhiteTerritory = whiteTerritory,
            BlackStones = pos.BlackStones,
            WhiteStones = pos.WhiteStones,
            CapturedBlack = pos.CapturedBlack,
            CapturedWhite = pos.CapturedWhite,
            Komi = config.GetKomi(size),
            Size = size,
        };
    }

    public static float Evaluate(ref GoPosition pos, Stone rootPlayer, GoConfig config, bool removeDead = true)
    {
        var b = ComputeBreakdown(ref pos, config, removeDead);
        return b.Diff(rootPlayer);
    }

    /// <summary>
    /// Эвристика до подсчёта: группы в атари после двойного паса считаем мёртвыми.
    /// В реальной партии их всё равно захватил бы следующий ход противника.
    /// </summary>
    private static void RemoveAtariGroups(ref GoPosition pos)
    {
        int n = pos.Count;
        // Один проход: удалённые камни превращаются в либерти соседних групп,
        // поэтому повторный проход иногда снимает ещёGroup — но это уже seki-зона,
        // где эвристика может навредить. Ограничиваемся одним проходом.
        for (int i = 0; i < n; i++)
        {
            if (pos.Cells[i] == Stone.Empty) continue;
            if (pos.CountLiberties(i, 2) == 1)
                pos.RemoveGroup(i);
        }
    }
}

public struct ScoreBreakdown
{
    public int BlackTerritory;
    public int WhiteTerritory;
    public int BlackStones;
    public int WhiteStones;
    public int CapturedBlack; // чёрных захвачено
    public int CapturedWhite; // белых захвачено
    public float Komi;
    public int Size;

    public float BlackTotal => BlackStones + BlackTerritory;
    public float WhiteTotal => WhiteStones + WhiteTerritory + Komi;

    public float Diff(Stone player) => player == Stone.Black
        ? BlackTotal - WhiteTotal
        : WhiteTotal - BlackTotal;

    public override string ToString() =>
        $"B: area={BlackTotal:0} (stones={BlackStones} terr={BlackTerritory}) | " +
        $"W: area={WhiteTotal:0} (stones={WhiteStones} terr={WhiteTerritory} komi={Komi:0.0}) | " +
        $"prisoners B={CapturedBlack} W={CapturedWhite}";
}
