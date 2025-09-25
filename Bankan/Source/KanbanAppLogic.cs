using System;
using System.Linq;
using System.Numerics;
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
                new KanbanTask { Text = "Design the main UI", ColorHex = "#bb86fc" },
                new KanbanTask { Text = "Implement drag and drop", ColorHex = "#ff7597" },
            }
            });
            _board.Columns.Add(new KanbanColumn
            {
                Id = "inprogress",
                Title = "In Progress",
                Tasks = new() {
                new KanbanTask { Text = "Set up DirectUI project", ColorHex = "#75ffff" }
            }
            });
            _board.Columns.Add(new KanbanColumn
            {
                Id = "done",
                Title = "Done",
                Tasks = new() {
                new KanbanTask { Text = "Analyze the web Kanban board", ColorHex = "#75ff9f" }
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

        // --- Define board and column dimensions ---
        float columnWidth = 350f;
        float columnGap = 40f;
        float scrollbarSize = 12f;
        var boardPadding = new Vector2(20, 20);
        var topMargin = 80f;

        var viewState = UI.State.GetOrCreateElement<BoardViewState>("board_view_state".GetHashCode());

        // 1. Pre-calculate the total required size of the content
        float totalBoardWidth = (_board.Columns.Count * columnWidth) + Math.Max(0, _board.Columns.Count - 1) * columnGap;
        float maxColumnHeight = 0f;
        if (_board.Columns.Any())
        {
            maxColumnHeight = _board.Columns.Max(CalculateColumnContentHeight);
        }
        var currentContentSize = new Vector2(totalBoardWidth, maxColumnHeight);

        // --- Process Logic ---
        // Pass layout info to the drag handler so it can calculate drop targets.
        if (_dragDropHandler.Update(new Vector2(viewState.ScrollOffset.X, viewState.ScrollOffset.Y), totalBoardWidth, maxColumnHeight, columnWidth, columnGap, topMargin, boardPadding))
        {
            SaveState(); // Save if drag-drop resulted in a change
        }
        _modalManager.ProcessPendingActions();

        // --- Draw UI ---
        DrawSettingsButton(windowSize);

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
            viewState.ScrollOffset.Y -= UI.Context.InputState.ScrollDelta * 40;
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
        _dragDropHandler.DrawDraggedTaskOverlay(columnWidth - 30f); // Column width minus content padding

        // 10. Store this frame's content size for the next frame's prediction
        viewState.ContentSize = currentContentSize;
    }

    private float CalculateColumnContentHeight(KanbanColumn column)
    {
        // This calculation is an estimate and must match the layout logic in the renderer.
        float columnWidth = 350f;
        float contentPadding = 15f;
        float gap = 10f;
        float tasksInnerWidth = columnWidth - (contentPadding * 2);

        float height = 0;
        height += contentPadding; // Top padding
        height += 30f + gap;      // Title + gap
        height += 12f + gap;      // Separator (2 thickness + 5*2 padding) + gap

        if (column.Tasks.Any())
        {
            var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
            foreach (var task in column.Tasks)
            {
                var wrappedLayout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(tasksInnerWidth - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
                height += wrappedLayout.Size.Y + 30; // Task widget height
                height += gap;
            }
            height -= gap; // Remove final gap after last task
        }

        if (column.Id == "todo")
        {
            height += gap;
            height += 40f; // Add task button
        }

        height += contentPadding; // Bottom padding
        return height;
    }

    private void DrawSettingsButton(Vector2 windowSize)
    {
        var settingsButtonSize = new Vector2(40, 40);
        var settingsButtonPos = new Vector2(windowSize.X - settingsButtonSize.X - 20, 20);

        var settingsTheme = new ButtonStylePack
        {
            Roundness = 0.5f
        };
        settingsTheme.Normal.FontName = "Segoe UI Symbol";
        settingsTheme.Normal.FontSize = 20;
        settingsTheme.Normal.FillColor = Colors.Transparent;
        settingsTheme.Normal.BorderLength = 0;
        settingsTheme.Hover.FillColor = new Color(50, 50, 50, 255);

        if (UI.Button("settings_btn", "⚙️", size: settingsButtonSize, origin: settingsButtonPos, theme: settingsTheme))
        {
            _modalManager.OpenSettingsModal(_settings);
        }
    }
}