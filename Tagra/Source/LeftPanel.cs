using DirectUI;
using System.Numerics;

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

        // Define the custom style for tag buttons
        var tagButtonStyle = new ButtonStylePack { Roundness = 0.2f, BorderLength = 0f };
        tagButtonStyle.Normal.FillColor = Colors.Transparent;
        tagButtonStyle.Normal.BorderColor = Colors.Transparent;
        tagButtonStyle.Hover.FillColor = new Color(255, 255, 255, 20); // Subtle hover
        tagButtonStyle.Hover.BorderColor = Colors.Transparent;
        tagButtonStyle.Pressed.FontColor = Colors.White;
        tagButtonStyle.Active.FontColor = Colors.White;
        tagButtonStyle.Active.FillColor = DefaultTheme.Accent; // Use accent color for the active/selected tag
        tagButtonStyle.Active.BorderColor = DefaultTheme.AccentBorder;
        tagButtonStyle.ActiveHover.FontColor = Colors.White; // Keep active style on hover
        tagButtonStyle.ActiveHover.FillColor = DefaultTheme.Accent;
        tagButtonStyle.ActiveHover.BorderColor = DefaultTheme.AccentBorder;

        foreach (var tag in _app.AllTags)
        {
            UI.BeginHBoxContainer($"tag_hbox_{tag.Id}", UI.Context.Layout.GetCurrentPosition(), gap: 5);
            if (UI.Button(
                id: $"tag_btn_{tag.Id}",
                text: $"{tag.Name} ({tag.FileCount})",
                size: new Vector2(scrollInnerWidth - 30, 24),
                theme: tagButtonStyle,
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
            // The delete button is now in the Manage Tags window.
            UI.Box("delete_placeholder", new Vector2(24, 24), new BoxStyle { FillColor = Colors.Transparent, BorderLength = 0f });

            UI.EndHBoxContainer();
        }
        UI.EndScrollableRegion();

        UI.EndVBoxContainer();
        UI.EndResizableVPanel();
    }
}