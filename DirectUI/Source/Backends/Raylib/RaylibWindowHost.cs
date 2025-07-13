// DirectUI/RaylibWindowHost.cs
using System;
using System.Numerics;
using DirectUI.Core; // For IWindowHost, IModalWindowService
using DirectUI.Drawing;
using DirectUI.Input;
using Raylib_cs;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

/// <summary>
/// A concrete implementation of <see cref="IWindowHost"/> for the Raylib backend.
/// </summary>
public class RaylibWindowHost : IWindowHost, IModalWindowService
{
    private readonly string _title;
    private readonly int _width;
    private readonly int _height;
    private readonly Color4 _backgroundColor;

    private AppEngine? _appEngine;
    private IRenderer? _renderer;
    private ITextService? _textService;
    private bool _isDisposed = false;

    // State for managing overlay modals
    private bool _isModalActive;
    private Rect _currentModalBounds; // Bounds of the modal overlay relative to the main window
    private Action<UIContext>? _currentModalDrawCallback;
    private Action<int>? _currentOnModalClosedCallback;
    private int _currentModalResultCode;

    public IntPtr Handle => IntPtr.Zero;
    public InputManager Input => _appEngine?.Input ?? new InputManager();
    public SizeI ClientSize => new SizeI(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

    public bool ShowFpsCounter
    {
        get => _appEngine?.ShowFpsCounter ?? false;
        set { if (_appEngine is not null) _appEngine.ShowFpsCounter = value; }
    }

    public IModalWindowService ModalWindowService => this;

    public RaylibWindowHost(string title, int width, int height, Color4 backgroundColor)
    {
        _title = title;
        _width = width;
        _height = height;
        _backgroundColor = backgroundColor;
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        Console.WriteLine("RaylibWindowHost initializing...");
        try
        {
            Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
            Raylib.InitWindow(_width, _height, _title);
            Raylib.SetTargetFPS(60);

            _appEngine = new AppEngine(uiDrawCallback, backgroundColor);
            _renderer = new DirectUI.Backends.RaylibRenderer();
            _textService = new DirectUI.Backends.RaylibTextService();

            // Raylib-specific font initialization and registration
            FontManager.Initialize();
            FontManager.RegisterFontVariant("Segoe UI", Vortice.DirectWrite.FontWeight.Normal, "C:/Windows/Fonts/segoeui.ttf");
            FontManager.RegisterFontVariant("Segoe UI", Vortice.DirectWrite.FontWeight.SemiBold, "C:/Windows/Fonts/seguisb.ttf");
            FontManager.RegisterFontVariant("Consolas", Vortice.DirectWrite.FontWeight.Normal, "C:/Windows/Fonts/consola.ttf");
            FontManager.RegisterFontVariant("Consolas", Vortice.DirectWrite.FontWeight.Bold, "C:/Windows/Fonts/consolab.ttf");

            _appEngine.Initialize(_textService, _renderer);

            _isModalActive = false;
            _currentModalDrawCallback = null;
            _currentOnModalClosedCallback = null;
            _currentModalResultCode = 0;
            _currentModalBounds = default;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize RaylibWindowHost: {ex.Message}");
            Cleanup(); // Ensure partial initialization is cleaned up
            return false;
        }
    }

    public void RunLoop()
    {
        while (!Raylib.WindowShouldClose())
        {
            // Process Raylib input events directly
            _appEngine?.Input.ProcessRaylibInput();

            // Begin drawing
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Raylib_cs.Color(
                (byte)(_backgroundColor.R * 255),
                (byte)(_backgroundColor.G * 255),
                (byte)(_backgroundColor.B * 255),
                (byte)(_backgroundColor.A * 255)
            ));

            // Update and render the UI through the AppEngine
            if (_renderer is not null && _textService is not null && _appEngine is not null)
            {
                if (_isModalActive)
                {
                    // Draw a dimming overlay first, covering the entire window
                    _renderer.DrawBox(
                        new Rect(0, 0, _renderer.RenderTargetSize.X, _renderer.RenderTargetSize.Y),
                        new BoxStyle { FillColor = new Color4(0, 0, 0, 0.5f), Roundness = 0f, BorderLength = 0f });

                    // Then, draw the modal's content by providing its specific draw callback to the AppEngine
                    // This call effectively replaces the main UI rendering for this frame.
                    _appEngine.UpdateAndRenderModal(_renderer, _textService,
                        (ctx) =>
                        {
                            // Push a layout origin for the modal's internal UI elements
                            // to draw relative to 0,0 for its content area.
                            ctx.Layout.PushLayoutOrigin(_currentModalBounds.TopLeft);

                            // Push a clip rect for the modal's content area
                            ctx.Renderer.PushClipRect(_currentModalBounds);
                            ctx.Layout.PushClipRect(_currentModalBounds);

                            _currentModalDrawCallback?.Invoke(ctx);

                            // Pop clip rect and layout origin
                            ctx.Layout.PopClipRect();
                            ctx.Renderer.PopClipRect();
                            ctx.Layout.PopLayoutOrigin();
                        }
                    );
                }
                else
                {
                    // If no modal is active, draw the main UI
                    _appEngine.UpdateAndRender(_renderer, _textService);
                }
            }

            // End drawing
            Raylib.EndDrawing();
        }
    }

    public void Cleanup()
    {
        if (_isDisposed) return;
        Console.WriteLine("RaylibWindowHost cleaning up its resources...");

        // Ensure the modal state is reset if the main window is destroyed while a modal is active.
        if (_isModalActive)
        {
            _isModalActive = false;
            _currentModalDrawCallback = null;
            _currentOnModalClosedCallback = null;
            _currentModalBounds = default;
        }

        _appEngine?.Cleanup();
        (_renderer as DirectUI.Backends.RaylibRenderer)?.Cleanup(); // Explicit cast for Cleanup
        (_textService as DirectUI.Backends.RaylibTextService)?.Cleanup(); // Explicit cast for Cleanup
        FontManager.UnloadAll(); // Unload all Raylib fonts

        if (Raylib.IsWindowReady())
        {
            Raylib.CloseWindow();
        }
        _appEngine = null;
        _renderer = null;
        _textService = null;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Cleanup();
    }

    public bool IsModalWindowOpen => _isModalActive;

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (_isModalActive)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            onClosedCallback?.Invoke(-1);
            return;
        }

        // Calculate modal position to center it over the main window
        var clientSize = ClientSize;
        float modalX = (clientSize.Width - width) / 2f;
        float modalY = (clientSize.Height - height) / 2f;
        _currentModalBounds = new Rect(modalX, modalY, width, height);

        _currentModalDrawCallback = drawCallback;
        _currentOnModalClosedCallback = onClosedCallback;
        _currentModalResultCode = -1; // Default to cancel/error

        _isModalActive = true;
        // Raylib does not have a direct equivalent to EnableWindow(hwnd, false) for blocking interaction.
        // The modal overlay and the fact that we render only the modal will achieve the blocking visually.
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (!_isModalActive)
        {
            return;
        }

        _currentModalResultCode = resultCode;
        _currentOnModalClosedCallback?.Invoke(_currentModalResultCode);

        // Clear local modal state
        _isModalActive = false;
        _currentModalDrawCallback = null;
        _currentOnModalClosedCallback = null;
        _currentModalBounds = default;
    }
}