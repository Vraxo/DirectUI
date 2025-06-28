using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    public static bool Button(
        int id,
        string text,
        Vector2 size = default,
        ButtonStylePack? theme = null,
        bool disabled = false,
        bool autoWidth = false,
        Vector2? textMargin = null,
        DirectUI.Button.ActionMode clickMode = DirectUI.Button.ActionMode.Release,
        Alignment? textAlignment = null,
        Vector2? textOffset = null,
        Vector2? origin = null,
        object? userData = null)
    {
        if (!IsContextValid()) return false;

        Button buttonInstance = State.GetOrCreateElement<Button>(id);
        buttonInstance.Position = Context.Layout.ApplyLayout(Vector2.Zero);

        // Configure the button instance from parameters
        buttonInstance.Text = text;
        buttonInstance.Size = size == default ? new Vector2(84, 28) : size;
        buttonInstance.Themes = theme ?? buttonInstance.Themes ?? new ButtonStylePack();
        buttonInstance.Disabled = disabled;
        buttonInstance.AutoWidth = autoWidth;
        buttonInstance.TextMargin = textMargin ?? new Vector2(10, 5);
        buttonInstance.LeftClickActionMode = clickMode;
        buttonInstance.TextAlignment = textAlignment ?? new Alignment(HAlignment.Center, VAlignment.Center);
        buttonInstance.TextOffset = textOffset ?? Vector2.Zero;
        buttonInstance.Origin = origin ?? Vector2.Zero;
        buttonInstance.UserData = userData;


        bool pushedClip = false;
        if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is GridContainerState grid)
        {
            float clipStartY = grid.CurrentDrawPosition.Y;
            float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
            float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
            Rect cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
            if (Context.RenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
            {
                Context.RenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased);
                pushedClip = true;
            }
        }

        bool clicked = buttonInstance.Update(id);
        if (pushedClip && Context.RenderTarget is not null)
        {
            Context.RenderTarget.PopAxisAlignedClip();
        }

        Context.Layout.AdvanceLayout(buttonInstance.Size);
        return clicked;
    }
}