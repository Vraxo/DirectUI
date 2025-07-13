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
        if (_backend is GraphicsBackend.Raylib or GraphicsBackend.Vulkan)
        {
            // For Raylib and Vulkan, we bypass the base Create and call Initialize directly
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
        switch (_backend)
        {
            case GraphicsBackend.Raylib:
                Console.WriteLine("Initializing with Raylib backend...");
                Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
                Raylib.InitWindow(Width, Height, Title);
                Raylib.SetTargetFPS(60);
                _appHost = CreateAppHost();
                return _appHost.Initialize(IntPtr.Zero, new Vortice.Mathematics.SizeI(Width, Height));

            case GraphicsBackend.Vulkan:
                // This logic is now handled by ApplicationRunner.
                // The class instance is created, but its windowing is not used.
                return true;

            case GraphicsBackend.Direct2D:
            default:
                return base.Initialize();
        }
    }

    protected override void Cleanup()
    {
        switch (_backend)
        {
            case GraphicsBackend.Raylib:
                Console.WriteLine("RaylibAppWindow cleaning up its resources...");
                _appHost?.Cleanup();
                _appHost = null;
                Raylib.CloseWindow();
                break;

            case GraphicsBackend.Vulkan:
                // Nothing to do here, ApplicationRunner handles Veldrid cleanup.
                break;

            case GraphicsBackend.Direct2D:
            default:
                base.Cleanup();
                break;
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
        switch (_backend)
        {
            case GraphicsBackend.Raylib:
                if (Raylib.WindowShouldClose())
                {
                    Application.Exit();
                    return;
                }
                _appHost?.Input.ProcessRaylibInput();
                _appHost?.Render();
                break;

            case GraphicsBackend.Vulkan:
                // This is not called in the Veldrid loop.
                break;

            case GraphicsBackend.Direct2D:
            default:
                // For D2D, Invalidate is enough to trigger WM_PAINT for continuous loop.
                Invalidate();
                break;
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
            if (_appHost != null) _appHost.ShowFpsCounter = !_appHost.ShowFpsCounter;
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
    public void DrawUI(UIContext context)
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

        // For Raylib/Vulkan backends, modal windows are not handled by Win32Window directly.
        if (_backend != GraphicsBackend.Direct2D)
        {
            Console.WriteLine($"Modal windows are not yet implemented for the {_backend} backend.");
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