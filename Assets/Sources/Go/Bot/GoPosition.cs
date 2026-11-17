using System;

/// <summary>
/// Геометрия доски. Создаётся один раз на размер доски и переиспользуется всеми
/// симуляциями: соседи/диагонали/расстояние до края не пересчитываются в BFS.
/// </summary>
public sealed class GoGeometry
{
    public readonly int Size;
    public readonly int Count;
    public readonly int[] Neighbors;   // 4 * Count, -1 = за краем доски
    public readonly int[] Diagonals;   // 4 * Count, -1 = за краем доски
    public readonly byte[] EdgeDistance; // 0 = первая линия

    private const int MaxCachedSize = 25;
    private static readonly GoGeometry[] Cache = new GoGeometry[MaxCachedSize + 1];

    public static GoGeometry Get(int size)
    {
        if (size < 2 || size > MaxCachedSize)
            throw new ArgumentOutOfRangeException(nameof(size));

        return Cache[size] ??= new GoGeometry(size);
    }

    private GoGeometry(int size)
    {
        Size = size;
        Count = size * size;
        Neighbors = new int[Count * 4];
        Diagonals = new int[Count * 4];
        EdgeDistance = new byte[Count];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                int b = idx * 4;

                Neighbors[b + 0] = x + 1 < size ? y * size + x + 1 : -1;
                Neighbors[b + 1] = x - 1 >= 0 ? y * size + x - 1 : -1;
                Neighbors[b + 2] = y + 1 < size ? (y + 1) * size + x : -1;
                Neighbors[b + 3] = y - 1 >= 0 ? (y - 1) * size + x : -1;

                Diagonals[b + 0] = (x + 1 < size && y + 1 < size) ? (y + 1) * size + x + 1 : -1;
                Diagonals[b + 1] = (x + 1 < size && y - 1 >= 0) ? (y - 1) * size + x + 1 : -1;
                Diagonals[b + 2] = (x - 1 >= 0 && y + 1 < size) ? (y + 1) * size + x - 1 : -1;
                Diagonals[b + 3] = (x - 1 >= 0 && y - 1 >= 0) ? (y - 1) * size + x - 1 : -1;

                int edge = Math.Min(Math.Min(x, y), Math.Min(size - 1 - x, size - 1 - y));
                EdgeDistance[idx] = (byte)edge;
            }
        }
    }
}

public static class GoRules
{
    public static Stone Opp(Stone s) => s == Stone.Black ? Stone.White : Stone.Black;
}

/// <summary>
/// Позиция и правила Го. Это struct: копия структуры дешевая, но все массивы общие.
/// Одна симуляция = BeginSimulation() -> набор Play/Pass -> Undo().
/// update-логика восстанавливает доску и счётчики, поэтому копировать Cells на каждый
/// ход не нужно.
/// </summary>
public struct GoPosition
{
    public GoGeometry Geometry;
    public Stone[] Cells;
    public int Count;
    public Stone SideToMove;
    public int KoPoint;
    public int CapturedBlack; // чёрных камней захвачено белыми
    public int CapturedWhite; // белых камней захвачено чёрными
    public int BlackStones;
    public int WhiteStones;
    public int LastMove;
    public int EmptyCount;

    // BFS-буферы (переиспользуются, штампы растут монотонно)
    public int[] GroupMark;
    public int[] LibertyMark;
    public int[] Queue;
    public int GroupStamp;
    public int LibertyStampValue;

    // Undo-лог
    public int[] UndoIndices;
    public Stone[] UndoOldStones;
    public int UndoCount;
    public int UndoSavedKo;
    public Stone UndoSavedSideToMove;
    public int UndoSavedCapturedBlack;
    public int UndoSavedCapturedWhite;
    public int UndoSavedBlackStones;
    public int UndoSavedWhiteStones;
    public int UndoSavedLastMove;
    public int UndoSavedEmptyCount;

    // Результат последнего хода (для view и ko)
    public int[] CapturedIndices;
    public int CapturedCount;

    public static GoPosition Create(Stone[] sourceCells, int size, Stone sideToMove, GoGeometry geometry = null)
    {
        geometry ??= GoGeometry.Get(size);
        int n = geometry.Count;

        var cells = new Stone[n];
        int black = 0, white = 0, empty = 0;
        for (int i = 0; i < n; i++)
        {
            cells[i] = i < sourceCells.Length ? sourceCells[i] : Stone.Empty;
            if (cells[i] == Stone.Black) black++;
            else if (cells[i] == Stone.White) white++;
            else empty++;
        }

        // Худший случай: ход + все захваченные камни на каждом ходе симуляции.
        int undoCapacity = 4 * n + 64;

        return new GoPosition
        {
            Geometry = geometry,
            Cells = cells,
            Count = n,
            SideToMove = sideToMove,
            KoPoint = -1,
            LastMove = -1,
            EmptyCount = empty,
            BlackStones = black,
            WhiteStones = white,

            GroupMark = new int[n],
            LibertyMark = new int[n],
            Queue = new int[n],
            GroupStamp = 0,
            LibertyStampValue = 0,

            UndoIndices = new int[undoCapacity],
            UndoOldStones = new Stone[undoCapacity],
            UndoCount = 0,

            CapturedIndices = new int[n],
            CapturedCount = 0,
        };
    }

    public void BeginSimulation()
    {
        UndoCount = 0;
        SaveScalars();
    }

    private void SaveScalars()
    {
        UndoSavedKo = KoPoint;
        UndoSavedSideToMove = SideToMove;
        UndoSavedCapturedBlack = CapturedBlack;
        UndoSavedCapturedWhite = CapturedWhite;
        UndoSavedBlackStones = BlackStones;
        UndoSavedWhiteStones = WhiteStones;
        UndoSavedLastMove = LastMove;
        UndoSavedEmptyCount = EmptyCount;
    }

    public void Undo()
    {
        for (int i = UndoCount - 1; i >= 0; i--)
            Cells[UndoIndices[i]] = UndoOldStones[i];
        UndoCount = 0;

        KoPoint = UndoSavedKo;
        SideToMove = UndoSavedSideToMove;
        CapturedBlack = UndoSavedCapturedBlack;
        CapturedWhite = UndoSavedCapturedWhite;
        BlackStones = UndoSavedBlackStones;
        WhiteStones = UndoSavedWhiteStones;
        LastMove = UndoSavedLastMove;
        EmptyCount = UndoSavedEmptyCount;
        CapturedCount = 0;
    }

    private void LogCell(int idx)
    {
        UndoIndices[UndoCount] = idx;
        UndoOldStones[UndoCount] = Cells[idx];
        UndoCount++;
    }

    public int NextStamp() => NextGroupStamp();

    private int NextGroupStamp()
    {
        int s = ++GroupStamp;
        if (s == int.MaxValue)
        {
            Array.Clear(GroupMark, 0, GroupMark.Length);
            GroupStamp = s = 1;
        }
        return s;
    }

    private int NextLibertyStamp()
    {
        int s = ++LibertyStampValue;
        if (s == int.MaxValue)
        {
            Array.Clear(LibertyMark, 0, LibertyMark.Length);
            LibertyStampValue = s = 1;
        }
        return s;
    }

    /// <summary>
    /// Обход группы. Уникальные либерти считаются через второй штамп — обычный
    /// счётчик «каждая пустая клетка соседствует с камнем» завышает либерти и ломает
    ///definition atari/ko. libertyLimit &gt; 0 = раньше выйти, когда либерти стало достаточно.
    /// remove = удалить группу и записать её клетки в CapturedIndices.
    /// </summary>
    private void ScanGroup(
        int start, int libertyLimit, bool remove,
        out int stones, out int liberties, out int firstLiberty)
    {
        stones = 0;
        liberties = 0;
        firstLiberty = -1;
        if (start < 0 || start >= Count || Cells[start] == Stone.Empty)
            return;

        Stone color = Cells[start];
        int mark = NextGroupStamp();
        int lmark = NextLibertyStamp();
        int[] markArr = GroupMark;
        int[] libArr = LibertyMark;
        int[] q = Queue;
        int[] nb = Geometry.Neighbors;

        int head = 0, tail = 0;
        q[tail++] = start;
        markArr[start] = mark;

        bool stopOnLimit = libertyLimit > 0;

        while (head < tail)
        {
            if (stopOnLimit && !remove && liberties >= libertyLimit)
                break;

            int idx = q[head++];
            stones++;

            if (remove)
            {
                LogCell(idx);
                if (Cells[idx] == Stone.Black) BlackStones--;
                else if (Cells[idx] == Stone.White) WhiteStones--;
                CapturedIndices[CapturedCount++] = idx;
                Cells[idx] = Stone.Empty;
                EmptyCount++;
            }

            int b = idx * 4;
            for (int d = 0; d < 4; d++)
            {
                int ni = nb[b + d];
                if (ni < 0) continue;

                Stone v = Cells[ni];
                if (v == Stone.Empty)
                {
                    if (libArr[ni] == lmark) continue; // та же либерти у нескольких камней
                    libArr[ni] = lmark;
                    liberties++;
                    if (firstLiberty < 0) firstLiberty = ni;
                }
                else if (v == color && markArr[ni] != mark)
                {
                    markArr[ni] = mark;
                    q[tail++] = ni;
                }
            }
        }
    }

    public int CountLiberties(int start, int limit = 0)
    {
        ScanGroup(start, limit, false, out _, out int libs, out _);
        return libs;
    }

    /// <summary>
    /// Информация о группе для анализа/триггеров.Массивы могут быть null —
    /// тогда возвращаются только счётчики. Не изменяет позицию.
    /// </summary>
    public int GetGroupInfo(int start, int[] groupCells, int[] libertyCells,
        out int groupCount, out int libertyCount)
    {
        groupCount = 0;
        libertyCount = 0;
        if (start < 0 || start >= Count || Cells[start] == Stone.Empty)
            return 0;

        Stone color = Cells[start];
        int gstamp = NextGroupStamp();
        int lstamp = NextLibertyStamp();
        int[] markArr = GroupMark;
        int[] libArr = LibertyMark;
        int[] q = Queue;
        int[] nb = Geometry.Neighbors;

        int head = 0, tail = 0;
        q[tail++] = start;
        markArr[start] = gstamp;

        while (head < tail)
        {
            int idx = q[head++];
            if (groupCells != null && groupCount < groupCells.Length)
                groupCells[groupCount] = idx;
            groupCount++;

            int b = idx * 4;
            for (int d = 0; d < 4; d++)
            {
                int ni = nb[b + d];
                if (ni < 0) continue;

                Stone v = Cells[ni];
                if (v == Stone.Empty)
                {
                    if (libArr[ni] == lstamp) continue;
                    libArr[ni] = lstamp;
                    if (libertyCells != null && libertyCount < libertyCells.Length)
                        libertyCells[libertyCount] = ni;
                    libertyCount++;
                }
                else if (v == color && markArr[ni] != gstamp)
                {
                    markArr[ni] = gstamp;
                    q[tail++] = ni;
                }
            }
        }

        return groupCount;
    }

    /// <summary>Удалить одну группу. Используется эвристикой мёртвых камней в конце партии.</summary>
    public int RemoveGroup(int start)
    {
        int before = CapturedCount;
        ScanGroup(start, 0, true, out int removed, out _, out _);
        return removed;
    }

    public bool HasLiberty(int start)
    {
        ScanGroup(start, 1, false, out _, out int libs, out _);
        return libs > 0;
    }

    /// <summary>Единственная либерти группы или -1 (один обход вместо двух).</summary>
    public int GetSingleLiberty(int start)
    {
        ScanGroup(start, 2, false, out _, out int libs, out int first);
        return libs == 1 ? first : -1;
    }

    /// <summary>Ход легален, если не занят/не ko, и либо есть либерти, либо захватывает.</summary>
    public bool IsLegal(int idx)
    {
        if (idx < 0 || idx >= Count) return false;
        if (Cells[idx] != Stone.Empty || idx == KoPoint) return false;

        Stone me = SideToMove;
        Stone opp = GoRules.Opp(me);
        int[] nb = Geometry.Neighbors;
        int b = idx * 4;

        for (int d = 0; d < 4; d++)
        {
            int ni = nb[b + d];
            if (ni < 0) continue;
            Stone v = Cells[ni];
            if (v == Stone.Empty) return true;
            if (v == opp && CountLiberties(ni, 2) == 1) return true; // захват
        }

        // Проверка суицида: временно ставим камень (без лога — состояние вернём).
        Cells[idx] = me;
        bool survives = HasLiberty(idx);
        Cells[idx] = Stone.Empty;
        return survives;
    }

    /// <summary>Захватывает ли ход минимум одну группу (для фильтра self-atari).</summary>
    public bool CapturesSomething(int idx)
    {
        Stone opp = GoRules.Opp(SideToMove);
        int[] nb = Geometry.Neighbors;
        int b = idx * 4;
        for (int d = 0; d < 4; d++)
        {
            int ni = nb[b + d];
            if (ni >= 0 && Cells[ni] == opp && CountLiberties(ni, 2) == 1)
                return true;
        }
        return false;
    }

    public bool Play(int idx, out int captured)
    {
        captured = 0;
        CapturedCount = 0;
        if (!IsLegal(idx)) return false;

        Stone me = SideToMove;
        Stone opp = GoRules.Opp(me);

        LogCell(idx);
        Cells[idx] = me;
        EmptyCount--;
        if (me == Stone.Black) BlackStones++;
        else WhiteStones++;

        int singleCapturePoint = -1;
        int[] nb = Geometry.Neighbors;
        int b = idx * 4;

        for (int d = 0; d < 4; d++)
        {
            int ni = nb[b + d];
            if (ni < 0 || Cells[ni] != opp) continue;
            // Камень уже поставлен: у захватываемой группы должно остаться
            // НОЛЬ либерти (до хода их была ровно одна — это проверяет IsLegal).
            if (CountLiberties(ni, 2) != 0) continue;

            int before = CapturedCount;
            ScanGroup(ni, 0, true, out int removed, out _, out _);
            captured += removed;
            if (CapturedCount - before == 1) singleCapturePoint = ni;

            if (opp == Stone.Black) CapturedBlack += removed;
            else CapturedWhite += removed;
        }

        // Простое ко: захватили ровно один камень, и наш камень-одиночка
        // оказался в атари с единственной либерти на захваченной точке.
        if (captured == 1 && singleCapturePoint >= 0)
        {
            ScanGroup(idx, 2, false, out int ownSize, out int ownLibs, out int ownLiberty);
            KoPoint = (ownSize == 1 && ownLibs == 1 && ownLiberty == singleCapturePoint)
                ? singleCapturePoint
                : -1;
        }
        else
        {
            KoPoint = -1;
        }

        LastMove = idx;
        SideToMove = opp;
        return true;
    }

    public void Pass()
    {
        KoPoint = -1;
        LastMove = -1;
        CapturedCount = 0;
        SideToMove = GoRules.Opp(SideToMove);
    }

    /// <summary>
    /// Пустая точка — глава «своего» цвета: все ортогональные соседи на доске
    /// дружественные, а диагональных врагов не больше одного (для середины доски).
    /// </summary>
    public bool IsEye(int idx, Stone me)
    {
        if (idx < 0 || idx >= Count || Cells[idx] != Stone.Empty) return false;

        Stone opp = GoRules.Opp(me);
        int[] nb = Geometry.Neighbors;
        int[] dg = Geometry.Diagonals;
        int b = idx * 4;
        int orth = 0;

        for (int d = 0; d < 4; d++)
        {
            int ni = nb[b + d];
            if (ni < 0) continue;
            if (Cells[ni] != me) return false;
            orth++;
        }
        if (orth < 2) return false; // минимум — угол доски

        bool center = orth == 4;
        int enemyDiagonals = 0;
        for (int d = 0; d < 4; d++)
        {
            int di = dg[b + d];
            if (di < 0 || Cells[di] != opp) continue;
            enemyDiagonals++;
            if (enemyDiagonals > (center ? 1 : 0)) return false;
        }
        return true;
    }

    /// <summary>Ход ведёт к self-atari (без захвата)?</summary>
    public bool IsSelfAtari(int idx)
    {
        if (idx < 0 || idx >= Count || Cells[idx] != Stone.Empty) return false;
        if (CapturesSomething(idx)) return false;

        Stone me = SideToMove;
        Cells[idx] = me;
        int libs = CountLiberties(idx, 2);
        Cells[idx] = Stone.Empty;
        return libs < 2;
    }
}
