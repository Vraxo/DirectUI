// NEW: Widgets/LineEditState.cs
using System.Collections.Generic;

namespace DirectUI;

// Record for undo/redo functionality
internal record struct LineEditUndoRecord(string Text, int CaretPosition, int TextStartIndex);

// State object for an immediate-mode LineEdit control
internal class LineEditState
{
    // Caret and view state
    internal int CaretPosition = 0;
    internal int TextStartIndex = 0; // The starting character index for the visible text portion

    // Blinking caret state
    internal float BlinkTimer = 0.0f;
    internal bool IsBlinkOn = true;

    // Held-key state for repeat actions
    internal float KeyRepeatTimer = 0.0f;
    internal Keys RepeatingKey = Keys.Unknown;

    // Undo/Redo state
    internal readonly Stack<LineEditUndoRecord> UndoStack = new();
    internal readonly Stack<LineEditUndoRecord> RedoStack = new();
    internal const int HistoryLimit = 50;

    // A flag to prevent pushing duplicate states to the undo stack
    internal bool NeedsUndoStatePush = false;
}