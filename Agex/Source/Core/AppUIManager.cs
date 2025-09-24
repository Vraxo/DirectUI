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
        currentY += titleSize.Y + 30;

        DrawControlsRow(windowSize, currentY);
        currentY += 32 + 20;

        float bottomPanelHeight = _state.BottomPanelHeight;
        UI.BeginResizableHPanel("execution_log_panel", ref bottomPanelHeight, 0, 0, minHeight: 100, maxHeight: windowSize.Y - 250, panelStyle: _styles.PanelStyle, padding: new Vector2(1, 1), gap: 5);
        _state.BottomPanelHeight = bottomPanelHeight;
        DrawPanel("Execution Log", "2. Execution Log", _state.ExecutionLogText, _state.BottomPanelHeight);
        UI.EndResizableHPanel();

        float splitterHeight = 5f;
        float topPanelY = currentY;
        float topPanelHeight = windowSize.Y - _state.BottomPanelHeight - splitterHeight - topPanelY;

        if (topPanelHeight > 60)
        {
            DrawTopPanel(topPanelY, topPanelHeight, windowSize);
        }
    }

    private void DrawTopPanel(float topPanelY, float topPanelHeight, Vector2 windowSize)
    {
        // Draw main panel background
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, topPanelY, windowSize.X, topPanelHeight), _styles.PanelStyle);

        // Draw header background separately, without padding
        float headerHeight = 30f;
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, topPanelY, windowSize.X, headerHeight), _styles.PanelHeaderStyle);

        // Draw header text using a temporary HBox for alignment
        UI.BeginHBoxContainer("ai_header_hbox", new Vector2(10, topPanelY), 0, VAlignment.Center, headerHeight);
        UI.Text("ai_header_text", "1. AI Response", style: _styles.PanelHeaderTextStyle);
        UI.EndHBoxContainer();

        // VBox for the content area below the header, with padding
        float contentY = topPanelY + headerHeight;
        float contentHeight = topPanelHeight - headerHeight;
        UI.BeginVBoxContainer("ai_content_vbox", new Vector2(10, contentY + 10), gap: 10);

        // Available height inside the padded content area
        float availableContentHeight = contentHeight - 20; // 10px top/bottom padding

        float buttonAreaHeight = 44 + 10; // button height + gap before it
        float textInputAvailableHeight = availableContentHeight - buttonAreaHeight;

        if (textInputAvailableHeight > 0)
        {
            string aiResponseText = _state.AiResponseText;
            if (UI.InputText("ai_response_input", ref aiResponseText, new Vector2(windowSize.X - 20, textInputAvailableHeight)))
            {
                _state.AiResponseText = aiResponseText;
            }
        }

        if (UI.Button("execute_tool_calls_btn", "Execute Tool Calls", new Vector2(windowSize.X - 20, 44), theme: _styles.ExecuteButtonStyle, disabled: _state.CurrentProject == null || string.IsNullOrWhiteSpace(_state.AiResponseText)))
        {
            _logic.HandleExecute();
        }

        UI.EndVBoxContainer();
    }

    private void DrawMenuBar(Vector2 windowSize, ref float currentY)
    {
        var menuBarHeight = 30f;
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, 0, windowSize.X, menuBarHeight), new BoxStyle { FillColor = new(50, 50, 50, 255), BorderLength = 0, Roundness = 0 });
        UI.BeginHBoxContainer("menu_bar", Vector2.Zero, gap: 0, verticalAlignment: VAlignment.Center, fixedRowHeight: menuBarHeight);
        UI.Button("menu_file", "File", new Vector2(50, menuBarHeight), _styles.MenuButtonStyle);
        UI.Button("menu_edit", "Edit", new Vector2(50, menuBarHeight), _styles.MenuButtonStyle);
        UI.Button("menu_view", "View", new Vector2(50, menuBarHeight), _styles.MenuButtonStyle);
        UI.Button("menu_window", "Window", new Vector2(70, menuBarHeight), _styles.MenuButtonStyle);
        UI.Button("menu_help", "Help", new Vector2(55, menuBarHeight), _styles.MenuButtonStyle);
        UI.EndHBoxContainer();
        currentY += menuBarHeight;
    }

    private void DrawControlsRow(Vector2 windowSize, float y)
    {
        UI.BeginHBoxContainer("project_controls", new Vector2(20, y), gap: 10, verticalAlignment: VAlignment.Center, fixedRowHeight: 32);
        UI.Text("current_project_label", "Current Project:");

        int newIndex = _state.SelectedProjectIndex;
        if (UI.Combobox("project_combo", ref newIndex, _state.ProjectListForCombo, new Vector2(300, 32)))
        {
            if (newIndex != _state.SelectedProjectIndex)
            {
                _state.SelectedProjectIndex = newIndex;
                _logic.SwitchProject(_state.RecentProjects[_state.SelectedProjectIndex]);
            }
        }
        if (UI.Button("remove_project_btn", "Remove", theme: _styles.RemoveButtonStyle, size: new Vector2(80, 32), disabled: _state.CurrentProject == null))
        {
            _logic.HandleRemoveProject();
        }
        if (UI.Button("load_project_btn", "Load New Project...", size: new Vector2(160, 32), theme: _styles.LoadButtonStyle))
        {
            _logic.HandleLoadProject();
        }
        UI.EndHBoxContainer();

        UI.BeginHBoxContainer("auto_mode_controls", new Vector2(windowSize.X - 250, y + 4), gap: 8, verticalAlignment: VAlignment.Center, fixedRowHeight: 24);
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

    private void DrawPanel(string id, string title, string text, float availableHeight)
    {
        var headerPos = UI.Context.Layout.GetCurrentPosition();
        DrawPanelHeader(title, headerPos);
        var contentWidth = UI.Context.Renderer.RenderTargetSize.X - 22;
        var textInputHeight = availableHeight - 1 - 30 - 5 - 1;

        string textCopy = text;
        if (textInputHeight > 0)
        {
            UI.InputText($"{id}_input", ref textCopy, new Vector2(contentWidth, textInputHeight), disabled: true);
        }
    }

    private void DrawPanelHeader(string title, Vector2 pos)
    {
        var panelContentWidth = UI.Context.Renderer.RenderTargetSize.X - 2;
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(pos.X, pos.Y, panelContentWidth, 30), _styles.PanelHeaderStyle);
        UI.BeginHBoxContainer($"{title}_header_hbox", Vector2.Zero, 0, VAlignment.Center, 30);
        UI.Context.Layout.AdvanceContainerLayout(new Vector2(10, 0));
        UI.Text($"{title}_header", title, style: _styles.PanelHeaderTextStyle);
        UI.EndHBoxContainer();
        UI.Context.Layout.AdvanceContainerLayout(new Vector2(panelContentWidth, 30));
    }
}