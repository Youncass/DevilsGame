[System.Flags]
public enum MoveTechnique
{
    None          = 0,
    SaveAtari     = 1 << 0, // FindAtariEscape
    ThreatenAtari = 1 << 1, // FindAtariThreat
    Capture       = 1 << 2, // FindCaptureMove
    Local         = 1 << 3, // локальный поиск в радиусе 2
    Random        = 1 << 4, // случайный ход с self-atari фильтром
    FullScan      = 1 << 5, // полный перебор (фолбэк)
    AnyLegal      = 1 << 6, // любой легальный (крайний фолбэк)

    FastRollout = SaveAtari | Random | FullScan | AnyLegal,
    FullRoot = SaveAtari | ThreatenAtari | Capture | Local | Random | FullScan | AnyLegal,
}
