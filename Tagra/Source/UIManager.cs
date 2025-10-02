namespace Tagra;

public class UIManager
{
    private readonly LeftPanel _leftPanel;
    private readonly MainContentPanel _mainContentPanel;
    private readonly RightPanel _rightPanel;

    public UIManager(App app)
    {
        _leftPanel = new LeftPanel(app);
        _mainContentPanel = new MainContentPanel(app);
        _rightPanel = new RightPanel(app);
    }

    public void DrawLayout()
    {
        _leftPanel.Draw();
        _mainContentPanel.Draw();
        _rightPanel.Draw();
    }
}