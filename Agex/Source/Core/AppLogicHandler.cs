using DirectUI.Core;

namespace Agex;

public class AppLogicHandler
{
    private readonly IWindowHost _host;
    private readonly AppState _appState;
    private readonly ToolExecutor _toolExecutor;

    public AppLogicHandler(IWindowHost host, AppState appState)
    {
        _host = host;
        _appState = appState;
        _toolExecutor = new ToolExecutor(_appState, LogMessage);
    }

    public void Initialize()
    {
        ConfigManager.Initialize();
        _appState.RecentProjects = ConfigManager.GetRecentProjects();
        var settings = ConfigManager.GetAppSettings();
        _appState.AutomaticMode = settings.AutomaticModeEnabled;

        UpdateProjectComboBox();
        if (_appState.RecentProjects.Any())
        {
            SwitchProject(_appState.RecentProjects.First());
        }

        try
        {
            _appState.InstructionsText = File.ReadAllText("instructions.md");
        }
        catch
        {
            _appState.InstructionsText = "Could not load instructions.md";
        }

        _appState.ClipboardTimer.Start();
    }

    public void HandleLoadProject()
    {
        var path = NativeDialogs.SelectDirectory(_host.Handle);
        if (string.IsNullOrEmpty(path)) return;

        var name = Path.GetFileName(path);
        var newProject = new Project { Name = name, Path = path };

        _appState.RecentProjects.RemoveAll(p => p.Path == path);
        _appState.RecentProjects.Insert(0, newProject);
        if (_appState.RecentProjects.Count > 5) _appState.RecentProjects.RemoveAt(5);

        ConfigManager.SetRecentProjects(_appState.RecentProjects);
        UpdateProjectComboBox();
        _appState.SelectedProjectIndex = 0;
        SwitchProject(newProject);
    }

    public void HandleRemoveProject()
    {
        if (_appState.SelectedProjectIndex < 0 || _appState.SelectedProjectIndex >= _appState.RecentProjects.Count) return;

        _appState.RecentProjects.RemoveAt(_appState.SelectedProjectIndex);
        ConfigManager.SetRecentProjects(_appState.RecentProjects);
        UpdateProjectComboBox();

        if (_appState.RecentProjects.Any())
        {
            _appState.SelectedProjectIndex = 0;
            SwitchProject(_appState.RecentProjects[0]);
        }
        else
        {
            _appState.SelectedProjectIndex = -1;
            _appState.CurrentProject = null;
            _appState.FileTreeText = "";
        }
    }

    public void SwitchProject(Project project)
    {
        _appState.CurrentProject = project;
        _appState.FileTreeText = "Loading tree...";
        _appState.IsTreeLoading = true;
        Task.Run(async () =>
        {
            var structure = await FileTreeGenerator.GetDirectoryStructureAsync(project.Path);
            _appState.FileTreeText = FileTreeGenerator.CreateTreeText(structure);
            _appState.IsTreeLoading = false;
        });
    }

    public void UpdateProjectComboBox()
    {
        _appState.ProjectListForCombo = _appState.RecentProjects.Select(p => $"{p.Name} ({p.Path})").ToArray();
    }

    public async void HandleExecute()
    {
        await _toolExecutor.ExecuteFromResponse();
        // Refresh file tree after execution
        if (_appState.CurrentProject != null)
        {
            SwitchProject(_appState.CurrentProject);
        }
    }

    public async void HandleAutomation()
    {
        if (!_appState.AutomaticMode || _appState.IsProcessingAutomation || _appState.ClipboardTimer.ElapsedMilliseconds < 500)
        {
            return;
        }
        _appState.ClipboardTimer.Restart();

        var currentClipboard = TextCopy.ClipboardService.GetText();
        if (string.IsNullOrEmpty(currentClipboard) || currentClipboard == _appState.LastClipboardContent) return;

        _appState.LastClipboardContent = currentClipboard;
        if (currentClipboard.Contains("<TOOL_RESULT>")) return;

        _appState.IsProcessingAutomation = true;
        _appState.AiResponseText = currentClipboard;
        HandleExecute();

        await Task.Delay(200); // Give a moment for log message to be set and copied

        try
        {
            Automation.Click(350, 1000);
            await Task.Delay(100);
            Automation.Paste();
            await Task.Delay(100);
            Automation.Click(900, 1000);
        }
        catch (Exception ex)
        {
            LogMessage("error", "Automation", ex.Message);
        }
        finally
        {
            _appState.IsProcessingAutomation = false;
        }
    }

    private void LogMessage(string type, string tool, string message)
    {
        _appState.ExecutionLogText = $"<TOOL_RESULT>\n    <status>{type}</status>\n    <tool_name>{tool}</tool_name>\n    <message><![CDATA[{message}]]></message>\n</TOOL_RESULT>";
        try
        {
            TextCopy.ClipboardService.SetText(_appState.ExecutionLogText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clipboard error: {ex.Message}");
        }
    }
}