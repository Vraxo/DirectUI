using DirectUI;
using DirectUI.Core;

namespace Agex;

public class AgexApp : IAppLogic
{
    private readonly AppState _appState;
    private readonly AppLogicHandler _logicHandler;
    private readonly AppUIManager _uiManager;

    public AgexApp(IWindowHost host)
    {
        _appState = new AppState();
        var appStyles = new AppStyles();
        _logicHandler = new AppLogicHandler(host, _appState);
        _uiManager = new AppUIManager(_appState, appStyles, _logicHandler);

        _logicHandler.Initialize();
    }

    public void DrawUI(UIContext context)
    {
        _uiManager.DrawUI();
    }

    public void SaveState() { }
}
