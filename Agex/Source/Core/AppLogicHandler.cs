using DirectUI;
using System.Diagnostics;
using System;
using DirectUI.Core;
using static ICSharpCode.SharpZipLib.Zip.ExtendedUnixData;
using static System.Runtime.InteropServices.JavaScript.JSType;
using TagLib.Matroska;

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
            _appState.InstructionsText = System.IO.File.ReadAllText("instructions.md");
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

    public async Task HandleExecute()
    {
        try
        {
            Console.WriteLine("[AgexApp] HandleExecute triggered.");
            await _toolExecutor.ExecuteFromResponse();
            // Refresh file tree after execution
            if (_appState.CurrentProject != null)
            {
                Console.WriteLine("[AgexApp] Refreshing file tree after execution.");
                SwitchProject(_appState.CurrentProject);
            }
        }
        catch (Exception ex)
        {
            // This is a top-level catch for the async void method.
            // It will catch any unhandled exceptions from the execution flow.
            var errorMessage = $"An unexpected error occurred during execution: {ex.Message}\n{ex.StackTrace}";
            Console.WriteLine(errorMessage);
            LogMessage("fatal", "ExecutionEngine", errorMessage);
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
        await HandleExecute(); // Await the task here

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
        var logContent = $"<TOOL_RESULT>\n    <status>{type}</status>\n    <tool_name>{tool}</tool_name>\n    <message><![CDATA[{message}]]></message>\n</TOOL_RESULT>";

        // This console log is critical for debugging.
        Console.WriteLine($"[AgexApp] LOGGING MESSAGE:\n{logContent}");

        _appState.ExecutionLogText = logContent;

        try
        {
            TextCopy.ClipboardService.SetText(_appState.ExecutionLogText);
        }
        catch (Exception ex)
        {
            // This is a non-critical error, but good to know about.
            Console.WriteLine($"[AgexApp] Clipboard error: {ex.Message}");
        }
    }
}