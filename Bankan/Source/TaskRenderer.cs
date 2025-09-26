using System.Numerics;
using DirectUI;
using DirectUI.Animation;
using DirectUI.Core;
using DirectUI.Drawing;

namespace Bankan.Rendering;

public class TaskRenderer
{
    private readonly KanbanSettings _settings;
    private readonly ModalManager _modalManager;
    private readonly DragDropHandler _dragDropHandler;
    private string? _activeContextMenuTaskId;

    public TaskRenderer(KanbanSettings settings, ModalManager modalManager, DragDropHandler dragDropHandler)
    {
        _settings = settings;
        _modalManager = modalManager;
        _dragDropHandler = dragDropHandler;
    }

    public void DrawTaskWidget(KanbanColumn column, Task task, int taskIndex, float logicalWidth)
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

        var renderTextStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = logicalFontSize };

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

        var renderBoxStyle = new ButtonStyle(finalStyle);
        // We only pass physical dimensions to the renderer, so scale the border length here
        renderBoxStyle.BorderLengthLeft *= scale;
        renderBoxStyle.FontSize *= scale;

        context.Renderer.DrawBox(taskBounds, renderBoxStyle);
        var textBounds = new Vortice.Mathematics.Rect(taskBounds.X + (15 * scale), taskBounds.Y, taskBounds.Width - (30 * scale), taskBounds.Height);

        renderTextStyle.FontSize *= scale;

        UI.DrawTextPrimitive(textBounds, task.Text, renderTextStyle, textAlign, Vector2.Zero);

        context.Layout.AdvanceLayout(new Vector2(logicalWidth, logicalHeight));
    }

    public void DrawDragPlaceholder(Task task, float logicalWidth)
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

    public void DrawDropIndicator(KanbanColumn column, int index, float logicalWidth)
    {
        var scale = UI.Context.UIScale;
        if (!_dragDropHandler.IsDragging() || _dragDropHandler.DropTargetColumn != column || _dragDropHandler.DropIndex != index) return;
        var physicalPos = UI.Context.Layout.ApplyLayout(Vector2.Zero);
        var indicatorRect = new Vortice.Mathematics.Rect(physicalPos.X, physicalPos.Y - (5 * scale), logicalWidth * scale, 4 * scale);
        var style = new BoxStyle { FillColor = DefaultTheme.Accent, BorderLength = 0, Roundness = 0.5f };
        UI.Context.Renderer.DrawBox(indicatorRect, style);
    }
}