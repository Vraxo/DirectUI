// Widgets/LineEditState.cs
using System.Collections.Generic;

namespace DirectUI;

// Record for undo/redo functionality
internal record struct LineEditUndoRecord(string Text, int CaretPosition, float ScrollPixelOffset);

// State object for an immediate-mode LineEdit control
internal class LineEditState
{
    // Caret and view state
    internal int CaretPosition;
    internal float ScrollPixelOffset;

    // Blinking caret state
    internal float BlinkTimer;
    internal bool IsBlinkOn = true;

    // Undo/Redo state
    internal readonly Stack<LineEditUndoRecord> UndoStack = new();
    internal readonly Stack<LineEditUndoRecord> RedoStack = new();
    internal const int HistoryLimit = 50;
}