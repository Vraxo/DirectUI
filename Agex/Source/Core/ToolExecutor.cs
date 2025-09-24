using System.Xml.Linq;

namespace Agex;

public class ToolExecutor
{
    private readonly AppState _appState;
    private readonly Action<string, string, string> _logMessageCallback;

    public ToolExecutor(AppState appState, Action<string, string, string> logMessageCallback)
    {
        _appState = appState;
        _logMessageCallback = logMessageCallback;
    }

    public async Task ExecuteFromResponse()
    {
        if (_appState.CurrentProject == null) return;

        var toolCalls = ParseToolCalls(_appState.AiResponseText);
        if (!toolCalls.Any())
        {
            _logMessageCallback("info", "No Tools Found", "No valid <tool_call> blocks were found in the input.");
            return;
        }

        foreach (var call in toolCalls)
        {
            await ExecuteTool(call.ToolName, call.Args);
        }
    }

    private List<(string ToolName, Dictionary<string, string> Args)> ParseToolCalls(string text)
    {
        var calls = new List<(string, Dictionary<string, string>)>();
        try
        {
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
            _logMessageCallback("error", "Tool Call Parse Error", ex.Message);
        }
        return calls;
    }

    private async Task ExecuteTool(string toolName, Dictionary<string, string> args)
    {
        if (_appState.CurrentProject == null) return;
        try
        {
            string resultMessage = toolName switch
            {
                "create_file" => await Tools.CreateFile(_appState.CurrentProject.Path, args.GetValueOrDefault("path")!, args.GetValueOrDefault("content")),
                "delete_file" => await Tools.DeleteFile(_appState.CurrentProject.Path, args.GetValueOrDefault("path")!),
                "make_dir" => await Tools.MakeDir(_appState.CurrentProject.Path, args.GetValueOrDefault("path")!),
                "read_file" => await Tools.ReadFile(_appState.CurrentProject.Path, args.GetValueOrDefault("path")!),
                "edit_file" => await Tools.EditFile(_appState.CurrentProject.Path, args.GetValueOrDefault("path")!, args.GetValueOrDefault("find")!, args.GetValueOrDefault("replace")!),
                "move_file" => await Tools.MoveFile(_appState.CurrentProject.Path, args.GetValueOrDefault("src")!, args.GetValueOrDefault("dest")!),
                "rename_file" => await Tools.RenameFile(_appState.CurrentProject.Path, args.GetValueOrDefault("src")!, args.GetValueOrDefault("new_name")!),
                "search_files" => await Tools.Search(_appState.CurrentProject.Path, args.GetValueOrDefault("path")!, args.GetValueOrDefault("pattern")!, false),
                "search_text" => await Tools.Search(_appState.CurrentProject.Path, args.GetValueOrDefault("path")!, args.GetValueOrDefault("pattern")!, true),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };
            _logMessageCallback("success", toolName, resultMessage);
        }
        catch (Exception ex)
        {
            _logMessageCallback("error", toolName, ex.Message);
        }
    }
}