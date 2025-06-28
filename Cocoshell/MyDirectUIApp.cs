// MyDirectUIApp.cs
using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

/// <summary>
/// The main application window class. This class is responsible for creating and
/// managing the window, hosting the rendering engine (AppHost), and orchestrating
/// the different views and modal dialogs.
/// </summary>
public class MyDirectUIApp : Direct2DAppWindow
{
    // View models for different parts of the UI
    private readonly MainView _mainView;
    private readonly InputMapEditor _inputMapEditor;

    // State for managing the modal "Project Settings" window
    private ModalWindow? _projectWindow;
    private bool _isProjectWindowOpen = false;
    private int _projectWindowActiveTab = 0;
    private static readonly string[] ProjectWindowTabLabels = { "General", "Input Map" };

    public MyDirectUIApp(string title, int width, int height) : base(title, width, height)
    {
        _mainView = new MainView();

        // This path would typically come from a config file or service locator
        string inputMapPath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cherris\Res\Cherris\InputMap.yaml";
        _inputMapEditor = new InputMapEditor(inputMapPath);
    }

    protected override AppHost CreateAppHost()
    {
        var backgroundColor = new Color4(21 / 255f, 21 / 255f, 21 / 255f, 1.0f); // #151515
        return new AppHost(DrawUI, backgroundColor);
    }

    protected override void OnKeyDown(Keys key)
    {
        if (key == Keys.F3)
        {
            if (_appHost != null)
            {
                _appHost.ShowFpsCounter = !_appHost.ShowFpsCounter;
            }
        }
        base.OnKeyDown(key); // Important: ensures input is registered and default keys (ESC) work
    }

    /// <summary>
    /// The primary drawing callback for the main application window. It delegates
    /// drawing to the main view after handling any window state management.
    /// </summary>
    private void DrawUI(UIContext context)
    {
        ManageModalWindowState();
        _mainView.Draw(context, OpenProjectWindow);
    }

    /// <summary>
    /// The drawing callback passed to the modal window.
    /// </summary>
    private void DrawProjectWindowUI(UIContext context)
    {
        var rt = context.RenderTarget;
        float windowWidth = rt.Size.Width;
        float windowHeight = rt.Size.Height;
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
        // BUG FIX: Convert Vortice.Mathematics.Size to System.Numerics.Vector2
        UI.Resources.DrawBoxStyleHelper(rt, contentArea.TopLeft, new Vector2(contentArea.Width, contentArea.Height), panelStyle);

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

    private void OpenProjectWindow()
    {
        if (_isProjectWindowOpen) return;

        _projectWindow = new ModalWindow(this, "Project Settings", 600, 400, DrawProjectWindowUI);
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