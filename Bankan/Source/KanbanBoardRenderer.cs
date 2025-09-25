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

    public void DrawBoard(Vector2 boardStartPosition, float columnWidth, float columnGap)
    {
        var scale = UI.Context.UIScale;
        // Reset the drop target state at the beginning of the render pass.
        // This ensures targets are freshly calculated based on this frame's layout.
        _dragDropHandler.PrepareNextFrameTarget();

        // The container system operates in logical (unscaled) space.
        // We pass the unscaled start position and gap.
        UI.BeginHBoxContainer("board_content_hbox", boardStartPosition / scale, gap: columnGap / scale);
        foreach (var column in _board.Columns)
        {
            // The column width passed here is LOGICAL width.
            DrawColumnContent(column, columnWidth / scale);
        }
        UI.EndHBoxContainer();
    }

    private void DrawColumnBackground(Vector2 position, Vector2 size)
    {
        var columnBgColor = new Color(30, 30, 30, 255); // #1e1e1e
        var columnStyle = new BoxStyle { FillColor = columnBgColor, Roundness = 0.1f, BorderLength = 0 };
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(position.X, position.Y, size.X, size.Y), columnStyle);
    }

    private void DrawColumnContent(KanbanColumn column, float logicalColumnWidth)
    {
        var scale = UI.Context.UIScale;

        // 1. Calculate this column's total height, which includes its outer padding.
        float myTotalLogicalHeight = CalculateColumnContentHeight(column) / scale;
        var myLogicalPosition = UI.Context.Layout.GetCurrentPosition();
        var myPhysicalPosition = myLogicalPosition * scale;

        // 2. Draw the background for the entire column area using PHYSICAL dimensions.
        DrawColumnBackground(myPhysicalPosition, new Vector2(logicalColumnWidth * scale, myTotalLogicalHeight * scale));

        // 3. Begin a VBox for the column's inner content. It operates in LOGICAL space.
        var contentPadding = 15f;
        var contentStartPosition = myLogicalPosition + new Vector2(contentPadding, contentPadding);
        UI.BeginVBoxContainer(column.Id, contentStartPosition, gap: 10f);

        // 4. Draw the actual widgets inside the column using LOGICAL dimensions.
        var contentWidth = logicalColumnWidth - contentPadding * 2;
        var titleStyle = new ButtonStyle { FontColor = new Color(224, 224, 224, 255), FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text(column.Id + "_title", column.Title, new Vector2(contentWidth, 30), titleStyle, new Alignment(HAlignment.Center, VAlignment.Center));

        // Separator vertical padding is 5, thickness 2. Total logical height = 12.
        UI.Separator(contentWidth, 2, 5, new Color(51, 51, 51, 255));

        int currentTaskIndex = 0;
        foreach (var task in column.Tasks)
        {
            DrawDropIndicator(column, currentTaskIndex, contentWidth);
            if (task != _dragDropHandler.DraggedTask)
            {
                DrawTaskWidget(column, task, currentTaskIndex, contentWidth);
            }
            else
            {
                DrawDragPlaceholder(task, contentWidth);
            }
            currentTaskIndex++;
        }
        // Draw a final drop indicator at the end of the list
        DrawDropIndicator(column, currentTaskIndex, contentWidth);
        // After iterating through tasks, check for a drop in an empty or final position
        var columnBounds = new Vortice.Mathematics.Rect(myPhysicalPosition.X, myPhysicalPosition.Y, logicalColumnWidth * scale, myTotalLogicalHeight * scale);
        _dragDropHandler.UpdateDropTargetForColumn(column, columnBounds, currentTaskIndex);

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

        var vboxState = UI.Context.Layout.PeekContainer() as VBoxContainerState;
        if (vboxState != null)
        {
            float actualContentLogicalHeight = vboxState.GetAccumulatedSize().Y;
            float desiredContentLogicalHeight = myTotalLogicalHeight - (contentPadding * 2);

            if (desiredContentLogicalHeight > actualContentLogicalHeight)
            {
                float paddingAmount = desiredContentLogicalHeight - actualContentLogicalHeight;
                UI.Context.Layout.AdvanceLayout(new Vector2(0, paddingAmount));
            }
        }

        UI.EndVBoxContainer();
    }

    private float CalculateColumnContentHeight(KanbanColumn column)
    {
        var scale = UI.Context.UIScale;
        float columnWidth = 350f; // Use logical width for calculation
        float contentPadding = 15f;
        float gap = 10f;
        float tasksInnerWidth = columnWidth - (contentPadding * 2);

        float height = 0;
        height += contentPadding; // Top padding
        height += 30f + gap;      // Title + gap
        height += 12f + gap;      // Separator (2 thickness + 5*2 padding) + gap

        if (column.Tasks.Any())
        {
            var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 * scale }; // Use scaled font size for measurement
            foreach (var task in column.Tasks)
            {
                var wrappedLayout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2((tasksInnerWidth - 30) * scale, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
                height += (wrappedLayout.Size.Y / scale) + 30; // Unscale measured height to get logical height, add logical padding
                height += gap;
            }
            height -= gap; // Remove final gap after last task
        }

        if (column.Id == "todo")
        {
            height += gap;
            height += 40f; // Add task button
        }

        height += contentPadding; // Bottom padding
        return height * scale; // Return final physical height
    }

    private void DrawTaskWidget(KanbanColumn column, KanbanTask task, int taskIndex, float logicalWidth)
    {
        var context = UI.Context;
        var scale = context.UIScale;
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 * scale };
        var wrappedLayout = context.TextService.GetTextLayout(task.Text, textStyle, new Vector2((logicalWidth - 30) * scale, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float logicalHeight = (wrappedLayout.Size.Y / scale) + 30;

        var taskTheme = new ButtonStylePack { Roundness = 0.1f };
        var cardBackground = new Color(42, 42, 42, 255);
        var hoverBackground = new Color(60, 60, 60, 255);

        var finalTextStyle = new ButtonStyle(textStyle) { FontSize = 14 }; // For rendering, use logical font size

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

        var logicalPos = context.Layout.GetCurrentPosition();
        var physicalPos = logicalPos * scale;
        var physicalSize = new Vector2(logicalWidth * scale, logicalHeight * scale);
        var taskBounds = new Vortice.Mathematics.Rect(physicalPos.X, physicalPos.Y, physicalSize.X, physicalSize.Y);

        _dragDropHandler.UpdateDropTarget(column, taskIndex, taskBounds);

        if (!context.Layout.IsRectVisible(taskBounds))
        {
            context.Layout.AdvanceLayout(new Vector2(logicalWidth, logicalHeight));
            return;
        }

        bool isHovering = taskBounds.Contains(context.InputState.MousePosition);
        if (isHovering && !_modalManager.IsModalOpen)
        {
            UI.State.SetPotentialInputTarget(task.Id.GetHashCode());
        }
        if (context.InputState.WasLeftMousePressedThisFrame && isHovering && UI.State.TrySetActivePress(task.Id.GetHashCode(), 1))
        {
            _dragDropHandler.BeginDrag(task, column, context.InputState.MousePosition, physicalPos);
        }

        if (UI.BeginContextMenu($"context_widget_{task.Id}"))
        {
            _modalManager.OpenContextMenuForTask(task);
        }

        var finalStyle = isHovering ? taskTheme.Hover : taskTheme.Normal;
        finalStyle.BorderLengthLeft *= scale;

        context.Renderer.DrawBox(taskBounds, finalStyle);
        var textBounds = new Vortice.Mathematics.Rect(taskBounds.X + (15 * scale), taskBounds.Y, taskBounds.Width - (30 * scale), taskBounds.Height);
        UI.DrawTextPrimitive(textBounds, task.Text, finalTextStyle, textAlign, Vector2.Zero);

        context.Layout.AdvanceLayout(new Vector2(logicalWidth, logicalHeight));
    }


    private void DrawDragPlaceholder(KanbanTask task, float logicalWidth)
    {
        var scale = UI.Context.UIScale;
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 * scale };
        var wrappedLayout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2((logicalWidth - 30) * scale, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float logicalHeight = (wrappedLayout.Size.Y / scale) + 30;

        var logicalSize = new Vector2(logicalWidth, logicalHeight);
        var physicalSize = logicalSize * scale;
        var physicalPos = UI.Context.Layout.ApplyLayout(Vector2.Zero);
        var bounds = new Vortice.Mathematics.Rect(physicalPos.X, physicalPos.Y, physicalSize.X, physicalSize.Y);
        var style = new BoxStyle { FillColor = new Color(0, 0, 0, 100), BorderColor = DefaultTheme.Accent, BorderLength = 1 * scale, Roundness = 0.1f };
        UI.Context.Renderer.DrawBox(bounds, style);
        UI.Context.Layout.AdvanceLayout(logicalSize);
    }

    private void DrawDropIndicator(KanbanColumn column, int index, float logicalWidth)
    {
        var scale = UI.Context.UIScale;
        if (!_dragDropHandler.IsDragging() || _dragDropHandler.DropTargetColumn != column || _dragDropHandler.DropIndex != index) return;
        var physicalPos = UI.Context.Layout.ApplyLayout(Vector2.Zero);
        var indicatorRect = new Vortice.Mathematics.Rect(physicalPos.X, physicalPos.Y - (5 * scale), logicalWidth * scale, 4 * scale);
        var style = new BoxStyle { FillColor = DefaultTheme.Accent, BorderLength = 0, Roundness = 0.5f };
        UI.Context.Renderer.DrawBox(indicatorRect, style);
    }
}