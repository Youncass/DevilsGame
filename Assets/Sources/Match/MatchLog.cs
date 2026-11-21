using UnityEngine;

/// <summary>Все сообщения партии в одном месте, чтобы логика не тонула в Debug.Log.</summary>
public static class MatchLog
{
    public static void NewGame(int gameNumber, bool tutorial, int level, int simulations) =>
        Debug.Log($"[Go] New game #{gameNumber} tutorial={tutorial} level={level} sims={simulations}");

    public static void Move(string who, Vector2Int move) =>
        Debug.Log($"[{who}] move={move}");

    public static void Pass(string who, int count) =>
        Debug.Log($"[{who}] pass ({count}/2)");

    public static void IllegalBotMove(Vector2Int move) =>
        Debug.LogError($"[Go] Bot returned illegal move {move}. Fallback to any legal move.");

    public static void Fallback(Vector2Int move) =>
        Debug.Log($"[Go] Fallback move={move}");

    public static void BotMove(MoveSearchResult result, long elapsedMs)
    {
        float second = 0f, worst = 0f;
        Vector2Int secondMove = default, worstMove = default;
        bool hasSecond = false, hasWorst = false;

        for (int i = 0; i < result.AllMoves.Count; i++)
        {
            var candidate = result.AllMoves[i];
            if (candidate.visits < 10 || candidate.move == result.BestMove) continue;

            if (!hasSecond || candidate.winRate > second)
            {
                second = candidate.winRate;
                secondMove = candidate.move;
                hasSecond = true;
            }

            if (!hasWorst || candidate.winRate < worst)
            {
                worst = candidate.winRate;
                worstMove = candidate.move;
                hasWorst = true;
            }
        }

        Debug.Log(
            $"[Bot] move={result.BestMove} score={result.BestWinRate:+0.00;-0.00;0.00} " +
            $"sims={result.Simulations} totalLegal={result.LegalMoveCount} time={elapsedMs}ms\n" +
            $"      2nd={(hasSecond ? $"{secondMove} wr={second:+0.00;-0.00;0.00}" : "—")} " +
            $"worst={(hasWorst ? $"{worstMove} wr={worst:+0.00;-0.00;0.00}" : "—")}");
    }

    public static void Score(ScoreBreakdown score, PieceColor playerColor, string label) =>
        Debug.Log(
            $"[Score:{label}] {score}\n" +
            $"        diff({playerColor})={score.Diff(ToStone(playerColor)):+0.0;-0.0;0.0}");

    public static void GameOver(
        bool playerWon, bool tutorial, int levelBefore, int levelAfter, int nextSimulations) =>
        Debug.Log(
            $"[Go] Game over (two passes). playerWon={playerWon} tutorial={tutorial} " +
            $"level {levelBefore} -> {levelAfter}; next sims={nextSimulations}");

    public static void Tutorial(GoTutorialEvent e) =>
        Debug.Log($"[Tutorial #{e.GameNumber}] {e.Title}: {e.Message}");

    private static Stone ToStone(PieceColor c) =>
        c == PieceColor.Black ? Stone.Black : Stone.White;
}
