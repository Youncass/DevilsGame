using UnityEngine;

public static class GoScore
{
    public static float Komi(int boardSize) => boardSize switch
    {
        <= 9 => 5.5f,
        <= 13 => 6.5f,
        _ => 7.5f,
    };

    public static ScoreBreakdown ComputeBreakdown(ref GameState state)
    {
        int size = state.Size;
        int n = state.Cells.Length;
        Stone[] board = state.Cells;
        int[] queue = state.Buffers.Queue;
        int[] stamp = state.Buffers.VisitedStamp;
        float[] influence = state.InfluenceBuffer;

        int blackTerritory = 0;
        int whiteTerritory = 0;

        // ============================================================
        // 0. Atari — первым.
        // ============================================================
        int atariBlack = 0;
        int atariWhite = 0;

        state.Buffers.NextStamp();
        int cur = state.Buffers.CurrentStamp;
        for (int i = 0; i < n; i++)
        {
            if (board[i] == Stone.Empty || stamp[i] == cur) continue;

            Stone color = board[i];
            int head = 0, tail = 0;
            queue[tail++] = i;
            stamp[i] = cur;
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
                    else if (board[nIdx] == color && stamp[nIdx] != cur)
                    {
                        stamp[nIdx] = cur;
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
        state.Buffers.NextStamp();
        cur = state.Buffers.CurrentStamp;
        for (int i = 0; i < n; i++)
        {
            if (board[i] != Stone.Empty) continue;
            if (stamp[i] == cur) continue;

            int head = 0, tail = 0;
            queue[tail++] = i;
            stamp[i] = cur;
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
                    else if (stamp[nIdx] != cur)
                    {
                        stamp[nIdx] = cur;
                        queue[tail++] = nIdx;
                    }
                }
            }

            if (touchesBlack && !touchesWhite) blackTerritory += regionCount;
            else if (touchesWhite && !touchesBlack) whiteTerritory += regionCount;
        }

        // ============================================================
        // 2. Influence для спорных клеток (multi-source BFS от камней).
        // ============================================================
        for (int i = 0; i < n; i++)
        {
            if (board[i] != Stone.Empty) { influence[i] = 0f; continue; }
            influence[i] = float.NaN;
        }

        state.Buffers.NextStamp();
        cur = state.Buffers.CurrentStamp;
        int head2 = 0, tail2 = 0;
        for (int i = 0; i < n; i++)
        {
            if (board[i] == Stone.Empty) continue;
            queue[tail2++] = i;
            stamp[i] = cur;
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
                if (!float.IsNaN(influence[nIdx])) continue;
                if (stamp[nIdx] == cur) continue;

                stamp[nIdx] = cur;

                float prevDist = board[idx] != Stone.Empty ? 0f : Mathf.Abs(influence[idx]);
                float newDist = prevDist + 1f;
                float sign = stone == Stone.Black ? 1f : -1f;
                influence[nIdx] = sign * newDist;
                queue[tail2++] = nIdx;
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (board[i] != Stone.Empty) continue;
            if (float.IsNaN(influence[i])) continue;
            if (Mathf.Abs(influence[i]) == 1f) continue; // уже территория

            float v = influence[i];
            float dist = Mathf.Abs(v);
            float sign = Mathf.Sign(v);

            if (dist <= size / 3f)
            {
                if (sign > 0) blackTerritory += 1;
                else whiteTerritory += 1;
            }
        }

        return new ScoreBreakdown
        {
            BlackTerritory = blackTerritory,
            WhiteTerritory = whiteTerritory,
            BlackStones = state.BlackStones,
            WhiteStones = state.WhiteStones,
            CapturedBlack = state.CapturedBlack,
            CapturedWhite = state.CapturedWhite,
            AtariPenaltyBlack = atariBlack,
            AtariPenaltyWhite = atariWhite,
            Komi = Komi(size),
        };
    }

    public static float Evaluate(ref GameState state, Stone rootPlayer)
    {
        var b = ComputeBreakdown(ref state);
        return b.Diff(rootPlayer);
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
