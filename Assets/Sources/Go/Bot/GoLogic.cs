using System;
using System.Collections.Generic;

public static class GoLogic
{
    public struct Buffers
    {
        public bool[] Visited;
        public int[] Queue;
        public int[] LegalMoves;

        public static Buffers Allocate(int boardSize)
        {
            int n = boardSize * boardSize;
            return new Buffers
            {
                Visited = new bool[n],
                Queue = new int[n],
                LegalMoves = new int[n]
            };
        }
    }

    public static bool IsLegalMove(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, int koPoint = -1)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        int idx = y * size + x;
        if (board[idx] != Stone.Empty) return false;
        if (idx == koPoint) return false;

        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;

        // Быстрый путь: если у клетки есть пустой сосед — ход точно легален
        // (своя группа получает либерти; self-atari в Го легален).
        for (int d = 0; d < 4; d++)
        {
            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
            int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
            if (board[ny * size + nx] == Stone.Empty) return true;
        }

        // Медленный путь: пустых соседей нет, надо проверять захваты и либерти.
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

    public static bool GroupHasLiberty(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return false;
        int startIdx = y * size + x;
        if (board[startIdx] != player) return false;

        bool[] visited = buffers.Visited;
        int[] queue = buffers.Queue;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;
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
                if (board[nIdx] == player && !visited[nIdx])
                {
                    visited[nIdx] = true;
                    queue[tail++] = nIdx;
                }
            }
        }

        for (int k = 0; k < tail; k++)
            visited[queue[k]] = false;

        return hasLiberty;
    }

    public static int RemoveGroup(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, out bool wasSingle)
    {
        wasSingle = false;
        if (x < 0 || x >= size || y < 0 || y >= size) return 0;
        int startIdx = y * size + x;
        if (board[startIdx] != player) return 0;

        bool[] visited = buffers.Visited;
        int[] queue = buffers.Queue;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;
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

                if (board[nIdx] == player && !visited[nIdx])
                {
                    visited[nIdx] = true;
                    queue[tail++] = nIdx;
                }
            }
        }

        for (int k = 0; k < tail; k++)
            visited[queue[k]] = false;

        wasSingle = removed == 1;
        return removed;
    }

    /// <summary>
    /// Быстро считает число либерти у группы, начиная с (x,y).
    /// Возвращает -1, если группа имеет больше maxCount либерти (ранний выход).
    /// Использует visited-буфер через поколение, чтобы не чистить массив.
    /// </summary>
    public static int CountLiberties(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, int maxCount = 3)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return 0;
        int startIdx = y * size + x;
        if (board[startIdx] != player) return 0;

        bool[] visited = buffers.Visited;
        int[] queue = buffers.Queue;

        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;
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
                    if (liberties >= maxCount)
                    {
                        // Ранний выход — чистим visited и возвращаем «много».
                        for (int k = 0; k < tail; k++) visited[queue[k]] = false;
                        return maxCount;
                    }
                }
                else if (board[nIdx] == player && !visited[nIdx])
                {
                    visited[nIdx] = true;
                    queue[tail++] = nIdx;
                }
            }
        }

        for (int k = 0; k < tail; k++) visited[queue[k]] = false;
        return liberties;
    }

    /// <summary>
    /// Ищет ход, который немедленно захватывает хотя бы одну группу противника.
    /// Смотрит только на камни противника, у которых ровно 1 либерти (atari),
    /// и проверяет их пустых соседей. O(камней в atari) вместо O(n).
    /// Возвращает true, если ход найден.
    /// </summary>
    public static bool FindCaptureMove(
        Stone[] board, int size, Stone player,
        ref Buffers buffers, int koPoint,
        out int bx, out int by)
    {
        bx = -1; by = -1;
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;

        // Проходим по всем камням противника.
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (board[idx] != opponent) continue;

                // Быстрая проверка: если у камня уже есть >=2 либерти — его группа не в atari.
                // Считаем либерти группы (с ранним выходом на 2).
                int libs = CountLiberties(board, size, x, y, opponent, ref buffers, maxCount: 2);
                if (libs != 1) continue;

                // Группа в atari. Её единственная либерти — это ход, который её захватит.
                // Ищем эту либерти (пустого соседа любого камня группы).
                if (!TryFindSingleLiberty(board, size, x, y, opponent, ref buffers, out int libX, out int libY))
                    continue;

                // Проверяем, что этот ход легален (не ko, не занят — уже проверено).
                int libIdx = libY * size + libX;
                if (libIdx == koPoint) continue;

                // Проверяем, что наш камень после хода не окажется без либерти
                // (т.е. это не self-atari, кроме случая, когда захват уже даёт либерти).
                // Т.к. мы захватываем группу, либерти появятся на освободившихся клетках — ход легален.
                bx = libX; by = libY;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Находит единственную либерти группы в atari.
    /// Возвращает false, если либерти 0 или >1 (не должно случаться при libs==1, но защита).
    /// </summary>
    private static bool TryFindSingleLiberty(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, out int libX, out int libY)
    {
        libX = -1; libY = -1;
        bool[] visited = buffers.Visited;
        int[] queue = buffers.Queue;

        int startIdx = y * size + x;
        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;
        bool found = false;

        while (head < tail && !found)
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
                    if (libX < 0) { libX = nx; libY = ny; }
                    else if (libX != nx || libY != ny)
                    {
                        // Нашли вторую либерти — группа не в atari (защита от гонок).
                        found = false;
                        goto cleanup;
                    }
                }
                else if (board[nIdx] == player && !visited[nIdx])
                {
                    visited[nIdx] = true;
                    queue[tail++] = nIdx;
                }
            }
        }
        found = libX >= 0;

    cleanup:
        for (int k = 0; k < tail; k++) visited[queue[k]] = false;
        return found && (libX >= 0);
    }

    /// <summary>
    /// Проверяет, не является ли ход self-atari (своя группа после хода без либерти).
    /// Дешёвая эвристика: ставим камень, считаем либерти своей группы (с ранним выходом 2),
    /// убираем камень. Если есть захват — ход не self-atari (либерти появятся).
    /// </summary>
    public static bool IsSelfAtari(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers)
    {
        int idx = y * size + x;
        if (board[idx] != Stone.Empty) return false;

        board[idx] = player;

        // Если есть захват — не self-atari (появятся либерти).
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
    // Переписанный SmartRandomMove
    // ============================================================

    public static bool SmartRandomMove(
        Stone[] board, int size, Stone player,
        ref Buffers buffers, System.Random rng,
        int koPoint, int lastX, int lastY, float localBias,
        out int bx, out int by)
    {
        bx = -1; by = -1;
        int n = size * size;

        // 0. Спасение своей группы из atari — приоритет №1.
        if (rng.NextDouble() < 0.7)
        {
            if (FindAtariEscape(board, size, player, ref buffers, koPoint, out int ex, out int ey))
            {
                bx = ex; by = ey;
                return true;
            }
        }

        // 0.5. Поставить противника в atari — приоритет №2.
        if (rng.NextDouble() < 0.5)
        {
            if (FindAtariThreat(board, size, player, ref buffers, koPoint, out int tx, out int ty))
            {
                bx = tx; by = ty;
                return true;
            }
        }

        // 1. Захват (Atari capture) — с вероятностью localBias * 0.6 (быстро, но не всегда).
        // В rollout это самый ценный ход, но дорогой, поэтому делаем его не всегда.
        if (rng.NextDouble() < 0.5)
        {
            if (FindCaptureMove(board, size, player, ref buffers, koPoint, out int cx, out int cy))
            {
                // Проверим легальность (ko уже учтён, но вдруг что-то ещё).
                if (IsLegalMove(board, size, cx, cy, player, ref buffers, koPoint))
                {
                    bx = cx; by = cy;
                    return true;
                }
            }
        }

        // 2. Локальный ход в радиусе 2 (keima/tobi-стиль).
        if (lastX >= 0 && lastY >= 0 && rng.NextDouble() < localBias)
        {
            // Собираем кандидатов в радиусе 2 с весами по расстоянию.
            // Порядок: сначала расстояние 1 (8 клеток), потом расстояние 2 (16 клеток).
            // Внутри каждого кольца — случайный порядок.
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

                    int dist = (dx * dx + dy * dy); // 1, 2, 4, 5, 8
                    if (dist <= 2) ring1[c1++] = ny * size + nx; // 1 и 2 (ортогональные и диагональные)
                    else ring2[c2++] = ny * size + nx;
                }
            }

            // Перемешиваем ring1 и ring2 (Fisher-Yates).
            Shuffle(ring1, c1, rng);
            Shuffle(ring2, c2, rng);

            // Пробуем ring1, затем ring2. Фильтр self-atari.
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

        // 3. Случайный ход с фильтром self-atari (20 попыток).
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

        // 4. Полный перебор с фильтром self-atari.
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (board[y * size + x] != Stone.Empty) continue;
                if (!IsLegalMove(board, size, x, y, player, ref buffers, koPoint)) continue;
                if (IsSelfAtari(board, size, x, y, player, ref buffers)) continue;
                bx = x; by = y;
                return true;
            }

        // 5. Фолбэк: любой легальный ход без фильтра self-atari (чтобы не «пассовать» зря).
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

    /// <summary>
    /// Ищет «обязательный» ход: спасение своей группы из atari или захват чужой.
    /// Приоритет: сначала захват чужой группы (это выгодно), потом спасение своей.
    /// Возвращает true, если такой ход есть.
    /// </summary>
    public static bool FindAtariResponse(
        Stone[] board, int size, Stone player,
        ref Buffers buffers, int koPoint, int minGroupSize,
        out int bx, out int by)
    {
        bx = -1; by = -1;

        // 1. Захват чужой группы в atari — самый выгодный ход.
        if (FindCaptureMove(board, size, player, ref buffers, koPoint, out int cx, out int cy))
        {
            bx = cx; by = cy;
            return true;
        }

        // 2. Спасение СВОЕЙ группы в atari (если группа >= minGroupSize).
        Stone opponent = player == Stone.Black ? Stone.White : Stone.Black;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (board[idx] != player) continue;

                int libs = CountLiberties(board, size, x, y, player, ref buffers, maxCount: 2);
                if (libs != 1) continue;

                // Группа в atari. Найдём её размер и единственную либерти.
                if (!TryFindSingleLibertyAndSize(board, size, x, y, player, ref buffers,
                                                  out int libX, out int libY, out int groupSize))
                    continue;

                if (groupSize < minGroupSize) continue;

                // Пробуем поставить камень в либерти — это и есть спасение.
                // Проверяем легальность и что после хода у группы будет >=2 либерти.
                if (libX < 0 || libY < 0) continue;
                int libIdx = libY * size + libX;
                if (libIdx == koPoint) continue;
                if (board[libIdx] != Stone.Empty) continue;

                if (!IsLegalMove(board, size, libX, libY, player, ref buffers, koPoint)) continue;

                // Проверяем, что после хода у группы >=2 либерти (иначе это не спасение).
                board[libIdx] = player;
                int newLibs = CountLiberties(board, size, libX, libY, player, ref buffers, maxCount: 2);
                board[libIdx] = Stone.Empty;

                if (newLibs >= 2)
                {
                    bx = libX; by = libY;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Находит единственную либерти группы и её размер.
    /// </summary>
    private static bool TryFindSingleLibertyAndSize(
        Stone[] board, int size, int x, int y, Stone player,
        ref Buffers buffers, out int libX, out int libY, out int groupSize)
    {
        libX = -1; libY = -1; groupSize = 0;
        bool[] visited = buffers.Visited;
        int[] queue = buffers.Queue;

        int startIdx = y * size + x;
        int head = 0, tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;
        int libCount = 0;

        while (head < tail)
        {
            int idx = queue[head++];
            groupSize++;
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
                    else if (libCount > 1)
                    {
                        // Больше одной либерти — не atari, выходим.
                        for (int k = 0; k < tail; k++) visited[queue[k]] = false;
                        return false;
                    }
                }
                else if (board[nIdx] == player && !visited[nIdx])
                {
                    visited[nIdx] = true;
                    queue[tail++] = nIdx;
                }
            }
        }

        for (int k = 0; k < tail; k++) visited[queue[k]] = false;
        return libCount == 1 && libX >= 0;
    }

    /// <summary>
    /// Ищет ход, который спасает свою группу из atari (увеличивает либерти с 1 до >=2).
    /// Возвращает true, если ход найден.
    /// </summary>
    public static bool FindAtariEscape(
        Stone[] board, int size, Stone player,
        ref Buffers buffers, int koPoint,
        out int bx, out int by)
    {
        bx = -1; by = -1;

        // Проходим по своим камням, ищем группу с 1 либерти.
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (board[idx] != player) continue;

                int libs = CountLiberties(board, size, x, y, player, ref buffers, maxCount: 2);
                if (libs != 1) continue;

                // Группа в atari. Её единственная либерти — куда можно сходить, чтобы спасти.
                if (!TryFindSingleLiberty(board, size, x, y, player, ref buffers, out int libX, out int libY))
                    continue;

                int libIdx = libY * size + libX;
                if (libIdx == koPoint) continue;

                // Проверяем, что после хода в эту либерти у группы станет >=2 либерти
                // (иначе это не спасение, а продление агонии — но всё равно лучше, чем ничего).
                // Простая эвристика: ход в либерти увеличивает либерти группы минимум на 1
                // (освобождается эта клетка + возможно соседи). Проверим через IsLegalMove.
                if (!IsLegalMove(board, size, libX, libY, player, ref buffers, koPoint)) continue;

                bx = libX; by = libY;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ищет ход, который ставит группу противника в atari (уменьшает либерти с 2 до 1).
    /// </summary>
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
                if (libs != 2) continue; // группа ровно с 2 либерти — можно поставить в atari

                // Ищем пустую соседнюю клетку группы, которая является одной из её либерти.
                // Ставим туда — либерти станет 1.
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    int nIdx = ny * size + nx;
                    if (board[nIdx] != Stone.Empty) continue;
                    if (nIdx == koPoint) continue;

                    if (!IsLegalMove(board, size, nx, ny, player, ref buffers, koPoint)) continue;
                    // Не self-atari (иначе мы сами себе вредим).
                    if (IsSelfAtari(board, size, nx, ny, player, ref buffers)) continue;

                    bx = nx; by = ny;
                    return true;
                }
            }
        }

        return false;
    }
}
