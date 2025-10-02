using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

/// <summary>
/// Provides the internal logic for the combobox widget, including managing popup state.
/// </summary>
internal class InternalComboboxLogic
{
    public int UpdateAndDraw(
        int id,
        int selectedIndex,
        string[] items,
        Vector2 position,
        Vector2 size,
        ButtonStylePack? theme,
        bool disabled)
    {
        var context = UI.Context;
        var state = UI.State;
        var renderer = context.Renderer; // Get the renderer from the context

        int newSelectedIndex = selectedIndex;

        // If a selection was made in the popup during the last frame's EndFrame,
        // the result will be available now for us to consume.
        if (state.PopupResultAvailable && state.PopupResultOwnerId == id)
        {
            newSelectedIndex = state.PopupResult;
        }

        var comboboxState = state.GetOrCreateElement<ComboboxState>(id);
        var themeId = HashCode.Combine(id, "theme");
        var finalTheme = theme ?? state.GetOrCreateElement<ButtonStylePack>(themeId);

        // Sync local open state with global popup state
        if (comboboxState.IsOpen && state.ActivePopupId != id)
        {
            comboboxState.IsOpen = false;
        }

        // The text on the button should reflect the potentially new index
        string currentItemText = (newSelectedIndex >= 0 && newSelectedIndex < items.Length)
            ? items[newSelectedIndex]
            : string.Empty;

        // Draw the main button
        var bounds = new Rect(position.X, position.Y, size.X, size.Y);
        bool clicked = UI.DrawButtonPrimitive(
            id,
            bounds,
            currentItemText,
            finalTheme,
            disabled,
            new Alignment(HAlignment.Left, VAlignment.Center),
            Button.ActionMode.Release,
            Button.ClickBehavior.Left,
            new Vector2(5, 0),
            out _,
            out _
        ) != ClickResult.None;

        if (clicked && !disabled)
        {
            if (comboboxState.IsOpen)
            {
                // If it was already open for us, clicking again closes it.
                state.ClearActivePopup();
                comboboxState.IsOpen = false;
            }
            else
            {
                // Request to open the popup.
                // First, ensure any other popups are closed.
                state.ClearActivePopup();

                // Calculate popup properties
                float popupY = position.Y + size.Y + 2;
                float itemHeight = size.Y; // Items are same height as the button
                float popupHeight = items.Length * itemHeight;
                var popupBounds = new Rect(position.X, popupY, size.X, popupHeight);

                // Define the draw callback for the popup, which runs at EndFrame
                Action<UIContext> drawCallback = (ctx) =>
                {
                    // Draw popup background using the renderer
                    var popupStyle = new BoxStyle { FillColor = DefaultTheme.NormalFill, BorderColor = DefaultTheme.FocusBorder, BorderLength = 1f, Roundness = 0f };
                    ctx.Renderer.DrawBox(popupBounds, popupStyle);

                    // Draw items
                    for (int i = 0; i < items.Length; i++)
                    {
                        var itemBounds = new Rect(popupBounds.X, popupBounds.Y + i * itemHeight, popupBounds.Width, itemHeight);
                        var itemTheme = new ButtonStylePack { Roundness = 0f, BorderLength = 0f };
                        itemTheme.Normal.FillColor = DirectUI.Drawing.Colors.Transparent;
                        itemTheme.Hover.FillColor = DefaultTheme.HoverFill;
                        itemTheme.Pressed.FillColor = DefaultTheme.Accent;

                        int itemId = HashCode.Combine(id, "item", i);

                        if (UI.DrawButtonPrimitive(itemId, itemBounds, items[i], itemTheme, false, new Alignment(HAlignment.Left, VAlignment.Center), Button.ActionMode.Release, Button.ClickBehavior.Left, new Vector2(5, 0), out _, out _) != ClickResult.None)
                        {
                            // A selection was made. Post the result to be picked up next frame.
                            state.SetPopupResult(id, i);

                            // Close the popup.
                            state.ClearActivePopup();
                        }
                    }
                };

                state.SetActivePopup(id, drawCallback, popupBounds);
                comboboxState.IsOpen = true;
            }
        }

        return newSelectedIndex;
    }
}