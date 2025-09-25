using System.Numerics;
using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;

namespace Bankan;

public class KanbanAppLogic : IAppLogic
{
    private const string BoardStateFile = "kanban_board.json";
    private const string SettingsStateFile = "kanban_settings.json";

    private readonly IWindowHost _windowHost;
    private readonly KanbanBoard _board;
    private readonly KanbanSettings _settings;

    private readonly KanbanBoardRenderer _boardRenderer;
    private readonly KanbanDragDropHandler _dragDropHandler;
    private readonly KanbanModalManager _modalManager;

    public KanbanAppLogic(IWindowHost windowHost)
    {
        _windowHost = windowHost;
        _board = StateSerializer.Load<KanbanBoard>(BoardStateFile) ?? new KanbanBoard();
        _settings = StateSerializer.Load<KanbanSettings>(SettingsStateFile) ?? new KanbanSettings();

        EnsureDefaultBoardState();

        _dragDropHandler = new KanbanDragDropHandler(_board);
        _modalManager = new KanbanModalManager(_windowHost, _board, SaveState);
        _boardRenderer = new KanbanBoardRenderer(_board, _settings, _modalManager, _dragDropHandler);
    }

    private void EnsureDefaultBoardState()
    {
        if (_board.Columns.Count == 0)
        {
            _board.Columns.Add(new KanbanColumn { Id = "todo", Title = "To Do", Tasks = new() { new KanbanTask { Text = "Design the main UI", ColorHex = "#bb86fc" }, new KanbanTask { Text = "Implement drag and drop", ColorHex = "#ff7597" }, } });
            _board.Columns.Add(new KanbanColumn { Id = "inprogress", Title = "In Progress", Tasks = new() { new KanbanTask { Text = "Set up DirectUI project", ColorHex = "#75ffff" } } });
            _board.Columns.Add(new KanbanColumn { Id = "done", Title = "Done", Tasks = new() { new KanbanTask { Text = "Analyze the web Kanban board", ColorHex = "#75ff9f" } } });
        }
    }

    public void SaveState()
    {
        StateSerializer.Save(_board, BoardStateFile);
        StateSerializer.Save(_settings, SettingsStateFile);
    }

    public void DrawUI(UIContext context)
    {
        var windowSize = context.Renderer.RenderTargetSize;

        var settingsButtonSize = new Vector2(40, 40);
        var settingsButtonPos = new Vector2(windowSize.X - settingsButtonSize.X - 20, 20);
        var settingsTheme = new ButtonStylePack { Normal = { FontName = "Segoe UI Symbol", FontSize = 20, FillColor = Colors.Transparent, BorderLength = 0 }, Hover = { FillColor = new Color(50, 50, 50, 255) }, Roundness = 0.5f };
        if (UI.Button("settings_btn", "⚙️", size: settingsButtonSize, origin: settingsButtonPos, theme: settingsTheme))
        {
            _modalManager.OpenSettingsModal(_settings);
        }

        var boardPadding = new Vector2(20, 20);
        var boardPosition = new Vector2(boardPadding.X, 80);
        var boardSize = new Vector2(windowSize.X - boardPadding.X * 2, windowSize.Y - boardPosition.Y - boardPadding.Y);
        var columnGap = 25f;
        float draggedTaskWidth = 0;

        UI.BeginGridContainer("board_grid", boardPosition, boardSize, _board.Columns.Count, new Vector2(columnGap, 0));

        if (_dragDropHandler.Update())
        {
            SaveState();
        }

        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();
        draggedTaskWidth = gridState.CellWidth;

        _boardRenderer.DrawColumnsInGrid();

        UI.EndGridContainer();

        _modalManager.ProcessPendingActions();

        _dragDropHandler.DrawDraggedTaskOverlay(draggedTaskWidth);
    }
}