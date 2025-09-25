using System.Linq;
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

    public void DrawBoard(Vector2 boardStartPosition, float uniformColumnHeight, float columnWidth, float columnGap)
    {
        // First pass: Draw all column backgrounds to ensure they have the same uniform height
        for (int i = 0; i < _board.Columns.Count; i++)
        {
            var columnPosition = new Vector2(boardStartPosition.X + i * (columnWidth + columnGap), boardStartPosition.Y);
            DrawColumnBackground(columnPosition, new Vector2(columnWidth, uniformColumnHeight));
        }

        // Second pass: Draw the content of each column on top of the backgrounds
        UI.BeginHBoxContainer("board_content_hbox", boardStartPosition, gap: columnGap);
        foreach (var column in _board.Columns)
        {
            DrawColumnContent(column, new Vector2(columnWidth, uniformColumnHeight));
        }
        UI.EndHBoxContainer();
    }

    private void DrawColumnBackground(Vector2 position, Vector2 size)
    {
        var columnBgColor = new Color(30, 30, 30, 255); // #1e1e1e
        var columnStyle = new BoxStyle { FillColor = columnBgColor, Roundness = 0.1f, BorderLength = 0 };
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(position.X, position.Y, size.X, size.Y), columnStyle);
    }

    private void DrawColumnContent(KanbanColumn column, Vector2 columnSize)
    {
        var contentPadding = 15f;
        var contentWidth = columnSize.X - contentPadding * 2;
        // The VBox starts relative to the HBox's cursor, so we just need padding.
        var contentStartPosition = UI.Context.Layout.GetCurrentPosition() + new Vector2(contentPadding, contentPadding);

        UI.BeginVBoxContainer(column.Id, contentStartPosition, gap: 10f);

        var titleStyle = new ButtonStyle { FontColor = new Color(224, 224, 224, 255), FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text(column.Id + "_title", column.Title, new Vector2(contentWidth, 30), titleStyle, new Alignment(HAlignment.Center, VAlignment.Center));

        // Separator has height and vertical padding, so we account for that
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
        // Draw a final drop indicator at the end of the list
        DrawDropIndicator(column, currentTaskIndex, contentWidth);

        if (column.Id == "todo")
        {
            var addTaskTheme = new ButtonStylePack();
            addTaskTheme.Normal.FillColor = Colors.Transparent;
            addTaskTheme.Normal.BorderColor = new Color(51, 51, 51, 255);
            addTaskTheme.Hover.FillColor = DefaultTheme.Accent;
            addTaskTheme.Hover.BorderColor = DefaultTheme.Accent;

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
        var hoverBackground = new Color(60, 60, 60, 255);

        var finalTextStyle = new ButtonStyle(textStyle);

        if (_settings.ColorStyle == TaskColorStyle.Background)
        {
            taskTheme.Normal.FillColor = task.Color;
            taskTheme.Normal.BorderColor = Colors.Transparent;
            finalTextStyle.FontColor = new Color(18, 18, 18, 255);
        }
        else
        {
            taskTheme.Normal.FillColor = cardBackground;
            taskTheme.Normal.BorderColor = task.Color;
            taskTheme.Normal.BorderLengthLeft = 4f;
            taskTheme.Normal.BorderLengthTop = 0;
            taskTheme.Normal.BorderLengthRight = 0;
            taskTheme.Normal.BorderLengthBottom = 0;
            finalTextStyle.FontColor = DefaultTheme.Text;
        }

        taskTheme.Hover.FillColor = hoverBackground; // Hover is always the same
        var textAlign = _settings.TextAlign == TaskTextAlign.Left ? new Alignment(HAlignment.Left, VAlignment.Center) : new Alignment(HAlignment.Center, VAlignment.Center);

        var pos = context.Layout.GetCurrentPosition();
        var taskBounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);

        if (!context.Layout.IsRectVisible(taskBounds))
        {
            context.Layout.AdvanceLayout(new Vector2(width, height));
            return;
        }

        // --- Interaction ---
        bool isHovering = taskBounds.Contains(context.InputState.MousePosition);
        if (isHovering && !_modalManager.IsModalOpen)
        {
            UI.State.SetPotentialInputTarget(task.Id.GetHashCode());
        }

        if (context.InputState.WasLeftMousePressedThisFrame && isHovering && UI.State.TrySetActivePress(task.Id.GetHashCode(), 1))
        {
            _dragDropHandler.BeginDrag(task, column, context.InputState.MousePosition, pos);
        }

        if (context.InputState.WasRightMousePressedThisFrame && isHovering && UI.State.PotentialInputTargetId == task.Id.GetHashCode())
        {
            _modalManager.SetActiveContextMenuOwner(task.Id);
        }

        // --- Context Menu ---
        if (_modalManager.ActiveContextMenuOwnerId == task.Id)
        {
            int contextMenuId = $"context_{task.Id}".GetHashCode();
            if (UI.BeginContextMenu($"context_widget_{task.Id}"))
            {
                UI.State.SetActivePopup(contextMenuId, (ctx) =>
                {
                    var choice = UI.ContextMenu($"context_menu_{task.Id}", new[] { "Edit Task", "Delete Task" });
                    if (choice != -1)
                    {
                        if (choice == 0) _modalManager.OpenEditTaskModal(task);
                        else if (choice == 1) _modalManager.RequestTaskDeletion(task);
                        _modalManager.ClearActiveContextMenuOwner();
                        UI.State.ClearActivePopup();
                    }
                }, new Vortice.Mathematics.Rect()); // Bounds are calculated internally now
            }
            if (!UI.State.IsPopupOpen && !UI.State.PopupWasOpenedThisFrame)
            {
                _modalManager.ClearActiveContextMenuOwner();
            }
        }

        // --- Drawing ---
        var finalStyle = isHovering ? taskTheme.Hover : taskTheme.Normal;
        context.Renderer.DrawBox(taskBounds, finalStyle);
        var textBounds = new Vortice.Mathematics.Rect(taskBounds.X + 15, taskBounds.Y, taskBounds.Width - 30, taskBounds.Height);
        UI.DrawTextPrimitive(textBounds, task.Text, finalTextStyle, textAlign, Vector2.Zero);

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
        if (!_dragDropHandler.IsDragging() || _dragDropHandler.DropTargetColumn != column || _dragDropHandler.DropIndex != index) return;
        var pos = UI.Context.Layout.GetCurrentPosition();
        var indicatorRect = new Vortice.Mathematics.Rect(pos.X, pos.Y - 5, width, 4); // Positioned between items
        var style = new BoxStyle { FillColor = DefaultTheme.Accent, BorderLength = 0, Roundness = 0.5f };
        UI.Context.Renderer.DrawBox(indicatorRect, style);
    }
}