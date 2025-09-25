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

    // Layout parameters for hit-testing, updated each frame
    private Vector2 _boardScrollOffset;
    private float _boardWidth;
    private float _boardHeight;
    private float _columnWidth;
    private float _columnGap;
    private float _topMargin;
    private Vector2 _boardPadding;

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

    public bool Update(Vector2 scrollOffset, float boardWidth, float boardHeight, float columnWidth, float columnGap, float topMargin, Vector2 boardPadding)
    {
        _boardScrollOffset = scrollOffset;
        _boardWidth = boardWidth;
        _boardHeight = boardHeight;
        _columnWidth = columnWidth;
        _columnGap = columnGap;
        _topMargin = topMargin;
        _boardPadding = boardPadding;

        var input = UI.Context.InputState;
        if (DraggedTask == null) return false;

        if (input.IsLeftMouseDown)
        {
            DropTargetColumn = FindDropColumn();
            DropIndex = DropTargetColumn != null ? FindDropIndexInColumn(DropTargetColumn) : -1;
        }
        else
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

            DraggedTask = null;
            _sourceColumn = null;
            DropTargetColumn = null;
            DropIndex = -1;
            UI.State.ClearActivePress(0); // Clear any active press state globally
            return modified;
        }
        return false;
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

    private KanbanColumn? FindDropColumn()
    {
        var mousePos = UI.Context.InputState.MousePosition;
        var windowSize = UI.Context.Renderer.RenderTargetSize;

        // Calculate the centered start position of the board content
        float startX = _boardPadding.X;
        if (_boardWidth < (windowSize.X - _boardPadding.X * 2))
            startX += ((windowSize.X - _boardPadding.X * 2) - _boardWidth) / 2f;
        else
            startX -= _boardScrollOffset.X;

        for (int i = 0; i < _board.Columns.Count; i++)
        {
            var colX = startX + i * (_columnWidth + _columnGap);
            var colBounds = new Vortice.Mathematics.Rect(colX, _topMargin, _columnWidth, _boardHeight);
            if (colBounds.Contains(mousePos))
            {
                return _board.Columns[i];
            }
        }
        return null;
    }

    private int FindDropIndexInColumn(KanbanColumn column)
    {
        var mousePos = UI.Context.InputState.MousePosition;
        var windowSize = UI.Context.Renderer.RenderTargetSize;

        // Calculate start positions again, as in FindDropColumn
        float startX = _boardPadding.X;
        if (_boardWidth < (windowSize.X - _boardPadding.X * 2))
            startX += ((windowSize.X - _boardPadding.X * 2) - _boardWidth) / 2f;
        else
            startX -= _boardScrollOffset.X;

        float startY = _topMargin;
        if (_boardHeight < (windowSize.Y - _topMargin - _boardPadding.Y))
            startY += ((windowSize.Y - _topMargin - _boardPadding.Y) - _boardHeight) / 2f;
        else
            startY -= _boardScrollOffset.Y;

        // Start Y position for the first task inside a column
        float currentY = startY + 15f + 30f + 10f + 14f + 10f; // Padding + Title + Gap + Separator + Gap

        int insertIndex = 0;
        foreach (var task in column.Tasks.Where(t => t != DraggedTask))
        {
            var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
            var layout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(_columnWidth - 60, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
            float taskHeight = layout.Size.Y + 30;
            float gap = 10f;

            float midPoint = currentY + (taskHeight / 2f);
            if (mousePos.Y < midPoint)
            {
                return insertIndex;
            }
            currentY += taskHeight + gap;
            insertIndex++;
        }
        return insertIndex; // Drop at the end if not between any tasks
    }
}