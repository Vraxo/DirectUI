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
    private readonly IWindowHost _windowHost;

    public KanbanAppLogic(IWindowHost windowHost)
    {
        _windowHost = windowHost;
        LoadState();
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

        // Settings Button (Top-Right Corner)
        var settingsButtonSize = new Vector2(40, 40);
        var settingsButtonPos = new Vector2(windowSize.X - settingsButtonSize.X - 20, 20);
        var settingsTheme = new ButtonStylePack();
        settingsTheme.Normal.FontName = "Segoe UI Symbol"; // Use a font that has the gear glyph
        settingsTheme.Normal.FontSize = 20;
        settingsTheme.Normal.FillColor = Colors.Transparent;
        settingsTheme.Normal.BorderLength = 0;
        settingsTheme.Hover.FillColor = new Color(50, 50, 50, 255);
        settingsTheme.Roundness = 0.5f;

        UI.Button("settings_btn", "⚙️", size: settingsButtonSize, origin: settingsButtonPos, theme: settingsTheme);

        // Main Board Layout - Switched to a responsive GridContainer
        var boardPadding = new Vector2(20, 20);
        var boardPosition = new Vector2(boardPadding.X, 80);
        var boardSize = new Vector2(windowSize.X - boardPadding.X * 2, windowSize.Y - boardPosition.Y - boardPadding.Y);
        var columnGap = 25f;

        UI.BeginGridContainer("board_grid", boardPosition, boardSize, _board.Columns.Count, new Vector2(columnGap, 0));

        foreach (var column in _board.Columns)
        {
            DrawColumn(column);
        }

        UI.EndGridContainer();
    }

    private void DrawColumn(KanbanColumn column)
    {
        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();
        var cellPosition = gridState.GetCurrentPosition();
        var cellWidth = gridState.CellWidth;
        var cellHeight = gridState.AvailableSize.Y; // Use the full available height

        // 1. Draw the column background FIRST
        var columnBgColor = new Color(30, 30, 30, 255); // #1e1e1e
        var columnStyle = new BoxStyle { FillColor = columnBgColor, Roundness = 0.1f, BorderLength = 0 };
        var columnBounds = new Vortice.Mathematics.Rect(cellPosition.X, cellPosition.Y, cellWidth, cellHeight);
        UI.Context.Renderer.DrawBox(columnBounds, columnStyle);

        // 2. Begin a VBox for the content, inset within the cell
        var contentPadding = new Vector2(15, 15);
        var contentWidth = cellWidth - contentPadding.X * 2;
        var contentStartPosition = cellPosition + contentPadding;

        // Use a VBox to layout the content vertically. The EndVBoxContainer call
        // will automatically advance the parent GridContainer.
        UI.BeginVBoxContainer(column.Id, contentStartPosition, gap: 10f);

        // 3. Draw Content
        var titleStyle = new ButtonStyle { FontColor = new Color(224, 224, 224, 255), FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text(column.Id + "_title", column.Title, new Vector2(contentWidth, 30), titleStyle, new Alignment(HAlignment.Center, VAlignment.Center));

        UI.Separator(contentWidth, 2, 5, new Color(51, 51, 51, 255));

        foreach (var task in column.Tasks)
        {
            DrawTaskWidget(task, contentWidth);
        }

        if (column.Id == "todo")
        {
            UI.Button(column.Id + "_add_task", "+ Add Task", size: new Vector2(contentWidth, 40));
        }

        // 4. End the VBox
        UI.EndVBoxContainer();
    }

    private void DrawTaskWidget(KanbanTask task, float width)
    {
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };

        // Use WrappedText for tasks so long text doesn't overflow
        UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        var wrappedLayout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float height = wrappedLayout.Size.Y + 30; // 15px padding top/bottom

        var taskTheme = new ButtonStylePack();
        var cardBackground = new Color(42, 42, 42, 255);

        if (_settings.ColorStyle == TaskColorStyle.Background)
        {
            taskTheme.Normal.FillColor = task.Color;
            taskTheme.Normal.BorderColor = Colors.Transparent;
            taskTheme.Normal.FontColor = new Color(18, 18, 18, 255);
        }
        else
        {
            taskTheme.Normal.FillColor = cardBackground;
            taskTheme.Normal.BorderColor = task.Color;
            taskTheme.Normal.BorderLengthLeft = 4f;
            taskTheme.Normal.BorderLengthTop = 0;
            taskTheme.Normal.BorderLengthRight = 0;
            taskTheme.Normal.BorderLengthBottom = 0;
            taskTheme.Normal.FontColor = DefaultTheme.Text;
        }

        taskTheme.Hover.FillColor = new Color(60, 60, 60, 255);
        taskTheme.Roundness = 0.1f;

        var textAlign = _settings.TextAlign == TaskTextAlign.Left
            ? new Alignment(HAlignment.Left, VAlignment.Center)
            : new Alignment(HAlignment.Center, VAlignment.Center);

        // We can't use a simple Button anymore because we need text wrapping.
        // Instead, we'll draw the background box and the wrapped text separately.
        var pos = UI.Context.Layout.GetCurrentPosition();
        var taskBounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);

        // Culling check
        if (!UI.Context.Layout.IsRectVisible(taskBounds))
        {
            UI.Context.Layout.AdvanceLayout(new Vector2(width, height));
            return;
        }

        UI.Context.Renderer.DrawBox(taskBounds, taskTheme.Normal);

        var textBounds = new Vortice.Mathematics.Rect(
            taskBounds.X + 15,
            taskBounds.Y,
            taskBounds.Width - 30,
            taskBounds.Height);

        UI.DrawTextPrimitive(textBounds, task.Text, textStyle, textAlign, Vector2.Zero);

        UI.Context.Layout.AdvanceLayout(new Vector2(width, height));
    }
}