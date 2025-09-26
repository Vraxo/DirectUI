using System.Numerics;
using Bankan.Rendering;
using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;
using DirectUI.Styling;

namespace Bankan;

public class AppLogic : IAppLogic
{
    private const string BoardStateFile = "kanban_board.json";
    private const string SettingsStateFile = "kanban_settings.json";

    private KanbanBoard _board = new();
    private KanbanSettings _settings = new();

    private readonly ModalManager _modalManager;
    private readonly DragDropHandler _dragDropHandler;
    private readonly BoardRenderer _boardRenderer;

    public AppLogic(IWindowHost windowHost)
    {
        LoadState();
        StyleManager.LoadStylesFromFile("Data/styles.yaml");

        _dragDropHandler = new DragDropHandler(_board);
        _modalManager = new ModalManager(windowHost, _board, SaveState);
        _boardRenderer = new BoardRenderer(_board, _settings, _modalManager, _dragDropHandler);
    }

    private void LoadState()
    {
        _board = StateSerializer.Load<KanbanBoard>(BoardStateFile) ?? new KanbanBoard();
        _settings = StateSerializer.Load<KanbanSettings>(SettingsStateFile) ?? new KanbanSettings();

        if (_board.Columns.Count != 0)
        {
            return;
        }

        _board.Columns.Add(new()
        {
            Id = "todo",
            Title = "To Do",
            Tasks = 
            [
                new()
                {
                    Text = "Design the main UI",
                    ColorHex = "#bb86fc"
                },
                new()
                {
                    Text = "Implement drag and drop",
                    ColorHex = "#ff7597"
                },
            ]
        });

        _board.Columns.Add(new()
        {
            Id = "inprogress",
            Title = "In Progress",
            Tasks = 
            [
                new()
                {
                    Text = "Set up DirectUI project",
                    ColorHex = "#75ffff"
                }
            ]
        });

        _board.Columns.Add(new()
        {
            Id = "done",
            Title = "Done",
            Tasks = 
            [
                new()
                {
                    Text = "Analyze the web Kanban board",
                    ColorHex = "#75ff9f"
                }
            ]
        });
    }

    public void SaveState()
    {
        StateSerializer.Save(_board, BoardStateFile);
        StateSerializer.Save(_settings, SettingsStateFile);
    }

    public void DrawUI(UIContext context)
    {
        Vector2 windowSize = UI.Context.Renderer.RenderTargetSize;
        var scale = context.UIScale;

        // --- Process Logic ---
        if (_dragDropHandler.Update())
        {
            SaveState(); // Save if drag-drop resulted in a change
        }
        _modalManager.ProcessPendingActions();

        // --- Draw UI ---
        DrawSettingsButton(windowSize, scale);

        // --- Define board dimensions in LOGICAL units for UI controls ---
        float columnLogicalWidth = 350f;
        float columnLogicalGap = 40f;
        var boardLogicalPadding = new Vector2(20, 20);
        var topLogicalMargin = 80f;

        // The scroll area takes up the remaining space.
        var scrollAreaLogicalSize = new Vector2(
            (windowSize.X / scale) - boardLogicalPadding.X * 2,
            (windowSize.Y / scale) - topLogicalMargin - boardLogicalPadding.Y
        );

        // Use a container to position the scroll area within the padding.
        UI.BeginVBoxContainer("main_layout", Vector2.Zero);
        UI.Context.Layout.AdvanceLayout(new Vector2(boardLogicalPadding.X, topLogicalMargin));

        UI.BeginScrollArea("board_scroll_area", scrollAreaLogicalSize);

        // Inside the scroll area, we use the BoardRenderer which starts its own HBox.
        // We pass logical (unscaled) dimensions to it.
        _boardRenderer.DrawBoard(columnLogicalWidth, columnLogicalGap);

        UI.EndScrollArea();
        UI.EndVBoxContainer();

        // Draw overlays (e.g., the task being dragged) on top of everything else.
        // The width passed here should be physical.
        _dragDropHandler.DrawDraggedTaskOverlay((columnLogicalWidth * scale) - (30f * scale));
    }

    private void DrawSettingsButton(Vector2 windowSize, float scale)
    {
        Vector2 settingsButtonSize = new Vector2(40, 40) * scale;
        _ = new Vector2(windowSize.X - settingsButtonSize.X - (20 * scale), 20 * scale);

        ButtonStylePack settingsTheme = new()
        {
            Roundness = 0.5f,
            Normal =
            {
                FontName = "Seoge UI Symbol",
                FontSize = 20 * scale,
                FillColor = Colors.Transparent,
                BorderLength = 0,
            },
            Hover =
            {
                FillColor = new(50, 50, 50, 255)
            }
        };

        if (UI.Button(
            "settings_btn",
            "⚙️",
            size: new(40, 40),
            origin: new(windowSize.X / scale - 40 - 20, 20),
            theme: settingsTheme))
        {
            _modalManager.OpenSettingsModal(_settings);
        }
    }
}