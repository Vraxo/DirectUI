// Direct2DAppWindow.cs - Updated to call UI.DoButton
using System;
using System.Numerics; // System.Numerics for Vector2

using Vortice;
using Vortice.Mathematics; // Provides Color4, Rect, SizeI etc.

using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI; // Provides Format, AlphaMode etc.
using SharpGen.Runtime; // Required for SharpGenException

using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

using D2DFactoryType = Vortice.Direct2D1.FactoryType;
using SizeI = Vortice.Mathematics.SizeI;
using Rect = Vortice.Mathematics.Rect;

using DirectUI;
using Vortice.DCommon;

namespace DirectUI;

public class Direct2DAppWindow : Win32Window
{
    // --- Core Factories ---
    private ID2D1Factory1? _d2dFactory;
    private IDWriteFactory? _dwriteFactory;

    // --- Render Target ---
    private ID2D1HwndRenderTarget? _renderTarget;

    // --- UI Elements ---
    private Button? _myTestButton;

    // --- Window State ---
    private Color4 _backgroundColor = new(0.1f, 0.1f, 0.15f, 1.0f);
    private bool _graphicsInitialized = false;

    // --- Input State Tracking ---
    private Vector2 _currentMousePos = new(-1, -1);
    private bool _isLeftMouseButtonDown = false;
    private bool _wasLeftMouseClickedThisFrame = false;

    public Direct2DAppWindow(string title = "Vortice DirectUI Window", int width = 800, int height = 600)
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
                Console.WriteLine("Graphics not ready in OnPaint, attempting initialization...");
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

            // --- Process and Draw UI Elements ---
            if (_myTestButton is not null)
            {
                // Use the RENAMED method UI.DoButton
                bool clicked = UI.DoButton(drawingContext, inputState, _myTestButton);

                if (clicked)
                {
                    Console.WriteLine($"Button '{_myTestButton.Text}' was clicked!");
                    _backgroundColor = new Color4((float)Random.Shared.NextDouble(), 0.5f, 0.5f, 1.0f);
                }
            }

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
            CleanupGraphics();
        }
        finally
        {
            _wasLeftMouseClickedThisFrame = false;
        }
    }

    // --- Event Handlers ---

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
            Console.WriteLine("Graphics not initialized during OnSize. Attempting Graphics initialization...");
            InitializeGraphics();
        }
    }

    protected override void OnMouseMove(int x, int y)
    {
        _currentMousePos = new Vector2(x, y);
    }

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
        if (keyCode == NativeMethods.VK_ESCAPE)
        {
            Close();
        }
    }

    protected override bool OnClose()
    {
        Console.WriteLine("Close requested.");
        return true;
    }

    // --- Graphics Management ---

    private bool InitializeGraphics()
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

            var dxgiPixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, (Vortice.DCommon.AlphaMode)Vortice.DXGI.AlphaMode.Premultiplied);
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

            _myTestButton = new Button
            {
                Position = new Vector2(50, 50),
                Size = new Vector2(150, 40),
                Text = "زنتو گاییدم وای"
            };
            _myTestButton.Clicked += (sender) => {
                Console.WriteLine($"Event Handler: Button '{sender.Text}' Received Click!");
            };
            _myTestButton.MouseEntered += (sender) => Console.WriteLine($"Event Handler: Mouse entered Button '{sender.Text}'");
            _myTestButton.MouseExited += (sender) => Console.WriteLine($"Event Handler: Mouse exited Button '{sender.Text}'");

            Console.WriteLine($"Vortice Graphics initialized successfully for HWND {Handle}.");
            _graphicsInitialized = true;
            return true;
        }
        catch (SharpGenException ex)
        {
            Console.WriteLine($"Graphics Initialization failed (SharpGenException): {ex.Message} HRESULT: {ex.ResultCode}");
            CleanupGraphics();
            _graphicsInitialized = false;
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Graphics Initialization failed (General Exception): {ex}");
            CleanupGraphics();
            _graphicsInitialized = false;
            return false;
        }
    }

    private void CleanupGraphics()
    {
        bool resourcesExisted = _d2dFactory is not null || _renderTarget is not null;
        if (resourcesExisted)
            Console.WriteLine("Cleaning up Vortice Graphics resources...");

        UI.CleanupResources(); // Cleanup UI cache

        _renderTarget?.Dispose(); _renderTarget = null;
        _dwriteFactory?.Dispose(); _dwriteFactory = null;
        _d2dFactory?.Dispose(); _d2dFactory = null;

        _graphicsInitialized = false;

        if (resourcesExisted)
            Console.WriteLine("Finished cleaning graphics resources.");
    }

    // --- Helpers ---

    private SizeI GetClientRectSize()
    {
        if (Handle != nint.Zero && NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            int width = Math.Max(1, r.right - r.left);
            int height = Math.Max(1, r.bottom - r.top);
            return new SizeI(width, height);
        }

        int baseWidth = Math.Max(1, Width);
        int baseHeight = Math.Max(1, Height);
        if (Handle != nint.Zero)
        {
            Console.WriteLine($"GetClientRect failed. Falling back to base size: {baseWidth}x{baseHeight}");
        }
        return new SizeI(baseWidth, baseHeight);
    }
}