using DirectUI;
using System.Linq;
using System.Numerics;
using Tagra.Data;

namespace Tagra;

public class RightPanel
{
    private readonly App _app;

    public RightPanel(App app)
    {
        _app = app;
    }

    public void Draw()
    {
        var panelStyle = new BoxStyle { FillColor = new(40, 40, 40, 255), BorderLength = 0f };
        UI.BeginResizableVPanel("details_panel", ref _app.RightPanelWidth, HAlignment.Right, topOffset: MenuBar.MenuBarHeight, minWidth: 200, maxWidth: 500, panelStyle: panelStyle);

        var clipRect = UI.Context.Layout.GetCurrentClipRect();
        var innerWidth = clipRect.Width / UI.Context.UIScale;

        if (_app.SelectedFile is null)
        {
            UI.Text("no_selection_label", "Select a file to see details.", new Vector2(innerWidth, 40));
        }
        else
        {
            var availableHeight = clipRect.Height / UI.Context.UIScale;
            UI.BeginScrollableRegion("details_scroll", new Vector2(innerWidth, availableHeight), out var scrollInnerWidth);
            UI.BeginVBoxContainer("details_vbox", UI.Context.Layout.GetCurrentPosition(), gap: 5f);

            UI.WrappedText("selected_path", _app.SelectedFile.Path, new Vector2(scrollInnerWidth, 0));
            UI.Separator(scrollInnerWidth);
            UI.Text("assigned_tags_label", "Assigned Tags:");

            foreach (var tag in _app.SelectedFile.Tags)
            {
                UI.BeginHBoxContainer($"assigned_tag_{tag.Id}", UI.Context.Layout.GetCurrentPosition(), gap: 5);
                if (UI.Button($"remove_tag_{tag.Id}", "x", new Vector2(20, 20)))
                {
                    _app.DbManager.RemoveTagFromFile(_app.SelectedFile.Id, tag.Id);
                    _app.RefreshAllData();
                }
                UI.Text($"assigned_tag_name_{tag.Id}", tag.Name, new Vector2(scrollInnerWidth - 30, 20));
                UI.EndHBoxContainer();
            }

            UI.Separator(scrollInnerWidth);
            UI.Text("available_tags_label", "Available Tags to Add:");

            // Define the custom style for tag buttons
            var tagButtonStyle = new ButtonStylePack { Roundness = 0.2f, BorderLength = 0f };
            tagButtonStyle.Normal.FillColor = Colors.Transparent;
            tagButtonStyle.Normal.BorderColor = Colors.Transparent;
            tagButtonStyle.Hover.FillColor = new Color(255, 255, 255, 20); // Subtle hover
            tagButtonStyle.Hover.BorderColor = Colors.Transparent;
            tagButtonStyle.Pressed.FontColor = Colors.White;

            var availableTags = _app.AllTags.Except(_app.SelectedFile.Tags, new TagComparer());
            foreach (var tag in availableTags)
            {
                if (UI.Button(
                    id: $"add_tag_{tag.Id}",
                    text: tag.Name,
                    size: new Vector2(scrollInnerWidth, 24),
                    theme: tagButtonStyle))
                {
                    _app.DbManager.AddTagToFile(_app.SelectedFile.Id, tag.Id);
                    _app.RefreshAllData();
                }
            }

            UI.EndVBoxContainer();
            UI.EndScrollableRegion();
        }

        UI.EndResizableVPanel();
    }
}