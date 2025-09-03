using System;
using System.Collections.Generic;

namespace DirectUI;

internal record struct LineEditUndoRecord(string Text, int CaretPosition, int SelectionAnchor, float ScrollPixelOffset);

internal class InputTextState
{
    // Selection state
    internal int CaretPosition;     // The "moving" end of the selection
    internal int SelectionAnchor;   // The "fixed" end of the selection

    // Helpers for selection
    internal bool HasSelection => CaretPosition != SelectionAnchor;
    internal int SelectionStart => Math.Min(CaretPosition, SelectionAnchor);
    internal int SelectionEnd => Math.Max(CaretPosition, SelectionAnchor);

    // View state
    internal float ScrollPixelOffset;

    // Caret blink state
    internal float BlinkTimer;
    internal bool IsBlinkOn = true;

    // Undo/Redo state
    internal readonly Stack<LineEditUndoRecord> UndoStack = new();
    internal readonly Stack<LineEditUndoRecord> RedoStack = new();
    internal const int HistoryLimit = 50;
}