using UnityEngine;

public static class GoScore
{
    public static float Komi(int boardSize) => boardSize switch
    {
        <= 9 => 5.5f,
        <= 13 => 6.5f,
        _ => 7.5f,
    };

    /// <summary>
    /// Вычисляет разбивку счёта. Использует generation-stamp для visited:
    /// visitedStamp[i] == currentStamp означает «посещено в текущем проходе».
    /// currentStamp инкрементируется внутри для каждого независимого BFS.
    /// </summary>
    public static ScoreBreakdown ComputeBreakdown(
        Stone[] board,
        int size,
        int blackStones,
        int whiteStones,
        int capturedBlack,
        int capturedWhite,
        int[] visitedStamp,
        int[] queue,
        float[] influenceBuffer)
    {
        int n = board.Length;
        // Обнуляем visited — гарантирует чистоту, не полагаемся на внешние метки.
        System.Array.Clear(visitedStamp, 0, n);
        int blackTerritory = 0;
        int whiteTerritory = 0;
        int stamp = 1;   // локальный счётчик поколений для этого вызова

        // ============================================================
        // 0. Atari — считаем ПЕРВЫМ.
        // ============================================================
        int atariBlack = 0;
        int atariWhite = 0;

        stamp++;
        for (int i = 0; i < n; i++)
        {
            if (board[i] == Stone.Empty || visitedStamp[i] == stamp) continue;

            Stone color = board[i];
            int head = 0, tail = 0;
            queue[tail++] = i;
            visitedStamp[i] = stamp;
            int groupSize = 0;
            int liberties = 0;

            while (head < tail)
            {
                int idx = queue[head++];
                groupSize++;
                int x = idx % size;
                int y = idx / size;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    int nIdx = ny * size + nx;

                    if (board[nIdx] == Stone.Empty) liberties++;
                    else if (board[nIdx] == color && visitedStamp[nIdx] != stamp)
                    {
                        visitedStamp[nIdx] = stamp;
                        queue[tail++] = nIdx;
                    }
                }
            }

            if (liberties <= 1)
            {
                if (color == Stone.Black) atariBlack += groupSize;
                else atariWhite += groupSize;
            }
        }

        // ============================================================
        // 1. Территория через BFS-регионы.
        // ============================================================
        stamp++;
        for (int i = 0; i < n; i++)
        {
            if (board[i] != Stone.Empty) { influenceBuffer[i] = 0f; continue; }
            if (visitedStamp[i] == stamp) continue;

            int head = 0, tail = 0;
            queue[tail++] = i;
            visitedStamp[i] = stamp;
            int regionCount = 0;
            bool touchesBlack = false;
            bool touchesWhite = false;

            while (head < tail)
            {
                int idx = queue[head++];
                regionCount++;
                int x = idx % size;
                int y = idx / size;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    int nIdx = ny * size + nx;

                    if (board[nIdx] == Stone.Black) touchesBlack = true;
                    else if (board[nIdx] == Stone.White) touchesWhite = true;
                    else if (visitedStamp[nIdx] != stamp)
                    {
                        visitedStamp[nIdx] = stamp;
                        queue[tail++] = nIdx;
                    }
                }
            }

            int ownerSign = 0;
            if (touchesBlack && !touchesWhite) { blackTerritory += regionCount; ownerSign = 1; }
            else if (touchesWhite && !touchesBlack) { whiteTerritory += regionCount; ownerSign = -1; }

            for (int k = 0; k < tail; k++)
                influenceBuffer[queue[k]] = ownerSign;
        }

        // ============================================================
        // 2. Influence для спорных клеток (multi-source BFS от камней).
        // ============================================================
        for (int i = 0; i < n; i++)
        {
            if (board[i] != Stone.Empty) { influenceBuffer[i] = 0f; continue; }
            if (influenceBuffer[i] != 0f) continue;
            influenceBuffer[i] = float.NaN;
        }

        stamp++;
        int head2 = 0, tail2 = 0;
        for (int i = 0; i < n; i++)
        {
            if (board[i] == Stone.Empty) continue;
            queue[tail2++] = i;
            visitedStamp[i] = stamp;
        }

        while (head2 < tail2)
        {
            int idx = queue[head2++];
            Stone stone = board[idx];
            int cx = idx % size;
            int cy = idx / size;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                int ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int nIdx = ny * size + nx;
                if (board[nIdx] != Stone.Empty) continue;
                if (float.IsNaN(influenceBuffer[nIdx]) == false) continue;
                if (visitedStamp[nIdx] == stamp) continue;

                visitedStamp[nIdx] = stamp;

                float prevDist = board[idx] != Stone.Empty ? 0f : Mathf.Abs(influenceBuffer[idx]);
                float newDist = prevDist + 1f;
                float sign = stone == Stone.Black ? 1f : -1f;
                influenceBuffer[nIdx] = sign * newDist;
                queue[tail2++] = nIdx;
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (board[i] != Stone.Empty) continue;
            if (float.IsNaN(influenceBuffer[i])) { influenceBuffer[i] = 0f; continue; }
            if (Mathf.Abs(influenceBuffer[i]) == 1f) continue;

            float v = influenceBuffer[i];
            float dist = Mathf.Abs(v);
            float sign = Mathf.Sign(v);

            if (dist <= size / 3f)
            {
                if (sign > 0) blackTerritory += 1;
                else whiteTerritory += 1;
            }
        }

        for (int i = 0; i < n; i++)
            if (float.IsNaN(influenceBuffer[i])) influenceBuffer[i] = 0f;

        return new ScoreBreakdown
        {
            BlackTerritory = blackTerritory,
            WhiteTerritory = whiteTerritory,
            BlackStones = blackStones,
            WhiteStones = whiteStones,
            CapturedBlack = capturedBlack,
            CapturedWhite = capturedWhite,
            AtariPenaltyBlack = atariBlack,
            AtariPenaltyWhite = atariWhite,
            Komi = Komi(size),
        };
    }

    public static float Evaluate(
        Stone[] board, int size, Stone player,
        int blackStones, int whiteStones,
        int capturedBlack, int capturedWhite,
        int[] visitedStamp, int[] queue, float[] influenceBuffer)
    {
        var b = ComputeBreakdown(board, size, blackStones, whiteStones,
                                 capturedBlack, capturedWhite,
                                 visitedStamp, queue, influenceBuffer);
        return b.Diff(player);
    }
}

public struct ScoreBreakdown
{
    public int BlackTerritory;
    public int WhiteTerritory;
    public int BlackStones;
    public int WhiteStones;
    public int CapturedBlack;
    public int CapturedWhite;
    public int AtariPenaltyBlack;
    public int AtariPenaltyWhite;
    public float Komi;

    private static float AtariPenalty(int stones)
    {
        if (stones <= 0) return 0f;
        return stones * (5f + 5f / Mathf.Sqrt(stones));
    }

    public float BlackTotal =>
        BlackTerritory + BlackStones + CapturedWhite - AtariPenalty(AtariPenaltyBlack);
    public float WhiteTotal =>
        WhiteTerritory + WhiteStones + CapturedBlack - AtariPenalty(AtariPenaltyWhite) + Komi;

    public float Diff(Stone player) => player == Stone.Black
        ? BlackTotal - WhiteTotal
        : WhiteTotal - BlackTotal;

    public override string ToString() =>
        $"B: terr={BlackTerritory} stones={BlackStones} cap={CapturedWhite} atari=-{AtariPenaltyBlack} " +
        $"(pen={AtariPenalty(AtariPenaltyBlack):0.0}) => {BlackTotal:0.0} | " +
        $"W: terr={WhiteTerritory} stones={WhiteStones} cap={CapturedBlack} atari=-{AtariPenaltyWhite} " +
        $"(pen={AtariPenalty(AtariPenaltyWhite):0.0}) komi={Komi:0.0} => {WhiteTotal:0.0}";
}
