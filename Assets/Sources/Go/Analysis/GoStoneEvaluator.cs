using System;

/// <summary>
/// Оценка отдельного камня/группы и распознавание базовых форм. Используется
/// tutorial-триггерами; в поиске намеренно НЕ вызывается — там считают более
/// дешёвые эвристики из GoPolicy.
/// </summary>
public struct GoStoneValue
{
    public Stone Color;
    public int GroupSize;
    public int Liberties;
    public int Influence;
    public bool InAtari;
    public bool HasTwoEyes;
    public float Value;     // «ценность»: сколько стоит группа/территория
    public float Strength;  // «сила»: насколько её сложно захватить

    public override string ToString() =>
        $"{Color}: stones={GroupSize} lib={Liberties} inf={Influence} " +
        $"atari={InAtari} eyes={HasTwoEyes} value={Value:0.0} strength={Strength:0.0}";
}

public static class GoStoneEvaluator
{
    /// <summary>Оценить камень и его группу. Чем выше Value/Strength, тем лучше.</summary>
    public static GoStoneValue Evaluate(ref GoPosition pos, int idx, GoConfig cfg = null)
    {
        var result = new GoStoneValue();
        if (idx < 0 || idx >= pos.Count || pos.Cells[idx] == Stone.Empty)
            return result;

        result.Color = pos.Cells[idx];
        var group = new int[pos.Count];
        var liberties = new int[pos.Count];
        pos.GetGroupInfo(idx, group, liberties, out int groupCount, out int libertyCount);

        result.GroupSize = groupCount;
        result.Liberties = libertyCount;
        result.InAtari = libertyCount == 1;
        result.HasTwoEyes = TryFindTwoEyes(ref pos, idx, out _, out _);

        // Влияние: пустые точки в радиусе 2 от камней группы.
        int size = pos.Geometry.Size;
        var seen = new bool[pos.Count];
        int influence = 0;
        for (int i = 0; i < groupCount; i++)
        {
            int gx = group[i] % size;
            int gy = group[i] / size;
            for (int dy = -2; dy <= 2; dy++)
            {
                int y = gy + dy;
                if (y < 0 || y >= size) continue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = gx + dx;
                    if (x < 0 || x >= size) continue;
                    int cell = y * size + x;
                    if (seen[cell] || pos.Cells[cell] != Stone.Empty) continue;
                    seen[cell] = true;
                    influence++;
                }
            }
        }
        result.Influence = influence;

        float libertyScore = libertyCount >= 4 ? 1f : libertyCount / 4f;
        float eyeScore = result.HasTwoEyes ? 1f : 0f;
        float atariPenalty = result.InAtari ? 1f : 0f;

        float captureBonus = cfg != null ? cfg.CaptureBonus : 40f;
        result.Value = Clamp(groupCount * 3f + influence * 0.8f + eyeScore * 25f - atariPenalty * 6f, 0f, 100f);
        result.Strength = Clamp(
            libertyScore * 45f + eyeScore * 50f - atariPenalty * 40f + groupCount * 1.5f - captureBonus * 0.15f,
            0f, 100f);

        return result;
    }

    /// <summary>Два настоящих глаза у группы содержащего камня idx.</summary>
    public static bool TryFindTwoEyes(ref GoPosition pos, int idx, out int eyeA, out int eyeB)
    {
        eyeA = eyeB = -1;
        if (idx < 0 || idx >= pos.Count || pos.Cells[idx] == Stone.Empty)
            return false;

        Stone color = pos.Cells[idx];
        var liberties = new int[pos.Count];
        pos.GetGroupInfo(idx, null, liberties, out _, out int libertyCount);

        int found = 0;
        int[] neighbors = pos.Geometry.Neighbors;
        for (int i = 0; i < libertyCount; i++)
        {
            int candidate = liberties[i];
            if (!pos.IsEye(candidate, color)) continue;

            // Два соседних пустых пункта — один большой глаз, а не два.
            if (found > 0 && AreAdjacent(neighbors, candidate, eyeA)) continue;

            if (found == 0) eyeA = candidate;
            else { eyeB = candidate; return true; }
            found++;
        }
        return false;
    }

    /// <summary>
    /// Крупнейший пустой район, граничащий только с одним цветом. Возвращает size,
    /// а сам район (при необходимости) кладёт в regionCells.
    /// </summary>
    public static int FindLargestTerritory(ref GoPosition pos, Stone owner, int minSize, int[] regionCells = null)
    {
        if (minSize < 1) minSize = 1;
        int[] nb = pos.Geometry.Neighbors;
        int[] mark = pos.GroupMark;
        int[] queue = pos.Queue;
        int stamp = pos.NextStamp();

        int best = 0;
        var bestRegion = new int[pos.Count];
        var currentRegion = new int[pos.Count];

        for (int start = 0; start < pos.Count; start++)
        {
            if (pos.Cells[start] != Stone.Empty || mark[start] == stamp) continue;

            int head = 0, tail = 0;
            queue[tail++] = start;
            mark[start] = stamp;

            int regionSize = 0;
            bool touchesOther = false;
            bool touchesOwner = false;

            while (head < tail)
            {
                int cell = queue[head++];
                regionSize++;
                currentRegion[regionSize - 1] = cell;

                int b = cell * 4;
                for (int d = 0; d < 4; d++)
                {
                    int ni = nb[b + d];
                    if (ni < 0) continue;
                    Stone v = pos.Cells[ni];
                    if (v == Stone.Empty)
                    {
                        if (mark[ni] == stamp) continue;
                        mark[ni] = stamp;
                        queue[tail++] = ni;
                    }
                    else if (v == owner) touchesOwner = true;
                    else touchesOther = true;
                }
            }

            if (regionSize > best && touchesOwner && !touchesOther && regionSize >= minSize)
            {
                best = regionSize;
                Array.Copy(currentRegion, 0, bestRegion, 0, regionSize);
            }
        }

        if (regionCells != null && best > 0)
            Array.Copy(bestRegion, 0, regionCells, 0, best);

        return best;
    }

    private static bool AreAdjacent(int[] neighbors, int a, int b)
    {
        if (b < 0) return false;
        int baseIndex = a * 4;
        for (int d = 0; d < 4; d++)
            if (neighbors[baseIndex + d] == b) return true;
        return false;
    }

    private static float Clamp(float value, float min, float max) =>
        value < min ? min : value > max ? max : value;
}
