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

        // Get logical position for drag'n'drop start.
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

        var taskTheme = new ButtonStylePack { Roundness = 0.1f };
        var cardBackground = new Color(42, 42, 42, 255);
        var hoverBackground = new Color(60, 60, 60, 255);

        // This style will hold font properties, including color, which we'll apply to all states in the theme.
        var fontStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = logicalFontSize };

        if (_settings.ColorStyle == TaskColorStyle.Background)
        {
            taskTheme.Normal.FillColor = task.Color;
            taskTheme.Normal.BorderColor = Colors.Transparent;
            fontStyle.FontColor = new Color(18, 18, 18, 255); // Dark text on colored background
        }
        else // Border style
        {
            taskTheme.Normal.FillColor = cardBackground;
            taskTheme.Normal.BorderColor = task.Color;
            taskTheme.Normal.BorderLengthLeft = 4f;
            taskTheme.Normal.BorderLengthTop = 0;
            taskTheme.Normal.BorderLengthRight = 0;
            taskTheme.Normal.BorderLengthBottom = 0;
            fontStyle.FontColor = DefaultTheme.Text; // Light text on dark background
        }

        // Apply hover state and font properties to all theme states
        taskTheme.Hover.FillColor = hoverBackground;
        var allStyles = new[] { taskTheme.Normal, taskTheme.Hover, taskTheme.Pressed, taskTheme.Disabled, taskTheme.Focused, taskTheme.Active, taskTheme.ActiveHover };
        foreach (var s in allStyles)
        {
            s.FontName = fontStyle.FontName;
            s.FontSize = fontStyle.FontSize;
            s.FontColor = fontStyle.FontColor;
        }


        // Add animation properties to the theme
        taskTheme.Animation = new AnimationInfo(0.15f, Easing.EaseOutQuad);
        taskTheme.Hover.Scale = new Vector2(0.98f, 0.98f);
        taskTheme.Pressed.Scale = new Vector2(0.95f, 0.95f);

        // --- Interaction & Animation Logic (inspired by UI.DrawButtonPrimitive) ---
        int taskIdHash = task.Id.GetHashCode();
        bool isInteractive = !UI.State.IsPopupOpen && !_modalManager.IsModalOpen;
        bool isHovering = isInteractive && taskBounds.Contains(context.InputState.MousePosition);

        if (isHovering)
        {
            UI.State.SetPotentialInputTarget(taskIdHash);
        }

        if (context.InputState.WasLeftMousePressedThisFrame && isHovering && UI.State.PotentialInputTargetId == taskIdHash && UI.State.TrySetActivePress(taskIdHash, 1))
        {
            _dragDropHandler.BeginDrag(task, column, context.InputState.MousePosition, physicalPos);
        }

        bool isPressed = UI.State.ActivelyPressedElementId == taskIdHash;
        taskTheme.UpdateCurrentStyle(isHovering, isPressed, !isInteractive, false, false);
        ButtonStyle targetStyle = taskTheme.Current;

        ButtonStyle animatedStyle;
        AnimationInfo? finalAnimation = targetStyle.Animation ?? taskTheme.Animation;

        if (finalAnimation != null && isInteractive)
        {
            var animManager = UI.State.AnimationManager;
            var currentTime = context.TotalTime;
            animatedStyle = new ButtonStyle(targetStyle)
            {
                FillColor = animManager.GetOrAnimate(HashCode.Combine(taskIdHash, "FillColor"), targetStyle.FillColor, currentTime, finalAnimation.Duration, finalAnimation.Easing),
                BorderColor = animManager.GetOrAnimate(HashCode.Combine(taskIdHash, "BorderColor"), targetStyle.BorderColor, currentTime, finalAnimation.Duration, finalAnimation.Easing),
                BorderLengthLeft = animManager.GetOrAnimate(HashCode.Combine(taskIdHash, "BorderLengthLeft"), targetStyle.BorderLengthLeft, currentTime, finalAnimation.Duration, finalAnimation.Easing),
                Scale = animManager.GetOrAnimate(HashCode.Combine(taskIdHash, "Scale"), targetStyle.Scale, currentTime, finalAnimation.Duration, finalAnimation.Easing)
            };
        }
        else
        {
            animatedStyle = targetStyle;
        }

        // --- Drawing ---
        Vector2 center = new Vector2(taskBounds.X + taskBounds.Width / 2f, taskBounds.Y + taskBounds.Height / 2f);
        float renderWidth = taskBounds.Width * animatedStyle.Scale.X;
        float renderHeight = taskBounds.Height * animatedStyle.Scale.Y;
        var renderBounds = new Vortice.Mathematics.Rect(center.X - renderWidth / 2f, center.Y - renderHeight / 2f, renderWidth, renderHeight);

        var renderBoxStyle = new ButtonStyle(animatedStyle);
        renderBoxStyle.BorderLengthLeft *= scale;
        context.Renderer.DrawBox(renderBounds, renderBoxStyle);

        var textBounds = new Vortice.Mathematics.Rect(taskBounds.X + (15 * scale), taskBounds.Y, taskBounds.Width - (30 * scale), taskBounds.Height);
        var renderTextStyle = new ButtonStyle
        {
            FontName = animatedStyle.FontName,
            FontSize = animatedStyle.FontSize * scale,
            FontColor = animatedStyle.FontColor
        };
        var textAlign = _settings.TextAlign == TaskTextAlign.Left ? new Alignment(HAlignment.Left, VAlignment.Center) : new Alignment(HAlignment.Center, VAlignment.Center);
        UI.DrawTextPrimitive(textBounds, task.Text, renderTextStyle, textAlign, Vector2.Zero);


        // --- Context Menu Logic (unchanged) ---
        if (isInteractive && UI.BeginContextMenu(task.Id))
        {
            _activeContextMenuTaskId = task.Id;
        }
        if (_activeContextMenuTaskId == task.Id)
        {
            var choice = UI.ContextMenu($"context_menu_{task.Id}", new[] { "Edit Task", "Delete Task" });
            if (choice != -1)
            {
                if (choice == 0) _modalManager.OpenEditTaskModal(task);
                else if (choice == 1) _modalManager.RequestTaskDeletion(task);
                _activeContextMenuTaskId = null;
            }
            else if (!UI.State.IsPopupOpen)
            {
                _activeContextMenuTaskId = null;
            }
        }

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