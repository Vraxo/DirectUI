// MyUILogic.cs
using System;
using System.Numerics;
using Vortice.Mathematics;
using DirectUI;
using DirectUI.Core;

namespace Cocoshell;

/// <summary>
/// This class encapsulates the actual UI logic and view management, decoupled from windowing.
/// It implements the IAppLogic interface.
/// </summary>
public class MyUILogic : IAppLogic
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
    private readonly InputMapEditor _inputMapEditor; // Input map editor instance
    private readonly IModalWindowService _modalWindowService;

    // State for managing the modal "Project Settings" window
    private int _projectWindowActiveTab = 0;
    private static readonly string[] ProjectWindowTabLabels = { "General", "Input Map" };


    public Action OpenProjectWindowAction { get; } // Now just a wrapper for the modal service call

    public MyUILogic(IModalWindowService modalWindowService)
    {
        _modalWindowService = modalWindowService ?? throw new ArgumentNullException(nameof(modalWindowService));
        _menuBarView = new MenuBarView();
        _sceneTreeView = new SceneTreeView();
        _inspectorView = new InspectorView();
        _bottomPanelView = new BottomPanelView();
        _inputMapEditor = new InputMapEditor("input_map.yaml"); // Initialize here, if it's part of the app logic

        // Provide an action that calls the injected modal service.
        OpenProjectWindowAction = () => OpenProjectWindowInternal();
    }

    public void DrawUI(UIContext context)
    {
        _menuBarView.Draw(context, OpenProjectWindowAction);
        DrawMainLayoutPanels();

        // After drawing all panels, check if the bottom panel has signalled a scene change.
        if (_bottomPanelView.SelectedScenePath is not null)
        {
            _sceneTreeView.LoadScene(_bottomPanelView.SelectedScenePath);
        }
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

        // Calculate the available content height inside the panel for the inspector view.
        float panelContentHeight = UI.Context.Renderer.RenderTargetSize.Y - MenuBarHeight - (padding.Y * 2);

        UI.BeginResizableVPanel(
            "right_panel",
            ref _rightPanelWidth,
            HAlignment.Right,
            MenuBarHeight,
            minWidth: 150,
            maxWidth: 400,
            padding: padding,
            gap: 0,
            panelStyle: panelStyle);
        {
            _inspectorView.Draw(_sceneTreeView.SelectedNode, _rightPanelWidth, panelContentHeight);
        }
        UI.EndResizableVPanel();
    }

    private void DrawBottomPanel()
    {
        BoxStyle panelStyle = new() { BorderLength = 1, Roundness = 0f };
        Vector2 padding = new(PanelPadding, PanelPadding);

        UI.BeginResizableHPanel(
            "bottom_panel",
            ref _bottomPanelHeight,
            _leftPanelWidth,
            _rightPanelWidth,
            MenuBarHeight,
            minHeight: 50,
            maxHeight: 300,
            padding: padding,
            gap: PanelGap,
            panelStyle: panelStyle);
        {
            _bottomPanelView.Draw();
        }
        UI.EndResizableHPanel();
    }

    /// <summary>
    /// The drawing callback for the modal window.
    /// </summary>
    private void DrawProjectWindowUI(UIContext context, float modalWidth, float modalHeight)
    {
        var renderer = context.Renderer;
        float tabBarHeight = 30f;
        // The content area is now relative to the modal's own 0,0, using its explicit width/height
        var contentArea = new Rect(0, tabBarHeight, modalWidth, modalHeight - tabBarHeight);

        // --- Draw Tab Bar ---
        // UI.TabBar starts at current layout position, which is 0,0 due to PushLayoutOrigin
        UI.TabBar("project_tabs", ProjectWindowTabLabels, ref _projectWindowActiveTab);

        // --- Draw Content Panel Background ---
        var panelStyle = new BoxStyle
        {
            FillColor = new(37 / 255f, 37 / 255f, 38 / 255f, 1.0f),
            BorderColor = DefaultTheme.HoverBorder,
            BorderLengthTop = 1f,
            Roundness = 0f
        };
        // Draw the background using the contentArea (relative to modal's 0,0)
        renderer.DrawBox(contentArea, panelStyle);

        // --- Draw Active Tab Content ---
        // These will use the current layout origin (which is the modal's top-left corner)
        // and draw their content relative to it.
        if (_projectWindowActiveTab == 0)
        {
            DrawGeneralSettingsTab(context, contentArea);
        }
        else if (_projectWindowActiveTab == 1)
        {
            // _inputMapEditor.Draw expects a contentArea, which is already relative to the modal's 0,0
            _inputMapEditor.Draw(context, contentArea);
        }
    }

    private void DrawGeneralSettingsTab(UIContext context, Rect contentArea)
    {
        var contentPos = contentArea.TopLeft + new Vector2(10, 10); // Still relative to modal's 0,0
        UI.BeginVBoxContainer("tab_general_vbox", contentPos, 10);
        if (UI.Button("modal_button_1", "A button in a modal")) { /* ... */ }
        if (UI.Button("modal_button_close", "Close Me"))
        {
            _modalWindowService.CloseModalWindow(0); // Signal the modal to close with success
        }
        UI.EndVBoxContainer();
    }

    /// <summary>
    /// Requests the host to open the project settings modal window.
    /// </summary>
    private void OpenProjectWindowInternal()
    {
        if (_modalWindowService.IsModalWindowOpen) return;

        float modalWidth = 600; // Define here
        float modalHeight = 400; // Define here

        _modalWindowService.OpenModalWindow(
            "Project Settings",
            (int)modalWidth, // Pass int width
            (int)modalHeight, // Pass int height
                              // MODIFIED: Lambda now captures modalWidth and modalHeight and passes them to the actual drawing method
            (ctx) => DrawProjectWindowUI(ctx, modalWidth, modalHeight),
            (result) => {
                Console.WriteLine($"Project Settings Modal Closed with result: {result}");
                // If the modal was closed without explicitly saving (e.g., by X button), revert changes.
                if (result != 0 && _inputMapEditor.IsDirty())
                {
                    Console.WriteLine("Modal closed without saving. Reverting Input Map changes.");
                    _inputMapEditor.RevertChanges();
                }
            }
        );
    }
}