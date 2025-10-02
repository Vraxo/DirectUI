using DirectUI;
using System.Numerics;
using DirectUI.Styling;

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
            UI.BeginHBoxContainer($"tag_hbox_{tag.Id}", UI.Context.Layout.GetCurrentPosition(), gap: 5, verticalAlignment: VAlignment.Center, fixedRowHeight: 24f);

            var color = ParseColorHex(tag.ColorHex);
            UI.Box($"tag_color_swatch_{tag.Id}", new Vector2(10, 10), new BoxStyle { FillColor = color, Roundness = 1.0f, BorderLength = 0f });

            if (UI.Button(
                id: $"tag_btn_{tag.Id}",
                text: $"{tag.Name} ({tag.FileCount})",
                size: new Vector2(scrollInnerWidth - 15, 24),
                theme: tagButtonStyle,
                textAlignment: new Alignment(HAlignment.Left, VAlignment.Center),
                isActive: _app.SearchText == tag.Name))
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

            UI.EndHBoxContainer();
        }
        UI.EndScrollableRegion();

        UI.EndVBoxContainer();
        UI.EndResizableVPanel();
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