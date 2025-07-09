// MyDirectUIApp.cs
using System;
using System.Numerics;
using Vortice.Mathematics;
using Raylib_cs; // Added for Raylib

namespace DirectUI;

/// <summary>
/// The main application window class. This class is responsible for creating and
/// managing the window, hosting the rendering engine (AppHost), and orchestrating
/// the different views and modal dialogs.
/// </summary>
public class MyDirectUIApp : Direct2DAppWindow // This class name might be misleading if we use Raylib
{
    // View models for different parts of the UI
    private readonly MainView _mainView;
    private readonly InputMapEditor _inputMapEditor;

    // State for managing the modal "Project Settings" window
    private ModalWindow? _projectWindow;
    private bool _isProjectWindowOpen = false;
    private int _projectWindowActiveTab = 0;
    private static readonly string[] ProjectWindowTabLabels = { "General", "Input Map" };

    // Flag to control which backend is used
    private readonly GraphicsBackend _backend;

    public MyDirectUIApp(string title, int width, int height, GraphicsBackend backend) : base(title, width, height)
    {
        _backend = backend;
        _mainView = new MainView();

        // This path would typically come from a config file or service locator
        string inputMapPath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cherris\Res\Cherris\InputMap.yaml";
        _inputMapEditor = new InputMapEditor(inputMapPath);
    }

    /// <summary>
    /// Creates the window and initializes resources for either D2D or Raylib backend.
    /// This is the primary entry point for starting the application window.
    /// </summary>
    public bool Create()
    {
        if (_backend == GraphicsBackend.Raylib)
        {
            // For Raylib, we bypass the base Create and call Initialize directly
            // which handles Raylib.InitWindow.
            return Initialize();
        }
        else
        {
            // For D2D, use the standard Win32 window creation from the base class.
            return base.Create();
        }
    }

    // Override Initialize and Cleanup in Win32Window base if using Raylib
    // For Raylib, we won't actually create a Win32 window directly.
    // The AppHost will handle the Raylib window management.
    protected override bool Initialize()
    {
        if (_backend == GraphicsBackend.Raylib)
        {
            // For Raylib, the window is created/managed by Raylib-cs directly, not Win32Window.
            // We just need to initialize AppHost.
            Console.WriteLine("Initializing with Raylib backend...");
            Raylib.InitWindow(Width, Height, Title);
            Raylib.SetTargetFPS(60); // Control FPS for Raylib

            // AppHost expects an IntPtr HWND even for Raylib.
            // It will ignore it if _backend == GraphicsBackend.Raylib is true.
            _appHost = CreateAppHost();
            return _appHost.Initialize(IntPtr.Zero, new Vortice.Mathematics.SizeI(Width, Height));
        }
        else
        {
            Console.WriteLine("Direct2DAppWindow initializing...");
            _appHost = CreateAppHost();
            return _appHost.Initialize(Handle, GetClientRectSize());
        }
    }

    protected override void Cleanup()
    {
        if (_backend == GraphicsBackend.Raylib)
        {
            Console.WriteLine("RaylibAppWindow cleaning up its resources...");
            _appHost?.Cleanup();
            _appHost = null;
            Raylib.CloseWindow(); // Close the Raylib window
        }
        else
        {
            Console.WriteLine("Direct2DAppWindow cleaning up its resources...");
            _appHost?.Cleanup();
            _appHost = null;
        }
    }

    // For Raylib, the main loop in Program.cs handles drawing, not OnPaint messages.
    // OnPaint will only be called for D2D backend.
    protected override void OnPaint()
    {
        if (_backend == GraphicsBackend.Direct2D)
        {
            _appHost?.Render();
        }
    }

    public override void FrameUpdate()
    {
        if (_backend == GraphicsBackend.Raylib)
        {
            // For Raylib, Raylib.WindowShouldClose() is the equivalent of WM_QUIT.
            if (Raylib.WindowShouldClose())
            {
                Application.Exit(); // Signal main loop to exit
                return;
            }

            // Manually process input and render loop for Raylib
            // Input is managed by Raylib directly, so we need to pump it into AppHost's InputManager.
            _appHost?.Input.ProcessRaylibInput(); // Call the new method to update input state
            _appHost?.Render(); // Render the UI for this frame
        }
        else
        {
            // For D2D, Invalidate is enough to trigger WM_PAINT for continuous loop.
            Invalidate();
        }
    }

    protected override AppHost CreateAppHost()
    {
        var backgroundColor = new Color4(21 / 255f, 21 / 255f, 21 / 255f, 1.0f); // #151515
        return new AppHost(DrawUI, backgroundColor, _backend == GraphicsBackend.Raylib); // Pass the backend flag
    }

    // For Raylib, AppHost's InputManager directly processes Raylib events.
    // Therefore, Win32Window's OnKeyDown/Move/Up/etc. methods should only call base
    // if using the D2D backend. Otherwise, they are irrelevant.
    protected override void OnKeyDown(Keys key)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnKeyDown(key);
        if (key == Keys.F3)
        {
            if (_appHost != null)
            {
                _appHost.ShowFpsCounter = !_appHost.ShowFpsCounter;
            }
        }
    }

    protected override void OnMouseMove(int x, int y)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnMouseMove(x, y);
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnMouseDown(button, x, y);
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnMouseUp(button, x, y);
    }

    protected override void OnMouseWheel(float delta)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnMouseWheel(delta);
    }

    protected override void OnKeyUp(Keys key)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnKeyUp(key);
    }

    protected override void OnChar(char c)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnChar(c);
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

    private void OpenProjectWindow()
    {
        if (_isProjectWindowOpen) return;

        // For Raylib backend, modal windows are not handled by Win32Window directly.
        // Implementing true modal behavior (disabling owner window, blocking loop) would
        // require a different approach or a Raylib-specific modal window class.
        if (_backend == GraphicsBackend.Raylib)
        {
            Console.WriteLine("Modal windows are not yet implemented for Raylib backend.");
            return;
        }

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
        if (_backend == GraphicsBackend.Raylib) return; // Modal window management is D2D-specific

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
