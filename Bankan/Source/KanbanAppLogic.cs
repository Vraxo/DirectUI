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

    // State for our custom scrollable board view
    private class BoardViewState { public Vector2 ScrollOffset; public Vector2 ContentSize; }

    // State for the "Add Task" modal
    private class AddTaskModalState
    {
        public string TaskText = "";
        public Color SelectedColor;
    }

    private readonly List<Color> _availableTaskColors = new()
    {
        new(187, 134, 252, 255), // #bb86fc
        new(255, 117, 151, 255), // #ff7597
        new(117, 255, 255, 255), // #75ffff
        new(117, 255, 159, 255), // #75ff9f
        new(255, 223, 117, 255)  // #ffdf75
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
        settingsTheme.Normal.FontName = "Segoe UI Symbol";
        settingsTheme.Normal.FontSize = 20;
        settingsTheme.Normal.FillColor = Colors.Transparent;
        settingsTheme.Normal.BorderLength = 0;
        settingsTheme.Hover.FillColor = new Color(50, 50, 50, 255);
        settingsTheme.Roundness = 0.5f;
        if (UI.Button("settings_btn", "⚙️", size: settingsButtonSize, origin: settingsButtonPos, theme: settingsTheme) && !_windowHost.ModalWindowService.IsModalWindowOpen)
        {
            _windowHost.ModalWindowService.OpenModalWindow(
                "Settings", 500, 250, DrawSettingsModal, _ => { /* No action needed on close */ });
        }

        // --- FIXED-SIZE SCROLLING/CENTERING BOARD LAYOUT ---

        var viewState = UI.State.GetOrCreateElement<BoardViewState>("board_view_state".GetHashCode());

        // Define board and column dimensions
        float columnWidth = 350f;
        float columnGap = 25f;
        float scrollbarSize = 12f;
        var boardPadding = new Vector2(20, 20);
        var topMargin = 80f;

        // 1. Pre-calculate the total required size of the content
        float totalBoardWidth = (_board.Columns.Count * columnWidth) + Math.Max(0, _board.Columns.Count - 1) * columnGap;
        float maxColumnHeight = 0f;
        foreach (var column in _board.Columns)
        {
            maxColumnHeight = Math.Max(maxColumnHeight, CalculateColumnContentHeight(column));
        }
        var currentContentSize = new Vector2(totalBoardWidth, maxColumnHeight);

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
        if (viewRect.Contains(UI.Context.InputState.MousePosition))
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
        if (currentContentSize.X < availableWidth)
            startX += (availableWidth - currentContentSize.X) / 2f;
        else
            startX -= viewState.ScrollOffset.X;

        float startY = viewRect.Y;
        if (currentContentSize.Y < availableHeight)
            startY += (availableHeight - currentContentSize.Y) / 2f;
        else
            startY -= viewState.ScrollOffset.Y;

        // 8. Draw columns inside a clipped area
        var contentClipRect = new Vortice.Mathematics.Rect(viewRect.X, viewRect.Y, availableWidth, availableHeight);
        UI.Context.Renderer.PushClipRect(contentClipRect);
        UI.Context.Layout.PushClipRect(contentClipRect);

        // We draw backgrounds first to ensure they are all the same height, then content over it
        for (int i = 0; i < _board.Columns.Count; i++)
        {
            var column = _board.Columns[i];
            var columnPosition = new Vector2(startX + i * (columnWidth + columnGap), startY);
            DrawColumnBackground(column, columnPosition, new Vector2(columnWidth, maxColumnHeight));
        }

        UI.BeginHBoxContainer("board_hbox", new Vector2(startX, startY), gap: columnGap);
        foreach (var column in _board.Columns)
        {
            DrawColumnContent(column, new Vector2(columnWidth, maxColumnHeight));
        }
        UI.EndHBoxContainer();

        UI.Context.Layout.PopClipRect();
        UI.Context.Renderer.PopClipRect();

        // 9. Store this frame's content size for the next frame's prediction
        viewState.ContentSize = currentContentSize;
    }

    private void DrawSettingsModal(UIContext context)
    {
        var modalContentWidth = 460f;
        UI.BeginVBoxContainer("settings_vbox", new Vector2(20, 20), 20f);

        var titleStyle = new ButtonStyle { FontSize = 20, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text("settings_title", "Settings", style: titleStyle);

        UI.Separator(modalContentWidth);

        var settingLabelStyle = new ButtonStyle { FontColor = new Color(200, 200, 200, 255) };
        var toggleLabelStyle = new ButtonStyle { FontColor = new Color(170, 170, 170, 255) };

        // --- Task Color Style Setting ---
        UI.BeginHBoxContainer("color_style_hbox", UI.Context.Layout.GetCurrentPosition(), 15f, VAlignment.Center);
        UI.Text("color_style_label", "Task Color Style", new Vector2(180, 24), settingLabelStyle);
        UI.Text("border_label", "Border", style: toggleLabelStyle);

        bool useBackgroundStyle = _settings.ColorStyle == TaskColorStyle.Background;
        if (UI.Checkbox("color_style_toggle", "", ref useBackgroundStyle))
        {
            _settings.ColorStyle = useBackgroundStyle ? TaskColorStyle.Background : TaskColorStyle.Border;
        }
        UI.Text("background_label", "Background", style: toggleLabelStyle);
        UI.EndHBoxContainer();

        // --- Task Text Alignment Setting ---
        UI.BeginHBoxContainer("text_align_hbox", UI.Context.Layout.GetCurrentPosition(), 15f, VAlignment.Center);
        UI.Text("text_align_label", "Task Text Alignment", new Vector2(180, 24), settingLabelStyle);
        UI.Text("left_align_label", "Left", style: toggleLabelStyle);

        bool useCenterAlign = _settings.TextAlign == TaskTextAlign.Center;
        if (UI.Checkbox("text_align_toggle", "", ref useCenterAlign))
        {
            _settings.TextAlign = useCenterAlign ? TaskTextAlign.Center : TaskTextAlign.Left;
        }
        UI.Text("center_align_label", "Center", style: toggleLabelStyle);
        UI.EndHBoxContainer();

        UI.EndVBoxContainer();
    }

    private void DrawAddTaskModal(UIContext context)
    {
        var modalState = UI.State.GetOrCreateElement<AddTaskModalState>("add_task_modal_state".GetHashCode());
        var modalContentWidth = 460f;

        UI.BeginVBoxContainer("add_task_vbox", new Vector2(20, 20), 20f);

        var titleStyle = new ButtonStyle { FontSize = 20, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text("add_task_title", "Create New Task", style: titleStyle);

        UI.InputText("add_task_input", ref modalState.TaskText, new Vector2(modalContentWidth, 35), placeholderText: "Enter task description...");

        // Color Selector
        UI.BeginHBoxContainer("color_selector_hbox", UI.Context.Layout.GetCurrentPosition(), 10f, VAlignment.Center);
        foreach (var color in _availableTaskColors)
        {
            var swatchTheme = new ButtonStylePack { Roundness = 1.0f };
            swatchTheme.Normal.FillColor = color;
            swatchTheme.Normal.BorderLength = 3;
            // If this color is selected, make the border white. Otherwise, transparent.
            swatchTheme.Normal.BorderColor = (color.R == modalState.SelectedColor.R && color.G == modalState.SelectedColor.G && color.B == modalState.SelectedColor.B)
                ? Colors.WhiteSmoke
                : Colors.Transparent;
            swatchTheme.Hover.FillColor = color;
            swatchTheme.Hover.BorderColor = swatchTheme.Normal.BorderColor;

            if (UI.Button($"swatch_{color.R}{color.G}{color.B}", "", new Vector2(30, 30), swatchTheme))
            {
                modalState.SelectedColor = color;
            }
        }
        UI.EndHBoxContainer();

        // Save Button
        if (UI.Button("save_task_button", "Save Task", new Vector2(modalContentWidth, 40)))
        {
            if (!string.IsNullOrWhiteSpace(modalState.TaskText))
            {
                var newTask = new KanbanTask
                {
                    Text = modalState.TaskText,
                    Color = modalState.SelectedColor
                };

                var todoColumn = _board.Columns.FirstOrDefault(c => c.Id == "todo");
                todoColumn?.Tasks.Add(newTask);

                _windowHost.ModalWindowService.CloseModalWindow(0);
            }
        }

        UI.EndVBoxContainer();
    }


    private float CalculateColumnContentHeight(KanbanColumn column)
    {
        float contentPadding = 15f;
        float gap = 10f;
        float contentWidth = 350f - contentPadding * 2;
        float tasksInnerWidth = contentWidth; // No inner scrollbar, so full width

        float height = 0;
        height += contentPadding; // Top padding
        height += 30f + gap; // Title + gap
        height += (2f + 5f * 2) + gap; // Separator + padding + gap

        if (column.Tasks.Any())
        {
            foreach (var task in column.Tasks)
            {
                var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
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

    private void DrawColumnBackground(KanbanColumn column, Vector2 position, Vector2 size)
    {
        var columnBgColor = new Color(30, 30, 30, 255); // #1e1e1e
        var columnStyle = new BoxStyle { FillColor = columnBgColor, Roundness = 0.1f, BorderLength = 0 };
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(position.X, position.Y, size.X, size.Y), columnStyle);
    }

    private void DrawColumnContent(KanbanColumn column, Vector2 columnSize)
    {
        float contentPadding = 15f;
        var contentWidth = columnSize.X - contentPadding * 2;
        // The VBox starts relative to the HBox's cursor, so we just need padding.
        var contentStartPosition = UI.Context.Layout.GetCurrentPosition() + new Vector2(contentPadding, contentPadding);

        UI.BeginVBoxContainer(column.Id, contentStartPosition, gap: 10f);

        var titleStyle = new ButtonStyle { FontColor = new Color(224, 224, 224, 255), FontSize = 18, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold };
        UI.Text(column.Id + "_title", column.Title, new Vector2(contentWidth, 30), titleStyle, new Alignment(HAlignment.Center, VAlignment.Center));
        UI.Separator(contentWidth, 2, 5, new Color(51, 51, 51, 255));

        foreach (var task in column.Tasks)
        {
            DrawTaskWidget(task, contentWidth);
        }

        if (column.Id == "todo")
        {
            if (UI.Button(column.Id + "_add_task", "+ Add Task", size: new Vector2(contentWidth, 40)) && !_windowHost.ModalWindowService.IsModalWindowOpen)
            {
                // Reset state before opening
                var modalState = UI.State.GetOrCreateElement<AddTaskModalState>("add_task_modal_state".GetHashCode());
                modalState.TaskText = "";
                modalState.SelectedColor = _availableTaskColors[0];

                _windowHost.ModalWindowService.OpenModalWindow(
                    "Create New Task", 500, 280, DrawAddTaskModal, _ => { /* No action needed on close */ });
            }
        }

        UI.EndVBoxContainer();
    }

    private void DrawTaskWidget(KanbanTask task, float width)
    {
        var textStyle = new ButtonStyle { FontName = "Segoe UI", FontSize = 14 };
        var wrappedLayout = UI.Context.TextService.GetTextLayout(task.Text, textStyle, new Vector2(width - 30, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        float height = wrappedLayout.Size.Y + 30; // 15px padding top/bottom

        var pos = UI.Context.Layout.GetCurrentPosition();
        var taskBounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, width, height);
        var taskId = task.Id.GetHashCode();
        var state = UI.State;
        var input = UI.Context.InputState;

        // --- Interaction Logic (from DrawButtonPrimitive) ---
        var clickResult = ClickResult.None;
        bool isHovering = !taskBounds.IsEmpty && taskBounds.Contains(input.MousePosition);

        if (isHovering)
        {
            var currentClip = UI.Context.Layout.GetCurrentClipRect();
            if (!currentClip.Contains(input.MousePosition))
            {
                isHovering = false;
            }
        }
        if (isHovering)
        {
            state.SetPotentialInputTarget(taskId);
        }

        bool isPressed = state.ActivelyPressedElementId == taskId;

        if (input.WasLeftMousePressedThisFrame && isHovering && state.PotentialInputTargetId == taskId)
        {
            state.ClickCaptureServer.RequestCapture(taskId, 1);
            if (state.TrySetActivePress(taskId, 1))
            {
                state.SetFocus(taskId);
            }
        }

        if (!input.IsLeftMouseDown && isPressed)
        {
            if (isHovering && state.InputCaptorId == taskId)
            {
                clickResult = state.RegisterClick(taskId);
            }
            state.ClearActivePress(taskId);
            isPressed = false;
        }
        isPressed = state.ActivelyPressedElementId == taskId;

        if (clickResult != ClickResult.None)
        {
            // TODO: Open edit task modal
            Console.WriteLine($"Task '{task.Text}' was clicked!");
        }

        // --- Style Resolution and Drawing ---
        var taskTheme = new ButtonStylePack();
        var cardBackground = new Color(42, 42, 42, 255);
        var hoverBackground = new Color(60, 60, 60, 255);

        if (_settings.ColorStyle == TaskColorStyle.Background)
        {
            taskTheme.Normal.FillColor = task.Color;
            taskTheme.Normal.BorderColor = Colors.Transparent;
            taskTheme.Normal.FontColor = new Color(18, 18, 18, 255);
            taskTheme.Hover.FillColor = task.Color; // Keep color, just darken it
            taskTheme.Hover.BorderColor = Colors.Transparent;
            taskTheme.Hover.FontColor = taskTheme.Normal.FontColor;
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
            taskTheme.Hover.FillColor = hoverBackground;
            taskTheme.Hover.BorderColor = taskTheme.Normal.BorderColor;
            taskTheme.Hover.BorderLengthLeft = 4f;
        }

        taskTheme.Roundness = 0.1f;
        taskTheme.Pressed.FillColor = DefaultTheme.Accent; // Keep a distinct pressed color

        taskTheme.UpdateCurrentStyle(isHovering, isPressed, false, false);
        var currentStyle = taskTheme.Current;

        // We use a copy of the resolved style for text drawing
        var currentTextStyle = new ButtonStyle(currentStyle)
        {
            FontName = "Segoe UI",
            FontSize = 14
        };

        var textAlign = _settings.TextAlign == TaskTextAlign.Left
            ? new Alignment(HAlignment.Left, VAlignment.Center)
            : new Alignment(HAlignment.Center, VAlignment.Center);

        // --- Drawing ---
        if (!UI.Context.Layout.IsRectVisible(taskBounds))
        {
            UI.Context.Layout.AdvanceLayout(new Vector2(width, height));
            return;
        }

        UI.Context.Renderer.DrawBox(taskBounds, currentStyle);

        var textBounds = new Vortice.Mathematics.Rect(
            taskBounds.X + 15,
            taskBounds.Y,
            taskBounds.Width - 30,
            taskBounds.Height);

        UI.DrawTextPrimitive(textBounds, task.Text, currentTextStyle, textAlign, Vector2.Zero);

        UI.Context.Layout.AdvanceLayout(new Vector2(width, height));
    }
}