using System.Numerics;
using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;

namespace Bankan;

public class KanbanBoardRenderer
{
    private readonly KanbanBoard _board;
    private readonly KanbanSettings _settings;
    private readonly KanbanModalManager _modalManager;
    private readonly KanbanDragDropHandler _dragDropHandler;

    public KanbanBoardRenderer(KanbanBoard board, KanbanSettings settings, KanbanModalManager modalManager, KanbanDragDropHandler dragDropHandler)
    {
        _board = board;
        _settings = settings;
        _modalManager = modalManager;
        _dragDropHandler = dragDropHandler;
    }

    public void DrawColumnsInGrid()
    {
        foreach (var column in _board.Columns)
        {
            DrawColumn(column);
        }
    }

    private void DrawColumn(KanbanColumn column)
    {
        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();
        var cellPosition = gridState.GetCurrentPosition();
        var cellWidth = gridState.CellWidth;
        var cellHeight = gridState.AvailableSize.Y;
        var columnBounds = new Vortice.Mathematics.Rect(cellPosition.X, cellPosition.Y, cellWidth, cellHeight);

        var columnBgColor = new Color(30, 30, 30, 255);
        var columnStyle = new BoxStyle { FillColor = columnBgColor, Roundness = 0.1f, BorderLength = 0 };
        UI.Context.Renderer.DrawBox(columnBounds, columnStyle);

        var contentPadding = new Vector2(15, 15);
        var contentWidth = cellWidth - contentPadding.X * 2;
        var contentStartPosition = cellPosition + contentPadding;

        UI.BeginVBoxContainer(column.Id, contentStartPosition, gap: 10f);

        var titleStyle = new ButtonStyle { FontColor = new Color(224, 224, 224, 255), FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text(column.Id + "_title", column.Title, new Vector2(contentWidth, 30), titleStyle, new Alignment(HAlignment.Center, VAlignment.Center));
        UI.Separator(contentWidth, 2, 5, new Color(51, 51, 51, 255));

        int currentTaskIndex = 0;
        foreach (var task in column.Tasks)
        {
            DrawDropIndicator(column, currentTaskIndex, contentWidth);
            if (task != _dragDropHandler.DraggedTask)
            {
                DrawTaskWidget(column, task, contentWidth);
            }
            else
            {
                DrawDragPlaceholder(task, contentWidth);
            }
            currentTaskIndex++;
        }
        DrawDropIndicator(column, currentTaskIndex, contentWidth);

        if (column.Id == "todo")
        {
            var addTaskTheme = new ButtonStylePack { Normal = { FillColor = Colors.Transparent, BorderColor = new Color(51, 51, 51, 255) }, Hover = { FillColor = DefaultTheme.Accent, BorderColor = DefaultTheme.Accent } };
            if (UI.Button(column.Id + "_add_task", "+ Add Task", size: new Vector2(contentWidth, 40), theme: addTaskTheme))
            {
                _modalManager.OpenAddTaskModal(column);
            }
        }

        UI.EndVBoxContainer();
    }

    private void DrawTaskWidget(KanbanColumn column, KanbanTask task, float width)
    {
        var context = UI.Context;
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
        var wrappedLayout = context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float height = wrappedLayout.Size.Y + 30;

        var taskTheme = new ButtonStylePack { Roundness = 0.1f };
        var cardBackground = new Color(42, 42, 42, 255);

        if (_settings.ColorStyle == TaskColorStyle.Background)
        {
            taskTheme.Normal.FillColor = task.Color;
            taskTheme.Normal.BorderColor = Colors.Transparent;
            taskTheme.Normal.FontColor = new Color(18, 18, 18, 255);
        }
        else
        {
            taskTheme.Normal.FillColor = cardBackground;
            taskTheme.Normal.BorderColor = task.Color;
            taskTheme.Normal.BorderLengthLeft = 4f;
            taskTheme.Normal.BorderLengthTop = 0;
            taskTheme.Normal.BorderLengthRight = 0;
            taskTheme.Normal.BorderLengthBottom = 0;
            taskTheme.Normal.FontColor = DefaultTheme.Text;
        }
        taskTheme.Hover.FillColor = new Color(60, 60, 60, 255);
        var textAlign = _settings.TextAlign == TaskTextAlign.Left ? new Alignment(HAlignment.Left, VAlignment.Center) : new Alignment(HAlignment.Center, VAlignment.Center);

        var pos = context.Layout.GetCurrentPosition();
        var taskBounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);

        if (!context.Layout.IsRectVisible(taskBounds))
        {
            context.Layout.AdvanceLayout(new Vector2(width, height));
            return;
        }

        bool isHovering = taskBounds.Contains(context.InputState.MousePosition);
        if (isHovering && !_modalManager.IsModalOpen)
        {
            UI.State.SetPotentialInputTarget(task.Id.GetHashCode());
        }

        var finalStyle = isHovering ? taskTheme.Hover : taskTheme.Normal;
        context.Renderer.DrawBox(taskBounds, finalStyle);
        var textBounds = new Vortice.Mathematics.Rect(taskBounds.X + 15, taskBounds.Y, taskBounds.Width - 30, taskBounds.Height);
        UI.DrawTextPrimitive(textBounds, task.Text, textStyle, textAlign, Vector2.Zero);

        if (context.InputState.WasLeftMousePressedThisFrame && isHovering && UI.State.TrySetActivePress(task.Id.GetHashCode(), 1))
        {
            _dragDropHandler.BeginDrag(task, column, context.InputState.MousePosition, pos);
        }

        if (UI.BeginContextMenu(task.Id))
        {
            int choice = UI.ContextMenu($"context_{task.Id}", new[] { "Edit Task", "Delete Task" });
            if (choice == 0) _modalManager.OpenEditTaskModal(task);
            else if (choice == 1)
            {
                column.Tasks.Remove(task);
                _modalManager.RequestSave();
            }
        }
        context.Layout.AdvanceLayout(new Vector2(width, height));
    }

    private void DrawDragPlaceholder(KanbanTask task, float width)
    {
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
        var wrappedLayout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float height = wrappedLayout.Size.Y + 30;

        var pos = UI.Context.Layout.GetCurrentPosition();
        var bounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);
        var style = new BoxStyle { FillColor = new Color(0, 0, 0, 100), BorderColor = DefaultTheme.Accent, BorderLength = 1, Roundness = 0.1f };
        UI.Context.Renderer.DrawBox(bounds, style);
        UI.Context.Layout.AdvanceLayout(new Vector2(width, height));
    }

    private void DrawDropIndicator(KanbanColumn column, int index, float width)
    {
        if (_dragDropHandler.DropTargetColumn != column || _dragDropHandler.DropIndex != index) return;
        var pos = UI.Context.Layout.GetCurrentPosition();
        var indicatorRect = new Vortice.Mathematics.Rect(pos.X, pos.Y - 5, width, 4);
        var style = new BoxStyle { FillColor = DefaultTheme.Accent, BorderLength = 0, Roundness = 0.5f };
        UI.Context.Renderer.DrawBox(indicatorRect, style);
    }
}