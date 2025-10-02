using DirectUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Tagra.Data;
using Vortice.Mathematics;

namespace Tagra;

public class MainContentPanel
{
    private readonly App _app;

    public MainContentPanel(App app)
    {
        _app = app;
    }

    public void Draw()
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
}