using System;

/// <summary>Быстрый детерминированный ГПСЧ — System.Random слишком тяжёлый в rollout'ах.</summary>
public struct GoRng
{
    private uint _state;

    public GoRng(uint seed) { _state = seed == 0 ? 0x9E3779B9u : seed; }

    public uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x * 1664525u + 1013904223u;
    }

    public int Next(int max) => max <= 0 ? 0 : (int)(NextUInt() % (uint)max);

    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);
}

/// <summary>
/// Генерация кандидатов и политика разыгрывания. Вся статистика/память — в одном
/// экземпляре на поиск, чтобы симуляции не аллоцировали массивы.
/// </summary>
public sealed class GoMoveGenerator
{
    private readonly int[] _mark;
    private readonly int[] _local;
    private int _stamp;
    private int _localStamp;

    public GoMoveGenerator(int count)
    {
        _mark = new int[count];
        _local = new int[count];
    }

    /// <summary>
    /// Кандидаты для дерева: окрестность камней + пас. Массив уже отсортирован
    /// по приору по возрастанию — дерево берёт элементы с конца.
    /// </summary>
    public int Generate(
        ref GoPosition pos, int lastMove, Stone rootPlayer, GoConfig cfg,
        int[] moves, float[] priors)
    {
        int n = pos.Count;
        int size = pos.Geometry.Size;
        _stamp++;
        if (_stamp == int.MaxValue)
        {
            Array.Clear(_mark, 0, _mark.Length);
            _stamp = 1;
        }
        int stamp = _stamp;
        int radius = Math.Max(1, cfg.CandidateRadius);

        bool anyStone = false;
        for (int i = 0; i < n; i++)
        {
            if (pos.Cells[i] == Stone.Empty) continue;
            anyStone = true;
            MarkVicinity(pos, i, radius, stamp);
        }

        if (!anyStone)
        {
            // Дебют: не рассматриваем первую линию, тянемся к центру.
            int mid = size / 2;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (pos.Geometry.EdgeDistance[y * size + x] >= 1)
                        _mark[y * size + x] = stamp;
        }

        int count = 0;
        Stone me = pos.SideToMove;

        for (int idx = 0; idx < n; idx++)
        {
            if (pos.Cells[idx] != Stone.Empty || _mark[idx] != stamp) continue;
            if (cfg.UseEyeFilter && pos.IsEye(idx, me)) continue;
            if (!pos.IsLegal(idx)) continue;
            if (cfg.UseSelfAtariFilter && pos.IsSelfAtari(idx)) continue;

            moves[count] = idx;
            priors[count] = ComputePrior(ref pos, idx, lastMove, rootPlayer, cfg);
            count++;
        }

        // Пас — полноценный ход в дереве. Раньше бот не умел пасовать, поэтому
        // никогда не доводил партию до конца и «доигрывал» мёртвые группы.
        moves[count] = n;
        float passPrior = -cfg.PassPenalty;
        if (pos.EmptyCount <= n * cfg.PassEmptyRatio)
            passPrior += cfg.PassPenalty * 2f;
        priors[count] = passPrior;
        count++;

        Array.Sort(priors, moves, 0, count);
        return count;
    }

    private void MarkVicinity(GoPosition pos, int idx, int radius, int stamp)
    {
        int size = pos.Geometry.Size;
        int cx = idx % size;
        int cy = idx / size;
        int x0 = Math.Max(0, cx - radius), x1 = Math.Min(size - 1, cx + radius);
        int y0 = Math.Max(0, cy - radius), y1 = Math.Min(size - 1, cy + radius);
        for (int y = y0; y <= y1; y++)
        {
            int row = y * size;
            for (int x = x0; x <= x1; x++)
                _mark[row + x] = stamp;
        }
    }

    private float ComputePrior(
        ref GoPosition pos, int idx, int lastMove, Stone rootPlayer, GoConfig cfg)
    {
        int size = pos.Geometry.Size;
        int x = idx % size;
        int y = idx / size;
        float p = 0f;

        int edge = pos.Geometry.EdgeDistance[idx];
        if (edge == 0) p -= cfg.FirstLinePenalty;
        else if (edge == 1) p -= cfg.SecondLinePenalty;
        else p += cfg.CenterBonus * Math.Min(edge, 3) / 3f;

        float mid = (size - 1) * 0.5f;
        float centerDist = Math.Abs(x - mid) + Math.Abs(y - mid);
        p += Math.Max(0f, size * 0.5f - centerDist) * 0.6f;

        Stone me = pos.SideToMove;
        Stone opp = GoRules.Opp(me);
        int[] nb = pos.Geometry.Neighbors;
        int b = idx * 4;
        int ownAdj = 0, oppAdj = 0;

        // Один обход соседей: счётчик либерти берём с запасом 3, чтобы
        // отличить захват (1), угрозу (2) и спокойную группу (3+).
        for (int d = 0; d < 4; d++)
        {
            int ni = nb[b + d];
            if (ni < 0) continue;
            Stone v = pos.Cells[ni];
            if (v == me)
            {
                ownAdj++;
                if (pos.CountLiberties(ni, 2) == 1)
                    p += cfg.SaveAtariBonus;
            }
            else if (v == opp)
            {
                oppAdj++;
                int libs = pos.CountLiberties(ni, 3);
                if (libs == 1) p += cfg.CaptureBonus;
                else if (libs == 2) p += cfg.ThreatBonus;
            }
        }
        p += ownAdj * 2f + oppAdj * 3f;

        if (lastMove >= 0)
        {
            int lx = lastMove % size, ly = lastMove / size;
            int dist = Math.Max(Math.Abs(x - lx), Math.Abs(y - ly));
            p += Math.Max(0, 6 - dist) * cfg.LocalBonus;
        }

        return p;
    }

    /// <summary>
    /// Лёгкая политика разыгрывания: спасение/захват атари, локальный ход,
    /// случайный ход с фильтрами, пас как крайняя мера.
    /// Возвращает индекс хода либо pos.Count для паса.
    /// </summary>
    public int PickRolloutMove(
        ref GoPosition pos, int lastMove, ref GoRng rng, GoConfig cfg)
    {
        int n = pos.Count;
        Stone me = pos.SideToMove;
        Stone opp = GoRules.Opp(me);

        if (rng.NextFloat() < cfg.AtariSaveProbability)
        {
            int save = FindAtariLiberty(ref pos, me, lastMove, ref rng);
            if (save >= 0 && IsRolloutMoveGood(ref pos, save, cfg))
                return save;
        }

        if (rng.NextFloat() < cfg.CaptureProbability)
        {
            int capture = FindAtariLiberty(ref pos, opp, lastMove, ref rng);
            if (capture >= 0 && IsRolloutMoveGood(ref pos, capture, cfg))
                return capture;
        }

        if (lastMove >= 0 && rng.NextFloat() < cfg.LocalBias)
        {
            int size = pos.Geometry.Size;
            int lx = lastMove % size, ly = lastMove / size;
            _localStamp++;
            if (_localStamp == int.MaxValue)
            {
                Array.Clear(_local, 0, _local.Length);
                _localStamp = 1;
            }
            int stamp = _localStamp;
            Span<int> candidates = stackalloc int[25];
            int c = 0;
            for (int dy = -2; dy <= 2; dy++)
            {
                int yy = ly + dy;
                if (yy < 0 || yy >= size) continue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    int xx = lx + dx;
                    if (xx < 0 || xx >= size) continue;
                    int idx = yy * size + xx;
                    if (pos.Cells[idx] != Stone.Empty) continue;
                    if (_local[idx] == stamp) continue;
                    _local[idx] = stamp;
                    if (c < candidates.Length) candidates[c++] = idx;
                }
            }
            if (c > 0)
            {
                int start = rng.Next(c);
                for (int k = 0; k < c; k++)
                {
                    int idx = candidates[(start + k) % c];
                    if (IsRolloutMoveGood(ref pos, idx, cfg)) return idx;
                }
            }
        }

        int attempts = Math.Min(12, Math.Max(1, n / 4));
        for (int k = 0; k < attempts; k++)
        {
            int idx = rng.Next(n);
            if (IsRolloutMoveGood(ref pos, idx, cfg)) return idx;
        }

        return n; // пас
    }

    private bool IsRolloutMoveGood(ref GoPosition pos, int idx, GoConfig cfg)
    {
        if (idx < 0 || idx >= pos.Count) return false;
        if (pos.Cells[idx] != Stone.Empty || idx == pos.KoPoint) return false;
        if (!pos.IsLegal(idx)) return false;
        if (cfg.UseEyeFilter && pos.IsEye(idx, pos.SideToMove)) return false;
        if (cfg.UseSelfAtariFilter && pos.IsSelfAtari(idx)) return false;
        return true;
    }

    /// <summary>
    /// Ищем атари-группу без полного сканирования доски на каждом ходе rollout'а:
    /// случайная выборка + соседи последнего хода (именно там меняются либерти).
    /// Полный перебор здесь съедал бы ~80 BFS на каждый ход симуляции.
    /// </summary>
    private int FindAtariLiberty(ref GoPosition pos, Stone color, int lastMove, ref GoRng rng)
    {
        int n = pos.Count;
        int start = rng.Next(n);
        int span = Math.Min(n, 12);

        for (int k = 0; k < span; k++)
        {
            int lib = CheckAtariStone(ref pos, (start + k) % n, color);
            if (lib >= 0) return lib;
        }

        if (lastMove >= 0)
        {
            int[] nb = pos.Geometry.Neighbors;
            int b = lastMove * 4;
            for (int d = 0; d < 4; d++)
            {
                int ni = nb[b + d];
                if (ni < 0) continue;
                int lib = CheckAtariStone(ref pos, ni, color);
                if (lib >= 0) return lib;
            }
        }
        return -1;
    }

    private int CheckAtariStone(ref GoPosition pos, int idx, Stone color)
    {
        if (pos.Cells[idx] != color) return -1;
        return pos.GetSingleLiberty(idx); // один BFS: либерти ровно одна -> индекс, иначе -1
    }
}
