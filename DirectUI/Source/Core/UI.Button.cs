using System.Numerics;
using DirectUI.Animation;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1; // Still used for AntialiasMode enum

namespace DirectUI;

public static partial class UI
{
    public static bool Button(
        string id,
        string text,
        Vector2 size = default,
        ButtonStylePack? theme = null,
        bool disabled = false,
        bool autoWidth = false,
        Vector2? textMargin = null,
        Button.ActionMode clickMode = DirectUI.Button.ActionMode.Release,
        Button.ClickBehavior clickBehavior = DirectUI.Button.ClickBehavior.Left,
        Alignment? textAlignment = null,
        Vector2? textOffset = null,
        Vector2? origin = null, // This parameter is now unused but kept for compatibility to avoid breaking changes elsewhere. It will be removed in the future.
        object? userData = null,
        bool isActive = false,
        int layer = 1,
        AnimationInfo? animation = null)
    {
        if (!IsContextValid()) return false;
        var scale = Context.UIScale;

        int intId = id.GetHashCode();
        var finalTheme = theme ?? State.GetOrCreateElement<ButtonStylePack>(HashCode.Combine(intId, "theme"));
        State.SetUserData(intId, userData);

        Vector2 logicalSize = size == default ? new Vector2(84, 28) : size;

        // Auto-width calculation must happen before culling.
        if (autoWidth)
        {
            var styleForMeasuring = new ButtonStyle(finalTheme.Normal) { FontSize = finalTheme.Normal.FontSize * scale };
            Vector2 measuredSize = Context.TextService.MeasureText(text, styleForMeasuring) / scale; // Unscale to logical
            Vector2 margin = textMargin ?? new Vector2(10, 5);
            logicalSize.X = measuredSize.X + margin.X * 2;
        }

        Vector2 finalSize = logicalSize * scale;
        Vector2 drawPos = Context.Layout.ApplyLayout(origin ?? Vector2.Zero);


        // New: Automatically adjust vertical position for HBox alignment
        if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is HBoxContainerState hbox)
        {
            if (hbox.VerticalAlignment != VAlignment.Top && hbox.FixedRowHeight.HasValue)
            {
                float yOffset = 0;
                switch (hbox.VerticalAlignment)
                {
                    case VAlignment.Center:
                        yOffset = (hbox.FixedRowHeight.Value - logicalSize.Y) / 2f;
                        break;
                    case VAlignment.Bottom:
                        yOffset = hbox.FixedRowHeight.Value - logicalSize.Y;
                        break;
                }
                drawPos.Y += yOffset * scale;
            }
        }

        Rect widgetBounds = new Rect(drawPos.X, drawPos.Y, finalSize.X, finalSize.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(logicalSize);
            return false;
        }

        bool pushedClip = false;
        if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is GridContainerState grid)
        {
            float clipStartY = grid.CurrentDrawPosition.Y;
            float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
            float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
            Rect cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
            if (cellClipRect.Width > 0 && cellClipRect.Height > 0)
            {
                Context.Renderer.PushClipRect(cellClipRect, D2D.AntialiasMode.Aliased);
                pushedClip = true;
            }
        }

        ClickResult clickResult = DrawButtonPrimitive(
            intId,
            widgetBounds,
            text,
            finalTheme,
            disabled,
            textAlignment ?? new Alignment(HAlignment.Center, VAlignment.Center),
            clickMode,
            clickBehavior,
            (textOffset ?? Vector2.Zero) * scale,
            out _,
            out _,
            isActive: isActive,
            layer: layer,
            animation: animation
        );

        if (pushedClip)
        {
            Context.Renderer.PopClipRect();
        }

        Context.Layout.AdvanceLayout(logicalSize);
        return clickResult != ClickResult.None;
    }
}