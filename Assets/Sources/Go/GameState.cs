public struct GameState
{
    public Stone[] Cells;
    public int Size;
    public Stone PlayerToMove;
    public int KoPoint;
    public int CapturedBlack;
    public int CapturedWhite;
    public int BlackStones;
    public int WhiteStones;

    public GoLogic.Buffers Buffers;
    public float[] InfluenceBuffer;

    // === Undo-лог ===
    public int[] UndoIndices;      // индексы изменённых клеток
    public Stone[] UndoOldStones;  // старые значения
    public int UndoCount;          // сколько записей использовано
    public int UndoSavedKoPoint;
    public Stone UndoSavedPlayerToMove;
    public int UndoSavedCapturedBlack;
    public int UndoSavedCapturedWhite;
    public int UndoSavedBlackStones;
    public int UndoSavedWhiteStones;

    public int BoardCells => Size * Size;

    public static GameState Create(Stone[] cells, int size, Stone playerToMove)
    {
        int n = size * size;
        int b = 0, w = 0;
        for (int i = 0; i < n; i++)
        {
            if (cells[i] == Stone.Black) b++;
            else if (cells[i] == Stone.White) w++;
        }

        // Undo-лог: должен покрывать ВСЮ симуляцию (путь + rollout + захваты).
        // Путь дерева ≤ n, rollout ≤ n + 20, захваты на каждом ходу ≤ n.
        // С запасом: 3 * n + 32.
        int undoCapacity = 3 * n + 32;

        return new GameState
        {
            Cells = cells,
            Size = size,
            PlayerToMove = playerToMove,
            KoPoint = -1,
            CapturedBlack = 0,
            CapturedWhite = 0,
            BlackStones = b,
            WhiteStones = w,
            Buffers = GoLogic.Buffers.Allocate(size),
            InfluenceBuffer = new float[n],
            UndoIndices = new int[undoCapacity],
            UndoOldStones = new Stone[undoCapacity],
            UndoCount = 0,
        };
    }

    /// <summary>
    /// Логирует изменение клетки перед ApplyMove. Вызывается из GoLogic.
    /// </summary>
    public void LogCell(int idx)
    {
        UndoIndices[UndoCount] = idx;
        UndoOldStones[UndoCount] = Cells[idx];
        UndoCount++;
    }

    /// <summary>
    /// Сохраняет скаляры перед ApplyMove.
    /// </summary>
    public void SaveScalars()
    {
        UndoSavedKoPoint = KoPoint;
        UndoSavedPlayerToMove = PlayerToMove;
        UndoSavedCapturedBlack = CapturedBlack;
        UndoSavedCapturedWhite = CapturedWhite;
        UndoSavedBlackStones = BlackStones;
        UndoSavedWhiteStones = WhiteStones;
    }

    /// <summary>
    /// Откатывает последний ход (всё, что залогировано с последнего SaveScalars).
    /// </summary>
    public void UndoLastMove()
    {
        for (int i = UndoCount - 1; i >= 0; i--)
            Cells[UndoIndices[i]] = UndoOldStones[i];
        UndoCount = 0;

        KoPoint = UndoSavedKoPoint;
        PlayerToMove = UndoSavedPlayerToMove;
        CapturedBlack = UndoSavedCapturedBlack;
        CapturedWhite = UndoSavedCapturedWhite;
        BlackStones = UndoSavedBlackStones;
        WhiteStones = UndoSavedWhiteStones;
    }

    public GameState Clone()
    {
        return new GameState
        {
            Cells = (Stone[])Cells.Clone(),
            Size = Size,
            PlayerToMove = PlayerToMove,
            KoPoint = KoPoint,
            CapturedBlack = CapturedBlack,
            CapturedWhite = CapturedWhite,
            BlackStones = BlackStones,
            WhiteStones = WhiteStones,
            Buffers = new GoLogic.Buffers
            {
                VisitedStamp = (int[])Buffers.VisitedStamp.Clone(),
                Queue = (int[])Buffers.Queue.Clone(),
                CurrentStamp = Buffers.CurrentStamp,
            },
            InfluenceBuffer = new float[InfluenceBuffer.Length],
            UndoIndices = new int[UndoIndices.Length],
            UndoOldStones = new Stone[UndoOldStones.Length],
            UndoCount = 0,
        };
    }

    /// <summary>
    /// Снимок для сохранения в узле дерева. Копирует только Cells;
    /// Buffers/InfluenceBuffer/Undo — shared (узел только читает).
    /// </summary>
    public GameState SnapshotForNode()
    {
        return new GameState
        {
            Cells = (Stone[])Cells.Clone(),
            Size = Size,
            PlayerToMove = PlayerToMove,
            KoPoint = KoPoint,
            CapturedBlack = CapturedBlack,
            CapturedWhite = CapturedWhite,
            BlackStones = BlackStones,
            WhiteStones = WhiteStones,
            Buffers = Buffers,
            InfluenceBuffer = InfluenceBuffer,
            UndoIndices = UndoIndices,
            UndoOldStones = UndoOldStones,
            UndoCount = 0,
        };
    }
}
