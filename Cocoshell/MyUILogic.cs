// MyUILogic.cs
using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

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
    private readonly InputMapEditor _inputMapEditor;

    // State for managing the modal "Project Settings" window
    // These are properties on the logic class, but their actual handling
    // for modal windows will be dependent on the capabilities of the host.
    private ModalWindow? _projectWindow; // This will only be instantiated by the D2D host
    private bool _isProjectWindowOpen = false; // Controlled by UI logic
    private int _projectWindowActiveTab = 0;
    private static readonly string[] ProjectWindowTabLabels = { "General", "Input Map" };

    // Flag to indicate the backend (for logic branching, e.g., modal window support)
    private readonly GraphicsBackend _backend;

    // Delegate to call the hosting window's OpenProjectWindow method, which handles platform-specific windowing.
    public Action OpenProjectWindowAction { get; private set; } = () => { };

    public MyUILogic(GraphicsBackend backend)
    {
        _backend = backend;
        _menuBarView = new MenuBarView();
        _sceneTreeView = new SceneTreeView();
        _inspectorView = new InspectorView();
        _bottomPanelView = new BottomPanelView();
    }

    /// <summary>
    /// Sets the action that the UI logic should call to request opening the project window.
    /// This action will be provided by the concrete window/host implementation.
    /// </summary>
    public void SetOpenProjectWindowHostAction(Action action)
    {
        OpenProjectWindowAction = action;
    }

    public void DrawUI(UIContext context)
    {
        ManageModalWindowState(); // Still manages state for D2D backend
        _menuBarView.Draw(context, OpenProjectWindowAction); // Pass the host-provided action
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
    private void DrawProjectWindowUI(UIContext context)
    {
        var renderer = context.Renderer;
        float windowWidth = renderer.RenderTargetSize.X;
        float windowHeight = renderer.RenderTargetSize.Y;
        float tabBarHeight = 30f;
        var contentArea = new Rect(0, tabBarHeight, windowWidth, windowHeight - tabBarHeight);

        // --- Draw Tab Bar ---
        UI.TabBar("project_tabs", ProjectWindowTabLabels, ref _projectWindowActiveTab);

        // --- Draw Content Panel Background ---
        var panelStyle = new BoxStyle
        {
            FillColor = new(37 / 255f, 37 / 255f, 38 / 255f, 1.0f),
            BorderColor = DefaultTheme.HoverBorder,
            BorderLengthTop = 1f,
            Roundness = 0f
        };
        renderer.DrawBox(contentArea, panelStyle);

        // --- Draw Active Tab Content ---
        if (_projectWindowActiveTab == 0)
        {
            DrawGeneralSettingsTab(context, contentArea);
        }
        else if (_projectWindowActiveTab == 1)
        {
            _inputMapEditor.Draw(context, contentArea);
        }
    }

    private void DrawGeneralSettingsTab(UIContext context, Rect contentArea)
    {
        var contentPos = contentArea.TopLeft + new Vector2(10, 10);
        UI.BeginVBoxContainer("tab_general_vbox", contentPos, 10);
        if (UI.Button("modal_button_1", "A button in a modal")) { /* ... */ }
        if (UI.Button("modal_button_close", "Close Me"))
        {
            _isProjectWindowOpen = false; // Signal the window to close
        }
        UI.EndVBoxContainer();
    }

    // This method is called internally by UI elements (e.g., MenuBarView)
    // It signals the _host_ to open a project window.
    // The host (e.g., MyDesktopAppWindow for D2D) then creates the modal window.
    internal void OpenProjectWindowInternal(Win32Window ownerWindow)
    {
        if (_isProjectWindowOpen) return;

        // Only D2D backend supports Win32 modal windows through this path.
        if (_backend != GraphicsBackend.Direct2D)
        {
            Console.WriteLine($"Modal windows are not yet implemented for the {_backend} backend.");
            return;
        }

        _projectWindow = new ModalWindow(ownerWindow, "Project Settings", 600, 400, DrawProjectWindowUI);
        if (_projectWindow.CreateAsModal())
        {
            _isProjectWindowOpen = true;
        }
        else
        {
            Console.WriteLine("Failed to create modal window.");
            _projectWindow.Dispose();
            _projectWindow = null;
        }
    }

    private void ManageModalWindowState()
    {
        if (_backend != GraphicsBackend.Direct2D) return; // Modal window management is D2D-specific

        if (_projectWindow == null) return;

        // If the OS window handle is gone (e.g., user clicked the 'X' button),
        // we must clean up our reference to it.
        if (_projectWindow.Handle == IntPtr.Zero)
        {
            _projectWindow.Dispose();
            _projectWindow = null;
            _isProjectWindowOpen = false;

            // Revert any unsaved changes in the editor since the window was closed abruptly.
            if (_inputMapEditor.IsDirty())
            {
                _inputMapEditor.RevertChanges();
            }
        }
        // If our application logic has requested the window to close (e.g., via a button click),
        // then we tell the window to close itself.
        else if (!_isProjectWindowOpen)
        {
            _projectWindow.Close();
        }
    }
}