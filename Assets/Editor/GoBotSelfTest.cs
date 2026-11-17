#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Быстрая проверка ядра бота прямо в редакторе: Go > Bot Self Test.</summary>
public static class GoBotSelfTest
{
    private static int _failed;

    [MenuItem("Go/Bot Self Test")]
    public static void Run()
    {
        _failed = 0;
        var cfg = ScriptableObject.CreateInstance<GoConfig>();

        TestCaptureAndKo();
        TestEye();
        TestSearch(cfg);
        TestDifficultyProfile();
        TestSkillProgression();
        TestStoneEvaluator(cfg);
        TestTutorialTriggers();

        Object.DestroyImmediate(cfg);
        Debug.Log(_failed == 0
            ? "[GoBotSelfTest] ALL PASSED"
            : $"[GoBotSelfTest] {_failed} FAILED");
    }

    private static void Check(bool ok, string label)
    {
        if (ok) Debug.Log("[GoBotSelfTest] PASS: " + label);
        else { _failed++; Debug.LogError("[GoBotSelfTest] FAIL: " + label); }
    }

    private static int Idx(int size, int x, int y) => y * size + x;

    private static void TestCaptureAndKo()
    {
        const int size = 9;
        var cells = new Stone[size * size];
        cells[Idx(size, 1, 2)] = Stone.Black;
        cells[Idx(size, 2, 1)] = Stone.Black;
        cells[Idx(size, 2, 3)] = Stone.Black;
        cells[Idx(size, 2, 2)] = Stone.White;
        cells[Idx(size, 4, 2)] = Stone.White;
        cells[Idx(size, 3, 1)] = Stone.White;
        cells[Idx(size, 3, 3)] = Stone.White;

        var pos = GoPosition.Create(cells, size, Stone.Black);
        Check(pos.Play(Idx(size, 3, 2), out int captured), "ko capture is legal");
        Check(captured == 1, "exactly one stone captured");
        Check(pos.KoPoint == Idx(size, 2, 2), "ko point set correctly");
        Check(!pos.IsLegal(Idx(size, 2, 2)), "immediate recapture forbidden");
        pos.Pass();
        Check(pos.IsLegal(Idx(size, 2, 2)), "recapture legal after pass");
    }

    private static void TestEye()
    {
        const int size = 5;
        var cells = new Stone[size * size];
        cells[Idx(size, 1, 2)] = Stone.Black;
        cells[Idx(size, 2, 1)] = Stone.Black;
        cells[Idx(size, 2, 3)] = Stone.Black;
        cells[Idx(size, 3, 2)] = Stone.Black;
        var pos = GoPosition.Create(cells, size, Stone.Black);
        Check(pos.IsEye(Idx(size, 2, 2), Stone.Black), "surrounded point is an eye");
    }

    private static void TestSearch(GoConfig cfg)
    {
        const int size = 9;
        var controller = new MCTSController(size, cfg);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = controller.Search(new Stone[size * size], Stone.Black, 200, -1);
        sw.Stop();

        Check(result.BestMove.x >= 0 && result.BestMove.x < size, "search returns a point on the board");
        Check(result.Simulations == 200, "simulation budget respected");
        Debug.Log($"[GoBotSelfTest] 200 sims in {sw.ElapsedMilliseconds} ms, best = {result.BestMove}");
    }

    private static void TestDifficultyProfile()
    {
        var profile = new GoDifficultyProfile();
        Check(profile.GetSimulations(0) == profile.TutorialSimulations, "tutorial uses tutorial simulation budget");
        Check(profile.GetSimulations(1) == profile.BaseSimulations, "level 1 uses base simulation budget");
        Check(profile.GetSimulations(20) == profile.MaxSimulations, "simulation budget is capped");
        Check(profile.GetSimulations(2) > profile.GetSimulations(1), "simulations grow with level");
        Check(profile.GetBlunderChance(0) > profile.GetBlunderChance(1), "tutorial blunders more often");
        Check(profile.ShouldRaiseTriggers(0) && profile.ShouldRaiseTriggers(1), "triggers active in tutorial and first ranked game");
        Check(!profile.ShouldRaiseTriggers(2), "triggers stop after first ranked game");
    }

    private static void TestSkillProgression()
    {
        const string prefix = "GoBotSelfTest.Skill";
        var tracker = new GoSkillTracker(prefix);
        tracker.ResetAll();

        tracker.ReportTutorialResult(true);
        Check(tracker.TutorialCompleted && tracker.Level == 1 && tracker.CompletedGames == 1,
            "tutorial completes and starts ranked games at level 1");

        tracker.ReportResult(false);
        Check(tracker.Level == 1 && tracker.HighestDefeatedLevel == 0,
            "early loss never goes below level 1");

        tracker.ReportResult(true);
        Check(tracker.HighestDefeatedLevel == 1, "win records the defeated level as a floor");

        tracker.ReportResult(true);
        Check(tracker.Level == 2, "two wins in recent games promote the player");

        tracker.ReportResult(false);
        tracker.ReportResult(false);
        Check(tracker.Level == 1 && tracker.HighestDefeatedLevel == 1,
            "losses reduce difficulty but stop at the defeated level");

        var loaded = new GoSkillTracker(prefix);
        loaded.Load();
        Check(loaded.Level == 1 && loaded.HighestDefeatedLevel == 1 && loaded.RankedGames == 5,
            "skill progression survives reload");
        loaded.ResetAll();
    }

    private static void TestStoneEvaluator(GoConfig cfg)
    {
        const int size = 5;

        var atariCells = new Stone[size * size];
        atariCells[Idx(size, 2, 2)] = Stone.White;
        atariCells[Idx(size, 1, 2)] = Stone.Black;
        atariCells[Idx(size, 3, 2)] = Stone.Black;
        atariCells[Idx(size, 2, 1)] = Stone.Black;
        var atari = GoPosition.Create(atariCells, size, Stone.Black);
        var atariValue = GoStoneEvaluator.Evaluate(ref atari, Idx(size, 2, 2), cfg);
        Check(atariValue.GroupSize == 1 && atariValue.Liberties == 1 && atariValue.InAtari,
            "evaluator detects a one-liberty group");
        Check(atariValue.Strength < 50f, "atari group is evaluated as weak");

        var eyeCells = BuildTwoEyesBoard(size);
        var eyes = GoPosition.Create(eyeCells, size, Stone.White);
        var eyeValue = GoStoneEvaluator.Evaluate(ref eyes, Idx(size, 2, 1), cfg);
        Check(eyeValue.HasTwoEyes, "evaluator detects two eyes");
        Check(eyeValue.Strength > 50f, "alive group is evaluated as strong");
        Check(GoStoneEvaluator.TryFindTwoEyes(ref eyes, Idx(size, 2, 1), out int eyeA, out int eyeB) &&
              eyeA >= 0 && eyeB >= 0 && eyeA != eyeB,
            "two independent eyes are returned");
    }

    private static void TestTutorialTriggers()
    {
        const int size = 5;
        var triggers = new GoTutorialTriggers(new GoDifficultyProfile());
        var seen = new List<GoTutorialEventId>();
        triggers.Raised += e => seen.Add(e.Id);

        triggers.BeginGame(0);
        var cells = new Stone[size * size];
        cells[Idx(size, 2, 2)] = Stone.Black;
        triggers.OnMove(cells, size, -1, null, PieceColor.Black, new Vector2Int(2, 2), null);
        Check(triggers.Fired.Contains(GoTutorialEventId.FirstStone), "first-stone trigger fires");

        cells[Idx(size, 3, 2)] = Stone.Black;
        triggers.OnMove(cells, size, -1, null, PieceColor.Black, new Vector2Int(3, 2), null);
        Check(triggers.Fired.Contains(GoTutorialEventId.Connection), "connection trigger fires");

        triggers.OnMove(cells, size, -1, null, PieceColor.Black, new Vector2Int(3, 2),
            new[] { new Vector2Int(4, 4) });
        Check(triggers.Fired.Contains(GoTutorialEventId.Capture), "capture trigger fires");

        triggers.OnMove(cells, size, Idx(size, 4, 4), null, PieceColor.Black, new Vector2Int(3, 2), null);
        Check(triggers.Fired.Contains(GoTutorialEventId.Ko), "ko trigger fires");

        var atariCells = new Stone[size * size];
        atariCells[Idx(size, 2, 2)] = Stone.White;
        atariCells[Idx(size, 1, 2)] = Stone.Black;
        atariCells[Idx(size, 3, 2)] = Stone.Black;
        atariCells[Idx(size, 2, 1)] = Stone.Black;
        triggers.OnMove(atariCells, size, -1, null, PieceColor.White, new Vector2Int(2, 2), null);
        Check(triggers.Fired.Contains(GoTutorialEventId.Atari), "atari trigger fires");

        triggers.OnMove(BuildTwoEyesBoard(size), size, -1, null, PieceColor.White, new Vector2Int(2, 1), null);
        Check(triggers.Fired.Contains(GoTutorialEventId.TwoEyes), "two-eyes trigger fires");

        triggers.OnGameOver(new ScoreBreakdown { WhiteTerritory = 8, BlackTerritory = 4 }, true);
        Check(triggers.Fired.Contains(GoTutorialEventId.GameOver), "game-over trigger fires");

        seen.Clear();
        triggers.BeginGame(2);
        var freshCells = new Stone[size * size];
        triggers.OnMove(freshCells, size, -1, null, PieceColor.White, new Vector2Int(2, 2), null);
        Check(seen.Count == 0, "triggers are disabled after the first ranked game");
    }

    private static Stone[] BuildTwoEyesBoard(int size)
    {
        var cells = new Stone[size * size];
        int[] white = {
            Idx(size, 1, 0), Idx(size, 2, 0), Idx(size, 3, 0),
            Idx(size, 0, 1), Idx(size, 2, 1), Idx(size, 4, 1),
            Idx(size, 0, 2), Idx(size, 1, 2), Idx(size, 2, 2), Idx(size, 3, 2), Idx(size, 4, 2),
        };
        for (int i = 0; i < white.Length; i++) cells[white[i]] = Stone.White;
        return cells;
    }
}
#endif
