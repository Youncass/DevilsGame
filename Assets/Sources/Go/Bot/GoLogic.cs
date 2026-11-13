using System;

public static class GoLogic
{
    public struct Buffers
    {
        public int[] VisitedStamp;
        public int[] Queue;
        public int CurrentStamp;

        public static Buffers Allocate(int boardSize)
        {
            int n = boardSize * boardSize;
            return new Buffers
            {
                VisitedStamp = new int[n],
                Queue = new int[n],
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
    // IsLegalMove
    // ============================================================
    public static bool IsLegalMove(ref GameState state, int x, int y)
    {
        int size = state.Size;
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        int idx = y * size + x;
        if (state.Cells[idx] != Stone.Empty) return false;
        if (idx == state.KoPoint) return false;

        Stone player = state.PlayerToMove;
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;

        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            if (state.Cells[ny * size + nx] == Stone.Empty) return true;
        }

        state.Cells[idx] = player;
        bool capturesAtLeastOne = false;

        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            int nIdx = ny * size + nx;
            if (state.Cells[nIdx] != opponent) continue;
            if (!GroupHasLiberty(ref state, nx, ny, opponent)) { capturesAtLeastOne = true; break; }
        }

        bool legal = capturesAtLeastOne || GroupHasLiberty(ref state, x, y, player);
        state.Cells[idx] = Stone.Empty;
        return legal;
    }

    // ============================================================
    // ApplyMove
    // ============================================================
    public static bool ApplyMove(ref GameState state, int x, int y, out int captured)
    {
        captured = 0;
        if (!IsLegalMove(ref state, x, y)) return false;

        int size = state.Size;
        Stone player = state.PlayerToMove;
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;
        int idx = y * size + x;

        state.LogCell(idx);
        state.Cells[idx] = player;
        if (player == Stone.Black) state.BlackStones++;
        else state.WhiteStones++;

        int removedGroups = 0;
        int lastRemovedSingleIdx = -1;

        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            int nIdx = ny * size + nx;
            if (state.Cells[nIdx] != opponent) continue;

            if (!GroupHasLiberty(ref state, nx, ny, opponent))
            {
                int removed = RemoveGroup(ref state, nx, ny, opponent, out bool wasSingle);
                captured += removed;
                removedGroups++;
                if (wasSingle) lastRemovedSingleIdx = nIdx;

                if (opponent == Stone.Black) { state.CapturedBlack += removed; state.BlackStones -= removed; }
                else { state.CapturedWhite += removed; state.WhiteStones -= removed; }
            }
        }

        state.KoPoint = (removedGroups == 1 && captured == 1) ? lastRemovedSingleIdx : -1;
        state.PlayerToMove = opponent;
        return true;
    }

    // ============================================================
    // GroupHasLiberty
    // ============================================================
    public static bool GroupHasLiberty(ref GameState state, int x, int y, Stone player)
    {
        int size = state.Size;
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        int startIdx = y * size + x;
        if (state.Cells[startIdx] != player) return false;

        state.Buffers.NextStamp();
        int[] stamp = state.Buffers.VisitedStamp;
        int[] queue = state.Buffers.Queue;
        int cur = state.Buffers.CurrentStamp;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        stamp[startIdx] = cur;

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

                if (state.Cells[nIdx] == Stone.Empty) return true;
                if (state.Cells[nIdx] == player && stamp[nIdx] != cur)
                {
                    stamp[nIdx] = cur;
                    queue[tail++] = nIdx;
                }
            }
        }
        return false;
    }

    // ============================================================
    // CountLiberties
    // ============================================================
    public static int CountLiberties(ref GameState state, int x, int y, Stone player, int maxCount = 3)
    {
        int size = state.Size;
        if (x < 0 || x >= size || y < 0 || y >= size) return 0;
        int startIdx = y * size + x;
        if (state.Cells[startIdx] != player) return 0;

        state.Buffers.NextStamp();
        int[] stamp = state.Buffers.VisitedStamp;
        int[] queue = state.Buffers.Queue;
        int cur = state.Buffers.CurrentStamp;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        stamp[startIdx] = cur;
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

                if (state.Cells[nIdx] == Stone.Empty)
                {
                    liberties++;
                    if (liberties >= maxCount) return maxCount;
                }
                else if (state.Cells[nIdx] == player && stamp[nIdx] != cur)
                {
                    stamp[nIdx] = cur;
                    queue[tail++] = nIdx;
                }
            }
        }
        return liberties;
    }

    // ============================================================
    // RemoveGroup
    // ============================================================
    public static int RemoveGroup(ref GameState state, int x, int y, Stone player, out bool wasSingle)
    {
        wasSingle = false;
        int size = state.Size;
        if (x < 0 || x >= size || y < 0 || y >= size) return 0;
        int startIdx = y * size + x;
        if (state.Cells[startIdx] != player) return 0;

        state.Buffers.NextStamp();
        int[] stamp = state.Buffers.VisitedStamp;
        int[] queue = state.Buffers.Queue;
        int cur = state.Buffers.CurrentStamp;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        stamp[startIdx] = cur;
        int removed = 0;

        while (head < tail)
        {
            int idx = queue[head++];
            state.LogCell(idx);
            state.Cells[idx] = Stone.Empty;
            removed++;

            int cx = idx % size;
            int cy = idx / size;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int nIdx = ny * size + nx;

                if (state.Cells[nIdx] == player && stamp[nIdx] != cur)
                {
                    stamp[nIdx] = cur;
                    queue[tail++] = nIdx;
                }
            }
        }

        wasSingle = removed == 1;
        return removed;
    }

    // ============================================================
    // TryFindSingleLiberty
    // ============================================================
    private static bool TryFindSingleLiberty(ref GameState state, int x, int y, Stone player, out int libX, out int libY)
    {
        libX = -1; libY = -1;
        int size = state.Size;
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        int startIdx = y * size + x;
        if (state.Cells[startIdx] != player) return false;

        state.Buffers.NextStamp();
        int[] stamp = state.Buffers.VisitedStamp;
        int[] queue = state.Buffers.Queue;
        int cur = state.Buffers.CurrentStamp;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        stamp[startIdx] = cur;
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

                if (state.Cells[nIdx] == Stone.Empty)
                {
                    libCount++;
                    if (libCount == 1) { libX = nx; libY = ny; }
                    else if (libCount > 1) return false;
                }
                else if (state.Cells[nIdx] == player && stamp[nIdx] != cur)
                {
                    stamp[nIdx] = cur;
                    queue[tail++] = nIdx;
                }
            }
        }
        return libCount == 1;
    }

    // ============================================================
    // FindCaptureMove
    // ============================================================
    public static bool FindCaptureMove(ref GameState state, out int bx, out int by)
    {
        bx = -1; by = -1;
        Stone player = state.PlayerToMove;
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;
        int size = state.Size;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (state.Cells[idx] != opponent) continue;

                int libs = CountLiberties(ref state, x, y, opponent, maxCount: 2);
                if (libs != 1) continue;

                if (!TryFindSingleLiberty(ref state, x, y, opponent, out int libX, out int libY)) continue;

                int libIdx = libY * size + libX;
                if (libIdx == state.KoPoint) continue;

                bx = libX; by = libY;
                return true;
            }
        }
        return false;
    }

    // ============================================================
    // IsSelfAtari
    // ============================================================
    public static bool IsSelfAtari(ref GameState state, int x, int y)
    {
        int size = state.Size;
        int idx = y * size + x;
        if (state.Cells[idx] != Stone.Empty) return false;

        Stone player = state.PlayerToMove;
        state.Cells[idx] = player;

        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;
        bool captures = false;
        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            int nIdx = ny * size + nx;
            if (state.Cells[nIdx] == opponent && !GroupHasLiberty(ref state, nx, ny, opponent))
            {
                captures = true;
                break;
            }
        }

        if (captures) { state.Cells[idx] = Stone.Empty; return false; }

        int libs = CountLiberties(ref state, x, y, player, maxCount: 2);
        state.Cells[idx] = Stone.Empty;
        return libs < 2;
    }

    public static bool HasAnyGroupInAtari(ref GameState state, Stone player)
    {
        int size = state.Size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (state.Cells[idx] != player) continue;

                int libs = CountLiberties(ref state, x, y, player, maxCount: 2);
                if (libs == 1) return true;
            }
        }
        return false;
    }

    // ============================================================
    // FindAtariEscape
    // ============================================================
    public static bool FindAtariEscape(ref GameState state, out int bx, out int by)
    {
        bx = -1; by = -1;
        Stone player = state.PlayerToMove;
        int size = state.Size;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (state.Cells[idx] != player) continue;

                int libs = CountLiberties(ref state, x, y, player, maxCount: 2);
                if (libs != 1) continue;

                if (!TryFindSingleLiberty(ref state, x, y, player, out int libX, out int libY)) continue;

                int libIdx = libY * size + libX;
                if (libIdx == state.KoPoint) continue;
                if (!IsLegalMove(ref state, libX, libY)) continue;

                state.Cells[libIdx] = player;
                int newLibs = CountLiberties(ref state, libX, libY, player, maxCount: 3);
                state.Cells[libIdx] = Stone.Empty;

                if (newLibs < 2) continue;

                bx = libX; by = libY;
                return true;
            }
        }
        return false;
    }

    // ============================================================
    // FindAtariThreat
    // ============================================================
    public static bool FindAtariThreat(ref GameState state, out int bx, out int by)
    {
        bx = -1; by = -1;
        Stone player = state.PlayerToMove;
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;
        int size = state.Size;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (state.Cells[idx] != opponent) continue;

                int libs = CountLiberties(ref state, x, y, opponent, maxCount: 3);
                if (libs != 2) continue;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    int nIdx = ny * size + nx;
                    if (state.Cells[nIdx] != Stone.Empty) continue;
                    if (nIdx == state.KoPoint) continue;
                    if (!IsLegalMove(ref state, nx, ny)) continue;
                    if (IsSelfAtari(ref state, nx, ny)) continue;

                    bx = nx; by = ny;
                    return true;
                }
            }
        }
        return false;
    }

    // ============================================================
    // SmartRandomMove
    // ============================================================
    public static bool SmartRandomMove(ref GameState state, System.Random rng,
    int lastX, int lastY, float localBias, MoveTechnique techniques, out int bx, out int by)
    {
        bx = -1; by = -1;
        int size = state.Size;
        int n = size * size;

        // 0. Спасение своей группы из atari.
        if ((techniques & MoveTechnique.SaveAtari) != 0)
        {
            if (FindAtariEscape(ref state, out int ex, out int ey))
            {
                bx = ex; by = ey;
                return true;
            }
        }

        // 1. Поставить противника в atari.
        if ((techniques & MoveTechnique.ThreatenAtari) != 0)
        {
            if (rng.NextDouble() < 0.7)
            {
                if (FindAtariThreat(ref state, out int tx, out int ty))
                {
                    bx = tx; by = ty;
                    return true;
                }
            }
        }

        // 2. Захват.
        if ((techniques & MoveTechnique.Capture) != 0)
        {
            if (rng.NextDouble() < 0.5)
            {
                if (FindCaptureMove(ref state, out int cx, out int cy))
                {
                    if (IsLegalMove(ref state, cx, cy))
                    {
                        bx = cx; by = cy;
                        return true;
                    }
                }
            }
        }

        // 3. Локальный ход в радиусе 2.
        if ((techniques & MoveTechnique.Local) != 0
            && lastX >= 0 && lastY >= 0 && rng.NextDouble() < localBias)
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
                    if (state.Cells[ny * size + nx] != Stone.Empty) continue;

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
                if (!IsLegalMove(ref state, x, y)) continue;
                if (IsSelfAtari(ref state, x, y)) continue;
                bx = x; by = y;
                return true;
            }
            for (int i = 0; i < c2; i++)
            {
                int idx = ring2[i];
                int x = idx % size, y = idx / size;
                if (!IsLegalMove(ref state, x, y)) continue;
                if (IsSelfAtari(ref state, x, y)) continue;
                bx = x; by = y;
                return true;
            }
        }

        // 4. Случайный ход с фильтром self-atari.
        if ((techniques & MoveTechnique.Random) != 0)
        {
            int attempts = 0;
            while (attempts < 20)
            {
                int idx = rng.Next(n);
                if (state.Cells[idx] != Stone.Empty) { attempts++; continue; }
                int x = idx % size;
                int y = idx / size;
                if (!IsLegalMove(ref state, x, y)) { attempts++; continue; }
                if (IsSelfAtari(ref state, x, y)) { attempts++; continue; }
                bx = x; by = y;
                return true;
            }
        }

        // 5. Полный перебор с фильтром self-atari.
        if ((techniques & MoveTechnique.FullScan) != 0)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (state.Cells[y * size + x] != Stone.Empty) continue;
                    if (!IsLegalMove(ref state, x, y)) continue;
                    if (IsSelfAtari(ref state, x, y)) continue;
                    bx = x; by = y;
                    return true;
                }
        }

        // 6. Любой легальный (крайний фолбэк).
        if ((techniques & MoveTechnique.AnyLegal) != 0)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (IsLegalMove(ref state, x, y))
                    {
                        bx = x; by = y;
                        return true;
                    }
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
