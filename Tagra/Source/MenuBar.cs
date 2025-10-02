using DirectUI;
using DirectUI.Drawing;
using System;
using System.Numerics;
using Vortice.Mathematics;

namespace Tagra;

public class MenuBar
{
    private readonly App _app;

    public const float MenuBarHeight = 30f;

    public MenuBar(App app)
    {
        _app = app;
    }

    public void Draw()
    {
        var context = UI.Context;
        var state = UI.State;
        var windowWidth = context.Renderer.RenderTargetSize.X / context.UIScale;

        var menuBarStyle = new BoxStyle { FillColor = new(50, 50, 50, 255), BorderLength = 0 };
        UI.Box("menu_bar_bg", new Vector2(windowWidth, MenuBarHeight), menuBarStyle);

        UI.BeginHBoxContainer("menu_bar_hbox", Vector2.Zero, gap: 0);

        var menuButtonTheme = new ButtonStylePack { Roundness = 0, BorderLength = 0 };
        menuButtonTheme.Normal.FillColor = DirectUI.Drawing.Colors.Transparent;
        menuButtonTheme.Hover.FillColor = new DirectUI.Drawing.Color(255, 255, 255, 30);
        menuButtonTheme.Pressed.FillColor = new DirectUI.Drawing.Color(255, 255, 255, 50);

        if (UI.Button("menu_file", "File", new Vector2(50, MenuBarHeight), theme: menuButtonTheme))
        {
            // TODO: Open File Menu
        }

        // --- Edit Button & Dropdown Logic ---
        int editMenuButtonId = "menu_edit".GetHashCode();
        var editButtonPos = UI.Context.Layout.GetCurrentPosition();

        if (UI.Button("menu_edit", "Edit", new Vector2(50, MenuBarHeight), theme: menuButtonTheme))
        {
            // If the popup is already open for this button, clicking again closes it.
            if (state.IsPopupOpen && state.ActivePopupId == editMenuButtonId)
            {
                state.ClearActivePopup();
            }
            else
            {
                state.ClearActivePopup(); // Close any other popups first

                // Define popup properties (using logical units)
                var popupPos = new Vector2(editButtonPos.X, editButtonPos.Y + MenuBarHeight);
                var popupSize = new Vector2(150, 60);
                // The bounds for click-away detection must be in physical units.
                var popupBoundsPhysical = new Rect(popupPos.X * context.UIScale, popupPos.Y * context.UIScale, popupSize.X * context.UIScale, popupSize.Y * context.UIScale);

                // Define the drawing logic for the popup
                Action<UIContext> drawCallback = (ctx) =>
                {
                    // Calculate physical bounds inside the callback to use the correct context
                    var scaledPopupBounds = new Rect(popupPos.X * ctx.UIScale, popupPos.Y * ctx.UIScale, popupSize.X * ctx.UIScale, popupSize.Y * ctx.UIScale);

                    var popupStyle = new BoxStyle { FillColor = new(60, 60, 60, 255), BorderColor = new(80, 80, 80, 255), BorderLength = 1f, Roundness = 0f };
                    ctx.Renderer.DrawBox(scaledPopupBounds, popupStyle);

                    var itemTheme = new ButtonStylePack { Roundness = 0f, BorderLength = 0f };
                    itemTheme.Normal.FillColor = DirectUI.Drawing.Colors.Transparent;
                    itemTheme.Hover.FillColor = DefaultTheme.HoverFill;
                    itemTheme.Pressed.FillColor = DefaultTheme.Accent;

                    var itemHeight = 30 * ctx.UIScale;

                    var manageTagsBounds = new Rect(scaledPopupBounds.X, scaledPopupBounds.Y, scaledPopupBounds.Width, itemHeight);
                    if (UI.DrawButtonPrimitive("manage_tags_item".GetHashCode(), manageTagsBounds, "Manage Tags...", itemTheme, false, new Alignment(HAlignment.Left, VAlignment.Center), Button.ActionMode.Release, Button.ClickBehavior.Left, new Vector2(5 * ctx.UIScale, 0), out _, out _) != ClickResult.None)
                    {
                        _app.ManageTagsRequested = true;
                        state.ClearActivePopup();
                    }

                    var settingsBounds = new Rect(scaledPopupBounds.X, scaledPopupBounds.Y + itemHeight, scaledPopupBounds.Width, itemHeight);
                    if (UI.DrawButtonPrimitive("settings_item".GetHashCode(), settingsBounds, "Settings...", itemTheme, false, new Alignment(HAlignment.Left, VAlignment.Center), Button.ActionMode.Release, Button.ClickBehavior.Left, new Vector2(5 * ctx.UIScale, 0), out _, out _) != ClickResult.None)
                    {
                        _app.SettingsRequested = true;
                        state.ClearActivePopup();
                    }
                };

                state.SetActivePopup(editMenuButtonId, drawCallback, popupBoundsPhysical);
            }
        }
        UI.EndHBoxContainer();
    }
}