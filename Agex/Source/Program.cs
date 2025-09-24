using DirectUI;
using DirectUI.Core;
using System.Numerics;
using Vortice.DirectWrite;
using System.Xml.Linq;
using System.Diagnostics;
using Agex.Core;
using Color = DirectUI.Drawing.Color;
using Colors = DirectUI.Drawing.Colors;

namespace Agex
{
    public class AgexApp : IAppLogic
    {
        // --- State ---
        private readonly IWindowHost _host;
        private List<Project> _recentProjects = new();
        private Project? _currentProject;
        private int _selectedProjectIndex = -1;
        private string[] _projectListForCombo = Array.Empty<string>();

        private bool _automaticMode = false;
        private string _aiResponseText = "Paste AI response with <TOOL_CALL> blocks here...";
        private string _executionLogText = "";
        private float _bottomPanelHeight = 250f;
        private string _fileTreeText = "";
        private bool _isTreeLoading = false;
        private string _instructionsText = "";

        // Automation state
        private Stopwatch _clipboardTimer = new();
        private string _lastClipboardContent = "";
        private bool _isProcessingAutomation = false;

        // Cached styles
        private ButtonStylePack? _menuButtonStyle, _removeButtonStyle, _executeButtonStyle, _loadButtonStyle;
        private BoxStyle? _panelStyle, _panelHeaderStyle;
        private ButtonStyle? _panelHeaderTextStyle, _titleTextStyle;

        public AgexApp(IWindowHost host)
        {
            _host = host;
            Initialize();
        }

        private void Initialize()
        {
            ConfigManager.Initialize();
            _recentProjects = ConfigManager.GetRecentProjects();
            var settings = ConfigManager.GetAppSettings();
            _automaticMode = settings.AutomaticModeEnabled;

            UpdateProjectComboBox();
            if (_recentProjects.Any())
            {
                SwitchProject(_recentProjects.First());
            }

            try
            {
                _instructionsText = File.ReadAllText("instructions.md");
            }
            catch
            {
                _instructionsText = "Could not load instructions.md";
            }

            _clipboardTimer.Start();
        }

        public void DrawUI(UIContext context)
        {
            InitializeStyles();
            var windowSize = UI.Context.Renderer.RenderTargetSize;
            float currentY = 0;

            HandleAutomation();

            // --- Menu Bar (placeholder) ---
            DrawMenuBar(windowSize, ref currentY);
            currentY += 20; // Padding after menu bar

            // --- Title ---
            var titleSize = UI.Context.TextService.MeasureText("Agex", _titleTextStyle);
            UI.BeginVBoxContainer("title_container", new Vector2(0, currentY), 0);
            UI.Text("title", "Agex", new Vector2(windowSize.X, titleSize.Y), _titleTextStyle, new Alignment(HAlignment.Center, VAlignment.Top));
            UI.EndVBoxContainer();
            currentY += titleSize.Y + 20;

            // --- Controls Row ---
            DrawControlsRow(windowSize, currentY);
            currentY += 28 + 20;

            // --- Bottom Panel (Execution Log) ---
            UI.BeginResizableHPanel("execution_log_panel", ref _bottomPanelHeight, 0, 0, minHeight: 100, maxHeight: windowSize.Y - 250, panelStyle: _panelStyle, padding: new Vector2(1, 1), gap: 5);
            DrawPanel("Execution Log", "2. Execution Log", ref _executionLogText, _bottomPanelHeight);
            UI.EndResizableHPanel();

            // --- Top Panel (AI Response) ---
            float splitterHeight = 5f;
            float topPanelY = currentY;
            float topPanelHeight = windowSize.Y - _bottomPanelHeight - splitterHeight - topPanelY;

            if (topPanelHeight > 60)
            {
                UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, topPanelY, windowSize.X, topPanelHeight), _panelStyle);
                UI.BeginVBoxContainer("ai_response_vbox", new Vector2(0, topPanelY), 0);
                DrawPanelHeader("1. AI Response", new Vector2(1, 1));

                var contentPos = UI.Context.Layout.GetCurrentPosition() + new Vector2(10, 10);
                UI.BeginVBoxContainer("ai_response_content", contentPos, 15);

                // Note: InputText is single-line. It will not look like a multi-line text area but will function.
                var inputTextHeight = topPanelHeight - 30 - 40 - 25;
                if (inputTextHeight > 0)
                {
                    UI.InputText("ai_response_input", ref _aiResponseText, new Vector2(windowSize.X - 22, inputTextHeight));
                }

                if (UI.Button("execute_tool_calls_btn", "Execute Tool Calls", new Vector2(windowSize.X - 22, 40), theme: _executeButtonStyle, disabled: _currentProject == null || string.IsNullOrWhiteSpace(_aiResponseText)))
                {
                    HandleExecute();
                }

                UI.EndVBoxContainer();
                UI.EndVBoxContainer();
            }
        }

        #region Drawing Helpers
        private void DrawMenuBar(Vector2 windowSize, ref float currentY)
        {
            var menuBarHeight = 24f;
            UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, 0, windowSize.X, menuBarHeight), new BoxStyle { FillColor = new Color(50, 50, 50, 255), BorderLength = 0, Roundness = 0 });
            UI.BeginHBoxContainer("menu_bar", Vector2.Zero, gap: 0, verticalAlignment: VAlignment.Center, fixedRowHeight: menuBarHeight);
            UI.Button("menu_file", "File", new Vector2(40, menuBarHeight), _menuButtonStyle);
            UI.Button("menu_edit", "Edit", new Vector2(40, menuBarHeight), _menuButtonStyle);
            UI.Button("menu_view", "View", new Vector2(40, menuBarHeight), _menuButtonStyle);
            UI.Button("menu_window", "Window", new Vector2(60, menuBarHeight), _menuButtonStyle);
            UI.Button("menu_help", "Help", new Vector2(45, menuBarHeight), _menuButtonStyle);
            UI.EndHBoxContainer();
            currentY += menuBarHeight;
        }

        private void DrawControlsRow(Vector2 windowSize, float y)
        {
            UI.BeginHBoxContainer("project_controls", new Vector2(20, y), gap: 10, verticalAlignment: VAlignment.Center, fixedRowHeight: 28);
            UI.Text("current_project_label", "Current Project:");
            int newIndex = _selectedProjectIndex;
            if (UI.Combobox("project_combo", ref newIndex, _projectListForCombo, new Vector2(250, 28)))
            {
                if (newIndex != _selectedProjectIndex)
                {
                    _selectedProjectIndex = newIndex;
                    SwitchProject(_recentProjects[_selectedProjectIndex]);
                }
            }
            if (UI.Button("remove_project_btn", "Remove", theme: _removeButtonStyle, size: new Vector2(70, 28), disabled: _currentProject == null))
            {
                HandleRemoveProject();
            }
            if (UI.Button("load_project_btn", "Load New Project...", size: new Vector2(140, 28), theme: _loadButtonStyle))
            {
                HandleLoadProject();
            }
            UI.EndHBoxContainer();

            UI.BeginHBoxContainer("auto_mode_controls", new Vector2(windowSize.X - 250, y + 2), gap: 8, verticalAlignment: VAlignment.Center, fixedRowHeight: 24);
            UI.Text("auto_mode_label", "Automatic Mode:");
            if (UI.Checkbox("auto_mode_check", "", ref _automaticMode))
            {
                ConfigManager.SetAppSettings(new AppSettings { AutomaticModeEnabled = _automaticMode });
            }
            UI.Text("auto_mode_status", _automaticMode ? "on" : "off");
            UI.EndHBoxContainer();
        }

        private void DrawPanel(string id, string title, ref string text, float availableHeight)
        {
            var headerPos = UI.Context.Layout.GetCurrentPosition();
            DrawPanelHeader(title, headerPos);

            var contentPos = UI.Context.Layout.GetCurrentPosition() + new Vector2(10, 10);
            var contentWidth = UI.Context.Renderer.RenderTargetSize.X - 22;
            var textInputHeight = availableHeight - 30 - 22;

            if (textInputHeight > 0)
            {
                UI.InputText($"{id}_input", ref text, new Vector2(contentWidth, textInputHeight), position: contentPos);
            }
        }

        private void DrawPanelHeader(string title, Vector2 pos)
        {
            var panelContentWidth = UI.Context.Renderer.RenderTargetSize.X - 2;
            UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(pos.X, pos.Y, panelContentWidth, 30), _panelHeaderStyle);
            UI.BeginHBoxContainer($"{title}_header_hbox", pos, 0, VAlignment.Center, 30);
            UI.Context.Layout.AdvanceContainerLayout(new Vector2(10, 0)); // Padding
            UI.Text($"{title}_header", title, style: _panelHeaderTextStyle);
            UI.EndHBoxContainer();
            UI.Context.Layout.AdvanceContainerLayout(new Vector2(panelContentWidth, 30));
        }
        #endregion

        #region Logic Handlers
        private void HandleLoadProject()
        {
            var path = NativeDialogs.SelectDirectory(_host.Handle);
            if (!string.IsNullOrEmpty(path))
            {
                var name = Path.GetFileName(path);
                var newProject = new Project { Name = name, Path = path };

                _recentProjects.RemoveAll(p => p.Path == path);
                _recentProjects.Insert(0, newProject);
                if (_recentProjects.Count > 5) _recentProjects.RemoveAt(5);

                ConfigManager.SetRecentProjects(_recentProjects);
                UpdateProjectComboBox();
                _selectedProjectIndex = 0;
                SwitchProject(newProject);
            }
        }

        private void HandleRemoveProject()
        {
            if (_selectedProjectIndex >= 0 && _selectedProjectIndex < _recentProjects.Count)
            {
                _recentProjects.RemoveAt(_selectedProjectIndex);
                ConfigManager.SetRecentProjects(_recentProjects);
                UpdateProjectComboBox();

                if (_recentProjects.Any())
                {
                    _selectedProjectIndex = 0;
                    SwitchProject(_recentProjects[0]);
                }
                else
                {
                    _selectedProjectIndex = -1;
                    _currentProject = null;
                    _fileTreeText = "";
                }
            }
        }

        private void SwitchProject(Project project)
        {
            _currentProject = project;
            _fileTreeText = "Loading tree...";
            _isTreeLoading = true;
            Task.Run(async () => {
                var structure = await FileTreeGenerator.GetDirectoryStructureAsync(project.Path);
                _fileTreeText = FileTreeGenerator.CreateTreeText(structure);
                _isTreeLoading = false;
            });
        }

        private void UpdateProjectComboBox()
        {
            _projectListForCombo = _recentProjects.Select(p => $"{p.Name} ({p.Path})").ToArray();
        }

        private async void HandleExecute()
        {
            if (_currentProject == null) return;

            var toolCalls = ParseToolCalls(_aiResponseText);
            if (!toolCalls.Any())
            {
                LogMessage("info", "No Tools Found", "No valid <tool_call> blocks were found in the input.");
                return;
            }

            foreach (var call in toolCalls)
            {
                await ExecuteTool(call.ToolName, call.Args);
            }

            // Refresh file tree after execution
            SwitchProject(_currentProject);
        }

        private List<(string ToolName, Dictionary<string, string> Args)> ParseToolCalls(string text)
        {
            var calls = new List<(string, Dictionary<string, string>)>();
            try
            {
                // Wrap text in a root element to handle multiple tool_call blocks
                var xmlDoc = XDocument.Parse($"<root>{text}</root>", LoadOptions.None);
                foreach (var toolCall in xmlDoc.Root!.Elements("tool_call"))
                {
                    var toolName = toolCall.Element("tool_name")?.Value ?? toolCall.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(toolName)) continue;

                    var args = toolCall.Element("args")?
                        .Elements()
                        .ToDictionary(e => e.Name.LocalName, e => e.Value)
                        ?? new Dictionary<string, string>();

                    calls.Add((toolName, args));
                }
            }
            catch (Exception ex)
            {
                LogMessage("error", "Tool Call Parse Error", ex.Message);
            }
            return calls;
        }

        private async Task ExecuteTool(string toolName, Dictionary<string, string> args)
        {
            if (_currentProject == null) return;
            try
            {
                string resultMessage = toolName switch
                {
                    "create_file" => await Tools.CreateFile(_currentProject.Path, args.GetValueOrDefault("path")!, args.GetValueOrDefault("content")),
                    "delete_file" => await Tools.DeleteFile(_currentProject.Path, args.GetValueOrDefault("path")!),
                    "make_dir" => await Tools.MakeDir(_currentProject.Path, args.GetValueOrDefault("path")!),
                    "read_file" => await Tools.ReadFile(_currentProject.Path, args.GetValueOrDefault("path")!),
                    "edit_file" => await Tools.EditFile(_currentProject.Path, args.GetValueOrDefault("path")!, args.GetValueOrDefault("find")!, args.GetValueOrDefault("replace")!),
                    "move_file" => await Tools.MoveFile(_currentProject.Path, args.GetValueOrDefault("src")!, args.GetValueOrDefault("dest")!),
                    "rename_file" => await Tools.RenameFile(_currentProject.Path, args.GetValueOrDefault("src")!, args.GetValueOrDefault("new_name")!),
                    "search_files" => await Tools.Search(_currentProject.Path, args.GetValueOrDefault("path")!, args.GetValueOrDefault("pattern")!, false),
                    "search_text" => await Tools.Search(_currentProject.Path, args.GetValueOrDefault("path")!, args.GetValueOrDefault("pattern")!, true),
                    _ => throw new ArgumentException($"Unknown tool: {toolName}")
                };
                LogMessage("success", toolName, resultMessage);
            }
            catch (Exception ex)
            {
                LogMessage("error", toolName, ex.Message);
            }
        }

        private void LogMessage(string type, string tool, string message)
        {
            _executionLogText = $"<TOOL_RESULT>\n    <status>{type}</status>\n    <tool_name>{tool}</tool_name>\n    <message><![CDATA[{message}]]></message>\n</TOOL_RESULT>";
            try
            {
                TextCopy.ClipboardService.SetText(_executionLogText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clipboard error: {ex.Message}");
            }
        }

        private async void HandleAutomation()
        {
            if (!_automaticMode || _isProcessingAutomation || _clipboardTimer.ElapsedMilliseconds < 500)
            {
                return;
            }
            _clipboardTimer.Restart();

            var currentClipboard = TextCopy.ClipboardService.GetText();
            if (!string.IsNullOrEmpty(currentClipboard) && currentClipboard != _lastClipboardContent)
            {
                _lastClipboardContent = currentClipboard;

                // Don't execute if it's our own log message
                if (currentClipboard.Contains("<TOOL_RESULT>")) return;

                _isProcessingAutomation = true;
                _aiResponseText = currentClipboard;
                HandleExecute();

                // Give a moment for log message to be set and copied
                await Task.Delay(200);

                try
                {
                    Automation.Click(350, 1000); // Click in AI Studio input
                    await Task.Delay(100);
                    Automation.Paste();
                    await Task.Delay(100);
                    Automation.Click(900, 1000); // Click "Send"
                }
                catch (Exception ex)
                {
                    LogMessage("error", "Automation", ex.Message);
                }
                finally
                {
                    _isProcessingAutomation = false;
                }
            }
        }
        #endregion

        private void InitializeStyles()
        {
            if (_menuButtonStyle != null) return; // Assume all are initialized if one is

            _menuButtonStyle = new ButtonStylePack { Roundness = 0, BorderLength = 0, Normal = { FillColor = Colors.Transparent }, Hover = { FillColor = DefaultTheme.HoverFill }, Pressed = { FillColor = DefaultTheme.Accent } };
            _removeButtonStyle = new ButtonStylePack { Normal = { FillColor = new Color(204, 63, 63, 255), BorderColor = new Color(139, 43, 43, 255) }, Hover = { FillColor = new Color(217, 76, 76, 255) }, Pressed = { FillColor = new Color(178, 55, 55, 255) }, BorderLength = 1, Roundness = 0.1f };
            _loadButtonStyle = new ButtonStylePack(new ButtonStylePack()); // Copy default
            _executeButtonStyle = new ButtonStylePack { Normal = { FillColor = DefaultTheme.Accent, BorderColor = DefaultTheme.AccentBorder, FontColor = Colors.WhiteSmoke }, Hover = { FillColor = new Color(77, 128, 230, 255) }, Pressed = { FillColor = new Color(40, 80, 180, 255) }, BorderLength = 1, Roundness = 0.1f };
            _panelStyle = new BoxStyle { FillColor = new Color(55, 55, 55, 255), BorderLength = 1, BorderColor = new Color(30, 30, 30, 255), Roundness = 0 };
            _panelHeaderStyle = new BoxStyle { FillColor = new Color(65, 65, 65, 255), BorderLength = 0, Roundness = 0 };
            _panelHeaderTextStyle = new ButtonStyle { FontColor = new Color(220, 220, 220, 255) };
            _titleTextStyle = new ButtonStyle { FontSize = 36, FontWeight = FontWeight.Light };
        }

        public void SaveState() { }
    }

    public static class Program
    {
        [STAThread] // Required for COM interop like the folder browser dialog.
        public static void Main(string[] args)
        {
            ApplicationRunner.Run(GraphicsBackend.Direct2D, (host) => new AgexApp(host));
        }
    }
}