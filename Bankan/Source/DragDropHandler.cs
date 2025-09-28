using System;
using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Drawing;

namespace Bankan;

public class DragDropHandler
{
    public Task? DraggedTask { get; private set; }

    // State for the CURRENT frame's render pass (calculated last frame)
    public KanbanColumn? DropTargetColumn { get; private set; }
    public int DropIndex { get; private set; } = -1;

    private KanbanColumn? _sourceColumn;
    private Vector2 _dragOffset;
    private readonly KanbanBoard _board;
    private readonly KanbanSettings _settings;

    // State being calculated THIS frame for use in the NEXT frame
    private KanbanColumn? _nextFrameDropTargetColumn;
    private int _nextFrameDropIndex = -1;
    private bool _dropTargetFoundThisFrame;

    public DragDropHandler(KanbanBoard board, KanbanSettings settings)
    {
        _board = board;
        _settings = settings;
    }

    public bool IsDragging() => DraggedTask is not null;

    public void BeginDrag(Task task, KanbanColumn sourceColumn, Vector2 mousePosition, Vector2 taskPosition)
    {
        DraggedTask = task;
        _sourceColumn = sourceColumn;
        _dragOffset = mousePosition - taskPosition;
    }

    /// <summary>
    /// Resets the state that will be calculated this frame for use in the next one.
    /// </summary>
    public void PrepareNextFrameTarget()
    {
        if (!IsDragging()) return;
        _nextFrameDropTargetColumn = null;
        _nextFrameDropIndex = -1;
        _dropTargetFoundThisFrame = false;
    }

    public bool Update()
    {
        var input = UI.Context.InputState;
        if (DraggedTask is null) return false;

        // At the start of the logic update, cycle the state. The drop target calculated
        // in the previous frame's render pass now becomes the current drop target.
        DropTargetColumn = _nextFrameDropTargetColumn;
        DropIndex = _nextFrameDropIndex;

        if (!input.IsLeftMouseDown)
        {
            bool modified = false;
            // Use the now-current DropTargetColumn and DropIndex for the final action.
            if (DraggedTask is not null && _sourceColumn is not null && DropTargetColumn is not null && DropIndex != -1)
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

            // Clean up all state now that the drag is over
            DraggedTask = null;
            _sourceColumn = null;
            DropTargetColumn = null;
            DropIndex = -1;
            _nextFrameDropTargetColumn = null;
            _nextFrameDropIndex = -1;

            UI.State.ClearAllActivePressState();
            return modified;
        }
        return false;
    }

    /// <summary>
    /// Called by the renderer for each task widget. It calculates the drop target for the NEXT frame.
    /// </summary>
    public void UpdateDropTarget(KanbanColumn column, int taskIndex, Vortice.Mathematics.Rect taskBounds)
    {
        if (!IsDragging() || _dropTargetFoundThisFrame) return;

        var mousePos = UI.Context.InputState.MousePosition;

        if (mousePos.X >= taskBounds.Left && mousePos.X <= taskBounds.Right)
        {
            float midPointY = taskBounds.Y + taskBounds.Height / 2f;
            if (mousePos.Y < midPointY)
            {
                _nextFrameDropTargetColumn = column;
                _nextFrameDropIndex = taskIndex;
                _dropTargetFoundThisFrame = true;
            }
        }
    }

    /// <summary>
    /// Called by the renderer for each column to handle dropping at the end. It calculates for the NEXT frame.
    /// </summary>
    public void UpdateDropTargetForColumn(KanbanColumn column, Vortice.Mathematics.Rect columnBounds, int lastTaskIndex)
    {
        if (!IsDragging() || _dropTargetFoundThisFrame) return;

        var mousePos = UI.Context.InputState.MousePosition;
        if (columnBounds.Contains(mousePos))
        {
            _nextFrameDropTargetColumn = column;
            _nextFrameDropIndex = lastTaskIndex;
            _dropTargetFoundThisFrame = true;
        }
    }

    public void DrawDraggedTaskOverlay(float physicalWidth)
    {
        if (DraggedTask is null || physicalWidth <= 0) return;
        var scale = UI.Context.UIScale;
        var mousePos = UI.Context.InputState.MousePosition;

        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 * scale };
        var wrappedLayout = UI.Context.TextService.GetTextLayout(DraggedTask.Text, textStyle, new Vector2(physicalWidth - (30 * scale), float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float physicalHeight = wrappedLayout.Size.Y + (30 * scale);
        var pos = mousePos - _dragOffset;

        var bounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, physicalWidth, physicalHeight);
        var semiTransparent = DraggedTask.Color;
        semiTransparent.A = 220;
        var style = new BoxStyle { FillColor = semiTransparent, BorderColor = Colors.White, BorderLength = 1 * scale, Roundness = 0.1f };
        UI.Context.Renderer.DrawBox(bounds, style);

        var textBounds = new Vortice.Mathematics.Rect(bounds.X + (15 * scale), bounds.Y, bounds.Width - (30 * scale), bounds.Height);

        // --- FIXES ---
        // Determine font color based on settings, just like TaskRenderer
        Color fontColor = _settings.ColorStyle == TaskColorStyle.Background
            ? new Color(18, 18, 18, 255)
            : DefaultTheme.Text;

        var renderTextStyle = new ButtonStyle
        {
            FontName = "Segoe UI",
            FontSize = 14 * scale, // Correctly scaled font size
            FontColor = fontColor  // Correct font color
        };

        // Get correct alignment from settings
        Alignment textAlign = GetTextAlignment();

        UI.DrawTextPrimitive(textBounds, DraggedTask.Text, renderTextStyle, textAlign, Vector2.Zero);
    }

    private Alignment GetTextAlignment()
    {
        return _settings.TextAlign == TaskTextAlign.Left
            ? new(HAlignment.Left, VAlignment.Center)
            : new(HAlignment.Center, VAlignment.Center);
    }
}