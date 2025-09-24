using DirectUI;
using DirectUI.Core;
using System.Numerics;
using Vortice.DirectWrite;
using Color = DirectUI.Drawing.Color;
using Colors = DirectUI.Drawing.Colors;

namespace Agex
{
    public class AgexApp : IAppLogic
    {
        // UI State
        private string[] _projects = { "Project A", "Project B", "Project C" };
        private int _selectedProjectIndex = 0;
        private bool _automaticMode = false;
        private string _aiResponseText = "Paste AI response with <TOOL_CALL> blocks here...";
        private string _executionLogText = "";
        private float _bottomPanelHeight = 250f;

        // Cached styles to avoid re-creation every frame
        private ButtonStylePack? _menuButtonStyle;
        private ButtonStylePack? _removeButtonStyle;
        private ButtonStylePack? _executeButtonStyle;
        private BoxStyle? _panelStyle;
        private BoxStyle? _panelHeaderStyle;
        private ButtonStyle? _panelHeaderTextStyle;
        private ButtonStyle? _titleTextStyle;

        public void DrawUI(UIContext context)
        {
            var windowSize = UI.Context.Renderer.RenderTargetSize;
            float currentY = 0;

            // Initialize styles on first run
            InitializeStyles();

            // --- Menu Bar ---
            var menuBarHeight = 24f;
            UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, 0, windowSize.X, menuBarHeight), new BoxStyle { FillColor = new Color(50, 50, 50, 255), BorderLength = 0, Roundness = 0 });

            UI.BeginHBoxContainer("menu_bar", Vector2.Zero, gap: 0, verticalAlignment: VAlignment.Center, fixedRowHeight: menuBarHeight);
            {
                UI.Button("menu_file", "File", new Vector2(40, menuBarHeight), _menuButtonStyle);
                UI.Button("menu_edit", "Edit", new Vector2(40, menuBarHeight), _menuButtonStyle);
                UI.Button("menu_view", "View", new Vector2(40, menuBarHeight), _menuButtonStyle);
                UI.Button("menu_window", "Window", new Vector2(60, menuBarHeight), _menuButtonStyle);
                UI.Button("menu_help", "Help", new Vector2(45, menuBarHeight), _menuButtonStyle);
            }
            UI.EndHBoxContainer();
            currentY += menuBarHeight + 20;

            // --- "Agex" Title ---
            var titleSize = UI.Context.TextService.MeasureText("Agex", _titleTextStyle);
            UI.BeginVBoxContainer("title_container", new Vector2(0, currentY), 0);
            UI.Text("title", "Agex", new Vector2(windowSize.X, titleSize.Y), _titleTextStyle, new Alignment(HAlignment.Center, VAlignment.Top));
            UI.EndVBoxContainer();
            currentY += titleSize.Y + 20;

            // --- Controls Row ---
            var controlsY = currentY;
            UI.BeginHBoxContainer("project_controls", new Vector2(20, controlsY), gap: 10, verticalAlignment: VAlignment.Center, fixedRowHeight: 28);
            {
                UI.Text("current_project_label", "Current Project:");
                UI.Combobox("project_combo", ref _selectedProjectIndex, _projects, new Vector2(200, 28));
                UI.Button("remove_project_btn", "Remove", theme: _removeButtonStyle, size: new Vector2(70, 28));
                UI.Button("load_project_btn", "Load New Project...", size: new Vector2(140, 28));
            }
            UI.EndHBoxContainer();

            UI.BeginHBoxContainer("auto_mode_controls", new Vector2(windowSize.X - 180, controlsY + 5), gap: 5, verticalAlignment: VAlignment.Center, fixedRowHeight: 24);
            {
                UI.Text("auto_mode_label", "Automatic Mode:");
                UI.Checkbox("auto_mode_check", "", ref _automaticMode);
                UI.Text("auto_mode_status", _automaticMode ? "on" : "off");
            }
            UI.EndHBoxContainer();
            currentY += 28 + 20;


            // --- Bottom Panel (Execution Log) ---
            UI.BeginResizableHPanel("execution_log_panel", ref _bottomPanelHeight, 0, 0,
                minHeight: 100, maxHeight: windowSize.Y - 250, panelStyle: _panelStyle, padding: new Vector2(1, 1), gap: 5);
            {
                var panelContentWidth = windowSize.X - 2;
                var headerPos = UI.Context.Layout.GetCurrentPosition();

                UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(headerPos.X, headerPos.Y, panelContentWidth, 30), _panelHeaderStyle);
                UI.BeginHBoxContainer("log_header_hbox", headerPos, 0, VAlignment.Center, 30);
                UI.Context.Layout.AdvanceContainerLayout(new Vector2(10, 0)); // Padding
                UI.Text("execution_log_header", "2. Execution Log", style: _panelHeaderTextStyle);
                UI.EndHBoxContainer();
                UI.Context.Layout.AdvanceContainerLayout(new Vector2(panelContentWidth, 30));

                var textInputHeight = _bottomPanelHeight - 30 - 20;
                if (textInputHeight > 0)
                {
                    UI.InputText("execution_log_input", ref _executionLogText, new Vector2(panelContentWidth - 20, textInputHeight), position: new Vector2(10, 0));
                }
            }
            UI.EndResizableHPanel();


            // --- Top Panel (AI Response) ---
            float splitterHeight = 5f;
            float topPanelY = currentY;
            float topPanelHeight = windowSize.Y - _bottomPanelHeight - splitterHeight - topPanelY;

            if (topPanelHeight > 60)
            {
                UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, topPanelY, windowSize.X, topPanelHeight), _panelStyle);

                var headerRect = new Vortice.Mathematics.Rect(1, topPanelY + 1, windowSize.X - 2, 30);
                UI.Context.Renderer.DrawBox(headerRect, _panelHeaderStyle);

                UI.BeginVBoxContainer("ai_response_vbox", new Vector2(0, topPanelY), 0);
                {
                    UI.BeginHBoxContainer("ai_response_header_hbox", new Vector2(11, 1), 0, VAlignment.Center, 30);
                    UI.Text("ai_response_header", "1. AI Response", style: _panelHeaderTextStyle);
                    UI.EndHBoxContainer();
                    UI.Context.Layout.AdvanceLayout(new Vector2(windowSize.X, 30));

                    UI.BeginVBoxContainer("ai_response_content", new Vector2(10, currentY), 5);
                    {
                        var inputTextHeight = topPanelHeight - 30 - 40 - 20;
                        if (inputTextHeight > 0)
                        {
                            UI.InputText("ai_response_input", ref _aiResponseText, new Vector2(windowSize.X - 20, inputTextHeight));
                        }
                        UI.Button("execute_tool_calls_btn", "Execute Tool Calls", new Vector2(windowSize.X - 20, 40), theme: _executeButtonStyle);
                    }
                    UI.EndVBoxContainer();
                }
                UI.EndVBoxContainer();
            }
        }

        private void InitializeStyles()
        {
            _menuButtonStyle ??= new ButtonStylePack
            {
                Roundness = 0,
                BorderLength = 0,
                Normal = { FillColor = Colors.Transparent },
                Hover = { FillColor = DefaultTheme.HoverFill },
                Pressed = { FillColor = DefaultTheme.Accent }
            };

            _removeButtonStyle ??= new ButtonStylePack
            {
                Normal = { FillColor = new Color(204, 63, 63, 255), BorderColor = new Color(139, 43, 43, 255) },
                Hover = { FillColor = new Color(217, 76, 76, 255) },
                Pressed = { FillColor = new Color(178, 55, 55, 255) },
                BorderLength = 1,
                Roundness = 0.1f
            };

            _executeButtonStyle ??= new ButtonStylePack
            {
                Normal = { FillColor = DefaultTheme.Accent, BorderColor = DefaultTheme.AccentBorder, FontColor = Colors.WhiteSmoke },
                Hover = { FillColor = new Color(77, 128, 230, 255) },
                Pressed = { FillColor = new Color(40, 80, 180, 255) },
                BorderLength = 1,
                Roundness = 0.1f
            };

            _panelStyle ??= new BoxStyle { FillColor = new Color(55, 55, 55, 255), BorderLength = 1, BorderColor = new Color(30, 30, 30, 255), Roundness = 0 };
            _panelHeaderStyle ??= new BoxStyle { FillColor = new Color(65, 65, 65, 255), BorderLength = 0, Roundness = 0 };
            _panelHeaderTextStyle ??= new ButtonStyle { FontColor = new Color(220, 220, 220, 255) };
            _titleTextStyle ??= new ButtonStyle { FontSize = 36, FontWeight = FontWeight.Light };
        }

        public void SaveState() { }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            // Use Direct2D backend for Windows for best results.
            ApplicationRunner.Run(GraphicsBackend.Direct2D, (host) => new AgexApp());
        }
    }
}