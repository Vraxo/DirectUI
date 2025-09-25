using System;
using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Drawing;

namespace Bankan;

public class KanbanDragDropHandler
{
    public KanbanTask? DraggedTask { get; private set; }
    public KanbanColumn? DropTargetColumn { get; private set; }
    public int DropIndex { get; private set; } = -1;

    private KanbanColumn? _sourceColumn;
    private Vector2 _dragOffset;
    private readonly KanbanBoard _board;

    public KanbanDragDropHandler(KanbanBoard board)
    {
        _board = board;
    }

    public bool IsDragging() => DraggedTask != null;

    public void BeginDrag(KanbanTask task, KanbanColumn sourceColumn, Vector2 mousePosition, Vector2 taskPosition)
    {
        DraggedTask = task;
        _sourceColumn = sourceColumn;
        _dragOffset = mousePosition - taskPosition;
    }

    /// <summary>
    /// Resets the per-frame drop target state. Called by the renderer before processing the board.
    /// </summary>
    public void ResetFrameDropTarget()
    {
        if (!IsDragging()) return;
        DropTargetColumn = null;
        DropIndex = -1;
    }

    public bool Update()
    {
        var input = UI.Context.InputState;
        if (DraggedTask == null) return false;

        // The only logic here is to finalize the drop when the mouse is released.
        // The drop target itself is continuously calculated by the renderer.
        if (!input.IsLeftMouseDown)
        {
            bool modified = false;
            if (DraggedTask != null && _sourceColumn != null && DropTargetColumn != null && DropIndex != -1)
            {
                int originalIndex = _sourceColumn.Tasks.IndexOf(DraggedTask);
                if (originalIndex != -1)
                {
                    _sourceColumn.Tasks.RemoveAt(originalIndex);
                    if (_sourceColumn == DropTargetColumn && DropIndex > originalIndex)
                    {
                        DropIndex--;
                    }
                    int clampedDropIndex = Math.Clamp(DropIndex, 0, DropTargetColumn.Tasks.Count);
                    DropTargetColumn.Tasks.Insert(clampedDropIndex, DraggedTask);
                    modified = true;
                }
            }

            // Clean up all state
            DraggedTask = null;
            _sourceColumn = null;
            DropTargetColumn = null;
            DropIndex = -1;
            UI.State.ClearAllActivePressState();
            return modified;
        }
        return false;
    }

    /// <summary>
    /// Called by the renderer for each task widget to determine if it's a potential drop target.
    /// This will overwrite previous targets found in the same frame, ensuring the one under the mouse wins.
    /// </summary>
    public void UpdateDropTarget(KanbanColumn column, int taskIndex, Vortice.Mathematics.Rect taskBounds)
    {
        if (!IsDragging()) return;

        var mousePos = UI.Context.InputState.MousePosition;

        // Check if mouse is inside the horizontal bounds of the task
        if (mousePos.X >= taskBounds.Left && mousePos.X <= taskBounds.Right)
        {
            float midPointY = taskBounds.Y + taskBounds.Height / 2f;
            if (mousePos.Y < midPointY)
            {
                // Only update if we haven't already found a target *above* this one.
                // This prevents a lower task from stealing the drop target if the mouse is
                // between two tasks.
                if (DropTargetColumn == null || DropTargetColumn != column || DropIndex > taskIndex)
                {
                    DropTargetColumn = column;
                    DropIndex = taskIndex;
                }
            }
        }
    }

    /// <summary>
    /// Called by the renderer for each column to handle dropping into an empty column
    /// or at the very end of a column with existing tasks.
    /// </summary>
    public void UpdateDropTargetForColumn(KanbanColumn column, Vortice.Mathematics.Rect columnBounds, int lastTaskIndex)
    {
        if (!IsDragging()) return;

        var mousePos = UI.Context.InputState.MousePosition;
        if (columnBounds.Contains(mousePos))
        {
            // If we are here, it means the mouse is over the column but no specific
            // task target has been set yet, so we drop at the end.
            if (DropTargetColumn == null)
            {
                DropTargetColumn = column;
                DropIndex = lastTaskIndex;
            }
        }
    }

    public void DrawDraggedTaskOverlay(float width)
    {
        if (DraggedTask == null || width <= 0) return;
        var mousePos = UI.Context.InputState.MousePosition;

        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
        var wrappedLayout = UI.Context.TextService.GetTextLayout(DraggedTask.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float height = wrappedLayout.Size.Y + 30;
        var pos = mousePos - _dragOffset;

        var bounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);
        var semiTransparent = DraggedTask.Color;
        semiTransparent.A = 220; // Make it more opaque while dragging
        var style = new BoxStyle { FillColor = semiTransparent, BorderColor = Colors.White, BorderLength = 1, Roundness = 0.1f };
        UI.Context.Renderer.DrawBox(bounds, style);

        var textBounds = new Vortice.Mathematics.Rect(bounds.X + 15, bounds.Y, bounds.Width - 30, bounds.Height);
        var textAlign = new Alignment(HAlignment.Left, VAlignment.Center);
        UI.DrawTextPrimitive(textBounds, DraggedTask.Text, new ButtonStyle(textStyle) { FontColor = new Color(18, 18, 18, 255) }, textAlign, Vector2.Zero);
    }
}