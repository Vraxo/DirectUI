using DirectUI;
using System.Numerics;
using DirectUI.Styling;
using Tagra.Data;
using System.IO;
using Vortice.Mathematics;
using Color = DirectUI.Drawing.Color;

namespace Tagra;

public class LeftPanel
{
    private readonly App _app;

    public LeftPanel(App app)
    {
        _app = app;
    }

    public void Draw()
    {
        var panelStyle = new BoxStyle { FillColor = new(40, 40, 40, 255), BorderLength = 0f };
        UI.BeginResizableVPanel("main_layout", ref _app.LeftPanelWidth, HAlignment.Left, topOffset: MenuBar.MenuBarHeight, minWidth: 150, maxWidth: 400, panelStyle: panelStyle);

        var clipRect = UI.Context.Layout.GetCurrentClipRect();
        var innerWidth = clipRect.Width / UI.Context.UIScale;
        var availableHeight = clipRect.Height / UI.Context.UIScale;
        var currentY = UI.Context.Layout.GetCurrentPosition().Y;

        UI.BeginVBoxContainer("left_panel_vbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);

        UI.Text("search_label", "Search by Tags");
        if (UI.InputText("search_bar", ref _app.SearchText, new Vector2(innerWidth, 28f), placeholderText: "e.g., cat vacation").EnterPressed)
        {
            _app.HandleSearch(); // Trigger search explicitly on Enter
        }

        UI.Separator(innerWidth);
        UI.Text("tags_label", "All Tags");

        var scrollHeight = availableHeight - (UI.Context.Layout.GetCurrentPosition().Y - currentY);
        UI.BeginScrollableRegion("tags_scroll", new Vector2(innerWidth, scrollHeight), out var scrollInnerWidth);

        // Load tag button style from StyleManager
        var tagButtonStyle = StyleManager.Get<ButtonStylePack>("TagButton");

        foreach (var tag in _app.AllTags)
        {
            if (DrawTagButton(tag, scrollInnerWidth, _app.SearchText == tag.Name, tagButtonStyle))
            {
                if (_app.SearchText == tag.Name)
                {
                    _app.SearchText = ""; // Clicking the active tag deselects it
                }
                else
                {
                    _app.SearchText = tag.Name; // Click a tag to search for it
                }
            }
        }
        UI.EndScrollableRegion();

        UI.EndVBoxContainer();
        UI.EndResizableVPanel();
    }

    private bool DrawTagButton(Tag tag, float width, bool isActive, ButtonStylePack theme)
    {
        var context = UI.Context;
        var scale = context.UIScale;

        // 1. Define bounds and get style for the entire widget
        var logicalSize = new Vector2(width, 24f);
        var startPos = context.Layout.GetCurrentPosition();
        var physicalBounds = new Rect(startPos.X * scale, startPos.Y * scale, logicalSize.X * scale, logicalSize.Y * scale);

        // 2. Use DrawButtonPrimitive for interaction and background. It returns the animated state.
        var clickResult = UI.DrawButtonPrimitive(
            $"tag_btn_{tag.Id}".GetHashCode(),
            physicalBounds,
            "", // No text, we draw content ourselves
            theme,
            disabled: false,
            textAlignment: new Alignment(HAlignment.Left, VAlignment.Center),
            clickMode: Button.ActionMode.Release,
            clickBehavior: Button.ClickBehavior.Left,
            textOffset: Vector2.Zero,
            out var renderBounds,
            out var animatedStyle,
            isActive: isActive
        );

        // 3. Draw custom content (circle and text) inside the animated renderBounds
        float padding = 5f * scale;

        if (_app.Settings.TagDisplay == TagDisplayMode.Emoji && !string.IsNullOrEmpty(tag.Emoji))
        {
            var textRenderBounds = new Rect(renderBounds.X + padding, renderBounds.Y, renderBounds.Width - padding * 2, renderBounds.Height);
            UI.DrawTextPrimitive(
                textRenderBounds,
                $"{tag.Emoji} {tag.Name} ({tag.FileCount})",
                animatedStyle,
                new Alignment(HAlignment.Left, VAlignment.Center),
                Vector2.Zero
            );
        }
        else
        {
            // Draw circle
            float circleDiameter = 10f * scale;
            var circleCenterY = renderBounds.Y + renderBounds.Height / 2f;
            var circleRect = new Rect(renderBounds.X + padding, circleCenterY - circleDiameter / 2f, circleDiameter, circleDiameter);
            var circleColor = ParseColorHex(tag.ColorHex);
            var circleStyle = new BoxStyle { FillColor = circleColor, Roundness = 1.0f, BorderLength = 0f };
            context.Renderer.DrawBox(circleRect, circleStyle);

            // Draw text
            var textStartX = circleRect.Right + padding;
            var textRenderBounds = new Rect(
                textStartX,
                renderBounds.Y,
                renderBounds.Width - (textStartX - renderBounds.X) - padding, // Add padding on the right
                renderBounds.Height
            );

            UI.DrawTextPrimitive(
                textRenderBounds,
                $"{tag.Name} ({tag.FileCount})",
                animatedStyle, // Use the animated style for correct color, etc.
                new Alignment(HAlignment.Left, VAlignment.Center),
                Vector2.Zero
            );
        }

        // 4. Advance the layout manually
        context.Layout.AdvanceLayout(logicalSize);

        return clickResult != ClickResult.None;
    }


    private static Color ParseColorHex(string hex)
    {
        if (!string.IsNullOrEmpty(hex) && hex.StartsWith("#") && hex.Length == 7)
        {
            try
            {
                byte r = System.Convert.ToByte(hex.Substring(1, 2), 16);
                byte g = System.Convert.ToByte(hex.Substring(3, 2), 16);
                byte b = System.Convert.ToByte(hex.Substring(5, 2), 16);
                return new Color(r, g, b, 255);
            }
            catch
            {
                return DefaultTheme.Accent;
            }
        }
        return DefaultTheme.Accent;
    }
}