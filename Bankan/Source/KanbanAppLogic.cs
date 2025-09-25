using System;
using System.Collections.Generic;
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
    private readonly IWindowHost _windowHost;

    // --- Modal State ---
    private string _newTaskText = "";
    private string _newTaskColorHex = "#bb86fc";
    private KanbanColumn? _columnToAddTaskTo;

    private KanbanTask? _taskToEdit;
    private string _editedTaskText = "";
    private string _editedTaskColorHex = "";

    // --- Drag & Drop State ---
    private KanbanTask? _draggedTask;
    private KanbanColumn? _sourceColumn;
    private Vector2 _dragOffset;
    private int _dropIndex = -1;
    private KanbanColumn? _dropTargetColumn;


    private readonly List<string> _availableTaskColors = new()
    {
        "#bb86fc", "#ff7597", "#75ffff", "#75ff9f", "#ffdf75"
    };

    public KanbanAppLogic(IWindowHost windowHost)
    {
        _windowHost = windowHost;
        LoadState();
    }

    private void LoadState()
    {
        _board = StateSerializer.Load<KanbanBoard>(BoardStateFile) ?? new KanbanBoard();
        _settings = StateSerializer.Load<KanbanSettings>(SettingsStateFile) ?? new KanbanSettings();

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
        var windowSize = UI.Context.Renderer.RenderTargetSize;

        HandleDragAndDrop();

        var settingsButtonSize = new Vector2(40, 40);
        var settingsButtonPos = new Vector2(windowSize.X - settingsButtonSize.X - 20, 20);
        var settingsTheme = new ButtonStylePack { Normal = { FontName = "Segoe UI Symbol", FontSize = 20, FillColor = Colors.Transparent, BorderLength = 0 }, Hover = { FillColor = new Color(50, 50, 50, 255) }, Roundness = 0.5f };
        if (UI.Button("settings_btn", "⚙️", size: settingsButtonSize, origin: settingsButtonPos, theme: settingsTheme))
        {
            OpenSettingsModal();
        }

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

        DrawDraggedTask();
    }

    private void DrawColumn(KanbanColumn column)
    {
        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();
        var cellPosition = gridState.GetCurrentPosition();
        var cellWidth = gridState.CellWidth;
        var cellHeight = gridState.AvailableSize.Y;
        var columnBounds = new Vortice.Mathematics.Rect(cellPosition.X, cellPosition.Y, cellWidth, cellHeight);

        var columnBgColor = new Color(30, 30, 30, 255);
        var columnStyle = new BoxStyle { FillColor = columnBgColor, Roundness = 0.1f, BorderLength = 0 };
        UI.Context.Renderer.DrawBox(columnBounds, columnStyle);

        var contentPadding = new Vector2(15, 15);
        var contentWidth = cellWidth - contentPadding.X * 2;
        var contentStartPosition = cellPosition + contentPadding;

        UI.BeginVBoxContainer(column.Id, contentStartPosition, gap: 10f);

        var titleStyle = new ButtonStyle { FontColor = new Color(224, 224, 224, 255), FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text(column.Id + "_title", column.Title, new Vector2(contentWidth, 30), titleStyle, new Alignment(HAlignment.Center, VAlignment.Center));
        UI.Separator(contentWidth, 2, 5, new Color(51, 51, 51, 255));

        int currentTaskIndex = 0;
        foreach (var task in column.Tasks)
        {
            DrawDropIndicator(column, currentTaskIndex, contentWidth);
            if (task != _draggedTask)
            {
                DrawTaskWidget(column, task, contentWidth);
            }
            else
            {
                DrawDragPlaceholder(task, contentWidth);
            }
            currentTaskIndex++;
        }
        DrawDropIndicator(column, currentTaskIndex, contentWidth);

        if (column.Id == "todo")
        {
            var addTaskTheme = new ButtonStylePack { Normal = { FillColor = Colors.Transparent, BorderColor = new Color(51, 51, 51, 255) }, Hover = { FillColor = DefaultTheme.Accent, BorderColor = DefaultTheme.Accent } };
            if (UI.Button(column.Id + "_add_task", "+ Add Task", size: new Vector2(contentWidth, 40), theme: addTaskTheme))
            {
                _columnToAddTaskTo = column;
                OpenAddTaskModal();
            }
        }

        UI.EndVBoxContainer();
    }

    private void DrawTaskWidget(KanbanColumn column, KanbanTask task, float width)
    {
        var context = UI.Context;
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
        var wrappedLayout = context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float height = wrappedLayout.Size.Y + 30;

        var taskTheme = new ButtonStylePack { Roundness = 0.1f };
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
        var textAlign = _settings.TextAlign == TaskTextAlign.Left ? new Alignment(HAlignment.Left, VAlignment.Center) : new Alignment(HAlignment.Center, VAlignment.Center);

        var pos = context.Layout.GetCurrentPosition();
        var taskBounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);

        if (!context.Layout.IsRectVisible(taskBounds))
        {
            context.Layout.AdvanceLayout(new Vector2(width, height));
            return;
        }

        bool isHovering = taskBounds.Contains(context.InputState.MousePosition);
        if (isHovering && !_windowHost.ModalWindowService.IsModalWindowOpen)
        {
            UI.State.SetPotentialInputTarget(task.Id.GetHashCode());
        }

        var finalStyle = isHovering ? taskTheme.Hover : taskTheme.Normal;
        context.Renderer.DrawBox(taskBounds, finalStyle);
        var textBounds = new Vortice.Mathematics.Rect(taskBounds.X + 15, taskBounds.Y, taskBounds.Width - 30, taskBounds.Height);
        UI.DrawTextPrimitive(textBounds, task.Text, textStyle, textAlign, Vector2.Zero);

        if (context.InputState.WasLeftMousePressedThisFrame && isHovering && UI.State.TrySetActivePress(task.Id.GetHashCode(), 1))
        {
            _draggedTask = task;
            _sourceColumn = column;
            _dragOffset = context.InputState.MousePosition - pos;
        }

        if (UI.BeginContextMenu(task.Id))
        {
            int choice = UI.ContextMenu($"context_{task.Id}", new[] { "Edit Task", "Delete Task" });
            if (choice == 0)
            {
                _taskToEdit = task;
                OpenEditTaskModal();
            }
            else if (choice == 1)
            {
                column.Tasks.Remove(task);
                SaveState();
            }
        }

        context.Layout.AdvanceLayout(new Vector2(width, height));
    }

    #region Drag and Drop Logic
    private void HandleDragAndDrop()
    {
        var input = UI.Context.InputState;
        if (_draggedTask == null) return;

        if (input.IsLeftMouseDown)
        {
            _dropTargetColumn = FindDropColumn();
            if (_dropTargetColumn != null)
            {
                _dropIndex = FindDropIndexInColumn(_dropTargetColumn);
            }
            else
            {
                _dropIndex = -1;
            }
        }
        else
        {
            if (_draggedTask != null && _sourceColumn != null && _dropTargetColumn != null && _dropIndex != -1)
            {
                _sourceColumn.Tasks.Remove(_draggedTask);
                _dropTargetColumn.Tasks.Insert(_dropIndex, _draggedTask);
                SaveState();
            }
            _draggedTask = null;
            _sourceColumn = null;
            _dropTargetColumn = null;
            _dropIndex = -1;
            UI.State.ClearActivePress(0);
        }
    }

    private KanbanColumn? FindDropColumn()
    {
        var mousePos = UI.Context.InputState.MousePosition;
        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();
        var cellWidth = gridState.CellWidth;
        var startX = gridState.StartPosition.X;
        var startY = gridState.StartPosition.Y;
        var gap = gridState.Gap.X;

        for (int i = 0; i < _board.Columns.Count; i++)
        {
            var colX = startX + i * (cellWidth + gap);
            var colBounds = new Vortice.Mathematics.Rect(colX, startY, cellWidth, gridState.AvailableSize.Y);
            if (colBounds.Contains(mousePos))
            {
                return _board.Columns[i];
            }
        }
        return null;
    }

    private int FindDropIndexInColumn(KanbanColumn column)
    {
        var mousePos = UI.Context.InputState.MousePosition;
        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();
        var colIndex = _board.Columns.IndexOf(column);
        var cellX = gridState.StartPosition.X + colIndex * (gridState.CellWidth + gridState.Gap.X);
        float currentY = gridState.StartPosition.Y + 15 + 30 + 17; // Initial offset for header, etc.

        int insertIndex = 0;
        foreach (var task in column.Tasks)
        {
            var taskToMeasure = task == _draggedTask ? _sourceColumn!.Tasks.FirstOrDefault(t => t.Id == _draggedTask.Id) ?? _draggedTask : task;
            var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
            var layout = UI.Context.TextService.GetTextLayout(taskToMeasure.Text, textStyle, new Vector2(gridState.CellWidth - 60, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
            float taskHeight = layout.Size.Y + 30 + 10;
            float midPoint = currentY + taskHeight / 2;
            if (mousePos.Y < midPoint)
            {
                return insertIndex;
            }
            currentY += taskHeight;
            insertIndex++;
        }
        return column.Tasks.Count;
    }

    private void DrawDragPlaceholder(KanbanTask task, float width)
    {
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
        var wrappedLayout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float height = wrappedLayout.Size.Y + 30;

        var pos = UI.Context.Layout.GetCurrentPosition();
        var bounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);
        var style = new BoxStyle { FillColor = new Color(0, 0, 0, 100), BorderColor = DefaultTheme.Accent, BorderLength = 1, Roundness = 0.1f };
        UI.Context.Renderer.DrawBox(bounds, style);
        UI.Context.Layout.AdvanceLayout(new Vector2(width, height));
    }

    private void DrawDropIndicator(KanbanColumn column, int index, float width)
    {
        if (_dropTargetColumn != column || _dropIndex != index) return;
        var pos = UI.Context.Layout.GetCurrentPosition();
        var indicatorRect = new Vortice.Mathematics.Rect(pos.X, pos.Y - 5, width, 4);
        var style = new BoxStyle { FillColor = DefaultTheme.Accent, BorderLength = 0, Roundness = 0.5f };
        UI.Context.Renderer.DrawBox(indicatorRect, style);
    }

    private void DrawDraggedTask()
    {
        if (_draggedTask == null) return;
        var mousePos = UI.Context.InputState.MousePosition;
        var gridState = (GridContainerState)UI.Context.Layout.PeekContainer();
        var width = gridState.CellWidth;
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
        var wrappedLayout = UI.Context.TextService.GetTextLayout(_draggedTask.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float height = wrappedLayout.Size.Y + 30;
        var pos = mousePos - _dragOffset;

        var bounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);
        var semiTransparent = _draggedTask.Color;
        semiTransparent.A = 150;
        var style = new BoxStyle { FillColor = semiTransparent, BorderColor = Colors.White, BorderLength = 1, Roundness = 0.1f };
        UI.Context.Renderer.DrawBox(bounds, style);

        var textBounds = new Vortice.Mathematics.Rect(bounds.X + 15, bounds.Y, bounds.Width - 30, bounds.Height);
        var textAlign = _settings.TextAlign == TaskTextAlign.Left ? new Alignment(HAlignment.Left, VAlignment.Center) : new Alignment(HAlignment.Center, VAlignment.Center);
        UI.DrawTextPrimitive(textBounds, _draggedTask.Text, textStyle, textAlign, Vector2.Zero);
    }
    #endregion

    #region Modal Implementations
    private void OpenSettingsModal()
    {
        if (_windowHost.ModalWindowService.IsModalWindowOpen) return;
        _windowHost.ModalWindowService.OpenModalWindow("Settings", 400, 250, DrawSettingsModalUI, _ => SaveState());
    }

    private void DrawSettingsModalUI(UIContext context)
    {
        var windowSize = UI.Context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("settings_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("settings_title", "Settings", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });
        UI.Separator(windowSize.X - 40);

        UI.Text("color_style_label", "Task Color Style");
        UI.BeginHBoxContainer("color_style_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("style_border_btn", "Border", isActive: _settings.ColorStyle == TaskColorStyle.Border)) _settings.ColorStyle = TaskColorStyle.Border;
        if (UI.Button("style_bg_btn", "Background", isActive: _settings.ColorStyle == TaskColorStyle.Background)) _settings.ColorStyle = TaskColorStyle.Background;
        UI.EndHBoxContainer();

        UI.Text("align_label", "Task Text Alignment");
        UI.BeginHBoxContainer("align_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("align_left_btn", "Left", isActive: _settings.TextAlign == TaskTextAlign.Left)) _settings.TextAlign = TaskTextAlign.Left;
        if (UI.Button("align_center_btn", "Center", isActive: _settings.TextAlign == TaskTextAlign.Center)) _settings.TextAlign = TaskTextAlign.Center;
        UI.EndHBoxContainer();

        var closeButtonPos = new Vector2((windowSize.X - 100) / 2, windowSize.Y - 60);
        if (UI.Button("close_settings_btn", "Close", size: new Vector2(100, 40), origin: closeButtonPos))
        {
            _windowHost.ModalWindowService.CloseModalWindow(0);
        }
        UI.EndVBoxContainer();
    }

    private void OpenAddTaskModal()
    {
        if (_windowHost.ModalWindowService.IsModalWindowOpen || _columnToAddTaskTo == null) return;
        _newTaskText = "";
        _newTaskColorHex = _availableTaskColors[0];
        _windowHost.ModalWindowService.OpenModalWindow("Create New Task", 450, 280, DrawAddTaskModalUI, resultCode =>
        {
            if (resultCode == 0 && !string.IsNullOrWhiteSpace(_newTaskText))
            {
                var newTask = new KanbanTask { Text = _newTaskText.Trim(), ColorHex = _newTaskColorHex };
                _columnToAddTaskTo.Tasks.Add(newTask);
                SaveState();
            }
            _columnToAddTaskTo = null;
            _newTaskText = "";
        });
    }

    private void DrawAddTaskModalUI(UIContext context)
    {
        var windowSize = UI.Context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("add_task_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("add_task_title", "Create New Task", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });
        UI.InputText("new_task_input", ref _newTaskText, new Vector2(windowSize.X - 40, 40), placeholderText: "Enter task description...");

        UI.BeginHBoxContainer("color_selector_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        foreach (var colorHex in _availableTaskColors)
        {
            var swatchTheme = new ButtonStylePack { Roundness = 0.5f, Normal = { FillColor = new KanbanTask { ColorHex = colorHex }.Color, BorderColor = colorHex == _newTaskColorHex ? Colors.White : Colors.Transparent, BorderLength = 3f } };
            if (UI.Button($"swatch_{colorHex}", "", size: new Vector2(30, 30), theme: swatchTheme)) _newTaskColorHex = colorHex;
        }
        UI.EndHBoxContainer();
        UI.Separator(windowSize.X - 40);

        UI.BeginHBoxContainer("add_task_actions_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("save_task_btn", "Save Task", size: new Vector2(120, 40))) _windowHost.ModalWindowService.CloseModalWindow(0);
        if (UI.Button("cancel_task_btn", "Cancel", size: new Vector2(120, 40))) _windowHost.ModalWindowService.CloseModalWindow(1);
        UI.EndHBoxContainer();
        UI.EndVBoxContainer();
    }

    private void OpenEditTaskModal()
    {
        if (_windowHost.ModalWindowService.IsModalWindowOpen || _taskToEdit == null) return;
        _editedTaskText = _taskToEdit.Text;
        _editedTaskColorHex = _taskToEdit.ColorHex;
        _windowHost.ModalWindowService.OpenModalWindow("Edit Task", 450, 320, DrawEditTaskModalUI, resultCode =>
        {
            if (resultCode == 0 && !string.IsNullOrWhiteSpace(_editedTaskText)) // Save
            {
                _taskToEdit.Text = _editedTaskText.Trim();
                _taskToEdit.ColorHex = _editedTaskColorHex;
                SaveState();
            }
            else if (resultCode == 2) // Delete
            {
                var column = _board.Columns.FirstOrDefault(c => c.Tasks.Contains(_taskToEdit));
                column?.Tasks.Remove(_taskToEdit);
                SaveState();
            }
            _taskToEdit = null;
        });
    }

    private void DrawEditTaskModalUI(UIContext context)
    {
        var windowSize = UI.Context.Renderer.RenderTargetSize;
        UI.BeginVBoxContainer("edit_task_vbox", new Vector2(20, 20), gap: 15f);
        UI.Text("edit_task_title", "Edit Task", style: new ButtonStyle { FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });
        UI.InputText("edit_task_input", ref _editedTaskText, new Vector2(windowSize.X - 40, 40), placeholderText: "Enter task description...");

        UI.BeginHBoxContainer("edit_color_selector_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        foreach (var colorHex in _availableTaskColors)
        {
            var swatchTheme = new ButtonStylePack { Roundness = 0.5f, Normal = { FillColor = new KanbanTask { ColorHex = colorHex }.Color, BorderColor = colorHex == _editedTaskColorHex ? Colors.White : Colors.Transparent, BorderLength = 3f } };
            if (UI.Button($"edit_swatch_{colorHex}", "", size: new Vector2(30, 30), theme: swatchTheme)) _editedTaskColorHex = colorHex;
        }
        UI.EndHBoxContainer();
        UI.Separator(windowSize.X - 40);

        UI.BeginHBoxContainer("edit_task_actions_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
        if (UI.Button("save_edit_btn", "Save Changes", size: new Vector2(140, 40))) _windowHost.ModalWindowService.CloseModalWindow(0);
        var deleteTheme = new ButtonStylePack { Normal = { FillColor = new Color(207, 102, 121, 255) }, Hover = { FillColor = new Color(176, 81, 98, 255) } };
        if (UI.Button("delete_task_btn", "Delete Task", size: new Vector2(120, 40), theme: deleteTheme)) _windowHost.ModalWindowService.CloseModalWindow(2);
        UI.EndHBoxContainer();
        UI.EndVBoxContainer();
    }
    #endregion
}