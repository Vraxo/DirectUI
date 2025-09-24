using System.Numerics;
using DirectUI;

namespace Agex;

public class AppUIManager
{
    private readonly AppState _state;
    private readonly AppStyles _styles;
    private readonly AppLogicHandler _logic;

    public AppUIManager(AppState state, AppStyles styles, AppLogicHandler logic)
    {
        _state = state;
        _styles = styles;
        _logic = logic;
    }

    public void DrawUI()
    {
        var windowSize = UI.Context.Renderer.RenderTargetSize;
        float currentY = 0;

        _logic.HandleAutomation();

        DrawMenuBar(windowSize, ref currentY);
        currentY += 20;

        var titleSize = UI.Context.TextService.MeasureText("Agex", _styles.TitleTextStyle);
        UI.BeginVBoxContainer("title_container", new Vector2(0, currentY), 0);
        UI.Text("title", "Agex", new Vector2(windowSize.X, titleSize.Y), _styles.TitleTextStyle, new Alignment(HAlignment.Center, VAlignment.Top));
        UI.EndVBoxContainer();
        currentY += titleSize.Y + 20;

        DrawControlsRow(windowSize, currentY);
        currentY += 28 + 20;

        // --- Main Content Layout ---
        float bottomPanelHeight = _state.BottomPanelHeight;
        float splitterHeight = 5f;
        float topPanelY = currentY;
        float topPanelHeight = windowSize.Y - bottomPanelHeight - splitterHeight - topPanelY;

        if (topPanelHeight > 60)
        {
            DrawTopPanel(topPanelY, topPanelHeight, windowSize);
        }

        float bottomPanelMaxHeight = windowSize.Y - currentY - 100; // Leave 100px min for top panel
        UI.BeginResizableHPanel("execution_log_panel", ref bottomPanelHeight, 0, 0, minHeight: 100, maxHeight: bottomPanelMaxHeight, panelStyle: _styles.PanelStyle, padding: new Vector2(1, 1), gap: 5);
        _state.BottomPanelHeight = bottomPanelHeight;
        string executionLogText = _state.ExecutionLogText;
        DrawPanel("Execution Log", "2. Execution Log", ref executionLogText, _state.BottomPanelHeight);
        _state.ExecutionLogText = executionLogText;
        UI.EndResizableHPanel();
    }

    private void DrawTopPanel(float topPanelY, float topPanelHeight, Vector2 windowSize)
    {
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, topPanelY, windowSize.X, topPanelHeight), _styles.PanelStyle);

        UI.BeginVBoxContainer("ai_panel_vbox", new Vector2(10, topPanelY + 10), gap: 10);

        float vboxAvailableHeight = topPanelHeight - 20; // 10px top/bottom padding
        float contentWidth = windowSize.X - 20;

        DrawPanelHeader("1. AI Response", contentWidth);

        float headerHeight = 30f;
        float buttonHeight = 40f;
        float gapsTotalHeight = 20f; // Two gaps of 10px in the VBox (header-input, input-button)

        float textInputHeight = vboxAvailableHeight - headerHeight - buttonHeight - gapsTotalHeight;

        if (textInputHeight > 20)
        {
            string aiResponseText = _state.AiResponseText;
            if (UI.InputText("ai_response_input", ref aiResponseText, new Vector2(contentWidth, textInputHeight)))
            {
                _state.AiResponseText = aiResponseText;
            }
        }

        if (UI.Button("execute_tool_calls_btn", "Execute Tool Calls", new Vector2(contentWidth, buttonHeight), theme: _styles.ExecuteButtonStyle, disabled: _state.CurrentProject == null || string.IsNullOrWhiteSpace(_state.AiResponseText)))
        {
            _logic.HandleExecute();
        }

        UI.EndVBoxContainer();
    }

    private void DrawMenuBar(Vector2 windowSize, ref float currentY)
    {
        var menuBarHeight = 24f;
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, 0, windowSize.X, menuBarHeight), new BoxStyle { FillColor = new(50, 50, 50, 255), BorderLength = 0, Roundness = 0 });
        UI.BeginHBoxContainer("menu_bar", Vector2.Zero, gap: 0, verticalAlignment: VAlignment.Center, fixedRowHeight: menuBarHeight);
        UI.Button("menu_file", "File", new Vector2(40, menuBarHeight), _styles.MenuButtonStyle);
        UI.Button("menu_edit", "Edit", new Vector2(40, menuBarHeight), _styles.MenuButtonStyle);
        UI.Button("menu_view", "View", new Vector2(40, menuBarHeight), _styles.MenuButtonStyle);
        UI.Button("menu_window", "Window", new Vector2(60, menuBarHeight), _styles.MenuButtonStyle);
        UI.Button("menu_help", "Help", new Vector2(45, menuBarHeight), _styles.MenuButtonStyle);
        UI.EndHBoxContainer();
        currentY += menuBarHeight;
    }

    private void DrawControlsRow(Vector2 windowSize, float y)
    {
        UI.BeginHBoxContainer("project_controls", new Vector2(20, y), gap: 10, verticalAlignment: VAlignment.Center, fixedRowHeight: 28);
        UI.Text("current_project_label", "Current Project:");

        int newIndex = _state.SelectedProjectIndex;
        if (UI.Combobox("project_combo", ref newIndex, _state.ProjectListForCombo, new Vector2(250, 28)))
        {
            if (newIndex != _state.SelectedProjectIndex)
            {
                _state.SelectedProjectIndex = newIndex;
                _logic.SwitchProject(_state.RecentProjects[_state.SelectedProjectIndex]);
            }
        }
        if (UI.Button("remove_project_btn", "Remove", theme: _styles.RemoveButtonStyle, size: new Vector2(70, 28), disabled: _state.CurrentProject == null))
        {
            _logic.HandleRemoveProject();
        }
        if (UI.Button("load_project_btn", "Load New Project...", size: new Vector2(140, 28), theme: _styles.LoadButtonStyle))
        {
            _logic.HandleLoadProject();
        }
        UI.EndHBoxContainer();

        UI.BeginHBoxContainer("auto_mode_controls", new Vector2(windowSize.X - 250, y + 2), gap: 8, verticalAlignment: VAlignment.Center, fixedRowHeight: 24);
        UI.Text("auto_mode_label", "Automatic Mode:");

        bool automaticMode = _state.AutomaticMode;
        if (UI.Checkbox("auto_mode_check", "", ref automaticMode))
        {
            _state.AutomaticMode = automaticMode;
            ConfigManager.SetAppSettings(new AppSettings { AutomaticModeEnabled = _state.AutomaticMode });
        }
        UI.Text("auto_mode_status", _state.AutomaticMode ? "on" : "off");
        UI.EndHBoxContainer();
    }

    private void DrawPanel(string id, string title, ref string text, float panelHeight)
    {
        // This is called inside BeginResizableHPanel, which provides a VBox container.
        var padding = new Vector2(1, 1); // As per BeginResizableHPanel call
        var gap = 5f; // As per BeginResizableHPanel call
        float contentWidth = UI.Context.Renderer.RenderTargetSize.X - (padding.X * 2);

        DrawPanelHeader(title, contentWidth);

        float headerHeight = 30f;
        // Total height for content is panel height minus padding
        float contentAreaHeight = panelHeight - (padding.Y * 2);
        // Height for input is content area height minus header and one gap
        var textInputHeight = contentAreaHeight - headerHeight - gap;

        if (textInputHeight > 20)
        {
            string newText = text;
            if (UI.InputText($"{id}_input", ref newText, new Vector2(contentWidth, textInputHeight)))
            {
                text = newText;
            }
        }
    }

    private void DrawPanelHeader(string title, float width)
    {
        var pos = UI.Context.Layout.GetCurrentPosition();
        var headerSize = new Vector2(width, 30);

        // Background
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(pos.X, pos.Y, headerSize.X, headerSize.Y), _styles.PanelHeaderStyle);

        // HBox for text with padding and alignment
        UI.BeginHBoxContainer($"{title}_header_hbox", pos, 0, VAlignment.Center, 30);
        UI.Context.Layout.AdvanceLayout(new Vector2(10, 0)); // 10px left padding
        UI.Text($"{title}_header", title, style: _styles.PanelHeaderTextStyle);
        UI.EndHBoxContainer();

        // Advance the parent container's layout
        UI.Context.Layout.AdvanceLayout(headerSize);
    }
}