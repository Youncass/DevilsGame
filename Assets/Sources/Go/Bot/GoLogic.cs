using System;

public static class GoLogic
{
    public struct Buffers
    {
        public int[] VisitedStamp;
        public int[] Queue;
        public int[] LegalMoves;
        public int CurrentStamp;

        public static Buffers Allocate(int boardSize)
        {
            int n = boardSize * boardSize;
            return new Buffers
            {
                VisitedStamp = new int[n],
                Queue = new int[n],
                LegalMoves = new int[n],
                CurrentStamp = 0,
            };
        }

        public void NextStamp()
        {
            CurrentStamp++;
            if (CurrentStamp == int.MaxValue)
            {
                Array.Clear(VisitedStamp, 0, VisitedStamp.Length);
                CurrentStamp = 1;
            }
        }
    }

    // ============================================================
    // IsLegalMove — БЕЗ NextStamp (использует GroupHasLiberty, который делает NextStamp)
    // ============================================================
    public static bool IsLegalMove(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, int koPoint = -1)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        int idx = y * size + x;
        if (board[idx] != Stone.Empty) return false;
        if (idx == koPoint) return false;

        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;

        // Быстрый путь: если есть пустой сосед — легально.
        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            if (board[ny * size + nx] == Stone.Empty) return true;
        }

        // Медленный путь.
        board[idx] = player;
        bool capturesAtLeastOne = false;

        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            int nIdx = ny * size + nx;
            if (board[nIdx] != opponent) continue;

            if (!GroupHasLiberty(board, size, nx, ny, opponent, ref buffers))
            {
                capturesAtLeastOne = true;
                break;
            }
        }

        bool legal = capturesAtLeastOne || GroupHasLiberty(board, size, x, y, player, ref buffers);
        board[idx] = Stone.Empty;
        return legal;
    }

    // ============================================================
    // ApplyMove — БЕЗ изменений
    // ============================================================
    public static int ApplyMove(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers,
        ref int capturedBlack, ref int capturedWhite,
        ref int blackStones, ref int whiteStones,
        out int captured)
    {
        int idx = y * size + x;
        board[idx] = player;
        if (player == Stone.Black) blackStones++;
        else whiteStones++;

        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;
        captured = 0;
        int newKoPoint = -1;

        int removedGroups = 0;
        int lastRemovedSingleIdx = -1;

        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            int nIdx = ny * size + nx;
            if (board[nIdx] != opponent) continue;

            if (!GroupHasLiberty(board, size, nx, ny, opponent, ref buffers))
            {
                int removed = RemoveGroup(board, size, nx, ny, opponent, ref buffers, out bool wasSingle);
                captured += removed;
                removedGroups++;
                if (wasSingle) lastRemovedSingleIdx = nIdx;

                if (opponent == Stone.Black) { capturedBlack += removed; blackStones -= removed; }
                else { capturedWhite += removed; whiteStones -= removed; }
            }
        }

        if (removedGroups == 1 && captured == 1)
            newKoPoint = lastRemovedSingleIdx;

        return newKoPoint;
    }

    // ============================================================
    // GroupHasLiberty — С NextStamp
    // ============================================================
    public static bool GroupHasLiberty(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        int startIdx = y * size + x;
        if (board[startIdx] != player) return false;

        buffers.NextStamp();
        int[] stamp = buffers.VisitedStamp;
        int[] queue = buffers.Queue;
        int currentStamp = buffers.CurrentStamp;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        stamp[startIdx] = currentStamp;
        bool hasLiberty = false;

        while (head < tail)
        {
            int idx = queue[head++];
            int cx = idx % size;
            int cy = idx / size;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int nIdx = ny * size + nx;

                if (board[nIdx] == Stone.Empty)
                {
                    hasLiberty = true;
                    head = tail;
                    break;
                }
                if (board[nIdx] == player && stamp[nIdx] != currentStamp)
                {
                    stamp[nIdx] = currentStamp;
                    queue[tail++] = nIdx;
                }
            }
        }

        return hasLiberty;
    }

    // ============================================================
    // CountLiberties — С NextStamp
    // ============================================================
    public static int CountLiberties(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, int maxCount = 3)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return 0;
        int startIdx = y * size + x;
        if (board[startIdx] != player) return 0;

        buffers.NextStamp();
        int[] stamp = buffers.VisitedStamp;
        int[] queue = buffers.Queue;
        int currentStamp = buffers.CurrentStamp;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        stamp[startIdx] = currentStamp;
        int liberties = 0;

        while (head < tail)
        {
            int idx = queue[head++];
            int cx = idx % size;
            int cy = idx / size;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int nIdx = ny * size + nx;

                if (board[nIdx] == Stone.Empty)
                {
                    liberties++;
                    if (liberties >= maxCount) return maxCount;
                }
                else if (board[nIdx] == player && stamp[nIdx] != currentStamp)
                {
                    stamp[nIdx] = currentStamp;
                    queue[tail++] = nIdx;
                }
            }
        }

        return liberties;
    }

    // ============================================================
    // RemoveGroup — С NextStamp
    // ============================================================
    public static int RemoveGroup(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, out bool wasSingle)
    {
        wasSingle = false;
        if (x < 0 || x >= size || y < 0 || y >= size) return 0;
        int startIdx = y * size + x;
        if (board[startIdx] != player) return 0;

        buffers.NextStamp();
        int[] stamp = buffers.VisitedStamp;
        int[] queue = buffers.Queue;
        int currentStamp = buffers.CurrentStamp;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        stamp[startIdx] = currentStamp;
        int removed = 0;

        while (head < tail)
        {
            int idx = queue[head++];
            board[idx] = Stone.Empty;
            removed++;

            int cx = idx % size;
            int cy = idx / size;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int nIdx = ny * size + nx;

                if (board[nIdx] == player && stamp[nIdx] != currentStamp)
                {
                    stamp[nIdx] = currentStamp;
                    queue[tail++] = nIdx;
                }
            }
        }

        wasSingle = removed == 1;
        return removed;
    }

    // ============================================================
    // TryFindSingleLiberty — С NextStamp
    // ============================================================
    private static bool TryFindSingleLiberty(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, out int libX, out int libY)
    {
        libX = -1; libY = -1;
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        int startIdx = y * size + x;
        if (board[startIdx] != player) return false;

        buffers.NextStamp();
        int[] stamp = buffers.VisitedStamp;
        int[] queue = buffers.Queue;
        int currentStamp = buffers.CurrentStamp;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        stamp[startIdx] = currentStamp;
        int libCount = 0;

        while (head < tail)
        {
            int idx = queue[head++];
            int cx = idx % size;
            int cy = idx / size;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int nIdx = ny * size + nx;

                if (board[nIdx] == Stone.Empty)
                {
                    libCount++;
                    if (libCount == 1) { libX = nx; libY = ny; }
                    else if (libCount > 1) return false;
                }
                else if (board[nIdx] == player && stamp[nIdx] != currentStamp)
                {
                    stamp[nIdx] = currentStamp;
                    queue[tail++] = nIdx;
                }
            }
        }

        return libCount == 1;
    }

    // ============================================================
    // FindCaptureMove — БЕЗ изменений (использует CountLiberties/TryFindSingleLiberty)
    // ============================================================
    public static bool FindCaptureMove(
        Stone[] board, int size, Stone player,
        ref Buffers buffers, int koPoint,
        out int bx, out int by)
    {
        bx = -1; by = -1;
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (board[idx] != opponent) continue;

                int libs = CountLiberties(board, size, x, y, opponent, ref buffers, maxCount: 2);
                if (libs != 1) continue;

                if (!TryFindSingleLiberty(board, size, x, y, opponent, ref buffers, out int libX, out int libY))
                    continue;

                int libIdx = libY * size + libX;
                if (libIdx == koPoint) continue;

                bx = libX; by = libY;
                return true;
            }
        }

        return false;
    }

    // ============================================================
    // IsSelfAtari — БЕЗ изменений (использует GroupHasLiberty/CountLiberties)
    // ============================================================
    public static bool IsSelfAtari(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers)
    {
        int idx = y * size + x;
        if (board[idx] != Stone.Empty) return false;

        board[idx] = player;

        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;
        bool captures = false;
        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            int nIdx = ny * size + nx;
            if (board[nIdx] == opponent && !GroupHasLiberty(board, size, nx, ny, opponent, ref buffers))
            {
                captures = true;
                break;
            }
        }

        if (captures)
        {
            board[idx] = Stone.Empty;
            return false;
        }

        int libs = CountLiberties(board, size, x, y, player, ref buffers, maxCount: 2);
        board[idx] = Stone.Empty;
        return libs < 2;
    }

    // ============================================================
    // FindAtariEscape — С ПРОВЕРКОЙ, что группа реально спасена
    // ============================================================
    public static bool FindAtariEscape(
        Stone[] board, int size, Stone player,
        ref Buffers buffers, int koPoint,
        out int bx, out int by)
    {
        bx = -1; by = -1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (board[idx] != player) continue;

                int libs = CountLiberties(board, size, x, y, player, ref buffers, maxCount: 2);
                if (libs != 1) continue;

                if (!TryFindSingleLiberty(board, size, x, y, player, ref buffers, out int libX, out int libY))
                    continue;

                int libIdx = libY * size + libX;
                if (libIdx == koPoint) continue;
                if (!IsLegalMove(board, size, libX, libY, player, ref buffers, koPoint)) continue;

                // === ПРОВЕРКА: после хода группа реально спасена (>=2 либерти)? ===
                board[libIdx] = player;
                int newLibs = CountLiberties(board, size, libX, libY, player, ref buffers, maxCount: 3);
                board[libIdx] = Stone.Empty;

                if (newLibs < 2) continue;   // не спасена — пропускаем

                bx = libX; by = libY;
                return true;
            }
        }

        return false;
    }

    // ============================================================
    // FindAtariThreat — БЕЗ изменений
    // ============================================================
    public static bool FindAtariThreat(
        Stone[] board, int size, Stone player,
        ref Buffers buffers, int koPoint,
        out int bx, out int by)
    {
        bx = -1; by = -1;
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (board[idx] != opponent) continue;

                int libs = CountLiberties(board, size, x, y, opponent, ref buffers, maxCount: 3);
                if (libs != 2) continue;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    int nIdx = ny * size + nx;
                    if (board[nIdx] != Stone.Empty) continue;
                    if (nIdx == koPoint) continue;
                    if (!IsLegalMove(board, size, nx, ny, player, ref buffers, koPoint)) continue;
                    if (IsSelfAtari(board, size, nx, ny, player, ref buffers)) continue;

                    bx = nx; by = ny;
                    return true;
                }
            }
        }

        return false;
    }

    // ============================================================
    // SmartRandomMove — БЕЗ изменений (использует обновлённые хелперы)
    // ============================================================
    public static bool SmartRandomMove(
        Stone[] board, int size, Stone player,
        ref Buffers buffers, System.Random rng,
        int koPoint, int lastX, int lastY, float localBias,
        out int bx, out int by)
    {
        bx = -1; by = -1;
        int n = size * size;

        if (FindAtariEscape(board, size, player, ref buffers, koPoint, out int ex, out int ey))
        {
            bx = ex; by = ey;
            return true;
        }

        if (rng.NextDouble() < 0.7)
        {
            if (FindAtariThreat(board, size, player, ref buffers, koPoint, out int tx, out int ty))
            {
                bx = tx; by = ty;
                return true;
            }
        }

        if (rng.NextDouble() < 0.5)
        {
            if (FindCaptureMove(board, size, player, ref buffers, koPoint, out int cx, out int cy))
            {
                if (IsLegalMove(board, size, cx, cy, player, ref buffers, koPoint))
                {
                    bx = cx; by = cy;
                    return true;
                }
            }
        }

        if (lastX >= 0 && lastY >= 0 && rng.NextDouble() < localBias)
        {
            Span<int> ring1 = stackalloc int[8];
            Span<int> ring2 = stackalloc int[16];
            int c1 = 0, c2 = 0;

            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = lastX + dx;
                    int ny = lastY + dy;
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    if (board[ny * size + nx] != Stone.Empty) continue;

                    int dist = (dx * dx + dy * dy);
                    if (dist <= 2) ring1[c1++] = ny * size + nx;
                    else ring2[c2++] = ny * size + nx;
                }
            }

            Shuffle(ring1, c1, rng);
            Shuffle(ring2, c2, rng);

            for (int i = 0; i < c1; i++)
            {
                int idx = ring1[i];
                int x = idx % size, y = idx / size;
                if (!IsLegalMove(board, size, x, y, player, ref buffers, koPoint)) continue;
                if (IsSelfAtari(board, size, x, y, player, ref buffers)) continue;
                bx = x; by = y;
                return true;
            }
            for (int i = 0; i < c2; i++)
            {
                int idx = ring2[i];
                int x = idx % size, y = idx / size;
                if (!IsLegalMove(board, size, x, y, player, ref buffers, koPoint)) continue;
                if (IsSelfAtari(board, size, x, y, player, ref buffers)) continue;
                bx = x; by = y;
                return true;
            }
        }

        int attempts = 0;
        while (attempts < 20)
        {
            int idx = rng.Next(n);
            if (board[idx] != Stone.Empty) { attempts++; continue; }
            int x = idx % size;
            int y = idx / size;
            if (!IsLegalMove(board, size, x, y, player, ref buffers, koPoint)) { attempts++; continue; }
            if (IsSelfAtari(board, size, x, y, player, ref buffers)) { attempts++; continue; }
            bx = x; by = y;
            return true;
        }

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (board[y * size + x] != Stone.Empty) continue;
                if (!IsLegalMove(board, size, x, y, player, ref buffers, koPoint)) continue;
                if (IsSelfAtari(board, size, x, y, player, ref buffers)) continue;
                bx = x; by = y;
                return true;
            }

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (IsLegalMove(board, size, x, y, player, ref buffers, koPoint))
                {
                    bx = x; by = y;
                    return true;
                }
            }

        return false;
    }

    private static void Shuffle(Span<int> arr, int count, System.Random rng)
    {
        for (int i = count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}
