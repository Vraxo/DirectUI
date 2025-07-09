using System.Collections.Generic;

namespace DirectUI;

internal record struct LineEditUndoRecord(string Text, int CaretPosition, float ScrollPixelOffset);

internal class InputTextState
{
    internal int CaretPosition;
    internal float ScrollPixelOffset;

    internal float BlinkTimer;
    internal bool IsBlinkOn = true;

    internal readonly Stack<LineEditUndoRecord> UndoStack = new();
    internal readonly Stack<LineEditUndoRecord> RedoStack = new();
    internal const int HistoryLimit = 50;
}