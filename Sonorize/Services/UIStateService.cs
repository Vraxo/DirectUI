namespace Sonorize.Services;

public class UIStateService
{
    public int ActiveSidebarIndex { get; set; } = 0;
    public string SearchText { get; set; } = string.Empty;
    public int RightClickedSongIndex { get; set; } = -1;
    public bool IsSidebarCollapsed { get; set; } = false;
}