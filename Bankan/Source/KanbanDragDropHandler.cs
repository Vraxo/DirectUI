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

    public void BeginDrag(KanbanTask task, KanbanColumn sourceColumn, Vector2 mousePosition, Vector2 taskPosition)
    {
        DraggedTask = task;
        _sourceColumn = sourceColumn;
        _dragOffset = mousePosition - taskPosition;
    }

    public bool Update()
    {
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
            UI.State.ClearActivePress(0);
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
        semiTransparent.A = 150;
        var style = new BoxStyle { FillColor = semiTransparent, BorderColor = Colors.White, BorderLength = 1, Roundness = 0.1f };
        UI.Context.Renderer.DrawBox(bounds, style);

        var textBounds = new Vortice.Mathematics.Rect(bounds.X + 15, bounds.Y, bounds.Width - 30, bounds.Height);
        var textAlign = new Alignment(HAlignment.Left, VAlignment.Center);
        UI.DrawTextPrimitive(textBounds, DraggedTask.Text, textStyle, textAlign, Vector2.Zero);
    }

    private KanbanColumn? FindDropColumn()
    {
        var mousePos = UI.Context.InputState.MousePosition;
        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();

        for (int i = 0; i < _board.Columns.Count; i++)
        {
            var colX = gridState.StartPosition.X + i * (gridState.CellWidth + gridState.Gap.X);
            var colBounds = new Vortice.Mathematics.Rect(colX, gridState.StartPosition.Y, gridState.CellWidth, gridState.AvailableSize.Y);
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
        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();
        float currentY = gridState.StartPosition.Y + 15 + 30 + 17;

        int insertIndex = 0;
        foreach (var task in column.Tasks)
        {
            var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
            var layout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(gridState.CellWidth - 60, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
            float taskHeight = layout.Size.Y + 30 + 10;
            float midPoint = currentY + taskHeight / 2;
            if (mousePos.Y < midPoint)
            {
                return insertIndex;
            }
            currentY += taskHeight;
            insertIndex++;
        }
        return column.Tasks.Count;
    }
}