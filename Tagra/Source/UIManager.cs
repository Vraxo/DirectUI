using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DirectUI;
using DirectUI.Core;
using Tagra.Data;
using Vortice.Mathematics;

namespace Tagra;

public class UIManager
{
    private readonly App _app;

    public UIManager(App app)
    {
        _app = app;
    }

    public void DrawLayout()
    {
        DrawLeftPanel();
        DrawMainContentPanel();
        DrawRightPanel();
    }

    private void DrawLeftPanel()
    {
        var panelStyle = new BoxStyle { FillColor = new(40, 40, 40, 255), BorderLength = 0f };
        UI.BeginResizableVPanel("main_layout", ref _app.LeftPanelWidth, HAlignment.Left, minWidth: 150, maxWidth: 400, panelStyle: panelStyle);

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

        // --- New Tag Creation ---
        UI.BeginHBoxContainer("new_tag_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 5f);
        UI.InputText("new_tag_input", ref _app.NewTagName, new Vector2(innerWidth - 55, 28f), placeholderText: "New Tag Name");
        if (UI.Button("create_tag_btn", "Add", new Vector2(50, 28)))
        {
            if (!string.IsNullOrWhiteSpace(_app.NewTagName))
            {
                _app.DbManager.AddTag(_app.NewTagName);
                _app.NewTagName = "";
                _app.RefreshAllData();
            }
        }
        UI.EndHBoxContainer();
        // --- End New Tag Creation ---

        UI.Separator(innerWidth);
        UI.Text("tags_label", "All Tags");

        var scrollHeight = availableHeight - (UI.Context.Layout.GetCurrentPosition().Y - currentY);
        UI.BeginScrollableRegion("tags_scroll", new Vector2(innerWidth, scrollHeight), out var scrollInnerWidth);
        foreach (var tag in _app.AllTags)
        {
            UI.BeginHBoxContainer($"tag_hbox_{tag.Id}", UI.Context.Layout.GetCurrentPosition(), gap: 5);
            if (UI.Button($"tag_btn_{tag.Id}", $"{tag.Name} ({tag.FileCount})", new Vector2(scrollInnerWidth - 30, 24)))
            {
                _app.SearchText = tag.Name; // Click a tag to search for it
            }
            if (UI.Button($"delete_tag_btn_{tag.Id}", "x", new Vector2(24, 24)))
            {
                if (!_app.TagIdToDelete.HasValue) // Prevent opening multiple dialogs
                {
                    _app.TagIdToDelete = tag.Id;
                }
            }
            UI.EndHBoxContainer();
        }
        UI.EndScrollableRegion();

        UI.EndVBoxContainer();
        UI.EndResizableVPanel();
    }

    private void DrawMainContentPanel()
    {
        var context = UI.Context;
        var scale = context.UIScale;
        var windowWidth = context.Renderer.RenderTargetSize.X;
        var windowHeight = context.Renderer.RenderTargetSize.Y;

        var mainPanelX = _app.LeftPanelWidth + 1;
        var mainPanelWidth = (windowWidth / scale) - mainPanelX - _app.RightPanelWidth;
        var mainPanelPos = new Vector2(mainPanelX, 0);

        UI.BeginVBoxContainer("main_content_vbox", mainPanelPos, gap: 10);

        if (UI.Button("add_file_btn", "Add File...", new Vector2(120, 28)))
        {
            var filters = new Dictionary<string, string>
            {
                { "Image Files", "jpg,jpeg,png,bmp,gif" },
                { "Document Files", "txt,pdf,doc,docx" }
            };
            string? path = FileDialogs.OpenFile(filters);
            if (!string.IsNullOrWhiteSpace(path))
            {
                _app.DbManager.AddFile(path);
                _app.RefreshAllData();
            }
        }

        UI.Separator(mainPanelWidth);

        var scrollPos = context.Layout.GetCurrentPosition();
        var scrollHeight = (windowHeight / scale) - scrollPos.Y;
        UI.BeginScrollArea("files_scroll_area", new Vector2(mainPanelWidth, scrollHeight));

        const float itemWidth = 120;
        const float itemHeight = 150;
        const float gridGap = 10;
        int numColumns = Math.Max(1, (int)((mainPanelWidth - gridGap) / (itemWidth + gridGap)));

        UI.BeginGridContainer("files_grid", Vector2.Zero, new Vector2(mainPanelWidth - 20, 10000), numColumns, new Vector2(gridGap, gridGap));

        foreach (var file in _app.DisplayedFiles)
        {
            DrawFileGridItem(file, new Vector2(itemWidth, itemHeight));
        }

        UI.EndGridContainer();
        UI.EndScrollArea();
        UI.EndVBoxContainer(advanceParentLayout: false);
    }

    private void DrawFileGridItem(FileEntry file, Vector2 logicalSize)
    {
        var context = UI.Context;
        var scale = context.UIScale;
        var startPos = context.Layout.GetCurrentPosition();
        var isSelected = _app.SelectedFile?.Id == file.Id;

        var itemBounds = new Rect(startPos.X * scale, startPos.Y * scale, logicalSize.X * scale, logicalSize.Y * scale);

        // Manual Hit-testing for selection
        if (itemBounds.Contains(context.InputState.MousePosition) && context.InputState.WasLeftMousePressedThisFrame)
        {
            _app.SelectedFile = file;
            _app.RefreshDataForSelectedFile();
        }

        // --- Visuals ---
        UI.BeginVBoxContainer($"file_item_{file.Id}", startPos, gap: 4);

        // Thumbnail or placeholder
        byte[]? thumbData = _app.ThumbnailService.GetThumbnail(file.Path, 100);
        if (thumbData != null)
        {
            UI.Image($"thumb_{file.Id}", thumbData, new Vector2(logicalSize.X, 100));
        }
        else
        {
            var boxStyle = new BoxStyle { FillColor = new(60, 60, 60, 255), Roundness = 0.1f };
            UI.Box($"placeholder_{file.Id}", new Vector2(logicalSize.X, 100), boxStyle);
        }

        // Filename
        UI.WrappedText($"filename_{file.Id}", Path.GetFileName(file.Path), new Vector2(logicalSize.X, logicalSize.Y - 104));

        UI.EndVBoxContainer(advanceParentLayout: false); // We are doing custom layout advancement

        // Draw selection highlight manually
        if (isSelected)
        {
            var highlightStyle = new BoxStyle
            {
                FillColor = DirectUI.Drawing.Colors.Transparent,
                BorderColor = DefaultTheme.Accent,
                BorderLength = 2,
                Roundness = 0.1f
            };
            context.Renderer.DrawBox(itemBounds, highlightStyle);
        }

        // Manually advance the parent grid container
        context.Layout.AdvanceLayout(logicalSize);
    }

    private void DrawRightPanel()
    {
        var panelStyle = new BoxStyle { FillColor = new(40, 40, 40, 255), BorderLength = 0f };
        UI.BeginResizableVPanel("details_panel", ref _app.RightPanelWidth, HAlignment.Right, minWidth: 200, maxWidth: 500, panelStyle: panelStyle);

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
            var availableTags = _app.AllTags.Except(_app.SelectedFile.Tags, new TagComparer());
            foreach (var tag in availableTags)
            {
                if (UI.Button($"add_tag_{tag.Id}", tag.Name, new Vector2(scrollInnerWidth, 24)))
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

    public void DrawDeleteConfirmationModal(Tag tagToDelete)
    {
        UI.BeginVBoxContainer("delete_modal_vbox", new Vector2(10, 10), gap: 15f);

        UI.WrappedText("delete_confirm_text", $"Are you sure you want to delete the tag '{tagToDelete.Name}'? This will remove it from {tagToDelete.FileCount} file(s) and cannot be undone.", new Vector2(280, 0));

        UI.BeginHBoxContainer("delete_modal_buttons", UI.Context.Layout.GetCurrentPosition(), gap: 10f);

        if (UI.Button("delete_yes", "Yes, Delete", new Vector2(120, 32)))
        {
            _app.Host.ModalWindowService.CloseModalWindow(0); // 0 for 'Yes'
        }
        if (UI.Button("delete_no", "Cancel", new Vector2(100, 32)))
        {
            _app.Host.ModalWindowService.CloseModalWindow(1); // 1 for 'Cancel'
        }

        UI.EndHBoxContainer();
        UI.EndVBoxContainer();
    }
}