using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class MainView
{
    // --- Constants ---
    private const float MenuBarHeight = 30f;
    private const float PanelPadding = 10f;
    private const float PanelGap = 10f;

    // --- State for layout ---
    private float _leftPanelWidth = 250f;
    private float _rightPanelWidth = 300f;
    private float _bottomPanelHeight = 150f;

    // --- Child Views ---
    private readonly MenuBarView _menuBarView;
    private readonly SceneTreeView _sceneTreeView;
    private readonly InspectorView _inspectorView;
    private readonly BottomPanelView _bottomPanelView;

    public MainView()
    {
        _menuBarView = new MenuBarView();
        _sceneTreeView = new SceneTreeView();
        _inspectorView = new InspectorView();
        _bottomPanelView = new BottomPanelView();
    }

    public void Draw(UIContext context, Action openProjectWindowAction)
    {
        _menuBarView.Draw(context, openProjectWindowAction);
        DrawMainLayoutPanels();
    }

    private void DrawMainLayoutPanels()
    {
        DrawLeftPanel();
        DrawRightPanel();
        DrawBottomPanel();
    }

    private void DrawLeftPanel()
    {
        var panelStyle = new BoxStyle { BorderLength = 1, Roundness = 0f };
        var padding = new Vector2(PanelPadding, PanelPadding);

        UI.BeginResizableVPanel("left_panel", ref _leftPanelWidth, HAlignment.Left, MenuBarHeight,
            minWidth: 150, maxWidth: 400, padding: padding, gap: PanelGap, panelStyle: panelStyle);

        _sceneTreeView.Draw();

        UI.EndResizableVPanel();
    }

    private void DrawRightPanel()
    {
        var panelStyle = new BoxStyle { BorderLength = 1, Roundness = 0f };
        var padding = new Vector2(PanelPadding, PanelPadding);

        UI.BeginResizableVPanel("right_panel", ref _rightPanelWidth, HAlignment.Right, MenuBarHeight,
            minWidth: 150, maxWidth: 400, padding: padding, gap: PanelGap, panelStyle: panelStyle);

        _inspectorView.Draw(_sceneTreeView.SelectedNode, _rightPanelWidth);

        UI.EndResizableVPanel();
    }

    private void DrawBottomPanel()
    {
        var panelStyle = new BoxStyle { BorderLength = 1, Roundness = 0f };
        var padding = new Vector2(PanelPadding, PanelPadding);

        UI.BeginResizableHPanel("bottom_panel", ref _bottomPanelHeight, _leftPanelWidth, _rightPanelWidth, MenuBarHeight,
            minHeight: 50, maxHeight: 300, padding: padding, gap: PanelGap, panelStyle: panelStyle);

        BottomPanelView.Draw();

        UI.EndResizableHPanel();
    }
}