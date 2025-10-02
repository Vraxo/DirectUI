namespace Tagra;

public class UIManager
{
    private readonly MenuBar _menuBar;
    private readonly LeftPanel _leftPanel;
    private readonly MainContentPanel _mainContentPanel;
    private readonly RightPanel _rightPanel;

    public UIManager(App app)
    {
        _menuBar = new MenuBar(app);
        _leftPanel = new LeftPanel(app);
        _mainContentPanel = new MainContentPanel(app);
        _rightPanel = new RightPanel(app);
    }

    public void DrawMenuBar()
    {
        _menuBar.Draw();
    }

    public void DrawMainLayout()
    {
        _leftPanel.Draw();
        _mainContentPanel.Draw();
        _rightPanel.Draw();
    }
}