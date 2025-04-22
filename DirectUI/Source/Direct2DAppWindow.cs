// Direct2DAppWindow.cs - Made more generic for UI derivation
using System;
using System.Numerics;

using Vortice;
using Vortice.Mathematics;

using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using SharpGen.Runtime;

using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using SizeI = Vortice.Mathematics.SizeI;
using Rect = Vortice.Mathematics.Rect;

using DirectUI;
using Vortice.DCommon; // Still need this for DrawingContext/InputState

namespace DirectUI;

// Base window class responsible for setting up Direct2D and the main loop
public class Direct2DAppWindow : Win32Window
{
    // --- Core Factories (Protected for potential use by derived classes) ---
    protected ID2D1Factory1? _d2dFactory;
    protected IDWriteFactory? _dwriteFactory;

    // --- Render Target (Protected) ---
    protected ID2D1HwndRenderTarget? _renderTarget;

    // --- Removed UI Elements ---
    // private Button? _myTestButton; // Moved to derived class

    // --- Window State ---
    protected Color4 _backgroundColor = new(0.1f, 0.1f, 0.15f, 1.0f); // Protected if derived class wants to change it
    protected bool _graphicsInitialized = false;

    // --- Input State Tracking (Protected) ---
    protected Vector2 _currentMousePos = new(-1, -1);
    protected bool _isLeftMouseButtonDown = false;
    protected bool _wasLeftMouseClickedThisFrame = false;

    public Direct2DAppWindow(string title = "Vortice DirectUI Base Window", int width = 800, int height = 600)
        : base(title, width, height)
    { }

    // --- Initialization & Cleanup ---

    protected override bool Initialize()
    {
        Console.WriteLine("Direct2DAppWindow initializing Vortice Graphics...");
        return InitializeGraphics();
    }

    protected override void Cleanup()
    {
        Console.WriteLine("Direct2DAppWindow cleaning up its resources...");
        CleanupGraphics();
    }

    // --- Core Loop ---

    protected override void OnPaint()
    {
        if (!_graphicsInitialized || _renderTarget is null || _dwriteFactory is null)
        {
            if (!_graphicsInitialized && Handle != nint.Zero)
            {
                InitializeGraphics();
                if (!_graphicsInitialized || _renderTarget is null || _dwriteFactory is null)
                {
                    _wasLeftMouseClickedThisFrame = false;
                    return;
                }
            }
            else
            {
                _wasLeftMouseClickedThisFrame = false;
                return;
            }
        }

        try
        {
            _renderTarget.BeginDraw();
            _renderTarget.Clear(_backgroundColor);

            // --- Prepare UI Context ---
            var inputState = new InputState(
                _currentMousePos,
                _wasLeftMouseClickedThisFrame,
                _isLeftMouseButtonDown
            );

            var drawingContext = new DrawingContext(_renderTarget, _dwriteFactory);

            // --- Call derived class implementation for UI drawing ---
            DrawUIContent(drawingContext, inputState); // NEW VIRTUAL METHOD CALL

            // --- End Drawing ---
            Result endDrawResult = _renderTarget.EndDraw();

            if (endDrawResult.Failure)
            {
                Console.WriteLine($"EndDraw failed: {endDrawResult.Description}");
                if (endDrawResult.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Console.WriteLine("Render target needs recreation (Detected in EndDraw).");
                    _graphicsInitialized = false;
                    CleanupGraphics();
                }
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught SharpGenException in OnPaint): {ex.Message}");
            _graphicsInitialized = false;
            CleanupGraphics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Rendering Error: {ex}");
            _graphicsInitialized = false;
            CleanupGraphics(); // Consider if all errors should stop graphics
        }
        finally
        {
            _wasLeftMouseClickedThisFrame = false;
        }
    }

    // --- New Virtual Method for UI Content ---
    /// <summary>
    /// Override this method in a derived class to draw UI elements.
    /// Called within OnPaint after BeginDraw and Clear.
    /// </summary>
    protected virtual void DrawUIContent(DrawingContext context, InputState input)
    {
        // Base implementation does nothing.
    }


    // --- Event Handlers (Remain in base class to capture input) ---

    protected override void OnSize(int width, int height)
    {
        if (_graphicsInitialized && _renderTarget is not null)
        {
            Console.WriteLine($"Window resized to {width}x{height}. Resizing render target...");
            try
            {
                var newPixelSize = new SizeI(width, height);
                _renderTarget.Resize(newPixelSize);
                Console.WriteLine($"Successfully resized render target.");
            }
            catch (SharpGenException ex)
            {
                Console.WriteLine($"Failed to resize Render Target (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
                if (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
                {
                    Console.WriteLine("Render target needs recreation (Detected in Resize Exception).");
                    _graphicsInitialized = false;
                    CleanupGraphics();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resize Render Target (General Exception): {ex}");
                _graphicsInitialized = false;
                CleanupGraphics();
            }
        }
        else if (!_graphicsInitialized && Handle != nint.Zero)
        {
            InitializeGraphics();
        }
    }

    protected override void OnMouseMove(int x, int y) { _currentMousePos = new Vector2(x, y); }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        _currentMousePos = new Vector2(x, y);
        if (button == MouseButton.Left)
        {
            _isLeftMouseButtonDown = true;
            _wasLeftMouseClickedThisFrame = true;
        }
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        _currentMousePos = new Vector2(x, y);
        if (button == MouseButton.Left)
        {
            _isLeftMouseButtonDown = false;
        }
    }

    protected override void OnKeyDown(int keyCode)
    {
        if (keyCode == NativeMethods.VK_ESCAPE) { Close(); }
        // Derived classes could override to handle other keys for UI interaction
    }

    protected override bool OnClose() { return true; }

    // --- Graphics Management ---

    protected virtual bool InitializeGraphics() // Made virtual if derived needs to add steps
    {
        if (_graphicsInitialized) return true;
        if (Handle == nint.Zero) return false;

        Console.WriteLine($"Attempting Graphics Initialization for HWND {Handle}...");

        try
        {
            CleanupGraphics();

            Result factoryResult = D2D1.D2D1CreateFactory(D2DFactoryType.SingleThreaded, out _d2dFactory);
            factoryResult.CheckError();
            if (_d2dFactory is null) throw new InvalidOperationException("D2D Factory creation failed silently.");

            Result dwriteResult = DWrite.DWriteCreateFactory(DW.FactoryType.Shared, out _dwriteFactory);
            dwriteResult.CheckError();
            if (_dwriteFactory is null) throw new InvalidOperationException("DWrite Factory creation failed silently.");

            var clientRectSize = GetClientRectSize();
            if (clientRectSize.Width <= 0 || clientRectSize.Height <= 0)
            {
                Console.WriteLine($"Invalid client rect size ({clientRectSize.Width}x{clientRectSize.Height}). Aborting graphics initialization.");
                _dwriteFactory?.Dispose(); _dwriteFactory = null;
                _d2dFactory?.Dispose(); _d2dFactory = null;
                return false;
            }

            var dxgiPixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var renderTargetProperties = new RenderTargetProperties(dxgiPixelFormat);
            var hwndRenderTargetProperties = new HwndRenderTargetProperties
            {
                Hwnd = Handle,
                PixelSize = new SizeI(clientRectSize.Width, clientRectSize.Height),
                PresentOptions = PresentOptions.None
            };

            _renderTarget = _d2dFactory.CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties);
            if (_renderTarget is null) throw new InvalidOperationException("Render target creation returned null unexpectedly.");

            _renderTarget.TextAntialiasMode = D2D.TextAntialiasMode.Cleartype;

            // Removed UI Element Initialization (_myTestButton)

            Console.WriteLine($"Vortice Graphics initialized successfully for HWND {Handle}.");
            _graphicsInitialized = true;
            return true;
        }
        catch (SharpGenException ex)
        {
            Console.WriteLine($"Graphics Initialization failed (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
            CleanupGraphics(); _graphicsInitialized = false; return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Graphics Initialization failed (General Exception): {ex}");
            CleanupGraphics(); _graphicsInitialized = false; return false;
        }
    }

    protected virtual void CleanupGraphics() // Made virtual
    {
        bool resourcesExisted = _d2dFactory is not null || _renderTarget is not null;
        if (resourcesExisted) Console.WriteLine("Cleaning up Vortice Graphics resources...");

        UI.CleanupResources(); // Still clean UI cache here

        _renderTarget?.Dispose(); _renderTarget = null;
        _dwriteFactory?.Dispose(); _dwriteFactory = null;
        _d2dFactory?.Dispose(); _d2dFactory = null;
        _graphicsInitialized = false;

        if (resourcesExisted) Console.WriteLine("Finished cleaning graphics resources.");
    }

    // --- Helpers ---

    protected SizeI GetClientRectSize() // Made protected
    {
        if (Handle != nint.Zero && NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            int width = Math.Max(1, r.right - r.left);
            int height = Math.Max(1, r.bottom - r.top);
            return new SizeI(width, height);
        }
        int baseWidth = Math.Max(1, Width); int baseHeight = Math.Max(1, Height);
        if (Handle != nint.Zero) { Console.WriteLine($"GetClientRect failed. Falling back to base size: {baseWidth}x{baseHeight}"); }
        return new SizeI(baseWidth, baseHeight);
    }
}