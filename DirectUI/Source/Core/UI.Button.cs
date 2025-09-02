using System.Numerics;
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
        Vector2? origin = null,
        object? userData = null,
        bool isActive = false,
        int layer = 1)
    {
        if (!IsContextValid()) return false;

        int intId = id.GetHashCode();
        var finalTheme = theme ?? State.GetOrCreateElement<ButtonStylePack>(HashCode.Combine(intId, "theme"));
        State.SetUserData(intId, userData);

        Vector2 finalSize = size == default ? new Vector2(84, 28) : size;
        Vector2 finalOrigin = origin ?? Vector2.Zero;

        // Auto-width calculation must happen before culling.
        if (autoWidth)
        {
            var styleForMeasuring = finalTheme.Normal; // Measure against the normal style
            Vector2 measuredSize = Context.TextService.MeasureText(text, styleForMeasuring);
            Vector2 margin = textMargin ?? new Vector2(10, 5);
            finalSize.X = measuredSize.X + margin.X * 2;
        }

        Vector2 drawPos = Context.Layout.GetCurrentPosition();
        Rect widgetBounds = new Rect(drawPos.X - finalOrigin.X, drawPos.Y - finalOrigin.Y, finalSize.X, finalSize.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(finalSize);
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
            textOffset ?? Vector2.Zero,
            isActive: isActive,
            layer: layer
        );

        if (pushedClip)
        {
            Context.Renderer.PopClipRect();
        }

        Context.Layout.AdvanceLayout(finalSize);
        return clickResult != ClickResult.None;
    }
}