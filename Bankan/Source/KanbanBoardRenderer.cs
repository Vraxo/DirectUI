using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Animation;
using DirectUI.Core;
using DirectUI.Drawing;

namespace Bankan;

public class KanbanBoardRenderer
{
    private readonly KanbanBoard _board;
    private readonly KanbanSettings _settings;
    private readonly KanbanModalManager _modalManager;
    private readonly KanbanDragDropHandler _dragDropHandler;
    private string? _activeContextMenuTaskId;

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
            DrawAddTaskButton(column, contentWidth);
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

    private void DrawAddTaskButton(KanbanColumn column, float logicalContentWidth)
    {
        var scale = UI.Context.UIScale;
        var buttonId = column.Id + "_add_task";
        var logicalSize = new Vector2(logicalContentWidth, 40);

        // We must calculate hover state before calling UI.Animate
        var logicalPos = UI.Context.Layout.GetCurrentPosition();
        var physicalBounds = new Vortice.Mathematics.Rect(
            logicalPos.X * scale, logicalPos.Y * scale,
            logicalSize.X * scale, logicalSize.Y * scale
        );
        bool isHovering = physicalBounds.Contains(UI.Context.InputState.MousePosition);

        // Use the new procedural animation system to get a scale factor
        float scaleFactor = UI.Animate(
            buttonId + "_scale",
            isHovering,
            defaultValue: 1.0f,
            WiggleOnHover
        );

        // Apply the animated scale to the button's logical size
        // This will cause the button to visually pop and also affect the layout,
        // pushing subsequent elements down, which is a nice effect for this.
        var animatedLogicalSize = logicalSize * scaleFactor;


        var addTaskTheme = new ButtonStylePack();
        addTaskTheme.Normal.FillColor = Colors.Transparent;
        addTaskTheme.Normal.BorderColor = new Color(51, 51, 51, 255);
        addTaskTheme.Hover.FillColor = DefaultTheme.Accent;
        addTaskTheme.Hover.BorderColor = DefaultTheme.Accent;

        if (UI.Button(buttonId, "+ Add Task", size: animatedLogicalSize, theme: addTaskTheme, animation: new AnimationInfo(1)))
        {
            _modalManager.OpenAddTaskModal(column);
        }
    }

    private float WiggleOnHover(float elapsedTime)
    {
        const float growDuration = 1f;
        const float shrinkDuration = 0.15f;
        const float maxScale = 1.05f;
        const float finalScale = 1.02f;
        const float normalScale = 1.0f;

        if (elapsedTime < growDuration)
        {
            // Phase 1: Grow from 1.0 to 1.05
            float progress = elapsedTime / growDuration;
            return normalScale + (maxScale - normalScale) * progress;
        }
        else if (elapsedTime < growDuration + shrinkDuration)
        {
            // Phase 2: Shrink from 1.05 to 1.02
            float progress = (elapsedTime - growDuration) / shrinkDuration;
            return maxScale + (finalScale - maxScale) * progress;
        }
        else
        {
            // Phase 3: Hold the final size
            return finalScale;
        }
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
        var logicalFontSize = 14f;

        var measurementStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = logicalFontSize * scale };
        var wrappedLayout = context.TextService.GetTextLayout(task.Text, measurementStyle, new Vector2((logicalWidth - 30) * scale, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float logicalHeight = (wrappedLayout.Size.Y / scale) + 30;

        var taskTheme = new ButtonStylePack { Roundness = 0.1f };
        var cardBackground = new Color(42, 42, 42, 255);
        var hoverBackground = new Color(60, 60, 60, 255);

        var renderTextStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = logicalFontSize * scale };

        if (_settings.ColorStyle == TaskColorStyle.Background)
        {
            taskTheme.Normal.FillColor = task.Color;
            taskTheme.Normal.BorderColor = Colors.Transparent;
            renderTextStyle.FontColor = new Color(18, 18, 18, 255);
        }
        else
        {
            taskTheme.Normal.FillColor = cardBackground;
            taskTheme.Normal.BorderColor = task.Color;
            taskTheme.Normal.BorderLengthLeft = 4f;
            taskTheme.Normal.BorderLengthTop = 0;
            taskTheme.Normal.BorderLengthRight = 0;
            taskTheme.Normal.BorderLengthBottom = 0;
            renderTextStyle.FontColor = DefaultTheme.Text;
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

        // If a popup or native modal is open, this task is not interactive.
        bool isInteractive = !UI.State.IsPopupOpen && !_modalManager.IsModalOpen;
        bool isHovering = isInteractive && taskBounds.Contains(context.InputState.MousePosition);

        if (isHovering)
        {
            UI.State.SetPotentialInputTarget(task.Id.GetHashCode());
        }
        if (context.InputState.WasLeftMousePressedThisFrame && isHovering && UI.State.TrySetActivePress(task.Id.GetHashCode(), 1))
        {
            _dragDropHandler.BeginDrag(task, column, context.InputState.MousePosition, physicalPos);
        }

        // Trigger setting the active context menu on right-click, only if interactive.
        if (isInteractive && UI.BeginContextMenu(task.Id))
        {
            _activeContextMenuTaskId = task.Id;
        }

        // If this task's context menu should be open, draw it and check for results.
        if (_activeContextMenuTaskId == task.Id)
        {
            var choice = UI.ContextMenu($"context_menu_{task.Id}", new[] { "Edit Task", "Delete Task" });
            if (choice != -1)
            {
                if (choice == 0) _modalManager.OpenEditTaskModal(task);
                else if (choice == 1) _modalManager.RequestTaskDeletion(task);
                _activeContextMenuTaskId = null; // Consume the result and close.
            }
            // If the popup was closed by other means (e.g., clicking outside), sync our state.
            else if (!UI.State.IsPopupOpen)
            {
                _activeContextMenuTaskId = null;
            }
        }

        var finalStyle = isHovering ? taskTheme.Hover : taskTheme.Normal;
        finalStyle.BorderLengthLeft *= scale;

        context.Renderer.DrawBox(taskBounds, finalStyle);
        var textBounds = new Vortice.Mathematics.Rect(taskBounds.X + (15 * scale), taskBounds.Y, taskBounds.Width - (30 * scale), taskBounds.Height);

        // We need to pass the render style to the primitive, not the measurement style.
        UI.DrawTextPrimitive(textBounds, task.Text, renderTextStyle, textAlign, Vector2.Zero);

        context.Layout.AdvanceLayout(new Vector2(logicalWidth, logicalHeight));
    }


    private void DrawDragPlaceholder(KanbanTask task, float logicalWidth)
    {
        var scale = UI.Context.UIScale;
        var logicalFontSize = 14f;
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = logicalFontSize * scale };
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