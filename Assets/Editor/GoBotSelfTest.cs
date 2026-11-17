#if UNITY_EDITOR
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
}
#endif
