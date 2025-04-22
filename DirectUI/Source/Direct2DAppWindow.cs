// Direct2DAppWindow.cs - Resolved AlphaMode ambiguity
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
using Vortice.DCommon; // This using caused the ambiguity

// Explicitly qualify PixelFormat from DCommon if needed elsewhere,
// but for the RenderTargetProperties, we need DXGI.PixelFormat.
// using DCommonPixelFormat = Vortice.DCommon.PixelFormat;

namespace DirectUI; // File-scoped namespace

public class Direct2DAppWindow : Win32Window
{
    private ID2D1Factory1? _d2dFactory;
    private IDWriteFactory? _dwriteFactory;
    private ID2D1HwndRenderTarget? _renderTarget;

    private ID2D1SolidColorBrush? _textBrush;
    private ID2D1SolidColorBrush? _buttonBackgroundBrush;
    private ID2D1SolidColorBrush? _buttonHoverBrush;

    private IDWriteTextFormat? _buttonTextFormat;

    private Color4 _backgroundColor = new(0.1f, 0.1f, 0.15f, 1.0f);

    private Vector2 _currentMousePos = new(-1, -1);
    private bool _isLeftMouseButtonDown = false;
    private bool _wasLeftMouseClickedThisFrame = false;

    private bool _graphicsInitialized = false;

    public Direct2DAppWindow(string title = "Vortice D2D UI Window", int width = 800, int height = 600)
        : base(title, width, height)
    { }

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

    protected override void OnPaint()
    {
        if (!_graphicsInitialized || _renderTarget is null)
        {
            if (!_graphicsInitialized && Handle != nint.Zero)
            {
                InitializeGraphics();
                if (!_graphicsInitialized || _renderTarget is null) return;
            }
            else
            {
                return;
            }
        }

        try
        {
            _renderTarget.BeginDraw();
            _renderTarget.Clear(_backgroundColor);

            float buttonLeft = 50;
            float buttonTop = 50;
            float buttonWidth = 150;
            float buttonHeight = 40;
            var buttonBounds = new Rect(buttonLeft, buttonTop, buttonLeft + buttonWidth, buttonTop + buttonHeight);
            string buttonText = "Vortice Btn";
            string buttonId = "MyVorticeButton";

            bool clicked = DoButton(buttonId, buttonText, buttonBounds);

            if (clicked)
            {
                Console.WriteLine($"Button '{buttonId}' was clicked!");
                _backgroundColor = new Color4((float)Random.Shared.NextDouble(), 0.5f, 0.5f, 1.0f);
            }

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

    protected override void OnSize(int width, int height)
    {
        if (_graphicsInitialized && _renderTarget is not null)
        {
            try
            {
                var newPixelSize = new SizeI(width, height);
                _renderTarget.Resize(newPixelSize);
                Console.WriteLine($"Resized render target to {width}x{height}");
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
            Console.WriteLine("Graphics not initialized. Attempting Graphics initialization during OnSize...");
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
        return true;
    }

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

            // Explicitly qualify PixelFormat and AlphaMode to resolve ambiguity
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

            _textBrush = _renderTarget.CreateSolidColorBrush(Colors.White);
            _buttonBackgroundBrush = _renderTarget.CreateSolidColorBrush(new Color4(0.2f, 0.2f, 0.8f, 1.0f));
            _buttonHoverBrush = _renderTarget.CreateSolidColorBrush(new Color4(0.4f, 0.4f, 0.9f, 1.0f));

            if (_textBrush is null || _buttonBackgroundBrush is null || _buttonHoverBrush is null)
            {
                throw new InvalidOperationException("Failed to create one or more brushes.");
            }

            _buttonTextFormat = _dwriteFactory.CreateTextFormat("Segoe UI", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 16.0f);
            if (_buttonTextFormat is null)
            {
                throw new InvalidOperationException("Text format creation failed.");
            }

            _buttonTextFormat.TextAlignment = TextAlignment.Center;
            _buttonTextFormat.ParagraphAlignment = ParagraphAlignment.Center;

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
        if (_d2dFactory is not null || _renderTarget is not null)
            Console.WriteLine("Cleaning up Vortice Graphics resources...");

        _buttonTextFormat?.Dispose();
        _buttonTextFormat = null;
        _buttonHoverBrush?.Dispose();
        _buttonHoverBrush = null;
        _buttonBackgroundBrush?.Dispose();
        _buttonBackgroundBrush = null;
        _textBrush?.Dispose();
        _textBrush = null;

        _renderTarget?.Dispose();
        _renderTarget = null;

        _dwriteFactory?.Dispose();
        _dwriteFactory = null;
        _d2dFactory?.Dispose();
        _d2dFactory = null;

        _graphicsInitialized = false;
        // Console.WriteLine("Finished cleaning graphics resources."); // Optional: Keep this if you prefer
    }


    private bool DoButton(string id, string text, Rect bounds)
    {
        if (_renderTarget is null || _buttonBackgroundBrush is null || _buttonHoverBrush is null ||
            _textBrush is null || _buttonTextFormat is null)
        {
            // Console.WriteLine($"Warning: DoButton called with null resources (ID: {id}).");
            return false;
        }

        bool isHovering = bounds.Contains(_currentMousePos.X, _currentMousePos.Y);
        bool wasClicked = false;

        ID2D1SolidColorBrush backgroundBrush = isHovering
            ? _buttonHoverBrush
            : _buttonBackgroundBrush;

        if (isHovering && _wasLeftMouseClickedThisFrame)
        {
            wasClicked = true;
        }

        try
        {
            _renderTarget.FillRectangle(bounds, backgroundBrush);
            _renderTarget.DrawRectangle(bounds, _textBrush);
            _renderTarget.DrawText(text, _buttonTextFormat, bounds, _textBrush);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == D2D.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught SharpGenException in DoButton for ID: {id}): {ex.Message}");
            _graphicsInitialized = false;
            CleanupGraphics();
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing button (ID: {id}): {ex}");
            _graphicsInitialized = false;
            CleanupGraphics();
            return false;
        }

        return wasClicked;
    }


    private SizeI GetClientRectSize()
    {
        if (Handle != nint.Zero && NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            int width = Math.Max(0, r.right - r.left);
            int height = Math.Max(0, r.bottom - r.top);
            return new SizeI(width, height);
        }

        int baseWidth = Math.Max(0, Width); // Use property from base class
        int baseHeight = Math.Max(0, Height); // Use property from base class
        // Log only if handle is non-zero but GetClientRect failed
        if (Handle != nint.Zero)
        {
            Console.WriteLine($"GetClientRect failed. Falling back to base size: {baseWidth}x{baseHeight}");
        }
        return new SizeI(baseWidth, baseHeight);
    }
}