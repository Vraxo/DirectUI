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

    private readonly record struct InteractionState(bool IsHovering, bool IsPressed, bool IsInteractive);

    public TaskRenderer(KanbanSettings settings, ModalManager modalManager, DragDropHandler dragDropHandler)
    {
        _settings = settings;
        _modalManager = modalManager;
        _dragDropHandler = dragDropHandler;
    }

    public void DrawTaskWidget(KanbanColumn column, Task task, int taskIndex, float logicalWidth)
    {
        (Vector2 logicalSize, Vector2 physicalPos, Vortice.Mathematics.Rect taskBounds) = CalculateTaskGeometry(task, logicalWidth);

        _dragDropHandler.UpdateDropTarget(column, taskIndex, taskBounds);

        if (!UI.Context.Layout.IsRectVisible(taskBounds))
        {
            UI.Context.Layout.AdvanceLayout(logicalSize);
            return;
        }

        ButtonStylePack taskTheme = CreateTaskTheme(task);
        InteractionState interaction = HandleInteraction(task, column, physicalPos, taskBounds);

        HandleContextMenu(task, interaction);

        DrawAnimatedTask(task, taskTheme, interaction, taskBounds);

        UI.Context.Layout.AdvanceLayout(logicalSize);
    }

    private static (Vector2 logicalSize, Vector2 physicalPos, Vortice.Mathematics.Rect taskBounds) CalculateTaskGeometry(Task task, float logicalWidth)
    {
        Vector2 logicalSize = CalculateTaskLogicalSize(task, logicalWidth);
        float scale = UI.Context.UIScale;

        Vector2 logicalPos = UI.Context.Layout.GetCurrentPosition();
        Vector2 physicalPos = logicalPos * scale;
        Vector2 physicalSize = new(logicalWidth * scale, logicalSize.Y * scale);
        Vortice.Mathematics.Rect taskBounds = new(physicalPos.X, physicalPos.Y, physicalSize.X, physicalSize.Y);

        return (logicalSize, physicalPos, taskBounds);
    }

    private static Vector2 CalculateTaskLogicalSize(Task task, float logicalWidth)
    {
        UIContext context = UI.Context;
        float scale = context.UIScale;
        float logicalFontSize = 14f;

        ButtonStyle measurementStyle = new() { FontName = "Segoe UI", FontSize = logicalFontSize * scale };
        ITextLayout wrappedLayout = context.TextService.GetTextLayout(task.Text, measurementStyle, new Vector2((logicalWidth - 30) * scale, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float logicalHeight = (wrappedLayout.Size.Y / scale) + 30;

        return new (logicalWidth, logicalHeight);
    }

    private ButtonStylePack CreateTaskTheme(Task task)
    {
        ButtonStylePack taskTheme = new() 
        { 
            Roundness = 0.1f 
        };

        Color cardBackground = new(42, 42, 42, 255);

        ButtonStyle fontStyle = new()
        {
            FontName = "Segoe UI",
            FontSize = 14f
        };

        if (_settings.ColorStyle == TaskColorStyle.Background)
        {
            taskTheme.Normal.FillColor = task.Color;
            taskTheme.Normal.BorderColor = Colors.Transparent;
            fontStyle.FontColor = new Color(18, 18, 18, 255);

            Color baseHoverColor = task.Color;
            
            taskTheme.Hover.FillColor = new Color(
                (byte)float.Min(255, baseHoverColor.R * 1.2f),
                (byte)float.Min(255, baseHoverColor.G * 1.2f),
                (byte)float.Min(255, baseHoverColor.B * 1.2f),
                baseHoverColor.A);
        }
        else
        {
            taskTheme.Normal.FillColor = cardBackground;
            taskTheme.Normal.BorderColor = task.Color;
            taskTheme.Normal.BorderLengthLeft = 4f;
            taskTheme.Normal.BorderLengthTop = 0;
            taskTheme.Normal.BorderLengthRight = 0;
            taskTheme.Normal.BorderLengthBottom = 0;
            fontStyle.FontColor = DefaultTheme.Text;
            taskTheme.Hover.FillColor = new Color(60, 60, 60, 255);
        }

        ButtonStyle[] allStyles = [taskTheme.Normal, taskTheme.Hover, taskTheme.Pressed, taskTheme.Disabled, taskTheme.Focused, taskTheme.Active, taskTheme.ActiveHover];
       
        foreach (ButtonStyle s in allStyles)
        {
            s.FontName = fontStyle.FontName;
            s.FontSize = fontStyle.FontSize;
            s.FontColor = fontStyle.FontColor;
        }

        taskTheme.Animation = new AnimationInfo(0.15f, Easing.EaseOutQuad);
        taskTheme.Hover.Scale = new Vector2(0.98f, 0.98f);
        taskTheme.Pressed.Scale = new Vector2(0.95f, 0.95f);

        return taskTheme;
    }

    private InteractionState HandleInteraction(Task task, KanbanColumn column, Vector2 physicalPos, Vortice.Mathematics.Rect taskBounds)
    {
        UIContext context = UI.Context;
        bool isInteractive = !UI.State.IsPopupOpen && !_modalManager.IsModalOpen;
        bool isHovering = isInteractive && taskBounds.Contains(context.InputState.MousePosition);
        int taskIdHash = task.Id.GetHashCode();

        if (isHovering)
        {
            UI.State.SetPotentialInputTarget(taskIdHash);
        }

        if (context.InputState.WasLeftMousePressedThisFrame && isHovering && UI.State.PotentialInputTargetId == taskIdHash && UI.State.TrySetActivePress(taskIdHash, 1))
        {
            _dragDropHandler.BeginDrag(task, column, context.InputState.MousePosition, physicalPos);
        }

        bool isPressed = UI.State.ActivelyPressedElementId == taskIdHash;

        return new(isHovering, isPressed, isInteractive);
    }

    private void HandleContextMenu(Task task, InteractionState interaction)
    {
        if (interaction.IsInteractive && UI.BeginContextMenu(task.Id))
        {
            _activeContextMenuTaskId = task.Id;
        }

        if (_activeContextMenuTaskId != task.Id)
        {
            return;
        }

        int choice = UI.ContextMenu($"context_menu_{task.Id}", ["Edit Task", "Delete Task"]);

        switch (choice)
        {
            case -1:
                if (UI.State.IsPopupOpen)
                {
                    break;
                }

                _activeContextMenuTaskId = null;
                break;
            case 0:
                _modalManager.OpenEditTaskModal(task);
                break;
            case 1:
                _modalManager.RequestTaskDeletion(task);
                break;
        }
    }

    private static ButtonStyle CalculateAnimatedStyle(int taskIdHash, ButtonStylePack theme, InteractionState interaction)
    {
        theme.UpdateCurrentStyle(interaction.IsHovering, interaction.IsPressed, !interaction.IsInteractive, false, false);
        ButtonStyle targetStyle = theme.Current;
        AnimationInfo? finalAnimation = targetStyle.Animation ?? theme.Animation;

        if (finalAnimation is null || !interaction.IsInteractive)
        {
            return targetStyle;
        }

        AnimationManager animManager = UI.State.AnimationManager;
        float currentTime = UI.Context.TotalTime;

        return new(targetStyle)
        {
            FillColor = animManager.GetOrAnimate(HashCode.Combine(taskIdHash, "FillColor"), targetStyle.FillColor, currentTime, finalAnimation.Duration, finalAnimation.Easing),
            BorderColor = animManager.GetOrAnimate(HashCode.Combine(taskIdHash, "BorderColor"), targetStyle.BorderColor, currentTime, finalAnimation.Duration, finalAnimation.Easing),
            BorderLengthLeft = animManager.GetOrAnimate(HashCode.Combine(taskIdHash, "BorderLengthLeft"), targetStyle.BorderLengthLeft, currentTime, finalAnimation.Duration, finalAnimation.Easing),
            Scale = animManager.GetOrAnimate(HashCode.Combine(taskIdHash, "Scale"), targetStyle.Scale, currentTime, finalAnimation.Duration, finalAnimation.Easing)
        };
    }

    private void DrawAnimatedTask(Task task, ButtonStylePack theme, InteractionState interaction, Vortice.Mathematics.Rect taskBounds)
    {
        UIContext context = UI.Context;
        float scale = context.UIScale;
        int taskIdHash = task.Id.GetHashCode();

        ButtonStyle animatedStyle = CalculateAnimatedStyle(taskIdHash, theme, interaction);

        Vector2 center = new(taskBounds.X + taskBounds.Width / 2f, taskBounds.Y + taskBounds.Height / 2f);
        float renderWidth = taskBounds.Width * animatedStyle.Scale.X;
        float renderHeight = taskBounds.Height * animatedStyle.Scale.Y;
        Vortice.Mathematics.Rect renderBounds = new(center.X - renderWidth / 2f, center.Y - renderHeight / 2f, renderWidth, renderHeight);

        ButtonStyle renderBoxStyle = new(animatedStyle);
        renderBoxStyle.BorderLengthLeft *= scale;
        context.Renderer.DrawBox(renderBounds, renderBoxStyle);

        Vortice.Mathematics.Rect textBounds = new(taskBounds.X + (15 * scale), taskBounds.Y, taskBounds.Width - (30 * scale), taskBounds.Height);
        ButtonStyle renderTextStyle = new()
        {
            FontName = animatedStyle.FontName,
            FontSize = animatedStyle.FontSize * scale,
            FontColor = animatedStyle.FontColor
        };
        Alignment textAlign = GetTextAlignment();
        UI.DrawTextPrimitive(textBounds, task.Text, renderTextStyle, textAlign, Vector2.Zero);
    }

    private Alignment GetTextAlignment()
    {
        return _settings.TextAlign == TaskTextAlign.Left
            ? new(HAlignment.Left, VAlignment.Center)
            : new(HAlignment.Center, VAlignment.Center);
    }

    public static void DrawDragPlaceholder(Task task, float logicalWidth)
    {
        Vector2 logicalSize = CalculateTaskLogicalSize(task, logicalWidth);
        float scale = UI.Context.UIScale;

        Vector2 physicalSize = logicalSize * scale;
        Vector2 physicalPos = UI.Context.Layout.ApplyLayout(Vector2.Zero);
        Vortice.Mathematics.Rect bounds = new(physicalPos.X, physicalPos.Y, physicalSize.X, physicalSize.Y);

        BoxStyle style = new()
        {
            FillColor = new Color(0, 0, 0, 100),
            BorderColor = DefaultTheme.Accent,
            BorderLength = 1 * scale,
            Roundness = 0.1f
        };
        UI.Context.Renderer.DrawBox(bounds, style);
        UI.Context.Layout.AdvanceLayout(logicalSize);
    }

    public void DrawDropIndicator(KanbanColumn column, int index, float logicalWidth)
    {
        float scale = UI.Context.UIScale;
        if (!_dragDropHandler.IsDragging() || _dragDropHandler.DropTargetColumn != column || _dragDropHandler.DropIndex != index)
        {
            return;
        }

        Vector2 physicalPos = UI.Context.Layout.ApplyLayout(Vector2.Zero);
        Vortice.Mathematics.Rect indicatorRect = new(physicalPos.X, physicalPos.Y - (5 * scale), logicalWidth * scale, 4 * scale);

        BoxStyle style = new()
        {
            FillColor = DefaultTheme.Accent,
            BorderLength = 0,
            Roundness = 0.5f
        };

        UI.Context.Renderer.DrawBox(indicatorRect, style);
    }
}