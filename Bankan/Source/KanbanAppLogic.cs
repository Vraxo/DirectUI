using System;
using System.Linq;
using System.Numerics;
using Bankan.Rendering;
using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;

namespace Bankan;

public class KanbanAppLogic : IAppLogic
{
    private const string BoardStateFile = "kanban_board.json";
    private const string SettingsStateFile = "kanban_settings.json";

    private KanbanBoard _board = new();
    private KanbanSettings _settings = new();

    private readonly KanbanModalManager _modalManager;
    private readonly KanbanDragDropHandler _dragDropHandler;
    private readonly KanbanBoardRenderer _boardRenderer;

    // State for our custom scrollable board view
    private class BoardViewState { public Vector2 ScrollOffset; public Vector2 ContentSize; }


    public KanbanAppLogic(IWindowHost windowHost)
    {
        LoadState();

        _dragDropHandler = new KanbanDragDropHandler(_board);
        _modalManager = new KanbanModalManager(windowHost, _board, SaveState);
        _boardRenderer = new KanbanBoardRenderer(_board, _settings, _modalManager, _dragDropHandler);
    }

    private void LoadState()
    {
        _board = StateSerializer.Load<KanbanBoard>(BoardStateFile) ?? new KanbanBoard();
        _settings = StateSerializer.Load<KanbanSettings>(SettingsStateFile) ?? new KanbanSettings();

        // If the board is empty, create a default structure
        if (_board.Columns.Count == 0)
        {
            _board.Columns.Add(new KanbanColumn
            {
                Id = "todo",
                Title = "To Do",
                Tasks = new() {
                new Task { Text = "Design the main UI", ColorHex = "#bb86fc" },
                new Task { Text = "Implement drag and drop", ColorHex = "#ff7597" },
            }
            });
            _board.Columns.Add(new KanbanColumn
            {
                Id = "inprogress",
                Title = "In Progress",
                Tasks = new() {
                new Task { Text = "Set up DirectUI project", ColorHex = "#75ffff" }
            }
            });
            _board.Columns.Add(new KanbanColumn
            {
                Id = "done",
                Title = "Done",
                Tasks = new() {
                new Task { Text = "Analyze the web Kanban board", ColorHex = "#75ff9f" }
            }
            });
        }
    }

    public void SaveState()
    {
        StateSerializer.Save(_board, BoardStateFile);
        StateSerializer.Save(_settings, SettingsStateFile);
    }

    public void DrawUI(UIContext context)
    {
        var windowSize = UI.Context.Renderer.RenderTargetSize;
        var scale = context.UIScale;

        // --- Define board and column dimensions ---
        float columnWidth = 350f * scale;
        float columnGap = 40f * scale;
        float scrollbarSize = 12f * scale;
        var boardPadding = new Vector2(20, 20) * scale;
        var topMargin = 80f * scale;

        var viewState = UI.State.GetOrCreateElement<BoardViewState>("board_view_state".GetHashCode());

        // 1. Pre-calculate the total required size of the content
        float totalBoardWidth = (_board.Columns.Count * columnWidth) + Math.Max(0, _board.Columns.Count - 1) * columnGap;
        float maxColumnHeight = 0f;
        if (_board.Columns.Any())
        {
            maxColumnHeight = _board.Columns.Max(c => KanbanLayoutCalculator.CalculateColumnContentHeight(c, scale));
        }
        var currentContentSize = new Vector2(totalBoardWidth, maxColumnHeight);

        // --- Process Logic ---
        // Update now simply manages the start/end of a drag operation.
        if (_dragDropHandler.Update())
        {
            SaveState(); // Save if drag-drop resulted in a change
        }
        _modalManager.ProcessPendingActions();

        // --- Draw UI ---
        DrawSettingsButton(windowSize, scale);

        // 2. Define the visible area (viewport) for the board
        var viewRect = new Vortice.Mathematics.Rect(
            boardPadding.X, topMargin,
            windowSize.X - boardPadding.X * 2,
            windowSize.Y - topMargin - boardPadding.Y
        );

        // 3. Predict scrollbar visibility based on LAST frame's content size to prevent layout jitter
        bool vScrollVisible = viewState.ContentSize.Y > viewRect.Height;
        bool hScrollVisible = viewState.ContentSize.X > viewRect.Width;

        // 4. Calculate the actual available area for content this frame
        float availableWidth = viewRect.Width - (vScrollVisible ? scrollbarSize : 0);
        float availableHeight = viewRect.Height - (hScrollVisible ? scrollbarSize : 0);

        // 5. Handle input and draw scrollbars based on CURRENT frame's content size
        if (viewRect.Contains(UI.Context.InputState.MousePosition) && !_dragDropHandler.IsDragging())
        {
            // Scroll speed is scaled for a more natural feel when zoomed in/out
            viewState.ScrollOffset.Y -= UI.Context.InputState.ScrollDelta * 40 * scale;
        }

        if (currentContentSize.Y > availableHeight)
        {
            viewState.ScrollOffset.Y = UI.VScrollBar("board_v_scroll", viewState.ScrollOffset.Y,
                new Vector2(viewRect.Right - scrollbarSize, viewRect.Y), availableHeight,
                currentContentSize.Y, availableHeight, scrollbarSize);
        }
        if (currentContentSize.X > availableWidth)
        {
            viewState.ScrollOffset.X = UI.HScrollBar("board_h_scroll", viewState.ScrollOffset.X,
                new Vector2(viewRect.X, viewRect.Bottom - scrollbarSize), availableWidth,
                currentContentSize.X, availableWidth, scrollbarSize);
        }

        // 6. Clamp scroll offsets
        viewState.ScrollOffset.X = Math.Clamp(viewState.ScrollOffset.X, 0, Math.Max(0, currentContentSize.X - availableWidth));
        viewState.ScrollOffset.Y = Math.Clamp(viewState.ScrollOffset.Y, 0, Math.Max(0, currentContentSize.Y - availableHeight));

        // 7. Calculate final content start position (for centering or scrolling)
        float startX = viewRect.X;
        if (currentContentSize.X < availableWidth) // Center horizontally if content is smaller than view
            startX += (availableWidth - currentContentSize.X) / 2f;
        else
            startX -= viewState.ScrollOffset.X;

        // Always align to the top of the view area, adjusted by the scroll offset.
        float startY = viewRect.Y - viewState.ScrollOffset.Y;

        var boardStartPosition = new Vector2(startX, startY);

        // 8. Draw columns inside a clipped area
        var contentClipRect = new Vortice.Mathematics.Rect(viewRect.X, viewRect.Y, availableWidth, availableHeight);
        UI.Context.Renderer.PushClipRect(contentClipRect);
        UI.Context.Layout.PushClipRect(contentClipRect);

        _boardRenderer.DrawBoard(boardStartPosition, columnWidth, columnGap);

        UI.Context.Layout.PopClipRect();
        UI.Context.Renderer.PopClipRect();

        // 9. Draw overlays (e.g., the task being dragged) on top of everything else
        _dragDropHandler.DrawDraggedTaskOverlay((350f * scale) - (30f * scale)); // Logical size scaled

        // 10. Store this frame's content size for the next frame's prediction
        viewState.ContentSize = currentContentSize;
    }

    private void DrawSettingsButton(Vector2 windowSize, float scale)
    {
        var settingsButtonSize = new Vector2(40, 40) * scale;
        var settingsButtonPos = new Vector2(windowSize.X - settingsButtonSize.X - (20 * scale), 20 * scale);

        var settingsTheme = new ButtonStylePack
        {
            Roundness = 0.5f
        };
        settingsTheme.Normal.FontName = "Segoe UI Symbol";
        settingsTheme.Normal.FontSize = 20 * scale;
        settingsTheme.Normal.FillColor = Colors.Transparent;
        settingsTheme.Normal.BorderLength = 0;
        settingsTheme.Hover.FillColor = new Color(50, 50, 50, 255);

        // Note: The UI.Button method internally handles scaling its logical size parameter.
        // We pass the unscaled size.
        if (UI.Button("settings_btn", "⚙️", size: new Vector2(40, 40), origin: new Vector2(windowSize.X / scale - 40 - 20, 20), theme: settingsTheme))
        {
            _modalManager.OpenSettingsModal(_settings);
        }
    }
}