using System.Diagnostics;

namespace Agex;

public class AppState
{
    // Project Management
    public List<Project> RecentProjects { get; set; } = new();
    public Project? CurrentProject { get; set; }
    public int SelectedProjectIndex { get; set; } = -1;
    public string[] ProjectListForCombo { get; set; } = System.Array.Empty<string>();

    // UI State
    public bool AutomaticMode { get; set; } = false;
    public string AiResponseText { get; set; } = "Paste AI response with <TOOL_CALL> blocks here...";
    public string ExecutionLogText { get; set; } = "";
    public float BottomPanelHeight { get; set; } = 250f;

    // File Tree
    public string FileTreeText { get; set; } = "";
    public bool IsTreeLoading { get; set; } = false;
    public string InstructionsText { get; set; } = "";

    // Automation
    public Stopwatch ClipboardTimer { get; } = new();
    public string LastClipboardContent { get; set; } = "";
    public bool IsProcessingAutomation { get; set; } = false;
}